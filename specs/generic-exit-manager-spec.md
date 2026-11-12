---
title: "Generic trade-management and exit layer (GX) for entry-only ideas — READY FOR IMPLEMENTATION"
rev: "1.1 READY FOR IMPLEMENTATION, 2026-11-12 (ET); rev 1.1 = owner answers 07:31 and 07:33 ET: Idx companions on real studies (§4.13), `_GenericExit` in the studies folder with one csproj line and the engine inside it, test strategies in `sandbox/` (§8.1, §8.7), T1 planned from the planned entry (§4.2, §5.1, §8); rev 1.0 = owner instruction 2026-11-12 07:13 ET to complete the spec for implementation; E2 fill-bar protection closed (§4.12), E7 optimiser companions closed (§4.13), §8 rewritten as the Coder hand-off, every remaining PROPOSED item resolved (§10); rev 0.9 = MiniPC E1–E8 native results recorded (scratch `_TestGx*`, §2, §3, §4.9, §5.1, §7, §8, §9); rev 0.8 = owner approval 18:54 ET of the §4.9 other-timeframe rule and the ETrailStart.AtTrigger rename (§4.4, §4.9, §6, §9); rev 0.7 = owner answers 18:18–18:21 ET to Q4, Q5, Q6, Q8, Q9 and the final-target / trail-start unit question (§4.1, §4.2, §4.4, §4.9, §6, §8, §9); rev 0.6 = T1 already passed at the fill ⇒ skip T1, trail all, Info line (owner 09:52 ET, §4.2); rev 0.2 = owner answers of 09:16 ET; rev 0.3 = planned vs actual risk % logging (owner request 09:19 ET, §5.3); rev 0.4 = Q10 answers 09:33 ET (whole-trade MAE, strategy equity only, 1.00 and 1.10 reported separately); rev 0.5 = short side explicit in every stop/target/BE/trail rule (owner 09:34 ET)"
status: "READY FOR IMPLEMENTATION (owner, 2026-11-12 07:13 ET, verbatim: \"Alright, you can recheck the tests left and please complete the spec so @Coder can start implementing strategies from it\"). Docs only; code follows in GX-1 to GX-5 (§8). Every rule is either owner-approved (quoted), marked \"Analyst decision (2026-11-12), owner may override\", or an implementation-build acceptance check run by Analyst (§8.4). Owner answers of 07:31 ET applied in rev 1.1. Owner placement rule 07:33 ET applied: `_GenericExit` (with the shared engine) in the studies folder with one csproj line, test strategies in `sandbox/` with none (§8.7). Scratch `_TestGx*` cleanup awaits owner approval."
requested_by: "Ettienne Hugo, 2026-11-11 09:02 ET (verbatim below)"
code_refs: "origin/main @ 47ffda2 (2026-11-10). Batch-1 framework references are to PR #117 (branch feat/strategy-specs-batch1, specs/strategies/00-programme-overview.md rev 1.6) and the M-PORT add-legs draft PR #118 (specs/m-port-add-legs-spec.md)."
holdout: "No price data on or after 2025-01-01 is viewed or reported by anything in this spec. Native checks use data ≤ 31 Dec 2024."
---

# Generic trade-management and exit layer (GX), rev 1.1 READY FOR IMPLEMENTATION

**Owner instruction, 2026-11-12 07:13 ET (verbatim):** *"Alright, you can recheck the tests left and please complete the spec so @Coder can start implementing strategies from it"*
- → status READY FOR IMPLEMENTATION; Coder starts with GX-1 (§8.1).
- → the E-checks still to do are acceptance checks on the implementation build (§8.4), not blockers to starting.

**Owner answers, 2026-11-12 07:31 ET (verbatim):** *"Yes real studies can get the numeric inputs. You can add the _GenericExit to the csproj file. Yes you can measure it from the T1 planned"*
- Numeric inputs → §4.13: the `<Name>Idx` companions are approved on `_GenericExit` and on every strategy that hosts the engine.
- csproj → §8.7: one hand-written `<Compile Include="_GenericExit.Strategy.CS" />` line in `PLStudies.csproj`, never an Editor-generated change.
- T1 → §4.2: T1 is planned from the planned entry price (PR #117 §7.2a), and T1-SKIPPED compares the actual fill with that planned T1.

**Owner, 2026-11-12 07:33 ET (verbatim):** *"Also _GenericExit can go under the Studies/CS folder since it is already a concrete strategy to use. The other strategies we develop for further testing will go under the sandbox folder and will not directly be included in the csproj."*
- → §8.1, §8.7: `_GenericExit.Strategy.CS` sits in the studies folder, which is the repo root (the MiniPC clone is `C:\ProgramData\TS Support\MultiCharts .NET64\StudyServer\Techniques\CS`, so the file is `Techniques\CS\_GenericExit.Strategy.CS` there), with its one `<Compile Include>` line.
- → §8.1, §8.7: test and development strategies (the A-check host `_GX_00_TestHost`) go in `sandbox/` under the sandbox naming lint, with no csproj line.

**Owner, 2026-11-12 15:28 ET (verbatim):** *"Change this so that open positions at 15:55 are flagged and closed. New positions opened after that window is kept open till the next day at 15:55 - I think that would be the semantic reasoning behind the close by rule."*
- → §4.6: this supersedes the 10:23 ET reading that flattened on any bar whose clock was at or after `FlatByHHMM`. The deadline is the first `FlatByHHMM` strictly after the original entry. The first bar at or after that deadline is `FLATBY`.

**How to read the approval marks.** An item is either **Owner-approved** (with the owner's words quoted), or marked **Analyst decision (2026-11-12), owner may override** (the owner may change it; Coder implements it as written until then), or an **acceptance check** on the implementation build (§8.4). The full list of Analyst decisions is in §9.

**Owner answers, 2026-11-11 09:16 ET (verbatim):** *"1. Yes, this approach is fine. 2. Yes, they are good - I assume we'd be able to set the first target and break-even profit percentage (or amount). 3. No, we can make the standalone strategy work in portfolio trader as well for now. 4. Keep the position open and log the warning."*
- 1 → §3: option C approved.
- 2 → §4.2, §4.3, §6: baseline values approved; the first target and break-even are configurable, with a typed trigger unit.
- 3 → §5.1: the wrapper works in Portfolio Trader now.
- 4 → §4.1, §5.2: an over-budget trade stays open and logs a Warning.

**Owner answers, 2026-11-11 18:18–18:21 ET (verbatim):** *"Q4 yes, but set ControlExitBars per timeframe"*; *"Q5: 1m 30, 5m 12, 15m 8, 60m 6, daily 5"*; *"Q6: AtrMultiple default, SignalBarExtreme selectable"*; *"Q8: mark the legacy exit studies superseded once GX exists"*; *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"*; and on the final-target / trail-start unit: *"Yes, the final target and trail start both get ETriggerUnit"*.
- Q4 → §4.9, §6: every entry-only idea also runs a `FixedBars` control arm with a per-timeframe bar count, and reports CS-8 forward MFE/MAE.
- Q5 → §4.9: `ControlExitBars` defaults per bar timeframe, as five native int Inputs.
- Q6 → §4.1, §6: wrapper default stop `AtrMultiple`; `SignalBarExtreme` selectable.
- Q8 → §1.1, §8.5: the legacy exit studies are marked superseded once GX exists; nothing is deleted.
- Q9 → §8.6: GX-5 is a planned milestone: batch 1 moves onto the engine after the E-checks pass, with a per-strategy equivalence re-check.
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
**Superseded once GX exists** (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q8: mark the legacy exit studies superseded once GX exists"*): `_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit` and `_ExitHalfRiskHalfRoll` get a header note and a docs entry marking them superseded by GX when GX-3 is merged (§8.5). They stay compiled and unchanged otherwise; **nothing is deleted**.

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
| F7 | **Fill-bar protection.** An entry signal can send its stop **with** the entry ("also protects the entry bar", engines manual v3.2 TS-01 example, `m_lxStp.Send(m_stop)` next to `m_le.Send`). A separate exit signal does not know a pending entry exists, so its explicit stop starts only after the fill bar has closed: engines manual v3.2 "the fill bar itself is unprotected", and `_Test10`'s pitfall note. MC's built-in `GenerateStopLoss` protects the fill bar **only when it is called every bar, including while flat**. Called only while in a position (the `_StopsAndTargets` pattern), it behaves like an explicit wrapper stop and starts on the next bar | Repo docs (engines manual v3.2 L739–741, L1056–1058; `_Test10` L108–109). **Confirmed natively (E2, 2026-11-11, 20 trades, 1-tick stop):** (a) wrapper explicit stop, 0 of 15 fill-bar exits, stop from the next bar, net −$450; (d) `GenerateStopLoss` only in a position, same 0 of 15 and −$450, order name "Stop Loss"; (b) `GenerateStopLoss` every bar, 15 of 15 fill-bar exits named "Stop Loss" (the exit-only signal never sees those positions); (c) stop sent with the entry (`GXLSe`), 14 of 14 fill-bar exits (the through-count is 14 rather than 15 because that stop is the entry-bar close minus 1 tick, not the fill minus 1 tick). **A separate wrapper cannot protect the fill bar.** The fill-bar stop must come from the entry, or from `GenerateStopLoss` called every bar. **GX choice (§4.12):** embedded mode sends the stop with the entry (c), the required path for production strategies; the wrapper uses (b) as an optional built-in disaster stop, on by default. Acceptance check A-E2 (§8.4) |
| F8 | In PT, an `IPortfolioStrategy` has `Signals : IStrategy[]` (several signals per instrument). The MM reads `Signals[0]` (`_PortfolioRiskManager.view` L125) and the strategy's `PortfolioData`. `EntryContracts` applies to every **entry** order of the strategy; exits are never gated | m-port-spec §1, §6; `_PortfolioRiskManager.Strategy.CS` L111, L125. **Confirmed natively (E4, 2026-11-11, scratch MM):** `MyPortfolioData` is one dictionary per strategy (`sameRef` True from both signals and the MM). `signals.Length` is 2 with the entry at `[0]` when listed first. The own index is readable at `StartCalc` via `PortfolioData` matching, but `Signals[i].Bars` throws `Unaccessible property(method): Bars` at `StartCalc`, so defer `Bars` reads to the first bar. Calc order equals list order. Deny-all (`AllowEntries = false`, `EntryContracts = 0`) leaves the wrapper's exits unchanged. Acceptance check A-E4 (§8.4) with the real wrapper and `_PortfolioRiskManager` |
| F9 | IOG is unavailable in PT, and so is Bar Magnifier. On a chart, IOG is a per-signal setting | m-port-spec §4 item 4. **Confirmed natively (E8, 2026-11-11):** with the exit IOG on and the entry off, the exit logged `calls(4) insideCalls(2)` and `iogEnabled(True)` every bar, the entry `calls(1) insideCalls(0)` and `iogEnabled(False)`. The exit filled intrabar at bar 105 (vs 106 in E1) and FromAll at bar 110 intrabar. The GX rule "IOG off" stays, because this would break chart/PT parity |

**Consequences.**
1. A stand-alone exit signal **works** on a chart and in PT as a second signal (F1, F8). But:
   - it cannot use `FromEntry` (F2);
   - it must use `Total` for partials (F3);
   - it does not know the entry's planned or structural stop, so it must derive R itself;
   - it leaves the fill bar unprotected unless it calls `GenerateStopLoss` every bar, including while flat (F7). Calling it only while in a position, or sending an explicit stop from the wrapper, starts the stop on the next bar. GX therefore gives the wrapper a built-in disaster stop for the fill bar (§4.12);
   - in PT, the entry strategy's `pm.riskOpen` would not follow the exit signal's stop (F8) unless the exit signal publishes it. The wrapper publishes it (§5.1); E4 confirmed the shared dictionary, list-order calc and `riskAtOpen` match on the scratch studies (acceptance check A-E4, §8.4).
2. An exit engine running **inside** the entry signal has none of these problems. It sends the stop with the entry, knows the structural and widened stops, calls `portfolioStop`, journals through CS-1, and can use `FromEntry` per leg.

## 3. Options compared

| | (A) One generic exit signal with many typed-enum Inputs | (B) A family of stacked exit signals (stop, target/scale, trail, time/session) | (C) A shared C# exit engine used by entry strategies, plus one thin wrapper signal |
|---|---|---|---|
| Where the logic runs | A separate signal on the chart / PT strategy | 3–5 separate signals | Inside the entry signal (`_CustomSignal` helper class). The wrapper is a separate signal only for entries that cannot embed it |
| Input count | 48 on one dialog (§4.11, including the 8 optimiser companions) | About 6–9 each, but 4 dialogs and 4 "same strategy" set-ups per test | Embedded: 0 new Inputs by default (the host exposes the few it varies, header §17); wrapper: 48, same as A |
| Knows the entry's stop / R | No: derives R (F2, F7) | No | **Yes** (embedded); wrapper: derives R |
| Fill-bar protection | Only through `GenerateStopLoss` called **every bar, including while flat** (E2). An explicit wrapper stop, or `GenerateStopLoss` only while in a position, starts on the next bar | Same, in the stop module | **Embedded: yes**, the stop is sent with the entry (E2(c)). Wrapper: the built-in disaster stop `FillBarDisasterStop` (§4.12, E2(b)) |
| Same-bar precedence (§7.4) | One place decides; one reason is journalled | **Not controllable**: each signal sends its own exit, and MC fills whichever triggers. E3 (2026-11-11): no overshoot (2→0 at bar 106). FromAll+FromAll: only the first-listed signal's order fills (`GXXA` normally, `GXBA` when swapped). Total+Total: both fill, 1 each. FromAll+Total: FromAll takes 2. List order decides. Do not build B | One place decides (the engine) |
| Optimiser sweeps | Fine for numeric Inputs, one dialog. **Enum Inputs cannot be swept** (E7, 2026-11-11): named members show in the Format Inputs dropdown, but the optimiser lists only numerics. GX gives each sweepable enum Input a numeric companion `<Name>Idx` (§4.13). Acceptance check A-E7 (§8.4) | Awkward: a sweep spans several signals | Embedded: the host exposes the exact Inputs a registered ablation needs, with the §4.13 companion for any enum it sweeps. Wrapper: as A |
| Testability off-MC | Possible if the logic sits in a pure class | Each module separately; the interactions are not | **Best**: one pure `_ExitEngine` state machine, unit-tested like `_PortMM` under `tests/`, fed by bar and fill events |
| Portfolio Trader | Works as a second signal, but `pm.riskOpen` / `pm.openR` are wrong unless the exit publishes (E4) | Same, with more signals | **Embedded: full M-PORT compatibility** (`portfolioStop`, `portfolioArmAdd`, R re-base §5). Wrapper: PT-capable as the second signal, publishing the stop keys (§5.1, owner-approved 09:16 ET; verified by E4) |
| Adds (PR #117 §7.7, PR #118) | Sees the position grow, so it can re-base R, but cannot see `pm.add` | Same | The engine owns the add path: `AddStopMode`, re-base, `portfolioArmAdd` |
| Risk of drift between strategies | Low | Medium (module versions) | **Lowest**: one engine. Batch-1 specs can later adopt it instead of hand-coding §7 |
| Effort | Medium | High (4 signals, plus precedence glue) | Medium–high once; then low per strategy |

**Recommendation: option C** (**Owner-approved 2026-11-11 09:16 ET**: *"1. Yes, this approach is fine."*).
- Build one pure C# exit engine (`_ExitEngine`) that implements PR #117 overview §7 exactly, and expose it to `_CustomSignal`-derived entries.
- Ship **one** thin wrapper signal, `_GenericExit`, for entry-only ideas that cannot embed the engine (a legacy entry, a third-party signal, a quick sandbox entry).
- Do not build B: same-bar precedence and a single exit reason cannot be guaranteed across separate signals.
- A is the wrapper half of C. If the wrapper's dialog proves too large in practice, split it into at most two wrappers (`_GenericExit` for stops/targets/trail, `_GenericExitClock` for time/session/news), with precedence documented as "the first exit to fill wins" (E3). **Analyst decision (2026-11-12), owner may override.** This is a fallback only: GX-3 ships the single `_GenericExit`; the split is built only if the owner asks for it after using the single dialog.

## 4. Exit component catalogue (typed enums)

The baseline defaults are owner-approved (§6, 09:16 ET: *"2. Yes, they are good"*), and so is every item quoted below. The enum member lists, the Input names and the non-default mode rules that carry no owner quote are **Analyst decision (2026-11-12), owner may override.** The member order of every enum is part of the contract (§4.13): members are never reordered or removed, only appended.

All prices are rounded to the tick. "R" is the initial 1R: |entry − initial stop| × BPV per contract, using the **widened** stop when the cost floor widens it (P1 rule). After an add, R is re-based (§5). Ints are counts, minutes or clock times, where 0 = off.

### 4.1 Initial stop
```csharp
public enum EInitialStop { EntryProvided, AtrMultiple, FixedTicks, SignalBarExtreme, SwingExtreme, Percent }
```
| Member | Long stop | Short stop | Inputs |
|---|---|---|---|
| `EntryProvided` (embedded default) | the entry's own structural stop (the one passed to `sizeForRisk` / `portfolioArm`) | same | — |
| `AtrMultiple` (wrapper default; **Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q6: AtrMultiple default, SignalBarExtreme selectable"*) | entry − `StopAtrMult` × ATR(`StopAtrLength`) | entry + `StopAtrMult` × ATR(`StopAtrLength`) | `StopAtrMult` (double, 1.5), `StopAtrLength` (int, 14); ATR on Data1 from closed bars |
| `FixedTicks` | entry − `StopTicks` × tick | entry + `StopTicks` × tick | `StopTicks` (int, default 0; must be > 0 with this mode) |
| `SignalBarExtreme` (selectable, owner-approved 18:18–18:21 ET) | low of the bar before the fill − 1 tick | high of the bar before the fill + 1 tick | — (`_ExitHalfRiskHalfRoll`'s R) |
| `SwingExtreme` | lowest low of the last `StopSwingBars` closed bars − 1 tick | highest high of the last `StopSwingBars` closed bars + 1 tick | `StopSwingBars` (int, 5) |
| `Percent` | entry × (1 − `StopPct`/100) | entry × (1 + `StopPct`/100) | `StopPct` (double, default 0; must be > 0 with this mode); `_StopsAndTargets`-compatible |

- **Floor:** the stop's distance from entry is always at least the M-INSTR cost floor (ES 14, NQ 19 ticks), via `CostModel.WidenedStop`. A long stop is never above entry − floor; a short stop is never below entry + floor.
- **Invalid stop settings** (**Analyst decision (2026-11-12), owner may override.**): `FixedTicks` with `StopTicks` ≤ 0, `Percent` with `StopPct` ≤ 0, `SwingExtreme` with `StopSwingBars` ≤ 0, or `EntryProvided` in the wrapper (which has no entry stop to read) is logged once at `StartCalc` as `<ID> Error: GX INITIAL-STOP invalid(<mode> <input>=<value>) using AtrMultiple`, and the stop falls back to `AtrMultiple` with `StopAtrMult` / `StopAtrLength`. A trade is never left without a stop.
- **Distance-based and level-based modes.** `AtrMultiple`, `FixedTicks` and `Percent` are a **distance** from the entry; `SignalBarExtreme` and `SwingExtreme` are a **level**. The wrapper's fill-bar disaster stop (§4.12) uses this split.
- **Embedded:** a trade whose widened risk is > 1% of equity is skipped by `sizeForRisk` before entry (P1). The initial stop is sent **with** the entry order (§4.12).
- **Wrapper:** it cannot skip an entry it did not make. When the derived risk exceeds the 1% budget, it **keeps the position open, manages it normally**, and logs one Warning (**Owner-approved 2026-11-11 09:16 ET**: *"4. Keep the position open and log the warning."*). Exact line and dedupe in §5.2.

### 4.2 Targets and scale-out
**Configurable first target (**Owner-approved 2026-11-11 09:16 ET**: *"I assume we'd be able to set the first target and break-even profit percentage (or amount)"*).** The trigger level and the fraction scaled out are Inputs. The unit of the trigger is a typed enum shared with break-even (§4.3).

```csharp
public enum ETargetMode { ScaleOutThenRunner, SingleTarget, None }
public enum ESingleLotMode { ExitAtT1, RunnerOnly }   // existing name and default from PR #117 §7.2
public enum ETriggerUnit { RMultiple, PercentOfEntry, Ticks, Points, CurrencyPerContract }   // default RMultiple
```

**`ETriggerUnit` measures a profit distance d from a reference price `ref`, in the trade direction** (long: level = ref + d; short: level = ref − d).

**Reference price and R:**
- **T1:** `ref` = the **planned entry price**, and R = the **planned** R per contract, |planned entry − planned (widened) stop| (**Owner-approved 2026-11-12 07:31 ET**, *"Yes you can measure it from the T1 planned"*; PR #117 overview §7.2a). The planned T1 is fixed at the arm and not recomputed from the fill. Where the planned entry comes from in each mode is below ("Planned entry").
- **Final target:** planned the same way as T1, so that the `SingleTarget` skip rule below works the same way. **Analyst decision (2026-11-12), owner may override.**
- **Break-even and the trail start (§4.3, §4.4):** `ref` = the initial position's average entry price (the actual fill), and R = the initial R from that fill (08:58 ET ruling: triggers use the **initial-entry** R).

**Units:**
- `RMultiple` (default): d = value × R per contract (as above), in price.
- `PercentOfEntry`: d = ref × value / 100. Example: 0.5 ⇒ 0.5% of entry.
- `Ticks`: d = value × tick.
- `Points`: d = value price points. ES: 1 point = 4 ticks = $50 per contract.
- `CurrencyPerContract`: d = value ÷ BigPointValue. The open profit per contract reaches value $. Example: ES $250 ⇒ 5 points.
- Values must be > 0 when the feature is on. A level that does not land on a tick is rounded **away** from `ref` to the next tick, so a target is never closer than asked. A value ≤ 0 with the feature on is an input error: logged at Error, and the feature is treated as off.

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

**T1 already passed at the fill** (**Owner-approved 2026-11-11 09:52 ET**, Ettienne: *"Yes, trail the whole position, log an info message."*; same rule as PR #117 overview §7.2a; measured against the planned T1, **Owner-approved 2026-11-12 07:31 ET**, *"Yes you can measure it from the T1 planned"*). On the first bar with the position, the engine compares the **actual fill** with the **planned T1**. If the fill is at or beyond it (long: fill ≥ planned T1; short: fill ≤ planned T1; for example a gap, or a fast bar through the target), the engine sends no T1 order, treats the whole position as the runner (the §4.4 trail is active from the fill, as if T1 had filled), keeps the final target if it is still beyond the fill, and logs once: `<ID> Info: T1-SKIPPED side(<±1>) fill(<px>) t1(<px>) past(<ticks>) qty(<q>) action(trail-all)`. The journal row gets `t1Skipped = 1`. In `SingleTarget` mode a planned final target already passed at the fill is handled the same way (no limit, trail all).

**Planned entry, by mode** (the planned entry and planned stop for T1 and the final target):
- **Embedded:** the host passes them to `exitPlan(...)` on the bar it sends the entry: the planned entry is the entry order's price (the stop or limit price; for a market order, the signal bar's close), and the planned stop is the stop it also passes to `sizeForRisk` / `portfolioArm` and sends with the entry (§4.12). The engine fixes the planned T1 and final target there. If the entry order is re-sent at a new price on a later bar, the last plan before the fill counts.
- **Wrapper** (**Analyst decision (2026-11-12), owner may override.**): the wrapper cannot see a pending entry, so a GX-aware entry publishes its plan at the arm in two new portfolio keys, `pm.gxPlanEntry` and `pm.gxPlanStop` (§5.1 item 3), written by the same `_CustomSignal` call that arms the entry and cleared when the entry is flat and not armed. On the first bar with the position, the wrapper uses them only if both are present, finite, on the position's side of each other (long: stop < entry; short: stop > entry), and were written on or before the fill bar. Otherwise it **falls back to the fill** as the planned entry and its own §4.1 initial stop as the planned stop, so T1 is placed from the fill and T1-SKIPPED cannot trigger for that trade. The fallback is logged once per run, `EXIT Info: GX T1-PLAN source(fill) - entry did not publish pm.gxPlanEntry/pm.gxPlanStop`, and the journal row gets `t1PlanSource` = `plan` or `fill`. A third-party or legacy entry always uses the fallback. Acceptance checks A-E4 and A-S confirm the keys are read in Portfolio Trader and on a chart (§8.4); if a chart has no shared portfolio data, the chart wrapper uses the fallback.

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
- `TrailStart` default `AfterT1`. `AtTrigger` starts the trail on the first **closed** bar at or beyond d from the initial entry, with d from `TrailStartUnit` (`ETriggerUnit`, default `RMultiple`) and `TrailStartValue` (double, default 1.0) (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Yes, the final target and trail start both get ETriggerUnit"*; replaces rev 0.6's `TrailStartR`, same default behaviour = +1R): **long** close ≥ entry + d; **short** close ≤ entry − d. `Immediately` starts at the fill. If `TrailStart = AfterT1` but no T1 order exists (`T1ScalePct` = 0 or `TargetMode` ≠ `ScaleOutThenRunner`), it falls back to `AtTrigger` with `TrailStartUnit` / `TrailStartValue`, logged once per run at Info, the same rule as break-even (§4.3; **Analyst decision (2026-11-12), owner may override.**). The T1-SKIPPED case (§4.2) starts the trail at the fill. **Rename `AtR` → `AtTrigger`:** **Owner-approved 2026-11-11 18:54 ET**, Ettienne: *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*.
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
- `FlatBy` (default for intraday): `FlatByHHMM` (int clock time, ET, default 1555 = 15:55, Cat. §2.1). **Owner-approved 2026-11-12 15:28 ET**, Ettienne: *"Change this so that open positions at 15:55 are flagged and closed. New positions opened after that window is kept open till the next day at 15:55 - I think that would be the semantic reasoning behind the close by rule."* This supersedes the 10:23 ET reading of CS-7 `mustFlatten` (any bar whose clock is at or after `FlatByHHMM`, with no 17:00 cap).
  - The position's deadline is the first `FlatByHHMM` **strictly after** its original entry time. An entry at 10:00 is due at 15:55 the same day. An entry at 16:00, or at 18:20 in the Globex evening, is due at 15:55 the next calendar day. An entry exactly at 15:55 is due at the next day's 15:55.
  - `FLATBY` fires on the first bar at or after that deadline, reason unchanged. A missing 15:55 bar does not need a holiday or weekend calendar: the next bar whose time is at or after the deadline flattens (a Friday 18:20 entry is due Saturday 15:55, so the Sunday 18:00 reopen is that bar).
  - Adds and re-entries keep the original entry time. The deadline does not move to the add.
  - Embedded and the wrapper both decide this in `Manage`.
- On an early-close day (`_CmeHolidays` / M8 calendars): that day's cutoff is `RthCloseEt − 5 min` when that time is strictly after the entry and earlier than the ordinary next `FlatByHHMM`.
- A full-close holiday has no session, so there is nothing to manage.
- `HoldOvernight`: swing holds keep the stop resting overnight, and `MaxHoldDays` (int, 0 = off) applies.
- The Input default is `FlatBy`; a swing idea's registered run sets `HoldOvernight` (§6). **Analyst decision (2026-11-12), owner may override.** On a daily or longer Data1, `FlatBy` does not fire inside the bar. There is no intraday clock on that bar, so the engine treats the session rule as `HoldOvernight`: it logs once `<ID> Warning: GX SESSION FlatBy ignored on <resolution> bars: HoldOvernight`, and `Manage` does not return `FLATBY`. The 15:28 ET deadline does not change this.

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
- `RandomBars`: a seeded uniform 1…N, N as below. The seed is the CS-8 `ControlSeed` (the existing `_CustomSignal` protected property, default 12345, not an Input), journalled in the START line (**Analyst decision (2026-11-12), owner may override.**).
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

### 4.11 Wrapper Input list (`_GenericExit`, 48)
- **Stop (7):** `InitialStop`, `StopAtrMult`, `StopAtrLength`, `StopTicks`, `StopSwingBars`, `StopPct`, `FillBarDisasterStop` (§4.12).
- **Targets (7):** `TargetMode`, `T1Unit`, `T1Value`, `T1ScalePct`, `FinalTargetUnit`, `FinalTargetValue`, `SingleLotMode`.
- **Break-even (4):** `BreakEven`, `BreakEvenUnit`, `BreakEvenValue`, `BreakEvenOffsetTicks`.
- **Trail (8):** `TrailMode`, `TrailStart`, `TrailStartUnit`, `TrailStartValue`, `TrailAtrMult`, `TrailAtrLength`, `TrailBars`, `TrailEmaLength`.
- **Clock and events (7):** `TimeStopBars`, `TimeStopMinutes`, `SessionExit`, `FlatByHHMM`, `MaxHoldDays`, `NewsExitMinutes`, `RollExit`.
- **Adds and control (7):** `AddStopMode` (`EAddStopMode`, from PR #117 §7.7), `ExitControl`, `ControlExitBarsM1`, `ControlExitBarsM5`, `ControlExitBarsM15`, `ControlExitBarsH1`, `ControlExitBarsD1`.
- **Optimiser companions (8, §4.13):** `InitialStopIdx`, `T1UnitIdx`, `FinalTargetUnitIdx`, `BreakEvenIdx`, `BreakEvenUnitIdx`, `TrailModeIdx`, `TrailStartIdx`, `TrailStartUnitIdx`.

Types and defaults for all 48 are in the consolidated table, §8.2.

Each gets a header §17 block. Mode-specific inputs are ignored when their mode is off (Interactions line). The embedded engine has no Inputs of its own: the host declares the ones its spec varies.

### 4.12 Fill-bar protection (closes the E2 design choice)
E2 (§7.1) showed that only a stop sent **with** the entry (E2(c)) or MC's built-in `GenerateStopLoss` called **every bar, including while flat** (E2(b)) protects the fill bar. GX uses one of these in each mode. **Analyst decision (2026-11-12), owner may override.**

**Embedded mode (the engine is hosted in the entry signal): the required path for production strategies.**
- On every bar on which the host sends an entry order, the engine also sends the initial protective stop for the next bar, in the same `CalcBar` (long `<ID>LS`, a sell stop; short `<ID>SS`, a buy-to-cover stop; PR #117 §7.1). This is the E2(c) pattern and the engines manual TS-01 pattern (`m_lxStp.Send(m_stop)` next to `m_le.Send`).
- The level is the planned initial stop: the entry's structural stop (`EntryProvided`) or the §4.1 rule, widened by the cost floor, computed at that bar's close (the same stop passed to `sizeForRisk` / `portfolioArm`).
- From the fill bar's close onward, the same level is re-sent every closed bar and only ever tightened (§7.3 one-way rules, PR #117 §7.1 "never moved away from the trade"); it is not recomputed from the fill. R uses the actual fill and this stop (§5).
- A gap fill beyond the planned stop (long: fill ≤ planned stop; short: fill ≥ planned stop) gives a market exit on the next bar, reason `STOP`, as in PR #117 §7.3.
- Production strategies (batch 1 under GX-5 and every new `_CustomSignal` strategy) use embedded mode. The wrapper is for research on entries that cannot embed the engine.

**Wrapper mode (`_GenericExit`, a separate signal): an optional built-in disaster stop.**
- The wrapper cannot protect the fill bar with its own explicit stop (E2(a)), because it does not see a pending entry. It therefore gets one bool Input, `FillBarDisasterStop` (default **true**).
- **While flat**, on **every** bar, the wrapper sets `CurSpecOrdersMode = ESpecOrdersMode.PerContract` and calls `GenerateStopLoss(amount)`, where `amount` = the planned initial-stop distance in $ per contract = distance × BigPointValue (E2(b)). The distance is computed at that bar's close with the §4.1 mode and the cost floor, using the bar's close as the proxy entry:
  - distance-based modes (`AtrMultiple`, `FixedTicks`, `Percent`): the mode's distance (ATR on closed bars; `Percent` on the close);
  - level-based modes (`SignalBarExtreme`, `SwingExtreme`): the wrapper does not know the side of a pending entry, so it takes the distance from the close to the long level (|close − long level|) and to the short level (|short level − close|) and uses the **larger** one, so the disaster stop is never tighter than the planned stop on either side;
  - never less than the cost floor (ES 14, NQ 19 ticks).
- **The explicit wrapper stop takes over from the next bar.** On the first bar the wrapper sees the position (the fill bar's close), it stops calling `GenerateStopLoss` and sends its explicit stop (`GXLS` / `GXSS`) from the next bar, re-sent every closed bar. For the distance-based modes the explicit initial stop is the fill ∓ the **same distance** the disaster stop used on the bar before the fill, so the fill bar and the later bars share one stop and one R. For the level-based modes the explicit stop is the §4.1 level. Back to flat ⇒ the disaster stop is called again from that bar.
- **Default true, and why:** an unprotected fill bar is a known pitfall (engines manual v3.2, `_Test10`); with it off, a wrapper backtest silently ignores every fill-bar stop-out, which overstates the entry. E2(b) filled 15 of 15 fill-bar stop-outs. The cost is that the disaster level on the fill bar is based on the bar before the fill, not the fill itself, and that the wrapper never sees a trade the disaster stop closes on its fill bar (E2(b), `FIRSTSEEN` 5 of 20).
- **Logging.**
  - Once at `StartCalc`: `EXIT Info: GX FILLBAR-PROTECTION mode(DisasterStop) builtin(GenerateStopLoss PerContract) handover(next bar after fill)`; with the Input off: `EXIT Warning: GX FILLBAR-UNPROTECTED the wrapper's stop starts on the bar after the fill (E2); set FillBarDisasterStop = true or embed the engine`.
  - A fill-bar disaster exit is detected when the wrapper was flat on the previous bar's close, is flat now, and `NetProfit` changed: `EXIT Info: GX FILLBAR-STOP bar(<n>) netChange($<x>) - closed on its fill bar by the built-in disaster stop, not seen by the wrapper`. The per-run summary reports the count as `fillBarStops`.
  - In MC's trade list those exits carry MC's order name "Stop Loss" (fixed by MC). They have no GX journal row, because the wrapper never saw the position.
- **Portfolio Trader:** the same rule applies. On a fill-bar disaster exit, the MM has seen only the entry's armed `pm.R$`. Acceptance check A-E4 confirms that the built-in stop fires in PT (§8.4).

### 4.13 Optimiser companions for enum Inputs (closes the E7 design choice)
E7 (§7.1) showed that the MC optimiser lists only numeric Inputs, not enum Inputs. Owner context, 2026-11-11 (verbatim): *"You can probably create a numeric input for the test sweeps of E7, I don't worry too much about test signals we're creating."* That remark covered the scratch test signal. **Real studies: Owner-approved 2026-11-12 07:31 ET**, Ettienne: *"Yes real studies can get the numeric inputs."* The companions are therefore approved on `_GenericExit` and on every strategy that hosts the engine. Which Inputs get one, the −1 default and the out-of-range rule below are **Analyst decision (2026-11-12), owner may override.**

- **Which Inputs.** Every enum Input meant to be swept gets a companion int Input named `<Name>Idx`. In the wrapper these are the 8 exit-component choices: `InitialStop`, `T1Unit`, `FinalTargetUnit`, `BreakEven`, `BreakEvenUnit`, `TrailMode`, `TrailStart`, `TrailStartUnit`. `TargetMode`, `SingleLotMode`, `SessionExit`, `RollExit`, `ExitControl` and `AddStopMode` get **no** companion, because they change what a run is (a structural or registered-arm choice, §4.9 "a separate registered run, not a sweep"), not a parameter value. An embedded host that exposes one of the 8 for a registered sweep exposes its companion too.
- **Semantics.**
  - Default **−1** = use the enum Input.
  - **0…N−1** = override by member index, in the declared member order:

| Companion | Enum | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|---|
| `InitialStopIdx` | `EInitialStop` | `EntryProvided` | `AtrMultiple` | `FixedTicks` | `SignalBarExtreme` | `SwingExtreme` | `Percent` |
| `T1UnitIdx`, `FinalTargetUnitIdx`, `BreakEvenUnitIdx`, `TrailStartUnitIdx` | `ETriggerUnit` | `RMultiple` | `PercentOfEntry` | `Ticks` | `Points` | `CurrencyPerContract` | — |
| `BreakEvenIdx` | `EBreakEven` | `AfterT1` | `AtTrigger` | `Off` | — | — | — |
| `TrailModeIdx` | `ETrailMode` | `Chandelier` | `Atr` | `BarExtreme` | `Ema` | `RollLevel` | `None` |
| `TrailStartIdx` | `ETrailStart` | `AfterT1` | `AtTrigger` | `Immediately` | — | — | — |

  - The index is the member's position, not its numeric value; the enums are declared without explicit values, so the two agree, and a unit test pins the order. Members are only ever appended.
  - **Out of range** (< −1 or ≥ N): logged once at `StartCalc` as `<ID> Error: GX IDX-RANGE <Name>Idx(<v>) valid(-1..<N-1>) using <Name>(<enum value>)`, and the enum Input is used.
  - An override that selects an invalid mode (for example `InitialStopIdx` = 0, `EntryProvided`, in the wrapper) then follows that mode's own rule (§4.1 fallback).
- **START line.** The engine logs one Info line at `StartCalc` with every effective GX setting. For each companion pair it prints the Input, the companion and the effective value: `<ID> Info: GX START pkg(<gxPackage>) seed(<ControlSeed>) ... T1Unit(RMultiple) T1UnitIdx(-1) T1UnitEff(RMultiple) ...`. `gxPackage` (§5.3) hashes the **effective** values, so a swept run and an enum-set run with the same effective settings share one package.
- **Programme rule 9** (PR #117 overview §3; owner 2026-11-10 20:34 ET: *"make sure that they are properly defined as enumerations and not just integers. It makes the inputs slightly easier to understand."*) still holds: the enum Input stays the primary, named setting, and the companion is only an optimiser handle. The exception is owner-approved (07:31 ET, above).
- **Owner sign-off.** PR #117 overview §10 says *"do not fall back to ints without owner sign-off"*. That sign-off is the 07:31 ET answer quoted above, for test signals and real studies alike.

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
  - **Wrapper in PT:** supported now (§5.1). E4 passed on the scratch studies; acceptance check A-E4 on the implementation build (§8.4).

### 5.1 `_GenericExit` in Portfolio Trader (**Owner-approved 2026-11-11 09:16 ET**: *"3. No, we can make the standalone strategy work in portfolio trader as well for now."*)
The wrapper runs in PT as the **second signal** of the same PT strategy (one row per instrument), with the entry signal first. Facts used:
- The MM reads `Signals[0]` for `PositionSide` and currency (`_PortfolioRiskManager.view` L125–129), and the strategy's `PortfolioData` for all `pm.*` keys (L131–158).
- The MM runs after all strategies in a step (Check 7 L1).
- Signals in one strategy calculate in Signals-list order (m-port-spec §4 item 2, from the MC wiki; **re-verified by E4**).

**Requirements:**
1. **Order.** In the PT strategy's signal list, the **entry signal is first** (`Signals[0]`) and `_GenericExit` is second. That gives:
   - the MM's `Signals[0]` reads (position sign, currency, `InitialCapital`) come from the entry, as today;
   - in each bar step the wrapper calculates **after** the entry, so its `pm.*` writes are the last ones the MM sees in that step.

   The wrapper checks this at `StartCalc`. **E4, 2026-11-11:** the own index is readable at `StartCalc` via `PortfolioData` matching (ES 0, NQ 1; `mySignalIndexByName` / `ByRef` also worked). `Signals[i].Bars` throws `Unaccessible property(method): Bars` at `StartCalc`, so any `Bars` read is deferred to the first bar. If the wrapper is not after a `pm.ver` publisher, it logs `EXIT Error: _GenericExit must follow the entry signal in the Portfolio Trader signal list` and stays inert, sending no exits and no fill-bar disaster stop (§4.12). The swap run supports that Error rule: with the exit listed first, `followsEntry` was N, the exit saw the entry's previous-step data (`entryStepSeen` = bar−1, `entryCalculatedFirst` N), and the MM `lastWriter` was ENTRY (vs EXIT when the exit is listed second).
2. **The entry must publish (M-PORT).** The MM denies any strategy without `pm.ver` ("no pm.ver"). In PT, the entry must therefore be a `_CustomSignal` publisher that arms its pending entry through `sizeForRisk` / `portfolioArm` with a **planned stop**.
   - To keep sizing and R consistent, the entry should compute that planned stop with the **same GX rule** the wrapper will use: a static `_ExitEngine.InitialStop(mode, …)` helper, the same function the wrapper calls.
   - An entry that cannot publish (a third-party or legacy `SignalObject`) runs in PT only **without** the MM signal. The wrapper then manages exits and journals R, but there is no book-level control. This limitation is logged once at `StartCalc`.
3. **Who writes which key.** `PortfolioData` is one dictionary per PT strategy (**confirmed E4, 2026-11-11**, L6-style `sameRef` True from both signals and from the MM).

   | Owner | Keys | When |
   |---|---|---|
   | Entry (`_CustomSignal`) | `pm.ver`, `pm.id`, `pm.family`, `pm.cluster`, `pm.state`, `pm.side`, `pm.pos`, `pm.eq`, `pm.pnl`, `pm.ret`, `pm.uRet`, `pm.vol`, `pm.day`, `pm.stamp`, and the **arm** keys `pm.R$`, `pm.R$w`, `pm.qtyCode`, `pm.riskNew`; a GX-aware entry also writes `pm.gxPlanEntry` and `pm.gxPlanStop` (planned entry and planned widened stop, prices) at the arm, and clears them when flat and not armed (§4.2) | as today; the two plan keys at the arm |
   | `_GenericExit` (when a position is open) | `pm.riskOpen`, `pm.openR`, plus `pm.R$` and `pm.R$w` **once, on the bar the position first appears**, re-computed from the actual fill and the wrapper's initial stop; new keys `pm.gxStop` (active stop), `pm.gxOwner` = `"GX"` | every bar while in a position, after the entry's `portfolioBar()` |

   - The wrapper never writes `pm.ver`, `pm.state`, `pm.side` or any arm key while flat, and never touches `CommonPortfolioData`.
   - It overrides `PortfolioPublish => false` so the base `startPortfolio()` does not re-initialise the entry's keys (`_CustomSignal` L1702–1718).
4. **R accounting stays right**, because the MM samples after the wrapper:
   - **Sizing** (RiskParity) uses the entry's armed `pm.R$` at the arm (planned entry, planned stop).
   - **The kill / session / weekly R stream** records `riskAtOpen = pm.R$ × |pos|` on the first evaluation with a position (`_PortMM` L963–967). By then the wrapper has re-written `pm.R$` from the **actual fill** and its own stop, so the MM's 1R equals the wrapper's 1R. The difference between planned and actual R is journalled: `EXIT Info: R fill <id> planned $<armR> actual $<fillR>`.
   - **Heat / cluster / total caps** use `pm.riskOpen`, which the wrapper keeps equal to its active stop (BE, trail, add raise).
   - **Adds and the R re-base** (PR #118 §3a) need PR #118 to be built. The wrapper owns the stop raise (`AddStopMode`) and publishes `pm.addStop` / `pm.px` on the add-fill bar. Until PR #118 lands, the MM refuses adds, so PT wrapper runs have no adds.
5. **Exits are never gated.** `EntryContracts` and `AllowEntries*` apply only to entry orders, and the wrapper sends exits only. **E4-b, 2026-11-11:** deny-all (`AllowEntries = false`, `EntryContracts = 0`) left the exits identical to E4-a (0 diffs): NQ stopped at bar 104, ES T1 filled at bar 106 and FromAll at bar 116. The MM's `ForceClosePosition` closes all legs (precedence 1). `Paused` is set only when flat (Check 7 L4).
6. **Known limitation (documented).** If the signals of one strategy do **not** share one `PortfolioData`, or the calc order is not the list order, the wrapper's keys never reach the MM. The fallback for that case only (**Analyst decision (2026-11-12), owner may override.**): the entry hosts the engine (option C embedded) for PT runs. **E4, 2026-11-11, did not trigger this:** `sameRef` was True, calc order was the list order, and `RISKATOPEN` matched on both rows (ES R$100 / `riskAtOpen` $200; NQ $40 / $80). Acceptance check A-E4 (§8.4) repeats this with the real `_GenericExit` wrapper and `_PortfolioRiskManager` before any production PT result is used.

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

**Scope.** It applies to **every** strategy, not only GX: the fields are computed in the `_CustomSignal` CS-1 journal (the base), so every `_CustomSignal` strategy logs them, and both the GX engine and the wrapper use the same code. The cross-reference is in PR #117 overview §8. The requirement is owner-requested. The three Q10 choices (whole-trade MAE, equity base, thresholds) are **Owner-approved 2026-11-11 09:33 ET** (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*). The other definition choices below are **Analyst decision (2026-11-12), owner may override.**

**Fields per closed trade:**
| Field | Definition | Unit / rounding |
|---|---|---|
| `equityAtEntry` | The **strategy's own equity** at the entry fill (see "Equity base") | $, 2 dp |
| `plannedRiskDollars` | Initial stop risk at the entry fill: \|entry fill − initial **widened** stop\| × BPV × initial contracts (the CS-1 `riskDollars`, the same 1R the trade is sized and skipped on). Excludes commission | $, 2 dp |
| `plannedRiskPct` | `plannedRiskDollars / equityAtEntry × 100`, e.g. 1.000 | percent, 3 dp |
| `plannedRiskPctPeak` | The largest planned risk over the trade: max over segments of the segment 1R $ (the post-add risk at the raised stop, §5 / PR #117 §7.7) ÷ `equityAtEntry` × 100. Equals `plannedRiskPct` for a trade with no add | percent, 3 dp |
| `maeTradeDollars` | **Worst adverse excursion of the whole trade, from the entry fill to the exit** (owner-approved 09:33 ET, Q10.1): min over the trade's bars of (realised P&L of this trade's partial exits so far + open P&L of all open contracts, incl. adds, marked at the bar's adverse extreme). Floored at 0: a trade that never went below its entry P&L has MAE 0. Reported as a positive number | $, 2 dp |
| `actualRiskPct` | `maeTradeDollars / equityAtEntry × 100`: how far the market actually moved against the position, as % of the account at entry | percent, 3 dp |
| `actualToPlanned` | `actualRiskPct / plannedRiskPct` = `maeTradeDollars / plannedRiskDollars` = MAE in **initial** R. 1.00 = the full planned risk was used, > 1 = stop slippage, a gap through the stop, or risk added by adds | ratio, 2 dp |
| `maeOpenDollars` | Secondary: the worst open-only excursion of the contracts still open (ignores realised partials) | $, 2 dp |
| `realizedPnlPct` | The trade's net P&L (MC trade objects, after MC commission and slippage settings) ÷ `equityAtEntry` × 100, signed | percent, 3 dp |

**Definitions** (Analyst decision (2026-11-12), owner may override, unless marked owner-approved):
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
  - MC's own `MaxDrawDown` (the current CS-1 `maeDollars`) is kept unchanged in its column for comparison. The new whole-trade value is always the new column `maeTradeDollars`, so existing readers of `maeDollars` keep working (**Analyst decision (2026-11-12), owner may override.**).
- **Adds.** MAE is computed on the **cumulative** position: every open contract, including adds from their fill onward, against its own fill price. The adverse move is therefore measured in $, not per contract.
  - `plannedRiskPct` stays the initial planned risk, so a trade with adds can show `actualToPlanned` > 1 without any slippage. `plannedRiskPctPeak` and `addCount` let reporting separate this.
  - The re-based R (`rMultiple`) is unchanged by this section.
- **Scale-outs** (owner-approved 09:33 ET, Q10.1: whole trade). The primary `maeTradeDollars` includes realised P&L from partial exits, so after T1 the locked-in profit offsets later adverse moves: it is "how much of the account the trade as a whole was down at worst". `maeOpenDollars` reports the remaining-contracts view.
- **Costs and slippage.** MAE uses actual fill prices, so entry slippage is included. Commissions are **excluded** from MAE and planned risk (they are price-excursion measures) and **included** in `realizedPnlPct`.
- **Signs.** `plannedRiskPct`, `actualRiskPct`, `maeTradeDollars` and `actualToPlanned` are ≥ 0 (adverse = positive). `realizedPnlPct` is signed (loss < 0).
- **Rounding.** Computed in double; rounded only when written (CSV: percents 3 dp, ratios 2 dp, $ 2 dp; invariant culture, `.` decimal). Percent fields are in percent units (1.000 = 1%), not fractions.
- **Undefined values.** `plannedRiskDollars` ≤ 0 or NaN (no stop known) ⇒ `plannedRiskPct`, `actualToPlanned` = empty, and `riskFlag` gets `NO-PLANNED-RISK`. `equityAtEntry` ≤ 0 ⇒ every percent empty.

**Close log line** (Journal level; prefix `EXIT` on charts, `PORT` in Portfolio Trader):
`EXIT CLOSE <id> side=<long|short> qty=<initial>+<adds> reason=<exit reason> R=<rebased> Rorig=<r> plannedRiskPct=<x.xxx> plannedRiskPctPeak=<x.xxx> actualRiskPct=<y.yyy> actualToPlanned=<z.zz> realizedPnlPct=<±w.www> mae=$<maeTradeDollars> maeScope=<bar|magnifier> equity=$<e>` (`equity` = the strategy's own equity at entry; `mae` = `maeTradeDollars`)

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
  - `exitReason` (the §4.10 reason: `STOP`, `T1`, `TARGET`, `BE-STOP`, `TRAIL`, `TRAIL-THROUGH`, `NEWS`, `FLATBY`, `ROLL`, `TIME`, `CONTROL`, `FLAT-BY-MM`; a wrapper fill-bar disaster exit has no row, §4.12);
  - `rMultiple` (re-based) and `rOrig`;
  - `mfeR` / `maeR` on the initial R (MC `MaxRunUp`/`MaxDrawDown` ÷ initial risk, existing);
  - `segments`;
  - `gxPackage` (a hash of the effective GX settings, so a run can be matched to its exit package);
  - `riskFlag` (`OVER-BUDGET`, `NO-PLANNED-RISK` or empty, §5.2, §5.3);
  - the planned-versus-actual risk columns of §5.3.
- **Per-run summary:** `fillBarStops` (wrapper, §4.12), expectancy in R, the R distribution by exit reason, MFE/MAE scatter in R (an MFE histogram for winners and losers, where the T1/trail levels sit), and the CS-8 forward MFE/MAE for the same signals.
- **Acceptance:** the programme's usual criteria (PR #117 overview §6), unchanged.

## 6. Baseline exit package for entry-only ideas (**Owner-approved 2026-11-11 09:16 ET**, predeclared, not fitted)
Owner (09:16 ET): *"2. Yes, they are good"*. This is the default of every Input above (the rev 1.0 additions, `FillBarDisasterStop` and the `<Name>Idx` companions, are Analyst decisions, §8.2), chosen to mirror the batch-1 framework and the existing `_ExitHalfRiskHalfRoll` (half at +1R, runner trailed):

| Component | Baseline |
|---|---|
| Size | 2 contracts (PR #117 §7.2), so the scale-out is exercised |
| Fill-bar protection | Embedded: the initial stop is sent with the entry. Wrapper: `FillBarDisasterStop` true (§4.12; **Analyst decision (2026-11-12), owner may override.**) |
| Initial stop | Embedded: `EntryProvided`. Wrapper: `AtrMultiple` 1.5 × ATR(14) on Data1 (owner-approved default 18:18–18:21 ET; `SignalBarExtreme` selectable), floored at the cost floor (ES 14 / NQ 19 ticks) |
| T1 | `T1Unit` `RMultiple`, `T1Value` 1.0, `T1ScalePct` 50 ⇒ ⌊q/2⌋ at +1.0R from the planned entry (planned R, §4.2), `OrderExit.Total`; q = 1 ⇒ `ExitAtT1` |
| Break-even | `AfterT1`, `BreakEvenOffsetTicks` 1 (entry + 1 tick); `BreakEvenUnit` `RMultiple` / `BreakEvenValue` 1.0 used only by `AtTrigger` |
| Runner trail | `Chandelier` 3.0 × ATR(22) on Data1, from T1 (`AfterT1`; `TrailStartUnit` `RMultiple` / `TrailStartValue` 1.0 used only by `AtTrigger`): **below the highest high for longs / above the lowest low for shorts, one-way** (long only moves up, short only moves down; owner-approved 09:34 ET) |
| Final target | none (`FinalTargetUnit` `RMultiple`, `FinalTargetValue` 0) |
| Time stop | off |
| Session | intraday `FlatBy`: the first 15:55 ET strictly after the original entry (early close: close − 5 min); an entry after 15:55 is held to the next day's 15:55 (§4.6, owner 15:28 ET). Swing ideas `HoldOvernight` with `ERollExit.FlattenBeforeSplice`. Daily bars do not flatten inside the bar |
| News exit | `NewsExitMinutes` 0 (off), approved here as part of the baseline; PR #117 overview §7.6 uses the same default and still marks it there for owner confirmation |
| Adds | as the entry decides; `AddStopMode` `RaiseToAddLow`; R re-based on the add fill |
| Control | `None` in the baseline arm. Each entry-only idea also runs a `FixedBars` control arm with N per timeframe (1m 30, 5m 12, 15m 8, 60m 6, daily 5) and reports CS-8 forward MFE/MAE (owner-approved 18:18–18:21 ET, Q4/Q5; other timeframes: nearest listed at or below, owner-approved 18:54 ET, §4.9) |
| Precedence | §4.10 |
| Costs and fills | Batch-1 settings (PR #117 §4): commissions and slippage as approved, 2 ticks of open slippage, closed-bar orders, no IOG |

## 7. Native checks for the MiniPC (backtest only, data ≤ 31 Dec 2024, temporary sandbox studies, no csproj commit)
| # | Question | How | Pass |
|---|---|---|---|
| E1 | Can an exit-only signal on the same chart manage another signal's position, and what does it see on the fill bar? | `_TestGxEntry` (buys 2 on bar 100) + `_TestGxExit` (journals `PositionSide`, `CurrentPosition.OpenTrades`, `AvgEntryPrice` each bar, and `Bars.Info.Resolution` (type, size) once on 1m, 5m, 15m, 60m, daily and one 2m chart; sends `Sell 1 Total` at bar 105, `Sell FromAll` at bar 110) | **PASS, 2026-11-11.** Exit fills as sent on all six timeframes; values visible from the fill bar's close; resolution values recorded for the §4.9 mapping |
| E2 | Fill-bar protection | Same pair on a bar where price trades through the stop inside the fill bar. Runs: (a) wrapper explicit stop, (b) wrapper `GenerateStopLoss` every bar, (c) entry sends its stop with the entry, (d) `GenerateStopLoss` only while in a position | **Recorded, 2026-11-11.** (c) and (b) stop out on the fill bar; (a) and (d) do not. A separate wrapper cannot protect the fill bar. Design closed in §4.12; acceptance check A-E2 (§8.4) |
| E3 | Two exits on one bar from two signals; Format Signals order | Two exit signals both `MarketNextBar` on bar 105 (FromAll+FromAll, both list orders; Total+Total; FromAll+Total) | **PASS, 2026-11-11.** No overshoot. List order decides FromAll+FromAll; Total+Total both fill; FromAll takes 2 against Total |
| E4 | PT: two signals per strategy (**verification item; the wrapper is already specified for PT, §5.1**) | Entry + wrapper in one PT strategy, MM on. Journal `Signals.Length`, `Signals[0]` name, each signal's own index, `MyPortfolioData` reference identity from both signals (L6-style `sameRef`), the calc order of the two signals in one step, the MM's `riskAtOpen` against the wrapper's fill R, and that `EntryContracts` / `AllowEntries` do not affect the wrapper's exits | **PASS, 2026-11-11, on the scratch studies.** Shared dictionary, list-order calc, MM R = wrapper R. Acceptance check A-E4 with the real wrapper and `_PortfolioRiskManager` (§8.4) |
| E5 | Repeated partials | `Sell 1 Total` twice with one order object, with and without `ExitFromOneEntryOnce.FillAndKillExitOrder` | **PASS, 2026-11-11.** Same object does not fill twice; FillAndKill does. GX uses separate order objects per partial |
| E6 | Pyramiding setting label | Strategy Properties dialog | **PASS by reference** to PR #118 A1 (2026-11-11) |
| E7 | Enum Inputs in the Format dialog and the optimiser | One enum Input (e.g. `ETriggerUnit` as `FinalTargetUnit`) | **Recorded, 2026-11-11.** Named members show in the Inputs dropdown. The optimiser lists only numeric Inputs, not enums. A numeric stand-in was used for the sweep. Design closed in §4.13; acceptance check A-E7 (§8.4) |
| E8 | IOG per signal | Wrapper IOG on, entry off | **PASS, 2026-11-11.** Confirmed per-signal; GX rule "IOG off" stays |

### 7.1 Results (MiniPC, 2026-11-11, backtest only, data in 2024)

Scratch studies `_TestGxEntry`, `_TestGxExit`, `_TestGxExitB`, `_TestGxExitFK` and `_TestGxMM`, compiled with F7 only. Logs are on the MiniPC at `C:\Share\MultiCharts\TestGX\out\` (`E1-*-ES`, `E2-a`…`d`, `E3-*`, `E4-a` / `E4-b` / `E4-swap`, `E5-*`, `E7-RMultiple-ES`, `E7-opt`, `E8-ES`). Raw logs are not committed (they contain prices). All chart runs used dxFeed `@ES` 1 minute, 2024-11-18 → 2024-11-22, unless noted. E1 also ran 2m, 5m, 15m, 60m (2024-11-04 → 2024-11-22) and daily (2024-06-03 → 2024-11-22, `EntryBar` 40). E4 used Portfolio Trader workspace `TestGX E4 Backtest.pws` (`@ES` + `@NQ` 1 minute, MM `_TestGxMM`). No price values from the logs are recorded here; dollar P&L and trade counts are.

**E1 — PASS** across 1m, 2m, 5m, 15m, 60m and daily. The exit-only signal managed the entry's position on every timeframe: `GXX1T` Sell 1 Total at rel 4 filled 2→1 on the next bar (bar 106; daily, with `EntryBar` 40, bar 46) and `GXXA` Sell FromAll at rel 9 filled 1→0 on the next bar (bar 111; daily bar 51). On the fill bar's close the exit saw `MarketPosition` 2, `PositionSide` 1 (long), `OpenTrades` 1, `AvgEntryPrice` equal to the fill, and the `GXLE` leg. Resolution values are in §4.9; all six match the control-bar mapping.

**E2 — recorded; design closed in §4.12, acceptance check A-E2 (§8.4).** Twenty trades, 1-tick stop, `@ES` 1 minute.

| Variant | Who protects | Through on the fill bar | Exited on the fill bar (of those through) | Net profit | Notes |
|---|---|---|---|---|---|
| a | Exit explicit stop `GXXS` from the first bar it sees the position | 15 | 0 of 15 | −$450 | Stop works from the next bar (17 `GXXS`, 3 time exits `GXXA`) |
| b | Exit `GenerateStopLoss` every bar, flat or not (`PerContract`, 1 tick × BPV) | 15 | 15 of 15 | — | Order name "Stop Loss" (17 "Stop Loss", 3 `GXXA`). The exit-only signal never sees the 15 fill-bar exits (`FIRSTSEEN` count 5) |
| c | Entry sends sell stop `GXLSe` with the entry | 14 | 14 of 14 | — | 17 `GXLSe`, 3 `GXXA`. Through-count is 14 rather than 15 because this stop is the entry-bar close minus 1 tick, not the fill minus 1 tick |
| d | Exit `GenerateStopLoss` only while in a position (the `_StopsAndTargets` pattern) | 15 | 0 of 15 | −$450 | Same timing as (a); order name "Stop Loss" from the next bar |

Implication: a separate wrapper cannot protect the fill bar. The fill-bar stop must come from the entry, or from built-in `GenerateStopLoss` called every bar. GX uses (c) in embedded mode and (b) as the wrapper's disaster stop (§4.12).

**E3 — PASS.** No overshoot: the position goes 2→0 at bar 106.

- FromAll + FromAll: only the first-listed signal's order fills (`GXXA` with Exit then ExitB; `GXBA` when swapped). List order decides.
- Total + Total: both fill, 1 each (`GXX1T` and `GXB1T`).
- FromAll + Total: FromAll takes 2 (`GXXA`); the Total order does not fill.

**E4 — PASS on the scratch studies** (`TestGX E4 Backtest.pws`, ES+NQ 1m, MM `_TestGxMM`; logs `E4-a`, `E4-b`, `E4-swap`). Acceptance check A-E4 (§8.4) repeats it with the real `_GenericExit` and `_PortfolioRiskManager`.

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

**E7 — recorded; design closed in §4.13, acceptance check A-E7 (§8.4).** The named `ETriggerUnit` stand-in members (`FinalTargetUnit`) show in the Inputs dropdown. RMultiple 2 gives a target at fill + 4 pts; the clean confirmation run (`E7-RMultiple-ES`) had 20 trades, net −$1,125, with 9 stop exits (`GXXS`) and 11 time exits (`GXXA`) and no target (`GXXT`) fills. The optimiser lists only numeric Inputs, not enums. A numeric stand-in `FinalTargetUnitIdx` (0…4, −1 = use the enum) was added to the scratch signal with Ettienne's approval (2026-11-11). Regular Exhaustive sweep, `FinalTargetValue` 2:

| Idx | Unit | Net Profit | Trades |
|---|---|---|---|
| 0 | RMultiple | −1,125 | 20 |
| 1 | PercentOfEntry | −1,125 | 20 |
| 2 | Ticks | −675 | 20 |
| 3 | Points | −150 | 20 |
| 4 | CurrencyPerContract | −325 | 20 |

`E7-opt.txt` confirms `effUnit` switched per iteration (the five `START` lines carry `idx` 0…4 and the matching `effUnit`). Idx 0 and 1 are identical because **neither target filled within the 10-bar window**: the 2R target is fill + 4 pts, and the 2% target is fill + about 118 pts; the two exit-fill sequences are the same bar-for-bar as each other and as the clean RMultiple run (9 stops, 11 time exits, 0 target fills). The tighter units (2 ticks, 2 points, $2 → 1 tick) did produce target fills and different nets. This is not evidence that the two units compute the same level.

Spec implication, closed in rev 1.0: every sweepable enum Input gets a `<Name>Idx` companion (§4.13), checked natively as A-E7 (§8.4).

**E8 — PASS.** IOG is per signal. The exit with IOG on had `calls(4) insideCalls(2)` and `iogEnabled(True)` per bar; the entry had `calls(1) insideCalls(0)` and `iogEnabled(False)`. The exit filled intrabar at bar 105 (vs 106 in E1), and FromAll at bar 110 intrabar. The GX rule "IOG off" stays.

**After this recording:** the implementation-build acceptance checks are in §8.4 (GX-4). Scratch cleanup of the `_TestGx*` studies, workspaces and logs awaits Ettienne's approval. Leave the Check7 files alone.

## 8. Implementation hand-off for Coder (approved 2026-11-12; separate PRs)

**Owner instruction, 2026-11-12 07:13 ET (verbatim):** *"Alright, you can recheck the tests left and please complete the spec so @Coder can start implementing strategies from it"*. Coder can start GX-1 now. Each GX step is its own PR on its own feature branch; Ettienne merges.

### 8.1 Order of work and files

| Step | Creates | Touches | Done when |
|---|---|---|---|
| **GX-1** engine | `_GenericExit.Strategy.CS` (repo root = the studies folder `Techniques\CS`). In GX-1 it holds the pure engine: the §4 enums and `public static class _ExitEngine` plus its state-machine classes in `namespace PowerLanguage.Function`, no MultiCharts types, and an inert `_GenericExit : _CustomSignal` placeholder (no Inputs, no orders) inside `#if !GX_OFFMC` … `#endif` so the file is a valid study. `tests/ExitEngineTests/ExitEngineTests.csproj`, `Program.cs`, `run.sh` (net8.0, C# 7.3, links `..\..\_GenericExit.Strategy.CS` with `<DefineConstants>GX_OFFMC</DefineConstants>`, so only the pure engine compiles there) | `PLStudies.csproj`: the one owner-approved hand-written line `<Compile Include="_GenericExit.Strategy.CS" />` (§8.7) | `tests/ExitEngineTests/run.sh` and every existing `tests/*` suite pass, including `RepoLintTests`; the file compiles in the MC Editor with F7 |
| **GX-2** embedded | — | `_CustomSignal.Strategy.CS`: protected helpers `exitPlan(...)` (at the entry: mode, planned entry, planned stop, the stop sent **with** the entry order §4.12, the planned T1 and final target §4.2, and the `pm.gxPlanEntry` / `pm.gxPlanStop` keys at the arm §5.1), `exitBar()` (each closed bar: precedence §4.10, BE, trail, T1, clock, control), `portfolioStop` / `portfolioArmAdd` hooks (§5), the §5.3 CS-1 columns and close line for **every** `_CustomSignal` strategy, and the GX START line (§4.13). No new base `[Input]`. `docs/engines-manual.md`: a GX section | Unit tests green; a strategy that does not call `exitPlan` behaves exactly as before (G0, D9a and the existing suites unchanged) |
| **GX-3** wrapper | `sandbox/_GX_00_TestHost.Strategy.CS` (the A-check test host, §8.4; sandbox naming lint, no csproj line; for a native run it gets the temporary selective-test `Compile Include` of docs/research-strategy-header.md, removed before commit) | `_GenericExit.Strategy.CS`: the real wrapper replaces the placeholder (derives from `_CustomSignal`, overrides `PortfolioPublish => false`; order and log prefix ID `GX`: `GXLS`/`GXSS`, `GXLT1`/`GXST1`, `GXLT`/`GXST`, `GXLX`/`GXSX`; the 48 Inputs of §8.2, each with a header §17 block; chart **and** Portfolio Trader rules §5.1; over-budget rule §5.2; fill-bar disaster stop §4.12; optimiser companions §4.13; planned-T1 keys §4.2). No csproj change. The five legacy exit studies get the superseded header note (§8.5). `docs/engines-manual.md` lists them as superseded | Unit tests green; Analyst's acceptance checks §8.4 can start |
| **GX-4** acceptance | — (Analyst, on the MiniPC) | `specs/live-check-sheet.md` (the A-checks), §7.1 of this spec | Every check in §8.4 passes |
| **GX-5** migration | — | batch-1 strategies, one PR per strategy (§8.6) | that strategy's equivalence re-check passes |

`_GX_00_TestHost` (`sandbox/_GX_00_TestHost.Strategy.CS`, public class `_GX_00_TestHost`, the sandbox name pattern `_<ID>_<NN>_<Name>` with `GX` as the ID and `00` for a test host; it carries the research header like every sandbox study) is **Analyst decision (2026-11-12), owner may override.** It derives from `_CustomSignal` and enters on a fixed cadence so the A-checks are repeatable. Inputs: `EmbedEngine` (bool, default true; true = host the engine with the §8.2 defaults, false = publish and arm only, through `sizeForRisk` / `portfolioArm` with the planned stop from `_ExitEngine.InitialStop`, for use with `_GenericExit`), `EntryEveryBars` (int, default 30), `EntrySide` (typed enum `ETestGxSide { Long, Short, Alternate }`, default `Alternate`). It is a test signal, not a strategy, and is never used for a research result.

**Where the engine lives** (**Analyst decision (2026-11-12), owner may override.**). Ettienne approved one csproj line, for `_GenericExit` (07:31 and 07:33 ET). The engine is therefore part of `_GenericExit.Strategy.CS`, not a separate `_ExitEngine.Function.CS`, which would need a second line under `tests/RepoLintTests`. Every root study compiles into the one PLStudies assembly, so `_CustomSignal` and every hosting strategy can call `_ExitEngine` in that file, as today's strategies use `_CustomSignal` from its own `.Strategy.CS` file. The `#if !GX_OFFMC` guard keeps the off-MC tests on the pure part only; no other repo file uses `#if` today, so the GX-1 F7 compile in the MC Editor is the check that the guard is harmless there. Options not taken: a separate `_ExitEngine.Function.CS` (cleanest, but a second csproj line that Ettienne has not approved; it stays the fallback if the guard fails); putting the engine in `_CustomSignal.Strategy.CS` or another listed file (MC types there make off-MC tests impossible, or it mixes unrelated engines).

The enums of §4 (`EInitialStop`, `ETargetMode`, `ESingleLotMode`, `ETriggerUnit`, `EBreakEven`, `ETrailMode`, `ETrailStart`, `ESessionExit`, `ERollExit`, `EExitControl`, `EAddStopMode { RaiseToAddLow, KeepCurrent }`) are top-level public enums in `_GenericExit.Strategy.CS` (the engine part, `namespace PowerLanguage.Function`), declared without explicit numeric values in the order shown in §4. They are top-level, not nested in a strategy class as PR #117 overview §3 rule 9 describes for single strategies, because the engine, the wrapper and every embedded host share them (**Analyst decision (2026-11-12), owner may override.**). Defaults are set in the constructor, never by member order (rule 9). None of these names exists in the repo today (checked on `47ffda2`); if a batch-1 PR defines `ESingleLotMode` or `EAddStopMode` first, GX reuses that definition instead of declaring a second one.

### 8.2 Consolidated Inputs and defaults (`_GenericExit`; 48)

"Source" says where each default comes from. "Owner" items are quoted where the section is referenced; every "Analyst" item is **Analyst decision (2026-11-12), owner may override.** In embedded mode the engine has no Inputs: the host uses these defaults, except `InitialStop` = `EntryProvided`, and declares as Inputs only what its spec varies (§4.11).

| # | Input | Type | Default | § | Source |
|---|---|---|---|---|---|
| 1 | `InitialStop` | `EInitialStop` | `AtrMultiple` (embedded host: `EntryProvided`) | 4.1 | Owner 18:18–18:21 ET (Q6) |
| 2 | `StopAtrMult` | double | 1.5 | 4.1 | Owner 09:16 ET (baseline) |
| 3 | `StopAtrLength` | int | 14 | 4.1 | Owner 09:16 ET (baseline) |
| 4 | `StopTicks` | int | 0 (must be > 0 with `FixedTicks`) | 4.1 | Analyst |
| 5 | `StopSwingBars` | int | 5 | 4.1 | Analyst |
| 6 | `StopPct` | double | 0 (must be > 0 with `Percent`) | 4.1 | Analyst |
| 7 | `FillBarDisasterStop` | bool | true | 4.12 | Analyst |
| 8 | `TargetMode` | `ETargetMode` | `ScaleOutThenRunner` | 4.2 | Owner 09:16 ET (baseline) |
| 9 | `T1Unit` | `ETriggerUnit` | `RMultiple` | 4.2 | Owner 09:16 ET (answer 2) |
| 10 | `T1Value` | double | 1.0 | 4.2 | Owner 09:16 ET (baseline) |
| 11 | `T1ScalePct` | int, 0–100 | 50 | 4.2 | Owner 09:16 ET (baseline) |
| 12 | `FinalTargetUnit` | `ETriggerUnit` | `RMultiple` | 4.2 | Owner 18:18–18:21 ET (units) |
| 13 | `FinalTargetValue` | double | 0 (off) | 4.2 | Owner 09:16 ET (baseline) |
| 14 | `SingleLotMode` | `ESingleLotMode` | `ExitAtT1` | 4.2 | Owner 2026-11-10 (batch 1) |
| 15 | `BreakEven` | `EBreakEven` | `AfterT1` | 4.3 | Owner 09:16 ET (baseline) |
| 16 | `BreakEvenUnit` | `ETriggerUnit` | `RMultiple` | 4.3 | Owner 09:16 ET (answer 2) |
| 17 | `BreakEvenValue` | double | 1.0 | 4.3 | Owner 09:16 ET (answer 2) |
| 18 | `BreakEvenOffsetTicks` | int, ≥ 0 | 1 | 4.3 | Owner 09:16 ET (baseline) |
| 19 | `TrailMode` | `ETrailMode` | `Chandelier` | 4.4 | Owner 09:16 ET (baseline) |
| 20 | `TrailStart` | `ETrailStart` | `AfterT1` | 4.4 | Owner 09:16 ET (baseline) |
| 21 | `TrailStartUnit` | `ETriggerUnit` | `RMultiple` | 4.4 | Owner 18:18–18:21 ET (units) |
| 22 | `TrailStartValue` | double | 1.0 | 4.4 | Owner 18:18–18:21 ET (units) |
| 23 | `TrailAtrMult` | double | 3.0 | 4.4 | Owner 09:16 ET (baseline) |
| 24 | `TrailAtrLength` | int | 22 | 4.4 | Owner 09:16 ET (baseline) |
| 25 | `TrailBars` | int | 2 | 4.4 | Analyst |
| 26 | `TrailEmaLength` | int | 20 | 4.4 | Analyst |
| 27 | `TimeStopBars` | int | 0 (off) | 4.5 | Owner 09:16 ET (baseline: off) |
| 28 | `TimeStopMinutes` | int | 0 (off) | 4.5 | Owner 09:16 ET (baseline: off) |
| 29 | `SessionExit` | `ESessionExit` | `FlatBy` | 4.6 | Owner 09:16 ET (baseline, intraday) |
| 30 | `FlatByHHMM` | int, ET clock | 1555 | 4.6 | Owner 09:16 ET (baseline) |
| 31 | `MaxHoldDays` | int | 0 (off) | 4.6 | Analyst |
| 32 | `NewsExitMinutes` | int, 0–120 | 0 (off) | 4.7 | Owner 09:16 ET (baseline) |
| 33 | `RollExit` | `ERollExit` | `FlattenBeforeSplice` | 4.8 | Owner 09:16 ET (baseline, swing) |
| 34 | `AddStopMode` | `EAddStopMode` | `RaiseToAddLow` | 5 | Owner 09:16 ET (baseline) |
| 35 | `ExitControl` | `EExitControl` | `None` | 4.9 | Owner 18:18–18:21 ET (Q4: control is a separate arm) |
| 36 | `ControlExitBarsM1` | int | 30 | 4.9 | Owner 18:18–18:21 ET (Q5) |
| 37 | `ControlExitBarsM5` | int | 12 | 4.9 | Owner 18:18–18:21 ET (Q5) |
| 38 | `ControlExitBarsM15` | int | 8 | 4.9 | Owner 18:18–18:21 ET (Q5) |
| 39 | `ControlExitBarsH1` | int | 6 | 4.9 | Owner 18:18–18:21 ET (Q5) |
| 40 | `ControlExitBarsD1` | int | 5 | 4.9 | Owner 18:18–18:21 ET (Q5) |
| 41 | `InitialStopIdx` | int | −1 (use the enum) | 4.13 | Analyst |
| 42 | `T1UnitIdx` | int | −1 | 4.13 | Analyst |
| 43 | `FinalTargetUnitIdx` | int | −1 | 4.13 | Analyst |
| 44 | `BreakEvenIdx` | int | −1 | 4.13 | Analyst |
| 45 | `BreakEvenUnitIdx` | int | −1 | 4.13 | Analyst |
| 46 | `TrailModeIdx` | int | −1 | 4.13 | Analyst |
| 47 | `TrailStartIdx` | int | −1 | 4.13 | Analyst |
| 48 | `TrailStartUnitIdx` | int | −1 | 4.13 | Analyst |

Not Inputs: the cost floor (M-INSTR), the 1% budget (P1), `ControlSeed` (CS-8, 12345), `TraceLevel` (the existing base Input), and the same-bar precedence (§4.10).

### 8.3 Unit tests (`tests/ExitEngineTests`, off-MC, `dotnet run`, linking `_GenericExit.Strategy.CS` with `GX_OFFMC`)
Each item is long **and** short unless it is side-free.
1. **Initial stop:** every `EInitialStop` mode's level; the cost floor (ES 14, NQ 19 ticks) widens a closer stop; `AtrMultiple` is the default; `SignalBarExtreme` long/short levels; the invalid-setting fallback to `AtrMultiple` with its Error line (§4.1).
2. **Fill-bar disaster distance (§4.12):** the $ per contract amount for each mode from a bar close; level-based modes take the larger of the long and short distances; never below the cost floor; the explicit wrapper initial stop of a distance-based mode equals fill ∓ the previous flat bar's distance; the handover sequence flat → in position → flat (disaster called only while flat).
3. **`ETriggerUnit` levels** for T1, break-even, the final target and the trail start: each unit; rounding away from the entry; value ≤ 0 with the feature on ⇒ Error, feature off.
4. **Defaults reproduce rev 0.6:** `FinalTargetValue` 0 ⇒ no final target; `TrailStart = AtTrigger` with `RMultiple` 1.0 ⇒ starts at +1R (long) / −1R (short).
5. **T1 quantity** (§4.2): q = 1, 2, 3, 5 with 0, 33, 50 and 100 %; `SingleLotMode`; planned T1 and final target from the planned entry and planned R (not from the fill); T1-SKIPPED when the actual fill is at or beyond the planned T1 ⇒ no T1, trail all, Info line, `t1Skipped` = 1, and not when it is short of it; the planned T1 is unchanged by a fill away from the plan; `SingleTarget` with the planned final target passed; the wrapper's plan source: valid `pm.gxPlanEntry`/`pm.gxPlanStop` ⇒ `plan`, missing, non-finite, wrong side or written after the fill ⇒ `fill` with the Info line once per run.
6. **Break-even and trail:** `AfterT1` and its fallback to `AtTrigger` when no T1 exists (both BE and trail start, Info once); one-way rules; the cap one tick inside the last close; `TRAIL-THROUGH`.
7. **Precedence (§4.10):** a bar where several exits fire gives exactly one reason, in order 1–9.
8. **Clock:** time stop in bars and minutes; `FlatBy` deadline is the first 15:55 strictly after the original entry (10:00 → same day, 16:00 and 18:20 → next day, an entry at 15:55 waits a day, a missed 15:55 bar flattens on the next bar, an add keeps the original entry); early-close day; `FlatBy` on daily bars ⇒ `HoldOvernight` with the Warning and no `FLATBY`; `MaxHoldDays`; news window `0 < m < N + B`; roll flatten before the splice.
9. **Control bars:** 1, 5, 15, 60 min and daily ⇒ 30/12/8/6/5; 2 ⇒ 30, 10 ⇒ 12, 30 ⇒ 8, 240 ⇒ 6, weekly ⇒ 5; seconds / tick / range bars ⇒ 30 + Warning; an Input override (e.g. `ControlExitBarsM5` = 20) wins; 0 ⇒ control off; `FixedBars` exits after exactly N closed bars with the disaster stop still active; `RandomBars` reproducible for a seed.
10. **Optimiser companions (§4.13):** −1 ⇒ the enum; each index 0…N−1 ⇒ the member in the §4.13 table; −2 and N ⇒ Error and the enum; a pinned member-order test for all five companion enums; the START line prints Input, Idx and effective value for all 8 pairs; `gxPackage` is equal for a swept run and an enum-set run with the same effective settings.
11. **R:** initial R from the widened stop; the add re-base by segments, `R` and `Rorig`; `AddMinR` on the current segment, triggers on the initial R (08:58 ET ruling).
12. **§5.3 fields:** `plannedRiskPct`, `plannedRiskPctPeak`, `maeTradeDollars` (whole trade with partials and adds, exit-bar cap), `maeOpenDollars`, `actualToPlanned`, `realizedPnlPct`, the undefined-value rules and rounding.
13. **Over-budget (§5.2):** one Warning per trade, the dedupe key, the `PORT` prefix in PT, `riskFlag`.

### 8.4 Native acceptance checks on the implementation build (Analyst, MiniPC)
Backtest only, data ≤ 31 Dec 2024 (`@ES`, `@NQ`, 1 minute, a dated From/To range in 2024 set before clicking OK), F7 compile only, no Forward Testing, no broker connection. The scratch-study results of 2026-11-11 (§7.1) are the reference. Results go into `specs/live-check-sheet.md` and §7.1, with no prices. The host `sandbox/_GX_00_TestHost.Strategy.CS` is loaded with the temporary selective-test `Compile Include` of docs/research-strategy-header.md; if the MC Editor will not import from `sandbox\` (not yet verified there), the documented workaround is a local, uncommitted root copy, deleted after the test. Nothing from the test is committed except the recorded results.

| # | Check | Set-up | Pass |
|---|---|---|---|
| **A-E2** | Fill-bar protection, both modes (§4.12) | (1) `_GX_00_TestHost` with `EmbedEngine` true; (2) `_GX_00_TestHost` with `EmbedEngine` false plus `_GenericExit` with `FillBarDisasterStop` true; (3) the same with it false. A stop tight enough (e.g. `FixedTicks` at the cost floor) that some fills trade through it on the fill bar | (1) every trade whose fill bar crosses the stop exits on that bar under `<ID>LS` / `<ID>SS`; (2) every such trade exits on that bar under MC's "Stop Loss", the `FILLBAR-STOP` Info line and `fillBarStops` count match, and from the bar after the fill the explicit `GXLS` / `GXSS` stop rests on every bar with no unprotected bar and no double exit; (3) 0 fill-bar exits and the `FILLBAR-UNPROTECTED` Warning once |
| **A-E4** | Portfolio Trader with the real studies (§5.1) | PT workspace with `@ES` + `@NQ` 1 minute; each strategy `_GX_00_TestHost` (`EmbedEngine` false) first, `_GenericExit` second; the real `_PortfolioRiskManager` | `sameRef` True; calc order = list order; MM `riskAtOpen` = the wrapper's fill R; the `R fill` Info line present; deny-all leaves the exits unchanged (0 diffs); swapped order ⇒ the Error line and an inert wrapper; no `Bars` exception at `StartCalc`; the fill-bar disaster stop fires in PT; the wrapper reads `pm.gxPlanEntry` / `pm.gxPlanStop` (`t1PlanSource` = `plan`). Also one run with `EmbedEngine` true and no wrapper: `pm.riskOpen` follows every stop move |
| **A-E7** | Enum Inputs and companions (§4.13) | `_GenericExit` on `@ES` 1 minute | The 8 enum Inputs show their named members in the Format Inputs dropdown (enums declared in the engine part of `_GenericExit.Strategy.CS`); the optimiser lists the 8 `<Name>Idx` Inputs; an Exhaustive sweep of `FinalTargetUnitIdx` 0…4 and one of `InitialStopIdx` 0…5 give START lines whose effective values change per iteration as the §4.13 table says; `FinalTargetUnitIdx` = 9 gives the `IDX-RANGE` Error and the enum value. No optimiser result is applied to the chart |
| **A-S** | Smoke: E1, E3, E5, E8 | `_GX_00_TestHost` (`EmbedEngine` false) + `_GenericExit`, defaults | E1: the wrapper manages the host's position on 1m, 5m, 15m, 60m and daily, and the `CONTROL-BARS` line maps each to 30/12/8/6/5. E3: one bar where a stop and a target are both reachable gives exactly one `exitReason`. E5: with 3 contracts and `T1ScalePct` 34, T1 closes 1 and the runner exits close the rest, each partial a separate order object, no overshoot. E8: IOG off on both signals (`iogEnabled` False logged). Planned T1 on a chart: record `t1PlanSource` (`plan` if the chart shares the plan keys, else `fill` with the Info line) |

A failed check is fixed in the GX-1 to GX-3 code, not by changing the check; the check's set-up or pass rule changes only with Ettienne's approval.

### 8.5 Legacy exit studies (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q8: mark the legacy exit studies superseded once GX exists"*)
In the GX-3 PR, `_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit` and `_ExitHalfRiskHalfRoll` get the header note `// SUPERSEDED by GX (_ExitEngine / _GenericExit, specs/generic-exit-manager-spec.md); kept for reproducibility, not deleted`, and `docs/engines-manual.md` lists them as superseded. No code in them changes, none is deleted, and they stay in `PLStudies.csproj`.

### 8.6 GX-5: batch-1 migration (**Owner-approved 2026-11-11 18:18–18:21 ET**, *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"*)
- Starts after GX-1 to GX-3 are merged and every §8.4 check passes.
- **One strategy at a time, one PR each.** The strategy calls `exitPlan` / `exitBar` (embedded mode, stop sent with the entry) instead of its hand-coded PR #117 §7 exits, and declares as Inputs only what its own spec varies.
- **Equivalence re-check per strategy** (Analyst, MiniPC, ≤ 2024 data): the engine version matches the hand-coded version trade-for-trade, long and short: same entries, exit bars, exit prices, exit reasons and R. Any difference is explained and fixed before the hand-coded path is retired. A strategy that does not match stays hand-coded until it does. Results are recorded without prices (counts, net $ and R only).
- Order of migration (**Analyst decision (2026-11-12), owner may override.**): the batch-1 strategies in the order of the PR #117 overview list, simplest exits first where the list allows.

### 8.7 Repo conventions (apply to every GX PR)
- Real strategies and the wrapper: one native `[Input]` per parameter (header §17 block each); multi-value inputs are **typed C# enums**; bools for on/off; ints for counts, minutes and clock times (0 = off). The only int stand-ins for enum choices are the §4.13 optimiser companions.
- Feature branches only; never push to main; Ettienne merges.
- **`PLStudies.csproj`** (**Owner-approved 2026-11-12 07:31 ET**, *"You can add the _GenericExit to the csproj file."*; and 07:33 ET, quoted at the top). GX adds exactly one hand-written line, `<Compile Include="_GenericExit.Strategy.CS" />`, in GX-1. Never commit an automatic (PowerLanguage Editor generated) csproj change; before each commit, `git diff -- PLStudies.csproj` shows only that line (GX-1) or nothing (later PRs). Test and development strategies get no committed line; a native test uses the temporary selective-test line, removed before commit.
- **Locations** (owner 07:33 ET): `_GenericExit.Strategy.CS`, the concrete wrapper with the engine inside, is in the studies folder, which is the repo root (`Techniques\CS\_GenericExit.Strategy.CS` on the MiniPC). Test and development strategies such as `_GX_00_TestHost` are in `sandbox/`, named `_<ID>_<NN>_<Name>.Strategy.CS` with one public class equal to the stem, and are not in the csproj. This replaces rev 1.0, which put `_ExitEngine.Function.CS` and the test host at the repo root with their own lines.
- Off-MC tests follow `tests/PortMMTests` (net8.0, C# 7.3, linked source, `run.sh`).
- No prices in the repo, no data on or after 2025-01-01, no committed raw logs.
- IOG off; Bar Magnifier off for any result compared with PT.

### 8.8 Log lines Coder implements (index)
| Line | § |
|---|---|
| `<ID> Info: GX START pkg(...) seed(...) ... <Name>(..) <Name>Idx(..) <Name>Eff(..)` | 4.13 |
| `<ID> Error: GX INITIAL-STOP invalid(<mode> <input>=<value>) using AtrMultiple` | 4.1 |
| `<ID> Error: GX IDX-RANGE <Name>Idx(<v>) valid(-1..<N-1>) using <Name>(<value>)` | 4.13 |
| `EXIT Info: GX FILLBAR-PROTECTION ...` / `EXIT Warning: GX FILLBAR-UNPROTECTED ...` / `EXIT Info: GX FILLBAR-STOP ...` | 4.12 |
| `<ID> Info: T1-SKIPPED ...` | 4.2 |
| `EXIT Info: GX T1-PLAN source(fill) - entry did not publish pm.gxPlanEntry/pm.gxPlanStop` | 4.2 |
| `AfterT1` → `AtTrigger` fallback (BE, trail start), Info once per run | 4.3, 4.4 |
| `<ID> Warning: GX SESSION FlatBy ignored on <resolution> bars: HoldOvernight` | 4.6 |
| `<ID> Info: CONTROL-BARS ...` / `<ID> Warning: CONTROL-BARS no listed timeframe ...` | 4.9 |
| `EXIT Error: _GenericExit must follow the entry signal in the Portfolio Trader signal list` | 5.1 |
| `EXIT Info: R fill <id> planned $<armR> actual $<fillR>` | 5.1 |
| `EXIT Warning: GX RISK-OVER-BUDGET ...` (`PORT` prefix in PT) | 5.2 |
| `EXIT CLOSE ...` / the PR #117 `<ID> EXIT ...` line with the §5.3 fields | 5.3 |

Regression: G0, D9a and the existing suites unchanged, because GX is inert unless a strategy calls it.

## 9. Owner answers and Analyst decisions

**Resolved, owner-approved 2026-11-11 09:16 ET** (*"1. Yes, this approach is fine. 2. Yes, they are good - I assume we'd be able to set the first target and break-even profit percentage (or amount). 3. No, we can make the standalone strategy work in portfolio trader as well for now. 4. Keep the position open and log the warning."*):
- **Q1:** option C approved (shared engine plus one thin `_GenericExit` wrapper).
- **Q2:** baseline values approved. The first target (unit, value, scale-out %) and break-even (unit, value, offset ticks) are Inputs with `ETriggerUnit` (§4.2, §4.3).
- **Q3:** the wrapper works in Portfolio Trader now (§5.1). E4 passed on the scratch studies (2026-11-11, §7.1); acceptance check A-E4 on the implementation build (§8.4).
- **Q7:** an over-budget wrapper trade stays open, is managed normally, and logs one Warning per trade (§5.2).

**Q10 resolved, owner-approved 2026-11-11 09:33 ET (Ettienne, Q10: *"1. Yes.  2. Only the strategy's own equity.   3. Yes, report them separately."*)** (§5.3):
- (1) `actualRiskPct` uses the whole-trade worst point, including realised scale-out P&L.
- (2) The base is the strategy's own equity only, on charts and in Portfolio Trader; the book and broker bases are removed.
- (3) 1.00 = exceeded plan, and > 1.10 is reported separately.

**Resolved, owner-approved 2026-11-11 18:18–18:21 ET:**
- ~~**Q4** Should every entry-only idea also run a control exit (`FixedBars` 10 and/or `RandomBars`) and report CS-8 forward MFE/MAE, as the three-way entry test in §4.9? PROPOSED: yes, `FixedBars` 10.~~ *"Q4 yes, but set ControlExitBars per timeframe"* → §4.9, §6: every entry-only idea runs a `FixedBars` control arm with N per timeframe and reports CS-8 forward MFE/MAE.
- ~~**Q5** What value should `FixedBars` / `ControlExitBars` take per timeframe (10 bars on 5m ≈ 50 minutes)?~~ *"Q5: 1m 30, 5m 12, 15m 8, 60m 6, daily 5"* → §4.9: five int Inputs `ControlExitBarsM1/M5/M15/H1/D1` = 30/12/8/6/5. Other timeframes: nearest listed at or below (**Owner-approved 2026-11-11 18:54 ET**, Ettienne: *"Yes to both the nearest-timeframe rule and the AtTrigger rename"*).
- ~~**Q6** Should the wrapper's default stop be `AtrMultiple`, or `SignalBarExtreme` (as `_ExitHalfRiskHalfRoll`)?~~ *"Q6: AtrMultiple default, SignalBarExtreme selectable"* → §4.1, §6.
- ~~**Q8** Keep the legacy exit studies (`_StopsAndTargets`, `_Stops_And_Targets`, `_ExitStatic`, `_StpOrLim_Exit`, `_ExitHalfRiskHalfRoll`) unchanged (PROPOSED), or mark them superseded once GX exists? Nothing is deleted by this spec.~~ *"Q8: mark the legacy exit studies superseded once GX exists"* → §1.1, §8.5 (GX-3): header note and docs entry; nothing deleted.
- ~~**Q9** Should batch-1 specs later move onto the engine (GX-5), or stay hand-coded?~~ *"Q9: move batch-1 onto the engine once it passes, with a re-check per strategy"* → §8.6 (GX-5): after the E-checks pass, one strategy at a time, each with an equivalence re-check.
- **Final-target and trail-start units:** *"Yes, the final target and trail start both get ETriggerUnit"* → §4.2 `FinalTargetUnit` / `FinalTargetValue`, §4.4 `TrailStartUnit` / `TrailStartValue` (`ETriggerUnit`, default `RMultiple`; defaults keep rev 0.6 behaviour; shorts mirror explicitly).

**Resolved, owner-approved 2026-11-11 18:54 ET** (*"Yes to both the nearest-timeframe rule and the AtTrigger rename"*):
- The rule for timeframes not in the `ControlExitBars` list (nearest listed timeframe at or below; sub-minute and non-time bars use the 1m value with a Warning, §4.9).
- The `ETrailStart.AtR` → `AtTrigger` rename (§4.4).

**Approved for implementation, owner 2026-11-12 07:13 ET** (*"Alright, you can recheck the tests left and please complete the spec so @Coder can start implementing strategies from it"*):
- The spec moves from PROPOSAL to READY FOR IMPLEMENTATION; Coder starts GX-1 (§8.1).
- The E-checks still to do are run on the implementation build as acceptance checks A-E2, A-E4, A-E7 and A-S (§8.4).

**Resolved, owner 2026-11-12 07:31 ET** (*"Yes real studies can get the numeric inputs. You can add the _GenericExit to the csproj file. Yes you can measure it from the T1 planned"*):
- The `<Name>Idx` optimiser companions are approved on real studies: `_GenericExit` and every strategy that hosts the engine (§4.13).
- `_GenericExit.Strategy.CS` gets one hand-written `<Compile Include>` line in `PLStudies.csproj` (§8.7).
- T1 is planned from the planned entry price, and T1-SKIPPED compares the actual fill with the planned T1 (§4.2).

**Resolved, owner 2026-11-12 07:33 ET** (*"Also _GenericExit can go under the Studies/CS folder since it is already a concrete strategy to use. The other strategies we develop for further testing will go under the sandbox folder and will not directly be included in the csproj."*):
- `_GenericExit` is in the studies folder (the repo root, `Techniques\CS` on the MiniPC) with its csproj line; test and development strategies, including `_GX_00_TestHost`, are in `sandbox/` with no csproj line (§8.1, §8.7).

**Resolved, owner 2026-11-12 15:28 ET** (*"Change this so that open positions at 15:55 are flagged and closed. New positions opened after that window is kept open till the next day at 15:55 - I think that would be the semantic reasoning behind the close by rule."*):
- Supersedes the 10:23 ET literal reading of §4.6 (any bar at or after `FlatByHHMM`, the CS-7 `mustFlatten` clock, no 17:00 cap).
- A position's flat-by deadline is the first `FlatByHHMM` strictly after its original entry. `FLATBY` fires on the first bar at or after that deadline. An RTH entry at 10:00 closes at 15:55 the same day. An entry at 16:00 or 18:20 is held overnight and closes at the next day's 15:55. Adds keep the original entry. Daily and longer bars stay `HoldOvernight` with the existing Warning and do not return `FLATBY` (§4.6).

**Analyst decisions (2026-11-12), owner may override.** Each is marked where it sits; Coder implements them as written unless Ettienne changes them:
1. **Fill-bar protection (§4.12):** embedded mode sends the initial stop with the entry order (E2(c)) and is the required path for production strategies; the wrapper gets `FillBarDisasterStop` (default true): `GenerateStopLoss` every flat bar, $ per contract from the planned distance, with the explicit wrapper stop taking over from the bar after the fill; the `FILLBAR-*` log lines and the `fillBarStops` count.
2. **Optimiser companions (§4.13):** which 8 enum Inputs get a `<Name>Idx` (not the 6 structural ones), −1 = use the enum, 0…N−1 by declared member order, out of range ⇒ Error and the enum; the START line prints Input, Idx and effective value; `gxPackage` hashes effective values. Numeric companions on real studies as such are owner-approved (07:31 ET).
3. **Catalogue details (§4):** the enum member lists, Input names and the non-default mode rules without an owner quote; members only ever appended.
4. **Invalid stop settings (§4.1):** Error and fall back to `AtrMultiple`; `StopTicks` and `StopPct` default 0; `EntryProvided` is invalid in the wrapper.
5. **Trail start fallback (§4.4):** `TrailStart = AfterT1` with no T1 order falls back to `AtTrigger`, as break-even does.
6. **`FlatBy` on daily or longer bars (§4.6):** treated as `HoldOvernight`, with a Warning.
7. **`RandomBars` seed (§4.9):** the CS-8 `ControlSeed` (12345), journalled.
8. **Non-baseline defaults (§8.2):** `StopSwingBars` 5, `TrailBars` 2, `TrailEmaLength` 20, `MaxHoldDays` 0.
9. **§5.3 definition details** that carry no owner quote, and the column name: the whole-trade MAE is always the new column `maeTradeDollars`; the existing `maeDollars` (MC `MaxDrawDown`) is unchanged.
10. **PT fallback (§5.1 item 6):** if a strategy's signals ever stop sharing one `PortfolioData`, the entry hosts the engine for PT runs.
11. **Split-wrapper fallback (§3):** built only if Ettienne asks for it after using the single dialog.
12. **Test host `_GX_00_TestHost` (§8.1)** and the acceptance-check set-ups (§8.4).
13. **Engine location (§8.1):** the shared engine lives inside `_GenericExit.Strategy.CS` (pure part in `namespace PowerLanguage.Function`, the MC wrapper class behind `#if !GX_OFFMC`), so the only csproj line is the owner-approved `_GenericExit` one; fallback a separate `_ExitEngine.Function.CS`, which would need Ettienne's approval for a second line. The file locations themselves are owner-approved (07:33 ET).
14. **GX-5 migration order (§8.6).**
15. **Shared enums are top-level** in the engine part of `_GenericExit.Strategy.CS`, not nested per strategy (§8.1).
16. **Final target planned like T1 (§4.2):** from the planned entry and planned R.
17. **Wrapper planned entry (§4.2, §5.1):** a GX-aware entry publishes `pm.gxPlanEntry` / `pm.gxPlanStop` at the arm; the wrapper uses them when valid and otherwise falls back to the fill (and its own initial stop), with `t1PlanSource` journalled and one Info line per run.

**Remaining, not blockers to starting:**
- The §8.4 acceptance checks, run by Analyst once GX-3 is built.
- Cleanup of the scratch `_TestGx*` studies, workspaces and logs awaits Ettienne's approval. Leave the Check7 files alone.
- No owner question is open. The 07:31 and 07:33 ET answers closed the three rev 1.0 questions (Idx on real studies, the csproj line, the planned T1). The 15:28 ET answer closed the flat-by clock (§4.6).

## 10. Revision history
- **1.1 amendment (2026-11-12 15:28 ET).** Owner: *"Change this so that open positions at 15:55 are flagged and closed. New positions opened after that window is kept open till the next day at 15:55 - I think that would be the semantic reasoning behind the close by rule."* §4.6: the flat-by deadline is the first `FlatByHHMM` strictly after the original entry; the first bar at or after that deadline is `FLATBY`. This supersedes the 10:23 ET reading (any bar at or after `FlatByHHMM`). Daily bars are unchanged (`HoldOvernight` plus the Warning, no `FLATBY` inside the bar).
- **1.1 (2026-11-12 ET).** Owner answers 07:31 ET (*"Yes real studies can get the numeric inputs. You can add the _GenericExit to the csproj file. Yes you can measure it from the T1 planned"*) and placement rule 07:33 ET (*"Also _GenericExit can go under the Studies/CS folder since it is already a concrete strategy to use. The other strategies we develop for further testing will go under the sandbox folder and will not directly be included in the csproj."*), quoted at the top and in §9. §4.13: Idx companions owner-approved on real studies; the one-config-per-member fallback removed. §4.2: T1 and the final target planned from the planned entry and planned R; T1-SKIPPED compares the actual fill with the planned T1; planned entry passed through `exitPlan` (embedded) or the new `pm.gxPlanEntry` / `pm.gxPlanStop` keys with a fill fallback (wrapper, §5.1). §8.1 / §8.7: `_GenericExit.Strategy.CS` in the studies folder (repo root) with the one owner-approved csproj line, the engine inside that file behind a `GX_OFFMC` guard for the off-MC tests (no `_ExitEngine.Function.CS`), the test host renamed `sandbox/_GX_00_TestHost.Strategy.CS` with no csproj line; the rev 1.0 "wrapper and host at the repo root, one line per new file" note replaced. Unit tests, acceptance checks and the log index updated to match.
- **1.0 (2026-11-12 ET), READY FOR IMPLEMENTATION.** Owner instruction 07:13 ET quoted in the status, the top of the spec, §8 and §9. E2 design closed: §4.12 fill-bar protection (embedded stop with the entry; wrapper `FillBarDisasterStop`, default true). E7 design closed: §4.13 `<Name>Idx` optimiser companions. §8 rewritten as the Coder hand-off: GX-1 to GX-5 with files, the consolidated 48-Input table, unit tests, native acceptance checks A-E2 / A-E4 / A-E7 / A-S, legacy studies, GX-5 migration, repo conventions and a log-line index. Every "PROPOSED" leftover resolved as owner-approved (quoted) or an Analyst decision (§9). Fixes: the wrapper Input count (§3 said about 28, §4.11 about 37; now 48 everywhere); `_GenericExit` moved from `sandbox/` to the repo root because the sandbox naming lint does not fit it; the `maeDollars` / `maeTradeDollars` naming choice closed; the embedded stop is re-sent at the planned level, never recomputed from the fill (PR #117 §7.1); owner questions listed in §9 (Idx sign-off under PR #117 §10, csproj lines, the T1-SKIPPED trigger); `TrailStart = AfterT1` without T1, invalid stop settings, `FlatBy` on daily bars and the `RandomBars` seed specified.
- **0.9 (2026-11-11 ET):** MiniPC E1–E8 native results recorded on the scratch `_TestGx*` studies.
- **0.8 (2026-11-11 ET):** owner approval 18:54 ET of the other-timeframe `ControlExitBars` rule and the `ETrailStart.AtTrigger` rename.
- **0.7 (2026-11-11 ET):** owner answers 18:18–18:21 ET (Q4, Q5, Q6, Q8, Q9, final-target / trail-start units).
- **0.2 to 0.6 (2026-11-11 ET):** owner answers 09:16, 09:19, 09:33, 09:34 and 09:52 ET (see the front matter).
