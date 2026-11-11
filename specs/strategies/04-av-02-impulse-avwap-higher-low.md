# AV-02 · Impulse-origin AVWAP higher-low continuation — implementation spec

**Rev 1.7 (draft), 11 Nov 2026 (EST)** — owner short-side answers applied (Ettienne 2026-11-11 09:52 ET: *"1. Yes, flag it and don't trade it.  2. Yes, and big-winner rule applies to it.  3. Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades. 4. Get their own values.  5. Yes, trail the whole position, log an info message.  6. Make it an input setting using an enum  7. Yes keep it the same - we might have to change it later on for the short side so make a note of that."*): own-mirror short accepted pending test confirmation; Long/Short input pairs (§8.2); T1-SKIPPED rule (Prog §7.2a); `TradesPerDay` typed-enum input (answer 6).

**Rev 1.6 (draft), 11 Nov 2026 (EST)** — short side (swing-high anchor) written out rule by rule in new §5a (Ettienne 2026-11-11 09:36 ET: *"Please review the strategies thoroughly and make sure that where shorts are involved, we have clarity around handling the short scenario's."*); conventions in Prog §7.9. No owner-approved rule changed in meaning.

**Rev 1.5 (draft), 10 Nov 2026 (EST)** — add inputs (`AddMode` etc., default None) per Prog §7.7, owner-approved 22:06 ET.

**Rev 1.4 (draft), 10 Nov 2026 (EST)** — §8.1 Variants added: every true variation is selectable via Inputs, default = primary (owner, 20:31 ET). Multi-value inputs are typed C# enums, not ints or strings (owner, 20:34 ET; Prog §3 rules 8–9).

**Rev 1.1 (draft), 10 Nov 2026 (EST)** — owner answers of 19:44 ET applied (costs, 2 contracts, news exit, holdout scope; Prog rev 1.1). Batch 1, build order #4. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § AV-02 (merged DV-12 touch arm). Target file: `sandbox/_AV_02_ImpulseAvwapHigherLow.Strategy.CS`, class `_AV_02_ImpulseAvwapHigherLow`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** After an impulse leg from a significant low, the AVWAP anchored at that low is the impulse buyers' average cost. The first pullback toward it, if it holds with a higher low, shows them defending; late shorts who sold the pullback are trapped (Cat. AV-02; Shannon: AVWAPs from significant lows act as support).
- **Evidence.** Grade **E**, practitioner only. AVWAP practice has no published controlled statistics; Telonics' AVWAP bounce rates (51.14% from yesterday's high, 52.37% monthly) sit inside noise (Cat. §0 item 3). Final-synthesis cites Tickfloor: a POC pullback did worse than a plain 50% retracement. So AV-02 must beat a fake anchor, a matching SMA and the **same HL rule at a 50% retracement**.
- **Scope in batch 1.** The **intraday arm** only (Page build order item 4: "ES/NQ intraday first, then its separately registered 60m/daily swing arm"). The 60m swing arm is outlined in §10 as a later registration. The DV-12 blind-limit touch arm (Dale) is a later arm too.

## 2. Instruments and timeframes
@ES, then @NQ; CL/GC later. **Data1 = @ES 15-minute** (anchor timeframe, AVWAP, HL and orders), dxFeed continuous unadjusted, 24h Globex template. **PROPOSED – owner to confirm:** a single 15m stream for the intraday arm. The catalogue's "5m anchor / 15m swing (intraday)" is ambiguous; one stream keeps the M-AVWAP P2 placebo available (P2 needs `AvwapLimits.dataStream = 1`). Bar Magnifier 1-minute. Warm-up: 20 sessions.

## 3. Engines
| Need | Engine / member |
|---|---|
| Impulse anchor (confirmed low that starts a ≥ 3 ATR leg) | PRICEACTION on Data1 with `PriceActionConfig.pivotMode = EPivotMode.AtrZigZag`, `atrMultiple = ImpulseAtr` (3.0), engine `atrLength` 20. A zigzag low is confirmed only when price has rallied ≥ 3 × ATR from it, which is the impulse definition (Cat. §2.2 "significant swing … usable from the confirmation bar") |
| AVWAP from the anchor + σ | M-AVWAP `AvwapEvents = EAvwapAnchor.SwingLow` (long) / `SwingHigh` (short); `_Avwap.Value`, `Sigma`, `Band(k)`, `UsableFrom`, `Direction`; `AvwapLimits.maxAgeSessions = 5` (**PROPOSED**); `placebos = EAvwapPlacebo.P2` for the control |
| HL trigger | Second PRICEACTION instance on Data1, `pivotMode = EPivotMode.Fractal`, strength per Futures preset (fractal N = 3 on 15m, Cat. §2.1), via `ExtraPriceAction` with role `"hl"` |
| Session, news, roll, cost floor, sizing, journal | As PS-02 §3 |

**VERIFY before coding (Coder):** `AvwapLimits.backfillBars` is documented as "PRICEACTION strength + 1" (fractal pivots). A zigzag low can confirm many bars after its extreme. Confirm that `_AvwapManager` back-fills from the extreme bar for `EPivotMode.AtrZigZag` pivots. If it does not, compute the anchor AVWAP in the strategy from a price-volume ring starting at the pivot's `extremeBar` (the AVWAP is computed from the anchor bar forward but used only from confirmation; Cat. §2.2), and keep M-AVWAP only for its P2 twin.

## 4. Entry rules (long shown; the short side from a confirmed swing high is written out in §5a)
1. **Anchor:** the newest confirmed zigzag pivot low (impulse ≥ 3 × ATR). The anchor AVWAP is usable from `UsableFrom` (confirmation). A newer confirmed zigzag low replaces it (`replaceSameType`). Anchors are invalidated at a roll.
2. **Impulse high** H = the highest high from the anchor bar up to the current bar, tracked until the pullback starts.
3. **First pullback:** after confirmation, the first bar whose low enters the touch zone [AVWAP − `TouchLowerSigma` × σ, AVWAP + 0.5 σ] (default lower edge 0.25 σ; Cat. AV-02 rule line). Only the first such pullback per anchor is traded. The pullback fails, and the anchor is done, if a 15m close < AVWAP − 1 σ or any low < the anchor low occurs before a trigger.
4. **Trigger:** within that pullback, a fractal pivot low (role `"hl"`) confirmed with `EPivotLabel.HL` and price > anchor low. Buy-stop `AV02LE` = confirming bar high + 1 tick.
5. **Order life:** re-sent each bar until filled, the pullback fails (step 3), or the entry cut-off. **PROPOSED – owner to confirm.**
6. **Window:** entries 09:45–15:00 ET on RTH bars (last 15m bar close 15:00; **PROPOSED – owner to confirm** 15:00 rather than 15:30 so a 15m trade has room); not on roll-window days, `EventBlackout` bars or early-close days after the cut-off. Anchors may come from earlier sessions (multi-session anchors, reset at rolls; Page).

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | min(HL − 1 tick, AVWAP − 1 σ at the trigger bar) for a long, i.e. the **farther** of the two. Floored at the cost floor from the planned entry (ES 14, NQ 19, CL 15 ticks). **Max:** none; widened risk > 1% equity → skip. Frozen at entry. | Cat. AV-02; Cat. §2.1 |
| **Targets** | `TargetMode = ImpulseHigh` (primary): T1 = impulse high H (frozen at the trigger), limit `AV02LT1`. q ≥ 2: ⌊q/2⌋ at T1, runner trailed. q = 1: `SingleLotMode` (default ExitAtT1). If the fill is already at or above H, T1 is skipped and the whole position trails, with the Info line `AV02 Info: T1-SKIPPED …` (Prog §7.2a, **Owner-approved 2026-11-11 09:52 ET**: *"Yes, trail the whole position, log an info message."*). `TargetMode = Trail` (sweep): no fixed target; whole position trailed. | Cat. AV-02 ("the impulse high (b usually ≥ 1), then trail EX-04"; sweep {impulse high, trail}). Half-and-trail reading **PROPOSED – owner to confirm** |
| **Break-even** | None specified; not added. | Cat. AV-02 |
| **Trailing (EX-04 on the impulse AVWAP)** | Activation: after T1 fills (ImpulseHigh) or at +1 R (Trail mode, **PROPOSED**). Rule: stop = max(stop, anchor AVWAP − 2 ticks) each closed bar (one-way), and exit at market on the first 15m close < AVWAP − 2 ticks. | Cat. EX-04 |
| **Time stop / max hold** | Intraday arm: flat at the close of the 15:45 ET bar (`FlatHHMM` = 1545 on 15m bars, so the `MarketNextBar` exit fills at 15:45, before 15:55). Early close: last bar ending ≥ 5 min before the close. No overnight. | Cat. §2.1; **PROPOSED** bar-alignment |
| **Invalidation** | Before T1: a 15m close below the anchor low → market exit (the impulse origin is gone). **PROPOSED – owner to confirm** (usually at or beyond the stop, so rarely binding). After the trail activates: the AVWAP-loss close. | Cat. AV-02 "fails when"; EX-04 |
| **Adds** | `AddMode` (`EAddMode`, default **None**: the catalogue and the notes do not add; Shannon not available). With `OnNewPullback`: a new confirmed 15m HL that holds above the impulse AVWAP + 0.25σ (buy-stop above it) while the position is open and the last close is ≥ average entry + `AddMinR` × R (the current 1R, re-based on each add fill: Prog §7.7, PR #118 §3a, owner-approved 22:35 ET); `AddContracts` 1 per add, at most `MaxAdds` (2); the stop for the whole position is raised per `AddStopMode` (default `RaiseToAddLow` = that HL − 1 tick, one-way). Adds join the runner. An add that fails the open-risk check is skipped (`SKIP add-risk`). Chart only until M-PORT supports adds (Prog §7.7). **Owner-approved 2026-11-10 22:06 ET** | Prog §7.7 |
| **Re-entry** | One trade per anchor. A new trade needs a new confirmed anchor. Trades per day are set by `TradesPerDay` (`ETradesPerDay`, **Owner-approved 2026-11-11 09:52 ET**, answer 6: *"Make it an input setting using an enum"*): `OnePerInstrument` (the first fill of either side ends the day), `OnePerSide` (at most one long and one short per day), `Unlimited` (only the one-per-anchor rule). Default `OnePerInstrument` (**PROPOSED** default). Always one open position at a time. | Cat. AV-02 "first pullback" |
| **News** | Entry blackout: no entry while `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm** the default). When > 0, at the last bar close before release − N (rule `0 < MinutesTo(EEventTier.T1) < N + bar minutes`, Prog §7.6) the strategy flattens with `AV02LX`, reason `NEWS`, stops arming entries until release + 10 min, and the setup's attempt is used. When ≤ 0, an open trade holds through the release with its stop. | Owner 2026-11-10; Cat. §2.1; Prog §7.6 |
| **Holiday / roll** | Prog §7.6. A roll invalidates all anchors (M-AVWAP); no entries on the roll-window day. | Manual §4.7 |
| **Orders and fills** | Buy-stop entry; stop `AV02LS`; T1 `AV02LT1`; market exits `AV02LX`. Magnifier 1m; stop-first. | Prog §4, §7.1 |
| **MM interaction** | Prog §7.5; arm every bar the stop is pending. `PortfolioFamily = "AV"`. | |
| **Same-bar precedence** | MM force-close → stop → T1 → news exit → FlatBy → anchor-low invalidation → AVWAP-loss close → trail update. | Prog §7.4 |

### 5a. Short side, rule by rule (from a confirmed swing high)
Source status: the catalogue states "short mirrors from a confirmed swing high" without short numbers; Shannon is not available (Prog §11 item 5). The numeric short rules below are **PROPOSED (mirror; source long-only)**, same numbers as the long. Owner-approved add mechanics keep their meaning, mirrored. Conventions: Prog §7.9. `side = −1` in every log line.
**Short side unconfirmed** (**Owner-approved 2026-11-11 09:52 ET**, answer 3: *"Accept them for now and we can clarify them using testing - note that in the signals that testing needs to confirm the short side trades."*): these own-mirror short rules are accepted for now. The implemented signal must say so in its header and short branch, and testing must confirm the short trades (Prog §7.9 item 11; §10 and §12 below).

| Item | Short rule |
|---|---|
| Anchor | The newest confirmed zigzag pivot **high** with a down impulse ≥ 3 × ATR; its AVWAP usable from confirmation; a newer confirmed zigzag high replaces it; invalidated at a roll. Long and short anchors are tracked independently (one of each may exist). |
| Impulse low | L = the **lowest low** from the anchor bar to the current bar, tracked until the pullback starts. |
| First pullback | The first bar whose **high** enters the zone **[AVWAP − 0.5 σ, AVWAP + `TouchLowerSigma` × σ]** (`TouchLowerSigma` is the far-side edge: below AVWAP for longs, **above** for shorts; default 0.25 σ). Fails, anchor done, on a 15m close **> AVWAP + 1 σ** or any **high > the anchor high** before a trigger. |
| Trigger and entry | A fractal pivot **high** (role `"lh"`) confirmed with **`EPivotLabel.LH`** and price **< anchor high**; **sell-stop** `AV02SE` = confirming bar **low − 1 tick**. Order life and window (09:45–15:00 ET) as the long. |
| Initial stop | **max(LH + 1 tick, AVWAP + 1 σ)** at the trigger bar, i.e. the farther of the two, widened **upward** to entry + cost floor; > 1% equity → skip; frozen. `AV02SS`. |
| Targets | `ImpulseHigh` mode, short: T1 = impulse **low** L (frozen at the trigger), buy-to-cover limit `AV02ST1`, ⌊q/2⌋, runner trailed. If entry ≤ L at the fill, T1 is skipped and the whole position trails, with the Info line `AV02 Info: T1-SKIPPED …` (Prog §7.2a, **Owner-approved 2026-11-11 09:52 ET**: *"Yes, trail the whole position, log an info message."*). The member keeps its name `ImpulseHigh` (= "the impulse extreme"). `Trail` mode as the long. |
| Break-even | None. |
| Trailing (EX-04) | After T1 or at **low ≤ entry − 1 R** (Trail mode): stop = **min(stop, anchor AVWAP + 2 ticks)** each closed bar, capped one tick above the last close; market exit on the first 15m close **> AVWAP + 2 ticks**. |
| Invalidation | Before T1: a 15m close **above the anchor high** → `AV02SX`. After the trail activates: the AVWAP-reclaim close above. |
| Adds (`OnNewPullback`) | A new confirmed 15m **LH** that holds **below AVWAP − 0.25 σ** (sell-stop below it) while last close **≤ average entry − `AddMinR` × R**; whole-position stop **lowered** to that **LH + 1 tick**, never raised; R re-base with `side = −1`; `AV02SA1`, `AV02SA2`. |
| Re-entry | One trade per anchor; trades per day per `TradesPerDay` (§5 Re-entry), counted across both sides for `OnePerInstrument`. |
| Time / session / news / holiday / roll | Side-blind; exits `AV02SX`. |
| MFE / MAE / risk %, reports | Prog §7.9 items 2 and 8. |
| Short-specific notes | A swing-high anchor after a sell-off is often followed by fast short-covering rallies through AVWAP; the 1 σ far-side stop and the "high > anchor high" fail rule bound this. |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` 2 (owner, 2026-11-10). Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide` | Both | — | enum | Fixed | Direction |
| `ImpulseAtr` | double | 3.0 | × ATR(20) anchor TF | fixed | Fixed (Cat.) | Zigzag reversal size = impulse |
| `TouchLowerSigma` | double | 0.25 | σ below AVWAP | {0.25, 0.50} | Tuned | Lower edge of the touch zone |
| `TouchUpperSigma` | double | 0.5 | σ above AVWAP | fixed | Fixed (Cat. rule line) | Upper edge |
| `TargetMode` | enum `ETargetMode { ImpulseHigh, Trail }` | ImpulseHigh | — | enum | Tuned | Target arm |
| `StopSigma` | double | 1.0 | σ | fixed | Fixed | AVWAP − kσ stop leg |
| `TrailOffsetTicks` | int | 2 | ticks | fixed | Fixed (EX-04) | Trail offset |
| `AddMode` | enum `EAddMode { None, OnNewPullback }` | None | — | enum | Fixed per registration | Allow adds (Prog §7.7) |
| `MaxAdds` / `AddContracts` / `AddMinR` | int / int / double | 2 / 1 / 1.0 | adds / contracts / R | 0–4 / 1–5 / 0.5–3 | Fixed (**PROPOSED**) | Add limits and the in-profit gate |
| `AddStopMode` | enum `EAddStopMode { RaiseToAddLow, KeepCurrent }` | RaiseToAddLow | — | enum | Fixed | Stop handling on an add fill |
| `TradesPerDay` | enum `ETradesPerDay { OnePerInstrument, OnePerSide, Unlimited }` | OnePerInstrument (**PROPOSED** default) | — | enum | Fixed per registration | Daily trade cap (**Owner-approved 2026-11-11 09:52 ET**) |
| `AnchorMaxAgeSessions` | int | 5 | sessions | 1–10 | Fixed (**PROPOSED**) | Anchor expiry |
| `EntryStartHHMM` 945, `EntryEndHHMM` 1500, `FlatHHMM` 1545, `SingleLotMode`, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct`, `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Batch-1 sweep = `TouchLowerSigma` {0.25, 0.5} × `TargetMode` {ImpulseHigh, Trail} = 4. The catalogue's third axis (anchor TF {15m, 60m}) becomes the separately registered 60m swing arm (Page), so the catalogue total of 8 is preserved across the two arms. The reading of the catalogue's "±0.25σ / ±0.5σ" sweep as the zone's lower edge is **PROPOSED – owner to confirm**.

### 8.1 Variants (true variations, all selectable via Inputs; default = primary)

A variant is an alternative rule, not a different value of a swept number (Prog §3 rules 8–9). Every variant below is a native `[Input]`. A true on/off switch is a `bool`. Every multi-value choice is a **typed C# enum** declared below, with named members only: no integer codes, no strings. The default is always the primary rule, and it is set in the constructor, never by member order. Enum `[Input]`s are already used by compiled repo studies (`_CustomSignal.TraceLevel` and others, Prog §3 rule 9). Whether the chart and Portfolio Trader optimizers can sweep an enum input is **not yet verified**; Coder checks it on the MiniPC (Prog §9). Inputs marked *sweep axis* are also part of the registered sweep. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants and are not listed.

| Variant | Input | Type | Values | Default (primary) | What it changes | Source |
|---|---|---|---|---|---|---|
| Target structure | `TargetMode` | enum `ETargetMode` | `ImpulseHigh`, `Trail` | `ImpulseHigh` | *Sweep axis.* ImpulseHigh = half at the impulse high, runner on the AVWAP trail; Trail = no fixed target, trail activates at +1 R | Cat. AV-02 |
| Adds | `AddMode` (with `MaxAdds`, `AddContracts`, `AddMinR`, `AddStopMode`) | enum `EAddMode`; int; int; double; enum `EAddStopMode` | `None`, `OnNewPullback`; 0–4; ≥ 1; R; `RaiseToAddLow`, `KeepCurrent` | `None` / 2 / 1 / 1.0 / `RaiseToAddLow` | Adds to a winner on the spec's own trigger; raises the whole-position stop | Prog §7.7; owner 22:06 ET |
| Single-contract handling | `SingleLotMode` | enum `ESingleLotMode` | `ExitAtT1`, `RunnerOnly` | `ExitAtT1` | When q = 1: exit at T1, or hold the single contract as a runner | Owner 2026-11-10; Prog §7.2 |
| Daily trade cap | `TradesPerDay` | enum `ETradesPerDay` | `OnePerInstrument`, `OnePerSide`, `Unlimited` | `OnePerInstrument` (**PROPOSED** default) | How many fills per instrument per day, across or per side | **Owner-approved 2026-11-11 09:52 ET** |

**Enum declarations** (nested `public enum` types in the strategy class; the input property uses the enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`):

```csharp
// Shared enums (Prog §3 rule 9), declared identically in every batch-1 file:
// ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
public enum EAddMode { None, OnNewPullback }              // default None (source does not add)
public enum EAddStopMode { RaiseToAddLow, KeepCurrent }     // default RaiseToAddLow
public enum ETargetMode { ImpulseHigh, Trail }       // default ImpulseHigh
public enum ETradesPerDay { OnePerInstrument, OnePerSide, Unlimited } // default OnePerInstrument (PROPOSED)
```

**Future variants (not built in batch 1, no input yet):** (1) the **60m/daily swing arm** (Page build item 4; §10), a separate registration, proposed `Arm` of type `public enum EArm { Intraday15m, Swing60m }` (default Intraday15m); (2) the **DV-12 blind-limit touch arm** (Dale), later. The numeric touch-edge sweep (`TouchLowerSigma`) stays a parameter.

### 8.2 Long / Short input pairs (**Owner-approved 2026-11-11 09:52 ET**, answer 4: *"Get their own values."*; Prog §7.9 item 12)
Each input below is replaced by a Long/Short pair. Wherever §4, §5, §5a, §8 or §8.1 name the single input, longs read the `…Long` member and shorts the `…Short` member. Short defaults equal the long defaults at first (**PROPOSED**), so the first run is symmetric; sweeps run per side (Prog §7.9 item 12, optimizer note). Inputs not listed stay single (side-blind).
| Single input (replaced) | Pair | Type | Default long / short | Range |
|---|---|---|---|---|
| `ImpulseAtr` | `ImpulseAtrLong` / `ImpulseAtrShort` | double | 3.0 / 3.0 | as the single input it replaces |
| `TouchLowerSigma` | `TouchLowerSigmaLong` / `TouchLowerSigmaShort` | double | 0.25 / 0.25 | as the single input it replaces |
| `TouchUpperSigma` | `TouchUpperSigmaLong` / `TouchUpperSigmaShort` | double | 0.5 / 0.5 | as the single input it replaces |
| `StopSigma` | `StopSigmaLong` / `StopSigmaShort` | double | 1.0 / 1.0 | as the single input it replaces |
| `TrailOffsetTicks` | `TrailOffsetTicksLong` / `TrailOffsetTicksShort` | int | 2 / 2 | as the single input it replaces |
| `AddMinR` | `AddMinRLong` / `AddMinRShort` | double | 1.0 / 1.0 | as the single input it replaces |

## 9. Logging
Prefix `AV-02`. `AV-02 ANCHOR tag(<_Avwap.Tag>) low() extremeTime() usable() atr()`; `AV-02 PULLBACK t() low() avwap() sigma()`; `AV-02 FAIL reason(close<avwap-1s|below-anchor)`. Skip reasons: `no-anchor`, `pullback-failed`, `no-hl`, `event-blackout`, `roll-window`, `risk-budget`, `after-cutoff`, `daily-limit`. `logSignal("AV02", …)` on every trigger, ctx with the P2 AVWAP, the SMA(bars since anchor) and the 50% retracement price of anchor→H.

## 10. Test plan
- Windows and costs: Prog §4–5; validation ends 31 Dec 2024 (fixed cut 2025-01-01, Prog §5.1, owner-approved). G0-a, G0-b. G0-d applies to the AVWAP level arm (Cat. §2.6 "AV level arms").
- **Controls (Cat. AV-02):** K-FL-anchor (AVWAP anchored at a random bar 20–60 bars before confirmation); K-SMA (SMA of length = bars since anchor); **50% retracement benchmark**: the identical HL rule with the touch zone at the 50% retracement of anchor → H (Tickfloor). Also M-AVWAP P2 (volume-shuffled) as a logged shadow. **Kill:** not better than the 50% retracement (Cat.); also the K-FL/K-SMA comparisons (Cat. §0: each AVWAP idea must beat a fake anchor and a plain SMA).
- **Later swing arm (separate registration, not batch 1):** Data1 60m, anchors from 60m zigzag lows (3 × ATR60), HL on 60m, overnight holding with AV-01's swing rules (max hold, roll flatten, weekend), K-FL-anchor at ±0.5 ATR20d. Needs owner sign-off before coding.
- **Short-side confirmation (Prog §7.9 item 11, owner-approved 09:52 ET):** the smoke run must produce short fills; review the first 20 short trades (or all if fewer) bar by bar against §5a (setup, trigger, order type and price, stop, T1, trail, exit reason); record `short-confirmed(<date>, n=<k>)` in the run sheet before any development read.

## 11. Acceptance criteria
Prog §6 + the AV-02 kill. `catalogue-default`.

## 12. Implementation checklist
- [ ] VERIFY M-AVWAP back-fill with zigzag pivots (§3); choose M-AVWAP or a local accumulator; record the choice in the CFG line.
- [ ] Two PRICEACTION instances: zigzag (anchor) and fractal (HL, role `"hl"`).
- [ ] Pullback state machine per anchor; trigger; stop/target/trail; 15m flatten alignment.
- [ ] Control shadows (P2, SMA, 50% retracement) in the event log.
- [ ] Off-MC tests: anchor replacement, first-pullback-only, failure conditions, trail monotonicity.
- [ ] `NewsExitMinutes` input and rule (Prog §7.6; PS-02 §12), with off-MC tests for the window edge (`MinutesTo` = N + B is not reached, N + B − ε is).
- [ ] Native compile + smoke.
- [ ] Header and short-branch comment `// SHORT SIDE UNCONFIRMED — testing must confirm short-side trades (Prog §7.9 item 11)` until the 20-trade short review is recorded.
- [ ] Long/Short input pairs per §8.2 (short defaults = long defaults); `T1-SKIPPED` rule and Info line (Prog §7.2a) where the spec has a T1.

## 13. Header mapping
Trigger: after a confirmed ≥ 3 ATR impulse from a 15m low, the first pullback into AVWAP − 0.25 σ … AVWAP + 0.5 σ that prints a confirmed HL above the anchor low → buy-stop above the HL bar. Management: stop at the farther of HL − 1 tick and AVWAP − 1 σ (floored); half off at the impulse high; runner trailed at AVWAP − 2 ticks. Exit: stop, T1, a 15m close below AVWAP − 2 ticks after T1, a close below the anchor low, or flat at 15:45 ET.

## 14. Open questions for Ettienne
1. Single 15m stream for the intraday arm.
2. Touch-zone sweep read as the lower edge {0.25 σ, 0.5 σ}, upper edge fixed at 0.5 σ.
3. Impulse high then trail = half at H, runner on the AVWAP trail; Trail arm activates at +1 R.
4. Entry cut-off 15:00 ET and flatten at the 15:45 bar; anchor expiry 5 sessions; one trade per day.
5. Anchor-low invalidation before T1.
6. Prog §10 batch-wide items.
7. ~~**Short side (rev 1.6, §5a):** accept the PROPOSED swing-high mirror with identical numbers (3 × ATR impulse, touch zone with the 0.25 σ edge above AVWAP, 1 σ stop, anchor-high invalidation)?~~ **Owner-approved 2026-11-11 09:52 ET**: accepted for now; testing must confirm the short trades.
8. ~~One trade per instrument per day across both sides (a long and a short anchor can both be live on one day)? PROPOSED: yes, the first fill uses the day.~~ **Owner-approved 2026-11-11 09:52 ET**: typed-enum input `TradesPerDay`, default `OnePerInstrument` (PROPOSED default).

## 15. Sources
Cat. AV-02 (+ DV-12), §0 item 3, §2.1, §2.2, §2.6, EX-04; Shannon, *Maximum Trading Gains with Anchored VWAP* (practitioner, via Cat. v1 AV family prior); final-synthesis (Tickfloor 50% retracement benchmark via R1); engines manual §4.7 (M-AVWAP), §4.8 (PRICEACTION), §5.23; Strategy-planning Page (item 4).
