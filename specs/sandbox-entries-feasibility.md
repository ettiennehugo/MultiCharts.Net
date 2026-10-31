# Feasibility: developing every catalogue entry in a sandbox that plugs into _Test1.._Test10

Prepared by Grok Bot (Analyst 2), Thu 22 Oct 2026 (EDT). This is a study only: I changed no code and pushed nothing.
I read the repo at origin/main 6450a9f (PR #20 merged), PRs #13–#20, the catalogue §2.5/§4/§5 and the A2 file.
The local working copy is on `feat/gmm-core` with an untracked `_Gmm.Function.CS`. I left it untouched.

## 1. Verdict
**Feasible, with one structural condition.** Entry rules have to be **plain C# classes in flat `_X*.Function.CS` files**. They return **intents**. The `_TestN` signal translates those intents into orders that were created up front. The pattern already works in the repo:
`_Gate0.Function.CS` (pure engine, no MC types) → `Gate0Intent` → `_TestGate0.Strategy.CS` (thin MC host), tested off-MC in `tests/Gate0Tests`.
Generalising that is low risk.

About **104 of the 123** matrix rows (S = standalone) fit as entry rules:
- About 60–70% of those are clean.
- The rest also need an exit/risk module, an extra data stream or a gate.

The following do **not** fit as entries. They need their own interfaces:
- **L layers (13)**, **K canaries/controls (6)**.
- **EX/A2-72..79 exit-risk modules**, **RG/A2 gates**.
- **PF/M-PORT portfolio items**, **A2-40/59 costs** (base class).
- **L2-01..07** (parked).

## 2. What the repo actually does (facts)
- **Slots.** `_Test1.._Test10.Strategy.CS` are still empty `SignalObject` stubs. The single `m_le = OrderCreator.MarketNextBar(...)` is created in Create() and sent every bar. Catalogue §2.5 says each slot must be re-based onto `_VolumeProfileFuturesBase` and given `Mode`, `Variant`, `ExitModule` (+ v2 `GateModule`, `RiskModule`) inputs.
- **Chain.** `_CustomSignal` (abstract, `PortfolioSignalObject`, 2925 lines) → `_VolumeProfileBase` → `_VolumeProfileFuturesBase` (abstract, 965 lines).
  - `_CustomSignal` covers: journal/trace (ETraceLevel; `MCJournal_<runId>.csv`, `RUN` line with BuildTag + reflected inputs), roll detection, the M-PORT publish hooks, MTF access, daily context, tick stream, XMKT legs and the A2-33 `_LevelStudy`.
  - `_VolumeProfileFuturesBase` adds: sessions/CME clock, the IB, profile window hooks, holidays.
- **Configuration.** Nothing is an `[Input]` except `TraceLevel` (rule D7). Engines are configured through **protected config properties** (`PriceActionConfig`, `ExtraPriceAction`, `DailyConfig`, `TickConfig`, `LevelStudyConfig`, `RollDetection`, `RollGapTicks`, `Fvgs` ...). The derived class sets them, usually in Create(), and the base builds each engine **lazily** with an `engineSink(ETraceLevel.Journal)` journal. Roll resets flow through `OnRoll` / `rollOn(stream)`.
- **_TestGate0 is the template.** It has only 2 inputs (`Mode`, `Variant`). Create() sets `RollDetection`. StartCalc() builds `Gate0Engine{mode, variant, tick, journal}`. CalcBar feeds closed bars and translates the intent. Every parameter is a pre-registered constant, and `Variant` decodes a sweep cell (A2-53 = 100..123).
- **Compilation.**
  - `PLStudies.csproj` (net48) lists 493 `Compile Include` files, all flat in the repo root, named `<Name>.<Function|Indicator|Strategy>.CS`. It references MC's PLTypes/PLStudiesProxy and DLLs under `C:\ProgramData\TS Support\MultiCharts .NET64\StudyServer\Techniques\`.
  - Namespaces are `PowerLanguage.Strategy` / `.Indicator` / `.Function`.
  - Plain helper classes in `*.Function.CS` files already compile and resolve across files: the CS-0 `nameof(_SignalJournal)` probe and the many engines (`_Gate0`, `_LevelStudy`, `_Rng`, `_BarRing`...).
  - Reflection is already used: input dump and tick-bar members in `_CustomSignal`. So reflection itself is not blocked.
  - PR #13 showed that **one compile error breaks every study**: MC compiles all studies into one assembly.
- **Off-MC tests.** 16 net8.0 projects in `tests/` (LangVersion 7.3), plus `PLStudies.Tests.sln`. They `<Compile Include="..\..\_X.Function.CS" Link=...>` the real files. Code that sits inside a study file is pulled out by `gen.sh`. This works only because the engines have **no MC types**.

## 3. MC .NET constraints that drive the design
Items marked (verify) need confirming in the PowerLanguage .NET Editor.

1. **Flat folder, naming.** The editor manages `StudyServer\Techniques\CS\*.CS`, one study per file, as `Name.Type.CS`. The study class name equals the file name, in the matching namespace. I know of no subfolder support (verify). → Keep sandbox files **flat** with a prefix. Do not use a repo subfolder unless a deploy script flattens it.
2. **One assembly.** Everything compiles together, so a broken entry breaks all strategies, live ones included. Compile time grows with file count; it is already 493 files. Mitigations: CI `dotnet build` of a net48/LangVersion 7.3 mirror project, plus the off-MC tests, **before** anything is imported into MC.
3. **C# 7.3 / .NET Framework 4.8.** No default interface methods, no records, no `init`, no switch expressions.
4. **Orders exist only from Create().** `OrderCreator` objects cannot be made later. A slot must pre-create a fixed order set: market/limit/stop × buy/sell/sellshort/buytocover, plus named exits. Rules must not own orders; they emit `EntryIntent`/`ExitIntent`. Order names drive the CS-1 journal arms, so one generic name per order type plus a tag field in the journal is enough.
5. **Inputs are valid only from StartCalc.** Create() cannot know which idea the inputs select. That collides with base engines configured in Create() (verify: is setting `DailyConfig`/`TickConfig`/`ExtraPriceAction`/`LevelStudyConfig`/`RollDetection` in StartCalc() *before* `base.StartCalc()` honoured by the lazy builders?). If it is not, there are two fallbacks:
   - enable the union of engines a slot's batch needs, in Create();
   - make the slot→idea mapping a constant per slot file.
6. **Extra data streams** (Data2+, the XMKT legs, the daily stream, 1-tick DataLoader) are set up on the chart or workspace, not in code. A rule that needs Data2 can only run on a chart that has Data2. The registry must declare stream needs and fail fast in StartCalc with a clear message, the way `_TestGate0` throws in Strategy mode.
7. **Importing into MC.** New files must be imported or copied into MC's CS folder and compiled. Is the repo itself MC's CS folder? The csproj references sit under StudyServer\Techniques, which hints that it is (verify how Ettienne deploys: clone in place vs copy vs .pln). Adding 100+ Function files means one import per batch.
8. **Reflection-based discovery** (scanning for `IEntryRule` types) probably works, but I advise against it. It hides compile-time coverage, depends on how MC loads the assembly (verify) and adds nothing over an explicit `switch`.
9. **Portfolio Trader vs chart.** M-PORT publishing is active only in Portfolio Trader. PF items are not per-signal entries at all.

## 4. Recommended structure
### Files (flat, in the repo root = MC folder)
```
_Sbx.Function.CS              interfaces + DTOs (ISbxContext, IEntryRule, IExitModule, IGateModule, IRiskModule,
                              EntryIntent/ExitIntent, SbxBar, SbxNeeds flags, SbxParams)
_SbxRegistry.Function.CS      static Create(string ideaId, int variant) => explicit switch; Needs(ideaId); list of ids
_SbxHost.Strategy.CS          abstract _SbxHost : _VolumeProfileFuturesBase  (the only MC-facing logic)
_Test1.._Test10.Strategy.CS   class _TestN : _SbxHost { }   (each one line, plus an optional DefaultIdea const)
_SbxE_<ID>.Function.CS        one file per entry rule, e.g. _SbxE_PS01.Function.CS, _SbxE_A2_49.Function.CS
_SbxX_<ID>.Function.CS        exit/risk modules (EX-xx, A2-72..79)
_SbxG_<ID>.Function.CS        gates/regime (RG-xx, A2 gates)
_SbxL_<ID>.Function.CS        layers (filters on host events)
tests/SandboxTests/           net8 project linking _Sbx*.Function.CS + a fake ISbxContext replaying CSV 1m bars
```
Group small sibling variants in one file (e.g. PS-01/PS-02/A2-41) to keep the file count down. Keep `_TestGate0` as is. Gate 0 is already a sandbox host and a model to follow.

### Interfaces (C# 7.3 sketch)
```csharp
[Flags] public enum SbxNeeds { None=0, Profile=1, Daily=2, Tick=4, Xmkt=8, PriceAction=16, LevelStudy=32, Avwap=64, Mtf=128, News=256 }
public interface ISbxContext {            // implemented by _SbxHost over the base class; by a fake in tests
  SbxBar Bar { get; } DateTime Et { get; } DateTime Day { get; } double Tick { get; }
  bool IsRth { get; } bool IsRollWindow { get; } bool RollToday { get; }
  int Position { get; } double EntryPrice { get; }
  // read-only views over lazy base engines; null when not enabled (rule declared no Need)
  IProfileView Profile { get; } IDailyView Daily { get; } ILevelView Levels { get; } ITickView Ticks { get; } IXmktView Xmkt(string role);
  void Journal(string line);              // engineSink(Journal), prefixed with idea id + variant
}
public interface IEntryRule {
  string Id { get; } SbxNeeds Needs { get; } string ParamsTag { get; }  // pre-registered sweep cell, journaled on RUN
  void OnSessionStart(ISbxContext c); void OnRoll(ISbxContext c);
  EntryIntent OnBar(ISbxContext c);       // closed bar only; Intent.None most bars
}
public interface IExitModule { string Id { get; } void OnEntry(ISbxContext c, EntryIntent e); ExitIntent OnBar(ISbxContext c); }
public interface IGateModule { string Id { get; } bool Allow(ISbxContext c, EntryIntent e); }
public interface IRiskModule { string Id { get; } int Size(ISbxContext c, EntryIntent e); }
```
- `EntryIntent` holds: side, kind (Market/Limit/Stop), price, its own stop/target (optional), tag and expiry bars.
- Inputs on `_SbxHost`: `Idea` (string, e.g. "PS-01"), `Mode`, `Variant`, `ExitModule`, `GateModule`, `RiskModule`. That keeps the catalogue's §2.5/§5 convention, and the RUN line already dumps the inputs.
  - Alternative: an int `Idea` with an enum, if a string input misbehaves in Portfolio Trader optimisation (verify).
- Mode handling: ControlRT/ControlFL/Canary are implemented once in the host, as transforms of the intent (random time, fake level shift via `_Rng`). Each rule does not reimplement them.
- Host flow:
  - Create(): pre-creates the order set.
  - StartCalc(): resolves the rule through the registry, checks `Needs` against the configured engines and chart streams (throws if missing), journals the pre-registration line, then calls base.
  - CalcBar(): runs gate → entry → risk → exit, sends orders, and writes per-trade gross/net through the CS-1 journal.

### Roll and journaling
- The host calls `rule.OnRoll` when `rollOn(1)`, and passes `IsRollWindow` (the ±1 exclusion).
- Multi-session state inside a rule must reset on roll (A2-39 Reset).
- The off-MC fake context injects synthetic rolls, the way RollTests does.
- Journal lines are prefixed `SBX <id> v<variant>`, and the config hash goes on RUN, per §2.5.

## 5. Fit of catalogue entries (approximate, by family; from the §4 matrix types and the Needs lines)
| Class | Examples | Fit |
|---|---|---|
| A. Pure entry on Data1 + base engines (profile, VWAP, AVWAP, IB, sessions, PriceAction) | most PS, VW, AV, MT, SP, TS, DV-01..15/19–23, A2-49/52/53/56/57/58/60/63–66 | clean: `IEntryRule` + a default bracket |
| B. Entry + named exit/risk module | PS-01 host H1, A2-09/63 hosts H2/H3, DV-24, A2-51 exit arms | clean once `IExitModule` exists; the module is chosen with `ExitModule` |
| C. Needs the daily stream / swing (W-D) | SW-01..06, SW-02, A2-69 | fits; `Needs=Daily`; chart needs a daily Data2 (or the internal DailyContext) |
| D. Needs ticks / bid-ask (W-T, ≈ 500 sessions) | OF-01..08, DV-16/17/18, OF-07 layer | fits the interface; `Needs=Tick`; data-limited; Bar Magnifier/DataLoader setup per chart |
| E. Cross-market (M-XMKT) | the 8 A2 XR entries, NQ/YM/RTY M10 checks | fits; `Needs=Xmkt`; chart/workspace must carry the legs |
| F. Missing data feeds | 11 ✗M-NEWS, 9 ✗M-GMM (now partly in PR #20 + feat/gmm-core), 1 ✗M-GEX, A2-36 | blocked by data, not by the sandbox |
| G. Layers L (13), gates RG/A2 | MT-03, VW-07, AV-08/10, RG-01..08, A2-25/26/29/31/43/50/55/61/70 | `IGateModule`/layer over host events, not entries |
| H. Canaries/controls K (6), Gate 0 | VW-08, A2-24, DV-10, A2-53, DV-01, PS-03, A2-33/39 | already in `_TestGate0`; could be ported later as `Mode=Canary` rules |
| I. Not signal-level | PF-01..07, M-PORT, A2-40/59 cost, A2-78, M1..M30 methodology | base class / Portfolio Trader / analysis scripts |
| J. Parked | L2-01..07 | need recorded L2; out of scope |

## 6. Risks
1. **One-assembly blast radius**: a bad sandbox file breaks live strategies. Gate imports on the CI build and tests.
2. **Create-vs-StartCalc ordering** for engine configs (§3.5). This is the main technical unknown.
3. **Fixed order set**: OCO brackets, scale-outs (A2-72..79) and the SAR (A2-53) need enough pre-created orders. Size the set once, from the most demanding module.
4. **Hidden lookahead/state leaks** between bars when rules read engine views. Expose only closed-bar views (`closedBarsOnly`), and replay-test against the off-MC fake.
5. **Memory/CPU** when a host enables many engines (`_TestGate0` already drops to 5 controls in Variant 0). Enable engines per declared Need.
6. **Parameter discipline**: `Variant` must decode only pre-registered cells (M2/M3). The registry should own the sweep tables so they are versioned and journaled.
7. **Compile/import time**: 100+ new files on top of 493. Grouping variants per file helps.

## 7. Open questions to verify in MC
1. Does the PowerLanguage .NET Editor support subfolders, or only the flat CS folder? Is the repo cloned directly into `StudyServer\Techniques\CS`?
2. Are base config properties set in StartCalc() before base.StartCalc() honoured by the lazy engine builders? Probe: set `DailyConfig.enabled` from an input.
3. Is there a cap on how many orders one signal can create in Create() (names, OCO groups)? Do generic order names plus a tag field keep the CS-1 journal arms readable?
4. Do string `[Input]`s work in Portfolio Trader and the optimiser? If not, use an int Idea index.
5. Does a study file that holds only Function-namespace plain classes (no FunctionObject) import and compile cleanly through File → Import? The repo says yes for `_Gate0`; confirm on a fresh install.
6. Compile time of the full assembly at about 600 files; any editor limit.
7. Data2/tick/XMKT stream requirements per slot: can the host detect a missing stream in StartCalc, or only on the first CalcBar?

## 8. Suggested first step (when implementation is approved)
1. Build `_Sbx.Function.CS`, `_SbxRegistry.Function.CS` and `_SbxHost.Strategy.CS`.
2. Re-base `_Test2` (TS-01) and `_Test3` (A2-49). Both are clean class-A, not G0-d-gated, Batch-1 ideas.
3. Add `tests/SandboxTests`.
4. Settle open questions 2–4 with a one-day MC probe before porting the rest.
