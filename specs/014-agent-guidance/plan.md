# Implementation Plan: Agent Guidance Generation

**Branch**: `014-agent-guidance` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-agent-guidance/spec.md`

> **Status: ✅ Implemented (2026-06-20).** `dotnet build FS.GG.SDD.sln -c Release`
> succeeds; `dotnet test FS.GG.SDD.sln -c Release` passes 281 tests, 0 failures;
> `dotnet fsi scripts/prelude.fsx` exits 0; CLI JSON/dry-run/text smokes exit 0.
> See [tasks.md](tasks.md) for per-task status and `readiness/` for evidence.
> Deferred (disclosed): static `agents-*` fixture directories (T003–T005) —
> scenarios are covered by real-evidence `AgentsCommandTests` instead.

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/014-agent-guidance`.

## Summary

Implement `fsgg-sdd agents` as a cross-cutting SDD generator that derives Claude
and Codex agent command and skill guidance from one selected work item's
normalized work model and emits it as a generated view under
`readiness/<id>/agent-commands/`. The feature extends the existing
`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects to:
load the `.fsgg/agents.yml` guidance configuration (already parsed by
`parseAgentGuidanceConfig` into `AgentGuidanceConfig`/`AgentGuidanceTarget`);
read the selected work item's generated work model and any existing generated
guidance; derive a single normalized guidance model from the work model; render
that model into a deterministic per-target generated guidance view (a structured
`guidance.json` manifest plus its Markdown projection) for every configured
target; evaluate generated-view currency and Claude/Codex behavior equivalence;
emit a deterministic JSON/text command report; and identify the next action.

This command is **not** a new lifecycle authoring stage. The lifecycle stage
chain still ends at `ship` (`nextLifecycleCommand Ship = None`); `agents` is a
generator over the normalized work model that can be run whenever a current work
model exists, and `nextLifecycleCommand Agents = None`. Like `analyze`, `verify`,
and `ship`, it authors no source artifact: its only writes are generated
guidance artifacts under each target's configured generated root. The normalized
work model and authored lifecycle artifacts remain authoritative; generated
guidance is a derived view and never a second source of truth (Constitution
VII). The command never rewrites authored specifications, plans, tasks, evidence
declarations, the `.fsgg/agents.yml` configuration, or the hand-owned
`CLAUDE.md`/`AGENTS.md` guidance-target files; it inspects authored intent plus
the work model and refreshes generated guidance facts.

Generated guidance for both targets is derived from the **same** normalized
guidance model, so Claude and Codex behavior is equivalent by construction; the
equivalence check is a guardrail that blocks a current-guidance outcome when a
configured target's existing generated guidance, or its derived behavior, would
diverge from the shared model while `requireEquivalentClaudeAndCodexBehavior` is
set. Governance effective-evidence freshness, route selection, profile
adjustment, gate selection, protected-boundary enforcement, audit verdicts, and
release gating are out of scope for this slice.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects; BCL, FSharp.Core,
System.Text.Json, and YamlDotNet through the artifact-model library; xUnit test
packages for artifact, command, report, fixture, CLI smoke, and public-surface
tests. The feature reuses the existing `AgentGuidanceConfig`,
`AgentGuidanceTarget`, and `parseAgentGuidanceConfig` contracts plus the
`GeneratedViewKind.AgentCommands` and `ArtifactWriteKind.AgentGuidanceTarget`
markers already present in the artifact and command libraries.

**Storage**: Filesystem lifecycle artifacts under `.fsgg/`, `work/<id>/`, and
`readiness/<id>/`; reads target `.fsgg/project.yml`, `.fsgg/sdd.yml`,
`.fsgg/agents.yml`, the selected `readiness/<id>/work-model.json` state, and any
existing generated guidance under each target's
`readiness/<id>/agent-commands/<target>/` root; no authored writes are planned;
generated writes target each configured target's generated root (for the
default configuration, `readiness/<id>/agent-commands/claude/` and
`readiness/<id>/agent-commands/codex/`)

**Schema/Migration**: The generated per-target guidance manifest
(`agent-commands/<target>/guidance.json`) and the agent-guidance command report
JSON use `schemaVersion: 1` for this feature. The migration posture is
diagnose-only: current schema version 1 is accepted; missing, malformed, future,
unsupported, or deprecated generated-guidance or `.fsgg/agents.yml` schema
versions are reported as configuration or generated-view diagnostics until a
later feature defines an explicit migration path. Breaking schema changes
require updated contracts, fixtures, surface baselines, and migration notes
before implementation.

**Testing**: `dotnet test` with xUnit; `.fsi` public surface and baseline tests;
focused agent-guidance view artifact-model tests (manifest parse, source-digest
currency, schema compatibility); command workflow tests; generate/rerun/refresh
tests; authored-source preservation tests (authored lifecycle artifacts,
`.fsgg/agents.yml`, and `CLAUDE.md`/`AGENTS.md` unchanged); blocked-disposition
tests for missing/malformed configuration, no configured targets, missing or
stale work model, malformed work model, malformed work id, duplicate work id,
unknown reference, malformed and stale generated guidance, and Claude/Codex
divergence; deterministic JSON/report tests; text projection tests; no-Governance
tests; CLI JSON/dry-run/text smoke tests; FSI or prelude evidence for the public
agent-guidance surface

**Target Platform**: Cross-platform .NET command library, console executable, and
tests on Linux/macOS/Windows

**Project Type**: F# command workflow library plus thin console executable over
the existing artifact-model library and fixture corpus

**Performance Goals**: Generate, refresh, or rerun the `agents-create`,
`agents-rerun-current`, and `agents-refreshes-stale` fixture scenarios in under
2 seconds each when run through the command test harness on the local
development machine; produce byte-identical guidance manifests and JSON reports
for three identical dry-run agent-guidance executions over the
deterministic-report fixture

**Constraints**: `.fsi` signatures precede public `.fs` implementation changes;
Markdown and YAML remain authoring surfaces while schema-versioned structured
artifacts remain the machine contract; generated guidance is derived readiness
data, not authored lifecycle source, and its presence is not proof of currency;
generated guidance is never a second source of truth (Constitution VII); the
command does not implement Governance effective-evidence freshness, route
selection, profile selection, gate enforcement, protected-boundary enforcement,
audit, or release behavior; reports and generated guidance exclude implicit
clocks, durations, terminal width, ANSI styling, directory enumeration order,
host-specific path separators, random values, and absolute host paths; dry-run
reports proposed generated changes without mutating files; optional Governance
pointers stay advisory compatibility facts

**Scale/Scope**: One initialized SDD project and one selected work item per
command invocation; one new cross-cutting command (`agents`); per-target
generated guidance manifest plus Markdown projection create/refresh, derived
guidance model, Claude/Codex equivalence guardrail, generated-view currency
handling, deterministic output, fixtures, dry-run behavior, and optional
Governance boundary facts only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Agent guidance must update `.fsi` signatures for the generated guidance manifest facts, derived guidance model, agent-guidance summary, finding/disposition/diagnostic contracts, workflow/report/effect contracts, and the `Agents` command-union addition before `.fs` behavior, and must exercise the public agent-guidance surface through FSI or prelude evidence. | PASS |
| II. Structured Artifacts Are the Machine Contract | The plan declares the generated per-target `guidance.json` manifest, schema version 1, source relationships, derived guidance model, generated-view currency, command report JSON, stale behavior, and diagnostics; the Markdown guidance projection is rendered from the manifest; authored Markdown/YAML sources, `.fsgg/agents.yml`, and the work model remain prior facts and are never rewritten. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Public command and artifact-model API changes remain in `.fsi` files and surface-baseline tests must be updated deliberately. | PASS |
| IV. Idiomatic Simplicity Is the Default | The implementation continues with records, discriminated unions, modules, explicit effects, deterministic serialization, and existing parsing/rendering helpers; no framework, reflection, or dynamic command machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | Agent guidance uses the existing `Model`, `Msg`, `Effect`, `init`, `update`, and edge-interpreter boundary, with pure guidance derivation and equivalence evaluation before any generated filesystem writes; the constitution names agent-command writers as an MVU-boundary case. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires failing agent-guidance semantic tests, real filesystem smoke paths, golden JSON/manifest/report tests, authored-source preservation tests, blocked-disposition diagnostics, generated-view diagnostics, divergence diagnostics, no-Governance tests, and FSI evidence. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Generated guidance is derived from the normalized work model for both Claude and Codex from one shared guidance model; the manifest is the structured contract and Markdown is its projection; generated guidance is explicitly not a second source of truth; stale or divergent guidance is a visible diagnostic. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover outside-project use, missing/malformed `.fsgg/agents.yml`, no configured targets, malformed or missing work id, missing/stale/malformed work model, unknown references, malformed/stale generated guidance, Claude/Codex divergence, and optional Governance boundary issues; agent-command generation errors are an explicitly named diagnostic family in the constitution. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/014-agent-guidance/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- agent-guidance-view.md
|   |-- agents-command.md
|   |-- agent-guidance-report-json.md
|   `-- agent-guidance-fixtures.md
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
|   |   analysis, verification, ship, generation manifest, and serialization
|   |   contracts (incl. AgentGuidanceConfig / AgentGuidanceTarget and the
|   |   GeneratedViewKind.AgentCommands marker)
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
|   |-- AgentGuidanceViewTests.fs
|   |-- ShipViewTests.fs
|   |-- GeneratedModelCurrencyTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- existing artifact-model and work-model tests
|-- FS.GG.SDD.Commands.Tests/
|   |-- AgentsCommandTests.fs
|   |-- CommandReportJsonTests.fs
|   |-- TextProjectionTests.fs
|   |-- GeneratedViewCommandTests.fs
|   |-- GovernanceBoundaryCommandTests.fs
|   |-- SurfaceBaselineTests.fs
|   `-- TestSupport.fs
`-- fixtures/
    `-- lifecycle-commands/
        |-- agents-create/
        |-- agents-rerun-current/
        |-- agents-preserves-authored/
        |-- agents-refreshes-stale/
        |-- agents-claude-only/
        |-- agents-codex-only/
        |-- agents-claude-and-codex/
        |-- missing-agents-config/
        |-- malformed-agents-config/
        |-- no-targets/
        |-- missing-work-model/
        |-- stale-work-model/
        |-- malformed-work-model/
        |-- malformed-work-id/
        |-- duplicate-work-id/
        |-- unknown-source-reference/
        |-- stale-generated-guidance/
        |-- malformed-generated-guidance/
        |-- claude-codex-divergence/
        |-- outside-project/
        |-- dry-run/
        |-- deterministic-report/
        |-- text-projection/
        `-- governance-boundary/
```

**Structure Decision**: Extend the existing `FS.GG.SDD.Artifacts`,
`FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects rather than adding new
projects. `agents` has a similar operational shape to the preceding
generated-view commands `analyze`, `verify`, and `ship`: load project context,
validate one work item, inspect authored configuration plus the generated work
model, evaluate SDD-owned readiness, refresh or diagnose a generated view, and
emit one deterministic report. Unlike those lifecycle-stage commands, `agents`
is cross-cutting (not part of the charter->ship stage chain), it consumes the
normalized work model rather than re-deriving lifecycle facts, it writes a
per-target generated view (one manifest plus Markdown projection per configured
target) instead of a single readiness file, and it adds a Claude/Codex
equivalence guardrail. The shared blocked-fixture roots (`outside-project`,
`malformed-work-id`, `duplicate-work-id`, `unknown-source-reference`, `dry-run`,
`deterministic-report`, `text-projection`, `governance-boundary`) follow the
established naming with agent-guidance-specific expected outputs, and new roots
cover configuration, target, work-model, and divergence cases unique to this
feature.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/agent-guidance-view.md](contracts/agent-guidance-view.md)
- [contracts/agents-command.md](contracts/agents-command.md)
- [contracts/agent-guidance-report-json.md](contracts/agent-guidance-report-json.md)
- [contracts/agent-guidance-fixtures.md](contracts/agent-guidance-fixtures.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: agent-guidance additions are planned through public `.fsi` artifact, work-model-consuming, command type/report/effect, diagnostic, serialization, and rendering contracts, with surface-baseline tests before implementation bodies. |
| Structured machine contract | PASS: the generated per-target `guidance.json` manifest, derived guidance model, generated-view currency, and command report JSON are structured contracts; the Markdown guidance projection renders from the manifest; authored Markdown/YAML sources, `.fsgg/agents.yml`, and the work model remain prior facts and are never rewritten; schema version 1 has diagnose-only migration posture. |
| Public API baseline | PASS: any guidance manifest, command type/report/effect, generated-view, or diagnostic changes require baseline updates and FSI evidence. |
| MVU boundary | PASS: project loading, configuration and work-model validation, guidance derivation, equivalence evaluation, generated-view refresh planning, and report building remain behind pure workflow transitions and explicit effects. |
| Evidence | PASS: quickstart and fixture contracts define generated guidance create/refresh, rerun currency, authored-source preservation, dry-run, missing/malformed configuration, no targets, missing/stale/malformed work model, unknown reference, malformed/stale generated guidance, Claude/Codex divergence, deterministic JSON, text projection, no-Governance, FSI, and build evidence. |
| Agent contract | PASS: generated guidance is derived from the work model for Claude and Codex from one shared model, marked generated with source digests, and is never a second source of truth; agent context points at this plan. |
| Safe failure | PASS: diagnostics identify the affected artifact or target, stable id, severity, correction, and next action before generated writes proceed; authored sources, `.fsgg/agents.yml`, and the hand-owned guidance-target files are never mutated. |

No new complexity exceptions were introduced by Phase 1 design.
