**SUPERSEDED BY REV 3.1** — folded into `specs/keller-b5-catalogue-additions.md` (rev 3.1, §6). Kept for reference only.

# Keller B5 — rev 3.1 addendum (KD-06..KD-11) — **DRAFT**

**For:** Ettienne Hugo · **By:** Grok Bot · **Date:** Thu 22 Oct 2026 (America/New_York, EDT) · **Status:** **DRAFT. This addendum applies only after Ettienne signs off rev 3.** It does not edit the frozen rev 3 (`specs/keller-b5-catalogue-additions.md`, cited as "rev3 Lnn"). Documentation only; nothing implemented.
**Sources:** `specs/keller-b5-p6p7-entries-analyst2.md` (Analyst 2, KD-06..11, "add."), `specs/keller-b5-p6p7-review-analyst1.md` (Analyst 1, "rev."), `specs/keller-b5-review-analyst2.md` (C1..C9). Rulings below are agreed by Analyst 1 and Analyst 2.

**Evidence rule.** All book numbers are FX/crypto (exhibits = grade E, census = grade C for ES). **ES prior = 0.** Repo note: `DvpConfig`, `_DvpEngine`, `_SbxHost` and the `_SbxE/G/S_KD` files exist only in specs, not in `MultiCharts.Net`.

## 1. Agreed rulings
1. **Config: no new `DvpConfig` fields.** `confMode` (family count), `minFamilies` (= 3) and `reportWeights` (VA 1.00 / POC 0.77, journal-only; logged in the config hash) are **constants**. The row floor is **fixed at one tick** (`rowWidth = max(σ-row, tick)`, effective `manRows` journaled). **No `rollAdjust`.** No new `[Input]`s.
2. **Sweep.** The frozen KD-01 **18-cell** sweep (rev3 L147) is unchanged. A separate, labelled **σ2/node robustness block** of **6 cells**: rail {σ2 (KD-06), node (KD-10)} × N {2,3,5} × exit {EX-05 trail + 41-bar time stop}. These cells are outside PS-25, are journaled as trials and can never become the primary. **Dropped:** the KD-06 +1R partial arm and the KD-10 next-rail target. **PS-25's single primary cell must be named before any dev data is read.**
3. **Rolls.** Rev 3's skip rule (`crossesRoll`, rev3 L132; roll bar ±1 excluded via `IsRollWindow`) stays. Journal the **share of skipped fit windows per stream**; any stream skipping **> 30%** is **diagnostic-only**. Back-adjusting is **report-only, after G0-c**, and never feeds Holm. Orders/fills/costs stay on the actual contract.
4. **Stand-ins.**
   - KD-10 "far outside value" = **|z_node| ≥ 2 [GAP stand-in]**; report splits {2–3, ≥ 3} (report-only).
   - KD-09 tolerance **fixed at 0.3 ATR** (replaces the confTol {0.2,0.3,0.5} sweep for KD-09). The **0.3–1.0 ATR disagreement band is a [GAP]**, pending the 7.1–7.4 re-read.
   - KD-09 requires **≥ 2 lattice ranges inside its own module only**; the frozen engine family rule (≥ 1 range, rev3 L169) is unchanged and `nRanges` is journaled.
   - KD-10 held adverse range = **3.2–14.6 ATR** (Analyst 2's tally, not the book's; add. L110 "15" corrected).
5. **KD-10 vs A2-46:** report trade overlap descriptively. **No P&L pick**, nothing dropped (same for KD-06 vs VW-03).
6. **Holm:** m = 5 unchanged, {PS-01, PS-02, DV-02, A2-31, PS-25}. The hidden-trial exclusion list (§4) stays out of Holm.

## 2. Entries (KD-06..KD-11, as amended)
| ID | Kind | Placement | Rev 3.1 rules | Holm |
|---|---|---|---|---|
| KD-06 | entry | KD-01 robustness arm σ2 | add. rules; exit EX-05 + 41-bar time stop only; KD-04 stand-down off for this arm (pre-registered) | no |
| KD-07 | gate | KD-02 canary gate form | ablation on fade hosts only; **never layered on a Holm member** (not on DV-02) | no |
| KD-08 | study | degree-score diagnostic | cone-ban "re-open" trigger ⇒ new pre-registered exploratory trial outside m = 5 | no |
| KD-09 | gate | KD-03 rev 3.1 module | ≥ 2 ranges in-module; tol 0.3 ATR; 0.3–1.0 band [GAP]; re-specifies a rule-7 (C) hypothesis; arm B (≤ 1) contrast kept | no |
| KD-10 | entry | KD-01 robustness arm node | |z| ≥ 2 [GAP stand-in]; EX-05 + 41-bar time stop; overlap with A2-46 reported | no |
| KD-11 | study | A2-33 / G0-d extension | "far nodes as destinations" needs a new pre-registration; never touches PS-25's primary | no |

## 3. Open question for Ettienne — acceptance default N
Rev 3 L141 sets KD-01's default **N = 3** (a [GAP] substitute). Analyst 2's original review (`keller-b5-review-analyst2.md` L17, item C2) asked for **N = 2 as primary with {3, 5} in the sweep**, to match the A2 acceptance convention (A2-08/A2-11). That edit was meant for rev 2 and was **missed**; it is not a new proposal. **Recommendation (both analysts): N = 2 primary; sweep stays {2, 3, 5}; δ = 0.10 ATR unchanged. Ettienne to confirm.** Either way the choice is fixed before dev data is read; switching after seeing results would be a hidden trial. The band-break persistence `m` (KD-04 stand-down) stays at 3 bars [GAP].

## 4. Hidden-trial exclusion list (copied from Analyst 1's review §8; all outside Holm, m = 5)
1. Choosing the best of the KD-01 cells → name PS-25's single primary cell before any dev read.
2. KD-06 +1R partial arm → deleted.
3. σ2/node arms (6 cells) → labelled robustness, ineligible for primary.
4. KD-09/KD-03 arms + confTol sweep → the filter never modifies the primary unless pre-registered before the read.
5. KD-07 veto on DV-02 (a Holm member) → excluded.
6. P&L "keep the better one" rules for VW-03 (add. L57) and A2-46 (add. L124) → overlap reported, no P&L drop. The only declared P&L choice stays the A2-31/PS-25 tie-break.
7. KD-11 → destinations and KD-08 → cone re-open → new pre-registration, outside m = 5.
8. Default N = 2 vs N = 3 → fixed before results (see §3).
9. Report-only arms (RTH, unadjusted twin / back-adjusted arm, t−2 endpoint drift, degree-1 twin, 0.77 weights, |z| ≥ 3 bucket) → never gate or select; journaled as such.

## 5. KD batch placement
The KD batch runs **after Batch 2**; Batch-1/2 slot assignments are unchanged.
- **`_Test1`:** studies and canary — KD-08 and KD-11 (`Mode = EventStudy`, KD-11 shares the A2-33 run), KD-02 canary with KD-07 gate (`Mode = Canary`).
- **`_Test2`:** KD-01 (frozen 18 cells + 6-cell robustness block) plus the **KD-09 gate** (`GateModule = "KD-09"`).

## Appendix A — Analyst 2 review C-items vs frozen rev 3
| Item | Ask | Status in rev 3 | Lines |
|---|---|---|---|
| C1 Degree | degMax 3; coverage-selected primary; d = 1 sensitivity; BIC report-only | **Partly landed.** Ruling landed (rev3 L205), but §2.2 config still reads `degMode=AutoBic; degMax=4` | rev3 L74 vs L205 |
| C2 Acceptance N | N = 2 primary, {3,5} swept | **Missed** (N = 3 default). δ and 41-bar symmetric clock landed | rev3 L141, L147, L140, L145, L152 |
| C3 One-bar hold inadmissible | relabel KD-03 "C(+) one-bar only, inadmissible" | **Missed** (label still "C (+) for one-bar hold"); A2-33 matched-control replication landed | rev3 L167, L171 |
| C4 Trial accounting | no separate family; KD-01→PS-25 etc. | **Landed** | rev3 L203, L215 |
| C5 Overlaps | cite A2-31/32/25/26/27/28/29/30/33 | **Partly landed.** A2-31 (tie-break) and A2-33 (G0-d) cited; A2-25..30, A2-32 not cited | rev3 L153, L204 |
| C6 Futures | (a) back-adjust roll; (b) ETH primary/RTH sens.; (c) tick-floor rows; (d) time-of-day volume confound | (a) **Not adopted by ruling** (skip kept; confirmed here §1.3); (b) **landed**; (c) **missed** in rev 3 (fixed by this addendum §1.1); (d) **missed** | rev3 L132, L97 |
| C7 Kill rules | pre-register Holm α + family; placebo; costs; KD-02 band without a 1.0–1.1 gap | **Partly landed.** Family, placebo, costs landed; **α not stated**; KD-02 still undefined between PF 1.0 and 1.1 | rev3 L152, L161, L203 |
| C8 Data caveat | FX/crypto only, no true delta, ES prior 0 on every KD row | **Partly landed** (common line + §2.8/§5.4, not per row) | rev3 L132, L121, §5 item 4 |
| C9 [GAP]s / freeze after App. F | freeze only after App. F | **Landed** | rev3 §5, change log |
| Agree-3 | add to §2.7: manifold re-maps every bar; only its t−1 snapshot is tradeable | **Missed** | rev3 L114–115 |

Missed items (C1 config line, C2, C3, C5, C6c/d, C7 α and KD-02 band, Agree-3) are proposed for rev 3.1 after sign-off; none edits rev 3.
