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

- Each arm journals running counts on every REGGATE line: `nSess`, `nUsable`, `nWarm` (warm-up: engine not Valid / no label), `nInv` (invalid label: state Valid but not usable for the arm, i.e. AsOf not before t, UsingFallback in G, label out of range) and `nBlock`. The last line holds the totals. Each line also carries `why(Ok|Warmup|Invalid)`.
- The **offline secondary view drops those days from G and A together**: the union of both arms' no-usable days is dropped from both arms (and from U), so G and A are compared on the same day set.

## (4) Label timing
The gate uses the **post-hysteresis Label as of the prior close** (`AsOf < t`, never RawArgmax).

- Test: the label, posterior, ATR tercile and decision used on day D are identical when day D's bars (and everything after) are removed, and AsOf equals D-1.

## (5) / (6)
Fine as built. The config hash (FNV-1a 64) is computed over the **named prereg field list** (`RegimeGate.HashFields`: every RegimeConfig field, gate arm/blocked label/placebo seed, and the host core), sorted by name as `name=value;`. It does not depend on field order or record length.

- Fields not on the list do not change the hash. A missing named field throws.
- Test: the hash is pinned with extra fields appended and with reversed field order.
