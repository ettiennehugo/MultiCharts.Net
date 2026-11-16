using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// Naming and header checks. The semantic C# 7.3 compile is Generated/SandboxLib.csproj,
// built before this program starts (see SandboxCompile.csproj). It compiles _CustomSignal,
// the volume-profile bases, the engines, and every sandbox strategy against McStubs.
// A type error fails that build, so this process only runs after the semantic compile succeeds.
static class Program
{
	static int s_pass, s_fail;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	static readonly string[] Header =
	{
		"// ## 1. Identity", "// ## 2. Provenance", "// ## 3. Hypothesis", "// ## 4. Trade walkthrough",
		"// ## 5. Timing", "// ## 6. Position and re-entry", "// ## 7. Instruments", "// ## 8. Data",
		"// ## 9. Entry and filters", "// ## 10. Position sizing and risk", "// ## 11. Orders, execution, and costs",
		"// ## 12. Trade management", "// ## 13. Exits", "// ## 14. Regimes", "// ## 15. Dependencies",
		"// ## 16. Pitfalls", "// ## 17. Parameters", "// ## 18. Acceptance policy", "// ## 19. Validation",
		"// ## 20. Edge monitoring", "// ## 21. Log diagnostics", "// ## 22. Notes",
	};

	static readonly HashSet<string> OriginalTen = new HashSet<string>(StringComparer.Ordinal)
	{
		"_A2_49_MiddayBreakout.Strategy.CS", "_A2_56_FirstHourBreak.Strategy.CS", "_A2_69_KaufmanThreeDay.Strategy.CS",
		"_A2_70_OpenFadeHvGate.Strategy.CS", "_AV_08_PeriodAvwapLayer.Strategy.CS", "_DV_15_EmaVwapPullback.Strategy.CS",
		"_SW_02_SwingPullback.Strategy.CS", "_TS_01_EuropeanOpenDrift.Strategy.CS", "_TS_03_CloseDrift.Strategy.CS",
		"_VW_05_LateDayVwap.Strategy.CS",
	};

	static int Main()
	{
		string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
		string sandbox = Path.Combine(root, "sandbox");
		var files = new List<string>(Directory.GetFiles(sandbox, "_*.Strategy.CS"));
		files.Sort(StringComparer.Ordinal);
		Check(files.Count >= 30, "sandbox strategies found: " + files.Count);
		var nameRx = new Regex(@"^_[A-Z][A-Z0-9]*_[0-9]{2}_[A-Z][A-Za-z0-9]*\.Strategy\.CS$", RegexOptions.CultureInvariant);
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string text = File.ReadAllText(path);
			Check(nameRx.IsMatch(name), "file name " + name);
			string stem = name.Substring(0, name.Length - ".Strategy.CS".Length);
			string code = Regex.Replace(text, @"//[^\n]*", "");
			var classes = new List<string>();
			foreach (Match m in Regex.Matches(code, @"\bpublic\s+class\s+(_\w+)")) classes.Add(m.Groups[1].Value);
			Check(classes.Count == 1 && classes[0] == stem, name + " class " + string.Join(",", classes.ToArray()));
			int at = -1; bool order = true;
			foreach (string h in Header)
			{
				int i = text.IndexOf(h, StringComparison.Ordinal);
				if (i < 0 || i <= at) { order = false; break; }
				at = i;
			}
			Check(order, name + " has the 22 header sections in order");
			if (!OriginalTen.Contains(name))
				Check(text.IndexOf("IOGMode.Disabled", StringComparison.Ordinal) >= 0, name + " sets IOGMode.Disabled");
		}
		var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
		int dup = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			if (OriginalTen.Contains(name)) continue;
			string key = CalcBarKey(File.ReadAllText(path));
			if (key.Length < 40) continue;
			string prior;
			if (bodies.TryGetValue(key, out prior))
			{
				dup++;
				Console.WriteLine("FAIL duplicate CalcBar " + name + " == " + prior);
			}
			else bodies[key] = name;
		}
		Check(dup == 0, "CalcBar bodies are unique apart from names, string literals and numeric literals (" + dup + " duplicates)");
		ConfigChecks(files);

		Console.WriteLine("SandboxCompile: semantic compile succeeded; " + s_pass + " checks passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}

	// Studies whose session-profile reads are left on MC's Volume Profile on purpose (header says so, or the source is mixed).
	static readonly HashSet<string> McProfileByDesign = new HashSet<string>(StringComparer.Ordinal)
	{
		"_A2_45_SinglePrintShelf.Strategy.CS", "_SW_02_SwingPullback.Strategy.CS",
	};

	// Batch-1 defect classes: a feature is queried but Create never turns its source on, so the query returns null and the
	// study never trades. The default-off hostGate arms are excluded (see the batch-1 PR). The config may sit in a sandbox
	// parent class (A2-15 extends PS-01, MT-03 extends PS-08), so the parent's text is included.
	static void ConfigChecks(List<string> files)
	{
		var text = new Dictionary<string, string>(StringComparer.Ordinal);
		var parent = new Dictionary<string, string>(StringComparer.Ordinal);
		var byClass = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			text[name] = code;
			Match m = Regex.Match(code, @"\bpublic\s+class\s+(_\w+)\s*:\s*(_\w+)");
			if (m.Success) { byClass[m.Groups[1].Value] = name; parent[name] = m.Groups[2].Value; }
		}
		// The Own RTH source only reaches Composite / IsBalanced / BalanceRange through the migrationProfiles hook (the futures
		// base override). The base default is the unchanged MC read, so production studies are not affected.
		string vpBase = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(files[0])), "_VolumeProfileBase.Strategy.CS"));
		foreach (string api in new[] { "public VpExtra Composite(int stream, int n)", "public bool IsBalanced(int stream, int n", "public bool BalanceRange(int stream, int n" })
		{
			int at = vpBase.IndexOf(api, StringComparison.Ordinal);
			int next = at < 0 ? -1 : vpBase.IndexOf("public ", at + api.Length, StringComparison.Ordinal);
			string body = at < 0 ? "" : vpBase.Substring(at, (next < 0 ? vpBase.Length : next) - at);
			Check(body.Contains("migrationProfiles(stream, n)") && !body.Contains("RecentCompletedProfiles("), api + " reads completed profiles through migrationProfiles");
		}
		var ownRx = new Regex(@"DevelopingSource\s*=\s*EDevelopingSource\.Own");
		var rthRx = new Regex(@"OwnProfileWindow\s*=\s*EOwnProfileWindow\.Rth");
		var rangeOnRx = new Regex(@"(RangeProfiles|ProfileStats)\.enabled\s*=\s*true");
		var priorRx = new Regex(@"\b(PriorRthProfile|RthProfile)\s*\(\s*\)");
		var compRx = new Regex(@"\b(Composite|IsBalanced|BalanceRange|ValueMigrationScore|ValueMigrationState)\s*\(\s*1\b");
		var rangeRx = new Regex(@"\b(RangeProfile|RangeProfileAfter|RangeProfileBars|OpenRange|SessionBars)\s*\(");
		foreach (string name in text.Keys)
		{
			string own = WithoutMethod(text[name], "hostGate");
			string chain = text[name];
			string p;
			string cur = name;
			int guard = 0;
			while (parent.TryGetValue(cur, out p) && byClass.ContainsKey(p) && guard++ < 5) { cur = byClass[p]; chain += text[cur]; }
			bool ownRth = ownRx.IsMatch(chain) && rthRx.IsMatch(chain);
			if (priorRx.IsMatch(own))
				Check(ownRth, name + " reads PriorRthProfile/RthProfile and sets DevelopingSource = Own and OwnProfileWindow = Rth");
			if (compRx.IsMatch(own) && !McProfileByDesign.Contains(name))
				Check(ownRth, name + " reads Composite/IsBalanced/ValueMigrationScore on Data1 and sets the Own RTH profile source");
			if (rangeRx.IsMatch(own))
				Check(rangeOnRx.IsMatch(chain), name + " queries a range profile and enables RangeProfiles (or ProfileStats)");
		}
	}

	// Removes the body of every method with this name (brace-matched), so default-off gate arms do not count as reads.
	static string WithoutMethod(string code, string method)
	{
		var rx = new Regex(@"\b" + method + @"\s*\([^)]*\)\s*\{");
		Match m;
		while ((m = rx.Match(code)).Success)
		{
			int depth = 0, end = code.Length - 1;
			for (int j = m.Index + m.Length - 1; j < code.Length; j++)
			{
				if (code[j] == '{') depth++;
				else if (code[j] == '}' && --depth == 0) { end = j; break; }
			}
			code = code.Substring(0, m.Index) + code.Substring(end + 1);
		}
		return code;
	}

	// Comments and quoted names are removed so two placeholders that differ only by an order name still match.
	static string CalcBarKey(string text)
	{
		int i = text.IndexOf("void CalcBar(", StringComparison.Ordinal);
		if (i < 0) return "";
		int brace = text.IndexOf('{', i);
		if (brace < 0) return "";
		int depth = 0;
		int end = brace;
		for (int j = brace; j < text.Length; j++)
		{
			if (text[j] == '{') depth++;
			else if (text[j] == '}')
			{
				depth--;
				if (depth == 0) { end = j; break; }
			}
		}
		string body = text.Substring(brace, end - brace + 1);
		body = Regex.Replace(body, @"//[^\n]*", "");
		body = Regex.Replace(body, "\"[^\"]*\"", "\"\"");
		body = Regex.Replace(body, @"\d+(?:\.\d+)?", "");
		body = Regex.Replace(body, @"\s+", "");
		return body;
	}
}
