using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _PortMM (specs/m-port-spec.md rev 1 §5).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime D0 = new DateTime(2024, 3, 11);

	static PortStratView V(int i, EPortState st = EPortState.Armed, int side = 1, double r = 500, int qty = 2, string cl = "", int pos = 0, double pnl = 0, double riskOpen = 0)
	{ return new PortStratView { index = i, id = "S" + i, publishes = true, state = st, side = side, rDollar = r, qtyCode = qty, cluster = cl == "" ? "C" + i : cl, position = pos, pnl = pnl, riskOpen = riskOpen }; }

	static _PortMM MM(Action<PortMmConfig> f = null, List<string> j = null)
	{
		var c = new PortMmConfig(); if (f != null) f(c);
		var m = new _PortMM { config = c }; if (j != null) m.journal = s => j.Add(s);
		string e = m.Configure(); if (e != null) throw new Exception(e); return m;
	}

	static void Main()
	{
		// config
		Check(new PortMmConfig().Validate() == null, "defaults valid");
		var c0 = new PortMmConfig();
		Check(c0.sizing == EPortSizing.RiskParity && c0.heatCap == 0.04 && c0.clusterCap == 0.02 && c0.dailyLossStop == 0.02 && c0.dailyLossFlatten == 0.03 &&
			c0.ddSteps.Select(s => s.dd).SequenceEqual(new[] { 0.05, 0.10, 0.15 }), "spec defaults (4% heat, 2% cluster, dd 5/10/15, -2%/-3%)");
		var bad = new PortMmConfig { clusterCap = 0.05, dailyLossFlatten = 0.01, minContracts = 0 }.Validate();
		Check(bad != null && bad.Contains("clusterCap") && bad.Contains("dailyLossFlatten") && bad.Contains("minContracts"), "Validate names every bad value");

		// returns ring
		var rr = new _PortReturns(3); var r1 = rr.Append(D0, 0.01);
		Check(rr.Count == 0 && r1.Count == 1, "copy-on-write append");
		var r4 = r1.Append(D0.AddDays(1), 0.02).Append(D0.AddDays(2), 0.03).Append(D0.AddDays(3), 0.04);
		Check(r4.Count == 3 && r4.Day(0) == D0.AddDays(1) && Near(r4.Value(2), 0.04) && r4.Append(D0.AddDays(3), 9) == r4 && r4.Append(D0.AddDays(5), double.NaN) == r4, "capacity, no duplicate/old day, NaN ignored");

		// math
		var rnd = new Random(5); _PortReturns a = new _PortReturns(), b = new _PortReturns(), cneg = new _PortReturns(), shortR = new _PortReturns();
		for (int i = 0; i < 80; i++) { double x = rnd.NextDouble() - 0.5; a = a.Append(D0.AddDays(i), x); b = b.Append(D0.AddDays(i), 2 * x + 0.001); cneg = cneg.Append(D0.AddDays(i), -x); if (i < 30) shortR = shortR.Append(D0.AddDays(i), x); }
		Check(Near(_PortMath.Pearson(a, b, 60, 40), 1, 1e-9) && Near(_PortMath.Pearson(a, cneg, 60, 40), -1, 1e-9) && double.IsNaN(_PortMath.Pearson(a, shortR, 60, 40)), "Pearson on common days, minDays");
		var rho = new double[3, 3]; rho[0, 1] = rho[1, 0] = 0.95; rho[0, 2] = rho[2, 0] = double.NaN; rho[1, 2] = rho[2, 1] = 0.2;
		var cl = _PortMath.Clusters(new[] { "A", "B", "C" }, rho, 0.9);
		Check(cl[0] == cl[1] && cl[2] != cl[0], "union-find merge by rho");
		Check(_PortMath.Clusters(new[] { "EQ", "X", "EQ" }, null, 0.9)[0] == _PortMath.Clusters(new[] { "EQ", "X", "EQ" }, null, 0.9)[2], "same seed always merges");
		var z = new double[2, 2]; Check(Near(_PortMath.Idm(new[] { 0, 1 }, z, new[] { 0, 1 }, 2.5), Math.Sqrt(2)) && Near(_PortMath.Idm(new[] { 0, 1 }, new double[,] { { 1, double.NaN }, { double.NaN, 1 } }, new[] { 0, 0 }, 2.5), 1), "IDM equal weights");
		Check(_PortMath.DdStage(c0.ddSteps, 0.049) == 0 && _PortMath.DdStage(c0.ddSteps, 0.05) == 1 && _PortMath.DdStage(c0.ddSteps, 0.12) == 2 && _PortMath.DdStage(c0.ddSteps, 0.2) == 3, "dd stages");
		Check(_PortMath.Size(c0, 1, 100000, 500, 2, double.NaN, 1, 1) == 1 && _PortMath.Size(c0, 1, 1000000, 500, 2, double.NaN, 1, 1) == 10, "risk parity floor(mult x 0.5% x book / R$)");
		var cv = new PortMmConfig { sizing = EPortSizing.VolTarget };
		Check(_PortMath.Size(cv, 1, 1000000, 500, 2, 1000, 2, 1.4) == (int)Math.Floor(0.1 / Math.Sqrt(252) * 1000000 * 1.4 / 2000), "vol target size");
		Check(_PortMath.Size(new PortMmConfig { sizing = EPortSizing.CodeSize }, 1, 1, 1, 1, 1, 1, 1) == -1, "code size = -1");

		// deny-all, explicit EntryContracts, non-publisher denied
		var mm = MM(); var views = new List<PortStratView> { V(0), new PortStratView { index = 1, publishes = false }, V(2, EPortState.Flat) };
		var d = mm.Decide(D0, 1000000, true, views);
		Check(d[0].allowEntries && d[0].allowLong && !d[0].allowShort && d[0].entryContracts == 10, "armed long allowed with RiskParity size");
		Check(!d[1].allowEntries && d[1].entryContracts == -1 && !d[2].allowEntries && d[2].entryContracts == -1, "others denied, EntryContracts still explicit (-1)");
		Check(d.All(x => !x.forceClose && !x.pause), "no kills on a normal calc");

		// heat cap: 4% of 1M = 40000; open risk 35000 -> room 5000 -> 10 x 500 ok; next gets 0
		mm = MM(); views = new List<PortStratView> { V(0, EPortState.Long, 1, 500, 2, "", 1, 0, 35000), V(1), V(2) };
		d = mm.Decide(D0, 1000000, true, views);
		Check(d[1].allowEntries && d[1].entryContracts == 10 && !d[2].allowEntries && d[2].reason == "heat cap", $"heat cap reduces/denies ({d[1].entryContracts}, {d[2].reason})");
		// cluster cap 2% = 20000: two strategies in EQIDX, first takes 5000, second limited to 15000/500 = 30 -> but size 10 ok; third ...
		mm = MM(c => c.riskPerTrade = 0.015); views = new List<PortStratView> { V(0, cl: "EQ"), V(1, cl: "EQ"), V(2, cl: "EN") };
		d = mm.Decide(D0, 1000000, true, views);
		Check(d[0].entryContracts == 30 && d[1].entryContracts == 10 && d[2].entryContracts == 30, $"cluster cap 2% shared by the seed cluster ({d[0].entryContracts},{d[1].entryContracts},{d[2].entryContracts})");
		// maxPositions
		mm = MM(c => c.maxPositions = 1); d = mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0), V(1) });
		Check(d[0].allowEntries && !d[1].allowEntries && d[1].reason == "maxPositions", "maxPositions");
		// score order
		mm = MM(c => c.maxPositions = 1); var vs = new List<PortStratView> { V(0), V(1) }; vs[1].score = 2;
		d = mm.Decide(D0, 1000000, true, vs);
		Check(!d[0].allowEntries && d[1].allowEntries, "higher pm.score first");
		// short side, stale, currency, missing R$
		mm = MM(); vs = new List<PortStratView> { V(0, side: -1), V(1), V(2), V(3, r: double.NaN) }; vs[1].stale = true; vs[2].currencyOk = false;
		d = mm.Decide(D0, 1000000, true, vs);
		Check(d[0].allowShort && !d[0].allowLong && !d[1].allowEntries && d[1].reason == "stale" && !d[2].allowEntries && !d[3].allowEntries, "short, stale, currency, no R$");
		Check(!MM().Decide(D0, 1000000, false, new List<PortStratView> { V(0) })[0].allowEntries, "portfolio currency not USD -> deny");
		// CodeSize
		mm = MM(c => c.sizing = EPortSizing.CodeSize); d = mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, qty: 3) });
		Check(d[0].allowEntries && d[0].entryContracts == -1, "CodeSize allows with EntryContracts -1");
		mm = MM(c => c.sizing = EPortSizing.CodeSize); d = mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Long, 1, 500, 1, "", 1, 0, 39000), V(1, qty: 3) });
		Check(!d[1].allowEntries && d[1].reason == "heat cap", "CodeSize still heat-capped on qtyCode x R$");

		// drawdown throttle with hysteresis
		var j = new List<string>(); mm = MM(c => { c.dailyLossStop = 0.5; c.dailyLossFlatten = 0.6; }, j);
		Func<double, DateTime, PortDecision> step = (pnl, day) => mm.Decide(day, 1000000, true, new List<PortStratView> { V(0, pnl: pnl) })[0];
		step(0, D0); step(100000, D0.AddDays(1));                       // peak 1.1M
		var x1 = step(100000 - 66000, D0.AddDays(2));                     // dd 6% -> 0.5
		Check(Near(mm.Mult, 0.5) && x1.entryContracts == (int)Math.Floor(0.5 * 0.005 * 1034000 / 500), $"dd 6% -> mult 0.5 ({x1.entryContracts})");
		step(100000 - 140000, D0.AddDays(3));                            // dd 12.7% -> 0.25
		Check(Near(mm.Mult, 0.25), "dd 12.7% -> 0.25");
		step(100000 - 180000, D0.AddDays(4));                            // dd 16.4% -> 0 entries off
		var x0 = step(100000 - 180000, D0.AddDays(5));
		Check(Near(mm.Mult, 0) && !x0.allowEntries && x0.reason == "dd throttle", "dd >= 15% -> entries off");
		step(100000 - 140000, D0.AddDays(6));                            // dd 12.7%: implied 2, needs dd < 7.5% or new 20d high
		Check(mm.DdStageNow == 3, "hysteresis: no recovery at dd 12.7%");
		step(100000 - 80000, D0.AddDays(7));                             // dd 7.3% < 7.5% -> implied stage 1
		Check(mm.DdStageNow == 1 && Near(mm.Mult, 0.5), "recover when dd < half the step");
		Check(j.Any(s => s.Contains("drawdown recovered")), "journaled");

		// daily stop and flatten
		j = new List<string>(); mm = MM(null, j);
		mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0), V(1, EPortState.Long, 1, 500, 1, "", 2) });
		d = mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, pnl: -21000), V(1, EPortState.Long, 1, 500, 1, "", 2) });
		Check(!d[0].allowEntries && d[0].reason == "daily stop" && !d[1].forceClose && mm.DayStopped, "-2.1% -> deny entries, no flatten");
		d = mm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, pnl: -31000), V(1, EPortState.Long, 1, 500, 1, "", 2) });
		Check(d[1].forceClose && !d[0].forceClose && j.Any(s => s.Contains("flatten")), "-3.1% -> ForceClosePosition on open positions");
		d = mm.Decide(D0.AddDays(1), 1000000, true, new List<PortStratView> { V(0, pnl: -31000), V(1, EPortState.Flat, pos: 0, pnl: 0) });
		Check(d[0].allowEntries && !mm.DayStopped, "new session resets the daily stop");

		// kill switch: -8R over trades -> flatten while in position, pause once flat, never again allowed
		j = new List<string>(); mm = MM(c => c.stratKillR = -2, j);
		Func<int, double, PortDecision> ks = (pos, pnl) => mm.Decide(D0, 10000000, true, new List<PortStratView> { V(0, pos != 0 ? EPortState.Long : EPortState.Armed, 1, 500, 1, "", pos, pnl) })[0];
		ks(0, 0); ks(1, 0); ks(0, -600);   // -1.2R
		ks(1, -600); var k1 = ks(0, -1200); // second trade -1.2R => -2.4R -> killed, flat -> pause
		Check(k1.pause && !k1.allowEntries && j.Any(s => s.Contains("PORT kill")), "cumulative R kill -> pause when flat");
		var k2 = mm.Decide(D0, 10000000, true, new List<PortStratView> { V(0, EPortState.Long, 1, 500, 1, "", 1, -1200) })[0];
		Check(k2.forceClose && !k2.pause, "killed strategy with a position: ForceClosePosition first, no pause");
		var h = mm.Decide(D0, 10000000, true, new List<PortStratView> { V(0), V(1, EPortState.Halted, pos: 2) });
		Check(h[1].forceClose && !h[1].pause, "own Halted state -> flatten");

		// clusters from returns: two seeds with rho ~1 merge -> share the 2% budget
		mm = MM(c => c.riskPerTrade = 0.015);
		var s0 = V(0, cl: "A"); var s1 = V(1, cl: "B"); s0.uRet = a; s1.uRet = b;
		d = mm.Decide(D0.AddDays(100), 1000000, true, new List<PortStratView> { s0, s1 });
		Check(d[0].entryContracts == 30 && d[1].entryContracts == 10, $"rho >= 0.9 merges clusters ({d[0].entryContracts},{d[1].entryContracts})");
		Check(mm.Rho != null && Near(mm.Rho[0, 1], 1, 1e-9), "rho matrix published");

		// exits are never part of a decision: PortDecision has no exit flags; forceClose only from kill/flatten
		Check(typeof(PortDecision).GetFields().All(f => !f.Name.ToLower().Contains("exit")), "no exit gating in decisions");

		Console.WriteLine($"PortMMTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
