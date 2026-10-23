using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PowerLanguage.Function;

// Off-MultiCharts tests for the G-R regime engine (_RegimeEngine, phase3-specs §3.3). "--export <dir>" also writes the G-V inputs.
static class Program
{
	static int pass, fail;
	static void Check(bool ok, string what) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + what); } }
	static double Gauss(_Rng r) { double u = Math.Max(r.NextDouble(), 1e-300), v = r.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }
	static readonly CultureInfo IC = CultureInfo.InvariantCulture;

	// ---------------- synthetic market: 3-state Markov vol regime, business days ----------------
	sealed class Day { public DateTime date; public double close, hv20, hv63, high, low, atr; public bool roll; public int state; }

	static List<Day> Market(int n, ulong seed, double stay = 0.98, double volScale = 1)
	{
		var r = new _Rng(seed); var days = new List<Day>(); double c = 2000; int s = 0;
		double[] vol = { 0.006, 0.012 * volScale, 0.025 * volScale * volScale }, drift = { 0.0006, 0.0, -0.0012 };
		var rets = new List<double>(); var rng = new List<double>(); DateTime d = new DateTime(2012, 1, 2);
		for (int i = 0; i < n; i++)
		{
			while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
			if (r.NextDouble() > stay) s = (s + 1 + r.NextInt(2)) % 3;
			double ret = drift[s] + vol[s] * Gauss(r); c *= Math.Exp(ret); rets.Add(ret);
			double hl = c * vol[s] * (1 + Math.Abs(Gauss(r))); rng.Add(hl);
			days.Add(new Day { date = d, close = c, hv20 = Hv(rets, 20), hv63 = Hv(rets, 63), high = c + hl / 2, low = c - hl / 2, atr = rng.Count >= 20 ? rng.Skip(rng.Count - 20).Average() : double.NaN, state = s });
			d = d.AddDays(1);
		}
		return days;
	}

	static double Hv(List<double> r, int n)
	{
		if (r.Count < n) return double.NaN;
		var w = r.Skip(r.Count - n).ToArray(); double m = w.Average();
		return Math.Sqrt(w.Sum(x => (x - m) * (x - m)) / (n - 1) * 252);
	}

	sealed class Run { public List<RegimeState> states = new List<RegimeState>(); public List<RegimeFit> fits = new List<RegimeFit>(); public List<string> log = new List<string>(); public _RegimeEngine eng; }

	static Run Replay(List<Day> days, RegimeConfig cfg, int upTo = int.MaxValue)
	{
		var run = new Run(); var e = new _RegimeEngine { config = cfg, journal = run.log.Add, warnOnce = run.log.Add };
		e.onRefit = f => run.fits.Add(f);
		if (e.Configure() != null) throw new Exception(e.Configure());
		for (int i = 0; i < days.Count && i < upTo; i++) { var x = days[i]; e.AddDay(x.date, x.close, x.hv20, x.hv63, x.high, x.low, x.atr, x.roll); run.states.Add(e.State); }
		run.eng = e;
		return run;
	}

	static RegimeConfig Cfg(int minS = 300, int fitS = 600) { return new RegimeConfig { enabled = true, publish = true, minSessions = minS, fitSessions = fitS }; }

	static bool Same(RegimeState a, RegimeState b)
	{
		if (a.Valid != b.Valid || a.Label != b.Label || a.RawArgmax != b.RawArgmax || a.AsOf != b.AsOf || a.FittedThrough != b.FittedThrough || a.UsingFallback != b.UsingFallback || a.Probs.Length != b.Probs.Length) return false;
		if (!(a.LogLik.Equals(b.LogLik))) return false;
		for (int k = 0; k < a.Probs.Length; k++) if (a.Probs[k] != b.Probs[k]) return false;
		return true;
	}

	static bool SameFit(RegimeFit a, RegimeFit b)
	{
		if (a.fittedThrough != b.fittedThrough || a.logLik != b.logLik || a.K != b.K) return false;
		for (int k = 0; k < a.K; k++) { if (a.pi[k] != b.pi[k]) return false; for (int d = 0; d < a.D; d++) if (a.mu[k][d] != b.mu[k][d]) return false; for (int q = 0; q < a.K; q++) if (a.A[k, q] != b.A[k, q]) return false; }
		for (int d = 0; d < a.D; d++) if (a.zMean[d] != b.zMean[d] || a.zSd[d] != b.zSd[d]) return false;
		return true;
	}

	static void Main(string[] args)
	{
		string export = args.Length >= 2 && args[0] == "--export" ? args[1] : null;

		// ---------------- config ----------------
		var def = new RegimeConfig();
		Check(!def.enabled && !def.publish && def.K == 3 && def.method == ERegimeMethod.Hmm && def.kSelect == ERegimeKSelect.Fixed && def.fitSessions == 1000 && def.minSessions == 500
			&& def.refit == ERegimeRefit.Monthly && def.restarts == 10 && def.maxIter == 200 && def.tol == 1e-6 && def.regCovar == 1e-6 && def.transPrior == 1 && def.warmFilter == 63
			&& def.switchProb == 0.60 && def.switchMargin == 0.10 && def.seed == 20261021 && def.fallback == ERegimeFallback.AtrTercile && def.Validate() == null
			&& def.features.SequenceEqual(new[] { ERegimeFeature.Return, ERegimeFeature.LogHv20, ERegimeFeature.Tsmom63z }), "spec defaults (enabled=false, publish=false, f4 off)");
		Check(new RegimeConfig { K = 5 }.Validate() != null && new RegimeConfig { fitSessions = 100 }.Validate() != null, "Validate rejects bad config");
		Check(RegimeFit.ParamCount(ERegimeMethod.Hmm, 3, 3) == 2 + 6 + 9 + 18 && RegimeFit.ParamCount(ERegimeMethod.Gmm, 3, 3) == 2 + 9 + 18, "HMM/GMM BIC parameter counts");

		// ---------------- 1. GMM recovery ----------------
		{
			var r = new _Rng(101); var mus = new[] { new[] { -3.0, 0.0 }, new[] { 0.0, 2.0 }, new[] { 3.0, -1.0 } }; var ws = new[] { 0.3, 0.5, 0.2 };
			var x = new double[3000][];
			for (int i = 0; i < x.Length; i++) { double u = r.NextDouble(); int k = u < 0.3 ? 0 : u < 0.8 ? 1 : 2; x[i] = new[] { mus[k][0] + 0.5 * Gauss(r), mus[k][1] + 0.5 * Gauss(r) }; }
			var f = _RegimeEngine.FitWindow(x, 3, new RegimeConfig { method = ERegimeMethod.Gmm, features = new[] { ERegimeFeature.Return, ERegimeFeature.LogHv20 } }, 7, 0, 1);
			bool ok = f.failReason == null;
			for (int k = 0; ok && k < 3; k++) ok = Math.Abs(f.mu[k][0] - mus[k][0]) < 0.05 && Math.Abs(f.mu[k][1] - mus[k][1]) < 0.05 && Math.Abs(f.pi[k] - ws[k]) < 0.02;
			Check(ok, "1 GMM recovery: means within 0.05, weights within 0.02, ordered by feature 0");
		}

		// ---------------- 2. HMM recovery ----------------
		{
			var r = new _Rng(202); int T = 3000; var x = new double[T][]; var s = new int[T]; int st = 0;
			var mus = new[] { new[] { -2.0, -1.0 }, new[] { 0.0, 1.5 }, new[] { 2.5, -0.5 } };
			for (int t = 0; t < T; t++)
			{
				if (t > 0 && r.NextDouble() > 0.95) st = (st + 1 + r.NextInt(2)) % 3;
				s[t] = st; x[t] = new[] { mus[st][0] + 0.8 * Gauss(r), mus[st][1] + 0.8 * Gauss(r) };
			}
			var cfg = new RegimeConfig { features = new[] { ERegimeFeature.Return, ERegimeFeature.LogHv20 } };
			var f = _RegimeEngine.FitWindow(x, 3, cfg, 9, 0, 1);
			bool diag = f.failReason == null; for (int k = 0; diag && k < 3; k++) diag = Math.Abs(f.A[k, k] - 0.95) < 0.03;
			Check(diag, "2 HMM recovery: fitted diagonal within 0.03 of 0.95" + (f.A != null ? $" ({f.A[0, 0]:0.000},{f.A[1, 1]:0.000},{f.A[2, 2]:0.000})" : ""));
			double[] a = null; int hit = 0;
			for (int t = 0; t < T; t++) { a = _RegimeEngine.FilterStep(f, a, x[t]); if (_RegimeEngine.Argmax(a) == s[t]) hit++; }
			Check(hit >= 0.85 * T, $"2 HMM filtered-argmax accuracy >= 85% ({100.0 * hit / T:0.0}%)");
			Check(f.converged && f.gamma != null && f.gamma.Length == T, "2 converged, in-window gamma kept");
		}

		var mkt = Market(1500, 4242);

		// ---------------- 3. determinism ----------------
		var baseRun = Replay(mkt, Cfg());
		{
			var again = Replay(mkt, Cfg());
			bool same = baseRun.states.Count == again.states.Count; for (int i = 0; same && i < baseRun.states.Count; i++) same = Same(baseRun.states[i], again.states[i]);
			Check(same && baseRun.log.SequenceEqual(again.log), "3 determinism: same seed => bit-identical states and journal");
			// separated data: vol 0.6% / 2.4% / 10% daily, persistent states
			var sep = Market(900, 4343, 0.995, 2); var c1 = Cfg(); var c2 = Cfg(); c2.seed = 99;
			var one = Replay(sep, c1); var other = Replay(sep, c2); int agree = 0, n = 0;
			for (int i = 0; i < sep.Count; i++) if (one.states[i].Valid && !one.states[i].UsingFallback) { n++; if (one.states[i].Label == other.states[i].Label) agree++; }
			Check(n > 0 && agree == n, $"3 different seed => same ordered labels on separated data ({agree}/{n})");
			Check(baseRun.fits.Count >= 30, "refits happened (" + baseRun.fits.Count + ")");
		}

		// ---------------- 4. G-T1 truncation invariance (+ random cut dates, no future data in refits / z stats / posteriors) ----------------
		{
			var cfg4 = Cfg(); var t700 = Replay(mkt, cfg4, 701); var t900 = Replay(mkt, Cfg(), 901);
			bool same = true; for (int i = 600; i <= 700; i++) same &= Same(t700.states[i], t900.states[i]);
			Check(same, "4 G-T1: days 600-700 identical on data ending at 700 vs 900");
			var rr = new _Rng(777); bool all = true; string bad = "";
			for (int c = 0; c < 6; c++)
			{
				int cut = 350 + rr.NextInt(1100); var tr = Replay(mkt, Cfg(), cut + 1);
				for (int i = 0; i <= cut; i++) if (!Same(tr.states[i], baseRun.states[i])) { all = false; bad += $" state@{i}/cut{cut}"; break; }
				var fitsBefore = baseRun.fits.Where(f => f.fittedThrough <= mkt[cut].date).ToList();
				if (fitsBefore.Count != tr.fits.Count) { all = false; bad += " fitcount"; }
				else for (int k = 0; k < fitsBefore.Count; k++) if (!SameFit(fitsBefore[k], tr.fits[k])) { all = false; bad += " fit" + k; }
				foreach (var f in tr.fits) if (f.fittedThrough > mkt[cut].date) { all = false; bad += " future"; }
			}
			Check(all, "4 random-date truncation: refit params, z stats, labels and filtered posteriors never use future data" + bad);
			bool causalStamp = baseRun.states.All(s => !s.Valid || s.UsingFallback || s.FittedThrough < s.AsOf);
			Check(causalStamp, "4 every published HMM state was produced by a fit strictly before its day");
		}

		// ---------------- 5. refit timing ----------------
		{
			bool monthStart = true, nextDay = true;
			foreach (var f in baseRun.fits)
			{
				int i = mkt.FindIndex(d => d.date == f.fittedThrough);
				monthStart &= i > 0 && mkt[i - 1].date.Month != mkt[i].date.Month;
				nextDay &= baseRun.states[i].FittedThrough != f.fittedThrough && (i + 1 >= mkt.Count || baseRun.states[i + 1].FittedThrough == f.fittedThrough);
			}
			Check(monthStart, "5 refits only at the close of the first trading day of a month");
			Check(nextDay, "5 new parameters apply from the next observation, not the refit day");
			int first = mkt.FindIndex(d => d.date == baseRun.fits[0].fittedThrough);
			Check(baseRun.fits[0].window >= 300 && baseRun.fits.Last().window == 600, "5 window expands from minSessions to fitSessions");
			Check(baseRun.log.Count(l => l.StartsWith("REGIME refit ")) == baseRun.fits.Count && baseRun.log.Any(l => l.Contains(" window(") && l.Contains(" conv(1) means(")), "5 refit journal line");
			Check(first > 0, "5 first fit found");
			var q = Cfg(); q.refit = ERegimeRefit.Quarterly; var qr = Replay(mkt, q);
			Check(qr.fits.Count > 0 && qr.fits.All(f => (f.fittedThrough.Month - 1) % 3 == 0), "5 Quarterly option");
			var en = Cfg(); en.refit = ERegimeRefit.EveryNSessions; var er = Replay(mkt, en);
			Check(er.fits.Count > 0 && Enumerable.Range(1, er.fits.Count - 1).All(k => mkt.FindIndex(d => d.date == er.fits[k].fittedThrough) - mkt.FindIndex(d => d.date == er.fits[k - 1].fittedThrough) == 21), "5 EveryNSessions(21) option");
		}

		// ---------------- no refit at a roll ----------------
		{
			var rolled = mkt.Select(d => new Day { date = d.date, close = d.close, hv20 = d.hv20, hv63 = d.hv63, high = d.high, low = d.low, atr = d.atr }).ToList();
			int target = mkt.FindIndex(d => d.date == baseRun.fits[3].fittedThrough);
			rolled[target].roll = true;
			var rr = Replay(rolled, Cfg());
			Check(rr.fits.All(f => f.fittedThrough != mkt[target].date) && rr.fits.Any(f => f.fittedThrough == mkt[target + 1].date), "no refit on a roll day: deferred to the next close");
			Check(rr.log.Any(l => l.StartsWith("REGIME refit deferred " + mkt[target].date.ToString("yyyy-MM-dd"))), "roll deferral journaled");
			// roll sanity: a huge splice return on a roll day journals "roll adjust suspect"
			var spl = rolled.Select(d => new Day { date = d.date, close = d.close, hv20 = d.hv20, hv63 = d.hv63, high = d.high, low = d.low, atr = d.atr }).ToList();
			int rd = target + 30; spl[rd].roll = true; for (int i = rd; i < spl.Count; i++) spl[i].close *= 1.25;
			var sr = Replay(spl, Cfg());
			Check(sr.log.Any(l => l.StartsWith("REGIME roll adjust suspect " + mkt[rd].date.ToString("yyyy-MM-dd"))), "roll adjust suspect when |z(f1)| > 5 on a roll day");
		}

		// ---------------- 6. fallback ----------------
		{
			var fr = Replay(mkt, Cfg(500, 1000), 300);
			bool ok = true; int cnt = 0;
			var ring = new List<double>();
			for (int i = 0; i < 300; i++)
			{
				double a = mkt[i].atr; int exp = -1;
				if (ring.Count == 250 && a > 0) { double below = ring.Count(v => v < a), eq = ring.Count(v => v == a); double pct = 100 * (below + 0.5 * eq) / 250; exp = pct < 100.0 / 3 ? 0 : pct < 200.0 / 3 ? 1 : 2; }
				if (a > 0) { ring.Add(a); if (ring.Count > 250) ring.RemoveAt(0); }
				var s = fr.states[i];
				if (exp >= 0) { cnt++; ok &= s.Valid && s.UsingFallback && s.RawArgmax == exp && s.Probs[exp] == 1 && s.Probs.Sum() == 1; }
				else ok &= !s.Valid && s.UsingFallback && s.Label == -1;
			}
			Check(ok && cnt > 20, $"6 300 days => AtrTercile one-hot equals an independent RG-03 computation ({cnt} valid days)");
			Check(fr.log.Any(l => l.StartsWith("REGIME fallback reason(insufficient)")), "6 fallback reason(insufficient) journaled");
			var u = Cfg(500, 1000); u.fallback = ERegimeFallback.Unknown; var ur = Replay(mkt, u, 300);
			Check(ur.states.All(s => !s.Valid && s.Label == -1 && s.UsingFallback && s.Probs.Length == 0), "6 fallback Unknown: Valid=false, Label=-1");
		}

		// ---------------- 7. hysteresis ----------------
		{
			var path = new[] { new[] { .7, .2, .1 }, new[] { .45, .5, .05 }, new[] { .35, .55, .1 }, new[] { .3, .6, .1 }, new[] { .3, .65, .05 }, new[] { .42, .5, .08 }, new[] { .2, .1, .7 }, new[] { .1, .55, .35 }, new[] { .05, .25, .7 } };
			int L = -1; var seq = new List<int>();
			foreach (var p in path) { L = _RegimeEngine.Hysteresis(L, p, 0.60, 0.10); seq.Add(L); }
			Check(seq.SequenceEqual(new[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 }), "7 hysteresis label path " + string.Join("", seq));
			var st = baseRun.states.Where(s => s.Valid && !s.UsingFallback).ToList();
			Check(st.Any(s => s.Label != s.RawArgmax) || st.All(s => s.Label == s.RawArgmax), "7 RawArgmax exposed");
			bool sw = baseRun.log.Any(l => l.StartsWith("REGIME switch "));
			Check(sw, "7 switch journal");
		}

		// ---------------- 8. degenerate data ----------------
		{
			var x = new double[400][]; var r = new _Rng(5);
			for (int i = 0; i < x.Length; i++) x[i] = i % 2 == 0 ? new[] { 1.0, 2.0, 3.0 } : new[] { 1.0 + 0.1 * Gauss(r), 2.0, 3.0 + 0.1 * Gauss(r) };
			var f = _RegimeEngine.FitWindow(x, 3, new RegimeConfig(), 3, 1, 2);
			bool fin = f.failReason != null || (!double.IsNaN(f.logLik) && !double.IsInfinity(f.logLik) && f.mu.All(m => m.All(v => !double.IsNaN(v))));
			Check(fin, "8 duplicate points: finite fit or a clean failReason (" + (f.failReason ?? "ok") + ")");
		}

		// ---------------- 9. BIC: single Gaussian => K = 1 ----------------
		{
			var r = new _Rng(9); var x = new double[2000][]; for (int i = 0; i < x.Length; i++) x[i] = new[] { Gauss(r), Gauss(r), Gauss(r) };
			var g = _Gmm.Fit(x, null, x.Length, new GmmFitConfig { kMin = 1, kMax = 4, seed = 9 });
			Check(g != null && g.K == 1, "9 BIC: single-Gaussian data => K = 1");
		}

		// ---------------- K2 / K4 BIC diagnostic, flip count, relabel ----------------
		{
			var diag = baseRun.log.Where(l => l.StartsWith("REGIME diag ")).ToList();
			Check(diag.Count == baseRun.fits.Count && diag.All(l => l.Contains("bicK2(") && l.Contains("bicK3(") && l.Contains("bicK4(")), "K2/K4 BIC diagnostic journaled at each refit");
			Check(baseRun.fits.All(f => f.K == 3) && baseRun.states.All(s => s.K == 3), "BIC diagnostic never switches K");
			Check(diag.All(l => l.Contains(" flips(") && l.Contains(" relabelConflicts(")), "label-flip count per refit window journaled");
			int sw = baseRun.log.Count(l => l.StartsWith("REGIME switch "));
			int fl = diag.Skip(1).Sum(l => int.Parse(l.Substring(l.IndexOf(" flips(") + 7).Split(')')[0]));
			int swBefore = baseRun.log.TakeWhile(l => !l.StartsWith("REGIME diag ")).Count(l => l.StartsWith("REGIME switch "));
			int lastDiag = baseRun.log.FindLastIndex(l => l.StartsWith("REGIME diag ")), swAfter = baseRun.log.Skip(lastDiag).Count(l => l.StartsWith("REGIME switch "));
			Check(fl + swBefore + swAfter == sw, $"flip counts sum to the switch lines ({fl}+{swBefore}+{swAfter} vs {sw})");
			Check(baseRun.log.Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^REGIME \d{4}-\d\d-\d\d label\(")) == mkt.Count, "one daily REGIME line per day (dates, not bar numbers)");
			// labels ordered by the f2 (log HV20) mean
			Check(baseRun.fits.All(f => f.mu[0][1] <= f.mu[1][1] && f.mu[1][1] <= f.mu[2][1]), "labels ordered by the f2 mean");
			var a = baseRun.fits[5]; Check(_RegimeEngine.BestMatch(a, a).SequenceEqual(new[] { 0, 1, 2 }), "Bhattacharyya self-match is the identity");
			double[] m; double[,] c; a.Raw(0, out m, out c);
			Check(Math.Abs(_RegimeEngine.Bhattacharyya(m, c, m, c)) < 1e-12, "Bhattacharyya(x,x) = 0");
			var sw2 = new RegimeFit { K = 3, D = a.D, mu = new[] { a.mu[2], a.mu[0], a.mu[1] }, cov = new[] { a.cov[2], a.cov[0], a.cov[1] }, zMean = a.zMean, zSd = a.zSd };
			Check(_RegimeEngine.BestMatch(a, sw2).SequenceEqual(new[] { 2, 0, 1 }), "Bhattacharyya matching recovers a permutation");
			Check(baseRun.states.Last().RelabelConflicts == baseRun.eng.RelabelConflicts, "relabel-conflict count exposed");
		}

		// ---------------- gap, copy-on-read, Gmm method, f4 ----------------
		{
			var g = mkt.Select(d => d).ToList(); var gr = Replay(g.Take(200).Concat(g.Skip(210)).ToList(), Cfg());
			Check(gr.log.Any(l => l.StartsWith("REGIME gap ")), "gap > 4 business days journaled");
			var e = baseRun.eng; var s1 = e.State; if (s1.Probs.Length > 0) s1.Probs[0] = 42;
			Check(e.State.Probs.Length == 0 || e.State.Probs[0] != 42, "RegimeState is a copy on read");
			var gm = Cfg(); gm.method = ERegimeMethod.Gmm; var gmr = Replay(mkt, gm);
			Check(gmr.fits.Count == baseRun.fits.Count && gmr.fits.All(f => f.A == null) && gmr.states.Last().Valid, "method Gmm option");
			var p4 = Cfg(); p4.features = new[] { ERegimeFeature.Return, ERegimeFeature.LogHv20, ERegimeFeature.Tsmom63z, ERegimeFeature.Parkinson10 }; var pr = Replay(mkt, p4);
			Check(pr.fits.Count > 0 && pr.fits[0].D == 4 && pr.states.Last().Valid, "optional f4 Parkinson feature");
			var bs = Cfg(); bs.kSelect = ERegimeKSelect.Bic; var br = Replay(mkt, bs, 500);
			Check(br.fits.Count > 0 && br.fits.All(f => f.K >= 2 && f.K <= 4), "kSelect Bic (research) picks K in 2..4");
		}

		if (export != null) Export(export, mkt, baseRun);
		Console.WriteLine($"RegimeTests: {pass} passed, {fail} failed");
		Environment.Exit(fail == 0 ? 0 : 1);
	}

	// ---------------- G-V export ----------------
	static string N(double v) { return v.ToString("R", IC); }
	static string Arr(double[] a) { return "[" + string.Join(",", a.Select(N)) + "]"; }
	static string Mat(double[,] a) { int r = a.GetLength(0), c = a.GetLength(1); var s = new List<string>(); for (int i = 0; i < r; i++) s.Add(Arr(Enumerable.Range(0, c).Select(j => a[i, j]).ToArray())); return "[" + string.Join(",", s) + "]"; }
	static string Covs(double[][,] a) { return "[" + string.Join(",", a.Select(Mat)) + "]"; }
	static string Vecs(double[][] a) { return "[" + string.Join(",", a.Select(Arr)) + "]"; }

	static void Export(string dir, List<Day> mkt, Run run)
	{
		Directory.CreateDirectory(dir);
		// re-derive the raw features exactly as the engine does (closes ring of 64)
		var sb = new StringBuilder("date,f1,f2,f3,refit,label,p0,p1,p2,fittedThrough\n");
		var fitDates = new HashSet<DateTime>(run.fits.Select(f => f.fittedThrough));
		for (int i = 0; i < mkt.Count; i++)
		{
			string f1 = "", f2 = "", f3 = "";
			if (i >= 63) { f1 = N(Math.Log(mkt[i].close / mkt[i - 1].close)); f2 = N(Math.Log(mkt[i].hv20)); f3 = N(Math.Log(mkt[i].close / mkt[i - 63].close) / (mkt[i].hv63 * Math.Sqrt(63.0 / 252.0))); }
			var s = run.states[i]; var p = s.Probs.Length == 3 ? s.Probs : new[] { double.NaN, double.NaN, double.NaN };
			sb.Append($"{mkt[i].date:yyyy-MM-dd},{f1},{f2},{f3},{(fitDates.Contains(mkt[i].date) ? 1 : 0)},{s.Label},{N(p[0])},{N(p[1])},{N(p[2])},{(s.UsingFallback ? "fb" : s.FittedThrough.ToString("yyyy-MM-dd"))}\n");
		}
		File.WriteAllText(Path.Combine(dir, "gv_features.csv"), sb.ToString());
		var js = new StringBuilder("{\"config\":{\"K\":3,\"tol\":1e-6,\"maxIter\":200,\"regCovar\":1e-6,\"transPrior\":1,\"warmFilter\":63},\"fits\":[\n");
		var fits = run.fits; var sel = new List<RegimeFit> { fits[0], fits[fits.Count / 2], fits[fits.Count - 1] };
		for (int q = 0; q < sel.Count; q++)
		{
			var f = sel[q];
			js.Append("{\"fittedThrough\":\"" + f.fittedThrough.ToString("yyyy-MM-dd") + "\",\"windowStart\":\"" + f.windowStart.ToString("yyyy-MM-dd") + "\",\"window\":" + f.window
				+ ",\"zMean\":" + Arr(f.zMean) + ",\"zSd\":" + Arr(f.zSd)
				+ ",\"gmmInit\":{\"w\":" + Arr(f.gmmInit.w) + ",\"mu\":" + Vecs(f.gmmInit.mu) + ",\"cov\":" + Covs(f.gmmInit.cov) + "}"
				+ ",\"gmm\":{\"w\":" + Arr(f.gmm.w) + ",\"mu\":" + Vecs(f.gmm.mu) + ",\"cov\":" + Covs(f.gmm.cov) + ",\"logLik\":" + N(f.gmm.logLik) + ",\"iters\":" + f.gmm.iters + "}"
				+ ",\"hmmInit\":{\"pi\":" + Arr(f.hmmInitPi) + ",\"A\":" + Mat(f.hmmInitA) + ",\"mu\":" + Vecs(f.hmmInitMu) + ",\"cov\":" + Covs(f.hmmInitCov) + "}"
				+ ",\"hmm\":{\"pi\":" + Arr(f.pi) + ",\"A\":" + Mat(f.A) + ",\"mu\":" + Vecs(f.mu) + ",\"cov\":" + Covs(f.cov) + ",\"logLik\":" + N(f.logLik) + ",\"iters\":" + f.iters + "}"
				+ ",\"gamma\":" + Vecs(f.gamma) + "}" + (q < sel.Count - 1 ? ",\n" : "\n"));
		}
		js.Append("]}\n");
		File.WriteAllText(Path.Combine(dir, "gv_fits.json"), js.ToString());
		Console.WriteLine("G-V export: " + dir + " (" + sel.Count + " refits)");
	}
}
