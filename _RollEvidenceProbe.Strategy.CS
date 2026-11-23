// ============================================================================================
// Probe T-R4 (_RollEvidenceProbe): the table-vs-data-splice evidence check (specs/sandbox-roll-floor-migration.md T-R4; owner
// ruling 2026-11-23: run it before #155 merges). Places no orders. Data1 only. Data through 2024-12-31 only.
//
// QUESTION. For every CME table roll date (the T-R2 table: RollTableFromProfile, contract years 2000-2035, ES/MES/NQ/MNQ digest
// fdd63bd8f179a8d4), does the dxFeed continuous series switch contract on the table date, on another day, or not visibly?
//
// WHAT IT LOGS (MCSignal*.csv, signal column _RollEvidenceProbe; the T-R4 lines are comma-free, so they are never quoted):
//   CFG ROLL T-R4 evidence on: dates(144) digest(fdd63bd8f179a8d4) sessions(+-5) standout(2.0) k(1.0) tick(0.25) name(@ES)
//   ROLL T-R4 table(2024-06-17) day(2024-06-13) rel(-2) start(2024-06-12 17:00 pts +12.25 ticks +49 atr 0.183)
//            maxBar(2024-06-13 07:30 pts -3.00 ticks -12 atr 0.045) atr20(66.90) largest(1)
//     One line per session from rel(-5) to rel(+5). start = the session's first bar (the Globex reopen): open minus the prior
//     bar's close. maxBar = the largest open-minus-prior-close of any LATER bar of that session, with its bar time. Points and
//     ticks are signed (+ = up); atr = |gap| / the Globex-day ATR(20) of the completed sessions before it (the T-R1 A; NaN
//     during warm-up). Times are the chart's time zone.
//   ROLL T-R4 verdict table(2024-06-17) aligned | data splice on yyyy-MM-dd | no visible splice
//            | largest(day start|bar time gap) ratio(x) tableStart(gap) confirmed(0|1|-) sessions(5 before 5 after)
//     Candidate per session = max(|start|, |maxBar|). aligned = the largest candidate is on the table date and is >= 2.0 x the
//     median of the other sessions' candidates; data splice on X = the largest is on day X and passes the same test; otherwise
//     no visible splice. confirmed = the T-R1 flag on the table date's start gap (gap > k x A). "(incomplete window)" = the
//     chart ended before 5 sessions after the date.
//   ROLL T-R4 summary aligned(n) dataSpliceOtherDay(n) noVisibleSplice(n) incomplete(n) ...   (on the last bar of the chart)
//   With TraceLevel Journal (the default here) the T-R1 lines print too: "ROLL table splice(...) confirmed(...)" and any
//   "ROLL off-table gap(...)" Warning, for a cross-check with the slot runs on 7ba7d37.
//
// CHART (one chart per symbol, four runs): Data1 = @ES, @MES, @NQ, @MNQ, 1 minute, the CME Globex 24h session template the
//   sandbox slots use. Format Instrument > Settings > Data Range From 2023-01-01 To 2024-12-31. ATR(20) is warm by early
//   February 2023, before the first window (2023-03-06). Inputs: TraceLevel = Journal (the default). IOG off (the study is
//   IOG-disabled). Import _RollDetector.Function.CS, _CustomSignal.Strategy.CS and this file from the PR head, then Compile.
//   Optional (the spec's full T-R4): @ES and @NQ 1 minute 2016-06-01 .. 2024-12-31, same settings.
//
// EXTRACT (Windows PowerShell 5.1, right after each run; change TR4_ES.txt to the run's root):
//   Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCSignal*.csv' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Select-Object -Last 1 | ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^Info,[^,]*,_RollEvidenceProbe,[^,]*,([0-9a-f]{8}),CFG ROLL T-R4 ' -CaseSensitive -Encoding UTF8 | Select-Object -Last 1 } | ForEach-Object { Select-String -LiteralPath $_.Path -Pattern ('^\w+,[^,]*,_RollEvidenceProbe,[^,]*,' + $_.Matches[0].Groups[1].Value + ',') -CaseSensitive -Encoding UTF8 | ForEach-Object -MemberName Line | Out-File -FilePath (Join-Path ([Environment]::GetFolderPath('Desktop')) 'TR4_ES.txt') -Encoding utf8 }
//
// READ: per symbol, the 8 verdict lines for 2023-03-13, 06-12, 09-11, 12-11, 2024-03-11, 06-17, 09-16, 12-16, and the summary.
//   For each: the verdict; the largest day and where it is (start = the Globex reopen; bar = inside the session, with its time);
//   its ticks and atr; the ratio; the table date's own start gap in ticks and atr. A largest maxBar at a data-release time
//   (07:30 or 09:00 CT) is news, not a splice: say so when it decides the verdict. Expected check: MES 2024-06-17 reads
//   "data splice on 2024-06-13" if the 06-13 gap Analyst saw is the vendor switch. No Error lines are expected.
// ============================================================================================
using PowerLanguage.Function;
using System;

namespace PowerLanguage.Strategy
{
	/// <summary>Probe T-R4: Table roll detection from the Data1 profile with the RollEvidence journal on. No orders. Run sheet above.</summary>
	[IOGMode(IOGMode.Disabled)]
	public class _RollEvidenceProbe : _CustomSignal
	{
		public _RollEvidenceProbe(object _ctx) : base(_ctx) { TraceLevel = ETraceLevel.Journal; }

		protected override void Create()
		{
			base.Create();
			RollDetection = ERollDetection.Table; RollTableFromProfile = true; RollEvidenceJournal = true;
		}

		protected override void StartCalc()
		{
			if (!isFuturesCategory(Bars.Info.Category)) RollDetection = ERollDetection.Off;   // futures only; Off prints the base Warning
			base.StartCalc();
		}

		protected override void CalcBar()
		{
			base.CalcBar();
			stepRollDetector();   // idempotent per bar (the base steps it in CalcBar too)
		}
	}
}
