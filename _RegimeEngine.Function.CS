using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 §3.3 — G-R market regime engine (pure managed C#, MC-free; tests/RegimeTests compile this file as is).
	// Daily features from M-DAILY's roll-adjusted RTH closes (S2): f1 = ln(rc_t / rc_t-1), f2 = ln(HV20_t),
	// f3 = ln(rc_t / rc_t-63) / (HV63_t x sqrt(63/252)), optional f4 = ln(Parkinson sigma of the last 10 daily bars) (off).
	// Z-scored with the fit window's mean / SD (frozen until the next refit). Gaussian HMM, full covariance, K = 3, Baum-Welch with
	// Rabiner scaling, initialised from the §3.1 _Gmm fit (10 restarts) and A = 0.9 diagonal; or method Gmm (i.i.d. days).
	// Walk-forward: trailing 1000 valid days (expanding from 500), refit after the close of the first trading day of each month
	// (never on a roll day: deferred to the next close), new parameters apply from the NEXT observation, filter re-warmed over the
	// last 63 observations. Only FILTERED posteriors P(s_t | x_1..x_t) are ever published; smoothed gamma stays inside the fit.
	// Labels ordered by the f2 mean (ties f3), Bhattacharyya cross-refit check (relabel-conflict journal), hysteresis 0.60 / 0.10.
	// Fallback AtrTercile (RG-03) or Unknown. A failed refit keeps the previous parameters.
	//
	// USE RULE (analyst ruling, phase3-review-analyst2 / Q2): the G-R output is a PRIOR / BUCKET for consumers (stratifier,
	// sizing bucket, pm.regime), NEVER a direct entry trigger. Nothing may consume Regime until that consumer is pre-registered
	// with a kill rule measured against an AtrTercile (RG-03) baseline. Keller ranks a causal regime variable (e.g. the ATR
	// tercile) above a fitted HMM label, hence the mandatory baseline comparison: the HMM must beat the causal control or die.
	// ============================================================================================

	// <regime>
	public enum ERegimeMethod { Hmm = 0, Gmm = 1 }
	public enum ERegimeKSelect { Fixed = 0, Bic = 1 }
	public enum ERegimeRefit { Monthly = 0, EveryNSessions = 1, Quarterly = 2 }
	public enum ERegimeFallback { AtrTercile = 0, Unknown = 1 }
	public enum ERegimeFeature { Return = 0, LogHv20 = 1, Tsmom63z = 2, Parkinson10 = 3 }

	/// <summary>Protected RegimeConfig on _CustomSignal (no [Input]). Spec §3.3 defaults.</summary>
	public sealed class RegimeConfig
	{
		public bool enabled = false;
		public ERegimeMethod method = ERegimeMethod.Hmm;
		public int K = 3;
		public ERegimeKSelect kSelect = ERegimeKSelect.Fixed;   // Bic (K = 2..4) is research only: K changing breaks label continuity
		public ERegimeFeature[] features = { ERegimeFeature.Return, ERegimeFeature.LogHv20, ERegimeFeature.Tsmom63z };   // add Parkinson10 for f4
		public int fitSessions = 1000;
		public int minSessions = 500;
		public ERegimeRefit refit = ERegimeRefit.Monthly;
		public int refitEveryN = 21;            // EveryNSessions: valid observations between refits
		public int restarts = 10;
		public int maxIter = 200;
		public double tol = 1e-6;               // per-sample lnL change (_Gmm tolMode PerSample)
		public double regCovar = 1e-6;
		public double transPrior = 1;
		public int warmFilter = 63;
		public double switchProb = 0.60;
		public double switchMargin = 0.10;
		public ulong seed = 20261021;
		public ERegimeFallback fallback = ERegimeFallback.AtrTercile;
		public bool publish = false;
		// ---- spec-silent knobs (documented choices) ----
		public int atrPctLookback = 250;        // RG-03: ATR20d percentile over the previous 250 days
		public int parkinsonBars = 10;          // f4
		public bool bicDiagnostic = true;       // journal BIC for K = 2 and K = 4 at each refit (diagnostic only, never switches K)
		public double rollSuspectZ = 5;         // |z(f1)| on a roll day above this journals "roll adjust suspect"
		public int gapBusinessDays = 4;         // a longer gap is kept as one HMM step and journaled (Q11)

		public int D { get { return features == null ? 0 : features.Length; } }

		public string Validate()
		{
			var e = new List<string>();
			if (K < 1 || K > 4) e.Add("K=" + K);
			if (features == null || features.Length < 1 || features.Length > _Gmm.MaxD) e.Add("features (1.." + _Gmm.MaxD + ")");
			else { var seen = new HashSet<ERegimeFeature>(); foreach (var f in features) if (!seen.Add(f)) e.Add("duplicate feature " + f); }
			if (minSessions < 10) e.Add("minSessions=" + minSessions);
			if (fitSessions < minSessions) e.Add("fitSessions=" + fitSessions + " < minSessions");
			if (refitEveryN < 1) e.Add("refitEveryN=" + refitEveryN);
			if (restarts < 1) e.Add("restarts=" + restarts);
			if (maxIter < 1) e.Add("maxIter=" + maxIter);
			if (!(tol > 0)) e.Add("tol=" + tol);
			if (!(regCovar >= 0)) e.Add("regCovar=" + regCovar);
			if (!(transPrior >= 0)) e.Add("transPrior=" + transPrior);
			if (warmFilter < 1) e.Add("warmFilter=" + warmFilter);
			if (!(switchProb >= 0 && switchProb <= 1)) e.Add("switchProb=" + switchProb);
			if (!(switchMargin >= 0 && switchMargin <= 1)) e.Add("switchMargin=" + switchMargin);
			if (atrPctLookback < 3) e.Add("atrPctLookback=" + atrPctLookback);
			if (parkinsonBars < 2) e.Add("parkinsonBars=" + parkinsonBars);
			return e.Count == 0 ? null : "RegimeConfig invalid: " + string.Join(", ", e);
		}

		public RegimeConfig Copy() { var c = (RegimeConfig)MemberwiseClone(); c.features = features == null ? null : (ERegimeFeature[])features.Clone(); return c; }

		public int IndexOf(ERegimeFeature f) { if (features != null) for (int i = 0; i < features.Length; i++) if (features[i] == f) return i; return -1; }
	}

	/// <summary>Published regime state (a copy on every read; arrays are never shared with the engine).</summary>
	public sealed class RegimeState
	{
		public bool Valid;
		public int Label = -1;          // 0..K-1 (0 = calmest), -1 when invalid
		public int RawArgmax = -1;      // argmax of Probs, no hysteresis
		public double[] Probs = new double[0];
		public DateTime AsOf = DateTime.MinValue;          // the day whose close produced this value; usable from the NEXT session
		public DateTime FittedThrough = DateTime.MinValue; // close of the refit whose parameters produced Probs (always < AsOf)
		public int K;
		public double LogLik = double.NaN;
		public int Refits;
		public bool UsingFallback;
		public int RelabelConflicts;    // model-health metric

		public RegimeState Copy() { var s = (RegimeState)MemberwiseClone(); s.Probs = Probs == null ? new double[0] : (double[])Probs.Clone(); return s; }

		public static RegimeState Neutral(int K) { return new RegimeState { K = K }; }
	}

	/// <summary>One fitted model in z-space plus its z-score stats and diagnostics.</summary>
	public sealed class RegimeFit
	{
		public ERegimeMethod method;
		public int K, D;
		public double[] pi;             // HMM pi0 (= gamma_1), GMM weights
		public double[,] A;             // HMM only (null for Gmm)
		public double[][] mu;
		public double[][,] cov;
		public double[][,] L;           // Cholesky factors of cov
		public double[] zMean, zSd;     // fit-window stats (frozen until the next refit)
		public double logLik = double.NaN, bic = double.NaN;
		public int iters, gmmIters;
		public bool converged;
		public DateTime fittedThrough = DateTime.MinValue, windowStart = DateTime.MinValue;
		public int window;
		public string failReason;       // null on success: insufficient | nonconv | chol
		// diagnostics (G-V export): the GMM's k-means++ start of the best restart, the GMM result, the HMM start, smoothed gamma
		public GmmModel gmmInit, gmm;
		public double[] hmmInitPi; public double[,] hmmInitA; public double[][] hmmInitMu; public double[][,] hmmInitCov;
		public double[][] gamma;

		public static int ParamCount(ERegimeMethod m, int K, int D) { return _Gmm.ParamCount(K, D) + (m == ERegimeMethod.Hmm ? K * (K - 1) : 0); }

		/// <summary>Mean / covariance of state k in raw (un-standardised) feature space.</summary>
		public void Raw(int k, out double[] m, out double[,] c)
		{
			m = new double[D]; c = new double[D, D];
			for (int a = 0; a < D; a++) { m[a] = mu[k][a] * zSd[a] + zMean[a]; for (int b = 0; b < D; b++) c[a, b] = cov[k][a, b] * zSd[a] * zSd[b]; }
		}
	}

	public sealed class _RegimeEngine
	{
		public RegimeConfig config = new RegimeConfig();
		public string label = "REGIME";
		public Action<string> journal;
		public Action<string> warnOnce;
		/// <summary>Diagnostic hook (tests / G-V export): every successful refit, after relabelling, before the warm filter.</summary>
		public Action<RegimeFit> onRefit;

		private struct Obs { public DateTime date; public double[] x; }
		private readonly List<Obs> m_obs = new List<Obs>();
		private double[] m_close; private int m_closeCount, m_closeNext;
		private double[] m_hl2; private int m_hlCount, m_hlNext;
		private double[] m_atr; private int m_atrCount, m_atrNext;
		private DateTime m_lastDate = DateTime.MinValue;
		private int m_validTotal, m_sinceRefit, m_flipsSinceRefit, m_relabelConflicts, m_refits;
		private bool m_refitPending, m_insufficientLogged;
		private RegimeFit m_model;
		private double[] m_alpha;
		private RegimeState m_state = new RegimeState();
		private int m_fIdx1, m_fIdx2, m_fIdx3;

		public bool Configured { get; private set; }
		/// <summary>Copy of the latest published state.</summary>
		public RegimeState State { get { return m_state.Copy(); } }
		public RegimeFit Model { get { return m_model; } }
		public int Refits { get { return m_refits; } }
		public int RelabelConflicts { get { return m_relabelConflicts; } }
		public int ValidObservations { get { return m_validTotal; } }

		public string Configure()
		{
			string err = config.Validate();
			if (err != null) { Configured = false; return err; }
			Configured = true; Reset();
			return null;
		}

		public void Reset()
		{
			m_obs.Clear();
			m_close = new double[64]; m_closeCount = m_closeNext = 0;
			m_hl2 = new double[config.parkinsonBars]; m_hlCount = m_hlNext = 0;
			m_atr = new double[config.atrPctLookback]; m_atrCount = m_atrNext = 0;
			m_lastDate = DateTime.MinValue;
			m_validTotal = m_sinceRefit = m_flipsSinceRefit = m_relabelConflicts = m_refits = 0;
			m_refitPending = m_insufficientLogged = false;
			m_model = null; m_alpha = null;
			m_state = RegimeState.Neutral(config.K);
			m_fIdx1 = config.IndexOf(ERegimeFeature.Return);
			m_fIdx2 = config.IndexOf(ERegimeFeature.LogHv20);
			m_fIdx3 = config.IndexOf(ERegimeFeature.Tsmom63z);
		}

		private void j(string s) { journal?.Invoke(s); }
		private static string F(double v, string fmt = "0.######") { return v.ToString(fmt, CultureInfo.InvariantCulture); }
		private static string D(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

		/// <summary>
		/// One COMPLETED trading day (holidays: no call). adjClose = M-DAILY ratio-adjusted RTH close (RthCloseAdj), hv20 / hv63 = M-DAILY
		/// Hv(20) / Hv(63), high / low = the day's bar (f4), atr20d = M-DAILY Atr20d (fallback), rollDay = first day of a new contract.
		/// The resulting State is the value at this day's close, usable from the next session.
		/// </summary>
		public void AddDay(DateTime date, double adjClose, double hv20, double hv63, double high, double low, double atr20d, bool rollDay)
		{
			if (!Configured) return;
			date = date.Date;
			if (date <= m_lastDate) { warnOnce?.Invoke($"{label}: out-of-order day {D(date)} <= {D(m_lastDate)} ignored"); return; }
			bool monthStart = false;
			if (m_lastDate != DateTime.MinValue)
			{
				int gap = businessDaysBetween(m_lastDate, date);
				if (gap > config.gapBusinessDays) j($"{label} gap {D(m_lastDate)}->{D(date)} {gap} business days: kept as one HMM step");
				monthStart = date.Month != m_lastDate.Month || date.Year != m_lastDate.Year;
			}
			m_lastDate = date;

			// rings: closes (64), Parkinson ln(H/L)^2, previous ATR20d values
			if (adjClose > 0 && !double.IsInfinity(adjClose)) { m_close[m_closeNext] = adjClose; m_closeNext = (m_closeNext + 1) % m_close.Length; if (m_closeCount < m_close.Length) m_closeCount++; }
			else { m_closeCount = 0; m_closeNext = 0; }   // a broken close restarts the feature history
			double hl = high > 0 && low > 0 && high >= low ? Math.Log(high / low) : double.NaN;
			m_hl2[m_hlNext] = hl * hl; m_hlNext = (m_hlNext + 1) % m_hl2.Length; if (m_hlCount < m_hl2.Length) m_hlCount++;
			double atrPct = AtrPercentile(m_atr, m_atrNext, m_atrCount, atr20d);
			if (atr20d > 0 && !double.IsInfinity(atr20d)) { m_atr[m_atrNext] = atr20d; m_atrNext = (m_atrNext + 1) % m_atr.Length; if (m_atrCount < m_atr.Length) m_atrCount++; }

			double[] raw = features(hv20, hv63);
			if (raw != null)
			{
				m_obs.Add(new Obs { date = date, x = raw }); m_validTotal++; m_sinceRefit++;
				int keep = Math.Max(config.fitSessions, config.warmFilter) + 1;
				if (m_obs.Count > keep + 256) m_obs.RemoveRange(0, m_obs.Count - keep);
			}

			publish(date, raw, rollDay, atrPct);

			// refit schedule (after publishing: today's value used the previous parameters)
			bool due;
			switch (config.refit)
			{
				case ERegimeRefit.Quarterly: due = monthStart && (date.Month - 1) % 3 == 0; break;
				case ERegimeRefit.EveryNSessions: due = m_model == null ? m_validTotal >= config.minSessions : m_sinceRefit >= config.refitEveryN; break;
				default: due = monthStart; break;
			}
			if (due) m_refitPending = true;
			if (m_refitPending)
			{
				if (rollDay) j($"{label} refit deferred {D(date)}: roll day (refit at the next close)");
				else
				{
					m_refitPending = false;
					if (m_validTotal >= config.minSessions) refit(date);
					else if (!m_insufficientLogged) { m_insufficientLogged = true; j($"{label} fallback reason(insufficient) {D(date)} valid({m_validTotal}/{config.minSessions})"); }
				}
			}
		}

		private double[] features(double hv20, double hv63)
		{
			if (m_closeCount < 64) return null;
			double c0 = close(0), c1 = close(1), c63 = close(63);
			var x = new double[config.D];
			for (int i = 0; i < x.Length; i++)
			{
				double v;
				switch (config.features[i])
				{
					case ERegimeFeature.Return: v = Math.Log(c0 / c1); break;
					case ERegimeFeature.LogHv20: v = hv20 > 0 ? Math.Log(hv20) : double.NaN; break;
					case ERegimeFeature.Tsmom63z: v = hv63 > 0 ? Math.Log(c0 / c63) / (hv63 * Math.Sqrt(63.0 / 252.0)) : double.NaN; break;
					default: v = parkinson(); v = v > 0 ? Math.Log(v) : double.NaN; break;
				}
				if (double.IsNaN(v) || double.IsInfinity(v)) return null;
				x[i] = v;
			}
			return x;
		}

		private double close(int ago) { return m_close[(m_closeNext - 1 - ago + 2 * m_close.Length) % m_close.Length]; }

		/// <summary>Daily Parkinson sigma sqrt(sum ln(H/L)^2 / (4 ln 2 n)) over the last parkinsonBars days (not annualised).</summary>
		private double parkinson()
		{
			if (m_hlCount < m_hl2.Length) return double.NaN;
			double s = 0; foreach (double v in m_hl2) { if (double.IsNaN(v)) return double.NaN; s += v; }
			return Math.Sqrt(s / (4 * Math.Log(2) * m_hl2.Length));
		}

		private static int businessDaysBetween(DateTime a, DateTime b)
		{
			int n = 0;
			for (DateTime d = a.AddDays(1); d < b; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) n++;
			return n;
		}

		/// <summary>RG-03: mid-rank percentile (0..100) of cur among the ring's values; NaN until the ring is full or cur is not finite.</summary>
		public static double AtrPercentile(double[] ring, int next, int count, double cur)
		{
			if (ring == null || count < ring.Length || !(cur > 0) || double.IsInfinity(cur)) return double.NaN;
			int below = 0, eq = 0;
			foreach (double x in ring) { if (x < cur) below++; else if (x == cur) eq++; }
			return 100.0 * (below + 0.5 * eq) / ring.Length;
		}

		/// <summary>RG-03 tercile label for K states: min(K - 1, floor(pct x K / 100)) (K = 3: terciles 0 / 1 / 2).</summary>
		public static int AtrBucket(double pct, int K) { return double.IsNaN(pct) ? -1 : Math.Min(K - 1, Math.Max(0, (int)Math.Floor(pct * K / 100.0))); }

		/// <summary>Hysteresis on the published label: switch from L to j = argmax only if p_j &gt;= switchProb and p_j - p_L &gt;= switchMargin.</summary>
		public static int Hysteresis(int current, double[] p, double switchProb, double switchMargin)
		{
			if (p == null || p.Length == 0) return -1;
			int jm = Argmax(p);
			if (current < 0 || current >= p.Length) return jm;
			if (jm == current) return current;
			return p[jm] >= switchProb && p[jm] - p[current] >= switchMargin ? jm : current;
		}

		public static int Argmax(double[] p) { int b = 0; for (int k = 1; k < p.Length; k++) if (p[k] > p[b]) b = k; return b; }

		private void publish(DateTime date, double[] raw, bool rollDay, double atrPct)
		{
			int K = m_model != null ? m_model.K : config.K;
			double[] probs = null; bool fb = false;
			if (m_model != null)
			{
				if (raw != null)
				{
					double[] z = Standardise(m_model, raw);
					if (rollDay && m_fIdx1 >= 0 && Math.Abs(z[m_fIdx1]) > config.rollSuspectZ) j($"{label} roll adjust suspect {D(date)} z(f1)({F(z[m_fIdx1], "0.##")})");
					m_alpha = FilterStep(m_model, m_alpha, z);
				}
				if (m_alpha != null) probs = (double[])m_alpha.Clone();
			}
			else
			{
				fb = true;
				if (config.fallback == ERegimeFallback.AtrTercile)
				{
					int b = AtrBucket(atrPct, K);
					if (b >= 0) { probs = new double[K]; probs[b] = 1; }
				}
			}
			var s = new RegimeState { K = K, AsOf = date, Refits = m_refits, UsingFallback = fb, RelabelConflicts = m_relabelConflicts,
				FittedThrough = m_model != null && !fb ? m_model.fittedThrough : DateTime.MinValue, LogLik = m_model != null && !fb ? m_model.logLik : double.NaN };
			int old = m_state.Label;
			if (probs != null)
			{
				s.Valid = true; s.Probs = probs; s.RawArgmax = Argmax(probs);
				s.Label = Hysteresis(old >= K ? -1 : old, probs, config.switchProb, config.switchMargin);
			}
			if (old >= 0 && s.Label >= 0 && s.Label != old) { m_flipsSinceRefit++; j($"{label} switch {old}->{s.Label} {D(date)}"); }
			m_state = s;
			if (journal != null)
			{
				var sb = new StringBuilder();
				for (int k = 0; k < s.Probs.Length; k++) { if (k > 0) sb.Append(','); sb.Append(F(s.Probs[k], "0.0000")); }
				j($"{label} {D(date)} label({s.Label}) raw({s.RawArgmax}) p({sb}) fb({(fb ? 1 : 0)})");
			}
		}

		// ---------------- refit ----------------

		private void refit(DateTime date)
		{
			int n = Math.Min(config.fitSessions, m_obs.Count), off = m_obs.Count - n, Dm = config.D;
			var rawW = new double[n][];
			for (int i = 0; i < n; i++) rawW[i] = m_obs[off + i].x;
			double[] mean, sd; ZStats(rawW, out mean, out sd);
			var z = new double[n][];
			for (int i = 0; i < n; i++) { z[i] = new double[Dm]; for (int d = 0; d < Dm; d++) z[i][d] = (rawW[i][d] - mean[d]) / sd[d]; }

			RegimeFit fit = null;
			if (config.kSelect == ERegimeKSelect.Bic)   // research only
			{
				for (int K = 2; K <= 4; K++)
				{
					var f = FitWindow(z, K, config, SeedFor(date, K), m_fIdx2, m_fIdx3);
					if (fit == null || (fit.failReason != null && f.failReason == null) || (f.failReason == null && f.bic < fit.bic)) fit = f;
				}
			}
			else fit = FitWindow(z, config.K, config, SeedFor(date, config.K), m_fIdx2, m_fIdx3);

			// diagnostics only: BIC for K = 2 and K = 4 on the same window (never switches K)
			string bicDiag = "";
			if (config.bicDiagnostic)
				foreach (int Kd in new[] { 2, 4 })
				{
					var f = FitWindow(z, Kd, config, SeedFor(date, Kd), m_fIdx2, m_fIdx3);
					bicDiag += $" bicK{Kd}({(f.failReason == null ? F(f.bic, "0.##") : "fail-" + f.failReason)})";
				}
			string flips = $" flips({m_flipsSinceRefit}) window-since({(m_model != null ? D(m_model.fittedThrough) : "-")})";

			if (fit.failReason != null)
			{
				j($"{label} refit {D(date)} window({n}) K({fit.K}) ll({F(fit.logLik, "0.##")}) iters({fit.iters}) conv(0) means()");
				j($"{label} fallback reason({fit.failReason}) {D(date)}{(m_model != null ? " keep-previous(" + D(m_model.fittedThrough) + ")" : "")}");
				j($"{label} diag {D(date)} bicK{fit.K}(fail){bicDiag}{flips} relabelConflicts({m_relabelConflicts})");
				m_sinceRefit = 0; m_flipsSinceRefit = 0;
				return;
			}
			fit.zMean = mean; fit.zSd = sd; fit.fittedThrough = date; fit.window = n; fit.windowStart = m_obs[off].date;

			// cross-refit label check: Bhattacharyya matching (raw feature space) vs the vol ordering; the ordering decides
			if (m_model != null && m_model.K == fit.K)
			{
				int[] perm = BestMatch(m_model, fit);
				bool identity = true; for (int k = 0; k < perm.Length; k++) if (perm[k] != k) identity = false;
				if (!identity) { m_relabelConflicts++; j($"{label} relabel-conflict {D(date)} match({string.Join(",", perm)}) total({m_relabelConflicts})"); }
			}

			var means = new StringBuilder();
			for (int k = 0; k < fit.K; k++)
			{
				double[] rm; double[,] rc; fit.Raw(k, out rm, out rc);
				if (k > 0) means.Append(';');
				for (int d = 0; d < Dm; d++) { if (d > 0) means.Append(','); means.Append(F(rm[d], "0.#####")); }
			}
			j($"{label} refit {D(date)} window({n}) K({fit.K}) ll({F(fit.logLik, "0.##")}) iters({fit.iters}) conv({(fit.converged ? 1 : 0)}) means({means})");
			j($"{label} diag {D(date)} bicK{fit.K}({F(fit.bic, "0.##")}){bicDiag}{flips} relabelConflicts({m_relabelConflicts})");

			m_model = fit; m_refits++; m_sinceRefit = 0; m_flipsSinceRefit = 0;
			onRefit?.Invoke(fit);
			// warm filter: forward pass over the last warmFilter observations of the window with the new parameters
			int w = Math.Min(config.warmFilter, n);
			m_alpha = null;
			for (int i = n - w; i < n; i++) m_alpha = FilterStep(fit, m_alpha, z[i]);
		}

		private ulong SeedFor(DateTime date, int K) { return _Rng.Seed(unchecked((long)config.seed), date.Ticks, K); }

		/// <summary>Population (ddof 0) mean / SD per column; a zero SD becomes 1 (constant feature).</summary>
		public static void ZStats(double[][] x, out double[] mean, out double[] sd)
		{
			int n = x.Length, Dm = x[0].Length; mean = new double[Dm]; sd = new double[Dm];
			for (int d = 0; d < Dm; d++)
			{
				double s = 0; for (int i = 0; i < n; i++) s += x[i][d];
				double m = s / n, q = 0; for (int i = 0; i < n; i++) { double e = x[i][d] - m; q += e * e; }
				mean[d] = m; sd[d] = Math.Sqrt(q / n); if (!(sd[d] > 0)) sd[d] = 1;
			}
		}

		public static double[] Standardise(RegimeFit m, double[] raw)
		{
			var z = new double[raw.Length];
			for (int d = 0; d < raw.Length; d++) z[d] = (raw[d] - m.zMean[d]) / m.zSd[d];
			return z;
		}

		/// <summary>One filtered step: alpha_t ∝ (alpha_t-1 A) .* b(x_t) (HMM; pi when alpha is null), or w .* b(x_t) (GMM). O(K^2).</summary>
		public static double[] FilterStep(RegimeFit m, double[] alpha, double[] z)
		{
			int K = m.K; var lb = new double[K]; double mx = double.NegativeInfinity;
			for (int k = 0; k < K; k++) { lb[k] = _Gmm.LogPdf(z, m.mu[k], m.L[k], m.D); if (lb[k] > mx) mx = lb[k]; }
			var a = new double[K]; double c = 0;
			for (int k = 0; k < K; k++)
			{
				double pred;
				if (m.A == null || alpha == null) pred = m.pi[k];
				else { pred = 0; for (int i = 0; i < K; i++) pred += alpha[i] * m.A[i, k]; }
				a[k] = pred * Math.Exp(lb[k] - mx); c += a[k];
			}
			if (!(c > 0) || double.IsInfinity(c)) return alpha != null ? (double[])alpha.Clone() : (double[])m.pi.Clone();
			for (int k = 0; k < K; k++) a[k] /= c;
			return a;
		}

		/// <summary>
		/// Fit K states on a z-scored window: _Gmm restarts (best converged lnL), then (Hmm) Baum-Welch from it with A = 0.9 diagonal;
		/// states ordered by mu[orderFeature] ascending (ties tieFeature). failReason set when every restart fails / does not converge
		/// (nonconv) or a covariance cannot be factored (chol).
		/// </summary>
		public static RegimeFit FitWindow(double[][] z, int K, RegimeConfig cfg, ulong seed, int orderFeature, int tieFeature)
		{
			int N = z.Length, Dm = z[0].Length;
			var r = new RegimeFit { method = cfg.method, K = K, D = Dm };
			if (N < K * 2) { r.failReason = "insufficient"; return r; }
			var gcfg = new GmmFitConfig { kMin = K, kMax = K, restarts = cfg.restarts, maxIter = cfg.maxIter, tol = cfg.tol, tolMode = EGmmTolMode.PerSample, regCovar = cfg.regCovar, seed = seed };
			var om = new double[N]; for (int i = 0; i < N; i++) om[i] = 1;
			GmmModel best = null;
			for (int rs = 0; rs < cfg.restarts; rs++)
			{
				GmmModel g = _Gmm.FitOnce(z, om, N, K, gcfg, rs);
				if (g == null || double.IsNaN(g.logLik) || double.IsInfinity(g.logLik) || !g.converged) continue;
				if (best == null || g.logLik > best.logLik) best = g;
			}
			if (best == null) { r.failReason = "nonconv"; return r; }
			_Gmm.Order(best, Math.Max(0, orderFeature), tieFeature);
			r.gmm = best.Clone(); r.gmmInit = best.init; r.gmmIters = best.iters;
			r.mu = new double[K][]; r.cov = new double[K][,]; r.pi = (double[])best.w.Clone();
			for (int k = 0; k < K; k++) { r.mu[k] = (double[])best.mu[k].Clone(); r.cov[k] = (double[,])best.cov[k].Clone(); }
			if (cfg.method == ERegimeMethod.Gmm)
			{
				r.logLik = best.logLik; r.iters = best.iters; r.converged = true;
				if (!factor(r, cfg)) { r.failReason = "chol"; return r; }
			}
			else
			{
				r.A = new double[K, K];
				for (int a = 0; a < K; a++) for (int b = 0; b < K; b++) r.A[a, b] = K == 1 ? 1 : (a == b ? 0.9 : 0.1 / (K - 1));
				r.hmmInitPi = (double[])r.pi.Clone(); r.hmmInitA = (double[,])r.A.Clone();
				r.hmmInitMu = new double[K][]; r.hmmInitCov = new double[K][,];
				for (int k = 0; k < K; k++) { r.hmmInitMu[k] = (double[])r.mu[k].Clone(); r.hmmInitCov[k] = (double[,])r.cov[k].Clone(); }
				string why = BaumWelch(z, r, cfg);
				if (why != null) { r.failReason = why; return r; }
				OrderStates(r, Math.Max(0, orderFeature), tieFeature);
			}
			r.bic = -2 * r.logLik + RegimeFit.ParamCount(cfg.method, K, Dm) * Math.Log(N);
			return r;
		}

		private static bool factor(RegimeFit r, RegimeConfig cfg)
		{
			r.L = new double[r.K][,];
			for (int k = 0; k < r.K; k++) { r.L[k] = new double[r.D, r.D]; int used = 0; if (!_Gmm.CholeskyWithRetry(r.cov[k], r.D, cfg.regCovar, 3, r.L[k], ref used)) return false; }
			return true;
		}

		/// <summary>Baum-Welch (Rabiner scaling; emissions shifted by the per-t max log density so lnL = sum (ln c_t + max_t)).</summary>
		private static string BaumWelch(double[][] x, RegimeFit r, RegimeConfig cfg)
		{
			int T = x.Length, K = r.K, Dm = r.D;
			var b = new double[T, K]; var shift = new double[T];
			var al = new double[T, K]; var be = new double[T, K]; var c = new double[T];
			var gamma = new double[T][];
			for (int t = 0; t < T; t++) gamma[t] = new double[K];
			var xi = new double[K, K];
			double prev = double.NaN; int it;
			if (!factor(r, cfg)) return "chol";
			for (it = 0; it < cfg.maxIter; it++)
			{
				double ll = eStep(x, r, b, shift, al, be, c, gamma, xi);
				if (double.IsNaN(ll) || double.IsInfinity(ll)) return "nonconv";
				r.logLik = ll; r.iters = it;
				if (it > 0 && Math.Abs(ll - prev) / T < cfg.tol) { r.converged = true; break; }
				prev = ll;
				// M step: pi0 = gamma_1; A with transPrior pseudo-counts; Gaussian M step of §3.1 on gamma
				for (int k = 0; k < K; k++) r.pi[k] = gamma[0][k];
				for (int i = 0; i < K; i++)
				{
					double den = 0; for (int t = 0; t < T - 1; t++) den += gamma[t][i];
					for (int jj = 0; jj < K; jj++) r.A[i, jj] = (xi[i, jj] + cfg.transPrior) / (den + K * cfg.transPrior);
				}
				for (int k = 0; k < K; k++)
				{
					double nk = 0; var mu = new double[Dm];
					for (int t = 0; t < T; t++) { double g = gamma[t][k]; nk += g; for (int d = 0; d < Dm; d++) mu[d] += g * x[t][d]; }
					if (!(nk > 1e-300)) return "nonconv";
					for (int d = 0; d < Dm; d++) mu[d] /= nk;
					var cv = new double[Dm, Dm];
					for (int t = 0; t < T; t++) { double g = gamma[t][k]; if (g == 0) continue; for (int a = 0; a < Dm; a++) for (int q = 0; q <= a; q++) cv[a, q] += g * (x[t][a] - mu[a]) * (x[t][q] - mu[q]); }
					for (int a = 0; a < Dm; a++) for (int q = 0; q <= a; q++) { double v = cv[a, q] / nk; cv[a, q] = v; cv[q, a] = v; }
					for (int a = 0; a < Dm; a++) cv[a, a] += cfg.regCovar;
					r.mu[k] = mu; r.cov[k] = cv;
				}
				if (!factor(r, cfg)) return "chol";
			}
			if (!r.converged) return "nonconv";
			r.gamma = gamma;
			return null;
		}

		private static double eStep(double[][] x, RegimeFit r, double[,] b, double[] shift, double[,] al, double[,] be, double[] c, double[][] gamma, double[,] xi)
		{
			int T = x.Length, K = r.K;
			for (int t = 0; t < T; t++)
			{
				double mx = double.NegativeInfinity;
				for (int k = 0; k < K; k++) { b[t, k] = _Gmm.LogPdf(x[t], r.mu[k], r.L[k], r.D); if (b[t, k] > mx) mx = b[t, k]; }
				shift[t] = mx;
				for (int k = 0; k < K; k++) b[t, k] = Math.Exp(b[t, k] - mx);
			}
			double ll = 0;
			for (int t = 0; t < T; t++)
			{
				double s = 0;
				for (int k = 0; k < K; k++)
				{
					double p;
					if (t == 0) p = r.pi[k];
					else { p = 0; for (int i = 0; i < K; i++) p += al[t - 1, i] * r.A[i, k]; }
					al[t, k] = p * b[t, k]; s += al[t, k];
				}
				if (!(s > 0)) return double.NaN;
				c[t] = s; for (int k = 0; k < K; k++) al[t, k] /= s;
				ll += Math.Log(s) + shift[t];
			}
			for (int k = 0; k < K; k++) be[T - 1, k] = 1;
			for (int t = T - 2; t >= 0; t--)
				for (int i = 0; i < K; i++)
				{
					double s = 0; for (int q = 0; q < K; q++) s += r.A[i, q] * b[t + 1, q] * be[t + 1, q];
					be[t, i] = s / c[t + 1];
				}
			for (int i = 0; i < K; i++) for (int q = 0; q < K; q++) xi[i, q] = 0;
			for (int t = 0; t < T; t++)
			{
				double s = 0; for (int k = 0; k < K; k++) { gamma[t][k] = al[t, k] * be[t, k]; s += gamma[t][k]; }
				for (int k = 0; k < K; k++) gamma[t][k] /= s;
				if (t < T - 1)
					for (int i = 0; i < K; i++) for (int q = 0; q < K; q++) xi[i, q] += al[t, i] * r.A[i, q] * b[t + 1, q] * be[t + 1, q] / c[t + 1];
			}
			return ll;
		}

		/// <summary>Order HMM states by mu[of] ascending (ties mu[tf], then index); permutes pi, A, mu, cov, L, gamma.</summary>
		public static void OrderStates(RegimeFit r, int of, int tf)
		{
			int K = r.K; var idx = new int[K]; for (int k = 0; k < K; k++) idx[k] = k;
			Array.Sort(idx, (a, b) =>
			{
				int c = r.mu[a][of].CompareTo(r.mu[b][of]);
				if (c == 0 && tf >= 0 && tf < r.D) c = r.mu[a][tf].CompareTo(r.mu[b][tf]);
				return c != 0 ? c : a.CompareTo(b);
			});
			var pi = new double[K]; var mu = new double[K][]; var cov = new double[K][,]; var L = new double[K][,]; var A = new double[K, K];
			for (int k = 0; k < K; k++) { pi[k] = r.pi[idx[k]]; mu[k] = r.mu[idx[k]]; cov[k] = r.cov[idx[k]]; L[k] = r.L[idx[k]]; for (int q = 0; q < K; q++) A[k, q] = r.A[idx[k], idx[q]]; }
			r.pi = pi; r.mu = mu; r.cov = cov; r.L = L; r.A = A;
			if (r.gamma != null) foreach (var g in r.gamma) { var c = (double[])g.Clone(); for (int k = 0; k < K; k++) g[k] = c[idx[k]]; }
		}

		/// <summary>Bhattacharyya distance between two Gaussians (any D &lt;= 4); +inf when the pooled covariance is singular.</summary>
		public static double Bhattacharyya(double[] m1, double[,] c1, double[] m2, double[,] c2)
		{
			int Dm = m1.Length; var c = new double[Dm, Dm];
			for (int a = 0; a < Dm; a++) for (int b = 0; b < Dm; b++) c[a, b] = 0.5 * (c1[a, b] + c2[a, b]);
			var L = new double[Dm, Dm]; var L1 = new double[Dm, Dm]; var L2 = new double[Dm, Dm];
			if (!_Gmm.Cholesky(c, Dm, L) || !_Gmm.Cholesky(c1, Dm, L1) || !_Gmm.Cholesky(c2, Dm, L2)) return double.PositiveInfinity;
			double q = 0, ld = 0, ld1 = 0, ld2 = 0; var y = new double[Dm];
			for (int i = 0; i < Dm; i++)
			{
				double s = m1[i] - m2[i]; for (int k = 0; k < i; k++) s -= L[i, k] * y[k];
				y[i] = s / L[i, i]; q += y[i] * y[i];
				ld += Math.Log(L[i, i]); ld1 += Math.Log(L1[i, i]); ld2 += Math.Log(L2[i, i]);
			}
			return q / 8 + (ld - 0.5 * (ld1 + ld2));   // 0.5 ln(det S / sqrt(det S1 det S2)) with ln det = 2 sum ln L_ii
		}

		/// <summary>perm[newState] = old state that minimises the total Bhattacharyya distance (brute force over K! permutations, raw space).</summary>
		public static int[] BestMatch(RegimeFit oldFit, RegimeFit newFit)
		{
			int K = newFit.K; var dist = new double[K, K];
			for (int a = 0; a < K; a++)
			{
				double[] mn; double[,] cn; newFit.Raw(a, out mn, out cn);
				for (int b = 0; b < K; b++) { double[] mo; double[,] co; oldFit.Raw(b, out mo, out co); dist[a, b] = Bhattacharyya(mn, cn, mo, co); }
			}
			var p = new int[K]; for (int k = 0; k < K; k++) p[k] = k;
			int[] best = (int[])p.Clone(); double bestD = double.PositiveInfinity;
			permute(p, 0, dist, ref best, ref bestD);
			return best;
		}

		private static void permute(int[] p, int i, double[,] dist, ref int[] best, ref double bestD)
		{
			if (i == p.Length)
			{
				double s = 0; for (int k = 0; k < p.Length; k++) s += dist[k, p[k]];
				if (s < bestD) { bestD = s; best = (int[])p.Clone(); }   // strict: ties keep the first found (identity is tried first)
				return;
			}
			for (int k = i; k < p.Length; k++)
			{
				int t = p[i]; p[i] = p[k]; p[k] = t;
				permute(p, i + 1, dist, ref best, ref bestD);
				t = p[i]; p[i] = p[k]; p[k] = t;
			}
		}
	}
	// </regime>
}
