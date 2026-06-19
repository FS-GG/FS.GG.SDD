# Implementation Plan: Tasks Command

**Branch**: `009-tasks-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/009-tasks-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/009-tasks-command`.

## Summary

Implement `fsgg-sdd tasks` as the next native SDD lifecycle command after
`plan`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
planned work item, reads `work/<id>/spec.md`,
`work/<id>/clarifications.md`, `work/<id>/checklist.md`, and
`work/<id>/plan.md`, creates or safely updates `work/<id>/tasks.yml`,
preserves existing task ids and task state, detects stale source links,
validates dependency and reference integrity, refreshes or diagnoses the
generated work-model view, emits deterministic JSON/text reports, and points
successful task-ready results to `analyze` without requiring the Governance
runtime.

The native SDD task artifact is `work/<id>/tasks.yml`. This is distinct from
`specs/009-tasks-command/tasks.md`, which will be the Spec Kit implementation
task list generated later by `$speckit-tasks`.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for artifact, command, report, fixture, CLI smoke, and public-surface
tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; prerequisite reads target `work/<id>/spec.md`,
`work/<id>/clarifications.md`, `work/<id>/checklist.md`, and
`work/<id>/plan.md`; task writes target `work/<id>/tasks.yml`; generated
work-model refresh targets `readiness/<id>/work-model.json` when valid source
data exists

**Schema/Migration**: `tasks.yml` and task command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future,
unsupported, or deprecated task schema versions block unsafe writes with
actionable diagnostics until a later feature defines an explicit migration
path. Breaking schema changes require updated contracts, fixtures, surface
baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused task artifact tests; command workflow tests; safe-rerun and
stable-id preservation tests; stale-source tests; missing prerequisite tests;
dependency-cycle and unknown-reference tests; completed-task evidence tests;
dry-run mutation tests; generated-view state tests; deterministic command
report JSON tests; text projection tests; no-Governance boundary tests;
temporary-directory CLI smoke tests; FSI/prelude evidence for the public
command surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `tasks-create` and
`tasks-rerun-preserves-status` fixture scenarios in under 2 seconds each when
run through the command test harness on the local development machine; produce
byte-identical JSON reports for three identical dry-run task executions over
the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface for spec, clarification,
checklist, and plan artifacts; `tasks.yml` is the structured authored source
and machine contract for task graph state; generated views remain outputs;
source snapshots and task source links drive stale detection; reports exclude
implicit clocks, durations, terminal width, ANSI styling, directory
enumeration order, host-specific path separators, random values, and absolute
host paths; existing authored task ids, statuses, ownership, dependencies,
required skills, required evidence obligations, skip rationales, and notes are
preserved unless the update is proven safe; unsafe task changes block before
filesystem mutation; failed planning prerequisites do not advance to analyze;
dry-run reports planned changes without mutating authored or generated
artifacts; Governance pointers stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected planned work item
per command invocation; one new lifecycle command (`tasks`); task artifact
creation/update, task graph validation, stale task/source state,
generated-work-model refresh or diagnostic, deterministic output, fixtures,
dry-run behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for task artifact facts, task summaries, task diagnostics, workflow/report/effect contracts, and parser additions before `.fs` behavior, and must exercise the public tasks surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored `tasks.yml`, schema version 1, typed task ids, source snapshots, dependency graph rules, command report JSON, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Task behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure graph validation before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing task semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and stale-source tests, dependency-cycle tests, completed-task evidence tests, missing-prerequisite tests, dry-run tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing specification, clarification, checklist, or plan prerequisites, failed planning state, malformed tasks, duplicate task ids, dependency cycles, unknown source references, task identity mismatch, unsafe overwrite, missing evidence for completed tasks, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/009-tasks-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- tasks-artifact.md
|   |-- tasks-command.md
|   |-- tasks-report-json.md
|   `-- tasks-fixtures.md
`-- tasks.md                 # Created by $speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln
Directory.Build.props
Directory.Packages.props

src/
|-- FS.GG.SDD.Artifacts/
|   |-- existing lifecycle artifact, identifier, diagnostic, work-model,
|   |   generation manifest, and serialization contracts
|   |-- Identifiers.fsi
|   |-- Identifiers.fs
|   |-- LifecycleArtifacts.fsi
|   `-- LifecycleArtifacts.fs
|-- FS.GG.SDD.Commands/
|   |-- FS.GG.SDD.Commands.fsproj
|   |-- CommandTypes.fsi
|   |-- CommandTypes.fs
|   |-- CommandReports.fsi
|   |-- CommandReports.fs
|   |-- CommandWorkflow.fsi
|   |-- CommandWorkflow.fs
|   |-- CommandEffects.fsi
|   |-- CommandEffects.fs
|   |-- CommandSerialization.fsi
|   |-- CommandSerialization.fs
|   |-- CommandRendering.fsi
|   `-- CommandRendering.fs
`-- FS.GG.SDD.Cli/
    |-- FS.GG.SDD.Cli.fsproj
    `-- Program.fs

scripts/
`-- prelude.fsx

tests/
|-- FS.GG.SDD.Artifacts.Tests/
|   |-- IdentifierTests.fs
|   |-- SchemaContractTests.fs
|   |-- NormalizedWorkModelTests.fs
|   |-- SpecificationArtifactTests.fs
|   |-- ClarificationArtifactTests.fs
|   |-- ChecklistArtifactTests.fs
|   |-- PlanArtifactTests.fs
|   |-- TasksArtifactTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- CharterCommandTests.fs
|   |-- SpecifyCommandTests.fs
|   |-- ClarifyCommandTests.fs
|   |-- ChecklistCommandTests.fs
|   |-- PlanCommandTests.fs
|   |-- TasksCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- tasks-create/
        |-- tasks-rerun-preserves-status/
        |-- tasks-adds-missing-items/
        |-- tasks-preserves-stable-ids/
        |-- tasks-records-required-skills/
        |-- tasks-records-evidence-obligations/
        |-- tasks-accepted-deferral/
        |-- outside-project/
        |-- missing-specification/
        |-- missing-clarification/
        |-- missing-checklist/
        |-- missing-plan/
        |-- failed-plan/
        |-- malformed-work-id/
        |-- malformed-tasks/
        |-- duplicate-work-id/
        |-- duplicate-task-id/
        |-- unknown-source-reference/
        |-- dependency-cycle/
        |-- tasks-identity-mismatch/
        |-- unsafe-overwrite/
        |-- done-task-missing-evidence/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        |-- stale-generated-view/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Tasks`; this feature removes the unsupported-command path
for that one command and wires tasks through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init`,
`charter`, `specify`, `clarify`, `checklist`, and `plan`.

The feature may add narrow artifact-model types and parser helpers for
`tasks.yml` root metadata, source snapshots, task entries, task dependencies,
task dispositions, required skills, required evidence obligations, stale task
state, graph readiness, and task findings because later analyze, evidence,
verify, and ship stages need those facts from the structured contract. It must
not introduce `analyze`, evidence update, verify, ship, release, generated
agent guidance, route selection, freshness, profile, gate, or Governance
enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/tasks-artifact.md](contracts/tasks-artifact.md)
- [contracts/tasks-command.md](contracts/tasks-command.md)
- [contracts/tasks-report-json.md](contracts/tasks-report-json.md)
- [contracts/tasks-fixtures.md](contracts/tasks-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: task additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: `tasks.yml`, typed task ids, source snapshots, dependency graph state, required skills/evidence, and command report JSON are structured contracts; prerequisite Markdown files remain authored prose; schema version 1 has diagnose-only migration posture; generated views report source and currency state. |
| Public API baseline | PASS: any identifier, lifecycle artifact, command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, task graph evaluation, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, stable id preservation, status preservation, required skill/evidence capture, accepted deferrals, stale source state, missing prerequisites, malformed task data, dependency cycles, completed-task evidence defects, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Safe failure | PASS: diagnostics identify the affected artifact, stable id, severity, correction, and next action before unsafe writes or analyze advancement. |

No new complexity exceptions were introduced by Phase 1 design.
