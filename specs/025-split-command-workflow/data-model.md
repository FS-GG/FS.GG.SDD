# Phase 1 Data Model: Module Decomposition

This refactor introduces no runtime data entities. The "model" here is the
**static module/file decomposition** of the monolith — the entities are F#
modules, their concerns, their source provenance, and their dependency edges.
All line ranges are against `CommandWorkflow.fs` on `main` (6,814 lines) and are
the *current* section boundaries; exact cut points are settled at implementation
under the ≤1,500-line cap (FR-004).

## Facade entity

| Module | Namespace | File | Public surface | Source |
|---|---|---|---|---|
| `CommandWorkflow` | `FS.GG.SDD.Commands` | `CommandWorkflow.fs` | `init`, `update` (via unchanged `.fsi`) | lines 6665–6814 (`nextLifecycleEffects`, `init`, `update`) |

The facade `open`s `FS.GG.SDD.Commands.Internal` and contains only the
orchestration trio. Its paired `CommandWorkflow.fsi` is byte-identical to `main`.

## Internal module entities (namespace `FS.GG.SDD.Commands.Internal`)

Each is `[<AutoOpen>] module internal <Name>`, no `.fsi`, with a per-file header
redeclaring the artifact aliases it uses (Decision 3).

| # | Module / file | Concern | Source range (approx.) | ~LOC | Key bindings |
|---|---|---|---|---|---|
| 1 | `Foundation` (`Foundation.fs`) | paths, config text, read-effects, plan routing, effect tracking, model nav, YAML base helpers, base types | 28–419 | ~390 | `normalizeRoot`, `initEffects`, `*ReadEffects` (×11), `plan`, `effectKey`, `snapshot`, `appendNewEffects`, `splitFrontMatter`, `CharterFrontMatter` |
| 2 | `ParsingEarly` (`ParsingEarly.fs`) | Charter, Specification, Clarification parse/template/diagnostics | 420–1439 | ~1,020 | `parseCharterFrontMatter`, `charterTemplate`, `parseSpecificationForCommand`, `specification…Facts`, `clarificationTemplate`, `clarification…Facts` |
| 3 | `ParsingMid` (`ParsingMid.fs`) | Checklist + Plan parse/template/diagnostics | 1440–2438 | ~1,000 | `plannedChecklistReviews`, `checklistTemplate`, `checklist…Facts`, `plannedPlanEntries`, `planTemplate`, `plan…Facts` |
| 4 | `ParsingTasks` (`ParsingTasks.fs`) | Tasks parse/template/diagnostics + evidence parse helper | 2439–3130 | ~692 | `tasksSummary`, `plannedTasks`, `taskValidationDiagnostics`, `parseEvidenceForCommand`, `tasks…Facts` |
| 5 | `ViewGeneration` (`ViewGeneration.fs`) | analysis sources, generated-view state, work-model snapshots | 3131–3712 | ~582 | `analysisSources`, `analysisJson`, `generatedViewState`, `generatedViewPlan`, `workModelSnapshots`, `charterWriteEffects` |
| 6 | `Prerequisites` (`Prerequisites.fs`) | prerequisite cascade + shared handler shell | 3713–3805 | ~93 | `PrerequisiteResolution`, `resolvePrerequisites`, `runHandler` |
| 7 | `HandlersEarly` (`HandlersEarly.fs`) | Charter…Tasks handlers + work-model extractor | 3806–4041 | ~235 | `computeCharterPlan`, `computeSpecifyPlan`, `computeClarifyPlan`, `computeChecklistPlan`, `computePlanPlan`, `computeTasksPlan`, `workModelJsonFromGeneratedEffects` |
| 8 | `HandlersAnalyze` (`HandlersAnalyze.fs`) | analyze handler + its JSON/view builders | 4042–4638 | ~597 | `computeAnalyzePlan` |
| 9 | `HandlersEvidence` (`HandlersEvidence.fs`) | evidence handler + obligations/artifact text | 4639–5094 | ~456 | `computeEvidencePlan` |
| 10 | `HandlersVerify` (`HandlersVerify.fs`) | verify handler + verify JSON/views | 5095–5626 | ~532 | `computeVerifyPlan` |
| 11 | `HandlersShip` (`HandlersShip.fs`) | ship handler + ship JSON / governance handoff | 5627–5956 | ~330 | `computeShipPlan` |
| 12 | `HandlersAgents` (`HandlersAgents.fs`) | agents handler + agents config/guidance | 5957–6230 | ~274 | `computeAgentsPlan` |
| 13 | `HandlersRefresh` (`HandlersRefresh.fs`) | refresh handler — **self-contained guard, no `runHandler`** | 6231–6664 | ~432 | `computeRefreshPlan` |

All thirteen are ≤ ~1,020 LOC — comfortably under the ~1,500 cap (SC-001).

> **Source lines 1–27** (the monolith's `namespace`/`open`s and the seven
> artifact-alias headers) are **not migrated** to any single file: they are
> recreated per-file by the T006 header convention (`namespace
> FS.GG.SDD.Commands.Internal`, `[<AutoOpen>] module internal <Name>`, plus the
> `module X = FS.GG.SDD.Artifacts.Y` aliases each module needs). The 28–6814
> ranges above therefore cover every *migrated* binding with no gap or overlap.

## Dependency edges (compile order = top to bottom)

```text
Foundation
   ▼ (paths, plan routing, effect/model helpers, YAML base)
ParsingEarly ─▶ ParsingMid ─▶ ParsingTasks
   ▼ (per-artifact facts feed the next stage and the view layer)
ViewGeneration   (generatedViewState/Plan, workModelSnapshots — shared by all handlers)
   ▼
Prerequisites    (resolvePrerequisites cascade + runHandler shell)
   ▼
HandlersEarly ─ HandlersAnalyze ─ HandlersEvidence ─ HandlersVerify ─ HandlersShip ─ HandlersAgents ─ HandlersRefresh
   ▼ (each handler consumes parsing facts, view generation, prerequisites)
CommandWorkflow (facade: nextLifecycleEffects → init/update)
```

No edge points upward; no cycle. The handler files are mutually independent
(each owns one `compute*Plan`) and may be reordered among themselves, but the
listed order matches the monolith and is retained.

## Invariants the model must hold (cross-ref FRs)

- **I-1** (FR-002): `CommandWorkflow.fsi` content is unchanged — the facade is the
  only file paired with it, and it still declares exactly `init`/`update`.
- **I-2** (FR-003/FR-006): every binding moves verbatim; no reordering *within* a
  section that would change evaluation; `computeRefreshPlan` keeps its own guard.
- **I-3** (FR-001/FR-005): each file is named for and contains one concern (or one
  handler family); any `compute*Plan` is locatable by file name (SC-005).
- **I-4** (FR-004): no file > ~1,500 lines.
- **I-5** (FR-008): no new project reference; layering and compile order valid.
