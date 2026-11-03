// ============================================================================================
// Probe P2 (_MtfProbe, DV-9.3, missing-functionality-specs.md §3 P2, VPB-8 acceptance). D10 blocker. Places no orders.
// RUN SHEET (supersedes live-check-sheet.md Check 6 steps 1-4: 4 runs instead of 5 + roll day).
//
// 1. RUN MATRIX: 4 runs. R1, R2 any time (historical). R3, R4 need live market hours.
//   R1 backtest, IOG off, no Bar Magnifier ......... any time. Answers (b), (c), (d), (e), and (a) at Data1 bar close.
//   R2 backtest, IOG on, Bar Magnifier on ........... any time. Answers (a) intrabar on history, (c) at magnifier resolution.
//   R3 live, IOG off ................................ market hours. Answers (c), (d) in real time.
//   R4 live, IOG on ................................. market hours. Answers (a) intrabar in real time, (b), (c) in real time.
//   R3 and R4 are one chart session: R3 across one hh:00 boundary, then switch IOG on (MC recalculates, new runId), R4 across
//   the next hh:00. About 80 minutes of Globex time (Sun 18:00 to Fri 17:00 ET, not the daily 17:00-18:00 ET halt).
//   Best window: 09:50-11:10 ET (busy ticks, 10:00 and 11:00 boundaries). Any other hh:00 inside Globex hours also works.
//   Dropped, with reasons:
//   - backtest, IOG off, Bar Magnifier on: the magnifier only changes fills; the probe places no orders, so it logs what R1 logs.
//   - backtest, IOG on, no Bar Magnifier: history is calculated once per bar (the Regime.IogHistoryNoMagnifier case), so the
//     calls are the same as R1's. R1 shows the bar-close view and R2 the intrabar one; this run would add nothing.
//   - playback: it replays stored ticks as if they were real time. R3/R4 use the real feed, which is the case that matters.
//     Use playback only as a stand-in for R3/R4 when live hours are not available (same settings, name the file P2_PB3/P2_PB4).
//   - live IOG on cannot replace R3: with IOG on every tick is a calc, so PG p54 "last calc before the Data1 Open" (d) only
//     exists with IOG off, and the live close order of two series (each closes on its own next tick) can differ from backtest.
//
// 2. DATA RANGE (Format Instrument > Settings > Data Range, From/To, same on Data1 and Data2; week of 8 Jul 2024: no roll,
//   no holiday). The probe writes one line per CalcBar; with MaxBarsBack 20 the first calc waits for 20 Data2 60-min bars.
//   R1: From 2024-07-07 To 2024-07-10 (3 sessions, about 3,000-4,000 lines, three RTH 10:00 bars; the first calc is about
//       Mon 8 Jul 14:00-15:00 ET if MaxBarsBack is per series, about Sun 7 Jul 18:20 ET if not, which is (e)).
//   R2: From 2024-07-09 To 2024-07-09 with Max bars back 5 (covers the 10:00 boundary). Lines = magnifier steps: 15 seconds
//       gives about 4 per minute (VERIFY your version offers Seconds; if only Tick, use 1 Tick: the log is then large, one
//       line per trade, and the R2 extract keeps only 09:40-10:19).
//   R3/R4: live chart, Data Range = 2 days back (enough history for 20 60-min bars), then about 20 live minutes per run.
//
// 3. CHART SETUP (all runs).
//   Data1 = @ES 1 minute. Data2 = @ES 60 minute (Insert > Instrument, VERIFY menu name in your MC version). Same root is
//   required (startMtf throws on a different root). Both on the 24h CME Globex session template, chart time zone as usual
//   (the log's 2nd column names it; "10:00" below means 10:00 in that zone).
//   Why 60 and not 15 minutes: (b) is worded on the 10:00 60-min bar; MT-07 trades the 60-min stream; a 60-bar forming window
//   shows (a) clearly (H/L widen over 60 Data1 calls); and 20 Data2 bars back is 20 hours, which stresses (e) far more than
//   15-min (5 hours).
//   Build Volume On = Trade Volume on BOTH Data1 and Data2 (Format Instrument > Settings). The #81 check throws only in
//   _VolumeProfileBase.StartCalc; _MtfProbe derives from _CustomSignal so it will not throw here, but set it anyway so the
//   chart matches the VP strategies (v= in the log is Data2 Volume[0] as MC builds it).
//   Max bars back: User specified 20 (R2: 5). Not Auto-detect: Auto-detect restarts the calc on the deep d2 read and hides (e).
//   Inputs: TraceLevel = Info (the only input). Nothing else to set.
//   Realtime-History Matching (RHM) MUST be ON (MC wiki: Format Signals > Signals tab > Properties > Backtesting > Realtime-History
//   Matching checkbox; on by default; VERIFY the exact tab in your MC version). MC .NET has no member to read it, so the probe
//   cannot log it: check it by hand. Data1 and Data2 must use the SAME session template; startMtf logs a Warning
//   "MTF Data2 session template differs from Data1" when Bars.Sessions differ (MC-1439: with RHM on and different session ends
//   the strategy stalls until Data2 prints), or "MTF Data2 session check unavailable" when either Sessions list is null or empty
//   (VERIFY: whether Sessions is populated at StartCalc). The RHM-required line is logged at Info.
//   IOG: Format Signals > Properties > Enable Intra-Bar Order Generation (VERIFY tab name). Bar Magnifier: Format Signals >
//   Properties > Backtesting > Use Bar Magnifier (VERIFY). The "P2 start" line logs iog(True|False) to confirm the setting.
//   Import order: _MtfMath.Function.CS, then _MtfProbe.Strategy.CS, then Compile.
//
// 4. EXTRACT (Windows PowerShell 5.1; paste the run's line as is, right after the run and before starting the next). Each line takes
//   the newest MCSignal*.csv (MCSignalLogs, else %TEMP%), the runId of its last "P2 start", and writes that run's lines to the
//   Desktop: every non-call line (RUN, P2 start, Warning/Error) plus the P2 call lines; R2 keeps only calls 09:40-10:19 on 9 Jul.
//   R1  Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCSignal*.csv' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Select-Object -Last 1 | ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^Info,[^,]*,_MtfProbe,[^,]*,([0-9a-f]{8}),"?P2 start ' -CaseSensitive -Encoding UTF8 | Select-Object -Last 1 } | ForEach-Object { Select-String -LiteralPath $_.Path -Pattern ('^\w+,[^,]*,_MtfProbe,[^,]*,' + $_.Matches[0].Groups[1].Value + ',(?!"?P2 call)|,' + $_.Matches[0].Groups[1].Value + ',"?P2 call.*d1\[') -CaseSensitive -Encoding UTF8 | ForEach-Object -MemberName Line | Out-File -FilePath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'P2_R1.txt') -Encoding utf8 }
//   R2  Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCSignal*.csv' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Select-Object -Last 1 | ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^Info,[^,]*,_MtfProbe,[^,]*,([0-9a-f]{8}),"?P2 start ' -CaseSensitive -Encoding UTF8 | Select-Object -Last 1 } | ForEach-Object { Select-String -LiteralPath $_.Path -Pattern ('^\w+,[^,]*,_MtfProbe,[^,]*,' + $_.Matches[0].Groups[1].Value + ',(?!"?P2 call)|,' + $_.Matches[0].Groups[1].Value + ',"?P2 call.*d1\[2024-07-09 (09:[45]|10:[01])') -CaseSensitive -Encoding UTF8 | ForEach-Object -MemberName Line | Out-File -FilePath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'P2_R2.txt') -Encoding utf8 }
//   R3  Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCSignal*.csv' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Select-Object -Last 1 | ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^Info,[^,]*,_MtfProbe,[^,]*,([0-9a-f]{8}),"?P2 start ' -CaseSensitive -Encoding UTF8 | Select-Object -Last 1 } | ForEach-Object { Select-String -LiteralPath $_.Path -Pattern ('^\w+,[^,]*,_MtfProbe,[^,]*,' + $_.Matches[0].Groups[1].Value + ',(?!"?P2 call)|,' + $_.Matches[0].Groups[1].Value + ',"?P2 call.*d1\[') -CaseSensitive -Encoding UTF8 | ForEach-Object -MemberName Line | Out-File -FilePath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'P2_R3.txt') -Encoding utf8 }
//   R4  Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCSignal*.csv' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Select-Object -Last 1 | ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^Info,[^,]*,_MtfProbe,[^,]*,([0-9a-f]{8}),"?P2 start ' -CaseSensitive -Encoding UTF8 | Select-Object -Last 1 } | ForEach-Object { Select-String -LiteralPath $_.Path -Pattern ('^\w+,[^,]*,_MtfProbe,[^,]*,' + $_.Matches[0].Groups[1].Value + ',(?!"?P2 call)|,' + $_.Matches[0].Groups[1].Value + ',"?P2 call.*d1\[') -CaseSensitive -Encoding UTF8 | ForEach-Object -MemberName Line | Out-File -FilePath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'P2_R4.txt') -Encoding utf8 }
//   Result: Desktop\P2_R1.txt .. P2_R4.txt. Send those four files.
//   Line format (tests/MtfTests pins it; numbers are culture-invariant, so the message is never quoted):
//   Info,<time> <zone>,_MtfProbe,@ES,<runId>,P2 start iog(False) maxBarsBack(20) d1(@ES Minute 1) d2(@ES Minute 60) d2.Close[MaxBarsBack]=5590.5 d1cb(1201) d2cb(21)
//   Info,<time> <zone>,_MtfProbe,@ES,<runId>,P2 call(n) same(k) d1[2024-07-09 10:00:00 Close c=5600.25 cb=1440] d2[2024-07-09 10:00:00 Close o= h= l= c= v= cb=40]
//     closedIdx(0) closed[2024-07-09 10:00:00 c=5600.25] mapped(0 2024-07-09 10:00:00) realTime(False)     (live: realTime(True wall=10:00:00.153))
//   closedIdx = HtfClosedIndex(2) under the P2 closed rule: Close, or None with d2 Time <= d1 Time -> 0; Open/Inside, or d2 Time
//   after d1 Time -> 1. P2 results (runs 8356ba60 05c8e4d9 c003d107 aa78186a 63e2d28d): missing-functionality-specs.md §3.
//   same(k) = k-th call on the same Data1 bar (CurrentBar); d2 o/h/l/c/v = Data2 bar 0 as MC shows it on that call.
//
// 5. PASS (what Analyst reads). Times are chart times; "hh:00" is any 60-min boundary.
//   (a) R1, calls stamped between boundaries (e.g. d1 10:17): record which of three d2 shows. FORMING = d2 time 11:00, status not
//       Close, d2 c = d1 c, h/l only widen through the hour. LOOK-AHEAD = d2 time 11:00 with c/h/l already equal to the 11:00
//       call's values. CLOSED-ONLY = d2 time 10:00 Close. Any one of them, the same on every hour, answers (a). R2 (d1 status
//       Inside lines) and R4 (realTime(True) lines) must show FORMING with d2 status not Close. Safety PASS in every run:
//       closed[...] is never later than d1 time and stays on the previous boundary (10:00 on every call from 10:01 to 10:59).
//   (b) R1: the call(s) with d1[... 10:00:00 Close] show d2[... 10:00:00 Close], closedIdx(0), closed[... 10:00:00] and
//       mapped(0 ... 10:00:00) = END stamps (the 10:00 60-min bar is visible on the 10:00 1-min bar). closed[... 09:00:00] or
//       d2 time 09:00 on that call = start stamps / one bar late. R4 must agree on its realTime(True) hh:00 lines.
//   (c) every run: list the calls with d1 time hh:00:00. One call (same(1)) with both Close = MC calculates once for shared
//       closes. Two or more: record which stream reads Close first (by k). PASS = one pattern at every hh:00 of the run; R1 vs
//       R3 equal, or the difference is recorded (real time can close Data2 only on its next tick, i.e. on a later d1 bar).
//   (d) R1 and R3 (iog(False)): per Data1 bar take max same(k). All same(1): no Data2-only calcs on 1m+60m, so every calc is
//       the last one before the next Data1 Open and p54 holds trivially. Any same(2+): the highest k must show d1 Close and, at
//       hh:00, d2 Close and closed[hh:00]; earlier k showing a part view is what p54 overrides. LIMIT: the probe sends no
//       orders, so it shows the state of each calc, not that MC discards orders from earlier calcs.
//   (e) R1 "P2 start": d2.Close[MaxBarsBack]= a number, d2cb >= 20, and d1cb far above 20 (the first calc waited for Data2) =
//       MaxBarsBack applies per series. d2.Close[MaxBarsBack]=<SomeException> = it does not; HTF code then needs its own
//       Data2 CurrentBar guard. Same reading: early calls with closedIdx(-1) closed[none] (the closed Data2 bar was not readable;
//       the probe logs that instead of throwing). R3's start line (history part) must agree.
// ============================================================================================
using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>Probe P2: one Info line per CalcBar call with both streams' Time/Status/values, the HtfClosed index and the mapped
	/// HTF bar; a start line with IOG, MaxBarsBack and the deep Data2 read. Run sheet above.</summary>
	public class _MtfProbe : _CustomSignal
	{
		private int m_calls, m_same, m_lastD1Bar;

		public _MtfProbe(object _ctx) : base(_ctx) { }

		protected override void StartCalc()
		{
			HtfStreams = new[] { 2 };
			base.StartCalc();
			m_calls = 0;
			m_same = 0;
			m_lastD1Bar = -1;
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			if (!traceOn(ETraceLevel.Info)) return;
			IInstrument d1 = Bars, d2 = BarsOfData(2);
			if (m_calls++ == 0)
			{
				string deep;
				try { deep = _MtfProbeLog.Num(d2.Close[ExecInfo.MaxBarsBack]); } catch (Exception e) { deep = "<" + e.GetType().Name + ">"; }
				trace(ETraceLevel.Info, _MtfProbeLog.Start(Environment.IOGEnabled, ExecInfo.MaxBarsBack, d1.Info.Name + " " + d1.Info.Resolution.Type + " " + d1.Info.Resolution.Size,
					d2.Info.Name + " " + d2.Info.Resolution.Type + " " + d2.Info.Resolution.Size, deep, d1.CurrentBar, d2.CurrentBar));
			}
			m_same = d1.CurrentBar == m_lastD1Bar ? m_same + 1 : 1;
			m_lastD1Bar = d1.CurrentBar;
			int ci = HtfClosedIndex(2), mi = -1;
			DateTime ct = DateTime.MinValue, mt = DateTime.MinValue;
			double cc = 0;
			try { ct = d2.Time[ci]; cc = d2.Close[ci]; } catch (Exception) { ci = -1; }
			try { mi = htfMapped(2); if (mi >= 0) mt = d2.Time[mi]; } catch (Exception) { mi = -1; }
			trace(ETraceLevel.Info, _MtfProbeLog.Call(m_calls, m_same, d1.Time[0], d1.Status.ToString(), d1.Close[0], d1.CurrentBar,
				d2.Time[0], d2.Status.ToString(), d2.Open[0], d2.High[0], d2.Low[0], d2.Close[0], d2.Volume[0], d2.CurrentBar,
				ci, ct, cc, mi, mt, Environment.IsRealTimeCalc, DateTime.Now));
		}
	}
}
