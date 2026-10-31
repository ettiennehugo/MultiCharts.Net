# M-INSTR spec: instrument profile layer (auto-detected per-instrument configuration)

**Rev 3, 2026-10-24 (ET).** Q1–Q11 decided by Ettienne Hugo (recommended answers; stock exceptions kept). Rev 2b: Rev 2 plus Analyst 1 rev 2 review edits R1–R6. Rev 1 plus all three blockers and edits E1–E16 of Analyst 1's review (`specs/instrument-profile-review-analyst1.md`), each code claim re-checked on `origin/main` @ `7be55bf`. Q1–Q11 are DECIDED (§13.1). Rev 1 was the first draft. Checked read-only against `github.com/ettiennehugo/MultiCharts.Net` `origin/main` @ `7be55bf` (2026-10-24 12:32 ET). Every class, member and line number below was read on that commit unless marked **VERIFY**. Items no source settles are marked **[GAP]** and fixed here by a stated default listed in §13. Ettienne's decisions on Q1–Q11 are in §13.1 (changing one later touches only the §8.1 matrix and its reason strings).

**Sources:** the repo (`_CustomSignal`, `_VolumeProfileBase`, `_VolumeProfileFuturesBase`, `_PriceActionEngine`, `_RegimeEngine`, `_EventCalendar`, `_NyseHolidays`, `_Gate0`, `_ProfileMath`, `_OrderFlow`, `_RollDetector`, `_DvpEngine`, `tests/*`), `specs/m-dvp-spec.md` rev 4, `specs/m-event-spec.md`, `trading-idea-catalogue.md` §2.1 (cost floor, L110; wording decision L4739), and the MultiCharts .NET Programming Guide in `/workspace/mc-docs` (`01M5BF386FDT0VH0AYQBMXHEF2.pdf`, §4.2.3 "Properties of the Data Series", `IInstrumentSettings`; §4.x `InstrumentDataRequest`, `SymbolStorage.GetSymbols`), and the MultiCharts User Manual (`01M5BF385QBTK6KHTM7Z5DYDA0.pdf`, symbol parameters BigPointValue/MinMove/PriceScale examples, L1695–1762).

**Scope.** M-INSTR is a **base service** in `_CustomSignal`. At `StartCalc()` it reads the instrument of every used data stream from MultiCharts (`IInstrument.Info`, type `IInstrumentSettings`), classifies it into an `EInstrumentClass`, builds an immutable `InstrumentProfile` (tick, point value, sessions, calendar, roll mode, volume quality, cost model, sizing unit), logs what it found (`INSTR` line), and lets each engine derive its effective configuration from the profile (`CFG` line). Engines that cannot run on the class throw `EngineNotSupportedException` with a fixed message format (§6).

**Not touched.** The PriceAction library (`/workspace/PriceAction`) is not changed; only the repo wrapper `_PriceActionEngine`'s *preset selection* (already in `_CustomSignal` / `_VolumeProfileFuturesBase`) is driven by the profile. No `[Input]` is added anywhere. ES on `_VolumeProfileFuturesBase` must stay **bit-identical** (§11).

### Revision log

| Rev | Date (ET) | Change |
|---|---|---|
| 1 | 2026-10-24 | First draft for Ettienne's review. |
| 2 | 2026-10-24 | Analyst 1 review applied. Blockers: CmeGlobex holidays stay on `_CmeHolidays.Rules` (E1); cost floor opt-in, off by default (E8); ES `_TestGate0` canary SHA-256 gate + more suites (E3); fail-closed detection, MIC codes, I0 probe before any Category-dependent throw (E4, E5, E13). Corrections: DvpConfig/startDvp pending (#36); hash routines (`EventRules.Sha256`, 16 hex; `RegimeGate.Hash` FNV-1a 64); line refs (E15); `isRthBar` is `_CustomSignal` L516 (VPF override L512); `_NyseHolidays` created unconditionally in `Create()`; OrderFlow T7 is per bar (E11); `EPriceActionPreset {Library, Futures}`, PR-I7 dropped; example INSTR line = dxFeed `@ES`, $17.00. Also E6 (continuous/micros roots), E7 ($17.00), E9 (EVENT expiry kinds), E10 (DV-7.0 Data2 clause, DVP dependency), E12 (Portfolio Trader §11a), E14 (PR order), E16. Matrix now follows the recommended Q1–Q11 answers, kept OPEN. |
| 2b | 2026-10-24 | Analyst 1 rev 2 review (R1–R6): IP-0.9 overrides fail-closed until probe (`probePending`), IP-T9b; unmapped exchange throws only for non-futures; equity = `equityAvailable()` base, `equityBasis` dropped; 1%-sizing consequence stated, risk-sized skip in IP-T51; single substring exchange rule, duplicate BATS removed, IP-7.5 reworded; matrix note 4 merged, Regime roots listed. |
| 3 | 2026-10-24 | Ettienne Hugo decided Q1–Q11: recommended answers adopted, 'provisional' wording removed. Q2: FLOW on stocks/ETFs throws (fragmented off-exchange tape, ES-tuned thresholds, no stock evidence; revisit after a stock tape study). Q9: ES $17.00 subject to Ettienne's IBKR plan check. |

---

## 0. Cross-cutting rules

- **IP-0.1 Laziness.** The profile itself is a small immutable object built once per `StartCalc()` for each data stream that an *enabled* engine reads (Data1 always, because sizing and costs need it). It allocates no per-bar state. An engine's effective config is derived only inside that engine's `start*()` (e.g. `startEngines`, the EVENT/REGIME starters; `startDvp` once #36 lands) and only when the engine is enabled; a disabled engine never sees, logs or validates the profile. A disabled engine never throws `EngineNotSupportedException` (test IP-T3).
- **IP-0.2 No new inputs.** All settings live in protected config objects on `_CustomSignal` (host pattern on main: `EventConfig`, `RegimeConfig`, `DailyConfig`, `PriceActionConfig`; `DvpConfig` exists only as the core class in `_DvpEngine.Function.CS` L38 (PR-D1 #34), and its host property / `startDvp` are **pending PR #36**, not on main). New: `protected InstrumentConfig InstrumentConfig { get; } = new InstrumentConfig();` (§3). Derived strategies set fields in their ctor or `Create()`.
- **IP-0.3 Auto first, explicit override wins, override is logged.** Every profile field has an auto-detected value. A non-null/non-NaN field in `InstrumentConfig` (or in an engine config's existing field when it was set explicitly, see IP-4.3) replaces it. Each replacement writes one `INSTR OVERRIDE` line (§5.3) at `ETraceLevel.Warning` if it contradicts detection (e.g. class Forex forced to Future), else at `Info`.
- **IP-0.4 MC-free core.** Classification (`InstrumentClassifier.Classify(string categoryName, string exchange, string name, double minMove, double priceScale, double bigPointValue, DateTime expiration)`), the default tables, the cost model, the support matrix and `Canon()` live between `// <instr-core>` and `// </instr-core>` in a new `_InstrumentProfile.Function.CS`, with no MultiCharts types, so `tests/InstrumentTests/gen.sh` can extract them with awk (the `tests/Dvp/gen.sh` pattern). The read of `IInstrumentSettings` happens outside the region, in `_CustomSignal.buildProfile(int ds)`.
- **IP-0.5 C# 7.3.** No switch expressions, `??=`, ranges, records. No LINQ on any per-bar path (profile code has no per-bar path).
- **IP-0.6 Determinism.** The profile is a pure function of (IInstrumentSettings fields read, `InstrumentConfig`, the default tables' version). `Canon()` lists every field in fixed order with invariant culture; `profileHash` = `EventRules.Sha256(Encoding.UTF8.GetBytes(Canon()))` truncated to the first **16** hex digits (the repo's only SHA-256 helper, `_EventCalendar` L424; same truncation as L422). (`RegimeGate.Hash` L208 is FNV-1a 64 and is not used here.) `_CustomSignal` has no configHash routine; the RUN line (L1782) carries no hash. profileHash is logged on INSTR, CFG and RUN lines and is **not** an input to any existing hash in v1 (Q7).
- **IP-0.7 Futures default is the identity.** For class Future, every derived value equals what the engine uses today on `origin/main`. The profile *adds* logging; it does not change numbers. For ES this is enforced by the `_TestGate0` canary hash (IP-T46) plus the MC-free suites (IP-T40..T45).
- **IP-0.8 Fail closed.** No enabled engine runs on a silent generic default. Any missing class fact (Unknown class, calendar resolved to None, unknown future root in COST) either throws or is listed explicitly as a Degraded cell in §8.1. The only Unknown cell that runs is PA (Degraded).
- **IP-0.9 Probe before Category throws.** No throw whose condition depends on the `Category` mapping (IP-2.2) may be merged before the PR-I0 probe output for dxFeed `@ES`, `@MES` and one sample per other class is pasted into the fixtures and confirms the mapping. Until then those cells log at Warning only (`probe-pending`). Throws that do not depend on Category (tick ≤ 0, invalid overrides) are allowed from PR-I1. **IP-0.9 overrides IP-0.8, IP-2.5 and IP-4.5.** While `probePending`, a class of Unknown logs `WARN M-INSTR probe-pending: <reason 5 text>` once per engine, and the engine runs exactly as today (for VPFUT: the futures defaults, unchanged). Unknown fails closed only after the PR-I0 output is in the fixtures and the mapping includes the Category reported for `@ES`/`@MES`. `probePending` is a compile-time constant in `<instr-core>`, flipped in the PR that adds the fixtures.

---

## 1. MultiCharts API used (verified vs VERIFY)

Read on `Bars.Info` / `BarsOfData(ds).Info` (type `IInstrumentSettings`, Programming Guide §4.2.3):

| Member | Type | Status | Use |
|---|---|---|---|
| `Name` | string | verified (doc; used `_CustomSignal` L2944) | log, roll root |
| `DataFeed` | string | verified (doc) | log |
| `Description` | string | verified (doc) | log only |
| `Category` | `ESymbolCategory` | verified (doc). Used on main at L1432 (`ToString() == "Future"`, roll Warning), L1782 (RUN line logs `category(...)`), L2001 (`autoTradingDay(..., Category.ToString())`) and L2740 (`!= ESymbolCategory.Stock`, so the member `Stock` is verified). Other member names (Future as an enum member, Forex, Index, ...) are **VERIFY** (I0); the guide only says "futures, Forex or stock" in prose. | classification key |
| `Exchange` | string | verified (doc; used `resolveBarTimeZone` L2138) | calendar, time zone |
| `PriceScale`, `MinMove` | double | verified (doc; tick = MinMove/PriceScale in `_TestPortArm` L101) | tick |
| `BigPointValue` | double | verified (doc; used `_TestGate0` L229, `_DeltaDivRevFuturesLELX2` L159). **Note:** the guide's prose ("ES = 50 ... when the price moves by one minimum increment") is muddled; the repo uses it as $ per 1.0 point, which matches the User Manual examples (L1695–1752). The repo reading is normative. | $ per point |
| `PointValue` | double | doc only | not used |
| `Expiration` | DateTime | doc ("for futures") | futures sanity check |
| `TimeZone` | `RequestTimeZone` | verified (`resolveBarTimeZone` L2129–2145) | bar time zone |
| `SessionName` | string | doc only, **VERIFY** value format | log; session template hint |
| `Resolution` | `Resolution` | verified (`_Test6` L110) | log, volume quality |
| `QuoteField` | `RequestQuoteField` (Ask/Bid/Trade) | doc only, **VERIFY** | FX bid/ask charts |
| `StrikePrice`, `OptionType` | | doc only | options → unsupported |

- **IP-1.1 VERIFY the full `ESymbolCategory` member list** (names and spelling) by a one-off probe strategy `_InstrProbe` that prints `Enum.GetNames(typeof(ESymbolCategory))` (PR-I0). Classification therefore keys on **`Category.ToString()` upper-cased**, with the mapping table below tolerant of the likely names, and logs the raw name. Until the probe runs, any name not in the table maps to `Unknown` (IP-2.2).
- **IP-1.2 VERIFY** whether `ASymbolInfo2` (`MTPA_MCSymbolInfo2`, PG §4.2.3; it is ambiguous whether it hangs off `Bars` or `Info`) exposes session times (open/close per weekday). If it does, it is a second source for IP-7; if not, sessions come from the per-class tables (§7). [GAP] default: tables.
- **IP-1.3 VERIFY** whether MC exposes the session list through `Bars.Sessions` (an `ISession` list, a common MC .NET member not found in these PDFs or the repo). Not required by this spec.
- **IP-1.4** Volume type ("Build Volume On" Trade Volume vs Tick Count) is not on `IInstrumentSettings`. `_OrderFlow`'s T7 is a **per-bar** check (Up + Down == Volume, L17–20), not a StartCalc probe, so StartCalc cannot know Tick Count mode. `volumeQuality` is decided at StartCalc from the class only (IP-10.1); a later T7 failure is handled by IP-10.5.
- **IP-1.5 What dxFeed reports is unverified.** Category, Exchange (MIC like `XCME` or name like `CME`), Expiration and Name for continuous `@ES` are all VERIFY until the PR-I0 output exists (IP-0.9).

---

## 2. Classification

- **IP-2.1 Classes.** `enum EInstrumentClass { Unknown = 0, Future = 1, Stock = 2, Etf = 3, Index = 4, SpotFx = 5, Cfd = 6, Crypto = 7, Option = 8, FutureOption = 9, Bond = 10, MutualFund = 11 }`. (Values are fixed; append only.)
- **IP-2.2 Mapping** from upper-cased `Category.ToString()` (substring tests in this order; first hit wins):

| Category name contains | Class | Note |
|---|---|---|
| `FUTUREOPTION` / `FUTURE_OPTION` / `OPTIONFUTURE` | FutureOption | VERIFY name |
| `OPTION` | Option | |
| `FUTURE` | Future | verified ("Future") |
| `FOREX` / `CURRENCY` (not `CRYPTO`) | SpotFx | same rule as `autoTradingDay` L2118 |
| `CRYPTO` | Crypto | same rule as `autoTradingDay` |
| `CFD` | Cfd | |
| `INDEX` | Index | |
| `MUTUAL` / `FUND` | MutualFund | |
| `BOND` | Bond | |
| `STOCK` / `EQUITY` | Stock (ETFs included, IP-2.3) | `Stock` verified (L2740) |
| anything else | Unknown | |

- **IP-2.3 ETF = Stock (Q1, decided).** ETFs are classified Stock in v1; there is no list or description heuristic (the description rule was fragile: "TRUST" matches banks and REITs). Engine support is identical. `EInstrumentClass.Etf` stays in the enum and can be set only by `classOverride` (logged at Warning). If Ettienne decides otherwise in Q1, a list-only rule (`InstrumentConfig.etfSymbols`, exact root match, `etfRule(list)` logged at Warning; never `" TRUST"`) is the variant to add.
- **IP-2.4 Sanity checks** (each failing check writes a `Warning` and is listed in the INSTR line's `warn` field; none throws by itself):
  - **Continuous series** (IP-2.6): `contSeries=1` logged; not a warning.
  - SpotFx with `BigPointValue` not in {1, 100000, 10000, 1000} or tick > 0.01: `fxScale?`.
  - Stock/Etf with tick ≠ 0.01 (and price ≥ 1): `stockTick?`.
  - tick ≤ 0 or BigPointValue ≤ 0: this one **throws** `InstrumentProfileException` (§6.4): no engine can size or round without it.
- **IP-2.5 Unknown.** Class Unknown fails closed (IP-0.8; after IP-0.9 is satisfied): every enabled engine except PA throws with the raw fields in the message (`category <raw>, name <name>, exchange <exchange>, expiration <date>`), so the probe value can be added to the mapping. PA runs Degraded. The only levers are a mapping fix (code PR) or `classOverride` (logged at Warning).
- **IP-2.6 Futures roots and continuous names.** Continuous = `Name` starts with `@` (dxFeed) **or** `Expiration` is the default value. The root parser strips a leading `@` or `/`, a trailing `#` or `:<MIC>` suffix, and a month code + 1–2 digit year (`ESZ26`, `ESZ6`). Root lookup is an **exact** match (MES ≠ ES, MNQ ≠ NQ). Fixtures: `@ES`, `@MES`, `/ESZ26:XCME`, `ESZ26`, `ESZ6` (IP-T7). Expected outputs VERIFY against I0.

---

## 3. `InstrumentConfig` (protected, no inputs)

| Field | Default | Meaning |
|---|---|---|
| `autoDetect` | true | false = use only the explicit fields below, Future defaults for the rest (escape hatch) |
| `classOverride` | null (`EInstrumentClass?`) | forces the class; logged (IP-0.3) |
| `tickOverride` | NaN | replaces MinMove/PriceScale |
| `pointValueOverride` | NaN | replaces BigPointValue (in instrument currency) |
| `quoteCurrency` | null | v1 is USD-quoted only (Q10): null = inferred ("USD" for US futures/stocks; the quote side of an `XXX/YYY` FX name). A non-USD inferred or set value throws (COST and every engine that sizes). No verified MC accessor for symbol currency exists (VERIFY). |
| `rollMode` | null (`ERollMode?`) | §9; null = from class |
| `sessionTemplate` | null (`ESessionTemplate?`) | §7; null = from class/exchange |
| `calendar` | null (`ECalendar?`) | §7; null = from exchange |
| `volumeQuality` | null (`EVolumeQuality?`) | §10; null = from class + flow probe |
| `costModel` | null (`CostModel`) | §8.2; null = from the root/class table; required for CFD/Crypto and unknown future roots (throws otherwise) |
| `sizingUnit` | null (`ESizingUnit?`) | Contracts / Shares / Units / Lots |
| `lotSize` | 0 | min order increment in sizing units; 0 = from class (Future 1, Stock 1, Etf 1, SpotFx 1000) |
| `allowDegraded` | true | false = a "degraded" cell (§8) throws instead of warning |
| `etfSymbols` | empty | only used if Q1 is decided for an ETF list |

- **IP-3.1 `Validate()`** returns null or an error list (DvpConfig pattern). Overrides must be positive and finite. `Canon()` lists every field plus the table version string `INSTR_TABLES_V = "1"`.
- **IP-3.2 `CostConfig`** (new protected config, separate so the floor is visibly opt-in): `enabled` = **false**, `riskBudgetFraction` = 0.01, `maxCostFractionOfR` = 0.10 (equity per §8.2 IP-8.1, from `equityAvailable()`; no `equityBasis`). Off = no object allocated, no line logged, ES output unchanged.
- **IP-3.3** `RollDetection` (existing protected property, L1389) stays the user's switch for *how* futures rolls are detected. `rollMode` (§9) is the new *whether rolls exist* axis.

---

## 4. Profile → engine configuration

- **IP-4.1 `InstrumentProfile`** (immutable, one per used ds): `ds, name, dataFeed, categoryRaw, cls, exchange, tick, pointValue, quoteCurrency, barTz, sessionTemplate, calendar, rollMode, volumeQuality, costModel, sizingUnit, lotSize, overrides[], warnings[], profileHash`.
- **IP-4.2 Engine hook.** Each engine gets a static `Apply(Config c, InstrumentProfile p, Action<string> log)` in its host code (not inside `// <...-core>` math regions), called once in `start*()` before `Validate()`. `Apply` first calls `InstrumentSupport.Check(engineId, p)` (§8), which throws or warns, then writes only the fields that are still at their *unset* sentinel (IP-4.3), then logs one `CFG` line (§5.2).
- **IP-4.3 Explicit engine-config values beat the profile.** To tell "set by the strategy" from "library default", each affected field gets a private `isSet` flag set by its property setter, or the field default becomes a sentinel (NaN / null) resolved by `Apply`. Which mechanism is used per field is listed in the PR. When an explicitly set field differs from the profile's value, one `CFG OVERRIDE` line is written (§5.3). [GAP] default: setter flags (keeps public field types unchanged).
- **IP-4.4 Future = today.** For `cls == Future`, `Apply` writes the exact values each engine uses on `origin/main` today. `_VolumeProfileFuturesBase` keeps its futures defaults (`DEFAULT_RTH_START 09:30`, `DEFAULT_RTH_END 16:15` L58–59, `configureSessions` L153, Globex trading day, `isRthBar` override L512, `IsGlobexClosedDay` L927, `isTradingHoliday` → `_CmeHolidays.Rules.Get(date).Closed` L961–964, `PriceActionConfig.ApplyPreset(EPriceActionPreset.Futures)` in `Create()` L118, `presetExtraPriceAction` L125). Other components (strategies deriving `_CustomSignal` directly, incl. `_VolumeProfileBase`) keep `_CustomSignal`'s general defaults (`isRthBar` = true, `_CustomSignal` L516; `tradingDayKey` = calendar date, or `autoTradingDay` when `TradingDayRoll == Auto`, L2001/L2081) unless the profile says otherwise. M-INSTR never re-routes any futures holiday, RTH or trading-day call.
- **IP-4.5 Futures base on a non-future (Q3, decided).** `_VolumeProfileFuturesBase` throws `EngineNotSupportedException` (engine `VPFUT`, reason 9, action "derive from _VolumeProfileBase instead") only when detection is **positive** for a non-future class. On Unknown (after IP-0.9 is satisfied) it throws with the raw Category/Name/Exchange/Expiration (IP-2.5). It never re-tunes. Per IP-0.9 / E13, PR-I3 cannot merge until the I0 output for `@ES` and `@MES` confirms Category maps to Future.
- **IP-4.6 PriceAction presets.** `enum EPriceActionPreset { Library = 0, Futures = 1 }` (`_PriceActionEngine` L31): there is nothing to map for stocks or FX. The profile does not touch presets; the futures base keeps Futures, everything else keeps Library (Q6 moot; former PR-I7 dropped).
- **IP-4.7 TradingDayRoll coexistence.** The existing `TradingDayRoll` property (`Calendar` default, `Auto` by category, L2001/L2081) is kept. Session templates (§7) supply `tradingDayKey` only when the strategy has not set `TradingDayRoll` explicitly and the class is not Future; an explicit `TradingDayRoll` wins and writes `CFG OVERRIDE ... field(tradingDay)`. The Fx24x5 and Crypto24x7 day rules are the same rules as `autoTradingDay` (no second implementation).

---

## 5. Logging (exact formats)

All lines go through `engineSink(level)` (L1463; `null` when filtered, so no string is formatted). Format: `KEY` then `name(value)` pairs, invariant culture, the style already used (`DAILY closeBasis=...`, `VPF-4 roll splice detected tradingDay(...)`). Doubles use `R` format for tick/pointValue, `0.####` elsewhere. The trace writer adds timestamp, zone, signal, symbol and runId columns itself (L122).

- **IP-5.1 `INSTR`** (level **Info**; also on at Journal, Verbose, Debug), one per used ds, at `StartCalc()`, before any `CFG` line:
  `INSTR ds(1) name(@ES) feed(dxFeed) category(Future) class(Future) exchange(XCME) root(ES) tick(0.25) pointValue(50) ccy(USD) barTz(Exchange:Central Standard Time) session(CmeEquityIndex) calendar(CmeGlobex) roll(Detect:SeriesOrTable) volume(Exchange) cost(off) sizing(Contracts lot=1) contSeries(1) overrides(none) warn(none) profileHash(1a2b3c4d5e6f7a8b)`
  (category/exchange values illustrative, VERIFY from I0.) With `CostConfig.enabled = true`: `cost(PerContract rt=17.00 floorTicks=14)`.
- **IP-5.2 `CFG`** (level **Info**), one per enabled engine, in engine start order:
  `CFG engine(DVP) ds(1) class(Future) support(Supported) source(auto) fields{rollMode=Detect;volWeight=Exchange;sessions=CmeEquityIndex;calendar=CmeGlobex;rowFloorTicks=1} configHash(<engine hash or ->) profileHash(1a2b3c4d5e6f7a8b)`
  - `support` ∈ {Supported, Degraded, Unsupported}; `source` ∈ {auto, override, mixed}; `fields{}` lists only the profile-derived fields (key=value; `;`-separated, fixed order per engine, listed in §8.3). `configHash` = the engine's own existing hash where one exists (EVENT: `EventRules` SHA-256; REGIME: `RegimeGate.Hash`, FNV-1a 64), else `-`. No new engine hash is invented here; DVP's comes with #36.
- **IP-5.3 Override lines.** `INSTR OVERRIDE ds(1) field(cls) detected(SpotFx) used(Future) by(InstrumentConfig.classOverride)` and `CFG OVERRIDE engine(DVP) field(rollMode) detected(None) used(Detect) by(DvpConfig.rollMode)`. Level Warning when IP-0.3 says it contradicts detection, else Info.
- **IP-5.4 Degradation.** `CFG DEGRADED engine(VP) class(SpotFx) reason(tick-volume only: profile weights are tick counts, not traded volume)` at **Warning**, written exactly once per engine per run, right after its CFG line.
- **IP-5.5 Journal echo.** The `INSTR` and `CFG` lines are also written to the journal (Journal level), and `profileHash(...)` is appended to the existing RUN line (L1782). profileHash is **not** an input to any existing configHash in v1 (Q7, decided), so every ES configHash stays identical and doubles as a free bit-identity check.
- **IP-5.6 Errors.** Every `EngineNotSupportedException` / `InstrumentProfileException` message is also written at **Error** before the throw, so it appears in the trace file even if MC only shows the dialog.

---

## 6. Exceptions

- **IP-6.1 Types** (in `_InstrumentProfile.Function.CS`, outside the core region is not needed: they are plain `System.Exception` subclasses):
  - `public class InstrumentProfileException : Exception` — the profile cannot be built (bad tick, bad override, unreadable Info).
  - `public sealed class EngineNotSupportedException : InstrumentProfileException` with read-only properties `Engine` (string id), `Instrument` (name), `Category` (raw), `Class` (EInstrumentClass), `Reason` (string), `Action` (string).
- **IP-6.2 Message format** (single line, exactly):
  `M-INSTR: engine <ENGINE> cannot run on <name> (category <categoryRaw>, class <Class>, exchange <exchange>, ds <n>): <reason>. To fix: <action>.`
  Example: `M-INSTR: engine FLOW cannot run on EUR/USD (category Forex, class SpotFx, exchange FXCM, ds 1): spot FX has no central traded volume or aggressor side; order flow would be fabricated from quote ticks. To fix: disable FlowConfig, or run the idea on the currency future (6E).`
  For class Unknown the reason ends with the raw fields: `(raw category <x>, name <y>, exchange <z>, expiration <d>)`.
- **IP-6.3 When thrown.** Only in `StartCalc()` of the host, inside the engine's `start*()`, i.e. only when the engine is enabled. Never in `CalcBar`. MC then stops the study with the message (existing behaviour of `resolveBarTimeZone` L2140, which throws plain `Exception`; that call is migrated to `InstrumentProfileException` with the same text in PR-I3, not earlier, since PR-I1 is unwired). Category-dependent throws obey IP-0.9.
- **IP-6.4 Degraded → throw.** With `InstrumentConfig.allowDegraded = false`, a Degraded cell throws with reason prefixed `degraded not allowed: `.
- **IP-6.5 No force-run flag (Q4, decided).** Setting `classOverride` changes the class, and therefore the matrix cell, and is always logged at **Warning** (IP-5.3). There is no per-engine "force run" flag.

---

## 7. Sessions, trading day and calendars

- **IP-7.1 Session templates** (`ESessionTemplate`), all in exchange-local wall clock, converted with the existing `ToZone`/`findTz` helpers:

| Template | Trading day | RTH (value window) | Weekly | Default for |
|---|---|---|---|---|
| CmeEquityIndex | 18:00→17:00 ET (GlobexDate) | 09:30–16:15 ET (value end 16:00) | Sun 18:00 ET – Fri 17:00 ET | Future on CME/CBOT/GLOBEX (= today) |
| CmeOther | as above | per-product override (`configureSessions`, L153) | | other CME roots: unchanged behaviour, Warning `rth=default-equity-index` until a product table exists |
| UsEquity | calendar date ET | 09:30–16:00 ET | Mon–Fri | Stock on NYSE/NASDAQ/ARCA/BATS/AMEX (and MIC forms, IP-7.3) |
| UsIndex | calendar date ET | 09:30–16:00 ET | Mon–Fri | Index (cash) on US exchanges |
| Fx24x5 | 17:00 ET → 17:00 ET (NY close; `autoTradingDay` rule L2118: ET+7h date) | none (`isRthBar` true for all bars); optional "London/NY overlap" 08:00–12:00 ET for engines that need a liquid window | opens Sun 17:00 ET, closes Fri 17:00 ET | SpotFx, Cfd on FX |
| Crypto24x7 | UTC date (`autoTradingDay`) | none | 24×7 | Crypto |
| ExchangeLocal | calendar date in exchange tz | from `InstrumentConfig` only; else none, Warning | Mon–Fri | Unknown / non-US stock exchanges |

- **IP-7.2 FX week.** Bars stamped between Fri 17:00 ET and Sun 17:00 ET are "closed market" (should not exist; if present they are logged once and ignored by session statistics). The Sunday open session (Sun 17:00 → Mon 17:00 ET) is trading day *Monday*. A "Sunday bar" never forms its own trading day. Daily engines (M-DAILY, Regime) therefore see 5 days per week, never 6. Test IP-T21.
- **IP-7.3 Calendars** (`ECalendar`):
  - `CmeGlobex` = **today's VPF behaviour, untouched**: `isTradingHoliday` → `_CmeHolidays.Rules.Get(date).Closed` (VPF L961–964, VPF-6 CME closures, not NYSE); RTH masks via `IsNoRthDay` (NYSE via `Holidays`) and `IsGlobexClosedDay` (L927, hook L183). M-INSTR does not re-route any futures holiday call.
  - `Nyse` = existing `_NyseHolidays` (`tools/m-event/nyse_holidays.txt` stays the **single** NYSE source; no second list anywhere).
  - `Fx` = closed only 25 Dec and 1 Jan; 24/31 Dec logged as thin days ([GAP]). Built from those two fixed dates only; it does not include or copy NYSE dates.
  - `None` = Crypto only (24×7).
  - `File(path)` = any other exchange (one ISO date per line, `#` comments, same format as `nyse_holidays.txt`).
  - **Mapping** (case-insensitive **substring** match (`Contains`), the same rule as main's `exchangeTimeZoneId` L2151, under which `XCME`/`XCBT` already resolve to Central today while `XNYM`/`XCEC`/`XNYS`/`XNAS` do not; tokens are tested in the order listed, CME group first; **all VERIFY from I0 output, do not guess**): `CME`, `CBOT`, `CBT`, `GLOBEX`, `NYMEX`, `COMEX`, MIC `XCME`, `XCBT`, `XNYM`, `XCEC` → CmeGlobex; `NYSE`, `NASDAQ`, `ARCA`, `AMEX`, `CBOE`, MIC `XNYS`, `XNAS`, `ARCX`, `XASE`, `BATS`/`XCBO` (Cboe MIC forms, VERIFY) → Nyse; SpotFx class → Fx regardless of exchange; Crypto → None; anything else → `File` if `InstrumentConfig.calendar` names one, else, for **non-Future** classes, **throw** for any enabled engine that counts trading days (VP, DVP, REGIME, EVENT, DAILY) with reason `no holiday calendar for exchange <x>` and action "set InstrumentConfig.calendar" (IP-0.8). For class **Future**, an unmapped exchange only logs Warning `calendar=unmapped exchange <x>` and keeps today's VPF/`_CustomSignal` holiday path (futures never use the profile calendar, IP-7.4). On the time-zone side, an unmapped exchange or MIC never adds a throw beyond today's `resolveBarTimeZone` behaviour. PA, G0 price-only parts and FLOW do not count days and are not affected.
- **IP-7.4 `isTradingHoliday`.** The base `_CustomSignal.isTradingHoliday` (L2759 → `m_holidays.isHoliday`) is kept for Nyse. For Fx/File/None the profile supplies the calendar through the same virtual, only for non-Future classes. The VPF override (L961) is not touched. `_NyseHolidays` stays created **unconditionally in `Create()`** (L1601–1604) as today: `Create()` runs before `StartCalc()`, so it cannot depend on the profile. A lazy variant, if wanted, is a separate PR with its own canary gate (out of scope).
- **IP-7.5 Time zones.** `exchangeTimeZoneId` (L2151) matches CME/CBOT/CBT/GLOBEX/CBOE (Central) and NYMEX/COMEX/NYSE/NASDAQ/ARCA (Eastern) by substring. The profile adds MIC forms (XCME/XCBT → Central; XNYM/XCEC/XNYS/XNAS/ARCX/XASE → Eastern; VERIFY from I0), and: FX feeds → bar zone from `Info.TimeZone` only (Local/GMT); `Exchange` mode on FX throws `InstrumentProfileException` "set the chart to Local or GMT" unless `exchangeTimeZoneId` is overridden. LSE→"GMT Standard Time", XETRA/EUREX→"W. Europe Standard Time", TSX→"Eastern Standard Time", ASX→"AUS Eastern Standard Time" added to the table ([GAP], VERIFY MC's exchange abbreviations in QuoteManager "Exchanges & ECNs").
- **IP-7.6 M-EVENT.** The economic calendar (`_EventCalendar`, FOMC/CPI/NFP/Claims/ISM..., ET wall clock, NYSE-holiday rules L250–275) is a **US macro** calendar.
  - Supported: Future on **CME equity-index roots** (ES, MES, NQ, MNQ, YM, MYM, RTY, M2K; exact root match); US Stock; US Index.
  - Other futures (CL, GC, ZN, ...): Supported for the macro kinds, but the expiry kinds **RollWeek, QuarterlyExpiry, SessionsToExpiry / IsRollDay** are ES-specific (quarterly third-Friday rule) and are **absent** on these roots, never guessed (EV-4.7 style; logged once in CFG `usOnly=1 expiryKinds=absent`). EIA (Energy) stays as today.
  - SpotFx with USD on either side: Degraded (US events only, as a filter; Q11 decided). Expiry kinds absent.
  - SpotFx without USD, CFD, Crypto: Unsupported (Q11, decided); in v1 non-USD-quoted FX already throws by Q10.
  - Every rollMode None class: expiry kinds absent.
  - Unknown: Unsupported.

---

## 8. Support matrix, costs and sizing

### 8.1 Engine × class matrix (normative)

S = Supported, D = Degraded (runs, `CFG DEGRADED` Warning, reason shown), U = Unsupported (throws, reason shown). Classes: Fut = Future, Stk = Stock, ETF, Idx = Index (cash, not tradeable), FX = SpotFx, CFD, Cry = Crypto, Opt = Option/FutureOption, Bnd/MF = Bond/MutualFund, Unk = Unknown.

| Engine (id) | Fut | Stk (incl. ETF) | Idx | FX | CFD | Cry | Opt | Bnd/MF | Unk |
|---|---|---|---|---|---|---|---|---|---|
| PriceAction (PA) | S | S | S | S | S | S | U¹ | D² | D² |
| _RegimeEngine (REGIME) | S (roots ES, MES only; other roots U³) | U³ | U³ | U³ | U³ | U³ | U¹ | U³ | U⁵ |
| M-EVENT (EVENT) | S⁴ | S | S | D⁴ (USD pairs) | U⁴ | U⁴ | U¹ | U⁴ | U⁵ |
| Gate0 / _ProfileMath (G0) | S | S | D⁶ | U⁷ | U⁷ | U⁷ | U¹ | U⁸ | U⁵ |
| _VolumeProfileBase (VP) | S | S | U⁶ | D⁷ᵛ | D⁷ᵛ | D⁷ᵛ | U¹ | U⁸ | U⁵ |
| _VolumeProfileFuturesBase (VPFUT) | S | U⁹ | U⁹ | U⁹ | U⁹ | U⁹ | U⁹ | U⁹ | U⁵ |
| Order flow (FLOW) | S | U¹⁰ | U⁶ | U¹¹ | U¹¹ | D¹² | U¹ | U⁸ | U⁵ |
| _RollDetector (ROLL) | S | U¹³ | U¹³ | U¹³ | U¹³ | U¹³ | U¹ | U¹³ | U⁵ |
| M-DVP (DVP) | S | S¹⁴ | U⁶ | U⁷ | U⁷ | U⁷ | U¹ | U⁸ | U⁵ |
| Cost floor / sizing (COST, opt-in) | S¹⁷ | S | U¹⁵ | S | U¹⁶ | U¹⁶ | U¹ | U¹ | U⁵ |
| Any engine, non-USD quote (v1) | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ | U¹⁸ |

**The matrix is data.** It is one static table `InstrumentSupport.s_matrix[engine, class] = {level, reasonId}` in `<instr-core>`, plus the reason strings below. Each decision-dependent cell is tagged with its Q number in a comment. Changing any Q1–Q11 decision changes only table cells and reason strings (and the fixture goldens of IP-T15); no engine code changes. Cells marked U whose condition depends on Category are Warning-only until IP-0.9 is satisfied.

Reasons and actions (these strings are the `reason` / `action` of §6.2):
1. Options: price is a function of the underlying and time; engines assume a continuous liquid series. Action: run on the underlying.
2. Bond/MF/Unknown: PA uses no class-specific facts; runs with Warning. (Every other engine throws on Unknown, IP-2.5, reason 5.)
3. Regime thresholds were fitted on ES; on other instruments they would produce unvalidated statistics (Q8, decided). Action: refit Regime for this instrument and add it to the fitted-roots list.
4. US macro events only (IP-7.6). Future: S on CME equity-index roots; other roots S without the expiry kinds (IP-7.6).
5. Unknown class: the engine needs a class-specific fact. Message shows the raw fields (IP-2.5). Action: report the raw Category so the mapping can be fixed, or set `InstrumentConfig.classOverride` (Warning).
6. Cash index has no traded volume (MC usually reports 0 or an aggregate). Action: use the index future or the ETF. (Gate0 is D on Index only for its price-only parts: ATR/levels run; volume parts return NaN with Warning.)
7. Tick volume only (§10): DVP and Gate0 statistics were fitted on exchange volume and are not validated on tick counts (Q5 decided: Unsupported). 7ᵛ = VP runs **Degraded as a visual/level tool only**: its levels are "activity" levels, are flagged `visualOnly=1`, and nothing from it feeds Holm or a consumer study.
8. Bonds/MF: no intraday volume profile meaning (MF one NAV per day).
9. Futures-only base (Globex day, CME RTH, roll handling). Action: derive the strategy from `_VolumeProfileBase` (§13.1 Q3).
10. Stocks/ETFs: consolidated tape, off-exchange prints, no validated aggressor classification; no stock tape study exists (Q2 decided: Unsupported). Action: disable FlowConfig on stocks until a tape study is done.
11. Spot FX/CFD: no central tape, no trade sizes, no aggressor; any delta would be fabricated from quote ticks. Action: disable FlowConfig or use the currency future (6E, 6J...).
12. Crypto: single-venue trades are real but cover one venue only. Warning.
13. No contract rolls on this class. Action: leave `RollDetection` Off; engines use `rollMode = None` (§9). Enabling `RollDetection` on a non-future throws with this reason.
14. Supported via the rollMode None amendment (§9); volume weights are real consolidated volume.
15. Cash index is not tradeable. Action: trade the future or the ETF.
16. CFD/Crypto: no default cost model (spread + financing vary by broker; Q9 decided). Throws unless `InstrumentConfig.costModel` is set, which turns the cell into S.
17. Future: S for roots in the cost table (IP-8.4); an unknown root throws unless `costModel` is set.
18. v1 is USD-quoted only (Q10, decided); there is no verified MC accessor for symbol currency.

### 8.2 Per-instrument cost model (generalises catalogue §2.1)

- **IP-8.0 What exists today.** No cost floor and no widen exist on main. The only related code is `CostFilterMaxFractionOfR` (default **0.0 = off**) with `costFilterPasses` (L2500–2528), which **skips** a trade when round-trip cost / R exceeds the fraction. Therefore the floor below is **new behaviour** and is **opt-in**: `CostConfig.enabled = false` by default (lazy: nothing is allocated or logged when off). With it off, ES output is unchanged. Turning it on is a deliberate behaviour change reviewed on its own run.
- **IP-8.0a Relation to `costFilterPasses`.** When `CostConfig.enabled` is true, the floor replaces the cost filter: `Validate()` rejects `CostFilterMaxFractionOfR > 0` together with `CostConfig.enabled` ("use one cost rule"), so the two are mutually exclusive. With the floor off, `costFilterPasses` behaves exactly as today. [GAP] alternative in §13.1 note.
- **IP-8.1 Rule (Ettienne, 2026-10-23; applies only when the floor is on).** stop distance = max(structural stop, cost floor) — e.g. ES: max(structural, 14 ticks). A structural stop shorter than the floor is widened to it. q comes from the strategy's **existing** sizing on the structural stop and is **never reduced**. Skip (and log) iff q × widened stop × pointValue > `riskBudgetFraction` (0.01) × equity. ES: skip iff q × widenedStopPoints × $50 > 0.01 × equity. **Equity** = the value `_CustomSignal.equityAvailable()` (L2010) computes before applying its percentage: account equity when available, else Chart = `InitialCapital + NetProfit` (L2048), Portfolio Trader = `InitialCapital + Portfolio.NetProfit` (L2061). Open equity is excluded, as in the existing sizing. There is no `equityBasis` setting; the chart/PT split already lives in that method. **Consequence (R4):** for a strategy that sizes q = floor(0.01 × equity / (structural × pointValue)), any widening above the structural stop exceeds 1% (apart from rounding slack), so with the floor on, "stop shorter than 14 ticks" effectively becomes "skip". The floor widens without skipping only for fixed-size strategies (e.g. `_TestGate0`, 1 contract) or sizing below 1% risk. This is intended (Ettienne 2026-10-23: never shrink).
- **IP-8.2 `CostModel`** (immutable, in `<instr-core>`): `perUnitRoundTrip` ($ per contract/share/unit, incl. commissions and fees), `perOrderRoundTrip` ($ per order pair, e.g. IBKR fixed minimums), `spreadTicks` (half-spread × 2, in ticks, paid per round trip), `slippageTicks` (per round trip), `maxCostFractionOfR` (default 0.10, the §2.1 "costs ≤ 0.10 R"), `borrowBpsPerDay` (shorts on Stock/Etf, report-only in v1).
- **IP-8.3 Floor formula.** cost per unit C(q) = perUnitRoundTrip + perOrderRoundTrip/q + (spreadTicks + slippageTicks)·tick·pointValue. floorTicks = ceil( C / (maxCostFractionOfR · tick · pointValue) ). **ES default** (matches the `_TestGate0` canary settings, L124–128, PR #30): perUnitRoundTrip 4.50 (2 × $2.25), spreadTicks 0, slippageTicks 1 (0.5/side × 2) → C = 4.50 + 12.50 = **$17.00**; floorTicks = ceil(17.00 / 1.25) = ceil(13.6) = **14**. The catalogue's $16.98 also gives 14. The other six roots keep the catalogue's $ figures (MES 2.47, MNQ 1.72, NQ 9.48, CL 14.72, GC 15.02, ZN 18.95) until their commissions are confirmed (Q9). IP-T50 checks all seven floors (MES 20, MNQ 35, ES 14, NQ 19, CL 15, GC 16, ZN 13).
- **IP-8.4 Future cost table.** The catalogue's seven roots (MES, MNQ, ES, NQ, CL, GC, ZN) are entered verbatim as a per-root table keyed by the symbol root (Name minus month/year code; **VERIFY** the root parser on continuous names like `@ES#`). Lookup is an exact root match (IP-2.6). An unknown future root **throws** in COST unless `InstrumentConfig.costModel` is set (no silent fallback to Strategy Properties; `roundTripCostPerContract` L2507 stays what the cost filter uses).
- **IP-8.5 Stock/ETF.** `perUnitRoundTrip` = 2 × 0.0035 $/share (IBKR fixed, [GAP] VERIFY plan), `perOrderRoundTrip` = 2 × 0.35 $ (minimum), spreadTicks = 1, slippageTicks = 1, tick 0.01, pointValue 1. Because perOrder depends on q, the floor is computed at the size that the **unfloored** risk rule gives (IP-8.7) and not iterated. Shorts add a Warning when `borrowBpsPerDay` is unknown.
- **IP-8.6 SpotFx.** perUnit = IBKR 2 × $2 per 100k → 0.00004 $/unit ([GAP], Q9), spreadTicks 2, slippageTicks 1. USD-quoted pairs only in v1 (Q10), so pointValue is already in USD.
- **IP-8.7 Sizing units.** `ESizingUnit`: Future Contracts, Stock/Etf Shares, SpotFx Units (lot from `lotSize`, default 1000), Crypto Units (fractional allowed: lot 0.0001 [GAP]). q is whatever the strategy's existing sizing gives on the **structural** stop (for a risk-based strategy, floor_to_lot(riskBudgetFraction · equity / (structuralStop · pointValue))); then the floor widens the stop (IP-8.1) and the skip test uses that q at the widened stop. For 1%-risk sizing this means any widening skips (IP-8.1 consequence). Stocks use the existing round-lot policy (`applyRoundLots` L2531: floor to 100 when q ≥ 100) unchanged. q = 0 after lot rounding → skip with reason `size<1 lot`.
- **IP-8.8 Logging.** Each skip writes the existing signal-journal skip with reason `cost-floor widen breaches risk budget: stop <a>→<b> ticks, risk <$> > <frac>·equity <$>`.

### 8.3 CFG `fields{}` per engine (fixed order)

PA: `preset;tick`. REGIME: `dailyClose;sessionsPerYear`. EVENT: `calendar;tz;usOnly`. G0: `tick;rowTicks;rth;volume`. (Hashes per IP-5.2.) VP: `tick;rth;tradingDay;volume;calendar`. VPFUT: `rthStart;rthEnd;tradingDay`. FLOW: `sourceCap;mcBidAsk`. ROLL: `rollMode;detection`. DVP: `rollMode;volWeight;sessions;calendar;rowFloorTicks`. COST: `model;floorTicks;sizing;lot;riskBudget`.

---

## 9. Rolls: `rollMode` and the M-DVP rev 4 amendment

- **IP-9.1** `enum ERollMode { Detect = 0, None = 1 }`. Auto: Future → Detect; every other class → None. `Detect` uses the existing `RollDetection` (Series/Table/SeriesOrTable). `None` means "this instrument has no contract rolls by nature": `rollOn(ds)` is false (as today with Off), `IsRollWindow` is false, and **no stream is downgraded** for it.
- **IP-9.2** Future with `RollDetection == Off` stays exactly as today: `rollMode = Detect` but nothing detected, the existing Warning (L1432), and DVP marks streams diagnostic-only (DV-7.0 unchanged). This keeps ES bit-identical.
- **IP-9.3** Non-Future with `RollDetection != Off` throws (`ROLL`, reason 13). Non-Future with `rollMode` forced to Detect throws likewise.
- **IP-9.4 M-DVP amendment (applied as DV-7.0 in `m-dvp-spec.md` rev 5 DRAFT, pending Ettienne review).** **Depends on the DVP host wiring (`DvpConfig` host property, `startDvp`), which is not on main (pending #36).** The DVP part of PR-I5 cannot land before that PR.
  > **DV-7.0 Roll detection.** If `profile.rollMode == Detect` (futures) and `RollDetection == Off`, `startDvp` logs one Warning "DVP: RollDetection Off — roll skip disabled" and marks every stream diagnostic-only (`DVP RUN rollDetect=Off`, skipShare NaN); such a run cannot feed Holm. If `profile.rollMode == None` (stocks, ETFs, spot FX, crypto; IP-9.1), no window can cross a roll: `crossesRoll` is always 0, skipShare is **0** (not NaN), `diagnosticOnly` is decided by the other criteria only, `DVP RUN` reports `rollDetect=None`, and the stream is tradeable. For ds ≥ 2, `rollOn(ds)` is valid only for the same root as Data1 (via `m_rollPending`); **a different root on Data2/3 is rejected in `Validate()`** (kept from rev 4). The rollMode of that stream's own profile applies; a Future Data2 under a non-future Data1 is rejected in `Validate()` (no detector for it).
  Consequential edits: DV-7.4 ("skipShare (0 when rollMode None)"), DV-11.3 (`rollDetect` ∈ {Series, Table, SeriesOrTable, Off, None}), DV-1.7 unchanged, DV-T27 unchanged, new DV-T27b (rollMode None: skipShare 0, not diagnostic, tradeable), DV-T27c (non-future with RollDetection on throws). DV-1.8 ("ES prior 0") reads "prior 0 for every instrument".
- **IP-9.5** Other roll consumers (AVWAP, PRICEACTION OnRoll, Gate0 roll-adjusted RTH close, VPF-4 splice) need no change: with None they behave as with Off today, minus the Warning (which L1432 already only prints for "Future").

---

## 10. Volume quality

- **IP-10.1** `enum EVolumeQuality { Exchange = 0, Consolidated = 1, TickCount = 2, None = 3, Unknown = 4 }`. Auto: Future → Exchange; Stock/Etf → Consolidated; SpotFx/Cfd → TickCount; Crypto → Exchange (single venue, `venueOnly=1`); Index → None; Unknown → Unknown. Decided at StartCalc from the class only (IP-1.4).
- **IP-10.2 Volume profile on TickCount:** VP runs Degraded, visual/level only (reason 7ᵛ). Gate0 on TickCount is Unsupported (reason 7, Q5). Thresholds expressed in absolute volume (if any; **VERIFY** in `_VolumeProfileBase`/`Gate0` for fixed contract-count constants) are disabled with Warning; relative ones (HVN/LVN ratios, VA 70%) are kept.
- **IP-10.3 M-DVP on TickCount:** Unsupported (reason 7, Q5 decided). If Q5 is changed to Degraded, only the matrix cell changes; DVP would then label `volWeight=TickCount` in its journal lines (#36). **None** (Index): DVP throws (reason 6), because DV-4.2 would deposit zeros.
- **IP-10.4 Order flow on TickCount/None:** throws (FLOW, reason 11 or 6). On Consolidated (stocks): throws (reason 10, Q2 decided).
- **IP-10.5 Late T7 failure.** If an engine assumed `volumeQuality = Exchange` at StartCalc and `_OrderFlow`'s per-bar T7 later fails (Tick Count mode), the engines that read volume latch: FLOW drops to `Unavailable` (existing behaviour), VP/G0/DVP log one Warning `CFG LATE engine(<id>) volume(TickCount) bar(<time>)` and mark their output from that bar `volumeSuspect=1`. They do not throw mid-run (IP-6.3 keeps throws in StartCalc). Futures runs with Trade Volume never fail T7, so ES is unaffected.

---

## 11. ES bit-identical guarantee and test plan

- **IP-11.1 Gate.** The MC-free suites verify **core math only**; none of them compiles `_CustomSignal` wiring (only PriceActionTests and SessionClockTests reference it). So the bit-identity proof is the canary:
  - **IP-T46 ES golden canary.** `_TestGate0` (the Variant set used for G0-b: Canary plus every Variant listed in its header) on dxFeed `@ES` 1-minute, plus the 1-tick window, with the canary cost settings ($2.25/side, 0.5 tick/side). Before and after **every** PR, export the Strategy Performance trade list and the journal CSV. Gate = SHA-256 of the trade list (entry/exit time, price, contracts, P&L, in a fixed CSV format) **identical**, both hashes recorded in the PR description. Journal diff identical after stripping only lines whose key is `INSTR` or `CFG` (and the `profileHash(...)` field of RUN).
  - MC-free suites run before and after every PR, byte-identical: AvwapTests, CalendarTests, DailyTests, Dvp, EventTests, Gate0Tests, OrderFlowTests, PortMMTests, PriceActionTests, ProfileTests, RegimeGateTests, RegimeTests, RollTests, SessionClockTests, VpHelperTests (plus GmmTests, LevelStudyTests, MtfTests, TickTests, TodTests, XmktTests, which exist on main).
  - Any diff other than the stripped lines blocks merge.
- **IP-11.2 Fixtures** (`tests/InstrumentTests/fixtures/*.txt`, one line per `IInstrumentSettings` snapshot: name, category, exchange, minMove, priceScale, bigPointValue, expiration, timeZone, description): `@ES`, `@MES`, `/ESZ26:XCME`, `ESZ26`, `ESZ6`, NQ, CL, GC, ZN, 6E (each with the CME-name and MIC exchange variants); AAPL (XNAS and NASDAQ), SPY (ARCX; classified Stock), a $0.50 penny stock; EUR/GBP (non-USD quote → throws); EUR/USD (5-decimal, BPV 1 and BPV 100000 variants), USD/JPY (3-decimal); BTC/USD (Crypto); SPX (Index); an option; a nonsense category string → Unknown; tick 0 → throw. **VERIFY** the real values by running `_InstrProbe` on each symbol in Ettienne's MC and pasting its output into the fixtures (PR-I0).
- **Test IDs** (MC-free unless stated):
  - IP-T1 classify every fixture; IP-T2 unknown category → Unknown + Warning; IP-T3 disabled engine never throws on any class; IP-T4 tick ≤ 0 throws `InstrumentProfileException`.
  - IP-T5 ETF = Stock (SPY → Stock; `classOverride = Etf` logged at Warning); IP-T6 overrides: each field override logged once, classOverride always Warning. IP-T7 root parser (IP-2.6) incl. exact match MES ≠ ES. IP-T8 MIC mapping (XCME/XNYS/...); unknown exchange with a day-counting engine throws for non-futures only. IP-T9 Category-dependent cells are Warning-only while `probePending` (IP-0.9). IP-T9b Unknown with `probePending`: one `probe-pending` Warning per engine, no throw, VPFUT runs on futures defaults; Future on an unmapped exchange: Warning, no throw (IP-7.3).
  - IP-T10 INSTR line exact text for ES fixture (golden string); IP-T11 CFG line field order per engine; IP-T12 profileHash (16 hex, `EventRules.Sha256`) stable across runs and changes when any Canon field changes; existing configHashes unchanged (Q7); IP-T13 null sink formats nothing (allocation counter, `engineSink` pattern).
  - IP-T15 matrix: for every (engine, class) cell, `InstrumentSupport.Check` returns S/D/U as §8.1; U throws `EngineNotSupportedException` with the §6.2 text (golden per reason); D with `allowDegraded=false` throws.
  - IP-T20 FX trading day: Sun 17:05 ET bar → Monday key; Fri 16:55 ET → Friday; 5 keys per week. IP-T21 no Saturday/Sunday trading day. IP-T22 UsEquity: 09:30–16:00 RTH, NYSE holidays skipped, half days from the session clock. IP-T23 calendar File loader; IP-T24 Fx calendar (25 Dec, 1 Jan).
  - IP-T30 rollMode None: DVP skipShare 0, tradeable (= DV-T27b); IP-T31 ES with RollDetection Off unchanged (= DV-T27); IP-T32 non-future + RollDetection on throws (= DV-T27c).
  - IP-T35 TickCount: VP runs Degraded with `visualOnly=1`; G0, DVP and FLOW throw; Index DVP throws. IP-T36 late T7 failure latches once (IP-10.5). IP-T37 CmeGlobex holidays still from `_CmeHolidays.Rules` (VPF override untouched). IP-T38 EVENT expiry kinds absent on CL and on Stock.
  - IP-T40..T45 MC-free suites byte-identical (IP-11.1); IP-T46 ES `_TestGate0` canary SHA-256 identical (the binding gate).
  - IP-T50 cost floors reproduce catalogue L110 (MES 20, MNQ 35, ES 14, NQ 19, CL 15, GC 16, ZN 13). IP-T51 floor off (default): no widen, no skip, ES identical. Floor on: stop = max(structural, floor); skip iff q × widened stop × pointValue > 0.01 × equity; q never reduced. Risk-sized case: q = floor(0.01 × equity / (structural × $50)) with structural 8 ticks and the floor on → expects skip (`cost floor exceeds risk budget`); same with q = 1 fixed → widen to 14 ticks, no skip. IP-T51b floor on + `CostFilterMaxFractionOfR > 0` rejected by Validate. IP-T51c CFD/Crypto/unknown root without costModel throws. IP-T52 stock sizing: shares, round-lot rule, perOrder minimum at small q. IP-T53 FX units and lot rounding; q=0 → skip.
  - IP-T60 (MC, manual) `_InstrProbe` on ES, AAPL, SPY, EUR/USD in Ettienne's MC: INSTR line printed, values match fixtures.

---

## 11a. Portfolio Trader

- **IP-11a.1** In Portfolio Trader each symbol gets its own `_CustomSignal` instance, so the profile, the `INSTR`/`CFG` lines and the matrix check are **per instance**. A mixed-class portfolio runs one matrix check per symbol; there is no cross-symbol fallback (a throwing symbol stops that instance).
- **IP-11a.2** A PMMS (`_PortMM`) may change contracts after the signal. The never-shrink rule and the skip test (IP-8.1) apply **at the signal**; PMMS changes are logged by M-PORT, not M-INSTR.
- **IP-11a.3** VP chart data is likely unavailable in PT (capabilities §1.8, U) and Bar Magnifier is unavailable in PT; engines that need them keep their existing PT behaviour. M-INSTR adds no PT-specific fallback. Equity in PT is `InitialCapital + Portfolio.NetProfit` via `equityAvailable()` (L2061), per IP-8.1.

## 12. PR plan (small, separate, each reviewed and merged by Ettienne)

| Order | PR | Content | Behaviour change | Tests |
|---|---|---|---|---|
| 1 | PR-I0 | `_InstrProbe` strategy: prints every `IInstrumentSettings` field, `Enum.GetNames(typeof(ESymbolCategory))`, and `ASymbolInfo2`/session members found by reflection. Ettienne runs it on `@ES`, `@MES` and one symbol per class; output becomes the fixtures. No engine touched. | none | IP-T60 (manual) |
| 2 | PR-I1 | `_InstrumentProfile.Function.CS` core: enums, classifier, root parser, `InstrumentConfig`/`CostConfig` (Validate/Canon), `InstrumentProfile`, exceptions, matrix table. `tests/InstrumentTests`. **Not wired.** | none | IP-T1–T9, T15 |
| 3 | PR-I2 | Wire `buildProfile(ds)` in `_CustomSignal.StartCalc()`; INSTR line; `profileHash` on RUN, kept **outside** every configHash. Records the first IP-T46 canary golden hash. | log only | IP-T10–T13, T40–46 |
| 4 | PR-I3 | Matrix checks + throws + `Apply`/CFG for PA, REGIME, EVENT, G0, VP, VPFUT, FLOW. Futures = identity. `resolveBarTimeZone` → `InstrumentProfileException`. **Cannot merge before I0 confirms `@ES`/`@MES` Category = Future** (IP-0.9). | non-futures only (throws) | IP-T11, T15, T35–38, T46 |
| 5 | PR-I4 | Sessions/calendars for non-futures only: templates, Fx24x5, UsEquity, Fx/File calendars, MIC tz table, TradingDayRoll coexistence. **No futures holiday re-route.** | non-futures only | IP-T20–T24, T37, T46 |
| 6 | PR-I6 | Cost model + sizing units, **opt-in** (`CostConfig.enabled = false`), mutual exclusion with `CostFilterMaxFractionOfR`, skip logging; catalogue §2.1 note in the same PR. | none unless enabled | IP-T50–T53, T46 |
| 7 | PR-I5 | rollMode + ROLL checks; M-DVP rev 5 spec edit (§9.4); DVP code change. **After the DVP host PR (#36).** | non-futures only | IP-T30–T32, DV-T27b/c, T46 |

PR-I7 (PriceAction presets) is dropped: `EPriceActionPreset` has only Library and Futures (IP-4.6). Every PR attaches the IP-T46 before/after canary hashes.

Each PR description lists: the rules it implements, the VERIFY items it closes, and the IP-11.1 diff report.

---

## 13. Decisions and open items

### 13.1 Decisions (Ettienne Hugo, 2026-10-24)

Ettienne: "Alright, use the recommendations on the above 11 questions. Keep the exception on the stocks if the engine will not correctly analyse them." Each answer below is the decided behaviour written into this spec. A later change still touches only the §8.1 matrix cells and reason strings (plus their goldens), not engine code.

- **Q1 ETF detection.** **DECIDED (Ettienne Hugo, 2026-10-24).** ETF counts as Stock in v1; no heuristic; `classOverride = Etf` only as explicit opt-in (IP-2.3).
- **Q2 Order flow on stocks/ETFs.** **DECIDED (Ettienne Hugo, 2026-10-24).** Unsupported: FLOW throws on stocks/ETFs (reason 10). The engine cannot correctly analyse the fragmented, off-exchange stock tape: its thresholds are ES-tuned and there is no stock evidence. Revisited only after a stock tape study.
- **Q3 Futures base on non-futures.** **DECIDED (Ettienne Hugo, 2026-10-24).** Throw, only on positive non-future detection; Unknown also throws (after IP-0.9 is satisfied), with the raw Category/Name/Exchange/Expiration shown; never re-tune (IP-4.5).
- **Q4 Force-run flag.** **DECIDED (Ettienne Hugo, 2026-10-24).** No per-engine force flag; `classOverride` is the only lever and is always logged at Warning (IP-6.5).
- **Q5 Spot FX volume engines.** **DECIDED (Ettienne Hugo, 2026-10-24).** DVP and Gate0 Unsupported on FX (tick volume); VP Degraded, visual/level only, nothing feeds Holm.
- **Q6 PriceAction presets for stocks/FX.** **DECIDED (Ettienne Hugo, 2026-10-24).** Moot; `EPriceActionPreset` has only Library/Futures; PR-I7 dropped.
- **Q7 configHash.** **DECIDED (Ettienne Hugo, 2026-10-24).** profileHash stays separate from every configHash in v1 (logged on INSTR/CFG/RUN); ES configHashes stay identical.
- **Q8 Regime on non-ES.** **DECIDED (Ettienne Hugo, 2026-10-24).** Regime throws on anything other than the ES-fitted roots (ES, MES) until refit for that instrument.
- **Q9 Cost numbers.** **DECIDED (Ettienne Hugo, 2026-10-24).** ES $17.00 ($2.25/side + 0.5 tick/side) stays, subject to Ettienne checking it against his IBKR plan; other roots keep catalogue figures; CFD and crypto must supply a `costModel` or COST throws.
- **Q10 Non-USD instruments.** **DECIDED (Ettienne Hugo, 2026-10-24).** USD-quoted only in v1; anything else throws.
- **Q11 M-EVENT on FX.** **DECIDED (Ettienne Hugo, 2026-10-24).** Degraded (US events as a filter) on USD pairs; Unsupported otherwise; expiry kinds absent on non-futures.

Note (not a Q): IP-8.0a makes the cost floor and `CostFilterMaxFractionOfR` mutually exclusive; the alternative (floor replaces the filter outright) is a one-line change if Ettienne prefers it.

### 13.2 [GAP] defaults and out of scope

- [GAP] Fx calendar holidays (25 Dec, 1 Jan); FX trading day 17:00 ET; crypto lot 0.0001; MIC/exchange tz additions (IP-7.5); setter flags for explicit-set tracking (IP-4.3); session tables used instead of MC session data until IP-1.2 is verified; FX commission default (IP-8.6).
- Out of scope: non-US economic calendars; per-product CME RTH tables beyond the existing `configureSessions` override; borrow cost modelling; options engines.

### 13.3 VERIFY list

All Category-dependent items are closed by the PR-I0 probe (IP-0.9): `ESymbolCategory` member names other than `Stock`; what dxFeed reports for Category / Exchange (name vs MIC) / Expiration / Name on `@ES` and `@MES`; `ASymbolInfo2` location and session members (IP-1.2); `Bars.Sessions` (IP-1.3); `SessionName` / `QuoteField` values; symbol currency accessor (IP-3, Q10). Others: IBKR plan numbers (Q9); absolute-volume constants in VP/Gate0 (IP-10.2); root parser on real names (IP-2.6).

Resolved in rev 2 (were VERIFY in rev 1): `EPriceActionPreset` = {Library, Futures} (`_PriceActionEngine` L31); hash routines (`EventRules.Sha256` L424, `RegimeGate.Hash` L208 FNV-1a 64); `_NyseHolidays` created unconditionally in `Create()` (L1601–1604).
