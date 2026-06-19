# Implementation Plan: Native SDD Lifecycle Commands

**Branch**: `003-native-sdd-lifecycle-commands` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-native-sdd-lifecycle-commands/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/003-native-sdd-lifecycle-commands`.

## Summary

Introduce the native `fsgg-sdd` lifecycle command surface for initialization,
charter, specify, clarify, checklist, plan, tasks, and analyze. The feature
adds a command workflow layer over the existing `FS.GG.SDD.Artifacts`
machine contract, keeps stateful filesystem behavior behind a testable
Elmish-style boundary, emits deterministic command reports as the automation
contract, renders plain text as a projection of those reports, refreshes SDD
generated views when valid source data exists, and preserves the boundary that
keeps route selection, freshness, profiles, gates, and enforcement in
FS.GG.Governance.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts` project, BCL,
FSharp.Core, System.Text.Json, and YamlDotNet through the artifact-model
library; no external CLI parser or presentation framework in this feature;
xUnit test packages for automated tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; command workflows model reads, writes, directory creation,
and generated-view refreshes as explicit effects before interpretation

**Testing**: `dotnet test` with xUnit; public `.fsi` signature and
surface-baseline tests; FSI/prelude evidence for public command contracts;
temporary-directory command smoke tests; golden JSON report fixtures;
plain-text projection tests; stale-view and unsafe-overwrite fixtures

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable,
companion command test project, and fixture corpus; existing
`FS.GG.SDD.Artifacts` remains the source of lifecycle artifact and work-model
facts

**Performance Goals**: Initialize or advance a representative lifecycle fixture
in under 2 seconds on a normal developer machine; produce byte-identical JSON
reports for identical inputs across three consecutive dry-run executions

**Constraints**: `.fsi` signatures before `.fs` implementation for public
modules; structured artifacts remain the machine contract; Markdown remains an
authoring surface; generated views are outputs and require currency checks;
command reports must exclude implicit clocks, terminal width, ANSI styling,
directory enumeration order, host-specific path separators, and absolute host
paths from authoritative content; unsafe overwrites are refused with
diagnostics; SDD remains usable without Governance installed

**Scale/Scope**: One selected SDD project and one selected work item at a time;
eight native lifecycle commands; project skeleton, lifecycle authoring
artifacts, deterministic command reports, `work-model.json` refresh behavior,
`analysis.json` generation, diagnostics, and optional Governance compatibility
facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must add `.fsi` signatures for command types, reports, workflow, effects, rendering, and serialization before `.fs` bodies; FSI/prelude evidence must exercise the public command shape before implementation hardens it. | PASS |
| II. Structured Artifacts Are the Machine Contract | Plan declares authored sources, structured command models, generated views, stale behavior, diagnostics, command report JSON, and conflict precedence. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | New public command modules require matching `.fsi` files and surface-baseline updates; executable entrypoint remains a thin host over public command modules. | PASS |
| IV. Idiomatic Simplicity Is the Default | Command behavior uses records, discriminated unions, modules, explicit effects, BCL parsing, and existing libraries; no framework, reflection-heavy, or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Stateful and file-changing commands expose `Model`, `Msg`, `Effect`, `init`, `update`, and an edge interpreter; pure transitions are testable without real filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | Plan requires failing semantic tests, temporary-directory smoke tests, golden command report JSON, fixture diagnostics, deterministic dry-run comparisons, FSI evidence, and public-surface coverage. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Claude and Codex guidance targets are lifecycle artifacts named by `.fsgg/agents.yml`; command reports and generated views are authoritative, while agent files remain projections and must stay equivalent. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed work ids, missing prerequisites, malformed artifacts, unsafe overwrites, stale generated views, unknown references, optional Governance boundary issues, and tool defects versus user input failures. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/003-native-sdd-lifecycle-commands/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- command-api.md
|   |-- command-report-json.md
|   |-- lifecycle-commands.md
|   `-- fixture-catalog.md
`-- tasks.md                 # Created by $speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln
Directory.Build.props
Directory.Packages.props

src/
|-- FS.GG.SDD.Artifacts/
|   |-- existing artifact model, work model, diagnostics, and serialization
|   `-- existing .fsi/.fs files
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
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- FS.GG.SDD.Commands.Tests.fsproj
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- LifecycleCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   `-- SurfaceBaselineTests.fs
`-- fixtures/
    |-- sdd-artifact-model/
    |-- normalized-work-model/
    `-- lifecycle-commands/
        |-- init-empty-project/
        |-- init-preserves-user-files/
        |-- init-conflicting-lifecycle-path/
        |-- lifecycle-through-analysis/
        |-- outside-project/
        |-- malformed-work-id/
        |-- missing-prerequisites/
        |-- malformed-artifact/
        |-- unsafe-overwrite/
        |-- unknown-reference/
        |-- stale-generated-view/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Add `FS.GG.SDD.Commands` as the public command workflow
library and `FS.GG.SDD.Cli` as a thin executable host for `fsgg-sdd`. The
existing `FS.GG.SDD.Artifacts` package continues to own lifecycle artifact
parsing, normalized work-model generation, source digests, generated-view
currency metadata, deterministic JSON helpers, and stable diagnostics.

Command behavior is split from the executable so tasks can write semantic tests
against pure `init`/`update` transitions, effect lists, command reports, and
renderers before wiring real filesystem I/O. The CLI project performs argument
normalization, invokes the command workflow, interprets effects at the edge,
and writes either deterministic JSON or a plain text projection from the same
report object. No Governance runtime, rendering template provider, task/evidence
update command, verify/ship command, or release packaging behavior is introduced
in this feature.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/command-api.md](contracts/command-api.md)
- [contracts/command-report-json.md](contracts/command-report-json.md)
- [contracts/lifecycle-commands.md](contracts/lifecycle-commands.md)
- [contracts/fixture-catalog.md](contracts/fixture-catalog.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: command types, reports, workflow, effects, serialization, and rendering are specified as public `.fsi` contracts before implementation bodies. |
| Structured machine contract | PASS: command reports, work-model refreshes, analysis views, source digests, generated-view status, and diagnostics are structured artifacts; Markdown remains authoring input. |
| Public API baseline | PASS: command library surface-baseline tests are required when public signatures change. |
| MVU boundary | PASS: all file-changing command behavior is planned behind `Model`, `Msg`, `Effect`, `init`, `update`, and an edge interpreter. |
| Evidence | PASS: quickstart and fixture catalog define init, lifecycle progression, diagnostics, generated-view currency, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: `.fsgg/agents.yml` names Claude and Codex targets and requires equivalent behavior; generated agent guidance remains future projection work. |
| Governance boundary | PASS: commands may report optional Governance compatibility facts but do not parse Governance policy, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
