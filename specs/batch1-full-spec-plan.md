# Batch 1: full-spec rebuild plan (GX-5), one PR per strategy

**Rev 1.1, Sunday 22 Nov 2026 (ET).** Coder's review (12:38 ET) and Ettienne's 12:41 ET answers folded in, each Coder point re-checked against `bd0eeb5`, `4135815` (#145) and `f7b2c56` (#147 tip at the time; AV-02 re-checked on `d559676`): §0 hostGate attribution corrected (#145); §7 corrected (#146 is a draft at `6d05819`, not merged); check D's baseline is now an open decision for Ettienne; AV-02's overnight hold is being fixed by Coder on #147; Coder's size estimates added (§7); OF-01 stays ≤ 2024-12-31 (**Confirmed by Ettienne 2026-11-22 (via Educator card)**). **Rev 1, Sunday 22 Nov 2026 (CT).** Prepared by Analyst (Grok Bot) for Coder. **Decision 3 of 2026-11-22, Confirmed by Ettienne 2026-11-22** (*"Yes, confirm all four decisions as written."*, about 12:20 ET, relayed to Analyst): the batch-1 strategies must be the **full spec versions** in `specs/strategies/` (01 PS-02, 02 PS-01, 03 VW-01, 04 AV-02, 05 AV-01, 06 OF-01; Rev 1.6–1.7, shared rules in the programme overview R1.8 / 1.9), not the engines-manual worked-example ports now in `sandbox/`. That includes **GX-5**: one PR per strategy, each with an equivalence re-check (`specs/generic-exit-manager-spec.md` §8.6). Docs only; code is Coder's.

**Basis.** Every gap below was checked against the code on `origin/main` @ `bd0eeb5`; line numbers are on that commit. The source was Educator's map (`educator-notes/spec-code-map.md` §4). Where I could not confirm a map claim, or confirmed it with a correction, the entry says so. The strategy specs themselves are **not edited**. I found no internal inconsistency that needs a spec change. The notes in §8 are cross-document points only.

## 0. Shared scope for all six PRs

Shared gaps, verified at bd0eeb5 on all six files:
- **Rolls:** all six set `RollDetection = ERollDetection.Series`: PS-02 L180, PS-01 L180, VW-01 L156, AV-02 L153, AV-01 L153, OF-01 L174. Spec: Table. Follow `m-instr-spec` rev 2.6 §7.1 (Table + `fillProfileRollDates`).
- **Cost floor:** PS-02 L157, PS-01 L156 and OF-01 L153 hard-code `costFloorTicks = 14`. VW-01, AV-02 and AV-01 read the profile floor but silently fall back to 1 tick (`int ticks = 1;` in `widen`). Spec: M7 `applyCostFloor = true` with the profile floor; a missing row fails loudly (`m-instr-spec` §9.1).
- **Size:** PS-02 / PS-01 / OF-01 use `contracts = 1`; VW / AV use `Contracts.Default`. Spec: 2 contracts (Prog §7.2).
- **Inputs:** `ESandboxGate` / `ESandboxExit` / `ESandboxSide` instead of each spec's typed `Variant` / `ESide` and Long/Short numeric pairs (Prog §7.9 item 12). VW-01, AV-02 and AV-01 are long only.
- **Missing everywhere:** `git grep` on the six files finds no `EventConfig`, `applyCostFloor`, `FailedBreak`, `NewsExit`, `SHORT SIDE UNCONFIRMED`, `T1-SKIPPED` or add path (verified). So there is no news blackout, no `NewsExitMinutes`, no adds, no short-unconfirmed header, and no CFG journal line.
- **Exits:** where GX is used, it runs `SingleTarget` + `ETrailMode.None` (full exit at T1). PS-01 and OF-01 still use local `sendExits`. GX-5 wants `ScaleOutThenRunner` 50% plus the strategy's own trail.
- **Stale headers:** PS-01 L133–134 says GX is "not in this build", but GX is on main.

Each PR:
1. A **new file** with the spec's class name, so the sandbox port stays available as a reference (whether it is the check-D baseline is open, see check D): `_PS_02_BalanceBreakClose` already matches, so its PR rewrites in place and keeps the port at a tagged commit. The others are `_PS_01_BalanceBreakPullback` (matches), `_VW_01_TrendDayVwapPullback`, `_AV_02_ImpulseAvwapHigherLow`, `_AV_01_SwingHighAvwapReclaim` and `_OF_01_StackedImbalanceRetest` (matches). Lifecycle stays `sandbox` (header §1), and the files stay out of `PLStudies.csproj` unless Ettienne promotes them.
2. Rules exactly as the spec's §4 / §5 / §5a. Inputs as §8 and §8.1 (typed enums; default constructor = primary rule). Logging as §9. Coder checklist §12.
3. Prerequisites: the roll and cost-floor engine tasks T-R1, T-C1 and T-C2 (`specs/sandbox-roll-floor-migration.md`) are merged first. GX flat-by early close (#146) is merged first for the intraday strategies. The enum-input MiniPC check (Prog §9 item 1) is recorded.
4. Rebase on #144 / #145 / #147 once they merge: they edit the same six sandbox files. For example, #145 (`4135815`) adds the `hostGate` calls before AV-02's entry orders (none on `bd0eeb5` or #144 `2df89f0`; the call sites appear in #145 and are kept on `f7b2c56`).

### Acceptance and equivalence checks (every PR)

- **A. Off-MC unit tests** for the MC-free parts: the state machine, the split rule, same-bar precedence (Prog §9 item 5).
- **B. Native compile** on the MiniPC (F7 only; one temporary `Compile Include`, not committed).
- **C. Determinism:** two identical chart runs give identical normalised journals.
- **D. GX-5 equivalence re-check: baseline OPEN, Ettienne to decide** (GX spec §8.6; Analyst, MiniPC, ≤ 2024 data). GX spec §8.6 says the engine version must match "the hand-coded version" trade for trade (same entries, exit bars, exit prices, exit reasons and R), assuming each strategy already had a hand-coded PR #117 §7 exit. **No such hand-coded full-spec exit exists** (Coder review, verified: §8.6 replaces "its hand-coded PR #117 §7 exits"; the sandbox ports are manual-example strategies, already partly on GX `SingleTarget` or on local `sendExits`, see §0). Options:
  - **D1. Hand-coded baseline:** each PR first builds the full-spec strategy with hand-coded exits, then the GX version, and matches them trade for trade as §8.6 says. Faithful to §8.6, but each PR needs both versions, which roughly doubles the work (§7 sizes).
  - **D2. Port behaviour plus spec-rule assertions:** build only the GX version. Compare it with the current sandbox file's behaviour as an explained delta list (entries that should match do; every difference is traced to a spec rule), and add assertions for each spec exit rule from the journal (T1 at 1 W for half, trail / runner rules, flat-by, failed-break, `T1-SKIPPED`). Cheaper, but not the trade-for-trade equivalence §8.6 describes, so §8.6 would need a one-line amendment.
  Until Ettienne picks one, check D is defined by that choice; checks A–C and E–F are unaffected.
- **E. Spec acceptance** (§11 of each spec): 0 Errors except the expected `skipped: multi-contract risk exceeds 1 percent`; the journal lines of §9 are present; floors per market; Table rolls with the confirmation journal.
- **F. Portfolio Trader one-row reconciliation** against the chart, with Bar Magnifier off. PT has none, and decision 4 makes off the default.

### Test windows (all PRs)

- **Smoke / equivalence:** validation window W-M, 1 Jan 2023 → 31 Dec 2024, @ES 1m (Data1/Data2 as each spec's §2).
- **Development:** first verified full month of 2016-06 → 31 Dec 2022, with the anchored yearly walk-forward of Prog §5.2. It is an engineering smoke test until the 2016–2022 minute history is confirmed downloaded and flushed in QuoteManager (Prog §10 item 5).
- **Holdout:** nothing on or after **2025-01-01** for any strategy in this plan, OF-01 included (see §6). No prices or results from the holdout are viewed or reported.

---

## 1. PS-02: `_PS_02_BalanceBreakClose` (spec 01, Rev 1.6)

**Gaps (verified at bd0eeb5):**
- δ is a flat `deltaTicks = 2` (L154). Spec: max(2 ticks, 0.10 × ATR5) (spec §4 item 2).
- The stop is always `stopW = 0.30` W (L155), and the noise / too-wide skips always apply. Spec: primary 0.25 W; 0.30 W and those skips are BookSpec-only.
- No `FailedBreakExit`. Spec: on by default (owner 2026-11-10 20:31 ET).
- Full exit at `targetW = 1.0` with `TargetMode = SingleTarget` and `TrailMode = None` (L245, L249). Spec: EX-05 runner, i.e. scale 50% at 1 W (`PS02LT1`) and trail the rest, with `T1-SKIPPED` when T1 sits inside the floor.
- Entropy is always on: `ProfileStats.enabled = true` (L182) and `Hnorm <= maxEntropy` (L268). Spec: BookSpec / optional filter.
- `hostGate` (L275) and `honorExit` (L293) are defined but never called on bd0eeb5. #145 changes that.
- Shared items as in §0.

**PR scope:** a rewrite in place to spec §4 / §5 / §5a / §8, with GX `ScaleOutThenRunner` + EX-05 trail, the failed-break exit and the `Variant` enum (primary / BookSpec A2-41).
**Specific checks:** a failed-break exit fires on a re-entry into value; `PS02LT1` is half the position; `T1-SKIPPED` lines; BookSpec variant parity with the old 0.30 W behaviour as a delta list.
**Windows:** §0.

## 2. PS-01: `_PS_01_BalanceBreakPullback` (spec 02, Rev 1.6)

**Gaps (verified):**
- The stop is 1 tick beyond the HL/LH pivot, then the 14-tick floor (L259). Spec: `StopMode = BalancePoc` (primary), with the pivot stop as the `HlPivot` variant.
- Full exit at `targetW = 1.0` through local `sendExits` (L223–234). Spec: 50% at 1 W, runner to the next HVN / nPOC, through GX.
- `hostGate` is called (L268) and `sandboxBook()` is called (L237), so the map is correct on this file.
- `holdW = 0.25` (L154) matches the spec.
- Shared items as in §0.

**PR scope:** GX migration (GX-5) plus `BalancePoc` stop, runner and spec inputs. Reuse PS-02's break / acceptance state machine (Prog §9 item 2).
**Specific checks:** stop = comp.poc ∓ `StopBufferTicks`, widened to the floor; runner target logged; check D per Ettienne's baseline choice.

## 3. VW-01: `_VW_01_TrendDayFirst` → `_VW_01_TrendDayVwapPullback` (spec 03, Rev 1.7)

**Gaps (verified):**
- `Vwap1Type = EVwapType.Session` (L157) is the Globex-session VWAP. Spec: an RTH-only local VWAP accumulator (spec "RTH VWAP (important)").
- Slope: ATR(5) on Data1 1m (L187) and a 5-minute alignment gate `tod % 5 == 0` (L186). Spec: ATR14 on the 5m series, slope over 6 bars.
- The POC-migration classifier leg is missing (code reading).
- The pullback is a crude 5-bar low (map L189–191, consistent with the code I read). Spec: PRICEACTION higher low.
- Target = `Session.rthHigh` (L196) matches.
- `TrailMode.None` (L210); intraday `FlatBy` (L211). `LvnSkip`, `AcceptAcrossExit` and the EX-04 trail are absent.
- `hostGate` (L244) is never called on bd0eeb5.

**PR scope:** a new file with the spec's class name; RTH VWAP accumulator; 5m Data2 slope; POC-migration leg; PRICEACTION HL; acceptance-across exit; EX-04 trail; adds input (Prog §7.7); short side with the UNCONFIRMED header.
**Specific checks:** the RTH VWAP resets at 09:30 ET, not 18:00; the trend-day classifier is logged at ≥ 10:30 ET; `AcceptAcrossExit` fires.

## 4. AV-02: `_AV_02_ImpulseOriginAVWAP` → `_AV_02_ImpulseAvwapHigherLow` (spec 04, Rev 1.7)

**Overnight holding: verified.** The spec's batch-1 scope is the **intraday arm** only (spec L16): entries 09:45–15:00 ET (L37), flat at the 15:45 ET bar, "No overnight" (L46), and `TradesPerDay` (owner-approved enum). The sandbox port calls `arm(true, entry, stop, peak, "AV02LE", true)` (L182). With `hold = true`, `arm` sets `GxConfig.SessionExit = ESessionExit.HoldOvernight` and `MaxHoldDays = 10` (L195–210). The port therefore holds overnight for up to 10 days against an intraday spec. It also has no entry window and no trades-per-day cap. AV-02 is **not** one of the 19 studies Ettienne switched to hold overnight in #147. On the #147 tip `f7b2c56` it still passes `hold = true`, so the overnight hold is a port artefact, not an owner ruling. **Fixed on #147 at `d559676`** ("AV-02 flat-by; only approved studies may select a GX hold"): the port now calls `arm(true, entry, stop, peak, "AV02LE", false)` (L187), so GX runs it flat-by with no overnight hold. The full-spec PR still adds the 09:45–15:00 ET entry window, the 15:45 ET flat and `TradesPerDay`. AV-01's overnight hold is correct for its spec (Coder agrees).

**Other gaps (verified):**
- The anchor is `AvwapManager.Latest(SwingLow)` (an approximation). Spec: impulse origin from a confirmed ≥ 3 ATR PRICEACTION swing.
- The HL is the low of the last 15 one-minute bars standing in for a 15m low, inside the band −0.25σ…+0.5σ. Spec: Data1 = 15m fractal / PRICEACTION HL.
- `TrailMode.None` (L202); no adds.
- Target = peak since the anchor (matches).
- `hostGate` (L214) is never called on bd0eeb5.

**PR scope:** a new file; 15m Data1 per spec §2; intraday window; flat at 15:45 ET; `TradesPerDay` enum; trail / `TargetMode` {ImpulseHigh, Trail}; adds; short side (swing-high anchor) with the UNCONFIRMED header.
**Specific checks:** **zero positions held past the 15:45 ET bar on any day** of 2023–2024; on early-close days, the last bar ending ≥ 5 min before the close; no entries outside 09:45–15:00 ET; the trades-per-day cap holds.

## 5. AV-01: `_AV_01_SwingHighAVWAP` → `_AV_01_SwingHighAvwapReclaim` (spec 05, Rev 1.7)

**Gaps (verified):**
- The daily filter is `Bars.Close[0] > DailyContext.Sma200` (with `smaLength = 50`) on the current 1m bar (L174). Spec: `DailyContext.PrevClose > Sma(50)` (spec L172).
- 60m proxy: clock-hour 1m closes (`tod % 60 == 0`, L176). Spec: Data2 = 60m, closed bars, δ60, setup-cancel rule.
- The HL is a 15-bar low comparison. Spec: PRICEACTION / fractal HL above AVWAP.
- Target = `Bars.High[ago]` (L196), the high of the anchor bar. Spec: the swing-high price.
- `arm(..., true)` (L198) means `HoldOvernight`, `MaxHoldDays = 10` (L221–223). Spec: hold up to 20 sessions, flatten one session before a roll, AcceptancePolicy `big-winner-trend`.
- `hostGate` (L233) is never called.

**PR scope:** a new file; Data2 60m; prior-close SMA50 filter; swing-high target; 20-session max hold; roll-ahead flatten using the Table roll dates (`IsRollWindow`); big-winner-trend policy; short side, owner-approved (`Side` default `Both`).
**Specific checks:** no position is held across a Table roll date; max hold ≤ 20 sessions; weekend holding is allowed.

## 6. OF-01: `_OF_01_StackedImbalanceRetest` (spec 06, Rev 1.6, data-gated)

**First-test claim: partly verified, with a correction.** The mechanism Educator describes is real. On the first armed bar whose low ≤ zone hi (long) or high ≥ zone lo (short), the code sets `m_used = true`, clears the zone and returns without sending a limit (L231 / L238). But MultiCharts keeps an order sent at bar t's close working through bar t+1. So the limit sent on the previous armed bar (L233 / L240) **is** working during the test bar and can fill there. Spending the zone after an unfilled test also matches spec rule 5: *"After it, the zone is spent whether or not an order filled."* The real defects are narrower:
- (a) If the previous closed bar sent no limit, the test bar spends the zone with nothing working, and the first test is missed. This happens when the previous bar was outside RTH (early return at L223), when `mustFlatten()` was true, or when the side was disallowed.
- (b) A test during ETH does not spend the zone, because the code returns at L223 before the zone logic. Spec rule 5 (PROPOSED in the spec) says an ETH test spends it.
- (c) Zones are cleared at every Globex day change (L209). Spec: a zone lives until its first test, a roll or `ZoneMaxSessions` (5).

**Other gaps (verified):**
- `closeTopFrac = 0.30` is on (L154, L247). Spec: an optional filter, off by default.
- `minVol = 10` is a PLACEHOLDER (L154). Spec: `VolFloorPct` 70.
- Arming is a 1 × ATR14 move away (L230 / L237). Spec: `MinAwayBars` whole footprints.
- `targetR = 1.0` fixed R (L154, L267). Spec: `DaleHvn`, MinRR, aggression trail, variant K.
- The footprint call is on Data1 (L248). Spec: a 30m zone TF with a stop band.
- `contracts = 1` (L153); local `sendExits` (L213), no GX.

**PR scope:** spec §4 zone store (life, `MinAwayBars` arming, first-test-spent logic including ETH, and the limit working on the test bar); 30m aggregator; `DaleHvn` target; aggression trail; variant K; GX migration.
**Specific checks:** a unit test per first-test path: armed bar, then a limit-working test bar, then fill / no fill → spent; ETH test → spent; no limit on the previous bar → the test is logged as `missed-no-order`. One trade per zone at most.
**Window:** the spec's W-T runs from 2024-10-28 to the day before the rolling cut (2025-04-30 as of 2026-11-10, Prog §5.1). **This plan stops at 2024-12-31**, about 45 sessions; the rest stays held out (**Confirmed by Ettienne 2026-11-22 (via Educator card)**).

---

## 7. PR order

1. Engine prerequisites T-C1 → T-C2, and T-R1 (roll journal). #146 (GX flat-by early close) is **not merged**: it is a draft PR at `6d05819` and must be merged by Ettienne before the intraday strategies' PRs.
2. PS-02 → PS-01 (shared break detector) → VW-01 → AV-02 → AV-01 (programme overview §1 order, GX §8.6 "simplest exits first").
3. OF-01 last, after the @ES bid/ask corpus is verified in QuoteManager (spec §10).

Each PR is opened only after the previous one merged. Analyst runs checks D–F after each.

**Rough sizes (Coder's estimates, 2026-11-22):** PS-02 1–2 d; PS-01 1–2 d; VW-01 about 2 d; AV-02 about 2 d; AV-01 2–3 d; OF-01 3+ d. Each roughly doubles if check D needs a hand-coded baseline (option D1).

**Open for Ettienne:** the check-D baseline, D1 or D2.

## 8. Cross-document notes (no strategy spec edited)
- AV-01 spec L60 ("Bar Magnifier 1m on Data1") and OF-01 L54 ("tick detail inside the verified tick window") predate decision 4 (Bar Magnifier off by default). Under programme overview rev 1.9 those become the labelled magnifier sensitivity runs.
- The batch-1 specs cite costs as "$17.00 ES / $9.48 NQ" (AV-01 L165, OF-01 L275). That is consistent with the confirmed rows. Floors come from the profile.
- The map's "honorExit runs after arm" note on VW-01 is verified: on bd0eeb5, L198 calls `arm` and L199 calls `honorExit`. It is inert at the default `ExitMode = Off`.
