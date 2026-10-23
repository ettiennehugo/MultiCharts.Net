using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Gate 0 (G0-b + level baseline) engine for _TestGate0. Pure C# (no MultiCharts types) so tests/Gate0Tests runs it
	// off-platform. It consumes CLOSED Data1 bars (tick or minute) already converted to Eastern time and stamped with the
	// Globex trading day, aggregates them to 1m / 5m / 30m buckets itself, and
	//   - builds the Gate-0 level families each RTH session and feeds them to a hosted _LevelStudy (A2-33) that sees 5-minute RTH
	//     bars only: 20 matched controls per level at 0.9-1.1x the level's distance from the RTH OPEN (either side), reaction
	//     R = max_{k=1..12} |P(t+k) - level| / sigma after the first touch, sigma = ATR14(5m) frozen at registration (RTH open);
	//   - or runs one G0-b canary and publishes the order it wants (Gate0Intent) for the strategy to translate.
	// A2-39 roll honesty (review: Reset only): every multi-session source (prior profiles, naked POCs, weekly, composite,
	// week/month AVWAP) restarts at the roll; analysis drops events on a roll session +-1 (review M2).
	// Development window only: nothing is registered or traded on a trading day >= DevEnd (2025-01-01).
	// ============================================================================================

	public enum EGate0Mode { Strategy = 0, EventStudy = 1, ControlRT = 2, ControlFL = 3, Canary = 4 }

	/// <summary>Variant in the level-study modes. 0 = all families.</summary>
	public enum EGate0Family { All = 0, PriorProfile = 1, NakedPoc = 2, IbEdge = 3, Overnight = 4, Avwap = 5, Vwap = 6, Weekly = 7, CompositeVa = 8, SinglePrint = 9, DvSource = 10, HvnLvn = 11, Mixture = 12, SigmaMixture = 13 }

	/// <summary>Variant in Canary mode (A2-53 sweep cells are Variant 100..123, see Gate0Engine.DecodeA253).</summary>
	public enum EGate0Canary { VW08 = 0, A2_24_i = 1, A2_24_ii = 2, A2_24_iii = 3, DV10 = 4, A2_53 = 5, DV01 = 6, PS03 = 7 }

	public enum EGate0Entry { None = 0, Market = 1, Limit = 2 }

	public struct Gate0Bar
	{
		public DateTime et;    // bar close time, Eastern
		public DateTime day;   // Globex trading day (date part used)
		public double o, h, l, c, v;
		public bool roll;      // first bar of a new contract (rollOn(1))
		public Gate0Bar(DateTime et, DateTime day, double o, double h, double l, double c, double v, bool roll = false)
		{ this.et = et; this.day = day.Date; this.o = o; this.h = h; this.l = l; this.c = c; this.v = v; this.roll = roll; }
	}

	/// <summary>What the canary wants on the next bar. Re-read after every Bar() call.</summary>
	public sealed class Gate0Intent
	{
		public int side;                       // wanted position sign: +1 / -1 / 0 (0 = flat or no opinion when entry None)
		public EGate0Entry entry;              // how to enter `side` while flat (or reverse, Market only)
		public double entryPrice = double.NaN; // limit price for EGate0Entry.Limit
		public double stop = double.NaN, target = double.NaN;   // protective exits while in a position
		public double reverseStop = double.NaN;// stop ENTRY in the opposite direction while in a position (A2-53 SAR)
		public bool flatten;                   // exit at market now
		public string tag = "";
		public void Clear() { side = 0; entry = EGate0Entry.None; entryPrice = stop = target = reverseStop = double.NaN; flatten = false; tag = ""; }
	}

	/// <summary>Volume-at-price on a tick grid; volume of a bar spread uniformly over its range.</summary>
	public sealed class Gate0Profile
	{
		public readonly SortedDictionary<long, double> rows = new SortedDictionary<long, double>();
		public double tick;
		public double total;
		public Gate0Profile(double tick) { this.tick = tick; }
		public void Add(double h, double l, double v)
		{
			if (!(v > 0)) return;
			long lo = (long)Math.Round(l / tick), hi = (long)Math.Round(h / tick);
			if (hi < lo) { long t = hi; hi = lo; lo = t; }
			double per = v / (hi - lo + 1);
			for (long r = lo; r <= hi; r++) { double x; rows.TryGetValue(r, out x); rows[r] = x + per; }
			total += v;
		}
		public void Merge(Gate0Profile p) { foreach (var kv in p.rows) { double x; rows.TryGetValue(kv.Key, out x); rows[kv.Key] = x + kv.Value; } total += p.total; }
		public bool Empty { get { return rows.Count == 0 || !(total > 0); } }

		/// <summary>POC (highest row, the one nearest the profile midpoint on ties) and the value area by two-sided expansion.</summary>
		public bool ValueArea(double pct, out double poc, out double vah, out double val)
		{
			poc = vah = val = double.NaN;
			if (Empty) return false;
			var keys = new List<long>(rows.Keys); var vol = new double[keys.Count];
			for (int i = 0; i < keys.Count; i++) vol[i] = rows[keys[i]];
			int p = 0; double mid = (keys.Count - 1) / 2.0;
			for (int i = 1; i < vol.Length; i++) if (vol[i] > vol[p] || (vol[i] == vol[p] && Math.Abs(i - mid) < Math.Abs(p - mid))) p = i;
			int a = p, b = p; double acc = vol[p], need = pct * total;
			while (acc < need && (a > 0 || b < vol.Length - 1))
			{
				double up = b < vol.Length - 1 ? vol[b + 1] : -1, dn = a > 0 ? vol[a - 1] : -1;
				if (up >= dn) { b++; acc += vol[b]; } else { a--; acc += vol[a]; }
			}
			poc = keys[p] * tick; vah = keys[b] * tick; val = keys[a] * tick;
			return true;
		}

		public long FirstRow { get { foreach (var k in rows.Keys) return k; return 0; } }
		public long LastRow { get { long l = 0; foreach (var k in rows.Keys) l = k; return l; } }

		/// <summary>Interior runs: >= minRows contiguous thin rows with a NON-thin row on both sides (tails at either end of the
		/// range are never runs). Each run is {upper edge price, lower edge price}.</summary>
		public static List<double[]> InteriorRuns(long first, long last, Func<long, bool> thin, int minRows, double tick)
		{
			var res = new List<double[]>(); bool solidBelow = false; long runStart = long.MinValue;
			for (long r = first; r <= last; r++)
			{
				if (thin(r)) { if (solidBelow && runStart == long.MinValue) runStart = r; continue; }
				if (runStart != long.MinValue && r - runStart >= minRows) res.Add(new[] { (r - 1) * tick, runStart * tick });
				runStart = long.MinValue; solidBelow = true;
			}
			return res;   // a run still open at `last` is a top tail: dropped
		}

		/// <summary>Volume sensitivity arm: interior runs of >= minRows rows with volume <= frac x POC row volume.</summary>
		public List<double[]> SinglePrintRuns(double frac, int minRows)
		{
			if (Empty) return new List<double[]>();
			double max = 0; foreach (var x in rows.Values) if (x > max) max = x;
			return InteriorRuns(FirstRow, LastRow, r => { double x; return !rows.TryGetValue(r, out x) || x <= frac * max; }, minRows, tick);
		}

		/// <summary>Primary single-print rule: interior runs of >= minRows rows touched by exactly one 30m period (TPO count 1,
		/// built from 1m bars). A row inside the range with count 0 (a 1m gap) counts as thin as well.</summary>
		public static List<double[]> TpoSinglePrints(IDictionary<long, int> tpo, int minRows, double tick)
		{
			if (tpo == null || tpo.Count == 0) return new List<double[]>();
			long first = long.MaxValue, last = long.MinValue; foreach (var k in tpo.Keys) { if (k < first) first = k; if (k > last) last = k; }
			return InteriorRuns(first, last, r => { int n; return !tpo.TryGetValue(r, out n) || n <= 1; }, minRows, tick);
		}

		/// <summary>Gaussian KDE on the tick grid (bandwidth in ticks). Returns HVNs {price, prominence} and LVNs {price, prominence}:
		/// HVN = local density maximum with topographic prominence >= prom x max density; LVN = the density minimum between two
		/// adjacent HVNs, prominence = (lower of the two peaks - trough) / max density, kept if >= prom.</summary>
		public void Nodes(double bwTicks, double prom, out List<double[]> hvn, out List<double[]> lvn)
		{
			hvn = new List<double[]>(); lvn = new List<double[]>(); if (Empty || !(bwTicks > 0)) return;
			long first = FirstRow, last = LastRow; int pad = (int)Math.Ceiling(3 * bwTicks); long lo = first - pad; int n = (int)(last + pad - lo + 1);
			var d = new double[n];
			foreach (var kv in rows)
				for (int j = Math.Max(0, (int)(kv.Key - lo) - pad); j <= Math.Min(n - 1, (int)(kv.Key - lo) + pad); j++)
				{ double z = (j + lo - kv.Key) / bwTicks; d[j] += kv.Value * Math.Exp(-0.5 * z * z); }
			double max = 0; foreach (var x in d) if (x > max) max = x; if (!(max > 0)) return;
			var peaks = new List<int>();
			for (int i = 0; i < n; i++)
			{
				if (!((i == 0 || d[i] > d[i - 1]) && (i == n - 1 || d[i] >= d[i + 1]))) continue;
				double lb = d[i], rb = d[i]; int k;
				for (k = i - 1; k >= 0 && d[k] <= d[i]; k--) if (d[k] < lb) lb = d[k]; if (k < 0) lb = Math.Min(lb, 0);
				for (k = i + 1; k < n && d[k] <= d[i]; k++) if (d[k] < rb) rb = d[k]; if (k >= n) rb = Math.Min(rb, 0);
				double p = (d[i] - Math.Max(lb, rb)) / max;
				if (p >= prom) { peaks.Add(i); hvn.Add(new[] { (i + lo) * tick, p }); }
			}
			for (int q = 1; q < peaks.Count; q++)
			{
				int a = peaks[q - 1], b = peaks[q], m = a; for (int i = a; i <= b; i++) if (d[i] < d[m]) m = i;
				double p = (Math.Min(d[a], d[b]) - d[m]) / max;
				if (p >= prom) lvn.Add(new[] { (m + lo) * tick, p });
			}
		}
	}

	/// <summary>Volume-weighted 1-D Gaussian mixture (EM), K chosen by BIC over 1..maxK. N for the BIC is nEff (weights are
	/// rescaled to sum to nEff). Components sorted by mean.</summary>
	public sealed class Gate0Gmm
	{
		public int K; public double[] mu = new double[0], sd = new double[0], pi = new double[0]; public double bic = double.NaN;
		public static Gate0Gmm Fit(IList<double> x, IList<double> w, int maxK, double nEff, double sdFloor, int iters = 200)
		{
			Gate0Gmm best = null; int n = x.Count; if (n < 2 || !(nEff > 1)) return null;
			double tw = 0; for (int i = 0; i < n; i++) tw += w[i]; if (!(tw > 0)) return null;
			var ww = new double[n]; for (int i = 0; i < n; i++) ww[i] = w[i] / tw * nEff;
			var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i; Array.Sort(idx, (a, b) => x[a].CompareTo(x[b]));
			for (int K = 1; K <= maxK; K++)
			{
				var g = new Gate0Gmm { K = K, mu = new double[K], sd = new double[K], pi = new double[K] };
				// init: weighted quantile means of K equal-mass slices
				double acc = 0; int sl = 0; var sw = new double[K]; var sx = new double[K]; var sxx = new double[K];
				foreach (int i in idx) { int k = Math.Min(K - 1, (int)(acc / nEff * K)); sw[k] += ww[i]; sx[k] += ww[i] * x[i]; sxx[k] += ww[i] * x[i] * x[i]; acc += ww[i]; sl++; }
				for (int k = 0; k < K; k++) { g.pi[k] = Math.Max(sw[k], 1e-9) / nEff; g.mu[k] = sw[k] > 0 ? sx[k] / sw[k] : x[idx[0]]; double v = sw[k] > 0 ? sxx[k] / sw[k] - g.mu[k] * g.mu[k] : 0; g.sd[k] = Math.Max(Math.Sqrt(Math.Max(v, 0)), sdFloor); }
				var resp = new double[K]; double ll = 0;
				for (int it = 0; it < iters; it++)
				{
					for (int k = 0; k < K; k++) { sw[k] = sx[k] = sxx[k] = 0; }
					ll = 0;
					for (int i = 0; i < n; i++)
					{
						double tot = 0;
						for (int k = 0; k < K; k++) { double z = (x[i] - g.mu[k]) / g.sd[k]; resp[k] = g.pi[k] * Math.Exp(-0.5 * z * z) / (g.sd[k] * 2.5066282746310002); tot += resp[k]; }
						if (!(tot > 0)) tot = 1e-300; ll += ww[i] * Math.Log(tot);
						for (int k = 0; k < K; k++) { double r = ww[i] * resp[k] / tot; sw[k] += r; sx[k] += r * x[i]; sxx[k] += r * x[i] * x[i]; }
					}
					for (int k = 0; k < K; k++)
					{
						if (!(sw[k] > 1e-9)) { g.pi[k] = 1e-9; continue; }
						g.pi[k] = sw[k] / nEff; g.mu[k] = sx[k] / sw[k];
						g.sd[k] = Math.Max(Math.Sqrt(Math.Max(sxx[k] / sw[k] - g.mu[k] * g.mu[k], 0)), sdFloor);
					}
				}
				g.bic = -2 * ll + (3 * K - 1) * Math.Log(nEff);
				var ord = new int[K]; for (int k = 0; k < K; k++) ord[k] = k; Array.Sort(ord, (a, b) => g.mu[a].CompareTo(g.mu[b]));
				g.mu = Array.ConvertAll(ord, k => g.mu[k]); g.sd = Array.ConvertAll(ord, k => g.sd[k]); g.pi = Array.ConvertAll(ord, k => g.pi[k]);
				if (best == null || g.bic < best.bic) best = g;
			}
			return best;
		}
	}

	/// <summary>Running VWAP with volume-weighted sigma.</summary>
	public sealed class Gate0Vwap
	{
		public double pv, p2v, v;
		public void Reset() { pv = p2v = v = 0; }
		public void Add(double price, double vol) { if (!(vol > 0)) return; pv += price * vol; p2v += price * price * vol; v += vol; }
		public bool Ready { get { return v > 0; } }
		public double Value { get { return v > 0 ? pv / v : double.NaN; } }
		public double Sigma { get { if (!(v > 0)) return double.NaN; double m = pv / v, s = p2v / v - m * m; return s > 0 ? Math.Sqrt(s) : 0; } }
	}

	/// <summary>Wilder ATR over completed bars.</summary>
	public sealed class Gate0Atr
	{
		private readonly int m_len; private int m_n; private double m_prevC = double.NaN, m_sum;
		public double Value = double.NaN;
		public Gate0Atr(int len) { m_len = len; }
		/// <summary>Forget the prior close (roll splice: the next true range is the bar's own range).</summary>
		public void ResetPrev() { m_prevC = double.NaN; }
		public void Add(double h, double l, double c)
		{
			double tr = double.IsNaN(m_prevC) ? h - l : Math.Max(h, m_prevC) - Math.Min(l, m_prevC);
			m_prevC = c; m_n++;
			if (m_n <= m_len) { m_sum += tr; if (m_n == m_len) Value = m_sum / m_len; }
			else Value = (Value * (m_len - 1) + tr) / m_len;
		}
	}

	/// <summary>Time-bucket aggregator: a bar belongs to the bucket that ends at its close time rounded up to `minutes`.
	/// A bucket closes on the bar stamped exactly at its end (minute data) or when a bar of a later bucket arrives (tick data).</summary>
	public sealed class Gate0Bucket
	{
		public readonly int minutes;
		public bool open; public DateTime end; public double o, h, l, c, v;
		public Gate0Bucket(int minutes) { this.minutes = minutes; }
		public static DateTime EndOf(DateTime t, int minutes)
		{
			long span = TimeSpan.FromMinutes(minutes).Ticks; long k = (t.Ticks + span - 1) / span; return new DateTime(k * span);
		}
		/// <summary>Returns true (with the closed bucket in the out params) when a bucket completed BEFORE this bar was added.</summary>
		public bool PreBar(DateTime t, out DateTime endOut, out double oo, out double hh, out double ll, out double cc, out double vv)
		{
			endOut = end; oo = o; hh = h; ll = l; cc = c; vv = v;
			if (open && EndOf(t, minutes) != end) { open = false; return true; }
			return false;
		}
		public void Add(DateTime t, double bo, double bh, double bl, double bc, double bv)
		{
			if (!open) { open = true; end = EndOf(t, minutes); o = bo; h = bh; l = bl; v = 0; }
			if (bh > h) h = bh; if (bl < l) l = bl; c = bc; v += bv;
		}
		/// <summary>True when the bar just added closes the bucket exactly (bar stamped at the bucket end).</summary>
		public bool PostBar(DateTime t) { if (open && t == end) { open = false; return true; } return false; }
	}

	/// <summary>One RTH session's facts (09:30-16:00 ET value window) plus the overnight before it.</summary>
	public sealed class Gate0Session
	{
		public DateTime day; public bool roll;
		public double rthO = double.NaN, rthH = double.MinValue, rthL = double.MaxValue, rthC = double.NaN;
		public double ibH = double.MinValue, ibL = double.MaxValue, onH = double.MinValue, onL = double.MaxValue;
		public Gate0Profile profile;                         // RTH volume profile
		public readonly Dictionary<long, int> tpo = new Dictionary<long, int>();       // row -> number of 30m periods that touched it (1m bars)
		internal readonly Dictionary<long, int> tpoLast = new Dictionary<long, int>();
		public Gate0Vwap vwap = new Gate0Vwap(), avHi = new Gate0Vwap(), avLo = new Gate0Vwap();
		public double poc = double.NaN, vah = double.NaN, val = double.NaN;
		public List<double> closes1m = new List<double>();   // RTH 1m closes (ControlRT draws)
		public int bars1m;                                    // RTH 1m bars (GMM BIC N)
		public List<RingBar> rth1m;                           // RTH 1m bars, kept only when the HVN/LVN h_cv arm is enabled
		public double hCv = double.NaN;                       // this session's KDE h_cv (price units, _ProfileStats.KdeSweep, contiguous 5-fold CV)
		public double pkSum; public int pkN;                  // Parkinson sum (ln H/L)^2 and count over RTH 5m bars (A2-32)
		public bool pocNaked = true;                          // POC not traded since this session ended
		public bool HasRth { get { return !double.IsNaN(rthO); } }
		/// <summary>A2-32 session sigma in price: sVWAP x sigma_bar x sqrt(n), NaN with fewer than 60 5m bars.</summary>
		public double SigmaPrice { get { if (pkN < Gate0Engine.ParkinsonMinBars || !vwap.Ready) return double.NaN; return vwap.Value * Math.Sqrt(pkSum / (4.0 * pkN * Math.Log(2))) * Math.Sqrt(pkN); } }
		public Gate0Session(DateTime day, double tick) { this.day = day; profile = new Gate0Profile(tick); }
	}

	/// <summary>A DV level source (DV_ROT_POC / DV_TREND_HVN) once it is valid (rotation follow-through done / trend HVN armed).</summary>
	public sealed class Gate0DvLevel
	{
		public string type = ""; public double price; public int dir; public DateTime validFrom; public int expirySessions; public bool touched;
	}

	/// <summary>Per-type Gate-0 result (A2-33 matched-control reaction edge; no binary hold / random placebo in the verdict path).</summary>
	public sealed class Gate0Result
	{
		public string type = ""; public int levels, touches, sessions;
		public int pairs; public double edge = double.NaN, t = double.NaN, p = double.NaN, pHolm = double.NaN, ciLo = double.NaN, ciHi = double.NaN;
		public double halfLife = double.NaN; public double[] tByAge = new double[0]; public int[] nByAge = new int[0]; public double[] tByTercile = new double[3];
		public string a2Verdict = "";
		public string verdict = "";
		public bool verdictRun = true;
		public string AgeList()
		{
			var sb = new StringBuilder();
			for (int a = 0; a < tByAge.Length; a++) if (nByAge[a] > 0) sb.AppendFormat(CultureInfo.InvariantCulture, "{0}{1}:{2:0.0}/{3}{4}", sb.Length > 0 ? " " : "", a + 1, tByAge[a], nByAge[a], double.IsNaN(tByAge[a]) || Math.Abs(tByAge[a]) < 2 ? "x" : "");
			return sb.ToString();
		}
		public string Line()
		{
			return string.Format(CultureInfo.InvariantCulture,
				"G0 type({0}) levels({1}) touched({2}) pairs({3}) edgeR({4:+0.000;-0.000;0.000}) t({5:0.00}) p({6:0.0000}) holm({7:0.0000}) ci95[{8:0.000},{9:0.000}] sessions({10}) halfLife({11:0.0}) tTercile({12:0.00}/{13:0.00}/{14:0.00}) tByAge[{15}] A2-33({16}) => {17}",
				type, levels, touches, pairs, edge, t, p, pHolm, ciLo, ciHi, sessions, halfLife, tByTercile[0], tByTercile[1], tByTercile[2], AgeList(), a2Verdict, verdict);
		}
	}

	public sealed class Gate0Engine
	{
		public static readonly DateTime DevEnd = new DateTime(2025, 1, 1);
		public static readonly DateTime CanaryA224From = new DateTime(2019, 1, 1);   // A2-24's own window 2019-2024 (reported next to the full window)
		public const int BootstrapDraws = 500, BootstrapBlock = 5;   // review M6
		public const int Controls = 20, ReactionBars5m = 12, MaxAge = 15;           // A2-33 verbatim
		public const int PriorProfileMaxAge = 15, NakedPocMaxAge = 15;
		/// <summary>Pre-registered composite length for CVAH/CVAL (sessions, current contract only). Not swept.</summary>
		public const int CompositeSessions = 5;
		public const int SigmaCompositeSessions = 20, SigmaEwmaSpan = 10, ParkinsonMinBars = 60, GmmMaxK = 4;   // A2-32
		public const int HistoryCap = 26;                                            // sessions kept (A2-32 needs 20 valid ones)
		public const double KdeBandwidthTicks = 6, KdeProminence = 0.10;            // §2.1 HVN/LVN (ES bandwidth) = PRIMARY arm
		// HVN/LVN sensitivity arm (types HVN_HCV / LVN_HCV): bandwidth = the prior session's h_cv from _ProfileStats.KdeSweep (PR #18,
		// uniform rows, contiguous 5-fold CV by 1m bar), computed when that session closes - no lookahead into the session traded.
		public const int HcvDistribution = 2;

		/// <summary>Pre-registered arms, journaled at Configure before any result.</summary>
		public static string PreRegistration()
		{
			return string.Format(CultureInfo.InvariantCulture,
				"G0 PRE-REGISTERED: DV rotation types DV_ROT_POC_R6 (6-bar box) and DV_ROT_POC_R12 (12-bar box), w={0} x ATR30, breakout {1} x ATR30, expiry {2} sessions; " +
				"HVN/LVN bandwidth arms PRIMARY [HVN, LVN] = {3} ticks fixed, SENSITIVITY [HVN_HCV, LVN_HCV] = prior session h_cv (_ProfileStats.KdeSweep); prominence >= {4:0%}; " +
				"composite {5} sessions; controls {6}; reaction {7} x 5m; ages 1..{8}; Holm m = {9}",
				DvRotW, DvBreakAtr, DvRotExpirySessions, KdeBandwidthTicks, KdeProminence, CompositeSessions, Controls, ReactionBars5m, MaxAge, AllTypes.Length);
		}
		public const int SinglePrintMinRows = 4; public const double SinglePrintVolFrac = 0.05;
		public const double DvRotW = 1.5, DvBreakAtr = 1.5, DvTrendLegAtr = 3.0, DvHvnMedianX = 2.0;
		public const int DvRotExpirySessions = 5, DvTrendExpirySessions = 10, DvTrendArmBars = 2, DvTrendLookback = 20, DvPendingBars = 26;
		public static readonly int[] DvRotBars = { 6, 12 };
		/// <summary>Every level type a verdict run can produce; Holm is applied per run with m = this count (conservative: valid
		/// across the per-family runs, which never see the other families' p-values).</summary>
		public static readonly string[] AllTypes = { "POC", "VAH", "VAL", "NPOC", "IBH", "IBL", "ONH", "ONL", "AV_PH", "AV_PL", "AV_WK", "AV_MO", "AV_OPEN", "AV_PC",
			"VWAP", "VWAP+1", "VWAP-1", "WPOC", "WVAH", "WVAL", "CVAH", "CVAL", "SP_HI", "SP_LO", "SPV_HI", "SPV_LO", "DV_ROT_POC_R6", "DV_ROT_POC_R12", "DV_TREND_HVN",
			"HVN", "LVN", "HVN_HCV", "LVN_HCV", "GMM_MU", "GMM_EDGE", "SIG_MU", "SIG_EDGE" };

		// ---- configuration (set before the first Bar) ----
		public EGate0Mode mode = EGate0Mode.EventStudy;
		public int variant;
		public double tick = 0.25;
		public long seed = 33;
		public double vaPct = 0.70;
		public Action<string> journal;
		// canary parameters (catalogue values where it gives one; see the _TestGate0 header for the rest)
		public double a253Gap = 0.005; public int a253Bar = 1, a253Minutes = 30; public bool a253Filter = true;
		public double dv01F = 0.15, dv01K = 1.0;
		public double dv10S = 0.5, dv10F = 0.20;

		internal readonly _LevelStudy study = new _LevelStudy();
		public readonly Gate0Intent intent = new Gate0Intent();
		public readonly List<DateTime> rollDays = new List<DateTime>();
		public readonly List<DateTime> sessionDays = new List<DateTime>();
		public int Trades { get; private set; }
		/// <summary>Canary: sessions skipped live because they are a roll day or the session right after a roll splice (the session
		/// BEFORE a roll is only known afterwards: the strategy drops its trades from the reported totals via InRollWindow).</summary>
		public int RollSkips { get; private set; }
		/// <summary>True when this is a verdict run (single family, 20 controls). Variant 0 is an overview only.</summary>
		public bool VerdictRun { get { return mode == EGate0Mode.Canary || variant != 0; } }

		// ---- state ----
		private readonly List<Gate0Session> m_hist = new List<Gate0Session>();   // completed sessions since the last roll, oldest first
		private Gate0Session m_cur;
		private DateTime m_day = DateTime.MinValue; private bool m_rthOn, m_studyOn;
		private readonly Gate0Bucket m_b1 = new Gate0Bucket(1), m_b5 = new Gate0Bucket(5), m_b15 = new Gate0Bucket(15), m_b30 = new Gate0Bucket(30);
		private readonly Gate0Atr m_atr5 = new Gate0Atr(14), m_atr30 = new Gate0Atr(14), m_atrD = new Gate0Atr(20);
		private readonly List<double> m_dailyRets = new List<double>(); private double m_prevDailyClose = double.NaN; private bool m_rollSplice;
		private readonly Gate0Vwap m_avWeek = new Gate0Vwap(), m_avMonth = new Gate0Vwap(), m_wkVwap = new Gate0Vwap(), m_avPc = new Gate0Vwap();
		private bool m_prevRth;   // AVWAP anchored at the prior RTH close: reset on the first bar after the RTH close
		private double m_lastC = double.NaN;
		private List<Gate0Profile> m_weekProfiles = new List<Gate0Profile>(), m_lastWeek; private int m_lastWeekAge; private DateTime m_weekKey;
		// DV sources: closed RTH 30m bars across sessions (reset at the roll) with their own profiles
		private sealed class Bar30 { public double h, l, c; public Gate0Profile prof; }
		private Gate0Profile m_block; private double m_blockH, m_blockL, m_blockC;
		private readonly List<Bar30> m_bars30 = new List<Bar30>();
		private sealed class DvPending { public string type; public int dir, bars, armCount; public double boxH, boxL, level; public bool rot; }
		private readonly List<DvPending> m_dvPending = new List<DvPending>();
		private readonly List<Gate0DvLevel> m_dv = new List<Gate0DvLevel>();
		private readonly Dictionary<int, int> m_rotCool = new Dictionary<int, int>(); private int m_trendCool;
		private int m_mp; private bool m_inited;

		// ---- public read-outs (tests) ----
		public IList<Gate0Session> History { get { return m_hist; } }
		public Gate0Session Current { get { return m_cur; } }
		public IList<Gate0DvLevel> DvLevels { get { return m_dv; } }
		public double SessionVwap { get { return m_cur != null ? m_cur.vwap.Value : double.NaN; } }
		public double Atr20d { get { return m_atrD.Value; } }
		public double Atr30 { get { return m_atr30.Value; } }

		/// <summary>A2-53 sweep cell: Variant 100..123 -> idx = Variant-100; gap g = {0.3,0.5,0.8}%[idx/8]; j = 1 + (idx/4)%2;
		/// bar = (idx/2)%2 == 0 ? 30m : 15m; vol filter = idx%2 == 0 (on). Variant 5 = the default cell 108 (0.5%, j=1, 30m, filter on).</summary>
		public static bool DecodeA253(int v, out double gap, out int j, out int minutes, out bool filter)
		{
			gap = 0.005; j = 1; minutes = 30; filter = true;
			if (v == (int)EGate0Canary.A2_53) return true;
			if (v < 100 || v > 123) return false;
			int i = v - 100; double[] g = { 0.003, 0.005, 0.008 };
			gap = g[i / 8]; j = 1 + (i / 4) % 2; minutes = (i / 2) % 2 == 0 ? 30 : 15; filter = i % 2 == 0;
			return true;
		}
		private EGate0Canary Cv { get { return variant >= 100 ? EGate0Canary.A2_53 : (EGate0Canary)variant; } }
		public string CanaryName { get { if (Cv != EGate0Canary.A2_53) return Cv.ToString(); return string.Format(CultureInfo.InvariantCulture, "A2_53[g={0:0.0}% j={1} {2}m filter={3}]", a253Gap * 100, a253Bar, a253Minutes, a253Filter ? "on" : "off"); } }

		public string Configure()
		{
			if (tick <= 0) return "tick must be > 0";
			if (mode == EGate0Mode.Canary && !((variant >= 0 && variant <= 7) || (variant >= 100 && variant <= 123))) return "Canary Variant must be 0..7 or 100..123 (A2-53 sweep)";
			if (mode != EGate0Mode.Canary && (variant < 0 || variant > 13)) return "level-study Variant must be 0..13";
			if (mode == EGate0Mode.Canary && Cv == EGate0Canary.A2_53) DecodeA253(variant, out a253Gap, out a253Bar, out a253Minutes, out a253Filter);
			study.tickSize = tick;
			study.config.enabled = true; study.config.to = DevEnd.AddDays(-1); study.config.maxAge = MaxAge;
			study.config.controls = Controls; study.config.reactionBars = ReactionBars5m;
			study.config.controlsEitherSide = true; study.config.reactionAfterTouch = true;
			if (mode != EGate0Mode.Canary && variant == 0) study.config.controls = 5;   // memory: overview only, NOT a verdict run
			m_inited = true;
			string err = study.Configure();
			if (err == null && journal != null && StudyMode) journal(PreRegistration());
			return err;
		}

		private bool StudyMode { get { return mode != EGate0Mode.Canary; } }
		private bool Fam(EGate0Family f) { return variant == 0 || variant == (int)f; }
		private static bool IsRthTime(TimeSpan t) { return t > new TimeSpan(9, 30, 0) && t <= new TimeSpan(16, 0, 0); }

		/// <summary>Feed one CLOSED Data1 bar. mp = current market position sign (0 in study modes).</summary>
		public void Bar(Gate0Bar b, int mp = 0)
		{
			if (!m_inited) Configure();
			m_mp = mp;
			if (b.day != m_day) newDay(b);
			if (b.roll) onRoll(b.day);
			TimeSpan t = b.et.TimeOfDay;
			bool rth = IsRthTime(t);
			DateTime e; double o, h, l, c, v;
			if (m_b1.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on1m(e, o, h, l, c, v);
			if (m_b5.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on5m(e, o, h, l, c, v);
			if (m_b15.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on15m(e, o, h, l, c, v);
			if (m_b30.PreBar(b.et, out e, out o, out h, out l, out c, out v)) on30m(e, o, h, l, c, v);
			double tp = (b.h + b.l + b.c) / 3;
			if (!rth && m_prevRth) m_avPc.Reset();
			m_prevRth = rth;
			if (rth && !m_rthOn) rthOpen(b);   // levels are registered from data that closed BEFORE this bar
			m_wkVwap.Add(tp, b.v); m_avWeek.Add(tp, b.v); m_avMonth.Add(tp, b.v); m_avPc.Add(tp, b.v);
			if (rth)
			{
				var s = m_cur;
				if (b.h > s.rthH) { s.rthH = b.h; s.avHi.Reset(); }
				if (b.l < s.rthL) { s.rthL = b.l; s.avLo.Reset(); }
				s.avHi.Add(tp, b.v); s.avLo.Add(tp, b.v);
				s.rthC = b.c; s.vwap.Add(tp, b.v); s.profile.Add(b.h, b.l, b.v);
				if (t <= new TimeSpan(10, 30, 0)) { if (b.h > s.ibH) s.ibH = b.h; if (b.l < s.ibL) s.ibL = b.l; }
				if (m_block == null) { m_block = new Gate0Profile(tick); m_blockH = double.MinValue; m_blockL = double.MaxValue; }
				m_block.Add(b.h, b.l, b.v); if (b.h > m_blockH) m_blockH = b.h; if (b.l < m_blockL) m_blockL = b.l; m_blockC = b.c;
				foreach (var old in m_hist) if (old.pocNaked && b.l <= old.poc && old.poc <= b.h) old.pocNaked = false;
				foreach (var dv in m_dv) if (!dv.touched && b.day >= dv.validFrom && b.l <= dv.price && dv.price <= b.h) dv.touched = true;
				m_b1.Add(b.et, b.o, b.h, b.l, b.c, b.v); m_b5.Add(b.et, b.o, b.h, b.l, b.c, b.v); m_b15.Add(b.et, b.o, b.h, b.l, b.c, b.v); m_b30.Add(b.et, b.o, b.h, b.l, b.c, b.v);
			}
			else if (m_cur != null && !m_rthOn)
			{
				if (b.h > m_cur.onH) m_cur.onH = b.h; if (b.l < m_cur.onL) m_cur.onL = b.l;
			}
			if (rth)
			{
				if (m_b1.PostBar(b.et)) on1m(m_b1.end, m_b1.o, m_b1.h, m_b1.l, m_b1.c, m_b1.v);
				if (m_b5.PostBar(b.et)) on5m(m_b5.end, m_b5.o, m_b5.h, m_b5.l, m_b5.c, m_b5.v);
				if (m_b15.PostBar(b.et)) on15m(m_b15.end, m_b15.o, m_b15.h, m_b15.l, m_b15.c, m_b15.v);
				if (m_b30.PostBar(b.et)) on30m(m_b30.end, m_b30.o, m_b30.h, m_b30.l, m_b30.c, m_b30.v);
			}
			if (mode == EGate0Mode.Canary) canaryBar(b, rth);
			m_lastC = b.c;
		}

		// ================================ sessions =================================
		private void newDay(Gate0Bar b)
		{
			closeRth();
			if (m_cur != null && m_cur.HasRth)
			{
				m_hist.Add(m_cur); if (m_hist.Count > HistoryCap) m_hist.RemoveAt(0);
				m_weekProfiles.Add(m_cur.profile);
			}
			DateTime wk = b.day.AddDays(-(((int)b.day.DayOfWeek + 6) % 7));
			if (wk != m_weekKey)
			{
				m_avWeek.Reset(); m_wkVwap.Reset();
				if (m_weekProfiles.Count > 0) { m_lastWeek = m_weekProfiles; m_lastWeekAge = 1; }
				m_weekProfiles = new List<Gate0Profile>(); m_weekKey = wk;
			}
			else if (m_lastWeek != null) m_lastWeekAge++;
			m_cur = new Gate0Session(b.day, tick);
			m_day = b.day; m_rthOn = false; sessionDays.Add(b.day);
			if (sessionDays.Count < 2 || sessionDays[sessionDays.Count - 2].Month != b.day.Month) m_avMonth.Reset();
			canaryNewDay();
		}

		private void onRoll(DateTime day)
		{
			if (rollDays.Count == 0 || rollDays[rollDays.Count - 1] != day) rollDays.Add(day);
			if (m_cur != null) m_cur.roll = true;
			// A2-39 Reset: forget every multi-session source built on the old contract
			m_hist.Clear(); m_weekProfiles.Clear(); m_lastWeek = null; m_avWeek.Reset(); m_avMonth.Reset(); m_wkVwap.Reset(); m_avPc.Reset();
			m_bars30.Clear(); m_dvPending.Clear(); m_dv.Clear(); m_rotCool.Clear(); m_trendCool = 0;
			m_atrD.ResetPrev(); m_rollSplice = true;
		}

		private void rthOpen(Gate0Bar b)
		{
			m_rthOn = true;
			var s = m_cur; s.rthO = b.o;
			m_block = null;
			if (StudyMode)
			{
				study.BeginSession(b.day);
				m_studyOn = b.day < DevEnd;
				if (m_studyOn) registerLevels(b);
			}
			canaryRthOpen(b);
		}

		private void closeRth()
		{
			if (m_cur == null || !m_cur.HasRth) return;
			var s = m_cur;
			double poc, vah, val;
			if (s.profile.ValueArea(vaPct, out poc, out vah, out val)) { s.poc = poc; s.vah = vah; s.val = val; }
			closeBlock();
			if (s.rth1m != null && s.rth1m.Count > 0) { s.hCv = _ProfileStats.KdeSweep(s.rth1m, tick, HcvDistribution).hCv; s.rth1m = null; }
			m_atrD.Add(s.rthH, s.rthL, s.rthC);
			if (!double.IsNaN(m_prevDailyClose) && !m_rollSplice) { m_dailyRets.Add(Math.Log(s.rthC / m_prevDailyClose)); if (m_dailyRets.Count > 300) m_dailyRets.RemoveAt(0); }
			m_prevDailyClose = s.rthC; m_rollSplice = false;
			// expire DV levels (sessions counted from validFrom, this one included)
			for (int i = m_dv.Count - 1; i >= 0; i--) if (m_dv[i].touched || SessionsSince(m_dv[i].validFrom) >= m_dv[i].expirySessions) m_dv.RemoveAt(i);
		}

		private int SessionsSince(DateTime d)
		{
			int i = sessionDays.BinarySearch(d.Date); if (i < 0) i = ~i;
			return sessionDays.Count - 1 - i + (sessionDays.Count > 0 && sessionDays[sessionDays.Count - 1] >= d.Date ? 1 : 0);
		}

		private Bar30 closeBlock()
		{
			if (m_block == null || m_block.Empty) { m_block = null; return null; }
			var bar = new Bar30 { h = m_blockH, l = m_blockL, c = m_blockC, prof = m_block };
			m_bars30.Add(bar); if (m_bars30.Count > 40) m_bars30.RemoveAt(0);
			m_block = null;
			return bar;
		}

		private void on1m(DateTime end, double o, double h, double l, double c, double v)
		{
			if (!IsRthTime(end.TimeOfDay)) return;
			if (m_cur != null)
			{
				var s = m_cur; s.closes1m.Add(c); s.bars1m++;
				if (StudyMode && Fam(EGate0Family.HvnLvn)) { if (s.rth1m == null) s.rth1m = new List<RingBar>(); s.rth1m.Add(new RingBar { time = end, tradingDay = s.day, o = o, h = h, l = l, c = c, v = v, rth = true }); }
				int period = (int)Math.Ceiling((end.TimeOfDay - new TimeSpan(9, 30, 0)).TotalMinutes / 30.0);
				long lo = (long)Math.Round(l / tick), hi = (long)Math.Round(h / tick);
				for (long r = lo; r <= hi; r++)
				{
					int last; if (s.tpoLast.TryGetValue(r, out last) && last == period) continue;
					s.tpoLast[r] = period; int n; s.tpo.TryGetValue(r, out n); s.tpo[r] = n + 1;
				}
			}
			if (mode == EGate0Mode.Canary) canary1m(end, o, h, l, c);
		}
		private void on5m(DateTime end, double o, double h, double l, double c, double v)
		{
			m_atr5.Add(h, l, c);
			if (!IsRthTime(end.TimeOfDay)) return;
			if (m_cur != null && l > 0 && h >= l) { double x = Math.Log(h / l); m_cur.pkSum += x * x; m_cur.pkN++; }
			if (StudyMode && m_studyOn) study.Bar(o, h, l, c, v);
		}
		private void on15m(DateTime end, double o, double h, double l, double c, double v)
		{
			if (mode == EGate0Mode.Canary && a253Minutes == 15) canaryGapBar(end);
		}
		private void on30m(DateTime end, double o, double h, double l, double c, double v)
		{
			double atrPrev = m_atr30.Value;
			m_atr30.Add(h, l, c);
			var bar = closeBlock();
			if (bar != null) dvBar(bar, atrPrev);
			if (mode == EGate0Mode.Canary) canary30m(end, o, h, l, c);
		}

		// ================================ DV level sources =================================
		/// <summary>One closed RTH 30m bar: DV_ROT_POC (DV-03/PS-13 rotation -> breakout -> follow-through) and DV_TREND_HVN (PS-12 trend
		/// leg HVN, armed after 2 closed 30m bars fully beyond it). Levels become valid for the NEXT session's registration.</summary>
		private void dvBar(Bar30 bar, double a)
		{
			int n = m_bars30.Count;
			// pending follow-through / arming
			for (int i = m_dvPending.Count - 1; i >= 0; i--)
			{
				var p = m_dvPending[i]; p.bars++;
				bool done = false, drop = p.bars > DvPendingBars;
				if (p.rot)
				{
					double w = p.boxH - p.boxL;
					if (p.bars > 1 && (p.dir > 0 ? bar.l <= p.level : bar.h >= p.level)) drop = true;   // retest before follow-through
					else if (p.dir > 0 ? bar.h >= p.boxH + w : bar.l <= p.boxL - w) done = true;
				}
				else
				{
					if (p.dir > 0 ? bar.l <= p.level : bar.h >= p.level) drop = true;                   // tested before armed
					else if (++p.armCount >= DvTrendArmBars) done = true;
				}
				if (done)
				{
					m_dv.Add(new Gate0DvLevel { type = p.type, price = p.level, dir = p.dir, validFrom = m_day.AddDays(1), expirySessions = p.rot ? DvRotExpirySessions : DvTrendExpirySessions });
					m_dvPending.RemoveAt(i);
				}
				else if (drop) m_dvPending.RemoveAt(i);
			}
			if (!(a > 0)) return;
			// rotation boxes
			foreach (int R in DvRotBars)
			{
				int cool; m_rotCool.TryGetValue(R, out cool); if (cool > 0) { m_rotCool[R] = cool - 1; continue; }
				if (n < R + 1) continue;
				double bh = double.MinValue, bl = double.MaxValue; var prof = new Gate0Profile(tick);
				for (int k = n - 1 - R; k < n - 1; k++) { var x = m_bars30[k]; if (x.h > bh) bh = x.h; if (x.l < bl) bl = x.l; prof.Merge(x.prof); }
				if (bh - bl > DvRotW * a || bar.h - bar.l < DvBreakAtr * a) continue;
				int dir = bar.c > bh ? 1 : bar.c < bl ? -1 : 0; if (dir == 0) continue;
				double poc, vh, vl; if (!prof.ValueArea(vaPct, out poc, out vh, out vl)) continue;
				var pend = new DvPending { type = "DV_ROT_POC_R" + R, dir = dir, boxH = bh, boxL = bl, level = poc, rot = true, bars = 1 };
				double w = bh - bl;
				if (dir > 0 ? bar.h >= bh + w : bar.l <= bl - w)
					m_dv.Add(new Gate0DvLevel { type = pend.type, price = poc, dir = dir, validFrom = m_day.AddDays(1), expirySessions = DvRotExpirySessions });
				else m_dvPending.Add(pend);
				m_rotCool[R] = R;
			}
			// trend-leg HVN
			if (m_trendCool > 0) { m_trendCool--; return; }
			if (n < DvTrendLookback + 1) return;
			double prevHi = double.MinValue, prevLo = double.MaxValue;
			for (int k = n - 1 - DvTrendLookback; k < n - 1; k++) { if (m_bars30[k].h > prevHi) prevHi = m_bars30[k].h; if (m_bars30[k].l < prevLo) prevLo = m_bars30[k].l; }
			int td = bar.c > prevHi ? 1 : bar.c < prevLo ? -1 : 0; if (td == 0) return;
			int s0 = n - 1 - DvTrendLookback; double ext = td > 0 ? double.MaxValue : double.MinValue;
			for (int k = n - 1 - DvTrendLookback; k < n; k++) { double x = td > 0 ? m_bars30[k].l : m_bars30[k].h; if (td > 0 ? x < ext : x > ext) { ext = x; s0 = k; } }
			double legHi = double.MinValue, legLo = double.MaxValue; var leg = new Gate0Profile(tick);
			for (int k = s0; k < n; k++) { var x = m_bars30[k]; if (x.h > legHi) legHi = x.h; if (x.l < legLo) legLo = x.l; leg.Merge(x.prof); }
			if (legHi - legLo < DvTrendLegAtr * a) return;
			m_trendCool = DvTrendLookback / 2;
			var vols = new List<double>(leg.rows.Values); vols.Sort(); double med = vols[vols.Count / 2];
			List<double[]> hv, lv; leg.Nodes(KdeBandwidthTicks, KdeProminence, out hv, out lv);
			foreach (var node in hv)
			{
				double pr = Math.Round(node[0] / tick) * tick, rv; leg.rows.TryGetValue((long)Math.Round(pr / tick), out rv);
				if (rv < DvHvnMedianX * med) continue;
				if (td > 0 ? bar.l <= pr : bar.h >= pr) continue;   // the breakout bar itself is not beyond the level
				m_dvPending.Add(new DvPending { type = "DV_TREND_HVN", dir = td, level = pr, rot = false, bars = 0 });
			}
		}

		// ================================ levels =================================
		/// <summary>Raw (type, {price, age, quality}) candidates of the enabled families for the session that opens now.</summary>
		public List<KeyValuePair<string, double[]>> Candidates()
		{
			var r = new List<KeyValuePair<string, double[]>>();
			Action<string, double, int, double> addQ = (ty, px, age, q) => { if (px > 0 && !double.IsNaN(px) && px < 1e12 && age >= 1 && age <= MaxAge) r.Add(new KeyValuePair<string, double[]>(ty, new[] { px, age, q })); };
			Action<string, double, int> add = (ty, px, age) => addQ(ty, px, age, double.NaN);
			int n = m_hist.Count;
			Gate0Session p1 = n > 0 ? m_hist[n - 1] : null;
			if (Fam(EGate0Family.PriorProfile))
				for (int a = 1; a <= Math.Min(PriorProfileMaxAge, n); a++) { var s = m_hist[n - a]; add("POC", s.poc, a); add("VAH", s.vah, a); add("VAL", s.val, a); }
			if (Fam(EGate0Family.NakedPoc))
				for (int a = 2; a <= Math.Min(NakedPocMaxAge, n); a++) { var s = m_hist[n - a]; if (s.pocNaked) add("NPOC", s.poc, a); }
			if (p1 != null && Fam(EGate0Family.IbEdge)) { add("IBH", p1.ibH, 1); add("IBL", p1.ibL, 1); }
			if (m_cur != null && Fam(EGate0Family.Overnight) && m_cur.onH > double.MinValue) { add("ONH", m_cur.onH, 1); add("ONL", m_cur.onL, 1); }
			if (p1 != null && Fam(EGate0Family.Avwap))
			{
				add("AV_PH", p1.avHi.Value, 1); add("AV_PL", p1.avLo.Value, 1);
				if (m_avWeek.Ready) add("AV_WK", m_avWeek.Value, 1);
				if (m_avMonth.Ready) add("AV_MO", m_avMonth.Value, 1);
				if (p1.vwap.Ready) add("AV_OPEN", p1.vwap.Value, 1);
				if (m_avPc.Ready) add("AV_PC", m_avPc.Value, 1);
			}
			if (p1 != null && Fam(EGate0Family.Vwap) && p1.vwap.Ready)
			{ double vw = p1.vwap.Value, sd = p1.vwap.Sigma; add("VWAP", vw, 1); if (sd > 0) { add("VWAP+1", vw + sd, 1); add("VWAP-1", vw - sd, 1); } }
			if (Fam(EGate0Family.Weekly) && m_lastWeek != null && m_lastWeekAge >= 1 && m_lastWeekAge <= 5)
			{
				double pc, ph, pl; var w = new Gate0Profile(tick); foreach (var p in m_lastWeek) w.Merge(p);
				if (w.ValueArea(vaPct, out pc, out ph, out pl)) { add("WPOC", pc, m_lastWeekAge); add("WVAH", ph, m_lastWeekAge); add("WVAL", pl, m_lastWeekAge); }
			}
			if (Fam(EGate0Family.CompositeVa) && n >= CompositeSessions)
			{
				var cp = new Gate0Profile(tick); for (int i = n - CompositeSessions; i < n; i++) cp.Merge(m_hist[i].profile);
				double pc, ph, pl; if (cp.ValueArea(vaPct, out pc, out ph, out pl)) { add("CVAH", ph, 1); add("CVAL", pl, 1); }
			}
			if (p1 != null && Fam(EGate0Family.SinglePrint))
			{
				foreach (var run in Gate0Profile.TpoSinglePrints(p1.tpo, SinglePrintMinRows, tick)) { add("SP_HI", run[0], 1); add("SP_LO", run[1], 1); }
				foreach (var run in p1.profile.SinglePrintRuns(SinglePrintVolFrac, SinglePrintMinRows)) { add("SPV_HI", run[0], 1); add("SPV_LO", run[1], 1); }   // sensitivity arm
			}
			if (Fam(EGate0Family.DvSource) && m_cur != null)
				foreach (var dv in m_dv) if (!dv.touched && dv.validFrom <= m_cur.day) add(dv.type, dv.price, Math.Max(1, SessionsSince(dv.validFrom) - 1 + 1));
			if (p1 != null && Fam(EGate0Family.HvnLvn))
			{
				List<double[]> hv, lv; p1.profile.Nodes(KdeBandwidthTicks, KdeProminence, out hv, out lv);
				foreach (var x in hv) addQ("HVN", Math.Round(x[0] / tick) * tick, 1, x[1]);
				foreach (var x in lv) addQ("LVN", Math.Round(x[0] / tick) * tick, 1, x[1]);
				if (p1.hCv > 0)
				{
					p1.profile.Nodes(p1.hCv / tick, KdeProminence, out hv, out lv);   // sensitivity arm: h = prior session h_cv
					foreach (var x in hv) addQ("HVN_HCV", Math.Round(x[0] / tick) * tick, 1, x[1]);
					foreach (var x in lv) addQ("LVN_HCV", Math.Round(x[0] / tick) * tick, 1, x[1]);
				}
			}
			if (p1 != null && Fam(EGate0Family.Mixture) && p1.bars1m > 1)
			{
				var xs = new List<double>(); var ws = new List<double>(); foreach (var kv in p1.profile.rows) { xs.Add(kv.Key * tick); ws.Add(kv.Value); }
				var g = Gate0Gmm.Fit(xs, ws, GmmMaxK, p1.bars1m, tick);
				if (g != null) for (int k = 0; k < g.K; k++) { addQ("GMM_MU", g.mu[k], 1, g.pi[k]); addQ("GMM_EDGE", g.mu[k] - g.sd[k], 1, g.pi[k]); addQ("GMM_EDGE", g.mu[k] + g.sd[k], 1, g.pi[k]); }
			}
			if (p1 != null && Fam(EGate0Family.SigmaMixture)) sigmaComposite(addQ);
			return r;
		}

		/// <summary>A2-32: pool the last 20 valid sessions (current contract) in z = (p - sVWAP)/sigma_session, fit the GMM (K <= 4, BIC),
		/// translate to price with the prior VWAP and sigma-hat = EWMA(10) of the session sigmas.</summary>
		private void sigmaComposite(Action<string, double, int, double> addQ)
		{
			var ses = new List<Gate0Session>();
			for (int i = m_hist.Count - 1; i >= 0 && ses.Count < SigmaCompositeSessions; i--) if (m_hist[i].SigmaPrice > 0) ses.Add(m_hist[i]);
			if (ses.Count < SigmaCompositeSessions) return;
			const double bin = 0.05; var hist = new SortedDictionary<long, double>(); double nEff = 0;
			foreach (var s in ses)
			{
				double vw = s.vwap.Value, sg = s.SigmaPrice; nEff += s.bars1m;
				foreach (var kv in s.profile.rows) { long z = (long)Math.Round((kv.Key * tick - vw) / sg / bin); double x; hist.TryGetValue(z, out x); hist[z] = x + kv.Value / s.profile.total; }
			}
			var xs = new List<double>(); var ws = new List<double>(); foreach (var kv in hist) { xs.Add(kv.Key * bin); ws.Add(kv.Value); }
			var g = Gate0Gmm.Fit(xs, ws, GmmMaxK, nEff, bin / 2); if (g == null) return;
			var sig = new List<double>(); for (int i = ses.Count - 1; i >= 0; i--) sig.Add(ses[i].SigmaPrice);   // oldest first
			double a = 2.0 / (SigmaEwmaSpan + 1), sh = double.NaN; int st = Math.Max(0, sig.Count - SigmaEwmaSpan);
			for (int i = st; i < sig.Count; i++) sh = double.IsNaN(sh) ? sig[i] : a * sig[i] + (1 - a) * sh;
			double vp = ses[0].vwap.Value;
			for (int k = 0; k < g.K; k++)
			{
				addQ("SIG_MU", vp + g.mu[k] * sh, 1, g.pi[k]);
				addQ("SIG_EDGE", vp + (g.mu[k] - g.sd[k]) * sh, 1, g.pi[k]); addQ("SIG_EDGE", vp + (g.mu[k] + g.sd[k]) * sh, 1, g.pi[k]);
			}
		}

		private void registerLevels(Gate0Bar b)
		{
			double refPx = b.o, sigma = m_atr5.Value;   // A2-33: distance from the price AT REGISTRATION (RTH open); sigma frozen = ATR14(5m)
			if (!(sigma > 0) || !(refPx > 0)) return;
			study.SetSessionReference(refPx, sigma);
			Gate0Session p1 = m_hist.Count > 0 ? m_hist[m_hist.Count - 1] : null;
			int no = 0;
			foreach (var kv in Candidates())
			{
				no++;
				string type = kv.Key; double px = kv.Value[0]; int age = (int)kv.Value[1]; double q = kv.Value[2];
				var rng = new _Rng(_Rng.Seed(seed, _Rng.Hash(type), b.day.Ticks, no));
				if (mode == EGate0Mode.ControlFL)
				{
					double[] k = { 0.5, 1, 2 }; px += (rng.NextInt(2) == 0 ? -1 : 1) * k[rng.NextInt(3)] * sigma; type = "FL:" + type;
				}
				else if (mode == EGate0Mode.ControlRT)
				{
					if (p1 == null || p1.closes1m.Count == 0) continue;
					px = p1.closes1m[rng.NextInt(p1.closes1m.Count)]; type = "RT:" + type;
				}
				px = Math.Round(px / tick) * tick;
				string key = type + "@" + px.ToString("R", CultureInfo.InvariantCulture) + "#" + no;
				study.AddLevel(type, px, age, q, key);
			}
		}

		/// <summary>True when day is a roll session or the session before / after one (review M2).</summary>
		public bool InRollWindow(DateTime day)
		{
			int i = sessionDays.BinarySearch(day.Date); if (i < 0) return false;
			for (int k = Math.Max(0, i - 1); k <= Math.Min(sessionDays.Count - 1, i + 1); k++) if (rollDays.Contains(sessionDays[k])) return true;
			return false;
		}

		/// <summary>Holm step-down with a fixed family size m (>= the number of p-values present): conservative when some members
		/// of the family were tested in another run.</summary>
		public static void HolmFixed(List<LevelTypeResult> r, int m)
		{
			var v = r.Where(x => !double.IsNaN(x.p)).OrderBy(x => x.p).ToList(); m = Math.Max(m, v.Count); double run = 0;
			for (int i = 0; i < v.Count; i++) { run = Math.Max(run, Math.Min(1, (m - i) * v[i].p)); v[i].pHolm = run; }
		}

		/// <summary>Finish the study and score every level type with A2-33's reaction edge vs its 20 matched controls (t per type and
		/// per age, Holm across ALL Gate-0 types, quality terciles, half-life from _LevelStudy.Analyse) plus a session block-bootstrap CI.</summary>
		public List<Gate0Result> Finish()
		{
			var res = new List<Gate0Result>();
			if (!StudyMode) return res;
			study.Finish();
			var kept = new List<LevelEvent>();
			foreach (var e in study.Events) if (e.day < DevEnd && !InRollWindow(e.day)) kept.Add(e);
			var a2l = _LevelStudy.Analyse(kept, study.config);
			HolmFixed(a2l, AllTypes.Length);
			var a2 = new Dictionary<string, LevelTypeResult>(StringComparer.Ordinal);
			foreach (var r in a2l) { r.verdict = _LevelStudy.Verdict(r); a2[r.type] = r; if (journal != null) journal(_LevelStudy.Summary(r)); }
			var ctlBy = new Dictionary<string, List<LevelEvent>>();
			foreach (var e in kept)
			{
				if (!e.control) continue;
				string k = e.type + "|" + e.day.Ticks + "|" + e.levelNo; List<LevelEvent> c;
				if (!ctlBy.TryGetValue(k, out c)) ctlBy[k] = c = new List<LevelEvent>(); c.Add(e);
			}
			var diffs = new SortedDictionary<string, List<KeyValuePair<DateTime, double>>>(StringComparer.Ordinal);
			foreach (var e in kept)
			{
				if (e.control) continue;
				List<KeyValuePair<DateTime, double>> dl;
				if (!diffs.TryGetValue(e.type, out dl)) diffs[e.type] = dl = new List<KeyValuePair<DateTime, double>>();
				List<LevelEvent> ctl; ctlBy.TryGetValue(e.type + "|" + e.day.Ticks + "|" + e.levelNo, out ctl);
				double d = PairedReaction(e, ctl);
				if (!double.IsNaN(d)) dl.Add(new KeyValuePair<DateTime, double>(e.day, d));
			}
			foreach (var kv in diffs)
			{
				var r = new Gate0Result { type = kv.Key, verdictRun = VerdictRun };
				LevelTypeResult lr; a2.TryGetValue(kv.Key, out lr);
				ScoreEdge(r, kv.Value, lr, seed);
				if (!VerdictRun) r.verdict = "NOT A VERDICT RUN (Variant 0: all families, 5 controls) - " + r.verdict;
				res.Add(r);
				if (journal != null) journal(r.Line());
			}
			if (journal != null && !VerdictRun) journal("G0 Variant 0 is an overview only: not a verdict run. Re-run each family (Variant 1..13) with 20 controls for G0-d.");
			return res;
		}

		/// <summary>A2-33 paired difference: level reaction R minus the mean R of its touched matched controls (NaN if either is missing).</summary>
		public static double PairedReaction(LevelEvent lv, IList<LevelEvent> ctl)
		{
			if (lv == null || !lv.touched || double.IsNaN(lv.reaction) || ctl == null) return double.NaN;
			double s = 0; int n = 0;
			foreach (var c in ctl) if (c.touched && !double.IsNaN(c.reaction)) { s += c.reaction; n++; }
			return n == 0 ? double.NaN : lv.reaction - s / n;
		}

		/// <summary>G0-d decision: the A2-33 reaction edge (paired vs matched controls) with a 95% percentile CI from a moving block
		/// bootstrap over sessions (500 draws, blocks of 5 consecutive sessions, _Rng seeded), then the verdict.</summary>
		public static void ScoreEdge(Gate0Result r, IList<KeyValuePair<DateTime, double>> diffs, LevelTypeResult a2, long seed)
		{
			r.pairs = diffs.Count;
			var days = new SortedDictionary<DateTime, double[]>();   // per session {sum d, n}
			foreach (var kv in diffs) { double[] a; if (!days.TryGetValue(kv.Key, out a)) days[kv.Key] = a = new double[2]; a[0] += kv.Value; a[1]++; }
			var s = new List<double[]>(days.Values); r.sessions = s.Count;
			double sum = 0; foreach (var kv in diffs) sum += kv.Value;
			r.edge = diffs.Count > 0 ? sum / diffs.Count : double.NaN;
			if (a2 != null)
			{
				r.levels = a2.levels; r.touches = a2.touchedLevels; r.t = a2.t; r.p = a2.p; r.pHolm = a2.pHolm; r.a2Verdict = a2.verdict; r.halfLife = a2.halfLife;
				r.tByAge = a2.tByAge ?? new double[0]; r.nByAge = a2.nByAge ?? new int[0]; r.tByTercile = a2.tByTercile;
			}
			if (s.Count > 0)
			{
				var rng = new _Rng(_Rng.Seed(seed, _Rng.Hash("Gate0"), _Rng.Hash(r.type)));
				var draws = new List<double>(BootstrapDraws);
				int nb = (s.Count + BootstrapBlock - 1) / BootstrapBlock, starts = Math.Max(1, s.Count - BootstrapBlock + 1);
				for (int d = 0; d < BootstrapDraws; d++)
				{
					double t = 0, n = 0;
					for (int k = 0; k < nb; k++)
					{
						int st = rng.NextInt(starts);
						for (int j = st; j < Math.Min(s.Count, st + BootstrapBlock); j++) { t += s[j][0]; n += s[j][1]; }
					}
					if (n > 0) draws.Add(t / n);
				}
				draws.Sort();
				if (draws.Count >= 20) { r.ciLo = draws[(int)Math.Floor(0.025 * (draws.Count - 1))]; r.ciHi = draws[(int)Math.Ceiling(0.975 * (draws.Count - 1))]; }
			}
			r.verdict = Verdict(r.pairs, r.t, r.a2Verdict);
		}

		/// <summary>G0-d verdict. §2.3 sample-size gates on the paired n, then A2-33's decision rule as computed by _LevelStudy.Verdict
		/// (Holm across all Gate-0 types): top-quality-tercile restriction passes even when the pooled |t| &lt; 2; otherwise |t| &lt; 2 ->
		/// not traded; "keep up to age h" = t &gt; 3 with decay (half-life).</summary>
		public static string Verdict(int n, double t, string a2)
		{
			if (n < 30) return "PARK (no evaluation: n<30)";
			if (n < 150) return "PARK (inconclusive: 30<=n<150)";
			if (a2 != null && a2.StartsWith("trade top")) return "PASS (" + a2 + ")";
			if (double.IsNaN(t) || Math.Abs(t) < 2) return "PARK (kill: edge ~ 0, |t| < 2 - level type not traded)";
			if (a2 != null && a2.StartsWith("keep")) return "PASS (" + a2 + ")";
			return "PARK (kill: " + (string.IsNullOrEmpty(a2) ? "A2-33 drop" : a2) + ")";
		}

		// ================================ canaries =================================
		private double m_vwPrev = double.NaN; private bool m_touched1, m_longDone, m_shortDone, m_dayDone, m_rthLive;
		private double m_priorClose = double.NaN, m_gap, m_sessHi, m_sessLo; private bool m_a253Ok, m_reversed; private int m_bar30, m_barG;
		private double m_dvPoc = double.NaN, m_dvSl; private int m_dvState;   // 0 wait move-away, 1 armed, 2 done
		private Gate0Session m_prior; private bool m_psOutside; private int m_psSide;
		private readonly List<double> m_wkUpper = new List<double>(), m_wkLower = new List<double>();
		private int m_prevMp; private bool m_skipDay;

		private Gate0Session PriorSession { get { return m_hist.Count > 0 ? m_hist[m_hist.Count - 1] : null; } }

		private void canaryNewDay()
		{
			m_touched1 = m_longDone = m_shortDone = m_dayDone = m_rthLive = m_reversed = false;
			m_vwPrev = double.NaN; m_bar30 = 0; m_barG = 0; m_dvState = 0; m_psOutside = false; m_psSide = 0;
			if (mode == EGate0Mode.Canary) intent.Clear();
		}

		private void canaryRthOpen(Gate0Bar b)
		{
			if (mode != EGate0Mode.Canary) return;
			m_prior = PriorSession;
			bool rollDay = m_cur.roll, afterRoll = sessionDays.Count >= 2 && rollDays.Contains(sessionDays[sessionDays.Count - 2]);
			m_skipDay = b.day >= DevEnd || rollDay || afterRoll || m_prior == null || !(m_atrD.Value > 0);
			m_rthLive = !m_skipDay;
			if (rollDay || afterRoll) { RollSkips++; if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "G0-b roll-skip {0:yyyy-MM-dd} total({1})", b.day, RollSkips)); }
			m_sessHi = b.h; m_sessLo = b.l;
			m_priorClose = m_prior != null ? m_prior.rthC : double.NaN;
			m_gap = m_prior != null ? (b.o - m_priorClose) / m_priorClose : 0;
			if (m_prior != null && Cv == EGate0Canary.A2_53)
			{
				m_a253Ok = Math.Abs(m_gap) >= a253Gap && (!a253Filter || VolFilter());
			}
			if (m_prior != null) { m_dvPoc = m_prior.poc; m_dvSl = dv01F * m_atrD.Value; m_psOutside = b.o > m_prior.vah || b.o < m_prior.val; m_psSide = b.o > m_prior.vah ? -1 : 1; }
		}


		/// <summary>A2-53 inside day, INCLUSIVE: H &lt;= H[-1] and L &gt;= L[-1].</summary>
		public static bool InsideDay(double h, double l, double h1, double l1) { return h <= h1 && l >= l1; }
		private bool VolFilter()
		{
			bool inside = m_hist.Count >= 2 && InsideDay(m_prior.rthH, m_prior.rthL, m_hist[m_hist.Count - 2].rthH, m_hist[m_hist.Count - 2].rthL);
			return inside || Hv(6) < Hv(100);
		}

		/// <summary>Sample stdev of the last n RTH close-to-close log returns (the return across a roll splice is skipped); NaN when short.</summary>
		public double Hv(int n)
		{
			int c = m_dailyRets.Count; if (c < n || n < 2) return double.NaN;
			double m = 0; var r = new double[n];
			for (int i = 0; i < n; i++) { r[i] = m_dailyRets[c - n + i]; m += r[i]; }
			m /= n; double ss = 0; foreach (var x in r) ss += (x - m) * (x - m);
			return Math.Sqrt(ss / (n - 1));
		}

		private void enter(int side, EGate0Entry kind, double px, double stop, double target, string tag)
		{
			intent.side = side; intent.entry = kind; intent.entryPrice = px; intent.stop = stop; intent.target = target; intent.tag = tag; intent.flatten = false;
		}

		/// <summary>Raw bar step: position bookkeeping and the 15:55 flat.</summary>
		private void canaryBar(Gate0Bar b, bool rth)
		{
			if (m_mp != 0 && m_prevMp == 0) { Trades++; intent.entry = EGate0Entry.None; if (journal != null) journal(string.Format(CultureInfo.InvariantCulture, "G0-b {0} fill side({1}) {2:yyyy-MM-dd HH:mm}", Cv, m_mp, b.et)); }
			if (m_mp != 0 && m_prevMp != 0 && m_mp != m_prevMp) { Trades++; onReversed(); }
			if (m_mp == 0 && m_prevMp != 0) { intent.stop = intent.target = intent.reverseStop = double.NaN; if (Cv != EGate0Canary.VW08) intent.side = 0; }
			m_prevMp = m_mp;
			if (rth) { if (b.h > m_sessHi) m_sessHi = b.h; if (b.l < m_sessLo) m_sessLo = b.l; }
			TimeSpan t = b.et.TimeOfDay;
			if (t >= new TimeSpan(15, 55, 0) || !rth)
			{
				m_rthLive = false;
				if (m_mp != 0 || intent.entry != EGate0Entry.None) { intent.Clear(); intent.flatten = m_mp != 0; }
				else intent.flatten = false;
			}
		}

		private void onReversed()
		{
			if (Cv == EGate0Canary.A2_53)
			{
				m_reversed = true; intent.reverseStop = double.NaN;
				intent.stop = m_mp > 0 ? m_sessLo - tick : m_sessHi + tick;
			}
		}

		private void canary1m(DateTime end, double o, double h, double l, double c)
		{
			var cv = Cv;
			double vw = m_cur.vwap.Value, sd = m_cur.vwap.Sigma, vwBefore = m_vwPrev;
			m_vwPrev = vw;
			if (!m_rthLive || end.TimeOfDay >= new TimeSpan(15, 55, 0)) return;
			switch (cv)
			{
				case EGate0Canary.VW08:
					if (c > vw) enter(1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "VW08L");
					else if (c < vw) enter(-1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "VW08S");
					break;
				// A2-24 (catalogue VW-08 entry, verbatim): (i) fade the first sVWAP touch 1R/1R - R = ATR14 on 5m is Coder-defined (spec silent);
				// (ii) fade a 2-sigma band close, target = sVWAP FROZEN AT ENTRY (intent.target is set once in enter() and never moved);
				// stop = mirror distance (Coder-defined); (iii) 2xATR-from-VWAP fade, ATR = ATR14 on 5m, stop mirror distance (Coder-defined).
				case EGate0Canary.A2_24_i:
					if (m_touched1 || double.IsNaN(vwBefore) || end.TimeOfDay < new TimeSpan(9, 35, 0)) break;
					if (l <= vwBefore && vwBefore <= h)
					{
						m_touched1 = true; double R = m_atr5.Value; if (!(R > 0) || m_mp != 0) break;
						int side = o >= vwBefore ? 1 : -1;   // approached from above -> VWAP as support -> long
						enter(side, EGate0Entry.Market, double.NaN, c - side * R, c + side * R, "A224i");
					}
					break;
				case EGate0Canary.A2_24_ii:
					if (m_mp != 0 || !(sd > 0)) break;
					if (c > vw + 2 * sd && !m_shortDone) { m_shortDone = true; enter(-1, EGate0Entry.Market, double.NaN, c + (c - vw), vw, "A224ii"); }
					else if (c < vw - 2 * sd && !m_longDone) { m_longDone = true; enter(1, EGate0Entry.Market, double.NaN, c - (vw - c), vw, "A224ii"); }
					break;
				case EGate0Canary.A2_24_iii:
					{
						double a = m_atr5.Value; if (m_mp != 0 || !(a > 0)) break;
						if (c - vw >= 2 * a && !m_shortDone) { m_shortDone = true; enter(-1, EGate0Entry.Market, double.NaN, c + (c - vw), vw, "A224iii"); }
						else if (vw - c >= 2 * a && !m_longDone) { m_longDone = true; enter(1, EGate0Entry.Market, double.NaN, c - (vw - c), vw, "A224iii"); }
					}
					break;
				case EGate0Canary.DV01:
					if (double.IsNaN(m_dvPoc) || m_dvState == 2) break;
					if (m_dvState == 0)
					{
						bool touch = l <= m_dvPoc && m_dvPoc <= h;
						double away = Math.Max(Math.Abs(h - m_dvPoc), Math.Abs(l - m_dvPoc)), need = dv01K * m_atr5.Value;
						if (touch && !(Math.Abs(o - m_dvPoc) >= need)) { m_dvState = 2; break; }   // first touch came before the move-away
						if (need > 0 && (Math.Abs(c - m_dvPoc) >= need || away >= need) && !touch)
						{
							m_dvState = 1; int side = c > m_dvPoc ? 1 : -1;
							enter(side, EGate0Entry.Limit, m_dvPoc, m_dvPoc - side * m_dvSl, m_dvPoc + side * m_dvSl, "DV01");
						}
					}
					else if (m_dvState == 1 && l <= m_dvPoc && m_dvPoc <= h)
					{
						m_dvState = 2; if (m_mp == 0) intent.entry = EGate0Entry.None;   // first touch only: unfilled -> cancel
					}
					break;
			}
		}

		/// <summary>A2-53 entry check on the close of the j-th RTH bar of the swept size (30m or 15m).</summary>
		private void canaryGapBar(DateTime end)
		{
			if (Cv != EGate0Canary.A2_53 || !IsRthTime(end.TimeOfDay)) return;
			m_barG++;
			if (!m_rthLive || end.TimeOfDay >= new TimeSpan(15, 55, 0)) return;
			if (m_barG != a253Bar || !m_a253Ok || m_mp != 0) return;
			if (m_gap > 0 && m_sessLo > m_priorClose) { enter(-1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "A253S"); intent.reverseStop = m_sessHi + tick; }
			else if (m_gap < 0 && m_sessHi < m_priorClose) { enter(1, EGate0Entry.Market, double.NaN, double.NaN, double.NaN, "A253L"); intent.reverseStop = m_sessLo - tick; }
		}

		private void canary30m(DateTime end, double o, double h, double l, double c)
		{
			if (a253Minutes == 30) canaryGapBar(end);
			var cv = Cv;
			if (!IsRthTime(end.TimeOfDay)) { if (cv == EGate0Canary.DV10) wkBand(); return; }
			m_bar30++;
			if (cv == EGate0Canary.DV10) wkBand();
			if (!m_rthLive || end.TimeOfDay >= new TimeSpan(15, 55, 0)) return;
			switch (cv)
			{
				case EGate0Canary.PS03:
					if (!m_psOutside || m_dayDone || m_mp != 0 || m_prior == null) break;
					if (c < m_prior.vah && c > m_prior.val)
					{
						m_dayDone = true; double W = m_prior.vah - m_prior.val;
						if (m_psSide < 0) enter(-1, EGate0Entry.Market, double.NaN, m_prior.vah + 0.25 * W, m_prior.poc, "PS03S");
						else enter(1, EGate0Entry.Market, double.NaN, m_prior.val - 0.25 * W, m_prior.poc, "PS03L");
					}
					break;
				case EGate0Canary.DV10:
					{
						if (m_dayDone || m_mp != 0) break;
						double vw = m_wkVwap.Value, sd = m_wkVwap.Sigma, a = m_atr30.Value; int n = m_wkUpper.Count;
						if (!(sd > 0) || !(a > 0) || n < 9) break;
						bool flat = Math.Abs(m_wkUpper[n - 1] - m_wkUpper[n - 9]) < dv10S * a && Math.Abs(m_wkLower[n - 1] - m_wkLower[n - 9]) < dv10S * a;
						if (!flat) { if (intent.entry == EGate0Entry.Limit) intent.entry = EGate0Entry.None; break; }
						double sl = dv10F * m_atrD.Value;
						if (c > vw && c < vw + sd) enter(-1, EGate0Entry.Limit, Math.Round((vw + sd) / tick) * tick, vw + sd + sl, vw, "DV10S");
						else if (c < vw && c > vw - sd) enter(1, EGate0Entry.Limit, Math.Round((vw - sd) / tick) * tick, vw - sd - sl, vw, "DV10L");
					}
					break;
			}
		}

		private void wkBand()
		{
			double vw = m_wkVwap.Value, sd = m_wkVwap.Sigma; if (double.IsNaN(vw)) return;
			m_wkUpper.Add(vw + sd); m_wkLower.Add(vw - sd);
			if (m_wkUpper.Count > 64) { m_wkUpper.RemoveAt(0); m_wkLower.RemoveAt(0); }
		}
	}
}
