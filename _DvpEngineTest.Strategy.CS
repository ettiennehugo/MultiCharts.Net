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
	//      Format Instrument → Settings: session template "CME Globex" (18:00-17:00 ET); time zone Exchange (recommended);
	//      Build Volume On = Trade Volume (NOT Tick Count: the #81 host throws on a Tick Count futures stream).
	//      Data Range: From 2016-01-01 (Q21 DECIDED, Ettienne Hugo, 2026-11-01: the degree path depends on the first bar) To 2024-12-31
	//      (later data is harmless: it is withheld from DVP, see HOLDOUT).
	//   2. Insert → Instrument: @ES again as Data2 = 60 Minute, same session template, same time zone, same From date 2016-01-01,
	//      and again Format Instrument → Settings → Build Volume On = Trade Volume for Data2.
	//   3. Insert this strategy. Format Signals → _DvpEngineTest → Inputs: TraceLevel = Journal (the only input; inherited).
	//      Properties: Maximum number of bars study will reference = User defined, 50 (NOT Auto-detect: an auto-detect restart
	//      recalculates from the first bar, and a different value moves the first calculated bar, i.e. the start of the degree path,
	//      Q21). The setup line prints it (maxBarsBack, ExecInfo.MaxBarsBack) and flags any other value. Bar Magnifier off (no orders).
	//      Nothing else to set: RollDetection = Table (EventRules.RollDate, 2015-2025, ES quarterly rolls only: the setup line warns
	//      when the Data1 root is not ES) and every DvpConfig field are set in code.
	//   4. Let it calculate to the last bar (it writes the report on Bars.LastBarOnChart; if it is stopped early nothing is reported).
	//      The log is %LOCALAPPDATA%\MCSignalLogs\MCSignal<yyyyMMdd-HHmmss>-<pid>.csv (one file per MultiCharts process; the Output
	//      window prints "Log file created <path>"). Lines of this run carry its run id (the RUN line).
	//      Expected run time and log size (measured off-MultiCharts, see "RUN TIME AND LOG SIZE" below): DVP engine about 9 s
	//      at Journal, log about 238 MB / 262k lines (almost all DVP FIT lines); MultiCharts' own bar processing comes on top.
	//   5. Timing (DV-13.4, journal off): set TraceLevel = Info, recalculate (Ctrl+R), note the "DVP D9A timing" line (dvp(on)).
	//      Then in this file set BaselineDvpOff = true, compile, recalculate at TraceLevel = Info and note the dvp(off) timing line.
	//      Set BaselineDvpOff back to false afterwards. slowdown = elapsedMs(on) / elapsedMs(off) - 1 (budget <= 0.15, PROBE).
	//      These timing runs have DVP on Data1 AND Data2, while the DV-13.4 budget is stated for Data1 only, so the measured slowdown
	//      is conservative (an upper bound for the Data1-only case).
	//   SEND BACK (grep the log for the run id of step 4, plus the two timing lines of step 5):
	//     RUN, "DVP CREATED", "DVP D9A setup", every "DVP DEGREE" line (warming / sel / change), "DVP RUN", every "DVP CAL" line,
	//     every "DVP D9A" line (month + summary per stream), the two "DVP D9A timing" lines, and any Warning / Error line.
	//     The DVP FIT lines (about 262k at refitEvery 1, Data1 + Data2) are NOT needed in full; the summary's drift{} carries the endpointDrift
	//     distribution. Keep the file in case a FIT-level question comes up.
	//
	// RUN TIME AND LOG SIZE (PR-D9a review item f; how the header figures were obtained)
	//   Measured on a Linux build box (8-vCPU Intel Xeon, .NET 8 Release, 2026-11-01) with tests/Dvp/D9aBench.cs
	//   (DVP_BENCH=1 dotnet run -c Release --project tests/Dvp): the real engine with this run's DvpConfig (SelfCalibrate PROBE,
	//   refitEvery 1, journalFits on, streams Data1 + Data2) on a synthetic 15m Globex-like series of 210,000 bars (Data1, about
	//   2016-2024 of @ES 15m) and 52,000 bars (Data2, about 2016-2024 of 60m), five runs each:
	//     journal off                                     180-184 ms
	//     journal on, lines counted only                  7.5-7.7 s   (261,653 lines: 261,522 DVP FIT, 93 DVP DEGREE)
	//     journal on, CSV line + File.AppendAllText each  8.7-9.0 s   (file 237,591,032 bytes = about 238 MB, about 908 bytes per line)
	//   So at Journal the fits behind the FIT lines (one per closed bar per stream) and their formatting dominate; the per-line append
	//   added about 1.0-1.5 s on Linux. NOT measured: MultiCharts' own bar processing, the inherited _VolumeProfileFuturesBase work,
	//   .NET Framework on Windows instead of .NET 8, and Windows per-call file open/append/close cost (NTFS, antivirus), which adds
	//   about 262k x (cost per call). The line count is from the series size, so it holds for the real run within the bar count
	//   (the FIT count is about Data1 + Data2 closed bars, not 200k); the bytes use the real line layout with an 8-character run id.
	//
	// WHAT THE REPORT GIVES (per stream; "DVP D9A summary"): share of post-warm-up months whose online d equals the pooled d* and that
	//   year's d*, switches, flip-backs within 3 rechecks, margin-blocked rechecks, rechecks kept by a NaN score (counted apart), warm-up end, the distribution of the score gaps
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
		/// <summary>The run steps' fixed "Maximum number of bars study will reference" (User defined).</summary>
		const int RunMaxBarsBack = 50;
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
			int every = Math.Max(1, DvpConfig.refitEvery);       // = DvpStream's refit period (m_r)
			for (int i = 0; i < m_rep.Length; i++)
			{
				DvpStream st = e.Stream(m_rep[i].Ds);
				if (!m_rep[i].Observe(st) || !drift) continue;
				_BarRing ring = Ring(m_rep[i].Ds);
				// refit bar first (review item h): the same test DvpStream.compute makes (Mod(bar, refitEvery) == 0 => snap.refitBar), so
				// Snapshot is only asked on refit bars; DvpStream exposes no refit flag, hence the host-side check.
				if (ring == null || ring.Count == 0 || ring[0].bar % every != 0) continue;
				DvpSnapshot s = st.Snapshot(ring, 0);
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
			int mbb = ExecInfo.MaxBarsBack;                       // typed IExecInfo member (as in _MtfProbe / _VolumeProfileBase)
			bool mbbOk = mbb == RunMaxBarsBack;
			string root = _MtfMath.SymbolRoot(Bars.Info.Name);
			bool rootOk = root == "ES";                           // the RollDate table (EventRules.RollDate) is ES-only
			bool startOk = first >= new DateTime(2016, 1, 1) && first < new DateTime(2016, 1, 8);   // the first @ES bar of 2016 (Sun 2016-01-03 18:00 ET)
			trace(ETraceLevel.Journal, "DVP D9A setup runId(" + RunId + ") data1(" + r1.ChartType + " " + r1.Type + " " + r1.Size + " " + Bars.Info.Name + ") data2(" + d2
				+ ") firstBarEt(" + first.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + ") deviation(data1=" + (d1ok ? 0 : 1)
				+ " data2=" + (d2ok ? 0 : 1) + " chartStart=" + (startOk ? 0 : 1) + " maxBarsBack=" + (mbbOk ? 0 : 1) + " root=" + (rootOk ? 0 : 1) + ") maxBarsBack(" + mbb
				+ ") root(" + root + ") roll(" + RollDetection + " " + RollDates.Count + " dates " + RollFromYear + "-" + RollToYear
				+ ") holdoutFrom(" + DvpConst.HoldoutFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ") dvp(" + (DvpConfig.enabled ? "on" : "off") + ")");
			if (!d1ok || !d2ok || !startOk || !mbbOk)
				trace(ETraceLevel.Warning, "DVP D9A setup deviation: expected @ES Regular 15 Minute Data1, Regular 60 Minute Data2, chart start 2016-01-01, Max bars back "
					+ RunMaxBarsBack + " (see the header of _DvpEngineTest)");
			if (!rootOk)
				trace(ETraceLevel.Warning, "DVP D9A setup: symbol root " + root + " is not ES; RollDetection = Table uses EventRules.RollDate, which is the ES quarterly roll table only");
		}
	}
}
