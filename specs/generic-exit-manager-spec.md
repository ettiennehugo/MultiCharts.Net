---
title: "Generic trade-management and exit layer (GX) for entry-only ideas — PROPOSAL"
rev: "0.1 PROPOSAL, 2026-11-11 (ET)"
status: "PROPOSAL, docs only, no code. Not approved. Every default and design choice not quoted from the owner is PROPOSED."
requested_by: "Ettienne Hugo, 2026-11-11 09:02 ET (verbatim below)"
code_refs: "origin/main @ 47ffda2 (2026-11-10). Batch-1 framework references are to PR #117 (branch feat/strategy-specs-batch1, specs/strategies/00-programme-overview.md rev 1.6) and the M-PORT add-legs draft PR #118 (specs/m-port-add-legs-spec.md)."
holdout: "No price data on or after 2025-01-01 is viewed or reported by anything in this spec. Native checks use data ≤ 31 Dec 2024."
---

# Generic trade-management and exit layer (GX), rev 0.1 PROPOSAL

## 0. Request and scope

**Owner (2026-11-11 09:02 ET, verbatim):** *"I think another thing to add it a general trade management and exit strategy. This would be required to help us test strategies that are purely just entries without concrete trade management rules. Please look into that and propose how we could implement it, consider either just a single strategy but if it becomes too complex to have a single strategy with a bunch of parameters we could consider implementing multiple strategies."*

**Goal.** One standard, predeclared way to manage and exit a position, so an idea that only defines an **entry** can be backtested with:
- comparable R, MFE/MAE and exit-reason reporting;
- the same costs and fill rules as batch 1;
- Portfolio Trader (PT) R accounting.

The exit layer must not become a hidden optimiser: its defaults are fixed before any run (catalogue M2), and a changed setting is a new trial.

**Not in scope:** entry logic, sizing (CS-4 `sizeForRisk` and the M-PORT MM stay as they are), live trading, broker connection, Forward Testing.

## 1. Survey: what exists

### 1.1 Exit studies already in the repo (root, compiled)
| File | What it does | Notes for GX |
|---|---|---|
| `_StopsAndTargets.Strategy.CS` (86 lines) | Exit-only `SignalObject`. Inputs: `OrderMode` (MC's `ESpecOrdersMode`: PerContract / PerPosition), `StopLossPct`, `BreakEvenPct`, `TrailingStopActivationPct`, `TrailingStopPct`, `TargetPct`. Uses MC built-in special orders (`GenerateStopLoss`, `GenerateBreakEven`, `GeneratePercentTrailing`, `GenerateProfitTarget`) | Percent of entry price only, no R, no journal. Source has a TODO for break-even and a price-extreme trail. It is evidence that an exit-only signal on the chart can manage another signal's position |
| `_Stops_And_Targets.Strategy.CS` (62 lines) | An older variant of the same idea | Legacy |
| `_ExitStatic.Strategy.CS` (98 lines) | Exit-only. `EExitStrategy { FixedStopTarget, FixedBars, RandomExit }` with `FixedStop`/`FixedTarget` ($), `FixedBars`, `MaxRandomBars`. Header: *"Static exit that can be used to test the effectiveness of an entry."* | The classic entry-test exits (fixed bars, random exit). GX keeps them as **control** modes (§4.9) |
| `_ExitHalfRiskHalfRoll.Strategy.CS` (374 lines) | Exit-only `PortfolioSignalObject`. Half off at +1R, the rest trailed on a "roll" level (entry-bar low − 1 tick, then the prior day's low − 1 tick). R = entry to one tick outside the prior bar. Invariant: never opens, adds or reverses. After another signal's add, R stays anchored at the first entry. Needs Data2 (longer resolution) | Closest existing design to GX. Its R-after-add rule is superseded by the owner's 22:35 ET re-base answer (§5) |
| `_StpOrLim_Exit.Strategy.CS` (90 lines) | A stop-or-limit exit helper | Legacy |
| `_TradeManagerTest.Strategy.CS` | A `_CustomSignal` test signal for the CS-7 primitives | — |

### 1.2 Base-class building blocks (`_CustomSignal.Strategy.CS`)
- **CS-1 trade journal** (L151–200, L2788 `journalArm`, L2846–2865): one CSV per run (`MCJournal_<runId>.csv`) with:
  - initial stop, `riskPerUnit`, `riskTicks`, `riskDollars`, `rMultiple`;
  - `mfeDollars`/`maeDollars` from MC's `MaxRunUp`/`MaxDrawDown`, `mfeTicks`/`maeTicks`, `mfeR`/`maeR`;
  - `excursionScope` (`trade` or `position(n trades)`);
  - the exit order name, which carries the exit reason.
- **CS-7 trade-management primitives** (L3290–3395, protected properties, not Inputs): `NoEntryBefore`, `NoEntryAfter`, `FlatBy`, `MaxTradesPerDay`, `DailyLossStopCount`, `DailyLossStopR`, `RiskUnitDollars`, `TimeStopBars`, `entryAllowed()`, `mustFlatten()`, `sessionClock()`. **The base sends no orders.**
- **CS-8 `logSignal`** (L3151–3191): forward MFE/MAE in R over horizons of 5/15/30/60 bars for every signal, taken or not. This is an **exit-independent** measure of entry quality.
- **M-PORT publisher** (L1702–1797; m-port-spec rev 2.7):
  - `portfolioArm(side, entry, stop, qtyCode, widenedStop)` arms the entry and publishes `pm.R$`, `pm.R$w`, `pm.qtyCode`.
  - `portfolioStop(stop)` sets the active stop behind `pm.riskOpen`.
  - `portfolioBar()` publishes the state, `pm.pos`, `pm.riskOpen`, `pm.openR` (unit `m_portRDollar`) and `pm.stamp`.
  - PR #118 proposes `portfolioArmAdd` and the R re-base on an add fill.
- **M-INSTR cost floor** (`CostModel.WidenedStop`, PR-M7, opt-in `applyCostFloor`): ES 14 ticks, NQ 19 ticks. **M-EVENT** (`EventCalendar.MinutesTo(EEventTier.T1)`), `_CmeHolidays` / M8 calendars (early closes), `RollDetection` / `IsRollWindow`.
- **Typed enums already used as Inputs:** `ETraceLevel`, `ERollDetection`, `EControlMode { None, RandomEntry, ShiftLevels, DelayEntryBars }`. `EControlMode` already offers entry-side controls, and GX adds the exit-side ones.

### 1.3 Standards GX must follow
- **Research strategy header** (`docs/research-strategy-header.md`, PR #103 merged):
  - Each parameter is one native `[Input]` (a public property with a parameterless `[Input]`, default set in the constructor). No input classes or structs for new research strategies.
  - Header §17 has one block per input: Purpose / Default / Units / Range / Effect / Interactions / Fixed or research-tuned.
  - Defaults are "predeclared choices", never "optimal".
- **Programme rules** (PR #117 overview §3):
  - rule 8: every variant is selectable via Inputs, and the default is the primary;
  - rule 9: multi-value inputs are typed C# enums with named members (no int codes, no strings), bools for on/off, ints only for counts, minutes or clock times where 0 = off.
- **Batch-1 shared exit framework** (PR #117 overview §7). GX reuses it unchanged:
  - §7.1 order set and names (`<ID>LS`, `<ID>LT1`, `<ID>LT`, `<ID>LX`);
  - §7.2 contract split: ⌊q/2⌋ at T1, the runner gets the odd contract; `SingleLotMode`; a 2-contract test size;
  - §7.3 one-way stops, `TRAIL-THROUGH`;
  - §7.4 same-bar precedence;
  - §7.5 MM interaction;
  - §7.6 sessions, news (`NewsExitMinutes`), early closes, rolls;
  - §7.6a open window;
  - §7.7 adds and the R re-base (owner-approved 22:35 ET; `AddMinR` in re-based 1R, and stop/target triggers on the initial R, owner-approved 2026-11-11 08:58 ET).

## 2. MultiCharts .NET constraint: can a separate exit-only signal manage another signal's position?

| # | Fact | Source / status |
|---|---|---|
| F1 | Several signals applied to **one chart** work as **one strategy**: signal A can enter and signal B can exit. Signals on different charts are independent | MC staff, forum *"Order handling in signal / multiple signals"* (viewtopic t=46258). The repo's exit-only studies (§1.1) rely on it. **Documented; confirm natively (E1)** |
| F2 | `OrderExit.FromEntry(IOrderObject)` can only tie an exit to an entry **created in the same signal**. A separate exit signal cannot name another signal's entry | Programming Guide v1.1 (2014-11-19), "OrderExit"; Help Center page *"Entry"*: *"An exit can only be tied to an entry within the same signal"*. **Documented** |
| F3 | `OrderExit.FromAll` with a quantity closes **that quantity from each entry**. With an initial entry plus adds, "Sell 1 FromAll" can sell more than 1 contract. `OrderExit.Total` closes exactly the quantity given, oldest entry first | Programming Guide v1.1, "Exit types" worked example (buy1 5, buy2 1, buy3 2; Sell FromAll 2 sells 5). **Documented.** GX rule: partial exits use `OrderExit.Total`; full exits use `FromAll` with no quantity |
| F4 | By default an exit can be applied to one entry only once. `[ExitFromOneEntryOnce(ExitFromOneEntryOnce.FillAndKillExitOrder)]` lets the same exit order be reused for repeated partials | Programming Guide v1.1. **Documented; GX needs it only if one order object is used for several scale-outs (E5)** |
| F5 | Pyramiding requires Strategy Properties *"Allow up to XXX entry orders in the same direction…"* | Programming Guide v1.1, rule 4. This is likely the "Position Limits" option PR #118 A1 asks about. **Confirm the label in the installed build (E6)** |
| F6 | `PositionSide`, `CurrentPosition` (`OpenTrades`, `MaxRunUp`, `MaxDrawDown`), `StrategyInfo.MarketPosition` and `StrategyInfo.AvgEntryPrice` describe the **strategy's** (all signals') position | Implied by F1 and used by `_ExitHalfRiskHalfRoll` and `_StopsAndTargets`. **Confirm what an exit signal sees on the entry-fill bar (E1)** |
| F7 | **Fill-bar protection.** An entry signal can send its stop **with** the entry ("also protects the entry bar", engines manual v3.2 TS-01 example, `m_lxStp.Send(m_stop)` next to `m_le.Send`). A separate exit signal does not know a pending entry exists, so its explicit stop starts only after the fill bar has closed: engines manual v3.2 "the fill bar itself is unprotected", and `_Test10`'s pitfall note. MC's built-in `GenerateStopLoss` is applied by MC from the fill | Repo docs (engines manual v3.2 L739–741, L1056–1058; `_Test10` L108–109). **The built-in special-order behaviour on the fill bar is unverified natively (E2)** |
| F8 | In PT, an `IPortfolioStrategy` has `Signals : IStrategy[]` (several signals per instrument). The MM reads `Signals[0]` (`_PortfolioRiskManager.view` L125) and the strategy's `PortfolioData`. `EntryContracts` applies to every **entry** order of the strategy; exits are never gated | m-port-spec §1, §6; `_PortfolioRiskManager.Strategy.CS` L111, L125. **Whether `MyPortfolioData` is one dictionary per strategy, shared by its signals, is unverified (E4)** |
| F9 | IOG is unavailable in PT, and so is Bar Magnifier. On a chart, IOG is a per-signal setting | m-port-spec §4 item 4. **A chart exit signal with IOG on while the entry runs closed-bar would break chart/PT parity. GX forbids IOG (E8 confirms the setting is per signal)** |

**Consequences.**
1. A stand-alone exit signal **works** on a chart and in PT as a second signal (F1, F8). But:
   - it cannot use `FromEntry` (F2);
   - it must use `Total` for partials (F3);
   - it does not know the entry's planned or structural stop, so it must derive R itself;
   - it leaves the fill bar unprotected unless it uses MC's built-in stop (F7);
   - in PT, the entry strategy's `pm.riskOpen` would not follow the exit signal's stop (F8), unless the exit signal publishes it, and that is unverified.
2. An exit engine running **inside** the entry signal has none of these problems. It sends the stop with the entry, knows the structural and widened stops, calls `portfolioStop`, journals through CS-1, and can use `FromEntry` per leg.

## 3. Options compared

| | (A) One generic exit signal with many typed-enum Inputs | (B) A family of stacked exit signals (stop, target/scale, trail, time/session) | (C) A shared C# exit engine used by entry strategies, plus one thin wrapper signal |
|---|---|---|---|
| Where the logic runs | A separate signal on the chart / PT strategy | 3–5 separate signals | Inside the entry signal (`_CustomSignal` helper class). The wrapper is a separate signal only for entries that cannot embed it |
| Input count | About 28 on one dialog (§4.11) | About 6–9 each, but 4 dialogs and 4 "same strategy" set-ups per test | Embedded: 0 new Inputs by default (the host exposes the few it varies, header §17); wrapper: about 28, same as A |
| Knows the entry's stop / R | No: derives R (F2, F7) | No | **Yes** (embedded); wrapper: derives R |
| Fill-bar protection | Only through `GenerateStopLoss` (E2) | Same, in the stop module | **Yes**: the stop is sent with the entry |
| Same-bar precedence (§7.4) | One place decides; one reason is journalled | **Not controllable**: each signal sends its own exit, and MC fills whichever triggers. Two close-based exits in one bar give two orders and an ambiguous reason. Ordering depends on the Format Signals list (E3) | One place decides (the engine) |
| Optimiser sweeps | Fine, one dialog. Whether enum Inputs can be swept is the existing open check (PR #117 overview §9, E7) | Awkward: a sweep spans several signals | Embedded: the host exposes the exact Inputs a registered ablation needs. Wrapper: as A |
| Testability off-MC | Possible if the logic sits in a pure class | Each module separately; the interactions are not | **Best**: one pure `_ExitEngine` state machine, unit-tested like `_PortMM` under `tests/`, fed by bar and fill events |
| Portfolio Trader | Works as a second signal, but `pm.riskOpen` / `pm.openR` are wrong unless the exit publishes (E4) | Same, with more signals | **Embedded: full M-PORT compatibility** (`portfolioStop`, `portfolioArmAdd`, R re-base §5). Wrapper: chart-first; PT use only after E4 |
| Adds (PR #117 §7.7, PR #118) | Sees the position grow, so it can re-base R, but cannot see `pm.add` | Same | The engine owns the add path: `AddStopMode`, re-base, `portfolioArmAdd` |
| Risk of drift between strategies | Low | Medium (module versions) | **Lowest**: one engine. Batch-1 specs can later adopt it instead of hand-coding §7 |
| Effort | Medium | High (4 signals, plus precedence glue) | Medium–high once; then low per strategy |

**Recommendation (PROPOSED): option C.**
- Build one pure C# exit engine (`_ExitEngine`) that implements PR #117 overview §7 exactly, and expose it to `_CustomSignal`-derived entries.
- Ship **one** thin wrapper signal, `_GenericExit`, for entry-only ideas that cannot embed the engine (a legacy entry, a third-party signal, a quick sandbox entry).
- Do not build B: same-bar precedence and a single exit reason cannot be guaranteed across separate signals.
- A is the wrapper half of C. If the wrapper's dialog proves too large in practice, split it into at most two wrappers (`_GenericExit` for stops/targets/trail, `_GenericExitClock` for time/session/news), with precedence documented as "the first exit to fill wins" (E3). PROPOSED fallback only.

## 4. Exit component catalogue (typed enums, PROPOSED)

All prices are rounded to the tick. "R" is the initial 1R: |entry − initial stop| × BPV per contract, using the **widened** stop when the cost floor widens it (P1 rule). After an add, R is re-based (§5). Ints are counts, minutes or clock times, where 0 = off.

### 4.1 Initial stop
```csharp
public enum EInitialStop { EntryProvided, AtrMultiple, FixedTicks, SignalBarExtreme, SwingExtreme, Percent }
```
| Member | Rule (long; short mirrors) | Inputs |
|---|---|---|
| `EntryProvided` (embedded default) | The entry's own structural stop: the one passed to `sizeForRisk` / `portfolioArm` | — |
| `AtrMultiple` (wrapper default) | entry − `StopAtrMult` × ATR(`StopAtrLength`) on Data1, ATR from closed bars | `StopAtrMult` (double, 1.5), `StopAtrLength` (int, 14) |
| `FixedTicks` | entry − `StopTicks` × tick | `StopTicks` (int, 0 = off ⇒ invalid with this mode) |
| `SignalBarExtreme` | low of the bar before the fill − 1 tick (`_ExitHalfRiskHalfRoll`'s R) | — |
| `SwingExtreme` | lowest low of the last `StopSwingBars` closed bars − 1 tick | `StopSwingBars` (int, 5) |
| `Percent` | entry × (1 − `StopPct`/100) (`_StopsAndTargets`-compatible) | `StopPct` (double) |

- **Floor:** the stop is always at least the M-INSTR cost floor (ES 14, NQ 19 ticks), via `CostModel.WidenedStop`.
- **Embedded:** a trade whose widened risk is > 1% of equity is skipped by `sizeForRisk` before entry (P1).
- **Wrapper:** it cannot skip an entry it did not make. It journals `GX RISK-OVER-BUDGET` and manages the trade normally. Q7 asks whether it should flatten instead.

### 4.2 Targets and scale-out
```csharp
public enum ETargetMode { ScaleOutThenRunner, SingleTarget, None }
public enum ESingleLotMode { ExitAtT1, RunnerOnly }   // existing name and default from PR #117 §7.2
```
- **`ScaleOutThenRunner`** (default): ⌊q/2⌋ at T1 = entry + `T1R` × R (`T1R` double 1.0), with a limit `<ID>LT1` and `OrderExit.Total`. The runner is managed by §4.4/§4.5. `FinalTargetR` (double, 0 = off) is an optional limit for the runner.
- **`SingleTarget`:** the whole position at `FinalTargetR` (must be > 0).
- **`None`:** no target; exits come only from the stop, trail and clock.
- **q = 1:** `SingleLotMode` (default `ExitAtT1`, owner-accepted 2026-11-10 for batch 1).
- T1 and the split use the **initial** contracts only. Adds join the runner (PR #117 §7.7).

### 4.3 Break-even
```csharp
public enum EBreakEven { AfterT1, AtR, Off }
```
- `AfterT1` (default): once T1 fills, stop = entry + `BreakEvenOffsetTicks` (int, default 1, PROPOSED: covers a tick of cost).
- `AtR`: on a close ≥ entry + `BreakEvenR` × R (double 1.0), measured on the **initial** R (owner-approved 2026-11-11 08:58 ET: stop/target triggers stay on the initial-entry R).
- One-way (§7.3).

### 4.4 Trailing stop (runner)
```csharp
public enum ETrailMode { Chandelier, Atr, BarExtreme, Ema, RollLevel, None }
public enum ETrailStart { AfterT1, AtR, Immediately }
```
| Member | Candidate stop (long) | Inputs |
|---|---|---|
| `Chandelier` (default) | highest high since entry − `TrailAtrMult` × ATR(`TrailAtrLength`) | `TrailAtrMult` 3.0, `TrailAtrLength` 22 |
| `Atr` | last close − `TrailAtrMult` × ATR | same |
| `BarExtreme` | lowest low of the last `TrailBars` closed bars − 1 tick | `TrailBars` (int, 2) |
| `Ema` | EMA(`TrailEmaLength`) of closes − 1 tick | `TrailEmaLength` (int, 20) |
| `RollLevel` | `_ExitHalfRiskHalfRoll`: entry-bar low − 1 tick, then the prior day's low − 1 tick (needs a daily series, M-DAILY) | — |
| `None` | no trail | — |

- `TrailStart` default `AfterT1`; `AtR` uses `TrailStartR` (double, 1.0) on the initial R.
- One-way, capped one tick below the last close. Through the market ⇒ market exit, `TRAIL-THROUGH` (§7.3).

### 4.5 Time stop
- `TimeStopBars` (int, 0 = off) uses CS-7 `TimeStopBars` (the oldest open entry decides).
- `TimeStopMinutes` (int, 0 = off): wall-clock minutes since the fill, for mixed resolutions.
- Both close-based (`<ID>LX`, reason `TIME`).

### 4.6 Session end, early close, holidays
```csharp
public enum ESessionExit { FlatBy, HoldOvernight }
```
- `FlatBy` (default for intraday): flatten at `FlatByHHMM` (int clock time, ET, default 1555 = 15:55, Cat. §2.1) through CS-7 `FlatBy` / `mustFlatten()`.
- On an early-close day (`_CmeHolidays` / M8 calendars): `RthCloseEt − 5 min`.
- A full-close holiday has no session, so there is nothing to manage.
- `HoldOvernight`: swing holds keep the stop resting overnight, and `MaxHoldDays` (int, 0 = off) applies.

### 4.7 News exit
- `NewsExitMinutes` (int, 0 = off, range 0–120) is exactly PR #117 overview §7.6: `EventCalendar.MinutesTo(EEventTier.T1)` and the window rule `0 < m < N + B`. Reason `NEWS`.
- Needs `EventConfig.enabled = true`. Coverage holes ⇒ no exit, and `evtCoverage` is journalled.

### 4.8 Roll
```csharp
public enum ERollExit { FlattenBeforeSplice, Ignore }
```
`FlattenBeforeSplice` (default): flatten at the close of the last session before the splice (§7.6; unadjusted continuous series). It matters only for `HoldOvernight`.

### 4.9 Control (null) exits for entry testing
```csharp
public enum EExitControl { None, FixedBars, RandomBars }
```
- `FixedBars`: exit after `ControlExitBars` (int, 10) bars whatever happens, with the initial stop still resting as a disaster stop.
- `RandomBars`: a seeded uniform 1…`ControlExitBars` (seed = the run seed, journalled).
- These carry over `_ExitStatic`'s entry-test idea. The exit layer gives three looks at the same entry:
  1. CS-8 forward MFE/MAE, with no exit at all;
  2. a dumb exit (`FixedBars` / `RandomBars`);
  3. the baseline package.
  An entry edge should show in all three.
- Default `None`.

### 4.10 Same-bar precedence (fixed, not an Input)
PR #117 overview §7.4, unchanged:
1. MM `ForceClosePosition`;
2. protective stop (intrabar);
3. target limit (intrabar; stop first when the order is ambiguous);
4. `NEWS`;
5. session-end flatten;
6. roll flatten;
7. invalidation (the host's own, embedded only);
8. time stop;
9. control exit.

After 4–9: break-even, trail and T1 re-send for the next bar, only if still in a position and no market exit was sent.

### 4.11 Wrapper Input list (`_GenericExit`, about 28)
- **Stop:** `InitialStop`, `StopAtrMult`, `StopAtrLength`, `StopTicks`, `StopSwingBars`, `StopPct`.
- **Targets:** `TargetMode`, `T1R`, `FinalTargetR`, `SingleLotMode`.
- **Break-even:** `BreakEven`, `BreakEvenR`, `BreakEvenOffsetTicks`.
- **Trail:** `TrailMode`, `TrailStart`, `TrailStartR`, `TrailAtrMult`, `TrailAtrLength`, `TrailBars`, `TrailEmaLength`.
- **Clock and events:** `TimeStopBars`, `TimeStopMinutes`, `SessionExit`, `FlatByHHMM`, `MaxHoldDays`, `NewsExitMinutes`, `RollExit`.
- **Adds and control:** `AddStopMode` (`EAddStopMode`, from PR #117 §7.7), `ExitControl`, `ControlExitBars`.

Each gets a header §17 block. Mode-specific inputs are ignored when their mode is off (Interactions line). The embedded engine has no Inputs of its own: the host declares the ones its spec varies.

## 5. R accounting, adds and Portfolio Trader compatibility
- **R definition.** Initial 1R = |entry fill − initial (widened) stop| × BPV × contracts. Embedded: the structural `pm.R$` and widened `pm.R$w` exactly as `portfolioArm` publishes them.
- **Adds.** The entry owns the add decision (`AddMode`, `MaxAdds`, `AddContracts`, `AddMinR`). The engine owns the stop move on an add fill (`AddStopMode`: `RaiseToAddLow` / `KeepCurrent`) and the **R re-base** on the add fill:
  - by segments, Σ segment P&L ÷ segment 1R, with `R1` = the full post-add risk at the new stop, priced at the fill bar's close;
  - partial exits stay in their segment;
  - both `R` and `Rorig` are journalled.
  This is identical to PR #117 overview §7.7 and PR #118 §3a, so chart and PT R agree. `AddMinR` uses the current segment's 1R; stop/target triggers use the initial R (owner-approved 2026-11-11 08:58 ET).
- **The wrapper** detects an add as "the position grew in the same direction" and applies the same re-base. It cannot see `pm.add`.
- **Portfolio Trader.**
  - Embedded: the engine calls `portfolioStop(stop)` on every stop move (BE, trail, add raise) and on an add fill, so `pm.riskOpen` and `pm.openR` stay right, and it uses `portfolioArmAdd` (PR #118).
  - The MM never moves stops and never gates exits (PR #117 overview §7.5; m-port-spec §5.2 "Exits are never touched"). `ForceClosePosition` (fills at the close of bar T) is precedence 1.
  - **Wrapper in PT:** not allowed until native check E4 shows how two signals share `PortfolioData`. The entry strategy's `pm.riskOpen` would otherwise use the armed stop, not the wrapper's. PROPOSED: wrapper = charts only in rev 0.1.
- **Journal and reporting.** CS-1 journal row per trade (existing columns). GX adds:
  - `exitReason` (the §4.10 reason: `STOP`, `T1`, `TARGET`, `BE-STOP`, `TRAIL`, `TRAIL-THROUGH`, `NEWS`, `FLATBY`, `ROLL`, `TIME`, `CONTROL`, `FLAT-BY-MM`);
  - `rMultiple` (re-based) and `rOrig`;
  - `mfeR` / `maeR` on the initial R (MC `MaxRunUp`/`MaxDrawDown` ÷ initial risk, existing);
  - `segments`;
  - `gxPackage` (a hash of the effective GX settings, so a run can be matched to its exit package).
- **Per-run summary:** expectancy in R, the R distribution by exit reason, MFE/MAE scatter in R (an MFE histogram for winners and losers, where the T1/trail levels sit), and the CS-8 forward MFE/MAE for the same signals.
- **Acceptance:** the programme's usual criteria (PR #117 overview §6), unchanged.

## 6. Baseline exit package for entry-only ideas (PROPOSED, predeclared, not fitted)
This is the default of every Input above, chosen to mirror the batch-1 framework and the existing `_ExitHalfRiskHalfRoll` (half at +1R, runner trailed):

| Component | Baseline |
|---|---|
| Size | 2 contracts (PR #117 §7.2), so the scale-out is exercised |
| Initial stop | Embedded: `EntryProvided`. Wrapper: `AtrMultiple` 1.5 × ATR(14) on Data1, floored at the cost floor (ES 14 / NQ 19 ticks) |
| T1 | ⌊q/2⌋ at +1.0R (initial R), `OrderExit.Total`; q = 1 ⇒ `ExitAtT1` |
| Break-even | `AfterT1`, entry + 1 tick |
| Runner trail | `Chandelier` 3.0 × ATR(22) on Data1, from T1 (`AfterT1`), one-way |
| Final target | none (`FinalTargetR` 0) |
| Time stop | off |
| Session | intraday `FlatBy` 15:55 ET (early close: close − 5 min); swing ideas `HoldOvernight` with `ERollExit.FlattenBeforeSplice` |
| News exit | `NewsExitMinutes` 0 (matches PR #117's PROPOSED default) |
| Adds | as the entry decides; `AddStopMode` `RaiseToAddLow`; R re-based on the add fill |
| Control | `None`. Each entry-only idea also runs `FixedBars` 10 as its control arm (Q5) |
| Precedence | §4.10 |
| Costs and fills | Batch-1 settings (PR #117 §4): commissions and slippage as approved, 2 ticks of open slippage, closed-bar orders, no IOG |

## 7. Native checks for the MiniPC (backtest only, data ≤ 31 Dec 2024, temporary sandbox studies, no csproj commit)
| # | Question | How | Pass |
|---|---|---|---|
| E1 | Can an exit-only signal on the same chart manage another signal's position, and what does it see on the fill bar? | `_TestGxEntry` (buys 2 on bar 100) + `_TestGxExit` (journals `PositionSide`, `CurrentPosition.OpenTrades`, `AvgEntryPrice` each bar; sends `Sell 1 Total` at bar 105, `Sell FromAll` at bar 110) | Exit fills as sent; values visible from the fill bar's close; journal recorded |
| E2 | Fill-bar protection | Same pair on a bar where price trades through the stop inside the fill bar. Runs: (a) wrapper explicit stop, (b) wrapper `GenerateStopLoss`, (c) entry sends its stop with the entry | (c) and maybe (b) stop out on the fill bar; (a) does not. Record (b) either way |
| E3 | Two exits on one bar from two signals; Format Signals order | Two exit signals both `MarketNextBar` on bar 105 | One fill, no overshoot; record which order name fills |
| E4 | PT: two signals per strategy | Entry + wrapper in one PT strategy, MM on. Journal `Signals.Length`, `Signals[0]` name, `MyPortfolioData` reference identity from both signals (L6-style `sameRef`), and `EntryContracts` effect on the wrapper's exits | Recorded. Decides whether the wrapper may publish `pm.riskOpen` |
| E5 | Repeated partials | `Sell 1 Total` twice with one order object, with and without `ExitFromOneEntryOnce.FillAndKillExitOrder` | Behaviour recorded; GX uses separate order objects if needed |
| E6 | Pyramiding setting label | Strategy Properties dialog | Exact label of "Allow up to XXX entry orders in the same direction" recorded (shared with PR #118 A1) |
| E7 | Enum Inputs in the Format dialog and the optimiser | One enum Input | Named members shown; sweep supported or not (shared with PR #117 overview §9) |
| E8 | IOG per signal | Wrapper IOG on, entry off | Confirms per-signal; GX rule "IOG off" stays |

## 8. Implementation plan (after approval; separate PRs)
1. **GX-1:** `_ExitEngine` (pure C# in a `.Function.CS` file): state machine, enums, precedence, R segments; `tests/ExitEngineTests` (`dotnet run`, like `tests/PortMMTests`). No MC dependency.
2. **GX-2:** `_CustomSignal` integration as a protected helper: `exitPlan(...)` at entry, `exitBar()` each bar, `portfolioStop` hooks, CS-1 columns. No new base `[Input]`.
3. **GX-3:** `_GenericExit` wrapper in `sandbox/` (header §17 for every Input), chart only.
4. **GX-4:** native checks E1–E8, recorded in `specs/live-check-sheet.md`.
5. **GX-5 (optional):** batch-1 specs adopt the engine instead of hand-coding §7. Results must match their hand-coded versions trade-for-trade on ≤ 2024 data.

Regression: G0, D9a and the existing suites unchanged, because GX is inert unless a strategy calls it.

## 9. Open questions for Ettienne
- **Q1** Approve option C (an engine inside the entry strategies plus one thin wrapper), rather than one big signal (A) or a family of signals (B)?
- **Q2** Baseline values: 1.5 × ATR(14) initial stop (wrapper), T1 half at +1R, break-even + 1 tick after T1, runner on a 3 × ATR(22) chandelier, flat by 15:55. All PROPOSED; are they acceptable as the predeclared package?
- **Q3** Is the wrapper chart-only until native check E4 passes (PROPOSED), or should it be made PT-capable straight away by having it publish the stop keys?
- **Q4** Should every entry-only idea also run a control exit (`FixedBars` 10 and/or `RandomBars`) and report CS-8 forward MFE/MAE, as the three-way entry test in §4.9? PROPOSED: yes, `FixedBars` 10.
- **Q5** What value should `FixedBars` / `ControlExitBars` take per timeframe (10 bars on 5m ≈ 50 minutes)?
- **Q6** Should the wrapper's default stop be `AtrMultiple`, or `SignalBarExtreme` (as `_ExitHalfRiskHalfRoll`)?
- **Q7** Wrapper, when the derived risk exceeds the 1% budget (it can't skip an entry it did not make): manage the trade normally and journal it (PROPOSED), or flatten immediately?
- **Q8** Keep the legacy exit studies (`_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit`, `_ExitHalfRiskHalfRoll`) unchanged (PROPOSED), or mark them superseded once GX exists? Nothing is deleted by this spec.
- **Q9** Should batch-1 specs later move onto the engine (GX-5), or stay hand-coded?
