using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PowerLanguage.Function;

// M-DVP PR-D4 (specs/m-dvp-spec.md rev 4 §5, §15): DV-T17 lattice (anchors, levels, merge/filter, ranges), the PA bridge on a real
// _PriceActionEngine driven through Add(), DV-13.3 hosting checks on the host source, and the zero-allocation check of the lattice path.
static partial class Program
{
	static DvpPivots Piv(params object[] q)   // (isHigh, price, extremeBar, confirmBar) quadruples
	{
		var p = new DvpPivots(DvpStream.PivotCapacity);
		for (int i = 0; i < q.Length; i += 4) p.Add((bool)q[i], Convert.ToDouble(q[i + 1], CI), (int)q[i + 2], (int)q[i + 3]);
		return p;
	}

	static double[] Fibs() { var f = new double[DvpLattice.NFib]; for (int i = 0; i < f.Length; i++) f[i] = DvpLattice.FibLevel(i); return f; }

	// Independent reference for one range (DV-5.3/5.4), written without the engine's arrays.
	static List<KeyValuePair<double, double>> RefRange(double a, double b, double atr, double close, double merge, double near)
	{
		var lv = Fibs().Select(f => new KeyValuePair<double, double>(b - f * (b - a), f)).OrderBy(x => x.Key).ThenBy(x => x.Value).ToList();
		var outp = new List<KeyValuePair<double, double>>();
		int g = 0;
		while (g < lv.Count)
		{
			var grp = lv.Skip(g).TakeWhile(x => x.Key - lv[g].Key < merge * atr).ToList();
			var rep = grp.OrderBy(x => Math.Abs(x.Value - Math.Round(x.Value))).ThenBy(x => x.Value).First();
			if (Math.Abs(rep.Key - close) <= near * atr) outp.Add(rep);
			g += grp.Count;
		}
		return outp;
	}

	static void T17()
	{
		{   // rev 5 S4: fibAnchor valid = MostRecentPivot only
			var ok = new DvpConfig { enabled = true }.Validate();
			var bad = new DvpConfig { enabled = true, fibAnchor = EDvpFibAnchor.ExtremeInLookback }.Validate();
			Check(ok == null && bad != null && bad.Count == 1 && bad[0].Contains("fibAnchor") && bad[0].Contains("ExtremeInLookback"), "S4 Validate rejects fibAnchor = ExtremeInLookback, accepts MostRecentPivot");
		}
		{   // Analyst 1 N2: the lattice is built only via dvpConfOf (query APIs + the post-PA dvpRailStep), never in stepDvp
			string h = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
			int st = h.IndexOf("private void stepDvp()", StringComparison.Ordinal);
			string stb = h.Substring(st, h.IndexOf("private ", st + 10, StringComparison.Ordinal) - st);
			Check(!stb.Contains("dvpLattice(") && !stb.Contains("LatticeOf(") && !stb.Contains("DvpPaBridge") && Count(h, "dvpLattice(") == 2,
				"N2 lattice built only via dvpConfOf (query APIs, post-PA dvpRailStep), not in stepDvp");
		}
		var fl = Fibs();
		Check(fl.Length == 24 && fl.SequenceEqual(new[] { 0, .22, .5, .62, .71, .786, .886, 1, 1.414, 1.618, 2.618, 3.618, 4.236, -.146, -.272, -.414, -.764, -1, -2, -2.5, -2.62, -3, -3.5, -4 }),
			"DV-5.3 FIB_LEVELS: the 24 ratios verbatim, in spec order");
		var c = new DvpConfig { enabled = true };
		Check(DvpLattice.Strength(c, 0) == 20 && DvpLattice.Strength(c, 1) == 40 && DvpLattice.Strength(c, 2) == 80 && DvpLattice.Strength(c, 3) == 160,
			"DV-5.1 strengths fibPivot*2^(j-1) = 20/40/80(/160)");

		// --- confirmBar: B (high @ extreme 990) confirms at 1010. At endBar 1009 B is the earlier low, at 1010 the high.
		var p = Piv(false, 90.0, 960, 980, true, 110.0, 990, 1010, true, 105.0, 940, 960);
		double a, b; int ab, bb;
		bool ok9 = DvpLattice.Anchor(p, 1009, 160, out a, out b, out ab, out bb);
		Check(ok9 && b == 90 && a == 105 && bb == 960 && ab == 940, "DV-T17 pivot unusable before confirmBar: endBar 1009 -> B = low 90 (A = high 105 @940)");
		bool ok10 = DvpLattice.Anchor(p, 1010, 160, out a, out b, out ab, out bb);
		Check(ok10 && b == 110 && a == 90 && bb == 990 && ab == 960, "DV-T17 at endBar = confirmBar the pivot is usable: B = high 110, A = low 90");
		// A must be opposite-side with extremeBar before B's, most recent such
		var p2 = Piv(true, 120.0, 900, 920, false, 80.0, 950, 970, false, 85.0, 1000, 1005, true, 115.0, 990, 1003);
		bool okA = DvpLattice.Anchor(p2, 1010, 400, out a, out b, out ab, out bb);
		Check(okA && b == 85 && bb == 1000 && a == 115 && ab == 990, "DV-5.2 B = latest confirmBar; A = latest opposite-side extreme before B's");
		Check(!DvpLattice.Anchor(Piv(true, 1.0, 5, 8, true, 2.0, 9, 12), 20, 100, out a, out b, out ab, out bb), "DV-5.2 no opposite-side pivot before B -> range empty");
		// --- lookback: look = fibLookMult*s = 160 for s = 20. A @ endBar-160 kept, endBar-161 dropped (and B likewise)
		Check(DvpLattice.Anchor(Piv(false, 90.0, 840, 860, true, 110.0, 990, 1000), 1000, 160, out a, out b, out ab, out bb), "DV-T17 anchor exactly fibLookMult*s back kept");
		Check(!DvpLattice.Anchor(Piv(false, 90.0, 839, 860, true, 110.0, 990, 1000), 1000, 160, out a, out b, out ab, out bb), "DV-T17 A outside fibLookMult*s -> range empty");
		{
			var lat = new DvpLattice(3);
			var cl = new DvpConfig { enabled = true, fibRanges = 3 };
			var pv = new[] { Piv(false, 90.0, 839, 860, true, 110.0, 990, 1000), Piv(false, 90.0, 839, 860, true, 110.0, 990, 1000), Piv(false, 90.0, 839, 860, true, 110.0, 990, 1000) };
			lat.Build(cl, pv, 1000, 2, 100);
			Check(!lat.RangeOk[0] && lat.RangeOk[1] && lat.RangeOk[2] && lat.Count > 0 && lat.Range.Take(lat.Count).All(r => r != 0),
				"DV-T17 Build: A 161 bars back dropped for s=20 (look 160) but kept for s=40/80 (look 320/640)");
		}

		// --- merge at 0.40 ATR: B = 100, A = 0 (range 100), ATR = 30 -> tol 12. Members .886 (11.4) and 1 (0): one group, f = 1 kept.
		{
			var cl = new DvpConfig { enabled = true, fibRanges = 1, fibNear = 1000 };
			var lat = new DvpLattice(1);
			lat.Build(cl, new[] { Piv(false, 0.0, 900, 905, true, 100.0, 950, 960) }, 1000, 30, 50);
			var got = Enumerable.Range(0, lat.Count).Select(i => new KeyValuePair<double, double>(lat.Price[i], lat.F[i])).ToList();
			Check(got.Any(x => x.Value == 1 && x.Key == 0) && !got.Any(x => x.Value == .886) && got.Any(x => x.Value == .786) && !got.Any(x => x.Value == .71),
				"DV-T17 merge 0.40 ATR (tol 12): {1 @0, .886 @11.4} keeps f = 1; next group {.786 @21.4, .71 @29} keeps .786 (|f - round f| .214 < .29)");
			var r = RefRange(0, 100, 30, 50, .40, 1000);
			Check(r.Count == lat.Count && r.Select((x, i) => x.Key == lat.Price[i] && x.Value == lat.F[i]).All(z => z), "DV-5.4 merge equals the independent reference (" + lat.Count + " levels)");
			for (int i = 1; i < lat.Count; i++) if (!(lat.Price[i] - lat.Price[i - 1] >= 0)) { Check(false, "DV-5.4 levels price-ascending within a range"); break; }
		}
		// tie rule on |f - round f|: lower f wins
		{
			// B = 100, A = 99 (range 1): every level within 5.3 of 100; ATR 100 -> tol 40: one group of all 24 -> reps with |f-round f| = 0: 0,1,-1,-2,-3,-4 -> lowest f = -4
			var cl = new DvpConfig { enabled = true, fibRanges = 1, fibNear = 1000 };
			var lat = new DvpLattice(1);
			lat.Build(cl, new[] { Piv(false, 99.0, 900, 905, true, 100.0, 950, 960) }, 1000, 100, 100);
			Check(lat.Count == 1 && lat.F[0] == -4 && lat.Price[0] == 104, "DV-5.4 tie on |f - round f| -> lower f (one group of 24: f = -4 @ 104)");
		}
		// --- fibNear: B = 100, A = 0, ATR = 1, C = 50: |P - 50| <= 14 kept: f=.5 (50), .62 (38), .414? no (-.414 -> 141.4). Keep 36..64 only.
		{
			var cl = new DvpConfig { enabled = true, fibRanges = 1, fibMerge = 0.40, fibNear = 14 };
			var lat = new DvpLattice(1);
			lat.Build(cl, new[] { Piv(false, 0.0, 900, 905, true, 100.0, 950, 960) }, 1000, 1, 50);
			var ps = Enumerable.Range(0, lat.Count).Select(i => lat.Price[i]).ToArray();
			Check(ps.Length == 2 && ps.All(x => Math.Abs(x - 50) <= 14) && ps.Contains(50) && ps.Contains(38), "DV-T17 +-14 ATR filter: only 38 (.62) and 50 (.5) within 14 of C = 50");
			lat.Build(cl, new[] { Piv(false, 0.0, 900, 905, true, 100.0, 950, 960) }, 1000, 1, 64);   // 50 at exactly 14 ATR kept
			Check(Enumerable.Range(0, lat.Count).Any(i => lat.Price[i] == 50) && !Enumerable.Range(0, lat.Count).Any(i => lat.Price[i] == 38),
				"DV-T17 filter boundary: |P - C| = 14 ATR kept, 26 ATR dropped");
		}
		// --- ranges never merge: identical anchors in all 3 ranges -> 3 copies, each its own range id
		{
			var cl = new DvpConfig { enabled = true, fibRanges = 3 };
			var lat = new DvpLattice(3);
			var one = new DvpLattice(1);
			Func<DvpPivots> mk = () => Piv(false, 4000.0, 900, 905, true, 4020.0, 950, 960);
			lat.Build(cl, new[] { mk(), mk(), mk() }, 1000, 2, 4010);
			one.Build(new DvpConfig { enabled = true, fibRanges = 1 }, new[] { mk() }, 1000, 2, 4010);
			bool same = one.Count > 0 && lat.Count == 3 * one.Count;
			for (int j = 0; j < 3 && same; j++) for (int i = 0; i < one.Count; i++) same &= lat.Range[j * one.Count + i] == j && lat.Price[j * one.Count + i] == one.Price[i];
			Check(same, "DV-T17 ranges never merge across: 3 identical ranges -> 3 x " + one.Count + " levels, range ids kept");
		}
		// --- random cross-check against the reference (merge + tie + filter), 300 cases
		{
			var rng = new Random(17); int bad = 0;
			var cl = new DvpConfig { enabled = true, fibRanges = 1 };
			var lat = new DvpLattice(1);
			for (int k = 0; k < 300; k++)
			{
				double A = Math.Round(4000 + 200 * rng.NextDouble(), 2), B = Math.Round(4000 + 200 * rng.NextDouble(), 2), atr = 0.5 + 10 * rng.NextDouble(), C = 3900 + 400 * rng.NextDouble();
				if (A == B) continue;
				lat.Build(cl, new[] { Piv(B < A, A, 900, 905, B > A, B, 950, 960) }, 1000, atr, C);
				var r = RefRange(A, B, atr, C, cl.fibMerge, cl.fibNear);
				if (r.Count != lat.Count || !r.Select((x, i) => x.Key == lat.Price[i] && x.Value == lat.F[i]).All(z => z)) bad++;
			}
			Check(bad == 0, "DV-5.4 300 random ranges match the reference merge/tie/filter (" + bad + " mismatches)");
		}
		// --- no ATR / no close -> empty
		{
			var lat = new DvpLattice(1);
			lat.Build(new DvpConfig { enabled = true, fibRanges = 1 }, new[] { Piv(false, 0.0, 900, 905, true, 100.0, 950, 960) }, 1000, double.NaN, 50);
			Check(lat.Count == 0 && !lat.RangeOk[0], "DV-5 NaN ATR -> empty lattice");
		}
		// --- tag (DV-10.2)
		Check(DvpLattice.Tag(1, new DateTime(2024, 3, 4, 10, 15, 0), 2, -.272, 0) == "DVP1:202403041015:d2:DvpFib-0.272R1"
			&& DvpLattice.Tag(1, new DateTime(2024, 3, 4, 10, 15, 0), 2, 0, 2) == "DVP1:202403041015:d2:DvpFib+0.000R3", "DV-10.2 lattice tag DVP{ds}:{t-1}:d{deg}:DvpFib{f:+0.000}R{j}");
	}

	static _PriceActionEngine PaFor(int strength)
	{
		var pc = new PriceActionConfig { enabled = true, dataStream = 1, layers = 1, strength = strength, pivotMode = EPivotMode.Fractal, minBreakoutLayer = 0, role = "DVP-S" + strength };
		var pa = new _PriceActionEngine { config = pc, label = "PA1-DVP-S" + strength };
		string err = pa.Configure();
		if (err != null) throw new Exception(err);
		return pa;
	}

	// The bridge on a real PA engine (bars numbered from 0 = ring bar - 1, i.e. offset 1 like CurrentBar vs FullSymbolData.Current):
	// pivots confirmed after endBar are excluded, bar numbers shifted, lattice published only on a valid snapshot.
	static void T17Pa()
	{
		bool[] hd; RingBar[] s = Series(4000, 41, null, null, out hd);
		var c = Cfg(240, 1, false); c.fibRanges = 3;
		var rig = new Rig(c, true, false);
		var pas = new[] { PaFor(20), PaFor(40), PaFor(80) };
		int checkedQ = 0, bad = 0, nonEmpty = 0, published = 0;
		var into = new List<LevelRef>(); var pool = new List<LevelRef>();
		for (int i = 0; i < s.Length; i++)
		{
			rig.Feed(s[i], hd[i]);
			foreach (var pa in pas) pa.Add(s[i].bar - 1, s[i].time, s[i].o, s[i].h, s[i].l, s[i].c, s[i].v);
			if (i < 600 || i % 37 != 0) continue;
			for (int ago = 0; ago <= 1; ago++)
			{
				DvpSnapshot x = rig.e.Snapshot(1, rig.ring, ago);
				if (x == null) continue;
				for (int j = 0; j < 3; j++) DvpPaBridge.Fill(pas[j], 1, x.endBar, rig.S.Pivots[j]);
				for (int j = 0; j < 3; j++)
				{
					var pv = rig.S.Pivots[j];
					for (int k = 0; k < pv.Count; k++) if (pv.ConfirmBar[k] > x.endBar || pv.ExtremeBar[k] >= pv.ConfirmBar[k]) bad++;
					int want = 0;
					for (int ag = 0; ag < pas[j].PivotCount(0, true); ag++) if (pas[j].High(0, ag).confirmBar + 1 <= x.endBar) want++;
					for (int ag = 0; ag < pas[j].PivotCount(0, false); ag++) if (pas[j].Low(0, ag).confirmBar + 1 <= x.endBar) want++;
					if (want != pv.Count) bad++;
				}
				DvpLattice lat = rig.e.LatticeOf(1, rig.ring, ago);
				checkedQ++;
				if (!x.valid) { if (lat != null) bad++; continue; }
				if (lat == null) { bad++; continue; }
				var rc = new DvpLattice(3);
				rc.Build(c, rig.S.Pivots, x.endBar, x.atr, rig.ring[ago].c);
				if (rc.Count != lat.Count) bad++;
				for (int k = 0; k < lat.Count; k++) if (Math.Abs(lat.Price[k] - rig.ring[ago].c) > c.fibNear * x.atr) bad++;
				if (lat.Count > 0) nonEmpty++;
				into.Clear(); lat.Publish(into, pool, 1, x.tPrev, x.t0, x.deg, false);
				if (into.Count != lat.Count || into.Any(r => r.kind != ELevelKind.DvpFib || !r.tag.StartsWith("DVP1:") || r.lo != r.price || r.hi != r.price)) bad++;
				published += into.Count;
			}
		}
		Check(bad == 0 && checkedQ > 100 && nonEmpty > 50 && published > 0,
			"DV-5.1 bridge on _PriceActionEngine (s=20/40/80): only confirmBar <= endBar, ring numbering, lattice null when the snapshot is invalid, flat DvpFib LevelRefs ("
			+ checkedQ + " queries, " + nonEmpty + " non-empty, bad " + bad + ")");

		// DV-13.1: zero allocations for bridge fill + lattice build + query after warm-up (PA stepping itself allocates one _Pivot per pivot; not DVP's).
		double sink = 0; long b0 = GC.GetAllocatedBytesForCurrentThread();
		for (int k = 0; k < 500; k++)
		{
			DvpSnapshot x = rig.e.Snapshot(1, rig.ring, k & 1);
			if (x == null) continue;
			for (int j = 0; j < 3; j++) DvpPaBridge.Fill(pas[j], 1, x.endBar, rig.S.Pivots[j]);
			DvpLattice lat = rig.e.LatticeOf(1, rig.ring, k & 1);
			if (lat != null) for (int i = 0; i < lat.Count; i++) sink += lat.Price[i];
		}
		long b1 = GC.GetAllocatedBytesForCurrentThread();
		Check(b1 - b0 == 0, "DV-13 zero allocations: PA bridge fill + lattice build over 500 queries (" + (b1 - b0) + " bytes)");
		if (double.IsNaN(sink)) Console.WriteLine("sink NaN");

		var off = new Rig(new DvpConfig { enabled = true, useFib = false }, true, false);
		Check(off.S.Pivots == null && off.S.Lattice == null && off.e.LatticeOf(1, off.ring, 0) == null, "DV-13.3 useFib off: no lattice or pivot buffers");
	}

	// DV-13.3 / DV-5.1 on the host source: PA instances only with useFib, shared by findSharedPa (XMKT pattern), stepped only when not hosted.
	static void T17Host()
	{
		string host = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int sd = host.IndexOf("private void startDvp()", StringComparison.Ordinal);
		string body = host.Substring(sd, host.IndexOf("private void stepDvp()", sd, StringComparison.Ordinal) - sd);
		int fib = body.IndexOf("if (DvpConfig.useFib)", StringComparison.Ordinal), mk = body.IndexOf("startDvpPa(", StringComparison.Ordinal);
		Check(fib > body.IndexOf("if (!DvpConfig.enabled) return;", StringComparison.Ordinal) && mk > fib && Count(host, "startDvpPa(") == 2 && Count(body, "new _PriceActionEngine {") == 0 && Count(host, "label = \"PA\" + ds + \"-DVP-S\"") == 1,
			"DV-13.3 DVP PA instances created only under useFib, after the enabled check");
		int sp = host.IndexOf("private string startDvpPa(", StringComparison.Ordinal);
		string spb = host.Substring(sp, sd - sp);
		Check(spb.Contains("findSharedPa(c, false)") && spb.Contains("presetExtraPriceAction(c)") && spb.Contains("layers = 1") && spb.Contains("pivotMode = EPivotMode.Fractal")
			&& spb.Contains("role = \"DVP-S\" + str") && spb.Contains("label = \"PA\" + ds + \"-DVP-S\" + str"),
			"DV-5.1 config {layers 1, Fractal, strength s_j, role DVP-S{s}} + per-field preset; shared via findSharedPa (XMKT pattern)");
		int st = host.IndexOf("private void stepDvp()", StringComparison.Ordinal);
		string stb = host.Substring(st, host.IndexOf("private ", st + 10, StringComparison.Ordinal) - st);
		Check(stb.Contains("paHosted(pa)") && stb.Contains("if (rollOn(ds)) pa.OnRoll(b.Time[0]);") && stb.Contains("pa.Step(b);")
			&& host.Contains("l.Add(\"DVP\")") && host.IndexOf("startExtraPa();", StringComparison.Ordinal) < host.IndexOf("startDvp();", StringComparison.Ordinal),
			"DV-5.1 owned instances stepped on closed bars with OnRoll on rollOn(ds); hosted twins left to stepExtraPa; DVP named in paConsumers; startExtraPa before startDvp");
	}


	// Analyst 2 (#37): an ExtraPriceAction entry with the DVP role/strength but a non-zero minBreakoutLayer (needs layers >= 2) is not
	// shared: Canon() differs, so findSharedPa (dataStream + Canon) misses and DVP builds its own instance; startDvpPa never touches
	// the ExtraPriceAction role set, so the equal role "DVP-S20" cannot raise the duplicate-role exception.
	static void T17SeparateInstance()
	{
		var host = new PriceActionConfig { enabled = true, dataStream = 1, layers = 2, minBreakoutLayer = 1, strength = 20, pivotMode = EPivotMode.Fractal, role = "DVP-S20" };
		var dvp = new PriceActionConfig { enabled = true, dataStream = 1, layers = 1, minBreakoutLayer = 0, strength = 20, pivotMode = EPivotMode.Fractal, role = "DVP-S20" };
		var hosted = new List<_PriceActionEngine>();
		var hpa = new _PriceActionEngine { config = host, label = "PA1-DVP-S20" };
		Check(host.Validate() == null && hpa.Configure() == null, "host twin minBreakoutLayer=1 (layers 2) is a valid ExtraPriceAction entry");
		hosted.Add(hpa);
		Func<PriceActionConfig, _PriceActionEngine> find = c => hosted.FirstOrDefault(e => e.config.dataStream == c.dataStream && e.config.Canon() == c.Canon());   // findSharedPa(c, false)
		Exception ex = null; _PriceActionEngine mine = null;
		try { mine = find(dvp) ?? new _PriceActionEngine { config = dvp, label = "PA1-DVP-S20" }; if (mine.Configure() != null) throw new Exception("configure"); }
		catch (Exception e) { ex = e; }
		Check(ex == null && mine != null && !ReferenceEquals(mine, hpa) && host.Canon() != dvp.Canon(), "#37: non-zero minBreakoutLayer host -> separate DVP PA instance, no exception");
		var host0 = new PriceActionConfig { enabled = true, dataStream = 1, layers = 1, minBreakoutLayer = 0, strength = 20, pivotMode = EPivotMode.Fractal, role = "Swing20" };
		var h0 = new _PriceActionEngine { config = host0, label = "PA1-Swing20" }; h0.Configure(); hosted.Add(h0);
		Check(ReferenceEquals(find(dvp), h0), "#37: a minBreakoutLayer=0 twin (role ignored by Canon) is shared");
		string src = File.ReadAllText(Path.Combine(Root(), "_CustomSignal.Strategy.CS"));
		int sp = src.IndexOf("private string startDvpPa(", StringComparison.Ordinal), sd = src.IndexOf("private void startDvp()", sp, StringComparison.Ordinal);
		string b = src.Substring(sp, sd - sp);
		Check(!b.Contains("m_paExtra") && !b.Contains("roles") && !b.Contains("duplicate role") && !b.Contains("m_paHosted.Add"),
			"#37: startDvpPa does not register in ExtraPriceAction roles (no duplicate-role exception path)");
	}

	static void D4Tests() { T17(); T17Pa(); T17Host(); T17SeparateInstance(); }
}
