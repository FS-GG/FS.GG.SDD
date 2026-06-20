# Implementation Plan: Verify Command

**Branch**: `012-verify-command` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/012-verify-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/012-verify-command`.

## Implementation Status (2026-06-20)

✅ **Implemented and green.** `fsgg-sdd verify` is wired end-to-end through the
artifact model, command workflow, serialization/rendering, and the CLI host.

- 🟢 `dotnet build FS.GG.SDD.sln -c Release` — succeeds
- 🟢 `dotnet test FS.GG.SDD.sln -c Release` — **235** passing (66 artifacts + 169
  commands), including 17 verify command tests, 3 verification-view tests, and the
  verify CLI JSON/dry-run/text smoke tests
- 🟢 `dotnet fsi scripts/prelude.fsx` — exercises the public verify surface
- 📁 Evidence under `specs/012-verify-command/readiness/`

Task-level progress (67 ✅ done / 15 🟡 partial / 0 ⬜ pending) is tracked in
[tasks.md](tasks.md). The 🟡 items are follow-up test placements and the full
blocked-fixture matrix; the verify behavior they describe is implemented and
covered by consolidated tests.

## Summary

Implement `fsgg-sdd verify` as the next SDD-owned lifecycle command after
`evidence`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host to load one evidence-ready work item; read the current
specification, clarification, checklist, plan, tasks, analysis, work-model, and
evidence sources; evaluate SDD-owned verification readiness over the task graph,
required evidence obligations, required test obligations, required skill
visibility, and generated-view currency; generate or refresh the selected work
item's `readiness/<id>/verify.json` view; refresh or diagnose the prerequisite
work-model and analysis generated views; emit deterministic JSON/text reports;
and point verification-ready work to `ship` without requiring or implementing
Governance effective-evidence freshness, routing, profiles, gates, audits,
release policy, or protected-boundary enforcement.

The verify command is non-destructive for authored lifecycle sources. Unlike
`evidence`, it authors no source artifact: it only generates the
`readiness/<id>/verify.json` view and refreshes prerequisite generated views
when source data is valid and the run is not dry-run. Verification readiness is
a disposition over task, evidence, required-test, and required-skill obligations
derived from lifecycle rules, planning decisions, task metadata, changed
artifact impact, accepted deferrals, and generated-view impacts. The command
never rewrites authored specifications, plans, tasks, or evidence declarations;
it inspects authored intent and refreshes generated readiness facts. Ship
readiness, Governance effective-evidence freshness, route selection, profile
adjustment, gate selection, protected-boundary enforcement, audit verdicts,
release gating, and generated agent guidance are out of scope for this slice.

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
`work/<id>/plan.md`, `work/<id>/tasks.yml`, `work/<id>/evidence.yml`,
`readiness/<id>/analysis.json`, the selected `readiness/<id>/work-model.json`
state, and existing `readiness/<id>/verify.json` when present; no authored
writes are planned; generated writes target `readiness/<id>/verify.json` and
target `readiness/<id>/work-model.json` when verification facts make the
normalized work-model refresh valid

**Schema/Migration**: `verify.json` and verify command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future, unsupported,
or deprecated generated verification view schema versions are reported as
generated-view diagnostics until a later feature defines an explicit migration
path. Breaking schema changes require updated contracts, fixtures, surface
baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused verification view artifact-model tests; command workflow tests;
generate/rerun/refresh tests; authored-source preservation tests; blocked
readiness disposition tests for missing/stale evidence, missing required test,
missing required skill, invalid task graph, unknown reference, undisclosed
synthetic evidence, invalid deferral, malformed and stale generated views;
deterministic JSON/report tests; text projection tests; no-Governance tests;
CLI JSON/dry-run/text smoke tests; FSI or prelude evidence for the public
verification surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Generate, refresh, or rerun the `verify-create`,
`verify-rerun-current`, and `verify-refreshes-work-model` fixture scenarios in
under 2 seconds each when run through the command test harness on the local
development machine; produce byte-identical JSON reports for three identical
dry-run verify executions over the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation changes;
Markdown and YAML remain authoring surfaces while schema-versioned structured
artifacts remain the machine contract; `verify.json` is generated readiness
data, not authored lifecycle source, and its presence is not proof of currency;
the verify command does not implement ship readiness, Governance
effective-evidence freshness, route selection, profile selection, gate
enforcement, audit, release behavior, or generated agent guidance; reports
exclude implicit clocks, durations, terminal width, ANSI styling, directory
enumeration order, host-specific path separators, random values, and absolute
host paths; dry-run reports proposed generated changes without mutating files;
optional Governance pointers stay advisory compatibility facts

**Scale/Scope**: One initialized SDD project and one selected evidence-ready
work item per command invocation; one new lifecycle command (`verify`);
generated verification view create/refresh, task/evidence/test/skill obligation
disposition, work-model and analysis currency handling, deterministic output,
fixtures, dry-run behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Verify must update `.fsi` signatures for the verification view facts, verification summary, finding/disposition/diagnostic contracts, workflow/report/effect contracts, and command-union additions before `.fs` behavior, and must exercise the public verification surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares the generated `verify.json` view, schema version 1, source relationships, task/evidence/test/skill dispositions, generated-view currency, command report JSON, stale behavior, and diagnostics; authored Markdown/YAML/evidence sources remain authored facts and are never rewritten. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Verify behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure verification disposition evaluation before any generated filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing verification semantic tests, real filesystem smoke paths, golden JSON/report tests, authored-source preservation tests, blocked readiness diagnostics, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports and the normalized work-model/verification facts are the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing evidence-ready prerequisites, malformed prerequisites, failed analysis/tasks, unknown references, dependency cycles, unsupported task states, missing required skills, missing required tests, stale/missing evidence, undisclosed synthetic evidence, invalid deferrals, malformed/stale verification or prerequisite generated views, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/012-verify-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- verify-view.md
|   |-- verify-command.md
|   |-- verify-report-json.md
|   `-- verify-fixtures.md
`-- tasks.md                 # Created by /speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln
Directory.Build.props
Directory.Packages.props

src/
|-- FS.GG.SDD.Artifacts/
|   |-- existing lifecycle artifact, identifier, diagnostic, work-model,
|   |   analysis, evidence, generation manifest, and serialization contracts
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
|   |-- VerificationViewTests.fs
|   |-- EvidenceArtifactTests.fs
|   |-- AnalysisViewTests.fs
|   |-- GeneratedModelCurrencyTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- VerifyCommandTests.fs
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
        |-- verify-create/
        |-- verify-rerun-current/
        |-- verify-preserves-authored/
        |-- verify-refreshes-work-model/
        |-- verify-refreshes-analysis/
        |-- verify-accepted-deferral/
        |-- outside-project/
        |-- missing-specification/
        |-- missing-clarification/
        |-- missing-checklist/
        |-- missing-plan/
        |-- missing-tasks/
        |-- missing-analysis/
        |-- missing-evidence/
        |-- failed-analysis/
        |-- failed-tasks/
        |-- malformed-work-id/
        |-- malformed-verify-view/
        |-- duplicate-work-id/
        |-- unknown-source-reference/
        |-- dependency-cycle/
        |-- unsupported-task-status/
        |-- missing-required-skill/
        |-- missing-required-test/
        |-- missing-required-evidence/
        |-- stale-analysis/
        |-- stale-tasks/
        |-- stale-evidence/
        |-- undisclosed-synthetic-evidence/
        |-- invalid-deferral/
        |-- stale-generated-view/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects rather than adding new
projects. `verify` has the same operational shape as the preceding generated-view
lifecycle command `analyze`: load project context, validate one work item,
inspect authored and generated source artifacts, evaluate SDD-owned readiness,
refresh or diagnose generated views, and emit one deterministic report. Unlike
`evidence`, `verify` authors no source; its only writes are generated readiness
views.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/verify-view.md](contracts/verify-view.md)
- [contracts/verify-command.md](contracts/verify-command.md)
- [contracts/verify-report-json.md](contracts/verify-report-json.md)
- [contracts/verify-fixtures.md](contracts/verify-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: verification additions are planned through public `.fsi` artifact, work-model, command type/report/effect, diagnostic, serialization, and rendering contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: the generated `verify.json` view, verification findings, task/evidence/test/skill dispositions, generated-view currency, and command report JSON are structured contracts; authored Markdown/YAML/evidence sources remain authored sources and are never rewritten; schema version 1 has diagnose-only migration posture. |
| Public API baseline | PASS: any verification view, command type/report/effect, generated-view, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, verification disposition evaluation, generated-view refresh planning, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define generated verification view, rerun currency, work-model/analysis refresh, authored-source preservation, dry-run, stale/missing source, missing prerequisite, missing required skill/test, invalid task graph, unknown reference, undisclosed synthetic evidence, invalid deferral, malformed/stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Safe failure | PASS: diagnostics identify the affected artifact, stable id, severity, correction, and next action before generated writes proceed; authored sources are never mutated. |

No new complexity exceptions were introduced by Phase 1 design.
