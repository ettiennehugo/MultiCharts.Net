# Base-class refactor / refinement spec: `_CustomSignal`, `_VolumeProfileBase`, `_VolumeProfileFuturesBase`

## Changelog
- **rev 2 (2026-10-19 21:45 ET)** — user + implementer feedback.
  - Removed **CS-2** (cost/slippage model), **VPF-1** (contract specs / IBKR costs) and the cost floor (`passesCostFloor`, `maxCostR`, `costInR`) — MC .NET does these natively (Strategy Properties commission rules / slippage; `Bars.Info.BigPointValue/MinMove/PriceScale`) [PG p17, p21, p45, p53; UM p282–286, p325, p389].
  - **CS-1 narrowed**: journal only what MC lacks (initial stop → R, MFE/MAE in R/ticks, context, planned-vs-fill). Exit reason, bars held, $ run-up/drawdown, commission and P&L are read from `Positions[]`/`ITrade`/`ITradeOrder` [PG p43–46], not re-tracked.
  - **Narrowed with citation**: CS-6 (holiday list → MC Holiday Lists), CS-7 (flat-at-session-end → `ExitOnClose`), VPB-2 (MC VA % is a chart setting), VPF-4 (rollover/back-adjust → MC Custom Futures), VPF-5 (bid/ask/delta come from MC), VPF-6 (table → MC Holiday List / session template preferred; Sunday bug fix stays).
  - Implementer notes: VPB-9 moved straight after CS-0 (PR #3), if helper files compile; VPB-07/VPB-21 settled (`BarsInterval.First` confirmed, VPB-21 fallback removed in PR #2) so VPB-3 is now a regression audit; VPB-1 source trade-off stated; every new `[Input]` on `_CustomSignal` listed as a user decision (§5, D7); VPF-6 Sunday bug confirmed (Sun 25 Dec 2022 → CME closed Mon 26 Dec).
  - `_CustomSignal` derives from **`PortfolioSignalObject`** (`CS:17`), not `SignalObject`. Neither PDF guide documents `PortfolioSignalObject` (**unverified** as a documented API; it compiles in the repo and the code uses `this.Portfolio.MaxRiskEquityPerPosPercent`, `CS:441`). The documented `Portfolio` (IPortfolioPerformance) property is on signals generally [PG p17; wiki `Advanced._Portfolio`].
- rev 1 (2026-10-19) — initial spec.

**Citation keys.** [PG] = *MultiCharts .NET Programming Guide v1.0* (attachment `01M5BF386FDT…pdf`), PDF page numbers. [UM] = *MultiCharts User Manual* (`01M5BF385QBT…pdf`), PDF page numbers. [PT] = *Portfolio Trader Manual* (`01M5BF39CB6Q…pdf`). Web: multicharts.com/features/trading-system-analysis/ (Performance Report: List of Trades, MAE/MFE graphs); multicharts.com/discussion/viewtopic.php?t=12989 (MAE graph values are in List of Trades); multicharts.com/trading-software/index.php?title=Advanced._Portfolio.

## 0a. What MultiCharts .NET already provides (do not rebuild)
| Need | Native | Cite |
|---|---|---|
| Commission | Strategy Properties → Commission Rule (Manage Rules); readable as `Commission` property; per-trade `ITrade.CommissionValue` | UM p282–290; PG p17, p45–46, p53 |
| Slippage | Strategy Properties → Slippage, per contract or per trade, entry+exit; readable as `Slippage` | UM p325, p440; PG p17, p45, p53 |
| Tick size / point value / category / session | `Bars.Info.MinMove`, `PriceScale` (tick = MinMove·PriceScale), `BigPointValue`, `Category` (ESymbolCategory), `SessionName` | PG p21, p40–41; UM p389, p395 |
| Position state | `StrategyInfo.MarketPosition/AvgEntryPrice/OpenEquity/ClosedEquity`; `CurrentPosition.Profit/MaxRunUp/MaxDrawDown/OpenTrades/ClosedTrades` | PG p43, p67 |
| Position history, $ run-up/drawdown per position | `Positions[]` (IMarketPosition): `Side`, `Profit`, `ProfitPerContract`, `MaxRunUp`, `MaxDrawDown`, `ClosedTrades[]` | PG p44–45 |
| Per-trade entry/exit | `ITrade.EntryOrder/ExitOrder/IsLong/IsOpen/Profit/CommissionValue`; `ITradeOrder.Name/Price/Time/BarNumber/Contracts/Category/Action` → exit reason by order name, bars held by BarNumber diff | PG p45–46 |
| Per-trade MFE/MAE on `ITrade` | **not documented** (only on IMarketPosition, in $) — **unverified** | PG p44–46 |
| Report stats | Strategy Performance Report: List of Trades, Run-up & Drawdown, MAE/MFE graphs, 200+ metrics, Monte-Carlo; strategy-level NetProfit, MaxDrawDown, NumWinTrades… in code | PG p45–46; UM p449; web features/trading-system-analysis, forum t=12989 |
| Built-in exits | `GenerateStopLoss/ProfitTarget/BreakEven/DollarTrailing/PercentTrailing/ExitOnClose` | PG p40–42 |
| Sessions / holidays | Session settings & templates per symbol; Holiday Lists (Tools → Holidays) assigned to symbols | UM p389–391, p403, p490–491 |
| Rollover / continuous | Custom Futures: rollover by date/volume/OI, Absolute-difference or Ratio back-adjustment | UM p405–408, p458 |
| VP value area | VP settings: Value Area % adjustable | UM p32–36 |
| Bid/ask volume, delta | VP Bid×Ask / Delta display; buy/sell assignment by uptick/downtick or by bid/ask (requests historical tick bid/ask) | UM p35, p158–159 |
| Portfolio money mgmt | `Portfolio` (IPortfolioPerformance), `SetMaxPotentialLossPerContract`, Portfolio Properties margin/exposure | PG p17, p58–62; wiki Advanced._Portfolio |

Not found in the guides (**unverified**, do not rely on until the Coder checks PowerLanguage .NET Help in the editor): code access to the chart VP Value Area %, to the symbol's Holiday List entries, to the Custom Futures rollover dates, and any `ITrade`-level MFE/MAE.

Source: github.com/ettiennehugo/MultiCharts.Net `main` @ e4c7307 (rev 1 note said `/workspace/mc-strategies` is byte-identical to the clone; as of rev 2 the clone's `_CustomSignal` additionally carries the CS-0 `HELPER_FILE_PROBE` line — PR #3).
Author: analyst review, 2026-10-19 (ET). Audience: Coder. **No repo changes were made.**
Scope rules: the 72 issues in `reviews/volume-profile-*.md` are not re-reported. Items still open there are referenced by their ID
(CS-20, CS-21, VPB-07/21, VPB-22, VPB-23, VPF-11). Trapped-trader setups are out of scope.

Line numbers refer to e4c7307. `CS` = `_CustomSignal.Strategy.CS`, `VPB` = `_VolumeProfileBase.Strategy.CS`, `VPF` = `_VolumeProfileFuturesBase.Strategy.CS`.

---

## 0. Ground rules for every work item

1. **Additive first.** 14 non-VP strategies derive from `_CustomSignal` (`_BreakoutBase`, `_BreakoutAutoBase`, `_EpisodicPivot*`, `_ParabolicShortSESX`,
   `_SuperSmoother*`, `_AdaptiveSuperSmoother*`, `_TimeRangeSignal`, `_TrendLine`, `_TradeManagerTest`). They call `equityAvailable` (55×), `Intraday*` (95×),
   `recentLow/High` (44×), `equityBasedPositionSize` (19×), `stopLossBasedPositionSize` (16×), `riskBasedPositionSize` (14×), `volumeBasedPositionSize` (12×),
   `tradingDayCount` (12×), `orderEntryTime` (9×). **Do not change the signature or default behaviour of any of these.** New behaviour goes behind new methods
   or new inputs whose defaults reproduce today's results.
2. VP derived strategies: `_DeltaDivRevFuturesLELX` is the only real consumer (uses `riskBasedPositionSize`, `capByNotional`, `equityAvailable`,
   `GetConfluenceTargets`, `NakedPocs`, `RecentCompletedProfiles`, `ExtraVolumeProfileForBar`, `VpExtra.poc/hvns/pocComplete`, `GlobexDate`, `GetGlobexState`,
   `InSession`). `_DeltaDivRevFuturesLELX2` uses `stopLossBasedPositionSize`, `capByNotional`, `IsRth`. The `_POCPullback*`, `_StackedImblCont*`,
   `_AbsorptionRev*`, `_DeltaDivRevFuturesSESX` files are stubs (override Create/StartCalc/CalcBar only) and are unaffected unless stated.
3. Every PR states whether it is a **NO-CHANGE** (identical trades on a fixed backtest), **BACKTEST CHANGE** or **LIVE CHANGE**, and for NO-CHANGE PRs
   attaches a before/after trade list of LELX on one fixed ES/MES chart (same as previous PRs).
4. C# 7.3, no new NuGet dependencies (MC .NET editor constraint). New helper types may live in `*.Function.CS` files as plain classes — **verify first** that the MC
   editor compiles a non-study helper class in its own file (PR CS-0 below); if not, keep them as nested classes.
5. Inputs: any new `[Input]` must have a default that keeps current behaviour, and is validated once in StartCalc.

---

## 1. Summary of findings per class

### 1.1 `_CustomSignal` — general base (all asset classes)
**Keep:** trace/logging (CSV, thread-safe), intraday OHLC tracking keyed by `tradingDayKey`, sizing helpers, TradeManager snapshot, `recentLow/High`, entry/exit time lookups.

**Missing for the stated goal (signal testing for profitability):**
- No per-trade journal of what MC does **not** record: initial stop → initial risk and R-multiple, MFE/MAE in R/ticks, signal tag/context, planned-vs-fill price. (Entry/exit, P&L, commission, $ run-up/drawdown, exit order name and bar numbers are native — §0a.)
- (rev 2) Costs: MC's commission/slippage are readable via `Commission`/`Slippage` and `ITrade.CommissionValue` [PG p17, p45–46] — the rev 1 claim "invisible to the code" was wrong. No own cost model.
- No signal-event logging independent of trades (skipped signals, forward MFE/MAE horizons, controls such as random entries) — needed for the event-study step of `final-synthesis.md §6.4`.
- No generic session / time-window or trade-management primitives (flat-by time, no entries after, max trades/day, daily loss stop, time stop). Derived strategies re-implement these (e.g. `OneTradePerGlobexDay` in LELX).
- Calendar is hard-wired to NYSE (MC Holiday Lists exist per symbol [UM p389–391] but code access is unverified) (`CS:238, 273`) and the trading day defaults to the calendar date (`CS:399`) — wrong for forex (17:00 New York roll) and crypto (UTC, 7 days).
- No input snapshot per run (parameter hygiene / trial counting).

**Bugs / risks (new, not in prior reviews):**
- `CS:898–924` `orderExitTime` (real time) selects *any* filled opposite-side order for the symbol in the last 63 days — no order-name filter, no "after entry" bound. With a flat→re-entry it can return the exit of an earlier trade, or a scale-out of another strategy on the same symbol.
- `CS:821, 847` order matching uses `Name.Contains(name)`: entry `"LE"` also matches `"LE2"`, `"LEDiv"` etc. Needs an exact/prefix option.
- `CS:291–294` intraday times seeded with `DateTime.Now` (the review fixed `m_currentDay` only). Harmless today because the first bar overwrites them, but `recentLow(…, includeToday:false)` paths and any trace before the first bar print wall-clock time. Use `DateTime.MinValue`.
- `CS:111` the log line stamps `Bars.Time[0]` with `zzz` = the PC's offset, not the bar's zone; misleading for Exchange/GMT charts. Log an explicit zone label or drop `zzz`.
- `CS:66` log file name has minute resolution and the singleton is process-wide: all optimisation passes / charts started in the same minute share one file with no run id column. Add a run id (see CS-3).
- `CS:436, 449` `equityAvailable` uses `InitialCapital + NetProfit` (closed P&L only). Fine for one position at a time; wrong for pyramiding/scale-in. Document, and add an `includeOpenPnl` overload (CS-4).
- `CS:631–643` `averageTrueRange` loops `length` bars each call and reads `Close[i+1]`; needs MaxBarsBack ≥ length+1 (guard is `CurrentBar < length`). Low priority.
- LELX comment `_DeltaDivRevFuturesLELX:476–477` says `stopLossBasedPositionSize` "treats the raw price distance as dollars". That was fixed (`CS:585–595` multiplies by BigPointValue). Comment is stale; the two derived strategies now size identically via different paths.

### 1.2 `_VolumeProfileBase` — VP analytics
**Keep:** stream packs + extras keyed by start bar, read-only closed profiles (VPB-28), naked-POC tracking with tolerance, per-stream level percentiles, HVN/LVN detection on a dense 1-tick grid, VWAP engine (3 slots), confluence engine, input validation helpers.

**Most important finding — backtest/live divergence of the developing profile:**
- `VPB:3070–3078`: in a backtest the developing profile's histogram is cleared (`clearHistogram`) because MC builds the whole window ahead of the calc bar (correct lookahead fix, VPB-03). In **real time** the same code path fills `levels`/`hvns`/`lvns` from the live profile. Result: any strategy that reads developing HVN/LVN, `levels`, `pocVol` or exceptional-volume percentiles on the developing profile sees **empty data in backtests and populated data live**. A strategy can never be validated on those features. Developing POC/VAH/VAL come from MC's `POCForBar/HighVAForBar/LowVAForBar`; as-of correctness is settled (VPB-07: `BarsInterval.First` confirmed by implementer).
- *(rev 2: implementer reports the VPB-21 fallback was removed in PR #2 — re-check whether this line still exists before doing VPB-4.)* `VPB:3524`: the POC fallback chain includes `priceForBar(vp, startChart, 0)` **unconditionally** (VAH/VAL do not have this term). When the as-of probes for `bar` and `bar-1` fail on a developing profile, POC silently becomes the POC *as of the profile's first bar* — a stale value with no flag. Should return 0 / mark stale.
- `VPB:3150`: if MC returns no POC, the POC falls back to the peak of the **smoothed** histogram (3-tick window), which can differ from the raw max row by a tick; inconsistent with MC's POC.

**Missing for the stated goal:**
- Value area %: configurable natively in the chart's VP settings [UM p32–36]; but not readable from code (unverified) and not available for the Own developing profile. Only the Own-profile VA is missing.
- Own developing-profile accumulator (needed for parity and for RTH-only profiles).
- Composites / multi-session balance (explicitly not built, `VPB:757`).
- Acceptance outside value, value migration score, poor highs/lows, single prints, profile shape, naked VAH/VAL, value relationship (higher/lower/overlapping).
- A truncation (no-lookahead) self-test (`final-synthesis.md §6.3.6`).

**Risks / hygiene:**
- `VPB:2351–2360` `pocComplete` is only stamped when `TrackNakedPoc = true` (open decision D4).
- `VPB:1669, 1686, 1695` `ConfluenceMinHits`, `ConfluenceSameStreamDecay`, `ConfluenceCrossStreamBonus` are validated and stored but never read (open decision D3).
- `VPB:1550–1551` docs say "Percentile of POC"; `fillNodes` uses them as percentiles of the traded-row distribution. Fix docs.
- `VPB:2192` `CurrentVwapSigma1` vs `CurrentVwap2Sigma`/`CurrentVwap3Sigma` naming inconsistent (add `CurrentVwap1Sigma`, keep old as `[Obsolete]` alias).
- `VPB:31` `DEFAULT_MAX_LVN_PCT_OF_RANGE` marked "TBD": make it a property, document.
- File is 3,720 lines (VPB-22 deferred) — the split in VPB-9 is a prerequisite for the new engines to stay reviewable.

### 1.3 `_VolumeProfileFuturesBase` — futures specifics
**Keep:** chart-zone → ET conversion, Globex date / state, session flags, `TrySessionBounds`, holiday hooks, per-bar cache.

**Missing:** IB / RTH / overnight session statistics, RTH-only profiles, order-flow pattern helpers (imbalances, stacked, unfinished auctions, absorption) on top of MC's bid/ask data. *Not missing (native, rev 2):* contract specs [PG p21], costs [UM p282–290, p325], rollover/back-adjust [UM p405–408], holiday/session calendars [UM p389–391, p490].

**Bugs / risks:**
- **Confirmed by implementer.** `VPF:613–619` `IsGlobexClosedDay` hard-codes Jan 1 and Dec 25 by calendar date. When they fall on a Sunday (Dec 25 2022, Jan 1 2023) CME is closed the following **Monday**; the default treats Monday as open (and Sunday is already closed), so the observed holiday is missed.
- `VPF:34–35` RTH default 09:30–16:15 is the futures "day session" convention, but `final-synthesis.md §4.0` uses RTH 09:30–16:00 for value logic. Session VPs built on 16:15 include the 16:00–16:15 settlement flow. Needs a separate "cash RTH" end or a configurable value-session end (see VPF-2).
- `VPF:557` `tradingDayKey = GlobexDate`, so `_CustomSignal.IntradayHigh/Low/Open` are **Globex-day** values (18:00→17:00), not RTH. Correct, but undocumented; VP strategies wanting RTH high/low/IB have nothing (VPF-2).
- Session logic lives below `_VolumeProfileBase`, so a non-VP futures strategy cannot use it (VPF-11 deferred). VPF-7 resolves this.
- `VPF:387, 406…` `PrimarySession/TimeSinceOpen/...` are `public` while `GetSessions/InSession` are `protected`; harmless, tidy when extracting.

---

## 2. What moves between classes

| From | To | What | Why |
|---|---|---|---|
| `VPF` (resolveChartTz, findTz, exchangeTimeZoneId, ToEastern, InWindow) | `_CustomSignal` (generic `ToZone(barTime, tzId)`, `BarTimeZone`) | Chart time-zone resolution | Every asset class needs session windows (forex London/NY, crypto UTC). Keep `ToEastern` in VPF as a thin wrapper. |
| `VPF` session clock (globexStateEt, sessionsEt, TrySessionBounds, holiday hooks) | New helper class `CmeSessionClock` (owned by VPF, composable) | CME session logic | VPF-11. Lets non-VP futures strategies use it without the VP machinery. VPF keeps all current protected methods as one-line delegates. |
| `VPB` (requireUnitInterval, requireAtLeast, requireNonNegative, requireDataStream) | `_CustomSignal` (protected) | Input validation helpers | Parameter hygiene is generic. |
| Derived (`OneTradePerGlobexDay`, session gating in LELX) | `_CustomSignal` trade-management primitives keyed on `tradingDayKey` | Daily limits | Avoid re-implementation per strategy (derived code is not forced to migrate). |
| `VPB` `tickSize()` | `_CustomSignal` | Tick resolution = `Bars.Info.MinMove × PriceScale` [PG p21, p41] | Journal (R/MFE in ticks). Thin wrapper over native fields; VPB keeps its call. |

Nothing moves *down* into futures except order-flow pattern helpers (new).

---

## 3. Work items (PR-sized)

Priority: **P0** = blocks honest validation, **P1** = needed before the first research run, **P2** = needed for the full VP spec set, **P3** = hygiene.
Dependencies are listed; anything without one can be done in parallel.

### `_CustomSignal`

#### CS-0 (P0, NO-CHANGE) — Verify helper-file compilation
- Add one trivial `internal sealed class` in a new `_SignalJournal.Function.CS` (or confirm it must be nested) and compile in the MC editor.
- **Accept:** compiles in MC, PLStudies.csproj updated; decision recorded in the PR description. Gates the file layout of CS-1, VPB-1, VPB-9, VPF-7. Status: open as **PR #3** (probe line in clone `CS:20–22`).

#### CS-1 (P0, NO-CHANGE for existing strategies) — Per-trade journal, **gap-only** (rev 2 narrowed)
- `TradeJournal` component, off by default. **Input decision D7-a:** `JournalEnabled` `[Input]` (appears on all 14 derived strategies) vs a protected property a derived strategy sets in StartCalc (no new input — recommended).
- API: `journalArm(string tag, double plannedEntry, double initialStop, double target, IDictionary<string,string> context)` at order send, keyed by entry **order name**.
- Read from MC, do not re-track: entry/exit time & price, qty, exit order name (= exit reason), bars held (`ExitOrder.BarNumber − EntryOrder.BarNumber`), P&L, commission (`ITrade.*`, `ITradeOrder.*` [PG p45–46]); $ run-up / drawdown of the position (`Positions[1].MaxRunUp/MaxDrawDown` when the position closes [PG p44]); slippage setting (`Slippage` [PG p45]).
- Compute only what MC lacks: initial risk ($, ticks) from the armed stop, **R-multiple** (gross = `ITrade.Profit`/risk; net uses MC's own commission+slippage — no own cost model), MFE/MAE expressed in **R and ticks** (from the native $ values ÷ (BigPointValue × qty); per-`ITrade` MFE/MAE for scale-outs is **unverified** — if absent, record per-position only and say so), planned-vs-fill price difference, tag, context, run id.
- Output: `MCJournal_<runId>.csv`, invariant culture. The Performance Report remains the source for aggregate stats and MAE/MFE graphs (web: features/trading-system-analysis).
- **Accept:** row count = MC trade count; Σ net P&L = MC NetProfit exactly (same source); R and tick conversions hand-checked on 3 trades; with one position at a time MFE/MAE match the List of Trades run-up/drawdown.

#### ~~CS-2~~ — removed (rev 2)
Cost/slippage model and cost floor dropped: MC Strategy Properties commission rules and slippage [UM p282–290, p325; PG p53], readable as `Commission`/`Slippage`/`ITrade.CommissionValue` [PG p17, p45–46]. If a pre-trade cost filter is wanted later it must read those native values (open question Q1).

#### CS-3 (P1, NO-CHANGE) — Run id and input snapshot
- At first CalcBar log one `RUN` line: run id (GUID short), strategy type, symbol, resolution, data range, all `[Input]` values (reflection over properties with `InputAttribute`), and a `BuildTag` constant (Coder bumps manually / git hash string).
- Add `runId` column to trace and journal; move VPB validation helpers here (table §2).
- **Accept:** two optimisation passes produce distinguishable rows; RUN line lists every input of LELX.

#### CS-4 (P1, NO-CHANGE) — Sizing API consolidation (resolves CS-21 without breaking callers)
- New `int sizeForRisk(IInstrument bars, double entry, double stop, double riskFraction, SizingLimits limits)`; `SizingLimits { int maxQty; double maxNotionalFraction; double unitNotional; double minStopTicks; bool roundLots; }` (rev 2: `maxCostR` removed). BPV/tick from `Bars.Info` [PG p21]. Returns 0 with a traced reason (min stop, notional cap, zero equity).
- Explicit "fraction" naming (`riskFraction` ∈ (0,1]); warn once if a caller passes > 1 to the old `*Percent` parameters (no behaviour change).
- `equityAvailable(fraction, includeOpenPnl=false)` overload.
- Fix stale comment in `_DeltaDivRevFuturesLELX:476–477`.
- **Accept:** for 20 random (entry, stop, equity, fraction) cases, `sizeForRisk` with no limits equals `stopLossBasedPositionSize`; existing methods unchanged byte-for-byte.
- **Impact:** none; LELX/LELX2 can migrate later.

#### CS-5 (P1, LIVE CHANGE only) — `orderExitTime`/`orderEntryTime` matching
- Add overloads `orderExitTime(side, useFill, DateTime notBefore, params string[] exitOrderNames)` and `matchMode` (Contains | Exact | Prefix) for entry names. Real-time exit query filters `FinalDT >= notBefore` (default: the current position's entry time) and, when names are given, by name.
- Keep the existing signatures delegating with today's behaviour.
- **Accept:** simulated order list with two round trips returns the second trade's exit; `"LE"` with Exact no longer matches `"LE2"`.
- **Impact:** only callers that opt in (1 caller of `orderExitTime` today).

#### CS-6 (P2, NO-CHANGE) — Trading-day roll for forex/crypto (rev 2 narrowed)
- Asset class from `Bars.Info.Category` [PG p21] — **no new input** unless the user wants an override (D7-b). Virtual `tradingDayKey` presets: Stock = date; Forex = date of (NY time + 7h) i.e. 17:00 NY roll; Crypto = UTC date; Futures overridden by VPF.
- Holidays: MC Holiday Lists per symbol [UM p389–391] already drop holiday bars from charts; code access to the list is **unverified**. Keep only a virtual `holidayCalendar()` hook defaulting to `_NyseHolidays` (unchanged) for `tradingDayCount`; no new calendars unless Q2 says code-side is needed.
- Generic session helpers moved from VPF (table §2): `ToZone`, `inTimeWindow(TimeSpan start, TimeSpan end, string tzId)`.
- **Accept:** Auto on a stock chart reproduces current `Intraday*` values exactly (NO-CHANGE); a forex unit test shows the day rolling at 17:00 NY across DST.

#### CS-7 (P2, NO-CHANGE) — Trade-management primitives
- Native first: flat at session end = `GenerateExitOnClose`, $ stop/target/trailing = built-in exits [PG p40–42]. Not native (no citation found): entry time windows, max trades/day, daily loss stop, time stop in bars.
- Settings as protected properties set by the derived strategy (**not** `[Input]`s on the base — D7-c): `NoEntryBefore`, `NoEntryAfter`, `FlatBy` (only for intra-session times; session end → ExitOnClose), `MaxTradesPerDay`, `DailyLossStopR` / `DailyLossStopCount`, `TimeStopBars`.
- API: `bool entryAllowed(out string reason)`, `bool mustFlatten()`, counters keyed on `tradingDayKey`. Base does **not** send orders; strategy acts on them.
- **Accept:** synthetic day with 3 losing trades blocks the third entry with `DailyLossStopCount=2`; reasons logged.

#### CS-8 (P2, NO-CHANGE) — Signal-event logging and controls
- `logSignal(string tag, int direction, double price, double stop, IDictionary<string,string> context, bool taken)`: base tracks forward MFE/MAE in R at configurable horizons (default 5/15/30/60 bars) and writes an `MCEvents_<runId>.csv` row when the last horizon completes (or session ends).
- Control hooks (default off; protected property or input — D7-d): `ControlMode` (None | RandomEntry with seed | ShiftLevels ±k ticks | DelayEntryBars ±1) exposed as `controlAdjust…` helpers the strategy can apply. This is the event-study/placebo layer of synthesis §6.4–6.5.
- **Accept:** with `taken=false` events, forward MFE/MAE match a manual calculation on synthetic bars; RandomEntry with a fixed seed is reproducible.

#### CS-9 (P3, NO-CHANGE) — Hygiene
- `CS:291–294` seed times with `DateTime.MinValue`; `CS:111` log the bar zone label instead of PC `zzz`; `CS:66` include seconds + run id in file name; `averageTrueRange` guard `CurrentBar <= length` + note MaxBarsBack ≥ length+1; doc that `IntradayClose/Volume` exclude the forming bar.
- **Accept:** trace output diff only.

### `_VolumeProfileBase`

#### VPB-1 (P0, BACKTEST CHANGE only when enabled) — Own developing-profile accumulator (parity + no lookahead)
- New `DevelopingProfile` engine: volume-at-price on a 1-tick grid accumulated **bar by bar up to the calc bar** from a configurable source stream (`DevProfileDataStream`, default 1). Source modes: `VolumeAtPrice` (when the source is a 1-tick/tick-volume stream — exact), `BarUniform` (spread bar volume uniformly across the bar's ticks — approximate, flagged), `BarTypical` (all volume at the close/typical price). Reset on a virtual `isNewProfileWindow()` (default: `IsNewVolumeProfile(stream)`; VPF overrides with Globex/RTH day). Optional bar filter `virtual bool includeBarInProfile(DateTime barTime)` (VPF uses it for RTH-only).
- Produces POC, VAH/VAL (VPB-2 method), HVN/LVN (reuse `fillNodes` math on its levels), `levels`, flag `approximate`.
- Exposed as `DevelopingExtra(int stream)` returning a `VpExtra` with `isDeveloping = true` (new field), and identical in backtest and real time by construction.
- Inputs (on `_VolumeProfileBase` only, so VP strategies only): `DevelopingSource` (MultiCharts | Own; default MultiCharts → NO-CHANGE) and `DevProfileSourceMode` (VolumeAtPrice | BarUniform | BarTypical) + `DevProfileDataStream`.
- **Source trade-off (implementer):** ≥ 99% POC match with MC needs a 1-tick (or tick-volume) source stream, which is heavy in backtests (data load, calc time). `BarUniform` and `BarTypical` are approximations — cheaper, flagged `approximate = true`, and must not be validated against the 99% criterion. The source is configurable; recommended: 1-tick for validation runs, an approximation only for exploratory sweeps (D6).
- On profile close, the developing snapshot of the last bar is compared against MC's completed POC/VA (trace mismatch count) as a parity check (VPB-07 itself is settled).
- **Accept:** (a) backtest vs bar-replay (Data Playback) of one session give identical developing POC/VA/HVN per bar; (b) truncation test VPB-3 passes; (c) on a 1-tick source only, completed POC equals MC's completed POC on ≥ 99% of sessions (differences traced); approximate modes report the match rate without a threshold.
- **Impact:** none by default. LELX can opt in once validated.

#### VPB-2 (P2, NO-CHANGE by default) — Value area for the **Own** profile only (rev 2 narrowed, P1→P2)
- MC's VP Value Area % is a native chart setting [UM p32–36]; keep `MultiCharts` as the path for MC profiles. The methods below apply only to VPB-1 profiles and composites (VPB-7), where MC computes no VA.
- `EValueAreaMethod { MultiCharts, VolumeExpansion, Sigma }`, `ValueAreaPercent` (default 0.70), `ValueAreaSigma` (default 1.0). `VolumeExpansion` = standard two-row expansion from POC (compare the next two rows above vs below, add the larger pair; ties → side nearer the POC/both). `Sigma` = volume-weighted mean ± k·σ of the histogram.
- Computed from `levels` (completed profiles) or the VPB-1 engine (developing). `MultiCharts` keeps today's path.
- Log which method in the RUN line; reading the MC chart VA % from code is **unverified** — if not exposed, log "MC VA % not readable — check chart".
- **Accept:** hand-computed 70% VA on 3 synthetic histograms (incl. ties, POC at edge); on real completed profiles `VolumeExpansion` matches MC VA (when MC is set to 70%) within 1 tick on ≥ 95% of sessions.

#### VPB-3 (P0, NO-CHANGE) — No-lookahead regression audit (rev 2: VPB-07/VPB-21 already settled)
- Debug input `LookaheadAudit` (default false, VP base only). Purpose is now a **regression guard** for the settled fixes (`BarsInterval.First`, PR #2 fallback removal) and the primary validation of the Own path. At every closed calc bar recompute developing POC/VAH/VAL (both MC path and VPB-1 path) from scratch over bars ≤ current and compare with the streaming values; also assert no extra has `startBar > current` and no completed profile is used before `endBar < current`. Plant-a-leak test: a debug flag that deliberately reads `endBar` POC must be caught.
- **Accept:** 0 mismatches on a 20-session ES run for the Own path; MC path also 0 (any mismatch = regression of VPB-07/VPB-21, file a bug).

#### VPB-4 (P1, BACKTEST CHANGE) — Stale POC fallback
- `VPB:3524` include `priceForBar(vp, startChart, 0)` only when `whole`; when the developing POC cannot be read, keep 0 and set `extra.pocStale = true` (new field) instead of returning the first-bar POC. `VPB:3150` use the raw-volume max row for the fallback POC, not the smoothed one.
- **Accept:** trace count of "developing POC unreadable" events; LELX trade list diff reviewed (expected: few or none).
- **Impact:** LELX only if it hit the fallback; noted as BACKTEST CHANGE.

#### VPB-5 (P1, decision D4) — Decouple `pocComplete` from `TrackNakedPoc`
- Recommended: always stamp `pocComplete` (`endBar >= startBar && endBar < current && poc > 0`) at `VPB:2351` before the `TrackNakedPoc` early return; `TrackNakedPoc` only gates the touch scan (`pocNaked/pocTouchedBar`).
- **Accept:** with `TrackNakedPoc=false`, `RecentCompletedProfiles` returns completed profiles and `NakedPocs` returns empty.
- **Impact:** BACKTEST CHANGE for LELX runs with `TrackNakedPoc=false` (it gains completed-profile targets). No change with the default `true`.

#### VPB-6 (P1, NO-CHANGE) — Completed-profile analytics
New read-only methods on `VpExtra` / base, computed from **completed** profiles only (developing variants via VPB-1 when explicitly requested):
- `ValueRelationship(prev, cur)` → Higher | Lower | OverlappingHigher | OverlappingLower | Inside | Outside (Dalton).
- `ValueMigrationScore(stream, n=3, minChangeFraction of ATR20d=0.10)` exactly per synthesis §4.0 (V ∈ [−4,+4], developing term optional).
- `IsPoorHigh/IsPoorLow` (top/bottom row volume ≥ x% of the profile's mean row volume and ≥ k rows of similar volume, default x=0.5, k=2) and `HasUnfinishedAuction(high|low)` when bid/ask both > 0 on the extreme row (only if ask/bid data present).
- `SinglePrints(minTicks)` (contiguous rows with volume ≤ p-percentile within value-area-to-extreme span).
- `ProfileShape` (P, b, D, Thin/Trend, Double) with documented thresholds.
- `NakedVah/NakedVal` analogous to naked POC (reuse the scan with VA levels).
- `AcceptanceOutsideValue(ref profile, side, minBarsOrMinutes, minCloses)` evaluated on bars after the reference profile closed; returns first acceptance bar.
- **Accept:** each function has a synthetic-histogram test with hand-computed results; none reads bars beyond the calc bar (VPB-3 audit extended).

#### VPB-7 (P2, NO-CHANGE) — Composites and multi-session balance
- `Composite(stream, nCompletedProfiles)` merges `levels` of the last N completed extras onto one tick grid → POC/VA/HVN/LVN (cached per close). Only completed profiles.
- `IsBalanced(stream, n, minVaOverlap=0.6)` and `BalanceRange` (union/intersection of VAs) for "accepted balance break" logic (synthesis Spec 1).
- **Accept:** composite of 1 profile equals that profile; composite of N equals a manual merge; never contains the developing profile.

#### VPB-8 (P2, NO-CHANGE) — Multi-timeframe safety
- For HTF streams used in confluence / composites, add `closedBarsOnly` (default true for new APIs) so an HTF profile/bar is used only after its bar closed (synthesis §6.3.3). Verify what `streamItemBarIndex` returns for Data2 on a Data1 tick (forming HTF bar) and document it.
- **Accept:** trace on a 1-min Data1 / 15-min Data2 chart shows HTF state at 10:07 comes from the 10:00 close.

#### VPB-9 (P0 sequencing, NO-CHANGE) — Split the file — **do right after CS-0 (PR #3) if helper files compile**; otherwise nested classes, same order (VPB-22) and de-duplicate slots (VPB-23)
- Move `VwapEngine`, `ConfluenceEngine`, histogram math (`fillNodes`, `smooth`, `percentile`, dense rows) and the new `DevelopingProfile` into helper files (per CS-0 outcome). Public/protected API unchanged.
- **Accept:** identical LELX trade list; base file < 1,500 lines.

#### VPB-10 (P3) — Hygiene / decision D3
- Docs for `HvnPercentile/LvnPercentile` (`VPB:1550–1551`); `CurrentVwap1Sigma` alias (`VPB:2192`); make `DEFAULT_MAX_LVN_PCT_OF_RANGE` a property (`VPB:31`); D3: delete or wire the three unused confluence properties (`VPB:1669, 1686, 1695`). Recommended: **mark `[Obsolete]` now, delete in VPB-9** (no derived strategy references them; verify with `rg`).

### `_VolumeProfileFuturesBase`

#### ~~VPF-1~~ — removed (rev 2)
Contract specs and IBKR cost table dropped: tick/point value from `Bars.Info.MinMove/PriceScale/BigPointValue` [PG p21; UM p389, p395]; fees via an MC Commission Rule matching the user's IBKR rates (MES/MNQ $1.22, ES/NQ $4.48 round trip) [UM p285–290]; slippage via Strategy Properties [UM p325]. Margin for D1 → Portfolio Properties "Margin per Contract %" [wiki Advanced._Portfolio; PG p58–62] (applies in Portfolio Trader).

#### VPF-2 (P1, NO-CHANGE) — Session statistics (RTH / ETH / IB)
- Per Globex day: overnight (ETH) high/low/volume, RTH open/high/low/close, **IB high/low** (`IbMinutes` default 60), `IbComplete` (true only after IB end — no lookahead), IB extension multiples, prior RTH H/L/C, prior Globex H/L, gap vs prior close.
- Add `m_rthValueEnd` (default 16:00) for value logic, distinct from `m_rthEnd` 16:15 (session flag) — see `VPF:34–35`; document which one each API uses.
- **Accept:** synthetic day: IB values are 0/unset before 10:30 ET and fixed after; prior-day values roll at the Globex date change; DST transition day passes.

#### VPF-3 (P1, depends VPB-1) — RTH-only and ETH profiles
- Override `includeBarInProfile` / `isNewProfileWindow` so the Own developing profile can run as RTH-only (09:30–16:00) or full Globex day; expose `RthProfile()`, `GlobexProfile()`, completed prior RTH profile.
- **Accept:** RTH profile excludes overnight bars (verified by trace of first/last bar times per session).

#### VPF-4 (P3, NO-CHANGE) — Rollover awareness (rev 2 narrowed)
- Native: MC Custom Futures handle rollover (date or volume/OI rule) and back-adjustment (absolute difference / ratio) [UM p405–408, p458]. Dropped: own roll calendar, `ContinuousAdjustment` input, `BlockEntriesInRollWindow`.
- Kept: a documented chart-setup note (use non-adjusted data for VP levels near a splice; synthesis §6.2) and, only if Q3 confirms code cannot see roll dates (**unverified** either way), a cheap splice detector (`Bars.Info.Name` change or a gap test) that flags naked POCs/composites with `crossesRoll`.
- **Accept:** on a Custom Futures chart with a known roll, the flag fires on the roll session and nowhere else.

#### VPF-5 (P2, NO-CHANGE) — Order-flow helpers (capability-gated)
- Native data, not recomputed: bid/ask volume and delta per price come from MC VP; MC assigns buy/sell volume by uptick/downtick or by bid/ask (historical tick bid/ask requested) [UM p35, p158–159]. Helpers only derive patterns. Log which classification the chart uses (manual check; code access unverified).
- `OrderFlowCapability` detected in StartCalc: delta stream present (`Resolution.DeltaType`, as LELX2 does at `_DeltaDivRevFuturesLELX2:93`), VP levels with non-zero Ask/Bid. If absent, helpers return NaN and log once.
- Helpers: bar delta, anchored CVD (`anchor` = session open / named bar), delta z (same clock minute over prior 20 sessions — prior sessions only), diagonal imbalance `Ask(p) ≥ ratio × Bid(p−1)` (default 3.0) with volume floor percentile, `StackedImbalances(side, minCount=3)`, unfinished auction at bar extreme, absorption heuristic (exceptional volume at an extreme with price progress ≤ k ticks and delta against the move; thresholds as inputs, documented as hypothesis).
- Data source: per-bar footprint needs a per-bar profile stream (MC VP set to "every 1 bar") or tick data; document the chart setup. **Verify that the IBKR feed delivers aggressor side** before trusting live values (synthesis §6.2).
- **Accept:** synthetic footprint tests for imbalance/stacked/unfinished auction; NaN path when capability missing.
- **Impact:** gives the `_StackedImblCont*` / `_AbsorptionRev*` stubs something to build on; no change to existing code.

#### VPF-6 (P1 for the bug, P3 for the table; decision D2) — CME holiday fix (rev 2 narrowed)
- **Bug fix (BACKTEST CHANGE, confirmed):** Sunday holiday → following Monday closed (Sun 25 Dec 2022 → CME closed Mon 26 Dec 2022; same for Sun 1 Jan 2023 → Mon 2 Jan). **Saturday** observance must be checked **per year** against CME holiday notices (e.g. Sat 25 Dec 2021 / Sat 1 Jan 2022); list each year's ruling with source in the PR.
- Table: MC Holiday Lists and session templates can encode closures and early closes per symbol [UM p389–391, p490–491]. Prefer configuring those; build a code table only if Q2 says the code must know (e.g. session flags/IB on half days). If built: opt-in table (`UseCmeHolidayTable`, default false) for equity-index futures: full closures, 13:00 ET Globex halts on no-RTH holidays (`HolidayHaltEt`), 13:15 ET early RTH close (day after Thanksgiving, Dec 24, Jul 3 where applicable) via `RthEarlyCloseEt`, Good Friday per year (D2). Years covered: whatever the backtests use (2017–2026), each entry with a source comment.
- **Accept:** state/session unit tests for each table date; default path unchanged.

#### VPF-7 (P2, NO-CHANGE) — Extract `CmeSessionClock` (VPF-11)
- Move session logic into a composable class (per CS-0) holding windows, tz, cache and holiday hooks (hooks passed as delegates or a small interface implemented by VPF). VPF keeps every current method as a delegate; generic tz bits go to `_CustomSignal` (CS-6).
- **Accept:** identical LELX trade list; a non-VP test strategy can instantiate the clock.

#### VPF-8 (P3) — Hygiene
- Consistent accessibility (`PrimarySession`, `TimeSinceOpen`… protected or all public); document that `Intraday*` are Globex-day values in futures; note fall-back DST ambiguity in `ToEastern` for Local-zone charts.

---

## 4. Suggested order

1. CS-0 (PR #3) → **VPB-9** file split (if helper files compile) → CS-1 (gap-only journal) → CS-3.
2. VPF-6 Sunday-bug fix; VPB-3 regression audit with VPB-1 → VPB-4 (if the line still exists).
3. VPF-2 → VPF-3.
4. VPB-5 (after D4), VPB-6, CS-4, CS-7, CS-8, VPB-2, VPB-7, VPF-5.
5. CS-5, CS-6.
6. VPF-7, VPF-4 (narrowed), VPF-6 table (only if Q2), CS-9, VPB-10, VPF-8.
7. Removed: CS-2, VPF-1.

## 5. Open decisions (Ettienne)

| ID | Decision | Recommendation (analyst) |
|---|---|---|
| D1 | ES/NQ notional cap: margin vs full contract value (`capByNotional`, used by LELX/LELX2 with `price × BPV`) | Keep **full contract value** as the default cap (conservative, broker-independent; an ES contract is roughly $300k+ notional, so the cap mostly prevents leverage creep). Offer margin-based capping only via MC Portfolio Properties margin settings [wiki Advanced._Portfolio] (VPF-1 removed), and always keep the risk-per-trade limit as the binding constraint. For small accounts, size on **MES/MNQ** rather than loosening the cap. |
| D2 | CME holiday/early-close hours incl. Good Friday | Ship the table **opt-in** (VPF-6), year by year with sources; treat Good Friday as **closed** unless the year's CME notice says otherwise (CME has opened equity futures for a short session on some Good Fridays when NFP was released; those sessions are thin and unrepresentative, so excluding them from research is the safer default — confirm the specific years from CME notices). Fix the Sunday-observance bug regardless. |
| D3 | Unused confluence properties (`ConfluenceMinHits`, `SameStreamDecay`, `CrossStreamBonus`) | Mark `[Obsolete]` now and delete with the file split (VPB-9). The engine's comments explicitly argue against decay/bonus (double counting); keeping dead knobs invites false optimisation and inflates the trial count. |
| D4 | Should completed profiles depend on `TrackNakedPoc`? | **No.** Decouple (VPB-5): completeness is a fact about the profile window; naked tracking is an optional cost. Only LELX runs with `TrackNakedPoc=false` change. |
| D5 (new) | RTH end for value logic: 16:00 vs 16:15 | 16:00 for profiles/value (matches synthesis), keep 16:15 as the session flag (VPF-2). |
| D6 (new) | Default developing-profile source once VPB-1 is validated; 1-tick vs approximation | Switch to `Own` after VPB-3 is clean; 1-tick source for validation, BarUniform/BarTypical only for exploratory runs. Until then no developing HVN/LVN feature in backtests. |
| D7 (rev 2) | New `[Input]`s on `_CustomSignal` (each shows on all 14 derived strategies and may reset saved workspace settings) | Minimise. Candidates: (a) `JournalEnabled` (CS-1), (b) `AssetClass` override (CS-6), (c) trade-mgmt limits (CS-7), (d) `ControlMode`/seed (CS-8). Recommended: **zero new base inputs** — expose all as protected properties set by derived strategies; at most (a). VP-base inputs (`DevelopingSource`, `DevProfileSourceMode`, `DevProfileDataStream`, `LookaheadAudit`, VPB-2 method) affect only VP strategies. |
| Q1 (rev 2) | Any pre-trade cost filter at all? | If wanted, read MC `Commission`/`Slippage` [PG p17, p45]; otherwise none. |
| Q2 (rev 2) | Holidays/early closes: MC Holiday List + session template only, or also a code table? | MC config first; code table only if session flags/IB must know half-days. |
| Q3 (rev 2) | Rollover data: individual contracts or MC Custom Futures (adjusted or not)? | Non-adjusted Custom Futures for VP levels; decides whether VPF-4 detector is needed. |
| (carried) | VPB-28 frozen VWAP snapshot; LELX2 `MaximumEquityPerPosPercent`; csproj `_Test1.Indicator.CS` | Still open from `volume-profile-fix-status.md`. |

## Rev 2.1 update (user decision on Q2)
MultiCharts Holiday Lists are not readable from code, so the code keeps its own US/NYSE holiday lists. VPF-6 goes back to P1, in full:
- Observed-date rules: a holiday on Sunday is observed Monday. Check the Saturday rule per exchange (NYSE does not observe on Friday when Jan 1 is a Saturday).
- Add a CME Globex holiday and early-close table alongside the NYSE one, with sourced dates per year. Good Friday is closed unless that year's CME notice says otherwise.
- Pick the calendar by asset class.
- Unit-test known dates. One known case: Dec 25 2022 was a Sunday, and CME closed Mon Dec 26.

## Rev 2.2 update (user answers, Oct 19)
- D7/inputs: keep new inputs to a minimum. For the journal, add a new ETraceLevel value above Info (e.g. Journal) that turns on per-trade performance journaling, instead of a separate input.
- Q1: add an optional pre-trade cost filter that skips a trade when MC Commission+Slippage, as a fraction of planned risk (R), is above a threshold. Default it off.
- Q2/Q3: add the CME holiday and early-close list (see Rev 2.1).
- Q3 (data): user will mostly use the datafeed's continuous futures, not MC Custom Futures. Keep the VPF-4 roll-splice flag (P2) so profiles and composites don't span a roll gap. Detect rolls from the continuous series where possible, otherwise use a roll-date table.
- D6: our own developing profile becomes the default once VPB-3 passes. Build it from whatever resolution is loaded: tick data gives the exact match to MC, minute data gives a documented approximation. The audit reports the match rate for each resolution.

## Rev 2.3 update (decisions D1–D5, Oct 19)
- D1: size ES by full contract value. No margin option.
- D2: add the CME holiday and early-close list (Rev 2.1).
- D3: remove the unused confluence inputs (ConfluenceMinHits, ConfluenceSameStreamDecay, ConfluenceCrossStreamBonus).
- D4: keep naked POC tracking. Completed-profile marking is separated from TrackNakedPoc, but naked POC tracking itself stays and its behaviour doesn't change.
- D5: value calculations use RTH end 16:00, and the session window ends at 16:15.
All decisions are now settled.

## Rev 2.4: VPB-6 ValueMigrationScore definition (self-contained)
Source: final-synthesis.md §4.0. This is my own coding of Keller Book 4 §8.4 ("trade with value migration over the last 3 windows"). It is a hypothesis, not a measured edge.

Inputs:
- `stream` (data stream)
- `n` = 3 (completed sessions compared)
- `minChangeAtrFraction` = 0.10
- `atrLen` = 20 (daily ATR)
- `includeDeveloping` = true
- Session = RTH profiles (09:30–16:00 ET for value, per D5). For non-futures, use the stream's session profile.

Definition:
- `D = minChangeAtrFraction × ATR(atrLen)`, using daily bars ending at the last completed session. No lookahead. If daily data isn't available, compute ATR from completed session ranges.
- `s(a, b) = 0 if |a − b| < D, else sign(a − b)`.
- `POC_0` is the most recent completed session and `POC_k` is the one k sessions earlier.
- `V = Σ_{i=0}^{n−1} s(POC_i, POC_{i+1})`. This needs n+1 completed POCs.
- If `includeDeveloping`, add `s(devPOC, POC_0)`, where devPOC is the developing POC as of the current bar (VPB-1 source).
- Range: [−n, +n], or [−(n+1), +(n+1)] with the developing term. Default is [−4, +4].

Classification (helper `ValueMigrationState`):
- Up when V ≥ +2.
- Down when V ≤ −2.
- Neutral otherwise.
- Make the thresholds parameters, with default 2.
- Return Unknown (not 0) when there are fewer than n+1 completed POCs or no ATR yet.

Use: Spec 1 longs need V ≥ +2. Fades are blocked when migration points against them. No new [Input] on the base: expose these as method parameters, and derived strategies decide.

Tests:
1. POCs 100, 101, 102, 103 (oldest to newest), D = 0.5, devPOC 103.2 → V = 3 (the developing term counts 0 because 0.2 < D), state Up.
2. Flat within D → V = 0, Neutral.
3. Mirror of test 1 → V = −3, Down.
4. Two completed sessions only → Unknown.
5. Truncation test: values at bar t don't change when data after t is removed.

---

## Rev 2.5: VPF-5 order-flow helpers in `_VolumeProfileFuturesBase` (Oct 20, 2026)

Scope: API-only helpers. No new `[Input]`s, everything is off by default (`OrderFlowEnabled = false`, set by derived strategies in code), and every returned value carries a `FlowQuality` tag. The repo was not edited.

Sources. **PG** = MultiCharts .NET Programming Guide (attachment `01M5BF386FDT0VH0AYQBMXHEF2.pdf`, 89 pp). **UM** = MultiCharts User Manual (`01M5BF385QBTK6KHTM7Z5DYDA0.pdf`, 494 pp). **BM** = Bar Magnifier/IOG article (`01M5BF385TMYH4V6Q967DXY60T.pdf`). Page numbers are PDF page indices, and these match the printed footers where I checked. Anything without a citation is marked *unverified*.

### A. Research answers

**Q1. Per-tick bid/ask at trade time; Lee-Ready in real time.**
- `Bars.StatusLine.Bid/Ask` return the *current* Ask/Bid only, with a snapshot `Time` (PG p22, §4.2.4). They have no history: "Status line provides only the current realtime data. There is no history of status line values. You need to load the corresponding ask and bid data series" (MC support, https://www.multicharts.com/discussion/viewtopic.php?t=18611). In real time a strategy can read the prevailing quote on each IOG tick call. Whether that quote is aligned with the trade being processed (same event, no race) is **unverified**. It is a snapshot, so treat it as "quote at or just before processing".
- `Bars.Request.QuoteField` reports whether a series is Ask, Bid, or Trade (PG p21). So a strategy can confirm that Data2/Data3 really are bid/ask series.
- **DataLoader** (`BeginLoadData(InstrumentDataRequest, ...)`): the request has `Resolution` (tick resolutions allowed), `Range`, `QuoteField` (Ask/Bid/Trade), and `Subscribe2RT` (a real-time subscription after history) (PG p71–75, §4.7.3–4.7.4). This is the only documented way to get **tick-level trade, bid and ask streams for both history and real time through one code path**, without needing the chart to carry them. A separate "RTDataLoader" class: **unverified** (not in the PG; `Subscribe2RT` covers it). Whether DataLoader tick bars include per-tick size: Bar has Volume, but whether a single 1-tick bar = one trade's size on IBKR is **unverified**.
- DOM: `Bars.DOM.Ask[0].Size` exists in real time (https://www.multicharts.com/trading-software/index.php?title=Data_Access). It has no history (**unverified**, assumed).
- Bid/Ask *volume* fields: the **strategy API has no per-bar bid-volume or ask-volume fields**. Bars exposes only Volume/Ticks/UpTicks/DownTicks (PG p20). Ask/Bid-traded volume exists only inside the chart tools: Volume Profile "Ask Traded vs Bid Traded" (UM p35) and the Volume Delta chart type (UM p158–159). A strategy reading those values: **unverified / not documented**.
- Lee-Ready in real time: **yes, feasible**. On each real-time tick, classify using the StatusLine quote, or the latest Bid/Ask from DataLoader streams. History: **no stored quotes in the main series**. Historical quotes exist only if the feed supplies historical bid/ask ticks (MC "downloads historical trade, ask and bid ticks", https://www.multicharts.com/features/volume-analysis/; the UM p35/p159 notes say Ask-vs-Bid mode "will request historical tick ask and bid data from your data source"). For **IBKR** historical bid/ask tick depth and alignment with trades: **unverified, must check live**. MC's own Ask/Bid rule differs from Lee-Ready: trades strictly inside the spread inherit the *last recorded side* (UM p35, p159), not the tick rule.

**Q2. Build Volume On; UpTicks/DownTicks; Bid/Ask charts.**
- Build Volume On (Format Instruments → Settings): **Trade Volume** builds on actual volume, **Tick Count** on the number of trades (UM p69; wiki Chart_Settings).
- `Bars.UpTicks/DownTicks` (wiki UpTicks, Symbol_DownTicks): with **Tick Count** they return the number of up/down ticks in the bar. With **Trade Volume** they return the volume traded on up/down ticks. An up tick = price above the preceding tick. **Zero ticks are not documented for UpTicks/DownTicks**: whether zero-tick volume is dropped or inherited is **unverified**. So `UpTicks + DownTicks == Volume` should not be assumed, and the helper must test it per bar. That is acceptance test T7.
- Gotcha (PowerLanguage wiki, https://www.multicharts.com/trading-software/index.php?title=Volume): for intraday charts, PL `Volume` returns *up-tick volume*, not total. Whether .NET `Bars.Volume` behaves the same on intraday bars is **unverified**. The helper must compute total as `UpTicks + DownTicks` only after T7 confirms it, and otherwise flag the bar.
- The Up/Down breakdown "Up Ticks vs Down Ticks" (tick rule with zero-tick inheritance; the first tick in a series counts as BUY) vs "Ask Traded vs Bid Traded" (≥ask BUY, ≤bid SELL, inside → last recorded side) is a **chart-tool setting** of Volume Profile (UM p35) and Volume Delta (UM p158–159). It does **not** change `Bars.UpTicks/DownTicks`, which are always the tick rule per the wiki. Whether the Format-Instruments-level "up/down by bid/ask" option exists: **unverified (not found)**.
- Ticks chart in Bid/Ask mode: the Quote Field can be Trades, Asks, or Bids (UM p69; wiki Chart_Settings), one per series. A Bid-quote or Ask-quote series contains quote updates, **not trades**, so it cannot be the signal series for trade classification. It is usable only as a *quote reference* stream.
- Separate Data2 = Bid and Data3 = Ask series: documented for precise/extended backtesting fills (PG p51; wiki Precise_Backtesting). For classification it is usable only at **bar granularity** in the strategy (`BarsOfData(2).Close`). It does not give tick-aligned quotes, because the strategy is calculated on Data1 (PG p51: "The calculation will be done using only Data Series 1 bars"). For Lee-Ready, use DataLoader tick streams instead (Q1).

**Q3. IOG / RecoverDrawings / Bar Magnifier.**
- IOG (`[IOGMode(IOGMode.Enabled)]`): `CalcBar()` is called "for every tick received ... in real-time or at Open, High, Low, Close of each bar at historic calculation. If Bar Magnifier mode is enabled ... for each detailed tick" (PG p49; BM p4).
- Without the magnifier, history runs on 4 synthetic points per bar (O-H-L-C, or O-L-H-C by proximity), filled with "all possible ticks ... with the minimum price step" for fills only (PG p50). **A tick rule run on that data is meaningless**, so the helper must report `Unavailable` there.
- Bar Magnifier: each detail bar is emulated as O-H-L-C / O-L-H-C, and "there are no ticks between the detailed resolution ticks" (PG p51; UM p316–317; BM p5–7). The resolution can be tick, second, minute or day (PG p50). Only **magnifier = 1 tick** gives real trade sequences. Per-tick size under the magnifier (Δ of `Bars.VolumeValue` between calls) is **unverified**. The magnifier is not supported on Volume Delta / Cumulative Delta chart types (UM p146, p160), and not in Portfolio Trader (BM p6).
- `RecoverDrawings`: drawings made on non-close calculations are deleted unless `[RecoverDrawings(false)]` (PG p29). This only matters if the helpers draw. Keep the helpers drawing-free.
- **Parity recommendation:** do not derive flow from `CalcBar` tick calls. Feed the footprint engine from **one source in both modes**: DataLoader 1-tick Trade stream (+ Bid and Ask tick streams when available), `Subscribe2RT = true`. Bucket ticks into Data1 bars by timestamp (tick time ≤ bar close time, using the bar's open/close boundaries). Compute values only for **closed** bars, plus an explicitly-flagged `Developing` value for the current bar. Use IOG + magnifier(1 tick) only as the fallback source, and history without a 1-tick magnifier is a `BarUpDown`-only regime.

### B. Recommended ranked fallback chain (per bar, first that qualifies)

| Rank | Source flag | Requirement | Quality |
|---|---|---|---|
| 1 | `Aggressor` | A native aggressor side per trade. Not exposed by MC .NET today (Background). Reserved for a future feed or bridge. | Exact |
| 2 | `Quote` | Trade ticks + aligned Bid/Ask tick streams (DataLoader), quote age ≤ `maxQuoteAgeMs` (default 1000, param). Lee-Ready: ≥ask BUY, ≤bid SELL, inside → tick rule. | High |
| 3 | `TickRule` | Trade ticks with sizes (DataLoader 1-tick, or IOG real time / magnifier 1-tick). The sign carries across bars and sessions; the first tick ever = BUY (matches MC, UM p35). | Medium (Low at bar extremes, see caveats) |
| 4 | `BarUpDown` | Only `Bars.UpTicks/DownTicks` with Build Volume On = Trade Volume, and T7 passed. Bar delta only, **no ladder**. | Low |
| 5 | `Unavailable` | Tick Count mode, a failed T7, IOG history without a 1-tick magnifier, or a gap in ticks. | n/a, return null/Unknown |

Bars are tagged individually. A cumulative series carries the **minimum** quality of its constituent bars. Mixed `Quote`/`TickRule` bars within one session are allowed only if `allowMixed` is set (default false → downgrade the whole session to the lower tag).

### C. API sketch (no `[Input]`s)

```csharp
enum FlowSource { Aggressor, Quote, TickRule, BarUpDown, Unavailable }
enum FlowQuality { Exact, High, Medium, Low, Unknown }
struct FlowValue { double Value; FlowSource Source; FlowQuality Quality; bool Developing; }

protected bool OrderFlowEnabled = false;            // set in derived ctor/StartCalc
protected OrderFlowConfig FlowCfg;                  // maxQuoteAgeMs=1000, resetMode=Session|RTH, allowMixed=false
FootprintBar   Footprint(int barsAgo);              // SortedDictionary<long priceTicks,(bidVol,askVol)> + Source/Quality
FlowValue      BarDelta(int barsAgo);               // askVol - bidVol
FlowValue      CumDelta(int barsAgo);               // reset at session open or RTH open (FlowCfg.resetMode)
IList<Imbalance> DiagonalImbalances(int barsAgo, double ratio=3.0, double minVol=10);
StackedResult  StackedImbalances(int barsAgo, double ratio=3.0, double minVol=10, int minStack=3);
FlowValue      Absorption(int barsAgo, Side side, int edgeTicks=2, double volMult=2.0, int holdTicks=1);
```

- Diagonal (standard footprint convention): a **buy** imbalance at price p when `Ask[p] ≥ ratio × Bid[p − 1 tick]` and `Ask[p] ≥ minVol`. A **sell** imbalance at p when `Bid[p] ≥ ratio × Ask[p + 1 tick]` and `Bid[p] ≥ minVol`. A zero denominator with the numerator ≥ minVol counts as an imbalance (ratio = +∞). A missing level (ladder edge) never counts. Stacked = ≥ `minStack` consecutive same-side imbalance prices.
- Absorption (side = Sell, i.e. bids absorbing): within `edgeTicks` of the bar low, Σ Bid vol ≥ `volMult` × mean level volume of the bar, **and** close ≥ low + `holdTicks` ticks, **and** the next bar does not trade below that low (evaluated only once the next bar closes, which avoids lookahead). Mirror for Buy at the high. **It requires quality ≥ High. With TickRule it returns `Unknown`**, per the caveat that size-based tools need real aggressor data.
- Imbalances under `TickRule` are returned with quality Low, and the levels at the bar high/low are excluded by default (`excludeExtremes = true`). Caveat: tick-rule footprints are wrong at bar extremes.

### D. Acceptance tests

- **T1 tick-rule delta.** Trades (price × size): 100.00×2 (first → BUY), 100.25×3 (+), 100.25×1 (0 → +), 100.00×4 (−), 100.00×2 (0 → −), 100.25×1 (+). Buy = 7, Sell = 6, **delta = +1**. Ladder: 100.00 = bid 6 / ask 2, 100.25 = bid 0 / ask 5. Source TickRule.
- **T2 zero-tick inheritance across a bar boundary.** Bar A ends with a down-tick. Bar B's first trade is at the same price, size 5 → SELL 5 (sign carried, not reset). Same across a session boundary. The cum delta resets, but the sign state does not.
- **T3 Lee-Ready.** Quote 99.75/100.00, trade 100.00×5 → BUY. Trade 99.75×3 → SELL. Quote 100.00/100.50, trade 100.25×2 (inside; previous trade 99.75 → up-tick) → BUY. **Delta = +4**, Source Quote. The same trade with quote age > maxQuoteAgeMs → that trade is classified by TickRule, and the bar is downgraded to Medium.
- **T4 truncation / no lookahead.** For every bar t, all helper outputs at t are identical when the data after t is removed (re-run on a truncated tick file). Absorption at t is not final until bar t+1 closes. Before that it is flagged `Developing`.
- **T5 history vs real-time parity.** Record a live session of ticks (Trade+Bid+Ask via DataLoader). Replay it through Data Playback/real time, then reload it as history. Per-bar delta, ladder, and imbalance sets must match exactly for Quote/TickRule. Allow a tolerance only on the first bar after a connect (flag it).
- **T6 imbalance vectors** (ratio 3.0, minVol 10, minStack 3), levels bid × ask: 100.00 10×5, 100.25 4×40, 100.50 6×30, 100.75 5×25, 101.00 20×8. Buy imbalances at 100.25 (40/10 = 4.0), 100.50 (30/4 = 7.5), 100.75 (25/6 = 4.17). Not at 101.00 (8/5 = 1.6). Not at 100.00 (edge). **Stacked buy = 3, at 100.25–100.75.** Sell imbalances: none (101.00 is an edge). minVol vector: ask 8 vs bid 2 below → ratio 4 but < minVol → none. Exact boundary: ask 30 vs bid 10 → ratio 3.0 → imbalance (≥).
- **T7 UpTicks/DownTicks sanity** (gate for BarUpDown). On a 1-tick-resolution recorded bar with known trades (T1), check `UpTicks + DownTicks == total volume` and up = 7, down = 6 under Trade Volume. If zero-tick volume is dropped instead (e.g. 6 / 6), mark BarUpDown as `Low` and never use it for ladders.
- **T8 Unavailable regime.** IOG history without the magnifier, or Build Volume On = Tick Count → all helpers return Unavailable/Unknown, and no exceptions are thrown.

### E. Must verify live (Ettienne)
1. Does IBKR via MC deliver **historical Bid and Ask tick** data for ES, and how far back? (DataLoader with QuoteField Bid/Ask, 1 tick.)
2. Does a DataLoader 1-tick Trade bar carry one trade's actual size on IBKR, or aggregated/filtered prints? (IB's real-time feed is known to be snapshot-based; this is *unverified*, so compare the tick count with an exchange-time-and-sales sample.)
3. Run T7 on IBKR ES: the UpTicks/DownTicks zero-tick behaviour, and whether .NET `Bars.Volume` = total or up-volume on intraday bars.
4. StatusLine Bid/Ask timing vs the IOG tick (staleness) in real time.
5. Bar Magnifier at 1 tick: does `Bars.VolumeValue` step per detailed tick (per-tick size available)?
6. If dxFeed is added later, check whether any MC .NET build exposes its aggressor flag (rank 1). Today none is documented.

## Rev 2.6: corrections from Analyst 2's MC docs review (verified against PG p76, p84–85)
1. **Bid/ask volume IS readable from code.** This corrects Rev 2.5 Q1.
   - `VolumeProfile.ItemForBar(bn)` returns an `IProfile` with `POC`, `VA`, `Values`, `MaxDelta`/`MinDelta` and `HighVAForBar`/`LowVAForBar`/`POCForBar` (PG p84).
   - Each `ILevel` has `AskTradedValue`, `BidTradedValue` and `TotalValue` (PG p85).
   - A DataLoader request with ChartType VolumeDelta or CumulativeDelta returns `BarPriceLevels { Bar, PriceLevel[] Levels, Index, Time, TickID }` per bar, which is a native footprint. Real time needs RTData2 (PG p76).
   - **VPF-5 chain update:** insert a new tier 2, **"MC-BidAsk (High)"**, using MultiCharts' own Ask/Bid-traded split read through the VolumeDelta DataLoader request or the VolumeProfile levels. Prefer it over our own Lee-Ready. Keep our own Quote tier as the cross-check and fallback. TickRule and BarUpDown stay below.
   - MC's rule puts inside-spread trades on the last recorded side (UM p35/p159), so tag the tier with the method name.
   - Acceptance test T9: on a replay day, MC-BidAsk bar delta and our Lee-Ready delta agree on sign for at least 90% of bars. That threshold is a guess; log the real figure.
2. **Continuous futures** can't use Bar Magnifier, roll only at session end, and can't be used on Delta charts (UM p405, p145, p159; check whether these are printed or PDF page numbers). The user plans to use continuous futures. So:
   - Order-flow helpers on a continuous Data1 series must read the bid/ask split from a separate per-contract DataLoader request, or return Unavailable.
   - Intrabar-order-generation backtests on continuous contracts can't use the 1-tick magnifier, so intrabar fills are approximations; document this in VPB-1 and VPF-5.
   - VPF-4 roll splice: rolls fall on session boundaries, which makes detection simpler.
3. **IB tick history limits:** 2,000 s per request and 60 requests per 10 minutes (UM p474). Deep tick or order-flow backtests need recorded or vendor tick data. VPB-3's audit and VPF-5's tests should run on locally recorded data, and the docs should say so.
4. **Slippage** applies only to stop and market fills (PG p53). The cost filter (Q1) should add slippage only for market and stop entries, and limit entries get commission only. Limit fill assumptions are still optimistic, so note it.
5. **UM citations:** printed page ≈ PDF page − 1. Earlier UM citations in this spec are PDF page numbers unless stated otherwise.

## Rev 2.7: dxFeed data source (Oct 20, 2026)

Setup confirmed by Ettienne: **dxFeed is the data feed** in MultiCharts .NET, and **IBKR is the broker only**. Every IB-data assumption in Rev 2.5/2.6 is replaced below. The repo was not edited.

Sources: **DXW** = MC wiki "DxFeed" (https://www.multicharts.com/trading-software/index.php?title=DxFeed). **DXF1** = MC forum "Continuous Futures data feed" (https://www.multicharts.com/discussion/viewtopic.php?t=59549, MC staff, Jan–Mar 2026). **DXF2** = MC forum "MultiCharts 15 Release 15 – Now with dxFeed" (https://www.multicharts.com/discussion/viewtopic.php?t=59414). **CFW** = MC wiki "Which Data Sources Support Expired and Continuous Futures Contracts". **DXS** = dxFeed symbology, U.S. formats (https://kb.dxfeed.com/en/data-model/symbology-guide/equities,-futures,-options,-and-spreads-symbology/u-s--formats.html). **DXT** = dxFeed TimeAndSale API (https://docs.dxfeed.com/dxfeed/api/com/dxfeed/event/market/TimeAndSale.html) and QD model (https://kb.dxfeed.com/en/data-model/market-events/qd-model-of-market-events.html). UM pages are PDF pages. The local MC docs (UM/PG/PT) don't mention dxFeed at all, because dxFeed was added in MC 15 R15 (DXF2), after these manuals.

### A. Research answers

**1. Connection, symbols, continuous contracts.**
- dxFeed is built into MC 15 R15+. It must be subscribed through MultiCharts; a subscription bought directly from dxFeed doesn't work in MC (DXF2). Delayed sample data comes free with a license (DXW). As with any feed, data connections live in QuoteManager and broker connections in MC Broker Profiles (UM p199).
- Native dxFeed symbols are `/ESZ26:XCME` (contract) and `/ES:XCME` (continuous product). Namespaces: XCME (ES/NQ/MES/MNQ), XNYM (CL), XCEC (GC), XCBT (ZN) (DXS). In MC, staff refer to the continuous symbol as **`@ES` / `@MNQ`** (DXF1). The exact name in MC's symbol lookup is **unverified**. Add symbols through QuoteManager → Add Symbol → From Data Source. MC staff say re-adding fixes stale parameters and order rejections (DXF2).
- dxFeed has native continuous data and supports expired contracts (CFW lists dxFeed; the cell values didn't render, so the expired-contract part is **unverified**).
- **Rolls:** hard-coded rules based on a Last-Trading-Date offset plus exchange recommendations. Equity index (ES/NQ/MES/MNQ) follows the CME Equity Index Roll Dates. Some products (e.g. MGC, ZS) roll on liquidity with no fixed schedule; the examples given were Sun Oct 26 2025 and Sun Dec 28 2025 (DXF1). The rules for CL, GC and ZN are **unverified**; ask MC support.
- **No back-adjustment**: futures data is not back-adjusted (DXW, DXF1). A roll shows up as a raw price gap at the splice.
- **Correction to Rev 2.6 §2:** the limits there (no Bar Magnifier, Regular chart type only, Playback "As Is", not on Volume/Cumulative Delta) apply to **MC Custom Futures** (UM p405, p145, p159). They are **not** documented for a feed's native continuous symbol such as dxFeed `@ES`. Whether `@ES` works with Bar Magnifier and on Volume Delta charts is **unverified, so test it live** (§E). Rev 2.6 §2 is superseded.

**2. Historical depth.** Up to **2 years of ticks, 9 years of 1-minute bars and 40 years of daily bars**. Example: NQ minute data from Jun 17 2016, ticks from Jul 1 2024 (DXW). Historical **Ask/Bid exist only at tick level** (no minute bid/ask), and Ask/Bid charts build slowly (DXW). There is no historical DOM; real-time DOM is 10 levels for futures (DXW, DXF2). Request/pacing limits for dxFeed in MC: **none documented (unverified)**. The IB pacing limits in Rev 2.6 §3 (UM p474) **no longer apply** to data. Whether the 2-year tick window rolls forward (so older ticks drop out) is **unverified**, so archive anything you need (§F).

**3. Bid/ask and aggressor.**
- Historical and real-time Bid and Ask ticks are supplied (DXW: tick level only). MC's "Ask Traded vs Bid Traded" mode "will request historical tick ask and bid data from your data source" (UM p35, p159). So MC's VP/Volume Delta split is built by **MC comparing trades with the Bid/Ask tick streams**, not from a vendor aggressor flag. Inside-spread trades take the last recorded side (UM p35).
- dxFeed's own TimeAndSale event carries `aggressorSide` (undefined/buy/sell) and the `bidPrice`/`askPrice` **at trade time**, plus `exchangeSaleConditions`, `validTick`, `type` (new/correction/cancel), `spreadLeg` and `extendedTradingHours` (DXT). Whether MC's dxFeed plug-in uses or exposes any of these: **no MC documentation, so unverified; assume not exposed**. The PG (p20–22, p71–76, p84–85) has no aggressor field, which confirms the Rev 2.5 finding for MC .NET.
- Whether dxFeed's historical TimeAndSale via MC includes bid/ask at trade time: the vendor event has the fields (DXT), but MC only exposes separate Bid/Ask tick series. Tick alignment between those series and Trade ticks (same timestamp resolution, sequence) is **unverified**.

**4. Bar Magnifier / Volume Delta on dxFeed continuous.** Bar Magnifier needs history at the detail resolution, and dxFeed supplies ticks for 2 years and minutes for 9 years (DXW). So a 1-tick magnifier is possible in principle on the last ~2 years, and a 1-minute magnifier on ~9 years. It isn't available in Portfolio Trader (BM p6). On `@ES` continuous symbols: **unverified** (see the correction in §1). The same goes for VolumeDelta chart types and DataLoader `ChartType = VolumeDelta` (PG p76) on `@ES`. If either fails, use the individual contract (`/ESZ26:XCME`) per session.

**5. Time zone, session, prints.**
- MC charts show Local or Exchange time per Format Instruments (UM p73). dxFeed timestamps are UTC epoch ms plus nanos (DXT), and MC converts them; which session template MC assigns to dxFeed CME symbols (CME Globex default vs custom) is **unverified**. Keep our `ToEastern` path and assert the chart time zone in StartCalc (the trace already logs it).
- At vendor level, prints carry `validTick`, sale conditions, corrections/cancels, spread-leg and ETH flags (DXT). Whether MC filters out invalid ticks, spread-leg prints or settlement records, and applies corrections to history, is **unverified**. Test: compare the daily volume of 1-tick bars against the daily bar and against CME's published volume for 3 sessions (§E).

**6. dxFeed data, IBKR execution.** This is the "Data Source ≠ Broker" case. Symbol Mapping is **not automatic**: map each chart symbol (dxFeed `@ES` or contract) to the IB contract in File → Broker Profiles → Symbol Mapping, and pick symbols through lookup (UM p310–311). Without a mapping, the chart's default broker plug-in is the first Trade Bar profile (UM p199). **Continuous risk:** an `@ES` chart has no expiry, so the IB mapping must point at the current front contract and be **changed on each roll**. dxFeed's equity-index roll follows CME's roll dates (DXF1), which may not match the day you roll at IB. Whether MC re-maps `@` symbols automatically: **unverified**. Tick size and point value come from the dxFeed instrument (`Bars.Info.MinMove/PriceScale/BigPointValue`). Both feeds quote exchange prices, so prices agree to the tick, but fills are reported by IB, so live slippage measurement (CS-1 journal) must use the IB fill against the dxFeed signal price.

### B. Changes per work item

- **VPF-5 chain (replaces the Rev 2.5 §B table plus the Rev 2.6 insertion):**

  | Rank | Source | Requirement | Quality |
  |---|---|---|---|
  | 1 | `Aggressor` | dxFeed `aggressorSide`. **Not exposed by MC .NET** (§A3). Stays reserved; never detected today. | Exact |
  | 2 | `MC-BidAsk` | VolumeDelta DataLoader `BarPriceLevels` or `ILevel.AskTradedValue/BidTradedValue` (PG p76, p84–85), with the chart in Ask/Bid-traded mode. It is built from dxFeed Bid/Ask ticks, so it is valid only within the **tick window (~2 yrs)**. | High |
  | 3 | `Quote` | Our Lee-Ready on DataLoader 1-tick Trade+Bid+Ask (dxFeed), `maxQuoteAgeMs`. Cross-check of tier 2 (T9). | High |
  | 4 | `TickRule` | 1-tick Trade only. | Medium |
  | 5 | `BarUpDown` | Up/Down volume, T7 passed. Gives the only flow signal **older than the tick window** (minute history back to ~2016). | Low |
  | 6 | `Unavailable` | Otherwise. | n/a |

  `OrderFlowCapability` also records the earliest tick date it found per stream and tags bars before it `BarUpDown`/`Unavailable` automatically. Drop the Rev 2.5 "verify IBKR delivers aggressor side" line and E1–E2; they're replaced by §E here. The Rev 2.6 rule "continuous Data1 must use a per-contract request" becomes conditional on test E3.
- **VPB-1 / VPB-3 (audit data):** run the audit on **dxFeed 1-tick history** (any 20 ES sessions after Jul 2024), not on locally recorded IB ticks. The 99% POC parity criterion applies only inside the tick window. Minute-source runs (2016+) report match rate only (Rev 2.2 D6). Remove the Rev 2.6 §3 "must run on locally recorded data" requirement. Keep one recorded live session for T5 (history vs real-time parity on the same feed).
- **VPF-4 (roll splice), P2:** dxFeed continuous is **unadjusted** and rolls by vendor rule (CME roll dates for equity index; liquidity for some others; possibly Sunday session opens). Detector: flag a splice when the session-open gap vs the prior close exceeds k × ATR **and** coincides with a table of known dxFeed roll dates (seeded from CME equity roll dates and per-product dates from MC support). Never span a composite or naked-POC search across the splice. Because the data is unadjusted, VP levels from before the roll are in the old contract's price space: invalidate them, don't shift them. Optional `RollShift` (front − back close on the roll day) for display only.
- **VPF-3:** RTH/ETH windows unchanged (09:30–16:00 ET value, 16:15 flag). Add a StartCalc assertion that the dxFeed chart session template is the 24h CME Globex one, so overnight bars exist. If ETH bars are missing, log it and disable `GlobexProfile()`. CL/GC/ZN need their own RTH windows (CL 09:00–14:30, GC 08:20–13:30, ZN 08:20–15:00 ET pit-era conventions); these are **unverified as the right "RTH" choice**, so make them parameters.
- **Cost filter (Q1):** unchanged formula (commission always, slippage only for market/stop entries, PG p53). Commissions stay IBKR rates in the MC Commission Rule. Add a note: the dxFeed subscription is a fixed cost, not per trade, so keep it out of the filter.
- **Test-data guidance:** (a) flow, VPB-1 parity, T1–T9: dxFeed ticks, Jul 2024 onward (NQ example; check the start date per product). (b) VP/value/IB/session logic: dxFeed 1-minute, 2016 onward. (c) Regime/daily studies: dxFeed daily. (d) Bar Magnifier fills: 1-tick inside the tick window, 1-minute before it, and label the run. Delete all IB-history-pacing guidance.

### C. Strategy notes (what backtests can trust)
- **Order-flow strategies** (`_StackedImblCont*`, `_AbsorptionRev*`, delta divergence) can be backtested at High quality only on **~2 years** of data (tick window), which is about 500 sessions per symbol. That's enough for a few parameters, not big sweeps. Count trials (CS-8) and hold out the latest 6 months.
- Anything before the tick window is BarUpDown (Low): use it for regime context only, never for footprint/imbalance/absorption signals.
- **VP / value-migration / IB strategies** can use ~9 years of minute data (approximate developing profile, exact completed profile from MC VP on minute data), which is enough for walk-forward.
- Continuous series are unadjusted, so price-level strategies are fine within a contract, but multi-day levels must reset at rolls (VPF-4). Long-horizon returns or ATR across rolls need roll-gap removal.
- Absorption needs real bid/ask, so require tier ≥ 3 (MC-BidAsk or Quote).

### D. Superseded
Rev 2.5 §A Q1 "For IBKR historical bid/ask tick depth: unverified"; Rev 2.5 §E1, E2, E6; Rev 2.6 §2 (Custom Futures limits applied to feed continuous symbols); Rev 2.6 §3 (IB tick pacing as a data constraint); and mc-capabilities-analyst2 items 9 and §4–5, insofar as they concern data.

### E. Must verify live (Ettienne)
1. The exact continuous symbol in MC's lookup (`@ES` vs `/ES:XCME`), and whether the subscription covers CME, NYMEX, COMEX and CBOT (CL/GC/ZN need separate bundles).
2. The earliest available tick, Bid and Ask date for ES, NQ, MES, MNQ, CL, GC and ZN (DataLoader 1-tick, QuoteField Trade/Bid/Ask). Is there any request size limit or throttling when loading 2 years of ticks?
3. On `@ES`: does Bar Magnifier (1 tick) enable, does a Volume Delta chart open, and does a DataLoader `ChartType = VolumeDelta` request return `BarPriceLevels`?
4. Is `Bars.Info` (MinMove/PriceScale/BigPointValue) correct for MES/MNQ/ZN (ZN = 1/64 tick)?
5. Session template and time zone that MC assigns to dxFeed CME symbols, and whether ETH bars are present.
6. Daily volume of 1-tick bars vs the dxFeed daily bar vs CME settlement volume for 3 sessions (tests filtering of block/spread/settlement prints).
7. The dxFeed roll dates for CL, GC and ZN (ask MC support), and the dates when `@ES` rolled vs when you roll at IB.
8. Symbol Mapping: `@ES` → IB front contract. Does MC need a manual re-map each roll? Place a sim/paper order to confirm.
9. Does the MC tick-ID/timestamp ordering of Trade vs Bid/Ask ticks allow aligned Lee-Ready (T9 agreement rate)?
10. T7 (UpTicks/DownTicks zero-tick behaviour, `Bars.Volume` total vs up-volume) on dxFeed ES.
