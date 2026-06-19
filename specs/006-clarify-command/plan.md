# Implementation Plan: Clarify Command

**Branch**: `006-clarify-command` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-clarify-command/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this
repository is currently on Git branch `main`; the active feature context is
persisted in `.specify/feature.json` as `specs/006-clarify-command`.

## Summary

Implement `fsgg-sdd clarify` as the next native SDD lifecycle command after
`specify`. The feature extends the existing `FS.GG.SDD.Commands` workflow and
thin CLI host: it loads initialized project settings, validates one selected
specified work item, reads `work/<id>/spec.md` for structured specification
facts and open ambiguity records, creates or safely updates
`work/<id>/clarifications.md`, preserves existing answers and decision ids,
reports clarified or still-blocked lifecycle state, points successful results
to `checklist`, and emits deterministic command reports and text projections
without requiring the Governance runtime.

The clarification artifact is an authored Markdown source with structured
front matter and stable typed ids for clarification questions and decisions.
Concrete clarification decisions and accepted deferrals are durable facts used
by later checklist, plan, task, evidence, and readiness stages. Command report
JSON remains the immediate automation contract for clarification creation,
reruns, dry-runs, blocked results, generated-view currency, diagnostics, and
optional Governance compatibility facts.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for command, report, fixture, and public-surface tests

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; specification prerequisite reads target
`work/<id>/spec.md`; clarification writes target
`work/<id>/clarifications.md`; generated-view refresh targets
`readiness/<id>/work-model.json` when valid source data exists

**Schema/Migration**: Clarification front matter and clarify command report
JSON use `schemaVersion: 1` for this feature. The migration posture is
diagnose-only: current schema version 1 is accepted; missing, malformed,
future, unsupported, or deprecated clarification schema versions block
unsafe writes with actionable diagnostics until a later feature defines an
explicit migration path. Breaking schema changes require updated contracts,
fixtures, surface baselines, and migration notes before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline
tests; focused clarify workflow tests; temporary-directory CLI smoke tests;
safe rerun and unsafe-decision-change tests; accepted-deferral tests; dry-run
mutation tests; generated-view state tests; deterministic command-report JSON
tests; text projection tests; no-Governance boundary tests; FSI/prelude
evidence for the public command surface

**Target Platform**: Cross-platform .NET command library, console executable,
and tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Create or rerun the `clarify-create` and
`clarify-rerun-preserves-decisions` fixture scenarios in under 2 seconds each
when run through the command test harness on the local development machine;
produce byte-identical JSON reports for three identical dry-run clarify
executions over the deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation
changes; Markdown remains the authoring surface; structured front matter,
typed ids, and command report JSON are the machine contracts; generated views
require source digest and generator-version currency checks; reports exclude
implicit clocks, durations, terminal width, ANSI styling, directory
enumeration order, host-specific path separators, random values, and absolute
host paths; existing authored clarification content and stable ids are
preserved unless the update is proven safe; unsafe decision changes block
before filesystem mutation; dry-run reports planned changes without mutating
authored or generated artifacts; Governance pointers stay optional
compatibility facts

**Scale/Scope**: One initialized SDD project and one selected specified work
item per command invocation; one new lifecycle command (`clarify`);
clarification artifact creation/update, lifecycle report state, generated
work-model refresh or diagnostic, deterministic output, fixtures, dry-run
behavior, and optional Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update `.fsi` signatures for clarification question ids, clarification facts, command workflow/report/effect contracts, and parser additions before `.fs` behavior and must exercise the public clarify surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares authored clarification Markdown, structured front matter, typed question/decision ids, command report JSON, schema migration posture, generated-view state, stale behavior, and diagnostics. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing helpers; no framework or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Clarify behavior uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure planning before filesystem writes. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing clarify semantic tests, real filesystem smoke paths, golden JSON/report tests, safe-rerun and unsafe-decision-change tests, accepted-deferral tests, dry-run tests, generated-view diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Command reports remain the source for JSON and text; agent context points to this plan; generated agent guidance is not introduced in this feature. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, malformed or missing work id, missing specification prerequisite, missing answers, unknown references, malformed clarification data, duplicate ids, unsafe decision changes, generated-view currency states, and optional Governance boundary issues. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/006-clarify-command/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- clarification-artifact.md
|   |-- clarify-command.md
|   |-- clarify-report-json.md
|   `-- clarify-fixtures.md
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
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- CommandWorkflowTests.fs
|   |-- InitCommandTests.fs
|   |-- CharterCommandTests.fs
|   |-- SpecifyCommandTests.fs
|   |-- ClarifyCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
	    `-- lifecycle-commands/
	        |-- clarify-create/
	        |-- no-open-ambiguity/
	        |-- clarify-rerun-preserves-decisions/
	        |-- clarify-adds-missing-sections/
	        |-- clarify-preserves-stable-ids/
	        |-- clarify-accepted-deferral/
	        |-- outside-project/
	        |-- missing-specification/
	        |-- missing-answer/
	        |-- duplicate-work-id/
	        |-- malformed-clarification/
	        |-- duplicate-clarification-id/
	        |-- unknown-ambiguity-reference/
	        |-- clarification-identity-mismatch/
	        |-- unsafe-overwrite/
	        |-- unsafe-decision-change/
	        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        |-- stale-generated-view/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Commands` library and
`FS.GG.SDD.Cli` host rather than adding new projects. The command identity
already includes `Clarify`; this feature removes the unsupported-command path
for that one command and wires clarify through the same request, effect,
interpreter, report, serializer, and renderer contracts used by `init`,
`charter`, and `specify`.

The feature may add narrow artifact-model types and parser helpers for
clarification front matter, clarification questions, answers, decisions,
accepted deferrals, and remaining ambiguity because later lifecycle stages
need those facts from the structured contract. It must not introduce
`checklist`, `plan`, `tasks`, `analyze`, task/evidence update, verify, ship,
release, generated agent guidance, route selection, freshness, profile, gate,
or Governance enforcement behavior.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/clarification-artifact.md](contracts/clarification-artifact.md)
- [contracts/clarify-command.md](contracts/clarify-command.md)
- [contracts/clarify-report-json.md](contracts/clarify-report-json.md)
- [contracts/clarify-fixtures.md](contracts/clarify-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: clarify additions are planned through public `.fsi` command and artifact contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: clarification front matter, typed question/decision ids, and command report JSON are structured contracts; Markdown body remains authored prose; schema version 1 has diagnose-only migration posture; generated views report source and currency state. |
| Public API baseline | PASS: any identifier, lifecycle artifact, command type/report/effect changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, specification prerequisite validation, answer normalization, safe writes, generated-view refresh, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define create, rerun, safe additions, stable id preservation, accepted deferrals, missing specification, missing answers, malformed clarification, stale generated view, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: agent context points at this plan and generated agent guidance remains out of scope. |
| Governance boundary | PASS: clarify reports may expose optional Governance pointers but do not parse Governance schemas, select routes, evaluate freshness, adjust profiles, select gates, or enforce protected boundaries. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
