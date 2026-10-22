using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 VP-4 + VP-6 — pure statistics on a profile histogram or a closed-bar window (no state, no laziness needed),
	// plus the stateful _EntropyTracker (same-clock-slot percentile rings). Pure C# (tests/VpHelperTests).
	//
	// VP-4 (review Q4 / H3): entropy over DENSE 1-tick rows on [L, H]; Miller-Madow N = contributing 1m bars; NO min(1, ...) clip
	// on H_norm,MM. Plain H_norm is the pre-registered A2-29 / A2-30 decision variable, H_MM a sensitivity arm; journal both.
	// KL (A2-31) on 1-tick histograms with eps = 1e-6, and (review M4) also on KDE-smoothed densities (h = h_cv).
	// Parkinson session scale (Q5): sigma_session = sigma_bar(5m) x sqrt(n_bars) x sVWAP; sessions with &lt; 60 RTH 5m bars are
	// dropped from the pool (NaN here); tomorrow's sigma-hat = EWMA (span 10, alpha = 2/11) of the last 10 valid values.
	// AggregateBars (review Q6 / H5): STATISTICS ONLY (Parkinson sigma, ATR(14, 30m) size thresholds). Every rule trigger
	// ("2 consecutive 5m/15m closes", A2-27/28/30/32/35) must use a real Data n stream through HtfClosed, never these bars.
	//
	// VP-6: (a) block-bootstrap POC: 500 draws, moving blocks of 5 1m bars, SplitMix64-seeded (review M6; Keller resamples
	// trades with 1000 draws - run 1000 once as a cost / precision check). (b) KDE bandwidth sweep: POC-stability grid
	// 0.05..1.5 sigma x 30 (unchanged); h_cv by 5-fold CV BY 1m BAR (row weights rebuilt per fold) on Keller's grid
	// 0.15..1.2 sigma, 25 log-spaced points (review H1); median-jump floor 1 tick (review Q7; floor 0 = literal Keller arm).
	// (c) V/T urgency prices (A2-34).
	// ============================================================================================

	// <profile-stats>
	public enum EEntropyRows { DenseRange = 0, TradedRows = 1 }
	public enum EMmN { Bars = 0, Trades = 1, Contracts = 2 }
	public enum EKdeVerdict { Unknown = 0, Stable, Flat, Bimodal }
	public enum ECvFolding { Contiguous = 0, Interleaved = 1 }

	public sealed class ProfileStatsConfig
	{
		public bool enabled = false;                 // host convenience accessors + Data1 ring (the static math needs no switch)
		public EEntropyRows entropyRows = EEntropyRows.DenseRange;
		public EMmN mmN = EMmN.Bars;
		public double klEpsilon = 1e-6;
		public int parkinsonMinBars = 60;
		public int ewmaSessions = 10;                // span; alpha = 2 / (span + 1) = 2/11
		public int pctLookbackA229 = 250;
		public int pctLookbackA230 = 60;
		public double pctMinFill = 0.8;
		public int bootstrapBlock = 5, bootstrapDraws = 500;
		public double kdeMedFloorTicks = 1;          // review Q7: 1-tick floor; 0 = literal Keller sensitivity arm
		public int kdeFolds = 5;
		public ECvFolding kdeFolding = ECvFolding.Contiguous;
		public int ringBars = 1440;

		public string Validate()
		{
			var e = new List<string>();
			if (!(klEpsilon > 0)) e.Add("klEpsilon=" + klEpsilon);
			if (parkinsonMinBars < 1) e.Add("parkinsonMinBars=" + parkinsonMinBars);
			if (ewmaSessions < 1) e.Add("ewmaSessions=" + ewmaSessions);
			if (pctLookbackA229 < 1 || pctLookbackA230 < 1) e.Add("pctLookback");
			if (!(pctMinFill > 0 && pctMinFill <= 1)) e.Add("pctMinFill=" + pctMinFill);
			if (bootstrapBlock < 1 || bootstrapDraws < 1) e.Add("bootstrap");
			if (kdeMedFloorTicks < 0) e.Add("kdeMedFloorTicks=" + kdeMedFloorTicks);
			if (kdeFolds < 2) e.Add("kdeFolds=" + kdeFolds);
			if (ringBars < 1) e.Add("ringBars=" + ringBars);
			return e.Count == 0 ? null : "ProfileStatsConfig invalid: " + string.Join(", ", e);
		}

		public string Canon()
		{
			return string.Format(CultureInfo.InvariantCulture, "ps:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}:{12}", (int)entropyRows, (int)mmN, klEpsilon,
				parkinsonMinBars, ewmaSessions, pctLookbackA229, pctLookbackA230, pctMinFill, bootstrapBlock, bootstrapDraws, kdeMedFloorTicks, kdeFolds, (int)kdeFolding);
		}
	}

	public struct EntropyResult
	{
		public double H, Hnorm, Hmm, HnormMm, Neff, N;
		public int n, m;   // n = rows used for normalisation, m = traded rows
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Hn({0:0.#####}) Hmm({1:0.#####}) neff({2:0.##}) n({3}) m({4}) N({5})", Hnorm, HnormMm, Neff, n, m, N);
		}
	}

	public sealed class BootstrapResult
	{
		public double medianPoc = double.NaN, ci90Ticks = double.NaN, stability = double.NaN, zoneLo = double.NaN, zoneHi = double.NaN;
		public double[] pocs = new double[0];
		public int bars, draws, block; public ulong seed;
	}

	public sealed class KdeResult
	{
		public EKdeVerdict verdict = EKdeVerdict.Unknown;
		public double sigma = double.NaN, maxJ = double.NaN, medJ = double.NaN, range = double.NaN, hCv = double.NaN, cvPoc = double.NaN;
		public ECvFolding folding = ECvFolding.Contiguous;   // scheme that produced hCv / cvPoc (ruling 1: Contiguous default)
		public double hCvAlt = double.NaN;                   // h_cv under the other fold scheme (ruling 1: journal both; Interleaved = sensitivity arm)
		public double HcvContiguous { get { return folding == ECvFolding.Contiguous ? hCv : hCvAlt; } }
		public double HcvInterleaved { get { return folding == ECvFolding.Interleaved ? hCv : hCvAlt; } }
		/// <summary>Journal line: PSTAT KDE {verdict} maxJ medJ range cvPoc hcvC hcvI (h_cv under BOTH fold schemes).</summary>
		public string Journal()
		{
			return string.Format(CultureInfo.InvariantCulture, "PSTAT KDE {0} maxJ({1}) medJ({2}) range({3}) cvPoc({4}) hcvC({5:0.######}) hcvI({6:0.######}) fold({7})",
				verdict.ToString().ToUpperInvariant(), maxJ, medJ, range, cvPoc, HcvContiguous, HcvInterleaved, folding);
		}
		public bool disagree;
		public double[] h = new double[0], poc = new double[0];        // POC-stability sweep (30 points)
		public double[] cvH = new double[0], cvLL = new double[0];     // CV grid (25 points) and summed held-out log-likelihood
	}

	public sealed class VtResult
	{
		public double volPoc = double.NaN, tpoPoc = double.NaN, atr = double.NaN;
		public readonly List<double> prices = new List<double>(), ratios = new List<double>();
	}

	public static class _ProfileStats
	{
		// ---------------- entropy ----------------

		/// <summary>
		/// Shannon entropy of a dense 1-tick histogram (zero rows included). H = -sum p ln p; H_norm = H / ln n with n = dense rows
		/// (DenseRange) or traded rows (TradedRows); H_norm NaN when n &lt;= 1 or sum v = 0. Miller-Madow H_MM = H + (m - 1) / (2N),
		/// m = traded rows, N = mmN (NaN / &lt;= 0 gives NaN). H_norm,MM = H_MM / ln n, NOT clipped (review H3). n_eff = 1 / sum p^2.
		/// </summary>
		public static EntropyResult Entropy(double[] vols, EEntropyRows rows, double mmN)
		{
			var r = new EntropyResult { H = double.NaN, Hnorm = double.NaN, Hmm = double.NaN, HnormMm = double.NaN, Neff = double.NaN, N = mmN };
			if (vols == null || vols.Length == 0) return r;
			double sum = 0; int m = 0;
			for (int i = 0; i < vols.Length; i++) if (vols[i] > 0) { sum += vols[i]; m++; }
			r.m = m;
			r.n = rows == EEntropyRows.TradedRows ? m : vols.Length;
			if (!(sum > 0)) return r;
			double h = 0, s2 = 0;
			for (int i = 0; i < vols.Length; i++)
				if (vols[i] > 0) { double p = vols[i] / sum; h -= p * Math.Log(p); s2 += p * p; }
			r.H = h; r.Neff = 1.0 / s2;
			if (mmN > 0) r.Hmm = h + (m - 1) / (2.0 * mmN);
			if (r.n > 1)
			{
				double ln = Math.Log(r.n);
				r.Hnorm = h / ln;
				if (!double.IsNaN(r.Hmm)) r.HnormMm = r.Hmm / ln;
			}
			return r;
		}

		/// <summary>Miller-Madow N for a window: bars (default), trades (ladder tier &lt;= TickRule only; NaN when unknown) or contracts (sum v).</summary>
		public static double MmN(EMmN mode, int bars, double trades, double contracts)
		{
			switch (mode)
			{
				case EMmN.Trades: return trades > 0 ? trades : double.NaN;
				case EMmN.Contracts: return contracts > 0 ? contracts : double.NaN;
				default: return bars > 0 ? bars : double.NaN;
			}
		}

		// ---------------- KL divergence ----------------

		/// <summary>
		/// D_KL(P || Q) in nats on a common 1-tick grid covering both ranges; eps added to every row of both, then renormalised.
		/// P = today's developing RTH profile, Q = the prior full RTH profile (A2-31). NaN when either is empty.
		/// </summary>
		public static double KL(double[] pPrices, double[] pVols, double[] qPrices, double[] qVols, double tick, double eps)
		{
			long lo, hi;
			if (!commonGrid(pPrices, pVols, qPrices, qVols, tick, 0, out lo, out hi)) return double.NaN;
			int n = (int)(hi - lo + 1);
			double[] p = new double[n], q = new double[n];
			scatter(pPrices, pVols, tick, lo, p); scatter(qPrices, qVols, tick, lo, q);
			return klDense(p, q, eps);
		}

		/// <summary>
		/// Ruling 2 default for A2-31: KL on KDE densities with h = the PRIOR trading day's h_cv (no lookahead), the same h for both P and Q.
		/// priorHcv comes from _PriorDayValue.Before(today). NaN when there is no prior h_cv.
		/// </summary>
		public static double KLKdePrior(double[] pPrices, double[] pVols, double[] qPrices, double[] qVols, double tick, double priorHcv, double eps)
		{
			return KLKde(pPrices, pVols, qPrices, qVols, tick, priorHcv, eps);
		}

		/// <summary>
		/// Review M4: KL on KDE-smoothed densities (Gaussian kernel, bandwidth h in price units, truncated at 5h), on a common 1-tick grid
		/// extended by ceil(5h / tick) rows each side; eps added after normalisation as in KL. NaN when h is not positive.
		/// </summary>
		public static double KLKde(double[] pPrices, double[] pVols, double[] qPrices, double[] qVols, double tick, double h, double eps)
		{
			if (!(h > 0) || !(tick > 0)) return double.NaN;
			int ext = (int)Math.Min(100000, Math.Ceiling(5 * h / tick));
			long lo, hi;
			if (!commonGrid(pPrices, pVols, qPrices, qVols, tick, ext, out lo, out hi)) return double.NaN;
			int n = (int)(hi - lo + 1);
			double[] p = KdeOnGrid(pPrices, pVols, lo * tick, n, tick, h, 5);
			double[] q = KdeOnGrid(qPrices, qVols, lo * tick, n, tick, h, 5);
			return klDense(p, q, eps);
		}

		private static bool commonGrid(double[] pp, double[] pv, double[] qp, double[] qv, double tick, int ext, out long lo, out long hi)
		{
			lo = long.MaxValue; hi = long.MinValue;
			if (!(tick > 0) || pp == null || qp == null || pv == null || qv == null) return false;
			double sp = 0, sq = 0;
			for (int i = 0; i < pp.Length; i++) if (pv[i] > 0) { long k = _ProfileMath.Key(pp[i], tick); if (k < lo) lo = k; if (k > hi) hi = k; sp += pv[i]; }
			for (int i = 0; i < qp.Length; i++) if (qv[i] > 0) { long k = _ProfileMath.Key(qp[i], tick); if (k < lo) lo = k; if (k > hi) hi = k; sq += qv[i]; }
			if (!(sp > 0) || !(sq > 0) || hi - lo + 1 > 400000) return false;
			lo -= ext; hi += ext;
			return true;
		}

		private static void scatter(double[] prices, double[] vols, double tick, long lo, double[] dst)
		{
			for (int i = 0; i < prices.Length; i++) if (vols[i] > 0) { long k = _ProfileMath.Key(prices[i], tick) - lo; if (k >= 0 && k < dst.Length) dst[k] += vols[i]; }
		}

		private static double klDense(double[] p, double[] q, double eps)
		{
			int n = p.Length;
			double sp = 0, sq = 0;
			for (int i = 0; i < n; i++) { sp += p[i]; sq += q[i]; }
			if (!(sp > 0) || !(sq > 0)) return double.NaN;
			double zp = 0, zq = 0;
			for (int i = 0; i < n; i++) { zp += p[i] / sp + eps; zq += q[i] / sq + eps; }
			double d = 0;
			for (int i = 0; i < n; i++)
			{
				double pi = (p[i] / sp + eps) / zp, qi = (q[i] / sq + eps) / zq;
				d += pi * Math.Log(pi / qi);
			}
			return d;
		}

		/// <summary>Gaussian KDE f(x) = sum_i v_i phi((x - x_i) / h) on a 1-tick grid starting at gridLo (unnormalised; kernel truncated at trunc x h).</summary>
		public static double[] KdeOnGrid(double[] prices, double[] vols, double gridLo, int gridN, double tick, double h, double trunc)
		{
			var f = new double[gridN];
			if (!(h > 0)) return f;
			int w = (int)Math.Floor(trunc * h / tick + 1e-9);
			long lo = _ProfileMath.Key(gridLo, tick);
			for (int i = 0; i < prices.Length; i++)
			{
				if (!(vols[i] > 0)) continue;
				long c = _ProfileMath.Key(prices[i], tick) - lo;
				for (long g = Math.Max(0, c - w); g <= Math.Min(gridN - 1, c + w); g++)
				{
					double z = (g - c) * tick / h;
					f[g] += vols[i] * Math.Exp(-0.5 * z * z);
				}
			}
			return f;
		}

		// ---------------- Parkinson / bar aggregation ----------------

		/// <summary>Parkinson sigma per bar (fractional): sigma^2 = sum (ln(H/L))^2 / (4 n ln 2) over bars with L &gt; 0 (H = L counts as 0). Roll (splice) bars excluded. NaN with no bar.</summary>
		public static double ParkinsonPerBar(IList<RingBar> bars, out int n)
		{
			n = 0; double s = 0;
			if (bars == null) return double.NaN;
			foreach (RingBar b in bars)
			{
				if (b.rollBar || !(b.l > 0) || b.h < b.l) continue;
				double x = Math.Log(b.h / b.l); s += x * x; n++;
			}
			return n > 0 ? Math.Sqrt(s / (4.0 * n * Math.Log(2))) : double.NaN;
		}

		public static double ParkinsonPerBar(IList<RingBar> bars) { int n; return ParkinsonPerBar(bars, out n); }

		// Ruling 4 (G-P scope, not built here): the A2-32 20-session sigma pool CROSSES rolls (sigma-space is centred per session on that
		// session's sVWAP, review M1) and excludes only the roll session itself, which is flagged (_RollWindow.IsRollDay); sessions with
		// fewer than parkinsonMinBars RTH 5m bars are dropped and the pool is filled from older sessions.
		/// <summary>Session-scale Parkinson in price units: refPrice x sigma_bar x sqrt(n). NaN with fewer than minBars usable bars (A2-32: 60 RTH 5m bars; a
		/// shorter session, e.g. a 13:00 half day, is dropped from the pool rather than NaN-ing the composite).</summary>
		public static double ParkinsonSessionPrice(IList<RingBar> bars, double refPrice, int minBars)
		{
			int n; double s = ParkinsonPerBar(bars, out n);
			if (n < minBars || !(refPrice > 0) || double.IsNaN(s)) return double.NaN;
			return refPrice * s * Math.Sqrt(n);
		}

		/// <summary>Tomorrow's sigma-hat: EWMA (alpha = 2 / (span + 1)) over the last `span` valid (finite, &gt; 0) values, oldest first, seeded with the oldest of them. NaN with none.</summary>
		public static double EwmaForecast(IList<double> valuesOldestFirst, int span)
		{
			if (valuesOldestFirst == null || span < 1) return double.NaN;
			var v = new List<double>();
			for (int i = valuesOldestFirst.Count - 1; i >= 0 && v.Count < span; i--)
			{
				double x = valuesOldestFirst[i];
				if (x > 0 && !double.IsInfinity(x)) v.Add(x);
			}
			if (v.Count == 0) return double.NaN;
			double a = 2.0 / (span + 1), s = v[v.Count - 1];
			for (int i = v.Count - 2; i >= 0; i--) s = a * v[i] + (1 - a) * s;
			return s;
		}

		/// <summary>
		/// STATISTICS ONLY (review H5): clock-aligned bars of `size` from 1m bars, MC stamping: a bar stamped t belongs to the bucket
		/// ending at the first multiple of size (from midnight) &gt;= t, i.e. (end - size, end]. Buckets never span a trading-day
		/// change (session-boundary truncation). The aggregated bar is stamped with its bucket end; rollBar if any part is.
		/// Never use these for rule triggers: those need a real Data n stream through HtfClosed.
		/// </summary>
		public static List<RingBar> AggregateBars(IList<RingBar> bars, TimeSpan size)
		{
			var res = new List<RingBar>();
			if (bars == null || size <= TimeSpan.Zero) return res;
			bool open = false; RingBar cur = new RingBar(); DateTime curEnd = DateTime.MinValue;
			foreach (RingBar b in bars)
			{
				DateTime end = bucketEnd(b.time, size);
				if (!open || end != curEnd || b.tradingDay != cur.tradingDay)
				{
					if (open) res.Add(cur);
					cur = b; cur.time = end; curEnd = end; open = true;
					continue;
				}
				if (b.h > cur.h) cur.h = b.h;
				if (b.l < cur.l) cur.l = b.l;
				cur.c = b.c; cur.v += b.v; cur.up += b.up; cur.down += b.down; cur.bar = b.bar;
				cur.rollBar |= b.rollBar; cur.rth &= b.rth;
			}
			if (open) res.Add(cur);
			return res;
		}

		private static DateTime bucketEnd(DateTime t, TimeSpan size)
		{
			long into = (t - t.Date).Ticks, s = size.Ticks;
			long k = (into + s - 1) / s;
			return t.Date.AddTicks(k * s);
		}

		/// <summary>Wilder ATR(len) of the last bar (seeded with the SMA of the first len true ranges). NaN with fewer than len bars.</summary>
		public static double WilderAtr(IList<RingBar> bars, int len)
		{
			if (bars == null || len < 1 || bars.Count < len) return double.NaN;
			double atr = 0;
			for (int i = 0; i < bars.Count; i++)
			{
				RingBar b = bars[i];
				double tr = i == 0 ? b.h - b.l : Math.Max(b.h, bars[i - 1].c) - Math.Min(b.l, bars[i - 1].c);
				if (i < len) { atr += tr; if (i == len - 1) atr /= len; }
				else atr = (atr * (len - 1) + tr) / len;
			}
			return atr;
		}

		// ---------------- histogram helpers ----------------

		/// <summary>Per-bar row contributions exactly as ProfileMath.Accumulator.Add(mode) (1 close, 2 uniform, 3 typical).</summary>
		public static void BarRows(RingBar b, double tick, int mode, Action<long, double> add)
		{
			if (b.h < b.l || b.l <= 0 || b.v <= 0) return;
			if (mode == 2)
			{
				long a = _ProfileMath.Key(b.l, tick), z = _ProfileMath.Key(b.h, tick);
				double each = b.v / (z - a + 1);
				for (long k = a; k <= z; k++) add(k, each);
			}
			else if (mode == 3) add(_ProfileMath.Key((b.h + b.l + b.c) / 3.0, tick), b.v);
			else add(_ProfileMath.Key(b.c, tick), b.v);
		}

		/// <summary>Dense histogram of bars (indexes into bars, may repeat), via ProfileMath.Accumulator.</summary>
		public static bool Histogram(IList<RingBar> bars, IList<int> idx, double tick, int mode, out double[] prices, out double[] vols)
		{
			var acc = new _ProfileMath.Accumulator(tick);
			if (idx == null) foreach (RingBar b in bars) acc.Add(b.bar, b.time, b.o, b.h, b.l, b.c, b.v, mode);
			else foreach (int i in idx) { RingBar b = bars[i]; acc.Add(b.bar, b.time, b.o, b.h, b.l, b.c, b.v, mode); }
			acc.Dense(out prices, out vols);
			return prices.Length > 0;
		}

		/// <summary>Volume-weighted standard deviation of row prices.</summary>
		public static double WeightedSd(double[] prices, double[] vols)
		{
			double s = 0, m = 0;
			for (int i = 0; i < prices.Length; i++) if (vols[i] > 0) { s += vols[i]; m += vols[i] * prices[i]; }
			if (!(s > 0)) return double.NaN;
			m /= s;
			double v = 0;
			for (int i = 0; i < prices.Length; i++) if (vols[i] > 0) v += vols[i] * (prices[i] - m) * (prices[i] - m);
			return Math.Sqrt(v / s);
		}

		/// <summary>numpy-default (linear interpolation) percentile, q in [0, 100]. Sorts a copy.</summary>
		public static double Percentile(IList<double> values, double q)
		{
			if (values == null || values.Count == 0) return double.NaN;
			var a = new double[values.Count];
			values.CopyTo(a, 0);
			Array.Sort(a);
			return percentileSorted(a, q);
		}

		private static double percentileSorted(double[] a, double q)
		{
			double pos = q / 100.0 * (a.Length - 1);
			int lo = (int)Math.Floor(pos), hi = Math.Min(a.Length - 1, lo + 1);
			return a[lo] + (pos - lo) * (a[hi] - a[lo]);
		}

		/// <summary>Mid-rank percentile of x among values: 100 x (count(&lt; x) + 0.5 x count(= x)) / count. NaN when empty.</summary>
		public static double MidRank(IList<double> values, double x)
		{
			if (values == null || values.Count == 0 || double.IsNaN(x)) return double.NaN;
			int lt = 0, eq = 0;
			foreach (double v in values) { if (v < x) lt++; else if (v == x) eq++; }
			return 100.0 * (lt + 0.5 * eq) / values.Count;
		}

		// ---------------- VP-6 (a) block-bootstrap POC ----------------

		public static ulong BootstrapSeed(DateTime tradingDay, int block, int draws) { return _Rng.Seed(25, tradingDay.Ticks, block, draws); }

		/// <summary>
		/// Moving-block bootstrap of the session POC (A2-25). Each draw concatenates ceil(n / block) blocks, each starting uniformly in
		/// [0, n - block], truncated to n bars; the profile is rebuilt with `distribution` and the POC taken with ProfileMath.PocIndex's
		/// tie rule. medianPoc / ci90 (P95 - P5, ticks) use numpy linear percentiles; stability = share of draws with |POC - median| &lt;= 1 tick.
		/// zone = median -/+ ci90 / 2.
		/// </summary>
		public static BootstrapResult BootstrapPoc(IList<RingBar> bars, double tick, int distribution, int block, int draws, ulong seed)
		{
			var r = new BootstrapResult { bars = bars != null ? bars.Count : 0, draws = draws, block = block, seed = seed };
			if (bars == null || bars.Count == 0 || draws < 1 || block < 1 || !(tick > 0)) return r;
			int n = bars.Count, blk = Math.Min(block, n), nb = (n + blk - 1) / blk;
			var rng = new _Rng(seed);
			var pocs = new double[draws];
			var idx = new List<int>(n);
			for (int d = 0; d < draws; d++)
			{
				idx.Clear();
				for (int k = 0; k < nb && idx.Count < n; k++)
				{
					int s = rng.NextInt(n - blk + 1);
					for (int j = 0; j < blk && idx.Count < n; j++) idx.Add(s + j);
				}
				double[] p, v;
				if (!Histogram(bars, idx, tick, distribution, out p, out v)) { pocs[d] = double.NaN; continue; }
				pocs[d] = p[_ProfileMath.PocIndex(v)];
			}
			var ok = new List<double>(draws);
			foreach (double x in pocs) if (!double.IsNaN(x)) ok.Add(x);
			r.pocs = pocs;
			if (ok.Count == 0) return r;
			var a = ok.ToArray(); Array.Sort(a);
			r.medianPoc = percentileSorted(a, 50);
			r.ci90Ticks = (percentileSorted(a, 95) - percentileSorted(a, 5)) / tick;
			int st = 0;
			foreach (double x in a) if (Math.Abs(x - r.medianPoc) <= tick * (1 + 1e-9)) st++;
			r.stability = (double)st / a.Length;
			r.zoneLo = r.medianPoc - r.ci90Ticks / 2 * tick;
			r.zoneHi = r.medianPoc + r.ci90Ticks / 2 * tick;
			return r;
		}

		// ---------------- VP-6 (b) KDE bandwidth sweep ----------------

		public const int KDE_SWEEP_POINTS = 30, KDE_CV_POINTS = 25;
		public const double KDE_CV_LO = 0.15, KDE_CV_HI = 1.2;

		/// <summary>Keller's CV grid: sigma x exp(ln 0.15 + k (ln 1.2 - ln 0.15) / 24), k = 0..24 (log-spaced, review H1).</summary>
		public static double[] KdeCvGrid(double sigma)
		{
			var g = new double[KDE_CV_POINTS];
			double a = Math.Log(KDE_CV_LO), b = Math.Log(KDE_CV_HI);
			for (int k = 0; k < KDE_CV_POINTS; k++) g[k] = sigma * Math.Exp(a + k * (b - a) / (KDE_CV_POINTS - 1));
			return g;
		}

		/// <summary>
		/// A2-26 KDE sweep on the bars of a window. sigma = volume-weighted SD of the row prices; h_j = sigma (0.05 + j 1.45 / 29), j = 0..29;
		/// f_j on the 1-tick grid (kernel truncated at 5 h_j); POC_j = argmax f_j (ties to the lowest price); J_j = |POC_j - POC_{j-1}|;
		/// verdict BIMODAL if max J &gt; 5 med, STABLE if max J &lt; 2 med + 1e-9 and range &lt; 0.15 sigma, else FLAT, with
		/// med = max(median J, medFloorTicks x tick). h_cv = argmax of the held-out log-likelihood under `folds`-fold CV by bar.
		/// disagree = |mcPoc - POC at h_cv| &gt; 4 ticks (false when mcPoc is NaN).
		/// </summary>
		public static KdeResult KdeSweep(IList<RingBar> bars, double tick, int distribution, double mcPoc = double.NaN, double medFloorTicks = 1, int folds = 5, ECvFolding folding = ECvFolding.Contiguous)
		{
			double[] p, v;
			if (bars == null || bars.Count == 0 || !Histogram(bars, null, tick, distribution, out p, out v)) return new KdeResult();
			KdeResult r = KdeSweep(p, v, tick, medFloorTicks);
			if (!(r.sigma > 0)) return r;
			r.cvH = KdeCvGrid(r.sigma);
			r.folding = folding;
			r.cvLL = CvLogLik(bars, tick, distribution, r.cvH, folds, folding);
			int best = argmaxLL(r.cvLL);
			int alt = argmaxLL(CvLogLik(bars, tick, distribution, r.cvH, folds, folding == ECvFolding.Contiguous ? ECvFolding.Interleaved : ECvFolding.Contiguous));
			if (alt >= 0) r.hCvAlt = r.cvH[alt];
			if (best >= 0)
			{
				r.hCv = r.cvH[best];
				r.cvPoc = argmaxLowest(KdeOnGrid(p, v, p[0], p.Length, tick, r.hCv, 5), p);
				r.disagree = !double.IsNaN(mcPoc) && Math.Abs(mcPoc - r.cvPoc) > 4 * tick + 1e-9 * tick;
			}
			return r;
		}

		private static int argmaxLL(double[] ll)
		{
			int best = -1;
			for (int k = 0; k < ll.Length; k++) if (!double.IsNaN(ll[k]) && (best < 0 || ll[k] > ll[best])) best = k;
			return best;
		}

		/// <summary>The POC-stability sweep and verdict on a dense histogram (no CV: hCv / cvPoc stay NaN).</summary>
		public static KdeResult KdeSweep(double[] prices, double[] vols, double tick, double medFloorTicks = 1)
		{
			var r = new KdeResult();
			if (prices == null || vols == null || prices.Length == 0) return r;
			r.sigma = WeightedSd(prices, vols);
			if (double.IsNaN(r.sigma)) return r;
			if (r.sigma == 0) { r.verdict = EKdeVerdict.Stable; r.maxJ = r.medJ = r.range = 0; return r; }   // single traded row
			r.h = new double[KDE_SWEEP_POINTS]; r.poc = new double[KDE_SWEEP_POINTS];
			for (int j = 0; j < KDE_SWEEP_POINTS; j++)
			{
				r.h[j] = r.sigma * (0.05 + j * 1.45 / (KDE_SWEEP_POINTS - 1));
				r.poc[j] = argmaxLowest(KdeOnGrid(prices, vols, prices[0], prices.Length, tick, r.h[j], 5), prices);
			}
			var jumps = new double[KDE_SWEEP_POINTS - 1];
			double mx = 0, lo = double.MaxValue, hi = double.MinValue;
			for (int j = 0; j < KDE_SWEEP_POINTS; j++) { lo = Math.Min(lo, r.poc[j]); hi = Math.Max(hi, r.poc[j]); }
			for (int j = 1; j < KDE_SWEEP_POINTS; j++) { jumps[j - 1] = Math.Abs(r.poc[j] - r.poc[j - 1]); mx = Math.Max(mx, jumps[j - 1]); }
			r.maxJ = mx; r.medJ = Percentile(jumps, 50); r.range = hi - lo;
			double med = Math.Max(r.medJ, medFloorTicks * tick);
			if (mx > 5 * med) r.verdict = EKdeVerdict.Bimodal;
			else if (mx < 2 * med + 1e-9 && r.range < 0.15 * r.sigma) r.verdict = EKdeVerdict.Stable;
			else r.verdict = EKdeVerdict.Flat;
			return r;
		}

		private static double argmaxLowest(double[] f, double[] prices)
		{
			int b = 0;
			for (int i = 1; i < f.Length; i++) if (f[i] > f[b]) b = i;
			return prices[b];
		}

		/// <summary>
		/// Held-out weighted log-likelihood per bandwidth, summed over folds. Bars with volume are split into `folds` folds (Contiguous
		/// chunks in time, or Interleaved i mod folds); for each fold the row weights are rebuilt from the other bars (train) and the
		/// fold's own bars (test); LL += sum_test w_r ln f_train(x_r), f_train = sum_i v_i phi((x - x_i) / h) / (h sum v) with the full
		/// (untruncated) Gaussian in log-sum-exp form, so a held-out row far from the training mass stays finite. NaN when a fold is empty.
		/// </summary>
		public static double[] CvLogLik(IList<RingBar> bars, double tick, int distribution, double[] hs, int folds, ECvFolding folding)
		{
			var ll = new double[hs.Length];
			var use = new List<int>();
			for (int i = 0; i < bars.Count; i++) if (bars[i].v > 0 && bars[i].l > 0 && bars[i].h >= bars[i].l) use.Add(i);
			if (folds < 2 || use.Count < folds) { for (int k = 0; k < ll.Length; k++) ll[k] = double.NaN; return ll; }
			var foldOf = new int[use.Count];
			for (int i = 0; i < use.Count; i++) foldOf[i] = folding == ECvFolding.Interleaved ? i % folds : (int)((long)i * folds / use.Count);
			for (int f = 0; f < folds; f++)
			{
				var train = new SortedDictionary<long, double>(); var test = new SortedDictionary<long, double>();
				for (int i = 0; i < use.Count; i++)
				{
					SortedDictionary<long, double> dst = foldOf[i] == f ? test : train;
					BarRows(bars[use[i]], tick, distribution, (k, x) => { double c; dst.TryGetValue(k, out c); dst[k] = c + x; });
				}
				if (train.Count == 0 || test.Count == 0) { for (int k = 0; k < ll.Length; k++) ll[k] = double.NaN; return ll; }
				var tk = new long[train.Count]; var tv = new double[train.Count]; double tsum = 0; int t = 0;
				foreach (var kv in train) { tk[t] = kv.Key; tv[t] = kv.Value; tsum += kv.Value; t++; }
				var lnv = new double[tv.Length];
				for (int i = 0; i < tv.Length; i++) lnv[i] = Math.Log(tv[i]);
				for (int k = 0; k < hs.Length; k++)
				{
					double h = hs[k], s = 0;
					foreach (var kv in test)
					{
						double mx = double.NegativeInfinity;
						for (int i = 0; i < tk.Length; i++) { double z = (kv.Key - tk[i]) * tick / h; double e = lnv[i] - 0.5 * z * z; if (e > mx) mx = e; }
						double acc = 0;
						for (int i = 0; i < tk.Length; i++) { double z = (kv.Key - tk[i]) * tick / h; acc += Math.Exp(lnv[i] - 0.5 * z * z - mx); }
						double lnf = mx + Math.Log(acc) - Math.Log(tsum) - Math.Log(h) - 0.5 * Math.Log(2 * Math.PI);
						s += kv.Value * lnf;
					}
					ll[k] += s;
				}
			}
			return ll;
		}

		// ---------------- VP-6 (c) V/T urgency prices ----------------

		/// <summary>
		/// A2-34: V(p) = row volume (distribution as VP-1), T(p) = number of bars with L &lt;= p &lt;= H, TPO POC = argmax T (ties to the
		/// lowest price), volume POC = ProfileMath.PocIndex. Output the top `top` V/T rows (ties to the lower price) that are &gt;= 0.5 x atr30
		/// from both POCs. atr30 = ATR(14, 30m) from AggregateBars (a statistic). Empty when atr30 is not positive.
		/// </summary>
		public static VtResult VtPrices(IList<RingBar> bars, double tick, int distribution, double atr30, int top = 3)
		{
			var r = new VtResult { atr = atr30 };
			double[] p, v;
			if (bars == null || bars.Count == 0 || !Histogram(bars, null, tick, distribution, out p, out v)) return r;
			long lo = _ProfileMath.Key(p[0], tick);
			var T = new int[p.Length];
			foreach (RingBar b in bars)
			{
				if (!(b.l > 0) || b.h < b.l) continue;
				long a = Math.Max(0, _ProfileMath.Key(b.l, tick) - lo), z = Math.Min(p.Length - 1, _ProfileMath.Key(b.h, tick) - lo);
				for (long k = a; k <= z; k++) T[k]++;
			}
			r.volPoc = p[_ProfileMath.PocIndex(v)];
			int tp = 0;
			for (int i = 1; i < T.Length; i++) if (T[i] > T[tp]) tp = i;
			r.tpoPoc = p[tp];
			if (!(atr30 > 0)) return r;
			var cand = new List<int>();
			for (int i = 0; i < p.Length; i++)
				if (T[i] > 0 && v[i] > 0 && Math.Abs(p[i] - r.volPoc) >= 0.5 * atr30 && Math.Abs(p[i] - r.tpoPoc) >= 0.5 * atr30) cand.Add(i);
			cand.Sort((a, b) => { int c = (v[b] / T[b]).CompareTo(v[a] / T[a]); return c != 0 ? c : a.CompareTo(b); });
			for (int i = 0; i < cand.Count && i < top; i++) { r.prices.Add(p[cand[i]]); r.ratios.Add(v[cand[i]] / T[cand[i]]); }
			return r;
		}

		// ---------------- LevelRef emitters ----------------

		public static List<LevelRef> BootstrapLevels(BootstrapResult b, DateTime created, DateTime t0, DateTime t1, int stream, string tag)
		{
			var res = new List<LevelRef>();
			if (b == null || double.IsNaN(b.medianPoc)) return res;
			res.Add(LevelRef.Zone(ELevelKind.BootstrapPocZoneLo, b.zoneLo, b.zoneLo, b.zoneHi, created, t0, t1, stream, tag, b.stability));
			res.Add(LevelRef.Zone(ELevelKind.BootstrapPocZoneHi, b.zoneHi, b.zoneLo, b.zoneHi, created, t0, t1, stream, tag, b.stability));
			return res;
		}

		public static List<LevelRef> VtLevels(VtResult vt, DateTime created, DateTime t0, DateTime t1, int stream, string tag)
		{
			var res = new List<LevelRef>();
			if (vt == null) return res;
			for (int i = 0; i < vt.prices.Count; i++) res.Add(LevelRef.Line(ELevelKind.VtPrice, vt.prices[i], created, t0, t1, stream, tag, vt.ratios[i]));
			return res;
		}
	}

	/// <summary>Per-trading-day value store for "use yesterday's value today" (ruling 2: the prior day's h_cv for KL on KDE densities).</summary>
	public sealed class _PriorDayValue
	{
		private readonly SortedList<DateTime, double> m_v = new SortedList<DateTime, double>();
		public int keep = 30;
		public void Set(DateTime day, double x)
		{
			if (double.IsNaN(x)) return;
			m_v[day.Date] = x;
			while (m_v.Count > keep) m_v.RemoveAt(0);
		}
		/// <summary>Value of the newest day strictly before `day` (no lookahead), NaN when none.</summary>
		public double Before(DateTime day)
		{
			for (int i = m_v.Count - 1; i >= 0; i--) if (m_v.Keys[i] < day.Date) return m_v.Values[i];
			return double.NaN;
		}
	}

	/// <summary>
	/// A2-29 / A2-30 same-clock-slot percentile rings. One instance per tracked metric (e.g. plain H_norm and H_MM separately).
	/// Only the configured decision slots are stored (review L1). Percentile(slot, x) uses strictly earlier sessions: push the value
	/// at the slot's decision time AFTER it has been used. NaN below minFill x lookback samples. One value per (slot, day).
	/// </summary>
	public sealed class _EntropyTracker
	{
		public readonly int lookback; public readonly double minFill;
		private readonly Dictionary<int, List<double>> m_vals = new Dictionary<int, List<double>>();
		private readonly Dictionary<int, DateTime> m_lastDay = new Dictionary<int, DateTime>();

		public _EntropyTracker(int lookback, double minFill, IEnumerable<int> slots)
		{
			this.lookback = Math.Max(1, lookback); this.minFill = minFill;
			if (slots != null) foreach (int s in slots) { m_vals[s] = new List<double>(); m_lastDay[s] = DateTime.MinValue; }
		}

		public bool HasSlot(int slot) { return m_vals.ContainsKey(slot); }
		public int Count(int slot) { List<double> l; return m_vals.TryGetValue(slot, out l) ? l.Count : 0; }

		/// <summary>Store the session's value for a slot (NaN, unconfigured slots and a second push on the same day are ignored).</summary>
		public bool Push(int slot, DateTime day, double x)
		{
			List<double> l;
			if (double.IsNaN(x) || double.IsInfinity(x) || !m_vals.TryGetValue(slot, out l)) return false;
			if (m_lastDay[slot] >= day.Date) return false;
			m_lastDay[slot] = day.Date;
			l.Add(x);
			if (l.Count > lookback) l.RemoveAt(0);
			return true;
		}

		/// <summary>Mid-rank percentile of x against the stored (strictly earlier) sessions; NaN below minFill x lookback samples.</summary>
		public double Percentile(int slot, double x)
		{
			List<double> l;
			if (!m_vals.TryGetValue(slot, out l) || l.Count < minFill * lookback - 1e-9) return double.NaN;
			return _ProfileStats.MidRank(l, x);
		}

		/// <summary>Slot key: minutes since midnight of the decision time (e.g. 11:00 = 660).</summary>
		public static int Slot(DateTime t) { return t.Hour * 60 + t.Minute; }
	}
	// </profile-stats>
}
