# Implementation Plan: Plan Command

**Branch**: `008-plan-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-plan-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/008-plan-command`.

## Summary

Implement `fsgg-sdd plan` as the next native SDD lifecycle command after
`checklist`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
checklist-ready work item, reads `work/<id>/spec.md`,
`work/<id>/clarifications.md`, and `work/<id>/checklist.md` for structured
lifecycle facts, creates or safely updates `work/<id>/plan.md`, preserves
existing plan decision and obligation ids, detects stale plan decisions when
source facts change, reports planned or still-blocked lifecycle state, points
successful planned results to `tasks`, and emits deterministic command reports
and text projections without requiring the Governance runtime.

The plan artifact is an authored Markdown source with structured front matter,
stable plan decision ids, stable contract reference ids, stable verification
obligation ids, source links, and source snapshot facts. Plan decisions,
contract references, verification obligations, migration notes, generated-view
impact, accepted deferrals, stale decisions, and advisory notes are durable
readiness facts used by later task, evidence, generated-view, analyze, verify,
and ship stages. Command report JSON remains the immediate automation contract
for plan creation, reruns, dry-runs, blocked results, generated-view currency,
diagnostics, and optional Governance compatibility facts.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for command, report, fixture, and public-surface tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; specification prerequisite reads target
`work/<id>/spec.md`; clarification prerequisite reads target
`work/<id>/clarifications.md`; checklist prerequisite reads target
`work/<id>/checklist.md`; plan writes target `work/<id>/plan.md`; generated
work-model refresh targets `readiness/<id>/work-model.json` when valid source
data exists

**Schema/Migration**: Plan front matter and plan command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future,
unsupported, or deprecated plan schema versions block unsafe writes with
actionable diagnostics until a later feature defines an explicit migration
path. Breaking schema changes require updated contracts, fixtures, surface
baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline tests;
focused plan workflow tests; temporary-directory CLI smoke tests; safe-rerun
and stale-decision tests; failed checklist prerequisite tests; accepted-deferral
visibility tests; dry-run mutation tests; generated-view state tests;
deterministic command-report JSON tests; text projection tests; no-Governance
boundary tests; FSI/prelude evidence for the public command surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `plan-create` and
`plan-rerun-preserves-decisions` fixture scenarios in under 2 seconds each when
run through the command test harness on the local development machine; produce
byte-identical JSON reports for three identical dry-run plan executions over
the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface; structured front matter,
typed ids, source snapshot facts, and command report JSON are the machine
contracts; generated views require source digest and generator-version currency
checks; reports exclude implicit clocks, durations, terminal width, ANSI
styling, directory enumeration order, host-specific path separators, random
values, and absolute host paths; existing authored plan content and stable ids
are preserved unless the update is proven safe; unsafe plan decision changes
block before filesystem mutation; failed checklist prerequisites do not
advance to tasks; dry-run reports planned changes without mutating authored or
generated artifacts; Governance pointers stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected checklist-ready
work item per command invocation; one new lifecycle command (`plan`); plan
artifact creation/update, planning decision state, generated work-model refresh
or diagnostic, deterministic output, fixtures, dry-run behavior, and optional
Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for plan decision/reference/obligation ids, plan facts, command workflow/report/effect contracts, and parser additions before `.fs` behavior and must exercise the public plan surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored plan Markdown, structured front matter, typed plan ids, source snapshots, command report JSON, schema migration posture, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Plan behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure planning before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing plan semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and stale-decision tests, failed-prerequisite tests, accepted-deferral tests, dry-run tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing specification, clarification, or checklist prerequisites, failed checklist results, unknown references, malformed plan data, duplicate ids, unsafe decision changes, stale source snapshots, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/008-plan-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- plan-artifact.md
|   |-- plan-command.md
|   |-- plan-report-json.md
|   `-- plan-fixtures.md
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
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- plan-create/
        |-- plan-rerun-preserves-decisions/
        |-- plan-adds-missing-entries/
        |-- plan-preserves-stable-ids/
        |-- plan-accepted-deferral/
        |-- plan-stale-decision/
        |-- outside-project/
        |-- missing-specification/
        |-- missing-clarification/
        |-- failed-checklist/
        |-- missing-checklist/
        |-- malformed-work-id/
        |-- malformed-plan/
        |-- duplicate-work-id/
        |-- duplicate-plan-id/
        |-- unknown-source-reference/
        |-- plan-identity-mismatch/
        |-- unsafe-overwrite/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        |-- stale-generated-view/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Plan`; this feature removes the unsupported-command path for
that one command and wires plan through the same request, effect, interpreter,
report, serializer, and renderer contracts used by `init`, `charter`,
`specify`, `clarify`, and `checklist`.

The feature may add narrow artifact-model types and parser helpers for plan
front matter, plan decision ids, contract reference ids, verification
obligation ids, migration notes, generated-view impact, source snapshots,
accepted deferrals, blocking findings, and stale decision state because later
lifecycle stages need those facts from the structured contract. It must not
introduce `tasks`, `analyze`, evidence update, verify, ship, release,
generated agent guidance, route selection, freshness, profile, gate, or
Governance enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/plan-artifact.md](contracts/plan-artifact.md)
- [contracts/plan-command.md](contracts/plan-command.md)
- [contracts/plan-report-json.md](contracts/plan-report-json.md)
- [contracts/plan-fixtures.md](contracts/plan-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: plan additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: plan front matter, typed plan decision/reference/obligation ids, source snapshot facts, and command report JSON are structured contracts; Markdown body remains authored prose; schema version 1 has diagnose-only migration posture; generated views report source and currency state. |
| Public API baseline | PASS: any identifier, lifecycle artifact, command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, plan evaluation, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, stable id preservation, accepted deferrals, stale source/decision state, failed checklist prerequisites, missing prerequisites, malformed plan, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Governance boundary | PASS: plan reports may expose optional Governance pointers but do not parse Governance schemas, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
