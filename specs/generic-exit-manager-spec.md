---
title: "Generic trade-management and exit layer (GX) for entry-only ideas — PROPOSAL"
rev: "0.9 PROPOSAL, 2026-11-11 (ET); rev 0.9 = MiniPC E1–E8 native results recorded (scratch `_TestGx*`, §2, §3, §4.9, §5.1, §7, §8, §9); rev 0.8 = owner approval 18:54 ET of the §4.9 other-timeframe rule and the ETrailStart.AtTrigger rename (§4.4, §4.9, §6, §9); rev 0.7 = owner answers 18:18–18:21 ET to Q4, Q5, Q6, Q8, Q9 and the final-target / trail-start unit question (§4.1, §4.2, §4.4, §4.9, §6, §8, §9); rev 0.6 = T1 already passed at the fill ⇒ skip T1, trail all, Info line (owner 09:52 ET, §4.2); rev 0.2 = owner answers of 09:16 ET; rev 0.3 = planned vs actual risk % logging (owner request 09:19 ET, §5.3); rev 0.4 = Q10 answers 09:33 ET (whole-trade MAE, strategy equity only, 1.00 and 1.10 reported separately); rev 0.5 = short side explicit in every stop/target/BE/trail rule (owner 09:34 ET)"
status: "PROPOSAL, docs only, no code. Every owner question is answered (09:16, 09:33, 09:34, 09:52, 18:18–18:21 and 18:54 ET). Native MiniPC checks E1–E8 recorded 2026-11-11 on scratch studies (§7). Still open: re-check E2, E4 and E7 on the implementation build; scratch `_TestGx*` cleanup awaits owner approval. Not yet approved for implementation as a whole. Anything else not quoted from the owner is PROPOSED."
requested_by: "Ettienne Hugo, 2026-11-11 09:02 ET (verbatim below)"
code_refs: "origin/main @ 47ffda2 (2026-11-10). Batch-1 framework references are to PR #117 (branch feat/strategy-specs-batch1, specs/strategies/00-programme-overview.md rev 1.6) and the M-PORT add-legs draft PR #118 (specs/m-port-add-legs-spec.md)."
holdout: "No price data on or after 2025-01-01 is viewed or reported by anything in this spec. Native checks use data ≤ 31 Dec 2024."
---

# Generic trade-management and exit layer (GX), rev 0.9 PROPOSAL

**Owner answers, 2026-11-11 09:16 ET (verbatim):** *"1. Yes, this approach is fine. 2. Yes, they are good - I assume we'd be able to set the first target and break-even profit percentage (or amount). 3. No, we can make the standalone strategy work in portfolio trader as well for now. 4. Keep the position open and log the warning."*
- 1 → §3: option C approved.
- 2 → §4.2, §4.3, §6: baseline values approved; the first target and break-even are configurable, with a typed trigger unit.
- 3 → §5.1: the wrapper works in Portfolio Trader now.
- 4 → §4.1, §5.2: an over-budget trade stays open and logs a Warning.

**Owner answers, 2026-11-11 18:18–18:21 ET (verbatim):** *"Q4 yes, but set ControlExitBars per timeframe"*; *"Q5: 1m 30, 5m 12, 15m 8, 60m 6, daily 5"*; *"Q6: AtrMultiple default, SignalBarExtreme selectable"*; *"Q8: mark the legacy exit studies superseded once GX exists"*; *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"*; and on the final-target / trail-start unit: *"Yes, the final target and trail start both get ETriggerUnit"*.
- Q4 → §4.9, §6: every entry-only idea also runs a `FixedBars` control arm with a per-timeframe bar count, and reports CS-8 forward MFE/MAE.
- Q5 → §4.9: `ControlExitBars` defaults per bar timeframe, as five native int Inputs.
- Q6 → §4.1, §6: wrapper default stop `AtrMultiple`; `SignalBarExtreme` selectable.
- Q8 → §1.1, §8: the legacy exit studies are marked superseded once GX exists; nothing is deleted.
- Q9 → §8: GX-5 is a planned milestone: batch 1 moves onto the engine after the E-checks pass, with a per-strategy equivalence re-check.
- Units → §4.2, §4.4: the final target and the trail start get `ETriggerUnit` Inputs; defaults keep today's behaviour.

**Owner answer, 2026-11-11 18:54 ET (verbatim):** *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*.
- → §4.9: the other-timeframe rule (nearest listed timeframe at or below) is approved.
- → §4.4: the `ETrailStart.AtR` → `AtTrigger` rename is approved.

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
**Superseded once GX exists** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q8: mark the legacy exit studies superseded once GX exists"*): `_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit` and `_ExitHalfRiskHalfRoll` get a header note and a docs entry marking them superseded by GX when GX-3 is merged (§8). They stay compiled and unchanged otherwise; **nothing is deleted**.

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
| F1 | Several signals applied to **one chart** work as **one strategy**: signal A can enter and signal B can exit. Signals on different charts are independent | MC staff, forum *"Order handling in signal / multiple signals"* (viewtopic t=46258). The repo's exit-only studies (§1.1) rely on it. **Confirmed natively (E1, 2026-11-11): the exit-only signal filled `GXX1T` then `GXXA` as sent, on 1m, 2m, 5m, 15m, 60m and daily** |
| F2 | `OrderExit.FromEntry(IOrderObject)` can only tie an exit to an entry **created in the same signal**. A separate exit signal cannot name another signal's entry | Programming Guide v1.1 (2014-11-19), "OrderExit"; Help Center page *"Entry"*: *"An exit can only be tied to an entry within the same signal"*. **Documented** |
| F3 | `OrderExit.FromAll` with a quantity closes **that quantity from each entry**. With an initial entry plus adds, "Sell 1 FromAll" can sell more than 1 contract. `OrderExit.Total` closes exactly the quantity given, oldest entry first | Programming Guide v1.1, "Exit types" worked example (buy1 5, buy2 1, buy3 2; Sell FromAll 2 sells 5). **Documented.** GX rule: partial exits use `OrderExit.Total`; full exits use `FromAll` with no quantity |
| F4 | By default an exit can be applied to one entry only once. `[ExitFromOneEntryOnce(ExitFromOneEntryOnce.FillAndKillExitOrder)]` lets the same exit order be reused for repeated partials | Programming Guide v1.1. **Confirmed natively (E5, 2026-11-11): the same order object sent twice in one trade does not fill twice (3→2, then 2→0 at the FromAll); with the FillAndKill attribute it does (3→2→1→0). GX uses separate order objects per partial. The attribute is per class, so in the embedded engine it would apply to every exit of the host signal** |
| F5 | Pyramiding requires Strategy Properties *"Allow up to XXX entry orders in the same direction…"* | Programming Guide v1.1, rule 4. **Confirmed (E6, PASS by reference to PR #118 A1, 2026-11-11):** the installed label is *"Allow up to 3 entry orders in the same direction as the currently held position:"*, with the two radios in that check |
| F6 | `PositionSide`, `CurrentPosition` (`OpenTrades`, `MaxRunUp`, `MaxDrawDown`), `StrategyInfo.MarketPosition` and `StrategyInfo.AvgEntryPrice` describe the **strategy's** (all signals') position | Implied by F1 and used by `_ExitHalfRiskHalfRoll` and `_StopsAndTargets`. **Confirmed natively (E1, 2026-11-11):** on the fill bar's close the exit signal sees `MarketPosition` = 2, `PositionSide` = 1 (long; the side, not the size), `OpenTrades` = 1, `AvgEntryPrice` = the fill, and the `GXLE` leg |
| F7 | **Fill-bar protection.** An entry signal can send its stop **with** the entry ("also protects the entry bar", engines manual v3.2 TS-01 example, `m_lxStp.Send(m_stop)` next to `m_le.Send`). A separate exit signal does not know a pending entry exists, so its explicit stop starts only after the fill bar has closed: engines manual v3.2 "the fill bar itself is unprotected", and `_Test10`'s pitfall note. MC's built-in `GenerateStopLoss` protects the fill bar **only when it is called every bar, including while flat**. Called only while in a position (the `_StopsAndTargets` pattern), it behaves like an explicit wrapper stop and starts on the next bar | Repo docs (engines manual v3.2 L739–741, L1056–1058; `_Test10` L108–109). **Confirmed natively (E2, 2026-11-11, 20 trades, 1-tick stop):** (a) wrapper explicit stop, 0 of 15 fill-bar exits, stop from the next bar, net −$450; (d) `GenerateStopLoss` only in a position, same 0 of 15 and −$450, order name "Stop Loss"; (b) `GenerateStopLoss` every bar, 15 of 15 fill-bar exits named "Stop Loss" (the exit-only signal never sees those positions); (c) stop sent with the entry (`GXLSe`), 14 of 14 fill-bar exits (the through-count is 14 rather than 15 because that stop is the entry-bar close minus 1 tick, not the fill minus 1 tick). **A separate wrapper cannot protect the fill bar.** The fill-bar stop must come from the entry, or from `GenerateStopLoss` called every bar. Re-check the chosen mechanism on the implementation build |
| F8 | In PT, an `IPortfolioStrategy` has `Signals : IStrategy[]` (several signals per instrument). The MM reads `Signals[0]` (`_PortfolioRiskManager.view` L125) and the strategy's `PortfolioData`. `EntryContracts` applies to every **entry** order of the strategy; exits are never gated | m-port-spec §1, §6; `_PortfolioRiskManager.Strategy.CS` L111, L125. **Confirmed natively (E4, 2026-11-11, scratch MM):** `MyPortfolioData` is one dictionary per strategy (`sameRef` True from both signals and the MM). `signals.Length` is 2 with the entry at `[0]` when listed first. The own index is readable at `StartCalc` via `PortfolioData` matching, but `Signals[i].Bars` throws `Unaccessible property(method): Bars` at `StartCalc`, so defer `Bars` reads to the first bar. Calc order equals list order. Deny-all (`AllowEntries = false`, `EntryContracts = 0`) leaves the wrapper's exits unchanged. Re-check with the real wrapper and `_PortfolioRiskManager` |
| F9 | IOG is unavailable in PT, and so is Bar Magnifier. On a chart, IOG is a per-signal setting | m-port-spec §4 item 4. **Confirmed natively (E8, 2026-11-11):** with the exit IOG on and the entry off, the exit logged `calls(4) insideCalls(2)` and `iogEnabled(True)` every bar, the entry `calls(1) insideCalls(0)` and `iogEnabled(False)`. The exit filled intrabar at bar 105 (vs 106 in E1) and FromAll at bar 110 intrabar. The GX rule "IOG off" stays, because this would break chart/PT parity |

**Consequences.**
1. A stand-alone exit signal **works** on a chart and in PT as a second signal (F1, F8). But:
   - it cannot use `FromEntry` (F2);
   - it must use `Total` for partials (F3);
   - it does not know the entry's planned or structural stop, so it must derive R itself;
   - it leaves the fill bar unprotected unless it calls `GenerateStopLoss` every bar, including while flat (F7). Calling it only while in a position, or sending an explicit stop from the wrapper, starts the stop on the next bar;
   - in PT, the entry strategy's `pm.riskOpen` would not follow the exit signal's stop (F8) unless the exit signal publishes it. The wrapper publishes it (§5.1); E4 confirmed the shared dictionary, list-order calc and `riskAtOpen` match on the scratch studies (re-check on the implementation build).
2. An exit engine running **inside** the entry signal has none of these problems. It sends the stop with the entry, knows the structural and widened stops, calls `portfolioStop`, journals through CS-1, and can use `FromEntry` per leg.

## 3. Options compared

| | (A) One generic exit signal with many typed-enum Inputs | (B) A family of stacked exit signals (stop, target/scale, trail, time/session) | (C) A shared C# exit engine used by entry strategies, plus one thin wrapper signal |
|---|---|---|---|
| Where the logic runs | A separate signal on the chart / PT strategy | 3–5 separate signals | Inside the entry signal (`_CustomSignal` helper class). The wrapper is a separate signal only for entries that cannot embed it |
| Input count | About 28 on one dialog (§4.11) | About 6–9 each, but 4 dialogs and 4 "same strategy" set-ups per test | Embedded: 0 new Inputs by default (the host exposes the few it varies, header §17); wrapper: about 28, same as A |
| Knows the entry's stop / R | No: derives R (F2, F7) | No | **Yes** (embedded); wrapper: derives R |
| Fill-bar protection | Only through `GenerateStopLoss` called **every bar, including while flat** (E2). An explicit wrapper stop, or `GenerateStopLoss` only while in a position, starts on the next bar | Same, in the stop module | **Yes**: the stop is sent with the entry |
| Same-bar precedence (§7.4) | One place decides; one reason is journalled | **Not controllable**: each signal sends its own exit, and MC fills whichever triggers. E3 (2026-11-11): no overshoot (2→0 at bar 106). FromAll+FromAll: only the first-listed signal's order fills (`GXXA` normally, `GXBA` when swapped). Total+Total: both fill, 1 each. FromAll+Total: FromAll takes 2. List order decides. Do not build B | One place decides (the engine) |
| Optimiser sweeps | Fine for numeric Inputs, one dialog. **Enum Inputs cannot be swept** (E7, 2026-11-11): named members show in the Format Inputs dropdown, but the optimiser lists only numerics. The implementation should expose a numeric optimiser companion (or document the limitation). Re-check on the implementation build | Awkward: a sweep spans several signals | Embedded: the host exposes the exact Inputs a registered ablation needs. Wrapper: as A |
| Testability off-MC | Possible if the logic sits in a pure class | Each module separately; the interactions are not | **Best**: one pure `_ExitEngine` state machine, unit-tested like `_PortMM` under `tests/`, fed by bar and fill events |
| Portfolio Trader | Works as a second signal, but `pm.riskOpen` / `pm.openR` are wrong unless the exit publishes (E4) | Same, with more signals | **Embedded: full M-PORT compatibility** (`portfolioStop`, `portfolioArmAdd`, R re-base §5). Wrapper: PT-capable as the second signal, publishing the stop keys (§5.1, owner-approved 09:16 ET; verified by E4) |
| Adds (PR #117 §7.7, PR #118) | Sees the position grow, so it can re-base R, but cannot see `pm.add` | Same | The engine owns the add path: `AddStopMode`, re-base, `portfolioArmAdd` |
| Risk of drift between strategies | Low | Medium (module versions) | **Lowest**: one engine. Batch-1 specs can later adopt it instead of hand-coding §7 |
| Effort | Medium | High (4 signals, plus precedence glue) | Medium–high once; then low per strategy |

**Recommendation: option C** (**Owner-approved 2026-11-11 09:16 ET**: *"1. Yes, this approach is fine."*).
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
| Member | Long stop | Short stop | Inputs |
|---|---|---|---|
| `EntryProvided` (embedded default) | the entry's own structural stop (the one passed to `sizeForRisk` / `portfolioArm`) | same | — |
| `AtrMultiple` (wrapper default; **Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q6: AtrMultiple default, SignalBarExtreme selectable"*) | entry − `StopAtrMult` × ATR(`StopAtrLength`) | entry + `StopAtrMult` × ATR(`StopAtrLength`) | `StopAtrMult` (double, 1.5), `StopAtrLength` (int, 14); ATR on Data1 from closed bars |
| `FixedTicks` | entry − `StopTicks` × tick | entry + `StopTicks` × tick | `StopTicks` (int, 0 = off ⇒ invalid with this mode) |
| `SignalBarExtreme` (selectable, owner-approved 18:18–18:21 ET) | low of the bar before the fill − 1 tick | high of the bar before the fill + 1 tick | — (`_ExitHalfRiskHalfRoll`'s R) |
| `SwingExtreme` | lowest low of the last `StopSwingBars` closed bars − 1 tick | highest high of the last `StopSwingBars` closed bars + 1 tick | `StopSwingBars` (int, 5) |
| `Percent` | entry × (1 − `StopPct`/100) | entry × (1 + `StopPct`/100) | `StopPct` (double); `_StopsAndTargets`-compatible |

- **Floor:** the stop's distance from entry is always at least the M-INSTR cost floor (ES 14, NQ 19 ticks), via `CostModel.WidenedStop`. A long stop is never above entry − floor; a short stop is never below entry + floor.
- **Embedded:** a trade whose widened risk is > 1% of equity is skipped by `sizeForRisk` before entry (P1).
- **Wrapper:** it cannot skip an entry it did not make. When the derived risk exceeds the 1% budget, it **keeps the position open, manages it normally**, and logs one Warning (**Owner-approved 2026-11-11 09:16 ET**: *"4. Keep the position open and log the warning."*). Exact line and dedupe in §5.2.

### 4.2 Targets and scale-out
**Configurable first target (**Owner-approved 2026-11-11 09:16 ET**: *"I assume we'd be able to set the first target and break-even profit percentage (or amount)"*).** The trigger level and the fraction scaled out are Inputs. The unit of the trigger is a typed enum shared with break-even (§4.3).

```csharp
public enum ETargetMode { ScaleOutThenRunner, SingleTarget, None }
public enum ESingleLotMode { ExitAtT1, RunnerOnly }   // existing name and default from PR #117 §7.2
public enum ETriggerUnit { RMultiple, PercentOfEntry, Ticks, Points, CurrencyPerContract }   // default RMultiple
```

**`ETriggerUnit` measures a profit distance d from the initial position's average entry price, in the trade direction** (long: level = entry + d; short: level = entry − d):
- `RMultiple` (default): d = value × initial R per contract, in price. With the 08:58 ET ruling, triggers use the **initial-entry** R.
- `PercentOfEntry`: d = entry × value / 100. Example: 0.5 ⇒ 0.5% of entry.
- `Ticks`: d = value × tick.
- `Points`: d = value price points. ES: 1 point = 4 ticks = $50 per contract.
- `CurrencyPerContract`: d = value ÷ BigPointValue. The open profit per contract reaches value $. Example: ES $250 ⇒ 5 points.
- Values must be > 0 when the feature is on. A level that does not land on a tick is rounded **away** from the entry to the next tick, so a target is never closer than asked. A value ≤ 0 with the feature on is an input error: logged at Error, and the feature is treated as off.

**Inputs:**
| Input | Type | Default | Meaning |
|---|---|---|---|
| `TargetMode` | `ETargetMode` | `ScaleOutThenRunner` | as below |
| `T1Unit` | `ETriggerUnit` | `RMultiple` | unit of `T1Value` |
| `T1Value` | double | 1.0 | first-target distance in `T1Unit` (baseline +1R) |
| `T1ScalePct` | int, 0–100 | 50 | percent of the **initial** position closed at T1 |
| `FinalTargetUnit` | `ETriggerUnit` | `RMultiple` | unit of `FinalTargetValue` (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Yes, the final target and trail start both get ETriggerUnit"*) |
| `FinalTargetValue` | double | 0 (off) | optional final (runner) target distance from entry in `FinalTargetUnit`; long level = entry + d, short level = entry − d (replaces rev 0.6's `FinalTargetR`; with the defaults it is the same rule) |
| `SingleLotMode` | `ESingleLotMode` | `ExitAtT1` | q = 1 rule |

**T1 quantity**, with q = the initial position's contracts:
- `T1ScalePct` = 0 ⇒ no T1 order; the position is managed as a runner.
- `T1ScalePct` = 100 ⇒ the whole initial position.
- Otherwise `qT1 = max(1, floor(q × T1ScalePct / 100))`, capped at q − 1 so a runner remains. The 50% default keeps PR #117 §7.2: ⌊q/2⌋, and the runner gets the odd contract (2 contracts ⇒ 1 + 1).
- q = 1 ⇒ `SingleLotMode` decides (default `ExitAtT1`, owner-accepted 2026-11-10 for batch 1).
- Orders: limit `<ID>LT1`, `OrderExit.Total` (F3).

**Modes:**
- `ScaleOutThenRunner` (default): T1 as above. The runner is managed by §4.4/§4.5. `FinalTargetValue` > 0 adds a runner limit `<ID>LT` (short: `<ID>ST`, a buy-to-cover limit below the entry, PR #117 §7.1).
- `SingleTarget`: the whole position at the final target (`FinalTargetValue` must be > 0). `T1*` inputs are ignored.
- `None`: no target; exits come only from the stop, trail and clock.

T1 and the split use the **initial** contracts only. Adds join the runner (PR #117 §7.7).

**T1 already passed at the fill** (**Owner-approved 2026-11-11 09:52 ET**, Ettienne: *"Yes, trail the whole position, log an info message."*; same rule as PR #117 overview §7.2a). If the entry fill is at or beyond the T1 level (long: fill ≥ T1; short: fill ≤ T1), the engine sends no T1 order, treats the whole position as the runner (the §4.4 trail is active from the fill, as if T1 had filled), keeps the final target if it is still beyond the fill, and logs once: `<ID> Info: T1-SKIPPED side(<±1>) fill(<px>) t1(<px>) past(<ticks>) qty(<q>) action(trail-all)`. The journal row gets `t1Skipped = 1`. In `SingleTarget` mode a target already passed at the fill is handled the same way (no limit, trail all).

### 4.3 Break-even
**Configurable trigger and offset (**Owner-approved 2026-11-11 09:16 ET**, answer 2).**

```csharp
public enum EBreakEven { AfterT1, AtTrigger, Off }
```

| Input | Type | Default | Meaning |
|---|---|---|---|
| `BreakEven` | `EBreakEven` | `AfterT1` | when the stop moves to break-even |
| `BreakEvenUnit` | `ETriggerUnit` | `RMultiple` | unit of `BreakEvenValue` (used by `AtTrigger`) |
| `BreakEvenValue` | double | 1.0 | trigger distance from entry; a **closed** bar must reach it (long: close ≥ entry + d; short: close ≤ entry − d) |
| `BreakEvenOffsetTicks` | int, ≥ 0 | 1 | the new stop = entry + offset ticks (long) or entry − offset ticks (short), so the BE stop covers about a tick of cost; 0 = exactly entry |

- `AfterT1` (default): when T1 fills, stop = entry + `BreakEvenOffsetTicks` × tick (long) or entry − `BreakEvenOffsetTicks` × tick (short). If `T1ScalePct` = 0 or `TargetMode` ≠ `ScaleOutThenRunner`, `AfterT1` falls back to `AtTrigger` with the same unit and value; this is logged once per run at Info.
- `AtTrigger`: on the first closed bar at or beyond the trigger (long: close ≥ entry + d; short: close ≤ entry − d; d from `BreakEvenUnit` / `BreakEvenValue`), the stop moves to the break-even level above from the next bar.
- `Off`: no break-even move.
- "Entry" is the **initial** position's average entry price. After an add, break-even still refers to the initial entry (stop/target triggers stay on the initial-entry R, owner-approved 08:58 ET), and the add's own stop raise (`AddStopMode`) can only tighten further.
- One-way (§7.3): break-even never loosens a stop that is already tighter, for example after an add raise. Long: never lower a stop that is already higher. Short: never raise a stop that is already lower.

### 4.4 Trailing stop (runner)
```csharp
public enum ETrailMode { Chandelier, Atr, BarExtreme, Ema, RollLevel, None }
public enum ETrailStart { AfterT1, AtTrigger, Immediately }   // rev 0.7: AtR renamed AtTrigger (unit now an Input); rename owner-approved 18:54 ET
```
**Both directions** (**Owner-approved 2026-11-11 09:34 ET** (Ettienne: *"I see in the question above you asked "Runner: trails 3 × ATR(22) below the highest high, and only ever moves up."  note that we want that to trail also down for short moves."*)). The runner trails **below the highest high for longs** and **above the lowest low for shorts**. A long trail only ever moves **up**; a short trail only ever moves **down**.

| Member | Candidate stop, long | Candidate stop, short | Inputs |
|---|---|---|---|
| `Chandelier` (default) | highest high since entry − `TrailAtrMult` × ATR(`TrailAtrLength`) | lowest low since entry + `TrailAtrMult` × ATR(`TrailAtrLength`) | `TrailAtrMult` 3.0, `TrailAtrLength` 22 |
| `Atr` | last close − `TrailAtrMult` × ATR | last close + `TrailAtrMult` × ATR | same |
| `BarExtreme` | lowest low of the last `TrailBars` closed bars − 1 tick | highest high of the last `TrailBars` closed bars + 1 tick | `TrailBars` (int, 2) |
| `Ema` | EMA(`TrailEmaLength`) of closes − 1 tick | EMA(`TrailEmaLength`) of closes + 1 tick | `TrailEmaLength` (int, 20) |
| `RollLevel` | `_ExitHalfRiskHalfRoll`: entry-bar low − 1 tick, then the prior day's low − 1 tick | mirror: entry-bar high + 1 tick, then the prior day's high + 1 tick | — (needs a daily series, M-DAILY) |
| `None` | no trail | no trail | — |

- "Since entry" means from the entry fill bar through the last **closed** bar; ATR and EMA are on Data1 closed bars.
- `TrailStart` default `AfterT1`. `AtTrigger` starts the trail on the first **closed** bar at or beyond d from the initial entry, with d from `TrailStartUnit` (`ETriggerUnit`, default `RMultiple`) and `TrailStartValue` (double, default 1.0) (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Yes, the final target and trail start both get ETriggerUnit"*; replaces rev 0.6's `TrailStartR`, same default behaviour = +1R): **long** close ≥ entry + d; **short** close ≤ entry − d. `Immediately` starts at the fill. **Rename `AtR` → `AtTrigger`:** **Owner-approved 2026-11-11 18:54 ET**, Ettienne: *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*.
- **One-way, both sides** (§7.3). The candidate is computed at the bar close and sent from the next bar.
  - **Long:** `newStop = max(oldStop, candidate)`, capped at one tick **below** the last close.
  - **Short:** `newStop = min(oldStop, candidate)`, capped at one tick **above** the last close.
  - A candidate at or through the market (long ≥ last close, short ≤ last close) ⇒ market exit next bar, reason `TRAIL-THROUGH` (§7.3).
- The trail never loosens a break-even or add-raise stop (same one-way rule).

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
- `FixedBars`: exit at market after N closed bars whatever happens (`<ID>LX` / `<ID>SX`, reason `CONTROL`), with the initial stop still resting as a disaster stop. Same rule for longs and shorts.
- `RandomBars`: a seeded uniform 1…N (seed = the run seed, journalled).
- **N per bar timeframe** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q4 yes, but set ControlExitBars per timeframe"*; *"Q5: 1m 30, 5m 12, 15m 8, 60m 6, daily 5"*). Five native int Inputs, each overridable (0 = control off for that timeframe):

| Input | Data1 timeframe | Default N | ≈ holding time |
|---|---|---|---|
| `ControlExitBarsM1` | 1 minute | 30 | 30 min |
| `ControlExitBarsM5` | 5 minutes | 12 | 1 h |
| `ControlExitBarsM15` | 15 minutes | 8 | 2 h |
| `ControlExitBarsH1` | 60 minutes | 6 | 6 h |
| `ControlExitBarsD1` | daily | 5 | 1 week |

  Individual int Inputs, not an enum or table, because each is a count the owner may override separately and plain int Inputs are this spec's convention for counts (`TrailBars`, `StopSwingBars`, `TimeStopBars`), and typed enums are kept for named choices.
- **Other timeframes** (**Owner-approved 2026-11-11 18:54 ET**, Ettienne: *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*): N = the value for the **nearest listed timeframe at or below** the chart's (Data1) bar size: 2–4 min ⇒ the 1m value; 10 min ⇒ 5m; 30 min ⇒ 15m; 120/240 min ⇒ 60m; weekly/monthly ⇒ daily. Below 1 minute (seconds, ticks) and non-time bars (volume, range, Renko) have no listed timeframe at or below: they use the 1m value and log one Warning `<ID> Warning: CONTROL-BARS no listed timeframe at or below <resolution>: using ControlExitBarsM1`. The timeframe is read once at `StartCalc` from `Bars.Info.Resolution` (type and size). **E1, 2026-11-11, `@ES`:** 1m is `Minute` size 1 duration `00:01:00` → M1/30; 2m is `Minute` size 2 duration `00:02:00` → M1/30; 5m is `Minute` size 5 duration `00:05:00` → M5/12; 15m is `Minute` size 15 duration `00:15:00` → M15/8; 60m is `Minute` size 60 duration `01:00:00` (not `Hour` 1) → H1/6; daily is `Day` size 1 duration `1.00:00:00` → D1/5. The scratch proxy mapped each of those to the §4.9 N. The resolved N is journalled once: `<ID> Info: CONTROL-BARS tf(<resolution>) mapped(<M1|M5|M15|H1|D1>) bars(<N>)`.
- **Every entry-only idea runs a control arm** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q4 yes, but set ControlExitBars per timeframe"*): besides the baseline package, each entry-only idea is run once with `ExitControl = FixedBars` (N as above), and reports CS-8 forward MFE/MAE for the same signals. That gives three looks at the same entry:
  1. CS-8 forward MFE/MAE, with no exit at all;
  2. the dumb exit (`FixedBars`; `RandomBars` optional);
  3. the baseline package.
  An entry edge should show in all three. The control arm is a separate registered run, not a sweep.
- These carry over `_ExitStatic`'s entry-test idea.
- `ExitControl` default `None` (the baseline arm).

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

### 4.11 Wrapper Input list (`_GenericExit`, about 37)
- **Stop:** `InitialStop`, `StopAtrMult`, `StopAtrLength`, `StopTicks`, `StopSwingBars`, `StopPct`.
- **Targets:** `TargetMode`, `T1Unit`, `T1Value`, `T1ScalePct`, `FinalTargetUnit`, `FinalTargetValue`, `SingleLotMode`.
- **Break-even:** `BreakEven`, `BreakEvenUnit`, `BreakEvenValue`, `BreakEvenOffsetTicks`.
- **Trail:** `TrailMode`, `TrailStart`, `TrailStartUnit`, `TrailStartValue`, `TrailAtrMult`, `TrailAtrLength`, `TrailBars`, `TrailEmaLength`.
- **Clock and events:** `TimeStopBars`, `TimeStopMinutes`, `SessionExit`, `FlatByHHMM`, `MaxHoldDays`, `NewsExitMinutes`, `RollExit`.
- **Adds and control:** `AddStopMode` (`EAddStopMode`, from PR #117 §7.7), `ExitControl`, `ControlExitBarsM1`, `ControlExitBarsM5`, `ControlExitBarsM15`, `ControlExitBarsH1`, `ControlExitBarsD1`.

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
  - **Wrapper in PT:** supported now (§5.1). E4 passed on the scratch studies; re-check on the implementation build.

### 5.1 `_GenericExit` in Portfolio Trader (**Owner-approved 2026-11-11 09:16 ET**: *"3. No, we can make the standalone strategy work in portfolio trader as well for now."*)
The wrapper runs in PT as the **second signal** of the same PT strategy (one row per instrument), with the entry signal first. Facts used:
- The MM reads `Signals[0]` for `PositionSide` and currency (`_PortfolioRiskManager.view` L125–129), and the strategy's `PortfolioData` for all `pm.*` keys (L131–158).
- The MM runs after all strategies in a step (Check 7 L1).
- Signals in one strategy calculate in Signals-list order (m-port-spec §4 item 2, from the MC wiki; **re-verified by E4**).

**Requirements:**
1. **Order.** In the PT strategy's signal list, the **entry signal is first** (`Signals[0]`) and `_GenericExit` is second. That gives:
   - the MM's `Signals[0]` reads (position sign, currency, `InitialCapital`) come from the entry, as today;
   - in each bar step the wrapper calculates **after** the entry, so its `pm.*` writes are the last ones the MM sees in that step.

   The wrapper checks this at `StartCalc`. **E4, 2026-11-11:** the own index is readable at `StartCalc` via `PortfolioData` matching (ES 0, NQ 1; `mySignalIndexByName` / `ByRef` also worked). `Signals[i].Bars` throws `Unaccessible property(method): Bars` at `StartCalc`, so any `Bars` read is deferred to the first bar. If the wrapper is not after a `pm.ver` publisher, it logs `EXIT Error: _GenericExit must follow the entry signal in the Portfolio Trader signal list` and stays inert, sending no exits. The swap run supports that Error rule: with the exit listed first, `followsEntry` was N, the exit saw the entry's previous-step data (`entryStepSeen` = bar−1, `entryCalculatedFirst` N), and the MM `lastWriter` was ENTRY (vs EXIT when the exit is listed second).
2. **The entry must publish (M-PORT).** The MM denies any strategy without `pm.ver` ("no pm.ver"). In PT, the entry must therefore be a `_CustomSignal` publisher that arms its pending entry through `sizeForRisk` / `portfolioArm` with a **planned stop**.
   - To keep sizing and R consistent, the entry should compute that planned stop with the **same GX rule** the wrapper will use: a static `_ExitEngine.InitialStop(mode, …)` helper, the same function the wrapper calls.
   - An entry that cannot publish (a third-party or legacy `SignalObject`) runs in PT only **without** the MM signal. The wrapper then manages exits and journals R, but there is no book-level control. This limitation is logged once at `StartCalc`.
3. **Who writes which key.** `PortfolioData` is one dictionary per PT strategy (**confirmed E4, 2026-11-11**, L6-style `sameRef` True from both signals and from the MM).

   | Owner | Keys | When |
   |---|---|---|
   | Entry (`_CustomSignal`) | `pm.ver`, `pm.id`, `pm.family`, `pm.cluster`, `pm.state`, `pm.side`, `pm.pos`, `pm.eq`, `pm.pnl`, `pm.ret`, `pm.uRet`, `pm.vol`, `pm.day`, `pm.stamp`, and the **arm** keys `pm.R$`, `pm.R$w`, `pm.qtyCode`, `pm.riskNew` | as today |
   | `_GenericExit` (when a position is open) | `pm.riskOpen`, `pm.openR`, plus `pm.R$` and `pm.R$w` **once, on the bar the position first appears**, re-computed from the actual fill and the wrapper's initial stop; new keys `pm.gxStop` (active stop), `pm.gxOwner` = `"GX"` | every bar while in a position, after the entry's `portfolioBar()` |

   - The wrapper never writes `pm.ver`, `pm.state`, `pm.side` or any arm key while flat, and never touches `CommonPortfolioData`.
   - It overrides `PortfolioPublish => false` so the base `startPortfolio()` does not re-initialise the entry's keys (`_CustomSignal` L1702–1718).
4. **R accounting stays right**, because the MM samples after the wrapper:
   - **Sizing** (RiskParity) uses the entry's armed `pm.R$` at the arm (planned entry, planned stop).
   - **The kill / session / weekly R stream** records `riskAtOpen = pm.R$ × |pos|` on the first evaluation with a position (`_PortMM` L963–967). By then the wrapper has re-written `pm.R$` from the **actual fill** and its own stop, so the MM's 1R equals the wrapper's 1R. The difference between planned and actual R is journalled: `EXIT Info: R fill <id> planned $<armR> actual $<fillR>`.
   - **Heat / cluster / total caps** use `pm.riskOpen`, which the wrapper keeps equal to its active stop (BE, trail, add raise).
   - **Adds and the R re-base** (PR #118 §3a) need PR #118 to be built. The wrapper owns the stop raise (`AddStopMode`) and publishes `pm.addStop` / `pm.px` on the add-fill bar. Until PR #118 lands, the MM refuses adds, so PT wrapper runs have no adds.
5. **Exits are never gated.** `EntryContracts` and `AllowEntries*` apply only to entry orders, and the wrapper sends exits only. **E4-b, 2026-11-11:** deny-all (`AllowEntries = false`, `EntryContracts = 0`) left the exits identical to E4-a (0 diffs): NQ stopped at bar 104, ES T1 filled at bar 106 and FromAll at bar 116. The MM's `ForceClosePosition` closes all legs (precedence 1). `Paused` is set only when flat (Check 7 L4).
6. **Known limitation (documented).** If the signals of one strategy do **not** share one `PortfolioData`, or the calc order is not the list order, the wrapper's keys never reach the MM. The fallback, proposed for that case only: the entry hosts the engine (option C embedded) for PT runs. **E4, 2026-11-11, did not trigger this:** `sameRef` was True, calc order was the list order, and `RISKATOPEN` matched on both rows (ES R$100 / `riskAtOpen` $200; NQ $40 / $80). Re-check with the real `_GenericExit` wrapper and `_PortfolioRiskManager` before any production PT result is used.

### 5.2 Over-budget trade in the wrapper (**Owner-approved 2026-11-11 09:16 ET**: *"4. Keep the position open and log the warning."*)
- **Test**, on the first bar the wrapper sees the position: `riskDollars = |fill − initial (widened) stop| × BPV × |pos|` > `0.01 × equity`. Equity is `InitialCapital + NetProfit + OpenProfit`, the same base as `sizeForRisk`'s 1% skip. In PT the MM's own per-trade budget has already checked the armed risk, so this catches only fill-versus-plan differences or a non-publishing entry.
- **Action:** none on the position. It stays open and is managed by every normal rule (stop, T1, BE, trail, clock).
- **Log:** one Warning per trade, deduped by (strategy instance, entry fill time, entry order name) and never repeated while that trade is open:
  - `EXIT Warning: GX RISK-OVER-BUDGET <id> side=<long|short> qty=<n> fill=<px> stop=<px> risk=$<r> budget=$<b> (1% of equity $<eq>) - position kept and managed normally`;
  - in PT the same text is prefixed `PORT` (`PORT Warning: GX RISK-OVER-BUDGET …`) so it sits with the MM journal;
  - the CS-1 journal row for the trade gets `riskFlag = OVER-BUDGET`.
- This is a Warning, not the Info skip line, because nothing is skipped: the trade went on and exceeded the rule.

### 5.3 Planned vs actual risk % on every closed trade (owner request, 2026-11-11 09:19 ET)
**Owner (verbatim):** *"Another thing we'd like to log when a trade is closed is the actual risk percentage given the market's movement. So we essentially have a projected risk percentage when we start out (e.g. 1%) but once the market has moved it may or may not have moved against us by a certain percentage so we want to log that as the actual risk percentage."*

**Scope.** It applies to **every** strategy, not only GX: the fields are computed in the `_CustomSignal` CS-1 journal (the base), so every `_CustomSignal` strategy logs them, and both the GX engine and the wrapper use the same code. The cross-reference is in PR #117 overview §8. The requirement is owner-requested. The three Q10 choices (whole-trade MAE, equity base, thresholds) are **Owner-approved 2026-11-11 09:33 ET** (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*). The other definition choices below are **PROPOSED**.

**Fields per closed trade:**
| Field | Definition | Unit / rounding |
|---|---|---|
| `equityAtEntry` | The **strategy's own equity** at the entry fill (see "Equity base") | $, 2 dp |
| `plannedRiskDollars` | Initial stop risk at the entry fill: \|entry fill − initial **widened** stop\| × BPV × initial contracts (the CS-1 `riskDollars`, the same 1R the trade is sized and skipped on). Excludes commission | $, 2 dp |
| `plannedRiskPct` | `plannedRiskDollars / equityAtEntry × 100`, e.g. 1.000 | percent, 3 dp |
| `plannedRiskPctPeak` | The largest planned risk over the trade: max over segments of the segment 1R $ (the post-add risk at the raised stop, §5 / PR #117 §7.7) ÷ `equityAtEntry` × 100. Equals `plannedRiskPct` for a trade with no add | percent, 3 dp |
| `maeDollars` | **Worst adverse excursion of the whole trade, from the entry fill to the exit** (owner-approved 09:33 ET, Q10.1): min over the trade's bars of (realised P&L of this trade's partial exits so far + open P&L of all open contracts, incl. adds, marked at the bar's adverse extreme). Floored at 0: a trade that never went below its entry P&L has MAE 0. Reported as a positive number | $, 2 dp |
| `actualRiskPct` | `maeDollars / equityAtEntry × 100`: how far the market actually moved against the position, as % of the account at entry | percent, 3 dp |
| `actualToPlanned` | `actualRiskPct / plannedRiskPct` = `maeDollars / plannedRiskDollars` = MAE in **initial** R. 1.00 = the full planned risk was used, > 1 = stop slippage, a gap through the stop, or risk added by adds | ratio, 2 dp |
| `maeOpenDollars` | Secondary: the worst open-only excursion of the contracts still open (ignores realised partials) | $, 2 dp |
| `realizedPnlPct` | The trade's net P&L (MC trade objects, after MC commission and slippage settings) ÷ `equityAtEntry` × 100, signed | percent, 3 dp |

**Definitions** (PROPOSED unless marked owner-approved):
- **Equity base: the strategy's own equity only, on charts and in Portfolio Trader** (**Owner-approved 2026-11-11 09:33 ET** (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*), Q10.2).
  - `equityAtEntry` = that strategy's `InitialCapital + NetProfit` at the entry fill bar (closed P&L only; the strategy is flat before the entry, so there is no open P&L).
  - It is the same number on a chart and in Portfolio Trader, so the two can be compared directly.
  - The MM book base and the broker-equity base proposed in rev 0.3 are **removed**. There is no book-based or broker-based field and no `equityBase` column.
  - Note: the MM's 1% per-trade budget and heat cap are measured against the **book** (m-port-spec §5.1). So in Portfolio Trader, `plannedRiskPct` (strategy equity) can differ from the risk % the MM approved. That is accepted; the MM journal keeps its own book-based numbers.
  - Equity is frozen at entry: later P&L does not change the denominators.
- **Intrabar MAE.** It is marked at each bar's adverse extreme (long: `Low`, short: `High`) on the bar series the trade runs on (Data1).
  - Limitation: the order of ticks inside a bar is unknown. On the **entry** bar the whole bar counts after the fill. With a stop or limit fill mid-bar, part of the bar may precede the fill, so MAE is conservative (possibly overstated).
  - On the **exit** bar the extreme is capped at the exit fill price, because the position no longer exists beyond the fill.
  - Bar Magnifier (charts only) and tick data narrow this; PT has no magnifier, so PT MAE is bar-resolution. Logged as `maeScope` (`bar` | `magnifier`).
  - MC's own `MaxDrawDown` (the current CS-1 `maeDollars`) is kept in its column for comparison. The new value is `maeTradeDollars` if the existing column name must stay unchanged (Coder chooses at implementation; both names are documented).
- **Adds.** MAE is computed on the **cumulative** position: every open contract, including adds from their fill onward, against its own fill price. The adverse move is therefore measured in $, not per contract.
  - `plannedRiskPct` stays the initial planned risk, so a trade with adds can show `actualToPlanned` > 1 without any slippage. `plannedRiskPctPeak` and `addCount` let reporting separate this.
  - The re-based R (`rMultiple`) is unchanged by this section.
- **Scale-outs** (owner-approved 09:33 ET, Q10.1: whole trade). The primary `maeDollars` includes realised P&L from partial exits, so after T1 the locked-in profit offsets later adverse moves: it is "how much of the account the trade as a whole was down at worst". `maeOpenDollars` reports the remaining-contracts view.
- **Costs and slippage.** MAE uses actual fill prices, so entry slippage is included. Commissions are **excluded** from MAE and planned risk (they are price-excursion measures) and **included** in `realizedPnlPct`.
- **Signs.** `plannedRiskPct`, `actualRiskPct`, `maeDollars` and `actualToPlanned` are ≥ 0 (adverse = positive). `realizedPnlPct` is signed (loss < 0).
- **Rounding.** Computed in double; rounded only when written (CSV: percents 3 dp, ratios 2 dp, $ 2 dp; invariant culture, `.` decimal). Percent fields are in percent units (1.000 = 1%), not fractions.
- **Undefined values.** `plannedRiskDollars` ≤ 0 or NaN (no stop known) ⇒ `plannedRiskPct`, `actualToPlanned` = empty, and `riskFlag` gets `NO-PLANNED-RISK`. `equityAtEntry` ≤ 0 ⇒ every percent empty.

**Close log line** (Journal level; prefix `EXIT` on charts, `PORT` in Portfolio Trader):
`EXIT CLOSE <id> side=<long|short> qty=<initial>+<adds> reason=<exit reason> R=<rebased> Rorig=<r> plannedRiskPct=<x.xxx> plannedRiskPctPeak=<x.xxx> actualRiskPct=<y.yyy> actualToPlanned=<z.zz> realizedPnlPct=<±w.www> mae=$<m> maeScope=<bar|magnifier> equity=$<e>` (`equity` = the strategy's own equity at entry)

In PR #117 strategies, these fields are appended to the existing `<ID> EXIT reason() px() R() mfe() mae()` line.

**Journal columns** (CS-1 `MCJournal_<runId>.csv`, appended at the end so existing readers keep working): `equityAtEntry, plannedRiskDollars, plannedRiskPct, plannedRiskPctPeak, maeTradeDollars, maeOpenDollars, actualRiskPct, actualToPlanned, realizedPnlPct, maeScope, addCount`.

**Reporting (per run):**
- the distribution of `actualRiskPct` (histogram plus 50/90/99th percentiles) next to `plannedRiskPct`;
- the distribution of `actualToPlanned`;
- **Exceeded-plan counts** (**Owner-approved 2026-11-11 09:33 ET** (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*), Q10.3: 1.00 = exceeded plan, > 1.10 reported separately). Two **separate** rows, each with a count and a share of trades:
  1. `actualToPlanned` > 1.00: the market went further against the trade than the planned stop;
  2. `actualToPlanned` > 1.10: a material overshoot (a gap or heavy slippage through the stop).

  Each row is split into:
  - (a) trades without adds: stop slippage or a gap through the stop;
  - (b) trades with adds: compared against `plannedRiskPctPeak`.

  Plus the worst trade (date, size, reason).
- `realizedPnlPct` beside it, so a deep MAE that recovered is visible.

- **Journal and reporting.** CS-1 journal row per trade (existing columns). GX adds:
  - `exitReason` (the §4.10 reason: `STOP`, `T1`, `TARGET`, `BE-STOP`, `TRAIL`, `TRAIL-THROUGH`, `NEWS`, `FLATBY`, `ROLL`, `TIME`, `CONTROL`, `FLAT-BY-MM`);
  - `rMultiple` (re-based) and `rOrig`;
  - `mfeR` / `maeR` on the initial R (MC `MaxRunUp`/`MaxDrawDown` ÷ initial risk, existing);
  - `segments`;
  - `gxPackage` (a hash of the effective GX settings, so a run can be matched to its exit package);
  - `riskFlag` (`OVER-BUDGET`, `NO-PLANNED-RISK` or empty, §5.2, §5.3);
  - the planned-versus-actual risk columns of §5.3.
- **Per-run summary:** expectancy in R, the R distribution by exit reason, MFE/MAE scatter in R (an MFE histogram for winners and losers, where the T1/trail levels sit), and the CS-8 forward MFE/MAE for the same signals.
- **Acceptance:** the programme's usual criteria (PR #117 overview §6), unchanged.

## 6. Baseline exit package for entry-only ideas (**Owner-approved 2026-11-11 09:16 ET**, predeclared, not fitted)
Owner (09:16 ET): *"2. Yes, they are good"*. This is the default of every Input above, chosen to mirror the batch-1 framework and the existing `_ExitHalfRiskHalfRoll` (half at +1R, runner trailed):

| Component | Baseline |
|---|---|
| Size | 2 contracts (PR #117 §7.2), so the scale-out is exercised |
| Initial stop | Embedded: `EntryProvided`. Wrapper: `AtrMultiple` 1.5 × ATR(14) on Data1 (owner-approved default 18:18–18:21 ET; `SignalBarExtreme` selectable), floored at the cost floor (ES 14 / NQ 19 ticks) |
| T1 | `T1Unit` `RMultiple`, `T1Value` 1.0, `T1ScalePct` 50 ⇒ ⌊q/2⌋ at +1.0R (initial R), `OrderExit.Total`; q = 1 ⇒ `ExitAtT1` |
| Break-even | `AfterT1`, `BreakEvenOffsetTicks` 1 (entry + 1 tick); `BreakEvenUnit` `RMultiple` / `BreakEvenValue` 1.0 used only by `AtTrigger` |
| Runner trail | `Chandelier` 3.0 × ATR(22) on Data1, from T1 (`AfterT1`; `TrailStartUnit` `RMultiple` / `TrailStartValue` 1.0 used only by `AtTrigger`): **below the highest high for longs / above the lowest low for shorts, one-way** (long only moves up, short only moves down; owner-approved 09:34 ET) |
| Final target | none (`FinalTargetUnit` `RMultiple`, `FinalTargetValue` 0) |
| Time stop | off |
| Session | intraday `FlatBy` 15:55 ET (early close: close − 5 min); swing ideas `HoldOvernight` with `ERollExit.FlattenBeforeSplice` |
| News exit | `NewsExitMinutes` 0 (matches PR #117's PROPOSED default) |
| Adds | as the entry decides; `AddStopMode` `RaiseToAddLow`; R re-based on the add fill |
| Control | `None` in the baseline arm. Each entry-only idea also runs a `FixedBars` control arm with N per timeframe (1m 30, 5m 12, 15m 8, 60m 6, daily 5) and reports CS-8 forward MFE/MAE (owner-approved 18:18–18:21 ET, Q4/Q5; other timeframes: nearest listed at or below, owner-approved 18:54 ET, §4.9) |
| Precedence | §4.10 |
| Costs and fills | Batch-1 settings (PR #117 §4): commissions and slippage as approved, 2 ticks of open slippage, closed-bar orders, no IOG |

## 7. Native checks for the MiniPC (backtest only, data ≤ 31 Dec 2024, temporary sandbox studies, no csproj commit)
| # | Question | How | Pass |
|---|---|---|---|
| E1 | Can an exit-only signal on the same chart manage another signal's position, and what does it see on the fill bar? | `_TestGxEntry` (buys 2 on bar 100) + `_TestGxExit` (journals `PositionSide`, `CurrentPosition.OpenTrades`, `AvgEntryPrice` each bar, and `Bars.Info.Resolution` (type, size) once on 1m, 5m, 15m, 60m, daily and one 2m chart; sends `Sell 1 Total` at bar 105, `Sell FromAll` at bar 110) | **PASS, 2026-11-11.** Exit fills as sent on all six timeframes; values visible from the fill bar's close; resolution values recorded for the §4.9 mapping |
| E2 | Fill-bar protection | Same pair on a bar where price trades through the stop inside the fill bar. Runs: (a) wrapper explicit stop, (b) wrapper `GenerateStopLoss` every bar, (c) entry sends its stop with the entry, (d) `GenerateStopLoss` only while in a position | **Recorded, 2026-11-11.** (c) and (b) stop out on the fill bar; (a) and (d) do not. A separate wrapper cannot protect the fill bar. Re-check the chosen mechanism on the implementation build |
| E3 | Two exits on one bar from two signals; Format Signals order | Two exit signals both `MarketNextBar` on bar 105 (FromAll+FromAll, both list orders; Total+Total; FromAll+Total) | **PASS, 2026-11-11.** No overshoot. List order decides FromAll+FromAll; Total+Total both fill; FromAll takes 2 against Total |
| E4 | PT: two signals per strategy (**verification item; the wrapper is already specified for PT, §5.1**) | Entry + wrapper in one PT strategy, MM on. Journal `Signals.Length`, `Signals[0]` name, each signal's own index, `MyPortfolioData` reference identity from both signals (L6-style `sameRef`), the calc order of the two signals in one step, the MM's `riskAtOpen` against the wrapper's fill R, and that `EntryContracts` / `AllowEntries` do not affect the wrapper's exits | **PASS, 2026-11-11, on the scratch studies.** Shared dictionary, list-order calc, MM R = wrapper R. Re-check with the real wrapper and `_PortfolioRiskManager` |
| E5 | Repeated partials | `Sell 1 Total` twice with one order object, with and without `ExitFromOneEntryOnce.FillAndKillExitOrder` | **PASS, 2026-11-11.** Same object does not fill twice; FillAndKill does. GX uses separate order objects per partial |
| E6 | Pyramiding setting label | Strategy Properties dialog | **PASS by reference** to PR #118 A1 (2026-11-11) |
| E7 | Enum Inputs in the Format dialog and the optimiser | One enum Input (e.g. `ETriggerUnit` as `FinalTargetUnit`) | **Recorded, 2026-11-11.** Named members show in the Inputs dropdown. The optimiser lists only numeric Inputs, not enums. A numeric stand-in was used for the sweep. Re-check on the implementation build |
| E8 | IOG per signal | Wrapper IOG on, entry off | **PASS, 2026-11-11.** Confirmed per-signal; GX rule "IOG off" stays |

### 7.1 Results (MiniPC, 2026-11-11, backtest only, data in 2024)

Scratch studies `_TestGxEntry`, `_TestGxExit`, `_TestGxExitB`, `_TestGxExitFK` and `_TestGxMM`, compiled with F7 only. Logs are on the MiniPC at `C:\Share\MultiCharts\TestGX\out\` (`E1-*-ES`, `E2-a`…`d`, `E3-*`, `E4-a` / `E4-b` / `E4-swap`, `E5-*`, `E7-RMultiple-ES`, `E7-opt`, `E8-ES`). Raw logs are not committed (they contain prices). All chart runs used dxFeed `@ES` 1 minute, 2024-11-18 → 2024-11-22, unless noted. E1 also ran 2m, 5m, 15m, 60m (2024-11-04 → 2024-11-22) and daily (2024-06-03 → 2024-11-22, `EntryBar` 40). E4 used Portfolio Trader workspace `TestGX E4 Backtest.pws` (`@ES` + `@NQ` 1 minute, MM `_TestGxMM`). No price values from the logs are recorded here; dollar P&L and trade counts are.

**E1 — PASS** across 1m, 2m, 5m, 15m, 60m and daily. The exit-only signal managed the entry's position on every timeframe: `GXX1T` Sell 1 Total at rel 4 filled 2→1 on the next bar (bar 106; daily, with `EntryBar` 40, bar 46) and `GXXA` Sell FromAll at rel 9 filled 1→0 on the next bar (bar 111; daily bar 51). On the fill bar's close the exit saw `MarketPosition` 2, `PositionSide` 1 (long), `OpenTrades` 1, `AvgEntryPrice` equal to the fill, and the `GXLE` leg. Resolution values are in §4.9; all six match the control-bar mapping.

**E2 — recorded (re-check the chosen fill-bar mechanism on the implementation build).** Twenty trades, 1-tick stop, `@ES` 1 minute.

| Variant | Who protects | Through on the fill bar | Exited on the fill bar (of those through) | Net profit | Notes |
|---|---|---|---|---|---|
| a | Exit explicit stop `GXXS` from the first bar it sees the position | 15 | 0 of 15 | −$450 | Stop works from the next bar (17 `GXXS`, 3 time exits `GXXA`) |
| b | Exit `GenerateStopLoss` every bar, flat or not (`PerContract`, 1 tick × BPV) | 15 | 15 of 15 | — | Order name "Stop Loss" (17 "Stop Loss", 3 `GXXA`). The exit-only signal never sees the 15 fill-bar exits (`FIRSTSEEN` count 5) |
| c | Entry sends sell stop `GXLSe` with the entry | 14 | 14 of 14 | — | 17 `GXLSe`, 3 `GXXA`. Through-count is 14 rather than 15 because this stop is the entry-bar close minus 1 tick, not the fill minus 1 tick |
| d | Exit `GenerateStopLoss` only while in a position (the `_StopsAndTargets` pattern) | 15 | 0 of 15 | −$450 | Same timing as (a); order name "Stop Loss" from the next bar |

Implication: a separate wrapper cannot protect the fill bar. The fill-bar stop must come from the entry, or from built-in `GenerateStopLoss` called every bar.

**E3 — PASS.** No overshoot: the position goes 2→0 at bar 106.

- FromAll + FromAll: only the first-listed signal's order fills (`GXXA` with Exit then ExitB; `GXBA` when swapped). List order decides.
- Total + Total: both fill, 1 each (`GXX1T` and `GXB1T`).
- FromAll + Total: FromAll takes 2 (`GXXA`); the Total order does not fill.

**E4 — PASS on the scratch studies** (`TestGX E4 Backtest.pws`, ES+NQ 1m, MM `_TestGxMM`; logs `E4-a`, `E4-b`, `E4-swap`). Re-check with the real `_GenericExit` and `_PortfolioRiskManager`.

- `signals.Length` 2, with `[0]` `_TestGxEntry` when the entry is listed first.
- Own index found via `PortfolioData` matching (ES 0, NQ 1). The index is readable at `StartCalc`, but `Signals[i].Bars` throws `Unaccessible property(method): Bars` at `StartCalc`, so defer that to the first bar.
- `sameRef` True for both signals and the MM: one `MyPortfolioData` dictionary per strategy.
- Calc order = list order, and the MM `lastWriter` is EXIT when the exit is listed second.
- Swap: the exit reads the entry's previous-step data (`entryStepSeen` = bar−1, `entryCalculatedFirst` N) and the MM `lastWriter` is ENTRY. This supports the Error rule for a wrapper listed before the entry.
- `RISKATOPEN` match: ES R$100 / `riskAtOpen` $200; NQ $40 / $80.
- E4-b: deny-all (`AllowEntries = false`, `EntryContracts = 0`) leaves the exits identical to E4-a (0 diffs).
- Swap fills are identical to E4-a. NQ stopped at bar 104 and ES T1 filled at bar 106 (FromAll at bar 116).

**E5 — PASS (GX uses separate order objects per partial).**

- a: the same order object sent twice in one bar does not fill twice (3→2, then 2→0 at bar 116).
- b: the FillAndKill attribute gives 3→2→1→0 (second partial at bar 111, FromAll at bar 116).
- The attribute is per class, so in the embedded engine it would apply to every exit of the host signal.

**E6 — PASS by reference** to PR #118 A1 (2026-11-11): the installed Strategy Properties label is *"Allow up to 3 entry orders in the same direction as the currently held position:"*, with the two radios recorded there.

**E7 — recorded (re-check on the implementation build).** The named `ETriggerUnit` stand-in members (`FinalTargetUnit`) show in the Inputs dropdown. RMultiple 2 gives a target at fill + 4 pts; the clean confirmation run (`E7-RMultiple-ES`) had 20 trades, net −$1,125, with 9 stop exits (`GXXS`) and 11 time exits (`GXXA`) and no target (`GXXT`) fills. The optimiser lists only numeric Inputs, not enums. A numeric stand-in `FinalTargetUnitIdx` (0…4, −1 = use the enum) was added to the scratch signal with Ettienne's approval (2026-11-11). Regular Exhaustive sweep, `FinalTargetValue` 2:

| Idx | Unit | Net Profit | Trades |
|---|---|---|---|
| 0 | RMultiple | −1,125 | 20 |
| 1 | PercentOfEntry | −1,125 | 20 |
| 2 | Ticks | −675 | 20 |
| 3 | Points | −150 | 20 |
| 4 | CurrencyPerContract | −325 | 20 |

`E7-opt.txt` confirms `effUnit` switched per iteration (the five `START` lines carry `idx` 0…4 and the matching `effUnit`). Idx 0 and 1 are identical because **neither target filled within the 10-bar window**: the 2R target is fill + 4 pts, and the 2% target is fill + about 118 pts; the two exit-fill sequences are the same bar-for-bar as each other and as the clean RMultiple run (9 stops, 11 time exits, 0 target fills). The tighter units (2 ticks, 2 points, $2 → 1 tick) did produce target fills and different nets. This is not evidence that the two units compute the same level.

Spec implication: the implementation should expose a numeric optimiser companion for each `ETriggerUnit` Input (or document the enum limitation), and a re-check is needed on the implementation build.

**E8 — PASS.** IOG is per signal. The exit with IOG on had `calls(4) insideCalls(2)` and `iogEnabled(True)` per bar; the entry had `calls(1) insideCalls(0)` and `iogEnabled(False)`. The exit filled intrabar at bar 105 (vs 106 in E1), and FromAll at bar 110 intrabar. The GX rule "IOG off" stays.

**Still to do after this recording:** re-check E2, E4 and E7 on the implementation build (GX-3 / GX-4); scratch cleanup of the `_TestGx*` studies, workspaces and logs awaits Ettienne's approval. Leave the Check7 files alone.

## 8. Implementation plan (after approval; separate PRs)
1. **GX-1:** `_ExitEngine` (pure C# in a `.Function.CS` file): state machine, enums, precedence, R segments; `tests/ExitEngineTests` (`dotnet run`, like `tests/PortMMTests`). No MC dependency. Off-MC tests include (rev 0.7):
   - `ETriggerUnit` levels for T1, break-even, the final target and the trail start, long and short (long entry + d, short entry − d; each unit; rounding away from the entry; value ≤ 0 with the feature on ⇒ Error, feature off);
   - defaults reproduce rev 0.6: `FinalTargetValue` 0 ⇒ no final target; `TrailStart = AtTrigger` with `RMultiple` 1.0 ⇒ starts at +1R (long) / −1R (short);
   - control-bar mapping: 1, 5, 15, 60 min and daily ⇒ 30/12/8/6/5; 2 ⇒ 30, 10 ⇒ 12, 30 ⇒ 8, 240 ⇒ 6, weekly ⇒ 5; seconds / tick / range bars ⇒ 30 + Warning; an Input override (e.g. `ControlExitBarsM5` = 20) wins; 0 ⇒ control off;
   - `FixedBars` exits after exactly N closed bars, long and short, with the disaster stop still active; `RandomBars` reproducible for a seed;
   - `InitialStop` default `AtrMultiple`; `SignalBarExtreme` long/short levels.
2. **GX-2:** `_CustomSignal` integration as a protected helper: `exitPlan(...)` at entry, `exitBar()` each bar, `portfolioStop` hooks, CS-1 columns. No new base `[Input]`.
3. **GX-3:** `_GenericExit` wrapper in `sandbox/` (header §17 for every Input), chart **and** Portfolio Trader (§5.1); E4 is run before any PT result is used. **Legacy exits superseded** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q8: mark the legacy exit studies superseded once GX exists"*): in the same PR, `_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit` and `_ExitHalfRiskHalfRoll` get a header note `// SUPERSEDED by GX (_ExitEngine / _GenericExit, specs/generic-exit-manager-spec.md); kept for reproducibility, not deleted`, and `docs/engines-manual.md` lists them as superseded. No code in them changes and none is deleted.
4. **GX-4:** native checks E1–E8, recorded in `specs/live-check-sheet.md`. The scratch-study run is already recorded in §7.1 (2026-11-11). Re-check E2 (the chosen fill-bar stop), E4 (real `_GenericExit` and `_PortfolioRiskManager`) and E7 (real `ETriggerUnit` Inputs, and the numeric optimiser companion) on the implementation build before treating those items as closed for production.
5. **GX-5 (planned milestone):** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"*) once GX-1–GX-4 are done and the E1–E8 checks pass, batch 1 moves onto the engine instead of hand-coding §7, **one strategy at a time**. Each move has its own equivalence re-check: the engine version must match the hand-coded version trade-for-trade on ≤ 2024 data (same entries, exit bars, prices, exit reasons and R, long and short), with any difference explained and fixed before the hand-coded path is retired. A strategy that does not match stays hand-coded until it does.

Regression: G0, D9a and the existing suites unchanged, because GX is inert unless a strategy calls it.

## 9. Owner answers and open questions

**Resolved, owner-approved 2026-11-11 09:16 ET** (*"1. Yes, this approach is fine. 2. Yes, they are good - I assume we'd be able to set the first target and break-even profit percentage (or amount). 3. No, we can make the standalone strategy work in portfolio trader as well for now. 4. Keep the position open and log the warning."*):
- **Q1:** option C approved (shared engine plus one thin `_GenericExit` wrapper).
- **Q2:** baseline values approved. The first target (unit, value, scale-out %) and break-even (unit, value, offset ticks) are Inputs with `ETriggerUnit` (§4.2, §4.3).
- **Q3:** the wrapper works in Portfolio Trader now (§5.1). E4 passed on the scratch studies (2026-11-11, §7.1); re-check on the implementation build.
- **Q7:** an over-budget wrapper trade stays open, is managed normally, and logs one Warning per trade (§5.2).

**Q10 resolved, owner-approved 2026-11-11 09:33 ET (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*)** (§5.3):
- (1) `actualRiskPct` uses the whole-trade worst point, including realised scale-out P&L.
- (2) The base is the strategy's own equity only, on charts and in Portfolio Trader; the book and broker bases are removed.
- (3) 1.00 = exceeded plan, and > 1.10 is reported separately.

**Resolved, owner-approved 2026-11-11 18:18–18:21 ET:**
- ~~**Q4** Should every entry-only idea also run a control exit (`FixedBars` 10 and/or `RandomBars`) and report CS-8 forward MFE/MAE, as the three-way entry test in §4.9? PROPOSED: yes, `FixedBars` 10.~~ *"Q4 yes, but set ControlExitBars per timeframe"* → §4.9, §6: every entry-only idea runs a `FixedBars` control arm with N per timeframe and reports CS-8 forward MFE/MAE.
- ~~**Q5** What value should `FixedBars` / `ControlExitBars` take per timeframe (10 bars on 5m ≈ 50 minutes)?~~ *"Q5: 1m 30, 5m 12, 15m 8, 60m 6, daily 5"* → §4.9: five int Inputs `ControlExitBarsM1/M5/M15/H1/D1` = 30/12/8/6/5. Other timeframes: nearest listed at or below (**Owner-approved 2026-11-11 18:54 ET**, Ettienne: *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*).
- ~~**Q6** Should the wrapper's default stop be `AtrMultiple`, or `SignalBarExtreme` (as `_ExitHalfRiskHalfRoll`)?~~ *"Q6: AtrMultiple default, SignalBarExtreme selectable"* → §4.1, §6.
- ~~**Q8** Keep the legacy exit studies (`_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit`, `_ExitHalfRiskHalfRoll`) unchanged (PROPOSED), or mark them superseded once GX exists? Nothing is deleted by this spec.~~ *"Q8: mark the legacy exit studies superseded once GX exists"* → §1.1, §8 GX-3: header note and docs entry; nothing deleted.
- ~~**Q9** Should batch-1 specs later move onto the engine (GX-5), or stay hand-coded?~~ *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"* → §8 GX-5: after the E-checks pass, one strategy at a time, each with an equivalence re-check.
- **Final-target and trail-start units:** *"Yes, the final target and trail start both get ETriggerUnit"* → §4.2 `FinalTargetUnit` / `FinalTargetValue`, §4.4 `TrailStartUnit` / `TrailStartValue` (`ETriggerUnit`, default `RMultiple`; defaults keep rev 0.6 behaviour; shorts mirror explicitly).

**Resolved, owner-approved 2026-11-11 18:54 ET** (*"Yes to both the nearest-timeframe rule and the AtTrigger rename"*):
- The rule for timeframes not in the `ControlExitBars` list (nearest listed timeframe at or below; sub-minute and non-time bars use the 1m value with a Warning, §4.9).
- The `ETrailStart.AtR` → `AtTrigger` rename (§4.4).

**Still open:**
- Re-check E2 (the chosen fill-bar stop mechanism), E4 (real `_GenericExit` wrapper and `_PortfolioRiskManager`) and E7 (real `ETriggerUnit` Inputs, and the numeric optimiser companion) on the implementation build. The scratch-study results are in §7.1.
- Cleanup of the scratch `_TestGx*` studies, workspaces and logs awaits Ettienne's approval. Leave the Check7 files alone.
