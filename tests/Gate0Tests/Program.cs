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
		Check(_ProfileStats.ProfileMixture(uni.ToArray(), uw.ToArray(), 4, 7UL, EGmmNeff.Bars, 200, 0.01).K == 1, "GMM BIC picks K=1 on one Gaussian (_Gmm core)");

		// ---- buckets ----
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

		// ---- roll window (+-1 session) ----
		var g = new Gate0Engine();
		for (int d = 0; d < 6; d++) g.sessionDays.Add(d0.AddDays(d));
		g.rollDays.Add(d0.AddDays(3));
		Check(!g.InRollWindow(d0.AddDays(1)) && g.InRollWindow(d0.AddDays(2)) && g.InRollWindow(d0.AddDays(3)) && g.InRollWindow(d0.AddDays(4)) && !g.InRollWindow(d0.AddDays(5)), "roll session +-1 excluded");

		// ---- configuration ----
		Check(Gate0Engine.DevEnd == new DateTime(2025, 1, 1), "development window ends 2025-01-01");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 8 }.Configure() != null, "canary variant out of range rejected");
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

		// ---- every canary (0..7 and a sweep cell) runs over bars without throwing ----
		foreach (int cvn in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 100, 111, 123 })
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

		Console.WriteLine($"Gate0Tests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
