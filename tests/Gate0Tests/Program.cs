using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _Gate0 (trading-idea-catalogue.md Gate 0).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }

	static LevelEvent Ev(DateTime day, bool touched, double asym)
	{ return new LevelEvent { day = day, type = "T", key = "k", touched = touched, asymmetry = asym }; }

	static void Main()
	{
		// ---- profile: POC / value area / single prints ----
		var p = new Gate0Profile(0.25);
		p.Add(100, 100, 10); p.Add(100.25, 100.25, 50); p.Add(100.5, 100.5, 20); p.Add(100.75, 100.75, 5); p.Add(99.75, 99.75, 15);
		double poc, vah, val;
		Check(p.ValueArea(0.70, out poc, out vah, out val) && Near(poc, 100.25), "POC is the busiest row");
		Check(val <= poc && vah >= poc && val >= 99.75 && vah <= 100.75, "value area brackets the POC inside the range");
		Check(!new Gate0Profile(0.25).ValueArea(0.7, out poc, out vah, out val), "empty profile has no value area");
		var sp = new Gate0Profile(0.25);
		sp.Add(100, 100, 100); for (int i = 1; i <= 5; i++) sp.Add(100 + i * 0.25, 100 + i * 0.25, 1); sp.Add(101.5, 101.5, 100);
		var runs = sp.SinglePrintRuns(0.05, 4);
		Check(runs.Count == 1 && Near(runs[0][0], 101.25) && Near(runs[0][1], 100.25), "single-print shelf found with upper/lower edges");
		Check(sp.SinglePrintRuns(0.05, 6).Count == 0, "shelf shorter than minRows is ignored");
		var tail = new Gate0Profile(0.25);   // bottom tail: thin rows from first+1 up to a fat row - NOT a single print
		tail.Add(100, 100, 1); for (int i = 1; i <= 5; i++) tail.Add(100 + i * 0.25, 100 + i * 0.25, 1); tail.Add(101.5, 101.5, 100);
		Check(tail.SinglePrintRuns(0.05, 4).Count == 0, "bottom tail rejected (volume arm)");
		var top = new Gate0Profile(0.25); top.Add(100, 100, 100); for (int i = 1; i <= 6; i++) top.Add(100 + i * 0.25, 100 + i * 0.25, 1);
		Check(top.SinglePrintRuns(0.05, 4).Count == 0, "top tail rejected (volume arm)");
		var bt = new Gate0Profile(0.25); bt.Add(100, 100, 100); bt.Add(100.25, 100.25, 1); for (int i = 2; i <= 6; i++) bt.Add(100 + i * 0.25, 100 + i * 0.25, 100);
		Check(bt.SinglePrintRuns(0.05, 1).Count == 1, "an interior one-row run is found at minRows 1");
		var tpo = new Dictionary<long, int>();
		for (long r = 400; r <= 420; r++) tpo[r] = r <= 405 ? 3 : r <= 410 ? 1 : 2;   // single TPOs 406..410 inside
		var tr = Gate0Profile.TpoSinglePrints(tpo, 4, 0.25);
		Check(tr.Count == 1 && Near(tr[0][0], 410 * 0.25) && Near(tr[0][1], 406 * 0.25), "TPO single prints: interior run of count-1 rows");
		var tpoTail = new Dictionary<long, int>(); for (long r = 400; r <= 420; r++) tpoTail[r] = r <= 405 ? 1 : 3;
		Check(Gate0Profile.TpoSinglePrints(tpoTail, 4, 0.25).Count == 0, "TPO tail at the low (first row on) rejected");
		var tpoTail2 = new Dictionary<long, int>(); for (long r = 400; r <= 420; r++) tpoTail2[r] = r >= 415 ? 1 : 3;
		Check(Gate0Profile.TpoSinglePrints(tpoTail2, 4, 0.25).Count == 0, "TPO tail at the high rejected");

		// ---- HVN / LVN (KDE) and GMM ----
		var bi = new Gate0Profile(0.25);
		for (int i = -40; i <= 40; i++) { double x = i * 0.25; double v = 1000 * Math.Exp(-0.5 * Math.Pow((x + 5) / 1.5, 2)) + 600 * Math.Exp(-0.5 * Math.Pow((x - 5) / 1.5, 2)) + 1; bi.Add(4000 + x, 4000 + x, v); }
		List<double[]> hv, lvn; bi.Nodes(Gate0Engine.KdeBandwidthTicks, Gate0Engine.KdeProminence, out hv, out lvn);
		Check(hv.Count == 2 && Math.Abs(hv[0][0] - 3995) <= 0.5 && Math.Abs(hv[1][0] - 4005) <= 0.5, "two HVNs at the modes");
		Check(lvn.Count == 1 && Math.Abs(lvn[0][0] - 4000) <= 1.0 && lvn[0][1] > 0.1, "one LVN between them");
		var gx = new List<double>(); var gw = new List<double>(); foreach (var kv in bi.rows) { gx.Add(kv.Key * 0.25); gw.Add(kv.Value); }
		var gm = _ProfileStats.ProfileMixture(gx.ToArray(), gw.ToArray(), 4, _ProfileStats.MixtureSeed(new DateTime(2024, 3, 11)), EGmmNeff.Bars, 390, 0.25);
		Check(gm.model != null && gm.K == 2 && Math.Abs(gm.mu[0] - 3995) < 1 && Math.Abs(gm.mu[gm.K - 1] - 4005) < 1, "GMM (_Gmm core, BIC delta > 10) finds the two components (K=" + gm.K + ")");
		Check(gm.voidPrice.Length == 1 && Math.Abs(gm.voidPrice[0] - 4000) <= 1 && gm.voidDepth[0] > 0.5, "GMM void between the two modes");
		var uni = new List<double>(); var uw = new List<double>(); var ur = new Random(3); for (int i = 0; i < 200; i++) { uni.Add(ur.NextDouble() * 0.1 + 50 + 3 * Math.Sqrt(-2 * Math.Log(ur.NextDouble() + 1e-12)) * Math.Cos(2 * Math.PI * ur.NextDouble())); uw.Add(1); }
		// family 13 runs on the shared _ProfileStats.SigmaMixture path: same raw pooled volume, AwayFromZero z rounding, sigma-hat
		{
			var rg = new _Rng(55); var g0 = new List<Gate0Session>(); var gs = new List<GmmSession>(); DateTime dd = new DateTime(2024, 2, 1);
			for (int si = 0; si < 22; si++)
			{
				var s0 = new Gate0Session(dd.AddDays(si), 0.25); s0.bars1m = 390;
				for (int bj = 0; bj < 390; bj++)
				{
					double c = 4800 + si + ((bj / 65) % 2 == 0 ? -8 : 8) + Math.Round(4 * (rg.NextDouble() - 0.5)) / 4, vol = 100 + rg.NextInt(400);
					s0.profile.Add(c + 0.25, c - 0.25, vol); s0.vwap.Add(c, vol);
					if (bj % 5 == 0) { double lr = Math.Log((c + 1) / (c - 1)); s0.pkSum += lr * lr; s0.pkN++; }
				}
				g0.Add(s0);
				var gm0 = new GmmSession { day = s0.day, contract = "X", bars = 390, sVwap = s0.vwap.Value, sigma = s0.SigmaPrice };
				var pp = new List<double>(); var vv = new List<double>(); foreach (var kv in s0.profile.rows) { pp.Add(kv.Key * 0.25); vv.Add(kv.Value); }
				gm0.prices = pp.ToArray(); gm0.vols = vv.ToArray(); gs.Add(gm0);
			}
			var a13 = Gate0Engine.SigmaCompositeFit(g0, 0.25);
			var b13 = _ProfileStats.SigmaMixture(gs, _ProfileStats.MixtureSeed(gs[gs.Count - 1].day), 20, 10, 0.05, 4);
			bool eq = a13.ok && b13.ok && a13.mix.K == b13.mix.K && a13.sigmaHat == b13.sigmaHat && a13.priorVwap == b13.priorVwap && a13.mix.model.bic == b13.mix.model.bic;
			for (int k = 0; eq && k < a13.mix.K; k++) eq = a13.mix.mu[k] == b13.mix.mu[k] && a13.mix.pi[k] == b13.mix.pi[k] && a13.mix.sd[k] == b13.mix.sd[k] && a13.levels[k] == b13.levels[k];
			Check(eq, "family 13 = _ProfileStats.SigmaMixture exactly (K=" + (a13.mix == null ? 0 : a13.mix.K) + ")");
			Check(a13.sessions == 20 && a13.days[0] == dd.AddDays(2), "family 13 window = newest 20 sessions");
		}
		Check(_ProfileStats.ProfileMixture(uni.ToArray(), uw.ToArray(), 4, 7UL, EGmmNeff.Bars, 200, 0.01).K == 1, "GMM BIC picks K=1 on one Gaussian (_Gmm core)");

		// ---- buckets ----
		// educator review G2: Data1 resolution guard
		Check(Gate0Data1.Check("Regular", "Tick", 1) == null && Gate0Data1.Check("Regular", "Minute", 1) == null && Gate0Data1.Check("Regular", "Second", 1) == null && Gate0Data1.Check("Regular", "Second", 15) == null && Gate0Data1.Check("Regular", "Second", 60) == null, "Data1 guard accepts 1-tick, 1-minute, 1/15/60-second bars");
		Check(Gate0Data1.Check("Regular", "Minute", 5) != null && Gate0Data1.Check("Regular", "Minute", 0) != null && Gate0Data1.Check("Regular", "Hour", 1) != null && Gate0Data1.Check("Regular", "Day", 1) != null, "Data1 guard rejects bars longer than 1 minute");
		Check(Gate0Data1.Check("Regular", "Tick", 100) != null && Gate0Data1.Check("Regular", "Contract", 500) != null && Gate0Data1.Check("Regular", "Point", 4) != null && Gate0Data1.Check("Regular", "PointOriginal", 4) != null && Gate0Data1.Check("Regular", "Change", 2) != null && Gate0Data1.Check("Regular", "Second", 7) != null && Gate0Data1.Check("Regular", "Second", 90) != null, "Data1 guard rejects multi-tick, volume, range and misaligned second bars");
		Check(Gate0Data1.Check("Regular", "Minute", 5).Contains("5 Minute"), "Data1 guard message names the resolution");
		Check(Gate0Data1.Check("Regular", "Tick", 2) != null, "Data1 guard rejects (Regular, Tick, 2)");
		Check(Gate0Data1.Check("Regular", "Minute", 2) != null, "Data1 guard rejects (Regular, Minute, 2)");
		{ string m0 = Gate0Data1.Check("Regular", "Second", 0); Check(m0 != null && m0.Contains("positive number of seconds"), "Data1 guard rejects (Regular, Second, 0) with the positive-seconds message"); }
		{ string mn = Gate0Data1.Check("Regular", "Second", -60); Check(mn != null && mn.Contains("positive number of seconds"), "Data1 guard rejects (Regular, Second, -60) with the positive-seconds message"); }
		Check(Gate0Data1.Check("Regular", "Second", 30) == null, "Data1 guard accepts (Regular, Second, 30)");
		Check(Gate0Data1.Check("Renko", "Minute", 1) != null && Gate0Data1.Check("Kagi", "Tick", 1) != null, "Data1 guard rejects non-Regular chart types");
		Check(Gate0Bucket.EndOf(new DateTime(2024, 3, 11, 9, 31, 0), 5) == new DateTime(2024, 3, 11, 9, 35, 0), "5m bucket end");
		Check(Gate0Bucket.EndOf(new DateTime(2024, 3, 11, 9, 35, 0), 5) == new DateTime(2024, 3, 11, 9, 35, 0), "end-stamped bar on a boundary stays in its bucket");

		// ---- verdicts (catalogue 2.3) ----
		Check(Gate0Engine.Verdict(29, 5, "keep").StartsWith("PARK (no evaluation"), "n<30 no evaluation");
		Check(Gate0Engine.Verdict(100, 5, "keep").StartsWith("PARK (inconclusive"), "30<=n<150 inconclusive");
		Check(Gate0Engine.Verdict(200, 1.5, "keep").Contains("|t| < 2"), "|t| < 2 kills (A2-33)");
		Check(Gate0Engine.Verdict(200, double.NaN, "keep").Contains("kill"), "NaN t kills");
		Check(Gate0Engine.Verdict(200, 3, "drop (negative edge)").Contains("kill: drop"), "A2-33 drop kills");
		Check(Gate0Engine.Verdict(200, -3, "keep") == "PASS (keep)", "pass on A2-33 keep");
		Check(Gate0Engine.Verdict(200, 3, "trade top-quality tercile only").StartsWith("PASS"), "pass on tercile restriction");

		// ---- scoring + block bootstrap ----
		Check(Gate0Engine.BootstrapDraws == 500 && Gate0Engine.BootstrapBlock == 5, "500 draws, block size 5");
		var d0 = new DateTime(2023, 1, 2);
		var diffs = new List<KeyValuePair<DateTime, double>>();
		for (int d = 0; d < 200; d++) diffs.Add(new KeyValuePair<DateTime, double>(d0.AddDays(d), 0.5 + (d % 3 - 1) * 0.1));
		var a2 = new LevelTypeResult { type = "T", t = 4.0, pHolm = 0.001, verdict = "keep" };
		Gate0Result r1 = new Gate0Result { type = "T" }, r2 = new Gate0Result { type = "T" }, r3 = new Gate0Result { type = "T" };
		Gate0Engine.ScoreEdge(r1, diffs, a2, 33); Gate0Engine.ScoreEdge(r2, diffs, a2, 33); Gate0Engine.ScoreEdge(r3, diffs, a2, 34);
		Check(r1.pairs == 200 && Near(r1.edge, diffs.Average(kv => kv.Value)), "paired reaction edge = mean difference");
		Check(r1.ciLo == r2.ciLo && r1.ciHi == r2.ciHi, "bootstrap reproducible for the same seed");
		Check(!double.IsNaN(r3.ciLo) && r1.ciLo <= r1.edge && r1.ciHi >= r1.edge, "CI brackets the edge");
		Check(r1.verdict.StartsWith("PASS"), "A2-33 keep with |t| >= 2 passes (" + r1.verdict + ")");
		var r4 = new Gate0Result { type = "T" }; Gate0Engine.ScoreEdge(r4, diffs, new LevelTypeResult { type = "T", t = 0.5, verdict = "drop (|t| < 2)" }, 33);
		Check(r4.verdict.Contains("kill"), "edge ~ 0 (|t| < 2) is killed");
		Check(Gate0Engine.Verdict(200, 1.0, "trade top-quality tercile only").StartsWith("PASS"), "top-tercile restriction passes even when pooled |t| < 2");
		Check(!string.IsNullOrEmpty(r1.Line()) && r1.Line().Contains("tByAge"), "result journal line carries t by age");
		var lv = new LevelEvent { touched = true, reaction = 2.0 };
		Check(Near(Gate0Engine.PairedReaction(lv, new[] { new LevelEvent { touched = true, reaction = 1.0 }, new LevelEvent { touched = true, reaction = 0.5 }, new LevelEvent { touched = false } }), 2.0 - 0.75), "paired reaction = R - mean touched control R");
		Check(double.IsNaN(Gate0Engine.PairedReaction(new LevelEvent { touched = false }, null)), "untouched level has no pair");
		var hl = new List<LevelTypeResult> { new LevelTypeResult { p = 0.001 }, new LevelTypeResult { p = 0.02 } };
		Gate0Engine.HolmFixed(hl, Gate0Engine.AllTypes.Length);
		Check(Gate0Engine.AllTypes.Length == 36 && !Gate0Engine.AllTypes.Contains("AV_OPEN") && Gate0Engine.AllTypes.Distinct().Count() == 36, "Holm m = 36: AV_OPEN retired (educator review G4)");
		Check(Gate0Engine.PreRegistration().EndsWith("Holm m = 36"), "pre-registration line states Holm m = 36");
		Check(Near(hl[0].pHolm, Gate0Engine.AllTypes.Length * 0.001) && hl[1].pHolm >= hl[0].pHolm, "Holm across all Gate-0 types (fixed m)");

		// ---- A2-33 study wiring: 20 controls, either side, frozen sigma, reaction after touch ----
		{
			var st = new _LevelStudy(); st.config.enabled = true; st.config.controls = 20; st.config.reactionBars = 12; st.config.maxAge = 15;
			st.config.controlsEitherSide = true; st.config.reactionAfterTouch = true;
			Check(st.Configure() == null, "study configure");
			st.BeginSession(d0); st.SetSessionReference(100, 2.0);
			st.AddLevel("X", 104, 1, double.NaN, "x");
			for (int i = 0; i < 20; i++) st.Bar(100, 100.5, 99.5, 100, 1);   // no touch
			st.Finish();
			var ctl = st.Events.Where(e => e.control).ToList();
			Check(ctl.Count == 20, "20 matched controls");
			Check(ctl.Any(e => e.price > 100) && ctl.Any(e => e.price < 100), "controls on either side of the RTH open");
			Check(ctl.All(e => Math.Abs(e.price - 100) >= 0.9 * 4 - 0.25 && Math.Abs(e.price - 100) <= 1.1 * 4 + 0.25), "controls at 0.9-1.1x the distance from the RTH open");
			Check(st.Events.All(e => Near(e.sigma, 2.0) && Near(e.refPrice, 100)), "sigma frozen at registration (ATR14 5m), ref = RTH open");
			var s2 = new _LevelStudy(); s2.config.enabled = true; s2.config.controls = 1; s2.config.reactionBars = 2; s2.config.reactionAfterTouch = true; s2.Configure();
			s2.BeginSession(d0); s2.SetSessionReference(100, 1.0); s2.AddLevel("Y", 101, 1, double.NaN, "y");
			s2.Bar(100, 101, 100, 101, 1);    // touch bar: |101-101| = 0, not counted
			s2.Bar(101, 103, 101, 103, 1);    // k=1: 2 sigma
			s2.Bar(103, 103, 101.5, 101.5, 1);// k=2: 0.5
			s2.Bar(101.5, 110, 101.5, 110, 1);// k=3: outside the window
			s2.Finish();
			var y = s2.Events.First(e => !e.control);
			Check(y.touched && Near(y.reaction, 2.0), "R = max over k=1..12 bars AFTER the touch (" + y.reaction + ")");
		}

		// ---- educator review G4: the AVWAP family (Variant 5) no longer emits AV_OPEN; VWAP (Variant 6) is unchanged ----
		{
			Func<int, Gate0Engine> runFam = v =>
			{
				var fe = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = v }; fe.Configure();
				var fr = new Random(11); double fp = 4800; var fs = new DateTime(2024, 3, 4);
				for (int d = 0; d < 21; d++)
				{
					var day = fs.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
					for (var t = day.AddHours(-5).AddMinutes(1); t <= day.AddHours(16); t = t.AddMinutes(1))
					{ double o = fp, c = Math.Round((fp + (fr.NextDouble() - 0.5) * 2) * 4) / 4; fp = c; fe.Bar(new Gate0Bar(t, day, o, Math.Max(o, c) + 0.25, Math.Min(o, c) - 0.25, c, 100)); }
				}
				return fe;
			};
			var av = runFam(5); var vw = runFam(6);
			Check(av.study.Events.Any(e => e.type == "AV_PC") && !av.study.Events.Any(e => e.type == "AV_OPEN"), "Variant 5 registers AVWAP levels but never AV_OPEN");
			Check(vw.study.Events.Any(e => e.type == "VWAP"), "Variant 6 still registers VWAP");
		}

		// ---- roll window (+-1 session) ----
		var g = new Gate0Engine();
		for (int d = 0; d < 6; d++) g.sessionDays.Add(d0.AddDays(d));
		g.rollDays.Add(d0.AddDays(3));
		Check(!g.InRollWindow(d0.AddDays(1)) && g.InRollWindow(d0.AddDays(2)) && g.InRollWindow(d0.AddDays(3)) && g.InRollWindow(d0.AddDays(4)) && !g.InRollWindow(d0.AddDays(5)), "roll session +-1 excluded");

		// ---- configuration ----
		Check(Gate0Engine.DevEnd == new DateTime(2025, 1, 1), "development window ends 2025-01-01");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 9 }.Configure() != null, "canary variant out of range rejected (9; 8 = KD02 since PR-D8)");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 134 }.Configure() != null && new Gate0Engine { mode = EGate0Mode.Canary, variant = 129 }.Configure() != null
			&& new Gate0Engine { mode = EGate0Mode.Canary, variant = 124 }.Configure() != null, "KD-02 sweep cell out of range rejected (129, 134; 124..129 unused)");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 124 }.Configure() != null, "A2-53 sweep cell out of range rejected");
		Check(new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 14 }.Configure() != null, "level variant out of range rejected");
		Check(new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 0 }.Configure() == null, "valid configuration accepted");
		Check(!new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 0 }.VerdictRun && new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 3 }.VerdictRun, "Variant 0 is not a verdict run");
		{
			double gp; int j, m; bool fl;
			Check(Gate0Engine.DecodeA253(100, out gp, out j, out m, out fl) && Near(gp, 0.003) && j == 1 && m == 30 && fl, "A2-53 cell 100 = 0.3% j1 30m filter on");
			Check(Gate0Engine.DecodeA253(123, out gp, out j, out m, out fl) && Near(gp, 0.008) && j == 2 && m == 15 && !fl, "A2-53 cell 123 = 0.8% j2 15m filter off");
			Check(Gate0Engine.DecodeA253(108, out gp, out j, out m, out fl) && Near(gp, 0.005) && j == 1 && m == 30 && fl, "A2-53 cell 108 = catalogue default");
			Check(Gate0Engine.DecodeA253(5, out gp, out j, out m, out fl) && Near(gp, 0.005) && j == 1 && m == 30 && fl, "Variant 5 = default cell");
			var cells = new HashSet<string>(); for (int v = 100; v <= 123; v++) { Gate0Engine.DecodeA253(v, out gp, out j, out m, out fl); cells.Add(gp + "/" + j + "/" + m + "/" + fl); }
			Check(cells.Count == 24 && !Gate0Engine.DecodeA253(99, out gp, out j, out m, out fl), "24 distinct sweep cells");
			var ce = new Gate0Engine { mode = EGate0Mode.Canary, variant = 111 }; ce.Configure();
			Check(ce.a253Minutes == 15 && ce.a253Bar == 1 && Near(ce.a253Gap, 0.005) && !ce.a253Filter && ce.CanaryName.StartsWith("A2_53"), "Configure applies the decoded cell");
		}
		Check(Gate0Engine.InsideDay(10, 5, 10, 5) && Gate0Engine.InsideDay(9, 6, 10, 5) && !Gate0Engine.InsideDay(10.25, 6, 10, 5) && !Gate0Engine.InsideDay(9, 4.75, 10, 5), "inside day is inclusive");

		// ---- DV_ROT_POC: rotation -> breakout -> follow-through ----
		{
			var de = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 10 }; de.Configure();
			var day0 = new DateTime(2023, 3, 6); bool found = false;
			for (int d = 0; d < 3; d++)
			{
				var day = day0.AddDays(d);
				for (var t = day.AddHours(-5); t <= day.AddHours(16); t = t.AddMinutes(1))
				{
					double c = 4000 + ((t.Minute % 4) < 2 ? 0 : 2);
					if (d == 2 && t.TimeOfDay > new TimeSpan(12, 30, 0) && t.TimeOfDay <= new TimeSpan(13, 0, 0)) c = 4002 + (t.TimeOfDay - new TimeSpan(12, 30, 0)).TotalMinutes * 0.25;
					else if (d == 2 && t.TimeOfDay > new TimeSpan(13, 0, 0)) c = 4009.5 + ((t.Minute % 4) < 2 ? 0 : 0.5);
					de.Bar(new Gate0Bar(t, day, c, c, c, c, 100));
					if (d == 2 && t.TimeOfDay == new TimeSpan(13, 1, 0)) found = de.DvLevels.Any(x => x.type == "DV_ROT_POC_R6" && x.dir == 1 && x.price >= 4000 && x.price <= 4002);
				}
			}
			Check(found, "DV_ROT_POC_R6 registered after rotation, breakout >= 1.5 ATR30 and follow-through, level = box POC");
		}

		// ---- pre-registration journal + h_cv arm ----
		{
			var lines = new List<string>();
			var pe = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 11, journal = lines.Add };
			Check(pe.Configure() == null && lines.Count == 1 && lines[0].StartsWith("G0 PRE-REGISTERED") && lines[0].Contains("DV_ROT_POC_R6") && lines[0].Contains("DV_ROT_POC_R12")
				&& lines[0].Contains("6 ticks") && lines[0].Contains("HVN_HCV") && lines[0].Contains("h_cv"), "pre-registered line journaled at Configure");
			Check(Gate0Engine.AllTypes.Contains("HVN_HCV") && Gate0Engine.AllTypes.Contains("LVN_HCV"), "h_cv arm types are in the Holm family");
			var ce2 = new List<string>(); new Gate0Engine { mode = EGate0Mode.Canary, variant = 0, journal = ce2.Add }.Configure();
			Check(ce2.Count == 0, "no pre-registration line in Canary mode");
		}

		// ---- every canary (0..8 and the sweep cells) runs over bars without throwing ----
		foreach (int cvn in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 100, 111, 123, 130, 133 })
		{
			bool ok = true;
			try
			{
				var ke = new Gate0Engine { mode = EGate0Mode.Canary, variant = cvn }; ok = ke.Configure() == null;
				var kr = new Random(5); double kp = 4800; var ks = new DateTime(2023, 1, 2);
				for (int d = 0; d < 10; d++)
				{
					var day = ks.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
					for (var t = day.AddHours(-1); t <= day.AddHours(16); t = t.AddMinutes(1))
					{ double o = kp, c = Math.Round((kp + (kr.NextDouble() - 0.5) * 2) * 4) / 4; kp = c; ke.Bar(new Gate0Bar(t, day, o, Math.Max(o, c) + 0.25, Math.Min(o, c) - 0.25, c, 100)); }
				}
				ok &= !string.IsNullOrEmpty(ke.CanaryName);
			}
			catch (Exception) { ok = false; }
			Check(ok, "canary " + cvn + " runs");
		}

		Kd02Tests();

		// ---- every family runs and emits its types ----
		{
			var want = new Dictionary<int, string> { { 1, "POC" }, { 7, "WPOC" }, { 8, "CVAH" }, { 11, "HVN" }, { -11, "HVN_HCV" }, { 12, "GMM_MU" }, { 13, "SIG_MU" } };
			foreach (var kv in want)
			{
				var fe = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = Math.Abs(kv.Key) }; fe.Configure();
				var fr = new Random(11); double fp = 4800; var fs = new DateTime(2023, 1, 2);
				for (int d = 0; d < 45; d++)
				{
					var day = fs.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
					for (var t = day.AddHours(-2); t <= day.AddHours(16); t = t.AddMinutes(1))
					{ double o = fp, c = Math.Round((fp + (fr.NextDouble() - 0.5) * 2) * 4) / 4; fp = c; fe.Bar(new Gate0Bar(t, day, o, Math.Max(o, c) + 0.25, Math.Min(o, c) - 0.25, c, 100)); }
				}
				fe.Finish();
				Check(fe.study.Events.Any(e => e.type == kv.Value) && fe.study.Events.Where(e => !e.control).All(e => e.age >= 1 && e.age <= 15), "family " + kv.Key + " emits " + kv.Value);
				if (kv.Key == -11) Check(fe.History.Count > 0 && fe.History.All(h => h.hCv > 0 && h.rth1m == null) && fe.study.Events.Any(e => e.type == "HVN"), "h_cv computed per session (bars released), both arms emitted");
				if (kv.Key == 1) Check(fe.study.Events.Any(e => e.type == "POC" && e.age == 15), "prior POC ages reach 15");
			}
		}

		// ---- educator review G1: ATR5 and ATR30 restart at a roll (only the splice TR is dropped) ----
		{
			Func<double, bool, int, Gate0Engine> runRoll = (gap, flag, endMin) =>
			{
				var re = new Gate0Engine { mode = EGate0Mode.Canary, variant = 0 }; re.Configure();
				var rs = new DateTime(2024, 3, 4); double rp = 4000; int sd = 0;
				for (int d = 0; d < 6; d++)
				{
					var day = rs.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
					bool first = true; if (sd++ == 3) rp += gap;
					for (var t = day.AddHours(-5).AddMinutes(1); t <= day.AddMinutes(endMin); t = t.AddMinutes(1))
					{ re.Bar(new Gate0Bar(t, day, rp, rp + 0.25, rp - 0.25, rp, 100, flag && sd == 4 && first)); first = false; }
				}
				return re;
			};
			Gate0Engine flat = runRoll(0, false, 960), rolled = runRoll(100, true, 960), unflagged = runRoll(100, false, 960);
			Check(Near(flat.Atr5, 0.5) && Near(flat.Atr30, 0.5), "roll control: ATR5/ATR30 = bucket range 0.5 (" + flat.Atr5 + "/" + flat.Atr30 + ")");
			Check(BitConverter.DoubleToInt64Bits(rolled.Atr5) == BitConverter.DoubleToInt64Bits(flat.Atr5), "ATR5 restarts at a roll: the 100-pt splice gap never enters TR (" + rolled.Atr5 + ")");
			Check(BitConverter.DoubleToInt64Bits(rolled.Atr30) == BitConverter.DoubleToInt64Bits(flat.Atr30), "ATR30 restarts at a roll: the 100-pt splice gap never enters TR (" + rolled.Atr30 + ")");
			Check(unflagged.Atr30 > flat.Atr30 + 1, "without the roll flag the same gap inflates ATR30 (test has teeth: " + unflagged.Atr30 + ")");
			Check(unflagged.Atr5 > flat.Atr5, "without the roll flag the same gap inflates ATR5 (teeth: " + unflagged.Atr5 + ")");
			// PR #43 review (Analyst 1): sessions end at 15:58, off a bucket boundary, so the old contract's last 5m/30m bucket
			// is still open when the roll bar arrives and is flushed by its PreBar. The reset must come after that flush.
			Gate0Engine flat58 = runRoll(0, false, 958), rolled58 = runRoll(100, true, 958);
			Check(BitConverter.DoubleToInt64Bits(rolled58.Atr5) == BitConverter.DoubleToInt64Bits(flat58.Atr5), "ATR5 restarts at a roll when the old session ends off a bucket boundary (15:58: " + rolled58.Atr5 + ")");
			Check(BitConverter.DoubleToInt64Bits(rolled58.Atr30) == BitConverter.DoubleToInt64Bits(flat58.Atr30), "ATR30 restarts at a roll when the old session ends off a bucket boundary (15:58: " + rolled58.Atr30 + ")");
		}

		// ---- PR #43 nit: off-boundary stamps (stand-in for tick data) - the old-contract bucket still open at the roll closes BEFORE
		// the ATR restart. NOTE: these are 1-minute bars stamped at :30, not real ticks; any stamp that leaves a 5m/30m bucket
		// pending at the roll exercises the same path. ----
		{
			Func<double, bool, Gate0Engine> runOffStamp = (gap, flag) =>
			{
				var re = new Gate0Engine { mode = EGate0Mode.Canary, variant = 0 }; re.Configure();
				var rs = new DateTime(2024, 3, 4); double rp = 4000; int sd = 0;
				for (int d = 0; d < 6; d++)
				{
					var day = rs.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
					bool first = true; if (sd++ == 3) rp += gap;
					var last = day.AddHours(16).AddSeconds(-30);
					for (var t = day.AddHours(-5).AddSeconds(30); t <= last; t = t.AddMinutes(1))
					{
						if (sd == 3 && t == last) rp += 1.0;   // old contract: the last bar (bucket still pending at the roll) jumps 1 pt and stays
						double tpx = rp;
						re.Bar(new Gate0Bar(t, day, tpx, tpx + 0.25, tpx - 0.25, tpx, 100, flag && sd == 4 && first)); first = false;
					}
				}
				return re;
			};
			Gate0Engine ctl = runOffStamp(0, false), rol = runOffStamp(100, true);
			Check(ctl.Atr30 > 0.5 && BitConverter.DoubleToInt64Bits(rol.Atr5) == BitConverter.DoubleToInt64Bits(ctl.Atr5) && BitConverter.DoubleToInt64Bits(rol.Atr30) == BitConverter.DoubleToInt64Bits(ctl.Atr30),
				"off-boundary stamps (1m bars at :30, tick stand-in): the pending old-contract bucket keeps its true range; only the splice TR is dropped (" + rol.Atr30 + " vs " + ctl.Atr30 + ")");
		}

		// ---- smoke: random walk across the holdout boundary never records holdout events ----
		var eng = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 0 };
		Check(eng.Configure() == null, "configure smoke engine");
		var rng = new Random(7); double px = 4800; var start = new DateTime(2024, 11, 1);
		for (int d = 0; d < 75; d++)
		{
			var day = start.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
			for (var t = day.AddHours(-5); t <= day.AddHours(16); t = t.AddMinutes(1))
			{
				double o = px, c = Math.Round((px + (rng.NextDouble() - 0.5) * 2) * 4) / 4; px = c;
				eng.Bar(new Gate0Bar(t, day, o, Math.Max(o, c) + 0.25, Math.Min(o, c) - 0.25, c, 100));
			}
		}
		var res = eng.Finish();
		Check(eng.study.Events.All(e => e.day < Gate0Engine.DevEnd), "no level registered on or after 2025-01-01");
		Check(res.All(x => !string.IsNullOrEmpty(x.verdict)), "every scored level type gets a verdict");
		Check(res.All(x => x.verdict.StartsWith("NOT A VERDICT RUN")), "Variant 0 results are marked not-a-verdict-run");

		// M-DVP PR-D0b, DV-T12a: Gate0Profile.Nodes now delegates to _ProfileMath.Prominence. Compare it bit for bit with the
		// pre-extraction code (OldNodes below, copied verbatim from origin/main @629da0d) on 2,000 random profiles.
		{
			var prng = new Random(20261023); bool same = true; int nodes = 0;
			double[] proms = { 0.0, 0.05, Gate0Engine.KdeProminence, 0.12, 0.3 };
			for (int c = 0; c < 2000 && same; c++)
			{
				var pr = new Gate0Profile(0.25); int bars = 1 + prng.Next(40); double ppx = 4000 + prng.Next(200) * 0.25;
				for (int b = 0; b < bars; b++) { ppx += (prng.Next(9) - 4) * 0.25; double lo = ppx - prng.Next(12) * 0.25, hi = ppx + prng.Next(12) * 0.25; pr.Add(hi, lo, prng.Next(4) == 0 ? 0 : 1 + prng.Next(5000)); }
				double bw = c % 7 == 0 ? 0 : 0.5 + prng.NextDouble() * 8, prom = proms[c % proms.Length];
				List<double[]> h1, l1, h2, l2; pr.Nodes(bw, prom, out h1, out l1); OldNodes(pr, bw, prom, out h2, out l2);
				same = SameNodes(h1, h2) && SameNodes(l1, l2); nodes += h1.Count + l1.Count;
			}
			Check(same && nodes > 1000, "DV-T12a Gate0Profile.Nodes bit-identical to the pre-extraction code on 2000 random profiles (" + nodes + " nodes)");
		}

		Console.WriteLine($"Gate0Tests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}

	static bool SameNodes(List<double[]> a, List<double[]> b)
	{
		if (a.Count != b.Count) return false;
		for (int i = 0; i < a.Count; i++)
			if (a[i].Length != 2 || BitConverter.DoubleToInt64Bits(a[i][0]) != BitConverter.DoubleToInt64Bits(b[i][0]) || BitConverter.DoubleToInt64Bits(a[i][1]) != BitConverter.DoubleToInt64Bits(b[i][1])) return false;
		return true;
	}

	/// <summary>Gate0Profile.Nodes as it was on origin/main @629da0d, before PR-D0b (verbatim; reference for DV-T12a).</summary>
	static void OldNodes(Gate0Profile pr, double bwTicks, double prom, out List<double[]> hvn, out List<double[]> lvn)
	{
		var rows = pr.rows; double tick = pr.tick;
		hvn = new List<double[]>(); lvn = new List<double[]>(); if (pr.Empty || !(bwTicks > 0)) return;
		long first = rows.Keys.First(), last = rows.Keys.Last(); int pad = (int)Math.Ceiling(3 * bwTicks); long lo = first - pad; int n = (int)(last + pad - lo + 1);
		var d = new double[n];
		foreach (var kv in rows)
			for (int j = Math.Max(0, (int)(kv.Key - lo) - pad); j <= Math.Min(n - 1, (int)(kv.Key - lo) + pad); j++)
			{ double z = (j + lo - kv.Key) / bwTicks; d[j] += kv.Value * Math.Exp(-0.5 * z * z); }
		double max = 0; foreach (var x in d) if (x > max) max = x; if (!(max > 0)) return;
		var peaks = new List<int>();
		for (int i = 0; i < n; i++)
		{
			if (!((i == 0 || d[i] > d[i - 1]) && (i == n - 1 || d[i] >= d[i + 1]))) continue;
			double lb = d[i], rb = d[i]; int k;
			for (k = i - 1; k >= 0 && d[k] <= d[i]; k--) if (d[k] < lb) lb = d[k]; if (k < 0) lb = Math.Min(lb, 0);
			for (k = i + 1; k < n && d[k] <= d[i]; k++) if (d[k] < rb) rb = d[k]; if (k >= n) rb = Math.Min(rb, 0);
			double p = (d[i] - Math.Max(lb, rb)) / max;
			if (p >= prom) { peaks.Add(i); hvn.Add(new[] { (i + lo) * tick, p }); }
		}
		for (int q = 1; q < peaks.Count; q++)
		{
			int a = peaks[q - 1], b = peaks[q], m = a; for (int i = a; i <= b; i++) if (d[i] < d[m]) m = i;
			double p = (Math.Min(d[a], d[b]) - d[m]) / max;
			if (p >= prom) lvn.Add(new[] { (m + lo) * tick, p });
		}
	}

	// ---- PR-D8 KD-02 canary: decode, configuration, laziness, the signal rule and a simulated run ----
	static void Kd02Tests()
	{
		int ts; double k;
		Check(Gate0Engine.DecodeKd02(8, out ts, out k) && ts == 19 && k == 3.0, "KD02 Variant 8 = default cell (19 bars, 3 sigma)");
		var cells = new[] { 130, 131, 132, 133 }.Select(v => { Gate0Engine.DecodeKd02(v, out ts, out k); return ts + "/" + k.ToString(System.Globalization.CultureInfo.InvariantCulture); }).ToArray();
		Check(string.Join(" ", cells) == "19/3 40/3 19/2.5 40/2.5", "KD02 sweep cells 130..133 = {19,40} x {3.0,2.5}: " + string.Join(" ", cells));
		Check(!Gate0Engine.DecodeKd02(7, out ts, out k) && !Gate0Engine.DecodeKd02(129, out ts, out k) && !Gate0Engine.DecodeKd02(134, out ts, out k), "KD02 decode rejects 7, 129, 134");
		var ok = new Gate0Engine { mode = EGate0Mode.Canary, variant = 8 };
		Check(ok.Configure() == null && ok.Kd02Engine != null && ok.Kd02Ring != null && ok.CanaryName == "KD02[15m stop=3.0s time=19]", "KD02 configures, allocates its DVP engine: " + ok.CanaryName);
		var c131 = new Gate0Engine { mode = EGate0Mode.Canary, variant = 131 }; c131.Configure();
		Check(c131.CanaryName == "KD02[15m stop=3.0s time=40]", "KD02 cell 131 name: " + c131.CanaryName);
		bool lazy = new[] { 0, 4, 5, 7, 108 }.All(v => { var e = new Gate0Engine { mode = EGate0Mode.Canary, variant = v }; e.Configure(); return e.Kd02Engine == null && e.Kd02Ring == null; })
			&& new Func<bool>(() => { var e = new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 3 }; e.Configure(); return e.Kd02Engine == null; })();
		Check(lazy, "KD02 laziness: no DVP engine for the other canaries or the level study");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 8, kd02Minutes = 0 }.Configure() != null
			&& new Gate0Engine { mode = EGate0Mode.Canary, variant = 8, kd02StopPadAtr = -0.1 }.Configure() != null, "KD02 parameters out of range rejected (minutes < 1, pad < 0; stop k comes from the Variant decode)");
		var cfg = new Gate0Engine { kd02StopK = 2.5 }.Kd02Config();
		Check(cfg.enabled && !cfg.journalFits && Array.IndexOf(cfg.sigmaList, 2.5) >= 0 && Array.IndexOf(cfg.sigmaList, 2.0) >= 0 && cfg.sigmaList.SequenceEqual(cfg.sigmaList.OrderBy(x => x))
			&& new DvpConfig().sigmaList.Length + (Array.IndexOf(new DvpConfig().sigmaList, 2.5) < 0 ? 1 : 0) == cfg.sigmaList.Length, "KD02 config: the stop rail k is added to sigmaList (sorted, once)");

		// simulated run: mean-reverting 1m prices, fills simulated from the intent (market entry next bar, stop/target/flatten exits)
		var g = new Gate0Engine { mode = EGate0Mode.Canary, variant = 8 }; Check(g.Configure() == null, "KD02 run configures");
		var r = new Random(17); double p = 4800, anchor = 4800; int mp = 0; double stp = double.NaN, tgt = double.NaN;
		int entries = 0, badGeom = 0, badTime = 0, exits = 0;
		var start = new DateTime(2024, 9, 2); int bars = 0, fedAfterEnd = -1;
		for (int d = 0; d < 140; d++)
		{
			var day = start.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
			if (day >= Gate0Engine.DevEnd && fedAfterEnd < 0) fedAfterEnd = g.Kd02Ring.Count;
			for (var t = day.AddHours(-6); t < day.AddHours(17); t = t.AddMinutes(1))
			{
				if (r.NextDouble() < 0.002) anchor += (r.NextDouble() - 0.5) * 40;
				double o = p, c = Math.Round((p + 0.02 * (anchor - p) + (r.NextDouble() - 0.5) * 3) * 4) / 4; p = c;
				double h = Math.Max(o, c) + 0.25, l = Math.Min(o, c) - 0.25;
				if (mp != 0 && ((mp > 0 && (l <= stp || h >= tgt)) || (mp < 0 && (h >= stp || l <= tgt)) || g.intent.flatten)) { mp = 0; exits++; }
				g.Bar(new Gate0Bar(t, day, o, h, l, c, 100), mp);
				bars++;
				if (mp == 0 && g.intent.entry == EGate0Entry.Market && g.intent.side != 0 && g.intent.tag != null && g.intent.tag.StartsWith("KD02"))
				{
					entries++;
					var tod = t.TimeOfDay;
					if (tod < new TimeSpan(9, 30, 0) || tod >= new TimeSpan(15, 55, 0)) badTime++;
					double sc = g.Kd02Ring[0].c;   // the signal close: the DVP bar just closed
					bool geom = g.intent.side < 0 ? g.intent.stop > sc && g.intent.target < sc : g.intent.stop < sc && g.intent.target > sc;
					if (!geom || Math.Abs(g.intent.stop * 4 - Math.Round(g.intent.stop * 4)) > 1e-9 || Math.Abs(g.intent.target * 4 - Math.Round(g.intent.target * 4)) > 1e-9) badGeom++;
					mp = g.intent.side; stp = g.intent.stop; tgt = g.intent.target;
				}
				else if (mp != 0 && !double.IsNaN(g.intent.target)) tgt = g.intent.target;   // the POC target is re-read each DVP bar
			}
		}
		Check(entries > 0 && g.Kd02Fills > 0 && g.Kd02Fills <= entries && exits > 0, "KD02 simulated run trades (entries " + entries + ", fills " + g.Kd02Fills + ", exits " + exits + ")");
		Check(badTime == 0 && badGeom == 0, "KD02 entries in RTH before 15:55, stop beyond / target toward the close, both on the tick grid (" + badTime + "/" + badGeom + ")");
		Check(g.Kd02PocReached <= g.Kd02Fills && g.Kd02TimeStops >= 0 && (g.Kd02Fills == 0 ? double.IsNaN(g.Kd02PocReachRate) : g.Kd02PocReachRate >= 0 && g.Kd02PocReachRate <= 1),
			"KD02 POC-reach " + g.Kd02PocReached + "/" + g.Kd02Fills + ", time stops " + g.Kd02TimeStops);
		Check(fedAfterEnd >= 0 && g.Kd02Ring.Count == fedAfterEnd, "KD02 holdout: the DVP stream is never fed on/after DevEnd");

		// the signal rule on the run's own engine and ring (any state where the t-1 rails exist)
		var e2 = new Gate0Engine { mode = EGate0Mode.Canary, variant = 8 }; e2.Configure();
		var r2 = new Random(3); double q = 4800; bool tested = false; var st2 = new DateTime(2024, 3, 4);
		for (int d = 0; d < 14 && !tested; d++)
		{
			var day = st2.AddDays(d); if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
			for (var t = day.AddHours(-6); t < day.AddHours(17) && !tested; t = t.AddMinutes(1))
			{
				double o = q, c = Math.Round((q + 0.02 * (4800 - q) + (r2.NextDouble() - 0.5) * 3) * 4) / 4; q = c;
				e2.Bar(new Gate0Bar(t, day, o, Math.Max(o, c) + 0.25, Math.Min(o, c) - 0.25, c, 100));
				var de = e2.Kd02Engine; var ring = e2.Kd02Ring;
				DvpSnapshot sn = de.Snapshot(1, ring, 1); DvpRails r1 = sn != null ? de.RailsOf(1, ring, 1) : null, r0 = de.RailsOf(1, ring, 0);
				if (r1 == null || r0 == null || !(sn.atr > 0)) continue;
				double up = r1.RailPrice(ELevelKind.DvpSigma, 2), dn = r1.RailPrice(ELevelKind.DvpSigma, -2), poc = r0.RailPrice(ELevelKind.DvpPoc, 0);
				if (double.IsNaN(up) || double.IsNaN(dn) || double.IsNaN(poc) || !(poc < up && poc > dn)) continue;
				int side; double s1, t1;
				bool hi = Gate0Engine.Kd02Signal(de, ring, up + 0.25, 3.0, 0.1, out side, out s1, out t1);
				bool hiOk = hi && side == -1 && t1 == poc && s1 == r1.RailPrice(ELevelKind.DvpSigma, 3.0) + 0.1 * sn.atr;
				bool lw = Gate0Engine.Kd02Signal(de, ring, dn - 0.25, 3.0, 0.1, out side, out s1, out t1);
				bool loOk = lw && side == 1 && t1 == poc && s1 == r1.RailPrice(ELevelKind.DvpSigma, -3.0) - 0.1 * sn.atr;
				bool mid = !Gate0Engine.Kd02Signal(de, ring, (up + dn) / 2, 3.0, 0.1, out side, out s1, out t1) && side == 0 && double.IsNaN(s1) && double.IsNaN(t1);
				bool at = !Gate0Engine.Kd02Signal(de, ring, up, 3.0, 0.1, out side, out s1, out t1);   // strictly beyond the rail
				Check(hiOk && loOk && mid && at, "KD02 rule: short above +2s (stop +3s + 0.1 ATR, target POC of t), long below -2s (mirror), none inside or on the rail");
				tested = true;
			}
		}
		Check(tested, "KD02 rule test found a bar with t-1 rails");
	}
}
