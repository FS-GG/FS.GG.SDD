# Feature Specification: Normalized Work Model

**Feature Branch**: `002-normalized-work-model`

**Created**: 2026-06-19

**Status**: Ready for Planning

**Change Tier**: Tier 1 (contracted change: normalized work-model contract,
generated readiness output, diagnostics, schema migration behavior, and
optional Governance compatibility facts)

**Input**: User description: "Create the next actionable SDD feature spec from
`docs/initial-implementation-plan.md`. The next SDD-owned roadmap item after
`001-sdd-artifact-model` is Phase 3: Normalized Work Model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Normalize Authored Lifecycle Work (Priority: P1)

As an SDD product maintainer, I need project-level and work-item lifecycle
sources normalized into one work model so that humans, agents, CLI users, CI,
and optional Governance tooling all inspect the same SDD facts.

**Why this priority**: The normalized work model is the machine-readable SDD
contract that later lifecycle commands, readiness views, evidence checks, and
agent guidance consume.

**Independent Test**: Can be tested with a representative valid SDD project and
work item by confirming that the generated work model contains all lifecycle
facts, source traceability, schema versions, generator metadata, and zero
blocking diagnostics.

**Acceptance Scenarios**:

1. **Given** valid project-level lifecycle settings and a work item with
   specification, planning, task, and evidence sources, **When** the work model
   is produced, **Then** it lists the work id, lifecycle stage, requirements,
   decisions, tasks, evidence declarations, artifact references, generated
   views, source paths, source digests, schema versions, generator version, and
   diagnostics in one contract.
2. **Given** a lifecycle command, agent, or CI check needs SDD facts, **When**
   it reads the work model, **Then** it can identify which structured lifecycle
   data is authoritative while still preserving authored Markdown context for
   human review.
3. **Given** optional Governance configuration exists beside SDD artifacts,
   **When** the work model is produced, **Then** SDD-owned lifecycle facts remain
   usable without requiring route selection, profile adjustment, freshness
   evaluation, or protected-boundary enforcement.

---

### User Story 2 - Diagnose Conflicts And Incomplete Typing (Priority: P2)

As a contributor or agent working on an SDD item, I need conflicts and missing
typed lifecycle links reported with stable diagnostics so that I can fix
authored sources without guessing which artifact is wrong.

**Why this priority**: The model must make Markdown and structured artifact
drift visible before later commands or readiness checks rely on the wrong
facts.

**Independent Test**: Can be tested with invalid fixtures that include
untyped Markdown requirements, structured task references to unknown
requirements or decisions, and mismatches between prose and structured
lifecycle data.

**Acceptance Scenarios**:

1. **Given** a Markdown requirement id that is absent from the normalized
   structured requirement set, **When** the work model is produced, **Then** it
   emits `requirementNotTyped` with the affected source and the expected
   correction.
2. **Given** a structured task that references an unknown requirement or
   decision, **When** the work model is produced, **Then** it emits
   `workModelInconsistent` and identifies the source artifact that contains the
   broken reference.
3. **Given** Markdown prose and structured lifecycle data disagree on status,
   dependencies, ownership, or required evidence, **When** the work model is
   produced, **Then** the structured graph remains authoritative for execution,
   the prose remains visible to users, and a consistency diagnostic explains
   the mismatch.

---

### User Story 3 - Detect Stale Or Missing Generated Models (Priority: P3)

As an SDD user or CI operator, I need generated work models to report whether
they match their declared sources so that a file's presence is never mistaken
for readiness.

**Why this priority**: Generated views are outputs. They are useful only when
their recorded source and generator metadata prove that they are current.

**Independent Test**: Can be tested by changing an authored source or generator
version after a work model is generated, and by deleting the generated view,
then confirming that stale or missing generated-model diagnostics are reported.

**Acceptance Scenarios**:

1. **Given** a generated work model whose recorded source digest no longer
   matches an authored source, **When** currency is checked, **Then** the result
   reports a stale generated-model diagnostic that names the changed source.
2. **Given** a generated work model created by an older generator version,
   **When** currency is checked, **Then** the result reports a generator
   mismatch diagnostic with the expected current generator version.
3. **Given** no generated work model exists for a known work item, **When**
   readiness is checked, **Then** the result reports the missing generated model
   and the output path expected by the lifecycle contract.

---

### User Story 4 - Explain Schema Migration Posture (Priority: P3)

As a maintainer evolving SDD artifacts, I need the work model to state how
current, deprecated, unsupported, and future schema versions are handled so that
schema changes can be planned without silent compatibility breaks.

**Why this priority**: The model becomes a public contract. Schema-version
behavior must be explicit before commands and generated views depend on it.

**Independent Test**: Can be tested with fixtures for current, deprecated,
unsupported, malformed, and future schema versions and by reviewing the
reported compatibility diagnostics.

**Acceptance Scenarios**:

1. **Given** a supported schema version, **When** the source is normalized,
   **Then** the work model records the version and continues without a schema
   compatibility blocker.
2. **Given** a deprecated schema version, **When** the source is normalized,
   **Then** the work model records a migration warning and preserves enough
   information for the user to update the source.
3. **Given** an unsupported, malformed, or future schema version, **When** the
   source is normalized, **Then** the work model reports a blocking diagnostic
   that identifies the artifact, version value, and supported range.

### Edge Cases

- Project-level lifecycle sources exist, but a selected work item is missing or
  has an id that does not match the requested work id.
- A work item contains Markdown requirements or acceptance criteria with ids
  that are missing from the normalized structured model.
- Structured tasks or evidence declarations reference unknown requirements,
  decisions, tasks, or artifacts.
- Markdown prose contradicts structured data for lifecycle stage, task status,
  dependencies, ownership, or required evidence.
- Generated work-model output exists but records stale source digests,
  incompatible schema versions, or an older generator version.
- Generated work-model output is missing even though the work item has
  sufficient authored sources to produce one.
- Optional Governance artifacts are present, absent, incomplete, or invalid;
  SDD still reports only SDD-owned lifecycle diagnostics and optional
  compatibility facts.
- Multiple sources contribute the same logical id or artifact reference, and
  the user needs one deterministic diagnostic order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST normalize SDD-owned project-level sources and one
  selected work item into a `WorkModel` that covers artifact references,
  lifecycle stage, requirements, decisions, tasks, evidence declarations,
  generated views, and diagnostics.
- **FR-002**: The work model MUST record model version, source paths, source
  digests, schema versions, generator version, and source-to-output
  traceability for every included lifecycle source.
- **FR-003**: The work model MUST define `readiness/<id>/work-model.json` as
  the generated readiness view for normalized lifecycle facts.
- **FR-004**: The work model MUST produce byte-stable content and diagnostic
  ordering for identical source trees across repeated runs.
- **FR-005**: The work model MUST exclude implicit clock, terminal, and
  environment details from deterministic content, or label them as
  non-authoritative metadata if they are included for human context.
- **FR-006**: Structured graph data MUST be the authoritative source for
  lifecycle execution when it disagrees with Markdown prose; the work model MUST
  preserve the prose context and emit a consistency diagnostic.
- **FR-007**: The work model MUST emit `requirementNotTyped` when a Markdown
  requirement id or acceptance criterion id is missing from the normalized
  structured requirement set.
- **FR-008**: The work model MUST emit `workModelInconsistent` when structured
  tasks, evidence declarations, or generated-view references point to unknown
  requirements, decisions, tasks, artifacts, source digests, or generator
  versions.
- **FR-009**: The feature MUST report stale or missing generated work models by
  comparing declared source digests, schema versions, and generator metadata
  rather than trusting that a generated file exists.
- **FR-010**: Every work-model diagnostic MUST include a stable diagnostic id,
  severity, affected artifact, source location when available, explanation, and
  expected correction.
- **FR-011**: The feature MUST document schema migration behavior and
  compatibility rules for current, deprecated, malformed, unsupported, and
  future schema versions.
- **FR-012**: The work model MUST preserve enough traceability for a user to
  explain why each requirement, decision, task, evidence declaration,
  generated view, and diagnostic appears in the model.
- **FR-013**: The work model MAY expose optional Governance compatibility facts,
  but MUST NOT perform route selection, profile adjustment, evidence freshness
  evaluation, protected-boundary gate enforcement, or Governance policy
  interpretation.
- **FR-014**: This feature MUST NOT introduce lifecycle authoring commands,
  agent guidance generation, project templates, rendering templates, or
  generated-product runtime behavior.

### Key Entities

- **Work Model**: The normalized lifecycle contract for one work item,
  containing project context, work-item facts, source traceability, generated
  views, diagnostics, and optional compatibility facts.
- **Work Item**: The selected lifecycle unit being normalized, identified by a
  stable work id and current lifecycle stage.
- **Lifecycle Source**: An authored project-level or work-item artifact that
  contributes facts to the work model.
- **Source Digest**: A content identity recorded for each lifecycle source so
  generated views can prove currency or report staleness.
- **Model Version**: The version marker for the normalized work-model contract.
- **Schema Version**: The version marker for each structured lifecycle source
  included in the work model.
- **Generator Version**: The version marker for the producer of the generated
  work-model view.
- **Requirement**: A stable, testable user or lifecycle need linked to
  acceptance criteria, decisions, tasks, and evidence.
- **Decision**: A durable clarification or planning choice referenced by later
  lifecycle work.
- **Task**: A typed implementation unit with status, owner, dependencies,
  required skills, required evidence, and references to requirements or
  decisions.
- **Evidence Declaration**: A structured record of implementation,
  verification, synthetic evidence disclosure, accepted deferral, missing
  evidence, or related support for lifecycle readiness.
- **Generated Work Model**: The generated readiness view at
  `readiness/<id>/work-model.json`.
- **Diagnostic**: A stable finding that identifies malformed, missing, stale,
  conflicting, or inconsistent lifecycle facts and gives the user an expected
  correction.
- **Migration Rule**: A compatibility statement for how a schema version is
  accepted, warned, blocked, or migrated.
- **Governance Compatibility Fact**: Optional SDD-produced data that Governance
  may inspect later without making SDD responsible for Governance semantics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A representative valid SDD work item produces a work model that
  includes 100% of required project-level lifecycle facts, work-item facts,
  source traceability fields, generated-view metadata, and zero blocking
  diagnostics.
- **SC-002**: For identical source inputs, generated work-model content is
  byte-identical across three consecutive runs.
- **SC-003**: 100% of stale-source, stale-generator, incompatible-schema, and
  missing-generated-model fixtures produce the expected diagnostic id and name
  the affected artifact.
- **SC-004**: 100% of fixtures for `requirementNotTyped`,
  `workModelInconsistent`, prose/structured mismatch, duplicate logical ids,
  unknown references, and malformed schema versions produce actionable
  diagnostics with an expected correction.
- **SC-005**: A contributor can trace any normalized requirement to its source,
  related tasks, related evidence declarations, and generated-view metadata in
  under 10 minutes using only the work-model contract and fixture walkthrough.
- **SC-006**: A compatibility review finds zero requirements that assign route
  selection, profile adjustment, freshness evaluation, or protected-boundary
  enforcement behavior to SDD.
- **SC-007**: Schema migration documentation covers current, deprecated,
  malformed, unsupported, and future schema versions, with at least one
  representative fixture or acceptance scenario for each category.

## Assumptions

- Phase 2 in `docs/initial-implementation-plan.md` is owned by
  FS.GG.Governance, so this specification covers the next SDD-owned roadmap
  item: Phase 3, Normalized Work Model.
- `001-sdd-artifact-model` has established the initial lifecycle artifact
  contracts, identifier types, diagnostics, generated-view metadata, and
  optional Governance boundaries that this feature builds on.
- Standard Spec Kit remains the development workflow for this repository while
  the product contract defines future native SDD work-item artifacts under
  `work/<id>`.
- Markdown remains an authoring surface, and schema-versioned structured data is
  the machine contract for executable lifecycle decisions.
- `readiness/<id>/work-model.json` is a generated view, not an authored source
  of truth.
- Governance integration remains optional; SDD must remain useful without the
  Governance gate runtime installed.
