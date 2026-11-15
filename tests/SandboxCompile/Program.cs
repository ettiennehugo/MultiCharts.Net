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
		Check(dup == 0, "CalcBar bodies are unique apart from names and string literals (" + dup + " duplicates)");

		Console.WriteLine("SandboxCompile: semantic compile succeeded; " + s_pass + " checks passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
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
		body = Regex.Replace(body, @"\s+", "");
		return body;
	}
}
