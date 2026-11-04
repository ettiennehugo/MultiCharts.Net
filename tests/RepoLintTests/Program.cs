using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Repo lint: a study or function that PLStudies.csproj does not compile is invisible in the PLStudies build, so every root-level
// _<Name>.Function.CS / _<Name>.Strategy.CS / _<Name>.Indicator.CS has to appear as <Compile Include="..."/> in PLStudies.csproj.
// Matching is case-insensitive (MSBuild on Windows). Root files named literally (or by a simple * glob) in the root .gitignore are
// skipped: they are local MultiCharts files that are not in the repo and may sit in a working copy without a csproj entry.
// Runs in the normal check via tests/RepoLintTests/run.sh (check-mport-wt.sh runs every tests/* suite).
static class Program
{
	static int s_pass, s_fail;
	static void Check(bool ok, string what) { if (ok) s_pass++; else { s_fail++; Console.WriteLine("FAIL " + what); } }

	static readonly Regex StudyRx = new Regex(@"^_[^\\/]+\.(Function|Strategy|Indicator)\.CS$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	static string Root()
	{
		string d = AppContext.BaseDirectory;
		for (int i = 0; i < 8 && d != null; i++) { if (File.Exists(Path.Combine(d, "PLStudies.csproj"))) return d; d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar)); }
		return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
	}

	static List<string> Includes(string csproj)
	{
		XDocument x = XDocument.Load(csproj);
		return x.Descendants().Where(e => e.Name.LocalName == "Compile").Select(e => (string)e.Attribute("Include")).Where(s => s != null)
			.Select(s => s.Replace('/', '\\')).ToList();
	}

	static List<Regex> Ignored(string gitignore)
	{
		var r = new List<Regex>();
		if (!File.Exists(gitignore)) return r;
		foreach (string raw in File.ReadAllLines(gitignore))
		{
			string l = raw.Trim();
			if (l.Length == 0 || l.StartsWith("#") || l.StartsWith("!") || l.EndsWith("/")) continue;
			r.Add(new Regex("^" + Regex.Escape(l.TrimStart('/')).Replace(@"\*", @"[^\\/]*").Replace(@"\?", @"[^\\/]") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
		}
		return r;
	}

	// The lint itself: study files (names) that no include covers.
	static List<string> Missing(IEnumerable<string> files, IEnumerable<string> includes, List<Regex> ignored)
	{
		var inc = new HashSet<string>(includes, StringComparer.OrdinalIgnoreCase);
		return files.Where(f => StudyRx.IsMatch(f) && !ignored.Any(g => g.IsMatch(f)) && !inc.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
	}


	// ---- MI-T14 (M-INSTR spec rev 2.3 §10, PR-M1d): typed MC enums only (R1) ----
	// Scoped to the MultiCharts enum types below. Flags, per code line (comments stripped) of every root *.CS:
	//   mc-enum-tostring   .ToString() of an MC-enum value (member typed by MC, or a local/field declared with an MC enum type)
	//   mc-enum-parse      Enum.Parse/TryParse/GetNames/GetValues/GetName/IsDefined/ToObject with an MC enum type
	//   mc-enum-int        an MC enum cast from / compared with an int literal
	//   reflection         System.Reflection, or member lookup by name (GetProperty/GetField/GetMethod/GetMember/InvokeMember, Type.GetType("..."))
	// A ToString into a log line is allowed only through the exact allow-list (file + whole code line), every entry of which must still match.
	internal static readonly string[] McEnums = { "ESymbolCategory", "RequestTimeZone", "EResolution", "BuildVolumeOn", "MTPA_MCSymbolCurrency", "MTPA_MCSymbolCategories", "MTPA_SymbolFields", "DayOfWeek" };
	// MC members whose type is one of McEnums (Info.Category, Info.TimeZone / Request.TimeZone, Resolution.Type, ASymbolInfo2 fields,
	// StrategyCurrencyCode, BuildVolumeOn, SessionObject Start/EndDay)
	internal static readonly string McMembers = @"Category|TimeZone|Resolution\.Type|BuildVolumeOn|SymbolCurrency|SymbolCategory|UnsupportedFields|StrategyCurrencyCode|StartDay|EndDay";

	internal static HashSet<string> McLocals(string code)
	{
		var r = new HashSet<string>();
		// typed declarations (locals, fields, parameters, auto-properties: `ESymbolCategory m_cat { get; set; }`)
		foreach (Match m in Regex.Matches(code, @"\b(?:" + string.Join("|", McEnums) + @")\s*(?:\[\s*\])?\s+(\w+)\s*(?=[=;,){])")) r.Add(m.Groups[1].Value);
		// `var x = ...<MC member>;` (the right-hand side ends in an MC-enum member)
		foreach (Match m in Regex.Matches(code, @"\bvar\s+(\w+)\s*=\s*[^;=]*\.(?:" + McMembers + @")\s*;")) r.Add(m.Groups[1].Value);
		return r;
	}

	internal static List<string> McEnumLint(string line, HashSet<string> locals, HashSet<string> ownTypes)
	{
		var r = new List<string>();
		string types = string.Join("|", McEnums);
		string member = @"\.(?:" + McMembers + @")\b(?!\s*\.\s*\w)";   // an MC-enum member as a value (not followed by a further member access)
		string local = locals.Count > 0 ? @"(?<![\w.])(?:" + string.Join("|", locals) + @")\b(?!\s*\.\s*\w)" : null;
		string val = local == null ? member : "(?:" + member + "|" + local + ")";
		// explicit ToString: any overload on an MC member (ToString("G") too); no-argument only on tracked locals (a format overload on a
		// local is DateTime/number in this repo: `d.ToString("yyyy-MM-dd")`)
		bool ts = Regex.IsMatch(line, @"\.(?:" + McMembers + @")\s*\.\s*ToString\s*\(")
			|| (locals.Count > 0 && Regex.IsMatch(line, @"(?<![\w.])(?:" + string.Join("|", locals) + @")\s*\.\s*ToString\s*\(\s*\)"));
		if (ts) r.Add("mc-enum-tostring");
		// implicit stringification ($"{..}", string.Format, Convert.ToString, "" + ..) of an MC-enum value on a line that also compares or
		// parses a string. Plain interpolation into a log line has no such marker and stays legal.
		bool implicitStr = Regex.IsMatch(line, @"\$@?""[^""]*\{[^}]*" + val + @"[^}]*\}") || Regex.IsMatch(line, @"string\s*\.\s*Format\s*\([^;]*" + val)
			|| Regex.IsMatch(line, @"Convert\s*\.\s*ToString\s*\([^;]*" + val) || Regex.IsMatch(line, @"""\s*\+\s*[\w.\[\]()]*" + val) || Regex.IsMatch(line, val + @"\s*\+\s*""");
		bool strUse = Regex.IsMatch(line, @"(?:==|!=)\s*@?""|""\s*(?:==|!=)|\.\s*(?:Equals|Contains)\s*\(|StartsWith|switch\s*\(|Parse");
		if (implicitStr && strUse) r.Add("mc-enum-implicit-string");
		if (Regex.IsMatch(line, @"Enum\s*\.\s*(?:Try)?Parse\s*<\s*(?:[\w.]*\.)?(?:" + types + @")\s*>")
			|| Regex.IsMatch(line, @"Enum\s*\.\s*(?:Try)?Parse\s*\([^;]*typeof\s*\(\s*(?:[\w.]*\.)?(?:" + types + @")\s*\)")
			|| Regex.IsMatch(line, @"Enum\s*\.\s*(?:GetNames|GetValues|GetName|IsDefined|ToObject|Parse|TryParse)\s*\(\s*typeof\s*\(\s*(?:[\w.]*\.)?(?:" + types + @")\s*\)")) r.Add("mc-enum-parse");
		if (Regex.IsMatch(line, @"\(\s*(?:[\w.]*\.)?(?:" + types + @")\s*\)\s*\(?\s*-?\d")
			|| Regex.IsMatch(line, @"\.(?:" + McMembers + @")\s*(?:==|!=|<=|>=|<|>)\s*\(?\s*-?\d")
			|| Regex.IsMatch(line, @"\(\s*int\s*\)\s*[\w.\[\]]*\.(?:" + McMembers + @")\s*(?:==|!=|<=|>=|<|>)\s*-?\d")
			|| Regex.IsMatch(line, @"(?<![\w.])-?\d+\s*(?:==|!=|<=|>=|<|>)\s*\(?\s*(?:\(\s*int\s*\)\s*)?[\w.\[\]]*\.(?:" + McMembers + @")\b")   // 12 == Info.Category
			|| Regex.IsMatch(line, @"Convert\s*\.\s*ToInt(?:16|32|64)\s*\([^;)]*\.(?:" + McMembers + @")\s*\)\s*(?:==|!=|<=|>=|<|>)\s*-?\d")
			|| Regex.IsMatch(line, @"(?<![\w.])-?\d+\s*(?:==|!=|<=|>=|<|>)\s*Convert\s*\.\s*ToInt(?:16|32|64)\s*\([^;)]*\.(?:" + McMembers + @")\s*\)")) r.Add("mc-enum-int");
		bool byName = false;
		foreach (Match m in Regex.Matches(line, @"\.\s*(?:GetProperty|GetField|GetMethod|GetMember|InvokeMember)\s*\("))
		{
			// reflection over a type declared in this repo (typeof(Own).GetField(..)) reads no MultiCharts member: R1 is about MC types
			Match o = Regex.Match(line.Substring(0, m.Index), @"typeof\(\s*(\w+)\s*\)\s*$");
			if (!(o.Success && ownTypes.Contains(o.Groups[1].Value))) byName = true;
		}
		if (byName || Regex.IsMatch(line, @"\bSystem\.Reflection\b") || Regex.IsMatch(line, @"(?<!\w)Type\s*\.\s*GetType\s*\(\s*@?""")) r.Add("reflection");
		return r;
	}

	// The exact MI-T14 allow-list (spec §10): file + whole code line (trimmed, comments stripped). Log formatting or the
	// [Input] snapshot only; nothing reads these back.
	internal static readonly string[][] McAllow = {
		new[] { "_CustomSignal.Strategy.CS", "System.Reflection.PropertyInfo[] props = this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);" },   // inputSnapshot
		new[] { "_CustomSignal.Strategy.CS", "foreach (System.Reflection.PropertyInfo p in props)" },   // inputSnapshot
		new[] { "_CustomSignal.Strategy.CS", "private static bool isInputProperty(System.Reflection.PropertyInfo p)" },   // inputSnapshot helper
		new[] { "_MarketBreadth.Strategy.CS", "using System.Reflection;" },
		new[] { "_CustomSignal.Strategy.CS", "try { return Bars.Info.TimeZone.ToString(); } catch { return \"?\"; }" },   // barZoneLabel (log)
		new[] { "_CustomSignal.Strategy.CS", "string warn = _RollDetector.SeriesKeyWarning(m_rollDet.series, intraday, res.ToString(), calendarKeyed && !m_rollGlobexKey);" },   // Warning text (log)
		// PR-M4: Ettienne's _RootDump probe (e1f5b11) lists the MTPA_SymbolFields members into its Output dump (log only, nothing read back)
		new[] { "_RootDump.Strategy.CS", "foreach (MTPA_SymbolFields f in Enum.GetValues(typeof(MTPA_SymbolFields)))" },
	};
	// _MtfMath Session.ToString (DayOfWeek day names into the MC-1439 mismatch text, log only; the compare is typed) is added
	// beyond the spec's list (PR-M1d judgment call).
	internal static readonly string[][] McAllowPrefix = {
		new[] { "_MtfMath.Function.CS", "return StartDay.ToString().Substring(0, 3) + \" \" + StartTime.ToString(" },
	};

	static string StripComment(string l) { return Regex.Replace(l, @"//.*$", "").Trim(); }

	static int Main()
	{
		string root = Root(), csproj = Path.Combine(root, "PLStudies.csproj");
		List<string> inc = Includes(csproj);
		List<Regex> ign = Ignored(Path.Combine(root, ".gitignore"));
		List<string> files = Directory.GetFiles(root).Select(Path.GetFileName).ToList();
		List<string> studies = files.Where(f => StudyRx.IsMatch(f)).ToList();

		// (1) sanity: the lint sees the repo (PLStudies.csproj parsed, the study files found)
		Check(inc.Count >= 400, "PLStudies.csproj parsed: " + inc.Count + " Compile includes");
		Check(studies.Count >= 200, "root study files found: " + studies.Count);

		// (2) the lint: every root-level _*.Function/Strategy/Indicator.CS is compiled by PLStudies.csproj
		List<string> miss = Missing(files, inc, ign);
		foreach (string m in miss) Console.WriteLine("  not in PLStudies.csproj: <Compile Include=\"" + m + "\" />");
		Check(miss.Count == 0, "every root _*.Function/Strategy/Indicator.CS is in PLStudies.csproj (" + miss.Count + " missing of " + studies.Count + ")");

		// (3) the lint catches a missing file and honours case / .gitignore (self-test on synthetic names)
		var fakeFiles = new[] { "_A.Function.CS", "_B.Strategy.CS", "_C.Indicator.CS", "_D.Signal.CS", "E.Function.CS", "_Ign.Indicator.CS", "_Glob1.Strategy.CS" };
		var fakeInc = new[] { "_a.function.cs", "_C.Indicator.CS" };
		var fakeIgn = new List<Regex> { new Regex("^_Ign\\.Indicator\\.CS$", RegexOptions.IgnoreCase), new Regex("^_Glob[^\\\\/]*\\.Strategy\\.CS$", RegexOptions.IgnoreCase) };
		List<string> fm = Missing(fakeFiles, fakeInc, fakeIgn);
		Check(fm.Count == 1 && fm[0] == "_B.Strategy.CS", "self-test: only _B.Strategy.CS reported, got [" + string.Join(",", fm) + "]");

		// (4) the PR-D9a files are listed
		Check(inc.Contains("_DvpDegreeReport.Function.CS") && inc.Contains("_DvpEngineTest.Strategy.CS"), "PR-D9a files in PLStudies.csproj");

		// (5) MI-T14: no MC enum / API member used as a string in any root *.CS (exact allow-list, no stale entries)
		{
			var hits = new List<string>(); var used = new HashSet<string>(); int scanned = 0;
			var rootFiles = Directory.GetFiles(root, "*.CS").Concat(Directory.GetFiles(root, "*.cs")).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
			var ownTypes = new HashSet<string>();
			foreach (string f in rootFiles)
				foreach (Match m in Regex.Matches(Regex.Replace(File.ReadAllText(f), @"//[^\n]*", ""), @"\b(?:class|struct|enum|interface)\s+(\w+)")) ownTypes.Add(m.Groups[1].Value);
			foreach (string f in rootFiles)
			{
				string name = Path.GetFileName(f); scanned++;
				string text = File.ReadAllText(f);
				string[] lines = text.Split('\n');
				var code = lines.Select(StripComment).ToArray();
				HashSet<string> locals = McLocals(string.Join("\n", code));
				for (int i = 0; i < code.Length; i++)
				{
					if (code[i].Length == 0) continue;
					List<string> rules = McEnumLint(code[i], locals, ownTypes);
					if (rules.Count == 0) continue;
					string[] a = McAllow.FirstOrDefault(e => e[0] == name && e[1] == code[i]) ?? McAllowPrefix.FirstOrDefault(e => e[0] == name && code[i].StartsWith(e[1], StringComparison.Ordinal));
					if (a != null) { used.Add(a[0] + "|" + a[1]); continue; }
					hits.Add(name + ":" + (i + 1) + " " + string.Join(",", rules) + " `" + code[i] + "`");
				}
			}
			foreach (string h in hits) Console.WriteLine("  MI-T14: " + h);
			Check(scanned >= 200 && hits.Count == 0, "MI-T14 no MC-enum ToString/parse/int literal or reflection by name outside the allow-list (" + hits.Count + " hits in " + scanned + " files)");
			Check(used.Count == McAllow.Length + McAllowPrefix.Length, "MI-T14 every allow-list entry still matches a source line (no stale entries): " + used.Count + "/" + (McAllow.Length + McAllowPrefix.Length));

			// self-test: the patterns R1 forbids are caught, the typed forms pass
			var loc = new HashSet<string> { "res", "cat", "d" }; var own = new HashSet<string> { "Entry" };
			string[] bad = {
				"if (Bars.Info.Category.ToString() == \"Future\") return;", "switch (Bars.Info.Resolution.Type.ToString()) { }", "bool fx = info.Category.ToString().Contains(\"FOREX\");",
				"if (res.ToString() == \"Tick\") x = 1;", "var c = (ESymbolCategory)Enum.Parse(typeof(ESymbolCategory), s);", "Enum.TryParse<EResolution>(s, out r);",
				"foreach (var n in Enum.GetNames(typeof(MTPA_MCSymbolCategories))) {}", "if (Enum.IsDefined(typeof(MTPA_SymbolFields), v)) {}", "var t = (RequestTimeZone)1;",
				"if (Bars.Info.Category == 12) return;", "if ((int)Bars.Info.Category == 12) return;", "if ((MTPA_SymbolFields)(-1) == f) {}", "var f = (MTPA_SymbolFields)512;",
				"if (s.SymbolCurrency.ToString() != \"eMTPA_MCSC_USD\") throw new X();", "object v = Bars.Info.GetType().GetProperty(\"Category\").GetValue(Bars.Info, null);",
				"x = b.GetType().InvokeMember(\"Close\", f, null, b, null);", "using System.Reflection;", "var t = Type.GetType(\"PowerLanguage.ESymbolCategory\");",
				"if (sess.StartDay.ToString() == \"Monday\") {}", "string k = cat.ToString().ToUpperInvariant();", "BuildVolumeOn b = (BuildVolumeOn)Enum.Parse(typeof(BuildVolumeOn), s);",
				"var p = typeof(IInstrument).GetProperty(\"Category\");",
				// #95 review: implicit stringification + compare/parse, ToString overloads, reversed and Convert.ToInt32 int compares
				"if ($\"{Bars.Info.Category}\" == \"Future\") return;", "bool fx = string.Format(\"{0}\", Bars.Info.Category).Contains(\"FOREX\");",
				"if (Convert.ToString(Bars.Info.Category).Equals(\"Future\")) return;", "switch (\"\" + Bars.Info.Category) { }", "var v = int.Parse(\"\" + s.UnsupportedFields);",
				"if (Bars.Info.Category + \"\" != \"Stock\") return;", "if ($\"{cat}\".StartsWith(\"Fut\")) return;",
				"if (Bars.Info.Category.ToString(\"G\") == \"Future\") return;", "if (12 == Bars.Info.Category) return;", "if (12 == (int)Bars.Info.Category) return;",
				"if (Convert.ToInt32(Bars.Info.Category) == 12) return;", "if (0 != Convert.ToInt32(s.UnsupportedFields)) return;" };
			var missed = bad.Where(b => McEnumLint(b, loc, own).Count == 0).ToList();
			Check(missed.Count == 0, "MI-T14 self-test: every forbidden pattern is flagged [" + string.Join(" | ", missed) + "]");
			string[] good = {
				"switch (Bars.Info.Category) { case ESymbolCategory.Future: break; }", "if (s.SymbolCurrency != MTPA_MCSymbolCurrency.eMTPA_MCSC_USD) throw new X();",
				"if ((s.UnsupportedFields & MTPA_SymbolFields.eMTPA_SF_Root) != 0) useParsed = true;", "EResolution res = Bars.Info.Resolution.Type;",
				"economicEvent.volatility = (EEventVolatility)Enum.Parse(typeof(EEventVolatility), line.volatility);", "string id = m_count.ToString();",
				"if (Bars.Info.Category == ESymbolCategory.Forex) return;", "var tier = (EEventTier)Enum.Parse(typeof(EEventTier), c[4]);", "int n = Bars.Info.Resolution.Size;",
				"var prop = typeof(Entry).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);", "return d.ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture);",
				"Output.WriteLine($\"category({info.Category}) zone({info.TimeZone}) res({info.Resolution.Type})\");", "string msg = \"cat \" + Bars.Info.Category;",
				"traceAlways(ETraceLevel.Error, null, \"category \" + Bars.Info.Category + \" unsupported\");", "if (x == \"a\") log($\"n({n})\");",
				"if (Bars.Info.Category == ESymbolCategory.Future && n == 12) return;", "int k = Convert.ToInt32(s, 10) == 12 ? 1 : 0;" };
			var falsePos = good.Where(g => McEnumLint(g, loc, own).Count != 0).ToList();
			Check(falsePos.Count == 0, "MI-T14 self-test: typed forms and repo-own enum parses pass [" + string.Join(" | ", falsePos) + "]");
			Check(McLocals("EResolution res = x; ESymbolCategory cat; MTPA_SymbolFields[] all = y; string s;").SetEquals(new[] { "res", "cat", "all" }), "MI-T14 self-test: MC-enum locals/fields are tracked");
			HashSet<string> lv = McLocals("var c = Bars.Info.Category; var z = info.TimeZone; var n = Bars.Info.Name; ESymbolCategory m_cat { get; set; } var r2 = Bars.Info.Resolution.Type;");
			Check(lv.SetEquals(new[] { "c", "z", "m_cat", "r2" }) && McEnumLint("if (c.ToString() == \"Future\") return;", lv, own).Count > 0
				&& McEnumLint("if (m_cat.ToString().Contains(\"FOREX\")) return;", lv, own).Count > 0 && McEnumLint("if (n.ToString() == \"ES\") return;", lv, own).Count == 0,
				"MI-T14 self-test: `var x = ..<MC member>;` and MC-enum auto-properties are tracked (and a var of a non-enum member is not) [" + string.Join(",", lv) + "]");
			Check(McEnums.Contains("MTPA_MCSymbolCategories") && McEnums.Contains("MTPA_SymbolFields") && McEnums.Length == 8, "MI-T14 scope = the eight MC enum types of spec rev 2.3 §10");
		}

		Console.WriteLine($"RepoLintTests: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
