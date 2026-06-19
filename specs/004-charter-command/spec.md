# Feature Specification: Charter Command

**Feature Branch**: `004-charter-command`

**Created**: 2026-06-19

**Status**: Ready for Implementation

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
work-item charter artifact, lifecycle state, command report, generated-view
currency behavior, diagnostics, and optional Governance boundary facts)

**Input**: User description: "Next item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd charter`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create A Work Charter (Priority: P1)

As a project maintainer or coding agent, I need to create the first charter for
a selected SDD work item so that implementation intent starts from explicit
principles, scope boundaries, and lifecycle policy notes before specification
work begins.

**Why this priority**: `fsgg-sdd charter` is the next native lifecycle action
after project initialization. Without it, users cannot advance from an
initialized SDD skeleton into a governed work item through the native command
surface.

**Independent Test**: Can be tested by running the charter command in an
initialized SDD project for one valid work id and confirming that the charter
artifact, lifecycle stage, command report, and next action are produced without
requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a valid work id with no existing
   charter, **When** the user creates a charter, **Then** the selected work item
   has a charter artifact containing identity, principles, boundaries, policy
   pointers, and lifecycle notes.
2. **Given** a charter is created successfully, **When** the command completes,
   **Then** the command report names the selected work id, changed artifact,
   generated-view state, diagnostics, outcome, and `specify` as the next
   lifecycle action.
3. **Given** optional Governance files are absent, **When** the user creates a
   charter, **Then** SDD-only charter creation still succeeds and does not ask
   Governance to evaluate policy, freshness, routes, profiles, gates, or
   enforcement.

---

### User Story 2 - Re-Run Or Update A Charter Safely (Priority: P1)

As a contributor maintaining an existing work item, I need charter re-runs to
preserve authored content and report any unsafe change before it happens so
that lifecycle commands never erase human decisions.

**Why this priority**: Charters are authored sources. Users and agents must be
able to rerun lifecycle commands without losing prose, policy notes, or
previously recorded boundaries.

**Independent Test**: Can be tested by running the charter command against a
work item that already has a charter and verifying that safe updates are
reported, unchanged content is preserved, and unsafe conflicts block writes.

**Acceptance Scenarios**:

1. **Given** an existing charter with user-authored principles and boundaries,
   **When** the user reruns the charter command without requesting conflicting
   changes, **Then** existing content is preserved and the report shows no
   destructive change.
2. **Given** an existing charter whose recorded work identity conflicts with the
   selected work id, **When** the user runs the charter command, **Then** the
   command refuses to write and reports the mismatch as a blocking diagnostic.
3. **Given** an existing charter can be safely completed with missing standard
   sections, **When** the user runs the charter command, **Then** the command
   records the safe additions and reports exactly which sections changed.

---

### User Story 3 - Diagnose Invalid Charter Requests (Priority: P2)

As a user or agent, I need invalid charter requests to fail with actionable
diagnostics so that I can fix the right lifecycle artifact instead of guessing
why the command did nothing.

**Why this priority**: Native lifecycle commands become workflow contracts.
Clear diagnostics are required before later specification, planning, task, and
analysis commands can rely on charter state.

**Independent Test**: Can be tested by invoking the charter command outside an
SDD project, with malformed work ids, missing project settings, malformed
existing charter data, and unsafe overwrite situations, then confirming that no
write occurs and each result contains a stable diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the charter command, **Then** no work artifact is created and
   the report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed, **When** the user runs the
   charter command, **Then** no work artifact is created and the report explains
   the accepted work-id shape.
3. **Given** required SDD project settings are missing or malformed, **When**
   the user runs the charter command, **Then** the report identifies the
   blocking project artifact and the correction needed before charter authoring
   can continue.
4. **Given** the existing charter or generated view is stale, malformed, or
   inconsistent with source data, **When** the command cannot safely refresh the
   lifecycle view, **Then** the report records a stale or blocked generated-view
   diagnostic instead of treating the existing generated file as current.

---

### User Story 4 - Keep Charter Output Traceable (Priority: P3)

As a CI operator, maintainer, or Governance integrator, I need charter command
outputs to be deterministic and traceable so that humans, agents, and optional
downstream tooling all read the same lifecycle facts.

**Why this priority**: The charter command is the first work-item command. Its
reporting and boundary behavior set the pattern for later lifecycle commands.

**Independent Test**: Can be tested by running the same charter request against
the same project state multiple times and confirming that machine-readable
reports are stable, plain text summaries contain no extra facts, and optional
Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and charter input, **When** the charter
   command is run repeatedly, **Then** the machine-readable report is identical
   for each run.
2. **Given** a user requests a human-readable summary, **When** the charter
   command completes, **Then** the summary reflects the same changed artifacts,
   diagnostics, generated-view state, and next action as the authoritative
   command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the charter command completes, **Then** the report may
   expose those pointers as compatibility facts but does not interpret or
   enforce Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing charter artifact.
- The target charter path exists with user-authored content that does not match
  the command's proposed update.
- Required project settings exist but are malformed, stale, or point to missing
  lifecycle roots.
- The charter contains missing standard sections, conflicting policy notes, or
  references to optional policy files that are absent.
- A generated work model exists but its source digests, schema version, or
  generator identity no longer match the current authored sources.
- The user requests a dry run and expects proposed changes and diagnostics
  without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for lifecycle authoring and compatibility facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd charter` as the next native SDD
  lifecycle command after project initialization.
- **FR-002**: The charter command MUST require an initialized SDD project and one
  valid selected work id before it plans any work-item artifact changes.
- **FR-003**: The charter command MUST create a charter artifact for the selected
  work item when one does not exist.
- **FR-004**: A charter artifact MUST capture project or work-item identity,
  governing principles, lifecycle boundaries, optional policy pointers, and
  notes needed before specification begins.
- **FR-005**: The charter command MUST preserve existing authored charter
  content unless it can report a safe, non-destructive update.
- **FR-006**: The charter command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, and malformed existing charter data
  before any authored artifact is changed.
- **FR-007**: The charter command MUST record the selected work item's lifecycle
  state as chartered when charter creation or update succeeds.
- **FR-008**: The charter command MUST identify `specify` as the next lifecycle
  action after a successful charter result.
- **FR-009**: The charter command MUST report changed artifacts, preserved
  artifacts, refused artifacts, generated-view state, diagnostics, outcome, and
  next action in the authoritative command report.
- **FR-010**: Machine-readable charter reports MUST be deterministic for
  identical project state and identical charter input.
- **FR-011**: Human-readable charter summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-012**: The charter command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when charter sources
  affect generated lifecycle state.
- **FR-013**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-014**: Charter diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-015**: The charter command MUST work when Governance is not installed or
  configured.
- **FR-016**: The charter command MAY expose optional Governance policy pointers
  as compatibility facts, but MUST NOT evaluate routes, evidence freshness,
  profiles, gates, protected-boundary verdicts, or release policy.
- **FR-017**: The feature MUST NOT introduce `specify`, `clarify`, `checklist`,
  `plan`, `tasks`, `analyze`, evidence update, verify, ship, release, generated
  agent guidance, or Governance enforcement behavior.
- **FR-018**: The charter command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and next
  action, and does not mutate authored or generated artifacts.

### Key Entities

- **Chartered Work Item**: The selected lifecycle unit that has a stable work id,
  current charter state, and a next expected lifecycle action.
- **Charter Artifact**: The authored source that records identity, principles,
  boundaries, policy pointers, and lifecycle notes for the selected work item.
- **Charter Principle**: A governing statement that shapes later specification,
  planning, task, and evidence decisions.
- **Scope Boundary**: An explicit inclusion, exclusion, or ownership line that
  prevents the work item from drifting into unrelated product or Governance
  concerns.
- **Policy Pointer**: An optional reference from the charter to SDD-owned policy
  notes or Governance-owned policy files without making SDD responsible for
  Governance enforcement.
- **Charter Command Report**: The authoritative result of a charter command,
  including context, artifact changes, generated-view state, diagnostics,
  outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by the charter source.
- **Charter Diagnostic**: A stable finding that explains invalid project
  context, malformed work id, unsafe content changes, stale generated views, or
  optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the charter command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project, a user can create a new work-item
  charter and receive the next lifecycle action in one command result.
- **SC-002**: 100% of valid charter fixture families (`charter-create`,
  `charter-rerun-preserves-content`, `charter-adds-missing-sections`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected charter artifact, selected
  work-id trace, successful outcome, and `specify` next action.
- **SC-003**: 100% of blocked charter fixture families (`outside-project`,
  `malformed-work-id`, `malformed-artifact`, `duplicate-work-id`,
  `charter-identity-mismatch`, `unsafe-overwrite`, and
  `stale-generated-view`) leave authored charter content unchanged and include
  at least one actionable diagnostic.
- **SC-004**: Three repeated charter runs over identical inputs produce
  identical machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite scenarios identify the affected artifact
  before refusing the change.
- **SC-006**: Maintainers can identify the changed artifact, blocking
  diagnostic, generated-view state, and next action from the human-readable
  summary during review, and readiness evidence records that review against the
  text-projection output.
- **SC-007**: Charter creation and update remain usable without Governance
  installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init` already creates the minimum SDD project skeleton used by this
  feature.
- The charter command operates on one selected work item at a time and writes
  the selected work item's charter artifact under the configured work root.
- A successful charter points to `specify` as the next lifecycle command, but
  implementing `specify` is outside this feature.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD remains
  independently usable without Governance installed.
