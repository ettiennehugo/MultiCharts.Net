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

		Console.WriteLine($"RepoLintTests: {s_pass} passed, {s_fail} failed");
		return s_fail == 0 ? 0 : 1;
	}
}
