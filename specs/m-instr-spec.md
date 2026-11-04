# M-INSTR spec: per-instrument profile layer (InstrumentProfile)

**Rev 2.4, 2026-11-04 (ET). APPROVED for implementation; M0–M3 merged.** Rev 2.4 re-bases the spec on merged `origin/main` @ **`5c9ccd2`** (merge of PR #97, PR-M3; first-parent merges #89 `6853398`, #90 `d2b41b7`, #91 `670e28d`, #92 `63cdff0`, #93 `ecc5436`, #95 `7ec8c95`, #96 `4500c67`, #94 `ed3e4a5`, #97 `5c9ccd2`). Every cite marked **@5c9ccd2** was read on that commit; `_InstrumentProfile.Function.CS` is abbreviated **IP**, `_CustomSignal.Strategy.CS` **CS**, `tests/InstrumentTests/Program.cs` **IT** (InstrumentTests: 668 passed, 0 failed on `5c9ccd2`, .NET 8, box). Where the code and this spec differ, the difference is flagged **DIVERGENCE** (§12.5). Older cites below are kept as history and say which commit they were read on. Cost keying (§12.4 D1) was DECIDED by Ettienne on 2026-11-04: costs are keyed by asset class + root (`Future:ES`), implemented in PR-M4. **D2** (profileHash over sessions in exchange time) was DECIDED by Ettienne on 2026-11-04, implemented in PR-M4 (§6.1). **MC verification 2026-11-04 (Ettienne, `5c9ccd2`):** MI-T1 PLEditor compile passed, `eMTPA_SF_Root` / `eMTPA_MCSC_USD` no longer VERIFY; the 6E MC run reproduced its profileHash byte-for-byte on .NET 8; the Portfolio Trader per-symbol test passed (§12.3). History of the rev 2.3 header follows. **Rev 2.3, 2026-11-03 (ET). APPROVED for implementation: all questions decided (Q1–Q10, N1–N6).** **Rev 2.3 is final and ships with PR-M1b** (#90 commits rev 2.2 byte-identical). Rev 2.2 was based on rev 2.1 as merged in `origin/main` @ `6853398` (PR #89, PR-M0; #88 merged as `cd7e50a`) and adds the PR-M0 dump results (§4.4.2). Data inputs still pending from Ettienne: Q3 IBKR per-side fees for the non-ES/MES roots and the Q7 bid/ask start dates; they do not block PR-M0 to PR-M3. Checked read-only against `github.com/ettiennehugo/MultiCharts.Net` `origin/main` @ `7dbdb1b` (merge of PR #85). PR #88 ("Land #86 + #87 on main") was OPEN at rev 2.1 and has since merged (`cd7e50a`); its code is quoted where marked "#88 head", and `_MtfMath.SymbolRoot` was at L108–119 (doc L108, method L109–119) on `6853398`, i.e. **main before #90**; after #90 (PR-M1a, head `a265f4d`) it is doc L108–119, method **L120–135**. Every file:line below was read on `7dbdb1b` unless marked **VERIFY** or "#88 head".

**Prerequisite: PR #88 merges before any M-PR.** #88 changes `_MtfMath` (around `SymbolRoot`, L29–39), `_CustomSignal` L1731–1830 (incl. the same-root check L1776–1785), `_DvpEngine`, the DVP golden fixtures and configHash. After it merges, `_CustomSignal` line numbers from about L1740 shift by up to about 50 lines; each M-PR re-reads its cites on the main it targets. Every M-PR targets `main` and is opened only after the previous one merged (no stacked PRs, §11).

**Sources:** the repo (every `*.CS`, `specs/*.md`, `tests/*`); `specs/instrument-profile-spec.md` rev 3 (2026-10-24, rev-3 Q1–Q11 DECIDED); `specs/m-dvp-spec.md` rev 6; `specs/m-port-spec.md` rev 2.7; the MultiCharts .NET Programming Guide, "Instrument properties" list (just before §4.2.4 StatusLine; box copy, not in the repo); Ettienne's typed API dump output `_McApiDump3.output.txt` (@ES, 2026-11-01; **box copy only, not in the repo**, and produced by a reflection dump, so it is cited as Ettienne's run output, not as a repo file; PR-M0 commits typed dump output as a fixture); CME contract-spec pages read 2026-11-02 (§8); the box research note `/workspace/m-instr/mc-sessions-research.md` (MC sessions: Programming Guide v1.1 §4.2.2–4.2.3, §4.7.4, MC wiki and forum staff answers, MC-1439; 2026-11-02); Ettienne's rulings (ES $17 and MES $2.50 per round trip, 2026-11-01; sessions from the chart, 2026-11-02; Q3–Q10 accepted, 2026-11-02).

**Why a new file.** Rev 3 treats "Future = today's ES behaviour" as an identity (IP-4.4) and classifies on `Category.ToString()` substrings (IP-1.1, IP-2.2). The 2026-11-01 audit found the futures path itself is ES-only (CME equity-index session clock, quarterly H/M/U/Z roll, third-Friday expiry, the root parser, the clusters). This spec **supersedes rev 3 where they conflict (§1.2)**; rev 3's DECIDED answers stay in force otherwise. Nothing from rev 3 I1–I6 is coded (`rg 'EInstrumentClass|InstrumentProfile' -g '*.CS'` finds nothing); rev-3 PR-I0 `_InstrProbe` is on main and is retired by PR-M1e.

**Scope.** M-INSTR is a **base service** in `_CustomSignal`. At `StartCalc()` (and, for session data, on the first `CalcBar` of each stream, §4.5) it:
1. reads every used stream's instrument through the **typed** API (`Bars.Info` / `BarsOfData(n).Info`, `IInstrumentSettings`) and the stream's **chart sessions** (`Bars.Sessions`, `IROList<SessionObject>`);
2. builds one immutable `InstrumentProfile` per stream from detection, the chart's sessions, a per-root **exchange-facts** preset (no times) and the user's **cost** figures;
3. logs detection and the applied configuration;
4. hands each engine its effective settings;
5. throws a clear `EngineNotSupportedException` where an engine cannot apply (one rule, R13).

Tuned parameters (thresholds, bandwidths, gap sizes, degrees) are **not** in profiles (R5).

**Not touched.** The PriceAction library. No new `[Input]`s. M-PORT behaviour (§9). ES on the current hosts and charts stays **bit-identical**, checked by the `_TestGate0` canary hash, the DVP D9a summary (post-#88 baseline) and the ES fixtures in `tests/*` (§10 MI-T15).

### Revision log

| Rev | Date (ET) | Change |
|---|---|---|
| 1 | 2026-11-02 | First draft (62-row audit, rev-3 conflicts C1–C8, Q1–Q10 OPEN). |
| 2 | 2026-11-02 | Coder review (37 items, Appendix A) applied, each re-verified on `7dbdb1b`. **Sessions come from the chart (Q1/Q2 DECIDED by Ettienne):** no session/RTH windows in any preset; trading day, day open and session end derive from typed `Bars.Sessions`; RTH-window presets dropped. Q3–Q10 DECIDED (accepted recommendations). One throw-vs-degrade rule (R13). Lint scoped to MC enums with an allow-list. `_InstrProbe` retired in its own PR. Gate0Tests updated with the G0 tick change. ringBars kept literal. §9 aligned with the `willArm` code; cost floor is new opt-in behaviour. Audit 70 rows (8 added, 5 corrected, counts fixed). F1 widened, F3 second bug. `pointValue` → `bigPointValue`. M1 split four ways (+ probe retirement), M6 per engine. New questions N1–N6. |
| 2.1 | 2026-11-03 | Ettienne 2026-11-03 09:47 ET: *"1 to 5 is fine, on 6 keep that dump in the same directory as the created standard log file"*. N1–N5 DECIDED as recommended; N6 DECIDED: `_InstrDump` writes `MCInstrDump_<symbol>_<yyyyMMdd-HHmmss>-<pid>.txt` into the directory of the host's standard `MCSignal*.csv` log, through the host's existing resolver (§4.4); no new hardcoded path. N4 becomes approved degraded mode D7. Status APPROVED for implementation. |
| 2.2 | 2026-11-03 | Base: rev 2.1 as merged in `origin/main` @ `6853398` (PR #89). **§4.3 root rule rewritten** (rev 2.1 steps 3–4 turned ESZ2024 into ESZ2024): leading digits + letter run, drop a final month letter followed by a digit when the run has ≥ 2 letters; MI-T2 gains ESZ2024, 'ES 12-24', ESZ24:XCME, CLZ2026, EUR/USD, BRK.B, Z4. **PR-M0 dump results** (Ettienne's 13 `_InstrDump` runs, §4.4.2): CONFIRMED `Bars.Sessions` filled at StartCalc and identical on the first bar (R12 now reads at StartCalc, re-checks on the first bar), session/bar times follow the chart time zone, per-series sessions, multiple sessions per day kept, `SessionName` value, 6E priceScale within the 1e-9 tolerance, SPY facts, CL/GC tick and big point value. **New MC behaviour:** on the GLOBEX template `LastBarInSession` did not fire at the 15:15 break, only at 16:00 → new **R14** (session ends from the Sessions list end times) and **MI-T16**. Still OPEN (awaiting Ettienne): Portfolio Trader second symbol not dumped; 6E Default session vs session-end bars (QuoteManager zone). B1/B2 not run (commented out in the build). |
| 2.4 | 2026-11-04 | Re-based on merged main `5c9ccd2` (M0, M1a–e, M2 #96, M3 #97 and #94 merged). **M2 (Ettienne's decisions, #96):** 6E roll = `BusinessDaysBeforeExpiry(5)` (§8.1, replaces "Mon of the expiry week"); CL/GC/ZN/6E use `ECalendar.NyseStandIn`, logged on INSTR as `calendar: NYSE stand-in (CME calendar pending M8)` until PR-M8; a preset with expiry/roll rules and no calendar refuses `calendar` at Build, no weekday fallback anywhere (R6); `expiry-worked.csv` roll column = preset rules, not vendor dates (MI-T4). **M3 (#97):** reason code `months` (§6.3); RUN suffix ` profileHash(ds=h;…;ds=none)` outside configHash (§6.1); log-only line format; enforced set = `mismatch:*` + `sessions:changed`, everything else a log-only Warning with a null profile (§6.2, two-phase Build); INSTR example gains `root(<r> rootSrc=…)` and `dataNotes(…)`, ES and 6E lines pinned as the tests pin them; profile numbers printed/hashed with `InstrumentText.Fact` (G15), `Num` ("R") for diagnostics only; the profile carries the preset's tick/bpv after the cross-check; §4.3 6E tick text corrected (profile `5E-05`; raw .NET Framework "R" `5.0000000000000009E-05`); pinned profileHash ES `020deca78d6392b7`, 6E `8e8b3e9b0e230e12`; cost floor only with a matched preset, else `floor(Unknown)` (-1), and a new M4 requirement to log why; costs keyed by root only (**OPEN decision D1**, §12.4); Portfolio Trader per-symbol Sessions/ASymbolInfo2 test before M4; `eMTPA_SF_Root` **VERIFY** (not in the PG or any dump) until MI-T1/MI-T15; tz labels `unresolved` / `unavailable(<ExceptionType>)`; ClassOf stays in `_CustomSignal`, unmapped category is a log-only Warning in M3 and must be enforced in M4; `MonthMask.Of` throws without an Error line on static preset data only (accepted). §11 statuses with PR numbers; #94 listed as an extra; PR-M4 scope made explicit (FALLBACK_TICK sites, `tickFromMinMove` PriceScale==0 path, floor-Unknown reason, ClassOf enforcement, log-only → enforced, PT test, `eMTPA_SF_Root`). New §12.4 (open decisions), §12.5 (code-vs-spec divergences). Standing rules restated after R14. **D1 DECIDED (Ettienne 2026-11-04, same revision):** costs keyed by asset class + root, key `<Class>:<ROOT>` (defaults `Future:ES` 1700, `Future:MES` 250); a row whose class does not match the symbol is not used (`cost(Unknown)`, `floor(Unknown src=cost-class-mismatch)`, one Warning); root-only keying is M3 behaviour; firm PR-M4 scope (§5.3, §9, §11.1 item 8, §12.4); configHash unaffected (no configHash includes `InstrumentConfig.Canon()` on `5c9ccd2`); profileHash unchanged for ES/MES/6E, changes only where a cross-class cost was shown (e.g. a stock `ES`). **D2 DECIDED (Ettienne 2026-11-04):** profileHash hashes the sessions (and a `ChartDaySession` rth) converted to the **exchange** time zone (`exchangeTimeZoneId(Info.Exchange)`, strict lookup), so one instrument hashes the same on any chart/PT zone; new refusals `tz:bar` / `tz:exchange`, never a silent fallback; INSTR keeps bar-time `sessions(…)` and adds `xsessions(…)`; expected pins after M4: 6E Default `55576df6a8a99c05` on any zone, ES Default `6cff053d1b215324`; IT pins with exchange-time input unchanged, new tz-invariance tests (§6.1, §11.1 item 9, §12.4, V15). **MC verification (Ettienne 2026-11-04, `5c9ccd2`):** MI-T1 Compile All passed (after the one-time import of `_InstrumentProfile.Function.CS`); `eMTPA_SF_Root` and `eMTPA_MCSC_USD` compile, VERIFY dropped; `rootSrc=mc` on 6E, ES, AAPL; MI-T15 6E chart hash `55576df6a8a99c05` reproduced on .NET 8 (runtime-stable; `8e8b3e9b0e230e12` is the synthetic 9-session list); PT per-symbol test passed (@6E, @ES, AAPL; ES `e1927ccf1ea6f41e`, AAPL `41886799d411dd0d`), PT open item closed; PORT publish lines stamped `0001-01-01` / zone `?` (cosmetic, M4 item 10). |
| 2.3 | 2026-11-03 | Prepared after #90 (which commits rev 2.2 byte-identical); **final, ships with PR-M1b**. Header: the `SymbolRoot` L108–119 cite is main **before #90** (`6853398`); post-#90 location L120–135. §4.3/MI-T2: `MESH26` example replaced by `MESZ26`/`MESH27` → `MES`, as in the #90 tests. **@6E zone RESOLVED:** QuoteManager custom sessions with ET hours entered as CT (user config error, not MC behaviour); Ettienne switched to exchange sessions and will rerun; the 6E fixture is marked superseded. Folded in Coder's `mc-session-templates-research.md`: the built-in E-mini GLOBEX template is stale (2021 halt removal, pre-2012 15:30–16:30 row) and sits on the D9a/canary chart (environment caveat, engines take sessions as given); "Combined" = two sessions per day; templates editable in QuoteManager → Tools → Session Templates; `LastBarInSession` fires on rows with Session End ticked (inferred, not documented) as R14's rationale, with sources. Portfolio Trader @CL stays OPEN. **MC stores the root (credit: Ettienne):** `Bars.Info.ASymbolInfo2` (`MTPA_MCSymbolInfo2`, PG §4.2.3, pg11.txt L901, L3828–3867) becomes the M2 resolver's primary root, with the #90 parser as fallback when empty or flagged in `UnsupportedFields` (typed); `!SameRoot` → Warning, MC wins. §4.4 B1/B2 lead with `SymbolExpiry`/`SymbolCurrency`; new B3 cross-checks `SymbolCategory`/`MinMove`/`PriceScale`/`BigPointValue` vs `Bars.Info`/`IInstrument`. **Resolved by Ettienne `_RootDump` 2026-11-03, relayed by Coder (§4.4.3):** SymbolRoot/Currency/Category/Exchange/MinMove/PriceScale/BigPointValue CONFIRMED; parser = pure fallback on `(UnsupportedFields & eMTPA_SF_Root) != 0` or empty root; SymbolExpiry placeholder rule (B1); currency from SymbolCurrency (B2); MC-internal check PriceScale × Info.PriceScale = 1 ±1e-9 and BPV equal, else throw; MI-T2/MI-T3 fixtures; removed from §12.3. **@6E CLOSED:** rerun `01M6HTKV3SV1Q5QDXBFTCH8D3K.txt` (17:00–16:00 Exchange, ends 16:00, matches ES; `bvo(TickCount)` so instrument/session facts only) is the 6E fixture; `01M6HSDCCJZXJHNB165KSF6Z5B.txt` discarded; 6E rerun removed from §12.3. Setup note: restart MC after changing QuoteManager sessions (cached). |

---

## 0. Rules (normative; every PR must comply)

- **R1 Typed API only.** Never use MultiCharts enums or API members as strings: no `Enum.Parse`/`TryParse`/`GetNames` on an MC enum, no `<MC enum>.ToString()` used in a comparison, parse or hash, no reflection by member name on MC types. Formatting an MC enum into a human-readable log line is allowed (nothing reads it back). Repo-own enums parsed from protected config are outside R1 (§10 MI-T14 allow-list). Where a typed member is uncertain it is **VERIFY** with a typed dump line (§4.4); a line that does not compile is the answer (the member does not exist). Never guess.
- **R2 Build hygiene.** New `.CS` → `<Compile Include>` in `PLStudies.csproj` + one-time PowerLanguage Editor import (listed in the PR). New test projects → `PLStudies.Tests.sln`, `CheckForOverflowUnderflow=true`, real-source linking via a `gen.sh` awk region (`// <instr-core>` … `// </instr-core>`), as in `tests/InstrProbeTests`. C# 7.3.
- **R3 PriceAction unchanged.** The profile does not touch `EPriceActionPreset` or the PriceAction library; VPFUT keeps its existing preset call.
- **R4 No new `[Input]`s.** Settings go through protected config objects (`InstrumentConfig`, §5.3), like `EventConfig`, `RegimeConfig`, `DailyConfig`, `DvpConfig`, `PortMmConfig`.
- **R5 Self-calibration over tables.** Presets hold **exchange facts only**: tick, big point value, currency, contract months, expiry rule, roll rule, holiday calendar (where verified), cluster. **No session or RTH times** (Q1/Q2). ES-tuned values (e.g. `RollGapTicks` 8, `KdeBandwidthTicks` 6, `_ProfileStats` 4-tick disagreement) stay as today with a CFG note "ES-tuned default", or become relative to a running statistic in a later PR (§7).
- **R6 Auto-detect, apply, log, refuse.** No silent fallback. `TICK_FALLBACK = 0.01` (`_CustomSignal` L29, `resolveTick` L2287–2301 with a bare `catch {}`) becomes an Error + throw (PR-M1b; **done** in #91: CS `resolveTick` L2347–2368 @5c9ccd2, Error line via `traceAlways` then throw). **No weekday fallback anywhere (rev 2.4, #96):** every expiry/roll computation needs an assigned business-day calendar. `InstrumentCalendars.Require` (IP L450–455) and `InstrumentRules.requireCalendar` (IP L467–471) refuse with reason `calendar`; Build calls `Require` whenever the profile has an expiry or roll rule (IP L803), so a preset (or override row) with roll rules but no calendar (`Observed`/`None`) is refused at profile build. The old weekday default gave wrong dates (CLF26 12-22, ZNH24 03-20, ZNZ25 12-22; IT L209, L228). In M3 that `calendar` refusal is log-only (§6.2); it is enforced from PR-M4. Known remaining fallbacks, audited and removed in **PR-M4** (§11): `FALLBACK_TICK = 0.01` in `_VolumeProfileBase` (declared L48, used L2418 `zoneClearsPrice`) and `_VpConfluenceEngine` (L1156 `confluenceTolerance`), and CS `tickFromMinMove` (L2527–2532) returning `MinMove` when `PriceScale == 0` (and `Math.Abs` of a negative ratio), all @5c9ccd2.
- **R7 Volume basis.** Futures (`Future`/`FutureRolover`) on Build Volume On = Tick Count → Error + throw; non-futures on Tick Count are tagged `TickCount`. **Already on main** (`_VolumeProfileBase` L1429–1461, L1505–1506); M-INSTR only moves it into `volumeBasis`.
- **R8 Sizing.** 1% risk per trade; hard max-qty/max-notional caps reduce size (`sizeForRisk`, L2921–2953). The portfolio path (`_PortMM`, deny-not-reduce) is unchanged. Details and the actual code path: §9.
- **R9 Costs.** ES $17.00 and MES $2.50 per round trip (Ettienne). Other roots: method DECIDED (Q3), figures need Ettienne's IBKR per-side fees per root (data input).
- **R10 Logs.** One `File.AppendAllText` per line; skipped entries at **Info**; doubles formatted with `CultureInfo.InvariantCulture`.
- **R11 Facts.** Cite file:line; exchange facts carry a source or **VERIFY**.
- **R12 Sessions come from the chart (DECIDED, Q1/Q2).** Session times, the trading-day boundary and the session end come from the stream's chart sessions via typed `Bars.Sessions` / `SessionObject`. The chart's session template is the source of truth (normally the instrument's MC "Default"). Engines do not second-guess it and do not warn about which template the user chose. Sessions are per stream (each data series has its own, **CONFIRMED** §4.4.2; in Portfolio Trader each symbol has its own, with one global time zone) and are read **in StartCalc and re-checked on the first CalcBar** of each stream (both reads identical in all 13 dump runs, **CONFIRMED** §4.4.2; a difference throws `sessions:changed`, S1). `Bars.Sessions` exists (it compiles on main) but is documented only on the MC forum; its behaviour is now taken from the PR-M0 dump (§4.4.2), not from the documentation. The template name (`Info.SessionName`, Programming Guide) is logged and never parsed. Holidays cannot be read from code, so holiday calendars stay in the repo as calendars only (PR-M8). The DV/MTF session-mismatch Warning between streams (#86/#88 `checkHtfSessions`), including its MC-1439 / Realtime-History Matching caveat, stays.
- **R13 Throw vs degrade (one rule).** An engine **throws** when it cannot apply to the instrument or to the chart's sessions. It runs **Degraded** only in a mode Ettienne has approved, which still produces correct results from reduced inputs; each such mode is listed in §6.4 with what is reduced. An output that is *undefined* for the given sessions (e.g. an overnight high on a chart with no overnight segment) is NaN, logged once at Info, and is not a degrade; §6.4 lists every such output.
- **R14 Session ends come from the Sessions list (NEW, rev 2.2).** A session ends at the `EndTime` of a `SessionObject` in the stream's Sessions list, not where `Bars.LastBarInSession` happens to be true. Observed (§4.4.2, `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt`, `01M6HSDBFH2D6H21PMYXYKFJHV.txt`): on the CME GLOBEX template (sessions 17:00–15:15 and 15:30–16:30 CT) `LastBarInSession` was true only on the 16:00 bar, **not at the 15:15 break**. The session reader and every session-reset engine (VPFUT, AVWAP, FLOW, TOD, RANGE, WEEKLY, DAILY, DVP, G0, REGGATE, ROLL keying) therefore assign each bar to the session containing its close (start exclusive, end inclusive) and detect a session end as: (a) a bar whose close equals the session's `EndTime`, or (b) the first bar that falls in a different session than the previous bar (the end is then attributed to the previous bar), or (c) `LastBarInSession` true (kept only as an extra early signal for the last bar when data stops before the listed end, e.g. ES data ends 16:00 inside a session listed to 16:30). `LastBarInSession` alone is never the only signal. A reset fires **once per (stream, trading day, session index)**: the two-series run logged the same 2026-10-26 16:00 Data1 bar as a session end twice (`01M6HSDBFH2D6H21PMYXYKFJHV.txt`, CalcBar called once per stream update), so resets must be idempotent. Tested by MI-T16. **Rationale (rev 2.3):** each template row has a **Session End** checkbox and MC staff say to tick it on the row that ends the trading day (forum t=45178, https://www.multicharts.com/discussion/viewtopic.php?t=45178); ticking it on every row gave a break at every row (t=5890, https://www.multicharts.com/discussion/viewtopic.php?t=5890). `LastBarInSession` therefore most likely fires only at rows with Session End ticked, which matches the GLOBEX observation (15:15 row presumably unticked). This is **inferred, not documented** (research `/workspace/m-instr/mc-session-templates-research.md` §6), and the tick is not exposed by `SessionObject` (§4.1), so the list `EndTime`s are the only readable boundary; live, the flag can also be late or missed when no tick prints in the last bar (staff, https://www.multicharts.com/discussion/viewtopic.php?t=50358; https://www.multicharts.com/discussion/viewtopic.php?t=48918).


**Standing rules (restated, rev 2.4; every PR still checks them):**
- **No MC enums or API values as strings** (R1): typed members only; MC enum labels are formatted by the host for log lines and never read back (IP header L8–11; lint MI-T14).
- **Sessions come from MC session data** (R12): the chart's template (normally Default) via typed `Bars.Sessions`. The only code-side windows are the accepted ES/MES/NQ/MNQ RTH split on a 24-hour chart (N1, `rthSource=EquityIndexLegacy`, IP `RthSplit` L643–665) and the fixed Asia/Europe/NY clock windows (N5).
- **Every new `.CS`** needs a `PLStudies.csproj` `<Compile Include>` and a one-time PowerLanguage Editor import, listed in the PR (R2; `_InstrumentProfile.Function.CS` is csproj L522 @5c9ccd2).
- **Error is logged before every throw** that leaves M-INSTR code (`InstrumentError.Fail`, IP L91–98; host `traceAlways`). In M3 a log-only refusal is caught by `InstrumentStreams.Guard` and written once as a Warning instead (§6.2); `MonthMask.Of` is the one accepted exception (§12.4 note).
- **No silent fallbacks** (R6), including no weekday business-day fallback.

---

## 1. Relation to `instrument-profile-spec.md` rev 3

### 1.1 Kept from rev 3
The engine × class support matrix (rev 3 §8.1) and its DECIDED answers: rev-3 Q1 ETF = Stock; rev-3 Q2 FLOW throws on stocks; rev-3 Q3 VPFUT throws on non-futures; rev-3 Q4 no force flag; rev-3 Q5 DVP/G0 Unsupported on FX, VP Degraded (visual) on FX; rev-3 Q7 `profileHash` outside configHash; rev-3 Q8 `_RegimeEngine` on ES/MES only; rev-3 Q9 ES $17; rev-3 Q10 USD only; rev-3 Q11 EVENT Degraded on USD FX pairs. Also kept: the INSTR/CFG log idea, the `EngineNotSupportedException` format (rev 3 §6), the cost-floor formula (IP-8.3).

### 1.2 Conflicts (this spec wins)

| # | Rev 3 | Conflict | Rev 2 |
|---|---|---|---|
| C1 | IP-1.1/IP-2.2 classify by `Category.ToString()`; probe uses `Enum.GetNames` | R1 | Exhaustive `switch` on `ESymbolCategory` in the host (§4.2). |
| C2 | IP-1.4 "Build Volume On is not on `IInstrumentSettings`" | Wrong: `Info.BVO` is typed (`_VolumeProfileBase` L1505) | `volumeBasis` from `Info.BVO`. |
| C3 | IP-4.4 "Future = today identity" | Futures path is ES-only | Per-root exchange facts (§8) + chart sessions; ES bit-identical. |
| C4 | IP-8.1 "q is never reduced" | Hard caps reduce today (L2943–2949) | §9 describes the actual code. |
| C5 | IP-8.3 MES $2.47 | R9 | MES $2.50, floor 20 ticks (integer-cents formula, §9). |
| C6 | PR-I0 `_InstrProbe` uses reflection (`_InstrProbe` L4/67/74/75/144; `_InstrProbeCore` L5/54/126/167) | R1 | Typed `_InstrDump` (PR-M0); `_InstrProbe` + `tests/InstrProbeTests` retired in PR-M1e (Q6 DECIDED). |
| C7 | Rev-3 line numbers | Stale | Re-read on `7dbdb1b`. |
| C8 | PR-I4 "no futures holiday re-route" | Other CME product groups have other holidays/early closes | Calendar rule §6.4 S4 and PR-M8; ES keeps `_CmeHolidays`. |
| C9 | Rev-3 session handling = CME equity-index clock for every future | R12 | Sessions from `Bars.Sessions` (§4.5); no session times in presets. |


---

## 2. Purpose

Today an engine run on anything other than @ES/@MES silently uses ES facts, guesses from strings, or fails later with an unclear error. M-INSTR: (1) detects the instrument from typed MC data; (2) takes session times, trading day and session end from the chart's sessions; (3) resolves the exchange facts each engine needs (tick, big point value, currency, contract months, expiry and roll rule, calendar, cluster) from detection plus a small exchange-facts preset per root; (4) applies them, ES bit-identical; (5) logs; (6) refuses clearly under R13.

---

## 3. Audit: ES- and futures-specific hardcoding on `origin/main` @ `7dbdb1b`

Method: `rg` over every `*.CS`, `specs/*.md`, `tests/*` for 0.25, 12.5, $50, `"ES"`/`@ES`, H/M/U/Z, `ThirdFriday`, `TimeSpan(9,30)`, (16,0), (16,15), (17,0), (18,0), (15,15), Globex, Eastern/Central, USD, `Category.ToString`, `SymbolRoot`, `PortfolioCluster`, cost literals (471 raw hits, `/workspace/m-instr/grep1.txt`), reduced to one row per distinct hardcoding, plus the 8 rows the rev-1 review found missing (63–70). Column "Sess" = **S** where the row is replaced by chart-session data under R12 (§4.5), **T** where it is a frozen pre-registered trial value (kept, coverage-checked, §6.4 S3).

Engine tags: CS host `_CustomSignal`; VPFUT `_VolumeProfileFuturesBase`; CLOCK `_CmeSessionClock`; CAL holiday calendars; ROLL `_RollDetector` + host roll code; EVENT `_EventCalendar`; G0 `_Gate0`/`_TestGate0`; FLOW `_OrderFlow`; REGGATE `_RegimeGate`/`_Test6`; REGIME `_RegimeEngine`; DAILY `_DailyContext`; TOD `_TodNormaliser` (host side); DVP `_DvpEngine`/`_DvpEngineTest`; SIZE CS-4 sizing; COST; PORT M-PORT; MTF `_MtfMath`; LEVEL, AVWAP, RANGE, WEEKLY, PSTATS `_ProfileStats`; LEGACY pre-engine studies.

### 3.1 Table

| # | file:line | What is hardcoded | Engine | Profile field | Sess |
|---|---|---|---|---|---|
| 1 | `_CustomSignal` L1603–1618 | `PortfolioCluster` switch on root strings: EQIDX (L1611), RATES (L1612), ENERGY (L1613), METALS (L1614); others fall through to the raw root | PORT | `cluster` | |
| 2 | `_CustomSignal` L2462 | `autoTradingDay(barTime, Bars.Info.Category.ToString())`: MC enum → string (R1) | CS | `tradingDay` (typed) | S |
| 3 | `_CustomSignal` L2581–2587 | `autoTradingDay` substrings "FOREX"/"CURRENCY"/"CRYPTO": FX day = ET+7h; crypto = UTC date | CS | `tradingDay` | S |
| 4 | `_CustomSignal` L2614–2626 | `exchangeTimeZoneId` substring map: L2619 CME/CBOT/CBT/GLOBEX/CBOE→Central; **L2621 NYMEX/COMEX/NYSE/NASDAQ/ARCA→Eastern** (CME Group quotes CL/GC hours in CT; dump: @CL shows 18:00–17:00 on both Exchange and Local, @GC 18:00–17:00 on Exchange, i.e. MC's exchange zone for NYMEX/COMEX on Ettienne's setup equals ET, §4.4.2 A3; whether that is the QuoteManager exchange zone or a template entered in ET is not separable from these runs, so the map entry stays as is and is not "fixed" to Central); other exchanges throw (L2603) | CS | `barZone` (only for ET conversions, §4.5) | S (partly) |
| 5 | `_CustomSignal` L29, L2287–2301 | `TICK_FALLBACK = 0.01` with a bare `catch {}` | CS | `tick` (throw) | |
| 6 | `_CustomSignal` L2062 | `_NyseHolidays` created for every instrument | CAL | `calendar` | |
| 7 | `_CustomSignal` L3228–3240 | `tradingDayCount` uses NYSE for non-stocks (warns once) | CAL | `calendar` | |
| 8 | `_CustomSignal` L921 | `isRthBar` returns true on the generic base | CS | `rthSplit` (§4.5, N1) | S |
| 9 | `_CustomSignal` L949 | `todMinuteOfSession` default; futures override = 18:00 ET (row 18) | TOD | `dayOpen` | S |
| 10 | `_CustomSignal` L1063–1072 | DAILY roll-offset warning assumes the 18:00 ET splice; `Category == Future` only (misses `FutureRolover`, F2) | DAILY | `tradingDay`, `assetClass` | S |
| 11 | `_CustomSignal` L1801, L1836 | `RollGapTicks = 8` (ES-tuned; `_RollDetector` L12–13) | ROLL | none (Q4) | |
| 12 | `_CustomSignal` L1866, L1881–1882 | Roll warning / `m_rollGlobexKey` require `Category == Future`; `isFuturesCategory` (`_VolumeProfileBase` L1440–1442) also accepts `FutureRolover` | ROLL | `assetClass` | |
| 13 | `_CustomSignal` L1840–1842 | Roll inheritance keyed by `_MtfMath.SymbolRoot` (row 31). (Rev 1 cited L1778–1783, which is the HtfStreams same-root throw L1776–1785, also a `SymbolRoot` user, fixed by PR-M1a.) | ROLL | `root` | |
| 14 | `_CustomSignal` L749, L766 | DVP root = `SymbolRoot(...).ToUpperInvariant()` | DVP | `root` | |
| 15 | `_CustomSignal` L2891–2953 | `unitNotional` 0 = entry × BigPointValue, implicit USD | SIZE | `bigPointValue`, `currency` | |
| 16 | `_CustomSignal` L2994–3002 | `roundTripCostPerContract` = 2 × (MC Commission + Slippage) | COST | `cost.roundTrip` | |
| 17 | `_CustomSignal` L2008 | AVWAP `sessionDate = tradingDayKey`, `rth = isRthBar` | AVWAP | `tradingDay`, `rthSplit` | S |
| 18 | `_VolumeProfileFuturesBase` L489–493 | TOD minute = minutes since 18:00 ET | TOD | `dayOpen` | S |
| 19 | `_VolumeProfileFuturesBase` L52–63 | `DEFAULT_*` strings in ET: Asia, Europe, NY, RTH 09:30–16:15, halt 17:00–18:00, Globex 18:00/17:00 | VPFUT | session data / N1 / N3 | S |
| 20 | `_VolumeProfileFuturesBase` L66–77 | Same defaults as `TimeSpan`s (VPF-13) | VPFUT | as row 19 | S |
| 21 | `_VolumeProfileFuturesBase` L361 | `tradingDayKey` = `GlobexDate` (18:00 ET) | VPFUT | `tradingDay` | S |
| 22 | `_VolumeProfileFuturesBase` L396–398 | `DefaultRthValueEnd` 16:00 ET (value window, D5) | VPFUT | `rthSplit` (N1) | S |
| 23 | `_VolumeProfileFuturesBase` L472–476 | `EOwnProfileWindow { MultiChartsProfile = 0, GlobexDay, Rth }` | VPFUT | names stay; GlobexDay = chart trading day | S |
| 24 | `_VolumeProfileFuturesBase` L517; `_CmeSessionClock` L373–379 | `isRthBar` = RTH **value** window (09:30, 16:00] ET via `IsRthValueBarEt`; `m_rthEnd` 16:15 is the session flag, not the value window | VPFUT | `rthSplit` (N1) | S |
| 25 | `_VolumeProfileFuturesBase` L975–995 | Tick-chunk plan per Globex day | FLOW | `tradingDay` | S |
| 26 | `_VolumeProfileFuturesBase` L1627, L1635–1671 | VPFUT's own `UseCmeHolidayTable` (L1627) and hooks: `IsGlobexClosedDay` L1635, `IsNoRthDay` via NYSE L1641–1646, `HaltEt` L1656, `RthCloseEt` L1665, `_CmeHolidays.Rules.Get(date).Closed` L1671 | CAL | `calendar` | |
| 27 | `_CmeSessionClock` L48–61 | "CME equity-index hours" defaults in ET: RTH, RthValueEnd, halt, Globex open/close, Asia/EU/NY | CLOCK | session data / N1 / N3 | S |
| 28 | `_CmeSessionClock` L81 | Clock's `UseCmeHolidayTable = true` (separate from VPFUT's L1627, row 26) | CAL | `calendar` | |
| 29 | `_CmeSessionClock` L248–249 | `GlobexTradingDayEt(et)` 18:00 ET open | CLOCK | `tradingDay` | S |
| 30 | `_CmeHolidays` L13–45, L65 | CME equity-index holiday/early-close rules (13:15 ET L44, Good Friday 09:15 ET L45, halts L65) | CAL | `calendar` (stays; not session times) | |
| 31 | `_MtfMath` L29–39 | `SymbolRoot`: letters only; drops a trailing month letter. "@6E", "6EZ26" → ""; "EUR/USD" → "EUR" (F1) | MTF | `root` | |
| 32 | `_RollDetector` L23 | `gapTicks = 8` | ROLL | none (Q4) | |
| 33 | `_RollDetector` L43 | `RollRuleMondayFrom = 2022-04-01` | ROLL | `roll` | |
| 34 | `_RollDetector` L46–51, L56–67 | `ThirdFriday`, quarterly `SeriesWindow` | ROLL | `contractMonths`, `expiry`, `roll` | |
| 35 | `_EventCalendar` L20 | Event rows in ET (correct for US macro) | EVENT | `barZone` for conversion | |
| 36 | `_EventCalendar` L128 | Default event classes (no EIA for ES) | EVENT | `eventClasses` | |
| 37 | `_EventCalendar` L189 | `EventRules.RollRuleMondayFrom` 2022-04-01 | EVENT | `roll` | |
| 38 | `_EventCalendar` L290–303 | `ExpiryDate`/`RuleExpiryDate` 3rd Fri; `RollDate` Mon before (2022+) else Thu −8 | EVENT | `expiry`, `roll` | |
| 39 | `_EventCalendar` L1913–1928 | `nextQuarterlyExpiry`, `SessionsToExpiry`, `RuleRollDate`: quarterly | EVENT | `contractMonths` | |
| 40 | `_Gate0` L328 | `KdeBandwidthTicks = 6` | G0 | none (ES canary) | |
| 41 | `_Gate0` L357 | `tick = 0.25` default (host sets it: `_TestGate0` L237; `_Gate0` L461 `study.tickSize = tick`) | G0 | `tick` | |
| 42 | `_Gate0` L474, L614, L1037/1059/1116/1129/1267, L1070 | RTH 09:30–16:00 ET, 30-min periods, flat 15:55, 09:35 | G0 | frozen trial windows | T |
| 43 | `_Gate0` L1185–1186 | Doc: "q × widened stop × $50"; `costFloorTicks` only in comments | G0 | doc | |
| 44 | `_Gate0` L1204 | `new _DvpEngine(c, "G0KD02", "ES", tick, …)` | G0/DVP | `root` | |
| 45 | `_TestGate0` L42, L156–158 | "CME Globex" template; $2.25/side + 0.5 tick/side = $17 | G0 | `cost` | |
| 46 | `_OrderFlow` L124 | `TickChunkBoundaryEt = 17:00` | FLOW | `sessionEnd` | S |
| 47 | `_OrderFlow` L935 | `PlanGlobexDays(...)` | FLOW | `tradingDay` | S |
| 48 | `_RegimeGate` L45–48 | A2-63: rthOpen 09:30, activeEnd 15:30, exit 15:55, rthClose 16:00 ET | REGGATE | frozen trial windows | T |
| 49 | `_RegimeGate` L55–62 | Roll years, digest `fdd63bd8f179a8d4` (L60), `tradingDay = "Globex18ET"` (string, L62) | REGGATE | `roll`, typed key | |
| 50 | `_Test6` L63, L89, L105–106 | ET zone string, Globex key, `RollDetection.ToString()` (repo enum) into the hash | REGGATE | typed key | S |
| 51 | `_DailyContext` L30, L33 | `{252,126,63}`, `hvAnnualisation 252` | DAILY | `sessionsPerYear` | |
| 52 | `_RegimeEngine` L272 | `sqrt(63/252)` | REGIME | `sessionsPerYear` | |
| 53 | `_LevelStudy` L79 | `tickSize = 0.25` (host sets L1410) | LEVEL | `tick` | |
| 54 | `_AvwapManager` L153 | `tickSize = 0.25` (host sets L1976; L195 rejects ≤ 0) | AVWAP | `tick` | |
| 55 | `_RangeProfile` L31 | `ringBars = 2880` (a **bar** count) | RANGE | none: literal kept | |
| 56 | `_WeeklyProfiles` L19, L26 | Default source `RthSessions` | WEEKLY | `rthSplit` (N1) | S |
| 57 | `_PortMM` L388, L740; `_PortfolioRiskManager` L87, L129 | USD-only book (typed `eMTPA_MCSC_USD`, correct) | PORT | `currency` | |
| 58 | `_DvpEngineTest` L213, L230 | `root == "ES"`: roll table ES-only | DVP | `roll` | |
| 59 | `_DeltaDivRevFuturesLELX` L152, L175, L528–564 | Globex state, Globex-day trade limit | LEGACY | note only | |
| 60 | `_ExitHalfRiskHalfRoll` L107–113 | "One tick" = `1 / PriceScale * BigPointValue` (0.5 on ES if PriceScale = 100: neither a tick nor $/tick) (F3a) | LEGACY | `tick` | |
| 61 | `_EconomicCalendar` L231; `_EhlersFilter` L149; `_StructuralTargetPatterns.Strategy` L1060; `_GenerateNNTrainingData` L354 | FX pip = `PointValue × 10 × BigPointValue` | LEGACY | note only | |
| 62 | `_EpisodicPivotBase` L32–35; `_ParabolicShortSESX` L51–52; `_BreakoutAutoLELX` L42; `_TrendLine` L2755–2756; `_VolumeProfileBaseLog` L206, L426 | US-stock 09:30/16:00 ET; `m_tick = 0.25` | LEGACY | note only | |
| 63 | `_CustomSignal` L1888–1889 | `findTz("Eastern Standard Time")` + `GlobexTradingDayEt` for the roll key on any future | CS | `tradingDay` (removed) | S |
| 64 | `_DvpEngineTest` L119–121; `_Test6` L100–102 | Host-generated quarterly `RollDates` (`for m = 3..12 step 3: RollDates.Add(EventRules.RollDate)`) | DVP | `contractMonths`, `roll` | |
| 65 | `_ProfileStats` L47 | `ringBars = 1440` (bar count) | PSTATS | none: literal kept | |
| 66 | `_ProfileStats` L575 | `disagree` when |mcPoc − cvPoc| > 4 × tick (ES-tuned) | PSTATS | none: CFG note | |
| 67 | `_TestGate0` L157 | NQ slippage $2.50 in the canary note | G0 | `cost` (doc) | |
| 68 | `_MtfMath` L28 | `SymbolRoot` doc says returns "@ES"; code returns "ES" | MTF | doc fix | |
| 69 | `_DvpEngine` L2445–2447 | `RootError`: empty == empty is a match, so any two digit-led roots pass (F1) | DVP | `root` | |
| 70 | `_ExitHalfRiskHalfRoll` L123 | `GenerateStopLoss(m_firstStopPrice)`: a price distance passed where MC takes currency (F3b) | LEGACY | `bigPointValue` | |

**Counts.** 70 rows; each counted in exactly one area. Rows 61 and 62 group several legacy files each.

| Area | Rows | Count |
|---|---|---|
| CS host | 2, 3, 4, 5, 8, 9, 10, 63 | 8 |
| VPFUT | 18–24 | 7 |
| CLOCK and CAL | 6, 7, 26–30 | 7 |
| ROLL | 11, 12, 13, 32–34 | 6 |
| EVENT | 35–39 | 5 |
| G0 | 40–45, 67 | 7 |
| FLOW | 25, 46, 47 | 3 |
| REGGATE | 48–50 | 3 |
| DAILY and REGIME | 51, 52 | 2 |
| SIZE and COST | 15, 16 | 2 |
| PORT | 1, 57 | 2 |
| DVP | 14, 58, 64, 69 | 4 |
| MTF | 31, 68 | 2 |
| AVWAP | 17, 54 | 2 |
| LEVEL, RANGE, WEEKLY, PSTATS | 53, 55, 56, 65, 66 | 5 |
| LEGACY | 59–62, 70 | 5 |
| **Total** | | **70** |

**Tests.** ES literals by project: VpHelper 102, OrderFlow 60, Gate0 45, Roll 39, PortMM 23, Dvp 16, Gmm 15, PriceAction 10, fewer elsewhere. These are ES fixtures by design and stay as the bit-identity guard; M-INSTR adds per-root fixtures. Exception: Gate0Tests is edited in PR-M6a (tick, §7).

### 3.2 Bugs today (fixed independently, PR-M1a–c)
- **F1 (wider than rev 1).** `SymbolRoot` drops digit-led roots ("@6E", "6EZ26" → "") and truncates FX pairs ("EUR/USD" → "EUR", colliding across EUR pairs). `_DvpEngine.RootError` L2445–2447 then treats "" == "" as a match, so any two digit-led roots pass the same-root check, and roll inheritance (L1840–1842) and the HtfStreams check (L1776–1785) are wrong for them. Fix in PR-M1a (§4.3); `RootError` treats an empty root as an error.
- **F2.** `Category == ESymbolCategory.Future` at L1069, L1866, L1882 misses `FutureRolover`; use the host's `isFuturesCategory` (`_VolumeProfileBase` L1440–1442). PR-M1b.
- **F3 (two bugs).** (a) `_ExitHalfRiskHalfRoll` L107–113 computes `1/PriceScale*BigPointValue`, which is neither a tick nor $/tick; (b) L123 passes `m_firstStopPrice`, a price distance, to `GenerateStopLoss`, which takes a currency amount. Fix: tick = `MinMove/PriceScale`; amount = distance × `BigPointValue` (per contract, matching the strategy's existing per-contract/position mode, VERIFY in the PR). PR-M1c.
- **F4.** `TICK_FALLBACK = 0.01` (L29) with a bare `catch {}` in `resolveTick` (L2287–2301) → Error + throw. PR-M1b.

### 3.3 MC-enum string uses on main (findings for PR-M1b / allow-list for MI-T14)
| file:line | Use | R1 status | Action |
|---|---|---|---|
| `_CustomSignal` L2462 | `Category.ToString()` passed to `autoTradingDay` substring tests | violation | PR-M1b: typed `switch` (`ClassOf`) |
| `_InstrProbe` L67, L74, L75 | `Category.ToString()` / reflection | violation | PR-M1e: retire the probe |
| `_CustomSignal` L279 | `TimeZone.ToString()` into a log line | allowed (log) | none |
| `_CustomSignal` L1883 | `res.ToString()` (`EResolution`) into a Warning text | allowed (log) | none |
| `_TestPortMM` L90 | `StrategyCurrencyCode.ToString()` log | allowed (log) | none |
| `_Test6` L105 | `RollDetection.ToString()` (repo enum `ERollDetection`, `_CustomSignal` L1791) into the A2-63 hash | outside R1 (repo enum) but hash-fragile | PR-M6h: explicit switch, same spellings |

---

## 4. Detection (typed MC API)

### 4.1 Members used

Status: **confirmed-PG** = in the MultiCharts .NET Programming Guide (v1.1 §4.2.3 "Properties of the Data Series", pp. 21–22; `mc-sessions-research.md` §6); **compiles** = used in code on main (so the member exists); **VERIFY** = neither, or a behaviour (not the member) is unconfirmed.

| Member | Type | Status | Source |
|---|---|---|---|
| `Info.Category` | `ESymbolCategory` | compiles; values Future 0, FutureOption 1, Stock 2, StockOption 3, Index 4, CurrOption 5, MutualFund 6, MoneyMarketFund 7, IndexOption 8, Cash 9, Bond 10, Spread 11, Forex 12, CPCSymbol 13, Composites 14, FutureRoot 15, FutureOptionRoot 16, StockOptionRoot 17, FutureRolover 18, Commodity 19, Crypto 20, Warrant 21, None 254 from Ettienne's dump3 run (box copy, not in repo) | `_VolumeProfileBase` L1442, `_CustomSignal` L1069 |
| `Info.MinMove`, `Info.PriceScale` | double | compiles, PG | `_CustomSignal` L2291–2292 |
| `Info.BigPointValue` | double | compiles, PG ("in the instrument currency") | `_CustomSignal` L1660, L1678, L2360 |
| `Info.PointValue` | double | compiles, PG (a **different** member from BigPointValue) | `_EconomicCalendar` L231 |
| `Info.Description` | string | compiles, PG | `_EconomicCalendar` L243, L679 |
| `Info.Exchange` | string (data value; parsing allowed) | compiles, PG | `_CustomSignal` L2601–2603 |
| `Info.Name` | string (data value) | compiles, PG | `_CustomSignal` L1886 |
| `Info.Resolution.Type` / `.Size` | `EResolution` / int | compiles, PG | `_CustomSignal` L1069, L966, L2245 |
| `Info.TimeZone` | `RequestTimeZone` {Local, Exchange, GMT} | compiles, PG | `_CustomSignal` L279, L2594–2606 |
| `Info.BVO` | `BuildVolumeOn` | compiles | `_VolumeProfileBase` L1505 |
| `Info.SessionName` | string | **confirmed-PG**; log only, never parsed. Returns the template name as set on the chart: `Default` for Default, `CME: Equity Index Futures [E-mini] (GLOBEX)` for the GLOBEX template (**CONFIRMED** `01M6HSDCPGEWFS9VAYZG2C808V.txt`, `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt`) | PG §4.2.3, dump |
| `Info.Request` | `DataRequest` | confirmed-PG; not used by M-INSTR | PG §4.2.3 |
| `Bars.LastBarInSession` | bool | confirmed-PG (§4.2.2). **Not a reliable session-end signal on its own**: not true at the GLOBEX 15:15 break (`01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt`); see R14 | PG, dump |
| `Bars.Sessions` | `IROList<SessionObject>` (`.Count`, `[i]`) | **compiles** (`_CheckData` L54 in StartCalc, L198–200; `_QullamaggieBase` L189–192; #88 head `mtfSessions` via `IInstrument`), so the members exist; **not in the PG** (forum/staff only). Populated at StartCalc and identical on the first CalcBar: **CONFIRMED** in all 13 runs (`sameAsStartCalc(yes)`, §4.4.2) | main, #88 head, dump |
| `SessionObject.StartDay/EndDay` (`DayOfWeek`), `.StartTime/.EndTime` (`TimeSpan`) | | compiles (same sites); zone of the times = the chart (Format Instrument) time zone of the stream: **CONFIRMED** (@ES 17:00–16:00 on Exchange `01M6HSDCPGEWFS9VAYZG2C808V.txt`, 18:00–17:00 on Local `01M6HSDBFJ9WRF71G51B82VKCH.txt`; bar times shift with it) | main, dump |
| `SessionObject.EndOfSession(DateTime)`, `Sessions.SessionForDate(DateTime)`, an end-of-session bool | | forum only: **VERIFY**; M-INSTR does **not** use them | research §6 |
| `Info.ASymbolInfo2` | `MTPA_MCSymbolInfo2` (COM struct) | **confirmed-PG**: listed under §4.2.3 "Instrument properties" (`MTPA_MCSymbolInfo2 ASymbolInfo2 { get; }`, pdftotext `/workspace/research/pg11.txt` L901); struct fields in the `MTPA_MCSymbolInfo2` section (L3828–3867, PG p. 82): `SymbolName`, **`SymbolRoot`** ("symbol root (for futures), for example ES, CL", L3833), `SymbolDescription`, `SymbolExchange`, `SymbolCategory` (`MTPA_MCSymbolCategories`), **`SymbolCurrency`** (`MTPA_MCSymbolCurrency`), **`SymbolExpiry`** (`DateTime`, "expiration date (for futures)"), `MinMove` (int), `PriceScale` (double; PG example "for ES = 1/100", L3853, whereas `Info.PriceScale` dumped `100`), `BigPointValue`, `PutOrCall`, `StrikePrice`, **`UnsupportedFields`** (`MTPA_SymbolFields`, "fields which are not provided by the source"); "received through … `Bars.Info.ASymbolInfo2` for the current symbol" (L3867). **CONFIRMED** (Ettienne `_RootDump` 2026-11-03, relayed by Coder, dxFeed; @ES, @CL, @6E, SPY, table in §4.4.3): `SymbolRoot`, `SymbolCurrency`, `SymbolCategory`, `SymbolExchange`, `MinMove`, `PriceScale` (= 1/`Info.PriceScale`: 0.01 vs 100; 6E 1E-05 vs 99999.999999999985) and `BigPointValue` (= `Info.BigPointValue`); `SymbolName` = `Info.Name` in all four; `UnsupportedFields` = 0 in all four. `SymbolExpiry` is a **placeholder** on continuous futures (2038-01-01) and stocks (1900-01-01) (§4.4 B1). `MTPA_SymbolFields` is **not** `[Flags]` but its values are powers of two: `eMTPA_SF_Root`=1, `Exchange`=4, `Description`=8, `Category`=16, `Expiry`=32, `PutOrCall`=64, `StrikePrice`=128, `Multiplier`=256, `Currency`=512, `ALL`=-1. Credit: Ettienne, rev 2.3. **Rev 2.4: `MTPA_SymbolFields.eMTPA_SF_Root` and `MTPA_MCSymbolCurrency.eMTPA_MCSC_USD` CONFIRMED (compile)**: Ettienne's MI-T1 PLEditor Compile All of `5c9ccd2` passed on 2026-11-04 (the first compile failed only because `_InstrumentProfile.Function.CS` had not yet been imported; one-time import), so both members exist as the code uses them typed (IP `InstrumentMcFields.RootUnsupported` L973, `CurrencyCode` L977). The code never relies on the numeric value (the value 1 is still only the test mirror, `tests/InstrumentTests/gen.sh` L23). The MC runs printed `rootSrc=mc` on 6E, ES and AAPL (root not flagged unsupported) | PG §4.2.3, PG p. 82 |
| `Info.Expiration` | `DateTime` | confirmed-PG as a property; never compiled in the repo (the probe reads it by reflection): **VERIFY**, and **not needed**: expiry comes from `SymbolExpiry` on dated contracts, else the roll calendar/detector (§4.4 B1); not used by M-INSTR | PG §4.2.3 |
| `StrategyCurrencyCode` | `MTPA_MCSymbolCurrency` | compiles on an `IStrategy` from a portfolio signal only (`_PortfolioRiskManager` L125–129, `_TestPortMM` L90); on a plain `SignalObject`: **VERIFY**, and **not needed**: currency comes from `ASymbolInfo2.SymbolCurrency` (CONFIRMED, §4.4 B2); not used by M-INSTR | main |
| Per-symbol currency on `Info` | — | no documented member; none guessed. Currency comes from the preset (USD, Q8) | — |
| Exchange time-zone member, `SessionStartBar`/`SessionEndBar`, "is Default" flag | — | not documented; not used | research §6 |
| Holidays from code | — | **not available** (MC staff, 2013/2015/2022) | research §6 |

### 4.2 Classification (host, exhaustive switch; replaces rev-1 MI-T1)
```csharp
static EAssetClass ClassOf(ESymbolCategory c) {
  switch (c) {
    case ESymbolCategory.Future: case ESymbolCategory.FutureRolover: return EAssetClass.Future;
    case ESymbolCategory.Stock: return EAssetClass.Stock;      // ETF = Stock (rev-3 Q1)
    case ESymbolCategory.Forex: return EAssetClass.SpotFx;
    case ESymbolCategory.Crypto: return EAssetClass.Crypto;
    case ESymbolCategory.Index: return EAssetClass.Index;
    case ESymbolCategory.FutureOption: case ESymbolCategory.StockOption: case ESymbolCategory.CurrOption:
    case ESymbolCategory.IndexOption: case ESymbolCategory.MutualFund: case ESymbolCategory.MoneyMarketFund:
    case ESymbolCategory.Cash: case ESymbolCategory.Bond: case ESymbolCategory.Spread: case ESymbolCategory.CPCSymbol:
    case ESymbolCategory.Composites: case ESymbolCategory.FutureRoot: case ESymbolCategory.FutureOptionRoot:
    case ESymbolCategory.StockOptionRoot: case ESymbolCategory.Commodity: case ESymbolCategory.Warrant:
    case ESymbolCategory.None: return EAssetClass.Unsupported;
    default: throw new InvalidOperationException("M-INSTR: unmapped ESymbolCategory " + ((int)c).ToString(CultureInfo.InvariantCulture)
      + " (not in spec §4.1); update ClassOf (§4.2) for this MC build.");
  }
}
```
All 23 members are listed by name; the `default` throws on a value added in a later MC build (formatting the int is a log, not a parse). It compiles only in PowerLanguage Editor (no test project references PowerLanguage), so it is checked by the PLEditor compile in PR-M2/M3, not by a test. The MC-free core uses only `EAssetClass`.

**As merged (rev 2.4, @5c9ccd2).** `ClassOf` stays in `_CustomSignal` (CS L2492–2510, region `<instr-host-core>`); `EAssetClass` lives in IP (`<instr-asset-class>`). Two callers:

- `buildProfiles()` (CS L2409) calls `InstrumentMcFields.Read(info, ClassOf(info.Category))` inside a catch-all (CS L2411–2416). An unmapped `ESymbolCategory` (the `default` throw) therefore becomes one **log-only Warning** `INSTR ds(n) log-only, not enforced until PR-M4: reading Data<n> <name> failed (InvalidOperationException: …); no profile` and the stream runs without a profile. **PR-M4 must enforce it** (Error line + throw `class`), like the other refusals (comment CS L2413–2414).
- `classOfData1()` (CS L2708–2723, `TradingDayRoll = Auto`) already writes the Error line (`traceAlways`) and rethrows.

### 4.3 Root resolution (PR-M1a, fixes F1; rule rewritten in rev 2.2)
1. **Clean:** trim; remove a leading `@`; remove a `#…` continuous suffix; uppercase invariant; remove every `/` ("EUR/USD" → "EURUSD"). In PR-M1a (`_MtfMath.SymbolRoot`, MC-free, no class) the `/` removal applies to every name; **PR-M2 gates it to Spot FX** (`SpotFx`) once the resolver knows the class.
2. **Root:** take the leading digits plus the run of letters that follows them (stop at the first character that is neither). If the letter run has **≥ 2 letters**, its last letter is a month code (`FGHJKMNQUVXZ`) and the character right after the run is a digit, drop that last letter. Nothing else is removed or kept: exchange suffixes (`:XCME`), spaces, year digits and `.`-classes fall away because the run stops there.
   Examples (MI-T2): `ESZ2024` → `ES`; `'ES 12-24'` → `ES`; `ESZ24:XCME` → `ES`; `ESZ4` → `ES`; `CLZ2026` → `CL`; `MESZ26` → `MES`; `MESH27` → `MES`; `@6E` → `6E`; `6EZ26` → `6E`; `6B` → `6B`; `EUR/USD` → `EURUSD`; `BRK.B` → `BRK`; `Z4` → `Z` (one-letter run: no month drop).
   (Rev 2.1 steps 3–4, "name minus a trailing month + 1–2 digit year", left `ESZ2024` unchanged; the code on `6853398` (main before #90) L109–119 already used the letter-run form but drops leading digits and does not remove `/`.)
3. **MC's stored root is primary (PR-M2 resolver; rev 2.3, Ettienne; CONFIRMED by Ettienne `_RootDump` 2026-11-03, relayed by Coder).** MC stores the root itself: `Bars.Info.ASymbolInfo2.SymbolRoot` (PG §4.2.3, `/workspace/research/pg11.txt` L901; struct L3828–3867, field L3833). dxFeed **populates it for futures and stocks** (@ES → `ES`, @CL → `CL`, @6E → `6E`, SPY → `SPY`; §4.4.3). The M2 resolver takes it as the **primary** root (trimmed, uppercase invariant); the #90 parser (`_MtfMath.SymbolRoot`, steps 1–2) is a **pure fallback**, used only when
   `(s.UnsupportedFields & MTPA_SymbolFields.eMTPA_SF_Root) != 0` (typed; also covers `ALL` = -1; `MTPA_SymbolFields` is not `[Flags]` but its values are powers of two, so the bitwise test is valid) **or** `string.IsNullOrWhiteSpace(s.SymbolRoot)`.
   Never a string or `ToString()` test on the enum (R1). When both roots are non-empty and `!_MtfMath.SameRoot(mcRoot, parsedRoot)`, log one **Warning** `INSTR root-crosscheck mc(<mcRoot>) parsed(<parsedRoot>) name(<Info.Name>)`; **MC's root wins** (it feeds presets, roll inheritance and the same-root checks). The INSTR line records `rootSrc=mc|parsed`. PR-M1a (`_MtfMath`, MC-free, #90) keeps the parser only; it has no access to `Info`.
   **As merged (rev 2.4):** `InstrumentRoot.Pick` (IP L308–322) implements the choice (MC root trimmed + uppercase; for Spot FX the `/` rule is applied to MC's root too); `Resolve` (IP L298–304) refuses `unknown-field:root` when both are empty. The Warning is logged once at StartCalc (`InstrumentStreams.Start`, IP L934). **Closed 2026-11-04:** the fallback test `(UnsupportedFields & eMTPA_SF_Root) != 0` compiles in PLEditor (MI-T1, §4.1) and the MC runs read `rootSrc=mc` on 6E, ES and AAPL. The parsed-fallback branch itself was not exercised in MC (no symbol flagged the root unsupported); it is covered by the MC-free tests.
   **MC-internal cross-check (every stream, before presets):** `s.PriceScale × Info.PriceScale = 1` within ±1e-9 relative (ES/CL/SPY 0.01 × 100; 6E 1E-05 × 99999.999999999985 = 0.99999999999999985) and `s.BigPointValue == Info.BigPointValue`. On a mismatch: Error + throw `mismatch:mcPriceScale` / `mismatch:mcBigPointValue` with a clear message, e.g. `M-INSTR: <ENGINE> cannot run on @6E (class Future, root 6E, sessions Default): mismatch:mcPriceScale (ASymbolInfo2.PriceScale 1E-05 × Info.PriceScale 99999.999999999985 = <x>, expected 1 ±1e-9). Check the symbol's settings in QuoteManager.` (§6.3). `MinMove` is equal in all four rows and is logged, not thrown on.
4. **Class-aware handling (PR-M2 resolver only):** stocks keep their share-class suffix (`BRK.B` is its own symbol; the M1a root `BRK` is used only for the MTF/DVP same-root checks), and the `/` gate of step 1 applies to Spot FX only. Presets (PR-M2) match the step-2 root exactly; the rev 2.1 longest-prefix search is dropped because step 2 already isolates the root (`MES` ≠ `ES`, `6E` ≠ `6B`).
5. Other classes when the fallback applies (MC gives no root or flags it): the cleaned name with the same step 2 (crypto `BTCUSD` → `BTCUSD`, `AAPL` → `AAPL`).
6. An empty root (MC and parser both empty) is an error (`RootError` and roll inheritance never compare empty roots).
7. Presets match on root **and** class ("ES" as a stock never gets the ES futures preset).
8. **Cross-check (futures with a preset):** detected tick = `MinMove/PriceScale` and `BigPointValue` equal the preset within 1e-9 relative, else Error + throw `mismatch:tick` / `mismatch:bigPointValue`. The tolerance covers MC's binary priceScale: @6E reports `priceScale(99999.999999999985)` with `minMove(5)` (`01M6HTKV3SV1Q5QDXBFTCH8D3K.txt`), raw tick 5 / 99999.999999999985 = `5.0000000000000009E-05` as .NET Framework `ToString("R")` prints it (17 digits; .NET 8 prints `5.000000000000001E-05`) vs the preset 0.00005, relative error ≈ 1.5e-16 (**CONFIRMED** covered; rev 2.3's `…07e-5` was wrong). **After the cross-check passes the profile carries the preset's exact tick and bigPointValue** (IP L792–793, preset values chosen just above), and `tickValue = tick × bpv` (0.00005 × 125000 = 6.25); without a preset it carries the detected tick and `Info.BigPointValue`. Profile facts on the INSTR line and in the profileHash canon are formatted with `InstrumentText.Fact` = `G15`, invariant (IP L111), so @6E prints `tick(5E-05)` on both runtimes; `InstrumentText.Num` = `"R"` (IP L108) is used **only in diagnostics** (mismatch/refusal texts), whose digits may differ between .NET Framework and .NET 8. The cross-check order and which checks are enforced in M3: §6.2. A detected tick ≤ 0 (or PriceScale ≤ 0) with a preset is `mismatch:tick` (enforced), not `tick`. @ES 25/100 bpv 50, @MES bpv 5, @CL 1/100 bpv 1000, @GC 1/10 bpv 100, @6E bpv 125000 all equal the §8.1 presets.
9. **No preset:** roll rule, contract months, expiry and cost are Unknown; an engine that needs one throws (§6.4 S5).

### 4.4 VERIFY dump `_InstrDump.Strategy.CS` (PR-M0; typed, no reflection)
Ettienne runs it once on @ES (his live chart, Default sessions), @ES on the "CME Globex" template (the D9a/canary chart), @MES, @CL, @GC, @ZN, @6E, one FX pair, one crypto symbol and one stock, and once in Portfolio Trader. Output: see **§4.4.1** (same directory as the standard MCSignal log, N6 DECIDED). Doubles via `ToString("R", CultureInfo.InvariantCulture)`. Restart MultiCharts after changing any symbol's sessions in QuoteManager (cached until restart, §4.5 setup note). 

#### 4.4.1 Output file (N6 DECIDED)
- **Directory = the directory of the standard log file the host already creates.** The resolver on `7dbdb1b` is in `_CustomSignal.Strategy.CS`, nested `LogFileSingleton` constructor **L71–91**: `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "MCSignalLogs")` (L79), `Directory.CreateDirectory` (L80), a zero-length write to prove access (L82), and on any exception the fallback `Path.GetTempPath()` (L85–88). The resolved file is exposed as `LogFileSingleton.FilePath` (L107) and obtained via `LogFileSingleton.GetInstance` (L93; used at L371 and L395). The same two-step rule is duplicated inline in `TradeJournal` (constructor L168–184, dir L176, fallback L182, `MCJournal_`/`MCEvents_` files).
- **Reuse, no new path.** `_InstrDump` derives from `_CustomSignal` (precedent: `_MtfProbe : _CustomSignal`, `_MtfProbe.Strategy.CS` L89). PR-M0 adds one accessor to `_CustomSignal`, no behaviour change: `protected string LogDirectory() { if (m_logger == null) m_logger = LogFileSingleton.GetInstance(null); return Path.GetDirectoryName(m_logger.FilePath); }` (same lazy pattern as `traceAlways`, L395). It therefore returns the directory of the log file actually created, including the `%TEMP%` fallback. No `const` path, no `C:\Share` path, no new environment lookup in `_InstrDump`.
- **Name**, following the existing `MCSignal<yyyyMMdd-HHmmss>-<pid>.csv` (L76) / `MCJournal_<runId>.csv` / `MCEvents_<runId>.csv` pattern: `MCInstrDump_<symbol>_<yyyyMMdd-HHmmss>-<pid>.txt`, timestamp `DateTime.Now` with `CultureInfo.InvariantCulture`, `<symbol>` = `Bars.Info.Name` with every `Path.GetInvalidFileNameChars()` character and `/` replaced by `_` (`@ES` stays `@ES`; `EUR/USD` → `EUR_USD`). One file per symbol per run (Portfolio Trader: one per symbol).
- **Writing:** one `File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false))` per line (UTF-8 without BOM, as the log, L66), doubles `ToString("R", CultureInfo.InvariantCulture)`. On the first line `_InstrDump` writes one trace line to the standard log, `INSTRDUMP file(<full path>)` (same form as `JOURNAL file(...)`, L2407), and one `Output.WriteLine` with the path. IO errors go to Output and never stop the strategy.
- **Collecting (PowerShell one-liners, no variables).** List the dumps in both possible directories, newest last:
  `Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCInstrDump_*.txt' -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime | Format-Table -Property LastWriteTime, Length, FullName -AutoSize`
  Make a Desktop folder for them:
  `New-Item -ItemType Directory -Force -Path (Join-Path ([Environment]::GetFolderPath('Desktop')) 'InstrDump')`
  Copy every dump into it:
  `Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MCSignalLogs'), ([System.IO.Path]::GetTempPath()) -Filter 'MCInstrDump_*.txt' -ErrorAction SilentlyContinue | Copy-Item -Destination (Join-Path ([Environment]::GetFolderPath('Desktop')) 'InstrDump') -Force`
  Then send the `InstrDump` folder (or attach the files to the PR-M0 thread); they are committed as `tests/InstrumentTests/fixtures/MCInstrDump_*.txt` (data only; nothing in M1 depends on them).

- **A1 (compiles today):** `name, cat (log), catInt, exch, tz (log), minMove, priceScale, bpv, bvo (log), res.Type (log), sessionName`. Description, PointValue and Resolution.Size are already used in compiled code (`_EconomicCalendar` L231/L243/L679, `_CustomSignal` L966/L2245) and are not dumped (`Info.SessionName`, confirmed-PG; record whether it prints "Default" or the template name).
- **A2 Sessions at StartCalc vs first bar:** print `Bars.Sessions` (`n`, then each `StartDay StartTime -> EndDay EndTime`) **in StartCalc and again on the first CalcBar**, and the first bar's `Bars.Time[0]` and `Bars.LastBarInSession`. Records: populated at StartCalc (yes/no/null), identical at the first bar, multiple sessions per day kept or collapsed (forum: fixed in MC .NET 9.0). **Result: CONFIRMED** populated and identical in all 13 runs; multiple sessions per day kept (GLOBEX: 9 entries, 17:00–15:15 and 15:30–16:30 each weekday).
- **A3 Session and bar zone:** run @ES and @CL each twice, Format Instrument → Time Zone = **Exchange**, then **Local**. Compare the same bar's `Time[0]` and the printed session times between runs: (i) whether `SessionObject` times follow the chart zone (staff: "main data series" zone); (ii) the exchange offset for NYMEX/COMEX (settles row 4, L2621). **Result: CONFIRMED** (i): session and bar times follow the chart zone (@ES 17–16 Exchange, 18–17 Local); (ii) @CL and @GC show the same 18:00–17:00 either way, see row 4.
- **A4 Multi-series / Portfolio:** with Data2 on a different template, print Data2's sessions (`BarsOfData(2).Sessions`) to confirm per-series sessions; in Portfolio Trader, print each symbol's sessions and `Info.TimeZone` (PT time zone is one global setting). **Result:** per-series sessions **CONFIRMED** (Data1 Default n=5, Data2 GLOBEX n=9, `01M6HSDBFH2D6H21PMYXYKFJHV.txt`); Portfolio Trader: the PR-M0 dump was incomplete (only @ES written, ds2–10 absent, `01M6HSDBFH56AB94QP1SZ4NFP7.txt`); **CLOSED 2026-11-04** by the PT per-symbol test on `5c9ccd2` (below).
- **B (resolved by `_RootDump`, rev 2.3; Ettienne `_RootDump` 2026-11-03, relayed by Coder):**
  - **B1 expiry:** `ASymbolInfo2.SymbolExpiry` is a **placeholder** on continuous futures (2038-01-01 on @ES, @CL, @6E) and stocks (1900-01-01 on SPY). It is used **only when strictly after 1900-01-01 and strictly before 2038-01-01** (i.e. a dated contract); otherwise expiry and roll come from the roll calendar (§8.1) and the roll detector. `Info.Expiration` stays VERIFY and is not needed.
  - **B2 currency:** `ASymbolInfo2.SymbolCurrency` (typed `MTPA_MCSymbolCurrency`): **CONFIRMED** USD for all four; non-USD throws (Q8). `StrategyCurrencyCode` is not needed.
  - **B3 cross-checks:** `SymbolRoot`, `SymbolExchange`, `SymbolCategory`, `MinMove`, `PriceScale`, `BigPointValue` **CONFIRMED** (§4.4.3); `PriceScale` convention: struct = 1/`Info.PriceScale` (the PG "for ES = 1/100" example, L3853, is the struct's); the resolver check is §4.3 step 3. `SymbolCategory` is typed (`MTPA_MCSymbolCategories`) and is logged, not used for classification (§4.2 stays on `Info.Category`).
  - `_RootDump` was Coder's throwaway; not committed. Its four rows are the MI-T2/MI-T3 fixtures (plain strings/ints/doubles in the MC-free core).

`{i.Category}` etc. are formatted only for the human log; nothing parses them (R1).

#### 4.4.2 PR-M0 dump results (Ettienne's runs, 2026-11-03 17:28–17:43 ET)
Thirteen fixture files from the PR-M0 `_InstrDump` build (rev 2.3: the 6E rerun `01M6HTKV3SV1Q5QDXBFTCH8D3K.txt` replaces the discarded `01M6HSDCCJZXJHNB165KSF6Z5B.txt`; the latter is still listed in `index.txt` but is not committed), indexed in `/workspace/instrdump/index.txt` (to be committed as `tests/InstrumentTests/fixtures/`; the file names are the upload names, not the `MCInstrDump_*` names). Not run yet: @ZN, an FX pair, a crypto symbol (B1/B2 resolved by `_RootDump`, §4.4.3).

| File | Run | Facts read from it |
|---|---|---|
| `01M6HSDCPGEWFS9VAYZG2C808V.txt` | @ES, Exchange, Default | n=5, Sun 17:00 → Mon 16:00 … Thu 17:00 → Fri 16:00; same at StartCalc and first bar; session-end bars 16:00 |
| `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt` | @ES, Exchange, GLOBEX template | `sessionName(CME: Equity Index Futures [E-mini] (GLOBEX))`; n=9 (17:00–15:15 + 15:30–16:30); **session-end bars only at 16:00, none at 15:15** (R14) |
| `01M6HSDC526NQSHVTNJG9AQ7A7.txt` | @MES, Exchange, Default | as @ES, bpv 5 |
| `01M6HSDDB20HAY23SAABKQ0MQ5.txt` / `01M6HSDCMNQG1HPR0B9XQWC4S2.txt` | @CL Exchange / Local | 18:00–17:00 in both; bars end 17:00 in both; minMove 1, priceScale 100, bpv 1000 |
| `01M6HSDC20TJ572NEKZBKKRBWD.txt` | @GC, Exchange | 18:00–17:00, bars end 17:00; minMove 1, priceScale 10, bpv 100 |
| `01M6HTKV3SV1Q5QDXBFTCH8D3K.txt` | @6E, Exchange (rerun 2026-11-03 18:05 ET, after switching @6E to the Exchange & ECN sessions and restarting MC) | `cat(Future)`, exch CME, priceScale 99999.999999999985, minMove 5, bpv 125000; Default session Sun–Thu 17:00 → next day 16:00 (Exchange), first bar 17:06, session-end bars at 16:00 (`lastBarInSession(True)`): **matches ES**. Chart has `bvo(TickCount)`: a futures chart on Tick Count must throw under R7 (volume basis), so this fixture is for **instrument and session facts only**, never a volume-engine fixture. Replaces `01M6HSDCCJZXJHNB165KSF6Z5B.txt` (misconfigured custom sessions, **discarded**; not a fixture) |
| `01M6HSDBFJ9WRF71G51B82VKCH.txt` | @ES, Local | 18:00–17:00; first bar 18:07, session ends 17:00 (= Exchange run + 1 h) |
| `01M6HSDBG7NET9EVPFJ8Z43MR5.txt`, `01M6HSDBWZ22Q03T3AV5GWP4CY.txt`, `01M6HSDBFH2D6H21PMYXYKFJHV.txt` | @ES Data1 Default + @ES Data2 GLOBEX (three identical runs apart from run id) | Data1 n=5, Data2 n=9 (per-series sessions); Data1 16:00 bar of 2026-10-26 logged as a session end twice (R14 idempotence) |
| `01M6HSDBFH56AB94QP1SZ4NFP7.txt` | Portfolio Trader, @ES + @CL, Local | `maxDataStream(10)`, ds2–10 `absent`; only the @ES dump was written (superseded: PT per-symbol test passed 2026-11-04) |
| `01M6HSDC62GCMK5ZANKB61PD5T.txt` | SPY, Exchange | `cat(Stock) catInt(2)`, exch ARCA, `bvo(TickCount)`, Mon–Fri 04:15–20:00, session ends 20:00 |

**CONFIRMED:** (1) `Bars.Sessions` is filled at StartCalc and identical on the first bar (all 13); (2) session and bar times follow the chart time zone (@ES 17–16 Exchange vs 18–17 Local; @CL and @GC 18–17 either way); (3) sessions are per data series and multiple sessions per day are kept; (4) @6E priceScale is within the 1e-9 tolerance; (5) SPY is Stock / TickCount / 04:15–20:00; (6) `SessionName` returns the chart's template name.
**NEW MC behaviour:** `LastBarInSession` did not fire at the GLOBEX 15:15 break, only at 16:00 (and on the 15:30–16:30 session it fired at 16:00, before the listed 16:30 end, because data stops at 16:00) → R14, MI-T16.
**CLOSED 2026-11-04 (Ettienne, `5c9ccd2`): Portfolio Trader per-symbol test.** One PT log (Local tz) for @6E, @ES and AAPL; each stream printed its own root, tick, bpv, sessions and calendar:

- **ES:** tick 0.25, bpv 50, `cost(17.00 src=Ettienne 2026-10-31)`, `floor(14)`, `rth(09:30-16:15 src=EquityIndexLegacy)`, profileHash `e1927ccf1ea6f41e` (Local tz; reproduced on the box from the Default list `Sun–Thu 18:00→17:00`).
- **AAPL:** `class(Stock)`, `preset(none)`, tick 0.01, `calendar(Nyse)`, `rth(09:30-16:00 src=ChartDaySession)`, profileHash `41886799d411dd0d`.
- **@6E:** profileHash `9523b25025f9ed5b` (Local, sessions 18:00→17:00 ET) vs `55576df6a8a99c05` on the Exchange-tz chart: the evidence for D2 (§12.4).

The PR-M0 question (did @CL not start?) is moot. Ettienne's earlier @6E rerun is closed (below).

**CLOSED (rev 2.3): @6E zone.** Ettienne's QuoteManager had @6E on **Use Custom Sessions**, Sun–Thu 18:00 → next day 17:00, with Session TimeZone = **Exchange (CT)**: ET hours entered as CT. A user configuration error, not MC behaviour (CME 6E trades 17:00–16:00 CT like ES, research `/workspace/m-instr/mc-session-templates-research.md` "Observation: 6E", https://www.cmegroup.com/trading/why-futures/welcome-to-cme-fx-futures.html). He switched @6E to the Exchange & ECN sessions and restarted MC; the rerun `01M6HTKV3SV1Q5QDXBFTCH8D3K.txt` reads Sun–Thu 17:00 → 16:00 Exchange, first bar 17:06, session ends 16:00, matching ES. It is the 6E fixture; `01M6HSDCCJZXJHNB165KSF6Z5B.txt` was misconfigured and is discarded (dropped from the fixture list).

#### 4.4.3 `_RootDump` results (Ettienne `_RootDump` 2026-11-03, relayed by Coder; dxFeed)

| Symbol | root | exch | cat | ccy | SymbolExpiry | minMove | PriceScale struct vs Info | BPV struct vs Info | UnsupportedFields |
|---|---|---|---|---|---|---|---|---|---|
| @ES | ES | CME | FUTURE | USD | 2038-01-01 | 25 | 0.01 vs 100 | 50 vs 50 | 0 |
| @CL | CL | NYMEX | FUTURE | USD | 2038-01-01 | 1 | 0.01 vs 100 | 1000 vs 1000 | 0 |
| @6E | 6E | CME | FUTURE | USD | 2038-01-01 | 5 | 1E-05 vs 99999.999999999985 | 125000 vs 125000 | 0 |
| SPY | SPY | ARCA | eMTPA_MCSC_STOCK | USD | 1900-01-01 | 1 | 0.01 vs 100 | 1 vs 1 | 0 |

`SymbolName` = `Info.Name` in every case. `MTPA_SymbolFields` values as in §4.1.

### 4.5 Sessions from the chart (R12; DECIDED Q1/Q2)
**What MC does (research doc `/workspace/m-instr/mc-sessions-research.md`).** Each data series has its own session setting (Format Instrument → Settings → Sessions = **Default**, i.e. the symbol's QuoteManager sessions, or a named template); in Portfolio Trader each symbol has its own session but the time zone is one global setting. Scripts cannot set sessions. Data outside sessions is filtered out; time bars always break on session boundaries and align to the session start; `Time` is the bar close. Holidays are removed by QuoteManager holiday lists and **cannot be read from code**. **Templates (research `/workspace/m-instr/mc-session-templates-research.md` §1, §3, §7):** a chart uses Default (the symbol's QuoteManager sessions: exchange sessions, custom sessions, a custom template or the symbol dictionary) or a named template; templates, built-in ones included, are editable in **QuoteManager → Tools → Session Templates** (Add/Edit/Delete, rows with Session Time and Session End; https://www.multicharts.com/trading-software/index.php?title=Setting_Custom_Session_Templates, https://www.multicharts.com/discussion/viewtopic.php?t=5890), but not from a script (staff, https://www.multicharts.com/discussion/viewtopic.php?t=53293). **"Combined" templates mean two sessions per day** (overnight + day, two daily bars; https://www.multicharts.com/discussion/viewtopic.php?t=45183), not one merged 23-hour session; "Open Outcry"/"Open Auction" templates describe pit sessions closed in 2015 and survive only as RTH-style windows.

**Setup note (MC caches sessions).** MultiCharts caches QuoteManager symbol sessions until a restart: after changing a symbol's sessions in QuoteManager, **restart MultiCharts** before running charts, dumps or tests (the @6E rerun, §4.4.2, needed it).

**Environment caveat (not an engine rule).** The built-in **"CME: Equity Index Futures [E-mini] (GLOBEX)"** template is stale: its 15:15–15:30 CT gap is the halt CME removed on 27/28 June 2021 (https://www.cmegroup.com/notices/electronic-trading/2021/06/20210621.html), and its 15:30–16:30 row matches the hours **before November 2012** (2012–2021 sessions ended 16:15, https://www.cmegroup.com/tools-information/lookups/advisories/market-data/20121112.html; pre-2012 sheet https://www.dormantrading.com/TraderTools/CMEeminisp500.pdf); current ES hours are 17:00–16:00 CT (https://www.cmegroup.com/markets/equities/sp/e-mini-sandp500.html). This template is on the **D9a/canary chart** (§4.4), so that chart has a session boundary at 15:15 and a listed end at 16:30. Engines **take sessions as given** (R12) and do not correct or warn about it; changing the canary chart's template is Ettienne's environment decision and would change MI-T15 baselines.

**Reading.** `SessionReader` (host) reads `BarsOfData(n).Sessions` into the MC-free struct `_MtfMath.Session` (#88 head, L47–52: `StartDay, StartTime, EndDay, EndTime`) **in StartCalc** (populated there, CONFIRMED §4.4.2) and **re-checks on the first CalcBar of each stream** (must be identical, else Error + throw `sessions:changed`, S1). Session ends follow R14 (from the list's `EndTime`s, not `LastBarInSession` alone). Null or empty sessions → throw `sessions:none` for every session-using engine (§6.4 S1). Template name: `Info.SessionName` on the INSTR line, log only. Whether the chart uses Default is not exposed (not documented) and is not checked: the chart's template is the truth.

**Zone.** Session times are in the bar time zone of the stream, i.e. the chart's Format Instrument time zone (**CONFIRMED** A3, §4.4.2: @ES 17:00–16:00 on Exchange, 18:00–17:00 on Local, bar times shift by the same hour; @CL/@GC identical either way). No conversion is needed before deriving. ET-anchored logic that remains (EVENT rows, the frozen G0/REGGATE trial windows) converts bar time to ET as today: Local → `TimeZoneInfo.Local`, GMT → UTC, Exchange → the `exchangeTimeZoneId` map (L2614–2626, kept only for this; its NYMEX/COMEX entry stays Eastern: A3 shows MC's exchange time for @CL/@GC equal to ET on Ettienne's setup, row 4).

**Derived fields (MC-free, `<instr-core>`):**
- `tradingDayKey(t)` = the calendar date, in session time, of the **end** of the session that contains bar close `t` (start exclusive, end inclusive). Several sessions ending on the same date share the key (e.g. the GLOBEX template 17:00–15:15 + 15:30–16:30, `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt`). On the CME Globex template (17:00–16:00 CT) this equals today's `GlobexTradingDayEt` (18:00 ET roll) for every bar; on a 24×5 FX template ending 17:00 ET it equals today's ET+7h rule; on a 00:00 UTC crypto template it equals the UTC date. A bar outside every session (should not occur, MC filters) → Error + throw `sessions:bar-outside`.
- `dayOpen` = start of the first session of the trading day (TOD minute-of-session; replaces 18:00 ET at VPFUT L489–493 and the default at L949).
- `sessionEnd` = end of the last session of the trading day, from the list (R14; on the GLOBEX template 16:30 even though the last bar closes at 16:00) (FLOW tick-chunk boundary, replaces `TickChunkBoundaryEt` 17:00 and the rev-1 `rollTime − 1h`).
- `hasOvernight` = some session of the trading day crosses midnight in session time (`StartDay != EndDay`). Globex 17:00 CT Sun → 16:00 CT Mon: true; an 08:30–15:15 day session: false. For 24-hour single-day templates (e.g. 00:00–24:00 crypto) the value is fixed in PR-M4 from the A2 output (**VERIFY**).
- `rthSplit` = the RTH segment inside the trading day: **when the chart's sessions are day-only (`!hasOvernight`), the chart session is the RTH**. For a 24-hour chart (N1 DECIDED): ES/MES/NQ/MNQ use today's equity-index split from `_CmeSessionClock` (RTH 09:30, value end 16:00, session flag 16:15 ET), logged `rth src=EquityIndexLegacy`; any other root on a 24-hour chart has `rthSplit = null` (S7). No RTH window in any preset.
- `sessionsPerWeek`/`sessionsPerYear`: annualisation stays a class constant (252 futures/stocks, 365 crypto, 260 FX **VERIFY**), not derived.

**Multi-stream.** Each stream has its own derived fields. The #86/#88 DV/MTF Warning when Data2..N sessions differ from Data1 (`checkHtfSessions`, #88 head L1802–1822) stays unchanged, including its MC-1439 / Realtime-History Matching caveat: with RHM on and different session ends, the strategy stalls after the shorter session's end until the auxiliary series prints. In Portfolio Trader, mismatched bar-close times across symbols can make the portfolio money-management signal run several times per bar (MC staff); M-PORT behaviour is unchanged, and the INSTR line shows each symbol's sessions so a mismatch is visible.

**Removed because they existed only for hardcoded times** (PR-M4 unless noted): `_CustomSignal` L1888–1889 (`"Eastern Standard Time"` + `GlobexTradingDayEt` for the roll key → `tradingDayKey`); `autoTradingDay` L2581–2587 + L2462 (→ `tradingDayKey`); VPFUT `GlobexDate` key L361 and TOD 18:00 L489–493; `_CmeSessionClock` Globex open/close/halt defaults L48–61 as *trading-day* inputs; `TickChunkBoundaryEt` (PR-M6b). The exchange-zone map stays only for ET conversions (above). Holiday calendars stay (they are not session times; §6.4 S4, PR-M8).

**ES bit-identity.** On the CME Globex template (D9a and canary charts, `_DvpEngineTest` L28–48, `_TestGate0` L42) every derived day key, day open and session end equals today's constant. On a chart with another template (e.g. Ettienne's live @ES Default sessions) results follow that chart; this is the intended behaviour of the decision and is logged on INSTR, not warned.

---

## 5. Data model

### 5.1 `InstrumentProfile` (immutable; MC-free core `<instr-core>` in `_InstrumentProfile.Function.CS`)

| Field | Type | Source | Notes |
|---|---|---|---|
| `assetClass` | `EAssetClass { Future, Stock, SpotFx, Crypto, Index, Unsupported }` | detect (§4.2) | |
| `root` | string | `ASymbolInfo2.SymbolRoot` (CONFIRMED), parser only as fallback (§4.3 step 3) | "ES", "6E", "EURUSD", "BTCUSD", "AAPL"; never empty |
| `presetId` | string or null | preset | |
| `tick` | double | preset tick when a preset matched (after the §4.3 cross-check), else `MinMove/Info.PriceScale` | > 0 else `tick` refusal (no preset) / `mismatch:tick` (preset); printed with G15 |
| `bigPointValue` | double | preset bpv when a preset matched, else `Info.BigPointValue` | currency per 1.0 price move (renamed from rev-1 `pointValue`; MC's `Info.PointValue` is another member) |
| `tickValue` | double | tick × bigPointValue | of the carried values (ES 12.5, 6E 6.25) |
| `currency` | ISO code | `ASymbolInfo2.SymbolCurrency` (CONFIRMED, §4.4 B2) | non-USD throws (Q8) |
| `sessions` | `_MtfMath.Session[]` | `Bars.Sessions` (§4.5) via `SessionReader` (IP L996–1006) | read at StartCalc, re-read and compared on the first bar (`WithSessions`, IP L814–822) |
| `sessionName` | string | `Info.SessionName` | log only |
| `barZone` | `RequestTimeZone` + resolved tz id | `Info.TimeZone` (+ map for Exchange) | ET conversions only |
| `tradingDay` | derived from `sessions` | §4.5 | key = session-end date |
| `dayOpen`, `sessionEnd`, `hasOvernight` | derived | §4.5 | |
| `rthSplit` | (start, end) or null, + `rthSource ∈ {ChartDaySession, EquityIndexLegacy, None}` | §4.5, N1 | |
| `calendar` | `ECalendar { CmeEquityIndex, Nyse, NyseStandIn, Observed, None }` (IP L33; + CME product groups when verified, PR-M8) | preset; no preset: Stock → `Nyse`, Unsupported → `None`, else `Observed` (IP L252) | holidays/early closes only; no session times. `NyseStandIn` = NYSE business days (`EventRules.IsNyseBusinessDay`) standing in for the CME calendar for CL/GC/ZN/6E until PR-M8; explicit and logged (`calendar(NyseStandIn; calendar: NYSE stand-in (CME calendar pending M8))`) |
| `contractMonths` | month mask | preset | |
| `expiry` | `EExpiryRule` + params | preset; a dated contract's `SymbolExpiry` when strictly inside (1900-01-01, 2038-01-01) (§4.4 B1) | §8.2 |
| `roll` | `ERollRule` + params | preset | §8.2 |
| `sessionsPerYear` | int | class | 252 / 365 / 260 (FX **VERIFY**) |
| `cost` | `CostModel { roundTripCents (long), source }` | `InstrumentConfig.costs` | Unknown when absent |
| `costFloorTicks` | int | §9 (integer cents); computed **only when a preset matched**, with the preset tick/bpv (IP L778–779) | -1 = Unknown (no preset, no cost, or invalid config); printed `floor(Unknown)` |
| `cluster` | string | preset | same strings as L1611–1614 |
| `volumeBasis`, `volumeQuality` | | `Info.BVO` + class | R7 |
| `dataNotes` | string or null | `InstrumentConfig.dataNotes[root]` | Q7; printed as `dataNotes(…)` only when configured. **DIVERGENCE** from rev 2.3 "list of (from, what)": it is one free-text string per root |
| `sizingUnit` | Contracts / Shares / Units | class | |

### 5.2 Presets: exchange facts only (no times)
`InstrumentPresets`: static table in `<instr-core>`, one row per root (§8): tick, bigPointValue, currency, contractMonths, expiry, roll, calendar, cluster, source. Adding a root = a one-row PR with sources.

### 5.3 `InstrumentConfig` (protected on `_CustomSignal`; no inputs)
- `enabled` (log-only until PR-M4; then applied).
- `overrides`: typed `InstrumentPresetRow`, null by default (fix an exchange fact without a code PR; contains no session fields).
- `costs`: **asset class + root** → round trip in **cents** (`long`) (**D1 DECIDED**, Ettienne 2026-11-04, §12.4). One `costSource` string for all (default `Ettienne 2026-10-31`, printed `cost(17.00 src=…)`); values > 0.
    - **Key format:** `<Class>:<ROOT>`, where `<Class>` is the `EAssetClass` name exactly as `InstrumentText.ClassName` prints it (`Future`, `Stock`, `SpotFx`, `Crypto`, `Index`; `Unsupported` is not allowed) and `<ROOT>` is the resolved root, trimmed uppercase, as on the INSTR `root(…)` field. Code builds keys with one helper, `InstrumentConfig.CostKey(EAssetClass, string root)`, so no code parses a class name back from text; `Validate` accepts a key only when it equals `CostKey(c, r)` for some class `c ≠ Unsupported` and a non-empty uppercase `r`. A root-only key (`ES`) or a misspelt class is invalid → `unknown-field:config` (no silent migration of old keys).
    - **Defaults map:** ES 1700 → `Future:ES` 1700; MES 250 → `Future:MES` 250. `costSource` unchanged.
    - **Lookup:** `CostFor(assetClass, root)` reads only `CostKey(profile class, profile root)`.
    - **Class mismatch:** when no row exists for the symbol's own class but a row exists for the same root under another class (e.g. a stock `ES` while `Future:ES` is configured), that row is **not used**: the profile has no cost (`cost(Unknown)`), the floor is Unknown with its reason (`floor(Unknown src=cost-class-mismatch)`, §6.1/§9), and one Warning per stream at StartCalc names it: `INSTR ds(<n>) cost row Future:ES not used: symbol class Stock root ES`. Never silently applied, never silently dropped.
    - **Canon:** `InstrumentConfig.Canon()` keeps its form with the new keys, sorted ordinal: `costs=[Future:ES:1700,Future:MES:250,]` (cents after the last `:`). The profile Canon is unchanged (`cost=<cents>` or `-`; floor `-1` when Unknown; the src reason is printed, not hashed), so the pinned ES/MES/6E profileHashes do not change; only a profile that today shows a cross-class cost (stock `ES`: `cost=1700` → `cost=-`) gets a new hash.
    - **configHash:** not affected. On `5c9ccd2` `InstrumentConfig.Canon()` (IP L413–424) is called only by tests (IT L457, L546); it is not part of the RUN line (CS L2296–2302: `inputs[…]`, `event[EventConfig.Canon()]`, profileHash suffix) or of any engine `configHash` (e.g. `_DvpEngine` hashes `DvpConfig.Canon()` only). If a later PR (PR-M7 cost floor) puts `InstrumentConfig.Canon()` into a hash, it must be with the class+root keys from the start.
    - **M3 behaviour (as merged, superseded by PR-M4):** keyed by **root only** (IP L398, `CostFor(root)` L426–430; keys trimmed uppercase): a stock and a future with the same root share a cost (a stock ticker `ES` shows `cost(17.00 …)`, IT L609–611). M3 limits the damage by computing the floor only for a matched preset (§9).
- `applyCostFloor` (PR-M7, default false).
- `dataNotes`: root → note string.
- `enabled = false` skips `buildProfiles()` with one Info line `INSTR off (InstrumentConfig.enabled = false): no profiles, no cross-checks` (CS L2392–2396 @5c9ccd2): no INSTR lines, no RUN suffix.
- An invalid config (`Validate() != null`) is the log-only refusal `unknown-field:config`; its `overrides` row is not trusted for the cross-check (§6.2).
- `Validate()`, `Canon()` as in existing configs; `profileHash` outside every `configHash` (rev-3 Q7).

---

## 6. Logging, application and refusal

### 6.1 Lines (`engineSink(level)`, one `File.AppendAllText` per line, InvariantCulture)
- `INSTR ds(<n>) name(<Info.Name>) cat(<Info.Category label>) class(<EAssetClass>) root(<r> rootSrc=mc|parsed) preset(<id>|none) exch(<Info.Exchange>) tz(<setting>→<zone id>|unresolved|unavailable(<ExceptionType>)) sessionName(<Info.SessionName>) sessions(n=<k> <Day hh:mm>→<Day hh:mm>; …) [xsessions(<exchange zone id> n=<k> …; rth <hh:mm-hh:mm>|none) — PR-M4, D2] dayKey(SessionEnd) dayOpen(hh:mm) sessionEnd(hh:mm) overnight(yes|no) rth(<start>-<end> src=EquityIndexLegacy|ChartDaySession) | rth(none src=None) tick(<G15>) bpv(<G15>) tickValue(<G15>) ccy(USD) bvo(<label>) calendar(<ECalendar>[; <note>]) months(<letters>) expiry(<rule>|none) roll(<rule>[(n)]|none) cost(<c.cc> src=<costSource>)|cost(Unknown) floor(<n>)|floor(Unknown) cluster(<c>|none)[ dataNotes(<note>)] profileHash(<16 hex>)` at **Info**, once per stream after the first-bar session read (`InstrumentProfile.InstrLine`, IP L846–872 @5c9ccd2). `dataNotes(…)` appears only when `InstrumentConfig.dataNotes` has the root; it sits after `cluster` and before `profileHash`. `cat`, `bvo` and the tz setting are MC enum labels formatted by the host for the log only (R1; `unavailable(<ExceptionType>)` when the typed read throws, CS L2449–2474). The tz zone part is the resolved `TimeZoneInfo.Id`, `unresolved` when no zone resolves, or `unavailable(<ExceptionType>)` (CS `instrZoneLabel` L2465–2471).
  **Pinned (rev 2.4; the tests pin these exact strings).** ES, CME GLOBEX 9-entry list (IT T12 L537–539):
  `INSTR ds(1) name(@ES) cat(Future) class(Future) root(ES rootSrc=mc) preset(ES) exch(CME) tz(Exchange→Central Standard Time) sessionName(test) sessions(n=9 Sun 17:00→Mon 15:15; Mon 15:30→Mon 16:30; Mon 17:00→Tue 15:15; Tue 15:30→Tue 16:30; Tue 17:00→Wed 15:15; Wed 15:30→Wed 16:30; Wed 17:00→Thu 15:15; Thu 15:30→Thu 16:30; Thu 17:00→Fri 15:15) dayKey(SessionEnd) dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(09:30-16:15 src=EquityIndexLegacy) tick(0.25) bpv(50) tickValue(12.5) ccy(USD) bvo(Volume) calendar(CmeEquityIndex) months(HMUZ) expiry(ThirdFridaySoq) roll(EquityIndexCustomary) cost(17.00 src=Ettienne 2026-10-31) floor(14) cluster(EQIDX) profileHash(020deca78d6392b7)`
  6E, same template, NyseStandIn calendar (IT T13 L475; tz label as the test passes it):
  `INSTR ds(1) name(@6E) cat(Future) class(Future) root(6E rootSrc=mc) preset(6E) exch(CME) tz(Exchange) sessionName(test) sessions(n=9 Sun 17:00→Mon 15:15; …same 9 entries as ES…) dayKey(SessionEnd) dayOpen(17:00) sessionEnd(16:30) overnight(yes) rth(none src=None) tick(5E-05) bpv(125000) tickValue(6.25) ccy(USD) bvo(Volume) calendar(NyseStandIn; calendar: NYSE stand-in (CME calendar pending M8)) months(HMUZ) expiry(ThirdWedMinus2Bd) roll(BusinessDaysBeforeExpiry(5)) cost(Unknown) floor(Unknown) cluster(FX_EUR) profileHash(8e8b3e9b0e230e12)`
  The 6E hash `8e8b3e9b0e230e12` is for the **synthetic** 9-session GLOBEX list the test passes. **MC result (MI-T15, Ettienne 2026-11-04):** the real @6E chart (Exchange tz, Default session, BVO TickCount) printed `tick(5E-05) bpv(125000) tickValue(6.25)`, the NyseStandIn calendar note, `roll(BusinessDaysBeforeExpiry(5))` and profileHash **`55576df6a8a99c05`**; the box (.NET 8) reproduces it byte-for-byte from the canon with `sessions=n=5 Sun 17:00→Mon 16:00; … Thu 17:00→Fri 16:00`, so the hash is **runtime-stable** (.NET Framework = .NET 8). It differs from the pin only by the session list.
- **Number formats.** Profile facts on the INSTR line and in the hash canon use `InstrumentText.Fact` = `ToString("G15", InvariantCulture)` (IP L111): `5E-05`, `0.25`, `12.5`. `InstrumentText.Num` = `"R"` (IP L108) is used only inside diagnostics (mismatch and refusal texts), e.g. the raw 6E tick `5.0000000000000009E-05` (.NET Framework; .NET 8 prints `5.000000000000001E-05`). Diagnostic digits may differ between runtimes; profile text and hash may not.
- **profileHash** = FNV-1a 64 over the canonical profile text (`Canon`, IP L825–835; `ProfileHash` L836–841): class, root, preset, tick, bpv, ccy, calendar, months, expiry, roll, cost cents, floor (-1 when Unknown), cluster, sessionsPerYear, the session list and the RTH split (in bar time today; in **exchange time from PR-M4**, D2 above). Not hashed: name, exchange, sessionName, the tz/cat/bvo labels, costSource, `dataNotes`, tickValue and the derived day fields. **RUN suffix:** the RUN line ends with ` profileHash(<ds>=<hash>;…;<ds>=none)` over the started streams (`none` = no profile: a Build refusal or a failed typed read); empty when M-INSTR is off or no stream started (`InstrumentStreams.RunSuffix`, IP L953–963; appended at CS L2302 after the `configHash` part). It is **outside every `configHash`** (rev-3 Q7); IT T12 L544 pins the format.
- **Log-only line (M3)**, at **Warning**: `INSTR ds(<n>) log-only, not enforced until PR-M4: <refusal>` (`InstrumentStreams.LogOnlyLine`, IP L904–907), where `<refusal>` is the full §6.3 exception text, or for a failed typed read `reading Data<n> <name> failed (<ExceptionType>: <message>); no profile` (CS L2415) / `reading Data<n> sessions at StartCalc failed (…)` (L2421) / `… sessions on the first bar failed (…)` (L2446).
- **Root cross-check** (§4.3 step 3), once per stream at StartCalc, **Warning**: `INSTR root-crosscheck mc(<r>) parsed(<p>) name(<n>)`.
- **Unexpected exception inside a guarded step**: `INSTR ds(<n>) unexpected <ExceptionType>: <message>` at **Error**, then rethrown (IP L925).
- **M-INSTR off**: `INSTR off (InstrumentConfig.enabled = false): no profiles, no cross-checks` at Info (CS L2394); no INSTR lines, no RUN suffix.
- **D2 (DECIDED, Ettienne 2026-11-04; PR-M4): profileHash over sessions in exchange time.** Today `Canon` hashes `InstrumentText.Sessions(sessions)` in the **bar** time zone, so one instrument hashes differently per chart/PT zone: @6E Exchange-tz chart (Sun 17:00→Mon 16:00 … Central) `55576df6a8a99c05`, @6E in Portfolio Trader on Local (18:00→17:00 ET) `9523b25025f9ed5b`. From PR-M4:
    - **Zones.** Bar zone = `resolveBarTimeZone(Bars.Info)` (CS L2849–2865). Exchange zone = `exchangeTimeZoneId(Bars.Info.Exchange)` (CS L2871–2880, virtual; CME/CBOT/CBT/GLOBEX/CBOE → `Central Standard Time`, NYMEX/COMEX/NYSE/NASDAQ/ARCA → `Eastern Standard Time`), looked up **whatever the chart's `Info.TimeZone` setting is**. The lookup for the profile must be strict: `findTz` (CS L2882–2888) today returns `America/New_York` for any id it cannot find, and `resolveBarTimeZone` returns Local for an unknown `RequestTimeZone`; the profile path must not use either fallback (a strict variant that throws instead).
    - **Refusal, never a silent fallback.** Bar zone unresolved → `tz:bar`; `exchangeTimeZoneId` returns null or the id does not resolve → `tz:exchange` (§6.3; Build refuses, RUN suffix `ds=none`, enforced like every other §6.3 refusal in M4). Action text: "add the exchange to exchangeTimeZoneId" / "set the chart time zone". A symbol on an exchange outside the map (FX, crypto feeds) is refused until its exchange is mapped.
    - **Conversion.** Each session's start and end (`DayOfWeek` + time) is converted bar zone → exchange zone on the trading week of the first bar (Sunday on or before the first bar's date, bar time), via `TimeZoneInfo.ConvertTime`; identity when the two zones have the same id. Same-DST zone pairs (ET↔CT) give the same text in any week; a **GMT** chart is **VERIFY** (does MC shift GMT sessions across DST?), to be checked with one MC run before M4 merges.
    - **What is hashed.** `sessions=` in exchange time, and `rth=` recomputed by `RthSplit.Of` on the exchange-time list (rth **is** in `Canon`, IP L834; a `ChartDaySession` rth is session-derived; `EquityIndexLegacy` is fixed ET constants and is unchanged). The zone id itself is **not** hashed (Windows `Central Standard Time` vs `America/Chicago` on .NET 8 would break runtime stability). `dayOpen`/`sessionEnd` are not hashed and stay in bar time.
    - **What engines and the INSTR line use.** The profile keeps `sessions`, `rthSplit`, `dayOpen`, `sessionEnd` in **bar time** for the engines (R12, bar times are in bar zone). The INSTR line keeps `sessions(…)`, `dayOpen`, `sessionEnd`, `rth(…)` in bar time and adds `xsessions(<exchange zone id> n=<k> …; rth <hh:mm-hh:mm>|none)` = exactly the hashed exchange-time text.
    - **Expected pins after M4 (box-computed from the canon; confirm in MC).** @6E Default `55576df6a8a99c05` on **any** chart/PT zone (PT Local `9523b25025f9ed5b` goes away); @ES Default (Sun–Thu 17:00→16:00 CT, EquityIndexLegacy rth) `6cff053d1b215324` on any zone (PT Local `e1927ccf1ea6f41e` goes away); AAPL `41886799d411dd0d` unchanged when the PT PC is on ET (bar zone = exchange zone). The IT pins ES `020deca78d6392b7` / 6E `8e8b3e9b0e230e12` stay **only if** the test declares bar zone = exchange zone; every test pin built from a non-exchange bar zone changes, and the tests gain the invariance checks of §11.1 item 9.
- **PR-M4 requirement (rev 2.4):** `floor(Unknown)` must say why, e.g. `floor(Unknown src=no-preset)`, `src=no-cost`, `src=cost-class-mismatch` (D1, §5.3), `src=config-invalid`. Today the line prints only `floor(Unknown)`.
- `CFG <ENGINE> applied{…} undefined{output=reason;…} degraded{mode}` at Info, once per engine.
- `SKIP <ENGINE> reason(<code>) …` at **Info**.
- Mismatches (and every other enforced refusal, §6.2) go out at **Error** before the throw.

### 6.2 Application
`buildProfiles()` runs in `StartCalc` for instrument fields; session-derived fields are filled on the first CalcBar of each stream, and engines whose configuration needs them (TOD, AVWAP session date, FLOW chunk plan, RANGE/WEEKLY session sources, VPFUT day key) finish configuring there, before their first update. Engines get the profile through their existing config objects; engines never read `Bars.Info` or `Bars.Sessions` directly.

**As merged in PR-M3 (#97, @5c9ccd2); log only, no engine reads a profile yet.**

- `buildProfiles()` (CS L2388–2428) runs in StartCalc **before** the engines start (CS L2179). Per stream: typed read (`InstrumentMcFields.Read`, IP L979–992), `InstrumentProfile.Build`, StartCalc sessions via `SessionReader`, then `InstrumentStreams.Start` (logs the root Warning). `profilesFirstBar()` (CS L2433–2462) runs on the first CalcBar, before the RUN line (CS L2191): re-reads sessions, `InstrumentSessions.Require` (S1, `sessions:changed`), derives day key/open/end/RTH split (`WithSessions`, IP L814–822) and prints the INSTR line.
- **`Build` is two-phase** (IP L756–811). **Phase 1, the enforced cross-checks**, runs only when the class is not Unsupported: root pick → `Checks.McInternal` on the raw inputs (`mismatch:mcPriceScale` / `mcBigPointValue`) → raw tick `MinMove / Info.PriceScale` (0 when PriceScale ≤ 0) → preset choice (a valid config's `overrides` row when it matches class + root, else `InstrumentPresets.Find`) → `Checks.Preset` when a preset matched (`mismatch:tick`, also for a raw tick ≤ 0; `mismatch:bigPointValue`) → the cost floor, only when the config is valid and a preset matched (`mismatch:tickValue`, §9). **Phase 2, the log-only refusals**, in this order: `class` → `unknown-field:config` (invalid `InstrumentConfig`) → `unknown-field:root` → `tick` (no preset and tick ≤ 0) → `currency` → `calendar` (only when the profile has an expiry or roll rule, R6).
- **Enforced set in M3** (`InstrumentStreams.Enforced`, IP L899–902): reason `mismatch:*` or `sessions:changed`. `InstrumentStreams.Guard` (IP L912–928) buffers the Error line; an enforced refusal writes it and rethrows (the study stops). Every other `InstrumentException` writes only the Warning log-only line (no Error line) and the stream gets a **null profile** (`profileHash(…=none)`). Any other exception writes `INSTR ds(n) unexpected …` at Error and rethrows.
- **Session refusals on the first bar** (`sessions:none`, `sessions:bar-outside`, `sessions:no-rth-split`, `sessions:window-not-covered`) are log-only and **keep the StartCalc profile** (IP `FirstBar`, L940–951): the INSTR line still prints, with the StartCalc sessions (`sessions(n=0)` when none were read) and a hash. Only Build refusals and failed typed reads give a null profile.
- From **PR-M4** every refusal is enforced (Error + throw) for engines that use the field (§11).

### 6.3 Exception format
`M-INSTR: <ENGINE> cannot run on <name> (class <c>, root <r>, sessions <sessionName>): <reason>. <action>.` Reason codes (`InstrumentReason`, IP L46–56 @5c9ccd2): `class`, `unknown-field:<f>` (`root`, `config`, `cost`), `tick`, `mismatch:<f>` (`tick`, `bigPointValue`, `mcPriceScale`, `mcBigPointValue`, `tickValue`), `volume-basis`, `currency`, `sessions:none`, `sessions:changed`, `sessions:bar-outside`, `sessions:no-rth-split`, `sessions:window-not-covered`, `calendar`, **`months`** (rev 2.4: `ContractFor` finds no listed contract month within 24 months, action "check the preset's contract months", IP `ContractFor` L519–530; tested IT T4 L241–242), and from PR-M4 (D2) **`tz:bar`** (the bar time zone cannot be resolved) and **`tz:exchange`** (no exchange zone for `Info.Exchange`, or the zone id does not resolve). In M3 only `mismatch:*` and `sessions:changed` are enforced (§6.2).

### 6.4 R13 applied: every throw, degraded mode and undefined output

**Throw rules**
| # | Condition | Engines | Action text |
|---|---|---|---|
| S1 | `Bars.Sessions` null/empty at StartCalc or on the first bar; or StartCalc and first-bar reads differ; or a bar outside every session | every session-using engine (TOD, AVWAP, VPFUT, FLOW, RANGE, WEEKLY, DAILY, DVP, G0, REGGATE, ROLL keying) | "check the chart's session template" |
| S2 | An engine whose purpose needs an overnight segment, on a chart with `!hasOvernight` | none on main (every overnight-specific item is an *output*, see U1–U2) | — |
| S3 | A frozen pre-registered window (G0 09:30–16:00 ET, `_Gate0` L474/L614/L1037…; REGGATE 09:30–16:00 ET, `_RegimeGate` L45–48) is not inside the chart's sessions (converted to ET) | G0, REGGATE | "use a session template that covers 09:30–16:00 ET" |
| S4 | An engine needs a holiday or early close **in advance** and the root has no verified calendar (rev 2.4: `NyseStandIn` for CL/GC/ZN/6E is a stand-in, not verified; it serves the expiry/roll business-day rules only. A preset with roll/expiry rules and no calendar is refused `calendar` at Build, R6; log-only in M3, enforced in M4) | REGGATE (early-close exit), G0 (early flat), VPFUT `HaltEt`/`RthCloseEt`/`IsGlobexClosedDay` hooks (L1635–1671), `_CmeSessionClock` holiday use | "add a verified calendar for <root> (PR-M8)" |
| S5 | A required preset field is Unknown (`roll`, `contractMonths`, `expiry`, `cost`) | ROLL Table/SeriesWindow, EVENT expiry kinds, DVP roll table, COST/cost floor | "add a preset row / cost for <root>" |
| S6 | Class Unsupported in the rev-3 matrix | per matrix | rev-3 reason text |
| S7 | An engine needs `rthSplit` and it is null (24-hour chart, root not ES/MES/NQ/MNQ; N1) | VPFUT RTH profile/value window/IB, AVWAP `rth`, WEEKLY `RthSessions`, DVP RTH features | "use a day-session template for <root>" |
| S8 | tick ≤ 0; detected ≠ preset; futures on Tick Count (R7); non-USD (Q8) | all | — |

**Approved degraded modes** (only these; each from a DECIDED answer): D1 VP on FX/CFD/crypto as visual/level only, nothing feeds Holm (rev-3 Q5); D2 G0 on Index price-only parts (rev-3 matrix note 6; G0 is ES-canary only in practice); D3 EVENT on USD FX pairs, US events as a filter (rev-3 Q11); D4 FLOW on crypto, single venue, Warning (rev-3 reason 12); D5 PriceAction on Bond/MF/Unknown (rev-3 reason 2); D6 FLOW bid/ask features undefined before a root's `dataNotes` bid/ask start date (Q7, accepted 2026-11-02); D7 TOD on a root without a verified calendar: no current-day half-day flag, observed short days excluded from baselines, one Info line (N4 DECIDED). Rev 1's "Degrade" on RTH-only charts, on unknown early closes and on unconfirmed RTH windows is **withdrawn**. A new degraded mode needs Ettienne's approval.

**Undefined outputs (NaN / false, one Info line, not a degrade)**
| # | Output | When |
|---|---|---|
| U1 | VPFUT overnight stats `ethHigh/ethLow/ethVolume` (L378) | `!hasOvernight` (N3 DECIDED) |
| U2 | VPFUT `IsAsia()/IsEurope()` helpers (L244–245) | `!hasOvernight` (N3 DECIDED) |
| U3 | DVP `rthShare` | `rthSplit == null` (already NaN on the generic base) |

**Post-hoc calendar use without a verified table (correct, not degraded):** `tradingDayCount` (L3228–3240) counts observed trading days (days that have bars) instead of NYSE days for roots without a table; `calendar = Observed`.

---

## 7. Per-engine migration

| Engine | Change | ES result (CME Globex template) | PR |
|---|---|---|---|
| CS host | L2462 typed; `autoTradingDay` and L1888–1889 → `tradingDayKey`; tick fallback → throw; `_NyseHolidays` for stocks, `p.calendar` otherwise; exchange map kept for ET conversions | identical | M1b, M4 |
| CLOCK / VPFUT | Day key, day open, Globex open/close come from session data; RTH split per N1 (ES: today's constants, `rthSource=EquityIndexLegacy`); value window (09:30, 16:00] unchanged for ES; Asia/EU/NY windows per N5; holiday hooks per S4 | identical | M4 |
| CAL | ES keeps `_CmeHolidays` (both `UseCmeHolidayTable` flags, VPFUT L1627 and clock L81); NYSE for stocks; other roots `Observed` for post-hoc use and S4 throws for in-advance use until a verified table lands | identical | M8 |
| ROLL | `_RollDetector` takes months/expiry/roll from the profile; F2; `gapTicks` stays 8 (Q4) | identical | M5 |
| EVENT | `RollDate`/`ExpiryDate`/`SessionsToExpiry` from `p.expiry/p.roll/p.contractMonths`; event classes per asset class | identical | M5 |
| G0 | Remove `tick = 0.25` (L357; throw if 0) **and update all 35 `new Gate0Engine` constructions in `tests/Gate0Tests/Program.cs` to set `tick = 0.25`** in the same PR (none sets it today); `"ES"` (L1204) → `p.root`; S3 coverage check; windows frozen | canary hash identical | M6a |
| FLOW | Tick-chunk boundary = `p.sessionEnd` (CME 16:00 CT = 17:00 ET, today's value); spot FX stays Unsupported (rev-3 reason 11); FX futures get their own session end | identical | M6b |
| DAILY / REGIME | Annualisation reads `p.sessionsPerYear` (252) | identical | M6c |
| LEVEL / AVWAP | Remove the 0.25 defaults (`_LevelStudy` L79, `_AvwapManager` L153); both hosts set them (L1410, L1976); AvwapTests already sets 0.25 (L17); `_AvwapManager` L195 already rejects ≤ 0 | identical | M6d |
| RANGE / WEEKLY / PSTATS | `ringBars` **literals kept** (`_RangeProfile` L31 2880, `_ProfileStats` L47 1440; bar counts, not minutes; no derivation); WEEKLY `RthSessions` needs `rthSplit` (S7), else the user picks GlobexSessions; `_ProfileStats` 4-tick note in CFG | identical | M6e |
| PORT | `PortfolioCluster` returns `p.cluster`, same strings | identical | M6f |
| DVP | root from `p.root` (F1 done in M1a); roll table from the profile replaces `root == "ES"` (L213/230) and host loops (L119–121) | identical (post-#88 baseline) | M6g |
| REGGATE / `_Test6` | ES only (N2); windows frozen + S3; `tradingDay` hash text from an explicit `switch (ETradingDayKey) { case Globex18ET: return "Globex18ET"; … }` and `RollDetection` via an explicit switch with the same spellings (L62, `_Test6` L105–106); roll table from the profile (`_Test6` L100–102) | hash identical | M6h |
| SIZE / COST | `unitNotional` = entry × `p.bigPointValue`; cost floor = **new opt-in behaviour** (§9) | identical (off by default) | M7 |
| MTF | `SymbolRoot` resolves via §4.3; doc fix (L28) | identical for ES | M1a |
| LEGACY | F3a/F3b only; others a one-line note | — | M1c |

---

## 8. Presets: exchange facts only, no session times (sources read 2026-11-02)

### 8.1 Futures
| Root | Tick | Big point value ($/1.0) | Tick $ | Months | Expiry rule | Roll rule (Q5 DECIDED method) | Calendar | Cluster | Source |
|---|---|---|---|---|---|---|---|---|---|
| ES | 0.25 | 50 | 12.50 | HMUZ | 3rd Fri 09:30 ET SOQ, back to the NYSE business day | Mon before the 3rd Fri (3rd Fri ≥ 2022-04-01), else Thu 8 days before (`_EventCalendar` L299–303) | CmeEquityIndex (`_CmeHolidays`) | EQIDX | CME ES specs; code |
| MES | 0.25 | 5 | 1.25 | HMUZ | as ES | as ES | CmeEquityIndex | EQIDX | CME Micro E-mini FAQ |
| NQ | 0.25 | 20 | 5.00 | HMUZ | as ES | as ES (**VERIFY** against the vendor splice) | CmeEquityIndex | EQIDX | CME Micro FAQ |
| MNQ | 0.25 | 2 | 0.50 | HMUZ | as ES | as NQ | CmeEquityIndex | EQIDX | CME Micro FAQ |
| CL | 0.01 | 1000 | 10.00 | all 12 | 3 business days before the 25th of the prior month (4 if the 25th is not a business day) | `BusinessDaysBeforeExpiry(2)`: 2 business days before LTD (**VERIFY** vs vendor splice) | NyseStandIn (logged; CME calendar PR-M8) | ENERGY | CME CL specs; CME notice chadv25-391 (CLF26 LTD 2025-12-19) |
| GC | 0.10 | 100 | 10.00 | active G J M Q V Z | third-last business day of the contract month | `BeforeFirstNotice(1)`: business day before first notice = last business day of the prior month (**VERIFY**) | NyseStandIn (logged; CME calendar PR-M8) | METALS | CME GC specs; GC fact card |
| ZN | 0.015625 | 1000 | 15.625 | HMUZ | 7 business days before the last business day of the month | `BeforeFirstNotice(1)` (**VERIFY**) | NyseStandIn (logged; CME calendar PR-M8) | RATES | CME ZN specs; Barchart (ZNZ25 LTD 2025-12-19, FND 2025-11-28) |
| 6E | 0.00005 | 125,000 | 6.25 | HMUZ (serials listed) | 2nd business day before the 3rd Wed | `BusinessDaysBeforeExpiry(5)`: 5 business days (about one week) before expiry (**Ettienne 2026-11-04**, #96; replaces "Mon of the expiry week") | NyseStandIn (logged; CME calendar PR-M8) | FX_EUR | CME 6E specs; CME rule 26101.G |

**Notes.** No session, RTH, halt or Globex-time column: those come from the chart (R12). Rev 1's RTH reference windows (CL 09:00–14:30, GC 08:20–13:30, ZN/6E 08:20–15:00 ET) and the ES 15:15–15:30 CT halt question are **dropped** (Q2 moot). `BigPointValue` for 6E is **CONFIRMED** 125000 (`01M6HTKV3SV1Q5QDXBFTCH8D3K.txt`); ZN is still **VERIFY** (not in the 13 runs); the cross-check (§4.3 step 8) refuses a mismatch. "Observed" calendars serve post-hoc use only; in-advance use throws (S4) until PR-M8 adds a sourced table.
**Rev 2.4 (as merged, IP `InstrumentPresets` L215–237 @5c9ccd2).** CL/GC/ZN/6E use `ECalendar.NyseStandIn`: their expiry/roll rules count NYSE business days (`EventRules.IsNyseBusinessDay`, IP `InstrumentCalendars.BusinessDay` L439–446) until PR-M8 adds the CME calendars; the choice is explicit and the INSTR line says so (`calendar(NyseStandIn; calendar: NYSE stand-in (CME calendar pending M8))`, note text IP L437). NYSE and CME holiday schedules are not identical, so a computed date next to a holiday is VERIFY until PR-M8. No weekday fallback (R6). **MI-T4 fixture** `tests/InstrumentTests/fixtures/expiry-worked.csv` (11 rows: CL×4, GC×2, ZN×2, 6E×3): the `expiry` column was checked by the analyst on 2026-11-04 against CME notice chadv25-391, Barchart, CME rule 26101.G and the GC fact card; the `roll` column is **computed**: the preset rule applied by hand (e.g. 6EM24 expiry 2024-06-17, roll 2024-06-10), not vendor roll dates. The vendor-splice comparison stays VERIFY (PR-M5/M9).

### 8.2 Rule kinds (typed enums)
`EExpiryRule { ThirdFridaySoq, CL25thMinus3Bd, ThirdLastBd, LastBdMinus7Bd, ThirdWedMinus2Bd }`, `ERollRule { EquityIndexCustomary, BusinessDaysBeforeExpiry(n), BeforeFirstNotice(n), None }` (n is the row's `rollDays`; the INSTR line prints `roll(BusinessDaysBeforeExpiry(5))`). `ECalendar { CmeEquityIndex, Nyse, NyseStandIn, Observed, None }`: `CmeEquityIndex`, `Nyse` and `NyseStandIn` give a business-day function, `Observed` and `None` give none, so a rule needing one refuses `calendar` (R6). ES/MES/NQ/MNQ use `ThirdFridaySoq` + `EquityIndexCustomary` = today's `EventRules.RuleExpiryDate`/`RollDate`.

### 8.3 Non-futures
| Class | Trading day | Calendar | Cost | Notes |
|---|---|---|---|---|
| Spot FX (12) | from the chart's sessions (a 17:00 ET-ending template gives today's ET+7h day) | Observed | Unknown (not costed until traded) | TickCount volume allowed (R7); DVP, G0, FLOW Unsupported (rev 3) |
| Crypto (20) | from the chart's sessions | Observed | Unknown | `sessionsPerYear` 365 |
| Stock (2) | from the chart's sessions | Nyse (`_NyseHolidays`) | Unknown | FLOW and VPFUT Unsupported (rev 3). SPY (**CONFIRMED** `01M6HSDC62GCMK5ZANKB61PD5T.txt`): `Stock`, ARCA, Build Volume On = TickCount (tagged `TickCount`, R7, no throw), Default session Mon–Fri 04:15–20:00 Exchange time. That template is day-only, so by N1 its `rthSplit` is the whole 04:15–20:00 chart session, extended hours included; an RTH-only study needs a 09:30–16:00 template on the chart. |

---

## 9. Sizing and costs (R8, R9): as the code is on `7dbdb1b`
1. q = 1% risk on the structural stop (CS-4 `sizeForRisk`, L2921), then `maxQty` (L2943) and the notional cap (L2945–2949) **reduce** q (Info when they bind). q never grows.
2. `willArm = m_portOn && bars == Bars` (L2957).
   - **Not willArm** (M-PORT off, or Data2..N): if q × stop × bigPointValue > 1% × equity the entry is **skipped**, Info "trade skipped" (L2958–2968).
   - **willArm** (portfolio Data1): the order goes to `portfolioArm` (L2970) and `_PortMM` applies deny-not-reduce. M-INSTR changes nothing here.
3. **Cost floor = new opt-in behaviour (PR-M7)**: `InstrumentConfig.applyCostFloor = false` by default (no input). When on, the stop is widened to `max(structural, costFloorTicks × tick)` before step 2, so the existing skip/deny paths act on the widened stop. Rev-1's claim that main already does this is withdrawn (`costFloorTicks` exists only in comments, `_Gate0` L1185, `_TestGate0` L145).
4. `costFloorTicks` in integer cents: `tickValueCents = tick × bigPointValue × 100`, which must be within 1e-6 of an integer (else throw `mismatch:tickValue`); `costFloorTicks = ceil(roundTripCents × 10 / tickValueCents)` in `long` arithmetic, `(a + b − 1) / b`. ES: 1700 × 10 / 1250 → 14. MES: 250 × 10 / 125 = 20 exactly (no float path can give 21).
4a. **As merged (M2/M3, IP `CostModel.FloorTicks` L379–389, Build L778–779 @5c9ccd2).** The floor is computed **only when a preset matched and the config is valid**, using the preset's tick and bigPointValue; otherwise `costFloorTicks = -1` (`floor(Unknown)`). A known cost with a negative cents value refuses `unknown-field:cost`; the non-integer tickValue is `mismatch:tickValue` (enforced in M3). Rationale: with M3's root-only keys (§5.3), a stock ticker `ES` must not get a floor from the ES future's cost. **PR-M4 (D1 DECIDED):** costs are keyed by class + root (§5.3), so the cost itself can no longer cross classes; the preset gate stays. PR-M4 also prints why the floor is Unknown: `floor(Unknown src=no-preset|no-cost|cost-class-mismatch|config-invalid)` (§6.1); `cost-class-mismatch` = a row exists for the root under another class and was not used.
5. Costs: `p.cost` from `InstrumentConfig.costs` (cents), keyed by **asset class + root** from PR-M4 (§5.3, D1 DECIDED 2026-11-04; M3 keys by root only). DECIDED: `Future:ES` 1700, `Future:MES` 250. Other roots: the method is DECIDED (Q3, IBKR fee per side ×2 + one tick slippage per side ×2), but the per-root IBKR fees are a **data input from Ettienne**; until given, cost = Unknown and cost users throw (S5).

---

## 10. Tests (MC-free; new `tests/InstrumentTests` in `PLStudies.Tests.sln`, `CheckForOverflowUnderflow=true`, real sources linked via the `gen.sh` region pattern)

| ID | Test |
|---|---|
| MI-T1 | (Not a test.) `ClassOf` is the exhaustive host switch of §4.2, checked by the PLEditor compile in PR-M3. The MC-free core is tested on `EAssetClass` only. **Result 2026-11-04 (Ettienne, `5c9ccd2`): Compile All passed** after the one-time import of `_InstrumentProfile.Function.CS`; `eMTPA_SF_Root`, `eMTPA_MCSC_USD` compile. |
| MI-T2 | Root resolver (§4.3): @ES, @ES#C, ESZ24, ESZ4, **ESZ2024 → ES, 'ES 12-24' → ES, ESZ24:XCME → ES, CLZ2026 → CL, @6E → 6E, 6EZ26 → 6E, EUR/USD → EURUSD, BRK.B → BRK (M1a; class-aware stock handling in the M2 resolver keeps BRK.B), Z4 → Z**, MESZ26 → MES, MESH27 → MES, **6E vs 6B differ**, @MNQ, GCQ26, ZNH27, **EUR/USD vs EUR/JPY differ**, BTCUSD, AAPL; M2: `/` removed for Spot FX only; M2 root source: MC root primary, parser fallback only on `(UnsupportedFields & eMTPA_SF_Root) != 0` (incl. -1) or empty root, `!SameRoot` → Warning + MC wins; **`_RootDump` fixtures (§4.4.3):** (@ES, ES, 0) → ES/mc, (@CL, CL, 0) → CL/mc, (@6E, 6E, 0) → 6E/mc, (SPY, SPY, 0) → SPY/mc, (@6E, "", 0) → 6E/parsed, (@ES, ES, 1) → ES/parsed, (@ES, ES, -1) → ES/parsed, (@ES, ES, 512) → ES/mc; SymbolExpiry 2038-01-01 / 1900-01-01 → placeholder (calendar), 2026-12-18 → used; "ES" as a stock gets no preset; empty root rejected; `RootError("", "")` is an error (F1). |
| MI-T3 | Preset cross-check: tick/bigPointValue mismatch throws `mismatch:tick` / `mismatch:bigPointValue`. MC-internal cross-check on the four `_RootDump` rows (§4.4.3) passes: 0.01 × 100, 1E-05 × 99999.999999999985 (within 1e-9), BPV 50/1000/125000/1 equal; perturbed rows (struct PriceScale 0.1 vs 100; BPV 50 vs 5) throw `mismatch:mcPriceScale` / `mismatch:mcBigPointValue` with the message naming both values. |
| MI-T4 | Expiry rules vs CME calendar dates per root 2016–2027 (fixture CSV with the source URL); ES rows = `EventRules.RuleExpiryDate`. **As merged (IT T4 L196–242):** `fixtures/expiry-worked.csv` (expiry analyst-checked, roll computed from the preset rule, §8.1); NyseStandIn on CL/GC/ZN/6E; 6E `BusinessDaysBeforeExpiry(5)` (L219); no weekday fallback (an Observed/None calendar refuses `calendar`, L228); `months` refusal (L241–242). |
| MI-T5 | Roll rules: the ES set reproduces `RollDatesDigest2000To2035 = "fdd63bd8f179a8d4"` (`_RegimeGate` L60). |
| MI-T6 | `SeriesWindow` generalised: `tests/RollTests` unchanged; CL/GC monthly windows. |
| MI-T7 | Trading day from sessions: CME Globex template (Sun 17:00 → Mon 16:00 CT, and the ET equivalent) gives `GlobexTradingDayEt` for every 1-min close over 2016–2024 incl. DST weeks; a two-session day (GLOBEX 17:00–15:15 + 15:30–16:30, the 9-entry list of `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt`) shares one key; FX 17:00 ET template = ET+7h; 00:00 UTC crypto = UTC date; a day-only template keys by calendar date; a bar outside every session throws. |
| MI-T8 | Derived fields: `dayOpen`, `sessionEnd`, `hasOvernight`, `rthSplit` (day-only template → chart session; Globex template + ES → `EquityIndexLegacy` equal to `_CmeSessionClock` defaults field by field; other 24 h → null). |
| MI-T9 | Cost floor in integer cents: ES 14, **MES 20**, a non-integer tickValueCents throws. |
| MI-T10 | Sizing: caps reduce q; the off-portfolio skip logs Info; floor off by default changes nothing; q never grows. |
| MI-T11 | Exception text per reason code (§6.3), incl. `sessions:*`. (The T11 list, IT L440, does not include `months`; `months` is tested in T4.) |
| MI-T12 | INSTR/CFG line formats pinned; culture-invariant (run under de-DE). **As merged:** full ES INSTR line (IT L537–539) and RUN suffix format (L544) pinned (§6.1); M3 ordering, enforced/log-only and Guard tests IT L596–630. |
| MI-T13 | `profileHash` stable and outside every `configHash`. **As merged (IT L446–476):** ES `020deca78d6392b7`, 6E `8e8b3e9b0e230e12` (L472–473), full 6E line (L475). MC (2026-11-04): real 6E chart `55576df6a8a99c05`, reproduced byte-for-byte on .NET 8 (runtime-stable). **PR-M4 (D2):** add the tz-invariance tests and the expected pins of §6.1 / §11.1 item 9. `MonthMask.Of` bad-letter test L631–640. |
| MI-T14 | Lint (in `tests/RepoLintTests`, PR-M1d) **scoped to MC enum types** (`ESymbolCategory`, `RequestTimeZone`, `EResolution`, `BuildVolumeOn`, `MTPA_MCSymbolCurrency`, `MTPA_MCSymbolCategories`, `MTPA_SymbolFields`, `DayOfWeek` from `SessionObject`): fails on `.ToString()` of those values feeding a comparison or a parse, on `Enum.Parse`/`TryParse` with those types, and on `System.Reflection` / `GetProperty(`. Exact allow-list (file + line pattern): `_CustomSignal` inputSnapshot reflection (L2254–2273), `_MarketBreadth.Strategy.CS` L7, `barZoneLabel` L279 (log), the L1883 `SeriesKeyWarning` text (log), `_TestPortMM` L90 (log). Repo-enum parses (`_EconomicCalendar` L690, `_StructuralTargetPatterns.Strategy` L305 / `.Indicator` L143, `_EventCalendar` L1307/L1318/L1322, `_PortMM` L233/L245) are out of scope. Runs after PR-M1b (L2462) and PR-M1e (probe) so it passes on that main. |
| MI-T15 | ES bit-identity per PR: `_TestGate0` canary hash and the DVP D9a summary (**post-#88 baseline**) unchanged; ES fixtures in `tests/*` unchanged; requires **Ettienne's MC runs for every PR**, numbers pasted into the PR. |
| MI-T16 | Session ends from the list (R14): the 9-entry GLOBEX list of `01M6HSDBFRQ9ZZFA4NBXZ8QZXC.txt` with 1-min bars and `LastBarInSession` false at 15:15 (true only at 16:00, as dumped) gives session-end events at **15:15 and 16:00** each weekday and trading-day rollover after 16:00; a duplicate CalcBar at the same 16:00 time (`01M6HSDBFH2D6H21PMYXYKFJHV.txt`) resets once; a session-reset engine fixture (VPFUT/AVWAP/FLOW reset hook) sees exactly two resets per GLOBEX day and one per Default day; a list with no bar at its `EndTime` (data stops early) ends the session on the next out-of-session bar or on `LastBarInSession`. |

**State on `5c9ccd2`:** `tests/InstrumentTests/run.sh` → `InstrumentTests: 668 passed, 0 failed` (box, .NET 8, 2026-11-04 ET).

---

## 11. PR list (each small; each targets `main`, opened after the previous one merged; no stacked PRs)

**Status on `5c9ccd2` (rev 2.4):** M0 **#89** merged, M1a **#90** merged, M1b **#91** merged, M1c **#92** merged, M1e **#93** merged, M1d **#95** merged, M2 **#96** merged, M3 **#97** merged. **Extra, outside this list:** **#94** `_ExitHalfRiskHalfRoll` exit-only (half off at +1R, trail the rest; exit-only invariant; `OrderExit.Total`), merged (`ed3e4a5`); it changes the legacy study only. Next: **PR-M4**.


| # | PR | Content | Behaviour change | Tests |
|---|---|---|---|---|
| — | **#88** | Land #86 + #87 (existing) | — | — |
| 0 | PR-M0 **#89 merged** | `_InstrDump.Strategy.CS` (§4.4, derives from `_CustomSignal`) + the `LogDirectory()` accessor in `_CustomSignal` (§4.4.1) + csproj entry + import note; `MCInstrDump_*.txt` output committed as fixtures after Ettienne's runs | none | existing suites |
| 1 | PR-M1a **#90 merged** | F1: `_MtfMath.SymbolRoot` (§4.3 steps 1–2: leading digits + letter run, month drop, "/" removed; L109–119 on `6853398` before #90, L120–135 after #90), doc L28, `RootError` empty = error, roll inheritance and same-root checks | digit-led roots / FX only | MI-T2 (subset) in existing MtfMath/Dvp tests |
| 2 | PR-M1b **#91 merged** | F2 (`isFuturesCategory` at L1069/L1866/L1882), F4 (tick throw), L2462 typed | FutureRolover, bad tick | existing suites |
| 3 | PR-M1c **#92 merged** | F3a + F3b in `_ExitHalfRiskHalfRoll` only | legacy study only | — |
| 4 | PR-M1e **#93 merged** | Retire `_InstrProbe` + `_InstrProbeCore` + `tests/InstrProbeTests` + csproj L521–522 + sln entry | none | suite count 23 → 22 |
| 5 | PR-M1d **#95 merged** | MI-T14 lint + allow-list | none | MI-T14 |
| 6 | PR-M2 **#96 merged** | `_InstrumentProfile.Function.CS` core (enums, resolver, presets, `InstrumentConfig`, `CostModel`, exceptions, session derivations on structs), `<Compile Include>` + import order, `tests/InstrumentTests` + sln, rev-3 "superseded for C1–C9" banner (Q10) | none (not wired) | MI-T2–T9, T11, T13, T16 (core) |
| 7 | PR-M3 **#97 merged** | Wire `buildProfiles()` + `SessionReader` (StartCalc read + first-CalcBar re-check), `ClassOf` switch, INSTR line, cross-checks, `profileHash` on RUN | log only (+ `mismatch:*` and `sessions:changed` throw, §6.2) | MI-T12, T13, T15 (MC run pending) |
| 8 | PR-M4 | Trading day/day open/session end from sessions; remove L1888–1889, `autoTradingDay`, VPFUT Globex key/TOD 18:00; S1/S3/S7 rules; RTH split per N1; exchange map for ET only (NYMEX/COMEX unchanged, A3); session ends per R14 in the reader and every session-reset engine. PR-M0 output in §4.4.2. **Plus the rev 2.4 scope list below (§11.1)** | non-Globex templates and non-ES only; refusals become throws | MI-T7, T8, T15, **T16** |
| 9 | PR-M5 | Expiry/roll from the profile: `_RollDetector`, `EventRules`, host quarterly loops (`_DvpEngineTest` L119–121, `_Test6` L100–102) | non-ES only | MI-T4–T6, T15 |
| 10 | PR-M6a | G0: tick default removed + **Gate0Tests 35 constructions set tick**; root; S3 | none for ES | Gate0Tests, T15 |
| 11 | PR-M6b | FLOW: chunk boundary = `sessionEnd` | non-ES only | OrderFlowTests |
| 12 | PR-M6c | DAILY/REGIME `sessionsPerYear` | non-futures only | |
| 13 | PR-M6d | LEVEL/AVWAP tick defaults removed | none | AvwapTests, LevelTests |
| 14 | PR-M6e | RANGE/WEEKLY/PSTATS: S7 for `RthSessions`; CFG note; ringBars literal | none for ES | |
| 15 | PR-M6f | PORT cluster from profile | none | PortMM tests |
| 16 | PR-M6g | DVP roll table from profile | none for ES | DvpTests |
| 17 | PR-M6h | REGGATE: ES only (N2), explicit hash switches, S3 | hash identical | RegimeGate tests, T15 |
| 18 | PR-M7 | Cost floor (opt-in, new) + costs in cents | none unless enabled | MI-T9, T10 |
| 19 | PR-M8 | Calendars for other roots, only when sourced (ES table unchanged) | non-ES only | calendar fixtures |
| later | PR-M9 | Self-calibrating roll gap (Q4); ES stays 8 until it reproduces `tests/RollTests` and the m-event splices; `tools/m-event/out/roll_splices_2016_2024.csv` is read-only | none for ES | RollTests |

### 11.1 PR-M4 scope (rev 2.4, explicit)
1. **Log-only → enforced.** Every refusal of §6.3 (incl. `class`, `unknown-field:*`, `tick`, `currency`, `calendar`, `months`, `sessions:*`) becomes Error + throw for engines that use the field; the `INSTR ds(n) log-only…` line and the null-profile path go away for those cases. The `calendar` refusal for a preset/override with rules and no calendar (R6) is enforced; consider also rejecting it in `InstrumentPresetRow.Validate` so an override cannot reach it.
2. **ClassOf:** an unmapped `ESymbolCategory` in `buildProfiles()` (today a catch-all Warning, CS L2411–2416) becomes Error + throw, like `classOfData1` (CS L2708–2723).
3. **FALLBACK_TICK audit (R6):** remove `FALLBACK_TICK = 0.01` in `_VolumeProfileBase.Strategy.CS` (L48, used L2418 `zoneClearsPrice`) and its use in `_VpConfluenceEngine.Function.CS` (L1156 `confluenceTolerance`); take the tick from the profile or throw.
4. **`tickFromMinMove` audit** (CS L2527–2532): the `PriceScale == 0 → MinMove` path and `Math.Abs` of a negative ratio are silent fallbacks; replace with the profile tick or a `tick` refusal. Today the host tick (engines) and the profile tick (refuses PriceScale ≤ 0) can disagree on such a symbol.
5. **Floor-Unknown reason** printed on INSTR (`floor(Unknown src=no-preset|no-cost|cost-class-mismatch|config-invalid)`, §6.1, §9).
6. **Portfolio Trader test, before M4 starts: DONE 2026-11-04** (Ettienne, `5c9ccd2`): one PT log for @6E, @ES, AAPL, each with its own root/tick/bpv/sessions/calendar (§4.4.2).
7. **`eMTPA_SF_Root`: DONE 2026-11-04**: MI-T1 compile passed, `rootSrc=mc` on 6E/ES/AAPL (§4.1); the MC 6E hash `55576df6a8a99c05` reproduces on .NET 8 (`8e8b3e9b0e230e12` is the synthetic-list pin).
8. **Cost keying by asset class + root (D1 DECIDED, Ettienne 2026-11-04):** `CostKey(EAssetClass, root)` = `<Class>:<ROOT>`; defaults `Future:ES` 1700, `Future:MES` 250; `CostFor(class, root)`; `Validate` rejects root-only and unknown-class keys (`unknown-field:config`); a same-root row of another class is not used (`cost(Unknown)`, `floor(Unknown src=cost-class-mismatch)`, one Warning). Tests: Future ES keeps `cost(17.00 …)`, `floor(14)` and hash `020deca78d6392b7`; stock `ES` gets `cost(Unknown)` + the mismatch reason (replaces IT L609–611); root-only key refused; `InstrumentConfig.Canon()` shows the new keys; no configHash changes.
9. **profileHash in exchange time (D2 DECIDED, Ettienne 2026-11-04; §6.1):** `Canon` hashes the sessions and the rth recomputed from them in the exchange zone (`exchangeTimeZoneId(Info.Exchange)`, strict lookup, no `findTz`/Local fallback); refusals `tz:bar` / `tz:exchange`; INSTR adds `xsessions(…)`; profile fields used by engines stay in bar time. Tests: the same 6E list given as Exchange CT (17:00→16:00) and Local ET (18:00→17:00) hashes equal, `55576df6a8a99c05`; ES Default `6cff053d1b215324` on either zone; unknown exchange → `tz:exchange`; unresolvable bar zone → `tz:bar`; existing pins kept only where the test's bar zone is the exchange zone. MC: rerun @6E and @ES on a chart (Exchange) and in PT (Local) and paste equal hashes; one GMT chart (VERIFY, §6.1).
10. **PORT publish line timestamp (cosmetic, observed 2026-11-04):** `PORT publish on: …` (CS L1656) is traced before any bar, so the trace stamps `0001-01-01` and zone `?` (`trace` off-calc path L395: `LastCalcBarTime` unset, `barZoneLabel` L276–279 throws). Stamp it with a clear "no bar yet" marker or write it after the first bar; no behaviour change.

Every PR lists the R1–R14 rules it touches, the VERIFY items it closes, its csproj/sln changes and the PowerLanguage Editor imports, and re-reads its line cites on current main.

---

## 12. Questions

### 12.1 DECIDED
- **Q1 / Q2 Sessions and RTH windows: DECIDED by Ettienne 2026-11-02.** Verbatim: *"The trading hours in questions 1 and 2 should be driven from Multicharts - it has session information you can get and use. This info is setup from the chart and just work with that. Each instrument also has a 'Default' setting which should be correct and used the traders in general."* Applied as R12 and §4.5. Q2 (RTH windows for CL/GC/ZN/6E) is moot: no RTH windows in presets.
- **Q3 Costs for other roots: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): IBKR fees + one tick slippage, as ES/MES. **Data input outstanding:** IBKR per-side fees for NQ, MNQ, CL, GC, ZN, 6E; until given those roots have cost Unknown.
- **Q4 Roll-gap threshold: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): self-calibrating in PR-M9, only after it reproduces RollTests and the m-event splices; ES keeps 8 until then.
- **Q5 Roll rules for CL/GC/ZN/6E: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): rule-based table per §8.1, confirmed against the vendor's continuous splice; series detection fallback.
- **Q6 Retire `_InstrProbe`: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): PR-M1e.
- **Q7 Data-availability notes: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): `dataNotes`, logged on INSTR; FLOW bid/ask features undefined before the date (approved degraded mode D6, §6.4). **Data input outstanding:** dxFeed tick bid/ask start dates for @ES and @MES.
- **Q8 Non-USD: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): USD only; non-USD throws `currency`.
- **Q9 Stop floor: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): cost-derived, integer cents (§9.4), opt-in (PR-M7).
- **Q10 Supersede rev 3: DECIDED** (Ettienne 2026-11-02 accepted the recommendation): banner in PR-M2.

### 12.2 N1–N6: DECIDED (Ettienne 2026-11-03 09:47 ET: *"1 to 5 is fine, on 6 keep that dump in the same directory as the created standard log file"*)
- **N1 RTH split on a 24-hour chart: DECIDED as recommended (B).** ES/MES/NQ/MNQ on a 24-hour chart keep today's equity-index split in code (`rth src=EquityIndexLegacy`, ES bit-identical); other roots on a 24-hour chart throw S7 for RTH-dependent engines; on a day-only chart the chart session is the RTH. Option A (a second RTH-template stream) is not used because of MC-1439 stalls under Realtime-History Matching.
- **N2 A2-63 on MES: DECIDED as recommended.** REGGATE accepts ES only; MES would be a new trial with its own pre-registration.
- **N3 Overnight-only outputs without an overnight segment: DECIDED as recommended.** NaN/false with one Info line (U1–U2), not a throw.
- **N4 TOD half-day flag without a verified calendar: DECIDED as recommended.** Approved degraded mode D7 (§6.4).
- **N5 Asia/Europe/NY clock windows: DECIDED as recommended.** Kept as instrument-independent code defaults in ET (VPFUT L52–63, `_CmeSessionClock` L48–61), clipped to the chart's sessions; not presets.
- **N6 `_InstrDump` output: DECIDED.** Same directory as the standard MCSignal log, via the host's resolver (`_CustomSignal` `LogFileSingleton` L71–91, exposed through a new `LogDirectory()` accessor); file `MCInstrDump_<symbol>_<yyyyMMdd-HHmmss>-<pid>.txt` (§4.4.1).

### 12.3 Data inputs still pending (do not block PR-M0 to PR-M3)
- Q3: IBKR per-side fees for NQ, MNQ, CL, GC, ZN, 6E (until given: cost Unknown, S5).
- Q7: dxFeed tick bid/ask start dates for @ES and @MES.
- PR-M0 OPEN items (§4.4.2): runs not yet made: @ZN, one FX pair, one crypto symbol. The Portfolio Trader per-symbol item is **CLOSED** (PT test passed 2026-11-04, §4.4.2, §11.1 item 6).
- **MI-T1 / MI-T15 (Ettienne): DONE 2026-11-04 on `5c9ccd2`.** Compile All passed (one-time import of `_InstrumentProfile.Function.CS`; `eMTPA_SF_Root`, `eMTPA_MCSC_USD` compile). 6E chart (Exchange tz, Default, BVO TickCount): `tick(5E-05) bpv(125000) tickValue(6.25)`, NyseStandIn note, `roll(BusinessDaysBeforeExpiry(5))`, profileHash `55576df6a8a99c05`, reproduced byte-for-byte on .NET 8; the pins `020deca78d6392b7` / `8e8b3e9b0e230e12` are for the synthetic 9-session list and are not expected in MC.

### 12.4 Decisions (rev 2.4)
- **D1 Cost keying: DECIDED by Ettienne, 2026-11-04: key costs by asset class + root** (e.g. `Future:ES`), so a configured futures cost never applies to a stock with the same root. Rule, key format, default mapping (`Future:ES` 1700, `Future:MES` 250), class-mismatch handling and the configHash check: §5.3; floor reasons: §9 4a; implementation: PR-M4 (§11.1 item 8). M3 (`5c9ccd2`) still keys by root only (IP L398, `CostFor` L426–430); that is the as-merged behaviour until PR-M4. Options considered: (a) keep root-only plus the preset gate; (b) class + root (recommended, chosen).
- **D2 profileHash time zone: DECIDED by Ettienne, 2026-11-04: compute profileHash from the sessions expressed in exchange time**, so the same instrument hashes the same whatever the chart / Portfolio Trader time zone. Evidence: @6E `55576df6a8a99c05` (chart, Exchange tz, Sun 17:00→Mon 16:00 … Central) vs `9523b25025f9ed5b` (PT, Local, 18:00→17:00 ET); both reproduced on the box from the canon. Zone source, refusals (`tz:bar`, `tz:exchange`), conversion, INSTR `xsessions(…)`, rth handling and expected pins: §6.1; implementation and tests: PR-M4 (§11.1 item 9). M3 (`5c9ccd2`) hashes bar-time sessions.
- **Accepted, recorded:** `MonthMask.Of` throws `ArgumentException` on a bad month letter **without** an Error line (exception to "Error before every throw"). It is called only on static preset data, the doc comment says so, and IT L631–640 tests it; a bad letter fails every test run before any MC run. No change.

### 12.5 Code vs spec divergences found for rev 2.4 (on `5c9ccd2`)
| # | Where | Spec said (rev 2.3) | Code does | Rev 2.4 |
|---|---|---|---|---|
| V1 | §5.1 `calendar` | `{CmeEquityIndex, Nyse, Observed, None}` | adds `NyseStandIn` (IP L33) | spec updated |
| V2 | §8.1 CL/GC/ZN/6E | calendar Observed; 6E roll "Mon of the expiry week" | NyseStandIn; 6E `BusinessDaysBeforeExpiry(5)` | spec updated (Ettienne's M2 decisions) |
| V3 | §4.3 step 8 | 6E tick `5.0000000000000007e-5` | raw `5.0000000000000009E-05` (R, Framework); profile `5E-05` | spec corrected |
| V4 | §5.1 `dataNotes` | list of (from, what) | one string per root | spec updated |
| V5 | §6.1 example | no `rootSrc`, no `dataNotes`, `sessionEnd(16:00)`, n=1 | `root(ES rootSrc=mc)`, optional `dataNotes(…)`; GLOBEX 9-entry list ends 16:30 | pinned lines added |
| V6 | R13/R6 "Error before every throw" | every refusal Error + throw | M3 log-only refusals write only a Warning (Error buffered and dropped by `Guard`); enforced ones write the Error | documented (§6.2); M4 enforces |
| V7 | R6 | no silent fallbacks | `FALLBACK_TICK` in VP/VpConfluence; `tickFromMinMove` PriceScale==0 → MinMove and `Math.Abs` | M4 scope (§11.1 items 3–4) |
| V8 | §4.2 | unmapped category throws | `buildProfiles` turns it into a log-only Warning (`classOfData1` still throws) | M4 scope (§11.1 item 2) |
| V9 | §5.1/§9 floor | floor from cost | only with a matched preset and a valid config; cost still shown without a preset | documented; floor reason M4 |
| V10 | R6 `calendar` | rules without a calendar throw | Build refusal is log-only in M3; reachable only via an override row (presets all have a calendar; `Validate` does not reject it) | documented; M4 |
| V11 | §6.2 | "null profile" on refusal | a first-bar session refusal keeps the StartCalc profile (INSTR still printed, `sessions(n=0)` when none were read) | documented |
| V12 | MI-T11 | every reason code | T11 list omits `months` (covered in T4) | noted |
| V13 | §4.1 | `eMTPA_SF_Root = 1` stated as fact | mirror-only; not in the PG or a dump | closed 2026-11-04: member compiles (MI-T1); value not relied on |
| V14 | §5.3 costs | class + root keys (D1, 2026-11-04) | root-only keys (`ES`, `MES`); a stock `ES` shows the future's cost | PR-M4 (§11.1 item 8) |
| V15 | §6.1 profileHash | sessions/rth hashed in exchange time (D2, 2026-11-04) | hashed in bar time; @6E `55576df6a8a99c05` (Exchange) vs `9523b25025f9ed5b` (PT Local) | PR-M4 (§11.1 item 9) |


---

## Appendix A. Review responses (Coder review of rev 1, 37 items; each re-checked on `7dbdb1b`)

| # | Item | Response | Where |
|---|---|---|---|
| 1 | Lint fails on main; needs allow-list, MC enums only | Accepted. Scoped to MC enums with an exact allow-list; remaining MC-enum string uses listed as findings | §3.3, MI-T14, PR-M1d |
| 2 | Removing `_InstrProbe` breaks InstrProbeTests | Accepted. Own PR with the suite, csproj L521–522 and sln; M1 does not depend on the M0 dump | PR-M1e |
| 3 | Removing G0 tick default breaks Gate0Tests | Accepted, with a correction: 35 `new Gate0Engine` constructions and **none** sets the engine tick (L168/184/191 are constructions); all 35 updated in PR-M6a | §7, PR-M6a |
| 4 | ringBars not ES-unchanged if derived | Accepted. Literals kept (2880; `_ProfileStats` 1440); they are bar counts | rows 55, 65; §7 |
| 5 | §9.1 vs willArm | Accepted. §9 restates the code; floor is new opt-in | §9 |
| 6 | Throw vs degrade conflicts | Accepted. One rule (R13), every case listed; rev-1 degrades withdrawn | R13, §6.4 |
| 7 | Row 13 cite | Accepted: L1840–1842 | row 13 |
| 8 | Row 23 missing `MultiChartsProfile=0` | Accepted | row 23 |
| 9 | Row 24 value window (09:30,16:00] | Accepted | row 24 |
| 10 | Rows 26/28 VPFUT own table | Accepted | rows 26, 28 |
| 11 | Counts | Accepted. One area per row, totals 70 | §3.1 |
| 12 | L2621 NYMEX/COMEX → Eastern | Accepted as a finding; map kept only for ET conversion; fixed in M4 if dump A3 shows Central | row 4, §4.4 A3, §4.5 |
| 13 | Host quarterly tables | Accepted | row 64, PR-M5 |
| 14 | L1888 Eastern | Accepted; removed by session-derived day key | row 63, §4.5 |
| 15 | `_ProfileStats` 4-tick; NQ $2.50 | Accepted | rows 66, 67 |
| 16 | `SymbolRoot` doc | Accepted | row 68, PR-M1a |
| 17 | F1 wider | Accepted; MI-T2 adds 6E/6B and EUR/USD vs EUR/JPY | §3.2, MI-T2 |
| 18 | F2 also L1069/L1866 | Accepted | §3.2, PR-M1b |
| 19 | F3 description + second bug | Accepted | rows 60, 70; PR-M1c |
| 20 | F4 | Accepted | PR-M1b |
| 21 | `Bars.Sessions` compiles | Accepted; StartCalc population, zone and per-series sessions CONFIRMED by the PR-M0 dump (rev 2.2); read at StartCalc, re-checked on the first bar | §4.1, §4.4 A2–A4, §4.5 |
| 22 | `StrategyCurrencyCode` on plain signal | Accepted; moved to section B | §4.4 B2 |
| 23 | Drop Description/PointValue/Resolution.Size from dump | Accepted | §4.4 A1 |
| 24 | Dump3 hygiene | Accepted; cited as box-only run output; M0 commits typed output | header, §4.4 |
| 25 | Rename to `bigPointValue` | Accepted | §5.1 |
| 26 | Split M1 | Accepted (a, b, c, d + e for the probe) | §11 |
| 27 | Sequential PRs on main | Accepted; #88 first, no stacking | header, §11 |
| 28 | M2 build plumbing | Accepted | PR-M2, §10 header |
| 29 | MI-T1 cannot be a test | Accepted; host exhaustive switch | §4.2, MI-T1 |
| 30 | MI-T15 baseline + MC runs | Accepted; post-#88 baseline, Ettienne's runs per PR | MI-T15 |
| 31 | Banner in M2 | Accepted | PR-M2 |
| 32 | R3 PriceAction preset | Accepted; preset clause dropped, "PriceAction unchanged" kept | R3 |
| 33 | R4 wording | Accepted: "no new [Input]s" | R4 |
| 34 | REGGATE Q8 cross-ref; hash switch | Accepted; cross-ref fixed, MES as new trial raised as N2, explicit switches | §7, N2, PR-M6h |
| 35 | Cost floor exact integers | Accepted; integer cents, MES = 20 test | §9.4, MI-T9 |
| 36 | FLOW boundary for FX | Changed: boundary = the chart's session end (no per-class rule); spot FX is already Unsupported for FLOW (rev-3 reason 11); 6E/FX futures use their own session end | §7, PR-M6b |
| 37 | Q4: ES stays 8; CSV read-only | Accepted | Q4, PR-M9 |

**Rejected outright:** none. **Partly changed:** item 3 (no Gate0 test sets the tick, so all 35 are updated) and item 36 (session end instead of a per-class rule).
