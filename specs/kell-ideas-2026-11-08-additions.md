# Oliver Kell ideas 2026-11-08 — catalogue additions (catalogue v2.8)

**For:** Ettienne Hugo · **Prepared by:** Grok Bot · **Date:** Monday 9 Nov 2026 (America/New_York, EST)

**What this is.** A documentation pass only. Family **OK** (Oliver Kell, Cycle of Price Action) is added to `specs/trading-idea-catalogue.md` as **v2.8**, section 3.20, IDs **OK-01…OK-24**, each with implementation status **Not started**. No strategy code. No project file. The field-complete entries live in §3.20. This file is the provenance, the constraints, the alias map, the build order, and the judgement calls. If a sentence here and a sentence in §3.20 ever disagree, **§3.20 wins**.

**Evidence.** Grade **E**. Sources are Kell speaking on two videos, a third-party presenter on a third video, and a secondary rulebook that is not authenticated Kell text. There is no independent controlled sample. CME futures are untested. Nothing in this pass was run. Source win-rate language is recorded once, below, as a conflict. It is **not** a design target and **not** a forecast.

## ID check

The draft prefix `OK` was provisional. It was checked against catalogue v2.7. Families already present: PS, VW, AV, MT, OF, SP, TS, SW, RG, EX, PF, A2, DV, L2, KD, VX, LE, YV. No `OK-` ID existed. The prefix is **kept**. OK-01…OK-24 are the 24 entries (223 + 24 = 247). Presenter variants are not extra entries.

| ID | What it is | Not an entry |
|---|---|---|
| OK-05P, OK-22P | Same presenter rule: trail each bar's low once price is extended | Optional, off, not a baseline |
| OK-P21EMA | One sensitivity, 21-EMA instead of 20-EMA | Not a setup |
| OK-P-synchronicity | Presenter's name for an ignite bar | Alias only |
| OK-P-PAR | Presenter said "I use PAR" | Undefined. Not codified |

## Owner constraints (on every OK ID)

These are visible in the §3.20 preamble and override Kell where they conflict.

1. **Data.** Evaluation and validation use bars only through **2024-12-31**. From **2025-01-01** through the catalogue's Oct 2026 end is unviewed holdout. No OK host and no refinement arm is designed, tuned, or kill-read on 2025–2026.
2. **Owner gates, after costs.** No single trade may exceed the user-set share of total profit. The largest winner is at most **10%** of net (this cap still binds if the user-set share is looser). The result stays **net positive after the top 1% of trades are removed**. A book that needs a handful of giant winners **fails**. That is a kill, not a reason to relax a gate. Kell's "few winners carry the year" language loses this argument.
3. **Ablations.** One arm at a time. At most two thresholds and three values per layer. At most 30 configs on W-M or W-D. At most four on W-T.
4. **Sizing words stay separate.** Allocation % is the share of equity Kell's slides assign to a name (25–35, 15–20, 12–15, 10–12, 7–8). Price-stop % is how far the stop sits. Account-risk % is roughly allocation times price-stop, before gaps and costs. The presenter says the top-idea allocation is not account risk. **None of the slide tiers is a default.** Margin tiers are **not enabled**. Tests use the program default: at most 1% of equity per new risk unit (A2-71). PF-04 is unchanged. OK-24 only records the slides.
5. **Volume is not order flow.** A bull snort is heavy **total** volume. No L2 is assumed. Footprint, delta, and aggressor tools are research arms.
6. **No lookahead.** Confirmed pivots only after the confirmation bar. Completed profiles only. Closed higher-timeframe bars only. No fill that needs the same bar's final volume and close.
7. **Futures scale.** Buffers of 0.5–1 stock point, and extensions of "10+ points" or a raw percent of price, are source text. ES and NQ use ATR or tick versions. The stock-point numbers are not coded on futures.

## Provenance

| Tag | Meaning |
|---|---|
| **K** | Kell speaking (wedge-pop video; ten-principles video) or a slide those videos show |
| **P** | Third-party presenter of the long breakout video, including paraphrase |
| **S** | Secondary rulebook only. Videos win on a conflict |
| **R** | Research-authored so the rule can be coded. Not Kell |

| Claim | Tag |
|---|---|
| Cycle: reversal extension, wedge pop, EMA crossback, base n' break, exhaustion, wedge drop | K, also paraphrased by P and restated by S |
| Bull snort = heavy total volume | K |
| Ignite bar, stop to its low | K. The rulebook disagrees on small-range versus wide-range. The joint definition is R |
| Wick play, and "not a random wick" | K |
| Intraday pivot points, VWAP, and an intraday 20-EMA as **chart tools** | K (ten principles, about 70:22–70:28). **No trigger** |
| Any VWAP slope, reclaim, distance gate, or anchored VWAP | **R**. The chart mention is not a trigger |
| Volume profile, footprint, delta, Fibonacci | Absent from Kell's materials and from the rulebook. Every such arm is **R** |
| Allocation tiers and the 50/25/25 build | K. Documentation only |
| "Allocation is not account risk" | P |
| Parabolic low-of-day trail | **P only** (about 94:11–96:01). The presenter calls it their own spin |
| Win rate about 30–35% | P, about Kell. Unsupported |
| Per-setup win rates about 60–75% | S. Unsupported. Conflicts with the presenter figure |
| Few winners dominate the year | Presentation theme. Conflicts with the owner gates. Gates win |

## Alias map (existing entries were not rewritten)

A single cross-reference line was added on each named entry. Rules on those entries are unchanged.

| OK ID | Closest existing | Why it stays separate |
|---|---|---|
| OK-01 | SP-05, SP-06, PS-18 | Higher-timeframe support and extension below the 10-EMA. Not a profile extreme |
| OK-02 | A2-79, YV-03 | First reclaim of a tight 10/20 after a downcycle. A2-79 may later gate it |
| OK-03 | DV-15 | DV-15 **requires** a VWAP slope. The OK-03 baseline has none. A later VWAP arm is R |
| OK-04 | A2-79, YV-03, SP-01 | Stage host. SP-01 is a failed double top, a different pattern |
| OK-05, OK-20 | A2-76, EX family | Exhaustion trim and an ignite-bar stop. A2-76 sells a climax |
| OK-09, OK-10 | SP-03, PS-18 | Higher-low double bottom versus undercut-and-reclaim. Not a value-area spring |
| OK-11, OK-15 | A2-79 | Flat base and inside-bar trigger. Not the VCP gate |
| OK-17 | SP-05 | Outside bar or 20-EMA shakeout. Not the four-part profile event |
| OK-21, OK-22 | EX-05, A2-73, A2-74 | Pivot trail and EMA trail. EX-05 stays the 5-minute higher-low trail |
| OK-23 | RG-06, YV-03 | 10/20 EMA gate on Kell hosts. RG-06's evidence does not transfer |
| OK-24 | A2-72, A2-71, PF-04 | Records Kell's tiers. Does not replace those sizers |

**Aliases with no new ID.** Basin Break = base n' break. EMA cross back = crossback. Ignite = ignition. Scum = shakeout reclaim. Darvish = Darvas (transcript error). Presenter 21-EMA = Kell 20-EMA, as a sensitivity only. An extra inside bar on a host is OK-15. A naked earnings-gap buy is an arm of OK-07. A weekly 10-EMA trim is an arm of OK-22.

## Research arms (required, and not Kell)

On each host, after the baseline, one at a time:

1. **VP (R).** A completed VAH, VAL, POC, HVN, or LVN within δ of the Kell level. No developing-bar lookahead. No L2.
2. **VWAP (R).** Session VWAP, or an event-anchored VWAP, by slope or reclaim. Not the unnamed chart tool.
3. **OF (R, W-T only).** Affirming bar delta or cumulative delta, or a failed opposing stack, at MC-BidAsk quality. Skip otherwise. Total volume does not qualify.
4. **Fib (R).** Confirmed-pivot 0.382 / 0.5 / 0.618 on the EMA or the pivot. Kell has no Fib rule.

Tier 1 of the later planning table stays the existing pure VP / VWAP / OF / Fib ideas. There is no pure-Kell tier-1 row. Tier 2 is a host plus exactly one of these arms. Tier 3 is the plain baseline. That tiering is a filing rule. It is not a forecast. The Strategy-planning page was **not** edited.

## Build order

Not a matrix score. The §4 matrix stays at 150 rows; these 24 IDs are unscored on purpose, so no evidence or frequency number was invented for them. Source ranks overlap between hosts and modules. The sequence is the order.

1. OK-23 regime gate (ES/NQ proxy: the traded root's own daily EMAs).
2. OK-22, OK-20, OK-21 on a stub host. Owner gates on.
3. OK-02 baseline, ES/NQ, through 2024-12-31.
4. OK-02 plus VP alone, then VWAP alone, then OF alone (OF is W-T).
5. OK-03 baseline, then its VWAP arm. A comparison with DV-15 comes later and is not a merge.
6. OK-04 with the OK-12 flag arm.
7. OK-16, then OK-15 as a layer, then OK-17, OK-01, OK-10.
8. Stock-native OK-07 and OK-08 only after a point-in-time earnings universe exists. Still below ES/NQ.
9. OK-05, OK-06, OK-18 last.

OK-24 is not in the code sequence.

## Discrepancies (kept visible, not resolved by picking a winner in secret)

1. **Win rates.** The presenter says about 30–35% overall. The rulebook says about 60–75% on setups, and about 70% on some of them. Both are unsupported. They conflict. They are not targets and not forecasts.
2. **Double bottom versus 2B.** The rulebook uses both names for a higher low and also describes a true undercut. Split: OK-09 higher low, OK-10 undercut then reclaim.
3. **Ignite bar.** Small-and-heavy versus wide-and-heavy. The baseline requires both a wide range and heavy volume (R). Dropping one condition is a sensitivity, not a second baseline.
4. **Base target.** 2× height, versus 1.5–2×, versus the 50-SMA first. Primary exit is OK-22. The projections are separate arms.
5. **Flag and pennant targets.** Flagpole versus flag height, and the pennant's ambiguous projection. Same treatment: arms, not a silent pick.
6. **Which EMA to trail.** Chosen at entry by a written rule (the average that held the last pullback; 20 if both did). Not chosen after the result.
7. **Checklist sign error.** "10-EMA below 20-EMA" as an uptrend is not implemented. Uptrend means the 10 is above the 20.
8. **Stock points.** 0.5–1 point and "10+ points" are not ES/NQ rules. ATR and ticks are mandatory on those rows.
9. **Rulebook versus video.** The rulebook is secondary. A video wins the provenance tag. Rulebook-only numbers stay S.
10. **Who is speaking.** The long breakout video is mostly a third-party presenter. The low-of-day trail is that presenter's spin and is excluded from the baseline.
11. **VWAP, profile, flow, Fib.** Kell's only VWAP remark is the chart-tool sentence. The rulebook has no VWAP. No profile, footprint, or Fib rule appears in the materials used here.
12. **Concentration.** Kell's materials celebrate large winners. The owner gates override that.
13. **Transcript errors.** Darvish, Basin Break, and "synchronicity" are corrected as alias, alias, and alias. "I use PAR" is not corrected into a rule, because it was never defined.
14. **Catalogue drift in the draft.** The draft was written against a v2.6a text plus a v2.7 inventory. This pass applied the entries to the actual v2.7 file (PF-08, SP-09, OF-09, OF-10, EX-13 already present). No ID collision resulted.

## Judgement calls

1. **Prefix kept.** `OK` was free. No rename.
2. **24 entries, not 27.** OK-05P, OK-22P, and the 21-EMA sensitivity sit inside the hosts. Counting them would have inflated the 247.
3. **OK-05 has no Kell short.** Stop and target for a short were not in the sources. None was written. An optional research short would need its own pre-registration later.
4. **Conflicting targets were not picked.** Where the rulebook offers two formulas, the primary cell exits with OK-22 and the formulas are arms.
5. **A few R numbers were frozen so the first cell is codeable.** They are labelled R, not K: extension k = 1.5 (arm 2.5); tight-band percentile 40 (arm 25); ignite-bar range 1.5 ATR; futures EMA buffer max(4 ticks, 0.25 ATR); channel "parallel" means slopes within 10%; wick at least twice the body; OK-23 on futures uses the traded root, because a cash Nasdaq index is not in the futures set; stock hosts would use QQQ. Change any of these only as a pre-registered arm.
6. **OK-07's earnings row is data-blocked.** The ES/NQ "catalyst gap plus flag" is labelled R and is not described as earnings. It still ranks below the ES/NQ cycle hosts.
7. **OK-11's 10% range stays on stocks.** Futures use an ATR multiple. A 10% ES range is a different object.
8. **OK-17 is two arms.** Outside bar, and 20-EMA shakeout. They are not AND-ed.
9. **OK-23 is a gate.** It is not scored as its own strategy. RG-06's drift warning still applies: "long ES above the 10 and 20" can be the index itself.
10. **OK-24 cannot be turned on.** The 25–35% tiers and the 120–150% margin book are written down so they are not copied into a sizer by accident.
11. **Matrix left at 150 rows.** Assigning Ev / frequency / priority numbers to 24 untested anecdotes would have looked like a ranking. The build order above is the ranking that was actually supplied. It is not a forecast.
12. **Owner gates are kill lines, not expected returns.** "Profit factor ≤ 1 after costs" and "beat the named control" are hurdles. No Sharpe, win rate, or annual return was set as a goal.
13. **Cross-references are one line each.** PS-18, SP-01, SP-03, SP-05, SP-06, RG-06, EX-05, PF-04, A2-71 (inside PF-06), A2-72, A2-73, A2-74, A2-76, A2-79, DV-15, YV-03. Appendix A.8 gained OK IDs on the YV-03 row and the weekly-EMA row only.

## What the draft contained that did not become an entry

- **Strategy-planning page rows.** The draft's section 7 said to append OK rows to that page later, and also said not to edit the page in the draft itself. This repository has no Strategy-planning page. It was not created and not edited. The tier rule is recorded here and in §3.20 instead.
- **"I use PAR."** Undefined. Not codified. Listed above so it is not forgotten.
- **Fundamental screens** (sales growth, EPS surprise, theme, beta, price, share volume). No point-in-time feed. Not setups. Not given IDs. Mentioned in the §3.20 preamble.
- **Emotion and discretion checks.** Not executable. Not given IDs.
- **Rulebook arithmetic examples** (illustrative monthly or yearly figures, an 8–9% benchmark). Not entered. Not treated as evidence or as a forecast.
- **A Kell short for exhaustion.** Incomplete in the sources. Not invented. See judgement call 3.

## Entry index

Full rules, sweeps, and kill lines: catalogue §3.20. Preferred arm means the first research ablation, and it is R.

| ID | Name | Kind | Preferred arm |
|---|---|---|---|
| OK-01 | Reversal extension | host | VP |
| OK-02 | Wedge pop | host | VP |
| OK-03 | EMA crossback | host | VWAP |
| OK-04 | Base n' break | host | VP |
| OK-05 | Exhaustion extension | exit module | VWAP |
| OK-06 | Wedge drop | short host | OF |
| OK-07 | Post-EPS flag | host (EPS row data-blocked) | VWAP |
| OK-08 | Cup and handle | host | VP |
| OK-09 | Double bottom, higher low | host | VP |
| OK-10 | Pivot-low failure / 2B | host | OF |
| OK-11 | Flat base | pattern arm | VP |
| OK-12 | Bull flag | pattern arm | VWAP |
| OK-13 | Bull pennant | pattern arm | VP |
| OK-14 | Descending channel break | host | VP |
| OK-15 | Inside bars | trigger layer | OF |
| OK-16 | Wick play | host | VP |
| OK-17 | Outside reversal / scum | host, two arms | OF |
| OK-18 | TTFTL short | short host | VP |
| OK-19 | Magic line | host | Fib |
| OK-20 | Ignite-bar stop raise | exit module | optional OF |
| OK-21 | Darvas / pivot trail | exit module | none first |
| OK-22 | EMA trail and scale-out | exit module | none first |
| OK-23 | Index 10/20 EMA gate | regime module | none |
| OK-24 | Allocation documentation | not a sizer | none |
