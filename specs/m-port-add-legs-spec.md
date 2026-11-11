---
title: "M-PORT add legs: let the portfolio risk manager approve same-direction adds within the risk caps (DRAFT spec, proposed rev 2.8)"
date: 2026-11-10 (ET)
status: "DRAFT, docs only, no code. Requested by Ettienne Hugo 2026-11-10 22:16 ET: \"Yes, spec the Portfolio risk manager change as a separate PR.\" Not approved for implementation. Everything not quoted from the owner is PROPOSED."
code_refs: "line refs are against origin/main @ 47ffda2 (2026-11-10, merge of PR #116, Check 7 findings)"
related: "specs/m-port-spec.md rev 2.7 (§5.2, §5.4 4c, §6, §9); specs/live-check-sheet.md Check 7 (L1–L9); PR #117 specs/strategies/00-programme-overview.md §7.7 (strategy-side add inputs)"
---

# M-PORT add legs (DRAFT, proposed rev 2.8)

## 0. Why, and scope

- **Owner decisions this builds on.**
  - 2026-11-10 22:06 ET (strategy specs, PR #117): *"you can allow adding to positions, for strategies where that make sense make it a clear Input parameter and allow adding."*
  - 2026-11-10 22:16 ET: *"Yes, spec the Portfolio risk manager change as a separate PR."*
- **The problem.**
  - The strategy specs on PR #117 now have add inputs: `AddMode`, `MaxAdds`, `AddContracts`, `AddMinR`, `AddStopMode` (overview §7.7). Users are VW-01, AV-02, AV-01 (default `None`) and the YV-11 candidate (default `OnNewPullback`).
  - The MM refuses every same-direction entry while a strategy holds a position. So in Portfolio Trader (PT), every add is denied, and PT runs must use `AddMode = None`.
- **Goal.** One opt-in MM policy that approves an add leg only when the position's total risk after the add stays inside the existing caps (per-trade 1%, heat, cluster, total). With the policy off (the default), behaviour is byte-for-byte unchanged.
- **Not in scope.**
  - No change to sizing of the initial entry, reversals (rev 2.5 4c), throttles, kills or exits.
  - No new `[Input]` on `_CustomSignal`.
  - No broker connection, live trading or forward testing.
  - Chart runs are unaffected: M-PORT is inert outside PT, and the strategy's own §7.7 chart-side 1% add check applies there.
- **Holdout.** Every native check below uses data ≤ 31 Dec 2024.

## 1. Current refusal path (as coded)

1. **Publisher (`_CustomSignal`).**
   - `portfolioBar()` (`_CustomSignal` L1754–1797) runs inside `base.CalcBar()` at L2445. It publishes:
     - `pm.state`: `Long`/`Short` while in a position, else `Flat`, or `Halted` (L1782, L1790);
     - `pm.pos`: the signed position (L1789).
   - It then clears `pm.side` and `pm.riskNew` unless the strategy armed on this bar (L1796: `m_portArmBar != Bars.CurrentBar`).
   - The derived strategy arms **after** `base.CalcBar()`. `portfolioArm(side, entry, stop, qtyCode, widenedStop)` (L1738–1749) writes:
     - `pm.R$`, `pm.R$w`, `pm.qtyCode`, `pm.riskNew` (via `_PortArm.Write`);
     - `pm.side` = ±1.
     It sets `pm.state = Armed` **only when flat** (L1748). An arm while in a position therefore leaves `pm.state` = `Long`/`Short`.
2. **MM view (`_PortfolioRiskManager.view`, L121–159).** It reads `pm.state`, `pm.side`, `pm.pos` (falling back to `PositionSide` as a sign, L128), `pm.R$`, `pm.R$w`, `pm.qtyCode` and `pm.riskOpen`.
3. **MM candidate filter (`_PortMM.Decide`, L803–830).**
   - `isReversal(s)` (L739–742) is `position != 0 && side ∈ {±1} && side != sign(position)`.
   - L813: `if (why == null && s.state != EPortState.Armed && !rev) why = s.state.ToString();`. An add arrives with state `Long`/`Short`, so it is refused here with reason `"Long"`/`"Short"`.
   - L814: `if (why == null && s.position != 0 && !rev) why = "in position"; // same-direction add stays refused`. This catches the rarer case of state `Armed` with a position (pinned by T-P22, `tests/PortMMTests/Program.cs` L515–517).
   - Neither reason is an "entry attempt" (L819), so the refusal is **silent**: no journal line, only `d[i].reason` in `Status`.
4. **Deny-all.** Every `PortDecision` starts with `allowEntries = false` and `entryContracts = −1` (L427, L761). `_PortfolioRiskManager.CalcBar` writes both every step (L108–111). An in-position strategy therefore has its entries denied, its `EntryContracts` at −1, and its exits untouched.
5. **Spec text that says the same.**
   - m-port-spec §5.2 L172 ("same-direction adds stay out: in position").
   - §5.4 4c ("a same-direction add while in position stays refused").
   - §5.2 L207 ("Positions already open are not resized in v1").
   - §6 L246 (`EntryContracts > 0` applies to every entry, including scale-ins).
   - §5.4 R-stream limits ("adds after the open do not update the risk at open").

**Publisher hazard found while reading (pre-existing, latent).** `portfolioArm` overwrites `m_portRDollar` and `m_portStop` even when in a position (L1745). An add arm that never fills would still move the published active stop (`pm.riskOpen`) and the `pm.openR` unit on the next bar. Today only reversals arm in position, and after a reversal fills the overwrite is correct. Adds need the separate path in §2.2.

## 2. How an add is identified

### 2.1 MM rule
A view `s` is an **add candidate** when **all** of these hold:
1. `s.position != 0` and `s.side == Math.Sign(s.position)`: same direction. This is the exact complement of `isReversal` among in-position arms.
2. `s.state ∈ {Long, Short}` matching `sign(position)`, or `Armed`. Accept both so the L814 case is covered; `Halted` and `Exiting` never qualify.
3. `pm.add ≥ 1`: the publisher declares this pending entry an add leg, and gives its ordinal (§2.2). A same-direction arm **without** `pm.add` keeps today's refusal (`"Long"`/`"Short"`/`"in position"`, silent). An old publisher or a strategy bug therefore cannot pyramid by accident.
4. `pm.armStamp == pm.stamp`: the add was armed on the publisher's current bar, so it is not a leftover (§2.2). It is not stale (`staleBars`), and every pre-loop rule applies (ver, currency, paused/killed).
5. The `addPolicy` config (§7) is `AllowWithinCap`. With `Refuse`, points 1–4 are not evaluated and L813/L814 run exactly as today.

### 2.2 Publisher keys and a separate arm call (PROPOSED)
These are additive optional keys, so **`pm.ver` stays 1**: the same rule as `pm.R$w` and `pm.regimeP`, and readers pull by name. Same-build deploy, as for rev 2.

| Key | Type | Written | Meaning |
|---|---|---|---|
| `pm.add` | int | every bar | 0 = no add pending (the default, reset in `portfolioBar` with `pm.side`); k ≥ 1 = the pending entry is the k-th add of the open position |
| `pm.addStop` | double | with `pm.add` | the **whole-position** stop after the add fills: `RaiseToAddLow` = add structural low − 1 tick (long), `KeepCurrent` = the current active stop. NaN ⇒ the MM refuses ("add: no addStop") |
| `pm.avgPx` | double | every bar | `StrategyInfo.AvgEntryPrice` while in a position (NaN when flat); needed for the risk after the add (§3) |
| `pm.bpv` | double | StartCalc | `Bars.Info.BigPointValue`, so the pure `_PortMM` can price the risk after the add without an `IStrategy` |
| `pm.armStamp` | DateTime | with every arm (initial, reversal, add) | `Bars.Time[0]` at the arm. The MM requires `armStamp == stamp` for an add (an "ARM stamp" in the spirit of `_TestPortArm`'s `ARM#i STAMP` journal) |

- **New publisher call `portfolioArmAdd(int side, double entry, double addStop, int qtyAdd, int addIndex, double widenedAddStop = NaN)`.**
  - It writes `pm.side`, `pm.add`, `pm.addStop`, `pm.armStamp`. It also writes `pm.R$` / `pm.R$w` / `pm.qtyCode` / `pm.riskNew` **for the add leg**: `R$` = |entry − addStop| × BPV per contract, `qtyCode` = `qtyAdd`.
  - It does **not** touch `m_portRDollar` or `m_portStop`. The open position's `pm.openR` unit stays the initial 1R, and `pm.riskOpen` keeps the current active stop until the add fills.
  - When the position grows in the add's direction, the strategy calls `portfolioStop(addStop)` (its §7.7 stop move), as today.
- `portfolioArm` while in a position, same direction, logs one `PORT Warning … same-direction arm without portfolioArmAdd - treated as not an add` per strategy, and the MM refuses it as today.
- **Order naming** (`<ID>LA1`, `<ID>LA2`, … for adds; overview §7.7) belongs to the strategy and the journal only. The MM cannot see order names: `IPortfolioStrategy` exposes no pending-order list (m-port-spec §1). `pm.add` = k must match the order-name suffix, and the strategy's `ADD` log line carries both.

## 3. Risk accounting for an add

**Principle.** An add is a change to an open position, the same shape as a reversal (rev 2.5). The caps see the book **without** the position's current open risk and **with** the position's risk after the add, at the stop the add sets. That is "incremental" in the sum, but every cap is tested on the post-add totals, never on the add leg alone.

- **Risk after the add** (pure helper `_PortAdd.RiskAfter`, unit-tested). Inputs: `pos` (signed), `avgPx`, the add's entry `e` (pending order price; market order = last close), `nAdd`, `addStop`, `bpv`.
  - `avgNew = (avgPx × |pos| + e × nAdd) / (|pos| + nAdd)`.
  - `riskAfter = _PortArm.RiskOpen(sign × (|pos| + nAdd), avgNew, e, addStop, bpv)`. That is the **same formula** the publisher uses for `pm.riskOpen` (`_PortMM` L473–478): |price − stop| × BPV × |pos|, priced at the add's entry `e`. It is 0 when `addStop` is at or beyond the new break-even `avgNew`, and never negative. So the post-add number the MM tests is what `pm.riskOpen` will publish right after the fill.
  - Widened: when the publisher sends `widenedAddStop` (cost floor), use it as `addStop` for every cap (P1 rule: caps use the widened risk).
- **Released risk.** `rel = s.riskOpen` (the position's `pm.riskOpen` now). Then `Δ = riskAfter − rel`, which may be ≤ 0 when the raised stop more than pays for the add.
- **Checks, in order** (gates as §5.2, then the caps; skip only, never resize):
  1. **Per-position budget:** `riskAfter > maxTradeRisk (0.01) × book` ⇒ skip `"add per-position budget"`. This is the owner rule "total open risk of the position ≤ 1% of equity" (overview §7.7, chart side). It is the add analogue of the "per-trade budget"; the add leg alone is never tested against 1%.
  2. **Heat:** `heat − rel + riskAfter > heatCap × book` ⇒ `"heat cap"`.
  3. **Cluster:** the cluster is occupied by the position itself, so the cluster cap is **always** checked for an add: `clHeat[c] − rel + riskAfter > clusterCap × book` ⇒ `"cluster cap"`. Under Starter (0.75%), an ES add with NQ open in the merged EQIDX cluster will usually be denied. That is intended ("one position per merged cluster").
  4. **Total risk cap:** `_PortRiskCap.Breaks(total − rel, riskAfter, cap)` ⇒ `"total risk cap"`.
  5. If allowed: `heat = heat − rel + riskAfter`, same for `clHeat[c]` and `total`. `open` and `clCount` do **not** change, because an add is not a new position (same as a reversal).
- **RiskParity at 0.5% applies to the initial entry only.** An add does not get a fresh `riskPerTrade` budget. Its size comes from §4, and its risk is bounded only by the post-add caps above. Real risk per position is therefore 0.5% (initial, structural) up to 1% (per-position budget).
- **R units.** The kill / session / weekly R stream keeps the **initial** 1R as its unit (`riskAtOpen` = `pm.R$ × |position at open|`, `_PortMM` L963–967, unchanged). Adds raise the $ at risk but not the R denominator, so a stop-out after adds can record more than −1R.
  - This is the strategy's own convention ("R for reporting stays the initial position's R", overview §7.7).
  - PROPOSED, open question Q3.

## 4. Add size, `MaxAdds` and `EntryContracts`

- **Who owns `MaxAdds`.**
  - The **strategy** owns the method: `AddMode`, `MaxAdds`, `AddContracts`, `AddMinR` and `AddStopMode` are its `[Input]`s, and it never arms add k > `MaxAdds`.
  - The **MM** enforces a book-level backstop, `maxAddsPerPosition` (config, default 2, PROPOSED, range 0–4). It counts add **fills** per position from the view: |pos| grows in the same sign between two evaluations while `pm.add ≥ 1` was approved. The count resets when the position goes flat or flips. Partial exits do **not** reset it, so a position can never take more than `maxAddsPerPosition` add fills.
  - Add k > backstop ⇒ skip `"max adds"`. If `pm.add` disagrees with the MM's own count, the MM count wins and one Warning per position is logged.
- **Add size** (`addSizing`, PROPOSED, typed enum `EAddSizing`):
  - `ScaleWithInitial` (default under RiskParity / VolTarget): `nAdd = floor(nInitial × qtyAdd / qtyCodeInitial)`. Here `nInitial` is the contracts the MM approved for the initial entry, remembered per position, and `qtyCodeInitial` is the publisher's code qty at that arm. Example: initial `qtyCode` 2 → n 20, `AddContracts` 1 ⇒ nAdd 10. `nAdd < minContracts` ⇒ skip `"size < minContracts"`.
  - `CodeQty`: `nAdd = qtyAdd` as published.
  - Under `CodeSize` sizing, the strategy's own qty applies (`EntryContracts = −1`), whatever `addSizing` says.
- **`EntryContracts` semantics for an add leg** (Check 7 L2 verified the base semantics):
  - **−1**: CodeSize. The add fills the strategy's coded add qty (`AddContracts`). The MM writes −1, as for every CodeSize entry.
  - **0**: never written. It blocks every entry (L2).
  - **> 0**: RiskParity / VolTarget. The MM writes `nAdd` on the step it approves the add. Because `EntryContracts > 0` applies to **every** entry order of the strategy (m-port-spec §6), a strategy may have **one** pending entry order per bar when it publishes an add (its initial entry is already filled). That is a publisher rule for `portfolioArmAdd`, and it must be checked natively (§9 A2).
  - On the following steps, deny-all writes −1 again. Any resting add order is re-sent each bar (m-port-spec §6), so it is re-approved or denied each step.
- **MC Position Limits.** The PT strategy's Strategy Properties → Position Limits must allow ≥ 1 + `maxAddsPerPosition` entries in the same direction. The exact option name must be checked in the installed build (VERIFY, §9 A1). Otherwise MC drops the add after the MM approved it. The existing `fillCheckEvals` Warning is extended to adds (§6) so this is visible.

## 5. T1 split, partial exits and reversals

- **T1 split (strategy side, unchanged).** T1 and the split rule use the **initial** contracts only: 2 contracts ⇒ 1 + 1 (overview §7.2, §7.7). Adds join the runner and leave with it.
  - The strategy names exits `From Entry` per leg so a T1 exit never sells add contracts (VERIFY natively, §9 A4).
  - The MM is indifferent. It sees only |pos| and `pm.riskOpen`, and after T1 fills both shrink.
  - Under `ScaleWithInitial`, `nInitial` is the approved initial n, not the post-T1 position.
- **Partial exit (T1 or any scale-out).**
  - It is not a closed trade (rev 2.4 R-stream rule, unchanged).
  - The add count is kept.
  - A later add is checked against the smaller current `riskOpen` (`rel`) and the smaller |pos| in `RiskAfter`.
- **Reversal while adds are open.**
  - It stays a rev 2.5 reversal: `rel` = the whole position's `pm.riskOpen`, all legs. MC's opposite entry closes every leg (VERIFY, §9 A6).
  - The R stream closes the trade at the flip in initial-1R units.
  - The add count, `nInitial` and `qtyCodeInitial` reset.
  - A reversal is never an add, and an add is never a reversal (§2.1 point 1).
- **Kill / daily flatten.** `ForceClosePosition` closes all legs, at the close of bar T (L3). `Paused` only once flat (L4). Unchanged.

## 6. Gates for an add and logging

**Gate order (proposed).** An add passes the same per-strategy gates as an initial entry, in the §5.2 order:
1. daily stop / dd mult 0, kill, session stop (4a), weekly observe (4b);
2. **`maxPositions` not checked** (not a new position);
3. **throttle:** any multiplier < 1 (ddSteps stage or weekly halve) ⇒ skip `"throttle: no adds"`, under every sizing mode. PROPOSED, Q4. The reason: no pyramiding while the book or the strategy is being cut back. A multiplier < 1 does **not** shrink `nAdd`.
4. `"max adds"`, then sizing (§4), then the caps (§3).

**Journal lines** (all Info, through the existing deduped `skip()`, re-logged when the reason changes or clears):
- Allowed: `PORT Info: add <id>#<i> leg <k>/<max> <long|short> allowed: pos <pos> n <nAdd> avg <avgPx> -> <avgNew> stop <old> -> <addStop> riskOpen $<rel> after $<riskAfter> delta $<Δ> heat $<heat> cap $<heatCap> cluster $<cl> cap $<clCap> total $<total> cap $<totalCap>`.
- Skips: `PORT Info: skip <id>#<i> <reason>: leg <k>/<max> n <nAdd> riskOpen $<rel> after $<riskAfter> <cap name> $<sum> cap $<cap>`. Reasons:
  - `add per-position budget`, `heat cap`, `cluster cap`, `total risk cap`, `max adds`;
  - `throttle: no adds`, `size < minContracts`;
  - `add: no addStop`, `add: no avgPx`, `add: stale arm` (`armStamp != stamp`).
- Fill: `PORT Info: add filled <id>#<i> leg <k> pos <old> -> <new>`. Unfilled: the `fillCheckEvals` Warning gains an add form, `… add leg <k> approved (qty <n>) but position did not grow after <k> MM evaluations - check Strategy Properties Position Limits (same-direction entries) and PT 'Max % Capital at Risk per Position' >= 1%`.
- `Status`: `… n=<nAdd> … add <k>/<max>` while an add is pending or allowed. With `Refuse`, `Status` is unchanged.
- `RUN` line: append `adds(<policy> max <maxAddsPerPosition> size <addSizing>)` **only when `addPolicy != Refuse`**, so the `Refuse` RUN line is byte-identical (§8).
- With `Refuse`, a same-direction arm keeps today's silent refusal (`"Long"`/`"Short"`/`"in position"`). No new line is printed.

## 7. Configuration (typed enums, programme rule 9)

These are `PortMmConfig` fields plus JSON keys, like every other MM setting (m-port-spec §0: the MM's only `[Input]` is `TotalRiskCapPct`). They are not `[Input]`s, because a PT run that wants adds sets them in the JSON file or in a subclass `Config`. Whether `AddPolicy` should instead be a second `[Input]` exception is open question Q1.

```csharp
public enum EAddPolicy { Refuse, AllowWithinCap }          // default Refuse: behaviour unchanged
public enum EAddSizing { ScaleWithInitial, CodeQty }       // default ScaleWithInitial (ignored under CodeSize)
public enum EAddThrottle { DenyWhenThrottled }             // one member now; room for a later ScaleWithMult (Q4)
// PortMmConfig
public EAddPolicy addPolicy = EAddPolicy.Refuse;
public int maxAddsPerPosition = 2;                         // 0..4; 0 with AllowWithinCap = adds refused, journalled as "max adds"
public EAddSizing addSizing = EAddSizing.ScaleWithInitial;
public EAddThrottle addThrottle = EAddThrottle.DenyWhenThrottled;
```

- **JSON:** `"addPolicy": "AllowWithinCap"`, `"maxAddsPerPosition": 2`, `"addSizing": "CodeQty"`, `"addThrottle": "DenyWhenThrottled"`. Enum values are parsed by member name, case-insensitive, via `Enum.TryParse` with an `IsDefined` check. Integer codes are rejected (rule 9: no int codes). An unknown name logs a Warning and keeps the field default.
- **Validate():** `0 ≤ maxAddsPerPosition ≤ 4`.
- **Both profiles** get `Refuse`. Starter and ScaledBook differ only in their caps, which already bound the adds.
- **Samples:** the two sample JSON files are **not** changed. Their hash stays the same, and a missing key means `Refuse`.

## 8. Backward compatibility and regression gates

- **Default `Refuse` ⇒ no behaviour change.**
  - The candidate filter keeps L813/L814 verbatim on the `Refuse` branch.
  - No new journal line, Status text or RUN text.
  - `PortDecision` values are unchanged: `entryContracts` −1 for every in-position strategy, `allowEntries` false.
- **Publisher keys are additive.**
  - `pm.ver` stays 1.
  - `pm.add` = 0, `pm.armStamp` and `pm.avgPx` are written but unread under `Refuse`.
  - The old `portfolioArm` path is unchanged, apart from also writing `pm.armStamp`.
  - The `m_portStop` hazard (§1) stays as it is for `portfolioArm`. Only `portfolioArmAdd` avoids it.
- **Regression gates (all must pass before merge):**
  1. **G0** (`_TestGate0` canary) and the **DVP D9a** summary: the documented ES baselines (m-instr-spec rev 2.5.2 / 2.5.13). Native re-run on the MiniPC must give **unchanged hashes**. These are chart runs where M-PORT is inert, so this proves the publisher edits (new keys, `portfolioArmAdd`) did not touch the chart path.
  2. **PortMMTests:** every existing test passes unchanged, including T-P22's "same-direction add refused" checks (L515–519), which must keep their exact reasons. The count rises only by the new tests (§9.1).
  3. **PT replay with `Refuse`:** the Check 7 L7 portfolio (`@ES` 1m + `@NQ` 5m, Check7 L7.pws, ≤ 2024) and one real-strategy portfolio, run before and after. The `PORT` journal lines, Status texts and trade lists must be identical (diff = empty).
  4. Every other suite under `tests/` is unchanged (no shared code is edited beyond `_PortMM`, `_PortfolioRiskManager` and the `_CustomSignal` publish block).
  5. No csproj change in the spec PR. The implementation PR adds no new file, so no csproj edit is expected there either: `_PortAdd` lives in `_PortMM.Function.CS`.

## 9. Tests

### 9.1 Off-MC (`tests/PortMMTests`, new T-P28 … T-P36, PROPOSED)
Book 1M, Starter unless stated, ES BPV 50.
- **T-P28 policy off:**
  - `Refuse` with a same-direction arm with `pm.add` = 1 ⇒ refused `"Long"` / `"in position"`, silent, `entryContracts` −1. Identical decisions to a run without the new keys.
  - `AllowWithinCap` with `pm.add` = 0 ⇒ refused as before.
- **T-P29 allowed add:** long 10 @ 5000, stop 4995, price 5010 ⇒ `riskOpen` = 15 × 50 × 10 = 7 500.
  - Add 5 @ 5010 with `addStop` 5002 ⇒ avgNew 5003.33 (stop below it), riskAfter = 8 × 50 × 15 = 6 000. Allowed; heat falls by 1 500 (Δ = −1 500).
  - With `addStop` 5004 (at or beyond the new break-even 5003.33) ⇒ riskAfter 0, allowed.
  - The journal line is as §6. `entryContracts` = 5 under `CodeQty`.
- **T-P30 per-position budget:**
  - riskAfter 10 000 ⇒ allowed (boundary, `>` not `≥`).
  - 10 001 ⇒ `"add per-position budget"`, n never cut.
  - The add leg alone under 1% but the position over it ⇒ denied.
- **T-P31 caps with release:**
  - Heat: the position's `rel` is released, then riskAfter counted. Another strategy's open risk still counts.
  - Cluster: always checked (own position occupies it). An ES add with NQ open, EQIDX over 7 500 ⇒ `"cluster cap"`.
  - Total cap with a lowered `TotalRiskCapPct`.
- **T-P32 `maxAddsPerPosition`:**
  - Fills counted from |pos| growth. The third add with max 2 ⇒ `"max adds"`.
  - Partial exit keeps the count. Flat / flip resets it.
  - `pm.add` mismatch ⇒ MM count wins + one Warning.
  - 0 ⇒ every add `"max adds"`.
- **T-P33 sizing:**
  - `ScaleWithInitial`: initial qtyCode 2 → n 20, add qtyCode 1 ⇒ nAdd 10.
  - nAdd < minContracts ⇒ skip.
  - CodeSize ⇒ −1.
  - Never 0 written.
- **T-P34 gates:**
  - Daily stop, kill, session stop, weekly observe deny with their usual reasons.
  - ddSteps stage 1 or weekly halve ⇒ `"throttle: no adds"` (also under CodeSize).
  - `maxPositions` full ⇒ the add is still allowed (not a new position).
- **T-P35 identity:**
  - Stale arm (`armStamp != stamp`) ⇒ `"add: stale arm"`.
  - NaN `addStop` ⇒ `"add: no addStop"`.
  - NaN `avgPx` ⇒ `"add: no avgPx"`.
  - An opposite side with `pm.add` = 1 ⇒ treated as a reversal (4c), `pm.add` ignored.
  - `Halted` ⇒ never an add.
- **T-P36 config:**
  - JSON enum names parse case-insensitively. `"addPolicy": 1` (int) rejected with a Warning.
  - Unknown name ⇒ default.
  - `maxAddsPerPosition` 5 ⇒ Validate fails ⇒ `ForProfile` fallback (`config=fallback`).
  - RUN line unchanged under `Refuse`; `adds(...)` appended otherwise.
  - Publisher pure helpers: `_PortAdd.RiskAfter` long/short, at/after break-even, bad BPV ⇒ NaN ⇒ refused.

### 9.2 Native MiniPC checks (Check 7 style, new "Check 7A", backtest only, data ≤ 2024)
- **Signal.** A throw-away signal `_TestPortAdd : PortfolioSignalObject`, not derived from `_CustomSignal` (as `_TestPortArm`), with input `Test` typed `public enum EAddCheck { A1, A2, A3, A4, A5, A6, A7 }` (rule 9; `_TestPortArm` still uses a string).
- **Setup** as Check 7:
  - `@ES` 1m + `@NQ` 1m, dxFeed.
  - PT Exposure / Max % capital at risk 100% / 100%.
  - The MM under test is the implementation build with `"addPolicy": "AllowWithinCap"`.
  - Every line is prefixed `P5b Ax` and journals `ARM#i FILL` and `ARM#i STAMP` like `_TestPortArm`.

| # | Question | How | Pass | If not |
|---|---|---|---|---|
| A1 | Does PT fill a same-direction add, and which Strategy Properties Position Limits option allows it? | Buy 1 at bar 10, buy 1 more ("LA1") at bar 20. Run with the limit set to 1 entry, then to "allow up to N entry orders in the same direction" with N = 3 | No add fill with limit 1. With N = 3 the position goes 1 → 2. Record the exact option name | Adds cannot run in PT; keep `AddMode = None` |
| A2 | `EntryContracts > 0` on the add step sizes only the add | MM writes 4 on the add step, −1 otherwise | Add fills 4 and the initial stays 1. If the strategy also sends a second entry the same bar, both get 4 (documents the one-pending-entry rule) | Use CodeSize for adds (§4) |
| A3 | A resting add stop order, re-sent each bar, is sized by the `EntryContracts` of its fill step | Add stop above market for 5 bars; MM writes 2, 3, 3, 3, 3 | Fill qty = that step's value | Publisher must re-arm every bar (already the rule) |
| A4 | T1 `From Entry` exit sells initial contracts only | Initial 2 + add 1; exit 1 "From Entry" initial at bar 30 | Position 3 → 2; the add leg stays | Spec the T1 exit by qty instead of name |
| A5 | `CurrentPosition`, `PositionSide`, `AvgEntryPrice` and `pm.pos` after an add, on the same step the MM reads | Journal them each bar | Updated on the fill bar's next MM step; avg = qty-weighted | Delay the add-fill counter by one step |
| A6 | Reversal with adds open | Long 2 + add 1, then `SellShort` 1 | All 3 longs closed, short 1 opened; one reversal | Reversal `rel` must sum legs by fills |
| A7 | `ForceClosePosition` with adds open | Long 2 + add 1, MM force-closes | All legs flat at the close of bar T (as L3) | Kill path must close per leg |

Results are recorded in `specs/live-check-sheet.md` (new Check 7A) and m-port-spec §9 (A1–A7). Paper / live sharing (L6) and the roll (L9) are not affected.

## 10. Build steps (for the implementation PR, after approval)
1. `_PortMM.Function.CS`:
   - the enums and fields from §7, plus their JSON parse;
   - `_PortAdd` (pure: `RiskAfter`, fill counter);
   - the add branch in `Decide` next to the reversal branch: `isAdd(s)`, the gates, the caps with release (§3);
   - add fill tracking in `checkFills`;
   - the journal lines.
2. `_PortfolioRiskManager.view`: read `pm.add`, `pm.addStop`, `pm.avgPx`, `pm.bpv`, `pm.armStamp` into new `PortStratView` fields.
3. `_CustomSignal`:
   - `portfolioArmAdd`;
   - `pm.add` reset with `pm.side` (L1796), `pm.avgPx`, `pm.bpv`, `pm.armStamp`;
   - the warning for a same-direction `portfolioArm`.
   - No `[Input]`.
4. Tests §9.1. Then the regression gates §8 (G0, D9a, PortMMTests, `Refuse` PT replay). Then Check 7A §9.2.
5. Specs: m-port-spec rev 2.8 (§3 keys, §5.2 pseudo-code, §5.4 4d "Add legs", §6, §9 A1–A7, §11). Overview §7.7 on PR #117: lift "PT runs use `AddMode = None`" once A1–A7 pass and the owner approves.

## 11. Open questions (owner or Analyst)
- **Q1** Where does `addPolicy` live? Config/JSON (this draft, consistent with §0), or a second MM `[Input]` (typed `EAddPolicy`) so it shows in the PT dialog and can be swept? PROPOSED: config.
- **Q2** Per-position budget = `maxTradeRisk` (1%, the same number as the chart rule), or a separate `addPositionRiskCap` ≤ 1%? PROPOSED: reuse `maxTradeRisk`.
- **Q3** R units after adds: keep the initial 1R (this draft; a full stop-out after adds can be < −1R and counts more heavily in kill / session / weekly R), or re-base `riskAtOpen` to the post-add risk? PROPOSED: initial 1R, with the post-add risk journalled.
- **Q4** Under a multiplier < 1 (ddSteps stage, weekly halve): deny adds (this draft), or allow with `nAdd × mult`? PROPOSED: deny.
- **Q5** Add sizing under RiskParity: `ScaleWithInitial` (keeps the strategy's add ratio at book scale), or `CodeQty`? PROPOSED: `ScaleWithInitial`.
- **Q6** `maxAddsPerPosition` default 2, matching YV-11's `MaxAdds` 2 and the overview's 2 + 2 × 1 = 4? Any lower Starter value?
- **Q7** Cluster cap: Starter 0.75% will usually deny an ES add while NQ is open, and an ES add alone once the ES position carries more than about 0.75% after the add. Accept as "one position per merged cluster", or let an add be checked only against the per-position 1%? PROPOSED: accept, the cluster cap applies.
- **Q8** Should `fillCheckEvals` also warn on an approved add that never fills (VERIFY that it cannot false-alarm on a resting add stop that does not trigger; the "likely" wording is kept)?
- **Q9** The MC Position Limits option name and behaviour in PT (A1) are unverified, and so is whether `From Entry` exits behave in PT as on charts (A4).
