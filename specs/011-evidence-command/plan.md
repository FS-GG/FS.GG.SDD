# Implementation Plan: Evidence Command

**Branch**: `011-evidence-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-evidence-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/011-evidence-command`.

## Summary

Implement `fsgg-sdd evidence` as the next SDD-owned lifecycle command after
`analyze` and implementation work. The feature extends the existing
`FS.GG.SDD.Commands` workflow and thin CLI host to load one analyzed work item,
read the current specification, clarification, checklist, plan, tasks,
analysis, work-model, and evidence sources, create or safely update the
authored evidence declaration artifact at `work/<id>/evidence.yml`, map task
and verification obligations to evidence dispositions, refresh or diagnose the
generated work-model state, emit deterministic JSON/text reports, and point
evidence-ready work to `verify` without requiring or implementing Governance
freshness, routing, profiles, gates, audits, or enforcement.

The evidence command is non-destructive for existing authored lifecycle
sources. It may create or append compatible evidence declarations, preserve
existing evidence ids and rationale, refuse unsafe evidence rewrites, and write
generated SDD views only when source data is valid and the run is not dry-run.
Semantic replacement of existing evidence declarations is out of scope for this
slice; changed declaration meaning, source references, synthetic disclosures, or
deferral rationales are refused until a later feature defines a replacement
contract.

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
`work/<id>/plan.md`, `work/<id>/tasks.yml`,
`readiness/<id>/analysis.json`, the selected `readiness/<id>/work-model.json`
state, and existing `work/<id>/evidence.yml` when present; authored writes
target `work/<id>/evidence.yml`; generated writes target
`readiness/<id>/work-model.json` when evidence facts make the normalized work
model refresh valid

**Schema/Migration**: `evidence.yml` and evidence command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future,
unsupported, or deprecated authored evidence schema versions are reported as
evidence diagnostics until a later feature defines an explicit migration path.
Breaking schema changes require updated contracts, fixtures, surface
baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused evidence artifact parser/model tests; command workflow tests;
safe create/update/refusal tests; stale and missing evidence disposition
tests; generated-view currency tests; deterministic JSON/report tests; text
projection tests; no-Governance tests; CLI JSON/dry-run/text smoke tests; FSI
or prelude evidence for the public evidence surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create, compatible-update, or rerun the
`evidence-create`, `evidence-compatible-update`, and `evidence-rerun-current`
fixture scenarios in under 2 seconds each when run through the command test
harness on the local development machine; produce byte-identical JSON reports
for three identical dry-run evidence executions over the deterministic-report
fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown and YAML remain authoring surfaces while schema-versioned
structured artifacts remain the machine contract; `evidence.yml` is authored
SDD lifecycle data, not generated readiness data; the evidence command does
not implement verify readiness, ship readiness, Governance effective-evidence
freshness, route selection, profile selection, gate enforcement, audit, or
release behavior; reports exclude implicit clocks, durations, terminal width,
ANSI styling, directory enumeration order, host-specific path separators,
random values, and absolute host paths; dry-run reports proposed authored and
generated changes without mutating files; optional Governance pointers stay
advisory compatibility facts

**Scale/Scope**: One initialized SDD project and one selected analyzed work
item per command invocation; one new lifecycle command (`evidence`); authored
evidence declaration create/update/refusal, evidence obligation disposition,
work-model currency handling, deterministic output, fixtures, dry-run
behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Evidence must update `.fsi` signatures for evidence artifact facts, evidence summaries, disposition/diagnostic contracts, workflow/report/effect contracts, and parser additions before `.fs` behavior, and must exercise the public evidence surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored `evidence.yml`, schema version 1, source relationships, evidence declarations, obligation dispositions, generated-view currency, command report JSON, stale behavior, and diagnostics; Markdown/YAML sources remain authored facts. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Evidence behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure evidence disposition evaluation before authored or generated filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing evidence semantic tests, real filesystem smoke paths, golden JSON/report tests, safe preservation tests, blocked evidence diagnostics, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports, authored `evidence.yml`, and normalized work-model facts are the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing analyzed prerequisite, malformed evidence, duplicate evidence ids, unknown task/source references, missing obligations, stale evidence, undisclosed synthetic evidence, missing deferral rationale, unsafe updates, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/011-evidence-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- evidence-artifact.md
|   |-- evidence-command.md
|   |-- evidence-report-json.md
|   `-- evidence-fixtures.md
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
|   |   analysis, generation manifest, and serialization contracts
|   |-- LifecycleArtifacts.fsi
|   |-- LifecycleArtifacts.fs
|   |-- WorkModel.fsi
|   `-- WorkModel.fs
|-- FS.GG.SDD.Commands/
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
|   |-- EvidenceArtifactTests.fs
|   |-- AnalysisViewTests.fs
|   |-- GeneratedModelCurrencyTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- EvidenceCommandTests.fs
|   |-- AnalyzeCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- evidence-create/
        |-- evidence-rerun-current/
        |-- evidence-preserves-existing/
        |-- evidence-compatible-update/
        |-- evidence-refreshes-work-model/
        |-- evidence-accepted-deferral/
        |-- evidence-synthetic-disclosed/
        |-- outside-project/
        |-- missing-analysis/
        |-- missing-tasks/
        |-- missing-required-evidence/
        |-- malformed-evidence/
        |-- duplicate-evidence-id/
        |-- unknown-evidence-reference/
        |-- stale-evidence/
        |-- undisclosed-synthetic-evidence/
        |-- missing-deferral-rationale/
        |-- unsafe-evidence-update/
        |-- stale-generated-view/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects rather than adding new
projects. `evidence` has the same operational shape as preceding lifecycle
commands: load project context, validate one work item, inspect authored and
generated source artifacts, plan safe writes, refresh or diagnose generated
views, and emit one deterministic report.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/evidence-artifact.md](contracts/evidence-artifact.md)
- [contracts/evidence-command.md](contracts/evidence-command.md)
- [contracts/evidence-report-json.md](contracts/evidence-report-json.md)
- [contracts/evidence-fixtures.md](contracts/evidence-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: evidence additions are planned through public `.fsi` artifact, work-model, command type/report/effect, diagnostic, serialization, and rendering contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: authored `evidence.yml`, evidence declarations, source relationships, obligation dispositions, generated-view currency, and command report JSON are structured contracts; prerequisite Markdown/YAML files remain authored sources; schema version 1 has diagnose-only migration posture. |
| Public API baseline | PASS: any evidence artifact, command type/report/effect, generated-view, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, evidence disposition evaluation, safe write planning, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define valid declaration, compatible update, safe refusal, authored-source preservation, dry-run, stale source, missing prerequisite, malformed evidence, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Safe failure | PASS: diagnostics identify the affected artifact, stable id, severity, correction, and next action before authored or generated writes proceed. |

No new complexity exceptions were introduced by Phase 1 design.
