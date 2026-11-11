# VW-01 · Trend-day first VWAP pullback — implementation spec

**Rev 1.1 (draft), 10 Nov 2026 (EST)** — owner answers of 19:44 ET applied (costs, 2 contracts, news exit, holdout scope; Prog rev 1.1). Batch 1, build order #3. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § VW-01; definitions §2.2 (trend day, HL entry). Target file: `sandbox/_VW_01_TrendDayVwapPullback.Strategy.CS`, class `_VW_01_TrendDayVwapPullback`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** On a day already classified as a trend day, session VWAP is the benchmark execution algorithms are measured against. Buy-side algorithms that are behind schedule buy at or below VWAP, and day traders who faded the trend cover there (Cat. VW-01). Keller B1 Part IV, setup 1: on a trend day, take the first or second VWAP touch, stop modestly beyond, target new extremes. Keller's day-type meter: repeated crossings or a flat VWAP mean balance; a one-sided sloping VWAP means trend.
- **Evidence and the bar to clear.** Grade **B (0)** for generic VWAP touches and **D** for this conditional setup; untested. Telonics (Oct 2026): VWAP bounce rate 50.2% over 21,541 touches, and shuffling volume left 14 of 14 measurements unchanged. RipBacktest (Oct 2026): ES VWAP trend-following PF 0.89 after costs (Cat. §0 item 2). VW-01 is admissible only as a *conditional* setup that must beat a volume-shuffled VWAP and a distance-matched SMA. If it cannot, the catalogue reframes it as "trend-day pullback, any average".

## 2. Instruments and timeframes
@ES, then @NQ; CL/GC later (Cat. VW-01). Data1 = @ES 5-minute, dxFeed continuous unadjusted, 24h Globex template. Classification and HL on 5m. Warm-up: 20 sessions (ATR5 and engines); trading days start after the first full RTH session.

**RTH VWAP (important).** The F-VWAP `Session` slot resets at the chart session start (18:00 ET on a Globex chart, manual §5.10), but the catalogue's trend-day definition is RTH-anchored ("since 09:45"). **PROPOSED – owner to confirm:** the strategy keeps a local RTH VWAP accumulator. It resets on the first RTH bar (09:30 ET bar), accumulates typical price (H + L + C) / 3 × volume per closed 5m bar, and computes σ as the volume-weighted standard deviation of typical price (the same definition as M-AVWAP σ, Cat. §2.2). Alternative: run Data1 on a day-session (RTH) template and use the `Session` slot; Coder documents which one is used in the CFG line. The same accumulator also produces the two control series (§10).

## 3. Engines
| Need | Engine / member |
|---|---|
| RTH VWAP + σ, slope | Local accumulator (above) |
| Developing RTH POC (classifier) | F-DEV Own RTH profile: `DevelopingSource = EDevelopingSource.Own`, `OwnProfileWindow = EOwnProfileWindow.Rth`, `RthProfile()` / `DevelopingExtra(1).poc`, read at each closed bar |
| IB (09:30–10:30) | F-SESS `SessionStats` (`ibHigh`, `ibLow`, `ibComplete`) |
| ATR5 | `averageTrueRange(Bars, 14)` |
| HL trigger | PRICEACTION layer 0, `EPivotLabel.HL` |
| Session, news, roll, cost floor, sizing, journal | As PS-02 §3 |

## 4. Entry rules (long; short mirrors on a down trend day)
1. **Classification time** C = `ClassifyHHMM` (default 10:30 ET; sweep {10:30, 11:00}). From C onward, a bar may classify the day as a **trend-up day** when all three hold (Cat. §2.2):
   a. Since 09:45, at most one 5m close below RTH VWAP.
   b. VWAP slope over the last 30 minutes = (VWAP_t − VWAP_{t−6 bars}) / 6 > 0.15 × ATR5 per bar.
   c. Developing RTH POC now − developing POC at IB completion (10:30) ≥ 0.5 × ATR5.
   Once classified, the day stays classified (no re-check). **PROPOSED – owner to confirm** that classification is sticky (the catalogue says "true at ≥ 10:30"; a non-sticky reading would require the proxy to still hold at the touch).
2. **Touch episode:** after classification, the first 5m bar whose low ≤ VWAP + 0.25 σ starts touch episode #1 (`TouchNumber` = 1 trades only episode 1; = 2 trades only episode 2). An episode ends when an HL arms (step 3), or a 5m close < VWAP − 1 σ (failed), or 6 bars pass with no HL (**PROPOSED – owner to confirm** the 6-bar limit). A new episode needs a bar whose low is > VWAP + 0.25 σ in between.
3. **Trigger:** within the episode, a PRICEACTION layer-0 pivot low confirmed with `EPivotLabel.HL`. Buy-stop `VW01LE` = confirming bar high + 1 tick (Cat. §2.2 HL entry).
4. **Order life:** re-sent each bar until filled, the episode fails (close < VWAP − 1 σ), or 15:30 ET (**PROPOSED – owner to confirm**).
5. **Not on:** before 10:30 / C, after 15:30, a roll-window day, `EventBlackout`, an early-close day after its entry cut-off, or once the day's trade is used.

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | Farther of (VWAP − 1 σ at the trigger bar) and (HL − 1 tick), then floored at the cost floor from the planned entry (ES 14 ticks, NQ 19). **Max:** none; widened risk > 1% equity → skip. The stop price is frozen at entry (it does not follow VWAP). | Cat. VW-01; Cat. §2.1 |
| **Targets** | `TargetMode = SessionHigh` (primary): T1 = the RTH session high at the moment of entry (frozen), limit `VW01LT1`. With q ≥ 2, ⌊q/2⌋ exit at T1 and the runner is trailed (EX-04 below). With q = 1, `SingleLotMode` (default ExitAtT1). `TargetMode = Trail` (sweep arm): no fixed target; the whole position is trailed. | Cat. VW-01 ("session high, then trail"; sweep {session high, trail}). The half-and-trail reading is **PROPOSED – owner to confirm** |
| **Minimum reward** | None (the catalogue sets none); b = (T1 − entry) / R is journaled so the E(b) report (EX-08) can show it. | Cat. VW-01; EX-08 |
| **Break-even** | None specified. Do not add. | Cat. VW-01 |
| **Trailing (EX-04 on RTH VWAP)** | Activation: after T1 fills (SessionHigh mode), or once a closed bar's high ≥ entry + 1 R (Trail mode, **PROPOSED – owner to confirm**). Rule: stop = max(stop, VWAP − 2 ticks), updated each closed bar (one-way, Prog §7.3); **plus** exit at market on the first 5m close < VWAP − 2 ticks. | Cat. EX-04 (A2-03 primary arm form, session-VWAP arm) |
| **Time stop / max hold** | Intraday. FlatBy 15:55 ET; early close: close − 5 min. No overnight. | Cat. §2.1 |
| **Invalidation** | Before T1, nothing beyond the stop (a close below VWAP − 1 σ is at or through the stop anyway). After the trail activates, the VWAP-loss close (above) is the invalidation. | Cat. VW-01; EX-04 |
| **Adds** | None. | Prog §7.7 |
| **Re-entry** | `TouchNumber = 1`: one trade per day. `TouchNumber = 2` trades only the second episode, also one trade. No re-entry after a stop. | Cat. VW-01 sweep |
| **News** | Entry blackout: no entry while `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm** the default). When > 0, at the last bar close before release − N (rule `0 < MinutesTo(EEventTier.T1) < N + bar minutes`, Prog §7.6) the strategy flattens with `VW01LX`, reason `NEWS`, stops arming entries until release + 10 min, and the setup's attempt is used. When ≤ 0, an open trade holds through the release with its stop. | Owner 2026-11-10; Cat. §2.1; Prog §7.6 |
| **Holiday / roll** | As Prog §7.6. Classification still runs on a blackout or news-window bar, but no entry is armed. | Prog §7.6 |
| **Orders and fills** | Buy-stop entry; stop `VW01LS`; T1 limit `VW01LT1` (trade-through); market exits `VW01LX`. Magnifier 1m; stop-first on unresolved bars. | Prog §4, §7.1 |
| **MM interaction** | As PS-01 (pending stop: arm every bar). `PortfolioFamily = "VW"`. | Prog §7.5 |
| **Same-bar precedence** | MM force-close → stop → T1 → news exit → FlatBy → VWAP-loss close → trail update. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` = 2 (owner, 2026-11-10), so the T1 + runner path is exercised. Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
RTH 09:30–16:00 ET VWAP; Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide` | Both | — | enum | Fixed | Direction filter |
| `ClassifyHHMM` | int | 1030 | ET clock | {1030, 1100} | Tuned | Earliest classification time |
| `TouchNumber` | int | 1 | episode | {1, 2} | Tuned | Which VWAP touch is traded |
| `TargetMode` | enum `ETargetMode { SessionHigh, Trail }` | SessionHigh | — | enum | Tuned | Target arm |
| `TouchBandSigma` | double | 0.25 | σ | fixed | Fixed | Touch zone above VWAP |
| `StopSigma` | double | 1.0 | σ | fixed | Fixed | VWAP − kσ structural stop |
| `SlopeAtrPerBar` | double | 0.15 | × ATR5 per 5m bar | fixed | Fixed (Cat. §2.2) | Trend-day slope threshold |
| `PocMigrationAtr` | double | 0.5 | × ATR5 | fixed | Fixed (Cat. §2.2) | Trend-day POC migration |
| `MaxClosesThroughVwap` | int | 1 | closes | fixed | Fixed (Cat. §2.2) | One-sidedness since 09:45 |
| `EpisodeMaxBars` | int | 6 | 5m bars | 3–12 | Fixed (**PROPOSED**) | Touch episode length |
| `TrailOffsetTicks` | int | 2 | ticks | fixed | Fixed (EX-04) | VWAP trail offset |
| `SingleLotMode`, `EntryEndHHMM` 1530, `FlatHHMM` 1555, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct`, `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Registered sweep = 2 × 2 × 2 = 8 (Cat. VW-01). Primary: 10:30, touch 1, SessionHigh.

## 9. Logging
Prefix `VW-01`. `VW-01 CLASSIFY <date> side() t() closesThrough() slope() pocMig() atr5()` once per day when it classifies (and `VW-01 NOCLASS <date>` at 15:30 for unclassified days, so trend-day frequency is measurable). Episode lines `VW-01 TOUCH n() t() vwap() sigma()`. Skip reasons: `no-trend-day`, `episode-failed`, `episode-timeout`, `event-blackout`, `roll-window`, `risk-budget`, `after-cutoff`. `logSignal("VW01", …)` on every armed HL, with ctx carrying the P2-VWAP and SMA control values at the same bar.

## 10. Test plan
- Windows and costs: Prog §4–5; validation ends 31 Dec 2024 (fixed cut 2025-01-01, Prog §5.1, owner-approved).
- **Controls (Cat. VW-01, mandatory):** (a) **volume-shuffled VWAP**: the same rules run on a VWAP whose bar volumes are permuted within the session so far (seeded, deterministic; like M-AVWAP's P2 twin); (b) **distance-matched SMA**: an SMA whose length is chosen so its average distance from price over the development period equals VWAP's. The length is frozen before any performance read. (c) K-RT on classified trend days only. Run controls as separate `ControlArm` runs or as logged shadow events. **Kill:** real VWAP ≤ either fake VWAP.
- **Gates:** G0-a, G0-b (the VW-08 VWAP canary suite must be ≈ 0 or negative before VW-01 is read). Not a prior-session level, so G0-d does not apply.
- Report trend-day frequency per year (the catalogue estimates 2–5 trades per month per instrument; this is a catalogue estimate, not data).

## 11. Acceptance criteria
Prog §6 + the VW-01 kill. `catalogue-default`.

## 12. Implementation checklist
- [ ] RTH VWAP/σ accumulator (and P2/SMA shadows) with an off-MC unit test against a hand-computed session.
- [ ] Classifier state machine (sticky), episode machine, HL trigger.
- [ ] Stop = farther of VWAP − 1σ and HL − 1 tick, floored; T1 at the frozen session high; EX-04 trail.
- [ ] Early-close/event/roll handling; one trade per day.
- [ ] Off-MC tests: classifier edge cases (exactly one close through), episode reset, trail monotonicity.
- [ ] `NewsExitMinutes` input and rule (Prog §7.6; PS-02 §12), with off-MC tests for the window edge (`MinutesTo` = N + B is not reached, N + B − ε is).
- [ ] Native compile + smoke as PS-02.

## 13. Header mapping
Trigger: after 10:30 ET on a day classified as a trend day (one-sided VWAP, VWAP slope > 0.15 ATR5/bar, POC migrated ≥ 0.5 ATR5), the first pullback to VWAP + 0.25 σ that prints a confirmed 5m HL → buy-stop above it. Management: stop at the farther of VWAP − 1 σ and HL − 1 tick (floored); half off at the session high; runner trailed at VWAP − 2 ticks. Exit: stop, T1, a 5m close below VWAP − 2 ticks after T1, or flat 15:55 ET.

## 14. Open questions for Ettienne
1. RTH VWAP via a local accumulator (proposed) or an RTH session template?
2. Sticky classification once true?
3. Touch episode limit of 6 bars; order valid until 15:30.
4. "Session high, then trail" read as half at the session high, runner trailed on VWAP; Trail arm activates at +1 R.
5. Prog §10 batch-wide items.

## 15. Sources
Cat. VW-01, §0 items 2 (Telonics, RipBacktest), §2.2, EX-04, EX-08, VW-08; Keller B1 Part IV setup 1 and the day-type meter (`kindle-notes/keller-b1-parts3-4.md`); engines manual §5.1, §5.10, §4.7 (σ definition and P2 twin), §4.8; Strategy-planning Page (build order item 3).
