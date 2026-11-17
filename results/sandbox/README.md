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
