# PS-02 · Balance break at acceptance close — implementation spec

**Rev 1.5 (draft), 11 Nov 2026 (EST)** — short side written out rule by rule in new §5a (Ettienne 2026-11-11 09:36 ET: *"Please review the strategies thoroughly and make sure that where shorts are involved, we have clarity around handling the short scenario's."*); conventions in Prog §7.9. No long rule changed.

**Rev 1.4 (draft), 10 Nov 2026 (EST)** — §8.1 Variants added: every true variation is selectable via Inputs, default = primary (owner, 20:31 ET). Multi-value inputs are typed C# enums, not ints or strings (owner, 20:34 ET; Prog §3 rules 8–9).

**Rev 1.3 (draft), 10 Nov 2026 (EST)** — Kindle checks of Keller B1 Ch 3.3 and Keller B4 §2.4/§8.4 applied (Kindle locations; quotes read from page images, may differ slightly). Rev 1.1: costs, 2 contracts, news exit, holdout. Batch 1, build order #1. Shared rules: [00-programme-overview.md](00-programme-overview.md) (cited as *Prog §x*). Catalogue: `specs/trading-idea-catalogue.md` § PS-02 and merged A2-41. Target file: `sandbox/_PS_02_BalanceBreakClose.Strategy.CS`, class `_PS_02_BalanceBreakClose`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** After at least 3 overlapping RTH sessions (a balance), a break that is *accepted* outside the composite value area traps the responsive sellers at the top of the range (or buyers at the bottom). Their covering, plus initiative traders, carries price about one balance width. Entering at the acceptance close catches the strongest breaks, where no pullback comes (Cat. PS-02 "Why it might work").
- **Evidence.** Keller B4 §2.4: on FX/crypto 1-minute data, a balance break travels its width 56.6% (FX, 214/378) / 59.1% (crypto) of the time, median 1.17–1.20 W, right-skewed. Keller B4 §8.4 calls the balance break "the only end-to-end spec": enter aligned with value migration, stop back inside the balance, scale at one width, run the rest; skip profiles with entropy > 0.93. Keller B4 §8.3: confirmation behaviours have median MFE/MAE 10.5 vs 0.70 for fades (an upper bound). Grade **C (+)** family; **CME untested** (Cat. PS-02). No statistic here is a CME result.
- **Keller B1 Ch 3.3 "The Balance-Break Setup" (Kindle Loc ~951–959):**
  - Start from a mature balance: a fat, multi-session bell with defined VAH/VAL.
  - "The escape itself is not the entry." There are two entries: **(a) acceptance** beyond the edge ("volume visibly building at the new prices, forming a fresh ledge — enter with the break, stop back inside the old value area") and **(b) the retest** (PS-01).
  - Targets: "a measured objective (balance width projected) and/or the next composite HVN".
  - Acceptance is judged on qualitative criteria only: new rows printing real volume beyond the edge (not one thin spike); the developing POC migrating toward or beyond the break; time holding (several rotations outside); an LVN corridor beyond is a plus, a composite HVN just beyond is a downgrade.
  - Failed break: accepted back inside value means "the setup inverts… frequently rotates to the opposite edge".
  - Traps: the late chase, and the hair-trigger fake (buying the first tick beyond VAH).
  - On a trend-day candidate: "trail, don't fade your own position".
  - **PS-02 = Keller entry (a), acceptance plus market entry.**
  - Checked, not specified in Keller B1: the number of closes, the buffer δ, order life, break-even.
- **Keller B4 §2.4 (Loc ~721–838), §8.4 Rule 4 (graded A-DOCUMENTED):**
  - Breaks travel the full width 56.6% of the time (FX, 378 breaks) and 59.1% (crypto, 489 breaks); median achieved 1.17 / 1.20 widths.
  - "A scale-out at 1.0 with a runner beyond it is the structure the data actually supports"; a fixed full exit at 1.0 truncates the part of the distribution that pays for the failures. Wide balances give distant targets, narrow ones near targets.
  - Rule 4: "Enter on the break aligned with migration, stop back inside the balance, scale at one range width. Run the remainder." Daily steps 7–8: take only the migration-aligned break, and run the remainder "against the nearest destination". Rule 2: profile levels are destinations, not entries (naked-POC fill 85.9%). Step 9: record R, MFE and MAE for every trade.
  - Hypothesis, grade C: filter by how much business the balance contained (A2-41's Q1 stratifier).
  - **These are FX/crypto samples, not ES/NQ futures evidence.**
- **Role in the programme.** The simplest profile host. PS-01 (next spec) reuses this event cohort and tests whether waiting for a pullback adds value (final-synthesis §6.6 rung L3).

## 2. Instruments and timeframes
- **Primary:** @ES, then @NQ (separate runs, never pooled to hide a losing arm). Later replications: CL (pit 09:00–14:30 value, Cat. §2.1) and 6E, only after M8/M9 and cost rows (Prog §4, §7.6). Catalogue also lists GC/ZN.
- **Data1:** @ES 5-minute, dxFeed continuous **unadjusted**, 24h CME Globex session template, exchange time zone (Example 27 setup). Composite and V-score use the Own RTH profile (`DevelopingSource = EDevelopingSource.Own`, `OwnProfileWindow = EOwnProfileWindow.Rth`).
- **Warm-up:** 250 sessions if the A2-41 quality score is used (trailing median); otherwise n + 20 sessions (composite + 20-day ATR). After each roll splice the composite needs n fresh sessions (≈ 5-session warm-up, Cat. PS-01 data line).

## 3. Engines and services (exact)
| Need | Engine / member | Enabled by |
|---|---|---|
| Composite VAH/VAL/W, balance test | F-COMP `Composite(1, n)`, `IsBalanced(1, n, 0.6)` | VP futures base (Own RTH) |
| Value migration (daily, **default filter**) | F-PROF `ValueMigrationScore(1, n, 0.10, 20, false)` (completed sessions only) | base |
| Acceptance flow check (optional) | F-DEV developing RTH profile `DevelopingExtra(1)` (`poc`, row volumes) | Own RTH profile |
| Runner destination (optional) | F-HVN `Composite(1, n).hvns`; F-NPOC `NakedPocs(1)` (`TrackNakedPoc = true`) | base |
| Entropy veto | M-GMM `ProfileEntropy(comp, bars)` → `Hnorm` | `ProfileStats.enabled = true` |
| ATR5 for δ | `averageTrueRange(Bars, 14)` on Data1 | — |
| Daily ATR (BookSpec skip rule) | M-DAILY `DailyContext.Atr20d` (t−1) | `NeedsDaily = true` in `StartCalc` |
| Runner trail (EX-05) | PRICEACTION `PriceActionEngine`, layer 0, `EPivotLabel.HL` | `PriceActionConfig.enabled = true`, `dataStream = 1` (Futures preset) |
| Session/flat | F-TM `FlatBy`, `mustFlatten()`, F-SESS, `_CmeHolidays` | base |
| News | M-EVENT `EventBlackout` | `EventConfig.enabled = true` |
| Rolls | `RollDetection = ERollDetection.Table`, `rollOn(1)`, `IsRollWindow` | base |
| Cost floor | M-INSTR M7 `InstrumentConfig.applyCostFloor = true` | `Create()` |
| Sizing / portfolio | `sizeForRisk(…, widenedStop)`, `portfolioArm`, `portfolioStop`; `PortfolioFamily = "PS"` | base; PT only |
| Journal / events | `journalArm`, `logSignal`, `trace(ETraceLevel.Journal, …)` | `TraceLevel` |

## 4. Entry rules (long shown; the short side is written out in §5a)
Evaluated on closed 5m bars only.
1. **Daily setup (at the first bar of each Globex trading day, from completed sessions only):** `comp = Composite(1, n)` exists; `IsBalanced(1, n, 0.6)` is true; W = comp.vah − comp.val > 0; V = `ValueMigrationScore(1, n, 0.10, 20, false)`; if `EntropyFilter` is on, require `Hnorm ≤ 0.93`. A day that fails is logged `SKIP no-balance|entropy|no-composite` once.
2. **δ** = max(2 ticks, 0.10 × ATR5) (Cat. §2.1). *Note: Example 27 uses a flat 2 ticks; the spec follows the catalogue.*
3. **Window:** bar close time in [09:45, 14:30] ET, an RTH bar, not a roll-window day, `EventBlackout` false at the signal bar.
4. **Acceptance:** `AcceptCloses` consecutive closed 5m bars with close > comp.vah + δ (counter resets on any close ≤ comp.vah + δ). The count of 2 and δ stay **PROPOSED**: Keller gives qualitative criteria only (checked: not specified in Keller B1 Ch 3.3 (Loc ~951–959)). Requiring closes, not a touch, avoids Keller's hair-trigger fake.
   - **Optional acceptance flow check** (`AcceptFlowCheck`, default **off**, PROPOSED thresholds; Keller 3.3 criteria): at the acceptance bar, (i) the developing RTH POC is ≥ comp.poc (migrated toward the break), and (ii) RTH volume traded above comp.vah since the open is ≥ `VolBeyondPct` (25%) of RTH volume so far (final-synthesis Spec 1's 25% volume-above-edge rule). It is off by default because Keller gives no numbers and the rule adds a filter to a registered host; it runs as a pre-registered ablation.
   - **Logged stratifiers (Keller 3.3):** `lvnBeyond` (an LVN of the composite within 0.5 W beyond the edge) and `hvnBeyond` (a composite HVN within 0.5 W beyond, a downgrade).
5. **Direction filter (value-migration alignment, default):** long requires V ≥ +1; short requires V ≤ −1. Sources: Cat. PS-02 "V-score aligned"; Keller B4 §8.4 Rule 4 and daily steps 7–8 ("take only the break aligned with value migration"). It is built from the existing `ValueMigrationScore` over completed sessions, so it stays the default.
6. **Entry:** `MarketNextBar` buy at the open of the next bar. **One attempt per day per instrument** (any outcome, including a skip, ends the day; Example 27 `m_done`).
7. **Skip checks before sending** (logged, never resized): widened stop risk > 1% of equity (Cat. §2.1); BookSpec only: 0.30 W < ATR5 + 2 ticks (`SKIP noise`) or 0.30 W > 1.5 × Atr20d (`SKIP too-wide`) (A2-41).

**Variant `BookSpec` (A2-41, same slot, `Variant` input; separately registered, never a replacement primary):** stop 0.30 W, the two skip rules above, and A2-41's quality score Q1 (balance volume vs trailing-250 median) as a reported stratifier. Its 3-day overnight runner arm is **not** implemented in batch 1 (overnight only for MES/MNQ per A2-41; intraday-only arm reported) — **PROPOSED – owner to confirm**.

## 5. Trade management and exits (dedicated)
| Item | Primary rule | Source |
|---|---|---|
| **Initial stop** | Long: comp.vah − `StopWidthW` × W (default 0.25 W), i.e. back inside the composite by 0.25 W. Widened to the cost floor if closer than the floor: ES 14 ticks, NQ 19, CL 15, 6E 18, measured from the planned entry (the signal close) (Prog §4). **Minimum** = cost floor. **Maximum** = none in Primary (a stop wider than the 1%-equity budget skips the trade). BookSpec: 0.30 W, min ATR5 + 2 ticks, max 1.5 × Atr20d (skip outside). | Cat. PS-02; A2-41; Cat. §2.1 |
| **First target (T1)** | Limit at comp.vah + `TargetW` × W (1.0 W beyond the broken edge, measured from the edge, not from entry): Keller 3.3's measured objective, "balance width projected". | Cat. PS-02; Keller B1 Ch 3.3; Keller B4 §2.4/§8.4 Rule 4; EX-03 |
| **Scale-out** | At T1, exit ⌊q/2⌋ contracts (limit `PS02LT1`); runner = the rest (test size 2 → 1 + 1). q = 1 → `SingleLotMode` (default `ExitAtT1`: the whole position exits at T1; owner-accepted 2026-11-10). | Cat. PS-02 "scale 50% at 1.0 W"; **Keller B4 §2.4** ("a scale-out at 1.0 with a runner beyond it is the structure the data actually supports") and §8.4 Rule 4 (FX/crypto evidence, not futures); split rule Prog §7.2 (odd-lot rule **PROPOSED**) |
| **Subsequent target** | Primary: none; the runner exits by trail, stop, failed-break exit or the session flatten (Cat. PS-02; Keller 3.3 trend-day "trail, don't fade your own position"). Optional `RunnerTarget = NearestDestination` (Keller B4 §8.4 steps 7–8, "run the remainder against the nearest destination"; Rule 2): a runner limit at the nearest destination beyond T1, i.e. the nearer of the next composite HVN low edge and the nearest naked POC, minus 2 ticks (offset **PROPOSED**). The trail still applies. A pre-registered ablation, not the primary, to keep the catalogue host unchanged. | Cat. PS-02; Keller B1 Ch 3.3; Keller B4 §8.4 |
| **Break-even** | **None in Primary** (the catalogue does not specify it; do not add). BookSpec: after T1, runner stop → entry + 0 ticks (A2-41). | Cat. PS-02; A2-41 |
| **Trailing (runner)** | EX-05: activates when T1 has filled. On each closed 5m bar, if PRICEACTION confirmed a new layer-0 pivot low labelled `EPivotLabel.HL` whose extreme is after the entry bar, candidate = pivot price − 1 tick; stop = max(stop, candidate) (Prog §7.3). It trails only at confirmed HLs (no fixed distance). BookSpec instead: highest 30m close since entry − 0.5 W, plus a hard exit on a 30m close back inside the composite VA. | Cat. EX-05; A2-41 |
| **Time stop / max hold** | Intraday only. FlatBy 15:55 ET (`mustFlatten()` → `PS02LX`). Early-close day: flatten at RTH close − 5 min. No overnight, no weekend. | Cat. §2.1; Prog §7.6 |
| **Invalidation: failed-break exit** | `FailedBreakExit` (default **on**, per Keller 3.3: a break accepted back inside value means "the setup inverts"). `FailCloses` (2, **PROPOSED**, mirroring `AcceptCloses`; checked: not specified in Keller B1) consecutive closed 5m bars with close < comp.vah − δ → `PS02LX` market next bar, reason `INVALIDATION`. Applies before and after T1. **Registration change, **Owner-approved 2026-11-10 20:31 ET** (Ettienne: *"These changes are fine"*):** the catalogue listed the EX-10 acceptance-failure exit as a separate later module; it is now on in the primary. `FailedBreakExit = false` reproduces the catalogue's original host. The opposite-edge reversal ("frequently rotates to the opposite edge") is a **future variant**, not batch 1. | Keller B1 Ch 3.3; Cat. EX-10 |
| **Adds / pyramiding** | Not allowed. No add input: this is a single break/retest or first-test setup, so adds are not natural here (Prog §7.7, rev 1.5). | Prog §7.7 |
| **Re-entry after a stop** | Not allowed: one attempt per instrument per day. | Example 27; Cat. §2.1 one live position |
| **News** | Entry blackout: no entry while `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm** the default). When > 0, at the last bar close before release − N (rule `0 < MinutesTo(EEventTier.T1) < N + bar minutes`, Prog §7.6) the strategy flattens with `PS02LX`, reason `NEWS`, stops arming entries until release + 10 min, and the setup's attempt is used. When ≤ 0, an open trade holds through the release with its stop. | Owner 2026-11-10; Cat. §2.1; Prog §7.6 |
| **Holiday** | Full-close days have no bars. Early-close days: entries stop 60 min before the close, flatten 5 min before (**PROPOSED – owner to confirm**). ES/NQ: `_CmeHolidays`; CL/6E: M8 tables. | Prog §7.6 |
| **Roll day** | No entry on a roll-window day (`IsRollWindow`). The composite is rebuilt from post-splice sessions; until n post-splice sessions exist, no setup (`SKIP roll-warmup`). Intraday, so never held over a splice. M9 changes nothing (log-only). | Cat. PS-02 data line; M-INSTR M9 |
| **Orders and fills** | Entry `MarketNextBar` (fills at the next bar's open + 1 tick slippage per MC settings). Stop `PS02LS` resting; T1 limit `PS02LT1` fills on trade-through; FlatBy/time exit `PS02LX` `MarketNextBar`. Stop and T1 are re-sent on every closed bar while in the position. Bar Magnifier 1-minute; stop-first on unresolved bars (Prog §4). | Example 27; Cat. §2.1 |
| **MM interaction (PT)** | The MM may deny or size the entry (`EntryContracts`: −1 = coded size, 0 = blocked, > 0 = MM size). The split uses the filled position. The MM may `ForceClosePosition` (fills at the bar-T close); after that the strategy logs `FLAT-BY-MM` and the day is done. The MM never moves the stop or target. `Paused` is only set when flat and blocks all fills. | Prog §7.5; Check 7 |
| **Same-bar precedence** | Prog §7.4: MM force-close → stop → T1/target → news exit → FlatBy/early close → failed-break exit → time. A trail update is computed only after these and applies from the next bar. | Prog §7.4 |

### 5a. Short side, rule by rule
Source status: the catalogue mechanism is two-sided ("traps the responsive sellers at the top of the range (or buyers at the bottom)") and Keller B4 §8.4 Rule 4 is direction-neutral ("enter on the break aligned with migration"), so the short is a **sourced mirror** with the same numbers as the long. Keller B1 Ch 3.3's wording is long-side ("buying the first tick beyond VAH"); its short reading is the mirror. Conventions: Prog §7.9. `side = −1` in every log line.
| Item | Short rule |
|---|---|
| Daily setup | Same balance, composite and entropy tests (side-blind). **V ≤ −1** (value migrating down). |
| Acceptance | `AcceptCloses` consecutive closed 5m bars with close **< comp.val − δ**; the counter resets on any close ≥ comp.val − δ. Same δ and window. |
| Acceptance flow check (optional) | Developing RTH POC **≤ comp.poc**, and RTH volume traded **below comp.val** since the open ≥ `VolBeyondPct` of RTH volume so far. |
| Stratifiers | `lvnBeyond` / `hvnBeyond` = a composite LVN / HVN within 0.5 W **below** comp.val. |
| Entry | `MarketNextBar` **SellShort** `PS02SE` at the next bar's open. One attempt per instrument per day for both sides together (a long attempt uses the day). |
| Initial stop | **comp.val + `StopWidthW` × W** (back inside the composite), `PS02SS` (buy-to-cover stop). Widened **upward** to entry + cost floor (ES 14, NQ 19, CL 15, 6E 18 ticks) if closer. BookSpec: comp.val + 0.30 W, skip rules unchanged. Risk > 1% equity → skip. |
| T1 | Buy-to-cover limit `PS02ST1` at **comp.val − `TargetW` × W** (1.0 W below the broken edge, measured from the edge). Split and `SingleLotMode` as the long. |
| Runner target (`NearestDestination`) | The nearer, **below** T1, of the next composite HVN's **high** edge and the nearest naked POC, **+ 2 ticks**, `PS02ST`. |
| Break-even | Primary none. BookSpec: after T1, runner stop → **entry − 0 ticks**. |
| Trailing (EX-05) | After T1: on each confirmed layer-0 pivot **high** labelled **`EPivotLabel.LH`** whose extreme is after the entry bar, candidate = pivot price **+ 1 tick**; stop = **min(stop, candidate)**, capped one tick above the last close (Prog §7.3). BookSpec: **lowest** 30m close since entry **+ 0.5 W**, plus a hard exit on a 30m close back **above comp.val** (inside the VA). |
| Failed-break exit | `FailCloses` consecutive closed 5m bars with close **> comp.val + δ** → `PS02SX`, reason `INVALIDATION` (owner-approved rule, mirrored). |
| Time / session / news / holiday / roll | Identical to the long (side-blind, Prog §7.9 item 6); exits use `PS02SX`. |
| Adds | None (as the long). |
| MFE / MAE / risk % | Adverse = price up: MAE marked at bar highs, MFE at bar lows; magnitudes ≥ 0 (Prog §7.9 item 2). |
| Reports | K-DR already tests longs and shorts separately; every table splits long / short / combined (Prog §7.9 item 8). |
| Short-specific notes | Intraday only, so no overnight gap risk. Downside breaks in ES/NQ are often faster (higher volatility) than upside breaks; δ and the stop are ATR/W-scaled, so no extra rule. The opposite-edge reversal future variant would, for a failed short, go long towards VAH. |

## 6. Sizing, risk and M-PORT integration
- **Chart (research):** fixed `Contracts` = 2 (owner, 2026-11-10). Skip, never resize, if `Contracts × |entry − widenedStop| × BigPointValue > MaxRiskPct% × equityAvailable()` (Cat. §2.1; manual §5.13).
- **Portfolio Trader:** `q = sizeForRisk(Bars, entry, structuralStop, RiskFractionPct/100, limits, widenedStop)`; arm on the signal bar (entry is a market order, so arming lasts one bar). `PortfolioFamily = "PS"`; cluster `EQIDX` for ES/NQ. Starter MM profile; `riskPerTrade` 0.005, `maxTradeRisk` 0.01 (MM defaults). `portfolioStop(stop)` after any trail move.
- **R** = |entry − widened initial stop| per contract, frozen at entry (never recomputed after the trail).
- **Future % account-risk sizing:** Prog §7.8. In PT the MM's existing RiskParity sizing already applies (`EntryContracts` > 0; −1 = coded size; 0 blocks). No chart-level `SizingMode` input in batch 1.

## 7. Session, news, holiday and roll handling
As in §5 and Prog §7.6. RTH value 09:30–16:00 ET for ES/NQ; CL uses pit value 09:00–14:30 when run.

## 8. Native inputs (header §17)
All `[Input]` properties. "Tuned" means research-tuned within the registered catalogue sweep only; "Fixed" means chosen before any run.
| Input | Type | Default | Units | Range (out-of-range behaviour) | Fixed/Tuned | Purpose / effect |
|---|---|---|---|---|---|---|
| `Variant` | enum `EVariant { Primary, BookSpec }` | Primary | — | enum | Fixed per run | Selects the PS-02 primary or the A2-41 book geometry |
| `Side` | enum `ESide { Both, LongOnly, ShortOnly }` | Both | — | enum | Fixed | Restricts direction; reports K-DR by side |
| `CompositeSessions` | int | 3 | sessions | 3–5 (else Error + no trades) | Fixed (A2-41 sweeps {3,5} in BookSpec only) | n in `Composite`/`IsBalanced`/V-score |
| `BalanceOverlapPct` | double | 60 | % | 50–80 (else Error) | Fixed | VA overlap needed for balance |
| `AcceptCloses` | int | 2 | 5m closes | {1, 2} (else Error) | Tuned | Closes beyond edge + δ before entry |
| `StopWidthW` | double | 0.25 | × W | {0.25, 0.50} (BookSpec forces 0.30) | Tuned | Distance back inside the composite |
| `EntropyFilter` | bool | true | — | on/off | Tuned | Skip composites with Hnorm > `MaxEntropy` |
| `MaxEntropy` | double | 0.93 | normalised entropy | 0.80–1.00 | Fixed (Keller B4 §8.4) | Entropy veto threshold |
| `DeltaMinTicks` | int | 2 | ticks | 1–8 | Fixed | Floor of δ = max(ticks, 0.10 × ATR5) |
| `TargetW` | double | 1.0 | × W | fixed 1.0 | Fixed (Keller B4 §8.4) | T1 distance beyond the edge |
| `FailedBreakExit` | bool | true | — | on/off | Fixed (Keller B1 3.3) | Exit when the break is accepted back inside value |
| `FailCloses` | int | 2 | 5m closes | 1–3 | Fixed (**PROPOSED**) | Closes back inside VAH − δ for the failed-break exit |
| `AcceptFlowCheck` | bool | false | — | on/off | Fixed (ablation; **PROPOSED** thresholds) | Keller 3.3 developing-POC and volume-beyond-edge check |
| `VolBeyondPct` | double | 25 | % of RTH volume | 10–50 | Fixed (**PROPOSED**; final-synthesis Spec 1) | Volume-beyond-edge threshold |
| `RunnerTarget` | enum `ERunnerTarget { TrailOnly, NearestDestination }` | TrailOnly | — | enum | Fixed (ablation) | Keller B4 runner destination |
| `SingleLotMode` | enum `ESingleLotMode { ExitAtT1, RunnerOnly }` | ExitAtT1 | — | enum | Fixed (owner 2026-11-10) | Behaviour when q = 1 |
| `EntryStartHHMM` / `EntryEndHHMM` / `FlatHHMM` | int | 945 / 1430 / 1555 | ET clock | valid HHMM, start < end < flat (else Error) | Fixed (Cat.) | Entry window and flatten |
| `Contracts` | int | 2 | contracts | 1–20 | Fixed (owner 2026-11-10) | Chart size (PT: overridden by MM) |
| `NewsExitMinutes` | int | 0 | minutes before a tier-1 release | 0–120; ≤ 0 = disabled | Fixed (**PROPOSED** default) | Flatten and block entries N minutes before scheduled high-impact news (Prog §7.6). Effect: fewer news-exposed trades; Interactions: entry blackout −2/+10, precedence item 4 |
| `RiskFractionPct` | double | 0.5 | % equity | (0, 1] | Fixed | `sizeForRisk` risk per trade in PT |
| `MaxRiskPct` | double | 1.0 | % equity | (0, 1] | Fixed (owner rule) | Skip budget for the widened stop |
| `ControlArm` | enum `EControlArm { None, RandomEntry, ShiftLevels, DelayEntryBars }` | None | — | maps to base `ControlMode` | Fixed per run | K-RT / K-FL controls (F-EVT) |
| `AcceptancePolicy` | enum `EAcceptancePolicy { CatalogueDefault, BigWinnerTrend }` | CatalogueDefault | — | enum | Fixed before any result | Logged; drives the offline evaluator |
| `MaxSingleTradeNetProfitPct` | double | 10 | % of net | (0, 100] | Fixed (owner) | Concentration cap, logged for the evaluator |
| `OutlierRemovalPct` | double | 1 | % of winners | (0, 10] | Fixed (owner) | Top-winner trim for the stress |

Tuned numeric parameters = `AcceptCloses`, `StopWidthW`, plus the on/off `EntropyFilter` = the catalogue's 8-config sweep.

### 8.1 Variants (true variations, all selectable via Inputs; default = primary)

A variant is an alternative rule, not a different value of a swept number (Prog §3 rules 8–9). Every variant below is a native `[Input]`. A true on/off switch is a `bool`. Every multi-value choice is a **typed C# enum** declared below, with named members only: no integer codes, no strings. The default is always the primary rule, and it is set in the constructor, never by member order. Enum `[Input]`s are already used by compiled repo studies (`_CustomSignal.TraceLevel` and others, Prog §3 rule 9). Whether the chart and Portfolio Trader optimizers can sweep an enum input is **not yet verified**; Coder checks it on the MiniPC (Prog §9). Inputs marked *sweep axis* are also part of the registered sweep. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants and are not listed.

| Variant | Input | Type | Values | Default (primary) | What it changes | Source |
|---|---|---|---|---|---|---|
| Book geometry | `Variant` | enum `EVariant` | `Primary`, `BookSpec` | `Primary` | A2-41 book geometry (noise/too-wide skips, 0.30 W stop, its own 8-config sweep) | Cat. A2-41; Keller B4 §2.4 |
| Failed-break exit | `FailedBreakExit` | bool | true / false | true (owner-approved 20:31 ET) | false = catalogue original host with no EX-10 exit; closes counted by `FailCloses` | Keller B1 Ch 3.3; Cat. EX-10 |
| Acceptance flow check | `AcceptFlowCheck` (threshold `VolBeyondPct`) | bool | true / false | false | Adds developing-POC ≥ comp.poc and volume-beyond-edge ≥ `VolBeyondPct` to acceptance | Keller B1 Ch 3.3; final-synthesis Spec 1 |
| Runner limit | `RunnerTarget` | enum `ERunnerTarget` | `TrailOnly`, `NearestDestination` | `TrailOnly` | Adds a runner limit at the nearer of the next composite HVN and the nearest naked POC, − 2 ticks; the trail stays | Keller B4 §8.4 steps 7–8; Cat. PS-02 |
| Entropy veto | `EntropyFilter` | bool | true / false | true | *Sweep axis.* Skips composites with Hnorm > `MaxEntropy` | Cat. PS-02; Keller B4 §8.4 |
| Single-contract handling | `SingleLotMode` | enum `ESingleLotMode` | `ExitAtT1`, `RunnerOnly` | `ExitAtT1` | When q = 1: exit at T1, or hold the single contract as a runner | Owner 2026-11-10; Prog §7.2 |

**Enum declarations** (nested `public enum` types in the strategy class; the input property uses the enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`):

```csharp
// Shared enums (Prog §3 rule 9), declared identically in every batch-1 file:
// ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
public enum EVariant { Primary, BookSpec }            // default Primary
public enum ERunnerTarget { TrailOnly, NearestDestination } // default TrailOnly here
```

**Future variants (not built in batch 1, no input yet):** (1) **opposite-edge reversal** after a failed break (Keller 3.3, "frequently rotates to the opposite edge"), proposed name `FailedBreakReverse` (bool, default false); (2) the BookSpec overnight 3-day runner (§14 item 1).

## 9. Logging and journal lines
Prog §8, with ID prefix `PS-02`. Setup fields: `W`, `vah`, `val`, `V`, `Hnorm`, `delta`, `acceptCount`. Exit lines carry R, MFE and MAE (Prog §8; Keller B4 §8.4 step 9). Stratifier fields `lvnBeyond`, `hvnBeyond`, `devPocVsCompPoc`, `volBeyondPct` on every accepted break. Skip reasons: `accept-flow` (when the check is on), `no-composite`, `no-balance`, `entropy`, `v-against`, `outside-window`, `event-blackout`, `roll-window`, `roll-warmup`, `risk-budget`, `noise` (BookSpec), `too-wide` (BookSpec), `mm-denied` (PT; read from the MM status if available, else inferred as no fill). `logSignal("PS02", side, close, stop, ctx, taken)` on every accepted break, including skipped ones.

## 10. Test plan
- **Windows:** Prog §5.2. W-M development Jun/Jul 2016–2022 with anchored yearly folds; validation 2023 → 31 Dec 2024 (fixed cut 2025-01-01, Prog §5.1, owner-approved); holdout sealed. Smoke tests only until the 2016–2022 minute history is verified.
- **Configs:** catalogue sweep: `AcceptCloses` ∈ {1, 2} × `StopWidthW` ∈ {0.25, 0.5} × `EntropyFilter` ∈ {on, off} = 8. BookSpec: A2-41's own 2×2×2 = 8, as a separate registered variant. Primary cell frozen before any read: (2, 0.25, on).
- **Controls:** K-FL (fake composite boxes of equal W shifted ±0.5 W and ±0.5/1/2 ATR5), K-DR (longs and shorts separately), K-RT (random-time, same bracket). Cat. PS-02 kill: mean R ≤ 0 at the 1.0 W target, or the result depends on the entropy filter alone.
- **Base-rate replication (A2-41 test 1):** P(reach 1.0 W) and median travel in W on CME for every accepted break, compared with FX 0.566 / 1.17.
- **Gates:** G0-a cost audit, G0-b canaries passed for the engine build, G0-c roll-honest composites, G0-d A2-33 verdict for the composite VAH/VAL level type (if |t| < 2, the level type is not traded).
- **Costs:** Prog §4; stresses +1 tick/side and 2×.
- **Holm family:** {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5.
- **Native protocol:** two identical chart runs → identical normalized journals; then a one-row Portfolio Trader run reconciled to the chart (PT has no Bar Magnifier: use Data1 1-minute in PT, or label the difference).

## 11. Acceptance criteria
Prog §6 defaults, plus the Cat. PS-02 kill and the A2-41 kill for BookSpec. Policy `catalogue-default` (10% cap applies).

## 12. Implementation checklist (Coder)
- [ ] Copy the header template; fill all 22 sections; §17 from table 8; §4 walkthrough from §5 here.
- [ ] `Create()`: Own RTH profile, `ProfileStats.enabled`, `PriceActionConfig.enabled`, `RollDetection = ERollDetection.Table`, `EventConfig.enabled`, `InstrumentConfig.applyCostFloor = true`, orders per Prog §7.1, `FlatBy` from input.
- [ ] `StartCalc()`: `NeedsDaily = true` (BookSpec, harmless in Primary), validate inputs (Error + no trades on invalid), write the `PS-02 CFG` line.
- [ ] Daily setup on the Globex-day change; break/acceptance counter; one attempt per day.
- [ ] Widened stop via the M7 profile floor; skip on budget; `journalArm` + `logSignal`.
- [ ] Failed-break exit counter (`FailCloses`), optional acceptance flow check, optional runner destination (HVN / naked POC).
- [ ] Position block: precedence per Prog §7.4; split from the actual position; EX-05 trail after T1; re-send stop/T1 every bar; `portfolioStop` on change.
- [ ] Early-close and event checks; `NewsExitMinutes` rule via `EventCalendar.MinutesTo(EEventTier.T1)` (Prog §7.6) with `NEWS-EXIT` / `SKIP news-window` lines; roll warm-up counter.
- [ ] Depends on the shared cost-row item (Prog §9 item 1) for NQ/CL/6E floors.
- [ ] Off-MC tests: acceptance counter, split rule (q = 1, 2, 3), precedence table, trail monotonicity.
- [ ] Native: temporary `Compile Include`, compile, smoke chart (To ≤ 12/31/2024), remove include; `git diff -- PLStudies.csproj` empty.

## 13. Header mapping
Header §4 walkthrough — Trigger: two 5m closes beyond the 3-session balanced composite edge + δ, V aligned, 09:45–14:30 ET → buy next bar open. Management: stop 0.25 W inside the composite (floored); half off at 1.0 W beyond the edge; runner trailed under each new confirmed 5m HL. Exit: stop, T1/trail, 2 closes back inside the old value (failed break), or flat 15:55 ET.

## 14. Open questions for Ettienne
1. BookSpec overnight 3-day runner: leave out of batch 1 (intraday-only arm)?
2. ~~Failed-break exit on in the primary~~: owner-approved 2026-11-10 20:31 ET. Still open: `FailCloses` = 2.
3. `AcceptCloses` 2 and δ (Keller: qualitative only); the acceptance flow check thresholds (dev POC ≥ comp POC; 25% volume beyond the edge) as an ablation; runner destination offset of 2 ticks.
4. **Short side (rev 1.5, §5a):** accept the mirrored short with identical numbers (same `AcceptCloses`, δ, `StopWidthW`, `FailCloses`), or register side-specific values? PROPOSED: identical numbers, results split by side.
5. Prog §10 batch-wide items (holdout scope, `NewsExitMinutes` default, trim rounding, early close, odd lots, data history).

## 15. Sources
Cat. PS-02, A2-41, §2.1–2.3, §2.6, EX-03, EX-05, EX-10; Keller, *Volume Profile & Order Flow: The Complete Institutional Reference* (Book 1) Ch 3.3 (Kindle Loc ~951–959); Keller, *Empirical Volume Profile & Order Flow* (Book 4) §2.4 (Loc ~721–838), §8.3, §8.4 Rules 2 and 4, daily steps 7–9 (Kindle, checked 2026-11-10; also `kindle-notes/keller-b4-*.md`); final-synthesis Spec 1A and §6.6; engines manual §3.27 (Example 27), §4.8, §4.16, §5.12–5.14; M-INSTR §9 (M7), M8, M9; live-check-sheet Check 7; Strategy-planning Page (order, acceptance, costs).
