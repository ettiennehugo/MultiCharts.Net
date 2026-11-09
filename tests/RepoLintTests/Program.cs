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
// sandbox/ is the research draft folder (docs/research-strategy-header.md). Those studies stay out of every csproj. Production
// projects stay on explicit file names: no wildcard Compile, no EnableDefaultCompileItems.
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

	// ---- research sandbox (docs/research-strategy-header.md) ----
	// Production projects list every file. A wildcard, or any Compile path that names the sandbox folder, would pull unproven
	// studies into the one MultiCharts assembly. The template is comments only and is not a study file.

	static readonly string[] HeaderSections = {
		"// ## 1. Identity",
		"// ## 2. Provenance",
		"// ## 3. Hypothesis",
		"// ## 4. Trade walkthrough",
		"// ## 5. Timing",
		"// ## 6. Position and re-entry",
		"// ## 7. Instruments",
		"// ## 8. Data",
		"// ## 9. Entry and filters",
		"// ## 10. Position sizing and risk",
		"// ## 11. Orders, execution, and costs",
		"// ## 12. Trade management",
		"// ## 13. Exits",
		"// ## 14. Regimes",
		"// ## 15. Dependencies",
		"// ## 16. Pitfalls",
		"// ## 17. Parameters",
		"// ## 18. Acceptance policy",
		"// ## 19. Validation",
		"// ## 20. Edge monitoring",
		"// ## 21. Log diagnostics",
		"// ## 22. Notes",
	};

	// Existing sandbox files whose public class predates the "class name = file stem" rule. New files are not added here.
	static readonly Dictionary<string, string> SandboxClassAllow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "_A2_70_OpenFadeHvGate.Strategy.CS", "_Ex17OpenFadeHvGate" },
		{ "_DV_15_EmaVwapPullback.Strategy.CS", "_Ex20EmaVwapPullback" },
	};

	static bool HasGlob(string include) { return include != null && (include.IndexOf('*') >= 0 || include.IndexOf('?') >= 0); }

	static bool PointsAtSandbox(string include)
	{
		if (string.IsNullOrEmpty(include)) return false;
		string n = include.Replace('/', '\\').Trim();
		while (n.StartsWith(".\\", StringComparison.Ordinal)) n = n.Substring(2);
		foreach (string p in n.Split('\\'))
			if (string.Equals(p, "sandbox", StringComparison.OrdinalIgnoreCase)) return true;
		return false;
	}

	static bool IsProductionProject(string csprojPath)
	{
		string name = Path.GetFileName(csprojPath);
		return string.Equals(name, "PLStudies.csproj", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(name, "PLStudiesPY.csproj", StringComparison.OrdinalIgnoreCase);
	}

	static bool EnablesDefaultCompile(string csprojText)
	{
		return Regex.IsMatch(csprojText ?? "", @"<EnableDefaultCompileItems>\s*true\s*</EnableDefaultCompileItems>", RegexOptions.IgnoreCase);
	}

	static List<string> AllCsproj(string root)
	{
		var list = new List<string>(Directory.GetFiles(root, "*.csproj"));
		string tests = Path.Combine(root, "tests");
		if (Directory.Exists(tests)) list.AddRange(Directory.GetFiles(tests, "*.csproj", SearchOption.AllDirectories));
		return list;
	}

	static string StudyStem(string fileName)
	{
		Match m = Regex.Match(fileName ?? "", @"^(_[^\\/]+)\.(Function|Strategy|Indicator)\.CS$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return m.Success ? m.Groups[1].Value : null;
	}

	// New research strategies: _<ID>_<StudyName>.Strategy.CS. ID is the catalogue id (FAM-NN) with the hyphen written as an underscore.
	// StudyName is PascalCase so the stem is one valid C# identifier and does not include ".Strategy.CS".
	static readonly Regex ResearchStrategyFileRx = new Regex(@"^_[A-Z][A-Z0-9]*_[0-9]{2}_[A-Z][A-Za-z0-9]*\.Strategy\.CS$", RegexOptions.CultureInvariant);

	static readonly string[] IdFileMap = {
		"Catalogue ID A2-63 maps to file _A2_63_StudyName.Strategy.CS and class _A2_63_StudyName.",
		"Catalogue ID OK-02 maps to file _OK_02_StudyName.Strategy.CS and class _OK_02_StudyName.",
	};

	// null when the name is not a strategy file, or it matches the catalogue-id file rule.
	static string ResearchStrategyNameProblem(string fileName)
	{
		if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".Strategy.CS", StringComparison.Ordinal)) return null;
		if (ResearchStrategyFileRx.IsMatch(fileName)) return null;
		return fileName + " must be _<ID>_<StudyName>.Strategy.CS (catalogue ID with hyphens as underscores; class is the file stem)";
	}

	static List<string> PublicClasses(string text)
	{
		string code = Regex.Replace(text ?? "", @"//[^\n]*", "");
		var r = new List<string>();
		foreach (Match m in Regex.Matches(code, @"\bpublic\s+class\s+(_\w+)")) r.Add(m.Groups[1].Value);
		return r;
	}

	// null when the file is not a study, or its single public class matches the stem (or the allow-list).
	static string ClassStemProblem(string fileName, IList<string> classes, Dictionary<string, string> allow)
	{
		string stem = StudyStem(fileName);
		if (stem == null) return null;
		if (classes == null || classes.Count != 1) return fileName + " public class count " + (classes == null ? 0 : classes.Count);
		string cls = classes[0];
		if (string.Equals(cls, stem, StringComparison.Ordinal)) return null;
		string allowed;
		if (allow != null && allow.TryGetValue(fileName, out allowed) && string.Equals(cls, allowed, StringComparison.Ordinal)) return null;
		return fileName + " class " + cls + " != stem " + stem;
	}

	static bool InOrder(string text, string[] markers, out string why)
	{
		int at = -1;
		why = null;
		if (text == null) { why = "empty"; return false; }
		foreach (string m in markers)
		{
			int i = text.IndexOf(m, StringComparison.Ordinal);
			if (i < 0) { why = "missing " + m; return false; }
			if (i <= at) { why = "out of order " + m; return false; }
			at = i;
		}
		return true;
	}

	static void SandboxGuard(string root)
	{
		const string templateRel = "sandbox/ResearchStrategyHeader.template.txt";
		string template = Path.Combine(root, "sandbox", "ResearchStrategyHeader.template.txt");
		string doc = Path.Combine(root, "docs", "research-strategy-header.md");
		string sandbox = Path.Combine(root, "sandbox");

		List<string> projects = AllCsproj(root);
		var sandboxHits = new List<string>();
		var globHits = new List<string>();
		var defaultCompileHits = new List<string>();
		foreach (string proj in projects)
		{
			string text = File.ReadAllText(proj);
			if (IsProductionProject(proj) && EnablesDefaultCompile(text)) defaultCompileHits.Add(Path.GetFileName(proj));
			foreach (string inc in Includes(proj))
			{
				if (PointsAtSandbox(inc)) sandboxHits.Add(Path.GetFileName(proj) + " -> " + inc);
				if (IsProductionProject(proj) && HasGlob(inc)) globHits.Add(Path.GetFileName(proj) + " -> " + inc);
			}
		}
		foreach (string h in sandboxHits) Console.WriteLine("  sandbox compile: " + h);
		foreach (string h in globHits) Console.WriteLine("  production wildcard: " + h);
		foreach (string h in defaultCompileHits) Console.WriteLine("  EnableDefaultCompileItems: " + h);
		Check(projects.Count >= 2 && sandboxHits.Count == 0, "no csproj Compile Include points at sandbox (" + sandboxHits.Count + " hits in " + projects.Count + " projects)");
		Check(globHits.Count == 0 && defaultCompileHits.Count == 0, "PLStudies.csproj and PLStudiesPY.csproj stay on explicit file names (" + globHits.Count + " wildcards, " + defaultCompileHits.Count + " default-compile)");

		var rootNames = new HashSet<string>(Directory.GetFiles(root).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
		var sandboxStudies = Directory.Exists(sandbox)
			? Directory.GetFiles(sandbox).Select(Path.GetFileName).Where(f => StudyRx.IsMatch(f)).OrderBy(f => f, StringComparer.Ordinal).ToList()
			: new List<string>();
		var nameClash = sandboxStudies.Where(f => rootNames.Contains(f)).ToList();
		foreach (string n in nameClash) Console.WriteLine("  sandbox file also at repo root: " + n);
		Check(sandboxStudies.Count >= 1 && nameClash.Count == 0, "sandbox study file names are not also at the repo root (" + nameClash.Count + " clashes, " + sandboxStudies.Count + " sandbox studies)");

		var rootClasses = new HashSet<string>(StringComparer.Ordinal);
		foreach (string f in Directory.GetFiles(root, "*.CS"))
			foreach (string c in PublicClasses(File.ReadAllText(f))) rootClasses.Add(c);
		var stemProblems = new List<string>();
		var fileNameProblems = new List<string>();
		var classClash = new List<string>();
		var seenSandbox = new HashSet<string>(StringComparer.Ordinal);
		foreach (string name in sandboxStudies)
		{
			string nameProblem = ResearchStrategyNameProblem(name);
			if (nameProblem != null) fileNameProblems.Add(nameProblem);
			string problem = ClassStemProblem(name, PublicClasses(File.ReadAllText(Path.Combine(sandbox, name))), SandboxClassAllow);
			if (problem != null) stemProblems.Add(problem);
			foreach (string c in PublicClasses(File.ReadAllText(Path.Combine(sandbox, name))))
			{
				if (!seenSandbox.Add(c)) classClash.Add("duplicate sandbox class " + c);
				if (rootClasses.Contains(c)) classClash.Add("class also in a root study: " + c);
			}
		}
		foreach (string p in fileNameProblems) Console.WriteLine("  strategy file name: " + p);
		foreach (string p in stemProblems) Console.WriteLine("  class/stem: " + p);
		foreach (string p in classClash) Console.WriteLine("  class clash: " + p);
		Check(fileNameProblems.Count == 0, "sandbox strategy files are _<ID>_<StudyName>.Strategy.CS (" + fileNameProblems.Count + ")");
		Check(stemProblems.Count == 0, "sandbox study class matches the file stem, except the two recorded mismatches (" + stemProblems.Count + ")");
		Check(classClash.Count == 0, "sandbox study class names are unique and absent from root studies (" + classClash.Count + ")");

		string templateText = File.Exists(template) ? File.ReadAllText(template) : "";
		string docText = File.Exists(doc) ? File.ReadAllText(doc) : "";
		string templateWhy, docWhy;
		bool templateOrder = InOrder(templateText, HeaderSections, out templateWhy);
		bool docOrder = InOrder(docText, HeaderSections, out docWhy);
		Check(File.Exists(template) && templateOrder, "header template has the 22 sections in order" + (templateOrder ? "" : " (" + templateWhy + ")"));
		Check(File.Exists(doc) && docOrder, "header standard repeats the 22 sections in order" + (docOrder ? "" : " (" + docWhy + ")"));
		Check(templateText.Contains("Not applicable") && templateText.Contains("Not yet verified"), "header template tells the author to write Not applicable / Not yet verified");
		Check(templateText.Contains("win_rate * avg_win - loss_rate * avg_loss") && docText.Contains("win_rate * avg_win - loss_rate * avg_loss"), "header standard and template state the cost-adjusted expectancy formula");
		Check(docText.Contains(templateRel) && !StudyRx.IsMatch(Path.GetFileName(template)), "template path is documented and the template is not a study file name");
		bool mapOk = true;
		foreach (string sentence in IdFileMap)
			if (!templateText.Contains(sentence) || !docText.Contains(sentence)) mapOk = false;
		Check(mapOk && docText.Contains("Compile Include=\"sandbox\\_A2_63_StudyName.Strategy.CS\""), "header standard and template map hyphenated catalogue IDs onto the file name and the class stem");

		// self-test: the classifiers, not the repo
		Check(PointsAtSandbox(@"sandbox\_X.Strategy.CS") && PointsAtSandbox("sandbox/_X.Strategy.CS")
			&& PointsAtSandbox(@"..\sandbox\_X.Strategy.CS") && PointsAtSandbox(@".\sandbox\_X.Strategy.CS")
			&& !PointsAtSandbox("_SandboxHelper.Strategy.CS") && !PointsAtSandbox("_CustomSignal.Strategy.CS"),
			"self-test: a path segment named sandbox is an inclusion; a file name containing those letters is not");
		Check(HasGlob("*.CS") && HasGlob(@"sandbox\*.CS") && HasGlob(@"**\*.CS") && HasGlob("Generated\\?.cs") && !HasGlob("_CustomSignal.Strategy.CS"),
			"self-test: * and ? are wildcards");
		string orderWhy;
		Check(!InOrder("// ## 2. Provenance\n// ## 1. Identity", HeaderSections, out orderWhy) && orderWhy != null,
			"self-test: a header with Identity after Provenance fails (" + orderWhy + ")");
		var allow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "_Old.Strategy.CS", "_Legacy" } };
		Check(ClassStemProblem("_A.Strategy.CS", new[] { "_A" }, allow) == null
			&& ClassStemProblem("_A2_63_StudyName.Strategy.CS", new[] { "_A2_63_StudyName" }, allow) == null
			&& ClassStemProblem("_A2_63_StudyName.Strategy.CS", new[] { "_A2_63_StudyName.Strategy" }, allow) != null
			&& ClassStemProblem("_A2_63_StudyName.Strategy.CS", new[] { "A2_63_StudyName" }, allow) != null
			&& ClassStemProblem("_Old.Strategy.CS", new[] { "_Legacy" }, allow) == null
			&& ClassStemProblem("_Old.Strategy.CS", new[] { "_Old" }, allow) == null
			&& ClassStemProblem("_Old.Strategy.CS", new[] { "_Other" }, allow) != null
			&& ClassStemProblem("_A.Strategy.CS", new[] { "_A", "_B" }, allow) != null
			&& ClassStemProblem("notes.md", new[] { "_A" }, allow) == null,
			"self-test: class name is the file stem, not the .Strategy.CS suffix, and the allow-list still works");
		Check(ResearchStrategyNameProblem("_A2_63_StudyName.Strategy.CS") == null
			&& ResearchStrategyNameProblem("_OK_02_StudyName.Strategy.CS") == null
			&& ResearchStrategyNameProblem("_A2_49_MiddayBreakout.Strategy.CS") == null
			&& ResearchStrategyNameProblem("_L2_01_DomFade.Strategy.CS") == null
			&& ResearchStrategyNameProblem("_StudyName.Strategy.CS") != null
			&& ResearchStrategyNameProblem("_A2-63_StudyName.Strategy.CS") != null
			&& ResearchStrategyNameProblem("_A2_63_studyName.Strategy.CS") != null
			&& ResearchStrategyNameProblem("_A2_63_Study_Name.Strategy.CS") != null
			&& ResearchStrategyNameProblem("_a2_63_StudyName.Strategy.CS") != null
			&& ResearchStrategyNameProblem("ResearchStrategyHeader.template.txt") == null,
			"self-test: strategy file names use the catalogue ID with underscores, then a PascalCase study name");
	}

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

		// (6) research drafts in sandbox/ stay out of the production project (docs/research-strategy-header.md)
		SandboxGuard(root);

		Console.WriteLine($"RepoLintTests: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
