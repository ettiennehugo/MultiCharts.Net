# Video ideas 2026-11-10: catalogue candidates (on top of catalogue v2.7)

**For:** Ettienne Hugo · **Prepared by:** Grok Bot · **Date:** Tuesday 10 Nov 2026 (America/New_York, EST) · **Branch:** `feat/strategy-specs-batch1` (PR #117, draft)

**Request (Ettienne, 21:38–21:41 ET):** *"Please watch this video and add it for strategy testing if we do not have anything similar documented"*, and for the others *"Also watch and document this video if we don't have something similar"*.

**What this is.** Four videos, checked against the catalogue (`specs/trading-idea-catalogue.md` v2.7), the 2026-11-08 additions, `specs/strategies/` (PR #117) and the planning notes.
- Two setups are new and get candidate IDs: **YV-11** and **YV-12**.
- Three more setups are close to existing entries. They are added as cited variant notes on **PS-24**, **PS-18** and **A2-49**, not as duplicates.
- One video is the source we already have (**YV-07 / SP-09**).
- One is process and psychology only (no strategy).

This file is a candidates list, not batch-1 specs. Nothing here has been run.

**Evidence.** Every video number is a **presenter claim**, unverified, grade **E**. No performance number was computed. The videos' content comes from the findings the parent agent relayed. Timestamps were not supplied with those findings, so each entry gives the URL plus the example dates; **[ts: VERIFY]** marks where a timestamp should be added on a re-watch.

**Holdout (unchanged).** All tests use data up to **31 Dec 2024** (fixed cut 2025-01-01; Prog §5.1). OF-01 is the only exception, and nothing here is OF-01. The catalogue's W-T split (last 125 sessions as holdout) is overridden by this owner rule for every entry below.

**House rules carried over (Prog §3).**
- Closed bars only.
- Native `[Input]`s.
- Every true variant is selectable via Inputs, and the default is the primary (rule 8).
- Multi-value inputs are typed C# enums with named members, and on/off switches are bools (rule 9).
- Costs and floors per Prog §4 (ES $17.00 round trip, 14-tick floor; NQ $9.48, 19 ticks; CL $14.72, 15 ticks; 6E $11.17, 18 ticks). GC has no confirmed cost row yet.
- No adds (Prog §7.7) unless Ettienne approves an exception.

## Video verdicts

| # | Video | Verdict | Catalogue action |
|---|---|---|---|
| A | Emmanuel Malyarovich, *The ONLY 2 Indicators I've Used to Make $843,886 Day Trading*, https://youtu.be/2e4WayvjLJo | **New ID added: YV-11.** Related, not duplicate: DV-15 (one EMA20 + VWAP-slope gate, 1:1 bracket), A2-58 (fast-MA counter-timing), VW-01 (VWAP pullback on a trend day), PS-07 (open-drive pullback) | YV-11 below; cross-reference line on DV-15 |
| B | SMB Capital (Jeff Holden), *Trading Became Easy After I did These 4 Things*, https://youtu.be/Yl8-N3DznAo | **Reviewed: process and psychology only, no strategy added** | General trade-management note (below) and one line in catalogue §3.10 |
| C | SMB Capital, *Why Simple Strategies Are Money Machines* ("Market Memory Model"), https://youtu.be/y_ujkIcs6so | **New ID added: YV-12** (setups 1–2, prior-day high/low range rejection with a wick candle). Setups 3–5 are **similar to PS-24, PS-18 and A2-49** and are added there as cited variants | YV-12 below; variant notes on PS-24, PS-18, A2-49 |
| D | Dylan Richards, *How to Find High Probability Scalps Every Day (No Daily Bias Needed)*, https://youtu.be/83d144f2e0k | **Similar to (in fact the same video as) YV-07 / SP-09**, catalogued 2026-11-01 and specified 2026-11-08 | Review note and one proposed variant input on SP-09; data/holdout flag |

---

#### YV-11 · 9 EMA / 20 SMA zone pullback in a sloping trend, with an over-extension veto (Malyarovich)
- **Implementation status:** **Not started**. Candidate entry only; no code or build spec.
- **Classification:** NEW. It is related to DV-15, A2-58, VW-01 and PS-07, but none of those has the two-MA entry zone, the slope gate, the over-extension veto or the daily-200-SMA ceiling.
- **Family · Source:** SP (trend pullback) · YouTube: *The ONLY 2 Indicators I've Used to Make $843,886 Day Trading*, Emmanuel Malyarovich, https://youtu.be/2e4WayvjLJo (37 min). Examples and timestamps:
  - LASE, Jun 2, 2m, 10:00–10:30 ET: entered over a doji, added twice, exited at about $1.80 for $4,368 [ts: VERIFY];
  - LASE, Sep 8–9, 15m 20 SMA short [ts: VERIFY];
  - ANF, Aug 28, 2m [ts: VERIFY];
  - DKS, 5m short [ts: VERIFY];
  - SHOP, May 8, 5m base breakdown [ts: VERIFY].
- **Evidence:** **E**. The title P&L and the example P&L are presenter claims. The family prior for MA-pullback continuation on index futures is weak: DV-15's only number is a 2-month in-sample EUR/USD test, and A2-58 says fast MAs lose on noisy equity indices.
- **Rules as stated:**
  - **Indicators:** 9 EMA (close), 20 SMA (close), 200 SMA (close). The 200 SMA is "best on hourly, daily and weekly".
  - **Bias:** start with the daily chart for bias and the plan. Trade with the trend only.
  - **Timeframe by time of day (ET):** 1m/2m 09:30–10:00; 2m/5m 10:00–12:00; 5m/15m 12:00–16:00.
  - **Long:** a bullish catalyst; the 20 SMA rising, ideally at about 45°; price above it; the 9 EMA above the 20 SMA. Short is the mirror.
  - **Setup:** a pullback, or a consolidation through time, into the "entry zone / buy area" between the 9 EMA and the 20 SMA.
  - **Trigger:** price respects the MAs, e.g. enter above a doji bar or a bottoming tail. No mechanical trigger, no order type.
  - **Stop:** below the pullback candle's low or the base low. The short side is not stated.
  - **Target:** the daily 200 SMA as a ceiling, otherwise "the next leg". Adds to winners and raises the stop to the new low. No scale-out, break-even or time exit.
  - **Stay out:** a flat 20 SMA; over-extension (a large gap from the 20 SMA, or a slope much steeper than 45°); the first pullback or breakout after an over-extension.
  - **Filters:** a clear catalyst (all examples are large gaps, LASE +30%, DKS −20%). No volume minimum.
- **Mechanical translation (MultiCharts .NET; long shown, short mirrors).** Every number below is **PROPOSED**, because the video gives none.
  1. **Streams:** Data1 = 1m. The signal timeframe is a strategy-local aggregation of Data1, or Data2/Data3 series (2m, 5m, 15m).
     - `TfSchedule = Fixed` (primary): one signal TF, `SignalTfMinutes` = 5. 5m is in two of his three windows.
     - `TfSchedule = ByTimeOfDay` (variant): 2m from 09:45 to 12:00, then 5m to 15:30. His 1m 09:30–10:00 slot sits inside the catalogue's 09:30–09:45 no-entry window (§2.1), so it starts at 09:45.
  2. **Daily bias:** M-DAILY (`NeedsDaily = true`, `DailyConfig.smaLength = 200`). A long needs the prior daily close above `Sma200`, because the daily plan is long-side. `DailyBias` (bool) switches this off.
  3. **Trend state on the signal TF:**
     - EMA9 > SMA20;
     - close > SMA20;
     - **slope gate** (`SlopeFilter`, bool): slope = (SMA20_t − SMA20_{t−5}) / 5 ≥ `SlopeMinAtr` (0.10) × ATR14(signal TF) per bar. The 45° in the video depends on chart scale, so it is replaced by slope in ATR units. Flat = below the threshold.
  4. **Over-extension veto** (`ExtensionVeto`, bool): skip if (close − SMA20) > `ExtMaxAtr` (2.0) × ATR14, or if the slope > `SlopeMaxAtr` (0.40) × ATR14 per bar.
     - `FirstPullbackVeto` (bool): after any over-extended bar, the first pullback into the zone is skipped, and so is a new high made before price has re-entered the zone once.
  5. **Context / catalyst** (`CatalystFilter`, enum):
     - `GapAtr` (primary on futures): the RTH open is ≥ `GapMinAtr` (0.5) × ATR20d from the prior RTH close, in the trade direction;
     - `PriorDayEvent`: a tier-1 M-EVENT release today before 10:00 ET, or on the prior day;
     - `Off`.
     - Stocks would use `GapPct` (gap ≥ 3%, PROPOSED). That is a run-sheet filter on a fixed list, not an input. See Instruments.
  6. **Zone and trigger.** The zone is [SMA20, EMA9]. A trigger bar is a closed signal-TF bar with low ≤ EMA9 and close ≥ SMA20 (respects the MAs). `Trigger` (enum):
     - `DojiOrTail` (primary): |close − open| ≤ 0.10 × range, or lower wick ≥ 0.50 × range with the close in the upper half;
     - `PriorBarHigh`: any bar that meets the zone test.
     - **Entry:** buy-stop at the trigger bar high + 1 tick, valid `TriggerBars` (2) bars. A newer trigger bar replaces it.
  7. **Window:** 09:45–15:30 ET. No entries during the M-EVENT blackout. At most one open position, and at most `MaxEntriesPerDay` (2) per direction.
- **Trade management and exits:**
  | Item | Rule | Source |
  |---|---|---|
  | Initial stop | Trigger-bar low (or the base low = lowest low since the zone was entered) − 1 tick, floored at the cost floor. Skip if the widened risk is > 1% of equity | Video; Prog §4 |
  | Target | `TargetMode = DailySma200ThenTrail` (primary): if the daily 200 SMA is above entry and ≥ 1 R away, a limit at Sma200 − 2 ticks for half; otherwise no fixed target. Runner: "next leg", trailed | Video ("200 SMA ceiling", "next leg") |
  | Trailing | Stop to each new confirmed signal-TF higher low (PRICEACTION `EPivotLabel.HL`) − 1 tick, one-way ("raises the stop to the new low") | Video |
  | Adds | `AllowAdds` (bool), default **false** (house rule Prog §7.7). When true: add 1 contract on each new qualified trigger in the trade direction, max `MaxAdds` (2), with one stop for the whole position at the latest pullback low. This is the presenter's practice and needs Ettienne's approval as an exception | Video; Prog §7.7 |
  | Break-even / scale | None (the video has none). The half at the 200 SMA is the only partial | Video |
  | Time | Flat by 15:55 ET; early close −5 min. No overnight | Prog §7.6 |
  | Invalidation | A signal-TF close below SMA20 → exit at market next bar (trend broken; PROPOSED) | Inferred |
  | News | Entry blackout −2/+10; `NewsExitMinutes` per Prog §7.6 | Prog §7.6 |
- **Variant inputs (typed enums / bools; default = primary):**
  ```csharp
  public enum ETfSchedule { Fixed, ByTimeOfDay }                 // default Fixed (SignalTfMinutes 5)
  public enum ETrigger { DojiOrTail, PriorBarHigh }              // default DojiOrTail
  public enum ETargetMode { DailySma200ThenTrail, TrailOnly, FixedR } // default DailySma200ThenTrail; FixedR uses TargetR 2.0 (PROPOSED)
  public enum ECatalystFilter { GapAtr, PriorDayEvent, Off }     // default GapAtr
  // bools: DailyBias (true), SlopeFilter (true), ExtensionVeto (true), FirstPullbackVeto (true), AllowAdds (false)
  // shared: ESide, ESingleLotMode, EControlArm, EAcceptancePolicy (Prog §3 rule 9)
  ```
  Swept numbers stay parameters: `SlopeMinAtr` {0.05, 0.10}, `ExtMaxAtr` {1.5, 2.5} → 4 configs (PROPOSED), with the primary (0.10, 2.0) read first. `SignalTfMinutes` is fixed at 5, and 2m is the schedule variant.
- **Instruments, timeframe, data:**
  - **Futures first:** ES, NQ, then CL, GC, 6E. 1m bars, W-M, development Jul 2016–2022, validation 2023–2024. Data ≤ 31 Dec 2024. No ticks needed. ETH bars are not needed.
  - **Stocks (later arm):** a fixed list with a mechanical gap filter. The catalyst and "in play" selection cannot be scanned in MultiCharts, so it is replaced by a list chosen before any run plus a rule.
    - Proposed list (PROPOSED): SPY, QQQ, IWM, AAPL, AMD, NVDA, TSLA, META, AMZN, AVGO, SHOP.
    - Rule: trade a name only on days when its RTH open gaps ≥ 3% from the prior close, or the prior day's range is ≥ 2 × ATR20d.
    - Needs: dxFeed stock 1m history (VERIFY the start per symbol), a stock cost row (none in M-INSTR yet; `EAssetClass.Stock` and share sizing exist), and the NYSE calendar.
  - **Test first: ES/NQ futures on 5m.** Data, costs and engines are verified there, and the gap and daily-bias filters are mechanical.
- **Feasibility notes:**
  - Buildable now on futures: EMA/SMA are trivial; M-DAILY `Sma200` and `Atr20d`; PRICEACTION HL; F-TM; M-EVENT.
  - `ByTimeOfDay` needs multi-series or local aggregation, and the switch between timeframes at 12:00 must not repaint.
  - The 45° and over-extension rules are scale-dependent in the video. The ATR versions are a translation and should be labelled so.
  - The video's stops on 2m bars will often be inside the 14-tick ES floor. The floor then shrinks the payoff, as on SP-09.
  - Frequency (author estimate): 2–6 per week on ES on gap days.
- **Test:** K-RT (random entry inside the zone on the same days), plus the same rules without the slope gate and without the extension veto (two ablations), and against DV-15 on the same days.
  - **Kill:** mean net R ≤ 0 at ≥ 150 development events, or no lift over the ungated zone entry.
- **Differs from:** **DV-15** (one EMA20 on 30m, a VWAP-slope gate, a limit at the EMA, a 1:1 bracket). **A2-58** (buys the fast-MA turn-down inside a slow uptrend, no zone or trigger bar). **VW-01** (VWAP, trend-day classifier). **PS-07** (open-drive classification and a 30% pullback HL).

#### YV-12 · Prior-day high/low range rejection on a "Day 2" session: wick candle → other side of the range (SMB Market Memory Model, setups 1–2)
- **Implementation status:** **Not started**. Candidate entry only.
- **Classification:** NEW for the entry. It sits in the family whose prior is B (0): prior-day highs/lows react +2.3 pp vs random, but fading them yields PF 1.01–1.02 (catalogue §3.6 family prior, R4). Related: PS-03 (value-edge failed break), SP-05 (reversal bar at a profile extreme), DV-02 (failed level flip).
- **Family · Source:** SP · YouTube: *Why Simple Strategies Are Money Machines*, SMB Capital, https://youtu.be/y_ujkIcs6so (24 min). Setup 1, Range Rejection Short: AMD, 1m, near the open [ts: VERIFY]. Setup 2, Range Rejection Long: BABA, 5m [ts: VERIFY].
- **Evidence:** **E**. No statistics given. Prior B (0) for prior-day level fades.
- **Rules as stated:**
  - **Universe:** "Day 2" names, i.e. a significant move with a catalyst on the prior day (a big red or green daily candle; earnings, news or a technical range break).
  - **Levels:** yesterday's high and low, plus intraday battle lines; 3–4 lines at most.
  - **Open rules:** if it opens near, at or above yesterday's high, don't buy; if it opens near, at or below yesterday's low, don't sell; avoid the middle.
  - **Trigger:** a "John Wick candle". For a short: an upside wick, a close at the lows, and distinct follow-through. Longs mirror.
  - **Short (setup 1):** it opens near support and runs into the D1 resistance; a wick candle forms. Short on the break of the wick candle's low. Stop above the wick high. Stop to break-even as it moves. Target the other side of the range.
  - **Long (setup 2):** a hard slide into D1 support and a hard lower wick. Long on the break above the prior candle. Stop below the low. T1 at VWAP, then hold for the other end of the range.
  - **Holding time:** scalps are held 5–30 minutes.
- **Mechanical translation (futures; short at PDH shown, long at PDL mirrors).** All numbers **PROPOSED**.
  1. **Levels:** PDH/PDL = the prior RTH (09:30–16:00 ET) high/low (F-SESS). Intraday battle lines are discretionary, so batch 1 uses only PDH/PDL.
  2. **"Day 2" filter** (`Day2Filter`, enum):
     - `RangeExpansion` (primary): the prior RTH range ≥ `Day2RangeAtr` (1.5) × the 20-day median RTH range, or |prior close − prior open| ≥ 1.0 × ATR20d;
     - `PriorDayEvent`: a tier-1 M-EVENT release on the prior day;
     - `Off`.
  3. **Open location** (`OpenLocationFilter`, bool, default true): no short at PDH if the RTH open is ≥ PDH − 0.10 × prior range ("opens at or above the high"). No long at PDL if the open is ≤ PDL + 0.10 × prior range.
     - "Avoid the middle": a trigger counts only at a level (step 4). Nothing is traded in the middle of the range.
  4. **Wick candle** (signal TF, `SignalTf`):
     - its high reaches PDH − `LevelTolAtr` (0.10) × ATR14;
     - its high is no more than 0.5 × ATR14 above PDH;
     - the upper wick ≥ 0.50 × range;
     - it closes in the bottom 25% of its range;
     - its range ≥ 1.0 × ATR14 ("distinct").
  5. **Entry:** sell-stop at the wick-candle low − 1 tick, valid 3 bars (follow-through). For the long, a buy-stop at the wick bar high + 1 tick ("break above the prior candle").
  6. **Window:** 09:45–11:30 ET ("near the open", PROPOSED). The AMD example was in the first minutes, inside the catalogue's 09:30–09:45 no-entry window; the `EntryWindow = FullRth` variant runs to 15:30. One attempt per level per day.
- **Trade management and exits:**
  | Item | Rule | Source |
  |---|---|---|
  | Initial stop | Wick high + 1 tick, floored (ES 14 ticks, NQ 19, CL 15, 6E 18); skip if > 1% of equity | Video |
  | Target | `TargetMode = OtherSide` (primary): the other side of the prior range (PDL + 2 ticks for a short), whole position. Variant `VwapThenOtherSide`: half at session VWAP (if it lies between entry and the target), the runner to the other side (the BABA long's stated exits). Skip if the target is < 1 R after the floor | Video |
  | Break-even | `StopManagement = BreakEvenAt1R` (primary, the stated "stop to break-even as it moves"; the trigger is +1 R on a closed bar, PROPOSED). Variant `NoBreakEven`, per the SMB "4 Things" advice (move the stop only on a new edge). The two SMB videos disagree, so the ablation is worth running | Video C; Video B |
  | Time stop | `TimeStopMinutes` (30): exit at market at entry + 30 minutes ("5–30 minutes") | Video |
  | Session | Flat by 15:55; early close −5 min | Prog §7.6 |
  | News | Blackout −2/+10; `NewsExitMinutes` | Prog §7.6 |
- **Variant inputs:**
  ```csharp
  public enum EDay2Filter { RangeExpansion, PriorDayEvent, Off }   // default RangeExpansion
  public enum ESignalTf { M5, M1 }                                // default M5 (1m = the AMD example; cost-floor sensitive)
  public enum ETargetMode { OtherSide, VwapThenOtherSide }        // default OtherSide
  public enum EStopManagement { BreakEvenAt1R, NoBreakEven }      // default BreakEvenAt1R
  public enum EEntryWindow { OpenWindow, FullRth }                // default OpenWindow (09:45–11:30)
  // bools: OpenLocationFilter (true)
  // shared: ESide, ESingleLotMode, EControlArm, EAcceptancePolicy
  ```
  Swept numbers: `LevelTolAtr` {0.10, 0.25} × `Day2RangeAtr` {1.25, 1.5} → 4 configs (PROPOSED).
- **Instruments, timeframe, data:**
  - **Futures first:** ES, NQ, CL, GC, 6E, on 1m data (W-M ≤ 2024), with 5m signal bars built from 1m.
  - **Stocks arm (later):** the same fixed list as YV-11, traded only on Day-2 days, i.e. when the prior day's |return| ≥ 4% or its range ≥ 2 × ATR20d (mechanical, PROPOSED). Needs stock data and costs as in YV-11.
  - **Test first: ES/NQ futures, 5m, `Day2Filter = RangeExpansion`.**
- **Feasibility notes:**
  - Buildable now: F-SESS prior RTH high/low, ATR, F-VWAP, M-EVENT, F-TM.
  - 1m wick stops will usually be inside the cost floor. The 5m default keeps the bracket meaningful.
  - The fade-PDH prior is B (0), so the wick and Day-2 filters must earn their lift.
  - Frequency (author estimate): 1–3 per week per instrument on Day-2 sessions.
- **Test:** controls are the same level fade without the wick-candle condition, and without the Day-2 filter (two ablations). Also K-FL (the level shifted ±0.25 × ATR20d) and K-RT.
  - **Kill:** no lift over the no-wick fade, or mean net R ≤ 0 at ≥ 150 events.
- **Differs from:** **PS-03** (VA edge, LH confirmation, target POC). **SP-05** (profile extreme, bar delta, 1 R target). **DV-02** (a level flips after failing). **MT-06** (excess/poor daily extremes).

### SMB Market Memory Model, setups 3–5 (cited variants on existing entries; no new IDs)

| Setup | As stated | Closest entry | Variant added there |
|---|---|---|---|
| 3. Breakout Pullback (AVGO, 1m) [ts: VERIFY] | "a breakout only matters when price moves beyond the remembered level and then holds"; don't chase; wait for the pullback to the level and a wick candle; long on the break of the wick high (implied); stop under the wick low; targets unstated | **PS-24** (ON high/low break-and-go; it already has an HL-entry variant and tests the same rule at the prior RTH high); also SP-08 (point-of-origin retest) | PS-24 variant **`Level = PriorRthExtreme`** with **`Entry = WickRetest`**: after 2 closes beyond PDH + δ, a pullback to PDH ± 0.10 × ATR5 that prints a lower-wick candle (wick ≥ 0.5 × range, close in the top 25%) → buy-stop at its high + 1 tick; stop at the wick low − 1 tick, floored; target unstated, so PS-24's b sweep applies (PROPOSED) |
| 4. Hitchhiker Scalp (NVDA, 2m, after about 10:00–10:30) [ts: VERIFY] | opens above yesterday's high and holds; later fails below the level and VWAP, then a wick candle and a reclaim of the level; long on the reclaim; stop below the wick low; targets unstated | **PS-18** (stop-sweep buy on the first close back above the swept level); also YV-08 (SFP reclaim) | PS-18 variant **`Level = PriorRthHigh`** with **`Context = OpenAboveAndHold`**: the RTH open is above PDH and ≥ 30 minutes are spent above it. After 10:00, a dip below PDH and below VWAP, then a lower-wick candle, then a 2m/5m close back above PDH → buy next bar. Stop at the wick low − 1 tick, floored. Target unstated: 1.5 R or the session high (PROPOSED). The shape filter and delta condition are off in this variant |
| 5. Backside Trade (RKLB, 2m, after 12:00) [ts: VERIFY] | an early run to resistance fails repeatedly, then a tight afternoon range forms; short on the break of the range lows; target VWAP, "a lot of times leak below"; stop unstated | **A2-49** (midday support/resistance and afternoon breakout, Kaufman) | A2-49 variant **`Context = MorningFailures`** with **`Target = Vwap`**: needs ≥ 2 failed tests of the morning high (each a high within 0.1 × ATR5 of it, then a close ≥ 0.5 × ATR5 below) before 12:00. The 12:00–13:30 range must be ≤ 0.6 × its own 20-day median. Sell-stop at the range low − 1 tick; stop at the range high + 1 tick (unstated, PROPOSED); T1 = VWAP for half, runner trailed for the "leak below" |

These are recorded as variant notes on the existing entries in the catalogue. Their typed inputs (e.g. `public enum ELevel { OvernightExtreme, PriorRthExtreme }`, `public enum EEntry { CloseBeyond, HigherLow, WickRetest }` on PS-24) are decided when that entry is specified. All three need data ≤ 2024 only (1m, W-M).

### SMB "4 Things" (video B): reviewed, no strategy

Holden's four points are process: (1) read price action top-down for uniformity across timeframes; (2) know the edge (catalyst, setup, trade, who is offside; about 4–5 ideas per trade taken); (3) start with the stop, placed where the edge started; (4) "feet in deep", i.e. focus and size on a precise edge. Nothing mechanical to test.

**General trade-management note (added to catalogue §3.10):** *move the stop only when a new edge appears (e.g. a new confirmed higher low, a new initiative print, a new accepted level), not to break-even just because the trade is working.* This argues against plain +1 R break-even rules (PS-01's BE, YV-12's stated BE, A2-73), and for structure-based trails (EX-05, EX-13's imbalance trail, OF-01's aggression trail). Where a spec has a break-even rule, a `NoBreakEven` / structure-only arm is the natural ablation. This is a note, not a rule change to any registered spec.

### Dylan Richards (video D): same source as YV-07 / SP-09

- **Same video ID** (`83d144f2e0k`). The 2026-11-10 findings match YV-07 and SP-09:
  - ES; 5m levels and FVG; 1m equal lows; 15s entry;
  - "macro" windows xx:50–xx:10 (example 09:50–10:10);
  - equal highs/lows or session extremes as the draw;
  - 5m FVG respected; 15s sweep, displacement and new FVG; sell at the FVG low;
  - stop at the sweep high; target the London lows; single full exit;
  - example ESU2024, Tue 10 Sep 2024, about +10 pts.
  - Discretionary pieces ("obvious equal", "strong displacement") are already frozen numerically in SP-09.
- **One cited variant proposed for SP-09:** the video takes the **London low itself** as the target. SP-09 allows a session extreme only as one side of an equal pair. Proposed input: `public enum EDrawMode { EqualPair, SessionExtreme }`, default `EqualPair` (SP-09's frozen primary); `SessionExtreme` lets the London low (03:00–08:30 ET, SP-09's assumption; the video does not state the hours) or the prior RTH low be the target alone. It needs the Globex ETH template (Rev 2.7 §E5).
- **Data / holdout flag for the 15s arm (Arm S):**
  - 15s bars need trade ticks. The catalogue says NQ ticks start 1 Jul 2024, and the @ES bid/ask notes say dxFeed ES ticks start about 2024-10-27. These two need reconciling.
  - With tests capped at 31 Dec 2024, Arm S has at most about 125 NQ sessions, or about 45 ES sessions. That is below SP-09's own 250-session W-T block, so **Arm S cannot be tested honestly before the holdout**.
  - The 1m-trigger **Arm M** (already SP-09's primary) is the proxy.
  - Before proposing 15s at all, do a **read-only QuoteManager check** of the first trade-tick date for @ES and @NQ. That check was not done here.
  - The example day (10 Sep 2024) is before the ES tick start, so it can only be replayed on 1m.

## Open questions for Ettienne

1. **YV-11 adds:** the presenter pyramids (adds twice and raises the stop). Batch rules forbid adds (Prog §7.7). Keep `AllowAdds = false` as the primary, with adds as a variant that needs your OK?
2. **Futures vs stocks:** test YV-11 and YV-12 on ES/NQ first (proposed)? Or do you want the stock arm too, which needs a fixed list (proposed: SPY, QQQ, IWM, AAPL, AMD, NVDA, TSLA, META, AMZN, AVGO, SHOP), dxFeed stock 1m history checks and stock cost rows?
3. **"Day 2" / catalyst proxy:** prior-day range ≥ 1.5 × the 20-day median, or a tier-1 event the prior day. Is that acceptable for futures?
4. **Opening-minutes entries:** both SMB and Malyarovich trade 09:30–10:00 on 1–2m bars. The catalogue blocks entries 09:30–09:45. Keep the block (proposed), or allow a variant that starts at 09:31?
5. **YV-12 break-even:** the Market Memory video moves the stop to break-even; the "4 Things" video says don't. Primary = break-even at +1 R as stated, with `NoBreakEven` as the ablation?
6. **SP-09:** add the `EDrawMode` variant (London low as a standalone target)? Which London hours: 03:00–08:30 ET (SP-09's assumption)?
7. **Tick history:** OK to do a read-only QuoteManager check of the first @ES/@NQ trade-tick date before any 15s work? With the ≤ 2024 cap, Arm S is likely untestable.
8. **Timestamps:** the relayed findings carry no timestamps; they are marked [ts: VERIFY]. Add them on a re-watch?

## Sources
- The four YouTube videos above (URLs as listed); findings relayed 2026-11-10.
- `specs/trading-idea-catalogue.md` v2.7: §2.0, §2.1, §3.6 family prior, PS-03, PS-07, PS-18, PS-24, SP-05, SP-08, SP-09, A2-49, A2-58, A2-73, DV-02, DV-15, YV-07, YV-08, MT-06, EX family.
- `specs/video-ideas-2026-11-08-additions.md` (SP-09 judgement calls 7–9).
- `specs/strategies/00-programme-overview.md` rev 1.4: §3 rules 8–9, §4 costs, §5.1 holdout, §7.6–7.7.
- `_InstrumentProfile.Function.CS` (`EAssetClass.Stock`, share sizing, no stock cost row).
