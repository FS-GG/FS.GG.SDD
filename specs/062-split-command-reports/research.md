# Research: Split CommandReports and type the defect/summary contracts

Phase 0 resolves the design decisions behind the plan. The load-bearing finding is
the **round-trip asymmetry** (Decision 2), which is why the defect classification
is a stored field while the staleness classification is a derived predicate.

## Decision 1 — Tool-defect class as a stored `IsToolDefect` bit

**Decision**: Add `IsToolDefect: bool` to the `Diagnostic` record
(`Diagnostics.fs`). Default it to `false` inside `create` (so `create`'s signature
and all its callers are unchanged), and flip it to `true` for the seven
defect-producing constructors via a small combinator:

```fsharp
let markToolDefect (d: Diagnostic) = { d with IsToolDefect = true }
// e.g. scaffoldProviderFailed name exitCode = create "..." ... |> markToolDefect
```

`exitCodeForReport` becomes:

```fsharp
if report.Diagnostics |> List.exists (fun d -> d.IsToolDefect) then 2 else 1
```

and `providerDefectIds` is deleted.

**Rationale**: The exit-code decision runs over the **in-process** report
diagnostics that were freshly constructed this invocation — so a stored bit set at
construction is authoritative there. This is the only design that meets **SC-003**
("adding a defect diagnostic requires editing zero separate registry locations"):
the class travels with the constructor, exactly as issue #72 asks ("type an IsDefect
bit on the diagnostic; producers already centralize id+message+correction").

**The seven defect ids to mark** (must reproduce today's `providerDefectIds` set
exactly — FR-003): `toolDefect` (in `CommandReports`→`DiagnosticConstructors`),
and in `Artifacts.Diagnostics`: `scaffold.providerFailed`,
`scaffold.providerUnavailable`, `scaffold.providerWroteSddTree`,
`scaffold.mirrorFailed`, `upgrade.selfUpdateFailed`, `upgrade.stepFailed`.

**Alternatives considered**:
- *Derived classifier keyed on id* (`isToolDefect: Diagnostic -> bool` matching id
  strings): keeps the record unchanged (Tier 2) but adding a defect still edits one
  central function — fails the spirit of SC-003 (the class is not co-located with
  the producer). Rejected.
- *Add a param to `create`*: ripples to every `create` call and record literal.
  Rejected in favour of the defaulted-field + `markToolDefect` combinator.
- *A `DiagnosticClass` DU field* (UserInput | ToolDefect | …): more expressive but
  over-scoped for a boolean escalation with two current outcomes. A bool is the
  idiomatic-simplicity choice (Principle IV); can widen later if a third class
  appears.

**Serialization safety**: the JSON writer names fields explicitly and is **not**
touched, so `IsToolDefect` is never emitted — default/`--json` bytes are unchanged
(FR-007), verified by the existing golden suites.

## Decision 2 — Staleness as a derived predicate, NOT a stored bit (the round-trip finding)

**Finding**: `HandlersAgents` classifies staleness over diagnostics **read back
from a persisted `work-model.json`**, reconstructed by
`WorkModel.parseEmbeddedDiagnostic`, which rebuilds each `Diagnostic` from only the
serialized fields (`id`, `severity`, `message`, `correction`, `relatedIds`) and
sets `Artifact = None`, `Location = None`. **Any non-serialized record field is
reset to its default across this round-trip.** A stored `SignalsStaleView` bit would
therefore be lost by the time `HandlersAgents` reads it — and serializing it would
change `work-model.json` bytes, violating FR-007/FR-012.

**Decision**: Replace the inline substring test with a single centralized predicate
in the `Diagnostics` module, keyed on the id (the one fact that survives the
round-trip):

```fsharp
// Diagnostics.fs — the single place that knows which ids signal a stale view.
let signalsStaleView (d: Diagnostic) =
    d.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) >= 0
```

`HandlersAgents` then calls `Diagnostics.signalsStaleView diagnostic` instead of
inlining `diagnostic.Id.IndexOf("stale") >= 0`. The `"stale"` literal exists in
exactly one named, documented function; the decision path (`HandlersAgents`) reads
a typed predicate (FR-004), and no free-form id literal remains in that path
(SC-004).

**Why the asymmetry with Decision 1 is correct, not accidental**: the defect
decision consumes **in-process** diagnostics (stored bit works, and SC-003 demands
it); the staleness decision consumes **round-tripped** diagnostics (stored bit
cannot survive, so it must be id-derived). The data-flow dictates the mechanism.
This is recorded so a future reader does not "unify" them into one stored bit and
silently break the agent-refresh path.

**Alternatives considered**:
- *Store the bit and re-derive it in `parseEmbeddedDiagnostic`* using the same
  predicate: lets `HandlersAgents` read a field, but adds a second record field and
  parse-time wiring for no behavioural gain over calling the predicate directly.
  Rejected (Principle IV).
- *Serialize the classification into `work-model.json`*: changes bytes and the
  artifact contract. Rejected (FR-007/FR-012).

## Decision 3 — Per-stage summaries as a defaulted `StagePlan` record

**Decision**: Introduce a record capturing the twelve values currently threaded
positionally through `nextLifecycleEffects`:

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
      GeneratedViews: GeneratedView list
      PlannedEffects: CommandEffect list }

let emptyStagePlan =
    { Diagnostics = []; Specification = None; Clarification = None; Checklist = None
      Plan = None; Tasks = None; Analysis = None; Evidence = None; Verification = None
      Ship = None; GeneratedViews = []; PlannedEffects = [] }
```

Each command arm builds `{ emptyStagePlan with … }` setting only its stage's
fields, e.g. `Specify -> let d, spec, gv, eff = computeSpecifyPlan model in {
emptyStagePlan with Diagnostics = d; Specification = spec; GeneratedViews = gv;
PlannedEffects = eff }`.

**Rationale**: Names each position, so the compiler catches a mis-assignment that
the positional 12-tuple accepts silently (FR-005). Keeping the existing
`computeXPlan` functions returning their current tuples confines the change to
`nextLifecycleEffects` — lowest-risk path to an identical report (the arms feed the
same values into the same downstream assembly). Field names mirror the exact
`CommandModel`/report fields the values already flow into.

**Alternatives considered**:
- *Change every `computeXPlan` to return `StagePlan`*: cleaner call sites but 10
  signature + `.fsi` changes and more surface churn for no behavioural difference.
  Deferred to the follow-up hotspot-split work item.
- *Anonymous record*: no shared `emptyStagePlan` default and weaker discoverability.
  Rejected.

## Decision 4 — Module split behind a stable `CommandReports` facade

**Decision**: Split `CommandReports.fs` into three internal units under a new
`src/FS.GG.SDD.Commands/CommandReports/` subfolder — `DiagnosticConstructors`
(~90 constructors), `NextActionRouting` (`outcome` + the ~290-line `nextAction`
cascade), `ReportAssembly` (`buildReport` / `helpReport` / `exitCodeForReport`) —
and keep `CommandReports.fs` as a thin facade that re-exports the constructors and
the three assembly functions so `module CommandReports`'s public members are
byte-identical.

**Rationale**: 34 files reference `CommandReports`, almost all via `open
FS.GG.SDD.Commands.CommandReports` calling constructors **and** assembly functions
unqualified; `PublicSurface.baseline` pins the entire `CommandReports.*` surface.
A facade keeps every call site and the baseline untouched (FR-010/FR-011) while
giving each responsibility its own compilation unit (FR-006). This mirrors the
established `CommandWorkflow.fs` + `CommandWorkflow/` facade pattern already in the
project.

**Cost**: the facade re-exports ~90 constructors as `let name =
DiagnosticConstructors.name` — mechanical and checked against the unchanged
`CommandReports.fsi`. Accepted as the price of a zero-churn, zero-surface-change
split. Compile order: `DiagnosticConstructors` → `NextActionRouting` →
`ReportAssembly` → `CommandReports` (facade must follow the modules it re-exports).

**Alternatives considered**:
- *Relocate `buildReport`/`exitCodeForReport`/`helpReport` to a public
  `ReportAssembly` module and drop the facade*: avoids the 90 re-exports but breaks
  ~8 test files' unqualified calls and changes `PublicSurface.baseline` — violates
  FR-010's "pass without baseline modification". Rejected.
- *Leave constructors in `CommandReports.fs`, extract only routing + assembly*:
  compile order forbids it — assembly must compile after the constructors, but the
  facade re-export must compile after assembly, so the constructors cannot share the
  facade's file. Rejected.

## Cross-cutting: regression strategy

The pre-existing golden/determinism suites plus `PublicSurface.baseline` and
`fsgg-sdd validate` are the safety net (US2). New semantic tests are added only
where behaviour is newly *typed*: (a) a defect-marked diagnostic exits 2 without
any id registry; (b) a stale-signalling diagnostic classifies regardless of id
spelling; (c) each command's report is unchanged under the tuple→record swap. The
`DiagnosticTests` suite is extended for `IsToolDefect`/`markToolDefect`/
`signalsStaleView`.
