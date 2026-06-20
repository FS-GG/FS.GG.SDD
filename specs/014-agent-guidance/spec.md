# Feature Specification: Agent Guidance Generation

**Feature Branch**: `014-agent-guidance`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
generated agent-guidance view, Claude/Codex guidance derived from the normalized
work model, command report, generated-view currency behavior, equivalence
obligation, diagnostics, and optional Governance boundary facts)

**Input**: User description: "Start the next item on the implementation plan.
With the `charter -> ship` lifecycle complete, the next SDD-owned item is Phase 8
of `docs/initial-implementation-plan.md`: generate Claude and Codex agent command
and skill guidance from the normalized lifecycle model into
`readiness/<id>/agent-commands/`, marked as generated with source digests and
stale-guidance diagnostics, while keeping agent guidance from becoming a second
source of truth."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Agent Guidance From the Lifecycle Model (Priority: P1)

As a project maintainer or coding agent, I need to generate Claude and Codex
agent guidance from the selected work item's normalized lifecycle model so that
human and agent workflows share one contract and the generated guidance is
recorded as a deterministic generated view at `readiness/<id>/agent-commands/`,
not as an authored source of lifecycle intent.

**Why this priority**: The lifecycle authoring commands (`charter` through
`ship`) already produce the normalized work model, but agents still lack
guidance that is provably derived from that model. Without this slice, Claude and
Codex instructions are hand-maintained and can silently drift from the lifecycle
contract, becoming a competing source of truth.

**Independent Test**: Can be tested by running the agent-guidance command in an
initialized SDD project with a valid work item and confirming that generated
Claude and Codex guidance, source relationships, generator identity,
generated-view state, diagnostics, and next action are produced from the
normalized work model without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project, a valid `.fsgg/agents.yml` guidance
   configuration, and a work item with a current normalized work model, **When**
   the user runs the agent-guidance command, **Then** the selected work item has
   generated agent guidance under `readiness/<id>/agent-commands/` that records
   the source work-model relationship, source digests, schema versions, generator
   identity, per-target Claude and Codex guidance, and a generated marker
   declaring the output is derived and not an authored source.
2. **Given** the guidance configuration declares both a Claude target and a Codex
   target, **When** the command completes, **Then** each configured target has
   generated guidance derived from the same normalized lifecycle model, and the
   command report names the selected work id, the generated guidance artifacts,
   the parsed configuration facts, the generated-view state, diagnostics,
   outcome, and the next action.
3. **Given** optional Governance files are absent, **When** the user generates
   agent guidance, **Then** SDD-only guidance generation still succeeds and does
   not ask Governance to evaluate evidence freshness, routes, profiles, gates,
   audit, release policy, or protected-boundary enforcement.

---

### User Story 2 - Detect Stale and Divergent Agent Guidance (Priority: P1)

As a maintainer or agent, I need stale generated agent guidance and any
Claude/Codex behavior divergence to be reported precisely so that I can refresh
the right guidance before an agent acts on instructions that no longer match the
lifecycle contract.

**Why this priority**: Generated guidance is an output; its presence is not proof
of currency. If lifecycle sources change after guidance was generated, or if one
agent target would receive workflow behavior the other does not, an agent could
follow instructions that contradict the current work model. Stale or divergent
guidance must be a visible finding, not a silent pass.

**Independent Test**: Can be tested by running the command against work items
whose generated guidance is missing, stale, malformed, or behaviorally divergent
across targets and verifying that no generated guidance is treated as current
until the report identifies the affected target, identifier, severity, and
correction.

**Acceptance Scenarios**:

1. **Given** generated agent guidance exists but its source digests, schema
   version, or generator identity no longer match the current normalized work
   model, **When** the command evaluates generated-view currency, **Then** the
   report records a stale-guidance diagnostic naming the affected target and
   source instead of treating the existing guidance file as current.
2. **Given** the guidance configuration requires equivalent Claude and Codex
   behavior, **When** the generated guidance for the configured targets would
   describe different workflow behavior for the same lifecycle model, **Then** the
   command reports an equivalence diagnostic and blocks a current-guidance
   outcome until the divergence is resolved.
3. **Given** the normalized work model is missing, stale, malformed, or blocked
   by invalid source data, **When** the command runs, **Then** the report records
   a generated-view diagnostic for the work model and does not generate or refresh
   agent guidance from an unusable model.

---

### User Story 3 - Keep Authored Sources and Agent Files Authoritative (Priority: P2)

As a user or agent, I need agent-guidance generation to be a non-destructive
generator so that diagnostics and generated guidance never silently rewrite
authored lifecycle artifacts, the `.fsgg/agents.yml` configuration, or hand-owned
agent context files, and so that generated guidance never becomes a second source
of truth.

**Why this priority**: Per the constitution, agent prompts and skills may help
author files but are not a second source of truth. Guidance generation must be
safe to run repeatedly during authoring, review, and CI, writing only generated
artifacts and leaving authored intent untouched.

**Independent Test**: Can be tested by running the command in current, stale, and
dry-run scenarios and confirming that authored lifecycle artifacts and the
guidance configuration remain unchanged while generated guidance and reports
reflect the current source state.

**Acceptance Scenarios**:

1. **Given** authored lifecycle artifacts and the `.fsgg/agents.yml`
   configuration already exist, **When** the user runs the agent-guidance
   command, **Then** the command does not create, update, reorder, normalize, or
   remove authored source artifacts or the configuration.
2. **Given** generated guidance can be safely refreshed, **When** the command
   runs normally, **Then** only generated guidance artifacts under the configured
   generated root are created or updated, each is marked as generated with source
   digests, and the report records the generated artifact operation.
3. **Given** the user requests a dry run, **When** the command completes, **Then**
   proposed generated guidance changes, diagnostics, generated-view state, and
   next action are reported without modifying authored or generated artifacts.

---

### User Story 4 - Keep Generated Guidance Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need generated
agent guidance to be deterministic and traceable so that humans, Claude, Codex,
and downstream tooling all read the same lifecycle-derived guidance with explicit
provenance.

**Why this priority**: Agent guidance is the surface that keeps human and agent
workflows on one contract. The generated-guidance contract must be stable and
traceable before agents and CI rely on it.

**Independent Test**: Can be tested by running the same guidance request against
the same project state multiple times and confirming that generated guidance and
machine-readable reports are stable, that each generated file identifies its
sources and generator identity, that plain text summaries contain no extra facts,
and that optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and identical guidance input, **When** the
   command is run repeatedly, **Then** generated guidance and machine-readable
   reports are identical for each run.
2. **Given** a user requests a human-readable summary, **When** the command
   completes, **Then** the summary reflects the same generated targets,
   generated-view state, divergence findings, diagnostics, outcome, and next
   action as the authoritative command report.
3. **Given** optional Governance policy, capability, tooling, route, profile,
   gate, audit, or enforcement pointers are present in SDD-owned sources, **When**
   the command completes, **Then** the report may expose those pointers as
   compatibility facts but does not interpret or enforce Governance-owned
   decisions.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The `.fsgg/agents.yml` guidance configuration is missing, malformed, stale,
  schema-incompatible, or declares no guidance targets.
- The guidance configuration points the work-model path or a target's generated
  root at a missing or invalid location.
- The selected work id is empty, malformed, duplicated, or inconsistent with the
  normalized work model.
- The normalized work model is missing, stale, malformed, or references unknown
  lifecycle facts, so guidance cannot be derived from a current model.
- Generated guidance exists but its source digests, schema version, or generator
  identity no longer match the current normalized work model.
- The guidance configuration requires equivalent Claude and Codex behavior, but
  the configured targets would receive divergent workflow behavior.
- Only one agent target is configured, both are configured, or additional future
  agent targets are configured.
- Lifecycle sources change after guidance captured a source snapshot, leaving
  generated guidance stale.
- The user requests a dry run and expects proposed guidance changes and
  diagnostics without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for SDD-owned agent guidance and compatibility
  facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd agents` as a native SDD command
  that generates agent command and skill guidance from the normalized lifecycle
  model, without introducing a new lifecycle authoring stage between any existing
  stages.
- **FR-002**: The command MUST require an initialized SDD project, one valid
  selected work id, a valid `.fsgg/agents.yml` guidance configuration, and a
  current normalized work model before it generates or refreshes agent guidance.
- **FR-003**: The command MUST load and validate the guidance configuration,
  including schema version, configured agent targets, each target's guidance path
  and generated root, the work-model source path, the generated-guidance
  authority posture, and the require-equivalent-Claude-and-Codex-behavior setting.
- **FR-004**: The command MUST derive Claude and Codex guidance from the same
  normalized work model for the selected work item rather than from independently
  authored agent instructions.
- **FR-005**: The command MUST generate or refresh the selected work item's
  agent-guidance view under `readiness/<id>/agent-commands/` when valid source
  data exists, guidance output is needed, and the run is not a dry run.
- **FR-006**: Each generated guidance artifact MUST be marked as generated and
  MUST record the source work-model relationship, source digests, schema
  versions, generator identity, and target identity so it can be traced back to
  declared sources.
- **FR-007**: The command MUST generate guidance for every configured agent
  target, supporting at least Claude and Codex targets, and MUST report which
  targets were generated, refreshed, or refused.
- **FR-008**: The command MUST report stale generated agent guidance when a
  target's generated guidance no longer matches the current normalized work
  model's source digests, schema version, or generator identity.
- **FR-009**: The command MUST evaluate Claude and Codex behavior equivalence and,
  when the configuration requires equivalent behavior, MUST block a
  current-guidance outcome and emit an equivalence diagnostic if the configured
  targets would describe divergent workflow behavior for the same lifecycle model.
- **FR-010**: The command MUST NOT create, update, reorder, normalize, or remove
  authored lifecycle artifacts, the `.fsgg/agents.yml` configuration, or
  hand-owned agent context files.
- **FR-011**: Generated agent guidance MUST NOT become a second source of truth;
  the normalized work model and authored lifecycle artifacts remain authoritative
  and the generated guidance is a view over them.
- **FR-012**: The command MUST refresh or diagnose the currency of the normalized
  work model it depends on, and MUST NOT generate guidance from a missing, stale,
  malformed, or blocked work model.
- **FR-013**: The command MUST map the selected work item's agent guidance to a
  current disposition of generated-current, stale, blocked, or advisory based on
  configuration validity, work-model currency, generated-view state, and
  equivalence findings.
- **FR-014**: The command MUST block guidance generation for invalid project
  context, missing or malformed guidance configuration, no configured targets,
  selected-id mismatches, duplicated logical work ids, a missing or not-current
  work model, unknown references, malformed generated guidance, unresolved
  Claude/Codex divergence when equivalence is required, and unsafe generated-view
  refresh conditions.
- **FR-015**: The command MUST identify regenerating downstream guidance or
  continuing the lifecycle as the next action after a successful
  generated-current result, and MUST identify configuration correction,
  work-model refresh, divergence resolution, or stale-source correction as the
  next action when blocking findings remain.
- **FR-016**: The command MUST report generated guidance artifacts, refreshed
  guidance artifacts, refused guidance artifacts, preserved authored artifacts,
  parsed configuration facts, generated targets, generated-view state, divergence
  findings, diagnostics, outcome, and next action in the authoritative command
  report.
- **FR-017**: Machine-readable guidance reports and generated guidance artifacts
  MUST be deterministic for identical project state and identical guidance input.
- **FR-018**: Human-readable guidance summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate guidance facts.
- **FR-019**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact or target that
  needs correction when available.
- **FR-020**: Guidance diagnostics MUST use stable identifiers and include the
  affected artifact or target, severity, explanation, and user-correctable
  action.
- **FR-021**: Dry-run guidance requests MUST report proposed generated guidance
  changes, diagnostics, generated-view state, and next action without modifying
  authored or generated artifacts.
- **FR-022**: The command MUST work when Governance is not installed or
  configured.
- **FR-023**: Optional Governance policy, capability, tooling, routing, profile,
  gate, audit, enforcement, and release facts MUST remain advisory compatibility
  facts in guidance reports and MUST NOT be interpreted as SDD-owned enforcement
  decisions.
- **FR-024**: The feature MUST NOT introduce Governance effective-evidence
  freshness, route selection, profile adjustment, gate selection,
  protected-boundary enforcement, audit verdicts, or release gating behavior.

### Key Entities

- **Agent Guidance Configuration**: The authored `.fsgg/agents.yml` settings that
  declare guidance targets, each target's guidance path and generated root, the
  work-model source path, the generated-guidance authority posture, and whether
  equivalent Claude and Codex behavior is required.
- **Agent Guidance Target**: A configured agent surface (such as Claude or Codex)
  that receives guidance derived from the normalized work model.
- **Generated Agent Guidance View**: The generated `readiness/<id>/agent-commands/`
  artifacts that record lifecycle-derived Claude and Codex guidance, marked as
  generated, with source relationships, source digests, schema versions, and
  generator identity.
- **Guidance Disposition**: The current state of the selected work item's agent
  guidance, such as generated-current, stale, blocked, or advisory.
- **Equivalence Obligation**: The requirement, when configured, that Claude and
  Codex targets describe equivalent workflow behavior for the same lifecycle
  model.
- **Agent Guidance Report**: The authoritative result of an agent-guidance
  command, including context, generated and refused guidance artifacts, parsed
  configuration facts, generated targets, generated-view state, divergence
  findings, diagnostics, outcome, and next action.
- **Guidance Diagnostic**: A stable finding that explains invalid project context,
  missing or malformed guidance configuration, a missing or not-current work
  model, stale generated guidance, Claude/Codex divergence, or optional boundary
  issues.
- **Optional Boundary Fact**: An advisory SDD report fact that exposes
  Governance-compatible context without evaluating freshness, routing, profiles,
  gates, audit, enforcement, or release policy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a valid guidance configuration
  and a current work model, a user can generate Claude and Codex agent guidance
  and receive the next action in one command result.
- **SC-002**: 100% of valid guidance fixture families (`agents-create`,
  `agents-rerun-current`, `agents-preserves-authored`, `agents-refreshes-stale`,
  `agents-claude-only`, `agents-codex-only`, `agents-claude-and-codex`,
  `dry-run`, `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected generated guidance or proposed
  dry-run changes, selected work-id trace, generated targets, successful outcome,
  and correct next action.
- **SC-003**: 100% of blocked guidance fixture families (`outside-project`,
  `missing-agents-config`, `malformed-agents-config`, `no-targets`,
  `missing-work-model`, `stale-work-model`, `malformed-work-model`,
  `malformed-work-id`, `duplicate-work-id`, `unknown-source-reference`,
  `stale-generated-guidance`, `malformed-generated-guidance`, and
  `claude-codex-divergence`) leave authored content unchanged and include at
  least one actionable diagnostic.
- **SC-004**: Three repeated guidance runs over identical inputs produce
  identical generated guidance artifacts and machine-readable command reports.
- **SC-005**: 100% of missing-config, malformed-config, missing or stale
  work-model, stale generated-guidance, malformed generated-guidance, and
  Claude/Codex divergence scenarios identify the affected artifact, target, or
  identifier before blocking a current-guidance outcome.
- **SC-006**: Dry-run guidance requests change 0 authored or generated files
  while still reporting proposed generated guidance, diagnostics, generated-view
  state, and next action.
- **SC-007**: Maintainers can identify the generated guidance artifacts, the
  generated target list, the generated-view state, divergence findings, and next
  action from the human-readable summary during review.
- **SC-008**: Every generated guidance artifact identifies its source work model,
  source digests, generator identity, and generated marker, so a reviewer can
  confirm the guidance was derived from the lifecycle model and is not an authored
  source of truth.
- **SC-009**: Agent guidance generation remains usable without Governance
  installed in every no-Governance validation scenario.

## Assumptions

- With the `charter -> ship` lifecycle commands complete, the next SDD-owned item
  in `docs/initial-implementation-plan.md` is Phase 8 agent guidance generation,
  which the `013-ship-command` plan explicitly deferred as out of scope.
- `fsgg-sdd init` already provisions `.fsgg/agents.yml`, and the artifact-model
  library already parses the guidance configuration (`AgentGuidanceConfig`,
  `AgentGuidanceTarget`) and exposes the generated agent-commands artifact in the
  generation manifest; this feature adds the command and generator that produce
  the generated view from those contracts.
- The command operates on one selected work item at a time and writes generated
  guidance under the configured generated root for each target.
- The normalized work model and authored lifecycle artifacts remain the source of
  truth; generated agent guidance is a view over them and is never an authored
  source of lifecycle intent.
- Claude and Codex targets are the initial supported agent surfaces, and the
  configuration may declare one, both, or additional future targets.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Effective evidence freshness, route selection, profile adjustment, gate
  selection, protected-boundary enforcement, audit verdicts, and release policy
  remain Governance-owned concerns.
- Optional Governance files may be referenced for compatibility, but SDD remains
  independently usable without Governance installed.
