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

		// ---- buckets ----
		Check(Gate0Bucket.EndOf(new DateTime(2024, 3, 11, 9, 31, 0), 5) == new DateTime(2024, 3, 11, 9, 35, 0), "5m bucket end");
		Check(Gate0Bucket.EndOf(new DateTime(2024, 3, 11, 9, 35, 0), 5) == new DateTime(2024, 3, 11, 9, 35, 0), "end-stamped bar on a boundary stays in its bucket");

		// ---- verdicts (catalogue 2.3) ----
		Check(Gate0Engine.Verdict(29, 0.2, 0.1).StartsWith("PARK (no evaluation"), "n<30 no evaluation");
		Check(Gate0Engine.Verdict(100, 0.2, 0.1).StartsWith("PARK (inconclusive"), "30<=n<150 inconclusive");
		Check(Gate0Engine.Verdict(200, 0.0, 0.1).Contains("kill: adjusted"), "adjusted <= 0 kills");
		Check(Gate0Engine.Verdict(200, 0.1, -0.01).Contains("kill: CI"), "CI including 0 kills");
		Check(Gate0Engine.Verdict(200, 0.1, 0.02) == "PASS", "pass");

		// ---- scoring + block bootstrap ----
		Check(Gate0Engine.BootstrapDraws == 500 && Gate0Engine.BootstrapBlock == 5, "500 draws, block size 5");
		var d0 = new DateTime(2023, 1, 2); var pairs = new List<KeyValuePair<LevelEvent, LevelEvent>>();
		for (int d = 0; d < 200; d++)
		{
			var day = d0.AddDays(d);
			pairs.Add(new KeyValuePair<LevelEvent, LevelEvent>(Ev(day, true, d % 4 != 0 ? 1 : -1), Ev(day, true, d % 2 == 0 ? 1 : -1)));
		}
		var r1 = Gate0Engine.Score("T", pairs, 33); var r2 = Gate0Engine.Score("T", pairs, 33); var r3 = Gate0Engine.Score("T", pairs, 34);
		Check(r1.touches == 200 && r1.holds == 150 && r1.placeboHolds == 100, "touch and hold counts");
		Check(Near(r1.adjusted, 0.75 - 0.5), "placebo-adjusted hold rate = level rate - placebo rate");
		Check(r1.ciLo == r2.ciLo && r1.ciHi == r2.ciHi, "bootstrap reproducible for the same seed");
		Check(!double.IsNaN(r3.ciLo) && r1.ciLo <= r1.adjusted && r1.ciHi >= r1.adjusted, "CI brackets the estimate");
		Check(r1.verdict == "PASS", "clear lift passes (" + r1.verdict + ")");
		var flat = pairs.Select(kv => new KeyValuePair<LevelEvent, LevelEvent>(kv.Value, kv.Value)).ToList();
		Check(Gate0Engine.Score("T", flat, 33).verdict.Contains("kill"), "no lift over placebo is killed");
		Check(!Gate0Engine.Hold(null) && !Gate0Engine.Hold(Ev(d0, false, 1)) && Gate0Engine.Hold(Ev(d0, true, 0.5)), "hold = touched with positive asymmetry");

		// ---- roll window (+-1 session) ----
		var g = new Gate0Engine();
		for (int d = 0; d < 6; d++) g.sessionDays.Add(d0.AddDays(d));
		g.rollDays.Add(d0.AddDays(3));
		Check(!g.InRollWindow(d0.AddDays(1)) && g.InRollWindow(d0.AddDays(2)) && g.InRollWindow(d0.AddDays(3)) && g.InRollWindow(d0.AddDays(4)) && !g.InRollWindow(d0.AddDays(5)), "roll session +-1 excluded");

		// ---- configuration ----
		Check(Gate0Engine.DevEnd == new DateTime(2025, 1, 1), "development window ends 2025-01-01");
		Check(new Gate0Engine { mode = EGate0Mode.Canary, variant = 8 }.Configure() != null, "canary variant out of range rejected");
		Check(new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 11 }.Configure() != null, "level variant out of range rejected");
		Check(new Gate0Engine { mode = EGate0Mode.EventStudy, variant = 0 }.Configure() == null, "valid configuration accepted");

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

		Console.WriteLine($"Gate0Tests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
