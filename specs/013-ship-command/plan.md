# Implementation Plan: Ship Command

**Branch**: `013-ship-command` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-ship-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/013-ship-command`.

## Implementation Progress

> Status as of 2026-06-20 — **🟢 COMPLETE & VERIFIED** (258/258 tests passing, clean build, FSI surface exercised).

| Phase | Scope | Status |
|---|---|---|
| Phase 1 — Setup | Test entry points + 31 ship fixture manifests | 🟩 Done |
| Phase 2 — Foundational contracts | `ShipView`/`parseShipView`, `SddCommand.Ship`, `ShipSummary`, report/model fields, ship diagnostics, FSI + baselines | 🟩 Done |
| Phase 3 — US1 ship-ready (P1, MVP) | `computeShipPlan`, `ship.json` generation, work-model refresh, `ship.next.protectedBoundary` | 🟩 Done |
| Phase 4 — US2 block lifecycle gaps (P1) | verification gate, prerequisite/generated-view/stale blocking diagnostics | 🟩 Done |
| Phase 5 — US3 preserve authored sources (P2) | non-destructive writes, dry-run, rerun `noChange` | 🟩 Done |
| Phase 6 — US4 traceable output (P3) | deterministic JSON, text projection, CLI smoke, Governance boundary | 🟩 Done |
| Phase 7 — Polish/evidence/docs | readiness evidence, traceability, README/AGENTS/CLAUDE/plan updates | 🟩 Done |

**Verification evidence** (`specs/013-ship-command/readiness/`):

- 🟩 `build-release.txt` — `dotnet build FS.GG.SDD.sln -c Release` clean (0 errors)
- 🟩 `full-suite.txt` — `dotnet test FS.GG.SDD.sln -c Release` → 258 passed / 0 failed / 0 skipped
- 🟩 `artifact-ship-tests.txt` — 4/4 `ShipViewTests`
- 🟩 `command-ship-tests.txt` — 19/19 `ShipCommandTests` (incl. 3 real CLI smoke)
- 🟩 `fsi-public-surface.txt` — prelude exercises the public ship surface (exit 0)
- 🟩 `artifact-traceability.md`, `sdd-governance-boundary.md`, `performance.md`, `human-summary-review.md`

Per-task checkboxes (all 🟩) and an honest test-consolidation note live in [tasks.md](tasks.md).

## Summary

Implement `fsgg-sdd ship` as the next SDD-owned lifecycle command after
`verify`. The feature extends the existing `FS.GG.SDD.Commands` workflow and thin
CLI host to load one verification-ready work item; read the current
specification, clarification, checklist, plan, tasks, analysis, work-model,
evidence, and verification sources; aggregate SDD-owned merge-boundary readiness
over lifecycle stage readiness, verification readiness, evidence dispositions,
and generated-view currency; generate or refresh the selected work item's
`readiness/<id>/ship.json` view; refresh or diagnose the prerequisite
work-model, analysis, and verification generated views; emit deterministic
JSON/text reports; and point ship-ready work to the protected-boundary handoff
without requiring or implementing Governance effective-evidence freshness,
routing, profiles, gates, audits, release policy, or protected-boundary
enforcement.

The ship command is non-destructive for authored lifecycle sources. Like
`verify`, it authors no source artifact: it only generates the
`readiness/<id>/ship.json` view and refreshes prerequisite generated views when
source data is valid and the run is not dry-run. Ship readiness is a single
merge-boundary disposition over aggregated lifecycle, verification, evidence, and
generated-view state. Unlike `verify`, ship does not re-derive task, evidence,
test, or skill dispositions; it consumes the verification view that owns those
facts and aggregates them into one merge-boundary result. The command never
rewrites authored specifications, plans, tasks, evidence declarations, or the
verification view; it inspects authored intent plus the upstream generated views
and refreshes generated readiness facts. Governance effective-evidence freshness,
route selection, profile adjustment, gate selection, protected-boundary
enforcement, audit verdicts, release gating, and generated agent guidance are out
of scope for this slice.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for artifact, command, report, fixture, CLI smoke, and public-surface
tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; prerequisite reads target `work/<id>/spec.md`,
`work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`,
`work/<id>/tasks.yml`, `work/<id>/evidence.yml`,
`readiness/<id>/analysis.json`, `readiness/<id>/verify.json`, the selected
`readiness/<id>/work-model.json` state, and existing `readiness/<id>/ship.json`
when present; no authored writes are planned; generated writes target
`readiness/<id>/ship.json` and target `readiness/<id>/work-model.json` when ship
facts make the normalized work-model refresh valid

**Schema/Migration**: `ship.json` and ship command report JSON use
`schemaVersion: 1` for this feature. The migration posture is diagnose-only:
current schema version 1 is accepted; missing, malformed, future, unsupported, or
deprecated generated ship view schema versions are reported as generated-view
diagnostics until a later feature defines an explicit migration path. Breaking
schema changes require updated contracts, fixtures, surface baselines, and
migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline tests;
focused ship view artifact-model tests; command workflow tests;
generate/rerun/refresh tests; authored-source preservation tests; blocked
readiness disposition tests for missing/stale prerequisites, missing or not-ready
verification view, stale evidence, unknown reference, undisclosed synthetic
evidence, invalid deferral, malformed and stale generated views; deterministic
JSON/report tests; text projection tests; no-Governance tests; CLI
JSON/dry-run/text smoke tests; FSI or prelude evidence for the public ship
surface

**Target Platform**: Cross-platform .NET command library, console executable, and
tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Generate, refresh, or rerun the `ship-create`,
`ship-rerun-current`, and `ship-refreshes-work-model` fixture scenarios in under
2 seconds each when run through the command test harness on the local development
machine; produce byte-identical JSON reports for three identical dry-run ship
executions over the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation changes;
Markdown and YAML remain authoring surfaces while schema-versioned structured
artifacts remain the machine contract; `ship.json` is generated readiness data,
not authored lifecycle source, and its presence is not proof of currency; the
ship command does not implement Governance effective-evidence freshness, route
selection, profile selection, gate enforcement, protected-boundary enforcement,
audit, release behavior, or generated agent guidance; reports exclude implicit
clocks, durations, terminal width, ANSI styling, directory enumeration order,
host-specific path separators, random values, and absolute host paths; dry-run
reports proposed generated changes without mutating files; optional Governance
pointers stay advisory compatibility facts

**Scale/Scope**: One initialized SDD project and one selected verification-ready
work item per command invocation; one new lifecycle command (`ship`); generated
ship view create/refresh, merge-boundary readiness aggregation, work-model
currency handling, deterministic output, fixtures, dry-run behavior, and optional
Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Ship must update `.fsi` signatures for the ship view facts, ship summary, finding/disposition/diagnostic contracts, workflow/report/effect contracts, and command-union additions before `.fs` behavior, and must exercise the public ship surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares the generated `ship.json` view, schema version 1, source relationships, aggregated lifecycle/verification readiness, generated-view currency, command report JSON, stale behavior, and diagnostics; authored Markdown/YAML/evidence sources and the verification view remain prior facts and are never rewritten. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Ship behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure ship-readiness aggregation before any generated filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing ship semantic tests, real filesystem smoke paths, golden JSON/report tests, authored-source preservation tests, blocked readiness diagnostics, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports and the normalized work-model/ship facts are the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing verification-ready prerequisites, malformed prerequisites, not-ready verification, unknown references, stale evidence, undisclosed synthetic evidence, invalid deferrals, malformed/stale ship or prerequisite generated views, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/013-ship-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- ship-view.md
|   |-- ship-command.md
|   |-- ship-report-json.md
|   `-- ship-fixtures.md
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
|   |   analysis, evidence, verification, generation manifest, and serialization
|   |   contracts
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
|   |-- ShipViewTests.fs
|   |-- VerificationViewTests.fs
|   |-- AnalysisViewTests.fs
|   |-- GeneratedModelCurrencyTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- ShipCommandTests.fs
|   |-- VerifyCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- ship-create/
        |-- ship-rerun-current/
        |-- ship-preserves-authored/
        |-- ship-refreshes-work-model/
        |-- ship-refreshes-verification/
        |-- ship-accepted-deferral/
        |-- outside-project/
        |-- missing-specification/
        |-- missing-clarification/
        |-- missing-checklist/
        |-- missing-plan/
        |-- missing-tasks/
        |-- missing-analysis/
        |-- missing-evidence/
        |-- missing-verification/
        |-- failed-verification/
        |-- not-verification-ready/
        |-- malformed-work-id/
        |-- malformed-ship-view/
        |-- duplicate-work-id/
        |-- unknown-source-reference/
        |-- stale-analysis/
        |-- stale-evidence/
        |-- stale-verification/
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
projects. `ship` has the same operational shape as the preceding generated-view
lifecycle command `verify`: load project context, validate one work item, inspect
authored and generated source artifacts, evaluate SDD-owned readiness, refresh or
diagnose generated views, and emit one deterministic report. Like `verify`,
`ship` authors no source; its only writes are generated readiness views. Unlike
`verify`, `ship` aggregates the upstream verification view instead of re-deriving
task/evidence/test/skill dispositions, and the existing valid/blocked fixture
roots (`outside-project`, `missing-*`, `malformed-work-id`,
`duplicate-work-id`, `unknown-source-reference`, `stale-*`,
`undisclosed-synthetic-evidence`, `invalid-deferral`, `stale-generated-view`,
`dry-run`, `deterministic-report`, `text-projection`, `governance-boundary`) are
reused with ship-specific expected outputs.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/ship-view.md](contracts/ship-view.md)
- [contracts/ship-command.md](contracts/ship-command.md)
- [contracts/ship-report-json.md](contracts/ship-report-json.md)
- [contracts/ship-fixtures.md](contracts/ship-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: ship additions are planned through public `.fsi` artifact, work-model, command type/report/effect, diagnostic, serialization, and rendering contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: the generated `ship.json` view, ship-readiness findings, aggregated lifecycle/verification readiness, generated-view currency, and command report JSON are structured contracts; authored Markdown/YAML/evidence sources and the verification view remain prior facts and are never rewritten; schema version 1 has diagnose-only migration posture. |
| Public API baseline | PASS: any ship view, command type/report/effect, generated-view, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, prerequisite validation, ship-readiness aggregation, generated-view refresh planning, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define generated ship view, rerun currency, work-model/verification refresh, authored-source preservation, dry-run, stale/missing source, missing prerequisite, not-ready verification, unknown reference, undisclosed synthetic evidence, invalid deferral, malformed/stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Safe failure | PASS: diagnostics identify the affected artifact, stable id, severity, correction, and next action before generated writes proceed; authored sources and the verification view are never mutated. |

No new complexity exceptions were introduced by Phase 1 design.
