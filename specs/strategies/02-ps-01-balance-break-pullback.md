# PS-01 · Accepted balance break → first-pullback higher low (ABB-HL) — implementation spec

**Rev 1.4 (draft), 10 Nov 2026 (EST)** — §8.1 Variants added: every true variation is selectable via Inputs, default = primary (owner, 20:31 ET). Multi-value inputs are typed C# enums, not ints or strings (owner, 20:34 ET; Prog §3 rules 8–9).

**Rev 1.3 (draft), 10 Nov 2026 (EST)** — Kindle checks of Keller B1 Ch 3.3 and Keller B4 §2.4/§8.4 applied (Kindle locations; quotes read from page images, may differ slightly). The stop now goes through the balance POC; the target is a scale-out at 1 W with a runner. Rev 1.1: costs, 2 contracts, news exit, holdout. Batch 1, build order #2. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Sibling: [01-ps-02-balance-break-close.md](01-ps-02-balance-break-close.md) (*PS-02 spec*). Catalogue: § PS-01. Target file: `sandbox/_PS_01_BalanceBreakPullback.Strategy.CS`, class `_PS_01_BalanceBreakPullback`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** Same trapped participants as PS-02. Waiting for the first pullback that holds above the old edge and prints a higher low lets the market show that the old balance edge has become support (Cat. PS-01). Keller B1 card 3.3: "escape is not entry; accept or retest." Keller B4 §8.3: "every behaviour that performs well waits."
- **Keller B1 Ch 3.3 "The Balance-Break Setup" (Kindle Loc ~951–959): PS-01 = Keller entry (b), the retest.** Quote: "price returning to the broken edge/POC edge, holding it as new support/resistance, entered there with a tighter stop through the balance POC". Targets: "a measured objective (balance width projected) and/or the next composite HVN". Failed break: accepted back inside value means "the setup inverts". The rest of Keller's acceptance checklist is in PS-02 §1. Checked, not specified in Keller B1: closes, buffer, order life, break-even.
  - Keller says the entry is made *at* the held edge. The catalogue's confirmed 5m HL above the hold zone is the "holding it" evidence and stays the primary trigger. A resting limit at the edge is a possible future variant.
- **Keller B4 §2.4 / §8.4 Rule 4 (FX/crypto, not futures):** "scale at one range width. Run the remainder", against "the nearest destination" (steps 7–8; Rule 2, levels are destinations). This applies to PS-01's exits (§5).
- **Evidence.** Family **C (+)** (Keller B4 §2.4 base rates on FX/crypto; Telonics: real levels are run through +3.7 to +11.6 pp more often than fakes). **The setup itself is untested.** Final-synthesis Spec 1 notes the pullback entry alone looks random in the literature (breakout-pullback 52.1% vs 51.7%, Krueger via R4), so the edge must be carried by the acceptance gate. That is exactly what the PS-02 comparison tests.
- **Question this host answers:** does waiting for the HL add net R over the PS-02 acceptance-close entry on the **same** break events (final-synthesis §6.6 rung L3)?

## 2. Instruments and timeframes
@ES, then @NQ; CL/GC later (Cat. PS-01); 6E after M8/M9. Data1 = @ES 5-minute, dxFeed continuous unadjusted, 24h Globex template (Example 26). Composite from n completed Own RTH sessions. 1-minute trigger: **not** in the primary. The catalogue allows "1m/5m HL"; Example 26 uses 5m PRICEACTION pivots; final-synthesis Spec 1's 1m BOS trigger is a later registered ablation. Warm-up and roll warm-up as PS-02.

## 3. Engines
As PS-02 §3, minus the entropy veto (PS-01 has none) and M-DAILY. Plus PRICEACTION used for the **entry** (layer-0 confirmed pivot low labelled `EPivotLabel.HL`, Futures preset, `dataStream = 1`). PS-01 shares PS-02's break/acceptance detector: same composite, δ, V, window and acceptance counter, so both specs produce the identical event cohort for a given n and acceptance count.

## 4. Entry rules (long; short mirrors with LH)
1. **Daily setup:** `IsBalanced(1, n, 0.6)` at the prior close; W > 0; **V ≥ +1 for longs (≤ −1 for shorts): the value-migration alignment filter, default** (Cat. PS-01; Keller B4 §8.4 Rule 4 and steps 7–8; existing `ValueMigrationScore`).
2. **Break and acceptance:** between 09:45 and 14:30 ET, `AcceptCloses` consecutive closed 5m bars with close > comp.vah + δ, δ = max(2 ticks, 0.10 × ATR5). This sets `side = +1` and records the acceptance time. The count and δ stay **PROPOSED** (Keller gives qualitative criteria only; checked: not specified in Keller B1 Ch 3.3). The optional `AcceptFlowCheck` (developing POC ≥ comp.poc and ≥ 25% of RTH volume beyond the edge, off by default) is shared with PS-02 §4 step 4, so both hosts keep the same cohort.
3. **Hold zone:** hold = comp.vah − 0.25 × W. If any later 5m bar's **low** < hold before entry, the setup is cancelled (`SKIP pullback-too-deep`), as in Example 26.
4. **Trigger:** after acceptance, the first PRICEACTION layer-0 pivot low that is confirmed on this bar (`NewPivotLow`) with `label == EPivotLabel.HL`. Planned entry = that confirming bar's high + 1 tick. Send a buy-stop `PS01LE`.
5. **Order life (PROPOSED – owner to confirm):** the buy-stop is re-sent on every closed bar until it fills, the setup is cancelled (step 3), or the bar time passes 14:30 ET. If a newer HL is confirmed before fill (still above hold), it replaces the trigger and stop (Example 26 behaviour). The catalogue does not state an expiry; final-synthesis Spec 1 uses "valid for 2 bars" for its 1m trigger.
6. **Not on:** a roll-window day, during `EventBlackout`, or after the day's single attempt is used.

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | `StopMode = BalancePoc` (**primary**, per Keller B1 Ch 3.3: "tighter stop through the balance POC"): stop = comp.poc − `StopBufferTicks` (1, **PROPOSED**; checked: not specified in Keller B1). Variant `StopMode = HlPivot` (Cat. PS-01 / Example 26): HL pivot − 1 tick, kept as a registered comparison because it is the catalogue host's stop. **Minimum:** the cost floor from the planned entry (ES 14 ticks, NQ 19, CL 15, 6E 18; widened by M7). **Maximum:** none; a widened risk > 1% equity skips the trade. The POC stop is usually wider than the HL stop, so more trades may hit the risk-budget skip; that count is reported. | Keller B1 Ch 3.3; Cat. PS-01; Example 26; Cat. §2.1 |
| **Targets** | `TargetMode = BalanceWidth` (primary): **T1 = comp.vah + 1.0 × W** for ⌊q/2⌋ contracts (`PS01LT1`; Keller 3.3 measured objective). The **runner** gets a limit at the **nearest destination** beyond T1 (`RunnerTarget = NearestDestination`; `TrailOnly` drops the limit): the nearer of the next composite HVN low edge (Keller 3.3 "next composite HVN") and the nearest naked POC (Keller B4 Rule 2), minus 2 ticks (offset **PROPOSED**), order `PS01LT`. If neither exists, the runner has no limit. After T1 the runner is also trailed (EX-05, Prog §7.3) until the destination, the stop or the flatten. q = 1: `SingleLotMode = ExitAtT1` (owner-accepted). Sweep variants kept: `NextHvn` (whole position at the next composite HVN − 2 ticks, EX-01) and `TrailEx05` (no fixed target). | Keller B1 Ch 3.3; Keller B4 §2.4, §8.4 Rule 4 and steps 7–8; Cat. PS-01 sweep |
| **Scale-out** | At T1, ⌊q/2⌋ off (test size 2 → 1 + 1); the runner as above. This replaces rev 1.1's single full exit at 1 W, which Keller B4 §2.4 says "truncates the part of the distribution that pays for the failures". **Registration change, **Owner-approved 2026-11-10 20:31 ET** (Ettienne: *"These changes are fine"*):** the catalogue's PS-01 primary was a full exit at 1 W; the POC stop and the scale-out with a runner are now the primary. | Keller B4 §2.4; Prog §7.2 |
| **Break-even** | When a closed bar's high ≥ entry + 1 R (R = \|entry − widened stop\|), stop → entry + 0 ticks from the next bar. Offset 0 is the catalogue's "break-even"; **PROPOSED – owner to confirm** offset 0 (checked: not specified in Keller B1 Ch 3.3). | Cat. PS-01; Example 26 |
| **Trailing** | Runner only, after T1: EX-05, candidate = latest confirmed 5m HL after entry − 1 tick, one-way (Prog §7.3); Keller 3.3 trend day: "trail, don't fade your own position". `TrailEx05` variant: the same trail on the whole position from the fill. | Keller B1 Ch 3.3; Cat. PS-01 sweep; EX-05 |
| **Time stop / max hold** | Intraday. FlatBy 15:55 ET; early close: close − 5 min. No overnight or weekend holds. | Cat. §2.1 |
| **Invalidation: failed-break exit** | `FailCloses` (1, the catalogue value) closed 5m bar(s) with close < comp.vah − δ → `PS01LX` market next bar, reason `INVALIDATION`. Keller: accepted back inside value means "the setup inverts". Applies from the fill, before and after T1. The opposite-edge reversal is a **future variant**. | Cat. PS-01; EX-10; Keller B1 Ch 3.3 |
| **Adds** | None. No add input: this is a single break/retest or first-test setup, so adds are not natural here (Prog §7.7, rev 1.5). | Prog §7.7 |
| **Re-entry after a stop** | None: one attempt per day per instrument. | Example 26 |
| **News** | Entry blackout: no entry while `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm** the default). When > 0, at the last bar close before release − N (rule `0 < MinutesTo(EEventTier.T1) < N + bar minutes`, Prog §7.6) the strategy flattens with `PS01LX`, reason `NEWS`, stops arming entries until release + 10 min, and the setup's attempt is used. When ≤ 0, an open trade holds through the release with its stop. | Owner 2026-11-10; Cat. §2.1; Prog §7.6 | 
| **Holiday / roll** | As PS-02 §5: early-close cut-off (**PROPOSED**); no entry on roll-window days; composite rebuilt after a splice. A working buy-stop is cancelled (not re-sent) once the news window is reached. | Prog §7.6 |
| **Orders and fills** | Entry buy-stop (fills at the trigger + MC slippage, or at the open if the bar gaps through). Stop `PS01LS`, T1 limit `PS01LT1` and runner destination limit `PS01LT` (trade-through), market exits `PS01LX`. Re-sent every bar. Magnifier 1m; stop-first on unresolved bars. | Prog §4, §7.1 |
| **MM interaction** | As PS-02 §5. The entry is a pending stop, so `sizeForRisk`/`portfolioArm` is called on **every bar** the stop is pending (manual §5.23: the base clears `pm.side` on bars where you did not arm). | Prog §7.5 |
| **Same-bar precedence** | MM force-close → stop → target → news exit → FlatBy/early close → invalidation (close-based) → BE update for next bar. If the invalidation close and the BE trigger happen on one bar, the invalidation exit wins. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6 (`PortfolioFamily = "PS"`). `Contracts` = 2 (owner, 2026-11-10): contract 1 at T1 (1 W), contract 2 runs to the nearest destination or the trail. Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
As PS-02 §7 and Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose / effect |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide` | Both | — | enum | Fixed | Direction filter |
| `CompositeSessions` | int | 3 | sessions | {3, 5} (else Error) | Tuned | n of the balance composite |
| `AcceptCloses` | int | 2 | 5m closes | {1, 2, 3} | Tuned | Acceptance before the pullback is watched |
| `TargetMode` | enum `ETargetMode { BalanceWidth, NextHvn, TrailEx05 }` | BalanceWidth | — | enum | Tuned | Target variant (sweep) |
| `TargetW` | double | 1.0 | × W | fixed | Fixed (Keller B4 §8.4) | T1 distance |
| `StopMode` | enum `EStopMode { BalancePoc, HlPivot }` | BalancePoc | — | enum | Fixed per registration | Keller retest stop vs catalogue HL stop |
| `StopBufferTicks` | int | 1 | ticks | 0–4 | Fixed (**PROPOSED**) | Distance through the POC |
| `RunnerTarget` | enum `ERunnerTarget { TrailOnly, NearestDestination }` | NearestDestination | — | enum | Fixed per registration | Runner limit at the nearest destination, or trail only (BalanceWidth mode) |
| `DestOffsetTicks` | int | 2 | ticks | 0–8 | Fixed (**PROPOSED**) | Runner limit before the destination |
| `FailCloses` | int | 1 | 5m closes | 1–3 | Fixed (Cat. PS-01) | Failed-break exit |
| `AcceptFlowCheck` / `VolBeyondPct` | bool / double | false / 25 | — / % | as PS-02 | Fixed (ablation) | Shared with PS-02 |
| `HoldW` | double | 0.25 | × W | fixed | Fixed | Depth the pullback may reach below the edge |
| `BreakEvenR` | double | 1.0 | R | fixed (0 = off is a new trial) | Fixed | BE trigger |
| `BalanceOverlapPct`, `DeltaMinTicks`, `EntryStartHHMM` 945, `EntryEndHHMM` 1430, `FlatHHMM` 1555, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct` 0.5, `MaxRiskPct` 1.0, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct` 10, `OutlierRemovalPct` 1 | as PS-02 §8 | | | | | |

Registered sweep = 2 × 3 × 3 = 18 configs (Cat. PS-01). Primary frozen: n = 3, acceptance 2, BalanceWidth (now scale + runner), `StopMode` BalancePoc. The `HlPivot` stop runs as one extra pre-registered comparison on the primary cell only (it is not multiplied into the sweep).

### 8.1 Variants (true variations, all selectable via Inputs; default = primary)

A variant is an alternative rule, not a different value of a swept number (Prog §3 rules 8–9). Every variant below is a native `[Input]`. A true on/off switch is a `bool`. Every multi-value choice is a **typed C# enum** declared below, with named members only: no integer codes, no strings. The default is always the primary rule, and it is set in the constructor, never by member order. Enum `[Input]`s are already used by compiled repo studies (`_CustomSignal.TraceLevel` and others, Prog §3 rule 9). Whether the chart and Portfolio Trader optimizers can sweep an enum input is **not yet verified**; Coder checks it on the MiniPC (Prog §9). Inputs marked *sweep axis* are also part of the registered sweep. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants and are not listed.

| Variant | Input | Type | Values | Default (primary) | What it changes | Source |
|---|---|---|---|---|---|---|
| Stop placement | `StopMode` | enum `EStopMode` | `BalancePoc`, `HlPivot` | `BalancePoc` (owner-approved 20:31 ET) | HlPivot = HL pivot − 1 tick (catalogue / Example 26) instead of comp.poc − `StopBufferTicks` | Keller B1 Ch 3.3; Cat. PS-01 |
| Target structure | `TargetMode` | enum `ETargetMode` | `BalanceWidth`, `NextHvn`, `TrailEx05` | `BalanceWidth` (half at 1 W + runner, owner-approved) | *Sweep axis.* NextHvn = whole position at the next composite HVN − 2 ticks; TrailEx05 = no fixed target, EX-05 trail from the fill | Cat. PS-01 sweep; EX-01; EX-05 |
| Runner limit | `RunnerTarget` | enum `ERunnerTarget` | `TrailOnly`, `NearestDestination` | `NearestDestination` | Same input name as PS-02, but a different default: in BalanceWidth mode the runner keeps or drops the destination limit; the trail always applies | Keller B1 Ch 3.3; Keller B4 §8.4 |
| Acceptance flow check | `AcceptFlowCheck` (threshold `VolBeyondPct`) | bool | true / false | false | As PS-02 §8.1 (shared cohort) | Keller B1 Ch 3.3 |
| Single-contract handling | `SingleLotMode` | enum `ESingleLotMode` | `ExitAtT1`, `RunnerOnly` | `ExitAtT1` | When q = 1: exit at T1, or hold the single contract as a runner | Owner 2026-11-10; Prog §7.2 |

**Enum declarations** (nested `public enum` types in the strategy class; the input property uses the enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`):

```csharp
// Shared enums (Prog §3 rule 9), declared identically in every batch-1 file:
// ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
public enum EStopMode { BalancePoc, HlPivot }          // default BalancePoc
public enum ETargetMode { BalanceWidth, NextHvn, TrailEx05 } // default BalanceWidth
public enum ERunnerTarget { TrailOnly, NearestDestination } // same declaration as PS-02; default NearestDestination here
```

**Future variants (not built in batch 1, no input yet):** (1) **opposite-edge reversal** after a failed break (Keller 3.3), proposed `FailedBreakReverse` (bool, default false); (2) Keller's resting **limit at the held edge** instead of the HL buy-stop, proposed `EntryMode` of type `public enum EEntryMode { HlBuyStop, EdgeLimit }` (default HlBuyStop); (3) Trader Oxygen YV-01 rules, not yet checked.

## 9. Logging
Prog §8, prefix `PS-01`. Setup fields: `W, vah, val, V, delta, acceptTime`; trigger fields `hlPrice, hlConfirmTime, trigger`. Exit lines carry R, MFE and MAE (Prog §8; Keller B4 §8.4 step 9). Skip reasons: as PS-02 plus `pullback-too-deep`, `trigger-expired`, `no-hl`. `logSignal("PS01", …)` for every accepted break (taken or not) **and** a second tag `PS01-HL` for every armed trigger, so the event study can compare PS-01 vs PS-02 on identical events.

## 10. Test plan
- Windows, gates, costs, Holm family: as PS-02 §10.
- **Controls (Cat. PS-01):** K-FL (fake composite edges), K-RT, a no-acceptance version (enter at the first close beyond the edge), and PS-02's acceptance-close entry on the same events.
- **Kill (Cat. PS-01):** no lift over the PS-02 at-acceptance entry, or < 56% reach of 1.0 W net of slippage.
- Later registered ablations (not in batch 1): final-synthesis Spec 1's 1m BOS trigger, 25% volume-above-edge rule, delta-z ratification (OF-02, W-T only), A2-15 skew layer, EX-04 AVWAP trail (A2-03 primary arm on this host). One at a time.

## 11. Acceptance criteria
Prog §6 + the Cat. PS-01 kill. `catalogue-default`.

## 12. Implementation checklist
- [ ] Reuse PS-02's daily setup and acceptance logic verbatim (copy, or a shared sandbox helper class with a unique name).
- [ ] Pending buy-stop life cycle (step 5) with re-arm every bar in PT.
- [ ] Stop per `StopMode` (comp.poc − buffer, or HL − 1 tick), floored; BE at +1 R; failed-break exit; FlatBy.
- [ ] Scale-out at T1 plus the runner destination (composite HVN / naked POC, `TrackNakedPoc = true`) and the EX-05 runner trail.
- [ ] `TargetMode` branches: BalanceWidth limit; NextHvn = first composite HVN above entry (F-HVN on the composite) − 2 ticks, skip if < 0.75 R away (EX-01 rule); TrailEx05.
- [ ] Off-MC tests: hold-zone cancel, trigger replacement, BE, invalidation precedence.
- [ ] `NewsExitMinutes` input and rule (Prog §7.6; PS-02 §12), with off-MC tests for the window edge (`MinutesTo` = N + B is not reached, N + B − ε is).
- [ ] Native compile and smoke run as PS-02.

## 13. Header mapping
Trigger: after a 2-close acceptance above the balanced composite (V ≥ +1), the first confirmed 5m HL that stays above VAH − 0.25 W → buy-stop 1 tick above the HL's confirming bar. Management: stop 1 tick through the balance POC (floored); BE at +1 R; half off at VAH + 1 W; runner to the next composite HVN or naked POC, trailed under new HLs. Exit: stop, targets, a 5m close back below VAH − δ, or flat 15:55 ET.

## 14. Open questions for Ettienne
1. Buy-stop life: valid until 14:30 ET with newer-HL replacement (Example 26), or a fixed 2-bar expiry (final-synthesis)?
2. Break-even offset 0 ticks (Keller: not stated).
3. ~~POC stop primary and scale-out with runner~~: owner-approved 2026-11-10 20:31 ET. Still open: POC stop buffer 1 tick; destination offset 2 ticks.
4. Prog §10 batch-wide items.

## 15. Sources
Cat. PS-01, PS-02, EX-01, EX-05, EX-10, §2.1–2.3; Keller Book 1 Ch 3.3 (Kindle Loc ~951–959); Keller Book 4 §2.4 (Loc ~721–838), §8.3, §8.4 Rules 2 and 4, steps 7–9; final-synthesis Spec 1 and §6.6; engines manual §3.26 (Example 26), §4.8, §5.23; Strategy-planning Page.
