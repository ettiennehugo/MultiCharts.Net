using System;
using System.Collections.Generic;
using PM = Extracted.ProfileMath;

// Spec VPB-1 / VPB-2 / VPB-6 / VPB-7 unit tests on synthetic histograms with hand-computed results.
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }

	static void Va(double[] v, double pct, int eLo, int eHi, int ePoc, string name)
	{
		int lo, hi, poc;
		bool ok = PM.ValueAreaExpansion(v, pct, out lo, out hi, out poc);
		Check(ok && lo == eLo && hi == eHi && poc == ePoc, $"VA {name}: got [{lo},{hi}] poc {poc}, expected [{eLo},{eHi}] poc {ePoc}");
	}

	static int Main()
	{
		// VPB-2 VolumeExpansion, 70%.
		// total 23, target 16.1: POC row 3 (10); up pair 4+2=6 > down pair 3+2=5 -> [3,5] 16; up 1 < down 5 -> [1,5] 21.
		Va(new double[] { 1, 2, 3, 10, 4, 2, 1 }, 0.70, 1, 5, 3, "basic");
		// tie: up pair 3+2 = down pair 3+2 -> both added.
		Va(new double[] { 2, 3, 10, 3, 2 }, 0.70, 0, 4, 2, "tie");
		// POC at the low edge: no rows below; up pair 5+4 -> [0,2] 19 >= 17.5.
		Va(new double[] { 10, 5, 4, 3, 2, 1 }, 0.70, 0, 2, 0, "poc at edge");
		// POC at the high edge: total 30, target 21; 20 < 21 so the down pair 4+3 is added -> [2,4].
		Va(new double[] { 1, 2, 3, 4, 20 }, 0.70, 2, 4, 4, "poc at high edge");
		Va(new double[] { 1, 2, 3, 4, 30 }, 0.70, 4, 4, 4, "poc alone holds 70%");
		Va(new double[] { 5 }, 0.70, 0, 0, 0, "single row");
		int l0, h0, p0; Check(!PM.ValueAreaExpansion(new double[] { 0, 0 }, 0.7, out l0, out h0, out p0), "VA empty histogram");

		// POC ties: nearest the volume-weighted mean.
		Check(PM.PocIndex(new double[] { 5, 1, 1, 1, 5, 4, 4 }) == 4, "POC tie -> row nearest the mean");

		// Sigma: symmetric 1-2-1 on prices 10,11,12: mean 11, var = (1+1)/4 = 0.5.
		double vah, val;
		Check(PM.ValueAreaSigma(new double[] { 10, 11, 12 }, new double[] { 1, 2, 1 }, 1.0, out vah, out val)
			&& Near(vah, 11 + Math.Sqrt(0.5)) && Near(val, 11 - Math.Sqrt(0.5)), $"Sigma VA got {val}..{vah}");

		// ToDense on a 0.25 tick, merging duplicates.
		double[] dp, dv;
		Check(PM.ToDense(new double[] { 100.5, 100.0, 100.5 }, new double[] { 2, 1, 3 }, 0.25, out dp, out dv)
			&& dp.Length == 3 && Near(dp[0], 100.0) && Near(dv[0], 1) && Near(dv[1], 0) && Near(dv[2], 5), "ToDense");

		// VPB-6 relationship (Dalton).
		Check(PM.Relationship(110, 100, 120, 111) == PM.ValueRel.Higher, "rel Higher");
		Check(PM.Relationship(110, 100, 99, 90) == PM.ValueRel.Lower, "rel Lower");
		Check(PM.Relationship(110, 100, 108, 102) == PM.ValueRel.Inside, "rel Inside");
		Check(PM.Relationship(110, 100, 112, 98) == PM.ValueRel.Outside, "rel Outside");
		Check(PM.Relationship(110, 100, 115, 105) == PM.ValueRel.OverlappingHigher, "rel OverlappingHigher");
		Check(PM.Relationship(110, 100, 105, 95) == PM.ValueRel.OverlappingLower, "rel OverlappingLower");
		Check(PM.Relationship(0, 100, 105, 95) == PM.ValueRel.Unknown, "rel Unknown");

		// VPB-6 poor extremes: mean of traded rows = 4; floor 2.
		double[] poorTop = { 1, 3, 6, 8, 3, 3 };
		Check(PM.IsPoorExtreme(poorTop, true, 0.5, 2), "poor high");
		Check(!PM.IsPoorExtreme(poorTop, false, 0.5, 2), "not poor low (tapering)");

		// VPB-6 single prints: VA rows [3,5]; 30th percentile of traded rows cut = 1.
		double[] sp = { 1, 1, 1, 9, 9, 9, 1, 5, 1, 1 };
		var prints = PM.SinglePrints(sp, 3, 5, 0.30, 2);
		Check(prints.Count == 2 && prints[0][0] == 0 && prints[0][1] == 2 && prints[1][0] == 8 && prints[1][1] == 9,
			"single prints got " + string.Join(";", prints.ConvertAll(p => p[0] + "-" + p[1])));

		// VPB-6 shapes.
		Check(PM.ProfileShape(new double[] { 1, 2, 4, 8, 10, 8, 4, 2, 1 }, 0.7) == PM.Shape.D, "shape D");
		Check(PM.ProfileShape(new double[] { 1, 1, 1, 1, 2, 3, 9, 12, 10 }, 0.7) == PM.Shape.P, "shape P");
		Check(PM.ProfileShape(new double[] { 10, 12, 9, 3, 2, 1, 1, 1, 1 }, 0.7) == PM.Shape.b, "shape b");
		Check(PM.ProfileShape(new double[] { 2, 10, 3, 1, 1, 1, 2, 9, 3 }, 0.7) == PM.Shape.Double, "shape Double");
		Check(PM.ProfileShape(new double[] { 3, 3, 3, 3, 3, 3, 3, 3, 4 }, 0.7) == PM.Shape.Thin, "shape Thin");

		// VPB-7 balance.
		Check(PM.IsBalanced(new double[] { 110, 111, 109 }, new double[] { 100, 101, 100 }, 0.6), "balanced");
		Check(!PM.IsBalanced(new double[] { 110, 120 }, new double[] { 100, 112 }, 0.6), "not balanced (overlap below 60%)");

		// VPB-6 value-migration score (synthesis 4.0): D = 1.0; POCs oldest first.
		int vm;
		Check(PM.ValueMigrationScore(new double[] { 100, 102, 104, 106 }, 3, 108, 1.0, out vm) && vm == 4, "migration +4 got " + vm);
		Check(PM.ValueMigrationScore(new double[] { 106, 104, 102, 100 }, 3, 98, 1.0, out vm) && vm == -4, "migration -4 got " + vm);
		// +2, -0.5 (below D -> 0), +3, developing -1.5 -> +1 + 0 + 1 - 1 = +1 (neutral).
		Check(PM.ValueMigrationScore(new double[] { 100, 102, 101.5, 104.5 }, 3, 103, 1.0, out vm) && vm == 1, "migration neutral got " + vm);
		// change exactly D counts; no developing term (0) -> 3 terms only.
		Check(PM.ValueMigrationScore(new double[] { 100, 101, 102, 103 }, 3, 0, 1.0, out vm) && vm == 3, "migration no developing got " + vm);
		// only the last n differences are scored (older POCs ignored).
		Check(PM.ValueMigrationScore(new double[] { 50, 100, 102, 104, 106 }, 3, 0, 1.0, out vm) && vm == 3, "migration window got " + vm);
		Check(!PM.ValueMigrationScore(new double[] { 100, 102, 104 }, 3, 0, 1.0, out vm) && vm == 0, "migration needs n + 1 POCs");
		Check(!PM.ValueMigrationScore(new double[] { 100, 0, 104, 106 }, 3, 0, 1.0, out vm), "migration rejects a missing POC");

		// VPB-1 accumulator: modes, determinism and truncation (prefix + continue == one pass; rebuild == stream).
		var bars = new List<double[]>();
		var rnd = new Random(7);
		double px = 4000;
		for (int i = 0; i < 400; i++)
		{
			double o = px, c = px + (rnd.Next(9) - 4) * 0.25, h = Math.Max(o, c) + rnd.Next(3) * 0.25, l = Math.Min(o, c) - rnd.Next(3) * 0.25;
			bars.Add(new[] { o, h, l, c, 100 + rnd.Next(900) }); px = c;
		}
		for (int mode = 1; mode <= 3; mode++)
		{
			var a = new PM.Accumulator(0.25); var b = new PM.Accumulator(0.25);
			for (int i = 0; i < bars.Count; i++) a.Add(i + 1, DateTime.MinValue, bars[i][0], bars[i][1], bars[i][2], bars[i][3], bars[i][4], mode);
			for (int i = 0; i < 200; i++) b.Add(i + 1, DateTime.MinValue, bars[i][0], bars[i][1], bars[i][2], bars[i][3], bars[i][4], mode);
			double[] bp200, bv200; b.Dense(out bp200, out bv200);
			for (int i = 200; i < bars.Count; i++) b.Add(i + 1, DateTime.MinValue, bars[i][0], bars[i][1], bars[i][2], bars[i][3], bars[i][4], mode);
			double[] ap, av, bp, bv; a.Dense(out ap, out av); b.Dense(out bp, out bv);
			bool same = ap.Length == bp.Length;
			for (int i = 0; same && i < ap.Length; i++) same = Near(ap[i], bp[i]) && Near(av[i], bv[i]);
			Check(same, "accumulator replay mode " + mode);
			double total = 0; foreach (double v in av) total += v;
			double expected = 0; foreach (var x in bars) expected += x[4];
			Check(Math.Abs(total - expected) < 1e-6, "accumulator conserves volume mode " + mode);
			Check(a.firstBar == 1 && a.lastBar == 400 && a.bars == 400, "accumulator bar range mode " + mode);
		}
		var u = new PM.Accumulator(1.0); u.Add(1, DateTime.MinValue, 10, 12, 10, 11, 30, 2);
		double[] up, uv; u.Dense(out up, out uv);
		Check(uv.Length == 3 && Near(uv[0], 10) && Near(uv[1], 10) && Near(uv[2], 10), "BarUniform spreads evenly");
		var t = new PM.Accumulator(1.0); t.Add(1, DateTime.MinValue, 10, 12, 9, 12, 30, 3);
		double[] tp, tv; t.Dense(out tp, out tv);
		Check(tv.Length == 1 && Near(tp[0], 11), "BarTypical at (H+L+C)/3");

		// VPB-6 ValueMigrationScore (Rev 2.4 test vectors), n = 3, D = 0.5.
		var vUp = new List<double> { 100, 101, 102, 103 };
		int? v1 = PM.MigrationScore(vUp, 3, 0.5, 103.2);
		Check(v1 == 3 && PM.ClassifyMigration(v1) == PM.MigrationState.Up, "VMS 1: V=3 Up, got " + v1);
		int? v2 = PM.MigrationScore(new List<double> { 100, 100.2, 99.9, 100.1 }, 3, 0.5, 100.3);
		Check(v2 == 0 && PM.ClassifyMigration(v2) == PM.MigrationState.Neutral, "VMS 2: V=0 Neutral, got " + v2);
		int? v3 = PM.MigrationScore(new List<double> { 103, 102, 101, 100 }, 3, 0.5, 99.8);
		Check(v3 == -3 && PM.ClassifyMigration(v3) == PM.MigrationState.Down, "VMS 3: V=-3 Down, got " + v3);
		int? v4 = PM.MigrationScore(new List<double> { 100, 101 }, 3, 0.5, 101.0);
		Check(v4 == null && PM.ClassifyMigration(v4) == PM.MigrationState.Unknown, "VMS 4: Unknown with two sessions");
		Check(PM.MigrationScore(vUp, 3, 0.5, 104) == 4, "VMS developing term counts at >= D (range +4)");
		Check(PM.MigrationScore(vUp, 3, 0.5, null) == 3, "VMS without developing term");
		Check(PM.MigrationStep(100.5, 100, 0.5) == 1 && PM.MigrationStep(100.49, 100, 0.5) == 0, "VMS |a-b| = D counts, < D does not");
		// 5. Truncation: the score at session t uses only POCs vUp to t; appending later sessions to a copy cut at t changes nothing.
		var series = new List<double> { 100, 99, 101, 102, 101.2, 103, 104, 102, 101, 100 };
		bool trunc = true;
		for (int s = 3; s < series.Count; s++)
		{
			int? full = PM.MigrationScore(series.GetRange(0, s + 1), 3, 0.5, null);
			var cut = series.GetRange(0, s + 1);
			int? again = PM.MigrationScore(cut, 3, 0.5, null);
			cut.AddRange(new double[] { 999, -5 }); // later data exists but is not passed for session t
			trunc &= full == again && PM.MigrationScore(cut.GetRange(0, s + 1), 3, 0.5, null) == full;
		}
		Check(trunc, "VMS 5: truncation");

		Console.WriteLine($"ProfileTests: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
