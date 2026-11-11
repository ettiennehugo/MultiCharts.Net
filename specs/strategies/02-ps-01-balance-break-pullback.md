# PS-01 · Accepted balance break → first-pullback higher low (ABB-HL) — implementation spec

**Rev 1.0 (draft), 10 Nov 2026 (EST).** Batch 1, build order #2. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Sibling: [01-ps-02-balance-break-close.md](01-ps-02-balance-break-close.md) (*PS-02 spec*). Catalogue: § PS-01. Target file: `sandbox/_PS_01_BalanceBreakPullback.Strategy.CS`, class `_PS_01_BalanceBreakPullback`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** Same trapped participants as PS-02. Waiting for the first pullback that holds above the old edge and prints a higher low lets the market show that the old balance edge has become support (Cat. PS-01). Keller B1 card 3.3: "escape is not entry; accept or retest." Keller B4 §8.3: "every behaviour that performs well waits."
- **Evidence.** Family **C (+)** (Keller B4 §2.4 base rates on FX/crypto; Telonics: real levels are run through +3.7 to +11.6 pp more often than fakes). **The setup itself is untested.** Final-synthesis Spec 1 notes the pullback entry alone looks random in the literature (breakout-pullback 52.1% vs 51.7%, Krueger via R4), so the edge must be carried by the acceptance gate. That is exactly what the PS-02 comparison tests.
- **Question this host answers:** does waiting for the HL add net R over the PS-02 acceptance-close entry on the **same** break events (final-synthesis §6.6 rung L3)?

## 2. Instruments and timeframes
@ES, then @NQ; CL/GC later (Cat. PS-01); 6E after M8/M9. Data1 = @ES 5-minute, dxFeed continuous unadjusted, 24h Globex template (Example 26). Composite from n completed Own RTH sessions. 1-minute trigger: **not** in the primary. The catalogue allows "1m/5m HL"; Example 26 uses 5m PRICEACTION pivots; final-synthesis Spec 1's 1m BOS trigger is a later registered ablation. Warm-up and roll warm-up as PS-02.

## 3. Engines
As PS-02 §3, minus the entropy veto (PS-01 has none) and M-DAILY. Plus PRICEACTION used for the **entry** (layer-0 confirmed pivot low labelled `EPivotLabel.HL`, Futures preset, `dataStream = 1`). PS-01 shares PS-02's break/acceptance detector: same composite, δ, V, window and acceptance counter, so both specs produce the identical event cohort for a given n and acceptance count.

## 4. Entry rules (long; short mirrors with LH)
1. **Daily setup:** `IsBalanced(1, n, 0.6)` at the prior close; W > 0; V ≥ +1 for longs (≤ −1 for shorts).
2. **Break and acceptance:** between 09:45 and 14:30 ET, `AcceptCloses` consecutive closed 5m bars with close > comp.vah + δ, δ = max(2 ticks, 0.10 × ATR5). This sets `side = +1` and records the acceptance time.
3. **Hold zone:** hold = comp.vah − 0.25 × W. If any later 5m bar's **low** < hold before entry, the setup is cancelled (`SKIP pullback-too-deep`), as in Example 26.
4. **Trigger:** after acceptance, the first PRICEACTION layer-0 pivot low that is confirmed on this bar (`NewPivotLow`) with `label == EPivotLabel.HL`. Planned entry = that confirming bar's high + 1 tick. Send a buy-stop `PS01LE`.
5. **Order life (PROPOSED – owner to confirm):** the buy-stop is re-sent on every closed bar until it fills, the setup is cancelled (step 3), or the bar time passes 14:30 ET. If a newer HL is confirmed before fill (still above hold), it replaces the trigger and stop (Example 26 behaviour). The catalogue does not state an expiry; final-synthesis Spec 1 uses "valid for 2 bars" for its 1m trigger.
6. **Not on:** a roll-window day, during `EventBlackout`, or after the day's single attempt is used.

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | HL pivot price − 1 tick (structural). **Minimum:** the cost floor from the planned entry (ES 14 ticks, NQ 19, CL 15, 6E 18). If the structural distance is smaller, the stop is widened to entry − floor (M7). **Maximum:** none; a widened risk > 1% equity skips the trade. | Cat. PS-01; Example 26; Cat. §2.1 |
| **Target** | One target: limit at comp.vah + 1.0 × W, full position (`PS01LT`). Registered variants (sweep): next composite HVN − 2 ticks (EX-01), or no fixed target with an EX-05 trail. | Cat. PS-01 sweep |
| **Scale-out** | None in PS-01 (single target, full exit). 1 contract and q contracts behave identically. | Cat. PS-01 |
| **Break-even** | When a closed bar's high ≥ entry + 1 R (R = |entry − widened stop|), stop → entry + 0 ticks from the next bar. Offset 0 is the catalogue's "break-even"; **PROPOSED – owner to confirm** offset 0 (costs make BE a small net loss). | Cat. PS-01; Example 26 |
| **Trailing** | Primary: none (fixed target). Trail variant: EX-05 from the fill, candidate = latest confirmed 5m HL after entry − 1 tick, one-way (Prog §7.3). | Cat. PS-01 sweep; EX-05 |
| **Time stop / max hold** | Intraday. FlatBy 15:55 ET; early close: close − 5 min. No overnight or weekend holds. | Cat. §2.1 |
| **Invalidation** | Hard invalidation: a closed 5m bar with close < comp.vah − δ → `PS01LX` market next bar (the acceptance failed). Applies from the fill. | Cat. PS-01; EX-10 |
| **Adds** | None. | Prog §7.7 |
| **Re-entry after a stop** | None: one attempt per day per instrument. | Example 26 |
| **News / holiday / roll** | As PS-02 §5: entries blocked in `EventBlackout`; hold through news with the stop (**PROPOSED**); early-close cut-off (**PROPOSED**); no entry on roll-window days; composite rebuilt after a splice. | Prog §7.6 |
| **Orders and fills** | Entry buy-stop (fills at the trigger + MC slippage, or at the open if the bar gaps through). Stop `PS01LS`, target limit `PS01LT` (trade-through), market exits `PS01LX`. Re-sent every bar. Magnifier 1m; stop-first on unresolved bars. | Prog §4, §7.1 |
| **MM interaction** | As PS-02 §5. The entry is a pending stop, so `sizeForRisk`/`portfolioArm` is called on **every bar** the stop is pending (manual §5.23: the base clears `pm.side` on bars where you did not arm). | Prog §7.5 |
| **Same-bar precedence** | MM force-close → stop → target → FlatBy/early close → invalidation (close-based) → BE update for next bar. If the invalidation close and the BE trigger happen on one bar, the invalidation exit wins. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6 (`PortfolioFamily = "PS"`). Default `Contracts` = 1 is enough here because there is no partial. **PROPOSED – owner to confirm** (use 2 if PS-01 and PS-02 must share identical size for the comparison; R-based comparison makes size irrelevant, so 1 is proposed).

## 7. Session, news, holiday and roll handling
As PS-02 §7 and Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose / effect |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide` | Both | — | enum | Fixed | Direction filter |
| `CompositeSessions` | int | 3 | sessions | {3, 5} (else Error) | Tuned | n of the balance composite |
| `AcceptCloses` | int | 2 | 5m closes | {1, 2, 3} | Tuned | Acceptance before the pullback is watched |
| `TargetMode` | enum `ETargetMode { BalanceWidth, NextHvn, TrailEx05 }` | BalanceWidth | — | enum | Tuned | Target variant (sweep) |
| `TargetW` | double | 1.0 | × W | fixed | Fixed | BalanceWidth distance |
| `HoldW` | double | 0.25 | × W | fixed | Fixed | Depth the pullback may reach below the edge |
| `BreakEvenR` | double | 1.0 | R | fixed (0 = off is a new trial) | Fixed | BE trigger |
| `BalanceOverlapPct`, `DeltaMinTicks`, `EntryStartHHMM` 945, `EntryEndHHMM` 1430, `FlatHHMM` 1555, `Contracts` 1, `RiskFractionPct` 0.5, `MaxRiskPct` 1.0, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct` 10, `OutlierRemovalPct` 1 | as PS-02 §8 | | | | | |

Registered sweep = 2 × 3 × 3 = 18 configs (Cat. PS-01). Primary frozen: n = 3, acceptance 2, BalanceWidth.

## 9. Logging
Prog §8, prefix `PS-01`. Setup fields: `W, vah, val, V, delta, acceptTime`; trigger fields `hlPrice, hlConfirmTime, trigger`. Skip reasons: as PS-02 plus `pullback-too-deep`, `trigger-expired`, `no-hl`. `logSignal("PS01", …)` for every accepted break (taken or not) **and** a second tag `PS01-HL` for every armed trigger, so the event study can compare PS-01 vs PS-02 on identical events.

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
- [ ] Stop = HL − 1 tick, floored; BE at +1 R; invalidation close; FlatBy.
- [ ] `TargetMode` branches: BalanceWidth limit; NextHvn = first composite HVN above entry (F-HVN on the composite) − 2 ticks, skip if < 0.75 R away (EX-01 rule); TrailEx05.
- [ ] Off-MC tests: hold-zone cancel, trigger replacement, BE, invalidation precedence.
- [ ] Native compile and smoke run as PS-02.

## 13. Header mapping
Trigger: after a 2-close acceptance above the balanced composite (V ≥ +1), the first confirmed 5m HL that stays above VAH − 0.25 W → buy-stop 1 tick above the HL's confirming bar. Management: stop 1 tick under the HL (floored); BE at +1 R. Exit: target VAH + 1 W, stop, a 5m close back below VAH − δ, or flat 15:55 ET.

## 14. Open questions for Ettienne
1. Buy-stop life: valid until 14:30 ET with newer-HL replacement (Example 26), or a fixed 2-bar expiry (final-synthesis)?
2. Break-even offset 0 ticks.
3. `Contracts` 1 for PS-01 (no partial).
4. Prog §10 batch-wide items.

## 15. Sources
Cat. PS-01, PS-02, EX-01, EX-05, EX-10, §2.1–2.3; Keller B1 card 3.3; Keller B4 §2.4, §8.3, §8.4; final-synthesis Spec 1 and §6.6; engines manual §3.26 (Example 26), §4.8, §5.23; Strategy-planning Page.
