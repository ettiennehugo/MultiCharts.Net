---
title: "Missing-functionality module specs (PRICEACTION, M-AVWAP, M-MTF, M-TOD, M-DAILY, M-XMKT, M-TICK, M-PORT)"
subtitle: "MultiCharts .NET (C#) · dxFeed data (unadjusted @ES continuous) · IBKR execution — rev 4, 2026-10-21 (ET)"
---

# Changelog

| Rev | Date (ET) | Change |
|---|---|---|
| 1 | 2026-10-21 12:03 | First draft (Analyst 2). |
| 2 | 2026-10-21 | Incorporated Analyst 1 review (`specs/missing-functionality-review-analyst1.md`): added **M-TICK**; listed M-GMM, M-GEX, M-COST, VP helpers as deferred/out of scope; **roll handling** for unadjusted `@ES` (VPF-4 detector, roll-gap removal, AVWAP invalidation, ATR reset, PT+IBKR symbol mapping); **zero new `[Input]`s** — all settings are protected properties (`PriceActionConfig` etc.); `ETraceLevel.Journal` facts with times, not bar numbers; per-FlowQuality-tier delta-z norms; unsupported MC claims turned into probes (function-object laziness, `FullSymbolData.Current` → `CurrentBar`, bar-end stamping, per-series MaxBarsBack); GV statement corrected (UM p488); PMMS fed by named values; PG p53–54 IOG-off caveat; `EVwapType` extended only with Week/Month/Quarter/RollDate, no `AnchoredEvent`/`AvwapMode`, event manager in `_CustomSignal` via protected `AvwapEvents`; `_PriceActionEngine` rev 2 fixes; 1m/15m acceptance trace; extended Data2 probe. |
| 3 | 2026-10-21 | **M-SWING renamed PRICEACTION** and widened to the full PriceAction scope; all items of `specs/priceaction-improvements.md` (H1–H9, M1–M10, L1–L6) folded into §1 with a review-item → engine → test mapping (§1.7); engine file `_PriceActionEngine.Function.CS` **supersedes `_SwingEngine.Function.CS`** (removed); `SwingConfig` → `PriceActionConfig` (protected, `enabled=false`); cross-refs (AVWAP lazy dependency, XMKT leader swing, §0 examples) updated. `/workspace/PriceAction` untouched. |
| 4 | 2026-10-21 | User decisions: PRICEACTION defaults stay at PriceAction-library parity; added `EPriceActionPreset {Library, Futures}`, `PriceActionConfig.ApplyPreset()`, `PriceActionConfig.Futures()/Library()` (§1.1); `_VolumeProfileFuturesBase` uses the Futures preset (specified in §1.8, code change deferred until `_CustomSignal` hosts `PriceActionConfig`); Q5 and Q6 resolved; library fixes submitted upstream as PRs #1–#10 (§1.9). |

Source module list: `trading-idea-catalogue.md` lines 165–178. Citations: **PG** = MC .NET Programming Guide v1.0 (`mc-docs/01M5BF386FDT0VH0AYQBMXHEF2.pdf`, printed pages), **PT** = Portfolio Trader Manual (`mc-docs/01M5BF39CB6Q6Z75MKY9GA3DF8.pdf`), **UM** = MC User Manual. Base rules: `reviews/analyst-base-class-spec.md` rev 2.7 (D7 minimal inputs; validate once in StartCalc; VPB-9 helper-file convention, `_VpVwapEngine.Function.CS`).

## Scope: covered, deferred, out of scope

| Item | Status |
|---|---|
| PRICEACTION, M-AVWAP, M-MTF, M-TOD, M-DAILY, M-XMKT, M-TICK, M-PORT | Specified here |
| **M-NEWS** | **Deferred** (user). `NewsBar` anchor reserved and inert; blackout ideas stay gated (F-TM entry window off for those); `_EconomicCalendar` port is a later item |
| **M-GMM** (bootstrap, KDE sweep, GMM/BIC, entropy, KL; 9 ideas) | **Deferred / next batch** — those ideas stay gated |
| **M-GEX** (external daily file; 1 idea) | **Deferred**; when done, as an ASCII QuoteManager symbol added as Data n (M-XMKT/M-DAILY stream) |
| **M-COST** | **Not a module** (rev 2: CS-2 removed). Mapping: MC native Commission/Slippage + the optional cost filter (Rev 2.2 Q1) + G0-a done offline from the journal (dxFeed signal price vs IB fill, Rev 2.7 §A6) |
| VP-base helpers (range profile [t0,t1], weekly profile stream, per-bar profile ladder, entropy/Parkinson/V(p)/T(p), A2-33 harness) | **Out of scope** of this document — VP-base items |

# 0. Cross-cutting rules

## 0.1 Lazy evaluation (all engines)

1. **Shape.** Every lazily needed engine is a **plain internal sealed class** (VPB-9 convention) created in `StartCalc()` only when required and otherwise held as `null`. This includes all new engines: `_PriceActionEngine`, `_AvwapManager`, `_TodNormaliser`, `_DailyContext`, `_XmktLeg`, `_TickStream`. Whether an *un-accessed* `FunctionSeries` still runs CalcBar is **not documented** (PG p34 only says create and address the value) → **Probe P1**. Until P1 is answered, existing `_ATR`/`_VWAP` function objects are created in `Create()` only for always-on features; nothing lazy depends on a function object.
2. **Enable signal = code, not inputs.** Each engine has a protected configuration property with a disabled default, set by a derived strategy in its ctor or StartCalc (`PriceActionConfig.enabled`, `AvwapEvents`, `TodMetrics`, `DailyConfig`, `XmktLegs`, `TickConfig`). **No new `[Input]` on `_CustomSignal`** (D7: inputs show on all 14 derived strategies and can reset saved workspaces). A strategy that wants to sweep a setting exposes its own `[Input]` or uses the `_TestN` `Variant` index.
3. **Dependencies are lazy and transitive:** enabling M-AVWAP swing/BOS anchors enables (creates) a PRICEACTION instance (`_PriceActionEngine`, candles off unless the strategy enables them); climax anchors enable M-TOD Volume; nothing else.
4. **Lifecycle.**
   ```csharp
   protected PriceActionConfig PriceActionConfig { get; } = new PriceActionConfig();   // enabled = false
   protected override void StartCalc() {
       base.StartCalc();
       if (PriceActionConfig.enabled) {
           m_pa = new _PriceActionEngine { config = PriceActionConfig,
               trace   = TraceLevel >= ETraceLevel.Debug   ? s => trace(ETraceLevel.Debug, s)   : (Action<string>)null,
               journal = TraceLevel >= ETraceLevel.Journal ? s => trace(ETraceLevel.Journal, s) : (Action<string>)null };
           string err = m_pa.Configure();           // validate once; report, never clamp
           if (err != null) throw new Exception(err);
       } else m_pa = null;
   }
   protected override void CalcBar() {
       if (m_pa != null) { if (rollOn(PriceActionConfig.dataStream)) m_pa.OnRoll(BarsOfData(PriceActionConfig.dataStream).Time[0]);
                              m_pa.Step(BarsOfData(PriceActionConfig.dataStream)); }
   }
   ```
   Accessors return neutral values (`null`, `NaN`, `Unknown`, empty list) and log once if a rule queries a disabled/unconfigured engine.
5. **Per-bar gating:** commit only on `Bars.Status == EBarState.Close` (PG p20, p35). Dedupe per stream with `bars.CurrentBar` (documented, PG p20). `FullSymbolData.Current` is **not** used (undocumented as an index; FullSymbolData also forces full-series load, PG p17).
6. **StopCalc:** engines → `null`; re-calc starts clean. `requireDataStream()` (existing) checks streams only for enabled engines.

## 0.2 Logging / journal

`ETraceLevel.Journal` facts (appear with the per-trade journal): swing confirmations, roll resets, AVWAP anchor create/expire/invalidate, MTF first mapping per session, XMKT/TICK stale-leg events, TOD tier resets. Always stamped with **bar times** (`yyyy-MM-dd HH:mm:ss`, chart time zone), never bar indexes (indexes are per stream). Per-bar detail stays at `Debug`; the owner passes a `null` sink when the level is filtered so no strings are formatted. Trade journal lines carry the AVWAP tag `AV:{type}:{anchorTime:yyyyMMddHHmm}` and the swing `confirmTime` used.

## 0.3 Roll handling (unadjusted dxFeed `@ES`, rev 2.7)

Continuous data is **unadjusted, always** (Rev 2.2 Q3). `Bars.Info.Name` does not change on `@ES`, so roll is detected with the **rev 2.7 VPF-4 detector**: session-open gap > k×ATR **and** the date is in the known dxFeed roll-date table. The base exposes `rollOn(ds)` = true on the first bar of the new contract for stream ds. On a roll:

- `_PriceActionEngine.OnRoll()`: clear swings and structure (old price space), reset the TR/ATR ring, skip the splice-bar TR.
- `_DailyContext`: roll-gap removal — difference-adjust prior closes for level series (SMA200, prev H/L/C), ratio for returns (TSMOM, RSI2 change), skip the splice day's TR in ATR20d; `GapPct` uses the adjusted prior close.
- `_AvwapManager`: **invalidate** (not shift) every live anchor of every type; journal `AV invalidated (roll)`.
- `_TodNormaliser`: Range/TR samples of the splice bar are dropped.
- Any Wilder ATR (Daily) restarts its seed.
- **Execution:** Portfolio Trader / chart auto-trading uses IBKR with **Symbol Mapping** of each `@` symbol to the IB front contract, re-mapped at each roll (rev 2.7 §E8).

## 0.4 Recalculation consistency (PG p53–54)

On multi-data charts a signal is calculated on any series' bar close (with IOG, every tick of any series). **With IOG off, orders go out only at Data1's Open tick and "the last calculation before the orders sending at Open is considered valid" (PG p54)** — an entry decided on a Data2-only calc is overridden by the next calc. Rules must therefore be **idempotent per Data1 bar**: derive entry conditions only from closed-bar state that does not change between calcs on the same Data1 bar.

# 1. PRICEACTION — price-action engine (formerly M-SWING)

**Renamed** from M-SWING in rev 3 and widened to cover the whole PriceAction library scope (candles + pivots + structure + breakouts). **File:** `/workspace/MultiCharts.Net/_PriceActionEngine.Function.CS` (DRAFT rev 3, uncompiled; must pass CS-0 helper-file compile, C# 7.3). It **supersedes `_PriceActionEngine.Function.CS`**, which was removed (it was never in `PLStudies.csproj`); the Coder must add `<Compile Include="_PriceActionEngine.Function.CS" />` when compiling. The user's library `/workspace/PriceAction` is reference only and is **not modified**.

**Origin.** `github.com/ettiennehugo/PriceAction` (`PriceActionAnalysis.cs`). The review `specs/priceaction-improvements.md` (items H1–H9, M1–M10, L1–L6) is folded in below; §1.7 maps every item to the engine.

## 1.1 Types and configuration

- Types: `EPivotMode {Fractal, AtrZigZag}`, `EPivotTieRule {Strict, LeftInclusive}`, `EPivotLabel {None, HH, LH, HL, LL}`, `EStructure`, `[Flags] ECandle`, `PriceActionConfig`, immutable `_Pivot`, internal `_Ring<T>`, `_PaBar`, `internal sealed class _PriceActionEngine`.
- **`PriceActionConfig`** (protected property on `_CustomSignal`, no `[Input]`; sweep via the strategy's own input or `_TestN` Variant): `enabled=false, dataStream=1, pivotMode=Fractal, tieRule=Strict, layers=2, strength=1, atrLength=20, volumeLength=20, volumeIncludesCurrent=true, atrMultiple=1.5, factorDoji=0.1, factorClose=0.1, factorStrong=1.0, factorEpsilon=0.05, equalFactor=0.1, lookbackPivots=5, minBreakoutLayer=1, breakoutNeedsStrong=true, history=50, candles=true`. Defaults equal PriceAction's constants.
- **Presets (rev 4):** `EPriceActionPreset { Library = 0, Futures = 1 }`. `Library` (default) = PriceAction-library parity: `tieRule=Strict`, `volumeIncludesCurrent=true`. `Futures` = `tieRule=LeftInclusive` (flat tops on tick-rounded prices form pivots), `volumeIncludesCurrent=false` (volume norm from prior bars only). Switch with one call: `PriceActionConfig.ApplyPreset(EPriceActionPreset.Futures)` or start from `PriceActionConfig.Futures()`; the preset only touches the fields it owns, so later per-field overrides still apply.
- `Validate()` returns all bad values (ranges as in PriceAction setters, plus `minBreakoutLayer < layers`, `history ≥ lookbackPivots+1`); owner throws in StartCalc. Config is frozen after `Configure()`.

## 1.2 Lifecycle, laziness, MC integration

- Created in `_CustomSignal.StartCalc()` only if `PriceActionConfig.enabled` **or** a lazy dependent requires it (M-AVWAP swing/breakout anchors, M-XMKT leader swing); otherwise `null` (§0.1). One instance per (stream, config).
- `Step(IInstrument bars)`: ignores ticks unless `bars.Status == EBarState.Close`; dedupes by `bars.CurrentBar`. `Add(barNumber, time, o, h, l, c, v)` is the MC-independent entry point used by tests; out-of-order bar numbers are rejected (warn once); bars are keyed by **bar number, never DateTime** (same-second bars on tick/seconds charts are distinct).
- Roll: owner calls `OnRoll(time)` on the VPF-4 roll bar before `Step` → window, TR/ATR and volume rings and pivots cleared, splice-bar TR skipped; sequence numbers keep increasing (§0.3).
- Sinks: `journal` (ETraceLevel.Journal: pivot confirmations with seq/label/EQ, breakouts, BOS/CHoCH, roll resets; times only), `trace` (Debug: per-bar candle flags), `warnOnce`; owner passes `null` when the level is filtered.

## 1.3 Indicators (O(1), no per-bar allocation)

- **ATR** = simple average of TR over `atrLength` (PriceAction/`AvgTrueRange` parity), running sum over a ring. `Atr` = NaN until warm; `IsWarm` = both TR and volume rings full.
- **Volume** mean and population σ from running Σv, Σv² over `volumeLength` (parity). `volumeIncludesCurrent=false` computes the norm from prior bars only. Optional `volumeZOverride` (e.g. M-TOD clock-minute volume z) replaces the internal z — fixes the "opening bars always +2SD" bias.
- Storage: fixed `_Ring<T>` buffers sized to the real lookback (bar window = 2·strength+1, TR/volume rings, `history` pivots per layer per side). The only per-bar allocation is one `_Pivot` per confirmed pivot.

## 1.4 Candle patterns (pure static `ClassifyCandle(bar, prior, atr, config)`)

Evaluated on the closed bar only. Rules (Brooks, *Trading Price Action Trends* p.89, where cited):

| Flag | Rule (ATR-scaled: near = factorClose·ATR, eps = factorEpsilon·ATR) | Change vs PriceAction |
|---|---|---|
| Bull / Bear | close > / < open | — |
| Doji | \|o−c\| ≤ factorDoji·ATR | — |
| **BodyAtHigh** (was `highPin`) | range > 0, body/range < `SMALL_BODY_RATIO` (1/3), high − body top < eps → hammer, long lower tail | renamed; named constant; body top = max(o,c) for both colours |
| **BodyAtLow** (was `lowPin`) | mirror → shooting star, long upper tail | renamed |
| Inside / Outside | strict vs prior high/low | — |
| Engulfing | body engulfs prior **range** (o < prior low and c > prior high, mirror for bear) | documented as range-engulfing |
| BullReversal | prior bear, this bull, not doji, ≥ 2 of: (1) open ≤ prior close + near and close > prior close; (2) lower tail ∈ [1/3, 1/2]·range and upper tail ≤ near; (5) close > prior high. Rules 3–4 not implemented | rule 2 fixed (PriceAction accepted a zero tail); split bull/bear flags |
| BearReversal | mirror; rule 1 = open ≥ prior close − near and close < prior close − near ("well below") | rule 1 fixed (PriceAction used prior open) |
| Strong | body ≥ factorStrong·ATR, opens within near of one extreme, closes within near of the other | — |
| Vol* | z = (v − mean)/σ (or override): AboveAvg z>0, Plus1/2/3SD, BelowAvg, Minus1/2/3SD | z-based; σ=0 → no volume flags |
| BreakoutUp / Down | see §1.6 | fixed |

`Candle` = flags of the last closed bar; `Changed` = flags that differ from the previous bar ([Flags] XOR), replacing the noisy `StateChanged()`.

## 1.5 Pivots, labels, structure

- **Fractal** layer 0: mid bar of a 2·strength+1 window; `tieRule=Strict` (`>`/`<`, PriceAction parity — adjacent equal highs never pivot) or `LeftInclusive` (`mid ≥ left && mid > right`, catches flat tops). **AtrZigZag**: starts undecided, tracks both extremes, direction set by the first ≥ atrMultiple·ATR move.
- Layer k: middle of the last three confirmed layer k−1 same-side pivots (same tie rule), confirmed on the bar that confirmed the newest of the three — no lookahead.
- Every pivot is **immutable**: `seq` (per layer and side, assigned at confirmation, monotonic across rolls, **never renumbered** — replaces `renumberPivots`), price, extremeTime/Bar, confirmTime/Bar, label, isEqual, equalToSeq, legSizeAtr.
- **Labels on every layer**: always HH/LH (highs) or HL/LL (lows) vs the previous same-side pivot; ties → LH/LL. No threshold gap (PriceAction required |Δ| > factorClose·ATR, leaving pivots unlabelled).
- **Equal / double top/bottom:** `isEqual` if within equalFactor·ATR of any of the last `lookbackPivots` same-side pivots of that layer; `equalToSeq` names the match. **The earlier pivot is never modified**, and HH/LH is kept.
- Structure per layer: Up (HH+HL), Down (LH+LL), else Range.

## 1.6 Breakout and BOS

- **Breakout** (candle flag): bar must be Strong (if `breakoutNeedsStrong`) and Bull/Bear; **close** beyond the last pivot of a layer ≥ `minBreakoutLayer` (default 1) whose `seq` has **not been broken yet**; checked from the highest layer down; each pivot breaks once. Fixes PriceAction's "every strong bar is a breakout with ≤ 2 layers" and its skipping of layers 0–1.
- **BOS** (layer 0, any bar): close beyond the last swing high/low not yet broken; **CHoCH** = BOS against the structure as it was before the break. 60-min BOS ideas use an instance on the Data2 stream.

## 1.7 Review-item mapping and tests

Unit tests run `Add()` with CSV fixtures (no MC needed): `es_1m_normal.csv`, `es_1m_roll_day.csv` (unadjusted splice), `flat_tops.csv`, `gaps.csv`, plus hand-built 5–10-bar cases. Golden files = journal lines.

| Review item | How the engine addresses it | Test / acceptance |
|---|---|---|
| H1 non-pivots in pivot lists | Pivots appended only when confirmed, on closed bars; never on a flag flip | Tick-replay of a bar that makes then un-makes a high: pivot list unchanged |
| H2 constant `index` after 500 bars | Bar number supplied by caller (`CurrentBar`); pivot `seq` monotonic | 2000-bar run: bar numbers and seq strictly increasing |
| H3 forming-bar repaint | Commit only on `Status == Close`; no provisional state | Closed-bar replay == IOG tick replay == playback (identical journal) |
| H4 `renumberPivots` asymmetry/index-0/rewrite | Removed; immutable `seq` at confirmation | Pivot fields never change after creation (snapshot compare over the run) |
| H5 double-top relabels older pivot | `isEqual`+`equalToSeq` on the new pivot only; labels kept | Two equal highs: first keeps HH/LH, second has isEqual and equalToSeq = first.seq |
| H6 breakout bugs | Explicit rule §1.6, unbroken-pivot tracking | layers=2: strong bar without close beyond pivot → no breakout; same pivot cannot break twice |
| H7 `Clear()` stale state | `Reset()` clears every field | Reset mid-run == fresh engine on remaining bars |
| H8 `Size` setter leaves stale pivots | No window-size setter; rings sized at Configure, config frozen | n/a (API removed); Configure twice → independent state |
| H9 out-of-order / duplicate timestamps | Keyed by bar number; out-of-order rejected with warning | Two bars with equal time, different numbers → both processed; decreasing number → ignored + warn |
| M1 O(n) ATR/volume, allocation | Running sums over rings | ATR/volume equal a naive recompute within 1e-9; allocation profiler: 0 B/bar excluding pivots |
| M2 SortedList RemoveAt(0) | `_Ring<T>` fixed buffers | Benchmark 1M bars: linear time |
| M3 no roll handling | `OnRoll()` resets; splice TR skipped | Roll-day fixture: ATR after roll equals ATR of new contract only; no pivot spans the splice; journal "roll reset" |
| M4 silent setters | `Validate()` reports all; config frozen | Each bad value → error text contains its name |
| M5 HH/LH threshold gap; layer 0 only | Always labelled, per layer, ties documented | Equal-price pivot → LH and isEqual; layer 1 labels present |
| M6 strict comparator | `EPivotTieRule` option, Strict default | `flat_tops.csv`: Strict → no pivot, LeftInclusive → pivot at left bar |
| M7 pin naming, magic constant, reversal rules | `BodyAtHigh/Low`, named constants, corrected rules 1–2 | Hand-built bars for each rule (zero-tail bull → not rule 2; bear close between prior close and prior open → not rule 1) |
| M8 mutable public API | Read-only properties, immutable `_Pivot`, no collections exposed | Compile-time (no setters) |
| M9 no tests / order dependency | Pure `ClassifyCandle`; `Add()` entry; fixtures above | Test project (Coder) |
| M10 DLL deployment, no MC awareness | Source helper file in PLStudies, `Step(IInstrument)` adapter | CS-0 compile; MC acceptance trace below |
| L1 stale comment | n/a in new code | — |
| L2 generic exception | Owner throws with the Validate text | — |
| L3 population σ, no TOD | Parity kept, documented; `volumeZOverride` hook | z override wired to M-TOD in one strategy |
| L4 noisy StateChanged | `Changed` = flag XOR | — |
| L5 ATR 0 before warm-up | `Atr` NaN, `IsWarm` | First atrLength bars: NaN |
| L6 duplicated high/low code | Shared `beats()` / side-parameterised paths | Code review |

**MC acceptance trace (VPB-8):** Data1 ES 1-min, Data2 ES 15-min, both Globex 24h; instances on ds1 and ds2, Journal on. Check: (a) each ds2 pivot `confirmTime` is a 15-min close and ≤ the Data1 bar time where it first appears (HTF pivot visible at 10:07 comes from the ≤ 10:00 15-min close); (b) no pivot re-emitted or changed; (c) IOG on/off, backtest vs playback give identical journals; (d) roll date shows "roll reset" and no pivot straddles it; (e) layer-0 Strict pivots on ds1 match PriceAction's layer-0 pivots on the same closed bars, except the documented differences (no renumbering, no relabel, corrected candle rules).

## 1.8 Futures defaults in `_VolumeProfileFuturesBase` (rev 4)

`_VolumeProfileFuturesBase` (`/workspace/MultiCharts.Net/_VolumeProfileFuturesBase.Strategy.CS`, user code; a different copy exists in `/workspace/mc-strategies`) must use the Futures preset. **Specified, not edited:** `PriceActionConfig` does not exist on `_CustomSignal` yet, so editing the base now would not compile. The minimal change, to be applied together with the `_CustomSignal` integration:

```csharp
// _VolumeProfileFuturesBase.Create() (or ctor), after base.Create():
PriceActionConfig.ApplyPreset(EPriceActionPreset.Futures);   // enabled stays false: still lazy until a derived strategy enables it
```

Derived futures strategies only set `PriceActionConfig.enabled = true` (plus any per-field overrides). Non-futures strategies keep library parity.

## 1.9 Upstream PriceAction library (rev 4)

The user's library is still not modified by this spec. With the user's authorisation, the review items were submitted as separate PRs against `ettiennehugo/PriceAction` master. They are not merged; no direct pushes to master.

| PR | Items | Title |
|---|---|---|
| #1 | M9 | Add xUnit test project with fixtures |
| #2 | M7 | Correct reversal-bar rules and name pin/reversal constants |
| #3 | H1, H2 | Fix non-pivots left in pivot lists and frozen bar index |
| #4 | H4 a/b | Fix renumberPivots high/low asymmetry and skipped index 0 |
| #5 | H6 | Fix breakout detection |
| #6 | H7, H8, H9 | Fix Clear() stale state, Size setter stale pivots, out-of-order bars |
| #7 | M1, L5 | Incremental ATR/volume statistics; IsWarm |
| #8 | M4, M6, L2 | Setter validation; optional PivotTieRule (default Strict) |
| #9 | M3 | NotifyRoll for unadjusted continuous futures |
| #10 | H3, H5 | [BREAKING 2.0] Closed-bar commit model; no double-top relabel |

Not submitted: H4c (fixed sequence numbers instead of renumbering), M2 (ring-buffer storage), M8 (read-only collections; breaking), M10 (MC packaging), L1/L3/L4/L6. The MC engine `_PriceActionEngine` already covers these.

# 2. M-AVWAP — event-anchored VWAP vs F-VWAP

**Relation.** Same math, one accumulator type (`_VpVwapEngine`: typical price (H+L+C)/3, ΣPV, ΣP²V, ΣV, σ = √(ΣP²V/ΣV − VWAP²), provisional/committed split for IOG).

- **F-VWAP** = up to **3 user-configured slots** on `_VolumeProfileBase` (`Vwap1..3Type`, existing inputs), static or calendar anchors.
- **M-AVWAP** = a **code-configured set of event anchors** with lifecycle (age, touch, expiry, tags), `_AvwapManager`, homed in **`_CustomSignal`** (it needs no VP data, so non-VP strategies can use it).
- `_VWAP.Function.CS` stays as-is for indicators.

**`EVwapType` answer.** Extend it only for single-anchor calendar resets; **values 0–3 unchanged**, no new input:
`EVwapType { Disabled=0, Session=1, Length=2, AdaptiveFromDate=3, Week=4, Month=5, Quarter=6, RollDate=7 }`.
Week/Month/Quarter reset at the first bar whose exchange-session date is in a new week/month/quarter; RollDate resets on the VPF-4 roll bar. Implemented as a reset predicate in `_VpVwapEngine`. `Day` is omitted (identical to `Session` on the 24h Globex template). **No `AnchoredEvent` value and no `AvwapMode` input**: a slot holds one value (`CurrentVwapN`), N dynamic anchors do not fit, and event anchors are rule-driven.

**Enabling:** protected `AvwapEvents` (`[Flags] EAvwapAnchor`, default `None`) + protected `AvwapLimits { maxAnchors=6, maxAgeSessions=10, replaceSameType=true, bandSigmas={1,2,3}, slopeBars=5, dataStream=1 }`. None → manager is null.

**Anchor events:**

| Event | Definition | Dependency |
|---|---|---|
| `SwingHigh` / `SwingLow` | PRICEACTION confirmed swing; anchor at the extreme bar; ΣPV from extreme to confirmation back-filled from a per-bar PV ring kept by the manager (ring length = swing strength + 1); usable only from `confirmTime` | lazily enables PRICEACTION (ds of the manager) |
| `Breakout` | PRICEACTION layer-0 BOS bar (`BosUp/BosDown`) or a `BreakoutUp/Down` candle flag (§1.6) | PRICEACTION |
| `GapOpen` | first RTH bar with \|open − prior RTH close\| ≥ 0.5 × `_DailyContext.Atr20d`; fallback if Daily not valid: ≥ 3 × Data1 SMA-TR(20) | M-DAILY if enabled |
| `VolumeClimax` | bar volume z ≥ 3 by M-TOD Volume (clock-minute norm); fallback while M-TOD not valid: 20-bar volume z ≥ 3 | lazily enables M-TOD Volume |
| `WeekOpen` / `MonthOpen` / `QuarterOpen` | first bar of the period | — |
| `NewsBar` | reserved, inert (M-NEWS deferred) | — |

`RollDate` as an *event* is not needed: a roll invalidates all anchors (§0.3) and calendar RollDate lives in F-VWAP slots.

**Per anchor:** id, type, anchorTime, ΣPV/ΣP²V/ΣV, value, σ, bands ±k σ, slope (ticks/bar and ATR/bar over `slopeBars` closed bars), age (bars, sessions), `touched` + touch count, `expired`, tag.
**Lifecycle:** evict oldest of the same type at `maxAnchors`; expire after `maxAgeSessions`; replace same type when `replaceSameType`; **invalidate all on roll**. Create/expire/invalidate are Journal facts.

# 3. M-MTF — higher timeframes from MultiCharts multi-data

HTF bars come from extra chart series read through `BarsOfData(n)` (PG p19–20), e.g. Data1 = ES 1-min, Data2 = ES 60-min. No internal bar building (supersedes catalogue lines 1119, 2527 — catalogue owner to update).

1. `HtfClosed(ds, ago)`: if `BarsOfData(ds).Status == EBarState.Close` use index `ago`, else `ago+1`. Default for all HTF rules (`closedBarsOnly`, VPB-8). Forming-bar access only via explicit `HtfForming(ds)`, journaled.
2. HTF engines step only on that stream's closed bars (CurrentBar dedupe) — built into each engine.
3. Mapping: for a Data1 closed bar at t, HTF context = latest closed HTF bar with Time ≤ t. Whether MC stamps bars with their **end** time (so the 10:00 60-min bar is closed and usable on the 10:00 1-min bar) is **not in the PG → Probe P2**. First mapping per session is a Journal fact.
4. Recalc consistency per §0.4.
5. Lookback: whether MaxBarsBack applies per series is **not in the PG → unverified (P2)**; for long HTF/daily lookbacks prefer reading history via `FullSymbolData` (PG p22) at StartCalc/once per HTF bar.
6. Session template: reuse the **VPF-3 StartCalc assertion** (24h CME Globex template, ETH bars present) for every stream; additionally assert same `Info.Name` root for HTF streams.

**Probe P2 (VPB-8 acceptance, extended):** Data1 1-min + Data2 15/60-min, IOG on/off, backtest with/without Bar Magnifier, playback, live. Log per CalcBar: which stream triggered (Status per stream), `BarsOfData(2).Time[0]`, `Status`, `Close[0]`, `CurrentBar`. Answers: (a) forming Data2 bar values/Status intrabar; (b) whether bar times are **end** stamps; (c) **ordering** of Data1 and Data2 closes sharing a timestamp; (d) the PG p54 "last calc before Data1 Open is valid" rule in practice; (e) per-series MaxBarsBack. Expected in backtest without IOG: closed HTF bar. Code uses `HtfClosed()` regardless.

# 4. M-TOD — time-of-day normalisers

**Recommendation: separate small lazy engine `_TodNormaliser`.** Reasons: shared non-trivial state (session-minute × 20-session rings per metric) used by many ideas and by M-AVWAP; must be keyed to the exchange session clock (`_CmeSessionClock`) with holiday/half-day handling (`_CmeHolidays`) — one tested implementation; memory is small but non-zero, so lazy; metric-agnostic `(sessionMinute, value)` input.

**Config (protected):** `TodMetrics` ([Flags] Volume, Delta, Range, TrueRange; default None), `TodSessions=20`, `TodMinSessions=10`, bucket = clock minute (or bar-size bucket if Data1 > 1 min).
**Outputs:** `Z(metric)`, `Median(metric)`, `Pct(metric)`, `ZCum(metric)` (session-to-date vs same-minute cumulative norm); norms from prior **completed** sessions only; half-days excluded; commit on Close.
**Delta z per FlowQuality tier:** delta comes from the rev 2.7 chain (MC-BidAsk > Quote > TickRule; BarUpDown never used for signals). Each sample is tagged with its tier; the norm **resets at a tier change** (journaled) and `valid=false` until `TodMinSessions` sessions of the same tier exist. The VPF-5 delta API is still to be implemented.

# 5. M-DAILY — daily/weekly context (approved)

**Source:** daily (optional weekly) series on the chart, same unadjusted `@` symbol, read via `HtfClosed`; history read via `FullSymbolData` where long lookbacks are needed. Fallback without a daily stream: aggregate Data1 at `LastBarInSession`.
**Config (protected):** `DailyConfig { dataStream=0 (=fallback), weeklyStream=0, atrLength=20, smaLength=200, rsiLength=2, tsmomLookbacks={252,126,63} }`; disabled when no consumer sets `NeedsDaily`.
**Outputs:** `Atr20d` (Wilder — documented; seed restarts at roll), `Sma200`, `Rsi2`, `Tsmom(n)`, `PrevHigh/Low/Close`, `PrevRange`, `GapPct`, `PrevWeekHigh/Low`; `valid` false until enough bars.
**Roll-gap removal** per §0.3 (unadjusted data).
**Settlement vs last trade** on dxFeed daily bars: **unverified** → add to rev 2.7 §E live-check list; until then compare dxFeed daily close vs CME settlement for 5 sessions.

# 6. M-XMKT — cross-market bar streams

Extra `BarsOfData(n)` series on the chart (NQ for ES, ZN, VIX …), **bar resolutions** (seconds/minutes). **Tick-level legs use M-TICK, not a chart stream:** per PG p53–54 a tick Data n makes CalcBar fire on every tick of that stream, breaking "zero cost unless used". No custom feeds; Global Variables not used — UM p488: GV pair *backtesting* works only in Portfolio Backtester (GV can pass values between charts in real time), so they are unsuitable for reproducible backtests.

1. Stream map as protected `XmktLegs` (role → ds, `maxStaleSec` default 2× bar size); StartCalc logs `Info.Name`, `Resolution`, `DataFeed` and fails loudly if a needed stream is missing.
2. Alignment: last closed bar of stream n with Time ≤ Data1 time (`HtfClosed`); stale → leg unavailable (Journal fact), no trade on its signal.
3. Each instrument keeps its own session template; measures computed on its own bars, never interpolated.
4. Lazy helper per leg: k-bar log return, rolling correlation/beta, RS-spread z, leader swing (own `_PriceActionEngine`).
5. Orders Data1 only; two-leg trading needs M-PORT.
6. Availability of VIX/ZN etc. depends on the dxFeed subscription bundles (rev 2.7 §E1).

# 7. M-TICK — lazy DataLoader 1-tick engine (new)

**Need:** catalogue ideas at lines 1280, 1291, 2226, 3341–3347 — seconds bursts, inter-print times, trade-size stream.
**Mechanism:** `_TickStream` plain class, created only when a derived strategy sets protected `TickConfig.enabled`. It issues a **DataLoader** 1-tick Trade request for the Data1 symbol (PG p71–75; `Subscribe2RT` for real time), bounded to the dxFeed tick history window (~2 years). No tick chart stream (see §6).
**Outputs** (per Data1 closed bar, from ticks with time in the bar): print count, max prints per second, inter-print time median/p10, burst flag (prints/s z vs M-TOD-style norm), trade-size histogram, large-print count ≥ threshold. Each value carries a FlowQuality tag; missing/late tick data → `valid=false` + Journal fact.
**Gate:** the **per-trade-size ideas stay parked until rev 2.7 §E6** (dxFeed trade-size fidelity through MC DataLoader) is verified; count/timing outputs may be used earlier.
**Recalc/IOG:** tick aggregation commits when the Data1 bar closes; DataLoader callbacks only buffer (thread-safe queue), CalcBar consumes.

# 8. M-PORT — portfolio sizing/throttles via Portfolio Trader

**Cited mechanisms:** portfolio calc of all strategies, one bar at a time, chronological; fills on next Data1 open (PG p57–58, §4.6.10). Money management: Initial Capital, Exposure %, Max % Capital at Risk per Position, Margin per Contract %, Potential Loss per Contract; `Portfolio.SetMaxPotentialLossPerContract(...)`; `IPortfolioPerformance` incl. `CalcMaxPotentialLossForEntry(side, contracts, price)` (PG p59); buying-power example and contract cutting (PG p60–62). PMM signal runs after all strategies each step, on the first instrument, and reaches other strategies only through `pmms_*` functions (PT p9–11, p17). `PortfolioEntriesPriority` is **not used** (PT p10: legacy, conflicts with PMM).

**Design:**

1. Each strategy publishes, per bar, **named values** (`pmm_set_my_named_num`, PT p17): `ret1d` (daily return of its Data1, published at session close), `riskOpen` (open risk $), `riskNew` (risk of a pending entry = stop distance × BigPointValue × contracts), `family` (numeric id), `ideaId`; plus `Portfolio.SetMaxPotentialLossPerContract(stopDistance × BigPointValue)`.
2. `_PortfolioRiskManager` PMM signal (`_PortfolioRiskManager.Strategy.CS`, idle if only one strategy) reads them with `pmms_get_strategy_named_num` and enforces: max open risk % of equity; per-instrument and per-family caps; **ρ ≥ 0.9 book rule** — ρ computed from the published `ret1d` series (rolling 60 sessions), strategies above the threshold share one risk budget (ES/MES/NQ as one); daily book loss stop → deny entries for the session; max concurrent positions.
3. Live: Portfolio Trader auto-trading via IBKR with per-roll Symbol Mapping (§0.3). Single charts: M-PORT inactive; base sizing applies.
4. Constraints: no Bar Magnifier in Portfolio Trader (BM p6) → intrabar-sensitive ideas need fine Data1; portfolio start = first instrument start (PT p10).
5. The .NET PMM base class / `pmms` equivalents are **not in the 2013 PG** → Probe P5 before any M-PORT code (check MC .NET editor PowerLanguage .NET help).

# 9. Probes and open questions

| # | Question | Status |
|---|---|---|
| P1 | Does an un-accessed `FunctionSeries` run CalcBar? (counter probe) | Open; design does not depend on it |
| P2 | Data2 forming-bar Status/values, bar-end stamping, same-timestamp ordering, PG p54 rule, per-series MaxBarsBack (§3) | Open; VPB-8 acceptance |
| P3 | dxFeed daily bar = settlement or last trade | Open; rev 2.7 §E list |
| P4 | rev 2.7 §E6: trade-size fidelity via DataLoader (gates M-TICK size ideas) | Open |
| P5 | .NET PMM signal API and named-value functions | **Resolved 2026-10-21** (§11, `m-port-spec.md`); live checks → P5b |
| Q1 | VPF-5 delta API for M-TOD delta z | To implement (rev 2.7) |
| Q2 | VPF-4 roll-date table source/maintenance and k for the gap test | Per rev 2.7; confirm values |
| Q3 | `_PriceActionEngine` must pass CS-0 compile; run the §1 acceptance trace | Coder |
| Q4 | Catalogue text for M-MTF ("build HTF internally") to be updated by its owner | Catalogue owner |
| Q5 | PRICEACTION defaults | **Resolved (rev 4):** library parity by default; Futures preset used by `_VolumeProfileFuturesBase` (§1.1, §1.8) |
| Q6 | Port the fixes back to the PriceAction library | **Resolved (rev 4):** submitted as upstream PRs #1–#10 (§1.9), not merged |
| Q8 | Merge order of upstream PRs #1–#10 (independent branches off master, small textual conflicts in `PriceActionAnalysis.cs`); whether to take the 2.0 PR #10 | User |
| Q7 | Add `<Compile Include="_PriceActionEngine.Function.CS" />` to `PLStudies.csproj` when the draft is compiled (not done, to keep the build green) | Coder |

# 10. Review status

Reviewed by Analyst 1: see `specs/missing-functionality-review-analyst1.md` (2026-10-21). All points in its §1–§6 are adopted in rev 2 (see changelog).

# 11. Update 2026-10-21: Probe P5 resolved

The user supplied the MultiCharts .NET portfolio API metadata (`specs/mc-portfolio-api.txt`): `PortfolioSignalObject` (base of `_CustomSignal`) exposes `PortfolioStrategies : IROList<IPortfolioStrategy>`, `MyPortfolioData`, `CommonPortfolioData` (`IPortfolioData`: `object this[string]`, `ContainsKey`) and `PortfolioCurrency`; `IPortfolioStrategy` has `Signals`, `Paused`, `RiskCapital`, `Status`, `EntryContracts` (<0 code size, 0 Strategy Properties, >0 override), `AllowEntries/Long/Short`, `AllowExits/Long/Short`, `PortfolioData`, `MyIndex`, `ForceClosePosition()`, `PartialClosePosition(isNextBar, contracts)`; `IStrategy` gives `PositionSide`, `CurrentPosition`, `Positions`, `Portfolio` and trade stats.

**P5 status: resolved.** §8 (M-PORT) is **superseded by `specs/m-port-spec.md`** (rev 1), which @Coder builds from. Differences from §8: named values are `IPortfolioData` objects, not `pmm_*` numbers; no `pmms_calc_*` helpers exist in .NET (coded in `_PortMM.Function.CS`); sizing is centralised via `EntryContracts` (default 0 = Strategy Properties size, so the MM sets −1 or an explicit size); `Paused` is assumed to block exits too (flatten first); IOG and Bar Magnifier are unavailable in Portfolio Trader. Remaining live checks are **Probe P5b** (L1–L9 in `m-port-spec.md` §9). The six PT example signals named by the user are not in the repo; the spec uses the MC wiki originals and `_MarketBreadth.Strategy.CS`.
