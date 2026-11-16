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

## Status rules

- `fail` when the run has no RUN-END line, the RUN inputs do not show `TraceLevel=Journal`, or any Error line other than `skipped: multi-contract risk exceeds 1 percent` (optionally prefixed by its ID, e.g. `A2-04 skipped: ...`) appears. Those skip Errors are expected and counted.
- `fail` with "holdout breach, rerun" when any trace or journal row, or the RUN-END last bar, is dated 2025-01-01 or later. No metric is computed from such a run.
- `by-design-0` for A2-31 and A2-34 (journal-only modules) and A2-36 (no GEX/VIX series) when the run has 0 trades and the journal carries their reason line.
- `blocked` for TS-04: CL/GC pit idea with no CL/GC data on the feed; the @ES run is a smoke test only.
- `fail` when an ordinary signal makes 0 trades (review its skip lines), otherwise `pass`.
- `pass` is mechanics only (the run completed cleanly and traded). It says nothing about edge; acceptance is the owner's decision.

Each chart follows the file's `// Chart:` line (Data1 1-minute, Data2/Data3 at the stated bar size, hidden). Where a section 8 or section 22 note disagrees with the Chart line, the Chart line is used and the row's notes say so.
