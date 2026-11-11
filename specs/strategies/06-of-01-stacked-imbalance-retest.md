# OF-01 · Stacked-imbalance origin: first retest continuation — implementation spec

**Rev 1.3 (draft), 10 Nov 2026 (EST).** Rev 1.3 replaces the unsourced variant B (HigherLow) with **variant K (Keller 6.3)**, from Keller B1 Ch 6.3 (Kindle Loc ~1896; quotes read from page images, may differ slightly). It adds the zone-age and escaped-balance options and logs R/MFE/MAE. Rev 1.2 refines the rules from Trader Dale, *ORDER FLOW: Trading Setups (The Insider's Guide To Trading)*, as read by Ettienne in the Kindle reader. Page numbers are the reader's labels, ±2. The holdout is owner-approved (18 months, OF-01 only). Where the book differs from rev 1.1, the book is followed by default ("per Dale p.X"), and the rev 1.1 rule survives only as a named variant where useful. Rev 1.1: 2 contracts, news exit, costs. Batch 1, build order #6. **Data-gated** on bid/ask tick availability only (§10).

Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § OF-01. Reference code: manual Example 38 (`_Ex38StackedImbalanceRetest`; limit arm, 1 R target, placeholders). Owner shell: `_StackedImblContLELX.Strategy.CS` (header text only, empty methods). Target file: `sandbox/_OF_01_StackedImbalanceRetest.Strategy.CS`, class `_OF_01_StackedImbalanceRetest`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** Three or more consecutive diagonal imbalances on one side mean aggressive traders lifted the offer (or hit the bid) through several prices. If they are still positioned, the first return to that zone is defended (Cat. OF-01; Dale p.76).
- **Trader Dale, *Order Flow: Trading Setups*, Setup #4 Stacked Imbalances (pp.74–76, plus the stop, take-profit and trailing chapters):**
  - **Imbalance (pp.74–76):** buying = Ask ≥ 300% of the Bid one price below (diagonal); selling = Bid ≥ 300% of the Ask one price above.
  - **Stack (pp.74–76):** ≥ 3 consecutive imbalances ("Imbalance cluster size 3"). "Minimal Imbalance Volume" = 0, so there is **no volume floor**.
  - **Zone:** the whole highlighted stacked cluster. The box is drawn "until tested".
  - **Move away:** "at least one or two whole footprints (candles) above or below it", on a **30-minute** footprint chart.
  - **Entry:** directly when price returns to the zone (no extra trigger).
  - **First test only:** "Trade only the first hit (first test)… don't attempt to trade one trading level more than once."
  - **Context:** best "within a trend or before a trend begins"; a strong one-way move is enough.
  - **Trades Filter** (e.g. 25+ contracts on 6E): a visual noise filter, not a setup requirement.
  - **Stops (p.136):** beyond the far edge of the zone (below support / above resistance), or behind the heavy-volume area. Band 10–20% of daily ATR: *"If your SL method points you to a SL that would be outside of this range (either too tight or too wide), then it is best to either skip the trade or use a fixed SL somewhere within the 10–20% range."*
  - **Targets (pp.124–128):** take profit a little before the next heavy-volume area: *"It is safer to take your profit a bit sooner — just a little bit before the heavy volume area."* If that area is too close for an acceptable risk/reward, skip the trade. Unfinished business can stretch the target slightly (p.83).
  - **Trailing (pp.129–131):** trail only while aggressive participants are on your side. For a long, each new buying imbalance in the trade's direction lets the stop move up (5-minute footprints). Stop trailing or exit on warning signs: opposite-side imbalances, absorption or limit orders against the trade, a price/delta divergence, especially near strong S/R or heavy volume.
  - Dale gives no win rates or statistics.
- **Keller B1 Ch 6.3, Stacked-Imbalance Continuation (Kindle Loc ~1896), checked 2026-11-10:**
  - Qualified stack: "≥3 consecutive same-side diagonal imbalances with real volume and real travel".
  - Entry: on the pullback to the origin, enter only on "renewed same-side aggression" (a fresh imbalance, or delta reasserting). Stop beyond the origin's far side.
  - Targets: the stack's extreme, then the next structure (nPOC, poor extreme, HVN edge).
  - Filters:
    - the stack escaped a balance edge or an LVN corridor, not formed mid-value;
    - a corrective pullback with fading opposing delta is good; an opposing qualified stack driving the pullback means stand down;
    - first retest only;
    - full size only on a confirmed trend day, at most a scalp in balance;
    - freshness decays (a stack from 3 hours ago is stale).
  - Chained origins become trailing-stop rungs.
  - Checked, not specified in Keller B1 Ch 6.3: numbers for "real volume" and "real travel", the aggression test, stop buffer, and the age cut-off beyond the 3-hour example.
  - Dale stays the **primary**. Keller's rules form **variant K** (§4 step 7). Keller's escaped-balance and freshness filters are also available as options on the primary.
- **Owner shell `_StackedImblContLELX`:** stop entry after a bullish close that holds the zone low, or a mid-zone limit on the 2nd/3rd test; RR 1.5–3. These are different rules (Dale trades the first test only) and remain a possible later registration (open question 6).
- **Evidence:** grade **E/D**, untested, book only. No statistics are claimed.

## 2. Instruments, timeframes and engine support
- @ES first, then @NQ. CL/GC later (Cat.).
- **Zone timeframe `ZoneTfMinutes` = 30** (per Dale's 30-minute footprints). **Management timeframe = 5 minutes** (per Dale p.129–131: trailing on 5m footprints).
- **Engine fact (checked on `main` @ 47ffda2):** footprints exist only for the **Data1** bars (`_VolumeProfileFuturesBase.Footprint(barsAgo)` / `StackedImbalances(barsAgo, …)` index Data1). There is no multi-bar aggregation and no Data2 footprint.
- **Chart layout (PROPOSED – owner to confirm):** Data1 = **@ES 5-minute** (dxFeed continuous unadjusted, `OrderFlowEnabled = true`, `FlowCfg.McBidAsk = _OrderFlow.EMcBidAsk.Prefer`) for orders, trailing and warnings. The 30m zone footprint is built by a strategy-local aggregator that sums the bid/ask ladder rows of the six Data1 5m footprints in each 30m bucket (Globex-aligned :00/:30). The stack test then runs on that aggregate.
  - **VERIFY / open implementation item (carried from rev 1.1):** can a `_OrderFlow.FootprintBar` (or an equivalent row list) be constructed from summed rows, so the public static `_OrderFlow.Stacked(fp, ratio, minVol, minStack, excludeExtremes)` can be reused? If not, Coder adds a small sandbox helper that applies the same diagonal rule (base-class-spec §VPF-5 definition) to the summed rows.
  - Test it against the T6 vector and against a native 30m chart's `StackedImbalances` on the same day.
- **Fallbacks, labelled deviations:**
  - (a) Data1 = 30m chart with native 30m footprints for zones. The 5m trailing then uses 30m footprints (deviation from Dale p.129–131).
  - (b) `ZoneTfMinutes = 5`: zones from native 5m footprints (the rev 1.1 behaviour).
- Bar Magnifier: tick detail inside the verified tick window.

## 3. Engines
| Need | Engine / member |
|---|---|
| Imbalances and stacks | `DiagonalImbalances` / `StackedImbalances` (Data1 5m), plus the 30m aggregator (§2). `ratio` = 3.0 (= 300%, per Dale p.74–76); `minStack` = 3. `minVol` = **1**: Dale's "Minimal Imbalance Volume 0" means no floor, and the engine floors `minVol` at 1 (`Math.Max(1.0, minVol)` in `_OrderFlow.Imbalances`), so 1 is the closest legal value. A level must trade at least 1 contract. |
| Data validity (not a setup filter) | Footprints must carry real bid/ask: `FlowQuality` ≤ `High` (MC-BidAsk/Quote). TickRule/BarUpDown bars are never used: manual §5.9 pitfall and Cat. §2.1. This is a data rule, not Dale's Trades Filter. |
| Daily ATR (stop band) | M-DAILY (`NeedsDaily = true`), `DailyConfig.atrLength = DailyAtrLength` (**14, PROPOSED**; Dale says "daily ATR" without a length). The property is named `Atr20d` but returns the ATR of `atrLength`. |
| Heavy-volume target | F-HVN: `Composite(1, TargetProfileSessions).hvns` (`Node.low/high/peak`; KDE nodes). Profile choice **PROPOSED** (§5). |
| Opposite imbalance, absorption, delta (warnings) | `StackedImbalances` / `DiagonalImbalances` (sell side), `Absorption(barsAgo, _OrderFlow.Side.Buy, …)` (limit sellers absorbing at the high), `BarDelta`, `CumDelta` (Data1 5m). Absorption needs quality ≥ High. |
| Unfinished business (target stretch) | **No engine helper on main** (base-class-spec lists "unfinished auction at bar extreme" but `_VolumeProfileFuturesBase` exposes none). Not in batch 1; logged as a future arm. |
| Session / flat | F-SESS `IsRth`, F-TM `FlatBy = 15:55`, `mustFlatten()` |
| News / holiday | M-EVENT `EventBlackout`, `NewsExitMinutes` (Prog §7.6); `_CmeHolidays` |
| Roll | `RollDetection = ERollDetection.Table`; `rollOn(1)` clears zones; no entry on `IsRollWindow` days |
| Cost floor, sizing, journal, controls | M-INSTR M7 floor (ES 14 ticks); `sizeForRisk`; `journalArm`, `logSignal`; `ControlArm` for K-SH and K-FL |

## 4. Entry rules (long shown; short mirrors with selling stacks = resistance, tested from below)
1. **Zone (per Dale pp.74–76):** on each closed zone-TF (30m) bar, every **separate** run of ≥ 3 consecutive buying imbalances is a zone [lo, hi] = the run's lowest and highest imbalance price. **Several runs in one bar = several zones, each tested independently** (**PROPOSED – owner to confirm**; the book does not say first, last or strongest). Optional catalogue filter `CloseTopFrac` (zone bar closes in its upper 30%): **not from Dale, default off** (variant `Cat-CloseTop`).
2. **Zone life (per Dale "until tested"):** a zone lives until its first test, a roll, or `ZoneMaxSessions` sessions (**5, PROPOSED**, to bound memory; Dale states no expiry). It does not expire at the session end. **Freshness option (Keller 6.3):** `ZoneMaxAgeMinutes` (clock minutes from the zone bar's close to the test bar). The default is 0 = off for the Dale primary; variant K registers **180** (Keller: "a stack from 3 hours ago is stale"; the exact cut is **PROPOSED**, checked: not specified in Keller B1 Ch 6.3). Prior-session zones are always older than 180 minutes, so this replaces rev 1.2's `Keller-SameSession` variant. Only RTH zone bars create zones (**PROPOSED**).
3. **Move away (per Dale p.76):** the zone is armed once `MinAwayBars` (default 1, range 1–2) complete zone-TF bars after the zone bar have their **low above zone hi**: whole footprints above it. This replaces rev 1.1's "≥ 1 × ATR5".
4. **Context filter (optional, PROPOSED definition; Dale: "within a trend or before a trend begins… a strong one-way move suffices"):** `ContextMode { Off, OneWayMove }`, default **Off**. With `OneWayMove`, the zone counts only if the zone-TF close rose ≥ `ContextAtr` (1.5, PROPOSED) × ATR14(zone TF) over the `ContextBars` (4, PROPOSED) zone-TF bars ending at the zone bar. Results are stratified by trend vs balance day either way (Keller 6.3).
   - **Escaped-balance filter option (Keller 6.3):** `EscapeFilter { Off, BalanceEdgeOrLvn }`. With `BalanceEdgeOrLvn`, a buying zone qualifies only if zone lo ≥ VAH of `Composite(1, 3)` (escaped above value), or the zone overlaps an LVN node of `Composite(1, 3).lvns` or the developing RTH profile `DevelopingExtra(1).lvns`. Otherwise it is "mid-value" and is logged and spent (`mid-value`). Shorts mirror at VAL. The default is Off for the Dale primary (Dale does not require it); variant K registers it on. The composite length of 3 and the overlap test are **PROPOSED** (checked: not specified in Keller B1 Ch 6.3).
5. **First test only (per Dale p.76):** the first Data1 bar after arming whose low ≤ zone hi is the test. Only that test may trade. "Don't attempt to trade one trading level more than once." After it, the zone is spent whether or not an order filled. A test during ETH or another no-entry time also spends the zone (**PROPOSED**).
6. **Entry, primary (per Dale p.76):** a buy **limit `OF01LE` at zone hi** (the near edge), working from the arming bar's close while the zone is armed and the window is open. There is no higher-low or aggression trigger. Fills on trade-through only (Prog §4). Before sending, the stop band (§5) and the R:R skip (§5) are checked against the planned entry = zone hi; a failing zone is logged and spent.
7. **Variant K (`EntryMode = KellerRenewed`, Keller B1 Ch 6.3).** This replaces rev 1.2's unsourced variant B (HigherLow), which is withdrawn. It is a separately registered variant, never the primary.
   - **Qualified stack:** ≥ 3 imbalances as in step 1, plus Keller's "real volume" and "real travel" (numbers **PROPOSED**, checked: not specified in Keller B1 Ch 6.3):
     - real volume: `VolFloorPct` = 70 (each stacked level's volume ≥ the 70th percentile of row volumes in the zone bar; the existing catalogue filter);
     - real travel: the drive from zone hi to its extreme before the test is ≥ 1 × ATR5 (rev 1.1's move-away rule).
   - **Registered filters:** `ZoneMaxAgeMinutes` 180, `EscapeFilter = BalanceEdgeOrLvn` (steps 2 and 4).
   - **Pullback quality:** if a qualified **selling** stack (≥ 3) prints on any Data1 bar between arming and the trigger, the zone is spent (`opposing-stack`, Keller: "stand down"). A fading opposing delta on the pullback (sum of `BarDelta` on the pullback bars shrinking toward 0) is logged as a stratifier, not required.
   - **Trigger (renewed same-side aggression):** after the first test (first retest only), and within `KAggressionBars` (3, **PROPOSED**) Data1 5m bars of it, the first closed bar that holds ≥ zone lo and either prints a fresh buying diagonal imbalance (`DiagonalImbalances`, buy side) or has `BarDelta` > 0 after a negative-delta pullback bar (delta reasserting). Entry `OF01LE` is market next bar (order type **PROPOSED**, checked: not specified in Keller B1 Ch 6.3). With no trigger, the zone is spent.
   - **Stop:** zone lo − `StopBufferTicks` (beyond the origin's far side; Keller and Dale agree). The Dale stop band (§5) still applies.
   - **Size (Keller "full size only on a confirmed trend day, at most a scalp in balance"):** if the day is classified as a trend day in the trade's direction by the VW-01 classifier (VW-01 §4 step 1, shared code) at the trigger bar, q = `Contracts`. Otherwise q = 1, which exits at T1 (the "scalp" reading is **PROPOSED**). In PT the MM size is used, and on a non-trend day the whole position exits at T1.
   - **Targets:** T1 = the stack's/drive's extreme (the highest high between the zone bar and the test) − 1 tick (offset **PROPOSED**). The runner goes to the next structure above T1: the nearer of the nearest naked POC (`NakedPocs(1)`, `TrackNakedPoc = true`) and the low edge of the next HVN of `Composite(1, TargetProfileSessions)`, − `TargetOffsetTicks`. Keller's poor extreme has no engine helper on main and is not used in batch 1. The `MinRR` skip uses T1.
   - **Runner trail (chained origins):** each new qualified buying stack (≥ 3) that prints in the trade's direction after entry is a rung: stop = max(stop, that stack's lo − `TrailBufferTicks`), one-way (Prog §7.3). The Dale warning freeze/exit (§5) applies unchanged.
8. **Cancel before fill:** a closed bar with close < zone lo − 1 tick cancels the zone and the working order (a sliced origin, Keller). The zone is also cancelled at a roll, after `ZoneMaxSessions`, or past `ZoneMaxAgeMinutes` when set.
9. **Window:** entries 09:45–15:00 ET (**PROPOSED**; none in the book or catalogue). Not on roll-window days, not in `EventBlackout` or the news-exit window. One position at a time; at most one filled trade per side per session (**PROPOSED**).

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | Structural stop = zone lo − `StopBufferTicks` (1, PROPOSED), beyond the far edge of the zone. Optional variant `StopBasis = BehindHvn`: below the low edge of the heavy-volume node that contains or sits just below the zone. Distance d = entry − stop. | per Dale p.136 |
| **Stop band** | Band [`StopMinAtrPct`, `StopMaxAtrPct`] = [10%, 20%] × daily ATR (`DailyAtrLength` 14, PROPOSED). If d is outside the band: `StopOutOfBandMode = Skip` (**default**) skips the trade (`SKIP stop-out-of-band(tight|wide)`). `ClampFixed` replaces d with the nearest band edge (too tight → 10%, too wide → 20%; "a fixed SL somewhere within the 10–20% range"; nearest edge is **PROPOSED**). The cost floor (ES 14 ticks) still applies as an absolute minimum; if the floor exceeds the band, skip. The stop is frozen at entry except for the trail. | per Dale p.136; Prog §4 |
| **Target (contract 1)** | `TargetMode = DaleHvn` (**primary**): the nearest HVN above the entry in `Composite(1, TargetProfileSessions)` (5 sessions, **PROPOSED** profile choice) whose low edge is > entry + 1 tick. T1 = that HVN's low − `TargetOffsetTicks` (2, **PROPOSED**: "a little bit before the heavy volume area"). Limit `OF01LT1`. No HVN above → skip (`no-hvn`). | per Dale pp.124–128 |
| **Minimum R:R** | RR = (T1 − entry) / d. If RR < `MinRR` (1.0, **PROPOSED**) → skip (`rr-too-low`), because the heavy-volume area is too close. | per Dale pp.124–128 |
| **Unfinished business stretch** | Not implemented in batch 1 (no engine helper). Journal whether a UB level lies between T1 and the HVN, for later study. | per Dale p.83 |
| **Fallback target variant** | `TargetMode = FixedR` (`TargetR` 1.0): the catalogue / Example 38 rule, not from Dale; a named variant only. The rev 1.1 "move's high" second target is removed. | Cat. OF-01; Ex 38 |
| **Contract split (q = 2)** | Contract 1 exits at T1 (before the HVN). Contract 2 is the **aggression runner**: no fixed target; exits by the trail, a warning exit, the news exit or the session end (**PROPOSED** split). q = 1 (MM size): the single contract exits at T1 (`SingleLotMode = ExitAtT1`, owner-accepted). q ≥ 3: ⌊q/2⌋ at T1, the rest run (Prog §7.2). | Dale pp.124–131; owner 2026-11-10 |
| **Break-even** | None (not in Dale or the catalogue). | — |
| **Trailing (aggression trail)** | Active from entry for the whole open position (one shared stop). On each closed Data1 **5m** bar with a new **buying** imbalance in the trade's direction (a `DiagonalImbalances` buy imbalance at a price above the current stop and above entry; with ≥ 3 stacked, use the run): candidate = the imbalance's (or run's) lowest price − `TrailBufferTicks` (1). Stop = max(stop, candidate), one-way and capped below the last close (Prog §7.3). The exact move rule is **PROPOSED**, sourced from Dale. | per Dale pp.129–131 |
| **Warning signs: freeze** | On a closed 5m bar: `Absorption(1, _OrderFlow.Side.Buy)` = 1 for the previous bar (limit sellers absorbing at the high; the engine reports a bar as Developing until the next bar closes, so it is read one bar later), or a price/delta divergence (bar high > highest high since entry while `BarDelta` < 0). Either one **freezes** the trail: no further stop moves, journal `WARN kind(absorption|divergence) action(freeze)`. **PROPOSED**, sourced from Dale. | per Dale pp.129–131 (and Confirmations #2–#4) |
| **Warning signs: exit** | On a closed 5m bar: a **stacked selling imbalance (≥ 3)** in the bar **and** `BarDelta` < 0 → market exit of the whole remaining position (`OF01LX`, reason `FLOW-WARN`). **PROPOSED**, sourced from Dale (opposite-side imbalances plus delta). Proximity to an HVN or strong S/R is logged as a stratifier, not a condition. | per Dale pp.129–131 |
| **Time stop** | None in the book or catalogue; `TimeStopBars = 0` (off). | — |
| **Session end** | Flat at 15:55 ET (`FlatBy`); early close: flatten at close − 5 min, entry cut-off close − 60 min (Prog §7.6). Never overnight. Zones may carry over to later sessions (§4 step 2), positions never do. | Ex 38; Prog §7.6 |
| **Invalidation before fill** | Close < zone lo − 1 tick cancels the zone (§4 step 8). | Keller 6.3 |
| **Variant K exits** | Stop as the primary; T1 at the drive extreme − 1 tick; runner to the nearer of naked POC / next HVN edge; trail on chained-origin rungs instead of single imbalances; non-trend day: the whole position exits at T1. Details in §4 step 7. | Keller B1 Ch 6.3 |
| **Adds / pyramiding** | None. Dale's new imbalances move the stop; they never add size. | Prog §7.7 |
| **Re-entry after a stop** | Never on that zone (per Dale p.76, first test only). Another zone may trade, subject to the session cap. | per Dale p.76 |
| **News (M-EVENT)** | Entry blackout −2/+10 min (tier-1). `NewsExitMinutes` (default 0 = off, **PROPOSED**): when > 0, at the last 5m close before release − N, flatten (`NEWS`). A working limit is not re-sent and the zone is spent. | Prog §7.6 |
| **Holiday** | No trading on full closes; early close as above. | Prog §7.6 |
| **Roll (M9)** | `rollOn(1)` clears all zones; no entries on `IsRollWindow` days. Flat daily, so no splice exposure. | Prog §7.6 |
| **Orders and fills** | Entry `OF01LE`: limit at zone hi (primary) or market next bar (variant K), `Contracts.UserSpecified`. Stop `OF01LS` (`OrderExit.FromAll`, re-sent every bar, trailed). T1 limit `OF01LT1` for contract 1 (`Contracts.UserSpecified`, qty from the actual position, Prog §7.2). Market exits `OF01LX` (`FromAll`). Limits fill on trade-through. An entry bar that also prints below the stop is resolved by the tick magnifier; if unresolved, assume entry then stop (Prog §4). | Prog §4, §7.1 |
| **MM interaction** | Prog §7.5 and the Check 7 facts: `ForceClosePosition` fills at the bar-T close; `Paused` blocks all fills; `EntryContracts` −1 = coded size, 0 blocks entries, > 0 = MM size. The MM never moves stops or targets. `PortfolioFamily = "OF"`. | Check 7 |
| **Same-bar precedence** | (1) MM force-close → (2) stop, including a trailed stop → (3) T1 limit → (4) news exit → (5) warning **exit** (`FLOW-WARN`) → (6) FlatBy / early close → (7) roll → then, still open and no market exit sent: (8) warning **freeze** check → (9) trail update (skipped if frozen) → (10) zone cancel / arm / test bookkeeping for other zones. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` = 2 (owner, 2026-11-10): contract 1 at T1 before the HVN, contract 2 trailed with aggression. Risk per contract = d (after the band and floor). The 1%-equity skip budget uses d. Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
§5 rows; Prog §7.6.

## 8. Native inputs (header §17)
Header §17 describes each input with Purpose, Default, Units, Range, Effect, Interactions, and Fixed/Tuned.
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose / effect |
|---|---|---|---|---|---|---|
| `ImbalanceRatio` | double | 3.0 | Ask/Bid diagonal (3.0 = 300%) | ≥ 3.0 (engine floor) | Fixed (per Dale p.74–76) | Imbalance threshold |
| `MinStack` | int | 3 | consecutive imbalances | 3–5 | Fixed (per Dale p.74–76) | Stack length |
| `ZoneTfMinutes` | int | 30 | minutes | {5, 30} | Fixed (per Dale; 5 = fallback b) | Footprint TF for zones |
| `MinAwayBars` | int | 1 | zone-TF bars | 1–2 | Tuned (per Dale p.76) | Whole footprints away before the test |
| `ZoneMaxSessions` | int | 5 | sessions | 1–10 | Fixed (**PROPOSED**) | Zone life bound (Dale: until tested) |
| `ContextMode` | enum `EContextMode { Off, OneWayMove }` | Off | — | enum | Fixed (**PROPOSED** definition) | Optional trend context (Dale) |
| `ContextAtr` / `ContextBars` | double / int | 1.5 / 4 | × ATR14(zone TF) / bars | 1.0–3.0 / 2–8 | Fixed (**PROPOSED**) | One-way-move definition |
| `EntryMode` | enum `EEntryMode { ZoneLimit, KellerRenewed }` | ZoneLimit | — | enum | Fixed per registration | Primary per Dale; KellerRenewed = variant K (Keller B1 Ch 6.3) |
| `KAggressionBars` | int | 3 | 5m bars | 1–6 | Fixed (**PROPOSED**) | Variant K: bars after the test to see renewed aggression |
| `ZoneMaxAgeMinutes` | int | 0 | minutes (0 = off) | 0 or 60–360 | Fixed (variant K registers 180, Keller 6.3; cut **PROPOSED**) | Freshness: zones older than this at the test are spent |
| `EscapeFilter` | enum `EEscapeFilter { Off, BalanceEdgeOrLvn }` | Off | — | enum | Fixed (variant K registers BalanceEdgeOrLvn, Keller 6.3) | Zone must have escaped value or sit in an LVN |
| `StopBasis` | enum `EStopBasis { ZoneEdge, BehindHvn }` | ZoneEdge | — | enum | Fixed | Stop beyond the zone or behind heavy volume (Dale p.136) |
| `StopBufferTicks` | int | 1 | ticks | 0–4 | Fixed (**PROPOSED**) | Beyond the far edge |
| `DailyAtrLength` | int | 14 | daily bars | 10–20 | Fixed (**PROPOSED**) | `DailyConfig.atrLength` for the band |
| `StopMinAtrPct` | double | 10 | % of daily ATR | 5–15 | Fixed (per Dale p.136) | Band floor |
| `StopMaxAtrPct` | double | 20 | % of daily ATR | 15–30 | Fixed (per Dale p.136) | Band ceiling |
| `StopOutOfBandMode` | enum `EStopBandMode { Skip, ClampFixed }` | Skip | — | enum | Tuned (per Dale p.136) | Skip, or clamp to the nearest band edge |
| `TargetMode` | enum `ETargetMode { DaleHvn, FixedR }` | DaleHvn | — | enum | Fixed per registration | Primary per Dale; FixedR = catalogue variant |
| `TargetProfileSessions` | int | 5 | sessions | 1–20 | Fixed (**PROPOSED**) | Composite used for HVNs |
| `TargetOffsetTicks` | int | 2 | ticks before the HVN | 0–8 | Fixed (**PROPOSED**) | "A little bit before" |
| `MinRR` | double | 1.0 | reward/risk | 0.5–3.0 | Fixed (**PROPOSED**) | Skip if the HVN is too close |
| `TargetR` | double | 1.0 | R | fixed | Fixed (Ex 38) | FixedR variant only |
| `TrailBufferTicks` | int | 1 | ticks | 0–4 | Fixed (**PROPOSED**) | Aggression-trail offset |
| `WarnExit` / `WarnFreeze` | bool | true / true | — | on/off | Fixed (**PROPOSED**) | Flow warning exit / trail freeze |
| `CloseTopFrac` | double | 0 | fraction of bar range (0 = off) | 0 or 0.30 | Fixed (not from Dale; catalogue variant) | Optional close-location filter |
| `VolFloorPct` | double | 0 | percentile (0 = off) | 0 or 70 | Fixed (not from Dale; catalogue variant) | Optional per-level volume floor |
| `TimeStopBars` | int | 0 | 5m bars (0 = off) | 0–24 | Fixed | Optional time stop |
| `EntryStartHHMM` 945, `EntryEndHHMM` 1500, `SingleLotMode` ExitAtT1, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct`, `OutlierRemovalPct` | as PS-02 §8 | | | | | |

**Registered configs (≤ 4 in W-T): PROPOSED – owner to confirm.**
- Rev 1.1 registered ratio {3:1, 4:1} × entry {limit, HL}. Dale fixes ratio = 300% and the direct limit entry.
- The proposed sweep is `MinAwayBars` {1, 2} × `StopOutOfBandMode` {Skip, ClampFixed} = **4**, both from Dale's own stated ranges.
- Primary cell (1, Skip) is frozen before any read.
- Variant K (Keller 6.3) and the FixedR target are separate registrations, outside the 4. Variant K is one registered cell: `ZoneMaxAgeMinutes` 180, `EscapeFilter` on, `VolFloorPct` 70, `KAggressionBars` 3.
- Replacing the catalogue's registered sweep needs the owner's OK.

## 9. Logging
Prefix `OF-01`.
- `OF-01 ZONE t() tf(30) side() lo() hi() levels(n) ratio() bar(<zone bar time>) quality() context(<off|ok|fail>)`; one line per separate run.
- `OF-01 ARM zone(<id>) awayBars(n)`, `OF-01 TEST zone(<id>) t() filled(<y|n>)`, `OF-01 CANCEL zone(<id>) reason(sliced|expired|roll|news|eth-test)`.
- `OF-01 PLAN entry() stop() d() band(<min>-<max>) dailyAtr() mode(<ok|clamp-tight|clamp-wide>) hvn(<low>-<high> peak()) t1() rr() ub(<price|none>)`.
- Skip reasons: `stop-out-of-band(tight|wide)`, `floor-above-band`, `no-hvn`, `rr-too-low`, `quality`, `risk-budget`, `window`, `cap`, `news-window`.
- `OF-01 TRAIL stop() basis(imb <px> | stack <lo>-<hi> | rung <lo>)`; variant K adds `OF-01 KTRIG kind(imb|delta) t()`, skip reasons `mid-value`, `stale`, `opposing-stack`, `no-aggression`, and the fields `trendDay(y|n)`, `pullbackDelta()`; `OF-01 WARN kind(absorption|divergence|opp-stack) delta() action(freeze|exit) nearHvn(<y|n>)`.
- EXIT reasons add `FLOW-WARN`. Exit lines carry R, MFE and MAE (Prog §8; Keller B4 §8.4 step 9). The rest as Prog §8 (`STOP`, `TARGET`, `NEWS`, `FLATBY`, …).
- `journalArm("OF01LE", entry, stop, t1, ctx)` with ctx = zone id, d, RR, quality tier and source. `logSignal` on every test (traded or skipped) for K-SH / K-FL.

## 10. Test plan (holdout owner-approved, Prog §5.1)
- **Holdout (owner, 2026-11-10 19:53 ET: "18 months for OF-01 only is fine"):** OF-01 uses the rolling 18-month cut, **2025-05-01** as of 2026-11-10, recorded per run. The holdout is 2025-05-01 onward and stays sealed.
- **Usable bid/ask span.** The catalogue's data notes (YV-01, YV-02, YV-04, YV-07 and OF-09 in v2.7) record **@ES bid/ask only from 2024-10-28** (dxFeed ticks from about 2024-10-27). The NQ start is not given (VERIFY). The OF-01 span is therefore about **28 Oct 2024 → 30 Apr 2025**: about 6 months, roughly 125 RTH sessions (a calendar count, not a data check).
- **Split (PROPOSED – owner to confirm), frozen before any read:**
  - **development:** 28 Oct 2024 → 28 Feb 2025, about 4 months / 83 sessions;
  - **validation:** 1 Mar 2025 → 30 Apr 2025, about 2 months / 42 sessions;
  - **walk-forward inside development:** anchored, one fold (train Nov–Dec 2024 → test Jan–Feb 2025).
  - This is the catalogue's W-T proportion of 2 : 1. ES and NQ are run separately.
- **Data check first (read-only, QuoteManager):** confirm that @ES tick + Bid/Ask (MC-BidAsk) history really starts 2024-10-28, find the @NQ start, and check that nothing has rolled off. Record the verified span and the quality-tier mix in the run sheet. If fewer than 4 development months exist, OF-01 is parked.
- **Sample expectation:** the catalogue frequency estimate (4–10 trades per month; an estimate, not data) implies roughly 16–40 development events and 8–20 validation trades. Dale's first-test-only rule and the R:R skip can only lower that. The Prog §6 rules apply unchanged:
  - Cat. §2.3: < 30 development events = no evaluation; 30–149 = inconclusive; ≥ 150 needed for Gate 3.
  - ≥ 100 validation trades are required.
  - The likely verdict is **"no evaluation" or "inconclusive"**. It is reported as such and never relaxed.

- **Configs:** the 4 in §8 (PROPOSED). Variant K and FixedR are reported as separate, pre-registered comparisons on the same events.
- **Controls:**
  - K-SH: shuffled-zone placebo (same count and time-of-day distribution, same exits).
  - K-FL: zone shifted ±0.5 ATR14(zone TF).
  - Variant K vs the primary on the same zones (K-LM). Also the primary with `ZoneMaxAgeMinutes` 180 and with `EscapeFilter` on, as two pre-registered ablations, so each Keller filter is measured separately.
  - Report results per quality tier, per regime (trend vs balance day, Keller), and with vs without `ContextMode`.
- **Kill:** result ≤ the shuffled placebo.
- **Costs and floors:** Prog §4 ($17.00 ES, $9.48 NQ). Tick magnifier inside the verified tick window.

## 11. Acceptance criteria
Prog §6 + the K-SH kill, on the §10 split (validation Mar–Apr 2025). `catalogue-default`. Below the sample minimums, the verdict is "no evaluation" or "inconclusive".

## 12. Implementation checklist
- [ ] Resolve §2 VERIFY: build a 30m footprint from six 5m footprints (summed rows), reusing `_OrderFlow.Stacked` if a `FootprintBar` can be built, else a sandbox helper. Unit-test it against T6 and a native 30m chart.
- [ ] Zone store: one zone per separate run; life until test / roll / `ZoneMaxSessions`; `MinAwayBars` arming; first-test-spent logic (including ETH tests); slice cancel.
- [ ] Entry: zone-edge limit (primary); variant K behind `EntryMode` (aggression trigger, opposing-stack stand-down, VW-01 trend-day classifier reuse, drive-extreme T1, nPOC/HVN runner, chained-origin rungs). Zone age and escaped-balance options.
- [ ] Pre-trade checks at zone hi: stop band with `StopOutOfBandMode` (`NeedsDaily = true`, `DailyConfig.atrLength = DailyAtrLength`); cost floor; HVN target from `Composite(1, TargetProfileSessions).hvns`; `MinRR` skip.
- [ ] Position block: precedence per §5; T1 for contract 1 sized from the actual position; aggression trail on 5m buy imbalances; warning freeze (absorption, divergence) and warning exit (sell stack + negative delta); news exit; FlatBy.
- [ ] Optional filters `CloseTopFrac`, `VolFloorPct` (off by default; not from Dale) and `ContextMode`.
- [ ] Off-MC unit tests:
  - 30m aggregation
  - several runs → several zones
  - `MinAwayBars` 1 vs 2
  - band skip and clamp (tight, wide, floor above band)
  - HVN target selection and `MinRR`
  - trail monotonicity and freeze
  - warning exit
  - precedence
- [ ] `NewsExitMinutes` rule (Prog §7.6).
- [ ] Native compile and smoke run on a verified bid/ask day before the cut (To ≤ 04/30/2025; never on or after 05/01/2025).

## 13. Header mapping
- **Trigger:** a 30m footprint shows ≥ 3 stacked 300% buying imbalances. After at least one whole 30m bar trades above the zone, buy with a limit at the zone top on the first return. One try per zone.
- **Management:**
  - Stop 1 tick beyond the zone low. Skip if it is under 10% or over 20% of the daily ATR.
  - Contract 1 takes profit 2 ticks before the next heavy-volume node (skip if that gives under 1:1).
  - Contract 2 trails under each new 5m buying imbalance.
- **Exit:** stop, T1, a selling stack with negative delta, news, or flat by 15:55.

## 14. Open questions for Ettienne
1. Several separate stacks in one zone bar: is each its own zone? Also: zone life of 5 sessions; ETH tests spend the zone; only RTH zone bars create zones.
2. Stop band: daily ATR length 14; `ClampFixed` clamps to the nearest band edge; stop buffer 1 tick.
3. Target: HVNs from the prior 5-session composite; 2 ticks before the node; `MinRR` 1.0; skip when no HVN.
4. Aggression trail: stop to 1 tick below each new buying imbalance (or run). Warning freeze: absorption or divergence. Warning exit: sell stack plus negative delta. Contract split: contract 1 at T1, contract 2 trailed.
5. Registered sweep `MinAwayBars` {1, 2} × `StopOutOfBandMode` {Skip, ClampFixed}, replacing the catalogue's ratio × entry sweep. Plus the 2 : 1 development/validation split (§10).
6. Context-filter definition (1.5 × ATR over 4 zone bars). Entry window 09:45–15:00 and one trade per side per session. Your `_StackedImblContLELX` rules as a later variant?
7. Chart layout: 5m Data1 with 30m aggregation (§2). Variant K numbers: 180-minute freshness cut, `VolFloorPct` 70, travel ≥ 1 × ATR5, 3 aggression bars, market entry, scalp = 1 contract exiting at T1 on non-trend days, composite of 3 sessions for the escape filter.
8. Prog §10 batch-wide items.

## 15. Sources
- Trader Dale, *ORDER FLOW: Trading Setups (The Insider's Guide To Trading)*, Kindle reader page labels (±2), read by Ettienne 2026-11-10:
  - pp.74–76 (imbalances, stacks, settings, move away, entry, first test, context)
  - Trades Filter section
  - p.83 (unfinished business)
  - pp.124–128 (take profit before heavy volume)
  - pp.129–131 (trailing and warning signs)
  - p.136 (stop placement and the 10–20% daily-ATR band)
  - Not yet read: SL examples 1–4 (p.137) and the second option in Tip #2.
- Earlier: the analyst's Kindle notes `kindle-notes/dale-orderflow-pp13-128.md`, `dale-orderflow-partial.md`.
- Keller, *Volume Profile & Order Flow: The Complete Institutional Reference* (Book 1) Ch 6.3, Stacked-Imbalance Continuation (Kindle Loc ~1896), checked 2026-11-10; Keller Book 4 §8.4 step 9 (R/MFE/MAE logging).
- Catalogue OF-01, §2.1, §2.3; YV/OF-09 data lines.
- Manual §3.30 (Example 38), §5.5 (F-HVN), §5.9 (F-OF), §5.19 (M-DAILY).
- Code: `_VolumeProfileFuturesBase` (`Footprint`, `StackedImbalances`, `Absorption`, `BarDelta`, `CumDelta`), `_OrderFlow.Function.CS` (`Imbalances` minVol floor, `Stacked`), `_DailyContext.Function.CS` (`atrLength`).
- Owner shell `_StackedImblContLELX.Strategy.CS`. Strategy-planning Page (build step 6).
