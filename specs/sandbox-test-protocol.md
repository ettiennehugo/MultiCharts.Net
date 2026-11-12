# Sandbox signal test protocol (`_TestNN` slots)

**Rev 1.0 (draft), Thursday 12 Nov 2026, America/New_York (EST).** Prepared by Analyst (Grok Bot) on branch `feat/test-slots-30`.

**Requested by Ettienne, 2026-11-12 07:36 ET (verbatim):** *"I'll want to systematically test all those signals by moving their code to one of the _Test** signals. Test them thoroughly and document the results. We can create more _Test** signals, say 30 so we can test larger batches of signals at a time."*
Earlier the same morning Ettienne said: test and development strategies live in `sandbox/` and are not in `PLStudies.csproj`; concrete strategies go in the studies folder.

**Status:** documentation and inert slots only. No signal has been run, validated or promoted by this document. Every result table below starts at "not run".

**Holdout (applies to every step).** Non-order-flow strategies use data **up to 31 Dec 2024 only** (programme overview `specs/strategies/00-programme-overview.md` §5.1). Never view, screenshot, export or report prices or results on or after 2025-01-01. Every chart and Portfolio Trader run gets a **dated** From/To range with To ≤ 2024-12-31 **before** OK is clicked. A journal row dated 2025-01-01 or later invalidates the run: delete the output, do not read it, and note "holdout breach, rerun" in the register.

**Backtesting only.** No Forward Testing, no Automate Order Execution, no broker connection for any slot.

---

## 1. Why slots

`sandbox/` files are not compiled by `PLStudies.csproj` (`docs/research-strategy-header.md`, "How the project includes studies"; enforced by `tests/RepoLintTests`). The PowerLanguage Editor subfolder import is unverified. The `_TestNN` slots are permanent root studies that **are** in `PLStudies.csproj`, so a sandbox signal can be compiled and run by pasting its code into a free slot, without adding a sandbox path or a root copy to the project.

The sandbox file stays the source of truth. A slot only ever holds a byte-for-byte copy of a sandbox body with the class renamed. Fixes found during a test are made in the sandbox file (on a feature branch), then re-copied into the slot.

## 2. Slot inventory (origin/main `7a27b21` + this branch)

| Slot | Base class | Current content | In `PLStudies.csproj` | May be overwritten? |
|---|---|---|---|---|
| `_Test1` | `_VolumeProfileFuturesBase` | Scaffold with no orders: `OrderFlowEnabled = true`, `FlowCfg.McBidAsk = Prefer`, `MinNodeTicks = 3`, `McBidAskChartType = FootPrint` (commit `7015acd`, "Changes to test check 9a"). Named as the Gate-0/KD batch host in the catalogue and `specs/keller-b5-p6p7-entries-analyst2.md` | yes | **No: reserved** (owner to release) |
| `_Test1.Indicator.CS` | indicator | not a slot; left alone | yes | No |
| `_Test2` | `_CustomSignal` | Copy of `sandbox/_A2_69_KaufmanThreeDay` (code-identical; only class name, BOM, whitespace differ) | yes | Yes, after its result is recorded |
| `_Test3` | `_CustomSignal` | Copy of `sandbox/_TS_02_EuropeanOpenDrift` (TS-01 rules). **Public class is `_Test03`, not `_Test3`** | yes | Yes, after its result is recorded. Fix the class name when it is next loaded |
| `_Test4` | `_CustomSignal` | Copy of `sandbox/_A2_49_MiddayBreakout` | yes | Yes, after its result is recorded |
| `_Test5` | `_CustomSignal` | Copy of `sandbox/_SW_02_SwingPullback` | yes | Yes, after its result is recorded |
| `_Test6` | `_CustomSignal` | **A2-63 host + pre-registered RegimeGate harness** (`specs/regime-consumer-prereg.md`, `_RegimeGate.Function.CS`, REGGATE journal, hash pins; named throughout the engines manual) | yes | **Never** |
| `_Test7` | `_VolumeProfileFuturesBase` | Copy of `sandbox/_AV_08_PeriodAvwapLayer` | yes | Yes, after its result is recorded |
| `_Test8` | `_VolumeProfileFuturesBase` | Copy of `sandbox/_VW_05_LateDayVwap` | yes | Yes, after its result is recorded |
| `_Test9` | `_CustomSignal` | Copy of `sandbox/_A2_56_FirstHourBreak` | yes | Yes, after its result is recorded |
| `_Test10` | `_VolumeProfileFuturesBase` | Copy of `sandbox/_A2_70_OpenFadeHvGate` (class `_Ex17OpenFadeHvGate`, A2-64 gated by A2-70) | yes | Yes, after its result is recorded |
| `_Test11`–`_Test30` | `_CustomSignal` | **Inert** (this branch): no orders, no `[Input]` besides the inherited `TraceLevel` | yes (this branch) | Yes |

Slots 2–5 and 7–10 were filled by Ettienne in `c862ad2` (7 Nov 2026) from the same sandbox sources. No results for them are recorded in the repo, so they are "loaded, not run". They can be tested where they are (batch 1, §7) instead of being moved.

Other root `_Test*` strategies (`_TestGate0`, `_TestPortArm`, `_TestPortThrottle`, `_TestPortThrottleMM`) are purpose-built harnesses, not slots. Do not use them.

## 3. Slot workflow

### 3.1 Load a signal into a slot

1. Pick a free slot from the slot register (§3.4). Never `_Test1` or `_Test6`.
2. Record the source: `git rev-parse --short HEAD` (commit) and `git rev-parse --short HEAD:sandbox/<file>` (blob) of the branch the sandbox file is read from.
3. Copy the sandbox file over `_TestNN.Strategy.CS`, then rename **only**:
   - the public class (`public class _A2_49_MiddayBreakout` → `public class _TestNN`);
   - the constructor (`public _A2_49_MiddayBreakout(object _ctx)` → `public _TestNN(object _ctx)`);
   - any other self-reference to the class name in code (for example a `typeof(...)` or an exception text). Comments may keep the original name.
   Keep `namespace PowerLanguage.Strategy`, the base class, the `using` lines, every `[SameAsSymbol]`/`[IOGMode]`/other class attribute, the order names and the journal tags unchanged. Order names and tags must stay the catalogue ones so the journal reads as the signal, not as the slot.
4. Diff check: the slot and the sandbox file must differ only in those renamed identifiers (and BOM/whitespace). On the box: `diff <(sed -E 's/\b_TestNN\b/CLS/g' _TestNN.Strategy.CS) <(sed -E 's/\b<SandboxClass>\b/CLS/g' sandbox/<file>)`.
5. On the MiniPC the studies folder `C:\ProgramData\TS Support\MultiCharts .NET64\StudyServer\Techniques\CS` is a git clone. Copy the prepared slot file over the existing slot file there (stage it in `C:\Share\MultiCharts\<Batch>\` first). Do not checkout, reset, stash, clean or pull in that clone.
6. Fill in the slot register row: slot, signal ID, sandbox file, commit, blob, date loaded (ET), loaded by.

**Never commit a loaded slot.** The committed content of every `_Test11`–`_Test30` is the inert placeholder. Slots 2–5 and 7–10 keep their committed content until the owner decides otherwise.

### 3.2 Compile (native)

- PowerLanguage Editor: open (or reload) the slot, press **F7** only. Never Ctrl+F7 and never Recompile All.
- A compile error is a battery failure at step (a). Record the first error line (file:line, message) in the result. Fix the sandbox file, re-copy, F7 again.
- If the Editor rewrites `PLStudies.csproj`, do not commit that change.
- A new slot file that the MiniPC clone does not yet have (slots 11–30 before this branch reaches the MiniPC) is registered once with File → New → Signal under the exact slot name, then the file is copied over and reloaded (the existing MiniPC routine for new studies).

### 3.3 Free a slot

After the result is recorded (§6) and the owner has read the verdict:

1. Copy the inert placeholder back (for slots 11–30, the committed file on this branch; for slots 2–10 the content at the commit recorded in the slot register when it was loaded).
2. F7, confirm "0 errors".
3. Remove the signal from its chart (or close the batch workspace without saving the slot inputs).
4. Mark the slot "free" in the register with the date.

### 3.4 Slot register (fill in)

| Slot | Signal ID | Sandbox file | Source commit | Blob | Loaded (ET) | Batch | Status |
|---|---|---|---|---|---|---|---|
| `_Test1` | — | — | — | — | — | — | reserved (Gate-0 / KD host) |
| `_Test2` | A2-69 | `_A2_69_KaufmanThreeDay.Strategy.CS` | `c862ad2` | `24309f4` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test3` | TS-01 (file TS-02) | `_TS_02_EuropeanOpenDrift.Strategy.CS` | `c862ad2` | `bbdd251` | 2026-11-07 (owner) | — | loaded, not run; class `_Test03` |
| `_Test4` | A2-49 | `_A2_49_MiddayBreakout.Strategy.CS` | `c862ad2` | `a61de99` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test5` | SW-02 | `_SW_02_SwingPullback.Strategy.CS` | `c862ad2` | `eb551f8` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test6` | A2-63 + G-R | (not a sandbox copy) | — | — | — | — | reserved, never overwrite |
| `_Test7` | AV-08 | `_AV_08_PeriodAvwapLayer.Strategy.CS` | `c862ad2` | `d4f7fef` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test8` | VW-05 | `_VW_05_LateDayVwap.Strategy.CS` | `c862ad2` | `8afaba5` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test9` | A2-56 | `_A2_56_FirstHourBreak.Strategy.CS` | `c862ad2` | `04b50f3` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test10` | A2-64 + A2-70 | `_A2_70_OpenFadeHvGate.Strategy.CS` | `c862ad2` | `888eeae` | 2026-11-07 (owner) | — | loaded, not run |
| `_Test11` | | | | | | | free |
| `_Test12` | | | | | | | free |
| `_Test13` | | | | | | | free |
| `_Test14` | | | | | | | free |
| `_Test15` | | | | | | | free |
| `_Test16` | | | | | | | free |
| `_Test17` | | | | | | | free |
| `_Test18` | | | | | | | free |
| `_Test19` | | | | | | | free |
| `_Test20` | | | | | | | free |
| `_Test21` | | | | | | | free |
| `_Test22` | | | | | | | free |
| `_Test23` | | | | | | | free |
| `_Test24` | | | | | | | free |
| `_Test25` | | | | | | | free |
| `_Test26` | | | | | | | free |
| `_Test27` | | | | | | | free |
| `_Test28` | | | | | | | free |
| `_Test29` | | | | | | | free |
| `_Test30` | | | | | | | free |

## 4. Sandbox signal inventory (origin/main `7a27b21`)

None of the ten files carries the 22-section research header yet (they predate `docs/research-strategy-header.md`; that standard says existing headers are not migrated). Spec reference = catalogue entry in `specs/trading-idea-catalogue.md` + the engines-manual example the file was cut from (`docs/engines-manual.md` §3).

| # | Catalogue ID | File (`sandbox/`) | Public class | Name | Spec reference | Base | Data1 | Data2 / other | Sides | `[Input]`s | Orders / tags |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | A2-49 | `_A2_49_MiddayBreakout.Strategy.CS` | `_A2_49_MiddayBreakout` | Kaufman afternoon breakout of the morning range (day-only, SAR 12:00–15:30, flat 15:55, disaster 1.5R) | Cat. A2-49; manual §3.4 (Ex 12) | `_CustomSignal` | @ES **30m**, 24h Globex, dxFeed unadjusted | none | L/S | TraceLevel | `A249LE/SE/LX/SX/LS/SS`; tag `A2-49` |
| 2 | A2-56 | `_A2_56_FirstHourBreak.Strategy.CS` | `_A2_56_FirstHourBreak` | First-hour breakout, core arm (stops at FH ± 1 tick 10:30–15:00, flat 15:55) | Cat. A2-56; manual §3.8 (Ex 18) | `_CustomSignal` | @ES **1m**, 24h Globex | none | L/S | TraceLevel | `A256LE/SE/LX/SX/LS/SS`; journalArm `A256LE/SE` |
| 3 | A2-69 | `_A2_69_KaufmanThreeDay.Strategy.CS` | `_A2_69_KaufmanThreeDay` | Kaufman three-day trade, core arm (09:31 or 15:59 entry, exit 15:59 next session, no stop) | Cat. A2-69; manual §3.1 (Ex 1) | `_CustomSignal` | @ES **1m**, CME Globex | none | L | TraceLevel | `K3LE/K3LX`; tag `A2-69` |
| 4 | A2-64 + A2-70 | `_A2_70_OpenFadeHvGate.Strategy.CS` | `_Ex17OpenFadeHvGate` (allowed legacy mismatch) | Fade to the RTH open at R ± m×ATR20d, gated by HV20 off-switch; **thetaOff is a PLACEHOLDER** | Cat. A2-64, A2-70; manual §3.10 (Ex 17) | `_VolumeProfileFuturesBase` | @ES **1m**, 24h Globex | M-DAILY aggregated from Data1 | L/S | TraceLevel + VP-base inputs | `A264LE/SE/LX/SX/LS/SS/LT/ST` |
| 5 | AV-08 | `_AV_08_PeriodAvwapLayer.Strategy.CS` | `_AV_08_PeriodAvwapLayer` | WTD/MTD VWAP regime layer on an **illustrative** 20-bar-high host | Cat. AV-08; manual §3.6 (Ex 3) | `_VolumeProfileFuturesBase` | @ES **1m**, 24h Globex | VWAP slots Week/Month on Data1 | L | TraceLevel + VP-base inputs (Vwap1/2Type must stay Week/Month) | `AV08LE/LX/LS`; tag `AV-08` |
| 6 | DV-15 | `_DV_15_EmaVwapPullback.Strategy.CS` | `_Ex20EmaVwapPullback` (allowed legacy mismatch) | EMA20 pullback with session-VWAP slope filter, 1:1 bracket at f×ATR20d; **fill bar unprotected** | Cat. DV-15; manual §3.9 (Ex 20) | `_VolumeProfileFuturesBase` | @ES **30m**, 24h Globex | session VWAP on Data1; M-DAILY | L/S | TraceLevel + VP-base inputs | `DV15LE/SE/LX/SX/LS/SS/LT/ST`; tag `DV-15` |
| 7 | SW-02 | `_SW_02_SwingPullback.Strategy.CS` | `_SW_02_SwingPullback` | Connors RSI(2) pullback with swing-low AVWAP filter (exit close > SMA5 or 5 bars; disaster 3×ATR20) | Cat. SW-02; manual §3.5 (Ex 16) | `_CustomSignal` | @ES **daily**, RTH template | M-AVWAP / M-PA on Data1 | L | TraceLevel | `SW02LE/LX/LS`; tag `SW-02` |
| 8 | TS-01 (file says TS-02) | `_TS_02_EuropeanOpenDrift.Strategy.CS` | `_TS_02_EuropeanOpenDrift` | European-open overnight drift: long 01:30→04:30 ET (or 02:00→03:00), event-filter variants, disaster 2×ATR60m | Cat. TS-01; manual §3.2 (Ex 2) | `_CustomSignal` | @ES **1m**, 24h Globex | **Data2 = @ES 60m** (`HtfStreams {2}`); M-EVENT (variants 3–4) | L | TraceLevel, `Variant` (int 1–4) | `TS01LE/LX/DS`; tag `TS-01` |
| 9 | TS-03 | `_TS_03_CloseDrift.Strategy.CS` | `_TS_03_CloseDrift` | Close regime variant A: 15:50 trade the 15:30–15:50 drift, exit 16:00, stop 1×ATR5; early-close days skipped | Cat. TS-03; manual §3.3 (Ex 13) | `_CustomSignal` | @ES **1m**, 24h Globex | **Data2 = @ES 5m** | L/S | TraceLevel | `TS03LE/SE/LX/SX/LS/SS`; tag `TS-03` |
| 10 | VW-05 | `_VW_05_LateDayVwap.Strategy.CS` | `_VW_05_LateDayVwap` | Late-day continuation, 15:00 arm: sign(15:00 − prior RTH close) = sign(price − session VWAP), exit 15:55, stop 1×ATR30m | Cat. VW-05; manual §3.7 (Ex 14) | `_VolumeProfileFuturesBase` | @ES **1m**, 24h Globex | **Data2 = @ES 30m**; session VWAP on Data1 | L/S | TraceLevel + VP-base inputs | `VW05LE/SE/LX/SX/LS/SS`; tag `VW-05` |

All ten use `RollDetection = Series` (unadjusted continuous series), 1 contract, a 14-tick cost floor and a 1%-of-equity risk-budget skip in their protected config. None sets IOG.

## 5. Native test battery (per signal)

Every step writes its evidence into the signal's result block (§6). The battery follows the programme overview (`specs/strategies/00-programme-overview.md` §4 costs and fills, §5 windows, §6 acceptance, §8 logging), the live-check sheet conventions (`specs/live-check-sheet.md`: run IDs, CS-1 log path, results table) and the engines manual (`docs/engines-manual.md` §1.5 journaling).

### 5.0 Common chart settings (record them in the result)

| Setting | Value |
|---|---|
| Data | dxFeed `@ES`, continuous **unadjusted**, session template as in §4 (24h CME Globex, or RTH for SW-02) |
| Date range | **dated** From/To, To ≤ 2024-12-31, set before OK. Windows: W-M development = first verified full month Jun/Jul 2016 → 2022-12-31; validation = 2023-01-01 → 2024-12-31. SW-02 (daily) W-D: development 2000–2015, validation 2016–2024 |
| Data coverage | If @ES minute history on the MiniPC still starts 26 Dec 2022 (overview §5.2 caveat), every 1m/30m run is an **engineering smoke test**, not development evidence. Record the first bar date actually loaded |
| Costs | ES: commission **$2.25 per contract per side**, slippage **$6.25 per contract per side** (= $17.00 round trip, overview §4). NQ $2.24 + $2.50, CL $2.36 + $5.00, 6E $2.46 + $3.125 if another instrument is run |
| Fills | Bar Magnifier **on**, 1-minute detail for bars above 1m (30m, daily). Limits fill on trade-through only. IOG **off** (none of the ten signals declares IOG) |
| Size / capital | Size as coded (1 contract). Initial capital: **$100,000** (PROPOSED, owner to confirm; see §9). Record it, because the 1% risk-budget skip depends on it |
| Max bars back | Auto-detect, or the value the signal's warm-up needs; record it |
| Inputs | `TraceLevel = Journal`. All other inputs at defaults unless the step says otherwise |
| Output | Output window + CS-1 log `%LOCALAPPDATA%\MCSignalLogs\MCSignal<stamp>-<pid>.csv`, `MCJournal_<runId>.csv`, `MCEvents_<runId>.csv`. Copy the three files for the run into the batch folder `C:\Share\MultiCharts\<Batch>\out\<Slot>_<ID>_<step>\` |
| Screenshots | Dialogs only (Format Instrument, Inputs, Properties). Never a price area or a full screen |

### 5.1 Steps

**(a) Compile.** F7 in the Editor; "0 errors". Then insert the slot on its chart: no exception in the Output window at StartCalc (missing Data2, wrong VWAP slot type, MC-BidAsk setting and so on). **Pass:** 0 errors and a RUN line in the log.

**(b) Runs on its spec instrument/timeframe.** Chart exactly as §4 (Data1, Data2 where named, session template) with §5.0 settings, development window. Record runId, first/last bar dates, bars loaded, number of trades, and every Warning/Error line (count by text). **Pass:** the run completes; no Error lines; trades > 0 (or a documented reason for 0, such as every night skipped by the risk budget); last trade exit ≤ 2024-12-31.

**(c) Entry-rule verification.** From `MCJournal`/`MCEvents` and the signal's SETUP/ARM/logSignal lines:
- Sample **N = 10 long and 10 short** entries (all if fewer; long-only signals: 20 long), using a fixed seed (12345) over the trade list, stratified so every calendar year in the window contributes at least one where possible.
- For each sampled entry, check every entry condition in the spec walk-through against the values in the log line (levels, times, side, filter states). Also check order type and price: market next bar, stop at level ± tick, or limit at level, as coded.
- Cross-check **3** of the sampled entries against the chart's bars (Data Window values only, cropped dialogs, dates ≤ 2024).
- Also sample **5 skipped setups** (`logSignal(..., false)` / `SKIP` lines) and confirm the skip reason is valid.
- **Pass:** 100% of sampled entries match the rule. Any mismatch is a fail, with the trade's date and the condition that failed.

**(d) Exits and control exit.**
- Classify every exit by its order name (`..LX/SX` time or rule exit, `..LS/SS` stop, `..LT/ST` target, `..DS` disaster stop). Check counts against the spec: time exits at the coded clock (for example 15:55 ET, 16:00 ET, 15:59 next session, 04:30 ET); early-close days use the early close minus the coded offset; stop distance = coded rule (ATR multiple, FH other side, morning-range R), widened to the 14-tick floor where coded.
- Sample 10 exits per exit type and check the price and time against the rule, as in (c).
- **No position held past its flat time**, and none held over a roll splice unless the spec allows it.
- **Control exit** (`specs/generic-exit-manager-spec.md` §4.9 on PR #119, owner-approved 2026-11-11): every entry-only idea also runs a control arm, `ExitControl = FixedBars`, N by Data1 timeframe: **1m 30, 5m 12, 15m 8, 60m 6, daily 5** (other bar sizes use the nearest listed timeframe at or below: 30m → 8). This needs the `_GenericExit` build (PR #119 hand-off). Until it exists, record "control arm pending GX build" and run CS-8 forward MFE/MAE from `MCEvents` as the no-exit look. Signals with their own exit rule in the catalogue (all ten here) still report their coded exits as the baseline arm.
- **Pass:** every sampled exit matches its rule; no overnight/weekend holding unless coded (A2-69 holds to the next session; SW-02 holds days).

**(e) No lookahead, IOG and fill-bar issues.**
1. **Truncation test.** Rerun with To moved back one year (for example 2021-12-31 instead of 2022-12-31). Every trade that closed before the earlier To must be identical (entry/exit time, price, size) in both runs. Any difference = lookahead or repaint.
2. **Determinism.** Recalculate the same run twice; trade lists must be identical (compare the journal hash or a sorted CSV diff).
3. **Decision bar before fill bar.** For every trade, entry fill time > the close time of the bar that armed it (`ARM`/`journalArm` time). For Data2 users (TS-01, TS-03, VW-05): the HTF value used was from a Data2 bar whose close ≤ the Data1 decision bar close (`HtfClosedBar`).
4. **IOG.** Properties show IOG off. If a spec ever needs IOG, its setting is recorded and the run repeated with Bar Magnifier off to show the difference.
5. **Fill bar.** Count trades whose fill bar crossed the coded stop level while the stop was not yet working. Known unprotected fill bars: DV-15 and A2-64/A2-70 (headers say so). Report the count and the extra loss versus a stop at the coded level. Also count ambiguous bars (stop and target both touched) and confirm stop-first scoring (overview §4).
6. **Bar Magnifier sensitivity.** Rerun with Bar Magnifier off; report the change in trades and net. A large change is a note, not a fail.
- **Pass:** 1–3 clean; 5 reported.

**(f) Performance summary (≤ 2024 only).** From the journal (preferred) or the Strategy Performance Report, development window, then validation window as a separate row:
- trades; net profit after costs; profit factor; win rate; average win / average loss; expectancy in $ and in R (`win_rate * avg_win - loss_rate * avg_loss`, cost-adjusted); max closed-trade drawdown in $ and in R; max open-equity drawdown in $; average R, median R, largest win as % of net (concentration, overview §6 item 3); trimmed expectancy after removing the top 1% of winners; MFE/MAE medians in R.
- Split **long / short / combined** (overview §7.9 item 8).
- Per calendar year: trades and net (overview §6 item 7).
- Label every figure with the window, detail (1m magnifier) and capital.

**(g) Verdict.** One of:
- **PASS (mechanics):** (a)–(e) pass. The signal does what its spec says. This says nothing about edge.
- **FAIL (mechanics):** any of (a)–(e) fails; the notes name the step and the evidence.
- **INCONCLUSIVE:** smoke-test data only, or too few trades to sample.

Then an edge note against overview §6 (net PF ≥ 1.20 and positive expectancy; ≥ 100 validation trades; DD ≤ 20R; concentration ≤ 10%), marked **"indicative, not acceptance"** unless the full development and validation windows were available and the trial-budget rules were followed. Acceptance and promotion are owner decisions.

## 6. Results template (one block per signal, appended below §8)

```text
### <ID> — <Name>   (slot _TestNN, batch <B>)
Source: sandbox/<file> @ <commit> (blob <blob>); slot diff check: identical except class rename [yes/no]
Chart: <symbol> <bar size> <session template> | Data2: <...> | From <date> To <date ≤ 2024-12-31> | first bar loaded <date>
Settings: commission $/side, slippage $/side, Bar Magnifier <on 1m/off>, IOG off, capital $<n>, MaxBarsBack <n>, inputs <non-default only>
Runs: <runId list with step letter>
(a) Compile: <PASS/FAIL> <first error if any>
(b) Run: <PASS/FAIL> bars <n>, trades <n>, Warnings <n by text>, Errors <n>
(c) Entries: <PASS/FAIL> sampled L <n>/S <n>, mismatches <n> (<date: condition>), chart cross-checks 3/3, skips 5/5
(d) Exits: <PASS/FAIL> by type <LX n, LS n, ...>, sampled mismatches <n>; control arm <run/pending GX build>
(e) Lookahead/IOG/fill bar: truncation <identical/diff>, determinism <identical/diff>, decision<fill <n violations>, IOG off,
    unprotected fill-bar hits <n> (extra loss $<x>), ambiguous bars <n>, magnifier-off delta trades <n> net $<x>
(f) Performance (window <dev/val>): trades <n> (L <n>/S <n>), net $<x>, PF <x>, win% <x>, expectancy $<x> / <x>R,
    max closed DD $<x> / <x>R, max open DD $<x>, avg R <x>, median R <x>, largest win % of net <x>, trimmed expectancy <x>R,
    MFE/MAE median <x>R/<x>R, per year <yyyy: n, $x; ...>
(g) Verdict: <PASS (mechanics) / FAIL (mechanics) / INCONCLUSIVE> — edge note: <indicative only / meets / misses §6 items ...>
Notes: <spec discrepancies, coding choices hit, follow-ups for the sandbox file>
Tested by / date (ET): <...>   Slot freed: <date or "still loaded">
```

## 7. Batches

- A batch is up to **30** signals, one per slot (`_Test11`–`_Test30` plus any released slot among 2–5 and 7–10; never `_Test1` or `_Test6`).
- **One chart workspace per batch:** `C:\Share\MultiCharts\Workspaces\SandboxBatch<NN> Charts.wsp`, one chart window per signal, titled `<Slot> <ID>`. One signal per chart: two signals on one chart share a position and contaminate each other.
- Charts that share Data1/Data2 (for example all 1m @ES Globex) can be built from one chart template, but each keeps its own signal.
- **Run one chart at a time** (set the other charts' strategies off, or keep only a few charts open). Ten or more 1m charts over 2016–2024 recalculating together will exhaust memory on the MiniPC.
- Portfolio Trader: an optional **one PT workspace per batch** (`SandboxBatch<NN> Backtest.pws`) is for portfolio-level checks only (M-PORT interactions). It is not used for the per-signal battery, because the MM and shared capital change fills.
- Staging and outputs per batch: `C:\Share\MultiCharts\SandboxBatch<NN>\` (slot files as copied) and `...\out\` (journals per run). Never open or save `M5 Validation 388314a.wsp` or any `Check7*` file.
- Order inside a batch: compile all slots first (one F7 each), then run (a)–(b) for every signal, then (c)–(f) per signal, then verdicts.
- **Batch 1 (proposed):** the ten sandbox signals of §4. Eight are already loaded in `_Test2`–`_Test5`, `_Test7`–`_Test10`, so only DV-15 and TS-03 need slots (`_Test11`, `_Test12`). `_Test3`'s class must be renamed to `_Test3` before F7 (see §9).

## 8. Results register (one row per sandbox signal)

| # | Catalogue ID | Sandbox file | Slot | Batch | (a) | (b) | (c) | (d) | (e) | (f) dev | (f) val | (g) Verdict | Date (ET) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | A2-49 | `_A2_49_MiddayBreakout` | `_Test4` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 2 | A2-56 | `_A2_56_FirstHourBreak` | `_Test9` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 3 | A2-69 | `_A2_69_KaufmanThreeDay` | `_Test2` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 4 | A2-64 + A2-70 | `_A2_70_OpenFadeHvGate` | `_Test10` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 5 | AV-08 | `_AV_08_PeriodAvwapLayer` | `_Test7` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 6 | DV-15 | `_DV_15_EmaVwapPullback` | — | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 7 | SW-02 | `_SW_02_SwingPullback` | `_Test5` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 8 | TS-01 | `_TS_02_EuropeanOpenDrift` | `_Test3` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 9 | TS-03 | `_TS_03_CloseDrift` | — | — | not run | not run | not run | not run | not run | not run | not run | not run | — |
| 10 | VW-05 | `_VW_05_LateDayVwap` | `_Test8` | — | not run | not run | not run | not run | not run | not run | not run | not run | — |

New sandbox signals (for example the batch-1 strategies PS-02, PS-01, VW-01, AV-02, AV-01, OF-01 once Coder writes them) get a row here when their file lands in `sandbox/`.

## 9. Open points

1. **`_Test3` class name.** The file `_Test3.Strategy.CS` declares `public class _Test03`. MultiCharts names a study after its class/file, so this slot will probably not load as `_Test3`. Not verified natively. Proposed: rename to `_Test3` when batch 1 loads it.
2. **`_Test1` reserved?** It holds an order-flow VP scaffold and is named as the Gate-0/KD host in the catalogue and the Keller B5 notes. This protocol treats it as reserved until the owner releases it.
3. **Initial capital.** PROPOSED $100,000 for every slot run. The sandbox signals skip a trade when 1 contract × stop × $50 > 1% of equity, so capital changes the trade list (AV-08 needs ≥ $20,000 just for its 16-tick stop).
4. **TS-02 file holds TS-01.** `sandbox/_TS_02_EuropeanOpenDrift.Strategy.CS` implements catalogue TS-01 (orders `TS01*`, tag `TS-01`). Results are recorded under TS-01. Renaming the file is a separate change.
5. **`Variant` is an int input** on TS-01 (1–4), against the house rule that multi-value inputs are typed enums. Tested as is; changing it is a sandbox-file change.
6. **Data coverage.** The 1m runs are smoke tests until 2016–2022 minute history is verified on the MiniPC (overview §5.2).
7. **Control arm** depends on the `_GenericExit` build from PR #119.
8. **New slots on the MiniPC.** Slots 11–30 exist on the MiniPC only after this branch is merged and the studies clone is updated by the owner; until then each new slot is registered once with New Signal (§3.2).
