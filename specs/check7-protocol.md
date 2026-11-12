# Check 7 protocol (throwaway)

Scratch studies for Portfolio Trader questions L1–L9. They are not meant to stay. Delete `_TestPortMM.Signal.CS`, `_TestCheck7Roll.Strategy.CS`, and this file after the sitting unless Ettienne says otherwise. `_TestPortArm` was already in the repo; its header now points here.

Machine: EttienneMiniPC. Data: dxFeed. Do not change production studies, fees, or their inputs.

APIs used (`AllowEntries`, `AllowExits`, `EntryContracts`, `ForceClosePosition`, `Paused`, `PortfolioData`, `PortfolioCurrency`, `StrategyCurrencyCode`, `PositionSide`) are the members in `specs/reference/mc-portfolio-api.txt` and are already called by `_PortfolioRiskManager`. This VM cannot compile against the installed MultiCharts .NET assembly. If a study fails to compile in the PowerLanguage Editor, paste the error; that is the L5-style answer for that member.

## Import

PowerLanguage Editor, in order, then Compile:

1. `_PortMM.Function.CS` (already imported if M-PORT is in the workspace)
2. `_CustomSignal.Strategy.CS`
3. `_TestPortArm.Strategy.CS`
4. `_TestPortMM.Signal.CS`
5. `_TestCheck7Roll.Strategy.CS` (L9 only)
6. `_PortfolioRiskManager.Strategy.CS` (L5 only; already imported if M-PORT is)

`PLStudies.csproj` lists the two new files. If the Editor does not pick them up from the project, import the `.CS` files once by hand.

## Common Portfolio Trader setup

- New portfolio. Two strategies. Each strategy contains **only** `_TestPortArm`.
- Index 0 = `@ES` 1-minute, index 1 = `@NQ` 1-minute, both dxFeed. Symbol-table order is `MyIndex`.
- L7 replaces `@NQ` with `@CL` 5-minute. L9 is a separate one-strategy portfolio (`_TestCheck7Roll`).
- Strategy Properties → trade size = **1**.
- Portfolio Settings → Exposure and Max % Capital at Risk per Position = **100% / 100%**.
- Money management signal = `_TestPortMM`, except the L2 no-MM run and the L5 production-MM run.
- Set the **same** `Test` and `Variant` on `_TestPortArm` (both rows) and on `_TestPortMM`.
- Backtest first. Date window below is short on purpose.
- Read the PowerLanguage Editor **Output** window. Lines start with `P5b L` or `C7 ROLL`.

`buy next bar market` fills on the **next** bar's open. That is the order type, not an MM lag. An MM lag is an extra bar beyond that.

## L1 — same-step MM gating (backtest)

| | |
|---|---|
| Test / Variant | `L1` / `a` |
| Symbols | `@ES` 1-min + `@NQ` 1-min |
| Window | One Globex session, e.g. 2024-11-18 18:00 ET through 2024-11-19 17:00 ET |
| MM | `_TestPortMM`. Denies every strategy, then `AllowEntries = true` on index 0 only when `Bars.CurrentBar` is odd |

Capture: `P5b L1 MM bar(n) time(..) odd(..) allow0(..)` and `P5b L1 ARM#… FILL entry time(..) size(..)`.

**Answer.** Index 0 (`@ES`) entry fills only on the open of the bar **after an odd MM bar**. Index 1 (`@NQ`) never fills.

**Fail.** `@ES` fills on the open after an even bar (MM effect lagged one step), or `@NQ` fills.

## L2 — `EntryContracts` default (backtest, four runs)

| | |
|---|---|
| Test | `L2` on both strategies. Strategy Properties size stays **1**. The signal sends a coded size of **3** |
| Window | Same one-session window as L1. One fill is enough; stop after the first `FILL entry` |

| Run | Variant | MM | What the MM does |
|---|---|---|---|
| no MM | `a` | none (clear Money management signal) | — |
| a | `a` | `_TestPortMM` | allows entries, does not write `EntryContracts` |
| b | `b` | `_TestPortMM` | writes `EntryContracts = 0` |
| c | `c` | `_TestPortMM` | writes `EntryContracts = -1` |

Capture: `P5b L2 ARM#… FILL entry … size(n)` and `P5b L2 MM variant(..) EntryContracts #i before(..) after(..)`.

**Answer.** Record the four sizes. The sheet's expected pass is (b) = 1 (properties size) and (c) = 3 (code size). Also record the no-MM size and the `before` value on variant `a` (the untouched default).

## L3 — `ForceClosePosition` (backtest, two runs)

| | |
|---|---|
| Test | `L3`. Signal buys once and never sends an exit |
| Variant `a` | `AllowExits` left true. After index 0 has been long for 3 MM calls, one `ForceClosePosition()` |
| Variant `b` | same, and `AllowExits = false` on index 0 every call |
| Window | Same one-session window |

Capture: `P5b L3 MM FORCECLOSE T bar(..) time(..) close(..) allowExits(..)`, each `P5b L3 MM after-T … pos(..)`, `P5b L3 MM RESULT closed(..)`, and `P5b L3 ARM#… FILL exit time(..) price(..) barOpen(..)`.

**Answer.** Compare the exit price with the `barOpen` on the ARM line for the bar after T, and with T's close.

- Pass the sheet's assumption if the exit price is the T+1 open **and** variant `b` still prints `RESULT closed(Y)`.
- If `b` stays open (`closed(N)`), `AllowExits=false` blocks the force-close. Do not set `AllowExits=false` in the MM.
- If the fill is T's close instead of T+1 open, record that and treat §6's "next open" assumption as wrong.

## L4 — `Paused` (backtest only, two runs)

Do not run this with a live or paper order route.

| | |
|---|---|
| Test | `L4`. Buys once, then sends a sell stop at entry − 10 ticks every bar |
| Variant `a` | MM sets `Paused = true` on index 0 as soon as it is long |
| Variant `b` | control: MM never pauses |
| Window | Start with one session. If `STOP crossed` has not printed, extend up to five sessions. A quiet day may not trade through the stop |

Capture: `P5b L4 MM PAUSED`, `P5b L4 MM … paused(..) pos(..) stamp(..) stampAdvancing(..)`, `P5b L4 ARM#… STOP crossed`, and any `FILL exit`.

**Answer (either result is a completed check).**

- Did the stop fill after `STOP crossed` while paused? (`FILL exit` = yes.)
- Did `stampAdvancing(True)` continue, and did ARM `STAMP` lines continue? That is "still calculating / publishing".
- Variant `b` should fill. If it does not, the stop test itself is broken; do not read variant `a`.

## L5 — how a .NET MM is designated (backtest)

Two selects, same `@ES`+`@NQ` portfolio, Test `L1` so the strategies are harmless if the MM denies them. One session.

1. Portfolio Settings → Money management signal → Select. Record whether `_TestPortMM` is in the list. Apply it. Pass for the throwaway: Output shows `P5b L5 MM selectable+running` and `P5b L5 MM first CalcBar`.
2. Select `_PortfolioRiskManager` instead. Pass: it is in the list and prints `PORT RUN strategies(..) signal(_PortfolioRiskManager)`.

Record any attribute, base-class, or "needs a symbol" error. The throwaway MM reads `Bars` (the wiki says the MM is calculated on the first instrument). If step 1 throws on `Bars` before any `P5b` line, the answer is that this MM object has no bar series. Paste the exception.

No separate data series is attached to the MM in this protocol. If MultiCharts requires one, that requirement is the L5 answer.

## L6 — `IPortfolioData` reference sharing (backtest, then IBKR paper)

| | |
|---|---|
| Test / Variant | `L6` / `a` |
| Orders | none |
| Backtest window | one session, same as L1 |

The strategy puts one `_TestPortCounter` object in `MyPortfolioData["test.counter"]` and stores `RuntimeHelpers.GetHashCode` under `test.counterHash`. The MM reads that reference back.

Capture: `P5b L6 ARM#… COUNTER hash(..) value(..) storedSame(..) created(..)` and `P5b L6 MM #i hash(..) publisherHash(..) sameRef(..) value(..) created(..) rt(..) auto(..) appDomain(..)`.

**Backtest answer.** `sameRef(True)` and the MM `value` equals the ARM `value` on that step (the MM runs after the strategies, so it should see the incremented value). `storedSame(True)` means the strategy read its own object back.

**Paper, still required.** Real-time Portfolio Trader on the IBKR paper profile, auto-trading on, same two symbols, a few minutes. Then stop Portfolio Trader and start it again.

- Pass the sheet's assumption if paper also shows `sameRef(True)` and `rt(True)`, and the restarted run prints a new `created` time with `value` starting over (no persistence).
- If paper shows `sameRef(False)` or `type …` instead of an int value, object sharing does not survive that mode. M-PORT would have to stay on scalars there.

## L7 — mixed resolutions (backtest)

| | |
|---|---|
| Test / Variant | `L7` / `a` |
| Symbols | index 0 `@ES` **1-minute**, index 1 `@CL` **5-minute** |
| Window | 2024-11-19 09:30–11:00 ET (enough 1-minute steps and several 5-minute bars) |
| MM | allows entries. The 1-minute strategy will buy once while flat; that does not affect the stamp log |

Capture every `P5b L7 MM call(n) time(..) wall(..)` and `P5b L7 MM #i sym(..) stamp(..) lag(..)`.

**Answer.** One MM call per 1-minute `@ES` step. `@CL`'s `stamp` changes only on 5-minute boundaries and `lag` is then about 0–4 minutes. If the MM is called only on 5-minute boundaries, it is not stepping with every strategy.

## L8 — currency (backtest, any L-test)

Free on every `_TestPortMM` run. Read the StartCalc line and the first CalcBar lines:

- `P5b L8 PortfolioCurrency(…) usd(…)`
- `P5b L8 StrategyCurrencyCode #i sym(..) code(..)`

**Answer.** Pass if both are USD (`eMTPA_MCSC_USD` / `usd(True)`). Otherwise the production MM will deny sizing; fix the symbol currency in QuoteManager and rerun.

## L9 — IBKR symbol-mapping roll (paper; backtest is only the detector)

This is Check 8. Expected equity roll **Thu 10 Dec 2026** (Monday-before-third-Friday rule for the Dec 2026 expiry). Confirm the date on cmegroup.com before the week. dxFeed's `@ES` switch can differ; record both.

### Backtest evidence (no broker)

One strategy, **only** `_TestCheck7Roll`, `@ES` 1-minute, dxFeed, 2024-12-13 through 2024-12-18. No MM. Strategy Properties size 1.

Capture: `C7 ROLL START … dates(..) digest(..)`, `C7 ROLL ARM`, and every `C7 ROLL FIRE time(..)`.

**Answer for the detector.** One `C7 ROLL FIRE`. The bar time should be the Globex open that belongs to the profile roll day (ES Dec 2024 roll day is Mon 2024-12-16, so the print is often Sun 2024-12-15 18:00 ET). A second fire, or none, is a fail for Table mode. This does **not** answer the IBKR mapping.

### Paper, still required (around 10 Dec 2026)

1. IBKR paper profile. TWS/Gateway API on, Read-Only API off, socket port matched.
2. File → Broker Profiles → Symbol Mapping. Chart symbol `@ES` (dxFeed) → broker symbol `ESZ6` (IB). Apply. Add the reverse line if the mapping does not save.
3. Portfolio Trader, one strategy `_TestCheck7Roll` on `@ES` 1-minute, auto-trading on, from a few sessions before the roll. A 1-lot position into the window is optional.
4. On the dxFeed switch day, record `C7 ROLL FIRE` time, the CME date, and the session-open gap if the journal shows one.
5. Stop auto-trading. Flatten or roll the IB position in TWS. Change the mapping to `ESH7`. Check Portfolio Trader position vs TWS. Restart the portfolio. Send one 1-lot and confirm TWS shows **ESH7**.

**Pass.** Detector fired once on the switch day; after the remap the test order is ESH7; MC position equals IB position.

**Fail.** Order on the expired month, a missed or extra fire, or a position mismatch.

## What this protocol does not decide

- Whether the MM's `Bars.CurrentBar` parity matches each strategy's bar index. L1 uses the MM's own `Bars.CurrentBar`. If that series is strategy 0 only, say so in the notes.
- L6 and L9 paper results. Backtest cannot see another process or an IBKR mapping.
