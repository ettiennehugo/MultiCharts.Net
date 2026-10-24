using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using PowerLanguage.Function;

// Off-MC tests for InstrProbeCore (M-INSTR PR-I0, specs/instrument-profile-spec.md rev 3 §12, IP-11.2).
public enum FakeCategory { Stock = 0, Future = 2, Forex = 5 }
public class FakeResolution { public string Type { get { return "Minute"; } } public int Size { get { return 1; } } public override string ToString() { return "1 Minute"; } }
public interface IFakeExtra { string FromInterface { get; } }
public class FakeInfo : IFakeExtra
{
	public string Name { get { return "@ES"; } }
	public FakeCategory Category { get { return FakeCategory.Future; } }
	public string Exchange { get { return "CME"; } }
	public double MinMove { get { return 25; } }
	public double PriceScale { get { return 100; } }
	public double BigPointValue { get { return 50; } }
	public DateTime Expiration { get { return new DateTime(2026, 12, 18); } }
	public string Description { get { return "E-mini\nS&P 500"; } }
	public FakeResolution Resolution { get { return new FakeResolution(); } }
	public string Throws { get { throw new InvalidOperationException("not available"); } }
	public int this[int i] { get { return i; } }
	public List<int> Sessions { get { return Enumerable.Range(0, 30).ToList(); } }
	public FakeInfo Self { get { return this; } }
	string IFakeExtra.FromInterface { get { return "x"; } }
}

static class Program
{
	static int s_fail, s_pass;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	static void Main()
	{
		Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); // invariant output even under a comma-decimal culture

		Check(InstrProbeCore.FormatValue(0.25) == "0.25" && InstrProbeCore.FormatValue(0.1 + 0.2) == "0.30000000000000004", "doubles invariant, round-trip");
		Check(InstrProbeCore.FormatValue(FakeCategory.Forex) == "Forex(=5)", "enum name and int");
		Check(InstrProbeCore.FormatValue(null) == "<null>" && InstrProbeCore.FormatValue("a\nb") == "\"a\\nb\"", "null and one-line string");
		Check(InstrProbeCore.FormatValue(new DateTime(2026, 12, 18, 13, 5, 0)) == "2026-12-18 13:05:00 kind=Unspecified", "DateTime format");
		Check(InstrProbeCore.FormatValue(default(DateTime)).StartsWith("0001-01-01 00:00:00"), "default DateTime visible (continuous series test, IP-2.6)");

		Check(string.Join(",", InstrProbeCore.EnumMembers(typeof(FakeCategory))) == "Stock=0,Future=2,Forex=5", "enum members with values (IP-1.1)");

		string fx = InstrProbeCore.FixtureLine("@ES", "Future", "CME", 25, 100, 50, new DateTime(2026, 12, 18), "Exchange", "E-mini|S&P\n500");
		Check(fx == "@ES|Future|CME|25|100|50|2026-12-18 00:00:00|Exchange|E-mini/S&P\\n500", "IP-11.2 fixture line: " + fx);
		Check(fx.Split('|').Length == 9, "fixture has exactly 9 fields");

		Check(InstrProbeCore.SafeFileName("@ES") == "_ES" && InstrProbeCore.SafeFileName("EUR/USD") == "EUR_USD" && InstrProbeCore.SafeFileName("ESZ26:XCME") == "ESZ26_XCME" && InstrProbeCore.SafeFileName(null) == "unnamed", "safe file names");

		var props = InstrProbeCore.Properties(typeof(FakeInfo)).Select(p => p.Name).ToList();
		Check(!props.Contains("Item"), "indexers skipped");
		Check(props.SequenceEqual(props.OrderBy(x => x, StringComparer.Ordinal)), "properties sorted");
		Check(props.Contains("FromInterface"), "explicit interface properties found via interfaces");

		var lines = new List<string>();
		InstrProbeCore.Dump(new FakeInfo(), "Info", 2, lines);
		Func<string, bool> has = s => lines.Any(l => l == s);
		Check(has("Info : runtimeType = FakeInfo"), "runtime type line");
		Check(has("Info.Category : FakeCategory = Future(=2)"), "enum property");
		Check(has("Info.MinMove : Double = 25") && has("Info.BigPointValue : Double = 50"), "double properties");
		Check(has("Info.Expiration : DateTime = 2026-12-18 00:00:00 kind=Unspecified"), "DateTime property");
		Check(has("Info.Description : String = \"E-mini\\nS&P 500\""), "string kept on one line");
		Check(has("Info.Throws : String = <InvalidOperationException: not available>"), "throwing getter reported, dump continues");
		Check(has("Info.Resolution.Type : String = \"Minute\"") && has("Info.Resolution.Size : Int32 = 1"), "nested object recursed");
		Check(has("Info.Self = <cycle>"), "self reference stops");
		Check(has("Info.Sessions[...] = <truncated at 20>") && has("Info.Sessions[19] = 19") && !has("Info.Sessions[20] = 20"), "enumerable capped at MAX_ITEMS");
		Check(lines.All(l => l.IndexOf('\n') < 0), "every dump line single-line");

		var l0 = new List<string>(); InstrProbeCore.Dump(new FakeInfo(), "Info", 0, l0);
		Check(l0.Count == 2 && l0[1] == "Info = \"FakeInfo\"", "depth 0 prints type and ToString only");

		var found = InstrProbeCore.FindTypes(new[] { typeof(Program).Assembly }, new[] { "fakeinfo", "RESOLUTION" });
		Check(found.Select(t => t.Name).SequenceEqual(new[] { "FakeInfo", "FakeResolution" }), "FindTypes case-insensitive, sorted, public only");

		var mem = InstrProbeCore.Members(typeof(FakeInfo));
		Check(mem.Contains("prop Double BigPointValue") && mem.Contains("prop List<Int32> Sessions") && !mem.Any(m => m.Contains("get_")), "Members: properties, generic names, no accessors");
		Check(InstrProbeCore.Members(typeof(FakeCategory)).SequenceEqual(new[] { "enum Stock=0", "enum Future=2", "enum Forex=5" }), "Members of an enum");

		Console.WriteLine(s_fail == 0 ? $"InstrProbeTests: {s_pass} passed, 0 failed" : $"InstrProbeTests: {s_fail} FAILED, {s_pass} passed");
		Environment.Exit(s_fail == 0 ? 0 : 1);
	}
}
