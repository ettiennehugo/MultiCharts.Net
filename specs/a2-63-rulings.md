# A2-63 host + regime gate: final rulings on PR #26 (Analyst 1, Analyst 2)

Binding alongside `specs/regime-consumer-prereg.md`, which is unchanged. This file records the rulings in substance, together with how each one is implemented.

## (1) ATR20d
ATR20d is the Wilder M-DAILY `Atr20d`, computed over **completed prior sessions only**. Today's session must not be included, and the value is journaled at the open.

- Implementation: `_Test6` passes `DailyContext.Atr20d` with its as-of date `DailyContext.PrevDate`. `A263Core.Latch` rejects the ATR (no entry that day, `atrOk(0)`) unless its as-of date is before the session date.
- REGGATE journals `O()`, `atr20d()`, `atrAsOf()`, `atrOk()` and `thr()` at the open.

## (2) Timing
Timing stays as built. All times are exchange (ET) wall-clock times, so the windows hold through DST.

- **CME half-days:** exit on the first bar closing at or after the early close minus 5 minutes. The early close comes from the existing calendar helper `_CmeHolidays.Rules.Get(date).RthCloseEt` (13:15 ET), so the exit is at 13:10 ET. No stop is worked at or after that time. CME no-RTH days (holiday halts) get no entry. `A263Config.halfDayExitBefore` is 5m.
- **Bar size:** run on **5m**. If that isn't possible, log the bar size used as a deviation. The REGGATE header journals `bar(type size)` and `barDeviation(0|1)`, and adds a `REGGATE deviation` line when the bar size is not 5 Minute.
- **Known limitation:** the stop works from the bar *after* the 09:30 latch bar, so a breakout inside the first bar is missed.

## (3) Days with no usable regime
These days **trade** in G and A, so the day set traded on them is identical across arms (equal to U).

- Each arm journals running counts on every REGGATE line: `nSess`, `nUsable`, `nWarm` (warm-up: engine not Valid / no label), `nInv` (invalid label: state Valid but AsOf not before t or label out of range), `nFb` (ATR fallback, arms U/G; see (a)), `nBlock` and `nNoRth` (see (b)). The last line holds the totals. Each line also carries `why(Ok|Warmup|Invalid|Fallback)`.
- The **offline secondary view drops those days from G and A together**: the union of both arms' no-usable days is dropped from both arms (and from U), so G and A are compared on the same day set.

## (4) Label timing
The gate uses the **post-hysteresis Label as of the prior close** (`AsOf < t`, never RawArgmax).

- Test: the label, posterior, ATR tercile and decision used on day D are identical when day D's bars (and everything after) are removed, and AsOf equals D-1.

## (5) / (6)
Fine as built. The config hash (FNV-1a 64) is computed over the **named prereg field list** (`RegimeGate.HashFields`: every RegimeConfig field, gate arm/blocked label/placebo seed, and the host core), sorted by name as `name=value;`. It does not depend on field order or record length.

- Fields not on the list do not change the hash. A missing named field throws.
- Test: the hash is pinned with extra fields appended and with reversed field order.

# Final answers on PR #26 (Analyst 1, Analyst 2)

## (a) Union of no-usable-regime days
The offline view drops the **union** of the no-usable-regime days from G and A together.

- The dropped days are journaled as three separate per-arm counts so the union can be checked: `nWarm` (warm-up), `nInv` (invalid label) and `nFb` (ATR fallback, with `why(Fallback)`).
- In arm A the ATR fallback is the gate itself, so those days count as usable and A's `nFb` is 0.
- Precedence when a day matches more than one: warm-up, then invalid (AsOf not before t comes before the fallback check), then fallback.

## (b) Holiday-halt days vs half-days
- Holiday-halt days with no RTH (`_CmeHolidays.Rules` `NoRth`) still get **no entry**, and each arm counts them as `nNoRth`.
- Half-days are distinct from halt days. They are not `NoRth`, still trade, and exit at the early close minus 5m (13:10 ET).
- Tests cover both cases: halt days on Jul 4, Thanksgiving, MLK and Memorial Day, and the day after Thanksgiving, Dec 24 and Jul 3 as half-days.

## (c) Version and hash pins
The version stays `A263-RG-2`. The version tag is field `v` of the hashed named-field record (`RegimeGate.HashFields`, canon `...;v=A263-RG-2;`), and changing it changes the hash.

Final pins (FNV-1a 64, tested in `tests/RegimeGateTests`):

| Arm | Hash |
|---|---|
| U | `26b38655261cc0bf` |
| G | `9c1da7b123ca9441` |
| A | `3d9814a5f264c353` |

Prereg history: 377bd18 committed a pre-sign-off draft of placebo step 5; 54d9188 replaced it with the signed rev 2 text (sha256 66ae7bb5…) before any code was committed.

# PR #46: roll handling (PROPOSED, pending Ettienne, except (h), DECIDED; analysts 1+2 review of PR #46)

Nothing in this section is pre-registered until Ettienne accepts it, except (h), which he decided. (d) and (e) describe what the PR #46 build does; they become rulings and pins of record only on his acceptance.

## (d) PROPOSED, pending Ettienne: roll-date source and flag placement
- **Source:** `EventRules.RollDate(y, m)` (CME customary roll). It is the Monday before the third Friday for contracts whose third Friday falls on or after `EventRules.RollRuleMondayFrom = 2022-04-01`, and the Thursday eight calendar days before it for earlier contracts. ESM22 is the first Monday roll (Mon 2022-06-13: Wayback 20220610060819 CME rolldates page; Sierra Chart support 2022-06-09). ESH22 rolled Thu 2022-03-10. Pinned in `tests/EventTests` against CME-published dates: ESZ21 2021-12-09, ESH22 2022-03-10, ESM22 2022-06-13, ESU22 2022-09-12, ESH25 2025-03-17, ESM26 2026-06-15 and ESU27 2027-09-13.
- **Table:** `_Test6` runs `RollDetection = Table` with `RollDates` = that rule for every quarterly month of 2000-2035 (144 dates, `_RollDetector.DatesDigest` = `fdd63bd8f179a8d4`). `tests/RollTests` fails if `_Test6` is not Table.
- **Flag placement:** `_Test6.tradingDayKey` is the Globex trading day (`_CmeSessionClock.GlobexTradingDayEt`: ET >= 18:00 belongs to the next date), the same key Gate0 uses through `_VolumeProfileFuturesBase.GlobexDate`. The flag therefore lands on the first bar of the roll trading day, the 18:00 ET Globex open of the evening before. The M-DAILY roll offset is that bar's open minus the old contract's last close (tests/DailyTests `EveningSplice`). Consequence: M-DAILY days and ATR20d are Globex days (18:00-17:00 ET), not calendar days.
- **Trial-definition change:** ATR20d (prereg §C1, ruling (1)) is now the Wilder ATR over Globex trading days (18:00-17:00 ET), not calendar days. Every arm's thresholds, stops and RG-03 terciles change with it.
- **Offline exclusions:** the same table (`tools/m-event/out/roll_splices_2016_2024.csv` rule_roll, generated by `tools/m-event/roll_splices.py` from the same rule and checked against `EventRules.RollDate` by EventTests) drives the offline roll ±1 exclusions for every arm.

## (e) PROPOSED, pending Ettienne: new builds and hashes
The pins below are facts of the PR #46 code (including the decided ATR carry, (h)). They become the pre-registered pins only after Ettienne accepts (d)-(g) and (i).

`RegimeGate.HashFields` adds `host.rollDetection`, `host.rollYears`, `host.rollDates` (the digest), `host.tradingDay` and `host.atrRestartOnRoll` (ruling (h)). `_Test6` writes the values it actually runs into `HostConfig` before hashing, and the REGGATE header journals `roll(Table 144 dates 2000-2035 digest …) dayKey(Globex18ET)`. These pins supersede the pins in (c):

| Arm | Hash |
|---|---|
| U | `0af309b4220ef9c8` |
| G | `b82e06fe0e07d09e` |
| A | `5d0a364e767bd8ae` |

## (f) PROPOSED, pending Ettienne: void earlier dev runs
Any dev run on a build with the pins in (c) (RollDetection Off, calendar day key) is void and must not be compared with the builds in (e). The run is not a trial of record.

## (g) RECOMMENDED, pending Ettienne: observed splices before any dev run
Before §8 step 3, run `_EventCalendarTest` Mode 2 on dxFeed @ES for 2016-2024, fill `observed_splice` / `offset_sessions` in `roll_splices_2016_2024.csv` and commit them. Then add a test that every offset is 0 sessions. If any offset is non-zero, build `RollDates` from the observed splices instead of the rule (a new build and hash).

## (h) DECIDED (Ettienne, 2026-10-29): carry ATR20d on from the roll-adjusted history
- **Decision:** "Carry ATR20d on from the roll-adjusted history." This replaces the earlier PROPOSED blackout entry.
- **Implementation:** `DailyConfig.atrRestartOnRoll` (default `true`, so other hosts keep the §0.3 reseed bit-identically). `_Test6` sets it to `false`. When false, `_DailyContext.applyRoll` keeps the Wilder state (`m_atr`, `m_trSeed`, `m_trSeen`) and skips only the splice day's true range. The history is already difference-adjusted and TR is shift-invariant, so the pre-roll state is valid in new-contract space. There is no post-roll NaN blackout (previously about 20 of every ~63 sessions, ~720 sessions over 2016-2024).
- **Trial-definition change:** ATR20d for every arm is a continuous Wilder ATR across rolls, with the splice-day TR excluded (previously reseeded after every roll, NaN for 20 days, with "no entry that day"). Entries, stops and RG-03 terciles on post-roll days change with it.
- **Hash:** `RegimeGate.HashFields` adds `host.atrRestartOnRoll` (`A263Config.atrRestartOnRoll`, default false, written by `_Test6` before hashing); pins in (e).
- **Tests:** tests/DailyTests `RollCarryAtr`: splice-day TR excluded (ATR20d unchanged on the roll day despite a 500-point range); no NaN after the roll, and Wilder continues from the pre-roll value. The default (`true`) still pins the 20-day reseed (`EveningSplice`).
- **Journal:** the REGGATE suffix keeps `nAtrNaN(n)` / `nRollAtrNaN(n)`. With the carry, `nRollAtrNaN` should be 0 after the first seed.

## (i) PROPOSED, pending Ettienne: Globex key on holidays
`GlobexTradingDayEt` is a pure 18:00 ET rule. On a Monday-holiday Globex session (MLK, Presidents, Memorial, Labor Day) the Sunday 18:00 to Monday halt session gets the holiday's own date, so M-DAILY sees one short extra day (one extra TR in ATR20d/SMA, about 5 times a year). CME books those trades to the next trade date. Gate0 uses the same key, so the two stay consistent. No roll date in 2000-2035 falls on such a day. Proposed: accept as documented (pinned in tests/DailyTests); folding holiday sessions into the next trade date would be a new build.
