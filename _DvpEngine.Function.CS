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
			if (fibAnchor != EDvpFibAnchor.MostRecentPivot) e.Add("fibAnchor " + fibAnchor + " not supported (MostRecentPivot only, spec rev 5 §2)");
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
			return fit == null ? Build(null, man, sigmaList, double.NaN) : Build(fit, man, sigmaList, fit.UT);
		}

		/// <summary>PR-D3: rails at an explicit u (between refits, DV-3.8: u_t of the frozen fit = UAhead(1 + bars since the anchor)).</summary>
		public bool Build(DvpFit fit, DvpManifold man, double[] sigmaList, double uT)
		{
			Count = 0; YhatT = Sigma = double.NaN; Deg = -1;
			if (fit == null || !fit.Valid || !(fit.SigmaD > 0)) return false;
			Deg = fit.Deg; YhatT = fit.Yhat(uT, Deg); Sigma = fit.SigmaD;
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
	// ---------------------------------------------------------------------------------------------------------------
	// PR-D3 (DV-0.1, DV-3.7/3.8, DV-7, DV-10, DV-11): per-stream state and the host-facing engine. MC-free: the host feeds
	// each closed bar's RingBar (already added to the stream's _BarRing) and queries lazily. Every buffer is allocated in the
	// DvpStream constructor, which runs only inside the _DvpEngine constructor (the host creates the engine in StartCalc
	// when DvpConfig.enabled, never at load). Nothing here is static except the frozen-degree table.
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>DV-10 snapshot: the as-of t-1 fit for bar t (window ends at endBar). Reused per stream; valid until the next closed bar.</summary>
	public sealed class DvpSnapshot
	{
		public int ds, endBar, anchorBar, sinceRefit, n, deg, dCap, manRowsEff;
		public DateTime tPrev, t0;
		public bool valid, crossesRoll, lowR2, refitBar, calibrating, manValid, diagnosticOnly;
		public double atr, sigma, sigmaAtr, r2, dw, skew, yhatEnd, slopeEnd, slope1, slope1SigmaPerBar, endpointDrift, uT, yhatT,
			pocZ, vaLoZ, vaHiZ, entropy, concentration, concentrationOcc, tailVol, inSampleCov2, rthShare;

		internal void Clear(int ds, int endBar, DateTime tPrev)
		{
			this.ds = ds; this.endBar = endBar; this.tPrev = tPrev; t0 = default(DateTime);
			anchorBar = int.MinValue; sinceRefit = 0; n = 0; deg = -1; dCap = 0; manRowsEff = 0;
			valid = crossesRoll = lowR2 = refitBar = calibrating = manValid = diagnosticOnly = false;
			atr = sigma = sigmaAtr = r2 = dw = skew = yhatEnd = slopeEnd = slope1 = slope1SigmaPerBar = endpointDrift = uT = yhatT = double.NaN;
			pocZ = vaLoZ = vaHiZ = entropy = concentration = concentrationOcc = tailVol = inSampleCov2 = rthShare = double.NaN;
		}
	}

	/// <summary>A copy of a fit's coefficients (no window arrays), for the journal-side forward-coverage tracker (DV-3.6 cov_fwd).</summary>
	public sealed class DvpCoef
	{
		public readonly double[] A = new double[DvpFit.MaxDeg + 1], B = new double[DvpFit.MaxDeg + 1], N = new double[DvpFit.MaxDeg + 1], C = new double[DvpFit.MaxDeg + 1];
		public double yBar, sigma; public int n, deg;
		public long startPos; public int seen, hits, hitsFlat;

		public void From(DvpFit f, long startPos)
		{
			yBar = f.YBar; sigma = f.SigmaD; n = f.n; deg = f.Deg;
			for (int k = 0; k <= deg; k++) { A[k] = f.A[k]; B[k] = f.B[k]; N[k] = f.N[k]; C[k] = f.C[k]; }
			this.startPos = startPos; seen = hits = hitsFlat = 0;
		}

		/// <summary>Same recurrence as DvpFit.Eval (yhat and leverage at u).</summary>
		public double Eval(double u, out double lev)
		{
			double pPrev = 0, p = 1, s = C[0], h = 1 / N[0];
			for (int k = 0; k < deg; k++)
			{
				double pn = (u - A[k]) * p - B[k] * pPrev;
				pPrev = p; p = pn;
				s += C[k + 1] * p; h += p * p / N[k + 1];
			}
			lev = h; return yBar + s;
		}
	}

	/// <summary>
	/// One DVP stream (DV-7): O(1) per-bar bookkeeping (ATR as of each bar, roll positions, skip share, sessions) plus a
	/// two-slot lazy cache keyed by window-end bar. Results depend only on the ring contents, never on query or journal
	/// history (E10, DV-T16b): refit bars are bar-index based (bar % refitEvery == 0), a between-refit query recomputes the
	/// scheduled refit from the ring, and endpointDrift recomputes the previous refit from the ring when it is not cached.
	/// </summary>
	public sealed class DvpStream
	{
		sealed class Slot
		{
			public readonly DvpFit fit; public readonly DvpManifold man; public readonly DvpRails rails; public readonly DvpSnapshot snap = new DvpSnapshot();
			public int key = int.MinValue, fitAnchor = int.MinValue;
			public bool fitOk;
			public double dw, skew, rthShare;
			public Slot(DvpConfig c) { fit = new DvpFit(c.winLen); man = new DvpManifold(c.manRows, c.splitFlow); rails = new DvpRails(c.sigmaList.Length); }
		}

		public readonly int Ds;
		public readonly DvpConfig Config;
		public readonly string Root;
		public readonly bool RollDetect, RthKnown;
		readonly double m_tick;
		readonly int m_n, m_r, m_K, m_H;
		readonly double[] m_y, m_h, m_l, m_v, m_up, m_dn, m_atrHist, m_fwdY;
		readonly Gate0Atr m_atr = new Gate0Atr(DvpConst.AtrLen);
		readonly Slot m_s0, m_s1, m_sh;   // m_sh: rail-history fills (DV-8.1), so they never evict the query cache
		readonly DvpFit m_prevFit;
		readonly DvpCoef[] m_fwd;
		int m_fwdCount;
		long m_pos = -1, m_roll1 = long.MinValue, m_roll0 = long.MinValue;
		int m_lastBar = int.MinValue;
		bool m_degWarned;

		/// <summary>Warning sink (DVP DEGREE fallback); null = silent.</summary>
		public Action<string> warnOnce;

		// sessions (DV-7.7): tradingDayKey changes on this stream's bars
		public DateTime SessionDay { get; private set; }
		public bool SessionHalfDay { get; private set; }
		public bool NewSession { get; private set; }
		public int Sessions { get; private set; }
		public bool LastHalfDay { get; private set; }

		// counters
		public long BarsSeen { get { return m_pos + 1; } }
		public long Eligible { get; private set; }     // windows after warm-up (DV-7.4)
		public long Skipped { get; private set; }      // of which crossing a roll
		public long FitsComputed { get; private set; } // window fits actually run (laziness counter, DV-T22)
		public long FitLines { get; private set; }
		public long SkipLines { get; private set; }
		double m_covInSum, m_covFwdSum, m_covFwdFlatSum; long m_covInN, m_covFwdN;

		public DvpStream(int ds, DvpConfig cfg, string root, double tick, bool rollDetect, bool rthKnown)
		{
			Ds = ds; Config = cfg; Root = root ?? ""; m_tick = tick; RollDetect = rollDetect; RthKnown = rthKnown;
			m_n = cfg.winLen; m_r = Math.Max(1, cfg.refitEvery); m_H = Math.Max(1, cfg.projBars);
			m_K = Math.Max(m_r + 4, DvpRailHistory.Depth + 2);   // spec rev 5.1 DV-8.1 (S2, depth): max(refitEvery + 4, 10); endAgo <= 8 carries its ATR(t-1)
			m_y = new double[m_n]; m_h = new double[m_n]; m_l = new double[m_n]; m_v = new double[m_n];
			m_up = cfg.splitFlow ? new double[m_n] : null; m_dn = cfg.splitFlow ? new double[m_n] : null;
			m_atrHist = new double[m_K];
			m_fwdY = new double[1];
			m_s0 = new Slot(cfg); m_s1 = new Slot(cfg); m_sh = new Slot(cfg);
			int maxRails = m_s0.rails.Items.Length, maxLat = cfg.useFib ? cfg.fibRanges * DvpLattice.NFib : 0;
			History = new DvpRailHistory(this, maxRails);
			Confluence = new DvpConfluence(maxRails + maxLat);
			RailJournal = cfg.journalRails ? new DvpRailJournal(maxRails) : null;   // DV-13.3: only when the DVP RAIL journal is on
			m_prevFit = new DvpFit(m_n);
			m_fwd = new DvpCoef[m_H / m_r + 2];
			for (int i = 0; i < m_fwd.Length; i++) m_fwd[i] = new DvpCoef();
			if (cfg.useFib)
			{
				Lattice = new DvpLattice(cfg.fibRanges); Pivots = new DvpPivots[cfg.fibRanges];
				for (int j = 0; j < cfg.fibRanges; j++) Pivots[j] = new DvpPivots(PivotCapacity);
			}
		}

		/// <summary>DV-5: lattice and its per-range pivot buffers (null when useFib is false).</summary>
		public readonly DvpLattice Lattice; public readonly DvpPivots[] Pivots;
		/// <summary>Pivots copied per range: both sides of a PriceActionConfig.history = 50 engine.</summary>
		public const int PivotCapacity = 128;

		/// <summary>DV-5 lattice of the snapshot ending endAgo, from Pivots (filled by the host for this query). Null when useFib is off
		/// or there is no valid fit (lattice levels carry a z-equivalent and are published with the rails).</summary>
		public DvpLattice LatticeFor(_BarRing ring, int endAgo)
		{
			if (Lattice == null) return null;
			DvpSnapshot s = Snapshot(ring, endAgo);
			if (s == null || !s.valid) { Lattice.Count = 0; return null; }
			Lattice.Build(Config, Pivots, s.endBar, s.atr, ring[endAgo].c);
			return Lattice;
		}

		/// <summary>Ring bars this stream needs (DV-0.2): winLen + refitEvery (anchor offset) + endpointDrift's previous window + slack.</summary>
		public static int RingNeed(DvpConfig c) { return c.winLen + 2 * Math.Max(1, c.refitEvery) + 8; }

		public bool DiagnosticOnly { get { return !RollDetect || SkipShare > 0.30; } }
		public double SkipShare { get { return !RollDetect ? double.NaN : Eligible > 0 ? (double)Skipped / Eligible : 0.0; } }
		public double MeanCovIn { get { return m_covInN > 0 ? m_covInSum / m_covInN : double.NaN; } }
		public double MeanCovFwd { get { return m_covFwdN > 0 ? m_covFwdSum / m_covFwdN : double.NaN; } }
		public double MeanCovFwdFlat { get { return m_covFwdN > 0 ? m_covFwdFlatSum / m_covFwdN : double.NaN; } }
		public int FallbackCount { get { return m_s0.fit.FallbackCount + m_s1.fit.FallbackCount + m_prevFit.FallbackCount; } }

		static int Mod(int a, int m) { int x = a % m; return x < 0 ? x + m : x; }

		double Src(RingBar b) { return Config.src == EDvpSrc.Close ? b.c : (b.h + b.l) * 0.5; }

		/// <summary>
		/// Closed-bar step (O(1) without a journal): ring[0] is this stream's newest closed bar, already added by the host.
		/// Returns true for a new bar. journal != null with journalFits makes the journal a consumer (Q14): every refit bar's
		/// window is computed and one DVP FIT line written.
		/// </summary>
		public bool OnBar(_BarRing ring, bool halfDay, Action<string> journal, string runId, string configHash)
		{
			if (ring == null || ring.Count == 0) return false;
			RingBar b = ring[0];
			if (b.bar == m_lastBar) return false;
			m_lastBar = b.bar; m_pos++;
			if (b.rollBar) { m_roll0 = m_roll1; m_roll1 = m_pos; if (RollDetect) m_atr.ResetPrev(); }   // DV-7.3
			m_atr.Add(b.h, b.l, b.c);
			m_atrHist[(int)(m_pos % m_K)] = m_atr.Value;
			NewSession = m_pos == 0 || b.tradingDay != SessionDay;   // DV-7.7: tradingDayKey changes, never calendar dates
			if (NewSession) { Sessions++; SessionDay = b.tradingDay; SessionHalfDay = halfDay; }
			LastHalfDay = SessionHalfDay;
			// DV-7.1 / 7.4: skip accounting on every closed bar from roll positions (no fit)
			long aPos = m_pos - Mod(b.bar, m_r);
			if (aPos - m_n + 1 >= 0)
			{
				Eligible++;
				if (RollDetect && crossesByPos(aPos)) Skipped++;
			}
			if (journal != null)
			{
				trackForward(b);
				if (Config.journalFits && Mod(b.bar, m_r) == 0) journalFit(ring, journal, runId, configHash);
			}
			return true;
		}

		bool crossesByPos(long aPos)
		{
			long lo = aPos - m_n + 2;   // i in [1, n-1]
			return (m_roll1 >= lo && m_roll1 <= aPos) || (m_roll0 >= lo && m_roll0 <= aPos);
		}

		static bool crossesInRing(_BarRing ring, int anchorAgo, int n)
		{
			for (int a = anchorAgo; a <= anchorAgo + n - 2; a++) if (ring[a].rollBar) return true;   // i = n-1-(a-anchorAgo) in [1, n-1]
			return false;
		}

		/// <summary>The snapshot whose window ends endAgo bars back in the ring (endAgo = 1 when ring[0] is the current closed bar t).</summary>
		public DvpSnapshot Snapshot(_BarRing ring, int endAgo)
		{
			if (ring == null || endAgo < 0 || ring.Count <= endAgo || ring[0].bar != m_lastBar) return null;
			RingBar eb = ring[endAgo];
			if (m_s0.key == eb.bar) return m_s0.snap;
			if (m_s1.key == eb.bar) return m_s1.snap;
			Slot s = m_s0.key <= m_s1.key ? m_s0 : m_s1;   // replace the older end bar
			compute(s, ring, endAgo);
			return s.snap;
		}

		/// <summary>PR-D5 (DV-8.1/6): rail history of the last 8 snapshots, the confluence scorer and the DVP RAIL tracker (all preallocated).</summary>
		public readonly DvpRailHistory History; public readonly DvpConfluence Confluence; public readonly DvpRailJournal RailJournal;

		/// <summary>
		/// DV-8.1: the snapshot ending endAgo for the rail history. Served from the query cache when it holds that end bar, else
		/// computed into a private slot, so a history fill never evicts (overwrites) a snapshot the host holds. Same pure function
		/// of the window as Snapshot (DV-0.7), so the history is path-independent.
		/// </summary>
		public DvpSnapshot HistSnapshot(_BarRing ring, int endAgo, out DvpRails rails)
		{
			rails = null;
			if (ring == null || endAgo < 0 || ring.Count <= endAgo || ring[0].bar != m_lastBar) return null;
			int key = ring[endAgo].bar;
			Slot s = m_s0.key == key ? m_s0 : m_s1.key == key ? m_s1 : m_sh;
			if (s.key != key) compute(s, ring, endAgo);
			if (s.snap.valid) rails = s.rails;
			return s.snap;
		}

		/// <summary>Rails of the cached snapshot for this window end (after Snapshot), or null.</summary>
		public DvpRails RailsFor(DvpSnapshot snap)
		{
			if (snap == null || !snap.valid) return null;
			if (m_s0.snap == snap) return m_s0.rails;
			if (m_s1.snap == snap) return m_s1.rails;
			return null;
		}

		public DvpFit FitFor(DvpSnapshot snap)
		{
			if (snap == null || !snap.valid) return null;
			if (m_s0.snap == snap) return m_s0.fit;
			if (m_s1.snap == snap) return m_s1.fit;
			return null;
		}

		void compute(Slot s, _BarRing ring, int endAgo)
		{
			DvpConfig c = Config;
			RingBar eb = ring[endAgo];
			DvpSnapshot snap = s.snap;
			snap.Clear(Ds, eb.bar, eb.time);
			s.key = eb.bar;
			snap.diagnosticOnly = !RollDetect;   // the skipShare verdict is run-level (DV-7.4), never a per-bar flag
			long ePos = m_pos - endAgo;
			int off = Mod(eb.bar, m_r), aAgo = endAgo + off;
			long aPos = ePos - off;
			snap.sinceRefit = off; snap.refitBar = off == 0;
			snap.atr = m_pos - ePos < m_K && ePos >= 0 ? m_atrHist[(int)(ePos % m_K)] : double.NaN;
			if (aPos - m_n + 1 < 0 || ring.Count < aAgo + m_n) return;   // warm-up: no fit (DV-7.1 "fits resume after n bars")
			snap.anchorBar = ring[aAgo].bar; snap.t0 = ring[aAgo + m_n - 1].time;
			if (RollDetect && crossesInRing(ring, aAgo, m_n)) { snap.crossesRoll = true; return; }   // skipped: no rails
			if (s.fitAnchor != snap.anchorBar)
			{
				s.fitAnchor = snap.anchorBar;
				s.fitOk = fitWindow(s.fit, ring, aAgo, true);
				s.rthShare = m_rthShare;
				if (s.fitOk)
				{
					s.man.Build(s.fit, m_h, m_l, m_v, m_up, m_dn, c.manSpan, m_tick, c.VaFraction, c.nodeProm, c.maxNodes);
					s.fit.ResidualStats(s.fit.Deg, out s.dw, out s.skew);
				}
			}
			if (!s.fitOk) return;
			DvpFit f = s.fit; DvpManifold man = s.man;
			int d = f.Deg;
			snap.valid = true; snap.n = f.n; snap.deg = d; snap.dCap = f.DTop; snap.calibrating = f.Calibrating;
			snap.sigma = f.SigmaD; snap.sigmaAtr = snap.sigma / snap.atr; snap.r2 = f.R2[d]; snap.lowR2 = !(snap.r2 >= c.minR2);
			snap.dw = s.dw; snap.skew = s.skew; snap.rthShare = s.rthShare;
			snap.yhatEnd = f.Yhat(1, d); snap.slopeEnd = f.SlopeEnd(d); snap.slope1 = f.SlopeEnd(1);
			snap.slope1SigmaPerBar = snap.slope1 / f.SigmaAt(1);
			snap.uT = f.UAhead(1 + off);   // DV-3.7: frozen coefficients at the current bar's u
			s.rails.Build(f, man, c.sigmaList, snap.uT);
			snap.yhatT = s.rails.YhatT;
			snap.manValid = man.Valid; snap.manRowsEff = man.RowsEff; snap.inSampleCov2 = man.InSampleCov2;
			if (man.Valid)
			{
				snap.pocZ = man.PocZ; snap.vaLoZ = man.VaLoZ; snap.vaHiZ = man.VaHiZ; snap.entropy = man.Entropy;
				snap.concentration = man.Concentration; snap.concentrationOcc = man.ConcentrationOcc; snap.tailVol = man.TailVol;
			}
			if (off == 0) snap.endpointDrift = drift(s, ring, aAgo);   // DV-3.7: refit bars only
		}

		double m_rthShare;

		/// <summary>Loads the n-bar window ending at anchorAgo (oldest first) and fits it with the DV-3.6 degree rule. No allocation.</summary>
		bool fitWindow(DvpFit fit, _BarRing ring, int anchorAgo, bool loadAll)
		{
			DvpConfig c = Config;
			int n = m_n;
			double vol = 0, rth = 0;
			for (int i = 0; i < n; i++)
			{
				RingBar x = ring[anchorAgo + n - 1 - i];
				m_y[i] = Src(x); m_v[i] = x.v;
				if (loadAll)
				{
					m_h[i] = x.h; m_l[i] = x.l;
					if (m_up != null) { m_up[i] = x.up; m_dn[i] = x.down; }
					if (x.v > 0 && !double.IsInfinity(x.v)) { vol += x.v; if (x.rth) rth += x.v; }
				}
			}
			if (loadAll) m_rthShare = RthKnown && vol > 0 ? rth / vol : double.NaN;   // DV-7.7: NaN on the generic base
			FitsComputed++;
			if (!fit.Fit(m_y, m_v, n, c.degMax, c.weightVol)) return false;
			int dStar, d; bool calib, warn;
			bool hasFrozen = DvpFrozenDegree.TryGet(Root, Ds, n, out dStar);
			DvpDegree.Select(c.degMode, n, c.degMax, hasFrozen, dStar, ref m_degWarned, out d, out calib, out warn);
			if (warn && warnOnce != null)
				warnOnce("DVP DEGREE: no frozen d* for root(" + Root + ") stream(Data" + Ds.ToString(CultureInfo.InvariantCulture) + ") winLen("
					+ n.ToString(CultureInfo.InvariantCulture) + "); DevCoverageFrozen falls back to d = 1");
			fit.Choose(d, calib);
			return true;
		}

		/// <summary>
		/// DV-3.8 endpointDrift, in ATR as of t-1: the new fit at the previous refit's endpoint bar minus the previous fit's own
		/// endpoint value (same bar; see the PR-D3 note). The previous refit window is the cached slot when present, else it is
		/// recomputed from the ring, so the value never depends on query or journal history. NaN when the previous window is
		/// not in the ring, crosses a roll or does not fit.
		/// </summary>
		double drift(Slot s, _BarRing ring, int aAgo)
		{
			int pAgo = aAgo + m_r;
			if (ring.Count < pAgo + m_n || m_pos - (aAgo + m_r) - m_n + 1 < 0) return double.NaN;
			if (RollDetect && crossesInRing(ring, pAgo, m_n)) return double.NaN;
			int pBar = ring[pAgo].bar;
			Slot o = s == m_s0 ? m_s1 : m_s0;
			DvpFit prev;
			if (o.fitAnchor == pBar) { if (!o.fitOk) return double.NaN; prev = o.fit; }
			else { if (!fitWindow(m_prevFit, ring, pAgo, false)) return double.NaN; prev = m_prevFit; }
			DvpFit f = s.fit;
			double now = f.Yhat(f.U(m_n - 1 - m_r), f.Deg), was = prev.Yhat(1, prev.Deg);
			return (now - was) / s.snap.atr;
		}

		// ---- journal side (Q14: the journal is a consumer only when its sink is on) ----

		/// <summary>cov_fwd horizons dropped because they crossed a roll (journal-side; DVP RUN fwdDropped).</summary>
		public long FwdDropped { get; private set; }

		void trackForward(RingBar b)
		{
			if (b.rollBar && RollDetect) { FwdDropped += m_fwdCount; m_fwdCount = 0; return; }   // a forward horizon across a splice is dropped (counted)
			double y = Src(b);
			int w = 0;
			for (int i = 0; i < m_fwdCount; i++)
			{
				DvpCoef q = m_fwd[i];
				int h = (int)(m_pos - q.startPos);
				double lev, yh = q.Eval(1 + 2.0 * h / (q.n - 1), out lev);
				double e = Math.Abs(y - yh);
				q.seen++;
				if (e <= 2 * q.sigma * Math.Sqrt(1 + lev)) q.hits++;
				if (e <= 2 * q.sigma) q.hitsFlat++;
				if (q.seen >= m_H) { m_covFwdSum += (double)q.hits / m_H; m_covFwdFlatSum += (double)q.hitsFlat / m_H; m_covFwdN++; }
				else { m_fwd[i] = m_fwd[w]; m_fwd[w] = q; w++; }
			}
			m_fwdCount = w;
		}

		void journalFit(_BarRing ring, Action<string> journal, string runId, string configHash)
		{
			DvpSnapshot s = Snapshot(ring, 0);
			if (s == null || s.anchorBar == int.MinValue) return;   // warm-up: no window yet
			DvpFit f = FitFor(s);
			journal(FitLine(s, f, runId, configHash));
			if (s.crossesRoll || !s.valid) { SkipLines++; return; }
			FitLines++;
			m_covInSum += f.CovIn(f.Deg, 2); m_covInN++;
			if (m_fwdCount < m_fwd.Length) m_fwd[m_fwdCount++].From(f, m_pos);
		}

		static string F(double x) { return x.ToString("R", CultureInfo.InvariantCulture); }
		static string I(long x) { return x.ToString(CultureInfo.InvariantCulture); }

		/// <summary>DV-11.1 DVP FIT line (invariant culture, R round-trip). A skipped window writes crossesRoll(1) and NaNs.</summary>
		public static string FitLine(DvpSnapshot s, DvpFit f, string runId, string configHash)
		{
			var sb = new StringBuilder(512);
			sb.Append("DVP FIT runId(").Append(runId).Append(") configHash(").Append(configHash).Append(") stream(Data").Append(I(s.ds))
				.Append(") barTime(").Append(s.tPrev.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(')');
			bool ok = s.valid && f != null;
			sb.Append(" n(").Append(ok ? I(s.n) : "NaN").Append(") deg(").Append(ok ? I(s.deg) : "NaN").Append(") bic(");
			if (ok) for (int k = 1; k <= f.MaxValidDeg; k++) { if (k > 1) sb.Append(';'); sb.Append(F(f.Bic[k])); }
			else sb.Append("NaN");
			sb.Append(") manRowsEff(").Append(ok ? I(s.manRowsEff) : "NaN").Append(") rthShare(").Append(F(s.rthShare))
				.Append(") crossesRoll(").Append(s.crossesRoll ? '1' : '0').Append(')');
			kv(sb, "sigma", s.sigma); kv(sb, "sigmaAtr", s.sigmaAtr); kv(sb, "r2", s.r2); kv(sb, "dw", s.dw); kv(sb, "skew", s.skew);
			kv(sb, "yhatEnd", s.yhatEnd); kv(sb, "slopeEnd", s.slopeEnd); kv(sb, "slope1", s.slope1); kv(sb, "endpointDrift", s.endpointDrift);
			kv(sb, "pocZ", s.pocZ); kv(sb, "vaLoZ", s.vaLoZ); kv(sb, "vaHiZ", s.vaHiZ); kv(sb, "entropy", s.entropy);
			kv(sb, "concentration", s.concentration); kv(sb, "concentrationOcc", s.concentrationOcc); kv(sb, "tailVol", s.tailVol);
			kv(sb, "inSampleCov2", s.inSampleCov2);
			sb.Append(" lowR2(").Append(ok ? (s.lowR2 ? "1" : "0") : "NaN").Append(") coef{");
			if (ok)
			{
				int d = f.Deg;
				sb.Append(F(f.YBar)).Append(';').Append(F(f.Ctr)).Append(';').Append(F(f.Scl));
				for (int k = 0; k <= d; k++) sb.Append(';').Append(F(f.A[k]));
				for (int k = 1; k <= d; k++) sb.Append(';').Append(F(f.B[k]));
				for (int k = 0; k <= d; k++) sb.Append(';').Append(F(f.N[k]));
				for (int k = 0; k <= d; k++) sb.Append(';').Append(F(f.C[k]));
			}
			else sb.Append("NaN");
			sb.Append('}');
			if (ok && s.calibrating)
			{
				sb.Append(" calibrate(sigma");
				for (int k = 1; k <= f.MaxValidDeg; k++) sb.Append(k == 1 ? "=" : ";").Append(F(f.Sigma[k]));
				sb.Append(" r2");
				for (int k = 1; k <= f.MaxValidDeg; k++) sb.Append(k == 1 ? "=" : ";").Append(F(f.R2[k]));
				sb.Append(" covIn2");
				for (int k = 1; k <= f.MaxValidDeg; k++) sb.Append(k == 1 ? "=" : ";").Append(F(f.CovIn(k, 2)));
				sb.Append(')');
			}
			return sb.ToString();
		}

		static void kv(StringBuilder sb, string k, double v) { sb.Append(' ').Append(k).Append('(').Append(F(v)).Append(')'); }

		/// <summary>DV-11.3 DVP RUN line for this stream.</summary>
		public string RunLine(string runId)
		{
			return "DVP RUN runId(" + runId + ") stream(Data" + I(Ds) + ") bars(" + I(BarsSeen) + ") fits(" + I(FitLines) + ") skips(" + I(SkipLines)
				+ ") eligible(" + I(Eligible) + ") skipped(" + I(Skipped) + ") skipShare(" + F(SkipShare) + ") rollDetect(" + (RollDetect ? "On" : "Off")
				+ ") diagnosticOnly(" + (DiagnosticOnly ? "1" : "0") + ") meanCovIn(" + F(MeanCovIn) + ") meanCovFwd(" + F(MeanCovFwd)
				+ ") meanCovFwdFlat(" + F(MeanCovFwdFlat) + ") fallbacks(" + I(FallbackCount) + ") sessions(" + I(Sessions) + ") fwdDropped(" + FwdDropped.ToString(CultureInfo.InvariantCulture) + ")";
		}
	}

	/// <summary>
	/// M-DVP engine (DV-0.1): internal sealed, created by the host in StartCalc only when DvpConfig.enabled (null otherwise).
	/// MC-free: the host passes each stream's ring and flags; the engine never touches Bars.
	/// </summary>
	internal sealed class _DvpEngine
	{
		public readonly DvpConfig Config;
		public readonly string RunId, ConfigHash;
		public readonly bool RollDetect;
		readonly DvpStream[] m_streams;
		readonly int[] m_ds;
		public Action<string> journal, warnOnce;
		bool m_finished;

		public _DvpEngine(DvpConfig cfg, string runId, string root, double tick, bool rollDetect, bool rthKnown, Action<string> journal, Action<string> warnOnce, string paLabels = null)
		{
			Config = cfg; RunId = runId ?? ""; RollDetect = rollDetect; this.journal = journal; this.warnOnce = warnOnce;
			ConfigHash = Fnv1a64(cfg.Canon()).ToString("x16", CultureInfo.InvariantCulture);
			m_ds = (int[])cfg.streams.Clone();
			m_streams = new DvpStream[m_ds.Length];
			for (int i = 0; i < m_ds.Length; i++) m_streams[i] = new DvpStream(m_ds[i], cfg, root, tick, rollDetect, rthKnown) { warnOnce = warnOnce };
			if (!rollDetect && warnOnce != null) warnOnce("DVP: RollDetection Off — roll skip disabled");   // DV-7.0: every stream diagnostic-only
			if (journal != null) journal(CreatedLine(root, paLabels));
		}

		public int StreamCount { get { return m_streams.Length; } }
		public DvpStream Stream(int ds) { for (int i = 0; i < m_ds.Length; i++) if (m_ds[i] == ds) return m_streams[i]; return null; }

		public static ulong Fnv1a64(string s)
		{
			ulong h = 14695981039346656037UL;
			for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 1099511628211UL; }
			return h;
		}

		string CreatedLine(string root, string paLabels)
		{
			var sb = new StringBuilder(256);
			sb.Append("DVP CREATED runId(").Append(RunId).Append(") configHash(").Append(ConfigHash).Append(") streams(");
			for (int i = 0; i < m_ds.Length; i++) { if (i > 0) sb.Append(','); sb.Append("Data").Append(m_ds[i].ToString(CultureInfo.InvariantCulture)); }
			sb.Append(") d(");
			for (int i = 0; i < m_ds.Length; i++)
			{
				if (i > 0) sb.Append(',');
				int dStar, d; bool calib, warn, latch = true;
				bool has = DvpFrozenDegree.TryGet(root ?? "", m_ds[i], Config.winLen, out dStar);
				DvpDegree.Select(Config.degMode, Config.winLen, Config.degMax, has, dStar, ref latch, out d, out calib, out warn);
				bool unfrozen = Config.degMode == EDvpDegMode.DevCoverageFrozen && !has;
				sb.Append("Data").Append(m_ds[i].ToString(CultureInfo.InvariantCulture)).Append('=').Append(d.ToString(CultureInfo.InvariantCulture));
				if (calib || unfrozen) sb.Append('(').Append(calib ? "calibrate" : "").Append(calib && unfrozen ? "," : "").Append(unfrozen ? "unfrozen" : "").Append(')');
			}
			sb.Append(") pa(").Append(string.IsNullOrEmpty(paLabels) ? "none" : paLabels).Append(") rollDetect(").Append(RollDetect ? "On" : "Off").Append(") canon(").Append(Config.Canon()).Append(')');
			return sb.ToString();
		}

		/// <summary>Closed-bar step for stream ds (ring[0] = the bar just closed).</summary>
		public bool OnBar(int ds, _BarRing ring, bool halfDay)
		{
			DvpStream s = Stream(ds);
			return s != null && s.OnBar(ring, halfDay, journal, RunId, ConfigHash);
		}

		/// <summary>DV-10 Snapshot for bar t; endAgo = 1 when ring[0] is the current closed bar, 0 intrabar. Null if no fit or skipped.</summary>
		public DvpSnapshot Snapshot(int ds, _BarRing ring, int endAgo)
		{
			DvpStream s = Stream(ds);
			DvpSnapshot x = s != null ? s.Snapshot(ring, endAgo) : null;
			return x != null && x.valid ? x : null;
		}

		/// <summary>Snapshot including warm-up / skipped records (valid = false), for tests and the journal.</summary>
		public DvpSnapshot SnapshotRaw(int ds, _BarRing ring, int endAgo)
		{
			DvpStream s = Stream(ds);
			return s != null ? s.Snapshot(ring, endAgo) : null;
		}

		public DvpLattice LatticeOf(int ds, _BarRing ring, int endAgo)
		{
			DvpStream s = Stream(ds);
			return s == null ? null : s.LatticeFor(ring, endAgo);
		}

		public DvpRails RailsOf(int ds, _BarRing ring, int endAgo)
		{
			DvpStream s = Stream(ds);
			if (s == null) return null;
			return s.RailsFor(s.Snapshot(ring, endAgo));
		}

		/// <summary>DV-8.1: stream ds's rail history loaded for one rail (kind, z), newest closed bar first; null if no such stream.</summary>
		public DvpRailHistory HistoryOf(int ds, _BarRing ring, ELevelKind kind, double z)
		{
			DvpStream s = Stream(ds);
			if (s == null) return null;
			s.History.Load(ring, kind, z);
			return s.History;
		}

		/// <summary>DV-6: stream ds's confluence scorer loaded with rails + lattice and clustered at ATR(t-1), C(t-1), confTol.</summary>
		public DvpConfluence ConfluenceOf(int ds, DvpSnapshot snap, DvpRails rails, DvpLattice lat, double cPrev)
		{
			DvpStream s = Stream(ds);
			if (s == null) return null;
			s.Confluence.Load(rails, lat);
			s.Confluence.Cluster(snap != null ? snap.atr : double.NaN, cPrev, Config.confTol);
			return s.Confluence;
		}

		/// <summary>DV-11.3: the DVP RUN lines, once at end of run.</summary>
		public void Finish()
		{
			if (m_finished) return;
			m_finished = true;
			if (journal == null) return;
			for (int i = 0; i < m_streams.Length; i++) { if (m_streams[i].RailJournal != null) m_streams[i].RailJournal.Finish(m_ds[i], journal, RunId); }   // DV-11.2 unfinished trackers
			for (int i = 0; i < m_streams.Length; i++) journal(m_streams[i].RunLine(RunId));
		}
	}

	// ---------------------------------------------------------------------------------------------------------------
	// PR-D4 (DV-5): Fib lattice. MC-free: the host copies each range's confirmed pivots (from a hosted _PriceActionEngine,
	// see DvpPaBridge) into a DvpPivots buffer, in ring bar numbering; DvpLattice picks the anchors, lays out the 24 levels,
	// merges and filters them. Buffers are sized once (DvpStream constructor, i.e. under the _DvpEngine constructor).
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>Confirmed pivots of one lattice range (either side, any order), bar numbers in the stream's ring numbering.</summary>
	public sealed class DvpPivots
	{
		public readonly double[] Price; public readonly int[] ExtremeBar, ConfirmBar; public readonly bool[] IsHigh;
		public int Count;
		public DvpPivots(int capacity) { Price = new double[capacity]; ExtremeBar = new int[capacity]; ConfirmBar = new int[capacity]; IsHigh = new bool[capacity]; }
		public int Capacity { get { return Price.Length; } }
		public void Clear() { Count = 0; }
		public bool Add(bool isHigh, double price, int extremeBar, int confirmBar)
		{
			if (Count >= Price.Length) return false;
			IsHigh[Count] = isHigh; Price[Count] = price; ExtremeBar[Count] = extremeBar; ConfirmBar[Count] = confirmBar; Count++;
			return true;
		}
	}

	/// <summary>
	/// DV-5.2..5.5 lattice for one snapshot. Range j (0-based here, R{j+1} in tags) uses strength s_j = fibPivot*2^j.
	/// Anchor (MostRecentPivot): B = the confirmed pivot (confirmBar &lt;= endBar) with the latest confirmBar (ties: later
	/// extremeBar, then the high); A = the confirmed opposite-side pivot with the latest extremeBar before B's. Both extremeBars
	/// must be within fibLookMult*s_j bars of endBar (bar t-1), else the range is empty. Levels L(f) = B - f(B - A) over the 24
	/// FIB_LEVELS; within a range, sorted by price, greedy merge: a level joins the current group while it is closer than
	/// fibMerge*ATR to the group's first (lowest) member (strict); the group's price is its member with the smallest |f - round f|
	/// (ties: lower f). Then levels further than fibNear*ATR from C(t-1) are dropped (|P - C| &lt;= fibNear*ATR kept). Ranges never merge.
	/// </summary>
	public sealed class DvpLattice
	{
		/// <summary>DV-5.3 verbatim (rev 3.1 §2.2): do not "correct" .22/.62/.71/-2.62.</summary>
		public static double FibLevel(int i)
		{
			switch (i)
			{
				case 0: return 0; case 1: return .22; case 2: return .5; case 3: return .62; case 4: return .71; case 5: return .786;
				case 6: return .886; case 7: return 1; case 8: return 1.414; case 9: return 1.618; case 10: return 2.618; case 11: return 3.618;
				case 12: return 4.236; case 13: return -.146; case 14: return -.272; case 15: return -.414; case 16: return -.764; case 17: return -1;
				case 18: return -2; case 19: return -2.5; case 20: return -2.62; case 21: return -3; case 22: return -3.5; case 23: return -4;
				default: return double.NaN;
			}
		}
		public const int NFib = 24;

		public readonly int Ranges;
		public readonly double[] Price, F; public readonly int[] Range;   // output, Count levels, grouped by range, price-ascending within a range
		public int Count;
		public readonly double[] AnchorA, AnchorB; public readonly int[] AnchorABar, AnchorBBar; public readonly bool[] RangeOk;
		readonly double[] m_p = new double[NFib], m_f = new double[NFib];

		public DvpLattice(int ranges)
		{
			Ranges = ranges;
			Price = new double[ranges * NFib]; F = new double[ranges * NFib]; Range = new int[ranges * NFib];
			AnchorA = new double[ranges]; AnchorB = new double[ranges]; AnchorABar = new int[ranges]; AnchorBBar = new int[ranges]; RangeOk = new bool[ranges];
		}

		public static int Strength(DvpConfig c, int j) { return c.fibPivot << j; }

		/// <summary>DV-5.2. False (range empty) when no B, no A, or either extreme is older than look bars before endBar.</summary>
		public static bool Anchor(DvpPivots p, int endBar, int look, out double a, out double b, out int aBar, out int bBar)
		{
			a = b = double.NaN; aBar = bBar = int.MinValue;
			int bi = -1;
			for (int i = 0; i < p.Count; i++)
			{
				if (p.ConfirmBar[i] > endBar) continue;   // unusable before confirmBar
				if (bi < 0 || p.ConfirmBar[i] > p.ConfirmBar[bi] || (p.ConfirmBar[i] == p.ConfirmBar[bi]
					&& (p.ExtremeBar[i] > p.ExtremeBar[bi] || (p.ExtremeBar[i] == p.ExtremeBar[bi] && p.IsHigh[i] && !p.IsHigh[bi])))) bi = i;
			}
			if (bi < 0) return false;
			int ai = -1;
			for (int i = 0; i < p.Count; i++)
			{
				if (p.ConfirmBar[i] > endBar || p.IsHigh[i] == p.IsHigh[bi] || p.ExtremeBar[i] >= p.ExtremeBar[bi]) continue;
				if (ai < 0 || p.ExtremeBar[i] > p.ExtremeBar[ai]) ai = i;
			}
			if (ai < 0) return false;
			if (endBar - p.ExtremeBar[bi] > look || endBar - p.ExtremeBar[ai] > look) return false;
			a = p.Price[ai]; b = p.Price[bi]; aBar = p.ExtremeBar[ai]; bBar = p.ExtremeBar[bi];
			return true;
		}

		/// <summary>Builds every range. pivots[j] holds range j's confirmed pivots. No allocation.</summary>
		public void Build(DvpConfig c, DvpPivots[] pivots, int endBar, double atr, double close)
		{
			Count = 0;
			for (int j = 0; j < Ranges; j++)
			{
				RangeOk[j] = false; AnchorA[j] = AnchorB[j] = double.NaN; AnchorABar[j] = AnchorBBar[j] = int.MinValue;
				if (!(atr > 0) || double.IsNaN(close) || pivots == null || j >= pivots.Length || pivots[j] == null) continue;
				double a, b; int ab, bb;
				if (!Anchor(pivots[j], endBar, c.fibLookMult * Strength(c, j), out a, out b, out ab, out bb)) continue;
				RangeOk[j] = true; AnchorA[j] = a; AnchorB[j] = b; AnchorABar[j] = ab; AnchorBBar[j] = bb;
				for (int i = 0; i < NFib; i++) { double f = FibLevel(i); m_f[i] = f; m_p[i] = b - f * (b - a); }
				for (int i = 1; i < NFib; i++)   // insertion sort by price, then f
				{
					double pp = m_p[i], ff = m_f[i]; int k = i - 1;
					while (k >= 0 && (m_p[k] > pp || (m_p[k] == pp && m_f[k] > ff))) { m_p[k + 1] = m_p[k]; m_f[k + 1] = m_f[k]; k--; }
					m_p[k + 1] = pp; m_f[k + 1] = ff;
				}
				double tol = c.fibMerge * atr, near = c.fibNear * atr;
				int g = 0;
				while (g < NFib)
				{
					int best = g, e = g + 1;
					while (e < NFib && m_p[e] - m_p[g] < tol) { if (better(m_f[e], m_f[best])) best = e; e++; }
					if (Math.Abs(m_p[best] - close) <= near) { Price[Count] = m_p[best]; F[Count] = m_f[best]; Range[Count] = j; Count++; }
					g = e;
				}
			}
		}

		static bool better(double f, double cur)
		{
			double df = Math.Abs(f - Math.Round(f)), dc = Math.Abs(cur - Math.Round(cur));
			return df < dc || (df == dc && f < cur);
		}

		/// <summary>DV-10.2 lattice tag: DVP{ds}:{t-1}:d{deg}:DvpFib{f:+0.000}R{j}.</summary>
		public static string Tag(int ds, DateTime tPrev, int deg, double f, int range)
		{
			return "DVP" + ds.ToString(CultureInfo.InvariantCulture) + ":" + tPrev.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + ":d"
				+ deg.ToString(CultureInfo.InvariantCulture) + ":" + ELevelKind.DvpFib.ToString() + f.ToString("+0.000;-0.000;+0.000", CultureInfo.InvariantCulture)
				+ "R" + (range + 1).ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Appends the levels as DvpFib LevelRefs (flat prices, DV-5.5) to into, reusing pool entries from into.Count on.</summary>
		public void Publish(List<LevelRef> into, List<LevelRef> pool, int ds, DateTime tPrev, DateTime t0, int deg, bool isRollWindow)
		{
			for (int i = 0; i < Count; i++)
			{
				int k = into.Count; LevelRef r;
				if (k < pool.Count) r = pool[k]; else { r = new LevelRef(); pool.Add(r); }
				r.kind = ELevelKind.DvpFib; r.price = r.lo = r.hi = Price[i]; r.createdTime = tPrev; r.sourceT0 = t0; r.sourceT1 = tPrev; r.dataStream = ds;
				r.tag = Tag(ds, tPrev, deg, F[i], Range[i]); r.quality = double.NaN; r.crossesRoll = false; r.isRollWindow = isRollWindow;
				into.Add(r);
			}
		}
	}

	// ---------------------------------------------------------------------------------------------------------------
	// PR-D5 (DV-6, DV-8, DV-11.2): confluence scorer, acceptance primitives, rail history and the DVP RAIL tracker.
	// MC-free and allocation-free on the query path: every buffer is sized in the DvpStream constructor.
	// ---------------------------------------------------------------------------------------------------------------

	/// <summary>
	/// DV-6 weightless confluence. Candidates (DV-6.1) are the snapshot's manifold rails (POC/VAH/VAL/HVN/LVN, family 0), sigma
	/// rails (family 1) and lattice levels (family 2, with their range id) at their bar-t prices; the cone is never a candidate.
	/// DV-6.2 greedy clustering with the deterministic weightless seed order: max F (distinct families among unclaimed candidates
	/// within confTol*ATR), then max K (their count), then min |P - C(t-1)|, then lowest price (then candidate order, which cannot
	/// matter once prices tie). The cluster is every unclaimed candidate within confTol*ATR of the seed (inclusive, DV-T18e) and its
	/// price is the seed's. DV-6.3 families = distinct families (0..3), nRanges = distinct lattice range ids; a cluster qualifies at
	/// families &gt;= DvpConst.MinFamilies (3). KD-09's "&gt;= 2 ranges" is not applied here. DV-6.5 score = families; score077 is
	/// report-only (journal) and never gates, ranks or selects anything.
	/// </summary>
	public sealed class DvpConfluence
	{
		public const int FamManifold = 0, FamSigma = 1, FamLattice = 2;
		public readonly double[] CandPrice; public readonly int[] CandFam, CandRange, CandCluster; public readonly ELevelKind[] CandKind;
		public int CandCount;
		/// <summary>Candidates [0, RailCount) are rails.Items in order; lattice level i is candidate RailCount + i (the dvpRails publish order).</summary>
		public int RailCount { get; private set; }
		public readonly double[] ClusterPrice, ClusterScore077; public readonly int[] ClusterFamilies, ClusterRanges, ClusterSize, ClusterSeed;
		public int ClusterCount;
		public double Atr { get; private set; }
		public double Tol { get; private set; }
		readonly bool[] m_claimed;

		public DvpConfluence(int capacity)
		{
			capacity = Math.Max(1, capacity);
			CandPrice = new double[capacity]; CandFam = new int[capacity]; CandRange = new int[capacity]; CandCluster = new int[capacity]; CandKind = new ELevelKind[capacity];
			ClusterPrice = new double[capacity]; ClusterScore077 = new double[capacity]; ClusterFamilies = new int[capacity]; ClusterRanges = new int[capacity];
			ClusterSize = new int[capacity]; ClusterSeed = new int[capacity]; m_claimed = new bool[capacity];
		}

		/// <summary>DV-6.1 family of a level kind: 0 manifold, 1 sigma, 2 lattice, -1 not a candidate (DvpConf, cone, non-DVP kinds).</summary>
		public static int Family(ELevelKind k)
		{
			if (k == ELevelKind.DvpPoc || k == ELevelKind.DvpVah || k == ELevelKind.DvpVal || k == ELevelKind.DvpHvn || k == ELevelKind.DvpLvn) return FamManifold;
			if (k == ELevelKind.DvpSigma) return FamSigma;
			if (k == ELevelKind.DvpFib) return FamLattice;
			return -1;
		}

		public void Clear() { CandCount = 0; RailCount = 0; ClusterCount = 0; }

		/// <summary>Appends one candidate (range = lattice range id, ignored for other families). False when full or not a candidate kind.</summary>
		public bool Add(ELevelKind kind, double price, int range)
		{
			int fam = Family(kind);
			if (fam < 0 || CandCount >= CandPrice.Length || double.IsNaN(price)) return false;
			CandKind[CandCount] = kind; CandPrice[CandCount] = price; CandFam[CandCount] = fam; CandRange[CandCount] = fam == FamLattice ? range : -1;
			CandCluster[CandCount] = -1; CandCount++;
			return true;
		}

		/// <summary>DV-6.1: loads the snapshot's rails then its lattice levels (lat may be null: useFib off or no ranges).</summary>
		public void Load(DvpRails rails, DvpLattice lat)
		{
			Clear();
			if (rails != null) for (int i = 0; i < rails.Count; i++)   // spec rev 5.1 DV-6.3/6.5 (S4): every rail occupies a slot in publish order; priceless = non-candidate slot; 1:1 with quality
					if (!Add(rails.Items[i].kind, rails.Items[i].price, -1) && CandCount < CandPrice.Length)
					{ CandKind[CandCount] = rails.Items[i].kind; CandPrice[CandCount] = rails.Items[i].price; CandFam[CandCount] = -1; CandRange[CandCount] = -1; CandCluster[CandCount] = -1; CandCount++; }
			RailCount = CandCount;
			if (lat != null) for (int i = 0; i < lat.Count; i++) Add(ELevelKind.DvpFib, lat.Price[i], lat.Range[i]);
		}

		static bool Within(double a, double b, double tol) { return Math.Abs(a - b) <= tol * (1 + 1e-12); }   // inclusive (DV-T18e), rounding-safe
		static int Bits(int m) { int c = 0; while (m != 0) { c += m & 1; m >>= 1; } return c; }
		static int RangeBit(int r) { return r >= 0 && r < 31 ? 1 << r : 0; }

		public void Cluster(double atr, double cPrev, double confTol) { Cluster(atr, cPrev, confTol, DvpConst.ReportWeightVa, DvpConst.ReportWeightPoc); }

		/// <summary>DV-6.2/6.3/6.5. wVa/wPoc feed score077 only (DV-T18c: they never change families or the cluster set). No allocation.</summary>
		public void Cluster(double atr, double cPrev, double confTol, double wVa, double wPoc)
		{
			ClusterCount = 0; Atr = atr; Tol = confTol * atr;
			for (int i = 0; i < CandCount; i++) { m_claimed[i] = CandFam[i] < 0; CandCluster[i] = -1; }   // non-candidate slots never cluster
			if (!(atr > 0) || !(confTol > 0) || double.IsInfinity(Tol)) return;
			double tol = Tol;
			while (true)
			{
				int seed = -1, bF = -1, bK = -1; double bD = 0, bP = 0;
				for (int c = 0; c < CandCount; c++)
				{
					if (m_claimed[c]) continue;
					int mask = 0, k = 0; double pc = CandPrice[c];
					for (int j = 0; j < CandCount; j++)
						if (!m_claimed[j] && Within(CandPrice[j], pc, tol)) { mask |= 1 << CandFam[j]; k++; }
					int f = Bits(mask); double d = Math.Abs(pc - cPrev);
					bool better = seed < 0 || f > bF || (f == bF && (k > bK || (k == bK && (d < bD || (d == bD && pc < bP)))));
					if (better) { seed = c; bF = f; bK = k; bD = d; bP = pc; }
				}
				if (seed < 0) break;
				int cl = ClusterCount++, fm = 0, rm = 0, n = 0; double ps = CandPrice[seed], wMan = double.NaN;
				for (int j = 0; j < CandCount; j++)
				{
					if (m_claimed[j] || !Within(CandPrice[j], ps, tol)) continue;
					m_claimed[j] = true; CandCluster[j] = cl; n++;
					fm |= 1 << CandFam[j];
					if (CandFam[j] == FamLattice) rm |= RangeBit(CandRange[j]);
					else if (CandFam[j] == FamManifold)
					{
						ELevelKind k = CandKind[j];
						double w = k == ELevelKind.DvpPoc ? wPoc : k == ELevelKind.DvpVah || k == ELevelKind.DvpVal ? wVa : 1.0;   // every other kind 1.00
						if (double.IsNaN(wMan) || w > wMan) wMan = w;
					}
				}
				ClusterPrice[cl] = ps; ClusterSeed[cl] = seed; ClusterSize[cl] = n; ClusterFamilies[cl] = Bits(fm); ClusterRanges[cl] = Bits(rm);
				ClusterScore077[cl] = (double.IsNaN(wMan) ? 0 : wMan) + ((fm & (1 << FamSigma)) != 0 ? 1 : 0) + (Bits(rm) >= 1 ? 1 : 0);
			}
		}

		public bool Qualifies(int cl) { return cl >= 0 && cl < ClusterCount && ClusterFamilies[cl] >= DvpConst.MinFamilies; }
		public int QualifyingCount { get { int q = 0; for (int i = 0; i < ClusterCount; i++) if (Qualifies(i)) q++; return q; } }

		/// <summary>DV-6.5: families of the cluster holding candidate i (the rail's score; LevelRef.quality), or 0 when unclustered.</summary>
		public int CandFamilies(int i) { int cl = i >= 0 && i < CandCount ? CandCluster[i] : -1; return cl >= 0 ? ClusterFamilies[cl] : 0; }
		public int CandRanges(int i) { int cl = i >= 0 && i < CandCount ? CandCluster[i] : -1; return cl >= 0 ? ClusterRanges[cl] : 0; }
		public double CandScore077(int i) { int cl = i >= 0 && i < CandCount ? CandCluster[i] : -1; return cl >= 0 ? ClusterScore077[cl] : double.NaN; }

		/// <summary>DV-6.4 point query: distinct families and lattice ranges among the loaded candidates within tolAtr*Atr of price
		/// (inclusive), without clustering. 0/0 when ATR is not usable.</summary>
		public void FamiliesAt(double price, double tolAtr, out int families, out int nRanges)
		{
			families = 0; nRanges = 0;
			double tol = tolAtr * Atr;
			if (!(Atr > 0) || !(tolAtr >= 0) || double.IsNaN(price)) return;
			int fm = 0, rm = 0;
			for (int j = 0; j < CandCount; j++)
			{
				if (CandFam[j] < 0 || !Within(CandPrice[j], price, tol)) continue;
				fm |= 1 << CandFam[j];
				if (CandFam[j] == FamLattice) rm |= RangeBit(CandRange[j]);
			}
			families = Bits(fm); nRanges = Bits(rm);
		}

		/// <summary>DV-10.2-style DvpConf tag: DVP{ds}:{t-1}:d{deg}:DvpConf{z:+0.00} (z of the cluster price).</summary>
		public static string Tag(int ds, DateTime tPrev, int deg, double z) { return DvpRails.Tag(ds, tPrev, deg, ELevelKind.DvpConf, z); }

		/// <summary>DV-6.3: qualifying clusters as DvpConf LevelRefs (quality = families) into a caller-owned list, pooled objects.
		/// Query-side only (tag strings allocate).</summary>
		public void Publish(List<LevelRef> into, List<LevelRef> pool, int ds, DateTime tPrev, DateTime t0, DvpRails rails, bool isRollWindow)
		{
			into.Clear();
			for (int cl = 0; cl < ClusterCount; cl++)
			{
				if (!Qualifies(cl)) continue;
				int k = into.Count; LevelRef r;
				if (k < pool.Count) r = pool[k]; else { r = new LevelRef(); pool.Add(r); }
				double p = ClusterPrice[cl];
				r.kind = ELevelKind.DvpConf; r.price = r.lo = r.hi = p; r.createdTime = tPrev; r.sourceT0 = t0; r.sourceT1 = tPrev; r.dataStream = ds;
				r.tag = Tag(ds, tPrev, rails != null ? rails.Deg : -1, rails != null ? rails.Z(p) : double.NaN); r.quality = ClusterFamilies[cl];
				r.crossesRoll = false; r.isRollWindow = isRollWindow;
				into.Add(r);
			}
		}
	}

	/// <summary>
	/// DV-8.2/8.3 acceptance primitives over one rail's history, newest first: k = 0 is the newest closed bar L, c[k] its close,
	/// p[k] its OWN rail price (the snapshot that was live during bar k), a[k] the ATR known at k-1. A NaN p (no fit, skipped
	/// window, rail absent) breaks every run. Pure and allocation-free; the engine has no counting default (each consumer passes
	/// countTrigger and deltaAtr).
	/// </summary>
	public static class DvpAccept
	{
		public const double Delta = 0.10;   // DV-1.4 / DV-8.2 trigger buffer, ATR
		public const int MaxN = 6;          // DV-8.1: trigger + 6 further closes + the pre-trigger bar = 8 snapshots

		static bool Ok(double[] c, double[] p, double[] a, int len, int k) { return k >= 0 && k < len && !double.IsNaN(c[k]) && !double.IsNaN(p[k]) && a[k] > 0; }

		/// <summary>C_k beyond its own rail by more than deltaAtr*ATR (strict; deltaAtr = 0 is closedThrough).</summary>
		public static bool Beyond(double[] c, double[] p, double[] a, int len, int k, int dir, double deltaAtr)
		{
			return Ok(c, p, a, len, k) && dir * (c[k] - p[k]) > deltaAtr * a[k];
		}

		/// <summary>DV-8.2: trigger at k: C_k beyond P_k + Delta*ATR and C_{k+1} not beyond P_{k+1} (no delta on the earlier bar).</summary>
		public static bool Triggered(double[] c, double[] p, double[] a, int len, int k, int dir)
		{
			if (!Beyond(c, p, a, len, k, dir, Delta) || k + 1 >= len || double.IsNaN(c[k + 1]) || double.IsNaN(p[k + 1])) return false;
			return dir * (c[k + 1] - p[k + 1]) <= 0;
		}

		/// <summary>DV-8.3: consecutive closed bars ending at L, each beyond its own rail + deltaAtr*ATR (saturates at len).</summary>
		public static int RunBeyond(double[] c, double[] p, double[] a, int len, int dir, double deltaAtr)
		{
			int r = 0;
			while (r < len && Beyond(c, p, a, len, r, dir, deltaAtr)) r++;
			return r;
		}

		/// <summary>DV-8.3: since the most recent trigger in the history (k &gt;= 1), some later close came back inside its own rail by
		/// more than Delta*ATR. False when there is no trigger in the history.</summary>
		public static bool BackInside(double[] c, double[] p, double[] a, int len, int dir)
		{
			for (int k = 1; k < len - 1; k++)
			{
				if (!Triggered(c, p, a, len, k, dir)) continue;
				for (int j = 0; j < k; j++) if (Ok(c, p, a, len, j) && dir * (c[j] - p[j]) < -Delta * a[j]) return true;
				return false;
			}
			return false;
		}

		/// <summary>
		/// DV-8.3 Accepted, true on the bar the run completes. countTrigger = true: the trigger plus N further closes (N + 1 bars,
		/// trigger first); false: N closes in total with the trigger first. Every bar of the run closes beyond its own rail +
		/// deltaAtr*ATR. Throws for N outside 1..6 (DV-8.1 assert).
		/// </summary>
		public static bool Accepted(double[] c, double[] p, double[] a, int len, int dir, int N, bool countTrigger, double deltaAtr)
		{
			if (N < 1 || N > MaxN) throw new ArgumentOutOfRangeException("N", "DV-8.1: N must be 1.." + MaxN);
			int m = countTrigger ? N + 1 : N;
			if (!Triggered(c, p, a, len, m - 1, dir)) return false;
			for (int k = 0; k < m; k++) if (!Beyond(c, p, a, len, k, dir, deltaAtr)) return false;
			return true;
		}
	}

	/// <summary>
	/// DV-8.1 rail history: a ring of the last 8 snapshots' rails per stream, keyed by window end bar. Bar ring[k]'s own rail comes
	/// from the snapshot ending ring[k+1] (k = 0..7, endAgo 1..8; RingNeed covers endAgo 8). Lazy: an entry is copied only when
	/// queried, normally one new snapshot per bar. Load fills C/P/A (newest first) for one rail, matched per snapshot by kind:
	/// POC/VAH/VAL unique; sigma by z (|dz| &lt;= 1e-6); HVN/LVN the same-kind rail nearest in z. Lattice (DvpFib) and DvpConf
	/// levels have no history here (acceptance is on manifold and sigma rails, DV-10.4).
	/// </summary>
	public sealed class DvpRailHistory
	{
		public const int Depth = 8;
		readonly DvpStream m_s;
		readonly int[] m_key, m_count; readonly bool[] m_valid; readonly double[] m_atr, m_yhat, m_sigma; readonly DvpRail[][] m_items;
		public readonly double[] C = new double[Depth], P = new double[Depth], A = new double[Depth];
		public int Len;
		public long Copied { get; private set; }   // entries copied (laziness counter)

		public DvpRailHistory(DvpStream s, int maxRails)
		{
			m_s = s;
			m_key = new int[Depth]; m_count = new int[Depth]; m_valid = new bool[Depth]; m_atr = new double[Depth]; m_yhat = new double[Depth]; m_sigma = new double[Depth];
			m_items = new DvpRail[Depth][];
			for (int i = 0; i < Depth; i++) { m_items[i] = new DvpRail[maxRails]; m_key[i] = int.MinValue; }
		}

		public void Reset() { for (int i = 0; i < Depth; i++) m_key[i] = int.MinValue; }

		static int Mod(int a, int m) { int x = a % m; return x < 0 ? x + m : x; }

		/// <summary>Slot of bar ring[k]'s own rails (snapshot ending ring[k+1]), or -1 when outside the ring.</summary>
		public int Entry(_BarRing ring, int k)
		{
			int endAgo = k + 1;
			if (ring == null || k < 0 || k >= Depth || ring.Count <= endAgo) return -1;
			int key = ring[endAgo].bar, slot = Mod(key, Depth);
			if (m_key[slot] == key) return slot;
			DvpRails rails;
			DvpSnapshot snap = m_s.HistSnapshot(ring, endAgo, out rails);
			if (snap == null) return -1;
			m_key[slot] = key; Copied++;
			m_valid[slot] = snap.valid && rails != null; m_atr[slot] = snap.atr; m_count[slot] = 0;
			m_yhat[slot] = m_sigma[slot] = double.NaN;
			if (m_valid[slot])
			{
				m_yhat[slot] = rails.YhatT; m_sigma[slot] = rails.Sigma;
				int n = Math.Min(rails.Count, m_items[slot].Length);
				for (int i = 0; i < n; i++) m_items[slot][i] = rails.Items[i];
				m_count[slot] = n;
			}
			return slot;
		}

		/// <summary>Index of the rail matching (kind, z) in items[0..n): POC/VAH/VAL by kind; sigma by |dz| &lt;= 1e-6; HVN/LVN the
		/// same-kind rail nearest in z provided |dz| &lt;= NodeZTol (so nodes that swap order still match by z, not position); a
		/// tracked node absent from a snapshot (not yet formed, merged) is -1 there, never a far node of the same kind. -1 if none.</summary>
		public const double NodeZTol = 0.25;   // spec rev 5.1 DV-8.1 (S1, matching): HVN/LVN |dz| <= 0.25, else absent (NaN)
		public static int Match(DvpRail[] it, int n, ELevelKind kind, double z)
		{
			bool unique = kind == ELevelKind.DvpPoc || kind == ELevelKind.DvpVah || kind == ELevelKind.DvpVal;
			bool node = kind == ELevelKind.DvpHvn || kind == ELevelKind.DvpLvn;
			int best = -1; double bd = double.MaxValue;
			for (int i = 0; i < n; i++)
			{
				if (it[i].kind != kind) continue;
				if (unique) return i;
				double dz = Math.Abs(it[i].z - z);
				if (node) { if (dz < bd) { bd = dz; best = i; } }
				else if (dz <= 1e-6) return i;
			}
			return best >= 0 && bd <= NodeZTol ? best : -1;
		}

		/// <summary>Own rail price of kind/z in a slot, or NaN.</summary>
		public double PriceIn(int slot, ELevelKind kind, double z)
		{
			if (slot < 0 || !m_valid[slot]) return double.NaN;
			int i = Match(m_items[slot], m_count[slot], kind, z);
			return i >= 0 ? m_items[slot][i].price : double.NaN;
		}

		/// <summary>Fills C/P/A (k = 0 newest closed bar ring[0]) for one rail; Len = bars available (&lt;= 8). No allocation.</summary>
		public int Load(_BarRing ring, ELevelKind kind, double z)
		{
			Len = 0;
			for (int k = 0; k < Depth; k++) { C[k] = P[k] = A[k] = double.NaN; }
			if (ring == null || ring.Count == 0 || DvpConfluence.Family(kind) < 0 || kind == ELevelKind.DvpFib) return 0;
			for (int k = Depth - 1; k >= 0; k--)   // oldest first, so the last copy is the newest (cache-friendly)
			{
				int slot = Entry(ring, k);
				if (slot < 0) continue;
				if (k + 1 > Len) Len = k + 1;
				C[k] = ring[k].c; P[k] = PriceIn(slot, kind, z); A[k] = m_atr[slot];
			}
			return Len;
		}

		/// <summary>RG-10 primitive: z of bar ring[k]'s close against its own snapshot, (C_k - yhat_k(u)) / sigma_k; NaN without a fit.</summary>
		public double ZClose(_BarRing ring, int k)
		{
			int slot = Entry(ring, k);
			return slot >= 0 && m_valid[slot] && m_sigma[slot] > 0 ? (ring[k].c - m_yhat[slot]) / m_sigma[slot] : double.NaN;
		}
	}

	/// <summary>One DVP RAIL interaction (DV-11.2), plus its 40-bar forward tracker state.</summary>
	public struct DvpRailEvent
	{
		public ELevelKind kind; public double z, price, close, atr, distAtr, score077, sigmaAtr, mfe, mae;
		public int dir, families, nRanges, heldBars, deg, elapsed;
		public bool trigger, closedThrough, acc2, acc3, acc5, isRollWindow, halfDay, active, accepted41, back41;
		public DateTime time, tPrev;
	}

	/// <summary>
	/// DV-11.2 DVP RAIL journal (journal-side, only when journalRails and a journal sink exist): on every closed bar, each manifold
	/// and sigma rail is tested both ways for a trigger and for acceptance under the PS-25 reading (countTrigger = true,
	/// deltaAtr = 0, N = 2/3/5). Each interaction is tracked for HOLD_BARS = 40 bars (MFE40/MAE40 in ATR from the event close) and
	/// written when the clock completes (or at Finish, complete(0)). verdict41 v0 (journal field verdict41v0) [GAP stand-in, pre-registered]: on the 41-bar clock
	/// (event bar + 40), trigger events only: "accepted" if Accepted(N = 2, countTrigger = true, deltaAtr = 0) completes on the event's
	/// OWN trigger (2 bars after the event) before any close back inside its own rail by &gt; 0.10 ATR, "failed" if such a close comes
	/// first, else "held". Accept events journal verdict41v0(NaN). Provisional, journal-only, never gates (spec rev 5.1 DV-8.4/DV-11.2 S3). Events of the current bar are exposed for the host's logSignal.
	/// </summary>
	public sealed class DvpRailJournal
	{
		public const int MaxTracked = 512;
		readonly DvpRailEvent[] m_track = new DvpRailEvent[MaxTracked];
		public readonly DvpRailEvent[] Events;
		public int EventCount;
		public long Written { get; private set; }
		public long Dropped { get; private set; }   // tracker pool full: written at once with complete(0)
		int m_lastBar = int.MinValue;

		public DvpRailJournal(int maxRails) { Events = new DvpRailEvent[Math.Max(1, 2 * maxRails)]; }

		static string F(double x) { return x.ToString("R", CultureInfo.InvariantCulture); }
		static string I(long x) { return x.ToString(CultureInfo.InvariantCulture); }

		public static string Line(DvpRailEvent e, int ds, string runId, bool complete)
		{
			var sb = new StringBuilder(384);
			string verdict = !e.trigger ? "NaN" : e.accepted41 ? "accepted" : e.back41 ? "failed" : complete ? "held" : "NaN";
			sb.Append("DVP RAIL runId(").Append(runId).Append(") stream(Data").Append(I(ds)).Append(") barTime(").Append(e.time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
				.Append(") tag(").Append(DvpRails.Tag(ds, e.tPrev, e.deg, e.kind, e.z)).Append(") event(").Append(e.trigger ? "trigger" : "accept").Append(") dir(").Append(e.dir > 0 ? "+1" : "-1")
				.Append(") railKind(").Append(e.kind.ToString()).Append(") z(").Append(F(e.z)).Append(") price(").Append(F(e.price)).Append(") families(").Append(I(e.families))
				.Append(") nRanges(").Append(I(e.nRanges)).Append(") score(").Append(I(e.families)).Append(") score077(").Append(F(e.score077)).Append(") distAtr(").Append(F(e.distAtr))
				.Append(") closedThrough(").Append(e.closedThrough ? '1' : '0').Append(") heldBars(").Append(I(e.heldBars)).Append(") accepted(N2=").Append(e.acc2 ? '1' : '0')
				.Append(",N3=").Append(e.acc3 ? '1' : '0').Append(",N5=").Append(e.acc5 ? '1' : '0').Append(") verdict41v0(").Append(verdict).Append(") isRollWindow(").Append(e.isRollWindow ? '1' : '0')
				.Append(") halfDay(").Append(e.halfDay ? '1' : '0').Append(") mfe40(").Append(F(e.atr > 0 && e.elapsed > 0 ? e.mfe / e.atr : double.NaN))
				.Append(") mae40(").Append(F(e.atr > 0 && e.elapsed > 0 ? e.mae / e.atr : double.NaN)).Append(") bars(").Append(I(e.elapsed)).Append(") complete(").Append(complete ? '1' : '0').Append(')');
			return sb.ToString();
		}

		/// <summary>
		/// One closed bar (ring[0] is new; rails = its own rails, conf loaded and clustered on the same snapshot). Advances the
		/// trackers, then records this bar's interactions. Returns EventCount. journal may be null (events only).
		/// </summary>
		public int Step(DvpStream s, _BarRing ring, DvpSnapshot snap, DvpRails rails, DvpConfluence conf, bool isRollWindow, bool halfDay, Action<string> journal, string runId)
		{
			EventCount = 0;
			if (ring == null || ring.Count == 0 || ring[0].bar == m_lastBar) return 0;
			m_lastBar = ring[0].bar;
			RingBar b = ring[0];
			DvpRailHistory h = s.History;
			for (int i = 0; i < MaxTracked; i++)
			{
				if (!m_track[i].active) continue;
				m_track[i].elapsed++;
				double fav = m_track[i].dir > 0 ? b.h - m_track[i].close : m_track[i].close - b.l;
				double adv = m_track[i].dir > 0 ? b.l - m_track[i].close : m_track[i].close - b.h;
				if (fav > m_track[i].mfe) m_track[i].mfe = fav;
				if (adv < m_track[i].mae) m_track[i].mae = adv;
				if (m_track[i].trigger && !m_track[i].accepted41 && !m_track[i].back41 && h.Load(ring, m_track[i].kind, m_track[i].z) > 0)
				{
					int dr = m_track[i].dir;   // back inside its own rail by more than Delta*ATR, at any bar of the clock
					if (!double.IsNaN(h.P[0]) && h.A[0] > 0 && dr * (h.C[0] - h.P[0]) < -DvpAccept.Delta * h.A[0]) m_track[i].back41 = true;
					else if (m_track[i].elapsed == 2 && DvpAccept.Accepted(h.C, h.P, h.A, h.Len, m_track[i].dir, 2, true, 0)) m_track[i].accepted41 = true;   // on the event's own trigger only
				}
				if (m_track[i].elapsed >= DvpConst.HoldBars)
				{
					if (journal != null) journal(Line(m_track[i], s.Ds, runId, true));
					m_track[i].active = false; Written++;
				}
			}
			if (snap == null || !snap.valid || rails == null) return 0;
			for (int i = 0; i < rails.Count; i++)
			{
				DvpRail r = rails.Items[i];
				if (h.Load(ring, r.kind, r.z) == 0) continue;
				for (int dir = 1; dir >= -1; dir -= 2)
				{
					bool trig = DvpAccept.Triggered(h.C, h.P, h.A, h.Len, 0, dir);
					bool a2 = DvpAccept.Accepted(h.C, h.P, h.A, h.Len, dir, 2, true, 0), a3 = DvpAccept.Accepted(h.C, h.P, h.A, h.Len, dir, 3, true, 0),
						a5 = DvpAccept.Accepted(h.C, h.P, h.A, h.Len, dir, 5, true, 0);
					if (!trig && !a2 && !a3 && !a5) continue;
					DvpRailEvent e = new DvpRailEvent();
					e.kind = r.kind; e.z = r.z; e.price = r.price; e.close = b.c; e.atr = snap.atr; e.dir = dir; e.trigger = trig;
					e.distAtr = snap.atr > 0 ? (b.c - r.price) / snap.atr : double.NaN;
					e.closedThrough = DvpAccept.Beyond(h.C, h.P, h.A, h.Len, 0, dir, 0);
					e.heldBars = DvpAccept.RunBeyond(h.C, h.P, h.A, h.Len, dir, 0);
					e.acc2 = a2; e.acc3 = a3; e.acc5 = a5;
					e.families = conf != null ? conf.CandFamilies(i) : 0; e.nRanges = conf != null ? conf.CandRanges(i) : 0;
					e.score077 = conf != null ? conf.CandScore077(i) : double.NaN;
					e.sigmaAtr = snap.sigmaAtr; e.deg = rails.Deg; e.isRollWindow = isRollWindow; e.halfDay = halfDay;
					e.time = b.time; e.tPrev = snap.tPrev; e.active = true;
					if (EventCount < Events.Length) Events[EventCount++] = e;
					int slot = -1;
					for (int t = 0; t < MaxTracked; t++) if (!m_track[t].active) { slot = t; break; }
					if (slot >= 0) m_track[slot] = e;
					else { Dropped++; Written++; if (journal != null) journal(Line(e, s.Ds, runId, false)); }
				}
			}
			return EventCount;
		}

		/// <summary>End of run: writes every unfinished tracker with complete(0).</summary>
		public void Finish(int ds, Action<string> journal, string runId)
		{
			for (int i = 0; i < MaxTracked; i++)
			{
				if (!m_track[i].active) continue;
				if (journal != null) journal(Line(m_track[i], ds, runId, false));
				m_track[i].active = false; Written++;
			}
		}
	}
	// </dvp-core>

	// <dvp-pa>
	/// <summary>
	/// PR-D4 (DV-5.1) bridge from a hosted _PriceActionEngine (layer 0) to a DvpPivots buffer. PA bar numbers are the stream's
	/// CurrentBar; the ring (and DvpSnapshot.endBar) uses FullSymbolData.Current, so barOffset = ring bar - PA bar of the same bar.
	/// Only pivots confirmed by endBar are copied (the anchor rule re-checks this). No allocation.
	/// </summary>
	internal static class DvpPaBridge
	{
		public static void Fill(_PriceActionEngine pa, int barOffset, int endBar, DvpPivots into)
		{
			into.Clear();
			if (pa == null) return;
			side(pa, true, barOffset, endBar, into);
			side(pa, false, barOffset, endBar, into);
		}
		static void side(_PriceActionEngine pa, bool isHigh, int off, int endBar, DvpPivots into)
		{
			int n = pa.PivotCount(0, isHigh);
			for (int ago = 0; ago < n; ago++)
			{
				_Pivot p = isHigh ? pa.High(0, ago) : pa.Low(0, ago);
				if (p == null) break;
				if (p.confirmBar + off > endBar) continue;
				if (!into.Add(isHigh, p.price, p.extremeBar + off, p.confirmBar + off)) return;
			}
		}
	}
	// </dvp-pa>
}
