using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MultiCharts tests for the shared GMM core (_Gmm, spec §3.1) and the G-P profile mixtures (_ProfileStats, spec §3.2).
static class Program
{
	static int pass, fail;
	static void Check(bool ok, string what) { if (ok) pass++; else { fail++; Console.WriteLine("FAIL: " + what); } }

	static double Gauss(_Rng r) { double u = Math.Max(r.NextDouble(), 1e-300), v = r.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

	// weighted histogram of samples from a 1-D mixture, on a grid of `tick`
	static void Hist(double[] mus, double[] sds, double[] pis, int n, double tick, ulong seed, out double[] p, out double[] v)
	{
		var r = new _Rng(seed); var h = new SortedDictionary<long, double>();
		for (int i = 0; i < n; i++)
		{
			double u = r.NextDouble(), acc = 0; int k = 0;
			for (; k < pis.Length - 1; k++) { acc += pis[k]; if (u < acc) break; }
			long g = (long)Math.Round((mus[k] + sds[k] * Gauss(r)) / tick); double x; h.TryGetValue(g, out x); h[g] = x + 1;
		}
		p = h.Keys.Select(g => g * tick).ToArray(); v = h.Values.ToArray();
	}

	static GmmFitConfig Cfg(int kMax, ulong seed) { return new GmmFitConfig { kMax = kMax, seed = seed }; }

	static List<RingBar> Session(DateTime day, double center, double spread, ulong seed, bool bimodal)
	{
		var r = new _Rng(seed); var bars = new List<RingBar>(); DateTime t = day.Date.AddHours(9.5);
		for (int i = 0; i < 390; i++)
		{
			double m = bimodal ? ((i / 65) % 2 == 0 ? center - 2 * spread : center + 2 * spread) : center;   // regime switches every 65 min
			double c = Math.Round((m + spread * 0.25 * Gauss(r)) * 4) / 4, rng = 0.25 * (1 + r.NextInt(3));
			bars.Add(new RingBar { bar = i, time = t.AddMinutes(i + 1), tradingDay = day.Date, o = c, c = c, h = c + rng, l = c - rng, v = 100 + r.NextInt(400), rth = true });
		}
		return bars;
	}

	static void Main()
	{
		// ---------------- §3.1 core ----------------
		Check(_Gmm.ParamCount(1, 1) == 2 && _Gmm.ParamCount(3, 1) == 8 && _Gmm.ParamCount(2, 2) == 11 && _Gmm.ParamCount(2, 4) == 1 + 8 + 20, "BIC parameter count p = (K-1) + KD + KD(D+1)/2");
		Check(new GmmFitConfig().restarts == 10 && new GmmFitConfig().bicDelta == 10 && new GmmFitConfig().kMax == 3 && new GmmFitConfig().regCovar == 1e-6 && new GmmFitConfig().Validate() == null, "spec defaults");

		double[] p, v;
		Hist(new[] { 100.0 }, new[] { 2.0 }, new[] { 1.0 }, 5000, 0.25, 1, out p, out v);
		var m1 = _Gmm.Fit1D(p, v, 390, Cfg(3, 11));
		Check(m1 != null && m1.K == 1 && Math.Abs(m1.mu[0][0] - 100) < 0.2 && Math.Abs(m1.Sd(0) - 2) < 0.2, "1-component recovery (K=" + (m1 == null ? 0 : m1.K) + ")");

		Hist(new[] { 95.0, 105.0 }, new[] { 1.5, 1.5 }, new[] { 0.4, 0.6 }, 6000, 0.25, 2, out p, out v);
		var m2 = _Gmm.Fit1D(p, v, 390, Cfg(3, 12));
		Check(m2.K == 2 && Math.Abs(m2.mu[0][0] - 95) < 0.3 && Math.Abs(m2.mu[1][0] - 105) < 0.3 && Math.Abs(m2.w[0] - 0.4) < 0.05, "2-component recovery, ordered by mean");

		Hist(new[] { 90.0, 100.0, 110.0 }, new[] { 1.5, 1.5, 1.5 }, new[] { 0.3, 0.4, 0.3 }, 9000, 0.25, 3, out p, out v);
		var m3 = _Gmm.Fit1D(p, v, 390, Cfg(4, 13));
		Check(m3.K == 3 && Math.Abs(m3.mu[0][0] - 90) < 0.4 && Math.Abs(m3.mu[1][0] - 100) < 0.4 && Math.Abs(m3.mu[2][0] - 110) < 0.4, "3-component recovery (K=" + m3.K + ")");
		bool sorted = true; for (int k = 1; k < m3.K; k++) sorted &= m3.mu[k][0] > m3.mu[k - 1][0];
		Check(sorted, "components ordered by ascending mean");
		Check(m3.perK != null && m3.perK.Length == 4, "per-K fits kept");
		// BIC delta rule: chosen K beats every smaller K by > 10 and no larger K beats it by > 10
		bool bicOk = true; for (int k = 0; k < m3.perK.Length; k++) { var f = m3.perK[k]; if (f == null) continue; if (f.K < m3.K) bicOk &= f.bic - m3.bic > 10; if (f.K > m3.K) bicOk &= !(m3.bic - f.bic > 10); }
		Check(bicOk, "BIC delta > 10 rule");
		// small separation with tiny N_eff: K=2 BIC gain < 10 -> K=1
		Hist(new[] { 99.0, 101.0 }, new[] { 1.2, 1.2 }, new[] { 0.5, 0.5 }, 4000, 0.25, 4, out p, out v);
		var mSmall = _Gmm.Fit1D(p, v, 20, Cfg(3, 14));
		Check(mSmall.K == 1, "BIC delta: weak 2nd mode with N_eff = 20 stays K=1");
		var mBig = _Gmm.Fit1D(p, v, 20, new GmmFitConfig { kMax = 3, seed = 14, bicDelta = 0 });
		Check(mBig.perK[1].bic < mBig.perK[0].bic + 10, "ΔBIC of K=2 vs K=1 below 10 in that case");

		// seed reproducibility
		var a = _Gmm.Fit1D(p, v, 390, Cfg(3, 99)); var b = _Gmm.Fit1D(p, v, 390, Cfg(3, 99));
		bool same = a.K == b.K && a.bic == b.bic; for (int k = 0; k < a.K && same; k++) same &= a.mu[k][0] == b.mu[k][0] && a.w[k] == b.w[k];
		Check(same, "same seed -> bit-identical fit");
		Check(_ProfileStats.MixtureSeed(new DateTime(2024, 3, 11)) == _Rng.Seed(27, new DateTime(2024, 3, 11).Ticks), "seed = _Rng.Seed(27, tradingDay.Ticks)");

		// collapse / reseed: K=3 forced on a 2-point dataset with one far outlier of tiny weight
		var xc = new List<double>(); var wc = new List<double>();
		for (int i = 0; i < 50; i++) { xc.Add(100 + 0.01 * i); wc.Add(1); }
		xc.Add(500); wc.Add(1e-6);
		var cc = new GmmFitConfig { kMin = 3, kMax = 3, seed = 5, minWeight = 1e-3 };
		var mc = _Gmm.Fit1D(xc, wc, 50, cc);
		Check(mc != null && mc.reseeds > 0, "collapsed component (w < minWeight) is reseeded (reseeds=" + (mc == null ? -1 : mc.reseeds) + ")");
		Check(mc != null && Math.Abs(mc.w.Sum() - 1) < 1e-9 && mc.w.All(w => w > 0), "weights renormalised after reseed");

		// Cholesky fallback
		var Lm = new double[2, 2]; int used = 0;
		var sing = new double[,] { { 1, 1 }, { 1, 1 } };
		Check(!_Gmm.Cholesky(new double[,] { { 1, 1 }, { 1, 1 - 1e-18 } }, 2, Lm), "plain Cholesky fails on a singular covariance");
		Check(_Gmm.CholeskyWithRetry(sing, 2, 1e-6, 3, Lm, ref used) && used >= 1, "Cholesky retry with regCovar x10 succeeds (retries=" + used + ")");
		used = 0; var neg = new double[,] { { 1, 0 }, { 0, -5 } };
		Check(!_Gmm.CholeskyWithRetry(neg, 2, 1e-6, 3, Lm, ref used), "Cholesky gives up after 3 retries");
		// 2-D fit on data with an exactly collinear feature still fits (fallback)
		var r2 = new _Rng(8); var x2 = new double[400][]; var w2 = new double[400];
		for (int i = 0; i < 400; i++) { double z = (i < 200 ? -3 : 3) + 0.5 * Gauss(r2); x2[i] = new[] { z, 2 * z }; w2[i] = 1; }
		var m2d = _Gmm.Fit(x2, w2, 400, Cfg(3, 21));
		Check(m2d != null && m2d.K == 2 && m2d.mu[0][0] < m2d.mu[1][0], "2-D collinear data fits via Cholesky fallback (K=" + (m2d == null ? 0 : m2d.K) + ")");

		// ---------------- §3.2 G-P ----------------
		Hist(new[] { 4990.0, 5010.0 }, new[] { 3.0, 3.0 }, new[] { 0.5, 0.5 }, 8000, 0.25, 31, out p, out v);
		var day = new DateTime(2024, 6, 3);
		var pm = _ProfileStats.ProfileMixture(p, v, 3, _ProfileStats.MixtureSeed(day), EGmmNeff.Bars, 390, 0.25);
		Check(pm.K == 2 && pm.voidPrice.Length == 1 && Math.Abs(pm.voidPrice[0] - 5000) <= 1, "void between the modes on the tick grid (" + string.Join(",", pm.voidPrice) + ")");
		Check(pm.voidDepth[0] > 0.8 && pm.voidDepth[0] <= 1, "void depth = 1 - f(void)/min(f(mu)) (" + pm.voidDepth[0] + ")");
		Check(Math.Abs(pm.voidPrice[0] / 0.25 - Math.Round(pm.voidPrice[0] / 0.25)) < 1e-9, "void on the tick grid");
		var pmR = _ProfileStats.ProfileMixture(p, v, 3, 1, EGmmNeff.Rows, 390, 0.25); var pmC = _ProfileStats.ProfileMixture(p, v, 3, 1, EGmmNeff.Contracts, 390, 0.25);
		Check(pm.nEff == 390 && pmR.nEff == p.Length && Math.Abs(pmC.nEff - v.Sum()) < 1e-6, "N_eff Bars | Rows | Contracts");
		string j = pm.Journal(day);
		Check(j.StartsWith("GMMP 2024-06-03 K(2) bic(") && j.Contains(" mu(") && j.Contains(" sd(") && j.Contains(" pi(") && j.Contains(" void(5000/") , "journal format: " + j);
		var lv = _ProfileStats.MixtureLevels(pm, 0.5, day, day, day, 1, "t");
		Check(lv.Count(l => l.kind == ELevelKind.GmmMu) == 2 && lv.Count(l => l.kind == ELevelKind.GmmEdge) == 4 && lv.Count(l => l.kind == ELevelKind.GmmVoid) == 1, "LevelRefs GmmMu / GmmEdge / GmmVoid");
		var e05 = lv.First(l => l.kind == ELevelKind.GmmEdge); var e10 = _ProfileStats.MixtureLevels(pm, 1.0, day, day, day, 1, "t").First(l => l.kind == ELevelKind.GmmEdge);
		Check(Math.Abs(e05.price - (pm.mu[0] - 0.5 * pm.sd[0])) < 1e-9 && Math.Abs(e10.price - (pm.mu[0] - pm.sd[0])) < 1e-9, "edges at mu -/+ c sd, c in {0.5, 1.0}");

		// A2-32 tracker
		var log = new List<string>();
		var tr = new _GmmProfileTracker(0.25) { journal = log.Add };
		DateTime d0 = new DateTime(2024, 1, 2);
		for (int i = 0; i < 19; i++) tr.OnSessionClose(d0.AddDays(i), Session(d0.AddDays(i), 4800 + i, 4, (ulong)(100 + i), true), false, "ESH4");
		tr.Levels();
		Check(tr.LastSigma != null && !tr.LastSigma.ok && tr.LastSigma.reason == "insufficient window", "A2-32 needs 20 sessions");
		tr.OnSessionClose(d0.AddDays(19), Session(d0.AddDays(19), 4819, 4, 119, true), false, "ESH4");
		var L = tr.Levels();
		Check(tr.LastSigma.ok && tr.LastSigma.sessions == 20 && L.Any(l => l.kind == ELevelKind.SigmaMu), "A2-32 fits with 20 valid sessions in one contract");
		var sig = tr.LastSigma;
		 Check(sig.mix.K >= 2 && sig.mix.mu[0] < -0.2 && sig.mix.mu[sig.mix.K - 1] > 0.2 && Math.Abs(Enumerable.Range(0, sig.mix.K).Where(k => sig.mix.mu[k] < 0).Sum(k => sig.mix.pi[k]) - 0.5) < 0.1, "sigma mixture finds the modes on both sides of sVWAP in z (K=" + sig.mix.K + ")");
		var sigmas = new List<double>(); var ses = new List<GmmSession>();
		for (int i = 0; i < 20; i++) { var g = GmmSession.FromBars(d0.AddDays(i), Session(d0.AddDays(i), 4800 + i, 4, (ulong)(100 + i), true), 0.25, false, "ESH4"); sigmas.Add(g.sigma); ses.Add(g); }
		Check(Math.Abs(sig.sigmaHat - _ProfileStats.EwmaForecast(sigmas, 10)) < 1e-12 && sig.sigmaHat > 0, "sigma-hat = EWMA(span 10) of the window sigmas");
		Check(Math.Abs(sig.levels[0] - (ses[19].sVwap + sig.mix.mu[0] * sig.sigmaHat)) < 1e-9, "levels = prior sVWAP + z_k sigma-hat");
		int fits = tr.Fits; tr.Levels(); tr.Levels();
		Check(tr.Fits == fits, "lazy: one fit per session close");
		Check(log.Any(s => s.StartsWith("GMMP 2024-01-21 K(") && s.Contains("space(z)")) && log.Any(s => s.StartsWith("GMMP 2024-01-21 K(") && !s.Contains("space(z)")), "price + sigma journal lines");
		// roll inside the window -> NaN + journal + skipped count
		tr.OnSessionClose(d0.AddDays(20), Session(d0.AddDays(20), 4830, 4, 120, true), true, "ESM4");
		tr.Levels();
		Check(!tr.LastSigma.ok && tr.LastSigma.levels.Length == 0 && !tr.Levels().Any(l => l.kind == ELevelKind.SigmaMu), "roll in the window -> no SigmaMu levels (NaN)");
		Check(log.Any(s => s.Contains("SIGMA insufficient window contract(ESM4) skipped(1)")), "insufficient window journal line");
		for (int i = 21; i < 25; i++) { tr.OnSessionClose(d0.AddDays(i), Session(d0.AddDays(i), 4830, 4, (ulong)(100 + i), true), false, "ESM4"); tr.Levels(); }
		Check(tr.SkippedSessions("ESM4") == 5 && tr.SkippedSessions("ESH4") == 1, "skipped-session count per contract (ESM4=" + tr.SkippedSessions("ESM4") + ", ESH4=" + tr.SkippedSessions("ESH4") + ")");
		tr.OnSessionClose(d0.AddDays(25), Session(d0.AddDays(25), 4830, 4, 125, true), false, "ESU4");
		Check(log.Any(s => s == "GMMP SIGMA contract(ESM4) skippedSessions(5)"), "skipped count journalled on contract change");
		// invalid-sigma session (too few bars) is skipped and the pool filled from older sessions
		var ses2 = new List<GmmSession>(ses); var shortS = GmmSession.FromBars(d0.AddDays(30), Session(d0.AddDays(30), 4800, 4, 7, true).Take(100).ToList(), 0.25, false, "ESH4");
		Check(double.IsNaN(shortS.sigma), "session with < 60 5m bars has NaN sigma");
		ses2.Insert(0, GmmSession.FromBars(d0.AddDays(-1), Session(d0.AddDays(-1), 4800, 4, 99, true), 0.25, false, "ESH4")); ses2.Insert(ses2.Count - 1, shortS); ses2.RemoveAt(1);
		var sm2 = _ProfileStats.SigmaMixture(ses2, 1);
		 Check(sm2.ok && sm2.sessions == 20 && sm2.days[0] == d0.AddDays(-1), "invalid-sigma session skipped, window filled from older sessions");

		// ---------------- audit fixes ----------------
		var bb = Session(d0, 4800, 4, 5, false);
		var b1 = _ProfileStats.BootstrapPoc(bb, 0.25, 2, 77UL); var b2 = _ProfileStats.BootstrapPoc(bb, 0.25, 2, 5, 500, 77UL);
		Check(b1.draws == 500 && b1.block == 5 && b1.medianPoc == b2.medianPoc, "BootstrapPoc default overload: block 5, draws 500");

		Console.WriteLine("GmmTests: " + pass + " passed, " + fail + " failed");
		Environment.Exit(fail == 0 ? 0 : 1);
	}
}
