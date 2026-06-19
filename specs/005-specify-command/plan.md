# Implementation Plan: Specify Command

**Branch**: `005-specify-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-specify-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/005-specify-command`.

## Summary

Implement `fsgg-sdd specify` as the next native SDD lifecycle command after
`charter`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
work id, confirms the selected work item has a valid charter prerequisite,
creates or safely updates `work/<id>/spec.md` from explicit specification
intent, reports the selected work item as specified, points the user to
`clarify`, and emits deterministic command reports and text projections without
requiring the Governance runtime.

The specification artifact is an authored Markdown source with structured
front matter and stable typed ids for stories, requirements, acceptance
scenarios, scope boundaries, and ambiguity records. Command report JSON is the
immediate automation contract for specification creation, reruns, dry-runs, and
blocked results. `readiness/<id>/work-model.json` is refreshed only when the
current artifact model has enough valid source data; otherwise the report
records missing, stale, malformed, or blocked generated-view state with
actionable diagnostics instead of treating generated file presence as current.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for command, report, fixture, and surface tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; charter prerequisite reads target
`work/<id>/charter.md`; specification writes target `work/<id>/spec.md`;
generated-view refresh targets `readiness/<id>/work-model.json` when valid
source data exists

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused specify workflow tests; temporary-directory CLI smoke tests;
safe rerun and unsafe-overwrite tests; dry-run mutation tests; generated-view
state tests; deterministic command-report JSON tests; text projection tests;
no-Governance boundary tests; FSI/prelude evidence for the public command
surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `specify-create` and
`specify-rerun-preserves-content` fixture scenarios in under 2 seconds each
when run through the command test harness on the local development machine;
produce byte-identical JSON reports for three identical dry-run specify
executions over the `deterministic-report` fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface; structured front matter,
typed ids, and command report JSON are the machine contracts; generated views
require source digest and generator-version currency checks; reports exclude
implicit clocks, durations, terminal width, ANSI styling, directory
enumeration order, host-specific path separators, random values, and absolute
host paths; existing authored specification content and stable ids are
preserved unless the update is proven safe; unsafe writes block before
filesystem mutation; dry-run reports planned changes without mutating authored
or generated artifacts; Governance pointers stay optional compatibility facts

**Scale/Scope**: One initialized SDD project and one selected chartered work
item per command invocation; one new lifecycle command (`specify`);
specification artifact creation/update, lifecycle report state, generated
work-model refresh or diagnostic, deterministic output, fixtures, dry-run
behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for specification ids, command workflow/report/effect contracts, and parser additions before `.fs` behavior and must exercise the public specify surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored spec Markdown, structured spec front matter, typed ids, command report JSON, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Specify behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure planning before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing specify semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and unsafe-overwrite tests, dry-run tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing charter prerequisite, missing intent, malformed project settings, existing spec identity mismatches, duplicate ids, unsafe content changes, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/005-specify-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- specification-artifact.md
|   |-- specify-command.md
|   |-- specify-report-json.md
|   `-- specify-fixtures.md
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
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- CharterCommandTests.fs
|   |-- SpecifyCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
	    `-- lifecycle-commands/
	        |-- specify-create/
	        |-- specify-rerun-preserves-content/
	        |-- specify-adds-missing-sections/
	        |-- specify-preserves-stable-ids/
	        |-- outside-project/
	        |-- missing-charter/
	        |-- missing-intent/
	        |-- malformed-work-id/
	        |-- malformed-specification/
	        |-- duplicate-work-id/
	        |-- duplicate-spec-id/
	        |-- specification-identity-mismatch/
	        |-- unsafe-overwrite/
	        |-- stale-generated-view/
	        |-- deterministic-report/
	        |-- text-projection/
	        |-- dry-run/
	        `-- governance-boundary/
	```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Specify`; this feature removes the unsupported-command path
for that one command and wires specify through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init` and
`charter`.

The feature may add narrow artifact-model types and parser helpers for
specification stories, acceptance scenarios, scope boundaries, ambiguity
records, and stable ids because later lifecycle stages need those facts from
the structured contract. It must not introduce `clarify`, `checklist`, `plan`,
`tasks`, `analyze`, task/evidence update, verify, ship, release, generated
agent guidance, route selection, freshness, profile, gate, or Governance
enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/specification-artifact.md](contracts/specification-artifact.md)
- [contracts/specify-command.md](contracts/specify-command.md)
- [contracts/specify-report-json.md](contracts/specify-report-json.md)
- [contracts/specify-fixtures.md](contracts/specify-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: specify additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: specification front matter, typed ids, and command report JSON are structured contracts; Markdown body remains authored prose; generated views report source and currency state. |
| Public API baseline | PASS: any identifier, lifecycle artifact, command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, charter prerequisite validation, intent normalization, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, stable id preservation, missing charter, missing intent, malformed spec, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Governance boundary | PASS: specify reports may expose optional Governance pointers but do not parse Governance schemas, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
