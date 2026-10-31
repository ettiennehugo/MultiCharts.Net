---
title: "M-PORT rev 2 risk profiles — decision sheet for Ettienne"
date: 2026-10-29
draft: specs/m-port-spec-risk-profiles-draft.md (v6)
review: specs/m-port-risk-profiles-review-analyst2.md
code: MultiCharts.Net origin/main @ 1dc1abb (M-PORT files byte-identical to 800bce7)
---

# Decisions (5) — all DECIDED by Ettienne Hugo, 2026-10-29

Already decided by you (not reopened): starter = heat 1.5%, 2 positions, 12% vol; daily stop −2% deny / −3% flatten; 2-loss session stop and −3R/−6R weekly throttle per strategy; scaled-book = 4% / 6 / 5-10-15% / 10%; protected config, no [Input], manual switch; per-trade risk ≤ 1%; size never shrunk to fit a stop.

Analysts settled without you: P3 starter drawdown steps 3/6/10%; P4 keep the permanent −8R over 20 trades kill; P7 a loss is R below −0.05 (|R| ≤ 0.05 = scratch); P8 JSON `profile` sets the base.

---

## P1 — How 0.5% per-trade sizing coexists with "never shrink size" and the 14-tick floor — **DECIDED (Ettienne Hugo, 2026-10-29)**
**Decision:** "size from the structural stop; skip if the widened risk is over 1%; caps deny". (Supersedes the v2 recommendation, option b, which sized on the floored stop and so shrank size.)
- n = floor(m × riskPerTrade × book / structural R$), riskPerTrade 0.005.
- The strategy widens the stop to max(structural, 14 ticks) and the trade is skipped if n × widened R$ > 1% of book; n is never recomputed.
- The MM checks heat and cluster caps on the widened risk n × widened R$ and denies; it never trims.
- Schema (checked on origin/main @800bce7, `_PortKeys` L24): only `pm.R$` exists. `pm.R$` stays the structural R$; new optional `pm.R$w` = widened R$ (absent ⇒ = `pm.R$`); `pm.riskNew` = `pm.R$w` × qtyCode. `pm.ver` stays 1 (Analyst 2 agreed, v3 review); the MM and all publishers must ship from the same build.
- Where the 1% skip runs (v5, Analyst 2 v4 review R1/R2): on chart/non-portfolio runs in the strategy (`sizeForRisk`); in Portfolio Trader the strategy arms and the MM skips (journal "per-trade budget"). Under CodeSize an entry with no usable `pm.R$` is now denied ("no pm.R$") instead of passing unchecked. No change to the decision itself.
- Consequence resolved by P2 (c): a lone widened entry up to 1% is no longer denied by the starter cluster cap.
Draft: §C.3, §C.5, §C.6, §D (P1 block), §E, PR-B items 2/2a/2b, PR-B0, tests T-P2, T-P16, T-P18 (incl. iv), T-P19, T-P21.

## P2 — Cluster cap value and "deny instead of reduce" on caps — **DECIDED (Ettienne Hugo, 2026-10-29)**
**Decision:** "P2: apply the cluster cap only when the cluster already holds a position" (option (c), recommended by both analysts).
- Deny-on-breach for heat and cluster caps in both profiles (never trim).
- clusterCap stays 0.75% (starter) / 2% (scaled-book) and is checked only when the merged cluster already holds a position (open, or allowed earlier in the same step). A lone entry is limited by the 1% per-trade skip and heat only; a second same-cluster entry is checked and in practice denied, so one position per cluster (ES and NQ count as one) holds.
Draft: §C.5, §C.7 item 1, §D, §E, PR-B 2/7, tests T-P2, T-P3, T-P18, T-P20.

## P5 — Scope of the 2-loss session stop and the −3R/−6R weekly throttle — **DECIDED (Ettienne Hugo, 2026-10-29)**
**Decision:** "P5: apply in both profiles" — the per-strategy 2-losses/day session stop and the −3R/−6R weekly throttle apply in starter and scaled-book.
Draft: §C.7 4a/4b, §D, PR-C, tests T-P5, T-P6.

## P6 — When the weekly throttles reset, and whether −6R clears by itself — **DECIDED (Ettienne Hugo, 2026-10-29)**
**Decision:** "P6: −6R state clears weekly". Both −3R (half size) and −6R (observation only) clear at the strategy's first session of the next ISO trading week (Sunday-evening ETH belongs to Monday's week). `weeklyObserveReset` default `Weekly` in both profiles.
Draft: §C.7 4b, §D, PR-C 4, test T-P6.

## P9 — What "3 or more uncorrelated live strategies" requires before you switch to scaled-book — **DECIDED (Ettienne Hugo, 2026-10-29)**
**Decision:** "P9: require both counts". Scaled-book is eligible only when `uncorr` (distinct ρ ≥ 0.9 clusters with a live strategy) ≥ 3 **and** `uncorr03` (greedy count, pairwise ρ < 0.3) ≥ 3. The MM only reports; the switch stays manual.
Draft: §5.1a, §E, PR-D, test T-P9.
