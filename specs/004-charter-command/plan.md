# Implementation Plan: Charter Command

**Branch**: `004-charter-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-charter-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/004-charter-command`.

## Summary

Implement `fsgg-sdd charter` as the next native SDD lifecycle command after
`init`. The feature extends the existing `FS.GG.SDD.Commands` workflow instead
of adding a new project: it loads initialized project settings, validates one
selected work id, creates or safely updates `work/<id>/charter.md`, reports the
selected work item as chartered, points the user to `specify`, and emits
deterministic command reports and text projections without requiring the
Governance runtime.

The charter artifact is an authored Markdown source with structured front
matter. Command report JSON is the immediate automation contract for charter
creation and reruns. `readiness/<id>/work-model.json` is refreshed only when
the existing artifact model has enough valid source data; otherwise the report
records a missing, stale, malformed, or blocked generated-view state with
actionable diagnostics instead of treating generated file presence as current.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for command, report, fixture, and surface tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; charter writes target `work/<id>/charter.md`; generated-view
refresh targets `readiness/<id>/work-model.json` when valid source data exists

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused charter workflow tests; temporary-directory CLI smoke tests;
safe rerun and unsafe-overwrite tests; generated-view state tests;
deterministic command-report JSON tests; text projection tests; no-Governance
boundary tests; FSI/prelude evidence for the public command surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `charter-create` and
`charter-rerun-preserves-content` fixture scenarios in under 2 seconds each
when run through the command test harness on the local development machine;
produce byte-identical JSON reports for three identical dry-run charter
executions over the `deterministic-report` fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation changes;
Markdown remains the authoring surface; structured front matter and command
report JSON are the machine contracts; generated views require source digest
and generator-version currency checks; reports exclude implicit clocks,
terminal width, ANSI styling, directory enumeration order, host-specific path
separators, and absolute host paths; existing authored charter content is
preserved unless the update is proven safe; unsafe writes block before
filesystem mutation; Governance pointers stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected work item per
command invocation; one new lifecycle command (`charter`); charter artifact
creation/update, lifecycle report state, generated work-model refresh or
diagnostic, deterministic output, fixtures, and optional Governance boundary
facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for command workflow/report/effect contracts before `.fs` behavior and must exercise the public charter surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored charter Markdown, structured charter front matter, command report JSON, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command API changes remain in existing `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Charter behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure planning before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing charter semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and unsafe-overwrite tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing or malformed project settings, existing charter identity mismatches, unsafe content changes, duplicate logical ids, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/004-charter-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- charter-artifact.md
|   |-- charter-command.md
|   |-- charter-report-json.md
|   `-- charter-fixtures.md
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
|   `-- optional narrow parser additions for charter front matter if needed
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
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- CharterCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- charter-create/
        |-- charter-rerun-preserves-content/
        |-- charter-adds-missing-sections/
        |-- charter-identity-mismatch/
        |-- duplicate-work-id/
        |-- outside-project/
        |-- malformed-work-id/
        |-- malformed-artifact/
        |-- unsafe-overwrite/
        |-- stale-generated-view/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Charter`; this feature removes the unsupported-command path
for that one command and wires charter through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init`.

The feature may add narrow artifact-model parsing for charter front matter only
where needed to keep structured data authoritative. It must not introduce
`specify`, `clarify`, `checklist`, `plan`, `tasks`, `analyze`, task/evidence
update, verify, ship, release, generated agent guidance, route selection,
freshness, profile, gate, or Governance enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/charter-artifact.md](contracts/charter-artifact.md)
- [contracts/charter-command.md](contracts/charter-command.md)
- [contracts/charter-report-json.md](contracts/charter-report-json.md)
- [contracts/charter-fixtures.md](contracts/charter-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: charter additions are planned through existing public `.fsi` command contracts and surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: charter front matter and command report JSON are structured contracts; Markdown body remains authored prose; generated views report source and currency state. |
| Public API baseline | PASS: any command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, charter planning, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, identity mismatch, outside-project, malformed id, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Governance boundary | PASS: charter reports may expose optional Governance pointers but do not parse Governance schemas, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
