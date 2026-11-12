# Research strategy source header

**Status:** workflow and documentation. This file does not implement a strategy, does not validate one, and does not report a result. The Jan 2025–Oct 2026 holdout (and the tick-window holdout of the last 125 sessions) stays unviewed.

**Applies to:** every new research strategy source file. Existing studies keep the headers they have until someone edits that file for another reason. Do not mass-rewrite them to match this standard. The ten sandbox examples were given this header when those files were standardized.

**Template:** [`sandbox/ResearchStrategyHeader.template.txt`](../sandbox/ResearchStrategyHeader.template.txt). Copy it to the top of the new strategy file. The template is comments only. It is not a study, and it stays out of every project file.

**File name.** The pattern is `_<ID>_<StudyName>.Strategy.CS`. The word `StudyName` in that pattern is a placeholder only. It is never the stem of a developed strategy. Replace it with a short PascalCase name taken from the catalogue description: an uppercase letter, then letters and digits. `ID` is the catalogue ID with each hyphen turned into an underscore. The suffix is `.Strategy.CS`. The public class is the file stem, a normal C# identifier, and does not include `.Strategy.CS`. The header's Catalogue ID line keeps the hyphen.

Catalogue ID A2-69 maps to file _A2_69_KaufmanThreeDay.Strategy.CS and class _A2_69_KaufmanThreeDay. The catalogue calls that entry "Kaufman 3-day trade"; `KaufmanThreeDay` is that description shortened. A file named `_A2_69_StudyName.Strategy.CS` does not follow this rule.

**Catalogue:** [`specs/trading-idea-catalogue.md`](../specs/trading-idea-catalogue.md) remains the idea library. Link the entry. Do not paste it into the header.

## What the header is for

The header is how a reader sees what the strategy does without reading the whole method. The same twenty-two section titles appear in the same order on every research strategy. A section that does not apply says `Not applicable`. A section that is not yet known says `Not yet verified`. Extra detail that only this strategy needs goes under **Notes**. Do not invent a new `// ##` title for it.

Write the header so it can be read in one sitting. A few lines per section is the aim. When the evidence is a catalogue entry, a journal, or a note, link it and name the window that was used.

## Required header sections

The template repeats these lines, in this order:

```
// ## 1. Identity
// ## 2. Provenance
// ## 3. Hypothesis
// ## 4. Trade walkthrough
// ## 5. Timing
// ## 6. Position and re-entry
// ## 7. Instruments
// ## 8. Data
// ## 9. Entry and filters
// ## 10. Position sizing and risk
// ## 11. Orders, execution, and costs
// ## 12. Trade management
// ## 13. Exits
// ## 14. Regimes
// ## 15. Dependencies
// ## 16. Pitfalls
// ## 17. Parameters
// ## 18. Acceptance policy
// ## 19. Validation
// ## 20. Edge monitoring
// ## 21. Log diagnostics
// ## 22. Notes
```

| Section | What to record |
|---|---|
| Identity | Hyphenated catalogue ID, the file name it maps to, study class (file stem only), header version, stage, version of the rules |
| Provenance | What comes from the source, and what is our assumption |
| Hypothesis | The mechanism, in a few sentences, plus the link |
| Trade walkthrough | Plain English: what opens the trade, what manages it, what closes it |
| Timing | When the signal is judged, and when the order can fill |
| Position and re-entry | How many positions, adds, reversals, and whether a stopped trade can come back |
| Instruments | Where the idea is usually discussed, and where it has actually been validated |
| Data | Data1, Data2, and any further series: bar size, what the series is, session, timezone, warmup |
| Entry and filters | The conditions that must all be true before an order is sent |
| Position sizing and risk | How size is chosen, and the risk limit |
| Orders, execution, and costs | Order type, fill assumption, commission, slippage |
| Trade management | What changes while the position is open |
| Exits | Hard stop, target, time exit, and what invalidates the idea |
| Regimes | Where the idea is meant to work, and where it is meant to stand aside |
| Dependencies | Engines and base class. Protected engine settings stay there. Strategy inputs are section 17 |
| Pitfalls | Lookahead, repaint, survivorship, rolls, data quality, and anything else that bites |
| Parameters | Each exposed strategy input: purpose, default, units, range, effect, interactions, and whether it is fixed or research-tuned |
| Acceptance policy | Which written policy this file is judged under |
| Validation | Status, and links to evidence. `Not yet verified` until a real run exists |
| Edge monitoring | How a person would notice the edge fading. See below |
| Log diagnostics | The log lines that explain why an order was sent, skipped, or closed |
| Notes | Anything useful that does not have a section. `Not applicable` when empty |

### Trade walkthrough, timing, position, logs

These four are what make the header useful on a chart. Keep them concrete.

**Trade walkthrough.** Three short paragraphs, or three labelled lines: trigger, management, exit. Name the prices and the clock times the code actually uses. This is the paragraph a reader should be able to retell.

**Timing.** Say whether `CalcBar` acts only on `EBarState.Close`, and which order type is sent (`MarketNextBar`, stop, limit). State which bar can fill. The engines manual §1.4 is the house rule: an order sent on bar *t*'s close works from bar *t+1*. If this strategy differs, say so here.

**Position and re-entry.** Say whether the strategy is flat-or-one, scales in, or reverses. Say whether a stop-out may re-enter the same session, and under which rule. If the strategy never re-enters, write that. `Not applicable` is for a study that does not take positions.

**Log diagnostics.** Name the tags the code already writes: the `journalArm` order name, the `logSignal` tag, and the skip or flatten strings. A reader should be able to open the journal and match a line to a decision. Point at engines manual §1.5 for how journaling is switched on. Do not paste that section. If the strategy does not log yet, write `Not yet verified` and the line you intend to add.

Format example only (not a strategy, not a result):

```
// Trigger: after 12:00 ET, a closed 30-minute bar beyond the morning high sends a buy stop.
// Management: the stop stays at the other side of the morning range. Nothing else moves it.
// Exit: the stop, or a market flatten at 15:55 ET. No target.
```

## Acceptance policy

The header names the policy. It does not rewrite the catalogue.

**`catalogue-default`.** Judge the file by the kill text already written for that catalogue ID, by catalogue §2.3, and, when the idea is A2-origin, by the A2 generic kill in §2.3. Where both speak, the stricter one stands. Copying those rules into the header is optional; changing them in the header does nothing to the catalogue.

**`big-winner-trend`.** Owner clarification, 8 Nov 2026. Use this only when the header predeclares it **before any result for this strategy is read**, and only because the written premise is a few large winners (a trend-following or other big-winner strategy). Scope is that one predeclared file.

Under `big-winner-trend`:

- A hard gate of the form "the largest winning trade is at most 10% of profits" is not applied to this file.
- Every other gate still applies, including the catalogue text for that ID.
- Cost-adjusted expectancy must still be positive after the largest 1% of **winning** trades are removed.
- Report both numbers: expectancy on all trades, and expectancy after that trim.
- The exemption is not a blanket for every strategy. A file on `catalogue-default` does not receive it.
- This standard does not edit existing catalogue entries to add or remove the 10% gate.

The catalogue on this branch (v2.7) states a different concentration rule: the A2 generic kill includes "> 50% of the PnL comes from one year" (§2.3), and M12 says no year should carry more than 50%. Those sentences stay as written.

**Expectancy**, wherever this standard uses the word, means the trader equation on cost-adjusted trades:

```
expectancy = win_rate * avg_win - loss_rate * avg_loss
```

`avg_win` and `avg_loss` are both positive magnitudes, so a loss adds to the second term. Use that convention for the original number and the trimmed number. `loss_rate = 1 - win_rate` when every closed trade is a win or a loss. If scratches exist, define them in the header before the run.

"The largest 1% of winning trades" is not given a rounding rule here. Before the run, write how many winning trades are removed when 1% is not a whole number. Do not choose that count after seeing the trimmed expectancy.

The 10% and 1% figures are the owner's stated policy. They are not an empirical finding from a run in this repo, and this document does not treat them as optimised thresholds.

## Validation and the sealed holdout

Section 19 stays `Not yet verified` until there is a run that this header can point at. A link needs a path or a document name, the sample window, and the cost model. A prose claim of "it works" with no link is not evidence.

Do not read, cite, or summarise results from the sealed holdout:

- W-M and W-D: January 2025 through October 2026
- W-T: the last 125 sessions

Catalogue §2.0.1 and M9 describe that split. One look makes it a new idea. This standard was written without opening any 2025–2026 performance output.

## Edge monitoring

Section 20 is a checklist for a person. It does not place orders, and it does not change parameters. There is no automatic retune and no autonomous live trading in this workflow.

Fill the five subsections inside section 20. They are part of that one section, on every strategy:

1. **Expectancy.** The formula above, on the same cost model as the test (commission, fees, slippage). Name the model. `Not yet verified` until a test exists.
2. **Observations.** How many closed trades the judgement uses. Catalogue M19 is the existing discussion of sample error. Do not copy that table in as a pass line for this strategy. Until a minimum count is written here **before** the incubation run, the count is `Not yet verified`.
3. **Drift.** What will be compared with the tested profile: regime mix, fill versus system price, and costs. Catalogue M15 and M21 are the existing language. The comparison values for this strategy are `Not yet verified` until they are taken from its own tested distribution and written down before incubation.
4. **Warning, pause, review.** Three human actions, predeclared or `Not yet verified`. A warning means look. A pause means stop new entries and keep managing open positions with the exits already in section 13. A review means the owner looks before new entries resume. None of the three is an automatic order or a parameter write.
5. **Uncertainty.** What would make the monitoring call weak: a short sample, one regime, a cost assumption, a data gap, the holdout still being sealed.

Do not invent a drawdown percent, a slippage band, or a trade count and present it as something this strategy's history has validated. If a number is required in order to incubate, predeclare it as a choice, label it as a choice, and leave `Not yet verified` on any claim that the number was measured.

Parameter changes, if a review later wants them, are a new parameter set written offline (catalogue M2, M11, M24). They are not applied by the monitor.

## Parameters

A new research strategy exposes each of its own parameters as one MultiCharts input. The shape verified in this repo is a public property with a parameterless `[Input]` attribute, and the code default assigned in the constructor (engines manual §1.2 and example 2):

```csharp
[Input]
public int DownCloses { get; set; }

public _A2_69_KaufmanThreeDay(object _ctx) : base(_ctx)
{
    DownCloses = 2;
}
```

That `2` is the catalogue's "two down closes", written down as a predeclared choice. It is not a fitted optimum.

No description argument on [Input] was verified in this repo. Study sources use a parameterless `[Input]`, and the attribute type itself is not in this repo. Do not invent an attribute, a constructor argument, or a description property. Header section 17 is the required description of every exposed parameter.

Do not put those strategy parameters in an input class, an input struct, or a protected config class. That earlier preference applied to research strategies and is superseded for new ones. It still applies to shared engines and to strategies already in the repo: leave their config classes as they are. Engine settings such as `DailyConfig` and `RollDetection` stay on the protected engine objects. Name those under Dependencies. They are not strategy inputs.

Section 17 has one block per exposed input, with these lines in this order:

```
// Purpose:
// Default:
// Units:
// Range:
// Effect:
// Interactions:
// Fixed or research-tuned:
```

- **Purpose.** What the input is for.
- **Default.** The constructor value. Call it a predeclared choice. Do not call a default optimal, best, or fitted unless a cited run, outside the sealed holdout, supports that word. If no default has been chosen, write `Not yet verified`.
- **Units.** Ticks, bars, closes, a fraction of equity, or `Not applicable`.
- **Range.** The values the strategy accepts, including what it does with a value outside that range. If the legal range has not been set, write `Not yet verified`.
- **Effect.** How changing it changes entries, size, or exits.
- **Interactions.** Other inputs it depends on or overrides. `Not applicable` when it stands alone.
- **Fixed or research-tuned.** `Fixed` means chosen before any run and not searched. `Research-tuned` means a range that was written down before any run. A value chosen after seeing results is a new parameter set (catalogue M2).

`TraceLevel` stays the base `[Input]` on `_CustomSignal`. A new strategy does not re-declare it unless it is deliberately overriding that input.

## Lifecycle

| Stage | Where the file lives | Who may move it on |
|---|---|---|
| `sandbox` | `sandbox/`, not in `PLStudies.csproj` | The author, when the draft exists |
| `selective-test` | Still `sandbox/`. One temporary project entry, removed when the test ends | The person running the test |
| `validated` | Still `sandbox/`. Section 19 points at evidence outside the holdout | The evidence, not a calendar date |
| `incubation` | Still out of the default project. A person monitors section 20 | The owner, to start a monitored run |
| `promoted` | Repo root, one explicit `Compile Include`, owner approval written in the header | The owner, in a separate change |
| `retired` | Removed from the project. Header says why and when. Source kept in `sandbox/` or git history | The owner |

Promotion is a separate change after validation and owner approval. This standard does not promote any file. Retirement removes the explicit `Compile Include` and takes the file out of the repo root, so the root lint does not demand that it stay compiled.

Incubation and selective tests may load the study in MultiCharts. Broker setup, Portfolio Trader deployment, and live order routing are outside this standard.

## Where a new file goes

1. Copy the template onto a new sandbox file whose stem is the catalogue ID plus a short descriptive name. For A2-69 that file is `sandbox/_A2_69_KaufmanThreeDay.Strategy.CS`. Do not leave the placeholder `StudyName` in the file name.
2. The public class, the file stem, and the MultiCharts study name are `_A2_69_KaufmanThreeDay`. That string is a valid C# identifier. It does not contain `.Strategy.CS`.
3. The header's Catalogue ID line keeps the hyphenated id, `A2-69`, and the file line shows `_A2_69_KaufmanThreeDay.Strategy.CS`.
4. That class name is unique among root studies and among sandbox studies. One assembly compiles every included study, so two public classes with one name fail the build.
5. The file stays in `sandbox/` through sandbox, selective-test, validated, and incubation.
6. Only a `promoted` file moves to the repo root and receives a `Compile Include`. A sandbox file is not renamed as part of promotion. A file whose name cites the wrong catalogue ID is renamed to the ID it implements: `_TS_02_EuropeanOpenDrift.Strategy.CS` implemented TS-01 and is now `_TS_01_EuropeanOpenDrift.Strategy.CS`.

Every sandbox study's public class is the file stem. The two earlier mismatches were aligned when those files were standardized: `_A2_70_OpenFadeHvGate.Strategy.CS` no longer declares `_Ex17OpenFadeHvGate`, and `_DV_15_EmaVwapPullback.Strategy.CS` no longer declares `_Ex20EmaVwapPullback`. `tests/RepoLintTests` does not allow a new mismatch.

## How the project includes studies

Checked on `origin/main` at `f7f5ac6` (merge of PR #101). PR #102 is not in that commit; see the open decisions below.

`PLStudies.csproj` is an old-style project (`ToolsVersion` 15.0, .NET Framework 4.8). It has no SDK-style default compile items. The C# files it builds are an explicit list of `<Compile Include="FileName.CS" />` in one item group: 507 includes, each a file name in the repo root, no directory and no `*` or `?`. Nothing under `sandbox/` is on that list. `PLStudiesPY.csproj` has no `Compile` items and no sandbox path.

MSBuild therefore does not compile `sandbox/`. A file sitting in that folder is source control only.

The test projects under `tests/` link named engine files (`..\..\_SomeEngine.Function.CS`) and, in some projects, `Generated\*.cs` inside that test project. Those globs do not reach `sandbox/`. A research strategy derives from MultiCharts types, so it is not a candidate for those net8.0 test projects.

`tests/RepoLintTests` requires every **root** `_*.Function.CS`, `_*.Strategy.CS`, and `_*.Indicator.CS` to appear in `PLStudies.csproj` (gitignore entries excepted). That rule is why a research file must not be copied to the repo root "just to try it" and then left there: the lint will require it to be compiled. The same suite now fails if:

- any `Compile Include` in any repo `csproj` points at `sandbox`
- `PLStudies.csproj` or `PLStudiesPY.csproj` uses a wildcard include, or turns on `EnableDefaultCompileItems`
- a sandbox study file name also exists at the repo root
- a sandbox strategy file is not `_<ID>_<DescriptiveName>.Strategy.CS`, uses the literal placeholder `StudyName`, disagrees in its public class with that file stem, or collides with another study

### Selective test, and putting it back

Known-safe for MSBuild, because the project only compiles listed files:

1. Add one line to the existing `Compile` item group:

   ```xml
   <Compile Include="sandbox\_A2_69_KaufmanThreeDay.Strategy.CS" />
   ```

2. Load that one file in the PowerLanguage Editor (File → Import of the single file, the same gesture the engines manual uses for a new study).
3. Run the test.
4. Remove that line.
5. Before any commit, `git diff -- PLStudies.csproj` shows no remaining change.

Do not add `<Compile Include="sandbox\*.CS" />`, `<Compile Include="*.CS" />`, or `<Compile Include="**\*.CS" />`. A root `*.CS` glob would compile every `.CS` file in the repo root, including gitignored local MultiCharts files and any new file dropped there. A `sandbox\` or `**` glob would compile every research draft into the same assembly as the live studies. One broken draft then breaks the assembly.

### PowerLanguage Editor churn

The Editor is a separate step from MSBuild. Two things are known, and one is not.

Known: studies are imported one file at a time (engines manual import lists; `specs/m-instr-spec.md` R2). The commit that created `sandbox/` (`c862ad2`, "Filling in test strategies and creating sandbox for studies") also rewrote `PLStudies.csproj` (378 insertions and 394 deletions in that file) while filling `_Test2` through `_Test10`. A wide project diff can land in the same save as a one-file import.

Known: after that commit, the ten sandbox sources were still absent from `PLStudies.csproj`. Storing a file under `sandbox/` did not, by itself, add a `Compile` line in the tree that was committed.

Not verified in the Editor on this machine: whether File → Import accepts a path in `sandbox\`, or only a file in the flat studies folder. `specs/sandbox-entries-feasibility.md` §7.1 left subfolder support open, and this change does not close it. If the Editor will not compile `sandbox\_A2_69_KaufmanThreeDay.Strategy.CS`, the temporary workaround is a **local uncommitted** copy at the repo root, with the same unique class and file name, plus one explicit root `Compile Include`. Delete the copy and the include when the test ends. Do not commit the root copy.

Also not claimed here: that opening the Editor rescans folders and rewrites the project. If a save touches any `Compile` line other than the one under test, restore those lines before committing. That is the churn to avoid. Do not "fix" it by switching the project to a wildcard.

### Cleanup checklist

- `PLStudies.csproj` matches the branch base, or the only difference is an owner-approved promotion.
- No `sandbox\` path and no wildcard in that file.
- No second copy of the study at the repo root.
- The header stage is no longer `selective-test` unless a test is actually in progress.
- Order names and the study class do not collide with a study that stayed in the project.

## Open decisions

- **PR #102** (catalogue v2.8, Oliver Kell) was merged into the PR #101 feature branch after PR #101 had already merged to `main`. This standard is based on `main` at `f7f5ac6`, which contains PR #101 and does not contain the Kell files. They were not pulled in.
- **Editor subfolder import** is unverified, as above. Until someone confirms it on the PowerLanguage Editor, a selective test may need the local root-copy workaround.
- **Sandbox class names** match the file stem, including the two files that used to declare `_Ex17OpenFadeHvGate` and `_Ex20EmaVwapPullback`.
- **Root-study headers** are not migrated by the sandbox standardization.
- **No strategy was validated** by writing this standard.
