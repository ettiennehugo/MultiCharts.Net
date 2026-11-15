using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

// Sandbox compile gate. GxMcCompile is not in this repo (_GenericExit is a spec, not a file).
// This program parses every sandbox strategy as C# 7.3 via csc -langversion:7.3 and fails on a
// syntax or language-version error. Missing MultiCharts types are expected: the reference
// assemblies are not on this machine. It also checks the file stem, the 22 header titles, and
// IOGMode.Disabled on every strategy that is not one of the ten PR 124 files.
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

	// csc errors that mean the file is not valid C# 7.3. Missing MC types are not in this set.
	static readonly Regex SyntaxError = new Regex(@"error CS(1001|1002|1003|1022|1026|1031|1513|1514|1519|1525|1644|8059)\b", RegexOptions.CultureInvariant);

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

		string csc = FindCsc();
		Check(csc != null, "csc.dll found for the C# 7.3 parse");
		if (csc != null)
		{
			int syntax = 0;
			foreach (string path in files)
			{
				var psi = new ProcessStartInfo("dotnet", "exec \"" + csc + "\" -nologo -langversion:7.3 -t:library \"" + path + "\"")
				{ RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
				using (var p = Process.Start(psi))
				{
					string both = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
					p.WaitForExit();
					if (SyntaxError.IsMatch(both))
					{
						syntax++;
						Console.WriteLine("SYNTAX " + Path.GetFileName(path));
						Console.WriteLine(both);
					}
				}
			}
			Check(syntax == 0, "C# 7.3 syntax errors: " + syntax + " (missing MultiCharts types are expected and are not counted)");
		}
		Console.WriteLine("SandboxCompile: " + s_pass + " passed, " + s_fail + " failed");
		return s_fail == 0 ? 0 : 1;
	}

	static string FindCsc()
	{
		string dotnet = Environment.GetEnvironmentVariable("DOTNET_ROOT");
		var roots = new List<string>();
		if (!string.IsNullOrEmpty(dotnet)) roots.Add(dotnet);
		roots.Add("/usr/lib/dotnet");
		roots.Add("/usr/share/dotnet");
		foreach (string r in roots)
		{
			string sdk = Path.Combine(r, "sdk");
			if (!Directory.Exists(sdk)) continue;
			foreach (string ver in Directory.GetDirectories(sdk))
			{
				string csc = Path.Combine(ver, "Roslyn", "bincore", "csc.dll");
				if (File.Exists(csc)) return csc;
			}
		}
		return null;
	}
}
