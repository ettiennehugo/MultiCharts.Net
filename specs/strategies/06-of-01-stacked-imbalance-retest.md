# OF-01 · Stacked-imbalance origin: first retest continuation — implementation spec

**Rev 1.1 (draft), 10 Nov 2026 (EST)** — owner answers of 19:44 ET applied: holdout re-scoped (Prog §5.1), 2 contracts, news exit, costs. Batch 1, build order #6. **Data-gated** on tick-data availability only (see §10). Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § OF-01. Reference code: manual Example 38 (`_Ex38StackedImbalanceRetest`, limit arm only, with placeholders). Owner shell: `_StackedImblContLELX.Strategy.CS` (header text only, empty methods; its rules differ, see §1). Target file: `sandbox/_OF_01_StackedImbalanceRetest.Strategy.CS`, class `_OF_01_StackedImbalanceRetest`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** Three or more consecutive diagonal buy imbalances mean aggressive buyers lifted the offer through several prices. If they are still positioned, the first return to that zone is defended (Cat. OF-01).
- **Dale, *Order Flow: Trading Setups*, Setup #4 (p.76, Kindle notes):**
  - imbalance = Ask ≥ 300% of the diagonal Bid
  - ≥ 3 stacked
  - price moves away ≥ 1–2 whole footprints
  - wait for the pullback; long on a hit from above
  - **first test only** ("2nd test probability much lower")
  - best within or before a trend
  - Dale gives no win rates and no stop or target for this setup.
- **Dale stop chapter (pp.129–137 notes):** stop beyond the S/R zone extreme. **Skip the trade if the stop exceeds 10–20% of daily ATR.** Trail only while imbalances keep appearing on your side.
- **Keller B1 card 6.3 (notes):**
  - The stack must have escaped something (balance edge / LVN).
  - The pullback to the origin is corrective. First retest only, same session.
  - Entry on renewed same-side aggression. Stop through the origin.
  - Targets: the stack extreme, then the next reference.
  - A "sliced" origin traps the continuation crowd, so expect a full retrace.
  - Results flip by regime (trend vs balance), so stratify.
- **Owner shell `_StackedImblContLELX`:** a stop entry after a bullish close that fails to break the zone low, or a limit at mid-zone on the 2nd/3rd test; reward factor 1.5–3. These are **different rules** (a later test, other entry prices). They are not used here and would be a separate registered entry (open question 7).
- **Evidence:** grade **E/D**, untested, book only. No statistics are claimed.

## 2. Instruments and timeframes
- @ES first, then @NQ. CL/GC later (Cat.).
- **Data1 = @ES 5-minute**, dxFeed continuous unadjusted, with tick-level Bid/Ask footprints (`OrderFlowEnabled = true`, `FlowCfg.McBidAsk = _OrderFlow.EMcBidAsk.Prefer`).
- Zone formation, ATR5 (ATR14 on 5m) and orders all use Data1.
- **1m HL arm (`EntryArm = HL`):** needs 1-minute bars for the trigger. **VERIFY:** can F-OF build 5m-zone footprints while the chart runs on 1m (aggregate five 1m ladders)?
  - If yes: Data1 = 1m for orders and the HL trigger, with zones from five-bar aggregates.
  - If not: the HL arm uses a **5m** fractal HL and is labelled a deviation.
  - Coder confirms this before building the arm.
- Bar Magnifier: tick detail (inside the tick window only).

## 3. Engines
| Need | Engine / member |
|---|---|
| Stacked imbalances | `_VolumeProfileFuturesBase.StackedImbalances(barsAgo, ratio, minVol, minStack)` → `_OrderFlow.StackedResult` (`Found`, `Quality`, `Runs` [lo, hi, …, side]) |
| Volume floor P70 | **Not computed by the engine.** Example 38 uses `minVol = 10` as a PLACEHOLDER. Spec: a strategy-local rolling 70th percentile of per-price-level traded volume (Ask + Bid) over the **prior 20 RTH sessions' 5m bars** (prior sessions only), passed as `minVol`. **PROPOSED – owner to confirm** the definition (Cat. §2.1 says only "70th-percentile volume floor") |
| Flow quality | `_OrderFlow.FlowQuality`: accept `Exact` or `High` only (Ex 38; catalogue "quality ≥ Medium" in Needs, "prefer MC-BidAsk" in Fails). Report per tier. **PROPOSED – owner to confirm** High as the minimum |
| ATR5 | ATR(14) on Data1 5m (Ex 38) |
| HL trigger (HL arm) | PRICEACTION main engine, fractal N = 3 (1m or 5m, §2) |
| Session / flat | F-SESS `IsRth`, F-TM `FlatBy = 15:55`, `mustFlatten()` |
| News / holiday | M-EVENT `EventBlackout`; `_CmeHolidays` (early close per Prog §7.6) |
| Roll | `RollDetection = ERollDetection.Table` (Ex 38 used `Series`; the batch standard is Table, Prog §7.6); `rollOn(1)` clears zones; no entry on `IsRollWindow` days |
| Cost floor, sizing, journal, controls | M-INSTR M7 floor (ES 14 ticks); M-PORT `sizeForRisk`; F-JRNL `journalArm`, `logSignal`; `ControlArm` for K-SH (shuffle) and K-FL (shift ±0.5 ATR5) |

## 4. Entry rules (long shown; short mirrors with sell stacks, lower 30%, retest from below)
1. **Zone:** on a closed RTH 5m bar that closes in its **upper 30%** (`(C−L)/(H−L) ≥ 0.70`), `StackedImbalances` finds ≥ 3 stacked **buy** imbalances at `ratio` (3:1 primary) above the P70 floor, with quality ≥ High. The zone is the **lowest** buy run [lo, hi] (Ex 38). **PROPOSED – owner to confirm** lowest vs highest run when there are several.
2. **One active zone per side.** A newer qualifying bar replaces an unarmed zone. It does not replace an armed one. **PROPOSED.**
3. **Arm:** after formation, a bar high ≥ zone hi + 1.0 × ATR5 arms the zone (Cat.; Dale "≥ 1–2 footprints").
4. **Retest = the first bar after arming with low ≤ zone hi.** Only that first retest may trade (Dale, Keller).
   - **Limit arm (primary):** a buy limit `OF01LE` at zone hi works from the arming bar's close. It fills only on trade-through (Prog §4). If the first retest bar touches without trading through, the zone is spent (no second try).
   - **HL arm (K-LM):** no resting order. During the retest, the first fractal HL whose low stays ≥ zone lo sets a buy-stop at the confirming bar's high + 1 tick, valid for 3 bars (**PROPOSED**). If price instead closes below zone lo first, the zone is cancelled.
5. **Cancel (sliced origin, Keller):** a closed bar with close < zone lo − 1 tick, before the fill, cancels the zone and the working order. Zones also expire at the end of the session (Ex 38; Keller "same session").
6. **Window:** entries 09:45–15:00 ET (**PROPOSED**; the catalogue states none; Ex 38 uses RTH to the flat time). Not on roll-window days, not in `EventBlackout`, one filled trade per side per session (**PROPOSED**).

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | zone lo − 1 tick, measured from the entry price (zone hi for the limit arm; the stop price for the HL arm). **Minimum:** the instrument cost floor (ES 14 ticks, NQ 19; Prog §4). **Maximum:** if the floored stop distance > 0.20 × ATR20d, skip the trade (`stop-too-wide`) — Dale's "10–20% of daily ATR, else skip", taking the upper bound. **PROPOSED – owner to confirm** (needs `NeedsDaily = true` for ATR20d). Frozen at entry. | Cat. OF-01; Ex 38; Dale stop chapter |
| **Targets** | Base R = entry − stop. Target 1 R (Ex 38; Cat. "prior high / 1 R"). **q = 1:** all out at 1 R. **q ≥ 2:** T1 = ⌊q/2⌋ at 1 R; the rest at the **excursion high** (highest high between zone formation and the retest, i.e. the "prior high" / Keller "stack extreme then next reference") if that is > 1 R, else at 1 R. **PROPOSED – owner to confirm** this reading of "prior high / 1 R". Not swept (≤ 4 configs in W-T). | Cat.; Ex 38; Keller 6.3 |
| **Break-even** | None (catalogue and Ex 38 have none). | |
| **Trailing** | None in the primary. Dale's flow trail ("trail only while imbalances appear on your side") is EX-13 / OF-09 territory and stays out of batch 1. | Dale; Cat. EX-13 |
| **Time stop** | None in the catalogue. **PROPOSED – owner to confirm:** `TimeStopBars = 0` (off) in the primary; 12 × 5m bars reported as a diagnostic only. | |
| **Session end** | Flat at 15:55 ET (`FlatBy`, Ex 38); early close: flatten at close − 5 min, entry cut-off close − 60 min (Prog §7.6). Never overnight. | Ex 38; Prog §7.6 |
| **Invalidation (in trade)** | None beyond the stop (the stop sits just under the zone). | |
| **Adds / pyramiding** | None. | Prog §7.7 |
| **Re-entry after a stop** | Not on that zone (first test only). A new zone formed later the same session may trade, subject to the one-trade-per-side-per-session cap (**PROPOSED**). | Dale; Keller |
| **News (M-EVENT)** | Entry blackout: no new zone arming or entries in `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm**). When > 0, at the last 5m close before release − N (`0 < MinutesTo(EEventTier.T1) < N + 5`, Prog §7.6) an open trade is flattened with `OF01LX`, reason `NEWS`; a working limit at the zone top is not re-sent and the zone is spent; arming resumes at release + 10 min. When ≤ 0, an open trade holds through news with its stop. Note the EV-4.6b coverage holes. | Owner 2026-11-10; Prog §7.6 |
| **Holiday** | No trading on full closes; early close as above. | Prog §7.6 |
| **Roll (M9)** | `rollOn(1)` clears zones; no entries on `IsRollWindow` days. The strategy is flat daily, so no splice exposure. | Ex 38; Prog §7.6 |
| **Orders and fills** | Entry `OF01LE`: limit (limit arm) or stop (HL arm), `Contracts.UserSpecified`. Exits: stop `OF01LS`, T1 `OF01LT1`, T2 `OF01LT`, market `OF01LX`, all `OrderExit.FromAll` / sized from the actual position (Prog §7.2). Limits fill on trade-through. A limit fill on a bar that also prints below the stop is resolved by tick magnifier; if it is unresolved, assume entry then stop (Prog §4). | Ex 38; Prog §4, §7.1 |
| **MM interaction** | Prog §7.5, including the Check 7 facts: `ForceClosePosition` fills at the bar-T close; Paused blocks all fills; `EntryContracts` −1 = coded size, 0 blocks entries. The MM may deny or resize entries and may force-flatten; it never moves stops or targets. `PortfolioFamily = "OF"`. | Check 7 |
| **Same-bar precedence** | MM force-close → stop → T1 → T2 → news exit → FlatBy / early close → roll → zone cancel (pre-fill) → arm/retest update. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` = 2 (owner, 2026-11-10): one contract at 1 R, one to the excursion high (§5). With q = 1 (MM size) everything exits at 1 R. Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
§5 rows; Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `ImbalanceRatio` | double | 3.0 | Ask/Bid diagonal | {3.0, 4.0} | Tuned | Imbalance threshold |
| `EntryArm` | enum `EEntryArm { Limit, HL }` | Limit | — | enum | Tuned | Limit vs HL (K-LM) |
| `MinStack` | int | 3 | levels | fixed | Fixed (Cat., Dale) | Stack length |
| `VolFloorPct` | double | 70 | percentile | fixed | Fixed (Cat. §2.1) | Volume floor |
| `VolFloorSessions` | int | 20 | sessions | fixed | Fixed (**PROPOSED**) | Floor look-back |
| `CloseTopFrac` | double | 0.30 | fraction of bar range | fixed | Fixed (Cat.) | Upper-30% close |
| `AwayAtr` | double | 1.0 | × ATR(14, 5m) | fixed | Fixed (Cat.) | Arming distance |
| `MinQuality` | enum `_OrderFlow.FlowQuality` | High | — | {Exact, High} | Fixed (**PROPOSED**) | Flow-quality gate |
| `MaxStopAtr20d` | double | 0.20 | × ATR20d | 0.10–0.20 | Fixed (**PROPOSED**, Dale) | Skip wide stops |
| `HlOrderBars` | int | 3 | bars | 1–5 | Fixed (**PROPOSED**) | HL-arm stop-order life |
| `TargetR` | double | 1.0 | R | fixed | Fixed (Ex 38) | Target |
| `TimeStopBars` | int | 0 | 5m bars (0 = off) | 0–24 | Fixed (**PROPOSED**) | Optional time stop |
| `EntryStartHHMM` 945, `EntryEndHHMM` 1500, `SingleLotMode`, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct`, `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Registered sweep = 2 × 2 = **4** (Cat.; W-T allows ≤ 4 configs).

## 9. Logging
Prefix `OF-01`.
- **Zone line:** `OF-01 ZONE t() side() lo() hi() stack(n) ratio() minVol(p70) quality() closeFrac()`.
- **Then:** `ARM`, `RETEST`, `CANCEL(sliced|expired|roll|event)`, `SPENT(no-fill)`.
- **Skip reasons:** `quality`, `stop-too-wide`, `risk-budget`, `window`, `cap`.
- **`journalArm("OF01LE", entry, stop, target, ctx)`:** ctx carries the quality tier and source (`McBidAskMethod` text when the tier is McBidAsk).
- **Control logging:** `logSignal` on every retest for K-SH / K-FL.
- **Bar summary:** one line per session with the zone count by quality tier.

## 10. Test plan (holdout re-scoped, Prog §5.1)
- **Rule change (owner, 2026-11-10 19:44 ET):** *"re-scope the holdout period to be about 18 months or even 12 months."* With the default `HoldoutMonths` = 18 and `HoldoutScope = OrderFlowOnly`, the OF-01 cut is **2025-05-01**, recorded per run. The holdout is 2025-05-01 onward and stays sealed.
- **Usable bid/ask span is shorter than "Jul 2024".** The catalogue's own data notes (YV-01, YV-02, YV-04, YV-07 and OF-09 in v2.7) record that **@ES bid/ask exists only from 2024-10-28** (dxFeed ticks from about 2024-10-27); "the catalogue's W-T 'Jul 2024' start does not hold for bid/ask". The NQ bid/ask start is not given (VERIFY).
  - So the usable OF-01 span under the 18-month cut is about **28 Oct 2024 → 30 Apr 2025**: about 6 months, roughly 125 RTH sessions (a calendar count, not a data check). That is not the ~10 months that a Jul 2024 start would give.
  - Under the 12-month cut (2025-11-01) it would be about 28 Oct 2024 → 31 Oct 2025: about 12 months, roughly 250 sessions. That is twice the sample, which is why the 12-month choice matters most for OF-01 (Prog §10 item 1).
- **Split (PROPOSED – owner to confirm), 18-month default, frozen before any read:**
  - **development:** 28 Oct 2024 → 28 Feb 2025, about 4 months / 83 sessions;
  - **validation:** 1 Mar 2025 → 30 Apr 2025, about 2 months / 42 sessions;
  - **walk-forward inside development:** anchored, one fold (train Nov–Dec 2024 → test Jan–Feb 2025).
  - This is the catalogue's W-T proportion of 2 : 1. ES and NQ are run separately.
  - If the owner picks 12 months: development 28 Oct 2024 → 30 Jun 2025, validation 1 Jul → 31 Oct 2025, with two anchored folds. This is pre-registered here so the choice cannot be made after seeing results.
- **Data check first (read-only, QuoteManager):** confirm that the tick + Bid/Ask (MC-BidAsk) history really starts 2024-10-28 for @ES (and find the @NQ start), and that nothing has rolled off. Record the verified span and the quality tier mix in the run sheet. If fewer than 4 development months exist, OF-01 is parked.
- **Sample expectation:** the catalogue frequency estimate (4–10 trades per month; an estimate, not data) implies roughly 16–40 development events and 8–20 validation trades under the 18-month cut, and about twice that under 12 months. The Prog §6 rules apply unchanged:
  - Cat. §2.3: < 30 development events means no evaluation; 30–149 is inconclusive; ≥ 150 is needed for Gate 3.
  - ≥ 100 validation trades are required.
  - OF-01 can now be evaluated rather than only mechanics-checked, but at 18 months the likely verdict is **"no evaluation" or "inconclusive"**. It is reported as such and never relaxed.

- **Configs:** the registered 4 (ratio × entry arm). Primary cell (3:1, Limit) frozen before any read.
- **Controls:**
  - K-SH: shuffled-zone placebo (same count and time-of-day distribution).
  - K-FL: zone shifted ±0.5 ATR5.
  - K-LM: limit vs HL, already in the sweep.
  - Report results per quality tier and per regime (trend vs balance day, Keller).
- **Kill:** result ≤ the shuffled placebo.
- **Costs and floors:** Prog §4 ($17.00 ES, $9.48 NQ). Tick magnifier inside the verified tick window.

## 11. Acceptance criteria
Prog §6 + the K-SH kill, on the §10 split (validation Mar–Apr 2025 under the 18-month default). `catalogue-default`. Below the sample minimums, the verdict is inconclusive.

## 12. Implementation checklist
- [ ] Start from Ex 38. Switch to native inputs and `RollDetection = ERollDetection.Table`. Add the P70 floor, the quality gate as an input, and the max-stop skip (`NeedsDaily = true` for ATR20d).
- [ ] Add the zone-cancel and first-retest-spent logic (already partly in Ex 38).
- [ ] Add the q ≥ 2 split to the excursion high, the HL arm (resolve the data-layout VERIFY in §2 first), and the window and cap rules.
- [ ] Off-MC unit tests:
  - the P70 floor uses prior sessions only
  - the zone state machine: arm, first retest, cancel, expiry
  - the T6 imbalance vector from base-class-spec
  - same-bar precedence
- [ ] `NewsExitMinutes` rule (Prog §7.6).
- [ ] Native compile and smoke run on a verified tick day before the §5.1 cut (To date ≤ 04/30/2025 for OF-01 under the default; never on or after 05/01/2025).

## 13. Header mapping
- **Trigger:** a 5m bar closing in its top 30% holds ≥ 3 stacked 3:1 buy imbalances. Price then runs 1 ATR above the zone. On the first pullback into it, buy at the zone top (or above a 1m higher low).
- **Management:** stop 1 tick under the zone, at least the cost floor. Skip if the stop is wider than 20% of daily ATR.
- **Exit:** 1 R, with the second half at the move's high when trading 2+ contracts. Flat by 15:55.

## 14. Open questions for Ettienne
1. Development/validation split of the re-scoped window (§10: 18-month and 12-month versions pre-registered), and parking OF-01 if fewer than 4 development months of bid/ask ticks exist.
2. The P70 volume-floor definition (per-level volume over the prior 20 sessions).
3. Minimum flow quality: High or Medium?
4. Max stop 0.20 × ATR20d (Dale 10–20%) → skip.
5. Reading of "prior high / 1 R": q = 1 exits at 1 R; q ≥ 2 sends the second half to the excursion high.
6. Entry window 09:45–15:00, one trade per side per session, lowest run as the zone, HL-order life of 3 bars, time stop off.
7. Your `_StackedImblContLELX` rules (stop entry after a bullish close; mid-zone limit on the 2nd/3rd test; RR 1.5–3) as a separate registered variant later?
8. HL-arm data layout (1m chart with 5m aggregation vs 5m HL).
9. Prog §10 batch-wide items.

## 15. Sources
- Catalogue: OF-01, §2.1 (imbalance definition), §2.3 (controls).
- Manual §3.30 (Example 38), §4.11 (M-TICK), §5.9 (F-OF).
- base-class-spec (diagonal-imbalance definition, T6 vector).
- `_OrderFlow.Function.CS` (`FlowSource`, `FlowQuality`, `EMcBidAsk`).
- Trader Dale, *Order Flow: Trading Setups*: Setup #4 p.76 and the stop/trail chapter pp.129–137 (owner's Kindle notes).
- Keller, Volume Profile Book 1, card 6.3 (owner's Kindle notes).
- Owner shell `_StackedImblContLELX.Strategy.CS`.
- Strategy-planning Page (build step 6).
