---
title: "M-PORT rev 2 DRAFT — risk profiles (starter / scaled-book) and per-strategy kill rules"
date: 2026-10-29 (draft v6.1; v6 2026-10-29; v5 2026-10-29; v4 2026-10-29; v3 2026-10-29; v2 2026-10-29; v1 2026-10-24)
status: "DRAFT v6.1 for review — Analyst 2 review folded in (v2); P1 DECIDED (Ettienne Hugo, 2026-10-29) applied (v3); Analyst 2 v3 review required edits applied (v4); Analyst 2 v4 review R1–R3 applied (v5); P2 (c), P5, P6, P9 DECIDED (Ettienne Hugo, 2026-10-29) applied (v6); Analyst 2 v6 review R1 and the open-risk occupancy note applied (v6.1) — no open decisions; not applied to m-port-spec.md, not coded"
base: "specs/m-port-spec.md rev 1; trading-idea-catalogue.md (/workspace, the copy the specs cite) §6.1 (L4176–4178), PF-04/PF-05 (L1942–1954), §2.1 floor rule (L110); MultiCharts.Net origin/main @ 800bce7 (2026-10-25 08:08 EDT, merge of PR #42), fresh clone /tmp/mcn-portcheck. Since v1's b2685dd only _CustomSignal.Strategy.CS changed (+27 lines, M-DVP D7 A2-33 hook); _PortMM, _PortfolioRiskManager, tests/PortMMTests and PLStudies.csproj are byte-identical, so _PortMM / _PortfolioRiskManager line refs are unchanged and _CustomSignal refs after L459 moved by +27. v4: every line ref re-checked 2026-10-29 against a fresh clone of origin/main (/tmp/mcnet), still @ 800bce7. v5: every line ref re-checked 2026-10-29 against a fresh clone of origin/main (/tmp/mcn-v5), still @ 800bce7. v6: re-checked 2026-10-29 against a fresh clone of origin/main (/tmp/mcn-v6) @ 1dc1abb (2026-10-29 22:11 EDT, merge of PR #45); 800bce7..1dc1abb touches only Gate0 files, so _PortMM, _PortfolioRiskManager, _CustomSignal, PLStudies.csproj and tests/PortMMTests are byte-identical and every line ref below is unchanged (code-state refs '@ 800bce7' therefore also hold @ 1dc1abb). v6.1: re-checked 2026-10-29 against a fresh clone of origin/main (/tmp/mcn-v61), still @ 1dc1abb; _PortMM L428–433 seeding and _CustomSignal L1593/L1618–1631 (`pm.riskOpen` publish) read."
decisions: "specs/m-port-risk-profiles-decisions.md (P1, P2, P5, P6, P9 — all DECIDED)"
---

# M-PORT rev 2 draft: risk profiles

**Decision (Ettienne, relayed):** until **3 or more uncorrelated strategies are live**, the catalogue §6 numbers are the live defaults ("starter" profile). The current rev 1 numbers become the "scaled-book" profile. All decisions (P1, P2, P5, P6, P9 by Ettienne; P3, P4, P7, P8 by the analysts) are taken — see §G and the decision sheet. §H lists the remaining live-check items.

**User rules this draft must keep:** per-trade risk ≤ 1% of equity; position size is **never shrunk** to fit a wider stop or a cap (deny instead; catalogue §2.1 L110); protected config, no `[Input]`; engines allocate only when used; PriceAction library unchanged; separate small PRs.

## A. What origin/main implements today (@ 800bce7)

| Item | Code | Note |
|---|---|---|
| Config class | `PortMmConfig` (sealed, public fields) in `_PortMM.Function.CS` L71–129 | |
| Defaults | `sizing=RiskParity, bookVolTarget=0.10, riskPerTrade=0.005, heatCap=0.04, clusterCap=0.02, rhoMerge=0.9, rhoWin=60, rhoMinDays=40, maxPositions=6, ddSteps={0.05→0.5, 0.10→0.25, 0.15→0}, ddRecoverHighDays=20, dailyLossStop=0.02, dailyLossFlatten=0.03, stratKillR=-8, stratKillTrades=20, staleBars=3, minContracts=1, idmCap=2.5` | `rhoMinDays`, `ddRecoverHighDays`, `dailyLossFlatten`, `stratKillTrades`, `idmCap` (L79–90) are absent from spec §5.1 rev 1 (dailyLossFlatten and "20 trades" appear only in prose) |
| Validation | `Validate()` requires `0 < clusterCap <= heatCap` (L98) | A starter config with clusterCap 0.02 > heatCap 0.015 is rejected |
| Invalid-config fallback | **Falls back with an Output warning only.** `_PortfolioRiskManager` L38 writes `PORT Warning: code Config invalid (...), using built-in defaults` and uses `new PortMmConfig()`; the loader (`_PortMM` L209–210) warns `rejected (...), using code defaults` and returns `defaults`. No Status text, no RUN flag | Easy to miss. Today `new PortMmConfig()` is scaled-book, so a broken starter subclass would silently-in-practice run **scaled-book**. Rev 2: fall back to `ForProfile(Profile)` and flag `config=fallback` in the RUN line and Status |
| Config selection | `protected virtual PortMmConfig Config => new PortMmConfig()` (L25), `protected virtual string ConfigPath => null` (L32). **No `[Input]`** (L14) | Spec §0 rev 1 wrongly says the MM has a `ConfigPath` input |
| Book multiplier | L421–422: `mult = ddSteps stage mult; if (m_dayStopped) mult = 0`. One book-wide multiplier; no per-strategy multiplier | |
| Cap handling | **CodeSize** path (L483–493): heat/cluster breach ⇒ **deny**. **RiskParity/VolTarget** path (L494–502; trim at L496–499): `room = min(heat room, cluster room)`, `if (q > qMax) q = qMax` ⇒ **reduces size** | The reduction breaks the never-shrink rule; rev 2 makes it deny (§5.2) |
| pm.* risk keys | `_PortKeys` L24: `RDollar = "pm.R$"`, `QtyCode`, `RiskNew`, `RiskOpen`; **no key for a widened stop**. `_CustomSignal.portfolioArm(side, entry, stop, qtyCode)` L1573–1585 takes **one** stop: `pm.R$ = |entry − stop| × BPV`, `pm.riskNew = pm.R$ × qtyCode`; `sizeForRisk` (L2779–2811) calls it for Data1 at L2809 with the **same** stop it sized on. No cost-floor code in `_CustomSignal` (floor is per-example; CostConfig IP-3.2 spec only). `pm.riskOpen` uses the active (order) stop | P1 needs one new optional key (§C.3) |
| CodeSize and multipliers | `_PortMath.Size` L362: `CodeSize: return mult > 0 ? -1 : 0` | Under CodeSize any 0 < mult < 1 (ddSteps 0.5/0.25, weekly halve) is **ignored**: the strategy trades its own size. Must be specified (§5.4 4b) |
| Strategy kill | `updateKills` L549–568: closed-trade R = ΔPnL / (R$ × \|pos\| at open), rolling last `stratKillTrades`; sum ≤ `stratKillR` ⇒ `killed` | R-stream limits: a trade is recorded only on a position→0 transition, so a **reversal** (+n→−n) and **scale-outs** are not closes; R needs `pm.R$` published on the open bar (else `riskAtOpen` NaN ⇒ trade not recorded) |
| P&L source | `v.pnl = pm.pnl` (`_PortfolioRiskManager` L125) = NetProfit + OpenProfit | Net of MC commission/slippage **only as far as MC cost settings are right** (Gate-0 $29.50 vs $16.98 issue) |
| `pm.state == Halted` | L440–445: permanent kill (`k.killed` never cleared; flatten, then pause). `_CustomSignal.PortfolioHalted` L1524 @800bce7 (L1497 @b2685dd) defaults false | A session-only stop cannot use `Halted` |
| Session day | `_PortfolioRiskManager` L60–69/L129: **one** `day` for the book = the first strategy's published `pm.day` (`_PortKeys.Day`, `_PortMM` L25), else the first strategy's bar date | `pm.day` is already published per strategy (`_CustomSignal` L1635 = tradingDayKey); only the MM discards all but the first |
| F-TM daily-loss counter | **Exists** as CS-7 in `_CustomSignal.Strategy.CS` L2647–2730 (@800bce7; L2619–2702 @b2685dd): protected `DailyLossStopCount` (L2658), `DailyLossStopR` (L2660), `RiskUnitDollars`, `LosersToday` (L2677), `TradesToday`, `NetProfitToday`; counts closed trades with `t.Profit < 0` (L2703) per `tradingDayKey`; enforced in `entryAllowed()` (L2718, L2729–2730). Defaults 0 = off. Not `[Input]`s | v1 said "not found" — **wrong**, corrected (grep missed `...LossStopCount`/`m_tmLosers`) |
| Live vs paper | No notion in code; `Environment` gives the application code, not per-strategy broker/sim state | Needs `LiveIds` (§5.1a) |
| Tests | `tests/PortMMTests/Program.cs`; L29 asserts `heatCap == 0.04 && clusterCap == 0.02 ...` | Spec §8 says `tests/PortMM`. Changing defaults **breaks L29** and the cap tests (heat 4%, cluster 2%, maxPositions 6, −8R) — repin to `ForProfile(ScaledBook)` |
| `_TestPortMM` / `_TestPortArm` | Read no `PortMmConfig` field | No change needed |
| csproj | Explicit includes, `PLStudies.csproj` L547 `_PortMM.Function.CS`, L548 `_PortfolioRiskManager.Strategy.CS` | Add `_PortfolioRiskManager_ScaledBook.Strategy.CS` after L548 |

## B. Catalogue §6 rules, exact meaning

Source: §6.1 items 3, 5 (L4176–4178) and PF-04 / PF-05 (L1942–1954).
- PF-04: per-trade risk = min(¼ Kelly on deflated win rate, **0.5%**); open risk **≤ 1.5%**; **≤ 2 cards live**. §6.1/PF-06: vol target **12%**.
- PF-05: "daily stop after 2 losses; **−3 R in a week → halve size; −6 R → observation only**; two CUSUMs (k = 0.5, h = 5); bench any spec at 0 R after 40 live observations."
- −3R = new entries at half size (still trading); −6R = no live entries, signals still evaluated and journalled ("observation").
- **Reset: not stated** (L1952). Decision P6.
- "Daily stop after 2 losses": per strategy, rest of that session (relayed decision). Loss definition P7 (§G).
- §2.1 (L110): stop = max(structural, cost floor); the trade is **skipped** if the widened stop breaches ≤ 1% of equity; **size is never shrunk** to fit a wider stop; "PF-04's 0.5% is not the default for this rule".

## C. Edit list for `specs/m-port-spec.md` (diff style)

### C.1 Front matter / title
```diff
-title: "M-PORT — ... (implementation spec, rev 1)"
-status: "API confirmed (Probe P5 resolved); spec ready; not yet coded"
+title: "M-PORT — ... (implementation spec, rev 2)"
+status: "rev 1 coded (PRs #14/#17/#22); rev 2 adds risk profiles, deny-not-reduce caps and per-strategy session/weekly throttles; not yet coded"
-# M-PORT implementation spec (rev 1)
+# M-PORT implementation spec (rev 2)
```

### C.2 §0 bullet "No new [Input]s"
```diff
-... The MM signal gets **one** optional input (`ConfigPath`, empty = defaults) only because PT-applied MM signals have no subclass to override properties in; everything else is a `PortMmConfig` with defaults in code (see §5.1). If even that is unwanted, ship subclasses (`_PortMM_Default`, `_PortMM_Conservative`) that override `Config`.
+... The MM signal has **no `[Input]`** (as coded): the risk profile is the protected `Profile` property; `Config`, `ConfigPath` and `LiveIds` are protected virtuals. One subclass per profile (`_PortfolioRiskManager` = starter, `_PortfolioRiskManager_ScaledBook` = scaled-book). See §5.1a.
```

### C.3 §3 risk keys — structural and widened R$ (P1 DECIDED)
Schema on main (@800bce7) has only `pm.R$`. Decision: **keep `pm.R$` = structural stop** (its current meaning, the same number CS-4 `sizeForRisk` uses, no change for existing publishers) and **add one optional key `pm.R$w`** = widened stop. Absent / NaN `pm.R$w` ⇒ the MM uses `pm.R$` (strategy has no floor, widened = structural). Additive optional key, so **`pm.ver` stays 1 (Analyst 2 agreed, v3 review §v3.3)**: the gate is `_PortKeys.Version = 1` (`_PortMM` L21), written once in `startPortfolio` (`_CustomSignal` L1558) and checked in `_PortfolioRiskManager.view()` L112–113 (else reason "no pm.ver", `_PortMM` L454); readers pull keys by name (`get()`/`num()`, L141–146) and no reader enumerates `PortfolioData`, so an unknown key is ignored (precedent: optional `pm.regimeP`, `_PortMM` L26–28). No MM code reads `pm.riskNew` (only the publisher writes it, `_CustomSignal` L1564/L1582/L1636; the loop recomputes risk from `rDollar`), so its change of meaning is safe. **Caveat: the MM and all publishers must ship from the same build** (same DLL) — an old MM with a new publisher sizes correctly on structural `pm.R$` but has no 1% skip, no widened caps and still trims.
```diff
-| `pm.R$` | double | when an entry is pending or a position is open | $ of 1R per contract = \|entry − stop\| × BPV (same numbers CS-4 `sizeForRisk` uses) |
-| `pm.qtyCode` | int | with `pm.R$` | contracts the code would trade (CS-4 result) |
-| `pm.riskNew` | double | each bar | $ risk of the pending entry = `pm.R$ × qtyCode`, 0 if none |
+| `pm.R$` | double | when an entry is pending or a position is open | $ of 1R per contract at the **structural** stop = \|entry − structural stop\| × BPV (same number CS-4 `sizeForRisk` uses). **Sizing input** for RiskParity and the unit of R for kill / session / weekly R |
+| `pm.R$w` | double | with `pm.R$` (optional, new) | $ per contract at the **widened** stop = \|entry − max-distance(structural, cost floor e.g. 14 ticks ES)\| × BPV, i.e. the stop the order will carry. ≥ `pm.R$`. Absent/NaN ⇒ = `pm.R$`. **Risk input** for the 1% skip and the heat/cluster caps |
+| `pm.qtyCode` | int | with `pm.R$` | contracts the code would trade (CS-4, sized on the structural stop; never reduced by the floor) |
+| `pm.riskNew` | double | each bar | $ risk of the pending entry at the **widened** stop = `pm.R$w × qtyCode`, 0 if none |
```
`pm.riskOpen` is unchanged (it already uses the active stop, which is the widened one once the order is live). Code: `portfolioArm(side, entry, stop, qtyCode, double widenedStop = double.NaN)` (optional parameter, existing callers unchanged). **`sizeForRisk` keeps sizing `qty` and publishing `pm.R$` on the structural stop** (`stopLossBasedPositionSize(bars, entry, stop, …)` L2798 and `portfolioArm(…, stop, qty)` L2809). A strategy with a floor passes the floored stop **only** as the new `widenedStop` argument — a new optional `sizeForRisk(…, double widenedStop = double.NaN)` parameter forwarded to `portfolioArm`, or a direct `portfolioArm` call; it never goes into the `stop` argument (that would shrink `qtyCode` and make `pm.R$` widened, the v2 error). **No stale key:** `MyPortfolioData` keeps values between bars and the per-bar reset (L1636) clears only `riskNew`/`side`, so `portfolioArm` **always** writes `pm.R$w` (the widened R$, or NaN when `widenedStop` is not given or is closer than the structural stop) and `startPortfolio` (L1550–1567) initialises `pm.R$w` = NaN. NaN reads as `num()` NaN ⇒ Rw = `pm.R$`. Until CostConfig lands, only strategies with per-example floor code publish a finite `pm.R$w`.

### C.4 §5.1 — replace the table with section D below; add §5.1a.

### C.5 §5.2 algorithm
```diff
 mult = ddMultiplier(book/peak − 1) × (dayPnL ≤ −dailyLossStop·bookOpen ? 0 : 1)
+// per-strategy gate, in this order (first that fires is the journalled reason):
+//  1 book daily stop (mult 0)  2 permanent kill (§5.4.4)  3 session stop (4a)  4 weekly observe (4b)  5 weekly halve (4b)  6 caps
+foreach s: sMult[s] = lossesToday[s] ≥ stratDailyLosses ? 0
+                    : weekR[s] ≤ weeklyObserveR ? 0
+                    : weekR[s] ≤ weeklyHalveR  ? weeklyHalveMult : 1
 ...
-   n = size(c, mult)
-   r = n × c.pm.R$
-   if n < minContracts → skip
-   if heat + r > heatCap·book → try n' = floor((heatCap·book − heat)/R$) else skip
-   if clusterHeat + r > clusterCap·book → same reduction
-   if openPositions ≥ maxPositions → skip
+   m = mult × sMult[c]
+   if m == 0 → deny (reason "daily stop" | "session stop" | "weekly observe"; journal PORT observe for Armed in observe)
+   if openPositions ≥ maxPositions → deny ("maxPositions")       // BEFORE sizing, as coded (_PortMM L480)
+   if sizing == CodeSize and m < 1 → deny (reason "throttle under CodeSize")      // EntryContracts −1 cannot scale; §5.4 4b
+   Rw = (isNaN(c.pm.R$w) || c.pm.R$w < c.pm.R$) ? c.pm.R$ : c.pm.R$w   // R$w < R$ = bad publisher: use R$, warn once per strategy
+   if sizing == CodeSize:
+       if isNaN(c.pm.R$) || c.pm.R$ ≤ 0 || c.pm.qtyCode ≤ 0 → deny ("no pm.R$")   // R2: unmeasurable risk cannot pass the 1%/heat/cluster checks (today L485 skips them and allows)
+       n = c.pm.qtyCode                               // unchanged; size() returns −1 (_PortMath.Size L362)
+   else:
+       n = size(c, m)                                 // RiskParity: floor(m × riskPerTrade × book / pm.R$)  — STRUCTURAL R$ (P1 DECIDED)
+       if n < minContracts → deny ("size < minContracts")
+   r = n × Rw                                        // WIDENED risk: used for every check below (CodeSize: qtyCode × Rw)
+   if r > maxTradeRisk·book → deny ("per-trade budget: widened risk > 1%")      // the §2.1 skip, both branches/profiles; n is NOT recomputed
+   if heat + r > heatCap·book → deny ("heat cap")                                // widened r; never reduce n
+   occupied = clusterCount[c] > 0 || clusterHeat[c] > 0     // v6.1: seeded open risk also marks the cluster occupied (open risk reported while pm.pos = 0 cannot skip the cap)
+   if occupied && clusterHeat[c] + r > clusterCap·book → deny ("cluster cap")   // P2 DECIDED (c): only when the cluster already holds a position or seeded open risk; widened r; never reduce n
+   heat += r ; clusterHeat[c] += r ; clusterCount[c] += 1 ; openPositions += 1   // clusterCount = open positions in the merged cluster + entries allowed earlier this step; clusterHeat[c] seeded from every publisher's pm.riskOpen (_PortMM L428–433)
+CommonPortfolioData["pm.profile"] = profile ; ["pm.uncorr"] = uncorr ; ["pm.uncorr03"] = uncorr03   // §5.1a, advisory only
```
Note (one sentence for the spec): the decided ddSteps and weekly-halve multipliers are **explicit risk throttles chosen by Ettienne**, applied uniformly before any stop is known; they are not "shrinking size to fit a stop", which stays forbidden — the floor and the caps can only deny.

### C.6 §5.3 RiskParity bullet
```diff
-- **RiskParity (default):** each new entry risks the same $: `n = floor(mult × riskPerTrade × book / pm.R$)`.
+- **RiskParity (default):** `n = floor(mult × riskPerTrade × book / pm.R$)` with `pm.R$` the **structural** stop (P1 DECIDED). The cost floor never changes `n`: the widened risk `n × pm.R$w` is only tested — > `maxTradeRisk` (1%) × book ⇒ skip (deny); heat/cluster caps are checked on the widened risk ⇒ deny. The MM never cuts `n`. Real risk per trade is therefore 0.5% at a structural stop and up to 1% when the floor widens it.
```

### C.7 §5.4 — replace item 1 wording and item 4; add 4a/4b
```diff
-1. **Heat cap** (`AllowEntries`): Σ open risk + new risk ≤ `heatCap` × book (book-level 3–4% rule, PF-04).
+1. **Heat and cluster caps** (`AllowEntries`): Σ open risk + new risk ≤ `heatCap` × book; per merged cluster ≤ `clusterCap` × book, checked **only when that cluster already holds a position** (open, or allowed earlier in the same step; P2 DECIDED (c)) or carries seeded open risk (`clusterHeat[c] > 0` from `pm.riskOpen`, v6.1) — a lone entry in an empty cluster is limited by `maxTradeRisk` (1%) and heat only. A breach **denies** the entry (journal reason); the size is never reduced to fit (both profiles; code change from rev 1, which reduced).
-4. **Strategy kill** (catalogue §2.3 kill rules): rolling R or own `pm.state == Halted` ⇒ ...
+4. **Strategy kill (permanent)** (catalogue §2.3 / PF-05 bench): rolling R (`stratKillR` over `stratKillTrades`) or own `pm.state == Halted` ⇒ `ForceClosePosition()` then `Paused = true` once `PositionSide == 0`; journal; unpause only by restarting the portfolio. `Halted` keeps this permanent meaning and is **not** used for 4a/4b. −8R/20 can fire in the same week as −6R: weekly observe is applied first, then the kill; both are journalled.
+4a. **Session stop after N losses** (PF-05; both profiles — P5 DECIDED): the MM counts per strategy the closed trades with R ≤ −`scratchR` (a loss; |R| ≤ 0.05 is a scratch, not counted) per that strategy's own `pm.day` (tradingDayKey). At `stratDailyLosses` (2) its entries are denied for the rest of that session; open positions keep their own exits (no force-close, no `Paused`). Resets when that strategy's `pm.day` changes.
+    **Relation to CS-7 (F-TM, `_CustomSignal` L2647–2730):** CS-7 already offers the chart-side analogue (`DailyLossStopCount`, protected, default 0 = off; counts `Profit < 0`, no scratch band, per strategy instance, per its tradingDayKey, enforced in `entryAllowed()`). 4a stays in the MM because it covers every PT strategy with no per-strategy opt-in. A strategy may also set `DailyLossStopCount = 2`; the two may then disagree on a scratch trade, and **whichever fires first wins** (both only deny entries). CS-7 is not changed by rev 2.
+4b. **Weekly R throttle** (PF-05; both profiles — P5 DECIDED): closed-trade R summed over the strategy's current trading week (ISO week of its `pm.day`, so Sunday-evening ETH belongs to Monday's week); scratches are included in the sum. ≤ `weeklyHalveR` (−3) ⇒ RiskParity/VolTarget size × `weeklyHalveMult` (0.5); ≤ `weeklyObserveR` (−6) ⇒ entries denied ("observation only": the strategy keeps calculating/publishing; each denied `Armed` journals `PORT observe`). No force-close, no `Paused`. **Reset (P6 DECIDED):** both the −3R halve and the −6R observe state clear at that strategy's first session of the next ISO trading week (`weekR` restarts at 0). **Under CodeSize** the MM cannot scale (`EntryContracts = −1`), so any multiplier < 1 (weekly halve or a ddSteps stage) **denies** with reason "throttle under CodeSize" (closes an existing rev 1 gap where ddSteps 0.5/0.25 were ignored under CodeSize).
+    **R-stream limits** (shared by 4, 4a, 4b): a trade is closed only on position→0; rev 2 also closes it on a **sign flip** (reversal = close + open). Scale-outs are not separate trades. A trade whose open bar had no `pm.R$` is not recorded (journal `PORT kill-R missing`). R is in units of the **structural** 1R (`pm.R$` at open), as the strategy itself counts R. P&L is net of MC cost settings only.
+    **Where it lives:** in the MM (`_PortMM`), not via `pm.state`, because `Halted` is permanent in code and `Paused` blocks exits (§6).
```

### C.8 §5.5 — append
```diff
+- **Day key per strategy:** the MM keeps each strategy's published `pm.day` (already published, `_PortKeys.Day`; no `pm.ver` bump) for 4a/4b; the book day stays the first strategy's for the daily stop and ρ.
+- **Uncorrelated live counts** (§5.1a), from the daily merge, computed lazily at the first merge: `uncorr` = distinct merged clusters (ρ ≥ `rhoMerge` 0.9, seeds always merged) with ≥ 1 live strategy; `uncorr03` = size of a **greedy** set of live strategies with pairwise max(ρ, stressed ρ) < 0.3 over the existing `pm.rho` matrix (no extra pass; greedy in PT table order, seeds treated as ρ = 1).
```

### C.9 §8 build steps — item 4 (tests)
```diff
-4. Off-MC tests `tests/PortMM`: sizing, caps, clusters, dd hysteresis, daily stop, stale handling.
+4. Off-MC tests `tests/PortMMTests` (actual folder): sizing, caps, clusters, dd hysteresis, daily stop, stale handling. Repin `Program.cs` L29 and the heat 4% / cluster 2% / maxPositions 6 / −8R tests to `ForProfile(ScaledBook)`. Rev 2 adds:
+   - T-P1 `ForProfile(Starter)` and `ForProfile(ScaledBook)` pass `Validate()`; values exactly as §5.1; `new PortMmConfig()` equals starter.
+   - T-P2 starter heat: book 1M, heatCap 0.015 ⇒ 15 000; two entries in different clusters with widened risk 5 000 each allowed (in the same cluster the second would total 10 000 > 7 500 and be denied "cluster cap"); a third denied by maxPositions = 2 even with heat room; two entries in different clusters whose widened risk is 9 000 each ⇒ second denied "heat cap" (18 000 > 15 000), first keeps its n. Runs with the starter clusterCap 0.0075: under P2 (c) each entry is alone in its cluster, so the cluster cap is not checked (9 000 ≤ 10 000 per-trade; heat 9 000 ≤ 15 000 for the first), and the heat cap is the reason for the second.
+   - T-P3 starter cluster cap: second EQIDX (ES + NQ) entry **denied** ("cluster cap"), not sized to the remainder; ES + CL both allowed. Same deny for a heat-cap breach in scaled-book.
+   - T-P4 starter ddSteps stages and hysteresis (3/6/10%).
+   - T-P5 session stop (run in starter and in ScaledBook, P5): 2 losing closes in one session ⇒ that strategy denied, others not; no force-close; allowed again when its `pm.day` changes; a winner between losses does not reset the count.
+   - T-P6 weekly: −3.0R ⇒ size halved (floor; < minContracts ⇒ deny); −6R ⇒ denied with `PORT observe`; −2.9R no effect. Reset (P6): after −6R (and after −3R) the first session of the next ISO week ⇒ full size and entries allowed; a Sunday-evening ETH session counts as Monday's week (no early reset); halve and observe in the same strategy both clear. Run in starter and in ScaledBook (P5: same results).
+   - T-P7 interplay: book mult 0.5 × weekly 0.5 = 0.25; daily stop overrides; gate order reason as §5.2.
+   - T-P8 `uncorr`: ES + CL + GC ⇒ 3; ES + NQ + CL ⇒ 2; CL/GC ρ ≥ 0.9 ⇒ merged ⇒ 2; paused / observe / non-live strategies excluded.
+   - T-P9 no automatic switch and the both-counts rule (P9): `uncorr` 2 → 3 changes no profile or limit; one advisory journal line per day; the line says "eligible" only when `uncorr` ≥ 3 **and** `uncorr03` ≥ 3 (uncorr 3 / uncorr03 2 ⇒ "not eligible"; uncorr 2 / uncorr03 3 ⇒ "not eligible").
+   - T-P10 JSON: `"profile": "ScaledBook"` replaces the base, then fields override; unknown profile ⇒ warning, subclass base kept; starter file with clusterCap 0.02 rejected ⇒ `ForProfile(Profile)`.
+   - T-P11 reversal (+2 → −2) records a closed trade.
+   - T-P12 per-strategy day key: CL and ES with different `pm.day` at the same step; ES's session stop does not reset on CL's new day and vice versa.
+   - T-P13 CodeSize + weekly halve (and + ddSteps 0.5) ⇒ deny "throttle under CodeSize".
+   - T-P14 scratch boundary: R = −0.05 not a loss; R = −0.0501 a loss; both added to weekly R.
+   - T-P15 `uncorr03` greedy count on a fixed ρ matrix.
+   - T-P16 floor interplay (P1 DECIDED): book 1M, `pm.R$` 250 (structural), `pm.R$w` 350 (14-tick floor) ⇒ n = floor(5 000/250) = 20 (not 14), r = 7 000 ⇒ allowed (7 000 is also ≤ cluster 7 500 and heat 15 000, so this tests the 1% rule only). Same with `pm.R$w` 600 ⇒ r = 12 000 > 10 000 ⇒ denied "per-trade budget", never resized. `pm.R$w` absent or NaN ⇒ Rw = `pm.R$`. `pm.R$w` < `pm.R$` (bad publisher) ⇒ Rw = `pm.R$` + one warning per strategy (not per bar).
+   - T-P17 a starter subclass config that fails Validate falls back to `ForProfile(Starter)` (not scaled-book) and the RUN line carries `config=fallback`.
+   - T-P18 caps on widened risk: (i) cluster only — two EQIDX candidates with structural risk 5 000 but widened 7 000 each under clusterCap 0.0075: the first enters an empty cluster (cap not checked); the second finds the cluster holding a position, 14 000 > 7 500 ⇒ denied "cluster cap" (heat 14 000 ≤ 15 000, so the cluster cap is the reason); neither n changed. (ii) heat only — two candidates in **different** clusters, widened 8 000 each (each ≤ 10 000 per-trade): 8 000 + 8 000 = 16 000 > heat 15 000 ⇒ second denied "heat cap", first keeps its n. Runs with the starter clusterCap 0.0075: under P2 (c) each is alone in its cluster, so 8 000 > 7 500 does not deny. (iii) CodeSize — `qtyCode` 20, `pm.R$` 250, `pm.R$w` 600 ⇒ r = 12 000 > 10 000 ⇒ denied "per-trade budget", `entryContracts` never changed; with `pm.R$w` 350 the heat/cluster sums use 7 000 (Rw, not `rDollar`). (iv) CodeSize, no measurable risk — `pm.R$` absent/NaN, `pm.R$` 0, and `qtyCode` 0 (each with heat/cluster room) ⇒ denied "no pm.R$" (today allowed with no checks, L485); RiskParity with NaN `pm.R$` ⇒ denied "no R$" by the pre-candidate filter (`_PortMM` L461, before sizing; confirms that branch is already safe).
+   - T-P19 `_CustomSignal.portfolioArm` with `widenedStop` publishes `pm.R$` structural, `pm.R$w` widened, `pm.riskNew = pm.R$w × qtyCode`; without it (or with a widened stop closer than structural) `pm.R$w` = NaN, overwriting any previous value (arm once with, then without ⇒ NaN, not the stale value); after `startPortfolio` `pm.R$w` = NaN; `sizeForRisk(…, widenedStop)` sizes `qty` on the structural stop (same qty as without `widenedStop`).
+   - T-P20 P2 (c) cluster cap only on an occupied cluster (starter, book 1M: per-trade 10 000, heat 15 000, cluster 7 500): (i) lone ES, empty EQIDX cluster, structural 5 000, widened 9 000 (0.9%) ⇒ allowed, n unchanged (9 000 > 7 500 but the cluster is empty); boundaries widened 7 500 (0.75%) and 10 000 (1%) ⇒ allowed, 10 001 ⇒ denied "per-trade budget" (not "cluster cap"). (ii) with that ES open (9 000), NQ (same merged cluster) widened 5 000 ⇒ heat 14 000 ≤ 15 000 but cluster 14 000 > 7 500 ⇒ denied "cluster cap", n not reduced. (iii) same step: ES and NQ both candidates, empty cluster, widened 9 000 and 5 000 ⇒ ES allowed, NQ denied "cluster cap" (an entry allowed earlier in the step counts as holding). (iv) ES open 9 000 + CL widened 5 000 ⇒ allowed (different cluster, heat 14 000). (v) after ES closes (cluster empty again) NQ widened 9 000 ⇒ allowed. (vi) ScaledBook: same rule — lone entry vs clusterCap 0.02 not checked; second same-cluster entry checked. (vii) v6.1 open risk while flat: a test publisher in EQIDX writes `pm.pos` 0 and `pm.riskOpen` 5 000 (clCount 0, clHeat 5 000); NQ widened 5 000 ⇒ heat 10 000 ≤ 15 000 but the cluster is occupied by seeded risk and 10 000 > 7 500 ⇒ denied "cluster cap", n not reduced; with `pm.riskOpen` 0 the same NQ entry is allowed (cluster empty).
+   - T-P21 1% skip in `sizeForRisk` (chart / non-portfolio runs and Data2 calls; with an MM the MM checks): equity 1M, sizing 0.5% on structural stop ⇒ qty 20; widened R$ 350 ⇒ 7 000 ≤ 10 000 ⇒ qty 20 returned; widened R$ 600 ⇒ 12 000 > 10 000 ⇒ returns 0 (not 16) and does not arm; no widenedStop ⇒ unchanged behaviour. Boundary: widened R$ 500 ⇒ exactly 10 000 = 1% ⇒ allowed (qty 20; the test is `>`, not `≥`). `widenedStop` closer than the structural stop ⇒ ignored: no skip, qty 20, `pm.R$w` = NaN. Final qty: the check uses the qty after maxQty, notional cap and roundLots (L2800–2807), e.g. maxQty 15 with widened R$ 600 ⇒ 9 000 ⇒ qty 15 allowed. Data2 call (`bars != Bars`, also with `m_portOn`) with widened 12 000 ⇒ returns 0 and does not arm (Data2 never arms, L2809, so the MM never sees it and the skip must stay). Portfolio mode (R1, `m_portOn`): widened 12 000 ⇒ returns 20 and arms with `pm.R$w` 600; the MM then denies "per-trade budget".
+   - T-P10 addition: JSON `"maxTradeRisk": 0.02` (> 0.01) ⇒ config rejected ⇒ fallback to `ForProfile(Profile)` with `config=fallback` (same path as T-P17).
```

### C.9a §8 build/setup steps — new item (PT setup)
```diff
+5. **Portfolio Trader setup:** set MC's "Max % capital at risk per position" (PT Portfolio Settings) to **≥ 1%** (≥ `maxTradeRisk`). Otherwise MC silently drops exactly the orders whose widened risk is between 0.5% and 1%, which P1 allows. Check it in the PT live check before the first rev 2 run.
```

### C.10 New §11 Revision log
```diff
+## 11. Revision log
+- **rev 1** (2026-10-21): initial spec; coded in PRs #14/#17/#22.
+- **rev 2** (DRAFT 2026-10-24, v2 2026-10-29 after Analyst 2 review): two risk profiles — starter (default until ≥ 3 uncorrelated live strategies; catalogue §6/PF-04/05/06: heat 1.5%, 2 positions, 0.5% per trade, 12% vol) and scaled-book (rev 1 numbers). Profile = protected property / subclass / JSON `profile`; no `[Input]`; manual switch only. Caps deny instead of reducing size (both profiles). P1 DECIDED (Ettienne Hugo, 2026-10-29): size from the structural stop (`pm.R$`, 0.5%); new optional `pm.R$w` widened stop; skip if widened risk > 1% (`maxTradeRisk`); heat/cluster caps on widened risk, deny. New §5.4 items 4a (session stop after 2 losses) and 4b (weekly −3R halve / −6R observe) in the MM, per-strategy `pm.day`; CodeSize denies when a multiplier < 1. §0 corrected (no ConfigPath input). §5.1 lists every coded field. rev 2 v4: structural-stop sizing kept in `sizeForRisk`; `pm.R$w` always written (NaN = none); Rw clamp; CodeSize uses Rw; 1% skip in `sizeForRisk`; `pm.ver` stays 1 (same-build deploy). rev 2 v5: `sizeForRisk` skips only off-portfolio (with an MM it arms and the MM decides); CodeSize denies "no pm.R$" when risk is unmeasurable. rev 2 v6: P2 DECIDED (c) — cluster cap checked only when the cluster already holds a position (v6.1: or seeded open risk); P5 DECIDED — 4a/4b in both profiles; P6 DECIDED — −6R observe clears weekly like −3R; P9 DECIDED — scaled-book eligible only when `uncorr` ≥ 3 and `uncorr03` ≥ 3 (all Ettienne Hugo, 2026-10-29). Tests T-P1…T-P21. Decisions in specs/m-port-risk-profiles-decisions.md.
```

## D. New §5.1 (full text)

### 5.1 Config (`PortMmConfig`) and risk profiles

Two profiles. **Starter is the default** (`new PortMmConfig()` and `_PortfolioRiskManager`) until ≥ 3 uncorrelated strategies are live (§5.1a). Bracketed P-numbers name the decision behind a value (§G; all decided).

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
| `weeklyObserveReset` (new) | `Weekly` (P6 DECIDED) | `Weekly` (P6 DECIDED) | `Weekly` (decided default) = the −6R observe state clears at the strategy's first session of the next ISO week, like −3R; `Manual` = persists until portfolio restart (kept only as a non-default override) |
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
   Note on "never cut size": `sizeForRisk`'s existing `maxQty` and notional caps (L2800–2806) can lower qty. They are limits the strategy author sets, not M-PORT caps, and rev 2 does not change them.

Loaded from `ConfigPath` JSON if given (field-by-field over the selected profile's defaults); else the profile defaults. Journal the effective config and profile once (`RUN` line; `config=fallback` when a fallback happened, also in `Status`).

### 5.1a Profile selection

- `_PortfolioRiskManager`: `protected virtual EPortProfile Profile => EPortProfile.Starter;` `protected virtual PortMmConfig Config => PortMmConfig.ForProfile(Profile);` Ship `_PortfolioRiskManager_ScaledBook : _PortfolioRiskManager` overriding `Profile`. No `[Input]`.
- A config that fails Validate falls back to `ForProfile(Profile)` — never to another profile.
- JSON: `"profile"` is applied first and replaces the base (`ForProfile(p).Copy()`), then other fields override. No `profile` ⇒ base = the subclass `Config`. Unknown profile ⇒ warning, subclass base kept. (P8, analysts' call.)
- **Manual only.** The MM never changes profile or limits. Switching = select the other MM signal (or edit the JSON) in PT and restart the portfolio; journal both.
- **Live strategies:** `protected virtual ISet<string> LiveIds => null` (null = every publishing strategy) or JSON `liveIds` (array of `pm.id`). Live = in `LiveIds`, valid `pm.ver`, not stale, not `Paused`/killed, not in weekly observation. (Separated from P8.)
- **"3+ uncorrelated live strategies"** = `uncorr` ≥ 3: after the daily §5.5 merge (union-find on max(ρ, stressed ρ) ≥ `rhoMerge` 0.9; `pm.cluster` seeds always merged, so ES/MES/NQ/MNQ/YM/RTY are one), the number of distinct clusters with ≥ 1 live strategy. < `rhoMinDays` own `pm.ret` ⇒ `pm.uRet` (as in §5.5). The MM also reports `uncorr03` (P9) and journals once per day `PORT advisory: uncorr=N (ρ≥0.9 clusters), uncorr03=M (greedy ρ<0.3) — scaled-book eligible when <rule>; switch manually`. **Switch rule (P9 DECIDED):** scaled-book eligible when `uncorr` ≥ 3 **and** `uncorr03` ≥ 3; `<rule>` in the line reads `uncorr≥3 and uncorr03≥3`.

## E. Catalogue §6 note (to add after §6.1 item 9 in `/workspace/trading-idea-catalogue.md`; separate docs change after PR-B merges)

```diff
+10. **Two M-PORT risk profiles (decided by Ettienne, 24 Oct 2026; M-PORT spec rev 2 §5.1).** Until ≥ 3 uncorrelated strategies are live, the numbers above are the live defaults: the **starter** profile = heat cap 1.5%, ≤ 2 positions, 0.5% per trade sized at the structural stop (a trade whose cost-floored stop would risk more than 1% is skipped, never resized), 12% book vol target, cluster cap 0.75% applied once a correlation cluster already holds a position or open risk (a lone entry is limited by the 1% skip and the heat cap) — with 0.5% entries and a cap breach denying the entry (size is never cut to fit), that is one position per correlation cluster, ES and NQ counting as one — drawdown steps 3/6/10%, the A2-78 book daily stop (−2% no new entries, −3% flatten), and the PF-05 per-strategy throttles enforced by the portfolio MM: no new entries for the rest of the session after 2 losing trades; −3 R in a trading week → half size, −6 R → observation only (no live entries), both clearing at the start of the next trading week. The **scaled-book** profile (heat 4%, 6 positions, 2% cluster cap, drawdown steps 5/10/15%, 10% vol target, same daily stop and per-strategy throttles) applies only after the number of distinct ρ ≥ 0.9 clusters holding a live strategy reaches 3 and the greedy count of live strategies with pairwise ρ < 0.3 also reaches 3. The switch is manual (a different MM signal or config file, then a portfolio restart), never automatic. The stricter PF-02 admission test (ρ < 0.3) still governs whether a sleeve is added at all; the §2.1 floor rule's 1% skip budget is unchanged and is not PF-04's 0.5%.
```
Text reflects P2 (c), P5, P6 and P9 as decided by Ettienne on 2026-10-29. Also Appendix B: "v2.3c: §6.1 item 10, two risk profiles (M-PORT rev 2)". PDF re-render later.

## F. Code changes Coder would need (origin/main @ 800bce7), as separate small PRs

**PR-A — spec text only** (`specs/m-port-spec.md`): §0 fix, §3 `pm.R$`/`pm.R$w`/`pm.qtyCode`/`pm.riskNew` rows, §5.1 full field list incl. the five missing coded fields, tests path `tests/PortMMTests`, revision log. No code.

**PR-B0 — publisher `pm.R$w` (lands first; harmless alone, the old MM ignores the key)**
1. `_PortKeys`: `RDollarW = "pm.R$w"` (constant only; shared DLL).
2. `_CustomSignal.portfolioArm`: optional `double widenedStop = double.NaN`; **always** write `pm.R$w` = |entry − widenedStop| × BPV when finite and ≥ structural, else NaN (never leave a previous value); `pm.riskNew = Rw × qtyCode` (Rw = structural when NaN). `startPortfolio` (L1550–1567) initialises `pm.R$w` = NaN next to `RiskNew` (L1564).
3. `_CustomSignal.sizeForRisk`: new optional `double widenedStop = double.NaN`. Sizing stays on the structural `stop` (L2798); forwards `widenedStop` to `portfolioArm` (L2809). Adds the P1 step-5 skip, **only when it will not arm** (`!m_portOn || bars != Bars`): if `widenedStop` is finite, not closer than `stop`, and `final qty × |entry − widenedStop| × BPV > 0.01 × equity` (final qty = after maxQty/notional/roundLots, L2800–2807; the same equity `stopLossBasedPositionSize` uses) ⇒ trace and return 0, never a smaller qty, and do not arm. When `m_portOn` and `bars == Bars`: no skip; arm and let the MM decide. maxQty/notional caps (L2800–2806) are author-set strategy limits, unchanged. The `minStopTicks` skip (L2793–2797) stays a skip; nothing widens the stop.
4. Tests T-P19, T-P21. Existing callers unchanged.

**PR-B — profiles + deny-not-reduce** (after PR-B0)
1. `_PortMM.Function.CS`: `public enum EPortProfile { Starter = 0, ScaledBook = 1 }`; field `profile`; starter initialisers (`bookVolTarget 0.12, heatCap 0.015, clusterCap 0.0075, maxPositions 2, ddSteps {0.03,0.5},{0.06,0.25},{0.10,0}`); `public static PortMmConfig ForProfile(EPortProfile p)` (ScaledBook = rev 1 values exactly); field `maxTradeRisk = 0.01`; `Validate()` additions (§5.1); `ToString()` prints profile.
2. Decision loop L476–504 (`maxPositions` L480 stays before sizing L481): size `q` from `s.rDollar` (structural, unchanged); new `Rw = (isNaN(s.rDollarW) || s.rDollarW < s.rDollar) ? s.rDollar : s.rDollarW` with a one-time warning per strategy for the `<` case, used for every risk check; **RiskParity/VolTarget branch** (L494–502): remove the trim (L496–499 `room`/`qMax`/`if (q > qMax) q = qMax`); `maxTradeRisk`, heat and cluster on `q × Rw` ⇒ deny with the existing reason strings; the cluster check runs only when the cluster is occupied, `clCount[c] > 0 || clHeat[c] > 0` (P2 (c); v6.1: seeded open risk counts as occupied); `heat += q × Rw; clHeat[c] += q × Rw; clCount[c]++`. **CodeSize branch** (L483–493): switch `rUnit = s.rDollar` (L482) to Rw, so `r = qtyCode × Rw`; replace the L485 guard (which today allows with no checks) with a deny "no pm.R$" when `rDollar` is NaN/≤ 0 or `qtyCode` ≤ 0 (v4 review R2); add the `maxTradeRisk` deny before heat/cluster; the cluster check there uses the same occupancy test `clCount[c] > 0 || clHeat[c] > 0`, and allowed entries add to `clCount[c]`. New `Dictionary<int,int> clCount` next to `clHeat` (L428), incremented in the open-risk seeding loop where `s.position != 0` (L432–433, next to `open++`), so it counts open positions per merged cluster; `clHeat[c]` keeps being seeded for every publisher with `riskOpen > 0` (L431–432), so a cluster with open risk but `position == 0` is still occupied (v6.1). Finding @ 1dc1abb: `_CustomSignal` sets `riskOpen` > 0 only inside `if (pos != 0 …)` (L1619–1625) and publishes `pm.pos` and `pm.riskOpen` together from the same `pos` (L1593, L1629–1631), so a `_CustomSignal` publisher cannot report open risk while flat; the MM-side seed (L431) does not check `position`, so a non-`_CustomSignal` publisher (or a hand-written `pm.*` writer) could — the `clHeat[c] > 0` clause is a defensive guard, not a fix for an observed case.
2a. (`RDollarW` key from PR-B0) `PortStratView.rDollarW`; `_PortfolioRiskManager.view()` reads it next to L121. `updateKills` keeps `riskAtOpen` on structural `rDollar`.
2b. Moved to PR-B0 (`portfolioArm` always writes `pm.R$w`, `startPortfolio` initialises it to NaN, `sizeForRisk` `widenedStop` parameter + 1% skip).
3. `PortMmConfigLoader.Parse`: pre-scan `"profile"` ⇒ base `ForProfile(p).Copy()`; add `maxtraderisk`; a value > 0.01 fails `Validate()` ⇒ whole file rejected ⇒ fallback to `ForProfile(Profile)` (same path as T-P17; test in T-P10).
4. `_PortfolioRiskManager.Strategy.CS`: `Profile` virtual; `Config => PortMmConfig.ForProfile(Profile)`; L38 fallback ⇒ `ForProfile(Profile)`; RUN line + Status `config=fallback`; write `pm.profile`.
5. New `_PortfolioRiskManager_ScaledBook.Strategy.CS`; `PLStudies.csproj` `<Compile Include="_PortfolioRiskManager_ScaledBook.Strategy.CS" />` after L548.
6. `samples/PortMmConfig.example.json` (starter values + `profile`, `maxTradeRisk`) and new `samples/PortMmConfig.scaled-book.json`.
7. Tests: repin L29 and cap tests to ScaledBook; T-P1–T-P4, T-P10 (incl. `maxTradeRisk` > 0.01), T-P16–T-P18 (incl. heat-only, CodeSize and CodeSize "no pm.R$" (iv) cases), T-P20 (P2 (c), incl. (vii) open risk while flat).

**PR-C — session/weekly throttles**
1. `PortStratView`: keep each strategy's `pm.day` (`_PortfolioRiskManager.view()` L129 currently only fills the book `day`).
2. `KillTracker`: `lossesToday`, `lossDay`, `weekR`, `weekKey`, `observe`; `updateKills`: close on position→0 **or sign flip**; roll keys on that strategy's `pm.day`; loss if R < −scratchR; add every R to `weekR`; journal `PORT kill-R missing` when `riskAtOpen` is NaN.
3. Decision loop: `sMult` gate in the §5.2 order, in both profiles (P5); CodeSize + m < 1 ⇒ deny; `PORT observe` journal.
4. Config fields `stratDailyLosses`, `scratchR`, `weeklyHalveR`, `weeklyHalveMult`, `weeklyObserveR`, `weeklyObserveReset` + loader cases + Validate; `weeklyObserveReset` default `Weekly` in both profiles (P6): on a new ISO week key `weekR = 0` and `observe = false`.
5. Tests T-P5–T-P7, T-P11–T-P14.

**PR-D — advisory**: `uncorr`, `uncorr03` (greedy over the cached ρ matrix, computed lazily at the first daily merge), `LiveIds` virtual + JSON `liveIds`, `_PortKeys.Profile`/`Uncorr`/`Uncorr03`, daily advisory line stating eligibility only when `uncorr` ≥ 3 and `uncorr03` ≥ 3 (P9); tests T-P8, T-P9, T-P15.

**Docs** (after PR-B): catalogue §6.1 item 10 + Appendix B line.

`_CustomSignal` changes only in PR-B0 (`portfolioArm`/`sizeForRisk` optional `widenedStop`, `startPortfolio` init; CS-7 and `PortfolioHalted` unchanged), `_TestPortMM`, `_TestPortArm`, PriceAction, or M-DVP. M-DVP strategies (incl. the PR #42 A2-33 hook) are ordinary publishers; an entry denied by 4a/4b leaves their DVP journal lines untouched.

## G. Decisions

| # | Rule | Status |
|---|---|---|
| P1 | size n from the structural stop at 0.5%; skip if n × widened R$ > 1%; heat/cluster caps on widened risk deny, never trim | **DECIDED (Ettienne Hugo, 2026-10-29)** |
| P2 | deny-on-breach for heat and cluster caps, both profiles; clusterCap stays 0.0075 (starter) / 0.02 (scaled-book) and is checked only when the cluster already holds an open or earlier-allowed position or seeded open risk (v6.1) — keeps the 1% budget for a lone entry and one-per-cluster; per-cluster count + T-P20 | **DECIDED (c)** (Ettienne Hugo, 2026-10-29): "P2: apply the cluster cap only when the cluster already holds a position". Deny-on-breach in both profiles kept |
| P3 | starter ddSteps 3/6/10% | analysts: accepted |
| P4 | keep −8R/20 permanent kill, both profiles; order weekly observe → kill | analysts: accepted |
| P5 | 4a (2-loss session stop) and 4b (−3R/−6R weekly throttle) in both profiles | **DECIDED** (Ettienne Hugo, 2026-10-29): "P5: apply in both profiles" |
| P6 | weekly reset at the strategy's first session of the next ISO trading week, for −3R and −6R alike (`weeklyObserveReset = Weekly`) | **DECIDED** (Ettienne Hugo, 2026-10-29): "P6: −6R state clears weekly" |
| P7 | loss = R < −0.05 (\|R\| ≤ 0.05 scratch); weekly sum includes scratches; differs from CS-7 (`Profit < 0`), documented not aligned | analysts: accepted |
| P8 | JSON `profile` replaces base, then field overrides | analysts: accepted |
| P9 | report `uncorr` and greedy ρ < 0.3 `uncorr03`; scaled-book eligible (manual switch) only when both ≥ 3 | **DECIDED** (Ettienne Hugo, 2026-10-29): "P9: require both counts" |

## H. Live-check items (remaining)
- How `pm.day` behaves for a strategy on a step where it has no new bar (stale `pm.day` should not roll the counters) — covered by T-P12; confirm in PT live check L7.
- CostConfig (IP-3.2) is spec-only; until it lands confirm which live strategies call `portfolioArm(..., widenedStop)` and publish `pm.R$w` (per-example `costFloorTicks=14`).

## Appendix: Analyst 2 review disposition

| Review item | Disposition |
|---|---|
| §1 F-TM exists | Accepted, re-verified @800bce7 (lines moved +27: L2647–2730) |
| §1 fallback not silent | Accepted; wording fixed |
| §1 tests L29 breaks | Accepted, re-verified |
| §2.2 per-strategy day key, option (a) | Accepted (`pm.day` published at `_CustomSignal` L1635) |
| §2.3 LiveIds | Accepted |
| §2.4 _TestPort* | Accepted, re-verified |
| §2.5 csproj | Accepted, re-verified L547–548 |
| §2.7 R-stream limits | Accepted; reversal now closes (T-P11) |
| §2.8 costs | Accepted |
| §2.9 CodeSize | Accepted and extended: the gap already exists for ddSteps (`_PortMath.Size` L362) |
| §3 P7 "brief says ≤ 0.05" | **Partly disputed:** v1 used "< 0.05" consistently in both places (no "≤" anywhere in v1 or the brief), so it was not an inconsistency. Adopted ≤ 0.05 anyway as one written rule |
| §3 P9 "greedy" | Accepted; noted greedy = order-dependent lower bound on the maximum set |
| §3 P2 "equivalent and simpler: 0.005 with deny" | **Disputed (minor):** not equivalent — at m = 1 both allow one entry per cluster, but after a weekly-halve or ddSteps 0.5 stage 0.0075 lets a full 0.5% position be joined by one 0.25% entry in the same cluster, which 0.005 forbids; and 0.0075 leaves room for open-risk drift on the first position. Kept 0.0075 (v6: Ettienne decided P2 (c) with 0.0075; 0.005 not adopted) |
| §4.3 drop duplicate `heatCap ≥ riskPerTrade × 1` | Accepted |
| §4.5 T-P8 garbled | Accepted, fixed |
| §4.7 PR split | Accepted |
| §4.8 catalogue text | Accepted, now conditional on deny-on-breach |
| §4.9 line refs | Confirmed |
| §6 CostConfig interplay | Accepted (§C.3, §D) |

### v3 review (Analyst 2, 2026-10-29) disposition — all applied in v4
| Required edit | Disposition |
|---|---|
| 1 §C.3 `sizeForRisk` sentence | Fixed: structural stays the sizing stop; floor only via `widenedStop`; `sizeForRisk` in PR-B0 item 3 (was PR-B 2b) |
| 2 stale `pm.R$w` | Fixed: always written (NaN), initialised in `startPortfolio`; T-P19 reworded |
| 3 Rw clamp | Added in §C.5 and PR-B 2 |
| 4 CodeSize line, gate order | Added; `maxPositions` moved before sizing (code L480) |
| 5 `pm.ver` / readers | §C.3; stays 1, same-build caveat; H item closed |
| 6 chart/CodeSize 1% skip | PR-B0 item 3 + T-P21 (chose "PR item and test", not "per-example") |
| 7 MC max % at risk | §C.9a setup step; H item closed |
| 8 tests | Reordered; T-P18 (ii)/(iii), T-P10 addition, T-P20 (placeholder in v4; full test in v6) |
| 9 P2 options | §G and decision sheet; decided (c) in v6 |
| 10 PR-B0 split | Done |

### v4 review (Analyst 2, 2026-10-29) disposition — all applied in v5
| Required edit | Disposition |
|---|---|
| R1 `sizeForRisk` skip only off-portfolio | §D P1 step 5 and PR-B0 item 3: skip only when the call will not arm (`m_portOn` false, or a Data2 call, per Q1); with an MM on Data1 it arms and the MM decides. T-P21 portfolio-mode case added |
| R1/Q1 T-P21 cases | Added: exactly 1% allowed; widened stop closer than structural ignored; check on final qty (after maxQty/notional/roundLots); Data2 skips without arming; portfolio mode arms, MM denies |
| R2 CodeSize "no pm.R$" | §C.5 pseudocode and PR-B 2: deny when `pm.R$` NaN/≤ 0 or `qtyCode` ≤ 0; T-P18 (iv) added (incl. RiskParity NaN ⇒ "size < minContracts"; v6 correction: the code denies it earlier as "no R$", `_PortMM` L461) |
| R3 Starter consistency | "(b)/(c) denied by heat cap" limited to different clusters; same cluster ⇒ cluster cap first |
| Side note maxQty/notional | Note under §D P1 step 5 (L2800–2806, author-set limits, not M-PORT caps) |
| Suggestion T-P2 clusterCap | Applied in v5 (clusterCap 0.01); superseded in v6: under P2 (c) T-P2 and T-P18 (ii) run with the starter clusterCap 0.0075 |
| Line refs | All re-checked @800bce7 (fresh clone /tmp/mcn-v5); `stopLossBasedPositionSize` call cited as L2798 (was L2799, the qty ≤ 0 guard); loader ref labelled `_PortMM` L209–210 |

### v6 (2026-10-29) — Ettienne's decisions applied
| Decision | Where applied |
|---|---|
| P2 (c) — "P2: apply the cluster cap only when the cluster already holds a position" | §C.5 (`clusterCount` guard), §C.7 item 1, §D table + Starter consistency, §E, PR-B 2/7, §G; T-P2 / T-P18 (i)(ii) rewritten for the starter cap, T-P20 full test |
| P5 — "P5: apply in both profiles" | §C.7 4a/4b, §D table, PR-C 3, §G; T-P5/T-P6 run in both profiles |
| P6 — "P6: −6R state clears weekly" | §C.7 4b reset, §D `weeklyObserveReset` default, PR-C 4, §G; T-P6 reset cases |
| P9 — "P9: require both counts" | §5.1a switch rule, §E, PR-D, §G; T-P9 eligibility cases |

### v6.1 (2026-10-29) — Analyst 2 v6 review
| Review item | Disposition |
|---|---|
| R1 T-P2 first sub-case clusters | Fixed: "two entries in different clusters with widened risk 5 000 each allowed"; same-cluster note (10 000 > 7 500 ⇒ "cluster cap") added; maxPositions third entry any cluster |
| Minor: clHeat seeded for any publisher, clCount only where `position != 0` | Applied: occupied = `clusterCount[c] > 0 \|\| clusterHeat[c] > 0` in §C.5, §C.7 item 1, §D table + Starter consistency, §E wording, PR-B 2 (both branches) and 7, §G P2, rev 2 history; T-P20 (vii) added. Finding @ 1dc1abb (fresh clone /tmp/mcn-v61): `_CustomSignal` cannot report open risk while flat (`riskOpen` computed only when `pos != 0`, L1619–1625, published with `pm.pos` from the same `pos`, L1629–1631); `_PortMM` L431 seeds without checking position, so other publishers could — defensive guard |
| Manual `weeklyObserveReset` option (extra scope) | Not adopted: keep `Weekly` only (Manual remains a non-default override as written) |

### Approval
v6.1 APPROVED for implementation by Ettienne Hugo, 2026-10-31.
