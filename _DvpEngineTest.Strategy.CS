using System;
using System.Diagnostics;
using PowerLanguage.Function;

namespace PowerLanguage.Strategy
{
	// ================================================================================================================
	// _DvpEngineTest — M-DVP host smoke test (DV-R3) + the PR-D9a self-calibration validation (specs/m-dvp-spec.md rev 6, §16 item 12)
	// ================================================================================================================
	//
	// WHAT IT IS
	//   A measurement strategy, not a trading idea: it never sends an order. It runs the DVP engine (DvpConfig, set in Create(), no
	//   [Input]) on Data1 and Data2 with degMode = SelfCalibrate (rev 6 DV-3.10, PROBE defaults calibLen 250, calibMinScored 60,
	//   calibMarginAbs 0.01, calibMarginRel 0.02, calibStep 60 = projBars; Q17 DECIDED) and, at the last bar, writes a REPORT-ONLY
	//   comparison of each stream's online degree path with the offline pooled d* and per-year d* of the same run (DvpDegreeReport,
	//   "DVP D9A" lines). No table, no gate, no freeze, no Canon() change: the only host hooks are _CustomSignal.dvpFeed / dvpFinish.
	//   HOLDOUT: dvpFeed withholds every closed bar whose US-Eastern bar time OR Globex trading day is on/after 2025-01-01
	//   (DvpConst.HoldoutFrom), so no 2025+ bar ever reaches a DVP stream even when the chart loads more data (counted in withheld()).
	//
	// HELPER FILES TO IMPORT (PowerLanguage .NET Editor → File → Import, in this order, then compile all)
	//   _EventCalendar.Function.CS  (EventRules.RollDate for RollDetection = Table; on main, skip if already imported)
	//   _DvpEngine.Function.CS      (PR-D9 head; the DVP engine)
	//   _DvpDegreeReport.Function.CS (new: the report-only online-vs-offline comparison)
	//   _CustomSignal.Strategy.CS   (PR-D9a: dvpFeed / dvpFinish hooks)
	//   _DvpEngineTest.Strategy.CS  (this file)
	//   then File → Compile all (_VolumeProfileBase / _VolumeProfileFuturesBase read DvpConfig and must be rebuilt).
	//
	// MULTICHARTS RUN STEPS (exact)
	//   1. New chart: symbol @ES (CME E-mini S&P 500 continuous, dxFeed, NOT back-adjusted), Data1 = 15 Minute, chart type Regular.
	//      Format Instrument → Settings: session template "CME Globex" (18:00-17:00 ET); time zone Exchange (recommended).
	//      Data Range: From 2016-01-01 (DECIDED, Ettienne Hugo, 2026-11-01: the degree path depends on the first bar) To 2024-12-31
	//      (later data is harmless: it is withheld from DVP, see HOLDOUT).
	//   2. Insert → Instrument: @ES again as Data2 = 60 Minute, same session template, same time zone, same From date 2016-01-01.
	//   3. Insert this strategy. Format Signals → _DvpEngineTest → Inputs: TraceLevel = Journal (the only input; inherited).
	//      Properties: Max bars back = default; Bar Magnifier off (no orders). Nothing else to set: RollDetection = Table
	//      (EventRules.RollDate, 2015-2025) and every DvpConfig field are set in code.
	//   4. Let it calculate to the last bar (it writes the report on Bars.LastBarOnChart; if it is stopped early nothing is reported).
	//      The log is %LOCALAPPDATA%\MCSignalLogs\MCSignal<yyyyMMdd-HHmmss>-<pid>.csv (one file per MultiCharts process; the Output
	//      window prints "Log file created <path>"). Lines of this run carry its run id (the RUN line).
	//   5. Timing (DV-13.4, journal off): set TraceLevel = Info, recalculate (Ctrl+R), note the "DVP D9A timing" line (dvp(on)).
	//      Then in this file set BaselineDvpOff = true, compile, recalculate at TraceLevel = Info and note the dvp(off) timing line.
	//      Set BaselineDvpOff back to false afterwards. slowdown = elapsedMs(on) / elapsedMs(off) - 1 (budget <= 0.15, PROBE).
	//   SEND BACK (grep the log for the run id of step 4, plus the two timing lines of step 5):
	//     RUN, "DVP CREATED", "DVP D9A setup", every "DVP DEGREE" line (warming / sel / change), "DVP RUN", every "DVP CAL" line,
	//     every "DVP D9A" line (month + summary per stream), the two "DVP D9A timing" lines, and any Warning / Error line.
	//     The DVP FIT lines (about 200k at refitEvery 1) are NOT needed in full; the summary's drift{} carries the endpointDrift
	//     distribution. Keep the file in case a FIT-level question comes up.
	//
	// WHAT THE REPORT GIVES (per stream; "DVP D9A summary"): share of post-warm-up months whose online d equals the pooled d* and that
	//   year's d*, switches, flip-backs within 3 rechecks, margin-blocked rechecks, warm-up end, the distribution of the score gaps
	//   score(current) - score(candidate) at rechecks (all eligible rechecks; those with a different candidate; and relative to
	//   score(current)), skipShare, mean cov_in / cov_fwd, |endpointDrift| distribution, the withheld-bar count and the PROBE values.
	//   "DVP D9A month" lines give the path itself (one per monthly recheck). Whether the PROBE defaults look right (Q17) is read
	//   from these numbers by a person; the code decides nothing.
	// ================================================================================================================
	public class _DvpEngineTest : _VolumeProfileFuturesBase
	{
		/// <summary>DV-13.4 baseline build only: true = the DVP engine is not created (timing run "dvp(off)"). Default false.</summary>
		static readonly bool BaselineDvpOff = false;
		static readonly int[] DvpStreams = { 1, 2 };
		const int RollFromYear = 2015, RollToYear = 2025;

		DvpDegreeReport[] m_rep;
		Stopwatch m_watch;
		long m_chartBars;
		bool m_setupLogged;

		public _DvpEngineTest(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			base.Create();
			DvpConfig.enabled = !BaselineDvpOff;
			DvpConfig.streams = (int[])DvpStreams.Clone();
			DvpConfig.degMode = EDvpDegMode.SelfCalibrate;   // rev 6 default, set explicitly
			DvpConfig.journalFits = true;                    // DV-R3: journal on (FIT lines carry cov_in / endpointDrift)
		}

		protected override void StartCalc()
		{
			RollDetection = ERollDetection.Table;   // before base.StartCalc: the DVP engine reads it there (skipShare needs roll detection)
			if (RollDates.Count == 0)
				for (int y = RollFromYear; y <= RollToYear; y++)
					for (int m = 3; m <= 12; m += 3) RollDates.Add(EventRules.RollDate(y, m));
			base.StartCalc();
			m_rep = null;
			if (DvpConfig.enabled)
			{
				m_rep = new DvpDegreeReport[DvpConfig.streams.Length];
				for (int i = 0; i < m_rep.Length; i++) m_rep[i] = new DvpDegreeReport(DvpConfig.streams[i]);
			}
			m_chartBars = 0;
			m_setupLogged = false;
			m_watch = Stopwatch.StartNew();
		}

		protected override void CalcBar()
		{
			if (Bars.Status == EBarState.Close) m_chartBars++;   // before base.CalcBar: RUN-END (dvpFinish) runs inside it on the last bar
			if (!m_setupLogged) { m_setupLogged = true; logSetup(); }
			base.CalcBar();
			observe();
		}

		/// <summary>PR-D9a holdout: feed only bars before HOLDOUT_FROM by both the ET bar time and the Globex trading day.</summary>
		protected override bool dvpFeed(int ds, DateTime barTime)
		{
			if (ToEastern(barTime) < DvpConst.HoldoutFrom && GlobexDate(barTime) < DvpConst.HoldoutFrom) return true;
			DvpDegreeReport r = report(ds);
			if (r != null) r.Withhold(barTime);
			return false;
		}

		/// <summary>The report lines first (Journal), then the timing line (Info), then the engine's DVP RUN / DVP CAL lines (base).</summary>
		protected override void dvpFinish()
		{
			observe();   // the last bar's recheck, if any (dvpFinish runs inside base.CalcBar, before this CalcBar's observe())
			if (m_rep != null && journalOn())
			{
				_DvpEngine e = Dvp;
				Action<string> j = engineSink(ETraceLevel.Journal);
				for (int i = 0; i < m_rep.Length; i++) m_rep[i].Lines(j, RunId, e != null ? e.Stream(m_rep[i].Ds) : null);
			}
			if (traceOn(ETraceLevel.Info))
				trace(ETraceLevel.Info, "DVP D9A timing runId(" + RunId + ") dvp(" + (DvpConfig.enabled ? "on" : "off") + ") journal(" + (journalOn() ? "on" : "off")
					+ ") elapsedMs(" + (m_watch != null ? m_watch.ElapsedMilliseconds : -1) + ") chartBars(" + m_chartBars + ")");
			base.dvpFinish();
		}

		DvpDegreeReport report(int ds)
		{
			if (m_rep == null) return null;
			for (int i = 0; i < m_rep.Length; i++) if (m_rep[i].Ds == ds) return m_rep[i];
			return null;
		}

		/// <summary>After the engine step: record rechecks; with the FIT journal on, read the (already cached) refit snapshot's endpointDrift.</summary>
		void observe()
		{
			if (m_rep == null) return;
			_DvpEngine e = Dvp;
			if (e == null) return;
			bool drift = journalOn() && DvpConfig.journalFits;   // only then the window-end snapshot is computed anyway (no extra fit)
			for (int i = 0; i < m_rep.Length; i++)
			{
				DvpStream st = e.Stream(m_rep[i].Ds);
				if (!m_rep[i].Observe(st) || !drift) continue;
				DvpSnapshot s = st.Snapshot(Ring(m_rep[i].Ds), 0);
				if (s != null && s.refitBar && s.valid && !s.crossesRoll) m_rep[i].ObserveDrift(s.endpointDrift);
			}
		}

		/// <summary>One setup line on the first bar: resolutions (typed Resolution members), first bar, chart-start and data-stream checks.</summary>
		void logSetup()
		{
			if (!journalOn()) return;
			Resolution r1 = Bars.Info.Resolution;
			bool d1ok = r1.Type == EResolution.Minute && r1.Size == 15 && r1.ChartType == ChartType.Regular;
			string d2 = "missing"; bool d2ok = false;
			if (IsExist(2))
			{
				Resolution r2 = BarsOfData(2).Info.Resolution;
				d2ok = r2.Type == EResolution.Minute && r2.Size == 60 && r2.ChartType == ChartType.Regular;
				d2 = r2.ChartType + " " + r2.Type + " " + r2.Size + " " + BarsOfData(2).Info.Name;
			}
			DateTime first = ToEastern(Bars.Time[0]);
			bool startOk = first >= new DateTime(2016, 1, 1) && first < new DateTime(2016, 1, 8);   // the first @ES bar of 2016 (Sun 2016-01-03 18:00 ET)
			trace(ETraceLevel.Journal, "DVP D9A setup runId(" + RunId + ") data1(" + r1.ChartType + " " + r1.Type + " " + r1.Size + " " + Bars.Info.Name + ") data2(" + d2
				+ ") firstBarEt(" + first.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + ") deviation(data1=" + (d1ok ? 0 : 1)
				+ " data2=" + (d2ok ? 0 : 1) + " chartStart=" + (startOk ? 0 : 1) + ") roll(" + RollDetection + " " + RollDates.Count + " dates " + RollFromYear + "-" + RollToYear
				+ ") holdoutFrom(" + DvpConst.HoldoutFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ") dvp(" + (DvpConfig.enabled ? "on" : "off") + ")");
			if (!d1ok || !d2ok || !startOk)
				trace(ETraceLevel.Warning, "DVP D9A setup deviation: expected @ES Regular 15 Minute Data1, Regular 60 Minute Data2, chart start 2016-01-01 (see the header of _DvpEngineTest)");
		}
	}
}
