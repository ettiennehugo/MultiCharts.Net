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
			if (r.rep.Observe(st) && drift)
			{
				DvpSnapshot snap = st.Snapshot(r.rig.ring, 0);
				if (snap != null && snap.refitBar && snap.valid && !snap.crossesRoll) r.rep.ObserveDrift(snap.endpointDrift);
			}
		}
		if (journal) r.sel = r.rig.journal.Where(l => l.StartsWith("DVP DEGREE sel", StringComparison.Ordinal)).ToList();
		return r;
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
		var w = new DvpDegreeReport(2); w.Withhold(new DateTime(2025, 1, 1, 18, 15, 0)); w.Withhold(new DateTime(2025, 1, 2, 9, 30, 0));
		Check(w.Withheld == 2 && w.FirstWithheld == new DateTime(2025, 1, 1, 18, 15, 0), "D9a report: withheld bars counted with the first one's time");
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
			&& host.Contains("From 2016-01-01") && host.Contains(@"%LOCALAPPDATA%\MCSignalLogs\"),
			"D9a host: no [Input], no Enum.Parse / reflection / StreamWriter / orders; holdout by ET time and Globex day; typed resolutions; run steps in the header");
		string rsrc = File.ReadAllText(Path.Combine(Root(), "_DvpDegreeReport.Function.CS"));
		Check(!rsrc.Contains("DvpConfig.") && !rsrc.Contains(".Canon(") && !rsrc.Contains("Enum.Parse") && rsrc.Contains("namespace PowerLanguage.Function"),
			"D9a report source: MC-free, no DvpConfig write or Canon() use (report-only)");
	}
}
