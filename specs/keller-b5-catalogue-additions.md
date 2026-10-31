# Keller Book 5 — *Detrended Volume Profile & Order Flow* — catalogue additions (rev 3.1, reviewed by Analyst 1 and Analyst 2, SIGNED OFF by Ettienne 23 Oct 2026 (N = 2 primary))

**For:** Ettienne Hugo · **Prepared by:** Grok Bot · **Date:** Thu 22 Oct 2026 (America/New_York, EDT) · **Status:** **rev 3.1, reviewed by Analyst 1 and Analyst 2, SIGNED OFF by Ettienne 23 Oct 2026 (N = 2 primary)** (rev 3 Appendix F rulings §5; rev 3.1 rulings and KD-06..KD-11 §6); documentation only, nothing implemented or tested.
**Sources:** `kindle-notes/detrended-vp-orderflow.md` (primary), `kindle-notes/keller-b5-dvp-analyst2.md` (Analyst 2, covers 0.1–5.4), earlier Keller notes (`keller-b1…b4*`); rev 3.1 inputs `specs/keller-b5-p6p7-entries-analyst2.md` (Analyst 2), `specs/keller-b5-p6p7-review-analyst1.md` (Analyst 1), `specs/keller-b5-review-analyst2.md` (C1..C9); catalogue `trading-idea-catalogue.md` (template §1, conventions §2, Gate 0 §2.6, base-feature map §2.4/2.4a); repo `MultiCharts.Net` @ the merged base.

**Evidence rule used throughout.** Every number below is the book's printed number, quoted with its Kindle location ("loc"), and it is a **Forex (incl. XAU/XAG) / crypto result**. The book says outright that its scan **does not cover futures or equity indices** (0.2, Analyst 2 notes). **No ES statistic exists in this book and none is stated here.** Exhibit ratios (MFE/MAE) come from *selected* charts (selection score rewards a decisive event; loc 104–147, 6081–6125), so they are grade **E** on their own. Census rows (Part Two, 5.1) are full-population, grade **C (other asset class)**.

**ID scheme.** The catalogue (v2 merge; IDs PS, VW, AV, MT, OF, SP, TS, SW, RG, EX, PF, A2, DV, L2) has **no KD-\* entries and no B5-derived entries**, so this draft opens a new family **KD (Keller Detrended)**, starting at **KD-01**. **Catalogue mapping (applied in catalogue v2.3, corrected 23 Oct 2026):** KD-01 → PS-25; KD-03 → RG-09 and KD-04 → RG-10 (RG modules on KD-01); KD-05 → MT-07; KD-02 stays a canary only (G0-b; no trial). There is **no PS-26**. KD-06…KD-11 live in the catalogue's §3.16; Appendix A of the catalogue carries the new rows (A.6).

---

## 1. Method summary (what the book does)

1. **Detrend the window.** Fit a polynomial ŷ(u) to the last `winLen` = 240 bars of hl2 (u scaled to [−1, +1]), using **Forsythe (1957) orthogonal polynomials by three-term recurrence** rather than Vandermonde normal equations (which have condition number ≈ 5×10⁹ at degree 2 and 1.7×10²⁰ at degree 4 on 240 bars, loc 314–355). Degree chosen by **BIC** = n·ln(SSE_d/n) + (d+2)·ln n, capped at 4 in the script (the census capped at 3). *(Book method. M-DVP's primary degree is `DevCoverageFrozen`, degMax = 3; BIC is report-only — §2.2.)*
2. **σ is the residual standard error,** √(SSE/(n−d−1)), not the dispersion of price (loc 374–414). Band = ŷ ± kσ; forward **cone** = ŷ ± kσ√(1+h(u)) with leverage h(u) = Σ p_k(u)²/N_k.
3. **Residual-space volume manifold.** Each bar's [low, high] is converted to residual z; its volume is spread over the residual rows it overlaps (64 rows over ±3σ; outside volume kept as "tail volume"). POC, 70% value area, HVN/LVN (prominence 0.12) are constant residual offsets that, mapped back through ŷ, become **curved rails** (loc 430–473).
4. **Fibonacci lattice** from up to 3–4 anchored swing ranges (pivot 20, each range ≈ double), no volume and no curve — the independent third witness. **Confluence** = number of *distinct families* (manifold, sigma rails, lattice) naming a price within **0.3 ATR**.
5. **The book's own verdicts** (Part Two, Ch 5.1, Ch 8.1, Ch 8.3): the band is calibrated only on its own bars; forward it collapses; degree costs forward; a third family adds 6–7 pts of one-bar hold, a second adds nothing; fit quality is irrelevant; band→POC reversion is a coin flip; "held" is a one-bar verdict with bad geometry, while **accepted** rails carry the good geometry. Operating rule 5: *treat every rail as a destination and every acceptance as the entry* (B·Practitioner, loc 6186–6213). The book is a measurement census, not a strategy (0.2: "does not contain … a strategy").

### 1.1 Printed numbers used in this draft (Forex | Crypto; all non-ES)

| Claim | FX | Crypto | Loc | Population? |
|---|---|---|---|---|
| In-sample ±2σ coverage (nominal 95.4%) | 95.6% (34,423 fits) | 95.5% (41,518 fits) | 83–145, 499–545 | census, **fit-on-own-data** |
| Forward ±2σ coverage (60 bars past a 240-bar window) | 28.2% | 27.7% | 553–593 | census |
| Forward ±1σ / ±3σ | 14.7% / 39.9% | 14.4% / 39.1% | 553–593 | census |
| Whole 60-bar projection inside cone | 5.4% (1,873) | 5.3% (2,182) | 553–593, 4540 | census |
| Median bars to first breach | 7 | 7 | 553–593 | census |
| Forward ±2σ by BIC degree 1 / 2 / 3 | 47.8 / 33.1 / 26.4% | 46.0 / 31.4 / 26.3% | 610–651 | census (selection-confounded; book's own caveat) |
| BIC picked degree 3 | 80% | 81% | 314–355 | census |
| Durbin–Watson of residuals | ≈ 0.1 | ≈ 0.1 | 374–414 | census |
| One-bar hold, 1 / 2 / 3 families | 79.1 / 78.8 / 86.5% | 79.1 / 78.7 / 85.4% | 665–710 | census (one-bar verdict) |
| Hold, R² ≥ 0.35 vs < 0.35 | 79.5 vs 80.4% | 79.3 vs 80.6% | 720–765 | census |
| ±2σ excursions returning to POC rail | 51.8% (66,411/128,247), median 19 bars | 51.3% (79,223/154,429), median 19 bars | 772–813 | census |
| One-bar hold by rail: VAH / VAL / POC / LVN / σ | 88.9 / 89.3 / 73.4 / 73.2 / 72.3% | 88.9 / 89.5 / 73.5 / 73.0 / 72.4% | 2684 | census; **HVN 100.0% is a definitional artefact, never cite** |
| After *held*: median MFE/MAE (ATR) | 2.4 / 5.2 | 2.1 / 6.0 | 2684–2726 | **selected exhibits** |
| After *accepted*: median MFE/MAE (ATR) | 6.3 / 0.45 | 6.2 / 0.3 | 2684–2726 | **selected exhibits**; MAE measured *after* acceptance was visible |
| #20/#21 acceptance beyond value, ratio | 19.6 / 24.0 | 47.0 / undefined (MAE 0.0) | 3264, 6058 | selected exhibits (3–6 per cell) |
| #17 POC accepted-through ratio | 11.45 | 10.67 | 2753 | selected exhibits |
| #27 band→POC exhibits ratio | 1.05 | 1.00 | 4237 | selected exhibits |
| 3-family exhibits (#34/#35) MFE/MAE | 0.58 / 0.60 | 0.80 / 0.44 | 5180–5201 | selected exhibits |
| Held vs accepted gap (mean ratio) | 0.48 vs 15.6 (~32×) | 0.36 vs 25.9 (~71×) | 6058–6125 | selected exhibits |

### 1.2 Gaps from unread chapters (must be closed before the specs are frozen)
- **Not read at all:** 0.3 (partly, via Analyst 2), 3.2–3.5 (BIC choice in practice; DW; asymmetric dispersion; volatility regime — matters for KD-02/KD-04), 4.1–4.4 (manifold shapes: value above/below trend, width, bimodal, heavy tails), 5.5 (HVN), 6.1 text, 7.1, **7.3 (#36/#37 confluence node held/failed — the population table for KD-03)**, **7.4 (#38 geometry vs volume disagree; rule 7 "stand aside")**, 7.5 text, 8.2 after loc 6150, **Appendix B (behaviour registry, incl. both-way resolutions), C (evidence register), D (corpus definitions, selection score, open items, the population-level falsification "item 1"), F (errata and the retraction)**, and Appendix E code loc 6945–7290 (manifold binning body, rails, lattice, confluence scoring) and 7300–7612.
- **Consequences:** (a) the exact acceptance definition used by the scan (persistence `m` for band breaks is "not published by scan", loc 4414; the 41-bar horizon is known); (b) the exact rail-interaction ("touch") definition; (c) the input between `vaPct` and `nodeProm` (not seen, loc 6769–6784); (d) lattice anchor rule details and HVN/LVN prominence code; (e) ~~Appendix F retraction content~~ — **closed in rev 3** (§5): weights withdrawn, none issued; M-DVP uses a weightless score. Every spec below marks these as **[GAP]** and uses an explicit, pre-registered substitute.

---

## 2. Proposed engine spec: **M-DVP** (detrended volume-profile engine)

**Status:** ✗ new (would join §2.4 as `M-DVP`; §8 build order: after VP helpers). **Convention:** spec `missing-functionality-specs.md` rev 4 §0.1 — plain `internal sealed class`, MC-free, created in `StartCalc` only when a derived strategy sets a **protected configuration property** on `_CustomSignal` (`protected DvpConfig DvpConfig { get; } = new DvpConfig();`, `enabled = false`); **no new `[Input]`** (D7). Null when disabled; querying it disabled logs one `ETraceLevel.Warning` (same pattern as `PriceActionEngine`).

### 2.1 Files and reuse
| Piece | Reuse | New |
|---|---|---|
| Ring of closed bars per stream | `_RangeProfile` ring pattern (`RangeProfileConfig.streams`, `ringBars`) or the M-MTF closed-bar feed | — |
| Higher-TF streams (60m/240m on a 15m chart) | **M-MTF**: `_CustomSignal.HtfClosed/HtfClosedBar` via `BarsOfData(n)`, closed bars only; no internal HTF bar building | — |
| Volume distribution over a span | `_ProfileMath.Accumulator.Add(…, mode 2 = uniform over [L,H])` — called with **residual-row coordinates** instead of price | thin adapter mapping z-rows to a dense row grid |
| POC / 70% VA | `_ProfileMath.ValueAreaExpansion(vols, 70, out lo, out hi, out poc)` (book: grow outward two rows at a time taking the heavier pair — verify identical; if not, flag `vaMethod`) | — |
| HVN/LVN | `_VpHistogram` node logic with prominence 0.12 as fraction of tallest row (book rule); [GAP] exact prominence code unread | prominence adapter |
| Entropy / concentration | `_ProfileStats.Entropy(vols, EEntropyRows.TradedRows, …)` (book H = −Σp ln p / ln R over occupied rows) | concentration = POC row / mean row |
| Level objects | `_LevelRef.Line/Zone` — new `ELevelKind` values `DvpPoc, DvpVah, DvpVal, DvpHvn, DvpLvn, DvpSigma, DvpFib, DvpConf`; `sourceT0/T1` = fit window; `createdTime` = first bar it may be used; `quality` = confluence score | enum additions |
| Swing anchors for the lattice | `_PriceActionEngine` confirmed pivots (confirmation time, not pivot time) | Fib lattice builder |
| ATR | the base's ATR / `Gate0Atr` (same ATR as the journal's R/MFE/MAE) | — |
| Optional comparison curve | M-AVWAP anchors (`_AvwapManager`) as an alternative "fair-value curve" control (book's institutional note) | — |
| Mixture check of the manifold (bimodal, #13) | `_Gmm` (K = 1..3, BIC) on residual rows — **optional, off**, M-GMM still deferred | — |

New code: `_DvpEngine.Function.CS` (`_DvpEngine`, `DvpConfig`, `DvpFit`, `DvpManifold`, `DvpRail`, `DvpLattice`), compiled in `PLStudies.csproj`; off-MC unit tests under `tests/Dvp` (recurrence vs QR reference, shift invariance drill 1.2, in-sample coverage drill 2.1, causality test below).

### 2.2 `DvpConfig` (protected; defaults = book Appendix E, loc 6752–6875)
```
enabled=false; streams={1}            // e.g. {1} 15m, {2} 60m, {3} 240m via BarsOfData
src=HL2; winLen=240 (30..1000); degMode=DevCoverageFrozen; degMax=3 (dCap=min(degMax, max(1, n/8)))
                                       // DevCoverageFrozen = the coverage-match score, dev only, frozen at sign-off
                                       // d = 1 sensitivity arm; BIC report-only; minBars=12
weightVol=false                        // WLS off (book default); if on, weights normalised to mean 1
minR2=0.35                             // description flag only; NOT a trade filter (Ch 2.5, 3.1)
manRows=64 (row floor one tick: rowWidth=max(σ-row, tick); effective manRows journaled); manSpan=3.0σ; vaPct=70; nodeProm=0.12; maxNodes=3/side; sigmaList={1,2,3}
projBars=60; coneMode=Prediction; coneMult=2.0
useFib=true; fibLevels={0,.22,.5,.62,.71,.786,.886,1,1.414,1.618,2.618,3.618,4.236,-.146,-.272,-.414,-.764,-1,-2,-2.5,-2.62,-3,-3.5,-4}
fibRanges=3 (≤4); fibPivot=20; fibLookMult=8; fibAnchor=MostRecentPivot; fibMerge=0.40 ATR; fibNear=14 ATR
confTol=0.30 ATR; scoring: WEIGHTLESS family count (rev 3, §5.1); within-family weights all 1.0 (POC = VA edge = 1.0/1.0)
// book's published wPoc=1.00/wVaEdge=0.80 are RETRACTED (App. F) and NEVER used
// report-only sensitivity: wPoc=0.77, wVaEdge=1.00 (no trial spent)
splitFlow=false                        // bar-shape proxy only; never used for levels or signals (catalogue §2.4 OF rule)
refitEvery=1 bar; journalFits=true; journalRails=true
```
`Validate()` and `Canon()` as in the other configs (config hash goes to the journal).

### 2.3 Fit (per stream, per closed bar)
- Window = closed bars t−n … t−1 (see §2.6). u_i = (i − ctr)/scl, ctr = (n−1)/2, scl = max((n−1)/2, 1e-9).
- Recurrence: p₋₁=0, p₀=1, p_{k+1}=(u−A_k)p_k − B_k p_{k−1}; N_k=Σw p_k²; A_k=Σw u p_k²/N_k; B_k=N_k/N_{k−1} (B₀=0); C_k=Σw y p_k/N_k; SSE_d=Σw y² − Σ_{k≤d}C_k²N_k (all degrees in one pass). **Numerical note:** compute on y − ȳ (de-meaned) to avoid the "add 1,000,000" drill failure (loc 314–355); assert SSE_d ≥ 0, fallback to two-pass residual sum.
- BIC(d) = n·ln(SSE_d/n) + (d+2)·ln n over 1..dCap is computed and journaled **report-only** (never selects). The primary degree d* is `DevCoverageFrozen`: the coverage-match score (|forward − in-sample ±2σ coverage|, averaged over dev walk-forward folds; lowest wins, ties to lower degree, d ≤ 3) evaluated on dev only and frozen at sign-off; d = 1 is the sensitivity arm. Store d*, also SSE₁ (degree-1 shadow fit, for KD-04 / book rule 2).
- σ = √(SSE/(n−d−1)); R², R²adj, DW, skew, kurt; h(u) = Σ p_k(u)²/N_k.
- Band at the *last window bar* (u=+1): ŷ(1) ± kσ. **Forward use:** cone ŷ(u) ± kσ√(1+h(u)) for u>1 — **reference only, never a stop/target edge** (book rule 4 of daily process, loc 6186–6213).

### 2.4 Manifold, rails, lattice, confluence
- z-rows: row width = 2·manSpan/manRows σ. Bar i spans [(l_i − ŷ_i)/σ, (h_i − ŷ_i)/σ]; v_r += v_i · overlap/(span) (zero-range bars → single row); volume outside ±manSpan → `tailVol` (never dropped).
- POC row, VA rows (70%), HVN/LVN rows (prominence), entropy, concentration, tail share.
- **Rails at bar t:** price_t(rail) = ŷ_{fit}(u_t) + z_rail·σ, where the fit is the one frozen at t−1 and u_t = 1 + 2/(n−1) (first step past the window). Sigma rails at ±1, ±2, ±3σ.
- **Lattice:** up to 3 ranges from confirmed pivots (strength 20, 40, 80 bars; anchor within 8×strength), levels from `fibLevels`, merged within 0.40 ATR, kept within ±14 ATR.
- **Confluence:** greedy cluster of candidates within **0.3 ATR** of the heaviest unclaimed one; `families` = count of non-zero {manifold, sigma, lattice}; `nRanges` = distinct lattice ranges; **score = `families`** (weightless, rev 3 §5.1): it counts families, not lines; a qualifying confluence requires the **third family** (`families == 3`, book 8.3 rule 3). Within a family every rail weighs 1.0 (POC 1.0 / VA edge 1.0). A level from two lines of one range counts once (book: distinct ranges, not lines).
- Output: `IReadOnlyList<LevelRef>` per stream with `quality` = score, `tag` = `"DVP{ds}:{t-1:yyyyMMddHHmm}:d{deg}:{kind}{z:+0.00}"`.

### 2.5 Laziness and cost
Created only if `enabled`; computes on the first query in a bar (cache keyed by stream + bar), not on every tick. O(n·d) recurrence + O(n·rows) manifold per refit per stream; for 3 streams at 240 bars this is negligible. `refitEvery` > 1 allowed for sweeps but **rails always come from the most recent fit ending at or before t−1**.

### 2.6 Strict causality (non-negotiable)
1. **Closed bars only.** The fit at bar t uses bars t−n … t−1 of that stream. The forming bar is never in a window. HTF streams come only from `HtfClosedBar` (M-MTF, VPB-8 `closedBarsOnly`); a 240m rail is usable on the 15m chart only after the 240m bar has closed **and** Probe P2 (bar-end stamping / same-timestamp ordering) has passed.
2. **Rails at t from the window ending at t−1.** `LevelRef.createdTime` = open time of bar t; an event at bar t is judged against rails frozen before t opened. The bar being judged never contributes to the curve, σ, or manifold that define its rail.
3. **Pivots at confirmation time** (`_PriceActionEngine` confirmation stamp), never at pivot time.
4. **ATR** for tolerances = ATR known at t−1.
5. **Unit test (off-MC):** recompute every rail at t from a truncated series ending at t−1 and assert bit-equality with the live value (the "truncation audit" pattern of F-DEV/VPB-4); plus a shuffled-future test (perturb bars ≥ t, rails at t unchanged).

### 2.7 Repainting risk of polynomial endpoints (must be in every report)
A least-squares polynomial is **worst-determined at its endpoints** (leverage h(u) peaks at u = ±1 and grows ~u^{2d} beyond, loc 374–414); adding one bar re-weights the whole curve, so **the "rail" drawn at bar t−20 on a chart today is not the rail that existed at t−20**. Charts and the book's exhibits (drawn from the full window, event inside it) therefore show endpoint-revised curves. Mitigations in M-DVP: (a) journal the **as-of** fit (degree, coefficients C_k, A_k, B_k, σ, ŷ(1)) at every bar so any rail can be reconstructed exactly as it was; (b) report `endpointDrift` = |ŷ_t-fit(u at bar t−1) − ŷ_{t−1}-fit(1)| / σ per bar as a diagnostic; (c) a degree-1 shadow fit for comparison (book rule 2: lowest degree not wrong about slope); (d) **never** evaluate on a non-causal (centred / full-window) refit. Any backtest that draws rails from a later fit is a lookahead bug and should trip the G0-b canaries. The residual manifold is itself a repaint object: every refit re-maps all 240 bars. Only the t−1 snapshot is tradeable, and charts are rebuilt from the journaled as-of fits.

### 2.8 Evidence caveats carried with the engine
- The book's **in-sample** band figures (95.5–95.6% at ±2σ) are **fit-on-own-data** and prove only "that arithmetic works" (loc 83–145); forward coverage collapses to **28.2% FX / 27.7% crypto** as printed (loc 553–593), with the whole projection inside the cone 5.3–5.4% of the time and median first breach at bar 7. Residual DW ≈ 0.1. Nothing in M-DVP should be described as a containment band.
- The reference implementation is not the census code (census = Python pipeline; script allows degree 4, census capped at 3; loc 6130–6150) — our engine reproduces the published method, not the measured one.
- Buy/sell split is a bar-shape proxy (Book 4 delta_proxy), not order flow; off by default.

### 2.9 Journal (ETraceLevel.Journal)
At `TraceLevel ≥ Journal`, per stream per bar (`journalFits`): `runId, configHash, stream, barTime, n, deg, bic[1..dCap] (report-only), manRowsEff, rthShare (RTH share of volume in the fit window), crossesRoll, sigma, sigmaAtr, r2, dw, skew, yhatEnd, slopeEnd, endpointDrift, pocZ, vaLoZ, vaHiZ, entropy, concentration, tailVol, inSampleCov2`. Per rail interaction (`journalRails`): `railKind, z, price, families, nRanges, score, distAtr, closedThrough(1 bar), heldBars, accepted(N), MFE40, MAE40` — the book's "journal both columns" rule (next bar + worst move against the rail in 40 bars, loc 6186–6213). Uses the existing `trace(ETraceLevel.Journal, …)` / `MCJournal_<runId>.csv`; forward MFE/MAE via F-EVT `logSignal` (so `ControlMode` RandomEntry / ShiftLevels / DelayEntryBars work unchanged).

### 2.10 Readiness
Not yet buildable — needs `_DvpEngine` (new), `ELevelKind` additions, Fib lattice builder, prominence adapter; depends on ✓ M-MTF (Probe P2 still open), ✓ M-SWING, F-EVT, `_ProfileMath`, `_ProfileStats`. Optional `_Gmm` (M-GMM deferred) not required.

---

## 3. Candidate ideas (KD-01 … KD-05)

Common to all: **Markets · TF:** ES (MES for live sizing) on **15m, 60m, 240m** M-DVP streams (15m = Data1; 60m/240m via `BarsOfData`). **Data · test window:** 1m-built bars · **W-M** (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: **a fit window may not cross a roll** — windows spanning a roll splice are skipped (`crossesRoll` → no rails) until `winLen` bars have accrued post-roll (240 × 240m ≈ 10+ weeks → 240m stream is sparse; sweep `winLen` 120 there; flagged). ETH included (book fits continuous bars); RTH-only variant as a sensitivity arm. **Time-of-day volume (C6d):** journal the RTH share of volume per fit window and report results split by RTH share {<50%, ≥50%} — report-only; ETH stays primary. **Row floor (C6c):** covered by the one-tick floor (§2.2). Costs: G0-a (A2-40 + A2-59). Standard controls §2.3 apply. **Evidence:** none of these setups has been tested by the book on any market; family-level evidence is FX/crypto only; every KD row carries **FX/crypto only · no true delta · ES prior 0**.

#### KD-01 · Accept-beyond-rail continuation (detrended rails, rails as targets) — **primary**
- **Family · Source:** KD · Keller B5 Ch 5.1 (loc 2684–2726), 5.4 #20/#21 (loc 3264), 5.2 #17 (loc 2753), 8.1 (loc 6058–6125), 8.3 rule 5 "every rail a destination, every acceptance the entry" (loc 6186–6213); continuity with Keller B4 §8.4 / PS-01, PS-02, DV-02.
- **Evidence:** **C (+) family, FX/crypto, exhibit-level** · **setup untested** · **E for the geometry**. Accepted-rail exhibits: MFE/MAE 6.3/0.45 ATR FX, 6.2/0.3 crypto (loc 2684); #20/#21 ratios 19.6/24.0 FX, 47.0/undefined crypto (loc 6058). **Caveat printed by the book itself:** MAE is measured *after* acceptance was visible, and acceptance is known only in hindsight (loc 3264, 6081–6125); the population-level comparison of entering at first close vs after acceptance was **not done** (C hypothesis, Analyst 2 note at 5.4). Lit: Book. No ES number exists. · **FX/crypto only · no true delta · ES prior 0**.
- **Why it might work:** a close beyond a trend-relative value rail that *stays* means the trend has re-rated and the manifold is out of date; responsive traders who faded the rail are offside; continuation runs to the next rail.
- **Rules (long; short mirrored):**
  - *Rails:* M-DVP rails at t from the fit ending t−1 (VAH, POC, upper HVN/LVN, +1/+2/+3σ). Event rail ∈ {VAH (#20), POC (#17)} — default VAH.
  - *Trigger:* bar t closes > rail_t + δ, δ = 0.10 ATR; bar t−1 closed ≤ rail_{t−1}.
  - *Acceptance (hold-for-N):* the next **N = 2** closed bars (primary; confirmed by Ettienne 23 Oct 2026, §6.3) all close > their own rail (each rail re-read from that bar's t−1 fit) **and** no close back inside by more than δ. [GAP: scan's acceptance horizon is 41 bars as verdict; its entry-relevant persistence `m` is unpublished, loc 4414 — N is our pre-registered substitute.]
  - *Filter:* fit stream not in roll exclusion; `sigmaAtr` ≤ 4 (book: weak-fit cards run σ 4.2 ATR vs corpus ~1.5, loc 856–878); **no R² filter** (book: no effect, loc 720–765).
  - *Entry:* buy-stop 1 tick above the high of the N-th acceptance bar, valid for 2 bars.
  - *Stop:* max(1 tick below the lowest low of the acceptance bars, rail_t − 0.5 ATR), cost floor per A2-40.
  - *Target:* next rail above in residual order (upper HVN → +2σ → +3σ) computed from the fit frozen at entry; partial 50% at first rail, remainder trailed by EX-05 or time stop 41 bars (book horizon).
  - *Invalidation:* close back below rail − δ.
- **Sweep (≤ 18):** N ∈ {2, 3, 5}; event rail ∈ {VAH/VAL, POC}; target ∈ {next rail, +2σ rail, trail EX-05} → 18.
- **Frequency (est.):** author estimate, 15m: a few per week; 60m: ~1–3 per month; 240m: < 1 per month (and fewer after roll exclusion).
- **Fails when:** acceptance is a news spike that retraces; rail moves with the refit (endpoint repaint) so the "hold" is partly a curve artefact; waiting N bars spends most of the move (book: the measured 6.2–6.3 ATR is what is *left* after waiting).
- **Needs:** ✗ M-DVP; ✓ M-MTF (P2 open); F-EVT, F-TM, F-JRNL; A2-40/59.
- **Readiness (as of this draft):** Not yet buildable — still needs: M-DVP (`_DvpEngine`, lattice, `ELevelKind` DVP kinds); Probe P2 for 60m/240m streams.
- **Test / falsification:** (1) **Population version of the book's open experiment:** for *every* first close beyond a VA/POC rail on ES, compare median MFE/MAE (40 bars) entering at the trigger close vs after N-bar acceptance; also held (no acceptance) vs accepted using a **symmetric 41-bar clock** (loc 2706–2726). (2) Controls: K-RT random-time entries matched by direction and vol; **K-FL/ShiftLevels**: same rules on rails shifted ±0.5σ (fake rails); **flat-profile twin** (same rules on an F-COMP flat profile of the same 240 bars = PS-02-style) — detrending must add something; **degree-1 twin**. **Kill:** population accepted-entry median MFE/MAE < 3 (book's own falsifier, loc 6186–6213) **or** no lift over ShiftLevels **or** no lift over the flat-profile twin **or** net expectancy ≤ 0 under G0-a.
- **Gate 0 hooks:** G0-a every result; G0-b canaries re-run after the engine lands (engine change) and must stay ≤ PF 1.0 — including KD-02 below as a DVP-specific canary; G0-c n/a for in-window rails but **roll-window exclusion** mandatory; **G0-d: add `DvpVah/DvpVal/DvpPoc/DvpHvn/DvpLvn/DvpSigma/DvpConf` to the A2-33 level list** (20 matched controls at 0.9–1.1× distance) — KD-01 is not read until A2-33 returns |t| ≥ 2 for the event rail type.
- **Readiness line:** *Readiness (as of this draft): Not yet buildable as specified — still needs: M-DVP; A2-33 harness (DVP level types); Probe P2 (HTF streams).*
- **Overlaps with A2 entries (C5; report-only, no P&L picks):** A2-31 → tie-break with PS-25 (rev 2.1 rulings); A2-33 → G0-d matched controls (above).
  - **A2-25:** check POC stability before using the POC as an event rail.
  - **A2-26/27:** reuse their bimodal/LVN veto.
  - **A2-28:** same rails as HVN/LVN.
  - **A2-29/30:** entropy is the same statistic as manifold entropy; report the correlation.
  - **A2-32:** closest overlap (sigma-space levels); report the trade overlap.
  - **No P&L picks.**

#### KD-02 · Band-stretch fade to the POC rail — **control / canary**
- **Family · Source:** KD · B5 Ch 2.6 (loc 772–813), 6.3 #27 (loc 4237), 8.3 rule 4 "do not fade the band".
- **Evidence:** **C (0) FX/crypto** — reversion to POC 51.8% FX / 51.3% crypto, median 19 bars (loc 772–813); exhibit ratio 1.05 / 1.00, "a trade with no shape" (loc 4237). Fade expectancy conditionals (E[MFE|rev], E[MAE|no rev]) **not in the census** (loc 772–813). ES: none. **Expected result: ≈ 0 or negative net. Kept as a canary, not as a candidate edge.** · **FX/crypto only · no true delta · ES prior 0**.
- **Rules (short at upper stretch; long mirrored):** *Trigger:* bar t closes > ŷ(u_t) + 2σ (fit ending t−1). *Entry:* market at open of t+1. *Stop:* +3σ rail + 0.1 ATR (book: "stop at the next band 1σ away"). *Target:* POC rail (as re-read each bar from the t−1 fit). *Time stop:* 19 bars (book median) and 40 bars (variant).
- **Sweep (≤ 4):** time stop ∈ {19, 40}; stop ∈ {+3σ, +2.5σ}.
- **Falsification / role:** canary passes if net PF ≤ 1.0 and the POC-reach rate is within ±5 pp of 50% (FX/crypto 51–52% is the prior, not an ES fact). **Kill (canary fails): net PF > 1.0 — no 1.0–1.1 zone.** A failed canary is treated first as a bug (rails from a later fit, forming-bar leakage, endpoint repaint) per G0-b, then — only after the causality unit tests pass — as a finding. It also fixes the denominator for KD-01 (same rail set, opposite logic).
- **Gate 0 hooks:** joins the **G0-b canary suite** (alongside VW-08, A2-24, DV-10 — this is the detrended analogue of DV-10's σ-band fade) for every M-DVP engine change; G0-a costs.
- **Readiness line:** *Readiness (as of this draft): Not yet buildable — still needs: M-DVP (band + POC rail only; manifold required, lattice not).*

#### KD-03 · Three-family confluence filter (module, not an entry)
- **Family · Source:** KD (rides as an RG-style module) · B5 Ch 1.4 (loc 430–473), 2.4 (loc 665–710), 7.2 (loc 5180–5201), 8.3 rule 3.
- **Evidence:** **C (+) one-bar hold only, inadmissible as evidence; admissible test = A2-33 matched controls.** · **FX/crypto only · no true delta · ES prior 0**. One-bar hold census, FX/crypto: 3 families 86.5% vs 1 family 79.1% FX; 85.4% vs 79.1% crypto; 2 families add nothing (78.8% / 78.7%) (loc 665–710). **E (0/−) for trade geometry:** 3-family exhibits MFE/MAE 0.58–0.80 (loc 5180); book: "do not buy confluence as an entry filter" (loc 5180). Population-level excursion split **not published** (C hypothesis). [GAP: 7.3 #36/#37 and 7.4 #38 tables unread.]
- **Hypothesis:** confluence does not choose direction, but conditional on **acceptance** (KD-01) a three-family rail that is *broken and accepted* is a more significant re-rating (book #37 "confluence node failed" is in the accepted family, ratio 14.0 FX / 31.0 crypto, loc 6058 — selected exhibits).
- **Rules:** `families(rail_t) == 3` within **0.3 ATR** (distinct families; lattice counted by distinct ranges, ≥ 1 range); manifold+sigma alone never counts as 2 independent witnesses. Arms: **A** (KD-01 only when event rail has 3 families), **B** (KD-01 only when ≤ 1 family), **C** (veto KD-01 when the lattice names a price within 0.3–1.0 ATR but not within 0.3 — book rule 7 "witnesses disagree", C hypothesis).
- **Sweep (≤ 6):** confTol ∈ {0.2, 0.3, 0.5} ATR × arm ∈ {A, C}.
- **Falsification:** (1) replicate the census on ES: one-bar hold by family count with a matched-control rail (A2-33 style); (2) book's own falsifier: median 40-bar MAE of held 3-family vs held 1-family interactions — if not materially smaller, confluence does not shape the trade (loc 5201); (3) the module is accepted only if arm A beats unfiltered KD-01 on net expectancy **and** beats a random-subsample filter with the same pass rate. **Kill:** no lift vs the random-subsample control.
- **Gate 0 hooks:** G0-d level type `DvpConf` scored by A2-33; G0-a; inherits KD-01's gates.
- **Readiness line:** *Readiness (as of this draft): Not yet buildable — still needs: M-DVP incl. Fib lattice and confluence scorer; ✓ M-SWING pivots.*

#### KD-04 · Detrended-fit regime gate: degree-1 slope with band-break refit (module)
- **Family · Source:** KD (RG module) · B5 2.3 (loc 610–651), 6.4 #28 (loc 4391–4414), 8.3 rule 2 and daily process step 6 ("refit when band breaks and keeps going").
- **Evidence:** **C (+) FX/crypto** that lower degree projects better: forward ±2σ 47.8/46.0% (degree 1) vs 26.4/26.3% (degree 3) (loc 610–651), **confounded by BIC selection** (book: separating experiment not done). **H** as a trade gate. · **FX/crypto only · no true delta · ES prior 0**.
- **Rules:** long-only for KD-01 when degree-1 slope over the frozen window > +0.05 σ/bar (short mirrored); **stand down** for `winLen/4` bars after |z| > 2 persists for ≥ 3 closed bars against the gate direction (band broke and kept going), then use the fresh fit.
- **Sweep (≤ 4):** slope threshold ∈ {0, 0.05} σ/bar × stand-down ∈ {on, off}.
- **Falsification:** ablation inside KD-01 and on host H1 (PS-01) — accepted only if it helps both; also pre-register the book's missing experiment on ES: refit every window at degree 1, 2, 3 and compare forward coverage *within the same windows*.
- **Gate 0 hooks:** not a level (not G0-d), G0-a/G0-b apply.
- **Readiness line:** *Readiness (as of this draft): Not yet buildable — still needs: M-DVP fit (manifold not required).*

#### KD-05 · HTF detrended-rail alignment (15m entries on 60m/240m accepted rails)
- **Family · Source:** KD (MT-style) · B5 0.3 measured line "confirmed on TF" (Analyst 2 notes, loc ~155–181); catalogue MT family.
- **Evidence:** **H** — the book reports "confirmed on [higher] TF" on cards but no census by timeframe alignment (not seen in read chapters; [GAP] App. B/D). · **FX/crypto only · no true delta · ES prior 0**.
- **Rules:** KD-01 on 15m only when the 60m (variant 240m) M-DVP stream, from its **last closed** HTF bar, shows the same-direction acceptance state (close beyond its VAH/VAL for ≥ 2 HTF bars) and the 15m target rail lies inside the HTF next-rail distance.
- **Sweep (≤ 4):** HTF ∈ {60m, 240m} × HTF acceptance bars ∈ {1, 2}.
- **Falsification:** vs KD-01 unfiltered and vs a time-shifted HTF state (state from 1 HTF bar earlier — tests lookahead sensitivity); kill if no lift or if lift appears only with forming-bar data.
- **Gate 0 hooks:** G0-d for DVP level types; Probe P2 must pass first (HTF bar-end stamping).
- **Readiness line:** *Readiness (as of this draft): Not yet buildable — still needs: M-DVP on Data2/Data3; Probe P2.*

---

## 4. Catalogue edits (applied in catalogue v2.3, 23 Oct 2026; mapping corrected per Analyst 2)
**Mapping:** KD-01 → **PS-25** (§3.1); KD-03 → **RG-09** and KD-04 → **RG-10** (§3.9; RG modules on KD-01); KD-05 → **MT-07** (§3.4); **KD-02 → canary only** (G0-b; no trial, not scored); **no PS-26**. KD-06…KD-11 in the new §3.16 (mapping table, KD-02 and KD-06…KD-11).
1. §2.4 / §2.4a: add row **✗ M-DVP** (spec §2 above); §8 build order: after VP helpers, before the KD batch (row 3d).
2. §2.6 G0-b: add **KD-02** to the canary list; G0-d: add DVP level types to A2-33's list.
3. Entries PS-25, RG-09, RG-10, MT-07 in their host families; new §3.16 "Family KD: Keller B5 detrended rails" with the mapping, KD-02 and KD-06…KD-11; Appendix A.6 rows; companion-documents line: add Keller B5 notes.
4. §4 matrix: PS-25 (KD-01) scored as C(family)/setup untested (non-evidence scores are drafter estimates [est]); KD-02 not scored (canary); RG-09/RG-10 as modules (not scored); MT-07 scored [est] per the catalogue's MT convention.

## Rev 2.1 rulings (agreed Analyst 1 / Analyst 2, 2026-10-22)

- **Holm family.** {PS-01, PS-02, DV-02, A2-31, PS-25}. Only KD-01's primary core enters, via PS-25. The family size stays fixed at m = 5 even if the tie-break drops A2-31 or PS-25, so Holm stays conservative and no trial is hidden. The final m and members are journaled.
- **A2-31 vs PS-25 tie-break (>70% trade overlap).** Decided on dev data only, by net expectancy after A2-40/A2-59 costs, and frozen at sign-off. This is a P&L-based choice and is declared as such.
- **Primary degree selection.** Score = |forward coverage − in-sample coverage| at ±2σ, averaged over the dev walk-forward folds. The lowest score wins, ties go to the lower degree, and d ≤ 3. Fixed d = 1 stays as the sensitivity arm.
- **Status.** Superseded by rev 3 (Appendix F read, §5) and rev 3.1 (§6).

## 5. Rev 3 — Appendix F rulings (Analyst 2, 2026-10-22; source `kindle-notes/keller-b5-dvp-analyst2.md`, App. F loc ~7587, Part Eight)

1. **Weightless confluence (replaces the wPoc/wVaEdge [GAP]).** M-DVP's confluence score is the number of distinct **families** {manifold, sigma, lattice} agreeing within confTol — families, not lines — and a qualifying confluence requires the **third family** (8.3 rule 3; census, loc 665–710: 2 families 78.8% / 78.7% vs 1 family 79.1%). Within a family all rails weigh equally, **1.0/1.0** (POC/VA edge). **Sensitivity (report-only, no trial spent, not in the Holm family):** POC 0.77 / VA edge 1.00 (commercial weights in the App. E code header; App. F itself issues no new values). **Never use 1.00/0.80:** the book retracts it in App. F and the census contradicts it — one-bar holds are **88.9–89.5% for VA rails vs 73.4–73.5% for the POC** (5.1). The one-bar hold is not a trading outcome, so the census only shows the old weights are wrong; it does not supply correct ones.
2. **8.3 operating rules tagged.** Rules **1–4: census-supported**. Rules **5–7: hypotheses** (the author's inference; incl. rule 5 "every rail a destination, every acceptance the entry" and rule 7 "witnesses disagree / stand aside"). KD-01 (rule 5) and KD-03 arm C (rule 7) therefore rest on a hypothesis, not on census.
3. **8.1 supports acceptance-primary.** Held exhibits post MFE/MAE **0.24–0.67**; accepted exhibits **8.9–47**, with nothing in between, in either market (selected exhibits, grade B; the population-level falsifier is App. D item 1). This supports keeping KD-01 (acceptance) as primary and KD-02 (held/fade) as canary.
4. **8.2 buy/sell split = bar-shape proxy.** The manifold's buy/sell split is a bar-shape proxy (Book Four's delta_proxy), not order flow; this confirms the **"no true delta"** label. The published App. E alerts **must not be ported**: they fire on rail touches, i.e. on held-family events.
5. **Errata.** XAU/XAG prices are printed at **1/100** of the dollar price on **93 of 300** exhibits (App. F item 2; e.g. gold 11.53 = $1,153). ATR- and σ-based statistics are unaffected; any price quoted from a metals exhibit must be ×100.
- **Unchanged from rev 2.1:** Holm m = 5 family (α fixed in rev 3.1, §6.1 item 7), A2-31/PS-25 tie-break, primary degree score, and the KD→catalogue mapping.

## 6. Rev 3.1 — KD-06..KD-11 and review fixes (Analyst 1 and Analyst 2, 2026-10-22)

Folds in the former addendum (`specs/keller-b5-rev3.1-addendum.md`, now superseded). Repo note: `DvpConfig`, `_DvpEngine`, `_SbxHost` and the `_SbxE/G/S_KD` files exist only in specs, not in `MultiCharts.Net`.

### 6.1 Agreed rulings (Analyst 1 and Analyst 2)
1. **Config: no new `DvpConfig` fields.** `confMode` (family count), `minFamilies` (= 3) and `reportWeights` (VA 1.00 / POC 0.77, journal-only; logged in the config hash) are **constants**. The row floor is **fixed at one tick** (`rowWidth = max(σ-row, tick)`, effective `manRows` journaled). **No `rollAdjust`.** No new `[Input]`s.
2. **Sweep.** The frozen KD-01 **18-cell** sweep (§3 KD-01) is unchanged. A separate, labelled **σ2/node robustness block** of **6 cells**: rail {σ2 (KD-06), node (KD-10)} × N {2,3,5} × exit {EX-05 trail + 41-bar time stop}. These cells are outside PS-25, are journaled as trials and can never become the primary. **Dropped:** the KD-06 +1R partial arm and the KD-10 next-rail target. **PS-25's single primary cell must be named before any dev data is read.**
3. **Rolls.** Rev 3's skip rule (`crossesRoll`, §3 common; roll bar ±1 excluded via `IsRollWindow`) stays. Journal the **share of skipped fit windows per stream**; any stream skipping **> 30%** is **diagnostic-only**. Back-adjusting is **report-only, after G0-c**, and never feeds Holm. Orders/fills/costs stay on the actual contract.
4. **Stand-ins.**
   - KD-10 "far outside value" = **|z_node| ≥ 2 [GAP stand-in]**; report splits {2–3, ≥ 3} (report-only).
   - KD-09 tolerance **fixed at 0.3 ATR** (replaces the confTol {0.2,0.3,0.5} sweep for KD-09). The **0.3–1.0 ATR disagreement band is a [GAP]**, pending the 7.1–7.4 re-read.
   - KD-09 requires **≥ 2 lattice ranges inside its own module only**; the frozen engine family rule (≥ 1 range, §3 KD-03) is unchanged and `nRanges` is journaled.
   - KD-10 held adverse range = **3.2–14.6 ATR** (Analyst 2's tally, not the book's; Analyst 2 entries L110 "15" corrected).
5. **KD-10 vs A2-46:** report trade overlap descriptively. **No P&L pick**, nothing dropped (same for KD-06 vs VW-03).
6. **Holm:** m = 5 unchanged, {PS-01, PS-02, DV-02, A2-31, PS-25}. The hidden-trial exclusion list (§4) stays out of Holm.
7. **Holm α (C7):** family-wise α = 0.05 (Holm) across m = 5, fixed before any dev read.
8. **Degree (C1):** `degMode = DevCoverageFrozen` (the coverage-match score, dev only, frozen at sign-off); `degMax = 3`; d = 1 sensitivity arm; BIC report-only (§2.2–2.3).
9. **KD-02 kill (C7):** net PF > 1.0, with no 1.0–1.1 zone (§3 KD-02).
10. **Futures (C6c/d):** C6c covered by the one-tick floor (item 1). C6d: journal the RTH share of volume per fit window; report results split by RTH share {<50%, ≥50%}; report-only; ETH stays primary (§3 common, §2.9).
11. **Data caveat (C8):** 'FX/crypto only · no true delta · ES prior 0' on every KD row (§3, §6.2).

### 6.2 Entries KD-06..KD-11 (as amended) — FX/crypto only · no true delta · ES prior 0
| ID | Kind | Placement | Rev 3.1 rules | Holm |
|---|---|---|---|---|
| KD-06 | entry | KD-01 robustness arm σ2 | Analyst 2 entry rules; exit EX-05 + 41-bar time stop only; KD-04 stand-down off for this arm (pre-registered) | no |
| KD-07 | gate | KD-02 canary gate form | ablation on fade hosts only; **never layered on a Holm member** (not on DV-02) | no |
| KD-08 | study | degree-score diagnostic | cone-ban "re-open" trigger ⇒ new pre-registered exploratory trial outside m = 5 | no |
| KD-09 | gate | KD-03 rev 3.1 module | ≥ 2 ranges in-module; tol 0.3 ATR; 0.3–1.0 band [GAP]; re-specifies a rule-7 (C) hypothesis; arm B (≤ 1) contrast kept | no |
| KD-10 | entry | KD-01 robustness arm node | |z| ≥ 2 [GAP stand-in]; EX-05 + 41-bar time stop; overlap with A2-46 reported | no |
| KD-11 | study | A2-33 / G0-d extension | "far nodes as destinations" needs a new pre-registration; never touches PS-25's primary | no |

### 6.3 Open question for Ettienne — acceptance default N
Rev 3 set KD-01's acceptance N to 3 (a [GAP] substitute); rev 3.1 writes **N = 2** into §3 KD-01 as the recommended primary, pending Ettienne. Analyst 2's original review (`keller-b5-review-analyst2.md` L17, item C2) asked for **N = 2 as primary with {3, 5} in the sweep**, to match the A2 acceptance convention (A2-08/A2-11). That edit was meant for rev 2 and was **missed**; it is not a new proposal. **Recommendation (both analysts): N = 2 primary; sweep stays {2, 3, 5}; δ = 0.10 ATR unchanged. Ettienne to confirm.** Either way the choice is fixed before dev data is read; switching after seeing results would be a hidden trial. The band-break persistence `m` (KD-04 stand-down) stays at 3 bars [GAP].

### 6.4 Hidden-trial exclusion list (copied from Analyst 1's review §8; all outside Holm, m = 5)
1. Choosing the best of the KD-01 cells → name PS-25's single primary cell before any dev read.
2. KD-06 +1R partial arm → deleted.
3. σ2/node arms (6 cells) → labelled robustness, ineligible for primary.
4. KD-09/KD-03 arms + confTol sweep → the filter never modifies the primary unless pre-registered before the read.
5. KD-07 veto on DV-02 (a Holm member) → excluded.
6. P&L "keep the better one" rules for VW-03 (Analyst 2 entries L57) and A2-46 (Analyst 2 entries L124) → overlap reported, no P&L drop. The only declared P&L choice stays the A2-31/PS-25 tie-break.
7. KD-11 → destinations and KD-08 → cone re-open → new pre-registration, outside m = 5.
8. Default N (N = 2 recommended vs 3) → fixed before results (see §6.3).
9. Report-only arms (RTH, unadjusted twin / back-adjusted arm, t−2 endpoint drift, degree-1 twin, 0.77 weights, |z| ≥ 3 bucket) → never gate or select; journaled as such.

### 6.5 KD batch placement
The KD batch runs **after Batch 2**; Batch-1/2 slot assignments are unchanged.
- **`_Test1`:** studies and canary — KD-08 and KD-11 (`Mode = EventStudy`, KD-11 shares the A2-33 run), KD-02 canary with KD-07 gate (`Mode = Canary`).
- **`_Test2`:** KD-01 (frozen 18 cells + 6-cell robustness block) plus the **KD-09 gate** (`GateModule = "KD-09"`).

### 6.6 Appendix A — Analyst 2 review C-items: rev 3 miss list and rev 3.1 resolution
| Item | Ask | Status in rev 3 (lines = /tmp/kb5-rev3.md) | Lines | Rev 3.1 |
|---|---|---|---|---|
| C1 Degree | degMax 3; coverage-selected primary; d = 1 sensitivity; BIC report-only | **Partly landed.** Ruling landed (rev3 L205), but §2.2 config still reads `degMode=AutoBic; degMax=4` | rev3 L74 vs L205 | Applied §2.2/§2.3 (DevCoverageFrozen; degMax 3; d=1 sens.; BIC report-only) |
| C2 Acceptance N | N = 2 primary, {3,5} swept | **Missed** (N = 3 default). δ and 41-bar symmetric clock landed | rev3 L141, L147, L140, L145, L152 | N = 2 written as recommended primary; sweep {2,3,5}; open for Ettienne §6.3 |
| C3 One-bar hold inadmissible | relabel KD-03 "C(+) one-bar only, inadmissible" | **Missed** (label still "C (+) for one-bar hold"); A2-33 matched-control replication landed | rev3 L167, L171 | Applied §3 KD-03 grade |
| C4 Trial accounting | no separate family; KD-01→PS-25 etc. | **Landed** | rev3 L203, L215 | — |
| C5 Overlaps | cite A2-31/32/25/26/27/28/29/30/33 | **Partly landed.** A2-31 (tie-break) and A2-33 (G0-d) cited; A2-25..30, A2-32 not cited | rev3 L153, L204 | Overlap lines added §3 KD-01; no P&L picks |
| C6 Futures | (a) back-adjust roll; (b) ETH primary/RTH sens.; (c) tick-floor rows; (d) time-of-day volume confound | (a) **Not adopted by ruling** (skip kept; confirmed here §1.3); (b) **landed**; (c) **missed** in rev 3 (fixed by this addendum §1.1); (d) **missed** | rev3 L132, L97 | (c) one-tick floor §2.2; (d) RTH-share journal + {<50%, ≥50%} split, report-only §3 |
| C7 Kill rules | pre-register Holm α + family; placebo; costs; KD-02 band without a 1.0–1.1 gap | **Partly landed.** Family, placebo, costs landed; **α not stated**; KD-02 still undefined between PF 1.0 and 1.1 | rev3 L152, L161, L203 | Holm α = 0.05, m = 5, pre-dev; KD-02 kill PF > 1.0 §3 |
| C8 Data caveat | FX/crypto only, no true delta, ES prior 0 on every KD row | **Partly landed** (common line + §2.8/§5.4, not per row) | rev3 L132, L121, §5 item 4 | Added to every KD row §3, §6.2 |
| C9 [GAP]s / freeze after App. F | freeze only after App. F | **Landed** | rev3 §5, change log | — |
| Agree-3 | add to §2.7: manifold re-maps every bar; only its t−1 snapshot is tradeable | **Missed** | rev3 L114–115 | Applied §2.7 |

All missed items (C1 config line, C2, C3, C5, C6c/d, C7 α and KD-02 band, Agree-3) are applied in rev 3.1; C2 awaits Ettienne's confirmation.

## Change log
- **rev 3.1 erratum (23 Oct 2026, Analyst 2 review of catalogue v2.3):** §4 and the ID-scheme note now match the applied catalogue mapping (KD-01 → PS-25, KD-03/04 → RG-09/10 as RG modules on KD-01, KD-05 → MT-07, KD-02 canary only with no trial, no PS-26). KD-09 replaces KD-03 arms A and C (confTol fixed at 0.3 ATR); arm B stays as KD-09's contrast; the KD batch runs only KD-09. No rule or statistic changed.
- **rev 3.1 (2026-10-22, reviewed by Analyst 1 and Analyst 2, awaiting Ettienne's sign-off):** Folded in the rev 3.1 addendum (§6: rulings, KD-06..KD-11, open N question, hidden-trial list, batch placement, Appendix A miss list). Applied Analyst 2's fixes: C1 `degMode=DevCoverageFrozen`, `degMax=3`, d=1 sensitivity, BIC report-only (§2.2/2.3); C2 N=2 recommended primary, sweep {2,3,5}, open for Ettienne (missed rev 2 edit); C3 KD-03 grade relabelled inadmissible; C5 A2 overlap lines; C6c one-tick floor, C6d RTH-share journal/split (report-only); C7 Holm α=0.05 over m=5 pre-dev, KD-02 kill PF>1.0 without 1.0–1.1 zone; C8 caveat on every KD row; §2.7 manifold-repaint sentence. Rev 3 backup: `/tmp/kb5-rev3.md`.
- **rev 3 (2026-10-22, FROZEN):** Applied Analyst 2's Appendix F rulings (§5): weightless 3-family confluence replaces wPoc/wVaEdge [GAP] (0.77/1.00 report-only; 1.00/0.80 banned); 8.3 rules 1–4 census / 5–7 hypothesis; 8.1 acceptance-primary note; 8.2 proxy/no-alert-port note; metals 1/100 erratum. Removed "not frozen until Appendix F" caveat. Rev 2.1 rulings unchanged.

- **rev 3.1 sign-off (23 Oct 2026):** Ettienne signed off rev 3.1 and confirmed acceptance N = 2 as primary (sweep {2,3,5}); §6.3 question closed.
