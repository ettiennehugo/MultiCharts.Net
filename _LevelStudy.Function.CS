using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// A2-33 — level persistence vs matched-control study (trading-idea-catalogue.md §3.0, Gate 0 G0-d; Keller B2 Ch 10.6).
	// Pure C#, no MultiCharts types (tests/LevelStudyTests). _CustomSignal hosts it lazily (LevelStudyConfig.enabled, default
	// false): the strategy registers each session's prior-session levels with levelStudyAdd(type, price, age, quality); the
	// harness adds `controls` matched control prices per level at 0.9-1.1x the level's distance from the reference price, follows
	// all of them over the session's Data1 bars and records touch, reaction R = max|P - p| / sigma over K bars after the first
	// touch, rejection asymmetry and volume attraction. Analyse() gives the paired edge (level - mean of its controls) per level
	// type, t-stat, Holm-adjusted p, quality terciles, edge by age with a half-life, a Kaplan-Meier "level death" curve per type
	// and the catalogue decision rule. Cox regression is not implemented (KM + age buckets instead).
	// ============================================================================================

	public sealed class LevelStudyConfig
	{
		public bool enabled = false;
		public int controls = 20;              // matched control prices per level
		public double distLo = 0.9, distHi = 1.1;
		public int reactionBars = 60;          // K bars of Data1 after the first touch (12 x 5m on a 1m chart)
		public int sigmaBars = 60;             // sigma = stdev of 1-bar close changes of the prior session x sqrt(reactionBars)
		public double touchTicks = 0;          // touch tolerance in ticks
		public int maxAge = 15;                // levels aged 1..maxAge sessions
		public double deathR = 1.0;            // KM: a touched level "dies" at the first age whose reaction R < deathR
		public int seed = 33;
		public DateTime from = DateTime.MinValue, to = DateTime.MaxValue;   // decision window (in-sample years); sessions outside are skipped
		public string csvPath = "";            // optional per-event CSV written at the end of the run
		public bool controlsEitherSide = false; // A2-33 "either side": each control gets a random sign (false = same side as the level)
		public bool reactionAfterTouch = false; // true: R is measured over the K bars AFTER the touch bar (k = 1..K), not including it

		public string Validate()
		{
			var e = new List<string>();
			if (controls < 1) e.Add($"controls={controls}");
			if (!(distLo > 0 && distHi >= distLo)) e.Add($"distLo={distLo} distHi={distHi}");
			if (reactionBars < 1) e.Add($"reactionBars={reactionBars}");
			if (sigmaBars < 2) e.Add($"sigmaBars={sigmaBars}");
			if (touchTicks < 0) e.Add($"touchTicks={touchTicks}");
			if (maxAge < 1) e.Add($"maxAge={maxAge}");
			if (!(deathR > 0)) e.Add($"deathR={deathR}");
			if (from > to) e.Add("from > to");
			return e.Count == 0 ? null : "LevelStudyConfig invalid: " + string.Join(", ", e);
		}
	}

	/// <summary>One followed price (a level or one of its controls) for one session.</summary>
	public sealed class LevelEvent
	{
		public DateTime day; public string type = ""; public string key = ""; public int age; public double quality = double.NaN;
		public bool control; public int levelNo; public double price, refPrice, sigma;
		public bool touched; public int touchBar = -1; public int fromAbove;   // +1 approached from above, -1 from below
		public double reaction = double.NaN, asymmetry = double.NaN, volAttraction = double.NaN; public int barsSeen;
		internal double maxBounce, maxThrough, volSum; internal int volBars;
	}

	public sealed class LevelTypeResult
	{
		public string type = ""; public int levels, touchedLevels;
		public double touchEdge = double.NaN, touchT = double.NaN;     // touch rate: level - controls
		public double edge = double.NaN, t = double.NaN, p = double.NaN, pHolm = double.NaN;   // reaction R, paired
		public double asymEdge = double.NaN, volEdge = double.NaN;
		public double[] edgeByAge = new double[0]; public int[] nByAge = new int[0]; public double[] tByAge = new double[0];
		public double halfLife = double.NaN;                           // sessions; NaN = no decay measured
		public double[] edgeByTercile = { double.NaN, double.NaN, double.NaN }; public double[] tByTercile = { double.NaN, double.NaN, double.NaN };
		public double[] kmSurvival = new double[0];                    // S(age) for age 1..maxAge
		public double kmMedianAge = double.NaN;
		public string verdict = "";
	}

	internal sealed class _LevelStudy
	{
		public LevelStudyConfig config = new LevelStudyConfig();
		public double tickSize = 0.25;
		public Action<string> journal;

		private readonly List<LevelEvent> m_all = new List<LevelEvent>();
		private readonly List<LevelEvent> m_open = new List<LevelEvent>();
		private DateTime m_day = DateTime.MinValue; private bool m_dayOn;
		private int m_bar; private double m_lastClose = double.NaN, m_sigma = double.NaN;
		private readonly List<double> m_prevDiffs = new List<double>(), m_curDiffs = new List<double>();
		private double m_volSum; private int m_volBars; private int m_levelNo;
		private double m_refOverride = double.NaN, m_sigmaOverride = double.NaN;

		public IReadOnlyList<LevelEvent> Events { get { return m_all; } }
		public string Configure() { return config.Validate(); }

		/// <summary>New trading day (call on the first Data1 bar of the session, before Bar()).</summary>
		public void BeginSession(DateTime day)
		{
			closeSession();
			m_prevDiffs.Clear(); m_prevDiffs.AddRange(m_curDiffs); m_curDiffs.Clear();
			m_sigma = sigmaOf(m_prevDiffs); m_refOverride = m_sigmaOverride = double.NaN;
			m_day = day.Date; m_dayOn = day >= config.from && day <= config.to; m_bar = 0; m_volSum = 0; m_volBars = 0;
		}

		/// <summary>Freeze this session's reference price (distance origin for the controls) and sigma (R denominator) at registration;
		/// NaN keeps the default (last close / prior-session close-change sigma). Call after BeginSession, before AddLevel.</summary>
		public void SetSessionReference(double refPrice, double sigma) { m_refOverride = refPrice; m_sigmaOverride = sigma; }

		/// <summary>Register a prior-session level for the current session (and its matched controls). Returns false when skipped.</summary>
		public bool AddLevel(string type, double price, int ageSessions, double quality = double.NaN, string key = null)
		{
			double refPx = double.IsNaN(m_refOverride) ? m_lastClose : m_refOverride, sig = double.IsNaN(m_sigmaOverride) ? m_sigma : m_sigmaOverride;
			if (!m_dayOn || ageSessions < 1 || ageSessions > config.maxAge || !(price > 0) || double.IsNaN(refPx) || !(sig > 0)) return false;
			double dist = price - refPx;
			int no = ++m_levelNo;
			var lv = make(type, key ?? (type + "@" + price.ToString("R", CultureInfo.InvariantCulture)), ageSessions, quality, false, no, price, refPx);
			lv.sigma = sig; m_open.Add(lv); m_all.Add(lv);
			var rnd = new Random(unchecked(config.seed * 397 ^ m_day.GetHashCode() ^ (type ?? "").GetHashCode() * 31 ^ price.GetHashCode()));
			for (int i = 0; i < config.controls; i++)
			{
				double u = config.distLo + rnd.NextDouble() * (config.distHi - config.distLo);
				double sign = config.controlsEitherSide && rnd.Next(2) == 0 ? -1 : 1;
				double cp = refPx + sign * dist * u;
				if (tickSize > 0) cp = Math.Round(cp / tickSize) * tickSize;
				var c = make(type, lv.key, ageSessions, quality, true, no, cp, refPx); c.sigma = sig;
				m_open.Add(c); m_all.Add(c);
			}
			return true;
		}

		private LevelEvent make(string type, string key, int age, double q, bool control, int no, double p, double refPx)
		{
			return new LevelEvent { day = m_day, type = type ?? "", key = key, age = age, quality = q, control = control, levelNo = no, price = p, refPrice = refPx, sigma = m_sigma };
		}

		/// <summary>One closed Data1 bar of the current session.</summary>
		public void Bar(double o, double h, double l, double c, double v)
		{
			if (!double.IsNaN(m_lastClose)) m_curDiffs.Add(c - m_lastClose);
			double prevClose = double.IsNaN(m_lastClose) ? o : m_lastClose;
			m_bar++; m_volSum += v; m_volBars++;
			double tol = config.touchTicks * tickSize;
			foreach (var e in m_open)
			{
				if (!e.touched)
				{
					if (l - tol <= e.price && e.price <= h + tol)
					{
						e.touched = true; e.touchBar = m_bar; e.fromAbove = prevClose >= e.price ? 1 : -1;
						double sessVol = m_volBars > 1 ? (m_volSum - v) / (m_volBars - 1) : v;
						e.volAttraction = sessVol > 0 ? 1.0 / sessVol : double.NaN;   // completed below
						if (config.reactionAfterTouch) continue;   // k = 1..K start on the next bar
					}
					else continue;
				}
				if (e.barsSeen >= config.reactionBars) continue;
				e.barsSeen++;
				double up = h - e.price, dn = e.price - l;
				double bounce = e.fromAbove > 0 ? up : dn, through = e.fromAbove > 0 ? dn : up;
				if (bounce > e.maxBounce) e.maxBounce = bounce;
				if (through > e.maxThrough) e.maxThrough = through;
				e.volSum += v; e.volBars++;
				double s = e.sigma;
				e.reaction = Math.Max(e.maxBounce, e.maxThrough) / s;
				e.asymmetry = (e.maxBounce - e.maxThrough) / s;
			}
			m_lastClose = c;
		}

		private void closeSession()
		{
			foreach (var e in m_open)
				if (e.touched && e.volBars > 0 && !double.IsNaN(e.volAttraction)) e.volAttraction = e.volSum / e.volBars * e.volAttraction;
				else if (e.touched) e.volAttraction = double.NaN;
			m_open.Clear();
		}

		private double sigmaOf(List<double> d)
		{
			int n = Math.Min(d.Count, config.sigmaBars); if (n < 2) return double.NaN;
			double m = 0; for (int i = d.Count - n; i < d.Count; i++) m += d[i]; m /= n;
			double s = 0; for (int i = d.Count - n; i < d.Count; i++) s += (d[i] - m) * (d[i] - m);
			return Math.Sqrt(s / (n - 1)) * Math.Sqrt(config.reactionBars);
		}

		/// <summary>Finish the open session and analyse everything recorded.</summary>
		public List<LevelTypeResult> Finish() { closeSession(); return Analyse(m_all, config); }

		// ---------------------------------------------------------------- analysis (static, pure)

		public static List<LevelTypeResult> Analyse(IList<LevelEvent> events, LevelStudyConfig cfg)
		{
			var res = new List<LevelTypeResult>();
			foreach (var g in events.GroupBy(e => e.type).OrderBy(x => x.Key, StringComparer.Ordinal))
			{
				var r = new LevelTypeResult { type = g.Key };
				var byLevel = g.GroupBy(e => new { e.day, e.levelNo }).Select(x => new { lv = x.FirstOrDefault(e => !e.control), ctl = x.Where(e => e.control).ToList() }).Where(x => x.lv != null).ToList();
				r.levels = byLevel.Count; r.touchedLevels = byLevel.Count(x => x.lv.touched);
				var touchD = byLevel.Where(x => x.ctl.Count > 0).Select(x => (x.lv.touched ? 1.0 : 0.0) - x.ctl.Average(c => c.touched ? 1.0 : 0.0)).ToList();
				meanT(touchD, out r.touchEdge, out r.touchT);
				var pairs = byLevel.Select(x => new { x.lv, d = pairDiff(x.lv, x.ctl, e => e.reaction), a = pairDiff(x.lv, x.ctl, e => e.asymmetry), v = pairDiff(x.lv, x.ctl, e => e.volAttraction) }).Where(x => !double.IsNaN(x.d)).ToList();
				meanT(pairs.Select(x => x.d).ToList(), out r.edge, out r.t);
				r.p = double.IsNaN(r.t) ? double.NaN : 2 * (1 - Phi(Math.Abs(r.t)));
				double dummy;
				meanT(pairs.Select(x => x.a).Where(x => !double.IsNaN(x)).ToList(), out r.asymEdge, out dummy);
				meanT(pairs.Select(x => x.v).Where(x => !double.IsNaN(x)).ToList(), out r.volEdge, out dummy);
				// by age
				r.edgeByAge = new double[cfg.maxAge]; r.nByAge = new int[cfg.maxAge]; r.tByAge = new double[cfg.maxAge];
				for (int a = 1; a <= cfg.maxAge; a++)
				{
					var da = pairs.Where(x => x.lv.age == a).Select(x => x.d).ToList();
					double ma, ta; meanT(da, out ma, out ta);
					r.nByAge[a - 1] = da.Count; r.edgeByAge[a - 1] = ma; r.tByAge[a - 1] = ta;
				}
				r.halfLife = HalfLife(r.edgeByAge, r.nByAge);
				// quality terciles (levels with a quality value)
				var q = pairs.Where(x => !double.IsNaN(x.lv.quality)).OrderBy(x => x.lv.quality).ToList();
				if (q.Count >= 3)
					for (int k = 0; k < 3; k++)
					{
						int a0 = q.Count * k / 3, a1 = q.Count * (k + 1) / 3;
						meanT(q.Skip(a0).Take(a1 - a0).Select(x => x.d).ToList(), out r.edgeByTercile[k], out r.tByTercile[k]);
					}
				// Kaplan-Meier level death over age, per level key
				var lives = new List<KeyValuePair<double, bool>>();
				foreach (var life in g.Where(e => !e.control).GroupBy(e => e.key))
				{
					var obs = life.OrderBy(e => e.age).ToList(); bool dead = false; int t = obs[obs.Count - 1].age;
					foreach (var e in obs) if (e.touched && e.reaction < cfg.deathR) { dead = true; t = e.age; break; }
					lives.Add(new KeyValuePair<double, bool>(t, dead));
				}
				r.kmSurvival = KaplanMeier(lives, cfg.maxAge);
				for (int a = 0; a < r.kmSurvival.Length; a++) if (r.kmSurvival[a] <= 0.5) { r.kmMedianAge = a + 1; break; }
				res.Add(r);
			}
			Holm(res);
			foreach (var r in res) r.verdict = Verdict(r);
			return res;
		}

		private static double pairDiff(LevelEvent lv, List<LevelEvent> ctl, Func<LevelEvent, double> f)
		{
			if (!lv.touched || double.IsNaN(f(lv))) return double.NaN;
			var c = ctl.Where(e => e.touched && !double.IsNaN(f(e))).Select(f).ToList();
			return c.Count == 0 ? double.NaN : f(lv) - c.Average();
		}

		public static void meanT(IList<double> x, out double mean, out double t)
		{
			mean = t = double.NaN; int n = x.Count; if (n == 0) return;
			mean = x.Average(); if (n < 2) return;
			double m = mean, sd = Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / (n - 1));
			t = sd > 0 ? mean / (sd / Math.Sqrt(n)) : double.NaN;
		}

		/// <summary>Standard normal CDF (Abramowitz-Stegun 7.1.26 via erf).</summary>
		public static double Phi(double z)
		{
			double x = Math.Abs(z) / Math.Sqrt(2), t = 1 / (1 + 0.3275911 * x);
			double erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
			return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
		}

		/// <summary>Holm step-down adjusted p across level types (NaN p stays NaN).</summary>
		public static void Holm(List<LevelTypeResult> r)
		{
			var v = r.Where(x => !double.IsNaN(x.p)).OrderBy(x => x.p).ToList(); int m = v.Count; double run = 0;
			for (int i = 0; i < m; i++) { run = Math.Max(run, Math.Min(1, (m - i) * v[i].p)); v[i].pHolm = run; }
		}

		/// <summary>Age (sessions) where the edge first falls to half of the age-1 edge (linear interpolation); NaN = no decay.</summary>
		public static double HalfLife(double[] edgeByAge, int[] n)
		{
			if (edgeByAge.Length == 0 || double.IsNaN(edgeByAge[0]) || !(edgeByAge[0] > 0)) return double.NaN;
			double half = edgeByAge[0] / 2, prev = edgeByAge[0]; int prevAge = 1;
			for (int a = 1; a < edgeByAge.Length; a++)
			{
				if (n[a] == 0 || double.IsNaN(edgeByAge[a])) continue;
				if (edgeByAge[a] <= half) return prevAge + (prev - half) / (prev - edgeByAge[a]) * (a + 1 - prevAge);
				prev = edgeByAge[a]; prevAge = a + 1;
			}
			return double.NaN;
		}

		/// <summary>Kaplan-Meier S(t) at t = 1..maxT for (time, died) observations (died=false = censored at time).</summary>
		public static double[] KaplanMeier(IList<KeyValuePair<double, bool>> obs, int maxT)
		{
			var s = new double[maxT]; double surv = 1;
			for (int t = 1; t <= maxT; t++)
			{
				int atRisk = obs.Count(o => o.Key >= t), deaths = obs.Count(o => o.Value && o.Key == t);
				if (atRisk > 0) surv *= 1 - (double)deaths / atRisk;
				s[t - 1] = surv;
			}
			return s;
		}

		/// <summary>Catalogue decision rule (A2-33): |t| &lt; 2 drop; t &gt; 3 with decay -> max age = half-life; edge only in the top tercile -> top tercile only.</summary>
		public static string Verdict(LevelTypeResult r)
		{
			if (double.IsNaN(r.t)) return "insufficient data";
			bool holmOk = !double.IsNaN(r.pHolm) && r.pHolm < 0.05;
			bool top = !double.IsNaN(r.tByTercile[2]) && r.tByTercile[2] > 2 && !(r.tByTercile[0] > 2) && !(r.tByTercile[1] > 2);
			if (Math.Abs(r.t) < 2) return top ? "trade top-quality tercile only" : "drop (edge ~ 0, |t| < 2)";
			if (r.edge < 0) return "drop (negative edge)";
			if (!holmOk) return top ? "trade top-quality tercile only" : "drop (not significant after Holm)";
			if (r.t > 3 && !double.IsNaN(r.halfLife)) return $"keep up to age {Math.Floor(r.halfLife)} (half-life {r.halfLife:0.0} sessions)";
			if (top) return "trade top-quality tercile only";
			return "keep";
		}

		public static string Summary(LevelTypeResult r)
		{
			var ic = CultureInfo.InvariantCulture;
			return string.Format(ic, "A2-33 {0}: levels({1}) touched({2}) touchEdge({3:0.000}, t {4:0.00}) edgeR({5:0.000}, t {6:0.00}, p {7:0.0000}, holm {8:0.0000}) asym({9:0.000}) vol({10:0.000}) halfLife({11:0.0}) terciles({12:0.000}/{13:0.000}/{14:0.000}) kmMedianAge({15}) -> {16}",
				r.type, r.levels, r.touchedLevels, r.touchEdge, r.touchT, r.edge, r.t, r.p, r.pHolm, r.asymEdge, r.volEdge, r.halfLife,
				r.edgeByTercile[0], r.edgeByTercile[1], r.edgeByTercile[2], double.IsNaN(r.kmMedianAge) ? ">" + r.kmSurvival.Length : r.kmMedianAge.ToString(ic), r.verdict);
		}

		public void WriteCsv(TextWriter w)
		{
			var ic = CultureInfo.InvariantCulture;
			w.WriteLine("day,type,key,age,quality,control,levelNo,price,refPrice,sigma,touched,touchBar,fromAbove,reaction,asymmetry,volAttraction,barsSeen");
			foreach (var e in m_all)
				w.WriteLine(string.Join(",", e.day.ToString("yyyy-MM-dd", ic), csv(e.type), csv(e.key), e.age.ToString(ic), e.quality.ToString("R", ic), e.control ? "1" : "0", e.levelNo.ToString(ic),
					e.price.ToString("R", ic), e.refPrice.ToString("R", ic), e.sigma.ToString("R", ic), e.touched ? "1" : "0", e.touchBar.ToString(ic), e.fromAbove.ToString(ic),
					e.reaction.ToString("R", ic), e.asymmetry.ToString("R", ic), e.volAttraction.ToString("R", ic), e.barsSeen.ToString(ic)));
		}
		private static string csv(string s) { return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }
	}
}
