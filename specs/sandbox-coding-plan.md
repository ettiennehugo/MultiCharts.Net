# Sandbox coding plan

**Date:** 15 Nov 2026. **Branch:** `cursor/sandbox-coding-plan-adb0`, from `origin/main` at the PR 124 merge. **Do not edit** the ten sandbox files that PR 126 (`cursor/sandbox-extend`) changes.

## Owner decisions (2026-11-15 20:06 ET)

1. Flat-by is 15:55 in the exchange zone (`_SandboxRules.Flat1555`). An earlier catalogue clock is Eastern and is converted by `_SandboxRules.FlatBy`. NYMEX and COMEX stay Eastern, matching the GX zone map. That is 15:55 ET there, not Central.
2. RG gates and the GX scale exits are default-off inputs on `_PS_01_BalanceBreakPullback` (`EHostGate.Off`, `EHostExit.Host`). The RG and EX files still journal the rule and do not enter.
3. `RollDetection` is turned off in `StartCalc` when the instrument is not a future.
4. Data1 is 1-minute. Data2 is the idea's native timeframe, declared on the `// Chart:` line. A file that emulates a higher bar requires Data2 and logs an Error when it is missing.
5. A priced entry is resent while its signal is still true. One fill per session.
6. A new swing anchor, and a new session, clears the once-only latch. That is one trade per anchor per day.
7. `ESandboxSide` exists for a host that codes both sides. The default is Both.
8. VX files do not trade. They log an Error that VIX data is unavailable. HV20 is not a substitute.
9. CL, GC, ZN and 6E use the pit table in `_SandboxRules.ApplyPit` when the chart name matches. Other symbols keep the base session.
10. A host exit of Partial1R or Partial15R uses 2 contracts, logs a Warning, and logs an Error if that risk exceeds 1 percent of equity.
11. A2-53 keeps a plain protective stop. The catalogue note says the sandbox does not stop-and-reverse.
12. PS-01 sends the protective stop on the same bar as the entry stop.

Deleted, because the catalogue merges them or says they do not exist: DV-04, DV-05, DV-10, DV-12, DV-13, DV-14, DV-17, DV-18, DV-19, DV-21, DV-24, PS-26. Their hosts are PS-12, PS-14, VW-08, AV-02, AV-11, AV-08, OF-08, OF-05, OF-06, DV-02, EX-01. There is no PS-26.

Analyst's count is 291 distinct IDs. This file lists all 291. The count by family matches that list: A2 79, PS 26, DV 24, EX 13, AV 13, YV 12, KD 11, OF 10, RG 10, SP 9, PF 8, VX 8, VW 8, TS 7, L2 7, MT 7, LE 7, VPF 6, VP 6, SW 6, CS 5, VPB 5, IX 3, EV 1.

Ten signals are already in `sandbox/` from PR 124. PR 126 on `cursor/sandbox-extend` adds arms to those same ten files. This plan does not edit them.

`_GenericExit` is on the GX branch (PRs 120, 122, 125), not on this branch. New files keep their own exit orders, and those orders are sent only from `sendExits`. `tests/SandboxCompile` does a semantic C# 7.3 compile of every sandbox strategy together with `_CustomSignal`, the volume-profile bases, and the engines, against MC stubs. The `TradeManager` using is aliased the same way as `tests/GxMcCompile`. MultiCharts reference assemblies are not in this environment. Native F7 on the MiniPC is still required. The check copies sources into `tests/SandboxCompile/Generated` and does not add a `Compile Include` for `sandbox/`, so RepoLint stays green.

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
| No VIX or cash index | VX-01 through VX-08, and any row that names SPX | No trade. The file logs an Error that VIX data is unavailable. HV20 is not a substitute. |
| ETF rows | LE-01 through LE-07 | The ETF itself, if it is on the stock/ETF feed. Not a future substitute. |
| Spot FX or crypto only | any row whose only market is not a future or a stock | 6E for a euro FX idea. No crypto substitute. The header says so. |
| Order-flow depth | OF-01, OF-07, OF-09, OF-10, EX-11, EX-12 | Code the rule. If flow quality is only BarUpDown, skip the flow arm and journal the skip. |

## (d) Batches

Batch 1 is the 20 worked examples in the engines manual that are not already in `sandbox/` and that do not subclass another example. They are the most fully specified code in the repo. Later batches follow A2, then AV, VW, DV, TS and SW, then the other entry families, then EX, RG and PF, then L2. Each later batch is its own branch and its own draft PR. Child examples wait until the parent file exists: MT-03 subclasses PS-08, RG-07 subclasses the A2-56 example (that file is frozen for PR 126, so RG-07 will subclass `_A2_56_FirstHourBreak` without editing it), A2-78 subclasses OF-07, A2-25 subclasses the DV-02 example.

The counts are in the table at the end of this file.

## (e) Open questions, and the default used here

1. **GX exit seam.** Main at 9107598 contains GX. From batch 3 on, a new entry whose exit is a stop, a point or R target, and a flat time calls `exitPlan` and `exitBar`. An exit GX cannot express keeps `sendExits`, with the reason in the header. Batches 1 and 2 still use `sendExits` until a later conversion commit. The ten PR 126 files are not edited.
2. **Merged ids.** Default: one file. A2-06 is the VW-05 arm, not a second file. A2-03 is EX-04. A2-22 is EX-12. DV-03 is the PS-13 file.
3. **A2-63, VW-08, RG-03.** Default: no second sandbox copy. They already exist on main.
4. **MT-02 `AcceptanceDays` was an int 1 or 2.** Default: enum `EAcceptanceDays` with default `Two`, which is the catalogue default of 2. The trade rule is unchanged.
5. **IOG.** Default: `[IOGMode(IOGMode.Disabled)]` on every new file. The original ten are not edited to add it.
6. **VX without a VIX series.** Default: HV20 on the traded future, marked ASSUMPTION.
7. **L2.** Default: do not invent depth.
8. **Compile.** Default: `tests/SandboxCompile` is a semantic C# 7.3 compile of every sandbox strategy with `_CustomSignal`, the volume-profile bases, the engines, and MC stubs. The TradeManager using is aliased the same way as `tests/GxMcCompile`. Native F7 after a slot copy is still required. No `csproj` line points at `sandbox/`.
9. **Holdout.** Default: no result from 2025 onward is read or written.
10. **Ambiguous catalogue rows with no heading.** Default: the batch that reaches them codes the sentence in the source that cites the id, and marks ASSUMPTION in the header. They are not skipped for lack of a heading.
11. **Child examples.** Default: MT-03 extends `_PS_08_NarrowIbBreak`, A2-78 extends `_OF_07_BurstExtremeBreak`, RG-07 extends `_A2_56_FirstHourBreak` (that file is not edited), and A2-25 extends `_DV_02_FailedLevelFlip`. A slot copy of the child compiles only when the parent file is in the same project.
12. **A2-26.** Default: it is the KDE veto inside the A2-25 file. No second file.
13. **Merged A2 rows.** Default: the catalogue mapping table wins. A row marked "Merged into" is not a second sandbox file. The host carries the arm when that host is coded. A2-06 and A2-07 stay on the PR 126 files, which this work does not edit.
14. **Catalogue-only batch 2 rules.** Default: the ASSUMPTION in each of A2-04, A2-09, A2-11, A2-12, A2-13, A2-14 and A2-15 is the reading that was coded. A2-15 is a gate on PS-01; the base gate returns true.

## (a) ID table

| ID | Title | Source | Kind | Target file | Batch | Status |
|---|---|---|---|---|---|---|
| A2-01 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-04) | done | merged into AV-04; no second sandbox file |
| A2-02 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none (merged into AV-03) | done | merged into AV-03; no second sandbox file |
| A2-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into EX-04) | done | merged into EX-04; no second sandbox file |
| A2-04 | Overnight inventory vs oVWAP open drive | trading-idea-catalogue.md | entry signal | sandbox/_A2_04_OvernightInventory.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-05 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-12) | done | merged into AV-12; no second sandbox file |
| A2-06 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into VW-05) | done | merged into VW-05; no second sandbox file |
| A2-07 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-08) | done | merged into AV-08; no second sandbox file |
| A2-08 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | duplicate-of | none (merged into PS-08) | done | merged into PS-08; no second sandbox file |
| A2-09 | Noise-band long-only with sVWAP trail (MNQ benchmark) | trading-idea-catalogue.md | entry signal | sandbox/_A2_09_NoiseBandLong.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-10 | (no heading; id is cited in the source) | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | duplicate-of | none (merged into VW-03) | done | merged into VW-03; no second sandbox file |
| A2-11 | Prior-day VWAP static-level acceptance | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_11_PriorDayVwap.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-12 | VWAP − TWAP divergence as an inventory signal | trading-idea-catalogue.md | entry signal | sandbox/_A2_12_VwapTwapDivergence.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-13 | Gap-day AVWAP-from-open: hold → go-with, fail → fade-to-value | trading-idea-catalogue.md | entry signal | sandbox/_A2_13_GapAvwapOpen.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-14 | Weekly AVWAP reclaim swing (MES daily) | trading-idea-catalogue.md | entry signal | sandbox/_A2_14_WeeklyAvwapReclaim.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-15 | Breakout-leg POC vs leg AVWAP skew (layer on PS-01) | trading-idea-catalogue.md | entry signal | sandbox/_A2_15_LegSkew.Strategy.CS | 2 | coded in batch 2; ASSUMPTION in the header |
| A2-16 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into RG-04) | done | merged into RG-04; no second sandbox file |
| A2-17 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-07) | done | merged into AV-07; no second sandbox file |
| A2-18 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into PS-10) | done | merged into PS-10; no second sandbox file |
| A2-19 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-13) | done | merged into AV-13; no second sandbox file |
| A2-20 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-06) | done | merged into AV-06; no second sandbox file |
| A2-21 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-06) | done | merged into AV-06; no second sandbox file |
| A2-22 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into EX-12) | done | merged into EX-12; no second sandbox file |
| A2-23 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into AV-09) | done | merged into AV-09; no second sandbox file |
| A2-24 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | duplicate-of | none (merged into VW-08) | done | merged into VW-08; no second sandbox file |
| A2-25 | POC bootstrap-stability gate and minimum-stop rule (module) | keller-b5-catalogue-additions.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_25_PocStabilityGate.Strategy.CS | 2 | coded in batch 2 |
| A2-26 | KDE bandwidth-sweep veto on FLAT/BIMODAL POCs (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | duplicate-of | sandbox/_A2_25_PocStabilityGate.Strategy.CS | 2 | coded inside the A2-25 file (KDE veto); no second file |
| A2-27 | Bimodal-profile void traverse | trading-idea-catalogue.md | entry signal | sandbox/_A2_27_BimodalVoidTraverse.Strategy.CS | 2 | coded in batch 2 |
| A2-28 | Minor-component excess rejection (confirmation, not touch) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_28_MinorExcess.Strategy.CS | 3 | coded in batch 3; GX exitPlan |
| A2-29 | Entropy day-type gate at 11:00 (module) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_29_EntropyDayType.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-30 | Rising-entropy balance break (leading signal) | trading-idea-catalogue.md | entry signal | sandbox/_A2_30_RisingEntropy.Strategy.CS | 3 | coded in batch 3; sendExits, close-back-inside is not a GX trail |
| A2-31 | KL-divergence acceptance/migration gate for prior-day levels (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_31_KlGate.Strategy.CS | 3 | coded in batch 3; classifier, no order |
| A2-32 | Volatility-normalised (σ-space) mixture composite levels | keller-b5-catalogue-additions.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_32_SigmaMixture.Strategy.CS | 3 | coded in batch 3; GX exitPlan |
| A2-33 | Level-persistence vs matched-control study (gate for every level idea) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none until a later batch | done | level study is _LevelStudy. A sandbox host is a later research batch, not batch 1. |
| A2-34 | V/T urgency-price revisit (event study, then target module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_34_VtTarget.Strategy.CS | 3 | coded in batch 3; target module, no order |
| A2-35 | Macro-event re-formation value area (CPI/NFP 08:30, FOMC 14:00) | trading-idea-catalogue.md | entry signal | sandbox/_A2_35_EventReformation.Strategy.CS | 3 | coded in batch 3; GX exitPlan |
| A2-36 | Dealer-gamma regime filter (module; data-blocked) | trading-idea-catalogue.md | data-unavailable | sandbox/_A2_36_GammaFilter.Strategy.CS | 3 | coded in batch 3; data-unavailable, no order |
| A2-37 | Cross-instrument dislocation fragility filter (module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_A2_37_DislocationFilter.Strategy.CS | 3 | coded in batch 3; filter, no order |
| A2-38 | Metaorder drift completion (tick window only) | trading-idea-catalogue.md | data-unavailable | sandbox/_A2_38_MetaorderDrift.Strategy.CS | 3 | coded in batch 3; data-unavailable, no order |
| A2-39 | Roll-honest composites on unadjusted @ES (infrastructure) | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | duplicate-of | none | done | roll-reset arm exists in the engines. Cash and back-adjusted arms are not coded. |
| A2-40 | Honest cost model and stressed-correlation sizing (Gate 0) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | sizing-portfolio | sandbox/_A2_40_CostModel.Strategy.CS | 3 | coded in batch 3; cost note, no fee change, no order |
| A2-41 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into PS-02) | done | merged into PS-02; no second sandbox file |
| A2-42 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into PS-03) | done | merged into PS-03; no second sandbox file |
| A2-43 | Confluent-HVN level gate (target and veto module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_43_ConfluentHvn.Strategy.CS | 3 | coded in batch 3; partial gate, no order |
| A2-44 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into PS-15) | done | merged into PS-15; no second sandbox file |
| A2-45 | Single-print shelf as destination (near-edge target, runner to far edge) | trading-idea-catalogue.md | entry signal | sandbox/_A2_45_SinglePrintShelf.Strategy.CS | 4 | coded in batch 4 |
| A2-46 | Volume-gap commitment entry to the far node | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_46_VolumeGapCommitment.Strategy.CS | 4 | coded in batch 4 |
| A2-47 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into PS-03) | done | merged into PS-03; no second sandbox file |
| A2-48 | Poor highs/lows as destinations (exit and stop-placement module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_48_PoorHighsLows.Strategy.CS | 4 | coded in batch 4 |
| A2-49 | Midday support/resistance and afternoon breakout (Kaufman TSM) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_49_MiddayBreakout.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-50 | Same-slot relative-volume opening regime gate (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_50_OpeningRvGate.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-51 | Intraday shock-bar fade with delayed stop and volume-decay exit | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_51_ShockBarFade.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-52 | Time-confirmed opening-range break with pivot-range bias (Fisher ACD) | trading-idea-catalogue.md | entry signal | sandbox/_A2_52_TimeConfirmedOpening.Strategy.CS | 4 | coded in batch 4 |
| A2-53 | RTH gap fade with stop-and-reverse at the day's extreme (negative canary) | trading-idea-catalogue.md | entry signal | sandbox/_A2_53_RTHGapFade.Strategy.CS | 4 | coded in batch 4 |
| A2-54 | Up-gap pullback-to-prior-close short, exit next open | trading-idea-catalogue.md | entry signal | sandbox/_A2_54_UpGapPullback.Strategy.CS | 4 | coded in batch 4 |
| A2-55 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | none (merged into RG-08) | done | merged into RG-08; no second sandbox file |
| A2-56 | Momentum Pinball: daily LBR/RSI setup, first-hour break, conditional overnight hold | trading-idea-catalogue.md | entry signal | sandbox/_A2_56_FirstHourBreak.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-57 | Higher-TF turn plus first short-TF oversold entry (Kaufman/Elder) | trading-idea-catalogue.md | entry signal | sandbox/_A2_57_TwoTimeframeTurn.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-58 | Fade the fast trend inside the slow trend, intraday | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_58_FastInSlowFade.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| A2-59 | Kaufman cost, 'unables' and shock-audit module (Gate 0, extends A2-40) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | sizing-portfolio | sandbox/_A2_59_KaufmanCostUnables.Strategy.CS | 4 | coded in batch 4 |
| A2-60 | Meyers normalised range-ratio adaptive breakout (5m) | trading-idea-catalogue.md | entry signal | sandbox/_A2_60_MeyersNormalisedRange.Strategy.CS | 4 | coded in batch 4 |
| A2-61 | Efficiency-ratio session router (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_61_EfficiencyRatioSession.Strategy.CS | 4 | coded in batch 4 |
| A2-62 | Taylor 3-day cycle 'low-first' buying day | trading-idea-catalogue.md | entry signal | sandbox/_A2_62_Taylor3Day.Strategy.CS | 4 | coded in batch 4 |
| A2-63 | Long-only open + 0.5 × daily ATR breakout, exit at the close (host H2) | trading-idea-catalogue.md | duplicate-of | _A2_63_RegimeGate.Strategy.CS | done | already on main as _A2_63_RegimeGate.Strategy.CS. No second sandbox copy in batch 1. |
| A2-64 | 1.0 × daily ATR fade from the open or prior close | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md | entry signal | sandbox/_A2_70_OpenFadeHvGate.Strategy.CS (shared with A2-70) | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-65 | Raschke '1st cross' oscillator/trend cross with bar confirmation | trading-idea-catalogue.md | entry signal | sandbox/_A2_65_Raschke1stCross.Strategy.CS | 4 | coded in batch 4 |
| A2-66 | Raschke/Kaufman '2nd cross' second stochastic pullback | trading-idea-catalogue.md | entry signal | sandbox/_A2_66_RaschkeKaufman2nd.Strategy.CS | 4 | coded in batch 4 |
| A2-67 | Intraday RSI-of-highs/lows divergence long | trading-idea-catalogue.md | entry signal | sandbox/_A2_67_IntradayRSIOf.Strategy.CS | 4 | coded in batch 4 |
| A2-68 | 5-bar run + doji reversal, target 50% of the run | trading-idea-catalogue.md | entry signal | sandbox/_A2_68_X5BarRun.Strategy.CS | 4 | coded in batch 4 |
| A2-69 | Kaufman 3-day trade: two down closes, buy a lower open, exit next close | trading-idea-catalogue.md | entry signal | sandbox/_A2_69_KaufmanThreeDay.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-70 | High-volatility skip gate (module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_70_OpenFadeHvGate.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| A2-71 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | duplicate-of | none (merged into PF-06) | done | merged into PF-06; no second sandbox file |
| A2-72 | Progressive exposure ladder 25/50/100% (sizing module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_72_ProgressiveExposureLadder.Strategy.CS | 4 | coded in batch 4 |
| A2-73 | Breakeven stop at ≈ 3R or above the rolling average gain (exit module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_A2_73_BreakevenStopAt.Strategy.CS | 4 | coded in batch 4 |
| A2-74 | Free roll: sell half at 2–3R, remainder at breakeven (scale-out module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_74_FreeRollSell.Strategy.CS | 5 | coded in batch 5 |
| A2-75 | Rolling hit-rate throttle (RBA module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_75_RollingHitRate.Strategy.CS | 5 | coded in batch 5 |
| A2-76 | Sell into the climax bar (profit-exit module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_76_SellIntoThe.Strategy.CS | 5 | coded in batch 5 |
| A2-77 | Failed follow-through exit: time stop and bar-quality test (exit module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_77_FailedFollowThrough.Strategy.CS | 5 | coded in batch 5 |
| A2-78 | Daily loss kill-switch and rules-first flatten (risk module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_78_DailyKillSwitch.Strategy.CS | 2 | coded in batch 2 |
| A2-79 | Intraday volatility contraction + volume dry-up entry gate (VCP analogue; module) | trading-idea-catalogue.md | entry signal | sandbox/_A2_79_IntradayVolatilityContraction.Strategy.CS | 5 | coded in batch 5 |
| AV-01 | Swing-high AVWAP reclaim (Shannon "buy strength after the pullback") | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_AV_01_SwingHighAVWAP.Strategy.CS | 5 | coded in batch 5 |
| AV-02 | Impulse-origin AVWAP higher-low continuation | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_AV_02_ImpulseOriginAVWAP.Strategy.CS | 5 | coded in batch 5 |
| AV-03 | AVWAP pinch breakout (Shannon) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_AV_03_AVWAPPinchBreakout.Strategy.CS | 5 | coded in batch 5 |
| AV-04 | AVWAP handoff continuation (Shannon) | trading-idea-catalogue.md | entry signal | sandbox/_AV_04_AVWAPHandoffContinuation.Strategy.CS | 5 | coded in batch 5 |
| AV-05 | Gap-anchored AVWAP swing hold | trading-idea-catalogue.md | entry signal | sandbox/_AV_05_GapAnchoredAVWAP.Strategy.CS | 5 | coded in batch 5 |
| AV-06 | Event-bar AVWAP (FOMC / CPI / NFP) acceptance | trading-idea-catalogue.md | entry signal | sandbox/_AV_06_EventBarAVWAP.Strategy.CS | 5 | coded in batch 5 |
| AV-07 | Climax-volume-bar AVWAP | trading-idea-catalogue.md | entry signal | sandbox/_AV_07_ClimaxVolumeBar.Strategy.CS | 5 | coded in batch 5 |
| AV-08 | Week-open / month-open / quarter-open AVWAP as regime layer | trading-idea-catalogue.md | entry signal | sandbox/_AV_08_PeriodAvwapLayer.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| AV-09 | Roll-anchored AVWAP swing bias | trading-idea-catalogue.md | entry signal | sandbox/_AV_09_RollAnchoredAVWAP.Strategy.CS | 5 | coded in batch 5 |
| AV-10 | AVWAP × profile confluence (does stacking help?) | trading-idea-catalogue.md | entry signal | sandbox/_AV_10_AVWAPProfileConfluence.Strategy.CS | 5 | coded in batch 5 |
| AV-11 | Breakout-bar AVWAP first pullback | trading-idea-catalogue.md | entry signal | sandbox/_AV_11_BreakAvwapPullback.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| AV-12 | Failed AVWAP reclaim → return to the opposing AVWAP | trading-idea-catalogue.md | entry signal | sandbox/_AV_12_FailedAVWAPReclaim.Strategy.CS | 5 | coded in batch 5 |
| AV-13 | AVWAP σ-stretch exit / scale signal | trading-idea-catalogue.md | entry signal | sandbox/_AV_13_AVWAPStretchExit.Strategy.CS | 5 | coded in batch 5 |
| VW-01 | Trend-day first VWAP pullback | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_VW_01_TrendDayFirst.Strategy.CS | 5 | coded in batch 5 |
| VW-02 | Balance-day ±2σ fade at a composite HVN edge (conditional) | trading-idea-catalogue.md | entry signal | sandbox/_VW_02_BalanceDay2.Strategy.CS | 5 | coded in batch 5 |
| VW-03 | σ-band expansion continuation | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_VW_03_BandExpansionContinuation.Strategy.CS | 5 | coded in batch 5 |
| VW-04 | Failed VWAP defence → reversal context | trading-idea-catalogue.md | entry signal | sandbox/_VW_04_FailedVWAPDefence.Strategy.CS | 6 | coded in batch 6 |
| VW-05 | Late-day continuation: VWAP side × rest-of-day return | trading-idea-catalogue.md | entry signal | sandbox/_VW_05_LateDayVwap.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| VW-06 | POC–VWAP skew pullback | trading-idea-catalogue.md | entry signal | sandbox/_VW_06_POCVWAPSkew.Strategy.CS | 6 | coded in batch 6 |
| VW-07 | Prior-day VWAP and ETH VWAP as opening reference (filter test) | trading-idea-catalogue.md | entry signal | sandbox/_VW_07_PriorDayVWAP.Strategy.CS | 6 | coded in batch 6 |
| VW-08 | VWAP-cross "Holy Grail" (canary) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | duplicate-of | _TestGate0 canary VW08 | done | already on main as _TestGate0 canary VW08. No second sandbox copy in batch 1. |
| DV-01 | Daily POC first-touch pullback ("The Simplest Volume Profile Intraday Strategy for Beginners") | trading-idea-catalogue.md | entry signal | sandbox/_DV_01_DailyPOCFirst.Strategy.CS | 6 | coded in batch 6 |
| DV-02 | Failed level → reversal (flip) trade | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_DV_02_FailedLevelFlip.Strategy.CS | 2 | coded in batch 2 |
| DV-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | duplicate-of | sandbox/_PS_13_RotationVolumeLevel.Strategy.CS | done | same worked example as PS-13. Do not write a second file. |
| DV-06 | End-of-trend-day cluster pullback (into the NY close) | trading-idea-catalogue.md | entry signal | sandbox/_DV_06_EndOfTrend.Strategy.CS | 6 | coded in batch 6 |
| DV-07 | Rotation / double-distribution edge → centre | trading-idea-catalogue.md | entry signal | sandbox/_DV_07_RotationDoubleDistribution.Strategy.CS | 6 | coded in batch 6 |
| DV-08 | Heavy-volume zone + Fair Value Gap alignment (level at the FVG start) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_08_FvgHvnAlignment.Strategy.CS | 2 | coded in batch 2 |
| DV-09 | Weekly-VWAP ±1σ trend pullback | trading-idea-catalogue.md | entry signal | sandbox/_DV_09_WeeklyVWAP1.Strategy.CS | 6 | coded in batch 6 |
| DV-11 | VWAP trend-failure (lose σ1 → pullback to σ1 → target VWAP) | trading-idea-catalogue.md | entry signal | sandbox/_DV_11_VWAPTrendFailure.Strategy.CS | 6 | coded in batch 6 |
| DV-15 | EMA20 pullback gated by VWAP slope | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_DV_15_EmaVwapPullback.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| DV-16 | Order-flow confirmation at a pre-defined level (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_16_OrderFlowConfirmation.Strategy.CS | 6 | coded in batch 6 |
| DV-20 | Stop-loss-cluster (weak low/high) veto | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_DV_20_StopLossCluster.Strategy.CS | 7 | coded in batch 7 |
| DV-22 | ABCD (AB = CD) | trading-idea-catalogue.md | entry signal | sandbox/_DV_22_ABCDABCD.Strategy.CS | 7 | coded in batch 7 |
| DV-23 | Initial-balance draw trade (guest: Dan McKen, Funded Trader Academy, not Dale) | trading-idea-catalogue.md | entry signal | sandbox/_DV_23_InitialBalanceDraw.Strategy.CS | 7 | coded in batch 7 |
| TS-01 | European-open overnight drift (long MES 01:30–04:30 ET) | trading-idea-catalogue.md | entry signal | sandbox/_TS_01_EuropeanOpenDrift.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| TS-02 | London-open break of the Asia range (CL/GC) with an Asia AVWAP filter | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_TS_02_LondonOpenBreak.Strategy.CS | 7 | coded in batch 7 |
| TS-03 | Close regime: 15:30–16:00 MOC drift then reversion | trading-idea-catalogue.md | entry signal | sandbox/_TS_03_CloseDrift.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| TS-04 | CL/GC pit-session profile (port of the PS ideas) | trading-idea-catalogue.md | entry signal | sandbox/_TS_04_CLGCPit.Strategy.CS | 7 | coded in batch 7 |
| TS-05 | ZN post-release AVWAP acceptance (08:30 data) | trading-idea-catalogue.md | entry signal | sandbox/_TS_05_ZNPostRelease.Strategy.CS | 7 | coded in batch 7 |
| TS-06 | Overnight inventory correction at the open | trading-idea-catalogue.md | entry signal | sandbox/_TS_06_OvernightInventoryCorrection.Strategy.CS | 7 | coded in batch 7 |
| TS-07 | Opening-noise observation window (filter test) | trading-idea-catalogue.md | entry signal | sandbox/_TS_07_OpeningNoiseObservation.Strategy.CS | 7 | coded in batch 7 |
| SW-01 | Weekly balance break (3–6-week composite) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_SW_01_WeeklyBalanceBreak.Strategy.CS | 7 | coded in batch 7 |
| SW-02 | RSI(2) dip-buy with a profile/AVWAP location filter | trading-idea-catalogue.md | entry signal | sandbox/_SW_02_SwingPullback.Strategy.CS | done | coded in sandbox on main (PR 124). PR 126 extends it. This plan does not edit that file. |
| SW-03 | Quarterly/monthly AVWAP swing pullback | trading-idea-catalogue.md | entry signal | sandbox/_SW_03_QuarterlyMonthlyAVWAP.Strategy.CS | 7 | coded in batch 7 |
| SW-04 | Multi-day P-profile hold (Levy "will it move within 3 days?") | trading-idea-catalogue.md | entry signal | sandbox/_SW_04_MultiDayP.Strategy.CS | 7 | coded in batch 7 |
| SW-05 | Composite gap-and-go (out of a 20-day composite) | trading-idea-catalogue.md | entry signal | sandbox/_SW_05_CompositeGapAnd.Strategy.CS | 7 | coded in batch 7 |
| SW-06 | Weekly IB (Mon–Tue range) breakout with acceptance | trading-idea-catalogue.md | entry signal | sandbox/_SW_06_WeeklyIBMon.Strategy.CS | 7 | coded in batch 7 |
| PS-01 | Accepted balance break → first-pullback higher low (ABB-HL) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_01_BalanceBreakPullback.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-02 | Balance break at acceptance close (K4 end-to-end spec) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_PS_02_BalanceBreakClose.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-03 | Confirmed failed break of value → rotation to the opposite edge | keller-b5-p6p7-entries-analyst2.md, trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_03_ConfirmedFailedBreak.Strategy.CS | 7 | coded in batch 7 |
| PS-04 | Prior-value magnet on a small newsless gap (Keller IX-1) | trading-idea-catalogue.md | entry signal | sandbox/_PS_04_PriorValueMagnet.Strategy.CS | 7 | coded in batch 7 |
| PS-05 | Rejected repricing open (Keller IX-3 · Spec 3) | trading-idea-catalogue.md | entry signal | sandbox/_PS_05_RejectedRepricingOpen.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-06 | Defeated adjustment → continuation (Keller IX-2) | trading-idea-catalogue.md | entry signal | sandbox/_PS_06_DefeatedAdjustmentContinuation.Strategy.CS | 7 | coded in batch 7 |
| PS-07 | Open-drive / one-way open: first-pullback continuation | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_07_OpenDrive.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-08 | IB extension with acceptance (narrow-IB tier) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_PS_08_NarrowIbBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-09 | IB C-period failure → IB midpoint | trading-idea-catalogue.md | entry signal | sandbox/_PS_09_IBCPeriod.Strategy.CS | 7 | coded in batch 7 |
| PS-10 | Developing-POC migration join (VAH-as-support pullback) | trading-idea-catalogue.md | entry signal | sandbox/_PS_10_DevelopingPOCMigration.Strategy.CS | 7 | coded in batch 7 |
| PS-11 | Unratified new extreme (POC refuses to follow) → confirmed fade | trading-idea-catalogue.md | entry signal | sandbox/_PS_11_UnratifiedNewExtreme.Strategy.CS | 8 | coded in batch 8 |
| PS-12 | Trend-cluster pullback (first touch of an in-trend volume cluster) | trading-idea-catalogue.md | entry signal | sandbox/_PS_12_TrendClusterPullback.Strategy.CS | 2 | coded in batch 2 |
| PS-13 | Volume-accumulation breakout: first-touch retest | trading-idea-catalogue.md | entry signal | sandbox/_PS_13_RotationVolumeLevel.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-14 | Rejection-ledge revisit fade | trading-idea-catalogue.md | entry signal | sandbox/_PS_14_RejectionLedgeRevisit.Strategy.CS | 8 | coded in batch 8 |
| PS-15 | LVN traverse after acceptance into the void | trading-idea-catalogue.md | entry signal | sandbox/_PS_15_LVNTraverseAfter.Strategy.CS | 8 | coded in batch 8 |
| PS-16 | LVN slow-approach bounce | trading-idea-catalogue.md | entry signal | sandbox/_PS_16_LVNSlowApproach.Strategy.CS | 8 | coded in batch 8 |
| PS-17 | Double-distribution waist trade | trading-idea-catalogue.md | entry signal | sandbox/_PS_17_DoubleDistributionWaist.Strategy.CS | 8 | coded in batch 8 |
| PS-18 | P/b-profile stop-sweep buy (Levy) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_18_PBProfile.Strategy.CS | 8 | coded in batch 8 |
| PS-19 | Poor high/low run-through (not repair-as-magnet) | trading-idea-catalogue.md | entry signal | sandbox/_PS_19_PoorHighRun.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-20 | Single-print (initiative tail) pullback continuation | trading-idea-catalogue.md | entry signal | sandbox/_PS_20_SinglePrintInitiative.Strategy.CS | 8 | coded in batch 8 |
| PS-21 | Naked POC / naked VAH–VAL first touch, with a confirming trigger | trading-idea-catalogue.md | entry signal | sandbox/_PS_21_NakedLevelReaction.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-22 | Composite LVN-edge rejection vs HVN-ceiling veto | trading-idea-catalogue.md | entry signal | sandbox/_PS_22_LvnEdgeRejection.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-23 | Profile shape at the close → next-session tilt | trading-idea-catalogue.md | entry signal | sandbox/_PS_23_ProfileShapeAt.Strategy.CS | 8 | coded in batch 8 |
| PS-24 | Overnight high/low break-and-go at RTH | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_PS_24_OvernightBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| PS-25 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_PS_25_PS25.Strategy.CS | 8 | coded in batch 8 |
| OF-01 | Stacked-imbalance origin: first retest continuation | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_OF_01_StackedImbalanceRetest.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| OF-02 | Delta-z ratification of the breakout bar (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_02_DeltaZRatification.Strategy.CS | 8 | coded in batch 8 |
| OF-03 | Anchored CVD flip at a failed break (layer) | trading-idea-catalogue.md | entry signal | sandbox/_OF_03_AnchoredCVDFlip.Strategy.CS | 8 | coded in batch 8 |
| OF-04 | Absorption at an aged HTF level (control) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_04_AbsorptionAtAn.Strategy.CS | 8 | coded in batch 8 |
| OF-05 | CVD divergence at a value extreme | trading-idea-catalogue.md | entry signal | sandbox/_OF_05_CVDDivergenceAt.Strategy.CS | 8 | coded in batch 8 |
| OF-06 | Unfinished-auction veto (layer) | trading-idea-catalogue.md | entry signal | sandbox/_OF_06_UnfinishedAuctionVeto.Strategy.CS | 8 | coded in batch 8 |
| OF-07 | Bar-level burst at a fresh extreme (layer / standalone) | trading-idea-catalogue.md | entry signal | sandbox/_OF_07_BurstExtremeBreak.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| OF-08 | Large-trade print zone retest | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_08_LargeTradePrint.Strategy.CS | 8 | coded in batch 8 |
| OF-09 | Weekly bias, 30m value-area break, 1m failed-seller (order-flow cascade) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_09_WeeklyBias30m.Strategy.CS | 8 | coded in batch 8 |
| OF-10 | ES vs NQ relative order-flow selection (layer on OF-09) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_OF_10_ESVsNQ.Strategy.CS | 8 | coded in batch 8 |
| MT-01 | HTF-aligned higher low at value (60m → 15m → 5m → 1m nesting) | trading-idea-catalogue.md | entry signal | sandbox/_MT_01_MultiTfStack.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| MT-02 | Weekly accepted value + 60m trigger (swing) | trading-idea-catalogue.md | entry signal | sandbox/_MT_02_WeeklyValueAcceptance.Strategy.CS | 1 | coded in this plan's first PR, port of the engines-manual example |
| MT-03 | 20-day composite location gate + 5m acceptance break | trading-idea-catalogue.md | entry signal | sandbox/_MT_03_CompositeValueGate.Strategy.CS | 2 | coded in batch 2 |
| MT-04 | 60m swing-AVWAP + 5m HL entry | trading-idea-catalogue.md | entry signal | sandbox/_MT_04_X60mSwingAVWAP.Strategy.CS | 9 | coded in batch 9 |
| MT-05 | 240m multi-session LVN as pullback zone | trading-idea-catalogue.md | entry signal | sandbox/_MT_05_X240mMultiSession.Strategy.CS | 9 | coded in batch 9 |
| MT-06 | Daily failed auction (excess / poor extreme) + intraday trigger | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_MT_06_DailyFailedAuction.Strategy.CS | 9 | coded in batch 9 |
| MT-07 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_MT_07_MT07.Strategy.CS | 9 | coded in batch 9 |
| SP-01 | Failed double top → upside breakout (T4) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_SP_01_FailedDoubleTop.Strategy.CS | 9 | coded in batch 9 |
| SP-02 | Double top at VAH with second-test divergence (T2, control) | trading-idea-catalogue.md | entry signal | sandbox/_SP_02_DoubleTopAt.Strategy.CS | 9 | coded in batch 9 |
| SP-03 | Wyckoff spring at composite VAL with a test (T3) | trading-idea-catalogue.md | entry signal | sandbox/_SP_03_WyckoffSpringAt.Strategy.CS | 9 | coded in batch 9 |
| SP-04 | Lower high after a failed auction at a composite extreme (T6) | trading-idea-catalogue.md | entry signal | sandbox/_SP_04_LowerHighAfter.Strategy.CS | 9 | coded in batch 9 |
| SP-05 | Four-part reversal-bar event at a profile extreme | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_05_FourPartReversal.Strategy.CS | 9 | coded in batch 9 |
| SP-06 | First higher low after a downtrend at composite VAL (change of character) | trading-idea-catalogue.md | entry signal | sandbox/_SP_06_FirstHigherLow.Strategy.CS | 9 | coded in batch 9 |
| SP-07 | Failed-failure re-break (Spec 4) | trading-idea-catalogue.md | entry signal | sandbox/_SP_07_FailedFailureRe.Strategy.CS | 9 | coded in batch 9 |
| SP-08 | Point-of-Origin retest (Levy) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_08_PointOfOrigin.Strategy.CS | 9 | coded in batch 9 |
| SP-09 | Clock-window equal-liquidity sweep into an HTF fair-value gap (ICT "macro" scalp) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_SP_09_ClockWindowEqual.Strategy.CS | 9 | coded in batch 9 |
| KD-01 | Accept-beyond-rail continuation (detrended rails, rails as targets) — **primary** | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_01_AcceptBeyondRail.Strategy.CS | 9 | coded in batch 9 |
| KD-02 | Band-stretch fade to the POC rail — control / canary (G0-b) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_02_BandStretchFade.Strategy.CS | 9 | coded in batch 9 |
| KD-03 | Three-family confluence filter (module, not an entry) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_03_ThreeFamilyConfluence.Strategy.CS | 9 | coded in batch 9 |
| KD-04 | Detrended-fit regime gate: degree-1 slope with band-break refit (module) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_04_DetrendedFitRegime.Strategy.CS | 9 | coded in batch 9 |
| KD-05 | HTF detrended-rail alignment (15m entries on 60m/240m accepted rails) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_05_HTFDetrendedRail.Strategy.CS | 9 | coded in batch 9 |
| KD-06 | σ-band acceptance breakout (PS-25 robustness arm "σ2") | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_06_BandAcceptanceBreakout.Strategy.CS | 9 | coded in batch 9 |
| KD-07 | No-fade veto (gate form of KD-02) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_07_NoFadeVeto.Strategy.CS | 9 | coded in batch 9 |
| KD-08 | Forward-cone diagnostic (study, not a trade) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_08_ForwardConeDiagnostic.Strategy.CS | 2 | coded in batch 2 |
| KD-09 | Family-count confluence filter (rev 3.1 module form of RG-09 / KD-03) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_09_FamilyCountConfluence.Strategy.CS | 10 | coded in batch 10 |
| KD-10 | Far-outside / band-edge node: acceptance only (PS-25 robustness arm "node") | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_10_FarOutsideBand.Strategy.CS | 10 | coded in batch 10 |
| KD-11 | Beyond-value node reach-rate study (Field Drill 7.5; diagnostic) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md | entry signal | sandbox/_KD_11_BeyondValueNode.Strategy.CS | 10 | coded in batch 10 |
| YV-01 | 30m balance/VA break → 5m big-trade confirmation → 1m failed-seller pullback entry (Trader Oxygen) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_01_X30mBalanceVA.Strategy.CS | 10 | coded in batch 10 |
| YV-02 | Cross-index flow selection: execute in whichever of NQ/ES shows the stronger confirming order flow (layer) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_02_CrossIndexFlow.Strategy.CS | 10 | coded in batch 10 |
| YV-03 | Momentum burst (Pradeep Bonde): 4% range-expansion day out of an orderly base (stocks) | trading-idea-catalogue.md | entry signal | sandbox/_YV_03_MomentumBurstPradeep.Strategy.CS | 10 | coded in batch 10 |
| YV-04 | Environment → location → confirmation: discount pullback in value-up structure with absorption and a second seller failure (Robbins Cup order flow) | trading-idea-catalogue.md | entry signal | sandbox/_YV_04_EnvironmentLocationConfirmation.Strategy.CS | 10 | coded in batch 10 |
| YV-05 | Absolute participation gate: trade only while 5m volume ≥ a fixed contract count (MNQ 20,000/5m) | trading-idea-catalogue.md | entry signal | sandbox/_YV_05_AbsoluteParticipationGate.Strategy.CS | 10 | coded in batch 10 |
| YV-06 | Session stop after 2 losses / 1–2 attempts per setup per day, inside a fixed early window | trading-idea-catalogue.md | entry signal | sandbox/_YV_06_SessionStopAfter.Strategy.CS | 10 | coded in batch 10 |
| YV-07 | ICT macro-window scalp: 5m FVG traded into inside xx:50–xx:10, 15s sweep of equal highs + new FVG → target equal lows | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_07_ICTMacroWindow.Strategy.CS | 10 | coded in batch 10 |
| YV-08 | Daily sweep strategy: 1h SFP of a prior-day/Asia/pre-NY swing → 5m FVG entry, candle-2 stop, 1:3 or opposing liquidity | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_08_DailySweepStrategy.Strategy.CS | 10 | coded in batch 10 |
| YV-09 | Turtle Donchian breakout: System 1 (20-day high/low, 10-day exit) and System 2 (55-day, 20-day exit); EMA20-close exit arm | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_09_TurtleDonchianBreakout.Strategy.CS | 10 | coded in batch 10 |
| YV-10 | Labelled Markov regime: 20-day-return states (±5%), 3×3 transition matrix, signal = P(bull) − P(bear) for tomorrow, HMM agreement | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | entry signal | sandbox/_YV_10_LabelledMarkovRegime.Strategy.CS | 10 | coded in batch 10 |
| YV-11 | 9 EMA / 20 SMA zone pullback in a sloping trend, with an over-extension veto (Malyarovich) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_11_X9EMA20.Strategy.CS | 10 | coded in batch 10 |
| YV-12 | Prior-day high/low range rejection on a "Day 2" session: wick candle → other side of the range (SMB Market Memory Model, setups 1–2) | trading-idea-catalogue.md, video-ideas-2026-11-10-additions.md | entry signal | sandbox/_YV_12_PriorDayHigh.Strategy.CS | 10 | coded in batch 10 |
| VX-01 | Hedged VX term-structure carry (Simon–Campasano roll rule) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_01_HedgedVXTerm.Strategy.CS | 10 | coded in batch 10 |
| VX-02 | VRP-filtered short volatility (Cooper VRP rule, futures form) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_02_VRPFilteredShort.Strategy.CS | 10 | coded in batch 10 |
| VX-03 | Long volatility in backwardation (VX-01's long leg alone) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_03_LongVolatilityIn.Strategy.CS | 10 | coded in batch 10 |
| VX-04 | Post-spike normalisation short (VIX spike mean reversion) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_04_PostSpikeNormalisation.Strategy.CS | 10 | coded in batch 10 |
| VX-05 | Long-volatility tail-hedge sleeve (portfolio insurance) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_05_LongVolatilityTail.Strategy.CS | 11 | coded in batch 11 |
| VX-06 | VIX term-structure regime gate for ES hosts (layer) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_06_VIXTermStructure.Strategy.CS | 11 | coded in batch 11 |
| VX-07 | VVIX gate (module) | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_07_VVIXGateModule.Strategy.CS | 11 | coded in batch 11 |
| VX-08 | VIX-stretch ES dip-buy (Connors & Alvarez) — layer on SW-02 / canary | trading-idea-catalogue.md | data-unavailable | sandbox/_VX_08_VIXStretchES.Strategy.CS | 11 | coded in batch 11 |
| LE-01 | 200-day trend leverage rotation (Gayed & Bilello) | trading-idea-catalogue.md | entry signal | sandbox/_LE_01_X200DayTrend.Strategy.CS | 11 | coded in batch 11 |
| LE-02 | Volatility-targeted leverage (exposure ∝ target vol / realised vol) | trading-idea-catalogue.md | entry signal | sandbox/_LE_02_VolatilityTargetedLeverage.Strategy.CS | 11 | coded in batch 11 |
| LE-03 | QQQ ↔ TQQQ regime leverage rotation | trading-idea-catalogue.md | entry signal | sandbox/_LE_03_QQQTQQQRegime.Strategy.CS | 11 | coded in batch 11 |
| LE-04 | LETF rebalancing pressure into the close (flow-scaled arm of VW-05) | trading-idea-catalogue.md | entry signal | sandbox/_LE_04_LETFRebalancingPressure.Strategy.CS | 11 | coded in batch 11 |
| LE-05 | Decay-pair short (short TQQQ + SQQQ, or UPRO + SPXU) | trading-idea-catalogue.md | entry signal | sandbox/_LE_05_DecayPairShort.Strategy.CS | 11 | coded in batch 11 |
| LE-06 | Short-term RSI mean reversion on LETFs (Connors-style) | trading-idea-catalogue.md | entry signal | sandbox/_LE_06_ShortTermRSI.Strategy.CS | 11 | coded in batch 11 |
| LE-07 | Inverse-LETF bear-trend hold (SQQQ/SPXU below SMA200) — negative control | trading-idea-catalogue.md | entry signal | sandbox/_LE_07_InverseLETFBear.Strategy.CS | 11 | coded in batch 11 |
| EX-01 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_01_EX01.Strategy.CS | 11 | coded in batch 11 |
| EX-02 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_02_EX02.Strategy.CS | 11 | coded in batch 11 |
| EX-03 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_03_EX03.Strategy.CS | 11 | coded in batch 11 |
| EX-04 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_04_EX04.Strategy.CS | 11 | coded in batch 11 |
| EX-05 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, keller-b5-p6p7-entries-analyst2.md, keller-b5-rev3.1-addendum.md, trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | exit | sandbox/_EX_05_EX05.Strategy.CS | 11 | coded in batch 11 |
| EX-06 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_06_EX06.Strategy.CS | 11 | coded in batch 11 |
| EX-07 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_07_EX07.Strategy.CS | 11 | coded in batch 11 |
| EX-08 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_08_EX08.Strategy.CS | 11 | coded in batch 11 |
| EX-09 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_09_EX09.Strategy.CS | 11 | coded in batch 11 |
| EX-10 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_10_EX10.Strategy.CS | 12 | coded in batch 12 |
| EX-11 | (no heading; id is cited in the source) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | exit | sandbox/_EX_11_EX11.Strategy.CS | 12 | coded in batch 12 |
| EX-12 | (no heading; id is cited in the source) | trading-idea-catalogue.md | exit | sandbox/_EX_12_EX12.Strategy.CS | 12 | coded in batch 12 |
| EX-13 | 1.5R partial, deep-pullback breakeven, trail under the initiative print (module) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md, video-ideas-2026-11-10-additions.md | exit | sandbox/_EX_13_X15RPartial.Strategy.CS | 12 | coded in batch 12 |
| RG-01 | Value-migration gate (V-score) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_01_ValueMigrationGate.Strategy.CS | 12 | coded in batch 12 |
| RG-02 | Balance/imbalance classifier + entropy skip | trading-idea-catalogue.md | regime-filter | sandbox/_RG_02_BalanceImbalanceClassifier.Strategy.CS | 12 | coded in batch 12 |
| RG-03 | Volatility regime stratifier | trading-idea-catalogue.md | duplicate-of | _RegimeEngine AtrTercile / _A2_63_RegimeGate arm A | done | already on main as _RegimeEngine AtrTercile / _A2_63_RegimeGate arm A. No second sandbox copy in batch 1. |
| RG-04 | Live day-type proxy at 10:30 (includes VWAP-cross count) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_04_LiveDayType.Strategy.CS | 12 | coded in batch 12 |
| RG-05 | Noise-band filter (Zarattini) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_05_NoiseBandFilter.Strategy.CS | 12 | coded in batch 12 |
| RG-06 | HTF trend state (TSMOM + 60m structure) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | regime-filter | sandbox/_RG_06_HTFTrendState.Strategy.CS | 12 | coded in batch 12 |
| RG-07 | Event, roll and thin-session exclusions | trading-idea-catalogue.md | regime-filter | sandbox/_RG_07_ExclusionGate.Strategy.CS | 2 | coded in batch 2 |
| RG-08 | Compression state (range/ATR) | trading-idea-catalogue.md | regime-filter | sandbox/_RG_08_CompressionStateRange.Strategy.CS | 12 | coded in batch 12 |
| RG-09 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | regime-filter | sandbox/_RG_09_RG09.Strategy.CS | 12 | coded in batch 12 |
| RG-10 | (no heading; id is cited in the source) | keller-b5-catalogue-additions.md, trading-idea-catalogue.md | regime-filter | sandbox/_RG_10_RG10.Strategy.CS | 12 | coded in batch 12 |
| PF-01 | Trend + carry core (parallel track) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_01_TrendCarryCore.Strategy.CS | 12 | coded in batch 12 |
| PF-02 | Regime-complementary intraday sleeves | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_02_RegimeComplementaryIntraday.Strategy.CS | 12 | coded in batch 12 |
| PF-03 | Cross-market diversification & hidden-beta monitor | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_03_XmktBetaMonitor.Strategy.CS | 2 | coded in batch 2 |
| PF-04 | Risk budgeting (deflated quarter-Kelly, capped) | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_04_RiskBudgetingDeflated.Strategy.CS | 12 | coded in batch 12 |
| PF-05 | Drawdown throttles + two-CUSUM monitoring | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_05_DrawdownThrottlesTwo.Strategy.CS | 12 | coded in batch 12 |
| PF-06 | Portfolio volatility targeting | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_06_PortfolioVolatilityTargeting.Strategy.CS | 12 | coded in batch 12 |
| PF-07 | Meta-labelling the survivors (later) | trading-idea-catalogue.md | sizing-portfolio | sandbox/_PF_07_MetaLabellingThe.Strategy.CS | 12 | coded in batch 12 |
| PF-08 | Donchian / Turtle daily trend sleeve (companion to PF-01) | trading-idea-catalogue.md, video-ideas-2026-11-08-additions.md | sizing-portfolio | sandbox/_PF_08_DonchianTurtleDaily.Strategy.CS | 13 | coded in batch 13 |
| L2-01 | Cancellation veto before level entries | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_01_CancellationVetoBefore.Strategy.CS | 13 | coded in batch 13 |
| L2-02 | Book-shape regime | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_02_BookShapeRegime.Strategy.CS | 13 | coded in batch 13 |
| L2-03 | Resilience half-life T½ prior to range breaks | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_03_ResilienceHalfLife.Strategy.CS | 13 | coded in batch 13 |
| L2-04 | Iceberg vs genuine replenishment, and iceberg-exhaustion trapdoor | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_04_IcebergVsGenuine.Strategy.CS | 13 | coded in batch 13 |
| L2-05 | Pre-event depth withdrawal forecasts the spike's revert fraction | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_05_PreEventDepth.Strategy.CS | 13 | coded in batch 13 |
| L2-06 | Queue-depth rule for resting orders (rest only if depth < 1× average trade size; Ch 11.1) | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_06_QueueDepthRule.Strategy.CS | 13 | coded in batch 13 |
| L2-07 | Two-sided cancellation surge = fragility alarm | trading-idea-catalogue.md | data-unavailable | sandbox/_L2_07_TwoSidedCancellation.Strategy.CS | 13 | coded in batch 13 |
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

Batch 2 in this update is the remaining engines-manual examples. Rows that the first draft called batch 2 and that the catalogue marks "Merged into" are now `done` / `duplicate-of`. The own entries that stay in batch 2 and are not coded yet are the next entry batch.

| Batch | IDs | What |
|---|---|---|
| done | 66 | A2-01, A2-02, A2-03, A2-05, A2-06, A2-07, A2-08, A2-10, A2-16, A2-17, A2-18, A2-19, ... (66) |
| 2 | 18 | A2-04, A2-09, A2-11, A2-12, A2-13, A2-14, A2-15, A2-25, A2-26, A2-27, A2-78, DV-02, DV-08, PS-12, MT-03, KD-08, RG-07, PF-03 |
| 3 | 11 | A2-28, A2-30, A2-31, A2-32, A2-34, A2-35, A2-36, A2-37, A2-38, A2-40, A2-43 |
| 1 | 20 | A2-29, A2-50, A2-51, A2-57, A2-58, AV-11, PS-01, PS-02, PS-05, PS-07, PS-08, PS-13, PS-19, PS-21, PS-22, PS-24, OF-01, OF-07, MT-01, MT-02 |
| 4 | 16 | A2-45, A2-46, A2-48, A2-52, A2-53, A2-54, A2-59, A2-60, A2-61, A2-62, A2-65, A2-66, A2-67, A2-68, A2-72, A2-73 |
| 5 | 19 | A2-74, A2-75, A2-76, A2-77, A2-79, AV-01, AV-02, AV-03, AV-04, AV-05, AV-06, AV-07, AV-09, AV-10, AV-12, AV-13, VW-01, VW-02, VW-03 |
| 6 | 18 | VW-04, VW-06, VW-07, DV-01, DV-04, DV-05, DV-06, DV-07, DV-09, DV-10, DV-11, DV-12, DV-13, DV-14, DV-16, DV-17, DV-18, DV-19 |
| 7 | 20 | DV-20, DV-21, DV-22, DV-23, DV-24, TS-02, TS-04, TS-05, TS-06, TS-07, SW-01, SW-03, SW-04, SW-05, SW-06, PS-03, PS-04, PS-06, PS-09, PS-10 |
| 8 | 18 | PS-11, PS-14, PS-15, PS-16, PS-17, PS-18, PS-20, PS-23, PS-25, PS-26, OF-02, OF-03, OF-04, OF-05, OF-06, OF-08, OF-09, OF-10 |
| 9 | 20 | MT-04, MT-05, MT-06, MT-07, SP-01, SP-02, SP-03, SP-04, SP-05, SP-06, SP-07, SP-08, SP-09, KD-01, KD-02, KD-03, KD-04, KD-05, KD-06, KD-07 |
| 10 | 19 | KD-09, KD-10, KD-11, YV-01, YV-02, YV-03, YV-04, YV-05, YV-06, YV-07, YV-08, YV-09, YV-10, YV-11, YV-12, VX-01, VX-02, VX-03, VX-04 |
| 11 | 20 | VX-05, VX-06, VX-07, VX-08, LE-01, LE-02, LE-03, LE-04, LE-05, LE-06, LE-07, EX-01, EX-02, EX-03, EX-04, EX-05, EX-06, EX-07, EX-08, EX-09 |
| 12 | 18 | EX-10, EX-11, EX-12, EX-13, RG-01, RG-02, RG-04, RG-05, RG-06, RG-08, RG-09, RG-10, PF-01, PF-02, PF-04, PF-05, PF-06, PF-07 |
| 13 | 8 | PF-08, L2-01, L2-02, L2-03, L2-04, L2-05, L2-06, L2-07 |

## Market tags

Each new sandbox header has `Market tag: futures-only`, `stock-only`, or `both`. Futures-only ideas use the futures session API (`IsRth`, `m_rthValueEnd`). Both and stock-only ideas use `Bars.Sessions` and never a hard-coded symbol, session clock, or tick floor. The floor is `InstrumentProfile.costFloorTicks`. Batches 1-3 were tagged from the code they already contain. New files are tagged with the idea.

The ten files from the merged PR 126 are not edited by this stack. Their tags live here:

| ID | Class | Market tag |
|---|---|---|
| A2-49 | `_A2_49_MiddayBreakout` | futures-only |
| A2-56 | `_A2_56_FirstHourBreak` | futures-only |
| A2-64 | `_A2_70_OpenFadeHvGate` | futures-only |
| A2-69 | `_A2_69_KaufmanThreeDay` | futures-only |
| A2-70 | `_A2_70_OpenFadeHvGate` | futures-only |
| AV-08 | `_AV_08_PeriodAvwapLayer` | futures-only |
| DV-15 | `_DV_15_EmaVwapPullback` | futures-only |
| SW-02 | `_SW_02_SwingPullback` | both |
| TS-01 | `_TS_01_EuropeanOpenDrift` | futures-only |
| TS-03 | `_TS_03_CloseDrift` | futures-only |
| VW-05 | `_VW_05_LateDayVwap` | futures-only |

## Non-trading rows

A file journals and does not send an order only when its catalogue heading has no entry. SandboxCompile rejects two sandbox signals whose CalcBar bodies match once comments, whitespace, quoted names and numeric literals are removed.

| ID | Reason |
|---|---|
| A2-29 | Entropy day-type gate. No entry. |
| A2-31 | KL-divergence acceptance gate. No entry. |
| A2-34 | V/T urgency target module. No entry. |
| A2-36 | No GEX series and no VIX series. The heading cannot be traded on this feed. |
| A2-37 | Cross-instrument dislocation filter. No entry. |
| A2-43 | Confluent-HVN veto. No entry. |
| A2-50 | Opening relative-volume gate. No entry. |
| A2-38 | Trade-size entropy is not in `_TickStream`. |
| A2-40 | Cost note. It does not set Commission or Slippage and it has no entry. |
| A2-48 | The heading says the module never opens a trade. It journals poor highs and poor lows. |
| A2-59 | Cost and unables audit. It does not set a fee. |
| A2-61 | Session efficiency-ratio router. It journals continuation or reversion and does not enter. |
| A2-72 | Size ladder. It journals 25/50/100 percent and does not enter. |
| A2-73 | Exit overlay. GX expresses it as BreakEven AtTrigger, RMultiple, value 3. It does not enter. |
| A2-74 | Scale-out module. It needs two contracts and has no entry. GX: ScaleOutThenRunner, T1 2.5 R, scale 50, BreakEven AfterT1. |
| A2-75 | Hit-rate throttle for a host. No entry. |
| A2-76 | Climax exit for an open host winner. No entry. |
| A2-77 | Follow-through checkpoint for a host. No entry. GX time stop does not test open profit. |
| A2-79 | Contraction gate for a host. No entry. |
| KD-08 | Forward-cone diagnostic. No entry. |
| PF-03 | Cross-market beta monitor. No entry. |
| AV-10 | Compares host events that already have an entry. No entry of its own. |
| AV-13 | Stretch scale for a host anchor. No entry. |
| VW-07 | Location filter for other entries. No standalone trade. No ETH type on EVwapType. |
| DV-16 | Order-flow layer on a host level. No standalone entry. |
| TS-04 | Re-runs PS-01 and PS-24 on a pit profile. No entry of its own. |
| TS-07 | Opening-noise blackout for other entries. No entry. |
| DV-20 | Weak-low veto for a host level. No entry. |
| OF-02 | Delta-z layer on a host breakout. No entry. |
| OF-03 | Anchored-CVD layer on a host entry. No entry. |
| OF-06 | Unfinished-auction veto. No entry. The journal uses a poor high as the proxy. |
| OF-08 | Parked. Per-trade size is not in _TickStream. No entry. |
| OF-10 | Not buildable. No second footprint engine. No entry. |
| KD-01 | Same entry as PS-25. No second trade. |
| KD-03 | Three-family confluence filter. No entry. |
| KD-04 | Detrended-fit regime gate. No entry. |
| KD-05 | Same layer as MT-07. No second trade. |
| KD-07 | No-fade veto, the gate form of KD-02. No entry. |
| MT-07 | Higher-timeframe gate on PS-25. No entry of its own. |
| KD-09 | Family-count filter on a host rail. No entry. |
| KD-11 | Node-touch diagnostic. No entry. |
| VX-01 | Needs VX1, VIX and the settlement calendar. HV20 is not that roll. |
| VX-02 | Needs VIX and VIX3M. HV10 minus HV20 is not the variance risk premium. |
| VX-03 | Needs VIX/VIX3M and the VX roll. |
| VX-04 | Needs VIX/VIX3M and a VIX 10-day high. |
| YV-02 | Chooses a second root by footprint. No second order-flow engine. |
| YV-05 | Volume gate on a host. No entry. |
| YV-06 | Stands a host down after losses. No entry. |
| VX-05 | Long-vol budget. Needs VIX and VVIX. |
| VX-06 | VIX/VIX3M state gate. Those indexes are missing. |
| VX-07 | VVIX percentile gate. VVIX is missing. |
| VX-08 | VIX stretch. VIX is missing. |
| LE-05 | Paired long and short leveraged funds. Needs borrow and the ETF pair. |
| EX-01 | HVN target module. GX cannot retarget the next HVN. |
| EX-02 | Naked-POC target module. No entry. |
| EX-03 | Balance-width target module. No entry. |
| EX-04 | AVWAP trail. GX has no AVWAP trail. |
| EX-05 | Confirmed higher-low trail. GX has no such trail. |
| EX-06 | Time stop only when open profit is under 0.5 R. GX time stop exits winners too. |
| EX-07 | Half off at 1 R. GX ScaleOutThenRunner, T1 1 R, scale 50, BreakEven AfterT1. One contract cannot scale. |
| EX-08 | Target-multiple diagnostic. No entry. |
| EX-09 | Stagnation exit for a host. No entry. |
| EX-10 | Acceptance-failure exit for a host. No entry. |
| EX-11 | Stop-beyond-LVN module. No entry. |
| EX-12 | CVD-divergence exit for a host runner. No entry. |
| EX-13 | 1.5 R partial and breakeven for a host. One contract cannot scale. |
| RG-01 | Value-migration gate. No entry. |
| RG-02 | Balance classifier. No entry. |
| RG-04 | Live day-type gate. No entry. |
| RG-05 | Noise-band gate. No entry. |
| RG-06 | Higher-timeframe trend gate. No entry. |
| RG-08 | Compression gate. No entry. |
| RG-09 | Three-family filter. Same module as KD-09. No entry. |
| RG-10 | Fit-regime stand-down. Same module as KD-04. No entry. |
| PF-01 | Multi-market trend and carry book. No single-instrument entry. |
| PF-02 | Regime sleeve router. No entry. |
| PF-04 | Portfolio risk budget. No entry. |
| PF-05 | Drawdown throttle. No entry. |
| PF-06 | Portfolio volatility target. No single-instrument entry. |
| PF-07 | Meta-label of host trades. No trained model and no entry. |
| L2-01 | Cancellation veto. No historical depth of book. |
| L2-02 | Book-shape regime. No historical depth of book. |
| L2-03 | Resilience half-life. No historical depth of book. |
| L2-04 | Iceberg versus replenishment. No historical depth of book. |
| L2-05 | Pre-event depth withdrawal. No historical depth of book. |
| L2-06 | Queue-depth rule. No historical depth of book. |
| L2-07 | Two-sided cancellation alarm. No historical depth of book. |

## Slot batches

Each batch is at most 30 studies, one per `_Test` slot. A subclass is in the same batch as its parent where the batches below allow it. A2-26 is the KDE veto inside `_A2_25_PocStabilityGate`, so it is copied with A2-25 and does not take a second slot. Copy a batch by placing each file in its own `_Test` slot and renaming only the class. When a child declares `: _Parent`, that base must be renamed to the parent's `_TestNN` class, or the original parent class must stay compiled in the same project. TS-04 extends PS-01, MT-07 extends PS-25, and KD-05 extends MT-07, so those copies need the parent class under its slot name.

### Slot batch 1 (30)

| ID | Class | Market tag |
|---|---|---|
| A2-04 | `_A2_04_OvernightInventory` | futures-only |
| A2-09 | `_A2_09_NoiseBandLong` | futures-only |
| A2-11 | `_A2_11_PriorDayVwap` | futures-only |
| A2-12 | `_A2_12_VwapTwapDivergence` | futures-only |
| A2-13 | `_A2_13_GapAvwapOpen` | futures-only |
| A2-14 | `_A2_14_WeeklyAvwapReclaim` | both |
| A2-27 | `_A2_27_BimodalVoidTraverse` | futures-only |
| A2-28 | `_A2_28_MinorExcess` | futures-only |
| A2-29 | `_A2_29_EntropyDayType` | futures-only |
| A2-30 | `_A2_30_RisingEntropy` | futures-only |
| A2-31 | `_A2_31_KlGate` | futures-only |
| A2-32 | `_A2_32_SigmaMixture` | futures-only |
| A2-34 | `_A2_34_VtTarget` | futures-only |
| A2-35 | `_A2_35_EventReformation` | futures-only |
| A2-36 | `_A2_36_GammaFilter` | both |
| A2-37 | `_A2_37_DislocationFilter` | both |
| A2-38 | `_A2_38_MetaorderDrift` | both |
| A2-40 | `_A2_40_CostModel` | both |
| A2-43 | `_A2_43_ConfluentHvn` | futures-only |
| A2-45 | `_A2_45_SinglePrintShelf` | futures-only |
| A2-46 | `_A2_46_VolumeGapCommitment` | futures-only |
| A2-48 | `_A2_48_PoorHighsLows` | futures-only |
| A2-49 | `_A2_49_MiddayBreakout` | futures-only |
| A2-50 | `_A2_50_OpeningRvGate` | futures-only |
| A2-51 | `_A2_51_ShockBarFade` | futures-only |
| A2-52 | `_A2_52_TimeConfirmedOpening` | futures-only |
| A2-53 | `_A2_53_RTHGapFade` | futures-only |
| A2-54 | `_A2_54_UpGapPullback` | futures-only |
| A2-56 | `_A2_56_FirstHourBreak` | futures-only |
| RG-07 | `_RG_07_ExclusionGate` | both |

### Slot batch 2 (30)

| ID | Class | Market tag |
|---|---|---|
| A2-57 | `_A2_57_TwoTimeframeTurn` | futures-only |
| A2-58 | `_A2_58_FastInSlowFade` | futures-only |
| A2-59 | `_A2_59_KaufmanCostUnables` | futures-only |
| A2-60 | `_A2_60_MeyersNormalisedRange` | futures-only |
| A2-61 | `_A2_61_EfficiencyRatioSession` | futures-only |
| A2-62 | `_A2_62_Taylor3Day` | futures-only |
| A2-65 | `_A2_65_Raschke1stCross` | both |
| A2-66 | `_A2_66_RaschkeKaufman2nd` | both |
| A2-67 | `_A2_67_IntradayRSIOf` | both |
| A2-68 | `_A2_68_X5BarRun` | both |
| A2-69 | `_A2_69_KaufmanThreeDay` | futures-only |
| A2-70 | `_A2_70_OpenFadeHvGate` | futures-only |
| A2-72 | `_A2_72_ProgressiveExposureLadder` | both |
| A2-73 | `_A2_73_BreakevenStopAt` | both |
| A2-74 | `_A2_74_FreeRollSell` | both |
| A2-75 | `_A2_75_RollingHitRate` | both |
| A2-76 | `_A2_76_SellIntoThe` | both |
| A2-77 | `_A2_77_FailedFollowThrough` | both |
| A2-79 | `_A2_79_IntradayVolatilityContraction` | futures-only |
| AV-01 | `_AV_01_SwingHighAVWAP` | futures-only |
| AV-02 | `_AV_02_ImpulseOriginAVWAP` | futures-only |
| AV-03 | `_AV_03_AVWAPPinchBreakout` | futures-only |
| AV-04 | `_AV_04_AVWAPHandoffContinuation` | futures-only |
| AV-05 | `_AV_05_GapAnchoredAVWAP` | futures-only |
| AV-06 | `_AV_06_EventBarAVWAP` | futures-only |
| AV-07 | `_AV_07_ClimaxVolumeBar` | futures-only |
| AV-08 | `_AV_08_PeriodAvwapLayer` | futures-only |
| AV-09 | `_AV_09_RollAnchoredAVWAP` | futures-only |
| AV-10 | `_AV_10_AVWAPProfileConfluence` | futures-only |
| AV-11 | `_AV_11_BreakAvwapPullback` | futures-only |

### Slot batch 3 (20)

| ID | Class | Market tag |
|---|---|---|
| AV-12 | `_AV_12_FailedAVWAPReclaim` | futures-only |
| AV-13 | `_AV_13_AVWAPStretchExit` | futures-only |
| DV-01 | `_DV_01_DailyPOCFirst` | futures-only |
| DV-02 | `_DV_02_FailedLevelFlip` | futures-only |
| A2-25 | `_A2_25_PocStabilityGate` | both |
| A2-26 | `_A2_25_PocStabilityGate` | both |
| DV-06 | `_DV_06_EndOfTrend` | futures-only |
| DV-07 | `_DV_07_RotationDoubleDistribution` | futures-only |
| DV-08 | `_DV_08_FvgHvnAlignment` | both |
| DV-09 | `_DV_09_WeeklyVWAP1` | futures-only |
| DV-11 | `_DV_11_VWAPTrendFailure` | futures-only |
| DV-15 | `_DV_15_EmaVwapPullback` | futures-only |
| DV-16 | `_DV_16_OrderFlowConfirmation` | futures-only |
| DV-20 | `_DV_20_StopLossCluster` | futures-only |
| DV-22 | `_DV_22_ABCDABCD` | futures-only |
| DV-23 | `_DV_23_InitialBalanceDraw` | futures-only |
| EX-01 | `_EX_01_EX01` | both |
| EX-02 | `_EX_02_EX02` | both |
| EX-03 | `_EX_03_EX03` | both |
| EX-04 | `_EX_04_EX04` | both |

### Slot batch 4 (30)

| ID | Class | Market tag |
|---|---|---|
| EX-05 | `_EX_05_EX05` | both |
| EX-06 | `_EX_06_EX06` | both |
| EX-07 | `_EX_07_EX07` | both |
| EX-08 | `_EX_08_EX08` | both |
| EX-09 | `_EX_09_EX09` | both |
| EX-10 | `_EX_10_EX10` | futures-only |
| EX-11 | `_EX_11_EX11` | futures-only |
| EX-12 | `_EX_12_EX12` | futures-only |
| EX-13 | `_EX_13_X15RPartial` | both |
| KD-01 | `_KD_01_AcceptBeyondRail` | futures-only |
| KD-02 | `_KD_02_BandStretchFade` | futures-only |
| KD-03 | `_KD_03_ThreeFamilyConfluence` | futures-only |
| KD-04 | `_KD_04_DetrendedFitRegime` | futures-only |
| KD-05 | `_KD_05_HTFDetrendedRail` | futures-only |
| KD-06 | `_KD_06_BandAcceptanceBreakout` | futures-only |
| KD-07 | `_KD_07_NoFadeVeto` | futures-only |
| KD-08 | `_KD_08_ForwardConeDiagnostic` | both |
| KD-09 | `_KD_09_FamilyCountConfluence` | futures-only |
| KD-10 | `_KD_10_FarOutsideBand` | futures-only |
| KD-11 | `_KD_11_BeyondValueNode` | futures-only |
| L2-01 | `_L2_01_CancellationVetoBefore` | futures-only |
| L2-02 | `_L2_02_BookShapeRegime` | futures-only |
| L2-03 | `_L2_03_ResilienceHalfLife` | futures-only |
| L2-04 | `_L2_04_IcebergVsGenuine` | futures-only |
| L2-05 | `_L2_05_PreEventDepth` | futures-only |
| L2-06 | `_L2_06_QueueDepthRule` | futures-only |
| L2-07 | `_L2_07_TwoSidedCancellation` | futures-only |
| LE-01 | `_LE_01_X200DayTrend` | both |
| LE-02 | `_LE_02_VolatilityTargetedLeverage` | both |
| LE-03 | `_LE_03_QQQTQQQRegime` | both |

### Slot batch 5 (29)

| ID | Class | Market tag |
|---|---|---|
| LE-04 | `_LE_04_LETFRebalancingPressure` | futures-only |
| LE-05 | `_LE_05_DecayPairShort` | stock-only |
| LE-06 | `_LE_06_ShortTermRSI` | both |
| LE-07 | `_LE_07_InverseLETFBear` | both |
| MT-01 | `_MT_01_MultiTfStack` | futures-only |
| MT-02 | `_MT_02_WeeklyValueAcceptance` | futures-only |
| MT-04 | `_MT_04_X60mSwingAVWAP` | futures-only |
| MT-05 | `_MT_05_X240mMultiSession` | futures-only |
| MT-06 | `_MT_06_DailyFailedAuction` | futures-only |
| MT-07 | `_MT_07_MT07` | futures-only |
| OF-01 | `_OF_01_StackedImbalanceRetest` | futures-only |
| OF-02 | `_OF_02_DeltaZRatification` | futures-only |
| OF-03 | `_OF_03_AnchoredCVDFlip` | futures-only |
| OF-04 | `_OF_04_AbsorptionAtAn` | futures-only |
| OF-05 | `_OF_05_CVDDivergenceAt` | futures-only |
| OF-06 | `_OF_06_UnfinishedAuctionVeto` | futures-only |
| OF-07 | `_OF_07_BurstExtremeBreak` | futures-only |
| A2-78 | `_A2_78_DailyKillSwitch` | both |
| OF-08 | `_OF_08_LargeTradePrint` | futures-only |
| OF-09 | `_OF_09_WeeklyBias30m` | futures-only |
| OF-10 | `_OF_10_ESVsNQ` | futures-only |
| PF-01 | `_PF_01_TrendCarryCore` | both |
| PF-02 | `_PF_02_RegimeComplementaryIntraday` | futures-only |
| PF-03 | `_PF_03_XmktBetaMonitor` | futures-only |
| PF-04 | `_PF_04_RiskBudgetingDeflated` | both |
| PF-05 | `_PF_05_DrawdownThrottlesTwo` | both |
| PF-06 | `_PF_06_PortfolioVolatilityTargeting` | both |
| PF-07 | `_PF_07_MetaLabellingThe` | both |
| PF-08 | `_PF_08_DonchianTurtleDaily` | futures-only |

### Slot batch 6 (29)

| ID | Class | Market tag |
|---|---|---|
| PS-01 | `_PS_01_BalanceBreakPullback` | futures-only |
| A2-15 | `_A2_15_LegSkew` | both |
| PS-02 | `_PS_02_BalanceBreakClose` | futures-only |
| PS-03 | `_PS_03_ConfirmedFailedBreak` | futures-only |
| PS-04 | `_PS_04_PriorValueMagnet` | futures-only |
| PS-05 | `_PS_05_RejectedRepricingOpen` | futures-only |
| PS-06 | `_PS_06_DefeatedAdjustmentContinuation` | futures-only |
| PS-07 | `_PS_07_OpenDrive` | futures-only |
| PS-08 | `_PS_08_NarrowIbBreak` | futures-only |
| MT-03 | `_MT_03_CompositeValueGate` | both |
| PS-09 | `_PS_09_IBCPeriod` | futures-only |
| PS-10 | `_PS_10_DevelopingPOCMigration` | futures-only |
| PS-11 | `_PS_11_UnratifiedNewExtreme` | futures-only |
| PS-12 | `_PS_12_TrendClusterPullback` | both |
| PS-13 | `_PS_13_RotationVolumeLevel` | futures-only |
| PS-14 | `_PS_14_RejectionLedgeRevisit` | futures-only |
| PS-15 | `_PS_15_LVNTraverseAfter` | futures-only |
| PS-16 | `_PS_16_LVNSlowApproach` | futures-only |
| PS-17 | `_PS_17_DoubleDistributionWaist` | futures-only |
| PS-18 | `_PS_18_PBProfile` | futures-only |
| PS-19 | `_PS_19_PoorHighRun` | futures-only |
| PS-20 | `_PS_20_SinglePrintInitiative` | futures-only |
| PS-21 | `_PS_21_NakedLevelReaction` | futures-only |
| PS-22 | `_PS_22_LvnEdgeRejection` | futures-only |
| PS-23 | `_PS_23_ProfileShapeAt` | futures-only |
| PS-24 | `_PS_24_OvernightBreak` | futures-only |
| PS-25 | `_PS_25_PS25` | futures-only |
| RG-01 | `_RG_01_ValueMigrationGate` | futures-only |
| RG-02 | `_RG_02_BalanceImbalanceClassifier` | futures-only |

### Slot batch 7 (30)

| ID | Class | Market tag |
|---|---|---|
| RG-04 | `_RG_04_LiveDayType` | futures-only |
| RG-05 | `_RG_05_NoiseBandFilter` | both |
| RG-06 | `_RG_06_HTFTrendState` | both |
| RG-08 | `_RG_08_CompressionStateRange` | both |
| RG-09 | `_RG_09_RG09` | futures-only |
| RG-10 | `_RG_10_RG10` | futures-only |
| SP-01 | `_SP_01_FailedDoubleTop` | futures-only |
| SP-02 | `_SP_02_DoubleTopAt` | futures-only |
| SP-03 | `_SP_03_WyckoffSpringAt` | futures-only |
| SP-04 | `_SP_04_LowerHighAfter` | futures-only |
| SP-05 | `_SP_05_FourPartReversal` | futures-only |
| SP-06 | `_SP_06_FirstHigherLow` | futures-only |
| SP-07 | `_SP_07_FailedFailureRe` | futures-only |
| SP-08 | `_SP_08_PointOfOrigin` | futures-only |
| SP-09 | `_SP_09_ClockWindowEqual` | futures-only |
| SW-01 | `_SW_01_WeeklyBalanceBreak` | futures-only |
| SW-02 | `_SW_02_SwingPullback` | both |
| SW-03 | `_SW_03_QuarterlyMonthlyAVWAP` | futures-only |
| SW-04 | `_SW_04_MultiDayP` | futures-only |
| SW-05 | `_SW_05_CompositeGapAnd` | futures-only |
| SW-06 | `_SW_06_WeeklyIBMon` | futures-only |
| TS-01 | `_TS_01_EuropeanOpenDrift` | futures-only |
| TS-02 | `_TS_02_LondonOpenBreak` | futures-only |
| TS-03 | `_TS_03_CloseDrift` | futures-only |
| TS-04 | `_TS_04_CLGCPit` | futures-only |
| TS-05 | `_TS_05_ZNPostRelease` | futures-only |
| TS-06 | `_TS_06_OvernightInventoryCorrection` | futures-only |
| TS-07 | `_TS_07_OpeningNoiseObservation` | futures-only |
| VW-01 | `_VW_01_TrendDayFirst` | futures-only |
| VW-02 | `_VW_02_BalanceDay2` | futures-only |

### Slot batch 8 (25)

| ID | Class | Market tag |
|---|---|---|
| VW-03 | `_VW_03_BandExpansionContinuation` | futures-only |
| VW-04 | `_VW_04_FailedVWAPDefence` | futures-only |
| VW-05 | `_VW_05_LateDayVwap` | futures-only |
| VW-06 | `_VW_06_POCVWAPSkew` | futures-only |
| VW-07 | `_VW_07_PriorDayVWAP` | futures-only |
| VX-01 | `_VX_01_HedgedVXTerm` | futures-only |
| VX-02 | `_VX_02_VRPFilteredShort` | futures-only |
| VX-03 | `_VX_03_LongVolatilityIn` | futures-only |
| VX-04 | `_VX_04_PostSpikeNormalisation` | futures-only |
| VX-05 | `_VX_05_LongVolatilityTail` | futures-only |
| VX-06 | `_VX_06_VIXTermStructure` | futures-only |
| VX-07 | `_VX_07_VVIXGateModule` | futures-only |
| VX-08 | `_VX_08_VIXStretchES` | futures-only |
| YV-01 | `_YV_01_X30mBalanceVA` | futures-only |
| YV-02 | `_YV_02_CrossIndexFlow` | futures-only |
| YV-03 | `_YV_03_MomentumBurstPradeep` | stock-only |
| YV-04 | `_YV_04_EnvironmentLocationConfirmation` | futures-only |
| YV-05 | `_YV_05_AbsoluteParticipationGate` | futures-only |
| YV-06 | `_YV_06_SessionStopAfter` | both |
| YV-07 | `_YV_07_ICTMacroWindow` | futures-only |
| YV-08 | `_YV_08_DailySweepStrategy` | futures-only |
| YV-09 | `_YV_09_TurtleDonchianBreakout` | both |
| YV-10 | `_YV_10_LabelledMarkovRegime` | both |
| YV-11 | `_YV_11_X9EMA20` | both |
| YV-12 | `_YV_12_PriorDayHigh` | futures-only |
