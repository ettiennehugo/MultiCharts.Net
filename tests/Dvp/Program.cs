using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PowerLanguage.Function;

// M-DVP PR-D1 (specs/m-dvp-spec.md §15): DV-T1..T8, T20, T23 on the extracted <dvp-core> fit.
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Rel(double a, double b, double tol, double floor = 0)
	{
		if (double.IsNegativeInfinity(a) && double.IsNegativeInfinity(b)) return true;
		return Math.Abs(a - b) <= tol * Math.Max(Math.Abs(b), floor);
	}

	static Random s_rng = new Random(20261024);
	static double Gauss() { double u1 = 1.0 - s_rng.NextDouble(), u2 = s_rng.NextDouble(); return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }

	static string Root()
	{
		string d = AppContext.BaseDirectory;
		for (int i = 0; i < 8 && d != null; i++) { if (File.Exists(Path.Combine(d, "_DvpEngine.Function.CS"))) return d; d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); }
		return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
	}

	static string CoreSource()
	{
		string src = File.ReadAllText(Path.Combine(Root(), "_DvpEngine.Function.CS"));
		int a = src.IndexOf("// <dvp-core>", StringComparison.Ordinal), b = src.IndexOf("// </dvp-core>", StringComparison.Ordinal);
		return a >= 0 && b > a ? src.Substring(a, b - a) : "";
	}

	static int Count(string s, string sub) { int c = 0, i = 0; while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { c++; i += sub.Length; } return c; }

	static double[] Parse(string line) { var p = line.Split(' '); var r = new double[p.Length - 1]; for (int i = 1; i < p.Length; i++) r[i - 1] = double.Parse(p[i], CultureInfo.InvariantCulture); return r; }

	// DV-T1: parity with reference.py (numpy QR on the Vandermonde basis), 50 series, d = 0..3, w = 1 and w = v.
	static void T1()
	{
		string path = Path.Combine(Root(), "tests", "Dvp", "fixtures", "fit_cases.txt");
		var lines = File.ReadAllLines(path);
		var fit = new DvpFit(1000);
		int cases = 0;
		for (int li = 0; li < lines.Length; )
		{
			var h = lines[li++].Split(' ');
			int id = int.Parse(h[1]), n = int.Parse(h[2]); bool weighted = h[3] == "1";
			double[] y = Parse(lines[li++]), v = Parse(lines[li++]);
			Check(fit.FitRaw(y, v, n, 3, weighted), "T1 fit case " + id);
			int[] samples = { 0, n / 3, n / 2, n - 1 };
			for (int d = 0; d <= 3; d++)
			{
				double[] eRaw = Parse(lines[li++]); var e = new double[eRaw.Length - 1]; Array.Copy(eRaw, 1, e, 0, e.Length);   // (d) sse sigma r2 bic dw skew yhat_ut h_ut (yhat_i h_i) x 4
				double dw, skew; fit.ResidualStats(d, out dw, out skew);
				double sc = e[1];   // sigma scale for absolute-ish comparisons of price values
				bool ok = Rel(fit.Sse[d], e[0], 1e-9) && Rel(fit.Sigma[d], e[1], 1e-9) && Math.Abs(fit.R2[d] - e[2]) <= 1e-9
					&& Rel(fit.Bic[d], e[3], 1e-9, 1) && Rel(dw, e[4], 1e-9) && Rel(skew, e[5], 1e-9, 1e-3);
				double lev, g;
				double yt = fit.Eval(fit.UT, d, out lev, out g);
				ok &= Rel(yt, e[6], 1e-9) && Math.Abs(yt - e[6]) <= 1e-9 * Math.Max(sc, 1) * 10 && Rel(lev, e[7], 1e-9);
				for (int s = 0; s < 4; s++)
				{
					double yi = fit.Eval(fit.U(samples[s]), d, out lev, out g);
					ok &= Rel(yi, e[8 + 2 * s], 1e-9) && Rel(lev, e[9 + 2 * s], 1e-9);
				}
				Check(ok, $"T1 case {id} n={n} w={(weighted ? "v" : "1")} d={d}: sse {fit.Sse[d]:R} vs {e[0]:R}, sigma {fit.Sigma[d]:R} vs {e[1]:R}, yhat_ut {yt:R} vs {e[6]:R}");
			}
			cases++;
		}
		Check(cases == 50, "T1 fixture has 50 cases (" + cases + ")");
	}

	static double[] Series(int n, double offset, double noise, double[] coef)
	{
		var y = new double[n];
		for (int i = 0; i < n; i++)
		{
			double t = 2.0 * i / (n - 1) - 1;
			y[i] = offset + coef[0] + coef[1] * t + coef[2] * t * t + coef[3] * t * t * t + noise * Gauss();
		}
		return y;
	}

	static double[] Volumes(int n) { var v = new double[n]; for (int i = 0; i < n; i++) v[i] = Math.Round(Math.Exp(6 + Gauss())); return v; }

	// DV-T2: shift / scale invariance.
	static void T2()
	{
		const int n = 240;
		var f0 = new DvpFit(n); var f1 = new DvpFit(n);
		for (int rep = 0; rep < 10; rep++)
		{
			bool wv = rep % 2 == 1;
			var y = Series(n, 4000, 2, new[] { 0.0, 15, -6, 4 }); var v = Volumes(n);
			var ys = new double[n]; var y4 = new double[n];
			for (int i = 0; i < n; i++) { ys[i] = y[i] + 1e6; y4[i] = y[i] * 4; }
			f0.Fit(y, v, n, 3, wv); f1.Fit(ys, v, n, 3, wv);
			bool ok = true;
			for (int d = 0; d <= 3; d++)
			{
				ok &= Rel(f1.Sigma[d], f0.Sigma[d], 1e-9);
				for (int i = 0; i < n; i++) ok &= Math.Abs(f1.Resid(i, d) - f0.Resid(i, d)) <= 1e-9 * f0.Sigma[d];
			}
			Check(ok, "T2 shift 1e6 leaves residuals and sigma unchanged (rep " + rep + ")");
			f1.Fit(y4, v, n, 3, wv);
			bool ok4 = true;
			for (int d = 0; d <= 3; d++) ok4 &= Rel(f1.Sigma[d], 4 * f0.Sigma[d], 1e-9);
			Check(ok4, "T2 y x 4 scales sigma by 4 (rep " + rep + ")");
		}
		// volume scale invariance of the weighted fit (normalised weights, App E)
		{
			var y = Series(n, 4000, 2, new[] { 0.0, 5, 3, -2 }); var v = Volumes(n); var v10 = new double[n];
			for (int i = 0; i < n; i++) v10[i] = v[i] * 10;
			f0.Fit(y, v, n, 3, true); f1.Fit(y, v10, n, 3, true);
			Check(Rel(f1.Sigma[3], f0.Sigma[3], 1e-12) && Rel(f1.SumW, n, 1e-12), "T2 weighted sigma invariant to volume units; sum w = n");
		}
	}

	// DV-T3: SSE non-increasing in d, SSE >= 0, N_k <= 0 forces the fallback path.
	static void T3()
	{
		var f = new DvpFit(1000);
		bool mono = true, nonneg = true;
		for (int rep = 0; rep < 40; rep++)
		{
			int n = new[] { 12, 30, 240, 1000 }[rep % 4];
			var y = Series(n, 4000, rep % 5 == 0 ? 0 : 1.5, new[] { 0.0, 3, 2, 1 });
			f.FitRaw(y, Volumes(n), n, 3, rep % 2 == 1);
			for (int d = 0; d <= 3; d++) { nonneg &= f.Sse[d] >= 0; if (d > 0) mono &= f.Sse[d] <= f.Sse[d - 1] * (1 + 1e-12) + 1e-18; }
		}
		Check(mono, "T3 SSE non-increasing in d");
		Check(nonneg, "T3 SSE >= 0");
		// constant series: SSE = 0 at every d, sigma 0, no NaN
		var c = new double[30]; for (int i = 0; i < 30; i++) c[i] = 4000.25;
		f.FitRaw(c, null, 30, 3, false);
		Check(f.Sse[0] == 0 && f.Sigma[0] == 0 && f.R2[0] == 0 && !double.IsNaN(f.Sse[3]), "T3 constant window: SSE 0, sigma 0, R2 0");
		// weights on two bars only: p_2 vanishes on the support (N_2 = 0) -> breakdown, fallback, degree capped at 1
		var y2 = Series(30, 4000, 1, new[] { 0.0, 3, 2, 1 }); var v2 = new double[30]; v2[3] = 100; v2[20] = 50;
		int fc = f.FallbackCount;
		f.Fit(y2, v2, 30, 3, true);
		Check(f.Valid && f.UsedFallback && f.MaxValidDeg == 1 && f.FallbackCount == fc + 1, "T3 N_2 <= 0 triggers the fallback (maxValidDeg " + f.MaxValidDeg + ", fallback " + f.UsedFallback + ")");
		Check(f.Sse[2] >= 0 && f.Sse[3] == f.Sse[2] && Math.Abs(f.Sse[1]) < 1e-12, "T3 fallback SSE from the residual pass, higher degrees repeat");
		f.Choose(3, false);
		Check(f.Deg == 1, "T3 Choose clamps to the valid degree after a breakdown");
		// three supported bars: breakdown at k = 3
		v2[11] = 70; f.Fit(y2, v2, 30, 3, true);
		Check(f.UsedFallback && f.MaxValidDeg == 2, "T3 three supported bars: breakdown at k = 3");
		// all-zero volume: App E falls back to w = 1
		f.Fit(y2, new double[30], 30, 3, true);
		Check(f.WeightsFellBack && !f.UsedFallback && f.MaxValidDeg == 3, "T3 zero volume sum falls back to w = 1");
		// a normal fit clears the flag
		f.Fit(y2, null, 30, 3, false);
		Check(!f.UsedFallback, "T3 a regular fit does not use the fallback");
	}

	// DV-T4: exact recovery.
	static void T4()
	{
		var f = new DvpFit(240);
		foreach (int n in new[] { 12, 30, 240 })
		{
			double[] cf = { 3.5, -2, 1.25, 0.75 };
			var y = new double[n];
			Func<double, double> cub = u => 4000 + cf[0] + cf[1] * u + cf[2] * u * u + cf[3] * u * u * u;
			for (int i = 0; i < n; i++) y[i] = cub((i - (n - 1) / 2.0) / ((n - 1) / 2.0));
			f.FitRaw(y, null, n, 3, false);
			Check(f.Sse[3] <= 1e-20 * f.Swy2 + 1e-18, $"T4 cubic n={n}: SSE_3 ~ 0 ({f.Sse[3]:E2})");
			Check(Rel(f.Yhat(f.UT, 3), cub(f.UT), 1e-12), $"T4 cubic n={n}: exact yhat(u_t)");
			for (int i = 0; i < n; i++) y[i] = 100 + 7 * i;
			f.FitRaw(y, null, n, 3, false);
			Check(Math.Abs(f.C[2]) <= 1e-12 * Math.Abs(f.C[1]) && Math.Abs(f.C[3]) <= 1e-12 * Math.Abs(f.C[1]), $"T4 linear n={n}, d=3: C2 = C3 ~ 0");
			Check(Rel(f.SlopeEnd(1), 7, 1e-12) && Rel(f.SlopeEnd(3), 7, 1e-9), $"T4 linear n={n}: slopeEnd = 7 per bar");
		}
	}

	// DV-T5: in-sample coverage drill.
	static void T5()
	{
		const int n = 240, W = 2000;
		var f = new DvpFit(n);
		double sum = 0, sum1 = 0;
		for (int w = 0; w < W; w++)
		{
			var y = Series(n, 4000, 1 + s_rng.NextDouble(), new[] { 0.0, 20 * Gauss(), 6 * Gauss(), 3 * Gauss() });
			f.Fit(y, null, n, 3, false);
			sum += f.CovIn(3, 2); sum1 += f.CovIn(1, 2);
		}
		double m = sum / W;
		Check(m >= 0.94 && m <= 0.965, "T5 mean cov_in(2 sigma) d=3 in [0.94, 0.965]: " + m.ToString("F4"));
		Console.WriteLine($"  T5 mean cov_in(2s): d=3 {m:F4}, d=1 {sum1 / W:F4}");
	}

	// DV-T6: leverage.
	static void T6()
	{
		var f = new DvpFit(240);
		foreach (int n in new[] { 30, 240 })
		{
			var y = Series(n, 100, 1, new[] { 0.0, 1, 1, 1 });
			f.FitRaw(y, null, n, 3, false);
			for (int d = 0; d <= 3; d++)
			{
				double s = 0, hmin = double.MaxValue, umin = 0;
				for (int i = 0; i < n; i++) { double u = f.U(i), h = f.Leverage(u, d); s += h; if (h < hmin) { hmin = h; umin = u; } }
				Check(Math.Abs(s - (d + 1)) <= 1e-9, $"T6 n={n} d={d}: sum h(u_i) = d+1 ({s:R})");
				if (d >= 1)
				{
					Check(Math.Abs(umin) <= 0.5 && f.Leverage(0, d) < f.Leverage(1, d), $"T6 n={n} d={d}: h minimal near u=0 (argmin u={umin:F3})");
					Check(f.Leverage(f.UT, d) > f.Leverage(1, d), $"T6 n={n} d={d}: h(u_t) > h(1)");
				}
			}
		}
	}

	// DV-T7: BIC formula; BIC never changes the chosen degree.
	static void T7()
	{
		const int n = 240;
		var f = new DvpFit(n);
		var y = Series(n, 4000, 1, new[] { 0.0, 30, -12, 8 });
		f.Fit(y, null, n, 3, false);
		bool ok = true; int argmin = 0;
		for (int d = 0; d <= 3; d++) { ok &= Rel(f.Bic[d], n * Math.Log(f.Sse[d] / n) + (d + 2) * Math.Log(n), 1e-14, 1); if (f.Bic[d] < f.Bic[argmin]) argmin = d; }
		Check(ok, "T7 BIC_d = n ln(SSE_d/n) + (d+2) ln n");
		Check(argmin == 3, "T7 test series has BIC argmin 3 (" + argmin + ")");
		foreach (EDvpDegMode mode in new[] { EDvpDegMode.Fixed1, EDvpDegMode.Calibrate, EDvpDegMode.DevCoverageFrozen })
		{
			bool latch = true; int d1, d2; bool cal, warn;
			DvpDegree.Select(mode, n, 3, mode == EDvpDegMode.DevCoverageFrozen, 2, ref latch, out d1, out cal, out warn);
			f.Choose(d1, cal); int before = f.Deg;
			for (int d = 0; d <= 3; d++) f.Bic[d] = d == 0 ? double.NegativeInfinity : 1e300;   // scramble BIC
			DvpDegree.Select(mode, n, 3, mode == EDvpDegMode.DevCoverageFrozen, 2, ref latch, out d2, out cal, out warn);
			f.Choose(d2, cal);
			Check(d1 != argmin && before == f.Deg && d1 == d2, "T7 " + mode + ": chosen d " + d1 + " is independent of BIC");
		}
		string core = CoreSource();
		Check(Count(core, "Bic[") == 2 && core.Contains("Bic[k] = "), "T7 source scan: Bic[] is only written (Finish), never read for selection");
	}

	// DV-T8: degree rules.
	static void T8()
	{
		Check(DvpDegree.Cap(12, 3) == 1 && DvpDegree.Cap(16, 3) == 2 && DvpDegree.Cap(24, 3) == 3 && DvpDegree.Cap(240, 3) == 3, "T8 dCap for n = 12/16/24/240 = 1/2/3/3");
		Check(DvpDegree.Cap(240, 1) == 1 && DvpDegree.Cap(240, 2) == 2 && DvpDegree.Cap(11, 3) == 0, "T8 dCap respects degMax; n < 12 -> 0");
		var f = new DvpFit(240); var y = Series(240, 50, 1, new[] { 0.0, 1, 2, 3 });
		Check(!f.Fit(y, null, 11, 3, false) && !f.Valid, "T8 n < 12 gives no fit");
		Check(f.Fit(y, null, 12, 3, false) && f.DTop == 1, "T8 n = 12 fits up to dCap 1");
		bool latch = false, cal, warn; int d;
		Check(DvpDegree.Select(EDvpDegMode.Fixed1, 240, 3, false, 0, ref latch, out d, out cal, out warn) && d == 1 && !cal && !warn && !latch, "T8 Fixed1 -> d = 1");
		var table = new[] { new DvpFrozenDegree.Entry("ES", 1, 240, 2), new DvpFrozenDegree.Entry("NQ", 1, 240, 3) };
		int ds; bool has = DvpFrozenDegree.TryGet(table, "ES", 1, 240, out ds);
		Check(has && ds == 2 && !DvpFrozenDegree.TryGet(table, "ES", 2, 240, out ds) && !DvpFrozenDegree.TryGet(table, "es", 1, 240, out ds), "T8 frozen table lookup by (root, stream, winLen)");
		DvpFrozenDegree.TryGet(table, "ES", 1, 240, out ds);
		Check(DvpDegree.Select(EDvpDegMode.DevCoverageFrozen, 240, 3, true, ds, ref latch, out d, out cal, out warn) && d == 2 && !cal && !warn, "T8 DevCoverageFrozen with an entry -> d*");
		Check(DvpDegree.Select(EDvpDegMode.DevCoverageFrozen, 16, 3, true, 3, ref latch, out d, out cal, out warn) && d == 2, "T8 DevCoverageFrozen -> min(d*, dCap)");
		Check(!DvpFrozenDegree.TryGet("ES", 1, 240, out ds) && DvpFrozenDegree.Table.Length == 0, "T8 shipped DvpFrozenDegree table is empty");
		latch = false;
		bool w1, w2, c1, c2; int a1, a2;
		DvpDegree.Select(EDvpDegMode.DevCoverageFrozen, 240, 3, false, 0, ref latch, out a1, out c1, out w1);
		DvpDegree.Select(EDvpDegMode.DevCoverageFrozen, 240, 3, false, 0, ref latch, out a2, out c2, out w2);
		Check(a1 == 1 && a2 == 1 && c1 && c2 && w1 && !w2, "T8 DevCoverageFrozen without an entry -> Calibrate (d = 1) with one Warning");
		latch = false;
		Check(DvpDegree.Select(EDvpDegMode.Calibrate, 240, 3, false, 0, ref latch, out d, out cal, out warn) && d == 1 && cal && !warn, "T8 Calibrate -> every d fitted, reported d = 1, no warning");
		Check(!DvpDegree.Select(EDvpDegMode.Calibrate, 11, 3, false, 0, ref latch, out d, out cal, out warn), "T8 Select: n < 12 -> no fit");
		// Calibrate: all degrees available side by side; degree-1 shadow equals a separate d = 1 fit
		f.Fit(y, null, 240, 3, false); f.Choose(3, false);
		var g = new DvpFit(240); g.FitRaw(y, null, 240, 1, false);
		Check(f.DTop == 3 && f.Deg == 3 && Rel(f.Sigma[1], g.Sigma[1], 1e-12) && Rel(f.Yhat(f.UT, 1), g.Yhat(g.UT, 1), 1e-13) && Rel(f.SlopeEnd(1), g.SlopeEnd(1), 1e-12), "T8 degree-1 shadow falls out of the nested fit");
	}

	// DV-T20: cone.
	static void T20()
	{
		var f = new DvpFit(240); var y = Series(240, 4000, 2, new[] { 0.0, 10, 4, -3 });
		f.Fit(y, null, 240, 3, false); f.Choose(3, false);
		bool ok = true, flare = true; double prevW = 0;
		for (int h = 1; h <= 60; h++)
		{
			double lo, hi, lo2, hi2, lev, g;
			f.Cone(h, 2, EDvpConeMode.Prediction, out lo, out hi);
			f.Cone(h, 2, EDvpConeMode.Confidence, out lo2, out hi2);
			double u = 1 + 2.0 * h / 239, yh = f.Eval(u, 3, out lev, out g), s = f.Sigma[3];
			ok &= Rel(lo, yh - 2 * s * Math.Sqrt(1 + lev), 1e-14) && Rel(hi, yh + 2 * s * Math.Sqrt(1 + lev), 1e-14)
				&& Rel(lo2, yh - 2 * s * Math.Sqrt(lev), 1e-14) && Rel(hi2, yh + 2 * s * Math.Sqrt(lev), 1e-14);
			flare &= hi - lo > prevW; prevW = hi - lo;
		}
		Check(ok, "T20 cone = yhat -+ coneMult sigma sqrt(1+h) (Prediction) / sqrt(h) (Confidence)");
		Check(flare, "T20 cone flares with the horizon");
		// Rails/Confluence/FamiliesAt arrive in PR-D2/D5; until then the core must not turn the cone into a level.
		string core = CoreSource();
		Check(Count(core, "Cone(") == 1 && !core.Contains("LevelRef"), "T20 source scan: Cone has no caller in <dvp-core> and the core builds no LevelRef (re-checked against Rails/Confluence in PR-D5)");
	}

	// DV-T23: config.
	static void T23()
	{
		var c = new DvpConfig();
		Check(c.Validate() == null, "T23 defaults validate");
		Check(Rel(c.VaFraction, 0.7, 1e-15), "T23 vaPct 70 is passed as the fraction");
		var bad = new List<KeyValuePair<string, Action<DvpConfig>>>
		{
			new KeyValuePair<string, Action<DvpConfig>>("winLen 29", x => x.winLen = 29), new KeyValuePair<string, Action<DvpConfig>>("winLen 1001", x => x.winLen = 1001),
			new KeyValuePair<string, Action<DvpConfig>>("degMax 0", x => x.degMax = 0), new KeyValuePair<string, Action<DvpConfig>>("degMax 4", x => x.degMax = 4),
			new KeyValuePair<string, Action<DvpConfig>>("minR2 1.1", x => x.minR2 = 1.1), new KeyValuePair<string, Action<DvpConfig>>("minR2 NaN", x => x.minR2 = double.NaN),
			new KeyValuePair<string, Action<DvpConfig>>("manRows 7", x => x.manRows = 7), new KeyValuePair<string, Action<DvpConfig>>("manRows 257", x => x.manRows = 257),
			new KeyValuePair<string, Action<DvpConfig>>("manSpan 0.5", x => x.manSpan = 0.5), new KeyValuePair<string, Action<DvpConfig>>("manSpan 7", x => x.manSpan = 7),
			new KeyValuePair<string, Action<DvpConfig>>("vaPct 0.7 (fraction)", x => x.vaPct = 0.7), new KeyValuePair<string, Action<DvpConfig>>("vaPct 96", x => x.vaPct = 96),
			new KeyValuePair<string, Action<DvpConfig>>("nodeProm 0", x => x.nodeProm = 0), new KeyValuePair<string, Action<DvpConfig>>("nodeProm 0.6", x => x.nodeProm = 0.6),
			new KeyValuePair<string, Action<DvpConfig>>("maxNodes -1", x => x.maxNodes = -1), new KeyValuePair<string, Action<DvpConfig>>("maxNodes 11", x => x.maxNodes = 11),
			new KeyValuePair<string, Action<DvpConfig>>("sigmaList empty", x => x.sigmaList = new double[0]), new KeyValuePair<string, Action<DvpConfig>>("sigmaList 0", x => x.sigmaList = new[] { 1.0, 0 }),
			new KeyValuePair<string, Action<DvpConfig>>("projBars 0", x => x.projBars = 0), new KeyValuePair<string, Action<DvpConfig>>("projBars 501", x => x.projBars = 501),
			new KeyValuePair<string, Action<DvpConfig>>("coneMult 0", x => x.coneMult = 0),
			new KeyValuePair<string, Action<DvpConfig>>("fibRanges 0", x => x.fibRanges = 0), new KeyValuePair<string, Action<DvpConfig>>("fibRanges 5", x => x.fibRanges = 5),
			new KeyValuePair<string, Action<DvpConfig>>("fibPivot 1", x => x.fibPivot = 1), new KeyValuePair<string, Action<DvpConfig>>("fibLookMult 0", x => x.fibLookMult = 0),
			new KeyValuePair<string, Action<DvpConfig>>("fibMerge -0.1", x => x.fibMerge = -0.1), new KeyValuePair<string, Action<DvpConfig>>("fibNear 0", x => x.fibNear = 0),
			new KeyValuePair<string, Action<DvpConfig>>("confTol 0", x => x.confTol = 0), new KeyValuePair<string, Action<DvpConfig>>("refitEvery 0", x => x.refitEvery = 0),
			new KeyValuePair<string, Action<DvpConfig>>("streams empty", x => x.streams = new int[0]), new KeyValuePair<string, Action<DvpConfig>>("streams 0", x => x.streams = new[] { 0 }),
			new KeyValuePair<string, Action<DvpConfig>>("streams duplicate", x => x.streams = new[] { 1, 2, 1 }),
		};
		foreach (var kv in bad) { var x = new DvpConfig(); kv.Value(x); var e = x.Validate(); Check(e != null && e.Count >= 1, "T23 Validate rejects " + kv.Key); }
		var okc = new DvpConfig { winLen = 30, degMax = 1, vaPct = 50, manRows = 8, streams = new[] { 1, 2, 3 }, fibRanges = 4, projBars = 500 };
		Check(okc.Validate() == null, "T23 range edges are accepted");

		string k1 = new DvpConfig().Canon(), k2 = new DvpConfig().Canon();
		Check(k1 == k2 && k1 == c.Canon(), "T23 Canon is stable");
		Check(k1.Contains("confMode=FamilyCount") && k1.Contains("minFamilies=3") && k1.Contains("reportWeights=VA:1,POC:0.77") && k1.Contains("rowFloor=1tick")
			&& k1.Contains("rollAdjust=None") && k1.Contains("minBars=12") && k1.Contains("ATR_LEN=20") && k1.Contains("HOLD_BARS=40"), "T23 Canon includes the DV-1.10 constants");
		Check(k1.EndsWith(";frozen=[]}"), "T23 Canon includes the (empty) DvpFrozenDegree table");
		Check(DvpFrozenDegree.Canon(new[] { new DvpFrozenDegree.Entry("ES", 1, 240, 2), new DvpFrozenDegree.Entry("NQ", 2, 120, 1) }) == "[ES:1:240=2,NQ:2:120=1]", "T23 frozen table canon format");
		var muts = new List<Action<DvpConfig>> { x => x.enabled = true, x => x.streams = new[] { 1, 2 }, x => x.src = EDvpSrc.Close, x => x.winLen = 120, x => x.degMode = EDvpDegMode.Fixed1,
			x => x.degMax = 2, x => x.weightVol = true, x => x.minR2 = 0.3, x => x.manRows = 32, x => x.manSpan = 2.5, x => x.vaPct = 68, x => x.nodeProm = 0.11, x => x.maxNodes = 2,
			x => x.sigmaList = new[] { 1.0, 2 }, x => x.projBars = 40, x => x.coneMode = EDvpConeMode.Confidence, x => x.coneMult = 1.5, x => x.useFib = false, x => x.fibRanges = 2,
			x => x.fibPivot = 10, x => x.fibLookMult = 6, x => x.fibAnchor = EDvpFibAnchor.ExtremeInLookback, x => x.fibMerge = 0.3, x => x.fibNear = 10, x => x.confTol = 0.25,
			x => x.splitFlow = true, x => x.refitEvery = 2, x => x.journalFits = false, x => x.journalRails = false };
		var seen = new HashSet<string> { k1 }; bool distinct = true;
		foreach (var m in muts) { var x = new DvpConfig(); m(x); distinct &= seen.Add(x.Canon()); }
		Check(distinct, "T23 every field changes Canon (" + muts.Count + " fields)");
		var inv = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new CultureInfo("de-DE");
		string kDe = new DvpConfig().Canon();
		CultureInfo.CurrentCulture = inv;
		Check(kDe == k1, "T23 Canon is culture-invariant");
	}

	// DV-0.4: no allocation on the fit path after Configure (the full budget is DV-T26 in PR-D2+).
	static void ZeroAlloc()
	{
		const int n = 240;
		var f = new DvpFit(n); var y = Series(n, 4000, 2, new[] { 0.0, 10, 4, -3 }); var v = Volumes(n); var fw = Series(60, 4000, 2, new[] { 0.0, 0, 0, 0 });
		double sink = 0, lo, hi, dw, sk;
		for (int i = 0; i < 50; i++) { f.Fit(y, v, n, 3, i % 2 == 0); f.Cone(5, 2, EDvpConeMode.Prediction, out lo, out hi); }
		long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			f.Fit(y, v, n, 3, i % 2 == 0); f.Choose(3, false);
			f.Cone(1 + i % 60, 2, EDvpConeMode.Prediction, out lo, out hi);
			sink += lo + f.CovIn(3, 2) + f.CovFwd(fw, 60, 3, 2, true) + f.SlopeEnd(1);
			f.ResidualStats(3, out dw, out sk); sink += dw;
		}
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		Check(b1 - b0 == 0, "DV-0.4 zero allocations over 1000 fits + queries (" + (b1 - b0) + " bytes)");
		if (double.IsNaN(sink)) Console.WriteLine("sink NaN");
	}

	static int Main()
	{
		T1(); T2(); T3(); T4(); T5(); T6(); T7(); T8(); T20(); T23(); ZeroAlloc();
		Console.WriteLine($"Dvp: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
