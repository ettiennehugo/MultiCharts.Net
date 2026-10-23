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
