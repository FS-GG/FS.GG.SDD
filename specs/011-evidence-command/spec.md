# Feature Specification: Evidence Command

**Feature Branch**: `011-evidence-command`

**Created**: 2026-06-19

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
authored evidence declarations, evidence obligation dispositions, lifecycle
readiness facts, command report, generated-view currency behavior, diagnostics,
and optional Governance boundary facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: begin Phase 6 task/evidence/verify/ship
readiness by adding `fsgg-sdd evidence` for authored evidence declarations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare Evidence For Completed Work (Priority: P1)

As a project maintainer or coding agent, I need to record evidence for
implemented SDD tasks so that completed work has visible support before verify
or ship readiness is evaluated.

**Why this priority**: Analysis can identify whether a work item is ready for
implementation, but users still need a durable SDD-owned way to declare what
evidence supports completed tasks, accepted deferrals, synthetic evidence, and
verification results. Without this slice, verify and ship readiness cannot
distinguish supported completion from unsupported task status.

**Independent Test**: Can be tested by running the evidence command in an
initialized SDD project for one analyzed work item and confirming that evidence
declarations, task links, evidence obligation dispositions, command report,
generated-view state, diagnostics, and next action are produced without
requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and an analyzed work item with task
   evidence obligations, **When** the user records evidence for completed
   tasks, **Then** the selected work item has an authored evidence declaration
   artifact containing work identity, stable evidence ids, linked task ids,
   linked requirement or decision references when known, evidence kind, source
   reference, result state, synthetic disclosure when applicable, and lifecycle
   notes.
2. **Given** all completed tasks have real evidence or accepted deferrals that
   remain visible to later lifecycle stages, **When** the command completes,
   **Then** the command report names the selected work id, changed evidence
   artifact, parsed evidence facts, evidence readiness state, generated-view
   state, diagnostics, outcome, and `verify` as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user records
   evidence, **Then** SDD-only evidence declaration still succeeds and does not
   ask Governance to evaluate freshness, route, profile, gate, audit, or
   enforcement policy.

---

### User Story 2 - Preserve And Update Evidence Safely (Priority: P1)

As a contributor maintaining implementation evidence, I need evidence updates
to preserve stable evidence ids, task links, result meaning, and deferral
rationales while detecting stale or unsafe declarations, so that evidence
history is not silently rewritten.

**Why this priority**: Evidence declarations become the bridge between task
completion, verification readiness, ship readiness, CI, agents, and optional
Governance consumers. Existing evidence must remain stable unless a user
intentionally adds a safe update or records a replacement that remains
traceable.

**Independent Test**: Can be tested by running the evidence command against a
work item with existing evidence declarations and verifying that compatible
additions preserve existing declarations, stale task or source links are
reported, and conflicting updates block before writing.

**Acceptance Scenarios**:

1. **Given** an existing evidence artifact with stable evidence ids, linked
   tasks, result states, source references, synthetic disclosures, and accepted
   deferral rationales, **When** the user adds compatible evidence, **Then**
   existing evidence facts are preserved and new evidence entries are added
   with stable ids.
2. **Given** a task, required evidence obligation, requirement, clarification
   decision, checklist result, plan decision, accepted deferral, or analysis
   finding has changed since evidence was recorded, **When** the command
   validates evidence readiness, **Then** affected declarations are reported as
   stale or needing review instead of being treated as current.
3. **Given** a proposed update would remove, renumber, reorder destructively,
   or change the meaning of an existing evidence declaration, source
   reference, result state, synthetic disclosure, or deferral rationale, **When**
   the command validates the update, **Then** it refuses to write and reports the
   conflict as a blocking diagnostic. This slice only allows preserving existing
   declarations and appending compatible new declarations; semantic replacement
   of existing declarations is out of scope.

---

### User Story 3 - Diagnose Missing Or Invalid Evidence (Priority: P2)

As a user or agent, I need evidence readiness problems to fail with actionable
diagnostics so that I can fix the correct task, declaration, or source
reference before verification proceeds.

**Why this priority**: Unsupported completion, stale task links, undisclosed
synthetic evidence, missing deferral rationale, and malformed evidence can make
verify and ship readiness misleading. SDD must surface these issues before
later stages rely on the declarations.

**Independent Test**: Can be tested by invoking the evidence command outside an
SDD project, before analysis, with missing tasks, completed tasks without
evidence, unknown task references, malformed evidence ids, stale evidence,
undisclosed synthetic evidence, and unsafe update attempts, then confirming
that no unsafe write occurs and each result contains a stable diagnostic and
correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the evidence command, **Then** no evidence artifact is created
   and the report explains how to initialize or select an SDD project.
2. **Given** the selected work item has not reached a valid analyzed state,
   **When** the user records evidence, **Then** no evidence artifact is created
   unless the command can safely report the missing prerequisite and preserve
   existing authored sources.
3. **Given** a completed task lacks required evidence or an accepted deferral,
   **When** evidence readiness is evaluated, **Then** the report identifies the
   task id, evidence obligation, affected source, and correction before
   allowing verification readiness.
4. **Given** evidence references an unknown task, unknown requirement, unknown
   decision, missing source, unsupported result state, synthetic evidence
   without disclosure, or deferral without rationale, **When** the command
   validates the declaration, **Then** the report identifies evidence
   correction as the next action.

---

### User Story 4 - Keep Evidence Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need
evidence command outputs to be deterministic and traceable so that humans,
agents, and downstream tooling all read the same evidence facts.

**Why this priority**: Evidence facts feed verify readiness, ship readiness,
generated summaries, and optional Governance freshness checks. The SDD
declaration contract and command reports must be stable before those stages
build on them.

**Independent Test**: Can be tested by running the same evidence request
against the same project state multiple times and confirming that
machine-readable reports are stable, plain text summaries contain no extra
facts, and optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and evidence input, **When** the evidence
   command is run repeatedly, **Then** the machine-readable report is identical
   for each run.
2. **Given** a user requests a human-readable summary, **When** the evidence
   command completes, **Then** the summary reflects the same changed artifacts,
   evidence counts, obligation dispositions, generated-view state,
   diagnostics, outcome, and next action as the authoritative command report.
3. **Given** optional Governance policy, capability, or tooling pointers are
   present in SDD-owned sources, **When** evidence declaration completes,
   **Then** the report may expose those pointers as compatibility facts but
   does not interpret or enforce Governance freshness, profiles, routes, gates,
   or protected-boundary policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the analyze stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, plan, tasks, analysis, or
  evidence artifact.
- The specification, clarification, checklist, plan, tasks, analysis, or
  evidence artifact is missing, malformed, stale, or references unknown
  lifecycle facts.
- An evidence declaration references an unknown task, requirement, acceptance
  scenario, clarification decision, checklist result, plan decision, accepted
  deferral, evidence obligation, or source artifact.
- A completed task lacks required evidence or an accepted deferral that remains
  visible to later lifecycle stages.
- Synthetic evidence is recorded without a disclosure explaining what real path
  it stands in for.
- An accepted deferral is recorded without rationale, owner, scope, or later
  lifecycle visibility.
- A source requirement, clarification decision, checklist result, plan decision,
  task, required skill, required evidence obligation, or analysis finding
  changes after evidence was recorded.
- Existing evidence ids, result states, source references, synthetic
  disclosures, or deferral rationales would be removed, renumbered, or
  semantically changed by the proposed update.
- Required project settings or artifact layout settings exist but are
  malformed, stale, or point to missing lifecycle roots.
- A generated work model or analysis view exists but its source digests, schema
  version, or generator identity no longer match the current authored sources.
- The user requests a dry run and expects proposed evidence changes and
  diagnostics without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for authored declarations and compatibility
  facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd evidence` as the next native
  SDD lifecycle command after `fsgg-sdd analyze` and implementation work.
- **FR-002**: The evidence command MUST require an initialized SDD project, one
  valid selected work id, valid prerequisite lifecycle artifacts, and a valid
  analyzed work-item state before it reports evidence readiness.
- **FR-003**: The evidence command MUST load and validate the selected work
  item's specification, clarification, checklist, plan, tasks, analysis, and
  evidence facts, including work identity, lifecycle state, requirement ids,
  user-story ids, acceptance-scenario ids, clarification decision ids,
  checklist result ids, plan decision ids, task ids, task dependencies,
  required skills, required evidence obligations, accepted deferrals, source
  snapshots, and blocking finding state.
- **FR-004**: The evidence command MUST create an evidence artifact for the
  selected work item when evidence declaration is needed and a safe evidence
  artifact does not already exist.
- **FR-005**: An evidence artifact MUST capture work identity, source artifact
  relationships, stable evidence ids, linked tasks, linked requirements or
  decisions when known, evidence kind, source reference, result state,
  synthetic evidence disclosure when applicable, accepted deferral rationale
  when applicable, and lifecycle notes needed before verification.
- **FR-006**: The evidence contract MUST expose stable identifiers for evidence
  declarations, with structured links to relevant tasks, requirements,
  acceptance scenarios, clarification decisions, checklist results, plan
  decisions, required evidence obligations, accepted deferrals, source
  artifacts, and generated views when known.
- **FR-007**: The evidence command MUST map required evidence obligations to
  evidence declarations so that every completed task and required evidence
  obligation has a visible disposition of supported, deferred, missing, stale,
  synthetic, or invalid.
- **FR-008**: The evidence command MUST distinguish real evidence, accepted
  deferrals, missing evidence, stale evidence, synthetic evidence with
  disclosure, synthetic evidence without disclosure, invalid references,
  advisory notes, and blocking evidence findings.
- **FR-009**: The evidence command MUST preserve existing authored evidence
  content, stable ids, task links, source references, result states, synthetic
  disclosures, accepted deferral rationales, and lifecycle notes unless it can
  report a safe, non-destructive update.
- **FR-010**: The evidence command MUST mark evidence declarations as stale or
  needing review when the source facts they reference have changed since the
  declaration was recorded.
- **FR-011**: The evidence command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate evidence ids, unknown
  source references, missing required identifiers, malformed existing evidence
  data, undisclosed synthetic evidence, unsupported result states, and
  unsupported destructive evidence updates before any authored artifact is
  changed.
- **FR-012**: The evidence command MUST report completed tasks without required
  evidence or accepted deferrals as evidence-readiness defects.
- **FR-013**: The evidence command MUST report missing required skills or
  capability tags when a declaration claims completion for work that required
  agent-driven capabilities not visible to the lifecycle artifacts.
- **FR-014**: The evidence command MUST record the selected work item's
  lifecycle state as evidence-ready only when all blocking evidence obligations
  are supported by real evidence or accepted deferrals that remain visible to
  later lifecycle stages.
- **FR-015**: The evidence command MUST identify `verify` as the next lifecycle
  action after a successful evidence-ready result.
- **FR-016**: The evidence command MUST identify implementation continuation,
  evidence correction, task correction, analysis rerun, or generated-view
  refresh as the next action when blocking evidence defects, stale task facts,
  unresolved obligations, missing skills, malformed identifiers, unknown
  references, unsupported synthetic evidence, or stale generated views remain.
- **FR-017**: The evidence command MUST report changed artifacts, preserved
  authored artifacts, refused artifacts, parsed source facts, evidence
  obligation counts, evidence disposition counts, generated-view state,
  diagnostics, outcome, and next action in the authoritative command report.
- **FR-018**: Machine-readable evidence reports MUST be deterministic for
  identical project state and identical evidence input.
- **FR-019**: Human-readable evidence summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-020**: The evidence command MUST refresh or explicitly diagnose the
  selected work item's generated SDD views when valid source data allows
  refresh and the run is not a dry run.
- **FR-021**: Dry-run evidence requests MUST report proposed authored and
  generated changes without modifying authored or generated artifacts.
- **FR-022**: Optional Governance policy, capability, tooling, freshness,
  routing, profile, gate, audit, and enforcement facts MUST remain advisory
  compatibility facts in SDD evidence reports and MUST NOT be interpreted as
  SDD-owned enforcement decisions.
- **FR-023**: The feature MUST NOT introduce verify readiness, ship readiness,
  Governance effective-evidence freshness, protected-boundary enforcement, or
  release gating behavior.

### Key Entities *(include if feature involves data)*

- **Evidence Artifact**: The authored work-item evidence source that records
  evidence declarations, source relationships, lifecycle state, and notes for
  one selected work item.
- **Evidence Declaration**: A stable declaration that links one or more tasks
  or obligations to supporting evidence, a result state, optional synthetic
  disclosure, optional deferral rationale, and source references.
- **Evidence Obligation**: A required proof point derived from tasks, planning
  decisions, lifecycle rules, required skills, required tests, accepted
  deferrals, or generated-view impacts.
- **Evidence Disposition**: The current state of an obligation, such as
  supported, deferred, missing, stale, synthetic, invalid, advisory, or
  blocking.
- **Evidence Source Reference**: A traceable pointer to the source that
  supports an evidence declaration, such as an implementation artifact,
  verification output, transcript, fixture, review record, or accepted
  deferral note.
- **Evidence Diagnostic**: A stable finding that identifies malformed,
  missing, stale, unsupported, undisclosed, or unsafe evidence data and the
  correction required before verification.
- **Optional Boundary Fact**: An advisory SDD report fact that exposes
  Governance-compatible context without evaluating freshness, routing,
  profiles, gates, or enforcement.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can record or update evidence for a standard analyzed work
  item and see evidence readiness in under 2 minutes.
- **SC-002**: 100% of completed tasks without required evidence or accepted
  deferrals are reported with the affected task id, missing obligation, and
  correction.
- **SC-003**: 100% of stale, malformed, unknown-reference, undisclosed
  synthetic, and unsafe overwrite evidence fixture cases produce a blocking or
  advisory diagnostic with the affected source and next action.
- **SC-004**: Repeated identical evidence requests over identical project state
  produce identical machine-readable reports.
- **SC-005**: Dry-run evidence requests change 0 authored or generated files
  while still reporting proposed changes and diagnostics.
- **SC-006**: Evidence declaration works in SDD-only projects with no
  Governance runtime installed across all acceptance fixtures.

## Assumptions

- The next applicable item from `docs/initial-implementation-plan.md` is the
  first SDD-owned slice of Phase 6 after `010-analyze-command`: authored
  evidence declarations before verify and ship readiness.
- `fsgg-sdd evidence` is the command name for this slice unless a later release
  decision chooses an equivalent `update` command.
- Evidence declarations are authored lifecycle sources under the selected work
  item; effective evidence freshness remains a Governance-owned concern.
- Safe evidence updates are append-only for this slice: existing declarations,
  ids, result meaning, source references, synthetic disclosures, and deferral
  rationales are preserved. Semantic replacement of an existing declaration is
  out of scope and must be refused until a later feature defines a replacement
  contract.
- Verify and ship readiness are later SDD features that consume evidence facts
  but are not implemented by this feature.
- The existing task and analysis contracts provide the required task ids,
  evidence obligations, source snapshots, and readiness findings needed for
  evidence declaration.
