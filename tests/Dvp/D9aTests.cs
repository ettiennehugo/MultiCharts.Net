using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PowerLanguage.Function;

// M-DVP PR-D9a (spec rev 6 §16 item 12, DV-R3): the report-only online-vs-offline degree comparison (DvpDegreeReport, linked
// whole from _DvpDegreeReport.Function.CS) and the _DvpEngineTest host source rules. Synthetic SelfCalibrate runs (CalSeries / SCfg
// of SelfCalibTests): the report must reproduce the "DVP DEGREE sel" path, DvpCalib.DStar of the pooled and per-year buckets,
// and change nothing in the engine's output.
static partial class Program
{
	static readonly Regex SelRx = new Regex(@"^DVP DEGREE sel stream\(Data1\) winLen\(\d+\) from\((\d)\) to\((\d)\) scores\[([^\]]*)\] n\((\d+)\)$");

	sealed class RepRun { public Rig rig; public DvpDegreeReport rep; public List<string> sel = new List<string>(); public List<int> tdYm = new List<int>(); }

	static RepRun RunRep(RingBar[] s, DvpConfig c, bool journal, bool observe, bool drift)
	{
		var r = new RepRun { rig = new Rig(c, true, journal), rep = new DvpDegreeReport(1) };
		long rc = 0;
		for (int i = 0; i < s.Length; i++)
		{
			r.rig.Feed(s[i], false);
			DvpStream st = r.rig.S;
			if (st.Self != null && st.Self.Rechecks != rc) { rc = st.Self.Rechecks; r.tdYm.Add(s[i].tradingDay.Year * 12 + s[i].tradingDay.Month - 1); }
			if (!observe) continue;
			if (r.rep.Observe(st) && drift && s[i].bar % Math.Max(1, c.refitEvery) == 0)   // the host's refit-bar check before Snapshot (item h)
			{
				DvpSnapshot snap = st.Snapshot(r.rig.ring, 0);
				if (snap != null && snap.refitBar && snap.valid && !snap.crossesRoll) r.rep.ObserveDrift(snap.endpointDrift);
			}
		}
		if (journal) r.sel = r.rig.journal.Where(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal)).ToList();
		return r;
	}

	// Two streams in one engine (Data1 = s1, Data2 = s2 every 4th Data1 bar), each with its own report. force = ask Snapshot on EVERY
	// bar (no refit-bar check, also with journalFits off) to prove the read is side-effect free. Returns journal + warnings + DVP RUN/CAL.
	static List<string> RunRep2(RingBar[] s1, RingBar[] s2, DvpConfig c, bool observe, bool force, out DvpDegreeReport[] reps)
	{
		var j = new List<string>(); var w = new List<string>();
		var e = new _DvpEngine(c, "t9a2", "ES", 0.25, true, false, j.Add, w.Add);
		var r1 = new _BarRing(DvpStream.RingNeed(c)); var r2 = new _BarRing(DvpStream.RingNeed(c));
		reps = new[] { new DvpDegreeReport(1), new DvpDegreeReport(2) };
		int k = 0;
		for (int i = 0; i < s1.Length; i++)
		{
			r1.Add(s1[i]); e.OnBar(1, r1, false); obs(e, 1, r1, reps[0], observe, force);
			if ((i & 3) == 3 && k < s2.Length) { r2.Add(s2[k++]); e.OnBar(2, r2, false); obs(e, 2, r2, reps[1], observe, force); }
		}
		e.Finish();
		j.AddRange(w.Select(x => "WARN " + x));
		return j;
	}

	static void obs(_DvpEngine e, int ds, _BarRing ring, DvpDegreeReport rep, bool observe, bool force)
	{
		if (!observe) return;
		DvpStream st = e.Stream(ds);
		if (!rep.Observe(st) && !force) return;
		if (!force && ring[0].bar % Math.Max(1, e.Config.refitEvery) != 0) return;
		DvpSnapshot snap = st.Snapshot(ring, 0);
		if (snap != null && snap.refitBar && snap.valid && !snap.crossesRoll) rep.ObserveDrift(snap.endpointDrift);
		if (force) e.Snapshot(ds, ring, 1);   // also the endAgo = 1 read a consumer would make intrabar
	}

	static double Pd(string x) { return double.Parse(x, CI); }

	static void D9aTests()
	{
		// a two-year series crossing a calendar year, so the per-year d* has two buckets; default PROBE margins (hysteresis active)
		RingBar[] s = CalSeries(420, 91, new[] { 700, 2100 }, null, new DateTime(2023, 3, 1, 18, 0, 0));
		DvpConfig c = SCfg(1, true); c.calibLen = 80; c.calibMinScored = 50; c.calibMarginAbs = 0.002; c.calibMarginRel = 0.01;
		RepRun r = RunRep(s, c, true, true, true);
		DvpSelfCalib sc = r.rig.S.Self; DvpCalib cal = r.rig.S.Calib; DvpDegreeReport rep = r.rep;

		// (1) one record per recheck, equal to the engine's DVP DEGREE sel lines and the recheck bar's tradingDay month
		bool same = rep.Count == sc.Rechecks && r.sel.Count == rep.Count && r.tdYm.Count == rep.Count && rep.Overflow == 0;
		for (int i = 0; same && i < rep.Count; i++)
		{
			Match m = SelRx.Match(r.sel[i]);
			if (!m.Success) { same = false; break; }
			string[] sco = m.Groups[3].Value.Split(',');
			double sf = Pd(sco[rep.From(i) - 1]), sb = rep.Candidate(i) >= 1 ? Pd(sco[rep.Candidate(i) - 1]) : double.NaN;
			double g = sf - sb;
			same = int.Parse(m.Groups[1].Value, CI) == rep.From(i) && int.Parse(m.Groups[2].Value, CI) == rep.To(i) && int.Parse(m.Groups[4].Value, CI) == rep.N(i)
				&& rep.YearMonth(i) == r.tdYm[i] && (g.Equals(rep.Gap(i)));
		}
		Check(same && rep.Count >= 12, "D9a report: one record per recheck = the DVP DEGREE sel path (from, to, n, gap from the R scores) on the tradingDay month (" + rep.Count + " rechecks)");

		// (2) offline d*: pooled and per-year from DvpCalib.DStar over the run's buckets (the DVP CAL table)
		DvpDegreeReport.Summary sum = rep.Compute(cal);
		bool yrs = cal.Years.Count >= 2 && sum.yearD.Count == cal.Years.Count;
		foreach (var kv in cal.Years) yrs &= sum.yearD[kv.Key] == DvpCalib.DStar(kv.Value);
		Check(sum.pooledD == DvpCalib.DStar(cal.Pooled()) && sum.pooledD >= 1 && yrs, "D9a report: pooled d* (" + sum.pooledD + ") and per-year d* (" + sum.yearD.Count + " years) = DvpCalib.DStar of the DVP CAL buckets");

		// (3) the month shares, switches, warm-up end and margin bookkeeping, recomputed by hand from the sel lines
		int post = 0, mp = 0, ym = 0, my = 0, sw = 0, warm = 0, blocked = 0; int firstPost = -1; bool rule = true;
		for (int i = 0; i < rep.Count; i++)
		{
			if (rep.WarmingAfter(i)) { warm++; rule &= rep.To(i) == 1 && !rep.Changed(i); continue; }
			if (firstPost < 0) firstPost = i;
			post++; if (rep.To(i) == sum.pooledD) mp++;
			int yd; if (sum.yearD.TryGetValue(rep.YearMonth(i) / 12, out yd)) { ym++; if (rep.To(i) == yd) my++; }
			if (rep.Changed(i)) { sw++; rule &= rep.Gap(i) >= rep.Need(i) && rep.To(i) == rep.Candidate(i); }
			if (rep.Blocked(i)) { blocked++; rule &= rep.Gap(i) < rep.Need(i) && rep.To(i) == rep.From(i); }
		}
		Check(sum.postWarm == post && sum.matchPooled == mp && sum.yearMonths == ym && sum.matchYear == my && sum.switches == sw && sw == sc.Changes
			&& sum.warmMonths == warm && warm >= 1 && post >= 6 && sum.warmEndYm == rep.YearMonth(firstPost) && sum.blocked == blocked && rule,
			"D9a report: post-warm months " + post + ", matchPooled " + mp + ", matchYear " + my + "/" + ym + ", switches " + sw + " (= engine Changes), blocked " + blocked + ", warm-up months " + warm);
		Check(sw >= 1, "D9a report: the synthetic run switches at least once (" + sw + ")");
		// the gap distribution is over the eligible rechecks; every gap >= 0 (the candidate is the argmin); gapCand excludes candidate == current
		Check(sum.gapAll.Length >= post && sum.gapAll.All(x => x >= 0) && sum.gapCand.Length <= sum.gapAll.Length && sum.gapCand.All(x => x >= 0)
			&& sum.gapAll.SequenceEqual(sum.gapAll.OrderBy(x => x)), "D9a report: score gaps at rechecks are >= 0 and sorted (" + sum.gapAll.Length + " eligible, " + sum.gapCand.Length + " with another candidate)");

		// (4) report-only: observing (and reading the cached refit snapshot) changes nothing in the engine's journal or end-of-run lines
		RepRun plain = RunRep(s, c, true, false, false);
		r.rig.e.Finish(); plain.rig.e.Finish();
		Check(r.rig.journal.SequenceEqual(plain.rig.journal) && r.rig.warn.SequenceEqual(plain.rig.warn) && r.rig.journal.Count > 1000,
			"D9a report-only: the engine journal (" + r.rig.journal.Count + " lines incl. DVP RUN / CAL) is identical with and without the report");
		// (4b) review item k: the same with refitEvery 4 (Snapshot on non-refit bars reads a past anchor) and with journalFits off (no FIT
		// line, so no snapshot is otherwise computed), Snapshot forced on every bar, and a second stream (Data2) in the same engine
		RingBar[] s2 = CalSeries(110, 92, new[] { 500 }, null, new DateTime(2023, 3, 1, 18, 0, 0));
		foreach (var v in new[] { new { every = 4, fits = true }, new { every = 1, fits = false }, new { every = 4, fits = false } })
		{
			DvpConfig cv = SCfg(v.every, v.fits); cv.calibLen = 80; cv.calibMinScored = 50; cv.streams = new[] { 1, 2 };
			DvpDegreeReport[] ra, rb;
			List<string> withRep = RunRep2(s, s2, cv, true, true, out ra), noRep = RunRep2(s, s2, cv, false, false, out rb);
			Check(withRep.SequenceEqual(noRep) && withRep.Count > 30 && withRep.Any(l => l.Contains("stream(Data2)")) && ra[0].Count > 0 && ra[1].Count > 0,
				"D9a report-only (item k): refitEvery " + v.every + ", journalFits " + (v.fits ? "on" : "off") + ", Snapshot forced every bar, Data1 + Data2: the engine output ("
				+ withRep.Count + " lines) is identical with and without the reports (" + ra[0].Count + "/" + ra[1].Count + " rechecks)");
		}
		Check(rep.DriftCount > 100 && Enumerable.Range(0, DvpDegreeReport.DriftEdges.Length + 1).Sum(b => rep.DriftBin(b)) == rep.DriftCount,
			"D9a report: |endpointDrift| histogram over " + rep.DriftCount + " refit snapshots");

		// (5) the lines: one month line per recheck, one summary line with the counts above
		var lines = new List<string>();
		rep.Lines(lines.Add, "t9a", r.rig.S);
		string last = lines.Count > 0 ? lines[lines.Count - 1] : "";
		Check(lines.Count == rep.Count + 1 && lines.Take(rep.Count).All(l => l.StartsWith("DVP D9A month runId(t9a) stream(Data1) ym(", StringComparison.Ordinal))
			&& last.StartsWith("DVP D9A summary runId(t9a) stream(Data1) winLen(30) ", StringComparison.Ordinal)
			&& last.Contains(" matchPooled(" + mp + "/" + post + " ") && last.Contains(" switches(" + sw + ") engineChanges(" + sc.Changes + ")")
			&& last.Contains(" pooledDStar(" + sum.pooledD + ") ") && last.Contains(" reportOnly(1)") && last.Contains("probe(calibLen=80 calibMinScored=50 "),
			"D9a report lines: " + rep.Count + " month lines + one summary");

		// (6) no allocation per bar in Observe (journal off: the engine step allocates nothing either, DV-T37)
		DvpConfig c0 = SCfg(); c0.calibLen = 60; c0.calibMinScored = 20;
		var rig0 = new Rig(c0, true, false); var rep0 = new DvpDegreeReport(1); long bytes = 0;
		for (int i = 0; i < s.Length; i++)
		{
			rig0.Feed(s[i], false);
			long b0 = GC.GetAllocatedBytesForCurrentThread(); rep0.Observe(rig0.S); rep0.Observe(rig0.S); bytes += GC.GetAllocatedBytesForCurrentThread() - b0;
		}
		Check(bytes == 0 && rep0.Count == rig0.S.Self.Rechecks && rep0.Count > 0, "D9a report: Observe allocates nothing per bar and repeat calls are no-ops (" + bytes + " bytes, " + rep0.Count + " rechecks)");
		// journal off: no per-year buckets, so the offline d* is 0 (not comparable) and no share is claimed
		DvpDegreeReport.Summary s0 = rep0.Compute(rig0.S.Calib);
		Check(rig0.S.Calib.Years.Count == 0 && s0.pooledD == 0 && s0.matchPooled == 0 && s0.yearMonths == 0, "D9a report: without a journal there is no offline table (pooled d* 0, no match counted)");

		// (7) capacity, withheld bars, non-SelfCalibrate streams, quantiles and drift bins
		var small = new DvpDegreeReport(1, 3); var rig1 = new Rig(c0, true, false);
		foreach (RingBar b in s) { rig1.Feed(b, false); small.Observe(rig1.S); }
		Check(small.Count == 3 && small.Overflow == rig1.S.Self.Rechecks - 3, "D9a report: rechecks past the capacity are counted in overflow, not recorded");
		var w = new DvpDegreeReport(2); w.Withhold(new DateTime(2025, 1, 1, 18, 15, 0)); w.Withhold(new DateTime(2025, 1, 1, 18, 15, 0));
		w.Withhold(new DateTime(2025, 1, 2, 9, 30, 0)); w.Withhold(new DateTime(2025, 1, 2, 9, 30, 0)); w.Withhold(new DateTime(2025, 1, 2, 9, 30, 0));
		Check(w.Withheld == 2 && w.FirstWithheld == new DateTime(2025, 1, 1, 18, 15, 0), "D9a report: withheld bars counted once per bar time (repeat calls deduped), with the first one's time");
		// item j: an eligible recheck whose gap is NaN (a NaN score) is a NaN recheck, not a margin block
		var nr = new DvpDegreeReport(1);
		nr.Record(2023 * 12, 1, 1, 2, 60, false, false, true, 0.5, 0.4, 0.01);              // blocked by the margin
		nr.Record(2023 * 12 + 1, 1, 1, 2, 60, false, false, true, double.NaN, 0.4, double.NaN);   // NaN score of the current d
		nr.Record(2023 * 12 + 2, 1, 1, 0, 60, false, false, true, 0.5, double.NaN, 0.01);     // no finite candidate
		nr.Record(2023 * 12 + 3, 1, 1, 2, 10, false, false, false, double.NaN, 0.4, double.NaN);  // not eligible: neither
		nr.Record(2023 * 12 + 4, 1, 2, 2, 60, false, true, true, 0.5, 0.1, 0.01);             // a switch
		DvpDegreeReport.Summary ns = nr.Compute(null);
		var nl = new List<string>(); rep.Lines(nl.Add, "n", r.rig.S);
		Check(nr.Blocked(0) && !nr.Blocked(1) && !nr.Blocked(2) && nr.NanRecheck(1) && nr.NanRecheck(2) && !nr.NanRecheck(0) && !nr.NanRecheck(3) && !nr.NanRecheck(4)
			&& ns.blocked == 1 && ns.nanRechecks == 2 && ns.switches == 1 && nl[nl.Count - 1].Contains(") nanRechecks(" + sum.nanRechecks + ")") && nl[0].Contains(") nan(0) sFrom("),
			"D9a report: NaN rechecks are counted apart from margin blocks (blocked 1, nanRechecks 2)");
		DvpConfig cf = SCfg(); cf.degMode = EDvpDegMode.Fixed1; var rigF = new Rig(cf, true, true); foreach (RingBar b in s.Take(400)) rigF.Feed(b, false);
		var lf = new List<string>(); new DvpDegreeReport(1).Lines(lf.Add, "f", rigF.S);
		Check(lf.Count == 1 && lf[0] == "DVP D9A skipped runId(f) stream(Data1) reason(degMode Fixed1 is not SelfCalibrate)", "D9a report: a non-SelfCalibrate stream writes one skipped line");
		double[] q = { 1, 2, 3, 4, 5 };
		Check(DvpDegreeReport.Quantile(q, 0) == 1 && DvpDegreeReport.Quantile(q, 0.5) == 3 && DvpDegreeReport.Quantile(q, 0.9) == 4.6 && DvpDegreeReport.Quantile(q, 1) == 5
			&& double.IsNaN(DvpDegreeReport.Quantile(new double[0], 0.5)), "D9a report: linear-interpolated quantiles");
		var d = new DvpDegreeReport(1);
		foreach (double x in new[] { 0.0, -0.05, 0.07, 0.3, -0.6, 1.5, 2.0, -9, double.NaN, double.PositiveInfinity }) d.ObserveDrift(x);
		Check(d.DriftCount == 8 && d.DriftBin(0) == 1 && d.DriftBin(1) == 2 && d.DriftBin(2) == 0 && d.DriftBin(3) == 1 && d.DriftBin(4) == 1 && d.DriftBin(5) == 1 && d.DriftBin(6) == 2,
			"D9a report: |drift| bins at the edges 0.05/0.1/0.25/0.5/1/2 (edge values go up), NaN and infinity ignored");

		// (8) host source rules (_DvpEngineTest.Strategy.CS)
		string host = File.ReadAllText(Path.Combine(Root(), "_DvpEngineTest.Strategy.CS"));
		string code = string.Join("\n", host.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
		Check(!code.Contains("[Input]") && !code.Contains("Enum.Parse") && !code.Contains("GetType(") && !code.Contains("StreamWriter") && !code.Contains("Send(")
			&& code.Contains("protected override bool dvpFeed(int ds, DateTime barTime)") && code.Contains("ToEastern(barTime) < DvpConst.HoldoutFrom && GlobexDate(barTime) < DvpConst.HoldoutFrom")
			&& code.Contains("base.dvpFinish();") && code.Contains("DvpConfig.degMode = EDvpDegMode.SelfCalibrate;") && code.Contains("RollDetection = ERollDetection.Table;")
			&& code.Contains("r1.Type == EResolution.Minute && r1.Size == 15") && code.Contains("r2.Type == EResolution.Minute && r2.Size == 60")
			&& host.Contains("From 2016-01-01") && host.Contains(@"%LOCALAPPDATA%\MCSignalLogs\")
			&& Regex.Matches(host, "Build Volume On = Trade Volume").Count == 2 && host.Contains("Q21 DECIDED") && !host.Contains("Q6")
			&& code.Contains("int mbb = ExecInfo.MaxBarsBack;") && code.Contains("const int RunMaxBarsBack = 50;") && host.Contains("User defined, 50")
			&& code.Contains("ring[0].bar % every != 0) continue;") && code.IndexOf("ring[0].bar % every") < code.IndexOf("st.Snapshot(ring, 0)")
			&& code.Contains("_MtfMath.SymbolRoot(Bars.Info.Name)") && code.Contains("root == \"ES\"") && host.Contains("Data1 AND Data2") && !host.Contains("@@"),
			"D9a host: no [Input], no Enum.Parse / reflection / StreamWriter / orders; holdout by ET time and Globex day; typed resolutions and ExecInfo.MaxBarsBack; refit bar checked before Snapshot; ES-root warning; run steps (Trade Volume x2, Max bars back 50, run time) in the header");
		string rsrc = File.ReadAllText(Path.Combine(Root(), "_DvpDegreeReport.Function.CS"));
		// (9) SEND BACK extract: the PowerShell Select-String pattern copied verbatim in the header, applied (same .NET Regex engine,
		// case-sensitive) to lines in LogFileSingleton's exact layout built from the real engine / report / host messages.
		Match pm = Regex.Match(host, @"-Pattern '(.*?)' -CaseSensitive -Encoding UTF8 \| ForEach-Object Line \| Out-File");
		var ex = new Regex(pm.Success ? pm.Groups[1].Value : "^$");
		Func<string, string, string> csv = (lvl, msg) => lvl + ",2020-06-15 10:30:00 Exchange,_DvpEngineTest,@ES,3f9c2a1b,"
			+ (msg.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + msg.Replace("\"", "\"\"") + "\"" : msg);
		var jr = new List<string>(); var wr = new List<string>();
		var cr = SCfg(); cr.streams = new[] { 1 }; cr.journalFits = true; cr.journalRails = true;
		var er = new _DvpEngine(cr, "t9ax", "ES", 0.25, true, false, jr.Add, wr.Add);
		var rr = new _BarRing(DvpStream.RingNeed(cr)); var repx = new DvpDegreeReport(1);
		foreach (RingBar b in CalSeries(420, 93, new[] { 700 }, null, new DateTime(2023, 3, 1, 18, 0, 0)))
		{
			rr.Add(b); er.OnBar(1, rr, false); repx.Observe(er.Stream(1));
			DvpStream sx = er.Stream(1); DvpSnapshot x = er.Snapshot(1, rr, 1); DvpRails rl = x != null ? er.RailsOf(1, rr, 1) : null;
			DvpConfluence cfx = rl != null && rr.Count > 1 ? er.ConfluenceOf(1, x, rl, null, rr[1].c) : null;
			sx.RailJournal.Step(sx, rr, x, rl, cfx, false, sx.SessionHalfDay, jr.Add, er.RunId);
		}
		er.Finish(); repx.Lines(jr.Add, er.RunId, er.Stream(1));
		string[] want = { "DVP CREATED ", "DVP DEGREE ", "DVP RUN ", "DVP CAL ", "DVP D9A ", "DVP RAIL " };
		bool allKinds = want.All(p => jr.Any(l => l.StartsWith(p, StringComparison.Ordinal)));
		bool kept = jr.Where(l => want.Any(p => l.StartsWith(p, StringComparison.Ordinal))).All(l => ex.IsMatch(csv("Journal", l)));
		bool fitOut = jr.Where(l => l.StartsWith("DVP FIT ", StringComparison.Ordinal)).All(l => !ex.IsMatch(csv("Journal", l))) && jr.Any(l => l.StartsWith("DVP FIT ", StringComparison.Ordinal));
		bool hostKept = new[] { "RUN id(1a2b3c4d) build(x) strategy(_DvpEngineTest) symbol(@ES) inputs[TraceLevel=Journal; X=1,2]", "RUN-END id(1a2b3c4d) lastBar(2024-12-31 16:45:00)",
			"DVP D9A setup runId(1a2b3c4d) data1(Regular Minute 15 @ES)", "DVP D9A timing runId(1a2b3c4d) dvp(on)" }.All(m => ex.IsMatch(csv("Info", m)))
			&& ex.IsMatch(csv("Warning", "anything, quoted")) && ex.IsMatch(csv("Error", "x")) && !ex.IsMatch(csv("Info", "DVP skip degChange stream(Data1)"))
			&& !ex.IsMatch(csv("Journal", "DVP FIT runId(1a2b3c4d)")) && !ex.IsMatch(csv("Info", "DVP RUNNER x"));
		bool a233 = ex.IsMatch(csv("Journal", DvpA233.Line("1a2b3c4d", new DateTime(2020, 6, 15), false, 1, 2, 0, 0)));
		Check(pm.Success && allKinds && kept && fitOut && hostKept && !a233
			&& host.Contains("Don't open the CSV in Excel or an editor while it runs (a lock drops lines)") && host.Contains("expect a few minutes")
			&& host.Contains("send dvp_d9a_extract.csv; keep the full CSV") && host.Contains("set TraceLevel = Journal; leave every other input at its"),
			"D9a SEND BACK: the verbatim extract pattern keeps DVP CREATED/DEGREE/RUN/CAL/D9A/RAIL, RUN/RUN-END, setup/timing, Warning/Error lines (quoted or not) and drops DVP FIT; "
			+ "it does NOT match the \"DVP A2-33\" prefix (alternative 'A2 ' needs a space after A2; no A2-33 lines in this run: LevelStudy is off)");
		// (10) setup check at every TraceLevel, line at Info, Warnings also to the Output window
		int ls = code.IndexOf("void logSetup()"), le = code.IndexOf("void setupWarning(");
		string setup = ls >= 0 && le > ls ? code.Substring(ls, le - ls) : "";
		Check(setup.Length > 0 && !setup.Contains("journalOn()") && setup.Contains("trace(ETraceLevel.Info, \"DVP D9A setup") && !setup.Contains("trace(ETraceLevel.Warning")
			&& Regex.Matches(setup, "setupWarning\\(").Count == 2 && code.Contains("trace(ETraceLevel.Warning, msg);") && code.Contains("Output.WriteLine(\"Warning: \" + msg)")
			&& host.Contains("Changing RunMaxBarsBack requires changing this startOk window") && host.Contains("about Tue 2016-01-05 evening"),
			"D9a host setup check: runs at every TraceLevel (setup line at Info), both Warnings to the log and the Output window; first-calculated-bar note (RunMaxBarsBack <-> startOk / Q21)");
		Check(!rsrc.Contains("DvpConfig.") && !rsrc.Contains(".Canon(") && !rsrc.Contains("Enum.Parse") && rsrc.Contains("namespace PowerLanguage.Function"),
			"D9a report source: MC-free, no DvpConfig write or Canon() use (report-only)");
	}
}
