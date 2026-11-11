# AV-01 · Swing-high AVWAP reclaim (swing sleeve) — implementation spec

**Rev 1.7 (draft), 11 Nov 2026 (EST)** — owner short-side answers applied (Ettienne 2026-11-11 09:52 ET: *"1. Yes, flag it and don't trade it.  2. Yes, and big-winner rule applies to it.  3. Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades. 4. Get their own values.  5. Yes, trail the whole position, log an info message.  6. Make it an input setting using an enum  7. Yes keep it the same - we might have to change it later on for the short side so make a note of that."*): short arm approved, `Side` default `Both`, big-winner rule on the short arm too (answer 2); own-mirror short rules pending test confirmation (answer 3); Long/Short input pairs (§8.2); T1-SKIPPED rule (Prog §7.2a).

**Rev 1.6 (draft), 11 Nov 2026 (EST)** — PROPOSED short mirror (swing-low AVWAP loss) written out in new §5a, gated by `Side` default `LongOnly` (Ettienne 2026-11-11 09:36 ET: *"Please review the strategies thoroughly and make sure that where shorts are involved, we have clarity around handling the short scenario's."*); conventions in Prog §7.9. No owner-approved rule changed in meaning.

**Rev 1.5 (draft), 10 Nov 2026 (EST)** — add inputs (`AddMode` etc., default None) per Prog §7.7, owner-approved 22:06 ET.

**Rev 1.4 (draft), 10 Nov 2026 (EST)** — §8.1 Variants added: every true variation is selectable via Inputs, default = primary (owner, 20:31 ET). Multi-value inputs are typed C# enums, not ints or strings (owner, 20:34 ET; Prog §3 rules 8–9).

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

## 4. Entry rules (long only in the catalogue; a PROPOSED short mirror is in §5a)
The catalogue writes AV-01 long-side ("buy strength"). Batch 1 implements **both sides** (`Side` default `Both`; **Owner-approved 2026-11-11 09:52 ET**, answer 2: *"Yes, and big-winner rule applies to it."*): the long as the catalogue describes it, and the short mirror (swing-low AVWAP loss) written out in §5a. The K-DR drift check is reported per side.
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
| **Minimum reward** | None in the catalogue; b journaled. If entry ≥ T1 at fill (gap through the swing high), T1 is skipped and the whole position goes to the trail, with the Info line `AV01 Info: T1-SKIPPED …` (Prog §7.2a, **Owner-approved 2026-11-11 09:52 ET**: *"Yes, trail the whole position, log an info message."*). | |
| **Break-even** | None specified; not added. | Cat. AV-01 |
| **Trailing (EX-04 on the new impulse AVWAP)** | Anchor = the entry HL bar. Activation: after T1 (SwingHigh) or at +1 R (Trail, **PROPOSED**). Rule: stop = max(stop, newAVWAP − 2 ticks) at each closed **60m** bar, and exit at market on the first 60m close < newAVWAP − 2 ticks. Using the 60m close rather than EX-04's 5m close for a swing hold is **PROPOSED – owner to confirm**. | Cat. AV-01; EX-04 |
| **Time stop / max hold** | Max hold `MaxHoldSessions` = 20 trading sessions (≈ one month, the owner's soft limit): market exit at the RTH close bar (15:45 ET 15m bar) of session 20. **PROPOSED – owner to confirm** (the catalogue states no time stop; the Page says each swing consumer needs an explicit maximum hold). | Owner horizon; Page decisions item 1 |
| **Overnight / weekend** | Allowed. Overnight and weekend gap risk is accepted: a stop gapped through fills at the next bar's open (MC behaviour) and is reported as gap slippage. Overnight margin is checked in the run sheet. | Cat. AV-01 "holds overnight (swing) → gap risk" |
| **Invalidation** | A **60m close below the original swing-high AVWAP − δ60** before T1 → market exit (the reclaim failed). **PROPOSED – owner to confirm.** | Mechanism in Cat. AV-01 |
| **Adds** | `AddMode` (`EAddMode`, default **None**: the catalogue and the notes do not add; Shannon not available). With `OnNewPullback`: a new confirmed 15m HL above the new impulse AVWAP (buy-stop above it; adds are placed only inside the entry window and are then held with the swing position) while the position is open and the last close is ≥ average entry + `AddMinR` × R (the current 1R, re-based on each add fill: Prog §7.7, PR #118 §3a, owner-approved 22:35 ET); `AddContracts` 1 per add, at most `MaxAdds` (2); the stop for the whole position is raised per `AddStopMode` (default `RaiseToAddLow` = that HL − 1 tick, one-way). Adds join the runner. An add that fails the open-risk check is skipped (`SKIP add-risk`). Chart only until M-PORT supports adds (Prog §7.7). **Owner-approved 2026-11-10 22:06 ET** | Prog §7.7 |
| **Re-entry** | One trade per anchor/reclaim. After a stop-out, a new trade needs a new reclaim (a new ≥ 10-bar spell below the AVWAP and a new close above). | Cat. AV-01 |
| **News** | Entry blackout: no entry orders in `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm**). When > 0, at the last closed **15m** bar before release − N (`0 < MinutesTo(EEventTier.T1) < N + 15`, Prog §7.6) the whole swing position is flattened with `AV01LX`, reason `NEWS`, and entries stay blocked until release + 10 min. This applies overnight too (e.g. an 08:30 release is exited from the 08:15 bar close when N = 5). A `NEWS` exit ends that reclaim; a new trade needs a new reclaim. Note: with N > 0 a 20-session swing will usually be cut by a tier-1 release (several per month), which changes the strategy's character, so a non-zero value is a separate registered trial. When ≤ 0 the position holds through news with its stop. | Owner 2026-11-10; Prog §7.6 |
| **Holiday** | Full-close days: no bars; the position is held. Early close: no new entry orders after the cut-off; the position is held over the early close. ES/NQ `_CmeHolidays`; CL/GC/ZN M8 tables (2016–2026; outside that range S4 refuses in-advance use, so runs stay inside it). | Prog §7.6; M-INSTR M8 |
| **Roll** | Unadjusted continuous data: a position held across the splice would book the roll gap as P&L. So: **flatten at the 15:45 ET bar of the last session before the roll date** (`RollFlattenSessions` = 1) and block new entries from 1 session before the roll; anchors are dropped at the splice (M-AVWAP invalidate-on-roll). ES/NQ roll dates from `RollDetection = ERollDetection.Table` (PR #116 Globex-day keying); M9 leaves detection at the literal 8-point gap. The roll exit is journaled `ROLL`. **PROPOSED – owner to confirm** the 1-session lead. | Cat. AV-01 data line; manual §4.7, §4.13 |
| **Orders and fills** | Buy-stop entry; stop `AV01LS` (24h); T1 `AV01LT1` (trade-through); market exits `AV01LX`. Bar Magnifier 1m on Data1. | Prog §4, §7.1 |
| **MM interaction** | Prog §7.5. In PT the MM's daily-loss flatten (`ForceClosePosition`, fills at the bar-T close) can close a swing position intraday; the strategy then logs `FLAT-BY-MM` and does not re-enter that anchor. `PortfolioFamily = "AV"`. | Check 7 |
| **Same-bar precedence** | MM force-close → stop → T1 → news exit → roll flatten → max-hold exit → invalidation (60m close) → AVWAP-trail close → trail update. | Prog §7.4 |

### 5a. Short side: swing-low AVWAP loss (approved arm, rules unconfirmed until tested)
**Status: short arm approved** (**Owner-approved 2026-11-11 09:52 ET**, answer 2: *"Yes, and big-winner rule applies to it."*). The catalogue and Shannon describe AV-01 long only ("buy strength after a pullback that recovers above a swing-high AVWAP"); the short rules below are our own mirror (rev 1.6), accepted for now and unconfirmed until testing confirms the short trades (note below). `Side` (`ESide`) default is now **`Both`**. The short arm is registered alongside the long with its own controls; the losing-side rule (Prog §6 item 12) applies. **Acceptance:** `big-winner-trend` applies to the short arm too (§11). Conventions: Prog §7.9. `side = −1` in every log line.
- **Mechanism (mirror, PROPOSED).** Everyone who sold since the last significant swing low is under water while price is above that swing-low AVWAP, so they cover on dips. When price loses and holds below it, that demand is gone; the longs from the low are now under water and sell rallies, so a rally that fails below the AVWAP is the entry.
**Short side unconfirmed** (**Owner-approved 2026-11-11 09:52 ET**, answer 3: *"Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades."*): these own-mirror short rules are accepted for now. The implemented signal must say so in its header and short branch, and testing must confirm the short trades (Prog §7.9 item 11; §10 and §12 below).

| Item | Short rule (own mirror; values from the `…Short` inputs, §8.2) |
|---|---|
| Anchor | The newest confirmed 60m zigzag swing **low** (reversal ≥ `AnchorK` × ATR on 60m); AVWAP usable from confirmation. Long and short anchors are tracked independently. |
| Daily filter | At the prior completed daily close, close **< SMA50** (`Sma50`), or TSMOM **< 0** (`Tsmom`). |
| Loss signal | After ≥ `BarsBelowMin` (10) consecutive closed 60m bars with close **above** the anchor AVWAP, a 60m close **below AVWAP − δ60** (same δ60). `SignalTf = M15` mirrors on 15m. |
| Trigger and entry | After the loss, the first Data1 15m fractal pivot **high** labelled **`EPivotLabel.LH`** whose price is **below** the AVWAP (the rally that fails below it). **Sell-stop** `AV01SE` = confirming bar **low − 1 tick**. |
| Setup cancel | A 60m close back **above AVWAP + δ60** before the fill; a new signal needs a fresh ≥ 10-bar spell above. |
| Window | As the long (orders sent 09:45–15:30 ET RTH bars; not on roll-window days, `EventBlackout`, or within `RollFlattenSessions` of the roll). |
| Initial stop | **LH + 1 tick** (above the recent lower high), widened **upward** to at least entry + max(`SwingFloorAtr20d` × ATR20d, instrument floor); > 1% equity → skip; frozen; works 24h. `AV01SS`. |
| T1 | `SwingHigh` mode, short: T1 = the anchor swing-**low** price − 0 (retest), buy-to-cover limit `AV01ST1`, ⌊q/2⌋; the member name `SwingHigh` means "the anchor swing extreme". If entry ≤ T1 at fill (gap through the swing low), T1 is skipped and the whole position goes to the trail, with the Info line `AV01 Info: T1-SKIPPED …` (Prog §7.2a, **Owner-approved 2026-11-11 09:52 ET**: *"Yes, trail the whole position, log an info message."*). `Trail` mode as the long. |
| Break-even | None. |
| Trailing (EX-04, new impulse AVWAP) | Anchor = the entry LH bar. After T1 or at **low ≤ entry − 1 R** (Trail mode): stop = **min(stop, newAVWAP + 2 ticks)** at each closed **60m** bar, capped one tick above the last close; market exit on the first 60m close **> newAVWAP + 2 ticks**. |
| Invalidation | Before T1: a 60m close **above the original swing-low AVWAP + δ60** → `AV01SX`. |
| Max hold / overnight / weekend / holiday / roll / news | As the long: 20 sessions; overnight and weekend holds allowed; flatten before the roll; `NewsExitMinutes` per §5. |
| Adds (`OnNewPullback`) | A new confirmed 15m **LH below the new impulse AVWAP** (sell-stop below it; inside the entry window) while last close **≤ average entry − `AddMinR` × R**; whole-position stop **lowered** to that **LH + 1 tick**, never raised; R re-base with `side = −1`; `AV01SA1`, `AV01SA2`. |
| Re-entry | One trade per anchor/loss; after a stop-out a new trade needs a new ≥ 10-bar spell above and a new close below. |
| MFE / MAE / risk %, reports | Prog §7.9 items 2 and 8; short results are never pooled with the long primary in its acceptance verdict. |
| Short-specific risks | (a) **Overnight gap-up / squeeze risk:** the only batch-1 swing short. A stop gapped through fills at the next bar's open (MC) and is journaled as gap slippage; the `actualToPlanned` > 1.00 / > 1.10 counts are reported by side. CME overnight price limits and downside-only RTH circuit breakers do not cap a rally (**VERIFY** the current CME rules before any run). (b) **Drift:** ES/NQ drift up over 20-session horizons, so the short arm faces a structural headwind; K-DR and the controls (K-FL-anchor, K-SMA) are run on the short arm separately. (c) The big-winner policy (`big-winner-trend`) applies to both arms (owner-approved 09:52 ET). |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` 2 (owner, 2026-11-10). Future %-risk sizing: Prog §7.8. Overnight margin per contract must fit the research equity; if not, the run is "not assessable" (Page acceptance note).

## 7. Session, news, holiday and roll handling
See §5 rows; Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide { Both, LongOnly, ShortOnly }` | **Both** (owner-approved 2026-11-11 09:52 ET) | — | enum | Fixed per registration | Both = long + the §5a short arm; LongOnly / ShortOnly restrict (e.g. a flagged losing side, Prog §6 item 12) |
| `AnchorK` | double | 3.0 | × ATR(60m) | {2.0, 3.0} | Tuned | Swing significance |
| `SignalTf` | enum `ESignalTf { H60, M15 }` | H60 | — | enum | Tuned | Reclaim close timeframe |
| `TargetMode` | enum `ETargetMode { SwingHigh, Trail }` | SwingHigh | — | enum | Tuned | Target arm |
| `BarsBelowMin` | int | 10 | signal-TF bars | fixed | Fixed (Cat.) | Spell below AVWAP before a reclaim counts |
| `TrendFilter` | enum `ETrendFilter { Sma50, Tsmom }` | Sma50 | — | enum | Fixed (**PROPOSED** choice) | Daily regime filter |
| `SwingFloorAtr20d` | double | 0.3 | × ATR20d | fixed | Fixed (Cat.) | Minimum stop distance |
| `MaxHoldSessions` | int | 20 | sessions | 5–30 | Fixed (**PROPOSED**) | Time stop |
| `RollFlattenSessions` | int | 1 | sessions before roll | 1–3 | Fixed (**PROPOSED**) | Roll exit lead |
| `TrailOffsetTicks` | int | 2 | ticks | fixed | Fixed (EX-04) | Trail offset |
| `AddMode` | enum `EAddMode { None, OnNewPullback }` | None | — | enum | Fixed per registration | Allow adds (Prog §7.7) |
| `MaxAdds` / `AddContracts` / `AddMinR` | int / int / double | 2 / 1 / 1.0 | adds / contracts / R | 0–4 / 1–5 / 0.5–3 | Fixed (**PROPOSED**) | Add limits and the in-profit gate |
| `AddStopMode` | enum `EAddStopMode { RaiseToAddLow, KeepCurrent }` | RaiseToAddLow | — | enum | Fixed | Stop handling on an add fill |
| `NewsExitMinutes` | int | 0 | minutes before a tier-1 release | 0–120; ≤ 0 = disabled | Fixed (**PROPOSED** default) | Flatten the swing position and block entries N minutes before scheduled high-impact news (Prog §7.6) |
| `EntryStartHHMM` 945, `EntryEndHHMM` 1530, `SingleLotMode`, `Contracts` 2, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy` (**default `BigWinnerTrend`** here), `MaxSingleTradeNetProfitPct` (logged only), `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Registered sweep = 2 × 2 × 2 = 8 (Cat. AV-01). Primary: k = 3, 60m signal, SwingHigh.

### 8.1 Variants (true variations, all selectable via Inputs; default = primary)

A variant is an alternative rule, not a different value of a swept number (Prog §3 rules 8–9). Every variant below is a native `[Input]`. A true on/off switch is a `bool`. Every multi-value choice is a **typed C# enum** declared below, with named members only: no integer codes, no strings. The default is always the primary rule, and it is set in the constructor, never by member order. Enum `[Input]`s are already used by compiled repo studies (`_CustomSignal.TraceLevel` and others, Prog §3 rule 9). Whether the chart and Portfolio Trader optimizers can sweep an enum input is **not yet verified**; Coder checks it on the MiniPC (Prog §9). Inputs marked *sweep axis* are also part of the registered sweep. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants and are not listed.

| Variant | Input | Type | Values | Default (primary) | What it changes | Source |
|---|---|---|---|---|---|---|
| Reclaim timeframe | `SignalTf` | enum `ESignalTf` | `H60`, `M15` | `H60` | *Sweep axis.* Timeframe of the reclaim close and the HL trigger | Cat. AV-01 |
| Target structure | `TargetMode` | enum `ETargetMode` | `SwingHigh`, `Trail` | `SwingHigh` | *Sweep axis.* SwingHigh = half at the anchor swing high, runner on the 60m trail; Trail = no fixed target | Cat. AV-01 |
| Daily regime filter | `TrendFilter` | enum `ETrendFilter` | `Sma50`, `Tsmom` | `Sma50` (**PROPOSED** choice) | Above the 50-day SMA, or time-series momentum, as the long regime gate (the §5a short arm mirrors it: below SMA50 / TSMOM < 0) | Cat. AV-01 |
| Adds | `AddMode` (with `MaxAdds`, `AddContracts`, `AddMinR`, `AddStopMode`) | enum `EAddMode`; int; int; double; enum `EAddStopMode` | `None`, `OnNewPullback`; 0–4; ≥ 1; R; `RaiseToAddLow`, `KeepCurrent` | `None` / 2 / 1 / 1.0 / `RaiseToAddLow` | Adds to a winner on the spec's own trigger; raises the whole-position stop | Prog §7.7; owner 22:06 ET |
| Single-contract handling | `SingleLotMode` | enum `ESingleLotMode` | `ExitAtT1`, `RunnerOnly` | `ExitAtT1` | When q = 1: exit at T1, or hold the single contract as a runner | Owner 2026-11-10; Prog §7.2 |

**Enum declarations** (nested `public enum` types in the strategy class; the input property uses the enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`):

```csharp
// Shared enums (Prog §3 rule 9), declared identically in every batch-1 file:
// ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
public enum EAddMode { None, OnNewPullback }              // default None (source does not add)
public enum EAddStopMode { RaiseToAddLow, KeepCurrent }     // default RaiseToAddLow
public enum ESignalTf { H60, M15 }                   // default H60
public enum ETargetMode { SwingHigh, Trail }         // default SwingHigh
public enum ETrendFilter { Sma50, Tsmom }            // default Sma50
```

**Future variants (not built in batch 1, no input yet):** none recorded. Shannon is not available in the owner's Kindle library, so no book variant was added. `AnchorK` stays a swept parameter.

### 8.2 Long / Short input pairs (**Owner-approved 2026-11-11 09:52 ET**, answer 4: *"Get their own values."*; Prog §7.9 item 12)
Each input below is replaced by a Long/Short pair. Wherever §4, §5, §5a, §8 or §8.1 name the single input, longs read the `…Long` member and shorts the `…Short` member. Short defaults equal the long defaults at first (**PROPOSED**), so the first run is symmetric; sweeps run per side (Prog §7.9 item 12, optimizer note). Inputs not listed stay single (side-blind).
| Single input (replaced) | Pair | Type | Default long / short | Range |
|---|---|---|---|---|
| `AnchorK` | `AnchorKLong` / `AnchorKShort` | double | 3.0 / 3.0 | as the single input it replaces |
| `BarsBelowMin` | `BarsBelowMinLong` / `BarsAboveMinShort` | int | 10 / 10 | as the single input it replaces |
| `SwingFloorAtr20d` | `SwingFloorAtr20dLong` / `SwingFloorAtr20dShort` | double | 0.3 / 0.3 | as the single input it replaces |
| `TrailOffsetTicks` | `TrailOffsetTicksLong` / `TrailOffsetTicksShort` | int | 2 / 2 | as the single input it replaces |
| `AddMinR` | `AddMinRLong` / `AddMinRShort` | double | 1.0 / 1.0 | as the single input it replaces |

## 9. Logging
Prefix `AV-01`. `AV-01 ANCHOR tag() high() usable()`; `AV-01 RECLAIM t() barsBelow() avwap() delta60() filter(sma50|tsmom value)`; `AV-01 HOLD day(n)` once per session while open (MFE/MAE so far); exit reasons add `MAXHOLD`, `ROLL`, `INVALIDATION`. Skip reasons: `filter-off`, `no-anchor`, `reclaim-cancel`, `roll-lead`, `event-blackout`, `risk-budget`, `margin`. `logSignal("AV01", …)` on every reclaim + HL, with ctx carrying the fake-anchor and SMA control values.

## 10. Test plan
- **Windows:** W-M (60m/15m built from 1-minute): development 2016–2022 with yearly anchored folds; validation 2023 → 31 Dec 2024 (fixed cut 2025-01-01, Prog §5.1, owner-approved). The daily filter needs ≥ 50 prior days. Holdout sealed.
- **Controls (Cat. AV-01):** K-FL-anchor (AVWAP anchored at a random bar 20–60 bars before confirmation); K-SMA (SMA of length = bars since anchor); K-FL ±0.5 ATR20d for swing ideas (Cat. §2.3). **Kill:** real anchor ≤ either fake.
- **Gates:** G0-a, G0-b; G0-c roll handling (multi-session anchors); G0-d for the AVWAP level arm.
- **Low frequency:** the catalogue estimates 1–3 trades per month per instrument (an estimate). If validation has < 100 trades the verdict is **inconclusive**; do not relax filters.
- Report overnight gap slippage separately from regular slippage.
- **Short-side confirmation (Prog §7.9 item 11, owner-approved 09:52 ET):** the smoke run must produce short fills; review the first 20 short trades (or all if fewer) bar by bar against §5a (setup, trigger, order type and price, stop, T1, trail, exit reason); record `short-confirmed(<date>, n=<k>)` in the run sheet before any development read.

## 11. Acceptance criteria
Prog §6 + the AV-01 kill, under **`big-winner-trend`** (header standard, Acceptance policy), for the long arm **and the short arm** (**Owner-approved 2026-11-11 09:52 ET**, answer 2: *"Yes, and big-winner rule applies to it."*), each also judged per side (Prog §6 item 12):
- The 10% largest-winner cap is **not** applied.
- **Trader's equation:** cost-adjusted expectancy E = P(win) × average win − P(loss) × average loss (net of $17.00 ES / $9.48 NQ round trips, in R and $) must be > 0.
- **Top-1% trim:** E must stay > 0 after removing the largest 1% of **winning** trades (K = max(1, ⌈1% × winners⌉), rounding per Prog §10); report both E values.
- Every other gate in Prog §6 still applies (net PF ≥ 1.20, samples, drawdown, folds, no year > 50% of net, costs, controls).
- `AcceptancePolicy` input default = `BigWinnerTrend`; `MaxSingleTradeNetProfitPct` is logged but not used as a gate.

## 12. Implementation checklist
- [ ] Data2 60m setup and closed-bar reads (M-MTF), PRICEACTION zigzag on Data2, M-AVWAP on Data2 (VERIFY back-fill).
- [ ] `NeedsDaily = true`, `DailyConfig.smaLength = 50`; filter = `DailyContext.PrevClose > DailyContext.Sma200` (SMA50), NaN → skip `filter-warmup`.
- [ ] Reclaim state machine (bars-below counter, cancel), 15m HL trigger.
- [ ] `Side` input (default `Both`); the §5a short arm (swing-low anchor, bars-above counter, 15m LH trigger, `AV01S*` orders); a side switched off logs `SKIP side-off`.
- [ ] Stop with the swing floor; T1; new-impulse AVWAP trail on 60m closes; max hold; roll flatten via the roll table; holiday/early-close handling.
- [ ] Off-MC tests: bars-below counter, roll-lead calculation from the table, max-hold counting across weekends and holidays, precedence.
- [ ] `NewsExitMinutes` rule on 15m bars, including overnight releases (Prog §7.6).
- [ ] Header §18 (Acceptance policy): `big-winner-trend` predeclared, with the big-winner premise written out (few large trend winners).
- [ ] Native compile + smoke (To ≤ 12/31/2024; check that no position crosses a roll).
- [ ] Header and short-branch comment `// SHORT SIDE UNCONFIRMED — testing must confirm short-side trades (Prog §7.9 item 11)` until the 20-trade short review is recorded.
- [ ] Long/Short input pairs per §8.2 (short defaults = long defaults); `T1-SKIPPED` rule and Info line (Prog §7.2a) where the spec has a T1.

## 13. Header mapping
Trigger: in a daily uptrend (close > SMA50), after ≥ 10 60m closes below the AVWAP of the last 3-ATR swing high, a 60m close back above it; then the first 15m HL above the AVWAP → buy-stop above the HL bar. Management: stop 1 tick under the HL (at least 0.3 × ATR20d); half off at the swing high; runner trailed at the new impulse AVWAP − 2 ticks on 60m closes. Exit: stop, T1/trail, a 60m close back below the swing-high AVWAP, 20 sessions, or the session before a roll.

## 14. Open questions for Ettienne
1. ~~Long only in batch 1? (Rev 1.6 had `Side = LongOnly`.)~~ **Owner-approved 2026-11-11 09:52 ET**: both sides; `Side` default `Both`.
2. Trend filter: SMA50 via `DailyConfig.smaLength = 50` (primary) or TSMOM > 0?
3. δ60 = max(2 ticks, 0.10 × ATR14(60m)).
4. Max hold 20 sessions; flatten 1 session before each roll.
5. Reclaim-failure invalidation (60m close back below AVWAP − δ60) and setup cancel.
6. Trail on 60m closes; Trail arm activates at +1 R; skip T1 if the fill gaps beyond it.
7. Hold over early closes, holidays and weekends (news: `NewsExitMinutes` default 0).
8. Prog §10 batch-wide items.
9. ~~**AV-01 short mirror (rev 1.6, §5a, PROPOSED):** approve the swing-low AVWAP loss short (rally fails below it, stop above the recent lower high) as a registered arm? Until then `Side` = `LongOnly`. If approved: same numbers as the long (AnchorK 3, δ60, 10 bars above, 20-session max hold), and does `big-winner-trend` apply to the short arm too, or `catalogue-default`?~~ **Owner-approved 2026-11-11 09:52 ET**: approved; `big-winner-trend` applies to the short arm; separate short values (§8.2) with short defaults = long defaults at first.

## 15. Sources
Cat. AV-01, §0 item 3, §2.1–2.3, §2.6, EX-04, RG-06; Shannon, *Maximum Trading Gains with Anchored VWAP*; Cat. v1 AV family prior (no controlled futures study, searched 20 Oct 2026); engines manual §4.3 (M-DAILY), §4.7, §4.8, §4.12 (M-MTF), §4.13 (rolls), §5.23; M-INSTR M8/M9; Strategy-planning Page (item 5; horizon decision).
