# Pre-registration: first consumer of the G-R regime engine

**Rev 2 (draft), 2026-10-22 (ET). Status: DRAFT. It is not registered until Ettienne signs it off and it is committed in a PR before any gated run.** Rev 1 plus every change in Analyst 2's review (`specs/regime-consumer-prereg-review-analyst2.md`, all accepted) and Ettienne's decisions on the gate direction, the placebo and the kill rule; see the changelog (§10). Checked against `github.com/ettiennehugo/MultiCharts.Net` `main` @ `04b92e7` (PR #23 G-R and PR #24 merged).

**Sources:** `_RegimeEngine.Function.CS` (`RegimeConfig`, `RegimeState`, `_RegimeEngine`), `_CustomSignal.Strategy.CS` (G-R hosting: `prepareRegime` / `startRegime` / `stepRegime`, `Regime`, `PortfolioRegime`), `specs/phase3-specs.md` §3.3 ("P3"), `specs/phase3-review-analyst2.md` (Q2, M8), `trading-idea-catalogue.md` (§2.0 / §2.0.1 windows, §2.1 costs, §2.3 kills, §2.6 Gate 0, §3.9 RG family, A2-55, A2-63, A2-70, SW-02, M11/M13), `specs/m-event-spec.md` §9 (clause style; `ClassifyExPost`), and Analyst 2's review of rev 1.

**This document contains no results.** No gated or ungated backtest of any candidate has been run, and no expected results are stated. Every figure below is a rule, a config value read from the code, a frequency quoted from the catalogue as an estimate, or Analyst 2's rough power estimate (§5.0), labelled as such.

---

## 0. Why this exists (the ruling)

- Analyst 2's Q2/M8: G-R may be built only after (c) "a pre-registered consumer is written ... with its kill rule".
- The engine header and `_CustomSignal` both state the **use rule**: Regime is a **prior / bucket** for a consumer (stratifier, sizing bucket, `pm.regime`), **never a direct entry trigger**. Nothing may consume `Regime` or `pm.regime` until a consumer is pre-registered with a kill rule **measured against an AtrTercile (RG-03) baseline**. "The HMM must beat the causal control or die."
- This document is that pre-registration. Until it is signed and committed, `RegimeConfig.enabled` stays `false` in every strategy except the runs in §8, and `publish` stays `false`.

## 1. Candidate consumers

Selection criteria (all must hold):
1. The host's base signal is **buildable now** on the merged base (catalogue "Readiness (as of merge)").
2. The host is **not gated by G0-d** (no prior-session level), so its own result is not waiting on the A2-33 study (§2.6).
3. Decisions are **at most once per session**, so a daily regime label (usable from the next session, P3 §3.3 "Timing") maps cleanly onto one decision.
4. The host has a **prior from the catalogue / Kaufman** that ties it to a volatility state, so the label mapping is fixed from literature, not from our data.
5. Enough trades for a decision on the holdout (§5 minimum counts).

### C1 (recommended): A2-63, long-only open + 0.5 × ATR20d breakout, exit 15:55 (host H2, `_Test6`)

- **Buildable:** catalogue A2-63 "Buildable now on the merged base" (needs ✓ M-DAILY Atr20d, F-TM). Not G0-d gated (§2.6 list). Note: `_Test6.Strategy.CS` is still the empty template; the host must be coded first (§8 step 1).
- **Core config (A2's pre-registered core, unchanged):** buy stop at O + 0.5 × ATR20d, O = 09:30 ET RTH open; 30m bar; ATR length 20; active 09:30–15:30; one entry per day; exit 15:55 ET; no stop, no target, no filter in the core. ES (MES as the cost-floor check).
- **Window relation to the host's own test:** A2-63's own test window starts Oct 2017. The evaluable window here starts at the first HMM label (≈ late 2018), so U in this document is a sub-window of the host's own validation, not a replacement for it.
- **Why it fits:**
  - Kaufman's volatility logic for this system is a **high-volatility skip** (KCTS Ch 3, Ch 18 / Ch 29 principle 6), and A2-70 names A2-63 as its primary host. That gives a label mapping fixed from a prior, before any data (§2.2).
  - **One decision per day and flat every day by 15:55.** Trades never span sessions, so the gated result is exactly the ungated trade list filtered by each day's label. That makes the placebo computable offline from one ungated run with no extra MC runs and no new inputs (§4), verified against a real gated MC run (§8, hard gate).
  - Frequency: the catalogue's author estimate is 6–10 entries a month (estimate only, not a result).
- **Interaction:** A2-70 (HV20 absolute high-vol veto) is close kin to the registered direction. It stays **off** in every run here, so the regime gate is not stacked on another vol gate. A2-70's own test must report its overlap with the G-R stressed days.

### C2 (alternate, not recommended for the first test): SW-02 base, RSI(2) < 10 dip-buy above the 200-day SMA (`_Test8`)

- **Buildable:** SW-02 "Buildable now on the merged base"; not G0-d gated. Base rule only (no profile/AVWAP filter arm). `_Test8` is also still the empty template.
- **Prior for the mapping:** SW-02 "Fails when: crash regimes (2020-03, 2022)". The mapping would be: **new entries disabled when Label = 2 (stressed)**; exits never touched.
- **Why not first (Analyst 2 agrees):**
  - The catalogue estimate is 1–3 trades a month; over a ~22-month holdout that is roughly 22–66 ungated entries before any gating, below the A2 generic 100-trade floor and likely below the §5 holdout minimum.
  - Trades span up to 5 sessions, so gating is not a pure day filter; the offline placebo needs one more equivalence check.
  - SW-02 is daily (W-D), but G-R here runs off Data1 1m via M-DAILY (from Jun 2016). A W-D run needs a daily Data n stream, which waits on P3.
- If C2 is ever tested, it is tested **under this same document** and enters the Holm family (§6).

**Recommendation: C1 (A2-63) only, with a single registered gate direction (§2.2).**

## 2. The gating rule (fixed before any result)

### 2.1 Engine config: defaults, frozen

`RegimeConfig` is protected config on `_CustomSignal` (no `[Input]`). The consumer sets it in code, and **no new strategy inputs are added**. Every field is the P3 §3.3 default except `enabled`:

| Field | Value | Field | Value |
|---|---|---|---|
| `enabled` | **true** (the only change) | `warmFilter` | 63 |
| `method` | Hmm | `switchProb` | 0.60 |
| `K` / `kSelect` | 3 / Fixed | `switchMargin` | 0.10 |
| `features` | {Return, LogHv20, Tsmom63z} (f4 off) | `seed` | 20261021 |
| `fitSessions` / `minSessions` | 1000 / 500 | `fallback` | AtrTercile |
| `refit` | Monthly (first trading day's close; deferred off roll days) | `publish` | **false** (no `pm.regime` consumer in this test) |
| `restarts` / `maxIter` / `tol` | 10 / 200 / 1e-6 | `regCovar` / `transPrior` | 1e-6 / 1 |
| `atrPctLookback` | 250 | `bicDiagnostic` | true (journal only, never switches K) |

Any change to any field, to the label mapping, or to the host config is a **new trial** and needs a new pre-registration (§6).

### 2.2 The rule (C1)

At the 09:30 ET latch of session *t*, read `RegimeState s = Regime` once and hold it for the session:

- **Usable** iff `s.Valid && !s.UsingFallback && s.AsOf < t` (`AsOf` is the previous completed day, `m_daily.PrevDate`; a value must never be used to trade its own day's RTH, P3 §3.3 Timing).
- **Gate (single registered direction):** the long buy stop for session *t* is **disabled when `s.Label == 2`** (stressed, highest f2 mean) and **enabled when `s.Label ∈ {0, 1}`**. No resizing: size stays 1 contract. The ATR arm (§2.3) disables tercile 2.
- **Why this direction (fixed before any data):**
  - **Kaufman's volatility filter for this system is a high-vol skip:** KCTS Ch 3 SPY breakout, best filter at annualised vol 0.70–0.80, which removes 2008 and 2020; Ch 18 / Ch 29 principle 6, "avoid entering when annualised vol > 50%". **A2-70** (high-volatility skip gate) names A2-63 as its primary host.
  - **A2-55** (Crabel/Kaufman volatility-contraction gate for breakout hosts) favours **low**-vol set-ups, the opposite of skipping calm days.
  - A2-63's threshold is already **k × ATR20d**, so the absolute vol level is normalised away: calm days have a small threshold, not a missing edge.
  - **Label-0 up-drift concern (K-DR):** with the f2 (ln HV20) mean ordering plus Tsmom63z in the features, Label 0 in ES is likely the low-vol up-drift state. Skipping longs there would probably remove the long-only host's best days, so a calm skip is a reason the gate could fail rather than a prior for it.
  - Rev 1's calm skip (Label 0) borrowed RG-03's single ORB note (R2); ORB is not A2-63. **Calm-skip is dropped. It is not registered as a second config**, and it may not be picked up later after seeing results (that would be a new trial with a new pre-registration).
- **The label is `Label`, i.e. after the engine's hysteresis** (switch only if p_j ≥ 0.60 and p_j − p_L ≥ 0.10). `RawArgmax` and `Probs` are journaled but not used for the decision.
- **`Probs` are the filtered posteriors** (forward pass only, P(s_t | x_1..x_t)). Smoothed posteriors are never read by the consumer or by the placebo.
- **Not usable** (warm-up, `UsingFallback`, `Valid == false`): the session is **excluded from every arm** (U, G, A, placebo), so all arms are compared on the same evaluable days.
- Exits, the 15:55 flatten and the cost model are never touched by the gate.
- Labels are **not** remapped across refits; the engine's f2 ordering decides (relabel conflicts are journaled and reported, §5.4).

### 2.3 The causal baseline arm A (mandatory per the use rule)

- **Arm A:** the same rule with the RG-03 tercile label instead of the HMM label (tercile 2 = high ATR20d percentile over 250 days → disabled; 0, 1 → enabled), on the same evaluable days.
- **Produced by the engine itself:** `fitSessions = minSessions = 100000` (passes `Validate`; the model is never fitted, so every day publishes the AtrTercile one-hot label with `UsingFallback = true`, through the same `AtrPercentile` / `AtrBucket` functions and the same hysteresis call). Hysteresis on a one-hot input always switches, so A's label equals the raw tercile; **verify this once in the journal** (`label(L) == raw(j)` on every A day). In this arm the "Usable" test drops the `!UsingFallback` clause; the day set is still G's evaluable days.
- **Validity (RG-03's own condition):** A is a valid gate only if the **dev tercile split of U is monotone in net $/trade in the gated direction** (tercile 2 worst). If not, A is reported, and every "G must beat A" guard is replaced by "G must beat U" with A's placebo percentile reported.
- **A has its own placebo** (circular shifts of the tercile path, §4.1), so the RG-03 fallback in §5.2 is judged by the same standard as G.

### 2.4 C2 rule (only if C2 is added)

Same latch and usability test at the RSI(2) entry close; **new entries disabled when `Label == 2`**; exits untouched. ATR arm: tercile 2 disabled.

## 3. Data split and walk-forward

| Item | Rule |
|---|---|
| Market / data | ES (dxFeed `@ES` 1m, unadjusted continuous; M-DAILY roll-gap removal for features and ATR20d). MES as the cost-floor check. NQ as a same-sign report (M10), not a decision. |
| Data start | dxFeed 1m from ≈ Jun 2016 (catalogue §2.0, "about 9 years"; **verify the first bar per symbol, Rev 2.7 §E2**, and journal it). |
| Engine warm-up | ≥ 64 adjusted closes for a valid feature, then 500 valid days before the first fit (P3: "after about 500 sessions (≈ 2 years)"), so roughly late 2018 from a mid-2016 start. **The actual first evaluable date is read from the journal** (first `REGIME ... fb(0)` line) and recorded; it is not assumed. |
| **Development** | first evaluable session → **2024-12-31** (catalogue W-M dev + validation, §2.0.1, as in M-EVENT §9(a)). |
| **Holdout** | **2025-01-02 → last complete session before the holdout run** (≈ Oct 2026; journal the end date). Run **once**, in the same run as the host's own W-M holdout (§2.0.1, M9b). |
| **Stage-2 order** | The **host holdout verdict (A2-63 kill) is recorded first**. If the host fails, the gate is not evaluated and is recorded as **"moot"**. A gate never rescues a host, on dev or holdout. |
| Ticks (W-T) | **Not used.** Bar Magnifier at 1 minute for the whole sample ("1m magnifier"). |
| Walk-forward | The engine's own: trailing 1000 valid days (expanding from 500), monthly refit after the first trading day's close, parameters applied from the next observation, filtered posteriors only. Fits in 2025+ use 2025+ data in their window (causal, allowed). The gate has zero free parameters, so no extra fold search is run. |
| Exclusions | Roll session ±1 (A2-39c / M2) and CME half days are excluded from all arms equally; no other day filter. |

## 4. Arms and metrics

### 4.1 Arms and placebo

| Arm | What |
|---|---|
| **U** | Ungated host (A2-63 core). |
| **G** | HMM-gated host (§2.2, skip Label 2). |
| **A** | AtrTercile-gated host (§2.3, skip tercile 2), the causal control. |
| **P_G / P_A** | Placebo gates for G and for A (below). |

**Primary null: circular shift** (fixed now).
1. Take the real label path (G's HMM labels; for P_A, A's tercile path) on the **concatenated evaluable-day index**; gaps from fallback or refit failure are dropped, not filled.
2. Shift it circularly by s days, **separately within dev and within holdout** (never across the boundary), and apply the same rule (Label 2 → off) to U's trades. This keeps the base rate, every run length and the transitions exactly.
3. **Excluded shifts:** |s| ≤ max(63, median run length of that period's path), wrapping both ends, since a near-zero shift is nearly the real path.
4. **Draws:** dev, 1000 admissible shifts drawn without replacement when available (dev ≈ 1500 days); holdout (≈ 450 days), **all** admissible shifts. N is the number of shifts used; it is journaled.
5. p = (1 + #{P ≥ G}) / (N + 1) on net PF, one-sided.

**Report-only null: semi-Markov resample.** Run-length pools per label and the jump matrix between labels, estimated on the **dev** path and **frozen at sign-off** (hash journaled); the same frozen dev law is used for the holdout draws (holdout statistics are never used; they are too few runs). 1000 draws per period. Not a decision input.

**Matched block share.** A placebo draw is accepted only if its off-day fraction in that period is within **±2 pp** of the real arm's off-day fraction (rejection sampling; acceptance rate journaled). Circular shifts satisfy this automatically; the rule binds the semi-Markov draws.

**Calendar overlap report (descriptive).** For G, A and the placebo distribution, report the share of blocked days that fall on roll week, quarterly expiry, FOMC, CPI and NFP days (M-EVENT `ClassifyExPost`). This shows whether a gate effect is really a calendar effect. Not a guard.

**Seeds and streams.** SplitMix64 (`_Rng`), seed **20261022**, stream = **(period, arm, draw)**, so dev and holdout (and G and A) use disjoint streams. Generator version and source hash journaled.

**Equivalence.** Because C1 is flat daily with one entry per day, placebos are computed offline by filtering U's trade list. The offline filter of U with G's (and A's) journaled labels must reproduce the MC trade list of G (and A) exactly, **in dev and in holdout**. This is a hard gate (§8): any mismatch voids the run until fixed.

### 4.2 Metrics (reported for U, G, A, and each placebo distribution: mean, 5th/50th/95th pct, and the arm's percentile)

| Metric | Definition |
|---|---|
| **Net PF** | gross wins / gross losses after G0-a costs (§5.3). Primary statistic. |
| **Net $ total** | sum of net trade P&L, 1 contract. Decision guard (§5). |
| **Blocked-day net P&L** | U's net P&L on the days the gate switched off (= net $(G) − net $(U) with sign reversed). Decision guard (§5). |
| **Max drawdown** | closed-trade equity peak-to-trough, $ at 1 contract; also MC intraday open-equity DD. **Reported, not a guard.** |
| **Sharpe** | mean / SD of daily net P&L over all evaluable business days (zero on no-trade days), × √252. Reported. |
| **MAR** | annualised net profit / max DD (fixed 1 contract). Reported. |
| **Trade count** | entries taken; % of evaluable days switched off. |
| Also reported | net $/trade, hit rate, avg win / avg loss, worst day, per-year net P&L, U's net $/trade per HMM label and per ATR tercile, longs only (K-DR caveat). |

**2× cost run:** commission **and** every slippage term doubled (including the 1% tail injection), **recomputed offline from the same trade lists** (no new holdout read, no new MC run).

## 5. Kill rule

Default outcome: **the gate is dropped** and no strategy consumes `Regime`. The gate is kept only if every condition below holds.

### 5.0 What the holdout can and cannot do

**The holdout can only confirm.** It cannot select, tune or rescue anything. Analyst 2's rough estimate (an estimate, not a result): at 6–10 A2-63 entries a month over ≈ 21–22 holdout months, U ≈ 130–215 trades, and with a skip-stressed gate blocking ≈ 15–25% of days, G ≈ 100–180. At n ≈ 100, PF(G) > PF(U) for a subset against its superset happens about half the time under the null, so the test has little power, and **most true but modest gates will be dropped**. That is accepted, because the default is drop.

### 5.1 Stage 1: development (read first; if it fails, the gated holdout is never read)
1. The host itself has passed its own kill (A2-63 controls and A2 generic kill, §2.3 of the catalogue) on its dev window and G0-a/G0-b. A gate is never used to rescue a host.
2. G has **≥ 150 trades** in dev (Gate 3 standard). 30–149 → inconclusive → drop. Never loosen the gate to raise the count.
3. Net PF(G) > net PF(U) **and** blocked-day net P&L < 0 (equivalently net $(G) > net $(U)); and net PF(G) > net PF(A) **when A is valid** (§2.3).
4. Placebo percentile ≥ 95% (p ≤ 0.05; Holm per §6 if the family has more than one member).
5. ≤ 50% of G's dev net P&L from one calendar year (A2 rule).

### 5.2 Stage 2: holdout (run once; host holdout verdict recorded first, §3)
The gate is **kept** only if all hold:
1. n(G) ≥ 50. Fewer → inconclusive → drop (no extension of the holdout).
2. Net PF(G) ≥ 1.15 (A2 generic kill; A2-63 is A2-origin) and net expectancy > 0.
3. **"Beats ungated":** net PF(G) > net PF(U) **and** net $(G) > net $(U) (blocked-day net P&L < 0).
4. Placebo p ≤ 0.05 on the circular-shift null (Holm per §6).
5. Net PF(G) > net PF(A) **when A is valid** (§2.3).
6. **2× costs** (offline, §4.2): net PF(G) ≥ 1.0 **and** net PF(G) > net PF(U).

Drawdown (G vs U, vs A, vs the placebo median), Sharpe and MAR are **reported, not guards**.

**RG-03 fallback.** If G fails item 5 (or fails overall) while A is valid and passes items 1–4 and 6 with A in place of G (its own placebo, P_A) at its Holm-adjusted level, **the HMM dies and the RG-03 tercile becomes the consumer** for this host. This only applies if A was declared adoptable at sign-off (§9), which puts A in the Holm family (§6).

### 5.3 Costs (G0-a)
- As G0-a / A2-40 + A2-59 and catalogue §2.1, identical on every arm: IBKR round trip + 1 tick (ES $16.98, MES $2.47 per §2.1), stop entries and market exits slip 1 tick; **2 ticks for fills in the 09:30–09:35 ET window** (the open) and around news; stop-first on ambiguous bars; the 1% tail of 4 R losses injected; cost floor rules as §2.1.
- A2-40's regime-bucketed p90 stop slippage needs ≥ 300 live fills; until then use its stated defaults (2 ticks calm, 4 ticks stressed). The "stressed" bucket is **not** taken from the G-R label (the gate must not set its own costs); it follows A2-40's own definition.
- M-PORT (`publishPortfolio`) is not coded, so the ρ = 0.9 book rule is moot for a single-instrument test; note it in the run sheet.

### 5.4 Model health
- **Preconditions (the test is not run without them):** the G-V reference check (P3 §3.3) passed on the ES series; the A-REG acceptance trace passed.
- **Reported, descriptive:** refits, failed refits (`fallback reason(nonconv|chol)`), `relabel-conflict` count, switches per year. No threshold is set, so they cannot be used to rescue or kill.

## 6. Trial budget and multiple testing

**Placebo p-value (binding).** p = (1 + #placebo draws with statistic ≥ observed) / (1 + #draws or allowed shifts), so p is never 0. With about 320 allowed holdout shifts the minimum p is 1/321 ≈ 0.0031; Holm over {G, A} at α = 0.05 needs p ≤ 0.025 for the first test, which is reachable.


- **Charged (M11 running count):** C1 = **2 configs** (G: HMM skip-stressed; A: AtrTercile skip-tercile-2). U is the host baseline already charged to the host; placebos are null distributions, not configs. RG family cap (≤ 2 thresholds × ≤ 3 values, §3.9): this uses 0 thresholds and 1 value.
- **Single direction:** only skip-Label-2 is registered. Registering both directions would have been 4 configs plus Holm; choosing one direction after dev is never allowed.
- **Not charged but frozen:** every `RegimeConfig` default, K = 3, the feature set, hysteresis 0.60 / 0.10, the placebo generators, the frozen semi-Markov dev law, seeds and streams. Changing any of them = a new trial + a new pre-registration.
- **Holm family** for the primary placebo tests = {every G config registered here} ∪ {A, if A is declared adoptable under §5.2}. Apply Holm at each stage. With the single G and A as a pure control, there is no adjustment (p ≤ 0.05). If A is adoptable, Holm over {G, A}: the smaller p must be ≤ 0.025, the larger ≤ 0.05. If C2 is added, its G joins the family. With N ≥ ~400 admissible shifts the smallest achievable p is below 0.0025, so these thresholds are reachable; the actual N is journaled.
- Per-label, per-year and calendar-overlap splits are descriptive; they never choose a different mapping.

## 7. What gets journaled

All via `_CustomSignal`'s engine sink (`ETraceLevel.Journal`), plus the run sheet:
- **Engine (already implemented):** per day `REGIME {date} label({L}) raw({j}) p({p0,p1,p2}) fb({0|1})`; `REGIME refit ... means(...)`; `REGIME diag ... bicK2/bicK4 flips relabelConflicts`; `REGIME switch`; `REGIME fallback reason(...)`; `relabel-conflict`; `roll adjust suspect`; gap lines.
- **Consumer (new, one line per session at the 09:30 latch):** `REGGATE {date} arm(U|G|A) asOf({d}) label({L}) raw({j}) pFilt({p_L}) fb({0|1}) usable({0|1}) allow({0|1})`, where `pFilt` is the filtered probability of the label actually used.
- **Per trade:** entry/exit time and price, intended (dxFeed) vs simulated fill, costs, net $, plus the `REGGATE` fields of its session.
- **Run header:** commit hash, config hash (P3 §0.5) of `RegimeConfig` + host config, **arm**, symbol, first data bar, first evaluable date, dev/holdout boundaries, Bar Magnifier resolution, cost settings.
- **Placebo output (offline):** generator version and source hash, seed, **stream id (period, arm, draw)**, N, excluded-shift band, acceptance rate (semi-Markov), frozen dev-law hash, per-draw metrics CSV, the arm's percentile per metric, calendar-overlap table.
- **Decision record:** host holdout verdict first, then the §5 checklist with pass/fail per line, dated, committed next to this document.

## 8. What Ettienne runs in MultiCharts

1. **Build the host** (`_Test6` → A2-63 core as a `_CustomSignal` subclass). Arm selection and `RegimeConfig` are set **in code** (protected constant / constructor), never as `[Input]`s: build `U` (gate off, `RegimeConfig.enabled = true`, journal only), `G` (gate on, skip Label 2), `A` (gate on, skip tercile 2, `fitSessions = minSessions = 100000`). Config hash differs per build. Unit test: the gate reads `AsOf < t` and disables exactly Label 2.
2. **Preconditions:** G-V reference check on ES passed; A-REG acceptance trace passed; host passes its own Gate 0 (G0-a costs on, G0-b canaries clean) and its own dev kill.
3. **Dev runs (charts, ES 1m from the first data bar, 1m Bar Magnifier, MC costs per §5.3):** U, G, A, each ending **2024-12-31**. Export the journals. Check A's journal: `label == raw` on every day.
4. **Equivalence check (hard gate):** offline-filter U's trades with G's and A's journaled labels; each must equal the MC trade list exactly. Stop on any mismatch.
5. **Offline:** RG-03 monotone check for A; circular-shift placebos (P_G, P_A), report-only semi-Markov, calendar overlap, metrics, 2× costs, Stage 1 checklist. **Stop here if Stage 1 fails.**
6. **Holdout (once):** record the holdout-open timestamp in §9 **before any export**. Extend U, G, A to the last complete session with the same builds (config hashes must match step 3). Record the host holdout verdict first; if the host fails, record the gate as "moot" and stop. Otherwise repeat the equivalence check (hard gate), then the holdout placebos (disjoint streams), 2× costs offline and the Stage 2 checklist.
7. **Commit** the decision record. If kept, a later PR may turn on `RegimeConfig.publish` for `pm.regime`; that is a separate consumer and needs its own pre-registration.

## 9. Sign-off

| Item | Value |
|---|---|
| Consumer(s) registered | C1 (A2-63) [ ] · C2 (SW-02) [ ] |
| Gate direction registered | skip Label 2 (stressed), single direction [ ] — skip Label 0 and "both, Holm" not registered (rev 2 decision) |
| A declared adoptable under §5.2 (puts A in the Holm family) | yes [ ] · no, control only [ ] |
| Semi-Markov dev law frozen (hash) | |
| Registered by / date (ET) | |
| Commit containing this file | |
| Holdout opened (timestamp, ET; recorded before any export) | not opened |

## 10. Changelog (rev 1 → rev 2)

All items from Analyst 2's review were accepted; Ettienne's decisions are marked **(E)**.

| § | Change |
|---|---|
| Header | Rev 2; sources add the review, A2-55, A2-70, M-EVENT `ClassifyExPost`. |
| §1 C1 | Added the window note (A2-63's own window starts Oct 2017; U here is a sub-window of the host's validation). Reason for fit now cites the Kaufman high-vol skip, not RG-03's ORB note. A2-70 must report overlap with G-R stressed days. |
| §2.2 | **(E)** Gate direction changed from skip Label 0 (calm) to **skip Label 2 (stressed)**, single direction; calm-skip dropped, not a second config. Reasoning added: Kaufman's vol filter (KCTS Ch 3, 18, 29), A2-70, A2-55's opposite (low-vol) strand, k × ATR20d normalisation, the Label-0 up-drift concern. Added that `Probs` are filtered posteriors and smoothed posteriors are never read. |
| §2.3 | **(E)** A skips tercile 2. Added the one-time journal check `label == raw`; A's validity via RG-03's monotone dev condition (else "beat A" → "beat U"); A gets its own placebo. |
| §3 | Added Stage-2 order: host holdout verdict first; gate "moot" if the host fails. |
| §4.1 | **(E)** Primary null is now the circular shift (separate within dev and holdout, minimum-shift exclusion max(63, median run), 1000 dev draws / all admissible holdout shifts). Semi-Markov is report-only with the dev law frozen at sign-off. Matched block share ±2 pp. Calendar overlap report (roll, expiry, FOMC, CPI, NFP). Streams (period, arm, draw). Equivalence check in dev and holdout. |
| §4.2 | Added net $ total and blocked-day net P&L as guards; max DD now reported only; 2× cost run defined (commission and all slippage incl. tail, offline, same trade list). |
| §5 | **(E)** New §5.0: the holdout can only confirm; Analyst 2's rough power estimate quoted as an estimate. Stage 1 item 3/4 and Stage 2 replaced per the review: "beats ungated" = PF(G) > PF(U) AND net $(G) > net $(U); DD items removed as guards; 2× costs PF(G) ≥ 1.0 and > PF(U); "beat A" only when A is valid; RG-03 fallback requires A's own placebo and Holm. Open slippage defined as fills 09:30–09:35 ET. G-V and A-REG made explicit preconditions. |
| §6 | 2 configs with the single direction (4 + Holm only if both directions had been registered; never chosen after dev). Holm family = {G configs} ∪ {A if adoptable}; Holm over {G, A} at ≤ 0.025 / ≤ 0.05. |
| §7 | Run header adds the arm; placebo output adds stream id, hashes, acceptance rate, overlap table; REGGATE adds `pFilt`. |
| §8 | Equivalence check made a hard gate in dev and holdout; A's `label == raw` check; holdout-open timestamp recorded before export; host verdict first. |
| §9 | Rows added: gate direction, A adoptable, frozen semi-Markov law hash; holdout timestamp before export. |
