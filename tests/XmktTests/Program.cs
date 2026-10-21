using System;
using System.Collections.Generic;
using System.Linq;
using PowerLanguage.Function;

// Off-MC tests for _XmktLeg (spec missing-functionality-specs.md rev 4 §6).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }
	static bool Near(double a, double b, double eps = 1e-9) { return Math.Abs(a - b) < eps; }
	static readonly DateTime T0 = new DateTime(2024, 3, 13, 9, 30, 0);

	static _XmktLeg Leg(Action<XmktLegConfig> f = null, List<string> j = null, int staleSec = 120)
	{
		var c = new XmktLegConfig { role = "NQ", dataStream = 2, corrLength = 20, rsLength = 20, returnBars = 3 }; if (f != null) f(c);
		var l = new _XmktLeg { config = c, maxStale = TimeSpan.FromSeconds(staleSec) }; if (j != null) l.journal = s => j.Add(s);
		string e = l.Configure(); if (e != null) throw new Exception(e);
		return l;
	}

	static void Main()
	{
		var bad = new XmktLegConfig { dataStream = 1, corrLength = 2, returnBars = 0 }.Validate();
		Check(bad != null && bad.Contains("dataStream=1") && bad.Contains("corrLength=2") && bad.Contains("returnBars=0"), "Validate names every bad value");

		// leg = Data1^2 in log space -> leg returns = 2 x Data1 returns: correlation 1, beta 0.5 (Data1 on leg)
		var r = new Random(9); var j = new List<string>(); var leg = Leg(null, j);
		Check(!leg.Available && double.IsNaN(leg.Correlation), "not available before data");
		double p = 100; var d1 = new List<double>(); var lg = new List<double>();
		for (int i = 0; i < 40; i++)
		{
			p *= Math.Exp((r.NextDouble() - 0.5) * 0.01); d1.Add(p); lg.Add(p * p / 100);
			leg.Step(T0.AddMinutes(i), p, i, T0.AddMinutes(i), p * p / 100);
		}
		Check(leg.Available && Near(leg.Correlation, 1, 1e-9) && Near(leg.Beta, 0.5, 1e-9), $"correlation / beta from paired leg returns ({leg.Correlation:0.######}, {leg.Beta:0.######})");
		Check(Near(leg.LogReturn, Math.Log(lg[39] / lg[36])) && Near(leg.Data1LogReturn, Math.Log(d1[39] / d1[36])), "k-bar log returns on the leg's own sampled bars");
		var rs = Enumerable.Range(20, 20).Select(i => Math.Log(lg[i] / d1[i])).ToArray(); double m = rs.Average(), sd = Math.Sqrt(rs.Select(x => (x - m) * (x - m)).Average());
		Check(Near(leg.RsZ, (rs[19] - m) / sd, 1e-6), "RS spread z over rsLength samples");
		Check(j.Count(s => s.Contains("stale")) == 1, "initial 'stale (no bar)' fact logged once");

		// leg stops updating: stale after maxStale, journaled; recovers when a new bar arrives
		leg.Step(T0.AddMinutes(41), p, 39, T0.AddMinutes(39), lg[39]);
		Check(leg.Available, "within maxStale (2 min) still available");
		leg.Step(T0.AddMinutes(42), p, 39, T0.AddMinutes(39), lg[39]);
		Check(!leg.Available && double.IsNaN(leg.Correlation) && j.Last().Contains("stale at 2024-03-13 10:12:00"), "stale leg unavailable, Journal fact with bar time");
		leg.Step(T0.AddMinutes(43), p, 43, T0.AddMinutes(43), lg[39]);
		Check(leg.Available && j.Last().Contains("available again"), "fresh bar -> available again (journaled)");

		// same leg bar seen again: no extra sample
		var l2 = Leg(c => c.corrLength = 3);
		for (int i = 0; i < 3; i++) l2.Step(T0.AddMinutes(i), 100 + i, i, T0.AddMinutes(i), 200 + 3 * i);
		double before = l2.Correlation;
		l2.Step(T0.AddMinutes(3), 150, 2, T0.AddMinutes(2), 999);
		Check(double.IsNaN(before) && double.IsNaN(l2.Correlation), "leg bar not advanced -> no new sample (no interpolation)");

		var l3 = Leg(); for (int i = 0; i < 30; i++) l3.Step(T0.AddMinutes(i), 100 + i, i, T0.AddMinutes(i), 200 + i);
		l3.OnRoll();
		Check(double.IsNaN(l3.Correlation) && double.IsNaN(l3.LogReturn) && double.IsNaN(l3.RsZ), "OnRoll drops return history");
		var l4 = Leg(null, null, 0); l4.Step(T0, 1, 0, T0.AddDays(-5), 1); l4.Step(T0.AddDays(1), 1, 1, T0.AddDays(-5), 1);
		Check(l4.Available, "maxStale 0 disables the staleness check");

		Console.WriteLine($"XmktTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
