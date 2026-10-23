using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 §3.1 — shared EM core for Gaussian mixtures (pure managed C#, no external library). Consumers: G-P profile
	// mixtures (_ProfileStats.ProfileMixture, A2-27 / A2-28 / A2-32) and, later, G-R (deferred).
	// Weighted EM in log space (logsumexp), full covariance, hand-coded Cholesky for D <= 4 (regCovar x10 retries up to 3,
	// then the component is degenerate and re-seeded), k-means++ init from _Rng (seed + restart index) + 10 Lloyd iterations,
	// collapse re-seed (N_k / sum w < minWeight) at the lowest-likelihood point with the global covariance, BIC with
	// p = (K-1) + KD + KD(D+1)/2 on N_eff = sum w, K stepped up only when BIC improves by more than bicDelta, best lnL across
	// restarts per K, components sorted by the ordering feature. Pure C# (tests/GmmTests).
	// ============================================================================================

	// <gmm>
	public enum EGmmTolMode { Relative = 0, PerSample = 1 }

	public sealed class GmmModel
	{
		public int K, D;
		public double[] w;
		public double[][] mu;
		public double[][,] cov;
		public double logLik = double.NaN, bic = double.NaN;
		public int iters;
		public bool converged;
		public int reseeds;
		// additive diagnostics
		public double nEff = double.NaN;
		public int restart = -1;        // restart index that produced this fit
		public int cholRetries;         // regCovar x10 retries that were needed
		public bool reseedInFinalIter;
		public GmmModel[] perK;         // best fit per K (index K - kMin); set on the selected model only
		public GmmModel init;           // the k-means++ / Lloyd start of this EM run (G-R / G-V reference export); additive

		public int Params { get { return _Gmm.ParamCount(K, D); } }

		/// <summary>1-D convenience: SD of component k (sqrt of cov[k][0,0]).</summary>
		public double Sd(int k) { return Math.Sqrt(cov[k][0, 0]); }

		public GmmModel Clone()
		{
			var m = (GmmModel)MemberwiseClone();
			m.w = (double[])w.Clone();
			m.mu = new double[K][]; m.cov = new double[K][,];
			for (int k = 0; k < K; k++) { m.mu[k] = (double[])mu[k].Clone(); m.cov[k] = (double[,])cov[k].Clone(); }
			m.perK = null;
			m.init = init == null ? null : init.Clone();
			return m;
		}
	}

	public sealed class GmmFitConfig
	{
		public int kMin = 1, kMax = 3, restarts = 10, maxIter = 200;
		public double tol = 1e-6;
		public EGmmTolMode tolMode = EGmmTolMode.Relative;
		public double regCovar = 1e-6, minWeight = 1e-3, bicDelta = 10;
		public ulong seed;
		public int lloydIters = 10;     // spec: 10 Lloyd iterations after k-means++
		public int cholRetries = 3;     // spec: regCovar x10, up to 3 times

		public string Validate()
		{
			var e = new List<string>();
			if (kMin < 1 || kMax < kMin) e.Add("kMin/kMax=" + kMin + "/" + kMax);
			if (restarts < 1) e.Add("restarts=" + restarts);
			if (maxIter < 1) e.Add("maxIter=" + maxIter);
			if (!(tol > 0)) e.Add("tol=" + tol);
			if (!(regCovar >= 0)) e.Add("regCovar=" + regCovar);
			if (!(minWeight >= 0 && minWeight < 1)) e.Add("minWeight=" + minWeight);
			if (!(bicDelta >= 0)) e.Add("bicDelta=" + bicDelta);
			return e.Count == 0 ? null : "GmmFitConfig invalid: " + string.Join(", ", e);
		}

		public string Canon()
		{
			return string.Format(CultureInfo.InvariantCulture, "gmm:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}",
				kMin, kMax, restarts, maxIter, tol, (int)tolMode, regCovar, minWeight, bicDelta, seed, lloydIters, cholRetries);
		}

		public GmmFitConfig Copy() { return (GmmFitConfig)MemberwiseClone(); }
	}

	public static class _Gmm
	{
		public const int MaxD = 4;
		private const double LN2PI = 1.8378770664093453;

		public static int ParamCount(int K, int D) { return (K - 1) + K * D + K * D * (D + 1) / 2; }

		/// <summary>
		/// Fit and select K. x[n][d] (D = x[0].Length, 1..4); weights (null = all 1) are rescaled so sum = nEff (nEff &lt;= 0 or NaN:
		/// sum of the raw weights, i.e. N for unit weights). orderFeature / tieFeature: sort keys for label stability (tieFeature -1 = none).
		/// Returns null when there is no usable data. The returned model carries perK (best lnL fit for every K tried).
		/// </summary>
		public static GmmModel Fit(double[][] x, double[] weights, double nEff, GmmFitConfig cfg, int orderFeature = 0, int tieFeature = -1)
		{
			if (cfg == null) cfg = new GmmFitConfig();
			string err = cfg.Validate(); if (err != null) throw new ArgumentException(err);
			double[] om;
			if (!prepare(x, weights, ref nEff, out om)) return null;
			int D = x[0].Length;
			var perK = new List<GmmModel>();
			GmmModel chosen = null;
			// Every K in kMin..kMax is fitted and compared against the best K so far (chosen), i.e. the search does NOT stop at the
			// first K that fails to beat it by bicDelta: K=3 can still win when K=2 did not improve on K=1 by more than bicDelta.
			for (int K = cfg.kMin; K <= cfg.kMax; K++)
			{
				GmmModel best = FitK(x, om, nEff, K, cfg, orderFeature, tieFeature);
				perK.Add(best);
				if (best == null) continue;
				if (chosen == null) chosen = best;
				else if (best.bic < chosen.bic - cfg.bicDelta) chosen = best;   // step up only on a BIC gain > bicDelta
			}
			if (chosen == null) return null;
			chosen = chosen.Clone();
			chosen.perK = perK.ToArray();
			return chosen;
		}

		/// <summary>1-D convenience (G-P): x = values, w = weights.</summary>
		public static GmmModel Fit1D(IList<double> x, IList<double> weights, double nEff, GmmFitConfig cfg)
		{
			if (x == null || x.Count == 0) return null;
			var xx = new double[x.Count][]; var ww = weights == null ? null : new double[x.Count];
			for (int i = 0; i < x.Count; i++) { xx[i] = new[] { x[i] }; if (ww != null) ww[i] = weights[i]; }
			return Fit(xx, ww, nEff, cfg, 0, -1);
		}

		private static bool prepare(double[][] x, double[] weights, ref double nEff, out double[] om)
		{
			om = null;
			if (x == null || x.Length == 0 || x[0] == null) return false;
			int N = x.Length, D = x[0].Length;
			if (D < 1 || D > MaxD) throw new ArgumentException("_Gmm: D must be 1.." + MaxD);
			if (weights != null && weights.Length != N) throw new ArgumentException("_Gmm: weights length");
			double sw = 0;
			om = new double[N];
			for (int n = 0; n < N; n++)
			{
				if (x[n] == null || x[n].Length != D) throw new ArgumentException("_Gmm: ragged x");
				bool fin = true; for (int d = 0; d < D; d++) if (double.IsNaN(x[n][d]) || double.IsInfinity(x[n][d])) fin = false;
				double wn = weights == null ? 1.0 : weights[n];
				om[n] = fin && wn > 0 && !double.IsInfinity(wn) ? wn : 0;
				sw += om[n];
			}
			if (!(sw > 0)) return false;
			if (!(nEff > 0) || double.IsInfinity(nEff)) nEff = sw;
			double s = nEff / sw;
			for (int n = 0; n < N; n++) om[n] *= s;
			return true;
		}

		/// <summary>Best-lnL fit of K components across cfg.restarts restarts (weights already scaled to sum nEff).</summary>
		public static GmmModel FitK(double[][] x, double[] om, double nEff, int K, GmmFitConfig cfg, int orderFeature = 0, int tieFeature = -1)
		{
			GmmModel best = null;
			for (int r = 0; r < cfg.restarts; r++)
			{
				GmmModel m = FitOnce(x, om, nEff, K, cfg, r);
				if (m == null || double.IsNaN(m.logLik)) continue;
				if (best == null || m.logLik > best.logLik) best = m;   // ties keep the earlier restart
			}
			if (best != null) Order(best, orderFeature, tieFeature);
			return best;
		}

		/// <summary>One EM run from a k-means++ init seeded by _Rng(cfg.seed + restart).</summary>
		public static GmmModel FitOnce(double[][] x, double[] om, double nEff, int K, GmmFitConfig cfg, int restart)
		{
			int N = x.Length, D = x[0].Length;
			double sw = 0; for (int n = 0; n < N; n++) sw += om[n];
			if (!(sw > 0)) return null;
			var rng = new _Rng(unchecked(cfg.seed + (ulong)restart));
			var m = new GmmModel { K = K, D = D, w = new double[K], mu = new double[K][], cov = new double[K][,], nEff = nEff, restart = restart };
			double[,] gcov = weightedCov(x, om, null, 0, mean(x, om, null, 0, D), D, cfg.regCovar);
			kmeansInit(x, om, K, D, cfg, rng, m, gcov);
			m.init = m.Clone();

			var L = new double[K][,]; var ok = new bool[K];
			var lnr = new double[N, K]; var lnp = new double[N];
			double prev = double.NaN; int lastReseedIter = -2, it;
			factorAll(m, L, ok, cfg, x, om, lnp, gcov, -1, ref lastReseedIter, false);
			for (it = 0; it < cfg.maxIter; it++)
			{
				double ll = eStep(x, om, m, L, lnr, lnp);
				m.logLik = ll; m.iters = it;
				if (it > 0 && !double.IsNaN(prev))
				{
					double change = cfg.tolMode == EGmmTolMode.Relative ? Math.Abs(ll - prev) / Math.Max(Math.Abs(ll), 1e-300) : Math.Abs(ll - prev) / sw;
					if (change < cfg.tol) { m.converged = true; break; }
				}
				prev = ll;
				mStep(x, om, m, lnr, sw, cfg.regCovar);
				// collapse: N_k / sum w < minWeight -> re-seed at the lowest-likelihood point with the global covariance
				for (int k = 0; k < K; k++)
					if (m.w[k] < cfg.minWeight) { reseed(m, k, x, om, lnp, gcov); m.reseeds++; lastReseedIter = it; }
				factorAll(m, L, ok, cfg, x, om, lnp, gcov, it, ref lastReseedIter, true);
			}
			if (!m.converged) { m.logLik = eStep(x, om, m, L, lnr, lnp); m.iters = cfg.maxIter; }
			// a re-seed in the final M step means the reported fit has not converged
			if (lastReseedIter >= 0 && lastReseedIter >= it - 1) { m.converged = false; m.reseedInFinalIter = true; }
			m.bic = -2 * m.logLik + ParamCount(K, D) * Math.Log(nEff);
			return m;
		}

		// ---------------- init ----------------

		private static void kmeansInit(double[][] x, double[] om, int K, int D, GmmFitConfig cfg, _Rng rng, GmmModel m, double[,] gcov)
		{
			int N = x.Length;
			var c = new double[K][];
			var d2 = new double[N];
			// k-means++ (weighted): first centre with probability proportional to w, then proportional to w x D^2
			int first = pick(om, rng);
			c[0] = (double[])x[first].Clone();
			for (int n = 0; n < N; n++) d2[n] = sq(x[n], c[0], D);
			for (int k = 1; k < K; k++)
			{
				var pr = new double[N]; for (int n = 0; n < N; n++) pr[n] = om[n] * d2[n];
				int j = pick(pr, rng); if (j < 0) j = pick(om, rng);
				c[k] = (double[])x[j].Clone();
				for (int n = 0; n < N; n++) d2[n] = Math.Min(d2[n], sq(x[n], c[k], D));
			}
			var lab = new int[N];
			for (int iter = 0; iter <= cfg.lloydIters; iter++)
			{
				for (int n = 0; n < N; n++)
				{
					int b = 0; double bd = sq(x[n], c[0], D);
					for (int k = 1; k < K; k++) { double dd = sq(x[n], c[k], D); if (dd < bd) { bd = dd; b = k; } }
					lab[n] = b;
				}
				if (iter == cfg.lloydIters) break;
				for (int k = 0; k < K; k++) { double s; double[] mk = mean(x, om, lab, k, D, out s); if (s > 0) c[k] = mk; }   // empty cluster keeps its centre
			}
			double sw = 0; for (int n = 0; n < N; n++) sw += om[n];
			for (int k = 0; k < K; k++)
			{
				double s = 0; for (int n = 0; n < N; n++) if (lab[n] == k) s += om[n];
				m.mu[k] = c[k];
				m.w[k] = s / sw;
				m.cov[k] = s > 0 ? weightedCov(x, om, lab, k, c[k], D, cfg.regCovar) : (double[,])gcov.Clone();
			}
		}

		private static int pick(double[] p, _Rng rng)
		{
			double s = 0; foreach (double v in p) s += v;
			if (!(s > 0)) return -1;
			double u = rng.NextDouble() * s, acc = 0; int last = -1;
			for (int n = 0; n < p.Length; n++) { if (!(p[n] > 0)) continue; acc += p[n]; last = n; if (u < acc) return n; }
			return last;
		}

		private static double sq(double[] a, double[] b, int D) { double s = 0; for (int d = 0; d < D; d++) { double z = a[d] - b[d]; s += z * z; } return s; }

		private static double[] mean(double[][] x, double[] om, int[] lab, int k, int D) { double s; return mean(x, om, lab, k, D, out s); }

		private static double[] mean(double[][] x, double[] om, int[] lab, int k, int D, out double s)
		{
			var m = new double[D]; s = 0;
			for (int n = 0; n < x.Length; n++) { if (lab != null && lab[n] != k) continue; s += om[n]; for (int d = 0; d < D; d++) m[d] += om[n] * x[n][d]; }
			if (s > 0) for (int d = 0; d < D; d++) m[d] /= s;
			return m;
		}

		private static double[,] weightedCov(double[][] x, double[] om, int[] lab, int k, double[] mu, int D, double reg)
		{
			var c = new double[D, D]; double s = 0;
			for (int n = 0; n < x.Length; n++)
			{
				if (lab != null && lab[n] != k) continue; if (!(om[n] > 0)) continue;
				s += om[n];
				for (int a = 0; a < D; a++) for (int b = 0; b <= a; b++) c[a, b] += om[n] * (x[n][a] - mu[a]) * (x[n][b] - mu[b]);
			}
			for (int a = 0; a < D; a++) for (int b = 0; b <= a; b++) { double v = s > 0 ? c[a, b] / s : 0; c[a, b] = v; c[b, a] = v; }
			for (int a = 0; a < D; a++) c[a, a] += reg;
			return c;
		}

		// ---------------- EM ----------------

		/// <summary>E step: ln r_nk = ln w_k + ln N(x_n | mu_k, S_k) - logsumexp_j; returns lnL = sum w_n ln p(x_n).</summary>
		private static double eStep(double[][] x, double[] om, GmmModel m, double[][,] L, double[,] lnr, double[] lnp)
		{
			int N = x.Length, K = m.K; double ll = 0;
			var t = new double[K];
			for (int n = 0; n < N; n++)
			{
				double mx = double.NegativeInfinity;
				for (int k = 0; k < K; k++)
				{
					t[k] = m.w[k] > 0 ? Math.Log(m.w[k]) + LogPdf(x[n], m.mu[k], L[k], m.D) : double.NegativeInfinity;
					if (t[k] > mx) mx = t[k];
				}
				double s = 0; for (int k = 0; k < K; k++) s += Math.Exp(t[k] - mx);
				double lse = mx + Math.Log(s);
				lnp[n] = lse;
				for (int k = 0; k < K; k++) lnr[n, k] = t[k] - lse;
				if (om[n] > 0) ll += om[n] * lse;
			}
			return ll;
		}

		private static void mStep(double[][] x, double[] om, GmmModel m, double[,] lnr, double sw, double reg)
		{
			int N = x.Length, K = m.K, D = m.D;
			for (int k = 0; k < K; k++)
			{
				double nk = 0; var mu = new double[D];
				for (int n = 0; n < N; n++) { if (!(om[n] > 0)) continue; double r = om[n] * Math.Exp(lnr[n, k]); nk += r; for (int d = 0; d < D; d++) mu[d] += r * x[n][d]; }
				m.w[k] = nk / sw;
				if (!(nk > 0)) continue;   // collapsed: handled by the caller
				for (int d = 0; d < D; d++) mu[d] /= nk;
				var c = new double[D, D];
				for (int n = 0; n < N; n++)
				{
					if (!(om[n] > 0)) continue; double r = om[n] * Math.Exp(lnr[n, k]); if (r == 0) continue;
					for (int a = 0; a < D; a++) for (int b = 0; b <= a; b++) c[a, b] += r * (x[n][a] - mu[a]) * (x[n][b] - mu[b]);
				}
				for (int a = 0; a < D; a++) for (int b = 0; b <= a; b++) { double v = c[a, b] / nk; c[a, b] = v; c[b, a] = v; }
				for (int a = 0; a < D; a++) c[a, a] += reg;
				m.mu[k] = mu; m.cov[k] = c;
			}
		}

		/// <summary>Re-seed component k: mean at the (positive-weight) point with the lowest current mixture likelihood (ties: lowest index),
		/// covariance = global covariance, weight = 1/K (others renormalised).</summary>
		private static void reseed(GmmModel m, int k, double[][] x, double[] om, double[] lnp, double[,] gcov)
		{
			int j = -1;
			for (int n = 0; n < x.Length; n++) if (om[n] > 0 && (j < 0 || lnp[n] < lnp[j])) j = n;
			if (j < 0) j = 0;
			m.mu[k] = (double[])x[j].Clone();
			m.cov[k] = (double[,])gcov.Clone();
			double rest = 0; for (int q = 0; q < m.K; q++) if (q != k) rest += m.w[q];
			double wk = 1.0 / m.K;
			for (int q = 0; q < m.K; q++) m.w[q] = q == k ? wk : (rest > 0 ? m.w[q] / rest * (1 - wk) : wk);
		}

		/// <summary>Cholesky of every component; on failure add regCovar x10, x100, x1000 to the diagonal; still failing = degenerate -> re-seed.</summary>
		private static void factorAll(GmmModel m, double[][,] L, bool[] ok, GmmFitConfig cfg, double[][] x, double[] om, double[] lnp, double[,] gcov, int it, ref int lastReseedIter, bool haveLnp)
		{
			for (int k = 0; k < m.K; k++)
			{
				L[k] = new double[m.D, m.D];
				ok[k] = CholeskyWithRetry(m.cov[k], m.D, cfg.regCovar, cfg.cholRetries, L[k], ref m.cholRetries);
				if (ok[k]) continue;
				if (haveLnp) reseed(m, k, x, om, lnp, gcov);
				else { m.mu[k] = mean(x, om, null, 0, m.D); m.cov[k] = (double[,])gcov.Clone(); }
				m.reseeds++; lastReseedIter = it;
				int dummy = 0;
				if (!CholeskyWithRetry(m.cov[k], m.D, cfg.regCovar, cfg.cholRetries, L[k], ref dummy))
				{
					var c = new double[m.D, m.D]; for (int d = 0; d < m.D; d++) c[d, d] = 1; m.cov[k] = c; Cholesky(c, m.D, L[k]);   // all-constant data: unit fallback
				}
			}
		}

		/// <summary>Cholesky with the regCovar x10 retry ladder (retries x10, x100, x1000 on the diagonal). The added regularisation is written back into cov.</summary>
		public static bool CholeskyWithRetry(double[,] cov, int D, double regCovar, int retries, double[,] L, ref int retriesUsed)
		{
			if (Cholesky(cov, D, L)) return true;
			double add = Math.Max(regCovar, 1e-12);
			for (int r = 1; r <= retries; r++)
			{
				add *= 10; retriesUsed++;
				var c = (double[,])cov.Clone();
				for (int d = 0; d < D; d++) c[d, d] += add;
				if (Cholesky(c, D, L)) { for (int d = 0; d < D; d++) cov[d, d] = c[d, d]; return true; }
			}
			return false;
		}

		/// <summary>Hand-coded Cholesky A = L L^T for D &lt;= 4. False when A is not (numerically) positive definite or not finite.</summary>
		public static bool Cholesky(double[,] a, int D, double[,] L)
		{
			if (D < 1 || D > MaxD) return false;
			for (int i = 0; i < D; i++)
				for (int j = 0; j < D; j++) L[i, j] = 0;
			for (int j = 0; j < D; j++)
			{
				double s = a[j, j];
				for (int k = 0; k < j; k++) s -= L[j, k] * L[j, k];
				if (!(s > 0) || double.IsInfinity(s)) return false;
				double ljj = Math.Sqrt(s);
				if (ljj <= 1e-150) return false;
				L[j, j] = ljj;
				for (int i = j + 1; i < D; i++)
				{
					double t = a[i, j];
					for (int k = 0; k < j; k++) t -= L[i, k] * L[j, k];
					L[i, j] = t / ljj;
				}
			}
			return true;
		}

		/// <summary>ln N(x | mu, L L^T) = -0.5 (D ln 2pi + 2 sum ln L_ii + |L^-1 (x - mu)|^2).</summary>
		public static double LogPdf(double[] x, double[] mu, double[,] L, int D)
		{
			var z = new double[D]; double q = 0, ld = 0;
			for (int i = 0; i < D; i++)
			{
				double s = x[i] - mu[i];
				for (int k = 0; k < i; k++) s -= L[i, k] * z[k];
				z[i] = s / L[i, i];
				q += z[i] * z[i]; ld += Math.Log(L[i, i]);
			}
			return -0.5 * (D * LN2PI + q) - ld;
		}

		/// <summary>Mixture log density ln f(x) of a fitted model.</summary>
		public static double LogDensity(GmmModel m, double[] x)
		{
			double mx = double.NegativeInfinity; var t = new double[m.K];
			for (int k = 0; k < m.K; k++)
			{
				var L = new double[m.D, m.D]; int r = 0;
				if (!(m.w[k] > 0) || !CholeskyWithRetry((double[,])m.cov[k].Clone(), m.D, 0, 3, L, ref r)) { t[k] = double.NegativeInfinity; continue; }
				t[k] = Math.Log(m.w[k]) + LogPdf(x, m.mu[k], L, m.D); if (t[k] > mx) mx = t[k];
			}
			if (double.IsNegativeInfinity(mx)) return mx;
			double s = 0; for (int k = 0; k < m.K; k++) s += Math.Exp(t[k] - mx);
			return mx + Math.Log(s);
		}

		/// <summary>1-D mixture density f(x).</summary>
		public static double Density1D(GmmModel m, double x)
		{
			double f = 0;
			for (int k = 0; k < m.K; k++) { double sd = Math.Sqrt(m.cov[k][0, 0]); double z = (x - m.mu[k][0]) / sd; f += m.w[k] * Math.Exp(-0.5 * z * z) / (sd * 2.5066282746310002); }
			return f;
		}

		/// <summary>Sort components by mu[orderFeature] ascending (ties by mu[tieFeature], then original index) and remap all parameters.</summary>
		public static void Order(GmmModel m, int orderFeature, int tieFeature)
		{
			if (m == null || m.K < 2) return;
			int of = Math.Max(0, Math.Min(m.D - 1, orderFeature));
			var idx = new int[m.K]; for (int k = 0; k < m.K; k++) idx[k] = k;
			Array.Sort(idx, (a, b) =>
			{
				int c = m.mu[a][of].CompareTo(m.mu[b][of]);
				if (c == 0 && tieFeature >= 0 && tieFeature < m.D) c = m.mu[a][tieFeature].CompareTo(m.mu[b][tieFeature]);
				return c != 0 ? c : a.CompareTo(b);
			});
			var w = new double[m.K]; var mu = new double[m.K][]; var cov = new double[m.K][,];
			for (int k = 0; k < m.K; k++) { w[k] = m.w[idx[k]]; mu[k] = m.mu[idx[k]]; cov[k] = m.cov[idx[k]]; }
			m.w = w; m.mu = mu; m.cov = cov;
		}
	}
	// </gmm>
}
