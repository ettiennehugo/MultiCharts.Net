# Strategy specs, batch 1: programme overview and shared rules

**Rev 1.3 (draft), Tuesday 10 Nov 2026, America/New_York (EST).** Rev 1.3 applies Ettienne's Kindle findings from Keller Book 1 (Ch 3.3, 4.3, 6.3), Keller Book 4 (§2.4, §8.4) and Dale *Volume Profile* (Setup #2) to PS-02, PS-01, VW-01 and OF-01 (§11). It adds MFE/MAE to the EXIT line (§8). Rev 1.2: holdout scope confirmed by Ettienne at 19:53 ET (*"18 months for OF-01 only is fine."*), and OF-01 refined from Trader Dale's book. Rev 1.1 applied Ettienne's answers of 19:44 ET (PR #117): *"1. $17 and yes, add costs for NQ, CL and 6E as well. 2. use 2 contracts, yes you can exit everything on the first target - ultimately I'd like to use dynamic position sizing based on percentage risk of the account. 3. re-scope the holdout period to be about 18 months or even 12 months. 4. yes, use the big-winner rule. 5. give a parameter for the number of minutes to exit before news (as integers in terms of minutes) and disable it when 0 or negative."* Rev 1.0: Prepared by Analyst (Grok Bot) for Coder. Requested by Ettienne, 19:12 ET: *"Yes, please write them up. Refer to the relevant books/sources if necessary to refine the specs."* Extra requirement, 19:14 ET: *"Also be cognizant of any trade management and exit rules relevant to the strategies and please create clarity around those aspects as well in the spec."*

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
- Brian Shannon, *Maximum Trading Gains with Anchored VWAP*: **not available in owner's Kindle library; AV-01 and AV-02 rules remain from catalogue/notes.**
- Trader Dale, *ORDER FLOW: Trading Setups (The Insider's Guide To Trading)*: Setup #4 Stacked Imbalances (pp.74–76), take profit (pp.124–128), trailing (pp.129–131), stop loss (p.136). Read by Ettienne in the Kindle reader on 2026-11-10 (page labels ±2); earlier analyst notes: `kindle-notes/dale-orderflow-pp13-128.md`.
- Brian Shannon, *Maximum Trading Gains with Anchored VWAP* (anchors at significant swings; buy strength after a pullback that recovers a swing-high AVWAP; stop under the most recent higher low). Practitioner, grade E (Cat. §0 item 3).
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
| `HoldoutScope` | `OrderFlowOnly`: the rolling cut applies to OF-01 (the only order-flow strategy in batch 1). All other strategies keep the fixed cut **2025-01-01**: tests use data up to 31 Dec 2024 only. |
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

Passing qualifies a strategy for **monitored paper incubation only**: intraday ≥ 3 months and ≥ 100 trades; swing ≥ 6 months and ≥ 30 trades (Page suggestion). Live capital is a separate owner decision.

## 7. Shared trade-management and exit framework

Each spec has its own dedicated **Trade management and exits** section with the exact numbers for that strategy. This section fixes the mechanics they all share, so the specs cannot drift.

### 7.1 Order set (per side; long shown, short mirrors)
| Purpose | MC order (typed) | Name pattern | Notes |
|---|---|---|---|
| Entry | `OrderCreator.Stop` / `MarketNextBar` / `Limit` with `Contracts.UserSpecified`, `EOrderAction.Buy` | `<ID>LE` (e.g. `PS02LE`) | Size = coded qty, or the MM's `EntryContracts` in Portfolio Trader |
| Protective stop | `OrderCreator.Stop`, `EOrderAction.Sell`, `OrderExit.FromAll` | `<ID>LS` | **Re-sent on every closed bar while in a position.** Never moved away from the trade |
| First target (partial) | `OrderCreator.Limit`, `Contracts.UserSpecified`, `EOrderAction.Sell` | `<ID>LT1` | Quantity = T1 qty (§7.2) |
| Final target | `OrderCreator.Limit`, `OrderExit.FromAll` | `<ID>LT` | Used only where the spec has a final fixed target |
| Close-based exit | `OrderCreator.MarketNextBar`, `OrderExit.FromAll` | `<ID>LX` | Invalidation, time stop, FlatBy, event, roll |

### 7.2 Contract split and the 1-contract case
- Partial scale-outs use the **actual open position** (`StrategyInfo.MarketPosition`, absolute value), not the coded size. The MM may have changed the size (§7.5).
- q ≥ 2: T1 qty = ⌊q / 2⌋; runner = q − ⌊q / 2⌋ (the runner gets the odd contract). With the fixed 2-contract test size this is 1 + 1. The odd-lot case only arises when the MM or a future %-risk size (§7.8) gives an odd q. **PROPOSED – owner to confirm** (the catalogue says "50%" without an odd-lot rule).
- q = 1: the spec's `SingleLotMode` input decides, default `ExitAtT1` = the whole position exits at T1, matching the Page's note that one lot exits entirely at +1 R in `_ExitHalfRiskHalfRoll`. The alternative `RunnerOnly` skips T1 and manages the single lot as the runner. Changing it is a new trial. Default accepted by the owner (2026-11-10: "yes you can exit everything on the first target").
- Test size is **2 contracts** on charts (owner, 2026-11-10: "use 2 contracts"), full-size ES/NQ (CL/6E later), so the scale-out rule is exercised. The Page notes that a genuine half-exit study needs ≥ 2 lots.

### 7.3 Stops are one-way
A stop update (break-even, trail) is computed on the bar close. It is sent from the next bar and accepted only if it tightens: long `newStop = max(oldStop, candidate)`. A trail candidate is also capped one tick below the last close for a long, so it cannot be placed through the market. If the candidate would be at or through the market, the strategy sends the market exit instead and logs `TRAIL-THROUGH`.

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
- **RTH clock:** ES/NQ value logic uses 09:30–16:00 ET. No new intraday entries 09:30–09:45 (except opening ideas), none after 15:30 unless the spec says earlier; flat by 15:55 (Cat. §2.1).
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

### 7.7 Re-entry and adds
- No adds or pyramiding in batch 1. A same-direction add would be refused by the MM anyway (`in position`).
- No reversals (an opposite signal while in a position is ignored and logged).
- Re-entry after a stop-out follows each spec. The default is **no re-entry on the same setup instance** (one attempt per setup), which matches the Example 26/27 `m_done` behaviour.

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

## 8. Logging contract (shared)

- `TraceLevel = ETraceLevel.Journal` for research runs. That writes `MCJournal_<runId>.csv` (trades) and `MCEvents_<runId>.csv` (signals).
- **RUN line** (base) carries every `[Input]` value. The strategy adds one Info line at the first `CalcBar`: `<ID> CFG v(<rulesVersion>) policy(<AcceptancePolicy>) maxTradePct(<x>) outlierPct(<y>) costFloor(<on|off>) floorTicks(<n>) roll(<mode>) evtCoverage(<partial|strict>) newsExitMin(<n|off>) holdoutCut(<yyyy-mm-dd>) holdoutMonths(<n>) detail(<1m|tick>)`.
- `journalArm(<order name>, plannedEntry, initialStop(widened), firstTarget, ctx)` next to the entry `Send`, with ctx including the setup's key levels.
- `logSignal(<tag>, dir, price, stop, ctx, taken)` for **every qualifying setup, traded or skipped**. This is the F-EVT event-study and control harness.
- Lines, all `ETraceLevel.Journal`, prefix = catalogue ID:
  - `<ID> SETUP <date> side(<±1>) <setup fields>`
  - `<ID> ARM side() entry() stop() widenedStop() target() qty() R$()`
  - `<ID> SKIP <reason>: <detail>` (reasons are fixed strings per spec)
  - `<ID> T1 qty() px()` · `<ID> BE stop()` · `<ID> TRAIL stop() basis()`
  - `<ID> EXIT reason(<STOP|TARGET|T1+RUNNER|INVALIDATION|TIME|FLATBY|EARLYCLOSE|NEWS|ROLL|FLAT-BY-MM|TRAIL-THROUGH|FLOW-WARN>) px() R() mfe() mae()`. MFE and MAE are in R, per position, from F-JRNL (manual §5.13). Every spec logs them, per Keller B4 §8.4 step 9 ("record R, MFE and MAE for every trade")
  - `<ID> NEWS-EXIT …` and `<ID> SKIP news-window …` (§7.6)
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
2. PS-02 first (simplest profile host), then PS-01 reusing the same break detector (factor the break/acceptance state machine into a private nested class inside each file, or into one sandbox helper if Coder prefers; no new root engine without owner sign-off).
3. VW-01, AV-02, AV-01.
4. OF-01 after the @ES/@NQ bid/ask corpus (from 2024-10-28 to the cut) is verified in QuoteManager (OF-01 §10).
5. For each: off-MC unit tests where logic is MC-free (state machine, split rule, precedence), native compile on the MiniPC via one temporary `Compile Include`, chart run (smoke, then development), journal diff of two identical runs, then Portfolio Trader one-row reconciliation.

## 10. Open questions for Ettienne (batch-wide)

Resolved 2026-11-10 19:44 and 19:53 ET and removed: ES $17.00 convention; NQ/CL/6E cost rows; 2-contract test size; single contract exits everything at T1; AV-01 `big-winner-trend`; news exit as an integer-minutes parameter; OF-01 test window (holdout re-scope).

- **[RESOLVED 2026-11-10 19:53 ET, owner-approved]** Holdout: 18 months, OF-01 only; all other strategies stay ≤ 2024 (§5.1).

Still open:

1. `NewsExitMinutes` default 0 (disabled) for all six, tier-1 releases only; and `EventConfig.allowPartialCoverage = true`?
2. Top-1% trim rounding: K = max(1, ⌈1% × winning trades⌉)?
3. Entry cut-off 60 min before an early close, flatten 5 min before.
4. Odd-lot split (runner gets the odd contract) for MM-sized or future %-risk positions.
5. Older @ES/@NQ minute history (2016–2022): is it downloaded and flushed? Until then everything is a smoke test.

## 11. PROPOSED rules to check against the books and videos (separate review)

Rev 1.3 (2026-11-10, 20:17 ET findings): the Kindle findings Ettienne relayed for Keller Book 1 (Ch 3.3, 4.3, 6.3), Keller Book 4 (§2.4, §8.4) and Dale *Volume Profile* (Setup #2) are applied below. Locations are Kindle locations, and the quotes were read from page images, so they may differ slightly. Where a book is silent, the item stays PROPOSED with "checked: not specified in Keller B1 / B4 / Dale VP". Rows 7–10 were checked against Dale *Order Flow* in rev 1.2. Each item names the source to open, the topic, and the rule to confirm or replace. Page and location references are from the analyst's Kindle notes in `kindle-notes/`, where known.

| # | Spec rule (PROPOSED) | Source to check | What to look for |
|---|---|---|---|
| 1 | ~~PS-02 stop, scale-out and runner~~ | **Checked against Keller B1 Ch 3.3 (Loc ~951–959) and Keller B4 §2.4 (Loc ~721–838) / §8.4, 2026-11-10** | Applied in PS-02 rev 1.3: mapped to Keller entry (a); stop back inside value confirmed; the half at 1 W plus runner is now sourced to B4 §2.4/§8.4 Rule 4 (FX/crypto, not futures evidence); V-score alignment default (B4 Rule 4, steps 7–8); failed-break exit added (Keller 3.3), which changes the registered host; optional nearest-destination runner limit and acceptance flow check. Checked, not specified in Keller B1 / B4: runner trail basis, BE, number of closes, buffer, order life. EX-05 trail and no-BE stay catalogue rules |
| 2 | PS-02/PS-01 acceptance = 2 closes beyond edge + δ (δ = max(2 ticks, 0.10 × ATR5)) | **Checked: not specified in Keller B1 / B4 / Dale VP** (Keller 3.3 gives qualitative criteria only; Dale VP Setup #2 has no acceptance rule) | Still **PROPOSED**. Keller's criteria are added as an optional check (developing POC migration; ≥ 25% volume beyond the edge, from final-synthesis Spec 1) and as logged stratifiers (LVN / HVN beyond the edge) |
| 3 | PS-01 stop, target, buy-stop life, BE, invalidation | **Checked against Keller B1 Ch 3.3 and B4 §8.4**; Trader Oxygen video YV-01 (https://youtu.be/jmX_ReJxFm8) still unchecked | Applied in PS-01 rev 1.3: Keller entry (b); stop through the balance POC is primary (HL − 1 tick kept as a variant); scale at 1 W, runner to the nearest destination (next composite HVN / naked POC); failed-break exit = the existing close-back-inside rule. Checked, not specified in Keller B1: buy-stop life, BE offset, POC buffer. Still PROPOSED |
| 4 | VW-01 classification, touches, stop, target, failure exit | **Checked against Keller B1 Ch 4.3 (Loc ~1307–1358), 2026-11-10**; Dale VP Setup #2 (pp.81–89) cross-referenced | Applied in VW-01 rev 1.3: 10:30 classification is inside Keller's 30–60 minute window (11:00 is not; swap proposed); first-or-second touch is primary; classification holds unless the day changes type; accept-across-VWAP exit; LVN-touch skip; stop VWAP − 1σ as the far-side-value proxy; target new extremes (session high, then trail). Checked, not specified in Keller B1: numbers, touch definition, σ count, trailing, RTH vs session VWAP, episode length. Still PROPOSED. Dale VP Setup #2 = catalogue PS-12/DV-04 (Example 34), a future alternative, not a new spec |
| 5 | AV-02 touch zone [AVWAP − 0.25σ, + 0.5σ]; anchor-low invalidation; 15:00 ET entry cut-off; 5-session anchor expiry | Shannon, *Maximum Trading Gains with Anchored VWAP* | **Shannon not available in owner's Kindle library; rules remain from catalogue/notes.** Still PROPOSED |
| 6 | AV-01: reclaim δ60 = max(2 ticks, 0.10 × ATR14(60m)); ≥ 10 bars below first; 60m-close trail on the new impulse AVWAP; max hold 20 sessions; long only | Shannon | **Shannon not available in owner's Kindle library; rules remain from catalogue/notes.** Still PROPOSED |
| 7 | ~~OF-01 move-away, first test, zone definition~~ | **Checked against Dale by Ettienne, 2026-11-10** (pp.74–76) | Applied in OF-01 rev 1.2: `MinAwayBars` 1–2 on 30m footprints; first test only; the whole stacked cluster is the zone. Still PROPOSED: several separate stacks = separate zones |
| 8 | ~~OF-01 stop, band, targets, trail~~ | **Checked against Dale** (p.136; pp.124–128; pp.129–131) | Applied: stop beyond the zone; 10–20% daily-ATR band with Skip/ClampFixed; target just before the next HVN with a `MinRR` skip; aggression trail and warning signs. Still PROPOSED: ATR length, clamp value, HVN profile/offset, `MinRR` value, exact trail-move and warning-exit rules. **Unread:** SL examples 1–4 (p.137) and the second option in Tip #2 |
| 9 | ~~OF-01 entry trigger~~ | **Checked against Dale** (p.76) and **Keller B1 Ch 6.3 (Loc ~1896), 2026-11-10** | Applied: direct limit at the zone edge is primary (Dale). Rev 1.3: the unsourced higher-low variant B is replaced by **variant K (Keller 6.3)**: renewed-aggression trigger, escaped-balance filter, 180-minute freshness, opposing-stack stand-down, trend-day sizing, drive-extreme / nPOC / HVN targets, chained-origin trail. Checked, not specified in Keller B1: numbers for real volume/travel, the aggression test, age cut. Still PROPOSED |
| 10 | ~~OF-01 volume floor and flow quality~~ | **Checked against Dale** (pp.74–76, "Minimal Imbalance Volume" 0; Trades Filter = visual only) | Applied: no volume floor (engine minimum `minVol` 1); P70 floor and close-location are optional, "not from Dale". Bid/ask data validity (quality ≤ High) stays as a data rule |
| 11 | `NewsExitMinutes` default 0 and the −2/+10 entry blackout | No book source found in the notes. Check the video write-ups (catalogue YV-01…YV-09 section and `specs/video-ideas-2026-11-08-additions.md`; the 2026-11-01 write-up is referenced by the catalogue but not in `specs/` on this branch) for any presenter rule on exiting before news | A stated lead time (e.g. 5 or 15 minutes) |
| 12 | Early-close entry cut-off 60 min / flatten 5 min; odd-lot split | No source in the notes; owner preference | — |
