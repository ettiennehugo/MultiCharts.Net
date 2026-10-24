using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// M-DVP (specs/m-dvp-spec.md rev 3): detrended volume-profile engine after Keller B5 App E (buildTrend / evalTrend /
	// evalLeverage / trendSE). PR-D1 = the fit core: DvpConfig (Validate/Canon), DvpFit (Forsythe recurrence, sigma,
	// SSE fallback, leverage, BIC, degree modes, degree-1 shadow, cone) and the empty DvpFrozenDegree table. PR-D2 = DvpManifold
	// (rows, tick floor, VA, entropy/concentration, nodes) and DvpRails (rails, residual order, LevelRef/tags). Not wired into
	// _CustomSignal yet (PR-D3). Everything between <dvp-core> markers is MC-free; tests/Dvp extracts it with awk (DV-0.3).
	// ============================================================================================

	// <dvp-core>
	public enum EDvpSrc { HL2, Close }
	public enum EDvpDegMode { DevCoverageFrozen, Fixed1, Calibrate }
	public enum EDvpConeMode { Prediction, Confidence }
	public enum EDvpFibAnchor { MostRecentPivot, ExtremeInLookback }

	/// <summary>DV-1.10 fixed constants. Not config fields; they are hashed through DvpConfig.Canon().</summary>
	public static class DvpConst
	{
		public const string ConfMode = "FamilyCount";
		public const int MinFamilies = 3;
		public const double ReportWeightVa = 1.00, ReportWeightPoc = 0.77;   // report-only (DV-1.3)
		public const int RowFloorTicks = 1;
		public const string RollAdjust = "None";
		public const int MinBars = 12;
		public const int AtrLen = 20;      // Wilder, Gate0Atr
		public const int HoldBars = 40;    // MFE/MAE horizon
		public const int MaxDegree = 3;    // DV-1.2
		public const double SseEps = 1e-12;   // DV-3.4 relative assert
		public const double BreakdownRel = 1e-13;   // N_k <= BreakdownRel * N_0 counts as N_k <= 0 (floating-point zero)
	}

	/// <summary>DV-0.2 / §2. Set in a derived strategy's ctor or Create(); no [Input]s.</summary>
	public sealed class DvpConfig
	{
		public bool enabled = false;
		public int[] streams = { 1 };
		public EDvpSrc src = EDvpSrc.HL2;
		public int winLen = 240;
		public EDvpDegMode degMode = EDvpDegMode.DevCoverageFrozen;
		public int degMax = 3;
		public bool weightVol = false;
		public double minR2 = 0.35;
		public int manRows = 64;
		public double manSpan = 3;
		public double vaPct = 70;          // percent; passed to ValueAreaExpansion as vaPct / 100 (DV-4.4)
		public double nodeProm = 0.12;
		public int maxNodes = 3;
		public double[] sigmaList = { 1, 2, 3 };
		public int projBars = 60;
		public EDvpConeMode coneMode = EDvpConeMode.Prediction;
		public double coneMult = 2;
		public bool useFib = true;
		public int fibRanges = 3;
		public int fibPivot = 20;
		public int fibLookMult = 8;
		public EDvpFibAnchor fibAnchor = EDvpFibAnchor.MostRecentPivot;
		public double fibMerge = 0.40;
		public double fibNear = 14;
		public double confTol = 0.30;
		public bool splitFlow = false;
		public int refitEvery = 1;
		public bool journalFits = true;
		public bool journalRails = true;

		public double VaFraction { get { return vaPct / 100.0; } }

		static bool In(double x, double lo, double hi) { return x >= lo && x <= hi; }   // false for NaN

		/// <summary>DV-2.1: null when valid, else the list of problems. Data-stream existence (requireDataStream) is checked by the host (PR-D3).</summary>
		public List<string> Validate()
		{
			var e = new List<string>();
			if (streams == null || streams.Length == 0) e.Add("streams: at least one stream");
			else
				for (int i = 0; i < streams.Length; i++)
				{
					if (streams[i] < 1) e.Add("streams: " + streams[i] + " < 1");
					for (int j = 0; j < i; j++) if (streams[j] == streams[i]) { e.Add("streams: duplicate " + streams[i]); break; }
				}
			if (!In(winLen, 30, 1000)) e.Add("winLen " + winLen + " not in 30..1000");
			if (winLen < DvpConst.MinBars) e.Add("winLen " + winLen + " < minBars " + DvpConst.MinBars);
			if (!In(degMax, 1, DvpConst.MaxDegree)) e.Add("degMax " + degMax + " not in 1..3");
			if (!In(minR2, 0, 1)) e.Add("minR2 not in 0..1");
			if (!In(manRows, 8, 256)) e.Add("manRows " + manRows + " not in 8..256");
			if (!In(manSpan, 1, 6)) e.Add("manSpan not in 1..6");
			if (!In(vaPct, 50, 95)) e.Add("vaPct " + vaPct.ToString(CultureInfo.InvariantCulture) + " not in 50..95 (percent)");
			if (!In(nodeProm, 0.01, 0.5)) e.Add("nodeProm not in 0.01..0.5");
			if (!In(maxNodes, 0, 10)) e.Add("maxNodes not in 0..10");
			if (sigmaList == null || sigmaList.Length == 0) e.Add("sigmaList: at least one k");
			else foreach (double k in sigmaList) if (!(k > 0) || double.IsInfinity(k)) e.Add("sigmaList: k must be > 0");
			if (!In(projBars, 1, 500)) e.Add("projBars not in 1..500");
			if (!(coneMult > 0) || double.IsInfinity(coneMult)) e.Add("coneMult must be > 0");
			if (!In(fibRanges, 1, 4)) e.Add("fibRanges not in 1..4");
			if (fibPivot < 2) e.Add("fibPivot < 2");
			if (fibLookMult < 1) e.Add("fibLookMult < 1");
			if (!(fibMerge >= 0) || double.IsInfinity(fibMerge)) e.Add("fibMerge must be >= 0 ATR");
			if (!(fibNear > 0) || double.IsInfinity(fibNear)) e.Add("fibNear must be > 0 ATR");
			if (!(confTol > 0) || double.IsInfinity(confTol)) e.Add("confTol must be > 0 ATR");
			if (refitEvery < 1) e.Add("refitEvery < 1");
			return e.Count == 0 ? null : e;
		}

		static string R(double x) { return x.ToString("R", CultureInfo.InvariantCulture); }

		/// <summary>DV-0.2: every field and DV-1.10 constant in a fixed order, plus the DvpFrozenDegree table (DV-2.2). Feeds configHash.</summary>
		public string Canon()
		{
			var sb = new StringBuilder(512);
			sb.Append("DvpConfig{enabled=").Append(enabled ? 1 : 0);
			sb.Append(";streams=");
			if (streams != null) for (int i = 0; i < streams.Length; i++) { if (i > 0) sb.Append(','); sb.Append(streams[i].ToString(CultureInfo.InvariantCulture)); }
			sb.Append(";src=").Append(src).Append(";winLen=").Append(winLen).Append(";degMode=").Append(degMode).Append(";degMax=").Append(degMax);
			sb.Append(";weightVol=").Append(weightVol ? 1 : 0).Append(";minR2=").Append(R(minR2)).Append(";manRows=").Append(manRows);
			sb.Append(";manSpan=").Append(R(manSpan)).Append(";vaPct=").Append(R(vaPct)).Append(";nodeProm=").Append(R(nodeProm)).Append(";maxNodes=").Append(maxNodes);
			sb.Append(";sigmaList=");
			if (sigmaList != null) for (int i = 0; i < sigmaList.Length; i++) { if (i > 0) sb.Append(','); sb.Append(R(sigmaList[i])); }
			sb.Append(";projBars=").Append(projBars).Append(";coneMode=").Append(coneMode).Append(";coneMult=").Append(R(coneMult));
			sb.Append(";useFib=").Append(useFib ? 1 : 0).Append(";fibRanges=").Append(fibRanges).Append(";fibPivot=").Append(fibPivot);
			sb.Append(";fibLookMult=").Append(fibLookMult).Append(";fibAnchor=").Append(fibAnchor).Append(";fibMerge=").Append(R(fibMerge));
			sb.Append(";fibNear=").Append(R(fibNear)).Append(";confTol=").Append(R(confTol)).Append(";splitFlow=").Append(splitFlow ? 1 : 0);
			sb.Append(";refitEvery=").Append(refitEvery).Append(";journalFits=").Append(journalFits ? 1 : 0).Append(";journalRails=").Append(journalRails ? 1 : 0);
			sb.Append(";const{confMode=").Append(DvpConst.ConfMode).Append(";minFamilies=").Append(DvpConst.MinFamilies);
			sb.Append(";reportWeights=VA:").Append(R(DvpConst.ReportWeightVa)).Append(",POC:").Append(R(DvpConst.ReportWeightPoc));
			sb.Append(";rowFloor=").Append(DvpConst.RowFloorTicks).Append("tick;rollAdjust=").Append(DvpConst.RollAdjust);
			sb.Append(";minBars=").Append(DvpConst.MinBars).Append(";ATR_LEN=").Append(DvpConst.AtrLen).Append(";HOLD_BARS=").Append(DvpConst.HoldBars).Append('}');
			sb.Append(";frozen=").Append(DvpFrozenDegree.Canon(DvpFrozenDegree.Table)).Append('}');
			return sb.ToString();
		}
	}

	/// <summary>DV-2.2: frozen degree d* per (symbol root, stream, winLen), set by KD-08's dev-only coverage-match study. Empty until then.</summary>
	public static class DvpFrozenDegree
	{
		public struct Entry
		{
			public string root; public int stream, winLen, degree;
			public Entry(string root, int stream, int winLen, int degree) { this.root = root; this.stream = stream; this.winLen = winLen; this.degree = degree; }
		}

		public static readonly Entry[] Table = new Entry[0];

		public static bool TryGet(string root, int stream, int winLen, out int degree) { return TryGet(Table, root, stream, winLen, out degree); }

		public static bool TryGet(Entry[] table, string root, int stream, int winLen, out int degree)
		{
			for (int i = 0; i < table.Length; i++)
				if (table[i].stream == stream && table[i].winLen == winLen && string.Equals(table[i].root, root, StringComparison.Ordinal)) { degree = table[i].degree; return true; }
			degree = 0; return false;
		}

		public static string Canon(Entry[] table)
		{
			var sb = new StringBuilder("[");
			for (int i = 0; i < table.Length; i++)
			{
				if (i > 0) sb.Append(',');
				sb.Append(table[i].root).Append(':').Append(table[i].stream).Append(':').Append(table[i].winLen).Append('=').Append(table[i].degree);
			}
			return sb.Append(']').ToString();
		}
	}

	/// <summary>DV-3.6 degree rules.</summary>
	public static class DvpDegree
	{
		/// <summary>dCap = min(degMax, max(1, floor(n/8))); 0 when n &lt; minBars (no fit).</summary>
		public static int Cap(int n, int degMax)
		{
			if (n < DvpConst.MinBars) return 0;
			return Math.Min(Math.Min(degMax, DvpConst.MaxDegree), Math.Max(1, n / 8));
		}

		/// <summary>
		/// Reported degree for a mode. DevCoverageFrozen without a table entry falls back to Calibrate (reported d = 1);
		/// <paramref name="warn"/> is true exactly once per <paramref name="warnedLatch"/> (journal "DVP DEGREE not frozen", Warning).
		/// Returns false when n &lt; minBars (no fit).
		/// </summary>
		public static bool Select(EDvpDegMode mode, int n, int degMax, bool hasFrozen, int dStar, ref bool warnedLatch, out int d, out bool calibrate, out bool warn)
		{
			warn = false; calibrate = false; d = 0;
			int cap = Cap(n, degMax);
			if (cap == 0) return false;
			if (mode == EDvpDegMode.DevCoverageFrozen && hasFrozen) { d = Math.Max(1, Math.Min(dStar, cap)); return true; }
			if (mode == EDvpDegMode.DevCoverageFrozen && !warnedLatch) { warnedLatch = true; warn = true; }
			if (mode != EDvpDegMode.Fixed1) calibrate = true;   // Calibrate, or the DevCoverageFrozen fallback: every d <= dCap is fitted
			d = 1;
			return true;
		}
	}

	/// <summary>
	/// DV-3: Forsythe orthogonal-polynomial fit of a window y[0..n-1] (oldest first; i = n-1 is bar t-1). One pass fits every
	/// degree 0..dTop (nested), so the degree-1 shadow, BIC[] and Calibrate cost nothing extra. All work arrays are sized once
	/// in the constructor (Configure); Fit and every query are allocation-free.
	/// Weights (DV-3.4 VERIFY, App E buildTrend): weightVol uses volume normalised to mean 1 (sum w = n), falling back to w = 1
	/// when the volume sum is not positive; so sigma = sqrt(SSE/(n-d-1)) stays in price units and "sum w in place of n" is n.
	/// </summary>
	public sealed class DvpFit
	{
		public const int MaxDeg = DvpConst.MaxDegree;
		readonly int m_capacity;
		readonly double[] m_u, m_w, m_yt, m_fit, m_pPrev, m_pCur, m_pNext;

		public readonly double[] A = new double[MaxDeg + 1], B = new double[MaxDeg + 1], N = new double[MaxDeg + 1], C = new double[MaxDeg + 1];
		public readonly double[] Sse = new double[MaxDeg + 1], Sigma = new double[MaxDeg + 1], R2 = new double[MaxDeg + 1], Bic = new double[MaxDeg + 1];

		public bool Valid { get; private set; }
		public int n { get; private set; }
		public int DTop { get; private set; }          // highest degree computed (dCap, or the FitRaw request)
		public int MaxValidDeg { get; private set; }   // DTop, or lower after a recurrence breakdown (N_k <= 0)
		public int Deg { get; private set; }           // reported / primary degree (Choose)
		public bool Calibrating { get; private set; }
		public double YBar { get; private set; }
		public double Ctr { get; private set; }
		public double Scl { get; private set; }
		public double SumW { get; private set; }
		public double Swy2 { get; private set; }       // sum w * ytilde^2
		public bool UsedFallback { get; private set; } // DV-3.4: SSE recomputed by the residual pass on this fit (journal DVP SSE-FALLBACK, Debug)
		public bool WeightsFellBack { get; private set; }
		public int FallbackCount { get; private set; } // cumulative, for DVP RUN

		public DvpFit(int capacity)
		{
			if (capacity < DvpConst.MinBars) throw new ArgumentException("DvpFit capacity < minBars");
			m_capacity = capacity;
			m_u = new double[capacity]; m_w = new double[capacity]; m_yt = new double[capacity]; m_fit = new double[capacity];
			m_pPrev = new double[capacity]; m_pCur = new double[capacity]; m_pNext = new double[capacity];
		}

		public int Capacity { get { return m_capacity; } }

		/// <summary>Fit with dTop = dCap (DV-3.6). Returns false (Valid = false) when n &lt; minBars.</summary>
		public bool Fit(double[] y, double[] v, int n, int degMax, bool weightVol)
		{
			int cap = DvpDegree.Cap(n, degMax);
			if (cap == 0) { Valid = false; this.n = n; return false; }
			return FitRaw(y, v, n, cap, weightVol);
		}

		/// <summary>Fit degrees 0..dTop without the dCap rule (reference parity tests use d up to 3 at n = 12).</summary>
		public bool FitRaw(double[] y, double[] v, int n, int dTop, bool weightVol)
		{
			Valid = false; UsedFallback = false; WeightsFellBack = false; this.n = n;
			if (n < DvpConst.MinBars || n > m_capacity || dTop < 0 || dTop > MaxDeg || dTop > n - 2) return false;
			for (int k = 0; k <= MaxDeg; k++) { A[k] = B[k] = N[k] = C[k] = 0; Sse[k] = Sigma[k] = R2[k] = Bic[k] = double.NaN; }

			// weights (App E: volume normalised to mean 1; w = 1 when the sum is not positive)
			double sv = 0;
			if (weightVol)
			{
				for (int i = 0; i < n; i++) { double x = v[i]; if (x > 0 && !double.IsInfinity(x)) sv += x; }
				if (!(sv > 0) || double.IsInfinity(sv)) { WeightsFellBack = true; weightVol = false; }
			}
			double sc = weightVol ? n / sv : 0;
			for (int i = 0; i < n; i++) { double x = weightVol ? v[i] : 1; m_w[i] = weightVol ? ((x > 0 && !double.IsInfinity(x)) ? x * sc : 0) : 1; }

			// DV-3.1 coordinates, DV-3.2 de-mean
			Ctr = (n - 1) / 2.0; Scl = Math.Max((n - 1) / 2.0, 1e-9);
			double sw = 0, swy = 0;
			for (int i = 0; i < n; i++) { m_u[i] = (i - Ctr) / Scl; sw += m_w[i]; swy += m_w[i] * y[i]; }
			SumW = sw; YBar = swy / sw;
			double s2 = 0;
			for (int i = 0; i < n; i++) { double t = y[i] - YBar; m_yt[i] = t; s2 += m_w[i] * t * t; m_fit[i] = 0; m_pPrev[i] = 0; m_pCur[i] = 1; }
			Swy2 = s2;

			// DV-3.3 recurrence, DV-3.4 SSE per degree
			double[] pPrev = m_pPrev, pCur = m_pCur, pNext = m_pNext;
			double cum = 0; MaxValidDeg = dTop;
			double lnN = Math.Log(n);
			for (int k = 0; k <= dTop; k++)
			{
				double nk = 0, ak = 0, ck = 0;
				for (int i = 0; i < n; i++) { double wp = m_w[i] * pCur[i]; nk += wp * pCur[i]; ak += wp * pCur[i] * m_u[i]; ck += wp * m_yt[i]; }
				if (k > 0 && !(nk > DvpConst.BreakdownRel * N[0]))
				{
					// recurrence breakdown: degrees >= k are not identifiable; they repeat degree k-1 with SSE from the residual pass
					MaxValidDeg = k - 1; UsedFallback = true;
					double sse = ResidualSse(n);
					for (int kk = k; kk <= dTop; kk++) { Sse[kk] = sse; Finish(kk, n, lnN); }
					Sse[k - 1] = sse; Finish(k - 1, n, lnN);
					break;
				}
				N[k] = nk; A[k] = ak / nk; B[k] = k == 0 ? 0 : nk / N[k - 1]; C[k] = ck / nk;
				double c = C[k];
				for (int i = 0; i < n; i++) m_fit[i] += c * pCur[i];
				cum += c * c * nk;
				double s = s2 - cum;
				if (s < -DvpConst.SseEps * s2) { s = ResidualSse(n); UsedFallback = true; }
				Sse[k] = s < 0 ? 0 : s;
				Finish(k, n, lnN);
				if (k < dTop)
				{
					double a = A[k], b = B[k];
					for (int i = 0; i < n; i++) pNext[i] = (m_u[i] - a) * pCur[i] - b * pPrev[i];
					double[] t = pPrev; pPrev = pCur; pCur = pNext; pNext = t;
				}
			}
			if (UsedFallback) FallbackCount++;
			DTop = dTop; Valid = true; Deg = Math.Min(1, MaxValidDeg); Calibrating = false;
			return true;
		}

		double ResidualSse(int n)
		{
			double s = 0;
			for (int i = 0; i < n; i++) { double e = m_yt[i] - m_fit[i]; s += m_w[i] * e * e; }
			return s;
		}

		void Finish(int k, int n, double lnN)
		{
			double s = Sse[k];
			Sigma[k] = Math.Sqrt(s / (n - k - 1));
			R2[k] = Swy2 > 0 ? 1 - s / Swy2 : 0;   // flat window: R2 = 0 (no variance to explain)
			Bic[k] = s > 0 ? n * Math.Log(s / n) + (k + 2) * lnN : double.NegativeInfinity;   // report-only (DV-3.5)
		}

		/// <summary>Set the primary degree (clamped to the valid range). calibrate = all d &lt;= dCap are journaled side by side.</summary>
		public void Choose(int d, bool calibrate)
		{
			if (d < 0) d = 0;
			Deg = Math.Min(d, MaxValidDeg); Calibrating = calibrate;
		}

		int Dc(int d) { return d < 0 ? 0 : (d > MaxValidDeg ? MaxValidDeg : d); }

		/// <summary>u of window index i (i = n-1 is bar t-1; i = n-1+h is h bars ahead).</summary>
		public double U(double i) { return (i - Ctr) / Scl; }
		/// <summary>u of the bar h closed bars after the window: 1 + 2h/(n-1). h = 1 is bar t (where rails are evaluated).</summary>
		public double UAhead(int h) { return 1 + 2.0 * h / (n - 1); }
		public double UT { get { return UAhead(1); } }

		/// <summary>Evaluates yhat(u), the leverage h(u) = sum p_k^2/N_k and dyhat/du at degree d, by the recurrence (no allocation).</summary>
		public double Eval(double u, int d, out double lev, out double dYdu)
		{
			d = Dc(d);
			double pPrev = 0, p = 1, dPrev = 0, dp = 0;
			double s = C[0], h = 1 / N[0], ds = 0;
			for (int k = 0; k < d; k++)
			{
				double pn = (u - A[k]) * p - B[k] * pPrev;
				double dn = p + (u - A[k]) * dp - B[k] * dPrev;
				pPrev = p; p = pn; dPrev = dp; dp = dn;
				s += C[k + 1] * p; ds += C[k + 1] * dp; h += p * p / N[k + 1];
			}
			lev = h; dYdu = ds;
			return YBar + s;
		}

		public double Yhat(double u, int d) { double h, g; return Eval(u, d, out h, out g); }
		public double Yhat(double u) { return Yhat(u, Deg); }
		public double Leverage(double u, int d) { double h, g; Eval(u, d, out h, out g); return h; }
		public double Leverage(double u) { return Leverage(u, Deg); }
		public double SigmaAt(int d) { return Sigma[Dc(d)]; }
		public double SigmaD { get { return Sigma[Deg]; } }

		/// <summary>DV-3.9 slopeEnd = dyhat/di at i = n-1 = (dyhat/du)(2/(n-1)); price per bar.</summary>
		public double SlopeEnd(int d) { double h, g; Eval(1, d, out h, out g); return g * 2.0 / (n - 1); }

		/// <summary>DV-3.5 diagnostic cone at h bars ahead: yhat(u) -+ coneMult*sigma*sqrt(1+h(u)) (Prediction) or sqrt(h(u)) (Confidence). Never a level (DV-1.5).</summary>
		public void Cone(int hAhead, double coneMult, EDvpConeMode mode, out double lo, out double hi)
		{
			double lev, g;
			double y = Eval(UAhead(hAhead), Deg, out lev, out g);
			double half = coneMult * Sigma[Deg] * Math.Sqrt(Math.Max((mode == EDvpConeMode.Prediction ? 1 : 0) + lev, 0));
			lo = y - half; hi = y + half;
		}

		/// <summary>Residual of window bar i at degree d (y_i - yhat_i, computed on the de-meaned series).</summary>
		public double Resid(int i, int d) { double h, g; return m_yt[i] - (Eval(m_u[i], d, out h, out g) - YBar); }

		/// <summary>In-sample coverage: share of window bars with |y - yhat| &lt;= k sigma at degree d (DV-3.6 cov_in).</summary>
		public double CovIn(int d, double k)
		{
			d = Dc(d);
			double band = k * Sigma[d]; int c = 0;
			for (int i = 0; i < n; i++) if (Math.Abs(Resid(i, d)) <= band) c++;
			return (double)c / n;
		}

		/// <summary>
		/// Forward coverage (DV-3.6 cov_fwd): share of yFwd[0..H-1] (the H closed bars after the window) with
		/// |y - yhat(u)| &lt;= k sigma sqrt(1 + h(u)); withLeverage = false gives the flat-band variant (report-only).
		/// </summary>
		public double CovFwd(double[] yFwd, int H, int d, double k, bool withLeverage)
		{
			d = Dc(d);
			if (H <= 0) return double.NaN;
			int c = 0;
			for (int j = 0; j < H; j++)
			{
				double lev, g;
				double yh = Eval(UAhead(j + 1), d, out lev, out g);
				double band = k * Sigma[d] * (withLeverage ? Math.Sqrt(1 + lev) : 1);
				if (Math.Abs(yFwd[j] - yh) <= band) c++;
			}
			return (double)c / H;
		}

		/// <summary>DV-3.9 Durbin-Watson and skew of the (unweighted) residuals at degree d.</summary>
		public void ResidualStats(int d, out double dw, out double skew)
		{
			d = Dc(d);
			double num = 0, den = 0, prev = 0, m1 = 0;
			for (int i = 0; i < n; i++)
			{
				double e = Resid(i, d);
				if (i > 0) num += (e - prev) * (e - prev);
				den += e * e; m1 += e; prev = e;
			}
			m1 /= n;
			double m2 = 0, m3 = 0;
			for (int i = 0; i < n; i++) { double e = Resid(i, d) - m1; m2 += e * e; m3 += e * e * e; }
			m2 /= n; m3 /= n;
			dw = den > 0 ? num / den : double.NaN;
			skew = m2 > 0 ? m3 / Math.Pow(m2, 1.5) : double.NaN;
		}
	}

	// ---------------------------------------------------------------------------------------------------------------
	// PR-D2 (DV-4): residual manifold, POC/VA, shape stats, nodes and rails. No allocation on the refit path: every
	// buffer (exact-length row arrays for each possible manRowsEff, node slots, candidate and rail arrays) is allocated
	// once in the constructor (DV-0.4, DV-13.2). LevelRef materialisation (tag strings) is a separate, query-side step.
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>One manifold node (DV-4.7): row index, z of the row centre, topographic prominence, side of POC (+1 above, -1 below).</summary>
	public struct DvpNode
	{
		public int row; public double z, prom; public int side;
	}

	/// <summary>One rail of a snapshot (DV-4.8): kind, residual level z, price P_t(z) = yhat_{t-1}(u_t) + z sigma_{t-1}.</summary>
	public struct DvpRail
	{
		public ELevelKind kind; public double z, price;
	}

	public sealed class DvpManifold
	{
		readonly int m_manRows;
		readonly double[][] m_rowsBy, m_upBy, m_downBy;   // exact-length row arrays, index = manRowsEff (1..manRows); all preallocated
		readonly List<double[]> m_hvnList, m_lvnList;      // caller-owned lists for _ProfileMath.Prominence
		readonly double[][] m_hvnSlots, m_lvnSlots;        // slot pool refilled into the lists before every call
		readonly DvpNode[] m_cand;                         // node-selection scratch
		readonly bool[] m_taken;
		double m_selSpan, m_selWz;
		public readonly DvpNode[] Hvn, Lvn;                // selected nodes, ascending z; counts HvnCount / LvnCount
		public int HvnCount { get; private set; }
		public int LvnCount { get; private set; }

		public bool Valid { get; private set; }            // a profile exists (sigma > 0 and some in-span volume)
		public int RowsEff { get; private set; }           // manRowsEff (DV-4.1), journaled
		public double Span { get; private set; }
		public double Wz { get; private set; }             // row height in sigma units
		public double Sigma { get; private set; }
		public double TailVol { get; private set; }
		public double TotalVol { get; private set; }       // sum of v_i that entered the deposit (v > 0 and finite)
		public double UpTail { get; private set; }
		public double DownTail { get; private set; }
		public int Poc { get; private set; }
		public int VaLo { get; private set; }
		public int VaHi { get; private set; }
		public double PocZ { get; private set; }
		public double VaLoZ { get; private set; }
		public double VaHiZ { get; private set; }
		public double Entropy { get; private set; }
		public double Concentration { get; private set; }
		public double ConcentrationOcc { get; private set; }
		public double InSampleCov2 { get; private set; }

		public DvpManifold(int manRows, bool splitFlow)
		{
			if (manRows < 1) throw new ArgumentOutOfRangeException("manRows");
			m_manRows = manRows;
			m_rowsBy = new double[manRows + 1][];
			if (splitFlow) { m_upBy = new double[manRows + 1][]; m_downBy = new double[manRows + 1][]; }
			for (int r = 1; r <= manRows; r++)
			{
				m_rowsBy[r] = new double[r];
				if (splitFlow) { m_upBy[r] = new double[r]; m_downBy[r] = new double[r]; }
			}
			int slots = manRows + 2;   // Prominence finds at most ceil(R/2) HVNs and one fewer LVN
			m_hvnList = new List<double[]>(slots); m_lvnList = new List<double[]>(slots);
			m_hvnSlots = new double[slots][]; m_lvnSlots = new double[slots][];
			for (int i = 0; i < slots; i++) { m_hvnSlots[i] = new double[2]; m_lvnSlots[i] = new double[2]; }
			m_cand = new DvpNode[slots]; m_taken = new bool[slots];
			Hvn = new DvpNode[2 * 10]; Lvn = new DvpNode[2 * 10];   // maxNodes <= 10 per side (DvpConfig.Validate)
			Clear();
		}

		public int ManRows { get { return m_manRows; } }
		/// <summary>The in-span rows of the last build (exact length RowsEff), or null.</summary>
		public double[] Rows { get { return RowsEff > 0 ? m_rowsBy[RowsEff] : null; } }
		public double[] UpRows { get { return m_upBy != null && RowsEff > 0 ? m_upBy[RowsEff] : null; } }
		public double[] DownRows { get { return m_downBy != null && RowsEff > 0 ? m_downBy[RowsEff] : null; } }
		/// <summary>Centre z of row r.</summary>
		public double RowZ(int r) { return -Span + (r + 0.5) * Wz; }

		void Clear()
		{
			Valid = false; RowsEff = 0; Sigma = Wz = Span = 0; TailVol = TotalVol = UpTail = DownTail = 0;
			Poc = VaLo = VaHi = -1; PocZ = VaLoZ = VaHiZ = Entropy = Concentration = ConcentrationOcc = InSampleCov2 = double.NaN;
			HvnCount = LvnCount = 0;
		}

		/// <summary>DV-4.1 tick floor: manRowsEff = max(1, floor(2 span sigma / tick)) when a nominal row is under one tick, else manRows.</summary>
		public static int RowsEffFor(int manRows, double span, double sigma, double tick)
		{
			double wz = 2.0 * span / manRows;
			if (tick > 0 && wz * sigma < tick)
			{
				double r = Math.Floor(2.0 * span * sigma / tick);
				return r < 1 ? 1 : (r > manRows ? manRows : (int)r);
			}
			return manRows;
		}

		/// <summary>
		/// DV-4.2 deposit of volume v over [loZ, hiZ] onto rows[0..R-1] (row r = [-span + r wz, -span + (r+1) wz)), in
		/// proportion to each row's overlap; the remainder (outside +-span) is returned as tail. A zero-range bar puts all of v
		/// into its row (or the tail when it is outside; +span itself is outside, rows are half-open).
		/// </summary>
		public static double Deposit(double[] rows, int R, double span, double wz, double loZ, double hiZ, double v)
		{
			if (!(v > 0) || double.IsInfinity(v)) return 0;
			if (hiZ < loZ) { double t = loZ; loZ = hiZ; hiZ = t; }
			double len = hiZ - loZ;
			if (!(len > 0))
			{
				double fr = Math.Floor((loZ + span) / wz);
				if (fr >= 0 && fr < R) { rows[(int)fr] += v; return 0; }
				return v;
			}
			double a = Math.Max(loZ, -span), b = Math.Min(hiZ, span), inside = 0;
			if (b > a)
			{
				int r0 = (int)Math.Floor((a + span) / wz), r1 = (int)Math.Floor((b + span) / wz);
				if (r0 < 0) r0 = 0; if (r1 > R - 1) r1 = R - 1;
				for (int r = r0; r <= r1; r++)
				{
					double rl = -span + r * wz, rh = rl + wz;
					double ov = Math.Min(b, rh) - Math.Max(a, rl);
					if (ov > 0) { double dep = v * (ov / len); rows[r] += dep; inside += dep; }
				}
			}
			double tail = v - inside;
			return tail > 0 ? tail : 0;
		}

		/// <summary>
		/// Build the manifold of a fitted window (DV-4.1..4.7). fit must be Valid with Choose already called; high/low/vol (and
		/// up/down when splitFlow) are the same n window bars as the fit, i = n-1 = bar t-1. vaFraction is vaPct/100 (a
		/// fraction; DV-4.4). Returns false (no profile) when sigma is not > 0 or no volume falls inside the span.
		/// </summary>
		public bool Build(DvpFit fit, double[] high, double[] low, double[] vol, double[] up, double[] down,
			double manSpan, double tick, double vaFraction, double nodeProm, int maxNodes)
		{
			if (!(vaFraction > 0 && vaFraction <= 1)) throw new ArgumentOutOfRangeException("vaFraction", "DV-4.4: pass vaPct / 100.0");
			Clear();
			if (fit == null || !fit.Valid) return false;
			int n = fit.n, d = fit.Deg;
			double sigma = fit.SigmaD;
			InSampleCov2 = fit.CovIn(d, 2);
			if (!(sigma > 0) || double.IsInfinity(sigma)) return false;
			int R = RowsEffFor(m_manRows, manSpan, sigma, tick);
			double wz = 2.0 * manSpan / R;
			RowsEff = R; Span = manSpan; Wz = wz; Sigma = sigma;
			double[] rows = m_rowsBy[R]; Array.Clear(rows, 0, R);
			bool split = m_upBy != null && up != null && down != null;
			double[] ur = split ? m_upBy[R] : null, dr = split ? m_downBy[R] : null;
			if (split) { Array.Clear(ur, 0, R); Array.Clear(dr, 0, R); }
			double tail = 0, total = 0, ut = 0, dt = 0;
			for (int i = 0; i < n; i++)
			{
				double v = vol[i];
				if (!(v > 0) || double.IsInfinity(v)) continue;
				double c = fit.Yhat(fit.U(i), d), lo = (low[i] - c) / sigma, hi = (high[i] - c) / sigma;
				total += v;
				tail += Deposit(rows, R, manSpan, wz, lo, hi, v);
				if (split) { ut += Deposit(ur, R, manSpan, wz, lo, hi, up[i]); dt += Deposit(dr, R, manSpan, wz, lo, hi, down[i]); }
			}
			TailVol = tail; TotalVol = total; UpTail = ut; DownTail = dt;
			int vlo, vhi, poc;
			if (!_ProfileMath.ValueAreaExpansion(rows, vaFraction, out vlo, out vhi, out poc)) return false;
			Valid = true; Poc = poc; VaLo = vlo; VaHi = vhi;
			PocZ = RowZ(poc); VaLoZ = -manSpan + vlo * wz; VaHiZ = -manSpan + (vhi + 1) * wz;   // outer edges (DV-4.4, Q9)
			double conc, concOcc;
			Shape(rows, poc, out conc, out concOcc);
			Concentration = conc; ConcentrationOcc = concOcc;
			Entropy = _ProfileStats.Entropy(rows, EEntropyRows.TradedRows, 0).Hnorm;
			SelectNodes(rows, poc, nodeProm, maxNodes, manSpan);
			return true;
		}

		/// <summary>DV-4.5: concentration = rows[poc] / (sum / R) over all R rows; concentrationOcc uses occupied rows only.</summary>
		public static void Shape(double[] rows, int poc, out double concentration, out double concentrationOcc)
		{
			concentration = concentrationOcc = double.NaN;
			if (rows == null || poc < 0 || poc >= rows.Length) return;
			double s = 0; int occ = 0;
			for (int i = 0; i < rows.Length; i++) if (rows[i] > 0) { s += rows[i]; occ++; }
			if (!(s > 0)) return;
			concentration = rows[poc] / (s / rows.Length);
			concentrationOcc = rows[poc] / (s / occ);
		}

		/// <summary>
		/// DV-4.6/4.7 on rows (raw, unsmoothed): _ProfileMath.Prominence at nodeProm, then drop the POC row and any HVN within one
		/// row of it, and keep per side of POC the maxNodes HVNs and maxNodes LVNs of highest prominence (ties: nearer to POC,
		/// then lower row). z = row centre for rows spanning [-span, +span]. Results land in Hvn/Lvn sorted by ascending z.
		/// Public for tests; no allocation.
		/// </summary>
		public void SelectNodes(double[] rows, int poc, double nodeProm, int maxNodes, double span)
		{
			HvnCount = LvnCount = 0;
			if (rows == null || rows.Length == 0 || rows.Length >= m_cand.Length) return;
			if (maxNodes > 10) maxNodes = 10;
			m_selSpan = span; m_selWz = 2.0 * span / rows.Length;
			refill(m_hvnList, m_hvnSlots); refill(m_lvnList, m_lvnSlots);
			_ProfileMath.Prominence(rows, nodeProm, m_hvnList, m_lvnList);
			HvnCount = pick(m_hvnList, poc, maxNodes, true, Hvn);
			LvnCount = pick(m_lvnList, poc, maxNodes, false, Lvn);
		}

		static void refill(List<double[]> list, double[][] slots)
		{
			list.Clear();
			for (int i = 0; i < slots.Length; i++) list.Add(slots[i]);   // capacity preset: no allocation; Prominence overwrites and trims
		}

		int pick(List<double[]> found, int poc, int maxNodes, bool hvn, DvpNode[] outp)
		{
			int nc = 0;
			for (int i = 0; i < found.Count && nc < m_cand.Length; i++)
			{
				int r = (int)found[i][0];
				if (r == poc || (hvn && Math.Abs(r - poc) <= 1)) continue;
				m_cand[nc].row = r; m_cand[nc].prom = found[i][1]; m_cand[nc].side = r > poc ? 1 : -1; m_cand[nc].z = -m_selSpan + (r + 0.5) * m_selWz;
				m_taken[nc] = false; nc++;
			}
			int no = 0;
			for (int side = -1; side <= 1; side += 2)
				for (int k = 0; k < maxNodes; k++)
				{
					int best = -1;
					for (int j = 0; j < nc; j++)
					{
						if (m_taken[j] || m_cand[j].side != side) continue;
						if (best < 0 || m_cand[j].prom > m_cand[best].prom
							|| (m_cand[j].prom == m_cand[best].prom && Math.Abs(m_cand[j].row - poc) < Math.Abs(m_cand[best].row - poc))) best = j;
					}
					if (best < 0) break;
					m_taken[best] = true; outp[no++] = m_cand[best];
				}
			for (int i = 1; i < no; i++)   // insertion sort by z (rows are distinct)
			{
				DvpNode x = outp[i]; int j = i - 1;
				while (j >= 0 && outp[j].row > x.row) { outp[j + 1] = outp[j]; j--; }
				outp[j + 1] = x;
			}
			return no;
		}
	}

	/// <summary>
	/// DV-4.8/4.9 rails of one snapshot: P_t(z) = yhat_{t-1}(u_t) + z sigma_{t-1}, sorted by z (residual order). Manifold kinds
	/// (Poc/Vah/Val/Hvn/Lvn) and sigma rails +-k; DvpFib (PR-D4) and DvpConf (PR-D5) are added later. No z = 0 trend rail,
	/// and the cone is never a rail (DV-1.5). Fixed capacity, no allocation in Build.
	/// </summary>
	public sealed class DvpRails
	{
		public readonly DvpRail[] Items;
		public int Count { get; private set; }
		public double YhatT { get; private set; }   // yhat_{t-1}(u_t)
		public double Sigma { get; private set; }
		public int Deg { get; private set; }

		public DvpRails(int maxSigmaRails) { Items = new DvpRail[3 + 2 * 20 + 2 * Math.Max(1, maxSigmaRails)]; Count = 0; Sigma = double.NaN; YhatT = double.NaN; }

		public bool Build(DvpFit fit, DvpManifold man, double[] sigmaList)
		{
			Count = 0; YhatT = Sigma = double.NaN; Deg = -1;
			if (fit == null || !fit.Valid || !(fit.SigmaD > 0)) return false;
			Deg = fit.Deg; YhatT = fit.Yhat(fit.UT, Deg); Sigma = fit.SigmaD;
			if (man != null && man.Valid)
			{
				add(ELevelKind.DvpPoc, man.PocZ); add(ELevelKind.DvpVah, man.VaHiZ); add(ELevelKind.DvpVal, man.VaLoZ);
				for (int i = 0; i < man.HvnCount; i++) add(ELevelKind.DvpHvn, man.Hvn[i].z);
				for (int i = 0; i < man.LvnCount; i++) add(ELevelKind.DvpLvn, man.Lvn[i].z);
			}
			if (sigmaList != null)
				for (int i = 0; i < sigmaList.Length && Count + 2 <= Items.Length; i++) { add(ELevelKind.DvpSigma, sigmaList[i]); add(ELevelKind.DvpSigma, -sigmaList[i]); }
			for (int i = 1; i < Count; i++)   // insertion sort: z, then kind ordinal (deterministic)
			{
				DvpRail x = Items[i]; int j = i - 1;
				while (j >= 0 && (Items[j].z > x.z || (Items[j].z == x.z && (int)Items[j].kind > (int)x.kind))) { Items[j + 1] = Items[j]; j--; }
				Items[j + 1] = x;
			}
			return true;
		}

		void add(ELevelKind kind, double z)
		{
			if (Count >= Items.Length || double.IsNaN(z)) return;
			Items[Count].kind = kind; Items[Count].z = z; Items[Count].price = Price(z); Count++;
		}

		/// <summary>P_t(z) (DV-4.8).</summary>
		public double Price(double z) { return YhatT + z * Sigma; }
		/// <summary>(price - yhat(u_t)) / sigma (DV-10 Z).</summary>
		public double Z(double price) { return Sigma > 0 ? (price - YhatT) / Sigma : double.NaN; }

		/// <summary>DV-10 RailPrice: Poc/Vah/Val ignore z; Sigma/Hvn/Lvn need the rail at z (|dz| &lt;= 1e-9); NaN if absent.</summary>
		public double RailPrice(ELevelKind kind, double z)
		{
			bool unique = kind == ELevelKind.DvpPoc || kind == ELevelKind.DvpVah || kind == ELevelKind.DvpVal;
			for (int i = 0; i < Count; i++)
				if (Items[i].kind == kind && (unique || Math.Abs(Items[i].z - z) <= 1e-9)) return Items[i].price;
			return double.NaN;
		}

		/// <summary>DV-4.9 NextRail: index of the first rail strictly beyond fromZ in direction dir (+1 up, -1 down), or -1.</summary>
		public int NextRail(double fromZ, int dir)
		{
			if (dir > 0) { for (int i = 0; i < Count; i++) if (Items[i].z > fromZ) return i; }
			else if (dir < 0) { for (int i = Count - 1; i >= 0; i--) if (Items[i].z < fromZ) return i; }
			return -1;
		}

		/// <summary>DV-10.2 tag "DVP{ds}:{t-1:yyyyMMddHHmm}:d{deg}:{kind}{z:+0.00}" (invariant culture; a z that rounds to 0 prints +0.00).</summary>
		public static string Tag(int ds, DateTime tPrev, int deg, ELevelKind kind, double z)
		{
			return "DVP" + ds.ToString(CultureInfo.InvariantCulture) + ":" + tPrev.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + ":d"
				+ deg.ToString(CultureInfo.InvariantCulture) + ":" + kind.ToString() + z.ToString("+0.00;-0.00;+0.00", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// DV-10.1 LevelRefs of the rails into a caller-owned list, reusing caller-pooled LevelRef objects (pool grows only when
		/// short). Query-side: tag strings allocate, so the host calls this lazily (PR-D3), never on the refit path. quality =
		/// families is NaN until confluence lands (PR-D5).
		/// </summary>
		public void Publish(List<LevelRef> into, List<LevelRef> pool, int ds, DateTime tPrev, DateTime t0, bool isRollWindow)
		{
			into.Clear();
			for (int i = 0; i < Count; i++)
			{
				LevelRef r;
				if (i < pool.Count) r = pool[i]; else { r = new LevelRef(); pool.Add(r); }
				DvpRail x = Items[i];
				r.kind = x.kind; r.price = r.lo = r.hi = x.price; r.createdTime = tPrev; r.sourceT0 = t0; r.sourceT1 = tPrev; r.dataStream = ds;
				r.tag = Tag(ds, tPrev, Deg, x.kind, x.z); r.quality = double.NaN; r.crossesRoll = false; r.isRollWindow = isRollWindow;
				into.Add(r);
			}
		}
	}
	// </dvp-core>
}
