---
title: "M-PORT add legs: let the portfolio risk manager approve same-direction adds within the risk caps (DRAFT spec, proposed rev 2.8)"
date: 2026-11-10 (ET); draft r9 2026-11-11 (ET)
status: "DRAFT, docs only, no code. Requested by Ettienne Hugo 2026-11-10 22:16 ET: \"Yes, spec the Portfolio risk manager change as a separate PR.\" Owner answers 2026-11-10 22:35 ET applied (AddPolicy as an Input; R re-based after an add; adds blocked under a throttle with a Warning). 2026-11-11 short-side check (Ettienne 09:36 ET: \"Please review the strategies thoroughly and make sure that where shorts are involved, we have clarity around handling the short scenario's.\"): short adds stated explicitly in §2.2/§3/§3a and T-P37; no rule changed. Draft r3: cluster cap applies to adds, owner-approved 2026-11-11 10:13 ET (§3 check 3, Q7 closed); `ClusterCapPct` [Input] overriding the profile cluster cap, owner-approved 2026-11-11 10:14 ET (§7). Draft r4: Q2/Q5/Q6/Q8 answered 2026-11-11 10:28 ET (per-position 1% at the new stop, `ScaleWithInitial`, `maxAddsPerPosition` 2, add fill-check Warning); Q10 closed. Draft r6: the r5 per-instrument cap (`MaxInstrumentRiskPct`) was dropped by the owner 2026-11-11 10:52 ET; `ClusterCapPct` covers it (§7). Draft r7: Q12 owner-approved 2026-11-11 11:09 ET (heat cap auto-raised to a higher `ClusterCapPct`); Q13 open (total cap). Draft r8: Q13 owner-approved 2026-11-11 13:29 ET: the effective total risk cap is also raised to a higher `ClusterCapPct` (config field + Info line replace the 'binds first' Warning; T-P39). Draft r9: Check 7A native results recorded 2026-11-11 (§9.2; A1–A7 pass, A0 pass on a scratch MM except the enum sweep, A8 proxy pass on ES; Q9 partly closed); no rule changed. Not approved for implementation as a whole. Everything not quoted from the owner is PROPOSED."
code_refs: "line refs are against origin/main @ 47ffda2 (2026-11-10, merge of PR #116, Check 7 findings)"
related: "specs/m-port-spec.md rev 2.7 (§5.2, §5.4 4c, §6, §9); specs/live-check-sheet.md Check 7 (L1–L9); PR #117 specs/strategies/00-programme-overview.md §7.7 (strategy-side add inputs)"
---

# M-PORT add legs (DRAFT r9, proposed rev 2.8)

## 0. Why, and scope

- **Owner answers 2026-11-10 22:35 ET** (verbatim): *"1. An Input on the risk manager. 2. No, the stop in general would move for the second entry so the risk calculation should be adjusted. 3. Adds should be blocked, issuing a warning log entry why it was blocked. 4. Explain this to me"*.
  - Answer 1 → §7: `AddPolicy` is an `[Input]`.
  - Answer 2 → §3a: R is re-based after an add.
  - Answer 3 → §6: adds are blocked under any throttle, with a Warning.
  - 10:28 ET answers: **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*) → Q2 §3 check 1, Q5 §4 `addSizing`, Q6 §4 `maxAddsPerPosition`, Q8 §6 add fill-check Warning.
  - **Per-instrument cap dropped (2026-11-11 10:52 ET):** Ettienne 2026-11-11 10:52 ET: *"Ignore the 8% per instrument, that should rather be on the cluster cap as a configurable input, I think we already have that."* The r5 `MaxInstrumentRiskPct` Input (owner request 10:30 ET) and everything added for it were reverted; the configurable cluster cap `ClusterCapPct` (§7) is the way to set e.g. 8%.
  - Cluster-cap override: **Owner-approved 2026-11-11 10:14 ET** (Ettienne chose: *"Yes, add ClusterCapPct as an Input that overrides the profile"*). → §7 `ClusterCapPct`.
  - Answer 4 (the cluster cap, Q7) was explained to him; **Owner-approved 2026-11-11 10:13 ET** (Ettienne chose the option *"Apply the cluster cap to adds"*). → §3 check 3.
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
5. The `AddPolicy` `[Input]` (§7) is `AllowWithinCap`. With `Refuse`, points 1–4 are not evaluated and L813/L814 run exactly as today.

### 2.2 Publisher keys and a separate arm call (PROPOSED)
These are additive optional keys, so **`pm.ver` stays 1**: the same rule as `pm.R$w` and `pm.regimeP`, and readers pull by name. Same-build deploy, as for rev 2.

| Key | Type | Written | Meaning |
|---|---|---|---|
| `pm.add` | int | every bar | 0 = no add pending (the default, reset in `portfolioBar` with `pm.side`); k ≥ 1 = the pending entry is the k-th add of the open position |
| `pm.addStop` | double | with `pm.add` | the **whole-position** stop after the add fills: `RaiseToAddLow` = add structural low − 1 tick (long; the stop is raised) or add structural **high + 1 tick (short; the stop is lowered)**, `KeepCurrent` = the current active stop. NaN ⇒ the MM refuses ("add: no addStop") |
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
- **Released risk.** `rel = s.riskOpen` (the position's `pm.riskOpen` now). Then `Δ = riskAfter − rel`, which may be ≤ 0 when the tightened stop (raised for a long, lowered for a short) more than pays for the add.
- **Checks, in order** (gates as §5.2, then the caps; skip only, never resize):
  1. **Per-position budget** (**Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*), answer 1): `riskAfter > maxTradeRisk (0.01) × book` ⇒ skip `"add per-position budget"`. `riskAfter` is the **whole post-add position** (all contracts, initial + earlier adds + this add) priced to the **new whole-position stop the add sets** (`pm.addStop`, i.e. the raised stop for a long / lowered stop for a short; with `KeepCurrent`, the current stop) — never to the original initial stop and never on the add leg alone. Widened (`widenedAddStop`) when sent. This is the owner rule "total open risk of the position ≤ 1% of equity" (overview §7.7, chart side). It is the add analogue of the "per-trade budget"; the add leg alone is never tested against 1%.
  2. **Heat:** `heat − rel + riskAfter > heatCap × book` ⇒ `"heat cap"`.
  3. **Cluster:** the cluster is occupied by the position itself, so the cluster cap is **always** checked for an add: `clHeat[c] − rel + riskAfter > clusterCap × book` ⇒ `"cluster cap"`. Under Starter (0.75%), an ES add with NQ open in the merged EQIDX cluster will usually be denied. That is intended ("one position per merged cluster"). **Owner-approved 2026-11-11 10:13 ET** (Ettienne chose the option *"Apply the cluster cap to adds"*): the post-add cluster risk must stay within the profile's cluster cap (Starter 0.75% of book); on a breach the add is **skipped, never resized**, and the skip line gives the reason (`PORT Info: skip <id>#<i> cluster cap: leg <k>/<max> n <nAdd> riskOpen $<rel> after $<riskAfter> cluster $<sum> cap $<cap>`, §6). The cap is `cfg.clusterCap` after the `ClusterCapPct` Input override (§7).
  4. **Total risk cap:** `_PortRiskCap.Breaks(total − rel, riskAfter, cap)` ⇒ `"total risk cap"`.
  5. If allowed: `heat = heat − rel + riskAfter`, same for `clHeat[c]` and `total`. `open` and `clCount` do **not** change, because an add is not a new position (same as a reversal).
- **RiskParity at 0.5% applies to the initial entry only.** An add does not get a fresh `riskPerTrade` budget. Its size comes from §4, and its risk is bounded only by the post-add caps above. Real risk per position is therefore 0.5% (initial, structural) up to 1% (per-position budget).
- **R units:** see §3a. R is re-based on the post-add position (**Owner-approved 2026-11-10 22:35 ET**).

### 3a. R re-base after an add (**Owner-approved 2026-11-10 22:35 ET**)

Ettienne: *"the stop in general would move for the second entry so the risk calculation should be adjusted."* After an add, 1R becomes the risk of the **adjusted** position: all contracts, at the stop the add set. A stop-out after adds is then measured against that new 1R, so it is about −1R, not −2R or worse, in the kill (§5.4 4), session (4a) and weekly (4b) counts.

- **When.** On the **add fill**, detected by the MM in `updateKills` (`_PortMM` L942–970) at the first evaluation where |`pm.pos`| has grown in the same sign while an approved add (`pm.add` ≥ 1) was pending.
  - Fills inside one evaluation are seen once: the existing sampling limit, rev 2.4.
  - If the position both grows and shrinks between two samples, the MM sees only the net change. A net growth counts as an add fill; a net shrink counts as a partial exit.
  - The re-base never happens on the arm, the approval, or a fill that is not an add.
- **New 1R.** `R1 = max(0, side × (px − stopNew)) × bpv × |posNew|`:
  - `px` = new key `pm.px` = `Bars.Close[0]` at the fill sample;
  - `stopNew` = the whole-position stop after the add: `pm.addStop` from the approval, which the strategy also sets via `portfolioStop`;
  - `posNew` = the new |`pm.pos`|.
  - This is the "full post-add risk": what `pm.riskOpen` shows right after the fill, without `RiskOpen`'s break-even zeroing, so it stays positive whenever the stop is below the price (long) or above it (short): `side` = −1 gives `R1 = max(0, stopNew − px) × bpv × |posNew|` for a short.
  - If `R1 ≤ 0` or NaN (stop at or through the price, bad BPV), the MM does **not** re-base. The current segment carries on with its old 1R, and a `PORT Warning` names the reason.
- **Trade R as segments.**
  - A trade is split at each re-base into segments: 0 is the initial position, 1 runs after add 1, 2 after add 2.
  - Segment j has its own 1R (`R0` = `riskAtOpen` as today = `pm.R$ × |position at open|`; `Rj` from the rule above) and its P&L, `pnl` at its end minus `pnl` at its start. The MM's `pnl` is closed + open (`pm.pnl`), so a re-base marks the open position to market.
  - **Trade R** = Σ_j (segment P&L_j / R_j). This one number goes into the kill queue, the session-loss count (with the `scratchR` band) and the weekly sum, so everything downstream is unchanged.
  - Example: initial long reaches +1.2R, then the add, then a stop-out at the raised stop. That gives R = +1.2 + (about −1.0) ≈ +0.2R, not −1.8R in initial units.
  - The R stream needs no other change.
- **Partial exits.**
  - A partial exit (T1, any scale-out) **before** an add realises its P&L inside segment 0, measured in R0.
  - A partial exit **after** an add realises its P&L inside the current segment, measured in that segment's Rj. The segment's 1R is **not** shrunk when contracts leave: R is frozen per segment, as the initial R is today.
  - A partial exit never starts a segment and never closes the trade (rev 2.4 rule).
  - Example: 2 contracts, T1 one contract at +1R0 in segment 0, then add 1, then a stop-out in segment 1 ⇒ R = (T1 P&L + open P&L of the runner at the add) / R0 + segment-1 P&L / R1.
- **Reversal and flat.**
  - The trade closes as today (position → 0, or a sign flip at a reversal), summing every segment.
  - The new trade after a flip starts with a new segment 0.
  - A reversal never re-bases.
- **Original R kept for audit.** The MM also keeps **R(original)** = total trade P&L / R0, today's single-denominator number. It is journalled only, never used by any throttle.
- **Publisher side.**
  - `pm.openR` keeps the strategy's own unit.
  - After an add fill, the strategy's `portfolioStop(addStop)` call and a new `portfolioRebaseR()` step (in `portfolioArmAdd`'s fill path) set `m_portRDollar` so that `pm.openR` uses `R1 / |posNew|`. Chart and Portfolio Trader then report the same re-based R.
  - **Consequence for PR #117 overview §7.7:** its line "R for reporting stays the initial position's R" conflicts with this answer. It needs the same re-base on the chart side. That is a separate edit on PR #117, not made here.
- **Journal lines (Info):**
  - On re-base: `PORT Info: R rebase <id>#<i> leg <k> at <bar time>: pos <old> -> <new> avg <avgPx> stop <stopOld> -> <stopNew> px <px> 1R $<R_prev> -> $<R1> segR(prev) <x.xx> tradeR(rebased) <y.yy> tradeR(original) <z.zz>`.
  - On trade close, only when the trade had ≥ 1 re-base: `PORT Info: trade R <id>#<i> closed at <bar time>: R(rebased) <r> R(original 1R) <r0> segments <n> [<segR_0> @ $<R0>, <segR_1> @ $<R1>, …]`.
  - **Planned vs actual risk % (owner request 2026-11-11 09:19 ET; definitions in PR #119 `specs/generic-exit-manager-spec.md` §5.3):** the PORT close line also carries `plannedRiskPct`, `actualRiskPct` and `actualToPlanned`. They are read from optional publisher keys `pm.riskPctPlan` / `pm.riskPctAct`, written by the strategy on the close bar (additive, `pm.ver` unchanged). The MM never computes MAE.
  - The existing `PORT kill …`, `session stop` and `weekly …` lines quote R(rebased). They append `(original <r0>)` only when the trade had a re-base, so every line for a trade without adds is byte-identical.
  - Skipped re-base: `PORT Warning: R rebase skipped <id>#<i> leg <k>: 1R $<R1> not positive (px <px> stop <stopNew>) - segment keeps 1R $<R_prev>`.

## 4. Add size, `MaxAdds` and `EntryContracts`

- **Who owns `MaxAdds`.**
  - The **strategy** owns the method: `AddMode`, `MaxAdds`, `AddContracts`, `AddMinR` and `AddStopMode` are its `[Input]`s, and it never arms add k > `MaxAdds`.
  - The **MM** enforces a book-level backstop, `maxAddsPerPosition` (config, default 2 for Starter and ScaledBook, **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*), answer 3; range 0–4). It counts add **fills** per position from the view: |pos| grows in the same sign between two evaluations while `pm.add ≥ 1` was approved. The count resets when the position goes flat or flips. Partial exits do **not** reset it, so a position can never take more than `maxAddsPerPosition` add fills.
  - Add k > backstop ⇒ skip `"max adds"`. If `pm.add` disagrees with the MM's own count, the MM count wins and one Warning per position is logged.
- **Add size** (`addSizing`, typed enum `EAddSizing`; default `ScaleWithInitial` **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*), answer 2):
  - `ScaleWithInitial` (default under RiskParity / VolTarget): `nAdd = floor(nInitial × qtyAdd / qtyCodeInitial)`. Here `nInitial` is the contracts the MM approved for the initial entry, remembered per position, and `qtyCodeInitial` is the publisher's code qty at that arm. Example: initial `qtyCode` 2 → n 20, `AddContracts` 1 ⇒ nAdd 10. `nAdd < minContracts` ⇒ skip `"size < minContracts"`.
  - `CodeQty`: `nAdd = qtyAdd` as published.
  - Under `CodeSize` sizing, the strategy's own qty applies (`EntryContracts = −1`), whatever `addSizing` says.
- **`EntryContracts` semantics for an add leg** (Check 7 L2 verified the base semantics):
  - **−1**: CodeSize. The add fills the strategy's coded add qty (`AddContracts`). The MM writes −1, as for every CodeSize entry.
  - **0**: never written. It blocks every entry (L2).
  - **> 0**: RiskParity / VolTarget. The MM writes `nAdd` on the step it approves the add. Because `EntryContracts > 0` applies to **every** entry order of the strategy (m-port-spec §6), a strategy may have **one** pending entry order per bar when it publishes an add (its initial entry is already filled). That is a publisher rule for `portfolioArmAdd`. **Native check A2 (2026-11-11):** confirmed: with `EntryContracts = 4` on the add step the add filled 4 and the initial stayed 1, and a second entry sent on the same step also filled 4 (Check 7A).
  - On the following steps, deny-all writes −1 again. Any resting add order is re-sent each bar (m-port-spec §6), so it is re-approved or denied each step. **Native check A3 (2026-11-11):** a resting add stop fills with the `EntryContracts` of the step that sent the filling order (2 on send 1, 3 on a later send), so re-arming every bar is required and sufficient.
- **MC Position Limits.** The PT strategy's Strategy Properties → Position Limits must allow ≥ 1 + `maxAddsPerPosition` entries in the same direction. Otherwise MC drops the add after the MM approved it. **Native check A1 (2026-11-11), installed PT wording with N = 3:** checkbox *"Allow up to 3 entry orders in the same direction as the currently held position:"* with radio *"when the order is generated by a different entry order"* or *"regardless of the entry that generated the order"*. Unchecked: no add fills. Checked + "different entry order": an add with its own order name (`<ID>LA1`) fills; re-sending the initial entry's order name does not. "regardless of the entry …": the same-name add fills too. Per-leg names (overview §7.7) therefore work with "different entry order"; set N ≥ 1 + `maxAddsPerPosition`. The existing `fillCheckEvals` Warning is extended to adds (§6) so this is visible.

## 5. T1 split, partial exits and reversals

- **T1 split (strategy side, unchanged).** T1 and the split rule use the **initial** contracts only: 2 contracts ⇒ 1 + 1 (overview §7.2, §7.7). Adds join the runner and leave with it.
  - The strategy names exits `From Entry` per leg so a T1 exit never sells add contracts. **Native check A4 (2026-11-11):** in PT, `OrderExit.FromEntry(initial)` qty 1 took 3 → 2 and closed only the initial leg; `FromEntry(add)` closed only the add leg. Same as on charts.
  - The MM is indifferent. It sees only |pos| and `pm.riskOpen`, and after T1 fills both shrink.
  - Under `ScaleWithInitial`, `nInitial` is the approved initial n, not the post-T1 position.
- **Partial exit (T1 or any scale-out).**
  - It is not a closed trade (rev 2.4 R-stream rule, unchanged). Its P&L counts in the current R segment (§3a).
  - The add count is kept.
  - A later add is checked against the smaller current `riskOpen` (`rel`) and the smaller |pos| in `RiskAfter`.
- **Reversal while adds are open.**
  - It stays a rev 2.5 reversal: `rel` = the whole position's `pm.riskOpen`, all legs. MC's opposite entry closes every leg. **Native check A6 (2026-11-11):** long 2 + add 1, then `SellShort` 1 ⇒ one fill 3 → −1 that closed both legs.
  - The R stream closes the trade at the flip as the sum of its segments (§3a). R(original) is journalled.
  - The add count, `nInitial` and `qtyCodeInitial` reset.
  - A reversal is never an add, and an add is never a reversal (§2.1 point 1).
- **Kill / daily flatten.** `ForceClosePosition` closes all legs, at the close of bar T (L3; **native check A7 2026-11-11** with an add open: both legs closed at T's close). `Paused` only once flat (L4). Unchanged.

## 6. Gates for an add and logging

**Gate order (proposed).** An add passes the same per-strategy gates as an initial entry, in the §5.2 order:
1. daily stop / dd mult 0, kill, session stop (4a), weekly observe (4b);
2. **`maxPositions` not checked** (not a new position);
3. **Throttle (**Owner-approved 2026-11-10 22:35 ET**: *"Adds should be blocked, issuing a warning log entry why it was blocked"*).**
   - Whenever the combined throttle multiplier `m = ddMult × sMult` is < 1, the add is **blocked**, never scaled down. That covers a ddSteps stage, the weekly halve, both together, and the 0 cases (daily stop, dd stage 0, session stop, weekly observe).
   - It applies under every sizing mode, CodeSize included.
   - The block is written at **Warning** (below), not through the Info skip line.
   - The book or strategy loss gates (daily stop, kill, session stop, weekly observe) keep their usual reason for **initial** entries. For an add, the same condition produces the `ADD-BLOCKED` Warning.
4. `"max adds"`, then sizing (§4), then the caps (§3).

**`ADD-BLOCKED` Warning (throttles only).**
- Format: `PORT Warning: ADD-BLOCKED reason=<ddStep|weeklyHalve|ddStep+weeklyHalve|dailyStop|ddStop|sessionStop|weeklyObserve> mult=<m> strat=<id>#<i> side=<long|short> leg=<k>/<max> dd=<dd%> ddStage=<s> weekR=<r> pos=<pos> n=<nAdd or qtyCode>`.
  - Example: `PORT Warning: ADD-BLOCKED reason=ddStep mult=0.5 strat=YV-11@ES#0 side=long leg=1/2 dd=3.4% ddStage=1 weekR=-0.8 pos=2 n=1`.
  - `Status` shows `ADD-BLOCKED <reason>`.
- **Why Warning, against the skip-logging rule.**
  - m-port-spec §5.4 says every skipped entry is logged at **Info**. This line is an explicit owner exception (22:35 ET), like the existing Warning exceptions (bad `BigPointValue`, `fillCheckEvals`).
  - Every other add skip stays Info (caps, max adds, identity). Initial-entry throttle skips stay Info.
- **How often it fires.** It uses the same per-strategy dedupe as `skip()` (`m_skipLogged`, `_PortMM` L745–752), with the key `ADD-BLOCKED|<reason>|<mult>|<leg>`. So it is written:
  - once when a pending add is first blocked;
  - again only when the reason, the multiplier or the leg changes, or after the block clears (an allowed or withdrawn add) and a new add is blocked.
  - A strategy re-sending the same add every bar under the same throttle gives **one** line, not one per bar. The number of lines is bounded by the number of distinct add attempts.
  - `PORT observe` does not ride on this line; the weekly-observe state appears as `reason=weeklyObserve`.

**Journal lines** (Info unless stated, through the existing deduped `skip()`, re-logged when the reason changes or clears):
- Allowed: `PORT Info: add <id>#<i> leg <k>/<max> <long|short> allowed: pos <pos> n <nAdd> avg <avgPx> -> <avgNew> stop <old> -> <addStop> riskOpen $<rel> after $<riskAfter> delta $<Δ> heat $<heat> cap $<heatCap> cluster $<cl> cap $<clCap> total $<total> cap $<totalCap>`.
- Skips: `PORT Info: skip <id>#<i> <reason>: leg <k>/<max> n <nAdd> riskOpen $<rel> after $<riskAfter> <cap name> $<sum> cap $<cap>`. Reasons:
  - `add per-position budget`, `heat cap`, `cluster cap`, `total risk cap`, `max adds`;
  - `size < minContracts` (throttle blocks are the `ADD-BLOCKED` Warning above, not an Info skip);
  - `add: no addStop`, `add: no avgPx`, `add: stale arm` (`armStamp != stamp`).
- Fill: `PORT Info: add filled <id>#<i> leg <k> pos <old> -> <new>`. Unfilled (**Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*), answer 4): the `fillCheckEvals` Warning gains an add form:
  - Line: `PORT Warning: <id>#<i> add leg <k>/<max> <long|short> approved (qty <n>) but position did not grow after <evals> MM evaluations - check Strategy Properties Position Limits (same-direction entries) and PT 'Max % Capital at Risk per Position' >= 1%`.
  - Counting: an evaluation counts only while the add stays approved **and** should have filled: a market add, or a resting add whose entry e (§3) has been traded through (last close ≥ e for a long buy-stop, ≤ e for a short sell-stop). A resting add stop that price never reaches is not counted, so it cannot false-alarm. The count resets when the add fills, is denied or is withdrawn (`pm.add` = 0).
  - Trigger: count ≥ `fillCheckEvals` (existing config, default 3; 0 = off).
  - Dedupe: **once per (strategy, position, add leg)**: a key of strategy index + position serial (reset on flat/flip) + leg k, held in the same kind of set as the existing `m_fillWarned`. A second leg or a new position can warn again; the same leg never repeats. The existing initial-entry fill Warning is unchanged.
- `Status`: `… n=<nAdd> … add <k>/<max>` while an add is pending or allowed. With `Refuse`, `Status` is unchanged.
- `RUN` line: append `adds(<AddPolicy> max <maxAddsPerPosition> size <addSizing>)` **only when `AddPolicy != Refuse`**, so the `Refuse` RUN line is byte-identical (§8).
- With `Refuse`, a same-direction arm keeps today's silent refusal (`"Long"`/`"Short"`/`"in position"`). No new line is printed.

## 7. Configuration (typed enums, programme rule 9)

**`AddPolicy` is an `[Input]` on the risk manager (**Owner-approved 2026-11-10 22:35 ET**: *"An Input on the risk manager."*).**

- **What the MM reads today.**
  - `_PortfolioRiskManager` has exactly one `[Input]`, `TotalRiskCapPct` (`_PortfolioRiskManager.Strategy.CS` L24–28; m-port-spec §0, "an explicit exception Ettienne asked for").
  - Every other setting comes from `PortMmConfig`: the profile defaults (`Profile` / `Config` protected virtuals), then the optional `ConfigPath` JSON (`StartCalc` L44–64).
  - The Input is folded in **after** config resolution: `cfg = cfg.Copy(); cfg.totalRiskCap = _PortRiskCap.Fraction(TotalRiskCapPct, …)` (L59).
- **`AddPolicy` follows the same pattern**, as the second MM `[Input]` exception (owner-approved). m-port-spec §0 must say "two `[Input]`s" in rev 2.8.

```csharp
public enum EAddPolicy { Refuse, AllowWithinCap }          // top-level in _PortMM.Function.CS, next to EPortProfile
// _PortfolioRiskManager
/// <summary>Same-direction add legs (specs/m-port-add-legs-spec.md): Refuse (default) = rev 2.7 behaviour, adds refused;
/// AllowWithinCap = an add is approved only if the position's risk after the add fits every cap. Never scales an add.</summary>
[Input] public EAddPolicy AddPolicy { get; set; }
// ctor: AddPolicy = EAddPolicy.Refuse;
// StartCalc, after Resolve and next to totalRiskCap:  cfg.addPolicy = AddPolicy;
```

- **Interaction with config.**
  - `PortMmConfig.addPolicy` is the field the pure `_PortMM` reads, so the tests can set it. It is **set only from the Input**.
  - It is **not** a JSON key. A `"addPolicy"` key in a JSON file logs `PORT Warning: PortMmConfig: addPolicy is the AddPolicy [Input] on the risk manager; JSON value ignored` and is ignored. A subclass `Config` value is likewise overwritten by the Input.
  - The RUN line prints the effective value (§6).
  - `_PortfolioRiskManager_ScaledBook` inherits the Input.
  - The other add settings stay config/JSON. They are tuning, not an on/off policy: `maxAddsPerPosition`, and `addSizing` (typed enum `EAddSizing`, by name).
- **Dialog and sweeps.**
  - `AddPolicy` appears in the Portfolio Trader money-management signal's Format dialog, next to `TotalRiskCapPct`. **Native check A0 (2026-11-11, on a scratch MM with the same Input types):** the typed enum Input shows its named members (`Refuse` / `AllowWithinCap`) and switching it changed the run (Refuse: add refused, max position 1; AllowWithinCap: 2). Re-check the label on the implementation build.
  - **Whether the MC optimizer can sweep an enum `[Input]` is still unverified** (not tried in Check 7A A0). This is the same open check as PR #117 overview §9 ("Verify enum inputs on the MiniPC": named members in the Format dialog and Portfolio Trader; whether the optimizer can sweep them).
  - If sweeps are not supported, compare `Refuse` against `AllowWithinCap` as two saved backtests.
**`ClusterCapPct` is an `[Input]` that overrides the profile cluster cap (**Owner-approved 2026-11-11 10:14 ET** (Ettienne chose: *"Yes, add ClusterCapPct as an Input that overrides the profile"*)).**

```csharp
// _PortfolioRiskManager
/// <summary>Cluster risk cap in % of book. 0 (default) = use the profile / Config / JSON clusterCap; > 0 overrides it.
/// Applies to new entries and to add legs alike.</summary>
[Input] public double ClusterCapPct { get; set; }
// ctor: ClusterCapPct = 0;
// StartCalc, after Resolve, next to totalRiskCap and addPolicy:
//   string src = "profile";
//   string heatSrc = "profile", totalSrc = "input";   // cfg.totalRiskCap already set from TotalRiskCapPct (L59)
//   if (ClusterCapPct > 0 && ClusterCapPct <= 100) {
//     cfg.clusterCap = ClusterCapPct / 100; src = "input";
//     if (cfg.clusterCap > cfg.heatCap) { cfg.heatCap = cfg.clusterCap; heatSrc = "raised-to-clusterCap"; Info (below); }
//     if (cfg.clusterCap > cfg.totalRiskCap) { cfg.totalRiskCap = cfg.clusterCap; totalSrc = "raised-to-clusterCap"; Info (below); }   // Q13; the Input property is not rewritten
//   } else if (ClusterCapPct != 0) Warning (out of range), src stays "profile";
//   Info: "PORT config clusterCap=<cfg.clusterCap> source=<input|profile> heatCap=<cfg.heatCap> source=<profile|raised-to-clusterCap> totalRiskCap=<cfg.totalRiskCap> source=<input|raised-to-clusterCap>"
```

- **Sentinel:** `0` (the default) = use the cluster cap resolved from the profile (Starter 0.75%, ScaledBook 2%), a subclass `Config`, or the `ConfigPath` JSON `clusterCap` key, exactly as today. `> 0` = that percentage of book, e.g. `0.75` = 0.75%.
- **Precedence:** when `ClusterCapPct > 0`, it overrides **any** JSON `clusterCap` and any subclass `Config` value (same "folded in after config resolution" pattern as `TotalRiskCapPct`, L59). The JSON key is still parsed and validated, but the Input wins.
- **Range:** valid when 0 < `ClusterCapPct` ≤ 100. A negative value or one above 100 logs `PORT Warning: ClusterCapPct(<x>) out of range (0 < pct <= 100): using the profile value <c>` and keeps the profile value (source=profile) (**PROPOSED** handling, unchanged).
- **A cluster cap above the heat cap raises the heat cap** (**Owner-approved 2026-11-11 11:09 ET** (Ettienne chose: *"Raise the overall open-risk cap automatically to match a higher cluster cap"*); replaces the r6 PROPOSED warn-and-fall-back rule, Q12). When `ClusterCapPct` / 100 > the effective `heatCap` (profile, subclass or JSON value), the effective `heatCap` for that run is set **equal to the cluster cap**, so the `Validate()` rule `clusterCap ≤ heatCap` still holds and the cluster cap can bind. It is logged in the config line (`heatCap=<x> source=raised-to-clusterCap`) plus one Info line: `PORT Info: heatCap raised from <old> to <new> to match ClusterCapPct(<pct>)`. Otherwise (cluster cap ≤ heat) the profile/JSON `heatCap` is unchanged (`source=profile`). Example: Starter (heat 1.5%) with `ClusterCapPct = 8` ⇒ cluster 8%, heat 8%.
- **A cluster cap above the total cap raises the total cap too** (**Owner-approved 2026-11-11 13:29 ET** (Ettienne: *"Yes, raise the TotalRiskCapPct as well."*); replaces the r7 "the total risk cap will bind first" Warning, Q13). The raise runs **after** `TotalRiskCapPct` is folded in (L59). When `ClusterCapPct` / 100 > the effective total risk cap, the effective `totalRiskCap` for that run is set **equal to the cluster cap**, logged in the config line (`totalRiskCap=<x> source=raised-to-clusterCap`) plus one Info line: `PORT Info: TotalRiskCapPct raised from <old> to <new> to match ClusterCapPct(<pct>)`. Otherwise it stays as set (`source=input`). Only the effective cap changes: the `TotalRiskCapPct` [Input] value itself is **not rewritten** (the Format dialog, RUN line Input dump and saved `.pws` keep the user's value). Example: `ClusterCapPct = 12` with `TotalRiskCapPct = 10` ⇒ cluster, heat and total caps all 12%.
- **Applies to new entries and adds:** the override replaces `cfg.clusterCap`, which feeds the existing new-entry cluster check (`clusterCapD`, `_PortMM` L843/L889, including P2 (c): a lone entry in an empty cluster is still not checked) and the add check in §3 check 3. No separate add cap.
- **Config-time log:** one Info line at `StartCalc`, every run: `PORT config clusterCap=<fraction> source=<input|profile>` (`profile` covers profile defaults, subclass `Config` and JSON). The existing config dump (`clusterCap(<x>)` in the RUN/CFG text) shows the effective value.
- **Default ⇒ no behaviour change:** with `ClusterCapPct = 0` the effective cap equals today's, so every decision is unchanged. G0 and DVP D9a are chart runs where M-PORT is inert, so their hashes are unchanged. The one new Info line is the only PT journal difference (§8).
- m-port-spec §0 must then say "three `[Input]`s" (`TotalRiskCapPct`, `AddPolicy`, `ClusterCapPct`) in rev 2.8.

- **Saved portfolios.** Adding an Input to a compiled signal may reset or re-order its saved inputs in existing `.pws` portfolios. **Native check A0 (2026-11-11, scratch MM):** a `.pws` saved with `TotalRiskCapPct = 7` kept 7 after two Inputs (`AddPolicy`, `ClusterCapPct`) were added and the signal recompiled. After the real update, still re-check that `TotalRiskCapPct` kept its value in Ettienne's saved portfolios.

```csharp
public enum EAddSizing { ScaleWithInitial, CodeQty }       // default ScaleWithInitial (ignored under CodeSize)
// PortMmConfig
public EAddPolicy addPolicy = EAddPolicy.Refuse;           // set from the AddPolicy [Input] only (not JSON)
public int maxAddsPerPosition = 2;                         // 0..4; 0 with AllowWithinCap = adds refused, journalled as "max adds"
public EAddSizing addSizing = EAddSizing.ScaleWithInitial;
```

- **JSON:** `"maxAddsPerPosition": 2`, `"addSizing": "CodeQty"`. Enum values are parsed by member name, case-insensitive, via `Enum.TryParse` with an `IsDefined` check. Integer codes are rejected (rule 9: no int codes). An unknown name logs a Warning and keeps the field default.
- There is no throttle option: the owner decided to block (§6), so the earlier `EAddThrottle` draft enum is dropped.
- **Validate():** `0 ≤ maxAddsPerPosition ≤ 4`.
- **Both profiles** default to `Refuse` (the Input default). Starter and ScaledBook differ only in their caps, which already bound the adds.
- **Samples:** the two sample JSON files are **not** changed. Their hash stays the same, and a missing key means `Refuse`.

## 8. Backward compatibility and regression gates

- **Default `Refuse` ⇒ no behaviour change.**
  - The candidate filter keeps L813/L814 verbatim on the `Refuse` branch.
  - No new journal line, Status text or RUN text.
  - The R stream is unchanged for trades without an add fill: one segment, so R(rebased) = R(original) and every existing kill / session / weekly line is byte-identical.
  - The new `ClusterCapPct` Input defaults to 0 (= profile value), so caps and decisions are unchanged; its `PORT config clusterCap=… source=profile` Info line is the one new PT journal line, even under `Refuse`.
  - The new `AddPolicy` Input defaults to `Refuse`. The `TotalRiskCapPct` value in saved portfolios must survive the signal update (A0).
  - `PortDecision` values are unchanged: `entryContracts` −1 for every in-position strategy, `allowEntries` false.
- **Publisher keys are additive.**
  - `pm.ver` stays 1.
  - `pm.add` = 0, `pm.armStamp`, `pm.avgPx` and `pm.px` are written but unread under `Refuse`.
  - The old `portfolioArm` path is unchanged, apart from also writing `pm.armStamp`.
  - The `m_portStop` hazard (§1) stays as it is for `portfolioArm`. Only `portfolioArmAdd` avoids it.
- **Regression gates (all must pass before merge):**
  1. **G0** (`_TestGate0` canary) and the **DVP D9a** summary: the documented ES baselines (m-instr-spec rev 2.5.2 / 2.5.13). Native re-run on the MiniPC must give **unchanged hashes**. These are chart runs where M-PORT is inert, so this proves the publisher edits (new keys, `portfolioArmAdd`) did not touch the chart path.
  2. **PortMMTests:** every existing test passes unchanged, including T-P22's "same-direction add refused" checks (L515–519), which must keep their exact reasons. The count rises only by the new tests (§9.1).
  3. **PT replay with `Refuse`:** the Check 7 L7 portfolio (`@ES` 1m + `@NQ` 5m, Check7 L7.pws, ≤ 2024) and one real-strategy portfolio, run before and after. The `PORT` journal lines, Status texts and trade lists must be identical (diff = empty).
  4. Every other suite under `tests/` is unchanged (no shared code is edited beyond `_PortMM`, `_PortfolioRiskManager` and the `_CustomSignal` publish block).
  5. No csproj change in the spec PR. The implementation PR adds no new file, so no csproj edit is expected there either: `_PortAdd` lives in `_PortMM.Function.CS`.

## 9. Tests

### 9.1 Off-MC (`tests/PortMMTests`, new T-P28 … T-P40, PROPOSED)
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
  - **New stop, not the original (owner 10:28 ET):** book 1M (cap 10 000), long 2 @ 5000, initial stop 4970, price 5040, add 1 @ 5040 with `addStop` 5000 ⇒ riskAfter = (5040 − 5000) × 50 × 3 = 6 000 ⇒ **allowed**. Measured to the original stop 4970 it would be 10 500 and refused, so the test asserts the MM uses the new stop. Same add with `addStop` 4973 ⇒ 67 × 150 = 10 050 ⇒ `"add per-position budget"`. Short mirror: short 2 @ 5000, stop 5030, add @ 4960 with `addStop` 5000 ⇒ 6 000 ⇒ allowed.
- **T-P31 caps with release:**
  - Heat: the position's `rel` is released, then riskAfter counted. Another strategy's open risk still counts.
  - Cluster: always checked (own position occupies it). An ES add with NQ open, EQIDX over 7 500 ⇒ `"cluster cap"`.
  - **Cluster cap refuses an add on its own (owner-approved 10:13 ET):** Starter profile (cluster cap 0.75% = 7 500 on a 1M book; per-position 1% and heat/total caps set high enough not to bind), ES alone, no NQ open; an add whose riskAfter puts EQIDX at 7 501 ⇒ skipped with reason `"cluster cap"`, the add is denied on that step (deny-all: `allowEntries = false`, `EntryContracts` stays −1; 0 is never written, §4), `nAdd` unchanged (never resized), the position and its stop unchanged, one `PORT Info: skip … cluster cap …` line with `cluster $7501 cap $7500`. At exactly 7 500 ⇒ allowed (the check is `>`).
  - Total cap with a lowered `TotalRiskCapPct`.
- **T-P32 `maxAddsPerPosition`:**
  - Fills counted from |pos| growth. The third add with max 2 (owner-approved default for Starter) ⇒ `"max adds"`.
  - Partial exit keeps the count. Flat / flip resets it.
  - `pm.add` mismatch ⇒ MM count wins + one Warning.
  - 0 ⇒ every add `"max adds"`.
- **T-P33 sizing:**
  - `ScaleWithInitial` (owner-approved default): initial qtyCode 2 → n 20, add qtyCode 1 ⇒ nAdd 10.
  - nAdd < minContracts ⇒ skip.
  - CodeSize ⇒ −1.
  - Never 0 written.
- **T-P34 gates:**
  - Daily stop, kill, session stop, weekly observe deny with their usual reasons.
  - Throttle multiplier < 1 ⇒ add blocked, never scaled down, with the exact `ADD-BLOCKED` Warning text. Cover: ddSteps stage 1 (`reason=ddStep mult=0.5`), weekly halve (`weeklyHalve`), both together (`ddStep+weeklyHalve mult=0.25`), daily stop, session stop, weekly observe. Under CodeSize too.
  - The same blocked add re-sent for 10 evaluations ⇒ exactly **one** Warning. A change of mult (0.5 → 0.25) or of leg ⇒ one more. Clear then block again ⇒ one more.
  - An initial entry under the same throttle keeps its existing Info skip text (no Warning).
  - `maxPositions` full ⇒ the add is still allowed (not a new position).
- **T-P35 identity:**
  - Stale arm (`armStamp != stamp`) ⇒ `"add: stale arm"`.
  - NaN `addStop` ⇒ `"add: no addStop"`.
  - NaN `avgPx` ⇒ `"add: no avgPx"`.
  - An opposite side with `pm.add` = 1 ⇒ treated as a reversal (4c), `pm.add` ignored.
  - `Halted` ⇒ never an add.
- **T-P36 config:**
  - `addSizing` JSON names parse case-insensitively; `"addSizing": 1` (int) is rejected with a Warning.
  - `"addPolicy"` in JSON ⇒ the "JSON value ignored" Warning; the effective policy = the Input value.
  - The `AddPolicy` Input sets `cfg.addPolicy` after Resolve (also over a subclass Config and after a `config=fallback`).
  - Unknown name ⇒ default.
  - `maxAddsPerPosition` 5 ⇒ Validate fails ⇒ `ForProfile` fallback (`config=fallback`).
  - RUN line unchanged under `Refuse`; `adds(...)` appended otherwise.
  - Publisher pure helpers: `_PortAdd.RiskAfter` long/short, at/after break-even, bad BPV ⇒ NaN ⇒ refused.
- **T-P37 R re-base** (book 1M, ES BPV 50):
  - Long 2 @ 5000, stop 4995 ⇒ R0 = 500. At price 5005 (+1R0 open, `pnl` +500), add 1 fills with `stopNew` 5001 ⇒ R1 = (5005 − 5001) × 50 × 3 = 600.
  - Stop-out at 5001: segment-1 P&L = (5001 − 5005) × 50 × 3 = −600 ⇒ trade R = +1.0 + (−1.0) = **0.0**.
  - R(original): total P&L = (5001 − 5000) × 2 × 50 + (5001 − 5005) × 1 × 50 = 100 − 200 = −100 ⇒ −100 / 500 = **−0.2**.
  - Check: kill queue, session count and weekly sum receive 0.0, not −0.2. Both values appear in the `trade R` line.
  - The re-base fires on the fill evaluation only, not on the arm or approval evaluations.
  - Two adds ⇒ three segments.
  - `R1 ≤ 0` ⇒ no re-base + Warning.
  - Reversal ⇒ no re-base; the trade closes summing its segments.
  - A trade without adds ⇒ identical R and identical lines to rev 2.7.
  - **Short mirror:** short 2 @ 5000, stop 5005 ⇒ R0 = 500. At 4995 (+1R0 open), add 1 fills with `stopNew` 4999 (the add's lower high + 1 tick, stop lowered) ⇒ R1 = max(0, −1 × (4995 − 4999)) × 50 × 3 = 600. Stop-out at 4999 ⇒ segment-1 P&L = −600 ⇒ trade R = **0.0**; R(original) = (100 − 200) / 500 = **−0.2**. A short `stopNew` ≤ `px` ⇒ `R1 ≤ 0` ⇒ no re-base + Warning.
- **T-P38 partial exits with re-base:**
  - T1 one contract at +1R0 before the add ⇒ its P&L stays in segment 0.
  - A T1 after the add ⇒ in segment 1, at R1 (not shrunk).
  - A partial exit never starts a segment or closes the trade.
  - A net-shrink sample is not an add fill; a net-grow sample with an approved add is.
- **T-P40 add fill-check Warning (owner 10:28 ET):** `fillCheckEvals` 3. (a) Approved market add, position does not grow for 3 evaluations ⇒ one `PORT Warning: … add leg 1/2 … approved (qty n) but position did not grow after 3 MM evaluations …`; a 4th and 5th evaluation ⇒ no repeat. (b) Resting long add buy-stop above a price that never reaches it, 10 evaluations ⇒ no Warning. (c) Price closes through the stop on 3 evaluations without a fill ⇒ one Warning. (d) Leg 2 of the same position, and leg 1 of a new position after flat, can each warn once. (e) `fillCheckEvals` 0 ⇒ never. (f) The add fills on evaluation 2 ⇒ no Warning, count reset.
- **T-P39 `ClusterCapPct` override (owner-approved 10:14 ET):** Starter profile (cluster cap 0.75%), book 1M.
  - `ClusterCapPct = 0` ⇒ effective cap 0.0075, `source=profile`; decisions identical to today.
  - JSON `"clusterCap": 0.01` and `ClusterCapPct = 0` ⇒ 0.01, `source=profile`.
  - JSON `"clusterCap": 0.01` and `ClusterCapPct = 1.5` ⇒ 0.015, `source=input` (Input overrides JSON).
  - `ClusterCapPct = 1.5`: a **new entry** (ES, NQ open in EQIDX at 8 000) that the profile cap refuses (`"cluster cap"`) is allowed up to 15 000; an **add** with EQIDX after the add at 12 000 is allowed, at 15 001 ⇒ skipped `"cluster cap"`, never resized.
  - `ClusterCapPct = −1` or `= 150` ⇒ the out-of-range Warning, effective cap = profile value, `source=profile`; heat unchanged.
  - **Heat auto-raise (owner-approved 11:09 ET):** Starter (heat 1.5%), `ClusterCapPct = 8` ⇒ clusterCap 0.08, heatCap 0.08, config line `heatCap=0.08 source=raised-to-clusterCap` and one `heatCap raised from 0.015 to 0.08` Info line; `Validate()` passes. A new entry that the old 1.5% heat would refuse but that fits 8% is allowed; an add taking EQIDX to 80 001 ⇒ `"cluster cap"`.
  - `ClusterCapPct = 1.0` (≤ heat 1.5%) ⇒ heatCap stays 0.015, `source=profile`, no raise line. JSON `heatCap` 0.10 with `ClusterCapPct = 8` ⇒ heat stays 0.10 (no raise).
  - **Both raises (owner-approved 11:09 and 13:29 ET):** Starter (heat 1.5%), `TotalRiskCapPct = 10`, `ClusterCapPct = 12` ⇒ clusterCap 0.12, heatCap 0.12, totalRiskCap 0.12; config line `clusterCap=0.12 source=input heatCap=0.12 source=raised-to-clusterCap totalRiskCap=0.12 source=raised-to-clusterCap`; exactly two Info lines (`heatCap raised from 0.015 to 0.12 …`, `TotalRiskCapPct raised from 0.1 to 0.12 to match ClusterCapPct(12)`); no Warning. The `TotalRiskCapPct` property still reads 10. A new entry taking total open risk to 11% (and the cluster within 12%) ⇒ allowed; to 12.0001% ⇒ refused (`"cluster cap"` or `"total risk cap"`, whichever is breached first in order).
  - `ClusterCapPct = 8` with `TotalRiskCapPct = 10` ⇒ heat raised to 0.08, totalRiskCap stays 0.10 (`source=input`), only the heat Info line.
  - `ClusterCapPct = 0` ⇒ no raise of either cap, config line `source=profile` / `source=input`.
  - The `PORT config clusterCap=<x> source=<input|profile>` line is written once per run in every case.

### 9.2 Native MiniPC checks (Check 7 style, new "Check 7A", backtest only, data ≤ 2024)
- **Signal.** A throw-away signal `_TestPortAdd : PortfolioSignalObject`, not derived from `_CustomSignal` (as `_TestPortArm`), with input `Test` typed `public enum EAddCheck { A0, A1, A2, A3, A4, A5, A6, A7, A8 }` (rule 9; `_TestPortArm` still uses a string).
- **Setup** as Check 7:
  - `@ES` 1m + `@NQ` 1m, dxFeed.
  - PT Exposure / Max % capital at risk 100% / 100%.
  - The MM under test is the implementation build with the `AddPolicy` Input = `AllowWithinCap`.
  - Every line is prefixed `P5b Ax` and journals `ARM#i FILL` and `ARM#i STAMP` like `_TestPortArm`.

| # | Question | How | Pass | If not |
|---|---|---|---|---|
| A0 | `AddPolicy` and `ClusterCapPct` Inputs in the PT money-management signal's Format dialog (and the `PORT config clusterCap=… source=…` line); does an existing `.pws` keep `TotalRiskCapPct` after the update; can the optimizer sweep `AddPolicy`? | Open an existing portfolio after compiling; read both Inputs; try an optimization over `AddPolicy` | Named members `Refuse` / `AllowWithinCap` shown; `TotalRiskCapPct` unchanged; sweep result recorded either way | Re-enter `TotalRiskCapPct`; if there is no enum sweep, use two saved backtests |
| A1 | Does PT fill a same-direction add, and which Strategy Properties Position Limits option allows it? | Buy 1 at bar 10, buy 1 more ("LA1") at bar 20. Run with the limit set to 1 entry, then to "allow up to N entry orders in the same direction" with N = 3 | No add fill with limit 1. With N = 3 the position goes 1 → 2. Record the exact option name | Adds cannot run in PT; keep `AddMode = None` |
| A2 | `EntryContracts > 0` on the add step sizes only the add | MM writes 4 on the add step, −1 otherwise | Add fills 4 and the initial stays 1. If the strategy also sends a second entry the same bar, both get 4 (documents the one-pending-entry rule) | Use CodeSize for adds (§4) |
| A3 | A resting add stop order, re-sent each bar, is sized by the `EntryContracts` of its fill step | Add stop above market for 5 bars; MM writes 2, 3, 3, 3, 3 | Fill qty = that step's value | Publisher must re-arm every bar (already the rule) |
| A4 | T1 `From Entry` exit sells initial contracts only | Initial 2 + add 1; exit 1 "From Entry" initial at bar 30 | Position 3 → 2; the add leg stays | Spec the T1 exit by qty instead of name |
| A5 | `CurrentPosition`, `PositionSide`, `AvgEntryPrice` and `pm.pos` after an add, on the same step the MM reads | Journal them each bar | Updated on the fill bar's next MM step; avg = qty-weighted | Delay the add-fill counter by one step |
| A6 | Reversal with adds open | Long 2 + add 1, then `SellShort` 1 | All 3 longs closed, short 1 opened; one reversal | Reversal `rel` must sum legs by fills |
| A7 | `ForceClosePosition` with adds open | Long 2 + add 1, MM force-closes | All legs flat at the close of bar T (as L3) | Kill path must close per leg |
| A8 | R re-base and `ADD-BLOCKED` in a real run | Long 2 + add 1 on `@ES`, stopped at the raised stop. Second run with a ddSteps override that puts stage 1 on before the add | One `R rebase` and one `trade R` line, with R(rebased) ≈ the segment sum. Second run: one `ADD-BLOCKED reason=ddStep` Warning and no add fill | Fix the fill detection or the dedupe key |

**Results 2026-11-11 (MiniPC, MC .NET64 16.0, backtest, `@ES` + `@NQ` 1 min, 2024-11-18 window; full lines in `specs/live-check-sheet.md` Check 7A).**

- The signal used is the scratch pair `_TestM118PortAdd` / `_TestM118MM` (not committed), with `AddPolicy = AllowWithinCap` for A1–A8.
- A1–A7 test only MultiCharts behaviour, so their answers are final.
- A0 is answered on the scratch MM.
- A8 is a proxy: the scratch MM applies §3a/§6 to real PT fills. **Re-run A0 and A8 on the implementation build.**

| # | Result |
|---|---|
| A0 | **Pass (scratch MM)**: named enum members shown; `TotalRiskCapPct` 7 kept after the new Inputs; Refuse 1 vs AllowWithinCap 2. **Optimizer enum sweep not tried.** The real `PORT config clusterCap=… source=…` line is still to check |
| A1 | **Pass**: unchecked 1; "different entry order" 2 with a new order name, 1 with the same name; "regardless of the entry …" 2. Wording in §4 |
| A2 | **Pass**: 1 → 5 (add 4, initial 1); with a second same-step entry 1 → 9 (both 4) |
| A3 | **Pass**: fill qty = the MM value on the sending step (3 from send 2; 2 from send 1) |
| A4 | **Pass**: `From Entry` sells only the named leg (3 → 2) |
| A5 | **Pass**: the MM sees `openLots`, `AvgEntryPrice` and `pm.pos` updated on the same step the strategy first sees the add; avg is qty-weighted. No extra delay is needed |
| A6 | **Pass**: one reversal 3 → −1 closes both legs |
| A7 | **Pass**: both legs flat at the close of bar T |
| A8 | **Pass on `@ES`, proxy; re-run on the implementation build.** a: one `R rebase`, one `trade R`, R(rebased) = the segment sum (0.38 + −1.00); b: one `ADD-BLOCKED reason=ddStep` Warning for 10 blocked sends, no add fill. `@NQ` was stopped out before the add in both runs (not exercised) |

**Observation (A8-a, no rule changed).**

- The add filled at 5906.75. `px` (the close at the fill sample) was 5905.75, and the raised stop 5905.5.
- So R1 = $37.50 (1 tick × 3 contracts). The stop-out was −1.00 R1, and R(rebased) was −0.63 for a trade that made +$37.50 (R(original) +0.19).
- That is §3a as written; it is listed for Ettienne.

Results are recorded in `specs/live-check-sheet.md` (new Check 7A) and m-port-spec §9 (A0–A8). Paper / live sharing (L6) and the roll (L9) are not affected.

## 10. Build steps (for the implementation PR, after approval)
1. `_PortMM.Function.CS`:
   - `EAddPolicy`, `EAddSizing` and the fields from §7, plus the JSON parse (`addSizing`, `maxAddsPerPosition`; an `addPolicy` key ⇒ the ignored Warning);
   - R segments in `KillTracker` / `updateKills` (§3a), and the `R rebase` / `trade R` lines;
   - the `ADD-BLOCKED` Warning with its dedupe key (§6);
   - `_PortAdd` (pure: `RiskAfter`, fill counter);
   - the add branch in `Decide` next to the reversal branch: `isAdd(s)`, the gates, the caps with release (§3);
   - add fill tracking in `checkFills`;
   - the journal lines.
2. `_PortfolioRiskManager`:
   - `[Input] public EAddPolicy AddPolicy` (default `Refuse`), copied into `cfg.addPolicy` in `StartCalc` next to `totalRiskCap` (L59);
   - `view` reads `pm.add`, `pm.addStop`, `pm.avgPx`, `pm.px`, `pm.bpv`, `pm.armStamp` into new `PortStratView` fields.
3. `_CustomSignal`:
   - `portfolioArmAdd`;
   - `pm.add` reset with `pm.side` (L1796), `pm.avgPx`, `pm.px`, `pm.bpv`, `pm.armStamp`;
   - the `pm.openR` re-base on an add fill (§3a);
   - the warning for a same-direction `portfolioArm`.
   - No `[Input]`.
4. Tests §9.1. Then the regression gates §8 (G0, D9a, PortMMTests, `Refuse` PT replay). Then Check 7A §9.2.
5. Specs:
   - m-port-spec rev 2.8: §0 (two `[Input]`s), §3 keys, §5.2 pseudo-code, §5.4 4d "Add legs", the R-stream re-base and the Warning exception in the skip-logging rule, §6, §9 A0–A8, §11.
   - Overview §7.7 on PR #117:
     - lift "PT runs use `AddMode = None`" once A0–A8 pass and the owner approves;
     - align its chart-side "R stays the initial position's R" with §3a.

## 11. Owner answers and open questions

**Resolved, owner-approved 2026-11-10 22:35 ET** (verbatim: *"1. An Input on the risk manager. 2. No, the stop in general would move for the second entry so the risk calculation should be adjusted. 3. Adds should be blocked, issuing a warning log entry why it was blocked. 4. Explain this to me"*):
- **Former Q1 (where the policy lives):** an `[Input]` on the risk manager, `AddPolicy` of type `EAddPolicy { Refuse, AllowWithinCap }`, default `Refuse` (§7).
- **Former Q3 (R units after adds):** re-base on the adjusted position at the add fill, by segments, with R(original) journalled for audit (§3a).
- **Former Q4 (throttles):** block the add, never scale it, with an `ADD-BLOCKED` Warning (§6).

**Still open:**
- ~~**Q7, cluster cap.** It was explained to Ettienne after 22:35 ET ("4. Explain this to me"); his decision is pending. Starter 0.75% will usually deny an ES add while NQ is open, and an ES add alone once the ES position carries more than about 0.75% after the add. Options: accept it as "one position per merged cluster", or let an add be checked only against the per-position 1%. PROPOSED: accept; the cluster cap applies.~~ **Owner-approved 2026-11-11 10:13 ET** (Ettienne chose the option *"Apply the cluster cap to adds"*).
- ~~**Q2** Per-position budget = `maxTradeRisk` (1%, the same number as the chart rule), or a separate `addPositionRiskCap` ≤ 1%? PROPOSED: reuse `maxTradeRisk`. The 22:35 answer 2 settles how the post-add risk is measured (at the moved stop, §3), not this cap level.~~ **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*): 1% of the account (`maxTradeRisk`), measured on the whole post-add position to the **new** stop level (§3 check 1).
- ~~**Q5** Add sizing under RiskParity: `ScaleWithInitial` (keeps the strategy's add ratio at book scale), or `CodeQty`? PROPOSED: `ScaleWithInitial`.~~ **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*): `ScaleWithInitial`.
- ~~**Q6** `maxAddsPerPosition` default 2, matching YV-11's `MaxAdds` 2 and the overview's 2 + 2 × 1 = 4? Any lower Starter value?~~ **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*): default 2 for Starter (and ScaledBook).
- ~~**Q8** Should `fillCheckEvals` also warn on an approved add that never fills? VERIFY that it cannot false-alarm on a resting add stop that does not trigger; the "likely" wording is kept.~~ **Owner-approved 2026-11-11 10:28 ET** (Ettienne 2026-11-11 10:28 ET: *"1. 1% of the account based off the new stop-level.  2. Yes   3. Yes   4. Yes"*): yes; line, counting and dedupe in §6, test T-P40.
- ~~**Q12 `ClusterCapPct` above the heat cap (owner 10:52 ET follow-up):** should a `ClusterCapPct` above `heatCap` be **accepted** (and, if so, should heat be raised to match, or clamped/warned), or should `heatCap` get its own Input so heat is raised alongside? PROPOSED today: Warning + fall back to the profile cluster cap (§7). Note `TotalRiskCapPct` must also be ≥ the cluster cap.~~ **Owner-approved 2026-11-11 11:09 ET** (Ettienne chose: *"Raise the overall open-risk cap automatically to match a higher cluster cap"*): the effective heat cap is raised to the cluster cap for the run (§7).
- ~~**Q13 Total cap with a large cluster cap:** should `TotalRiskCapPct` also auto-raise when `ClusterCapPct` exceeds it, or stay as set (today: unchanged, with a Warning that the total cap binds first)? Not decided.~~ **Closed.** **Owner-approved 2026-11-11 13:29 ET** (Ettienne: *"Yes, raise the TotalRiskCapPct as well."*): the effective total risk cap is raised to match (§7).
- **Q9 (native checks, not owner questions).** Check 7A ran 2026-11-11 (§9.2):
  - ~~the MC Position Limits option name and behaviour in PT (A1)~~ **Answered (A1):** wording and behaviour in §4.
  - ~~whether `From Entry` exits behave in PT as on charts (A4)~~ **Answered (A4):** yes.
  - whether saved inputs survive the new Input (A0): **answered on a scratch MM** (kept); re-check on the implementation build.
  - whether the optimizer can sweep the `AddPolicy` enum (A0): **still open**, not tried.
  - A0 `PORT config` line and A8 (R re-base / `ADD-BLOCKED`): **still open** until re-run on the implementation build (the A8 proxy passed on `@ES`).
- ~~**Q10** PR #117 overview §7.7 says "R for reporting stays the initial position's R" on charts. Should the chart side re-base too, so chart and Portfolio Trader R agree? PROPOSED: yes, as a separate edit on PR #117.~~ **Closed 2026-11-11**: already owner-approved 2026-11-10 22:35 ET (answer 2) and applied in PR #117 overview rev 1.6 §7.7 (chart-side R re-base).
