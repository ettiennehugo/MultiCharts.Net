# Sandbox coding plan

**Date:** 15 Nov 2026. **Branch:** `cursor/sandbox-coding-plan-adb0`, from `origin/main` at the PR 124 merge. **Do not edit** the ten sandbox files that PR 126 (`cursor/sandbox-extend`) changes.

Analyst's count is 291 distinct IDs. This file lists all 291. The count by family matches that list: A2 79, PS 26, DV 24, EX 13, AV 13, YV 12, KD 11, OF 10, RG 10, SP 9, PF 8, VX 8, VW 8, TS 7, L2 7, MT 7, LE 7, VPF 6, VP 6, SW 6, CS 5, VPB 5, IX 3, EV 1.

Ten signals are already in `sandbox/` from PR 124. PR 126 on `cursor/sandbox-extend` adds arms to those same ten files. This plan does not edit them.

`_GenericExit` and `exitPlan` are specified in `specs/generic-exit-manager-spec.md` and are not in the build. There is no `GxMcCompile` project in the repo. New files keep the worked example's own exit orders. A compile check lives in `tests/SandboxCompile`. It parses every sandbox strategy as C# 7.3 and fails on a language-version or syntax error. MultiCharts reference assemblies are not in this environment, so the check does not link `PowerLanguage.dll`. Native F7 on the MiniPC is still required. The check does not add a `Compile Include` for `sandbox/`, so RepoLint stays green.

## (b) Exits, regime filters, sizing

**EX.** These are modules, not standalone entries. Where GX already has the shape, the later batch sets that GX input and does not reimplement the exit. GX is not edited here.

| ID | GX | What this plan does |
|---|---|---|
| EX-06 | Time stop is in the GX spec (`TimeStopBars`). The "< +0.5 R" condition is not a GX input. | Host passes the condition until GX grows a stagnation predicate. |
| EX-07 | Partial at 1 R and break-even match the GX T1 and break-even shapes. | Use those settings when GX exists. |
| EX-08 | Fixed-R targets match GX target modes. One run per `b`. | Not one file that sweeps `b` inside a trade. |
| EX-01, EX-02, EX-03 | GX can hold a price the host hands it. It does not know the next HVN, a naked POC, or balance width. | The host computes the price. GX does not gain a profile lookup. |
| EX-04, EX-05 | AVWAP trail and last-swing trail are not GX trail modes. | Host updates the stop. Listed as not expressible inside GX alone. |
| EX-09, EX-10 | Stagnation and acceptance-failure are not GX modes. | Host exits. |
| EX-11, EX-12 | Flow trail and anchored-CVD divergence need order flow. EX-12 is tick-window only. | Not expressible in GX. EX-12 stays off unless flow quality is at least the tick tier. |
| EX-13 | The 1.5 R partial and the break-even are close to GX. The initiative-print trail is not. | Partial and break-even through GX later. Trail stays in the host. |

**RG.** Each RG idea becomes a sandbox filter the entry calls, plus an enum or bool on the entry (`RgFilter`, default off so the entry's trade list does not change). The filter class lives in the sandbox file or a sandbox function whose class name is the file stem. `_RegimeEngine` and `_RegimeGate` stay as they are. RG-03 is already the ATR-tercile arm on `_A2_63_RegimeGate`.

**PF and PS sizing.** Portfolio ideas become a sandbox note or a sandbox signal that only journals the sleeve weights. They do not add inputs to `_PortMM` or `_PortfolioRiskManager`. PS-26 and the other PS rows are entries unless the row is explicitly a book-level risk rule. A2-40 and A2-59 are cost models, kind `sizing-portfolio`, not new order logic.

**Engine ids.** VPF, VPB, CS, VP, IX and EV are infrastructure names already covered by the engines. They do not get a second strategy file.

## (c) Data we do not have

dxFeed in this project is futures, stocks and ETFs. It is not an index feed. Forex spot and crypto are not assumed. L2 depth is not assumed. Bid/ask order flow exists only where MultiCharts exposes it, inside the tick window.

| Gap | IDs | Substitute used when the batch is written |
|---|---|---|
| No DOM / L2 | L2-01 through L2-07 | None. Kind is `data-unavailable`. No fake book. |
| No VIX or cash index | VX-01 through VX-08, and any row that names SPX | HV20 from M-DAILY on the future being traded. The header says ASSUMPTION. |
| ETF rows | LE-01 through LE-07 | The ETF itself, if it is on the stock/ETF feed. Not a future substitute. |
| Spot FX or crypto only | any row whose only market is not a future or a stock | 6E for a euro FX idea. No crypto substitute. The header says so. |
| Order-flow depth | OF-01, OF-07, OF-09, OF-10, EX-11, EX-12 | Code the rule. If flow quality is only BarUpDown, skip the flow arm and journal the skip. |

## (d) Batches

Batch 1 is the 20 worked examples in the engines manual that are not already in `sandbox/` and that do not subclass another example. They are the most fully specified code in the repo. Later batches follow A2, then AV, VW, DV, TS and SW, then the other entry families, then EX, RG and PF, then L2. Each later batch is its own branch and its own draft PR. Child examples wait until the parent file exists: MT-03 subclasses PS-08, RG-07 subclasses the A2-56 example (that file is frozen for PR 126, so RG-07 will subclass `_A2_56_FirstHourBreak` without editing it), A2-78 subclasses OF-07, A2-25 subclasses the DV-02 example.

The counts are in the table at the end of this file.

## (e) Open questions, and the default used here

1. **GX is not in the build.** Default: do not call `exitPlan`. Keep the worked example's orders. Do not add `_GenericExit` or edit the spec's engine.
2. **Merged ids.** Default: one file. A2-06 is the VW-05 arm, not a second file. A2-03 is EX-04. A2-22 is EX-12. DV-03 is the PS-13 file.
3. **A2-63, VW-08, RG-03.** Default: no second sandbox copy. They already exist on main.
4. **MT-02 `AcceptanceDays` was an int 1 or 2.** Default: enum `EAcceptanceDays` with default `Two`, which is the catalogue default of 2. The trade rule is unchanged.
5. **IOG.** Default: `[IOGMode(IOGMode.Disabled)]` on every new file. The original ten are not edited to add it.
6. **VX without a VIX series.** Default: HV20 on the traded future, marked ASSUMPTION.
7. **L2.** Default: do not invent depth.
8. **Compile.** Default: `tests/SandboxCompile` is the C# 7.3 syntax gate. Native compile is F7 after a slot copy. No `csproj` line points at `sandbox/`.
9. **Holdout.** Default: no result from 2025 onward is read or written.
10. **Ambiguous catalogue rows with no heading.** Default: the batch that reaches them codes the sentence in the source that cites the id, and marks ASSUMPTION in the header. They are not skipped for lack of a heading.

## (a) ID table

| ID | Title | Source | Kind | Target file | Batch | Status |
|---|---|---|---|---|---|---|
| A2-01 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_01_A201.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-02 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_A2_02_A202.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_03_A203.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-04 | Overnight inventory vs oVWAP open drive | trading-idea-catalogue.md | entry signal | sandbox/_A2_04_OvernightInventoryVs.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-05 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_05_A205.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-06 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_06_A206.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-07 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_07_A207.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-08 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_08_A208.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-09 | Noise-band long-only with sVWAP trail (MNQ benchmark) | trading-idea-catalogue.md | entry signal | sandbox/_A2_09_NoiseBandLong.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-10 | (no heading; id is cited in the source) | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_10_A210.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-11 | Prior-day VWAP static-level acceptance | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_11_PriorDayVWAP.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-12 | VWAP − TWAP divergence as an inventory signal | trading-idea-catalogue.md | entry signal | sandbox/_A2_12_VWAPTWAPDivergence.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-13 | Gap-day AVWAP-from-open: hold → go-with, fail → fade-to-value | trading-idea-catalogue.md | entry signal | sandbox/_A2_13_GapDayAVWAP.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-14 | Weekly AVWAP reclaim swing (MES daily) | trading-idea-catalogue.md | entry signal | sandbox/_A2_14_WeeklyAVWAPReclaim.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-15 | Breakout-leg POC vs leg AVWAP skew (layer on PS-01) | trading-idea-catalogue.md | entry signal | sandbox/_A2_15_BreakoutLegPOC.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-16 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_16_A216.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-17 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_17_A217.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-18 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_18_A218.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-19 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_19_A219.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-20 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_20_A220.Strategy.CS (name fixed when the batch is written) | 2 | not coded |
| A2-21 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_21_A221.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-22 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_22_A222.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-23 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_23_A223.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-24 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_24_A224.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-25 | POC bootstrap-stability gate and minimum-stop rule (module) | keller-b5-catalogue-additions.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_25_POCBootstrapStability.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-26 | KDE bandwidth-sweep veto on FLAT/BIMODAL POCs (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_26_KDEBandwidthSweep.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-27 | Bimodal-profile void traverse | trading-idea-catalogue.md | entry signal | sandbox/_A2_27_BimodalProfileVoid.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-28 | Minor-component excess rejection (confirmation, not touch) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_28_MinorComponentExcess.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-29 | Entropy day-type gate at 11:00 (module) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_29_EntropyDayType.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-30 | Rising-entropy balance break (leading signal) | trading-idea-catalogue.md | entry signal | sandbox/_A2_30_RisingEntropyBalance.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-31 | KL-divergence acceptance/migration gate for prior-day levels (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_31_KLDivergenceAcceptance.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-32 | Volatility-normalised (σ-space) mixture composite levels | keller-b5-catalogue-additions.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_32_VolatilityNormalisedSpace.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-33 | Level-persistence vs matched-control study (gate for every level idea) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none until a later batch | done | level study is _LevelStudy. A sandbox host is a later research batch, not batch 1. |
| A2-34 | V/T urgency-price revisit (event study, then target module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_34_VTUrgency.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-35 | Macro-event re-formation value area (CPI/NFP 08:30, FOMC 14:00) | trading-idea-catalogue.md | entry signal | sandbox/_A2_35_MacroEventRe.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-36 | Dealer-gamma regime filter (module; data-blocked) | trading-idea-catalogue.md | entry signal | sandbox/_A2_36_DealerGammaRegime.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-37 | Cross-instrument dislocation fragility filter (module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_A2_37_CrossInstrumentDislocation.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-38 | Metaorder drift completion (tick window only) | trading-idea-catalogue.md | entry signal | sandbox/_A2_38_MetaorderDriftCompletion.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-39 | Roll-honest composites on unadjusted @ES (infrastructure) | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | duplicate-of | none | done | roll-reset arm exists in the engines. Cash and back-adjusted arms are not coded. |
| A2-40 | Honest cost model and stressed-correlation sizing (Gate 0) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | sizing-portfolio | sandbox/_A2_40_HonestCostModel.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-41 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_41_A241.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-42 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_42_A242.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-43 | Confluent-HVN level gate (target and veto module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_43_ConfluentHVNLevel.Strategy.CS (name fixed when the batch is written) | 3 | not coded |
| A2-44 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_44_A244.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-45 | Single-print shelf as destination (near-edge target, runner to far edge) | trading-idea-catalogue.md | entry signal | sandbox/_A2_45_SinglePrintShelf.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-46 | Volume-gap commitment entry to the far node | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_46_VolumeGapCommitment.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-47 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_47_A247.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-48 | Poor highs/lows as destinations (exit and stop-placement module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_48_PoorHighsLows.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-49 | Midday support/resistance and afternoon breakout (Kaufman TSM) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_49_MiddayBreakout.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-50 | Same-slot relative-volume opening regime gate (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_50_OpeningRvGate.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-51 | Intraday shock-bar fade with delayed stop and volume-decay exit | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_51_ShockBarFade.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-52 | Time-confirmed opening-range break with pivot-range bias (Fisher ACD) | trading-idea-catalogue.md | entry signal | sandbox/_A2_52_TimeConfirmedOpening.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-53 | RTH gap fade with stop-and-reverse at the day's extreme (negative canary) | trading-idea-catalogue.md | entry signal | sandbox/_A2_53_RTHGapFade.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-54 | Up-gap pullback-to-prior-close short, exit next open | trading-idea-catalogue.md | entry signal | sandbox/_A2_54_UpGapPullback.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-55 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_A2_55_A255.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-56 | Momentum Pinball: daily LBR/RSI setup, first-hour break, conditional overnight hold | trading-idea-catalogue.md | entry signal | sandbox/_A2_56_FirstHourBreak.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-57 | Higher-TF turn plus first short-TF oversold entry (Kaufman/Elder) | trading-idea-catalogue.md | entry signal | sandbox/_A2_57_TwoTimeframeTurn.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-58 | Fade the fast trend inside the slow trend, intraday | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_58_FastInSlowFade.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-59 | Kaufman cost, 'unables' and shock-audit module (Gate 0, extends A2-40) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | sizing-portfolio | sandbox/_A2_59_KaufmanCostUnables.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-60 | Meyers normalised range-ratio adaptive breakout (5m) | trading-idea-catalogue.md | entry signal | sandbox/_A2_60_MeyersNormalisedRange.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-61 | Efficiency-ratio session router (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_61_EfficiencyRatioSession.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-62 | Taylor 3-day cycle 'low-first' buying day | trading-idea-catalogue.md | entry signal | sandbox/_A2_62_Taylor3Day.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-63 | Long-only open + 0.5 × daily ATR breakout, exit at the close (host H2) | trading-idea-catalogue.md | duplicate-of | _A2_63_RegimeGate.Strategy.CS | done | already on main as _A2_63_RegimeGate.Strategy.CS. No second sandbox copy in batch 1. |
| A2-64 | 1.0 × daily ATR fade from the open or prior close | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_70_OpenFadeHvGate.Strategy.CS (shared with A2-70) | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-65 | Raschke '1st cross' oscillator/trend cross with bar confirmation | trading-idea-catalogue.md | entry signal | sandbox/_A2_65_Raschke1stCross.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-66 | Raschke/Kaufman '2nd cross' second stochastic pullback | trading-idea-catalogue.md | entry signal | sandbox/_A2_66_RaschkeKaufman2nd.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-67 | Intraday RSI-of-highs/lows divergence long | trading-idea-catalogue.md | entry signal | sandbox/_A2_67_IntradayRSIOf.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-68 | 5-bar run + doji reversal, target 50% of the run | trading-idea-catalogue.md | entry signal | sandbox/_A2_68_X5BarRun.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-69 | Kaufman 3-day trade: two down closes, buy a lower open, exit next close | trading-idea-catalogue.md | entry signal | sandbox/_A2_69_KaufmanThreeDay.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-70 | High-volatility skip gate (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_70_OpenFadeHvGate.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-71 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_A2_71_A271.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-72 | Progressive exposure ladder 25/50/100% (sizing module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_72_ProgressiveExposureLadder.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-73 | Breakeven stop at ≈ 3R or above the rolling average gain (exit module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_73_BreakevenStopAt.Strategy.CS (name fixed when the batch is written) | 4 | not coded |
| A2-74 | Free roll: sell half at 2–3R, remainder at breakeven (scale-out module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_74_FreeRollSell.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| A2-75 | Rolling hit-rate throttle (RBA module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_75_RollingHitRate.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| A2-76 | Sell into the climax bar (profit-exit module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_76_SellIntoThe.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| A2-77 | Failed follow-through exit: time stop and bar-quality test (exit module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_77_FailedFollowThrough.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| A2-78 | Daily loss kill-switch and rules-first flatten (risk module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_78_DailyLossKill.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| A2-79 | Intraday volatility contraction + volume dry-up entry gate (VCP analogue; module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_79_IntradayVolatilityContraction.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-01 | Swing-high AVWAP reclaim (Shannon "buy strength after the pullback") | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_AV_01_SwingHighAVWAP.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-02 | Impulse-origin AVWAP higher-low continuation | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_AV_02_ImpulseOriginAVWAP.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-03 | AVWAP pinch breakout (Shannon) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_AV_03_AVWAPPinchBreakout.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-04 | AVWAP handoff continuation (Shannon) | trading-idea-catalogue.md | entry signal | sandbox/_AV_04_AVWAPHandoffContinuation.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-05 | Gap-anchored AVWAP swing hold | trading-idea-catalogue.md | entry signal | sandbox/_AV_05_GapAnchoredAVWAP.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-06 | Event-bar AVWAP (FOMC / CPI / NFP) acceptance | trading-idea-catalogue.md | entry signal | sandbox/_AV_06_EventBarAVWAP.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-07 | Climax-volume-bar AVWAP | trading-idea-catalogue.md | entry signal | sandbox/_AV_07_ClimaxVolumeBar.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-08 | Week-open / month-open / quarter-open AVWAP as regime layer | trading-idea-catalogue.md | entry signal | sandbox/_AV_08_PeriodAvwapLayer.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| AV-09 | Roll-anchored AVWAP swing bias | trading-idea-catalogue.md | entry signal | sandbox/_AV_09_RollAnchoredAVWAP.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-10 | AVWAP × profile confluence (does stacking help?) | trading-idea-catalogue.md | entry signal | sandbox/_AV_10_AVWAPProfileConfluence.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-11 | Breakout-bar AVWAP first pullback | trading-idea-catalogue.md | entry signal | sandbox/_AV_11_BreakAvwapPullback.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| AV-12 | Failed AVWAP reclaim → return to the opposing AVWAP | trading-idea-catalogue.md | entry signal | sandbox/_AV_12_FailedAVWAPReclaim.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| AV-13 | AVWAP σ-stretch exit / scale signal | trading-idea-catalogue.md | entry signal | sandbox/_AV_13_AVWAPStretchExit.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| VW-01 | Trend-day first VWAP pullback | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_VW_01_TrendDayFirst.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| VW-02 | Balance-day ±2σ fade at a composite HVN edge (conditional) | trading-idea-catalogue.md | entry signal | sandbox/_VW_02_BalanceDay2.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| VW-03 | σ-band expansion continuation | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_VW_03_BandExpansionContinuation.Strategy.CS (name fixed when the batch is written) | 5 | not coded |
| VW-04 | Failed VWAP defence → reversal context | trading-idea-catalogue.md | entry signal | sandbox/_VW_04_FailedVWAPDefence.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| VW-05 | Late-day continuation: VWAP side × rest-of-day return | trading-idea-catalogue.md | entry signal | sandbox/_VW_05_LateDayVwap.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| VW-06 | POC–VWAP skew pullback | trading-idea-catalogue.md | entry signal | sandbox/_VW_06_POCVWAPSkew.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| VW-07 | Prior-day VWAP and ETH VWAP as opening reference (filter test) | trading-idea-catalogue.md | entry signal | sandbox/_VW_07_PriorDayVWAP.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| VW-08 | VWAP-cross "Holy Grail" (canary) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | duplicate-of | _TestGate0 canary VW08 | done | already on main as _TestGate0 canary VW08. No second sandbox copy in batch 1. |
| DV-01 | Daily POC first-touch pullback ("The Simplest Volume Profile Intraday Strategy for Beginners") | trading-idea-catalogue.md | entry signal | sandbox/_DV_01_DailyPOCFirst.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-02 | Failed level → reversal (flip) trade | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_DV_02_FailedLevelReversal.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | sandbox/_PS_13_RotationVolumeLevel.Strategy.CS | done | same worked example as PS-13. Do not write a second file. |
| DV-04 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_04_DV04.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-05 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_05_DV05.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-06 | End-of-trend-day cluster pullback (into the NY close) | trading-idea-catalogue.md | entry signal | sandbox/_DV_06_EndOfTrend.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-07 | Rotation / double-distribution edge → centre | trading-idea-catalogue.md | entry signal | sandbox/_DV_07_RotationDoubleDistribution.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-08 | Heavy-volume zone + Fair Value Gap alignment (level at the FVG start) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_08_HeavyVolumeZone.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-09 | Weekly-VWAP ±1σ trend pullback | trading-idea-catalogue.md | entry signal | sandbox/_DV_09_WeeklyVWAP1.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-10 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_DV_10_DV10.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-11 | VWAP trend-failure (lose σ1 → pullback to σ1 → target VWAP) | trading-idea-catalogue.md | entry signal | sandbox/_DV_11_VWAPTrendFailure.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-12 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_12_DV12.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-13 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_13_DV13.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-14 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_14_DV14.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-15 | EMA20 pullback gated by VWAP slope | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_DV_15_EmaVwapPullback.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| DV-16 | Order-flow confirmation at a pre-defined level (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_16_OrderFlowConfirmation.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-17 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_17_DV17.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-18 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_18_DV18.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-19 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_19_DV19.Strategy.CS (name fixed when the batch is written) | 6 | not coded |
| DV-20 | Stop-loss-cluster (weak low/high) veto | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_20_StopLossCluster.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| DV-21 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_21_DV21.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| DV-22 | ABCD (AB = CD) | trading-idea-catalogue.md | entry signal | sandbox/_DV_22_ABCDABCD.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| DV-23 | Initial-balance draw trade (guest: Dan McKen, Funded Trader Academy, not Dale) | trading-idea-catalogue.md | entry signal | sandbox/_DV_23_InitialBalanceDraw.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| DV-24 | (no heading; id is cited in the source) | trading-idea-catalogue.md | entry signal | sandbox/_DV_24_DV24.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| TS-01 | European-open overnight drift (long MES 01:30–04:30 ET) | trading-idea-catalogue.md | entry signal | sandbox/_TS_01_EuropeanOpenDrift.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| TS-02 | London-open break of the Asia range (CL/GC) with an Asia AVWAP filter | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_TS_02_LondonOpenBreak.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| TS-03 | Close regime: 15:30–16:00 MOC drift then reversion | trading-idea-catalogue.md | entry signal | sandbox/_TS_03_CloseDrift.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| TS-04 | CL/GC pit-session profile (port of the PS ideas) | trading-idea-catalogue.md | entry signal | sandbox/_TS_04_CLGCPit.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| TS-05 | ZN post-release AVWAP acceptance (08:30 data) | trading-idea-catalogue.md | entry signal | sandbox/_TS_05_ZNPostRelease.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| TS-06 | Overnight inventory correction at the open | trading-idea-catalogue.md | entry signal | sandbox/_TS_06_OvernightInventoryCorrection.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| TS-07 | Opening-noise observation window (filter test) | trading-idea-catalogue.md | entry signal | sandbox/_TS_07_OpeningNoiseObservation.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| SW-01 | Weekly balance break (3–6-week composite) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_SW_01_WeeklyBalanceBreak.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| SW-02 | RSI(2) dip-buy with a profile/AVWAP location filter | trading-idea-catalogue.md | entry signal | sandbox/_SW_02_SwingPullback.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| SW-03 | Quarterly/monthly AVWAP swing pullback | trading-idea-catalogue.md | entry signal | sandbox/_SW_03_QuarterlyMonthlyAVWAP.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| SW-04 | Multi-day P-profile hold (Levy "will it move within 3 days?") | trading-idea-catalogue.md | entry signal | sandbox/_SW_04_MultiDayP.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| SW-05 | Composite gap-and-go (out of a 20-day composite) | trading-idea-catalogue.md | entry signal | sandbox/_SW_05_CompositeGapAnd.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| SW-06 | Weekly IB (Mon–Tue range) breakout with acceptance | trading-idea-catalogue.md | entry signal | sandbox/_SW_06_WeeklyIBMon.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-01 | Accepted balance break → first-pullback higher low (ABB-HL) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_01_BalanceBreakPullback.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-02 | Balance break at acceptance close (K4 end-to-end spec) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_PS_02_BalanceBreakClose.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-03 | Confirmed failed break of value → rotation to the opposite edge | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_03_ConfirmedFailedBreak.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-04 | Prior-value magnet on a small newsless gap (Keller IX-1) | trading-idea-catalogue.md | entry signal | sandbox/_PS_04_PriorValueMagnet.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-05 | Rejected repricing open (Keller IX-3 · Spec 3) | trading-idea-catalogue.md | entry signal | sandbox/_PS_05_RejectedRepricingOpen.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-06 | Defeated adjustment → continuation (Keller IX-2) | trading-idea-catalogue.md | entry signal | sandbox/_PS_06_DefeatedAdjustmentContinuation.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-07 | Open-drive / one-way open: first-pullback continuation | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_07_OpenDrive.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-08 | IB extension with acceptance (narrow-IB tier) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_PS_08_NarrowIbBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-09 | IB C-period failure → IB midpoint | trading-idea-catalogue.md | entry signal | sandbox/_PS_09_IBCPeriod.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-10 | Developing-POC migration join (VAH-as-support pullback) | trading-idea-catalogue.md | entry signal | sandbox/_PS_10_DevelopingPOCMigration.Strategy.CS (name fixed when the batch is written) | 7 | not coded |
| PS-11 | Unratified new extreme (POC refuses to follow) → confirmed fade | trading-idea-catalogue.md | entry signal | sandbox/_PS_11_UnratifiedNewExtreme.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-12 | Trend-cluster pullback (first touch of an in-trend volume cluster) | trading-idea-catalogue.md | entry signal | sandbox/_PS_12_TrendClusterPullback.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-13 | Volume-accumulation breakout: first-touch retest | trading-idea-catalogue.md | entry signal | sandbox/_PS_13_RotationVolumeLevel.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-14 | Rejection-ledge revisit fade | trading-idea-catalogue.md | entry signal | sandbox/_PS_14_RejectionLedgeRevisit.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-15 | LVN traverse after acceptance into the void | trading-idea-catalogue.md | entry signal | sandbox/_PS_15_LVNTraverseAfter.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-16 | LVN slow-approach bounce | trading-idea-catalogue.md | entry signal | sandbox/_PS_16_LVNSlowApproach.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-17 | Double-distribution waist trade | trading-idea-catalogue.md | entry signal | sandbox/_PS_17_DoubleDistributionWaist.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-18 | P/b-profile stop-sweep buy (Levy) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_18_PBProfile.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-19 | Poor high/low run-through (not repair-as-magnet) | trading-idea-catalogue.md | entry signal | sandbox/_PS_19_PoorHighRun.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-20 | Single-print (initiative tail) pullback continuation | trading-idea-catalogue.md | entry signal | sandbox/_PS_20_SinglePrintInitiative.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-21 | Naked POC / naked VAH–VAL first touch, with a confirming trigger | trading-idea-catalogue.md | entry signal | sandbox/_PS_21_NakedLevelReaction.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-22 | Composite LVN-edge rejection vs HVN-ceiling veto | trading-idea-catalogue.md | entry signal | sandbox/_PS_22_LvnEdgeRejection.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-23 | Profile shape at the close → next-session tilt | trading-idea-catalogue.md | entry signal | sandbox/_PS_23_ProfileShapeAt.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-24 | Overnight high/low break-and-go at RTH | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_24_OvernightBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-25 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_PS_25_PS25.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| PS-26 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_PS_26_PS26.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-01 | Stacked-imbalance origin: first retest continuation | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_OF_01_StackedImbalanceRetest.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| OF-02 | Delta-z ratification of the breakout bar (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_02_DeltaZRatification.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-03 | Anchored CVD flip at a failed break (layer) | trading-idea-catalogue.md | entry signal | sandbox/_OF_03_AnchoredCVDFlip.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-04 | Absorption at an aged HTF level (control) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_04_AbsorptionAtAn.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-05 | CVD divergence at a value extreme | trading-idea-catalogue.md | entry signal | sandbox/_OF_05_CVDDivergenceAt.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-06 | Unfinished-auction veto (layer) | trading-idea-catalogue.md | entry signal | sandbox/_OF_06_UnfinishedAuctionVeto.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-07 | Bar-level burst at a fresh extreme (layer / standalone) | trading-idea-catalogue.md | entry signal | sandbox/_OF_07_BurstExtremeBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| OF-08 | Large-trade print zone retest | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_08_LargeTradePrint.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-09 | Weekly bias, 30m value-area break, 1m failed-seller (order-flow cascade) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_09_WeeklyBias30m.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| OF-10 | ES vs NQ relative order-flow selection (layer on OF-09) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_10_ESVsNQ.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| MT-01 | HTF-aligned higher low at value (60m → 15m → 5m → 1m nesting) | trading-idea-catalogue.md | entry signal | sandbox/_MT_01_MultiTfStack.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| MT-02 | Weekly accepted value + 60m trigger (swing) | trading-idea-catalogue.md | entry signal | sandbox/_MT_02_WeeklyValueAcceptance.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| MT-03 | 20-day composite location gate + 5m acceptance break | trading-idea-catalogue.md | entry signal | sandbox/_MT_03_X20DayComposite.Strategy.CS (name fixed when the batch is written) | 8 | not coded |
| MT-04 | 60m swing-AVWAP + 5m HL entry | trading-idea-catalogue.md | entry signal | sandbox/_MT_04_X60mSwingAVWAP.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| MT-05 | 240m multi-session LVN as pullback zone | trading-idea-catalogue.md | entry signal | sandbox/_MT_05_X240mMultiSession.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| MT-06 | Daily failed auction (excess / poor extreme) + intraday trigger | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_MT_06_DailyFailedAuction.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| MT-07 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_MT_07_MT07.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-01 | Failed double top → upside breakout (T4) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_SP_01_FailedDoubleTop.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-02 | Double top at VAH with second-test divergence (T2, control) | trading-idea-catalogue.md | entry signal | sandbox/_SP_02_DoubleTopAt.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-03 | Wyckoff spring at composite VAL with a test (T3) | trading-idea-catalogue.md | entry signal | sandbox/_SP_03_WyckoffSpringAt.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-04 | Lower high after a failed auction at a composite extreme (T6) | trading-idea-catalogue.md | entry signal | sandbox/_SP_04_LowerHighAfter.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-05 | Four-part reversal-bar event at a profile extreme | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_05_FourPartReversal.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-06 | First higher low after a downtrend at composite VAL (change of character) | trading-idea-catalogue.md | entry signal | sandbox/_SP_06_FirstHigherLow.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-07 | Failed-failure re-break (Spec 4) | trading-idea-catalogue.md | entry signal | sandbox/_SP_07_FailedFailureRe.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-08 | Point-of-Origin retest (Levy) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_08_PointOfOrigin.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| SP-09 | Clock-window equal-liquidity sweep into an HTF fair-value gap (ICT "macro" scalp) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_09_ClockWindowEqual.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-01 | Accept-beyond-rail continuation (detrended rails, rails as targets) — **primary** | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_01_AcceptBeyondRail.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-02 | Band-stretch fade to the POC rail — control / canary (G0-b) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_02_BandStretchFade.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-03 | Three-family confluence filter (module, not an entry) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_03_ThreeFamilyConfluence.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-04 | Detrended-fit regime gate: degree-1 slope with band-break refit (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_04_DetrendedFitRegime.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-05 | HTF detrended-rail alignment (15m entries on 60m/240m accepted rails) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_05_HTFDetrendedRail.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-06 | σ-band acceptance breakout (PS-25 robustness arm "σ2") | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_06_BandAcceptanceBreakout.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-07 | No-fade veto (gate form of KD-02) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_07_NoFadeVeto.Strategy.CS (name fixed when the batch is written) | 9 | not coded |
| KD-08 | Forward-cone diagnostic (study, not a trade) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_08_ForwardConeDiagnostic.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| KD-09 | Family-count confluence filter (rev 3.1 module form of RG-09 / KD-03) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_09_FamilyCountConfluence.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| KD-10 | Far-outside / band-edge node: acceptance only (PS-25 robustness arm "node") | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_10_FarOutsideBand.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| KD-11 | Beyond-value node reach-rate study (Field Drill 7.5; diagnostic) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_11_BeyondValueNode.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-01 | 30m balance/VA break → 5m big-trade confirmation → 1m failed-seller pullback entry (Trader Oxygen) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_01_X30mBalanceVA.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-02 | Cross-index flow selection: execute in whichever of NQ/ES shows the stronger confirming order flow (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_02_CrossIndexFlow.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-03 | Momentum burst (Pradeep Bonde): 4% range-expansion day out of an orderly base (stocks) | trading-idea-catalogue.md | entry signal | sandbox/_YV_03_MomentumBurstPradeep.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-04 | Environment → location → confirmation: discount pullback in value-up structure with absorption and a second seller failure (Robbins Cup order flow) | trading-idea-catalogue.md | entry signal | sandbox/_YV_04_EnvironmentLocationConfirmation.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-05 | Absolute participation gate: trade only while 5m volume ≥ a fixed contract count (MNQ 20,000/5m) | trading-idea-catalogue.md | entry signal | sandbox/_YV_05_AbsoluteParticipationGate.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-06 | Session stop after 2 losses / 1–2 attempts per setup per day, inside a fixed early window | trading-idea-catalogue.md | entry signal | sandbox/_YV_06_SessionStopAfter.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-07 | ICT macro-window scalp: 5m FVG traded into inside xx:50–xx:10, 15s sweep of equal highs + new FVG → target equal lows | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_07_ICTMacroWindow.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-08 | Daily sweep strategy: 1h SFP of a prior-day/Asia/pre-NY swing → 5m FVG entry, candle-2 stop, 1:3 or opposing liquidity | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_08_DailySweepStrategy.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-09 | Turtle Donchian breakout: System 1 (20-day high/low, 10-day exit) and System 2 (55-day, 20-day exit); EMA20-close exit arm | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_09_TurtleDonchianBreakout.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-10 | Labelled Markov regime: 20-day-return states (±5%), 3×3 transition matrix, signal = P(bull) − P(bear) for tomorrow, HMM agreement | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_10_LabelledMarkovRegime.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-11 | 9 EMA / 20 SMA zone pullback in a sloping trend, with an over-extension veto (Malyarovich) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_11_X9EMA20.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| YV-12 | Prior-day high/low range rejection on a "Day 2" session: wick candle → other side of the range (SMB Market Memory Model, setups 1–2) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_12_PriorDayHigh.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| VX-01 | Hedged VX term-structure carry (Simon–Campasano roll rule) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_01_HedgedVXTerm.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| VX-02 | VRP-filtered short volatility (Cooper VRP rule, futures form) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_02_VRPFilteredShort.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| VX-03 | Long volatility in backwardation (VX-01's long leg alone) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_03_LongVolatilityIn.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| VX-04 | Post-spike normalisation short (VIX spike mean reversion) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_04_PostSpikeNormalisation.Strategy.CS (name fixed when the batch is written) | 10 | not coded |
| VX-05 | Long-volatility tail-hedge sleeve (portfolio insurance) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_05_LongVolatilityTail.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| VX-06 | VIX term-structure regime gate for ES hosts (layer) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_06_VIXTermStructure.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| VX-07 | VVIX gate (module) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_07_VVIXGateModule.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| VX-08 | VIX-stretch ES dip-buy (Connors & Alvarez) — layer on SW-02 / canary | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_08_VIXStretchES.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-01 | 200-day trend leverage rotation (Gayed & Bilello) | trading-idea-catalogue.md | entry signal | sandbox/_LE_01_X200DayTrend.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-02 | Volatility-targeted leverage (exposure ∝ target vol / realised vol) | trading-idea-catalogue.md | entry signal | sandbox/_LE_02_VolatilityTargetedLeverage.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-03 | QQQ ↔ TQQQ regime leverage rotation | trading-idea-catalogue.md | entry signal | sandbox/_LE_03_QQQTQQQRegime.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-04 | LETF rebalancing pressure into the close (flow-scaled arm of VW-05) | trading-idea-catalogue.md | entry signal | sandbox/_LE_04_LETFRebalancingPressure.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-05 | Decay-pair short (short TQQQ + SQQQ, or UPRO + SPXU) | trading-idea-catalogue.md | entry signal | sandbox/_LE_05_DecayPairShort.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-06 | Short-term RSI mean reversion on LETFs (Connors-style) | trading-idea-catalogue.md | entry signal | sandbox/_LE_06_ShortTermRSI.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| LE-07 | Inverse-LETF bear-trend hold (SQQQ/SPXU below SMA200) — negative control | trading-idea-catalogue.md | entry signal | sandbox/_LE_07_InverseLETFBear.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-01 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_01_EX01.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-02 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_02_EX02.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_03_EX03.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-04 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_04_EX04.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-05 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | exit | sandbox/_EX_05_EX05.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-06 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_06_EX06.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-07 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_07_EX07.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-08 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_08_EX08.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-09 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_09_EX09.Strategy.CS (name fixed when the batch is written) | 11 | not coded |
| EX-10 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_10_EX10.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| EX-11 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_11_EX11.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| EX-12 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_12_EX12.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| EX-13 | 1.5R partial, deep-pullback breakeven, trail under the initiative print (module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | exit | sandbox/_EX_13_X15RPartial.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-01 | Value-migration gate (V-score) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_01_ValueMigrationGate.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-02 | Balance/imbalance classifier + entropy skip | trading-idea-catalogue.md | regime-filter | sandbox/_RG_02_BalanceImbalanceClassifier.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-03 | Volatility regime stratifier | trading-idea-catalogue.md | duplicate-of | _RegimeEngine AtrTercile / _A2_63_RegimeGate arm A | done | already on main as _RegimeEngine AtrTercile / _A2_63_RegimeGate arm A. No second sandbox copy in batch 1. |
| RG-04 | Live day-type proxy at 10:30 (includes VWAP-cross count) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_04_LiveDayType.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-05 | Noise-band filter (Zarattini) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_05_NoiseBandFilter.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-06 | HTF trend state (TSMOM + 60m structure) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | regime-filter | sandbox/_RG_06_HTFTrendState.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-07 | Event, roll and thin-session exclusions | trading-idea-catalogue.md | regime-filter | sandbox/_RG_07_EventRollAnd.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-08 | Compression state (range/ATR) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_08_CompressionStateRange.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-09 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | regime-filter | sandbox/_RG_09_RG09.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| RG-10 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | regime-filter | sandbox/_RG_10_RG10.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-01 | Trend + carry core (parallel track) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_01_TrendCarryCore.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-02 | Regime-complementary intraday sleeves | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_02_RegimeComplementaryIntraday.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-03 | Cross-market diversification & hidden-beta monitor | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_03_CrossMarketDiversification.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-04 | Risk budgeting (deflated quarter-Kelly, capped) | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_04_RiskBudgetingDeflated.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-05 | Drawdown throttles + two-CUSUM monitoring | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_05_DrawdownThrottlesTwo.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-06 | Portfolio volatility targeting | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_06_PortfolioVolatilityTargeting.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-07 | Meta-labelling the survivors (later) | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_07_MetaLabellingThe.Strategy.CS (name fixed when the batch is written) | 12 | not coded |
| PF-08 | Donchian / Turtle daily trend sleeve (companion to PF-01) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_08_DonchianTurtleDaily.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-01 | Cancellation veto before level entries | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_01_CancellationVetoBefore.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-02 | Book-shape regime | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_02_BookShapeRegime.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-03 | Resilience half-life T½ prior to range breaks | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_03_ResilienceHalfLife.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-04 | Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_04_IcebergVsGenuine.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-05 | Pre-event depth withdrawal forecasts the spike's revert fraction | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_05_PreEventDepth.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-06 | Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1) | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_06_QueueDepthRule.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| L2-07 | Two-sided cancellation surge = fragility alarm | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_07_TwoSidedCancellation.Strategy.CS (name fixed when the batch is written) | 13 | not coded |
| VPF-2 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPF-3 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPF-4 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPF-5 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPF-6 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPF-7 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-1 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-2 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-3 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-4 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-5 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VP-6 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| CS-1 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| CS-3 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| CS-4 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| CS-7 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| CS-8 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPB-1 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPB-4 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPB-6 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPB-7 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| VPB-8 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| IX-1 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| IX-2 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| IX-3 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |
| EV-4 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none | done | engine or catalogue infrastructure id, not a new sandbox strategy |

## Batch counts

| Batch | IDs | What |
|---|---|---|
| done | 43 | Already in sandbox, already on main as a production study, or not a strategy file (engine ids). |
| 1 | 20 | A2-29, A2-50, A2-51, A2-57, A2-58, AV-11, PS-01, PS-02, PS-05, PS-07, PS-08, PS-13, PS-19, PS-21, PS-22, PS-24, OF-01, OF-07, MT-01, MT-02 |
| 2 | 20 | A2-01, A2-02, A2-03, A2-04, A2-05, A2-06, A2-07, A2-08, A2-09, A2-10, A2-11, A2-12, A2-13, A2-14, A2-15, A2-16, A2-17, A2-18, A2-19, A2-20 |
| 3 | 20 | A2-21, A2-22, A2-23, A2-24, A2-25, A2-26, A2-27, A2-28, A2-30, A2-31, A2-32, A2-34, A2-35, A2-36, A2-37, A2-38, A2-40, A2-41, A2-42, A2-43 |
| 4 | 20 | A2-44, A2-45, A2-46, A2-47, A2-48, A2-52, A2-53, A2-54, A2-55, A2-59, A2-60, A2-61, A2-62, A2-65, A2-66, A2-67, A2-68, A2-71, A2-72, A2-73 |
| 5 | 20 | A2-74, A2-75, A2-76, A2-77, A2-78, A2-79, AV-01, AV-02, AV-03, AV-04, AV-05, AV-06, AV-07, AV-09, AV-10, AV-12, AV-13, VW-01, VW-02, VW-03 |
| 6 | 20 | VW-04, VW-06, VW-07, DV-01, DV-02, DV-04, DV-05, DV-06, DV-07, DV-08, DV-09, DV-10, DV-11, DV-12, DV-13, DV-14, DV-16, DV-17, DV-18, DV-19 |
| 7 | 20 | DV-20, DV-21, DV-22, DV-23, DV-24, TS-02, TS-04, TS-05, TS-06, TS-07, SW-01, SW-03, SW-04, SW-05, SW-06, PS-03, PS-04, PS-06, PS-09, PS-10 |
| 8 | 20 | PS-11, PS-12, PS-14, PS-15, PS-16, PS-17, PS-18, PS-20, PS-23, PS-25, PS-26, OF-02, OF-03, OF-04, OF-05, OF-06, OF-08, OF-09, OF-10, MT-03 |
| 9 | 20 | MT-04, MT-05, MT-06, MT-07, SP-01, SP-02, SP-03, SP-04, SP-05, SP-06, SP-07, SP-08, SP-09, KD-01, KD-02, KD-03, KD-04, KD-05, KD-06, KD-07 |
| 10 | 20 | KD-08, KD-09, KD-10, KD-11, YV-01, YV-02, YV-03, YV-04, YV-05, YV-06, YV-07, YV-08, YV-09, YV-10, YV-11, YV-12, VX-01, VX-02, VX-03, VX-04 |
| 11 | 20 | VX-05, VX-06, VX-07, VX-08, LE-01, LE-02, LE-03, LE-04, LE-05, LE-06, LE-07, EX-01, EX-02, EX-03, EX-04, EX-05, EX-06, EX-07, EX-08, EX-09 |
| 12 | 20 | EX-10, EX-11, EX-12, EX-13, RG-01, RG-02, RG-04, RG-05, RG-06, RG-07, RG-08, RG-09, RG-10, PF-01, PF-02, PF-03, PF-04, PF-05, PF-06, PF-07 |
| 13 | 8 | PF-08, L2-01, L2-02, L2-03, L2-04, L2-05, L2-06, L2-07 |
