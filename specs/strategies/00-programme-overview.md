# Strategy specs, batch 1: programme overview and shared rules

**Rev 1.0 (draft), Tuesday 10 Nov 2026, America/New_York (EST).** Prepared by Analyst (Grok Bot) for Coder. Requested by Ettienne, 19:12 ET: *"Yes, please write them up. Refer to the relevant books/sources if necessary to refine the specs."* Extra requirement, 19:14 ET: *"Also be cognizant of any trade management and exit rules relevant to the strategies and please create clarity around those aspects as well in the spec."*

**Status:** documentation only. No strategy is implemented, run or validated by this document. Every number below is a predeclared rule or a source figure. None is a measured result on CME data. The 2025–2026 holdout has not been opened and must not be opened while building or tuning (§5).

**Marking convention.** Anything not backed by the catalogue, a cited book or document, an owner decision or existing engine behaviour is marked **PROPOSED – owner to confirm**. Every such item is listed again in the spec's *Open questions* section and in §10 below.

## 1. What this batch is

Batch 1 follows the first six rows of the owner-approved build order on the *Strategy-planning phase* Page: PS-02 → PS-01 → VW-01 → AV-02 → AV-01 → OF-01. Rows 7–8 (PS-25 DVP rail continuation, KD-09 Fibonacci family-count module) are batch 2. They wait for the native DVP gates and for workable hosts. The G0 multi-timeframe Fibonacci/confluence spec stays parked until several signals work (owner, 8 Nov 2026).

| # | Spec | Catalogue ID | One line | Readiness |
|---|---|---|---|---|
| 1 | [01-ps-02-balance-break-close.md](01-ps-02-balance-break-close.md) | PS-02 (+ A2-41 BookSpec variant) | Market entry after a 2-bar accepted break of a balanced 3–5-session composite; 1.0 W target with a runner | Buildable; engines exist; level gate G0-d pending |
| 2 | [02-ps-01-balance-break-pullback.md](02-ps-01-balance-break-pullback.md) | PS-01 | Same break cohort as PS-02, but enter on the first confirmed higher low that holds above the old edge | Buildable; shares PS-02's event code |
| 3 | [03-vw-01-trend-day-vwap-pullback.md](03-vw-01-trend-day-vwap-pullback.md) | VW-01 | On a classified trend day (≥ 10:30 ET), buy the first VWAP ± 0.25 σ pullback that prints a 5m higher low | Buildable; RTH VWAP must be built (see spec) |
| 4 | [04-av-02-impulse-avwap-higher-low.md](04-av-02-impulse-avwap-higher-low.md) | AV-02 (+ DV-12 touch arm, later) | After a ≥ 3 ATR impulse from a confirmed low, buy the first higher low that holds at the impulse AVWAP | Buildable; M-AVWAP + PRICEACTION exist |
| 5 | [05-av-01-swing-high-avwap-reclaim.md](05-av-01-swing-high-avwap-reclaim.md) | AV-01 | Swing sleeve: after price reclaims the AVWAP from the last significant swing high, buy the first 15m higher low; hold days to weeks | Buildable; needs a 50-day SMA filter source |
| 6 | [06-of-01-stacked-imbalance-retest.md](06-of-01-stacked-imbalance-retest.md) | OF-01 | First retest of a ≥ 3 stacked buy-imbalance zone on genuine bid/ask footprints | **Data-gated**: needs verified MC-BidAsk ticks; see the ≤ 2024 conflict in that spec |

Each spec follows the same 15 sections: purpose and edge; instruments and timeframes; engines; entry rules; **trade management and exits**; sizing, risk and M-PORT; session, news, holiday and roll handling; native inputs; logging and journal lines; test plan; acceptance criteria; implementation checklist; source-file header mapping; open questions; sources.

## 2. Sources used across the batch

Internal (repo `ettiennehugo/MultiCharts.Net`, `main` @ `47ffda2`):
- `specs/trading-idea-catalogue.md` v2.7 (entries PS-01, PS-02, A2-41, VW-01, AV-01, AV-02, DV-12, OF-01; §§2.0–2.3, 2.6, 3.10 EX family). Cited as *Cat. <ID>* or *Cat. §x*.
- `docs/research-strategy-header.md` and `sandbox/ResearchStrategyHeader.template.txt` (PR #103): header sections 1–22, §17 parameter block, acceptance policies, sandbox lifecycle.
- `docs/engines-manual.md` v4.2: §1.4 (closed-bar timing), §1.5 (journaling), §3.26 Example 26 (PS-01), §3.27 Example 27 (PS-02), §3.30 Example 38 (OF-01), §4.7 M-AVWAP, §4.8 PRICEACTION, §4.16 / §5.23 M-PORT, §5.10 F-VWAP, §5.12 F-TM, §5.13 F-JRNL / `sizeForRisk`.
- `specs/m-port-spec.md` rev 2.7 and `specs/live-check-sheet.md` (Check 7, verified 2026-11-10).
- `specs/m-instr-spec.md` rev 2.5.13 (§5.3 `InstrumentConfig`, §9 sizing and cost floor = PR-M7, PR-M8 CME product holiday calendars, PR-M9 roll-gap calibrator).
- `final-synthesis.md` (Spec 1/1A, §4.0 common definitions, §6.6 ablation ladder, §6.7 splits). Analyst workspace copy; not in the repo.
- *Strategy-planning phase* Page (8 Nov 2026): build order, testing protocol, costs, acceptance defaults, profit-concentration contract, lifecycle, recorded decisions.

Books and external sources (via the analyst's Kindle notes; page/location as in those notes):
- Keller, Book 4 (empirical volume-profile study, FX/crypto 1-minute): §2.4 "a balance break travels its width just over half the time" (FX 214/378 = 56.6%, median 1.17 W; crypto 59.1%, 1.20 W); §8.3 confirmation vs prediction excursions (median MFE/MAE 10.5 vs 0.70); §8.4 rules (trade with value migration, balance break is the only end-to-end spec, scale at one width and run the rest, skip entropy > 0.93). Notes: `kindle-notes/keller-b4-empirical.md`, `keller-b4-parts-2-7-analyst2.md`.
- Keller, Book 1: card 3.3 ("escape is not entry; accept or retest"); Part IV VWAP setup 1 (trend-day first/second VWAP touch, stop modestly beyond, target new extremes); card 6.3 (stacked imbalances). Notes: `kindle-notes/keller-b1-parts3-4.md`.
- Trader Dale, *Order Flow* e-book: Setup #4 Stacked Imbalances (p. 76: ≥ 300% diagonal, ≥ 3 stacked, price moves away, **first test only**). Notes: `kindle-notes/dale-orderflow-pp13-128.md`.
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
| ES | $17.00 (`InstrumentConfig` default `Future:ES` 1700 cents) | Owner-confirmed 2026-11-01 (Cat. §2.1, v2.6a). **Convention open** (Page): $17.00 = commission + one total tick; per-side tick = $29.50 | 14 |
| MES | $2.50 (`Future:MES` 250) | Owner-confirmed 2026-11-01 | 20 |
| NQ | $9.48 | Provisional (owner list) | 19 |
| MNQ | $1.72 | Provisional | 35 |
| CL | $14.72 | Confirmed (owner list; not provisional) | 15 |
| 6E | $11.17 | Provisional | 18 (= ⌈1117 × 10 / 625⌉, M-INSTR §9 formula) |
| GC / ZN | $15.02 / $18.945 | Provisional; not in batch 1 markets except as later replications | 16 / 13 |

- The non-ES totals already include one tick of slippage (Page). Do not add that tick twice.
- **Cost floor (Cat. §2.1, decided 2026-10-23):** stop distance = max(structural stop, cost floor). The trade is skipped and logged only if the widened stop breaches the per-trade risk budget (1% of equity). Size is never shrunk to fit a wider stop. In code this is M7: set `InstrumentConfig.applyCostFloor = true` in `Create()` for the primary arm and pass the widened stop to `sizeForRisk(…, widenedStop)` / `portfolioArm(…, widenedStop)`. NQ, MNQ, CL and 6E need cost rows added to `InstrumentConfig.costs` (cents, `CostKey(EAssetClass.Future, root)`) before their floor is known. Otherwise M7 refuses with `unknown-field:cost`. **PROPOSED – owner to confirm:** add the provisional rows above as research rows with `costSource` naming them provisional.
- **MultiCharts costs:** Commission = the round trip above minus the included slippage tick; Slippage = 1 tick per side in MC's Strategy Properties. **PROPOSED – owner to confirm** how to split the ES $17.00 between commission and slippage (Page open sign-off).
- **Fills in backtest:** Bar Magnifier on, at 1-minute detail on 5m/15m charts (W-M), tick detail only inside the verified tick window. Label every result with the detail used. Limits fill only on trade-through (Cat. §2.1). Where a bar touches both stop and target and the magnifier cannot resolve the order, evaluation assumes **stop first**: the offline trade-list check re-scores those trades as stop-outs. Report the count.
- **Stresses (Page proposal):** +1 adverse tick per executed side, and separately 2× total cost, on the same trade list. Catalogue §2.1 also requires 2 ticks at the open and around news, plus a 1% tail of 4 R losses. These are offline post-processing on the journal unless MC can model them natively. Say which in the run sheet.

## 5. Test windows (frozen before any performance read)

- **Owner constraint:** tests use data up to 31 Dec 2024 only. Every chart and Portfolio Trader *To* date is **12/31/2024 or earlier**. 2025–2026 is the sealed holdout. Do not view, screenshot or report 2025–2026 prices or results.
- **W-M (1-minute data):** development = first verified full month in Jun/Jul 2016 → 31 Dec 2022, with anchored yearly walk-forward inside it (train 2016–2018 → test 2019; train → 2019, test 2020; → 2021; → 2022). Validation = 1 Jan 2023 → 31 Dec 2024. Holdout = 2025 → last complete session, run once, later, under a separate owner sign-off.
- **W-D (daily):** development 2000–2015, validation 2016–2024 (AV-01's daily filter only).
- **W-T (ticks):** 250/125/125 sessions per the catalogue. **Conflict:** the tick window starts about Jul 2024, so only about 125 sessions fall on or before 31 Dec 2024. See the OF-01 spec.
- **Coverage caveat (Page):** @ES minute history is reported to start 26 Dec 2022 on the MiniPC. The older download is unverified. Until 2016–2022 is present and flushed in QuoteManager, runs are **engineering smoke tests**, not development or acceptance evidence.
- **Trial budget (owner-approved, 8 Nov 2026, 5:56 PM EST):** test the unchanged catalogue host first. Then add only pre-registered ablations, one component at a time. Use at most the catalogue's registered sweep per spec, recorded in a shared trial ledger. Proposal on the Page: at most three research-tuned numeric parameters per new spec. Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, α = 0.05, fixed.

## 6. Acceptance criteria (shared defaults)

Applied separately to validation (2023–2024) now, and to holdout later. Owner-approved items are marked; the rest are Page proposals the owner accepted as defaults ("Defaults accepted; not achieved results").

1. **Catalogue kill text** for the ID, plus Cat. §2.3 standard kill (Gate 3, ≥ 150 development events; 30–149 inconclusive; < 30 no evaluation). Where both speak, the stricter applies.
2. **Net edge:** net PF ≥ 1.20 and positive expectancy after costs.
3. **Profit concentration (owner-approved, 8 Nov 2026):** largest winning trade ≤ `MaxSingleTradeNetProfitPct` (default 10%) of total net profit after costs; *and* net profit stays positive after removing the top `OutlierRemovalPct` (default 1%) of winning trades. Report original and trimmed expectancy. K = max(1, ⌈1% × number of winning trades⌉) — **PROPOSED – owner to confirm** this rounding before any run (the header standard requires it frozen). Trade unit = one parent position including all partial exits.
4. **Big-winner exception (owner, 8 Nov 2026):** only a file whose header predeclares `big-winner-trend` *before* any result skips the 10% cap. It is judged on cost-adjusted expectancy (trader's equation) and must still stay positive after removing the top 1% of winners. In batch 1 all six specs default to `catalogue-default`. AV-01 is flagged as a candidate for `big-winner-trend` (**PROPOSED – owner to confirm before its first run**).
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
- q ≥ 2: T1 qty = ⌊q / 2⌋; runner = q − ⌊q / 2⌋ (the runner gets the odd contract). **PROPOSED – owner to confirm** (the catalogue says "50%" without an odd-lot rule).
- q = 1: the spec's `SingleLotMode` input decides, default `ExitAtT1` = the whole position exits at T1, matching the Page's note that one lot exits entirely at +1 R in `_ExitHalfRiskHalfRoll`. The alternative `RunnerOnly` skips T1 and manages the single lot as the runner. Changing it is a new trial. **PROPOSED – owner to confirm** the default.
- Research default size is 2 contracts on charts, so the scale-out rule is exercised. The Page notes that a genuine half-exit study needs ≥ 2 lots. **PROPOSED – owner to confirm** 2 ES/NQ versus micros.

### 7.3 Stops are one-way
A stop update (break-even, trail) is computed on the bar close. It is sent from the next bar and accepted only if it tightens: long `newStop = max(oldStop, candidate)`. A trail candidate is also capped one tick below the last close for a long, so it cannot be placed through the market. If the candidate would be at or through the market, the strategy sends the market exit instead and logs `TRAIL-THROUGH`.

### 7.4 Same-bar precedence
When several exit conditions fire on the same bar, the order below decides which exit happens and which reason is journaled. Items 1–3 are intrabar fills; items 4–9 are evaluated at the bar close and executed as one `MarketNextBar` exit with the highest-ranked reason.
1. **MM `ForceClosePosition`** (daily-loss flatten or kill). Fills at the **close of the same bar T** (Check 7). The strategy does not fight it: on the next bar it sees itself flat and logs `FLAT-BY-MM`.
2. **Protective stop** (resting from the previous bar), intrabar.
3. **Target limit**, intrabar. If 2 and 3 are both touched in one bar and the magnifier cannot order them, evaluation scores it as stop first (§4).
4. Event flatten (`EventFlattenNow`, only where the spec enables it).
5. Session-end flatten (`FlatBy` / early-close flatten via `mustFlatten()`).
6. Roll flatten (swing positions before a splice).
7. Invalidation (acceptance failure, VWAP loss, zone violation).
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
- **News:** M-EVENT `EventBlackout` blocks new entries from −2 to +10 minutes around tier-1 releases (Cat. §2.1; RG-07). Open positions keep their stop and are **not** flattened by default (`EventFlattenNow` unused) — **PROPOSED – owner to confirm**. Coverage: EV-4.6b leaves 80 GDP/PCE holes in 2016–2024. **PROPOSED – owner to confirm** `EventConfig.allowPartialCoverage = true`, so missing rows do not block 15–18% of days. The journal records `evtCoverage` per entry either way.
- **Rolls:** `RollDetection = ERollDetection.Table` for ES/NQ (PR #116 keys the table roll by the Globex day on calendar-keyed charts; M9's calibrator is log-only and ES detection stays at the literal 8-point gap). Multi-session levels (composites, swing anchors) reset at the splice. The strategy may not enter on a roll-window day (`IsRollWindow`). Intraday strategies are flat every day, so no position crosses a splice. Swing positions (AV-01) are flattened at the close of the last session before the splice (§7.4 item 6), because the continuous series is **unadjusted** and a held position would book the roll gap as fake P&L. CL and 6E roll rules come from their M-INSTR presets; their vendor-splice equivalence is still VERIFY (M9), so CL/6E runs are labelled provisional.
- **Overnight / weekend:** intraday strategies never hold. Only AV-01 (and AV-02's later swing arm) holds overnight and over weekends. Each has its own max hold.

### 7.7 Re-entry and adds
- No adds or pyramiding in batch 1. A same-direction add would be refused by the MM anyway (`in position`).
- No reversals (an opposite signal while in a position is ignored and logged).
- Re-entry after a stop-out follows each spec. The default is **no re-entry on the same setup instance** (one attempt per setup), which matches the Example 26/27 `m_done` behaviour.

## 8. Logging contract (shared)

- `TraceLevel = ETraceLevel.Journal` for research runs. That writes `MCJournal_<runId>.csv` (trades) and `MCEvents_<runId>.csv` (signals).
- **RUN line** (base) carries every `[Input]` value. The strategy adds one Info line at the first `CalcBar`: `<ID> CFG v(<rulesVersion>) policy(<AcceptancePolicy>) maxTradePct(<x>) outlierPct(<y>) costFloor(<on|off>) floorTicks(<n>) roll(<mode>) evtCoverage(<partial|strict>) detail(<1m|tick>)`.
- `journalArm(<order name>, plannedEntry, initialStop(widened), firstTarget, ctx)` next to the entry `Send`, with ctx including the setup's key levels.
- `logSignal(<tag>, dir, price, stop, ctx, taken)` for **every qualifying setup, traded or skipped**. This is the F-EVT event-study and control harness.
- Lines, all `ETraceLevel.Journal`, prefix = catalogue ID:
  - `<ID> SETUP <date> side(<±1>) <setup fields>`
  - `<ID> ARM side() entry() stop() widenedStop() target() qty() R$()`
  - `<ID> SKIP <reason>: <detail>` (reasons are fixed strings per spec)
  - `<ID> T1 qty() px()` · `<ID> BE stop()` · `<ID> TRAIL stop() basis()`
  - `<ID> EXIT reason(<STOP|TARGET|T1+RUNNER|INVALIDATION|TIME|FLATBY|EARLYCLOSE|EVENT|ROLL|FLAT-BY-MM|TRAIL-THROUGH>) px() R()`
- These strings are journal text, not MC API members (§3 rule 5 concerns enums and API members).

## 9. Implementation order for Coder

1. Shared: decide (with the owner) the open items in §10 that block code (cost rows, 1-lot mode, ES cost split). Read the PR #103 header template.
2. PS-02 first (simplest profile host), then PS-01 reusing the same break detector (factor the break/acceptance state machine into a private nested class inside each file, or into one sandbox helper if Coder prefers; no new root engine without owner sign-off).
3. VW-01, AV-02, AV-01.
4. OF-01 only after the tick-corpus question is answered.
5. For each: off-MC unit tests where logic is MC-free (state machine, split rule, precedence), native compile on the MiniPC via one temporary `Compile Include`, chart run (smoke, then development), journal diff of two identical runs, then Portfolio Trader one-row reconciliation.

## 10. Open questions for Ettienne (batch-wide)

1. ES round-trip convention: keep $17.00 (one total tick), or $29.50 (one tick per side)? And how to split it into MC Commission and Slippage.
2. Add the provisional NQ/MNQ/6E costs (and confirmed CL) to `InstrumentConfig.costs`, so the M7 cost floor works on those roots?
3. Research size: 2 ES/NQ contracts on charts (so scale-outs are real), or micros?
4. Odd-lot split (runner gets the extra contract) and 1-contract default `ExitAtT1`.
5. Top-1% trim rounding: K = max(1, ⌈1% × winning trades⌉)?
6. Entry cut-off 60 min before an early close, flatten 5 min before.
7. News: hold open positions through tier-1 releases (no event flatten)? And allow partial event-calendar coverage?
8. AV-01: predeclare `big-winner-trend` before its first run?
9. OF-01: how to test under the ≤ 2024 rule when only about 125 tick sessions exist before 2025 (see that spec).
10. Older @ES/@NQ minute history (2016–2022): is it downloaded and flushed? Until then everything is a smoke test.
