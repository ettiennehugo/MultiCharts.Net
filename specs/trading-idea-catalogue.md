# Trading-Idea Catalogue (v2.7): Volume Profile, VWAP, Anchored VWAP and Companions

**For:** Ettienne Hugo · **Prepared by:** Grok Bot · **Date:** Tuesday 20 Oct 2026 (America/New_York, EDT) · **v2.4:** Saturday 31 Oct 2026 (EDT), implementation status re-checked against `origin/main` @ `3f9e9be` (PRs #30–#58) · **v2.5:** Saturday 31 Oct 2026 (EDT), adds families VX (volatility) and LE (leveraged ETFs) from R7 `volatility-leveraged-etf-research.md` · **v2.6:** Sunday 1 Nov 2026 (EST), adds family YV (10 ideas from 8 YouTube videos; `specs/video-ideas-2026-11-01-additions.md`) · **v2.6a:** Sunday 1 Nov 2026 (EST), per-contract costs confirmed (ES $17, MES $2.50 per round trip) · **v2.7:** Sunday 8 Nov 2026 (EST), adds PF-08, SP-09, OF-09, OF-10 and EX-13 (`specs/video-ideas-2026-11-08-additions.md`)

**Data / broker:** **dxFeed through MultiCharts .NET is the data source; IBKR is the broker only** (commissions and fills). The data constraints follow `reviews/analyst-base-class-spec.md` **Rev 2.7**: about 2 years of ticks, about 9–10 years of 1-minute bars, about 40 years of daily bars; the bid/ask-traded split is readable only inside the tick window; continuous symbols are **not back-adjusted**.

**Merged (v2, 20 Oct 2026):** (1) catalogue v1 (114 entries); (2) Analyst 2 **Rev 6** (`trading-ideas-analyst2.md`: A2-01…A2-79, Appendix A L2-01…L2-07, Methodology M1…M30), checked against `catalogue-merge-audit-analyst2.md` and `analyst2-handoff-ids.md`; (3) Trader Dale's YouTube strategies DV-01…DV-24 (`trading-ideas-dale-video.md`). Every source ID is either its own entry or "Merged into X" (Appendix A, script-checked). v1 is kept as `trading-idea-catalogue-v1.md/.pdf`. **Update v2.3 (23 Oct 2026):** (4) Keller Book 5 *Detrended Volume Profile & Order Flow* additions **rev 3.1, signed off by Ettienne 23 Oct 2026 (acceptance N = 2 primary)** (`specs/keller-b5-catalogue-additions.md`, with `specs/keller-b5-p6p7-entries-analyst2.md` as amended by rev 3.1): KD-01…KD-11 and the M-DVP engine (§2.4b, §3.16, Appendix A.6, Appendix B v2.3). **Update v2.3b (23 Oct 2026):** implementation statuses re-audited against `MultiCharts.Net` `origin/main` @ `629da0d` (PRs #14–#29); see §2.4a and Appendix B v2.3b.

**Status:** documentation, with per-entry implementation status in §2.4c (v2.4; v2.5 adds 15 new entries and v2.6 adds 10 YV entries, all not started; v2.7 adds 5 entries, all not started). Of 223 entries: **6 implemented** (coded on `main`, none yet run in MultiCharts), **16 partial**, **12 spec'd**, **189 not started**. Nothing here has been tested. Every idea is a **hypothesis to be put through the final-synthesis validation gates**.

**Companion documents:** `final-synthesis.md` (validation gates, costs, specs 1–4); the six research reports (R1 volume-profile-futures, R2 high-winrate-low-RR, R3 orderflow-volume-profile, R4 price-action-confluence, R5 structural-price-action, R6 futures-edges-beyond-volume-profile); `reviews/analyst-base-class-spec.md` and `reviews/base-class-rev2-status.md`; Kindle notes on Keller B1–B4, Dale (VP and Order Flow) and Levy; `trading-ideas-analyst2.md` (Rev 6) with its Kindle notes on Kaufman (TSaM, KCTS) and Minervini; `trading-ideas-dale-video.md`; Keller B5 notes `kindle-notes/detrended-vp-orderflow.md` and `kindle-notes/keller-b5-dvp-analyst2.md`, with `specs/keller-b5-catalogue-additions.md` (rev 3.1), `specs/keller-b5-p6p7-entries-analyst2.md` and `specs/keller-b5-p6p7-review-analyst1.md`.

---

## 0. Read this first: five sentences of honesty

1. **No idea in this catalogue has a measured, cost-inclusive, out-of-sample edge on CME index futures that I can cite.** The strongest evidence is for *behaviours* (destinations, continuation after acceptance, the overnight drift, daily trend/carry), not for any ready-made intraday setup.
2. **The VWAP evidence is mostly negative.** An independent study found the VWAP bounce rate is 50.2% (a coin flip) across 21,541 touches on eight instruments and six anchors. Shuffling the volume left 14 of 14 measurements unchanged. Buying 2 ATR below VWAP and targeting VWAP wins about 70% of the time but has a profit factor of **0.81** (Telonics, Oct 2026). On ES, VWAP trend-following, VWAP reversion and the ±2–3σ band fade all fail after costs, with PF 0.89, 0.79 and 0.78 respectively (RipBacktest, Oct 2026). The "VWAP Holy Grail" paper (Zarattini & Aziz 2023, Sharpe 2.1 on QQQ) collapses to a Sharpe of **0.10 after one tick of slippage** (Telonics, Sep 2026). Because of this, VWAP and AVWAP ideas are framed here as *regime, location and trail tools*, or as conditional setups that must beat those null results. Bounce and magnet ideas are not treated as edges.
3. **AVWAP practice (Brian Shannon: anchors, handoffs, pinches) has no published controlled statistics.** The one controlled test that touches it found bounce rates of 51.14% for an AVWAP anchored at yesterday's high and 52.37% for a monthly anchor, inside the noise (Telonics). Every AVWAP idea here is grade E or Hypothesis.
4. **Your "≥50% at 1:1 / ≥60% at 1:1.5" targets are gross numbers.** Net of costs, break-even is (1+c)/(1+b). At 1:1 with c = 0.10 R, you need **≥55%** to break even. "60% at 1.5 R" implies about +0.5 R per trade and PF ≈ 2.25, which no source supports. A realistic survivor here nets **+0.05 to +0.20 R** per trade (final-synthesis §2.2).
5. **The smooth-equity / beats-the-S&P goal will be met by a *portfolio*, not by any single idea** (§6). Most ideas in this catalogue are expected to die at Gate 3 (event study vs controls), and that is the intended function of the catalogue.

---

## Key takeaways (v2)

1. **Run Gate 0 before any level-based idea.** A2-33 (level persistence vs matched control prices, with A2-39 roll-honest composites) decides which level types may be traded at all. A2-40 + A2-59 (honest costs, unables, shock audit) must be in the base class before any result is read. The VWAP canary suite (VW-08 with A2-24 and DV-10) and A2-53 must come out ≈ 0 or negative. Nothing level-based is evaluated until these have run (§2.6).
2. **The best-documented additions are mechanical clock/volatility systems, not profile setups.** Kaufman's ES tests are A2-49 midday S/R (8,404 trades, gross), A2-56 Momentum Pinball (1,734 trades, gross), A2-63 (748 trades, $8/contract costs but no slippage) and A2-69 (ES/NQ/RU 1990–2016, gross).
   - They are in-sample, carry no or thin costs, and are mostly pit-era or old-contract, so they are graded **C**, not B.
   - Our 2016–2026 data post-dates almost all of those samples, so testing them here is a genuine out-of-sample replication.
   - That is why A2-49, A2-63 and A2-69 enter Batch 1.
3. **Trader Dale's videos add no new evidence.** All 24 strategies are grade E (unaudited journal win rates). Twelve are merged into existing entries, and DV-01 (daily POC first touch) is kept as a control. The one with a clear prior is DV-02, the failed-level flip, which is a continuation entry consistent with the run-through evidence (P7). DV-15 (an EMA20 pullback gated by VWAP slope) is the cheapest to test.
4. **The Minervini additions are exit and risk modules (A2-72…A2-79), not entries.** They are ablated on three fixed hosts: H1 = PS-01/Spec 1, H2 = A2-63 and H3 = A2-09. They are tested only after a host passes its own kill test, and a module is accepted only if it helps ≥ 2 of the 3 hosts. A2-78's operational part (broker-side stops, heartbeat flatten) is required before any live trade, whatever the test shows.
5. **The v1 merge errors are fixed.**
   - Analyst 2's kill criteria are restored for A2-05, A2-06, A2-13, A2-17 and A2-20, plus the A2-01 location-filter kill.
   - The A2-05 delta arm is W-T only again. The A2-14 and A2-17 event study goes back to daily data.
   - Every rescaled grade now shows the original grade next to it.
   - The window splits are reconciled to one program-wide split (§2.0.1). There is one holdout (Jan 2025–Oct 2026) for every 1-minute idea. The tick window uses A2's 250/125/125-session split.
6. **The portfolio thesis is unchanged.** Trend + carry (PF-01) is the core, and intraday survivors are satellites. Two additions: size by ATR with a 12% volatility target (A2-71, now in PF-06), and expect live information ratio (IR) to be about **half** the backtest's (M18). Scale capital only on live evidence (M30).

---

## 1. How to read an entry

Every idea has the same fields:

| Field | Meaning |
|---|---|
| **ID · Name** | Family prefix + number. |
| **Family · Source** | Book/section, report, or *author idea* (mine, unsourced). |
| **Evidence** | Grade A–E or **H** (hypothesis), with a sign: **(+)** supportive, **(0)** null/indistinguishable from control, **(−)** negative after costs. **Lit:** *Indep* = independent tests exist; *Book* = only the books (Keller/Dale/Levy), not independently verified; *Practitioner* = educator/vendor; *Author* = proposed here. |
| **Original grade** | For any idea brought in from Analyst 2 or Dale, the grade the source gave it, verbatim, next to the catalogue grade (§3.12 explains the rescaling). |
| **Markets · TF** | Instruments and bar timeframes (1/3/5/10/15/30/60/120/240 min, D, W). |
| **Data · test window** | The dxFeed data the idea needs and the window it can honestly be tested on (W-M / W-T / W-D, §2.0), plus roll and ETH caveats. |
| **Why it might work** | Who is trapped, forced or mis-positioned, and why price should respond. |
| **Rules** | Filter → Entry → Stop → Target/Exit. All rules are codeable with **no lookahead**: developing or completed data only, pivots used at confirmation time, closed higher-timeframe bars only. |
| **Sweep** | A small pre-registered parameter grid (≤2 thresholds per layer, ≤3 values each; ≤30 configs per spec). |
| **Frequency** | **Author estimate, not measured**, per instrument. Treat it as an order of magnitude only. |
| **Fails when** | The main failure modes. |
| **Needs** | Base features (codes in §2.4), **missing** pieces (marked ✗), and data-quality limits. As of the merge (§2.4a): **✓** = available in the merged code, **◐** = available with a stated gap, **✗** = still missing or deferred. |
| **Test** | Controls / placebo specific to the idea, plus **kill** criteria. The standard controls (§2.3) always apply. |

**Gate (v2)** and **Kill** lines name the Gate-0 prerequisites (§2.6) and the idea-specific kill criteria. Merged ideas are marked **Merged · ID** inside the host entry, with the source text carried verbatim.

**Grades (from final-synthesis §1):** A = peer-reviewed and replicated, or long OOS/live record; B = independent, controlled, large-sample practitioner research; C = the author's own real-data statistics with material limits; D = mechanism-reasoned framework with no statistics; E = anecdote / vendor claim; **H** = hypothesis (my idea, or a book idea extended beyond what the book says).

A family-level grade is **not** a setup-level grade. When a population behaviour is graded C but the specific setup has never been tested, the entry says so explicitly, e.g. "C (family) · setup untested".

---

## 2. Common conventions (apply to every idea unless overridden)

### 2.0 Data source and usable test windows (Rev 2.7: dxFeed via MultiCharts; IBKR is the broker only)

| Code | Data | Usable window (verify the start date per symbol: Rev 2.7 §E2) | What it supports |
|---|---|---|---|
| **W-M** | dxFeed **1-minute** bars | ≈ **Jun 2016 → Oct 2026** (NQ minute data from 17 Jun 2016 per the MC wiki; "about 9 years" in Rev 2.7), ≈ 2,500 RTH sessions per index | Profile, value, VWAP/AVWAP, IB, structure, time-of-day ideas. Completed profiles are exact (MC VP on minute data); the developing profile on 1m bars is **approximate** (VPB-1 parity is checked only inside W-T). **Split (agreed, §2.0.1):** dev Jul 2016–2022, validation 2023–2024, holdout Jan 2025–Oct 2026 (run once), anchored yearly walk-forward inside dev |
| **W-T** | dxFeed **1-tick** Trade + Bid + Ask | ≈ **Jul 2024 → Oct 2026** (NQ ticks from 1 Jul 2024), ≈ **500 sessions** | Order flow at High quality via the **MC-BidAsk** split (readable from code only inside this window; **no aggressor field is exposed**) or our Lee-Ready (Quote). **Split (A2's, adopted, §2.0.1):** first 250 sessions in-sample, next 125 validation, last 125 holdout (≈ the latest 6 months). **Sweeps ≤ 4 configs.** Before Jul 2024, flow is **BarUpDown (Low)**: regime context only, never footprint, imbalance or absorption signals |
| **W-D** | dxFeed **daily** bars | Up to ≈ 40 years depending on product (ES e-mini only since 1997, NQ since 1999, MES/MNQ since May 2019) | Daily rules that need no volume-at-price and no intraday entry (RSI(2) base, TSMOM, carry, volatility regimes). Any rule that needs profiles or intraday entries stays in W-M. **Split (§2.0.1):** dev 2000–2015, validation 2016–2024, holdout Jan 2025–Oct 2026 |

**Rolls (unadjusted continuous `@ES`).** dxFeed continuous data is not back-adjusted, so a roll is a raw price gap.
- **Every multi-day level resets at a roll splice:** composites, naked POCs, prior-week/month VA, multi-day AVWAPs, and swing anchors older than the splice. Invalidate them; don't shift them.
- On ES/NQ (quarterly rolls), a 20-session composite is therefore missing for about the first 20 sessions of each contract, unless the incoming contract's own history (e.g. `/ESH27:XCME`) is loaded to seed it. Expired/individual-contract support is unverified (Rev 2.7 §A1).
- Returns, ATR20d, 200-day SMAs and TSMOM across rolls need roll-gap removal.
- The session after a splice is excluded from every gap idea.

**ETH bars.** Overnight ideas (TS-01, PS-24, TS-06, A2-04, TS-02, TS-05, VW-07) need the 24h CME Globex template. Whether MC assigns it to dxFeed symbols is unverified (Rev 2.7 §E5); check before running them.

**Other markets.** CL, GC and ZN need the NYMEX/COMEX/CBOT bundles (Rev 2.7 §E1), and their dxFeed roll dates are unverified (§E7).

**Execution.** Commissions remain IBKR's. Live slippage is measured as the **IB fill vs the dxFeed signal price** (CS-1 journal). `@ES` → IB front-contract symbol mapping may need a manual re-map at each roll (§E8).

### 2.0.1 Reconciled test-window split (v2): one program-wide split, A2 differences explained

The merge audit found that v1 had silently replaced Analyst 2's windows with its own. v2 keeps **one split for the whole program** and records Analyst 2's original split next to it. The reason is that ideas share hosts, controls and the portfolio holdout. If A2's 2024 were a holdout while 2024 is validation for the A1 hosts on the same days (H1 = PS-01 is both an A1 idea and an A2 module host), the holdout would already have been seen.

| Window | Agreed split (used for every idea) | Analyst 2 Rev 6 original | Reconciliation |
|---|---|---|---|
| **W-M** (1m) | Dev **Jun/Jul 2016 – Dec 2022** (anchored yearly walk-forward inside) · validation **2023–2024** · holdout **Jan 2025 – Oct 2026**, run once | IS Oct 2017 – Dec 2021 · val 2022–23 · holdout 2024–26 (a "~9 years" assumption) | Same as final-synthesis/v1 for validation and holdout. A2's Oct 2017 start was a conservative reading of Rev 2.7's "about 9 years"; the spec's example has NQ minute data from 17 Jun 2016. **Verify per symbol (Rev 2.7 §E2):** if a symbol starts later (MES/MNQ only from May 2019), dev starts at its first full month. A2's 2022 validation year becomes dev, and A2's 2024 holdout year becomes validation. A2-origin ideas keep every other A2 rule: holdout single-use (M9b), alternating random IS/OOS blocks inside dev+validation only (M9a), and > 50% of P&L from one year → kill. |
| **W-T** (ticks + Bid/Ask) | **First ≥ 250 sessions in-sample · next 125 validation · last 125 holdout** (≈ the latest 6 months, run once); ≤ 4 configs | 250 / 125 / 125 sessions | **A2's split adopted.** It refines v1's "dev Jul 2024–Mar 2026, holdout the last 6 months" by adding a validation block. The holdout is still ≈ the last 6 months, as Rev 2.7 asks. No tick-rule or BarUpDown back-fill before W-T counts as order-flow evidence (A2 rule, restored). |
| **W-D** (daily) | Dev **2000–2015** (includes 2008) · validation **2016–2024** · holdout **Jan 2025 – Oct 2026** | "Up to 2000–2026 where the market existed" (no split stated) | Calendar holdout aligned with W-M, so the portfolio holdout is one block. M20 (2008 and 2020 must be in the test) is satisfied by dev (2008) and validation (2020). PF-01 keeps R6's own protocol (2000–2015 design, 2016–2026 holdout). |

**M21 (recent-vs-old t-test) on the agreed split:** older block Jun 2016 – Aug 2021 vs newer block Aug 2021 – Oct 2026 (equal halves of W-M; A2's example split Oct 2017–Apr 2022 vs Apr 2022–Oct 2026).

### 2.1 Sessions, timing, costs (from final-synthesis §4.0)

- **Value** is computed from RTH 09:30–16:00 ET (ES/NQ/ZN). **Levels** may also use full-session (ETH) extremes. CL uses the 09:00–14:30 pit session and GC 08:20–13:30 when "pit value" is specified.
- **Intraday ideas:** no entries 09:30–09:45 unless the idea is an opening idea; no new entries after 15:30; flat by 15:55; news blackout −2/+10 min around tier-1 releases; one live position per instrument; ES and NQ count as **one** correlated position.
- **Fills (simulated on dxFeed data; Bar Magnifier 1-tick inside W-T, 1-minute before it; label which):** stops and market orders slip 1 tick (2 ticks at the open and around news); limits fill only on trade-through; stop-first when a bar touches both stop and target; a 1% tail of 4 R losses is injected; a 2× cost stress test is mandatory.
- **Cost floor:** the minimum stop that keeps costs ≤ 0.10 R is MES 20 ticks, MNQ 35, ES 14, NQ 19, CL 15, GC 16, ZN 13 (IBKR round trip + 1 tick: **ES $17 and MES $2.50 per contract, confirmed by Ettienne 2026-11-01** (v2.6a; MES = $0.25 IBKR commission + $0.35 CME exchange fee + ≈$0.02 regulatory fee per side, plus one $1.25 tick of slippage = 2 MES ticks; ES $17 = 1.36 ES ticks; previously MES $2.47, ES $16.98); MNQ $1.72, NQ $9.48, CL $14.72, GC $15.02, ZN $18.95 remain the R6 figures, unconfirmed). The MES and ES floors are unchanged by the confirmed figures ($2.50 / (0.10 × $1.25) = 20 ticks; $17 / (0.10 × $12.50) = 13.6 → 14 ticks). **Every idea's stop distance is max(structural stop, cost floor):** a structural stop shorter than the floor is **widened** to the floor. The trade is then **skipped (and logged) only if that widened stop breaches the per-trade risk budget** ≤ 1% of equity (decided 2026-10-23; PF-04's 0.5% is not the default for this rule); position size is never shrunk to fit a wider stop.
- **Units:** ATR5 = ATR(14) on 5-minute bars; ATR20d = 20-day daily ATR; δ (proximity) = max(2 ticks, 0.10 × ATR5).
- **Pivots:** fractal N=2 on 1m/5m, N=3 on 15m/60m, N=2 on daily. A pivot exists only from its confirmation bar onward.
- **HVN/LVN:** KDE on completed profiles (bandwidth 6 ticks ES / 20 ticks NQ; prominence ≥10%). In-session HVN/LVN uses the *Own* developing profile only.
- **Delta z:** bar delta standardised against the same clock minute over the prior 20 sessions.
- **Imbalance:** diagonal 3:1 with a 70th-percentile volume floor; stacked = ≥3 consecutive.
- **V-score:** value migration over the last 3 sessions plus the developing POC, V ∈ [−4, +4]; a step counts only if it is ≥ 0.10 × ATR20d.

### 2.2 Definitions used across many ideas

- **Acceptance (beyond level L):** two consecutive closed 5-minute bars beyond L + δ, *or* ≥ 2 thirty-minute TPO periods with close beyond L (Levy's "2 × 60-min TPOs outside VA = alert" is the slower variant). **Rejection:** returning through L within ≤ 2 bars of the break.
- **Balance:** ≥ 3 consecutive sessions whose VAs overlap ≥ 60% (`IsBalanced`), with composite range W = composite VAH − composite VAL.
- **Trend day (live proxy, at 10:30 or later):** price on one side of session VWAP since 09:45 (≤ 1 close through it), VWAP slope over the last 30 minutes > 0.15 × ATR5 per bar in the trade direction, *and* the developing POC has migrated ≥ 0.5 × ATR5 in the same direction since the IB. **Balance day:** ≥ 3 VWAP crosses and |slope| < 0.05 × ATR5 per bar.
- **Significant swing (for AVWAP anchors):** a ZigZag reversal of ≥ k × ATR on the anchor timeframe (k ∈ {2, 3}). The anchor bar is the swing extreme. The AVWAP is computed from that bar forward, but **only becomes usable from the confirmation bar**, the bar where the k × ATR reversal completes. Computing it retroactively uses only past data, so there is no lookahead, provided no signal fires before confirmation.
- **AVWAP σ-bands:** volume-weighted standard deviation of typical price since the anchor.
- **Higher low (HL) entry:** after a pullback, the first confirmed fractal low that is above the prior swing low; entry is a buy-stop 1 tick above the high of the confirming bar.

### 2.3 Standard controls and kill rules (apply to every idea)

| Code | Control | Purpose |
|---|---|---|
| **K-RT** | Random-time entries, same side, same bracket (stop/target in ticks), same session window | Is the *timing* worth anything? |
| **K-FL** | Fake levels shifted ±0.5, 1, 2 ATR5 (and ±0.5 ATR20d for swing ideas) | Is the *level* worth anything? |
| **K-ST** | Stale levels (same construct taken from 5 sessions or 5 weeks earlier) | Is *freshness* worth anything? |
| **K-SH** | Shuffled-flow placebo (permute bar deltas within the session) | Is the *order-flow layer* worth anything? |
| **K-DR** | Drift control: report longs and shorts separately; replicate on CL/GC/ZN | Is it just index long drift? |
| **K-LM** | Limit-at-level vs stop-on-confirmation execution | Is it a fill-selection artefact? |
| **K-RW** | Random-walk yardstick: hit rate 1/(1+b) for a b R target | Is the win rate above chance at all? |

**Standard kill (Gate 3, on the development part of the idea's window: W-M Jul 2016–2022; W-T the first 250 sessions; W-D 2000–2015):** at ≥ 150 events (ES+NQ pooled, or per market for others), kill the idea if mean net R ≤ 0, *or* if the confidence interval of its lift over the best matched control includes 0, *or* if it is positive only on one side of the market (long only on ES/NQ: suspect drift). Below 30 events: **no evaluation**. Between 30 and 149 events: **park as inconclusive**; never loosen a filter to raise the count, which would be a new configuration charged to the trial budget.

**Analyst 2's generic kill (A2 §1) applies in addition to every A2-origin idea and module** (restored v2):
- net expectancy ≤ 0 in validation;
- OR PF < 1.15 net;
- OR fewer than 100 trades over the full sample;
- OR performance within the 90% CI of the P1/P2 placebo;
- OR > 50% of the PnL comes from one year.

Where an entry states its own kill, both apply; the stricter one decides. A2's placebos: **P1** = unweighted average from the same anchor, **P2** = volume-shuffled AVWAP, **P3** = EMA20.

### 2.4 Base-feature map (what exists, what is missing)

Status from `reviews/base-class-rev2-status.md` (PR #4/#5 merged) and the strategy sources.

| Code | Feature | Status |
|---|---|---|
| **F-DEV** | Own developing profile, VA methods, lookahead audit (VPB-1/2/3/4) | Done (MC verification of the truncation audit still owed) |
| **F-PROF** | Completed-profile analytics (VPB-6): ValueRelationship, IsPoorHigh/Low, unfinished auction, SinglePrints, ProfileShape P/b/D/Thin/Double, IsNakedVah/Val, AcceptanceOutsideValue, ValueMigrationScore | Done |
| **F-COMP** | Composites N sessions, IsBalanced / BalanceRange (VPB-7) | Done |
| **F-NPOC** | Naked-POC tracking | Done (existing) |
| **F-HVN** | KDE HVN/LVN on a 1-tick grid | Done (existing) |
| **F-SESS** | Session stats: ETH/RTH/IB, prior day, gap (VPF-2); RTH/ETH profile streams (VPF-3) | Done |
| **F-ROLL** | Roll-splice flag (VPF-4) | Done; **v2.4:** `RollDetection.Series` emits one splice per contract with Globex-day keying ([#51](https://github.com/ettiennehugo/MultiCharts.Net/pull/51)); `RollTests` pin 30 rolls 2016–2024, U18/U19/U21 are not detectable from the series ([#55](https://github.com/ettiennehugo/MultiCharts.Net/pull/55), [#57](https://github.com/ettiennehugo/MultiCharts.Net/pull/57)); `_Test6` uses `RollDetection=Table` ([#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)) |
| **F-CAL** | CME holidays/half-days (VPF-6), CmeSessionClock (VPF-7) | Done |
| **F-OF** | Order-flow helpers (VPF-5): BarDelta, CumDelta, Diagonal/StackedImbalances, Absorption. FlowQuality chain per Rev 2.7: Aggressor (never exposed) > **MC-BidAsk** (High; VolumeDelta DataLoader / AskTraded-BidTraded, tick window only) > Quote (High; our Lee-Ready on dxFeed Trade+Bid+Ask) > TickRule (Medium) > BarUpDown (Low; the only flow before the tick window) > Unavailable | Done; **v2.3b:** the MC-BidAsk tier is now on main (PR #19 (72b4013)): `_OrderFlow.FlowSource` = Aggressor, **McBidAsk**, Quote, TickRule, BarUpDown, Unavailable (off by default, `FlowConfig.McBidAsk`). Per-stream earliest-tick tagging: partial, VERIFY. Absorption needs tier ≥ 3 **v2.4:** `CumDelta` carries a `Truncated` flag when the tick window cuts a session ([#48](https://github.com/ettiennehugo/MultiCharts.Net/pull/48)). |
| **F-VWAP** | VWAP engine, 3 slots (`Vwap1Type…Vwap3Type`): Disabled / Session / Length / AdaptiveFromDate (fixed date string), with σ (`CurrentVwap1Sigma`…) | Done; **as of merge** `EVwapType` adds Week, Month, Quarter, RollDate (`_VolumeProfileBase.EVwapType`, reset predicate in `_VpVwapEngine.Function.CS`) = "M-AVWAP-lite". Event anchors live in M-AVWAP **v2.4:** opt-in roll reset for Week/Month VWAP ([#52](https://github.com/ettiennehugo/MultiCharts.Net/pull/52), [#54](https://github.com/ettiennehugo/MultiCharts.Net/pull/54)). |
| **F-CONF** | Confluence engine | Done (existing); **v2.4 behaviour change:** `_VpConfluenceEngine` zone split and developing-profile confluence weight ([#47](https://github.com/ettiennehugo/MultiCharts.Net/pull/47)) — results from earlier runs are not comparable |
| **F-TM** | Trade-management primitives (CS-7): entry windows, FlatBy, MaxTradesPerDay, daily loss stop, TimeStopBars | Done |
| **F-JRNL** | Journal: R, MFE/MAE, run id (CS-1/CS-3); sizeForRisk (CS-4); optional cost filter | Done **v2.4:** per-line logging and `EVTEST` lines ([#49](https://github.com/ettiennehugo/MultiCharts.Net/pull/49)). |
| **F-EVT** | `logSignal` forward MFE/MAE + ControlMode (RandomEntry, ShiftLevels, DelayEntryBars) (CS-8) | Done; this **is** the Gate-3 event-study harness |
| ✓ **M-AVWAP** | **AVWAP anchor manager:** N dynamic anchors created at runtime on events (confirmed swing, gap-open bar, news bar, volume-climax bar, breakout bar, roll date, week/month/quarter open), each with σ-bands, slope, age, "touched" state, expiry, and a per-anchor journal tag | **Available** (PR #7): `_AvwapManager.Function.CS` (`_AvwapManager`, `EAvwapAnchor`) via `_CustomSignal.AvwapEvents`/`AvwapLimits`; anchors SwingHigh, SwingLow, Breakout, GapOpen, VolumeClimax, WeekOpen, MonthOpen, QuarterOpen, invalidate-all on roll. Gaps: `NewsBar` reserved and inert (still so on main; M-EVENT is not wired to it); the P1 unweighted / P2 volume-shuffled placebo twins are **now built in** (PR #19 (72b4013): `EAvwapPlacebo` P1/P2, off by default); roll invalidation fires only where `rollOn()` is overridden (`_VolumeProfileFuturesBase`, Data1). |
| ✓ **M-SWING** (= **PRICEACTION**) | Swing/ZigZag state machine: ATR-scaled swings, HH/HL/LH/LL labels, break-of-structure, confirmation timestamps, multi-TF | **Available** (PR #6): `_PriceActionEngine.Function.CS` (`_PriceActionEngine`, `PriceActionConfig`) via `_CustomSignal.PriceActionConfig`/`PriceActionEngine`; Fractal, AtrZigZag pivots, layers, HH, LH, HL, LL, isEqual, BOS/CHoCH, candle flags, `OnRoll`; `_VolumeProfileFuturesBase` applies `EPriceActionPreset.Futures`. Gaps: extra instances on other streams now via `_CustomSignal.ExtraPriceAction` (PR #19 (72b4013)); MC acceptance trace (spec Q3) not yet run. |
| ✓ **M-MTF** | Multi-TF via **native `BarsOfData(n)`, no internal HTF bar building** (VPB-8 closedBarsOnly) | **Available** (PR #8): `_CustomSignal.HtfClosed/HtfClosedBar/HtfForming/HtfStreams` + `_MtfMath.Function.CS`; probe strategy `_MtfProbe.Strategy.CS`. Gap: **Probe P2** (forming-bar Status, bar-end stamping, same-timestamp ordering, per-series MaxBarsBack) still to be run in MC. |
| ✓ **M-TOD** | Time-of-day normalisers (volume z, delta z, range z by clock minute over the prior 20 sessions) | **Available** (PR #9): `_TodNormaliser.Function.CS` via `_CustomSignal.TodMetrics` — Volume, Delta, Range, TrueRange, `Z, Median, Pct, ZCum`, per-tier delta reset. Gaps: delta is fed only by `_VolumeProfileFuturesBase.todDelta()` (VPF-5 footprint; W-T only; BarUpDown → NaN); the Rev 2.7 **MC-BidAsk** tier is now in `_OrderFlow.FlowSource` (PR #19 (72b4013)); the TOD delta feed uses it whenever the flow engine selects it (`todDelta()` returns the closed `FootprintBar`'s delta and `Quality` tier; McBidAsk is off by default, `FlowConfig.McBidAsk`) — verified pass 2. |
| ✓ **M-DAILY** | Daily/weekly bar stream with ATR20d, 200-day SMA, RSI(2), TSMOM | **Available** (PR #10): `_DailyContext.Function.CS` via `_CustomSignal.NeedsDaily`/`DailyConfig` — Wilder `Atr20d`, `Sma200`, `Rsi2`, `Tsmom(n)`, prev H, L, C, range, `GapPct`, prev-week H/L, roll-gap removal. **v2.3b:** HV and RSI-of-ROC now on main (PR #19 (72b4013)): `Hv(n)` / `HvPercentile` (`DailyConfig.hvLengths`), LBR = Wilder RSI of ROC1 (`lbrRsiLength`). Gap: Probe P3 (dxFeed daily = settlement?) open. |
| ◐ **M-NEWS / M-EVENT** | Tier-1 event calendar feed for blackout and anchoring | **Scheduled-event calendar available** (M-EVENT, PRs #25 (acf2f40), #27, #28 (3c3c008), #29 (db70e8f)): `_EventCalendar.Function.CS` (`_EventCalendar`, `EventConfig`; kinds FomcDecision/Press/Minutes, Cpi, Ppi, Nfp, …; generated table 2016–2027, coverage tri-state) via `_CustomSignal.EventCalendar`, `EventBlackout`, `EventFlattenNow`; tests `tests/EventTests`, `tests/CalendarTests`. Gaps: unscheduled/overnight news not covered; `EAvwapAnchor.NewsBar` still inert. **EV-4.6b fail-closed coverage:** the 80 GDP/PCE holes over 2016-24 block about 15-18% of days in 2016-22 under the Macro mask with `allowPartialCoverage=false`. **v2.4:** nits ([#31](https://github.com/ettiennehugo/MultiCharts.Net/pull/31)); `NoCoverage` state exposed ([#50](https://github.com/ettiennehugo/MultiCharts.Net/pull/50)). |
| ◐ **M-TICK** | Seconds-level burst / trade-size stream via DataLoader | **Available for counts/timing** (PR #12): `_TickStream.Function.CS` via `_CustomSignal.TickConfig` — DataLoader 1-tick request, per-bar prints, max prints/s, inter-print median/p10, burst flag. **Trade-size outputs** (histogram, large prints, volume) stay off (`TickConfig.sizeOutputsVerified=false`) until Rev 2.7 §E6 is verified. W-T only. |
| ✓ **M-XMKT** | Second instrument stream (e.g. NQ for ES, ZN for risk-off) | **Available** (PR #11): `_XmktLeg.Function.CS` via `_CustomSignal.XmktLegs` — staleness, log return, correlation, beta, RS-spread z, optional leader swing. Gaps: VIX/ZN depend on dxFeed bundles (§E1); orders on Data1 only (two-leg trading needs M-PORT). |
| ◐ **M-PORT** | Portfolio-level sizing/throttles across strategies (MC Portfolio Trader or an external tool) | **Coded on main** (PR #14 (8b71a28), PR #17 (cd2ea61), PR #22 (85a724d)): `_PortMM.Function.CS` (`_PortMM` allocator, JSON `PortMmConfig`), `_PortfolioRiskManager.Strategy.CS`, `pm.*` publishing in `_CustomSignal`, P5b test signals `_TestPortArm`/`_TestPortMM`, `tests/PortMMTests`. **Gap: Probe P5b live checks not yet run** (test signals `_TestPortArm`/`_TestPortMM` cover L1–L8; L9 is the IBKR roll-mapping check on a paper account over one roll, not a test signal). .NET API: `PortfolioSignalObject.PortfolioStrategies` / `MyPortfolioData` / `CommonPortfolioData`; `IPortfolioStrategy.EntryContracts`, `AllowEntries*`, `Paused`, `ForceClosePosition()`. Design: `_CustomSignal` publishes `pm.*` keys; `_PortfolioRiskManager` MM signal (risk-parity/vol-target sizing, heat cap, drawdown throttle, ρ clusters, kill switches). Live checks L1–L9 (Probe P5b) before any PF result is read. |
| ◐ **M-COST** | Style-dependent slippage, p90 stop slippage, haircut | **Not a module** (spec rev 2): MC native Commission/Slippage + the optional cost filter `_CustomSignal.CostFilterMaxFractionOfR` (exists) + G0-a done offline from the journal. Style, p90, haircut are not coded. |
| ✓ **M-GMM** | Bootstrap POC, KDE sweep, GMM/BIC, entropy, KL | **Available**: `_ProfileStats.Function.CS` · `BootstrapPoc` (block 5, 500 draws), `KdeSweep` (30 points, `EKdeVerdict` Stable/Flat/Bimodal, h_cv), `Entropy` (Miller–Madow) + `_EntropyTracker`, `KL` / `KLKde` (PR #18 (5122247, 12b40b1)); `_Gmm.Function.CS` (`_Gmm`, BIC K-search) and G-P mixtures `ProfileMixture` / `SigmaMixture` / `_GmmProfileTracker` (PR #21 (e911e07) + PR #24 (4c90e00)); tests `tests/VpHelperTests`, `tests/GmmTests`. |
| ✗ **M-GEX** | External daily GEX file | **Deferred** (no code; data-blocked). |
| ✓ **VP helpers** | Range profile [t0,t1], weekly profile stream, per-bar profile ladder, entropy/Parkinson/V(p)/T(p), FVG detector | **Available** (PR #18 (5122247, 12b40b1)): `_BarRing`, `_Rng`, `_LevelRef` (`ELevelKind`); VP-1 `_RangeProfile.Function.CS` (host `RangeProfileAfter`); VP-2 `_WeeklyProfiles.Function.CS` (`WeeklyProfiles`, N-week composites, roll split); VP-3 `_BarLadder.Function.CS` (`_BarLadderStore`, `BarLadders`, Data2+ HTF ladders); VP-4/VP-6 `_ProfileStats` (entropy, `ParkinsonPerBar`/`ParkinsonSessionPrice`, `VtPrices` V(p)/T(p)); VP-5 `_FvgDetector.Function.CS`; `tests/VpHelperTests`. |
| ◐ **A2-33 harness** | Matched-control level study, Kaplan–Meier/Cox level death | **Available except Cox** (PR #15 (18b5404) + PR #16; Gate-0 verdict PR #20 (5ca25a6)): `_LevelStudy.Function.CS` (`_LevelStudy`, `LevelStudyConfig`; matched controls 0.9–1.1× distance, paired edge, Holm, quality terciles, age half-life, **Kaplan–Meier**) via `_CustomSignal.LevelStudyConfig`; `tests/LevelStudyTests`. **Cox regression not implemented** (source comment: "KM + age buckets instead"); DVP level kinds absent (M-DVP). **v2.4:** DVP level kinds registered through the M-DVP hook (PR-D7, [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); `ELevelKind` DVP kinds ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32)). |
| ✓ **Gate0** | Gate-0 measurement: level families vs seeded placebos via a hosted `_LevelStudy`; G0-b canaries | **Available** (PR #20: 932b7ca, efd5252, 3beddbf, 5ca25a6, 5c9c490): `_Gate0.Function.CS` (`Gate0Engine`), `_TestGate0`, `tests/Gate0Tests`. Gap: not yet run in MultiCharts. **v2.4:** canary slippage 0.5 tick/side ([#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30)); onRoll ATR reset per `specs/gate0-prereg-atr-roll.md` ([#43](https://github.com/ettiennehugo/MultiCharts.Net/pull/43)); AV_OPEN retired, Holm m 37→36 ([#44](https://github.com/ettiennehugo/MultiCharts.Net/pull/44)); Data1 ≤ 1-minute guard ([#45](https://github.com/ettiennehugo/MultiCharts.Net/pull/45)); KD-02 canary `EGate0Canary.KD02` + DVP coverage journal ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)). Still not run in MC. |
| ◐ **`_RegimeEngine`** (G-R) | Market regime: daily features → Gaussian HMM (K = 3) with walk-forward refit, filtered posteriors, hysteresis; AtrTercile (RG-03) fallback | **Partial — code complete** (PR #23 (bc9aef2)): `_RegimeEngine.Function.CS` (`_RegimeEngine`) via `_CustomSignal.RegimeConfig`; `tests/RegimeTests`. Partial only because consumers need pre-registration, consumer C1 has not run, and the HMM must beat the RG-03 baseline. (`_RegimeGate`, PR #26, has no row; see A2-63 / RG-03.) **v2.4:** `_Test6` (A2-63 consumer) uses `RollDetection=Table` and the RegimeGate config hash includes the roll fields ([#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)). |
| ◐ **M-DVP** | Detrended volume-profile engine (Keller B5): Forsythe orthogonal-polynomial fit over the last 240 closed bars, residual σ, residual-space volume manifold (POC / 70% VA / HVN / LVN as curved rails), ±1/2/3σ rails, Fibonacci lattice, weightless 3-family confluence; spec and `DvpConfig` in §2.4b | **New (Keller B5 rev 3.1, signed off 23 Oct 2026); not yet coded.** Exists only in `specs/keller-b5-catalogue-additions.md` §2 **v2.4: partial — engine on main** (PR-D0…D8: [#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#33](https://github.com/ettiennehugo/MultiCharts.Net/pull/33), [#34](https://github.com/ettiennehugo/MultiCharts.Net/pull/34), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#37](https://github.com/ettiennehugo/MultiCharts.Net/pull/37), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58); D2 manifold/rails arrived via #36's cherry-pick, #35 was merged into a feature branch): `_DvpEngine.Function.CS`, `tests/Dvp`. **Open:** PR-D9 (freeze d* in `DvpFrozenDegree` from the KD-08 coverage run) and PR-D10 (flip `P2_PASSED`); MC runs DV-R3/DV-R4 pending. Consumers (PS-25, MT-07, RG-09/10, KD-06/09/10) may not start before PR-D9. |
| ◐ **M-INSTR** | Instrument profile (tick size, sessions, roll mode per root; `specs/instrument-profile-spec.md`) | **v2.4: partial — probe only** (PR-I0 `_InstrProbe`, [#38](https://github.com/ettiennehugo/MultiCharts.Net/pull/38); `tests/InstrProbeTests`). PR-I1…I6 (the profile itself) not started. |
| ✗ **M-VXTS** | VX term-structure engine (v2.5): per-contract VX1/VX2 with days to expiry, 30-day constant-maturity interpolation, daily roll D, VIX/VX1 and VIX/VIX3M ratios, SOQ/expiry calendar via `EventRules.VixExpiry` | **Missing**: needs CFE per-contract history and the Cboe index feed (`$VIX`, `$VIX3M`, `$VVIX`), both unverified; unlocks VX-01…VX-05, VX-07 |
| ✗ **M-ETF** | ETF instrument profile (v2.5): share-based sizing, split/dividend adjustment, short locate/borrow-fee model (assumed schedule; no history in dxFeed) | **Missing**: unlocks ETF arms of LE-01…LE-07 (LE-05 data-blocked on borrow) |

**Data-quality rule for order flow:** there is no aggressor field. Inside W-T, flow comes from MC's bid/ask-traded split (High) or our Lee-Ready (High). Before W-T, only BarUpDown (Low) exists, and it is never used for signals. Results are reported **per FlowQuality tier**. *Absorption* and *delta at single prices* need tier ≥ 3. All OF ideas need the **K-SH shuffled-flow placebo** and **≤ 4 configs** (≈ 500 sessions).

### 2.4a Implementation status (as of merge)

**v2.4 (31 Oct 2026):** re-checked against `origin/main` @ `3f9e9be` (PRs #30–#58; `gh pr list` + `git log --merges` + `git grep` in a fresh worktree); M-DVP and M-INSTR rows changed, other rows gained notes. Originally checked against `github.com/ettiennehugo/MultiCharts.Net` `main` @ `9603948` (PRs #6–#13 merged 2026-10-21, 15:34–17:59 ET) by grepping the code, not from PR titles. **v2.3b re-audit (23 Oct 2026):** re-checked against `origin/main` @ `629da0d` (PRs #14–#29, incl. #29 merged) with `git grep` / `git ls-tree`; every changed row cites its PR/commit; full evidence in `specs/catalogue-v2.3b-status-changes.md`. Spec: `specs/missing-functionality-specs.md` (rev 4). All engines are lazy, enabled by protected properties on `_CustomSignal` (no new `[Input]`s) and compiled in `PLStudies.csproj`. Off-MC unit tests exist under `tests/` (PriceAction, Avwap, Mtf, Tod, Daily, Xmkt, Tick; since v2.3b also VpHelper, Gmm, LevelStudy, PortMM, Event, Calendar, Roll, OrderFlow, Gate0, Regime, RegimeGate); **no MC acceptance trace or probe (P1–P4) has been run yet**; P5 (.NET portfolio API) was resolved from the API metadata on 2026-10-21 (§2.4a M-PORT row).

| ID | Status | Where (file · class/member) | Remaining gap |
|---|---|---|---|
| M-SWING / PRICEACTION | Available | `_PriceActionEngine.Function.CS` · `_PriceActionEngine`, `PriceActionConfig`, `EPriceActionPreset`; `_CustomSignal.PriceActionConfig`/`PriceActionEngine` | Extra streams via `ExtraPriceAction` (PR #19 (72b4013)); MC acceptance trace (Q3) |
| M-AVWAP | Available | `_AvwapManager.Function.CS` · `_AvwapManager`, `_Avwap`, `EAvwapAnchor`, `AvwapLimits`; `_CustomSignal.AvwapEvents` | NewsBar inert; P1/P2 placebo twins built in (PR #19 (72b4013)); roll invalidation only with the futures base |
| F-VWAP (calendar anchors) | Available | `_VolumeProfileBase.EVwapType` Week, Month, Quarter, RollDate; `_VpVwapEngine.Function.CS` | — |
| M-MTF | Available | `_CustomSignal.HtfClosed`, `HtfClosedBar`, `HtfForming`, `HtfStreams`; `_MtfMath.Function.CS`; `_MtfProbe.Strategy.CS` | Probe P2 not yet run |
| M-TOD | Available | `_TodNormaliser.Function.CS` · `_TodNormaliser`, `ETodMetric`; `_CustomSignal.TodMetrics`; `_VolumeProfileFuturesBase.todDelta` | Delta only on futures base, W-T; MC-BidAsk tier now in `FlowSource` (PR #19 (72b4013)), TOD delta uses it when `FlowConfig.McBidAsk` is on (`todDelta` → `FootprintBar.Quality`) |
| M-DAILY | Available | `_DailyContext.Function.CS` · `_DailyContext`, `DailyConfig`; `_CustomSignal.NeedsDaily` | HV / LBR RSI-of-ROC1 added (PR #19 (72b4013)); P3 settlement check |
| M-XMKT | Available | `_XmktLeg.Function.CS` · `_XmktLeg`, `XmktLegConfig`; `_CustomSignal.XmktLegs` | Data bundles (VIX/ZN); single-leg orders |
| M-TICK | Partial | `_TickStream.Function.CS` · `_TickStream`, `TickConfig`, `TickBarStats`; `_CustomSignal` DataLoader request | Size outputs gated until §E6 |
| M-COST | Partial (by design) | MC native costs + `_CustomSignal.CostFilterMaxFractionOfR` | Style, p90 slippage, haircut offline only |
| M-PORT | **Partial: coded, live checks pending** (PR #14 (8b71a28), PR #17 (cd2ea61), PR #22 (85a724d)) | `_PortMM.Function.CS` · `_PortMM`, `PortMmConfig`; `_PortfolioRiskManager.Strategy.CS`; `pm.*` publishing in `_CustomSignal`; `_TestPortArm`, `_TestPortMM`; `tests/PortMMTests` | Probe P5b live checks (MM timing, `EntryContracts` default, `Paused`/`ForceClosePosition` semantics, IBKR roll mapping) |
| M-NEWS / M-EVENT | **Partial** (M-EVENT, PRs #25 (acf2f40), #27, #28 (3c3c008), #29 (db70e8f)) | `_EventCalendar.Function.CS` · `_EventCalendar`, `EventConfig`; `_CustomSignal.EventCalendar`, `EventBlackout`, `EventFlattenNow`; `_EventCalendarTest.Strategy.CS`; `tools/m-event/`; `tests/EventTests` | Scheduled events only (no unscheduled news); `NewsBar` AVWAP anchor inert; EV-4.6b fail-closed coverage: 80 GDP/PCE holes over 2016-24 block about 15-18% of days in 2016-22 under the Macro mask with `allowPartialCoverage=false` |
| M-GMM | **Available** (PR #18 (5122247, 12b40b1); PR #21 (e911e07) + PR #24 (4c90e00)) | `_ProfileStats.Function.CS` · `BootstrapPoc`, `KdeSweep`, `Entropy`, `_EntropyTracker`, `KL`, `KLKde`, `ProfileMixture`, `SigmaMixture`; `_Gmm.Function.CS` · `_Gmm`; `tests/VpHelperTests`, `tests/GmmTests` | MC acceptance trace not run |
| M-GEX | **Deferred** | — | Data-blocked |
| VP helpers (range profile, weekly profile, per-bar ladder, entropy/Parkinson, V(p)/T(p), FVG) | **Available** (PR #18 (5122247, 12b40b1)) | `_RangeProfile`, `_WeeklyProfiles`, `_BarLadder` (`_BarLadderStore`), `_ProfileStats`, `_FvgDetector`, `_BarRing`, `_Rng`, `_LevelRef`; host `_VolumeProfileBase.RangeProfileAfter`/`WeeklyProfiles`/`BarLadders`; `tests/VpHelperTests` | MC acceptance trace not run |
| A2-33 harness (matched controls, KM/Cox) | **Partial** (PR #15 (18b5404) + PR #16; Gate-0 verdict PR #20 (5ca25a6)) | `_LevelStudy.Function.CS` · `_LevelStudy`, `LevelStudyConfig`; `_CustomSignal.LevelStudyConfig`; `tests/LevelStudyTests` | Cox regression not implemented (KM + age buckets); DVP level kinds now registered ([#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)) |
| Gate0 | **Available** (PR #20: 932b7ca, efd5252, 3beddbf, 5ca25a6, 5c9c490) | `_Gate0.Function.CS` · `Gate0Engine`; `_TestGate0`; `tests/Gate0Tests` | Not yet run in MultiCharts; v2.4: [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30), [#43](https://github.com/ettiennehugo/MultiCharts.Net/pull/43), [#44](https://github.com/ettiennehugo/MultiCharts.Net/pull/44), [#45](https://github.com/ettiennehugo/MultiCharts.Net/pull/45), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58) (KD-02 canary added) |
| `_RegimeEngine` (G-R) | **Partial: code complete** (PR #23 (bc9aef2)) | `_RegimeEngine.Function.CS` · `_RegimeEngine`; `_CustomSignal.RegimeConfig`; `tests/RegimeTests` | Consumers must be pre-registered; C1 (`_RegimeGate`, PR #26) not yet run; HMM must beat RG-03 |
| M-DVP (detrended volume-profile engine, Keller B5) | **Partial: engine coded (v2.4)** ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#33](https://github.com/ettiennehugo/MultiCharts.Net/pull/33), [#34](https://github.com/ettiennehugo/MultiCharts.Net/pull/34), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#37](https://github.com/ettiennehugo/MultiCharts.Net/pull/37), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)) | `_DvpEngine.Function.CS` · `_DvpEngine`, `DvpConfig`, `DvpCalib`; `ELevelKind` DVP kinds (`_LevelRef`); `_ProfileMath.Prominence`; `tests/Dvp` | PR-D9 d* freeze, PR-D10 `P2_PASSED`; MC runs DV-R3/DV-R4; no consumers |
| M-INSTR (instrument profile) | **Partial: probe only (v2.4)** ([#38](https://github.com/ettiennehugo/MultiCharts.Net/pull/38)) | `_InstrProbe*`; `tests/InstrProbeTests` | PR-I1…I6 |
| M-VXTS (VX term structure, v2.5) | **Missing** | — | CFE per-contract VX + index feed (verify); build engine (R7 §2) |
| M-ETF (ETF profile, v2.5) | **Missing** | — | Equity bundle (verify); splits/dividends; borrow model (no data) |

**Summary (v2.5, 21 rows):** as v2.4 plus **2 missing** (M-VXTS, M-ETF; new in v2.5). *(v2.4, 19 rows:)* 10 available (unchanged list), **8 partial** (the six below plus **M-DVP** — engine coded, PR-D9/D10 open — and **M-INSTR** — probe only), **1 deferred** (M-GEX). *(v2.3b, 18 rows, kept for the record:)* 10 available (M-SWING/PRICEACTION, M-AVWAP, F-VWAP calendar anchors, M-MTF, M-TOD, M-DAILY, M-XMKT, M-GMM, VP helpers, Gate0 — not yet run in MC —), 6 partial (M-TICK, M-COST, M-PORT — coded, P5b live checks pending —, M-NEWS/M-EVENT — scheduled events only; GDP/PCE holes (EV-4.6b) —, A2-33 harness — no Cox —, `_RegimeEngine` — code complete, consumer-restricted), 2 missing/deferred (M-GEX, M-DVP). *(First v2.3b audit: 9 / 5 / 2 over 16 rows; v2.3a: 7 / 2 / 6 over 15 rows; the A2-33 harness is now its own row.)* Each idea's **Readiness (as of merge)** line below is derived from its Needs line.

### 2.4b M-DVP: detrended volume-profile engine (Keller B5, rev 3.1)

Source: `specs/keller-b5-catalogue-additions.md` §2 (rev 3.1, signed off 23 Oct 2026). **Status (v2.4): ◐ engine coded on `main`** — PR-D0…D8 ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#33](https://github.com/ettiennehugo/MultiCharts.Net/pull/33), [#34](https://github.com/ettiennehugo/MultiCharts.Net/pull/34), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#37](https://github.com/ettiennehugo/MultiCharts.Net/pull/37), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); D2 reached main through #36. `_SbxHost` and the `_SbxE/G/S_KD` consumer files are not coded. **Open:** PR-D9 (freeze d*), PR-D10 (`P2_PASSED`), MC runs DV-R3/DV-R4. Authoritative spec: `specs/m-dvp-spec.md`. *(v2.3b: ✗ new, not coded.)* §8 build order: after the VP helpers (row 3d).

- **Convention:** spec `missing-functionality-specs.md` rev 4 §0.1. A plain `internal sealed class`, MC-free, created in `StartCalc` only when a derived strategy sets a **protected configuration property** on `_CustomSignal` (`protected DvpConfig DvpConfig { get; } = new DvpConfig();`, `enabled = false`). **No new `[Input]`** (D7). Null when disabled; querying it disabled logs one `ETraceLevel.Warning`.
- **New code:** `_DvpEngine.Function.CS` (`_DvpEngine`, `DvpConfig`, `DvpFit`, `DvpManifold`, `DvpRail`, `DvpLattice`), compiled in `PLStudies.csproj`; off-MC unit tests under `tests/Dvp`.
- **Reuse:** the `_RangeProfile` ring pattern or the M-MTF closed-bar feed; M-MTF `HtfClosed/HtfClosedBar` for 60m/240m streams; `_ProfileMath.Accumulator.Add` (mode 2) on residual rows; `_ProfileMath.ValueAreaExpansion`; `_VpHistogram` node logic (prominence 0.12) [GAP: exact prominence code unread]; `_ProfileStats.Entropy`; `_LevelRef` with new `ELevelKind` values `DvpPoc, DvpVah, DvpVal, DvpHvn, DvpLvn, DvpSigma, DvpFib, DvpConf`; `_PriceActionEngine` confirmed pivots for the lattice; the base ATR / `Gate0Atr`; optional M-AVWAP comparison curve; optional `_Gmm` (off; `_Gmm` is on main, PR #21 (e911e07) + PR #24 (4c90e00)).
- **Fit:** Forsythe orthogonal polynomials by three-term recurrence on de-meaned hl2 over closed bars t−n…t−1 (`winLen` = 240). BIC is computed and journaled **report-only**; the primary degree is `DevCoverageFrozen` (|forward − in-sample ±2σ coverage|, averaged over dev walk-forward folds; lowest wins, ties to the lower degree, d ≤ 3), evaluated on dev only and frozen at sign-off; **d = 1 is the sensitivity arm**. σ = residual standard error. The forward cone is **reference only, never a stop or target edge**.
- **Manifold, rails, lattice, confluence:** bar volume spread over residual z-rows (64 rows over ±3σ; outside volume kept as `tailVol`); rails at bar t come from the fit frozen at t−1; lattice from up to 3 ranges of confirmed pivots (strength 20/40/80), merged within 0.40 ATR, kept within ±14 ATR; confluence = greedy cluster within **0.3 ATR**, **score = number of distinct families** {manifold, sigma, lattice} (weightless); a qualifying confluence needs the **third family**; a level from two lines of one range counts once.

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

**Rev 3.1 constants (ruling 6.1 item 1; constants, not new `DvpConfig` fields and not `[Input]`s; logged in the config hash):**

```
confMode      = FamilyCount            // constant
minFamilies   = 3                      // constant
reportWeights = VA 1.00 / POC 0.77     // journal-only; never gates, never selects
row floor     = one tick               // rowWidth = max(σ-row, tick); effective manRows journaled
rollAdjust    = none                   // no back-adjustment; crossesRoll skip stays (roll bar ±1 via IsRollWindow)
```

- **Strict causality (non-negotiable):** closed bars only; rails at t from the window ending at t−1 (`LevelRef.createdTime` = open of bar t); pivots at confirmation time; ATR known at t−1; HTF rails only after the HTF bar has closed **and** Probe P2 has passed. Off-MC truncation audit (bit-equality of every rail at t from a series truncated at t−1) plus a shuffled-future test.
- **Repaint risk:** polynomial endpoints are worst-determined; every refit re-maps all 240 bars of the manifold, so only the t−1 snapshot is tradeable. Journal the as-of fit at every bar, report `endpointDrift`, keep a degree-1 shadow fit, never evaluate on a centred/full-window refit. A rail drawn from a later fit is a lookahead bug and should trip the G0-b canaries.
- **Rolls:** a fit window may not cross a roll (`crossesRoll` → no rails) until `winLen` bars have accrued post-roll. Journal the **share of skipped fit windows per stream**; a stream skipping **> 30%** is **diagnostic-only**. Back-adjusting is report-only, after G0-c, and never feeds Holm. Orders, fills and costs stay on the actual contract.
- **Journal** (`ETraceLevel.Journal`): per stream per bar `runId, configHash, stream, barTime, n, deg, bic[1..dCap] (report-only), manRowsEff, rthShare, crossesRoll, sigma, sigmaAtr, r2, dw, skew, yhatEnd, slopeEnd, endpointDrift, pocZ, vaLoZ, vaHiZ, entropy, concentration, tailVol, inSampleCov2`; per rail interaction `railKind, z, price, families, nRanges, score, distAtr, closedThrough(1 bar), heldBars, accepted(N), MFE40, MAE40` via F-EVT `logSignal`.
- **Evidence caveats carried with the engine:** the book's in-sample ±2σ coverage (95.5–95.6%) is fit-on-own-data; forward ±2σ coverage is 28.2% FX / 27.7% crypto as printed (loc 553–593). Nothing in M-DVP is a containment band. The reference script is not the census code. The buy/sell split is a bar-shape proxy (off). The book's published alerts are **not ported** (they fire on rail touches, i.e. held-family events). XAU/XAG prices are printed at 1/100 on 93 of 300 exhibits (App. F erratum). Every KD row carries **FX/crypto only · no true delta · ES prior 0**.
- **Readiness (v2.4):** engine buildable and coded; consumers wait on PR-D9. *(Superseded:* Not yet buildable — needs `_DvpEngine` (new), `ELevelKind` additions, Fib lattice builder, prominence adapter; depends on ✓ M-MTF (Probe P2 still open), ✓ M-SWING, F-EVT, `_ProfileMath`, `_ProfileStats`.

### 2.4c Per-entry implementation status (v2.4, `origin/main` @ `3f9e9be`)

Checked on Sat 31 Oct 2026 (EDT) against every PR merged up to #58 (#30–#58 new in this audit), by `gh pr list --state merged`, `git log --merges` and `git grep` of each ID in a fresh worktree. No MC run has been made of anything below, so **"implemented" means coded and unit-tested off-MC, not tested in the sense of §7**.

**Definitions.** **Implemented:** the entry's own rule or measurement is coded on `main`. **Partial:** part of it is coded (one arm, or the core statistic without the rule or host). **Spec'd:** no entry-specific code, but a build spec beyond this catalogue exists (final-synthesis Specs 1–4, `specs/m-dvp-spec.md` §10.4). **Not started:** catalogue entry only; the engines it needs may exist (see each Readiness line).

**Counts (223 entries, v2.7):** Implemented **6** · Partial **16** · Spec'd **12** · Not started **189** (v2.4: 193 entries, 159 not started; v2.5 adds VX-01…VX-08 and LE-01…LE-07; v2.6 adds YV-01…YV-10; v2.7 adds PF-08, SP-09, OF-09, OF-10, EX-13; all not started).

| ID | Status | Evidence (PRs, files) |
|---|---|---|
| VW-08 | **Implemented** | G0-b canaries VW08 + A2_24_i/ii/iii + DV10 in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30), [#43](https://github.com/ettiennehugo/MultiCharts.Net/pull/43), [#44](https://github.com/ettiennehugo/MultiCharts.Net/pull/44), [#45](https://github.com/ettiennehugo/MultiCharts.Net/pull/45)). Includes merged A2-24 and DV-10. Not run in MC. |
| RG-03 | **Implemented** | ATR-tercile regime (`_RegimeEngine` fallback, PR #23; `AtrTercileTracker` and arm A of `_Test6`, PR #26, [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)). Not run in MC. |
| A2-53 | **Implemented** | G0-b canary A2_53 in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30)). Not run in MC. |
| A2-63 | **Implemented** | `_RegimeGate` + `_Test6` consumer per `specs/regime-consumer-prereg.md` / `a2-63-rulings.md` (PR #26; [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)). Pre-registered test not run. |
| DV-01 | **Implemented** | As the G0-b POC first-touch control (canary DV01) in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30)); the trade form is not coded. Not run in MC. |
| KD-02 | **Implemented** | Canary `EGate0Canary.KD02 = 8`, sweep 130..133 ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)). Not run in MC. |
| A2-40 | **Partial** | MC native costs + cost filter; style slippage, p90 stop slippage, haircut, ρ-sizing not coded (M-COST). |
| A2-59 | **Partial** | Event strata available via M-EVENT ([#31](https://github.com/ettiennehugo/MultiCharts.Net/pull/31), [#50](https://github.com/ettiennehugo/MultiCharts.Net/pull/50)); shock-flip audit and report-window cost strata not coded. |
| A2-39 | **Partial** | Reset arm only (roll reset in Gate0 / `_LevelStudy`; roll table [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46), [#51](https://github.com/ettiennehugo/MultiCharts.Net/pull/51), [#55](https://github.com/ettiennehugo/MultiCharts.Net/pull/55)); cash-terms and back-adjusted arms not coded. |
| A2-33 | **Partial** | `_LevelStudy` (PRs #15/#16/#20) + DVP kinds ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); Cox not implemented. |
| PS-03 | **Partial** | Naive-80% arm coded as G0-b canary PS03 (PR #20); confirmed-entry rule (final-synthesis Spec 2) not coded. |
| A2-25 | **Partial** | Statistic coded (`_ProfileStats.BootstrapPoc`, PR #18); gate rule / host not coded. |
| A2-26 | **Partial** | Statistic coded (`_ProfileStats.KdeSweep`, PR #18); gate rule / host not coded. |
| A2-27 | **Partial** | Mixture components coded (`_Gmm`, PRs #21/#24) and used as a Gate0 level family; trade rule not coded. |
| A2-28 | **Partial** | Entropy statistic coded (`_ProfileStats`, `_Gmm`, PR #18/#21); rule not coded. |
| A2-29 | **Partial** | Statistic coded in `_ProfileStats` (PR #18); rule not coded. |
| A2-30 | **Partial** | Statistic coded (`_ProfileStats`, `_RangeProfile`, PR #18); rule not coded. |
| A2-31 | **Partial** | Statistic coded in `_ProfileStats` (PR #18); rule not coded. |
| A2-32 | **Partial** | σ-mixture coded (`SigmaMixture`, PR #24) and used as a Gate0 family; rule not coded. |
| A2-34 | **Partial** | V(p)/T(p) coded (`_ProfileStats`, PR #18); event study not coded. |
| KD-08 | **Partial** | `DvpCalib` coverage journal ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); dev-only coverage-match run and d* freeze (PR-D9) open. |
| KD-11 | **Partial** | DVP levels reach `_LevelStudy` via the PR-D7 hook ([#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); reach-rate tabulation not coded. |
| PS-01 | **Spec'd** | final-synthesis Spec 1; no strategy code. |
| PS-02 | **Spec'd** | final-synthesis Spec 1A; no strategy code. |
| PS-05 | **Spec'd** | final-synthesis Spec 3; no strategy code. |
| PS-06 | **Spec'd** | final-synthesis Spec 3 (mirror); no strategy code. |
| PS-25 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| MT-07 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| SP-07 | **Spec'd** | final-synthesis Spec 4; no strategy code. |
| RG-09 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| RG-10 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| KD-06 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| KD-09 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |
| KD-10 | **Spec'd** | `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9. |

**Not started (189):**
- **PS** (19): PS-04, PS-07, PS-08, PS-09, PS-10, PS-11, PS-12, PS-13, PS-14, PS-15, PS-16, PS-17, PS-18, PS-19, PS-20, PS-21, PS-22, PS-23, PS-24
- **VW** (7): VW-01, VW-02, VW-03, VW-04, VW-05, VW-06, VW-07
- **AV** (13): AV-01, AV-02, AV-03, AV-04, AV-05, AV-06, AV-07, AV-08, AV-09, AV-10, AV-11, AV-12, AV-13
- **MT** (6): MT-01, MT-02, MT-03, MT-04, MT-05, MT-06
- **OF** (10): OF-01, OF-02, OF-03, OF-04, OF-05, OF-06, OF-07, OF-08, OF-09, OF-10
- **SP** (8): SP-01, SP-02, SP-03, SP-04, SP-05, SP-06, SP-08, SP-09
- **TS** (7): TS-01, TS-02, TS-03, TS-04, TS-05, TS-06, TS-07
- **SW** (6): SW-01, SW-02, SW-03, SW-04, SW-05, SW-06
- **RG** (7): RG-01, RG-02, RG-04, RG-05, RG-06, RG-07, RG-08
- **PF** (8): PF-01, PF-02, PF-03, PF-04, PF-05, PF-06, PF-07, PF-08
- **A2** (41): A2-04, A2-09, A2-11, A2-12, A2-13, A2-14, A2-15, A2-35, A2-36, A2-37, A2-38, A2-43, A2-45, A2-46, A2-48, A2-49, A2-50, A2-51, A2-52, A2-54, A2-56, A2-57, A2-58, A2-60, A2-61, A2-62, A2-64, A2-65, A2-66, A2-67, A2-68, A2-69, A2-70, A2-72, A2-73, A2-74, A2-75, A2-76, A2-77, A2-78, A2-79
- **DV** (11): DV-02, DV-06, DV-07, DV-08, DV-09, DV-11, DV-15, DV-16, DV-20, DV-22, DV-23
- **L2** (7): L2-01, L2-02, L2-03, L2-04, L2-05, L2-06, L2-07
- **KD** (1): KD-07
- **EX** (13): EX-01, EX-02, EX-03, EX-04, EX-05, EX-06, EX-07, EX-08, EX-09, EX-10, EX-11, EX-12, EX-13
- **VX** (8, v2.5): VX-01, VX-02, VX-03, VX-04, VX-05, VX-06, VX-07, VX-08
- **LE** (7, v2.5): LE-01, LE-02, LE-03, LE-04, LE-05, LE-06, LE-07
- **YV** (10, v2.6): YV-01, YV-02, YV-03, YV-04, YV-05, YV-06, YV-07, YV-08, YV-09, YV-10

Each `####` entry below also carries an **Implementation status (v2.4)** line under its heading (VX/LE entries: **v2.5**; YV entries: **v2.6**; PF-08, SP-09, OF-09, OF-10 and EX-13: **v2.7**). The EX rows EX-01…EX-12 (§3.10) are covered by this list only; EX-13 also has a `####` entry.

### 2.5 The ten sandbox slots

`_Test1 … _Test10` (`MultiCharts.Net/_TestN.Strategy.CS`) are currently empty `SignalObject` stubs. Before first use, each must be re-based onto `_VolumeProfileFuturesBase`. Proposed convention for every slot:

- an input `Mode` ∈ {`Strategy`, `EventStudy`, `ControlRT`, `ControlFL`, `Canary`};
- an input `Variant` (integer index into the pre-registered sweep);
- an input `ExitModule` (index into the EX family, so exits are an ablation rather than a new slot);
- the run id and config hash written to the journal on every trade;
- (v2) an input `GateModule` (RG/A2 gate under ablation) and an input `RiskModule` (A2-72…A2-79 exit/risk modules, hosts H1–H3).

Exits (EX family) and regime filters (RG family) are **modules that ride inside a slot**, not ideas that occupy a slot of their own.

### 2.6 Gate 0: prerequisites that run before any level-based idea (v2)

The order is fixed. An idea's Gate-3 result is not read until every item that applies to it has passed.

| Step | Item | What it decides | Applies to |
|---|---|---|---|
| G0-a | **A2-40 + A2-59** honest cost model: style-dependent slippage, unables haircut, report-window strata, shock-flip audit, p90 stop slippage, ρ = 0.9 index-book sizing | Whether a net edge survives realistic costs. **An idea whose net edge turns ≤ 0 under it is killed** | **Every** idea (built into the base class, not a slot) |
| G0-b | **VW-08 canary suite** (+ A2-24 i–iii, DV-10 weekly-σ1 fade), **A2-53** gap-fade canary, **DV-01** POC first-touch control, PS-03 naive-80% canary, **KD-02** detrended band-stretch fade to the POC rail (Keller B5; re-run on every M-DVP engine change; kill net PF > 1.0, no 1.0–1.1 zone) | Simulator honesty: net PF > 1.0 on a canary means a bug (lookahead, moving-target fill, cost error) | Every idea (run once per engine change) |
| G0-c | **A2-39** roll-honest composites (reset vs cash-terms vs back-adjusted), scored by A2-33 | Which multi-session level construction to use on unadjusted `@ES` | Every multi-session level |
| G0-d | **A2-33** level persistence vs matched control prices (20 controls per level at 0.9–1.1× distance; Kaplan–Meier "level death"; Holm across level types). v2 adds to its level list the types Batch 1 uses: ON high/low, IB edges, composite VAH/VAL, prior VAH/VAL/POC, naked POC, HVN/LVN, single-print shelves, mixture components; **Keller B5 (v2.3): `DvpVah/DvpVal/DvpPoc/DvpHvn/DvpLvn/DvpSigma/DvpConf`** (KD-11 shares the run) | Which level types may be traded, up to what age, and in which quality tercile. **Every level-based idea inherits the verdict**; edge ≈ 0 (|t| < 2) → that level type is not traded | All level-based ideas (PS, MT, SP, AV level arms, DV level ideas, A2-25…A2-32, A2-41…A2-47, PS-25, MT-07, RG-09, KD-06, KD-10) |

**Not gated by G0-d** (no prior-session level): TS-01, VW-05, A2-09, A2-49, A2-56, A2-58, A2-60, A2-63/64/65/66/69, PS-07, DV-15, SW-02. These may run in parallel with the A2-33 study, but G0-a and G0-b still apply to them.

**v2.7:** PF-08 (daily Donchian), OF-09 (value area built the same day) and SP-09 (equal-high and FVG types, which are not in the G0-d list above) are not blocked on A2-33. SP-09 still reports those levels into the study. G0-a and G0-b apply to all three.

---

## 3. The catalogue

Counts (v2.7): **223 entries** = the v2.3 193 + 8 VX + 7 LE (R7, v2.5) + 10 YV (YouTube videos 2026-11-01, v2.6) + 5 video specifications (PF-08, SP-09, OF-09, OF-10, EX-13; v2.7). v2.3 breakdown: **193 entries** = 114 from v1 + 49 new Analyst 2 entries + 12 Dale (DV) entries + 7 parked L2 entries + 11 Keller B5 (KD) entries, plus the 30 methodology items M1–M30 (§9).
- **By family:** PS 25 · VW 8 · AV 13 · MT 7 · OF 10 · SP 9 · TS 7 · SW 6 · RG 10 · EX 13 · PF 8 · A2 56 (by type: EX 2 · G0 4 · IM 13 · OF 1 · PS 6 · RG 10 · SW 4 · TS 1 · XR 8 · v1 7; "v1" = the 7 A2 entries kept from v1) · DV 12 · L2 7 · KD 7 · VX 8 · LE 7 (v2.5) · YV 10 (v2.6, §3.19) (§3.16; KD-01/03/04/05 sit in PS/RG/MT as PS-25, RG-09, RG-10, MT-07). v2.7 adds OF-09, OF-10, SP-09, EX-13, PF-08.
- **By source:** catalogue v1 107 (non-A2) + Analyst 2 56 own (23 more merged into hosts) + Trader Dale 12 own (12 merged) + L2 7 (parked) + Keller B5 11 own (KD-01…KD-11; none merged) + YouTube videos 2026-11-01 10 own (YV-01…YV-10; 4 NEW, 6 VARIANT; 'existing-only' ideas mapped in Appendix A.8) + YouTube specifications 2026-11-08 5 own (PF-08, SP-09, OF-09, OF-10, EX-13; Appendix A.9).
- **Testable rows in the §4 matrix:** 150 (v2.7: +4 rows [est], PF-08, SP-09, OF-09, OF-10; v2.6: +8 YV rows [est]; v2.5: +13 VX/LE rows [est]; v2.4: 125) (standalone, layers, canaries). The rest are modules, gates or parked ideas. EX-13 is a module and is not a row.
- **Every source ID** (114 v1 IDs, A2-01…A2-79, L2-01…L2-07, M1…M30, DV-01…DV-24) is either its own entry or "Merged into X" (Appendix A, script-checked).

### 3.0 Gate 0: prerequisite entries (run before any level-based idea; §2.6)

These four Analyst 2 entries are **infrastructure and gates, not trade ideas**. They are not scored in the matrix. A2-40 and A2-59 sit in the base class; A2-33 and A2-39 run in `_Test1` of Batch 1. The canaries that complete Gate 0 are VW-08 (+ A2-24, DV-10), A2-53 (§3.12), DV-01 (§3.14) and, once M-DVP exists, KD-02 (§3.16).

#### A2-40 · Honest cost model and stressed-correlation sizing (Gate 0)
- **Implementation status (v2.4):** **Partial** — MC native costs + cost filter; style slippage, p90 stop slippage, haircut, ρ-sizing not coded (M-COST).
- **Family · Source:** Gate 0 (prerequisite) · Analyst 2 Rev 6 · Keller B2 Ch 13.6 (conditional slippage: stops at the 90th percentile in fragile regimes; fill probability for limits; capacity ∝ edge²; Drill 13.6) and 14.5 (size for stressed correlations). **Grade A** (documented).
- **Evidence:** **A (method)** · **original A2 grade: A (documented)** (rescaled per §3.12).
- **Markets · TF:** all
- **Data · test window:** all windows; live fill log from IBKR
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** n/a
- **Fails when:** n/a; an idea whose net edge turns ≤ 0 under it is killed
- **Needs:** ◐ M-COST (not a module: MC costs + `CostFilterMaxFractionOfR`; G0-a offline), F-JRNL (dxFeed signal price vs IB fill), ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending) (ρ = 0.9 book rule)
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT coded (PRs #14/#17/#22), Probe P5b live checks pending; ◐ M-COST (MC costs + `CostFilterMaxFractionOfR`; G0-a offline).
- **Gate (v2):** **Gate 0: must be live in the base before any result is evaluated**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Rules:**
    - Backtest costs: aggressive entries = 1 tick + fees; stops = p90 slippage from the live IB fill log bucketed by regime (until ≥ 300 fills exist: 2 ticks calm, 4 ticks when A2-37 persistent or short gamma); limits fill only on trade-through by ≥ 1 tick.
    - Live: log intended dxFeed price vs IB fill price on every order (aggressive/passive, stop/not, regime).
    - Portfolio: if open ES/NQ/RTY positions point the same way, size the book as one position with ρ = 0.9.
  - **Test:** re-run the top-5 A2 ideas under this model; any idea whose net edge turns ≤ 0 is killed.

#### A2-59 · Kaufman cost, 'unables' and shock-audit module (Gate 0, extends A2-40)
- **Implementation status (v2.4):** **Partial** — Event strata available via M-EVENT ([#31](https://github.com/ettiennehugo/MultiCharts.Net/pull/31), [#50](https://github.com/ettiennehugo/MultiCharts.Net/pull/50)); shock-flip audit and report-window cost strata not coded.
- **Family · Source:** Gate 0 (prerequisite) · Analyst 2 Rev 6 · Kaufman Ch 16:
- **Evidence:** **A (method)** · **original A2 grade: n/a (method)** (rescaled per §3.12).
- **Markets · TF:** all
- **Data · test window:** all windows; slippage regression on ≥ 200 live/paper fills (W-T era onward)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** n/a
- **Fails when:** n/a; an idea fails if net ≤ 0 under u = 10% plus scaled slippage, or under the shock flip
- **Needs:** ◐ M-COST (MC costs + cost filter; G0-a offline), M-TOD (SSB of TR), ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29) (report windows)
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. ◐ M-COST (MC costs + cost filter; G0-a offline). Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Gate (v2):** **Gate 0, applied to every idea before promotion**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - Trend and stop entries pay full slippage; mean-reversion limit entries pay about 50%.
    - Scale slippage with volatility but floor it.
    - Unables come only from winners (10% unables cut his example PF from 1.66 to 1.25).
    - Regression model slip = a0 + a1·vol + a2·dailyVol + a3·time + a4·size.
    - Ch 21: a shock audit. Days or bars ≥ 3.5× the average range; if the system wins on > 50% of them, flip the average shock P&L; "average neighbouring tests but use the largest drawdown".
    - **Grade:** n/a (method).
  - **Differs from:** A2-40 (honest cost model and stressed-correlation sizing). A2-59 adds three things A2-40 lacks: (i) **style-dependent slippage**, (ii) an **unables haircut**, (iii) a **shock flip test**. It is applied to every idea before promotion.
  - **Components:**
    1. **Slippage:** base s0 = 1 tick per side on stop and market fills. Scale by max(1, ATR(14, 5m)_now / median ATR(14, 5m) over the trailing 60 sessions), and never below 1 tick. Limit fills: 0 ticks, plus the 0.5-tick adverse-selection rule (A2 §1).
    2. **Unables:** for stop entries triggered on a bar whose range ≥ 2× SSB_TR, drop the trade **if it was a winner**, with probability u ∈ {0, 10, 20}%. Seeded and reported as a distribution over 200 seeds.
    3. **Report windows:** stratify trades within ±5 min of 08:30 / 10:00 / 14:00 ET releases. Report "ex-window" results as the primary figure.
    4. **Shock audit:** shock bars = 5m TR ≥ 3.5 × ATR(14, 5m) (and daily range ≥ 3.5 × ATR(10 d) for overnight holds). Compute the strategy's P&L on shock bars. If the win share > 50%, replace each shock-bar P&L with −|mean shock-bar P&L| and re-report.
    5. **Slippage regression (window T):** fit the Kaufman regression to live or paper IBKR fills once ≥ 200 fills exist, and replace s0.
  - **Kill:** n/a. An idea fails the module if it is net ≤ 0 under u = 10% plus the scaled slippage, or under the shock flip.

#### A2-39 · Roll-honest composites on unadjusted @ES (infrastructure)
- **Implementation status (v2.4):** **Partial** — Reset arm only (roll reset in Gate0 / `_LevelStudy`; roll table [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46), [#51](https://github.com/ettiennehugo/MultiCharts.Net/pull/51), [#55](https://github.com/ettiennehugo/MultiCharts.Net/pull/55)); cash-terms and back-adjusted arms not coded.
- **Family · Source:** Gate 0 (prerequisite) · Analyst 2 Rev 6 · Keller B2 Ch 14.3 (back-adjusted profiles put levels at prices nobody traded; use actual traded prices and reset at the roll, or keep long-horizon structure in cash-index terms; Drill 14.3). **Grade B.**
- **Evidence:** **D** · **original A2 grade: B** (rescaled per §3.12).
- **Markets · TF:** all multi-session levels
- **Data · test window:** 1m · W-M (agreed split §2.0.1); scored with A2-33
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** n/a
- **Fails when:** n/a (adopt the version that wins A2-33)
- **Needs:** F-ROLL, F-COMP; cash-terms arm needs SPX index data (unverified in the feed)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** runs inside the A2-33 gate
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Why:** dxFeed `@ES` is unadjusted, so composites that span a roll include a raw calendar-spread gap.
  - **Rules:** (a) build every multi-session composite only from sessions of the current front contract (reset at the dxFeed roll date); (b) optional cash-terms version: store levels as (level − SPX close) at the build date and translate with today's basis (ES − SPX at 09:29) — only if SPX index data is available in the feed (unverified); (c) exclude the roll session ±1 from all tests.
  - **Test:** Drill 14.3: reset vs cash-terms vs MC Custom Futures back-adjusted composite; score each with the A2-33 level study.
  - **Kill:** none; adopt the version that wins A2-33.

#### A2-33 · Level-persistence vs matched-control study (gate for every level idea)
- **Implementation status (v2.4):** **Partial** — `_LevelStudy` (PRs #15/#16/#20) + DVP kinds ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); Cox not implemented.
- **Family · Source:** Gate 0 (prerequisite) · Analyst 2 Rev 6 · Keller B2 Ch 10.6 (edge = E[reaction | level type, age] − E[reaction | control price at matched distance]; persistence curve; Drill 10.6). **Grade:** infrastructure; outcome decides which level types to keep.
- **Evidence:** **n/a (infrastructure; Keller B2 matched-control method)** · **original A2 grade: infrastructure; outcome decides which level types to keep** (rescaled per §3.12).
- **Markets · TF:** ES/NQ (then CL/GC/ZN); prior-session levels aged 1–15 sessions, 5m reaction window
- **Data · test window:** 1m · W-M (agreed split §2.0.1); in-sample (dev) years only for the decision, confirmed on validation; multi-session levels built roll-honestly (A2-39)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every prior-session level of every type (thousands of level-days; 20 control prices each)
- **Fails when:** n/a (infrastructure); risk: too many level types → Holm correction removes power
- **Needs:** F-EVT (CS-8 logSignal), F-PROF, F-NPOC, F-HVN, F-COMP; ◐ A2-33 harness (`_LevelStudy`, PRs #15/#16, #20: matched controls, Kaplan–Meier ✓; Cox ✗); ✓ M-GMM (PR #18, PR #21/#24) for A2-25/26/27/32 quality strata
- **Readiness (v2.3b):** Buildable with caveats — `_LevelStudy` (PRs #15/#16, #20) gives matched controls, Holm and Kaplan–Meier level death; **Cox regression is not implemented** (KM + age buckets instead).
- **Gate (v2):** **Gate 0 (G0-d): its verdict per level type gates every level-based idea.** v2 extends its level list to ON high/low, IB edges, composite VAH/VAL, single-print shelves and DV level sources (accumulation/cluster)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Design:** for each prior-session level (POC, VAH, VAL, HVN, LVN, naked POC, A2-27/32 components) aged 1–15 sessions, and 20 control prices per level at 0.9–1.1× the same distance from the current price, measure over the next session: touch rate, reaction R = max|P(t+k) − level|/σ over K = 12 5m bars after first touch, volume attraction, and rejection asymmetry. Stratify by quality (A2-25 stability, A2-26 verdict, mixture π). Fit a Kaplan–Meier/Cox "level death" model.
  - **Decision rules:** edge ≈ 0 with |t| < 2 → stop marking that level type; edge > 0 with t > 3 that decays → stop using levels older than the measured half-life; edge only in the top-quality tercile → trade only that tercile. Holm correction across all level types.
  - **Data:** window M (in-sample years only for the decision; confirm on validation).
  - **Kill:** none (infrastructure), but every level-based idea inherits its verdict.
- **Extension · Keller B5 (v2.3):** G0-d adds the DVP level types `DvpVah/DvpVal/DvpPoc/DvpHvn/DvpLvn/DvpSigma/DvpConf` (20 matched controls at 0.9–1.1× distance). PS-25 (KD-01) is not read until its event-rail type returns |t| ≥ 2; KD-11 shares this run (§3.16).


### 3.1 Family PS: Profile structure (25)

#### PS-01 · Accepted balance break → first-pullback higher low (ABB-HL)
- **Implementation status (v2.4):** **Spec'd** — final-synthesis Spec 1; no strategy code.
- **Family · Source:** PS · final-synthesis Spec 1; Keller B1 card 3.3 ("escape is not entry; accept or retest"), B4 §8.4 ("balance break is the only end-to-end spec"); R1 P1; R5 T5.
- **Evidence:** **C (+)** for the family. K4: balance breaks travel ≥ 1 width 56.6% of the time, median 1.17–1.20 W; confirmation-type behaviours MFE/MAE 10.5 vs 0.70 for fades. Telonics: real levels are run through +3.7 to +11.6 pp more often than fakes. **The setup itself is untested.** Lit: Book (K4 is the author's own data) + Indep (Telonics behaviour).
- **Markets · TF:** ES/NQ (MES/MNQ), CL, GC; 5m signal, 1m trigger; composite of 3–5 RTH sessions.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); developing profile approximate on 1m, parity-checked in W-T; composites multi-day levels reset at rolls (≈5-session warm-up after each splice)
- **Why it might work:** Participants who sold the top of a multi-day balance (responsive sellers) and short-term range traders are offside once value is accepted outside. Their covering plus initiative buyers produce continuation. Entering on the *first pullback HL* rather than at the break lets the market show that the old edge has become support.
- **Rules:** *Filter:* `IsBalanced(RTH, n, 0.6)` true at the prior close; V-score ≥ +1 for longs (≤ −1 for shorts); the break happens 09:45–14:30. *Acceptance:* 2 closed 5m bars > composite VAH + δ. *Entry:* the first pullback that holds above composite VAH − 0.25 × W and prints a confirmed 1m/5m HL; buy-stop 1 tick above the HL confirming bar. *Stop:* max(1 tick below the HL, cost floor); hard invalidation on a 5m close back below composite VAH − δ. *Target:* composite VAH + 1.0 W (variant: next HVN − 2 ticks); move to break-even at +1 R.
- **Sweep:** n ∈ {3, 5}; acceptance bars ∈ {1, 2, 3}; target ∈ {1.0 W, next HVN, trail EX-05} → 18 configs.
- **Frequency (est.):** 2–6 per month per index pair; rarer on CL/GC.
- **Fails when:** a news-driven break gets retraced (K3 gap tree); the pullback is too deep and the HL forms back inside value; ES/NQ correlation doubles risk.
- **Needs:** F-COMP, F-SESS, F-PROF (V-score), F-HVN, F-TM; ✓ M-SWING (HL detection; `_PriceActionEngine`).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** controls K-FL (fake composite edges), K-RT, a no-acceptance version (enter on the first close beyond the edge), and K4's own baseline (the break-at-close version, PS-02). **Kill:** no lift over the PS-02 at-acceptance entry, or < 56% reach of 1.0 W net of slippage.
- **Holm · Keller B5 (v2.3):** member of the Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, family-wise α = 0.05, fixed before any dev read (§3.16).


#### PS-02 · Balance break at acceptance close (K4 end-to-end spec)
- **Implementation status (v2.4):** **Spec'd** — final-synthesis Spec 1A; no strategy code.
- **Family · Source:** PS · Keller B4 §8.4; final-synthesis Spec 1A rung.
- **Evidence:** **C (+)** family; this is the only end-to-end spec K4 itself reports, on FX/crypto, not CME. Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC, ZN; 5m / 30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work:** Same participants as PS-01, but entering immediately catches trades where no pullback ever comes (the strongest breaks).
- **Rules:** *Filter:* balance as in PS-01; V-score aligned (K4: "trade with value migration"); skip if composite profile entropy > 0.93 (K4). *Entry:* market at the open of the bar after the 2nd accepting 5m close. *Stop:* back inside the composite by 0.25 W, or the cost floor if larger. *Target:* 1.0 W; scale 50% at 1.0 W and trail the rest with EX-05.
- **Sweep:** acceptance bars ∈ {1, 2}; stop ∈ {0.25 W, 0.5 W}; entropy filter on/off → 8.
- **Frequency (est.):** as PS-01, but every qualifying break trades (roughly 1.5–2× as many trades).
- **Fails when:** wide W makes R tiny relative to cost (good) but stops wide (bad R:R vs 1.17 W median); late-day breaks run out of time.
- **Needs:** F-COMP, F-PROF, F-TM; ✓ entropy (`_ProfileStats.Entropy`, PR #18).
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Test:** pair with PS-01 in the **same slot** (`Variant`), which makes the entry style an ablation. K-FL, K-DR. **Kill:** mean R ≤ 0 with the 1.0 W target, or the result depends on the entropy filter alone (one threshold doing all the work = overfit risk).
- **Merged · A2-41 (Variant=BookSpec of PS-02: Keller B4 book-spec balance break with a balance-quality filter).** Original A2 grade: B (other asset class) for the base rates (FX 378 breaks, 56.6% reach 1 W, median 1.17 W; crypto 59.1%, 1.20 W). **C** for the quality filter, which the book itself never tested. CME: untested. → catalogue grade **C (FX/crypto base rates, other asset class) · H (quality filter)**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "Book-Spec Balance Break with a Balance-Quality Filter (variant of A1 PS-02)"):
  - **Source:** Keller B4 Ch 2.4, 7.1, 7.2, 7.7 ("the one fully specified setup": stop ~0.3 W back inside, scale at 1.0 W, run the rest; "balance quality = single highest-value open question"). **Grade:** B (other asset class) for the base rates (FX 378 breaks, 56.6% reach 1 W, median 1.17 W; crypto 59.1%, 1.20 W). **C** for the quality filter, which the book itself never tested. CME: untested.
  - **Differs from:** A1 PS-02 (0.25 W stop, entropy filter, 50% scale with EX-05 trail) and PS-01 (pullback entry). This is the book's exact geometry, and its new element is the quality filter. Run it in the **same slot as PS-02** as `Variant=BookSpec`. A2-30 (entropy-led breaks of 2-session ranges) is a separate trigger.
  - **Timeframes:** daily composite; 5m acceptance; 30m runner management.
  - **Quality score (computed at the prior close, known before the session):**
    - Q1 = total RTH volume traded inside [composite VAL, composite VAH] over the n balance sessions, divided by the median n-session RTH volume over the trailing 250 sessions (the book's "area under the histogram").
    - Q2 = share of composite volume within the composite VA (≥ 0.70 by construction; higher = more concentrated agreement); also the composite's D-shape flag (single KDE mode).
    - Quality pass = Q1 ≥ its trailing-250-session median **and** a unimodal composite. Report all four quartile buckets of Q1, not just the pass/fail split.
  - **Filter:** balance true at the prior close; V aligned with the break direction (VA-midpoint migration up for long breaks, down for short breaks; neutral = no trade in the base arm); break occurs 09:45–14:30 ET.
  - **Entry (long):** two consecutive closed 5m bars > composite VAH + δ. Buy at the open of the next 5m bar. Short is the mirror.
  - **Stop:** composite VAH − 0.30×W (initial; never widened). Skip if 0.30×W < 1.0×ATR(14, 5m) + 2 ticks (stop inside noise) or if 0.30×W > 1.5×ATR(14, daily).
  - **Target / management:**
    - 50% off at composite VAH + 1.0×W (limit).
    - After T1, move the runner's stop to entry. Trail it to (highest 30m close since entry − 0.5×W). Hard exit on a 30m close back inside the composite VA.
    - Time stop: the runner is flat at 15:55 on day 3 (overnight hold allowed only for MES/MNQ under the A2-40 overnight-margin check; intraday-only arm also reported).
  - **Parameters (coarse, 2×2×2 = 8):** n ∈ {3, 5}; acceptance closes ∈ {1, 2}; Q1 cut ∈ {median, 60th pct}. The 0.30 W stop and 1.0 W scale are fixed by the book, not swept.
  - **Test window:** M (1m, Oct 2017 – Oct 2026; IS to Dec 2021, validation 2022–23, holdout 2024–26). Tick re-check of entry and stop fills in window T.
  - **Tests:**
    1. Replicate the book's base rates on CME: P(reach 1.0 W) and median R in W units, every detected break including failures. Compare with FX 0.566 / 1.17.
    2. Quality ablation: expectancy Q1-top-half minus bottom-half, plus the book's untested "low inventory → no travel" prediction.
    3. Runner ablation: runner vs full exit at 1.0 W (the book's claim that a fixed 1.0 W exit truncates the paying tail).
    4. Controls: K-RT, K-FL (fake boxes of equal W shifted ±0.5 W), V-against arm reported separately.
  - **Kill:** generic criteria; **or** CME P(reach 1.0 W) < 0.50 with median R < 1.0; **or** quality top-minus-bottom < 0.05 W per trade (then drop the filter, keep the base only if PS-02 does not dominate it); **or** the runner arm's net R ≤ the full-exit arm's (then drop the runner).
- **Holm · Keller B5 (v2.3):** member of the Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, family-wise α = 0.05, fixed before any dev read (§3.16).


#### PS-03 · Confirmed failed break of value → rotation to the opposite edge
- **Implementation status (v2.4):** **Partial** — Naive-80% arm coded as G0-b canary PS03 (PR #20); confirmed-entry rule (final-synthesis Spec 2) not coded.
- **Family · Source:** PS · Keller B1 3.3 ("look-above-and-fail rotates to the opposite edge"), B2 15.4 (failed break = inventory trap); final-synthesis Spec 2 (FBIT); Dalton's 80% rule as the parent.
- **Evidence:** **B (−) for the naive form.** The 80% rule has PF ≤ 1.10 and completes 31.4% (K4); sweep fades are 49.9% vs 50.0% at fake levels (Telonics). **D** for the confirmed variant · untested. Lit: Indep (negative) + Book.
- **Markets · TF:** ES/NQ, CL, GC; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composite variant multi-day levels reset at rolls
- **Why it might work:** Breakout buyers above VAH are trapped once price re-accepts inside value. Their stop-outs fuel the rotation. The naive fade fails because most pokes are not traps, so the confirmation (LH + break of the post-poke swing low) is the whole point. *(Trapped-trader logic; the full treatment is deferred to the later trapped-trader round.)*
- **Rules:** *Filter:* prior-day VA (or composite) edge; the poke goes ≥ δ beyond the edge but **fails acceptance** (≤ 1 close beyond). *Entry:* after 2 closes back inside, the first 5m LH below the poke high; sell-stop 1 tick under the LH bar low. *Stop:* poke high + 1 tick (or floor). *Target:* T1 = value POC (b ≈ 1), T2 = level − 1.5σ (Keller B2) or the opposite edge.
- **Sweep:** closes back inside ∈ {1, 2}; target ∈ {POC, opposite edge, −1.5σ} → 6 (+ control).
- **Frequency (est.):** 1–3 per week per instrument.
- **Fails when:** trend days (the poke was the start of acceptance); value migrating with the poke direction.
- **Needs:** F-PROF, F-SESS, F-DEV; ✓ M-SWING (LH).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **canary pairing:** run the naive 80% rule (enter on re-entry, target POC) as `Mode=Canary`; it must show ≈ 0 edge, or the pipeline is suspect. K-FL. **Kill:** confirmed variant ≤ naive + 0.05 R, or ≤ 0 on CL/GC (drift-free).
- **Merged · A2-42 (PS-03 naive-80% canary resized as a 1-in-3, 3:1 trade (descriptive replication; the canary expectation is unaffected)).** Original A2 grade: B (other asset class) for the 31.4% FX / 28.4% crypto traverse rates (≈ 19,000 re-entries). **C** for the 3:1 geometry (exhibit ratios, selected). Pit-era CME day session is the original setting, but the book flags it as untested on modern index futures. → catalogue grade **C (FX/crypto traverse rates) · E (3:1 geometry, exhibit ratios)**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "80% Rule Resized as a 1-in-3, 3:1 Trade (variant of A1 PS-03's naive canary)"):
  - **Source:** Keller B4 Ch 2.1 and 5.7 ("the 80% rule is a 30% rule… it does not kill it, it reclassifies it"; risk ~0.25 W to make ~0.75 W; "take every signal"; strict close-out). **Grade:** B (other asset class) for the 31.4% FX / 28.4% crypto traverse rates (≈ 19,000 re-entries). **C** for the 3:1 geometry (exhibit ratios, selected). Pit-era CME day session is the original setting, but the book flags it as untested on modern index futures.
  - **Differs from:** PS-03 runs the naive rule (enter on re-entry, target the **POC**) as a canary that must show ≈ 0 edge. This idea uses the **far edge** as the target with a tight 0.25 W stop and the strict close-out. That is a different payoff geometry, so it gets its own test rather than the canary's expectation.
  - **Timeframes:** 30m TPO periods (classic) and 5m (sensitivity arm).
  - **Setup:** prior RTH VA [VAL0, VAH0], W0 = VAH0 − VAL0. Today's RTH opens outside the VA (above VAH0 or below VAL0) and inside the prior RTH range or ≤ 1.0×W0 outside it.
  - **Entry (from above, short):** the first closed 30m bar (09:30–14:30) that closes ≤ VAH0 − δ after the open was above VAH0. Sell at the next bar open. Classic-Dalton arm: require 2 consecutive 30m closes inside.
  - **Stop:** two-part, whichever triggers first:
    - hard stop at VAH0 + 0.25×W0 (resting order);
    - strict close-out: market exit on the first 30m close back above VAH0 (the book's failure definition, "losses small and fast").
  - **Target:** VAL0 + 1 tick (far edge). Flat at 15:55 if neither hit. Mirror long from below.
  - **Execution discipline:** no discretionary skip; every signal is taken (the book: selective taking destroys the 31% base rate). One attempt per side per session.
  - **Scaled variant (desk version, Ch 5.7):** three equal clips at VAH0 − δ, POC0 + 0.25×(VAH0 − POC0), POC0. Exit thirds at POC0, the midpoint POC0–VAL0, and VAL0. Same stops. Reported separately (it converts the binary into F, the fraction crossed).
  - **Parameters:** confirmation closes ∈ {1, 2}; bar TF ∈ {30m, 5m}; stop ∈ {0.20, 0.25, 0.33}×W0 → 12.
  - **Test window:** M.
  - **Tests:**
    1. Descriptive replication: CME P(far edge before close-out) and the full histogram of F, the fraction of VA crossed, with Hartigan's dip test for bimodality (the book's open question: bimodal = attraction, unimodal ≈ 0.3 = diffusion).
    2. Strategy vs K-RW: breakeven hit rate = stop/(stop + target distance) after costs. Require the hit rate CI to lie above it.
    3. Bootstrap max losing streak; report the risk per trade that keeps p95 drawdown ≤ 10%.
    4. K-FL with fake VAs (prior range midpoint ± W0/2).
  - **Kill:** generic criteria; **or** hit rate not above the cost-adjusted breakeven in validation; **or** p95 losing streak > 12 at the realised hit rate; **or** the F histogram is indistinguishable from the K-FL fake-VA histogram (no structure). Note that PS-03's canary expectation (≈ 0 for the POC-target naive form) is unaffected. Both can be true.
- **Merged · A2-47 (Variant=K4 of PS-03: confirmed failed auction aligned with value migration, target inside value).** Original A2 grade: C (B practitioner in the book; the fade-at-touch version is B-negative with MAE 6.3 / 12.6 ATR). → catalogue grade **E (book practitioner) · B (−) for the fade-at-touch version**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "Confirmed Failed Auction Aligned with Value Migration, Target Inside Value (variant of A1 PS-03 / A2-28)"):
  - **Source:** Keller B4 Ch 5.1, 5.5, 5.6 ("wait for the failed auction, not the touch"; "the continuation short after the return into value"; target the VA midpoint, not beyond it; two-stage resolution with poor lows), Ch 7.3 ("do not fade a high in a market whose value is migrating up", the most reliable single filter), and Ch 5.3 (failure diagnostic: volume did not accumulate in the new rows). **Grade:** C (B practitioner in the book; the fade-at-touch version is B-negative with MAE 6.3 / 12.6 ATR).
  - **Differs from:** PS-03 confirms with an LH + swing break and targets POC, opposite edge or −1.5σ. A2-28 works on mixture minor components. A2-47 adds three book-specific conditions: (i) the **volume-non-accumulation** test, (ii) a **hard value-migration gate**, and (iii) a **target capped inside value** (VA midpoint) with a poor-extreme override. Run it as `Variant=K4` in PS-03's slot.
  - **Timeframes:** prior RTH VA; 5m bars; 30m failure test.
  - **Excursion (short case):** price trades ≥ δ above VAH0 during 09:30–14:00 ET. X = excursion high so far.
  - **Failure confirmation (all, on closed bars):**
    1. Two consecutive 5m closes ≤ VAH0 − δ.
    2. Volume traded at rows > VAH0 since the excursion began ≤ 15% of the session's volume so far, **and** the developing VAL has not risen above VAH0 (the book's acceptance test failed).
    3. At most 3 closed 30m bars spent above VAH0 (to exclude a mature acceptance that later fades).
  - **Migration gate:** shorts only if the VA-midpoint migration is down or neutral (never when up). Longs (the failed auction below VAL0) only if it is up or neutral. Report the "against migration" arm as a negative control only.
  - **Entry:** sell at the open of the 5m bar after confirmation condition 1 completes (conditions 2 and 3 checked at that close).
  - **Stop:** X + 2 ticks (the failed excursion's extreme), floored at 1.0×ATR(14, 5m) from entry; skip if that floor is > 2.0×ATR5.
  - **Target:** VA midpoint (VAH0+VAL0)/2. **Poor-extreme override (low side):** for a long after a failed auction below value where the excursion low is a poor low (`IsPoorLow` on the developing profile), the target stays at the VA midpoint (never beyond). Record whether the low is revisited within 3 sessions (two-stage test). Flat 15:55.
  - **Parameters:** volume share cut ∈ {10, 15, 25}%; inside closes ∈ {1, 2} → 6.
  - **Test window:** M.
  - **Tests:** (1) Arm comparison: touch-fade (expected negative, canary) vs return-close only vs + volume test vs + migration gate. (2) Target comparison: VA midpoint vs opposite edge (the book predicts the midpoint is better supported). (3) K-DR: longs and shorts separately; replicate on CL/GC.
  - **Kill:** full arm ≤ PS-03 + 0.05R; **or** the migration gate adds < 0.05R; **or** generic criteria.

#### PS-04 · Prior-value magnet on a small newsless gap (Keller IX-1)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B3 card IX-1.
- **Evidence:** **D** · untested · Lit: Book. Related evidence: K4 naked-POC fill 85.9% but EV LABS shows ES fills at the same rate as distance-matched controls, so the magnet part is (0) until shown otherwise.
- **Markets · TF:** ES/NQ, ZN; 5m / 30m TPO periods.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); skip the session after a roll splice (artificial gap)
- **Why it might work:** A small overnight gap into yesterday's value with no news is an inventory imbalance, not new information. Once RTH accepts back inside, overnight inventory is forced to rebalance toward yesterday's POC.
- **Rules:** *Filter:* no tier-1 news 18:00–09:30; |RTH open − prior close| ≤ 0.75 ATR20d; the open lies *inside* the prior VA. *Acceptance:* 2 thirty-minute periods (09:30–10:30) closing inside the prior VA. *Entry:* 10:30 bar close, in the direction of the prior POC. *Stop:* beyond the session extreme made so far (or floor). *Target:* T1 prior POC; T2 opposite prior VA edge; time stop 13:00.
- **Sweep:** gap cap ∈ {0.5, 0.75} ATR20d; acceptance periods ∈ {1, 2} → 4.
- **Frequency (est.):** 3–6 per month per instrument (only when the open is inside value, not at POC, and no news).
- **Fails when:** the open is already at the POC (no room), or the session trends from inside value.
- **Needs:** F-SESS (gap), F-PROF, F-CAL; ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29; scheduled events only) (overnight news flag).
- **Readiness (v2.3b):** Buildable with caveats — the tier-1 overnight-news filter is incomplete (scheduled events only): scheduled overnight releases can be flagged via ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29); unscheduled overnight news stays gated. Caveat: needs ETH bars. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:** K-FL on the prior POC (distance-matched price). **Kill:** POC hit rate within 2 pp of the distance-matched control (this is exactly EV LABS' null).

#### PS-05 · Rejected repricing open (Keller IX-3 · Spec 3)
- **Implementation status (v2.4):** **Spec'd** — final-synthesis Spec 3; no strategy code.
- **Family · Source:** PS · Keller B3 IX-3 (rated B+ by the author); final-synthesis Spec 3.
- **Evidence:** **D** · untested · Lit: Book. Adjacent evidence: open-drive continuation 62.5% (n=793); one-way opens positive in 7/7 markets (R1/R2).
- **Markets · TF:** ES/NQ; 1m/5m around the open.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); skip roll-splice sessions
- **Why it might work:** An open outside prior value is a repricing attempt. If RTH immediately rejects it (re-enters prior value and holds), the overnight participants who drove the gap are trapped. The rejection then tends to carry through value.
- **Rules:** *Filter:* open outside prior VA by ≥ 0.25 ATR20d; gap ≤ 1.5 ATR20d. *Signal:* by 10:15, two 5m closes back inside the prior VA. *Entry:* stop-order through the low (for gap-up) of the second inside bar. *Stop:* session extreme + 1 tick. *Target:* prior POC, then the opposite VA edge.
- **Sweep:** gap-size lower bound ∈ {0.25, 0.5}; signal deadline ∈ {10:15, 10:45} → 4.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** news-driven gaps (re-acceptance is temporary); the gap is in the direction of a multi-day trend.
- **Needs:** F-SESS, F-PROF, F-DEV; ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29).
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. Caveat: needs ETH bars. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:** pair with PS-06 (its mirror) in one slot. K-RT restricted to gap days. **Kill:** ≤ 0 R, or no better than the random-time entry on the same days.

#### PS-06 · Defeated adjustment → continuation (Keller IX-2)
- **Implementation status (v2.4):** **Spec'd** — final-synthesis Spec 3 (mirror); no strategy code.
- **Family · Source:** PS · Keller B3 IX-2; final-synthesis Spec 3 mirror.
- **Evidence:** **D** · untested · Lit: Book. Adjacent: open-drive 62.5% continuation (B).
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); skip roll-splice sessions
- **Why it might work:** An open outside value first *tests back* toward the prior VA (the "adjustment"). If the test fails to get inside, the responsive participants are defeated and the repricing is ratified.
- **Rules:** *Filter:* gap as in PS-05. *Signal:* by 10:30, price retraces ≥ 50% of the gap but **no** 5m close inside the prior VA, then a confirmed 5m HL (gap up). *Entry:* buy-stop above the HL bar. *Stop:* HL − 1 tick (or floor). *Target:* 1.0 × gap size beyond the open, or trail EX-05.
- **Sweep:** retrace threshold ∈ {38%, 50%}; target ∈ {1 × gap, trail} → 4.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** a late re-entry into value after 10:30.
- **Needs:** as PS-05.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** same slot as PS-05. **Kill:** combined PS-05/06 ≤ 0 R, or one leg carries everything (then keep only that leg, charged as a new configuration).

#### PS-07 · Open-drive / one-way open: first-pullback continuation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · R1 S5; Dalton opening types; Keller B2 15.7 ("do nothing 0–5 min").
- **Evidence:** **B (+) for the behaviour:** open-drive continuation 62.5% (n=793); one-way opens positive in 7/7 markets; first-candle direction worth about +0.1 R (Krueger ORB replication). **Setup untested.** ORB as a whole nets about 0 (B, 0). Lit: Indep.
- **Markets · TF:** ES/NQ, CL (09:00 open), GC (08:20 open); 1m/3m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 1m granularity is enough for a 15-min classification (tick-precise re-check in W-T)
- **Why it might work:** An open-drive means one side (often overnight-informed or institutional) is executing urgently. Late responsive traders who fade the drive are forced out on the first pullback failure.
- **Rules:** *Classify at 09:45:* open-drive = the first 15 minutes trade one-way (the open is within 10% of the range extreme opposite the drive) and the range ≥ 0.6 × median 15-min opening range (20 sessions). *Entry:* the first pullback of ≥ 30% of the drive that prints a 1m/3m HL; buy-stop above it. *Stop:* HL − 1 tick (or floor). *Target:* 1.0 R / 1.5 R / trail; time stop 11:30.
- **Sweep:** range threshold ∈ {0.6, 1.0}; target b ∈ {1, 1.5, trail} → 6.
- **Frequency (est.):** 3–6 per month per instrument.
- **Fails when:** the drive is news-driven and mean-reverts; low-volatility regimes (ORB negative in 2015–17 per R2).
- **Needs:** F-SESS, F-TM; ✓ M-TOD (opening-range median).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT (random pullback entries on drive days only), K-DR (shorts and CL/GC). **Kill:** lift over "enter at 09:45 market in the drive direction" < 0.05 R.

#### PS-08 · IB extension with acceptance (narrow-IB tier)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · R1 S2/P3; Dalton IB.
- **Evidence:** **B (0/+)** descriptive. The first IB break fails 34% of the time; Zarattini's noise-band ORB is decayed (C). Setup untested. Lit: Indep (descriptive).
- **Markets · TF:** ES/NQ; 5m / 30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A narrow IB means other-timeframe participants haven't committed. Accepted range extension means they have, and responsive IB-edge faders are offside.
- **Rules:** *Filter:* IB range ≤ 33rd percentile of the last 20 IBs. *Signal:* 2 closed 5m bars beyond IB ± δ after 10:30. *Entry:* first 5m HL above IBH (or market on the 2nd close in the variant). *Stop:* back inside the IB by 0.25 × IB range (or floor). *Target:* 1.0 × IB range beyond the IB; trail.
- **Sweep:** IB tier ∈ {33rd, 50th}; entry ∈ {HL, close} → 4.
- **Frequency (est.):** 4–8 per month.
- **Fails when:** the C-period failure (failed C-period on 30% of days, R1).
- **Needs:** F-SESS, F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** compare against the unconditional IB break (all IB widths). **Kill:** the narrow-IB tier adds < 0.05 R over unconditional.
- **Merged · A2-08 (variant B entry):** anchor an AVWAP at the 1m bar that first trades beyond the IB high after 10:30; filter IB ≤ 40th pct and the prior day not a trend day; after 3 consecutive 5m closes above the IB high, enter on the first pullback whose 5m low stays ≥ AVWAP_IBbreak, then a 1m close above the prior 1m high. Stop: lower of AVWAP − 0.2×ATR5 and IB high − 2 ticks. Targets IB high + 1.0/2.0 × IB range; exit on a 5m close below the AVWAP. Drop it if > 60% event overlap with PS-05/06 continuation without being superior (A2's own rule). Flat 15:55 (A2). *(Grades: A2-08 original **C** → catalogue **inherits host B (0/+)**.)*

#### PS-09 · IB C-period failure → IB midpoint
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · R1 S2.
- **Evidence:** **B (0)** descriptive (failed C-period on ~30% of days; first break fails 34%) · trade untested. Lit: Indep (descriptive). Trapped-trader flavour.
- **Markets · TF:** ES/NQ; 5m/30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** Breakout buyers in the C period are trapped when price returns inside the IB.
- **Rules:** *Signal:* the C period (10:30–11:00) trades ≥ δ beyond the IB but closes back inside. *Entry:* sell-stop under the C-period close bar (for an upside failure). *Stop:* C high + 1 tick (or floor). *Target:* IB midpoint; time stop 13:30.
- **Sweep:** penetration ∈ {δ, 0.25 IB}; target ∈ {IB mid, opposite IB edge} → 4.
- **Frequency (est.):** 4–6 per month.
- **Fails when:** trend days.
- **Needs:** F-SESS.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL on the IB edges. **Kill:** ≤ 0 R; queue it for the trapped-trader round if marginal.

#### PS-10 · Developing-POC migration join (VAH-as-support pullback)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 ("POC migration ratifies the move"); Dale VP trend cluster. Extended by the author.
- **Evidence:** **D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC; 5m / 15m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); developing profile approximate on 1m, parity-checked in W-T
- **Why it might work:** When the *developing* POC migrates, the bulk of today's business is being done at higher prices. Late shorts at the old value are offside, and the developing VAH turns into the floor that new business defends.
- **Rules:** *Filter:* developing POC (Own profile) has moved ≥ 0.5 × ATR5 × 6 (≈ 3 ATR of 5m bars) above the IB-end POC; after 11:00. *Entry:* the first pullback to within δ of the developing VAH that prints a 5m HL. *Stop:* below the developing VAL or HL − 1 tick, whichever is nearer but ≥ floor. *Target:* session high + 0.5 × IB range, or trail EX-05.
- **Sweep:** migration threshold ∈ {2, 3} ATR5; entry level ∈ {dev VAH, dev POC} → 4.
- **Frequency (est.):** 3–6 per month.
- **Fails when:** P-shaped short-covering days where the POC migrates but the move is exhausted (Dalton's "short covering"; K1).
- **Needs:** F-DEV (developing VA/POC, already audited), F-SESS; ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (shift the developing VAH ±0.5 ATR5); compare to the same pullback without the POC-migration filter. **Kill:** the filter adds < 0.05 R.
- **Merged · A2-18 (variant B):** condition on the developing POC migrating above sVWAP + 0.3×ATR30 and holding for 2×30m bars (value migrating faster than the average); entry = the first 5m pullback into the (new POC, sVWAP) zone + a 1m BOS; stop sVWAP − 0.2×ATR30; target the nearer of prior VAH/high or an IB extension of 1.0 if ≥ 1 R; flat 15:55 (A2). Control: POC migration without the sVWAP comparison. Kill (A2): no lift over that control, plus A2's generic kill (§2.3). *(Grades: A2-18 original **C** → catalogue **D**.)*

#### PS-11 · Unratified new extreme (POC refuses to follow) → confirmed fade
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 (POC refusal = unratified move); R4. Author's operationalisation.
- **Evidence:** **D** · untested · Lit: Book. The fade family is (−) by default (Telonics).
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); developing profile approximate on 1m, parity-checked in W-T
- **Why it might work:** A new session high without the developing POC following is a probe that has not attracted business. Late breakout buyers are long at prices nobody else values.
- **Rules:** *Filter:* new session high ≥ 1.5 × ATR5 above the developing POC; the developing POC has not moved up during the last 60 min; after 11:00. *Entry:* a confirmed 5m LH plus a close below the developing VAH. *Stop:* session high + 1 tick (or floor). *Target:* developing POC.
- **Sweep:** POC stall window ∈ {30, 60} min → 2.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** a trend day where the POC catches up later.
- **Needs:** F-DEV; ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT on days with new highs. **Kill:** ≤ 0 R, or same as the plain LH-after-new-high entry.

#### PS-12 · Trend-cluster pullback (first touch of an in-trend volume cluster)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 card 3.1; Dale VP (trend cluster), Dale OF setup #1/#2 (volume clusters, multiple nodes; 30m bars, price moves ≥ 1–2 footprints away, first touch only).
- **Evidence:** **E** (Dale) / **D** (Keller) · untested. Negative adjacent evidence: a POC-pullback entry is worse than a 50% retracement (Tickfloor, crypto). Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC; 30m clusters, 5m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) with cluster prices from 1m volume-at-price (approximate); footprint-exact clusters only in W-T; multi-day levels reset at rolls
- **Why it might work:** A cluster formed during a pause in a trend is where the trend's participants added. The first revisit is defended by those same participants, so trapped counter-trend traders give up.
- **Rules:** *Filter:* 60m trend state up (M-SWING HH/HL); a 30m bar whose single-price volume ≥ 2 × the bar's median row volume, inside a pause (bar range ≤ 0.7 × ATR30m). *Validity:* price closes ≥ 2 thirty-minute bars away. *Entry:* the first touch of the cluster price ± δ, then a 5m HL (confirmed version) **or** a limit at the cluster (the Dale version, as a variant). *Stop:* beyond the next LVN below the cluster (Dale/Levy), ≥ floor. *Target:* prior swing high, or "before the next cluster" (Dale).
- **Sweep:** cluster multiple ∈ {2, 3}; entry ∈ {limit, HL} → 4 (the multiple-node, two-bar variant of Dale #2 = 2 more).
- **Frequency (est.):** 3–8 per month.
- **Fails when:** the cluster was distribution, not accumulation; limit fills are adversely selected.
- **Needs:** F-HVN (on per-bar profiles), F-PROF; ✓ M-SWING; ✓ per-bar profile ladder (`_BarLadderStore`, `_VolumeProfileBase.BarLadders`, PR #18; 30m via `htfStreams` Data2+).
- **Readiness (v2.3b):** Buildable now on the merged base (configure the 30m stream in `BarLadderConfig.htfStreams`).
- **Test:** K-LM (limit vs confirmed is the core question), K-FL, a 50%-retracement entry as benchmark (Tickfloor). **Kill:** not better than a plain 50% retracement entry of the same swing.
- **Merged · DV-04 (Trader Dale video: trend-cluster pullback with S/R-flip confluence; Dale priority 4).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Agree (continuation) but first-touch limit. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:**
    - https://www.youtube.com/watch?v=Oe3DEoRlH3Q (Trend setup)
    - https://www.youtube.com/watch?v=RFVY939jV0c
    - https://www.youtube.com/watch?v=ehBimmFB_Jo ("My #1 setup": trend cluster + the level reacted before = S/R flip)
    - https://www.youtube.com/watch?v=rzilCi2wtXY (order-flow version, 5–60m, prefers 30m)
    - https://www.youtube.com/watch?v=XPk1nrsDaYc (30m cluster, 5m confirmation)
    - https://www.youtube.com/watch?v=nbdLCKBGv7k, https://www.youtube.com/watch?v=MJKANontC4s
  - **Evidence:** E. Adjacent negative: a POC-pullback entry was worse than a 50% retracement (Tickfloor, crypto; see PS-12).
  - **Markets · TF [Dale]:** 30m; forex/ES. **[coding]** ES/NQ, 6E; 30m signal, 1m fill.
  - **Data:** W-M (cluster detection), W-T for the order-flow variant.
  - **Why [Dale]:** "big trading institutions were adding to their longs there… they defend… and push the price".
  - **Rules:**
    - *Trend [Dale]:* "strong trend". **[coding]** M-SWING state HH/HL (60m) or 30m close > 20-bar high within the last 10 bars; leg ≥ 3 × ATR30m.
    - *Cluster [Dale]:* a "bump on the volume profile" inside the trend leg (flexible profile over the trend only). In the order-flow version it is a block of ≥ ~5 dark (heavy) cells.
      - **[coding]** Local HVN on the leg profile with peak volume ≥ 2 × median row volume of the leg (PS-12 definition).
      - Level = the HVN peak (Oe3D) or the near edge, "beginning of the zone" (ehBim, rzil).
    - *Validity [Dale]:* "price makes some price action above that volume cluster" before the pullback (not an instant test); the pullback should come within "a week, two weeks max".
      - **[coding]** ≥ 2 closed 30m bars entirely beyond the level before arming; expiry 10 sessions.
    - *#1 confluence [Dale]:* "there was a strong reaction to that level… in the past" (old resistance → new support). **[coding]** A prior confirmed swing (M-SWING) whose extreme lies within ±0.25 × ATR30m of the level and predates the leg; reaction ≥ 1 × ATR30m.
    - *Entry:* limit at the level, with-trend, first touch [Dale]. Variant [Dale, XPk1]: wait for a 5m order-flow confirmation near the level (DV-16).
    - *Stop / Target:* 12/10 pips or behind the cluster / before the next HVN (DV-24) [Dale]. **[coding]** As DV-03.
  - **Sweep:** level ∈ {peak, near edge}; flip-confluence ∈ {required, not required}; entry ∈ {limit, 5m confirmation close} → 8.
  - **Frequency (est.):** ES 30m about 4–8 per month without the flip filter, about 1–3 per month with it.
  - **Fails when:** the trend ends (the pullback is the reversal); the cluster is news volume; a late-trend cluster.
  - **Needs:** ✓ M-SWING, F-HVN, range profile (as DV-03), F-TM; F-OF (variant, W-T).
  - **Readiness (as of merge):** Buildable now on the merged base.
  - **Test:**
    - K-FL, K-ST (Dale says stale touches are worse); compare with PS-12; K-LM for limit vs confirmation.
    - **Kill:** limit ≤ fake levels, and the confirmation variant shows no lift.
  - **Books:**
    - **Agree** with continuation (P1) and the Dale book (#2 "best with support-becomes-resistance").
    - Conflict: the blind limit (P2).
  - **Priority 4.**

#### PS-13 · Volume-accumulation breakout: first-touch retest
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Dale VP setup "volume accumulation".
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL; 15m/30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999) (200-day ATR on a roll-gap-removed daily series); multi-day levels reset at rolls
- **Why it might work:** A rotation with a heavy-volume price that is then left with a strong move marks where the move's sponsors built positions. They defend the first retest.
- **Rules:** *Filter:* a rotation of ≥ 6 bars on 15m with range ≤ 1.5 × ATR15m, then a breakout bar closing outside with range ≥ 1.5 × ATR15m. *Level:* the highest-volume price in the rotation. *Entry:* first touch (limit) or first 5m HL after touch; first touch only (Dale discard rule; level valid 30–40 days on daily, 5 sessions intraday). *Stop:* 10–20% of the 200-day daily ATR (Dale), floored. *Target:* before the next cluster.
- **Sweep:** stop ∈ {10%, 20%} of ATR200d; entry ∈ {limit, HL} → 4.
- **Frequency (est.):** 2–6 per month.
- **Fails when:** the breakout fails (then the level is resistance, not support).
- **Needs:** F-HVN, F-PROF; ✓ M-DAILY (`DailyConfig.atrLength` = 200).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL, K-ST (touches after a level has already been tested; Dale says these should be worse; testable). **Kill:** first touches ≤ stale touches.
- **Merged · DV-03 (Trader Dale video: volume accumulation (rotation → trend, first retest); Dale priority 4).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Mostly agree. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:**
    - https://www.youtube.com/watch?v=zMfGDv7yALg
    - https://www.youtube.com/watch?v=9V1UEqvjJlc
    - https://www.youtube.com/watch?v=grEU59ExG_k (setup #1 of "Top 3", 30m)
    - https://www.youtube.com/watch?v=c3EgcoAgyyM (logic)
    - https://www.youtube.com/watch?v=dqsI3FmVUJA and https://www.youtube.com/watch?v=gXmdBGQBAP8 (level at the "beginning of the heavy volume zone")
  - **Evidence:** E (his 66% combined figure covers this plus 2 other setups). The family PS-01/PS-13 is C(+) for breaks travelling. **The setup itself is untested.**
  - **Markets · TF [Dale]:** 30m, forex + ES/NQ; "works with all time frames". **[coding]** 15m/30m signal; 1m execution.
  - **Data:** W-M; multi-day rotations reset at rolls.
  - **Why [Dale]:** institutions "accumulate… in rotations… unnoticed"; then push the trend; on the pullback they "defend" their positions. "The longer the rotation… the stronger the trend."
  - **Rules:**
    - *Rotation [Dale]:* "sideways price action or price channel… followed by strong buying or selling".
      - **[coding]** ≥ R bars (30m) with high−low ≤ w × ATR30m(14);
      - then a breakout bar closing outside the box with range ≥ 1.5 × ATR30m;
      - then follow-through: price reaches ≥ 1 × box width beyond the box before any retest (my reading of "strong trend activity").
    - *Level [Dale]:* "where the heaviest volumes got accumulated" in the rotation, via a flexible profile over the rotation only. Newer videos say "the beginning of the heavy volume zone" (the near edge of the HVN facing price). A wide vs tight rotation box giving the same peak is a "bonus".
      - **[coding]** Variant A = rotation POC; variant B = near edge of the HVN containing the POC (F-HVN KDE edge).
    - *Entry [Dale]:* limit, "first touch", "in the direction of the trend", "markets have great memory" (no expiry stated). **[coding]** Limit at the level; first touch only; expiry ≤ N sessions.
    - *Stop / Target [Dale]:* 12 / 10 pips standard (older videos), or 10–20% ADR. In later videos the SL goes behind the heavy-volume zone and the TP before the next barrier (DV-24). **[coding]** Bracket f × ATR20d; variant = DV-24.
  - **Sweep:** R ∈ {6, 12} bars; level ∈ {POC, near HVN edge}; exit ∈ {fixed f = 0.15, DV-24} → 8.
  - **Frequency (est.):** ES 30m: about 3–6 per month; 6E: similar.
  - **Fails when:** a stop-cluster below the level (DV-20); the breakout is news-driven and fully retraced; the retest is the start of a new balance.
  - **Needs:** F-HVN, F-PROF on an arbitrary bar range (flexible profile: ✓ `_RangeProfile` / `RangeProfileAfter`, PR #18), F-COMP, F-TM.
  - **Readiness (v2.3b):** Buildable now on the merged base (`_RangeProfile`, PR #18).
  - **Test:**
    - K-FL, K-ST, K-RT; compare with PS-13 (same idea) and PS-02 (enter at the break).
    - **Kill:** first touches ≤ stale touches, or no lift over K-FL.
  - **Books:**
    - **Mostly agree.** It is a retest of a balance break: continuation, and P1 is satisfied by the follow-through requirement.
    - Conflict: the entry is a blind limit at a level (P2). The Dale book (#1 "no confirmation") and Keller ("escape is not entry; accept or retest") agree on the retest.
  - **Priority 4.**

#### PS-14 · Rejection-ledge revisit fade
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 card 3.2; Dale VP "rejection"; Dale OF #1 (rejection cluster).
- **Evidence:** **D/E** · untested · fade family (−) by default. Lit: Book.
- **Markets · TF:** ES/NQ, GC; 5m/30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A high-volume spike at an extreme that reverses fast shows a large responsive participant. On a *slow* revisit the same participant may still be there.
- **Rules:** *Level:* a bar at a session or swing extreme whose volume z ≥ 2 (clock-minute normalised), whose close is in the opposite 30% of its range, and whose next 3 bars move ≥ 1 ATR5 away. *Revisit:* only after ≥ 12 bars, approaching with falling range (the last 3 bars' average range < 0.8 × ATR5). *Entry:* fade on the first rejection close at the level ± δ. *Stop:* level + floor. *Target:* developing POC / 1 R.
- **Sweep:** volume z ∈ {2, 3}; revisit speed filter on/off → 4.
- **Frequency (est.):** 2–5 per month.
- **Fails when:** the revisit is a breakout run (Telonics: run-throughs dominate at real levels).
- **Needs:** F-DEV, ✓ M-TOD (volume z).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL, K-RT; record run-through rate vs fake levels (the expected failure). **Kill:** run-through rate higher at real than at fake levels → reverse the idea into a continuation (as a new, budgeted config).
- **Merged · DV-05 (Trader Dale video: rejection HV-zone retest with the 4-point checklist; Dale priority 2).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Conflict (fade at level). Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:**
    - https://www.youtube.com/watch?v=jBns3QbU5qk
    - https://www.youtube.com/watch?v=cBcL8e5r_lU
    - https://www.youtube.com/watch?v=4f4s_WmIlY0
    - https://www.youtube.com/watch?v=u88dEb7ErCU (only take rejections with FVGs)
    - https://www.youtube.com/watch?v=grEU59ExG_k (setup #3)
  - **Evidence:** E.
  - **TF:** 30m [Dale].
  - **Why [Dale]:** aggressive sellers "rejecting higher prices… adding… at the volume cluster… will defend".
  - **Rules:**
    - *Checklist [Dale]* (≥ 3 of 4 "still good"):
      1. market rotating;
      2. rejection strong (a clear single high/low);
      3. volumes "distributed well" (an HVN with an LVN beyond it toward the extreme);
      4. an FVG in the departure.
    - **[coding]**
      - (1) RG-02 balance = true over the prior 20 bars;
      - (2) the rejection swing extreme is a single bar extreme; the bar's wick ≥ 0.5 × range; departure ≥ 2 × ATR30m within 4 bars;
      - (3) the range-profile over the rejection leg has an HVN with an LVN (≤ 0.3 × peak) between it and the extreme;
      - (4) DV-08 FVG ≥ 0.3 × ATR30m in the departure.
    - *Level [Dale]:* "resistance is not at the top of the rejection. It is the heavy volume zone… beginning of the heavy-volume cluster… ideally also the beginning of the FVG".
    - *Entry:* limit at the level, first touch [Dale].
    - *Stop / Target:* standard bracket / DV-24.
  - **Sweep:** checklist score ∈ {3, 4}; level ∈ {HVN edge, FVG start} → 4.
  - **Frequency (est.):** 2–5 per month per market.
  - **Fails when:** the rejection is a stop-run of a weak high (DV-20) inside a trend.
  - **Needs:** range profile, F-HVN, RG-02, ✓ FVG detector (`_FvgDetector`, PR #18); range profile ✓ (PR #18).
  - **Readiness (v2.3b):** Buildable now on the merged base (`_FvgDetector` + `_RangeProfile`, PR #18).
  - **Test:** K-FL, K-ST; ablate each checklist item (does each item add lift?). **Kill:** checklist ≥ 3 ≤ checklist < 3.
  - **Books:** **Conflict.** It is a fade at a level without confirmation (P1). Matches the Dale book #3 ("hardest") and PS-14. **Priority 2.**

#### PS-15 · LVN traverse after acceptance into the void
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 (LVN = air pocket); Levy ("black hole").
- **Evidence:** **C (0)**. K4 finds little traverse difference between thin and thick rows (1.19 vs 1.23 ATR). Levy: E. Lit: Book.
- **Markets · TF:** ES/NQ, CL; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 5-session composite multi-day levels reset at rolls
- **Why it might work:** Little volume traded inside an LVN means few participants have positions there to defend, so price should travel through it quickly to the next HVN.
- **Rules:** *Level:* composite (5-session) LVN with width ≥ 0.5 × ATR5. *Signal:* a 5m close beyond the LVN's near edge, then beyond its midpoint, within 3 bars. *Entry:* market at the next bar. *Stop:* back across the near edge, ≥ floor. *Target:* far edge of the LVN (next HVN edge − 2 ticks).
- **Sweep:** LVN width ∈ {0.5, 1.0} ATR5 → 2.
- **Frequency (est.):** 4–10 per month.
- **Fails when:** K4 is right (no thin-row effect); news spikes.
- **Needs:** F-HVN, F-COMP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **mandatory comparison to the same entry rule in HVN zones** (the K4 null). **Kill:** traverse-to-target rate no higher than in the HVN control.
- **Merged · A2-44 (post-crossing fast-LVN continuation (variant of PS-15)).** Original A2 grade: B (other asset class) **against** the anticipatory version; C for the confirmed version. → catalogue grade **C (−) against the anticipatory version · E (confirmed version)**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "Post-Crossing Fast-LVN Continuation (variant of A1 PS-15)"):
  - **Source:** Keller B4 Ch 2.3 (speed claim rejected: 1.19 vs 1.23 bars/row FX, 1.18 vs 1.25 crypto) and Ch 6.4 ("the fast traverse is a confirmation, not a prediction"; only 8.5% FX / 10.3% crypto of LVN crossings are fast; exhibits 7.0 / 8.45 ratio). **Grade:** B (other asset class) **against** the anticipatory version; C for the confirmed version.
  - **Differs from:** PS-15 enters at the LVN **midpoint** expecting speed (the book says this is wrong 9 times out of 10). A2-27 traverses a mixture void from a prior session. A2-44 enters only **after** the whole LVN has been crossed, and only if that crossing was fast by the trailing distribution.
  - **Timeframes:** 5-session composite LVNs; 1m crossing clock; 5m entry.
  - **Setup:** a composite LVN (KDE trough between two HVNs) with width L ≥ 0.5×ATR(14, 5m). Edges are the rows where density returns to ≥ 50% of the lower adjacent peak.
  - **Crossing measurement (causal):** t0 = first 1m close beyond the near edge, t1 = first 1m close beyond the far edge. Speed s = L / (t1 − t0 minutes). Fast = s ≥ the 90th percentile of all LVN crossings over the trailing 120 sessions (matches the book's ~10% selection fraction). The percentile table is rolled forward daily.
  - **Entry (long):** at t1, if fast, and if the next closed 5m bar also closes beyond the far edge. Buy at the next 5m open. Window 09:45–14:30 ET. Mirror short.
  - **Stop:** LVN midpoint − 1 tick, floored at 0.75×ATR(14, 5m) from entry.
  - **Target:** T1 50% at far edge + 1.0×L (or the first confluent HVN from A2-43, whichever is nearer). Runner trails at the lowest low of the last 3 closed 5m bars. Flat 15:55.
  - **Filter:** V not against the direction (skip if VA-midpoint migration is opposite).
  - **Parameters:** speed pct ∈ {80, 90}; L floor ∈ {0.5, 1.0}×ATR5; extra 5m hold close ∈ {0, 1} → 8.
  - **Test window:** M; T for a tick-accurate re-check of t0/t1 timestamps and fills.
  - **Tests:** (1) Replicate the CME selection fraction and the HVN/LVN bars-per-row ratio. (2) Fast-vs-slow crossings, same entry rule (the core control). (3) Fast crossings of equally sized **non-LVN** price ranges (is it the LVN or just speed/momentum?). (4) PS-15 midpoint entry in the same slot.
  - **Kill:** fast-LVN arm ≤ fast-non-LVN arm + 0.05R (it is momentum, not the node, so fold it into a momentum idea), **or** generic criteria.

#### PS-16 · LVN slow-approach bounce
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Levy (LVN "bounce if approached slowly").
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, GC; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 5-session composite multi-day levels reset at rolls
- **Why it might work:** Levy's claim is that a slow, low-momentum approach to an LVN signals no initiative behind the move, so the LVN edge acts as a rejection zone.
- **Rules:** *Filter:* approach to a composite LVN edge with the last 6 bars' range < 0.7 × ATR5 and volume z < 0. *Entry:* first 5m rejection close (wick into the LVN, close back out). *Stop:* beyond the LVN's far edge (Levy: "stops beyond the LVN"), floored. *Target:* the HVN behind price (developing POC).
- **Sweep:** slowness threshold ∈ {0.6, 0.8} → 2.
- **Frequency (est.):** 3–8 per month.
- **Fails when:** the slowness is lunchtime liquidity, not exhaustion.
- **Needs:** F-HVN, ✓ M-TOD.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** pair with PS-15 (fast vs slow approach) in one slot: the two are the same level split by speed, which is the cleanest test of Levy's claim. **Kill:** no difference by speed.

#### PS-17 · Double-distribution waist trade
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Levy (double distribution: buy at the LVN between distributions); Keller B1 (B-profile waist).
- **Evidence:** **E/D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 30m TPO, 5m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); developing profile approximate on 1m, parity-checked in W-T
- **Why it might work:** A day that builds two distributions has moved value. The waist LVN separates the old and new value, and buyers of the new upper distribution defend the waist.
- **Rules:** *Filter:* the developing profile has shape `Double` (`ProfileShape`) after 12:00, with the upper distribution POC newer. *Entry:* a pullback to the waist LVN ± δ, then a 5m HL. *Stop:* inside the lower distribution by 0.25 × waist-to-lower-POC distance, floored. *Target:* the upper distribution's high / developing VAH. Next-day variant: the same level on the following session's first test.
- **Sweep:** intraday vs next-day → 2.
- **Frequency (est.):** 1–3 per month.
- **Fails when:** the second distribution was a news shock that reverses.
- **Needs:** F-DEV, F-PROF (ProfileShape on the *developing* profile; verify it works on Own-dev).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL. **Kill:** < 30 events in the W-M dev window → park as inconclusive; ≤ 0 R → kill.

#### PS-18 · P/b-profile stop-sweep buy (Levy)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Levy Ch10 (P-profile: buy ~3 ticks below the swing low on declining sell aggression).
- **Evidence:** **E** · untested · sweep fades are (0) per Telonics (49.9% vs 50.0%). Lit: Book. Trapped-trader flavour.
- **Markets · TF:** ES/NQ; 1m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) for shape/sweep; the delta condition only in W-T (MC-BidAsk) — in W-M it degrades to BarUpDown (Low) and is reported separately
- **Why it might work:** In a P-shaped session (short covering / new buyers above), a dip that runs the stops under the last swing low with *weakening* sell aggression is a liquidity grab, not new supply.
- **Rules:** *Filter:* developing shape `P`; after 11:00. *Signal:* price trades 1–4 ticks below a confirmed 5m swing low, and 1m bar delta (TickRule) is less negative than on the bars making that swing low. *Entry:* buy on the first 1m close back above the swing low. *Stop:* sweep low − 1 tick, floored. *Target:* developing POC / session high.
- **Sweep:** sweep depth ∈ {≤3, ≤6 ticks}; delta condition on/off → 4.
- **Frequency (est.):** 2–5 per month.
- **Fails when:** Telonics is right (sweep fades = coin flip); TickRule delta is noise.
- **Needs:** F-DEV, F-PROF, F-OF (quality ≥ Medium); ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-SH on the delta condition; the sweep fade **S7 canary** (expected ≈ 0) without the shape filter. **Kill:** no lift over the canary.
- **See also (v2.7):** SP-09 is another sweep-fade (clock window + FVG). The Telonics 49.9% vs 50.0% prior cited here applies to it. This entry's rules are unchanged.

#### PS-19 · Poor high/low run-through (not repair-as-magnet)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 card 3.5 (poor extreme = repair candidate). Re-cast by the author as a run-through trade.
- **Evidence:** **B (0) as a magnet.** Perfiltrade: poor highs revisited 11.9% vs 12.9% for controls, so no attraction. Telonics: real levels are run through more often than fakes. **H** for the run-through trade. Lit: Indep + Author.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); skip the first session after a splice
- **Why it might work:** A poor (flat, unfinished) high has resting buy stops above it. Once price trades through, those stops fill and add momentum. The *break*, not the magnet, is the edge.
- **Rules:** *Level:* prior-session poor high (`IsPoorHigh`). *Signal:* the first 5m close ≥ δ above it during 09:45–14:30. *Entry:* market next bar. *Stop:* 0.5 × ATR5 back below the level, floored. *Target:* b ∈ {0.75, 1, 1.5} (E(b) sweep, EX-08).
- **Sweep:** b ∈ 3 values → 3.
- **Frequency (est.):** 2–5 per month.
- **Fails when:** the run-through is a single tick and reverses (stop hunt).
- **Needs:** F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (normal highs of matched distance). **Kill:** no lift over normal prior-day highs.

#### PS-20 · Single-print (initiative tail) pullback continuation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Dalton single prints; Keller B1; tradingstats (single-print days fully reverse only 6–10%).
- **Evidence:** **B (+) descriptive** (non-reversal) · trade untested. Lit: Indep (descriptive) + Author.
- **Markets · TF:** ES/NQ; 30m TPO, 5m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** Single prints left by an initiative move mark urgency. Because full reversal is rare, the top of the single-print zone (for an upward move) acts as the line the initiators defend.
- **Rules:** *Filter:* the developing RTH profile has a single-print zone ≥ 4 ticks (`SinglePrints`) created before 12:00. *Entry:* the first pullback into the upper half of the zone that prints a 5m HL. *Stop:* below the zone's low, floored. *Target:* session high + 0.5 × zone height, or trail.
- **Sweep:** zone min ∈ {4, 8} ticks; entry depth ∈ {top of zone, mid} → 4.
- **Frequency (est.):** 2–5 per month.
- **Fails when:** the zone gets filled (price trades back through, ~6–10% full reversal by itself is not stop-level).
- **Needs:** F-PROF on the developing profile (verify SinglePrints works intraday; else compute on Own-dev), ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (shift the zone), K-RT (on single-print days). **Kill:** no lift over K-RT.

#### PS-21 · Naked POC / naked VAH–VAL first touch, with a confirming trigger
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 card 3.4 (nPOCs as ranked targets only); Dale OF #5 (unfinished business); base `IsNakedVah/Val`.
- **Evidence:** **B (0).** K4 nPOC filled 85.9%, but in ES at the same rate as distance-matched controls (EV LABS); Perfiltrade magnets = controls. Lit: Indep (null).
- **Markets · TF:** ES/NQ, GC, CL; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); naked levels ≤20 sessions, multi-day levels reset at rolls
- **Why it might work (the case to test):** Keller says only as a *target*. This entry tests whether *reaction* at the first touch (not the fill) has value when combined with a trigger.
- **Rules:** *Level:* the nearest naked POC/VAH/VAL ≤ 20 sessions old. *Entry:* first touch ± δ, then a 5m rejection close. *Stop:* level + floor. *Target:* 1 R.
- **Sweep:** age ∈ {≤5, ≤20} sessions; level type ∈ {POC, VAH/VAL} → 4.
- **Frequency (est.):** 6–12 per month.
- **Fails when:** null as reported.
- **Needs:** F-NPOC, F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **K-FL with distance matching is mandatory.** **Kill:** expected; kept as a *control* for EX-02.

#### PS-22 · Composite LVN-edge rejection vs HVN-ceiling veto
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B1 windows ("session rejection at a composite LVN edge = high-quality fade; composite HVN = ceiling, veto").
- **Evidence:** **D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 5m signal, 20-session composite.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 10/20-session composites multi-day levels reset at rolls (on ES ≈ one-third of each quarter has no full 20-session composite unless individual-contract history seeds it)
- **Why it might work:** At a composite LVN edge, price is entering a zone the longer-term market rejected. Short-term momentum traders have no longer-term counterparties there.
- **Rules:** *Level:* 20-session composite LVN edges. *Entry:* 5m rejection close (as PS-16 but without the slowness filter). *Veto layer (separate test):* skip any long whose 1 R target lies beyond a composite HVN ceiling.
- **Sweep:** composite N ∈ {10, 20} → 2; veto on/off → 4.
- **Frequency (est.):** 4–10 per month.
- **Fails when:** composite levels are too stale (K-ST should catch it).
- **Needs:** F-COMP, F-HVN.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** the veto is tested as a **layer on PS-01/MT-01 events** (does it remove losers?). **Kill:** veto removes winners and losers at the same rate.

#### PS-23 · Profile shape at the close → next-session tilt
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Levy (P/b shapes); Keller B1; Dalton (short-covering P vs liquidation b).
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC; daily decision, 5m execution.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A P-shape close (value built high, thin tail below) can be either new buying (continuation) or short covering (exhaustion). Dalton's distinction is whether the next open accepts above the P's POC.
- **Rules:** *Filter:* completed RTH shape ∈ {P, b}. *Next day:* if the RTH open is above the P's POC and 2 thirty-minute periods hold above it → long on the first 5m HL; stop below the P's POC, floored; target prior high + 0.5 ATR20d. Mirror for b.
- **Sweep:** acceptance periods ∈ {1, 2} → 2.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** short-covering P (exhaustion).
- **Needs:** F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT on next-day; compare P vs D-shape days with the same entry. **Kill:** P-day results ≤ D-day results.

#### PS-24 · Overnight high/low break-and-go at RTH
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS · Keller B3 §4.2 (ON high/low are violated in RTH more often than RTH extremes); R4 C3.
- **Evidence:** **B (+)** for run-through at real levels (Telonics) · **D** for the setup · Lit: Indep + Book.
- **Markets · TF:** ES/NQ; 1m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5)
- **Why it might work:** Overnight extremes are thin-liquidity prices with resting stops. Once RTH participation takes price through them, stops and breakout orders add fuel.
- **Rules:** *Filter:* 09:45–11:30; the ON extreme is ≥ 0.5 × ATR5 outside the RTH open; V-score not opposed. *Signal:* 2 closes beyond the ON extreme + δ. *Entry:* market, or the first 1m HL (variant). *Stop:* 0.5 × ATR5 back inside, floored. *Target:* b sweep.
- **Sweep:** entry ∈ {close, HL}; b ∈ {0.75, 1.5} → 4.
- **Frequency (est.):** 6–10 per month.
- **Fails when:** a flat V-score; the break occurs right after a news release.
- **Needs:** F-SESS (ETH high/low), F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (fake ON extremes), plus the same rule at the prior RTH high (Keller says the ON extreme should be better). **Kill:** ON ≤ RTH-extreme version.

#### PS-25 (= KD-01) · Accept-beyond-rail continuation on detrended rails (rails as targets) — Keller B5 primary
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Family · Source:** PS · Keller B5 Ch 5.1 (loc 2684–2726), 5.4 #20/#21 (loc 3264), 5.2 #17 (loc 2753), 8.1 (loc 6058–6125), 8.3 rule 5 "every rail a destination, every acceptance the entry" (loc 6186–6213; a **hypothesis**, not census); continuity with Keller B4 §8.4 / PS-01, PS-02, DV-02. Rev 3.1, signed off 23 Oct 2026.
- **Evidence:** **C (+) family, FX/crypto, exhibit-level** · **setup untested** · **E for the geometry**. Accepted-rail exhibits: MFE/MAE 6.3/0.45 ATR FX, 6.2/0.3 crypto (loc 2684); #20/#21 ratios 19.6/24.0 FX, 47.0/undefined crypto (loc 6058). The book's own caveat: MAE is measured *after* acceptance was visible; the population comparison of first-close vs after-acceptance entry was **not done**. Lit: Book. No ES number exists. · **FX/crypto only · no true delta · ES prior 0**. *(Original grade (B5 rev 3.1): same; not rescaled.)*
- **Markets · TF:** ES (MES for live sizing); M-DVP streams 15m (Data1), 60m and 240m via `BarsOfData` (240m sparse after roll exclusion; `winLen` 120 swept there).
- **Data · test window:** 1m-built bars · W-M (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: a fit window may not cross a roll (`crossesRoll` skip; skip share journaled per stream, > 30% → diagnostic-only); ETH primary, RTH-only sensitivity arm; RTH share of fit-window volume journaled, results split {<50%, ≥50%} report-only.
- **Why it might work:** a close beyond a trend-relative value rail that *stays* means the trend has re-rated and the manifold is out of date; responsive traders who faded the rail are offside; continuation runs to the next rail.
- **Rules (long; short mirrored):**
  - *Rails:* M-DVP rails at t from the fit ending t−1 (VAH, POC, upper HVN/LVN, +1/+2/+3σ). Event rail ∈ {VAH (#20), POC (#17)} — default VAH.
  - *Trigger:* bar t closes > rail_t + δ, δ = 0.10 ATR; bar t−1 closed ≤ rail_{t−1}.
  - *Acceptance (hold-for-N):* the next **N = 2** closed bars (**primary; confirmed by Ettienne 23 Oct 2026**) all close > their own rail (each re-read from that bar's t−1 fit) and no close back inside by more than δ. [GAP: the scan's entry-relevant persistence `m` is unpublished, loc 4414; N is the pre-registered substitute.]
  - *Filter:* fit stream not in roll exclusion; `sigmaAtr` ≤ 4; **no R² filter**.
  - *Entry:* buy-stop 1 tick above the high of the N-th acceptance bar, valid for 2 bars.
  - *Stop:* max(1 tick below the lowest low of the acceptance bars, rail_t − 0.5 ATR), cost floor per A2-40.
  - *Target:* next rail above in residual order (upper HVN → +2σ → +3σ) from the fit frozen at entry; partial 50% at the first rail, remainder trailed by EX-05 or a 41-bar time stop (book horizon).
  - *Invalidation:* close back below rail − δ.
- **Sweep (frozen, 18 cells):** N ∈ {2, 3, 5}; event rail ∈ {VAH/VAL, POC}; target ∈ {next rail, +2σ rail, trail EX-05}. **Separate σ2/node robustness block (6 cells, outside PS-25, journaled as trials, never the primary):** rail {σ2 (KD-06), node (KD-10)} × N {2, 3, 5} × exit EX-05 trail + 41-bar time stop (§3.16).
- **Primary cell (pre-registration):** **PROPOSED (both analysts), Ettienne to confirm before any dev read:** **N = 2; event rail VAH/VAL; exit EX-05 trail plus the 41-bar time stop** (one of the 18 frozen cells). *Rationale:* VA rails beat the POC in the census (one-bar hold VAH/VAL 88.9–89.5% vs POC 73.4%, FX/crypto, loc 2684), and App. F withdraws POC-first weighting (rev 3 §5 item 1: POC = VA edge, 1.0/1.0); the acceptance edge is run length (8.1: accepted exhibits MFE/MAE 8.9–47), so a trail fits better than a fixed next-rail target. Until confirmed, the cell is not frozen (rev 3.1 ruling 6.1 item 2; hidden-trial list item 1).
- **Frequency (est.):** author estimate, 15m: a few per week; 60m: ~1–3 per month; 240m: < 1 per month (fewer after roll exclusion).
- **Fails when:** acceptance is a news spike that retraces; the rail moves with the refit (endpoint repaint) so the "hold" is partly a curve artefact; waiting N bars spends most of the move.
- **Needs:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) (§2.4b); ✓ M-MTF (P2 open); F-EVT, F-TM, F-JRNL; A2-40/59.
- **Readiness (v2.4):** Engine on main (PR-D0…D8); consumer rule not coded; may start only after PR-D9 freezes d*; A2-33 DVP level types are registered (#42); HTF streams need Probe P2.
- **Gate (v2):** G0-a on every result; G0-b canaries (incl. KD-02) re-run after the engine lands and must stay ≤ PF 1.0; G0-c roll-window exclusion mandatory; **G0-d: not read until A2-33 returns |t| ≥ 2 for the event rail type** (`DvpVah/DvpVal/DvpPoc`).
- **Test · Kill:** (1) population version of the book's open experiment: every first close beyond a VA/POC rail on ES, median MFE/MAE (40 bars) entering at the trigger close vs after N-bar acceptance; held vs accepted on a **symmetric 41-bar clock**. (2) Controls: K-RT random-time entries matched by direction and vol; K-FL/ShiftLevels (rails shifted ±0.5σ); **flat-profile twin** (F-COMP flat profile of the same 240 bars); degree-1 twin (report-only). **Kill:** population accepted-entry median MFE/MAE < 3 (the book's own falsifier) **or** no lift over ShiftLevels **or** no lift over the flat-profile twin **or** net expectancy ≤ 0 under G0-a.
- **Holm (rev 3.1):** PS-25 (KD-01's primary core only) is a member of the Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, **m = 5, family-wise α = 0.05**, fixed before any dev read; m stays 5 even if the A2-31/PS-25 tie-break drops one. **A2-31 vs PS-25 tie-break (> 70% trade overlap):** dev data only, net expectancy after A2-40/A2-59, frozen at sign-off — the only declared P&L choice.
- **Overlaps (report-only, no P&L picks):** A2-25 (check POC stability before using the POC as an event rail); A2-26/A2-27 (reuse their bimodal/LVN veto); A2-28 (same rails as HVN/LVN); A2-29/A2-30 (entropy = manifold entropy; report the correlation); **A2-32 (closest overlap, σ-space levels; report the trade overlap)**; A2-31 (tie-break above); A2-33 (G0-d matched controls).

### 3.2 Family VW: Session VWAP and σ-bands (8)

> **Family prior.** Independent evidence says session VWAP is not a bounce level (50.2%) and not a magnet (48.4% reach-first vs 51.6% for the mirror distance). The volume weighting is undetectable (14/14 shuffle tests). The only true belief is directional: the forward-hour return is positive 55.2% of the time above VWAP vs 53.4% below. That effect is intraday momentum, worth 0.28× trading cost on NQ and 0.07× on ES (Telonics, Oct 2026). On ES, VWAP-cross trend, reversion and ±2–3σ band fades all fail after costs (RipBacktest, Oct 2026). **Consequence:** VWAP ideas below must beat a *fake VWAP* (shuffled-volume or plain moving average of equal turnover), not just zero.

#### VW-01 · Trend-day first VWAP pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · Keller B1 Part IV setup 1 (first/second VWAP touch on a classified trend day).
- **Evidence:** **B (0)** for generic VWAP touches (Telonics); **D** for the conditional setup · untested · Lit: Book + Indep (null for the parent).
- **Markets · TF:** ES/NQ, CL, GC; 5m classification, 1m/5m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** On a *classified* trend day, VWAP is the benchmark that execution algorithms are measured against. Buy-side algos that are behind schedule buy at or below VWAP, and day-traders who faded the trend cover there. Telonics' null averages trend and balance days together; the conditional subset is untested.
- **Rules:** *Classification:* trend-day proxy (§2.2) true at ≥ 10:30. *Entry:* the first (variant: second) touch of VWAP ± 0.25 σ that prints a 5m HL; buy-stop above it. *Stop:* VWAP − 1σ or HL − 1 tick, whichever is farther, floored. *Target:* session high, then trail (EX-04 on session VWAP).
- **Sweep:** touch # ∈ {1, 2}; classification time ∈ {10:30, 11:00}; target ∈ {session high, trail} → 8.
- **Frequency (est.):** 2–5 per month per instrument (trend days only).
- **Fails when:** the trend day ends at the touch (the classification was late); lunchtime chop.
- **Needs:** F-VWAP (session slot + σ), F-SESS, F-DEV (POC migration in the classifier); ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **K-FL with a fake VWAP**: (a) volume-shuffled VWAP and (b) an SMA matched to VWAP's average distance. Plus K-RT on trend days only. **Kill:** real VWAP ≤ fake VWAP (the Telonics outcome); then reframe it as "trend-day pullback, any average".

#### VW-02 · Balance-day ±2σ fade at a composite HVN edge (conditional)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · Keller B1 Part IV setup 2 (rated B− and cost-sensitive by the author).
- **Evidence:** **B (−)** for unconditional band fades: RipBacktest ES PF 0.78 (n=7,803); Telonics "2 ATR below, target VWAP" wins ~70% with PF 0.81. **D** for the conditional version · Lit: Indep (negative) + Book.
- **Markets · TF:** ES/NQ, ZN; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work (the case to test):** On a *balance* day where the 2σ band coincides with a composite HVN edge, two independent definitions of "fair price" agree that the extension is too far.
- **Rules:** *Filter:* balance-day proxy (§2.2) and a ±2σ band within δ of a composite (5-session) VAH/VAL or HVN edge. *Entry:* 5m rejection close back inside 2σ. *Stop:* 3σ, floored. *Target:* VWAP (b typically 1–2).
- **Sweep:** σ ∈ {2, 2.5}; confluence required on/off → 4.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** this is the documented negative family; any "balance day" can become a trend day at 14:00.
- **Needs:** F-VWAP σ, F-COMP, F-HVN.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **the unconditional band fade runs as `Mode=Canary`** (expect PF ≈ 0.78). **Kill:** the conditional version is not ≥ +0.10 R *and* ≥ +0.15 R above the canary.

#### VW-03 · σ-band expansion continuation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · author idea (the mirror of the failed band fade).
- **Evidence:** **H** · Lit: Author. A losing fade does *not* imply a winning mirror (costs are paid twice), so this is a genuine hypothesis.
- **Markets · TF:** ES/NQ, CL, GC; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A close beyond +2σ while σ itself is expanding and VWAP is sloping up means price is accelerating away from where most volume traded. Mean-reverters who sold +2σ are now trapped.
- **Rules:** *Filter:* VWAP slope > 0.15 × ATR5 per bar over 6 bars; σ(t) > σ(t−6) × 1.1. *Entry:* the second consecutive 5m close above +2σ; buy next bar open. *Stop:* back below +1σ, floored. *Target:* +3σ, or trail at +1σ.
- **Sweep:** band ∈ {1.5σ, 2σ}; target ∈ {3σ, trail} → 4.
- **Frequency (est.):** 3–6 per month.
- **Fails when:** the move is a news spike; late-day expansion runs out of time.
- **Needs:** F-VWAP σ.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** compare to the same rule on Keltner (EMA ± ATR) bands with matched trigger frequency, the "is VWAP special?" control. **Kill:** VWAP version ≤ Keltner version.
- **Merged · A2-10 (variant B entry):** after 10:30, a 5m close beyond +2σ with range ≥ 1.2×ATR5 and relative volume ≥ 1.5 (vs the same time of day over 20 sessions); wait for the first pullback that holds +1σ (no 5m close below it); enter on a 1m close above the pullback's 1m swing high; stop below +1σ − 0.1×ATR5; T1 +3σ, then trail on a 5m close below +1σ; flat 15:55 (A2). Supporting text: Coles & Hawkins (MIDAS ch. 15): price "either returns to the centre or travels through to the outer band". *(Grades: A2-10 original **C** → catalogue **H**.)*
- **Overlap · Keller B5 (v2.3, report-only):** KD-06 (PS-25 σ2 arm) is the detrended-curve analogue; VW-03 on the same bars is KD-06's flat-σ twin control. Trade overlap reported only — **no P&L pick, nothing dropped**.


#### VW-04 · Failed VWAP defence → reversal context
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · Keller B1 Part IV ("failed VWAP defence on a heavy one-sided day = reversal context").
- **Evidence:** **D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** After a one-sided morning, the VWAP is where trend participants' average cost sits. When price is *accepted* on the other side of it, the morning's buyers are underwater on average and start to liquidate.
- **Rules:** *Filter:* the morning trend proxy was true at 10:30 (up). *Signal:* after 11:30, two consecutive 5m closes below VWAP − 0.25σ, then a 5m LH below VWAP. *Entry:* sell-stop under the LH bar. *Stop:* VWAP + 0.5σ, floored. *Target:* IB low / −1σ.
- **Sweep:** acceptance closes ∈ {2, 3} → 2; target ∈ {IB low, −1σ} → 4.
- **Frequency (est.):** 1–3 per month.
- **Fails when:** the afternoon resumes the trend (lunch dip).
- **Needs:** F-VWAP, F-SESS; ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** fake VWAP control as VW-01. **Kill:** ≤ 0 R or ≤ fake.

#### VW-05 · Late-day continuation: VWAP side × rest-of-day return
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW / TS · Gao et al. / Baltussen et al. (intraday momentum: the first half-hour, or the rest of day, predicts the last half-hour); Keller B1 (late-day behind-schedule algos defend VWAP).
- **Evidence:** **B/C (0/−) after decay.** Published intraday-momentum results were fragile out of sample: Krueger's holdout reversed (t −3.7). Lit: Indep.
- **Markets · TF:** ES/NQ, ZN; 30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (the published effect also has decades of daily-close data, but the 15:00/15:30 entry needs 1m)
- **Why it might work:** Gamma-hedging and leveraged-ETF rebalancing flows trade in the direction of the day's move into the close. VWAP-benchmarked algos that are behind schedule finish their buying late on up days.
- **Rules:** *Signal at 15:00:* the sign of (15:00 price − prior close) agrees with the sign of (price − session VWAP). *Entry:* 15:00 market in that direction. *Exit:* 15:55. *Stop:* 1 × ATR30m, floored.
- **Sweep:** entry time ∈ {14:30, 15:00, 15:30} → 3.
- **Frequency (est.):** ~20 per month (one per day when signs agree; roughly half of days).
- **Fails when:** decay; the MOC imbalance reverses at 15:50 (K3: treat 15:30–16:00 as its own regime).
- **Needs:** F-VWAP, F-SESS, F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** the plain rest-of-day momentum version (without VWAP) is the control; for the A2-06 primary rule, A2's controls and VIX/NGE/0DTE splits above. **Kill:** A2-06's kill above (no lift over ROD-only, or a negative holdout) for the primary rule; the v1 kill (≤ 0 in 2023–24 validation) applies **only to the 15:00 arm**.
- **Merged · A2-06 (adopted as the primary rule):** at 15:29, long if the rest-of-day return > 0 **and** close > sVWAP **and** sVWAP's 60-min slope > 0 (short = mirror); skip if |r_ROD| < 0.5×σ_daily. Enter at the 15:30 open, exit at the 15:59 close, stop 1×ATR5. Controls: ROD-only and ROD + EMA side; separate 2022+ (0DTE era) sub-sample. Note that Baltussen et al. (JFE 2021) document the base effect across 60+ futures markets, 1974–2020 (A2 grade A−, decaying). **A2-06's own test and kill (restored v2):** compare with (i) ROD-only and (ii) ROD + EMA side; **split by VIX regime and by an NGE proxy if available**; the 2022+ 0DTE period gets its own sub-sample. **Kill:** no lift over ROD-only, or a negative holdout. *(Grades: A2-06 original **B (base effect) / C (VWAP variant)** → catalogue **B/C (0/−)**.)*

#### VW-06 · POC–VWAP skew pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · Levy (P-profile: buying below VWAP when value is skewed up); author's operationalisation.
- **Evidence:** **E/H** · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); developing profile approximate on 1m, parity-checked in W-T
- **Why it might work:** When the developing POC sits above VWAP, the *mode* of business is above the *mean*. Most volume accepted higher prices, so dips below VWAP are cheap relative to where business is being done.
- **Rules:** *Filter:* developing POC − VWAP ≥ 0.5σ; after 11:00. *Entry:* price ≤ VWAP − 0.25σ, then a 5m HL. *Stop:* VWAP − 1.5σ, floored. *Target:* developing POC.
- **Sweep:** skew threshold ∈ {0.5σ, 1σ} → 2.
- **Frequency (est.):** 2–5 per month.
- **Fails when:** the skew is a stale morning artefact.
- **Needs:** F-DEV, F-VWAP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL on VWAP, plus the same entry without the skew filter. **Kill:** the skew filter adds < 0.05 R.

#### VW-07 · Prior-day VWAP and ETH VWAP as opening reference (filter test)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · practitioner lore; Telonics anchor table.
- **Evidence:** **B (0).** Telonics: previous-day VWAP reach-first 51.18% (7/8 arms contain 50%); overnight-anchor bias +0.0041 (tiny). Lit: Indep (null).
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5)
- **Why it might work:** Only as a *location filter* for other ideas (a long setup opening above the ETH VWAP *and* the prior-day VWAP has both cohorts in profit).
- **Rules:** a binary layer, `aboveBoth`, applied to PS-01, PS-07, MT-01 events. No standalone trade.
- **Sweep:** layer on/off → 2 per host.
- **Frequency:** inherits the host's.
- **Fails when:** null, as reported.
- **Needs:** F-VWAP slot 2 = ETH session; slot 3 = AdaptiveFromDate (per-period re-anchoring ✓ via `EVwapType` Week/Month/Quarter / M-AVWAP).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** layer ablation (L-step) on host events. **Kill:** the layer changes host mean R by < 0.03 R.

#### VW-08 · VWAP-cross "Holy Grail" (canary)
- **Implementation status (v2.4):** **Implemented** — G0-b canaries VW08 + A2_24_i/ii/iii + DV10 in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30), [#43](https://github.com/ettiennehugo/MultiCharts.Net/pull/43), [#44](https://github.com/ettiennehugo/MultiCharts.Net/pull/44), [#45](https://github.com/ettiennehugo/MultiCharts.Net/pull/45)). Includes merged A2-24 and DV-10. Not run in MC.
- **Family · Source:** VW · Zarattini & Aziz 2023 (SSRN 4631351).
- **Evidence:** **B (−).** Telonics replicates Sharpe 1.91 at zero slippage → 0.10 at 1 tick on QQQ; post-publication Sharpe 0.63 even at zero cost. RipBacktest ES VWAP-trend PF 0.89 (n=11,707). Lit: Indep.
- **Markets · TF:** MNQ/MES; 1m and 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 1-tick slippage on every fill
- **Why include it:** As a **canary** for the cost model and fill engine: with honest 1-tick slippage it **must** come out ≈ 0 or negative. If it comes out strongly positive, the simulator is lying.
- **Rules:** long when a 1m bar closes above session VWAP, short below, reverse on an opposite close, flat at 15:55.
- **Sweep:** none (1 config, charged to the trial budget as a control).
- **Frequency:** ~15 reversals per day.
- **Needs:** F-VWAP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **Kill the simulator, not the idea,** if net Sharpe > 0.5 at 1-tick slippage.
- **Merged · DV-10 (Trader Dale video: weekly-VWAP ±1σ rotation fade → VWAP, run as canary arm (iv); Dale priority 1).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Conflict (P5). Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Source:** https://www.youtube.com/watch?v=N4t8_dJUMho (setup 1 "rotation setup": flat σ1 bands; long at the lower σ1, short at the upper σ1, "take profit is the VWAP… works as a magnet").
  - **Evidence:** E.
  - **Rules:**
    - **[coding]** |slope σ1| < s × ATR30m over 8 bars; limit at σ1; TP = VWAP; SL = f × ATR20d.
  - **Sweep:** 4.
  - **Frequency:** high (10+ per month).
  - **Books:** **Conflict.** P5 (±σ band fade PF 0.78; bounce 50.2%). **Priority 1:** run as a canary next to VW-08/VW-02. It must show about zero edge.
- **Merged · A2-24 (canary suite, run before any VWAP/AVWAP idea is evaluated):** (i) fade the first sVWAP touch, 1 R/1 R; (ii) fade a 2σ-band close with the target = sVWAP *frozen at entry* (avoids the moving-target fill bug); (iii) a 2×ATR-from-VWAP fade. If any shows net PF > 1.0 on **ES 2019–2024** (A2's window; restored v2, then repeated across W-M), the simulator has a bug (lookahead, moving-target fill or cost error). Run before any A2 idea is evaluated; it is part of **Gate 0 (§2.6)**. Kill: none (infrastructure). *(Grades: A2-24 original **A (negative)** → catalogue **B (−)**.)*

### 3.3 Family AV: Anchored VWAP (13)

> **Family prior.** AVWAP methodology (Brian Shannon, *Maximum Trading Gains with Anchored VWAP*; CMT Association materials; Benzinga 2022; S&C V41:07 interview) is a coherent "average cost of a cohort since an event" framework: anchors at earnings/gaps/Fed days/significant swing highs and lows/WTD-MTD-YTD; "above the AVWAP = buyers in control"; handoffs; pinches. **It publishes no controlled statistics (grade E).** The one controlled test that touches it found a 51.14% bounce rate for an AVWAP anchored at yesterday's high and 52.37% for a monthly one, inside noise (Telonics). No controlled study of swing-anchored AVWAPs on futures was found (searched 20 Oct 2026). **Everything here needs ✓ M-AVWAP** (event-anchored anchors). Each idea must beat a *fake anchor* (the same construction anchored at a random bar of the same age) and a *plain SMA* of matching length.

#### AV-01 · Swing-high AVWAP reclaim (Shannon "buy strength after the pullback")
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon (buys strength after a pullback that recovers above a swing-high AVWAP; stop below the most recent HL).
- **Evidence:** **E** · untested · Lit: Practitioner.
- **Markets · TF:** ES/NQ, CL, GC, ZN; anchors on 60m/240m/daily swings; 15m/60m signal (swing).
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (60m/240m built from 1m) + daily · W-D (decades; ES from 1997, NQ from 1999); swing anchors older than the last splice are dropped (multi-day levels reset at rolls)
- **Why it might work:** Everyone who bought since the last significant swing high is underwater while price is below that AVWAP. They sell into rallies to get out at break-even. When price reclaims and holds above it, that overhead supply is absorbed, the cohort is back in profit, and shorts from the high are underwater.
- **Rules:** *Anchor:* the last significant swing high (k = 3 × ATR on 60m), usable from confirmation. *Filter:* daily close > 50-day SMA (or TSMOM > 0, RG-06). *Signal:* a 60m close above the swing-high AVWAP + δ60, after ≥ 10 bars below it. *Entry:* the first 15m HL above the AVWAP; buy-stop. *Stop:* HL − 1 tick (Shannon), floored at the swing cost floor (≥ 0.3 × ATR20d). *Target:* swing high + 0 (retest), then trail EX-04 on the new impulse AVWAP.
- **Sweep:** anchor k ∈ {2, 3}; signal TF ∈ {15m, 60m} → 4; target ∈ {swing high, trail} → 8.
- **Frequency (est.):** 1–3 per month per instrument.
- **Fails when:** range-bound markets with repeated reclaim/lose cycles (whipsaw); anchor choice (the "craft").
- **Needs:** ✓ M-AVWAP, ✓ M-SWING, ✓ M-DAILY; holds overnight (swing) → gap risk.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **K-FL-anchor:** an AVWAP anchored at a random bar 20–60 bars before confirmation. **K-SMA:** an SMA of length = bars since anchor. **Kill:** real anchor ≤ either fake.

#### AV-02 · Impulse-origin AVWAP higher-low continuation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon (AVWAPs from significant lows act as support) + author's codification.
- **Evidence:** **E** · untested · Lit: Practitioner.
- **Markets · TF:** ES/NQ, CL, GC; 5m anchor/15m swing (intraday) and 60m/daily (swing).
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 60m anchors multi-day levels reset at rolls
- **Why it might work:** After an impulse leg from a significant low, the AVWAP from that low is the impulse buyers' average cost. The first pullback toward it, if it holds with a HL, shows them defending their position. Late shorts who sold the pullback are trapped.
- **Rules:** *Anchor:* the low that starts an impulse of ≥ 3 × ATR (anchor TF), confirmed. *Signal:* the first pullback whose low is within [AVWAP − 0.25σ, AVWAP + 0.5σ] and prints a confirmed HL above the anchor low. *Entry:* buy-stop above the HL bar. *Stop:* min(HL − 1 tick, AVWAP − 1σ), floored. *Target:* the impulse high (b usually ≥ 1), then trail EX-04.
- **Sweep:** anchor TF ∈ {15m, 60m}; touch zone ∈ {±0.25σ, ±0.5σ} → 4; target ∈ {impulse high, trail} → 8.
- **Frequency (est.):** intraday 3–6 per month; swing 1–2 per month.
- **Fails when:** the impulse was a news spike (no real cohort); the second test is weaker than the first.
- **Needs:** ✓ M-AVWAP, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL-anchor, K-SMA, and the identical HL rule using a 50% retracement zone (Tickfloor benchmark). **Kill:** not better than 50% retracement.
- **Merged · DV-12 (Trader Dale video: prominent-swing AVWAP blind-limit pullbacks (≤ 3 touches) as the touch-entry arm; Dale priority 2).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Conflict (P5). Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:** https://www.youtube.com/watch?v=iAo1BgeuZvc and https://www.youtube.com/watch?v=wPP_r2hd0y4 (setup 1; EUR/USD, ES, 30m).
  - **Rules:**
    - *[Dale]* anchor to "the most prominent swing point… every trader sees"; trade every pullback to the AVWAP in the trend direction; works best in trends.
    - **[coding]** Anchor = the most recent M-SWING pivot of order ≥ 20 bars (30m) at confirmation time (no lookahead: the AVWAP starts at the pivot bar but becomes tradeable only after confirmation).
    - Enter a limit at the AVWAP while price is on the trend side; maximum 3 touches; SL/TP f × ATR20d.
  - **Evidence:** E.
  - **Sweep:** pivot order ∈ {10, 20}; touches ∈ {1, 3} → 4.
  - **Frequency:** 4–8 per month.
  - **Books:** **Conflict.** Telonics AVWAP bounce is about 51% (P5). **Priority 2.** Overlaps AV-01/AV-02.

#### AV-03 · AVWAP pinch breakout (Shannon)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon, *Maximum Trading Gains with Anchored VWAP* (pinch chapter: "like squeezing a spring"; trade the break, not the first touch).
- **Evidence:** **E** · untested. Adjacent: compression ideas (NR7, "full vs empty book") are (−/0) (R2/K2 grade C). Lit: Practitioner.
- **Markets · TF:** ES/NQ, CL, GC, ZN; 60m/240m/daily anchors (swing), 15m/60m signal.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999); daily-swing anchors often straddle a roll → within-contract anchors only, so fewer events
- **Why it might work:** When the AVWAP from the last significant high and the AVWAP from the last significant low converge, both cohorts' average cost is the same price. Whichever side price is accepted on, the other cohort is underwater and must cover or liquidate.
- **Rules:** *Anchors:* the most recent confirmed significant swing high H and swing low L (k = 3 × ATR, anchor TF 60m). *Pinch:* |AVWAP_H − AVWAP_L| ≤ 0.5 × ATR(anchor TF) for ≥ 5 bars with price between them. *Signal:* 2 closes beyond the outer AVWAP + δ in the direction of the 20-day trend (variant: either direction). *Entry:* next bar open, or the first HL (variant). *Stop:* the opposite AVWAP − δ, floored. *Target:* 2 × pinch-period range, or trail on the surviving AVWAP (Shannon handoff).
- **Sweep:** pinch width ∈ {0.5, 1.0} ATR; direction ∈ {with trend, both} → 4; entry ∈ {close, HL} → 8.
- **Frequency (est.):** 1–3 per month per instrument.
- **Fails when:** false breaks (Shannon: "not all pinches resolve in the direction of the trend"); anchor selection.
- **Needs:** ✓ M-AVWAP (two dynamic anchors; spread computed in the strategy), ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **compression control**: the same break rule on a Donchian/NR-range of equal width without AVWAPs. **Kill:** the pinch version ≤ the plain compression break.
- **Merged · A2-02:** A2's version uses four anchors (last confirmed daily swing high and low, N=3; prior-month first RTH bar; YTD open). Pinch = max−min of the AVWAPs ≤ 0.5×ATRd **and** the 5-day realised range ≤ the 30th percentile of 250 days. Long on a 30m close > max(AVWAPs) + 0.1×ATRd, plus a second accepting close; stop below min(AVWAPs) − 0.1×ATRd, capped at 1×ATRd; 2 R or trail; time stop 10 sessions. Controls: matched low-volatility non-pinch days, and Donchian-20 under the same compression filter. *(Grades: A2-02 original **C** → catalogue **E**.)*

#### AV-04 · AVWAP handoff continuation (Shannon)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon ("handoff": anchor a new AVWAP at the acceleration point after a test of the old one).
- **Evidence:** **E** · untested · Lit: Practitioner.
- **Markets · TF:** ES/NQ, GC, CL; 60m/daily (swing).
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** Each test-and-acceleration marks a fresh, more aggressive cohort. Trading pullbacks to the *newest* AVWAP keeps entries close to the most urgent buyers' cost.
- **Rules:** *Handoff event:* price tests the current AVWAP (low within ±0.25σ) and then makes a new swing high within 10 bars. A new anchor is placed at the test low, usable from the new-high confirmation. *Entry:* the first pullback to the newest AVWAP with a 60m HL. *Stop:* the older AVWAP − δ (Shannon: the original anchor is often support), floored. *Target:* trail EX-04 on the newest AVWAP.
- **Sweep:** test zone ∈ {±0.25σ, ±0.5σ}; acceleration window ∈ {5, 10} bars → 4.
- **Frequency (est.):** 1–2 per month per instrument.
- **Fails when:** late-stage trends (each handoff is closer to the top); choppy tape.
- **Needs:** ✓ M-AVWAP (anchor chains via `Anchors`), ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** compare to AV-02 entries on the *original* anchor only. **Kill:** handoff entries ≤ original-anchor entries; for the A2-01 primary spec, also A2-01's kill above (no improvement over Spec 1 as a location filter).
- **Merged · A2-01 (primary spec):** AV1 = AVWAP from the last confirmed 60m swing low that started a HH/HL sequence; AV2 = AVWAP anchored at the low of a pullback that came within 0.25×ATR60 of AV1 and was followed by a 60m close above the prior 60m swing high (confirmed at that close). Filter: AV2 > AV1, AV2 slope over 6×60m > 0, 60m state UP, price above both. Entry: the first 5m pullback to within 0.15×ATR60 of AV2 with no 5m close below AV2 − 0.1×ATR60, then a 1m close above the high of the pullback's lowest 5m bar. Stop: min(pullback low, AV2 − 0.25×ATR60) − 1 tick. T1 +1 R (50%), runner exits on a 60m close below AV2; time stop 5 sessions. Sweep: pivot N ∈ {2,3,5} × tolerance ∈ {0.1, 0.15, 0.25} → 9. It must beat P1 (unweighted), P2 (volume-shuffled) and P3 (EMA20) by ≥ 0.05 R. **Kill (A2, restored v2):** generic criteria, or **no improvement over Analyst 1's Spec 1 (PS-01) when used as a location filter**. *(Grades: A2-01 original **C** → catalogue **E**.)*

#### AV-05 · Gap-anchored AVWAP swing hold
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon (earnings-gap / event AVWAPs) adapted to futures RTH gaps.
- **Evidence:** **E** · untested · Lit: Practitioner.
- **Markets · TF:** ES/NQ, CL, GC; daily + 60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999); multi-day levels reset at rolls; roll gaps excluded as anchors
- **Why it might work:** A large RTH gap that holds marks a repricing. The AVWAP from the gap-day open is the new cohort's cost. If it holds for days, latecomers buy pullbacks to it; if it is lost, the gap buyers are underwater.
- **Rules:** *Anchor:* the RTH open of a day with |gap| ≥ 0.75 × ATR20d **and** a close in the gap direction. *Filter:* no re-entry into the prior RTH range for 2 sessions. *Entry:* the first pullback to the gap AVWAP ± 0.5σ, with a 60m HL, within 10 sessions. *Stop:* below the gap-fill level (prior RTH high for up-gaps), floored. *Target:* trail EX-04; time stop 10 sessions.
- **Sweep:** gap ∈ {0.75, 1.25} ATR20d → 2; holding period ∈ {5, 10} → 4.
- **Frequency (est.):** 0.5–2 per month per instrument.
- **Fails when:** the gap was a liquidity event (holiday, roll).
- **Needs:** ✓ M-AVWAP, F-SESS, F-ROLL (exclude roll gaps), ✓ M-DAILY.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL-anchor (random day open as anchor). **Kill:** < 30 events → park; ≤ fake → kill.

#### AV-06 · Event-bar AVWAP (FOMC / CPI / NFP) acceptance
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon (Fed-day AVWAPs) · author operationalisation for futures.
- **Evidence:** **E** · untested. Adjacent: Krueger macro-release reactions are at chance (C/B, 0). Lit: Practitioner.
- **Markets · TF:** ES/NQ, ZN, GC; 1m/5m intraday, 60m swing.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); needs an event-calendar file back to 2016 (M-NEWS; v2.3b: ✓ M-EVENT table `tools/m-event/out/events_2016_2027.csv`, embedded in `_EventCalendar`, PR #27); ◐: table covers 2016-27 except GDP/PCE holes (EV-4.6b)
- **Why it might work:** The release bar creates a cohort that traded the headline. If, after the noise (+10 min blackout), price is accepted on one side of the event AVWAP, the market has voted on the information and the losing cohort exits over hours to days.
- **Rules:** *Anchor:* the first 1m bar of a tier-1 release (or the FOMC statement). *Signal:* from +10 to +60 min, 3 consecutive 5m closes on one side of the event AVWAP ± 0.5σ. *Entry:* the first 5m HL (long side). *Stop:* event AVWAP − 1σ, floored ×2 (news slippage). *Target:* intraday flat 15:55 (variant: hold to the next session close if above the AVWAP).
- **Sweep:** closes ∈ {2, 3}; hold ∈ {intraday, 1 session} → 4.
- **Frequency (est.):** 3–5 per month across instruments.
- **Fails when:** second-reaction reversals (FOMC press conference 30 min after the statement).
- **Needs:** ✓ M-AVWAP, ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29; event times ✓, `EAvwapAnchor.NewsBar` still inert), F-TM.
- **Readiness (v2.3b):** Buildable with caveats — event times from ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29); `EAvwapAnchor.NewsBar` is still inert and `_AvwapManager` has no public custom-anchor call (anchors are created only internally from `AvwapEvents`), so the strategy must compute its own event-bar AVWAP (or `NewsBar` must be wired to M-EVENT first). Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:** K-RT on the same event days, at the same times; K-FL-anchor (anchor at −60 min). **Kill:** ≤ K-RT.
- **Merged · A2-20 (CL/EIA arm) and Merged · A2-21 (FOMC/CPI/NFP arm):** *EIA:* anchor = the 10:30 ET Wednesday release bar (AVWAP from the 10:31 open); at 10:45, if price is ≥ 0.3×ATR5 beyond the AVWAP and every 1m close since 10:35 is on that side → enter; skip if the 10:30 bar range > 3×ATR5; stop AVWAP ∓ 0.2×ATR5; 1.5 R, or exit on a 5m close across, or 12:00; MCL first. Halova/Kurov is cited for EIA surprise impact. *FOMC:* decide at 14:45: if ≥ 75% of 14:00–14:45 closes are on one side of the AVWAP_FOMC and the 14:45 close agrees → enter; stop AVWAP ∓ 0.25×ATR5; 2 R or 15:55. CPI/NFP use the same logic, deciding at 09:45. **A2-21 kill:** fewer than 100 pooled events, or a negative pooled expectancy (FOMC alone is ~8 per year). **A2-20 test and kill (restored v2):** the event study must first show drift from 10:45 to 12:00 conditional on the side; kill on the generic criteria, with **≥ 150 events (about 3 years of weekly releases) needed per sample** (not pooled). *(Grades: A2-20 original **C/B−** → catalogue **E**; A2-21 original **C** → catalogue **E**.)*

#### AV-07 · Climax-volume-bar AVWAP
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · author idea, inspired by Shannon (high-volume anchors) and Dale ("big limit orders", volume clusters).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; 5m anchor, 5m/15m signal.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (intraday arm); daily A2-17 arm on daily · W-D (decades; ES from 1997, NQ from 1999) only where daily volume is roll-consistent
- **Why it might work:** A volume climax (volume z ≥ 3 for its clock minute) at a session extreme is where the largest single cohort transacted. If price later trades *through* that cohort's AVWAP, a big block of positions flips from profit to loss.
- **Rules:** *Anchor:* a 5m bar with clock-minute volume z ≥ 3 that makes a session high or low. *Signal:* a 5m close crossing that AVWAP against the climax direction (e.g. through it upward after a selling climax low) + δ. *Entry:* the first HL. *Stop:* the climax-bar extreme, floored. *Target:* 1.5 R / trail.
- **Sweep:** z ∈ {2.5, 3.5} → 2; target → 4.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** the climax is a news spike (excluded by the blackout).
- **Needs:** ✓ M-AVWAP, ✓ M-TOD.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL-anchor (a random non-climax bar at the same extreme). **Kill:** ≤ fake (intraday arm); the A2-17 daily arm uses A2's kill above.
- **Merged · A2-17 (daily arm):** anchor at a daily bar with volume ≥ 2.5× its 20-day average, range ≥ 2×ATRd and close in the bottom 25% of its range; after ≥ 2 daily closes above that AVWAP, a 60m HL above it, entering on a 15m BOS; stop HL − 1 tick or AVWAP − 0.25×ATRd; T1 the anchor-bar high, T2 the swing-high AVWAP; time stop 15 sessions. A2's estimate is ~20–40 ES events since 2010, so pool ES/NQ/YM/RTY/CL/GC and require ≥ 100 pooled events. **Window (restored v2):** the **event study runs on daily bars (W-D) over all pooled-market history**, so pre-2016 events count; the 15m/60m execution layer (the strategy) exists only in W-M. **Kill (A2):** fewer than 100 pooled events, or a negative pooled expectancy. *(Grades: A2-17 original **C** → catalogue **H**.)*

#### AV-08 · Week-open / month-open / quarter-open AVWAP as regime layer
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Shannon (WTD/MTD/YTD AVWAPs); Telonics weekly/monthly anchors.
- **Evidence:** **B (0)** as levels (Telonics weekly bounce 50.50%, monthly 52.37%; weekly bias +0.0039) · **H** as a regime layer · Lit: Indep (null as level).
- **Markets · TF:** all; layer on intraday and swing hosts.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); MTD/QTD anchors multi-day levels reset at rolls
- **Why it might work:** Only as a slow-moving "who is in control this week/month" filter for continuation setups.
- **Rules:** layer `aboveWTD && aboveMTD` (long only) / `belowWTD && belowMTD` (short only), applied to PS-01, PS-07, MT-01, AV-02 events.
- **Sweep:** {WTD only, WTD+MTD, MTD+QTD} → 3 per host.
- **Frequency:** inherits the host's.
- **Needs:** F-VWAP slots (per-period re-anchor ✓ `EVwapType` Week/Month/Quarter).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** layer ablation. **Kill:** < 0.03 R change in host mean R, or it just mimics RG-06 (correlation > 0.8 with the TSMOM sign → redundant).
- **Merged · A2-07:** A2's stack = weekly AVWAP (from Sunday 18:00) + monthly AVWAP + AVWAP from the last confirmed daily swing low; it is tested on the pooled event set of Specs 1–3. Kill if lift < 0.03 R or the filter drops > 40% of trades without lift. Expected result: reject (the Telonics bias column ≈ 0). *(Grades: A2-07 original **C** → catalogue **B (0) / H**.)*
- **Merged · DV-14 (Trader Dale video: week-open AVWAP S/R with side flip as a standalone arm; Dale priority 1–2).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Conflict (P5). Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Source:** https://www.youtube.com/watch?v=difA0DwzXCE (30m; anchor to the first candle of the week; trade pullbacks; flips S↔R when crossed; best in trends).
  - **Rules:**
    - **[coding]** AVWAP from the week open.
    - Side = the side of the last 3 closes.
    - Limit at the AVWAP; skip when |slope| is small; f × ATR20d bracket.
  - **Evidence:** E.
  - **Books:** **Conflict** (P5; AV-08 treats it only as a regime layer). **Priority 1–2.**

#### AV-09 · Roll-anchored AVWAP swing bias
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · author idea; Keller B3 §4.5 (re-anchor AVWAPs at the roll).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ (quarterly), CL (monthly), GC (bi-monthly), ZN (quarterly); daily/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999); the anchor *is* the splice, so it fits unadjusted data naturally
- **Why it might work:** At the volume roll, open interest migrates into the new contract. Its AVWAP from the roll date is the new-contract cohort's average entry: a natural "positioning cost" line for the next 3 months.
- **Rules:** *Anchor:* the first session when the new contract's volume exceeds the old (F-ROLL). *Layer:* long-only above / short-only below, for swing hosts (AV-01/02, SW-*). Standalone variant: a daily close crossing the roll AVWAP with 2-day acceptance → enter on the next day's first 60m HL; stop the cross-day extreme; trail.
- **Sweep:** layer vs standalone → 2.
- **Frequency (est.):** standalone 0–2 per quarter per instrument.
- **Fails when:** too few events (stats will be thin).
- **Needs:** F-ROLL, ✓ M-AVWAP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** compare to an AVWAP anchored 10 sessions before or after the roll. **Kill:** no difference vs shifted anchors.
- **Merged · A2-23:** the same anchor (the first RTH bar of the day volume crosses into the new front month). Under dxFeed it coincides with the unadjusted continuous splice, which makes it the natural *first* multi-day anchor after every roll. A2's rule: treat it as one extra anchor inside a multiple-testing family (Holm); drop it unless it survives. A2 also uses this anchor for **A2-14's reclaim swing** (restored v2). *(Grades: A2-23 original **C** → catalogue **H**.)*

#### AV-10 · AVWAP × profile confluence (does stacking help?)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · Keller B1 card 3.6 (confluence by *independent* families); Keller B3 (effective n of confluence = n(1−ρ)+ρ).
- **Evidence:** **B (0)** for stacked confluence in general (Telonics stacked levels 46.9% vs 46.5% random walk; chart formations 46.5% vs 46.8%) · **D** for AVWAP + profile specifically · Lit: Indep (null) + Book.
- **Markets · TF:** ES/NQ, CL, GC; 5m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** An AVWAP (cohort cost) and a profile edge (where value was accepted) are built from different information. If both fall at one price, more distinct participants care about it.
- **Rules:** *Event:* host setups (AV-02, PS-12, MT-01) where the entry zone has a swing AVWAP within δ of a composite VAH/VAL/POC or nPOC. *Test:* confluent vs non-confluent host events.
- **Sweep:** δ ∈ {0.5, 1.0} × base δ → 2.
- **Frequency:** a subset of hosts.
- **Needs:** ✓ M-AVWAP, F-COMP, F-NPOC, F-CONF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **count-matched control:** random second "level" from a fake anchor at the same distance distribution. **Kill:** confluent − non-confluent < 0.05 R, or not above the count-matched control.

#### AV-11 · Breakout-bar AVWAP first pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · author idea; fits Spec 1 (anchor at the balance-break bar).
- **Evidence:** **H** (on top of PS-01's C-family) · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** The AVWAP from the bar that broke balance is the breakout cohort's cost. A pullback that holds there means the breakout buyers are still in profit and defending.
- **Rules:** *Anchor:* the first bar closing outside composite VAH/VAL in an accepted PS-01 break. *Entry:* the first pullback to that AVWAP ± 0.25σ with a 1m/5m HL. *Stop:* AVWAP − 1σ or below composite VAH − 0.25 W, floored. *Target:* as PS-01.
- **Sweep:** zone ∈ {±0.25σ, ±0.5σ} → 2.
- **Frequency (est.):** a subset of PS-01 (roughly 1/2–2/3 of its trades).
- **Needs:** ✓ M-AVWAP, PS-01 stack.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** runs **inside the PS-01 slot** as a `Variant`. **Kill:** no better than the HL-anywhere version (PS-01).
- **Merged · DV-13 (Trader Dale video: accumulation-end AVWAP pullback (anchor = rotation breakout bar); Dale priority 3).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Neutral. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:** https://www.youtube.com/watch?v=pKzXxB9Blts and https://www.youtube.com/watch?v=nbdLCKBGv7k.
  - **Rules:**
    - *[Dale]* anchor at "the last candle of the rotation, which is the first candle of the sell-off"; trade pullbacks to it; "when the market is rotating, VWAP setups don't really work"; once price crosses and holds the other side, the AVWAP flips to support.
    - **[coding]** Anchor = the DV-03 breakout bar (known at its close). Enter a limit at the AVWAP with-trend; skip if RG-02 = balance; flip side after 3 closed bars beyond it. The confluence variant requires the DV-03 level within 0.25 × ATR30m of the AVWAP.
  - **Evidence:** E.
  - **Sweep:** confluence ∈ {required, not required}; 2 × bracket → 4.
  - **Frequency:** 2–5 per month.
  - **Books:** neutral (AV-10 asks the same "does stacking help?" question). **Priority 3.**

#### AV-12 · Failed AVWAP reclaim → return to the opposing AVWAP
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV · author idea (mirror of AV-01; trapped-trader flavour).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; 15m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** Buyers who chased a reclaim of the swing-high AVWAP are trapped if the reclaim fails quickly. Their exit pushes price back to the swing-low AVWAP (the other cohort's cost).
- **Rules:** *Signal:* a 60m close above the swing-high AVWAP followed, within ≤ 3 bars, by a 60m close back below it − δ. *Entry:* sell-stop under that bar. *Stop:* the reclaim high + 1 tick, floored. *Target:* the swing-low AVWAP.
- **Sweep:** failure window ∈ {2, 3} bars → 2.
- **Frequency (est.):** 1–2 per month.
- **Fails when:** range-bound whipsaw (the same regime that hurts AV-01).
- **Needs:** ✓ M-AVWAP, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** pair with AV-01 in one slot (complementary regimes). **Kill:** ≤ 0 R for the AV-12 swing arm only (the A2-05 arm uses A2's kill above); defer the deep work to the trapped-trader round.
- **Merged · A2-05:** extends the anchors to event bars (CPI/NFP/FOMC release minute) and RTH opens of gaps > 1×ATRd. Context: ≥ 4 consecutive 15m closes below the event AVWAP. Trigger: a 5m bar trades above it and closes back below within 3 bars without 2 consecutive closes above; the breakout-excursion delta z ≥ +1. **Windows (restored v2):** the delta-filtered arm runs **only in W-T** (MC-BidAsk; no BarUpDown or tick-rule fallback counts as order-flow evidence); the **no-delta arm runs on W-M and serves as the control**. Entry on a 1m close below the failed-reclaim bar's low; stop excursion high + 1 tick; T1 1 R, T2 the event-bar low or AVWAP −1σ (band as *target*, never as entry). This is a confirmation entry, not a touch fade. **Kill (A2, restored v2):** generic criteria, plus the requirement to beat the same rule at a P1 line. *(Grades: A2-05 original **C** → catalogue **H**.)*

#### AV-13 · AVWAP σ-stretch exit / scale signal
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV/EX · author idea (AVWAP σ-bands from the entry's own anchor).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** any host with an anchor (AV-01..07, PS-01).
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A move to +2σ above the cohort's AVWAP *without* new value forming (developing POC lagging) is stretched. Scaling out there captures the fast part of the move (the low-b edge per R2/R5).
- **Rules:** scale 50% at anchor-AVWAP + 2σ if the developing POC is < AVWAP + 1σ; trail the rest.
- **Sweep:** {1.5σ, 2σ, off} → 3.
- **Needs:** ✓ M-AVWAP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** an EX-08-style ablation inside hosts. **Kill:** no improvement in R or in drawdown per unit of R.
- **Merged · A2-19:** on Spec 1–3 runners, replace the next-naked-POC target with the nearer of (event-AVWAP +2σ, next naked POC). Paired comparison on identical entries: net R and MFE capture ratio. Kill if lift < 0.02 R. *(Grades: A2-19 original **C** → catalogue **H**.)*

### 3.4 Family MT: Multi-timeframe confluence (7)

> **Family prior.** MTF alignment is the most commonly taught filter and the least tested. Independent stacked-confluence tests are null (Telonics 46.9% vs 46.5%). Keller's warning applies: correlated layers (all built from the same price series) are close to n = 1 effective vote (B3: n_eff = n(1−ρ)+ρ). **Implementation warning:** VPB-8 (MTF safety) is deferred, so until Data2 behaviour is verified, HTF bars must be built internally from the base series, using **closed HTF bars only**.

#### MT-01 · HTF-aligned higher low at value (60m → 15m → 5m → 1m nesting)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT · R5 T1; R4 alignment score A (daily TSMOM + composite location + 60m structure).
- **Evidence:** **D** · untested · Lit: Book/Report.
- **Markets · TF:** ES/NQ, CL, GC; 60m trend, 15m pullback, 5m/1m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999) (TSMOM on a roll-gap-removed series); multi-day levels reset at rolls
- **Why it might work:** When the slow timeframe is trending, pullbacks on the fast timeframe are counter-trend inventory that must be unwound when the fast timeframe resumes. Requiring the pullback to end *at value* (composite VAH or developing POC) adds a location where longer-term buyers are known to have transacted.
- **Rules:** *Filter:* daily TSMOM(20) > 0; last closed 60m structure HH/HL; the 15m pullback ends within δ15 of {composite VAH, developing POC, swing-AVWAP (if M-AVWAP)}. *Entry:* a 5m HL confirmed, buy-stop above it (1m HL in the fast variant). *Stop:* HL − 1 tick, floored. *Target:* the 15m swing high; EX-08 b sweep.
- **Sweep:** trigger TF ∈ {1m, 5m}; location set ∈ {profile only, profile+AVWAP} → 4; b ∈ {1, 1.5, trail} → 12.
- **Frequency (est.):** 6–15 per month per instrument (the highest-frequency candidate).
- **Fails when:** at HTF turning points (the HTF trend label is lagging); in correlated ES/NQ.
- **Needs:** ✓ M-SWING (multi-TF via `ExtraPriceAction`, PR #19), ✓ M-MTF (native BarsOfData(n), no internal HTF bar building), ✓ M-DAILY, F-COMP, F-DEV.
- **Readiness (v2.3b):** Buildable now on the merged base (extra PRICEACTION instances via `ExtraPriceAction`, PR #19; run MC Probe P2 first).
- **Test:** ablation: remove each layer one at a time (L-ladder). K-RT with the same HTF filter. **Kill:** the full stack ≤ the "HTF filter + random 5m HL" control.

#### MT-02 · Weekly accepted value + 60m trigger (swing)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT · Keller B1 (TF windows); Dalton (weekly value); author codification.
- **Evidence:** **D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC, ZN; weekly profile, daily acceptance, 60m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (weekly profiles need volume-at-price); multi-day levels reset at rolls
- **Why it might work:** When a new week opens above the prior week's VA and holds (acceptance), longer-timeframe participants have repriced. The prior-week VAH is where late weekly sellers were proven wrong, so it acts as support.
- **Rules:** *Filter:* by Tuesday close, ≥ 2 daily closes above the prior-week VAH. *Entry:* the first pullback to prior-week VAH ± 0.25 ATR20d with a 60m HL, Wed–Fri. *Stop:* back inside the prior-week VA by 0.25 × weekly VA height, floored. *Target:* prior-week VAH + 1 × weekly VA height; time stop 7 sessions.
- **Sweep:** acceptance days ∈ {1, 2}; target ∈ {1 × height, trail} → 4.
- **Frequency (est.):** 1–2 per month per instrument.
- **Fails when:** event weeks; the weekly profile is too wide (R tiny).
- **Needs:** ✓ weekly profile stream (`_WeeklyProfiles`, PR #18), ✓ M-MTF, ✓ M-SWING.
- **Readiness (v2.3b):** Buildable now on the merged base (run MC Probe P2 first).
- **Test:** K-FL (shift the weekly VAH ±0.5 ATR20d); K-DR (CL/GC/ZN). **Kill:** ≤ fake.

#### MT-03 · 20-day composite location gate + 5m acceptance break
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT · R4 alignment score A; Keller B4 ("trade with value migration").
- **Evidence:** **D/C** · untested as a gate · Lit: Report.
- **Markets · TF:** ES/NQ; 20-session composite, 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); 20-session composite multi-day levels reset at rolls
- **Why it might work:** Breaks of intraday structure in the *upper* third of a rising 20-day composite move with the longer-term auction. Breaks in the lower third against it run into responsive buyers/sellers from the longer timeframe.
- **Rules:** a gate layer for PS-01/PS-08/PS-24 events: long only if price ≥ 20-day composite POC and 20-day V-score ≥ 0.
- **Sweep:** POC vs VAH as the gate → 2 per host.
- **Needs:** F-COMP.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** layer ablation; **kill** if host mean R changes by < 0.05 R.

#### MT-04 · 60m swing-AVWAP + 5m HL entry
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT/AV · author idea (Shannon's MTF trend alignment + R5 T1).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** It combines the slow cohort's cost (60m swing-low AVWAP) with fast confirmation (5m HL) so that stops stay small relative to the swing target.
- **Rules:** *Filter:* price above the 60m swing-low AVWAP and the 60m AVWAP slope > 0 over 10 bars. *Entry:* 5m HL within 0.5σ of that AVWAP. *Stop:* HL − 1 tick, floored. *Target:* the 60m swing high.
- **Sweep:** zone ∈ {0.5σ, 1σ} → 2; target ∈ {swing high, trail} → 4.
- **Frequency (est.):** 2–5 per month.
- **Needs:** ✓ M-AVWAP, ✓ M-SWING, ✓ M-MTF.
- **Readiness (as of merge):** Buildable now on the merged base (run MC Probe P2 first).
- **Test:** K-FL-anchor; compare with MT-01 (the same entry with a profile location instead). **Kill:** ≤ MT-01.

#### MT-05 · 240m multi-session LVN as pullback zone
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT · author idea (Keller TF windows; Dale "levels valid 30–40 days").
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; a 240m-window composite (≈ 6 × 4h blocks), 15m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); multi-day levels reset at rolls
- **Why it might work:** The swing-timeframe LVN between two multi-day HVNs separates two acceptance zones. When price returns to it from above after leaving it, it is the edge of the newer value.
- **Rules:** *Level:* an LVN in the last 5-session ETH composite (smoothed on 4h-window profiles). *Entry:* first touch, then a 15m HL. *Stop:* the far side of the LVN, floored. *Target:* the upper HVN's POC / swing high.
- **Sweep:** composite length ∈ {5, 10} sessions → 2.
- **Frequency (est.):** 2–4 per month.
- **Needs:** F-COMP (ETH stream), F-HVN, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL, K-ST. **Kill:** ≤ fake.

#### MT-06 · Daily failed auction (excess / poor extreme) + intraday trigger
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** MT · Keller B1 card 3.5 (excess = completed auction; poor = unfinished); R4.
- **Evidence:** **D** · untested; poor extremes as magnets are (0) (Perfiltrade) · Lit: Book.
- **Markets · TF:** ES/NQ, GC, CL; prior-day profile, 5m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** A prior-day high with genuine excess (a long single-print tail) is a *completed* auction: the other side responded strongly. Intraday shorts that retest it have the daily seller behind them.
- **Rules:** *Level:* the prior day's high with an excess tail ≥ 4 ticks (single prints at the extreme). *Entry:* the first test within δ, then a 5m LH. *Stop:* above the tail high, floored. *Target:* prior-day POC.
- **Sweep:** tail ≥ {4, 8} ticks → 2.
- **Frequency (est.):** 2–4 per month.
- **Needs:** F-PROF (excess = SinglePrints at the extreme), ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** compare excess highs vs poor highs vs plain prior-day highs (fade PF 1.01–1.02 baseline per R4). **Kill:** excess ≤ plain.

#### MT-07 (= KD-05) · HTF detrended-rail alignment (15m PS-25 entries on 60m/240m accepted rails)
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Family · Source:** MT · Keller B5 0.3 measured line "confirmed on TF" (Analyst 2 notes, loc ~155–181). Rev 3.1.
- **Evidence:** **H** — the book reports "confirmed on [higher] TF" on cards but no census by timeframe alignment ([GAP] App. B/D). · **FX/crypto only · no true delta · ES prior 0**. *(Original grade (B5 rev 3.1): same.)*
- **Markets · TF:** ES; 15m entries, 60m (variant 240m) M-DVP stream from the **last closed** HTF bar.
- **Data · test window:** 1m-built bars · W-M (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: a fit window may not cross a roll (`crossesRoll` skip; skip share journaled per stream, > 30% → diagnostic-only); ETH primary, RTH-only sensitivity arm; RTH share of fit-window volume journaled, results split {<50%, ≥50%} report-only.
- **Rules:** PS-25 (KD-01) on 15m only when the 60m (variant 240m) M-DVP stream, from its last closed HTF bar, shows the same-direction acceptance state (close beyond its VAH/VAL for ≥ 2 HTF bars) and the 15m target rail lies inside the HTF next-rail distance.
- **Sweep (≤ 4):** HTF ∈ {60m, 240m} × HTF acceptance bars ∈ {1, 2}.
- **Needs:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) on Data2/Data3; ✓ M-MTF (closed HTF bars).
- **Readiness (v2.4):** Engine on main (PR-D0…D8); consumer rule not coded; may start only after PR-D9 freezes d*; Data2/Data3 HTF wiring done (PR-D6, #41); Probe P2 still open.
- **Gate (v2):** G0-d for DVP level types; Probe P2 (HTF bar-end stamping) must pass first. Not a Holm member.
- **Test · Kill:** vs PS-25 unfiltered and vs a time-shifted HTF state (state from 1 HTF bar earlier — tests lookahead sensitivity). **Kill:** no lift, or lift appears only with forming-bar data.

### 3.5 Family OF: Order-flow overlays (10)

> **Family prior and data limits (Rev 2.7).** No aggressor field is exposed. MC's bid/ask-traded split (built from dxFeed Bid/Ask ticks) is readable from code **only inside the ≈ 2-year tick window (W-T, ≈ 500 sessions)**, so every OF idea is tested on W-T with **≤ 4 configs** and a 6-month holdout. Earlier minute history carries BarUpDown (Low) flow only. Absorption *sorted trades the wrong way* at sweeps (Telonics, B −). The tape at VWAP shows flow pushing through, not absorbing (Telonics). Order flow is therefore tested only as a **layer on host events**, reported per quality tier, with the **K-SH shuffled-flow placebo mandatory**.

#### OF-01 · Stacked-imbalance origin: first retest continuation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · Dale OF setup #4 (≥ 3 stacked at 300%, first retest only); Keller B1 card 6.3.
- **Evidence:** **E/D** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC; 1m/5m footprints.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs)
- **Why it might work:** Three or more consecutive diagonal imbalances mean aggressive buyers lifted the offer through several prices. If they are still positioned, the first return to that zone is defended.
- **Rules:** *Zone:* ≥ 3 stacked buy imbalances (3:1, volume floor P70) on a 5m bar that closes in its upper 30%; price then moves ≥ 1 ATR5 away. *Entry:* the first retest of the zone (limit at the zone top vs a 1m HL: K-LM). *Stop:* below the zone − 1 tick, floored. *Target:* the prior high / 1 R.
- **Sweep:** ratio ∈ {3:1, 4:1}; entry ∈ {limit, HL} → 4.
- **Frequency (est.):** 4–10 per month (depends heavily on TickRule noise).
- **Fails when:** imbalances are artefacts when only TickRule is available (prefer MC-BidAsk); 500 sessions give few events.
- **Needs:** F-OF (StackedImbalances, quality ≥ Medium).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** **K-SH** and per-quality-tier reporting; K-FL (shift the zone ±0.5 ATR5). **Kill:** ≤ shuffled placebo.
- **See also (v2.7):** OF-09 uses this stacked-imbalance definition (3:1, P70, ≥ 3) as its buildable pullback zone. This entry's rules are unchanged.

#### OF-02 · Delta-z ratification of the breakout bar (layer)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · R3 (delta z by clock minute); Dale OF ("aggressive orders + delta").
- **Evidence:** **D** · untested · Lit: Report.
- **Markets · TF:** host events PS-01, PS-08, PS-24.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs) (layer on host events inside W-T)
- **Why it might work:** A breakout with net aggressive buying unusual for that clock minute signals initiative participants, rather than a drift through thin liquidity.
- **Rules:** layer: breakout-bar delta z ≥ +1 (longs).
- **Sweep:** z ∈ {0.5, 1.0} → 2 per host.
- **Needs:** F-OF, ◐ M-TOD (delta z: futures base, W-T tiers only).
- **Readiness (as of merge):** Buildable with caveats — M-TOD (delta z: futures base, W-T tiers only).
- **Test:** K-SH. **Kill:** layer lift < 0.05 R, or lift ≤ the shuffled placebo.
- **See also (v2.7):** OF-09's 30m breakout bar reuses this delta-z gate (`z*` ∈ {0.5, 1.0}) inside a value-area cascade. This entry's rules are unchanged.

#### OF-03 · Anchored CVD flip at a failed break (layer)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · R4 C1; Keller B2 15.4.
- **Evidence:** **D** · untested; adjacent absorption evidence is negative · Lit: Report.
- **Markets · TF:** host PS-03, AV-12.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs)
- **Why it might work:** CVD anchored at the poke start that turns negative while price is still above the level shows the breakout buyers have stopped and sellers are hitting bids.
- **Rules:** layer: anchored CVD since the poke start < 0 at the entry bar.
- **Sweep:** on/off → 2 per host.
- **Needs:** F-OF (CumDelta with a custom reset key).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-SH. **Kill:** as OF-02.

#### OF-04 · Absorption at an aged HTF level (control)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · Dale OF (absorption confirmation); Keller B2 15.3 (λ residual); R4 C4/H3.
- **Evidence:** **B (−).** Absorption sorted trades the wrong way at sweeps (Telonics) · Lit: Indep.
- **Markets · TF:** ES/NQ; 1m.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs); tier ≥ 3 (MC-BidAsk or Quote) required
- **Why include it:** It is the most popular order-flow claim, so it should be tested directly as a control. Expected result: it predicts *continuation through* the level, not reversal.
- **Rules:** at a ≥ 5-session-old level, a 1m bar with volume z ≥ 2 and |price change| ≤ 2 ticks (absorption proxy) → fade; and a mirror variant → continuation.
- **Sweep:** fade vs continue → 2.
- **Needs:** F-OF Absorption at tier ≥ 3 (MC-BidAsk or Quote) → W-T only.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-SH. **Kill:** the fade version ≤ 0 is expected; promote the continuation mirror only if it beats K-SH.

#### OF-05 · CVD divergence at a value extreme
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · Dale OF (cumulative-delta divergence on 1m); Keller B1 card 6.1; R4 S6.
- **Evidence:** **E** (vendor-type claim) · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 1m/5m.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs)
- **Why it might work:** A new high at VAH with lower CVD than the prior high means fewer aggressive buyers are needed (or available). The poke is passive drift into stops.
- **Rules:** *Signal:* a new session high within δ of VAH; session CVD at the new high < CVD at the previous swing high. *Entry:* the first 5m LH. *Stop:* high + 1 tick, floored. *Target:* developing POC.
- **Sweep:** CVD reset ∈ {RTH, 60-min rolling} → 2.
- **Frequency (est.):** 3–6 per month.
- **Needs:** F-OF, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-SH (critical), K-FL. **Kill:** ≤ placebo.
- **Merged · DV-18 (Trader Dale video: 4-hour profile POC + 1m CVD divergence variant; Dale priority 2).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: P6 untested. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:** https://www.youtube.com/watch?v=TOUVbUvpNmI (6E; 1m chart with profiles every 4h; on the pullback to the 4h POC, enter if the CVD diverges from price) and https://www.youtube.com/watch?v=AxZCJtl2CGc.
  - **Rules:**
    - **[coding]** 4h block profiles (ETH clock blocks). Level = POC of the last completed block.
    - On the approach (last 10 1m bars): price makes a new low toward the level while the CVD makes a higher low (or the CVD slope is opposite to the price slope).
    - Enter at the touch; SL f × ATR20d.
  - **Evidence:** E.
  - **Books:** P6/OF-05 untested; P4 (POC levels ≈ controls). **Priority 2.**

#### OF-06 · Unfinished-auction veto (layer)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · Dale OF p. 88 (unfinished business: bid and ask both traded at an extreme → the market tends to return; veto trades toward it as stop placement).
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** layer on all intraday hosts.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs); MC-BidAsk tier required
- **Why it might work:** A session extreme with volume on both bid and ask (no zero-print) is "unfinished". Dale treats it as a magnet, so it should be a bad place to hide a stop behind.
- **Rules:** veto any trade whose stop sits beyond an unfinished extreme within 2 ticks.
- **Sweep:** on/off.
- **Needs:** F-PROF (unfinished auction) + F-OF tier ≥ 3 (MC-BidAsk); TickRule cannot tell bid from ask at a single price reliably.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** veto ablation. **Kill:** < 0.03 R effect.
- **Merged · DV-19 (Trader Dale video: failed-auction cluster as target extension / stop veto; Dale priority 3).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: Agree as target. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:** https://www.youtube.com/watch?v=EpmpBFenzPE (a footprint high/low without a zero on the bid/ask; "I'm not saying you just go short… what does work is a cluster of failed auctions… if price comes close… very likely it will test"; hold a trade to 1 pip beyond the last failed auction) and https://www.youtube.com/watch?v=WRlc0LF-rEQ.
  - **Rules:**
    - **[coding]** Unfinished auction per bar extreme (F-PROF/F-OF).
    - Cluster = ≥ 2 within 0.5 × ATR5.
    - When price is within 0.5 × ATR5 of the cluster in the trade direction, extend the TP to 1 tick beyond the farthest one. Veto stops placed behind a cluster.
  - **Evidence:** E.
  - **Books:** **Agree** as a *target* (P2, Keller unfinished business); OF-06 is the same veto. **Priority 3 (exit layer).**

#### OF-07 · Bar-level burst at a fresh extreme (layer / standalone)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · R3/R4 C3/H1 (burst-through mechanism: real levels run through more often).
- **Evidence:** **B (+) for the run-through behaviour** (Telonics +3.7–11.6 pp) · setup untested · Lit: Indep.
- **Markets · TF:** ES/NQ; 1m.
- **Data · test window:** bar-level variant: 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); seconds-level burst: 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs)
- **Why it might work:** A 1m bar that takes out a session/ON extreme with volume z ≥ 2 and range ≥ 1.5 × its clock-minute norm is stop-driven acceleration, the mechanism behind the run-through behaviour.
- **Rules:** *Standalone:* enter in the break direction at the next bar; stop at the break bar's midpoint, floored; target b ∈ {0.5, 0.75, 1} (fast-decaying edge per R5). *Layer:* the burst condition on PS-24 events.
- **Sweep:** z ∈ {2, 3}; b → 6.
- **Frequency (est.):** 6–15 per month.
- **Needs:** ✓ M-TOD; true seconds-level burst ✓ M-TICK (`_TickStream` burst flag, W-T).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (bursts through fake extremes). **Kill:** ≤ fake.

#### OF-08 · Large-trade print zone retest
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · Dale OF setup #3 (trades filter: ES ~300+ lots).
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** ES, CL; 1m.
- **Data · test window:** 1-tick + Bid/Ask (MC-BidAsk) · **W-T only** (Jul 2024→Oct 2026, ≈500 sessions; holdout = last 6 months; ≤4 configs); per-print size from dxFeed ticks (whether CME prints arrive aggregated is unverified, Rev 2.7 E6)
- **Why it might work:** A cluster of very large prints marks institutional interest at a price, and they defend it on retest.
- **Rules:** *Zone:* the price with ≥ 3 prints ≥ N lots within 5 min. *Entry:* first retest, 1m HL. *Stop:* the zone − floor. *Target:* 1 R.
- **Sweep:** N ∈ {200, 300} (ES) → 2.
- **Needs:** ◐ M-TICK (`_TickStream`): **per-trade size** outputs gated off until §E6 (W-T only). If CME prints arrive aggregated per aggressor order, "large trades" are not institutional fills (verify via Rev 2.7 §E6); park it until confirmed.
- **Readiness (as of merge):** **Parked** — needs per-trade size from M-TICK, which stays gated off until Rev 2.7 §E6 is verified (`TickConfig.sizeOutputsVerified`).
- **Test:** K-FL. **Kill:** data unavailable → document as blocked.
- **Merged · DV-17 (Trader Dale video: large-trade (trades-filter) zone pullback; Dale priority 2).** Original DV grade: E → catalogue grade **E**. Books agree/conflict: P6 untested. Dale's specification, verbatim (`trading-ideas-dale-video.md`):
  - **Sources:** https://www.youtube.com/watch?v=qPwi3_L6OwY (30m preferred; set the minimum trade size per market, e.g. 6E ≈ 30–35 lots; price must move away, then pull back → trade in the move-away direction) and https://www.youtube.com/watch?v=7reCIPz5-kg (6E 30m, filter 25, "TP 10 / SL 10 ideal").
  - **Rules:**
    - **[coding]** Print ≥ size threshold (per-market 99.5th percentile of trade size, W-T).
    - Zone = the price of the print. Move-away ≥ 1 × ATR30m. First pullback: limit at the zone in the move-away direction. Bracket 1:1.
  - **Evidence:** E.
  - **Books:** P6; OF-08 covers the same thing. **Priority 2.**

#### OF-09 · Weekly bias, 30m value-area break, 1m failed-seller (order-flow cascade)
- **Implementation status (v2.7):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF · YouTube, Trader Oxygen, *Most Traders Enter Too Early: Order Flow Entry (World Champion Method)*, https://youtu.be/jmX_ReJxFm8. Transcript checked against the 2026-11-08 auto-captions. The discretionary extract is **YV-01**. Longs are written out; shorts mirror. Selection across ES and NQ is **OF-10**. Partial, breakeven and trail are **EX-13**. This entry is the signal and the bracket.
- **Evidence:** **E**. One narrated example. Spoken management: a partial near 1:1.5 and a stop-out near 1:2.5. Both are presenter claims about that example, not a sample. Lit: YouTube. Adjacent evidence is hostile: OF-04 absorption sorted the wrong way at sweeps (Telonics, **B (−)**); OF-08's large-print retest is parked. Do not treat the example's R-multiple as an expectancy.
- **Markets · TF:** ES or NQ, one instrument per run (OF-10 is the only thing allowed to choose between them). Micro for execution (MES/MNQ) under the same rules. Weekly completed profile for bias; 30m for the balance and the value area; 5m for the pullback zone; 1m for the trigger. The video's context chart was MNQ and the fill was MES; the rules are identical on either root and are not fit to that one morning.
- **Data · test window:** 1-tick Trade + Bid + Ask · **W-T only** (≈ Jul 2024–Oct 2026; @ES bid/ask from 2024-10-28; 250/125/125; **≤ 4 configs**). FlowQuality must be MC-BidAsk or Quote (High). BarUpDown is never a signal. There is no aggressor field. The price skeleton (balance, break, pullback) may be **logged** on W-M with flow fields set to NaN, as a control series, and is not a fill. Big-trade size is gated: `TickConfig.sizeOutputsVerified = false` until Rev 2.7 §E6 (live check). CME may aggregate prints per aggressor order, so a "big trade" is not known to be one institutional order.
- **Why it might work:** A value area that has held is the price the passive inventory will defend. A close that leaves it on heavy, one-sided delta and does not come back is initiative, not a poke (the same mechanism as PS-01 / OF-02). The first pullback into the initiative prints is where late sellers try to fade and fail. DV-16 already bets on a failed footprint at a **pre-defined** level. Here the level is the value-area break itself.
- **Rules:** Closed bars only. Higher-timeframe values are read from the last **closed** HTF bar (`HtfClosed`). Longs below; shorts mirror (VAH↔VAL, buy imbalance↔sell imbalance).
  - **Weekly bias, fixed for the session:** from `_WeeklyProfiles`, `includeDeveloping = false`, RTH sessions, `minSessions = 3`, roll policy Reset. A week with `crossesRoll` is ignored. Bias is known only after the week is complete (after Friday 16:00 ET), and it does not update on the developing week. The video looked at the current week; **this spec does not**, because that profile repaints. `bias = +1` if the last completed week's close > that week's POC **and** that close > the prior completed week's close. `bias = −1` on both inequalities the other way. Otherwise `bias = 0` (the "choppy" case). The daily profile is **not** a filter. The video's daily read ("range, no direction") did not cancel the trade.
  - **30m balance:** on closed RTH 30m bars, a suffix of n bars ending at the latest closed bar, n ∈ [4, 12], is a balance when `(max high − min low) ≤ 1.5 × ATR(14)` on 30m, ATR from bars **before** the suffix's last bar. The active balance is the longest such suffix. `_RangeProfile` on those bars only: distribution mode 2 (uniform over each bar's range), `valueAreaPercent = 0.70`, `useTickLadder = false`, `rollPolicy = Reject` (a suffix that touches a splice is dropped). VAH, VAL, POC usable from the **next** 30m bar.
  - **Breakout (the level):** the first later closed 30m bar whose close is ≥ `VAH + δ`, with δ = max(2 ticks, 0.10 × ATR5) (§2.1), **and** that bar's volume z ≥ +1 **and** its delta z ≥ `z*` **and** its bar delta > 0. z is the M-TOD z against the same clock bucket over the prior 20 sessions, MC-BidAsk delta. Then the **next** closed 30m bar must still close above VAH (it did not return). If it closes back through VAH, cancel. The 09:30 bar may be the breakout bar; the **entry** is still forbidden before 09:45 (§2.1). One breakout per RTH session. The pullback must happen the same session or the setup expires at 15:30.
  - **Pullback zone, 5m, buildable arm (no print size):** the price rows of buy-side `StackedImbalances` on closed 5m bars from the breakout bar's open through the bar before the 1m trigger. Stack = the §2.1 definition: diagonal **3:1**, volume floor at the 70th percentile, **≥ 3** consecutive. If that set is empty, the zone is the upper 30% of the breakout 30m bar's range: `[low + 0.70×(high−low), high]`. The 1m pullback must trade into the zone (`low ≤ zone high`) and close back above the zone low, and it must stay above VAH.
  - **Big-trade zone, parked, not one of the 4 configs:** a print is big when its size ≥ the **P99.5** percentile of trade sizes over the prior 20 RTH sessions (arm P99, not crossed in). Buy-side = ask-traded size on the MC-BidAsk split (there is no aggressor flag). Zone = those print prices ± 2 ticks on the breakout leg. **Do not run this arm** while `sizeOutputsVerified` is false. It replaces the stacked-imbalance zone when it is someday enabled; it does not add to it inside the same config.
  - **1m trigger (failed seller):** on a closed 1m bar S inside the zone, `StackedImbalances` sell-side ≥ 3 (same 3:1 and P70). The next closed bar S+1 **closes above S.high** (the sellers did not extend) **and** S+1's bar delta > 0. Entering on the first up-close **without** S is forbidden (the video's explicit negative example). Buy **limit** at S+1's close, working for 3 one-minute bars, then cancelled.
  - **Stop:** the lowest 1m low from the first bar that traded into the zone through S+1, minus 1 tick, then the §2.1 floor (ES 14 ticks, MES 20, NQ 19, MNQ 35). Skip if the widened stop breaches 1% of equity.
  - **Counter-HTF size:** if `bias ≠ 0` and the breakout direction ≠ `bias`, size at **0.5×** (floor; floor 0 → skip) and the **target is 1.5R with no runner**. If `bias = 0` or the breakout agrees with `bias`, size at 1% and the runner, if any, is EX-13's job. The video's "in and out quickly" is this 1.5R cap, not a discretion.
  - **Target when EX-13 is off:** 1.5R bracket, no partial. Flat 15:55. News blackout −2/+10 still applies.
- **Sweep (the only four W-T configs):** `z*` ∈ {0.5, 1.0} × counter-HTF ∈ {0.5×, skip}. Primary, read first: **`z* = 1.0`, counter-HTF = 0.5×**, stacked-imbalance zone, failed-seller required. Balance length, the 1.5× ATR cap, the 3:1 stack, the empty-zone fallback and the 1.5R target are frozen. The big-trade percentile is not in these four.
- **Frequency (est.):** a few times a month per root inside W-T. Author estimate only. About 500 sessions will not support a finer grid.
- **Fails when:** the breakout delta is the clock-minute's usual open burst (z does not separate it); stacked imbalances are footprint noise; the counter-HTF half-size still loses the full trend the weekly bias was pointing at; §E6 later shows the parked bubble arm was the only part the presenter was actually using.
- **Needs:** ✓ F-COMP is **not** the balance (that helper is multi-session). The balance profile is ✓ `_RangeProfile` (VP-1) · ✓ `_WeeklyProfiles` (VP-2) for the completed week · ✓ F-OF `StackedImbalances`, `BarDelta`, `CumDelta` at MC-BidAsk · ✓ M-TOD delta z and volume z (W-T) · ✓ M-MTF closed 30m and 5m · ✓ M-SWING not required for the trigger (the stop is the pullback low, not a fractal) · ◐ M-TICK size outputs **gated** (§E6) for the parked zone only · ◐ M-EVENT blackout · ✗ no dual-instrument footprint (that block is OF-10, not this entry).
- **Readiness (v2.7):** The four-config arm is buildable on W-T now, without print size. The big-trade zone stays parked with OF-08 until §E6 passes. Not buildable on W-M as a trade.
- **Gate:** G0-a and G0-b. The value area is built the same day from closed bars, so G0-d (prior-session level persistence) is not a prerequisite. K-SH is mandatory regardless.
- **Test:** on the same breakout events, the price-only control is PS-01's trigger: first confirmed 1m higher low inside the zone, no imbalance and no delta gate. **K-SH:** permute bar deltas inside the session and rebuild the delta-z gate and the failed-seller delta. Report MC-BidAsk and Quote separately; drop BarUpDown. **Kill:** lift over the price-only higher-low ≤ 0, **or** lift ≤ K-SH, on the first 250 sessions, at the primary cell. The other three cells are robustness, not a second chance. Below 30 events: no evaluation. Do not drop the failed-seller bar to raise the count.
- **Differs from:** **DV-16** (confirmation at a pre-defined Dale level; arms a–d. OF-09 builds the level from the 30m balance and uses only the failed-stack arm, plus a weekly size scalar). **OF-01** (first retest of a stacked-imbalance origin; no value-area break and no failed-seller sequence). **OF-02** (delta-z layer on PS-01/PS-08/PS-24; the `z*` values {0.5, 1.0} are reused and then stopped). **OF-08 / DV-17** (large-print zone, P99.5; that definition is the parked arm here, not the entry). **PS-01** (accepted balance break, first higher low; the price control). **YV-01** (same video, verbal rules). **EX-11** (flow trail; the trail lives in EX-13).
- **Sources:**
  - https://youtu.be/jmX_ReJxFm8 (auto-captions, 2026-11-08: weekly POC magnet and bearish weekly read ≈ 01:48–03:02; daily chop ≈ 03:06–03:50; 30m fixed-range value area and the 09:30 break with volume, green delta and rising CVD ≈ 03:53–07:40; counter to the weekly bias, smaller and quicker ≈ 06:48–07:14; 5m green bubbles ≈ 08:54–10:19; do not buy the first up candle ≈ 11:43–12:28; buy limit, stop under the 1m swing ≈ 15:19–15:25)
  - YV-01 (§3.19). Engines: `_RangeProfile`, `_WeeklyProfiles`, `_OrderFlow` stacked imbalances, `_TodNormaliser`.

#### OF-10 · ES vs NQ relative order-flow selection (layer on OF-09)
- **Implementation status (v2.7):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF (layer) · same video, https://youtu.be/jmX_ReJxFm8, the 11:00 ET switch from MNQ to MES. The discretionary extract is **YV-02**. The score below is a **catalogue proposal**. The video said "more bullish confluence", more green bubbles, a failed bearish stack, and a more bullish CVD. It did not state a formula.
- **Evidence:** **E**. One example. No statistic. Lit: YouTube.
- **Markets · TF:** ES and NQ (micros for the fill). Compared on the closed 1m bar where OF-09's trigger completes, plus the prior two closed 1m bars (three bars).
- **Data · test window:** W-T only, and **only on OF-09's primary cell** (`z* = 1.0`, counter-HTF 0.5×). NQ bid/ask depth is unverified (YV-02). If NQ's MC-BidAsk history does not cover the ES window, the overlap is the test, and the rest is dropped. ≤ 4 configs, and this layer uses two of them, not times four.
- **Why it might work:** ES and NQ usually move together. If one footprint shows the seller failing and the other does not, the failing-seller leg is the one the video switched to. It can also be noise: the second leg's CVD z has a wide error on three bars.
- **Rules:** Run OF-09's **detection** (not the order) on both roots. Instruments stay one position (§2.1): at most one fill.
  - Let the **host** be a root that just closed a valid OF-09 trigger (S+1).
  - On each root, over the three closed 1m bars ending at that timestamp, with that root's own MC-BidAsk footprint:
    - `cvdZ` = M-TOD z of the sum of the three bar deltas, same clock bucket, prior 20 sessions. Missing z → that root is not scorable.
    - `imb` = 1 if a failed opposite stack printed (OF-09's S / S+1 definition) on that root inside the three bars, else 0.
    - `big` = 1 if a same-side print ≥ P99.5 occurred, else 0. **`big` is held at 0** until §E6. It is not in the sweep.
  - `score = cvdZ + imb + big`.
  - The other root is eligible only if its last 1m bar traded into **its own** OF-09 pullback zone (no entry on a pretty footprint that is not at the level).
  - If the other root is ineligible, or unscorable, take the host.
  - If both scores < 0, skip.
  - If the eligible other score ≥ host score + 0.5, take the other root. Otherwise take the host. Ties within 0.5 stay on the host.
  - Size, stop and target are OF-09's, measured on the **chosen** root (the stop is that root's pullback low, not the host's).
- **Sweep:** on the primary OF-09 cell only: score ∈ {`cvdZ` alone, `cvdZ + imb`} → 2. Primary: `cvdZ + imb`. Do not re-sweep `z*` here.
- **Frequency (est.):** a subset of OF-09 events. Author estimate only.
- **Fails when:** the score picks the noisier leg; NQ and ES stops are not the same R because the point values differ (they are not: R is the chosen root's own stop); the second footprint is stale.
- **Needs:** ✗ **a second order-flow engine.** F-OF today is one stream. ✓ M-XMKT streams the other instrument's **price**, return, correlation and beta, and it does **not** build a footprint. ✓ M-MTF can host a second series. The missing piece is an `_OrderFlow` instance on Data2 with its own MC-BidAsk tier and its own M-TOD delta. Until that exists this layer does not run. Do not approximate it with M-XMKT's return z.
- **Readiness (v2.7):** **Not buildable.** Specified so the layer can be coded when the second footprint exists. No dual-instrument order-flow engine is in the repo.
- **Test:** paired, same OF-09 events: R of the host leg vs R of the chosen leg, costs on each root (ES $17, NQ $9.48 unconfirmed, micros at their own §2.1 rates). **K-SH** on both legs' deltas. **Kill:** chosen-leg mean net R ≤ host-leg mean net R on the first 250 sessions. Below 30 paired events: no evaluation.
- **Differs from:** **YV-02** (same video; the score here is the formula that note left open). **PF-03** (correlation monitor; it does not choose the fill). **M-XMKT** (infrastructure). **A2-37** (cross-instrument dislocation as a fragility **filter**, not an execution choice). **OF-09** (the host; this layer never fires alone).
- **Sources:**
  - https://youtu.be/jmX_ReJxFm8 (≈ 12:33–14:54: MNQ showed a red bubble and no green bubbles; MES showed green bubbles, a bearish stacked imbalance that failed, and a more bullish CVD; the fill was MES)
  - YV-02 (§3.19)

### 3.6 Family SP: Structural price action (9)

> **Family prior.** Chart formations hit targets at 46.5% vs 46.8% for a random walk (Krueger, B 0). Prior-day highs/lows react +2.3 pp vs random, but fading them yields PF 1.01–1.02 (R4, B 0). The breakout-pullback family is the one with supportive mechanism evidence (K4 continuation). **Two of the eight are trapped-trader-type entries; they are listed here only where natural, and their deep treatment is deferred.**

#### SP-01 · Failed double top → upside breakout (T4)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · R5 T4.
- **Evidence:** **D/H** · untested · Lit: Report.
- **Markets · TF:** ES/NQ, CL, GC; 5m/15m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** Shorts who sold the second top with a stop just above it are forced to cover when price takes out the double top. Breakout buyers join.
- **Rules:** *Pattern:* two confirmed 15m swing highs within 0.25 × ATR15 of each other, ≥ 6 bars apart, with a pullback ≥ 1 ATR15 between them. *Entry:* the first 15m close > max(high1, high2) + δ while the developing POC ≥ the pattern midpoint. *Stop:* below the breakout bar's low, floored. *Target:* the pattern height projected (1×).
- **Sweep:** POC condition on/off → 2; target ∈ {1×, trail} → 4.
- **Frequency (est.):** 2–4 per month.
- **Needs:** ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT, K-FL (shift the pattern level). **Kill:** ≤ 0 R or < a random breakout of a 20-bar high.

#### SP-02 · Double top at VAH with second-test divergence (T2, control)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · R5 T2.
- **Evidence:** **B (0)** (formations ≈ random walk) · Lit: Indep.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); divergence arm only in W-T
- **Why include it:** As the null twin of SP-01 (same pattern, opposite trade), so the slot measures both directions.
- **Rules:** second test within δ of VAH, with a lower delta or lower volume than the first; enter on the LH; stop above; target developing POC.
- **Sweep:** divergence on/off → 2.
- **Needs:** ✓ M-SWING, F-OF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** runs in the SP-01 slot. **Kill:** expected.

#### SP-03 · Wyckoff spring at composite VAL with a test (T3)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · R5 T3 (Wyckoff); Levy (sweep buy on declining aggression).
- **Evidence:** **B (0/−)** weak/negative (sweep fades 49.9% vs 50.0% at fake levels) · Lit: Indep.
- **Markets · TF:** ES/NQ, GC; 15m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work:** A spring below the range low that is reclaimed, followed by a *low-volume test* that holds, shows that supply is exhausted. The test is what distinguishes it from a plain sweep fade.
- **Rules:** *Spring:* a 15m low < composite VAL − δ, closing back above VAL within 2 bars. *Test:* the next pullback stays above the spring low with volume z < 0. *Entry:* a 15m HL after the test. *Stop:* spring low − 1 tick, floored. *Target:* composite POC → VAH.
- **Sweep:** test required on/off → 2.
- **Frequency (est.):** 1–3 per month.
- **Needs:** F-COMP, ✓ M-SWING, ✓ M-TOD.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** the no-test version is the canary (S7-like). **Kill:** ≤ canary.

#### SP-04 · Lower high after a failed auction at a composite extreme (T6)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · R5 T6.
- **Evidence:** **D** · untested; the fade family is ≈ chance · Lit: Report.
- **Markets · TF:** ES/NQ; 15m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work:** After a failed auction above the composite VAH, the first lower high confirms that the probe found no buyers. Composite-level sellers re-engage.
- **Rules:** probe ≥ δ above composite VAH that fails acceptance; then a 15m LH below the probe high; sell-stop under it; stop above the LH, floored; target composite POC.
- **Sweep:** LH TF ∈ {5m, 15m} → 2.
- **Frequency (est.):** 2–3 per month.
- **Needs:** F-COMP, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** shares the slot with PS-03 (same mechanism, different TF). **Kill:** ≤ 0.

#### SP-05 · Four-part reversal-bar event at a profile extreme
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · R4 §3.9 (four-part event: level + extension + reversal bar + flow).
- **Evidence:** **B (0/−)** family · Lit: Report.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); delta condition only in W-T
- **Why it might work:** It would require all four conditions together, testing whether conjunction adds anything over the components (Keller: correlated votes ≈ one vote).
- **Rules:** (1) at prior VAH/VAL/composite edge ± δ; (2) bar range ≥ 1.5 × ATR5; (3) close in the opposite 25% of range; (4) bar delta opposite the bar's direction (TickRule). Enter next bar; stop beyond the bar; target 1 R.
- **Sweep:** delta condition on/off → 2.
- **Frequency (est.):** 2–4 per month.
- **Needs:** F-PROF, F-OF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** report each condition's marginal contribution; K-SH. **Kill:** conjunction ≤ best single condition.

#### SP-06 · First higher low after a downtrend at composite VAL (change of character)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · author idea (SMC "CHoCH" codified with profile location).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; 15m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work:** At composite VAL, the longer timeframe has a reason to buy. The first HL plus a break of the last LH shows that short-term sellers are no longer able to make new lows.
- **Rules:** *Filter:* a 60m downtrend (LL/LH) into composite VAL ± δ. *Signal:* a 15m HL, then a close above the last 15m LH. *Entry:* the first 5m pullback HL after that. *Stop:* below the 15m HL, floored. *Target:* composite POC.
- **Sweep:** TF pair ∈ {(15,5), (60,15)} → 2.
- **Frequency (est.):** 1–3 per month.
- **Needs:** F-COMP, ✓ M-SWING (multi-TF via `ExtraPriceAction`, PR #19).
- **Readiness (v2.3b):** Buildable now on the merged base (extra PRICEACTION instances via `ExtraPriceAction`, PR #19).
- **Test:** K-FL (fake VAL). **Kill:** ≤ fake.

#### SP-07 · Failed-failure re-break (Spec 4)
- **Implementation status (v2.4):** **Spec'd** — final-synthesis Spec 4; no strategy code.
- **Family · Source:** SP · final-synthesis Spec 4; R1 P5.
- **Evidence:** **D** · untested · Lit: Report. Trapped-trader flavour.
- **Markets · TF:** ES/NQ; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); composites multi-day levels reset at rolls
- **Why it might work:** A balance break fails (back inside), shorts pile in for the rotation, then price re-breaks the same edge. Both the failed-break faders and the original breakout's stopped-out longs re-enter or cover.
- **Rules:** as final-synthesis Spec 4: after a PS-03-qualifying failure, a second accepted break of the same edge within 2 sessions; enter on the first HL; stop below the failure low; target 1.0 W.
- **Sweep:** window ∈ {same session, 2 sessions} → 2.
- **Frequency (est.):** 1–2 per month.
- **Needs:** as PS-01 + PS-03.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** deferred to the trapped-trader round; listed for completeness.

#### SP-08 · Point-of-Origin retest (Levy)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · Levy (PoO = the open of the bar that closes outside the range).
- **Evidence:** **E** · untested · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC; 15m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** The PoO is the last price before initiative activity started. A retest that holds means the initiators still defend their starting price.
- **Rules:** *Level:* the open of the first 15m bar closing outside a ≥ 8-bar range. *Entry:* the first retest to PoO ± δ with a 5m HL. *Stop:* PoO − 0.5 × ATR15, floored. *Target:* the breakout bar's high + 1 × range height.
- **Sweep:** range length ∈ {8, 16} bars → 2.
- **Frequency (est.):** 3–6 per month.
- **Needs:** ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (bar midpoint or close as level instead of open). **Kill:** PoO ≤ alternative levels.

#### SP-09 · Clock-window equal-liquidity sweep into an HTF fair-value gap (ICT "macro" scalp)
- **Implementation status (v2.7):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SP · YouTube, Dylan Richards (Adora Trading), *How to Find High Probability Scalps Every Day (No Daily Bias Needed)*, https://www.youtube.com/watch?v=83d144f2e0k. The discretionary extract is **YV-07**. Shorts are written out; longs mirror (high↔low, bearish FVG↔bullish FVG).
- **Evidence:** **E**. Presenter claims, unverified: about $2,500 in under two minutes across several accounts; about 10 ES points in about two minutes. **Prior on the naked sweep is (0):** Telonics, sweep fades 49.9% vs 50.0% (PS-18). This entry is a sweep fade with extra filters. Those filters are untested. Lit: YouTube. The word **macro** here is the presenter's clock window. It is not the M-EVENT Macro mask.
- **Markets · TF:** ES, with MES as the execution vehicle when the stop floor requires it. NQ/MNQ is the K-DR replicate, not a second simultaneous position (§2.1: ES and NQ are one correlated position — run them as separate experiments). Two arms:
  - **Arm M (primary, W-M):** context and the draw on 5-minute bars, trigger on 1-minute bars. This is the fractal the notes asked for so the idea is testable without ticks.
  - **Arm S (W-T only):** context stays 5-minute; trigger is **15-second** bars built from trade ticks (last price and size; bid/ask not required). 15-second bars do not exist on the 1-minute history.
- **Data · test window:** Arm M: 1m · **W-M** (agreed split §2.0.1). Verify the symbol's first full month (Rev 2.7 §E2). Arm S: trade ticks · **W-T** (≈ Jul 2024–Oct 2026, 250/125/125 sessions, **≤ 4 configs**). Arm S does not multiply Arm M's grid. No bid/ask and no order flow. ETH bars are needed only to **see** the London session extreme (03:00–08:30 ET); if the Globex template is unverified (Rev 2.7 §E5), drop session extremes and keep equal pivots only. RTH entries either way.
- **Why it might work:** Stops sit beyond equal highs. Running those stops into a 5-minute bearish gap, then failing, traps the breakout buyers. The new 1-minute (or 15-second) gap is the retest, and the equal lows are the next stop pool. The clock filter is the presenter's claim that this delivery clusters in the first and last ten minutes of the hour. PS-18 says the sweep alone is a coin flip, so the clock and the gap have to earn their place against the window-off control.
- **Rules:** Closed bars only. A pivot exists only from its `confirmTime` (M-SWING, fractal, strength bars after the extreme). An FVG exists only from bar 3's close (`_FvgDetector`, VP-5): bullish if `Low3 > High1`, zone `[High1, Low3]`, start = `High1`; bearish if `High3 < Low1`, zone `[High3, Low1]`, start = `Low1`. Usable from the next bar. `crossSessions = false`, `rthOnly = true`, `maxAgeSessions = 5`, `fillOn = Close`. A bearish gap is filled, and therefore dead, when a later bar **closes above** its `hi`.
  - **Clock filter ("macro"):** ET. A trigger bar is inside the window when its **close** time falls in `[H:50, H+1:10)`. RTH windows only: 09:50–10:10, 10:50–11:10, 11:50–12:10, 12:50–13:10, 13:50–14:10, 14:50–15:10. The 09:30–09:45 blackout (§2.1) does not cover 09:50. No new order after 15:30. The M-EVENT blackout (−2/+10 around tier-1 releases) still vetoes. One attempt per window per direction.
  - **Draw on liquidity (the target), 5m:** an equal-low pair from M-SWING. `isEqual` is true when a new confirmed 5m swing low is within `equalFactor × ATR` of an earlier swing low, looking back `lookbackPivots` same-side pivots. Primary: `equalFactor = 0.10`, `lookbackPivots = 5`, fractal **strength = 2** (§2.1: N = 2 on 1m and 5m). The tolerance is the engine's own: `equalFactor × ATR` with `atrLength = 20` (SMA of true range, the `_PriceActionEngine` default). Do not rebuild that test with ATR(14); `isEqual` would no longer match. Displacement, below, is a different number and uses ATR(14) on the trigger stream (§2.1). The target price is the **nearer** of the two lows (the higher one), minus 1 tick. A London extreme (high/low of 03:00–08:30 ET, closed 1m bars) or the prior RTH low counts as one side of the pair **only** when it is within the same tolerance of a confirmed 5m swing. It is never a target by itself. If no pair sits at least `0.5 R` beyond the entry after the stop floor, skip.
  - **HTF gap, 5m, must be respected:** a live bearish 5m FVG with `minTicks = 1` and `minAtrFrac = 0.15` (**catalogue freeze**: a 1-tick gap is not "the" 5m gap; 0.15 was not in the video). The sweep bar's range overlaps the zone (`high ≥ lo` and `low ≤ hi`). The gap's `createdTime` is strictly before the sweep bar. It has not been filled.
  - **Sweep and intermediate-term high, on the trigger TF:** during the window, a closed trigger bar trades at least 1 tick **above** a confirmed trigger-TF swing high that is `isEqual` to an earlier trigger-TF swing high (same `equalFactor`, strength 2, `lookbackPivots` as the 5m draw). The intermediate-term high (ITH) is the highest trigger-TF high from that sweep bar through the displacement bar, inclusive. The ITH is above the swept pivot by construction.
  - **Displacement:** `_FvgDetector.NewThisBar` on the trigger TF is a bearish FVG (`minTicks = 1`, `minAtrFrac = 0`; the size gate is the next sentence, not the detector). Bar 3's close is ≤ `sweptHigh − d × ATR(14)` of the trigger TF. Bar 3 closes within **3** trigger bars of the sweep bar. Primary `d = 0.50`.
  - **Entry:** sell **limit** at the new bearish FVG's **low edge** (`lo` = `High3`), the near edge a retrace from below hits first. This is **not** `start` (DV-08's "beginning", which for a bearish gap is the upper edge). The limit is working until the window ends plus **2** trigger bars, then cancelled. One position. Stop-first if a bar touches stop and target (§2.1).
  - **Stop:** `ITH + 1 tick`, then `max(that, cost floor)`: ES 14 ticks, MES 20, NQ 19, MNQ 35 (§2.1). If the widened stop risks more than 1% of equity, skip and log. Do not shrink size to fit.
  - **Target:** the draw price from the equal-low rule. Bracket. No partial, no breakeven, no trail. Flat by 15:55.
  - **No daily bias.** No weekly filter. No order-flow filter. The presenter's "order flow" is the 5m gap being respected, and that is the FVG rule above.
- **Sweep:** Arm M, five cells, primary read first: (`equalFactor`, `lookbackPivots`, `d`) ∈ {**(0.10, 5, 0.50)**, (0.25, 5, 0.50), (0.10, 8, 0.50), (0.25, 8, 0.50), (0.10, 5, 0.25)}. Strength stays 2. `minAtrFrac = 0.15` on the 5m gap stays frozen. Arm S, two cells, not crossed with Arm M: strength ∈ {2, 1} at (0.10, 5, 0.50) on 15-second bars. Under the W-T cap of 4.
- **Frequency (est.):** Arm M, 0–2 per session, often zero. Author estimate only. The presenter's "every day" is a claim, not a measured rate.
- **Fails when:** the sweep-fade prior is right and the clock does not help; the 14-tick floor blows the reward on a 15-second ITH; equal highs at `equalFactor = 0.25` are just noise; the 5m gap is so wide that "tap the gap" is true most of the session.
- **Needs:** ✓ `_FvgDetector` (VP-5) on the 5m stream and on the trigger stream (`Extra` instance or a second detector; FVG is per stream) · ✓ M-SWING / `_PriceActionEngine` on those streams (`equalFactor`, `lookbackPivots`, `strength`, `isEqual`) · ✓ M-MTF for closed 5m bars (`HtfClosed`) · ✓ M-TOD or the session clock for the window · ✓ F-TM (flat 15:55, one position) · ◐ M-EVENT for the news blackout · Arm S: ✓ M-TICK as a **bar builder** only (counts/timing exist; size outputs are not used). 15-second aggregation from the trade tick is strategy code, not a new engine.
- **Readiness (v2.7):** Arm M is buildable on W-M now. Arm S is buildable only inside W-T, and only as a tick aggregation. Session-extreme targets need the ETH template; without it the equal-pivot target still runs.
- **Gate:** G0-a and G0-b. G0-d's current level list does not include equal-highs or FVGs, so this is not blocked on A2-33. Report the equal-low and the FVG edge into the level study anyway.
- **Test:** **Clock control (pre-registered):** the same Arm M rules with the window filter **off**, entries allowed 09:45–15:30, same one-attempt cap removed only in the sense that the window no longer defines the attempt — cap attempts at one per 20-minute block shifted to `H:20–H:40` as well, and read the on-window arm against the off-window arm on the same days. Second control: enter at the 5m bearish FVG's near edge (`lo`) with no trigger-TF sweep and no new FVG. K-FL: shift the equal-low target by ±0.5 × ATR5. K-RT inside the window. The PS-18 S7 canary (sweep fade with no shape filter, expected ≈ 0) is the base rate this must beat, not a host to merge into. **Kill:** Arm M mean net R ≤ 0 at ≥ 150 dev events, **or** the confidence interval of its lift over the off-window control includes 0, **or** it does not beat the 5m-FVG-only entry. Arm S is killed on its own 250-session block the same way, and is never pooled with Arm M to manufacture a sample. Between 30 and 149 events: park. Do not loosen `equalFactor` to raise the count.
- **Differs from:** **YV-07** (same video; "relatively equal", "displacement" and "intermediate-term high" left verbal). **PS-18** (P/b-profile, 1–4 tick sweep of a 5m swing, buy the close back above; no FVG, no clock window; the Telonics null is the caution, not a template). **DV-08** (30m FVG aligned with a heavy-volume zone, used as a **level filter** on other Dale entries; the entry here is the new lower-TF gap's near edge, and the 5m gap is the context). **DV-20** (equal lows as a veto, tolerance 0.1 × ATR30 on 30m swings). **YV-08** (1h swing-failure close, then a 5m FVG; no clock window). **TS-02** (London **range break** on CL/GC, not an ES liquidity draw). **SP-01** (failed double top as a breakout, not a fade).
- **Sources:**
  - https://www.youtube.com/watch?v=83d144f2e0k
  - YV-07 (§3.19); `_FvgDetector.Function.CS` (VP-5); M-SWING `equalFactor` / `isEqual` (`_PriceActionEngine.Function.CS`)
  - Telonics sweep-fade null, as cited on PS-18 (49.9% vs 50.0%)

### 3.7 Family TS: Time-of-day and session effects (7)

#### TS-01 · European-open overnight drift (long MES 01:30–04:30 ET)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · R6 (Bondarenko & Muravyev; NY Fed overnight-drift research); final-synthesis parallel module.
- **Evidence:** **B (+).** Bondarenko & Muravyev (JFQA 2023): about 4 hours around the European open carry essentially the whole market return, Sharpe 1.6 including costs. NY Fed SR 917: concentrated in 02:00–03:00 ET, ~3.7% p.a. No independent futures holdout was found (R6) · Lit: Indep (peer-reviewed).
- **Markets · TF:** MES/MNQ; 30m/60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5) (signal on ES prices from 2016; MES costs; MES itself only from May 2019)
- **Why it might work:** Dealers and market makers who absorbed customer selling at the US close unwind inventory risk when European liquidity opens. That produces a small, persistent positive drift.
- **Rules:** buy at 01:30 ET, sell at 04:30 ET, every trading day; skip CME holidays and the nights before tier-1 08:30 releases (variant). No stop (time-boxed), but a disaster stop at 2 × ATR60m.
- **Sweep:** window ∈ {01:30–04:30, 02:00–03:00}; event filter on/off → 4.
- **Frequency:** ~20 per month.
- **Fails when:** crisis regimes (overnight gaps); decay post-publication.
- **Needs:** F-CAL, F-TM (time windows); ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29) for the event filter; a 24h data series.
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. Caveat: needs ETH bars. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:** the control is the same-length window at other hours (e.g. 19:00–22:00). **Kill:** net ≤ 0 after $2.50 RT cost per MES (v2.6a; was $2.47), or ≤ the control window.

#### TS-02 · London-open break of the Asia range (CL/GC) with an Asia AVWAP filter
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · author idea; practitioner London-breakout lore.
- **Evidence:** **H** · Telonics: Asia/London sweep behaviour is null · Lit: Author.
- **Markets · TF:** GC, CL (also 6E if added); 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); CL/GC need NYMEX/COMEX bundles (verify E1)
- **Why it might work:** European participants reprice Asia-session inventory. If the break holds above the AVWAP from the Asia open, the Asia cohort is offside.
- **Rules:** *Range:* 19:00–02:00 ET high/low. *Signal:* 03:00–05:00 ET, 2 closes beyond the range + δ, with price on the same side of the Asia-open AVWAP. *Entry:* next bar. *Stop:* range midpoint, floored. *Target:* 1 × range height; flat by 08:00.
- **Sweep:** AVWAP filter on/off; target ∈ {1×, 0.5×} → 4.
- **Frequency (est.):** 6–12 per month per market.
- **Needs:** ✓ F-VWAP Session slot (24h Globex template; 19:00 ET = session open), F-SESS (custom session).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT within the window. **Kill:** ≤ K-RT.

#### TS-03 · Close regime: 15:30–16:00 MOC drift then reversion
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · Keller B3 §4.3; R6.
- **Evidence:** **C (+?)** · Lit: Book + Report.
- **Markets · TF:** ES/NQ; 1m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5) (post-16:00 bars)
- **Why it might work:** MOC imbalance publications (≈15:50) force liquidity providers to pre-position. The imbalance-driven drift often reverses after the close prints.
- **Rules:** *Variant A:* at 15:50, trade in the direction of 15:30–15:50 drift, exit at 16:00. *Variant B (reversion):* enter against the drift at 16:00 (ETH), exit at 16:45. Stop 1 × ATR5, floored.
- **Sweep:** A vs B → 2.
- **Frequency:** ~20 per month.
- **Needs:** F-SESS (ETH after 16:00), F-TM; MOC imbalance data not available → proxy only.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT (random 10-min windows 13:00–15:00). **Kill:** ≤ K-RT.

#### TS-04 · CL/GC pit-session profile (port of the PS ideas)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · R1 (pit sessions: CL 09:00–14:30, GC 08:20–13:30; EIA Wed 10:30 blackout).
- **Evidence:** **H** (port) · Lit: Report.
- **Markets · TF:** CL, GC; 5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); NYMEX/COMEX bundles (E1)
- **Why it might work:** Value built during the liquid pit session reflects the commercial participants; the overnight trade in these markets is thinner. PS-01/PS-24 levels from pit profiles should be cleaner, and they carry no equity drift (a natural K-DR replication).
- **Rules:** re-run PS-01 and PS-24 with pit-session profiles; EIA blackout −5/+15 for CL.
- **Sweep:** session ∈ {pit, full RTH} → 2 per host.
- **Needs:** F-SESS (custom session templates), F-CAL.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** pit vs full-session profiles. **Kill:** no difference → use the simpler one.

#### TS-05 · ZN post-release AVWAP acceptance (08:30 data)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS/AV · author idea; Krueger macro-release study (reactions at chance).
- **Evidence:** **C (0)** for generic release reactions · **H** for AVWAP acceptance · Lit: Indep + Author.
- **Markets · TF:** ZN (also ES); 1m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5); CBOT bundle (E1)
- **Why it might work:** Rates react to data first and are the cleanest repricing instrument. If ZN accepts on one side of its 08:30 release-bar AVWAP by 09:15, the repricing is ratified.
- **Rules:** as AV-06 with ZN parameters, entry window 08:40–09:30, flat by 11:00.
- **Sweep:** closes ∈ {2, 3} → 2.
- **Frequency (est.):** 4–8 per month.
- **Needs:** ✓ M-AVWAP, ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29; `NewsBar` anchor inert).
- **Readiness (v2.3b):** Buildable with caveats — release times from ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29); `EAvwapAnchor.NewsBar` is inert and `_AvwapManager` has no public custom-anchor call, so the strategy must compute its own post-release AVWAP (or `NewsBar` must be wired to M-EVENT first). Caveat: needs ETH bars. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:** K-RT on release days. **Kill:** ≤ K-RT.

#### TS-06 · Overnight inventory correction at the open
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · Dalton (overnight inventory); Keller B3.
- **Evidence:** **E/H** · untested · Lit: Book.
- **Markets · TF:** ES/NQ; 1m/5m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5)
- **Why it might work:** When the overnight session is heavily one-sided (inventory long) but the RTH open is still inside prior value, the overnight longs are early. Their partial liquidation in the first 30 minutes pulls price back toward the ETH VWAP.
- **Rules:** *Filter:* (09:30 price − ETH VWAP) ≥ 0.3 × ATR20d and the open is inside the prior VA. *Entry:* 09:35–09:45, the first 1m LH. *Stop:* the ON high or 0.5 × ATR5 above the entry, floored (×2 slip at the open). *Target:* ETH VWAP; time stop 10:15.
- **Sweep:** inventory threshold ∈ {0.3, 0.5} → 2.
- **Frequency (est.):** 4–8 per month.
- **Needs:** F-VWAP (slot = ETH session), F-SESS.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT at the same time on qualifying days. **Kill:** ≤ K-RT.

#### TS-07 · Opening-noise observation window (filter test)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS · Keller B2 15.7 ("do nothing 0–5 min"); final-synthesis (no entries 09:30–09:45).
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Evidence:** **D** · Lit: Book.
- **Why include it:** The 15-minute open blackout is applied everywhere. It should be tested once rather than assumed.
- **Rules:** the blackout ∈ {5, 15, 30} min as a layer on PS-07, PS-24, MT-01.
- **Needs:** F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** layer ablation. **Kill:** n/a; choose the value that does not hurt and keep it fixed across all ideas (pre-registered).

### 3.8 Family SW: Swing / daily / weekly profile (6)

#### SW-01 · Weekly balance break (3–6-week composite)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW · Keller B4 §8.4 transposed to weekly; Dalton.
- **Evidence:** **C (+)** family by analogy (K4 balance-break travel) · untested on weekly CME · Lit: Book.
- **Markets · TF:** ES/NQ, CL, GC, ZN; weekly composites, daily signal, 60m entry.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999); 3–6-week composites must sit inside one contract (on ES only weeks ≈6–13 of a quarter qualify), so pool markets
- **Why it might work:** Multi-week balance means longer-term participants agree on value. An accepted break forces the range's responsive traders to rethink, and these flows play out over days.
- **Rules:** *Filter:* the last 3–6 weekly VAs overlap ≥ 60%. *Acceptance:* 2 daily closes beyond the composite edge + 0.1 × ATR20d. *Entry:* the next day's first 60m HL (or the day-3 open). *Stop:* back inside by 0.25 W, floored. *Target:* 1.0 W; time stop 15 sessions. *Control twin:* the weekly 80% rotation (expected null).
- **Sweep:** weeks ∈ {3, 6}; acceptance ∈ {1, 2} days → 4.
- **Frequency (est.):** 0.5–1.5 per month per market (≈ 30–80 events across 5 markets over the W-M dev window, fewer after roll resets: a thin sample, so pool markets).
- **Needs:** ✓ weekly profile stream + N-week composites (`_WeeklyProfiles`, PR #18), ✓ M-DAILY.
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Test:** pool across markets; K-FL; K-DR. **Kill:** pooled ≤ 0 R.

#### SW-02 · RSI(2) dip-buy with a profile/AVWAP location filter
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW · Connors RSI(2) (R6 B−: +48 bp/trade in a holdout); author's filter extension.
- **Evidence:** **B− (+)** base · **H** for the filter · Lit: Indep + Author.
- **Markets · TF:** MES/MNQ (also GC); daily.
- **Data · test window:** base rule: daily · W-D (decades; ES from 1997, NQ from 1999); profile/AVWAP filter arms: 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** Short-term oversold dips in an uptrending index are liquidity-driven and mean-revert. A location filter (dip into the 20-day composite VAL or the swing-low AVWAP) might separate liquidity dips from repricing.
- **Rules:** *Base:* close > 200-day SMA, RSI(2) < 10 → buy at the close; exit when close > 5-day SMA, or after 5 days. *Filter variant:* additionally, the close ≤ the 20-day composite VAH (not stretched) or within 0.5 × ATR20d of the swing-low AVWAP. *Stop:* none in the base (time exit); a 3 × ATR20d disaster stop.
- **Sweep:** RSI threshold ∈ {5, 10}; filter ∈ {none, composite, AVWAP} → 6.
- **Frequency (est.):** 1–3 per month.
- **Fails when:** crash regimes (2020-03, 2022); a long-only drift dependency.
- **Needs:** ✓ M-DAILY, F-COMP; ✓ M-AVWAP for the AVWAP filter.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** the base RSI(2) is the control for the filter. K-DR: also test GC/ZN (no equity drift). **Kill:** the filter adds < 0.05 R or reduces trades > 60% without improving the Sharpe.

#### SW-03 · Quarterly/monthly AVWAP swing pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW/AV · Shannon (YTD/QTD AVWAPs).
- **Evidence:** **E** · untested · Lit: Practitioner.
- **Markets · TF:** ES/NQ, GC, CL, ZN; daily anchor, 60m entry.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + daily · W-D (decades; ES from 1997, NQ from 1999); the ES roll falls ≈1 week before quarter-end, so QTD anchors and splices nearly coincide
- **Why it might work:** The quarter-to-date cohort's cost is a reference that portfolio managers track. Pullbacks to it within an up-quarter attract benchmark-aware buyers.
- **Rules:** *Filter:* price > QTD AVWAP for ≥ 10 sessions. *Entry:* the first daily low within 0.25σ of the QTD AVWAP, then a 60m HL. *Stop:* QTD AVWAP − 1σ, floored. *Target:* the quarter high; time stop 10 sessions.
- **Sweep:** anchor ∈ {MTD, QTD} → 2.
- **Frequency (est.):** 0.5–1 per month per market.
- **Needs:** ✓ M-AVWAP, ✓ M-DAILY.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL-anchor (an anchor at a random date within the prior quarter). **Kill:** ≤ fake.

#### SW-04 · Multi-day P-profile hold (Levy "will it move within 3 days?")
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW · Levy (swing checklist), Keller B1.
- **Evidence:** **E** · Lit: Book.
- **Markets · TF:** ES/NQ, GC; a 3-day composite, daily.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** A 3-day composite with a P shape (value built high, thin below) that closes in the upper third is a continuation setup. Levy's 3-day rule bounds the holding time.
- **Rules:** *Filter:* the 3-day composite shape = P and the close is in the upper third. *Entry:* the next day's open if it is above the 3-day POC. *Stop:* below the 3-day POC − 0.25 ATR20d, floored. *Exit:* 3 sessions, or close < POC.
- **Sweep:** hold ∈ {2, 3} days → 2.
- **Frequency (est.):** 1–2 per month.
- **Needs:** F-COMP, F-PROF (shape on composites).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT (random 3-day long holds); K-DR. **Kill:** ≤ K-RT (drift).

#### SW-05 · Composite gap-and-go (out of a 20-day composite)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW · author idea (K3 gap tree in ATR units).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC; daily + 60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Why it might work:** An overnight gap that opens outside the 20-day composite VA and is accepted in the first hour is a multi-week repricing. The composite's responsive traders are offside for days.
- **Rules:** *Filter:* the RTH open is outside the 20-day composite VA by ≥ 0.25 × ATR20d; the first hour closes outside. *Entry:* the 10:30 bar close. *Stop:* the composite edge, floored. *Exit:* close of day 2 (variant: trail EX-04 on the gap AVWAP).
- **Sweep:** hold ∈ {1, 2} days; exit ∈ {time, trail} → 4.
- **Frequency (est.):** 1–2 per month.
- **Needs:** F-COMP, F-SESS; ✓ M-AVWAP for the trail.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL (10-day composite instead), K-RT. **Kill:** ≤ K-RT.

#### SW-06 · Weekly IB (Mon–Tue range) breakout with acceptance
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW · author idea (Dalton IB logic transposed to the week).
- **Evidence:** **H** · Lit: Author.
- **Markets · TF:** ES/NQ, CL, GC, ZN; daily, 60m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** Monday–Tuesday establishes the week's initial balance. Accepted extension mid-week means weekly other-timeframe participants have committed.
- **Rules:** *Range:* Mon–Tue RTH high/low. *Signal:* Wed/Thu, 2 × 60m closes beyond + δ60. *Entry:* the next 60m HL. *Stop:* the weekly IB midpoint, floored. *Exit:* Friday 15:55, or 1 × weekly-IB range.
- **Sweep:** narrow-IB filter (≤ 50th pct) on/off → 2.
- **Frequency (est.):** 2–3 per month.
- **Needs:** F-SESS (multi-day stats), ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT (random Wed/Thu entries same direction). **Kill:** ≤ K-RT.

### 3.9 Family RG: Regime filters (10, as modules)

Regime filters never trade. Each is a **layer** tested on host events through the ablation ladder, and each costs ≤ 2 thresholds × ≤ 3 values against the trial budget.

#### RG-01 · Value-migration gate (V-score)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Keller B4 §8.4 ("trade with value migration") · **C (+)** · Lit: Book.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls (3-session V-score warm-up)
- **Rule:** longs only if V ≥ +1, shorts only if V ≤ −1; responsive (fade) setups only if |V| ≤ 1.
- **Why:** continuation works when value is already moving; fades work only when it is not.
- **Hosts · Needs:** all PS/AV/MT · F-PROF (`ValueMigrationScore`, done).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** ablation; kill if it removes winners as fast as losers (lift < 0.05 R).

#### RG-02 · Balance/imbalance classifier + entropy skip
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Keller B4 (skip profiles with entropy > 0.93); `IsBalanced` · **C** · Lit: Book.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); multi-day levels reset at rolls
- **Rule:** state ∈ {Balance (≥ 3 overlapping VAs), Imbalance}; skip all setups when composite profile entropy > 0.93 (a flat, uninformative profile).
- **Hosts · Needs:** all · F-COMP; ✓ entropy (`_ProfileStats.Entropy`, PR #18).
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Test · Kill:** ablation; threshold ∈ {0.90, 0.93, 0.96}.

#### RG-03 · Volatility regime stratifier
- **Implementation status (v2.4):** **Implemented** — ATR-tercile regime (`_RegimeEngine` fallback, PR #23; `AtrTercileTracker` and arm A of `_Test6`, PR #26, [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)). Not run in MC.
- **Source · Evidence:** R2 (ORB negative in the low-vol 2015–17 regime); R6 · **B** (as a stratifier) · Lit: Indep.
- **Data · test window:** daily · W-D (decades; ES from 1997, NQ from 1999) (ATR on a roll-gap-removed series)
- **Rule:** ATR20d percentile over 250 days → tercile {low, mid, high}. Report every idea per tercile. Optional gate: continuation ideas off in the low tercile.
- **Hosts · Needs:** all · ✓ M-DAILY.
- **Readiness (as of merge):** Buildable now on the merged base.
- **G-R consumer (v2.3b, A2 review):** `_RegimeGate` (A2-63 G-R consumer C1; arm A = skip ATR tercile 2, i.e. this RG-03 stratifier) is coded in PR #26 (cd55796 etc.: `_RegimeGate.Function.CS`, `tests/RegimeGateTests`; pre-registration `specs/regime-consumer-prereg.md`); its pre-registered test is **not yet run**. No §2.4 module row.
- **Test · Kill:** pre-register the gate **only** if the dev-period tercile split is monotone; otherwise report only.

#### RG-04 · Live day-type proxy at 10:30 (includes VWAP-cross count)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Keller B1 Part IV ("classify TREND/BALANCE 30–60 min in"); Perfiltrade (mid-session class matches the final day type only ~half the time) · **D/B (0)** · Lit: Book + Indep.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Rule:** the §2.2 trend/balance proxy; route continuation setups to trend days and responsive setups to balance days.
- **Hosts · Needs:** VW-01, VW-02, PS-10, PS-11 · F-VWAP, F-DEV, F-SESS.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** measure classification accuracy vs end-of-day type (only for diagnosis; the trade test uses live labels). Kill if host lift < 0.05 R.
- **Merged · A2-16 (thresholds adopted):** at 10:30, slope = (sVWAP(10:30) − sVWAP(09:45))/ATR30 and the % of 1m closes on one side since 09:45. Slope > +0.3 and ≥ 85% of closes above → trend-up state: disable all fades, Spec 2 shorts and VW-02 for the session. Precision is checked against a locked Dalton trend-day definition (close in the top 10% of the range; range > 1.3× the 20-day average). Kill if precision < 55% or there is no PnL lift. *(Grades: A2-16 original **C** → catalogue **inherits host D / B (0)**.)*

#### RG-05 · Noise-band filter (Zarattini)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Zarattini et al. noise-area ORB · **C (decayed)** · Lit: Indep.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (14-session time-of-day noise)
- **Rule:** no breakout entries while price is inside open ± (average absolute move from the open at this time of day over 14 sessions).
- **Hosts · Needs:** PS-08, PS-24, OF-07 · ✓ M-TOD.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** ablation.

#### RG-06 · HTF trend state (TSMOM + 60m structure)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** R6 TSMOM **A**; R4 alignment score · Lit: Indep.
- **Data · test window:** daily · W-D (decades; ES from 1997, NQ from 1999) + 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Rule:** daily TSMOM sign (20/60-day return) + 60m HH/HL state; continuation setups only in the agreed direction.
- **Hosts · Needs:** all · ✓ M-DAILY, ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** **drift confound**: on ES/NQ, "long only when TSMOM > 0" partly re-earns the index drift, so require the effect on CL/GC/ZN too.

#### RG-07 · Event, roll and thin-session exclusions
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** practitioner; final-synthesis §4.0 · **D** · Lit: Report.
- **Data · test window:** calendar/roll files over W-M
- **Rule:** tier-1 news −2/+10 min; FOMC day after 14:00 → off; roll week (F-ROLL) → swing ideas skip new entries; CME half-days and holiday-adjacent sessions → off.
- **Hosts · Needs:** all · F-CAL, F-ROLL, ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29).
- **Readiness (v2.3b):** Buildable now — tier-1 blackout and FOMC-day rules via ◐ M-EVENT (`EventBlackout`, `EventFlattenNow`; M-EVENT PRs #25/#27/#28/#29); unscheduled news not covered. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test · Kill:** report event-day results separately (stratify); do not tune.

#### RG-08 · Compression state (range/ATR)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Keller B2 15.5 (full book vs empty book: K2 grade C); NR7 negative (R2) · **C/B (0/−)** · Lit: Book + Indep.
- **Data · test window:** daily · W-D (decades; ES from 1997, NQ from 1999); volume-per-range proxy 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Rule:** prior-day range / ATR20d ≤ 20th pct → "compressed". Without book depth, the full-book/empty-book split is **unavailable**; the bar proxy is volume per range unit (high = full book).
- **Hosts · Needs:** PS-02, AV-03, SW-01 · ✓ M-DAILY.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** ablation; kill if ≤ unconditional.
- **Merged · A2-55 (volatility-contraction gate (gates G1–G4) for intraday breakout hosts).** Original A2 grade: B− (CL), C (ES: few trades); the catalogue grades NR7 negative. → catalogue grade **C (CL, gross) · E (ES, few trades); NR7 stays negative**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "Volatility-Contraction Gate for Intraday Breakout Hosts (Crabel / Kaufman module)"):
  - **Source:** Kaufman Ch 16:
    - CL 5m ORB 2009–2011: with overnight holds, **inside days lifted PF from about 1.1 to about 2.0–2.5 and $/trade about 10×** on about 10% of days.
    - On the S&P, inside days turned negative ORB results positive at most sizes (few trades).
    - An HV6 < HV100 filter halves the trades. Ch 20: low-volatility entries improve all markets at thresholds ≤ 0.35σ.
    - Ch 20 (Brower): inside days had no significance for next-day S&P range. That conflicts with the above.
    - **Grade:** B− (CL), C (ES: few trades); the catalogue grades NR7 negative.
  - **Differs from:** NR7 in the catalogue (negative), which is itself a breakout entry. A2-55 is a **gate module** applied to existing breakout hosts, with three contraction definitions tested side by side. It answers the catalogue's NR7 result: is the problem contraction, or the NR7 definition?
  - **Gate definitions (computed at the prior close):**
    - **G1** prior RTH day is an inside day (H ≤ H−1, L ≥ L−1).
    - **G2** NR4 (the prior day's range < each of the 3 before it).
    - **G3** HV(6 d) < HV(100 d).
    - **G4** prior-day range / ATR(20 d) ≤ 0.6.
  - **Hosts:** A2-52, A2-56, A2-49 BO arm, A1 Spec 1/ORB slot, A2-41.
  - **Hold variant (book):** for gated trades only, allow an overnight hold to 09:31 next day if the trade is in profit at 15:55.
  - **Parameters:** gate ∈ {G1..G4}; overnight hold ∈ {off, on} → 8 per host.
  - **Test window:** M (gates on daily bars from the same contract, roll-safe per A2-39).
  - **Tests:** (1) Host $/trade and PF gated vs ungated, per gate. (2) The trade-count fraction (expect about 10% for G1, about 50% for G3). (3) Next-day range prediction by gate (a Brower-style descriptive check).
  - **Kill:** no gate lifts any host's net expectancy by ≥ 0.05R with ≥ 100 gated trades; **or** the gated improvement appears on ES only and not NQ.

#### RG-09 (= KD-03; rev 3.1 module form = KD-09) · Three-family detrended confluence filter (module, not an entry)
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Source · Evidence:** Keller B5 Ch 1.4 (loc 430–473), 2.4 (loc 665–710), 7.2 (loc 5180–5201), 8.3 rule 3 (census) and rule 7 (hypothesis) · **C (+) one-bar hold only, inadmissible as evidence; admissible test = A2-33 matched controls.** One-bar hold census: 3 families 86.5% vs 1 family 79.1% FX; 85.4% vs 79.1% crypto; 2 families add nothing (78.8% / 78.7%) (loc 665–710). **E (0/−) for trade geometry:** 3-family exhibits MFE/MAE 0.58–0.80 (loc 5180); book: "do not buy confluence as an entry filter". [GAP: 7.3 #36/#37 and 7.4 #38 tables unread.] · **FX/crypto only · no true delta · ES prior 0**.
- **Data · test window:** 1m-built bars · W-M (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: a fit window may not cross a roll (`crossesRoll` skip; skip share journaled per stream, > 30% → diagnostic-only); ETH primary, RTH-only sensitivity arm; RTH share of fit-window volume journaled, results split {<50%, ≥50%} report-only.
- **Hypothesis:** confluence does not choose direction; conditional on **acceptance** (PS-25) a three-family rail that is broken and accepted is a more significant re-rating (#37, selected exhibits).
- **Rule (KD-03, frozen rev 3):** `families(rail_t) == 3` within 0.3 ATR (distinct families; lattice counted by distinct ranges, ≥ 1 range); manifold + sigma alone never counts as 2 independent witnesses. Arms: **A** (PS-25 only when the event rail has 3 families), **B** (only when ≤ 1 family), **C** (veto when the lattice names a price within 0.3–1.0 ATR but not within 0.3 — rule 7, C hypothesis). Sweep (≤ 6): confTol ∈ {0.2, 0.3, 0.5} ATR × arm ∈ {A, C}. **Superseded (Analyst 2, 23 Oct 2026): KD-09 replaces arms A and C, with confTol fixed at 0.3 ATR (the confTol sweep is not run); arm B (≤ 1 family/range) stays as KD-09's contrast arm. The KD batch runs only KD-09; this frozen rev 3 rule is kept for traceability and is not run.**
- **Rule (KD-09, rev 3.1 module form, the form run in the KD batch as `GateModule = "KD-09"`):** evaluated on the host's event rail at t−1, **only after acceptance**; tolerance **fixed at 0.3 ATR** (replaces the confTol sweep for KD-09); the lattice counts as a family only if **≥ 2 distinct anchored ranges** name a price within 0.3 ATR (**in this module only**; the frozen engine family rule stays ≥ 1 range and `nRanges` is journaled); pass if fam ≥ 3; **disagreement veto** if the lattice (≥ 2 ranges) names a price within **0.3–1.0 ATR [GAP, pending the 7.1–7.4 re-read]** of a manifold rail but not within 0.3 ATR; arm B (≤ 1 family) contrast kept; the 1.00/0.77 VA/POC weighted score is report-only (1.00/0.80 never used). Details in §3.16 KD-09.
- **Hosts · Needs:** PS-25 (KD-01 family) · ◐ M-DVP (engine on main; d* not frozen — PR-D9) incl. Fib lattice and confluence scorer; ✓ M-SWING pivots.
- **Readiness (v2.4):** Engine on main (PR-D0…D8); consumer rule not coded; may start only after PR-D9 freezes d* (Fib lattice #37 and `FamiliesAt` confluence #39/#40 are on main).
- **Gate (v2):** G0-d level type `DvpConf` scored by A2-33; G0-a; inherits PS-25's gates. Not a Holm member; the filter never modifies PS-25's primary unless pre-registered before the read (hidden-trial list item 4).
- **Test · Kill:** (1) replicate the census on ES: one-bar hold by family count with a matched-control rail (A2-33 style); (2) the book's falsifier: median 40-bar MAE of held 3-family vs held 1-family interactions; (3) accepted only if it beats unfiltered PS-25 on net expectancy **and** a random-subsample filter with the same pass rate. **Kill:** no lift vs the random-subsample control; for KD-09 also: population #38 40-bar MAE not larger than all three-family MAE (rule-7 falsifier), or A2-33 `DvpConf` |t| < 2.

#### RG-10 (= KD-04) · Detrended-fit regime gate: degree-1 slope with band-break stand-down (module)
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Source · Evidence:** Keller B5 2.3 (loc 610–651), 6.4 #28 (loc 4391–4414), 8.3 rule 2 and daily-process step 6 ("refit when the band breaks and keeps going") · **C (+) FX/crypto** that lower degree projects better: forward ±2σ 47.8/46.0% (degree 1) vs 26.4/26.3% (degree 3) (loc 610–651), **confounded by BIC selection** (separating experiment not done); **H** as a trade gate. · **FX/crypto only · no true delta · ES prior 0**.
- **Data · test window:** 1m-built bars · W-M (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: a fit window may not cross a roll (`crossesRoll` skip; skip share journaled per stream, > 30% → diagnostic-only); ETH primary, RTH-only sensitivity arm; RTH share of fit-window volume journaled, results split {<50%, ≥50%} report-only.
- **Rule:** long-only for PS-25 when the degree-1 slope over the frozen window > +0.05 σ/bar (short mirrored); **stand down** for `winLen/4` bars after |z| > 2 persists for ≥ 3 closed bars against the gate direction (band-break persistence `m` = 3 bars [GAP]), then use the fresh fit. Off for the KD-06 σ2 arm (pre-registered).
- **Sweep (≤ 4):** slope threshold ∈ {0, 0.05} σ/bar × stand-down ∈ {on, off}.
- **Hosts · Needs:** PS-25, host H1 (PS-01) · ◐ M-DVP (engine on main; d* not frozen — PR-D9) fit (manifold not required).
- **Readiness (v2.4):** Engine on main (PR-D0…D8); consumer rule not coded; may start only after PR-D9 freezes d*.
- **Gate (v2):** not a level (not G0-d); G0-a/G0-b apply. Not a Holm member.
- **Test · Kill:** ablation inside PS-25 and on host H1 (PS-01); accepted only if it helps both. Also pre-register the book's missing experiment on ES: refit every window at degree 1, 2, 3 and compare forward coverage *within the same windows*.

### 3.10 Family EX: Exit and management modules (13)

Exits are selected **inside** each slot through the `ExitModule` input. **Data window:** each exit inherits its host's window (W-M), except the flow-based parts of EX-11 and EX-12, which are W-T only. The **E(b) sweep (EX-08) runs on every surviving idea** and tells you whether the edge is fast-decaying (best at b ≤ 1, scale-outs help) or persistent (best with trails).

| ID | Module | Source · Evidence | Rule (codeable, no lookahead) | Needs | Test / kill |
|---|---|---|---|---|---|
| **EX-01** | Target *before* the next HVN | Dale VP/OF ("take profit just before heavy volume"); Keller B1 · **E/D**; Perfiltrade magnets = controls **(0)** | target = (next composite/developing HVN edge in the trade direction) − 2 ticks; if < 0.75 R away → skip trade (variant) or take 0.75 R | F-HVN, F-COMP | vs fixed-b target at equal median distance; kill if not better |
| **EX-02** | Naked-POC target | Keller B1 3.4 (nPOC = ranked target only) · **B (0)** (EV LABS: = distance-matched control) | target = nearest nPOC ≤ 20 sessions old in the trade direction, if 0.75–3 R away | F-NPOC | distance-matched price control; kill expected |
| **EX-03** | Balance-width projection | Keller B4 (median 1.17–1.20 W; ≥ 1 W in 56.6%) · **C (+)** | target = edge + 1.0 W; variant: 50% at 1.0 W, rest trails | F-COMP | vs 0.75 W / 1.5 W |
| **EX-04** (= **A2-03**) | AVWAP / VWAP trail | Shannon (handoff/support) · **B (+)** for the VWAP-trail role (Zarattini/Barbon/Aziz: Sharpe 0.61 → 1.24 on SPY; EdgeLab NQ long side positive OOS); **H** for breakout-anchored transfer | **A2-03 primary arm:** after T1, trail the runner at max(structural stop, AVWAP_break − 2 ticks), anchor = close of the acceptance-confirming break bar; exit on the first 5m close beyond. Handoff variant: a new anchor at each confirmed HL | ✓ M-AVWAP (session-VWAP arm works today) | Head-to-head on identical Spec-1 entries: (a) native exit, (b) AVWAP trail (pre-registered primary), (c) EMA20(5m), (d) chandelier 3×ATR5; kill if (b) does not beat (a) by ≥ 0.03 R with a lower 80% CI ≥ 0 · A2 grade **B** |
| **EX-05** | Structural HL trail | R5 · **D** | stop = last confirmed 5m HL − 1 tick (15m for swing) | ✓ M-SWING | vs fixed target |
| **EX-06** | Decay-based time stop | R5 (E(b) / edge decay arithmetic, **A** as maths) | exit after T bars if < +0.5 R; T = 2× the dev-period median time-to-target (fixed after development) | F-TM (TimeStopBars) | ablation; kill if it cuts winners |
| **EX-07** | Scale-out 50% at 1 R + runner | R2 §7.4 (maths: equal-expectancy lower-b paths have about half the drawdown) · **A (maths)** | 50% at 1 R, stop → BE, rest per EX-04/05 | F-TM, CS-4 sizing (≥ 2 micro contracts) | compare R-variance and max DD at equal mean R |
| **EX-08** | E(b) target ablation | R5; final-synthesis gate 5 · **A (method)** | b ∈ {0.5, 0.75, 1, 1.5, 2, trail}; report hit rate vs the 1/(1+b) yardstick | F-JRNL (MFE/MAE) | diagnostic; never picks b on holdout |
| **EX-09** | Stagnation exit | Levy ("exit stagnating trades") · **E** | if after N bars price is within ±0.25 R of entry and the last 3 bars overlap ≥ 70% → exit | F-TM | ablation |
| **EX-10** | Acceptance-failure invalidation | Keller B1 3.3 · **D** | exit at market if a 5m bar closes back inside the broken value by > δ (before the stop is hit) | F-COMP | compare R with/without |
| **EX-12** (= **A2-22**) | AVWAP-anchored CVD divergence exit | Valtos/Jigsaw practitioners via A2 · **E** | on a long runner: CVD anchored at the trail's AVWAP anchor; exit early if a new 5m swing high comes with an anchored-CVD lower high by > 1 z **and** the next 5m close is below the prior 5m low | F-OF (MC-BidAsk) · **W-T only** | paired vs base trail; K-SH placebo; kill if no lift or the placebo matches; **ES and NQ pooled** (A2); W-T 250/125/125 split · A2 grade **C** |
| **EX-13** | 1.5R partial, deep-pullback breakeven, initiative-print trail | Trader Oxygen video · **E** (one example) | The specification is the **EX-13** entry below this table. Host: OF-09 aligned trades. Half off at 1.5R; stop to true breakeven after a pullback to entry + 0.75R; trail under the next 1m buy-stack bar. Bubble trail gated on §E6 | ✓ F-TM, F-OF; ◐ M-TICK size (bubble arm) | vs the host's plain 1.5R bracket; K-SH on the trail; kill if mean net R falls or max DD rises at a mean R that does not |
| **EX-11** | Stop placement & flow-conditioned trail | Dale OF (stops beyond excess/LVN, never behind unfinished business; trail only while imbalances run in your direction); Levy (stops beyond the LVN) · **E** | stop = beyond the nearest excess or LVN edge (≥ floor); veto if an unfinished extreme is within 2 ticks behind the stop; tighten the trail when 3 consecutive bars show opposite-side imbalances | F-HVN, F-PROF, F-OF | vs plain structural stop; K-SH for the flow part |

- **Merged · A2-03** (row **EX-04** above): Analyst 2's breakout-AVWAP trail on Spec 1 is the pre-registered primary arm of EX-04, tested on identical Spec-1 (PS-01, host H1) entries. Original A2 grade **B** (for the VWAP-trail role) → catalogue **B (+)** trail role / **H** for the breakout-anchored transfer.
- **Merged · A2-22** (row **EX-12** above): Analyst 2's AVWAP-anchored CVD divergence exit; W-T only (250/125/125 split), ES and NQ pooled to reach a sample. Original A2 grade **C** → catalogue **E**.
- **Merged · DV-24** (into **EX-01**; Trader Dale exit module: TP before the barrier, SL behind it, daily-close stop + catastrophic stop; Dale priority 4). Original DV grade: E → catalogue **E**. Books: Agree (EX-01; P2). Dale's specification, verbatim:
  - **Sources:**
    - https://www.youtube.com/watch?v=MJKANontC4s ("take profit before a barrier at its start… most commonly heavy volume zone… exit at the first one"; "stop goes behind a barrier at its far edge… if there is a little swing point close to the end of the barrier… place my stop there"; "if the barrier fails then the trade is over")
    - https://www.youtube.com/watch?v=srEJqcTRDEI (swing trades: exit only on a **daily close** beyond the stop line; a hard "catastrophic" stop at 1.5 × the normal stop distance)
    - https://www.youtube.com/watch?v=unPNYtFzuKs, https://www.youtube.com/watch?v=nU_VuvDTKoY
  - **Rules:**
    - **[coding]**
      - TP = the near edge of the next HVN (range profile of the pullback leg, closed data) − 2 ticks.
      - SL = the far edge of the entry HVN + 1 tick, or the nearest confirmed swing beyond it if within 0.25 × ATR.
      - Swing variant: close-only stop on a daily close beyond the SL, plus a hard stop at 1.5 × the SL distance.
  - **Sweep:** applied to DV-02/03/04 vs the fixed bracket (2 × 3).
  - **Evidence:** E.
  - **Books:** **Agree** (EX-01 target before the HVN; P2 levels as destinations; the Dale book "PT before next cluster, SL behind cluster"). **Priority 4.**

#### EX-13 · 1.5R partial, deep-pullback breakeven, trail under the initiative print (module)
- **Implementation status (v2.7):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** EX (module) · same video, https://youtu.be/jmX_ReJxFm8, the management after the fill (≈ 15:19–17:00). Host for the test: **OF-09** aligned trades only. OF-09 counter-HTF trades stay on their own 1.5R bracket and do **not** take this module. The video's "swing low" in the caption at the target sentence is treated as a caption error: a long target below the swing low contradicts the rest of the clip, which places the partial near 1:1.5 and under the prior swing high.
- **Evidence:** **E**. One example (partial about 1:1.5, remainder stopped about 1:2.5). Lit: YouTube. No claim that a 1.5R partial raises expectancy. EX-07's only support for scaling out is the variance arithmetic, not this video.
- **Markets · TF:** the host's. Tested on OF-09, so W-T, ES/NQ, 1m management bars.
- **Data · test window:** inherits OF-09 (W-T, primary cell only). The partial and the breakeven do not need ticks. The **bubble** trail does, and it is gated. The substitute trail uses bar delta and stacked imbalances, which are already required by OF-09.
- **Why it might work:** If the edge dies after 1.5R (EX-08's fast-decay case), taking half there and letting the rest go only while initiative prints continue cuts the give-back. If the edge is the trend, the partial taxes it. The test is which one OF-09 is. The video does not answer that.
- **Rules:** Longs; shorts mirror. Applied after the OF-09 limit fill. `R` = the filled stop distance (after the cost floor).
  - **Partial:** if `contracts ≥ 2`, sell half (floor) at +1.5R, limit, good until 15:55. If `contracts = 1`, there is no partial: the whole position uses the 1.5R limit (log "partial skipped, size 1"). Micros exist so a 1% risk often clears 2 MES; when it does not, the size-1 path is correct, not a bug.
  - **Deep pullback → breakeven:** only after the partial has filled. If a later closed 1m bar prints `low ≤ entry + 0.75R` and `low > entry`, move the stop on the next bar to true breakeven: `entry + (round-trip cost in points + 1 tick)`. Cost is §2.1 (ES $17 → 0.34 pt; MES $2.50 → 2 ticks). Never loosen. The video's "retraced heavily, stop to breakeven" is this 0.75R give-back (half of the 1.5R that was taken), not a touch of the entry.
  - **Trail, buildable substitute:** after breakeven, each new closed 1m bar with bar delta > 0 **and** a buy-side stack (≥ 3, 3:1, P70) sets the stop to that bar's low − 1 tick if that is tighter. This stands in for "trail under the new green bubble" while size outputs are gated.
  - **Trail, gated bubble (off):** when `sizeOutputsVerified` becomes true, the same bar may instead set the stop to the lowest same-side P99.5 print on that bar, − 1 tick, if tighter. Do not run both trails. The bubble trail is a **replacement** arm, not in the four cells below.
  - Flat at 15:55 whatever the trail is doing. No add-on. The original stop remains until a tighter one replaces it.
- **Sweep:** on OF-09 primary, aligned trades, four cells: partial ∈ {on, off} × breakeven ∈ {on, off}. When breakeven is off, the substitute trail is off. When breakeven is on, the substitute trail is on. Primary cell: **partial on, breakeven on**. The bubble trail is a fifth config held back until §E6, and it replaces the substitute rather than joining this factorial. These four do not multiply OF-09's four.
- **Frequency (est.):** one management path per OF-09 aligned fill.
- **Fails when:** OF-09's winners run past 1.5R and the partial gives them away; the 0.75R breakeven is tagged and the trade then goes; the substitute trail ratchets up on ordinary positive-delta bars and is shaken out. All three are what the ablation measures.
- **Needs:** ✓ F-TM (scale-out and a working stop; EX-07 already assumes this) · ✓ F-OF for the substitute trail (already on for OF-09) · ◐ M-TICK size outputs gated, bubble arm only · ✓ F-JRNL so the partial and the runner are one trade's R, not two. Size ≥ 2 uses MES/MNQ (A2-71's micro point, not its vol target).
- **Readiness (v2.7):** Partial and breakeven are buildable on the host window now. Substitute trail is buildable on W-T with OF-09. Bubble trail is parked with §E6.
- **Test:** against OF-09's plain 1.5R bracket (this module off) on the **same** fills. Report mean R, R variance and max drawdown. **K-SH** on the trail's delta and stack (rebuild the trail on shuffled deltas; the partial and the breakeven stay, because they do not use flow). **Kill:** mean net R lower than the bracket, **or** max drawdown higher at a mean R that is not higher, on the first 250 sessions. A tie keeps the bracket (fewer states). The bubble arm, when it is eventually run, has to beat the substitute trail, not just the bracket.
- **Differs from:** **EX-07** (half off at **1R**, stop to breakeven immediately, runner on EX-04/05). **EX-11** (trail while imbalances continue; stop parked beyond an LVN; no 1.5R partial and no pullback-triggered breakeven). **A2-73** (breakeven around 3R or the rolling average gain, not after a 0.75R give-back). **EX-05** (last confirmed 5m higher low; this trail is the 1m initiative bar). **YV-01** (the verbal trail). **OF-09** (the host; counter-HTF trades never reach this module).
- **Sources:**
  - https://youtu.be/jmX_ReJxFm8 (≈ 15:19–17:03: buy limit, stop under the 1m swing, first partial about 1:1.5, sellers failed so the rest was held, stop to breakeven on the deep retrace, then trailed under the new buy bubble and the buy-side stack, stopped near 1:2.5)
  - EX-07, EX-08, EX-11, A2-73 in this catalogue

### 3.11 Family PF: Portfolio and diversification (8)

#### PF-01 · Trend + carry core (parallel track)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** R6: time-series momentum **A** (Moskowitz/Ooi/Pedersen; Hurst/Ooi/Pedersen century of evidence); carry Sharpe 1.20 (Koijen et al., B+/A) · Lit: Indep.
- **Data · test window:** daily · W-D (decades; ES from 1997, NQ from 1999); dxFeed continuous is unadjusted, so build back-adjusted series (roll-gap removal or individual contracts) before any return/ATR calculation
- **Rules:** 8–12 micro futures (MES, MNQ, MCL, MGC, M2K, MYM, 10Y/ZN, micro FX if available), multi-speed EWMAC trend + carry, 12% volatility target, weekly rebalance (R6 design).
- **Why for this goal:** It is the only grade-A return source here, it is crisis-convex, and it has low correlation to intraday mean-R streams. It anchors the smooth-equity target.
- **Needs:** outside the MC slots (pysystemtrade or a daily MC portfolio) · ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending).
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT is coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`); run Probe P5b live checks before reading any portfolio result.
- **Test:** an honest backtest with roll costs; expect a Sharpe of roughly 0.3–0.7 net for a small account (R6 range). Kill: none; it is the baseline sleeve.
- **See also (v2.7):** PF-08 is a Donchian daily-trend sleeve proposed as a companion. It does not change the EWMAC + carry rules above.

#### PF-02 · Regime-complementary intraday sleeves
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** author idea built on R2/R5 (regime complementarity) · **H**.
- **Data · test window:** inherits the sleeves' windows; correlation measured on the overlapping window
- **Rules:** run at most one *continuation* survivor (e.g. PS-01/PS-07) + one *responsive* survivor (e.g. PS-04 or VW-02) + one *time-based* survivor (TS-01). Allocate risk so that each sleeve's expected R-vol is equal.
- **Why:** continuation wins on trend days, responsive on balance days, and the overnight drift is independent of both. Their daily R streams should be weakly or negatively correlated.
- **Test:** measure the correlation of daily R in validation; accept only if pairwise ρ < 0.3.

#### PF-03 · Cross-market diversification & hidden-beta monitor
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Markowitz / Keller B2 (correlated positions = one) · **A (principle)**.
- **Data · test window:** inherits; SPX correlation on daily closes
- **Rules:** ES/NQ = one risk unit; add CL, GC, ZN versions of each survivor. Monitor the rolling 60-day correlation of each sleeve's daily P&L with SPX daily returns; flag a sleeve with ρ > 0.5 as "hidden beta" (it is then not adding beyond buy-and-hold).
- **Needs:** ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending).
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT is coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`); run Probe P5b live checks before reading any portfolio result.

#### PF-04 · Risk budgeting (deflated quarter-Kelly, capped)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** final-synthesis §6 gate 9; Keller B2 (Kelly drawdown table) · **A (maths)**.
- **Data · test window:** inherits
- **Rules:** per-trade risk = min(0.25 × Kelly on the *deflated* win rate, 0.5% of equity); portfolio open risk ≤ 1.5%; at most 2 cards live (Keller B1).
- **Needs:** CS-4 sizeForRisk (done), ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending).
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT is coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`); run Probe P5b live checks before reading any portfolio result.

#### PF-05 · Drawdown throttles + two-CUSUM monitoring
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** final-synthesis gate 9; Keller B1/B2 · **D (rules)**.
- **Data · test window:** live/paper only
- **Rules:** daily stop after 2 losses; −3 R in a week → halve size; −6 R → observation only; two CUSUMs (mechanism hit rate and P&L, k = 0.5, h = 5); bench any spec at 0 R after 40 live observations.
- **Needs:** F-TM (daily loss stop), ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending).
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT is coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`); run Probe P5b live checks before reading any portfolio result.

#### PF-06 · Portfolio volatility targeting
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** Moreira & Muir (vol-managed portfolios), R6 · **B** (the out-of-sample benefit is debated).
- **Data · test window:** inherits
- **Rules:** scale total risk so that the trailing 60-day portfolio volatility ≈ target (e.g. 10–12% annualised); cap leverage changes at ±25% per week.
- **Why:** it is the cheapest way to flatten the equity curve. It does not create edge.
- **Merged · A2-71 (ATR risk sizing and 12% volatility targeting (extends PF-06 and A2-40)).** Original A2 grade: C (method; book numbers are in-sample). → catalogue grade **C (method, in-sample book numbers); PF-06 host grade applies to the vol-targeting principle**. Window: W-M (agreed split §2.0.1); gate: A2-33 verdict for the level type (§2.6) where a profile level is used; A2-40/59 costs. Analyst 2's specification, verbatim (Rev 6, "ATR Risk Sizing and 12% Volatility Targeting for the A2 Book (infrastructure, extends A2-40)"):
  - **Source:** KCTS Ch 4/13/18/19/25: contracts = risk budget / (ATR20d × point value) (book: $25,000 / ATR$ per market); equal risk per market and per strategy; never risk > 1% of equity per trade. Vol targeting: factor = 12% target / current 20-day (or 10-day) portfolio vol; change size only when the factor moves > 20% from the last adjustment; book example AROR 10.5% → 27.6%, IR 1.27 → 1.40, gain mostly from levering up in calm periods. Ch 25 four-market example: in-sample vol factors gave a "too good" curve ("we cheated"); rolling 20-day vol gave similar returns, more honestly. Turtles deleverage: −20% size per 10% drawdown. **Grade:** C (method; book numbers are in-sample).
  - **Differs from:** A2-40 (cost model plus stressed-correlation book sizing: ES/NQ/RTY treated as one position at ρ = 0.9) and A2-59 (costs). Neither sets **per-trade contract count from ATR** or **targets portfolio vol**. A2-71 sits on top: A2-40's ρ = 0.9 rule still caps aggregate same-direction index exposure.
  - **Per-trade sizing:** contracts = floor(B × equity / (ATR20d × $/pt)), B ∈ {0.25, 0.5}% of equity per 1 ATR. Micro contracts (MES/MNQ/M2K/MYM) give the granularity; if floor = 0, skip the trade (not round up). Cap: 1% equity at the trade's own stop distance.
  - **Vol targeting (portfolio of surviving strategies):** daily strategy-level P&L → realised vol over the trailing 20 sessions (arm: 60) → factor = 12% / vol, clipped to [0.25, 2.0]; rebalance only when the new factor differs from the applied one by > 20%. Use **only past P&L** (rolling, never full-sample).
  - **Drawdown overlay:** −20% size per 10% drawdown from peak equity; restore +10% per 6⅔% recovery (Turtles).
  - **Margin check:** IBKR intraday margin per contract × contracts ≤ 25–40% of equity; skip excess.
  - **Test window:** the P&L streams of surviving ideas on M (and D for daily hosts); compare 1-lot vs ATR-sized vs ATR-sized + vol-target.
  - **Tests:** (1) In-sample-factor version vs rolling version (expect the in-sample one to look better; report it only as the "cheat" bound). (2) Turnover cost of rebalancing under A2-59. (3) Hysteresis 0% / 20% / 40%.
  - **Kill:** n/a (infrastructure). Adopt the vol-target overlay only if the rolling version raises net IR and does not raise max DD on validation; otherwise keep ATR per-trade sizing alone.
  - **See also (v2.7):** PF-08 is a Donchian entry this overlay could size later. The −20% size per 10% drawdown rule stays here and is not part of PF-08.

#### PF-07 · Meta-labelling the survivors (later)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Source · Evidence:** López de Prado (meta-labelling) · **B (method)**.
- **Data · test window:** host's window; order-flow features only inside W-T
- **Rules:** for a primary survivor with ≥ 300 dev trades, train a small classifier (≤ 5 features: V-score, vol tercile, day-type, time bucket, distance to nearest HVN) to size 0 / 0.5 / 1×. CPCV-evaluated; counts as configurations against the budget.
- **When:** only after a primary passes Gate 8.

#### PF-08 · Donchian / Turtle daily trend sleeve (companion to PF-01)
- **Implementation status (v2.7):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PF · YouTube, Dollar Wisdom, *With This Simple Breakout Strategy, He Turned $1,600 Into a $1.2 Billion Fortune* (owner's local file, 2026-11-08 notes; the 2026-11-01 pass matched the same title to https://youtu.be/lDn93erRQSM). The discretionary extract is **YV-09** and is not restated here. Optional arms, labelled below, are from Curtis Faith, *The Original Turtle Trading Rules* (public, https://www.tradingblox.com/originalturtles/originalturtlerules.htm), **not** from the video.
- **Evidence:** **E** for this sleeve. Presenter claims, unverified and **not** results of these rules: account from $1,600 to hundreds of millions; Turtles about $175M; a win rate under 40% is acceptable. The **family** prior for diversified time-series momentum is **A** (PF-01: Moskowitz, Ooi & Pedersen; Hurst, Ooi & Pedersen). That grade does **not** transfer to a 20/55-day Donchian on this book. Lit: YouTube + Indep (family only).
- **Markets · TF:** Daily. **Signal** on a difference-adjusted continuous of ES (primary), then NQ, and CL, GC, ZN where the dxFeed bundle exists (Rev 2.7 §E1, unverified). **Execution** on the micro (MES, MNQ, MCL, MGC) when the floor of the unit is at least 1; otherwise the full-size contract or skip. MES/MNQ exist only from May 2019, so they are execution vehicles, not the W-D history. No FX, no cash index, no crypto. The video's "currencies and stock indexes" are out of the data set. Start with ES alone; the book is the portfolio form.
- **Data · test window:** daily · **W-D** (agreed split §2.0.1: dev 2000–2015, validation 2016–2024, holdout Jan 2025–Oct 2026, once). ES exists from 1997, NQ from 1999, so dev starts at 2000 for those two. CL/GC/ZN start dates unverified. **Back-adjustment is required.** dxFeed `@ES` is unadjusted. A 55-day high taken across a raw roll gap is not a Donchian level. M-DAILY already difference-adjusts stored o/h/l/c into the new contract on each roll (`OnRoll` / `applyRoll`). Donchian highs, Donchian lows, EMA(20) and N are computed on that adjusted series. Orders are sent on the **current** contract (the adjusted history is already in current-contract prices after the last roll). Set `atrRestartOnRoll = false` so N carries across the splice (the splice day's true range is skipped). The day ring must hold ≥ 60 completed days (`smaLength` ≥ 60); there is no public `High(ago)`, so the strategy keeps its own buffer of those adjusted highs and lows. A week or a day that is itself the splice session takes no new entry.
- **Why it might work:** Futures trends have a fat right tail. A channel breakout holds the tail and cuts the rest with a channel exit. Diversifying across low-correlation roots is what makes a low win rate survivable. This is the same reason PF-01 exists. PF-08 is a simpler sleeve (one Donchian speed, no carry), not a replacement.
- **Rules:** All levels use **completed** daily bars only. The level for day t is known at the close of day t−1. "Preceding L days" does **not** include day t.
  - **Filter:** a root is eligible when N (below) is finite and the adjusted history has ≥ 55 completed days since the last rejected splice. One position per root. ES and NQ are **not** collapsed to one unit here (they are two roots); PF-03's ρ monitor still flags them if the book's daily P&L is hidden beta. No intraday clock.
  - **Signal / entry (stop, intraday on the daily bar):** buy stop at `DonchianHigh(L) + 1 tick`, sell stop at `DonchianLow(L) − 1 tick`, placed before the session. `DonchianHigh(L)` = max adjusted high of the preceding L completed days. Fill if the day's high (low) trades through the stop; if the open gaps through, fill at the open. Slip 1 tick (§2.1). Do not wait for the close. Cancel the unfilled stop at the session's end.
  - **Video systems** (the only systems in the pre-registered sweep):
    - **S1:** L = 20 entry, exit M = 10. Long exits on a stop at `DonchianLow(10) − 1 tick`; short mirrors.
    - **S2 (primary):** L = 55 entry, exit M = 20.
    - **Modern:** L = 55 entry. Exit is **not** a channel. At the close of day t, if a long's adjusted close is below EMA(20) of adjusted closes, exit at the **next** session's open (1 tick slip). Short: close above EMA(20). EMA period 20, multiplier 2/21, seed = SMA of the first 20 adjusted closes, usable from bar 20. The video drew no initial stop on this arm. **Catalogue assumption:** initial stop = `2 × Atr20d` beyond the fill (Wilder ATR from M-DAILY, `atrLength = 20`, `atrRestartOnRoll = false`), and the trade also exits if that stop is traded through. Whichever of the stop and the EMA exit comes first.
  - **S1/S2 risk stop:** the exit channel **is** the stop. There is no tighter video stop. Distance = fill − exit-stop price (long). If that distance is ≤ 0 (the channel is on the wrong side of the fill), skip.
  - **Size (video):** risk% of equity at that distance. `contracts = floor(risk% × equity / (distance × $/point))`. `risk%` is 1% in the primary cell and 2% in the sweep. Floor 0 → skip, do not round up. $/point: ES 50, MES 5, NQ 20, MNQ 2, and the root's own point value otherwise. The §2.1 14-tick floor does not bind on a daily channel stop; do not widen a daily stop to 14 ticks.
  - **Management:** no partial, no breakeven, no pyramid in the video systems. Reverse is allowed: an opposite signal flattens and enters the new side only when that side's own signal fires (do not reverse inside the channel). Flat means no position, not "always in".
  - **Optional public arms (Faith 2003 document; each is one extra config; do not cross them with each other or with the six video cells):**
    - **O1 — N unit, channel exit.** `TR = max(H−L, |H−PDC|, |PDC−L|)` on the adjusted daily bar. `N = (19×PDN + TR) / 20`, seeded with the 20-day simple average of TR. This is Wilder ATR(20) and equals `Atr20d` when the flags above are set. `Unit = floor(0.01 × equity / (N × $/point))`. Entry and exit stay on the S2 channel. One unit, no pyramid.
    - **O2 — 2N stop.** As O1, plus an initial stop `2×N` beyond the fill. Exit is the first of that stop and the S2 channel exit. Faith: no trade risks more than 2% because 1N was sized as 1% of equity.
    - **O3 — pyramid.** As O2. Add one unit every `0.5×N` beyond the **previous fill** (not beyond the original breakout), up to 4 units. On each add, raise every earlier unit's stop by `0.5×N`, so the book sits about `2×N` beyond the latest fill. Adds may happen the same day. Still capped at 4 units in one root (Faith's single-market cap). The 6/10/12-unit correlation caps are **not** implemented here; PF-03 / M-PORT heat is the book cap instead.
    - **O4 — S1 skip-after-winner.** S1 only. A 20-day breakout is **ignored** when the previous breakout in that root, whether it was taken or skipped, and in either direction, was a winner. It was a loser only if price moved `2×N` against the breakout direction before a profitable 10-day exit. If the 20-day breakout is ignored, the entry for that episode is the 55-day breakout (Faith's failsafe). Sizing and stop as O2, no pyramid. S2 does not use this filter.
- **Sweep:** video layer, system ∈ {S1, S2, Modern} × risk% ∈ {1, 2} → **6**. Primary cell, read first: **S2 × 1%**. Optional arms O1–O4 are four further pre-registered configs, not a factorial. Total 10, under the §1 cap of 30. No other Donchian lengths.
- **Frequency (est.):** a handful of entries per root per year per system. Author estimate only.
- **Fails when:** a multi-year range (the channel whipsaws); one root dominates the book's P&L; the adjusted series is wrong at a roll (a gap left in the high/low buffer); the modern arm's 2×ATR stop, which the video never stated, cuts trends the EMA would have kept.
- **Needs:** ✓ M-DAILY (difference-adjusted o/h/l/c, `Atr20d`, `atrRestartOnRoll = false`, ring ≥ 60) · ✓ F-ROLL (splice flag; skip the splice session) · ✗ no public Donchian accessor (local buffer of adjusted highs/lows, same roll rebase) · EMA(20) computed in the strategy on adjusted closes · ◐ M-PORT for the multi-root book (coded, P5b live checks pending). Single-root ES does not need M-PORT.
- **Readiness (v2.7):** Buildable on W-D for one root (ES) with the adjusted-bar buffer. The book waits on M-PORT Probe P5b before any portfolio number is read. No order flow.
- **Gate:** G0-a and G0-b. Not a G0-d level (no prior-session profile).
- **Test:** on the same adjusted series and the same 1% risk, against (1) RG-06's daily piece, long/short the sign of `Tsmom(20)`, flat when the sign is zero, stop `2 × Atr20d`; (2) buy-and-hold ES. Report longs and shorts separately (K-DR). A long-only ES result is not accepted as the sleeve: CL/GC/ZN (once the bundles are verified) have to be in the book before the result is called anything other than index drift. Costs: ES $17 per round trip per contract, MES $2.50, other roots at the unconfirmed §2.1 R6 figures. **Kill:** on the holdout, net Sharpe ≤ the TSMOM(20) sleeve **or** ≤ 0. Below 30 pooled round-trips on the dev book: no evaluation (§2.3). Between 30 and 149: park. An ES-only run is expected to land in that band; it does not get a loosened rule.
- **Differs from:** **PF-01** (EWMAC at several speeds + carry + 12% vol target; this is one Donchian, no carry). **RG-06** (sign of a 20/60-day return, not a channel break). **A2-71** (Turtle −20% size per 10% drawdown and ATR sizing; an overlay, not an entry — it may sit on top of PF-08 later, and it is not switched on here). **YV-09** (the same video, discretionary; the "initial stop = the exit channel" note there is adopted above for S1/S2 and marked as a catalogue assumption for the modern arm). **AV-03 / A2-02** use a 20-day Donchian only as a control, not as this sleeve. **SW-01** is a weekly balance break, not a daily channel.
- **Sources:**
  - Video (local file; public match): https://youtu.be/lDn93erRQSM
  - YV-09 in this catalogue (§3.19) and `specs/video-ideas-2026-11-01-additions.md`
  - Optional arms only: Curtis Faith, *The Original Turtle Trading Rules*, https://www.tradingblox.com/originalturtles/originalturtlerules.htm — N = 20-day exponential true range, unit = 1% / (N × $/point), stop 2N, add every ½N up to 4 units, System 1 skipped when the previous breakout was a winner, 55-day failsafe.

### 3.12 Analyst 2's strand (Rev 6): merged and deduplicated

Source: `/workspace/trading-ideas-analyst2.md` **Rev 6** (79 ideas A2-01…A2-79, Appendix A L2-01…L2-07, Methodology M1…M30). v1 merged only Rev 1 (A2-01…A2-24). v2 merges the whole file, re-checked against `catalogue-merge-audit-analyst2.md`.
- **Result:** 56 A2 ideas are their own entries, and 23 are merged into hosts, each marked **Merged · A2-xx** inside the host with A2's text and the original grade.
- **Locations:**
  - Gate-0 entries are in §3.0.
  - Exit/risk modules A2-72…A2-79 are in §3.13.
  - L2-01…L2-07 are in §3.15.
  - M1…M30 are in §9.

**A2's conclusion is adopted:** fading at VWAP or AVWAP has no published edge.
- VWAP is used only as a **trail, filter, target or acceptance level**.
- Every AVWAP idea must beat A2's placebos: **P1** (an unweighted average from the same anchor), **P2** (a volume-shuffled AVWAP) and, where relevant, **P3** (EMA20).
- If P1/P2 tie, keep the idea only as a momentum/structure idea and drop the "VWAP" label.

**Grade harmonisation (rescaling rules; the original grade is always shown next to the catalogue grade):**
- **A2 Rev 1 (A2-01…A2-24):**
  - Telonics/RipBacktest "A (negative)" → **B (−)**: independent, controlled practitioner research, not peer-reviewed.
  - Shannon/Levine "C" → **E/H**: A2's "C" there meant "plausible hypothesis", not "the author's own data".
- **Keller Book 2 (A2-25…A2-40):** A2 keeps the book's own scale (A documented, B mechanism, C untested). Mapping:
  - A for a documented **method** → A (method) or B (descriptive);
  - B mechanism → **D**;
  - C untested trade → **H**.
- **Keller Book 4 (A2-41…A2-48):**
  - "B (other asset class)" population scans on FX/crypto → **C**: real data with material limits (not CME, not index futures);
  - exhibit ratios → **E**.
- **Kaufman TSaM/KCTS (A2-49…A2-71):**
  - "B−" (the author's own gross or in-sample tests, often pit-era) → **C**;
  - "C" with numbers → C or E, depending on sample;
  - "C" without numbers → **E**.
  - Our 2016–2026 data post-dates almost all of these samples, so a replication here is genuinely out of sample.
- **Minervini (A2-72…A2-79):** "C" (stated rule, no test) → **E**; "D" → **D**.

**New evidence brought in by A2 (spot-checked):** the VWAP *trailing stop* on a noise-band momentum entry. Zarattini, Barbon & Aziz ("Beat the Market") report that the trail doubles the Sharpe (0.61 → 1.24) on SPY. Chuk (SSRN 7609644) runs a pre-registered NQ replication. **EdgeLab (Jul 2026, verified here):** on 2010–2026 NQ 1m data, long-only business-day Sharpe was 0.90 in-sample and 0.68 out-of-sample, net of a modelled 0.01% round-trip cost. The short side was −0.21 in-sample and its positive OOS run is regime luck. The trail role is therefore graded **B (+)**, long side only.


#### Mapping table (all 79 A2 IDs)

| A2 ID | Analyst 2 idea | Disposition | Catalogue grade | Original A2 grade |
|---|---|---|---|---|
| A2-01 | Event-AVWAP Handoff Pullback (trend continuation) | **Merged into AV-04** | E | C |
| A2-02 | AVWAP Pinch Break (multi-anchor convergence → expansion) | **Merged into AV-03** | E | C |
| A2-03 | Breakout-AVWAP Trail on Analyst 1's Spec 1 (exit overlay) | **Merged into EX-04** | B (+) trail role / H transfer | B |
| A2-04 | Overnight-Inventory vs oVWAP Open Drive | Own entry (§3.12) | E/D | C |
| A2-05 | Failed Reclaim of the Event AVWAP (trapped-cohort short) | **Merged into AV-12** | H | C |
| A2-06 | Session-VWAP Side as Momentum Hold Filter for the Last 30 Minutes | **Merged into VW-05** | B/C (0/−) | B (base effect) / C (VWAP variant) |
| A2-07 | Weekly/Monthly AVWAP Regime Filter for Analyst 1's Specs | **Merged into AV-08** | B (0) / H | C |
| A2-08 | AVWAP From the Initial-Balance Extreme (IB Breakout Acceptance) | **Merged into PS-08** | inherits host B (0/+) | C |
| A2-09 | Noise-Band Long-Only with AVWAP-of-Day Trail, MNQ (replication target) | Own entry (§3.12) | B (+) long-only | B |
| A2-10 | AVWAP σ-Band Breakout Continuation (bands as momentum, not reversion) | **Merged into VW-03** | H | C |
| A2-11 | Prior-Day VWAP "Static Level" Acceptance Test | Own entry (§3.12) | B (0) | C |
| A2-12 | Volume-Weighted vs Unweighted Divergence as Inventory Signal | Own entry (§3.12) | H | C |
| A2-13 | Gap-Day AVWAP-from-Open Fade Failure → Go-With (Shannon gap chapter) | Own entry (§3.12) | E | C |
| A2-14 | Weekly AVWAP Reclaim Swing (MES daily) | Own entry (§3.12) | E | C |
| A2-15 | Volume-Profile POC of the Anchored Leg vs AVWAP of the Same Leg (agreement filter) | Own entry (§3.12) | H/E | C |
| A2-16 | Opening-Range AVWAP Slope Regime (trend-day classifier) | **Merged into RG-04** | inherits host D / B (0) | C |
| A2-17 | Multi-Session AVWAP from a High-Volume Liquidation Bar (capitulation anchor) | **Merged into AV-07** | H | C |
| A2-18 | Developing-Profile POC Migration vs sVWAP Lag (value-migration timing) | **Merged into PS-10** | D | C |
| A2-19 | Event-AVWAP Bands as Profit Targets for Analyst 1 Spec Runners | **Merged into AV-13** | H | C |
| A2-20 | CL Inventory-Release AVWAP Continuation (Wednesday 10:30 EIA) | **Merged into AV-06** | E | C/B− |
| A2-21 | FOMC-Statement AVWAP Second-Leg Entry (14:00 ET) | **Merged into AV-06** | E | C |
| A2-22 | AVWAP-Anchored Cumulative Delta Divergence as Exit-Only Signal | **Merged into EX-12** | E | C |
| A2-23 | Quarterly-Roll / Contract-Expiry AVWAP Reset (structural anchor) | **Merged into AV-09** | H | C |
| A2-24 | VWAP Fade Negative Control Suite (must run) | **Merged into VW-08** | B (−) | A (negative) |
| A2-25 | POC Bootstrap-Stability Gate and Minimum-Stop Rule | Own entry (§3.12) | B for the statistic (Keller method) · H for the trading lift | B for the statistics, C for the trading lift |
| A2-26 | KDE Bandwidth-Sweep Veto (no trade on FLAT or BIMODAL POCs) | Own entry (§3.12) | A for the estimator (method) · H for the lift | A for the estimator, B/C for the trading lift |
| A2-27 | Bimodal-Profile Void Traverse (continuation across the void) | Own entry (§3.12) | D (mechanism) · H (trade) | B (mechanism) / C (trade) |
| A2-28 | Minor-Component Excess Rejection (confirmation, not a touch fade) | Own entry (§3.12) | H | C |
| A2-29 | Entropy Day-Type Gate | Own entry (§3.12) | B (descriptive) · H (gate) | A descriptive, C for the gate |
| A2-30 | Rising-Entropy Balance Break (leading-signal hypothesis) | Own entry (§3.12) | D/H | B/C |
| A2-31 | KL-Divergence Acceptance / Migration Gate for Prior-Day Levels | Own entry (§3.12) | H | C |
| A2-32 | Volatility-Normalised Mixture Composite (σ-space levels) | Own entry (§3.12) | D (method) · H (trade) | B (method) / C (trade) |
| A2-33 | Level-Persistence vs Matched-Control Study (must run before any level idea) | Own entry (§3.0) | n/a (infrastructure; Keller B2 matched-control method) | infrastructure; outcome decides which level types to keep |
| A2-34 | V/T Urgency-Price Revisit (event study first) | Own entry (§3.12) | H | C |
| A2-35 | Macro-Event Re-Formation Value Area (CPI/NFP 08:30, FOMC 14:00) | Own entry (§3.12) | D (mechanism) · H (trade) | B (mechanism) / C (trade) |
| A2-36 | Dealer-Gamma Regime Filter (regime, not level) | Own entry (§3.12) | D (regime read) · H (flip level) | B for the regime read, C for any specific flip level |
| A2-37 | Cross-Instrument Dislocation Fragility Filter | Own entry (§3.12) | D | B |
| A2-38 | Metaorder Drift Completion (tick window only) | Own entry (§3.12) | H | C |
| A2-39 | Roll-Honest Composites on Unadjusted `@ES` (infrastructure) | Own entry (§3.0) | D | B |
| A2-40 | Honest Cost Model and Stressed-Correlation Sizing (infrastructure) | Own entry (§3.0) | A (method) | A (documented) |
| A2-41 | Book-Spec Balance Break with a Balance-Quality Filter (variant of A1 PS-02) | **Merged into PS-02** | C (FX/crypto base rates, other asset class) · H (quality filter) | B (other asset class) for the base rates (FX 378 breaks, 56.6% reach 1 W, median 1.17 W; crypto 59.1%, 1.20 W). **C** for the quality filter, which the book itself never tested. CME: untested. |
| A2-42 | 80% Rule Resized as a 1-in-3, 3:1 Trade (variant of A1 PS-03's naive canary) | **Merged into PS-03** | C (FX/crypto traverse rates) · E (3:1 geometry, exhibit ratios) | B (other asset class) for the 31.4% FX / 28.4% crypto traverse rates (≈ 19,000 re-entries). **C** for the 3:1 geometry (exhibit ratios, selected). Pit-era CME day session is the original setting, but the book flags it as untested on modern index futures. |
| A2-43 | Confluent-HVN Level Gate (HVN counts only with independent confluence) | Own entry (§3.12) | E (and the book's own HVN rejection ratios are < 1) | C |
| A2-44 | Post-Crossing Fast-LVN Continuation (variant of A1 PS-15) | **Merged into PS-15** | C (−) against the anticipatory version · E (confirmed version) | B (other asset class) **against** the anticipatory version; C for the confirmed version. |
| A2-45 | Single-Print Shelf as Destination (near edge target, runner to the far edge) | Own entry (§3.12) | H | C |
| A2-46 | Volume-Gap Commitment Entry to the Far Node (variant of A2-27; contrast with PS-15) | Own entry (§3.12) | E (exhibit ratios, selected) | C |
| A2-47 | Confirmed Failed Auction Aligned with Value Migration, Target Inside Value (variant of A1 PS-03 / A2-28) | **Merged into PS-03** | E (book practitioner) · B (−) for the fade-at-touch version | C (B practitioner in the book; the fade-at-touch version is B-negative with MAE 6.3 / 12.6 ATR). |
| A2-48 | Poor Highs/Lows as Destinations (exit and stop-placement module) | Own entry (§3.12) | B (0) prior for the magnet (Perfiltrade 11.9% vs 12.9% controls) · H for the module | C |
| A2-49 | Midday Support/Resistance and Afternoon Breakout (TSM Midday S&R replication) | Own entry (§3.12) | C (+, gross) | B− (gross, old contract and pit hours) |
| A2-50 | Same-Slot Relative Volume Opening Regime Gate (Kaufman intraday volume) | Own entry (§3.12) | E | C |
| A2-51 | Intraday Shock-Bar Fade with Delayed Stop and Volume-Decay Exit | Own entry (§3.12) | C (other markets, gross) | C (other markets, gross) |
| A2-52 | Time-Confirmed Opening-Range Break with Pivot-Range Bias (Fisher ACD, variant of A1 ORB specs) | Own entry (§3.12) | E (practitioner, no numbers); plain ORB ≈ 0 net (B, 0) | C |
| A2-53 | RTH Gap Fade with Stop-and-Reverse at the Day's Extreme (TSM Intraday High-Low; negative canary) | Own entry (§3.12) | C (gross; net expected ≤ 0) | B− (gross; the net edge is likely ≤ 0) |
| A2-54 | Up-Gap Pullback-to-Prior-Close Short, Exit Next Open (TSM Intraday Gaps) | Own entry (§3.12) | E | C |
| A2-55 | Volatility-Contraction Gate for Intraday Breakout Hosts (Crabel / Kaufman module) | **Merged into RG-08** | C (CL, gross) · E (ES, few trades); NR7 stays negative | B− (CL), C (ES: few trades); the catalogue grades NR7 negative. |
| A2-56 | Momentum Pinball: Daily LBR/RSI Setup, First-Hour Break, Conditional Overnight Hold | Own entry (§3.12) | C (+, gross) | B− (gross, but $703 leaves a large cost margin) |
| A2-57 | Higher-TF Turn plus First Short-TF Oversold Entry (Kaufman/Elder MTF tuning) | Own entry (§3.12) | E | C |
| A2-58 | Fade the Fast Trend Inside the Slow Trend, Intraday (Kaufman "profiting from the worst results") | Own entry (§3.12) | C (daily NASDAQ) · H intraday | B− on daily bars, C intraday |
| A2-59 | Kaufman Cost, "Unables" and Shock-Audit Module (infrastructure, extends A2-40) | Own entry (§3.0) | A (method) | n/a (method) |
| A2-60 | Meyers Normalised Range-Ratio Adaptive Breakout (5m, flat before the close) | Own entry (§3.12) | E (one optimised ETF article, no OOS) | C |
| A2-61 | Efficiency-Ratio Session Router (noise gate selecting the reversion vs continuation arm) | Own entry (§3.12) | E | C |
| A2-62 | Taylor 3-Day Cycle "Low-First" Buying Day (daily rhythm, RTH execution) | Own entry (§3.12) | E/D | C/D |
| A2-63 | Long-Only Open + 0.5 × Daily ATR Breakout, Exit at the Close (KCTS Ch 25) | Own entry (§3.12) | C (+, in-sample, no slippage, multi-contract sizing) | B− (ES, multi-year, but in-sample, no slippage and multi-contract sizing) |
| A2-64 | 1.0 × Daily ATR Fade from the Open or Prior Close, with the Prior-Close Exit (KCTS Ch 25) | Own entry (§3.12) | E (S&P leg not isolated, in-sample) | C (S&P result not isolated, in-sample) |
| A2-65 | Raschke "1st Cross" — First Oscillator/Trend Cross with Bar Confirmation (KCTS Ch 24) | Own entry (§3.12) | C (+, daily ES) · H on 30m | B− on daily ES; C on intraday bars (untested) |
| A2-66 | Raschke/Kaufman "2nd Cross" — Second Stochastic Pullback in a Confirmed Trend (KCTS Ch 24) | Own entry (§3.12) | C (daily ES, 86 trades, in-sample) | B− (daily ES, but only 86 trades and in-sample) |
| A2-67 | Intraday RSI-of-Highs/Lows Divergence Long (KCTS Ch 25, IWM/QQQ) | Own entry (§3.12) | E (14 trades, one year) | C (one year, 14 trades) |
| A2-68 | 5-Bar Run + Doji Reversal, Target 50% of the Run (KCTS Ch 25, from S&C) | Own entry (§3.12) | E (S&C article, no numbers) | D |
| A2-69 | Kaufman 3-Day Trade — Two Down Closes, Buy a Lower Open, Exit Next Close (KCTS Ch 15/24/27) | Own entry (§3.12) | C (+, multi-decade, gross) | B− (multi-decade ES/NQ/RU, gross) |
| A2-70 | High-Volatility Skip Gate (KCTS Ch 3/5/15/18; module) | Own entry (§3.12) | E | C (SPY daily test only; thresholds are market-specific) |
| A2-71 | ATR Risk Sizing and 12% Volatility Targeting for the A2 Book (infrastructure, extends A2-40) | **Merged into PF-06** | C (method, in-sample book numbers); PF-06 host grade applies to the vol-targeting principle | C (method; book numbers are in-sample). |
| A2-72 | Progressive Exposure Ladder — 25/50/100% After Wins, Step Down After a Loss (sizing module) | Own entry (§3.13) | D | D (illustrative table, no test) |
| A2-73 | Breakeven Stop at ≈ 3R or Above the Rolling Average Gain (exit module) | Own entry (§3.13) | E | C (stated numeric rule, no test) |
| A2-74 | Free Roll — Sell Half at 2–3R, Remainder at Breakeven (scale-out module) | Own entry (§3.13) | E | C |
| A2-75 | Rolling Hit-Rate Throttle — Tighten Stops and Targets When the Batting Average Falls (RBA module) | Own entry (§3.13) | D | D (no test; the trigger is discretionary in the book) |
| A2-76 | Sell Into the Climax Bar — Largest Range or Largest Up Bar Since Entry (profit-exit module) | Own entry (§3.13) | D | D (daily-chart heuristic; intraday analogue untested) |
| A2-77 | Failed Follow-Through Exit — Time Stop and Bar-Quality Test After Entry (exit module) | Own entry (§3.13) | E | C (stated heuristics) |
| A2-78 | Daily Loss Kill-Switch and Rules-First Flatten (risk module, infrastructure + test) | Own entry (§3.13) | D for edge; mandatory as an operational control | D for edge (no evidence it improves expectancy); mandatory as an operational control |
| A2-79 | Intraday Volatility-Contraction plus Volume Dry-Up Entry Gate (VCP analogue; module) | Own entry (§3.13) | D | D (a daily/weekly chart pattern; intraday analogue needs empirical testing) |

#### Analyst 2's convention blocks (verbatim; they apply to the A2 entries they name)

### Profile-statistics conventions for A2-25 to A2-35 (no lookahead)
- **Profile source:** RTH 1m bars, each bar's volume spread uniformly across its range at 1-tick rows (window M). Inside window T, rebuild from ticks and report both. A "prior-session" statistic is computed after the 16:00 ET close and used only from the next session's open. A "developing" statistic at time t uses only closed 1m bars up to t.
- **Bootstrap POC (Keller 10.1):** resample the session's 1m bars with replacement (block size 5 bars, to respect order-splitting autocorrelation), 500 draws, rebuild the profile, record the POC. `ci90` = 5th–95th percentile POC range (ticks); `stability` = share of draws within ±1 tick of the median POC.
- **KDE sweep (10.2):** Gaussian KDE on volume-weighted prices, bandwidth h from 0.05σ to 1.5σ (30 steps; σ = session volume-weighted SD). Verdict: STABLE if max POC jump < 2× median jump and POC range < 0.15σ; BIMODAL if max jump > 5× median jump; else FLAT.
- **Mixture (10.4):** volume-weighted GMM, K = 1..3 chosen by BIC, 10 restarts; prefer the smaller K unless BIC improves by > 10. Components μ_k, σ_k, π_k; void = density minimum between adjacent μ's.
- **Entropy (10.3):** H_norm = H/ln(n) over 1-tick rows, Miller–Madow corrected; n_eff = 1/HHI. Percentiles are versus the trailing 250 sessions (rank known at decision time).
- **Roll rule:** multi-session statistics never span a roll gap on unadjusted `@ES` (see A2-39).

### Conventions for A2-41 to A2-48 (no lookahead)
- **Profiles:** as in the A2-25 conventions (RTH 1m, volume spread uniformly over each bar's range, 1-tick rows; tick rebuild inside window T). Rows are reported in ticks, and also as a fraction of ATR(14, 30m) for comparison with the book's ATR-row convention.
- **Balance / W:** Analyst 1's `IsBalanced(RTH, n, 0.6)` (≥ n consecutive sessions whose VAs overlap ≥ 60%), evaluated after the prior 16:00 close. Composite = those n sessions; W = composite VAH − composite VAL. Valid from the next 09:30 open only. Never spans a roll (A2-39).
- **Value migration (V):** Analyst 1's `ValueMigrationScore` on completed sessions. Also compute the book's preferred form, the slope of a regression of the last 3 RTH **VA midpoints** against session index, divided by ATR(14, daily). "Migration up" = slope > +0.10; "down" < −0.10; else neutral. Report how often POC-based and VA-midpoint-based signs disagree (Keller Drill 4.5) and use the VA-midpoint version.
- **δ:** max(2 ticks, 0.05×W) for composite edges; max(2 ticks, 0.05×prior-session VA width) for single-session edges.
- **Single prints (TPO):** a 1-tick row touched by exactly one 30m RTH period. A **shelf** is a contiguous run of ≥ 4 such rows (no free threshold beyond the minimum width; Ch 6.5).
- **Poor high/low:** Analyst 1's `IsPoorHigh`/`IsPoorLow` (session extreme row touched by ≥ 2 TPO periods and no single-print tail ≥ 2 rows).
- **HVN/LVN:** Analyst 1's KDE detector (§2.2 of the catalogue: 6-tick ES / 20-tick NQ bandwidth, prominence ≥ 10%), plus the book's ratio form for sweeps: HVN row v_i > θ×median(v), LVN v_i < φ×median(v).
- **Costs and sizing:** A2-40 cost model. Fixed-fractional risk per trade; any idea with an expected hit rate < 40% must report max losing streak and the 95th-percentile streak under a bootstrap.

### Kaufman TSaM conventions (A2-49 to A2-62)
- **Source:** Kaufman, *Trading Systems and Methods* 5th ed. (2013), notes in `kindle-notes/kaufman-tsam-analyst2.md`. The book's results are **gross of costs, on 1 contract, and pit-era or old-contract**. None is net. Grade accordingly:
  - **B−** = a published multi-year ES/S&P test of exactly that rule;
  - **C** = a published test on another market, or a practitioner rule with no numbers;
  - **D** = conceptual.
- **"Same-slot baseline" (SSB):** for a 1m/5m/15m/30m bar at clock time τ, the mean (and SD) of the same statistic (volume, TR) over the bars at τ in the trailing N RTH sessions. Today is excluded, and only closed sessions are used. Kaufman's "average-off" bar. Default N = 20 for volume and 100 for TR (the book's figure). **Lagged SSB:** the same, but excluding the last 5 sessions (Kaufman's lagged spike baseline).
- **RTH gap:** the 09:30 ET open vs the prior 16:00 ET close of the same contract. Roll days are excluded. Globex ES has essentially no gaps, so the overnight session must not be used for gap definitions.
- All entries are at the open of the bar after the signal bar closes, unless stated. All are flat by 15:55 ET unless an overnight hold is part of the rule. Overnight holds are **exit at 09:31 ET next RTH** unless stated, and they carry the overnight-gap risk in the cost model (A2-59).
- Every idea reports **$/contract net** (Kaufman's day-trading metric), plus the Methodology items M1–M12 below.

### KCTS conventions (A2-63 to A2-71)
- **Source:** Kaufman, *Kaufman Constructs Trading Systems*, notes in `kindle-notes/kaufman-constructs-analyst2.md`. Ch 24–25 results are on ES/SPY/IWM/QQQ, mostly 2010–2020. They are **in-sample**, often gross or at $8/contract with no slippage, and Kaufman admits one equity curve was "too good" because it was scaled in-sample. Grades use the TSaM scale above (B− = published multi-year ES/S&P test of that rule; C = other market or no numbers; D = conceptual).
- The TSaM conventions block (SSB, RTH gap, next-bar-open entries, flat 15:55, overnight exit 09:31) applies here too.
- **Daily ATR(20):** simple (not Wilder) mean of the true range of the **20 completed RTH daily bars** of the same contract (roll-safe per A2-39). Fixed at 09:30 and never updated intraday. Kaufman uses daily ATR, not intraday volatility, for all intraday thresholds.
- **HV20:** SD of the last 20 daily RTH close-to-close log returns × √252, from completed days.
- **Dedup check done against A2-01 to A2-62.** The closest neighbours are named in each idea's "Differs from" line: A2-09 (noise band), A2-52 (ORB), A2-53 (gap fade), A2-57 (first-OS MTF), A2-58 (fast-in-slow fade), A2-62 (Taylor cycle), A2-40/59 (costs/sizing) and A2-55 (contraction gate).

#### A2 entries carried from v1 (A2-04, A2-09, A2-11…A2-15)

#### A2-04 · Overnight inventory vs oVWAP open drive
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS/VW · A2 (Dalton/Steidlmayer overnight inventory + Shannon "price vs AVWAP = who's in control").
- **Evidence:** **E/D** · untested · Lit: Practitioner/Book · **original A2 grade: C**.
- **Markets · TF:** ES/NQ; ETH context 18:00–09:29, 5m execution.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) + needs ETH bars (verify E5)
- **Why it might work:** When overnight longs are in profit (ON close well above the overnight VWAP) and the RTH open is at or above the ON high, the overnight cohort is not under pressure to liquidate. RTH initiative can extend their move. TS-06 tests the opposite case, when the open is still inside prior value.
- **Rules:** *Setup at 09:29:* ON close − oVWAP > +0.5 × ATR(14, 30m), and the RTH open is ≥ ON high − 0.25 × ATR30. *Entry:* the first two 5m RTH bars close above oVWAP and the opening 5m low holds → long at the 09:40 close. *Stop:* opening 10-min low − 1 tick, capped at 0.6 × ATR30 and floored. *Target:* T1 1 R; runner trails at sVWAP from 10:00 (exit on a 5m close below); flat 11:30.
- **Sweep:** inventory threshold ∈ {0.5, 0.75} ATR30 → 2.
- **Frequency (est.):** 3–6 per month.
- **Fails when:** news at 08:30 created the ON move; gap-and-crap opens.
- **Needs:** F-VWAP (ETH session slot), F-SESS; ✗ ETH-session template verified.
- **Readiness (as of merge):** Not yet buildable as specified — still needs: ETH-session template verified.
- **Test:** A2's control (the same rule without the oVWAP condition, ON-range position only); P1 (TWAP of the overnight). Shares a slot with TS-06 (the two are mutually exclusive by construction). **Kill:** the oVWAP condition adds < 0.03 R.

#### A2-09 · Noise-band long-only with sVWAP trail (MNQ benchmark)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW/TS · Zarattini, Barbon & Aziz ("Beat the Market"); Chuk (SSRN 7609644); EdgeLab NQ replication.
- **Evidence:** **B (+), long side only** (EdgeLab: business-day Sharpe 0.90 IS / 0.68 OOS net of 0.01% RT cost; 6/9 OOS years positive; drawdown −11%). The both-sides version is inflated by short-side regime luck. Lit: Indep · **original A2 grade: B**.
- **Markets · TF:** MNQ (then MES as a robustness check); 1m bars, decisions at :00/:30.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1) (EdgeLab used 2010–2026 Databento NQ; our window starts 2016)
- **Why it might work:** Moves outside the typical time-of-day range from the open reflect real order imbalance (and, per Baltussen, dealer hedging flows) that persists intraday. The sVWAP trail exits when the day's average buyer stops being in profit.
- **Rules:** noise area = open × (1 ± mean |move from open| at the same clock time over 14 sessions); on gap days the upper reference is max(open, prior close). At :00/:30 closes: long if close > upper band. Exit at a check time if close < max(upper band, sVWAP), or 15:59. No shorts. 1 MNQ fixed.
- **Sweep:** none (replication; 1 config). Stress test: +1 point stop slippage.
- **Frequency (est.):** several entries per week (re-entries allowed).
- **Fails when:** cost slippage at :00/:30 crowding; post-2024 soft patch (EdgeLab notes one).
- **Needs:** F-VWAP, ✓ M-TOD (time-of-day noise), F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** replicate first (target: **Chuk's net +$10/day ±**, A2; restored v2), then forward-paper 60 sessions against pre-registered thresholds (A2). It is the **benchmark every intraday idea must beat on a risk-adjusted basis**. **Kill:** forward-60 mean < 0, or holdout bootstrap lower bound < 0.

#### A2-11 · Prior-day VWAP static-level acceptance
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · A2, from Telonics (pdVWAP reach-first 51.18%, the only static anchor).
- **Evidence:** **B (0)** for the level; the acceptance entry is untested · Lit: Indep (null) + A2 · **original A2 grade: C**.
- **Markets · TF:** ES/NQ; 15m acceptance, 5m HL, 1m trigger.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); skip the first session after a splice
- **Why it might work:** Only as an acceptance level. Once price holds above yesterday's average transaction price, yesterday's buyers are in profit and there is less overhead supply.
- **Rules:** the open is below pdVWAP; 2 consecutive 15m closes above it; the first 5m pullback HL above pdVWAP; enter on a 1m break of structure. *Stop:* HL − 1 tick (floored). *Target:* the nearer of prior-day high / prior VAH if ≥ 1 R away, else skip.
- **Sweep:** acceptance bars ∈ {2, 3} → 2.
- **Frequency (est.):** 3–6 per month.
- **Needs:** F-VWAP (pdVWAP = prior Session-slot value latched at the session end; ✓ no new module needed), F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** the same rule at the prior-day midpoint and at the prior POC (A2's controls). **Kill:** pdVWAP does not beat both.

#### A2-12 · VWAP − TWAP divergence as an inventory signal
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VW · A2 original (derived from the Telonics placebo design).
- **Evidence:** **H** · Lit: Author (A2) · **original A2 grade: C**.
- **Markets · TF:** ES/NQ; 1m → 30m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** If VWAP ≈ TWAP, volume carried no price information. A large gap means volume concentrated away from the time-centre, i.e. one-sided participation. It is the cheapest direct test of whether volume weighting carries *any* information in these markets.
- **Rules:** D = (sVWAP − sTWAP)/ATR30 at 11:00. *Event study first:* afternoon return by D quintile. Strategy (only if the study passes): D > +0.25 and price > sVWAP → long at 11:00; stop 1 × ATR30; exit 15:55 or on a 30m close across sVWAP.
- **Sweep:** D threshold ∈ {0.25, 0.5} → 2.
- **Frequency (est.):** event study: every session; strategy: a minority of days.
- **Needs:** F-VWAP + a TWAP line (trivial).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** proceed only if the top-vs-bottom quintile spread > 0.15 ATR with a session-bootstrap CI excluding 0 (A2). **Kill:** the event study fails.

#### A2-13 · Gap-day AVWAP-from-open: hold → go-with, fail → fade-to-value
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV/PS · Shannon gap chapter (via A2); complements Keller IX-2/IX-3 (PS-05/06).
- **Evidence:** **E** · untested · Lit: Practitioner · **original A2 grade: C**.
- **Markets · TF:** ES/NQ; 5m/1m.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1); skip roll-splice sessions
- **Why it might work:** The AVWAP from the RTH open is the gap-day cohort's cost. If it holds, the gap is accepted; if price is accepted below it, the gap buyers become supply. It is the same mechanism as RRO/IX-2, with an AVWAP location instead of the prior VA.
- **Rules:** *Filter:* gap ≥ 0.5 × ATRd and the open is outside the prior VA. *Branch A (hold):* no 5m close below AVWAP_open by 10:15; the first pullback to within 0.1 × ATR5 then a 1m close in the gap direction → enter; stop AVWAP_open − 0.3 × ATR5 (floored); 1 R then trail on AVWAP_open; flat 15:55. *Branch B (fail):* 2 consecutive 5m closes below AVWAP_open before 10:30 → enter against the gap on a 1m close below the 2nd bar's low; stop at the high since the first close below + 1 tick; target prior VAH, then the prior close.
- **Sweep:** none beyond the two branches (2).
- **Frequency (est.):** 2–5 per month.
- **Needs:** F-VWAP (session slot starting at 09:30 = AVWAP_open), F-SESS, F-PROF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** runs in the **PS-05/06 slot**; each branch separately; **overlap with RRO/IX-2 is reported** (not a kill criterion; v1's > 60%-overlap kill was an addition and is removed). P1/P2 placebos (A2 §0). **Kill (A2, restored v2):** generic criteria per branch (§2.3).

#### A2-14 · Weekly AVWAP reclaim swing (MES daily)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** AV/SW · Shannon (the week's AVWAP defines short-term swing control), via A2.
- **Evidence:** **E** · untested; the Telonics weekly anchor is null as a bounce level · Lit: Practitioner · **original A2 grade: C**.
- **Markets · TF:** MES/MNQ (then GC/ZN as drift controls); daily.
- **Data · test window:** **daily · W-D primary** (A2: window D only; restored v2), split §2.0.1; the week-open AVWAP needs 1m/ETH bars only to build the anchor inside W-M, so the W-M part is a robustness re-run, not the test; multi-day levels reset at rolls (A2-23 roll anchor variant)
- **Why it might work:** After ≥ 2 daily closes below the week's AVWAP, a close back above it puts the week's average buyer back in profit while the 20-day trend is up, a "failed dip" of the weekly cohort.
- **Rules:** daily close below the week-open AVWAP for ≥ 2 days, then a close back above, with 20-day SMA slope > 0 → buy at the next RTH open. *Stop:* lowest low of the below days − 0.1 × ATRd. *Target:* 2 R, or a daily close back below; time stop 7 sessions.
- **Sweep:** days below ∈ {2, 3} → 2.
- **Frequency (est.):** 1–2 per month.
- **Needs:** ✓ M-AVWAP (`WeekOpen` anchor) or F-VWAP `EVwapType.Week`; ✓ M-DAILY.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** A2's controls: an unweighted weekly-average reclaim and a 5-day-SMA reclaim; K-DR. **Kill (A2):** generic criteria plus the control.

#### A2-15 · Breakout-leg POC vs leg AVWAP skew (layer on PS-01)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS/AV · A2 (Coles & Hawkins MIDAS vs volume-at-price centre); cousin of VW-06 and Levy's P-profile skew.
- **Evidence:** **H/E** · untested · Lit: Book/A2 · **original A2 grade: C**.
- **Markets · TF:** host PS-01 events.
- **Data · test window:** 1m · W-M (Jun 2016→Oct 2026, ≈2,500 sessions; agreed split §2.0.1)
- **Why it might work:** If the leg's AVWAP sits above its POC (skew > +0.3 ATR5), buyers kept paying up through the leg, which is initiative rather than responsive buying.
- **Rules:** layer: skew = (leg AVWAP − leg POC)/ATR5; test skew > +0.3 as "favourable" and |skew| ≤ 0.2 as "neutral".
- **Sweep:** threshold ∈ {0.3, 0.5} → 2.
- **Needs:** ✓ M-AVWAP (leg anchor = Swing/Breakout), ✓ range profile (`_RangeProfile`, PR #18).
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Test:** ablation on PS-01 events. **Kill:** lift < 0.03 R.

#### New A2 entries (Rev 2–Rev 5: A2-25…A2-71, not merged)

#### A2-25 · POC bootstrap-stability gate and minimum-stop rule (module)
- **Implementation status (v2.4):** **Partial** — Statistic coded (`_ProfileStats.BootstrapPoc`, PR #18); gate rule / host not coded.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 10.1 (bootstrap POC; "ci_width_ticks = your minimum stop distance"; Drill 10.1). **Grade:** B for the statistics, C for the trading lift.
- **Evidence:** **B for the statistic (Keller method) · H for the trading lift** · **original A2 grade: B for the statistics, C for the trading lift** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily statistic, host entry TF
- **Data · test window:** 1m · W-M (agreed split §2.0.1); prior-session statistic only; tick rebuild re-check inside W-T
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** inherits the hosts (every POC-referenced trade is classified)
- **Fails when:** POC stability merely proxies range or volume (then it is a volatility filter); the widened stop pushes R:R below 1
- **Needs:** ✓ M-GMM `_ProfileStats.BootstrapPoc` (PR #18) (block bootstrap of 1m bars, 500 draws), F-PROF, F-JRNL
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**; hosts PS-01/PS-02, A2-11, PS-10/A2-18, A2-15
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **What it is:** a gate and stop rule on every A1/A2 trade whose entry or stop references a prior-session POC (A1 Spec 1/2, A2-11 control, A2-18, A2-15).
  - **Rules (computed after the prior close):**
    - stability ≥ 0.60 → trade allowed; stop distance from the POC = max(the spec's stop, ci90/2 + 1 tick);
    - 0.25 ≤ stability < 0.60 → trade allowed only if the POC zone [POC ± ci90/2] is used as the level (entry trigger measured from the zone edge, stop beyond the far edge + 1 tick);
    - stability < 0.25 → no POC-referenced trade that session.
    - Skip the trade if the widened stop makes the target < 1R.
  - **Timeframes:** daily statistic; the host spec's entry TF. **Parameters:** stability cut-offs {0.5/0.25, 0.6/0.25, 0.6/0.35}; block size {3, 5, 10}.
  - **Test:** ablation on the host specs' event sets (window M). Report expectancy for the high/mid/low stability buckets and the stopped-out-then-target-hit rate with the original vs widened stop.
  - **Kill:** high-minus-low bucket expectancy < 0.05R, or the gate removes > 50% of trades without lift.
- **Overlap · Keller B5 (v2.3, report-only):** PS-25 (KD-01) checks POC stability with this gate before using the POC as an event rail (§3.1, §3.16). No P&L pick.


#### A2-26 · KDE bandwidth-sweep veto on FLAT/BIMODAL POCs (module)
- **Implementation status (v2.4):** **Partial** — Statistic coded (`_ProfileStats.KdeSweep`, PR #18); gate rule / host not coded.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 10.2 (bandwidth-sweep diagnostic; "four-minute habit"; Drill 10.2). **Grade:** A for the estimator, B/C for the trading lift.
- **Evidence:** **A for the estimator (method) · H for the lift** · **original A2 grade: A for the estimator, B/C for the trading lift** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily statistic
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** inherits the hosts
- **Fails when:** flags overlap > 70% with A2-25 (then keep one)
- **Needs:** ✓ M-GMM `_ProfileStats.KdeSweep`, `EKdeVerdict` (PR #18) (KDE sweep, 30 bandwidths), F-HVN
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Rule:** before any trade referencing the prior-session POC, take the prior session's sweep verdict:
    - STABLE → allowed (host rules unchanged);
    - FLAT → veto (no level);
    - BIMODAL → veto POC trades and route to A2-27/A2-28 (the platform POC may sit in a void).
    - Add a disagreement flag: |MC VP POC − cross-validated KDE POC| > 4 ticks counts as non-STABLE.
  - **Test:** same host event sets as A2-25, window M. Compare with A2-25; keep only one if the two flags overlap > 70%.
  - **Kill:** STABLE-minus-(FLAT ∪ BIMODAL) expectancy < 0.05R.
- **Overlap · Keller B5 (v2.3, report-only):** PS-25 (KD-01) reuses this bimodal/flat veto; KD-09 (RG-09) is complementary — both may run as gates, reported separately. No P&L pick.


#### A2-27 · Bimodal-profile void traverse
- **Implementation status (v2.4):** **Partial** — Mixture components coded (`_Gmm`, PRs #21/#24) and used as a Gate0 level family; trade rule not coded.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B2 Ch 10.4 ("void is a velocity zone… target across the void to the far μ, do not target into it"; Drill 10.4). **Grade:** B (mechanism) / C (trade).
- **Evidence:** **D (mechanism) · H (trade)** · **original A2 grade: B (mechanism) / C (trade)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ, CL/GC robustness; prior-session mixture, 5m entry 09:45–14:30
- **Data · test window:** 1m · W-M (agreed split §2.0.1); prior-session profile only (roll affects only the first session after a splice)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 1–3 per month per market (needs K = 2 with both π ≥ 0.30)
- **Fails when:** the void is not faster than the components (Keller B4 Ch 2.3 found no speed difference for LVNs on FX/crypto); single-profile VA geometry does as well
- **Needs:** ✓ M-GMM `_Gmm` + `ProfileMixture` (PR #21/#24) (volume-weighted GMM, BIC), F-PROF, F-TM
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs** (mixture components are in A2-33's level list)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Setup (prior session, after the close):** BIC selects K = 2; both π_k ≥ 0.30; void depth ≥ 0.5; |μ2 − μ1| ≥ 1.0×ATR(14, 30m). Define component edges E1 = μ1 + σ1 (lower component, upper edge) and E2 = μ2 − σ2 (upper component, lower edge).
  - **Entry (long, today RTH, 09:45–14:30):** price is in or below the lower component, then 2 consecutive 5m closes above E1 (acceptance into the void). Enter at the next 5m open.
  - **Stop:** μ1 − 1 tick, capped at 1.0×ATR(14, 5m) from entry; skip if target < 1.2R.
  - **Target:** μ2 (the far component centre); T1 50% at E2. Time stop: 90 minutes or 15:55.
  - **Filter:** skip if price opened inside the void (no component of origin). Mirror short from the upper component.
  - **Parameters:** π floor {0.25, 0.30, 0.35}; acceptance closes {1, 2}; edge = μ ± {0.5, 1.0}σ.
  - **Test:** event study first: void traverse speed (ticks/minute) vs component traverse speed, normalised by region width (Drill 10.4). Strategy only if void speed ≥ 1.5× component speed with a session-bootstrap CI excluding 1. Control: same rule using the prior VA edges and VAH/VAL midpoint (single-profile geometry).
  - **Kill:** generic criteria, or no lift over the single-profile control.
- **Overlap · Keller B5 (v2.3, report-only):** PS-25 (KD-01) reuses this bimodal/LVN void veto. No P&L pick.


#### A2-28 · Minor-component excess rejection (confirmation, not touch)
- **Implementation status (v2.4):** **Partial** — Entropy statistic coded (`_ProfileStats`, `_Gmm`, PR #18/#21); rule not coded.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B2 Ch 10.4 ("π = (0.75, 0.25): the minor cluster is likely an excess that will be rejected → its edge is a fade candidate"). **Grade C.**
- **Evidence:** **H** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; prior-session mixture, 5m confirmation, 1m trigger
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 1–2 per month per market
- **Fails when:** trend days (disabled by A2-16/RG-04 state); the VA-edge version (PS-03 geometry) does as well
- **Needs:** ✓ M-GMM `_Gmm` + `ProfileMixture` (PR #21/#24), F-PROF, RG-04 state
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Setup:** prior session K = 2 with π_minor ≤ 0.30 and the minor component at a session extreme (above the major for a short, below for a long).
  - **Entry (short case):** today trades into the minor component (above μ_minor − σ_minor), then 2 consecutive 5m closes back below μ_minor − σ_minor; enter on a 1m close below the low of the second 5m bar. (A touch alone is never an entry.)
  - **Stop:** today's high since entering the minor component + 1 tick, capped at 1.2×ATR5.
  - **Target:** μ_major (T1 50% at the void minimum). Flat by 15:55.
  - **Filter:** disabled on A2-16 trend-day state and in a negative-gamma regime (A2-36) if that filter survives.
  - **Test:** compare with the same confirmation rule at the prior VAH/VAL (A1 Spec 2 geometry). Window M.
  - **Kill:** generic criteria, or no lift over the VA-edge version.
- **Overlap · Keller B5 (v2.3, report-only):** PS-25 (KD-01) uses the same rail roles as HVN/LVN (detrended manifold nodes). No P&L pick.


#### A2-29 · Entropy day-type gate at 11:00 (module)
- **Implementation status (v2.4):** **Partial** — Statistic coded in `_ProfileStats` (PR #18); rule not coded.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 10.3 (H_norm/n_eff day-type map; Heresy 6; Drill 10.3). **Grade:** A descriptive, C for the gate.
- **Evidence:** **B (descriptive) · H (gate)** · **original A2 grade: A descriptive, C for the gate** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; developing RTH profile 09:30–11:00
- **Data · test window:** 1m · W-M (agreed split §2.0.1); 250-session percentile warm-up
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every session classified; most sessions are 'no change' by design
- **Fails when:** duplicates RG-04/A2-16 (kept only if it adds beyond it); entropy just tracks volume
- **Needs:** ✓ M-GMM `_ProfileStats.Entropy` (Miller–Madow) + `_EntropyTracker` (PR #18) (Miller–Madow entropy, n_eff), F-DEV; relation: RG-02 uses completed-composite entropy > 0.93, A2-29 uses the developing percentile
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Gate (v2):** none (classifier); host lift is measured after the hosts pass
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Feature at 11:00 ET:** developing RTH H_norm and n_eff (09:30–11:00), as percentiles versus the same-time values of the trailing 250 sessions.
  - **Rule:**
    - H_norm pct ≥ 80 → "trend/dispersed" state: disable A1 fade/control ideas and A2-28; allow continuation ideas only.
    - H_norm pct ≤ 30 and n_eff pct ≤ 30 → "balance" state: allow value-edge confirmation trades; disable breakout entries until 13:00.
    - Otherwise: no change (ambiguous sessions are the common case; do not force a call).
  - **Test:** (1) classifier check: precision for the locked trend-day definition used in A2-16; (2) PnL ablation on pooled A1/A2 events (window M); (3) compare with A2-16 (sVWAP slope); keep the better one or the AND.
  - **Kill:** precision < 55%, or PnL lift < 0.03R, or no lift beyond A2-16.
- **Overlap · Keller B5 (v2.3, report-only):** entropy here is the same statistic as M-DVP manifold entropy (§2.4b); report the correlation with PS-25 (KD-01) conditions. No P&L pick.


#### A2-30 · Rising-entropy balance break (leading signal)
- **Implementation status (v2.4):** **Partial** — Statistic coded (`_ProfileStats`, `_RangeProfile`, PR #18); rule not coded.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B2 Ch 10.3 ("balance about to break shows entropy beginning to rise before price leaves the range"; grade B, untested). **Grade B/C.**
- **Evidence:** **D/H** · **original A2 grade: B/C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 2-session balance, 30-min rolling entropy sampled every 5m, 5m entry
- **Data · test window:** 1m · W-M (agreed split §2.0.1); trailing 60-session same-time percentile
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 1–3 per month
- **Fails when:** 'entropy rose' just means 'volume rose' (volume-surge control); plain breakout does as well
- **Needs:** ✓ M-GMM `_EntropyTracker` (same-slot percentile rings, PR #18) (rolling entropy), M-TOD (same-time percentile), F-TM
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Setup:** a balance = the prior 2 RTH sessions' combined range ≤ 1.2×ATR(14, daily) and today's price inside that range at 10:30.
  - **Signal:** rolling 30-minute H_norm of today's profile, sampled every 5 minutes; H_slope = 15-minute change. Alert when H_slope > its 90th percentile (trailing 60 sessions, same time of day).
  - **Entry:** within 60 minutes of the alert, the first 2 consecutive 5m closes outside the balance range; enter at the next 5m open in that direction.
  - **Stop:** back inside the range by 0.25×range, capped at 1.0×ATR(14, 30m).
  - **Target:** 1.0× balance range from the broken edge (T1 50% at 0.5×). Trail on a 5m close back inside. Flat 15:55.
  - **Control:** the same breakout without the entropy alert, and with a volume-surge alert of the same frequency (the Miller–Madow caveat: "entropy rose" must not just mean "volume rose").
  - **Kill:** no lift over both controls.
- **Overlap · Keller B5 (v2.3, report-only):** entropy here is the same statistic as M-DVP manifold entropy (§2.4b); report the correlation with PS-25 (KD-01) conditions. No P&L pick.


#### A2-31 · KL-divergence acceptance/migration gate for prior-day levels (module)
- **Implementation status (v2.4):** **Partial** — Statistic coded in `_ProfileStats` (PR #18); rule not coded.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 10.3 (D_KL(today‖yesterday): < 0.15 acceptance, > 0.60 migration; downweight old levels after migration) and 10.6 ("levels expire with regime change, not age"). **Grade C.**
- **Evidence:** **H** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; developing vs prior RTH profile at 11:00/12:00
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every session classified
- **Fails when:** no difference in level reaction across D_KL buckets
- **Needs:** ✓ M-GMM `_ProfileStats.KL` / `KLKde` / `KLKdePrior` (PR #18) (KL on a 1-tick grid), F-DEV, F-PROF
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs** (its event-study arm uses the A2-33 design)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Feature at 12:00 ET:** D_KL between today's developing RTH profile (09:30–12:00) and the prior session's full RTH profile, on a common 1-tick grid with ε-smoothing (1e-6).
  - **Rule:**
    - D_KL < 0.15 → acceptance: prior VAH/VAL/POC trades allowed for the afternoon.
    - D_KL > 0.60 → migration: disable all prior-day-level trades (A1 Spec 2, A2-11, A2-28) for the session; allow continuation in the direction (today's POC − yesterday's POC).
    - Between → no change.
  - **Parameters:** thresholds {0.10/0.50, 0.15/0.60, 0.25/0.80}; decision time {11:00, 12:00}.
  - **Test:** afternoon-trade ablation (window M). Also an event study: afternoon reaction at the prior VA edges vs matched control prices (A2-33 design), split by D_KL bucket.
  - **Kill:** lift < 0.03R, or no difference in level reaction across buckets.
- **Holm · Keller B5 (v2.3):** member of the Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, α = 0.05, fixed before any dev read. **Tie-break with PS-25 (KD-01) at > 70% trade overlap:** dev data only, net expectancy after A2-40/A2-59, frozen at sign-off (the only declared P&L choice); m stays 5 either way (§3.16).


#### A2-32 · Volatility-normalised (σ-space) mixture composite levels
- **Implementation status (v2.4):** **Partial** — σ-mixture coded (`SigmaMixture`, PR #24) and used as a Gate0 family; rule not coded.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B2 Ch 10.5 (σ-normalised profiles; tick-space composites are biased toward quiet days; Heresy 7) and 10.4 (fit a mixture to the composite; Drill 10.5). **Grade:** B (method) / C (trade).
- **Evidence:** **D (method) · H (trade)** · **original A2 grade: B (method) / C (trade)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 20-session σ-space composite, 15m acceptance
- **Data · test window:** 1m · W-M (agreed split §2.0.1); multi-session statistics reset at roll splices (A2-39): the 20-session window must lie inside one contract (≈ ⅓ of each ES quarter has no full window unless seeded)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–4 per month
- **Fails when:** σ-levels do not beat tick-space composite levels in the A2-33 level study
- **Needs:** ✓ M-GMM `_ProfileStats.SigmaMixture` (PR #21/#24), F-COMP, ✓ Parkinson σ (`ParkinsonSessionPrice`, PR #18)
- **Readiness (v2.3b):** Buildable now on the merged base; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Construction (after each close):** for each of the last 20 sessions (no roll inside the window), map 1m volume to z = (p − sVWAP_session)/σ_session, σ = Parkinson σ of that session's 5m bars (≥ 60 observations). Pool into a σ-space composite, fit GMM (K ≤ 4, BIC). Translate components to price for tomorrow as level_k = prior-session VWAP + z_k × σ̂, with σ̂ = EWMA of the last 10 sessions' σ (known at the close).
  - **Trade rule (acceptance breakout, both directions):** 2 consecutive 15m closes beyond a component edge (z_k ± 1σ_k, in price) after 10:00 → enter at the next 15m open toward the next component centre. Stop: back inside the edge by 0.3×ATR(14, 30m). Target: next μ (≥ 1.2R, else skip). Flat 15:55.
  - **Control:** the identical rule on the tick-space 20-day composite POC/VA (A1 levels).
  - **Test:** first the level study (A2-33 design) for σ-composite components vs tick-composite POC/VA; then the strategy. Window M.
  - **Kill:** σ-levels do not beat tick levels on the level study, or strategy lift < 0.05R over the control.
- **Overlap · Keller B5 (v2.3, report-only):** the closest overlap with PS-25 (KD-01) — both use σ-space levels; report the trade overlap. No P&L pick.


#### A2-34 · V/T urgency-price revisit (event study, then target module)
- **Implementation status (v2.4):** **Partial** — V(p)/T(p) coded (`_ProfileStats`, PR #18); event study not coded.
- **Family · Source:** EX module · Analyst 2 Rev 6 · Keller B2 Ch 10.7 (V/T intensity = volume per unit time at price; high V/T = a forced participant with a deadline; "urgency prices are revisited"; Drill 10.7). **Grade C** (author's own untested proposal).
- **Evidence:** **H** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily statistic, used as a runner target only
- **Data · test window:** 1m · W-M (agreed split §2.0.1); tick-time T(p) re-check inside W-T
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** 3 V/T prices per session; used only as targets
- **Fails when:** revisit/reaction not above the volume POC, TPO POC and matched controls
- **Needs:** ✓ V(p)/T(p) (`_ProfileStats.VtPrices`, PR #18), F-PROF
- **Readiness (v2.3b):** Buildable now on the merged base. *(v2.3a's line wrongly said "entropy helper"; the real need was V(p)/T(p).)*
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Feature (after each close):** at each 1-tick row, V(p) = volume at price and T(p) = minutes at price (1m bars touching p). V/T = V/T. Take the top-3 V/T prices that are ≥ 0.5×ATR30 from the volume POC and the TPO POC.
  - **Event study:** probability of revisit within 5 sessions and the reaction on first revisit (A2-33 metric), vs the volume POC, the TPO POC and matched controls.
  - **Trade (only if the event study passes):** treat the top V/T price as a target for A1/A2 runners (A2-19 style), never as an entry.
  - **Data:** window M; re-check inside window T with tick-time T(p).
  - **Kill:** revisit/reaction not above controls with Holm-corrected p < 0.05.

#### A2-35 · Macro-event re-formation value area (CPI/NFP 08:30, FOMC 14:00)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** TS (time/session) · Analyst 2 Rev 6 · Keller B2 Ch 14.4 ("watch the rebuild, not the spike… trade the edges of THIS [the 20-minute post-event profile], not the extremes of the spike"; T+0 do nothing). **Grade:** B (mechanism) / C (trade).
- **Evidence:** **D (mechanism) · H (trade)** · **original A2 grade: B (mechanism) / C (trade)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ pooled as one family; T+1..T+20 1m profile, 5m entries
- **Data · test window:** 1m · W-M (agreed split §2.0.1) + ETH bars for 08:30 releases (verify E5); event calendar back to 2016
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** A2 estimate: ≈ 290 events per market over ~9 years (12 CPI + 12 NFP + 8 FOMC per year); ≈ 32 per year
- **Fails when:** the re-formation VA behaves like any opening-range profile (non-event control); FOMC press-conference second leg
- **Needs:** ◐ M-EVENT (CPI/NFP/FOMC scheduled times, M-EVENT PRs #25/#27/#28/#29), F-DEV on an arbitrary window (✓ `_RangeProfile`, PR #18), F-TM
- **Readiness (v2.3b):** Buildable now on the merged base (scheduled events only); run gated by A2-33 verdict (harness ◐; Gate-0 not yet run). Caveat: needs ETH bars. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**; shares the event slot with AV-06 (A2-20/21) and TS-05
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Construction:** at T+20 minutes after the release (08:50 for CPI/NFP; 14:20 for FOMC), build the profile of the 1m bars from T+1 to T+20. New POC, new VAH/VAL (70%).
  - **Entries (decided only after T+20; nothing before):**
    - **Acceptance break:** 2 consecutive 5m closes outside the re-formation VA → enter at the next 5m open in the break direction. Stop: back inside the VA by 0.3×(VAH − VAL). Target: 1.0× VA height beyond the edge (T1 50% at 0.5×). Time stop 90 minutes (CPI/NFP: hold into RTH allowed, flat by 11:30; FOMC: flat 15:55).
    - **Failed break:** a 5m close outside the VA followed within 2 bars by a 5m close back inside → enter toward the opposite edge on the next 1m close in that direction. Stop: the outside extreme + 1 tick. Target: new POC (T1), opposite VA edge (T2).
  - **Filter:** skip if the T+1..T+20 range < 1.0×ATR(14, 5m) of the prior 5 sessions (no event), or if the 08:30 data for CPI/NFP sits in ETH and 1m volume for the window < 30% of its 60-day mean (thin).
  - **Sample:** ~9 years × (12 CPI + 12 NFP + 8 FOMC) ≈ 290 events per market; pool ES/NQ (count as one family).
  - **Spike-reversion arm:** the book's revert_frac forecast needs pre-event depth (L2) → Appendix A. A price-only check is allowed: fraction of the T+0..T+2 spike retraced by T+30, reported descriptively.
  - **Control:** the same rules on the opening-range (09:30–09:50) profile of non-event days at matched times.
  - **Kill:** generic criteria per arm, or no lift over the non-event control.

#### A2-36 · Dealer-gamma regime filter (module; data-blocked)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 14.2 (long gamma: fades work, breakouts fail, tighter stops; short gamma: do not fade, continuation works, widen stops, reduce size; "treat the flip as a zone, never a line"; Drill 14.2). **Grade:** B for the regime read, C for any specific flip level.
- **Evidence:** **D (regime read) · H (flip level)** · **original A2 grade: B for the regime read, C for any specific flip level** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily state at 09:30
- **Data · test window:** W-M ∩ external GEX history (not in dxFeed); VIX9D/VIX proxy fallback reported as a proxy
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every session
- **Fails when:** no GEX history can be sourced (then only the VIX proxy runs); the 0DTE era changes the regime
- **Needs:** ✗ M-GEX (deferred; external daily file via QuoteManager ASCII), F-TM
- **Readiness (as of merge):** Not yet buildable as specified — still needs: M-GEX (deferred; external daily file via QuoteManager ASCII).
- **Gate (v2):** data dependency first
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Data dependency:** **not available from dxFeed in MC.** Needs a daily external aggregate GEX series published before the RTH open (e.g. a vendor GEX/flip file imported as an ASCII daily series in QuoteManager). Use only the value known at 09:30 (previous evening's publication). Record the vendor's dealer-sign convention.
  - **Rule (decided at 09:30):**
    - GEX > 0 and spot ≥ flip × 1.003 → **long-gamma state**: allow A1/A2 confirmation fades (A2-28, Spec 2, A2-13 Branch B); disable A2-27/30/32 breakouts; host stops unchanged.
    - GEX < 0 or spot ≤ flip × 0.997 → **short-gamma state**: disable all fade/mean-reversion ideas; allow continuation/breakout ideas; stops × 1.25; size × 0.5.
    - Inside the ±0.3% flip zone → neither state; host rules with size × 0.5.
  - **Fallback proxy (if no GEX history):** VIX9D/VIX < 0.9 and VIX < 1-year median as a long-gamma proxy; report it as a proxy, not gamma.
  - **Test:** Drill 14.2 on pooled A1/A2 events, window M ∩ GEX history: win rate and expectancy of fade vs continuation setups by regime, and average excursion beyond the stop. Note the 0DTE era (2022+) as its own sub-sample.
  - **Kill:** the fade-minus-continuation difference does not flip sign across regimes, or filtered pooled expectancy lift < 0.03R.

#### A2-37 · Cross-instrument dislocation fragility filter (module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B2 Ch 14.1 ("persistent wide dislocation = arbitrage capital has withdrawn = fragility signal, NOT an opportunity"; not the level, the persistence). **Grade B.**
- **Evidence:** **D** · **original A2 grade: B** (rescaled per §3.12).
- **Markets · TF:** ES vs NQ and ES vs YM, 1m
- **Data · test window:** 1m · W-M (agreed split §2.0.1); hedge ratio from the prior 20 sessions; multi-session statistics reset at roll splices (A2-39)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** persistent-dislocation state on a minority of sessions (author estimate)
- **Fails when:** no reduction in tail loss and no expectancy lift
- **Needs:** ✓ M-XMKT (`_XmktLeg` per stream), F-JRNL
- **Readiness (as of merge):** Buildable now on the merged base (run MC Probe P2 first).
- **Gate (v2):** none (filter on surviving hosts)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Pair:** ES vs NQ, and ES vs YM (all dxFeed, 1m). Hedge ratio h = rolling 20-session OLS of 1m log returns (known at the prior close).
  - **Feature:** spread = ln(ES) − h·ln(B); z vs a rolling 500-bar window; wide = |z| > 2.5; persistent = share of the last 60 bars wide > 0.60.
  - **Rule:** while persistent on either pair → no new fade entries; size × 0.5 for continuation entries; stops × 1.25. Never trade the spread itself.
  - **Test:** ablation on pooled A1/A2 events (window M). Also check the stop-slippage proxy (bar range at stop-out) by state.
  - **Kill:** no reduction in tail loss (worst 5% of trades) and no expectancy lift.

#### A2-38 · Metaorder drift completion (tick window only)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** OF (order flow) · Analyst 2 Rev 6 · Keller B2 Ch 13.1–13.2 ("most volume is a schedule"; front-loaded flow decays, flat/TWAP-like flow persists; the drift that "goes and goes for no reason, then stops and gives it back"; null test on shuffled timestamps, Drill 13.2). **Grade C** (author: detection is hard, use as an interpretive frame first).
- **Evidence:** **H** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 20-minute windows stepped by 5 minutes on classified ticks
- **Data · test window:** 1-tick + Bid/Ask · W-T only (250/125/125-session split, §2.0.1); thresholds fixed on the first 250 sessions
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: a few per month at most; A2 requires ≥ 60 events in W-T
- **Fails when:** detector rate on shuffled timestamps is as high as on real data; too few events
- **Needs:** ◐ M-TICK (inter-print times ✓; trade-size entropy ✗ gated by §E6 and not computed), F-OF tier ≥ 3, λ̂ regression helper
- **Readiness (as of merge):** Not yet buildable as specified — still needs: trade-size entropy (gated by §E6, not computed in `_TickStream`) and a λ̂ regression helper; inter-print times ✓ (M-TICK).
- **Gate (v2):** mandatory shuffled-timestamp null first
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Feature (window T, 20-minute windows stepped by 5 minutes, Ask/Bid-classified ticks):** for the dominant side, inter-print CV, normalised trade-size entropy, and 1-minute participation stability; machine_like = (CV < 0.45) + (H_norm < 0.55) + (stability > 0.80). Muted impact = price change below the expected λ̂·OF (λ̂ from a rolling 20-session regression).
  - **Setup:** ≥ 60 minutes of one-sided drift (≥ 80% of 5m closes in one direction, no scheduled event) with machine_like ≥ 2 for ≥ 3 consecutive windows, then machine_like drops to 0 and the classified one-sided imbalance falls below its median (completion).
  - **Entry:** after completion, the first 5m close back through the drift's last 5m swing → enter against the drift at the next 5m open.
  - **Stop:** the drift extreme + 1 tick. **Target:** 50% retracement of the drift (T1), drift AVWAP (T2). Time stop 60 minutes.
  - **Mandatory null:** run the detector on shuffled timestamps; proceed only if real-data detection rate ≥ 2× the shuffled rate. Thresholds fixed on the in-sample 250 sessions.
  - **Kill:** null test fails, fewer than 60 events in window T, or generic criteria.

#### A2-43 · Confluent-HVN level gate (target and veto module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Keller B4 Ch 6.2 ("an HVN with no independent confirmation is one of ~six candidates, not a level"; HVN rejection ratio 0.91 FX / 0.82 crypto, <1; θ unreported; Ch 8.1 measures confluence). **Grade:** C (practitioner, and the book's own HVN numbers are negative for rejection trades).
- **Evidence:** **E (and the book's own HVN rejection ratios are < 1)** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 20-session composite HVNs with intraday confluence
- **Data · test window:** 1m · W-M (agreed split §2.0.1); multi-session statistics reset at roll splices (A2-39)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** inherits the hosts
- **Fails when:** reaction does not rise with confluence count C; works at only one θ
- **Needs:** F-HVN, F-COMP, F-VWAP bands, F-PROF single prints, A2-25
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs** (C buckets are scored with the A2-33 design)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** PS-22 (HVN ceiling veto on composite HVNs, no confluence requirement), VW-02 (2σ fade at an HVN edge), A2-33 (generic level study). This is a **definition gate** reused by the other ideas, plus a level study. It is **not** a touch-fade entry (the book's rejection family has no viable stop).
  - **Definition (computed from closed bars only):** a candidate HVN is a peak in the 20-session composite with v_i > θ×median(v). It is **confluent** if within ±δ (δ = 2 ticks ES, 6 ticks NQ) of at least one of:
    1. the current sVWAP ±1σ or ±2σ band (A2 §1 bands, ≥ 30 minutes into the session);
    2. a 5-session composite HVN (a different window from the 20-session one);
    3. the edge of a single-print shelf from the last 5 sessions;
    4. the prior-session POC that passes A2-25 stability.
    Confluence count C ∈ {0..4}.
  - **Uses:**
    - **(a) Target module:** for A2-41/44/45/46 runners and A1 continuation specs, the first confluent HVN (C ≥ 1) in the trade direction is a 50% scale point (−1 tick). Non-confluent HVNs are ignored.
    - **(b) Veto module (ablation of PS-22):** skip a continuation entry whose 1R target lies beyond a confluent HVN with C ≥ 2.
  - **Timeframes:** daily composite levels; intraday bands updated every closed 1m bar.
  - **Parameters:** θ ∈ {2, 3, 4} (the book's proposed falsification sweep); C threshold ∈ {1, 2}.
  - **Test window:** M.
  - **Tests:**
    1. A2-33 level study by C bucket (0, 1, ≥2) vs matched controls: does the reaction/stall rate rise with C? Plot reaction vs θ.
    2. Host ablation: host expectancy with (a), with (b), and with neither. Count the VWAP-band and POC confluences as one family (A2 caveat §4).
    3. Stratify by whether the HVN formed in transit (60m trend state up/down while it built) vs in balance (book's untested hypothesis).
  - **Kill:** C ≥ 2 reaction rate within the CI of C = 0, **or** no host improves by ≥ 0.03R, **or** the result appears at only one θ.

#### A2-45 · Single-print shelf as destination (near-edge target, runner to far edge)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B4 Ch 6.5 ("the one level with no free parameter"; "a destination, not a barrier… take the near edge, leave a runner for the far edge"; shelf width as a graded signal is untested). FX ratio 1.66, crypto 0.70 (the asset classes disagree). **Grade:** C.
- **Evidence:** **H** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 30m TPO levels, 15m entry, 5m management
- **Data · test window:** 1m · W-M (agreed split §2.0.1); shelves ≤ 5 sessions old (roll affects the first sessions after a splice)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–4 per month
- **Fails when:** shelf fill curves inside the matched-control CI (the book has no control); FX and crypto ratios disagree (1.66 vs 0.70)
- **Needs:** F-PROF (SinglePrints), F-VWAP, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** PS-20 (pullback *defending* an intraday shelf; shelf = support) and MT-06 (fading an excess tail). Here the shelf is a **target to trade toward**.
  - **Timeframes:** 30m TPO levels; 15m entry; 5m management.
  - **Setup:** an unfilled shelf (≥ 4 rows, see conventions) from the last 1–5 RTH sessions. Its near edge E_n (the edge closest to current price) is between 0.5 and 2.5×ATR(14, 30m) from price at the decision bar. Width S = rows × tick. The shelf must be unfilled: no RTH row inside it traded since it was created.
  - **Entry (long toward a shelf above):** after 10:00 ET, the first 15m close above the prior 15m bar's high **and** above sVWAP. Buy at the next 15m open. One trade per shelf. Mirror for shelves below.
  - **Filter:** VA-midpoint migration not against the direction; skip if a confluent HVN (A2-43, C ≥ 2) lies between price and E_n.
  - **Stop:** min(the low of the last 2 closed 15m bars, entry − 1.0×ATR(14, 15m)) − 1 tick.
  - **Target:** T1 = 60% at E_n − 1 tick; runner to the far edge E_f − 1 tick; stop to entry after T1. Flat 15:55. Skip if T1 < 1.0R.
  - **Width study:** S buckets {4–7, 8–15, ≥ 16 rows}. Report fill probability within the session and within 3 sessions, by bucket (the book's graded-signal hypothesis).
  - **Parameters:** age ∈ {1, ≤ 5 sessions}; T1 share ∈ {50, 60}% → 4, plus the width buckets as a report, not a sweep.
  - **Test window:** M.
  - **Tests:** (1) Event study: Kaplan–Meier fill curves for shelf near edges vs matched control prices at the same distance (A2-33 design). The book itself lacks this control. (2) Strategy vs the same entry with targets at control prices (K-FL). (3) Partial-fill rate: P(far edge | near edge filled).
  - **Kill:** shelf fill curve inside the control CI; **or** the strategy's lift over K-FL < 0.05R; **or** the width gradient is non-monotonic (then drop width as a signal).

#### A2-46 · Volume-gap commitment entry to the far node
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** PS (profile structure) · Analyst 2 Rev 6 · Keller B4 Ch 6.6 ("enter on commitment, price closed inside the gap, not on approach… one bar of patience = thirty-fold geometry difference"; far node = destination, not stopping point; median excursion ~8× node distance in exhibits). Ratio 27 / 23 (exhibits, selected); destination control missing. **Grade:** C.
- **Evidence:** **E (exhibit ratios, selected)** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 10-session composite, 5m entry 09:45–14:30
- **Data · test window:** 1m · W-M (agreed split §2.0.1); multi-session statistics reset at roll splices (A2-39)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–4 per month
- **Fails when:** far-edge hit rate within the non-gap control CI; trades overlap > 70% with A2-27
- **Needs:** F-HVN, F-COMP, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** **A2-33 verdict for its level type must be positive before this runs**
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-27 (prior-session two-component mixture void, 2 closes past the component edge) and PS-15 (a single LVN, entry at the midpoint). A2-46 uses **multi-session composite gaps between two KDE HVNs**, enters on the **first close inside the gap**, and targets the **far node** with a runner. If its trades overlap > 70% with A2-27's, keep only the better one.
  - **Timeframes:** 10-session composite; 5m entry.
  - **Gap definition (prior close):** two adjacent composite HVN peaks P_lo < P_hi with every row between them below 0.35× the smaller peak. Gap G = [P_lo edge, P_hi edge], where an edge is the row where density falls below 50% of its peak. Require |G| ≥ 1.0×ATR(14, 30m).
  - **Entry (long):** price comes from the lower node, and a closed 5m bar closes ≥ 0.15×|G| inside G above its lower edge. Buy at the next 5m open. 09:45–14:30 ET. Mirror short.
  - **Stop:** lower gap edge − 0.25×ATR(14, 5m).
  - **Target:** T1 50% at the upper gap edge − 1 tick (far node start); T2 25% at P_hi; runner trails at the far gap edge once price is beyond P_hi (exit on a 30m close back inside G). Time stop: end of session + 1 (not quick, per the book: "a real destination, but not quickly").
  - **Parameters:** commitment depth ∈ {0.10, 0.15, 0.25}×|G|; gap floor ∈ {0.35, 0.25}× peak → 6.
  - **Test window:** M.
  - **Tests:** (1) Destination control (the book's gap): P(reach far edge | committed) vs P(reach an equally distant arbitrary price | committed into a non-gap range of equal width). (2) Approach-entry arm (limit at the near edge) to measure the book's "one bar of patience" claim on CME. (3) Speed: bars per row inside the gap vs in nodes (expect a negligible difference; it is not required for the idea).
  - **Kill:** far-edge hit rate within the control CI; **or** commitment arm ≤ approach arm; **or** generic criteria.
- **Overlap · Keller B5 (v2.3, report-only):** KD-10 (PS-25 node arm) is near this entry; the trade overlap is reported descriptively — **no P&L pick, nothing dropped** (rev 3.1; the "keep only the better one" rule is not applied to KD-10).


#### A2-48 · Poor highs/lows as destinations (exit and stop-placement module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** EX module · Analyst 2 Rev 6 · Keller B4 Ch 7.5, 7.6 ("treat a poor high as context/target, not signal/entry"; "if long and poor high above, that's where to reduce"; the unfinished extreme against you is a plausible stop-run location; ~half of all profiles have one; the 9.7 ratio is contaminated by co-tags). **Grade:** C. **Prior independent evidence is negative for the magnet:** Perfiltrade found poor highs revisited 11.9% vs 12.9% for controls (cited in A1 PS-19), so the expected prior is ~0.
- **Evidence:** **B (0) prior for the magnet (Perfiltrade 11.9% vs 12.9% controls) · H for the module** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** hosts A2-41/44/45/46, PS-01/PS-02
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** inherits the hosts
- **Fails when:** isolated poor-extreme revisit rate equals normal extremes
- **Needs:** F-PROF (IsPoorHigh/Low), F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** hosts must pass first
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** PS-19 (run-through **entry** after the break of a poor high) and MT-06 (excess-tail fade). This module never opens a trade.
  - **Rules (applied to open A1/A2 positions):**
    - **Reduce rule:** if an unrepaired poor high (long) or poor low (short) from the last 1–5 RTH sessions lies between 1.0R and 3.0R from entry, take 50% off at that level − 1 tick (long) / + 1 tick (short). The rest follows the host's rules.
    - **Stop-placement rule:** if the host stop lies within 0.25×ATR(14, 5m) beyond an unfinished extreme *against* the position, move the stop to that extreme ∓ 0.25×ATR5 (beyond the likely stop-run). Skip the trade if the new R makes the host target < 1R.
    - "Unrepaired" = price has not traded through the extreme since it was created (checked on closed bars).
  - **Isolation (to handle the co-tag problem):** classify each poor extreme as **isolated** (the session was not a balance-break day per A2-41 and not a VA-migration day) or **co-tagged**. Report each separately.
  - **Hosts:** A2-41, A2-44, A2-45, A2-46, A1 Spec 1 / PS-01 / PS-02.
  - **Test window:** M.
  - **Tests:** (1) Revisit study: P(touch within 1/3/5 sessions) for poor vs normal extremes at matched distance and age, isolated vs co-tagged. (2) Host ablation: reduce rule on/off, stop rule on/off (2×2). (3) Stop-rule check: the stopped-then-target-hit rate with the original vs moved stop.
  - **Kill:** isolated poor-extreme revisit rate within the CI of normal extremes (then drop the reduce rule; the book's 9.7 figure was co-tag contamination); **or** neither rule improves any host by ≥ 0.03R.

#### A2-49 · Midday support/resistance and afternoon breakout (Kaufman TSM)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16, "Variations on Support and Resistance" and "Afternoon Breakouts". ES Jan 2000 – May 2011: **8,404 trades, +$268/trade, no costs, parameters never tuned** (12:00 and 25%). Supporting evidence: Ch 15, the open is about 8× as likely as midday to hold the day's extreme, and a post-midday break raises the odds of a close in that direction. **Grade:** B− (gross, old contract and pit hours).
- **Evidence:** **C (+, gross)** · **original A2 grade: B− (gross, old contract and pit hours)** (rescaled per §3.12).
- **Markets · TF:** ES, NQ/YM/RTY cross-market OOS; 30m (primary), 15m
- **Data · test window:** 1m · W-M (agreed split §2.0.1) (2016–2026 is entirely after the book's Jan 2000–May 2011 sample)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman: 8,404 ES trades over ~11 years (≈ 60 per month, always-in afternoon)
- **Fails when:** costs on a high-turnover always-in system; regime change since the pit era; NQ fails while ES wins (M10)
- **Needs:** F-SESS, F-TM (base exists); SAR handling in the slot
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A1 PS-02/PS-03 (value-area based) and A2-08 (IB extreme AVWAP). Here the levels are the **morning range only**, with a fixed clock, and the system is **always in** during the afternoon (reversal and breakout arms combined).
  - **Timeframes:** 30m (primary, as in the book) and 15m (robustness).
  - **Levels (fixed at 12:00 ET from closed bars):** MH, ML = the 09:30–12:00 high and low. R = MH − ML. Zones: lower zone ≤ ML + 0.25R; upper zone ≥ MH − 0.25R.
  - **Rules (evaluated on each closed bar from 12:00 to 15:30 ET):**
    1. Flat or short, and the bar closes in the lower zone (without having broken ML) → go long (cover).
    2. Flat or long, and the bar closes in the upper zone → go short.
    3. Long, and a bar **trades** below ML → reverse short, using a stop order at ML − 1 tick.
    4. Short, and a bar trades above MH → reverse long at MH + 1 tick.
    5. Arms: **SR** (rules 1–2 only), **BO** (rules 3–4 entered from flat on a first break, as well as reversals), **Both** (book).
  - **Stops:** inherent in the reversal rules. Add a disaster stop at 1.5R from entry, which the book does not have.
  - **Exit:** (a) flat 15:55 (day-only variant); (b) hold overnight and exit at 09:31 next day (book variant: "can greatly increase profits per trade").
  - **Filter (pre-registered, one only):** Kaufman says a trend filter helps rates and FX, not the S&P. Test the arms on NQ unfiltered, and with a daily 20-day slope filter on BO only.
  - **Parameters:** midday ∈ {11:30, 12:00, 12:30}; zone ∈ {20, 25, 33}%; bar ∈ {15, 30}m → 18 combinations, geometric-ish. The book point (12:00, 25%, 30m) is the pre-registered core.
  - **Test window:** M (ES 2017–2026 is fully out of the book's sample, since the book ends in 2011). NQ, YM and RTY serve as cross-market OOS (M10).
  - **Tests:** (1) Exact replication at the core, gross, then net (M4). (2) Arm decomposition SR vs BO vs Both. (3) The surface across all 18 combinations: % profitable, mean − 1 SD (M6). (4) Random-entry control: same clock, same holding period, random side.
  - **Kill:** net $/contract at the core ≤ 0 in validation; **or** fewer than 70% of the 18 combinations profitable net; **or** NQ loses net while ES wins (M10); **or** generic criteria.

#### A2-50 · Same-slot relative-volume opening regime gate (module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Kaufman Ch 16, "Intraday Volume Patterns": higher-than-average early volume relative to the **same bar's** average sets up a more volatile, directional day, and lower volume points to a reversal or drift. Also Ch 12 (U-shape; raw volume rules fire mechanically in the afternoon) and Ch 20 (intraday volume–volatility R = 0.595). No numbers. **Grade:** C.
- **Evidence:** **E** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 09:30–10:00 volume vs same-slot baseline
- **Data · test window:** 1m · W-M (agreed split §2.0.1); 250-session percentile warm-up; 08:30-report days as a separate stratum
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every session
- **Fails when:** the 10:00 range-ratio placebo does as well (it is volatility, not volume)
- **Needs:** ✓ M-TOD (SSB), F-SESS
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** routes hosts; tested after the hosts pass
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** the A2-10 relative-volume bar filter (a single-bar condition on a breakout) and A2-29/A2-16 (entropy or AVWAP-slope day type). This is a **pre-registered session-level gate**, computed once at 10:00 ET, that routes trades to momentum vs reversion hosts.
  - **Timeframes:** 1m volume aggregated over the 09:30–10:00 window; the gate is used for the rest of the session.
  - **Statistic:** RV30 = Σvol(09:30–10:00) / SSB(N = 20) of the same 30-minute window. Not lagged in the default; the lagged-SSB variant is a parameter.
  - **States:** HIGH if RV30 ≥ q_hi; LOW if RV30 ≤ q_lo; else MID. q_hi and q_lo are the trailing 250-session 70th/30th percentiles (rolled daily, causal).
  - **Use (module):** HIGH → allow only continuation hosts (A2-49 BO arm, A2-52, A2-56, A1 Spec 1), and veto fades. LOW → allow only reversion hosts (A2-49 SR arm, A2-51, A2-53), and veto breakouts. MID → both.
  - **Parameters:** window ∈ {15, 30} min; percentiles ∈ {70/30, 80/20}; SSB lag ∈ {0, 5} → 8.
  - **Test window:** M. Exclude 08:30-report days as a stratum, reported separately (the report inflates RV mechanically).
  - **Tests:** (1) Descriptive: the 10:00–15:55 |return|/ATR and trend-day frequency (close in the top or bottom 20% of the range) by state. (2) Host ablation, gated vs ungated, for each host. (3) Placebo: RV computed against **raw** (not same-slot) average volume, to show the SSB matters. (4) Placebo: the 10:00 range ratio (volatility instead of volume). If it does as well, the gate is volatility, not volume.
  - **Kill:** the trend-day frequency difference HIGH − LOW < 10 percentage points; **or** no host improves by ≥ 0.03R; **or** the range-ratio placebo matches the gate (then relabel it as a volatility gate and keep the cheaper input).

#### A2-51 · Intraday shock-bar fade with delayed stop and volume-decay exit
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16, "Intraday Price Shocks": TR > 2× the same-bar 100-day average TR (3× for the opening bar, vs the previous bar), with the close in the extreme 20% → fade at the next open; no stop for 2 bars, then a stop beyond the 3-bar extreme; exit at the close. Profitable on 20m gold (2000–2011) and bonds (factor 2.5). His suggested improvement is to **exit when volume falls back to the same-bar average, or to ½ the shock bar's volume**. **Grade:** C (other markets, gross).
- **Evidence:** **C (other markets, gross)** · **original A2 grade: C (other markets, gross)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 5m and 15m bars, 10:00–15:00
- **Data · test window:** 1m · W-M (agreed split §2.0.1); fill-accurate re-check in W-T
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–6 per month per market at k = 2.5
- **Fails when:** momentum after shock bars (Gao et al.) dominates; adverse fills and 'unables' after fast bars
- **Needs:** ✓ M-TOD (SSB of TR and volume, N = 100), ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29) (release strata), F-TM
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-24 (VWAP band fades: location-based) and the A1 exhaustion ideas. The trigger here is a **same-slot normalised range shock**, not a location. The 2-bar stop delay and the volume-decay exit are distinctive and are tested as ablations.
  - **Timeframes:** 5m and 15m (20m is not native in MC, and 15m is the nearest standard).
  - **Signal (closed bar b at slot τ, 10:00–15:00 ET; the opening bar is excluded in the default, and the book's open rule is a separate arm):** TR_b ≥ k × SSB_TR(τ, N = 100), **and** the close in the top 20% of the bar's range (→ short) or the bottom 20% (→ long). Skip bars containing a scheduled release (08:30 / 10:00 / 14:00 ±2 min). They are a separate stratum.
  - **Entry:** market at the next bar's open.
  - **Stop:** none for bars b+1 and b+2 (the book's rule). From the close of b+2, the stop sits beyond the extreme of bars b..b+2 + 1 tick. A disaster stop at entry ± 1.5 × TR_b applies throughout (our addition, because unlimited 2-bar risk is not acceptable).
  - **Exit (arms):** **E0** 15:55 (book); **E1** the first closed bar with volume ≤ SSB_vol(τ); **E2** the first bar with volume ≤ 0.5 × shock-bar volume; **E3** a time stop after 6 bars.
  - **Filter:** A2-50 state ≠ HIGH (do not fade on high-participation days), tested as an ablation.
  - **Parameters:** k ∈ {2.0, 2.5, 3.0}; bar ∈ {5, 15}m; exit ∈ {E0, E1, E2, E3} → 24.
  - **Test window:** M; T for a fill-accurate check, because the entry follows a fast bar (expect "unables" and adverse fills, M5).
  - **Tests:** (1) Event study: forward 1/3/6-bar returns after shock bars vs same-slot non-shock bars with an equal close location. (2) Stop-delay ablation (0 vs 2 bars). (3) Exit arms E0–E3 on identical entries. (4) The open-bar arm (3× the prior bar) separately.
  - **Kill:** event-study reversion within the CI of the control; **or** net $/contract ≤ 0 at a 2-tick stress; **or** the result exists at only one k.

#### A2-52 · Time-confirmed opening-range break with pivot-range bias (Fisher ACD)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16, Mark Fisher's method:
- **Evidence:** **E (practitioner, no numbers); plain ORB ≈ 0 net (B, 0)** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 15-min OR, 1m confirmation clock, 5m management
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 8–15 per month (most days produce one side)
- **Fails when:** no element adds over plain ORB (the Krueger null)
- **Needs:** F-SESS, F-TM; plain-ORB control in the same slot
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - A = ORH + x% of the OR width; C = a larger %.
    - **Hold beyond A for ½·OR minutes to enter.** Stop 1 tick beyond the opposite OR edge. **Exit if not profitable within 1·OR.** Reverse on C held for ½·OR.
    - Daily pivot range: DPP = (H+L+C)/3, DPD = DPP − (H+L)/2, band DPP ± |DPD|.
    - No numbers; practitioner method. Analyst 1 and the catalogue grade unconditional ORB at about 0 net. **Grade:** C.
  - **Differs from:** the A1 ORB / Krueger replication (first-candle direction, plain range break). A2-52 adds three ACD-specific elements, each ablated: (i) a **time-confirmation hold**, (ii) a **time-based failure exit**, (iii) a **prior-day pivot-range bias**. If no element adds ≥ 0.03R over plain ORB in the same slot, the idea is closed.
  - **Timeframes:** OR = 09:30–09:45 (15 min); 1m confirmation clock; 5m management.
  - **Levels:** ORH/ORL from closed 1m bars. W = ORH − ORL. A_up = ORH + a·W, A_dn = ORL − a·W; C_up = ORH + c·W, C_dn = ORL − c·W.
  - **Entry (long):** first time 1m closes stay ≥ A_up for **½·OR = 8 consecutive minutes** (all closes ≥ A_up). Buy at the next 1m open. Window 09:45–11:30 ET. Mirror short at A_dn.
  - **Bias filter (pivot range):** longs only if the prior RTH close ≥ the pivot-range low, and price has not traded below the pivot-range low today before entry. Mirror for shorts. Arm without the filter as well.
  - **Stop:** ORL − 1 tick (long). **Failure time stop:** if the open P&L ≤ 0 after 1·OR = 15 minutes from entry, exit at market.
  - **Reversal:** while long, if 1m closes stay ≤ C_dn for 8 minutes → reverse short, with the stop at ORH + 1 tick.
  - **Target:** 50% at entry + 1.0·W; runner to 15:55, trailing at the low of the last 3 closed 5m bars after T1.
  - **Parameters:** a ∈ {0.25, 0.5, 1.0}; c ∈ {1.0, 1.5}; confirmation ∈ {½, 1}·OR; OR ∈ {15, 30} min → 24.
  - **Test window:** M.
  - **Tests:** (1) Plain ORB (immediate stop entry at ORH/ORL) in the same slot, the control. (2) 2³ ablation of hold / failure exit / pivot filter. (3) % of the 24 combinations profitable net.
  - **Kill:** the best element adds < 0.03R over plain ORB; **or** < 70% of combinations profitable net; **or** generic criteria.

#### A2-53 · RTH gap fade with stop-and-reverse at the day's extreme (negative canary)
- **Implementation status (v2.4):** **Implemented** — G0-b canary A2_53 in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30)). Not run in MC.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16, "Fading the Opening Range": big S&P pit session, gap ≥ 0.50%. On the designated bar, fade the gap, with a stop-and-reverse at the day's extreme at entry time; exit at the close. **30m, 2000–May 2011: > 1,600 trades, +$32/trade gross.** Kaufman: needs a volatility filter. Ch 15: the open is the most likely daily extreme, and after big gaps the low (high) is at the open on up (down) gaps. That cuts **against** fading big gaps. **Grade:** B− (gross; the net edge is likely ≤ 0).
- **Evidence:** **C (gross; net expected ≤ 0)** · **original A2 grade: B− (gross; the net edge is likely ≤ 0)** (rescaled per §3.12).
- **Markets · TF:** ES; 30m (book) and 15m
- **Data · test window:** 1m · W-M (agreed split §2.0.1); gap tables on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026); skip the session after a roll splice
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman: > 1,600 trades on 30m 2000–2011 (≈ 12 per month)
- **Fails when:** expected to fail net; a positive result with the filter in validation would promote it
- **Needs:** F-SESS, ✓ M-DAILY (`_DailyContext.Hv(n)`, PR #19)
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Gate (v2):** canary (runs in the G0 slot)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-13 (gap-day AVWAP-from-open fade failure → go-with). That one trades the *failure* of a fade around an AVWAP. This is the plain book fade, plus the book's volatility filter. It exists as a canary: if the plain fade is negative net, it supports A2-13's go-with logic.
  - **Timeframes:** 30m (book) and 15m.
  - **Setup:** |RTH gap| ≥ g. Entry bar index j (the j-th bar's close).
  - **Entry:** short at the open of bar j+1 on up-gaps (long on down-gaps) **if** price has not already traded back through the prior close.
  - **Stop-and-reverse:** at the session high at entry time + 1 tick (short case). Once reversed, the stop is the session low at the reversal time − 1 tick, with no further reversal.
  - **Exit:** 15:55.
  - **Volatility filter (book suggestion, pre-registered):** trade only if the prior day was an inside day **or** HV(6 d) < HV(100 d), with HV = SD of daily log returns.
  - **Parameters:** g ∈ {0.3, 0.5, 0.8}%; j ∈ {1, 2} (30m) → 6, × filter on/off.
  - **Test window:** M (D for the daily-bar gap statistics).
  - **Tests:** (1) Replicate Kaufman's gap table on ES RTH 2017–2026 (close: continued / between / reversed; next-open continuation). (2) The strategy gross, then net. (3) Filter on/off.
  - **Kill:** net ≤ 0 (expected). If it is killed, record it as evidence for go-with gap logic (A2-13, A2-54). If **positive** net with the filter in validation, promote it to a full spec.

#### A2-54 · Up-gap pullback-to-prior-close short, exit next open
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16, "Short-Term Gap Patterns". Williams/DeMark gap and "lapse" definitions. Entry choice 3 (on an up-gap, short when price pulls back to the prior close) with exit choice 2 (next open) was the best combination on 15–30m data. No numbers. "Exiting on the next open adds risk but tends to be rewarded." **Grade:** C.
- **Evidence:** **E** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 15m/30m trigger on a 1m touch; overnight hold
- **Data · test window:** 1m · W-M (agreed split §2.0.1) + ETH/overnight hold; skip roll-splice sessions
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–4 per month
- **Fails when:** gap-day event return equal to non-gap days touching C0; overnight risk not rewarded
- **Needs:** F-SESS, F-TM; overnight-margin check
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-53 (fades the gap immediately at the open) and A2-13 (AVWAP). Here the trigger is a **full retrace to the prior close**, i.e. the gap fails completely, and the position is held overnight. This is a gap-failure continuation, the mirror of the gap-fill logic.
  - **Timeframes:** 15m/30m triggers on a 1m price touch; daily exit.
  - **Setup:** RTH up-gap ≥ g, i.e. open > the prior RTH close (C0) by ≥ g. Down-gaps mirror.
  - **Entry (short):** sell stop at C0 − 1 tick, active 09:45–14:30 ET. One trade per day.
  - **Stop:** the session high at entry + 1 tick, floored at 1.0 × ATR(14, 15m) from entry.
  - **Exit:** (a) next RTH open (book); (b) 15:55 (day-only control).
  - **Filter:** none in the core. Arm: the A2-50 state.
  - **Parameters:** g ∈ {0.2, 0.4, 0.6}%; touch level ∈ {C0, C0 − 0.1 × gap} → 6, × exit a/b.
  - **Test window:** M.
  - **Tests:** (1) Event study: the return from C0-touch to the next open vs the touch → 15:55, on gap days vs non-gap days that touch C0 from above. (2) Overnight vs day-only. (3) Gap-size buckets.
  - **Kill:** overnight arm ≤ day-only arm; **or** net ≤ 0 in validation; **or** the gap-day event return is within the CI of the non-gap control.

#### A2-56 · Momentum Pinball: daily LBR/RSI setup, first-hour break, conditional overnight hold
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 16 (Raschke & Connors):
- **Evidence:** **C (+, gross)** · **original A2 grade: B− (gross, but $703 leaves a large cost margin)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily setup, 60m first hour, 1m trigger
- **Data · test window:** 1m · W-M (agreed split §2.0.1) for the strategy; daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) for the setup event study
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman: 1,734 ES trades 2000–May 2011 (≈ 13 per month)
- **Fails when:** the setup adds nothing over the plain first-hour break; overnight gap risk
- **Needs:** ✓ M-DAILY (roll-gap removal ✓; LBR = RSI(3) of ROC1 via `DailyConfig.lbrRsiLength`, PR #19), F-SESS, F-TM
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - Setup: LBR/RSI = **RSI(3) of the 1-day ROC** < 30 yesterday (buy setup; > 70 sell).
    - Trigger: a buy stop above the first-hour high, with the stop at the first-hour low. Re-enter on a re-break if stopped.
    - Carry overnight if profitable. Exit on the next open if it opens in profit.
    - **Emini S&P 2000 – May 2011: 1,734 trades, +$703/trade, no costs.**
    - **Grade:** B− (gross, but $703 leaves a large cost margin).
  - **Differs from:** A2-52 and A1 ORB (no daily setup) and A2-09 (noise-band momentum). This is a **multi-timeframe hybrid**: a daily oversold or overbought setup with an intraday breakout in the *opposite* (reversal) direction, plus a conditional overnight hold.
  - **Timeframes:** daily (setup, RTH closes); 60m first-hour range; 1m trigger.
  - **Setup (prior close, causal):** ROC1_t = C_t − C_{t−1} (RTH closes). LBR = Wilder RSI(3) of ROC1. Long setup if LBR_{t−1} < 30; short setup if > 70.
  - **Entry (long):** after 10:30 ET, buy stop at FH_high + 1 tick (FH = 09:30–10:30). Active until 15:00 ET.
  - **Stop:** FH_low − 1 tick. **Re-entry:** once, if stopped and price later trades back above FH_high + 1 tick before 15:00.
  - **Exit:** if the position is in profit at 15:55, hold it; else flat at 15:55. Next day, if the 09:30 open is beyond the prior 15:55 price in the trade's favour → exit at 09:31. Otherwise exit with a stop at the entry price, or at 15:55 that day (a 2-day maximum; our addition, since the book leaves it open).
  - **Parameters:** RSI length ∈ {2, 3, 4}; thresholds ∈ {30/70, 25/75}; first-range ∈ {30, 60} min → 12.
  - **Test window:** M; D for the setup event study (ES daily back to 2000).
  - **Tests:** (1) Exact book replication (3, 30/70, 60 min), gross and net. (2) The setup alone: next-day RTH return after LBR < 30 vs unconditional. (3) The trigger alone (first-hour break with no setup), the control. (4) Overnight-hold vs day-only.
  - **Kill:** net $/contract ≤ 0 in validation; **or** the setup adds < 0.05R over the trigger-alone control; **or** < 70% of the 12 combinations profitable net.

#### A2-57 · Higher-TF turn plus first short-TF oversold entry (Kaufman/Elder)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 19, "Tuning Two Time Frames":
- **Evidence:** **E** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 60m/12m/1m (robustness 30m/6m/1m)
- **Data · test window:** 1m · W-M (agreed split §2.0.1) (60m bars include Globex; RTH-only variant); needs ETH bars (verify E5)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 3–6 per month
- **Fails when:** 'any OS' does as well as 'first OS' (Kaufman's key claim fails); trend-only entry does as well
- **Needs:** ✓ M-MTF (native BarsOfData(n), no internal HTF bar building), F-TM
- **Readiness (as of merge):** Buildable now on the merged base (run MC Probe P2 first).
- **Gate (v2):** A2-40/A2-59 cost model on; timing contest with A2-65/A2-66 in one slot
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - Choose a timing oscillator that reaches OB/OS about once in the first 10–20% of the trend trade's holding period, and **take only the first OS after the trend signal**. Its own P&L is irrelevant. EURUSD 60d MA + 3-day stochastic: one trade went from $2,075 to $6,200.
    - Elder Triple Screen: time frames step ×5; Force Index EMA2 below 0; buy stop above the prior bar's high; stops start below the entry/prior bar low, move to break-even, then trail at 50% of peak open profit.
    - Krausz: the higher TF overrides.
    - **Grade:** C (no index-intraday test).
  - **Differs from:** A2-01 (AVWAP handoff pullback: location-based) and A1 pullback specs. A2-57 is purely **time-structure tuned**: a single higher-TF state change followed by the **first** lower-TF oscillator extreme. Later extremes are ignored, which is the key Kaufman rule.
  - **Timeframes:** 60m trend / 12m oscillator / 1m trigger (ratio ×5). Robustness: 30m / 6m / 1m.
  - **Trend (60m, closed bars):** EMA(13) of 60m closes. Up-turn = the EMA slope changes from ≤ 0 to > 0. The state stays up until the slope turns ≤ 0. Overnight bars are included in the 60m series (24h). A variant uses RTH only.
  - **Oscillator (12m):** raw stochastic %K(3) = (C − LL3)/(HH3 − LL3). Arm F: Force Index = V × ΔC, EMA(2) < 0 and above its 20-bar low.
  - **Entry (long):** the **first** 12m bar after the 60m up-turn with %K(3) ≤ 20 (or the arm-F condition). Then a buy stop at that 12m bar's high + 1 tick, valid for the next 2 12m bars. RTH 09:45–15:00 ET only. One trade per 60m up-turn.
  - **Stop:** the lower of the 12m signal-bar low and the prior 12m bar's low − 1 tick. Move to break-even at +1R. After that, trail to protect 50% of the peak open profit.
  - **Exit:** the 60m EMA slope turns ≤ 0 (close-based), the trail, or 15:55.
  - **Parameters:** stochastic length ∈ {2, 3, 5}; OS level ∈ {20, 30}; TF set ∈ {60/12/1, 30/6/1} → 12, plus arm F.
  - **Test window:** M.
  - **Tests:** (1) Trend-only control: enter at the 60m turn (the next 1m open) with the same exits. The book's claim is that the timed entry beats it. (2) "Any OS" vs "first OS only". (3) The 14-period stochastic, which the book predicts is too slow (a negative control).
  - **Kill:** the timed entry ≤ the trend-only control in $/trade **and** in % winners; **or** "any OS" ≥ "first OS" (the key claim fails); **or** generic criteria.

#### A2-58 · Fade the fast trend inside the slow trend, intraday
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 21:
- **Evidence:** **C (daily NASDAQ) · H intraday** · **original A2 grade: B− on daily bars, C intraday** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 5m (primary), 15m
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 4–10 per month
- **Fails when:** the fast-only system is not negative on CME 5m (the noise premise is absent)
- **Needs:** F-TM (base exists)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on; overlap check vs A2-57
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - Fast MA periods lose consistently on noisy equity indices (NASDAQ periods 1–11 all lost). Use the losing fast trend as **counter-timing** inside a profitable slow trend: buy when the slow MA is up and the fast MA turns down; exit when the slow turns.
    - NASDAQ daily: **PF 2.26 vs 1.51, 72 vs 212 trades, 47% vs 35% winners, $741 vs $277/trade** vs the plain MA.
    - Ch 1/16: the S&P is the noisiest market, so wait for a pullback.
    - **Grade:** B− on daily bars, C intraday.
  - **Differs from:** A2-57. That one uses a **higher-TF** state with an oscillator, while A2-58 uses two MAs on **one** timeframe (Kaufman's exact construction). If trades overlap > 70%, keep the better one. Also distinct from the A1 trend specs (no fade timing).
  - **Timeframes:** 5m (primary), 15m (robustness).
  - **Slow trend:** SMA(S) of closes, direction = sign(SMA_t − SMA_{t−1}). **Fast trend:** SMA(F).
  - **Entry (long):** the slow direction is up, and the fast direction turns down (from up to down on this closed bar). Buy at the next open. 09:45–15:00 ET. Mirror short.
  - **Exit:** the slow direction turns (close-based), or 15:55. **Disaster stop:** 2.0 × ATR(14, 5m) (not in the book).
  - **Parameters (geometric, M3):** S ∈ {20, 30, 45, 68}; F ∈ {2, 3, 5} → 12.
  - **Test window:** M.
  - **Tests:** (1) The plain slow-MA system (enter on the slow turn) in the same slot, as the book's comparison. (2) The fast-only MA system, which should lose (it confirms the noise premise on CME 5m). (3) The surface: % profitable, smoothed surface max (M6, M7).
  - **Kill:** the fade's net $/trade ≤ the plain slow MA's; **or** the fast-only system is *not* negative (the premise is absent, so the idea is unfounded); **or** generic criteria.

#### A2-60 · Meyers normalised range-ratio adaptive breakout (5m)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · Kaufman Ch 17, "Adaptive Intraday Breakout":
- **Evidence:** **E (one optimised ETF article, no OOS)** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** MNQ/NQ primary (QQQ analogue), ES OOS; 5m RTH
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman/Meyers: about 1 trade per day on QQQ
- **Fails when:** peak solution (shell-1 average < 50% of core)
- **Needs:** F-TM (base exists)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - HR = H_t − L_{t−n}, LR = L_t − H_{t−n} (the single bar n back); NHR = HR / n^a / ATR(n); HRmax = highest(NHR, n), likewise LRmax.
    - Buy if HRmax > thrH and LRmax < thrL; sell the reverse. Flat 5 minutes before the close.
    - QQQ optimum: **n = 6, a = 0.75, thrH = 0.50, thrL = 1.05**, n ≤ 25, about 1 trade/day. Kaufman: "worth pursuing if consistent over a long period".
    - **Grade:** C (a single optimised ETF article, no OOS).
  - **Differs from:** fixed-range breakouts (A2-52, ORB) and the noise-band momentum of A2-09. The threshold is a √n-type volatility-normalised path statistic. Its job here is a **pre-registered replication at the published point** on MNQ (the QQQ analogue), with ES as OOS.
  - **Timeframes:** 5m (primary), 1m (robustness). RTH only; indicators reset each session (no overnight bars inside n).
  - **Rules:** as the source. A signal on a closed bar → the next bar's open. Stop-and-reverse on the opposite signal. Flat 15:55. Window 09:45–15:50.
  - **Stop:** 1.5 × ATR(n, 5m) disaster stop (our addition).
  - **Parameters:** the core is fixed at the published values. Sensitivity shells (M8): n ∈ {4, 6, 9}, a ∈ {0.5, 0.75, 1.0}, thrH ∈ {0.4, 0.5, 0.6}, thrL ∈ {0.9, 1.05, 1.2} → 81 combinations, used only for the shell-decay plot, not for selection.
  - **Test window:** M.
  - **Tests:** (1) The published core on MNQ/NQ, then ES/MES. (2) n-space shell decay around the core. (3) A random-entry control with the same trade frequency and holding time.
  - **Kill:** net ≤ 0 at the core on NQ in validation; **or** the shell-1 average < 50% of the core (a peak solution); **or** generic criteria.

#### A2-61 · Efficiency-ratio session router (module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · Kaufman Ch 1 (ER as the noise measure; equity indices noisiest), Ch 17 (KAMA: slow in noise, fast in clean trends), Ch 16 (noise favours mean reversion and pullback entries), Ch 23 (r² > 0.25 / ADX > 20 as trend-state thresholds). **Grade:** C.
- **Evidence:** **E** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 5m 09:30–11:00, used 11:00–15:55
- **Data · test window:** 1m · W-M (agreed split §2.0.1); 250-session percentile warm-up
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every session
- **Fails when:** morning ER does not persist (rank correlation < 0.15); dominated by A2-29/A2-50
- **Needs:** F-TM (trivial)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** routes hosts after they pass
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-29 (entropy day-type gate on the profile distribution) and A2-16 (OR AVWAP slope). A2-50 is a volume gate. A2-61 is a **path-efficiency** gate. It is built to be raced against A2-29/A2-50 on the same hosts. If any two gates agree on > 80% of sessions, keep the cheapest.
  - **Timeframes:** 5m bars, 09:30–11:00 ET (18 bars); used 11:00–15:55.
  - **Statistic:** ER18 = |C_11:00 − O_09:30| / Σ|ΔC| over the 18 closed 5m bars. Percentile vs the trailing 250 sessions (causal).
  - **Routing:** ER pct ≥ 70 → continuation hosts only (A2-49 BO arm, A2-58 in the trend direction, A2-57). ER pct ≤ 30 → reversion hosts only (A2-49 SR arm, A2-51). Else both.
  - **Arm (KAMA):** a KAMA(10, 2, 30) on 5m as the trailing stop for continuation hosts on high-ER days, vs their native stop.
  - **Parameters:** window ∈ {60, 90} min; percentiles ∈ {70/30, 80/20} → 4, plus the KAMA arm.
  - **Test window:** M.
  - **Tests:** (1) Descriptive: the rest-of-day ER and trend-day frequency by morning-ER bucket (persistence of noise). (2) Host ablation, routed vs unrouted. (3) Race against A2-29 and A2-50 (the overlap matrix).
  - **Kill:** the morning-ER → afternoon-ER rank correlation < 0.15; **or** no host improves by ≥ 0.03R; **or** dominated by another gate on every host.

#### A2-62 · Taylor 3-day cycle 'low-first' buying day
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW (swing/daily) · Analyst 2 Rev 6 · Kaufman Ch 16, Taylor Trading Technique:
- **Evidence:** **E/D** · **original A2 grade: C/D** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; daily RTH cycle, 1m order of extremes
- **Data · test window:** 1m · W-M (agreed split §2.0.1); daily-only approximation on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 3–5 per month in uptrends
- **Fails when:** the buying-day phase does no better than phase-shifted placebos
- **Needs:** ✓ M-DAILY, F-SESS
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
    - Buying day / selling day / short-sale day rhythm. Buy on the buying day only if **the low comes before the high** (a test of the prior low first). In an uptrend, buy any lower open. Sell the next day at or above the prior high.
    - The rigid 3-Day method enters the objectives in advance.
    - Kaufman: the 3-day cycle "continues to be a successful pattern" (no numbers).
    - **Grade:** C/D.
  - **Differs from:** the A1 daily swing ideas and A2-14 (weekly AVWAP). This uses a **mechanical cycle count** plus an intraday order-of-extremes rule. It is the only rhythm idea in the file. It is deliberately simple, so it serves as an inexpensive falsification test of a famous claim.
  - **Timeframes:** daily RTH bars for the cycle and trend; 1m for the order of extremes and fills.
  - **Uptrend:** the 20-day SMA of RTH closes is rising (higher than 5 days ago).
  - **Cycle anchor:** re-anchor every 20 sessions. The buying day = the day after the lowest RTH low of the prior 10 sessions, then every 3rd session forward (causal: the anchor uses only completed sessions). This is a fixed rule set, decided up front.
  - **Entry (long, buying day, uptrend only):** if price trades ≤ the prior RTH low **before** it trades ≥ the prior RTH high, buy at the prior low (a limit, filled only on a trade-through by 1 tick). If the open < the prior close, also allow a buy at 09:35 (Taylor's "any lower open in an uptrend"), with one entry per day.
  - **Stop:** the prior low − 1.0 × ATR(14, daily) × 0.5.
  - **Exit:** the next day (selling day) at a limit of the buying day's high; else at 15:55 of the selling day.
  - **Parameters:** trend SMA ∈ {10, 20, 40}; anchor window ∈ {10, 15} → 6.
  - **Test window:** M (needs the intraday order of extremes); D for a daily-only approximation (open → buy-day low proxy).
  - **Tests:** (1) Cycle vs a **phase-shifted placebo** (anchor + 1 and + 2 days), which is the decisive control for any cycle claim. (2) The low-first condition vs no condition.
  - **Kill:** the buying-day phase ≤ both shifted phases + 0.05R; **or** net ≤ 0 in validation.

#### A2-63 · Long-only open + 0.5 × daily ATR breakout, exit at the close (host H2)
- **Implementation status (v2.4):** **Implemented** — `_RegimeGate` + `_Test6` consumer per `specs/regime-consumer-prereg.md` / `a2-63-rulings.md` (PR #26; [#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)). Pre-registered test not run.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · KCTS Ch 25, "Expanding the concept": ES 30m bars from 09:30 ET, threshold = today's open + factor × daily ATR(20), long-only for the S&P. Final S&P breakout factor **0.5**: **748 long trades, 52% profitable, net $727,125 after $8/contract, Jan 2010 – Sep 2016** (size $25,000/ATR$, so it is many contracts, not 1). From-the-open SPY test 1 (LVF 0): steady gains until Covid. **Grade:** B− (ES, multi-year, but in-sample, no slippage and multi-contract sizing).
- **Evidence:** **C (+, in-sample, no slippage, multi-contract sizing)** · **original A2 grade: B− (ES, multi-year, but in-sample, no slippage and multi-contract sizing)** (rescaled per §3.12).
- **Markets · TF:** MES/ES, NQ/RTY/YM cross-market; 30m (book), 20m; 1m stop fill
- **Data · test window:** 1m · W-M (agreed split §2.0.1) (2016–2026 is after the book's Jan 2010–Sep 2016 sample, so out of sample); 2008 daily proxy on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) (M20)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** at most one per day; author estimate 6–10 per month
- **Fails when:** long-only drift masquerading as edge (K-DR); overlap > 70% with A2-09
- **Needs:** ✓ M-DAILY (Atr20d, roll-gap removed; latch at 09:30 in the strategy), F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **G-R consumer (v2.3b, A2 review):** `_RegimeGate` (A2-63 G-R consumer C1; arms U/G/A) is coded in PR #26 (cd55796 etc.: `_RegimeGate.Function.CS`, `tests/RegimeGateTests`; pre-registration `specs/regime-consumer-prereg.md`); its pre-registered test is **not yet run**. No §2.4 module row.
- **Gate (v2):** A2-40/A2-59 cost model on; host H2 for A2-72…A2-79 (disaster-stop-at-the-open arm on)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-09 (noise band = same-slot average |move from open| over 14 days, checked at :00/:30, VWAP trail). A2-63 uses a **single daily-ATR threshold fixed at the open**, any-bar trigger and a plain close exit. A2-52 / A1 ORB use the opening-range high, not a volatility distance. Run A2-63 and A2-09 on the same days and report the trade overlap. If overlap > 70%, keep the one with the better net $/trade.
  - **Timeframes:** 30m (book), 20m (Kaufman's other preferred size); 1m for the stop-order fill.
  - **Entry (long only):** buy stop at O + k × ATR20d, O = the 09:30 ET RTH open. Active 09:30–15:30 ET. One entry per day. A stop order is causal; the bar-close variant (enter at the next bar's open after a 30m close above the level) is an arm.
  - **Stop:** none in the book (the exit is the close). Our addition, as an ablation: a disaster stop at O (the open), i.e. k × ATR below entry.
  - **Target:** none in the core. Arm: entry + 0.5 × ATR20d (best target in the SPY open-based test 2).
  - **Exit:** 15:55 ET. Arm: hold overnight if profitable at 15:55, exit 09:31 (book floor-trader rule; the SPY overnight version was rejected for Covid, so expect it to lose).
  - **Filter:** none in the core. Arms: (i) 50-day SMA of RTH closes rising; (ii) prior inside day / 3-bar compression (A2-55 G1). Each arm must pass M13.
  - **Parameters:** k ∈ {0.3, 0.5, 0.75}; bar ∈ {20, 30}m; ATR length ∈ {10, 20, 40} d → 18. Pre-registered core: k 0.5, 30m, ATR 20.
  - **Sizing:** 1 MES/ES for the edge test; A2-71 for the portfolio version.
  - **Test window:** M (Oct 2017 – 2026 is entirely after the book's Jan 2010 – Sep 2016 sample, so it is true OOS). NQ/RTY/YM per M10.
  - **Tests:** (1) Replicate the core, gross, then net (A2-59). (2) Short arm (O − k·ATR) as a control: the book predicts it loses on the S&P. (3) Surface (M6–M8). (4) Overlap with A2-09.
  - **Kill:** net $/contract ≤ 0 in validation; **or** < 70% of the 18 combinations net-positive; **or** NQ net < 0 at the core; **or** generic criteria.

#### A2-64 · 1.0 × daily ATR fade from the open or prior close
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · KCTS Ch 25, "Mean-reversion trade" and "Combining": fade moves beyond the open/prior close ± MR × daily ATR(20). S&P "outstanding" both sides; final S&P MR threshold **1.0** (bonds 0.8, euro 1.0, crude 0.8). Exit rule: an MR long exits if price returns through the previous close; after an MR entry, only MR trades that day. The MR threshold doubles as the breakout system's profit target. No separate MR figure for the S&P (the $727K is the combined, mostly the breakout leg). **Grade:** C (S&P result not isolated, in-sample).
- **Evidence:** **E (S&P leg not isolated, in-sample)** · **original A2 grade: C (S&P result not isolated, in-sample)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 30m/20m signals, 1m limit fills
- **Data · test window:** 1m · W-M (agreed split §2.0.1); 2008 daily proxy on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–5 per month (1 ATR from the reference is reached on a minority of days)
- **Fails when:** fading a trend day; the short side loses on index futures
- **Needs:** ✓ M-DAILY, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on; paired with A2-63 (book interaction rule)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-24 (VWAP band fades, a negative control), A2-51 (same-slot shock-bar fade: one-bar range trigger), A2-53 (gap fade at the open, triggered by the gap). A2-64 is a **distance-from-reference fade measured in daily ATR**, at any time of day, no gap requirement.
  - **Timeframes:** 30m/20m signal bars; 1m for limit fills.
  - **References:** arm R-O = today's 09:30 open; arm R-C = prior RTH 16:00 close (prior 15:59 close on contracts without a 16:00 print). Both fixed at 09:30.
  - **Entry:** sell limit at R + m × ATR20d; buy limit at R − m × ATR20d. Limit fills only on a trade-through by 1 tick (A2-40). Active 09:45–15:00 ET. Max one entry per side per day. Arm: bar-close entry (next bar open after a close beyond the level), which pays full slippage per A2-59.
  - **Stop:** none in the book. Disaster stop at R ± 2 × m × ATR (our addition; reported with and without).
  - **Exit:** (a) price returns to R (the reference) — target; (b) 15:55. For R-C, also the book's "exit if price goes back through the previous close".
  - **Interaction rule:** if A2-63 is live the same day, after an A2-64 entry, A2-63 is disabled for that day (book rule). A2-63 long is also exited at the upper A2-64 level (the book's "don't be long above the upper MR level"), as an A2-63 target arm.
  - **Filter:** A2-50 state ≠ HIGH and A2-61 ER pct ≤ 70, each as an ablation.
  - **Parameters:** m ∈ {0.75, 1.0, 1.25}; reference ∈ {R-O, R-C}; bar ∈ {20, 30}m → 12. Core: m 1.0, R-O, 30m.
  - **Test window:** M.
  - **Tests:** (1) Event study: forward return to 15:55 after the first touch of R ± m·ATR vs same-time touches of a placebo level R ± m·ATR on random days of matched ATR. (2) Long and short reported separately (expect longs stronger). (3) Combined A2-63 + A2-64 book system vs each leg.
  - **Kill:** net ≤ 0 at a 2-tick stress; **or** the short side alone negative on both ES and NQ (then long-only, and re-run as a new ID); **or** the event-study reversion within the placebo CI; **or** generic criteria.

#### A2-65 · Raschke '1st cross' oscillator/trend cross with bar confirmation
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW (swing/daily) · Analyst 2 Rev 6 · KCTS Ch 24: osc = SMA(fast) − SMA(slow); osctrend = SMA(osc, mom). Buy when osc crosses above osctrend **and** today's low > yesterday's low (simultaneous); short mirror with highs; exit on the opposite cross. **ES daily Jan 1998 – May 2019, 17/50/5: net $1.54M, PF 1.38, 213 trades, 49.3% win; long PF 1.62, short PF 1.21, max DD 45%.** Delayed entry is worse (PF 1.10). Stochastic-band version (13/25/3): PF 1.69, 270 trades, DD 17.9%. **Grade:** B− on daily ES; **C** on intraday bars (untested).
- **Evidence:** **C (+, daily ES) · H on 30m** · **original A2 grade: B− on daily ES; C on intraday bars (untested)** (rescaled per §3.12).
- **Markets · TF:** ES; daily (replication) and 30m
- **Data · test window:** daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) (replication incl. 2008/2020) and 1m · W-M (agreed split §2.0.1) (30m)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman daily ES: 213 trades 1998–2019 (≈ 0.8 per month); 30m more
- **Fails when:** short side weak (PF 1.21 in the book); max DD 45% on daily
- **Needs:** ✓ M-DAILY, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on; timing contest with A2-57/A2-66
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-57 (60m EMA turn, then the first 12m %K extreme, multi-timeframe). A2-65 is a **single-timeframe MACD-style osc/signal cross with a higher-low bar condition**, no oscillator extreme. A2-58 fades the fast MA inside the slow; A2-65 joins the turn. If trade overlap with A2-57 > 70%, keep one.
  - **Timeframes:** 30m (intraday; the SMAs run across sessions on RTH bars only, with no daily reset) and daily (D, replication).
  - **Entry (long):** on a closed bar, osc crosses above osctrend **and** low_t > low_{t−1}. Buy at the next bar's open. Intraday window 09:45–15:00 ET. Short mirror (arm; the book says longs only is better).
  - **Exit:** osc crosses below osctrend (close-based), next bar open. Intraday: also 15:55 (day arm) vs carry (swing arm, exit at the next bar open after the cross, including 09:31 next RTH).
  - **Stop:** disaster 2.5 × ATR(14) of the bar TF (our addition, ablated).
  - **Arm SB (stochastic bands):** stoch = slow %K(3) of osc over 14 bars; long after osc > 0 when stoch first falls < 20, exit at > 80; first occurrence only.
  - **Parameters (M3):** (fast, slow, mom) ∈ {(11, 34, 3), (17, 50, 5), (25, 75, 8)}; TF ∈ {30m, daily} → 6, plus the SB arm at 13/25/3.
  - **Test window:** D (ES daily 2000–2026, replication incl. 2008/2020) and M (30m).
  - **Tests:** (1) Daily replication 17/50/5 on @ES, long and short separately, net. (2) Simultaneous vs delayed confirmation. (3) 30m version with the same three sets.
  - **Kill:** long-side net PF < 1.15 on daily 2000–2026; **or** the 30m version net ≤ 0 (then keep it as a daily-only idea); **or** generic criteria.

#### A2-66 · Raschke/Kaufman '2nd cross' second stochastic pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW (swing/daily) · Analyst 2 Rev 6 · KCTS Ch 24: trend up = fast SMA > slow SMA; 1st cross begins when stoch < 50 and ends when stoch > 50; then buy the **2nd** pullback when stoch < 20; exit on stoch > 95 or trend change. Short: sell when stoch > 60, exit < 45 or trend change. **ES daily, fast 11 / slow 80 / mom 3: net $398K, PF 2.79, 86 trades, 65% win; long PF 5.15 (52 trades, 75%), short PF 1.74; max DD 20.7%.** SPY short PF 0.93. **Grade:** B− (daily ES, but only 86 trades → sample error ≈ 11%; and in-sample).
- **Evidence:** **C (daily ES, 86 trades, in-sample)** · **original A2 grade: B− (daily ES, but only 86 trades and in-sample)** (rescaled per §3.12).
- **Markets · TF:** ES; daily and 30m
- **Data · test window:** daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) and 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman daily ES: 86 trades in the book test (period not restated by A2); expect < 1 per month on daily
- **Fails when:** < 100 trades at the core (cannot be judged); 2nd-OS no better than any-OS
- **Needs:** ✓ M-DAILY, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-57 explicitly takes **only the first** oversold after the trend turn and ignores later ones. A2-66 deliberately **skips the first** pullback and takes the second. Run both on the same trend definition as a direct test of Kaufman's two contradictory timing claims. A2-65 uses no oscillator extreme.
  - **Timeframes:** daily (D) and 30m (M), RTH bars.
  - **State machine (long, closed bars):** S0 trend up (SMA11 > SMA80) → S1 stoch(osc) < 50 → S2 stoch > 50 → **entry** when stoch < L at the next bar's open. Reset to S0 on a trend change. One entry per trend leg.
  - **Exit:** stoch > U, or SMA11 < SMA80, at the next bar's open; intraday also 15:55 (day arm) vs carry (swing arm).
  - **Stop:** disaster 2.5 × ATR(14) of the TF (ablated).
  - **Shorts:** arm only (book: SPY short PF < 1).
  - **Parameters:** (fast, slow) ∈ {(11, 80), (8, 55), (16, 120)}; L/U ∈ {20/95, 30/90} → 6; stoch %K = 14, slow 3.
  - **Test window:** D (2000–2026) and M.
  - **Tests:** (1) Daily replication, long-only, net. (2) 1st-OS (A2-57 logic on the same trend) vs 2nd-OS vs any-OS. (3) 30m version.
  - **Kill:** 2nd-OS ≤ any-OS in $/trade **and** % winners; **or** < 100 trades in the combined D+M sample at the core; **or** generic criteria.

#### A2-67 · Intraday RSI-of-highs/lows divergence long
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · KCTS Ch 25: 5m bars; long when (1) lows rising L_t > L_{t−1} > L_{t−2}; (2) RSI(10) of lows < 20; (3) RSI(10) of highs − RSI(10) of lows > 15; exit when RSI(10) of highs ≥ 95; no stop. **IWM 2016: 14 longs, 78% correct, nearly all profit from longs; QQQ "90% good trades".** Kaufman's own chart check found it fired after **up** bars (a momentum, not divergence, pattern). **Grade:** C (one year, 14 trades).
- **Evidence:** **E (14 trades, one year)** · **original A2 grade: C (one year, 14 trades)** (rescaled per §3.12).
- **Markets · TF:** MNQ/NQ primary, RTY, ES OOS; 5m
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** Kaufman: about 14 per year on IWM (likely too few: M19)
- **Fails when:** fires after up bars (Kaufman's own check: momentum, not divergence); too few trades
- **Needs:** F-TM (trivial)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-22 (cumulative-delta divergence, exit only) and A2-12 (volume-weighted vs unweighted). No existing idea uses RSI of highs vs RSI of lows.
  - **Timeframes:** 5m (book), 3m and 10m (robustness). RSI is Wilder, computed on RTH 5m bars, carried across sessions (arm: reset daily with a 10-bar warm-up from 09:30, no signal before 10:20).
  - **Entry (long):** all three conditions on a closed bar → buy at the next open, 09:45–15:30 ET.
  - **Exit:** RSI(highs) ≥ X; or 15:55. **Stop:** none in the book; disaster 2 × ATR(14, 5m) (ablated). Short mirror is an arm only (book: shorts made little).
  - **Parameters:** RSI length ∈ {7, 10, 14}; diff ∈ {10, 15, 20}; exit X ∈ {90, 95} → 18.
  - **Test window:** M (MNQ/NQ primary as the QQQ analogue; RTY as the IWM analogue; ES OOS).
  - **Tests:** (1) Count signals per year first (M1: the book had about 14/yr on IWM, so expect too few trades — sample-size check per M19). (2) Label test: compare against a plain "3 rising lows + RSI(lows) < 20" without the diff condition.
  - **Kill:** < 100 trades over 9 years at the core (then it cannot be judged — close); **or** net ≤ 0; **or** the diff condition adds nothing.

#### A2-68 · 5-bar run + doji reversal, target 50% of the run
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** IM (intraday mechanical, Kaufman) · Analyst 2 Rev 6 · KCTS Ch 25 (from *Technical Analysis of Stocks & Commodities*): 1-hour bars; 5 down bars in a row; RSI < 10; then a doji; buy with a target at entry + 50% of the 5-bar down move; support (prior swing low) nearby helps. No test numbers. **Grade:** D.
- **Evidence:** **E (S&C article, no numbers)** · **original A2 grade: D** (rescaled per §3.12).
- **Markets · TF:** ES/NQ; 60m (24h and RTH arms), 30m
- **Data · test window:** 1m · W-M (agreed split §2.0.1) (24h arm needs ETH bars, verify E5)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** rare (A2 expects < 100 trades)
- **Fails when:** too few signals; within the random-control CI
- **Needs:** F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-51 (single shock bar fade) and A2-67 (RSI divergence). This is a **count-of-bars exhaustion with a doji confirmation** and a measured-move target.
  - **Timeframes:** 60m (book) on 24h Globex bars and RTH-only bars (two arms); 30m robustness.
  - **Definitions:** down bar = close < open. Doji = |C − O| ≤ d × (H − L), d = 0.1. Run = 5 consecutive down bars ending at bar t−1; doji at bar t. RSI(14) on the bar TF, < 10 at the last run bar (arm: < 20, because RSI < 10 on 60m is rare).
  - **Entry (long):** at the open of the bar after the doji. Short mirror is an arm.
  - **Target:** entry + 0.5 × (high of the first run bar − low of the run). **Stop:** low of the run − 1 tick (our addition). **Time exit:** 5 bars, or 15:55 for the RTH arm.
  - **Support arm:** require the run low within 0.5 × ATR(14) of the prior swing low (3-bar pivot, confirmed).
  - **Parameters:** run length ∈ {4, 5}; RSI threshold ∈ {10, 20}; TF ∈ {30, 60}m → 8.
  - **Test window:** M.
  - **Tests:** (1) Signal count first (expect few). (2) Doji vs no-doji ablation. (3) Random-entry same-geometry control.
  - **Kill:** < 100 trades at all settings (record as untestable); **or** net ≤ 0; **or** within the random-control CI.

#### A2-69 · Kaufman 3-day trade: two down closes, buy a lower open, exit next close
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** SW (swing/daily) · Analyst 2 Rev 6 · KCTS Ch 15/24/27: two lower closes → buy a lower open (else the close), exit on the close of the following day; mirror shorts. **ES/NQ/RU futures 1990–2016: longs profitable in all three (ES $5.13M, NQ $4.22M, RU $4.78M), shorts weak or negative; 2016 OOS longs positive.** SPY 1999–2020 with a 120-day MA filter: in the market 21% of days, no 2008 decline. Ch 27 version: buy the 3rd-day lower close above the 50-day MA, hold 1 day; daily-return correlation 0.21 with an 80-day MA system; IR 0.53. **Grade:** B− (multi-decade ES/NQ/RU, gross).
- **Evidence:** **C (+, multi-decade, gross)** · **original A2 grade: B− (multi-decade ES/NQ/RU, gross)** (rescaled per §3.12).
- **Markets · TF:** ES/NQ/RTY daily; 1m fills at 09:31/15:59
- **Data · test window:** daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) (primary, includes 2008/2020); 1m · W-M (agreed split §2.0.1) for the 1m fill check
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** author estimate: 2–4 per month
- **Fails when:** equity-beta drift (long-only); overlap with SW-02 and A2-62
- **Needs:** ✓ M-DAILY, F-SESS
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** A2-40/A2-59 cost model on; shares a slot with SW-02 (overlap reported)
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-62 (Taylor 3-day **cycle**, a calendar count anchored on a 10-day low, plus an order-of-extremes rule). A2-69 is a **close-count pattern**, with no cycle anchor. Run A2-62 and A2-69 on the same days and report the overlap. A2-56 uses an RSI(3) setup plus a first-hour break.
  - **Timeframes:** daily RTH bars (signals); 1m for the 09:30 open / 15:59 close fills.
  - **Setup (prior close, causal):** C_{t−1} < C_{t−2} < C_{t−3} (two lower RTH closes).
  - **Entry (long):** if O_t < C_{t−1}, buy at 09:31 (first 1m bar open); else buy at 15:59 (MOC proxy). Arm C27: buy only at the close of day t if C_t < C_{t−1} and close > SMA50.
  - **Exit:** 15:59 on the following session (t+1). Arm: hold one extra day.
  - **Profit-take arm:** exit intraday at entry + 2 × ATR20d (book rule 1). **Spike arm:** also buy any day whose close is ≥ 2.5 × ATR20d below the prior close **and** in the lowest 25% of its range (book rule 2).
  - **Filters:** long-only in the core (book: shorts lose on equity index); trend filter SMA ∈ {off, 50, 120} as arms; A2-70 high-vol skip as an arm.
  - **Stop:** none in the book (2-day hold is the risk control). Disaster 3 × ATR20d (ablated).
  - **Parameters:** trend SMA ∈ {off, 50, 120}; entry ∈ {open-else-close, close-only} → 6.
  - **Test window:** D (ES/NQ/RTY daily 2000–2026, includes 2008/2020) for the edge; M for the 1m fill check.
  - **Tests:** (1) Replication, long vs short, by weekday (report only — **no day-of-week selection**, per the book's "don't micromanage days"). (2) Fridays-only short as a pre-registered single arm (the book's one consistent short). (3) Correlation with A2-56 and A2-62 daily P&L.
  - **Kill:** long-side net ≤ 0 on ES 2017–2026 (post-book); **or** NQ long net ≤ 0; **or** the 2000–2016 long result is not reproduced in sign.

#### A2-70 · High-volatility skip gate (module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** RG module (gate) · Analyst 2 Rev 6 · KCTS: skip new short-term trades when 20-day annualised vol > 45–50% (single market), and the portfolio threshold is nearer 30%. SPY annualised vol hit about 100% in 2008. The Ch 3 SPY breakout test: best threshold 0.70–0.80, filtering 2008 and 2020, slightly lower profit, better reward/risk. "Avoid entering when annualised vol > 50%" (Ch 18, Ch 29 principle 6). **Grade:** C (SPY daily test only; thresholds are market-specific per Kaufman).
- **Evidence:** **E** · **original A2 grade: C (SPY daily test only; thresholds are market-specific)** (rescaled per §3.12).
- **Markets · TF:** all hosts
- **Data · test window:** daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) for gate statistics (2008 only in W-D); 1m · W-M (agreed split §2.0.1) for host ablation
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** blocks a few % of sessions (A2 kill if < 3% at θ = 50% on W-M)
- **Fails when:** inert in the 1m window; blocks the days when fades pay most
- **Needs:** ✓ M-DAILY (`Hv(20)`, `HvPercentile`, PR #19)
- **Readiness (v2.3b):** Buildable now on the merged base.
- **Gate (v2):** applied to survivors
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-55 (volatility **contraction** gate: low-vol days only, for breakout hosts) and the A2-53 HV6 < HV100 filter (relative, not absolute). A2-70 is an **absolute high-volatility veto** on all hosts, with a re-entry gap to stop whipsaw. A2-50/61 route trades between host types; A2-70 removes them.
  - **Statistic:** HV20 (see conventions), fixed at the prior close. Arm: VIX prior close if available in the feed (unverified).
  - **Gate:** no new entries while HV20 > θ_off; re-enable only when HV20 < θ_on (hysteresis θ_on = 0.8 × θ_off, Kaufman's AMZN gap example). Open trades are not closed by the gate (arm: close swing holds at the next open).
  - **Hosts:** every surviving intraday and daily host, especially A2-63/64/69 (the book's own systems) and A2-49/56.
  - **Parameters:** θ_off ∈ {30, 40, 50, 70}% (absolute); arm with θ_off = the trailing 2-year 90th percentile of HV20 (market-adaptive) → 5. Fixed across ES/NQ/RTY/YM except the percentile arm.
  - **Test window:** D for the gate statistics (fraction of days blocked since 2000: 2008, 2011, 2020, 2022); M for host ablation. **Note:** 2008 exists only in D; 1m tests see 2018 Q4, 2020 and 2022 (M20).
  - **Tests:** (1) Host ablation per M13: improves the **average across parameter sets**, not one peak. (2) Report blocked-day P&L of each host (positive blocked P&L is the expected cost). (3) For MR hosts (A2-64), test the reverse hypothesis too: high vol may be where fades pay most.
  - **Kill:** no host's net IR rises by ≥ 10% with max DD not higher; **or** the gate blocks < 3% of sessions at θ = 50% on M (then it is inert on this window — keep it only as a disaster rule).

### 3.13 Exit and risk modules (A2-72…A2-79, Minervini via Analyst 2)

These are **modules, not entries**. They are tested as ablations on three fixed hosts, using A2's protocol:
- **Hosts:** H1 = PS-01/Spec 1; H2 = A2-63, with the disaster stop at the open on; H3 = A2-09.
- **When:** only after a host has passed its own kill test.
- **Acceptance:** a module is adopted only if it improves net expectancy or MaxDD/expectancy on **≥ 2 of the 3 hosts**, with no host made worse beyond its CI.
- **Order (A2):** A2-77 → A2-73 → A2-74 → A2-79 → A2-72/A2-75 → A2-76. A2-78's test part runs on the combined book; its operational part (broker-side stops, heartbeat flatten) is mandatory before any live trade.
- **Other exit modules:** A2-34 (V/T revisit target) and A2-48 (destination targets) are in §3.12.

### Minervini trade-management conventions (A2-72 to A2-79)
- **Source:** Minervini, *Think & Trade Like a Champion*, notes in `kindle-notes/minervini-champion-analyst2.md`. The book is discretionary, on US stocks, daily/weekly bars, with no controlled tests; its numbers (avg gain 13.35%, avg loss 5.85%, BA 46%, 95 trades in one sample year) are one trader's results. **Grade scale for this block:** C = a stated numeric rule with an illustrative track record only; D = conceptual. None of these modules has a published futures test, so none is graded above C.
- **What these are:** trade-management **modules**, not entries. No stock-selection rule (Trend Template, Stage 2, RS rank, base count, P/E expansion, earnings, 3-C/cup patterns) is carried over.
- **Fixed hosts (entries never change inside an ablation):**
  - **H1** = Analyst 1 Spec 1 (ABB-HL), with its own structural stop and T1/runner exits;
  - **H2** = A2-63 core (k 0.5, 30m, ATR 20, long-only, exit 15:55), with the disaster-stop-at-the-open arm switched **on** so that R exists;
  - **H3** = A2-09 (MNQ long-only noise band, sVWAP trail).
  - Optional H4 = any other survivor after its own validation (never a host that has not passed its own kill test).
- **R unit (fixed at the entry fill, never revised):** H1 = entry − structural stop; H2 = entry − O (= k × ATR20d plus any slippage); H3 = entry − max(upper band, sVWAP) at the signal bar, floored at 0.25 × ATR(14, 30m). All R and MFE/MAE values are in points from the actual fill, net of A2-59 costs where stated.
- **No lookahead:** every module decision uses closed 1m bars (or the host's TF bar where stated) at or before the decision time and acts at the next 1m bar open (stops as resting stop orders, filled per A2-40/59). "Average gain", "hit rate" and similar statistics are **rolling over the host's own prior closed trades only** (expanding window with a warm-up; never full-sample). Warm-up: the first 30 host trades use the in-sample-period estimate frozen at the IS end only **inside validation/holdout**; inside IS they use the expanding window with the module **off** until N ≥ 30.
- **Ablation protocol (all modules):** (0) host baseline exactly as specified; (1) host + one module; (2) after all single-module runs, the best two surviving modules together (no wider combinations: M14). Report per host: net $/trade, R/trade, PF, hit rate, avg win/loss, max DD, worst day, time in market, and the M6 surface average across the module's parameter grid. Accept a module only per M13 (it lifts the average across its grid and the host's surviving parameter sets, not one peak) and only if it helps on **≥ 2 of the 3 hosts** with the same sign on NQ (M10). Windows: M for all three hosts (H2 also on D via the M20 daily proxy where the module is expressible on daily OHLC; H1/H3 cannot be).
- **Dedup check done against A2-01 to A2-71.** Existing exits that already contain one of these mechanics are named in each "Differs from" line: A2-03 (AVWAP trail after T1), A2-09 (sVWAP trail), A2-48 (poor-high destinations), A2-51 E3 (6-bar time stop), A2-52 (15-minute failure stop), A2-55 (daily contraction gate), A2-70 (high-vol veto), A2-71 (ATR sizing, vol target, drawdown overlay).

#### A2-72 · Progressive exposure ladder 25/50/100% (sizing module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 5 (Fig 5-5/5-6: quarter → half → full position, 6.25% → 12.5% → 25% of equity at 2:1, "scale down after a loss"; "trade small before you trade big", Sec 10 key 2). **Grade:** D (illustrative table, no test; the book's own ladder is per new campaign from cash).
- **Evidence:** **D** · **original A2 grade: D (illustrative table, no test)** (rescaled per §3.12).
- **Markets · TF:** hosts H1 PS-01/Spec 1, H2 A2-63, H3 A2-09
- **Data · test window:** 1m · W-M (agreed split §2.0.1); H2 also on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026) (daily proxy, M20)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every host trade
- **Fails when:** outcomes do not cluster (the ladder only lowers average size)
- **Needs:** F-JRNL, CS-4 sizeForRisk, micro contracts
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes its own kill test
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-71 (ATR per-trade size, portfolio vol target and the Turtle −20%-per-10%-DD overlay: driven by volatility and drawdown depth). A2-72 is a **trade-sequence ladder** driven by the last trade's sign; A2-71's overlay reacts only after a 10% drawdown. Applied **on top of** the A2-71 base size (or 1 lot × 4 micro units when testing edge-only).
  - **Rule:** levels L ∈ {25%, 50%, 100%} of the base size (base = 4 micro contracts in the edge test, so 1/2/4 MES or MNQ). Start each **ladder episode** at 25%. After a closed winning trade (net > 0) move up one level (cap 100%); after a losing trade (net < 0) move down one level (floor 25%); scratch (|net| < 0.1R) leaves the level unchanged. The level for trade n is set from trades ≤ n−1, before the entry.
  - **Reset arms:** (a) never (state persists across days); (b) reset to 25% at each session start; (c) reset to 25% after 2 consecutive losses at any level. Arm "house money": step up only if the cumulative episode P&L ≥ the extra risk taken by the step (Minervini's "profits finance risk").
  - **Parameters:** reset ∈ {a, b, c}; step-down ∈ {one level, straight to 25%} → 6.
  - **Test window:** M (per host). D for H2's daily proxy.
  - **Tests:** (1) Net P&L, IR and max DD vs fixed 100% and vs fixed 50% (the same average exposure: a ladder that only "wins" by being smaller is a null). (2) Serial correlation of the host's trade outcomes (runs test, lag-1 autocorrelation of sign): the ladder has an edge only if outcomes cluster. (3) Shuffled-order control: re-run the ladder on 1,000 permutations of the host's trade sequence; the real result must exceed the 90th percentile.
  - **Kill:** IR not above the fixed-size host at matched average exposure; **or** the real sequence within the shuffled 90% band (no clustering → the ladder only lowers size); **or** helps < 2 of 3 hosts.

#### A2-73 · Breakeven stop at ≈ 3R or above the rolling average gain (exit module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 3/5/10 ("if the stock rises to ≥ 3× your risk, or above your average gain, move the stop to at least breakeven"; "never let a good-size gain turn into a loss"; "protect breakeven, but only after enough movement not to choke the trade"). **Grade:** C (stated numeric rule, no test).
- **Evidence:** **E** · **original A2 grade: C (stated numeric rule, no test)** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3
- **Data · test window:** 1m · W-M (agreed split §2.0.1); H2 also on daily · W-D (dev 2000–2015, val 2016–2024, holdout Jan 2025–Oct 2026)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every host trade that reaches the trigger
- **Fails when:** chokes winners (BE stop-outs that later reach the original exit)
- **Needs:** F-TM, F-JRNL (rolling avg win)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-03 (AVWAP trail after T1) and A2-09 (sVWAP trail), which trail continuously from the start. A2-73 is a **one-time stop move to entry ± costs at a fixed trigger**, with the host's own trail and targets unchanged otherwise. No existing idea has a BE trigger.
  - **Trigger (on a closed 1m bar):** MFE since entry ≥ X, where X is (T1) 3R; (T2) 2R; (T3) the rolling average **winning** trade of that host in R (prior closed trades, conventions), floored at 1R; (T4) max(3R, rolling avg win) — the book's "3R **and** above the average gain".
  - **Action:** stop = entry + (round-trip cost [§2.1 v2.6a: ES $17 = 1.36 ticks, MES $2.50 = 2 ticks] + 1 tick) for longs (true breakeven after costs), placed as a resting stop from the next 1m bar. Never lowered. If the host stop is already above it, nothing changes.
  - **Parameters:** trigger ∈ {T1..T4}; BE offset ∈ {costs + 1 tick, costs + 0.25R} → 8.
  - **Test window:** M.
  - **Tests:** (1) The fraction of host trades that reach each trigger (if < 10% reach 3R on H2/H3, T1 is inert, report and move on). (2) "Choke rate": trades stopped at BE that later reach the host's original exit at a profit. (3) MFE/MAE scatter of the host to locate where winners and losers separate before the run.
  - **Kill:** no trigger lifts the host's net R/trade ≥ 0.02R with max DD not higher; **or** choke rate > 50% at the best trigger; **or** helps < 2 of 3 hosts.

#### A2-74 · Free roll: sell half at 2–3R, remainder at breakeven (scale-out module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 4/9 ("free roll": stock up 2–3× the stop → sell half, stop to breakeven on the rest, or keep the original stop; the sell-half rule "to protect the psyche"; never sell half of a loser). **Grade:** C.
- **Evidence:** **E** · **original A2 grade: C** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3 (≥ 2 micro contracts)
- **Data · test window:** 1m · W-M (agreed split §2.0.1); W-T re-check of partial fills for H1
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every host trade reaching Y·R
- **Fails when:** lower variance at equal or lower IR (neutral, not a pass)
- **Needs:** F-TM, CS-4 (≥ 2 contracts)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes; compared with A2-73 and EX-07
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-73 (BE move with no scale-out) and A1 Spec 1's own T1 (fixed structural target). For H1 this module **replaces** Spec 1's T1 rule (stated as the comparison); for H2/H3 (single-exit hosts) it adds a partial target. Requires ≥ 2 contracts (base = 2 micro units minimum; odd counts round the half **down**).
  - **Rule:** on a resting limit at entry + Y·R, sell 50%. On that fill, the remaining stop moves to (arm BE) entry + costs + 1 tick, or (arm KEEP) stays at the original stop (book alternative: worst case ≈ breakeven overall). The remainder follows the host's own exit (Spec 1 runner/trail, A2-63 15:55 close, A2-09 sVWAP trail). Limit fill only on a 1-tick trade-through (A2-40).
  - **Parameters:** Y ∈ {2, 2.5, 3}; remainder stop ∈ {BE, KEEP}; fraction ∈ {50%, 33%} → 12.
  - **Test window:** M (T re-check of partial fills for H1).
  - **Tests:** (1) Net R/trade and the distribution change (expect lower mean, lower variance; report both — the book's claim is psychological, so a lower-variance, equal-IR result counts as "neutral", not a pass). (2) Compare with A2-73 alone on the same trades. (3) Skew and worst-decile trade.
  - **Kill:** net IR (not R/trade) not above the host; **or** below A2-73 alone on ≥ 2 hosts (then the partial adds nothing over the BE move); **or** generic criteria.

#### A2-75 · Rolling hit-rate throttle (RBA module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 3/4 (tough-market playbook: batting average < 50% → tighten stops 7–8% → 5–6%, take 10–12% instead of 15–20%, cut exposure; "do not widen stops for volatility"; Result-Based Assumption: stops driven by actual average gain/loss; "Adjust. Do it!"). **Grade:** D (no test; the trigger is discretionary in the book).
- **Evidence:** **D** · **original A2 grade: D (no test; the trigger is discretionary in the book)** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** ON 15–35% of the time (A2 expectation)
- **Fails when:** a low rolling hit rate does not forecast the next 10 trades
- **Needs:** F-JRNL, F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** predictive check first, then ablation
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-70 (absolute HV20 veto, market state) and A2-71 (vol target, drawdown overlay: P&L size, not hit rate). A2-72 reacts to the last trade only. A2-75 reacts to the **rolling hit rate of the host's last N trades** and changes **stop/target geometry**, optionally size. Note the book's "don't widen in high vol" conflicts with ATR-based stops used by H2: ATR stays the base, only the multiplier shrinks.
  - **State (before each entry, from prior closed trades of that host):** BA_N = winners / N over the last N trades; baseline BA* = the host's IS hit rate (frozen at the IS end). Throttle ON when BA_N < BA* − δ; OFF when BA_N ≥ BA* (hysteresis).
  - **Action when ON:** (i) stop distance × s (s < 1, by moving the stop closer; the host's position size is **unchanged**, so risk $ falls); (ii) arm: the target/T1 distance × s as well (book: "settle for smaller profits"); (iii) arm: size × 0.5 instead of geometry changes (pure exposure throttle, the control).
  - **Parameters:** N ∈ {10, 20}; δ ∈ {5, 10} pp; s ∈ {0.67, 0.8}; action ∈ {i, i+ii, iii} → 24 (above M14's comfort; pre-register core N 20, δ 10, s 0.8, action i+ii, and judge on the M6 average).
  - **Test window:** M.
  - **Tests:** (1) Predictive check first: does a low BA_N forecast a low hit rate in the next 10 trades? (logistic regression of next-trade win on BA_N; if the slope's CI spans 0, the throttle cannot work — stop.) (2) Ablation of i, i+ii, iii; (iii) is the control: if halving size does as well as changing geometry, the geometry claim fails. (3) Count ON-time share (expect 15–35%).
  - **Kill:** step (1) slope not significant at p < 0.10 on IS for ≥ 2 hosts; **or** no action beats the host at matched exposure; **or** (i)/(i+ii) not better than (iii).

#### A2-76 · Sell into the climax bar (profit-exit module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 9 (sell-into-strength signals: largest up day since the move began, widest high-to-low spread, 70%+ up bars over 7–15 bars, churning, exhaustion gaps; "sell into the rally, don't wait for the crash") and Sec 10 key 1 ("pros sell into strength"). **Grade:** D (daily-chart heuristic; intraday analogue untested).
- **Evidence:** **D** · **original A2 grade: D (daily-chart heuristic; intraday analogue untested)** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3 (long)
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** a subset of open winners
- **Fails when:** intraday climax bars are momentum, not exhaustion (A2-51, Gao et al.)
- **Needs:** ✓ M-TOD (SSB of range and volume), F-TM
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-48 (poor highs as **destinations**: a price target), A2-51 (fades a shock bar as an **entry**), and A2-22 (cumulative-delta divergence exit, window T). A2-76 exits an **open winner** on a bar-shape condition relative to the trade's own bars, on window M with no delta.
  - **Definitions (bar TF b, closed bars since entry, RTH only, same-slot normalised per M16):** range_rel = (H − L) / SSB_range(τ); up_rel = (C − O) / SSB_range(τ) for up bars.
    - **C1 widest bar:** current bar's range_rel is the largest since entry **and** ≥ q (q = 2.0) **and** the trade's open P&L ≥ 1R.
    - **C2 largest up bar:** the largest up_rel since entry, ≥ q, open P&L ≥ 1R.
    - **C3 up-bar run:** ≥ 70% up bars over the last 10 bars **and** open P&L ≥ 2R (book's 7-of-10 on extended stocks).
    - **C4 churn:** bar volume ≥ 2 × SSB_vol(τ) with |C − O| ≤ 0.25 × (H − L), open P&L ≥ 1R.
  - **Action:** exit the full position (arm: half, remainder on the host exit) with a limit at the climax bar's close + 1 tick for the next 2 minutes, then market. Long hosts only (all three hosts are long).
  - **Parameters:** condition ∈ {C1, C2, C3, C4}; b ∈ {5, 15}m; q ∈ {1.5, 2.0} (C1/C2 only) → 12; size ∈ {full, half} as an arm.
  - **Test window:** M.
  - **Tests:** (1) Event study: forward return over 30/60 minutes after C1–C4 bars inside open host winners vs the same bars outside trades and vs matched random bars of the host (does a climax bar mark a local top intraday, or is it momentum? A2-51's and Gao et al.'s evidence points to momentum). (2) Ablation. (3) Report give-back avoided vs upside forgone.
  - **Kill:** forward 30-minute return after the climax bar ≥ 0 (no exhaustion) for the best condition on both ES and NQ; **or** net R/trade not above the host on ≥ 2 hosts; **or** generic criteria.

#### A2-77 · Failed follow-through exit: time stop and bar-quality test (exit module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 1 (expect follow-through: more up bars than down, 3 of 4 / 6 of 8, closes in the upper half; red flags: low-volume breakout then high-volume reversal, more bad closes than good; Ryan: "if I don't see a profit soon after I buy, get out"; "winning horses don't back up into the gate"; squat-and-recover allowed for a limited time). **Grade:** C (stated heuristics; intraday time stops are documented elsewhere in the file but not on these hosts).
- **Evidence:** **E** · **original A2 grade: C (stated heuristics)** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** every host trade (one checkpoint)
- **Fails when:** failed trades have non-negative remaining P&L from the checkpoint
- **Needs:** F-TM (TimeStopBars), M-TOD (F3)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes; generalises A2-52's failure stop and EX-06
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-52's 15-minute failure stop (one host, one rule, P&L only) and A2-51 E3 (6-bar time exit for a fade). A2-77 is a **host-agnostic module with a bar-quality test**, run on H1–H3, none of which has a time stop (H1 has a session close, H2 a 15:55 close, H3 a band/sVWAP trail).
  - **Checkpoint:** at entry + τ (closed bars of TF b after the fill), evaluate:
    - **F1 P&L:** open P&L ≤ 0 (the Ryan rule).
    - **F2 bar quality:** fewer than 3 of the first 4 bars closed up, **or** fewer than half of them closed in the upper half of their range.
    - **F3 volume reversal:** the largest-volume bar since entry is a down bar with volume ≥ SSB_vol(τ) (M16).
    - Exit at the next 1m open if the chosen condition holds. One checkpoint only (no rolling re-checks, to limit the parameter count).
  - **Squat allowance arm:** if F1 fails but the trade's MAE has not reached 0.5R, extend to a second checkpoint at 2τ (book: let it recover "within days" — here, one extra window).
  - **Parameters:** b ∈ {5, 15}m; τ ∈ {4, 8} bars; condition ∈ {F1, F2, F1∨F3} → 12; squat arm on/off.
  - **Test window:** M.
  - **Tests:** (1) Conditional P&L table: host trades split by checkpoint state (pass/fail) — the module is justified only if failed trades have negative mean remaining P&L **from the checkpoint onward** (the correct counterfactual; not their full-trade P&L). (2) Ablation. (3) Time-in-market reduction (M12 prefers less time at equal expectancy).
  - **Kill:** mean remaining P&L of "failed" trades ≥ 0 at the best setting on ≥ 2 hosts; **or** net R/trade not above the host; **or** generic criteria.

#### A2-78 · Daily loss kill-switch and rules-first flatten (risk module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 1/2/5/10 (disaster plan, backup connection, hedge account; "stop after you are wrong", "never revenge trade / double down", "trade smaller or take a break when timing is off"; Synthesis items 7–8). **Grade:** D for edge (no evidence it improves expectancy); mandatory as an operational control regardless.
- **Evidence:** **D for edge; mandatory as an operational control** · **original A2 grade: D for edge (no evidence it improves expectancy); mandatory as an operational control** (rescaled per §3.12).
- **Markets · TF:** whole book (all live strategies on the IBKR account)
- **Data · test window:** 1m · W-M (agreed split §2.0.1) on combined host P&L, time-ordered
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** A2 expects < 5% of sessions at D = 3
- **Fails when:** forgoes positive afternoons (accept only if the worst-day tail falls ≥ 30%)
- **Needs:** F-TM (daily loss stop), ◐ M-PORT (coded, PRs #14/#17/#22; P5b live checks pending), broker-side stops, heartbeat
- **Readiness (v2.3b):** Buildable with caveats — ◐ M-PORT is coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`); run Probe P5b live checks before reading any portfolio result.
- **Gate (v2):** operational part before any live trade; test part on surviving hosts
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Differs from:** A2-71's drawdown overlay (size after −10% from peak, multi-week) and A2-72 (trade sequence). A2-78 is an **intraday hard stop on the whole A2 book**, plus an operational flatten.
  - **Rule (book level, all live A2 strategies on the IBKR account):** daily realised + open P&L, marked on closed 1m bars at dxFeed last (arm: IBKR mark), from 18:00 ET (Globex day) or 09:30 (RTH-only arm). If P&L ≤ −D × daily risk budget: cancel working entries, flatten all positions at market, disable new entries until the next session. Arm "two-strike": also stop for the day after K consecutive losing trades across the book.
  - **Operational part (not ablated, always on live):** broker-side resting stops for every open position (never MC-only); a heartbeat check (no dxFeed tick for 30 s in RTH, or MC-IBKR position mismatch) → flatten via the IBKR TWS/Gateway order path and alert; a manual "flatten-all" control tested weekly in paper. Re-enable is manual after a heartbeat flatten.
  - **Parameters (test part):** D ∈ {2, 3, 4} × the per-trade risk unit R$; K ∈ {off, 3} → 6.
  - **Test window:** M on the combined daily P&L of surviving hosts (H1–H3 and any survivor), with intraday timestamps so the trigger is applied in time order.
  - **Tests:** (1) Effect on mean daily P&L, IR, worst day, worst month and max DD. (2) "Afternoon after the trigger": P&L that the switch forgoes (if positive on average, the switch costs edge — accept it only if it cuts the worst-day tail ≥ 30%). (3) Trigger frequency (expect < 5% of sessions at D = 3).
  - **Kill (test part only):** IR falls > 10% **and** the worst day does not improve by ≥ 30%. The operational part is never killed.

#### A2-79 · Intraday volatility contraction + volume dry-up entry gate (VCP analogue; module)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** XR (exit/risk module) · Analyst 2 Rev 6 · Minervini Sec 6/7 (VCP: 2–6 contractions, each about half the prior; volume dries up on the last contraction, below the 50-day average, with one or two very low-volume bars; tight closes; buy the break of the pivot on expanding volume; footprint "T–depth/last–count"). **Grade:** D (a daily/weekly chart pattern; Synthesis: intraday analogues "need empirical testing").
- **Evidence:** **D** · **original A2 grade: D (a daily/weekly chart pattern; intraday analogue needs empirical testing)** (rescaled per §3.12).
- **Markets · TF:** hosts H1–H3, A2-52, A2-41
- **Data · test window:** 1m · W-M (agreed split §2.0.1)
- **Sweep:** the pre-registered ranges in A2's specification below (M2/M3; ≤ 30 configs; W-T ≤ 4).
- **Frequency (est.):** keeps 10–30% of host trades (A2 expectation)
- **Fails when:** the dry-up leg adds nothing over contraction-only; ES-only improvement
- **Needs:** ✓ M-SWING (2-bar zigzag = Fractal strength 1), ✓ M-TOD (SSB volume)
- **Readiness (as of merge):** Buildable now on the merged base.
- **Gate (v2):** only after a host passes; AND-ed with RG-08/A2-55
- **Controls · Kill:** standard controls (§2.3) + A2's controls and kill in the specification below; A2's generic kill (§2.3) also applies.
- **Analyst 2's specification (verbatim, Rev 6; "window M/T/D" = W-M/W-T/W-D with the agreed split §2.0.1):**
  - **Dedup vs A2-55 (checked):** A2-55 gates on **prior-day** daily contraction (inside day, NR4, HV6 < HV100, range/ATR) computed at the prior close. It has no intraday swing-contraction sequence and no volume condition. A2-79 is **intraday, in the hour(s) before the host's entry**, and requires a **volume dry-up**; it is not covered by A2-55 and is tested as a separate gate (and as A2-55 ∧ A2-79). The catalogue NR7 negative result and A2-70 (high-vol veto) are the other neighbours.
  - **Construction (5m RTH bars, closed only, before the host signal bar):**
    - Swing pullbacks: confirmed 2-bar zigzag pivots (a pivot is usable only 2 bars after it forms). A contraction c_i = (swing high − subsequent swing low) / ATR(14, 5m).
    - **VCP state** at the host signal bar: the last n contractions within the lookback W satisfy c_i ≤ r × c_{i−1} for all i, n ≥ 2, the last contraction ≤ 0.75 × ATR(14, 5m), **and** dry-up: the mean volume of the last contraction's bars ≤ v × SSB_vol (M16, same-slot 20-day mean) with at least one bar ≤ 0.5 × SSB_vol.
    - Optional pivot rule (book): host entry allowed only above the high of the last contraction (no change for H1/H2, whose triggers are already breakouts; for H3, an extra condition).
  - **Hosts:** H1, H2, H3 (entries unchanged; the gate only removes trades), plus A2-52 and A2-41 for continuity with A2-55.
  - **Parameters:** r ∈ {0.5, 0.75}; n ∈ {2, 3}; v ∈ {0.7, 0.85}; W ∈ {60, 120} min → 16 (core r 0.75, n 2, v 0.85, W 120).
  - **Test window:** M.
  - **Tests:** (1) Gated vs ungated host $/trade, PF, trade-count fraction (expect 10–30%). (2) Contraction-only vs dry-up-only vs both (does the volume half add anything?). (3) Overlap and AND with A2-55's best gate. (4) Placebo: the same contraction rule on volume-shuffled bars (P2-style) for the dry-up leg.
  - **Kill:** no setting lifts net expectancy ≥ 0.05R on ≥ 2 hosts with ≥ 100 gated trades; **or** the dry-up leg adds < 0.02R over contraction-only (then drop the volume claim and fold the contraction leg into A2-55 as a new gate number); **or** ES-only improvement (M10).


  ---

### 3.14 Family DV: Trader Dale's YouTube strategies (DV-01…DV-24)

Source: `/workspace/trading-ideas-dale-video.md` (transcripts of 63 strategy videos from https://www.youtube.com/channel/UC1_SoqTCXaM8kk0SxkAyhcQ).
- **Grades:** every DV idea is **grade E**, because the only numbers are Dale's unaudited journal win rates. DV-15 has a tiny in-sample test of his own (2 months of EUR/USD). The DV ratings (Dale priority 1–5, books agree/conflict) are kept verbatim.
- **Merges:** 12 DV ideas are merged into existing entries, as **Merged · DV-xx** inside the host. They are DV-03→PS-13, DV-04→PS-12, DV-05→PS-14, DV-10→VW-08 (canary arm iv), DV-12→AV-02, DV-13→AV-11, DV-14→AV-08, DV-17→OF-08, DV-18→OF-05, DV-19→OF-06, DV-21→DV-02 and DV-24→EX-01 (note after the EX table).
- **Own entries:** the other 12 are below. **DV-01 is a control (type K)**, run in the Gate-0 canary slot. Each own entry adds the catalogue fields the video file lacked (window, sweep, frequency, fails, needs, gate), marked as additions.

**Context for every DV idea (verbatim):**

- Dale trades mainly forex majors (EUR/USD, GBP, AUD, CAD, CHF, JPY) and also ES/NQ, on a **30-minute chart** ("my favorite time frame … last 10 or 12 years").
- His standard intraday bracket is **TP 10 pips / SL 12 pips**, or 10–20% of average daily range, at about 1:1.
- The only statistics he shows are from his own journal: a 66% combined win rate since 2023 at 1:1 for three VP setups, 71% on EUR/USD (grEU59ExG_k), and 67% over 1,500+ published predictions (Nj2Gb-x7lIA). These are unaudited vendor figures, so **grade E**. They include his discretionary confluence and are not per setup.
- **Port to dxFeed:** EUR/USD → 6E. 10–20% of ADR → 0.10–0.20 × ATR20d (daily, roll-gap-removed). For ES, ATR20d of about 60–90 pts gives roughly 6–18 pts.

**Data windows (catalogue §2.0).**
- **W-M:** dxFeed 1-minute bars, ≈ Jun 2016 → Oct 2026. Completed profiles are exact; developing profiles are approximate.
- **W-T:** ticks with the bid/ask split, only ≈ Jul 2024 → Oct 2026 (≈ 500 sessions). Every footprint idea is W-T only.
- **W-D:** daily bars.

**Book priors used for the ratings** (final-synthesis, Kindle notes, catalogue):
- (P1) Confirmation and continuation beat first-touch fades.
- (P2) Profile levels work as destinations, not as entry signals.
- (P3) The 80% rule completes about 31%.
- (P4) Naked POCs are no better than distance-matched control prices.
- (P5) Fading VWAP has no edge: bounce 50.2%, band fade PF 0.78, reversion PF 0.79, trend PF 0.89.
- (P6) Footprint signals are untested.
- (P7) Real levels get run through more often than fakes (Telonics +3.7 to +11.6 pp), which supports flip/continuation trades.

**DV summary table and overlap map (verbatim):**

| ID | Strategy | Evidence | Books agree/conflict | Priority | Overlaps with |
|---|---|---|---|---|---|
| DV-01 | Daily POC first-touch pullback | E (vendor) | Conflict (P1, P2, P4) | 2 (control/baseline) | PS-21, PS-10, EX-02 |
| DV-02 | Failed level → reversal (flip) trade | E | **Agree** (P1, P7; SP-07) | **5** | SP-07, PS-03 (inverse), PS-15, Dale book "Reversal trade" |
| DV-03 | Volume accumulation (rotation → trend, first retest) | E | Mostly agree (PS-01/PS-13 family C) | **4** | PS-13, PS-01, PS-02, Dale book #1 |
| DV-04 | Trend-cluster pullback (+ S/R-flip confluence) | E | Agree (continuation, P1) but first-touch limit | **4** | PS-12, PS-10, OF-01, SP-08, Dale book #2 |
| DV-05 | Rejection HV-zone retest with 4-point checklist | E | Conflict (fade at level, P1) | 2 | PS-14, SP-05, Dale book #3 |
| DV-06 | End-of-trend-day cluster pullback (NY close) | E | Neutral | 2 | PS-12, TS-03, PS-23 |
| DV-07 | Rotation / double-distribution edge → centre | E | Conflict (P3 analogue; Keller says centre is a destination) | 2 | PS-17, PS-03, VW-02 |
| DV-08 | HV zone + FVG alignment (level at FVG start) | E | Untested layer | 3 | PS-13, PS-12, PS-20 (single prints ≈ FVG), F-CONF |
| DV-09 | Weekly-VWAP ±1σ trend pullback | E (+ his ~60% claim) | Conflict (P5: VWAP trend PF 0.89) | 2 | VW-01, VW-03, AV-08 |
| DV-10 | Weekly-VWAP ±1σ rotation fade → VWAP | E | **Conflict** (P5 band fade PF 0.78) | 1 (canary) | VW-02, VW-08 |
| DV-11 | VWAP trend-failure (lose +1σ → short pullback → VWAP) | E | Partly agree (failed-trend = confirmation) | 3 | VW-04, AV-12, SP-07 |
| DV-12 | Prominent-swing AVWAP pullback | E | Conflict (P5 / Telonics AVWAP 51%) | 2 | AV-01, AV-02, MT-04 |
| DV-13 | Accumulation-end AVWAP pullback (+ flip) | E | Neutral; confluence untested | 3 | AV-02, AV-11, AV-10, PS-13 |
| DV-14 | Week-open AVWAP S/R with side flip | E | Conflict (P5) | 1–2 | AV-08, VW-07 |
| DV-15 | EMA20 pullback gated by VWAP slope | **E+ (his small test: 2 mo EUR/USD, 138→73 trades, 48.5%→60% at 10/10 pips)** | Agree (trend filter on a pullback) | **4** | VW-01, RG-04, RG-06 |
| DV-16 | Order-flow confirmation at a level (absorption / big limit / aggressor / trapped) | E | P6 untested; P1 agrees in principle | 3 (layer) | OF-04, OF-07, OF-08, OF-02, SP-07 |
| DV-17 | Large-trade (trades-filter) zone pullback | E | P6 untested | 2 | OF-08 |
| DV-18 | 4h-profile POC + cumulative-delta divergence (1m) | E | P6; CVD divergence untested | 2 | OF-05, PS-21, EX-12 |
| DV-19 | Failed-auction cluster as magnet/target (+ veto) | E | Agree as a *target* (P2) | 3 (exit layer) | OF-06, PS-19, MT-06, EX-02 |
| DV-20 | Stop-loss-cluster ("weak low/high") veto | E | Agree (liquidity runs; PS-19) | 3 (filter) | PS-19, PS-18, A2 sweep ideas |
| DV-21 | Price-action S/R flip (standalone) | E | Agree in direction (P7) | 3 | SP-07, SP-08, PS-01 |
| DV-22 | ABCD (AB = CD) pattern | E (he claims "65% win rate" in title only) | No book support | 1 | none (new) |
| DV-23 | IB draw trade (guest Dan/FTA, not Dale) | E (claims 96.4% IB-touch) | Partly (IB-touch rate is a population fact; Keller IB) | 3 | PS-08, PS-09, RG-04 |
| DV-24 | Exit module: TP before barrier, SL behind barrier, daily-close stop + catastrophic stop | E | Agree (EX-01; P2) | **4** (exit) | EX-01, EX-11, EX-10, Dale book SL/PT |

**Overlap notes (verbatim):**

**DV-01 vs the catalogue.**
- **PS-21** (naked POC first touch with a trigger; B(0), kept as a control) is the closest. DV-01 is PS-21 *without* the trigger and with the age limited to recent daily POCs. Expect the same null (P4).
- **PS-10** (developing-POC migration join) uses the POC as support only after migration, which is a confirmation version.
- **EX-02** uses a naked POC as a *target*, which is Keller's preferred use.
- DV-01's "reversal trade" is split out as **DV-02**, which maps to **SP-07** (failed-failure re-break) and the PS-01 acceptance logic.

**DV-01 vs the Dale book notes (`kindle-notes/dale-volume-profile.md`).**
- **Same:** POC as the key level; TP/SL 10–20% of average daily range (the book says "of 200-day daily ATR", PT10/SL12 pips); about 1:1; the "Reversal trade (level breached and stopped out, re-enter opposite on return)"; news avoidance ("check calendar").
- **Different:**
  - The book's setups are *volume clusters* (accumulation / trend / rejection) on 30m with a flexible profile. The video uses only the *daily POC*.
  - The book adds a 240m/daily trend filter ("only with-trend in strong trend") and a 30m price-action map. The video has no trend filter.
  - The book has a tested-level filter (a 0–3-pip approach then a ≥ 8-pip reaction discards the level) and a 30–40-day validity. The video's "first touch only" is the strict version.
  - The book has no statistics; the video only gives journal win rates.
- DV-03/04/05 are the book's setups #1/#2/#3 almost verbatim. DV-24 = the book's "volume-based PT before next cluster; SL behind cluster/swing".

**Other overlaps:** see the table column. Main clusters:
- PS-12/PS-13/PS-14 = DV-04/03/05;
- VW-01/VW-02/VW-04/VW-08 = DV-09/10/11;
- AV-01/02/08/10/11 = DV-12/13/14;
- OF-04/05/06/08 = DV-16/18/19/17;
- PS-08/09 = DV-23;
- EX-01/EX-11 = DV-24.

#### DV-01 · Daily POC first-touch pullback ("The Simplest Volume Profile Intraday Strategy for Beginners")
- **Implementation status (v2.4):** **Implemented** — As the G0-b POC first-touch control (canary DV01) in `_Gate0` / `_TestGate0` (PR #20; [#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30)); the trade form is not coded. Not run in MC.
- **Catalogue grade:** **E** (+ Dale priority 2 (control/baseline)) · **original DV grade: E (vendor)** · books agree/conflict: Conflict (P1, P2, P4) · family PS (control).
- **Catalogue role:** control (type K). Baseline for DV-02/DV-04 and the PS-13/DV-03 merge; companion of PS-21 (same null expected, P4). Runs in the G0 canary slot. Level type must also be scored in A2-33 (prior-session POC).
- **Family · Source:** DV · https://www.youtube.com/watch?v=uDvT4uDWgB4 (primary). The same rule plus his preferred "beginning of the heavy-volume zone" variant is in https://www.youtube.com/watch?v=wwf_XM31XIg.
- **Evidence:** **E.**
  - No statistics are given for this setup. He says it "in the long run … is profitable. In the short term … it is not consistent", with losing streaks.
  - His journal figures (EUR/USD 71%, USD/CAD 59%, ES 67%; worst on Mondays, best on Fridays) are for *his* strategy with confluences, not this setup.
  - Lit: Practitioner.
- **Markets · TF [Dale]:**
  - Examples are EUR/USD intraday; ES is mentioned in his journal; he says to trade 2–3 instruments.
  - Profile = TradingView "Session Volume HD" daily profile with "extend POC to the right until tested".
  - The chart timeframe for this video is **not stated** (the examples look intraday; elsewhere he uses 30m).
  - **[coding]** ES/NQ (MES/MNQ), 6E, CL, GC; daily RTH profile (ETH variant); 1m execution.
- **Data · test window:** W-M (Jun 2016 → Oct 2026). Completed daily profiles are exact on 1m. Levels are ≤ 1 session old, so the roll only affects the first session after the splice.
- **Why it might work [Dale]:**
  - "Heavy volumes are significant support and resistance zones… especially… the point of control."
  - (1) "Buyers defend their longs": their average price sits at the POC.
  - (2) "Sellers are quitting their shorts": covering = buying.
- **Rules:**
  - *Level [Dale]:* "wait until the daily profile is fully formed… until the day is over"; use that day's POC. **[coding]** POC of session D, computed only after D's RTH close (F-PROF); valid from D+1 open.
  - *Move-away [Dale]:* "price moves away from the point of control". The distance is **not stated**. **[coding]** After the level is created, price must trade ≥ k × ATR5(14) (or ≥ the stop distance) away from the POC before a touch counts. Opening-gap days count as move-away (his third example "opened with a gap").
  - *Direction [Dale]:* "when the price hits the point of control from above, then you go long… from below… you go short". **[coding]** Side = sign(price − POC) at the moment move-away is satisfied.
  - *First test only [Dale]:* "only the first touch". **[coding]** One trade per level. If the level is touched before move-away is satisfied, the touch counts as the test and the level is discarded (my choice). The level stays live until first touched; maximum age is **not stated**. **[coding]** age ≤ N sessions; the book says levels stay valid for 30–40 days.
  - *Entry [coding]:* limit order at POC (± 0 ticks), filled when price trades through the limit by ≥ 1 tick (conservative fill; K-LM).
  - *Stop / Target [Dale]:* fixed and symmetric, "10 to 20% of average daily volatility… risk-reward ratio one" (EUR/USD 80-pip ADR → 8–16 pips, mid 12). **[coding]** SL = TP = f × ATR20d(daily, roll-adjusted), f ∈ {0.10, 0.15, 0.20}.
  - *Filter [Dale]:* "avoid high-impact news… quit your trades 5 minutes before… wait… 10 minutes after". **[coding]** M-NEWS blackout [−5, +10] min around tier-1 events: flatten open positions, no new entries. A session end flat-by rule is **not stated** → **[coding]** FlatBy 15:55 ET.
  - *Optional improvements [Dale]* (not part of the base rule): confluence with weekly-VWAP σ1, an FVG start, order-flow confirmation; fine-tune by symbol/day using a journal. **[coding]** Treated as separate layers (DV-08, DV-16) and not used in the baseline.
- **Sweep:** f ∈ {0.10, 0.15, 0.20}; move-away k ∈ {0.5, 1.0, 2.0} ATR5; level age ∈ {1 session (yesterday only), ≤ 5 sessions} → 18 configs.
- **Frequency (author estimate, not measured):** ES: about 2–4 qualifying first touches per week on yesterday's POC alone, about 10–15 per month. Higher with older naked POCs.
- **Fails when:** trend days with POC far behind; the POC sits inside a balance where "move away" is noise; the 1:1 bracket is smaller than the noise (cost floor); naked-POC "reaction" is the same as control prices (P4).
- **Needs:** F-PROF (POC), F-NPOC (untested tracking), F-SESS, F-TM, F-JRNL, F-EVT; ATR20d (✓ M-DAILY); ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29).
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. Caveat: run gated by A2-33 verdict (harness ◐; Gate-0 not yet run). Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:**
  - K-FL is mandatory (shift POC ±0.5/1/2 ATR5, distance-matched); K-ST (POC from 5 sessions back); K-RT; K-RW (1:1 → 50%, net break-even ≈ 55% with c = 0.1 R); K-DR (longs/shorts separately).
  - **Kill:** first-touch reaction ≤ fake levels, or net win rate < break-even at ≥ 150 events.
  - Keep it as the **baseline/control** for DV-02 and DV-03.
- **Books agree/conflict:** **Conflict.** A first-touch fade (P1), a level used as an entry (P2), and close to PS-21 (naked POC, B(0)). It agrees with the Dale book ("limit on first touch, no confirmation"; TP/SL 10–20% of ATR; trade with the trend only in strong trends). **Priority 2.**

#### DV-02 · Failed level → reversal (flip) trade
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 5) · **original DV grade: E** · books agree/conflict: Agree (P1, P7; SP-07) · family SP.
- **Gate:** A2-33 verdict for the source level type (POC, accumulation, cluster, swing extreme) first; A2-40/59 costs on.
- **Fails when (add):** the cost floor exceeds the fixed f × ATR20d bracket on ES at f = 0.10 (skip, not resize).
- **Sources:**
  - https://www.youtube.com/watch?v=uDvT4uDWgB4 ("reversal trade, not a revenge trade")
  - https://www.youtube.com/watch?v=O8bhSguZ9U4 (What to do when POC fails)
  - https://www.youtube.com/watch?v=gXmdBGQBAP8 (Turn a losing trade into a winner)
  - https://www.youtube.com/watch?v=RSDMzztosis (Complete guide to reversal trades)
  - https://www.youtube.com/watch?v=nbdLCKBGv7k (weekly POC traded "from the other side")
- **Evidence:** E · Lit: Practitioner + Book (Dale VP "Reversal trade").
- **Markets · TF:** any of his levels (POC, accumulation, trend cluster); 30m [Dale]. **[coding]** ES/NQ, 6E, CL, GC; 5m/30m.
- **Data · test window:** W-M.
- **Why [Dale]:** "when the price just shoots past a daily point of control… with no reaction at all… we are dealing with strong buyers… join them." Buyers "make a pullback to that level to get some liquidity." The ideal case is "shoots past… very quickly… could be macro news… that's perfect."
- **Rules:**
  - *Trigger [Dale]:* the original trade at the level was stopped out (or the level was "shot past" with no reaction).
  - *Entry [Dale]:* "wait for pullback to that… exact point of control… enter… from the exact same level", opposite side; "set a limit order"; "you need to be quick".
  - **[coding]** After a DV-01/03/04 level L is touched and price closes ≥ s × ATR5 beyond L on the far side within M bars of the touch (s = the original stop distance), arm a limit at L on the new side. Valid for ≤ X bars. One reversal per level.
  - "No reaction" **[coding]:** max adverse excursion against the break direction after the touch < 0.25 × stop before the break-through.
  - *Stop/Target:* same fixed bracket as the original [Dale: implied, **not stated** explicitly]. **[coding]** Same f × ATR20d bracket; variant TP at the next HVN (DV-24).
  - *Filter:* none stated beyond news; Dale welcomes a macro-news break. **[coding]** Test both the news-break and no-news subsets.
- **Sweep:** s ∈ {1.0, 1.5} × stop; arm window X ∈ {1 session, 3 sessions}; bracket f ∈ {0.10, 0.15} → 8.
- **Frequency (est.):** about 30–45% of DV-01/DV-03 levels fail. On ES that is about 4–8 per month.
- **Fails when:** the break is a stop-run that reverses again (double trap); the pullback never comes (fast trend); the original level was a fake level (flip of noise).
- **Needs:** level generators (DV-01/03/04), F-TM, F-EVT; ◐ M-EVENT (M-EVENT PRs #25/#27/#28/#29) (to tag news breaks).
- **Readiness (v2.3b):** Buildable now; the scheduled-event filter/arm uses ◐ M-EVENT (`_EventCalendar`, `EventBlackout`; M-EVENT PRs #25/#27/#28/#29). Unscheduled news is not covered. Caveat: needs the DV-01/03/04 level generators; run gated by A2-33 verdict (harness ◐; Gate-0 not yet run); scheduled news breaks only; unscheduled subset untestable. Caveat: GDP/PCE coverage holes 2016-24 (EV-4.6b).
- **Test:**
  - Controls: K-FL (flip at fake levels), K-LM (limit vs a stop entry above a confirmation bar); compare with SP-07 (failed-failure re-break) and with PS-01 acceptance pullback.
  - **Kill:** no lift over fake-level flips, or net R ≤ 0.
- **Books agree/conflict:**
  - **Agree.** It is an acceptance/continuation entry in the break direction (P1), and Telonics shows real levels are run through more than fakes (P7).
  - It matches the Dale book ("level breached and stopped out, re-enter opposite on return") and catalogue SP-07/PS-01.
- **Priority 5.**
- **Merged · DV-21 (price-action S/R flip: swing-extreme levels as a level-source variant of this flip retest; Dale priority 3).** Original DV grade: E → catalogue **E**. Books: Agree in direction (P7). Dale's specification, verbatim:
  - **Source:** https://www.youtube.com/watch?v=v2dOQ0l3J1s (setup 1: a level with ≥ 1 strong reaction, "the more the better"; price blows past it; trade the pullback). Also https://www.youtube.com/watch?v=RFVY939jV0c.
  - **Rules:**
    - **[coding]** A confirmed swing extreme with reaction ≥ 1.5 × ATR30m.
    - Later a 30m close beyond it by ≥ 0.25 × ATR30m.
    - First pullback: limit at the level in the break direction. Reactions count ∈ {1, 2+}.
  - **Evidence:** E.
  - **Books:** **Agree** in direction (P7; SP-07/SP-08). **Priority 3.**
- **Holm · Keller B5 (v2.3):** member of the Holm family {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, family-wise α = 0.05, fixed before any dev read (§3.16). KD-07's no-fade veto is never layered on DV-02.


#### DV-06 · End-of-trend-day cluster pullback (into the NY close)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 2) · **original DV grade: E** · books agree/conflict: Neutral · family PS.
- **Markets · TF:** ES/NQ (port of 30m forex); 30m signal, 1m fill.
- **Data · test window:** 1m · W-M (agreed split §2.0.1); same-session levels only (no roll exposure).
- **Why it might work [Dale]:** late-day calm after a trend day lets the trend cohort defend the last heavy-volume zone.
- **Kill:** standard Gate-3 kill (§2.3) or no lift over K-RT in the same window.
- **Source:** https://www.youtube.com/watch?v=ExX17Jp-uyo.
- **Evidence:** E ("very good win rate in my experience", no numbers).
- **TF:** 30m forex [Dale].
- **Rules:**
  - *[Dale]* "strong trend day… at the end of the day… look with the flexible volume profile into the trend zone… heavy volumes around the low of the day… often tested with little pullbacks"; "trade… towards the New York close as the price tends to calm down"; "sometimes you need to be quick".
  - **[coding]**
    - Trend day = RG-04 day-type trend at 14:00 ET (or close-to-open ≥ 0.7 × day range).
    - Level = the HVN of the session profile nearest the trend extreme (the heavy volume near the low in a downtrend), formed in the last 3 hours.
    - Enter a limit at its near edge in the trend direction (resistance in a downtrend = short; support in an uptrend = long). Dale describes it as the high of the post-trend rotation.
    - Window 14:00–15:55 ET (ES) / NY close (6E); flat by session end.
  - Stop/Target **not stated** → **[coding]** f × ATR20d, f ∈ {0.10, 0.15}.
- **Sweep:** f × window ∈ {13:30, 14:30 start} → 4.
- **Frequency (est.):** about 2–4 per month per market.
- **Fails when:** late trend continuation (MOC flows, TS-03).
- **Needs:** F-PROF developing, RG-04, F-HVN.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-RT within the same window, TS-03 drift control. **Kill:** no lift over K-RT.
- **Books:** neutral (late-day continuation VW-05 suggests the risk). **Priority 2.**

#### DV-07 · Rotation / double-distribution edge → centre
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 2) · **original DV grade: E** · books agree/conflict: Conflict (P3 analogue) · family PS.
- **Markets · TF:** ES/NQ, 6E; 30m signal on a 3–10-session composite.
- **Data · test window:** 1m · W-M (agreed split §2.0.1); composites reset at roll splices (A2-39).
- **Needs (add):** F-HVN KDE trough between two HVNs; ✗ range-profile helper not needed (composite).
- **Gate:** A2-33 (composite HVN peaks); the PS-03 / A2-42 canary result is the prior.
- **Sources:** https://www.youtube.com/watch?v=SIzxVo-ylFU (double distribution: "shorts from [upper HVN], longs from [lower HVN], take profit towards the center… becomes a D-shaped profile") and https://www.youtube.com/watch?v=wcvOQz_8XUc (rotation: "trade from the extremes… target the center… no trailing"; he says markets rotate 70–80% of the time).
- **Evidence:** E.
- **Rules:**
  - **[coding]** RG-02 balance over a multi-day composite (3–10 sessions).
  - A double distribution = two HVNs separated by an LVN ≤ 0.3 × peak.
  - Entry: limit at the upper HVN peak (short) / lower HVN peak (long), first touch.
  - Target: the composite midpoint (LVN waist / range centre). Stop: f × ATR20d beyond the HVN. No trailing [Dale].
- **Sweep:** composite ∈ {3, 5} sessions; f ∈ {0.10, 0.15} → 4.
- **Frequency (est.):** 2–4 per month.
- **Fails when:** a balance break (PS-01 territory).
- **Needs:** F-COMP, F-HVN.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** K-FL; compare with PS-17 and the PS-03 canary. **Kill:** standard.
- **Books:** **Conflict.** It is a fade inside balance (P1); like the 80% rule it hopes for a rotation to the opposite side (P3 ≈ 31%). Keller treats the centre as a destination, which partly supports the *target*. **Priority 2.**

#### DV-08 · Heavy-volume zone + Fair Value Gap alignment (level at the FVG start)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 3) · **original DV grade: E** · books agree/conflict: Untested layer · family RG (layer).
- **Markets · TF:** parents' markets; 30m FVG.
- **Data · test window:** 1m · W-M (agreed split §2.0.1).
- **Fails when:** FVG edges coincide with HVN edges by construction on fast moves (no independent information); the parent has no edge to filter.
- **Gate:** parents (DV-02/DV-04 or PS-12/PS-13/PS-14) must pass first.
- **Sources:**
  - https://www.youtube.com/watch?v=dqsI3FmVUJA ("80 or 90% of all my trades have heavy volumes and FVGs")
  - https://www.youtube.com/watch?v=cBcL8e5r_lU
  - https://www.youtube.com/watch?v=u88dEb7ErCU
  - https://www.youtube.com/watch?v=flMdF-A2mmM
  - https://www.youtube.com/watch?v=uDvT4uDWgB4 (confluence)
  - https://www.youtube.com/watch?v=srEJqcTRDEI
- **Evidence:** E.
- **Rules:**
  - *[Dale]* FVG = 3 candles; bullish if high(1) < low(3); "the bigger the gap, the better"; trade "from the beginning of the FVG" (the edge nearest price at departure, i.e. high(1) for bullish); "the beginning of the heavy volume zone needs to align with the beginning of the FVG".
  - **[coding]** Bullish FVG on closed 30m bars with gap ≥ g × ATR30m.
  - Level = high(bar1) if |high(bar1) − near HVN edge| ≤ 0.2 × ATR30m.
  - Used as a **layer** on DV-03/04/05: replaces the level and requires alignment.
- **Sweep:** g ∈ {0.3, 0.6}; alignment tolerance ∈ {0.1, 0.2} ATR30m → 4.
- **Frequency:** a subset (~30–50%) of the parent setups.
- **Needs:** ✓ FVG detector (`_FvgDetector`, PR #18), F-CONF.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:** an ablation with vs without FVG on the same parents; K-FL on the FVG. **Kill:** no lift over the parent.
- **Books:** untested layer; FVG ≈ a single-print/initiative tail (PS-20 logic, continuation-friendly). **Priority 3.**
- **See also (v2.7):** SP-09 reuses `_FvgDetector` (VP-5) as an entry. This row stays a 30m alignment layer. Its rules are unchanged.

#### DV-09 · Weekly-VWAP ±1σ trend pullback
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 2) · **original DV grade: E (+ his ~60% claim)** · books agree/conflict: Conflict (P5) · family VW.
- **Markets · TF:** ES/NQ, 6E; 30m; weekly VWAP from the Sunday 18:00 ET open.
- **Data · test window:** 1m · W-M (agreed split §2.0.1) + ETH bars for the weekly anchor (verify E5).
- **Fails when:** VWAP trend pullbacks fail after costs on ES (RipBacktest PF 0.89); early-week bands are unstable.
- **Needs:** F-VWAP AdaptiveFromDate re-anchored weekly (✓ M-AVWAP-lite = `EVwapType.Week`), F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Controls:** VW-01 and a slope-less placebo; P1 (unweighted weekly average) per A2 §0.
- **Sources:** https://www.youtube.com/watch?v=CrExYe4o6ng ("VWAP: A Trend Trading Strategy"; claims a "positive win ratio of around 60%… not super consistent") and https://www.youtube.com/watch?v=N4t8_dJUMho (setup 2).
- **Evidence:** E.
- **TF:** 30m, weekly VWAP with first deviations only [Dale].
- **Rules:**
  - *[Dale]* trend = "first deviations… trending… at least one of them"; short when price is below the lower σ1 and pulls back to it; don't trade early in the week ("usually not on Monday, Tuesday sometimes") until the bands develop; no macro news.
  - **[coding]**
    - Weekly VWAP (F-VWAP AdaptiveFromDate = week open, ETH).
    - Slope of the σ1 band over the last 8 closed 30m bars ≥ s × ATR30m.
    - Price closed beyond the band by ≥ 0.25 ATR30m, then a touch of the band = limit entry with-trend.
    - From Wednesday 00:00 ET (variant: Tuesday 12:00).
  - Stop/Target **not stated in my extract** → **[coding]** f × ATR20d.
- **Sweep:** s ∈ {0.1, 0.2}; start ∈ {Tue noon, Wed} → 4.
- **Frequency:** 4–10 per month.
- **Test:** compare with VW-01; a slope-less placebo. **Kill:** standard.
- **Books:** **Conflict** with P5 (RipBacktest VWAP trend-following PF 0.89 on ES). **Priority 2.**

#### DV-11 · VWAP trend-failure (lose σ1 → pullback to σ1 → target VWAP)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 3) · **original DV grade: E** · books agree/conflict: Partly agree · family VW.
- **Markets · TF:** ES/NQ, 6E; 30m; weekly VWAP.
- **Data · test window:** 1m · W-M (agreed split §2.0.1) + ETH bars (verify E5).
- **Fails when:** the VWAP target is not a magnet (P5); range weeks produce repeated band crosses.
- **Needs:** weekly-anchored F-VWAP (✓ M-AVWAP-lite = `EVwapType.Week`), ✓ M-SWING (swing stop arm).
- **Readiness (as of merge):** Buildable now on the merged base.
- **Source:** https://www.youtube.com/watch?v=wtiPaOjnKIc (also a weekly-VWAP σ1 + HV zone confluence there).
- **Evidence:** E.
- **Rules:**
  - *[Dale]* the trend has price "above the first deviation", then "the trend needs to fail… price goes below the first deviation… pullback to that first deviation… short… ideal take profit… VWAP".
  - **[coding]**
    - ≥ 6 of the last 10 closed 30m bars closed above +σ1 (weekly VWAP).
    - Then a 30m close < +σ1 − 0.1 × ATR30m.
    - Arm a short limit at +σ1 for ≤ 8 bars.
    - TP = VWAP; SL = recent swing high (M-SWING) or f × ATR20d. Mirror for longs.
- **Sweep:** close-buffer ∈ {0.1, 0.25}; stop ∈ {swing, fixed} → 4.
- **Frequency:** 2–5 per month.
- **Test:** compare with VW-04, AV-12. **Kill:** standard.
- **Books:** **Partly agree.** It acts on a *confirmed* failure (P1 style; failed-break logic), but the target is the VWAP magnet (P5 says no magnet edge). **Priority 3.**

#### DV-15 · EMA20 pullback gated by VWAP slope
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 4) · **original DV grade: **E+ (his small test: 2 mo EUR/USD, 138→73 trades, 48.5%→60% at 10/10 pips)**** · books agree/conflict: Agree · family VW.
- **Markets · TF:** 6E (replication of his number), then ES/NQ; 30m.
- **Data · test window:** 1m · W-M (agreed split §2.0.1); weekly-VWAP arm needs ETH bars (verify E5).
- **Gate:** A2-40/59 costs (the 10-pip / small-bracket version is the most cost-sensitive idea in the DV set).
- **Source:** https://www.youtube.com/watch?v=1gpdZhV-rzg.
- **Evidence:** **E, with the only numbers on the channel:** EUR/USD, last 2 months, 30m, TP = SL = 10 pips. The plain EMA20 had 138 trades at 48.5%; with the VWAP-slope gate, 73 trades at 60%. That is in-sample, tiny and one market; costs are **not stated**.
- **Rules:**
  - *[Dale]* "If the price hits the EMA 20 from above → long… from below → short… only if there's a slope in the VWAP… If price is above VWAP, I trade longs… below VWAP, shorts." Which VWAP is **not stated in the extract** (his default chart is the weekly VWAP). The slope threshold is **not stated**.
  - **[coding]**
    - 30m EMA20; side = sign(close − EMA) for ≥ 3 bars.
    - Touch = bar trades through the EMA; enter a limit at the EMA.
    - Require sign(VWAP slope over the last 8 bars) = side, |slope| ≥ s × ATR30m, and price on the same side of the VWAP.
    - Bracket 1:1, f × ATR20d (ES port) or 10/10 pips (6E: 0.0010).
- **Sweep:** VWAP ∈ {session, weekly}; s ∈ {0.05, 0.15}; f ∈ {0.10, 0.15} → 8.
- **Frequency:** his figure is about 36 per month on EUR/USD 30m (73 trades in 2 months).
- **Fails when:** a choppy trend; a cost floor at a 10-pip bracket.
- **Needs:** F-VWAP, EMA (trivial), F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test:**
  - K-RT, K-RW (1:1 → 50%).
  - Replicate his EUR/USD number on 6E in W-M as a pipeline sanity check, then ES/NQ.
  - **Kill:** < break-even net at ≥ 150 events, or the gate gives no lift over the ungated version.
- **Books:** **Agree:** a trend filter on pullbacks (RG-06/VW-01 logic) and the cheapest to test. **Priority 4.**

#### DV-16 · Order-flow confirmation at a pre-defined level (layer)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 3 (layer)) · **original DV grade: E** · books agree/conflict: P6 untested · family OF (layer).
- **Markets · TF:** ES/NQ; 5m footprints at pre-defined levels.
- **Data · test window:** 1-tick + Bid/Ask · W-T only (≤ 4 configs; 250/125/125-session split).
- **Fails when:** absorption reads the wrong way at sweeps (OF-04, Telonics B(−)); TickRule-quality flow; trapped-trader arm (d) belongs to the deferred trapped-trader round and is only logged here.
- **Sources:**
  - https://www.youtube.com/watch?v=AZBACKQQXLE and https://www.youtube.com/watch?v=W4LfdqGupAs (absorption: "unusually heavy volumes… both on bid and ask… around strong support or resistance"; 5m preferred; imbalance at 300%)
  - https://www.youtube.com/watch?v=FGlJGPHIYPk and https://www.youtube.com/watch?v=8QUcKAUXmEc (a big single order at the level; at resistance, a big print on the ask is read as a sell limit)
  - https://www.youtube.com/watch?v=_W1mRVsSVHI (aggressive traders: imbalances in the trade direction after absorption)
  - https://www.youtube.com/watch?v=uD5xto10twk and https://www.youtube.com/watch?v=0h2Z3U0ZYLU (trapped traders: heavy ask volume or a buy imbalance at a footprint high, then no follow-through → short; only inside a level)
  - https://www.youtube.com/watch?v=1j9pYnJStWo (imbalance = diagonal ≥ 4×; a breakout variant was not extracted)
  - https://www.youtube.com/watch?v=XPk1nrsDaYc
- **Evidence:** E · P6 (footprint signals untested).
- **Rules:**
  - **[coding]** Within ±0.25 × ATR5 of a DV-01/03/04 level, on closed 5m footprints (W-T, MC-BidAsk):
    - (a) absorption = bid and ask volume both ≥ z = 3 vs the 20-session clock-minute distribution at the same price(s), and the bar's close-move is ≤ 0.3 × range;
    - (b) big trade ≥ q-th percentile print size (◐ M-TICK: size gated until §E6);
    - (c) ≥ 2 diagonal imbalances at 300% in the trade direction;
    - (d) trapped: the prior bar has ≥ 2 buy imbalances in its top 3 rows and the next bar closes below its low.
  - Entry at the close of the confirming bar; stop beyond the bar extreme or level + floor.
- **Sweep:** signal type (4) × level family (2) → 8.
- **Frequency:** about 30–60% of level touches in W-T.
- **Needs:** F-OF (W-T only), ✓ M-TOD (volume z), ◐ M-TICK (size outputs gated until §E6).
- **Readiness (as of merge):** Buildable with caveats — M-TICK (size outputs gated until §E6).
- **Test:** **K-SH (shuffled flow) is mandatory**; K-LM vs a blind limit. **Kill:** no lift over K-SH.
- **Books:** P1 agrees with confirmation in principle; P6 says untested; OF-04 is already a control. **Priority 3** (W-T ≈ 500 sessions only).
- **See also (v2.7):** OF-09 extends this layer into a weekly → 30m value-area → 1m failed-seller cascade. The rules above are unchanged.

#### DV-20 · Stop-loss-cluster (weak low/high) veto
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 3 (filter)) · **original DV grade: E** · books agree/conflict: Agree · family RG (layer).
- **Markets · TF:** ES/NQ, 6E; 30m swing lows/highs.
- **Data · test window:** 1m · W-M (agreed split §2.0.1).
- **Sweep:** tolerance ∈ {0.1, 0.2} × ATR30m; minimum count ∈ {2, 3} → 4.
- **Frequency (est.):** vetoes a minority of host level trades (author estimate 10–25%).
- **Fails when:** equal lows are noise at 30m; the veto removes winners as fast as losers.
- **Needs:** ✓ M-SWING, host level generators.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** veto ablation on DV-02/DV-04/PS-12/PS-13 hosts and K-FL (fake equal-low clusters); kill if lift < 0.03 R. Variant (DV-02 pre-arm) is reported separately.
- **Sources:** https://www.youtube.com/watch?v=lBqNWBfrXiI (multiple candle lows at almost the same level below a long level = "weak low", so the level will fail; a single sharp rejection = "strong low") and https://www.youtube.com/watch?v=-lbhVapPivI (a weak high gets tested).
- **Rules:**
  - **[coding]** Weak low = ≥ 3 30m swing/bar lows within 0.1 × ATR30m of each other, below the level and within 1 × ATR30m of it. Veto longs at that level (mirror for shorts).
  - Variant: use as a DV-02 pre-arm (expect the break).
- **Evidence:** E.
- **Books:** **Agree** (liquidity runs; PS-19 poor highs/lows get run through). **Priority 3 (filter).**

#### DV-22 · ABCD (AB = CD)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 1) · **original DV grade: E (he claims "65% win rate" in title only)** · books agree/conflict: No support · family SP.
- **Markets · TF:** ES/NQ, 6E; 30m.
- **Data · test window:** 1m · W-M (agreed split §2.0.1).
- **Sweep:** C retracement band ∈ {38–50%, 50–62%}; bracket f ∈ {0.10, 0.15} → 4.
- **Frequency (est.):** author estimate 2–5 per month.
- **Fails when:** trends (a counter-trend fade at a measured move); pivot-confirmation lag moves D.
- **Needs:** ✓ M-SWING.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** K-RT and K-FL (D shifted ± 0.5 ATR30m); K-RW 1:1 → 50%; kill on the standard Gate-3 rule.
- **Sources:** https://www.youtube.com/watch?v=a-coHiobKpA (the title claims "65% win rate"; he says he back-tested "hundreds maybe even a thousand trades", no numbers shown) and https://www.youtube.com/watch?v=v2dOQ0l3J1s.
- **Rules:**
  - *[Dale]* bullish leg A→B; pullback C must be "above A" and "below 50% of AB"; D = C + (B − A); "from point D you go short".
  - **[coding]** M-SWING pivots; enter a limit at D, counter to the CD leg.
  - SL/TP **not stated in the extract** → f × ATR20d.
- **Books:** no support; a counter-trend fade at a measured move. **Priority 1.**

#### DV-23 · Initial-balance draw trade (guest: Dan McKen, Funded Trader Academy, not Dale)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Catalogue grade:** **E** (+ Dale priority 3) · **original DV grade: E (claims 96.4% IB-touch)** · books agree/conflict: Partly · family PS.
- **Markets · TF:** ES/NQ; IB 09:30–10:30, 5m.
- **Data · test window:** 1m · W-M (agreed split §2.0.1).
- **Sweep:** stop ∈ {0.25, 0.4} × IB range; decision time ∈ {10:30, 11:00} → 4.
- **Frequency (est.):** only days with neither IB extreme traded after 10:30 (author estimate 4–8 per month).
- **Fails when:** the touch rate is high because the target is near and the stop wide (payoff, not rate, decides); trend days break the IB early.
- **Needs:** F-SESS (IB), F-TM.
- **Readiness (as of merge):** Buildable now on the merged base.
- **Test · Kill:** first measure the IB-extreme touch rate in W-M (the 96.4% claim) and K-RW for the actual b; then the strategy vs K-RT; standard Gate-3 kill. Shares the PS-08 slot as a Variant.
- **Sources:** https://www.youtube.com/watch?v=VoyOpvmre9E ("there is a 96.4% chance that during the day we will hit either the low or the high of the initial balance"; after 10:30, if neither has been hit, target the IB extreme on the side of the IB midpoint where price is; after an IB-low break, "no more longs… only a 20% chance it's going to reach the initial balance high"). The same guest appears in https://www.youtube.com/watch?v=P3qeLp497hk (ES/NQ OF A–Z; VA 70%, IB mid).
- **Rules:**
  - **[coding]** At ≥ 10:30 ET, if neither IBH nor IBL has been traded since 10:30: price > IB mid → long, target IBH − 1 tick; price < IB mid → short, target IBL + 1 tick.
  - Entry is **not stated** precisely (he uses lower-TF levels) → **[coding]** market at the first 5m close after 10:30 that satisfies the condition. Stop: IB mid ± 0.25 × IB range.
  - Rule 2 **[coding]**: after an IB break, disable counter-direction trades (filter for other ideas).
- **Evidence:** E. The 96.4% is a population-rate claim and testable directly in W-M. A high touch rate does not imply edge: the target is near and the stop is wide.
- **Books:** partly agree (Keller IB extension/failure; PS-08/09). **Priority 3.** First measure the touch rate and the payoff.

### 3.15 Parked: ideas that need recorded L2 data (L2-01…L2-07)

dxFeed supplies **no historical DOM** (real-time 10 levels only), and MC .NET exposes no order-by-order (MBO) data or aggressor flag. Each idea below is marked **needs recorded L2 data**: it can only be tested after recording the real-time 10-level DOM locally for months (and some need exchange MBO timestamps, which a 10-level aggregated feed cannot give).

| ID | Idea (Keller B2) | Why it needs L2 | Status |
|---|---|---|---|
| L2-01 | Cancellation veto before level entries: skip a long if bid-side inferred cancellations at the level are in the top decile (Ch 11.6) | Depth changes at the level | needs recorded L2 data |
| L2-02 | Book-shape regime: β_shape, facade ratio, Δβ asymmetry as a mean-reversion/trend gate (Ch 11.3, Drill 11.3) | Levels 1–10 depth | needs recorded L2 data |
| L2-03 | Resilience half-life T½ prior to range breaks: brittle → take breakouts, resilient → skip (Ch 11.5, Drill 11.5; "most valuable untested idea") | Best-level depth refill timing | needs recorded L2 data (sub-second; 10-level snapshots may be too coarse) |
| L2-04 | Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor (Ch 11.4, 13.3) | Displayed depth + refill latency < 5 ms | needs recorded L2 data **and** MBO timestamps |
| L2-05 | Pre-event depth withdrawal forecasts the spike's revert fraction (Ch 14.4, Drill 14.4; complements A2-35) | Depth/spread 10 minutes before the release | needs recorded L2 data |
| L2-06 | Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1) | Queue size at the price | needs recorded L2 data (execution rule, not a signal) |
| L2-07 | Two-sided cancellation surge = fragility alarm (Ch 11.6) | Depth changes both sides | needs recorded L2 data |

Not carried forward: top-of-book DOM imbalance (Ch 11.2) and millisecond lead–lag (Ch 14.1). The book itself rates them as arbitraged at human horizons.

#### L2-01 · Cancellation veto before level entries
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data").
- **Idea (verbatim):** Cancellation veto before level entries: skip a long if bid-side inferred cancellations at the level are in the top decile (Ch 11.6)
- **Why it needs L2:** Depth changes at the level
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-02 · Book-shape regime
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data").
- **Idea (verbatim):** Book-shape regime: β_shape, facade ratio, Δβ asymmetry as a mean-reversion/trend gate (Ch 11.3, Drill 11.3)
- **Why it needs L2:** Levels 1–10 depth
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-03 · Resilience half-life T½ prior to range breaks
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data (sub-second; 10-level snapshots may be too coarse)").
- **Idea (verbatim):** Resilience half-life T½ prior to range breaks: brittle → take breakouts, resilient → skip (Ch 11.5, Drill 11.5; "most valuable untested idea")
- **Why it needs L2:** Best-level depth refill timing
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-04 · Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data **and** MBO timestamps").
- **Idea (verbatim):** Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor (Ch 11.4, 13.3)
- **Why it needs L2:** Displayed depth + refill latency < 5 ms
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-05 · Pre-event depth withdrawal forecasts the spike's revert fraction
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data").
- **Idea (verbatim):** Pre-event depth withdrawal forecasts the spike's revert fraction (Ch 14.4, Drill 14.4; complements A2-35)
- **Why it needs L2:** Depth/spread 10 minutes before the release
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-06 · Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data (execution rule, not a signal)").
- **Idea (verbatim):** Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1)
- **Why it needs L2:** Queue size at the price
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

#### L2-07 · Two-sided cancellation surge = fragility alarm
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** L2 (parked) · Analyst 2 Rev 6 Appendix A (Keller B2).
- **Evidence:** **H** · **original grade: not graded** (A2 lists it as "needs recorded L2 data").
- **Idea (verbatim):** Two-sided cancellation surge = fragility alarm (Ch 11.6)
- **Why it needs L2:** Depth changes both sides
- **Data · test window:** **none in history.** dxFeed supplies no historical DOM (real-time 10 levels only), and MC .NET exposes no MBO or aggressor flag. Testable only after months of recording the live 10-level DOM locally (§8 row 11).
- **Status:** parked; not scored; not slotted.

---

### 3.16 Family KD: Keller B5 detrended rails (KD-01…KD-11; rev 3.1, signed off 23 Oct 2026)

Source: `specs/keller-b5-catalogue-additions.md` rev 3.1 (reviewed by Analyst 1 and Analyst 2; **signed off by Ettienne 23 Oct 2026, acceptance N = 2 primary**), folding in `specs/keller-b5-p6p7-entries-analyst2.md` (KD-06…KD-11) as amended by the rev 3.1 rulings. Engine: **M-DVP** (§2.4b). Documentation only; nothing implemented or tested.

> **Evidence rule (every KD row).** Every number is the book's printed number with its Kindle location, and it is a **Forex (incl. XAU/XAG) / crypto result**; the book's scan does **not** cover futures or equity indices. **No ES statistic exists in this book and none is stated here.** Exhibit ratios come from *selected* charts → grade **E** on their own; census rows are full-population → grade **C (other asset class)**. 8.3 operating rules 1–4 are census-supported; rules 5–7 are author hypotheses (incl. rule 5 "every rail a destination, every acceptance the entry" and rule 7 "witnesses disagree / stand aside"). Every KD row carries **FX/crypto only · no true delta · ES prior 0**.

**Placement in the catalogue (user mapping):**

| KD ID | Kind | Catalogue entry | Holm |
|---|---|---|---|
| KD-01 | entry (primary) | **PS-25** (§3.1) | yes (PS-25 primary core only) |
| KD-02 | canary | **KD-02** below; G0-b canary (§2.6) — canary only, not a candidate edge | no |
| KD-03 | module | **RG-09** (§3.9) | no |
| KD-04 | module | **RG-10** (§3.9) | no |
| KD-05 | layer | **MT-07** (§3.4) | no |
| KD-06 | entry | PS-25 robustness arm σ2 (below) | no |
| KD-07 | gate | KD-02 gate form (below) | no |
| KD-08 | study | degree-score diagnostic (below) | no |
| KD-09 | gate | RG-09 rev 3.1 module form (below; `GateModule = "KD-09"`) | no |
| KD-10 | entry | PS-25 robustness arm node (below) | no |
| KD-11 | study | A2-33 / G0-d extension (below) | no |

**Common to all KD rows:** ES (MES for live sizing) on 15m, 60m, 240m M-DVP streams (15m = Data1; 60m/240m via `BarsOfData`, Probe P2 first). Data: 1m-built bars · W-M (agreed split §2.0.1; holdout Jan 2025–Oct 2026); unadjusted `@ES`: a fit window may not cross a roll (`crossesRoll` skip; skip share journaled per stream, > 30% → diagnostic-only); ETH primary, RTH-only sensitivity arm; RTH share of fit-window volume journaled, results split {<50%, ≥50%} report-only. Costs G0-a (A2-40 + A2-59); standard controls §2.3. Acceptance = N = 2 consecutive closed bars beyond rail + δ (δ = 0.10 ATR), each judged against its own frozen rail (catalogue convention, A2-08/A2-11). The cone is never a stop or target (diagnostic only, KD-08). Sandbox: `IEntryRule` / `IGateModule` in `_SbxE_KD` / `_SbxG_KD` / `_SbxS_KD` (spec only), enabled through the protected `DvpConfig` (`enabled = true`), `PriceActionConfig.enabled` (pivots 20/40/80) and `LevelStudyConfig` (KD-11); **no new `[Input]`s**.

**Rev 3.1 rulings carried by this family:**
- **Holm family** {PS-01, PS-02, DV-02, A2-31, PS-25}, **m = 5, family-wise α = 0.05**, fixed before any dev read; m stays 5 even if the A2-31/PS-25 tie-break drops one; final m and members journaled. Only KD-01's primary core enters, via PS-25.
- **Sweep:** the frozen PS-25 18-cell sweep plus a separate, labelled **σ2/node robustness block of 6 cells** (rail {σ2 (KD-06), node (KD-10)} × N {2, 3, 5} × EX-05 trail + 41-bar time stop), journaled as trials, never the primary. Dropped: the KD-06 +1R partial arm and the KD-10 next-rail target. **PS-25's primary cell is PROPOSED (both analysts) as N = 2, VAH/VAL, EX-05 trail + 41-bar time stop — Ettienne to confirm before any dev read** (§3.1).
- **Rolls:** the `crossesRoll` skip stays (roll bar ±1 via `IsRollWindow`); skip share journaled per stream; > 30% skipped → stream diagnostic-only; back-adjusting report-only after G0-c, never in Holm. (This overrides Analyst 2's back-adjusted-roll proposal.)
- **Stand-ins:** KD-10 "far outside value" = |z_node| ≥ 2 [GAP stand-in], report splits {2–3, ≥ 3}; KD-09 tolerance fixed at 0.3 ATR, 0.3–1.0 ATR disagreement band [GAP], ≥ 2 lattice ranges in-module only; KD-10 held adverse range 3.2–14.6 ATR (Analyst 2's tally, not the book's).
- **Overlaps:** KD-10 vs A2-46 and KD-06 vs VW-03 — trade overlap reported descriptively, **no P&L pick, nothing dropped**.
- **KD-02 kill:** net PF > 1.0, no 1.0–1.1 zone.
- **Time-of-day volume:** RTH share per fit window journaled; results split {<50%, ≥50%}, report-only; ETH stays primary.
- **Hidden-trial exclusion list (all outside Holm, m = 5):** (1) choosing the best of the KD-01 cells → name PS-25's single primary cell before any dev read; (2) KD-06 +1R partial arm → deleted; (3) σ2/node arms (6 cells) → labelled robustness, ineligible for primary; (4) KD-09 arms (KD-03 arms A/C and the confTol sweep are replaced by KD-09, not run) → the filter never modifies the primary unless pre-registered before the read; (5) KD-07 veto on DV-02 (a Holm member) → excluded; (6) P&L "keep the better one" rules for VW-03 and A2-46 → overlap reported, no P&L drop (the only declared P&L choice stays the A2-31/PS-25 tie-break); (7) KD-11 → destinations and KD-08 → cone re-open → new pre-registration, outside m = 5; (8) default N → fixed before results (N = 2, confirmed 23 Oct 2026); (9) report-only arms (RTH, unadjusted twin / back-adjusted arm, t−2 endpoint drift, degree-1 twin, 0.77 weights, |z| ≥ 3 bucket) → never gate or select; journaled as such.

#### KD-02 · Band-stretch fade to the POC rail — control / canary (G0-b)
- **Implementation status (v2.4):** **Implemented** — Canary `EGate0Canary.KD02 = 8`, sweep 130..133 ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)). Not run in MC.
- **Family · Source:** KD (canary) · Keller B5 Ch 2.6 (loc 772–813), 6.3 #27 (loc 4237), 8.3 rule 4 "do not fade the band".
- **Evidence:** **C (0) FX/crypto** — reversion to POC 51.8% FX / 51.3% crypto, median 19 bars (loc 772–813); exhibit ratio 1.05 / 1.00, "a trade with no shape" (loc 4237). Fade expectancy conditionals are **not in the census**. ES: none. **Expected result: ≈ 0 or negative net. Kept as a canary, not as a candidate edge.** · **FX/crypto only · no true delta · ES prior 0**.
- **Markets · TF · Data:** as the family common line.
- **Rules (short at upper stretch; long mirrored):** *Trigger:* bar t closes > ŷ(u_t) + 2σ (fit ending t−1). *Entry:* market at the open of t+1. *Stop:* +3σ rail + 0.1 ATR. *Target:* POC rail (re-read each bar from the t−1 fit). *Time stop:* 19 bars (book median) and 40 bars (variant).
- **Sweep (≤ 4):** time stop ∈ {19, 40}; stop ∈ {+3σ, +2.5σ}.
- **Needs:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) (band + POC rail only; manifold required, lattice not).
- **Readiness (v2.4):** **Implemented** as Gate-0 canary `EGate0Canary.KD02 = 8` (sweep 130..133) ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); not yet run in MC.
- **Gate (v2):** joins the **G0-b canary suite** (with VW-08, A2-24, DV-10; it is the detrended analogue of DV-10's σ-band fade), re-run on every M-DVP engine change; G0-a costs. Not scored in §4.
- **Role · Kill:** canary passes if net PF ≤ 1.0 and the POC-reach rate is within ±5 pp of 50% (the FX/crypto 51–52% is the prior, not an ES fact). **Kill (canary fails): net PF > 1.0 — no 1.0–1.1 zone.** A failed canary is treated first as a bug (rails from a later fit, forming-bar leakage, endpoint repaint), then — only after the causality unit tests pass — as a finding. It fixes the denominator for PS-25 (same rail set, opposite logic).

#### KD-06 · σ-band acceptance breakout (PS-25 robustness arm "σ2")
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Family · Source:** KD (entry arm of PS-25) · Keller B5 6.1 (#25 held: fav 2.3–2.5 vs adv 4.5–5.65 ATR), 6.2 (#26 accepted 5.5–10.8 fav / ≤ 0.6 adv), 6.4 (#28 break kept going; accepted 4.0–6.5 / ≤ 0.8), 8.3 rule 4 (census) and rule 5 (hypothesis).
- **Evidence:** **E** (selected exhibits) · setup untested · **FX/crypto only · no true delta · ES prior 0**.
- **Rules (long; short mirrored):** *Trigger:* C_t > rail_t(+2) + δ and C_{t−1} ≤ rail_{t−1}(+2). *Acceptance:* C_{t+1} > rail_{t+1}(+2) + δ (N = 2; N ∈ {2, 3, 5} in the robustness block). *Filters:* `sigmaAtr` ≤ 4; **RG-10 (KD-04) stand-down off for this arm (pre-registered)**. *Entry:* buy-stop 1 tick above H_{t+1}, valid 2 bars. *Stop:* max(L_{t..t+1} − 1 tick, rail_entry(+2) − 0.5 ATR), floored per A2-40. *Exit (rev 3.1):* **EX-05 trail + 41-bar time stop only** (no cone edge, no forward σ rail; the +1R partial arm is deleted). *Invalidation:* a close back < rail(+2) − δ, read from the normal rolling refit.
- **Needs:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) (fit + band only).
- **Readiness (v2.4):** Engine on main (PR-D0…D8); consumer rule not coded; may start only after PR-D9 freezes d*.
- **Gate (v2):** as PS-25 (G0-a/b/c; G0-d `DvpSigma`). **Holm: no** — a robustness cell, never the primary.
- **Test · Kill:** population accepted median MFE/MAE (40 bars) < 3; or no lift vs K-RT or ShiftLevels ±0.5σ; or no lift vs the flat-σ twin (VW-03 on the same bars); or net expectancy ≤ 0 after G0-a. The t−2 endpoint-drift sensitivity is **report-only** (hidden-trial item 9).
- **Overlap:** VW-03 / A2-10 (VWAP σ-band breakout, different curve) — trade overlap **reported only, no P&L pick**.
- **Slot:** KD batch `_Test2`, `Idea = "KD-01"`, Variant cell σ2.

#### KD-07 · No-fade veto (gate form of KD-02)
- **Implementation status (v2.4):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** KD (gate) · Keller B5 rule 4 (POC reached 51.8/51.3%, median 19 bars), 6.1 (stop at 3σ sits inside the adverse leg), 6.2 ("no σ multiple at which adverse < favourable"), 6.3 (OU half-life ≈ 13–14 bars).
- **Evidence:** **C (0) FX/crypto** (census, rule 4) as a reason not to fade; **H** as a gate on ES · **FX/crypto only · no true delta · ES prior 0**.
- **Rule:** `IGateModule.Allow` returns false for any counter-direction entry when |z_t| ≥ 2 on the frozen fit and the entry's side opposes sign(z) (a short when z ≥ +2, a long when z ≤ −2); the veto stays on until |z| < 1 or `winLen/4` bars have passed.
- **Hosts:** fade/reversion ideas PS-03, A2-64, A2-51, DV-10, **as an ablation only**; **never layered on a Holm member (not on DV-02)**.
- **Needs · Readiness:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) (band) · v2.4: engine on main; gate not coded; waits on PR-D9.
- **Kill:** if host fades **without** the veto beat the vetoed version on net expectancy over dev folds, the veto is dropped. KD-02 keeps its own canary kill (net PF > 1.0, no 1.0–1.1 zone). **Holm: no.**
- **Slot:** KD batch `_Test1` (`Mode = Canary`, `Idea = "KD-02"`); `GateModule = "KD-07"`.

#### KD-08 · Forward-cone diagnostic (study, not a trade)
- **Implementation status (v2.4):** **Partial** — `DvpCalib` coverage journal ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); dev-only coverage-match run and d* freeze (PR-D9) open.
- **Family · Source:** KD (study) · Keller B5 2.2 / 6.5 / rule 1: forward ±2σ coverage 28.2/27.7%; whole-projection containment 5.4/5.3%; median breach bar 7 (FX/crypto). Drill 6.2.
- **Evidence:** **C FX/crypto** (census) · **FX/crypto only · no true delta · ES prior 0**.
- **Rule:** for every fit at t−1, project ŷ with degree d* and degree 1 for h = 1..60; journal inside ±1/2/3σ·√(1+h(u)), the first-breach bar and mean |z| by h. Output: coverage by h and degree, breach-bar distribution, ES-vs-book comparison. Feeds the `DevCoverageFrozen` degree score.
- **Needs · Readiness:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) (fit + leverage) · v2.4: partial — `DvpCalib` coverage journal (DVP CAL lines) on main ([#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); the dev-only coverage-match run and the d* freeze (PR-D9) are open.
- **Action:** if the ES median breach bar is > 20 **and** forward ±2σ coverage > 80%, the "cone is never a target" ban may be re-opened **only as a new pre-registered exploratory trial outside m = 5**; otherwise the ban stands. **Holm: no.**
- **Slot:** KD batch `_Test1`, `Mode = EventStudy`, `Idea = "KD-08"`.

#### KD-09 · Family-count confluence filter (rev 3.1 module form of RG-09 / KD-03)
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Family · Source:** KD (gate; alias of RG-09 for traceability) · Keller B5 7.1 (#32 one range: 2.85/4.95 FX, 2.7/10.2 crypto, ratio 0.58/0.26; #33 several ranges ≈ 0.64/0.56), 7.2 / rule 3 (third family +7.4/+6.3 pts one-bar hold; a second adds 0), 7.4 / rule 7 (#38 disagreement adverse 10.3/11.2 ATR, ratio 0.32/0.23, seven exhibits), Appendix F (weights withdrawn).
- **Evidence:** one-bar hold **C, inadmissible as evidence** (admissible test = A2-33); exhibits **E**; rule 7 is a **C hypothesis** that KD-09 re-specifies · **FX/crypto only · no true delta · ES prior 0**.
- **Rules (on the host's event rail at t−1; only after acceptance; no direction):** (1) lattice counts as a family only if **≥ 2 distinct anchored ranges** name a price within **0.3 ATR (fixed)** — in-module only; (2) `fam` = count of {manifold rail, σ rail, lattice} within 0.3 ATR (manifold + σ alone = 2, "no better than one"); (3) **pass if fam ≥ 3**; (4) **disagreement veto (#38):** lattice (≥ 2 ranges) within **0.3–1.0 ATR [GAP]** of a manifold rail but not within 0.3 ATR; (5) report-only weighted score 1.00/0.77 VA/POC (1.00/0.80 never used). Arm B (≤ 1 family) kept as the contrast. Pre-registered arms: {fam ≥ 3, fam ≤ 1, veto-only}. **KD-09 replaces KD-03 arms A and C; confTol is fixed at 0.3 ATR (no confTol sweep); arm B (≤ 1) stays as KD-09's contrast arm. The KD batch runs only KD-09** (RG-09's frozen KD-03 rule is not run).
- **Relation:** complementary to A2-26 (both may run as gates, reported separately).
- **Needs · Readiness:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) incl. lattice + confluence (PRICEACTION pivots at confirmation) · v2.4: `FamiliesAt` on main (#39/#40); filter not coded; waits on PR-D9.
- **Kill:** no lift over a random-subsample filter with the same pass rate; or population #38 40-bar MAE not larger than all three-family MAE; or A2-33 `DvpConf` |t| < 2. **Holm: no**; it never modifies PS-25's primary unless pre-registered before the read.
- **Slot:** `GateModule = "KD-09"` on the KD batch `_Test2` host.

#### KD-10 · Far-outside / band-edge node: acceptance only (PS-25 robustness arm "node")
- **Implementation status (v2.4):** **Spec'd** — `specs/m-dvp-spec.md` §10.4 consumer row; engine on main ([#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)); consumer not coded; waits on PR-D9.
- **Family · Source:** KD (entry arm of PS-25) · Keller B5 7.5 (#39 node on band edge, #40 far outside value) and 7.4 tail ("double reference ≠ double strength"; adv 4.1–4.6 vs fav 2.45–3.3). Analyst 2's tally of 15 exhibits: held fav 0.6–3.2 (to 5.5), **adv 3.2–14.6 ATR**; accepted (4) fav 3.8–9.9, adv 0.5–1.7 (Analyst 2's tally, not the book's).
- **Evidence:** **E** (single-chart exhibits); far-outside margin unpublished · **FX/crypto only · no true delta · ES prior 0**.
- **Event set:** an HVN/LVN or confluence node with **|z_node| ≥ 2 [GAP stand-in]**; report splits {2–3, ≥ 3} (report-only). The #39 band-edge case is |z_node − (±2)| ≤ 0.3·ATR/σ.
- **Rules (long beyond an upper node; short mirrored):** no bounce entry ever (the touch-fade side is vetoed by KD-07 + KD-09). *Trigger:* C_t > node_t + δ with C_{t−1} ≤ node_{t−1}. *Acceptance:* C_{t+1} > node_{t+1} + δ (N = 2; N ∈ {2, 3, 5} in the robustness block). *Entry:* buy-stop 1 tick above H_{t+1}, valid 2 bars. *Stop:* max(L_{t..t+1} − 1 tick, node − 0.5 ATR). *Exit (rev 3.1):* **EX-05 trail + 41-bar time stop only** (the next-rail target is dropped). *Invalidation:* close < node − δ.
- **Needs · Readiness:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) full (manifold nodes + lattice) · v2.4: engine on main; arm not coded; waits on PR-D9.
- **Kill:** fewer than 30 dev events (then report-only); or population accepted median MFE/MAE < 3; or no lift vs ShiftLevels / K-RT; or G0-d `DvpHvn` / `DvpLvn` |t| < 2. **Holm: no** (robustness only).
- **Overlap:** A2-46 (volume-gap commitment to the far node) and #23 / LVN crossed quickly — trade overlap **reported only, no P&L pick, nothing dropped**.
- **Slot:** KD batch `_Test2`, `Idea = "KD-01"`, Variant cell "node".

#### KD-11 · Beyond-value node reach-rate study (Field Drill 7.5; diagnostic)
- **Implementation status (v2.4):** **Partial** — DVP levels reach `_LevelStudy` via the PR-D7 hook ([#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); reach-rate tabulation not coded.
- **Family · Source:** KD (study; A2-33 / G0-d extension) · Keller B5 7.4 tail / 7.5 (C hypothesis: the corpus does not measure reach) and Drill 7.5.
- **Evidence:** **C hypothesis**, unmeasured in the book · **FX/crypto only · no true delta · ES prior 0**.
- **Rules:** at each fit (t−1), list every node outside the 70% VA; record z distance, ATR distance, whether any closed bar's high/low touches the node's frozen-at-creation price within 40 bars, and bars-to-touch. Baselines: AR(1) first passage (ρ estimated on the window) and the plain Gaussian reflection 2·(1 − Φ(d/(σ√h))). Matched control: random prices at the same residual distance (20 controls at 0.9–1.1×). Output: reach rate vs both baselines by |z| bucket {1–2, 2–3, > 3}, plus a Kaplan–Meier curve.
- **Needs · Readiness:** ◐ M-DVP (engine on main; d* not frozen — PR-D9) + A2-33 harness (`_LevelStudy`, kinds `DvpHvn` / `DvpLvn` / `DvpConf`) · v2.4: partial — DVP levels reach `_LevelStudy` via the PR-D7 hook ([#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)); the reach-rate tabulation is not coded.
- **Use:** far nodes may become PS-25 **destinations** only through a **new pre-registration** (outside m = 5); never entries, and **never touches PS-25's primary**. **Holm: no.**
- **Slot:** KD batch `_Test1`, `Mode = EventStudy`, `Idea = "KD-11"` (shares the A2-33 run).

### 3.17 Family VX: volatility (VIX, VX/VXM futures, VIX ETPs) (VX-01…VX-08; new v2.5)

Source: R7 `volatility-leveraged-etf-research.md` (31 Oct 2026), which also holds the primer (VIX computation, term structure, SOQ/VRO, ETP decay, Feb 2018, taxes) and the stress-sizing rule.

> **1% rule for VX/VXM:** the ES 14-tick floor does not apply; a stop does not cap a volatility gap, so contracts = floor(0.01·Equity / (S·multiplier)) with S a pre-set stress move in VX points; skip at 0. Never hold into the SOQ.

#### VX-01 · Hedged VX term-structure carry (Simon–Campasano roll rule)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX (volatility, new v2.5) · R7 `volatility-leveraged-etf-research.md` §3; Simon & Campasano 2014 (*J. Derivatives* 21(3)); Johnson 2017 (*JFQA*)
- **Evidence:** **B (+)** in sample (2006–2011 after bid-ask and fees; SLOPE predicts VX returns 2004–2017); post-2018 ETP practitioner versions weak (SVXY carry 0.02%/yr) · Lit: Indep.
- **Markets · TF:** VX or VXM (CFE) + MES hedge · daily, close/TAS
- **Data · test window:** CFE per-contract VX daily 2004+ (VXM only from 2020) + `$VIX` · dev 2006–2016, holdout 2017+ (must contain 2018-02-05, 2020-03, 2024-08)
- **Why it might work:** Futures converge to spot VIX; in contango the short future earns the roll (Eraker & Wu: rolled 1-month VX ≈ −30%/yr 2006–2013); basis predicts futures, not spot, returns.
- **Rules:** Daily roll D = (VX1 − VIX)/business days to VX1 settlement. D > +0.10 → short VX1; D < −0.10 → long VX1; flat otherwise; exit when |D| < 0.05. Hedge with MES at a rolling regression ratio. Roll ≥ 5 business days before settlement; never hold into the SOQ (VRO).
- **Sweep:** entry {0.05, 0.10, 0.15}; exit band {0.03, 0.05}; contract VX1 vs VX2 (Holm m = 6)
- **Frequency (est.):** in a position most of the year; 1–3 entries/month
- **Fails when:** a contango short before a gap spike (Feb 2018, Mar 2020, Aug 2024); hedge slippage; crowding
- **Needs:** ✗ CFE bundle + per-contract history (verify) · ✗ M-VXTS · ✓ M-EVENT `VixExpiry` · ◐ M-XMKT for the MES leg
- **Readiness (v2.5):** data-blocked (CFE history depth unverified; no term-structure series); stress sizing per R7 §1.8: contracts = floor(0.01·E / (S·mult)), skip at 0.
- **Test:** net of VX ≈ $4.8 RT + 1 tick ($50) or VXM ≈ $1 RT + measured spread; replay 2018-02-05. **Kill:** holdout net Sharpe < 0.3; or 2018-02-05 replay loss > 2% of equity; or any day's loss > 3× the stress budget.

#### VX-02 · VRP-filtered short volatility (Cooper VRP rule, futures form)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX · R7 `volatility-leveraged-etf-research.md` §3; Cooper 2013 (SSRN 2255327); Carr & Wu 2009 (*RFS*); Kipnis 2017 OOS
- **Evidence:** **D (0).** The premium is real (short 30-day variance-swap Sharpe 0.98, Carr & Wu) but the timing rule underperformed B&H XIV out of sample (Sharpe 0.41 vs 0.60, DD −72%; close fills) · Lit: Indep (premium) / Practitioner (rule).
- **Markets · TF:** VXM (SVXY −0.5× as a check) · daily
- **Data · test window:** as VX-01 + ES daily HV (M-DAILY)
- **Why it might work:** Implied vol above realised = insurance premium; short only while the premium is positive and the curve is in contango.
- **Rules:** VRP = VIX − HV10 (annualised, 5-day mean). Short VXM while VRP > 0 and VIX < VIX3M; else flat. No long-VXX leg.
- **Sweep:** HV {10, 20}; smoothing {1, 5}
- **Frequency (est.):** 1–2 entries/month
- **Fails when:** realised vol lags, so VRP > 0 survives into the first crash day
- **Needs:** as VX-01 + `$VIX3M` index feed (verify) + ✓ M-DAILY `Hv`
- **Readiness (v2.5):** data-blocked as VX-01; stress-sized.
- **Test:** as a layer on VX-01 (must add information conditional on VX-01's roll signal). **Kill:** no lift over VX-01 alone.

#### VX-03 · Long volatility in backwardation (VX-01's long leg alone)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX · R7 `volatility-leveraged-etf-research.md` §3; Simon & Campasano 2014
- **Evidence:** **C (+)** as part of S&C; rare, clustered episodes (2008, 2011, 2020, 2022) · Lit: Indep.
- **Markets · TF:** VX/VXM + long MES hedge · daily
- **Data · test window:** as VX-01
- **Why it might work:** Backwardated futures roll *up* toward a still-high spot.
- **Rules:** When VIX/VIX3M > 1 and D < −0.10: long VX1/VXM, hedged long MES. Exit D > −0.05 or after 10 sessions.
- **Sweep:** entry {−0.05, −0.10, −0.15}; time stop {5, 10}
- **Frequency (est.):** a few episodes per year
- **Fails when:** the spike keeps going for weeks (2008); fast collapse once panic ends
- **Needs:** as VX-01
- **Readiness (v2.5):** data-blocked; stress S must allow a −10-point collapse.
- **Test:** episode count first. **Kill:** fewer than 40 independent episodes → diagnostic only.

#### VX-04 · Post-spike normalisation short (VIX spike mean reversion)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX · R7 `volatility-leveraged-etf-research.md` §3 (hypothesis; VIX mean reversion is the base fact)
- **Evidence:** **H (+).** No tested source found for this specific rule; none invented · Lit: Practitioner hypothesis.
- **Markets · TF:** VXM or VX2 · daily
- **Data · test window:** `$VIX`, `$VIX3M`, VX per contract · as VX-01
- **Why it might work:** After a spike, implied vol mean-reverts and the curve returns to contango; waiting for normalisation avoids shorting the peak.
- **Rules:** Arm after VIX/VIX3M > 1.0 for ≥ 2 closes. Enter short on the first close with VIX/VIX3M < 0.95 and VIX ≥ 15% below its 10-day high. Exit at D > 0.10 (hand off to VX-01), 15 sessions, or stress stop.
- **Sweep:** ratio {0.90, 0.95}; drawdown-from-high {10, 15, 20}%
- **Frequency (est.):** 2–5 per year
- **Fails when:** double-dip crises (2008, Aug 2011, Mar 2020)
- **Needs:** as VX-01
- **Readiness (v2.5):** data-blocked; stress-sized.
- **Test:** event study first. **Kill:** average trade ≤ 2× round-trip cost, or a second-spike loss > stress budget.

#### VX-05 · Long-volatility tail-hedge sleeve (portfolio insurance)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX (portfolio sleeve) · R7 `volatility-leveraged-etf-research.md` §3; Whaley 2013 (*JPM*); Israelov 2019 (*J. Alt. Inv.*); Park 2015 (*J. Fin. Markets*)
- **Evidence:** **C (−)** stand-alone: long VIX products lose heavily (VXX −93.6% to 2012; puts rarely improve drawdowns vs holding less equity); VVIX timing has support (Park) · Lit: Indep.
- **Markets · TF:** VX2/VXM long (SPX put spreads outside the stack) · monthly
- **Data · test window:** `$VIX`, `$VVIX`, VX per contract
- **Why it might work:** Pays in crashes; only worth it if cheaper than simply holding less ES.
- **Rules:** Annual premium budget x% of equity, rolled monthly; buy only when VIX is in its bottom 30% (1-year) and VVIX < median.
- **Sweep:** budget {0.5, 1, 2}%/yr
- **Frequency (est.):** monthly roll
- **Fails when:** carry bleed every calm year
- **Needs:** as VX-01 + `$VVIX` (verify)
- **Readiness (v2.5):** data-blocked; judged at portfolio level (§6) only, not per trade; not scored in §4.
- **Test:** portfolio worst-1%-month vs an equal-cost cut in ES exposure. **Kill:** no improvement vs the equal-cost exposure cut.

#### VX-06 · VIX term-structure regime gate for ES hosts (layer)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX (layer; Kaufman regime splitting) · R7 `volatility-leveraged-etf-research.md` §3; Tickfloor 2005–2025 VIX/VIX3M timing test (practitioner)
- **Evidence:** **C (0).** Timing equity exposure with VIX < VIX3M lagged buy-and-hold (QQQ −5.1 pts/yr; long/short −11.5; not significant); conditioning intraday hosts is untested · Lit: Practitioner.
- **Markets · TF:** ES/MES hosts · gate read at the prior close
- **Data · test window:** `$VIX`, `$VIX3M` daily (index feed or daily file) · host windows
- **Why it might work:** Implied-vol regime may change which intraday behaviours pay (breakout vs fade).
- **Rules:** States: A VIX/VIX3M < 0.9 · B 0.9–1.0 · C ≥ 1.0; also VIX terciles. Report-only first; then a pre-registered per-host gate (e.g. continuation hosts off in C).
- **Sweep:** cut-points {0.85/0.9, 1.0}; pre-registered before holdout
- **Frequency (est.):** inherits host frequency
- **Fails when:** hidden-trial regime picking; few C days
- **Needs:** ✗ `$VIX`/`$VIX3M` in dxFeed/MC (verify; `specs/live-check-sheet.md` Check 3) · ✓ M-XMKT or daily file
- **Readiness (v2.5):** buildable once the index feed (or a daily CSV) is confirmed; host sizing unchanged (14-tick floor, 1%).
- **Test:** per-host, per-state report vs ungated. **Kill:** no host improves net expectancy or drawdown in the holdout under the pre-registered gate.

#### VX-07 · VVIX gate (module)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX (module) · R7 `volatility-leveraged-etf-research.md` §3; Park 2015 (*J. Financial Markets* 26)
- **Evidence:** **C (+)** for information content (high VVIX predicts lower SPX-put and VIX-call returns over 3–4 weeks); thresholds **H** · Lit: Indep / hypothesis.
- **Markets · TF:** VX-01/02/04 entries, VX-05 timing · daily
- **Data · test window:** `$VVIX` daily (verify)
- **Why it might work:** High vol-of-vol makes a short-vol jump likelier and hedges dearer.
- **Rules:** Block short-vol entries when VVIX > 80th percentile (1-year); time VX-05 buys for VVIX < median.
- **Sweep:** percentile {70, 80, 90}
- **Frequency (est.):** module
- **Fails when:** VVIX spikes with VIX (redundant)
- **Needs:** ✗ `$VVIX` (verify)
- **Readiness (v2.5):** data-blocked; module, not scored.
- **Test:** worst-decile VX-01 losses with/without the gate at equal trade count. **Kill:** no reduction in VX-01 worst-decile losses.

#### VX-08 · VIX-stretch ES dip-buy (Connors & Alvarez) — layer on SW-02 / canary
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** VX (layer) · R7 `volatility-leveraged-etf-research.md` §3; Connors & Alvarez, *Short Term Trading Strategies That Work* (2009); HoldoutLabs audit (practitioner)
- **Evidence:** **E (−).** Book: 33 trades, 84.85% wins. Audit SPY 1994–2026: CAGR 2.5% vs 10.9%, excess Sharpe −0.54, DD 35.6%, grade F; worse after 2018 (cash at 0% biases against it) · Lit: Book / Practitioner.
- **Markets · TF:** ES/MES · daily
- **Data · test window:** `$VIX` daily + ES daily (M-DAILY) · full history
- **Why it might work:** Fear stretches mark short-term oversold points inside uptrends.
- **Rules:** Underlying above SMA200 and VIX close ≥ 5% above its SMA10 for ≥ 3 days → buy; exit RSI(2) > 65.
- **Sweep:** stretch {3, 5, 10}%; days {2, 3}
- **Frequency (est.):** ~1/year in the book sample
- **Fails when:** few trades; underperforms buy-and-hold
- **Needs:** ✗ `$VIX` (verify) · ✓ M-DAILY `Rsi2`, `Sma200`
- **Readiness (v2.5):** buildable after the index feed; SW-02 sizing.
- **Test:** as a layer on SW-02; also a lookahead canary for the VIX pipeline. **Kill:** no lift over SW-02 alone.

### 3.18 Family LE: leveraged-ETF strategies (LE-01…LE-07; new v2.5)

Source: R7 `volatility-leveraged-etf-research.md` §4. Daily-reset drag: L_t/L_0 = (S_t/S_0)^β·exp[(β−β²/2)V_t + …] (Avellaneda & Zhang 2010), i.e. −V for 2×, −3V for 3×, −6V for −3×. **Preferred vehicle for this stack is MES/MNQ notional**, not the ETF (no daily reset, §1256, near-24h stops); ETF arms need M-ETF.

#### LE-01 · 200-day trend leverage rotation (Gayed & Bilello)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE (leveraged exposure, new v2.5) · R7 `volatility-leveraged-etf-research.md` §4; Gayed & Bilello, *Leverage for the Long Run* (SSRN 2741701, rev. 2026); sma200.trade replication (practitioner)
- **Evidence:** **C (+).** 1928–2015 simulated 2× SMA200 Sharpe 0.51 / DD −78.7%; 1988–2026 revision CAGR 14.00%, Sharpe 0.55, DD −46.51%. Real LETFs to May 2026: SMA200 cut DD (TQQQ −50.0% vs −81.7%) but lowered Sharpe (0.84 vs 0.91) · Lit: Practitioner.
- **Markets · TF:** MES/MNQ at 2–3× notional (preferred) or SSO/UPRO/QLD/TQQQ · daily
- **Data · test window:** ES/NQ daily (M-DAILY) · ETF bars 2009+ (verify dxFeed equity bundle)
- **Why it might work:** Volatility drag (−(β²−β)/2·V) is worst in high-variance down-trends, which the 200-day filter tends to avoid.
- **Rules:** Underlying close > SMA200 → hold 2–3× exposure; below → cash. Signal on the underlying, executed next session.
- **Sweep:** leverage {2, 3}; band {0, 1, 3}%
- **Frequency (est.):** 3–5 switches/year
- **Fails when:** whipsaw; crash below the SMA before the signal (2020); bull-dominated sample
- **Needs:** ✓ M-DAILY `Sma200` · ✗ M-ETF for ETF arms
- **Readiness (v2.5):** buildable now on MES/MNQ. **1% rule:** an allocation sleeve (open-ended risk until the cross), not per-trade risk; a 1%-sized MES/ATR-stop variant is a separate labelled arm.
- **Test:** Calmar and max DD vs unleveraged SMA200 and leveraged B&H. **Kill:** holdout DD > unleveraged index B&H DD, or Calmar < unleveraged SMA200.

#### LE-02 · Volatility-targeted leverage (exposure ∝ target vol / realised vol)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE · R7 `volatility-leveraged-etf-research.md` §4; Moreira & Muir 2017 (*JF*); Cederburg, O'Doherty, Wang & Yan 2020 (*JFE* 138(1))
- **Evidence:** **C (+), contested.** Market alpha 4.9%/yr, Sharpe +25% (Moreira & Muir); real-time OOS does not systematically beat unmanaged (Cederburg et al.). Mechanically cuts LETF drag · Lit: Indep.
- **Markets · TF:** MES/MNQ (or QQQ/TQQQ mix) · daily or weekly
- **Data · test window:** ES/NQ daily (M-DAILY `Hv`) · full M-DAILY history
- **Why it might work:** Volatility clusters while expected return does not rise in proportion; lower exposure in high-variance regimes also cuts the drag term.
- **Rules:** Exposure = min(3, σ*/σ̂20); optional × LE-01 trend state; rebalance only outside a ±20% band.
- **Sweep:** σ* {10, 15, 20}%; σ̂ {HV20, EWMA}
- **Frequency (est.):** ~1–2 contract adjustments/week
- **Fails when:** vol jumps faster than σ̂ (Feb 2018, Mar 2020)
- **Needs:** ✓ M-DAILY `Hv` · ◐ M-PORT for multi-sleeve
- **Readiness (v2.5):** buildable now; σ* is the declared risk budget of a stop-free sleeve (size is not "cut" per trade; it is the rule).
- **Test:** vs LE-01 alone and vs PF-06. **Kill:** no Calmar improvement over LE-01 in the holdout.

#### LE-03 · QQQ ↔ TQQQ regime leverage rotation
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE · R7 `volatility-leveraged-etf-research.md` §4 (LE-01 × LE-02 discrete form)
- **Evidence:** **H (+).** Practitioner backtests seen were not verified · Lit: Practitioner hypothesis.
- **Markets · TF:** MNQ notional 1×/3× or QQQ/TQQQ · daily
- **Data · test window:** as LE-01/LE-02
- **Why it might work:** Use leverage only when trend is up and volatility is low.
- **Rules:** Above SMA200 and HV20 < 1-year median → 3×; above SMA200 with high vol → 1×; below → cash.
- **Sweep:** median vs tercile cut
- **Frequency (est.):** 4–8 switches/year
- **Fails when:** hidden trial over LE-01/LE-02
- **Needs:** as LE-01
- **Readiness (v2.5):** buildable now on MNQ.
- **Test:** vs better of LE-01, LE-02. **Kill:** no Calmar improvement over the better of LE-01 and LE-02.

#### LE-04 · LETF rebalancing pressure into the close (flow-scaled arm of VW-05)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE (TS clock) · R7 `volatility-leveraged-etf-research.md` §4; Baltussen, Da, Lammers & Martens 2021 (*JFE* 142(1)); Cheng & Madhavan 2009; Shum et al. 2016 (*Rev. Finance*)
- **Evidence:** **B (−), decaying.** Last-30-minute momentum explained by gamma hedging incl. LETFs; base effect already graded in VW-05 · Lit: Indep.
- **Markets · TF:** ES/MES · 15:30 → close
- **Data · test window:** ES 1m (W-M) + daily LETF AUM (issuer sites; GAP)
- **Why it might work:** LETFs must buy after up days and sell after down days ∝ AUM·β(β−1)·r, concentrated near the close.
- **Rules:** At 15:30 ET compute r and demand ≈ Σ AUMᵢ·βᵢ(βᵢ−1)·r; if |demand| > k·(ES ADV-normalised) trade in r's direction to the close. Without AUM: |r| × constant proxy (reported as proxy).
- **Sweep:** k terciles; entry {15:30, 15:45}
- **Frequency (est.):** ~8–12/month
- **Fails when:** 0DTE dealer gamma dominates; effect crowded
- **Needs:** ✓ W-M · ✗ LETF AUM file · cross-ref VW-05 / TS-03
- **Readiness (v2.5):** buildable as a VW-05 arm with the proxy; AUM data gap. ES 14-tick floor, 1%.
- **Test:** against VW-05's ROD-only control. **Kill:** no lift over VW-05 ROD-only.

#### LE-05 · Decay-pair short (short TQQQ + SQQQ, or UPRO + SPXU)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE · R7 `volatility-leveraged-etf-research.md` §4; Jiang & Peterburgsky 2017 (*Applied Economics* 49(44)), *J. Trading* 2018, *J. Investing* 2021
- **Evidence:** **C (+), simulated;** borrow costs, recalls and locates ignored · Lit: Indep (simulated).
- **Markets · TF:** 3× bull/bear ETF pairs · daily, band rebalancing
- **Data · test window:** ETF daily bars (verify) + borrow history (GAP)
- **Why it might work:** Both legs suffer −(β²−β)/2·V drag; shorting both harvests it.
- **Rules:** Short equal-delta amounts of the +3× and −3× fund; rebalance to delta-neutral when the ratio drifts outside a band.
- **Sweep:** band {5, 10, 20}%; borrow arms {0, 5, 20}%/yr
- **Frequency (est.):** a few rebalances/month
- **Fails when:** borrow spikes and buy-ins; strong one-way trend; reverse splits
- **Needs:** ✗ M-ETF (short profile, borrow model, dividends) · ✗ borrow data
- **Readiness (v2.5):** data-blocked; poor 1% fit (uncapped grind + buy-in) → stress-budget sleeve.
- **Test:** net of borrow arms vs unleveraged index. **Kill:** Sharpe < 0.5 or worse than the unleveraged index at the 5% borrow arm.

#### LE-06 · Short-term RSI mean reversion on LETFs (Connors-style)
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE · R7 `volatility-leveraged-etf-research.md` §4; Connors & Alvarez, *High Probability ETF Trading* (2009)
- **Evidence:** **D (+).** Book results only, on ETFs generally; on ES the same idea is SW-02 · Lit: Book.
- **Markets · TF:** TQQQ or MNQ 3× notional · daily, 2–5-day holds
- **Data · test window:** daily ETF or NQ bars
- **Why it might work:** Short-term oversold reversion inside uptrends; leverage only scales it.
- **Rules:** Underlying above SMA200; buy when RSI(4) < 25, exit RSI(4) > 55; RSI(2) < 10 / > 65 arm.
- **Sweep:** RSI {2, 4}; thresholds {10/25}
- **Frequency (est.):** 2–4/month
- **Fails when:** gap days (3× NQ gap); drag in chop
- **Needs:** ✓ M-DAILY · ✗ M-ETF for the ETF arm
- **Readiness (v2.5):** buildable on MNQ now; stop-based 1% sizing works for short holds.
- **Test:** vs SW-02 at the same notional on MES/MNQ. **Kill:** no lift over SW-02 (the LETF adds nothing).

#### LE-07 · Inverse-LETF bear-trend hold (SQQQ/SPXU below SMA200) — negative control
- **Implementation status (v2.5):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Family · Source:** LE (canary) · R7 `volatility-leveraged-etf-research.md` §4; Avellaneda & Zhang 2010 (*SIAM J. Financial Math.*) drag formula
- **Evidence:** **H (−) expected** from mechanics (−3× drag = 6·V) · Lit: hypothesis.
- **Markets · TF:** SQQQ/SPXU · daily
- **Data · test window:** ETF daily bars (verify), split-adjusted
- **Why it might work:** None — it should lose to short MNQ in the same windows.
- **Rules:** Hold SQQQ while QQQ < SMA200; cash above.
- **Sweep:** none
- **Frequency (est.):** 3–5 switches/year
- **Fails when:** if it wins over the full sample, suspect data (unadjusted reverse splits) or fills
- **Needs:** ✗ M-ETF (splits)
- **Readiness (v2.5):** canary; not a candidate.
- **Test:** vs short MNQ in identical windows. **Kill:** n/a — must lose; otherwise investigate the pipeline before trusting any LE result.

### 3.19 Family YV: ideas from YouTube videos, 2026-11-01 (YV-01…YV-10; new v2.6)

Source: 8 YouTube videos reviewed 2026-11-01 (transcripts in `video-ideas-2026-11-01/<id>.txt`, frames in `video-ideas-2026-11-01/frames/<id>/`; full write-up `specs/video-ideas-2026-11-01-additions.md`). All rules and numbers are as stated by the presenters (timestamps [mm:ss]); every statistic is a **presenter claim**, untested (grade **E**). Anything marked *catalogue proposal* or *catalogue assumption* is the drafter's, not the video's. Ideas already covered (Tom Vorwald VP video: PS-03/A2-42, PS-16/PS-22, PS-01/DV-02, A2-43, PS-02/PS-15; GEX = A2-36; weekly POC magnet = MT-02/EX-02; Turtle DD = A2-71) got no new entry — see Appendix A.8. Data constraints applied: @ES bid/ask from 2024-10-28 only; dxFeed 1m from 2016-12-29, ticks from ≈2024-10-27; VX/VIX do not resolve on dxFeed; costs ES $17 / MES $2.50 per round trip (v2.6a), 1% risk, 14-tick stop floor.

#### YV-01 · 30m balance/VA break → 5m big-trade confirmation → 1m failed-seller pullback entry (Trader Oxygen)
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of PS-01 (with DV-16 / OF-08 / EX-11 layers)
- **Family · Source:** PS (variant) · YouTube: *Most Traders Enter Too Early: Order Flow Entry (World Champion Method)* — Trader Oxygen — https://youtu.be/jmX_ReJxFm8
- **Evidence:** **E** (presenter claims only: T1 ≈ 1:1.5 partial, final 1:2.5 on one shown trade [15:15, 17:00]; one discretionary example, no sample) · Lit: YouTube.
- **Markets · TF:** MNQ setup; executed on MES (switch per YV-02) · 30m setup, 5m confirmation, 1m entry
- **Timeframes:** Weekly bars + weekly VP — HTF bias and POC magnet target, MNQ [01:48–02:30; frame 01_0140] · Daily bars + daily VP — HTF bias (here 'choppy/range') , MNQ [03:12–03:53; frame 01_0140] · 30m — balance range, range VP (VAH/VAL/POC) and the 09:30 ET break out of value, MNQ [03:53–05:57; frame 02_0450] · 5m — big-trade bubbles confirming the break; the 10:30 retracement stalls in a 'heavy buying area', MNQ [09:49–10:50; frame 03_1110] · 1m — entry: failed aggressive sellers, bearish stacked imbalance that fails, green bubbles, rising CVD; **switched from MNQ to MES (S&P) 1m** at 11:00 ET [11:10–15:15; frames 04_1350, 05_1535] · 1m — management: BE after retrace, trail below the latest green bubble / buy stacked imbalance, MES [15:37–16:40; frame 06_1720]
- **Data · test window:** 1-tick + Bid/Ask · **W-T only; @ES bid/ask exists only from 2024-10-28** (dxFeed ticks from ≈2024-10-27; the catalogue's W-T 'Jul 2024' start does not hold for bid/ask) → ≈500 sessions, ≤ 4 configs
- **Why it might work:** PS-01's logic (accepted break of balance, enter the first pullback), with institutional-size prints and failed aggression as the pullback-end trigger instead of a pure price HL.
- **Rules:** *Setup (30m):* range VP over the current balance; a strong break above VAH at/after 09:30 ET with heavy volume, green delta and rising CVD [05:36–07:40]. *Confirmation (5m):* the break carries large-trade bubbles on the buy side [09:49–10:09]. *Entry (1m):* wait for the retracement into a discount/heavy-buying area; enter only when aggressive sellers fail (a bearish stacked imbalance that does not extend), buy-side big trades print and CVD turns up — explicitly **not** on the first blue (up) 1m candle [11:30–15:15]. Buy **limit** order. *Stop:* below the 1m swing low [15:15]. *Target:* T1 just below the prior swing high (≈1:1.5, partial), stop → BE on a deep retrace, then trail below each new green bubble / buy-side stacked imbalance [15:37–16:40]. *Filter:* when HTF (weekly) bias opposes the 30m break, the trade is still allowed but **size down and exit quickly** [06:39–07:00].
- **Sweep:** confirmation {big-trade bubble required, CVD-only}; entry {limit at the 1m failed-seller bar, stop-entry above it}; HTF-counter {skip, half size} → ≤ 4 configs (W-T cap)
- **Frequency (est.):** 1–4 per month (one 09:30 break per session at most; needs balance first) — drafter estimate
- **Fails when:** 'big trades' are aggregated prints, not institutional fills (OF-08 §E6 caveat); absorption/aggression reads the wrong way at sweeps (OF-04, Telonics B(−)); counter-HTF context
- **Needs:** ✓ F-COMP/F-PROF (range VP), ✓ M-SWING, F-OF (stacked imbalances, CVD) at MC-BidAsk tier, ◐ M-TICK per-trade size (gated, §E6) for bubbles
- **Readiness (v2.6):** buildable on W-T without the bubble arm; the big-trade arm is parked like OF-08 until §E6 is verified
- **Feasibility · costs:** Shown MES position tool: stop 25 ticks, target 36 ticks, RR 1.44 (frame 05_1535) vs the spoken '≈1:1.5' — minor mismatch, both recorded. 25 ticks > 14-tick floor, so no widening; on ES $17 = 5.4% of a 25-tick 1R ($312.50); on MES (the instrument shown) $2.50 = 2 ticks = 8% of a 25-tick 1R ($31.25). Order-flow arm limited to ≈500 sessions from 2024-10-28 → low event count (≈30–90 trades). Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** Chart: dark-theme footprint/candle platform; weekly and daily candles with vertical VP histograms and labelled POC lines (01_0140) — overlay text says this is the session's **2nd** trade. · 30m: grey-blue range box with VA shading, green VAH line, blue VAL line, POC; a thin red line also plotted (unlabelled — not identified); breakout candle closes above the box (02_0450). · 5m/1m: green (buy) and red (sell) **circles** sized by trade size, horizontal green/red bars = stacked imbalances, a CVD line pane and a volume pane below (03_1110, 04_1350). · Entry: long position tool (green target box above, pink stop box below) placed after a red stacked-imbalance bar is overrun at the pullback low (05_1535). · Chart vs speech: the tool reads RR 1.44 / 36 vs 25 ticks, spoken '≈1:1.5'; the trail is drawn under the latest green bubble cluster (06_1720). Frames: `jmX_ReJxFm8/02_0450_MNQ-30m-range-VA-breakout-0930.jpg`, `jmX_ReJxFm8/04_1350_MES-1m-failed-sellers-stacked-imbalance.jpg`, `jmX_ReJxFm8/05_1535_MES-1m-long-entry-stop-target-tool.jpg`.
- **Test:** vs PS-01 (price-HL trigger) on identical W-T events, K-SH placebo for the flow trigger. **Kill:** no lift over PS-01's HL trigger in W-T, or ≤ K-SH.
- **Buildable specification (v2.7):** OF-09 (entry), OF-10 (ES/NQ selection) and EX-13 (partial, breakeven, trail). This extract is unchanged.

#### YV-02 · Cross-index flow selection: execute in whichever of NQ/ES shows the stronger confirming order flow (layer)
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** NEW (layer; related to M-XMKT infrastructure, PF-03 diversification, A2 pair rows — none selects the execution instrument by flow)
- **Family · Source:** OF (layer) · YouTube: *Most Traders Enter Too Early: Order Flow Entry (World Champion Method)* — Trader Oxygen — https://youtu.be/jmX_ReJxFm8
- **Evidence:** **E** · one discretionary example; no stats claimed · Lit: YouTube.
- **Markets · TF:** MNQ ↔ MES (presenter); ES ↔ NQ (Ettienne) · 1m at the trigger moment
- **Timeframes:** 1m — both MNQ and MES footprints compared at the same timestamp (11:00 ET): MNQ red bubble vs MES green bubbles, failed bearish stacked imbalance and more bullish CVD on MES → trade MES [12:33–14:54; frames 04_1350, 05_1535] · 30m/5m — the setup itself is defined on MNQ only (YV-01) [03:53–10:29]
- **Data · test window:** 1-tick + Bid/Ask · **W-T only; @ES bid/ask exists only from 2024-10-28** (dxFeed ticks from ≈2024-10-27; the catalogue's W-T 'Jul 2024' start does not hold for bid/ask) → ≈500 sessions, ≤ 4 configs; second leg via M-XMKT
- **Why it might work:** Index futures are highly correlated; if one leg shows absorption-and-turn while the other still shows sellers, the leading leg may give the better fill/RR for the same macro move.
- **Rules:** When a host (YV-01, PS-01, OF-xx) fires on instrument A, compute over the trigger bar window on both A and B (ES/NQ): CVD slope z, net large-trade side, failed opposite stacked imbalance (yes/no). Execute on the leg with the higher confirming score; skip if neither confirms. Presenter's spoken basis only: 'more bullish confluence' in S&P vs Nasdaq [13:33–14:54]; the scoring formula is a catalogue proposal, not from the video.
- **Sweep:** score {CVD-only, CVD+imbalance} × execute {better leg, host leg always} → 4
- **Frequency (est.):** rides on host events
- **Fails when:** leg choice adds noise; ES and NQ fills are not independent; tick-size/notional differences change R
- **Needs:** ✓ M-XMKT (second stream), F-OF on both legs at MC-BidAsk tier (NQ bid/ask history depth to verify)
- **Readiness (v2.6):** buildable as a layer on W-T once NQ bid/ask depth is confirmed
- **Feasibility · costs:** Only W-T (from 2024-10-28 for @ES bid/ask); NQ bid/ask start date not given — verify. Costs per contract: ES $17 and MES $2.50 per round trip (confirmed 2026-11-01); NQ/MNQ legs keep the unconfirmed §2.1 R6 figures (NQ $9.48, MNQ $1.72). Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** Same 11:00 ET moment shown on MNQ 1m then MES 1m; MES shows a red stacked-imbalance bar overrun and green bubbles at the low (04_1350). · Instrument switch visible only via the chart title change (MNQ → MES); no indicator compares the two legs on screen. Frames: `jmX_ReJxFm8/04_1350_MES-1m-failed-sellers-stacked-imbalance.jpg`.
- **Test:** paired: host-leg vs chosen-leg R on the same events. **Kill:** chosen-leg mean R ≤ host-leg mean R.
- **Buildable specification (v2.7):** OF-10. Not buildable until a second footprint exists. This extract is unchanged.

#### YV-03 · Momentum burst (Pradeep Bonde): 4% range-expansion day out of an orderly base (stocks)
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** NEW (no stock-universe momentum-burst entry; conflicts with RG-08/A2-55 NR7 negative grade — the NR day is a pre-condition here)
- **Family · Source:** SW (swing, stocks) · YouTube: *Momentum Burst: How to Catch Breakouts Early* — Financial Wisdom — https://youtu.be/9ksbN69G7i8
- **Evidence:** **E** · claims: 8–40% moves in 3–5 days; 'thousands of these moves every year' [00:00, 01:22] · Lit: YouTube (Bonde practitioner).
- **Markets · TF:** US stocks $100M–$10B market cap · daily setup, intraday entry
- **Timeframes:** Daily — linear trend, ≥ 3-day orderly consolidation (pullback 8–20% max), no big breakdowns (most days < ADR), NR or negative day before, no 3 up-days in a row, ≥ 4% breakout day, close near high, volume > prior day; stocks [02:46–04:51; frames 01_0205–03_0426, 07_1020, 08_1050] · Intraday — scan in the first hour, enter in the first 10–15 min or at the consolidation/tight-candle break when up 4–6%; hourly chart for intraday volume; stocks [05:31–06:51; frames 04_0520, 05_0710] · Weekly — index 10/20 EMA (QQQ above both, rising) as market regime; Nasdaq 100 / S&P 500 / Russell 2000 [08:56–09:16] · 1/3/6-month — top-performer lists to find trending stocks [05:11]
- **Data · test window:** stock daily + intraday bars (not in the dxFeed futures pipeline — verify); ES/MES port only as a feasibility note
- **Why it might work:** Stair-step momentum: after an orderly rest, the first range-expansion day attracts follow-through buying for a few days.
- **Rules:** *Scan:* market cap $100M–$10B, up ≥ 4% today, no volume filter in the scanner [04:51]. *Filters:* the 8 daily criteria above; NR day before the breakout ideally < 2% move [06:11]. *Entry:* when up 4–6% or at the break of the consolidation range / tight-candle high; often within the first 10–15 min [06:31–06:51]. *Stop:* low of the breakout day, or half the entry day's range if the low is too far — 'typically 3% or less' [07:12]. Exit if the stock gives up a large part of the day's gain. *Size:* 3% risk × 25% position = 0.75% account risk [07:33]. *Exit:* choppy market → take profit fast, even same day; trending market → hold 3–5 days; trailing stop; exit on a gap below tolerance [09:36–09:56]. *Regime:* weekly 10/20 EMA on indices; breadth, A/D, % above 200DMA [08:36–09:16].
- **Sweep:** breakout threshold {4%, 6%}; stop {day low, ½ range}; hold {3, 5 days} → 8
- **Frequency (est.):** many per week across the universe (presenter: thousands per year)
- **Fails when:** selling-dominated markets ('even perfect setups fail' [08:15]); gap-downs; small-cap slippage
- **Needs:** ✗ stock universe data + scanner (not in the pipeline), ✓ M-DAILY ADR/NR helpers
- **Readiness (v2.6):** **data-blocked** for Ettienne's futures stack. ES port: a ≥ 4% ES day is a crisis-day event (very rare) — the literal rule does not transfer; any port would need a volatility-scaled threshold (catalogue proposal, untested).
- **Feasibility · costs:** 0.75% risk (3% × 25%) is below the 1% budget; fine. Futures port: not feasible as stated. Futures costs (ES $17 / MES $2.50 per round trip) and the 14-tick floor do not apply to stocks. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** Daily candles with two coloured MAs (likely 10/20, inference — legend not readable) on ALM; stair-step trend marked by a hand pointer (01_0205). · Animated slides: 'No Big Breakdowns — look for daily price moves below ADR' (02_0345); 'Min 4%' breakout candle with higher volume (03_0426). · Scanner slide: Market Cap > $100 mn, < $10 bn, Daily Change ≥ 4%, **No Volume Filter**, Top Performers (04_0520). · Entry slide: horizontal ENTRY line at the consolidation top; callout 'enter within the first 10–15 minutes… 14% to 30% on day one' (05_0710). Sizing slide: 3% RISK + 25% POSITION SIZE = 0.75% ACCOUNT RISK (06_0750). · AXTI daily with '$5.4' level and a checklist: Linear Trend ✓, Orderly Consolidation ✓, Narrow Range Day ✓, No 3 Days Up ✓, 4% Move ✓; the NR day labelled −1% and breakout +13% (07_1020, 08_1050). **Adds to speech:** two checklist boxes (No Big Breakdowns, Volume Higher, Strong Close) appear unticked in the frame while the narration says it 'checked all the boxes' [10:40]. Frames: `9ksbN69G7i8/04_0520_scanner-criteria.jpg`, `9ksbN69G7i8/05_0710_entry-at-consolidation-break.jpg`, `9ksbN69G7i8/08_1050_AXTI-checklist-NR-day-minus1pct.jpg`.
- **Test:** if stock data is ever added: vs a random 4%-up-day control with the same hold. **Kill:** no lift over the random-4%-day control after costs.

#### YV-04 · Environment → location → confirmation: discount pullback in value-up structure with absorption and a second seller failure (Robbins Cup order flow)
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of OF-04 / DV-16 (absorption at a level) with MT-01 location; GEX environment = EXISTING A2-36
- **Family · Source:** OF (variant) · YouTube: *Trading WORLD CHAMPION Reveals the Orderflow Strategy That Won the Robbins Cup (Step-by-Step)* — IQCapital (guest Chris Creamer) — https://youtu.be/PL7LKUsCgIQ
- **Evidence:** **E** · presenter claims: win rate 'usually floats around 60–65%', profit factor ≈ 1.8, typical 1.5–2 R [32:25–32:47]; Robbins Cup win stated by the channel · Lit: YouTube.
- **Markets · TF:** MNQ (presenter watches MNQ, not NQ, order flow [37:40]) · 1h/4h environment, 5m execution
- **Timeframes:** 1h / 4h — environment: value-up/-down structure (HH/HL, value migrating) [08:40–09:00; frame 01_1453] · Daily / session (pre-market) — GEX regime (naive GEX on NQ/QQQ/NDX), call wall, put wall, gamma flip, done before the open [09:22–14:12] · Session/range VP — value area defines discount (below VA) vs premium [15:14] · 15m — looked at [27:58] · 5m — execution: footprint candles, absorption at the wick extreme, ≥ 400% imbalances, second failure; participation gate (YV-05) [27:58–31:01]; MNQ · 1m — 'sometimes' [27:58]
- **Data · test window:** 1-tick + Bid/Ask · **W-T only; @ES bid/ask exists only from 2024-10-28** (dxFeed ticks from ≈2024-10-27; the catalogue's W-T 'Jul 2024' start does not hold for bid/ask) → ≈500 sessions, ≤ 4 configs (MNQ/NQ order flow; NQ bid/ask depth to verify)
- **Why it might work:** Pullbacks into discount inside a value-up structure trap late sellers; when their aggression is absorbed and fails twice, covering plus responsive buyers lift price back into value.
- **Rules:** *Environment:* value-up structure on 1h/4h; note GEX regime (positive = dampened, more failed breakouts; negative = amplified) [11:08–13:52]. *Location:* pullback below the value area (discount) into the 0.705 / 0.788 / 0.886 Fib zone of the last swing low→high, with an internal swing point; **a move below 0.886 invalidates** [25:08–27:17]. *Confirmation (5m footprint):* a bearish candle with heavy negative delta at the wick extreme (absorption) — not enough alone; then buy-side ≥ 400% imbalances light up; **wait for sellers to try again and fail a second time**, then go long [28:38–31:01]. *Stop:* under the confirmation/failed-seller area [31:01]. *Management:* buyers must reclaim the VA; if they fail → BE or cut [22:03–22:24]. *Target:* swing high (usually), POC or call wall; trail as price approaches them [31:44–32:25]. *Session:* first 1.5 h of NY only [05:11, 46:16]; session stop after 2 losses (YV-06).
- **Sweep:** fib zone {0.705–0.886, 0.786–0.886}; second-failure {required, not}; target {swing high, POC} → 8 (> W-T cap; pre-register 4)
- **Frequency (est.):** 1–3 per week (first 90 min only) — drafter estimate
- **Fails when:** absorption read the wrong way at sweeps (OF-04 B(−), Telonics); trend days where discount keeps falling; low participation (lunch)
- **Needs:** F-OF (delta ladder, 400% imbalance), ✓ F-PROF/F-COMP, ✓ M-SWING (fib from last swing), ✗ M-GEX (A2-36, data-blocked)
- **Readiness (v2.6):** buildable on W-T without the GEX arm; GEX arm data-blocked (A2-36)
- **Feasibility · costs:** Stop under the failed-seller wick on 5m MNQ — size not stated; apply the 14-tick floor in ES terms. W-T ≈500 sessions × first 90 min → small sample. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** All five frames are a hand-drawn **whiteboard**, not a chart: '1. ENVIRONMENT 2. LOCATION 3. CONFIRMATION' (01_1453–05_3802). · Environment sketch: stair-stepping value boxes (value up); below: 'GEX Positive + / NEG −' with a choppy vs trending squiggle (01_1453). · Confirmation sketch: two candles with a ladder drawn in, circled lower wicks, a second, higher failure, then a box over the range (02_2019, 03_2306). · Location: '705 / 788 / 886' written below the last value box; 'CALL WALL' and 'PUT WALL' labels added later (04_3001, 05_3802). · Participation: '20000' over '2000' written next to a volume squiggle (05_3802) — the spoken number is 20,000 contracts per 5m MNQ candle [38:02]. Frames: `PL7LKUsCgIQ/01_1453_whiteboard-environment-value-up-GEX.jpg`, `PL7LKUsCgIQ/03_2306_whiteboard-entry-stop-value-reclaim-target.jpg`, `PL7LKUsCgIQ/04_3001_whiteboard-fib-705-788-886-zone.jpg`.
- **Test:** vs OF-04 fade arm and a location-only control (enter at the Fib zone without flow). **Kill:** ≤ location-only control or ≤ K-SH.

#### YV-05 · Absolute participation gate: trade only while 5m volume ≥ a fixed contract count (MNQ 20,000/5m)
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of A2-50 (same-slot relative-volume gate → absolute contracts-per-bar threshold)
- **Family · Source:** RG (gate) · YouTube: *Trading WORLD CHAMPION Reveals the Orderflow Strategy That Won the Robbins Cup (Step-by-Step)* — IQCapital (guest Chris Creamer) — https://youtu.be/PL7LKUsCgIQ
- **Evidence:** **E** · presenter's own rule; no stats · Lit: YouTube.
- **Markets · TF:** MNQ (threshold is MNQ-specific) · 5m
- **Timeframes:** 5m — MNQ contracts per candle; below 20,000 'participation is dying out', likely heading into lunch [37:40–38:02; frame 05_3802]
- **Data · test window:** 1m · W-M (dxFeed 1m from **2016-12-29** per Ettienne's data note; §2.0 says Jul 2016 — use 2016-12-29) (volume only; no bid/ask needed)
- **Why it might work:** Low participation means fewer forced/trapped participants and weaker follow-through for flow setups.
- **Rules:** Allow host entries only if the last closed 5m bar volume ≥ V*; presenter V* = 20,000 MNQ contracts [38:02]. ES/MES port: V* must be re-derived (catalogue proposal: the MNQ-equivalent percentile of the 09:30–11:00 distribution); a fixed count drifts with contract popularity — prefer A2-50's relative version as the control.
- **Sweep:** V* {presenter fixed, percentile P30, P50 of the same slot} → 3
- **Frequency (est.):** gate; vetoes late-morning/lunch bars
- **Fails when:** fixed counts drift across years and roll weeks
- **Needs:** ✓ bar volume, ✓ M-TOD
- **Readiness (v2.6):** buildable now
- **Feasibility · costs:** Volume history covers W-M from 2016-12-29 for ES; MNQ only since May 2019. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** Whiteboard only: '20000' over '2000' beside a volume sketch (05_3802); no chart shown. Frames: `PL7LKUsCgIQ/05_3802_whiteboard-20000-contract-5m-threshold.jpg`.
- **Test:** ablation on hosts (YV-04, PS-01) vs A2-50. **Kill:** no lift over A2-50 or vetoes winners as fast as losers.

#### YV-06 · Session stop after 2 losses / 1–2 attempts per setup per day, inside a fixed early window
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of A2-78 (daily loss kill-switch) / A2-75 (hit-rate throttle): consecutive-loss count instead of a loss amount
- **Family · Source:** EX/risk (module) · YouTube: *Trading WORLD CHAMPION Reveals the Orderflow Strategy That Won the Robbins Cup (Step-by-Step)* — IQCapital (guest Chris Creamer) — https://youtu.be/PL7LKUsCgIQ; YouTube: *This 5 Minute Strategy Is The EASIEST Way To Become Profitable Fast* — JadeCap — https://youtu.be/DQiOqFFptfA
- **Evidence:** **E** · PL7: 'if I take three losses in a row there's like a 50/50 chance I start making bad decisions… stop after two losses'; trades 'dramatically worse' after the first 1.5 h [46:16–46:56]. DQi: 'one or two attempts… then get off the desk' [04:05, 12:34] · Lit: YouTube.
- **Markets · TF:** any intraday host · session
- **Timeframes:** Session (NY open + 90 min) — PL7 [05:11, 46:16] · Session/day — 1–2 attempts per day, DQi [04:05–04:26, 12:34]
- **Data · test window:** inherits the host window
- **Why it might work:** Limits clustering of losses on hostile days and behavioural degradation (presenters' rationale, not a market edge).
- **Rules:** After N consecutive losing trades in a session (N = 2), stand down until the next session; optionally cap attempts per setup per day at 2 (DQi) and stop new entries after 09:30 + 90 min (PL7).
- **Sweep:** N {2, 3}; window {90 min, none} → 4
- **Frequency (est.):** module
- **Fails when:** removes the recovery trades of good days; for mechanical systems the behavioural rationale does not apply
- **Needs:** ✓ F-TM / journal state
- **Readiness (v2.6):** buildable now (state counter)
- **Feasibility · costs:** no data constraint. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** No chart content for this rule in either video (PL7 talk segment; DQi slides at 03:03–04:26 are text).
- **Test:** ablation on every surviving intraday host; report R-variance and max DD at equal mean R. **Kill:** raises max DD or lowers net R vs A2-78.

#### YV-07 · ICT macro-window scalp: 5m FVG traded into inside xx:50–xx:10, 15s sweep of equal highs + new FVG → target equal lows
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** NEW (related to DV-08 FVG, DV-20 equal-lows logic, TS-02 London range — no macro-window or 15s entry exists)
- **Family · Source:** TS / SP · YouTube: *How to Find High Probability Scalps Every Day (No Daily Bias Needed)* — Dylan Richards — https://youtu.be/83d144f2e0k
- **Evidence:** **E** · claims: '$2,500 in less than two minutes' across 8 accounts; '≈10 points' in ES [00:00, 02:26, 08:35] · Lit: YouTube (ICT practitioner).
- **Markets · TF:** ES (5m S&P chart) · 5m levels/FVG, 15s entry
- **Timeframes:** 5m — key levels (London session lows, relatively equal lows), 09:30 open run, the bearish 5m FVG price trades back into; ES [02:26–05:30; frames 01_0255, 02_0635] · 1m — equal lows also identified on the 1m [08:15] · 15s — entry: sweep of relative equal highs, displacement creating a 15s FVG, short at its low; ES [06:33–08:35; frames 03_0720–05_0850] · Time windows — 'macro' = last 10 min of the hour + first 10 min of the next (here 09:50–10:10 ET) [06:12, 06:53] · Presenter says the model works on 'every time frame' from daily to 15s [09:16] — untested claim
- **Data · test window:** 1-tick + Bid/Ask · **W-T only; @ES bid/ask exists only from 2024-10-28** (dxFeed ticks from ≈2024-10-27; the catalogue's W-T 'Jul 2024' start does not hold for bid/ask) → ≈500 sessions, ≤ 4 configs (15s bars need ticks; 1m-only W-M arm possible with a 1m entry, a different rule)
- **Why it might work:** Stops resting above equal highs are run into a higher-TF imbalance at a scheduled liquidity time; the displacement after the run shows the side in control, and equal lows below are the next obvious stop pool.
- **Rules:** *Levels (5m):* London session high/low and relatively equal highs/lows = draw on liquidity [03:07]. *Context:* price trades back into a 5m FVG that must respect it to the downside (bearish order flow) [04:08]. *Time:* inside a macro window xx:50–xx:10 ET (example 09:50–10:10) [06:12]. *Entry (15s):* price runs above relatively equal highs, displaces down creating a 15s FVG; sell at the FVG **low** on the retrace [07:13–07:34]. *Stop:* at the high (intermediate-term high that took the liquidity) [07:55]. *Target:* the 5m/1m equal lows [07:55–08:15]. 'No daily bias needed' [10:58].
- **Sweep:** window {xx:50–xx:10, 09:30–10:10 only}; entry TF {15s, 1m} → 4
- **Frequency (est.):** presenter: 'every single trading day'; drafter: 0–2 per session
- **Fails when:** FVG/equal-high definitions are discretionary ('relatively equal'); 15s noise; costs on a 2–4 pt stop
- **Needs:** ✓ FVG helper (DV-08), ✓ M-SWING equal-high tolerance (DV-20), ✓ M-TOD windows, ✗ 15s bars from ticks (W-T)
- **Readiness (v2.6):** 1m arm buildable on W-M now; 15s arm W-T only
- **Feasibility · costs:** Shown example ≈9.75 pt (frame measure) vs '≈10 points' spoken. Stop at a 15s high is likely < 3.5 pt → **widened to the 14-tick floor**, which roughly halves the RR on examples like this; ES $17 ≈ 9.7% of a floored ES 1R ($17 / $175); MES $2.50 ≈ 14% of a 14-tick MES 1R ($2.50 / $17.50). Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** TradingView-style light chart, ES 5m: 9:30 line, red 'FVG' box, green 'Macro' box, and a dark-red 'London Session Lows' line below (01_0255). · 5m price returns up into the red FVG (02_0635). · 15s: price pierces the prior highs into the 5m FVG, then a small 'FVG' label below the wick (03_0720); circled entry at the 15s FVG with a magnet icon marking the target at the equal lows (04_0835). · Measure tool from entry to the lows shows ≈9.75 pts (05_0850) — **adds to speech** ('10 points'). Frames: `83d144f2e0k/01_0255_ES-5m-0930-drop-London-low-FVG-macro.jpg`, `83d144f2e0k/04_0835_ES-15s-short-entry-at-FVG.jpg`, `83d144f2e0k/05_0850_ES-15s-target-hit-measure-9.75pts.jpg`.
- **Test:** vs the same entry outside macro windows (time placebo) and vs a 5m-FVG-only entry. **Kill:** no lift over the outside-window placebo.
- **Buildable specification (v2.7):** SP-09 (1m/5m arm on W-M, 15s arm on W-T). This extract is unchanged.

#### YV-08 · Daily sweep strategy: 1h SFP of a prior-day/Asia/pre-NY swing → 5m FVG entry, candle-2 stop, 1:3 or opposing liquidity
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of PS-18 (stop-sweep buy: here a 1h close back over the swing, prior-day/Asia levels, 5m FVG entry, candle-2 stop, no P/b-profile filter); related DV-08, YV-07
- **Family · Source:** SP (variant) · YouTube: *This 5 Minute Strategy Is The EASIEST Way To Become Profitable Fast* — JadeCap — https://youtu.be/DQiOqFFptfA
- **Evidence:** **E** · claims: works at a 40–50% win rate; examples 3R long, 5R short [05:08, 10:53, 12:14] · Lit: YouTube (ICT/SFP practitioner).
- **Markets · TF:** instrument **not stated** (light-theme TradingView chart; ticker unreadable on storyboards) · 1h structure, 5m entry
- **Timeframes:** 1h — HTF structure and swing-failure pattern; the SFP is confirmed on the hourly close ('closure of this hourly candle'; bearish example closes at 09:00 ET) [08:52, 10:33, 11:54; frame 02_0559 (storyboard)] · Prior day / Asian session / pre-NY — levels to mark [07:11] · 5m — FVG entry, stop and target [08:11, 09:52–10:33, 11:54–12:14; frames 03_0628, 04_0727 (storyboards)] · Instrument: not stated; inference from storyboards: a TradingView chart (logo visible on 02_0559), symbol unreadable
- **Data · test window:** 1m · W-M (dxFeed 1m from **2016-12-29** per Ettienne's data note; §2.0 says Jul 2016 — use 2016-12-29) (pure price; no bid/ask needed)
- **Why it might work:** Stops beyond obvious swing points are run; a close back inside traps breakout traders, and the displacement FVG gives a defined-risk retest.
- **Rules:** *Levels:* previous day's swing highs/lows, Asian session range, price action before the NY open [07:11]. *Sweep:* price trades beyond the swing point; do nothing yet [07:51]. *Confirm:* a **1h close back** above the swept low (long) / below the swept high (short) = SFP [08:11, 10:33]. *Entry:* the 5m FVG left by the displacement — long at the FVG midpoint; short at the FVG low [09:52, 11:54]. *Stop:* beyond candle 2 of the FVG [10:13, 11:54]. *Target:* fixed 1:3 (1:2 or 1:3 for shorts) or opposing liquidity — buy-side liquidity / Asian range low / weekly open [10:13, 11:54]. *Frequency rule:* 1–2 attempts per day (YV-06) [04:05, 12:34].
- **Sweep:** confirm TF {1h close, 15m close}; entry {FVG mid, FVG edge}; target {1:3, opposing liquidity} → 8 (pre-register 4)
- **Frequency (est.):** presenter: 'almost every single day'; with 1–2 attempts/day cap
- **Fails when:** trend days through the swept level; 1h-close confirmation arrives late, FVG may be far
- **Needs:** ✓ FVG (DV-08), ✓ M-SWING, ✓ M-TOD sessions (Asia, pre-NY), ✓ M-MTF (1h close)
- **Readiness (v2.6):** buildable now on W-M (price-only)
- **Feasibility · costs:** Candle-2 stop on 5m is often < 14 ticks on ES in quiet sessions → floor applies; ES $17 ≈ 9.7% of a floored ES 1R ($17 / $175); MES $2.50 ≈ 14% of a 14-tick MES 1R ($2.50 / $17.50). W-M ≈ 2,450 sessions from 2016-12-29 → adequate sample. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** **Storyboard tiles only (≈320×180 upscaled; the video download was bot-blocked)** — fine detail unreadable. · Slide 'Bullish Sweep — FVG Long' with a 5-step checklist card and two chart panels (01_0534). · 1h chart (TradingView logo bottom-left): several swing lows marked with curved arrows; a crosshair/horizontal line at the right where the latest low is swept (02_0559). · 5m long: a large green target box above a small pink stop box at the right of a sharp spike low, below a horizontal level (03_0628) — consistent with ≈1:3. · Bearish example: pink stop box at the swept high and a tall grey target region below, price falling into it (04_0727); summary slide 'The Daily Sweep Strategy — Wait for the sweep. Then strike.' (05_0742). Frames: `DQiOqFFptfA/02_0559_storyboard_1h-SFP-sweep-of-low.jpg`, `DQiOqFFptfA/03_0628_storyboard_5m-FVG-long-position-tool.jpg`, `DQiOqFFptfA/04_0727_storyboard_bearish-FVG-short-position-tool.jpg`.
- **Test:** vs PS-18 and vs the same entry without the 1h-close requirement. **Kill:** ≤ PS-18 or no lift from the 1h close.

#### YV-09 · Turtle Donchian breakout: System 1 (20-day high/low, 10-day exit) and System 2 (55-day, 20-day exit); EMA20-close exit arm
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** NEW (no Donchian/Turtle entry in the catalogue; related RG-06 TSMOM, A2-71 Turtle DD overlay = sizing only, PF-01 trend core)
- **Family · Source:** PF/SW (trend) · YouTube: *With This Simple Breakout Strategy, He Turned $1,600 Into a $1.2 Billion Fortune.* — Dollar Wisdom — https://youtu.be/lDn93erRQSM
- **Evidence:** **E** for this video (claims: win rate below 40% [10:16]; NVDA example breakout ≈ $51 Jan 2024 then '+90% in 43 trading days' [15:19–15:44]; '$1,600 → $1.2 billion' title claim) · Lit: YouTube; channel-breakout trend-following has an independent literature not reviewed in this pass.
- **Markets · TF:** 'commodities and futures' generally (diversified); example NVDA stock · daily
- **Timeframes:** Daily — System 1: entry on a 20-day high (long) / 20-day low (short), exit on a 10-day low / high [08:02–08:40; frame 01_0840] · Daily — System 2: 55-day breakout, exit on the 20-day low/high [08:52–09:20; frames 02_0920, 03_0940] · Daily — modern arm: Donchian 55 + exit on a close below the 20-period EMA, NVDA 1D [14:05–16:10; frames 04_1430–07_1615]
- **Data · test window:** daily · W-D (ES since 1997, MES since May 2019; roll-gap-removed series, continuous symbols are not back-adjusted); ES/MES only, or a futures basket (CL, GC, ZN… via M-XMKT/PF-01) for the diversified form
- **Why it might work:** Trend persistence (fat right tails) in futures; few large winners pay for many small losses (presenter: <40% win rate).
- **Rules:** *S1:* buy when price exceeds the 20-day high; exit when price falls below the 10-day low; shorts mirror [08:02]. *S2:* buy on a 55-day high; exit on the 20-day low; shorts mirror [08:52]. *Modern arm:* 55-day breakout entry, exit on a daily **close below EMA(20)** [14:45–16:32]. *Risk:* 1–2% of account per trade [11:04, 15:34]. *Diversify* across markets [11:52]. The video also says Dennis' original S2 exit was a close below the 20-day low [14:57]. *Stop:* the video gives **no initial stop**; the frame calls the opposite channel line 'your short trigger or stop reference'. Catalogue assumption (not from the video): initial stop = the exit channel (10-day / 20-day) at entry, so 1% sizing is defined.
- **Sweep:** system {S1, S2, 55/EMA20} × side {long only, long/short} → 6
- **Frequency (est.):** ES alone: a few entries per year per system
- **Fails when:** range years (2015-style); whipsaw around the channel; single-market concentration
- **Needs:** ✓ M-DAILY (Donchian, EMA), roll-gap-removed series, ◐ M-XMKT for a basket
- **Readiness (v2.6):** buildable now on W-D
- **Feasibility · costs:** Daily stops are many points wide: 1% risk on ES (stop = channel distance, e.g. 60–150 pt = $3k–7.5k/contract) needs ≥ $300k–750k equity per ES contract; **MES** (×$5) brings this to ≈ $30k–75k. 14-tick floor never binds; costs (ES $17 / MES $2.50 per round trip) negligible. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** Rule slides: 'System 1 — 20-day breakout, 10-day exit' and 'System 2 — 55-day, 20-day exit'; a side-by-side rules table (01_0840, 02_0920, 03_0940). Win-rate claim slide (08_1015). · TradingView 'NVIDIA Corporation · 1D · NASDAQ' with 'DC 55 0' Donchian channel and 'EMA 20 close' (04_1430, 05_1500). · Breakout arrow at the upper Donchian line (~Jan 2024) with 'Risk only 1–2% of account' note; no initial stop price is drawn (06_1545). · Exit arrow where price closes below the EMA20 (~early April 2024) with the '+90%' annotation (07_1615). · **Contradiction:** the legend values (O 180.18, DC 195.62 / 164.07, EMA 182.88) are current values that do not match the plotted price scale (~$28–92, Aug 2023–Apr 2024, split-adjusted) — the legend is not the example's data. Frames: `lDn93erRQSM/03_0940_system1-vs-system2-rules-table.jpg`, `lDn93erRQSM/06_1545_NVDA-daily-55d-breakout-risk-1-2pct.jpg`, `lDn93erRQSM/07_1615_NVDA-daily-exit-close-below-EMA20-90pct.jpg`.
- **Test:** vs RG-06 TSMOM(20) sign and buy-and-hold ES on W-D, net of $17 RT per ES contract ($2.50 per MES contract if run on MES); holdout last 25% of years. **Kill:** holdout Sharpe ≤ TSMOM(20) or ≤ 0.3 net.
- **Buildable specification (v2.7):** PF-08. Optional Faith arms (N, 2N, pyramid, S1 skip) are labelled there and are not part of this video extract. This extract is unchanged.

#### YV-10 · Labelled Markov regime: 20-day-return states (±5%), 3×3 transition matrix, signal = P(bull) − P(bear) for tomorrow, HMM agreement
- **Implementation status (v2.6):** **Not started** — catalogue entry only; no entry-specific code or build spec.
- **Classification (v2.6):** VARIANT of RG-06 (TSMOM(20) sign) + the G-R `_RegimeEngine` (Gaussian HMM K = 3, walk-forward): discrete labels + empirical transition matrix + signed-probability sizing are new
- **Family · Source:** RG (module / daily standalone) · YouTube: *I Re-Created A Quant Trading Strategy With Claude Code (Insanely Cool)* — Lewis Jackson Investing — https://youtu.be/ZVMTeDBmSrI
- **Evidence:** **E** · no performance stats given; 'hedge funds and quants' framing only · Lit: YouTube (Pine Script built by Claude Code; demo values only).
- **Markets · TF:** BTC (demo), also XRP, TSLA, SPY 10-year mentioned · daily
- **Timeframes:** Daily — state from the trailing 20-day return: ≥ +5% bull, ≤ −5% bear, else sideways [03:12–03:53; frame 01_0227] · Daily (next day) — transition probabilities, n-day forecasts by matrix powers, stationary distribution [07:00–13:11; frames 02_0455, 03_0495] · Chart TF of the BTC Pine demo: not stated; frame 05_1555 shows a candle chart with the Markov tables — bar size unreadable (inference: daily, since the model is daily)
- **Data · test window:** daily · W-D (ES since 1997, MES since May 2019; roll-gap-removed series, continuous symbols are not back-adjusted) (20-day returns on a roll-gap-removed series)
- **Why it might work:** State persistence (diagonal 'stickiness') makes tomorrow's state partly predictable; a signed probability spread gives direction and size.
- **Rules:** *Label:* each day's state from the sum of the last 20 daily returns vs ±5% [03:12]. *Estimate:* count all state→next-state transitions, row-normalise to a 3×3 matrix (walk-forward: only past data) [06:20–07:21, 16:17]. *Signal:* s = P(bull tomorrow | today) − P(bear tomorrow | today); long if s > 0, short if s < 0; size ∝ |s| (example 65% − 20% = +45%) [13:32–15:37]. *Confirmation:* also fit an unsupervised HMM with 3 states; trade only when the HMM state agrees with the label [18:40–20:41]. Size mapping from |s| is not specified ('each fund has its own').
- **Sweep:** threshold {±5%, ±3%}; window {20, 60}; HMM gate {on, off} → 8 (pre-register 4)
- **Frequency (est.):** daily signal; position changes a few times per month
- **Fails when:** labels are a lagging TSMOM transform (likely ≈ RG-06); matrix rows dominated by the diagonal → signal ≈ current state
- **Needs:** ✓ M-DAILY, ◐ G-R `_RegimeEngine` (HMM K = 3 walk-forward, PR #23)
- **Readiness (v2.6):** buildable now on W-D (label/matrix part); HMM gate via `_RegimeEngine`
- **Feasibility · costs:** As a daily ES/MES position, 1% risk needs a defined stop (none in the video) → use as an exposure/regime module on hosts, or stress-size like VX. Costs (ES $17 / MES $2.50 per round trip) negligible. Costs/risk per §2.1 (v2.6a, confirmed 2026-11-01): **ES $17 per round trip**, **MES $2.50 per round trip** (= 2 MES ticks), 1% risk per trade, stop = max(structural, 14-tick floor). On ES the floor is 3.5 pt = $175 + $17 = $192 per contract at 1% → ≥ $19.2k equity per ES contract (cost ≈ 0.097 R). On MES the 14-tick floor is $17.50 + $2.50 = $20 per contract at 1% → ≥ $2k equity per MES contract; cost = 2/14 ticks ≈ 0.14 R at 14 ticks, 0.10 R at the §2.1 MES floor of 20 ticks.
- **What to look for (frames, `video-ideas-2026-11-01/frames/`):** **Storyboard tiles only (≈9.9 s apart; video download bot-blocked).** · 'The 20-day rule' slide: 20 cells summing to Σ +20%; '1% × 20 = +20% → BULL', '15·(+1%) + 5·(−1%) = +10% → BULL', 'Σ ≤ −5% → BEAR' (01_0227). · 'Count → probability' 3×3 grid, rows today / columns tomorrow: Bull 80/15/5, Side 20/50/30, Bear 5/20/75, each row '= 100%' — an illustrative matrix, not fitted data (02_0455). · Diagonal highlighted for persistence (03_0495); '65% − 20% = +45%' with a LONG bar (04_0891). · BTC chart (TradingView) with a 3×3 table top-right and a 3-cell 'long-run mix' row bottom-right (05_1555); spoken values 29 / 42 / 29 (bull/bear/sideways) [25:47–26:07] — **ambiguity:** the speaker calls it the 'long run mix' and also 'tomorrow', so it may be the stationary distribution, not the next-day row. Frames: `ZVMTeDBmSrI/01_0227_storyboard_20d-return-state-rule-plus-minus-5pct.jpg`, `ZVMTeDBmSrI/02_0455_storyboard_example-3x3-transition-matrix.jpg`, `ZVMTeDBmSrI/04_0891_storyboard_signal-Pbull-minus-Pbear-plus45.jpg`.
- **Test:** vs RG-06 TSMOM(20) sign on W-D (information beyond the sign of the 20-day return?). **Kill:** correlation with TSMOM(20) sign > 0.9 or no lift over it.
---

## 4. Prioritisation matrix

**Scores (1–5; halves allowed):**
- **Ev (evidence):** A = 5, B(+) = 4, C(+) = 3, D = 2, E/H = 1.5, null or negative = 1.
- **Nov (novelty / information value):** 5 = nobody has tested it; 1 = already tested and dead.
- **Freq (estimated events):** 5 ≥ 10/month, 4 = 4–10, 3 = 2–4, 2 = 1–2, 1 < 1.
- **Impl/Data:**
  - 5 = all base features exist and the full W-M window is usable;
  - 4 = a minor helper is needed;
  - 3 = needs M-SWING/M-TOD, or ETH bars;
  - 2 = needs M-AVWAP/M-MTF;
  - *(As of merge: M-SWING, M-TOD, M-AVWAP and M-MTF exist (§2.4a), so ideas scored 2–3 for them would now score 4. The matrix scores below are **not** re-run; use each entry's Readiness line.)*
  - 1 = data-blocked, or W-T only for a standalone idea.
  - **Order-flow ideas lose 1 point for the ≈500-session tick window (Rev 2.7).**
- **Smooth (fit to the smooth-equity objective):** frequency × independence of return source × expected low R-variance.

**Priority = (0.30·Ev + 0.10·Nov + 0.20·Freq + 0.20·Impl + 0.20·Smooth) / 5 × 100.** Type: S = standalone, L = layer (rides on host events), K = canary/control.

**v2:** Gate 0 (§2.6) runs before the ranking is used. A level-based idea's score is irrelevant until A2-33 has passed its level type. The weights are a judgement call, not a statistic. Change them and re-run `_cat/matrix.py` if your priorities differ. The batches in §5 deviate from the pure ranking on purpose: they add family diversity, force construction of M-AVWAP early, and keep trapped-trader ideas for their own round.

### 4.1 Top 40 by priority (v2; v2.3 adds PS-25)

| Rank | ID | Idea | Type | Priority |
|---|---|---|---|---|
| 1 | TS-01 | European-open overnight drift (MES) | S | 84 |
| 2 | PS-24 | Overnight high/low break-and-go | S | 76 |
| 3 | A2-49 | Midday S/R + afternoon breakout (Kaufman) | S | 76 |
| 4 | PS-07 | Open-drive first-pullback continuation | S | 74 |
| 5 | OF-07 | Bar-level burst at fresh extreme | S | 72 |
| 6 | A2-09 | Noise-band long-only + sVWAP trail (MNQ benchmark) | S | 72 |
| 7 | A2-63 | Open + 0.5×ATR20d long-only breakout (H2) | S | 72 |
| 8 | VW-05 | Late-day VWAP-side continuation | S | 71 |
| 9 | SW-02 | RSI(2) dip-buy + profile/AVWAP filter | S | 71 |
| 10 | PS-08 | IB extension with acceptance (narrow IB) | S | 70 |
| 11 | MT-03 | 20-day composite location gate | L | 69 |
| 12 | TS-03 | MOC window drift / reversion | S | 69 |
| 13 | TS-07 | Opening blackout length test | L | 68 |
| 14 | A2-12 | VWAP−TWAP divergence (event study) | S | 68 |
| 15 | A2-58 | Fade fast trend inside slow trend (5m) | S | 68 |
| 16 | A2-69 | Kaufman 3-day trade (2 down closes, lower open) | S | 68 |
| 17 | PS-19 | Poor high/low run-through | S | 67 |
| 18 | A2-60 | Meyers normalised range-ratio breakout | S | 67 |
| 19 | DV-15 | EMA20 pullback gated by VWAP slope | S | 67 |
| 20 | MT-01 | HTF-aligned HL at value (nesting) | S | 66 |
| 21 | TS-04 | CL/GC pit-session profile port | L | 66 |
| 22 | PS-01 | Accepted balance break → first-pullback HL | S | 64 |
| 23 | A2-51 | Same-slot shock-bar fade | S | 64 |
| 24 | A2-56 | Momentum Pinball (LBR/RSI + first-hour break) | S | 64 |
| 25 | A2-64 | 1×ATR20d fade from open/prior close | S | 64 |
| 26 | PS-25 | Accept-beyond-rail continuation (detrended rails, Keller B5) | S | 64 [est]¹ |
| 27 | A2-52 | Time-confirmed ORB + pivot bias (ACD) | S | 63 |
| 28 | DV-23 | IB draw trade (guest) | S | 63 |
| 29 | PS-02 | Balance break at acceptance close | S | 62 |
| 30 | PS-22 | Composite LVN-edge rejection / HVN veto | S | 62 |
| 31 | AV-11 | Breakout-bar AVWAP first pullback | S | 61 |
| 32 | TS-06 | Overnight inventory correction | S | 61 |
| 33 | PS-05 | Rejected repricing open (IX-3) | S | 60 |
| 34 | PS-06 | Defeated adjustment continuation (IX-2) | S | 60 |
| 35 | PS-09 | IB C-period failure → IB mid | S | 60 |
| 36 | A2-65 | Raschke 1st cross (daily/30m) | S | 60 |
| 37 | DV-02 | Failed level → flip retest (+ DV-21 PA levels) | S | 60 |
| 38 | PS-20 | Single-print pullback continuation | S | 59 |
| 39 | VW-03 | σ-band expansion continuation | S | 59 |
| 40 | AV-02 | Impulse-origin AVWAP higher low | S | 59 |

*v2.3 note:* PS-25 enters at rank 26 on **drafter-estimated** scores ([est]¹, §4.2); the ranks below shift by one and **SW-01 (58) drops out of the top 40 on estimates alone — pending Ettienne's confirmation**. Until then, read SW-01 as rank 40 tied-in-waiting; no other score was re-run.

*v2.5 note:* the 13 VX/LE rows are **drafter estimates** (§4.2 ²). Estimated priorities: LE-02 66, LE-04 59, VX-06 58, VX-01 52, LE-06 52, LE-01 50, LE-05 50, LE-03 43, VX-03 42, VX-02 38, VX-04 33, VX-08 33, LE-07 32. Rows at or above the rank-40 cut (59) on estimates: **LE-02** (66), **LE-04** (59); they are **not** inserted into the ranks above until Ettienne confirms the scores (the table above is unchanged). R7's own shortlist (evidence- and data-weighted, not the matrix): VX-06, LE-02, LE-01, VX-01, LE-04.

*v2.6 note:* the 8 YV rows are **drafter estimates** (§4.2 ³). Estimated priorities: YV-08 57, YV-10 55, YV-07 53, YV-03 51, YV-09 47, YV-02 45, YV-01 41, YV-04 41. None reaches the rank-40 cut (59); ranks unchanged.

*v2.7 note:* four further **drafter estimates** (§4.2 ⁴): PF-08 49, SP-09 50, OF-10 37, OF-09 35. None reaches the rank-40 cut (59). Ranks above are unchanged. EX-13 is not scored (module).

### 4.2 Full matrix (125 testable entries: standalone, layers, canaries; v2 adds 37 rows: 25 A2 and 12 DV; v2.3 adds PS-25 = KD-01 and MT-07 = KD-05 [est]; v2.5 adds 13 VX/LE rows [est] → 138; v2.6 adds 8 YV rows [est] → 146; v2.7 adds 4 rows [est] → 150)

| ID | Idea | Type | Ev | Nov | Freq | Impl/Data | Smooth | **Priority** |
|---|---|---|---|---|---|---|---|---|
| PS-01 | Accepted balance break → first-pullback HL | S | 3 | 3 | 3 | 3 | 4 | **64** |
| PS-02 | Balance break at acceptance close | S | 3 | 2 | 3 | 4 | 3 | **62** |
| PS-03 | Confirmed failed break → rotation (80% canary) | S | 2 | 3 | 3 | 3 | 3 | **54** |
| PS-04 | Prior-value magnet on small newsless gap (IX-1) | S | 2 | 4 | 3 | 3 | 3 | **56** |
| PS-05 | Rejected repricing open (IX-3) | S | 2 | 4 | 3 | 4 | 3 | **60** |
| PS-06 | Defeated adjustment continuation (IX-2) | S | 2 | 4 | 3 | 4 | 3 | **60** |
| PS-07 | Open-drive first-pullback continuation | S | 4 | 3 | 3 | 4 | 4 | **74** |
| PS-08 | IB extension with acceptance (narrow IB) | S | 3 | 2 | 4 | 5 | 3 | **70** |
| PS-09 | IB C-period failure → IB mid | S | 2 | 2 | 3 | 5 | 3 | **60** |
| PS-10 | Developing-POC migration join | S | 2 | 4 | 3 | 3 | 3 | **56** |
| PS-11 | Unratified new extreme fade | S | 1.5 | 4 | 3 | 3 | 2 | **49** |
| PS-12 | Trend-cluster pullback (Dale/Keller) | S | 1.5 | 3 | 4 | 2 | 3 | **51** |
| PS-13 | Volume-accumulation first touch | S | 1.5 | 3 | 3 | 3 | 2 | **47** |
| PS-14 | Rejection-ledge slow revisit fade | S | 1.5 | 3 | 3 | 3 | 2 | **47** |
| PS-15 | LVN traverse after acceptance | S | 1.5 | 2 | 4 | 4 | 3 | **57** |
| PS-16 | LVN slow-approach bounce (Levy) | S | 1.5 | 3 | 4 | 3 | 3 | **55** |
| PS-17 | Double-distribution waist | S | 1.5 | 3 | 2 | 4 | 2 | **47** |
| PS-18 | P-profile stop-sweep buy (Levy) | S | 1 | 3 | 3 | 3 | 2 | **44** |
| PS-19 | Poor high/low run-through | S | 2.5 | 4 | 3 | 5 | 3 | **67** |
| PS-20 | Single-print pullback continuation | S | 2.5 | 4 | 3 | 3 | 3 | **59** |
| PS-21 | Naked POC/VA first touch (control) | K | 1 | 1 | 4 | 5 | 2 | **52** |
| PS-22 | Composite LVN-edge rejection / HVN veto | S | 2 | 3 | 4 | 4 | 3 | **62** |
| PS-23 | Profile shape → next-session tilt | S | 1.5 | 3 | 3 | 4 | 2 | **51** |
| PS-24 | Overnight high/low break-and-go | S | 3 | 3 | 4 | 5 | 4 | **76** |
| PS-25 | Accept-beyond-rail continuation (detrended rails, KD-01) | S | 3 | 5 [est] | 4 [est] | 2 [est] | 3 [est] | **64** [est]¹ |
| VW-01 | Trend-day first VWAP pullback | S | 2 | 3 | 3 | 3 | 3 | **54** |
| VW-02 | Balance-day ±2σ fade at composite edge | S | 1 | 2 | 3 | 4 | 2 | **46** |
| VW-03 | σ-band expansion continuation | S | 1.5 | 5 | 3 | 4 | 3 | **59** |
| VW-04 | Failed VWAP defence reversal | S | 2 | 4 | 2 | 3 | 2 | **48** |
| VW-05 | Late-day VWAP-side continuation | S | 2.5 | 2 | 5 | 5 | 3 | **71** |
| VW-06 | POC–VWAP skew pullback | S | 1.5 | 4 | 3 | 3 | 3 | **53** |
| VW-07 | Prior-day / ETH VWAP location layer | L | 1.5 | 2 | 5 | 3 | 3 | **57** |
| VW-08 | VWAP-cross Holy Grail (canary) | K | 1 | 1 | 5 | 5 | 1 | **52** |
| AV-01 | Swing-high AVWAP reclaim (Shannon) | S | 1.5 | 5 | 2 | 2 | 3 | **47** |
| AV-02 | Impulse-origin AVWAP higher low | S | 1.5 | 5 | 4 | 2 | 4 | **59** |
| AV-03 | AVWAP pinch breakout (Shannon) | S | 1.5 | 5 | 2 | 2 | 3 | **47** |
| AV-04 | AVWAP handoff continuation | S | 1.5 | 5 | 2 | 2 | 3 | **47** |
| AV-05 | Gap-anchored AVWAP swing hold | S | 1.5 | 5 | 1 | 2 | 2 | **39** |
| AV-06 | Event-bar AVWAP acceptance | S | 1.5 | 5 | 3 | 1.5 | 3 | **49** |
| AV-07 | Climax-volume-bar AVWAP cross | S | 1 | 5 | 3 | 2 | 3 | **48** |
| AV-08 | WTD/MTD AVWAP regime layer | L | 1.5 | 3 | 5 | 2 | 3 | **55** |
| AV-09 | Roll-anchored AVWAP bias | S | 1 | 5 | 1 | 2 | 2 | **36** |
| AV-10 | AVWAP × profile confluence | L | 1.5 | 4 | 4 | 2 | 3 | **53** |
| AV-11 | Breakout-bar AVWAP first pullback | S | 2.5 | 5 | 3 | 2 | 4 | **61** |
| AV-12 | Failed AVWAP reclaim → opposing AVWAP | S | 1 | 5 | 2 | 2 | 2 | **40** |
| MT-01 | HTF-aligned HL at value (nesting) | S | 2 | 3 | 5 | 2 | 5 | **66** |
| MT-02 | Weekly accepted value + 60m trigger | S | 2 | 4 | 2 | 1.5 | 3 | **46** |
| MT-03 | 20-day composite location gate | L | 2.5 | 3 | 4 | 5 | 3 | **69** |
| MT-04 | 60m swing-AVWAP + 5m HL | S | 1.5 | 5 | 3 | 1.5 | 4 | **53** |
| MT-05 | 240m multi-session LVN pullback | S | 1 | 4 | 3 | 3 | 3 | **50** |
| MT-06 | Daily excess high + intraday trigger | S | 2 | 3 | 3 | 3 | 3 | **54** |
| MT-07 | HTF detrended-rail alignment (KD-05 layer on PS-25) | L | 1.5 | 4 [est] | 2 [est] | 2 [est] | 3 [est] | **45** [est]¹ |
| OF-01 | Stacked-imbalance origin retest | S | 1.5 | 3 | 4 | 2 | 3 | **51** |
| OF-02 | Delta-z ratification layer | L | 2 | 3 | 4 | 2 | 3 | **54** |
| OF-03 | Anchored CVD flip layer | L | 2 | 3 | 3 | 2 | 3 | **50** |
| OF-04 | Absorption at aged level (control) | K | 1 | 1 | 3 | 1 | 1 | **28** |
| OF-05 | CVD divergence at value extreme | S | 1 | 2 | 3 | 2 | 2 | **38** |
| OF-06 | Unfinished-auction veto | L | 1.5 | 3 | 4 | 1 | 3 | **47** |
| OF-07 | Bar-level burst at fresh extreme | S | 3 | 3 | 5 | 3 | 4 | **72** |
| OF-08 | Large-trade print retest | S | 1 | 3 | 3 | 1 | 2 | **36** |
| SP-01 | Failed double top → breakout | S | 2 | 3 | 3 | 3 | 3 | **54** |
| SP-02 | Double top at VAH (control) | K | 1 | 1 | 3 | 3 | 2 | **40** |
| SP-03 | Wyckoff spring + test at composite VAL | S | 1.5 | 2 | 2 | 3 | 2 | **41** |
| SP-04 | LH after failed auction at composite edge | S | 1.5 | 2 | 3 | 3 | 2 | **45** |
| SP-05 | Four-part reversal-bar event | S | 1 | 2 | 3 | 4 | 2 | **46** |
| SP-06 | First HL after downtrend at composite VAL | S | 1.5 | 4 | 2 | 3 | 3 | **49** |
| SP-07 | Failed-failure re-break (Spec 4) | S | 2 | 3 | 2 | 3 | 3 | **50** |
| SP-08 | Point-of-Origin retest (Levy) | S | 1.5 | 4 | 4 | 3 | 3 | **57** |
| TS-01 | European-open overnight drift (MES) | S | 4 | 2 | 5 | 4 | 5 | **84** |
| TS-02 | London-open Asia-range break + AVWAP | S | 1 | 4 | 4 | 3 | 3 | **54** |
| TS-03 | MOC window drift / reversion | S | 2.5 | 3 | 5 | 4 | 3 | **69** |
| TS-04 | CL/GC pit-session profile port | L | 2 | 3 | 4 | 4 | 4 | **66** |
| TS-05 | ZN post-release AVWAP acceptance | S | 1.5 | 5 | 4 | 1.5 | 3 | **53** |
| TS-06 | Overnight inventory correction | S | 1.5 | 4 | 4 | 4 | 3 | **61** |
| TS-07 | Opening blackout length test | L | 2 | 2 | 5 | 5 | 3 | **68** |
| SW-01 | Weekly balance break | S | 3 | 4 | 2 | 2 | 4 | **58** |
| SW-02 | RSI(2) dip-buy + profile/AVWAP filter | S | 3.5 | 3 | 3 | 4 | 4 | **71** |
| SW-03 | QTD/MTD AVWAP swing pullback | S | 1.5 | 5 | 1 | 2 | 3 | **43** |
| SW-04 | Multi-day P-profile hold | S | 1.5 | 3 | 2 | 4 | 2 | **47** |
| SW-05 | Composite gap-and-go | S | 1.5 | 4 | 2 | 4 | 3 | **53** |
| SW-06 | Weekly IB breakout | S | 1 | 5 | 3 | 3 | 3 | **52** |
| A2-04 | Overnight inventory vs oVWAP open drive | S | 1.5 | 3 | 3 | 4 | 3 | **55** |
| A2-09 | Noise-band long-only + sVWAP trail (MNQ benchmark) | S | 4 | 2 | 4 | 3 | 4 | **72** |
| A2-11 | Prior-day VWAP static acceptance | S | 1.5 | 3 | 3 | 3 | 3 | **51** |
| A2-12 | VWAP−TWAP divergence (event study) | S | 1 | 5 | 5 | 5 | 3 | **68** |
| A2-13 | Gap-day AVWAP-from-open hold/fail | S | 1.5 | 4 | 3 | 4 | 3 | **57** |
| A2-14 | Weekly AVWAP reclaim swing | S | 1.5 | 4 | 2 | 3 | 3 | **49** |
| A2-15 | Breakout-leg POC vs AVWAP skew layer | L | 1.5 | 4 | 3 | 2 | 3 | **49** |
| A2-27 | Bimodal-profile void traverse | S | 1.5 | 4 | 2 | 3 | 3 | **49** |
| A2-28 | Minor-component excess rejection | S | 1.5 | 4 | 2 | 3 | 2 | **45** |
| A2-30 | Rising-entropy balance break | S | 1.5 | 5 | 2 | 3 | 3 | **51** |
| A2-32 | σ-space mixture composite break | S | 1.5 | 5 | 3 | 2 | 3 | **51** |
| A2-35 | Macro-event re-formation VA (CPI/NFP/FOMC) | S | 2 | 4 | 3 | 3 | 3 | **56** |
| A2-38 | Metaorder drift completion (W-T) | S | 1.5 | 5 | 2 | 1 | 2 | **39** |
| A2-45 | Single-print shelf as destination | S | 1.5 | 4 | 3 | 4 | 3 | **57** |
| A2-46 | Volume-gap commitment to far node | S | 1.5 | 4 | 3 | 4 | 3 | **57** |
| A2-49 | Midday S/R + afternoon breakout (Kaufman) | S | 3 | 3 | 5 | 5 | 3 | **76** |
| A2-51 | Same-slot shock-bar fade | S | 2 | 4 | 4 | 4 | 3 | **64** |
| A2-52 | Time-confirmed ORB + pivot bias (ACD) | S | 1.5 | 3 | 5 | 4 | 3 | **63** |
| A2-53 | RTH gap fade SAR (negative canary) | K | 1 | 1 | 3 | 5 | 1 | **44** |
| A2-54 | Up-gap pullback-to-close short, exit next open | S | 1.5 | 4 | 3 | 3 | 3 | **53** |
| A2-56 | Momentum Pinball (LBR/RSI + first-hour break) | S | 3 | 3 | 3 | 4 | 3 | **64** |
| A2-57 | HTF turn + first short-TF oversold | S | 1.5 | 4 | 4 | 3 | 3 | **57** |
| A2-58 | Fade fast trend inside slow trend (5m) | S | 2 | 4 | 4 | 5 | 3 | **68** |
| A2-60 | Meyers normalised range-ratio breakout | S | 1.5 | 3 | 5 | 5 | 3 | **67** |
| A2-62 | Taylor 3-day cycle buying day | S | 1.5 | 3 | 3 | 4 | 2 | **51** |
| A2-63 | Open + 0.5×ATR20d long-only breakout (H2) | S | 3 | 3 | 4 | 5 | 3 | **72** |
| A2-64 | 1×ATR20d fade from open/prior close | S | 2 | 4 | 3 | 5 | 3 | **64** |
| A2-65 | Raschke 1st cross (daily/30m) | S | 3 | 3 | 2 | 4 | 3 | **60** |
| A2-66 | Raschke 2nd cross (daily/30m) | S | 2.5 | 3 | 1 | 4 | 3 | **53** |
| A2-67 | RSI-of-highs/lows divergence long | S | 1.5 | 3 | 2 | 5 | 2 | **51** |
| A2-68 | 5-bar run + doji reversal | S | 1.5 | 3 | 1 | 5 | 2 | **47** |
| A2-69 | Kaufman 3-day trade (2 down closes, lower open) | S | 3 | 3 | 3 | 4 | 4 | **68** |
| DV-01 | Daily POC first-touch pullback (control) | K | 1 | 1 | 4 | 5 | 2 | **52** |
| DV-02 | Failed level → flip retest (+ DV-21 PA levels) | S | 2 | 4 | 4 | 3 | 3 | **60** |
| DV-06 | End-of-trend-day cluster pullback | S | 1.5 | 3 | 3 | 3 | 2 | **47** |
| DV-07 | Rotation / double-distribution edge → centre | S | 1 | 2 | 3 | 4 | 2 | **46** |
| DV-08 | HV zone + FVG alignment layer | L | 1.5 | 4 | 3 | 4 | 3 | **57** |
| DV-09 | Weekly-VWAP ±1σ trend pullback | S | 1 | 2 | 4 | 2 | 2 | **42** |
| DV-11 | Weekly-VWAP σ1 trend-failure | S | 1.5 | 3 | 3 | 2 | 2 | **43** |
| DV-15 | EMA20 pullback gated by VWAP slope | S | 1.5 | 3 | 5 | 5 | 3 | **67** |
| DV-16 | Order-flow confirmation at level (layer) | L | 1.5 | 3 | 3 | 1 | 3 | **43** |
| DV-20 | Weak-low/high stop-cluster veto | L | 1.5 | 4 | 3 | 3 | 3 | **53** |
| DV-22 | ABCD measured-move fade | S | 1.5 | 2 | 3 | 3 | 2 | **45** |
| DV-23 | IB draw trade (guest) | S | 1.5 | 3 | 4 | 5 | 3 | **63** |
| VX-01 | Hedged VX term-structure carry | S | 4 | 4² | 2² | 1² | 2² | **52** [est] |
| VX-02 | VRP-filtered short vol (layer on VX-01) | L | 2 | 3² | 2² | 1² | 2² | **38** [est] |
| VX-03 | Long VX in backwardation | S | 3 | 4² | 1² | 1² | 2² | **42** [est] |
| VX-04 | Post-spike normalisation short | S | 1.5 | 4² | 1² | 1² | 2² | **33** [est] |
| VX-06 | VIX/VIX3M regime gate on ES hosts | L | 2 | 3² | 5² | 2² | 3² | **58** [est] |
| VX-08 | VIX-stretch dip-buy (layer on SW-02) | L | 1.5 | 2² | 1² | 2² | 2² | **33** [est] |
| LE-01 | 200-day trend leverage rotation (MES/MNQ) | S | 3 | 2² | 1² | 4² | 2² | **50** [est] |
| LE-02 | Vol-targeted leverage | S | 3 | 2² | 4² | 4² | 3² | **66** [est] |
| LE-03 | QQQ↔TQQQ regime leverage rotation | S | 1.5 | 3² | 1² | 4² | 2² | **43** [est] |
| LE-04 | LETF rebalancing flow into close (VW-05 arm) | L | 2.5 | 2² | 4² | 3² | 3² | **59** [est] |
| LE-05 | Decay-pair short TQQQ+SQQQ | S | 3 | 4² | 2² | 1² | 3² | **50** [est] |
| LE-06 | RSI mean reversion on LETFs | S | 2 | 2² | 3² | 4² | 2² | **52** [est] |
| LE-07 | Inverse-LETF bear hold (negative control) | K | 1 | 3² | 1² | 3² | 1² | **32** [est] |
| YV-01 | 30m VA break → 5m big trades → 1m failed sellers (PS-01 variant) | S | 1.5 | 2³ | 3³ | 1³ | 3³ | **41** [est] |
| YV-02 | Cross-index flow selection (layer) | L | 1.5 | 4³ | 3³ | 1³ | 3³ | **45** [est] |
| YV-03 | Momentum burst, stocks (data-blocked) | S | 1.5 | 3³ | 5³ | 1³ | 3³ | **51** [est] |
| YV-04 | Value-up discount + absorption second failure (OF-04 variant) | S | 1.5 | 2³ | 3³ | 1³ | 3³ | **41** [est] |
| YV-07 | Macro-window 15s sweep-FVG scalp | S | 1.5 | 4³ | 4³ | 2³ | 3³ | **53** [est] |
| YV-08 | 1h SFP sweep → 5m FVG (PS-18 variant) | S | 1.5 | 2³ | 4³ | 4³ | 3³ | **57** [est] |
| YV-09 | Turtle Donchian S1/S2 (daily) | S | 1.5 | 3³ | 1³ | 5³ | 2³ | **47** [est] |
| YV-10 | 20d-return Markov regime signal (layer) | L | 1.5 | 3³ | 3³ | 4³ | 3³ | **55** [est] |
| PF-08 | Donchian / Turtle daily sleeve (companion to PF-01) | S | 1.5 | 2⁴ | 1⁴ | 4⁴ | 4⁴ | **49** [est] |
| SP-09 | Clock-window sweep into an HTF FVG | S | 1 | 4⁴ | 3⁴ | 4⁴ | 2⁴ | **50** [est] |
| OF-09 | Weekly → 30m VA → 1m failed-seller cascade | S | 1.5 | 3⁴ | 2⁴ | 1⁴ | 2⁴ | **35** [est] |
| OF-10 | ES vs NQ order-flow selection (layer) | L | 1.5 | 4⁴ | 2⁴ | 1⁴ | 2⁴ | **37** [est] |

*Not scored (they ride inside a slot or are infrastructure):* EX-01…EX-13, RG-01…RG-08, PF-01…PF-07, AV-13; Gate 0 (A2-33, A2-39, A2-40, A2-59); A2 gate modules (A2-25, 26, 29, 31, 36, 37, 43, 50, 61, 70); A2 exit modules (A2-34, A2-48); exit/risk modules A2-72…A2-79; L2-01…L2-07 (parked, no data); Keller B5 (v2.3): KD-02 (canary), RG-09/KD-09 and RG-10 (modules), KD-06/KD-10 (robustness arms inside PS-25's row), KD-07 (gate), KD-08/KD-11 (studies). Merged IDs inherit their host's row. ¹ **[est] (v2.3, Keller B5):** PS-25's and MT-07's Nov, Freq, Impl/Data and Smooth scores — and hence their priorities — are **drafter estimates**, not statistics; only Ev comes from the source (PS-25 Ev 3 = C (+) family, setup untested; MT-07 Ev 1.5 = H). **ES evidence is prior 0** (FX/crypto only · no true delta). MT-07 follows the catalogue's convention of scoring MT modules as layers (type L). Weights unchanged. The A2-xx and DV-xx scores use the **catalogue** grade (Ev), not the original grade.

² v2.5 VX/LE rows: drafter estimates from R7 (Impl 1 = data-blocked on CFE per-contract history / index feed / borrow data); Ev follows the entry grade. *Not scored (v2.5):* VX-05 (portfolio sleeve, §6) and VX-07 (module).

³ v2.6 YV rows: drafter estimates; Ev 1.5 = grade E (presenter claims only). Order-flow rows (YV-01, YV-02, YV-04) take Impl 1 (W-T only, bid/ask from 2024-10-28, −1 for order flow); YV-03 Impl 1 = no stock data in the pipeline; YV-07 Impl 2 (1m arm on W-M, 15s arm W-T). *Not scored (v2.6):* YV-05 (gate) and YV-06 (risk module).

⁴ v2.7 rows: drafter estimates, not statistics. Ev follows the entry grade (E → 1.5, except SP-09 Ev 1 because the naked sweep-fade prior is 0). PF-08 is scored even though PF-01…PF-07 are infrastructure and unscored: PF-08 is an entry sleeve. Impl 4 = W-D with a local Donchian buffer; SP-09 Impl 4 = Arm M on W-M; OF-09 and OF-10 Impl 1 = W-T, and OF-10 is not buildable until a second footprint exists. *Not scored (v2.7):* EX-13 (module).

---

## 5. Proposed batches for the ten `_Test` slots (v2)

Every slot carries the `Mode` (Strategy / EventStudy / ControlRT / ControlFL / Canary), `Variant` and `ExitModule` inputs (§2.5). In v2 two more inputs are added: `GateModule` (the RG/A2 gate under ablation) and `RiskModule` (A2-72…A2-79).

**Run Gate 0 first (§2.6).** G0-a (A2-40 + A2-59 costs) goes into the base class before any slot reports a number. G0-b, G0-c and G0-d run in `_Test1`. Non-level ideas (marked "not gated by G0-d") may start while A2-33 runs. Level-based ideas start only once A2-33 has scored their level type. Trapped-trader entries (PS-03 confirmed arm, PS-09, SP-07, AV-12/A2-05, DV-16 arm d) stay held for their own round, which is deferred and not covered in this catalogue.

### 5.1 Batch 1

| Slot | Idea(s) | Why first | Window | Gate | Inside the slot |
|---|---|---|---|---|---|
| `_Test1` | **Gate-0 suite: A2-33** level study (+ **A2-39** roll-honest variants) · canaries **VW-08** (+ A2-24, DV-10), **A2-53**, **DV-01**, PS-03 naive 80% | Decides which level types exist for every later slot; proves the simulator is honest | W-M (dev years only for the A2-33 decision; confirmed on validation) | is G0 | `Mode=EventStudy` for A2-33; `Mode=Canary` for the canaries |
| `_Test2` | **TS-01** European-open drift (MES/ES) | Rank 1 (84); the only peer-reviewed intraday edge (JFQA; NY Fed); independent clock | W-M + ETH | G0-a/b | Control window at other hours |
| `_Test3` | **A2-49** midday S/R + afternoon breakout | Rank 3 (76); Kaufman ES 2000–2011, 8,404 trades, gross and untuned; our window is fully out of sample | W-M; NQ/YM/RTY as M10 | G0-a/b | Arms SR / BO / Both; 18-cell surface (M6) |
| `_Test4` | **PS-24** ON high/low break-and-go (+ **OF-07** burst layer) | Rank 2 (76); run-through behaviour (B) | W-M + ETH; OF-07 seconds arm in W-T | **G0-d (ONH/ONL)** | Verify ETH bars first (E5) |
| `_Test5` | **PS-07** open-drive first pullback (+ TS-07 blackout layer) | Rank 4 (74); B evidence for the behaviour | W-M | G0-a/b | E(b) sweep |
| `_Test6` | **A2-09** noise-band long-only (H3) **vs A2-63** open + 0.5 ATR long-only (H2), **A2-64** paired fade arm | Ranks 6–7 (72); A2-09 is the only VWAP role with independent net-positive OOS evidence; A2-63 is Kaufman's ES day system. A2 asks for the two to be raced on the same days. They also become the **hosts H2/H3** for A2-72…A2-79 | W-M; A2-63 2008 proxy on W-D (M20) | G0-a/b | Overlap report (> 70% → keep the better); `RiskModule` ablations only after each host passes |
| `_Test7` | **VW-05 (= A2-06)** late-day conditioned momentum (+ TS-03 MOC variant) | Rank 8 (71); peer-reviewed base effect (Baltussen), decaying; a unique clock | W-M | G0-a/b | ROD-only and ROD + EMA controls; VIX / NGE / 0DTE splits (restored) |
| `_Test8` | **SW-02** RSI(2) dip-buy **+ A2-69** Kaufman 3-day trade | Ranks 9 and 16; daily dip-buyers with the longest records (Krueger holdout; Kaufman 1990–2016 ES/NQ/RU); the swing sleeve | W-D (base) / W-M (filters, 1m fills) | G0-a/b | Overlap and daily-P&L correlation reported; GC/ZN drift check; no day-of-week selection |
| `_Test9` | **PS-01** ABB-HL = Spec 1 (variants PS-02 incl. **A2-41 BookSpec**, AV-11/DV-13) | Final-synthesis Spec 1 and **host H1** for every exit/risk module | W-M | **G0-d (composite VA edges)** | Exit arms EX-03/EX-05/EX-04 (= A2-03 primary); gates A2-25/A2-26/RG-01; A2-48 module; canary = naive 80% |
| `_Test10` | **PS-08** IB extension with acceptance (+ **A2-08** IB-break AVWAP; **DV-23** IB draw as a Variant) | Rank 10 (70); high frequency; the IB state is shared, so overlap is measured in one place | W-M | **G0-d (IB edges)** | DV-23 starts with the IB-touch-rate study (the 96.4% claim) |

**Changes vs v1 Batch 1:** out go PS-05/06 (+A2-13), MT-01, VW-01 and AV-02/AV-04, all moved to Batch 2. In come the Gate-0 suite, A2-49, A2-63 (with A2-09), A2-69 (with SW-02) and PS-08. PS-01 stays because it is host H1.

**Batch-1 readiness (as of merge, §2.4a):** every Batch-1 slot can now be coded on the merged base except where noted. `_Test1`: the canaries and A2-39 are buildable; the **A2-33 harness** is on main (`_LevelStudy`, PRs #15/#16; matched controls + Kaplan–Meier; Cox not implemented — v2.3b), A2-53's HV6/HV100 come from `_DailyContext.Hv(n)` (PR #19), and DV-01's news filter uses M-EVENT (PRs #25/#27/#28/#29). `_Test2` TS-01: buildable; the tier-1 event filter uses M-EVENT `EventBlackout` (v2.3b). `_Test3` A2-49, `_Test7` VW-05/TS-03, `_Test10` PS-08/A2-08/DV-23: buildable now. `_Test4` PS-24: buildable; the OF-07 burst arm uses M-TICK counts/timing (W-T). `_Test5` PS-07: buildable (M-TOD opening-range norm); the TS-07 blackout is time-window only. `_Test6` A2-09/A2-63/A2-64: buildable (M-TOD noise, M-DAILY Atr20d). `_Test8` SW-02/A2-69: buildable on M-DAILY. `_Test9` PS-01: buildable (PRICEACTION HL); the PS-02 entropy arm uses `_ProfileStats.Entropy` (PR #18, v2.3b). Before any multi-timeframe or cross-market variant is read, run **Probe P2** (`_MtfProbe`). G0-a (M-COST) is MC costs + the cost filter, with the slippage audit done offline from the journal.

### 5.2 Batch 2 (after Batch 1 has cleared Gate 3; A2-33 verdicts in hand)

| Slot | Idea(s) | Why | Gate |
|---|---|---|---|
| `_Test1` | **Kaufman timing contest:** A2-58 fade-fast-in-slow, A2-57 first OS, A2-65 1st cross, A2-66 2nd cross (Variants) | A2-58 is rank 15; one slot settles Kaufman's contradictory "first vs second pullback" claims on one trend definition | G0-a/b |
| `_Test2` | **PS-19** poor high/low run-through (+ A2-48 destinations module on the other hosts) | Rank 17; turns a null magnet result into a testable run-through | G0-d |
| `_Test3` | **A2-60** Meyers adaptive breakout (MNQ core, ES OOS) | Rank 18; a pre-registered replication at the published point with shell-decay check (M8) | G0-a/b |
| `_Test4` | **DV-15** EMA20 pullback gated by VWAP slope **vs VW-01** trend-day first VWAP pullback | DV-15 rank 19 (67); VW-01 scores 54. DV-15 is the only Dale idea with any numbers of its own (2 months of EUR/USD, in-sample); fake-VWAP controls | G0-a/b |
| `_Test5` | **MT-01** HTF-aligned HL at value | Rank 20; tests whether MTF alignment adds anything | G0-d |
| `_Test6` | **A2-51** shock-bar fade (exit arms E0–E3) | Rank 23; a fade with a published (gross) test on gold and bonds (Kaufman, 2000–2011); stop-delay and volume-decay exit ablations | G0-a/b |
| `_Test7` | **A2-56** Momentum Pinball (+ A2-55/RG-08 contraction gate) | Rank 24; Kaufman ES 2000–May 2011: 1,734 trades, +$703/trade gross, a wide cost margin | G0-a/b |
| `_Test8` | **A2-52** time-confirmed ORB (ACD) vs plain ORB | Rank 26; settles whether any ACD element beats plain ORB | G0-a/b |
| `_Test9` | **PS-05 + PS-06** gap tree (+ **A2-13** AVWAP-from-open branches, **A2-54** up-gap pullback short) | Keller's top cards and the gap family in one place; A2-53 (Batch 1 canary) is the prior | G0-d |
| `_Test10` | **AV-02 + AV-04 (= A2-01)** impulse-origin AVWAP HL and handoff (+ DV-12 touch arm as control) | Forces the M-AVWAP build; P1/P2/P3 placebos | G0-d |

**Module ablations (no slot of their own; they run inside the host slots once each host has passed its kill test):**
- **Exit/risk modules** A2-77 → A2-73 → A2-74 → A2-79 → A2-72/A2-75 → A2-76, in A2's order, on H1 (`_Test9`), H2/H3 (`_Test6`).
- **A2-78:** the test part runs on the combined book; the operational part is required before going live.
- **Gates** RG-08 (= A2-55), A2-50, A2-61, A2-70, A2-29/A2-31, A2-43 run on the survivors.

**Cheap side-runs:** A2-12 (VWAP−TWAP event study); A2-34 (V/T event study); A2-41 and A2-42 base-rate replications (descriptive); the A2-53 gap tables; A2-36 if GEX history can be sourced; the layer tests MT-03, VW-07, AV-08 (= A2-07, + DV-14), AV-10, RG-03 and DV-08 on pooled host events.

**Batch 3 candidates** (not slotted): A2-35 + AV-06 (+A2-20/21) + TS-05 event slot; A2-27/A2-46 void and gap traverse; A2-44 fast-LVN (in the PS-15 slot); DV-02 flip retest; OF-01/OF-02 in W-T; SW-01; A2-30, A2-32, A2-45.


### 5.3 KD batch (Keller B5 rev 3.1; after Batch 2)

Runs **after Batch 2**; the Batch-1/2 slot assignments are unchanged. `_Test1`/`_Test2` here mean the KD batch (reusing the Gate-0 host in `_Test1`). Slots set the protected `DvpConfig` (`enabled = true`; rev 3.1 constants, §2.4b), `PriceActionConfig.enabled` and `LevelStudyConfig.enabled`; **no new `[Input]`s**.

| Slot | Idea(s) | Why | Window | Gate | Inside the slot |
|---|---|---|---|---|---|
| `_Test1` | **KD-08** forward-cone diagnostic and **KD-11** node reach-rate study (`Mode = EventStudy`; KD-11 shares the A2-33 run) · **KD-02** canary with the **KD-07** gate (`Mode = Canary`, `GateModule = "KD-07"`) | Measures the engine on ES before any trade is read; KD-02 proves the detrended rails are causal | W-M (common KD line, §3.16) | is G0 (G0-b canary; G0-d DVP kinds) | KD-02 kill: net PF > 1.0 (no 1.0–1.1 zone) → bug first |
| `_Test2` | **KD-01 = PS-25** (frozen 18 cells **+ the 6-cell σ2/node robustness block**: KD-06, KD-10) plus the **KD-09 gate** (`GateModule = "KD-09"`; the only confluence module run — KD-03 arms A/C and the confTol sweep are replaced; arm B is KD-09's contrast) | The Keller B5 primary; the only KD row in the Holm family | W-M | G0-a/b/c; **G0-d (DVP event-rail types)** | PS-25's primary cell (PROPOSED: N = 2, VAH/VAL, EX-05 trail + 41-bar) confirmed by Ettienne before any dev read; robustness cells never primary; Holm {PS-01, PS-02, DV-02, A2-31, PS-25}, m = 5, α = 0.05; A2-31 tie-break on dev only |

**Pre-requisites (v2.4: engine coded and unit-tested, PRs #32–#58; A2-33 DVP kinds done, #42; still open: PR-D9 d* freeze, PR-D10, Probe P2):** M-DVP coded and unit-tested (truncation and shuffled-future tests), the A2-33 harness with DVP level kinds, and Probe P2 for any 60m/240m stream. RG-10 (KD-04) and MT-07 (KD-05) run as ablations inside `_Test2` only after PS-25 has passed its kill test.
---

## 6. Combining survivors into a low-drawdown portfolio, and judging "beats the S&P by a few multiples" honestly

### 6.1 Construction

1. **Core + satellites.** PF-01 (trend + carry, grade A, 12% volatility target) is the core: 70–85% of risk per R6. Intraday and swing survivors are satellites. A smooth curve comes mostly from *low correlation between sleeves*, not from any single high-win-rate setup.
2. **Diversify by mechanism, clock and market.** One continuation sleeve (e.g. PS-01/PS-07), one responsive sleeve (only if one survives; most fades are expected to die), one time sleeve (TS-01, VW-05), one swing sleeve (SW-02/SW-01). Then replicate across CL/GC/ZN. **ES and NQ count as one market.** Accept a new sleeve only if its daily-R correlation with the existing book is < 0.3 in validation (PF-02).
3. **Size by risk, not conviction.** Quarter-Kelly on the *deflated* win rate, capped at 0.5% per trade; ≤ 1.5% open risk; ≤ 2 cards live (PF-04). Inverse-volatility weights across sleeves; portfolio volatility targeting (PF-06).
4. **Prefer lower b at equal expectancy.** Equal-expectancy systems with smaller targets have about half the drawdown (R2 §7.4). Use EX-08 to see whether an edge is fast-decaying (favour b ≤ 1 and scale-outs) or persistent (favour trails).
5. **Throttles and monitoring.** Daily stop after 2 losses; −3 R in a week → half size; −6 R → observation only; two CUSUMs; bench a spec at 0 R after 40 live observations (PF-05).
6. **Hidden-beta check.** A sleeve whose daily P&L correlates > 0.5 with SPX is mostly index exposure (PF-03). On ES/NQ, every long-only result must be repeated on CL/GC/ZN or as shorts (K-DR).
7. **Size every sleeve by ATR, then target 12% volatility on the combined book (PF-06, now including A2-71).** Contracts = floor(B × equity / (ATR20d × $/pt)) with B ∈ {0.25, 0.5}% per ATR. Skip, never round up. The factor is 12% / trailing-20-session realised volatility, clipped to [0.25, 2.0], with 20% hysteresis, using past P&L only. A2-40's ρ = 0.9 rule still caps same-direction ES/NQ/RTY exposure.
8. **Book-level risk modules (§3.13).** A2-78 is the daily loss kill-switch and broker-side flatten; its operational part is mandatory before going live. A2-72 (exposure ladder) and A2-75 (hit-rate throttle) are adopted only if their clustering and predictive checks pass. Otherwise they reduce to "trade smaller", which PF-06 already does more cheaply.
9. **Plan on half the backtest IR (M18), and scale capital on evidence only (M30).** A newly promoted sleeve starts at 25% of its target risk. It moves to 50% after ≥ 50 live trades inside the backtest's 80% band, and to 100% after ≥ 100. Plan capital for the larger of 3 × the largest neighbouring MaxDD (M12) and 2 × the worst tested drawdown including 2008/2020 (M20).

### 6.2 What "beats the S&P by a few multiples" can honestly mean

- **Compare risk-adjusted, not raw.** Any strategy can show "3× the S&P return" by using more leverage. The honest comparisons over the **same dates**, on daily marked-to-market equity:
  - **(a)** Sharpe and Sortino vs SPX total return;
  - **(b)** return at **matched volatility**: scale the strategy, or a levered MES position, to the same annualised volatility;
  - **(c)** MAR/Calmar (CAGR / max drawdown) and the longest underwater period;
  - **(d)** correlation to SPX and the information ratio vs SPX.

  "A few multiples" is credible only as **a multiple of SPX's Sharpe/MAR at matched risk.** Compute SPX's own Sharpe on your test window rather than assuming one: Telonics' reproduction shows a QQQ buy-and-hold Sharpe of 0.70 over 2018–2023, which illustrates how window-dependent it is.
- **Deflate before you compare.** With N configurations tried, the expected maximum Sharpe *by chance* over 500 days is 1.12 at N = 10, 1.62 at N = 50 and 1.96 at N = 200 (final-synthesis). Pass only if the observed Sharpe > 1.5× that. Also require DSR > 0.95, PBO (CSCV) < 0.20, BH-FDR at 0.10, Harvey–Liu–Zhu t > 3, and White's Reality Check / Hansen SPA across the whole batch, **counting every variant in this catalogue that you ran**.
- **Holdout once.** The holdout window (W-M and W-D: Jan 2025–Oct 2026; W-T: the last 125 sessions ≈ Apr–Oct 2026; §2.0.1) is run **once**, after all choices are frozen. A second look turns it into validation.
- **Realistic expectation.**
  - **Sources:** a diversified trend + carry core nets a Sharpe of about **0.3–0.7** (R6). Surviving intraday setups net about **+0.05 to +0.20 R** per trade (final-synthesis). The best-documented intraday momentum benchmark, long only, nets an OOS Sharpe of about **0.68** (EdgeLab).
  - **Implication (inference):** a combined portfolio Sharpe of 1.0–1.5 at modest drawdown would already be an excellent, multiple-of-SPX *risk-adjusted* result. A raw return multiple far above that would come from leverage, and leverage multiplies drawdown in proportion.
  - **Treat anything above Sharpe ~2 net in a backtest as a bug until proven otherwise** (the VW-08 canary exists for exactly this).
  - **Discount for live decay (M18):** plan on live IR ≈ ½ of the backtest's average-of-all-tests IR (Kaufman). An idea whose halved IR is below 0.3 is not worth the operational risk.
- **Capacity and costs.** Micros keep costs per R high: hence the cost floors. Re-test every survivor at 2× costs, and measure live slippage as the IB fill vs the dxFeed signal price.

---

## 7. Validation gates: short reminder (final-synthesis §6, with Rev 2.7 data windows)

| # | Gate | Key thresholds |
|---|---|---|
| 0 | **Gate 0 (v2, §2.6)** | A2-40 + A2-59 costs in the base; canaries (VW-08/A2-24/DV-10, A2-53, DV-01, naive 80%) ≈ 0; A2-39 roll-honest composites; **A2-33 level verdicts before any level idea** |
| 1 | Pre-register | ≤ 30 configs per spec, ≤ 100 in total; ≤ 2 thresholds per layer × ≤ 3 values. **W-T ideas: ≤ 4 configs** |
| 2 | No lookahead | Developing POC/VA only; pivots at confirmation; closed HTF bars only (M-MTF: native BarsOfData(n) + `HtfClosed`, no internal HTF bar building; Probe P2 pending); truncation test; canaries (S7 sweep fade, H4 failed-break-to-POC, VW-08/A2-24/DV-10, A2-53, DV-01) ≈ 0 edge. **Multi-day levels and anchors reset at dxFeed roll splices** (unadjusted continuous) |
| 3 | Event study vs controls | K-RT, K-FL, K-ST, K-SH, K-DR, K-LM, K-RW 1/(1+b); A2 placebos P1/P2/P3 for VWAP/AVWAP |
| 4 | Honest simulation | Stops/markets +1 tick (2 at the open and around news); limits fill on trade-through; stop-first; 1% tail of 4 R losses; 2× cost stress. Bar Magnifier: 1-tick inside W-T, 1-minute before it; **label the run** |
| 5 | Ablation ladder L0–L5 | + E(b) sweep b ∈ {0.5, 0.75, 1, 1.5, 2, trail} |
| 6 | Splits | **W-M:** dev Jul 2016–2022, validation 2023–24, holdout Jan 2025–Oct 2026 (once); anchored walk-forward by year inside dev; CPCV 10 blocks / 2 test, embargo, clustered by day. **W-T:** 250 / 125 / 125 sessions (in-sample / validation / holdout ≈ last 6 months; A2's split adopted). **W-D:** dev 2000–2015, validation 2016–2024, holdout Jan 2025–Oct 2026; intraday-dependent rules stay in W-M. Reconciliation with A2's split: §2.0.1. A2 ideas also follow M9 (single-use holdout; random IS/OOS blocks only inside dev + validation) and M20/M21 |
| 7 | Multiple testing | E[max SR] (T = 500 days): 1.12 / 1.62 / 1.96 at N = 10 / 50 / 200; observed > 1.5×; DSR > 0.95; PBO < 0.20; BH-FDR 0.10; HLZ t > 3; Reality Check / SPA |
| 8 | Pass criteria | (A2 ideas: also A2's generic kill and M19 trade-count table) ≥ 30 events to evaluate; ≥ 150 per ablation cell; ≥ 300 OOS trades; net ≥ +0.10 R with t > 2 (target ≥ +0.15 R, PF ≥ 1.3); beats controls; positive in ≥ 70% of OOS half-years; parameter plateau; positive without the 10 best trades; same sign on ES and NQ; holdout > 0. **W-T standalone ideas will rarely reach 300 OOS trades: label them "exploratory" or use them only as layers until the tick archive grows** |
| 9 | Paper → live | 2–3 months paper; quarter-Kelly on a deflated win rate, ≤ 0.5%; CUSUMs; daily/weekly throttles; bench at 0 R after 40 live observations. **v2:** A2-78 operational part (broker-side stops, heartbeat flatten) before the first live trade; M27 contingency plan written; M30 staged capital (25/50/100%); plan on live IR ≈ ½ backtest (M18) |

---

## 8. Build order for missing base features (what unlocks the most ideas)

| Order | Feature | Unlocks | Notes |
|---|---|---|---|
| 0 | **◐ by design (MC costs + cost filter; offline G0-a)** **M-COST** (A2-40 + A2-59: style slippage scaled by ATR, unables haircut, report-window strata, shock audit, p90 stop slippage; IB-fill log) | Gate 0 for every idea | Must exist before any result is read |
| 0b | **◐ done except Cox (PRs #15/#16 `_LevelStudy`; Gate-0 matched-control verdict PR #20, 5ca25a6)** **A2-33 level-study harness** (matched control prices, Kaplan–Meier level death, Holm) on top of F-EVT | Gate 0 for every level idea | Extends CS-8 `logSignal` |
| 1 | **M-SWING** (ATR ZigZag + fractal pivots with confirmation timestamps, HH/HL state, BOS, multi-TF via native BarsOfData(n)) — **✓ done (PR #6, PRICEACTION)** | PS-01, PS-07, MT-*, SP-*, AV-* (anchors), most HL entries | Needed by more than half the catalogue |
| 2 | **✓ done (PR #7; placebo twins added in PR #19, 72b4013)** **M-AVWAP** anchor manager (N dynamic anchors on events; σ-bands; slope; age; roll-reset; per-anchor journal tag; P1 unweighted and P2 volume-shuffled twins built in) | All AV, A2 AVWAP ideas, EX-04, VW-07, SW-03, DV-09/11/12/13/14 | Today's VWAP engine is limited to 3 static slots |
| 3 | **✓ done (PR #9)** **M-TOD** (volume, delta, range and noise by clock minute, 14/20 sessions) = A2's same-slot baseline (SSB, M16) | PS-07, PS-14, OF-02, OF-07, RG-05, A2-09, A2-30, A2-49…A2-62 |
| 3b | **✓ done (PR #18 5122247/12b40b1: bootstrap POC, KDE sweep, entropy, KL; PR #21 e911e07 + PR #24 4c90e00: `_Gmm`, G-P mixtures)** **M-GMM** profile-statistics helper (block-bootstrap POC, KDE bandwidth sweep, Miller–Madow entropy, KL, volume-weighted GMM/BIC) | A2-25…A2-32, A2-41 quality, A2-43 | Prior-session statistics only (no lookahead) |
| 3c | **✓ done (PR #18: `_RangeProfile`, `_FvgDetector`; also weekly profiles, per-bar ladder)** **Range-profile helper** (profile over an arbitrary closed-bar range [t0, t1]) + **FVG detector** | DV-02…DV-08, A2-15, A2-35, PS-12/13/14 merges | Small | Small |
| 3d | **◐ engine coded (v2.4: PR-D0…D8, [#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#33](https://github.com/ettiennehugo/MultiCharts.Net/pull/33), [#34](https://github.com/ettiennehugo/MultiCharts.Net/pull/34), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#37](https://github.com/ettiennehugo/MultiCharts.Net/pull/37), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58); PR-D9/D10 open)** **M-DVP** detrended volume-profile engine (§2.4b: `_DvpEngine`, `DvpConfig` with rev 3.1 constants, `ELevelKind` DVP kinds, Fib lattice, prominence adapter) | PS-25, MT-07, RG-09, RG-10, KD-02, KD-06…KD-11 (KD batch §5.3) | After the VP helpers; before the KD batch. Needs ✓ M-MTF (Probe P2), ✓ M-SWING, F-EVT; A2-33 harness for G0-d |
| 4 | **✓ done (PR #10)** **M-DAILY** with roll-gap removal | MT-01, RG-03, RG-06, SW-*, PF-01, PS-13 | dxFeed continuous is unadjusted |
| 5 | Roll-splice table + level reset (VPF-4 per Rev 2.7) — *v2.3b: `_RollDetector` (PR #19) and `tools/m-event/roll_splices.py` (PR #25, acf2f40) exist on main. v2.4: `RollDetection.Series` one splice per contract ([#51](https://github.com/ettiennehugo/MultiCharts.Net/pull/51)), 30 rolls pinned in `RollTests` ([#55](https://github.com/ettiennehugo/MultiCharts.Net/pull/55)), `_Test6` on the table ([#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)), opt-in Week/Month VWAP roll reset ([#52](https://github.com/ettiennehugo/MultiCharts.Net/pull/52), [#54](https://github.com/ettiennehugo/MultiCharts.Net/pull/54)); CL/GC/ZN seeds still open → partial* | Everything multi-day | Seed it from CME equity roll dates; ask MC support for CL/GC/ZN (E7) |
| 6 | **Tick archive** (save dxFeed ticks + Bid/Ask locally from now on) | Every OF idea; extends W-T beyond 2 years | Rev 2.7 §F: the 2-year window may roll forward |
| 7 | **◐ scheduled events done (M-EVENT `_EventCalendar`, PRs #25/#27/#28/#29); unscheduled news and `NewsBar` anchor open** M-NEWS (port `_EconomicCalendar` CSV to a base service) | AV-06, TS-05, RG-07, PS-04/05, A2-35, DV blackout filters | |
| 7b | **✓ done (PR #11)** M-XMKT second stream (NQ/YM/RTY for ES; ZN) | A2-37, M10 cross-market OOS, A2-71 | MC multi-data |
| 8 | **✓ done (PR #8)** M-MTF / VPB-8 MTF safety: native BarsOfData(n), no internal HTF bar building | Lets MT ideas use Data2 | Probe P2 still to run in MC |
| 9 | **◐ coded (PRs #14/#17/#22: `_PortMM`, `_PortfolioRiskManager`, P5b test signals); Probe P5b live checks pending** M-PORT | PF-*, A2-71, A2-72/75/78 (book level) | Can live outside MC |
| 10 | **✗ deferred** M-GEX (external daily GEX file via QuoteManager) | A2-36 | Data-blocked until a vendor history is sourced |
| 11 | Local DOM recorder (10 levels, real time) | L2-01…L2-07 | Months of recording before any test |
| 12 | **✗ v2.5** Cboe index feed check (`$VIX`, `$VIX3M`, `$VVIX` in dxFeed/MC, history depth) or a daily CSV via M-XMKT | VX-06, VX-08 (and inputs to M-VXTS) | Cheapest VX unlock; `specs/live-check-sheet.md` Check 3 |
| 13 | **✗ v2.5** M-VXTS (CFE per-contract VX/VXM history, term-structure engine, SOQ calendar via `VixExpiry`) | VX-01…VX-05, VX-07 | Verify CFE bundle and VXM symbology first; stress sizing per R7 §1.8 |
| 14 | **✗ v2.5** M-ETF (ETF profile: splits, dividends, borrow model) + LETF AUM file | ETF arms of LE-01…LE-07; LE-04 flow arm | MES/MNQ arms of LE-01/02/03/06 need none of this |

---

## 9. Methodology M1…M30 (Analyst 2's testing protocol, from Kaufman and Minervini)

Analyst 2's methodology section, verbatim (Rev 6). It applies to **every** idea in this catalogue on top of §2–§7.
- **Window references:** read "window M/T/D" as W-M/W-T/W-D with the agreed split (§2.0.1).
- **M9 (holdout):** the single holdout is Jan 2025–Oct 2026 (W-M/W-D) and the last 125 sessions (W-T).
- **M21 (recent vs old):** compares Jun 2016–Aug 2021 with Aug 2021–Oct 2026.
- **Where the rules live in the catalogue:**
  - M5 and M12 are built into Gate 0 (A2-40/59).
  - M17 and M18 are in §6.
  - M22–M30 are paper/live process rules (§6, §7 gate 9).

These apply to **every** A2 idea, alongside §1's generic protocol. They are mandatory for A2-49 to A2-71 and recommended retroactively for A2-01 to A2-48. M17–M21 were added in Rev 5 from *Kaufman Constructs Trading Systems*; M22–M30 (journal, expectancy and process controls) were added in Rev 6 from Minervini, *Think & Trade Like a Champion*.

- **M1. Write the expectations first.** Before any run, record the expected trade count, % winners, avg win/loss and $/contract in the idea's test log. Testing is for validation, not discovery. A result far from the expectations, *good or bad*, triggers a code and logic audit before it is interpreted.
- **M2. Fix the parameter range in advance, inside the concept.** Ranges are the ones listed in each idea. Extending a range after seeing results counts as a new idea and a new test, toward the multiple-testing total (M11).
- **M3. Space parameters geometrically.** Use about ×1.5 steps for periods (e.g. 2, 3, 5, 7, 11, 17, 26, 38, 58), or dense-short and sparse-long spacing. Equal steps over-weight the long end in any "average of all tests". Never average across coded (rule-switch) parameters.
- **M4. Test in order of importance.** Primary signal first, then timing, then risk. A stop or target must not be what makes the system profitable. If the entry is negative with neutral exits (a time exit), stop there.
- **M5. Model costs by style, and haircut "unables".** Apply A2-59:
  - full slippage on stop and market entries, about 50% for passive mean-reversion entries;
  - slippage scaled up with volatility and floored at 1 tick;
  - the unables haircut taken from winners only;
  - report-window stratification.
  - Always report **net $/contract**, not just R. Show the cost-sweep curve ($0, base, 2× base). An edge that survives only a narrow band of costs is fragile.
- **M6. Judge the whole surface, not the peak.**
  - Report % of profitable combinations (target ≥ 70% over the pre-registered range).
  - Report mean and SD of all tests, and mean − 1 SD as the conservative expectation.
  - **Use the average of all tests as the forecast of live performance**, not the selected point.
- **M7. Smooth before you select.** Choose parameters at the maximum of the neighbour-averaged surface:
  - 1-D: 3-point mean;
  - 2-D: 9-cell mean (6 on edges, 4 at corners).
  - Pick from a broad plateau. A spike surrounded by losses is rejected.
- **M8. n-space sensitivity shells.**
  - Compute each test's Euclidean distance from the chosen core in normalised parameter units.
  - Bin into about 10 shells and plot the average net expectancy per shell.
  - Shell 1 must retain ≥ 70% of the core. A steep decay means a peak solution, so kill it or widen it.
- **M9. Out-of-sample discipline.**
  - (a) In addition to §1's chronological split, use **alternating random blocks fixed once per idea** (e.g. 4 IS / 4 OOS segments, each 6–18 months, drawn by a seeded uniform draw at registration). IS performance is reported as stitched daily P&L, never cumulative equity.
  - (b) The OOS and holdout are **single-use**. Any change after viewing them creates a new idea ID.
  - (c) Walk-forward, if used: IS ≥ 3 years of 1m data for intraday ideas (Kaufman's fix for the fast-model bias), step 6 months. Flag it if the selected parameters flip between range ends in successive windows.
  - (d) The ~500-session tick window (T) is **too short for optimisation**. Use it only to re-check fills of rules fixed on window M.
  - (e) A ~50% IS→OOS decay is normal. Kill below 30% retention.
- **M10. Cross-market out-of-sample.** An ES idea must be net-positive (or at least non-negative, with the same sign of edge) on NQ, and should be checked on YM/RTY, with identical parameters. ES-only success is treated as overfitting. CL/GC remain robustness markets per the file scope.
- **M11. Count every test.** Keep a running count of all parameter combinations × ideas tested (A2-01 onward). Apply Deflated Sharpe / Holm (per §1) to that cumulative total, not per idea. One success in 100 is not significant.
- **M12. Shock audit and risk realism.**
  - Run the A2-59 shock audit, and flip the shock P&L if the win share is > 50%.
  - Report the **largest drawdown among neighbouring parameter sets**, not the selected set's own.
  - Check that profits are spread evenly over time: the slope of quarterly net P&L should be ≈ 0, and no year should carry > 50% (§1 already requires the latter).
  - Prefer the variant with **less time in market** when expectancy is comparable.
  - Capital = 3 × the largest neighbouring MaxDD as a floor.
- **M13. Rule changes must improve globally.** A new rule or filter is accepted only if it raises the **average across all parameter combinations** (with the SD unchanged or lower) or lowers the SD at the same average. A rule that lifts the peak but raises kurtosis across tests is a fit to a case.
- **M14. Keep rules minimal (≤ 4 free parameters).** Futures Truth's observation: systems with ≤ 4 variables dominate. Ideas with more free parameters must justify each with a pre-stated premise.
- **M15. Live monitoring against the tested profile.** Trade exactly the tested code on the same dxFeed data. Log system price vs IBKR fill per trade, and refit the A2-59 slippage regression at 200 fills. Halt review triggers:
  - live drawdown reaches the tested 95th-percentile drawdown (Wolberg's formula, or a bootstrap of the tested daily P&L);
  - live losses run steadily from day 1 to the prior MaxDD.
- **M16. Same-slot normalisation for any volume or volatility condition.** Intraday volume and volatility follow a U-shape (Kaufman Ch 12/16/20). Every volume or volatility threshold in an intraday rule must be relative to the same-time-of-day baseline (SSB), not to a rolling bar average. Otherwise the rule fires by clock time, not by condition.

- **M17. Trade at least three parameter sets, equally weighted (KCTS Ch 4/12/14/15).** Do not deploy the single best set. Deploy ≥ 3 sets spread across the pre-registered range (e.g. ×2 spacing: 30/60/120, or the low/core/high of each idea's range), equal risk each, and judge the idea on the **combined** equally weighted P&L. If you want several sets to differ, at least half of the parameters must differ between them. The combined stream is what goes into M18–M21 and into A2-71.
- **M18. Expect live IR ≈ half the backtest (KCTS Ch 14).** A theoretical IR of 1.5 typically becomes about 0.75 live. Size and set go/no-go thresholds on **0.5 × backtest net IR** (and use the average of all tests per M6, not the best). An idea whose halved IR is below 0.3 (Kaufman's stock-market benchmark) is not worth the operational risk, even if it passes everything else. Backtest IR > 3.0 is treated as overfit or a cost error until proven otherwise.
- **M19. Trade-count significance table (KCTS Ch 12/20, Seibert Monte Carlo).** Sample error ≈ 1/√N (about 14% at 50 trades, 5% at 400). Before going live, look up the chance of a net loss after N trades at the expected IR, and use it both for "do we have enough trades" and for live "is it broken" judgement:

  | N trades | IR 0.5 | 0.7 | 1.0 | 2.0 | 3.0 |
  |---|---|---|---|---|---|
  | 10 | 48% | 47% | 45% | 41% | 34% |
  | 20 | 47% | 44% | 42% | 37% | 32% |
  | 50 | 44% | 40% | 35% | 28% | 18% |
  | 100 | 38% | 38% | 32% | 22% | 13% |
  | 300 | 36% | 31% | 23% | 8% | 3% |
  | 500 | 30% | 25% | 18% | 4% | 0% |
  | 1,000 | 24% | 18% | 11% | 1% | 0% |

  Use the M18-halved IR. A live loss is "statistical" while its probability in this table is > 10%. A first-week result far outside the backtest's daily/weekly distribution points to a **systematic** error (code, cost or data) — halt and audit, per M15.
- **M20. Include 2008 and 2020 (KCTS Ch 20/29).** No test may exclude 2008 or 2020. [v2 note: under the agreed split, W-M starts ≈ Jun 2016, so the 2008 requirement is met via W-D] The 1m window M starts Oct 2017, so it contains 2018 Q4, 2020 and 2022 but **not 2008**. Every idea that can be approximated on daily bars must also be run on window D through 2008–2009 (e.g. A2-63/64 with daily OHLC as an "open ± k·ATR touched?" proxy; A2-65/66/69 natively). Report the worst drawdown across these crises and plan capital for **2 × the worst tested drawdown** (Kaufman: assume the future is worse), in addition to M12's 3 × neighbouring MaxDD floor; use the larger.
- **M21. Recent-vs-old t-test (KCTS Ch 14/20).** Split each idea's daily (or per-trade) net returns into an older and a newer block of equal length (e.g. on M: Oct 2017 – Apr 2022 vs Apr 2022 – Oct 2026). Run a two-tailed t-test (Kaufman uses Excel paired type 1 on aligned series; for non-aligned trade samples use Welch's unequal-variance test). p < 0.05 with the recent mean lower means the edge has shifted: flag decay, add the quarterly-return regression slope (M12), and do not promote on the old block's evidence. Run it again on live returns vs the backtest once live N ≥ 100.
- **M22. One trade journal per strategy, every trade (Minervini Sec 4).** Backtest, paper and live trades go into the same schema, keyed by idea ID, host, module set and parameter set: signal time, entry/exit fill and system price (M15), R at entry, MFE/MAE in R, hold time, exit reason, costs, slippage, and the module states (A2-72 level, A2-75 ON/OFF, A2-78 trigger). Never pool styles (H1 vs H2 vs H3; day vs swing) in one statistic.
- **M23. Track the expectancy triangle on a rolling basis.** Per strategy, per month and rolling 20/50 trades: hit rate (BA), average win and loss (in R and $), W/L ratio, BA-adjusted W/L, expectancy E = BA × AvgWin − (1 − BA) × AvgLoss, largest win/loss, and average hold of winners vs losers. Flags: largest loss > largest win, or losers held longer than winners over a rolling 6 months ("stubborn trader") → audit code and exit logic. Compare each month to the backtest's distribution (M19 table).
- **M24. Result-based assumptions, not targets.** Set stops, targets and the A2-73/75 thresholds from the strategy's **realised** rolling statistics (conventions above), never from hoped-for targets. A stop larger than the realised average win (in R) is a design error. Changes are made offline, applied to the next trade only, and each change is logged as a new parameter set (M2/M11).
- **M25. Outcome histogram and "The Wall".** Plot each strategy's per-trade R distribution monthly. Almost no mass should lie left of −1R plus slippage; any trade worse than −1.5R is a stop-execution incident and is investigated (gap through stop, platform outage, missing broker-side stop). Right skew is the expected shape for H1–H3.
- **M26. Compounded vs non-compounded and sequence-risk reporting.** Report every promoted strategy's equity curve both at fixed size and compounded, and show the loss-recovery table (10% → 11.1%, 25% → 33%, 50% → 100%). For sizing modules (A2-71/72/75) also report a 1,000-path reshuffle of trade order (worst 5% DD).
- **M27. Pre-registered contingency plan before going live.** Written per strategy: exit if wrong (the stop), re-entry rule after a stop-out (allowed only if the host's entry re-forms under its own rules — never a discretionary second try), when to sell into strength (A2-74/76 if adopted), disaster plan (A2-78 operational part, backup connection). No intraday "audibles": rule changes only after the session, logged per M24.
- **M28. No rule exceptions, ever.** A live trade that deviates from the tested code (manual override, skipped stop, added to a loser, revenge trade) is logged as a rule breach in the journal, excluded from the strategy's statistics, and counted separately. Two breaches in a month → that strategy goes back to paper for 20 sessions.
- **M29. Daily and periodic review loop.** Before each session: review the prior day's trades against the journal (what went to plan, lessons, one improvement) and check the M15/M19 status. Monthly: M23 dashboard and M21 drift test once N ≥ 100. Quarterly: full post-trade analysis including the losing periods. Analysis is of process adherence first, P&L second.
- **M30. Scale capital only on evidence (Minervini's progressive exposure, at the strategy level).** A newly promoted strategy starts at 25% of its target risk, moves to 50% after ≥ 50 live trades with expectancy inside the backtest's 80% band, and to 100% after ≥ 100; it steps down one level if live results fall outside the M19 > 10% "statistical" band or M15 triggers. This is separate from the per-trade A2-72 ladder, which must earn its place by test.

---

---

## 10. References (beyond the internal reports and Kindle notes)

- Telonics Research, "Does VWAP actually work? Three beliefs, 21,541 touches" (Oct 2026). https://telonicstrading.com/research-vwap-tested.html
- Telonics Research, "VWAP trend trading — does the 'holy grail' paper hold up?" (Sep 2026). https://telonicstrading.com/research-vwap.html
- Zarattini, C. & Aziz, A. (2023), "VWAP: The Holy Grail for Day Trading Systems", SSRN 4631351.
- Zarattini, C., Barbon, A. & Aziz, A., "Beat the Market: An Effective Intraday Momentum Strategy" (Concretum). https://concretumgroup.com/wp-content/uploads/2026/02/Beat-the-Market.pdf (via A2)
- Chuk (2026), SSRN 7609644 (pre-registered NQ replication; via A2, not re-verified).
- EdgeLab, "Zarattini's intraday momentum on the Nasdaq: the long side survives" (Jul 2026). https://edgelabtrading.com/blog/zarattini-intraday-momentum-nasdaq/
- RipBacktest, "VWAP Strategy Backtest: ES Futures, 5-Minute Bars, 2020 to 2026" (Oct 2026). https://ripbacktest.com/posts/vwap-strategy-es-futures/
- Shannon, B., *Maximum Trading Gains with Anchored VWAP* (2023); CMT Association deck https://cmtassociation.org/wp-content/uploads/2024/01/Shannon-Specific-Anchored-VWAP-Strategies-1.pdf; S&C V41:07 interview; The Chart Report, 14 Mar 2023 (pinch) https://www.thechartreport.com/2023-03-14/chart-day-tuesday-march-14th-2023
- Coles, A. & Hawkins, D., *MIDAS Technical Analysis* (Bloomberg, 2011) (via A2).
- Baltussen, Da, Lammers & Martens (2021), "Hedging demand and market intraday momentum", *JFE*. https://doi.org/10.1016/j.jfineco.2021.04.029
- Gao, Han, Li & Zhou (2018), "Market intraday momentum", *JFE*.
- Bondarenko & Muravyev (2023), *JFQA*. https://doi.org/10.1017/s0022109022000783; Boyarchenko, Larsen & Whelan, NY Fed Staff Report 917.
- Koijen, Moskowitz, Pedersen & Vrugt (2018), "Carry", *JFE*; Moskowitz, Ooi & Pedersen (2012), "Time series momentum", *JFE*; Hurst, Ooi & Pedersen, "A century of evidence on trend-following investing".
- Moreira & Muir (2017), "Volatility-managed portfolios", *JF* (out-of-sample critiques exist).
- López de Prado, *Advances in Financial Machine Learning* (meta-labelling, CPCV, PBO, DSR); Bailey & López de Prado (2014), "The Deflated Sharpe Ratio"; Harvey, Liu & Zhu (2016), "…and the Cross-Section of Expected Returns".
- MultiCharts wiki "DxFeed" and forum threads cited in `reviews/analyst-base-class-spec.md` Rev 2.7 (data constraints).
- Kaufman, P. J., *Trading Systems and Methods*, 5th ed. (Wiley, 2013), via A2 (`kindle-notes/kaufman-tsam-analyst2.md`); Kaufman, P. J., *Kaufman Constructs Trading Systems* (KCTS), via A2 (`kindle-notes/kaufman-constructs-analyst2.md`).
- Minervini, M., *Think & Trade Like a Champion*, 2nd ed., via A2 (`kindle-notes/minervini-champion-analyst2.md`).
- Keller, *Advanced Volume Profile & Order Flow* (Book 2) and *Empirical Volume Profile & Order Flow: Behaviour Evidence File* (Book 4), via A2 Kindle notes (`kindle-notes/keller-b2-parts-X-XI-XIII-XIV-analyst2.md`, `kindle-notes/keller-b4-parts-2-7-analyst2.md`).
- Trader Dale YouTube channel, https://www.youtube.com/channel/UC1_SoqTCXaM8kk0SxkAyhcQ (per-video URLs in §3.14 entries; transcripts in `videos/dale/`).
- **v2.5 (R7, volatility and leveraged ETFs; full list in `volatility-leveraged-etf-research.md` §6):** Cboe VIX methodology and Mini VIX fact sheet; Simon & Campasano (2014) *J. Derivatives* 21(3); Johnson (2017) *JFQA* 52(6); Eraker & Wu (2017) *JFE* 125(1); Whaley (2013) *JPM* 40(1); Carr & Wu (2009) *RFS* 22(3); Park (2015) *J. Financial Markets* 26; Israelov (2019) *J. Alternative Investments* 21(3); Griffin & Shams (2018) *RFS* 31(4); Cooper (2013) SSRN 2255327; Cederburg, O'Doherty, Wang & Yan (2020) *JFE* 138(1); Avellaneda & Zhang (2010) *SIAM J. Financial Math.*; Cheng & Madhavan (2009) SSRN 1393995; Shum et al. (2016) *Rev. Finance* 20(6); Gayed & Bilello SSRN 2741701; Jiang & Peterburgsky (2017) *Applied Economics* 49(44); Connors & Alvarez (2009). Practitioner: HoldoutLabs, Tickfloor, sma200.trade, Kipnis (2017).
- Internal: `final-synthesis.md`; R1–R6 research reports; `trading-ideas-analyst2.md` (Rev 6), `analyst2-handoff-ids.md`, `catalogue-merge-audit-analyst2.md`; `trading-ideas-dale-video.md`; `reviews/analyst-base-class-spec.md` (Rev 2.7), `reviews/base-class-rev2-status.md`; Kindle notes (Keller B1–B4, Dale VP and Order Flow, Levy).

---

## Appendix A. Full ID mapping (every source ID → its entry)

Disposition is either **Own entry** (with its section) or **Merged into X**, where X is an own entry that carries a **Merged · ID** note. The original grade is the source's own grade, verbatim; the catalogue grade uses the §1 scale (rescaling rules: §3.12).

### A.1 Catalogue v1 IDs (114)

| ID | Name | Disposition (v2) | Catalogue grade | Original grade |
|---|---|---|---|---|
| PS-01 | Accepted balance break → first-pullback higher low (ABB-HL) | Own entry (§3.1) | C (+) | (catalogue's own) |
| PS-02 | Balance break at acceptance close (K4 end-to-end spec) | Own entry (§3.1) | C (+) | (catalogue's own) |
| PS-03 | Confirmed failed break of value → rotation to the opposite edge | Own entry (§3.1) | B (−) for the naive form. | (catalogue's own) |
| PS-04 | Prior-value magnet on a small newsless gap (Keller IX-1) | Own entry (§3.1) | D | (catalogue's own) |
| PS-05 | Rejected repricing open (Keller IX-3 · Spec 3) | Own entry (§3.1) | D | (catalogue's own) |
| PS-06 | Defeated adjustment → continuation (Keller IX-2) | Own entry (§3.1) | D | (catalogue's own) |
| PS-07 | Open-drive / one-way open: first-pullback continuation | Own entry (§3.1) | B (+) for the behaviour: | (catalogue's own) |
| PS-08 | IB extension with acceptance (narrow-IB tier) | Own entry (§3.1) | B (0/+) | (catalogue's own) |
| PS-09 | IB C-period failure → IB midpoint | Own entry (§3.1) | B (0) | (catalogue's own) |
| PS-10 | Developing-POC migration join (VAH-as-support pullback) | Own entry (§3.1) | D | (catalogue's own) |
| PS-11 | Unratified new extreme (POC refuses to follow) → confirmed fade | Own entry (§3.1) | D | (catalogue's own) |
| PS-12 | Trend-cluster pullback (first touch of an in-trend volume cluster) | Own entry (§3.1) | E | (catalogue's own) |
| PS-13 | Volume-accumulation breakout: first-touch retest | Own entry (§3.1) | E | (catalogue's own) |
| PS-14 | Rejection-ledge revisit fade | Own entry (§3.1) | D/E | (catalogue's own) |
| PS-15 | LVN traverse after acceptance into the void | Own entry (§3.1) | C (0) | (catalogue's own) |
| PS-16 | LVN slow-approach bounce | Own entry (§3.1) | E | (catalogue's own) |
| PS-17 | Double-distribution waist trade | Own entry (§3.1) | E/D | (catalogue's own) |
| PS-18 | P/b-profile stop-sweep buy (Levy) | Own entry (§3.1) | E | (catalogue's own) |
| PS-19 | Poor high/low run-through (not repair-as-magnet) | Own entry (§3.1) | B (0) as a magnet. | (catalogue's own) |
| PS-20 | Single-print (initiative tail) pullback continuation | Own entry (§3.1) | B (+) descriptive | (catalogue's own) |
| PS-21 | Naked POC / naked VAH–VAL first touch, with a confirming trigger | Own entry (§3.1) | B (0). | (catalogue's own) |
| PS-22 | Composite LVN-edge rejection vs HVN-ceiling veto | Own entry (§3.1) | D | (catalogue's own) |
| PS-23 | Profile shape at the close → next-session tilt | Own entry (§3.1) | E | (catalogue's own) |
| PS-24 | Overnight high/low break-and-go at RTH | Own entry (§3.1) | B (+) | (catalogue's own) |
| VW-01 | Trend-day first VWAP pullback | Own entry (§3.2) | B (0) | (catalogue's own) |
| VW-02 | Balance-day ±2σ fade at a composite HVN edge (conditional) | Own entry (§3.2) | B (−) | (catalogue's own) |
| VW-03 | σ-band expansion continuation | Own entry (§3.2) | H | (catalogue's own) |
| VW-04 | Failed VWAP defence → reversal context | Own entry (§3.2) | D | (catalogue's own) |
| VW-05 | Late-day continuation: VWAP side × rest-of-day return | Own entry (§3.2) | B/C (0/−) after decay. | (catalogue's own) |
| VW-06 | POC–VWAP skew pullback | Own entry (§3.2) | E/H | (catalogue's own) |
| VW-07 | Prior-day VWAP and ETH VWAP as opening reference (filter test) | Own entry (§3.2) | B (0). | (catalogue's own) |
| VW-08 | VWAP-cross "Holy Grail" (canary) | Own entry (§3.2) | B (−). | (catalogue's own) |
| AV-01 | Swing-high AVWAP reclaim (Shannon "buy strength after the pullback") | Own entry (§3.3) | E | (catalogue's own) |
| AV-02 | Impulse-origin AVWAP higher-low continuation | Own entry (§3.3) | E | (catalogue's own) |
| AV-03 | AVWAP pinch breakout (Shannon) | Own entry (§3.3) | E | (catalogue's own) |
| AV-04 | AVWAP handoff continuation (Shannon) | Own entry (§3.3) | E | (catalogue's own) |
| AV-05 | Gap-anchored AVWAP swing hold | Own entry (§3.3) | E | (catalogue's own) |
| AV-06 | Event-bar AVWAP (FOMC / CPI / NFP) acceptance | Own entry (§3.3) | E | (catalogue's own) |
| AV-07 | Climax-volume-bar AVWAP | Own entry (§3.3) | H | (catalogue's own) |
| AV-08 | Week-open / month-open / quarter-open AVWAP as regime layer | Own entry (§3.3) | B (0) | (catalogue's own) |
| AV-09 | Roll-anchored AVWAP swing bias | Own entry (§3.3) | H | (catalogue's own) |
| AV-10 | AVWAP × profile confluence (does stacking help?) | Own entry (§3.3) | B (0) | (catalogue's own) |
| AV-11 | Breakout-bar AVWAP first pullback | Own entry (§3.3) | H | (catalogue's own) |
| AV-12 | Failed AVWAP reclaim → return to the opposing AVWAP | Own entry (§3.3) | H | (catalogue's own) |
| AV-13 | AVWAP σ-stretch exit / scale signal | Own entry (§3.3) | H | (catalogue's own) |
| MT-01 | HTF-aligned higher low at value (60m → 15m → 5m → 1m nesting) | Own entry (§3.4) | D | (catalogue's own) |
| MT-02 | Weekly accepted value + 60m trigger (swing) | Own entry (§3.4) | D | (catalogue's own) |
| MT-03 | 20-day composite location gate + 5m acceptance break | Own entry (§3.4) | D/C | (catalogue's own) |
| MT-04 | 60m swing-AVWAP + 5m HL entry | Own entry (§3.4) | H | (catalogue's own) |
| MT-05 | 240m multi-session LVN as pullback zone | Own entry (§3.4) | H | (catalogue's own) |
| MT-06 | Daily failed auction (excess / poor extreme) + intraday trigger | Own entry (§3.4) | D | (catalogue's own) |
| OF-01 | Stacked-imbalance origin: first retest continuation | Own entry (§3.5) | E/D | (catalogue's own) |
| OF-02 | Delta-z ratification of the breakout bar (layer) | Own entry (§3.5) | D | (catalogue's own) |
| OF-03 | Anchored CVD flip at a failed break (layer) | Own entry (§3.5) | D | (catalogue's own) |
| OF-04 | Absorption at an aged HTF level (control) | Own entry (§3.5) | B (−). | (catalogue's own) |
| OF-05 | CVD divergence at a value extreme | Own entry (§3.5) | E | (catalogue's own) |
| OF-06 | Unfinished-auction veto (layer) | Own entry (§3.5) | E | (catalogue's own) |
| OF-07 | Bar-level burst at a fresh extreme (layer / standalone) | Own entry (§3.5) | B (+) for the run-through behaviour | (catalogue's own) |
| OF-08 | Large-trade print zone retest | Own entry (§3.5) | E | (catalogue's own) |
| SP-01 | Failed double top → upside breakout (T4) | Own entry (§3.6) | D/H | (catalogue's own) |
| SP-02 | Double top at VAH with second-test divergence (T2, control) | Own entry (§3.6) | B (0) | (catalogue's own) |
| SP-03 | Wyckoff spring at composite VAL with a test (T3) | Own entry (§3.6) | B (0/−) | (catalogue's own) |
| SP-04 | Lower high after a failed auction at a composite extreme (T6) | Own entry (§3.6) | D | (catalogue's own) |
| SP-05 | Four-part reversal-bar event at a profile extreme | Own entry (§3.6) | B (0/−) | (catalogue's own) |
| SP-06 | First higher low after a downtrend at composite VAL (change of character) | Own entry (§3.6) | H | (catalogue's own) |
| SP-07 | Failed-failure re-break (Spec 4) | Own entry (§3.6) | D | (catalogue's own) |
| SP-08 | Point-of-Origin retest (Levy) | Own entry (§3.6) | E | (catalogue's own) |
| TS-01 | European-open overnight drift (long MES 01:30–04:30 ET) | Own entry (§3.7) | B (+). | (catalogue's own) |
| TS-02 | London-open break of the Asia range (CL/GC) with an Asia AVWAP filter | Own entry (§3.7) | H | (catalogue's own) |
| TS-03 | Close regime: 15:30–16:00 MOC drift then reversion | Own entry (§3.7) | C (+?) | (catalogue's own) |
| TS-04 | CL/GC pit-session profile (port of the PS ideas) | Own entry (§3.7) | H | (catalogue's own) |
| TS-05 | ZN post-release AVWAP acceptance (08:30 data) | Own entry (§3.7) | C (0) | (catalogue's own) |
| TS-06 | Overnight inventory correction at the open | Own entry (§3.7) | E/H | (catalogue's own) |
| TS-07 | Opening-noise observation window (filter test) | Own entry (§3.7) | D | (catalogue's own) |
| SW-01 | Weekly balance break (3–6-week composite) | Own entry (§3.8) | C (+) | (catalogue's own) |
| SW-02 | RSI(2) dip-buy with a profile/AVWAP location filter | Own entry (§3.8) | B− (+) | (catalogue's own) |
| SW-03 | Quarterly/monthly AVWAP swing pullback | Own entry (§3.8) | E | (catalogue's own) |
| SW-04 | Multi-day P-profile hold (Levy "will it move within 3 days?") | Own entry (§3.8) | E | (catalogue's own) |
| SW-05 | Composite gap-and-go (out of a 20-day composite) | Own entry (§3.8) | H | (catalogue's own) |
| SW-06 | Weekly IB (Mon–Tue range) breakout with acceptance | Own entry (§3.8) | H | (catalogue's own) |
| RG-01 | Value-migration gate (V-score) | Own entry (§3.9) | C (+) | (catalogue's own) |
| RG-02 | Balance/imbalance classifier + entropy skip | Own entry (§3.9) | C | (catalogue's own) |
| RG-03 | Volatility regime stratifier | Own entry (§3.9) | B | (catalogue's own) |
| RG-04 | Live day-type proxy at 10:30 (includes VWAP-cross count) | Own entry (§3.9) | D/B (0) | (catalogue's own) |
| RG-05 | Noise-band filter (Zarattini) | Own entry (§3.9) | C (decayed) | (catalogue's own) |
| RG-06 | HTF trend state (TSMOM + 60m structure) | Own entry (§3.9) | see entry | (catalogue's own) |
| RG-07 | Event, roll and thin-session exclusions | Own entry (§3.9) | D | (catalogue's own) |
| RG-08 | Compression state (range/ATR) | Own entry (§3.9) | C/B (0/−) | (catalogue's own) |
| PF-01 | Trend + carry core (parallel track) | Own entry (§3.11) | see entry | (catalogue's own) |
| PF-02 | Regime-complementary intraday sleeves | Own entry (§3.11) | H | (catalogue's own) |
| PF-03 | Cross-market diversification & hidden-beta monitor | Own entry (§3.11) | A (principle) | (catalogue's own) |
| PF-04 | Risk budgeting (deflated quarter-Kelly, capped) | Own entry (§3.11) | A (maths) | (catalogue's own) |
| PF-05 | Drawdown throttles + two-CUSUM monitoring | Own entry (§3.11) | D (rules) | (catalogue's own) |
| PF-06 | Portfolio volatility targeting | Own entry (§3.11) | B | (catalogue's own) |
| PF-07 | Meta-labelling the survivors (later) | Own entry (§3.11) | B (method) | (catalogue's own) |
| A2-04 | Overnight inventory vs oVWAP open drive | Own entry (§3.12) | E/D | A2: C |
| A2-09 | Noise-band long-only with sVWAP trail (MNQ benchmark) | Own entry (§3.12) | B (+) long-only | A2: B |
| A2-11 | Prior-day VWAP static-level acceptance | Own entry (§3.12) | B (0) | A2: C |
| A2-12 | VWAP − TWAP divergence as an inventory signal | Own entry (§3.12) | H | A2: C |
| A2-13 | Gap-day AVWAP-from-open: hold → go-with, fail → fade-to-value | Own entry (§3.12) | E | A2: C |
| A2-14 | Weekly AVWAP reclaim swing (MES daily) | Own entry (§3.12) | E | A2: C |
| A2-15 | Breakout-leg POC vs leg AVWAP skew (layer on PS-01) | Own entry (§3.12) | H/E | A2: C |
| EX-01 | Target *before* the next HVN | Own entry (§3.10) | (0) | (catalogue's own) |
| EX-02 | Naked-POC target | Own entry (§3.10) | B (0) | (catalogue's own) |
| EX-03 | Balance-width projection | Own entry (§3.10) | C (+) | (catalogue's own) |
| EX-04 | AVWAP / VWAP trail | Own entry (§3.10) | H | (catalogue's own) |
| EX-05 | Structural HL trail | Own entry (§3.10) | D | (catalogue's own) |
| EX-06 | Decay-based time stop | Own entry (§3.10) | A | (catalogue's own) |
| EX-07 | Scale-out 50% at 1 R + runner | Own entry (§3.10) | A (maths) | (catalogue's own) |
| EX-08 | E(b) target ablation | Own entry (§3.10) | A (method) | (catalogue's own) |
| EX-09 | Stagnation exit | Own entry (§3.10) | E | (catalogue's own) |
| EX-10 | Acceptance-failure invalidation | Own entry (§3.10) | D | (catalogue's own) |
| EX-12 | AVWAP-anchored CVD divergence exit | Own entry (§3.10) | E | (catalogue's own) |
| EX-11 | Stop placement & flow-conditioned trail | Own entry (§3.10) | E | (catalogue's own) |

### A.2 Analyst 2 IDs A2-01…A2-79 (Rev 6)

| ID | Name | Disposition (v2) | Catalogue grade | Original A2 grade |
|---|---|---|---|---|
| A2-01 | Event-AVWAP Handoff Pullback (trend continuation) | Merged into AV-04 | E | C |
| A2-02 | AVWAP Pinch Break (multi-anchor convergence → expansion) | Merged into AV-03 | E | C |
| A2-03 | Breakout-AVWAP Trail on Analyst 1's Spec 1 (exit overlay) | Merged into EX-04 | B (+) trail role / H transfer | B |
| A2-04 | Overnight-Inventory vs oVWAP Open Drive | Own entry (§3.12) | E/D | C |
| A2-05 | Failed Reclaim of the Event AVWAP (trapped-cohort short) | Merged into AV-12 | H | C |
| A2-06 | Session-VWAP Side as Momentum Hold Filter for the Last 30 Minutes | Merged into VW-05 | B/C (0/−) | B (base effect) / C (VWAP variant) |
| A2-07 | Weekly/Monthly AVWAP Regime Filter for Analyst 1's Specs | Merged into AV-08 | B (0) / H | C |
| A2-08 | AVWAP From the Initial-Balance Extreme (IB Breakout Acceptance) | Merged into PS-08 | inherits host B (0/+) | C |
| A2-09 | Noise-Band Long-Only with AVWAP-of-Day Trail, MNQ (replication target) | Own entry (§3.12) | B (+) long-only | B |
| A2-10 | AVWAP σ-Band Breakout Continuation (bands as momentum, not reversion) | Merged into VW-03 | H | C |
| A2-11 | Prior-Day VWAP "Static Level" Acceptance Test | Own entry (§3.12) | B (0) | C |
| A2-12 | Volume-Weighted vs Unweighted Divergence as Inventory Signal | Own entry (§3.12) | H | C |
| A2-13 | Gap-Day AVWAP-from-Open Fade Failure → Go-With (Shannon gap chapter) | Own entry (§3.12) | E | C |
| A2-14 | Weekly AVWAP Reclaim Swing (MES daily) | Own entry (§3.12) | E | C |
| A2-15 | Volume-Profile POC of the Anchored Leg vs AVWAP of the Same Leg (agreement filter) | Own entry (§3.12) | H/E | C |
| A2-16 | Opening-Range AVWAP Slope Regime (trend-day classifier) | Merged into RG-04 | inherits host D / B (0) | C |
| A2-17 | Multi-Session AVWAP from a High-Volume Liquidation Bar (capitulation anchor) | Merged into AV-07 | H | C |
| A2-18 | Developing-Profile POC Migration vs sVWAP Lag (value-migration timing) | Merged into PS-10 | D | C |
| A2-19 | Event-AVWAP Bands as Profit Targets for Analyst 1 Spec Runners | Merged into AV-13 | H | C |
| A2-20 | CL Inventory-Release AVWAP Continuation (Wednesday 10:30 EIA) | Merged into AV-06 | E | C/B− |
| A2-21 | FOMC-Statement AVWAP Second-Leg Entry (14:00 ET) | Merged into AV-06 | E | C |
| A2-22 | AVWAP-Anchored Cumulative Delta Divergence as Exit-Only Signal | Merged into EX-12 | E | C |
| A2-23 | Quarterly-Roll / Contract-Expiry AVWAP Reset (structural anchor) | Merged into AV-09 | H | C |
| A2-24 | VWAP Fade Negative Control Suite (must run) | Merged into VW-08 | B (−) | A (negative) |
| A2-25 | POC Bootstrap-Stability Gate and Minimum-Stop Rule | Own entry (§3.12) | B for the statistic (Keller method) · H for the trading lift | B for the statistics, C for the trading lift |
| A2-26 | KDE Bandwidth-Sweep Veto (no trade on FLAT or BIMODAL POCs) | Own entry (§3.12) | A for the estimator (method) · H for the lift | A for the estimator, B/C for the trading lift |
| A2-27 | Bimodal-Profile Void Traverse (continuation across the void) | Own entry (§3.12) | D (mechanism) · H (trade) | B (mechanism) / C (trade) |
| A2-28 | Minor-Component Excess Rejection (confirmation, not a touch fade) | Own entry (§3.12) | H | C |
| A2-29 | Entropy Day-Type Gate | Own entry (§3.12) | B (descriptive) · H (gate) | A descriptive, C for the gate |
| A2-30 | Rising-Entropy Balance Break (leading-signal hypothesis) | Own entry (§3.12) | D/H | B/C |
| A2-31 | KL-Divergence Acceptance / Migration Gate for Prior-Day Levels | Own entry (§3.12) | H | C |
| A2-32 | Volatility-Normalised Mixture Composite (σ-space levels) | Own entry (§3.12) | D (method) · H (trade) | B (method) / C (trade) |
| A2-33 | Level-Persistence vs Matched-Control Study (must run before any level idea) | Own entry (§3.0) | n/a (infrastructure; Keller B2 matched-control method) | infrastructure; outcome decides which level types to keep |
| A2-34 | V/T Urgency-Price Revisit (event study first) | Own entry (§3.12) | H | C |
| A2-35 | Macro-Event Re-Formation Value Area (CPI/NFP 08:30, FOMC 14:00) | Own entry (§3.12) | D (mechanism) · H (trade) | B (mechanism) / C (trade) |
| A2-36 | Dealer-Gamma Regime Filter (regime, not level) | Own entry (§3.12) | D (regime read) · H (flip level) | B for the regime read, C for any specific flip level |
| A2-37 | Cross-Instrument Dislocation Fragility Filter | Own entry (§3.12) | D | B |
| A2-38 | Metaorder Drift Completion (tick window only) | Own entry (§3.12) | H | C |
| A2-39 | Roll-Honest Composites on Unadjusted `@ES` (infrastructure) | Own entry (§3.0) | D | B |
| A2-40 | Honest Cost Model and Stressed-Correlation Sizing (infrastructure) | Own entry (§3.0) | A (method) | A (documented) |
| A2-41 | Book-Spec Balance Break with a Balance-Quality Filter (variant of A1 PS-02) | Merged into PS-02 | C (FX/crypto base rates, other asset class) · H (quality filter) | B (other asset class) for the base rates (FX 378 breaks, 56.6% reach 1 W, median 1.17 W; crypto 59.1%, 1.20 W). **C** for the quality filter, which the book itself never tested. CME: untested. |
| A2-42 | 80% Rule Resized as a 1-in-3, 3:1 Trade (variant of A1 PS-03's naive canary) | Merged into PS-03 | C (FX/crypto traverse rates) · E (3:1 geometry, exhibit ratios) | B (other asset class) for the 31.4% FX / 28.4% crypto traverse rates (≈ 19,000 re-entries). **C** for the 3:1 geometry (exhibit ratios, selected). Pit-era CME day session is the original setting, but the book flags it as untested on modern index futures. |
| A2-43 | Confluent-HVN Level Gate (HVN counts only with independent confluence) | Own entry (§3.12) | E (and the book's own HVN rejection ratios are < 1) | C |
| A2-44 | Post-Crossing Fast-LVN Continuation (variant of A1 PS-15) | Merged into PS-15 | C (−) against the anticipatory version · E (confirmed version) | B (other asset class) **against** the anticipatory version; C for the confirmed version. |
| A2-45 | Single-Print Shelf as Destination (near edge target, runner to the far edge) | Own entry (§3.12) | H | C |
| A2-46 | Volume-Gap Commitment Entry to the Far Node (variant of A2-27; contrast with PS-15) | Own entry (§3.12) | E (exhibit ratios, selected) | C |
| A2-47 | Confirmed Failed Auction Aligned with Value Migration, Target Inside Value (variant of A1 PS-03 / A2-28) | Merged into PS-03 | E (book practitioner) · B (−) for the fade-at-touch version | C (B practitioner in the book; the fade-at-touch version is B-negative with MAE 6.3 / 12.6 ATR). |
| A2-48 | Poor Highs/Lows as Destinations (exit and stop-placement module) | Own entry (§3.12) | B (0) prior for the magnet (Perfiltrade 11.9% vs 12.9% controls) · H for the module | C |
| A2-49 | Midday Support/Resistance and Afternoon Breakout (TSM Midday S&R replication) | Own entry (§3.12) | C (+, gross) | B− (gross, old contract and pit hours) |
| A2-50 | Same-Slot Relative Volume Opening Regime Gate (Kaufman intraday volume) | Own entry (§3.12) | E | C |
| A2-51 | Intraday Shock-Bar Fade with Delayed Stop and Volume-Decay Exit | Own entry (§3.12) | C (other markets, gross) | C (other markets, gross) |
| A2-52 | Time-Confirmed Opening-Range Break with Pivot-Range Bias (Fisher ACD, variant of A1 ORB specs) | Own entry (§3.12) | E (practitioner, no numbers); plain ORB ≈ 0 net (B, 0) | C |
| A2-53 | RTH Gap Fade with Stop-and-Reverse at the Day's Extreme (TSM Intraday High-Low; negative canary) | Own entry (§3.12) | C (gross; net expected ≤ 0) | B− (gross; the net edge is likely ≤ 0) |
| A2-54 | Up-Gap Pullback-to-Prior-Close Short, Exit Next Open (TSM Intraday Gaps) | Own entry (§3.12) | E | C |
| A2-55 | Volatility-Contraction Gate for Intraday Breakout Hosts (Crabel / Kaufman module) | Merged into RG-08 | C (CL, gross) · E (ES, few trades); NR7 stays negative | B− (CL), C (ES: few trades); the catalogue grades NR7 negative. |
| A2-56 | Momentum Pinball: Daily LBR/RSI Setup, First-Hour Break, Conditional Overnight Hold | Own entry (§3.12) | C (+, gross) | B− (gross, but $703 leaves a large cost margin) |
| A2-57 | Higher-TF Turn plus First Short-TF Oversold Entry (Kaufman/Elder MTF tuning) | Own entry (§3.12) | E | C |
| A2-58 | Fade the Fast Trend Inside the Slow Trend, Intraday (Kaufman "profiting from the worst results") | Own entry (§3.12) | C (daily NASDAQ) · H intraday | B− on daily bars, C intraday |
| A2-59 | Kaufman Cost, "Unables" and Shock-Audit Module (infrastructure, extends A2-40) | Own entry (§3.0) | A (method) | n/a (method) |
| A2-60 | Meyers Normalised Range-Ratio Adaptive Breakout (5m, flat before the close) | Own entry (§3.12) | E (one optimised ETF article, no OOS) | C |
| A2-61 | Efficiency-Ratio Session Router (noise gate selecting the reversion vs continuation arm) | Own entry (§3.12) | E | C |
| A2-62 | Taylor 3-Day Cycle "Low-First" Buying Day (daily rhythm, RTH execution) | Own entry (§3.12) | E/D | C/D |
| A2-63 | Long-Only Open + 0.5 × Daily ATR Breakout, Exit at the Close (KCTS Ch 25) | Own entry (§3.12) | C (+, in-sample, no slippage, multi-contract sizing) | B− (ES, multi-year, but in-sample, no slippage and multi-contract sizing) |
| A2-64 | 1.0 × Daily ATR Fade from the Open or Prior Close, with the Prior-Close Exit (KCTS Ch 25) | Own entry (§3.12) | E (S&P leg not isolated, in-sample) | C (S&P result not isolated, in-sample) |
| A2-65 | Raschke "1st Cross" — First Oscillator/Trend Cross with Bar Confirmation (KCTS Ch 24) | Own entry (§3.12) | C (+, daily ES) · H on 30m | B− on daily ES; C on intraday bars (untested) |
| A2-66 | Raschke/Kaufman "2nd Cross" — Second Stochastic Pullback in a Confirmed Trend (KCTS Ch 24) | Own entry (§3.12) | C (daily ES, 86 trades, in-sample) | B− (daily ES, but only 86 trades and in-sample) |
| A2-67 | Intraday RSI-of-Highs/Lows Divergence Long (KCTS Ch 25, IWM/QQQ) | Own entry (§3.12) | E (14 trades, one year) | C (one year, 14 trades) |
| A2-68 | 5-Bar Run + Doji Reversal, Target 50% of the Run (KCTS Ch 25, from S&C) | Own entry (§3.12) | E (S&C article, no numbers) | D |
| A2-69 | Kaufman 3-Day Trade — Two Down Closes, Buy a Lower Open, Exit Next Close (KCTS Ch 15/24/27) | Own entry (§3.12) | C (+, multi-decade, gross) | B− (multi-decade ES/NQ/RU, gross) |
| A2-70 | High-Volatility Skip Gate (KCTS Ch 3/5/15/18; module) | Own entry (§3.12) | E | C (SPY daily test only; thresholds are market-specific) |
| A2-71 | ATR Risk Sizing and 12% Volatility Targeting for the A2 Book (infrastructure, extends A2-40) | Merged into PF-06 | C (method, in-sample book numbers); PF-06 host grade applies to the vol-targeting principle | C (method; book numbers are in-sample). |
| A2-72 | Progressive Exposure Ladder — 25/50/100% After Wins, Step Down After a Loss (sizing module) | Own entry (§3.13) | D | D (illustrative table, no test) |
| A2-73 | Breakeven Stop at ≈ 3R or Above the Rolling Average Gain (exit module) | Own entry (§3.13) | E | C (stated numeric rule, no test) |
| A2-74 | Free Roll — Sell Half at 2–3R, Remainder at Breakeven (scale-out module) | Own entry (§3.13) | E | C |
| A2-75 | Rolling Hit-Rate Throttle — Tighten Stops and Targets When the Batting Average Falls (RBA module) | Own entry (§3.13) | D | D (no test; the trigger is discretionary in the book) |
| A2-76 | Sell Into the Climax Bar — Largest Range or Largest Up Bar Since Entry (profit-exit module) | Own entry (§3.13) | D | D (daily-chart heuristic; intraday analogue untested) |
| A2-77 | Failed Follow-Through Exit — Time Stop and Bar-Quality Test After Entry (exit module) | Own entry (§3.13) | E | C (stated heuristics) |
| A2-78 | Daily Loss Kill-Switch and Rules-First Flatten (risk module, infrastructure + test) | Own entry (§3.13) | D for edge; mandatory as an operational control | D for edge (no evidence it improves expectancy); mandatory as an operational control |
| A2-79 | Intraday Volatility-Contraction plus Volume Dry-Up Entry Gate (VCP analogue; module) | Own entry (§3.13) | D | D (a daily/weekly chart pattern; intraday analogue needs empirical testing) |

### A.3 Analyst 2 L2 IDs (Appendix A of the A2 file)

| ID | Name | Disposition (v2) | Catalogue grade | Original grade |
|---|---|---|---|---|
| L2-01 | Cancellation veto before level entries | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data) |
| L2-02 | Book-shape regime | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data) |
| L2-03 | Resilience half-life T½ prior to range breaks | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data (sub-second; 10-level snapshots may be too coarse)) |
| L2-04 | Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor (Ch 11.4, 13.3) | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data **and** MBO timestamps) |
| L2-05 | Pre-event depth withdrawal forecasts the spike's revert fraction (Ch 14.4, Drill 14.4; complements A2-35) | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data) |
| L2-06 | Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1) | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data (execution rule, not a signal)) |
| L2-07 | Two-sided cancellation surge = fragility alarm (Ch 11.6) | Own entry (§3.15, parked) | H | not graded (needs recorded L2 data) |

### A.4 Analyst 2 Methodology M1…M30

| ID | Name | Disposition (v2) | Catalogue grade | Original grade |
|---|---|---|---|---|
| M1 | Write the expectations first | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M2 | Fix the parameter range in advance, inside the concept | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M3 | Space parameters geometrically | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M4 | Test in order of importance | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M5 | Model costs by style, and haircut "unables" | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M6 | Judge the whole surface, not the peak | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M7 | Smooth before you select | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M8 | n-space sensitivity shells | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M9 | Out-of-sample discipline | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M10 | Cross-market out-of-sample | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M11 | Count every test | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M12 | Shock audit and risk realism | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M13 | Rule changes must improve globally | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M14 | Keep rules minimal (≤ 4 free parameters) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M15 | Live monitoring against the tested profile | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M16 | Same-slot normalisation for any volume or volatility condition | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M17 | Trade at least three parameter sets, equally weighted (KCTS Ch 4/12/14/15) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M18 | Expect live IR ≈ half the backtest (KCTS Ch 14) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M19 | Trade-count significance table (KCTS Ch 12/20, Seibert Monte Carlo) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M20 | Include 2008 and 2020 (KCTS Ch 20/29) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M21 | Recent-vs-old t-test (KCTS Ch 14/20) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M22 | One trade journal per strategy, every trade (Minervini Sec 4) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M23 | Track the expectancy triangle on a rolling basis | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M24 | Result-based assumptions, not targets | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M25 | Outcome histogram and "The Wall" | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M26 | Compounded vs non-compounded and sequence-risk reporting | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M27 | Pre-registered contingency plan before going live | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M28 | No rule exceptions, ever | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M29 | Daily and periodic review loop | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |
| M30 | Scale capital only on evidence (Minervini's progressive exposure, at the strategy level) | Own item (§9 Methodology) | n/a (protocol) | n/a (protocol) |

### A.5 Trader Dale video IDs DV-01…DV-24

| ID | Name | Disposition (v2) | Catalogue grade | Original DV grade · Dale priority |
|---|---|---|---|---|
| DV-01 | Daily POC first-touch pullback | Own entry (§3.14) — control | E | E (vendor) · priority 2 (control/baseline) |
| DV-02 | Failed level → reversal (flip) trade | Own entry (§3.14) | E | E · priority 5 |
| DV-03 | Volume accumulation (rotation → trend, first retest) | Merged into PS-13 | E | E · priority 4 |
| DV-04 | Trend-cluster pullback (+ S/R-flip confluence) | Merged into PS-12 | E | E · priority 4 |
| DV-05 | Rejection HV-zone retest with 4-point checklist | Merged into PS-14 | E | E · priority 2 |
| DV-06 | End-of-trend-day cluster pullback (NY close) | Own entry (§3.14) | E | E · priority 2 |
| DV-07 | Rotation / double-distribution edge → centre | Own entry (§3.14) | E | E · priority 2 |
| DV-08 | HV zone + FVG alignment (level at FVG start) | Own entry (§3.14) | E | E · priority 3 |
| DV-09 | Weekly-VWAP ±1σ trend pullback | Own entry (§3.14) | E | E (+ his ~60% claim) · priority 2 |
| DV-10 | Weekly-VWAP ±1σ rotation fade → VWAP | Merged into VW-08 | E | E · priority 1 (canary) |
| DV-11 | VWAP trend-failure (lose +1σ → short pullback → VWAP) | Own entry (§3.14) | E | E · priority 3 |
| DV-12 | Prominent-swing AVWAP pullback | Merged into AV-02 | E | E · priority 2 |
| DV-13 | Accumulation-end AVWAP pullback (+ flip) | Merged into AV-11 | E | E · priority 3 |
| DV-14 | Week-open AVWAP S/R with side flip | Merged into AV-08 | E | E · priority 1–2 |
| DV-15 | EMA20 pullback gated by VWAP slope | Own entry (§3.14) | E | **E+ (his small test: 2 mo EUR/USD, 138→73 trades, 48.5%→60% at 10/10 pips)** · priority 4 |
| DV-16 | Order-flow confirmation at a level (absorption / big limit / aggressor / trapped) | Own entry (§3.14) | E | E · priority 3 (layer) |
| DV-17 | Large-trade (trades-filter) zone pullback | Merged into OF-08 | E | E · priority 2 |
| DV-18 | 4h-profile POC + cumulative-delta divergence (1m) | Merged into OF-05 | E | E · priority 2 |
| DV-19 | Failed-auction cluster as magnet/target (+ veto) | Merged into OF-06 | E | E · priority 3 (exit layer) |
| DV-20 | Stop-loss-cluster ("weak low/high") veto | Own entry (§3.14) | E | E · priority 3 (filter) |
| DV-21 | Price-action S/R flip (standalone) | Merged into DV-02 | E | E · priority 3 |
| DV-22 | ABCD (AB = CD) pattern | Own entry (§3.14) | E | E (he claims "65% win rate" in title only) · priority 1 |
| DV-23 | IB draw trade (guest Dan/FTA, not Dale) | Own entry (§3.14) | E | E (claims 96.4% IB-touch) · priority 3 |
| DV-24 | Exit module: TP before barrier, SL behind barrier, daily-close stop + catastrophic stop | Merged into EX-01 | E | E · priority 4 (exit) |


### A.6 Keller B5 IDs KD-01…KD-11 (rev 3.1, signed off 23 Oct 2026)

| ID | Name | Disposition (v2.3) | Catalogue grade | Original grade (B5 rev 3.1) |
|---|---|---|---|---|
| KD-01 | Accept-beyond-rail continuation (detrended rails, rails as targets) | Own entry **PS-25** (§3.1) | C (+) family · setup untested · E geometry | same · FX/crypto only · no true delta · ES prior 0 |
| KD-02 | Band-stretch fade to the POC rail (control / canary) | Own entry (§3.16); G0-b canary | C (0) | same · FX/crypto only · no true delta · ES prior 0 |
| KD-03 | Three-family confluence filter (module) | Own entry **RG-09** (§3.9) | C (+) one-bar hold only, inadmissible; E (0/−) geometry | same · FX/crypto only · no true delta · ES prior 0 |
| KD-04 | Detrended-fit regime gate: degree-1 slope with band-break refit (module) | Own entry **RG-10** (§3.9) | C (+) FX/crypto; H as a gate | same · FX/crypto only · no true delta · ES prior 0 |
| KD-05 | HTF detrended-rail alignment | Own entry **MT-07** (§3.4) | H | same · FX/crypto only · no true delta · ES prior 0 |
| KD-06 | σ-band acceptance breakout (PS-25 arm σ2) | Own entry (§3.16), robustness arm of PS-25 | E | E · FX/crypto only · no true delta · ES prior 0 |
| KD-07 | No-fade veto (gate form of KD-02) | Own entry (§3.16) | C (0) reason / H gate | not graded separately · FX/crypto only · no true delta · ES prior 0 |
| KD-08 | Forward-cone diagnostic (study) | Own entry (§3.16) | C (census, FX/crypto) | census · FX/crypto only · no true delta · ES prior 0 |
| KD-09 | Family-count confluence filter (RG-09 rev 3.1 module form) | Own entry (§3.16); alias of RG-09 | C inadmissible / E / C hypothesis | same · FX/crypto only · no true delta · ES prior 0 |
| KD-10 | Far-outside / band-edge node, acceptance only (PS-25 arm node) | Own entry (§3.16), robustness arm of PS-25 | E | E · FX/crypto only · no true delta · ES prior 0 |
| KD-11 | Beyond-value node reach-rate study (Drill 7.5) | Own entry (§3.16); A2-33 / G0-d extension | C hypothesis | C hypothesis · FX/crypto only · no true delta · ES prior 0 |

### A.7 R7 volatility and leveraged-ETF IDs VX-01…VX-08, LE-01…LE-07 (v2.5)

| ID | Name | Disposition (v2.5) | Catalogue grade | Original grade (R7) |
|---|---|---|---|---|
| VX-01 | Hedged VX term-structure carry (Simon–Campasano roll rule) | Own entry (§3.17) | B (+) | same |
| VX-02 | VRP-filtered short volatility (Cooper VRP rule, futures form) | Own entry (§3.17) | D (0) | same |
| VX-03 | Long volatility in backwardation (VX-01's long leg alone) | Own entry (§3.17) | C (+) | same |
| VX-04 | Post-spike normalisation short (VIX spike mean reversion) | Own entry (§3.17) | H (+) | same |
| VX-05 | Long-volatility tail-hedge sleeve (portfolio insurance) | Own entry (§3.17) | C (−) | same |
| VX-06 | VIX term-structure regime gate for ES hosts (layer) | Own entry (§3.17) | C (0) | same |
| VX-07 | VVIX gate (module) | Own entry (§3.17) | C (+) | same |
| VX-08 | VIX-stretch ES dip-buy (Connors & Alvarez) — layer on SW-02 / canary | Own entry (§3.17) | E (−) | same |
| LE-01 | 200-day trend leverage rotation (Gayed & Bilello) | Own entry (§3.18) | C (+) | same |
| LE-02 | Volatility-targeted leverage (exposure ∝ target vol / realised vol) | Own entry (§3.18) | C (+), contested | same |
| LE-03 | QQQ ↔ TQQQ regime leverage rotation | Own entry (§3.18) | H (+) | same |
| LE-04 | LETF rebalancing pressure into the close (flow-scaled arm of VW-05) | Own entry (§3.18) | B (−), decaying | same |
| LE-05 | Decay-pair short (short TQQQ + SQQQ, or UPRO + SPXU) | Own entry (§3.18) | C (+), simulated; | same |
| LE-06 | Short-term RSI mean reversion on LETFs (Connors-style) | Own entry (§3.18) | D (+) | same |
| LE-07 | Inverse-LETF bear-trend hold (SQQQ/SPXU below SMA200) — negative control | Own entry (§3.18) | H (−) expected | same |

### A.8 YouTube-video IDs YV-01…YV-10 and existing-only ideas (v2.6)

| ID | Name | Source video | Classification (v2.6) | Related IDs | Catalogue grade |
|---|---|---|---|---|---|
| YV-01 | 30m VA break → 5m big trades → 1m failed-seller entry | Trader Oxygen jmX_ReJxFm8 | VARIANT | PS-01; DV-16, OF-08, EX-11; **OF-09, EX-13** (v2.7 spec) | E |
| YV-02 | Cross-index flow selection | Trader Oxygen jmX_ReJxFm8 | NEW (layer) | M-XMKT, PF-03; **OF-10** (v2.7 spec) | E |
| YV-03 | Momentum burst (stocks) | Financial Wisdom 9ksbN69G7i8 | NEW | RG-06 (regime), A2-71/PF-04 (sizing), RG-08/A2-55 (NR conflict) | E |
| YV-04 | Environment–location–confirmation absorption | IQCapital PL7LKUsCgIQ | VARIANT | OF-04, DV-16, MT-01, A2-36 | E |
| YV-05 | Absolute participation gate (20,000 MNQ/5m) | IQCapital PL7LKUsCgIQ | VARIANT | A2-50 | E |
| YV-06 | Session stop after 2 losses / 1–2 attempts | IQCapital PL7LKUsCgIQ; JadeCap DQiOqFFptfA | VARIANT | A2-78, A2-75, F-TM | E |
| YV-07 | Macro-window 15s sweep-FVG scalp | Dylan Richards 83d144f2e0k | NEW | DV-08, DV-20, TS-02; **SP-09** (v2.7 spec) | E |
| YV-08 | Daily sweep: 1h SFP → 5m FVG | JadeCap DQiOqFFptfA | VARIANT | PS-18, DV-08, YV-07 | E |
| YV-09 | Turtle Donchian S1/S2 + EMA20 exit | Dollar Wisdom lDn93erRQSM | NEW | RG-06, A2-71, PF-01; **PF-08** (v2.7 spec) | E |
| YV-10 | 20d-return Markov regime signal | Lewis Jackson Investing ZVMTeDBmSrI | VARIANT | RG-06, G-R `_RegimeEngine` | E |
| — | Open into prior VA → traverse (80% rule) | Tom Vorwald EN YmygDgtoxO8 | EXISTING | PS-03, A2-42 | — |
| — | LVN bounce / rejection | Tom Vorwald EN YmygDgtoxO8 | EXISTING | PS-16, PS-22 | — |
| — | Balance shift → afternoon retest of the broken edge | Tom Vorwald EN YmygDgtoxO8 | EXISTING | PS-01 (mirror), DV-02 | — |
| — | Multi-day overlap (LVN+VAH+POC) rejection | Tom Vorwald EN YmygDgtoxO8 | EXISTING | A2-43, PS-22 | — |
| — | Acceleration through an LVN/below value | Tom Vorwald EN YmygDgtoxO8 | EXISTING | PS-02, PS-15 | — |
| — | GEX regime (positive/negative gamma, call/put wall) | IQCapital PL7LKUsCgIQ | EXISTING | A2-36 (data-blocked) | — |
| — | Weekly POC as HTF magnet/target | Trader Oxygen jmX_ReJxFm8 | EXISTING | MT-02, EX-02 | — |
| — | Weekly 10/20 EMA index regime | Financial Wisdom 9ksbN69G7i8 | VARIANT folded into YV-03 | RG-06 | — |
| — | Turtle risk 1–2% per trade / drawdown | Dollar Wisdom lDn93erRQSM | EXISTING | A2-71 | — |
---


### A.9 Video specifications PF-08, SP-09, OF-09, OF-10, EX-13 (v2.7)

Source: `specs/video-ideas-2026-11-08-additions.md`. These are closed-bar specifications. YV-01, YV-02, YV-07 and YV-09 remain the discretionary extracts (§3.19) and are not replaced.

| ID | Name | Disposition (v2.7) | Catalogue grade | Video |
|---|---|---|---|---|
| PF-08 | Donchian / Turtle daily trend sleeve | Own entry (§3.11), companion to PF-01 | E (family prior A does not transfer) | Dollar Wisdom lDn93erRQSM |
| SP-09 | Clock-window equal-liquidity sweep into an HTF FVG | Own entry (§3.6) | E (sweep-fade prior 0, PS-18) | Dylan Richards 83d144f2e0k |
| OF-09 | Weekly bias → 30m VA break → 1m failed-seller | Own entry (§3.5); extends DV-16 | E | Trader Oxygen jmX_ReJxFm8 |
| OF-10 | ES vs NQ order-flow selection | Own entry (§3.5); layer; not buildable yet | E | Trader Oxygen jmX_ReJxFm8 |
| EX-13 | 1.5R partial, deep-pullback breakeven, initiative trail | Own entry (§3.10); module on OF-09 | E | Trader Oxygen jmX_ReJxFm8 |

## Appendix B. Changes vs v1 (`trading-idea-catalogue-v1.md`)

**Sources added**
- Analyst 2 Rev 2–Rev 6: A2-25…A2-79 (v1 had merged Rev 1 only, A2-01…A2-24), L2-01…L2-07 and Methodology M1…M30.
- Trader Dale YouTube DV-01…DV-24.
- New entries: 49 A2, 12 DV and 7 L2. Merged into hosts: 6 more A2 (A2-41→PS-02, A2-42→PS-03, A2-44→PS-15, A2-47→PS-03, A2-55→RG-08, A2-71→PF-06) and 12 DV.

**New sections**
- Key takeaways.
- §2.0.1 window reconciliation.
- §2.6 Gate 0.
- §3.0 Gate-0 entries.
- §3.12 rewritten, with a 79-row mapping table, the rescaling rules, A2's convention blocks and the new entries.
- §3.13 exit/risk modules.
- §3.14 DV family.
- §3.15 L2 parked.
- §9 Methodology (References become §10).
- Appendices A and B.
- §1 gains an "Original grade" field. §2.3 gains A2's generic kill. §2.5 gains the `GateModule`/`RiskModule` inputs.

**Merge fixes (from `catalogue-merge-audit-analyst2.md`)**
- **Windows:**
  - Reconciled to one program-wide split (§2.0.1), with A2's original split shown alongside.
  - W-T now uses A2's 250/125/125-session split.
  - W-D now has an explicit dev/validation/holdout split.
- **A2-05 (in AV-12):**
  - Removed the BarUpDown fallback. The delta arm is W-T only, and the no-delta arm on W-M is the control.
  - Restored the kill "generic + beat the same rule at a P1 line". The host "≤ 0 R" kill now applies only to the AV-12 swing arm.
- **A2-06 (in VW-05):** restored the VIX-regime and NGE-proxy splits and the kill "no lift over ROD-only, or a negative holdout". The v1 kill now applies to the 15:00 arm only.
- **A2-13:** removed the added > 60%-overlap kill (overlap is reported only). Kill = generic per branch, with P1/P2.
- **A2-17 (in AV-07):** the event study is back on W-D across all pooled-market history (the strategy layer stays W-M). Kill: < 100 pooled events or a negative pooled expectancy. Removed "expect park".
- **A2-20 (in AV-06):** restored ≥ 150 events per sample and the required 10:45–12:00 conditional-drift event study. A2-21 keeps ≥ 100 pooled.
- **A2-01 (in AV-04):** restored the kill "no improvement over Spec 1 as a location filter".
- **A2-14:** W-D primary (A2: window D only).
- **A2-24 (in VW-08):** ES 2019–2024 window restored, and it is now part of Gate 0.
- **Minor restorations:** flat 15:55 for A2-08, A2-10 and A2-18; Chuk's +$10/day replication target for A2-09; ES/NQ pooling for A2-22; A2-23's use as the A2-14 reclaim-swing anchor; explicit "Merged · A2-03" and "Merged · A2-22" notes after the EX table.
- **Grades:** the original A2 grade is now shown next to every rescaled grade (merged notes, own entries, the mapping table and Appendix A).
- **Gating:**
  - A2-33, A2-39, A2-40 and A2-59 are Gate-0 prerequisites (§2.6, §3.0, §5, §7 row 0).
  - A2-72…A2-79 are exit/risk ablations on hosts H1 (PS-01/Spec 1), H2 (A2-63) and H3 (A2-09).

**Matrix and batches**
- 37 new matrix rows (A2 and DV), re-run with unchanged weights. The matrix now has 123 rows; v1 had 86.
- Batch 1 and Batch 2 rebuilt (§5).
- §6 portfolio: A2-71 sizing, A2-78 kill-switch, and M17/M18/M30.
- §7 gates: row 0, split row 6, gate 9.
- §8 build order: M-COST, the A2-33 harness, M-GMM, the range-profile/FVG helpers, M-XMKT, M-GEX and the DOM recorder.

**v2.1 (2026-10-21, as of merge of MultiCharts.Net PRs #6–#13)**
- New §2.4a implementation-status table; §2.4 rows, every Needs line (✓/◐/✗), a Readiness line per idea, §4 Impl note, §5.1 batch-1 readiness, §8 build-order status.
- M-MTF text now reads "native BarsOfData(n), no internal HTF bar building" (Analyst 2).
- IDs and evidence grades unchanged; matrix scores not re-run.

**v2.2 (2026-10-21, Probe P5 resolved)**
- MC .NET portfolio API supplied by the user (`specs/mc-portfolio-api.txt`); M-PORT spec written (`specs/m-port-spec.md`).
- §2.4 / §2.4a M-PORT rows: "API confirmed (P5 resolved), spec ready, not yet coded"; §8 build-order row 9 updated.
- Needs/Readiness lines of the six M-PORT-blocked ideas updated: A2-40, PF-01, PF-03, PF-04, PF-05, A2-78.
- IDs, grades and rules unchanged.

**v2.3 (2026-10-23, Keller B5 rev 3.1 signed off; acceptance N = 2 primary)**
- Source added: `specs/keller-b5-catalogue-additions.md` rev 3.1 (with `specs/keller-b5-p6p7-entries-analyst2.md` as amended): KD-01…KD-11. Mapping: KD-01 → PS-25, KD-03 → RG-09, KD-04 → RG-10, KD-05 → MT-07; KD-02 canary only; KD-06…KD-11 in the new §3.16.
- Header (merged line, companion documents); §2.4 and §2.4a M-DVP rows and summary count; new §2.4b (M-DVP spec, `DvpConfig`, rev 3.1 constants); §2.6 G0-b (KD-02) and G0-d (DVP level kinds); §3 counts (193 entries; PS 25, MT 7, RG 10, KD 7); §3.0 canary note.
- New entries PS-25, MT-07, RG-09, RG-10 and §3.16 (mapping, rulings, Holm m = 5 α = 0.05, hidden-trial exclusions, KD-02, KD-06…KD-11).
- Cross-references added to A2-25…A2-30, A2-31 (Holm, tie-break), A2-32, A2-33, A2-46, VW-03, PS-01, PS-02, DV-02 (Holm membership).
- §4: PS-25 row (124 rows) and top-40 rank 26 (later ranks shift by one; SW-01 drops out of the top 40); other scores not re-run. §5.3 KD batch after Batch 2; §8 row 3d M-DVP; Appendix A.6.
- No existing IDs, grades or rules of earlier entries changed; no new statistics (every KD number is the book's printed FX/crypto number, or Analyst 2's tally where labelled).

**v2.3a (2026-10-23, Analyst 2 review of v2.3)**
- RG-09, §3.16 (KD-09, hidden-trial item 4), §5.3: KD-09 replaces KD-03 arms A and C, confTol fixed at 0.3 ATR; arm B (≤ 1) stays as KD-09's contrast; the KD batch runs only KD-09.
- PS-25 primary cell: PROPOSED (both analysts) N = 2, event rail VAH/VAL, EX-05 trail + 41-bar time stop, with rationale; Ettienne to confirm before any dev read (PS-25, §3.16, §5.3).
- §4.2: PS-25's non-evidence scores and priority marked [est] with footnote ¹ (drafter estimates; ES evidence prior 0); MT-07 added as an [est] layer row (Ev 1.5 H; priority 45) per the MT convention; removed from the not-scored list (matrix now 125 rows). §4.1: [est] on PS-25's rank and a note that SW-01 drops out of the top 40 on estimates alone, pending Ettienne's confirmation.
- Companion spec `specs/keller-b5-catalogue-additions.md` §4 and ID-scheme note corrected to this mapping (no PS-26); its PDF rebuilt. Renderer `render_v2_1.py` gains the `pre` wrap rule.

**v2.3b (2026-10-23, implementation-status re-audit against `origin/main` @ `629da0d`)**
- §2.4 / §2.4a re-checked by `git grep` / `git ls-tree` on `origin/main` (PRs #14–#29; #29 merged as `629da0d`). Corrected: M-GMM ✗ → ✓ (PR #18; PR #21/#24), VP helpers ✗ → ✓ (PR #18), A2-33 harness split out as its own row ◐ (PRs #15/#16, #20 `5ca25a6`; Cox not implemented), M-PORT "not yet coded" → ◐ coded, P5b live checks pending (PRs #14/#17/#22), M-NEWS ✗ → ◐ M-NEWS/M-EVENT (PRs #25/#27/#28/#29), gap notes on F-OF, M-AVWAP, M-SWING, M-TOD, M-DAILY (PR #19). Summary now 9 available / 5 partial / 2 missing over 16 rows.
- Needs/Readiness lines of 41 ideas updated (list in `specs/catalogue-v2.3b-status-changes.md`); A2-34's readiness text fixed (it said "entropy helper"; the need is V(p)/T(p)); DV-08's Needs ✗ FVG fixed (its Readiness already said buildable).
- §5.1 batch-1 readiness and §8 rows 0b, 2, 3b, 3c, 5, 7, 9 updated. Unchanged and re-confirmed missing: M-DVP, M-GEX, M-TICK size outputs (§E6), M-COST style/p90/haircut, A2-38 trade-size entropy and λ̂ helper.
- IDs, grades, rules and matrix scores unchanged.
- Verification pass 2 (23 Oct 2026): resolved VERIFY notes from code — M-TOD delta uses the McBidAsk tier when enabled (`todDelta` → `FootprintBar.Quality`); M-PORT L9 is a paper-account roll check, not a test signal; AV-06/TS-05: `_AvwapManager` has no public custom-anchor call, so event-bar AVWAPs are strategy-computed; AV-06 event table covers 2016–2027 (PR #27).
- Analyst 2 review edits (23 Oct 2026; these edits came from Analyst 2's review): M-EVENT rows gain EV-4.6b fail-closed coverage (80 GDP/PCE holes 2016-24 block about 15-18% of 2016-22 days under the Macro mask, `allowPartialCoverage=false`) and every M-EVENT idea (A2-59, PS-04, PS-05, AV-06, TS-01, TS-05, RG-07, A2-35, A2-51, DV-01, DV-02) gets the caveat "GDP/PCE coverage holes 2016-24 (EV-4.6b)"; A2-25/26/27/28/30/31/32 and A2-35 stay buildable now but are run-gated by the A2-33 verdict (A2-29 unchanged); "needs ETH bars" added to PS-04, PS-05, TS-01, TS-05, A2-35; PS-04 → buildable with caveats; DV-01/DV-02 gated by the A2-33 verdict, DV-02 also needs the DV-01/03/04 level generators (scheduled news breaks only; unscheduled subset untestable); AV-06 data line notes the GDP/PCE holes; §2.4/§2.4a gain Gate0 ✓ (PR #20) and `_RegimeEngine` ◐ (PR #23); `_RegimeGate` (PR #26) recorded under A2-63 / RG-03, no row. Summary now 10 available / 6 partial / 2 missing over 18 rows.
- Cost-floor wording (23 Oct 2026, decided by Ettienne): §2.1 reworded so it is unambiguous: stop distance = max(structural stop, cost floor); a trade is skipped only if the widened stop breaches the per-trade risk budget; size is never shrunk. No rule or number changed.
- Floor-rule risk budget (23 Oct 2026, decided by Ettienne): §2.1 per-trade risk budget for the cost-floor skip is ≤ 1% of equity; PF-04's 0.5% is not the default for this rule.
 (Appendix A.1).
**v2.4 (2026-10-31, implementation-status re-audit against `origin/main` @ `3f9e9be`, PRs #30–#58)**
- New §2.4c per-entry status table (Implemented 6 / Partial 16 / Spec'd 12 / Not started 159) and an **Implementation status (v2.4)** line under every `####` entry; header Status line updated.
- §2.4 / §2.4a: M-DVP ✗ → ◐ (PR-D0…D8: [#32](https://github.com/ettiennehugo/MultiCharts.Net/pull/32), [#33](https://github.com/ettiennehugo/MultiCharts.Net/pull/33), [#34](https://github.com/ettiennehugo/MultiCharts.Net/pull/34), [#36](https://github.com/ettiennehugo/MultiCharts.Net/pull/36), [#37](https://github.com/ettiennehugo/MultiCharts.Net/pull/37), [#39](https://github.com/ettiennehugo/MultiCharts.Net/pull/39), [#40](https://github.com/ettiennehugo/MultiCharts.Net/pull/40), [#41](https://github.com/ettiennehugo/MultiCharts.Net/pull/41), [#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58); #35 merged into `feat/dvp-d1-fit`, delivered via #36); new M-INSTR row ◐ (probe only, [#38](https://github.com/ettiennehugo/MultiCharts.Net/pull/38)); notes on Gate0 ([#30](https://github.com/ettiennehugo/MultiCharts.Net/pull/30), [#43](https://github.com/ettiennehugo/MultiCharts.Net/pull/43), [#44](https://github.com/ettiennehugo/MultiCharts.Net/pull/44), [#45](https://github.com/ettiennehugo/MultiCharts.Net/pull/45), [#58](https://github.com/ettiennehugo/MultiCharts.Net/pull/58)), F-CONF ([#47](https://github.com/ettiennehugo/MultiCharts.Net/pull/47), behaviour change), F-OF ([#48](https://github.com/ettiennehugo/MultiCharts.Net/pull/48)), F-JRNL ([#49](https://github.com/ettiennehugo/MultiCharts.Net/pull/49)), M-EVENT ([#31](https://github.com/ettiennehugo/MultiCharts.Net/pull/31), [#50](https://github.com/ettiennehugo/MultiCharts.Net/pull/50)), F-VWAP ([#52](https://github.com/ettiennehugo/MultiCharts.Net/pull/52), [#54](https://github.com/ettiennehugo/MultiCharts.Net/pull/54)), F-ROLL ([#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46), [#51](https://github.com/ettiennehugo/MultiCharts.Net/pull/51), [#55](https://github.com/ettiennehugo/MultiCharts.Net/pull/55), [#57](https://github.com/ettiennehugo/MultiCharts.Net/pull/57)), `_RegimeEngine` ([#46](https://github.com/ettiennehugo/MultiCharts.Net/pull/46)), A2-33 harness ([#42](https://github.com/ettiennehugo/MultiCharts.Net/pull/42)). Summary now 10 available / 8 partial / 1 deferred over 19 rows. Docs: engines manual v4.1 is canonical at `docs/engines-manual.md` ([#53](https://github.com/ettiennehugo/MultiCharts.Net/pull/53), [#56](https://github.com/ettiennehugo/MultiCharts.Net/pull/56)).
- §2.4b status, M-DVP Needs/Readiness lines of PS-25, MT-07, RG-09, RG-10, KD-02, KD-06…KD-11; §5.3 prerequisites; §8 rows 3d and 5.
- Merge-audit fixes (`catalogue-merge-audit-analyst2.md`) re-verified: all already applied in v2 (see "Merge fixes" above); none missing, nothing re-applied.
- IDs, grades, rules and matrix scores unchanged.

**v2.5 (2026-10-31, new families VX and LE from R7 `volatility-leveraged-etf-research.md`)**
- New §3.17 VX-01…VX-08 (volatility) and §3.18 LE-01…LE-07 (leveraged ETFs), each with **Implementation status (v2.5): Not started**; Appendix A.7.
- Header and Status line (208 entries; 174 not started); §2.4 / §2.4a rows ✗ M-VXTS and ✗ M-ETF (21 rows: 10 available, 8 partial, 1 deferred, 2 missing); §2.4c counts and Not-started lists; §3 counts (VX 8, LE 7; matrix 138 rows).
- §4.2: 13 [est] rows (footnote ²; VX-05, VX-07 not scored); §4.1 v2.5 note (ranks unchanged pending confirmation). §8 rows 12–14. §10 v2.5 references.
- Existing IDs, grades, rules and matrix scores unchanged. Backup: `/tmp/cat-backup/pre-v2.5.md` / `.pdf`.

**v2.6 (2026-11-01, YouTube video ideas)**
- New §3.19 YV-01…YV-10 from 8 YouTube videos (4 NEW: YV-02, YV-03, YV-07, YV-09; 6 VARIANTs: YV-01, YV-04, YV-05, YV-06, YV-08, YV-10), each with **Implementation status (v2.6): Not started**, plus new **Timeframes**, **Feasibility · costs** and **What to look for** (frame references) fields; Appendix A.8 (incl. existing-only ideas).
- Header and Status line (218 entries; 184 not started); §2.4c counts and Not-started list; §3 counts (YV 10; matrix 146 rows).
- §4.2: 8 [est] rows (footnote ³; YV-05, YV-06 not scored); §4.1 v2.6 note (ranks unchanged).
- Existing IDs, grades, rules and matrix scores unchanged. Backup: `/tmp/cat-backup-2026-11-01/`. Write-up: `specs/video-ideas-2026-11-01-additions.md` (+ PDF).

**v2.6a (2026-11-01, per-contract costs confirmed by Ettienne)**
- Costs per contract per round trip: **ES $17**, **MES $2.50** (MES = $0.25 IBKR commission + $0.35 CME exchange fee + ≈$0.02 regulatory fee per side, plus one $1.25 tick of slippage = 2 MES ticks). §2.1 cost floor line updated (was MES $2.47, ES $16.98); floors unchanged (MES 20, ES 14 ticks). Other roots keep the unconfirmed R6 figures.
- §3.19 YV: removed the statement that MES-only intraday execution is cost-dead (it assumed $17 per MES contract = 13.6 MES ticks); every YV costs line now gives ES $17 and MES $2.50 (2 MES ticks; ≈ 0.14 R at a 14-tick MES stop, 0.10 R at 20 ticks; ≥ $2k equity per MES contract at 1%). ES share of a floored 1R corrected from ≈ 9% to ≈ 9.7%.
- TS-01 kill line $2.47 → $2.50 per MES; A2-73 breakeven-plus-costs action annotated with ES/MES costs. No IDs, grades, rules or matrix scores changed. Backup: `/tmp/cost-backup-2026-11-01/`.


**v2.7 (2026-11-08, three video specifications)**
- New entries, all **Implementation status (v2.7): Not started**: **PF-08** (Donchian / Turtle daily sleeve, companion to PF-01; §3.11), **SP-09** (clock-window equal-liquidity sweep into an HTF FVG; §3.6), **OF-09** (weekly bias → 30m value-area break → 1m failed-seller; §3.5), **OF-10** (ES vs NQ order-flow selection layer; not buildable until a second footprint exists), **EX-13** (1.5R partial, deep-pullback breakeven, initiative trail; module on OF-09). Write-up: `specs/video-ideas-2026-11-08-additions.md`. Appendix A.9.
- Header, §2.4c counts (223 entries; 189 not started), §2.6 note, §3 counts (OF 10, SP 9, EX 13, PF 8; matrix 150 rows). §4.1 / §4.2: four [est] rows (footnote ⁴); ranks unchanged. EX-13 unscored.
- Cross-references only, no rule changes, on PF-01, DV-16, PS-18, DV-08, OF-01, OF-02, the A2-71 block inside PF-06, and YV-01, YV-02, YV-07, YV-09. Appendix A.8 related-ID cells for those four YV rows gain the new IDs.
- No existing grade, rule, sweep or matrix score was edited. No performance number was computed.

- No rules of v1's own ideas were changed, apart from the A2-restorations listed above.
- There is still no trapped-trader section (deferred). Trapped-trader ideas stay where they were.
