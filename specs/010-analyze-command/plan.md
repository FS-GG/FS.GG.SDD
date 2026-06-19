# Implementation Plan: Analyze Command

**Branch**: `010-analyze-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/010-analyze-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/010-analyze-command`.

## Summary

Implement `fsgg-sdd analyze` as the next native SDD lifecycle command after
`tasks`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
tasks-ready work item, reads `work/<id>/spec.md`,
`work/<id>/clarifications.md`, `work/<id>/checklist.md`,
`work/<id>/plan.md`, and `work/<id>/tasks.yml`, refreshes or diagnoses the
generated work-model view, creates or safely refreshes
`readiness/<id>/analysis.json`, emits deterministic JSON/text reports, and
points implementation-ready results to implementation without requiring the
Governance runtime.

The analyze command is non-destructive for authored lifecycle sources.
Authored specifications, clarifications, checklists, plans, and task graphs are
input sources only. The command may write generated readiness views when source
data is valid and the run is not dry-run.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for artifact, command, report, fixture, CLI smoke, and public-surface
tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; prerequisite reads target `work/<id>/spec.md`,
`work/<id>/clarifications.md`, `work/<id>/checklist.md`,
`work/<id>/plan.md`, `work/<id>/tasks.yml`, and the selected
`readiness/<id>/work-model.json` state; generated writes target
`readiness/<id>/work-model.json` when refresh is valid and
`readiness/<id>/analysis.json` when analysis can be generated

**Schema/Migration**: `analysis.json` and analyze command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future,
unsupported, or deprecated generated analysis schema versions are reported as
generated-view diagnostics until a later feature defines an explicit migration
path. Breaking schema changes require updated contracts, fixtures, surface
baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused analysis artifact/view tests; command workflow tests;
non-destructive authored-source tests; valid, blocked, dry-run, deterministic,
generated-view, text projection, no-Governance, and CLI smoke tests; FSI or
prelude evidence for the public analyze surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `analysis-create` and
`analysis-rerun-current` fixture scenarios in under 2 seconds each when run
through the command test harness on the local development machine; produce
byte-identical JSON reports for three identical dry-run analysis executions
over the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface for spec, clarification,
checklist, and plan artifacts; `tasks.yml` remains the structured authored
task source and machine contract; `analysis.json` is generated readiness data;
analyze does not create, update, reorder, normalize, or remove authored
lifecycle artifacts; reports exclude implicit clocks, durations, terminal
width, ANSI styling, directory enumeration order, host-specific path
separators, random values, and absolute host paths; failed analysis does not
advance to implementation readiness; dry-run reports proposed generated
changes without mutating authored or generated artifacts; Governance pointers
stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected tasks-ready work
item per command invocation; one new lifecycle command (`analyze`); generated
analysis view creation/refresh, work-model currency handling, cross-artifact
consistency findings, readiness outcome, deterministic output, fixtures,
dry-run behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Analyze must update `.fsi` signatures for analysis view facts, analysis summaries, finding/diagnostic contracts, workflow/report/effect contracts, and parser additions before `.fs` behavior, and must exercise the public analyze surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares generated `analysis.json`, schema version 1, source relationships, structured findings, generated-view currency, command report JSON, stale behavior, and diagnostics; authored Markdown/YAML sources remain the source facts. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Analyze behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure consistency evaluation before generated filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing analysis semantic tests, real filesystem smoke paths, golden JSON/report tests, non-destructive source tests, blocked consistency tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports and generated analysis JSON are the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing specification, clarification, checklist, plan, or tasks prerequisites, failed checklist/plan/tasks state, stale decisions/tasks, unknown references, dependency defects, missing dispositions, malformed generated views, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/010-analyze-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- analysis-view.md
|   |-- analyze-command.md
|   |-- analyze-report-json.md
|   `-- analysis-fixtures.md
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
|   |-- GenerationManifest.fsi
|   |-- GenerationManifest.fs
|   |-- LifecycleArtifacts.fsi
|   |-- LifecycleArtifacts.fs
|   |-- WorkModel.fsi
|   `-- WorkModel.fs
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
|   |-- GeneratedModelCurrencyTests.fs
|   |-- NormalizedWorkModelTests.fs
|   |-- TasksArtifactTests.fs
|   |-- AnalysisViewTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- CommandWorkflowTests.fs
|   |-- TasksCommandTests.fs
|   |-- AnalyzeCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- analysis-create/
        |-- analysis-rerun-current/
        |-- analysis-preserves-authored/
        |-- analysis-refreshes-work-model/
        |-- analysis-accepted-deferral/
        |-- outside-project/
        |-- missing-specification/
        |-- missing-clarification/
        |-- missing-checklist/
        |-- missing-plan/
        |-- missing-tasks/
        |-- failed-checklist/
        |-- failed-plan/
        |-- failed-tasks/
        |-- malformed-work-id/
        |-- malformed-analysis/
        |-- duplicate-work-id/
        |-- unknown-source-reference/
        |-- dependency-cycle/
        |-- stale-plan/
        |-- stale-tasks/
        |-- analysis-identity-mismatch/
        |-- done-task-missing-evidence/
        |-- stale-generated-view/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Analyze`; this feature removes the unsupported-command path
for that command and wires analysis through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init`,
`charter`, `specify`, `clarify`, `checklist`, `plan`, and `tasks`.

The feature may add narrow artifact-model types and generated-view helpers for
`analysis.json`, analysis finding ids, source relationships, lifecycle
readiness, generated-view state, and analysis diagnostics because later
implementation, evidence, verify, and ship stages need those facts from the
structured contract. It must not introduce implementation execution, evidence
update, verify, ship, release, generated agent guidance, route selection,
freshness, profile, gate, or Governance enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/analysis-view.md](contracts/analysis-view.md)
- [contracts/analyze-command.md](contracts/analyze-command.md)
- [contracts/analyze-report-json.md](contracts/analyze-report-json.md)
- [contracts/analysis-fixtures.md](contracts/analysis-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: analysis additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: generated `analysis.json`, analysis findings, source relationships, readiness state, generated-view currency, and command report JSON are structured contracts; prerequisite Markdown/YAML files remain authored sources; schema version 1 has diagnose-only migration posture. |
| Public API baseline | PASS: any analysis view, command type/report/effect, generated-view, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, consistency evaluation, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define valid generation, rerun currency, authored-source preservation, dry-run, stale source, missing prerequisite, dependency defect, generated-view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Safe failure | PASS: diagnostics identify the affected artifact, stable id, severity, correction, and next action before generated writes or implementation readiness. |

No new complexity exceptions were introduced by Phase 1 design.
