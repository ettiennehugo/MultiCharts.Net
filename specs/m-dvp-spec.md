# M-DVP spec: detrended volume-profile engine (Keller B5)

**Rev 5.3, 2026-10-24 (ET), approved by Ettienne Hugo 2026-10-24** (rev 5 plus the 5.1–5.3 amendments in the revision table). Rev 4 plus PR #37 review spec edits S1–S5 and the instrument-profile DV-7.0 amendment (rollMode None) (`specs/pr37-review-analyst1.md`; see the revision log). Rev 4 (2026-10-24): Rev 3 plus Ettienne's DV-3.8 decision, the DV-3.6 roll-horizon rule and removal of the 240m sweep (see the revision log). Rev 3 (2026-10-23): Rev 2 plus Ettienne's decisions on Q1, Q4, Q6 and the PS-25 primary cell. Nothing is OPEN; only VERIFY/[GAP] items remain. Rev 2 was Rev 1 plus all 16 edits (E1–E16) of Analyst 1's review (`specs/m-dvp-review-analyst1.md`), with every code claim re-checked on `origin/main` @ `629da0d`. Q2, Q3, Q5 and Q7–Q15 are resolved as Analyst 1 recommended. See the revision log below.

Implementation spec for the engine described in `specs/keller-b5-catalogue-additions.md` rev 3.1 §2 (signed off 2026-10-23) and the catalogue's §2.4b M-DVP row. Checked against `github.com/ettiennehugo/MultiCharts.Net` `origin/main` @ `629da0d` (PRs #14–#29). Every class, method and line number below was read on that commit, except where it is marked **VERIFY**. Items the book does not settle are marked **[GAP]** and are fixed here by a stated default that is listed in §14.

**Sources:** the repo (`_ProfileMath`, `_ProfileStats`, `_LevelRef`, `_RollDetector`, `_BarRing`, `_MtfMath`, `_PriceActionEngine`, `_Gate0`, `_LevelStudy`, `_CustomSignal`, `_VolumeProfileBase`, `_VpHistogram`, `tests/*`), `trading-idea-catalogue.md` (§2.4b L217–259; rows A2-33, PS-25, MT-07, RG-09, RG-10, KD common/KD-02/KD-06), `specs/keller-b5-catalogue-additions.md` rev 3.1 ("B5"), the Analyst 1 and Analyst 2 reviews and P6/P7 entries (superseded points are listed in §14.2), and `kindle-notes/keller-b5-dvp-analyst2.md` (Appendix E functions).

**Scope.** M-DVP is a **base service**, not a strategy. It fits a polynomial trend to the last `winLen` closed bars of a stream, builds a volume profile of the residuals (the "manifold"), and publishes **rails**: trend-following price levels for the next bar. It also builds a flat Fibonacci lattice from confirmed swing pivots and counts how many independent families agree at a price. It produces levels, states and journal lines. It places no orders. The consumer ideas (PS-25, MT-07, RG-09/KD-09, RG-10, KD-02, KD-06, KD-10, A2-33) are written in their own sandboxes and specs; this spec only fixes the API they read.

**Not touched.** The PriceAction library (`/workspace/PriceAction`) is not changed. The repo's wrapper `_PriceActionEngine` is used as it is. No `[Input]` is added to `_CustomSignal` or to any strategy.

### Revision log

| Rev | Date (ET) | Change |
|---|---|---|
| 1 | 2026-10-23 | First draft. |
| 2 | 2026-10-23 | Analyst 1 review applied: E1 score = family count (DV-6.5, DV-10.1; Q12 moot). E2 entropy call `Entropy(rows, TradedRows, 0)` (DV-4.5). E3 concentration = POC row / mean row (DV-4.5). E4 δ only on the trigger close; engine has no counting default (DV-8.2/8.3). E5 fixed row array, no `Accumulator.Dense()` (DV-0.8, DV-4.2). E6 forward coverage with the leverage term (DV-3.6). E7 verbatim 24-ratio Fib list (DV-5.3). E8 RollDetection Off ⇒ diagnostic-only (DV-7.0, DV-T27). E9 sessions/24h/half-days (DV-7.7, DV-T28/29). E10 lazy path-independence (DV-3.7/3.8, DV-T16b). E11 PS-25 primary cell PROPOSED (§10.4). E12 skip-share estimates labelled (DV-7.4). E13 `verdict41`, horizon 40 (DV-8.4, DV-11.2). E14 VA/edge/z=0/POC-tie basis (DV-4.4). E15 PR plan additions (§16). E16 conflicts l–n (§14.2). §14.1 rewritten as resolved decisions + OPEN items. |
| 3 | 2026-10-23 | Ettienne's decisions: Q1 = the trigger close plus 2 further closes beyond the rail; Q4 = drop 240m (MT-07 on 60m only, 240m diagnostic, no winLen 90 trial); Q6 = Fib direction pre-registered now (App E read before PR-D4 optional); PS-25 primary cell CONFIRMED (N = 2, VAH/VAL rails, EX-05 trail + 41-bar time stop). `Accepted` semantics fixed: N = further closes after the trigger when countTrigger = true; PS-25 passes countTrigger = true, N = 2; KD-06/KD-10 pass countTrigger = false (their legacy "N closes including the trigger" reading). Updated: header, DV-1.4, DV-5.3, DV-7.4, DV-7.5, DV-8.1, DV-8.3, §10.4 PS-25/MT-07/KD-06/KD-10 rows, §14.1, §14.2-i/j, DV-T19, DV-T19b, PR-D5. |
| 4 | 2026-10-24 | Ettienne's decision: DV-3.8 endpointDrift redefined to match PR-D3 code (previous fit evaluated at its own last bar / previous refit bar; NaN across roll or out of buffer). DV-3.6: roll-crossing forward horizons dropped, count journaled in DVP RUN. 240m winLen sweep removed (DV-1.1, DV-7.5, PR-D6). |
| 5 | 2026-10-24 | **APPROVED (Ettienne Hugo, 2026-10-24).** PR #37 review (Analyst 1) spec edits: S1 DV-5.1 `minBreakoutLayer = 0` in the PA config (Validate requires 0..layers−1), VERIFY resolved (XMKT `findSharedPa` pattern), host reference updated to `_CustomSignal` ~L1044 (PR #37 head). S2 DV-5.2 anchor tie rules, A selection, inclusive lookback after selection with no fallback. S3 DV-5.4 seed-based grouping (no chaining), fibNear ≤ applied after merge. S4 §2 `fibAnchor` valid = MostRecentPivot only; `ExtremeInLookback` rejected by Validate. S5 DV-0.3/§15 gen.sh extracts `// <dvp-pa>`; `_PriceActionEngine` linked whole in tests/Dvp. Instrument-profile amendment (M-INSTR rev 3 IP-9.4): DV-7.0 rollMode None → skipShare 0, stream stays tradeable; ES with RollDetection Off unchanged; different root on Data2/3 still rejected; waits on DVP host PR #36; DV-7.4/DV-11.3 consequential edits, DV-T27b/c added. Still DRAFT pending Ettienne review. |
| 5.1 | 2026-10-24 | Approved by Ettienne Hugo (2026-10-24), from the PR #39 review: S1 HVN/LVN match cap |Δz| ≤ 0.25 (DV-8.1); S2 ATR history depth (DV-8.1); S3 verdict41 v0 definition (DV-8.4/DV-11.2); S4 candidate slots 1:1 (DV-6.3/6.5). |
| 5.2 | 2026-10-24 | Approved by Ettienne Hugo (2026-10-24), from the PR #41 review: HtfAccepted signature with countTrigger/deltaAtr (§10, DV-9.4); NextRailDistAtr strictly-beyond and NaN cases (DV-9.4); Tradeable never consults diagnosticOnly (DV-9.3/DV-7.0). |
| 5.3 | 2026-10-24 | Approved by Ettienne Hugo (2026-10-24), from the PR #42 review: DV-12.1 halfDay on the per-session Journal line joined by day (S1); level registered after LevelStudy Bar(), first evaluated next bar (S2). |

---

## 0. Cross-cutting rules (inherited)

- **DV-0.1 Laziness.** `_DvpEngine` is an `internal sealed class` created in `StartCalc()` only when `DvpConfig.enabled` is true, and is `null` otherwise. This is the MFS §0.1 pattern used by `PriceActionConfig` / `m_pa` (`_CustomSignal` L383–397). When the engine is disabled, it costs nothing: no ring is requested, no hosted pivot engine is created, and `CalcBar` does one null check. A query while disabled returns neutral values (`null`, `NaN`, empty lists) and logs one `ETraceLevel.Warning` (`m_dvpQueryWarned`, the `m_paQueryWarned` pattern). When the engine is enabled, the fit runs **on demand**: the first query on a closed bar computes the snapshot and caches it per (stream, bar). Journal output (`journalFits`) counts as a consumer **only** when `engineSink(ETraceLevel.Journal) != null` (Q14, resolved). With the journal off and no queries, no fit runs. Results never depend on which bars were queried (DV-3.7, DV-3.8, DV-T16b).
- **DV-0.2 Configuration.** `protected DvpConfig DvpConfig { get; } = new DvpConfig();` on `_CustomSignal`, set in a derived strategy's ctor or `Create()`. No `[Input]`. `Validate()` returns null or an error list. `Canon()` returns every output-affecting field and constant in a fixed order, and the journal's per-run `configHash` covers it (P3 §0.5).
- **DV-0.3 MC-free core.** The fit, manifold, rails, lattice math, confluence and acceptance primitives live in `_DvpEngine.Function.CS` between `// <dvp-core>` and `// </dvp-core>`. They use no MultiCharts types, so `tests/Dvp/gen.sh` extracts them with awk (the `tests/ProfileTests/gen.sh` pattern) together with `// <profile-math>`, `// <level-ref>` and `// <dvp-pa>` (`DvpPaBridge`, MC-free; rev 5). `_PriceActionEngine.Function.CS` is linked whole into tests/Dvp (not extracted). Host wiring (rings, ATR feed, rolls, pivots, journal) lives outside the region and in `_CustomSignal`.
- **DV-0.4 C# 7.3, no per-bar allocation.** No switch expressions, `??=`, ranges, LINQ or per-bar `new` on the fit path. Work arrays (`p_{k-1}`, `p_k`, residuals, rows) are allocated once per stream in `Configure()`.
- **DV-0.5 Causality.** A rail used **during bar t** comes only from bars `t−n … t−1` (closed) and an ATR known at `t−1`. An HTF stream contributes only after its bar has closed (`HtfClosedBar`, `_MtfMath.ClosedIndex`), and only once Probe P2 has passed (DV-9.3). Pivots count from their `confirmBar` / `confirmTime`, never from `extremeBar`.
- **DV-0.6 Journal.** Facts go to `engineSink(ETraceLevel.Journal)`, stamped with bar times (`yyyy-MM-dd HH:mm:ss`, chart TZ), never bar indexes. A `null` sink formats no string.
- **DV-0.7 Recalc idempotence.** Each snapshot is a pure function of (window bars, ATR at t−1, confirmed pivots, config). A recalculation reproduces it bit for bit.
- **DV-0.8 Reuse, not copy.** Value area uses `_ProfileMath.ValueAreaExpansion` and `PocIndex`. Rows are a fixed `double[manRows]` per stream, allocated in `Configure()` and cleared per refit. Residual rows are not tick keys, so `Accumulator` does not apply, and `Accumulator.Dense()` allocates two arrays per call (verified, `_ProfileMath` L312–322), so it is not used on the fit path. Entropy uses `_ProfileStats.Entropy`. Prominence uses a routine extracted from `Gate0Profile.Nodes` (DV-4.6). Rolls use `rollOn(ds)` / `_RollDetector` / `RingBar.rollBar` / `IsRollWindow`. Bars come from `requireRing`/`Ring`/`_BarRing`. HTF access uses M-MTF. Pivots come from `_PriceActionEngine`. ATR uses `Gate0Atr`. Levels are `LevelRef` with new `ELevelKind` values.

---

## 1. Fixed rulings (normative, from B5 rev 3.1; not configurable)

- **DV-1.1** Window `winLen = 240` closed bars by default (range 30..1000). No winLen sweep is run (240m dropped, Q4; DV-7.5).
- **DV-1.2** Degree `d ≤ 3`. It is chosen **once** by dev-window coverage match and then **frozen** (`degMode = DevCoverageFrozen`). Degree 1 is the sensitivity arm. BIC is computed and journaled, **report-only**.
- **DV-1.3** Confluence is a **weightless family count**. A qualifying cluster needs all **3** families {manifold, sigma, lattice}. Report-only weights are VA 1.00 and POC 0.77. The weights 1.00/0.80 (App F) and 1.0/0.7/0.8 (App E) are **retracted and never used**. No code path may read them (test DV-T18c).
- **DV-1.4** Acceptance: **N = 2** primary, sweep {2, 3, 5}, buffer **δ = 0.10 ATR**. For PS-25, N counts closes **after** the trigger: N = 2 means the trigger close plus 2 further closes beyond the rail (Q1 **DECIDED (Ettienne Hugo, 2026-10-23)**).
- **DV-1.5** The prediction cone is **diagnostic only**. It is never a `LevelRef`, never a confluence candidate and never a target. The band is **never faded** in a tradeable idea. KD-02 is a G0-b canary only.
- **DV-1.6** Far nodes (|z_node| ≥ 2) are used only **after acceptance** (KD-10).
- **DV-1.7** A fit window that crosses a roll is **skipped**. A stream whose skip share is above **30%** is **diagnostic-only** (DV-7.4). There is no back-adjustment.
- **DV-1.8** The ES prior is 0 (no prior edge is assumed for any DVP consumer).
- **DV-1.9** Holm family m = 5 {PS-01, PS-02, DV-02, A2-31, PS-25}, α = 0.05 (the consumers' study rule; the engine only journals what they need).
- **DV-1.10** Constants (in `Canon()`, not fields): `confMode = FamilyCount`, `minFamilies = 3`, `reportWeights = {VA 1.00, POC 0.77}`, `rowFloor = 1 tick`, `rollAdjust = None`, `minBars = 12`, `ATR_LEN = 20` (Wilder, `Gate0Atr`; Q7 resolved), `HOLD_BARS = 40` (MFE/MAE horizon).

---

## 2. Configuration (`DvpConfig`, catalogue §2.2 defaults)

| Field | Default | Valid | Meaning |
|---|---|---|---|
| `enabled` | false | | engine created only when true |
| `streams` | {1} | each ≥1, `requireDataStream` | one fit per stream (Data1 LTF; Data2/3 HTF, DV-9) |
| `src` | HL2 | HL2, Close | fitted series |
| `winLen` | 240 | 30..1000 | n |
| `degMode` | DevCoverageFrozen | DevCoverageFrozen, Fixed1, Calibrate | Fixed1 = sensitivity arm; Calibrate = run d=1..dCap side by side and journal coverage (DV-3.6) |
| `degMax` | 3 | 1..3 | |
| `weightVol` | false | | true = volume weights wᵢ = vᵢ; default wᵢ = 1 |
| `minR2` | 0.35 | 0..1 | **flag only** (`lowR2` in journal), never a filter |
| `manRows` | 64 | 8..256 | rows over ±manSpan σ |
| `manSpan` | 3 | 1..6 | in σ |
| `vaPct` | 70 | 50..95 | passed to `ValueAreaExpansion` as **0.70** (fraction, see DV-4.4) |
| `nodeProm` | 0.12 | 0.01..0.5 | prominence as a fraction of the tallest row |
| `maxNodes` | 3 | 0..10 | per side of POC, per kind |
| `sigmaList` | {1,2,3} | >0 | sigma rails at ±k |
| `projBars` | 60 | 1..500 | cone horizon (diagnostic) |
| `coneMode` | Prediction | Prediction, Confidence | √(1+h) vs √h |
| `coneMult` | 2 | >0 | |
| `useFib` | true | | lattice on/off; off ⇒ no qualifying confluence (all 3 families needed) |
| `fibRanges` | 3 | 1..4 | strengths fibPivot·2^(j−1) = 20/40/80; 4 allowed but **not swept** (Q10) |
| `fibPivot` | 20 | ≥2 | |
| `fibLookMult` | 8 | ≥1 | anchor lookback = fibLookMult × strength bars |
| `fibAnchor` | MostRecentPivot | MostRecentPivot | only MostRecentPivot is defined (DV-5.2). `ExtremeInLookback` is undefined; Validate rejects it (rev 5). |
| `fibMerge` | 0.40 | ATR | merge distance within a range |
| `fibNear` | 14 | ATR | keep levels within ±fibNear ATR of C_{t−1} |
| `confTol` | 0.30 | ATR | cluster tolerance |
| `splitFlow` | false | | up/down-tick manifolds (`RingBar.up/down`); diagnostic |
| `refitEvery` | 1 | ≥1 | refit cadence in closed bars (between refits the frozen fit is re-evaluated at the new u) |
| `journalFits` | true | | DVP FIT lines (DV-11.1) |
| `journalRails` | true | | DVP RAIL lines (DV-11.2) |

- **DV-2.1** `Validate()` rejects out-of-range values, duplicate streams, `winLen < minBars`, and `streams` entries that do not exist (`requireDataStream`). It throws `ArgumentException` in `StartCalc`, as `startExtraPa` does.
- **DV-2.2** The frozen degree `d*` per (symbol root, stream, winLen) is a **code constant table** `DvpFrozenDegree` in `// <dvp-core>`, which is part of `Canon()`. It is **not** a config field (the "no new fields" ruling). Until the table has an entry, `DevCoverageFrozen` falls back to `Calibrate` and journals `DVP DEGREE not frozen` once (Warning). Consumers must treat the stream as diagnostic-only until then (Q3 resolved: constant table, hashed in `Canon()`).

---

## 3. Fit (`DvpFit`, book App E `buildTrend` / `evalTrend` / `evalLeverage` / `trendSE`)

Notation: the window is bars i = 0..n−1, where i = n−1 is bar t−1. y_i = src(bar i) (HL2 = (H+L)/2). w_i = 1, or v_i if `weightVol`.

- **DV-3.1 Coordinates.** ctr = (n−1)/2, scl = (n−1)/2, u_i = (i − ctr)/scl ∈ [−1, 1]. Rails for bar t are evaluated at **u_t = 1 + 2/(n−1)** (one bar beyond the window).
- **DV-3.2 De-mean.** ȳ = Σw y / Σw; fit ỹ_i = y_i − ȳ. This gives shift invariance (test DV-T2).
- **DV-3.3 Forsythe recurrence.** p₋₁ = 0, p₀ = 1, p_{k+1}(u) = (u − A_k) p_k(u) − B_k p_{k−1}(u), where
  N_k = Σ w p_k², A_k = Σ w u p_k² / N_k, B_0 = 0, B_k = N_k / N_{k−1}, C_k = Σ w ỹ p_k / N_k, for k = 0..d.
  ŷ(u) = ȳ + Σ_k C_k p_k(u). The coefficients (ȳ, ctr, scl, A_k, B_k, N_k, C_k) fully define the fit and are journaled (DV-11.1).
- **DV-3.4 SSE and σ.** SSE_d = Σ w ỹ² − Σ_{k≤d} C_k² N_k. Assert SSE_d ≥ −ε·Σwỹ² (ε = 1e−12). If it is below that, or the recurrence breaks down (N_k ≤ 0), recompute SSE by a second pass over residuals and journal `DVP SSE-FALLBACK` (Debug). σ = √(SSE_d / (n − d − 1)), using Σw in place of n when `weightVol` (**VERIFY** against App E). R² = 1 − SSE_d / Σwỹ².
- **DV-3.5 Leverage, cone, BIC.** h(u) = Σ_{k≤d} p_k(u)² / N_k. The cone (diagnostic) is ŷ(u) ± coneMult·σ·√(1 + h(u)) for Prediction, or √h(u) for Confidence, for u up to 1 + 2·projBars/(n−1). BIC_d = n·ln(SSE_d/n) + (d+2)·ln n, computed for every d ≤ dCap (one pass gives all of them, since the recurrence is nested). It is journaled as `bic[]` and never used for selection.
- **DV-3.6 Degree.** dCap = min(degMax, max(1, ⌊n/8⌋)); if n < minBars there is no fit. `Fixed1` ⇒ d = 1. `DevCoverageFrozen` ⇒ d = min(d*, dCap). `Calibrate` ⇒ every d ≤ dCap is fitted and journaled, and the reported d is 1. **Coverage-match score** (used offline to set d*): over the dev walk-forward folds (catalogue §2.0.1, **VERIFY** fold boundaries) and windows sampled every `projBars` bars, score(d) = |cov_fwd(d) − cov_in(d)|. cov_in is the share of window bars with |y − ŷ| ≤ 2σ. cov_fwd is the share of the next `projBars` closed bars with |y_{n+h} − ŷ(u)| ≤ kσ√(1 + h(u)), using the book's forward band with k = 2 and H = projBars = 60 (B5 ch 2.2; the printed 28.2%/27.7% use this definition). Forward horizons that cross a roll splice are dropped from cov_fwd, and the dropped count is journaled in the `DVP RUN` line (rev 4). The flat-band variant (no leverage) is journaled next to it, report-only (Q2 resolved). The holdout (Jan 2025–Oct 2026) is never read. d* = argmin score, ties to the lower d, pooled over folds. The study that sets it is KD-08's diagnostic run (DV-PR8).
- **DV-3.7 Refit cadence.** With `refitEvery = r > 1`, the bars between refits re-evaluate the frozen coefficients at u = (i_t − ctr)/scl, measured from the last refit window. endpointDrift is reported only on refit bars. Refit bars are defined by bar index (`CurrentBar % refitEvery == 0` on the stream), never by when a query arrives. A query between refit bars recomputes the last scheduled refit from the ring if it is not cached.
- **DV-3.8 Repaint mitigation.** Every refit journals the as-of fit (DV-11.1) and `endpointDrift` = (ŷ_now(u of b) − ŷ_prev(u of b)) / ATR_{t−1}, where b is the **last bar of the previous fit's window** (the previous refit bar; with refitEvery = 1 that is bar t−1 relative to the new fit's last bar t, evaluated at u = 1 in the previous fit). It is NaN across a roll splice or when b has left the ring buffer. (Rev 4, DECIDED (Ettienne Hugo, 2026-10-24): matches the PR-D3 code and test TDriftR4; replaces the earlier "u_t of the previous fit" wording.) endpointDrift is reported only on refit bars. A **degree-1 shadow fit** (d = 1, same window) is always computed alongside, because RG-10 reads its slope and KD-08 its drift. Its marginal cost is zero, since it falls out of the nested recurrence. If the previous fit is not cached, `endpointDrift` recomputes it from the ring, so the value does not depend on journal or query history (test DV-T16b).
- **DV-3.9 Fit diagnostics** (journal-only): r2, Durbin–Watson dw on residuals, skew of residuals, sigmaAtr = σ / ATR_{t−1}, slopeEnd = dŷ/di at i = n−1 = (dŷ/du)·(2/(n−1)), and `lowR2` = r2 < minR2.

---

## 4. Manifold, rails and nodes (`buildManifold`, `buildNodes`)

- **DV-4.1 Rows.** The span is z ∈ [−manSpan, +manSpan] in σ units of the current fit. The nominal row width is w_z = 2·manSpan/manRows. **Tick floor:** if w_z·σ < tick, then manRowsEff = max(1, ⌊2·manSpan·σ / tick⌋) and w_z = 2·manSpan/manRowsEff; otherwise manRowsEff = manRows. manRowsEff is journaled. Row r covers [−manSpan + r·w_z, −manSpan + (r+1)·w_z), centre z_r = −manSpan + (r + ½)·w_z.
- **DV-4.2 Volume deposit.** For each window bar i: lo_z = (L_i − ŷ(u_i))/σ, hi_z = (H_i − ŷ(u_i))/σ, using the **current** fit (the whole window is re-mapped on every refit). The bar's volume v_i is spread over [lo_z, hi_z] in proportion to each row's overlap. The share outside ±manSpan goes to `tailVol`. A zero-range bar (hi_z = lo_z) deposits all of v_i into its row, or into tailVol if it falls outside. Rows are a fixed `double[manRows]` per stream, allocated in `Configure()` and cleared per refit (E5). `ValueAreaExpansion`, `PocIndex`, `Entropy` and `Prominence` are called on that array sliced to `manRowsEff`. Those APIs use `vols.Length`, so an exact-length array per `manRowsEff` value is kept in a small cache (allocated once per distinct value). §14.2-f still holds: `Accumulator.Add(..., mode 2)` is not used, because it spreads uniformly over tick-rounded keys, not by proportional overlap. With `splitFlow`, the same deposit is repeated with `RingBar.up` and `RingBar.down` into two more fixed arrays (diagnostic).
- **DV-4.3 Conservation.** Σ rows + tailVol = Σ v_i within 1e−9 relative (test DV-T9).
- **DV-4.4 POC and VA.** `_ProfileMath.ValueAreaExpansion(rows, vaPct / 100.0, out lo, out hi, out poc)` (the code multiplies the total by `percent`, so it takes a **fraction**; the catalogue's "70" is a percentage). VA = 70% of **in-span (manifold) volume**; tailVol is excluded, as in the book. The book's "70% value area grown outward from POC" matches `ValueAreaExpansion`'s two-row rule. POC ties use `PocIndex` (nearest to the volume-weighted mean row). pocZ = z_poc. vaHiZ = **outer** (upper) edge of row hi, vaLoZ = outer (lower) edge of row lo, consistent with the printed VA widths being whole multiples of the row height (Q9 resolved).
- **DV-4.5 Shape stats.**
  - `entropy` = `_ProfileStats.Entropy(rows, EEntropyRows.TradedRows, 0).Hnorm`: H normalised by ln R over **occupied** rows, as in the book (B5 §2.1). There is no Miller–Madow correction, because the inputs are volume shares, not counts. Verified on main (`_ProfileStats` L193–213): `mmN = 0` skips the correction (`if (mmN > 0)`), so `Hmm`/`HnormMm` stay NaN. `Hnorm = H / ln(n)` with n = occupied rows for `TradedRows`, and NaN when n ≤ 1. `EntropyResult` is a struct, so no allocation. Unit case: rows {1,1,0,1,1} → **Hnorm** = 1.0 (raw H = ln 4 ≈ 1.386).
  - `concentration` = rows[poc] / (Σrows / manRowsEff): the POC row over the mean row, printed as a multiple (book §1.3 / rev 3.1 §2.1). [GAP: the book does not print whether its mean runs over all span rows or only occupied rows. We use all `manRowsEff` rows (the "even spread" over the span) and journal the occupied-rows variant as `concentrationOcc`.]
  - `inSampleCov2` = cov_in at 2σ (DV-3.6).
- **DV-4.6 Prominence adapter.** PR-D0b extracts the peak/trough prominence loop of `Gate0Profile.Nodes` (`_Gate0.Function.CS`, the part after the KDE) into `_ProfileMath.Prominence(double[] d, double prom, List<double[]> hvn, List<double[]> lvn)` inside `// <profile-math>`. It returns `{index, prominence}` pairs, and `Gate0Profile.Nodes` calls it. The change is behaviour-preserving: `tests/Gate0Tests` and `tests/ProfileTests` output must be unchanged. DVP calls it on the **raw** rows with prom = nodeProm. An HVN is a local maximum with topographic prominence ≥ nodeProm × max row. An LVN is the minimum between two adjacent HVNs with (lower peak − trough)/max ≥ nodeProm. `_VpHistogram` is not reused: it is bound to MultiCharts `IProfile` (§14.2-g). Smoothing is **off**: the book applies prominence 0.12 to the raw manifold rows (Q5 resolved). `Prominence` fills caller-owned lists; the DVP caller reuses preallocated `double[2]` node slots (DV-13.2).
- **DV-4.7 Node selection.** The POC row is removed from the HVN list, along with any HVN within 1 row of it. Per side of POC, keep the `maxNodes` HVNs and the `maxNodes` LVNs with the highest prominence (ties go to the nearer row to POC). Each node records z_node, prominence and side.
- **DV-4.8 Rails.** For bar t, a rail of residual level z has price **P_t(z) = ŷ_{t−1}(u_t) + z·σ_{t−1}** (DV-3.1). Rail kinds and their z:

| ELevelKind | z | family |
|---|---|---|
| `DvpPoc` | pocZ | manifold |
| `DvpVah` / `DvpVal` | vaHiZ / vaLoZ | manifold |
| `DvpHvn` / `DvpLvn` | z_node | manifold |
| `DvpSigma` | ±k, k ∈ sigmaList | sigma |
| `DvpFib` | flat price (no z) | lattice |
| `DvpConf` | cluster price | (output of §6) |

  No z = 0 trend rail is published: the book's rail kinds are poc/vah/val/hvn/lvn/sigma (Q9 resolved).
- **DV-4.9 Residual order.** The rails of one snapshot, sorted by z (lattice rails by their z-equivalent (P − ŷ(u_t))/σ), define "next rail in residual order" for targets: `NextRail(stream, fromZ, dir)`.

---

## 5. Lattice (`lattice`, Fib lattice builder)

- **DV-5.1 Pivots.** For each range j = 1..fibRanges, strength s_j = fibPivot·2^(j−1) (20, 40, 80). The pivots come from a `_PriceActionEngine` with `PriceActionConfig { enabled = true, dataStream = ds, layers = 1, strength = s_j, pivotMode = Fractal, minBreakoutLayer = 0, role = "DVP-S{s_j}" }` and the futures preset applied per field (`presetExtraPriceAction`). minBreakoutLayer = 0 is required because `PriceActionConfig.Validate` (L132) requires 0..layers−1, so layers = 1 fails with the default of 1; DVP never reads breakouts, so pivots are unaffected. Because `Canon()` includes minBreakoutLayer, DVP shares an ExtraPriceAction instance only when that host also uses 0; otherwise it gets its own instance (test T17SeparateInstance) (rev 5). Instances are found or shared by (dataStream, `Canon()`), as `findSharedPa` does. They are hosted by the DVP host code and stepped on their stream's closed bars, with `OnRoll` on `rollOn(ds)`: the same pattern as M-XMKT's hosted instances (`_CustomSignal` ~L1044 (PR #37 head)). The PriceAction library is not changed. Host order rules out ExtraPriceAction (`startExtraPa` runs before `startDvp`); XMKT `findSharedPa` pattern used (`findSharedPa(c, false)`, owned instances stepped in `stepDvp` unless hosted; rev 5, PR-D4). Book App E goes up to 4 ranges (20/40/80/160); fibRanges = 3 is the spec default, and 4 is allowed but not swept (Q10 resolved, §14.2-d).
- **DV-5.2 Anchor (MostRecentPivot).** For range j at bar t: B = the most recently **confirmed** pivot (confirmBar ≤ t−1) of either side, and A = the most recent confirmed pivot of the opposite side whose extremeBar is before B's. Both extremeBars must be within fibLookMult·s_j bars of t−1; if they are not, the range is empty. Range = |B − A|. Ties in B's confirmBar go to the later extremeBar, then to the high. A is the opposite-side confirmed pivot with the latest extremeBar before B's. The lookback is applied after selection and is inclusive (within ≤ fibLookMult·s_j bars); there is no fallback to an older pair, so an anchor outside it gives an empty range (rev 5).
- **DV-5.3 Levels.** `FIB_LEVELS = {0, .22, .5, .62, .71, .786, .886, 1, 1.414, 1.618, 2.618, 3.618, 4.236, −.146, −.272, −.414, −.764, −1, −2, −2.5, −2.62, −3, −3.5, −4}` (24 values, verbatim from rev 3.1 §2.2 and catalogue §2.4b; do **not** "correct" .22/.62/.71/−2.62 to textbook ratios). The level price is L(f) = B − f·(B − A): f = 0 is B, f = 1 is A, f > 1 extends past A, and f < 0 extends past B. [GAP: App E code unread; convention pre-registered, not book-verified. Q6 **DECIDED (Ettienne Hugo, 2026-10-23)**: pre-registered now; reading App E before PR-D4 is optional, and any change after that read is a new trial.]
- **DV-5.4 Merge and filter.** Within a range, greedily group levels in ascending price: a level joins the current group while it is strictly closer (<) than fibMerge·ATR_{t−1} to the group's first (lowest) member (no chaining) (rev 5). The merged price is the price of the member with the smallest |f − round(f)|, ties to the lower f. Drop levels further than fibNear·ATR_{t−1} from C_{t−1} (applied to each merged level's price after merging; |P − C_{t−1}| ≤ fibNear·ATR_{t−1} is kept, inclusive; rev 5). Levels from different ranges are never merged (each keeps its range id for nRanges).
- **DV-5.5 Flatness.** Lattice levels are flat prices. They are not re-evaluated on the curve.

---

## 6. Confluence scorer

- **DV-6.1 Candidates.** All manifold rails (POC, VAH, VAL, HVN, LVN), all sigma rails, and all lattice levels of the snapshot, at their bar-t prices. The cone is excluded.
- **DV-6.2 Deterministic greedy clustering.** "Heaviest unclaimed" is not defined under a weightless count, so it is replaced by this deterministic, weightless order (Q11 resolved). For every unclaimed candidate c, let F(c) = the number of distinct families among unclaimed candidates within confTol·ATR_{t−1} of c, and K(c) = their count. The seed is the candidate with max F, then max K, then min |P − C_{t−1}|, then lowest price. The cluster is every unclaimed candidate within confTol·ATR of the seed. Mark them claimed and repeat until none are left. The cluster price is the seed price.
- **DV-6.3 Count.** `families` = the number of distinct families in the cluster (0..3). `nRanges` = the number of distinct lattice range ids. A cluster qualifies when families ≥ minFamilies = 3, which needs the lattice with ≥ 1 range. KD-09's "≥ 2 ranges" is applied **inside the KD-09 module**, never in the engine. Qualifying clusters are published as `DvpConf` LevelRefs with quality = families.
  - **Rev 5.1 (S4):** Every rail occupies a candidate slot in publish order (a rail without a price is a non-candidate slot), so `quality` and journal families index rails 1:1.
- **DV-6.4 Point query.** `FamiliesAt(stream, price, tolAtr = confTol)` counts the distinct families and ranges among candidates within tolAtr·ATR of a given price, without clustering. RG-09/KD-09 use it on the host's event rail.
- **DV-6.5 Score.** `score` = `families` (0..3), the weightless family count of rev 3.1 §5.1. It is stored in `LevelRef.quality` for rails and confluence levels. A report-only sensitivity column `score077` is journaled next to it: the within-family maximum weight under VA edge 1.00 / POC 0.77 / every other kind 1.00, plus 1 for a sigma rail, plus 1 if nRanges ≥ 1. Neither value ever gates, ranks, sizes or selects. The published 1.00/0.80 weights are never computed. (Q12 moot.)

---

## 7. Rolls, sessions and streams

- **DV-7.0 Roll detection (rev 5, instrument-profile amendment, M-INSTR IP-9.4).** **Waits on the DVP host PR (#36: `DvpConfig` host property, `startDvp`).** `rollOn()` returns false when `RollDetection == Off` (verified, `_CustomSignal` `rollOn`), and `IsRollWindow` is false when no ring exists. If `profile.rollMode == Detect` (futures) and `RollDetection == Off`, `Validate()`/`startDvp` logs one `ETraceLevel.Warning` "DVP: RollDetection Off — roll skip disabled" and **marks every stream diagnostic-only** (`DVP RUN` reports `rollDetect=Off`, skipShare = NaN); such a run cannot feed Holm. **ES with RollDetection Off is therefore unchanged from rev 4.** If `profile.rollMode == None` (stocks, ETFs, spot FX, crypto; no contract rolls by nature), no window can cross a roll: `crossesRoll` is always 0, **skipShare = 0** (not NaN), `diagnosticOnly` is decided by the other criteria only, `DVP RUN` reports `rollDetect=None`, and **the stream stays tradeable**. For ds ≥ 2, `rollOn(ds)` is valid only for the same root as Data1 (via `m_rollPending`). **A different root on Data2/3 is rejected in `Validate()`** (kept from rev 4). A Future Data2/3 under a non-future Data1 is rejected in `Validate()` (no detector for it). Under rollMode None, the RollDetection setting must be Off (a non-future with RollDetection on throws, DV-T27c), and `DVP RUN` then reports `rollDetect=None`, not Off.
  Consequential edits: DV-7.4 skipShare is 0 when rollMode None; DV-11.3 `rollDetect` ∈ {Series, Table, SeriesOrTable, Off, None}; DV-T27 unchanged; new DV-T27b (rollMode None: skipShare 0, not diagnostic-only, tradeable) and DV-T27c (non-future with RollDetection on throws, M-INSTR reason 13).
- **DV-7.1 crossesRoll.** A window crosses a roll if any bar i ∈ [1, n−1] of it has `RingBar.rollBar` (the first bar of a new contract). A window that starts on the roll bar is clean. On a crossing, the snapshot is **skipped**: no rails, `Rails()` returns empty, and the journal line has `crossesRoll=1`. Fits resume once n post-roll bars have accrued. `crossesRoll` and skip counting run on **every** closed bar from `RingBar.rollBar` (O(1) per bar, no fit), so skipShare is the same whether or not anyone queries (E10).
- **DV-7.2 Roll window.** Rail interactions whose trading day satisfies `IsRollWindow(day)` (roll session ±1) are journaled with `isRollWindow=1`, which is also set on the LevelRef. Consumers filter on it. (`IsRollWindow` is live only when a Phase-3 helper is enabled; DVP's `requireRing` call enables it.)
- **DV-7.3 ATR.** One `Gate0Atr(ATR_LEN)` per stream, fed on closed bars. `ResetPrev()` is called on `rollOn(ds)`. Snapshots use the value as of t−1.
- **DV-7.4 Skip share.** Per stream, skipShare = skipped / (eligible windows after warm-up), journaled in the end-of-run `DVP RUN` line. skipShare is 0 when the profile's rollMode is None (DV-7.0). **A stream with skipShare > 0.30 is diagnostic-only.** This is a run-level verdict used in analysis and reports. It cannot gate live bars, because it is only known at the end of the run. Arithmetic estimate only, not measured: if each roll invalidates n − 1 windows and a quarter has about 63 sessions, the expected shares are ≈ 4% (15m), ≈ 17% (60m), ≈ 63% (240m, about 6 bars/session, winLen 240) and ≈ 32% (240m, winLen 120). Measured values come from `DVP RUN` (DV-R3). Q4 **DECIDED (Ettienne Hugo, 2026-10-23)**: 240m is dropped from MT-07 and is diagnostic only.
- **DV-7.5 240m stream.** No winLen sweep (rev 4: the former {240, 120} sweep is removed). The 240m stream is **diagnostic only**; MT-07 trades 60m only. No winLen 90 trial (Q4 **DECIDED (Ettienne Hugo, 2026-10-23)**).
- **DV-7.6 ETH/RTH.** ETH is primary (all bars). RTH is the sensitivity arm, a second engine config built only from bars with `RingBar.rth`. `rthShare` = RTH volume / window volume is journaled and split into {< 50%, ≥ 50%}.
- **DV-7.7 Sessions, 24h charts and half-days.** Session boundaries are `tradingDayKey(t)` changes on the stream's own bars, never calendar dates. On the futures base this is the Globex date (verified: `_VolumeProfileFuturesBase` L356 overrides `tradingDayKey` with `GlobexDate`), so a 24h chart's session starts at 18:00 ET. The RTH arm (DV-7.6) requires the futures base. On the generic base `isRthBar` always returns true (verified, `_CustomSignal` L516; futures override L512), so `rthShare` is journaled as NaN and the RTH arm is refused with a Warning. Half-day sessions (`todHalfDay`; base false at L546, futures override L491) are **not** removed from fit windows, because bars are bars. They are flagged `halfDay=1` on `DVP RAIL` and on A2-33 samples. The A2-33 once-per-session sample (Q8) is taken on the first closed bar of the new `tradingDayKey`, including half-days.

---

## 8. Acceptance primitives

- **DV-8.1 Rail history.** Per stream, a ring of the last 8 snapshots' rail prices, so every bar is compared with **its own** t−1 rail. Trigger plus N further closes with N = 5 (sweep max) needs 6 snapshots and fits in 8 (t−1 … t−7). Assert N ≤ 6.
  - **Rev 5.1 (S1, matching):** A rail is matched across snapshots by kind for POC/VAH/VAL, by z within 1e−6 for σ rails, and for HVN/LVN by the same-kind node nearest in z **provided |Δz| ≤ 0.25**. Otherwise the rail is absent in that snapshot (NaN), which breaks any run.
  - **Rev 5.1 (S2, depth):** The ATR(t−1) history depth is max(refitEvery + 4, 10), so every rail-history snapshot (endAgo ≤ 8) carries its own ATR whatever the query order. Snapshots older than endAgo 9 are not part of the history contract.
- **DV-8.2 Trigger.** An up-trigger on rail R at bar t: C_t > P_t(R) + δ·ATR_{t−1} **and** C_{t−1} ≤ P_{t−1}(R) (no δ on t−1). A down-trigger mirrors it. δ = 0.10.
- **DV-8.3 Acceptance primitives.** The engine exposes:
  - `RunBeyond(R, dir, deltaAtr)`: the number of consecutive closed bars, ending at t−1, each closing beyond its own rail + deltaAtr·ATR.
  - `BackInside(R, dir)`: true if any close has come back inside by more than δ since the trigger.
  - `Accepted(R, dir, N, countTrigger, deltaAtr)`: a thin wrapper over the two. **countTrigger = true:** the accepted run is the trigger close **plus N further closes**, each beyond its own rail + deltaAtr·ATR (N + 1 bars, trigger first). **countTrigger = false:** the run is N closes in total with the trigger as the first (trigger + N − 1 further closes).

  **The engine has no default convention.** Each consumer passes its own catalogue text. PS-25 → `Accepted(R, dir, N = 2, countTrigger = true, deltaAtr = 0)` plus `!BackInside`: the trigger close plus 2 further closes beyond the rail, entry above the 2nd further close (Q1 **DECIDED (Ettienne Hugo, 2026-10-23)**). KD-06/KD-10 → countTrigger = false, N = 2, deltaAtr = δ on the confirmation bar ("C_t and C_{t+1} make N = 2", i.e. trigger + 1).
- **DV-8.4** `closedThrough` (C beyond the rail with no δ), `heldBars` (RunBeyond with deltaAtr = 0), `distAtr` = (C − P)/ATR, and `verdict41` (held vs accepted on a symmetric 41-bar clock, PS-25 test 1) are reported for every interaction, next to MFE40/MAE40 in ATR (engine-side).
  - **Rev 5.1 (S3, verdict41 v0, [GAP]):** verdict41 is provisional, journal-only and never gates. For **trigger** events only, on the clock of the event bar + 40 bars: `accepted` if Accepted(N = 2, countTrigger = true, deltaAtr = 0) completes **on the event's own trigger** (2 bars after the event) before any close back inside its own rail by > 0.10 ATR; `failed` if such a close comes first; `held` otherwise. Accept events journal verdict41 = NaN. Journal field name: `verdict41v0`.

---

## 9. HTF streams (MT-07, Probe P2)

- **DV-9.1** A stream ds > 1 in `streams` gets its own ring (`requireRing(ds, winLen + 8, "DVP")`), ATR, pivots and fit. It steps once per closed HTF bar (`BarsOfData(ds).Status == EBarState.Close`, `ringBarOf(ds)`).
- **DV-9.2** A Data1 bar reads the HTF snapshot of the last closed HTF bar via `_MtfMath.ClosedIndex` / `HtfClosedIndex(ds)`. Forming HTF bars are never used.
- **DV-9.3** **Probe P2 gate.** The constant `DvpConst.P2_PASSED = false` (in `Canon()`). While it is false, HTF snapshots are computed and journaled but `Tradeable(ds)` returns false for ds > 1, and a Warning `DVP Data{ds} used before Probe P2` is logged once. It is flipped in a one-line PR once `_MtfProbe` P2 passes. **VERIFY**: the P2 status (the catalogue says "not yet run").
  - **Rev 5.2:** `Tradeable(ds)` never consults `diagnosticOnly`; that is a run-level verdict reported in DVP RUN only (DV-7.0, DV-7.4).
- **DV-9.4 HTF acceptance.** `HtfAccepted(ds, R, dir, N ≥ 2)` applies DV-8.3 on HTF closed bars. `HtfNextRailDistAtr(ds, dir)` is the distance from C_{t−1} (Data1) to the next HTF rail in dir, in Data-ds ATR.
  - **Rev 5.2:** The next rail is strictly beyond C(t−1) in dir (a rail exactly at the price is skipped). ATR is the Data-ds ATR from its t−1 snapshot. The result is NaN when dir = 0, there is no rail, or ATR ≤ 0 or NaN. `HtfAccepted` takes `countTrigger` and `deltaAtr` explicitly (no defaults, N ≥ 2).

---

## 10. API (internal, on `_CustomSignal` / `_DvpEngine`)

```
internal _DvpEngine Dvp { get; }                        // null + one Warning when disabled
DvpSnapshot Snapshot(int ds)                            // as-of t−1 fit for bar t; null if no fit / skipped
  .deg .n .sigma .sigmaAtr .r2 .yhatEnd .slopeEnd .slope1SigmaPerBar .endpointDrift
  .pocZ .vaLoZ .vaHiZ .entropy .concentration .tailVol .crossesRoll .lowR2
double RailPrice(int ds, ELevelKind kind, double z)     // P_t(z); NaN if absent
IReadOnlyList<LevelRef> Rails(int ds)                   // Poc/Vah/Val/Hvn/Lvn/Sigma/Fib, tag DV-10.2
IReadOnlyList<LevelRef> Confluence(int ds)              // DvpConf clusters, families==3
void FamiliesAt(int ds, double price, double tolAtr, out int families, out int nRanges)
LevelRef NextRail(int ds, double fromZ, int dir)        // residual order (DV-4.9)
double Z(int ds, double price)                          // (price − ŷ(u_t))/σ
bool Triggered(int ds, LevelRef r, int dir); int RunBeyond(int ds, LevelRef r, int dir, double deltaAtr)
bool BackInside(int ds, LevelRef r, int dir); bool Accepted(int ds, LevelRef r, int dir, int N, bool countTrigger, double deltaAtr)
bool HtfAccepted(int ds, LevelRef r, int dir, int N, bool countTrigger, double deltaAtr); // N ≥ 2, no defaults (rev 5.2) double HtfNextRailDistAtr(int ds, int dir); bool Tradeable(int ds)
void Cone(int ds, int h, out double lo, out double hi)  // [Diagnostic] never a level
```

- **DV-10.1** Each `LevelRef` uses `LevelRef.Line(kind, price, created = time of bar t−1 close, t0 = window first bar time, t1 = bar t−1 time, stream, tag, quality = families)`, with `crossesRoll` and `isRollWindow` set.
- **DV-10.2 Tag.** `"DVP{ds}:{t−1:yyyyMMddHHmm}:d{deg}:{kind}{z:+0.00}"` (lattice: `{kind}{f:+0.000}R{j}`).
- **DV-10.3 New ELevelKind values**, **appended** after `BootstrapPocZoneHi` so existing ordinals do not change: `DvpPoc, DvpVah, DvpVal, DvpHvn, DvpLvn, DvpSigma, DvpFib, DvpConf`. No switch over `ELevelKind` exists on main (checked), so appending is safe. `LevelRef.StudyType` (= kind.ToString()) gives the A2-33 type strings automatically.

### 10.4 What each consumer reads

| Idea | Reads | Rule owned by the consumer (not the engine) |
|---|---|---|
| **PS-25** | `Rails(1)` (VAH/POC/HVN/LVN/σ), `Triggered`, `Accepted(N∈{2,3,5}, countTrigger = true)`, `Snapshot.sigmaAtr`, `NextRail` | entry on acceptance = trigger close + N further closes (countTrigger = true, deltaAtr = 0, `!BackInside`; Q1 **DECIDED (Ettienne Hugo, 2026-10-23)**); sigmaAtr ≤ 4; target = next rail; 18-cell sweep + 6-cell σ2/node block. **Primary cell CONFIRMED, DECIDED (Ettienne Hugo, 2026-10-23):** N = 2 (trigger + 2 further closes), event rail VAH/VAL, EX-05 trail + 41-bar time stop (catalogue PS-25, rev 3.1 ruling 6.1 item 2). It is frozen before any dev read; the other 17 cells are sensitivity cells. |
| **MT-07** | `HtfAccepted(ds = the 60m stream, N≥2)`, `HtfNextRailDistAtr`, `Tradeable(ds)` | HTF state as a filter for LTF entries. HTF = **60m only**; 240m is diagnostic, never traded (Q4 **DECIDED (Ettienne Hugo, 2026-10-23)**) |
| **RG-09 / KD-09** | `FamiliesAt(rail, 0.3)` after `Accepted` | ≥ 2 ranges in-module; disagreement veto 0.3–1.0 ATR **[GAP]**; arm B families ≤ 1 |
| **RG-10** | `Snapshot.slope1SigmaPerBar` (degree-1 shadow slope / σ₁), `Z(close)` history | gate if slope > 0.05 σ/bar; stand down winLen/4 bars after \|z\|>2 against the gate for ≥ 3 bars (off for KD-06) |
| **KD-02** (canary) | `RailPrice(Sigma, +2)`, `RailPrice(Poc)`, `RailPrice(Sigma, +3)` | close > ŷ+2σ fade, target POC, stop 3σ + 0.1 ATR, time stops 19/40; **kill if net PF > 1.0**; `EGate0Canary.KD02` only |
| **KD-06** | `Accepted(Sigma ±2, N=2, countTrigger = false, deltaAtr = δ)` (trigger + 1) | EX-05 trail + 41-bar time stop |
| **KD-10** | node rails with \|z_node\| ≥ 2 after `Accepted(N = 2, countTrigger = false, deltaAtr = δ)` | stand-in for far nodes |
| **A2-33** | `Rails`/`Confluence` via the hook DV-12 | G0-d types DvpPoc/Vah/Val/Hvn/Lvn/Sigma/Conf |

---

## 11. Journal and trace

- **DV-11.1 `DVP FIT`** (Journal, one per stream per refit when `journalFits`): `runId, configHash, stream, barTime, n, deg, bic[1..dCap], manRowsEff, rthShare, crossesRoll, sigma, sigmaAtr, r2, dw, skew, yhatEnd, slopeEnd, slope1, endpointDrift, pocZ, vaLoZ, vaHiZ, entropy, concentration, concentrationOcc, tailVol, inSampleCov2, lowR2, coef{ȳ;ctr;scl;A0..Ad;B1..Bd;N0..Nd;C0..Cd}` (invariant culture, R round-trip format for coefficients). A skipped window writes `crossesRoll=1` and NaNs.
- **DV-11.2 `DVP RAIL`** (Journal, when `journalRails`, on every trigger and acceptance): `railKind, z, price, families, nRanges, score, score077, distAtr, closedThrough, heldBars, accepted(N=2,3,5), verdict41, isRollWindow, halfDay`. Forward `MFE40 / MAE40` in ATR come from an engine-side 40-bar tracker. The interaction is also passed to `logSignal(tag, dir, price, stop = rail ∓ δ·ATR, context, taken: false)` with context keys `railKind, z, families, nRanges, score, deg, sigmaAtr`. (`logSignal` measures in R at `SignalHorizons` {5,15,30,60}. The DVP study strategy sets `SignalHorizons = {5,15,30,40,60}`, a strategy property, not a `DvpConfig` field; Q15 resolved.)
- **DV-11.3 `DVP CREATED`** (once): streams, Canon(), d per stream, hosted PA labels, P2 state. **`DVP RUN`** (end of run): per stream: fits, skips, skipShare (NaN if rollMode Detect with RollDetection Off; 0 if rollMode None), `rollDetect` ∈ {Series, Table, SeriesOrTable, Off, None}, `diagnosticOnly` (skipShare > 0.30, or rollMode Detect with RollDetection Off), mean cov_in, mean cov_fwd, fallback count.
- **DV-11.4** Per-bar rows and lattice detail go to `Debug`.

---

## 12. A2-33 harness hook

- **DV-12.1** With `LevelStudyConfig.enabled` and DVP enabled, the host calls `levelStudyAdd(r.StudyType, r.price, ageSessions: 1, quality: r.quality, key: r.tag)` for every `Rails(1)` and `Confluence(1)` level on the **first closed bar of each new `tradingDayKey`** (half-days included, flagged `halfDay`), using that bar's t−1 snapshot (the price is frozen at creation, the KD-11 convention). `_LevelStudy` assumes static levels aged in sessions, so a moving rail is sampled once per session (Q8 resolved; this avoids serial overlap). Types: DvpPoc, DvpVah, DvpVal, DvpHvn, DvpLvn, DvpSigma, DvpConf (DvpFib is reported but is not a G0-d type).
  - **Rev 5.3 (S1):** `halfDay` is written on the per-session `DVP A2-33` Journal line, which joins the LevelStudy CSV by day (`tradingDayKey`, yyyy-MM-dd, one line per session), not by key; it is visible only at Journal trace level.
  - **Rev 5.3 (S2):** Each level is registered after that bar's LevelStudy `Bar()`, so it is first evaluated on the next bar (never on its creation bar); the reference price is the creation-bar close.
- **DV-12.2** Levels with `crossesRoll` or `isRollWindow` are not added (A2-39c filter).

---

## 13. Performance budget

- **DV-13.1** Disabled: one null check per `CalcBar`, no allocation, no rings, no PA instances (test DV-T22).
- **DV-13.2** Per refit per stream (n = 240, d ≤ 3, 64 rows, 3 ranges, ≈ 100 candidates): recurrence O(n·d), manifold O(n·rows spanned), VA/nodes O(rows), confluence O(K²). Target **median ≤ 0.2 ms, p99 ≤ 1 ms** on the dev box, measured by `tests/Dvp` bench DV-T26 over 100k refits. **Zero allocations** per refit after warm-up (fixed row arrays and node slots, E5) (`GC.GetAllocatedBytesForCurrentThread` delta = 0).
- **DV-13.3** Memory per stream: ring winLen + 8 bars, 8 rail snapshots, work arrays ≈ 16 KB. Hosted PA engines are the main cost (3 per stream). They are created only with `useFib`, and shared when their configs are equal.
- **DV-13.4** End to end, a 10-year @ES 15m backtest with DVP on Data1 and journal off may slow down by at most 15% versus DVP off (smoke test DV-R3, **PROBE**).

---

## 14. Open questions and conflicts

### 14.1 Decisions

**Decided by Ettienne Hugo, 2026-10-23:**

- **Q1 PS-25 acceptance counting. **DECIDED (Ettienne Hugo, 2026-10-23)**.** "The trigger bar plus 2 more closes": acceptance = the trigger close plus 2 further closes beyond the rail; PS-25 passes `countTrigger = true, N = 2` (DV-8.3). KD-06/KD-10 keep their own text (trigger + 1, `countTrigger = false`).
- **PS-25 primary cell. CONFIRMED, DECIDED (Ettienne Hugo, 2026-10-23).** N = 2, event rail VAH/VAL, EX-05 trail + 41-bar time stop (§10.4).
- **Q4 240m stream. **DECIDED (Ettienne Hugo, 2026-10-23)**.** Drop 240m: MT-07 runs on 60m only; 240m is journaled as diagnostic; no winLen 90 trial (DV-7.4, DV-7.5).
- **Q6 Fib direction. **DECIDED (Ettienne Hugo, 2026-10-23)**.** Pre-register L(f) = B − f(B − A) now (DV-5.3); reading App E before PR-D4 is optional.

Nothing is OPEN. Remaining uncertainty is limited to the VERIFY and [GAP] items in the text.

**Resolved (Analyst 1 recommendation adopted, rev 2):**

| Q | Decision | Where |
|---|---|---|
| Q2 | Forward coverage uses the book's √(1+h) band, k = 2, H = 60; flat band report-only | DV-3.6 |
| Q3 | Frozen d* in a code constant table hashed in `Canon()` | DV-2.2 |
| Q5 | Prominence on raw rows, no smoothing | DV-4.6 |
| Q7 | `Gate0Atr(20)`, Wilder | DV-1.10, DV-7.3 |
| Q8 | A2-33 sample once per session, keyed by `tradingDayKey` | DV-7.7, DV-12.1 |
| Q9 | Outer VA edges; no z = 0 rail; concentration = POC / mean row; `PocIndex` ties | DV-4.4, DV-4.5, DV-4.8 |
| Q10 | fibRanges = 3; 4 allowed, not swept | DV-5.1 |
| Q11 | Seed: most families, then most candidates, then nearest to C_{t−1}, then lowest price | DV-6.2 |
| Q12 | Moot: score is the family count; `score077` uses node 1.00 | DV-6.5 |
| Q13 | RG-10 slope = degree-1 shadow slope over σ₁ | §10.4 |
| Q14 | Journal counts as a consumer only when `engineSink(Journal) != null`; results path-independent | DV-0.1, DV-3.7/3.8 |
| Q15 | Horizon 40 in the study strategy's `SignalHorizons`, plus engine MFE40/MAE40 in ATR and `verdict41` | DV-8.4, DV-11.2 |

### 14.2 Conflicts found among the sources (resolution applied)

- a) **Analyst 2 review "Agree 5"** endorses weights 1.0/0.7/0.8. These are superseded by rev 3/3.1 (weightless count) and never used.
- b) **Analyst 2 P6/P7 entries** propose back-adjusted rolls, 5 new config fields and a KD-07 kill at PF > 1.1. Rev 3.1 overrules them: skip windows, no new fields, kill at PF > 1.0.
- c) **Analyst 1 P6/P7 review §8.8** says "N = 3 stays frozen". This is superseded by the N = 2 sign-off.
- d) **Book lattice of 4 ranges** vs fibRanges = 3. The spec keeps 3 and leaves 4 configurable (Q10).
- e) **Catalogue "ValueAreaExpansion(vols, 70, …)"**: the code expects the fraction 0.70 (DV-4.4).
- f) **Catalogue "Accumulator.Add (mode 2) on residual rows"**: mode 2 is uniform over tick keys, not proportional overlap. The spec uses `AddRow` (DV-4.2).
- g) **Catalogue "_VpHistogram node logic"**: that class is `IProfile`-bound. The reusable prominence code is in `Gate0Profile.Nodes` (KDE-based), and it is extracted in PR-D0b.
- h) **Catalogue "_RangeProfile ring pattern"**: the Phase-3 `requireRing`/`_BarRing` is the current mechanism and is the one used.
- i) **PS-25 vs KD common acceptance wording**: resolved by Q1 (DECIDED (Ettienne Hugo, 2026-10-23)); PS-25 = trigger + N further (countTrigger = true), KD-06/KD-10 = N including the trigger (countTrigger = false).
- j) **240m sparsity** makes the 30% rule bite: resolved by Q4 (DECIDED (Ettienne Hugo, 2026-10-23)), 240m diagnostic only.
- k) **Line numbers** cited in the Analyst 1 review (`IsRollWindow` ~L1100, VPF-4 ~L1391–1417) still match main (`IsRollWindow` L1100, `_RollDetector` L1398). **Confirmed.** The catalogue's `HtfClosed/HtfClosedBar` exist (L1330–1334). **Confirmed.** (The `PriceActionConfig`/`m_pa` block is at L383–397, not L379–397.)
- l) **Rev 1's report score vs rev 3.1** (E1): replaced by the family count.
- m) **Rev 1's flat-band forward coverage vs the book's leveraged definition** (E6): the leveraged definition is adopted.
- n) **KD-06 vs PS-25 δ placement** (E4): δ goes on the trigger only (PS-25) or on the confirmation bar (KD-06/KD-10), passed per consumer.

---

## 15. Tests (`tests/Dvp`, ProfileTests style)

`gen.sh` extracts `// <dvp-core>`, `// <profile-math>`, `// <level-ref>` and `// <dvp-pa>` (`DvpPaBridge`). `_PriceActionEngine.Function.CS` (with `PriceActionTests/McShim.cs`) is linked whole in the tests/Dvp csproj (rev 5). `Program.cs` uses `Check(bool, string)` with pass/fail counts. `run.sh` runs `dotnet run -c Release` with LangVersion 7.3. `reference.py` (numpy `lstsq` / QR) generates the fixture CSVs under `tests/Dvp/fixtures/`, following the RegimeTests reference pattern.

- **DV-T1 Recurrence vs reference.** 50 random series (n ∈ {12, 30, 240, 1000}, d = 0..3, w = 1 and w = v): ŷ, σ, SSE and h match `reference.py` to 1e−9 relative.
- **DV-T2 Shift/scale invariance.** y + 10⁶ gives the same residuals and σ to 1e−9; y × 4 scales σ by 4.
- **DV-T3** SSE is non-increasing in d. SSE ≥ 0. Forcing N_k ≤ 0 triggers the fallback path and journals it.
- **DV-T4 Exact recovery.** A cubic y with no noise gives SSE ≈ 0 and exact ŷ(u_t). A linear y with d = 3 gives C₂ = C₃ ≈ 0.
- **DV-T5 Coverage drill.** Trend plus Gaussian noise, 2,000 windows: mean cov_in(2σ) ∈ [0.94, 0.965].
- **DV-T6 Leverage.** Σ_i h(u_i) = d + 1 (w = 1). h is minimal near u = 0 and h(u_t) > h(1).
- **DV-T7 BIC** values equal the formula. BIC never changes the chosen d.
- **DV-T8 Degree.** dCap for n = 12, 16, 24, 240. Fixed1. DevCoverageFrozen with and without a table entry (falls back to Calibrate and logs one Warning). n < 12 gives no fit.
- **DV-T9a Shape stats.** Entropy rows {1,1,0,1,1} → Hnorm = 1.0 with `mmN = 0` (HnormMm NaN). Concentration on a known histogram equals rows[poc]/(Σ/manRowsEff); `concentrationOcc` is journaled.
- **DV-T9 Manifold deposit.** A bar spanning 2.5 rows splits 0.2/0.4/0.4 as its overlap dictates. Zero range goes to one row. An outside bar goes to tailVol. Conservation holds (DV-4.3).
- **DV-T10 Tick floor.** σ = 0.5 tick gives manRowsEff = max(1, ⌊6·0.5⌋) = 3, and it is journaled.
- **DV-T11 VA.** Equals `ValueAreaExpansion(rows, 0.70)`. Passing 70 is rejected by Validate/asserted. POC tie rule.
- **DV-T12 Prominence.** (a) `tests/Gate0Tests` and `tests/ProfileTests` outputs are bit-identical before and after PR-D0b. (b) Synthetic bimodal rows give 2 HVN and 1 LVN; at prominence 0.11 vs 0.13 the threshold is respected. (c) maxNodes per side and POC exclusion.
- **DV-T13 Rails.** P_t(z) uses the t−1 fit at u_t = 1 + 2/(n−1). Rail kinds and tags match DV-10.2.
- **DV-T14 Truncation audit.** Snapshots at bar t computed on a series truncated at t−1 are bit-identical to the full-series run, for 500 random t.
- **DV-T15 Shuffled future.** Permuting bars > t−1 leaves every bar-t output unchanged.
- **DV-T16 Rolls.** A synthetic splice: windows containing it are skipped. A window starting on the roll bar is clean. Fits resume after n post-roll bars. skipShare and `diagnosticOnly` at 0.30 ± one window. ATR `ResetPrev`. `rollOn(1)` sets `rollBar` only on the closed first bar of the new contract.
- **DV-T16b Path independence.** Journal-on and journal-off runs (and runs with random query patterns) give identical snapshots, endpointDrift and skipShare on 100 random bars, including `refitEvery > 1`.
- **DV-T17 Lattice.** A pivot is unusable before `confirmBar`. Anchors outside fibLookMult·s are dropped. Merge at 0.40 ATR keeps the lowest-|f−round f| member. The ±14 ATR filter applies. Ranges never merge across.
- **DV-T18 Confluence.** (a) Manifold + sigma only gives 2 (no qualify); adding a lattice line gives 3 (qualifies). (b) Two lattice lines from one range give nRanges = 1; from two ranges, 2. (c) Changing the report weights never changes families or the cluster set, and a source scan finds no 0.80 / 0.7 weight constant in `// <dvp-core>`. (d) The seed order is deterministic under input permutation. (e) The tolerance boundary at 0.30 ATR is inclusive.
- **DV-T19 Acceptance.** N = 2/3/5 with a moving rail: a bar that beats the old rail but not its own rail does not count. The trigger needs C_t > rail + δ and C_{t−1} ≤ rail (no δ). Both counting readings and both deltaAtr values; `BackInside`; assert N ≤ 6.
- **DV-T19b PS-25 counting (Q1 DECIDED).** With `countTrigger = true, N = 2`: trigger + 1 further close beyond → not accepted; trigger + 2 further closes → accepted on the 2nd further close; a 2nd further close beyond the old rail but not its own → not accepted. With `countTrigger = false, N = 2` (KD-06): trigger + 1 further close → accepted. N = 5 with countTrigger = true uses 6 snapshots.
- **DV-T20 Cone.** Matches the formula. It never appears in `Rails`, `Confluence` or `FamiliesAt`.
- **DV-T21 HTF.** A forming Data2 bar never changes a Data1 snapshot (truncate the HTF series at the forming bar: bit-identical). A same-root Data2 roll is detected; a different root is rejected by Validate. The Data1 read maps to the last closed HTF bar. `Tradeable(2)` is false while P2_PASSED is false, with one Warning.
- **DV-T22 Laziness.** Disabled: `Dvp == null`, one Warning on query, no ring, no PA. Enabled with no query and journal off: the fit counter is 0.
- **DV-T23 Config.** Validate ranges. `Canon()` is stable and includes the constants and the `DvpFrozenDegree` table.
- **DV-T24 RG-10 primitives.** The slope sign and units (σ₁ per bar) on a synthetic ramp.
- **DV-T25 Recalc.** Running twice gives identical journals.
- **DV-T26 Perf.** The DV-13.2 budget and zero allocations (no `Accumulator.Dense()` on the path).
- **DV-T27 RollDetection Off (rollMode Detect).** One Warning, every stream diagnostic-only, skipShare reported as NaN (not 0).
- **DV-T27b rollMode None** (stock/ETF/FX/crypto profile, waits on #36): no Warning, skipShare = 0, stream not diagnostic-only, tradeable.
- **DV-T27c** Non-future with RollDetection on: throws (M-INSTR reason 13).
- **DV-T28 24h session.** A synthetic Globex session crossing midnight is one session (one A2-33 sample, one session boundary).
- **DV-T29 Half-day.** A half-day session gives one A2-33 sample with `halfDay=1`, and its bars stay in fit windows.

### 15.1 Regression fixtures

- **DV-R1 Golden journal.** A synthetic 15m series of 5,000 bars with two splices (fixture CSV). `DVP FIT`/`RAIL` lines are byte-identical to `fixtures/golden_fit.txt`.
- **DV-R2 Reference parity.** `reference.py` reproduces DV-R1's σ, pocZ, vaLoZ/HiZ and the node set to 1e−9 / exact.
- **DV-R3 Host smoke test (PROBE).** `_DvpEngineTest.Strategy.CS` on @ES 15m (+ Data2 60m), 2 years, journal on. Check skipShare per stream, mean cov_in ≈ 0.95, endpointDrift distribution, and the ≤ 15% slowdown.
- **DV-R4 Canary.** `EGate0Canary.KD02` in G0-b must have net PF ≤ 1.0 (> 1.0 = bug).

---

## 16. PR plan (each PR small and independently reviewable; merged by Ettienne)

1. **PR-D0a: `ELevelKind` DVP kinds.** Append the 8 kinds (DV-10.3). No behaviour change. Build + all test suites.
2. **PR-D0b: Prominence extraction.** `_ProfileMath.Prominence` with `Gate0Profile.Nodes` delegating to it. DV-T12a (bit-identical Gate0/Profile tests).
3. **PR-D1: Fit core.** `_DvpEngine.Function.CS` `// <dvp-core>`: `DvpConfig` (Validate/Canon), `DvpFit` (recurrence, σ, SSE fallback, leverage, BIC, degree modes, shadow d=1, cone), `DvpFrozenDegree` (empty). `tests/Dvp` scaffold + `reference.py`. DV-T1–T8, T20, T23. Not wired.
4. **PR-D2: Manifold and rails.** Fixed row array (E5), tick floor, VA, entropy/concentration (E2/E3) with unit cases, nodes, rails, LevelRef/tags. DV-T9a, T9–T13.
5. **PR-D3: Host wiring, Data1.** `protected DvpConfig`, startDvp/stepDvp in `startEngines`/`stepEngines`, `requireRing`, ATR, roll skip, lazy cache with path independence (E10), RollDetection-Off warning and diagnostic flag (E8), tradingDayKey/half-day handling (E9), `DVP FIT`/`CREATED`/`RUN` journal. DV-T14–T16, T16b, T22, T25, T27–T29, DV-R1, and the `rollOn(1)` rollBar test.
6. **PR-D4: Lattice.** Hosted PA pivots (DV-5.1), anchors, levels, merge/filter. DV-T17.
7. **PR-D5: Confluence + acceptance.** Clustering, `FamiliesAt`, report score, trigger, `RunBeyond`/`BackInside`/`Accepted(countTrigger, deltaAtr)` (E4) with tests for both counting readings (DV-T19, DV-T19b), rail history, `DVP RAIL` + `logSignal` + MFE40/MAE40, RG-10 primitives. DV-T18, T19, T24.
8. **PR-D6: HTF streams.** Data2/Data3, P2 gate, HtfAccepted/HtfNextRailDistAtr, no winLen sweep (240m diagnostic only, Q4). DV-T21, including the forming-HTF-bar truncation test and the same-root Data2 roll test.
9. **PR-D7: A2-33 hook.** DV-12. A LevelStudyTests case with DVP type strings.
10. **PR-D8: Canary + calibration.** Stays before PR-D9: the canary must run on the engine before any consumer. `EGate0Canary.KD02` **appended** after `PS03` (Gate0 calls `// <dvp-core>`), `degMode = Calibrate` coverage journal for KD-08. DV-R3 smoke and DV-R4.
11. **PR-D9: Freeze d\*.** Populate `DvpFrozenDegree` from the PR-D8 dev-window study (one table, numbers in the PR body). This changes `Canon()`, so every earlier journal hash changes; the PR body must say so. After this, consumer sandboxes (PS-25, MT-07, RG-09/10, KD-06/10) may start.
12. **PR-D10 (when P2 passes): flip `P2_PASSED`.** One line plus the probe evidence.
