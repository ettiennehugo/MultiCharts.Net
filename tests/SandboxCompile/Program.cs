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
		LookbackChecks(files);
		OwnSourceChecks(files);
		BookChecks(files);
		AtrRollChecks(files);

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

	// Analyst MC17 run (A2-15 aborted): the sandbox workspaces use MaxBarsBack 49, so a Data1 series read Bars.X[i] with a
	// computed index (or a literal past 49) aborts the study. Deep Data1 history comes from the engine ring through lbBar().
	// Computed-index reads that stay within 49 are listed with their bound. Owner 2026-11-16: the original ten are checked too.
	const int SandboxMaxBarsBack = 49;
	static readonly Dictionary<string, string> BoundedLookback = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "_A2_58_FastInSlowFade.Strategy.CS", @"\bslow\s*=\s*(\d+)" },        // sma(Cfg.slow) on Data2; Cfg.fast is smaller
		{ "_AV_08_PeriodAvwapLayer.Strategy.CS", @"\bhostLookback\s*=\s*(\d+)" }, // Bars.High/Low[1..Cfg.hostLookback] on Data1
	};
	static void LookbackChecks(List<string> files)
	{
		var d1 = new Regex(@"\bBars\.(Time|Close|Open|High|Low|Volume|Ticks|UpTicks|DownTicks)\[\s*([^\]]+?)\s*\]");
		var dn = new Regex(@"\bBarsOfData\(\s*\d+\s*\)\.(Time|Close|Open|High|Low|Volume|Ticks|UpTicks|DownTicks)\[\s*([^\]]+?)\s*\]");
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			var bad = new List<string>();
			bool d2computed = false;
			bool computed = false;
			foreach (Match m in d1.Matches(code))
			{
				int lit;
				if (int.TryParse(m.Groups[2].Value, out lit)) { if (lit > SandboxMaxBarsBack) bad.Add(m.Value); continue; }
				if (BoundedLookback.ContainsKey(name)) computed = true; else bad.Add(m.Value);
			}
			foreach (Match m in dn.Matches(code))
			{
				int lit;
				if (!int.TryParse(m.Groups[2].Value, out lit)) { d2computed = true; computed = true; }
				else if (lit > SandboxMaxBarsBack) bad.Add(m.Value);
			}
			Check(bad.Count == 0, name + " reads Data1 history only within MaxBarsBack " + SandboxMaxBarsBack + " (deeper reads go through lbBar): " + string.Join(", ", bad.ToArray()));
			if (code.Contains("lbBar("))
				Check(Regex.IsMatch(code, @"requireRing\(\s*1\s*,"), name + " uses lbBar and asks for a Data1 ring in StartCalc");
			if (code.Contains("d2Bar("))
				Check(Regex.IsMatch(code, @"requireRing\(\s*2\s*,"), name + " uses d2Bar and asks for a Data2 ring in StartCalc");
			if (computed)
			{
				string rx;
				Match b = BoundedLookback.TryGetValue(name, out rx) ? Regex.Match(code, rx) : Match.Empty;
				int bound;
				Check(b.Success && int.TryParse(b.Groups[1].Value, out bound) && bound <= SandboxMaxBarsBack, name + " reads history by a computed index with a known bound within MaxBarsBack");
			}
		}
	}

	// Analyst MC17 run (A2-13): DevelopingSource is an [Input], so a value assigned in Create() is replaced by the value saved
	// with the chart's study instance. A sandbox study that needs the Own RTH engine re-applies it in StartCalc.
	static void OwnSourceChecks(List<string> files)
	{
		var ownRx = new Regex(@"DevelopingSource\s*=\s*EDevelopingSource\.Own");
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			if (OriginalTen.Contains(name)) continue;
			string code = WithoutMethod(Regex.Replace(File.ReadAllText(path), @"//[^\n]*", ""), "applyGateNeeds");
			if (!ownRx.IsMatch(MethodBody(code, "void Create()"))) continue;
			string start = MethodBody(code, "void StartCalc()");
			Match own = ownRx.Match(start);
			int b = start.IndexOf("base.StartCalc()", StringComparison.Ordinal);
			Check(own.Success && b > own.Index, name + " sets DevelopingSource = Own in Create and re-applies it in StartCalc before base.StartCalc()");
		}
	}

	// Analyst MC17 run (A2-32 / A2-35 traded once, then never again): sandboxBook ran before the day reset, so after the first
	// fill Rest returned Done on every later bar. Every sandboxBook re-arms m_filledSetup at the trading-day change before Rest.
	static void BookChecks(List<string> files)
	{
		var rearm = new Regex(@"if\s*\(\s*bookDay\s*!=\s*m_bookDay\s*\)\s*\{\s*m_bookDay\s*=\s*bookDay;\s*m_filledSetup\s*=\s*false;");
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			if (OriginalTen.Contains(name)) continue;
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			if (!code.Contains("_SandboxRules.Rest(")) continue;
			string book = MethodBody(code, "bool sandboxBook()");
			Match m = rearm.Match(book);
			int rest = book.IndexOf("_SandboxRules.Rest(", StringComparison.Ordinal);
			Check(m.Success && rest > m.Index, name + " sandboxBook re-arms the one-trade-per-day state at the trading-day change before Rest");
		}
	}

	// Brace-matched body of the first method whose text starts with `signature`, or "" when absent.
	static string MethodBody(string code, string signature)
	{
		int i = code.IndexOf(signature, StringComparison.Ordinal);
		if (i < 0) return "";
		int brace = code.IndexOf('{', i);
		if (brace < 0) return "";
		int depth = 0;
		for (int j = brace; j < code.Length; j++)
		{
			if (code[j] == '{') depth++;
			else if (code[j] == '}' && --depth == 0) return code.Substring(brace, j - brace + 1);
		}
		return code.Substring(brace);
	}

	// Owner 2026-11-16: every sandbox study that reads DailyContext.Atr20d carries the 20-day ATR over the roll
	// (DailyConfig.atrRestartOnRoll = false, PR #46); the default reseed left Atr20d NaN ~20 sessions after each roll.
	// Reads inside the default-off hostGate arms are excluded (PS-01's Compression arm).
	static void AtrRollChecks(List<string> files)
	{
		int readers = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = WithoutMethod(Regex.Replace(File.ReadAllText(path), @"//[^\n]*", ""), "hostGate");
			if (!Regex.IsMatch(code, @"\bAtr20d\b")) continue;
			readers++;
			Check(Regex.IsMatch(code, @"DailyConfig\.atrRestartOnRoll\s*=\s*false\s*;"), name + " reads Atr20d and sets DailyConfig.atrRestartOnRoll = false");
		}
		Check(readers >= 32, "Atr20d readers found: " + readers);
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
