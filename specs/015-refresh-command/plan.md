# Implementation Plan: Generated-View Refresh

**Branch**: `015-refresh-command` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-refresh-command/spec.md`

**Status (2026-06-20)**: ✅ Implemented and merged. Build green; full suite
**306 tests pass** (was 281). Progress and the one disclosed scope deviation
(analysis/verify/ship are currency-reported, not destructively re-run) are
tracked in [tasks.md](tasks.md) Implementation Status + Implementation Notes;
evidence is under [readiness/](readiness/).

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/015-refresh-command`.

## Summary

Implement `fsgg-sdd refresh` as a native, cross-cutting SDD command that brings
one selected work item's SDD-owned generated views back into currency from its
current declared sources in a single deterministic run. The command extends the
existing `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli`
projects to: load project context and validate one work id; re-run the existing
deterministic per-view generators (`work-model.json`, `analysis.json`,
`verify.json`, `ship.json`, and the per-target `agent-commands/`) from their
current declared sources in declared-source order; render the new
`readiness/<id>/summary.md` projection from the structured readiness data;
evaluate the currency of every SDD-owned generated view; refresh the views that
can be refreshed while reporting missing, stale, malformed, and blocked views
precisely; and emit one deterministic JSON/text command report plus a
next action.

`refresh` is **not** a new lifecycle authoring stage and does not sit between any
existing stages. Like `analyze`, `verify`, `ship`, and `agents`, it authors no
source artifact; its only writes are generated views under their configured
generated roots. Unlike the single-view generated-view commands, `refresh`
orchestrates *all* SDD-owned generated views for a work item together, honoring
declared source-of dependencies (it brings an upstream generated view — e.g. the
normalized work model — to currency before a dependent view such as agent
guidance or the summary), and it adds the human-readable
`readiness/<id>/summary.md` rendered strictly from the structured readiness data.
Authored lifecycle artifacts and the normalized work model remain authoritative;
refreshed views are projections and never a second source of truth
(Constitution VII). The command never rewrites authored specifications, plans,
tasks, evidence declarations, `.fsgg/*.yml` configuration, or the hand-owned
`CLAUDE.md`/`AGENTS.md` files. `refresh` reuses the same generators the lifecycle
and `agents` commands already use, so its outputs are byte-identical to those
commands for identical inputs; it adds the orchestration, the `summary.md`
projection, and the cross-view currency report rather than new generated-view
contracts.

Governance effective-evidence freshness, route selection, profile adjustment,
gate selection, protected-boundary enforcement (including stale-view blocking at
a protected boundary), audit verdicts, and release gating are out of scope. SDD
reports stale views as findings; blocking them at a boundary remains a
Governance-owned concern. The command works with Governance absent and exposes
any Governance pointers only as advisory compatibility facts.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for artifact, command, report, fixture, CLI smoke, and public-surface
tests. The feature reuses the existing `GenerationManifest`,
`GeneratedViewKind` (including the already-defined `Summary` kind),
`SourceIdentity`, `isStale`, and per-view generators, plus the existing
`CommandReport`, `GeneratedViewState`, `GeneratedViewCurrency`,
`ArtifactChange`, `NextAction`, and the per-stage summary records on the report.

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; reads target `.fsgg/project.yml`, `.fsgg/sdd.yml`, optionally
`.fsgg/agents.yml`, the selected work item's authored sources, and the existing
generated views under `readiness/<id>/` (`work-model.json`, `analysis.json`,
`verify.json`, `ship.json`, `summary.md`, `agent-commands/<target>/`); no
authored writes are planned; generated writes target each view's configured
generated root under `readiness/<id>/`, including the new
`readiness/<id>/summary.md`.

**Schema/Migration**: The refresh command report JSON uses `schemaVersion: 1`.
The refreshed structured views keep the schema versions their existing
generators already emit (`work-model.json`, `analysis.json`, `verify.json`,
`ship.json`, and the per-target `agent-commands/<target>/guidance.json`);
`refresh` does not change those contracts. `readiness/<id>/summary.md` is a
generated Markdown projection carrying a generation manifest header
(`GeneratedViewKind.Summary`, schema version 1, generator identity, source
relationships, source digests). The migration posture is diagnose-only: current
schema versions are accepted; missing, malformed, future, unsupported, or
deprecated generated-view or source schema versions are reported as
generated-view or source diagnostics until a later feature defines an explicit
migration path. Breaking schema changes require updated contracts, fixtures,
surface baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline tests;
focused artifact-model tests for the `summary.md` generation manifest header and
projection rendering, cross-view currency evaluation, and source-of dependency
ordering; command workflow tests for the orchestrated refresh; refresh/rerun
currency tests; authored-source preservation tests (authored lifecycle
artifacts, `.fsgg/*.yml`, and `CLAUDE.md`/`AGENTS.md` byte-unchanged);
blocked-disposition tests for outside-project, malformed/duplicate/mismatched
work id, missing/malformed/stale sources, unknown source references, malformed
existing generated views, and blocked-upstream views; not-applicable agent-target
handling; dry-run tests (zero file changes); deterministic JSON/report tests;
text-projection equivalence tests; summary-projection-faithfulness tests (no
facts absent from structured views); no-Governance tests; CLI JSON/dry-run/text
smoke tests; FSI or prelude evidence for the public refresh surface.

**Target Platform**: Cross-platform .NET command library, console executable, and
tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Orchestrate a full work-item refresh (work model →
analysis → verify → ship → agent guidance → summary) for the `refresh-current`
and `refresh-stale-views` fixtures in under 3 seconds each when run through the
command test harness on the local development machine; produce byte-identical
refreshed views and JSON reports across three identical refresh executions over
the `deterministic-report` fixture.

**Constraints**: `.fsi` signatures precede public `.fs` implementation changes;
Markdown and YAML remain authoring surfaces while schema-versioned structured
artifacts remain the machine contract; refreshed views are derived readiness
data, not authored source, and their presence is not proof of currency;
refreshed views are never a second source of truth (Constitution VII);
`summary.md` introduces no fact absent from the structured readiness views it
projects (FR-006); each refreshable view is regenerated from its current
declared sources, not from a prior generated view's cached content, except where
one generated view is the declared source of another (FR-004); the command does
not implement Governance effective-evidence freshness, route/profile selection,
gate enforcement, protected-boundary enforcement (including stale-view blocking),
audit, or release behavior; reports and refreshed views exclude implicit clocks,
durations, terminal width, ANSI styling, directory enumeration order,
host-specific path separators, random values, and absolute host paths; dry-run
reports proposed generated changes without mutating any authored or generated
file; optional Governance pointers stay advisory compatibility facts.

**Scale/Scope**: One initialized SDD project and one selected work item per
command invocation; one new cross-cutting command (`refresh`); orchestrated
refresh of the existing SDD-owned generated views, the new `summary.md`
projection, cross-view currency evaluation with source-of dependency ordering,
deterministic output, fixtures, dry-run behavior, and optional Governance
boundary facts only.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Refresh must update `.fsi` signatures for the `Refresh` command-union case, the `summary.md` generation-manifest/projection surface, the refresh summary/disposition/diagnostic contracts, and any workflow/report/effect additions before `.fs` behavior, and must exercise the public refresh surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares the refresh command report JSON (schema version 1), reuses the existing structured view contracts unchanged, and adds `summary.md` as a generated projection with a manifest header; the structured readiness views remain authoritative and `summary.md` introduces no independent facts. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes stay in `.fsi` files and surface-baseline tests are updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and the existing per-view generators and parsers; no framework, reflection, or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Refresh uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure currency evaluation, source-of ordering, and projection rendering before any generated filesystem writes; the constitution names generators and validators as MVU-boundary cases. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing refresh semantic tests, real filesystem smoke paths, golden JSON/summary/report fixtures, authored-source preservation tests, blocked-disposition diagnostics, generated-view diagnostics, summary-faithfulness tests, deterministic and no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Refresh is the single command that brings the full SDD-owned generated-view set back into agreement with declared sources for humans, Claude, Codex, CLI, and CI; `summary.md` is rendered from the structured data and is explicitly not a second source of truth; stale/blocked views are visible diagnostics. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed/duplicate/mismatched work id, missing/malformed/stale sources, unknown source references, malformed existing generated views, blocked-upstream views, unrenderable summary, and optional Governance boundary issues; stale generated views are an explicitly named diagnostic family in the constitution. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/015-refresh-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- refresh-summary-view.md
|   |-- refresh-command.md
|   |-- refresh-report-json.md
|   `-- refresh-fixtures.md
`-- tasks.md                 # Created by /speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln
Directory.Build.props
Directory.Packages.props

src/
|-- FS.GG.SDD.Artifacts/
|   |-- GenerationManifest.fsi        # Summary kind already present; add
|   |-- GenerationManifest.fs         #   summary manifest/render helpers
|   |-- LifecycleArtifacts.fsi
|   |-- LifecycleArtifacts.fs
|   |-- WorkModel.fsi
|   `-- WorkModel.fs
|-- FS.GG.SDD.Commands/
|   |-- CommandTypes.fsi              # add Refresh case + refresh summary record
|   |-- CommandTypes.fs
|   |-- CommandReports.fsi            # add refresh-specific diagnostics + report
|   |-- CommandReports.fs
|   |-- CommandWorkflow.fsi
|   |-- CommandWorkflow.fs            # orchestrate per-view refresh + summary
|   |-- CommandEffects.fsi
|   |-- CommandEffects.fs
|   |-- CommandSerialization.fsi
|   |-- CommandSerialization.fs
|   |-- CommandRendering.fsi
|   `-- CommandRendering.fs
`-- FS.GG.SDD.Cli/
    |-- FS.GG.SDD.Cli.fsproj
    `-- Program.fs                    # dispatch `refresh`

scripts/
`-- prelude.fsx                       # FSI evidence for the refresh surface

tests/
|-- FS.GG.SDD.Artifacts.Tests/
|   |-- RefreshSummaryViewTests.fs
|   |-- GeneratedModelCurrencyTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- RefreshCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- refresh-current/
        |-- refresh-stale-views/
        |-- refresh-missing-view/
        |-- refresh-summary/
        |-- refresh-preserves-authored/
        |-- refresh-no-agent-targets/
        |-- malformed-generated-view/
        |-- blocked-upstream-view/
        |-- stale-source/
        |-- missing-source/      # new generic source root (earlier slices used
        |-- malformed-source/    #   per-artifact missing-/malformed-* roots)
        |-- (reuses shared roots: outside-project, malformed-work-id,
        |    duplicate-work-id, unknown-source-reference, dry-run,
        |    deterministic-report, text-projection, governance-boundary)
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects rather than adding new
projects. `refresh` shares the operational shape of the preceding generated-view
commands (`analyze`, `verify`, `ship`, `agents`): load project context, validate
one work item, inspect authored sources plus existing generated views, evaluate
SDD-owned currency, refresh or diagnose generated views, and emit one
deterministic report. It differs in three ways: (1) it orchestrates *all*
SDD-owned generated views together rather than one, honoring declared source-of
dependency order so upstream views are brought to currency before dependents;
(2) it produces the new human-readable `readiness/<id>/summary.md` projection
rendered from the structured readiness data; and (3) it is cross-cutting
(`nextLifecycleCommand Refresh = None`), reusing the existing per-view generators
rather than introducing new generated-view contracts. Shared blocked-fixture
roots follow the established naming with refresh-specific expected outputs; new
roots cover the orchestrated current/stale/missing, summary, multi-view
preservation, and blocked-upstream cases unique to this feature.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/refresh-summary-view.md](contracts/refresh-summary-view.md)
- [contracts/refresh-command.md](contracts/refresh-command.md)
- [contracts/refresh-report-json.md](contracts/refresh-report-json.md)
- [contracts/refresh-fixtures.md](contracts/refresh-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: refresh additions are planned through public `.fsi` artifact (summary manifest/render), command type/report/effect, diagnostic, serialization, and rendering contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: the refresh report JSON (schema version 1) is structured; the existing structured view contracts are reused unchanged; `summary.md` is a generated projection with a manifest header rendered from structured readiness data; schema versions have diagnose-only migration posture. |
| Public API baseline | PASS: any command type/report/effect, generation-manifest, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, work-item validation, per-view currency evaluation, source-of ordering, projection rendering, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define orchestrated refresh, stale/missing/malformed/blocked-upstream handling, summary projection, authored-source preservation, dry-run, deterministic JSON, text projection, summary faithfulness, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: refresh keeps the full generated-view set in agreement with declared sources for all consumers; `summary.md` is rendered from structured data, marked generated with source digests, and is never a second source of truth; agent context points at this plan. |
| Safe failure | PASS: diagnostics identify the affected view and source or upstream view, stable id, severity, correction, and next action before generated writes proceed; the views that can be refreshed are refreshed even when others are blocked; authored sources are never mutated. |

No new complexity exceptions were introduced by Phase 1 design.
