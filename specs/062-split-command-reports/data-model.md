# Data Model: Split CommandReports and type the defect/summary contracts

No persisted-artifact schema changes. The entities below are **in-memory types**
and **module boundaries**; none is serialized (the JSON writer is untouched).

## 1. `Diagnostic` (extended) — `FS.GG.SDD.Artifacts/Diagnostics.fs(i)`

Add one field to the existing record:

```fsharp
type Diagnostic =
    { Id: string
      Severity: DiagnosticSeverity
      Artifact: ArtifactRef option
      Location: SourceLocation option
      Message: string
      Correction: string
      RelatedIds: string list
      IsToolDefect: bool }        // NEW — true ⇒ a blocked command escalates to exit 2
```

**Rules**
- `create` sets `IsToolDefect = false` (signature unchanged; existing callers
  unaffected).
- Exactly seven constructors set it `true` via `markToolDefect`: `toolDefect`,
  `scaffold.providerFailed`, `scaffold.providerUnavailable`,
  `scaffold.providerWroteSddTree`, `scaffold.mirrorFailed`,
  `upgrade.selfUpdateFailed`, `upgrade.stepFailed`. This set MUST equal today's
  `providerDefectIds` (FR-003).
- Not serialized. `WorkModel.parseEmbeddedDiagnostic` sets `IsToolDefect = false`
  on the round-trip (the field is meaningless for round-tripped diagnostics; see
  research Decision 2).

**New public functions** (the only sanctioned surface additions, FR-011):
- `markToolDefect: Diagnostic -> Diagnostic` — sets `IsToolDefect = true`.
- `signalsStaleView: Diagnostic -> bool` — the single predicate deciding whether a
  diagnostic signals a stale generated view (replaces the `IndexOf("stale")`
  substring test in `HandlersAgents`).

## 2. `StagePlan` — per-stage summaries carrier (Commands)

Replaces the positional 12-tuple in `CommandWorkflow.nextLifecycleEffects`. Field
names and types mirror `CommandModel` exactly so values flow through unchanged.

```fsharp
type StagePlan =
    { Diagnostics: Diagnostic list
      Specification: SpecificationSummary option
      Clarification: ClarificationSummary option
      Checklist: ChecklistSummary option
      Plan: PlanSummary option
      Tasks: TasksSummary option
      Analysis: AnalysisSummary option
      Evidence: EvidenceSummary option
      Verification: VerificationSummary option
      Ship: ShipSummary option
      GeneratedViews: GeneratedViewState list
      PlannedEffects: CommandEffect list }

let emptyStagePlan =
    { Diagnostics = []; Specification = None; Clarification = None; Checklist = None
      Plan = None; Tasks = None; Analysis = None; Evidence = None
      Verification = None; Ship = None; GeneratedViews = []; PlannedEffects = [] }
```

**Rules**
- Each command arm produces `{ emptyStagePlan with <its fields> }`; the downstream
  assembly reads the record fields in place of the tuple positions.
- Placement: co-locate with `nextLifecycleEffects` (in `CommandWorkflow.fs`, or
  `CommandTypes` if a `.fsi` entry is warranted). Internal — not part of any
  serialized or public CLI contract.

## 3. Module layout (Commands) — the split

| Unit | File | Responsibility |
|------|------|----------------|
| `DiagnosticConstructors` | `CommandReports/DiagnosticConstructors.fs(i)` | ~90 command diagnostic constructors (incl. `toolDefect`, now `|> markToolDefect`) |
| `NextActionRouting` | `CommandReports/NextActionRouting.fs(i)` | `outcome` + the `nextAction` elif cascade |
| `ReportAssembly` | `CommandReports/ReportAssembly.fs(i)` | `buildReport`, `helpReport`, `exitCodeForReport` (reads `IsToolDefect`) |
| `CommandReports` (facade) | `CommandReports.fs(i)` | re-exports the constructors + the three assembly functions; **public surface unchanged** |

**Rules**
- `CommandReports.fsi` is byte-identical to today (facade preserves every member).
- `providerDefectIds` is deleted (lived in the old assembly region).
- Compile order (in `.fsproj`): `DiagnosticConstructors` → `NextActionRouting` →
  `ReportAssembly` → `CommandReports`, replacing the single `CommandReports.fs`
  entry at its current position.

## Invariants (must hold after the change)

- `exitCodeForReport`: `Blocked ∧ (∃ d. d.IsToolDefect) ⇒ 2`; `Blocked ∧ ¬… ⇒ 1`;
  `Succeeded | SucceededWithWarnings | NoChange ⇒ 0`. Identical to today for every
  current diagnostic.
- `HandlersAgents` staleness set is unchanged: `signalsStaleView d` returns the
  same boolean as `d.Id.IndexOf("stale", OrdinalIgnoreCase) >= 0` for every id.
- Every command's `CommandReport` (and thus JSON/text bytes) is identical under the
  tuple→`StagePlan` swap and the module split.
