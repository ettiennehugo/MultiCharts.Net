# AV-01 · Swing-high AVWAP reclaim (swing sleeve) — implementation spec

**Rev 1.1 (draft), 10 Nov 2026 (EST)** — owner answers of 19:44 ET applied (costs, 2 contracts, news exit, holdout scope, big-winner rule; Prog rev 1.1). Batch 1, build order #5. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § AV-01. Target file: `sandbox/_AV_01_SwingHighAvwapReclaim.Strategy.CS`, class `_AV_01_SwingHighAvwapReclaim`. Stage: sandbox. Acceptance policy: **`big-winner-trend`** (owner, 2026-11-10 19:44 ET: "yes, use the big-winner rule"), predeclared in the header before any result is read.

## 1. Purpose and edge (with sources)
- **Mechanism.** Everyone who bought since the last significant swing high is under water while price is below that swing-high AVWAP, so they sell rallies to get out at break-even. When price reclaims and holds above it, that supply has been absorbed; the cohort is back in profit and the shorts from the high are under water (Cat. AV-01). Shannon: buy strength after a pullback that recovers above a swing-high AVWAP; stop below the most recent higher low.
- **Evidence.** Grade **E**, practitioner only, untested. No controlled study of swing-anchored AVWAPs on futures was found (Cat. v1 AV family prior, searched 20 Oct 2026); Telonics AVWAP bounces are inside noise (Cat. §0 item 3). The test must beat a fake anchor and a matching-length SMA.
- **Horizon.** Swing: days to weeks (owner: scalps to swings of days–weeks, a month as a soft limit). This is the only batch-1 strategy that holds overnight and over weekends.

## 2. Instruments and timeframes
@ES, then @NQ swing sleeve; CL/GC/ZN later (Cat. AV-01) after M8/M9. Streams (same root, same 24h Globex template, unadjusted):
- **Data1 = 15-minute:** HL trigger, orders.
- **Data2 = 60-minute:** swing-high anchor, AVWAP, reclaim signal (`SignalTf = H60`). Closed Data2 bars only (M-MTF `HtfClosed`; manual §4.12; P2 rule: a Data2 bar is used only once its close time ≤ the Data1 bar time).
- **Daily:** M-DAILY (`NeedsDaily = true`) for ATR20d and the trend filter.
- Warm-up: 60 sessions (50-day filter + anchors). Anchors older than the last roll splice are dropped (Cat. AV-01 data line).

## 3. Engines
| Need | Engine / member |
|---|---|
| Swing-high anchor (k × ATR on 60m) | PRICEACTION extra instance on Data2, `pivotMode = EPivotMode.AtrZigZag`, `atrMultiple = AnchorK` (3.0), role `"anchor"` |
| AVWAP from the swing high | M-AVWAP with `AvwapLimits.dataStream = 2`, `AvwapEvents = EAvwapAnchor.SwingHigh` (long side), `maxAgeSessions = 30` (**PROPOSED**). Same back-fill VERIFY as AV-02 §3; fall back to a local Data2 accumulator if needed |
| HL trigger | PRICEACTION main engine on Data1 (fractal, N = 3 on 15m per Cat. §2.1) |
| Trend filter | **Daily close > 50-day SMA** (primary, Cat. AV-01). M-DAILY's SMA length is configurable: `DailyConfig.smaLength` (default 200) drives the property named `Sma200` (`_DailyContext.Function.CS` l.28/132). So set `smaLength = 50` through the protected config and read `Sma200` as SMA50 (comment the misleading name). Compare it with `PrevClose`, the same level-close series. Alternative per the catalogue: `Tsmom(n) > 0` (RG-06). **PROPOSED – owner to confirm** SMA50 as primary. No engine change is needed |
| Runner trail (EX-04 on the new impulse AVWAP) | A second AVWAP anchored at the entry HL bar on Data1 (local accumulator, or M-AVWAP `SwingLow` on Data1 if a second manager is supported — VERIFY) |
| Session, news, roll, cost floor, sizing, journal | As PS-02 §3 |

## 4. Entry rules (long only in the catalogue; the short mirror is not specified)
The catalogue writes AV-01 long-side ("buy strength"). **PROPOSED – owner to confirm:** batch 1 implements **long only**, with the K-DR drift check reported; a short mirror (swing-low AVWAP loss) would be a new registered entry.
1. **Anchor:** the newest confirmed 60m zigzag swing high (reversal ≥ `AnchorK` × ATR on 60m); AVWAP usable from confirmation.
2. **Filter:** at the prior completed daily close, close > SMA50 (or the chosen alternative).
3. **Reclaim signal:** after ≥ 10 consecutive closed 60m bars with close below the anchor AVWAP, a 60m close above AVWAP + δ60, with δ60 = max(2 ticks, 0.10 × ATR14(60m)) (**PROPOSED – owner to confirm** δ60, by analogy with Cat. §2.1 δ). `SignalTf = M15` (sweep arm): the same on 15m closes (≥ 10 bars below on 15m).
4. **Trigger:** after the reclaim, the first Data1 15m fractal pivot low labelled `EPivotLabel.HL` whose price is above the AVWAP. Buy-stop `AV01LE` = confirming bar high + 1 tick.
5. **Setup cancel:** a 60m close back below AVWAP − δ60 before the fill cancels the setup (**PROPOSED – owner to confirm**); a new reclaim needs a fresh ≥ 10-bar spell below.
6. **Window:** entry orders are sent only on RTH bars 09:45–15:30 ET (**PROPOSED – owner to confirm**; the stop may fill on the next RTH bar), not on a roll-window day, not in `EventBlackout`, and not within `RollFlattenSessions` of the next roll.

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | HL − 1 tick (Shannon). **Minimum:** the larger of the swing cost floor 0.3 × ATR20d and the instrument cost floor (ES 14, NQ 19 ticks), measured from the planned entry. **Maximum:** none; widened risk > 1% equity → skip. Frozen at entry; works 24h on the Globex chart. | Cat. AV-01 ("floored at the swing cost floor (≥ 0.3 × ATR20d)"); Cat. §2.1 |
| **Targets** | `TargetMode = SwingHigh` (primary): T1 = the anchor swing-high price + 0 (retest), limit `AV01LT1`. q ≥ 2: ⌊q/2⌋ at T1, runner trailed. q = 1: `SingleLotMode` (default ExitAtT1). `TargetMode = Trail`: no fixed target. | Cat. AV-01 ("swing high + 0 (retest), then trail EX-04"; sweep {swing high, trail}). Half-and-trail reading **PROPOSED – owner to confirm** |
| **Minimum reward** | None in the catalogue; b journaled. If entry ≥ T1 at fill (gap through the swing high), T1 is skipped and the whole position goes to the trail (**PROPOSED – owner to confirm**). | |
| **Break-even** | None specified; not added. | Cat. AV-01 |
| **Trailing (EX-04 on the new impulse AVWAP)** | Anchor = the entry HL bar. Activation: after T1 (SwingHigh) or at +1 R (Trail, **PROPOSED**). Rule: stop = max(stop, newAVWAP − 2 ticks) at each closed **60m** bar, and exit at market on the first 60m close < newAVWAP − 2 ticks. Using the 60m close rather than EX-04's 5m close for a swing hold is **PROPOSED – owner to confirm**. | Cat. AV-01; EX-04 |
| **Time stop / max hold** | Max hold `MaxHoldSessions` = 20 trading sessions (≈ one month, the owner's soft limit): market exit at the RTH close bar (15:45 ET 15m bar) of session 20. **PROPOSED – owner to confirm** (the catalogue states no time stop; the Page says each swing consumer needs an explicit maximum hold). | Owner horizon; Page decisions item 1 |
| **Overnight / weekend** | Allowed. Overnight and weekend gap risk is accepted: a stop gapped through fills at the next bar's open (MC behaviour) and is reported as gap slippage. Overnight margin is checked in the run sheet. | Cat. AV-01 "holds overnight (swing) → gap risk" |
| **Invalidation** | A **60m close below the original swing-high AVWAP − δ60** before T1 → market exit (the reclaim failed). **PROPOSED – owner to confirm.** | Mechanism in Cat. AV-01 |
| **Adds** | None. | Prog §7.7 |
| **Re-entry** | One trade per anchor/reclaim. After a stop-out, a new trade needs a new reclaim (a new ≥ 10-bar spell below the AVWAP and a new close above). | Cat. AV-01 |
| **News** | Entry blackout: no entry orders in `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm**). When > 0, at the last closed **15m** bar before release − N (`0 < MinutesTo(EEventTier.T1) < N + 15`, Prog §7.6) the whole swing position is flattened with `AV01LX`, reason `NEWS`, and entries stay blocked until release + 10 min. This applies overnight too (e.g. an 08:30 release is exited from the 08:15 bar close when N = 5). A `NEWS` exit ends that reclaim; a new trade needs a new reclaim. Note: with N > 0 a 20-session swing will usually be cut by a tier-1 release (several per month), which changes the strategy's character, so a non-zero value is a separate registered trial. When ≤ 0 the position holds through news with its stop. | Owner 2026-11-10; Prog §7.6 |
| **Holiday** | Full-close days: no bars; the position is held. Early close: no new entry orders after the cut-off; the position is held over the early close. ES/NQ `_CmeHolidays`; CL/GC/ZN M8 tables (2016–2026; outside that range S4 refuses in-advance use, so runs stay inside it). | Prog §7.6; M-INSTR M8 |
| **Roll** | Unadjusted continuous data: a position held across the splice would book the roll gap as P&L. So: **flatten at the 15:45 ET bar of the last session before the roll date** (`RollFlattenSessions` = 1) and block new entries from 1 session before the roll; anchors are dropped at the splice (M-AVWAP invalidate-on-roll). ES/NQ roll dates from `RollDetection = ERollDetection.Table` (PR #116 Globex-day keying); M9 leaves detection at the literal 8-point gap. The roll exit is journaled `ROLL`. **PROPOSED – owner to confirm** the 1-session lead. | Cat. AV-01 data line; manual §4.7, §4.13 |
| **Orders and fills** | Buy-stop entry; stop `AV01LS` (24h); T1 `AV01LT1` (trade-through); market exits `AV01LX`. Bar Magnifier 1m on Data1. | Prog §4, §7.1 |
| **MM interaction** | Prog §7.5. In PT the MM's daily-loss flatten (`ForceClosePosition`, fills at the bar-T close) can close a swing position intraday; the strategy then logs `FLAT-BY-MM` and does not re-enter that anchor. `PortfolioFamily = "AV"`. | Check 7 |
| **Same-bar precedence** | MM force-close → stop → T1 → news exit → roll flatten → max-hold exit → invalidation (60m close) → AVWAP-trail close → trail update. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` 2 (owner, 2026-11-10). Future %-risk sizing: Prog §7.8. Overnight margin per contract must fit the research equity; if not, the run is "not assessable" (Page acceptance note).

## 7. Session, news, holiday and roll handling
See §5 rows; Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `AnchorK` | double | 3.0 | × ATR(60m) | {2.0, 3.0} | Tuned | Swing significance |
| `SignalTf` | enum `ESignalTf { H60, M15 }` | H60 | — | enum | Tuned | Reclaim close timeframe |
| `TargetMode` | enum `ETargetMode { SwingHigh, Trail }` | SwingHigh | — | enum | Tuned | Target arm |
| `BarsBelowMin` | int | 10 | signal-TF bars | fixed | Fixed (Cat.) | Spell below AVWAP before a reclaim counts |
| `TrendFilter` | enum `ETrendFilter { Sma50, Tsmom }` | Sma50 | — | enum | Fixed (**PROPOSED** choice) | Daily regime filter |
| `SwingFloorAtr20d` | double | 0.3 | × ATR20d | fixed | Fixed (Cat.) | Minimum stop distance |
| `MaxHoldSessions` | int | 20 | sessions | 5–30 | Fixed (**PROPOSED**) | Time stop |
| `RollFlattenSessions` | int | 1 | sessions before roll | 1–3 | Fixed (**PROPOSED**) | Roll exit lead |
| `TrailOffsetTicks` | int | 2 | ticks | fixed | Fixed (EX-04) | Trail offset |
| `NewsExitMinutes` | int | 0 | minutes before a tier-1 release | 0–120; ≤ 0 = disabled | Fixed (**PROPOSED** default) | Flatten the swing position and block entries N minutes before scheduled high-impact news (Prog §7.6) |
| `EntryStartHHMM` 945, `EntryEndHHMM` 1530, `SingleLotMode`, `Contracts` 2, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy` (**default `BigWinnerTrend`** here), `MaxSingleTradeNetProfitPct` (logged only), `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Registered sweep = 2 × 2 × 2 = 8 (Cat. AV-01). Primary: k = 3, 60m signal, SwingHigh.

## 9. Logging
Prefix `AV-01`. `AV-01 ANCHOR tag() high() usable()`; `AV-01 RECLAIM t() barsBelow() avwap() delta60() filter(sma50|tsmom value)`; `AV-01 HOLD day(n)` once per session while open (MFE/MAE so far); exit reasons add `MAXHOLD`, `ROLL`, `INVALIDATION`. Skip reasons: `filter-off`, `no-anchor`, `reclaim-cancel`, `roll-lead`, `event-blackout`, `risk-budget`, `margin`. `logSignal("AV01", …)` on every reclaim + HL, with ctx carrying the fake-anchor and SMA control values.

## 10. Test plan
- **Windows:** W-M (60m/15m built from 1-minute): development 2016–2022 with yearly anchored folds; validation 2023 → the §5.1 holdout cut (default for this strategy: data ≤ 31 Dec 2024; with `HoldoutScope = AllStrategies` and 18 months, to 2025-04-30). The daily filter needs ≥ 50 prior days. Holdout sealed.
- **Controls (Cat. AV-01):** K-FL-anchor (AVWAP anchored at a random bar 20–60 bars before confirmation); K-SMA (SMA of length = bars since anchor); K-FL ±0.5 ATR20d for swing ideas (Cat. §2.3). **Kill:** real anchor ≤ either fake.
- **Gates:** G0-a, G0-b; G0-c roll handling (multi-session anchors); G0-d for the AVWAP level arm.
- **Low frequency:** the catalogue estimates 1–3 trades per month per instrument (an estimate). If validation has < 100 trades the verdict is **inconclusive**; do not relax filters.
- Report overnight gap slippage separately from regular slippage.

## 11. Acceptance criteria
Prog §6 + the AV-01 kill, under **`big-winner-trend`** (header standard, Acceptance policy):
- The 10% largest-winner cap is **not** applied.
- **Trader's equation:** cost-adjusted expectancy E = P(win) × average win − P(loss) × average loss (net of $17.00 ES / $9.48 NQ round trips, in R and $) must be > 0.
- **Top-1% trim:** E must stay > 0 after removing the largest 1% of **winning** trades (K = max(1, ⌈1% × winners⌉), rounding per Prog §10); report both E values.
- Every other gate in Prog §6 still applies (net PF ≥ 1.20, samples, drawdown, folds, no year > 50% of net, costs, controls).
- `AcceptancePolicy` input default = `BigWinnerTrend`; `MaxSingleTradeNetProfitPct` is logged but not used as a gate.

## 12. Implementation checklist
- [ ] Data2 60m setup and closed-bar reads (M-MTF), PRICEACTION zigzag on Data2, M-AVWAP on Data2 (VERIFY back-fill).
- [ ] `NeedsDaily = true`, `DailyConfig.smaLength = 50`; filter = `DailyContext.PrevClose > DailyContext.Sma200` (SMA50), NaN → skip `filter-warmup`.
- [ ] Reclaim state machine (bars-below counter, cancel), 15m HL trigger.
- [ ] Stop with the swing floor; T1; new-impulse AVWAP trail on 60m closes; max hold; roll flatten via the roll table; holiday/early-close handling.
- [ ] Off-MC tests: bars-below counter, roll-lead calculation from the table, max-hold counting across weekends and holidays, precedence.
- [ ] `NewsExitMinutes` rule on 15m bars, including overnight releases (Prog §7.6).
- [ ] Header §18 (Acceptance policy): `big-winner-trend` predeclared, with the big-winner premise written out (few large trend winners).
- [ ] Native compile + smoke (To date before the §5.1 cut, ≤ 12/31/2024 by default; check that no position crosses a roll).

## 13. Header mapping
Trigger: in a daily uptrend (close > SMA50), after ≥ 10 60m closes below the AVWAP of the last 3-ATR swing high, a 60m close back above it; then the first 15m HL above the AVWAP → buy-stop above the HL bar. Management: stop 1 tick under the HL (at least 0.3 × ATR20d); half off at the swing high; runner trailed at the new impulse AVWAP − 2 ticks on 60m closes. Exit: stop, T1/trail, a 60m close back below the swing-high AVWAP, 20 sessions, or the session before a roll.

## 14. Open questions for Ettienne
1. Long only in batch 1?
2. Trend filter: SMA50 via `DailyConfig.smaLength = 50` (primary) or TSMOM > 0?
3. δ60 = max(2 ticks, 0.10 × ATR14(60m)).
4. Max hold 20 sessions; flatten 1 session before each roll.
5. Reclaim-failure invalidation (60m close back below AVWAP − δ60) and setup cancel.
6. Trail on 60m closes; Trail arm activates at +1 R; skip T1 if the fill gaps beyond it.
7. Hold over early closes, holidays and weekends (news: `NewsExitMinutes` default 0).
8. Prog §10 batch-wide items.

## 15. Sources
Cat. AV-01, §0 item 3, §2.1–2.3, §2.6, EX-04, RG-06; Shannon, *Maximum Trading Gains with Anchored VWAP*; Cat. v1 AV family prior (no controlled futures study, searched 20 Oct 2026); engines manual §4.3 (M-DAILY), §4.7, §4.8, §4.12 (M-MTF), §4.13 (rolls), §5.23; M-INSTR M8/M9; Strategy-planning Page (item 5; horizon decision).
