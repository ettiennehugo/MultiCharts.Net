using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PowerLanguage.Function;

// M-DVP PR-D1 (specs/m-dvp-spec.md §15): DV-T1..T8, T20, T23 on the extracted <dvp-core> fit.
static partial class Program
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
		int jf = core.IndexOf("public static string FitLine(", StringComparison.Ordinal), je = core.IndexOf("static void kv(", StringComparison.Ordinal);
		if (jf > 0 && je > jf) core = core.Remove(jf, je - jf);   // PR-D3: the DVP FIT journal formatter prints bic[1..dCap] (DV-11.1); it selects nothing
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
		Check(Count(core, "Cone(") == 1, "T20 source scan: Cone has no caller in <dvp-core> (rails never read it; Confluence/FamiliesAt re-checked in PR-D5)");
		// PR-D2: the cone bounds never appear among the rails
		var man = new DvpManifold(64, false); var rails = new DvpRails(3);
		var bh = new double[240]; var low = new double[240]; var vol = Volumes(240);
		for (int i = 0; i < 240; i++) { bh[i] = y[i] + 1; low[i] = y[i] - 1; }
		man.Build(f, bh, low, vol, null, null, 3, 0.25, 0.7, 0.12, 3); rails.Build(f, man, new[] { 1.0, 2, 3 });
		bool none = rails.Count > 0;
		for (int h = 1; h <= 60; h++)
		{
			double cl, ch; f.Cone(h, 2, EDvpConeMode.Prediction, out cl, out ch);
			for (int i = 0; i < rails.Count; i++) none &= rails.Items[i].price != cl && rails.Items[i].price != ch;
		}
		Check(none, "T20 no cone bound is a rail");
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

	// ---------------- PR-D2: manifold and rails ----------------

	// A synthetic window: HL2 series y, bars of random half-range around it, volumes with ~5% zeros, up/down split.
	static void Window(int n, double noise, double[] coef, out double[] y, out double[] hi, out double[] lo, out double[] v, out double[] up, out double[] dn)
	{
		y = Series(n, 4000, noise, coef); hi = new double[n]; lo = new double[n]; v = Volumes(n); up = new double[n]; dn = new double[n];
		for (int i = 0; i < n; i++)
		{
			double r = Math.Abs(Gauss()) * noise * 0.8 + (i % 17 == 0 ? 0 : 0.01 * noise);
			hi[i] = y[i] + r; lo[i] = y[i] - r;
			if (s_rng.NextDouble() < 0.05) v[i] = 0;
			double sh = s_rng.NextDouble(); up[i] = v[i] * sh; dn[i] = v[i] - up[i];
		}
	}

	static DvpFit FitWindow(double[] y, int n, int deg)
	{
		var f = new DvpFit(n); f.Fit(y, null, n, 3, false); f.Choose(deg, false); return f;
	}

	// DV-T9a: shape stats.
	static void T9a()
	{
		var e = _ProfileStats.Entropy(new double[] { 1, 1, 0, 1, 1 }, EEntropyRows.TradedRows, 0);
		Check(Rel(e.Hnorm, 1.0, 1e-15) && Rel(e.H, Math.Log(4), 1e-15) && double.IsNaN(e.HnormMm) && double.IsNaN(e.Hmm), "T9a entropy {1,1,0,1,1}: Hnorm = 1, H = ln 4, mmN = 0 -> HnormMm NaN");
		var rows = new double[] { 0, 2, 8, 4, 0, 2, 0, 0 };   // sum 16, R = 8, occupied 4, poc = 2
		double c, co; DvpManifold.Shape(rows, _ProfileMath.PocIndex(rows), out c, out co);
		Check(Rel(c, 8.0 / (16.0 / 8), 1e-15) && Rel(co, 8.0 / (16.0 / 4), 1e-15), "T9a concentration = rows[poc] / (sum / manRowsEff) = 4; concentrationOcc = 2");
		Check(double.IsNaN(_ProfileStats.Entropy(new double[] { 0, 3, 0 }, EEntropyRows.TradedRows, 0).Hnorm), "T9a one occupied row -> Hnorm NaN");
	}

	// DV-T9: deposit and conservation.
	static void T9()
	{
		// rows of height 1 over [-3, 3): R = 6. A bar over [-2.5, 0) covers half of row 0... use [-2.5, 0): rows 0 (0.5), 1, 2 -> 0.2/0.4/0.4
		var rows = new double[6];
		double tail = DvpManifold.Deposit(rows, 6, 3, 1, -2.5, 0, 10);
		Check(tail == 0 && Rel(rows[0], 2, 1e-15) && Rel(rows[1], 4, 1e-15) && Rel(rows[2], 4, 1e-15) && rows[3] == 0, "T9 bar spanning 2.5 rows splits 0.2/0.4/0.4");
		Array.Clear(rows, 0, 6);
		tail = DvpManifold.Deposit(rows, 6, 3, 1, 0.3, 0.3, 7);
		Check(tail == 0 && rows[3] == 7, "T9 zero-range bar deposits into one row");
		Array.Clear(rows, 0, 6);
		Check(DvpManifold.Deposit(rows, 6, 3, 1, 3.2, 4, 5) == 5 && DvpManifold.Deposit(rows, 6, 3, 1, 3, 3, 5) == 5 && DvpManifold.Deposit(rows, 6, 3, 1, -9, -3.5, 5) == 5 && rows[5] == 0, "T9 outside bars (and +span exactly) go to tailVol");
		tail = DvpManifold.Deposit(rows, 6, 3, 1, 2, 4, 10);
		Check(Rel(tail, 5, 1e-15) && Rel(rows[5], 5, 1e-15), "T9 straddling bar: in-span share to rows, rest to tailVol");
		Check(DvpManifold.Deposit(rows, 6, 3, 1, 0, 1, double.NaN) == 0 && DvpManifold.Deposit(rows, 6, 3, 1, 0, 1, -3) == 0, "T9 NaN/negative volume deposits nothing");
		bool cons = true, consSplit = true;
		var man = new DvpManifold(64, true);
		for (int rep = 0; rep < 50; rep++)
		{
			int n = rep % 2 == 0 ? 240 : 30;
			double[] y, hi, lo, v, up, dn; Window(n, 1 + 3 * s_rng.NextDouble(), new[] { 0.0, 10 * Gauss(), 4 * Gauss(), 2 * Gauss() }, out y, out hi, out lo, out v, out up, out dn);
			var f = FitWindow(y, n, rep % 4);
			man.Build(f, hi, lo, v, up, dn, 1 + rep % 3, 0.25, 0.7, 0.12, 3);
			double sv = 0; foreach (double x in v) if (x > 0) sv += x;
			double sr = 0; foreach (double x in man.Rows) sr += x;
			cons &= Rel(sr + man.TailVol, sv, 1e-9) && Rel(man.TotalVol, sv, 1e-12);
			double su = 0, sd = 0, vu = 0, vd = 0;
			foreach (double x in man.UpRows) su += x; foreach (double x in man.DownRows) sd += x;
			for (int i = 0; i < n; i++) if (v[i] > 0) { vu += up[i]; vd += dn[i]; }
			consSplit &= Rel(su + man.UpTail, vu, 1e-9) && Rel(sd + man.DownTail, vd, 1e-9);
		}
		Check(cons, "T9 conservation: sum rows + tailVol = sum v within 1e-9 (50 windows, manSpan 1..3)");
		Check(consSplit, "T9 splitFlow up/down manifolds conserve their volume");
	}

	// DV-T10: tick floor.
	static void T10()
	{
		Check(DvpManifold.RowsEffFor(64, 3, 0.5, 1) == 3, "T10 sigma = 0.5 tick -> manRowsEff = max(1, floor(6 x 0.5)) = 3");
		Check(DvpManifold.RowsEffFor(64, 3, 0.1, 1) == 1 && DvpManifold.RowsEffFor(64, 3, 100, 1) == 64 && DvpManifold.RowsEffFor(64, 3, 0.5, 0) == 64, "T10 floor at 1 row; no floor for wide rows or tick <= 0");
		Check(DvpManifold.RowsEffFor(64, 3, 64.0 / 6, 1) == 64 && DvpManifold.RowsEffFor(64, 3, 64.0 / 6 - 1e-9, 1) == 63, "T10 boundary: row = 1 tick keeps manRows");
		double[] y, hi, lo, v, up, dn; Window(240, 0.1, new[] { 0.0, 0.5, 0, 0 }, out y, out hi, out lo, out v, out up, out dn);
		var f = FitWindow(y, 240, 1); var man = new DvpManifold(64, false);
		man.Build(f, hi, lo, v, null, null, 3, 0.25, 0.7, 0.12, 3);
		double sr = 0; foreach (double x in man.Rows) sr += x;
		Check(man.RowsEff == DvpManifold.RowsEffFor(64, 3, f.SigmaD, 0.25) && man.RowsEff < 64 && man.Rows.Length == man.RowsEff && Rel(man.Wz, 6.0 / man.RowsEff, 1e-15)
			&& Rel(sr + man.TailVol, man.TotalVol, 1e-9), "T10 build on a sub-tick sigma uses manRowsEff = " + man.RowsEff + " (exact-length rows, conserved)");
	}

	// DV-T11: value area.
	static void T11()
	{
		var man = new DvpManifold(64, false); bool ok = true;
		for (int rep = 0; rep < 20; rep++)
		{
			double[] y, hi, lo, v, up, dn; Window(240, 2, new[] { 0.0, 8, -3, 2 }, out y, out hi, out lo, out v, out up, out dn);
			var f = FitWindow(y, 240, 3);
			man.Build(f, hi, lo, v, null, null, 3, 0.25, new DvpConfig().VaFraction, 0.12, 3);
			int l, h, p; _ProfileMath.ValueAreaExpansion(man.Rows, 0.70, out l, out h, out p);
			ok &= man.Valid && man.VaLo == l && man.VaHi == h && man.Poc == p && Rel(man.PocZ, -3 + (p + 0.5) * man.Wz, 1e-12)
				&& Rel(man.VaLoZ, -3 + l * man.Wz, 1e-12) && Rel(man.VaHiZ, -3 + (h + 1) * man.Wz, 1e-12);
		}
		Check(ok, "T11 VA = ValueAreaExpansion(rows, 0.70); vaLoZ/vaHiZ are the outer row edges, pocZ the POC row centre");
		bool threw = false;
		try { double[] y, hi, lo, v, up, dn; Window(60, 2, new[] { 0.0, 1, 0, 0 }, out y, out hi, out lo, out v, out up, out dn); man.Build(FitWindow(y, 60, 1), hi, lo, v, null, null, 3, 0.25, 70, 0.12, 3); }
		catch (ArgumentOutOfRangeException) { threw = true; }
		var bad = new DvpConfig { vaPct = 0.7 };
		Check(threw && bad.Validate() != null, "T11 passing 70 as the fraction is asserted; vaPct 0.7 is rejected by Validate");
		Check(_ProfileMath.PocIndex(new double[] { 0, 5, 1, 5, 2, 0 }) == 3 && _ProfileMath.PocIndex(new double[] { 5, 0, 5 }) == 0, "T11 POC tie rule: nearest the volume-weighted mean row, then the lower row");
	}

	// DV-T12: prominence and node selection.
	static void T12()
	{
		// (a) bit-identity of Gate0/Profile outputs is DV-T12a in PR-D0b (#33); their suites run unchanged in this PR.
		var rows = new double[] { 1, 3, 10, 7, 4.8, 5.5, 6, 2, 0.5 };
		var hv = new List<double[]>(); var lv = new List<double[]>();
		_ProfileMath.Prominence(rows, 0.11, hv, lv);
		Check(hv.Count == 2 && lv.Count == 1 && (int)hv[0][0] == 2 && (int)hv[1][0] == 6 && (int)lv[0][0] == 4, "T12b bimodal rows: 2 HVN + 1 LVN at prominence 0.11");
		_ProfileMath.Prominence(rows, 0.13, hv, lv);
		Check(hv.Count == 1 && lv.Count == 0, "T12b prominence 0.13 drops the 0.12 peak and its LVN");
		var r = new double[21]; for (int i = 0; i < 21; i++) r[i] = 1;
		r[2] = 6; r[5] = 4; r[7] = 5; r[10] = 10; r[11] = 10; r[14] = 7; r[17] = 3; r[19] = 8;
		int poc = _ProfileMath.PocIndex(r);
		var man = new DvpManifold(64, false);
		man.SelectNodes(r, poc, 0.12, 2, 3);
		bool near = true; for (int i = 0; i < man.HvnCount; i++) near &= Math.Abs(man.Hvn[i].row - poc) > 1;
		Check(poc == 11 && near && man.HvnCount == 4 && man.Hvn[0].row == 2 && man.Hvn[1].row == 7 && man.Hvn[2].row == 14 && man.Hvn[3].row == 19,
			"T12c maxNodes = 2 per side by prominence; the HVN next to POC (row 10) is excluded");
		Check(man.Hvn[0].side == -1 && man.Hvn[3].side == 1 && Rel(man.Hvn[2].z, -3 + 14.5 * 6.0 / 21, 1e-12) && Rel(man.Hvn[2].prom, 0.6, 1e-12), "T12c node side, z and prominence");
		int lvBelow = 0, lvAbove = 0; for (int i = 0; i < man.LvnCount; i++) { if (man.Lvn[i].side < 0) lvBelow++; else lvAbove++; }
		Check(lvBelow <= 2 && lvAbove <= 2 && man.LvnCount >= 2, "T12c LVNs capped at maxNodes per side (" + lvBelow + "/" + lvAbove + ")");
		man.SelectNodes(r, poc, 0.12, 1, 3);
		Check(man.HvnCount == 2 && man.Hvn[0].row == 2 && man.Hvn[1].row == 19, "T12c maxNodes = 1 keeps the most prominent per side");
		man.SelectNodes(r, poc, 0.12, 0, 3);
		Check(man.HvnCount == 0 && man.LvnCount == 0, "T12c maxNodes = 0 -> no nodes");
		var t = new double[] { 1, 4, 1, 4, 1, 10, 1, 4, 1 };
		man.SelectNodes(t, 5, 0.12, 1, 3);
		Check(man.HvnCount == 2 && man.Hvn[0].row == 3 && man.Hvn[1].row == 7, "T12c prominence tie goes to the row nearer POC");
	}

	// DV-T13: rails.
	static void T13()
	{
		const int n = 240;
		double[] y, hi, lo, v, up, dn; Window(n, 2, new[] { 0.0, 12, -5, 3 }, out y, out hi, out lo, out v, out up, out dn);
		var f = FitWindow(y, n, 3); var man = new DvpManifold(64, false); var rails = new DvpRails(3);
		man.Build(f, hi, lo, v, null, null, 3, 0.25, 0.7, 0.12, 3);
		Check(rails.Build(f, man, new[] { 1.0, 2, 3 }), "T13 rails build");
		double lev, g, yt = f.Eval(1 + 2.0 / (n - 1), 3, out lev, out g);
		Check(Rel(rails.YhatT, yt, 1e-15) && rails.Sigma == f.SigmaD && rails.Deg == 3, "T13 rails use the t-1 fit at u_t = 1 + 2/(n-1)");
		bool price = true, sorted = true; int np = 0, nva = 0, nvl = 0, nh = 0, nl = 0, ns = 0;
		for (int i = 0; i < rails.Count; i++)
		{
			var x = rails.Items[i];
			price &= Rel(x.price, yt + x.z * f.SigmaD, 1e-15);
			if (i > 0) sorted &= rails.Items[i - 1].z <= x.z;
			switch (x.kind) { case ELevelKind.DvpPoc: np++; break; case ELevelKind.DvpVah: nva++; break; case ELevelKind.DvpVal: nvl++; break; case ELevelKind.DvpHvn: nh++; break; case ELevelKind.DvpLvn: nl++; break; case ELevelKind.DvpSigma: ns++; break; default: price = false; break; }
		}
		Check(price, "T13 every rail price = yhat_{t-1}(u_t) + z sigma_{t-1}");
		Check(sorted, "T13 rails sorted by z (residual order, DV-4.9)");
		Check(np == 1 && nva == 1 && nvl == 1 && nh == man.HvnCount && nl == man.LvnCount && ns == 6, $"T13 rail kinds: Poc/Vah/Val once, {nh} Hvn, {nl} Lvn, 6 sigma");
		Check(Rel(rails.RailPrice(ELevelKind.DvpPoc, 99), yt + man.PocZ * f.SigmaD, 1e-15) && Rel(rails.RailPrice(ELevelKind.DvpSigma, -2), yt - 2 * f.SigmaD, 1e-15)
			&& double.IsNaN(rails.RailPrice(ELevelKind.DvpSigma, 2.5)) && double.IsNaN(rails.RailPrice(ELevelKind.DvpFib, 0)), "T13 RailPrice: unique kinds ignore z, sigma by z, NaN if absent");
		Check(Rel(rails.Z(rails.Price(1.7)), 1.7, 1e-12), "T13 Z inverts P_t(z)");
		int up1 = rails.NextRail(man.PocZ, 1), dn1 = rails.NextRail(man.PocZ, -1);
		Check(up1 >= 0 && rails.Items[up1].z > man.PocZ && (up1 == 0 || rails.Items[up1 - 1].z <= man.PocZ) && dn1 >= 0 && rails.Items[dn1].z < man.PocZ && rails.NextRail(1e9, 1) == -1, "T13 NextRail in residual order");
		Check(DvpRails.Tag(1, new DateTime(2026, 10, 23, 14, 45, 0), 3, ELevelKind.DvpSigma, 2) == "DVP1:202610231445:d3:DvpSigma+2.00"
			&& DvpRails.Tag(2, new DateTime(2026, 1, 2, 9, 5, 0), 1, ELevelKind.DvpVal, -0.8333) == "DVP2:202601020905:d1:DvpVal-0.83"
			&& DvpRails.Tag(1, new DateTime(2026, 1, 2, 9, 5, 0), 1, ELevelKind.DvpPoc, -0.001) == "DVP1:202601020905:d1:DvpPoc+0.00", "T13 tag format DV-10.2");
		var into = new List<LevelRef>(); var pool = new List<LevelRef>();
		DateTime tp = new DateTime(2026, 10, 23, 15, 0, 0), t0 = new DateTime(2026, 10, 13, 9, 30, 0);
		rails.Publish(into, pool, 1, tp, t0, true);
		bool lr = into.Count == rails.Count;
		for (int i = 0; i < into.Count && lr; i++)
		{
			var L = into[i]; var x = rails.Items[i];
			lr &= L.kind == x.kind && L.price == x.price && L.lo == x.price && L.hi == x.price && L.createdTime == tp && L.sourceT0 == t0 && L.sourceT1 == tp
				&& L.dataStream == 1 && L.isRollWindow && !L.crossesRoll && L.tag == DvpRails.Tag(1, tp, 3, x.kind, x.z) && L.StudyType == x.kind.ToString();
		}
		Check(lr, "T13 LevelRefs: kind/price/times/stream/tag per DV-10.1/10.2");
		var first = into[0]; rails.Publish(into, pool, 1, tp, t0, false);
		Check(ReferenceEquals(first, into[0]) && pool.Count == rails.Count, "T13 Publish reuses pooled LevelRef objects");
		var empty = new DvpRails(3);
		Check(!empty.Build(new DvpFit(30), man, new[] { 1.0 }) && empty.Count == 0 && double.IsNaN(empty.RailPrice(ELevelKind.DvpPoc, 0)), "T13 no fit -> no rails");
	}

	// DV-0.4 / DV-13.2: manifold + nodes + rails allocate nothing per refit, across changing manRowsEff.
	static void ZeroAllocD2()
	{
		const int n = 240;
		double[] y, hi, lo, v, up, dn; Window(n, 2, new[] { 0.0, 10, 4, -3 }, out y, out hi, out lo, out v, out up, out dn);
		var ys = new double[n]; var his = new double[n]; var los = new double[n];
		var f = new DvpFit(n); var man = new DvpManifold(64, true); var rails = new DvpRails(3); var sl = new[] { 1.0, 2, 3 };
		double sink = 0;
		for (int i = 0; i < 64; i++) { f.Fit(y, v, n, 3, false); f.Choose(3, false); man.Build(f, hi, lo, v, up, dn, 3, 0.25, 0.7, 0.12, 3); rails.Build(f, man, sl); }
		var seen = new bool[65]; int distinct = 0;
		long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int k = 0; k < 1000; k++)
		{
			double sc = 0.01 + (k % 50) * 0.02;   // shrink the series so the tick floor changes manRowsEff
			for (int i = 0; i < n; i++) { ys[i] = 4000 + (y[i] - 4000) * sc; his[i] = 4000 + (hi[i] - 4000) * sc; los[i] = 4000 + (lo[i] - 4000) * sc; }
			f.Fit(ys, v, n, 3, k % 2 == 0); f.Choose(3, false);
			man.Build(f, his, los, v, up, dn, 3, 0.25, 0.7, 0.12, 3); rails.Build(f, man, sl);
			sink += man.PocZ + rails.Count + rails.RailPrice(ELevelKind.DvpSigma, 2) + rails.NextRail(0, 1);
			if (!seen[man.RowsEff]) { seen[man.RowsEff] = true; distinct++; }
		}
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		Check(b1 - b0 == 0 && distinct >= 5, "DV-13.2 zero allocations over 1000 refits (fit + manifold + nodes + rails), " + distinct + " distinct manRowsEff (" + (b1 - b0) + " bytes)");
		if (double.IsNaN(sink)) Console.WriteLine("sink NaN");
	}

	static int Main()
	{
		T1(); T2(); T3(); T4(); T5(); T6(); T7(); T8(); T20(); T23(); ZeroAlloc();
		T9a(); T9(); T10(); T11(); T12(); T13(); ZeroAllocD2();
		D3Tests();
		Console.WriteLine($"Dvp: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
