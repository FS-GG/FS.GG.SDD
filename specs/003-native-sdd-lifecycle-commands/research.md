# Research: Native SDD Lifecycle Commands

## Command Project Shape

Decision: Add a public `FS.GG.SDD.Commands` library and a thin
`FS.GG.SDD.Cli` executable.

Rationale: Command behavior needs a public, testable boundary before real
filesystem effects are interpreted. A command library lets tests exercise
`Model`, `Msg`, `Effect`, `init`, `update`, report serialization, text
projection, and diagnostics without shelling out for every case. The CLI can
stay small: parse arguments, invoke the workflow, interpret effects, and write
the selected projection.

Alternatives considered:

- Extend `FS.GG.SDD.Artifacts`: rejected because artifacts already own the
  lifecycle machine contract and pure work-model generation; command workflow
  and filesystem mutation would blur that package boundary.
- Put all logic in the executable: rejected because it would make semantic
  tests harder and would hide public command contracts behind process behavior.

## CLI Parsing Dependency

Decision: Use explicit BCL-based argument parsing for this feature and defer
external CLI/presentation packages.

Rationale: The first command surface is a fixed, small command family with
stable options. Keeping parsing explicit avoids adding a framework dependency
before the command contract is stable and aligns with the constitution's
preference for simple F# and the standard library. Plain text output can be a
small renderer over the same report object; richer presentation can be planned
with release/distribution work.

Alternatives considered:

- `System.CommandLine`: deferred because the current feature does not need
  advanced parser services, completion, or command discovery.
- Argu: deferred because it would add another public dependency before the
  stable `fsgg-sdd` argument contract is known.
- Spectre.Console: rejected for this phase because human presentation must not
  become a source of facts and richer terminal UX is release-readiness work.

## Command Workflow Boundary

Decision: Model every stateful command as `Model`, `Msg`, `Effect`, `init`,
`update`, and an edge effect interpreter.

Rationale: Initialization, lifecycle artifact updates, generated-view refresh,
and analysis all read and write filesystem state. The pure transition boundary
lets tests verify planned effects, refused overwrites, diagnostics, next
actions, and reports before any file is changed. The interpreter becomes the
only place that touches the host filesystem.

Alternatives considered:

- Direct service functions that read and write files: rejected because they
  would be harder to test before implementation and would violate the
  constitution's MVU boundary for stateful workflows.
- A broad effect abstraction with arbitrary process execution: rejected because
  this feature does not need external command execution and should keep the
  effect vocabulary auditable.

## Command Reports As The Automation Contract

Decision: Define deterministic `CommandReport` JSON as the authoritative
command result and make plain text a projection over the same value.

Rationale: CI, humans, and agents must not receive different lifecycle facts.
The JSON report records command identity, selected context, changed artifacts,
generated-view currency, diagnostics, outcome, and next action. Text rendering
uses the same value and does not add facts. Determinism is preserved by
documented property ordering, stable sort keys, repository-relative paths, and
no implicit timestamps.

Alternatives considered:

- Let stdout text be primary and parse it in automation: rejected because text
  wrapping, ANSI, locale, and formatting changes would become contract changes.
- Include wall-clock timestamps in reports: rejected for the authoritative
  report because identical inputs must produce byte-identical JSON.

## Generated-View Refresh Behavior

Decision: Commands refresh `readiness/<id>/work-model.json` through the existing
artifact-model generation API whenever source data is valid enough, and
`analyze` also emits `readiness/<id>/analysis.json`.

Rationale: Phase 3 already established the normalized work model as the shared
machine contract. Command features should use it instead of introducing a
parallel command-specific work graph. When sources are missing, malformed, or
conflicting, commands emit stale or blocked generated-view diagnostics instead
of treating old generated files as current.

Alternatives considered:

- Require a separate refresh command first: rejected because each lifecycle
  command must be able to report generated-view currency as part of its result.
- Treat generated files as current when present: rejected because generated
  views are outputs, not proof of currency.

## Initialization Skeleton

Decision: `fsgg-sdd init` creates the minimum SDD skeleton: `.fsgg/project.yml`,
`.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`, `readiness/`, and safe Claude and
Codex guidance target handling.

Rationale: A project must be able to start the lifecycle without Governance,
Rendering, product templates, or source code. The skeleton records SDD-owned
lifecycle settings, work and readiness roots, generated-view policy, and agent
target paths. If `CLAUDE.md` or `AGENTS.md` already exists, init preserves user
content and reports any unsafe update instead of overwriting it.

Alternatives considered:

- Create Governance policy and capability files by default: rejected because
  Governance integration is optional and versioned.
- Generate full agent command guidance during init: rejected because generated
  agent guidance belongs to a later roadmap phase.

## Lifecycle Artifact Paths

Decision: Native commands operate over `.fsgg` plus `work/<id>`:
`charter.md`, `spec.md`, `clarifications.md`, `checklist.md`, `plan.md`,
`contracts/`, and `tasks.yml`.

Rationale: The initial implementation plan already names this as the SDD source
model. Keeping typed task data in `tasks.yml` preserves the structured machine
contract while allowing Markdown files to remain authoring surfaces.

Alternatives considered:

- Reuse Spec Kit `specs/<id>` as the native command layout: rejected because
  this repository may continue using Spec Kit internally, but FS.GG.SDD's
  consumer contract is `.fsgg` plus `work/<id>`.
- Store all lifecycle data in Markdown only: rejected because structured,
  schema-versioned artifacts are the machine contract.

## Governance Boundary

Decision: Commands may report optional Governance compatibility facts, but they
do not parse Governance policy, select routes, evaluate freshness, adjust
profiles, select gates, or produce protected-boundary verdicts.

Rationale: FS.GG.SDD must remain independently useful without Governance. The
boundary keeps lifecycle authoring and generated SDD views in this repository
while preserving Governance ownership of route, freshness, profiles, gates, and
enforcement.

Alternatives considered:

- Run Governance checks from `analyze`: rejected because that would make
  Governance required for the native SDD command phase and would cross the
  product boundary.
- Ignore Governance pointers entirely: rejected because SDD should expose
  explicit optional facts that Governance can consume later.
