# Keller B5 Parts 6–7: testable entries (addendum to M-DVP rev 2.1)

**For:** Ettienne Hugo · **By:** Grok Bot (Analyst 2) · **Date:** Thu 22 Oct 2026, 23:20 EDT · **Status:** draft for review. This is documentation only: no code was changed and nothing was pushed.
**Inputs:** `kindle-notes/keller-b5-dvp-analyst2.md` (Run 2: Part 6, 7.1; Run 3: 7.4 tail, 7.5; Run 4: Appendix F, Part 8, Appendices A–E), `specs/keller-b5-catalogue-additions.md` (rev 2.1, KD-01..05, Holm family), `specs/sandbox-entries-feasibility.md`, `specs/missing-functionality-specs.md`, and the catalogue (A2-08/11 acceptance convention, A2-26..33, A2-40/59).

**Evidence rule.** Every number below is a FX/metals/crypto number. The book has **no futures or index data, so the ES prior is zero**. Numbers from exhibits come from charts the book selected for decisive outcomes, so they are grade E. Numbers from the census are grade C for ES (different asset class). The 8.3 operating rules are tagged as follows:
- **Rules 1–4 are census-supported (A):** band on the residual and no trust forward; lowest degree for projection; count families and require the third; do not fade the band.
- **Rules 5–7 are author hypotheses (B/B/C):** every rail a destination and every acceptance the entry; "held" means one bar only; stand aside where witnesses disagree.

**Appendix F resolved (steer 22 Oct).** Appendix F retracts the published weights wPoc = 1.00 and wVaEdge = 0.80 and issues no replacement; the code keeps them only so it reproduces the book. In this addendum, **confluence = an unweighted count of distinct families** {manifold, sigma, lattice (≥ 2 distinct ranges)}. A node counts only when the count is ≥ 3 (rule 3). As a **report-only** sensitivity we also journal a weighted score with wVaEdge/wPoc = 1.00/0.77 (VA edge over POC, from the E tooltip ratio 0.77). That score never gates a trade. The "unfrozen until App F" hold is lifted for this addendum. The remaining [GAP]s below must be closed or accepted by sign-off.

**Not ported:** the reference script's alerts. They fire on rail touches, which are held-family events (8.2), so they are excluded.

---

## 0. Common rules (all entries)

**Causality.** All rules use closed bars only. The fit, σ, manifold, rails and lattice used at bar t are frozen from the window t−n…t−1 (rev 2.1 §2.6). ATR is the value known at t−1. Pivots count from their confirmation time. A rail compared with a later bar's close is re-read from that bar's own t−1 fit; it is never redrawn. Shuffled-future and truncation unit tests are required (rev 2.1 §2.6.5).

Notation:
- z_t = (C_t − ŷ_{t−1}(u_t)) / σ_{t−1}
- rail_t(k) = ŷ_{t−1}(u_t) + k·σ_{t−1}
- δ = 0.10·ATR_{t−1}

**Acceptance** (catalogue convention, A2-08/A2-11): **2 consecutive closed bars** beyond rail + δ, each judged against its own frozen rail, with no close back inside by more than δ. KD-01's N ∈ {2, 3, 5} sweep stays; **N = 2 is the default for the new arms**, so they match A2-08/11.

**Futures specifics**
- **ES ETH is the primary session** (the book fits continuous bars). An RTH-only variant is a sensitivity arm.
- **Rolls are back-adjusted** (difference method) for the fit, σ and manifold, so windows may span a roll. This **replaces rev 2.1's `crossesRoll` skip** and needs Analyst 1's agreement; see GAP-6. Orders, fills and costs stay on the actual contract. The roll bar is ±1 excluded from triggers (A2-39 / `IsRollWindow`). Each result reports one control: the unadjusted-with-skip twin.
- **Rows are σ-sized with a 1-tick floor:** row height = max(2·manSpan·σ/manRows, 1 tick). When the floor binds, manRows is reduced to ⌊2·manSpan·σ/tick⌋ and the row count is journaled.
- **Costs:** G0-a (A2-40 + A2-59). Stops are floored at the A2-40 cost floor and at 1 tick.
- Every stop/target price is rounded to the tick, away from the entry for stops and toward the entry for targets.

**Cone.** The cone is never a stop or target in any entry (rule 1; breach median bar 7; whole-projection containment 5.3–5.4%). It is diagnostic only (KD-08).

**Sandbox.** Rules follow the `IEntryRule` / `IGateModule` pattern in `_SbxE_KD.Function.CS`, `_SbxG_KD.Function.CS` and `_SbxS_KD.Function.CS`, with `Needs = Profile|PriceAction|Mtf`. The engine is enabled only through the protected `DvpConfig` (`enabled = true`), plus `PriceActionConfig.enabled` for lattice pivots and `LevelStudyConfig` for diagnostics. **No new `[Input]`s:** `Idea`, `Mode` and `Variant` already exist on `_SbxHost`.

---

## 1. Entries

### KD-06 · σ-band acceptance breakout (merged → **KD-01 arm "σ2"**)
- **Source:** 6.1 (#25 held: fav 2.3–2.5 vs adv 4.5–5.65 ATR), 6.2 (#26 accepted 5.5–10.8 fav / ≤ 0.6 adv), 6.4 (#28 break kept going; accepted 4.0–6.5 / ≤ 0.8), 8.3 rule 4 (A) and rule 5 (B hypothesis). Grade: E (exhibits); setup untested; ES prior 0.
- **Rules (long; short mirrored):**
  - *Trigger:* C_t > rail_t(+2) + δ, and C_{t−1} ≤ rail_{t−1}(+2).
  - *Acceptance:* C_{t+1} > rail_{t+1}(+2) + δ. The two closes t and t+1 make N = 2.
  - *Filters:* `sigmaAtr` ≤ 4. **KD-04 band-break stand-down is off for this arm.** The break is the signal; the old curve is not used for the target.
- **Entry:** buy-stop 1 tick above H_{t+1}, valid for 2 bars.
- **Stop:** max(L_{t..t+1} − 1 tick, rail_entry(+2) − 0.5·ATR), floored per A2-40.
- **Exit:**
  - The target is **not** a cone edge or a forward σ rail.
  - Primary: EX-05 trail, or a 41-bar time stop.
  - Arm: a +1 R partial, then the trail.
  - Invalidation: a close back < rail(+2) − δ.
  - Rule 6 of the daily process: after acceptance the window curve is stale. The **refit from the next bar** (normal rolling fit) is used only for invalidation.
- **Engine:** M-DVP (fit + band only; manifold and lattice not needed).
- **Catalogue mapping:** this is the event-rail = ±2σ arm of **KD-01** (rev 2.1 sweep "event rail" gains the value σ2). It overlaps **VW-03 / A2-10** (VWAP σ-band breakout) in logic, but uses a different curve. If trade overlap > 70%, report it and keep the better one on dev only.
- **Holm:** **not a new trial.** It is a robustness arm of PS-25/KD-01. m stays 5.
- **Kill:**
  - population accepted median MFE/MAE (40 bars) < 3 (8.1 falsifier); or
  - no lift vs K-RT random-time entries or vs ShiftLevels ±0.5σ; or
  - no lift vs the flat-σ twin (VW-03 on the same bars); or
  - net expectancy ≤ 0 after G0-a; or
  - endpoint-drift sensitivity: the result vanishes when rails come from the t−2 fit.
- **Slot:** the KD batch host, `_Test2` (see §3). `Idea = "KD-01"`, Variant cell σ2.

### KD-07 · No-fade veto (module; merged → **KD-02 canary + gate**)
- **Source:** rule 4 (A: POC reached 51.8/51.3%, median 19 bars), 6.1 (stop at 3σ sits inside the adverse leg), 6.2 ("no σ multiple at which adverse < favourable"), 6.3 (OU half-life ≈ 13–14 bars > the extension).
- **Rule:** `IGateModule.Allow` returns false for any **counter-direction** entry from any catalogue idea when |z_t| ≥ 2 on the frozen fit and the entry's side opposes sign(z). Examples are a short when z ≥ +2 and a long when z ≤ −2. The veto stays on until |z| < 1 or a refit window has passed (winLen/4 bars).
- **Stop/exit:** n/a (gate).
- **Engine:** M-DVP (band).
- **Mapping:** this is the gate form of **KD-02**, which stays the G0-b canary with its own rules unchanged. It applies as a layer to the fade/reversion ideas (PS-03, A2-64, A2-51, DV-10) only as an **ablation**.
- **Holm:** no. It is a robustness/ablation module.
- **Kill:** if host fades **without** the veto beat the vetoed version on net expectancy over dev folds, the veto is dropped. The KD-02 canary keeps its own PF > 1.1 ⇒ "bug first" rule.
- **Slot:** `_Test1` (`Mode = Canary`, `Idea = "KD-02"`); module id `GateModule = "KD-07"`.

### KD-08 · Forward-cone diagnostic (study, not a trade)
- **Source:** 2.2 / 6.5 / rule 1 (A): forward ±2σ coverage 28.2/27.7%; whole-projection containment 5.4/5.3%; median breach bar 7. Drill 6.2 (mean |residual| vs bars forward).
- **Rule:** for every fit at t−1, project ŷ with degree d* **and** degree 1 (rule 2) for h = 1..60. Journal the following:
  - whether the bar is inside ±1/2/3σ·√(1+h(u));
  - the first-breach bar;
  - mean |z| by h.
- **Output:** coverage by h and degree, the breach-bar distribution, and the ES-vs-book comparison.
- **Engine:** M-DVP (fit + leverage).
- **Mapping:** feeds rev 2.1's degree-selection score (|fwd − in-sample cov|). It adds no ID to the trade list.
- **Holm:** no (diagnostic).
- **Kill / action:** if the ES median breach bar is > 20 **and** forward ±2σ coverage is > 80%, re-open the "cone is never a target" ban with a pre-registered test. Otherwise the ban stands.
- **Slot:** `_Test1`, `Mode = EventStudy`, `Idea = "KD-08"`.

### KD-09 · Family-count confluence filter (module; **replaces KD-03 arms A/C**)
- **Source:** 7.1 (#32 one range: 2.85/4.95 FX, 2.7/10.2 crypto, ratio 0.58/0.26; #33 several ranges ≈ 0.64/0.56). 7.2 / rule 3 (A: third family +7.4/+6.3 pts one-bar hold; a second adds 0). 7.4 / rule 7 (C: #38 disagreement adverse 10.3/11.2 ATR, ratio 0.32/0.23, seven exhibits). Appendix F (weights withdrawn).
- **Rules** (evaluated on the event rail of the host entry, at t−1):
  1. Lattice counts as a family only if **≥ 2 distinct anchored ranges** name a price within 0.3·ATR of the rail. Several lines from one range count as 0 additional witnesses.
  2. `fam` = the count of {manifold rail, σ rail, lattice} within 0.3·ATR. Manifold + σ alone = 2, which is "no better than one".
  3. **Pass** if fam ≥ 3.
  4. **Disagreement veto (#38):** veto if the lattice (≥ 2 ranges) names a price within 0.3–1.0·ATR of a manifold rail but not within 0.3·ATR. "Geometric vs volume disagree" is the worst case.
  5. **Report only:** the weighted score with 1.00/0.77 VA/POC. The retracted published 1.00/0.80 is never used, not even as a report.
- **Direction:** none. Confluence does not choose direction or change geometry (7.2/7.3 B). It may be applied **only after acceptance** (KD-01 family).
- **Engine:** M-DVP incl. lattice + confluence (PRICEACTION pivots at confirmation).
- **Mapping:** supersedes KD-03 arms A and C. KD-03 arm B (≤ 1 family) is retained as the contrast. The KD-03 ID is kept, with this text as its rev 2.2 rules; KD-09 is an alias for traceability. Its relation to **A2-26** (KDE veto on flat/bimodal POCs) is complementary: both may run as gates, reported separately.
- **Holm:** no. It is a filter/robustness module on PS-25/KD-01.
- **Kill:**
  - no lift over a random-subsample filter with the same pass rate; or
  - population #38 40-bar MAE not larger than all three-family MAE (the rule-7 falsifier); or
  - the A2-33 `DvpConf` |t| < 2.
- **Slot:** `GateModule = "KD-09"` on the `_Test2` KD host.

### KD-10 · Far-outside / band-edge node: acceptance only (merged → **KD-01 arm "node"**)
- **Source:** 7.5 (#39 node on band edge, #40 far outside value) and 7.4 tail ("double reference ≠ double strength"; adv 4.1–4.6 vs fav 2.45–3.3).
  - My tally of 15 distinct exhibits: **held: fav 0.6–3.2 (to 5.5), adv 3.2–15 ATR; accepted (4): fav 3.8–9.9, adv 0.5–1.7**.
  - These are single-chart exhibits and the corpus is FX/crypto only, so the **ES prior is 0**. The far-outside margin is unpublished.
- **Event set:** an HVN/LVN or confluence node whose residual |z_node| ≥ max(|z_VAedge| + 0.5, 2.0) (#40). The #39 band-edge case is |z_node − (±2)| ≤ 0.3·ATR/σ.
- **Rules (long beyond an upper node; short mirrored):**
  - **No bounce entry ever**: the touch-fade side is vetoed by KD-07 + KD-09.
  - *Trigger:* C_t > node_t + δ with C_{t−1} ≤ node_{t−1}.
  - *Acceptance:* C_{t+1} > node_{t+1} + δ (N = 2).
  - *Entry:* buy-stop 1 tick above H_{t+1}, valid 2 bars.
- **Stop:** max(L_{t..t+1} − 1 tick, node − 0.5·ATR).
- **Exit:**
  - Target = next frozen rail beyond (if any within 14·ATR), else EX-05 trail.
  - Time stop 41 bars.
  - Invalidation: close < node − δ.
- **Engine:** M-DVP full (manifold nodes + lattice).
- **Mapping:** event-rail arm of **KD-01**. It is near **A2-46** (volume-gap commitment to the far node) and **#23 / LVN crossed quickly**. If the overlap with A2-46 is > 70%, report it and keep one on dev only.
- **Holm:** no. It is robustness only, and frequency is expected to be low.
- **Kill:**
  - fewer than 30 dev events (then it is report-only); or
  - population accepted median MFE/MAE < 3; or
  - no lift vs ShiftLevels / K-RT; or
  - G0-d `DvpHvn` / `DvpLvn` |t| < 2.
- **Slot:** `_Test2`, `Idea = "KD-01"`, Variant cell "node".

### KD-11 · Beyond-value node reach-rate study (Field Drill 7.5; diagnostic)
- **Source:** 7.4 tail / 7.5 (C hypothesis: the corpus does not measure reach; forward |z| > 3 occurs ~60% of the time, so the σ label overstates distance) and Drill 7.5.
- **Rules:** at each fit (t−1), list every node outside the 70% VA. For each, record the following:
  - z distance;
  - ATR distance;
  - whether **any closed bar's high/low touches** the node's frozen-at-creation price within **40 bars**;
  - bars-to-touch.
- **Baseline:** P_touch under normal residuals, where first passage of AR(1) with ρ is estimated on the window. The plain Gaussian reflection 2·(1 − Φ(d/(σ√h))) is reported as a second baseline.
- **Matched control:** random prices at the same residual distance (A2-33-style; 20 controls at 0.9–1.1×).
- **Output:** reach rate vs both baselines by |z| bucket {1–2, 2–3, > 3}, plus a Kaplan–Meier curve.
- **Engine:** M-DVP + **A2-33 harness** (`_LevelStudy`, new kinds `DvpHvn` / `DvpLvn` / `DvpConf`).
- **Mapping:** extends A2-33 / G0-d. No trade.
- **Holm:** no (diagnostic).
- **Use:** if the reach rate ≫ baseline, far nodes may be used as KD-01 **destinations** (rule 5, hypothesis). This never makes them entries.
- **Slot:** `_Test1`, `Mode = EventStudy`, `Idea = "KD-11"` (shares the A2-33 run).

---

## 2. Mapping summary

| New | Kind | Merged into | Holm | Slot / Mode |
|---|---|---|---|---|
| KD-06 | entry | KD-01 arm σ2 (≈ VW-03/A2-10 logic) | no (robustness) | _Test2 / Strategy |
| KD-07 | gate | KD-02 (canary) gate form | no | _Test1 Canary + GateModule |
| KD-08 | study | rev 2.1 degree score | no (diagnostic) | _Test1 EventStudy |
| KD-09 | gate | KD-03 rev 2.2 (replaces arms A/C) | no | GateModule on _Test2 |
| KD-10 | entry | KD-01 arm node (≈ A2-46) | no (robustness) | _Test2 / Strategy |
| KD-11 | study | A2-33 / G0-d extension | no (diagnostic) | _Test1 EventStudy |

**Holm family unchanged:** {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5. None of these entries replaces a member.

## 3. Slot and config plan
- `_Test1` and `_Test2` here mean the **KD batch**, scheduled after Batch 2. The Batch-1/2 `_Test1/_Test2` assignments stay as they are. Running G0 + KD studies in `_Test1` reuses the Gate-0 host.
- **Protected config, set by the slot (no `[Input]`):**
  - `DvpConfig { enabled = true; streams = {1}; winLen = 240; degMode = DevCoverageFrozen /* rev 2.1; BIC report-only */; degMax = 3; manRows = 64; manSpan = 3; vaPct = 70; nodeProm = 0.12; useFib = true; fibRanges = 3; confTol = 0.30; confMode = FamilyCount; minFamilies = 3; reportWeights = {1.00/0.77}; rowFloorTicks = 1; rollAdjust = BackAdjusted; journalFits = true; journalRails = true }`
  - `PriceActionConfig.enabled = true` (pivots 20/40/80)
  - `LevelStudyConfig.enabled = true` (KD-11)
  - `RollDetection` on
- **New `DvpConfig` fields** (`confMode`, `minFamilies`, `reportWeights`, `rowFloorTicks`, `rollAdjust`) are **protected-config additions to the M-DVP spec, not inputs**. They need Analyst 1 sign-off on rev 2.2.
- **Variant cells (pre-registered):**
  - KD-01: event rail {VAH/VAL, POC, σ2, node} × N {2, 3, 5} × target {next rail, EX-05}. This is 24 cells, over rev 2.1's cap of 18. **Proposal:** fix target = EX-05 for σ2/node, which gives 18.
  - KD-09: arm {fam ≥ 3, fam ≤ 1, veto-only}.

## 4. Open gaps
1. **[GAP] 7.1 exhibits, 7.2, 7.3 and the 7.4 body are not in the notes.**
   - Affected: KD-09 rule 4 (exact #38 definition and the "nearby" distance; 0.3–1.0 ATR is our substitute) and the #36/#37 confluence held/failed population figures.
   - Behaviour #31 is unread. The Appendix B rows for #34–#38 exist, but the chapter text does not.
2. **[GAP]** The "far outside value" margin is unpublished (KD-10 uses VA edge + 0.5σ, min 2σ). The band-break persistence `m` is unpublished, so we use N = 2.
3. **[GAP]** 8.3 daily-process step 5 was not captured.
4. **[GAP]** Appendix D "open items" and the rest of the Appendix C B/C list may run past the last captured page. The Appendix E code body (rails, lattice, node scoring, loc 6945–7290) is unread, so the touch definition and the HVN prominence code are still substitutes.
5. Appendix F has been read. The weights question is closed: we use family count, and weights are report-only.
6. **Back-adjusted rolls vs rev 2.1's unadjusted `crossesRoll` skip.** This is a conflict that needs an Analyst 1 ruling. Back-adjusting changes price levels but not residuals within a window, so σ/z are unaffected; only absolute rail prices shift.
7. **Probe P2** must pass for any 60m/240m stream. **M-DVP, the lattice and the A2-33 DVP level kinds are still unbuilt.**
