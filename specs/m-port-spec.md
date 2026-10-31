---
title: "M-PORT — portfolio money management on MultiCharts .NET Portfolio Trader (implementation spec, rev 2)"
date: 2026-10-31 (rev 2 = risk-profiles draft v6.1, approved for implementation by Ettienne Hugo 2026-10-31; rev 1 2026-10-21)
status: "rev 1 coded (PRs #14/#17/#22); rev 2 adds risk profiles, deny-not-reduce caps and per-strategy session/weekly throttles; implementation in progress: PR-B0 (#60, publisher pm.R$w + sizeForRisk 1% skip) in review; PR-B (profiles, deny-not-reduce), PR-C (session/weekly throttles), PR-D (advisory counts) pending — each implementation PR updates this line"
code_refs: "line refs re-verified against origin/main @ 3f9e9be (2026-10-31, merge of PR #58); _PortMM.Function.CS, _PortfolioRiskManager.Strategy.CS, PLStudies.csproj and tests/PortMMTests are byte-identical to 800bce7 (the draft's base); _CustomSignal.Strategy.CS refs moved +6 (portfolio block) and +36 (CS-4/CS-7 block)"
---

# M-PORT implementation spec (rev 2)

**Inputs to this spec:** `specs/mc-portfolio-api.txt` (user-supplied MC .NET metadata: `PortfolioSignalObject`, `IPortfolio`, `IPortfolioStrategy`, `IPortfolioData`, `IStrategy`, `MTPA_MCSymbolCurrency`); MC wiki *Category:Portfolio Trading* (Portfolio Trader Strategy Examples, Understanding Portfolio Backtesting, Configuring Strategies, Chart vs Portfolio Backtesting); the repo `MultiCharts.Net` `main` @ `9603948` (`_CustomSignal.Strategy.CS`, `_MarketBreadth.Strategy.CS`); `specs/missing-functionality-specs.md` §8 (earlier M-PORT design, now superseded where this file differs).

> **Note on the examples.** The six example signals named by the user (`Portfolio_Rank_MM`, `Portfolio_Rank_Signal_Base`, `Portfolio_Rotation`, `Portfolio_Rotation_MM`, `Portfolio_SpreadTradingSystem_Master/_Slave` `.Strategy.CS`) are **not in the repo** (checked `main` after `git pull`, every remote branch and the full git history on 2026-10-21). This spec uses their PowerLanguage originals on the MC wiki page *Portfolio Trader Strategy Examples*, mapped to the .NET API above, plus the repo's own working .NET example `_MarketBreadth.Strategy.CS` (publishes via `MyPortfolioData[...]`, reads via `PortfolioStrategies[i].PortfolioData[...]`). If the .NET example files are added later, re-check §3 and §9 against them.

## 0. Scope and rules

- **Goal:** book-level sizing and throttles across all strategies in one Portfolio Trader (PT) portfolio: vol-target / risk-parity size, portfolio heat cap, drawdown throttles, correlation-cluster budgets, kill switches.
- **Where:** MC Portfolio Trader only. On single charts M-PORT is inert and CS-4 `sizeForRisk` applies unchanged.
- **No new `[Input]`s on `_CustomSignal`.** Publishing is switched by a protected property. The MM signal has **one `[Input]`**, `TotalRiskCapPct` (§5.4 item 0; an explicit exception Ettienne asked for, 2026-10-31); otherwise no `[Input]`: the risk profile is the protected `Profile` property; `Config`, `ConfigPath` and `LiveIds` are protected virtuals. One subclass per profile (`_PortfolioRiskManager` = starter, `_PortfolioRiskManager_ScaledBook` = scaled-book). See §5.1a.
- **Rely on MC built-ins:** PT Money Management settings (Initial Capital, Exposure %, Max % Capital at Risk per Position, Margin/Potential Loss per contract), `IStrategy.Portfolio` (`IPortfolioPerformance`), `CurrentPosition`, `RiskCapital`, `Status` column, MC commissions/slippage. M-PORT adds only what MC lacks: cross-strategy risk aggregation, correlation and throttles.

## 1. API (confirmed, P5)

| Object | Member | Use in M-PORT |
|---|---|---|
| `PortfolioSignalObject` (base of `_CustomSignal`, `_MarketBreadth`) | `PortfolioStrategies : IROList<IPortfolioStrategy>` | MM loops over strategies (index = `MyIndex`) |
| | `MyPortfolioData : IPortfolioData` | per-strategy publish (`object this[string]`, `ContainsKey`) |
| | `CommonPortfolioData : IPortfolioData` | book-level values written by the MM (`pm.*` keys), read by signals |
| | `PortfolioCurrency : MTPA_MCSymbolCurrency` | assert USD (all our futures are USD) |
| `IPortfolioStrategy` | `Signals : IStrategy[]` | `Signals[0].Bars.Info` (symbol, BPV), `PositionSide`, `CurrentPosition`, `Portfolio` |
| | `PortfolioData` | read the strategy's published values |
| | `EntryContracts` (int, get/set) | **< 0** = size from code; **0** (default) = Strategy Properties size; **> 0** = override for every entry |
| | `AllowEntries`, `AllowEntriesLong/Short` | heat cap, throttles, cluster budget |
| | `AllowExits`, `AllowExitsLong/Short` | **never set false** (rule: the MM never blocks exits) |
| | `Paused` (get/set) | hard kill of a strategy ("allows/forbids order placement" — blocks exits too, see §6) |
| | `ForceClosePosition()`, `PartialClosePosition(bool isNextBar, int contracts)` | kill switch / de-risking |
| | `RiskCapital` (get) | MC's own margin/max-loss capital for the strategy (cross-check of `risk.open`) |
| | `Status` (set) | text in the PT real-time "Custom Text" column |
| | `MyIndex` | stable index for keys and logs |
| `IStrategy` | `PositionSide`, `CurrentPosition`, `Positions`, `Portfolio`, `InitialCapital`, `NetProfit`-style stats (`GrossProfit`, `GrossLoss`, `MaxDrawDown`, `TotalTrades` …) | open-position and P&L reads without needing a publish |

**Missing vs PowerLanguage `pmms_*`** (must be implemented in C#, see §5): `pmms_calc_contracts_for_entry`, `pmms_calc_money_cost_for_entry_per_cntrct`, `pmms_strategies_in_long_count`, `pmms_strategies_get_by_symbol_name`, `pmms_to/from_portfolio_currency`, `pmms_strategies_deny_entries_all`. All are trivial loops over `PortfolioStrategies`; currency conversion is not needed while everything is USD (assert `PortfolioCurrency == eMTPA_MCSC_USD` and `Signals[0].StrategyCurrencyCode == USD`, else refuse to size and set `Status`).

## 2. Pattern (from the examples)

Every wiki example and `_MarketBreadth` follows the same two-role pattern:

1. **Per-strategy signal (publisher):** computes its own value each bar and writes it into its strategy's portfolio data (`pmm_set_my_named_num` → `MyPortfolioData[key] = value`). It **always generates its entries** ("buy next bar market; sellshort next bar market" in Rank/Rotation) and leaves the gating to the MM.
2. **MM signal (controller):** each calculation, first **deny entries for all** (`pmms_strategies_deny_entries_all` → `foreach s: s.AllowEntries=false`), read every strategy's values (`s.PortfolioData[key]`), rank/aggregate, then **re-allow and size** the chosen ones (`AllowEntriesLong/Short = true`, `EntryContracts = n`) and **force-close** ones that dropped out (`ForceClosePosition`).
3. **Master/slave (spread example):** a strategy may write into *another* strategy's data (`pmms_set_strategy_named_num(slave_idx, "MPMoney", …)` → `PortfolioStrategies[i].PortfolioData[key] = v`). We use this only for M-XMKT two-leg trades later, not in M-PORT v1.
4. **Typed values:** .NET `IPortfolioData` stores `object`, not just numbers (`_MarketBreadth` stores a `SortedDictionary<DateTime,Entry>`). M-PORT publishes small immutable snapshots and a ring buffer of daily returns (§3) instead of the MM rebuilding history from scalars.

## 3. What `_CustomSignal` publishes (standard keys)

Enabled by `protected virtual bool PortfolioPublish => Environment.ApplicationCode == EApplicationCode.Portfolio;` (no input). Written at the **end of `CalcBar`** (after the strategy's own logic decided its orders, so values describe the order about to be sent). Keys are constants in a new `_PortKeys` static class (`_PortMM.Function.CS`). All money in account currency (USD); all R in units of the strategy's own 1R.

| Key | Type | Written | Meaning |
|---|---|---|---|
| `pm.ver` | int | StartCalc | schema version (=1); MM refuses mismatches |
| `pm.id` | string | StartCalc | `Name` + symbol + idea ID (e.g. `"PS-01@ES"`) |
| `pm.family` | string | StartCalc | catalogue family (`PS`, `VW`, `PF` …) via `protected virtual string PortfolioFamily` |
| `pm.cluster` | string | StartCalc | correlation cluster seed: root symbol class (`EQIDX` for ES/MES/NQ/MNQ/YM/RTY, `RATES`, `ENERGY`, `METALS`); MM may merge clusters by measured ρ (§5.5) |
| `pm.eq` | double | each bar | strategy equity = `InitialCapital + NetProfit + OpenProfit` (closed + open) |
| `pm.ret` | `_PortReturns` | session close (first bar of new `tradingDayKey`) | ring buffer (len 260) of **daily returns of strategy equity** in $ / book equity at the prior close, plus `lastDay`; immutable copy-on-write |
| `pm.uRet` | `_PortReturns` | session close | ring buffer of **underlying** Data1 daily log returns, roll-gap removed via M-DAILY (needed for ρ before the strategy has traded) |
| `pm.vol` | double | session close | Atr20d × BPV per contract (M-DAILY), i.e. $ daily vol per contract |
| `pm.R$` | double | when an entry is pending or a position is open | $ of 1R per contract at the **structural** stop = \|entry − structural stop\| × BPV (same number CS-4 `sizeForRisk` uses). **Sizing input** for RiskParity and the unit of R for kill / session / weekly R |
| `pm.R$w` | double | with `pm.R$` (optional, rev 2) | $ per contract at the **widened** stop = \|entry − max-distance(structural, cost floor e.g. 14 ticks ES)\| × BPV, i.e. the stop the order will carry. ≥ `pm.R$`. Absent/NaN ⇒ = `pm.R$`. **Risk input** for the 1% skip and the heat/cluster caps |
| `pm.qtyCode` | int | with `pm.R$` | contracts the code would trade (CS-4, sized on the structural stop; never reduced by the floor) |
| `pm.riskNew` | double | each bar | $ risk of the pending entry at the **widened** stop = `pm.R$w × qtyCode`, 0 if none |
| `pm.riskOpen` | double | each bar | $ open risk = \|price − active stop\| × BPV × \|position\| (0 if the stop is beyond breakeven; never negative) |
| `pm.openR` | double | each bar | open P&L in R |
| `pm.state` | int (enum `EPortState`) | each bar | `Flat=0, Armed=1` (setup live, entry order pending), `Long=2, Short=3, Exiting=4, Halted=5` (own kill rule / F-TM daily stop hit) |
| `pm.side` | int | with state | +1/−1 intended side of the pending entry |
| `pm.regime` | int | session close | strategy's regime tag (`protected virtual int PortfolioRegime`; default 0 = unknown; e.g. M-DAILY TSMOM sign, vol bucket) |
| `pm.regimeP` | double[] | session close, only when `RegimeConfig.publish` | **optional** (added by phase3-specs §3.3 G-R, open question Q10): filtered regime probabilities P(s_t \| x_1..x_t), index = label (0 = calmest); empty array = unknown; a fresh array each close (copy-on-write). `pm.regime` then = Label + 1. Prior/bucket only, never an entry trigger |
| `pm.score` | double | optional | rank/priority value (Rank example `RankStrategyR`); default `pm.R$`-free expectancy proxy = NaN → MM uses index order |
| `pm.stamp` | DateTime | every write | `Bars.Time[0]`; MM ignores data older than the current bar's day (stale) |

**Structural and widened R$ (rev 2, P1 DECIDED).** Keep `pm.R$` = structural stop (its rev 1 meaning, the same number CS-4 `sizeForRisk` uses, no change for existing publishers) and add one optional key `pm.R$w` = widened stop. Absent / NaN `pm.R$w` ⇒ the MM uses `pm.R$` (strategy has no floor, widened = structural). Additive optional key, so **`pm.ver` stays 1**: the gate is `_PortKeys.Version = 1` (`_PortMM` L21), written once in `startPortfolio` (`_CustomSignal` L1564) and checked in `_PortfolioRiskManager.view()` L112–113 (else reason "no pm.ver", `_PortMM` L454); readers pull keys by name (`get()`/`num()`, `_PortfolioRiskManager` L141–146) and no reader enumerates `PortfolioData`, so an unknown key is ignored (precedent: optional `pm.regimeP`, `_PortMM` L26–28). No MM code reads `pm.riskNew` (only the publisher writes it, `_CustomSignal` L1570/L1588/L1642; the loop recomputes risk from `rDollar`), so its change of meaning is safe. **Caveat: the MM and all publishers must ship from the same build** (same DLL) — an old MM with a new publisher sizes correctly on structural `pm.R$` but has no 1% skip, no widened caps and still trims.

`pm.riskOpen` is unchanged (it already uses the active stop, which is the widened one once the order is live). Code: `portfolioArm(side, entry, stop, qtyCode, double widenedStop = double.NaN)` (optional parameter, existing callers unchanged). **`sizeForRisk` keeps sizing `qty` and publishing `pm.R$` on the structural stop** (`stopLossBasedPositionSize(bars, entry, stop, …)` `_CustomSignal` L2834 and `portfolioArm(…, stop, qty)` L2845). A strategy with a floor passes the floored stop **only** as the new `widenedStop` argument — a new optional `sizeForRisk(…, double widenedStop = double.NaN)` parameter forwarded to `portfolioArm`, or a direct `portfolioArm` call; it never goes into the `stop` argument (that would shrink `qtyCode` and make `pm.R$` widened). **No stale key:** `MyPortfolioData` keeps values between bars and the per-bar reset (L1642) clears only `riskNew`/`side`, so `portfolioArm` **always** writes `pm.R$w` (the widened R$, or NaN when `widenedStop` is not given or is closer than the structural stop) and `startPortfolio` (L1556–1573) initialises `pm.R$w` = NaN. NaN reads as `num()` NaN ⇒ Rw = `pm.R$`. Until CostConfig lands, only strategies with per-example floor code publish a finite `pm.R$w`.

Rules:
- Publishing is **side-effect-free** for chart runs and costs one dictionary write per key per bar.
- `pm.riskNew` must be written on the bar the entry order is generated (orders fill next bar open in PT), else the heat cap sees it one bar late.
- `_CustomSignal` reads back `CommonPortfolioData["pm.mult"]` (book risk multiplier, §5.4) **only to journal it**; sizing changes are applied by the MM through `EntryContracts`, not by each strategy, so there is one source of truth.

## 4. Order of calculation (what we know, what we assume)

From *Understanding Portfolio Backtesting* and *Configuring Strategies*:
1. Per bar step, each symbol's bar is evaluated by its strategy's signals **in the PT symbol-table order**; order sets are produced in that order.
2. Signals in one strategy run in Signals-list order (top = higher priority).
3. Risk control then executes order sets in sequence until PT Money Management limits (Exposure %, Max % risk per position, margin, max potential loss) block the rest; the rest are **discarded**. Symbol priority = table order (or `PortfolioEntriesPriority`, which we do **not** use — legacy, conflicts with the MM signal).
4. Orders are "next bar" in PT backtests; **IOG is not available in Portfolio** (Chart-vs-Portfolio page) and Bar Magnifier is unavailable → any idea whose chart version relies on IOG or magnifier will differ in PT.
5. **MM signal timing (assumed, live check L1):** the MM signal runs **after all strategies** have calculated for the bar step and **before** risk control/order sending, so `AllowEntries`/`EntryContracts` set in the MM affect the orders generated in the same step (this is how Rank/Rotation work: deny-all then allow-best each calc). Values the MM writes to `CommonPortfolioData` are seen by strategies only on the **next** step.
6. **Mixed timeframes:** strategies on different resolutions are interleaved by time; on a step where a strategy has no new bar its published values are from its last bar → use `pm.stamp`.

Consequences for the design:
- The MM **must not assume `AllowEntries` persists** as intended across steps: start every calc with deny-all and re-allow (example pattern) — idempotent.
- Anything a strategy needs *from* the MM in the same bar (e.g. size) must be applied via `EntryContracts`, not read from `CommonPortfolioData`.

## 5. `_PortfolioRiskManager` (MM signal)

File `_PortfolioRiskManager.Strategy.CS`, class `_PortfolioRiskManager : PortfolioSignalObject` (not `_CustomSignal`; it trades nothing). Helpers in `_PortMM.Function.CS` (`_PortKeys`, `_PortReturns`, `PortMmConfig`, `_PortMath` — pure C#, unit-tested off-MC under `tests/PortMMTests`). Applied as the portfolio **Money Management signal** in PT settings.

### 5.1 Config (`PortMmConfig`) and risk profiles

Two profiles. **Starter is the default** (`new PortMmConfig()` and `_PortfolioRiskManager`) until ≥ 3 uncorrelated strategies are live (§5.1a). Bracketed P-numbers name the decision behind a value (decision sheet `specs/m-port-risk-profiles-decisions.md`, committed next to this file; all decided).

| Field | Starter (default) | Scaled-book | Meaning |
|---|---|---|---|
| `profile` | `Starter` | `ScaledBook` | base set of defaults used (journalled; new) |
| `sizing` | `RiskParity` | `RiskParity` | `CodeSize` (EntryContracts = −1; multipliers < 1 deny), `VolTarget`, `RiskParity` |
| `bookVolTarget` | **0.12** | 0.10 | annualised book vol target (VolTarget; PF-06 12%) |
| `riskPerTrade` | **0.005** (P1 DECIDED) | 0.005 | book fraction per new entry at m = 1, sized on the **structural** stop `pm.R$` (PF-04 cap 0.5%) — P1 DECIDED |
| `maxTradeRisk` (new) | 0.01 | 0.01 | hard per-trade cap (user rule, catalogue §2.1): n × widened R$ (`pm.R$w`) > this × book ⇒ deny (skip). Not configurable above 0.01 |
| `heatCap` | **0.015** | 0.04 | max Σ open + new **widened** risk / book; breach ⇒ deny (PF-04 1.5%) |
| `clusterCap` | **0.0075** (P2 DECIDED) | 0.02 | max widened risk per merged cluster, checked only when the cluster already holds a position or seeded open risk (P2 (c); v6.1); breach ⇒ deny; ≤ `heatCap` |
| `rhoMerge` / `rhoWin` / `rhoMinDays` | 0.9 / 60 / 40 | 0.9 / 60 / 40 | cluster merge threshold, window (sessions), min common days |
| `maxPositions` | **2** | 6 | concurrent positions (PF-04 "≤ 2 cards") |
| `ddSteps` | **{3%→0.5, 6%→0.25, 10%→0}** (P3) | {5%→0.5, 10%→0.25, 15%→0} | drawdown-from-peak multiplier (PF-05) |
| `ddRecoverHighDays` | 20 | 20 | hysteresis: new N-session high or dd < half step |
| `dailyLossStop` / `dailyLossFlatten` | 0.02 / 0.03 | 0.02 / 0.03 | book loss today ⇒ deny entries / flatten all (A2-78; kept as decided) |
| `stratDailyLosses` (new) | **2** | 2 (P5 DECIDED) | losing closes in a strategy's session ⇒ its entries denied for that session (§5.4 4a) |
| `scratchR` (new) | 0.05 | 0.05 (P7) | \|R\| ≤ this is a scratch, not a loss (loss count only) |
| `weeklyHalveR` / `weeklyHalveMult` (new) | **−3 / 0.5** | −3 / 0.5 (P5 DECIDED) | weekly closed R ≤ −3 ⇒ size × 0.5 (§5.4 4b) |
| `weeklyObserveR` (new) | **−6** | −6 (P5 DECIDED) | weekly closed R ≤ −6 ⇒ observation only (entries denied) |
| `weeklyObserveReset` (new) | `Weekly` (P6 DECIDED) | `Weekly` (P6 DECIDED) | `Weekly` only: the −6R observe state clears at the strategy's first session of the next ISO week, like −3R. A `Manual` (persist until portfolio restart) option is **not adopted** (2026-10-31) |
| `stratKillR` / `stratKillTrades` | −8 / 20 (P4) | −8 / 20 | permanent kill: cumulative R over last N trades ⇒ flatten + `Paused` |
| `staleBars` | 3 | 3 | publisher stale ⇒ deny its entries |
| `minContracts` | 1 | 1 | below this after m-scaling ⇒ deny |
| `idmCap` | 2.5 | 2.5 | VolTarget diversification cap |

Validation (in addition to the coded rules): `0 < riskPerTrade ≤ maxTradeRisk ≤ 0.01`; `riskPerTrade ≤ heatCap`; `clusterCap ≤ heatCap` (coded); `weeklyObserveR < weeklyHalveR < 0`; `0 < weeklyHalveMult < 1`; `stratDailyLosses ≥ 1`; `0 ≤ scratchR < 1`.

**Starter consistency.** Sizing is 0.5% at the structural stop; real (widened) risk per trade is 0.5%–1%. Under P2 DECIDED (c) the cluster cap is checked only when the cluster already holds a position (or seeded open risk, v6.1), so a **lone** entry in an empty cluster may carry the full 1% widened risk (limited by `maxTradeRisk` and heat only). Two entries at structural risk in different clusters fit 1.0% ≤ heat 1.5%; two entries in **different** clusters whose floor widened them past 0.75% each do not — the second is **denied** by the heat cap (accepted consequence of P1: the floor can cost a position, never contracts). In the **same** merged cluster (ES and NQ are one) a second entry is checked against clusterCap 0.75%: with the first at ≥ 0.5% widened any full-size second entry exceeds 0.75% and is denied — "cluster cap", or "heat cap" when the pair also exceeds 1.5% (heat is checked first) — one position per merged cluster. Only after a 0.5 multiplier (weekly halve or ddSteps stage) can a 0.25% entry join an open position of exactly 0.5% widened risk in one cluster (sum exactly 0.75%). Daily deny at −2% ≈ 4 structural R, flatten at −3% ≈ 6R. ddSteps 3% ≈ 6R will trigger in normal variance (accepted tuning value).

**P1 — DECIDED (Ettienne Hugo, 2026-10-29): "size from the structural stop; skip if the widened risk is over 1%; caps deny".**
1. The strategy computes its structural stop and the widened stop = max(structural, cost floor, e.g. 14 ticks ES) and publishes both (`pm.R$`, `pm.R$w`).
2. The MM sizes `n = floor(m × riskPerTrade × book / pm.R$)` — structural, 0.5% at m = 1. The floor never changes `n`.
3. If `n × pm.R$w > maxTradeRisk (0.01) × book` the entry is skipped (denied, journal "per-trade budget"). The order, when allowed, carries the widened stop.
4. Heat and cluster caps use the widened risk `n × pm.R$w` and deny on breach; the MM never trims `n`.
5. On chart and other non-portfolio runs (`m_portOn` false), and for any Data2 call (`bars != Bars`, which never arms), the strategy does the same skip with its own qty (CS-4 size on the structural stop; skip threshold a fixed 0.01 of equity — never the MM's 0.5%). Implemented in `sizeForRisk` (PR-B0 item 3; test T-P21): `qty × widened R$ > 0.01 × equity` ⇒ return 0 — never a smaller qty. **When publishing to an MM (`m_portOn`, Data1) `sizeForRisk` does not skip:** it arms with qty and `widenedStop` and the MM decides (RiskParity/VolTarget on `n × Rw`, CodeSize on `qtyCode × Rw`, both against book), so the deny reason lands in the MM journal (v4 review R1).
   Note on "never cut size": `sizeForRisk`'s existing `maxQty` and notional caps (`_CustomSignal` L2836–2842) can lower qty. They are limits the strategy author sets, not M-PORT caps, and rev 2 does not change them.

Loaded from `ConfigPath` JSON if given (field-by-field over the selected profile's defaults); else the profile defaults. Journal the effective config and profile once (`RUN` line; `config=fallback` when a fallback happened, also in `Status`).

### 5.1a Profile selection

- `_PortfolioRiskManager`: `protected virtual EPortProfile Profile => EPortProfile.Starter;` `protected virtual PortMmConfig Config => PortMmConfig.ForProfile(Profile);` Ship `_PortfolioRiskManager_ScaledBook : _PortfolioRiskManager` overriding `Profile`. No `[Input]`.
- A config that fails Validate falls back to `ForProfile(Profile)` — never to another profile.
- JSON: `"profile"` is applied first and replaces the base (`ForProfile(p).Copy()`), then other fields override. No `profile` ⇒ base = the subclass `Config`. Unknown profile ⇒ warning, subclass base kept. (P8, analysts' call.)
- **Manual only.** The MM never changes profile or limits. Switching = select the other MM signal (or edit the JSON) in PT and restart the portfolio; journal both.
- **Live strategies:** `protected virtual ISet<string> LiveIds => null` (null = every publishing strategy) or JSON `liveIds` (array of `pm.id`). Live = in `LiveIds`, valid `pm.ver`, not stale, not `Paused`/killed, not in weekly observation. (Separated from P8.)
- **"3+ uncorrelated live strategies"** = `uncorr` ≥ 3: after the daily §5.5 merge (union-find on max(ρ, stressed ρ) ≥ `rhoMerge` 0.9; `pm.cluster` seeds always merged, so ES/MES/NQ/MNQ/YM/RTY are one), the number of distinct clusters with ≥ 1 live strategy. < `rhoMinDays` own `pm.ret` ⇒ `pm.uRet` (as in §5.5). The MM also reports `uncorr03` (P9) and journals once per day `PORT advisory: uncorr=N (ρ≥0.9 clusters), uncorr03=M (greedy ρ<0.3) — scaled-book eligible when <rule>; switch manually`. **Switch rule (P9 DECIDED):** scaled-book eligible when `uncorr` ≥ 3 **and** `uncorr03` ≥ 3; `<rule>` in the line reads `uncorr≥3 and uncorr03≥3`.

### 5.2 Per-calc algorithm

```
if ApplicationCode != Portfolio: return
book = Σ s.pm.eq  (fallback: Signals[0].Portfolio equity)   ; peak = max(peak, book)
dayPnL = book − bookAtSessionOpen
foreach s: s.AllowEntries = false                      // deny-all (Rank/Rotation pattern)
if killedToday or dayPnL ≤ −dailyLossFlatten·bookOpen: ForceCloseAll(); status; return
mult = ddMultiplier(book/peak − 1) × (dayPnL ≤ −dailyLossStop·bookOpen ? 0 : 1)
CommonPortfolioData["pm.mult"] = mult; ["pm.heat"] = heat; ["pm.dd"] = dd
clusters = mergeByCorrelation(pm.cluster seeds, ρ from pm.ret / pm.uRet)   // daily, cached
heat = Σ s.pm.riskOpen ; clusterHeat[c] = Σ over c
candidates = s with pm.state == Armed, not stale, not Paused, ver ok
order candidates by pm.score desc, then PT table order
// per-strategy gate, in this order (first that fires is the journalled reason):
//  1 book daily stop (mult 0)  2 permanent kill (§5.4.4)  3 session stop (4a)  4 weekly observe (4b)  5 weekly halve (4b)  6 caps
foreach s: sMult[s] = lossesToday[s] ≥ stratDailyLosses ? 0
                    : weekR[s] ≤ weeklyObserveR ? 0
                    : weekR[s] ≤ weeklyHalveR  ? weeklyHalveMult : 1
foreach c in candidates:
   m = mult × sMult[c]
   if m == 0 → deny (reason "daily stop" | "session stop" | "weekly observe"; journal PORT observe for Armed in observe)
   if openPositions ≥ maxPositions → deny ("maxPositions")       // BEFORE sizing, as coded (_PortMM L480)
   if sizing == CodeSize and m < 1 → deny (reason "throttle under CodeSize")      // EntryContracts −1 cannot scale; §5.4 4b
   Rw = (isNaN(c.pm.R$w) || c.pm.R$w < c.pm.R$) ? c.pm.R$ : c.pm.R$w   // R$w < R$ = bad publisher: use R$, warn once per strategy
   if sizing == CodeSize:
       if isNaN(c.pm.R$) || c.pm.R$ ≤ 0 || c.pm.qtyCode ≤ 0 → deny ("no pm.R$")   // unmeasurable risk cannot pass the 1%/heat/cluster checks (rev 1 L485 skips them and allows)
       n = c.pm.qtyCode                               // unchanged; size() returns −1 (_PortMath.Size L362)
   else:
       n = size(c, m)                                 // §5.3 RiskParity: floor(m × riskPerTrade × book / pm.R$) — STRUCTURAL R$ (P1 DECIDED)
       if n < minContracts → deny ("size < minContracts")
   r = n × Rw                                        // WIDENED risk: used for every check below (CodeSize: qtyCode × Rw)
   if r > maxTradeRisk·book → deny ("per-trade budget: widened risk > 1%")      // the §2.1 skip, both branches/profiles; n is NOT recomputed
   if heat + r > heatCap·book → deny ("heat cap")                                // widened r; never reduce n
   occupied = clusterCount[c] > 0 || clusterHeat[c] > 0     // seeded open risk also marks the cluster occupied
   if occupied && clusterHeat[c] + r > clusterCap·book → deny ("cluster cap")   // P2 DECIDED (c): only when the cluster already holds a position or seeded open risk; widened r; never reduce n
   c.EntryContracts = n (CodeSize: −1) ; c.AllowEntriesLong = side>0 ; c.AllowEntriesShort = side<0 ; c.AllowEntries = true
   heat += r ; clusterHeat[c] += r ; clusterCount[c] += 1 ; openPositions += 1   // clusterCount = open positions in the merged cluster + entries allowed earlier this step; clusterHeat[c] seeded from every publisher's pm.riskOpen (_PortMM L428–433)
CommonPortfolioData["pm.profile"] = profile ; ["pm.uncorr"] = uncorr ; ["pm.uncorr03"] = uncorr03   // §5.1a, advisory only
foreach s: s.Status = $"{state} n={EntryContracts} R={openR:0.0} mult={mult:0.00} cl={cluster}"
foreach s with kill rule hit: s.ForceClosePosition(); s.Paused = true (after flat, see §6)
```

The decided ddSteps and weekly-halve multipliers are **explicit risk throttles chosen by Ettienne**, applied uniformly before any stop is known; they are not "shrinking size to fit a stop", which stays forbidden — the floor and the caps can only deny.

Exits are never touched (`AllowExits*` left true). Positions already open are not resized in v1 (no pyramiding/partial de-risk except §5.4 step 3).

### 5.3 Sizing via `EntryContracts`

- **CodeSize:** `EntryContracts = −1` for every strategy → CS-4 `sizeForRisk` in the strategy decides; MM only gates. Use this first to prove parity with single-chart runs. **Must be set explicitly**: the default 0 means *Strategy Properties size*, which silently overrides code sizes in PT (live check L2).
- **RiskParity (default):** `n = floor(mult × riskPerTrade × book / pm.R$)` with `pm.R$` the **structural** stop (P1 DECIDED). The cost floor never changes `n`: the widened risk `n × pm.R$w` is only tested — > `maxTradeRisk` (1%) × book ⇒ skip (deny); heat/cluster caps are checked on the widened risk ⇒ deny. The MM never cuts `n`. Real risk per trade is therefore 0.5% at a structural stop and up to 1% when the floor widens it.
- **VolTarget:** each strategy gets an equal share of book vol, scaled by diversification: `n = floor(mult × (bookVolTarget/√252) × book / (k × pm.vol × IDM))`, `k` = number of active strategies, `IDM = 1/√(wᵀρw)` (equal weights, capped at 2.5) from §5.5. Then the 1% skip and the heat/cluster caps still apply via the widened risk `n × Rw` (§5.2; rev 1 said `pm.R$`).
- All results clamped by `Strategy Properties → Max shares/contracts per position` (MC built-in) and PT Money Management limits (MC still discards orders beyond them — keep PT "Max % capital at risk per position" ≥ `maxTradeRisk` (1%, §8 step 5; rev 1 said ≥ `riskPerTrade`) so MC does not drop entries the MM allowed).

### 5.4 Throttles and kill switches

0. **Total open-risk cap** (outer hard ceiling; Ettienne 2026-10-31): `_PortfolioRiskManager` `[Input] TotalRiskCapPct`, % of book, valid (0, 100], **default 10** (≈ 10 concurrent 1% trades). Invalid input logs `PORT Warning` and is rejected to the default (≤ 0, NaN, ∞) or clamped to 100 (> 100); stored as `PortMmConfig.totalRiskCap` (fraction, not a JSON key). It caps the combined **widened** risk: `total` = Σ `pm.riskOpen` of every publisher (the active = widened stop) + the widened risk of entries already allowed in the same step; a candidate's widened risk = `n × Rw` (Rw = `pm.R$w` when finite and ≥ `pm.R$`, else `pm.R$`; CodeSize: `qtyCode × Rw`). If `total + risk > totalRiskCap × book` the entry is **skipped, never size-cut** (exactly at the cap is allowed); an entry whose risk cannot be measured (any sizing, CodeSize included, without `pm.R$`) is skipped as `no R$`. **Base:** the cap is `totalRiskCap × MM book` (initialCapital + realized/open P&L tracked by the MM — the same base as `heatCap`), whereas the per-trade 1% budget in `sizeForRisk` uses `equityAvailable`, which may come from broker account equity; the two bases can differ. It applies **on top of** `heatCap` (1.5% starter / 4% scaled-book): both are checked, so the tighter binds — with the v6.1 defaults heat always binds first, and the cap matters only if a profile or override raises heat above it. **Scope: Portfolio Trader only.** Standalone charts sharing one account have no coordinator: the repo has no sanctioned cross-chart/cross-process shared state (only per-process statics such as the event calendar cache), so on charts the only limits are each strategy's own 1% per-trade skip (§5.1 step 5) — the total cap is **not enforced** there (known gap; run multi-strategy books under PT).
1. **Heat and cluster caps** (`AllowEntries`): Σ open risk + new risk ≤ `heatCap` × book; per merged cluster ≤ `clusterCap` × book, checked **only when that cluster already holds a position** (open, or allowed earlier in the same step; P2 DECIDED (c)) or carries seeded open risk (`clusterHeat[c] > 0` from `pm.riskOpen`) — a lone entry in an empty cluster is limited by `maxTradeRisk` (1%) and heat only. A breach **denies** the entry (journal reason); the size is never reduced to fit (both profiles; code change from rev 1, which reduced).
2. **Drawdown throttle** (PF-05): multiplier steps by drawdown from equity peak (closed + open); recover only after a new 20-day high or dd < half the step (hysteresis). Optional two-CUSUM on daily book returns sets `mult = 0` until reset (PF-05 second arm; v1.1).
3. **Daily loss stop** (A2-78): at −`dailyLossStop` deny all entries for the rest of the session; at −1.5× that, `ForceClosePosition()` on every strategy with a position. Session = `tradingDayKey` of the first strategy (ES RTH/ETH per its session template).
4. **Strategy kill (permanent)** (catalogue §2.3 / PF-05 bench): rolling R (`stratKillR` over `stratKillTrades`) or own `pm.state == Halted` ⇒ `ForceClosePosition()` then `Paused = true` once `PositionSide == 0`; journal; unpause only by restarting the portfolio. `Halted` keeps this permanent meaning and is **not** used for 4a/4b. −8R/20 can fire in the same week as −6R: weekly observe is applied first, then the kill; both are journalled.
4a. **Session stop after N losses** (PF-05; both profiles — P5 DECIDED): the MM counts per strategy the closed trades with R ≤ −`scratchR` (a loss; |R| ≤ 0.05 is a scratch, not counted) per that strategy's own `pm.day` (tradingDayKey). At `stratDailyLosses` (2) its entries are denied for the rest of that session; open positions keep their own exits (no force-close, no `Paused`). Resets when that strategy's `pm.day` changes.
    **Relation to CS-7 (F-TM, `_CustomSignal` L2683–2766):** CS-7 already offers the chart-side analogue (`DailyLossStopCount`, protected, default 0 = off; counts `Profit < 0`, no scratch band, per strategy instance, per its tradingDayKey, enforced in `entryAllowed()`). 4a stays in the MM because it covers every PT strategy with no per-strategy opt-in. A strategy may also set `DailyLossStopCount = 2`; the two may then disagree on a scratch trade, and **whichever fires first wins** (both only deny entries). CS-7 is not changed by rev 2.
4b. **Weekly R throttle** (PF-05; both profiles — P5 DECIDED): closed-trade R summed over the strategy's current trading week (ISO week of its `pm.day`, so Sunday-evening ETH belongs to Monday's week); scratches are included in the sum. ≤ `weeklyHalveR` (−3) ⇒ RiskParity/VolTarget size × `weeklyHalveMult` (0.5); ≤ `weeklyObserveR` (−6) ⇒ entries denied ("observation only": the strategy keeps calculating/publishing; each denied `Armed` journals `PORT observe`). No force-close, no `Paused`. **Reset (P6 DECIDED):** both the −3R halve and the −6R observe state clear at that strategy's first session of the next ISO trading week (`weekR` restarts at 0). **Under CodeSize** the MM cannot scale (`EntryContracts = −1`), so any multiplier < 1 (weekly halve or a ddSteps stage) **denies** with reason "throttle under CodeSize" (closes an existing rev 1 gap where ddSteps 0.5/0.25 were ignored under CodeSize).
    **R-stream limits** (shared by 4, 4a, 4b): a trade is closed only on position→0; rev 2 also closes it on a **sign flip** (reversal = close + open). Scale-outs are not separate trades. A trade whose open bar had no `pm.R$` is not recorded (journal `PORT kill-R missing`). R is in units of the **structural** 1R (`pm.R$` at open), as the strategy itself counts R. P&L is net of MC cost settings only.
    **Where it lives:** in the MM (`_PortMM`), not via `pm.state`, because `Halted` is permanent in code and `Paused` blocks exits (§6).
5. **Stale/invalid publisher** (bad `pm.ver`, NaN `pm.R$`, `pm.stamp` older than `staleBars`): deny entries for that strategy only.

**Skip logging rule (Ettienne 2026-10-31):** every skipped entry is logged at **Info**, never Debug, with the reason, the risk $ and the cap/budget $. MM: `PORT Info: skip <id>#<index> <reason>: risk $… total|heat|cluster $… cap $…` for maxPositions, heat cap, cluster cap, size < minContracts, daily stop / dd throttle and total risk cap, journaled once per (strategy, reason) while the entry stays pending, re-logged when the reason changes or the skip clears. Rejections before the main loop are entry attempts too and journal the same way: stale, no R$ (CodeSize included), currency not USD, paused/killed (not Armed, in position and no side are not entry attempts and stay silent). Heat/cluster room is never met by trimming size: if `q × R$` exceeds the room the entry is skipped (`heat cap`/`cluster cap`, risk $ and room $). The per-call 1% skip in `sizeForRisk` logs on every call (no dedupe). **Never-cut exceptions (Ettienne 2026-10-31):** the only places size is reduced rather than skipped are the two hard caps in `sizeForRisk` — `maxQty` and the notional cap — which keep reducing quantity by design; every other cap (1% budget, heat, cluster, total risk cap, minContracts) skips. Publisher: every `sizeForRisk - 0` trace (bad args, minStopTicks, zero size, notional cap, round lots, 1% budget skip with ticks, risk $ and budget $) is at Info. Pinned by tests in `tests/PortMMTests`. PR-B/PR-C deny reasons follow the same rule.

### 5.5 Correlation from published returns

- At each new session, for every pair with ≥ 40 overlapping days in `pm.ret` (fallback `pm.uRet` when a strategy has < 40 trading days), compute Pearson ρ over the last `rhoWin` sessions; also a **stressed ρ** = ρ on days where the book lost > 1σ (Keller B2 14.5).
- Union-find merge: pairs with max(ρ, stressed ρ) ≥ `rhoMerge` share one cluster budget; seeded clusters (`pm.cluster`) always merge (ES/MES/NQ treated as one even before data).
- Store the matrix in `CommonPortfolioData["pm.rho"]` (double[,]) for journals and for PF-03's hidden-beta monitor.
- Cost is O(k²·60) once a day; k ≤ 20.
- **Day key per strategy:** the MM keeps each strategy's published `pm.day` (already published, `_PortKeys.Day`; no `pm.ver` bump) for 4a/4b; the book day stays the first strategy's for the daily stop and ρ.
- **Uncorrelated live counts** (§5.1a), from the daily merge, computed lazily at the first merge: `uncorr` = distinct merged clusters (ρ ≥ `rhoMerge` 0.9, seeds always merged) with ≥ 1 live strategy; `uncorr03` = size of a **greedy** set of live strategies with pairwise max(ρ, stressed ρ) < 0.3 over the existing `pm.rho` matrix (no extra pass; greedy in PT table order, seeds treated as ρ = 1).

## 6. Semantics to respect (and to verify)

- `Paused` "allows/forbids order placement" — assume it blocks **exits and protective stops too**. Therefore: never pause a strategy with an open position; flatten first (`ForceClosePosition`), pause on a later step when flat. Broker-side stops (A2-78) remain the safety net live.
- `ForceClosePosition()` timing (this bar vs next bar) is undocumented; `PartialClosePosition(isNextBar, n)` exposes the flag, suggesting `ForceClosePosition` acts like a market exit at the next order send. Treat as next-bar open in backtests (live check L3).
- `EntryContracts > 0` applies to **every entry** order of the strategy, including scale-ins: strategies that pyramid must use CodeSize mode or publish `pm.qtyCode` per leg (v1: pyramiding strategies run with `EntryContracts = −1` and are only gated).
- `RiskCapital` is MC's margin/max-loss capital, not our stop risk — journal both; alert if they disagree by > 3×.
- Changing `AllowEntries` does not cancel an already-working limit/stop entry from a previous bar in real time unless the strategy re-generates orders each bar (MC cancels orders not re-sent) — our strategies re-send every bar, so deny-all works.

## 7. Live trading via IBKR (dxFeed data, `@ES` continuous unadjusted)

- Data: dxFeed continuous `@ES`/`@NQ` (not back-adjusted) in PT; roll gaps removed in returns by M-DAILY roll-gap logic, so `pm.ret`/`pm.uRet`/ρ are not polluted by roll jumps. The VPF-4 roll detector also resets `pm.R$`-dependent state (pending entries are cancelled on a roll day).
- Broker: PT auto-trading with the IBKR broker profile. **Symbol Mapping** must map `@ES` → the front IB contract (`ES` + expiry, `GLOBEX`) and be **updated at each roll** (Thursday before the 3rd Friday of Mar/Jun/Sep/Dec, CME equity roll date); otherwise orders go to the expired or wrong month. Checklist item per roll: update mapping, verify PT position = IB position for every strategy (no MC "sync" assumptions), then restart the portfolio.
- `EntryContracts` sizing is in contracts of the traded symbol; MES/MNQ strategies are separate PT strategies with their own BPV (the MM never converts ES↔MES in v1).

## 8. Build steps for @Coder

1. `_PortMM.Function.CS`: `_PortKeys`, `EPortState`, `_PortReturns` (immutable ring buffer), `PortMmConfig` (+ JSON loader), `_PortMath` (ρ, stressed ρ, union-find clusters, IDM, dd multiplier, sizing formulas). Add to `PLStudies.csproj`.
2. `_CustomSignal`: `PortfolioPublish`, `PortfolioFamily`, `PortfolioRegime`, `PortfolioScore` virtuals; `publishPortfolio()` at the end of `CalcBar`; hook `pm.R$`/`pm.qtyCode`/`pm.riskNew` inside the existing entry path where `sizeForRisk` is called; `pm.state` from position + pending entry. No `[Input]`.
3. `_PortfolioRiskManager.Strategy.CS` per §5.
4. Off-MC tests `tests/PortMMTests` (actual folder): sizing, caps, clusters, dd hysteresis, daily stop, stale handling. Repin `Program.cs` L29 and the heat 4% / cluster 2% / maxPositions 6 / −8R tests to `ForProfile(ScaledBook)`. Rev 2 adds:
   - T-P1 `ForProfile(Starter)` and `ForProfile(ScaledBook)` pass `Validate()`; values exactly as §5.1; `new PortMmConfig()` equals starter.
   - T-P2 starter heat: book 1M, heatCap 0.015 ⇒ 15 000; two entries in different clusters with widened risk 5 000 each allowed (in the same cluster the second would total 10 000 > 7 500 and be denied "cluster cap"); a third denied by maxPositions = 2 even with heat room; two entries in different clusters whose widened risk is 9 000 each ⇒ second denied "heat cap" (18 000 > 15 000), first keeps its n. Runs with the starter clusterCap 0.0075: under P2 (c) each entry is alone in its cluster, so the cluster cap is not checked (9 000 ≤ 10 000 per-trade; heat 9 000 ≤ 15 000 for the first), and the heat cap is the reason for the second.
   - T-P3 starter cluster cap: second EQIDX (ES + NQ) entry **denied** ("cluster cap"), not sized to the remainder; ES + CL both allowed. Same deny for a heat-cap breach in scaled-book.
   - T-P4 starter ddSteps stages and hysteresis (3/6/10%).
   - T-P5 session stop (run in starter and in ScaledBook, P5): 2 losing closes in one session ⇒ that strategy denied, others not; no force-close; allowed again when its `pm.day` changes; a winner between losses does not reset the count.
   - T-P6 weekly: −3.0R ⇒ size halved (floor; < minContracts ⇒ deny); −6R ⇒ denied with `PORT observe`; −2.9R no effect. Reset (P6): after −6R (and after −3R) the first session of the next ISO week ⇒ full size and entries allowed; a Sunday-evening ETH session counts as Monday's week (no early reset); halve and observe in the same strategy both clear. Run in starter and in ScaledBook (P5: same results).
   - T-P7 interplay: book mult 0.5 × weekly 0.5 = 0.25; daily stop overrides; gate order reason as §5.2.
   - T-P8 `uncorr`: ES + CL + GC ⇒ 3; ES + NQ + CL ⇒ 2; CL/GC ρ ≥ 0.9 ⇒ merged ⇒ 2; paused / observe / non-live strategies excluded.
   - T-P9 no automatic switch and the both-counts rule (P9): `uncorr` 2 → 3 changes no profile or limit; one advisory journal line per day; the line says "eligible" only when `uncorr` ≥ 3 **and** `uncorr03` ≥ 3 (uncorr 3 / uncorr03 2 ⇒ "not eligible"; uncorr 2 / uncorr03 3 ⇒ "not eligible").
   - T-P10 JSON: `"profile": "ScaledBook"` replaces the base, then fields override; unknown profile ⇒ warning, subclass base kept; starter file with clusterCap 0.02 rejected ⇒ `ForProfile(Profile)`.
   - T-P11 reversal (+2 → −2) records a closed trade.
   - T-P12 per-strategy day key: CL and ES with different `pm.day` at the same step; ES's session stop does not reset on CL's new day and vice versa.
   - T-P13 CodeSize + weekly halve (and + ddSteps 0.5) ⇒ deny "throttle under CodeSize".
   - T-P14 scratch boundary: R = −0.05 not a loss; R = −0.0501 a loss; both added to weekly R.
   - T-P15 `uncorr03` greedy count on a fixed ρ matrix.
   - T-P16 floor interplay (P1 DECIDED): book 1M, `pm.R$` 250 (structural), `pm.R$w` 350 (14-tick floor) ⇒ n = floor(5 000/250) = 20 (not 14), r = 7 000 ⇒ allowed (7 000 is also ≤ cluster 7 500 and heat 15 000, so this tests the 1% rule only). Same with `pm.R$w` 600 ⇒ r = 12 000 > 10 000 ⇒ denied "per-trade budget", never resized. `pm.R$w` absent or NaN ⇒ Rw = `pm.R$`. `pm.R$w` < `pm.R$` (bad publisher) ⇒ Rw = `pm.R$` + one warning per strategy (not per bar).
   - T-P17 a starter subclass config that fails Validate falls back to `ForProfile(Starter)` (not scaled-book) and the RUN line carries `config=fallback`.
   - T-P18 caps on widened risk: (i) cluster only — two EQIDX candidates with structural risk 5 000 but widened 7 000 each under clusterCap 0.0075: the first enters an empty cluster (cap not checked); the second finds the cluster holding a position, 14 000 > 7 500 ⇒ denied "cluster cap" (heat 14 000 ≤ 15 000, so the cluster cap is the reason); neither n changed. (ii) heat only — two candidates in **different** clusters, widened 8 000 each (each ≤ 10 000 per-trade): 8 000 + 8 000 = 16 000 > heat 15 000 ⇒ second denied "heat cap", first keeps its n. Runs with the starter clusterCap 0.0075: under P2 (c) each is alone in its cluster, so 8 000 > 7 500 does not deny. (iii) CodeSize — `qtyCode` 20, `pm.R$` 250, `pm.R$w` 600 ⇒ r = 12 000 > 10 000 ⇒ denied "per-trade budget", `entryContracts` never changed; with `pm.R$w` 350 the heat/cluster sums use 7 000 (Rw, not `rDollar`). (iv) CodeSize, no measurable risk — `pm.R$` absent/NaN, `pm.R$` 0, and `qtyCode` 0 (each with heat/cluster room) ⇒ denied "no pm.R$" (rev 1 allows with no checks, `_PortMM` L485); RiskParity with NaN `pm.R$` ⇒ denied "no R$" by the pre-candidate filter (`_PortMM` L461, before sizing; confirms that branch is already safe).
   - T-P19 `_CustomSignal.portfolioArm` with `widenedStop` publishes `pm.R$` structural, `pm.R$w` widened, `pm.riskNew = pm.R$w × qtyCode`; without it (or with a widened stop closer than structural) `pm.R$w` = NaN, overwriting any previous value (arm once with, then without ⇒ NaN, not the stale value); after `startPortfolio` `pm.R$w` = NaN; `sizeForRisk(…, widenedStop)` sizes `qty` on the structural stop (same qty as without `widenedStop`).
   - T-P20 P2 (c) cluster cap only on an occupied cluster (starter, book 1M: per-trade 10 000, heat 15 000, cluster 7 500): (i) lone ES, empty EQIDX cluster, structural 5 000, widened 9 000 (0.9%) ⇒ allowed, n unchanged (9 000 > 7 500 but the cluster is empty); boundaries widened 7 500 (0.75%) and 10 000 (1%) ⇒ allowed, 10 001 ⇒ denied "per-trade budget" (not "cluster cap"). (ii) with that ES open (9 000), NQ (same merged cluster) widened 5 000 ⇒ heat 14 000 ≤ 15 000 but cluster 14 000 > 7 500 ⇒ denied "cluster cap", n not reduced. (iii) same step: ES and NQ both candidates, empty cluster, widened 9 000 and 5 000 ⇒ ES allowed, NQ denied "cluster cap" (an entry allowed earlier in the step counts as holding). (iv) ES open 9 000 + CL widened 5 000 ⇒ allowed (different cluster, heat 14 000). (v) after ES closes (cluster empty again) NQ widened 9 000 ⇒ allowed. (vi) ScaledBook: same rule — lone entry vs clusterCap 0.02 not checked; second same-cluster entry checked. (vii) v6.1 open risk while flat: a test publisher in EQIDX writes `pm.pos` 0 and `pm.riskOpen` 5 000 (clCount 0, clHeat 5 000); NQ widened 5 000 ⇒ heat 10 000 ≤ 15 000 but the cluster is occupied by seeded risk and 10 000 > 7 500 ⇒ denied "cluster cap", n not reduced; with `pm.riskOpen` 0 the same NQ entry is allowed (cluster empty).
   - T-P21 1% skip in `sizeForRisk` (chart / non-portfolio runs and Data2 calls; with an MM the MM checks): equity 1M, sizing 0.5% on structural stop ⇒ qty 20; widened R$ 350 ⇒ 7 000 ≤ 10 000 ⇒ qty 20 returned; widened R$ 600 ⇒ 12 000 > 10 000 ⇒ returns 0 (not 16) and does not arm; no widenedStop ⇒ unchanged behaviour. Boundary: widened R$ 500 ⇒ exactly 10 000 = 1% ⇒ allowed (qty 20; the test is `>`, not `≥`). `widenedStop` closer than the structural stop ⇒ ignored: no skip, qty 20, `pm.R$w` = NaN. Final qty: the check uses the qty after maxQty, notional cap and roundLots (`_CustomSignal` L2836–2843), e.g. maxQty 15 with widened R$ 600 ⇒ 9 000 ⇒ qty 15 allowed. Data2 call (`bars != Bars`, also with `m_portOn`) with widened 12 000 ⇒ returns 0 and does not arm (Data2 never arms, `_CustomSignal` L2845, so the MM never sees it and the skip must stay). Portfolio mode (R1, `m_portOn`): widened 12 000 ⇒ returns 20 and arms with `pm.R$w` 600; the MM then denies "per-trade budget".
   - T-P10 addition: JSON `"maxTradeRisk": 0.02` (> 0.01) ⇒ config rejected ⇒ fallback to `ForProfile(Profile)` with `config=fallback` (same path as T-P17).
5. **Portfolio Trader setup:** set MC's "Max % capital at risk per position" (PT Portfolio Settings) to **≥ 1%** (≥ `maxTradeRisk`). Otherwise MC silently drops exactly the orders whose widened risk is between 0.5% and 1%, which P1 allows. Check it in the PT live check before the first rev 2 run.
6. **Live check before PR-B** (after PR-B0, before any MM code reads `pm.R$w`; add to the live-check sheet): (a) one MultiCharts **chart** run of a strategy that passes `widenedStop` to `sizeForRisk`, showing at least one Info `sizeForRisk - 0: … trade skipped` line in the trace log (widened risk > 1% of equity); (b) one Portfolio Trader run where `pm.R$w` is visible for an armed entry (`_TestPortArm` / MM journal), finite when widened and NaN otherwise.
7. MC acceptance (§9) before any PF idea is read.

## 9. Still needs a live check in MC (Probe P5b)

| # | Question | How |
|---|---|---|
| L1 | MM signal runs after all strategies and before order sending in the **same** step; `AllowEntries`/`EntryContracts` take effect on that step's orders | 2 strategies on ES/NQ that always arm; MM allows only index 0 on odd bars; check fills |
| L2 | Default `EntryContracts` = 0 really overrides code size when an MM signal is present (and when none is) | run CodeSize vs unset; compare trade sizes |
| L3 | `ForceClosePosition()` fill timing (this bar close vs next open) and whether it works while `AllowExits=false` | trace fills |
| L4 | `Paused = true` blocks exits/stops? Does a paused strategy still calculate and publish? | pause with open position in backtest (not live) |
| L5 | How the .NET MM signal is designated in PT (separate MM slot; any attribute/base needed beyond `PortfolioSignalObject`)? Does it need a data series/symbol? | try applying `_PortfolioRiskManager` as the PT MM signal |
| L6 | `IPortfolioData` object values: same process/AppDomain for all strategies (reference sharing works) in backtest **and** auto-trading; persistence across PT restarts (assume none) | `_MarketBreadth` already relies on it in backtest; test in real-time PT |
| L7 | Mixed resolutions: does the MM run on every step of any strategy? stamp check | ES 1m + CL 5m portfolio |
| L8 | `PortfolioCurrency` and `StrategyCurrencyCode` values for dxFeed CME symbols (USD expected) | print once |
| L9 | IBKR symbol-mapping roll in PT live/paper | paper account over one roll |

## 10. Changes vs `missing-functionality-specs.md` §8 (earlier assumptions)

- The earlier text assumed PowerLanguage `pmm_set_my_named_num`/`pmms_*` named numbers. **.NET uses `IPortfolioData` object indexers** — any object (arrays, ring buffers) can be shared; no `pmms_calc_*` helpers exist and must be coded.
- **`_CustomSignal` already derives from `PortfolioSignalObject`**, so every strategy can publish (and could even read other strategies) without a new base class.
- Earlier: "PMM runs on the first instrument". The .NET API exposes no such notion; the MM is just a `PortfolioSignalObject` set as the MM signal (L5).
- Earlier sizing relied on each strategy's `sizeForRisk` plus `Portfolio.SetMaxPotentialLossPerContract`. Now sizing is centralised through `EntryContracts`; **default 0 = Strategy Properties size** means code sizes are ignored unless the MM sets −1 (L2).
- `Paused` likely also blocks exits ⇒ kill switch = flatten first, pause later.
- IOG is unavailable in Portfolio Trader; Bar Magnifier too.
- Exits are never gated by the MM (not stated before).

## 11. Revision log
- **rev 1** (2026-10-21): initial spec; coded in PRs #14/#17/#22.
- **rev 2** (DRAFT 2026-10-24, v2 2026-10-29 after Analyst 2 review): two risk profiles — starter (default until ≥ 3 uncorrelated live strategies; catalogue §6/PF-04/05/06: heat 1.5%, 2 positions, 0.5% per trade, 12% vol) and scaled-book (rev 1 numbers). Profile = protected property / subclass / JSON `profile`; no `[Input]`; manual switch only. Caps deny instead of reducing size (both profiles). P1 DECIDED (Ettienne Hugo, 2026-10-29): size from the structural stop (`pm.R$`, 0.5%); new optional `pm.R$w` widened stop; skip if widened risk > 1% (`maxTradeRisk`); heat/cluster caps on widened risk, deny. New §5.4 items 4a (session stop after 2 losses) and 4b (weekly −3R halve / −6R observe) in the MM, per-strategy `pm.day`; CodeSize denies when a multiplier < 1. §0 corrected (no ConfigPath input). §5.1 lists every coded field. rev 2 v4: structural-stop sizing kept in `sizeForRisk`; `pm.R$w` always written (NaN = none); Rw clamp; CodeSize uses Rw; 1% skip in `sizeForRisk`; `pm.ver` stays 1 (same-build deploy). rev 2 v5: `sizeForRisk` skips only off-portfolio (with an MM it arms and the MM decides); CodeSize denies "no pm.R$" when risk is unmeasurable. rev 2 v6: P2 DECIDED (c) — cluster cap checked only when the cluster already holds a position (v6.1: or seeded open risk); P5 DECIDED — 4a/4b in both profiles; P6 DECIDED — −6R observe clears weekly like −3R; P9 DECIDED — scaled-book eligible only when `uncorr` ≥ 3 and `uncorr03` ≥ 3 (all Ettienne Hugo, 2026-10-29). Tests T-P1…T-P21. Decisions in specs/m-port-risk-profiles-decisions.md.
- **rev 2 v6.1** (2026-10-29): cluster occupied also by seeded open risk (`clusterHeat[c] > 0`), T-P20 (vii). **Approved for implementation by Ettienne Hugo, 2026-10-31**; `weeklyObserveReset` is `Weekly` only (the `Manual` option is not adopted). Two consistency edits in §5.3 follow from P1 (no new decision): VolTarget — the 1% skip and heat/cluster caps apply via the widened risk `n × Rw` (rev 1 said `pm.R$`); PT "Max % capital at risk per position" ≥ `maxTradeRisk` 1% (rev 1 said ≥ `riskPerTrade`). §8 gains the PT setup step (5) and the pre-PR-B live check (6). Decision sheet committed as `specs/m-port-risk-profiles-decisions.md`. Applied to this file 2026-10-31 with every code line ref re-verified on origin/main @ 3f9e9be. Implementation PRs: PR-B0 (publisher `pm.R$w`, `sizeForRisk` 1% skip), PR-B (profiles, deny-not-reduce), PR-C (session/weekly throttles), PR-D (advisory counts).
- **rev 2.1** (2026-10-31, Ettienne): §5.4 item 0 total open-risk cap (`TotalRiskCapPct` [Input], default 10%, widened risk, skip never cut, PT only — standalone gap documented); §5.4 skip logging rule (Info with reason, risk $, cap $).
- **rev 2.2** (2026-10-31, Analyst 2 review of #67): heat/cluster room trim replaced by a skip (size never cut); pre-loop rejections (stale, no R$, currency, paused/killed) journaled at Info; CodeSize without `pm.R$` skipped as `no R$`; dedupe re-logs on reason change or clear; cap base = MM book vs 1% budget base = equityAvailable (may be broker equity). Decided: `sizeForRisk` maxQty/notional stay hard caps that reduce size — the only never-cut exceptions; heat/cluster stay on structural R$ until PR-B.
