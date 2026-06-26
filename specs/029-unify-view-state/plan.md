# Implementation Plan: Unify generated-view-state construction

**Branch**: `029-unify-view-state` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/029-unify-view-state/spec.md`

## Summary

R8 — an internal (Tier 2) follow-on to the completed R1–R7 roadmap — collapses four
byte-identical generated-view-state constructors into one `kind`-parameterized
constructor and routes every call site through it, then extracts the two view/effect
helper patterns that cluster around it. The cluster is the residual §5.5
micro-duplication the original refactor report predicted would "fall out naturally" but
did not.

Three behavior-preserving moves, in priority order:

1. **One constructor (US1/P1).** `generatedViewState` (`Kind = "workModel"`),
   `analysisGeneratedViewState` (`Kind = "analysis"`), and `verifyGeneratedViewState`
   (`Kind = "verification"`) are character-for-character identical to
   `shipGeneratedViewState` except for a frozen `Kind` literal. `shipGeneratedViewState`
   already takes `kind` and is in active use for `"analysis"`/`"verification"`/
   `"ship"`/`"governance-handoff"`/`"agent-commands"`, so it is the proven canonical shape. Keep
   **one** constructor named `generatedViewState` with the ship signature
   (`path → kind → generator → sources → outputDigest → currency → diagnosticIds`),
   hosted in `Foundation.fs` (compiles first), and delete the other three.
2. **`blockingDiagnosticIds` helper (US2/P2).** The shape
   `diagnostics |> List.filter (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError) |> List.map _.Id`
   appears **10×**. Extract once; route all sites through it.
3. **`blockedWorkModelView` helper (US3/P3).** The shape
   `generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids`
   appears **9×** (handlers only — never `computeRefreshPlan`). Extract once.

**Explicitly out of scope:** the inline `GeneratedViewState` record in
`HandlersRefresh.fs` (`Kind = "summary"`) is **not** re-pointed onto the unified
constructor — its `Sources` follow `structuredSourcePaths` order (shared with the
rendered summary Markdown) and are intentionally not `List.sortBy _.Path`-normalized,
so routing it would risk output drift. It stays an inline literal (see
data-model.md "Out of scope"); SC-001 counts only the four named `let` constructors.

**Technical approach**: mechanical edits inside `src/FS.GG.SDD.Commands/CommandWorkflow/`,
gated by the existing test suite plus byte-identical `.fsi`, all four
`PublicSurface.baseline` snapshots, and deterministic `--json`/`--text` output across
every command. All affected bindings are `internal`/`[<AutoOpen>]` in the
`FS.GG.SDD.Commands.Internal` namespace, so no signature or surface changes. Output is
*provably* byte-identical because the unified body keeps the exact `Sources |> List.sortBy
_.Path` and `DiagnosticIds |> List.distinct |> List.sort` normalizations. Finish by adding
an R8 row + status detail to the refactor analysis report.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: standard library only. No new dependency; Spectre.Console / JSON
layers are untouched.

**Storage**: N/A — source-only edits.

**Testing**: `dotnet test FS.GG.SDD.sln` (F# xUnit-style tests across 4 test projects:
Artifacts, Validation, Cli, Commands), each carrying a `PublicSurface.baseline` snapshot.
Baseline: 434 `[<Fact>]`/`[<Theory>]` attributes (438 assertions per report).

**Target Platform**: Linux/cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`).

**Performance Goals**: N/A — no hot-path change; the unified constructor does the same
work as today. Output must stay byte-identical.

**Constraints**: Tier 2 — every public `.fsi` and every `PublicSurface.baseline` stays
byte-identical; deterministic `--json`/`--text` stays byte-identical; Release build green;
no new warning category; the `WarningsAsErrors=FS3261;FS0025` ratchet stays at 0
(`Directory.Build.props:19`); no `#nowarn` introduced; `Directory.Build.props` unchanged.

**Scale/Scope**: 4 constructor definitions → 1; ~20 constructor call sites re-pointed;
10 `blockingDiagnosticIds` sites; 9 `blockedWorkModelView` sites; 1 local-shadow rename.
All within 8 files under `src/FS.GG.SDD.Commands/CommandWorkflow/`. Verified against `main`
@ `7a6280f` on 2026-06-26.

### Grounded inventory (current tree, verified 2026-06-26)

Constructor **definitions** (4 → 1):

| Definition | File:line | `Kind` | Disposition |
|---|---|---|---|
| `generatedViewState` | `ViewGeneration.fs:463` | `"workModel"` | becomes canonical (gains `kind` param), moves to `Foundation.fs` |
| `analysisGeneratedViewState` | `ViewGeneration.fs:258` | `"analysis"` | deleted; caller passes `"analysis"` |
| `verifyGeneratedViewState` | `HandlersVerify.fs:228` | `"verification"` | deleted; callers pass `"verification"` |
| `shipGeneratedViewState` | `HandlersShip.fs:29` | param | deleted; callers call `generatedViewState` (already pass `kind`) |

Constructor **call sites** (re-pointed):

| Symbol | Sites | Locations |
|---|---|---|
| `generatedViewState` (workModel) | 11 | `ViewGeneration.fs:562,579,607`; `HandlersEarly.fs:56,97,140,189,240`; `HandlersAnalyze.fs:67`; `HandlersEvidence.fs:613`; `HandlersVerify.fs:479`; `HandlersShip.fs:430` |
| `analysisGeneratedViewState` | 1 | `ViewGeneration.fs:448` |
| `verifyGeneratedViewState` | 2 | `HandlersVerify.fs:510,511` |
| `shipGeneratedViewState` | 6 | `HandlersShip.fs:172,448,449,453,454`; `HandlersAgents.fs:345` |

`blockingDiagnosticIds` shape (`filter Error |> map _.Id`): **10** sites (HandlersEarly ×5
+ analyze/evidence/verify/ship + ViewGeneration). `blockedWorkModelView` shape
(`… GeneratorVersion [] None Blocked ids`): **9** sites (HandlersEarly:56,97,140,189,240;
HandlersAnalyze:67; HandlersEvidence:613; HandlersVerify:479; HandlersShip:430).

**Naming hazard (must handle):** `HandlersAgents.fs:364` binds a *local string*
`let generatedViewState = "blocked"|"stale"|"missing"|"current"` for the
`AgentGuidanceSummary.GeneratedViewState` field — unrelated to the constructor. The
constructor is called earlier in the same function (`HandlersAgents.fs:345`, currently
`shipGeneratedViewState`), so after renaming there is no resolution error, but the
identically-named local string is a readability trap. Rename the local to
`generatedViewStateLabel` (output-neutral; updates the `GeneratedViewState = …` field
assignment only).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ | Tier 2; affected bindings are `internal` with no `.fsi`. The public `CommandWorkflow.fsi` is the only signature and stays byte-identical. Verification = byte-identical deterministic output + existing suite. |
| II. Structured artifacts are the contract | ✅ | No artifact schema, field, or `Kind` value changes. `GeneratedViewState` shape and serialized form are untouched. |
| III. Visibility lives in `.fsi` | ✅ | All edits inside `[<AutoOpen>] module internal` files in `…Commands.Internal`; `.fsi` and all 4 `PublicSurface.baseline`s byte-identical. |
| IV. Idiomatic simplicity | ✅ | Net *fewer* functions; plain `let` helpers, no new abstraction machinery. |
| V. Elmish/MVU boundary | ✅ | Pure view-construction helpers inside the existing `update`/effect path; no new I/O, no boundary change. |
| VI. Test evidence | ✅ (see research) | Behavior-preserving, so the gate is byte-identical golden/snapshot output, not a new fail-before test. Internal bindings are not reachable from the test projects (no `InternalsVisibleTo`), so coverage is asserted at the command-output (golden) layer. |
| VII. One contract for agents + humans | ✅ | No command, output, or agent-surface change. |
| VIII. Observability & safe failure | ✅ | Diagnostic content/ordering unchanged (`blockingDiagnosticIds` preserves filter+map+order; constructor preserves `distinct |> sort`). |

**Change tier**: **Tier 2 (internal change)** — implementation cleanup, no user- or
tool-visible contract change. No Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/029-unify-view-state/
├── plan.md              # This file
├── research.md          # Phase 0 output (constructor home/name, P3 scope, test posture)
├── data-model.md        # Phase 1 output (GeneratedViewState entity + unified signature)
├── quickstart.md        # Phase 1 output (build/test/byte-diff verification)
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

No `contracts/` directory: this feature changes **no** external/tool-facing interface
(Tier 2, internal-only). The single relevant signature — the unified internal constructor
— is documented in `data-model.md`.

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
├── Foundation.fs        # + canonical `generatedViewState` (kind-param) + `blockingDiagnosticIds` + `blockedWorkModelView`
├── ViewGeneration.fs    # − `generatedViewState` def, − `analysisGeneratedViewState` def; call sites re-pointed
├── HandlersEarly.fs     # blocked-view + blocking-id call sites re-pointed
├── HandlersAnalyze.fs   # blocked-view + blocking-id call sites re-pointed
├── HandlersEvidence.fs  # blocked-view + blocking-id call sites re-pointed
├── HandlersVerify.fs    # − `verifyGeneratedViewState` def; call sites re-pointed
├── HandlersShip.fs      # − `shipGeneratedViewState` def; call sites → `generatedViewState`
└── HandlersAgents.fs    # call site → `generatedViewState`; rename local string `generatedViewState`→`generatedViewStateLabel`

tests/FS.GG.SDD.Commands.Tests/   # golden/byte-identical command-output coverage (unchanged baselines)
```

**Structure Decision**: Single-solution F# layout retained. All edits are confined to the
eight `CommandWorkflow/` internal modules plus the refactor-report doc. The canonical
constructor and both helpers live in `Foundation.fs` because it compiles before every
consumer (fsproj line 14, ahead of `ViewGeneration.fs` at 18 and all `Handlers*` at 20–26)
and already opens `FS.GG.SDD.Artifacts.WorkModel`, bringing `GeneratedViewState`,
`GeneratedViewCurrency`, `GeneratorVersion`, and `GeneratedViewSource` into scope.

## Complexity Tracking

No Constitution Check violations — this section is intentionally empty.
