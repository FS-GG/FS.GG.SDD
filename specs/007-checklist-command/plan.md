# Implementation Plan: Checklist Command

**Branch**: `007-checklist-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-checklist-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/007-checklist-command`.

## Summary

Implement `fsgg-sdd checklist` as the next native SDD lifecycle command after
`clarify`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
clarified work item, reads `work/<id>/spec.md` and
`work/<id>/clarifications.md` for structured lifecycle facts, creates or safely
updates `work/<id>/checklist.md`, preserves existing checklist item and review
result ids, detects stale review results when source facts change, reports
checklist-ready or still-blocked lifecycle state, points successful ready
results to `plan`, and emits deterministic command reports and text
projections without requiring the Governance runtime.

The checklist artifact is an authored Markdown source with structured front
matter, stable checklist item ids, stable result ids, source links, and source
snapshot facts. Passed checks, failed blocking checks, accepted deferrals,
stale review results, and advisory notes are durable readiness facts used by
later plan, task, evidence, generated-view, verify, and ship stages. Command
report JSON remains the immediate automation contract for checklist creation,
reruns, dry-runs, failed quality results, blocked results, generated-view
currency, diagnostics, and optional Governance compatibility facts.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for command, report, fixture, and public-surface tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; specification prerequisite reads target
`work/<id>/spec.md`; clarification prerequisite reads target
`work/<id>/clarifications.md`; checklist writes target
`work/<id>/checklist.md`; generated-view refresh targets
`readiness/<id>/work-model.json` when valid source data exists

**Schema/Migration**: Checklist front matter and checklist command report JSON
use `schemaVersion: 1` for this feature. The migration posture is
diagnose-only: current schema version 1 is accepted; missing, malformed,
future, unsupported, or deprecated checklist schema versions block unsafe
writes with actionable diagnostics until a later feature defines an explicit
migration path. Breaking schema changes require updated contracts, fixtures,
surface baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline tests;
focused checklist workflow tests; temporary-directory CLI smoke tests;
safe-rerun and stale-result tests; failed requirements-quality tests;
accepted-deferral tests; dry-run mutation tests; generated-view state tests;
deterministic command-report JSON tests; text projection tests; no-Governance
boundary tests; FSI/prelude evidence for the public command surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `checklist-create` and
`checklist-rerun-preserves-results` fixture scenarios in under 2 seconds each
when run through the command test harness on the local development machine;
produce byte-identical JSON reports for three identical dry-run checklist
executions over the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface; structured front matter,
typed ids, source snapshot facts, and command report JSON are the machine
contracts; generated views require source digest and generator-version currency
checks; reports exclude implicit clocks, durations, terminal width, ANSI
styling, directory enumeration order, host-specific path separators, random
values, and absolute host paths; existing authored checklist content and stable
ids are preserved unless the update is proven safe; unsafe review result
changes block before filesystem mutation; failed requirements-quality checks
write safe checklist findings but do not advance to planning; dry-run reports
planned changes without mutating authored or generated artifacts; Governance
pointers stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected clarified work
item per command invocation; one new lifecycle command (`checklist`);
checklist artifact creation/update, requirements-quality review state,
generated work-model refresh or diagnostic, deterministic output, fixtures,
dry-run behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for checklist item/result ids, checklist facts, command workflow/report/effect contracts, and parser additions before `.fs` behavior and must exercise the public checklist surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored checklist Markdown, structured front matter, typed item/result ids, source snapshots, command report JSON, schema migration posture, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Checklist behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure planning before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing checklist semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and stale-result tests, failed-quality tests, accepted-deferral tests, dry-run tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing specification or clarification prerequisites, unresolved ambiguity, failed requirements quality, unknown references, malformed checklist data, duplicate ids, unsafe result changes, stale source snapshots, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/007-checklist-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- checklist-artifact.md
|   |-- checklist-command.md
|   |-- checklist-report-json.md
|   `-- checklist-fixtures.md
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
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- CharterCommandTests.fs
|   |-- SpecifyCommandTests.fs
|   |-- ClarifyCommandTests.fs
|   |-- ChecklistCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- checklist-create/
        |-- checklist-rerun-preserves-results/
        |-- checklist-adds-missing-items/
        |-- checklist-preserves-stable-ids/
        |-- checklist-accepted-deferral/
        |-- checklist-stale-result/
        |-- failed-requirements-quality/
        |-- unresolved-ambiguity/
        |-- missing-clarification/
        |-- malformed-checklist/
        |-- duplicate-checklist-id/
        |-- unknown-source-reference/
        |-- checklist-identity-mismatch/
        |-- unsafe-checklist-result-change/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        |-- stale-generated-view/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Checklist`; this feature removes the unsupported-command path
for that one command and wires checklist through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init`,
`charter`, `specify`, and `clarify`.

The feature may add narrow artifact-model types and parser helpers for
checklist front matter, checklist items, checklist results, accepted deferrals,
source snapshots, blocking findings, and stale review state because later
lifecycle stages need those facts from the structured contract. It must not
introduce `plan`, `tasks`, `analyze`, task/evidence update, verify, ship,
release, generated agent guidance, route selection, freshness, profile, gate,
or Governance enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/checklist-artifact.md](contracts/checklist-artifact.md)
- [contracts/checklist-command.md](contracts/checklist-command.md)
- [contracts/checklist-report-json.md](contracts/checklist-report-json.md)
- [contracts/checklist-fixtures.md](contracts/checklist-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: checklist additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: checklist front matter, typed checklist item/result ids, source snapshot facts, and command report JSON are structured contracts; Markdown body remains authored prose; schema version 1 has diagnose-only migration posture; generated views report source and currency state. |
| Public API baseline | PASS: any identifier, lifecycle artifact, command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, checklist evaluation, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, stable id preservation, accepted deferrals, stale source/result state, failed requirements quality, missing prerequisites, malformed checklist, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Governance boundary | PASS: checklist reports may expose optional Governance pointers but do not parse Governance schemas, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
