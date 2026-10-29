# Pre-registration amendment: Gate 0, ATR5/ATR30 restart at a futures roll

**Status: NEW PRE-REGISTRATION, proposed in PR #43 (branch `fix-a-gate0-atr-roll`), 2026-10-29 (ET).** It is registered when Ettienne merges PR #43. Base: `main` @ `800bce7`.
**Sources:** educator finding G1 (`educator-findings-review-analyst1.md` §1 G1; `educator-findings-review-analyst2.md` §1, both CONFIRM), the PR #43 reviews by Analyst 1 (REQUEST CHANGES: reset ordering, this document) and Analyst 2 (nits), and the engines manual L328 (Gate-0 constants are pre-registered) and L361 ("all multi-session sources restart at the roll").
**This document contains no results.**

## 1. Rule change
- **Before (main @ 800bce7):** at a roll (`Gate0Bar.roll`) only the daily ATR20d dropped the splice true range (`m_atrD.ResetPrev()` in `onRoll`). ATR5 = ATR14 on 5m buckets and ATR30 = ATR14 on 30m buckets kept the old contract's prior close, so the first new-contract bucket's TR contained the cross-contract gap. That inflated TR then decayed under Wilder smoothing into roughly session +2.
- **After:** on the roll bar, `Gate0Engine.Bar()` calls `m_atr5.ResetPrev(); m_atr30.ResetPrev();` **after** the PreBar flush of buckets still open from the old contract. The first new-contract 5m/30m TR is that bucket's own H−L. Wilder state (n, value) is kept and no new warm-up starts. This is the same policy ATR20d already used.
- Ordering: if the reset ran in `onRoll`, before the flush, an old-contract bucket still open at the roll would close after the reset and re-arm the old close. The gap would then leak anyway. That happens with tick data, or when the session's last bar is not stamped on a bucket end (for example 15:58). Only the two ATR resets moved; the rest of `onRoll` is unchanged (see the PR body).

## 2. What changes (ATR5 and ATR30 values after every roll)
Every rule that reads ATR5 or ATR30 from the roll session until the excess has decayed (about session +2). Sessions with no roll in their look-back are bit-identical.
- **A2-33 sigma** (`_Gate0.Function.CS` ~L715): `sigma = m_atr5.Value`, frozen at the RTH open. This changes the R / reaction scaling of every level study, the matched-control distances, and the ControlFL fake-level offsets (±{0.5,1,2}×ATR14(5m)).
- **Canary ATR rules** (~L955-983): A2-24 (i) R = ATR5 (~L961); A2-24 (iii) the 2×ATR5-from-VWAP fade (~L973); DV-01 `need = dv01K * ATR5` (~L983).
- **DV-10** (~L1032): band check using `a = m_atr30.Value`.
- **dvBar atrPrev** (~L536-539): the ATR30 before each 30m bar feeds the DV rotation box width (w = 1.5×ATR30), the breakout range (1.5×ATR30) and the trend-leg threshold (3×ATR30), so it changes which DV_ROT_POC_R6/R12 and DV_TREND_HVN levels exist.
- ATR20d, Holm m, the costs and all other constants are unchanged.

## 3. Consequence for earlier results
Gate-0 runs made before this change (G0 level study Variants 0-13, controls, G0-b canaries 0..7 and the A2-53 sweep) belong to a **different trial**. They must not be pooled with, or compared as replications of, runs on the amended engine. Journal and config hashes change. After merge, the G0 study, the controls and the G0-b canaries are re-run. The roll session ±1 stays excluded from scoring, as before.

## 4. Verification (no results)
`tests/Gate0Tests`: the splice tests (minute data ending at 16:00 and at 15:58, plus tick-style 30-second stamps with an old-contract bucket pending at the roll) require ATR5/ATR30 to be bit-identical to the no-gap control. The unflagged-gap controls show that ATR5 and ATR30 inflate without the reset. The 15:58 and tick tests fail on 5385c90 (reset inside `onRoll`) and pass on this branch.
