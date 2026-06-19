# Feature Specification: SDD Artifact Model

**Feature Branch**: `001-sdd-artifact-model`

**Created**: 2026-06-19

**Status**: Ready for Implementation

**Change Tier**: Tier 1 (contracted change: lifecycle artifact layout,
schema contracts, generated-view contracts, diagnostics, and agent guidance
contracts)

**Input**: User description: "Create the first feature spec for
`001-sdd-artifact-model` from the next unchecked item in
`docs/initial-implementation-plan.md`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define The Lifecycle Contract (Priority: P1)

As an SDD product maintainer, I need one explicit contract for project-level
and work-item lifecycle artifacts so that future commands, agents, and CI all
operate on the same source of truth.

**Why this priority**: The artifact model is the foundation for every later SDD
command, generated view, task model, evidence declaration, and optional
Governance integration.

**Independent Test**: Can be tested by reviewing the contract for all
SDD-owned lifecycle artifacts named in the roadmap and confirming that each has
an owner, purpose, structured shape, schema-versioning rule, generated-view
relationship, stale behavior, and diagnostics.

**Acceptance Scenarios**:

1. **Given** a new SDD-governed project, **When** a maintainer reviews the
   artifact model, **Then** the required project-level lifecycle files,
   work-item files, generated readiness views, and agent guidance targets are
   all named with their purpose and ownership.
2. **Given** a lifecycle artifact in the model, **When** a maintainer traces its
   contract, **Then** the authored source, structured machine contract,
   generated view, stale-view behavior, and relevant diagnostics are all clear.
3. **Given** a future command or agent workflow, **When** it needs to decide
   which lifecycle data is authoritative, **Then** the artifact model states
   the source of truth and the diagnostic required for conflicts.

---

### User Story 2 - Diagnose Invalid Or Conflicting Work (Priority: P2)

As a contributor or agent working on an SDD item, I need stable identifiers and
diagnostics for malformed, missing, duplicate, stale, or conflicting artifacts
so that I can correct lifecycle work without guessing what failed.

**Why this priority**: The model is only useful if invalid lifecycle artifacts
produce actionable findings before source code, generated views, or merge
readiness depend on them.

**Independent Test**: Can be tested with representative valid and invalid
work-item fixtures that exercise identifier stability, missing artifacts,
schema-version failures, unknown references, duplicate identifiers,
prose/structured mismatches, and stale generated views.

**Acceptance Scenarios**:

1. **Given** a work item with duplicate requirement identifiers, **When** the
   artifact model is validated, **Then** the result identifies the duplicate
   identifiers, their source locations, and the correction expected.
2. **Given** a task or evidence declaration that references an unknown
   requirement or decision, **When** the artifact model is validated, **Then**
   the result identifies the unknown reference and the artifact that contains
   it.
3. **Given** a generated readiness view whose recorded source digest no longer
   matches the authored source, **When** generated-view currency is checked,
   **Then** the result reports a stale-view diagnostic instead of treating the
   file's presence as proof of currency.
4. **Given** Markdown prose and structured lifecycle data that disagree, **When**
   the work item is normalized, **Then** the structured graph is used for
   executable lifecycle decisions, the prose remains visible to users, and a
   consistency diagnostic records the mismatch.

---

### User Story 3 - Expose Optional Governance Boundaries (Priority: P3)

As a Governance integrator, I need SDD lifecycle rule contracts and readiness
facts that can be inspected without making SDD responsible for rule routing,
freshness policy, profiles, or protected-boundary enforcement.

**Why this priority**: SDD must remain independently useful while still
providing versioned contracts that Governance can consume later.

**Independent Test**: Can be tested by reviewing the SDD-owned rule contracts
and compatibility boundaries and confirming that Governance-owned policy,
capability, tooling, route, profile, freshness, and enforcement semantics are
not implemented or made mandatory by this feature.

**Acceptance Scenarios**:

1. **Given** an SDD lifecycle rule for required specification sections, **When**
   Governance compatibility is reviewed, **Then** the rule's inputs, finding
   shape, and expected evidence are described without route, profile, or gate
   enforcement behavior.
2. **Given** a project that also has Governance configuration, **When** the SDD
   artifact model is reviewed, **Then** Governance-owned files are recognized as
   compatibility boundaries and are not treated as SDD-owned lifecycle sources.
3. **Given** a project without Governance installed, **When** SDD lifecycle
   artifacts are authored and validated, **Then** the artifact model remains
   usable and complete for SDD readiness.

### Edge Cases

- A required lifecycle artifact is missing from a project or work item.
- A structured artifact has no schema version, an unsupported schema version,
  or a malformed schema version.
- Two requirements, tasks, decisions, or evidence declarations use the same
  identifier where uniqueness is required.
- A task, evidence declaration, or generated view references a requirement,
  decision, artifact, source digest, or generator version that does not exist.
- Markdown prose contradicts structured lifecycle data for status,
  dependencies, ownership, required evidence, or requirement linkage.
- Generated views exist but are stale because the authored source, schema
  version, generator version, or output digest changed.
- Governance-owned configuration files exist in the same project but SDD must
  not evaluate route, profile, freshness, or protected-boundary gate behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The artifact model MUST define stable lifecycle identifiers for
  work items, stages, requirements, decisions, tasks, evidence declarations,
  artifact references, schema versions, source digests, and generator versions.
- **FR-002**: The artifact model MUST define SDD-owned project-level contracts
  for `.fsgg/project.yml` lifecycle fields, `.fsgg/sdd.yml`, and
  `.fsgg/agents.yml`.
- **FR-003**: The artifact model MUST define SDD-owned work-item contracts for
  `work/<id>` metadata, structured front matter where used, `tasks.yml`, and
  `evidence.yml`.
- **FR-004**: The artifact model MUST identify the authored source, structured
  machine contract, generated views, stale-view behavior, and diagnostics for
  every SDD-owned lifecycle artifact.
- **FR-005**: The artifact model MUST define schema-version expectations for
  every structured artifact, including how missing, malformed, unsupported, and
  future schema versions are reported.
- **FR-006**: The artifact model MUST define diagnostic identifiers for missing
  artifacts, malformed schema versions, duplicate identifiers, unknown
  references, stale generated views, and prose/structured mismatches.
- **FR-007**: The artifact model MUST define reference rules for requirements,
  decisions, tasks, evidence declarations, artifact references, source digests,
  and generator versions.
- **FR-008**: The artifact model MUST define conflict behavior for requirement
  identifiers, task references, decision references, status, dependency,
  ownership, and required-evidence disagreement.
- **FR-009**: The artifact model MUST define SDD lifecycle rule contracts for
  required specification sections, plan obligations, task graph shape, evidence
  declarations, loaded agent skills, and test obligations.
- **FR-010**: The artifact model MUST express lifecycle rule contracts in a
  Governance-compatible shape while excluding route selection, profile
  adjustment, evidence freshness, and gate enforcement semantics from SDD.
- **FR-011**: The artifact model MUST define deterministic ordering and digest
  requirements for machine-readable lifecycle outputs and diagnostics.
- **FR-012**: The artifact model MUST include acceptance fixtures for a valid
  work item and for missing artifacts, malformed schema versions, duplicate
  identifiers, unknown references, prose/structured mismatch, stale generated
  views, and deterministic ordering.
- **FR-013**: The artifact model MUST record compatibility boundaries for
  Governance-owned `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and
  `.fsgg/tooling.yml` without making those files required for SDD-only use.
- **FR-014**: The artifact model MUST define agent guidance targets for Claude
  and Codex as lifecycle contract consumers without making generated agent
  prompts a second source of truth.
- **FR-015**: This feature MUST NOT introduce lifecycle authoring commands,
  route commands, freshness evaluation, protected-boundary enforcement,
  rendering templates, or generated-product runtime behavior.

### Key Entities

- **Work Item**: A lifecycle unit identified by a stable work id, current stage,
  authored sources, structured artifacts, generated views, diagnostics, tasks,
  and evidence declarations.
- **Lifecycle Stage**: A named step in the SDD workflow such as charter,
  specify, clarify, checklist, plan, tasks, analyze, implement, evidence,
  verify, or ship.
- **Lifecycle Artifact**: An authored source, structured model, or generated view
  that participates in SDD readiness.
- **Requirement**: A stable, testable need captured from specification sources
  and linked to tasks, decisions, evidence, and acceptance criteria.
- **Decision**: A durable planning choice or clarification answer that later
  tasks or evidence may reference.
- **Task**: A typed implementation work item with status, owner, dependencies,
  required skills, required evidence, and references to requirements or
  decisions.
- **Evidence Declaration**: A structured record describing implementation,
  verification, synthetic evidence disclosure, accepted deferral, or missing
  evidence for a task or requirement.
- **Artifact Reference**: A stable pointer to an authored source, structured
  artifact, generated view, or external compatibility boundary.
- **Schema Version**: A version marker that identifies the contract expected for
  a structured artifact.
- **Source Digest**: A content identity for an authored source that generated
  views use to report currency or staleness.
- **Generator Version**: A version marker for the producer of a generated view.
- **Diagnostic**: A stable finding with an identifier, severity, affected
  artifact, source location when available, and actionable correction.
- **Lifecycle Rule Contract**: A versioned description of an SDD rule's inputs,
  expected outputs, evidence needs, and diagnostic behavior.
- **Agent Guidance Target**: A Claude, Codex, or future-agent instruction target
  generated from lifecycle data and treated as a projection, not authority.
- **Governance Compatibility Boundary**: A file, rule, or readiness fact that
  SDD exposes for optional Governance consumption while leaving route,
  freshness, profile, and gate semantics to Governance.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of SDD-owned lifecycle artifacts named in the initial
  implementation plan have a documented owner, purpose, schema-version posture,
  generated-view relationship, stale behavior, and diagnostic coverage.
- **SC-002**: The model defines stable diagnostic identifiers for at least the
  six required invalid states: missing artifacts, malformed schema versions,
  duplicate identifiers, unknown references, stale generated views, and
  prose/structured mismatches.
- **SC-003**: A representative valid work item can be traced from project-level
  lifecycle configuration through requirements, decisions, tasks, evidence, and
  generated readiness views with zero blocking diagnostics.
- **SC-004**: Each required invalid fixture, including the missing-artifact
  fixture, produces an actionable diagnostic that names the affected artifact,
  explains the issue, and states the expected correction.
- **SC-005**: For identical source inputs, normalized lifecycle output and
  diagnostic ordering are byte-identical across three consecutive validation
  runs.
- **SC-006**: A Governance boundary review finds zero SDD requirements that
  implement route selection, profile adjustment, freshness evaluation, or
  protected-boundary enforcement.
- **SC-007**: A contributor can identify the source of truth, generated view,
  stale-view behavior, and diagnostic family for any modeled lifecycle artifact
  in under 10 minutes by following the recorded artifact traceability
  walkthrough produced from the contract and fixtures.

## Assumptions

- The first feature directory for this repository is
  `specs/001-sdd-artifact-model`.
- This specification describes the Phase 1 artifact model slice from
  `docs/initial-implementation-plan.md`.
- Standard Spec Kit remains the development workflow for this repository while
  the product contract defines future native SDD work-item artifacts under
  `work/<id>`.
- Markdown remains an authoring surface, and schema-versioned structured data is
  the machine contract for executable lifecycle decisions.
- SDD readiness must remain usable without Governance installed.
- Governance-owned policy, capability, tooling, route, profile, freshness, and
  protected-boundary enforcement behavior remains outside this feature.
- Lifecycle commands, generators, package projects, and tests are introduced
  only by later planning and implementation tasks for this feature.
