---
title: "Live-check sheet: dxFeed / MultiCharts .NET / IBKR (ES futures)"
subtitle: "For Ettienne Hugo · repo MultiCharts.Net main @ 6450a9f (PRs #18–#20 merged) · 2026-10-22 (ET)"
---

# How to use this sheet

- **Setup:** MultiCharts .NET with dxFeed data (QuoteManager), IBKR as **broker only**, continuous **unadjusted** `@ES`, Portfolio Trader (PT).
- **Sources:** `specs/missing-functionality-specs.md` (MFS), `missing-functionality-review-analyst1.md` (A1), `m-port-spec.md` (M-PORT, §9 = P5b L1–L9), `phase3-specs.md` (P3S), `phase3-review-analyst2.md` (A2R), `trading-idea-catalogue.md` (CAT), and the repo.
- **Rev 2.7 §E references.** The specs cite "Rev 2.7 §E1/E2/E5/E6/E8" from `reviews/analyst-base-class-spec.md`. That file is **not in this workspace**, so the §E labels below come from how MFS and CAT quote them.
- **UI paths.** Paths marked **[MC wiki]** were checked against multicharts.com / the MC wiki on 2026-10-22. Paths marked **(confirm in your MC version)** could not be verified there, so only the goal is given.
- **Run IDs.** Before you start, set `TraceLevel = Journal` on any probe strategy. The CS-1 log goes to `%LOCALAPPDATA%\MCSignalLogs\MCSignal<stamp>-<pid>.csv` (fallback: `%TEMP%`), per `_CustomSignal.Strategy.CS` l.75–88. The trade journal goes to `MCJournal_<runId>.csv`.
- **Facts already published by MC/dxFeed** (MC wiki *DxFeed* page): up to **2 years of tick data**, **9 years of minute bars** and **40 years of daily bars**. Example given: NQ minute data from **17 Jun 2016**, tick data from **1 Jul 2024**. Futures are **not back-adjusted**. Ask/Bid history exists **only at tick level**. An MC forum reply (MC staff, quoting dxFeed) says equity-index continuous rolls follow the **CME Equity Index Roll Dates**. Checks 4 and 8 confirm these numbers on your own account; they are not re-derived here.

---

# Check 1: dxFeed trade sizes, aggregated or per trade (Rev 2.7 §E6)

**Purpose / unblocks.** Turns on the M-TICK size outputs (`TickConfig.sizeOutputsVerified`, `_TickStream.Function.CS` l.25, now `false`). Ideas parked on this flag: the large-print / trade-size ideas at CAT l.1379–1384, the A2-38 trade-size entropy (CAT l.2371) and the "big trade" arm (CAT l.3527). CAT l.1383 warns that if CME prints arrive aggregated per aggressor order, "large trades" are not institutional fills.

**Steps**

1. Pick a liquid RTH minute (for example 09:30:00–09:31:00 ET) on a recent day inside the tick window. Use the **front contract** (for example `ESZ26` in dxFeed naming, see Check 3), not `@ES`, so that no roll logic is involved.
2. Open a **1-tick chart** of that symbol from dxFeed. [MC wiki: *File → New → Chart Window*, Settings tab → Resolution.]
3. Export or read the ticks for that minute: time, price, size. The ways to do this are the Data Window or the QuoteManager *Edit Data* / export feature (**confirm in your MC version**), or a 10-line `_TickStream` journal run with `TickConfig.enabled = true` (the DataLoader 1-tick Trade request, CS l.794 prints `TICK DataLoader 1-tick Trade request …`).
4. Pull the same minute from an independent per-trade source if you have one (CME DataMine/MDP 3.0 time & sales, or an IBKR TWS Time & Sales window). IB's tape is itself sampled, so treat it only as a sanity check.
5. Compare: (a) the number of prints, (b) Σ size, and (c) whether there are runs of consecutive prints at the same timestamp and price (per-fill delivery) or single prints whose size equals the sum of such a run (aggregated per aggressor order / match event).
6. Also compare Σ tick size in the minute with the 1-min bar volume from dxFeed.

**Record.** Symbol, minute, dxFeed print count, Σ size, the max print, the % of prints with an identical timestamp+price neighbour, the reference count and Σ size, and the bar volume.

**Pass / fail.**

- **Pass (per trade):** dxFeed print count is within ±5% of a per-fill reference, Σ size equals bar volume, and same-timestamp runs are visible.
- **Pass (aggregated, documented):** dxFeed Σ size is right but the prints are clearly one per match event. In that case the flag stays off for "institutional size" ideas, but a size histogram labelled *per-event* is allowed.
- **Fail:** Σ size ≠ bar volume, or the result can't be determined.

**If it fails.** Keep `sizeOutputsVerified = false`. The size-based ideas stay **Parked** (CAT l.1384). Counts and timing outputs remain usable (MFS §M-TICK gate). Ask MC/dxFeed support which delivery dxFeed uses for CME `TimeAndSale` into MC.

---

# Check 2: Bar Magnifier and Volume Delta on `@ES`

**Purpose / unblocks.** (a) **Bar Magnifier.** The CAT honest-simulation rule needs 1-tick magnifier inside W-T and 1-minute before it (CAT l.109, l.3954). The MC wiki *Custom Futures* page says Bar Magnifier is **not available for Custom Futures**. `@ES` from dxFeed is a native continuous symbol, not an MC Custom Future, but this still has to be confirmed. (b) **Volume Delta.** This is the MC-BidAsk FlowQuality tier (CAT l.164; `_VolumeProfileFuturesBase` `McBidAskChartTypeName = "VolumeDelta"`, l.603) and the P-BA probes (P3S S1).

**Steps: Bar Magnifier** [MC wiki *Bar Magnifier*]

1. Make an `@ES` 5-min chart (dxFeed) with any strategy applied, for example `_Simple` or a probe.
2. Right-click the chart → **Format Signals** → Signals tab → **Properties** → **Backtesting** tab → *Backtesting Precision* → tick **Use Bar Magnifier**.
3. Run it twice: once with **Tick = 1** over a range inside the tick window (after ~Jul 2024), once with **Intra-day = 1 minute** over a range before it.
4. Compare the trade list (Strategy Performance Report) with a run where magnifier is off. Check whether MC shows any warning or greys out the option.
5. The wiki notes that where tick data does not cover the range, ticks are **emulated from OHLC**. Record whether the earliest magnified fills fall before the tick window.

**Steps: Volume Delta** [MC wiki *Volume Delta*]

1. Open a new `@ES` 1-min chart → Format Instrument → **Settings** tab → enable **Show Delta** → Delta Type **Volume Delta**.
2. Set **Breakdown by = Ask Traded vs Bid Traded** and **Build from = Ticks**. Per the wiki, this makes MC request historical tick ask and bid data for the whole chart range.
3. Load 5 sessions inside the tick window. Then load 1 session before Jul 2024 and note what is shown (blank, error, or fallback).
4. Repeat with Breakdown = Up Ticks vs Down Ticks, as a contrast.

**Record.** Magnifier: is the option enabled on `@ES` (Y/N), are the trade lists different (Y/N, count of differing fills), any warning text. Volume Delta: does it render (Y/N), the first date with a non-empty ask/bid split, the build time, and any message.

**Pass / fail.**

- **Pass:** magnifier is selectable and changes fills on `@ES` at both 1-tick and 1-minute settings. Volume Delta (Ask/Bid) renders on `@ES` inside the tick window.
- **Fail:** either is unavailable on `@ES`, or Volume Delta is empty inside the window.

**If it fails.** Magnifier: run backtests on the individual contract (`ESZ26`, etc.) for fill checks, or use a fine-resolution Data1 as MFS §8 item 4 already says for PT. **Label every run** with the precision used (CAT l.3954). Volume Delta: keep `FlowCfg.McBidAsk = Off`, so the tier falls back to Quote (our Lee–Ready) / TickRule (CAT l.164). Note: **Bar Magnifier and IOG are unavailable in Portfolio Trader** regardless (M-PORT §4.4, §10). That comes from the MC wiki *Chart vs Portfolio Backtesting* as cited by M-PORT. This check is for chart backtests only.

---

# Check 3: exact dxFeed symbol names (Rev 2.7 §E1)

**Purpose / unblocks.** Fixes `@ES` / `@NQ` / `@MES` and individual-contract names for QuoteManager, XMKT legs (NQ, VIX, ZN, CL, GC need the right bundles, CAT l.89, MFS §M-XMKT 6), the `RollDates` table, and the **Symbol Mapping** in Check 8. The MC wiki *Symbol Guide* has **no dxFeed column**, so it must be read off your install.

**Steps** [MC wiki *How to Add Symbols*]

1. QuoteManager → **Instrument → Add Symbol → From Data Source** → choose dxFeed → **Future** tab.
2. Look up `ES`. Record each name exactly as shown: the continuous symbol (expected `@ES`) and individual contracts (for example `ESZ26` or `ESZ6`; dxFeed's own feed uses forms such as `/ESZ25:XCME`, per the MC forum).
3. Repeat for `NQ`, `MES`, `MNQ`, `YM`, `RTY`, `CL`, `GC`, `ZN`, `VX`/VIX, plus any XMKT leg you plan to use. Note which ones return nothing (a missing bundle).
4. For each, open the symbol's properties in QuoteManager (Edit Symbol, **confirm in your MC version**) and record: exchange, **category** (the exact text, needed for P-R1), price scale / min move, big point value, currency, and **session template** (expect a 24h CME Globex template, Rev 2.7 §E5, CAT l.87).
5. Do the same lookup on the **Interactive Brokers** data source (the MC wiki says the IB connection is needed to add broker symbols even when it is not used for data). Record the IB names (wiki format: `ESZ6`, root + month + 1-digit year).

**Record.** A table: root | dxFeed continuous | dxFeed contract example | exchange | category | BPV | min move | currency | session template | IB contract name.

**Pass / fail.**

- **Pass:** every symbol the catalogue needs resolves on dxFeed, with USD currency and a 24h Globex template for ES/NQ.
- **Fail:** a symbol is missing, or ES/NQ have an RTH-only template.

**If it fails.** Missing symbol: mark the ideas that use it gated (bundle needed, §E1). Wrong template: assign the CME 24h template in QuoteManager before running ETH ideas (TS-01, PS-24, TS-06, A2-04, TS-02, TS-05, VW-07 per CAT l.87).

---

# Check 4: how far back tick data goes (Rev 2.7 §E2, P-BA3)

**Purpose / unblocks.** Sets W-T (tick window ≈ 500 sessions, CAT l.1269, l.1379: "Jul 2024 → Oct 2026") and confirms the per-symbol W-M start (CAT l.75, l.99). Also sizes the M-TICK `historyTicks` request and the P-BA3 "history depth on dxFeed" question.

**Steps**

1. `@ES` **1-tick** chart. Format Instrument → Settings → set the data range far back (for example "from 2023-01-01"; the exact range control: **confirm in your MC version**). Let it load.
2. Record the **first tick timestamp** shown. Repeat with *Ask* and *Bid* fields if you can choose the field (the dxFeed page says Ask/Bid history exists only at tick level).
3. Repeat on a **1-min** chart from 2014 and record the first minute bar. Repeat on **daily** from 1985.
4. Repeat steps 1–3 for `ESZ26` (individual contract) and for `@NQ`, `@MES`.
5. Optional (P-BA3): run a strategy with `FlowCfg.McBidAsk = Prefer` on `_VolumeProfileFuturesBase` and read the journal line `OF McBidAsk loaded bars(n) first(...) last(...)` (l.736).

**Record.** Per symbol: first tick (trade), first tick (bid/ask), first 1-min bar, first daily bar, and the `OF McBidAsk first(...)` date.

**Pass / fail.**

- **Pass:** first ES tick ≤ 2024-07-01 (± a few days), consistent with the dxFeed page, and the W-T start in CAT holds.
- **Fail:** the tick history starts later, or it starts at a rolling "now − 2 years" (expected; record the date).

**If it fails / differs.** Move the W-T start to the measured date. Re-split 250/125/125 (CAT l.38). Shorten the holdout as CAT §2.0.1 requires. Note that the window **slides**: re-measure it before each W-T run.

---

# Check 5: are daily closes settlement or last trade? (Probe P3)

**Purpose / unblocks.** P3 is open (MFS l.257, l.296; P3S l.562). Until it closes, the code journals `DAILY closeBasis=Bar (P3 unverified)` (CS l.637). Affects M-DAILY when it uses a daily Data n stream: HV, ROC1, LBR, GapPct, TSMOM and prev close. A1 §5.5 says to compare against CME settlement for 5 sessions.

**Steps**

1. `@ES` **daily** chart (dxFeed), 24h template. Read the Close of the last **5 complete sessions** (Data Window or the hover tooltip).
2. On a 1-min `@ES` chart, record the last close for the same days at **16:00 ET** (RTH close), **16:15 ET** (CME ES daily settlement is based on trading around 15:14:30–15:15:00 CT = 16:14:30–16:15:00 ET; confirm on cmegroup.com), and **17:00 ET** (Globex session end).
3. Get the official CME **settlement price** for the front ES contract for those 5 days (CME Group website → E-mini S&P 500 → Settlements).
4. Compare each daily close with the settlement price, the 16:00 ET last, the 16:15 ET last and the 17:00 ET last.

**Record.** Per day: dxFeed daily close | CME settle | 16:00 ET last | 16:15 ET last | 17:00 ET last | which one matches.

**Pass / fail.**

- **Settlement:** the daily close equals the CME settle on 5/5 days.
- **Last trade:** it equals one of the last-trade prices on 5/5 days. Note which one.
- **Mixed / none:** inconclusive. Treat as a fail.

**If it fails / is last trade.** Keep `closeBasis=Bar` and use the **Data1-aggregate RTH close** path (P3S l.561: close of the last bar stamped ≤ 16:00 ET or the early close) for every daily statistic, instead of the daily stream. If it is settlement: record "P3 = settlement" in MFS §9 and keep the daily stream. Either way, update the journal label.

---

# Check 6: run `_MtfProbe` (Probe P2 / VPB-8)

**Purpose / unblocks.** P2 gates the VP-3 HTF ladder and S5 readings (P3S l.898), `HtfClosed` usage, the A2R alignment rule ("(t−5, t] end-stamped", A2R l.63), and the PA Data2 trace (MFS l.168).

**What the probe does** (`_MtfProbe.Strategy.CS`). It places no orders and sets `HtfStreams = {2}`. At `TraceLevel ≥ Info` it writes:

- **Once:** `P2 start iog(..) maxBarsBack(..) d1(name type size) d2(...) d2.Close[MaxBarsBack]=...`
- **Every CalcBar:** `P2 call(n) d1[time status c= cb=] d2[time status c= cb=] closedIdx(i) closed[time c=] mapped(i time) realTime(..)`

**Steps**

1. Chart: Data1 = `@ES` 1-min, Data2 = `@ES` 15-min (second run: 60-min). Same symbol, both on the 24h Globex template. Adding a second data series: Insert → Instrument (**confirm in your MC version**).
2. Compile `_MtfProbe` in the PowerLanguage .NET Editor and insert it as a signal. Set **TraceLevel = Info**.
3. Runs: (a) backtest, IOG **off**, magnifier off; (b) backtest, IOG **on** (Strategy Properties → General tab, Intra-bar Order Generation: **confirm in your MC version**); (c) backtest with Bar Magnifier 1-tick (Check 2 path); (d) Data Playback over the same day; (e) live for ≥ 30 minutes around a 15-min boundary.
4. Use a window that includes 09:30, 10:00, 16:00–16:15 ET, the 17:00 halt, and one roll day.
5. Open the `MCSignal*.csv` log.

**What to read off it**

| Question (MFS P2) | Where in the log | Expected |
|---|---|---|
| (a) forming Data2 values/Status intrabar | `d2[... status]` on 1-min calls between 15-min closes | the Data2 status is not "closed" mid-bar; `closed[...]` stays on the last closed HTF bar |
| (b) are bar times **end** stamps? | the `d1` call stamped 10:00:00: does `closed[10:00:00]` appear on that same call? | yes means end stamps (the 10:00 HTF bar is usable on the 10:00 1-min bar) |
| (c) ordering at a shared timestamp | number of calls with the same `d1` time; d2 status change before/after | one call or two; record which |
| (d) PG p54 "last calc before Data1 Open is valid" | IOG-off run: count calls per Data1 bar | entries decided on a Data2-only calc are overridden |
| (e) per-series MaxBarsBack | `P2 start ... d2.Close[MaxBarsBack]=` | a value means Data2 has ≥ MBB bars; `<Exception>` means it does not |
| identical across modes | diff `closed[...]` / `mapped(...)` between runs a–e | identical mapping in backtest, playback and live |

**Record.** The 6 answers above and the run files (name + run).

**Pass / fail.**

- **Pass:** end stamps confirmed, `closedIdx` never points at a forming bar, and the mapping is identical across a–e.
- **Fail:** any mismatch across modes, or a forming HTF bar being read as closed.

**If it fails.** Keep `HtfClosed()` in all code (already the rule). Disable the VP-3 HTF ladder and S5 readings until fixed. Report the exact divergent lines. If the stamps are **start** stamps, shift the A2R (t−5, t] alignment and re-run the VPB-8 acceptance (MFS l.168).

---

# Check 7: Portfolio Trader checks P5b L1–L9 (M-PORT §9)

**Purpose / unblocks.** `_PortfolioRiskManager` and the M-PORT sizing, throttles and kill switches (M-PORT §5). All PF ideas wait on these (§8 step 5).

**Common setup** [MC wiki *Portfolio Settings*, *Understanding Portfolio Trader*]

- PT portfolio with 2 strategies: `@ES` and `@NQ`, 1-min, both dxFeed.
- The MM signal goes in **Portfolio Settings** tab → *Money management signal* → **Select**. The MC wiki states that the PMM signal is calculated **after all strategy signals** on every step and "always calculated on the first instrument".
- Set Exposure / Max % Capital at Risk per Position high (for example 100% / 100%), so that MC's own Money & Risk layer does not cut orders during the test.
- Use backtest first. For L6 and L9, use real time on an **IBKR paper** profile.
- **Note:** some L-tests need small throw-away test signals (the "always arm" publisher, an "allow index 0 on odd bars" MM). They are **not in the repo**. Write them as `_Test*` variants before the sitting.

| # | Question | Exact steps | Record | Pass | If it fails |
|---|---|---|---|---|---|
| L1 | MM runs after all strategies and before order sending in the **same** step | Both strategies send `buy next bar market` every bar. Test MM: deny-all, then `PortfolioStrategies[0].AllowEntries = true` only on odd bars. Backtest 1 session. | Fill times per strategy vs bar parity | index 0 fills only on bars after odd bars, index 1 never fills | MM changes lag one step: move all gating one bar earlier (publish `pm.riskNew` a bar ahead) and revise §4.5 |
| L2 | Default `EntryContracts = 0` overrides code size | Strategy codes 3 contracts; Strategy Properties size 1. Runs: (a) no MM, (b) MM leaves 0, (c) MM sets −1. | Trade size in each run | (b) = 1 (properties size), (c) = 3 | Either way, set `EntryContracts` explicitly every calc (spec rule); record the actual default |
| L3 | `ForceClosePosition()` timing; works with `AllowExits = false`? | Open a position, call `ForceClosePosition()` on bar T; repeat with `AllowExits=false` | Exit fill bar/price vs T close / T+1 open | Fill at T+1 open (spec assumption) and close works with AllowExits=false | Update §6 timing; if blocked by AllowExits=false, never set it (already the rule) |
| L4 | `Paused = true` blocks exits/stops? still calculates/publishes? | **Backtest only.** Open a position with a stop, set `Paused=true`, let price cross the stop | Stop filled? `pm.stamp` still updates? | Documented either way (spec assumes it blocks exits) | Keep "flatten first, pause when flat" (§5.4.4) |
| L5 | How a .NET MM signal is designated; does it need a data series? | Apply `_PortfolioRiskManager` via Portfolio Settings → Money management signal → Select | Does it appear in the list? any error/attribute needed? | Selectable and runs | Find the required base/attribute in the PowerLanguage .NET help (PortfolioSignalObject docs) and report back |
| L6 | `IPortfolioData` object sharing: same AppDomain in backtest **and** auto-trading; persistence across restarts | Publisher writes a counter object; MM reads the reference. Run in backtest, then in real-time PT on IBKR paper; restart PT | MM sees same object (Y/N) per mode; value after restart | Shared in both modes; reset on restart (assumed) | Fall back to scalar values only; if not shared live, M-PORT is backtest-only until solved |
| L7 | Mixed resolutions: MM runs on every step of any strategy? | Portfolio: `@ES` 1m + `@CL` 5m. The MM journals each call time and each `pm.stamp` | MM call times vs 1m/5m bar times | MM fires on every 1m step; CL stamp lags as expected | Apply the stale rule (`staleBars`) per strategy resolution |
| L8 | `PortfolioCurrency` and `StrategyCurrencyCode` for dxFeed CME symbols | Print both once in MM StartCalc | Values | Both USD | The MM refuses to size (spec rule); fix the symbol currency in QuoteManager |
| L9 | IBKR symbol-mapping roll in PT live/paper | **See Check 8.** | — | — | — |

---

# Check 8: contract roll on an IBKR paper account (L9, Rev 2.7 §E8, MFS §0.3)

**Purpose / unblocks.** Live readiness. `@ES` is not tradable at IB and must be mapped to the front contract (MC wiki *How to Set Up Symbol Mapping*: "@ES should be mapped to the currently traded contract, e.g. … ESZ26"). M-PORT §7 requires a re-map at **each roll** (the Thursday before the 3rd Friday of Mar/Jun/Sep/Dec, the CME equity roll date). It also confirms that the code-side roll handling fires on the dxFeed splice day.

**Code-side roll handling to verify.** `_CustomSignal`: `RollDetection` (Off by default), `RollGapTicks = 8`, `RollDates` table (CS l.1232–1264). `_RollDetector`: a roll is (a) a new trading day opening ≥ `gapTicks`×tick from the prior close, on days 1–21 of Mar/Jun/Sep/Dec, or (b) a date in `RollDates`, or (c) a change in `Bars.Info.Name`. On a roll: AVWAP anchors are **invalidated** (`AV invalidated (roll)`), PA `OnRoll` runs, `_DailyContext` removes the roll gap, the ATR splice TR is skipped, and pending entries are cancelled (MFS §0.3, M-PORT §7). P3S A-ROLL trace: journal shows `AV invalidated (roll)` and the PA OnRoll lines on both streams.

**Next window.** The ESZ26 → ESH27 roll. CME publishes the date on its *Equity Index Roll Dates* page. By the rule above it is expected to be **Thu 10 Dec 2026**; **confirm on cmegroup.com**, and confirm the day dxFeed's `@ES` switches (MC forum: dxFeed follows CME's recommendations).

**Steps**

1. **Before (by early Dec):** IBKR paper broker profile connected. [MC wiki *Interactive Brokers Broker Profile*: TWS/Gateway API enabled, Read-Only API off, socket port matched.]
2. **File → Broker Profiles → Symbol Mapping** [MC wiki]. Choose the IB profile in *Showed mapped symbols for*. Add the line Chart Symbol `@ES` (dxFeed) → Broker Symbol `ESZ6` (IB, added from the IB data source). Apply → OK. If the mapping is not saved, the wiki says to also add the reverse line.
3. Run the PT portfolio (or a single-chart strategy) in auto-trading on the paper account with `RollDetection = SeriesOrTable` and the 2025–26 `RollDates`, `TraceLevel = Journal`. Hold a 1-lot position into the roll window if you want to test the position-transfer procedure.
4. **On the dxFeed switch day:** watch the journal for the roll line, `AV invalidated (roll)`, PA OnRoll, and cancelled pendings. Note the session-open gap in ticks.
5. **Roll procedure (M-PORT §7 checklist):** stop auto-trading → flatten or roll the IB position manually in TWS → change the mapping to `ESH7` → verify PT/MC position = IB position for each strategy (do not assume MC syncs) → restart the portfolio.
6. Send one 1-lot paper test order after the restart and confirm it fills in **ESH7** in TWS.

**Record.** dxFeed switch date/time (ET) | CME roll date | `rollOn` fire bar (journal time) | gap in ticks vs `RollGapTicks` | journal lines seen | mapping change time | MC vs IB position before and after | test order contract | anything that went wrong.

**Pass / fail.**

- **Pass:** the detector fires on the dxFeed switch day (once only); the invalidation and OnRoll lines appear; no false roll fired on other days of Mar/Jun/Sep/Dec 1–21; post-roll orders go to ESH7; MC position = IB position.
- **Fail:** any order goes to the expired or wrong month, there are false or missed roll detections, or positions mismatch.

**If it fails.** Wrong-month order: set a calendar reminder for the remap 2 trading days before every CME roll date, and add a StartCalc assertion comparing the mapped contract with the expected front contract (new work). Missed or false detection: put the measured switch date into `RollDates` and use `Table` mode only; tune `RollGapTicks`. Position mismatch: keep a manual reconcile step and never let PT "sync" on its own.

---

# Check 9: P-BA4 and P-R1

## P-BA4: must the chart/request be in Ask/Bid-traded mode?

**Purpose / unblocks.** The MC-BidAsk tier ingestion (P3S S1 step 6). The code clones `Bars.Request` and sets `Resolution.ChartType` to `VolumeDelta` (parsed by name, P-BA1), then reads `Data2` / `RTData2` by reflection (P-BA2) (`_VolumeProfileFuturesBase` l.660–700). The PG says nothing about whether the Breakdown mode (Ask Traded vs Bid Traded) must be set on the chart (P3S l.517).

**Steps**

1. Strategy on `_VolumeProfileFuturesBase` with `FlowCfg.McBidAsk = Prefer` and `TraceLevel = Journal`, on an `@ES` 1-min chart inside the tick window.
2. Run A: chart Format Instrument → Settings → Show Delta → Volume Delta, **Breakdown = Ask Traded vs Bid Traded**, Build from Ticks (Check 2 path).
3. Run B: same, but **Breakdown = Up Ticks vs Down Ticks**.
4. Run C: Show Delta **off** (a regular chart).
5. For each run, read the journal: `OF McBidAsk loaded bars(..) first(..) last(..)`, the per-session `OF T9 session(..) signAgree(..) bars(..) mcCover(..)`, any `OF tier McBidAsk->… reason(..)` fall-back, and any `chartType(...) unknown (PROBE P-BA1)` warning.

**Record.** Per run: bars loaded, first/last, mcCover %, signAgree %, fall-back reasons, P-BA1 warning (Y/N).

**Pass / fail.** P-BA4 is **answered** when runs A–C are compared:

- If C (and B) give the same mcCover and signAgree as A, the request is independent of chart mode.
- If only A gives data, the chart must be in Ask/Bid mode (as BCS asserts).

The check **fails** only if A also yields 0 bars. In that case also look at P-BA1 and P-BA2.

**If it fails.** Leave `McBidAsk = Off` and label the flow tier "Quote / TickRule". If chart mode is required, add a StartCalc assertion and a setup note: "Ask/Bid Volume Delta on Data1".

## P-R1: the enum value that identifies futures

**Purpose / unblocks.** The S4.4 warning "RollDetection is Off on a futures symbol" (CS l.1277–1278) compares `Bars.Info.Category.ToString() == "Future"`. The name is unverified, so the warning may be silent. The same category drives `TradingDayRoll = Auto` (CS l.1830).

**Steps**

1. Run any `_CustomSignal` strategy (for example `_MtfProbe`) on `@ES` with `TraceLevel ≥ Info`. The `RUN` line prints `category({info.Category})` (CS l.1619).
2. Read the exact text. Also check the `ESymbolCategory` members in the PowerLanguage .NET Editor's object browser or IntelliSense (**confirm in your MC version**).
3. Repeat on `ESZ26` (individual contract) to see whether it gives the same value.

**Record.** The `category(...)` text for `@ES` and `ESZ26`, and the full member list of `ESymbolCategory`.

**Pass / fail.**

- **Pass:** the text is exactly `Future`, so the warning works as coded.
- **Fail:** any other text (for example `Futures`).

**If it fails.** Replace the string test with the real enum member (`Bars.Info.Category == ESymbolCategory.<X>`) in CS l.1278 and in `autoTradingDay`, then rebuild.

---

# Check 10: M-PORT PR-C session/weekly throttles (#72), before #72 is marked ready

Portfolio Trader replay with two strategies, **ES + CL** (so their `pm.day` keys differ), under **RiskParity**, plus one repeat with `"sizing": "CodeSize"`. Read the MM journal (`PORT …` lines).

- (a) **Session stop:** after 2 losing closes (R < −0.05) on one strategy, `PORT session stop` and then Info `skip … session stop` appear for **that strategy only**. The other strategy keeps entering. Entries resume on its next `pm.day`, and CL's new day does not clear ES's stop.
- (b) **Weekly halve and observe:** at week R ≤ −3, `PORT weekly halve` appears and EntryContracts is about half. At ≤ −6, `PORT weekly observe` and `skip … weekly observe` (PORT observe) appear. The **Sunday-evening ETH session does not reset early**: the state clears only at the first session of the next ISO week.
- (c) **CodeSize skip:** in the CodeSize run, with ddSteps stage 1 (0.5) or a weekly halve, entries log `skip … throttle under CodeSize` and no order is sent.
- (d) **Reversal:** a long→short flip records a closed trade (R counted toward 4a/4b/kill; the session-stop count increases).
- **Pass:** a–d as described. **Fail:** any missing journal line, a skip on the wrong strategy, or an early reset.

---

# Check 11: M-PORT PR-D risk profiles and advisory (#73), before #73 is marked ready

Portfolio Trader replay over at least 3 sessions with **ES + CL + GC**, run twice: once with `_PortfolioRiskManager` as the MM signal and once with `_PortfolioRiskManager_ScaledBook`, with no JSON file. Read the MM output (`PORT …` lines).

- (a) **RUN line:** it shows `signal(_PortfolioRiskManager)` with `config=ok` and `profile(Starter) … heatCap(0.015) clusterCap(0.0075) maxPositions(2)` in the first run, and `signal(_PortfolioRiskManager_ScaledBook)` with `config=ok` and `profile(ScaledBook) … heatCap(0.04) clusterCap(0.02) maxPositions(6)` in the second.
- (b) **Advisory:** exactly **one** `PORT advisory: uncorr=… uncorr03=… [ids] …` line per book day, and it says `not eligible` unless both counts are ≥ 3. With less than `rhoMinDays` of history it notes `ρ unknown`. The profile never changes during the run.
- (c) **Optional fallback:** point `ConfigPath` at a file with `"maxTradeRisk": 0.02`. The RUN line then shows `config=fallback` and the signal's own profile values.
- **Pass:** a–b (and c if run). **Fail:** wrong profile values, missing or repeated advisory lines, or `config=fallback` without a bad file.

---

# Results table (fill in)

| # | Check | Date / time (ET) | Key measurement | Pass / Fail / Info | Action taken | Spec to update |
|---|---|---|---|---|---|---|
| 1 | dxFeed trade size per trade vs aggregated (§E6) | | | | | CAT M-TICK, `TickConfig.sizeOutputsVerified` |
| 2a | Bar Magnifier on `@ES` | | | | | CAT §honest sim |
| 2b | Volume Delta (Ask/Bid) on `@ES` | | | | | CAT F-OF |
| 3 | dxFeed symbol names / categories / sessions (§E1, §E5) | | | | | MFS §M-XMKT, CAT |
| 4 | Tick / 1m / daily history start (§E2, P-BA3) | | | | | CAT W-T / W-M |
| 5 | Daily close = settlement or last (P3) | | | | | MFS §9 P3, P3S S2 |
| 6 | `_MtfProbe` (P2) answers a–e | | | | | MFS §3, P3S |
| 7-L1 | MM same-step effect | | | | | M-PORT §4.5 |
| 7-L2 | EntryContracts default | | | | | M-PORT §5.3 |
| 7-L3 | ForceClosePosition timing | | | | | M-PORT §6 |
| 7-L4 | Paused blocks exits? | | | | | M-PORT §6 |
| 7-L5 | .NET MM designation | | | | | M-PORT §5 |
| 7-L6 | IPortfolioData sharing / persistence | | | | | M-PORT §2.4 |
| 7-L7 | Mixed resolutions | | | | | M-PORT §4.6 |
| 7-L8 | Currency codes | | | | | M-PORT §1 |
| 7-L9 / 8 | IBKR paper roll + roll detector | | | | | M-PORT §7, MFS §0.3 |
| 9a | P-BA4 chart mode | | | | | P3S S1 |
| 9b | P-R1 futures category text | | | | | CS l.1278 |
| 10 | PR-C throttles: session stop, halve/observe (no Sunday reset), CodeSize skip, reversal | | | | | M-PORT §5.4 4a/4b, T-P13 |
| 11 | PR-D profiles: RUN line signal / config=ok / profile, one PORT advisory per day | | | | | M-PORT §5.1/§5.1a |

# Items not pinned down by documentation

- **dxFeed symbol names** (`@ES` assumed; contract format unknown). The MC Symbol Guide has no dxFeed column, so Check 3 gives the answer.
- **The rev 2.7 §E text** (`reviews/analyst-base-class-spec.md`) is not in the workspace. The §E labels come from MFS and CAT.
- **UI paths not verified on the MC wiki:** QuoteManager Edit Symbol / data export, the chart data-range control, Insert → Instrument for Data2, the IOG checkbox location, the `ESymbolCategory` object browser, the PowerLanguage .NET Editor output. These are marked "confirm in your MC version".
- **The exact Dec-2026 CME roll date** and the day dxFeed's `@ES` switches. Confirm on cmegroup.com.
- **Test signals for L1–L7** are not in the repo and must be written first.
