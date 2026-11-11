# VW-01 · Trend-day first VWAP pullback — implementation spec

**Rev 1.5 (draft), 10 Nov 2026 (EST)** — add inputs (`AddMode` etc., default None) per Prog §7.7, owner-approved 22:06 ET.

**Rev 1.4 (draft), 10 Nov 2026 (EST)** — §8.1 Variants added: every true variation is selectable via Inputs, default = primary (owner, 20:31 ET). Multi-value inputs are typed C# enums, not ints or strings (owner, 20:34 ET; Prog §3 rules 8–9).

**Rev 1.3 (draft), 10 Nov 2026 (EST)** — Kindle check of Keller B1 Ch 4.3 (VWAP Setup 1, Loc ~1307–1358) and Dale VP Setup #2 applied (Kindle locations / pages; quotes read from page images, may differ slightly). Rev 1.1: costs, 2 contracts, news exit, holdout. Batch 1, build order #3. Shared rules: [00-programme-overview.md](00-programme-overview.md) (*Prog §x*). Catalogue: § VW-01; definitions §2.2 (trend day, HL entry). Target file: `sandbox/_VW_01_TrendDayVwapPullback.Strategy.CS`, class `_VW_01_TrendDayVwapPullback`. Stage: sandbox. Acceptance policy: `catalogue-default`.

## 1. Purpose and edge (with sources)
- **Mechanism.** On a day already classified as a trend day, session VWAP is the benchmark execution algorithms are measured against. Buy-side algorithms that are behind schedule buy at or below VWAP, and day traders who faded the trend cover there (Cat. VW-01). Keller B1 Part IV, setup 1: on a trend day, take the first or second VWAP touch, stop modestly beyond, target new extremes. Keller's day-type meter: repeated crossings or a flat VWAP mean balance; a one-sided sloping VWAP means trend.
- **Keller B1 Ch 4.3, VWAP Setup 1 (Kindle Loc ~1307–1358), checked 2026-11-10:**
  - "qualified trend session (one-sided, price sustained on one side of a sloping VWAP); entry at the first/second VWAP touch, stop modestly beyond (through the value building on the far side), target new extremes."
  - "qualify the day/context first". The day type is read from crossings, slope and range expansion, and labelled 30–60 minutes into the session (Drill 4.3).
  - Failure: "the pullback that accepts across VWAP and builds volume there is… the day changing type: exit, don't average."
  - Skip touches that sit in an LVN vacuum.
  - VWAP is cumulative from the session open.
  - Keller's grade: mechanism A, composite B.
  - Checked, not specified in Keller B1 Ch 4.3: any numeric thresholds, the touch definition, stop size in ticks or σ, a trailing rule, and RTH vs full-session VWAP.
- **Dale, *Volume Profile: The Insider's Guide to Trading*, Setup #2 Trend (pp. 81–89):**
  - Mark the heaviest level of a volume cluster formed in a pause inside a strong trend.
  - Enter on the first touch in the trend direction, "no need for any sort of confirmation". Stop in the LVN beyond the cluster. No trailing rule.
  - pp. 98/100: intraday SL/PT about 10–20% of the average daily ATR (swing 0.5–4× daily ATR).
  - This is the catalogue's **PS-12 / DV-04** trend-cluster pullback (Dale limit variant, Example 34, manual §3.38). It is cross-referenced here as a **future alternative** to VW-01's pullback concept (cluster level instead of VWAP; limit, no HL confirmation). It is not a new batch-1 strategy and does not change VW-01.
  - The 10–20% daily-ATR band is logged as a stop-size stratifier (`stopPctDailyAtr`), not used as a rule.
- **Evidence and the bar to clear.** Grade **B (0)** for generic VWAP touches and **D** for this conditional setup; untested. Telonics (Oct 2026): VWAP bounce rate 50.2% over 21,541 touches, and shuffling volume left 14 of 14 measurements unchanged. RipBacktest (Oct 2026): ES VWAP trend-following PF 0.89 after costs (Cat. §0 item 2). VW-01 is admissible only as a *conditional* setup that must beat a volume-shuffled VWAP and a distance-matched SMA. If it cannot, the catalogue reframes it as "trend-day pullback, any average".

## 2. Instruments and timeframes
@ES, then @NQ; CL/GC later (Cat. VW-01). Data1 = @ES 5-minute, dxFeed continuous unadjusted, 24h Globex template. Classification and HL on 5m. Warm-up: 20 sessions (ATR5 and engines); trading days start after the first full RTH session.

**RTH VWAP (important).** The F-VWAP `Session` slot resets at the chart session start (18:00 ET on a Globex chart, manual §5.10), but the catalogue's trend-day definition is RTH-anchored ("since 09:45"). **PROPOSED – owner to confirm** (Keller says VWAP is cumulative from the session open but does not say RTH or full session; checked: not specified in Keller B1 Ch 4.3): the strategy keeps a local RTH VWAP accumulator. It resets on the first RTH bar (09:30 ET bar), accumulates typical price (H + L + C) / 3 × volume per closed 5m bar, and computes σ as the volume-weighted standard deviation of typical price (the same definition as M-AVWAP σ, Cat. §2.2). Alternative: run Data1 on a day-session (RTH) template and use the `Session` slot; Selected by `VwapAnchor` (§8.1) and written in the CFG line. The same accumulator also produces the two control series (§10).

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
1. **Classification time** C = `ClassifyHHMM` (default 10:30 ET; sweep {10:30, 11:00}). 10:30 is 60 minutes in, inside Keller's 30–60 minute labelling window (Ch 4.3, Drill 4.3). The 11:00 catalogue arm is 90 minutes in, outside Keller's window. It stays in the registered sweep, but swapping it for 10:00 (30 minutes in) is **PROPOSED – owner to confirm**. From C onward, a bar may classify the day as a **trend-up day** when all three hold (Cat. §2.2):
   a. Since 09:45, at most one 5m close below RTH VWAP.
   b. VWAP slope over the last 30 minutes = (VWAP_t − VWAP_{t−6 bars}) / 6 > 0.15 × ATR5 per bar.
   c. Developing RTH POC now − developing POC at IB completion (10:30) ≥ 0.5 × ATR5.
   Once classified, the day stays classified unless **the day changes type** (Keller Ch 4.3): an accept-across-VWAP event (§5 Invalidation) declassifies the day for the rest of the session, so no new episode can start. Apart from that the proxy is not re-checked. Keller's rule settles the rev 1.1 sticky-classification question; the numeric accept-across rule stays **PROPOSED**.
2. **Touch episode:** after classification, the first 5m bar whose low ≤ VWAP + 0.25 σ starts touch episode #1 (touch band **PROPOSED**; checked: not specified in Keller B1 Ch 4.3).
   - `TouchMode = FirstOrSecond` (**primary**, Keller: "first/second VWAP touch"): episode 1 may trade. If it ends without a fill (failed, timed out or LVN-skipped), episode 2 may trade. There is still one trade per day.
   - `FirstOnly` and `SecondOnly` reproduce the catalogue's `TouchNumber` (1, 2).
   - An episode ends when the HL buy-stop fills, a 5m close < VWAP − 1 σ (failed), or `EpisodeMaxBars` = 6 bars pass with no fill (the count stays **PROPOSED**; checked: not specified in Keller B1 Ch 4.3).
   - A new episode needs a bar whose low is > VWAP + 0.25 σ in between.
   - **LVN skip** (`LvnSkip`, default on; Keller Ch 4.3): if, at the episode's first bar, VWAP lies inside an LVN node `[low, high]` of the developing RTH profile (`DevelopingExtra(1).lvns`), the episode is skipped (reason `lvn-touch`) and counts as a used touch.
3. **Trigger:** within the episode, a PRICEACTION layer-0 pivot low confirmed with `EPivotLabel.HL`. Buy-stop `VW01LE` = confirming bar high + 1 tick (Cat. §2.2 HL entry).
4. **Order life:** re-sent each bar until filled, the episode fails (close < VWAP − 1 σ), or 15:30 ET (**PROPOSED – owner to confirm**).
5. **Not on:** a declassified day (accepted across VWAP), before 10:30 / C, after 15:30, a roll-window day, `EventBlackout`, an early-close day after its entry cut-off, or once the day's trade is used.

## 5. Trade management and exits (dedicated)
| Item | Rule | Source |
|---|---|---|
| **Initial stop** | Keller: "stop modestly beyond (through the value building on the far side)". VWAP − 1 σ is the proxy for the far-side value, so the stop is the farther of (VWAP − 1 σ at the trigger bar) and (HL − 1 tick), then floored. The 1 σ count stays **PROPOSED** (checked: not specified in Keller B1 Ch 4.3). Floored at the cost floor from the planned entry (ES 14 ticks, NQ 19). **Max:** none; widened risk > 1% equity → skip. The stop price is frozen at entry (it does not follow VWAP). | Cat. VW-01; Keller B1 Ch 4.3; Cat. §2.1 |
| **Targets** | `TargetMode = SessionHigh` (primary): T1 = the RTH session high at the moment of entry (frozen), limit `VW01LT1`. With q ≥ 2, ⌊q/2⌋ exit at T1 and the runner is trailed (EX-04 below). With q = 1, `SingleLotMode` (default ExitAtT1). `TargetMode = Trail` (sweep arm): no fixed target; the whole position is trailed. | Cat. VW-01 ("session high, then trail"; sweep {session high, trail}); Keller Ch 4.3 "target new extremes" supports the session high plus a runner going for new extremes. The half-and-trail split is **PROPOSED – owner to confirm** (checked: not specified in Keller B1 Ch 4.3, trailing not stated) |
| **Minimum reward** | None (the catalogue sets none); b = (T1 − entry) / R is journaled so the E(b) report (EX-08) can show it. | Cat. VW-01; EX-08 |
| **Break-even** | None specified. Do not add. | Cat. VW-01 |
| **Trailing (EX-04 on RTH VWAP)** | Activation: after T1 fills (SessionHigh mode), or once a closed bar's high ≥ entry + 1 R (Trail mode, **PROPOSED – owner to confirm**). Rule: stop = max(stop, VWAP − 2 ticks), updated each closed bar (one-way, Prog §7.3); **plus** exit at market on the first 5m close < VWAP − 2 ticks. | Cat. EX-04 (A2-03 primary arm form, session-VWAP arm) |
| **Time stop / max hold** | Intraday. FlatBy 15:55 ET; early close: close − 5 min. No overnight. | Cat. §2.1 |
| **Invalidation: day changes type** | `AcceptAcrossExit` (default on; Keller Ch 4.3: "accepts across VWAP and builds volume there… exit, don't average"): `AcceptAcrossCloses` = 2 consecutive 5m closes below VWAP, each with volume ≥ the median 5m RTH volume so far today → `VW01LX` market next bar, reason `INVALIDATION`, and the day is declassified. The numbers (2 closes, median volume) are **PROPOSED** (checked: not specified in Keller B1 Ch 4.3). Applies before T1; after the trail activates, the tighter VWAP-loss close (above) usually fires first. No averaging down, ever (Prog §7.7). | Keller B1 Ch 4.3; Cat. VW-01; EX-04 |
| **Adds** | `AddMode` (`EAddMode`, default **None**: Keller and the catalogue do not add; Keller's "exit, don't average" forbids adding to a loser). With `OnNewPullback`: a new VWAP touch episode after T1 prints a confirmed 5m HL (buy-stop above it) while the position is open and the last close is ≥ average entry + `AddMinR` × R (the current 1R, re-based on each add fill: Prog §7.7, PR #118 §3a, owner-approved 22:35 ET); `AddContracts` 1 per add, at most `MaxAdds` (2); the stop for the whole position is raised per `AddStopMode` (default `RaiseToAddLow` = that HL − 1 tick, one-way). Adds join the runner. An add that fails the open-risk check is skipped (`SKIP add-risk`). Chart only until M-PORT supports adds (Prog §7.7). **Owner-approved 2026-11-10 22:06 ET** | Prog §7.7 |
| **Re-entry** | One trade per day in every `TouchMode`. `FirstOrSecond` moves to episode 2 only if episode 1 produced no fill. No re-entry after any fill. | Keller B1 Ch 4.3; Cat. VW-01 sweep |
| **News** | Entry blackout: no entry while `EventBlackout` (−2/+10 min, tier-1). **News exit:** `NewsExitMinutes` (int minutes; default 0 = off, **PROPOSED – owner to confirm** the default). When > 0, at the last bar close before release − N (rule `0 < MinutesTo(EEventTier.T1) < N + bar minutes`, Prog §7.6) the strategy flattens with `VW01LX`, reason `NEWS`, stops arming entries until release + 10 min, and the setup's attempt is used. When ≤ 0, an open trade holds through the release with its stop. | Owner 2026-11-10; Cat. §2.1; Prog §7.6 |
| **Holiday / roll** | As Prog §7.6. Classification still runs on a blackout or news-window bar, but no entry is armed. | Prog §7.6 |
| **Orders and fills** | Buy-stop entry; stop `VW01LS`; T1 limit `VW01LT1` (trade-through); market exits `VW01LX`. Magnifier 1m; stop-first on unresolved bars. | Prog §4, §7.1 |
| **MM interaction** | As PS-01 (pending stop: arm every bar). `PortfolioFamily = "VW"`. | Prog §7.5 |
| **Same-bar precedence** | MM force-close → stop → T1 → news exit → FlatBy → accept-across / VWAP-loss close → trail update. | Prog §7.4 |

## 6. Sizing, risk and M-PORT
As PS-02 §6. `Contracts` = 2 (owner, 2026-11-10), so the T1 + runner path is exercised. Future %-risk sizing: Prog §7.8.

## 7. Session, news, holiday and roll handling
RTH 09:30–16:00 ET VWAP; Prog §7.6.

## 8. Native inputs (header §17)
| Input | Type | Default | Units | Range | Fixed/Tuned | Purpose |
|---|---|---|---|---|---|---|
| `Side` | enum `ESide` | Both | — | enum | Fixed | Direction filter |
| `ClassifyHHMM` | int | 1030 | ET clock | {1030, 1100} | Tuned | Earliest classification time |
| `TouchMode` | enum `ETouchMode { FirstOrSecond, FirstOnly, SecondOnly }` | FirstOrSecond | — | enum | Tuned (sweep {FirstOrSecond, FirstOnly}) | Which VWAP touch may be traded (Keller Ch 4.3) |
| `LvnSkip` | bool | true | — | on/off | Fixed (Keller Ch 4.3) | Skip touches where VWAP sits in a developing LVN |
| `AcceptAcrossExit` | bool | true | — | on/off | Fixed (Keller Ch 4.3) | Exit and declassify when price accepts across VWAP |
| `AcceptAcrossCloses` | int | 2 | 5m closes | 1–4 | Fixed (**PROPOSED**) | Closes across VWAP with ≥ median volume |
| `VwapAnchor` | enum `EVwapAnchor { RthLocal, ChartSession }` | RthLocal | — | enum | Fixed (**PROPOSED**) | RTH accumulator (§2) or the F-VWAP `Session` slot (with an RTH session template) |
| `TargetMode` | enum `ETargetMode { SessionHigh, Trail }` | SessionHigh | — | enum | Tuned | Target arm |
| `TouchBandSigma` | double | 0.25 | σ | fixed | Fixed | Touch zone above VWAP |
| `StopSigma` | double | 1.0 | σ | fixed | Fixed | VWAP − kσ structural stop |
| `SlopeAtrPerBar` | double | 0.15 | × ATR5 per 5m bar | fixed | Fixed (Cat. §2.2) | Trend-day slope threshold |
| `PocMigrationAtr` | double | 0.5 | × ATR5 | fixed | Fixed (Cat. §2.2) | Trend-day POC migration |
| `MaxClosesThroughVwap` | int | 1 | closes | fixed | Fixed (Cat. §2.2) | One-sidedness since 09:45 |
| `EpisodeMaxBars` | int | 6 | 5m bars | 3–12 | Fixed (**PROPOSED**) | Touch episode length |
| `TrailOffsetTicks` | int | 2 | ticks | fixed | Fixed (EX-04) | VWAP trail offset |
| `AddMode` | enum `EAddMode { None, OnNewPullback }` | None | — | enum | Fixed per registration | Allow adds (Prog §7.7) |
| `MaxAdds` / `AddContracts` / `AddMinR` | int / int / double | 2 / 1 / 1.0 | adds / contracts / R | 0–4 / 1–5 / 0.5–3 | Fixed (**PROPOSED**) | Add limits and the in-profit gate |
| `AddStopMode` | enum `EAddStopMode { RaiseToAddLow, KeepCurrent }` | RaiseToAddLow | — | enum | Fixed | Stop handling on an add fill |
| `SingleLotMode`, `EntryEndHHMM` 1530, `FlatHHMM` 1555, `Contracts` 2, `NewsExitMinutes` 0, `RiskFractionPct`, `MaxRiskPct`, `ControlArm`, `AcceptancePolicy`, `MaxSingleTradeNetProfitPct`, `OutlierRemovalPct` | as PS-02 §8 | | | | | |

Registered sweep = 2 × 2 × 2 = 8 (Cat. VW-01, touch axis now {FirstOrSecond, FirstOnly}, **Owner-approved 2026-11-10 20:31 ET** (Ettienne: *"These changes are fine"*); `SecondOnly` stays selectable but is not swept). Primary: 10:30, FirstOrSecond, SessionHigh.

### 8.1 Variants (true variations, all selectable via Inputs; default = primary)

A variant is an alternative rule, not a different value of a swept number (Prog §3 rules 8–9). Every variant below is a native `[Input]`. A true on/off switch is a `bool`. Every multi-value choice is a **typed C# enum** declared below, with named members only: no integer codes, no strings. The default is always the primary rule, and it is set in the constructor, never by member order. Enum `[Input]`s are already used by compiled repo studies (`_CustomSignal.TraceLevel` and others, Prog §3 rule 9). Whether the chart and Portfolio Trader optimizers can sweep an enum input is **not yet verified**; Coder checks it on the MiniPC (Prog §9). Inputs marked *sweep axis* are also part of the registered sweep. Controls (`ControlArm`) and the offline evaluator setting (`AcceptancePolicy`) are not trading variants and are not listed.

| Variant | Input | Type | Values | Default (primary) | What it changes | Source |
|---|---|---|---|---|---|---|
| Touch selection | `TouchMode` | enum `ETouchMode` | `FirstOrSecond`, `FirstOnly`, `SecondOnly` | `FirstOrSecond` | *Sweep axis* {0, 1}, owner-approved 20:31 ET. Which VWAP touch episode may trade; one trade per day in all modes | Keller B1 Ch 4.3; Cat. VW-01 |
| Target structure | `TargetMode` | enum `ETargetMode` | `SessionHigh`, `Trail` | `SessionHigh` | *Sweep axis.* Trail = no fixed target, EX-04 trail activates at +1 R | Cat. VW-01; Keller Ch 4.3 |
| LVN touch skip | `LvnSkip` | bool | true / false | true | false = trade touches even when VWAP sits in a developing LVN | Keller B1 Ch 4.3 |
| Day-changes-type exit | `AcceptAcrossExit` (closes `AcceptAcrossCloses`) | bool | true / false | true | false = no accept-across exit and classification never lapses (rev 1.1 sticky rule) | Keller B1 Ch 4.3 |
| VWAP anchor | `VwapAnchor` | enum `EVwapAnchor` | `RthLocal`, `ChartSession` | `RthLocal` (**PROPOSED**) | Which VWAP/σ series the rules use | §2; Keller Ch 4.3 (not specified) |
| Adds | `AddMode` (with `MaxAdds`, `AddContracts`, `AddMinR`, `AddStopMode`) | enum `EAddMode`; int; int; double; enum `EAddStopMode` | `None`, `OnNewPullback`; 0–4; ≥ 1; R; `RaiseToAddLow`, `KeepCurrent` | `None` / 2 / 1 / 1.0 / `RaiseToAddLow` | Adds to a winner on the spec's own trigger; raises the whole-position stop | Prog §7.7; owner 22:06 ET |
| Single-contract handling | `SingleLotMode` | enum `ESingleLotMode` | `ExitAtT1`, `RunnerOnly` | `ExitAtT1` | When q = 1: exit at T1, or hold the single contract as a runner | Owner 2026-11-10; Prog §7.2 |

**Enum declarations** (nested `public enum` types in the strategy class; the input property uses the enum type, e.g. `[Input] public EStopMode StopMode { get; set; }`):

```csharp
// Shared enums (Prog §3 rule 9), declared identically in every batch-1 file:
// ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
public enum EAddMode { None, OnNewPullback }              // default None (source does not add)
public enum EAddStopMode { RaiseToAddLow, KeepCurrent }     // default RaiseToAddLow
public enum ETouchMode { FirstOrSecond, FirstOnly, SecondOnly } // default FirstOrSecond
public enum ETargetMode { SessionHigh, Trail }       // default SessionHigh
public enum EVwapAnchor { RthLocal, ChartSession }    // default RthLocal
```

**Future variants (not built in batch 1, no input yet):** Dale VP Setup #2 cluster pullback (limit at the heaviest cluster level, stop in the LVN beyond) is catalogue **PS-12 / DV-04** (Example 34), a separate strategy, not a VW-01 input. The volume-shuffled VWAP and distance-matched SMA are controls (`ControlArm`), not variants.

## 9. Logging
Prefix `VW-01`. `VW-01 CLASSIFY <date> side() t() closesThrough() slope() pocMig() atr5()` once per day when it classifies (and `VW-01 NOCLASS <date>` at 15:30 for unclassified days, so trend-day frequency is measurable). Episode lines `VW-01 TOUCH n() t() vwap() sigma()`. Exit lines carry R, MFE and MAE (Prog §8). Extra fields: `stopPctDailyAtr` (Dale band; = R / M-DAILY `Atr20d`, `NeedsDaily = true`), `declassified` and `accept-across` lines. Skip reasons: `lvn-touch`, `declassified`, `no-trend-day`, `episode-failed`, `episode-timeout`, `event-blackout`, `roll-window`, `risk-budget`, `after-cutoff`. `logSignal("VW01", …)` on every armed HL, with ctx carrying the P2-VWAP and SMA control values at the same bar.

## 10. Test plan
- Windows and costs: Prog §4–5; validation ends 31 Dec 2024 (fixed cut 2025-01-01, Prog §5.1, owner-approved).
- **Controls (Cat. VW-01, mandatory):** (a) **volume-shuffled VWAP**: the same rules run on a VWAP whose bar volumes are permuted within the session so far (seeded, deterministic; like M-AVWAP's P2 twin); (b) **distance-matched SMA**: an SMA whose length is chosen so its average distance from price over the development period equals VWAP's. The length is frozen before any performance read. (c) K-RT on classified trend days only. Run controls as separate `ControlArm` runs or as logged shadow events. **Kill:** real VWAP ≤ either fake VWAP.
- **Gates:** G0-a, G0-b (the VW-08 VWAP canary suite must be ≈ 0 or negative before VW-01 is read). Not a prior-session level, so G0-d does not apply.
- Report trend-day frequency per year (the catalogue estimates 2–5 trades per month per instrument; this is a catalogue estimate, not data).

## 11. Acceptance criteria
Prog §6 + the VW-01 kill. `catalogue-default`.

## 12. Implementation checklist
- [ ] RTH VWAP/σ accumulator (and P2/SMA shadows) with an off-MC unit test against a hand-computed session.
- [ ] Classifier state machine (holds unless declassified by accept-across), episode machine with `TouchMode` and the LVN skip (`DevelopingExtra(1).lvns`), HL trigger.
- [ ] Stop = farther of VWAP − 1σ and HL − 1 tick, floored; T1 at the frozen session high; EX-04 trail.
- [ ] Early-close/event/roll handling; one trade per day.
- [ ] Off-MC tests: classifier edge cases (exactly one close through), episode reset, trail monotonicity.
- [ ] `NewsExitMinutes` input and rule (Prog §7.6; PS-02 §12), with off-MC tests for the window edge (`MinutesTo` = N + B is not reached, N + B − ε is).
- [ ] Native compile + smoke as PS-02.

## 13. Header mapping
Trigger: after 10:30 ET on a day classified as a trend day (one-sided VWAP, VWAP slope > 0.15 ATR5/bar, POC migrated ≥ 0.5 ATR5), the first (or, if that one does not fill, the second) pullback to VWAP + 0.25 σ, not inside a developing LVN, that prints a confirmed 5m HL → buy-stop above it. Management: stop at the farther of VWAP − 1 σ and HL − 1 tick (floored); half off at the session high; runner trailed at VWAP − 2 ticks. Exit: stop, T1, 2 high-volume 5m closes below VWAP (the day changed type), a 5m close below VWAP − 2 ticks after T1, or flat 15:55 ET.

## 14. Open questions for Ettienne
1. RTH VWAP via a local accumulator (proposed) or an RTH session template? (Keller: not specified.)
2. Classification sweep {10:30, 11:00}: 11:00 is outside Keller's 30–60 minute window. Swap to {10:00, 10:30}?
3. Touch episode limit of 6 bars; order valid until 15:30; touch band 0.25 σ; stop at 1 σ; accept-across rule (2 closes, ≥ median volume). (Touch sweep {FirstOrSecond, FirstOnly}: owner-approved 2026-11-10 20:31 ET.)
4. "Session high, then trail" read as half at the session high, runner trailed on VWAP; Trail arm activates at +1 R.
5. Prog §10 batch-wide items.

## 15. Sources
Cat. VW-01, §0 items 2 (Telonics, RipBacktest), §2.2, EX-04, EX-08, VW-08; Keller, *Volume Profile & Order Flow: The Complete Institutional Reference* (Book 1) Ch 4.3 VWAP Setup 1 and Drill 4.3 (Kindle Loc ~1307–1358; also `kindle-notes/keller-b1-parts3-4.md`); Dale, *Volume Profile: The Insider's Guide to Trading*, Setup #2 Trend pp. 81–89 and pp. 98/100; Cat. PS-12 / DV-04 and engines manual §3.38 (Example 34); engines manual §5.1, §5.10, §4.7 (σ definition and P2 twin), §4.8; Strategy-planning Page (build order item 3).
