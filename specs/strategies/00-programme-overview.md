# Strategy specs, batch 1: programme overview and shared rules

**Rev 1.8 (draft), Wednesday 11 Nov 2026, America/New_York (EST).** Rev 1.8: Ettienne's short-side answers (Ettienne 2026-11-11 09:52 ET: *"1. Yes, flag it and don't trade it.  2. Yes, and big-winner rule applies to it.  3. Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades. 4. Get their own values.  5. Yes, trail the whole position, log an info message.  6. Make it an input setting using an enum  7. Yes keep it the same - we might have to change it later on for the short side so make a note of that."*): a losing side is flagged and not traded (§6 item 12, §7.9 item 8); AV-01 short approved, `Side` default `Both`, big-winner rule on both sides; own-mirror shorts accepted pending test confirmation (§7.9 item 11); separate Long/Short numeric inputs (§7.9 item 12); T1 already passed at the fill ⇒ skip T1, trail everything, Info line (§7.2a); AV-02 `TradesPerDay` enum; YV-12 Day-2 filter side-blind for now. Rev 1.7: short-side review (Ettienne 2026-11-11 09:36 ET: *"Please review the strategies thoroughly and make sure that where shorts are involved, we have clarity around handling the short scenario's."*). New §7.9 *Short-side conventions*; §7.1 short order names; §7.3 and §7.7 written for both sides; every batch-1 spec gets an explicit short table (§5a). No owner-approved rule changes meaning. Rev 1.6: R re-based on each add fill (§7.7), owner-approved 22:35 ET, consistent with PR #118 §3a. Rev 1.5: Ettienne's answers of 22:06 ET. Adds are allowed where they make sense, via clear inputs (§7.7); add inputs on VW-01, AV-02 and AV-01. The 09:30–09:45 block's source and the 09:31 variant are in §7.6a. Rev 1.4: Ettienne approved the rev 1.3 catalogue changes at 20:31 ET (*"These changes are fine, just document any variations (if there are true variation) and make it so that we can invoke them from the Input parameters."*). Every spec now lists its variants in §8.1, all selectable via Inputs (§3 rule 8), and multi-value inputs are typed enums (§3 rule 9, 20:34 ET). Rev 1.3 applies Ettienne's Kindle findings from Keller Book 1 (Ch 3.3, 4.3, 6.3), Keller Book 4 (§2.4, §8.4) and Dale *Volume Profile* (Setup #2) to PS-02, PS-01, VW-01 and OF-01 (§11). It adds MFE/MAE to the EXIT line (§8). Rev 1.2: holdout scope confirmed by Ettienne at 19:53 ET (*"18 months for OF-01 only is fine."*), and OF-01 refined from Trader Dale's book. Rev 1.1 applied Ettienne's answers of 19:44 ET (PR #117): *"1. $17 and yes, add costs for NQ, CL and 6E as well. 2. use 2 contracts, yes you can exit everything on the first target - ultimately I'd like to use dynamic position sizing based on percentage risk of the account. 3. re-scope the holdout period to be about 18 months or even 12 months. 4. yes, use the big-winner rule. 5. give a parameter for the number of minutes to exit before news (as integers in terms of minutes) and disable it when 0 or negative."* Rev 1.0: Prepared by Analyst (Grok Bot) for Coder. Requested by Ettienne, 19:12 ET: *"Yes, please write them up. Refer to the relevant books/sources if necessary to refine the specs."* Extra requirement, 19:14 ET: *"Also be cognizant of any trade management and exit rules relevant to the strategies and please create clarity around those aspects as well in the spec."*

**Status:** documentation only. No strategy is implemented, run or validated by this document. Every number below is a predeclared rule or a source figure. None is a measured result on CME data. The holdout (§5, as re-scoped in rev 1.1) has not been opened and must not be opened while building or tuning.

**Marking convention.** Anything not backed by the catalogue, a cited book or document, an owner decision or existing engine behaviour is marked **PROPOSED – owner to confirm**. Every such item is listed again in the spec's *Open questions* section and in §10 below.

## 1. What this batch is

Batch 1 follows the first six rows of the owner-approved build order on the *Strategy-planning phase* Page: PS-02 → PS-01 → VW-01 → AV-02 → AV-01 → OF-01. Rows 7–8 (PS-25 DVP rail continuation, KD-09 Fibonacci family-count module) are batch 2. They wait for the native DVP gates and for workable hosts. The G0 multi-timeframe Fibonacci/confluence spec stays parked until several signals work (owner, 8 Nov 2026).

| # | Spec | Catalogue ID | One line | Readiness |
|---|---|---|---|---|
| 1 | [01-ps-02-balance-break-close.md](01-ps-02-balance-break-close.md) | PS-02 (+ A2-41 BookSpec variant) | Market entry after a 2-bar accepted break of a balanced 3–5-session composite; half at 1.0 W, runner trailed (Keller B4 §8.4); failed-break exit (Keller B1 3.3) | Buildable; engines exist; level gate G0-d pending |
| 2 | [02-ps-01-balance-break-pullback.md](02-ps-01-balance-break-pullback.md) | PS-01 | Same break cohort as PS-02, but enter on the first confirmed higher low that holds above the old edge (Keller 3.3 retest); stop through the balance POC; half at 1 W, runner to the next HVN / naked POC | Buildable; shares PS-02's event code |
| 3 | [03-vw-01-trend-day-vwap-pullback.md](03-vw-01-trend-day-vwap-pullback.md) | VW-01 | On a classified trend day (≥ 10:30 ET), buy the first or second VWAP + 0.25 σ pullback that prints a 5m higher low (Keller B1 4.3); exit if price accepts across VWAP | Buildable; RTH VWAP must be built (see spec) |
| 4 | [04-av-02-impulse-avwap-higher-low.md](04-av-02-impulse-avwap-higher-low.md) | AV-02 (+ DV-12 touch arm, later) | After a ≥ 3 ATR impulse from a confirmed low, buy the first higher low that holds at the impulse AVWAP | Buildable; M-AVWAP + PRICEACTION exist |
| 5 | [05-av-01-swing-high-avwap-reclaim.md](05-av-01-swing-high-avwap-reclaim.md) | AV-01 | Swing sleeve: after price reclaims the AVWAP from the last significant swing high, buy the first 15m higher low; hold days to weeks | Buildable; 50-day SMA via `DailyConfig.smaLength = 50`; acceptance `big-winner-trend` (owner, 2026-11-10) |
| 6 | [06-of-01-stacked-imbalance-retest.md](06-of-01-stacked-imbalance-retest.md) | OF-01 | Per Trader Dale: first test of a ≥ 3 stacked 300% imbalance zone on 30m footprints, limit at the zone edge, target before the next HVN, 5m aggression trail; variant K (Keller 6.3) | **Data-gated**: needs verified MC-BidAsk ticks, @ES from 2024-10-28 to the §5.1 cut (holdout re-scope) |

Each spec follows the same 15 sections: purpose and edge; instruments and timeframes; engines; entry rules; **trade management and exits**; sizing, risk and M-PORT; session, news, holiday and roll handling; native inputs; logging and journal lines; test plan; acceptance criteria; implementation checklist; source-file header mapping; open questions; sources.

## 2. Sources used across the batch

Internal (repo `ettiennehugo/MultiCharts.Net`, `main` @ `47ffda2`):
- `specs/trading-idea-catalogue.md` v2.7 (entries PS-01, PS-02, A2-41, VW-01, AV-01, AV-02, DV-12, OF-01; §§2.0–2.3, 2.6, 3.10 EX family). Cited as *Cat. <ID>* or *Cat. §x*.
- `docs/research-strategy-header.md` and `sandbox/ResearchStrategyHeader.template.txt` (PR #103): header sections 1–22, §17 parameter block, acceptance policies, sandbox lifecycle.
- `docs/engines-manual.md` v4.2: §1.4 (closed-bar timing), §1.5 (journaling), §3.26 Example 26 (PS-01), §3.27 Example 27 (PS-02), §3.30 Example 38 (OF-01), §4.7 M-AVWAP, §4.8 PRICEACTION, §4.16 / §5.23 M-PORT, §5.10 F-VWAP, §5.12 F-TM, §5.13 F-JRNL / `sizeForRisk`.
- `specs/m-port-spec.md` rev 2.7 and `specs/live-check-sheet.md` (Check 7, verified 2026-11-10).
- `specs/m-instr-spec.md` rev 2.5.13 (§5.3 `InstrumentConfig`, §9 sizing and cost floor = PR-M7, PR-M8 CME product holiday calendars, PR-M9 roll-gap calibrator).
- `final-synthesis.md` (Spec 1/1A, §4.0 common definitions, §6.6 ablation ladder, §6.7 splits). Analyst workspace copy; not in the repo.
- Catalogue YV-01/02/04/07 and OF-09 data lines (@ES bid/ask from 2024-10-28); `_PortMM.Function.CS` / `_PortfolioRiskManager.Strategy.CS` (sizing modes, `EntryContracts`); `_EventCalendar.Function.CS` (`MinutesTo`, blackout semantics); `_InstrumentProfile.Function.CS` (`CostKey`, `costs`, `costSource`) — rev 1.1.
- *Strategy-planning phase* Page (8 Nov 2026): build order, testing protocol, costs, acceptance defaults, profit-concentration contract, lifecycle, recorded decisions.

Books and external sources (via the analyst's Kindle notes; page/location as in those notes):
- Keller, Book 4 (empirical volume-profile study, FX/crypto 1-minute): §2.4 "a balance break travels its width just over half the time" (FX 214/378 = 56.6%, median 1.17 W; crypto 59.1%, 1.20 W); §8.3 confirmation vs prediction excursions (median MFE/MAE 10.5 vs 0.70); §8.4 rules (trade with value migration, balance break is the only end-to-end spec, scale at one width and run the rest, skip entropy > 0.93). Notes: `kindle-notes/keller-b4-empirical.md`, `keller-b4-parts-2-7-analyst2.md`.
- Keller, Book 1: card 3.3 ("escape is not entry; accept or retest"); Part IV VWAP setup 1 (trend-day first/second VWAP touch, stop modestly beyond, target new extremes); card 6.3 (stacked imbalances). Notes: `kindle-notes/keller-b1-parts3-4.md`.

**Books checked in the owner's Kindle library (rev 1.3, findings relayed by Ettienne 2026-11-10; Kindle locations, quotes read from page images, may differ slightly):**
- Keller, *Volume Profile & Order Flow: The Complete Institutional Reference* (**Keller B1**): Ch 3.3 The Balance-Break Setup (Loc ~951–959) → PS-02, PS-01; Ch 4.3 VWAP Setup 1 and Drill 4.3 (Loc ~1307–1358) → VW-01; Ch 6.3 Stacked-Imbalance Continuation (Loc ~1896) → OF-01 variant K.
- Keller, *Empirical Volume Profile & Order Flow* (**Keller B4**): §2.4 (Loc ~721–838) and §8.4 Rules 2 and 4, daily steps 7–9 → PS-02, PS-01, and the §8 logging contract. FX/crypto data, not futures evidence.
- Trader Dale, *VOLUME PROFILE: The Insider's Guide to Trading* (**Dale VP**): Setup #2 Trend (pp.81–89) and SL/PT sizing (pp.98/100) → cross-referenced in VW-01 to catalogue PS-12/DV-04 (Example 34); no new batch-1 strategy.
- Trader Dale, *ORDER FLOW: Trading Setups* → OF-01 (rev 1.2).
- Brian Shannon, *Maximum Trading Gains with Anchored VWAP* (anchors at significant swings; buy strength after a pullback that recovers a swing-high AVWAP; stop under the most recent higher low; practitioner, grade E, Cat. §0 item 3): **not available in owner's Kindle library; AV-01 and AV-02 rules remain from catalogue/notes.**
- Trader Dale, *ORDER FLOW: Trading Setups (The Insider's Guide To Trading)*: Setup #4 Stacked Imbalances (pp.74–76), take profit (pp.124–128), trailing (pp.129–131), stop loss (p.136). Read by Ettienne in the Kindle reader on 2026-11-10 (page labels ±2); earlier analyst notes: `kindle-notes/dale-orderflow-pp13-128.md`.
- Telonics (Oct 2026) and RipBacktest (Oct 2026) VWAP null results; Telonics AVWAP bounce rates 51.14% / 52.37% (as cited in Cat. §0 items 2–3). These are the null results VW-01, AV-01 and AV-02 must beat.

No new web search was needed for batch 1. The catalogue already carries the external evidence, and the owner's attachments folder holds only the MultiCharts .NET Programming Guide and User Manual (used for API facts), plus run logs.

## 3. House rules every batch-1 strategy obeys

1. **Sandbox only.** Each file lives in `sandbox/` with the standard 22-section header, per `docs/research-strategy-header.md`. It is never added to `PLStudies.csproj` in a commit. Selective tests add one temporary `Compile Include` and remove it before commit. No automatic csproj churn is committed.
2. **Naming.** `_<ID with _>_<PascalName>.Strategy.CS`; the class is the file stem (e.g. `sandbox/_PS_02_BalanceBreakClose.Strategy.CS`, class `_PS_02_BalanceBreakClose`).
3. **Inputs.** Individual native MultiCharts `[Input]` properties (owner, pt507u). No input structs, input classes or JSON for strategy rules. Header §17 describes every input (Purpose, Default, Units, Range, Effect, Interactions, Fixed or research-tuned). `TraceLevel` stays the base input.
4. **Engines.** Engines run only when used (lazy). Engine settings go through the protected config properties (`PriceActionConfig`, `AvwapLimits`, `DailyConfig`, `EventConfig`, `InstrumentConfig`, `FlowConfig`, …) in `Create()`, never as new inputs. VP-base slots that are already `[Input]`s (e.g. `Vwap1Type`) are set in code and asserted in `StartCalc`.
5. **Typed MC members only.** Never write a MultiCharts enum or API member as a string (`EBarState.Close`, `EOrderAction.Buy`, `EPivotLabel.HL`, `ERollDetection.Table`, `EVwapType.Session`, `EAvwapAnchor.SwingLow`, …). Strategy-specific choices are C# enums nested in the class.
6. **Closed bars only.** `CalcBar` acts only on `Bars.Status == EBarState.Close`. An order sent on bar *t* works from bar *t+1* (manual §1.4). Pivots and anchors are used only from their confirmation time.
7. **No broker connection, no auto-trading, no Forward Testing** in this phase. Historical charts and Portfolio Trader backtests only.
8. **Variants (owner, 2026-11-10 20:31 ET: *"document any variations (if there are true variation) and make it so that we can invoke them from the Input parameters"*).** Every variant must be selectable via Inputs; default = primary. A variant is an alternative rule (a different stop, entry, target or filter rule), not a different value of a swept number. Swept numbers stay parameters. Each spec lists its variants in §8.1, with the input name, type, values, default, effect and source, and every variant input is in that spec's §8 Inputs table. A variant that is not built yet is listed as future and has no input. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants.
9. **Multi-value inputs are typed enums, not ints/strings (owner, 2026-11-10 20:34 ET: *"make sure that they are properly defined as enumerations and not just integers. It makes the inputs slightly easier to understand."*).** Every multi-value choice is a C# `public enum` with named members, declared in the spec and nested in the strategy class. The `[Input]` property has that enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`. The default (the primary) is set in the constructor, never by member order. No integer codes, no strings. True on/off switches are `bool`. Numeric parameters with a documented 0 = off value (`NewsExitMinutes`, `ZoneMaxAgeMinutes`, `TimeStopBars`, `CloseTopFrac`, `VolFloorPct`) are numbers, not choices, and stay numeric.
   - **Shared declarations** (identical in every batch-1 file):
     ```csharp
     public enum ESide { Both, LongOnly, ShortOnly }                                // default Both
     public enum ESingleLotMode { ExitAtT1, RunnerOnly }                           // default ExitAtT1
     public enum EControlArm { None, RandomEntry, ShiftLevels, DelayEntryBars }    // default None
     public enum EAcceptancePolicy { CatalogueDefault, BigWinnerTrend }            // default CatalogueDefault (AV-01: BigWinnerTrend)
     ```
   - **Is an enum `[Input]` supported?** The repo says yes for charts. Existing studies already declare enum-typed inputs: `_CustomSignal.TraceLevel` (`ETraceLevel`, nested in `_CustomSignal`, used by every strategy), `_BreakoutAutoBase.StopLossPlacement` (`EStopLossPlacement`), `_StopsAndTargets.OrderMode` (`ESpecOrdersMode`), `_VolumeProfileStatistics.ProfileRef` / `SideFilter` (`EVpRefMode`, `EVpSide`), and `_EhlersFilter.CoefficientStrategy`. Header §17 sets no type restriction. **Not verified:** whether an enum input can be swept by the chart Strategy Optimizer and the Portfolio Trader optimizer, and how it appears in their parameter grids. `specs/sandbox-entries-feasibility.md` also flags PT optimisation of non-numeric inputs as "verify". Coder checks this on the MiniPC (§9). If an enum cannot be swept, the sweep runs one registered config per enum member. Inputs are not silently changed to ints.

## 4. Costs, fills and the cost floor

| Root | Round trip used in research | Status | Cost-floor ticks (cost ≤ 0.10 R) |
|---|---|---|---|
| ES | $17.00 = cost key `Future:ES` 1700 cents (already the `InstrumentConfig.costs` default) | Owner-confirmed 2026-11-01; convention confirmed 2026-11-10: $17.00 is the whole round trip (commission + one total tick), not $29.50 | 14 |
| MES | $2.50 (`Future:MES` 250) | Owner-confirmed 2026-11-01 | 20 |
| NQ | $9.48 = `Future:NQ` 948 (to add, §9) | Provisional figure, accepted by the owner 2026-11-10 | 19 |
| MNQ | $1.72 | Provisional; not added (the owner named NQ, CL and 6E) | 35 |
| CL | $14.72 = `Future:CL` 1472 (to add, §9) | Confirmed | 15 |
| 6E | $11.17 = `Future:6E` 1117 (to add, §9) | Provisional figure, accepted by the owner 2026-11-10 | 18 (= ⌈1117 × 10 / 625⌉, M-INSTR §9 formula) |
| GC / ZN | $15.02 / $18.945 | Provisional; not in batch 1 markets except as later replications | 16 / 13 |

- The non-ES totals already include one tick of slippage (Page). Do not add that tick twice.
- **Cost floor (Cat. §2.1, decided 2026-10-23):** stop distance = max(structural stop, cost floor). The trade is skipped and logged only if the widened stop breaches the per-trade risk budget (1% of equity). Size is never shrunk to fit a wider stop. In code this is M7: set `InstrumentConfig.applyCostFloor = true` in `Create()` for the primary arm and pass the widened stop to `sizeForRisk(…, widenedStop)` / `portfolioArm(…, widenedStop)`. NQ, CL and 6E get cost rows in `InstrumentConfig.costs` (owner, 2026-11-10; Coder checklist §9 item 1). Until those rows exist, M7 refuses on those roots with `unknown-field:cost`.
- **MultiCharts costs:** every round trip above already contains one tick of slippage in total. In MC Strategy Properties, set Commission per contract per side = (round trip − one tick) / 2 and Slippage per contract per side = half a tick. ES: commission $2.25 per side, slippage $6.25 per side ($4.50 + $12.50 = $17.00). NQ: $2.24 + $2.50; CL: $2.36 + $5.00; 6E: $2.46 + $3.125. This is arithmetic on the confirmed convention, not a new figure. The offline evaluator uses the same totals.
- **Fills in backtest:** Bar Magnifier on, at 1-minute detail on 5m/15m charts (W-M), tick detail only inside the verified tick window. Label every result with the detail used. Limits fill only on trade-through (Cat. §2.1). Where a bar touches both stop and target and the magnifier cannot resolve the order, evaluation assumes **stop first**: the offline trade-list check re-scores those trades as stop-outs. Report the count.
- **Stresses (Page proposal):** +1 adverse tick per executed side, and separately 2× total cost, on the same trade list. Catalogue §2.1 also requires 2 ticks at the open and around news, plus a 1% tail of 4 R losses. These are offline post-processing on the journal unless MC can model them natively. Say which in the run sheet.

## 5. Test windows (frozen before any performance read)

### 5.1 Holdout definition (owner-approved)
Owner decisions:
- 2026-11-10 19:44 ET: *"re-scope the holdout period to be about 18 months or even 12 months."*
- 2026-11-10 19:53 ET: *"18 months for OF-01 only is fine."*

| Setting | Value (owner-approved 2026-11-10) |
|---|---|
| `HoldoutScope` (run-sheet setting of type `public enum EHoldoutScope { OrderFlowOnly, AllStrategies }`, default `OrderFlowOnly`) | `OrderFlowOnly`: the rolling cut applies to OF-01 (the only order-flow strategy in batch 1). All other strategies keep the fixed cut **2025-01-01**: tests use data up to 31 Dec 2024 only. |
| `HoldoutMonths` | 18, applied to OF-01 |

- **OF-01 rolling cut** = the first day of the month containing (run date − 18 months). As of 2026-11-10 the cut is **2025-05-01**, so OF-01's last usable day is **2025-04-30**.
- **Fixed per run.** Every run sheet and every strategy's `CFG` journal line record `holdoutCut(<yyyy-mm-dd>)` and `holdoutMonths(<n>)` (`n/a` for fixed-cut strategies). The chart and Portfolio Trader *To* date must be strictly before the cut. A test series (development → validation of one strategy) keeps the cut it started with, even if later runs happen after the window has rolled. A new cut means a new, logged test series.
- These are protocol settings for the run sheet and the offline evaluator, not MultiCharts inputs.
- **Never** view, screenshot or report prices or results on or after the applicable cut. The holdout is run once, later, under a separate owner sign-off.

### 5.2 Windows
- **W-M (1-minute data, all non-order-flow strategies):** development = first verified full month in Jun/Jul 2016 → 31 Dec 2022, with anchored yearly walk-forward inside it (train 2016–2018 → test 2019; train → 2019, test 2020; → 2021; → 2022). Validation = 1 Jan 2023 → 31 Dec 2024. Holdout = 2025-01-01 onward.
- **W-D (daily):** development 2000–2015, validation 2016–2024 (AV-01's daily filter only).
- **W-T (ticks, order-flow strategies):** from the first verified **bid/ask** session to the day before the §5.1 cut. The catalogue says ticks start about Jul 2024, but its YV/OF-09 data notes record **@ES bid/ask only from 2024-10-28**. So the OF-01 span is about 28 Oct 2024 → 30 Apr 2025 (about 6 months, about 125 sessions). The split is in the OF-01 spec.
- **Coverage caveat (Page):** @ES minute history is reported to start 26 Dec 2022 on the MiniPC. The older download is unverified. Until 2016–2022 is present and flushed in QuoteManager, runs are **engineering smoke tests**, not development or acceptance evidence.
- **Trial budget (owner-approved, 8 Nov 2026, 5:56 PM EST):** test the unchanged catalogue host first. Then add only pre-registered ablations, one component at a time. Use at most the catalogue's registered sweep per spec, recorded in a shared trial ledger. Proposal on the Page: at most three research-tuned numeric parameters per new spec. Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, α = 0.05, fixed.

## 6. Acceptance criteria (shared defaults)

Applied separately to validation (§5.2) now, and to the holdout later. Owner-approved items are marked; the rest are Page proposals the owner accepted as defaults ("Defaults accepted; not achieved results").

1. **Catalogue kill text** for the ID, plus Cat. §2.3 standard kill (Gate 3, ≥ 150 development events; 30–149 inconclusive; < 30 no evaluation). Where both speak, the stricter applies.
2. **Net edge:** net PF ≥ 1.20 and positive expectancy after costs.
3. **Profit concentration (owner-approved, 8 Nov 2026):** largest winning trade ≤ `MaxSingleTradeNetProfitPct` (default 10%) of total net profit after costs; *and* net profit stays positive after removing the top `OutlierRemovalPct` (default 1%) of winning trades. Report original and trimmed expectancy. K = max(1, ⌈1% × number of winning trades⌉) — **PROPOSED – owner to confirm** this rounding before any run (the header standard requires it frozen). Trade unit = one parent position including all partial exits.
4. **Big-winner exception (owner, 8 Nov 2026):** only a file whose header predeclares `big-winner-trend` *before* any result skips the 10% cap. It is judged on cost-adjusted expectancy (trader's equation) and must still stay positive after removing the top 1% of winners. **AV-01 uses `big-winner-trend`** (owner, 2026-11-10: "yes, use the big-winner rule"); it is predeclared in its header before any result. The other five batch-1 specs use `catalogue-default`.
5. **Samples:** ≥ 100 validation trades and ≥ 50 holdout trades per strategy/instrument. Fewer = inconclusive, never relaxed.
6. **Drawdown:** closed-trade DD ≤ 20 R per strategy; open-equity DD reported.
7. **Across time:** ≥ 70% of evaluable walk-forward folds net positive; no single year > 50% of net profit.
8. **Across markets:** same-sign net expectancy on two eligible instruments without retuning. ES and NQ count as correlated replication, not diversification.
9. **Parameter stability:** ≥ 70% of the predeclared neighbouring configurations net positive in development.
10. **Cost robustness:** net PF ≥ 1.00 and positive expectancy under +1 tick per side, and under 2× costs.
11. **Controls:** beat each matched control named in the spec (Cat. §2.3) with a confidence interval on the lift that excludes 0.
12. **Per-side reporting and the losing-side rule** (**Owner-approved 2026-11-11 09:52 ET**, answer 1: *"Yes, flag it and don't trade it."*): every criterion above is reported for long, short and combined. If the combined strategy passes but one side has net expectancy ≤ 0 after costs, or fails any side-level criterion (items 2, 3, 6, 7, 10, 11 computed on that side's trades; item 5 samples per side), that side is **flagged and not traded**: the registered config for paper incubation sets `Side` to the passing side (`LongOnly` / `ShortOnly`), and the verdict line records `side-flagged(<long|short>) reason(<criterion>)`. The combined verdict is then re-stated on the passing side's trades alone. A side with too few trades for item 5 is "inconclusive" and is also not traded. Re-admitting a flagged side is a new registered trial.

Passing qualifies a strategy for **monitored paper incubation only**: intraday ≥ 3 months and ≥ 100 trades; swing ≥ 6 months and ≥ 30 trades (Page suggestion). Live capital is a separate owner decision.

## 7. Shared trade-management and exit framework

Each spec has its own dedicated **Trade management and exits** section with the exact numbers for that strategy. This section fixes the mechanics they all share, so the specs cannot drift.

### 7.1 Order set (both sides)
| Purpose | MC order (typed) | Name pattern | Notes |
|---|---|---|---|
| Entry | `OrderCreator.Stop` / `MarketNextBar` / `Limit` with `Contracts.UserSpecified`, `EOrderAction.Buy` | `<ID>LE` (e.g. `PS02LE`) | Size = coded qty, or the MM's `EntryContracts` in Portfolio Trader |
| Protective stop | `OrderCreator.Stop`, `EOrderAction.Sell`, `OrderExit.FromAll` | `<ID>LS` | **Re-sent on every closed bar while in a position.** Never moved away from the trade |
| First target (partial) | `OrderCreator.Limit`, `Contracts.UserSpecified`, `EOrderAction.Sell` | `<ID>LT1` | Quantity = T1 qty (§7.2) |
| Final target | `OrderCreator.Limit`, `OrderExit.FromAll` | `<ID>LT` | Used only where the spec has a final fixed target |
| Close-based exit | `OrderCreator.MarketNextBar`, `OrderExit.FromAll` | `<ID>LX` | Invalidation, time stop, FlatBy, event, roll |

**Short side (explicit).** Same orders with the opposite actions and an `S` in the name:
| Purpose | MC order (typed) | Name pattern | Notes |
|---|---|---|---|
| Entry | `OrderCreator.Stop` (sell-stop, **below** the market) / `MarketNextBar` / `Limit` (sell limit, **above** the market), `Contracts.UserSpecified`, `EOrderAction.SellShort` | `<ID>SE` | Size as the long |
| Add leg | as the entry | `<ID>SA1`, `<ID>SA2`, … | §7.7 |
| Protective stop | `OrderCreator.Stop`, `EOrderAction.BuyToCover`, `OrderExit.FromAll`, **above** the entry | `<ID>SS` | Re-sent every closed bar; only ever lowered (§7.3) |
| First target (partial) | `OrderCreator.Limit`, `Contracts.UserSpecified`, `EOrderAction.BuyToCover`, **below** the entry | `<ID>ST1` | Quantity = T1 qty (§7.2) |
| Final target | `OrderCreator.Limit`, `EOrderAction.BuyToCover`, `OrderExit.FromAll` | `<ID>ST` | |
| Close-based exit | `OrderCreator.MarketNextBar`, `EOrderAction.BuyToCover`, `OrderExit.FromAll` | `<ID>SX` | Invalidation, time stop, FlatBy, event, roll |

### 7.2 Contract split and the 1-contract case
- Partial scale-outs use the **actual open position** (`StrategyInfo.MarketPosition`, absolute value), not the coded size. The MM may have changed the size (§7.5).
- q ≥ 2: T1 qty = ⌊q / 2⌋; runner = q − ⌊q / 2⌋ (the runner gets the odd contract). With the fixed 2-contract test size this is 1 + 1. The odd-lot case only arises when the MM or a future %-risk size (§7.8) gives an odd q. **PROPOSED – owner to confirm** (the catalogue says "50%" without an odd-lot rule).
- q = 1: the spec's `SingleLotMode` input decides, default `ExitAtT1` = the whole position exits at T1, matching the Page's note that one lot exits entirely at +1 R in `_ExitHalfRiskHalfRoll`. The alternative `RunnerOnly` skips T1 and manages the single lot as the runner. Changing it is a new trial. Default accepted by the owner (2026-11-10: "yes you can exit everything on the first target").
- Test size is **2 contracts** on charts (owner, 2026-11-10: "use 2 contracts"), full-size ES/NQ (CL/6E later), so the scale-out rule is exercised. The Page notes that a genuine half-exit study needs ≥ 2 lots.

### 7.2a T1 already passed at the entry fill (all specs)
**Owner-approved 2026-11-11 09:52 ET** (answer 5: *"Yes, trail the whole position, log an info message."*). If the entry fills at or beyond T1 (long: fill ≥ T1; short: fill ≤ T1; e.g. a gap or a fast bar through the target), T1 is **not sent**. The whole position becomes the runner: the spec's trail is active from the fill as if T1 had filled (Trail-mode activation rules do not apply), the initial stop stays until the trail tightens it, and any final target / destination limit beyond T1 still applies. One Info line: `<ID> Info: T1-SKIPPED side(<±1>) fill(<px>) t1(<px>) past(<ticks>) qty(<q>) action(trail-all)`. The journal flags the trade `t1Skipped = 1`, and reports count these trades per side. Applies to every batch-1 spec and candidate with a T1; the same rule is proposed for the GX exit engine (PR #119 §4.2).

### 7.3 Stops are one-way
A stop update (break-even, trail) is computed on the bar close. It is sent from the next bar and accepted only if it tightens: long `newStop = max(oldStop, candidate)`; short `newStop = min(oldStop, candidate)` (a short stop only moves **down**). A trail candidate is also capped one tick below the last close for a long, and one tick **above** the last close for a short, so it cannot be placed through the market. If the candidate would be at or through the market, the strategy sends the market exit instead and logs `TRAIL-THROUGH`.

### 7.4 Same-bar precedence
When several exit conditions fire on the same bar, the order below decides which exit happens and which reason is journaled. Items 1–3 are intrabar fills; items 4–9 are evaluated at the bar close and executed as one `MarketNextBar` exit with the highest-ranked reason.
1. **MM `ForceClosePosition`** (daily-loss flatten or kill). Fills at the **close of the same bar T** (Check 7). The strategy does not fight it: on the next bar it sees itself flat and logs `FLAT-BY-MM`.
2. **Protective stop** (resting from the previous bar), intrabar.
3. **Target limit**, intrabar. If 2 and 3 are both touched in one bar and the magnifier cannot order them, evaluation scores it as stop first (§4).
4. **News exit** (`NewsExitMinutes` > 0 and a tier-1 release is within reach, §7.6). Reason `NEWS`.
5. Session-end flatten (`FlatBy` / early-close flatten via `mustFlatten()`).
6. Roll flatten (swing positions before a splice).
7. Invalidation (acceptance failure, VWAP loss, zone violation, OF-01 flow-warning exit `FLOW-WARN`).
8. Time stop / max hold.
9. Stagnation exit (only if a spec enables it).
After items 4–9, management updates (BE, trail, T1 re-send) are computed for the next bar only if the position is still open and no market exit was sent.

### 7.5 Interaction with the portfolio money manager (M-PORT, Portfolio Trader only)
- The strategy arms every pending entry through `sizeForRisk(Bars, entry, structuralStop, riskFraction, limits, widenedStop)` (which calls `portfolioArm` on Data1 in PT), on every bar the entry is pending, after `base.CalcBar()`. While in a position it calls `portfolioStop(currentStop)` whenever the stop moves (BE or trail).
- **The MM may:** deny an entry (heat, cluster, total cap, per-trade budget, max positions, drawdown stage, daily stop, session/weekly throttle, kill, stale); set the entry size (`EntryContracts` > 0; RiskParity sizing by default); leave coded size (`EntryContracts = −1`); block all entries (`EntryContracts = 0`); force-close (`ForceClosePosition`, fills at bar T close); pause a flat strategy (`Paused = true`, which blocks **all** fills, so the MM only pauses once flat).
- **The MM never:** resizes an open position, moves a stop or target, or sets `AllowExits = false`. Protective exits always remain the strategy's job.
- ES and NQ share cluster `EQIDX`, so the cluster cap treats them as one correlated position (Cat. §2.1 "ES and NQ count as one").
- On a chart (no PT) `sizeForRisk` skips when the widened risk exceeds 1% of equity, and `portfolioArm` is a no-op.
- PT setting: *Max % Capital at Risk per Position* ≥ 1% (manual §4.16.1), or PT silently drops approved orders.

### 7.6 Sessions, news, holidays, rolls (shared)
- **RTH clock:** ES/NQ value logic uses 09:30–16:00 ET. No new intraday entries 09:30–09:45 (except opening ideas; source and reason in §7.6a), none after 15:30 unless the spec says earlier; flat by 15:55 (Cat. §2.1).
- **Early-close days:** from `_CmeHolidays` (ES/NQ) or the M8 product calendars (`CmeEnergy` for CL, `CmeFx` for 6E; 2016–2026 table). On an early close, the flatten time becomes `RthCloseEt − 5 min` and the entry cut-off becomes `RthCloseEt − 60 min` (the same pattern as `_VW_05_LateDayVwap`). Full-close days have no session. **PROPOSED – owner to confirm** the 60-minute entry cut-off on early-close days.
- **News: entry blackout (unchanged).** M-EVENT `EventBlackout` blocks new entries from −2 to +10 minutes around tier-1 releases (Cat. §2.1; RG-07): `EventConfig.enabled = true`, `blackoutTier = EEventTier.T1`, `blackoutPreMin = 2`, `blackoutPostMin = 10`, `blackoutFlatten = false`. Coverage: EV-4.6b leaves 80 GDP/PCE holes in 2016–2024. **PROPOSED – owner to confirm** `EventConfig.allowPartialCoverage = true`, so missing rows do not block 15–18% of days. The journal records `evtCoverage` per entry either way.
- **News: pre-release exit, `NewsExitMinutes` (owner, 2026-11-10).** Every batch-1 strategy has this native input, because they all otherwise hold open positions through scheduled news.
  - **Type and units:** `int`, minutes before a scheduled **tier-1** (high-impact) release (`EEventTier.T1`, macro classes, the same tier as the blackout). Range 0–120 for research.
  - **Default 0 = disabled** (**PROPOSED – owner to confirm** the default). Any value ≤ 0 disables the feature, and behaviour is then exactly the hold-through rule above.
  - **When > 0, at each closed bar t:** `m = EventCalendar.MinutesTo(EEventTier.T1)` (minutes from this bar's close to the next tier-1 release; NaN when none or outside coverage). With `B` = the Data1 bar length in minutes, the window is **reached** when `0 < m < NewsExitMinutes + B`, i.e. the next bar would end inside the last N minutes before the release. The market exit then fills at the next bar's open, at or before release − N. On 5m bars with N = 5 and an 08:30 release, the exit is sent at the 08:25 close and fills at 08:25.
  - **Effects when reached:**
    - an open position is flattened with `<ID>LX`, reason `NEWS`;
    - working entry orders are not re-sent, and no new entry is armed;
    - entries stay blocked until the existing post-release blackout ends (release + 10 minutes);
    - after that the spec's normal re-entry rule applies. A `NEWS` exit counts as the setup's attempt (no re-entry on the same setup instance, §7.7).
  - **Why not `EventFlattenNow`:** M-EVENT's blackout tests whether the *current* bar overlaps the window, so its flatten fires one bar late on a closed-bar strategy. The strategy computes the rule above with `MinutesTo`. It needs `EventConfig.enabled = true`, which every batch-1 strategy already sets for the blackout.
  - **Coverage holes:** when `MinutesTo` is NaN because a day is a coverage hole, no news exit fires. That entry is already journaled with `evtCoverage`.
  - **Precedence:** item 4 in §7.4, after the MM force-close, stop and target, and before the session-end flatten.
  - **Journal:** `<ID> NEWS-EXIT t() event(<kind> <release ET>) minutesTo() lead(<N>) pos() px()` when flattening, and `<ID> SKIP news-window: event(<kind> <release ET>) minutesTo()` for a blocked entry.
- **Rolls:** `RollDetection = ERollDetection.Table` for ES/NQ (PR #116 keys the table roll by the Globex day on calendar-keyed charts; M9's calibrator is log-only and ES detection stays at the literal 8-point gap). Multi-session levels (composites, swing anchors) reset at the splice. The strategy may not enter on a roll-window day (`IsRollWindow`). Intraday strategies are flat every day, so no position crosses a splice. Swing positions (AV-01) are flattened at the close of the last session before the splice (§7.4 item 6), because the continuous series is **unadjusted** and a held position would book the roll gap as fake P&L. CL and 6E roll rules come from their M-INSTR presets; their vendor-splice equivalence is still VERIFY (M9), so CL/6E runs are labelled provisional.
- **Overnight / weekend:** intraday strategies never hold. Only AV-01 (and AV-02's later swing arm) holds overnight and over weekends. Each has its own max hold.

### 7.6a Why entries are blocked 09:30–09:45
- **Source:** catalogue §2.1, "Sessions, timing, costs (from final-synthesis §4.0)": *"Intraday ideas: no entries 09:30–09:45 unless the idea is an opening idea"*.
- **Reason as cited:** opening noise. TS-07 cites Keller B2 15.7, *"do nothing 0–5 min"*, and final-synthesis (no entries 09:30–09:45). The fill model also assumes 2 ticks of slippage at the open instead of 1 (§2.1).
- **Range:** it covers **09:30–09:45 ET (15 minutes)**, not 09:30–10:00. The 09:30–10:00 in the YV-11 notes is Malyarovich's 1m/2m timeframe slot, not a block.
- **Status:** the catalogue calls it an assumption to test once (TS-07: blackout ∈ {5, 15, 30} minutes as a layer on PS-07, PS-24, MT-01).
- **09:31 variant (**Owner-approved 2026-11-10 22:06 ET**: *"Add a variant that starts at 9:31"*):** entries that trade the open use `OpenWindow`, type `public enum EOpenWindow { Block0930to0945, From0931 }`, default `Block0930to0945`. With `From0931`, the first closed 1m bar (09:30–09:31) can arm an order, so the first order can work from 09:31 (closed-bar rule, §3 rule 6). Opening-bar slippage of 2 ticks applies. Batch-1 specs already expose the numeric `EntryStartHHMM` (a clock time, not a choice), so 931 is a legal value there; none of them is an opening idea.

- **Keep Keller's block as the default; offer the open as an option only where it fits (Owner-approved 2026-11-10 22:17 ET):** *"Regarding not trading the opening window we can keep Keller's advice for now but lets keep it as an option if it makes sense for the strategy to do."*
  - The default stays `Block0930to0945` everywhere.
  - The `OpenWindow` input (`From0931`) is offered **only** on strategies where trading the open makes sense: the opening ideas YV-11 and YV-12, and any other entry that is an opening idea.
  - Strategies that are not opening ideas, including all of batch 1, get no `OpenWindow` input and keep the 09:30–09:45 block.

### 7.7 Re-entry and adds
- **Adds are allowed where they make sense** (**Owner-approved 2026-11-10 22:06 ET**: *"1. No, you can allow adding to positions, for strategies where that make sense make it a clear Input parameter and allow adding."*). This replaces rev 1.4's "no adds in batch 1".
  - **Inputs (identical names in every spec that allows adds):**
    - `AddMode`, type `public enum EAddMode { None, OnNewPullback }`. The default is whatever the source says: `None` unless the source method adds.
    - `MaxAdds` (int, 0–4): the most adds per position.
    - `AddContracts` (int, ≥ 1, default 1): contracts per add.
    - `AddMinR` (double, default 1.0, **PROPOSED**): an add is armed only while the trade is `AddMinR` × R in profit: long last close ≥ average entry + `AddMinR` × R; short last close ≤ average entry − `AddMinR` × R. You add to a winner, never to a loser ("exit, don't average", Keller B1 4.3).
    - `AddStopMode`, type `public enum EAddStopMode { RaiseToAddLow, KeepCurrent }`, default `RaiseToAddLow`: on an add fill, the stop for the **whole** position moves up to the add trigger's structural low − 1 tick. It is one-way (§7.3) and never lowered. **Short:** the same member moves the whole-position stop **down** to the add trigger's structural **high** + 1 tick (a new lower high), and it is never raised. The member name stays `RaiseToAddLow` for both sides (one enum per Prog §3 rule 9); its meaning is "tighten to the add trigger's structural extreme". `KeepCurrent` leaves the stop where it is.
  - **Add trigger:** spec-specific (each spec's §5 Adds row). It is always the spec's own entry trigger firing again in the trade direction while the position is open. Each add uses its own order name (`<ID>LA1`, `<ID>LA2`, … long; `<ID>SA1`, `<ID>SA2`, … short) so the journal can separate legs.
  - **Exits:** adds join the runner. T1 and the split rule (§7.2) use the **initial** position only, so the 2-contract test keeps its 1 + 1 split. Adds exit with the runner by trail, stop, invalidation, news or FlatBy. Maximum chart size = `Contracts` + `MaxAdds` × `AddContracts` (2 + 2 × 1 = 4 by default for a spec that adds).
  - **Risk (chart):** before arming an add, compute the total open risk = Σ contracts × |entry_i − new stop| × BigPointValue, using the stop the add would set. Skip the add (`SKIP add-risk`) if this is > `MaxRiskPct`% of equity (1%). Adds are logged as `<ID> ADD n() qty() px() newStop() openRisk()`.
  - **R is re-based on each add fill** (**Owner-approved 2026-11-10 22:35 ET** (Ettienne, PR #118 answer 2: *"the stop in general would move for the second entry so the risk calculation should be adjusted"*; applied here for consistency with PR #118 §3a)). This replaces rev 1.5's "R stays the initial position's R". It uses the same rules as PR #118 (`specs/m-port-add-legs-spec.md` §3a), so chart and Portfolio Trader R agree:
    - **When:** on the bar the add **fills** (the position grows in the trade direction). Never on the arm, and never for a non-add fill.
    - **New 1R:** `R1 = max(0, side × (close of the fill bar − new whole-position stop)) × BigPointValue × contracts after the add`. That is the full post-add risk at the stop the add set. `side` = +1 long, −1 short, so for a short `R1 = max(0, new stop − close) × BigPointValue × contracts`. PR #118 uses the same close (`pm.px`), so both sides compute the same 1R.
      - If `R1` ≤ 0, there is no re-base and the segment keeps its 1R: log `<ID> REBASE-SKIP R1()`.
    - **Trade R = Σ segment P&L ÷ segment 1R.**
      - Segment 0 runs from the entry to the first add fill, with 1R = the initial R frozen at entry.
      - Each add fill starts a new segment, marking the open position to market at that bar's close.
      - A stop-out at the raised stop is then about −1R for that segment, not a multiple of the initial R.
    - **Partial exits** (T1, scale-outs) realise their P&L in the segment they happen in. A segment's 1R is frozen: it does not shrink when contracts leave. A partial exit never starts a segment.
    - **Where re-based R is used:**
      - trade reporting in R and the `R` field of the exit log;
      - the trader's-equation and R-based acceptance stats;
      - `AddMinR` for the next add, measured from the current average entry in the current segment's 1R per contract (**Owner-approved 2026-11-11 08:58 ET** (Ettienne: *"These rules are fine, you can add them. We might change some of the mechanics later on as required."*; the mechanics may be revised later)).
      - Stop and target rules keep their own spec definitions, which are based on the initial R and its trigger levels. They are unchanged by the re-base (**Owner-approved 2026-11-11 08:58 ET** (Ettienne: *"These rules are fine, you can add them. We might change some of the mechanics later on as required."*; the mechanics may be revised later)).
    - **Audit logs:** on each re-base, `<ID> REBASE leg() pos(a->b) stop(old->new) px() R0() R1() segR()`. The exit log carries both `R()` (re-based) and `Rorig()` (total P&L ÷ initial R). Trades without an add have one segment, so `R` = `Rorig`, and their logs are unchanged apart from the `Rorig()` field.
  - **MultiCharts setting:** Strategy Properties → Position Limits must allow up to 1 + `MaxAdds` entry orders in the same direction. Coder verifies the exact option name in the installed build.
  - **Portfolio Trader / MM:** the current MM refuses a same-direction entry while in a position (`_PortMM.Function.CS` "in position"; m-port-spec rev 2.5 §5.4: "same-direction adds stay out"). `EntryContracts > 0` would also apply to every entry order, including adds (m-port-spec: "strategies that pyramid must use CodeSize mode or publish `pm.qtyCode` per leg").
    - **Until M-PORT supports adds, PT runs use `AddMode = None`**, and add arms are chart-only research runs.
    - **M-PORT change spec (DRAFT, docs only): PR #118**, `specs/m-port-add-legs-spec.md` on `feat/mport-add-legs-spec` (Ettienne 2026-11-10 22:16 ET: *"Yes, spec the Portfolio risk manager change as a separate PR."*). It proposes opt-in `EAddPolicy { Refuse, AllowWithinCap }` (default `Refuse`), post-add whole-position risk with the current open risk released, and native checks A1–A7. Until it is approved, built and checked, the line above stands.
    - Allowing adds in PT needs an M-PORT change in its own PR with owner sign-off: an add leg is approved only if the position's total open risk after the add stays within `maxTradeRisk` (0.01), the cluster cap (ES and NQ = one EQIDX position) and the total risk cap, with the stop update sent through `portfolioStop`.
- No reversals (an opposite signal while in a position is ignored and logged).
- Re-entry after a stop-out follows each spec. The default is **no re-entry on the same setup instance** (one attempt per setup), which matches the Example 26/27 `m_done` behaviour. Adds are not re-entries.
- **Specs with add inputs:** VW-01, AV-02 and AV-01 (default `None`; their sources do not add). Candidate YV-11 (default `OnNewPullback`; adds are part of Malyarovich's method). PS-02, PS-01 and OF-01 have none: they are single break/retest or first-test setups, and OF-01's chained new imbalances move the stop rather than add size (Dale; Keller 6.3 "rungs").

### 7.8 FUTURE: dynamic position sizing by % account risk (not a batch-1 test setting)
Ettienne, 2026-11-10: *"ultimately I'd like to use dynamic position sizing based on percentage risk of the account."*

**What already exists (checked on `main` @ 47ffda2):**
- **M-PORT supports it in Portfolio Trader.** `_PortMM.Function.CS` `PortMmConfig.sizing = EPortSizing.RiskParity` is the default. `_PortMath.Size` computes contracts = ⌊mult × `riskPerTrade` × book equity / R$⌋. The Starter profile uses `riskPerTrade` 0.005 and the hard cap `maxTradeRisk` 0.01. Here R$ is the strategy's armed risk per contract from `sizeForRisk` / `portfolioArm` (widened stop). `_PortfolioRiskManager` writes the result to `EntryContracts`.
- **`EntryContracts` semantics (Check 7, 2026-11-10):** −1 = the strategy's coded size; 0 blocks every entry; > 0 = the MM's size. Under `EPortSizing.CodeSize` the MM returns −1 and skips, rather than scales, an entry when a throttle multiplier is below 1 (T-P13). `PortMmConfig` has no inputs; the profile is changed by a `_PortfolioRiskManager` subclass overriding `Config` (as `_PortfolioRiskManager_ScaledBook` does).
- **On a chart,** the base `sizeForRisk(Bars, entry, stop, riskFraction, limits, widenedStop)` already returns units ≈ equity × riskFraction / (|entry − stop| × BPV).

**Batch 1:**
- Chart tests use fixed `Contracts` = 2 (owner).
- Portfolio Trader runs use M-PORT's existing RiskParity sizing through the existing `sizeForRisk` / `portfolioArm` calls. No new sizing code is written in batch 1.

**Later (needs a separate owner go-ahead):** a native input `SizingMode` (enum `ESizingMode { FixedContracts, PercentRisk }`, default FixedContracts) on chart runs.
- `PercentRisk` uses `sizeForRisk(…, RiskFractionPct / 100, limits, widenedStop)`, with `limits.maxQty` as a hard cap.
- The odd-lot split rule (§7.2) and `SingleLotMode` then matter whenever q is odd or 1.
- Changing the sizing mode is a new trial. It needs R-based (not $-based) comparison across runs.

### 7.9 Short-side conventions (all batch-1 specs)
Added rev 1.7 for Ettienne's review request (2026-11-11 09:36 ET). Each spec's **§5a** writes out its own short rules; this section fixes what they share. Where a source (book or video) describes only the long side, the spec's short rule is marked **PROPOSED (mirror; source long-only)**.
1. **Side input.** Every spec has `Side` (`ESide { Both, LongOnly, ShortOnly }`). Default `Both` in every spec, including AV-01 (**Owner-approved 2026-11-11 09:52 ET**, answer 2: *"Yes, and big-winner rule applies to it."*). A side that is switched off still logs its setups through `logSignal(…, taken = false)` with `SKIP side-off`, so its event study exists.
2. **Sign conventions.** `side` = +1 long, −1 short, in every `SETUP`/`ARM`/`SKIP`/`EXIT` line and in `pm.side`. Distances are positive: R = |entry − widened stop|; the cost floor is the same distance for both sides and is measured from the planned entry **away from the trade** (short: stop ≥ entry + floor). Signed P&L in points = side × (exit − entry). MFE, MAE, `plannedRiskPct`, `actualRiskPct`, `maeDollars` and `actualToPlanned` are adverse/favourable magnitudes ≥ 0 for both sides (PR #119 §5.3 "Signs"); `R()` and `realizedPnlPct` are signed (loss < 0). For a short, adverse = price **up**: intrabar MAE is marked at the bar's **high**, MFE at the bar's **low**.
3. **Price words swap.** For a short read: above ↔ below, high ↔ low, VAH ↔ VAL, HL ↔ LH (`EPivotLabel.LH`), buying ↔ selling imbalance, `max(stop, c)` ↔ `min(stop, c)`, "+ 1 tick" ↔ "− 1 tick" on the entry/stop side, "low edge of the next HVN above" ↔ "high edge of the next HVN below". Targets are below the entry, stops above. Prices are rounded to the tick **away from the entry**, so a stop is never tighter and a target never closer than computed: a short stop rounds **up**, a short target rounds **down** (a long stop rounds down, a long target up); the same rule as PR #119 §4.2 (**PROPOSED**).
4. **Orders and fills (short).** A short breakout entry is a **sell-stop below the market**; it fills when price trades down to it, or at the open if the bar gaps through it (worse price, reported as slippage). A short fade entry is a **sell limit above the market**, and a short target is a **buy-to-cover limit below**. Backtests have no bid/ask: limits fill only on trade-through (§4), i.e. a sell limit needs a print ≥ 1 tick **above** it (bar high ≥ limit + 1 tick) and a buy-to-cover limit a print ≥ 1 tick below it. Live routing (sell limits rest on the offer, sell-stops trigger on a trade at or below the stop) is out of scope: no live or forward testing in batch 1.
5. **Stops are one-way down** for shorts (§7.3), break-even = entry − offset ticks (never above the entry for a positive offset), and every trail candidate for a short is "structure high + buffer", capped one tick above the last close (through-market ⇒ `TRAIL-THROUGH` market exit).
6. **Session, news, time, holiday and roll exits are side-blind.** The same FlatBy, early-close, `NewsExitMinutes`, time-stop, roll and MM rules (§7.4–§7.6) apply unchanged; only the order action differs (`<ID>SX`).
7. **Adds (short):** §7.7 — the add trigger is the spec's short entry trigger again (a new rally that fails at a lower high), armed only when last close ≤ average entry − `AddMinR` × R; the whole-position stop is **lowered** to the add's high + 1 tick; R re-base uses `side` = −1. PR #118 §2.2/§3a covers the MM side for both signs.
8. **Asymmetry and reporting (required in every report).** Equity index futures have a long-run upward drift, so long and short results are not exchangeable. Every report, acceptance table (§6), control (K-DR is the per-side drift control) and planned-vs-actual risk table splits **long / short / combined**: trades, net expectancy (R), PF, win rate, MFE/MAE, `actualToPlanned` > 1.00 and > 1.10 counts. A combined pass with one side net negative (or failing a side-level criterion) is flagged and **that side is not traded** (**Owner-approved 2026-11-11 09:52 ET**; rule in §6 item 12, set through the `Side` restriction of the registered config).
9. **Short-specific risks.** (a) **Gap / limit-up:** an intraday short is flat by 15:55 ET, so overnight gaps only matter for AV-01's short mirror (swing). A stop gapped through fills at the next open and is reported as gap slippage; `actualToPlanned` > 1 counts it. CME equity index price limits (overnight up/down limits; RTH circuit breakers are downside-only) mean an up-move can also run without a limit halt (**VERIFY** the current CME rule set before any swing short run). (b) **Squeezes around news:** short-covering spikes are covered by the same entry blackout and `NewsExitMinutes`; no extra short rule is added. (c) **Stocks only:** borrow availability / fees and SEC Rule 201 (short-sale restriction after a 10% fall) cannot be reconstructed from bars, so stock arms start long-only (video additions, pool design item 7). Futures have no uptick rule.
10. **Engine support for both sides** (checked on `main`): `EPivotLabel` has `LH`/`LL` (`_PriceActionEngine`), `_OrderFlow.Side { Buy, Sell }` with `Absorption(ago, Side.Sell)` = bids absorbing at the bar low (the mirror of `Side.Buy` at the high), `DiagonalImbalances`/`StackedImbalances` return both sides, and M-AVWAP anchors on pivot highs and lows. No engine change is needed for any short rule in §5a of the specs.
11. **Own-mirror shorts are accepted pending test confirmation** (**Owner-approved 2026-11-11 09:52 ET**, answer 3: *"Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades."*). Applies to the shorts marked "PROPOSED (mirror; source long-only)": VW-01, AV-02, AV-01, OF-01 and YV-11 (§5a / the YV-11 short table). Required:
   - **Source header:** the implemented signal's header (Status / Notes section) and the short branch in code carry the comment `// SHORT SIDE UNCONFIRMED — testing must confirm short-side trades (Prog §7.9 item 11)` until the confirmation below is recorded. Coder checklist item in §9.
   - **Test step (before any development read):** a smoke chart run must produce short fills; then a **trade-list review of the first 20 short trades** (or all, if fewer than 20 in the smoke window) against the spec's §5a rules: setup, trigger bar, order type and price, initial stop, T1, trail moves and exit reason, checked bar by bar on the chart. Any mismatch is a bug fixed before the run continues. Record the result in the run sheet as `short-confirmed(<date>, n=<k>)`; only then is the header comment removed. The same review of 20 long trades is already part of the native protocol.
12. **Separate long and short values** (**Owner-approved 2026-11-11 09:52 ET**, answer 4: *"Get their own values."*). Every numeric input that shapes an entry, stop or target is a **Long/Short pair** (`<Name>Long`, `<Name>Short`). Short defaults equal the long defaults at first (**PROPOSED** values, so the first run is symmetric). Side-blind context inputs (composite length, balance overlap, classification time, footprint TF, imbalance ratio, entry window, flatten time, news, max hold, add counts) stay single. Enums stay typed and shared. Each spec's **§8.2** lists its pairs:
   | Spec | Split into Long/Short pairs |
   |---|---|
   | PS-02 | `AcceptCloses`, `DeltaMinTicks`, `StopWidthW`, `TargetW`, `FailCloses`, `VolBeyondPct` |
   | PS-01 | `AcceptCloses`, `DeltaMinTicks`, `HoldW`, `StopBufferTicks`, `TargetW`, `DestOffsetTicks`, `BreakEvenR`, `FailCloses`, `VolBeyondPct` |
   | VW-01 | `TouchBandSigma`, `StopSigma`, `SlopeAtrPerBar`, `PocMigrationAtr`, `MaxClosesThroughVwap`, `EpisodeMaxBars`, `AcceptAcrossCloses`, `TrailOffsetTicks`, `AddMinR` |
   | AV-02 | `ImpulseAtr`, `TouchLowerSigma`, `TouchUpperSigma`, `StopSigma`, `TrailOffsetTicks`, `AddMinR` |
   | AV-01 | `AnchorK`, `BarsBelowMin` (short: bars **above**), `SwingFloorAtr20d`, `TrailOffsetTicks`, `AddMinR` |
   | OF-01 | `MinStack`, `MinAwayBars`, `ContextAtr`, `StopBufferTicks`, `StopMinAtrPct`, `StopMaxAtrPct`, `TargetOffsetTicks`, `MinRR`, `TargetR`, `TrailBufferTicks`, `KAggressionBars` |
   | YV-11 | `SlopeMinAtr`, `SlopeMaxAtr`, `ExtMaxAtr`, `GapMinAtr`, `TargetR`, `TriggerBars`, `AddMinR` |
   | YV-12 | `LevelTolAtr`, `MaxOvershootAtr`, `WickMinFrac`, `CloseZoneFrac`, `MinRangeAtr`, `TargetOffsetTicks`, `BreakEvenR` |
   - **Optimizer impact.** Pairs double the swept dimensions: a crossed long × short sweep of an 8-config grid is 64 configs, with a matching multiple-testing cost. Rule (**PROPOSED**): the registered sweep is run **per side**, i.e. once with `Side = LongOnly` over the `*Long` inputs and once with `Side = ShortOnly` over the `*Short` inputs (2 × 8 = 16 configs, not 64). Each side's primary cell is frozen before any read, as today. Parameter stability (§6 item 9) and the Holm family are counted per side. MC's optimizer sweeps the two inputs independently, so a run sheet must pin the unused side's inputs at their defaults. Whether enum inputs can be swept is still the open check in §9.

## 8. Logging contract (shared)

- `TraceLevel = ETraceLevel.Journal` for research runs. That writes `MCJournal_<runId>.csv` (trades) and `MCEvents_<runId>.csv` (signals).
- **RUN line** (base) carries every `[Input]` value. The strategy adds one Info line at the first `CalcBar`: `<ID> CFG v(<rulesVersion>) policy(<AcceptancePolicy>) maxTradePct(<x>) outlierPct(<y>) costFloor(<on|off>) floorTicks(<n>) roll(<mode>) evtCoverage(<partial|strict>) newsExitMin(<n|off>) holdoutCut(<yyyy-mm-dd>) holdoutMonths(<n>) detail(<1m|tick>)`.
- `journalArm(<order name>, plannedEntry, initialStop(widened), firstTarget, ctx)` next to the entry `Send`, with ctx including the setup's key levels.
- `logSignal(<tag>, dir, price, stop, ctx, taken)` for **every qualifying setup, traded or skipped**. This is the F-EVT event-study and control harness.
- Lines, all `ETraceLevel.Journal`, prefix = catalogue ID:
  - `<ID> SETUP <date> side(<±1>) <setup fields>` (side −1 = short; every line below carries `side()` too, §7.9)
  - `<ID> ARM side() entry() stop() widenedStop() target() qty() R$()`
  - `<ID> SKIP <reason>: <detail>` (reasons are fixed strings per spec)
  - `<ID> T1 qty() px()` · `<ID> BE stop()` · `<ID> TRAIL stop() basis()`
  - `<ID> EXIT reason(<STOP|TARGET|T1+RUNNER|INVALIDATION|TIME|FLATBY|EARLYCLOSE|NEWS|ROLL|FLAT-BY-MM|TRAIL-THROUGH|FLOW-WARN>) px() R() mfe() mae()`. MFE and MAE are in R, per position, from F-JRNL (manual §5.13). Every spec logs them, per Keller B4 §8.4 step 9 ("record R, MFE and MAE for every trade")
  - `<ID> NEWS-EXIT …` and `<ID> SKIP news-window …` (§7.6)
  - **Planned vs actual risk % on every closed trade** (owner request 2026-11-11 09:19 ET: *"we essentially have a projected risk percentage when we start out (e.g. 1%) but once the market has moved it may or may not have moved against us by a certain percentage so we want to log that as the actual risk percentage"*). The `EXIT` line and the CS-1 journal row also carry `plannedRiskPct`, `plannedRiskPctPeak`, `actualRiskPct` (whole-trade MAE $ ÷ equity at entry), `actualToPlanned` (MAE in initial R) and `realizedPnlPct`. The definitions and the reporting rules (actualRiskPct distribution; count of trades above the planned risk) are in PR #119, `specs/generic-exit-manager-spec.md` §5.3. They are computed in the `_CustomSignal` base, so every strategy logs them.
- These strings are journal text, not MC API members (§3 rule 5 concerns enums and API members).

## 9. Implementation order for Coder

1. Shared, before any strategy code:
   - [ ] **Cost rows** (owner, 2026-11-10). In `_InstrumentProfile.Function.CS`, `InstrumentConfig.costs` defaults (currently `{ CostKey(EAssetClass.Future, "ES"), 1700 }, { CostKey(EAssetClass.Future, "MES"), 250 }`), add the rows below in the same format:
     - `{ CostKey(EAssetClass.Future, "NQ"), 948 }`, provisional, accepted
     - `{ CostKey(EAssetClass.Future, "CL"), 1472 }`, confirmed
     - `{ CostKey(EAssetClass.Future, "6E"), 1117 }`, provisional, accepted

     The key format is `<Class>:<ROOT>`, e.g. `Future:NQ`, validated by `ValidCostKey`. Then:
     - Update the single `costSource` string to name the 2026-11-10 decision and mark NQ/6E as provisional (e.g. `"Ettienne 2026-10-31 (ES/MES); 2026-11-10 NQ 948 prov., CL 1472, 6E 1117 prov."`).
     - Extend the M7 off-MC tests with the floors NQ 19, CL 15 and 6E 18 ticks.
     - Note that the Canon/config hash changes. This goes in its own small PR on a feature branch. It is an engine-default change, so no strategy edits are needed.
   - [ ] Read the PR #103 header template.
   - [ ] **Adds (§7.7):** for specs with `AddMode`: separate add order names, Position Limits allowing 1 + `MaxAdds` same-direction entries, the add risk check, one-way whole-position stop raise, adds excluded from the T1 split. PT runs keep `AddMode = None` until M-PORT supports add legs.
   - [ ] **Short side (§7.9, rev 1.8):** Long/Short input pairs per spec §8.2 (short defaults = long defaults); the `SHORT SIDE UNCONFIRMED — testing must confirm short-side trades` header comment and short-branch comment on VW-01, AV-02, AV-01, OF-01, YV-11 until the 20-trade short review is recorded (§7.9 item 11); the `T1-SKIPPED` rule and Info line (§7.2a); `side-off` skip logging.
   - [ ] **Variants:** every variant in each spec's §8.1 is selectable via Inputs; default = primary. Check that every §8.1 input is in the §8 table and in header §17, and that the default constructor values reproduce the primary rule exactly.
   - [ ] **Multi-value inputs are typed enums, not ints/strings.** Declare each spec's enums (Prog §3 rule 9, spec §8.1) as nested `public enum` types, type the `[Input]` property with the enum, and set the default in the constructor. Bools for on/off only.
   - [ ] **Verify enum inputs on the MiniPC** (one temporary sandbox study, removed before commit, no csproj commit): (a) an enum `[Input]` shows its named members in the chart Format Signal dialog and in Portfolio Trader; (b) whether the chart Strategy Optimizer and the PT optimizer can sweep it. Record the result in the spec and in Prog §10. If sweeping is not supported, run one registered config per member; do not fall back to ints without owner sign-off.
2. PS-02 first (simplest profile host), then PS-01 reusing the same break detector (factor the break/acceptance state machine into a private nested class inside each file, or into one sandbox helper if Coder prefers; no new root engine without owner sign-off).
3. VW-01, AV-02, AV-01.
4. OF-01 after the @ES/@NQ bid/ask corpus (from 2024-10-28 to the cut) is verified in QuoteManager (OF-01 §10).
5. For each: off-MC unit tests where logic is MC-free (state machine, split rule, precedence), native compile on the MiniPC via one temporary `Compile Include`, chart run (smoke, then development), journal diff of two identical runs, then Portfolio Trader one-row reconciliation.

## 10. Open questions for Ettienne (batch-wide)

Resolved 2026-11-10 19:44 and 19:53 ET and removed: ES $17.00 convention; NQ/CL/6E cost rows; 2-contract test size; single contract exits everything at T1; AV-01 `big-winner-trend`; news exit as an integer-minutes parameter; OF-01 test window (holdout re-scope).

- **[RESOLVED 2026-11-10 19:53 ET, owner-approved]** Holdout: 18 months, OF-01 only; all other strategies stay ≤ 2024 (§5.1).

- **[RESOLVED 2026-11-10 20:31 ET, owner-approved]** Rev 1.3 catalogue changes: the PS-02 failed-break exit is on in the primary; the PS-01 POC stop and scale-out with a runner; the VW-01 touch sweep {first-or-second, first only}. Variants are documented and selectable via Inputs (§3 rule 8); multi-value inputs are typed enums (§3 rule 9, 20:34 ET).

- **[RESOLVED 2026-11-10 22:06 ET, owner-approved]** Adds allowed where they make sense, as clear inputs (§7.7). Stock scans documented for TC2000/FinViz, and a predetermined-pool backtest design is noted (`specs/video-ideas-2026-11-10-additions.md`); ES/NQ stay primary. Day-2 futures proxy approved. 09:31 start variant (§7.6a). YV-12 break-even at +1 R primary, no-BE comparison.

Still open:

1. `NewsExitMinutes` default 0 (disabled) for all six, tier-1 releases only; and `EventConfig.allowPartialCoverage = true`?
2. Top-1% trim rounding: K = max(1, ⌈1% × winning trades⌉)?
3. Entry cut-off 60 min before an early close, flatten 5 min before.
4. Odd-lot split (runner gets the odd contract) for MM-sized or future %-risk positions.
5. Older @ES/@NQ minute history (2016–2022): is it downloaded and flushed? Until then everything is a smoke test.
6. ~~**Short side (rev 1.7, §7.9):** if the combined strategy passes but one side is net negative, does that side fail (run the other side only as a new registered trial), or does the combined result stand? PROPOSED: the losing side is flagged and not traded.~~ **Owner-approved 2026-11-11 09:52 ET**: flagged and not traded (§6 item 12).
7. ~~**AV-01 short mirror**~~ **Owner-approved 2026-11-11 09:52 ET**: approved, `Side` default `Both`, big-winner rule applies (AV-01 §5a).
8. ~~**Short mirrors not stated by the sources** (marked "PROPOSED (mirror; source long-only)" in each §5a): accept the mechanical mirrors as written, or check the books (Keller B1 Ch 4.3 for down trend days, Dale for selling stacks) first?~~ **Owner-approved 2026-11-11 09:52 ET**: accepted for now, to be confirmed by testing (§7.9 item 11).
9. **Paired-input sweep:** run the registered sweep per side (2 × 8 configs) rather than crossed (64), with each side's primary cell frozen first (§7.9 item 12)? PROPOSED: per side.

## 11. PROPOSED rules to check against the books and videos (separate review)

Rev 1.3 (2026-11-10, 20:17 ET findings): the Kindle findings Ettienne relayed for Keller Book 1 (Ch 3.3, 4.3, 6.3), Keller Book 4 (§2.4, §8.4) and Dale *Volume Profile* (Setup #2) are applied below. Locations are Kindle locations, and the quotes were read from page images, so they may differ slightly. Where a book is silent, the item stays PROPOSED with "checked: not specified in Keller B1 / B4 / Dale VP". Rows 7–10 were checked against Dale *Order Flow* in rev 1.2. Each item names the source to open, the topic, and the rule to confirm or replace. Page and location references are from the analyst's Kindle notes in `kindle-notes/`, where known.

| # | Spec rule (PROPOSED) | Source to check | What to look for |
|---|---|---|---|
| 1 | ~~PS-02 stop, scale-out and runner~~ | **Checked against Keller B1 Ch 3.3 (Loc ~951–959) and Keller B4 §2.4 (Loc ~721–838) / §8.4, 2026-11-10** | Applied in PS-02 rev 1.3: mapped to Keller entry (a); stop back inside value confirmed; the half at 1 W plus runner is now sourced to B4 §2.4/§8.4 Rule 4 (FX/crypto, not futures evidence); V-score alignment default (B4 Rule 4, steps 7–8); failed-break exit added (Keller 3.3), a registration change owner-approved 2026-11-10 20:31 ET; optional nearest-destination runner limit and acceptance flow check. Checked, not specified in Keller B1 / B4: runner trail basis, BE, number of closes, buffer, order life. EX-05 trail and no-BE stay catalogue rules |
| 2 | PS-02/PS-01 acceptance = 2 closes beyond edge + δ (δ = max(2 ticks, 0.10 × ATR5)) | **Checked: not specified in Keller B1 / B4 / Dale VP** (Keller 3.3 gives qualitative criteria only; Dale VP Setup #2 has no acceptance rule) | Still **PROPOSED**. Keller's criteria are added as an optional check (developing POC migration; ≥ 25% volume beyond the edge, from final-synthesis Spec 1) and as logged stratifiers (LVN / HVN beyond the edge) |
| 3 | PS-01 stop, target, buy-stop life, BE, invalidation | **Checked against Keller B1 Ch 3.3 and B4 §8.4**; Trader Oxygen video YV-01 (https://youtu.be/jmX_ReJxFm8) still unchecked | Applied in PS-01 rev 1.3: Keller entry (b); stop through the balance POC is primary (HL − 1 tick kept as a variant); scale at 1 W, runner to the nearest destination (next composite HVN / naked POC); failed-break exit = the existing close-back-inside rule. POC stop and scale-out owner-approved 2026-11-10 20:31 ET. Checked, not specified in Keller B1: buy-stop life, BE offset, POC buffer. Still PROPOSED |
| 4 | VW-01 classification, touches, stop, target, failure exit | **Checked against Keller B1 Ch 4.3 (Loc ~1307–1358), 2026-11-10**; Dale VP Setup #2 (pp.81–89) cross-referenced | Applied in VW-01 rev 1.3: 10:30 classification is inside Keller's 30–60 minute window (11:00 is not; swap proposed); first-or-second touch is primary (touch sweep {first-or-second, first only} owner-approved 2026-11-10 20:31 ET); classification holds unless the day changes type; accept-across-VWAP exit; LVN-touch skip; stop VWAP − 1σ as the far-side-value proxy; target new extremes (session high, then trail). Checked, not specified in Keller B1: numbers, touch definition, σ count, trailing, RTH vs session VWAP, episode length. Still PROPOSED. Dale VP Setup #2 = catalogue PS-12/DV-04 (Example 34), a future alternative, not a new spec |
| 5 | AV-02 touch zone [AVWAP − 0.25σ, + 0.5σ]; anchor-low invalidation; 15:00 ET entry cut-off; 5-session anchor expiry | Shannon, *Maximum Trading Gains with Anchored VWAP* | **Shannon not available in owner's Kindle library; rules remain from catalogue/notes.** Still PROPOSED |
| 6 | AV-01: reclaim δ60 = max(2 ticks, 0.10 × ATR14(60m)); ≥ 10 bars below first; 60m-close trail on the new impulse AVWAP; max hold 20 sessions; long only | Shannon | **Shannon not available in owner's Kindle library; rules remain from catalogue/notes.** Still PROPOSED |
| 7 | ~~OF-01 move-away, first test, zone definition~~ | **Checked against Dale by Ettienne, 2026-11-10** (pp.74–76) | Applied in OF-01 rev 1.2: `MinAwayBars` 1–2 on 30m footprints; first test only; the whole stacked cluster is the zone. Still PROPOSED: several separate stacks = separate zones |
| 8 | ~~OF-01 stop, band, targets, trail~~ | **Checked against Dale** (p.136; pp.124–128; pp.129–131) | Applied: stop beyond the zone; 10–20% daily-ATR band with Skip/ClampFixed; target just before the next HVN with a `MinRR` skip; aggression trail and warning signs. Still PROPOSED: ATR length, clamp value, HVN profile/offset, `MinRR` value, exact trail-move and warning-exit rules. **Unread:** SL examples 1–4 (p.137) and the second option in Tip #2 |
| 9 | ~~OF-01 entry trigger~~ | **Checked against Dale** (p.76) and **Keller B1 Ch 6.3 (Loc ~1896), 2026-11-10** | Applied: direct limit at the zone edge is primary (Dale). Rev 1.3: the unsourced higher-low variant B is replaced by **variant K (Keller 6.3)**: renewed-aggression trigger, escaped-balance filter, 180-minute freshness, opposing-stack stand-down, trend-day sizing, drive-extreme / nPOC / HVN targets, chained-origin trail. Checked, not specified in Keller B1: numbers for real volume/travel, the aggression test, age cut. Still PROPOSED |
| 10 | ~~OF-01 volume floor and flow quality~~ | **Checked against Dale** (pp.74–76, "Minimal Imbalance Volume" 0; Trades Filter = visual only) | Applied: no volume floor (engine minimum `minVol` 1); P70 floor and close-location are optional, "not from Dale". Bid/ask data validity (quality ≤ High) stays as a data rule |
| 11 | `NewsExitMinutes` default 0 and the −2/+10 entry blackout | No book source found in the notes. Check the video write-ups (catalogue YV-01…YV-09 section and `specs/video-ideas-2026-11-08-additions.md`; the 2026-11-01 write-up is referenced by the catalogue but not in `specs/` on this branch) for any presenter rule on exiting before news | A stated lead time (e.g. 5 or 15 minutes) |
| 12 | Early-close entry cut-off 60 min / flatten 5 min; odd-lot split | No source in the notes; owner preference | — |
