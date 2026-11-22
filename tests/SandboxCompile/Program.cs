using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
		CalendarKeyChecks(files);
		EarlyFlatChecks(files);
		RuntimeFlatChecks(root);
		GateSourceChecks(files);
		GateWiringChecks(files);
		GateRuleProbes(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Generated", "bin", "Release", "net8.0", "SandboxLib.dll"));

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

	// Every study with a Gate input turns on what the gate reads at the top of StartCalc, and its hostGate reads that data
	// behind a try/null check that blocks with a once-per-day Warning instead of dereferencing it (PS-01's Trend arm did).
	static void GateSourceChecks(List<string> files)
	{
		int hosts = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			if (!Regex.IsMatch(code, @"\[Input\]\s*public\s+ESandboxGate\s+Gate\b")) continue;
			hosts++;
			string start = MethodBody(code, @"void\s+StartCalc\s*\(\s*\)");
			int apply = start.IndexOf("applyGateNeeds();", StringComparison.Ordinal);
			int bse = start.IndexOf("base.StartCalc();", StringComparison.Ordinal);
			Check(apply >= 0 && bse > apply, name + " applies the gate's needs in StartCalc before base.StartCalc");
			string needs = MethodBody(code, @"void\s+applyGateNeeds\s*\(\s*\)");
			Check(needs.Contains("_SandboxRules.GateNeeds(Gate,") && needs.Contains("NeedsDaily = true") && needs.Contains("DvpConfig.enabled = true")
				&& needs.Contains("OwnProfileWindow = EOwnProfileWindow.Rth") && needs.Contains("NeedsDaily = false"), name + " applyGateNeeds turns on (and restores) Own RTH, daily and DVP from GateNeeds");
			string gate = MethodBody(code, @"bool\s+hostGate\s*\(\s*bool\s+buy\s*\)");
			Check(gate.Length > 0 && gate.Contains("try") && gate.Contains("catch (Exception") && gate.Contains("gateWarn(") && !Regex.IsMatch(gate, @"\bDailyContext\s*\."),
				name + " hostGate guards its reads (try, no DailyContext dereference, Warning on not ready)");
			string warn = MethodBody(code, @"void\s+gateWarn\s*\(\s*string\s+why\s*\)");
			Check(warn.Contains("m_gateWarnDay") && warn.Contains("ETraceLevel.Warning"), name + " gateWarn is a Warning once per day");
		}
		Check(hosts >= 89, "gate hosts found: " + hosts);
	}

	// Owner (2026-11-16): every gate study calls its gate before every entry order, side-aware, so a blocked gate skips the
	// entry. Each Send on an order created with EOrderAction.Buy / SellShort must sit directly behind if (hostGate(true / false)).
	static void GateWiringChecks(List<string> files)
	{
		var entryRx = new Regex(@"(\w+)\s*=\s*OrderCreator\.\w+\(\s*new\s+SOrderParameters\([^;]*?EOrderAction\.(Buy|SellShort)\)");
		int hosts = 0, sends = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			if (!Regex.IsMatch(code, @"bool\s+hostGate\s*\(\s*bool\s+buy\s*\)")) continue;
			hosts++;
			var side = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (Match m in entryRx.Matches(code)) side[m.Groups[1].Value] = m.Groups[2].Value == "Buy" ? "true" : "false";
			Check(side.Count > 0, name + " has entry orders for the gate to guard");
			int bad = 0, n = 0;
			foreach (var kv in side)
				foreach (Match s in Regex.Matches(code, @"(?<![\w.])" + Regex.Escape(kv.Key) + @"\.Send\("))
				{
					n++;
					string before = code.Substring(0, s.Index).TrimEnd();
					if (!before.EndsWith("if (hostGate(" + kv.Value + "))", StringComparison.Ordinal)) { bad++; Console.WriteLine("  ungated entry " + name + ": " + kv.Key + ".Send"); }
				}
			sends += n;
			Check(n > 0 && bad == 0, name + " calls hostGate(" + string.Join("/", new List<string>(side.Values).ToArray()) + ") before each of its " + n + " entry order calls");
		}
		Check(hosts >= 89, "gate hosts wired: " + hosts + " (entry calls " + sends + ")");
	}

	static string MethodBody(string code, string signature)
	{
		Match m = new Regex(signature + @"\s*\{").Match(code);
		if (!m.Success) return "";
		int depth = 0;
		for (int j = m.Index + m.Length - 1; j < code.Length; j++)
		{
			if (code[j] == '{') depth++;
			else if (code[j] == '}' && --depth == 0) return code.Substring(m.Index, j + 1 - m.Index);
		}
		return "";
	}

	// Unit probes of the shared gate rule (the built SandboxLib): a gate needs exactly the inputs its GateAllows arm reads,
	// a missing input is reported as not ready, and no gate throws on missing (NaN / 0) inputs.
	static void GateRuleProbes(string dll)
	{
		Type rules = null, gateT = null;
		try { Assembly a = Assembly.LoadFrom(Path.GetFullPath(dll)); rules = a.GetType("PowerLanguage.Strategy._SandboxRules"); gateT = a.GetType("PowerLanguage.Strategy.ESandboxGate"); }
		catch (Exception ex) { Console.WriteLine("  gate probe load: " + ex.Message); }
		Check(rules != null && gateT != null, "SandboxLib _SandboxRules loads for the gate probes");
		if (rules == null || gateT == null) return;
		MethodInfo allows = rules.GetMethod("GateAllows"), needs = rules.GetMethod("GateNeeds"), notReady = rules.GetMethod("GateNotReady");
		Func<object, bool, double, double, double, double, double, double, double, int, bool> A = (g, buy, c, poc, hi, lo, atr, sma, prev, fam) =>
			(bool)allows.Invoke(null, new object[] { g, buy, c, poc, hi, lo, atr, sma, prev, fam });
		foreach (object g in Enum.GetValues(gateT))
		{
			string n = g.ToString();
			// A setup every gate allows for a buy: close above the prior POC and the SMA, a narrow session, three families.
			double c = 100, poc = 99, hi = 100.5, lo = 100.2, atr = 2, sma = 95, prev = 98; int fam = 3;
			bool readsPoc = false, readsSma = false, readsFam = false, threw = false;
			try
			{
				bool baseOk = A(g, true, c, poc, hi, lo, atr, sma, prev, fam);
				readsPoc = baseOk != A(g, true, c, double.NaN, hi, lo, atr, sma, prev, fam);
				readsSma = baseOk != A(g, true, c, poc, hi, lo, atr, double.NaN, prev, fam);
				readsFam = baseOk != A(g, true, c, poc, hi, lo, atr, sma, prev, 0);
				foreach (bool buy in new[] { true, false })
				{
					A(g, buy, c, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0);
					A(g, buy, c, 0, 0, 0, 0, 0, 0, 0);
				}
			}
			catch (Exception) { threw = true; }
			Check(!threw, n + ": GateAllows does not throw on missing inputs");
			object[] args = { g, false, false, false, false };
			needs.Invoke(null, args);
			bool own = (bool)args[2], daily = (bool)args[3], dvp = (bool)args[4];
			Check(own == readsPoc && daily == readsSma && dvp == readsFam,
				n + ": GateNeeds(ownRth " + own + ", daily " + daily + ", dvp " + dvp + ") matches what GateAllows reads (poc " + readsPoc + ", sma " + readsSma + ", families " + readsFam + ")");
			Func<double, double, double, double, bool, string> R = (p, s, pr, at, d) => (string)notReady.Invoke(null, new object[] { g, p, s, pr, at, d });
			bool atrGate = n == "Balance" || n == "DayType" || n == "Noise" || n == "Compression";
			Check(R(poc, sma, prev, atr, true) == null, n + ": ready with every input present");
			if (readsPoc) Check(R(double.NaN, sma, prev, atr, true) != null, n + ": not ready without the prior RTH profile");
			if (readsSma) Check(R(poc, double.NaN, prev, atr, true) != null, n + ": not ready without the daily SMA200");
			if (readsFam) Check(R(poc, sma, prev, atr, false) != null, n + ": not ready with the DVP engine off");
			if (atrGate) Check(R(poc, sma, prev, double.NaN, true) != null, n + ": not ready without ATR(14)");
			if (n == "Off") Check(!own && !daily && !dvp && R(double.NaN, double.NaN, double.NaN, double.NaN, false) == null, "Off: needs nothing and is always ready");
		}
		object comp = Enum.Parse(gateT, "Compression");
		object[] ca = { comp, true, false, false, false }; needs.Invoke(null, ca);
		Check((bool)ca[3], "Compression with compressionUsesDaily (PS-01) needs the daily context");
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
			if (!ownRx.IsMatch(MethodBody(code, @"void\s+Create\s*\(\s*\)"))) continue;
			string start = MethodBody(code, @"void\s+StartCalc\s*\(\s*\)");
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
			string book = MethodBody(code, @"bool\s+sandboxBook\s*\(\s*\)");
			Match m = rearm.Match(book);
			int rest = book.IndexOf("_SandboxRules.Rest(", StringComparison.Ordinal);
			Check(m.Success && rest > m.Index, name + " sandboxBook re-arms the one-trade-per-day state at the trading-day change before Rest");
		}
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

	static string Src(List<string> files, string stem)
	{
		foreach (string p in files) if (Path.GetFileName(p).StartsWith(stem, StringComparison.Ordinal)) return Regex.Replace(File.ReadAllText(p), @"//[^\n]*", "");
		return "";
	}

	// Analyst MC17 (A2-30 balance lines dated on exchange holidays): a no-RTH Globex date (holiday halt, abbreviated Good Friday)
	// is not a trading day for prior-RTH bookkeeping, held-day counts or the A2-14 daily close; AV-05 reads its gap on the first RTH bar.
	static void CalendarKeyChecks(List<string> files)
	{
		Check(Regex.IsMatch(Src(files, "_A2_30_"), @"if \(IsNoRthDay\(day\)\) m_hi = m_lo = double\.NaN; else remember\(\);"), "A2-30 skips remember() on a no-RTH Globex date");
		Check(Src(files, "_LE_06_").Contains("Session.priorRthClose > 0 && !IsNoRthDay(day)"), "LE-06 does not add the carried prior close on a no-RTH date");
		Check(Src(files, "_SW_04_").Contains("if (!IsNoRthDay(day)) m_held++;"), "SW-04 counts RTH days held");
		string av5 = Src(files, "_AV_05_");
		Check(av5.Contains("if (m_alive && !IsNoRthDay(day)) m_held++;"), "AV-05 counts RTH days held");
		Check(Regex.IsMatch(av5, @"if \(!m_gapChecked && IsRth\(\) && Session != null && Session\.rthOpen > 0\)"), "AV-05 reads the gap on the first RTH bar, not at the rollover");
		string a14 = Src(files, "_A2_14_");
		Check(a14.IndexOf("IsNoRthDay(Bars.Time[0].Date)", StringComparison.Ordinal) >= 0 && a14.IndexOf("IsNoRthDay(Bars.Time[0].Date)", StringComparison.Ordinal) < a14.IndexOf("m_closes.Insert(0", StringComparison.Ordinal), "A2-14 does not treat a holiday session bar as a daily close");
		string a15 = Src(files, "_A2_15_");
		Check(a15.Contains("averageTrueRange(BarsOfData(2), 14)") && !a15.Contains("averageTrueRange(Bars, 14)"), "A2-15 ATR5 is ATR(14) of the 5-minute Data2 stream");
		Check(Regex.Matches(a15, @"skewLine\(").Count >= 5, "A2-15 writes a skew journal line for every evaluated setup");
		Check(a15.Contains("legWindow(side, out t0, out t1)") && a15.Contains("RangeProfile(1, t0, t1)"), "A2-15 leg is origin to extreme, not acceptance to now");
		Check(Src(files, "_A2_35_").Contains("if (_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)) return;"), "A2-35 sends no entry after that day's flat-by cutoff");
	}

	static readonly HashSet<string> NoEtClock = new HashSet<string>(StringComparer.Ordinal) { "_A2_49_MiddayBreakout.Strategy.CS", "_A2_57_TwoTimeframeTurn.Strategy.CS" };

	// Owner 2026-11-22 ("wire flat-by into all 78"): the GX FlatBy studies whose idea is multi-day or overnight by its catalogue
	// entry. Their arm() template still selects FlatBy, which is the wrong setting, so they are listed for the owner and are not wired.
	// Remove a name once the owner has ruled (HoldOvernight, or FlatBy and wire it).
	static readonly HashSet<string> FlatByReview = new HashSet<string>(StringComparer.Ordinal)
	{
		"AV-04", "AV-09", "LE-01", "LE-02", "LE-03", "LE-05", "LE-06", "LE-07", "PF-01", "PF-08",
		"SW-04", "VX-01", "VX-02", "VX-03", "VX-04", "VX-05", "VX-08", "YV-03", "YV-09",
	};

	static string StudyId(string name) { string[] p = name.Split('_'); return p.Length > 2 ? p[1] + "-" + p[2] : name; }

	// Analyst MC17 (A2-35 held over Good Friday 2023): the flat-by cutoff is that day's session end minus 5 minutes.
	// Owner 2026-11-22: every futures study that selects GX FlatBy reaches GX's flat-by from CalcBar through exitBar(), unless it is
	// listed in FlatByReview. GX itself handles early closes and holiday halts (PR #146), so no study runs a second early-flat path.
	static void EarlyFlatChecks(List<string> files)
	{
		int gx = 0, mf = 0, review = 0, flatBy = 0, own = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			bool futures = Regex.IsMatch(code, @"class _\w+ : (?!_CustomSignal\b|_VolumeProfileBase\b)\w+");
			if (!NoEtClock.Contains(name))
				Check(!Regex.IsMatch(code, @"_SandboxRules\.PastFlat\((sessionClock|tod\b|todNow\b)"), name + " uses the date-aware PastFlat");
			if (!futures) continue;
			string id = StudyId(name);
			if (code.Contains("ESessionExit.FlatBy"))
			{
				flatBy++;
				if (FlatByReview.Contains(id))
				{
					review++;
					Check(!code.Contains("exitBar();"), name + " is on the multi-day review list, so it is not wired to GX flat-by");
				}
				else
				{
					string calc = MethodBody(code, @"void\s+CalcBar\s*\(\s*\)");
					int at = calc.IndexOf("exitBar();", StringComparison.Ordinal);
					Check(at >= 0, name + " selects GX FlatBy and CalcBar calls exitBar()");
					Match entry = Regex.Match(calc, @"hostGate\(|(?<![\w.])arm\(");
					Check(at >= 0 && (!entry.Success || at < entry.Index), name + " runs the flat-by path before any entry or GX arm in CalcBar");
				}
			}
			if (code.Contains("ESessionExit.FlatBy") && code.Contains("exitBar();"))
			{
				gx++;
			}
			Check(!code.Contains("sandboxEarlyFlat"), name + " has no sandboxEarlyFlat: GX's own early-close flat-by is the one path (no double FLATBY)");
			// A study that flattens with its own orders at the cutoff never arms GX, so the two paths cannot both send an exit.
			if (code.Contains("ESessionExit.FlatBy") && Regex.IsMatch(code, @"bool flat = _SandboxRules\.PastFlat\("))
			{
				own++;
				int arms = Regex.Matches(code, @"(?<![\w.])arm\(").Count - Regex.Matches(code, @"void arm\(").Count;
				Check(arms == 0, name + " flattens with its own orders and never arms GX (" + arms + " arm calls)");
				Check(code.Contains("flat-by: \" + _SandboxRules.FlatByReason("), name + " journals its own flat-by exit with the shared reason");
			}
			int bare = Regex.Matches(code, @"mustFlatten\(\)").Count, paired = Regex.Matches(code, @"\(mustFlatten\(\) \|\| _SandboxRules\.EarlyFlatDue\(").Count;
			if (bare > 0)
			{
				mf++;
				Check(bare == paired, name + " pairs every mustFlatten() with EarlyFlatDue");
			}
		}
		Check(flatBy == 151, "futures studies that select GX FlatBy: " + flatBy);
		Check(gx == 132, "GX FlatBy studies with exitBar: " + gx + " (73 before, 59 wired by the owner's 2026-11-22 ruling)");
		Check(review == FlatByReview.Count && gx + review == flatBy, "multi-day FlatBy studies left for the owner: " + review);
		Check(own == 1, "own-order flat-by studies that select GX FlatBy (YV-11): " + own);
		Check(mf >= 15, "mustFlatten studies found: " + mf);
	}

	// Runs _SandboxRules from the semantic-compile assembly (reflection, so this project keeps no reference to it).
	static void RuntimeFlatChecks(string root)
	{
		string bin = Path.Combine(root, "tests", "SandboxCompile", "Generated", "bin");
		string dll = null; DateTime best = DateTime.MinValue;
		if (Directory.Exists(bin)) foreach (string p in Directory.GetFiles(bin, "SandboxLib.dll", SearchOption.AllDirectories)) { DateTime w = File.GetLastWriteTimeUtc(p); if (w > best) { best = w; dll = p; } }
		Check(dll != null, "SandboxLib.dll found for the runtime flat-by checks");
		if (dll == null) return;
		Type r = Assembly.LoadFrom(dll).GetType("PowerLanguage.Strategy._SandboxRules");
		Check(r != null, "_SandboxRules type loaded");
		if (r == null) return;
		MethodInfo past = r.GetMethod("PastFlat", new[] { typeof(DateTime), typeof(string), typeof(TimeSpan) });
		MethodInfo early = r.GetMethod("EarlyFlatDue", new[] { typeof(DateTime), typeof(string), typeof(TimeSpan) });
		MethodInfo end = r.GetMethod("SessionEndEt", new[] { typeof(DateTime), typeof(string), typeof(TimeSpan) });
		Func<int, int, int, int, int, bool> P = (y, mo, d, h, mi) => (bool)past.Invoke(null, new object[] { new DateTime(y, mo, d, h, mi, 0), "ES", TimeSpan.Zero });
		Func<int, int, int, int, int, bool> E = (y, mo, d, h, mi) => (bool)early.Invoke(null, new object[] { new DateTime(y, mo, d, h, mi, 0), "ES", TimeSpan.Zero });
		Func<int, int, int, TimeSpan> End = (y, mo, d) => (TimeSpan)end.Invoke(null, new object[] { new DateTime(y, mo, d), "ES", TimeSpan.Zero });
		Check(End(2023, 4, 7) == new TimeSpan(9, 15, 0), "Good Friday 2023 session ends 09:15 ET (" + End(2023, 4, 7) + ")");
		Check(!P(2023, 4, 7, 9, 9) && P(2023, 4, 7, 9, 10) && E(2023, 4, 7, 9, 10), "Good Friday 2023 flat-by 09:10 ET");
		Check(End(2024, 1, 15) == new TimeSpan(13, 0, 0) && E(2024, 1, 15, 12, 55) && !E(2024, 1, 15, 12, 54), "MLK 2024 halt 13:00 ET, flat-by 12:55 ET");
		Check(End(2024, 11, 29) == new TimeSpan(13, 15, 0) && E(2024, 11, 29, 13, 10) && !P(2024, 11, 29, 13, 9), "day after Thanksgiving 2024 flat-by 13:10 ET");
		Check(End(2024, 6, 3) == new TimeSpan(16, 0, 0) && P(2024, 6, 3, 15, 55) && !P(2024, 6, 3, 15, 54) && !E(2024, 6, 3, 15, 56), "regular day flat-by 15:55 ET, no early flat");
		Check(!P(2023, 4, 6, 18, 30) && !E(2023, 4, 6, 18, 30), "the 18:00 ET evening belongs to the next Globex date");

		// Shared journal reason for an own-order flat-by (YV-11).
		MethodInfo reason = r.GetMethod("FlatByReason", new[] { typeof(DateTime), typeof(string), typeof(TimeSpan) });
		Check(reason != null, "_SandboxRules.FlatByReason found");
		if (reason != null)
		{
			Func<DateTime, string> Why = t => (string)reason.Invoke(null, new object[] { t, "ES", TimeSpan.Zero });
			Check(Why(new DateTime(2024, 6, 3, 15, 55, 0)) == "regular session ends 16:00 ET", "regular-day flat-by reason: " + Why(new DateTime(2024, 6, 3, 15, 55, 0)));
			Check(Why(new DateTime(2023, 4, 7, 9, 10, 0)).StartsWith("Good Friday") && Why(new DateTime(2023, 4, 7, 9, 10, 0)).EndsWith(" session ends 09:15 ET"), "Good Friday 2023 flat-by reason: " + Why(new DateTime(2023, 4, 7, 9, 10, 0)));
			Check(Why(new DateTime(2024, 1, 15, 12, 55, 0)).EndsWith(" session ends 13:00 ET") && !Why(new DateTime(2024, 1, 15, 12, 55, 0)).StartsWith("regular"), "MLK 2024 flat-by reason names the halt");
		}
		WiredStudyFlatProbes(r);
	}

	// Behaviour of the flat-by path a wired study runs (owner 2026-11-22). Every wired study's arm() sets SessionExit = FlatBy and
	// FlatByHHMM = _SandboxRules.FlatBy(0 or its override, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd); exitBar() lets GX
	// close at the first FlatByHHMM after the fill. Early closes and holiday halts are GX's (PR #146, tested in ExitEngineTests there).
	// Representative: OF-08 and RG-04 on @ES 1-minute (CME, Central exchange clock), a regular day and a fill after the cutoff.
	static void WiredStudyFlatProbes(Type rules)
	{
		Type es = rules.Assembly.GetType("PowerLanguage.Function.ExitSession");
		MethodInfo flatBy = rules.GetMethod("FlatBy", new[] { typeof(int), typeof(string), typeof(string), typeof(TimeSpan) });
		MethodInfo deadline = es == null ? null : es.GetMethod("FlatByDeadline", new[] { typeof(DateTime), typeof(int) });
		Check(es != null && flatBy != null && deadline != null, "GX ExitSession.FlatByDeadline and _SandboxRules.FlatBy load from SandboxLib");
		if (es == null || flatBy == null || deadline == null) return;
		foreach (string study in new[] { "OF-08", "RG-04" })
		{
			int hhmm = (int)flatBy.Invoke(null, new object[] { 0, "CME", "@ES", TimeSpan.Zero });
			Check(hhmm == 1455, study + " on @ES (CME): GX FlatByHHMM is 14:55 exchange time, 15:55 ET (" + hhmm + ")");
			Func<DateTime, DateTime> Flat = fillCt => (DateTime)deadline.Invoke(null, new object[] { fillCt, hhmm });
			Check(Flat(new DateTime(2024, 6, 3, 10, 0, 0)) == new DateTime(2024, 6, 3, 14, 55, 0), study + " regular day 2024-06-03: a 10:00 CT fill flattens at 14:55 CT (15:55 ET)");
			Check(Flat(new DateTime(2024, 6, 3, 15, 10, 0)) == new DateTime(2024, 6, 4, 14, 55, 0), study + " a fill after the cutoff (15:10 CT) is held to the next session's 14:55 CT");
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
