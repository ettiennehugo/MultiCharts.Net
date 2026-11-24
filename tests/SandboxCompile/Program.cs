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
		BookOrderChecks(files);
		BugfixBatchChecks(files);
		AtrRollChecks(files);
		RollTableChecks(files);
		CalendarKeyChecks(files);
		EarlyFlatChecks(files);
		RuntimeFlatChecks(root);
		HoldChecks(files);
		FillBarUnarmedChecks(files, root);
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

	// Coder 2026-11-22 (owner rule: one attempt per anchor per day). _SandboxRules.Rest() only sees a fill if it runs on the
	// bar the position opens. A position early-return before sandboxBook() hid every fill, so after a same-day exit the book
	// resent the old entry. Every study with a resting book calls sandboxBook() in CalcBar before any position early-return.
	static readonly Regex PosGateRx = new Regex(@"if\s*\((?:[^;{()]|\([^;{()]*\))*(?:StrategyInfo\.MarketPosition|\bpos)\s*(?:!=|>|<)\s*0");
	static void BookOrderChecks(List<string> files)
	{
		int books = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			if (OriginalTen.Contains(name)) continue;
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			if (!code.Contains("_SandboxRules.Rest(") || !Regex.IsMatch(code, @"\bm_restOn\s*=\s*true")) continue;
			string calc = WithoutMethod(MethodBody(code, @"void\s+CalcBar\s*\(\s*\)"), "sendExits");   // a local exit function is not a return
			if (calc.Length == 0) continue;
			books++;
			int book = calc.IndexOf("sandboxBook()", StringComparison.Ordinal);
			Match gate = PosGateRx.Match(calc);
			Check(book >= 0 && (!gate.Success || book < gate.Index), name + " calls sandboxBook() in CalcBar before any position early-return");
		}
		Check(books >= 38, "resting-book studies checked for sandboxBook order: " + books);
	}

	// Coder 2026-11-22 (bugfix batch on #147): pins for each fix so a later edit cannot quietly undo it.
	static void BugfixBatchChecks(List<string> files)
	{
		var src = new Dictionary<string, string>();
		var raw = new Dictionary<string, string>();
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			Match m = Regex.Match(name, @"^_([A-Z0-9]+_[0-9]+)_");
			if (!m.Success) continue;
			string text = File.ReadAllText(path);
			raw[m.Groups[1].Value] = text;
			src[m.Groups[1].Value] = Regex.Replace(text, @"//[^\n]*", "");
		}
		Func<string, string> code = k => src.ContainsKey(k) ? src[k] : "";
		// Data2 = native timeframe: these read closed Data2 bars.
		foreach (string k in new[] { "YV_12", "A2_76", "A2_79", "SP_08", "DV_11", "A2_04" })
			Check(code(k).Contains("HtfClosedBar(2,"), k + " reads closed Data2 bars (HtfClosedBar(2, ...))");
		Check(Regex.IsMatch(code("YV_12"), @"atr14\s*=\s*nativeAtr\(14\)") && Regex.IsMatch(code("YV_12"), @"span\s*>=\s*1\.0\s*\*\s*atr14"), "YV_12 signal bar range >= 1.0 x ATR14 of the Data2 5m bars");
		// Recalc reuse: StartCalc clears the per-pass history.
		var clears = new[] { new[] { "A2_09", "m_hist.Clear()" }, new[] { "A2_14", "m_closes.Clear()" }, new[] { "A2_30", "m_slopes.Clear()" },
			new[] { "A2_37", "m_spread.Clear()" }, new[] { "A2_61", "m_hist.Clear()" }, new[] { "A2_61", "m_fives.Clear()" }, new[] { "A2_11", "m_wasComplete = false" }, new[] { "A2_62", "m_hi.Clear()" }, new[] { "A2_62", "m_cl.Clear()" } };
		foreach (string[] c in clears)
			Check(MethodBody(code(c[0]), @"void\s+StartCalc\s*\(\s*\)").Contains(c[1]), c[0] + " StartCalc resets " + c[1]);
		Check(!MethodBody(code("A2_14"), @"void\s+dailyClose\s*\(\s*int\s+pos\s*\)").Contains("m_le.Send(") && MethodBody(code("A2_14"), @"void\s+openEntry\s*\(\s*DateTime\s+day\s*\)").Contains("et.TimeOfDay < m_rthStart"), "A2_14 buys at the next RTH open (catalogue), not the Globex reopen (Analyst MES 12563c11)");
		Check(raw.ContainsKey("A2_14") && raw["A2_14"].Contains("// Chart: Data1 = 1-minute (owner rule). The daily swing builds its daily bars"), "A2_14 header: Data1 1-minute, daily bars built from it");
		Check(code("A2_14").Contains("private void dailyClose(int pos)") && Regex.IsMatch(MethodBody(code("A2_14"), @"void\s+dailyClose\s*\(\s*int\s+pos\s*\)"), @"m_held\+\+") && !Regex.IsMatch(MethodBody(code("A2_14"), @"void\s+CalcBar\s*\(\s*\)"), @"m_held\+\+|m_closes\.Insert"), "A2_14 counts held days and daily closes once per trading day, not per 1m bar");
		Check(Regex.IsMatch(code("VW_04"), @"if \(day != m_day\) \{[^}]*m_restOn = m_filledSetup = false"), "VW_04 new day clears the resting book");
		// Daily checks run on the first RTH bar, not the first Globex bar.
		foreach (string k in new[] { "TS_07", "RG_01", "RG_02", "RG_04", "RG_05", "RG_06", "RG_08", "RG_09", "RG_10", "YV_02", "A2_43" })
		{
			string calc = MethodBody(code(k), @"void\s+CalcBar\s*\(\s*\)");
			int rth = calc.IndexOf("if (!IsRth()) return;", StringComparison.Ordinal), day = calc.IndexOf("GlobexDate(Bars.Time[0])", StringComparison.Ordinal);
			Check(rth >= 0 && day > rth, k + " latches its daily check on the first RTH bar");
		}
		// Resting-book side and resend fixes.
		foreach (string k in new[] { "PS_09", "PS_23" })
			Check(code(k).Contains("m_restBuy") && !Regex.IsMatch(code(k), @"m_restBuy\s*=\s*true"), k + " rests the side it entered, not always a buy");
		foreach (string k in new[] { "AV_11", "A2_62", "A2_54" })
		{
			string book = MethodBody(code(k), @"bool\s+sandboxBook\s*\(\s*\)");
			int rs = book.IndexOf("ERest.Resend", StringComparison.Ordinal);
			Check(rs >= 0 && book.IndexOf(".Send(", rs, StringComparison.Ordinal) > rs, k + " sandboxBook resend sends the entry");
		}
		Check(MethodBody(code("A2_54"), @"void\s+CalcBar\s*\(\s*\)").Contains("sandboxBook()"), "A2_54 CalcBar runs its resting book");
		// A2-35 (catalogue Rev 6): acceptance on 5m closes, size 1 with the 1 percent budget on 1 contract.
		string a235 = code("A2_35");
		Check(MethodBody(a235, @"void\s+accept\s*\(\s*DateTime\s+et\s*\)").Contains("et.Minute % 5 != 0"), "A2_35 acceptance counts 5-minute closes only");
		Check(a235.Contains("m_le.Send(2)") && Regex.IsMatch(a235, @"\b2\s*\*\s*risk") && Regex.IsMatch(a235, @"exitPlan\([^;]*,\s*2,\s*mkt"), "A2_35 enters 2 contracts (catalogue T1 50 percent), budgets 2 and plans 2 lots");
		string a232 = code("A2_32");
		Check(a232.Contains("m_le.Send(2)") && !Regex.IsMatch(a232, @"\.Send\(1\)") && Regex.IsMatch(a232, @"(?<![\d.])2\s*\*\s*risk") && Regex.IsMatch(a232, @"exitPlan\([^;]*,\s*2,\s*mkt") && a232.Contains("ETargetMode.ScaleOutThenRunner") && a232.Contains("Minute % 15 != 0"), "A2_32 enters 2 contracts (owner 2026-11-22), budgets 2, plans 2 lots (T1 + runner), counts 15-minute closes");
		// A study that sends 2 contracts plans 2 lots in GX (it planned 1 and left a contract without exits).
		foreach (string k in src.Keys)
			if (Regex.IsMatch(src[k], @"\.Send\(2\)") && src[k].Contains("exitPlan("))
				Check(!Regex.IsMatch(src[k], @"exitPlan\([^;]*,\s*1,\s*mkt"), k + " plans as many GX lots as it sends");
		Check(Regex.IsMatch(code("A2_45"), @"GxConfig\.T1ScalePct = 60") && code("A2_45").Contains("m_far, up ?"), "A2_45 runs the second lot to the far shelf edge");
		Check(Regex.IsMatch(code("A2_28"), @"2\s*\*\s*risk") && code("A2_28").Contains("double edge = m_muMinor + m_side * m_sdMinor;"), "A2_28 budgets 2 contracts and tests its own side's edge");
		foreach (string k in new[] { "YV_11", "A2_28", "A2_11", "A2_61", "A2_30", "A2_09", "A2_46", "PS_09" })
			Check(code(k).Contains("HtfClosedBar(2,"), k + " reads closed Data2 bars (HtfClosedBar(2, ...))");
		Check(raw.ContainsKey("A2_46") && raw["A2_46"].Contains("// Chart: Data1 = 1-minute. Data2 = 5-minute"), "A2_46 declares Data2 = 5-minute");
		Check(raw.ContainsKey("PS_09") && raw["PS_09"].Contains("// Chart: Data1 = 1-minute. Data2 = 30-minute"), "PS_09 declares Data2 = 30-minute");
		string yv11 = MethodBody(code("YV_11"), @"void\s+CalcBar\s*\(\s*\)");
		Check(!Regex.IsMatch(yv11, @"if\s*\(\s*!IsExist\(2\)\s*\)\s*\{[^}]*return;"), "YV_11 runs its exits when Data2 is missing");
		Check(code("DV_01").Contains("mins >= -5 && mins < 10") && code("DV_01").Contains("GxConfig.NewsExitMinutes = 5") && code("DV_01").Contains("GxMinutesToNews = minutesToNews(day)"), "DV_01 news blackout [-5, +10] and GX NEWS flatten");
		Check(!code("TS_04").Contains("new TimeSpan(9, 0, 0)") && !code("TS_04").Contains("configureSessions"), "TS_04 takes its pit clock from ApplyPit, not a hard-coded CL clock");
		Check(Regex.IsMatch(MethodBody(code("OF_01"), @"bool\s+sandboxBook\s*\(\s*\)"), @"m_seLmt( : null\)\?)?\.Send\(") && MethodBody(code("OF_01"), @"void\s+CalcBar\s*\(\s*\)").Contains("sandboxBook()"), "OF_01 runs its book and resends the short limit");
		Check(Regex.Matches(MethodBody(code("A2_37"), @"void\s+CalcBar\s*\(\s*\)"), @"IsExist\(2\)").Count == 1, "A2_37 has one reachable Data2 guard");
		Check(Regex.IsMatch(code("AV_11"), @"m_seStp( : null\)\?)?\.Send\(") && Regex.IsMatch(MethodBody(code("AV_11"), @"bool\s+sandboxBook\s*\(\s*\)"), @"m_seStp( : null\)\?)?\.Send\("), "AV_11 resend covers the short side");
		// Analyst native retest of #152, item 1: no entry on or after the flat-by cutoff bar.
		var holdHosts = new HashSet<string> { "AV_04", "AV_09", "DV_09", "DV_11", "LE_01", "LE_02", "LE_03", "LE_06", "LE_07", "PF_08", "SW_04", "YV_03", "YV_09" };
		var overnightOwn = new HashSet<string> { "A2_14", "SW_02", "A2_69", "GX_00" };   // overnight by design, or the GX test harness
		int cutHosts = 0, cutOwn = 0;
		// Owner 2026-11-22 22:50 CT (catalogue 2.1 "no new entries after 15:30"): exempt from NoEntryMinutesBeforeFlat, derived from code.
		var holdWindowExempt = new HashSet<string> { "A2_46", "AV_01", "AV_03", "AV_05", "AV_12", "MT_04", "MT_05", "SW_01", "SW_03", "SW_05", "SW_06", "YV_10" };   // every arm holds overnight
		var ownWindowExempt = new HashSet<string> { "A2_62", "TS_03", "VW_05" };   // next-session exit; catalogue entries at 15:50 / 15:30 ET
		Action<string, string> checkWindowInput = (k, c) =>
		{
			Check(Regex.IsMatch(c, @"\[Input\] public int NoEntryMinutesBeforeFlat \{ get; set; \}") && Regex.IsMatch(c, @"\(object _ctx\)\s*:\s*base\(_ctx\)\s*\{[^}]*NoEntryMinutesBeforeFlat = 25;"), k + " declares NoEntryMinutesBeforeFlat with the constructor default 25");
			Check(raw[k].Contains("// Input: NoEntryMinutesBeforeFlat") && raw[k].Contains("// Default: 25 minutes"), k + " header section 17 documents NoEntryMinutesBeforeFlat");
		};
		foreach (var kv in src)
		{
			string c = kv.Value;
			if (!c.Contains("OrderCreator.")) continue;
			if (Regex.IsMatch(c, @"bool\s+hostGate\s*\(\s*bool\s+buy\s*\)"))
			{
				if (holdHosts.Contains(kv.Key)) { Check(Regex.IsMatch(c, @"SessionExit\s*=\s*ESessionExit\.HoldOvernight"), kv.Key + " is exempt from the cutoff guard only because GX holds it overnight"); continue; }
				string hg = MethodBody(c, @"bool\s+hostGate\s*\(\s*bool\s+buy\s*\)");
				string ecb = MethodBody(c, @"bool\s+entryCutoff\s*\(\s*\)");
				bool hostWindow = ecb.Contains("!gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd), NoEntryMinutesBeforeFlat)");
				Check((hostWindow || ecb.Contains("!gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, m_rthValueEnd))")) && !ecb.Contains("PastFlat("), kv.Key + " entryCutoff uses the GX date-aware cutoff (#146), not a duplicate");
				// Owner 2026-11-22 22:50 CT: every intraday GX host has the NoEntryMinutesBeforeFlat input (default 25); the all-hold hosts are exempt.
				Check(hostWindow == !holdWindowExempt.Contains(kv.Key), kv.Key + (hostWindow ? " passes NoEntryMinutesBeforeFlat to the cutoff" : " is exempt from NoEntryMinutesBeforeFlat (every entry held overnight)"));
				if (hostWindow) checkWindowInput(kv.Key, c);
				Check(Regex.IsMatch(hg, @"\)\s*\{\s*if\s*\(\s*entryCutoff\(\)\s*\)\s*return\s+false;"), kv.Key + " hostGate refuses entries on or after the flat-by cutoff bar");
				cutHosts++;
				continue;
			}
			var entries = new HashSet<string>();
			foreach (Match em in Regex.Matches(c, @"(m_\w+)\s*=\s*OrderCreator\.\w+\(new SOrderParameters\([^;]*EOrderAction\.(?:Buy|SellShort)\)")) entries.Add(em.Groups[1].Value);
			if (entries.Count == 0 || overnightOwn.Contains(kv.Key)) continue;
			bool ownWindow = Regex.IsMatch(c, @"bool entryOpen\(\) \{ return !gxPastFlatCutoff\(_SandboxRules\.FlatBy\(0, Bars\.Info\.Exchange, Bars\.Info\.Name, (m_rthValueEnd|Clock\.RthValueEnd)\), (Hold == ESessionHold\.Overnight \? 0 : )?NoEntryMinutesBeforeFlat\); \}");
			Check(ownWindow || Regex.IsMatch(c, @"bool entryOpen\(\) \{ return !gxPastFlatCutoff\(_SandboxRules\.FlatBy\(0, Bars\.Info\.Exchange, Bars\.Info\.Name, (m_rthValueEnd|Clock\.RthValueEnd)\)\); \}"), kv.Key + " has the flat-by entry cutoff (GX date-aware cutoff, #146)");
			Check(ownWindow == !ownWindowExempt.Contains(kv.Key), kv.Key + (ownWindow ? " passes NoEntryMinutesBeforeFlat to the cutoff" : " is exempt from NoEntryMinutesBeforeFlat (overnight hold or a catalogue entry after 15:30)"));
			if (ownWindow) checkWindowInput(kv.Key, c);
			foreach (string e in entries)
				Check(!Regex.IsMatch(c, @"(?<![\w.?])" + e + @"\.Send\("), kv.Key + " " + e + " is sent only through entryOpen()");
			cutOwn++;
		}
		Check(cutHosts >= 70 && cutOwn >= 35, "flat-by entry cutoff covers the hosts (" + cutHosts + ") and the own-entry studies (" + cutOwn + ")");
		// Item 7 (log volume): the per-bar DVP FIT / DVP RAIL journals are Verbose in the sandbox, and no CalcBar journals at Journal
		// level on every bar. A top-level Journal statement in CalcBar must follow a once-per-day / on-change return guard.
		foreach (var kv in src)
		{
			string c = kv.Value;
			if (kv.Key == "KD_08") continue;   // the forward-cone measurement: its DVP FIT journal is the deliverable
			Check(!Regex.IsMatch(c, @"DvpConfig\.journal(Fits|Rails)\s*=\s*true"), kv.Key + " does not force the DVP journals on");
			if (Regex.Matches(c, @"base\.StartCalc\(\);").Count == 1)
				Check(Regex.IsMatch(c, @"DvpConfig\.journalFits\s*=\s*DvpConfig\.journalRails\s*=\s*traceOn\(ETraceLevel\.Verbose\);[^;]*?base\.StartCalc\(\);"), kv.Key + " DVP FIT / RAIL journals are Verbose");
			string cb = MethodBody(c, @"void\s+CalcBar\s*\(\s*\)");
			if (cb.Length == 0) continue;
			cb = cb.Substring(cb.IndexOf('{') + 1, cb.Length - cb.IndexOf('{') - 2);
			var guard = new Regex(@"if\s*\([^;{]*(==|!=)\s*m_\w+[^;{]*\)\s*return\s*;|if\s*\(\s*!?\w+\s*\)\s*return\s*;|tod\s*!=|m_done\s*=\s*true|m_logged");
			int depth = 0, start = 0;
			for (int j = 0; j < cb.Length; j++)
			{
				char ch = cb[j];
				if (ch == '{') depth++;
				else if (ch == '}') { if (--depth == 0) start = j + 1; }
				else if (ch == ';' && depth == 0)
				{
					string st = cb.Substring(start, j + 1 - start);
					if (st.Contains("ETraceLevel.Journal") && !Regex.IsMatch(st, @"\breturn\b") && !Regex.IsMatch(st, @"if\s*\(\s*\w+\s*&&\s*journalOn") && !Regex.IsMatch(st, @"!=\s*m_\w*[Ll]og\w*\)"))
						Check(guard.IsMatch(cb.Substring(0, start)), kv.Key + " CalcBar journals at Journal level only once per day or on change");
					start = j + 1;
				}
			}
		}
		Check(code("A2_04").Contains("m_lxLmt.Send(m_target, 1)") && code("A2_04").Contains("Contracts.UserSpecified, \"A204LT\"") && !Regex.IsMatch(raw["A2_04"], @"Size(:| stays) 1 contract"), "A2_04 2 lots: T1 one lot at 1 R, the runner on the stop / sVWAP / 11:30 flat, header agrees");
		Check(code("MT_07").Contains("private const int HtfDs = 3;") && code("MT_07").Contains("dvpRailPrice(HtfDs, ELevelKind.DvpVah, 0)") && code("MT_07").Contains("HtfClosedBar(HtfDs, 0,") && Regex.IsMatch(code("MT_07"), @"DvpConfig\.streams\s*=\s*new\[\]\s*\{\s*2\s*,\s*3\s*\}") && code("PS_25").Contains("htfStep();"), "MT_07 tests the 60m (Data3) close against the 60m DVP stream's VAH after the engines step");
		// Analyst MT-07 retest (22 of 36 on the +2 sigma target, 13 on the bar after entry): PS-25 runs on the 15m Data2 stream in MT-07.
		Check(Regex.IsMatch(code("MT_07"), @"override\s+int\s+signalDs\(\)\s*\{\s*return\s+2;\s*\}") && code("PS_25").Contains("protected virtual int signalDs() { return 1; }")
			&& code("PS_25").Contains("dvpRailPrice(ds, ELevelKind.DvpVah, 0)") && code("PS_25").Contains("dvpRailPrice(ds, ELevelKind.DvpSigma, 2)") && code("PS_25").Contains("HtfClosedBar(ds, ago,")
			&& code("PS_25").Contains("if (ds > 1 && !newSignalBar(ds)) return;") && code("PS_25").Contains("double atr = ds == 1 ? averageTrueRange(Bars, 14) : sigAtr(ds, 14);")
			&& code("PS_25").Contains("GxConfig.FinalTargetValue = has ? Math.Abs(target - entry) : 0;") && code("PS_25").Contains("arm(true, entry, stop, up2, \"PS25LE\", 0)"), "MT_07 PS-25 rails, ATR, acceptance closes and +2 sigma target come from the 15m Data2 stream; the target is the rail price, in points from the entry");
		Check(code("PS_25").Contains("GxConfig.TimeStopBars = ds == 1 ? 41 : 0; m_timeStop = ds == 1 ? 0 : 41 * sigMin;"), "PS_25 41-bar time stop counts signal-stream bars (MT-07: 41 x 15 minutes)");
		Check(Regex.IsMatch(MethodBody(code("MT_07"), @"override\s+string\s+targetBlock\s*\(\s*double\s+target\s*\)"), @"dvpHtfNextRailDistAtr\(HtfDs, 1\)[\s\S]*target <= htfRail \? null"), "MT_07 the 15m target rail lies inside the 60m next-rail distance (catalogue MT-07)");
		Check(raw["MT_07"].Contains("// Chart: Data1 = 1-minute. Data2 = 15-minute (the PS-25 signal stream). Data3 = 60-minute (the HTF stream)."), "MT_07 header: Data2 15m, Data3 60m");
		// KD-05 is MT-07 (it inherits the code): same 1m / 15m / 60m chart, no override of the streams it reads.
		Check(Regex.IsMatch(code("KD_05"), @"class\s+_KD_05_HTFDetrendedRail\s*:\s*_MT_07_HtfDetrendedRail") && raw["KD_05"].Contains("// Chart: Data1 = 1-minute. Data2 = 15-minute (the PS-25 signal stream). Data3 = 60-minute (the HTF stream).")
			&& raw["KD_05"].Contains("// Data2: the same instrument on 15-minute bars") && raw["KD_05"].Contains("// Further series: Data3 = the same instrument on 60-minute bars") && !raw["KD_05"].Contains("Data2 = 60-minute"), "KD_05 header: the MT-07 chart, Data2 15m, Data3 60m");
		Check(!Regex.IsMatch(code("KD_05"), @"signalDs|targetBlock|htfStep|htfAccepted|DvpConfig\.streams|HtfClosedBar|dvpRailPrice") && code("KD_05").Contains("if (!IsExist(2) || !IsExist(3))"), "KD_05 reads Data2 / Data3 exactly as MT-07 (no override), and requires both");
		Check(code("PS_25").Contains("+ \" time stop 41 bars = \" + (41 * sigMin) + \" min\")"), "PS_25 plan line shows the time stop (41 bars = 41 min on Data1, 615 min for MT-07 / KD-05)");
		// Analyst LE-01 recheck: 'days' is ring occupancy (cap 253); the journal prints the uncapped count and the ring.
		Check(code("LE_01").Contains("DailyContext.DaysSeen + \" (ring \" + DailyContext.Days + \"/\" + DailyContext.Capacity"), "LE_01 journals the uncapped day count and the ring occupancy");
		// Resting-order windows use the GX date-aware cutoff (#146), not _SandboxRules.PastFlat.
		{
			int books = 0;
			foreach (var kv in src)
			{
				string sb = MethodBody(kv.Value, @"bool\s+sandboxBook\s*\(\s*\)");
				if (sb.Length == 0 || !sb.Contains("_SandboxRules.Rest(")) continue;
				Check(!sb.Contains("PastFlat("), kv.Key + " sandboxBook window does not use _SandboxRules.PastFlat");
				if (sb.Contains("gxPastFlatCutoff(_SandboxRules.FlatBy(0, Bars.Info.Exchange, Bars.Info.Name, ")) books++;
			}
			Check(Regex.Matches(code("A2_49"), @"if \(overRiskBudget\(r\)\) \{ budgetSkip\(").Count == 2 && Regex.IsMatch(code("A2_49"), @"logSignal\(""A2-49"", side, entry, entry - side \* r, ctx, false\)"), "A2_49 risk-budget skips are taken=false signal events (Analyst run 4ca93ff2)");
			foreach (string k in new[] { "A2_49", "DV_15" })
				Check(Regex.IsMatch(code(k), @"bool window = [^;]*!gxPastFlatCutoff\(_SandboxRules\.FlatBy\(0, Bars\.Info\.Exchange, Bars\.Info\.Name, (m_rthValueEnd|Clock\.RthValueEnd)\)\);") && !Regex.IsMatch(code(k), @"bool window = [^;]*PastFlat\("), k + " resting window uses the GX date-aware cutoff");
			Check(books >= 90, "sandboxBook windows on the GX cutoff: " + books);
		}
		Check(code("A2_28").Contains("mix.voidPrice[0]") && !Regex.IsMatch(code("A2_28"), @"v\[k\]\s*<\s*minV"), "A2_28 T1 is the mixture void, not the raw histogram's thinnest row");
		Check(Regex.IsMatch(code("A2_28"), @"GxConfig\.BreakEven\s*=\s*scale\s*\?\s*EBreakEven\.AfterT1") && !code("A2_28").Contains("GxConfig.BreakEven = EBreakEven.Off;") && raw["A2_28"].Contains("move A2-28's runner stop to entry after T1"), "A2_28 runner stop to entry after T1 (owner 2026-11-22), noted in the header");
		Check(code("A2_28").Contains("_SandboxRules.MidT1Points(longSide, entry, target, tickSize())") && code("A2_28").Contains("t1Src = \"mid\"") && code("A2_28").Contains("\" source \" + t1Src")
			&& raw["A2_28"].Contains("A2-28: use T1 halfway between entry and mu_major when the void is behind entry"), "A2_28 T1 falls back to the entry / mu_major midpoint (owner 2026-11-22 16:39 CT), journals its source, header quotes the decision");
		Check(code("A2_04").Contains("tod.Minutes % 5 == 0 && m_rthDen > 0 && Bars.Close[0] < m_rthNum / m_rthDen"), "A2_04 sVWAP trail tests 5-minute closes");
		// Analyst #152 recheck: GX re-resolves a host's per-trade modes while flat; A2-28 journals its plan once per trade; the DVP
		// lattice pivot/BOS facts are Verbose; DVP RUN fits counts the windows fitted; LE-01 journals its daily state.
		{
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(files[0]), ".."));
			string cs = File.ReadAllText(Path.Combine(root, "_CustomSignal.Strategy.CS"));
			string gx = File.ReadAllText(Path.Combine(root, "_GenericExit.Strategy.CS"));
			string dvp = File.ReadAllText(Path.Combine(root, "_DvpEngine.Function.CS"));
			string plan = MethodBody(cs, @"protected\s+double\s+exitPlan\s*\([^)]*\)");
			Check(gx.Contains("public bool Reconfigure()") && Regex.IsMatch(plan, @"MarketPosition\s*==\s*0\s*&&\s*m_gx\.Reconfigure\(\)") && plan.IndexOf("m_gx.Reconfigure()") < plan.IndexOf("m_gx.InitialStop("), "GX exitPlan re-resolves the host's modes while flat (A2-28 T1 / BreakEven)");
			string pf = MethodBody(cs, @"protected\s+bool\s+gxPastFlatCutoff\s*\(\s*int\s+flatByHHMM\s*\)");
			Check(pf.Contains("ExitSession.CutoffFor(ex.Date, flatByHHMM, f)") && pf.Contains("gxFlatDay(ex.Date)") && pf.Contains("gxTradingDate(ex) != ex.Date") && pf.Contains("f.Closed"), "the entry cutoff is GX's date-aware CutoffFor (#146) with the trading-date rollover");
			Check(cs.Contains("label = \"PA\" + ds + \"-DVP-S\" + str, journal = engineSink(ETraceLevel.Verbose)"), "DVP lattice PA pivot / BOS journal is Verbose");
			Check(dvp.Contains("\") fits(\" + (Config.journalFits ? I(FitLines) : \"off\")"), "DVP RUN fits reads off, not 0, when the FIT journal is off");
			Check(raw["A2_28"].Contains("\"A2-28 plan \"") && raw["A2_28"].Contains("\" muMajor \"") && raw["A2_28"].Contains("\" void \""), "A2_28 journals entry / stop / void / T1 / mu_major once per trade");
			Check(raw["LE_01"].Contains("m_stateDay") && raw["LE_01"].Contains("\" sma200 \""), "LE_01 journals its daily rule state once per day");
		}
		// Item 2: a stop on the fill bar still counts as a fill. Every Rest() call passes today's entry count.
		foreach (var kv in src)
			foreach (Match rm in Regex.Matches(kv.Value, @"_SandboxRules\.Rest\(([^;]*)\);"))
				Check(Regex.IsMatch(rm.Groups[1].Value, @"^\s*[^,]+,\s*TradesToday\s*,\s*ref\s+m_bookEntries\s*,"), kv.Key + " Rest() sees fills through TradesToday");
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
		// Coder 2026-11-22: A2-14 now builds daily bars from Data1 and keys the holiday test on the Globex trading date (m_day).
		Check(a14.IndexOf("IsNoRthDay(m_day)", StringComparison.Ordinal) >= 0 && a14.IndexOf("IsNoRthDay(m_day)", StringComparison.Ordinal) < a14.IndexOf("m_closes.Insert(0", StringComparison.Ordinal), "A2-14 does not treat a holiday session bar as a daily close");
		string a15 = Src(files, "_A2_15_");
		Check(a15.Contains("averageTrueRange(BarsOfData(2), 14)") && !a15.Contains("averageTrueRange(Bars, 14)"), "A2-15 ATR5 is ATR(14) of the 5-minute Data2 stream");
		Check(Regex.Matches(a15, @"skewLine\(").Count >= 5, "A2-15 writes a skew journal line for every evaluated setup");
		Check(a15.Contains("legWindow(side, out t0, out t1)") && a15.Contains("RangeProfile(1, t0, t1)"), "A2-15 leg is origin to extreme, not acceptance to now");
		Check(Src(files, "_A2_35_").Contains("if (_SandboxRules.PastFlat(ToEastern(Bars.Time[0]), Bars.Info.Name, m_rthValueEnd)) return;"), "A2-35 sends no entry after that day's flat-by cutoff");
	}

	static readonly HashSet<string> NoEtClock = new HashSet<string>(StringComparer.Ordinal) { "_A2_49_MiddayBreakout.Strategy.CS", "_A2_57_TwoTimeframeTurn.Strategy.CS" };

	// Owner 2026-11-22 11:03 CT ("Switch all 19 to hold overnight"): studies whose idea is multi-day or overnight by its catalogue
	// entry. Their arm() template selects HoldOvernight, and no mustFlatten / EarlyFlatDue / sandbox flat-by path closes them intraday.
	static readonly HashSet<string> HoldOvernightByOwner = new HashSet<string>(StringComparer.Ordinal)
	{
		"AV-04", "AV-09", "LE-01", "LE-02", "LE-03", "LE-05", "LE-06", "LE-07", "PF-01", "PF-08",
		"SW-04", "VX-01", "VX-02", "VX-03", "VX-04", "VX-05", "VX-08", "YV-03", "YV-09",
	};

	static string StudyId(string name) { string[] p = name.Split('_'); return p.Length > 2 ? p[1] + "-" + p[2] : name; }

	// Analyst MC17 (A2-35 held over Good Friday 2023): the flat-by cutoff is that day's session end minus 5 minutes.
	// Owner 2026-11-22: every futures study that selects GX FlatBy reaches GX's flat-by from CalcBar through exitBar(), and the owner's
	// multi-day studies (HoldOvernightByOwner) hold overnight. GX itself handles early closes and holiday halts (PR #146), so no study runs a second early-flat path.
	static void EarlyFlatChecks(List<string> files)
	{
		int gx = 0, mf = 0, hold = 0, flatBy = 0, own = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
			bool futures = Regex.IsMatch(code, @"class _\w+ : (?!_CustomSignal\b|_VolumeProfileBase\b)\w+");
			if (!NoEtClock.Contains(name))
				Check(!Regex.IsMatch(code, @"_SandboxRules\.PastFlat\((sessionClock|tod\b|todNow\b)"), name + " uses the date-aware PastFlat");
			if (!futures) continue;
			string id = StudyId(name);
			if (HoldOvernightByOwner.Contains(id))
			{
				hold++;
				Check(!code.Contains("ESessionExit.FlatBy") && Regex.IsMatch(code, @"GxConfig\.SessionExit = ESessionExit\.HoldOvernight;"), name + " is multi-day by the owner's ruling and its arm() selects HoldOvernight only");
				Check(!code.Contains("mustFlatten()") && !code.Contains("EarlyFlatDue") && !code.Contains("FlatByReason") && !Regex.IsMatch(code, @"bool flat = _SandboxRules\.PastFlat\("), name + " has no intraday flat-by path that contradicts the overnight hold");
			}
			if (code.Contains("ESessionExit.FlatBy"))
			{
				flatBy++;
				string calc = MethodBody(code, @"void\s+CalcBar\s*\(\s*\)");
				int at = calc.IndexOf("exitBar();", StringComparison.Ordinal);
				Check(at >= 0, name + " selects GX FlatBy and CalcBar calls exitBar()");
				Match entry = Regex.Match(calc, @"hostGate\(|(?<![\w.])arm\(");
				Check(at >= 0 && (!entry.Success || at < entry.Index), name + " runs the flat-by path before any entry or GX arm in CalcBar");
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
		Check(flatBy == 132, "futures studies that select GX FlatBy: " + flatBy);
		Check(gx == 132, "GX FlatBy studies with exitBar: " + gx + " (73 before, 59 wired by the owner's 2026-11-22 ruling)");
		Check(hold == HoldOvernightByOwner.Count && hold == 19 && gx == flatBy, "owner's multi-day studies set to HoldOvernight: " + hold + "; every FlatBy study calls exitBar()");
		Check(own == 1, "own-order flat-by studies that select GX FlatBy (YV-11): " + own);
		Check(mf >= 15, "mustFlatten studies found: " + mf);
	}

	// Owner 2026-11-22 11:48 CT ("Keep all 5 holding overnight"): holds approved although the catalogue entry window is intraday.
	// A2-46, SW-05 and YV-10 hold overnight with MaxHoldDays 1; DV-16 and VW-07 are journal-only layers whose unused arm()
	// template holds with MaxHoldDays 5. Code unchanged; their headers describe the hold and cite the ruling.
	static readonly HashSet<string> HoldApprovedByOwner = new HashSet<string>(StringComparer.Ordinal) { "A2-46", "SW-05", "YV-10", "DV-16", "VW-07" };

	// Top-level arguments of the call whose '(' is at open.
	static List<string> CallArgs(string code, int open)
	{
		var args = new List<string>(); int depth = 0; var cur = new System.Text.StringBuilder();
		for (int j = open; j < code.Length; j++)
		{
			char ch = code[j];
			if (ch == '(' || ch == '[' || ch == '{') { if (++depth == 1) continue; }
			else if (ch == ')' || ch == ']' || ch == '}') { if (--depth == 0) { args.Add(cur.ToString().Trim()); return args; } }
			else if (ch == ',' && depth == 1) { args.Add(cur.ToString().Trim()); cur.Clear(); continue; }
			cur.Append(ch);
		}
		return args;
	}

	// Owner 2026-11-22 ("fix the flat-by gaps", AV-02 held intraday trades for 10 sessions): only approved studies may select a GX
	// hold. A study selects a hold when its arm() sets HoldOvernight unconditionally, or sets it on a hold/holdDays argument and some
	// arm(...) call passes true or a positive count. Approved: the owner's 19 multi-day studies, a header whose exit line says
	// "Hold up to N sessions" / "MaxHoldDays is N" with N >= 2, or the owner's five approved holds above.
	static void HoldChecks(List<string> files)
	{
		int holds = 0, byHeader = 0, approved = 0; int maxHold1 = 0;
		var headerHold = new Regex(@"^// Exit orders:[^\n]*?(?:Hold up to (\d+) sessions|MaxHoldDays is (\d+))", RegexOptions.Multiline);
		foreach (string path in files)
		{
			string name = Path.GetFileName(path), text = File.ReadAllText(path);
			string code = Regex.Replace(Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\n]*", "");
			string id = StudyId(name);
			var sets = Regex.Matches(code, @"GxConfig\.SessionExit = ([^;]*);");
			bool always = false, maybe = false;
			foreach (Match s in sets)
			{
				string v = s.Groups[1].Value.Trim();
				if (v == "ESessionExit.HoldOvernight") always = true;
				else if (v.Contains("ESessionExit.HoldOvernight")) maybe = true;
			}
			bool holdCall = false;
			foreach (Match m in Regex.Matches(code, @"(?<![\w.])arm\("))
			{
				if (Regex.IsMatch(code.Substring(Math.Max(0, m.Index - 6), Math.Min(6, m.Index)), @"void\s+$")) continue;
				List<string> a = CallArgs(code, m.Index + m.Length - 1);
				string last = a.Count > 0 ? a[a.Count - 1] : "";
				if (last != "false" && last != "0" && !last.StartsWith("\"") && !last.Contains("?")) holdCall = true;
			}
			if (!(always || (maybe && holdCall))) continue;
			holds++;
			bool owner = HoldOvernightByOwner.Contains(id), approved5 = HoldApprovedByOwner.Contains(id), header = false;
			Match h = headerHold.Match(text);
			if (h.Success) { int n; header = int.TryParse(h.Groups[1].Success ? h.Groups[1].Value : h.Groups[2].Value, out n) && n >= 2; }
			if (!owner && header) byHeader++;
			if (approved5) approved++;
			Check(owner || header || approved5, name + " selects a GX hold (HoldOvernight) but is not an approved multi-day study");
			if (approved5) Check(text.Contains("owner decision 2026-11-22: keep holding overnight") && !text.Contains("Flat at the session value end") && !text.Contains("flat at the next session's value end"), name + " header describes the overnight hold and cites the owner's ruling");
			// GX PR #151: MaxHoldDays counts trading sessions, so the 1-day hold ends at the next trading session's flat-by cutoff.
			if (approved5 && text.Contains("MaxHoldDays 1 closes it"))
			{
				maxHold1++;
				Check(text.Contains("MaxHoldDays 1 closes it at the next trading session's flat-by cutoff (session end minus 5 minutes, early-close aware; an evening entry belongs to the next trading date)")
					&& !text.Contains("first bar dated the next calendar day"), name + " header describes MaxHoldDays 1 in trading sessions (GX #151)");
			}
		}
		Check(maxHold1 == 3, "MaxHoldDays 1 hold headers (A2-46, SW-05, YV-10): " + maxHold1);
		string av2 = Src(files, "_AV_02_");
		Check(av2.Contains("arm(true, entry, stop, peak, \"AV02LE\", false);") && !av2.Contains("\"AV02LE\", true)"), "AV-02 is intraday: arm(..., false) selects GX FlatBy");
		Check(holds == 35 && byHeader == 11 && approved == HoldApprovedByOwner.Count, "hold studies: " + holds + " (19 owner multi-day, " + byHeader + " multi-day by header, " + approved + " intraday-window holds approved by the owner)");
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
		// Owner 2026-11-22 16:39 CT (A2-28): T1 halfway between entry and mu_major when the void is behind the entry.
		MethodInfo mid = r.GetMethod("MidT1Points");
		Check(mid != null, "_SandboxRules.MidT1Points exists");
		if (mid != null)
		{
			Func<bool, double, double, double> M = (lg, en, tg) => (double)mid.Invoke(null, new object[] { lg, en, tg, 0.25 });
			Check(Math.Abs(M(true, 5000, 5010) - 5.0) < 1e-9 && Math.Abs(M(false, 5000, 4990) - 5.0) < 1e-9, "A2-28 mid T1: long and short midpoints, on the target side");
			Check(Math.Abs(M(true, 5000, 5010.25) - 5.25) < 1e-9 && Math.Abs(M(false, 5000, 4989.75) - 5.25) < 1e-9, "A2-28 mid T1: an odd tick count rounds the midpoint to the tick");
			Check(Math.Abs(M(true, 5000, 5000.5) - 0.25) < 1e-9, "A2-28 mid T1: a 2-tick target gives T1 one tick beyond the entry, one tick short of mu_major");
			Check(double.IsNaN(M(true, 5000, 5000.25)) && double.IsNaN(M(true, 5000, 4995)) && double.IsNaN(M(false, 5000, 5005)), "A2-28 mid T1: none when mu_major is under 2 ticks away or behind the entry");
		}
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


	// Coder 2026-11-23: a fill-and-stop on the same bar leaves MarketPosition == 0, so a resting entry that only checks
	// position will resend unarmed. DV-11 had its own 8-bar loop. Every other GX priced entry resends only through
	// Rest(TradesToday), which already treats that same-bar fill as Done.
	static void FillBarUnarmedChecks(List<string> files, string root)
	{
		string dv11Path = Path.Combine(root, "sandbox", "_DV_11_VWAPTrendFailure.Strategy.CS");
		string dv11 = File.ReadAllText(dv11Path);
		string calc = MethodBody(dv11, @"void\s+CalcBar\s*\(\s*\)");
		Check(calc.IndexOf("if (m_arm > 0 && !GxArmed)", StringComparison.Ordinal) >= 0, "DV-11 refuses to resend a resting limit when GX is disarmed");
		Check(calc.IndexOf("if (GxFillBarStopped) m_done = true;", StringComparison.Ordinal) >= 0, "DV-11 ends the session after a fill-bar stop (header §6)");
		Check(calc.IndexOf("exitDisarm();", StringComparison.Ordinal) >= 0, "DV-11 calls exitDisarm when the resting limit expires unfilled");
		int unarmedAt = calc.IndexOf("if (m_arm > 0 && !GxArmed)", StringComparison.Ordinal);
		int sendLoop = unarmedAt >= 0 ? calc.IndexOf("sendLimit();", unarmedAt, StringComparison.Ordinal) : -1;
		int guardEnd = unarmedAt >= 0 ? calc.IndexOf("if (rollOn(1)", unarmedAt, StringComparison.Ordinal) : -1;
		Check(unarmedAt >= 0 && guardEnd > unarmedAt && (sendLoop < 0 || sendLoop > guardEnd), "DV-11 does not call sendLimit in the unarmed-arm clear");

		var ownLoop = new List<string>();
		int restToday = 0, restOld = 0, exitPlan = 0;
		foreach (string path in files)
		{
			string name = Path.GetFileName(path);
			string text = File.ReadAllText(path);
			if (text.IndexOf("exitPlan(", StringComparison.Ordinal) < 0) continue;
			exitPlan++;
			string body = MethodBody(text, @"void\s+CalcBar\s*\(\s*\)");
			if (body.IndexOf("if (m_arm > 0)", StringComparison.Ordinal) >= 0 && body.IndexOf(".Send(", StringComparison.Ordinal) >= 0
				&& body.IndexOf("sandboxBook()", StringComparison.Ordinal) >= 0)
			{
				// A private countdown that resends after exitPlan, besides the book.
				if (body.IndexOf("if (m_arm > 0 && !GxArmed)", StringComparison.Ordinal) < 0
					&& body.IndexOf("if (!GxArmed)", StringComparison.Ordinal) < 0)
					ownLoop.Add(name);
			}
			if (text.IndexOf("_SandboxRules.Rest(", StringComparison.Ordinal) >= 0)
			{
				if (text.IndexOf("_SandboxRules.Rest(StrategyInfo.MarketPosition, TradesToday,", StringComparison.Ordinal) >= 0
					|| text.IndexOf("_SandboxRules.Rest(pos, TradesToday,", StringComparison.Ordinal) >= 0)
					restToday++;
				else
					restOld++;
			}
		}
		Check(ownLoop.Count == 0, "every GX study with its own resting resend loop guards !GxArmed: " + string.Join(", ", ownLoop.ToArray()));
		Check(restOld == 0 && restToday > 0, "every GX Rest() caller uses the TradesToday overload (" + restToday + " studies)");
		Check(exitPlan > 50, "audited exitPlan sandbox studies: " + exitPlan);
		Check(File.ReadAllText(Path.Combine(root, "sandbox", "_SandboxRules.Function.CS")).IndexOf("public static ERest Rest(int position, int entriesToday, ref int seenEntries", StringComparison.Ordinal) >= 0, "Rest(TradesToday) still detects a same-bar fill-and-stop");
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

	// T-R2 (m-instr §7.1 items 1 and 10, Confirmed by Ettienne 2026-11-22): the CME roll-date table is the roll rule. No sandbox
	// file uses Series / SeriesOrTable; every file that sets Table fills it from the profile (RollTableFromProfile = true in the
	// same statement line), keeps the non-futures Off line, and says Table on its header's Engine settings line.
	static void RollTableChecks(List<string> files)
	{
		int table = 0; var bad = new List<string>();
		foreach (string path in files)
		{
			string name = Path.GetFileName(path), text = File.ReadAllText(path);
			string code = Regex.Replace(text, @"//[^\n]*", "");
			if (Regex.IsMatch(code, @"ERollDetection\.(Series|SeriesOrTable)\b")) bad.Add(name + " uses Series / SeriesOrTable");
			if (!Regex.IsMatch(code, @"RollDetection\s*=\s*ERollDetection\.Table\s*;")) continue;
			table++;
			if (!Regex.IsMatch(code, @"RollDetection\s*=\s*ERollDetection\.Table\s*;\s*RollTableFromProfile\s*=\s*true\s*;")) bad.Add(name + " sets Table without RollTableFromProfile = true (T-R1b would throw on an empty table)");
			if (!Regex.IsMatch(code, @"if\s*\(!isFuturesCategory\(Bars\.Info\.Category\)\)\s*RollDetection\s*=\s*ERollDetection\.Off\s*;")) bad.Add(name + " lost the non-futures Off line");
			Match h = Regex.Match(text, @"^// Engine settings enabled:[^\n]*$", RegexOptions.Multiline);
			if (!h.Success || !h.Value.Contains("RollDetection = Table (the CME roll-date table")) bad.Add(name + " header Engine settings line does not say RollDetection = Table");
			if (Regex.IsMatch(text, @"^// Rolls:[^\n]*\bSeries\b", RegexOptions.Multiline)) bad.Add(name + " header Rolls line still says Series");
		}
		// Owner ruling 2026-11-23: the futures studies that had rolls off move to the table too; TS-01 stays off by design.
		string[] moved = { "_A2_31_", "_A2_34_", "_A2_36_", "_A2_37_", "_A2_38_", "_A2_40_", "_A2_43_", "_A2_72_", "_A2_73_", "_A2_74_", "_A2_75_", "_A2_76_", "_A2_77_",
			"_L2_01_", "_L2_02_", "_L2_03_", "_L2_04_", "_L2_05_", "_L2_06_", "_L2_07_", "_KD_08_", "_YV_03_" };
		foreach (string m in moved)
		{
			string f = files.Find(x => Path.GetFileName(x).StartsWith(m, StringComparison.Ordinal));
			string c = f == null ? "" : Regex.Replace(File.ReadAllText(f), @"//[^\n]*", "");
			if (!Regex.IsMatch(c, @"RollDetection\s*=\s*ERollDetection\.Table\s*;\s*RollTableFromProfile\s*=\s*true\s*;")) bad.Add(m + " (rolls-off study, owner ruling 2026-11-23) is not on Table");
		}
		string ts01 = files.Find(x => Path.GetFileName(x).StartsWith("_TS_01_", StringComparison.Ordinal));
		if (ts01 == null || Regex.IsMatch(Regex.Replace(File.ReadAllText(ts01), @"//[^\n]*", ""), @"ERollDetection\.(Table|Series|SeriesOrTable)\b")) bad.Add("_TS_01_ must stay with roll detection off (by design)");
		foreach (string f in files) if (File.ReadAllText(f).Contains("Not migrated to Table (T-R2 review list)")) bad.Add(Path.GetFileName(f) + " header still says Not migrated to Table");
		foreach (string b in bad) Console.WriteLine("FAIL " + b);
		Check(bad.Count == 0 && table >= 213, "T-R2 sandbox roll mode: " + table + " Table studies filled from the profile, none on Series / SeriesOrTable (" + bad.Count + " problems)");
	}
}
