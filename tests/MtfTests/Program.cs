using System;
using PowerLanguage.Function;

// Off-MC tests for _MtfMath (spec missing-functionality-specs.md rev 4 §3).
static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	static void Main()
	{
		Check(_MtfMath.ClosedIndex(true, 0) == 0 && _MtfMath.ClosedIndex(false, 0) == 1 && _MtfMath.ClosedIndex(false, 3) == 4, "ClosedIndex: forming bar 0 is skipped");
		// HTF 60-min bars end-stamped 09:00, 10:00, 11:00 (index 0 = 11:00 forming)
		var t0 = new DateTime(2024, 3, 13, 11, 0, 0);
		Func<int, DateTime> times = i => t0.AddHours(-i);
		Check(_MtfMath.MapIndex(times, _MtfMath.ClosedIndex(false, 0), new DateTime(2024, 3, 13, 10, 7, 0), 3) == 1, "10:07 Data1 bar maps to the 10:00 closed HTF bar");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 10, 0, 0), 3) == 1, "HTF bar stamped after Data1 time is skipped");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 11, 0, 0), 3) == 0, "same-timestamp closed HTF bar is usable (end stamps)");
		Check(_MtfMath.MapIndex(times, 0, new DateTime(2024, 3, 13, 7, 0, 0), 2) == -1, "no bar within maxBack -> -1");
		Check(_MtfMath.SymbolRoot("@ES") == "ES" && _MtfMath.SymbolRoot("@es#C") == "ES" && _MtfMath.SymbolRoot("ESZ24") == "ES" && _MtfMath.SymbolRoot("ESZ4") == "ES", "SymbolRoot continuous / outright");
		Check(_MtfMath.SymbolRoot("@NQ") != _MtfMath.SymbolRoot("@ES") && _MtfMath.SymbolRoot("SPY") == "SPY" && _MtfMath.SymbolRoot("") == "", "SymbolRoot distinguishes markets, equities unchanged");
		Check(_MtfMath.SymbolRoot("@MES") == "MES" && _MtfMath.SymbolRoot("MESH25") == "MES", "micro root kept");
		Check(_MtfMath.IsStale(t0.AddMinutes(-3), t0, TimeSpan.FromMinutes(2)) && !_MtfMath.IsStale(t0.AddMinutes(-2), t0, TimeSpan.FromMinutes(2)) && !_MtfMath.IsStale(t0.AddDays(-1), t0, TimeSpan.Zero), "IsStale; maxStale 0 disables");
		Console.WriteLine($"MtfTests: {s_pass} passed, {s_fail} failed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
