# Sandbox slot results

`slot-results.csv` holds one row per sandbox signal per test window, run from a `_TestNN` slot (see `specs/sandbox-test-protocol.md` and the slot batches in `specs/sandbox-coding-plan.md`). Rows are produced from the signals' own logs (CS-1 journal, CS-3 trace), not typed by hand. Data through 2024-12-31 only.

## Columns

| Column | Meaning |
|---|---|
| slot | `_TestNN` slot the signal ran in |
| study | sandbox class copied into the slot (`sandbox/<study>.Strategy.CS`) |
| idea_id | catalogue ID (A2-25+A2-26 share one file) |
| instrument | Data1 symbol from the RUN line |
| window | `validation` (2023-01-01 to 2024-12-31) or `development` |
| from, to | first calculated bar (RUN line) and last bar (RUN-END line), dates |
| trades | positions from `MCJournal_<runId>.csv`: rows with the same entry time, tag and side are one trade (scale-outs merged) |
| net_pnl | sum over trades of (exit − entry) × direction × qty × big point value, minus $17.00 per contract round trip ($2.25 commission + $6.25 slippage per side, ES; overview §4). MC-side commission and slippage are 0 in the batch workspaces, so costs are applied once, here |
| profit_factor | gross winning net ÷ gross losing net; `inf` when there is no losing trade |
| max_dd | largest peak-to-trough fall of the closed-trade equity curve (net, ordered by exit time), dollars |
| win_rate | percent of trades with net > 0 |
| avg_trade | net_pnl ÷ trades |
| return_pct_50k | net_pnl ÷ $50,000 × 100 |
| error_count, warning_count | Error and Warning lines of that run in the trace log (expected multi-contract skip Errors included) |
| status | `pass`, `fail`, `blocked` or `by-design-0` (rules below) |
| notes | run id, header mismatches, expected-skip counts, failure reasons |
| commit | repo commit the slot files were copied from |

## Cost assumptions

- Costs are applied once, by the parser: $2.25 commission + $6.25 slippage per contract per side = **$17.00 per contract round trip** (ES; overview §4). The same model is used for every futures row in batch 1 (all @ES).
- MC-side commission and slippage are 0 in the batch workspaces. The parser checks the journal `commission` column and notes any non-zero row.
- Big point value comes from a root table: ES 50, NQ 20, MES 5, MNQ 2 (`@` prefix and outright month codes stripped; the journal `symbol` column is the fallback). A row with trades and no BPV fails.
- Cross-check: MC's own `profit` per journal row must equal (exit − entry) × direction × qty × BPV within $0.01. A real mismatch fails the row; a match is noted.
- Capital for `return_pct_50k` is $50,000.

## Status rules (Coder's judging rules, 2026-11-16)

- Each chart follows the file's `// Chart:` line (A2-04 is 1m + 30m Data2; A2-14 is 1m only). Where a section 8 or section 22 note disagrees, the Chart line is used and the row's notes say so.
- `fail` when the run has no RUN-END line, the RUN inputs do not show `TraceLevel=Journal`, or any Error line other than `skipped: multi-contract risk exceeds 1 percent` (optionally prefixed by its ID, e.g. `A2-04 skipped: ...`) appears. Those skip Errors are expected and counted in `error_count`, never failed.
- `fail` with "holdout breach, rerun" when any trace or journal row, or the RUN-END last bar, is dated 2025-01-01 or later. No metric is computed from such a run.
- `by-design-0` for journal-only or data-unavailable modules when the run has 0 trades and the journal carries their reason line: A2-31 and A2-34 (journal-only), A2-36 (no GEX/VIX series), and A2-29 (header: day-type module, no orders; it has no order objects).
- `blocked` for TS-04: CL/GC pit idea with no CL/GC data on the feed; the @ES run is a smoke test only. A slot whose run is blocked by missing data or inputs is `blocked`.
- OF-07 / A2-78 (`_Test6`, `_Test7`) are `fail` until the startTick Range.To StartCalc crash is rerun on the PR #142 fix.
- 0-trade slots are reviewed against their journal/trace skip lines and source, and the parser carries the verdict (`$ZeroReview`):
  - a clear code or setup defect is `fail`, with the defect in the notes;
  - missing data or inputs is `blocked`;
  - a legitimately rare signal with clean mechanics is `pass`, and the notes start with `rare-0` and say why.
  An unreviewed 0-trade slot is `fail` ("not yet reviewed").
- Otherwise `pass`. `pass` is mechanics only (the run completed cleanly, costs and BPV checked). It says nothing about edge; acceptance is the owner's decision.

## Batch 1 validation (@ES 1m, 2023-01-01 to 2024-12-31, main 770d14e)

29 rows (`_Test1`–`_Test29`; A2-25 and A2-26 share `_Test5`). Produced by `Parse-SlotResults.ps1` v2 on the MiniPC (`C:\Share\MultiCharts\SandboxBatch01`) from `%LOCALAPPDATA%\MCSignalLogs`. Parser v1 fixes: run symbol was taken from the first trace line (blank before INSTR), so every BPV lookup failed and every trading slot was failed on the MC-profit cross-check; that check now fails only on a real mismatch. The trace scan is now compiled C#.

## PR #144 reruns on 51bd1ae (2026-11-16)

Eight slots rerun on #144 head 51bd1ae (ATR carry-over over the quarterly roll, MaxBarsBack-49 ring lookback, Own-source re-apply, daily re-arm): `_Test2` A2-15, `_Test4` DV-02, `_Test5` A2-25+A2-26, `_Test19` A2-13, `_Test20` A2-14, `_Test24` A2-30, `_Test26` A2-32, `_Test28` A2-35. Parsed with `Parse-SlotResults.v3.ps1 -Since '2026-11-16 18:55' -Commit 51bd1ae`; evidence checks against Coder's expected lines are in each row's notes. The other 21 rows keep their earlier commit (de2378a or 770d14e); ten of them (`_Test1`, 3, 6, 7, 9, 10, 11, 12, 17, 22) are marked STALE because their study or host class changed in fdc5cf8 and need a rerun.

Status totals after this update: **20 pass, 4 fail (`_Test6`, `_Test7` pending PR #142; `_Test24` A2-30 holiday-dated balance lines; `_Test28` A2-35 Good Friday position held over the weekend), 4 by-design-0, 1 blocked**.

## PR #152 reruns (c068c60, A2-32 at 03686df; 2026-11-22)

Eleven slots rerun on PR #152 head c068c60 staged over main 2e1a397 GX files, with `_Test26` A2-32 replaced by 03686df (2 contracts, owner decision 2026-11-22): `_Test3` TS-04, `_Test10` PS-25, `_Test11` MT-07, `_Test15` A2-04, `_Test16` A2-09, `_Test17` A2-11, `_Test20` A2-14, `_Test22` A2-28, `_Test24` A2-30, `_Test26` A2-32, `_Test28` A2-35. Parsed with `Parse-SlotResults.ps1 -Since '2026-11-22 13:50'`, then judged against Coder's #152 retest list (items 3, 4, 5, flat-by 14:55 CT = 15:55 ET, re-entry) from the journals; evidence is in each row's notes. Commit column: `pr152 c068c60 / A2-32 03686df`.

New fails: `_Test10` PS-25 run incomplete (no RUN-END, stopped at 2023-09-08; per-bar DVP FIT/RAIL Journal lines); `_Test17` A2-11 and `_Test26` A2-32 each took one entry at 15:01 CT after the 14:55 CT flat-by; `_Test22` A2-28 and `_Test28` A2-35 each re-entered once after a fill-bar stop on the same session/event. `_Test24` A2-30 (holiday-dated lines) and `_Test28` A2-35 (Good Friday weekend hold) earlier fails are fixed. `_Test20` A2-14 is rare-0 at $50k (all 36 daily setups over the 1 pct budget on ES).

Status totals after this update: **17 pass, 7 fail (`_Test6`, `_Test7` pending PR #142; `_Test10`, `_Test17`, `_Test22`, `_Test26`, `_Test28` per above), 4 by-design-0, 1 blocked**.

## PR #152 reruns (fdc6a95, then 69fc801 for A2-28 / A2-04; 2026-11-22)

R152b set: batch-1 `_Test1`-`_Test29` staged from fdc6a95 (with `_SandboxRules` 030b2c79) over main 2e1a397 GX files, plus `_Test30` = LE-01 as a HoldOvernight spot-check. Compiled 16:17 ET and run 16:18-17:00 ET (MiniPC clock), with R152b workspaces 2023-01-01..2024-12-31 @ES 1m. `_Test22` A2-28 and `_Test15` A2-04 were rerun on 69fc801 at 17:04-17:07 ET. Every repeat-pass pair (`_Test28`, `_Test15`, `_Test16`, `_Test11`, `_Test10`, `_Test4`, `_Test30`) has identical signal logs and journals, and every run has RUN-END 2024-12-31. Nothing is dated 2025 or later, and all Errors are the expected multi-contract risk skips. Judged against Coder's fdc6a95 recheck list (cutoff 14:55 CT, exchange time CT) and the 69fc801 checks. Commit column: `pr152 fdc6a95` or `pr152 69fc801`.

Fixed: A2-11 (2023-01-25) and A2-32 (2023-09-08) no longer enter at 15:01 CT; each logs one 'entry blocked: at or after the flat-by cutoff' line, and no run has an entry at or after 14:55 CT. A2-28 (2023-10-17) and A2-35 (2024-11-07) no longer re-enter after a fill-bar stop, and no run re-enters after a same-day exit (A2-09 re-enters by catalogue design). PS-25 now runs to RUN-END. MT-07 reads the 60m Data2 DVP and trades in 2024 (17 of 36 trades). The log volume is down from 1.42M lines / 949 MB to 67k lines / 9 MB per run: DVP FIT/RAIL lines are 0, and the PA-DVP pivot/BOS lines that remain make up 99%. A2-09 has 0 overnight holds. A2-04 (69fc801) uses 2 lots, takes T1 on 1 lot at 1R, and the runner exits on the stop, on a 5m sVWAP close-below after 10:00 ET, or at the 11:30 ET flat. DV-02 entries are identical to the 51bd1ae baseline.

Still open: `_Test22` A2-28 (69fc801, journal identical to fdc6a95) still closes both lots together on all 64 trades with 0 T1 fills, so neither the void T1 nor the break-even after T1 has been exercised (fail). `_Test30` LE-01 has 1 closed trade (2023-10-10 to 2023-10-19, held overnight) and no entry after that, so it is pending a check for an open position at RUN-END.

Status totals after this update: **21 pass, 3 fail (`_Test6`, `_Test7` pending PR #142; `_Test22`), 4 by-design-0, 1 blocked, 1 pending (`_Test30` LE-01 spot-check)**.

## PR #152 rechecks on e03f6d1 (2026-11-22)

Set1 staged at 17:38 ET from e03f6d1: the batch-1 slots plus `_Test30` LE-01. The GX root files are e03f6d1 merged into main 2e1a397, because the #152 branch predates #142/#146/#151: `_CustomSignal` 068e151f, `_GenericExit` 6907986e, `_DvpEngine` 365836cd. F7 at 17:42:48 ET; runs 17:45-18:01 ET MiniPC clock (16:45-17:01 CT). Rerun: `_Test26` A2-32, `_Test30` LE-01, `_Test11` MT-07, `_Test10` PS-25. `_Test22` A2-28 was not run, because it is waiting for the rebased head with the new T1 placement. Commit column: `pr152 e03f6d1`.

GX regression, run first:
- D9a `_DvpEngineTest` (runId 3953b235) matches the baseline. Its events file has 182,503 lines, with stripped SHA-256 f7fc033abce6ef94, equal to 7fbe5bff / bcca8d35 / 1ff61444 / 650fe308.
  - configHash is 80cf8ab6ad9ba5cd, profileHash is 020deca78d6392b7 and RUN-END is bar 188581.
  - With runId and elapsedMs stripped, the DEGREE/D9A/CAL lines (457) and every other non-PA session-log line (420,356) are identical to the 2026-11-15 13:44 ET log (runId 650fe308).
  - The 21,478 PA-DVP pivot/BOS lines are gone at Journal level, as expected. The D9a RUN fits() stay numeric.
- G0 `_TestGate0` (runId 5a654011) still has 35,110 journal lines and 37,225 session-log lines, and the session-log lines are identical to the 2026-11-15 run.
  - The journal's stripped SHA-256 is 00faa50bc1230df6, not 8f6b25deff509c9f.
  - Only 2 rows differ, both in maeDollars/maeTicks: G0SE 2024-12-10 08:53 CT is 4 ticks deeper and G0LE 2024-12-30 09:41 CT is 9 ticks deeper. Entries, exits, P&L and MFE are unchanged.
  - That column is MC's own `pos.MaxDrawDown`, which e03f6d1 does not touch. December 2024 is the month whose @ES ticks were downloaded into QuoteManager earlier today, so this is most likely a data change, not a code change.

Results:
- **A2-32**: all 105 trades (135 rows) are identical to fdc6a95 run 5a6d9631; only gxPackage differs.
  - It now logs 39 GX START lines instead of 1, so the exit modes are re-read before each trade. They all resolve to the same modes, so no A2-32 trade changes.
- **MT-07**: this slot is changed by the fix. On fdc6a95 all 36 trades ran on the first trade's package (b22b158b, no target).
  - Now 30 trades get their own package. 22 of those exit on their own +2 sigma target, always before the old stop/time exit, and 13 of those 22 fill on the next bar.
  - Exits are now 22 target / 10 stop / 4 time; they were 26 stop / 10 time. The entries are unchanged.
- **PS-25**: trades are identical to fdc6a95; only gxPackage differs.
- **MT-07 and PS-25 logging**: 0 PA-DVP pivot/BOS lines at Journal level. The DVP RUN lines print fits(off) skips(off).
  - Logs are 497 lines / 0.08 MB (MT-07) and 794 lines / 0.16 MB (PS-25), down from about 67k lines / 9 MB.
- **LE-01**: resolved. The journal still has the single closed trade from 2023-10-10 to 2023-10-19.
  - The daily 'LE-01' lines show it flat from 2023-10-20 to 2023-11-03, entering on 2023-11-06, then 'up True pos 1' on every trading day through 2024-12-31.
  - So the second long is still open at RUN-END under the trend-break rule.
- **Cutoff and re-entry**: A2-32, MT-07 and PS-25 have 0 entries at or after 14:55 CT, 0 same-day re-entries and 0 overnight holds.
- **Errors**: the only Errors are A2-32's 26 expected multi-contract 1 percent risk skips. Nothing is dated 2025 or later.

Status totals after this update: **22 pass, 3 fail (`_Test6`, `_Test7` pending PR #142; `_Test22` pending the rebased #152 head), 4 by-design-0, 1 blocked, 0 pending**.

## PR #152 rechecks on a71c9a8 (2026-11-22)

Set1 staged at 18:39 ET straight from the a71c9a8 tip (rebased stack on main 2e1a397, fast-forward of 3c6e32c; no merge needed): the batch-1 slots plus `_Test30` LE-01, roots `_CustomSignal` 4e3bef96, `_GenericExit` 6907986e, `_DvpEngine` 365836cd, `_SandboxRules` 8f87cbbf and `_DailyContext` 73c23e95. F7 at 18:40:13 ET; runs 18:41-18:52 ET MiniPC clock (17:41-17:52 CT). Rerun: `_Test22` A2-28, `_Test10` PS-25, `_Test30` LE-01, `_Test11` MT-07 (workspace changed in-session to Data1 1m + Data2 15m + Data3 60m, as the new header requires).

Results:
- **A2-28** (now pass): 64 plan lines, 53 'source mid', 8 'source void', 3 'T1 none'.
  - Every 'source mid' T1 equals the entry/mu_major midpoint rounded to the tick (53 of 53, checked in ticks).
  - T1 fills on 26 of 64 trades (22 mid, 4 void); it was 0 of 64 on fdc6a95 / 69fc801.
  - After T1 the runner exits 10 at mu_major, 1 at flat-by and 15 on its stop: 14 at entry +/-1 tick (break-even), 1 four ticks through entry on the bar after T1 (gap fill).
- **PS-25**: all 201 trades identical to e03f6d1 (0 diffs in times, side, qty, exit order or fills); the new 'plan Data1 (1m)' line is present.
- **MT-07**: plan lines read 'Data2 (15m)'. 3 trades (was 36): 2 target, 1 stop.
  - Target exits on the bar after entry: 0 of 2 (was 13 of 22); median target distance 11.5 ticks (was 5.5).
  - 6 '+2 sigma target beyond the 60m next rail' skips, 48 '+2 sigma rail not above the entry', 20 risk-budget skips.
  - The time stop is 41 x the Data2 bar minutes (615 with 15m); it is not printed and no trade reached it (longest hold 74 min).
  - KD-05 (`_Test12`) inherits this code, but its header and B01v2 workspace still say Data2 = 60m.
- **LE-01**: the daily line now reads 'days N (ring R/253)'. N rises every day with no stall: 253 on 2023-12-22, 254 on 2023-12-26, 516 on 2024-12-31.
  - Same closed trade as before; 'pos 1' every day from 2023-11-07 to 2024-12-31, so the 2023-11-06 long is still open at RUN-END.
- **Early closes**: no entry, plan or order activity after the date-aware cutoff on 2023-07-03, 2023-11-24, 2024-07-03, 2024-11-29 or 2024-12-24 in any of the four runs; no activity at all on Good Friday 2023-04-07. None of these four studies enters in the evening session, so the evening-entry check needs the hold studies in Set2.
- **Errors**: only A2-28's 13 expected multi-contract 1 percent risk skips. Nothing is dated 2025 or later.

Status totals after this update: **23 pass, 2 fail (`_Test6`, `_Test7` pending PR #142), 4 by-design-0, 1 blocked, 0 pending**.
