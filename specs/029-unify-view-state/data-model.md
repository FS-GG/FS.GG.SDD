# Phase 1 Data Model: Unify generated-view-state construction

This refactor introduces **no** new data and changes **no** record shape, field, or
schema version. It changes only how one existing record is constructed. The entities below
are the *subject* of the refactor, documented so the unified signature is unambiguous.

## Entity: `GeneratedViewState` (unchanged)

The lifecycle generated-view record emitted by command handlers. Defined in
`FS.GG.SDD.Artifacts.WorkModel`; **not modified** by this feature.

| Field | Type | Notes (invariant preserved by the unified constructor) |
|---|---|---|
| `Path` | `string` | view artifact path; passed through verbatim |
| `Kind` | `string` | view discriminator — the **only** value that differed across the four old constructors; now the `kind` parameter |
| `SchemaVersion` | `int option` | always `Some 1` |
| `Generator` | `GeneratorVersion option` | always `Some generator` |
| `Sources` | `GeneratedViewSource list` | normalized `List.sortBy _.Path` |
| `OutputDigest` | `OutputDigest option` | passed through verbatim |
| `Currency` | `GeneratedViewCurrency` | passed through verbatim (`Current`/`Blocked`/`Missing`/`Stale`) |
| `DiagnosticIds` | `string list` | normalized `List.distinct |> List.sort` |

**Valid `Kind` values** (the set in use today — this refactor adds none):
`"workModel"`, `"analysis"`, `"verification"`, `"ship"`, `"governance-handoff"`,
`"agent-commands"`, and `"summary"`. The first six are produced via the four
named constructors and are re-pointed onto the unified `generatedViewState`.
`"summary"` is built by an **inline** `GeneratedViewState` record in
`HandlersRefresh.fs` and is **out of scope** — see "Out of scope" below.

## Out of scope: refresh's `"summary"` view

`HandlersRefresh.fs` constructs a `GeneratedViewState` with `Kind = "summary"`
as an inline record literal (not one of the four named constructors). Its
`Sources` field is assigned `summarySources` verbatim — order follows
`structuredSourcePaths`, the same order consumed by the rendered summary
Markdown (`refreshSummaryMarkdown`) — and is deliberately **not**
`List.sortBy _.Path`-normalized. Routing this site through the unified
constructor would impose that sort and could change output bytes, so it is
**excluded** from this refactor and stays an inline literal. This keeps
`computeRefreshPlan`'s self-contained guard untouched (research R-3) and the
constructor-definition count (SC-001) unaffected (the inline literal is not a
named `let` constructor).

## Internal constructor (the change)

**Before** — four definitions differing only by a frozen `Kind`:

| Symbol | `Kind` | Location |
|---|---|---|
| `generatedViewState` | `"workModel"` | `ViewGeneration.fs:463` |
| `analysisGeneratedViewState` | `"analysis"` | `ViewGeneration.fs:258` |
| `verifyGeneratedViewState` | `"verification"` | `HandlersVerify.fs:228` |
| `shipGeneratedViewState` | `kind` (param) | `HandlersShip.fs:29` |

**After** — one definition in `Foundation.fs`:

```fsharp
// FS.GG.SDD.Commands.Internal.Foundation
let generatedViewState
    (path: string)
    (kind: string)
    (generator: GeneratorVersion)
    (sources: GeneratedViewSource list)
    (outputDigest: OutputDigest option)
    (currency: GeneratedViewCurrency)
    (diagnosticIds: string list)
    : GeneratedViewState =
    { Path = path
      Kind = kind
      SchemaVersion = Some 1
      Generator = Some generator
      Sources = sources |> List.sortBy _.Path
      OutputDigest = outputDigest
      Currency = currency
      DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }
```

Visibility: `internal` via the `[<AutoOpen>] module internal Foundation` in namespace
`FS.GG.SDD.Commands.Internal`. No `.fsi` entry (the public surface is the unchanged
`CommandWorkflow.fsi` facade).

## Helper: `blockingDiagnosticIds` (new, internal)

Extracts the recurring "error-severity diagnostic ids" projection (10 call sites).

```fsharp
let blockingDiagnosticIds (diagnostics: Diagnostic list) : string list =
    diagnostics
    |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
    |> List.map _.Id
```

Behavior-equivalent to every inlined occurrence: same predicate, same projection, same
order (no added sort — callers that previously sorted still sort downstream, e.g. inside
the constructor's `DiagnosticIds` normalization).

## Helper: `blockedWorkModelView` (new, internal)

Extracts the "prerequisites missing → blocked workModel view" construction (9 handler
sites; excludes the non-identical `ViewGeneration.fs:562`).

```fsharp
let blockedWorkModelView (path: string) (generator: GeneratorVersion) (blockingIds: string list) : GeneratedViewState =
    generatedViewState path "workModel" generator [] None GeneratedViewCurrency.Blocked blockingIds
```

## Local rename (output-neutral)

`HandlersAgents.fs:364`: local string `generatedViewState` → `generatedViewStateLabel`
(the `AgentGuidanceSummary.GeneratedViewState` status label), to avoid shadowing the
module-level constructor. Not an entity change.

## State transitions

None. This feature has no stateful behavior; it is pure construction-site consolidation
within the existing MVU `update` path.
