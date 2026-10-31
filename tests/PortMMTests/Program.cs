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
		var c = PortMmConfig.ForProfile(EPortProfile.ScaledBook); if (f != null) f(c);   // rev 1 tests repinned to ScaledBook (spec §8)
		var m = new _PortMM { config = c }; if (j != null) m.journal = s => j.Add(s);
		string e = m.Configure(); if (e != null) throw new Exception(e); return m;
	}

	static _PortMM MP(EPortProfile p, Action<PortMmConfig> f = null, List<string> j = null)
	{
		var c = PortMmConfig.ForProfile(p); if (f != null) f(c);
		var m = new _PortMM { config = c }; if (j != null) m.journal = s => j.Add(s);
		string e = m.Configure(); if (e != null) throw new Exception(e); return m;
	}
	static PortStratView W(int i, double rw, string cl, EPortState st = EPortState.Armed, int pos = 0, double riskOpen = 0)
	{ var x = V(i, st, 1, 500, 2, cl, pos, 0, riskOpen); x.rDollarW = rw; return x; }

	static void Main()
	{
		// config
		Check(new PortMmConfig().Validate() == null, "defaults valid");
		var c0 = PortMmConfig.ForProfile(EPortProfile.ScaledBook);
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
		var cv = PortMmConfig.ForProfile(EPortProfile.ScaledBook); cv.sizing = EPortSizing.VolTarget; 
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
		mm = MM(c => { c.riskPerTrade = 0.0075; c.clusterCap = 0.01; }); views = new List<PortStratView> { V(0, cl: "EQ"), V(1, cl: "EQ"), V(2, cl: "EN") };
		d = mm.Decide(D0, 1000000, true, views);
		Check(d[0].entryContracts == 15 && !d[1].allowEntries && d[1].reason == "cluster cap" && d[2].entryContracts == 15, $"cluster cap 2% shared by the seed cluster: no room for 30 -> skipped, never cut ({d[0].entryContracts},{d[1].entryContracts},{d[2].entryContracts})");
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
		mm = MM(c => { c.riskPerTrade = 0.0075; c.clusterCap = 0.01; });
		var s0 = V(0, cl: "A"); var s1 = V(1, cl: "B"); s0.uRet = a; s1.uRet = b;
		d = mm.Decide(D0.AddDays(100), 1000000, true, new List<PortStratView> { s0, s1 });
		Check(d[0].entryContracts == 15 && !d[1].allowEntries && d[1].reason == "cluster cap", $"rho >= 0.9 merges clusters ({d[0].entryContracts},{d[1].entryContracts})");
		Check(mm.Rho != null && Near(mm.Rho[0, 1], 1, 1e-9), "rho matrix published");

		// exits are never part of a decision: PortDecision has no exit flags; forceClose only from kill/flatten
		Check(typeof(PortDecision).GetFields().All(f => !f.Name.ToLower().Contains("exit")), "no exit gating in decisions");

		// ---- JSON config (PortMmConfigLoader) ----
		{
			var def = PortMmConfig.ForProfile(EPortProfile.ScaledBook);
			var w = new List<string>();
			var pc = PortMmConfigLoader.Parse("{ \"heatCap\": 0.03, \"Sizing\": \"codesize\", \"_note\": \"x\" }", def, w);
			Check(pc != def && Near(pc.heatCap, 0.03) && pc.sizing == EPortSizing.CodeSize && Near(pc.clusterCap, 0.02) && w.Count == 0, "partial JSON overrides only present fields (case-insensitive, _comments ok)");
			Check(Near(def.heatCap, 0.04) && def.sizing == EPortSizing.RiskParity, "defaults object not mutated");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"heatCap\": 0.03, \"bogus\": 1 }", def, w);
			Check(Near(pc.heatCap, 0.03) && w.Count == 1 && w[0].Contains("unknown field 'bogus'"), "unknown field -> warning");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"heatCap\": -1, \"maxPositions\": 2.5, \"sizing\": \"Kelly\", \"riskPerTrade\": \"x\", \"riskPertrade2\": 0 }", def, w);
			Check(Near(pc.heatCap, 0.04) && pc.maxPositions == 6 && pc.sizing == EPortSizing.RiskParity && w.Count(x => x.Contains("bad value")) == 4, "bad values -> warning, field keeps default");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"ddSteps\": [ {\"dd\":0.1,\"mult\":0.5}, {\"dd\":0.05,\"mult\":0.25} ] }", def, w);
			Check(pc.ddSteps.Length == 3 && Near(pc.ddSteps[0].dd, 0.05) && w.Count == 1 && w[0].Contains("ddSteps"), "ddSteps must ascend");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"ddSteps\": [ {\"dd\":0.04,\"mult\":0.6}, {\"dd\":0.2,\"mult\":0} ] }", def, w);
			Check(pc.ddSteps.Length == 2 && Near(pc.ddSteps[1].dd, 0.2) && w.Count == 0, "valid ddSteps replace the default list");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"dailyLossStop\": 0.04, \"dailyLossFlatten\": 0.03 }", def, w);
			Check(pc == def && w.Count == 1 && w[0].Contains("rejected"), "daily stop must be < flatten: whole file rejected -> defaults");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"clusterCap\": 0.05 }", def, w);
			Check(pc == def && w[0].Contains("rejected"), "clusterCap > heatCap rejected");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ not json", def, w);
			Check(pc == def && w.Count == 1 && w[0].Contains("not valid JSON"), "malformed JSON -> defaults");
			w.Clear(); pc = PortMmConfigLoader.Parse("[1,2]", def, w);
			Check(pc == def && w[0].Contains("not a JSON object"), "non-object -> defaults");
			w.Clear(); pc = PortMmConfigLoader.Parse("{ \"stratKillR\": 3, \"rhoWin\": 2 }", def, w);
			Check(Near(pc.stratKillR, -8) && pc.rhoWin == 60 && w.Count == 2, "kill R must be negative, windows >= 3");
			var info = new List<string>(); w.Clear();
			Check(PortMmConfigLoader.LoadFile(null, def, w, info) == def && PortMmConfigLoader.LoadFile("", def, w, info) == def && w.Count == 0 && info.Count == 0, "no path -> defaults silently");
			Check(PortMmConfigLoader.LoadFile("/nonexistent/x.json", def, w, info) == def && w.Count == 1 && w[0].Contains("not found"), "missing file -> warning + defaults");
			string tmp = System.IO.Path.GetTempFileName(); System.IO.File.WriteAllText(tmp, "{ \"maxPositions\": 3 }"); w.Clear();
			pc = PortMmConfigLoader.LoadFile(tmp, def, w, info);
			Check(pc.maxPositions == 3 && w.Count == 0 && info.Count == 1 && info[0].Contains("maxPositions(3)"), "file load logs the effective config at Info");
			System.IO.File.Delete(tmp);
			string sample = System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../samples/PortMmConfig.example.json");
			var st = new PortMmConfig(); w.Clear(); info.Clear(); pc = PortMmConfigLoader.LoadFile(sample, def, w, info);
			Check(w.Count == 0 && info.Count == 1 && pc.ToString() == st.ToString() && Near(pc.idmCap, st.idmCap) && pc.rhoMinDays == st.rhoMinDays && pc.ddRecoverHighDays == st.ddRecoverHighDays && Near(pc.bookVolTarget, st.bookVolTarget),
				$"samples/PortMmConfig.example.json (profile Starter) parses cleanly onto a ScaledBook base and equals the Starter defaults ({string.Join(" | ", w)})");
			var cp = def.Copy(); cp.ddSteps[0].dd = 0.07; Check(Near(def.ddSteps[0].dd, 0.05), "Copy is deep");
		}

		// T-P19 (rev 2 §3): portfolioArm always writes pm.R$w (NaN when not widened), pm.riskNew = Rw x qtyCode, pm.R$ stays structural.
		{
			var pd = new Dictionary<string, object>(); Action<string, object> set = (k, v) => pd[k] = v;
			_PortArm.Init(set);
			Check(pd.ContainsKey(_PortKeys.RDollarW) && double.IsNaN((double)pd[_PortKeys.RDollarW]), "T-P19 startPortfolio initialises pm.R$w = NaN");
			Check(_PortKeys.RDollarW == "pm.R$w" && _PortKeys.Version == 1, "T-P19 key name pm.R$w, pm.ver stays 1");
			double bpv = 50, entry = 5000, stop = 4998, wide = 4996.5;   // ES: 8-tick structural, 14-tick floor
			double rd = Math.Abs(entry - stop) * bpv, rdw = _PortArm.RDollarW(entry, stop, wide, bpv);
			Check(Near(rd, 100) && Near(rdw, 175), "T-P19 R$w = |entry - widened| x BPV");
			_PortArm.Write(set, rd, rdw, 5);
			Check(Near((double)pd[_PortKeys.RDollar], 100) && Near((double)pd[_PortKeys.RDollarW], 175) && (int)pd[_PortKeys.QtyCode] == 5 && Near((double)pd[_PortKeys.RiskNew], 875),
				"T-P19 widened arm: R$ structural, R$w widened, riskNew = R$w x qty");
			_PortArm.Write(set, rd, _PortArm.RDollarW(entry, stop, double.NaN, bpv), 5);
			Check(double.IsNaN((double)pd[_PortKeys.RDollarW]) && Near((double)pd[_PortKeys.RiskNew], 500), "T-P19 later arm without widenedStop overwrites pm.R$w with NaN (no stale key), riskNew = R$ x qty");
			Check(double.IsNaN(_PortArm.RDollarW(entry, stop, 4999, bpv)), "T-P19 widened stop closer than structural -> NaN");
			Check(double.IsNaN(_PortArm.RDollarW(entry, stop, double.PositiveInfinity, bpv)), "T-P19 infinite widened stop -> NaN");
			Check(Near(_PortArm.RDollarW(entry, stop, stop, bpv), 100), "T-P19 widened == structural -> R$w = R$");
			Check(Near(_PortArm.RDollarW(5000, 5002, 5003.5, bpv), 175), "T-P19 short side");
			Check(Near(_PortArm.RiskNew(100, double.NaN, -1), 0), "T-P19 riskNew never negative");
		}
		// T-P21 (rev 2 §5.1 step 5): sizeForRisk sizes on the structural stop; skips (returns 0, never a smaller qty) only when it will not arm
		// and finalQty x widened R$ > 1% of equity; exactly 1% is allowed.
		{
			double bpv = 50, eq = 100000, budget = eq * _PortArm.TradeBudgetFraction, entry = 5000;
			Func<double, int> qty = r => (int)Math.Floor(0.005 * eq / r);   // CS-4 0.5% on the structural stop
			int q8 = qty(100);   // 8 ticks
			Check(q8 == 5 && !_PortArm.SkipForBudget(false, q8, entry, 4998, 4996.5, bpv, budget), "T-P21 8-tick structural, 14-tick floor: 5 x 175 = 875 <= 1000, trade");
			int q4 = qty(50);    // 4 ticks
			Check(q4 == 10 && _PortArm.SkipForBudget(false, q4, entry, 4999, 4996.5, bpv, budget), "T-P21 4-tick structural: 10 x 175 = 1750 > 1000, skip");
			Check(!_PortArm.SkipForBudget(false, 3, entry, 4999, 4996.5, bpv, budget), "T-P21 finalQty after maxQty(3): 525 <= 1000, trade");
			Check(!_PortArm.SkipForBudget(false, 5, entry, 4998, 4996, bpv, budget), "T-P21 exactly 1% (5 x 200 = 1000) is allowed");
			Check(_PortArm.SkipForBudget(false, 5, entry, 4998, 4995.75, bpv, budget), "T-P21 just over 1% skips");
			Check(!_PortArm.SkipForBudget(true, q4, entry, 4999, 4996.5, bpv, budget), "T-P21 portfolio Data1 (arms): no skip, MM decides");
			Check(!_PortArm.SkipForBudget(false, q4, entry, 4999, double.NaN, bpv, budget), "T-P21 no widenedStop: no skip");
			Check(!_PortArm.SkipForBudget(false, q4, entry, 4999, 4999.5, bpv, budget), "T-P21 widened closer than structural: ignored");
			Check(_PortArm.SkipForBudget(false, q4, 5000, 5001, 5003.5, bpv, budget), "T-P21 short side skips");
			Check(!_PortArm.SkipForBudget(false, 0, entry, 4999, 4996.5, bpv, budget), "T-P21 qty 0: nothing to skip");
			bool warned = false;
			Check(!_PortArm.WarnCloserOnce(ref warned, entry, 4998, double.NaN) && !_PortArm.WarnCloserOnce(ref warned, entry, 4998, 4996.5) && !_PortArm.WarnCloserOnce(ref warned, entry, 4998, 4998) && !warned,
				"T-P21 no warning for NaN, wider or equal widened stop");
			Check(_PortArm.WarnCloserOnce(ref warned, entry, 4998, 4999) && warned, "T-P21 closer widened stop warns the first time");
			Check(!_PortArm.WarnCloserOnce(ref warned, entry, 4998, 4999) && !_PortArm.WarnCloserOnce(ref warned, 5000, 5002, 5001), "T-P21 closer widened stop warns only once per instance");
			Check(_PortArm.WidenedCloser(5000, 5002, 5001) && double.IsNaN(_PortArm.RDollarW(5000, 5002, 5001, bpv)) && !_PortArm.SkipForBudget(false, 100, 5000, 5002, 5001, bpv, budget),
				"T-P21 closer widened stop: structural used (R$w NaN, no skip even at a large qty)");
		}

		// Total open-risk cap (spec §5.4 item 0, TotalRiskCapPct [Input]) and the Info-level skip journal (spec §5.6).
		{
			string w;
			Check(Near(_PortRiskCap.Fraction(10, out w), 0.10) && w == null, "cap: 10% -> 0.10, no warning");
			Check(Near(_PortRiskCap.Fraction(100, out w), 1.0) && w == null, "cap: 100% allowed");
			Check(Near(_PortRiskCap.Fraction(0, out w), 0.10) && w != null && Near(_PortRiskCap.Fraction(-5, out w), 0.10) && w != null && Near(_PortRiskCap.Fraction(double.NaN, out w), 0.10) && w != null,
				"cap: <= 0 / NaN rejected -> default 10% with a warning");
			Check(Near(_PortRiskCap.Fraction(150, out w), 1.0) && w != null && w.Contains("clamped"), "cap: > 100 clamped to 100% with a warning");
			Check(Near(new PortMmConfig().totalRiskCap, 0.10) && new PortMmConfig { totalRiskCap = 0 }.Validate() != null && new PortMmConfig { totalRiskCap = 1.5 }.Validate() != null, "cap: default 0.10, Validate range (0, 1]");
			Check(Near(_PortRiskCap.Rw(500, double.NaN), 500) && Near(_PortRiskCap.Rw(500, 400), 500) && Near(_PortRiskCap.Rw(500, 700), 700), "cap: Rw = R$w when finite and >= R$, else R$");
			Check(!_PortRiskCap.Breaks(15000, 5000, 20000) && _PortRiskCap.Breaks(15000, 5000.01, 20000) && _PortRiskCap.Breaks(0, double.NaN, 20000), "cap: exactly at the cap allowed, over or unknown breaks");

			var tj = new List<string>();
			var tmm = MM(c => c.totalRiskCap = 0.02, tj);   // $20k on a $1M book; heat 4% = $40k
			var open0 = V(0, EPortState.Flat, pos: 1, riskOpen: 15000);
			var c1 = V(1, r: 500); c1.rDollarW = 1000;   // RiskParity q = 10, widened 10 x 1000 = $10k
			var td = tmm.Decide(D0, 1000000, true, new List<PortStratView> { open0, c1 });
			Check(!td[1].allowEntries && td[1].reason == "total risk cap", "cap: widened risk over the cap -> skipped (not cut)");
			Check(tj.Exists(x => x == "PORT Info: skip S1#1 total risk cap: risk $10000.00 total $15000.00 cap $20000.00"), "cap: skip journaled at Info with total $, new risk $ and cap $ (" + string.Join(" | ", tj) + ")");
			int nLog = tj.Count; tmm.Decide(D0, 1000000, true, new List<PortStratView> { open0, c1 });
			Check(tj.Count == nLog, "cap: the same pending skip is journaled once, not every bar");
			c1.rDollarW = double.NaN;
			td = MM(c => c.totalRiskCap = 0.02).Decide(D0, 1000000, true, new List<PortStratView> { open0, c1 });
			Check(td[1].allowEntries && td[1].entryContracts == 10, "cap: structural risk 10 x 500 = $5k reaches exactly $20k -> allowed, full size");
			var mm2 = MM(c => c.totalRiskCap = 0.02);
			var c2 = V(2, r: 500); c2.rDollarW = 600; var c3 = V(3, r: 500); c3.rDollarW = 600;
			td = mm2.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat, pos: 1, riskOpen: 9000), c2, c3 });
			Check(td[1].allowEntries && !td[2].allowEntries && td[2].reason == "total risk cap" && Near(mm2.TotalRisk, 15000), "cap: entries accepted in the same step count toward the total");
			td = MM(c => c.heatCap = 0.04).Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat, pos: 1, riskOpen: 39800), V(1) });
			Check(!td[1].allowEntries && td[1].reason == "heat cap", "cap: heat (4%) tighter than the 10% total cap binds");
			td = MM(c => c.sizing = EPortSizing.CodeSize).Decide(D0, 1000000, true, new List<PortStratView> { V(0, r: double.NaN) });
			Check(!td[0].allowEntries && td[0].reason == "no R$", "CodeSize with unknown R$ is skipped as no R$");
			{   // review #67: size never cut to heat/cluster room; pre-candidate rejections at Info; dedupe re-logs on change/clear
				var lj = new List<string>(); var lm = MM(c => { c.riskPerTrade = 0.0075; c.heatCap = 0.04; }); lm.journal = x => lj.Add(x);
				var lv = new List<PortStratView> { V(0, EPortState.Flat, pos: 1, riskOpen: 35000), V(1) };   // wants 15 x 500 = 7500, room 5000
				var ld = lm.Decide(D0, 1000000, true, lv);
				Check(!ld[1].allowEntries && ld[1].entryContracts == -1 && ld[1].reason == "heat cap", "room 5000 < risk 7500 -> skipped, not trimmed to 10");
				Check(lj.Any(x => x.StartsWith("PORT Info: skip") && x.Contains("heat cap") && x.Contains("risk $7500.00") && x.Contains("room $5000.00")), "room skip journaled at Info with risk $ and room $");
				lj.Clear(); var sv = V(1); sv.stale = true;
				lm.Decide(D0, 1000000, true, new List<PortStratView> { sv });
				Check(lj.Count == 1 && lj[0].StartsWith("PORT Info: skip") && lj[0].Contains(" stale:") && lj[0].Contains("risk $") && lj[0].Contains("cap $"), "stale rejection journaled at Info");
				lm.Decide(D0, 1000000, true, new List<PortStratView> { sv });
				Check(lj.Count == 1, "same reason pending -> not re-journaled");
				var nv = V(1, r: double.NaN); lm.Decide(D0, 1000000, true, new List<PortStratView> { nv });
				Check(lj.Count == 2 && lj[1].Contains(" no R$:"), "reason change -> re-journaled (no R$ at Info)");
				lm.Decide(D0, 1000000, true, new List<PortStratView> { V(1) });   // clears (allowed)
				lm.Decide(D0, 1000000, true, new List<PortStratView> { nv });
				Check(lj.Count == 3, "after the skip clears, the same reason is journaled again");
				lj.Clear(); var ucv = V(1); ucv.currencyOk = false; lm.Decide(D0, 1000000, true, new List<PortStratView> { ucv });
				Check(lj.Count == 1 && lj[0].Contains("currency not USD"), "currency rejection journaled at Info");
				lj.Clear(); var pv = V(1); pv.paused = true; lm.Decide(D0, 1000000, true, new List<PortStratView> { pv });
				Check(lj.Count == 1 && lj[0].Contains("paused/killed"), "paused rejection journaled at Info");
				lj.Clear(); lm.Decide(D0, 1000000, true, new List<PortStratView> { V(1, EPortState.Long, pos: 1), V(2, EPortState.Flat) });
				Check(lj.Count(x => !x.Contains("no pm.day")) == 0, "in position / not Armed are not entry attempts -> silent");
				lj.Clear(); lm.Decide(D0, 1000000, true, new List<PortStratView> { nv }); var cd = MM(c => c.sizing = EPortSizing.CodeSize); var cj = new List<string>(); cd.journal = x => cj.Add(x);
				cd.Decide(D0, 1000000, true, new List<PortStratView> { V(0, r: double.NaN) });
				cj.RemoveAll(x => x.StartsWith("PORT advisory")); Check(cj.Count == 1 && cj[0].StartsWith("PORT Info: skip") && cj[0].Contains(" no R$:"), "CodeSize no R$ journaled at Info");
			}

			// every skip touched logs at Info (Ettienne's rule)
			Func<List<string>, string, bool> info = (lj, reason) => lj.Exists(x => x.StartsWith("PORT Info: skip ") && x.Contains(" " + reason + ": "));
			tj = new List<string>(); MM(c => c.maxPositions = 1, tj).Decide(D0, 1000000, true, new List<PortStratView> { V(0), V(1) });
			Check(info(tj, "maxPositions"), "Info: maxPositions skip");
			tj = new List<string>(); MM(null, tj).Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat, pos: 1, riskOpen: 39800), V(1) });
			Check(info(tj, "heat cap"), "Info: heat cap skip");
			tj = new List<string>(); MM(null, tj).Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat, pos: 1, riskOpen: 19800, cl: "X"), V(1, cl: "X") });
			Check(info(tj, "cluster cap"), "Info: cluster cap skip");
			tj = new List<string>(); MM(c => c.minContracts = 2, tj).Decide(D0, 1000000, true, new List<PortStratView> { V(0, r: 5000) });
			Check(info(tj, "size < minContracts"), "Info: minContracts skip");
			tj = new List<string>(); var mm3 = MM(null, tj); mm3.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat) });
			mm3.Decide(D0, 1000000, true, new List<PortStratView> { V(0, pnl: -50000) });
			Check(info(tj, "daily stop"), "Info: daily stop skip");
			tj = new List<string>(); MM(c => { c.sizing = EPortSizing.CodeSize; c.heatCap = 0.0005; c.clusterCap = 0.0005; }, tj).Decide(D0, 1000000, true, new List<PortStratView> { V(0) });
			Check(info(tj, "heat cap"), "Info: CodeSize heat cap skip");
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../_CustomSignal.Strategy.CS"));
			Check(!System.Text.RegularExpressions.Regex.IsMatch(src, @"trace\(ETraceLevel\.(Debug|Verbose), \$?""sizeForRisk - 0") && System.Text.RegularExpressions.Regex.Matches(src, @"trace\(ETraceLevel\.Info, \$?""sizeForRisk - 0").Count == 6,
				"Info: every sizeForRisk skip (bad args, minStopTicks, zero size, notional cap, round lots, 1% budget) traces at Info");
		}

		{   // PR-B: widened risk (pm.R$w) for the 1% per-trade budget, heat and cluster caps; skip, never cut
			Func<int, double, double, string, PortStratView> W = (wi, wr, ww, wcl) => { var x = V(wi, r: wr, cl: wcl); x.rDollarW = ww; return x; };
			var bj = new List<string>(); var bm = MM(null, bj);
			var bd = bm.Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 350, "") });
			Check(bd[0].allowEntries && bd[0].entryContracts == 20, "T-P16: n from structural (20, not 14); widened 7000 <= 1% -> allowed");
			bd = MM(null, bj).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 600, "") });
			Check(!bd[0].allowEntries && bd[0].entryContracts == -1 && bd[0].reason == "per-trade budget", "T-P16: widened 12000 > 1% -> skipped 'per-trade budget', never resized");
			Check(bj.Any(x => x.StartsWith("PORT Info: skip") && x.Contains("per-trade budget") && x.Contains("$12000.00") && x.Contains("budget $10000.00")), "per-trade skip at Info with risk $ and budget $");
			bd = MM().Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 500, "") });
			Check(bd[0].allowEntries && bd[0].entryContracts == 20, "T-P16: exactly 1% (10000) -> allowed (> not >=)");
			bd = MM().Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, double.NaN, "") });
			Check(bd[0].allowEntries && bd[0].entryContracts == 20, "T-P16: pm.R$w NaN -> Rw = pm.R$");
			var wj = new List<string>(); var wm = MM(null, wj);
			bd = wm.Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 200, "") });
			wm.Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 200, "") });
			Check(bd[0].allowEntries && bd[0].entryContracts == 20 && wj.Count(x => x.StartsWith("PORT Warning") && x.Contains("pm.R$w")) == 1, "T-P16: pm.R$w < pm.R$ -> Rw = pm.R$, one warning per strategy");
			bd = MM(c => { c.heatCap = 0.015; c.clusterCap = 0.015; }).Decide(D0, 1000000, true, new List<PortStratView> { V(9, EPortState.Flat, pos: 1, riskOpen: 8000), W(1, 250, 400, "") });
			Check(!bd[1].allowEntries && bd[1].entryContracts == -1 && bd[1].reason == "heat cap", "T-P18 heat on widened: 8000 + 20x400 = 16000 > 15000 -> skipped (structural 13000 would pass)");
			bd = MM(c => c.clusterCap = 0.01).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 250, 300, "EQ"), W(1, 250, 300, "EQ") });
			Check(bd[0].allowEntries && bd[0].entryContracts == 20 && !bd[1].allowEntries && bd[1].reason == "cluster cap", "T-P18 cluster on widened: 6000 + 6000 > 10000 -> second skipped, first keeps n (structural 10000 would pass)");
			var cm = MM(c => c.sizing = EPortSizing.CodeSize); var cx = W(0, 250, 600, ""); cx.qtyCode = 20;
			bd = cm.Decide(D0, 1000000, true, new List<PortStratView> { cx });
			Check(!bd[0].allowEntries && bd[0].reason == "per-trade budget", "T-P18(iii) CodeSize qtyCode 20 x 600 = 12000 -> 'per-trade budget'");
			cx.rDollarW = 350; cm = MM(c => c.sizing = EPortSizing.CodeSize);
			bd = cm.Decide(D0, 1000000, true, new List<PortStratView> { cx });
			Check(bd[0].allowEntries && bd[0].entryContracts == -1 && Near(cm.TotalRisk, 7000) && Near(cm.Heat, 0.007), "T-P18(iii) CodeSize allowed; heat/total use 20 x Rw 350 = 7000");
			var vc = new PortMmConfig { maxTradeRisk = 0.02 }; Check(vc.Validate() != null, "maxTradeRisk 0.02 > 0.01 -> invalid");
			vc = new PortMmConfig { riskPerTrade = 0.015 }; Check(vc.Validate() != null, "riskPerTrade > maxTradeRisk -> invalid");
			var mw = new List<string>(); var mc = PortMmConfigLoader.Parse("{ \"maxTradeRisk\": 0.02 }", new PortMmConfig(), mw);
			Check(Near(mc.maxTradeRisk, 0.01) && mw.Count > 0, "T-P10 add: JSON maxTradeRisk 0.02 rejected -> defaults");
			mw.Clear(); mc = PortMmConfigLoader.Parse("{ \"maxTradeRisk\": 0.008 }", new PortMmConfig(), mw);
			Check(Near(mc.maxTradeRisk, 0.008) && mw.Count == 0, "JSON maxTradeRisk 0.008 accepted");
			Check(!System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../_PortMM.Function.CS")), @"q\s*=\s*qMax|Math\.Floor\(room"), "no rev 1 heat-room trimming left in _PortMM");
		}
		{   // PT 'Max % Capital at Risk per Position' drop detector (approved, never filled)
			var fj = new List<string>(); var fm = MM(null, fj);
			Func<List<string>, int> nw = l => l.Count(x => x.StartsWith("PORT Warning") && x.Contains("Max % Capital at Risk per Position"));
			var a0 = V(0, r: 250);
			for (int k = 0; k < 2; k++) fm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			Check(nw(fj) == 0, "fill check: no warning before the window (3 evaluations)");
			fm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			fm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			var fw = fj.FirstOrDefault(x => x.Contains("Max % Capital at Risk per Position")) ?? "";
			Check(nw(fj) == 1 && fw.Contains("S0#0") && fw.Contains("qty 20") && fw.Contains("risk $5000.00") && fw.Contains(">= 1%"), "fill check: approved 3 times with no position -> one Warning with strategy, qty and risk $");
			for (int k = 0; k < 5; k++) fm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			Check(nw(fj) == 1, "fill check: one Warning per strategy, no spam");
			fm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Long, pos: 20, riskOpen: 5000) });
			for (int k = 0; k < 4; k++) fm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			Check(nw(fj) == 2, "fill check: re-armed after a fill -> warns again on the next silent drop");
			var gj = new List<string>(); var gm = MM(null, gj);
			gm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			gm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Long, pos: 20, riskOpen: 5000) });
			for (int k = 0; k < 3; k++) { gm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, EPortState.Flat) }); gm.Decide(D0, 1000000, true, new List<PortStratView> { a0 }); }
			Check(nw(gj) == 0, "fill check: filled entry, or signal withdrawing (not Armed) -> no warning");
			var hj = new List<string>(); var hm = MM(c => c.heatCap = 0.04, hj);
			hm.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			for (int k = 0; k < 5; k++) hm.Decide(D0, 1000000, true, new List<PortStratView> { V(9, EPortState.Flat, pos: 1, riskOpen: 39000), a0 });
			Check(nw(hj) == 0, "fill check: entry later skipped by the MM (not approved) -> no warning");
			var oj = new List<string>(); var om = MM(c => c.fillCheckEvals = 0, oj);
			for (int k = 0; k < 6; k++) om.Decide(D0, 1000000, true, new List<PortStratView> { a0 });
			Check(nw(oj) == 0, "fill check: fillCheckEvals 0 -> off");
		}
		{   // PR-C: per-strategy session / weekly throttles (§5.4 4a/4b), T-P5/6/7/11/12/13/14
			double[] cum = { 0 };
			Func<int, DateTime, EPortState, int, PortStratView> TV = (ti, td, ts, tp) => { var x = V(ti, ts, pos: tp, pnl: cum[0]); x.day = td; return x; };
			Action<_PortMM, DateTime, double> TR = (tm, td, tr) =>
			{   // one closed trade of 10 contracts x R$500 (1R = $5000) on the strategy's day td
				tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Long, 10) });
				cum[0] += tr * 5000;
				tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Flat, 0) });
			};
			Func<_PortMM, DateTime, PortDecision> PR = (tm, td) => tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Armed, 0) })[0];
			var tj = new List<string>(); var tm1 = MM(null, tj);
			TR(tm1, D0, -1); Check(PR(tm1, D0).allowEntries, "T-P5: one loss -> still allowed");
			TR(tm1, D0, 2); TR(tm1, D0, -1);
			var t5 = PR(tm1, D0);
			Check(!t5.allowEntries && t5.reason == "session stop" && !t5.forceClose && !t5.pause, "T-P5: 2 losses in a session (winner between does not reset) -> session stop, no force-close/pause");
			Check(tj.Any(x => x.StartsWith("PORT Info: skip") && x.Contains("session stop") && x.Contains("risk $") && x.Contains("budget $")), "T-P5: session stop skip logged at Info with risk $ and budget $");
			var t5b = PR(tm1, D0.AddDays(1));
			Check(t5b.allowEntries && t5b.entryContracts > 0, "T-P5: allowed again on the strategy's next pm.day");
			// T-P14 scratch boundary
			cum[0] = 0; var tm2 = MM();
			TR(tm2, D0, -0.05); TR(tm2, D0, -0.05); Check(PR(tm2, D0).allowEntries, "T-P14: R -0.05 x2 are scratches, not losses");
			TR(tm2, D0, -0.0501); TR(tm2, D0, -0.0501); Check(PR(tm2, D0).reason == "session stop", "T-P14: R -0.0501 x2 are losses");
			// T-P6 weekly
			cum[0] = 0; var wj = new List<string>(); var tm3 = MM(null, wj);
			TR(tm3, D0, -2.9); var w0 = PR(tm3, D0.AddDays(1));
			Check(w0.allowEntries && w0.entryContracts == (int)Math.Floor(0.005 * (100000000 + cum[0]) / 500 + 1e-9), "T-P6: -2.9R week -> full size");
			TR(tm3, D0.AddDays(1), -0.1); var w1 = PR(tm3, D0.AddDays(2));
			int full = (int)Math.Floor(0.005 * (100000000 + cum[0]) / 500 + 1e-9);
			Check(w1.allowEntries && w1.entryContracts == (int)Math.Floor(0.5 * 0.005 * (100000000 + cum[0]) / 500 + 1e-9) && w1.entryContracts < full, "T-P6: -3.0R week -> size halved");
			Check(wj.Any(x => x.StartsWith("PORT weekly halve")), "T-P6: halve journalled");
			TR(tm3, D0.AddDays(2), -3); var w2 = PR(tm3, D0.AddDays(3));
			Check(!w2.allowEntries && w2.reason == "weekly observe" && wj.Any(x => x.Contains("PORT observe")), "T-P6: -6R week -> observe only (PORT observe)");
			var w3 = PR(tm3, new DateTime(2024, 3, 17));   // Sunday-evening ETH key: Monday's week
			Check(w3.allowEntries && w3.entryContracts == (int)Math.Floor(0.005 * (100000000 + cum[0]) / 500 + 1e-9), "T-P6: Sunday key belongs to next week -> observe and halve cleared, full size");
			Check(_PortMM.IsoWeek(new DateTime(2024, 3, 17)) == new DateTime(2024, 3, 18) && _PortMM.IsoWeek(new DateTime(2024, 3, 16)) == D0, "T-P6: ISO week mapping (Sat -> this Monday, Sun -> next Monday)");
			// T-P7 interplay: dd 0.5 x weekly 0.5 = 0.25
			cum[0] = 0; var tm4 = MM(c => { c.dailyLossStop = 0.5; c.dailyLossFlatten = 0.6; }); TR(tm4, D0, -3);
			cum[0] = 20000000; tm4.Decide(D0.AddDays(1), 100000000, true, new List<PortStratView> { TV(0, D0.AddDays(1), EPortState.Flat, 0) });   // peak 1.2e8
			cum[0] = 20000000 - 8400000; var w4 = PR(tm4, D0.AddDays(2));   // dd 7% -> 0.5
			Check(tm4.Mult == 0.5 && w4.entryContracts == (int)Math.Floor(0.25 * 0.005 * (100000000 + cum[0]) / 500 + 1e-9), $"T-P7: dd 0.5 x weekly 0.5 = 0.25 size ({w4.entryContracts} {tm4.Mult})");
			// T-P11 reversal
			cum[0] = 0; var tm5 = MM();
			tm5.Decide(D0, 100000000, true, new List<PortStratView> { TV(0, D0, EPortState.Long, 10) });
			cum[0] -= 5000; tm5.Decide(D0, 100000000, true, new List<PortStratView> { TV(0, D0, EPortState.Short, -10) });
			cum[0] -= 5000; tm5.Decide(D0, 100000000, true, new List<PortStratView> { TV(0, D0, EPortState.Flat, 0) });
			Check(PR(tm5, D0).reason == "session stop", "T-P11: reversal (+10 -> -10) records a closed trade");
			// T-P12 per-strategy day keys
			cum[0] = 0; var tm6 = MM(); TR(tm6, D0, -1); TR(tm6, D0, -1);
			var es = TV(0, D0, EPortState.Armed, 0); var clv = V(1); clv.day = D0.AddDays(1); clv.cluster = "x";
			var d12 = tm6.Decide(D0.AddDays(1), 100000000, true, new List<PortStratView> { es, clv });
			Check(!d12[0].allowEntries && d12[0].reason == "session stop" && d12[1].allowEntries, "T-P12: ES session stop not reset by CL's new pm.day");
			// T-P13 CodeSize
			cum[0] = 0; var tm7 = MM(c => c.sizing = EPortSizing.CodeSize); TR(tm7, D0, -3);
			var w7 = PR(tm7, D0.AddDays(1));
			Check(!w7.allowEntries && w7.reason == "throttle under CodeSize", "T-P13: CodeSize + weekly halve -> skip");
			cum[0] = 20000000; var tm8 = MM(c => { c.sizing = EPortSizing.CodeSize; c.dailyLossStop = 0.5; c.dailyLossFlatten = 0.6; });
			tm8.Decide(D0, 100000000, true, new List<PortStratView> { TV(0, D0, EPortState.Flat, 0) });
			cum[0] = 20000000 - 8400000; var w8 = PR(tm8, D0.AddDays(1));
			Check(!w8.allowEntries && w8.reason == "throttle under CodeSize", "T-P13: CodeSize + ddSteps 0.5 -> skip");
			var w9 = MM(c => c.sizing = EPortSizing.CodeSize).Decide(D0, 100000000, true, new List<PortStratView> { TV(0, D0, EPortState.Armed, 0) })[0];
			Check(w9.allowEntries && w9.entryContracts == -1, "T-P13: CodeSize at mult 1 -> allowed with code qty");
			// validation + JSON
			Check(new PortMmConfig { weeklyObserveR = -3, weeklyHalveR = -3 }.Validate() != null && new PortMmConfig { weeklyHalveMult = 1 }.Validate() != null
				&& new PortMmConfig { stratDailyLosses = 0 }.Validate() != null && new PortMmConfig { scratchR = 1 }.Validate() != null && new PortMmConfig().Validate() == null, "PR-C config validation");
		}
		{   // PR-C review: F8 JSON keys, F6 missing pm.day warning, F2 gate order
			var jw = new List<string>();
			var jc = PortMmConfigLoader.Parse("{\"stratDailyLosses\": 3, \"scratchR\": 0.1, \"weeklyHalveR\": -2, \"weeklyHalveMult\": 0.25, \"weeklyObserveR\": -5}", new PortMmConfig(), jw);
			Check(jc.stratDailyLosses == 3 && Near(jc.scratchR, 0.1) && Near(jc.weeklyHalveR, -2) && Near(jc.weeklyHalveMult, 0.25) && Near(jc.weeklyObserveR, -5) && jw.Count == 0, "F8: JSON parses all five PR-C keys");
			var jw2 = new List<string>();
			var jc2 = PortMmConfigLoader.Parse("{\"weeklyHalveR\": 1}", new PortMmConfig(), jw2);
			Check(Near(jc2.weeklyHalveR, -3) && jw2.Any(x => x.Contains("weeklyHalveR")), "F8: bad weeklyHalveR (1) warns and keeps -3");
			var jw3 = new List<string>();
			var jc3 = PortMmConfigLoader.Parse("{\"weeklyHalveR\": -6, \"weeklyObserveR\": -3}", new PortMmConfig(), jw3);
			Check(new PortMmConfig { weeklyHalveR = -6, weeklyObserveR = -3 }.Validate() != null && Near(jc3.weeklyHalveR, -3) && Near(jc3.weeklyObserveR, -6) && jw3.Count > 0, "F8: halve -6 / observe -3 rejected (defaults kept, warned)");
			var nj = new List<string>(); var nm = MM(null, nj);
			for (int k = 0; k < 3; k++) { nm.Decide(D0.AddDays(k), 1000000, true, new List<PortStratView> { V(0, EPortState.Long, pos: 2) }); nm.Decide(D0.AddDays(k), 1000000, true, new List<PortStratView> { V(0) }); }
			Check(nj.Count(x => x.Contains("no pm.day, 4a/4b use book day")) == 1, "F6: missing pm.day -> one Warning per strategy");
			var oj = new List<string>(); var om = MM(c => { c.sizing = EPortSizing.CodeSize; c.maxPositions = 1; c.dailyLossStop = 0.5; c.dailyLossFlatten = 0.6; }, oj);
			om.Decide(D0, 1000000, true, new List<PortStratView> { V(0, pnl: 200000), V(1, EPortState.Long, pos: 2, pnl: 0) });
			var od = om.Decide(D0.AddDays(1), 1000000, true, new List<PortStratView> { V(0, pnl: 130000), V(1, EPortState.Long, pos: 2, pnl: 0) });
			Check(om.Mult < 1 && od[0].reason == "maxPositions", $"F2: maxPositions checked before the CodeSize throttle ({od[0].reason} {om.Mult})");
		}
		{   // PR-D: risk profiles (§5.1/§5.1a) T-P1/2/8/9/10/15/17, P2 (c) T-P20, T-P5/T-P6 under both profiles
			var st = PortMmConfig.ForProfile(EPortProfile.Starter); var sb = PortMmConfig.ForProfile(EPortProfile.ScaledBook);
			Func<PortMmConfig, string> dds = c => string.Join(",", c.ddSteps.Select(x => x.dd + ">" + x.mult));
			Check(st.Validate() == null && sb.Validate() == null, "T-P1: both profiles validate");
			Check(st.profile == EPortProfile.Starter && Near(st.bookVolTarget, 0.12) && Near(st.heatCap, 0.015) && Near(st.clusterCap, 0.0075) && st.maxPositions == 2 && dds(st) == "0.03>0.5,0.06>0.25,0.1>0", "T-P1: Starter values (§5.1)");
			Check(sb.profile == EPortProfile.ScaledBook && Near(sb.bookVolTarget, 0.10) && Near(sb.heatCap, 0.04) && Near(sb.clusterCap, 0.02) && sb.maxPositions == 6 && dds(sb) == "0.05>0.5,0.1>0.25,0.15>0", "T-P1: ScaledBook values (§5.1)");
			Check(new PortMmConfig().ToString() == st.ToString() && dds(new PortMmConfig()) == dds(st), "T-P1: new PortMmConfig() equals Starter");
			var sbx = sb.Copy(); sbx.profile = EPortProfile.Starter; sbx.bookVolTarget = 0.12; sbx.heatCap = 0.015; sbx.clusterCap = 0.0075; sbx.maxPositions = 2; sbx.ddSteps = st.ddSteps;
			Check(sbx.ToString() == st.ToString(), "T-P1: every other field identical in both profiles");

			// T-P2 starter heat, book 1M: heat 15 000, cluster 7 500, per-trade 10 000
			var d2 = MP(EPortProfile.Starter).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 500, "A"), W(1, 500, "B") });
			Check(d2[0].allowEntries && d2[1].allowEntries && d2[0].entryContracts == 10 && d2[1].entryContracts == 10, "T-P2: two 5 000 entries in different clusters allowed");
			d2 = MP(EPortProfile.Starter).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 500, "A"), W(1, 500, "A") });
			Check(d2[0].allowEntries && !d2[1].allowEntries && d2[1].reason == "cluster cap", "T-P2: same cluster second 5 000 -> 10 000 > 7 500 cluster cap");
			d2 = MP(EPortProfile.Starter).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 100, "A"), W(1, 100, "B"), W(2, 100, "C") });
			Check(d2[0].allowEntries && d2[1].allowEntries && !d2[2].allowEntries && d2[2].reason == "maxPositions", $"T-P2: third denied by maxPositions 2 with heat room ({d2[2].reason})");
			d2 = MP(EPortProfile.Starter).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 900, "A"), W(1, 900, "B") });
			Check(d2[0].allowEntries && d2[0].entryContracts == 10 && !d2[1].allowEntries && d2[1].reason == "heat cap", $"T-P2: 9 000 + 9 000 -> second heat cap, first keeps n ({d2[1].reason})");

			// T-P20 P2 (c)
			Func<EPortProfile, List<PortStratView>, PortDecision[]> D = (pf, l) => MP(pf).Decide(D0, 1000000, true, l);
			var e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 900, "EQ") });
			Check(e[0].allowEntries && e[0].entryContracts == 10, "T-P20 (i): lone ES 9 000 > cluster 7 500 in an empty cluster -> allowed, n unchanged");
			Check(D(EPortProfile.Starter, new List<PortStratView> { W(0, 750, "EQ") })[0].allowEntries && D(EPortProfile.Starter, new List<PortStratView> { W(0, 1000, "EQ") })[0].allowEntries, "T-P20 (i): 7 500 and 10 000 allowed");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 1000.1, "EQ") });
			Check(!e[0].allowEntries && e[0].reason == "per-trade budget", $"T-P20 (i): 10 001 -> per-trade budget, not cluster cap ({e[0].reason})");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 900, "EQ", EPortState.Long, 10, 9000), W(1, 500, "EQ") });
			Check(!e[1].allowEntries && e[1].reason == "cluster cap", $"T-P20 (ii): ES open 9 000 + NQ 5 000 -> cluster cap ({e[1].reason})");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 900, "EQ"), W(1, 500, "EQ") });
			Check(e[0].allowEntries && e[0].entryContracts == 10 && !e[1].allowEntries && e[1].reason == "cluster cap", "T-P20 (iii): same step, entry allowed earlier counts as holding");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 900, "EQ", EPortState.Long, 10, 9000), W(1, 500, "CL") });
			Check(e[1].allowEntries && e[1].entryContracts == 10, "T-P20 (iv): ES open + CL 5 000 (other cluster) allowed");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 900, "EQ", EPortState.Flat), W(1, 900, "EQ") });
			Check(e[1].allowEntries && e[1].entryContracts == 10, "T-P20 (v): after ES closes, NQ 9 000 allowed");
			var sbc = MP(EPortProfile.ScaledBook, c => c.clusterCap = 0.005).Decide(D0, 1000000, true, new List<PortStratView> { W(0, 900, "EQ"), W(1, 500, "EQ") });
			Check(sbc[0].allowEntries && sbc[0].entryContracts == 10 && !sbc[1].allowEntries && sbc[1].reason == "cluster cap", "T-P20 (vi): ScaledBook same rule (lone not checked, second checked)");
			e = D(EPortProfile.ScaledBook, new List<PortStratView> { W(0, 900, "EQ", EPortState.Long, 20, 15000), W(1, 900, "EQ") });
			Check(!e[1].allowEntries && e[1].reason == "cluster cap", "T-P20 (vi): ScaledBook 15 000 open + 9 000 > 20 000 -> cluster cap");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 500, "EQ", EPortState.Flat, 0, 5000), W(1, 500, "EQ") });
			Check(!e[1].allowEntries && e[1].reason == "cluster cap", "T-P20 (vii): flat publisher with seeded riskOpen 5 000 occupies the cluster -> cluster cap");
			e = D(EPortProfile.Starter, new List<PortStratView> { W(0, 500, "EQ", EPortState.Flat, 0, 0), W(1, 500, "EQ") });
			Check(e[1].allowEntries, "T-P20 (vii): riskOpen 0 -> cluster empty -> allowed");

			// T-P10 / T-P17 JSON + fallback
			var w = new List<string>();
			var pj = PortMmConfigLoader.Parse("{\"maxPositions\": 3, \"profile\": \"scaledbook\"}", st, w);
			Check(pj.profile == EPortProfile.ScaledBook && Near(pj.heatCap, 0.04) && pj.maxPositions == 3 && w.Count == 0, "T-P10: profile applied first (even listed last), fields override");
			w.Clear(); pj = PortMmConfigLoader.Parse("{\"profile\": \"Huge\"}", sb, w);
			Check(pj.profile == EPortProfile.ScaledBook && Near(pj.heatCap, 0.04) && w.Count == 1 && w[0].Contains("unknown profile"), "T-P10: unknown profile -> warning, subclass base kept");
			w.Clear(); pj = PortMmConfigLoader.Parse("{\"profile\": 1}", st, w);
			Check(pj.profile == EPortProfile.Starter && w.Count == 1, "T-P10: non-string profile -> warning");
			string tf = System.IO.Path.GetTempFileName(); var inf = new List<string>(); bool fb;
			System.IO.File.WriteAllText(tf, "{\"clusterCap\": 0.02}"); w.Clear();
			var rc = PortMmConfigLoader.Resolve(PortMmConfig.ForProfile(EPortProfile.Starter), EPortProfile.Starter, tf, w, inf, out fb);
			Check(fb && rc.ToString() == st.ToString() && w.Any(x => x.Contains("rejected")), "T-P10: starter file clusterCap 0.02 rejected -> ForProfile(Starter), fallback");
			System.IO.File.WriteAllText(tf, "{\"maxTradeRisk\": 0.02}"); w.Clear();
			rc = PortMmConfigLoader.Resolve(PortMmConfig.ForProfile(EPortProfile.ScaledBook), EPortProfile.ScaledBook, tf, w, inf, out fb);
			Check(fb && rc.ToString() == sb.ToString(), "T-P10 add.: maxTradeRisk 0.02 rejected -> ForProfile(ScaledBook), fallback");
			System.IO.File.WriteAllText(tf, "{\"maxPositions\": 1}"); w.Clear();
			rc = PortMmConfigLoader.Resolve(PortMmConfig.ForProfile(EPortProfile.Starter), EPortProfile.Starter, tf, w, inf, out fb);
			Check(!fb && rc.maxPositions == 1 && w.Count == 0, "T-P10: valid file -> no fallback");
			System.IO.File.Delete(tf); w.Clear();
			var badStarter = PortMmConfig.ForProfile(EPortProfile.Starter); badStarter.clusterCap = 0.5;
			rc = PortMmConfigLoader.Resolve(badStarter, EPortProfile.Starter, null, w, inf, out fb);
			Check(fb && rc.ToString() == st.ToString() && rc.profile == EPortProfile.Starter && w.Count == 1, "T-P17: invalid starter subclass Config -> ForProfile(Starter) (not ScaledBook), config=fallback");
			rc = PortMmConfigLoader.Resolve(null, EPortProfile.ScaledBook, null, w, inf, out fb);
			Check(fb && rc.profile == EPortProfile.ScaledBook, "T-P17: null Config -> ForProfile(Profile)");

			// T-P8 / T-P9 / T-P15 advisory counts
			var aj = new List<string>(); var am = MP(EPortProfile.Starter, null, aj);
			am.Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "ES"), V(1, cl: "CL"), V(2, cl: "GC") });
			Check(am.Uncorr == 3, $"T-P8: ES + CL + GC -> uncorr 3 ({am.Uncorr})");
			Check(aj.Count(x => x.StartsWith("PORT advisory")) == 1 && am.config.profile == EPortProfile.Starter && Near(am.config.heatCap, 0.015), "T-P9: one advisory line, no automatic switch");
			am.Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "ES"), V(1, cl: "CL"), V(2, cl: "GC") });
			Check(aj.Count(x => x.StartsWith("PORT advisory")) == 1, "T-P9: advisory once per day");
			var bm = MP(EPortProfile.Starter); bm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "EQIDX"), V(1, cl: "EQIDX"), V(2, cl: "CL") });
			Check(bm.Uncorr == 2, "T-P8: ES + NQ (same seed) + CL -> 2");
			var pv = V(2, cl: "GC"); pv.paused = true; var stv = V(3, cl: "SI"); stv.stale = true; var npv = V(4, cl: "HG"); npv.publishes = false;
			var cm = MP(EPortProfile.Starter); cm.Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "ES"), V(1, cl: "CL"), pv, stv, npv });
			Check(cm.Uncorr == 2, "T-P8: paused / stale / non-publishing excluded");
			var lm2 = MP(EPortProfile.Starter, c => c.liveIds = new[] { "S0" }); lm2.Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "ES"), V(1, cl: "CL") });
			Check(lm2.Uncorr == 1, "T-P8: liveIds restricts live set");
			int u, u3;
			_PortMM.Counts(3, i => true, new[] { 0, 1, 1 }, new[] { "A", "B", "C" }, new double[,] { { 1, 0.1, 0.2 }, { 0.1, 1, 0.95 }, { 0.2, 0.95, 1 } }, out u, out u3);
			Check(u == 2 && u3 == 2, "T-P8: CL/GC rho >= 0.9 merged -> uncorr 2");
			var R = new double[,] { { 1, 0.1, 0.5, 0.2, 0.1 }, { 0.1, 1, 0.2, 0.35, 0.0 }, { 0.5, 0.2, 1, 0.1, 0.1 }, { 0.2, 0.35, 0.1, 1, 0.25 }, { 0.1, 0.0, 0.1, 0.25, 1 } };
			_PortMM.Counts(5, i => true, new[] { 0, 1, 2, 3, 4 }, new[] { "A", "B", "C", "D", "E" }, R, out u, out u3);
			Check(u == 5 && u3 == 3, $"T-P15: greedy rho<0.3 on a fixed matrix -> {{0,1,4}} = 3 ({u3})");
			_PortMM.Counts(5, i => i != 1, new[] { 0, 1, 2, 3, 4 }, new[] { "A", "B", "C", "D", "E" }, R, out u, out u3);
			Check(u == 4 && u3 == 3, $"T-P15: non-live skipped -> {{0,3,4}} ({u3})");
			R[0, 1] = R[1, 0] = double.NaN; _PortMM.Counts(2, i => true, new[] { 0, 1 }, new[] { "A", "B" }, R, out u, out u3);
			Check(u3 == 1, "T-P15: unknown rho counts as correlated");
			Check(_PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 3, 3).Contains("- scaled-book eligible") && _PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 3, 2).Contains("not eligible")
				&& _PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 2, 3).Contains("not eligible") && _PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 3, 3).Contains("uncorr≥3 and uncorr03≥3; switch manually"), "T-P9: eligible only when both counts >= 3");

			// PR-D review: F1 scaled-book sample, F2 rejected flag, ρ unknown + picked ids in the advisory
			string ssb = System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../samples/PortMmConfig.scaled-book.json"); w.Clear(); inf.Clear(); bool rj;
			var psb = PortMmConfigLoader.LoadFile(ssb, st, w, inf, out rj);
			Check(!rj && w.Count == 0 && psb.ToString() == sb.ToString() && Near(psb.bookVolTarget, 0.10), $"F1: samples/PortMmConfig.scaled-book.json on a Starter base equals ForProfile(ScaledBook) ({string.Join(" | ", w)})");
			w.Clear(); PortMmConfigLoader.Parse("{\"clusterCap\": 0.02}", st, w, out rj); bool rj2; PortMmConfigLoader.Parse("{\"maxPositions\": 3}", st, w, out rj2); bool rj3; PortMmConfigLoader.Parse("[1]", st, w, out rj3);
			Check(rj && !rj2 && rj3, "F2: Parse reports rejected via out flag");
			w.Clear(); string tf2 = System.IO.Path.GetTempFileName(); System.IO.File.WriteAllText(tf2, "{\"_note\": \"rejected ( text only in a comment\", \"maxPositions\": 3}");
			rc = PortMmConfigLoader.Resolve(st, EPortProfile.Starter, tf2, w, inf, out fb); System.IO.File.Delete(tf2);
			Check(!fb && rc.maxPositions == 3, "F2: fallback no longer depends on warning text");
			int uk; List<int> pk; R[0, 1] = R[1, 0] = double.NaN;
			_PortMM.Counts(3, i => true, new[] { 0, 1, 2 }, new[] { "A", "B", "C" }, R, out u, out u3, out uk, out pk);
			Check(uk == 1 && u3 == 1 && pk.SequenceEqual(new[] { 0 }), $"advisory: unknown pairs counted ({uk}, {u3})");
			string al = _PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 3, 1, 2, new[] { "S0" });
			Check(al.Contains("ρ unknown for 2 pair(s)") && al.Contains("uncorr03=1 [S0]") && al.Contains("not eligible"), "advisory line: ρ unknown + picked ids");
			Check(!_PortMM.AdvisoryLine(EPortProfile.Starter, 0.9, 3, 3, 0, new string[0]).Contains("unknown"), "advisory line: no ρ unknown note when all known");
			var ajx = new List<string>(); MP(EPortProfile.Starter, null, ajx).Decide(D0, 1000000, true, new List<PortStratView> { V(0, cl: "ES"), V(1, cl: "CL"), V(2, cl: "GC") });
			Check(ajx.Any(x => x.StartsWith("PORT advisory") && x.Contains("uncorr03=1 [S0]") && x.Contains("ρ unknown for 3 pair(s)")), "advisory: no history -> ρ unknown, picked S0");

			// T-P5 / T-P6 under both profiles (P5: same results)
			foreach (var pf in new[] { EPortProfile.Starter, EPortProfile.ScaledBook })
			{
				double[] cum = { 0 }; string L = " [" + pf + "]";
				Func<int, DateTime, EPortState, int, PortStratView> TV = (ti, td, ts, tp) => { var x = V(ti, ts, pos: tp, pnl: cum[0]); x.day = td; return x; };
				Action<_PortMM, DateTime, double> TR = (tm, td, tr) =>
				{
					tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Long, 10), V(1, cl: "o") });
					cum[0] += tr * 5000;
					tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Flat, 0), V(1, cl: "o") });
				};
				Func<_PortMM, DateTime, PortDecision[]> PR = (tm, td) => tm.Decide(td, 100000000, true, new List<PortStratView> { TV(0, td, EPortState.Armed, 0), V(1, cl: "o") });
				var t1 = MP(pf);
				TR(t1, D0, -1); Check(PR(t1, D0)[0].allowEntries, "T-P5: one loss -> allowed" + L);
				TR(t1, D0, 2); TR(t1, D0, -1); var p5 = PR(t1, D0);
				Check(!p5[0].allowEntries && p5[0].reason == "session stop" && !p5[0].forceClose && p5[1].allowEntries, "T-P5: 2 losses (winner between) -> session stop, other strategy allowed, no force-close" + L);
				Check(PR(t1, D0.AddDays(1))[0].allowEntries, "T-P5: allowed on next pm.day" + L);
				cum[0] = 0; var wj = new List<string>(); var t3 = MP(pf, null, wj);
				Func<int> full = () => (int)Math.Floor(0.005 * (100000000 + cum[0]) / 500 + 1e-9);
				TR(t3, D0, -2.9); Check(PR(t3, D0.AddDays(1))[0].entryContracts == full(), "T-P6: -2.9R -> full size" + L);
				TR(t3, D0.AddDays(1), -0.1); var h6 = PR(t3, D0.AddDays(2))[0];
				Check(h6.allowEntries && h6.entryContracts == (int)Math.Floor(0.5 * 0.005 * (100000000 + cum[0]) / 500 + 1e-9), "T-P6: -3.0R -> halved" + L);
				TR(t3, D0.AddDays(2), -3); var o6 = PR(t3, D0.AddDays(3))[0];
				Check(!o6.allowEntries && o6.reason == "weekly observe" && wj.Any(x => x.Contains("PORT observe")), "T-P6: -6R -> observe" + L);
				var r6 = PR(t3, new DateTime(2024, 3, 17))[0];
				Check(r6.allowEntries && r6.entryContracts == full(), "T-P6: next ISO week (Sunday key) -> both cleared, full size" + L);
			}
		}
		Console.WriteLine($"PortMMTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
