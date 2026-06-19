# Feature Specification: Checklist Command

**Feature Branch**: `007-checklist-command`

**Created**: 2026-06-19

**Status**: Ready for Implementation

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
requirements-quality checklist artifact, checklist result traceability,
lifecycle state, command report, generated-view currency behavior,
diagnostics, and optional Governance boundary facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd checklist`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create Requirements-Quality Checklist (Priority: P1)

As a project maintainer or coding agent, I need to create a requirements-quality
checklist for a clarified SDD work item so that the work can be reviewed for
readiness before technical planning starts.

**Why this priority**: `fsgg-sdd checklist` is the next native lifecycle action
after `clarify`. Without it, users can record specification and clarification
facts but cannot turn them into a durable readiness review before plan, tasks,
evidence, and verification depend on those facts.

**Independent Test**: Can be tested by running the checklist command in an
initialized SDD project for one clarified work id and confirming that the
checklist artifact, checklist result facts, lifecycle state, command report,
generated-view state, diagnostics, and next action are produced without
requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a clarified work item with a valid
   specification and clarification decisions, **When** the user runs the
   checklist command, **Then** the selected work item has a checklist artifact
   containing identity, source links, requirements-quality checks, review
   results, blocking findings, and lifecycle notes.
2. **Given** every blocking requirements-quality check passes or has an
   accepted deferral that remains visible, **When** the command completes,
   **Then** the command report names the selected work id, changed artifact,
   parsed checklist facts, generated-view state, diagnostics, outcome, and
   `plan` as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user creates a
   checklist, **Then** SDD-only checklist authoring still succeeds and does not
   ask Governance to evaluate policy, freshness, routes, profiles, gates, or
   enforcement.

---

### User Story 2 - Preserve And Refresh Checklist Review Decisions (Priority: P1)

As a contributor maintaining an existing work item, I need checklist reruns to
preserve prior review decisions while detecting when changed source facts make
those decisions stale, so that later planning work does not rely on outdated
requirements-quality evidence.

**Why this priority**: Checklist results are durable readiness facts. Reruns
must allow safe additions from updated specifications or clarifications without
silently changing or discarding the review state that plans and generated views
may reference.

**Independent Test**: Can be tested by running the checklist command against a
work item with an existing checklist artifact and verifying that compatible new
checks are added, existing decisions remain stable, stale decisions are
reported, and conflicting updates block before writing.

**Acceptance Scenarios**:

1. **Given** an existing checklist artifact with stable check ids and review
   results, **When** the user reruns the checklist command after compatible
   specification or clarification additions, **Then** existing results are
   preserved, new required checks are added, and the report shows the safe
   additions.
2. **Given** a source requirement, acceptance scenario, or clarification
   decision has changed since a checklist result was recorded, **When** the
   user reruns the checklist command, **Then** the affected checklist result is
   reported as stale or needing review instead of being treated as current.
3. **Given** a proposed update would remove, renumber, or semantically change
   an existing checklist result without a safe replacement path, **When** the
   command validates the update, **Then** it refuses to write and reports the
   conflict as a blocking diagnostic.

---

### User Story 3 - Diagnose Checklist Readiness Problems (Priority: P2)

As a user or agent, I need invalid or incomplete checklist requests to fail
with actionable diagnostics so that I can fix the correct lifecycle artifact
before planning starts.

**Why this priority**: Checklist findings determine whether a work item can move
into planning. Missing prerequisites, unresolved ambiguity, untestable
requirements, absent acceptance scenarios, or malformed checklist state must be
visible before planning commits to contracts and tasks.

**Independent Test**: Can be tested by invoking the checklist command outside
an SDD project, before clarification, with unresolved blocking ambiguity,
malformed work ids, malformed checklist data, duplicate check ids, missing
required checklist results, stale generated views, and unsafe overwrite
situations, then confirming that no unsafe write occurs and each result
contains a stable diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the checklist command, **Then** no work artifact is created and
   the report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed or has not reached a valid
   clarified state, **When** the user runs the checklist command, **Then** no
   checklist artifact is created and the report identifies the missing or
   invalid prerequisite.
3. **Given** the specification or clarification facts still contain blocking
   ambiguity, untestable requirements, missing acceptance scenarios, unclear
   scope boundaries, or unresolved dependencies, **When** the checklist command
   evaluates readiness, **Then** the report records checklist findings and
   identifies correction as the next action instead of allowing planning.
4. **Given** the checklist artifact or generated view is stale, malformed, or
   inconsistent with source data, **When** the command cannot safely refresh the
   lifecycle view, **Then** the report records a stale or blocked generated-view
   diagnostic instead of treating the existing generated file as current.

---

### User Story 4 - Keep Checklist Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need
checklist command outputs to be deterministic and traceable so that humans,
agents, and downstream tooling all read the same readiness facts.

**Why this priority**: Checklist results feed plans, tasks, evidence,
generated readiness views, and optional Governance-compatible checks. The
report shape must be stable before those stages build on it.

**Independent Test**: Can be tested by running the same checklist request
against the same project state multiple times and confirming that
machine-readable reports are stable, plain text summaries contain no extra
facts, and optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and checklist input, **When** the
   checklist command is run repeatedly, **Then** the machine-readable report is
   identical for each run.
2. **Given** a user requests a human-readable summary, **When** the checklist
   command completes, **Then** the summary reflects the same changed artifacts,
   checklist results, blocking findings, diagnostics, generated-view state, and
   next action as the authoritative command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the checklist command completes, **Then** the report may
   expose those pointers as compatibility facts but does not interpret or
   enforce Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the clarify stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, or checklist artifact.
- The specification has missing user stories, missing acceptance scenarios,
  untestable requirements, unclear success criteria, unresolved dependencies,
  or scope boundaries that are too broad for planning.
- The clarification artifact records still-open blocking ambiguity or accepted
  deferrals that must remain visible to planning.
- Existing checklist items, results, source links, or stable ids would be
  removed, renumbered, or semantically changed by the proposed update.
- Two checklist items or results use the same stable identifier.
- A source requirement, acceptance scenario, or clarification decision changes
  after a checklist result was recorded.
- Required project settings or artifact layout settings exist but are
  malformed, stale, or point to missing lifecycle roots.
- A generated work model exists but its source digests, schema version, or
  generator identity no longer match the current authored sources.
- The user requests a dry run and expects proposed changes and diagnostics
  without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for lifecycle authoring and compatibility facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd checklist` as the next native
  SDD lifecycle command after `fsgg-sdd clarify`.
- **FR-002**: The checklist command MUST require an initialized SDD project, one
  valid selected work id, a valid specification, and a valid clarified
  work-item state before it plans any checklist artifact changes.
- **FR-003**: The checklist command MUST load and validate the selected work
  item's specification and clarification facts, including work identity,
  lifecycle state, requirement ids, user-story ids, acceptance-scenario ids,
  scope-boundary ids, ambiguity ids, clarification question ids, and
  clarification decision ids.
- **FR-004**: The checklist command MUST create a checklist artifact for the
  selected work item when requirements-quality review is needed and a safe
  checklist artifact does not already exist.
- **FR-005**: A checklist artifact MUST capture work identity, source
  specification relationship, source clarification relationship,
  requirements-quality checklist items, source links, review results,
  accepted deferrals, blocking findings, non-blocking notes, and lifecycle
  notes needed before planning.
- **FR-006**: The checklist contract MUST expose stable identifiers for
  checklist items and checklist results, with structured links to relevant
  requirements, user stories, acceptance scenarios, scope boundaries,
  ambiguity records, clarification questions, or clarification decisions when
  known.
- **FR-007**: The checklist command MUST evaluate requirements quality for
  testability, measurable success criteria, acceptance-scenario coverage,
  edge-case coverage, scope boundaries, dependency assumptions, remaining
  ambiguity, and absence of implementation-planning detail in the user-facing
  specification.
- **FR-008**: The checklist command MUST distinguish passed checks, failed
  blocking checks, accepted deferrals, stale review results, and non-blocking
  advisory notes.
- **FR-009**: The checklist command MUST preserve existing authored checklist
  content, review results, and stable identifiers unless it can report a safe,
  non-destructive update.
- **FR-010**: The checklist command MUST mark review results as stale or needing
  review when the source facts they reference have changed since the result was
  recorded.
- **FR-011**: The checklist command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate checklist ids, unknown
  source references, missing required identifiers, and malformed existing
  checklist data before any authored artifact is changed.
- **FR-012**: The checklist command MUST record the selected work item's
  lifecycle state as checklist-ready when all blocking requirements-quality
  checks pass or have accepted deferrals that remain visible to planning.
- **FR-013**: The checklist command MUST identify `plan` as the next lifecycle
  action after a successful checklist-ready result.
- **FR-014**: The checklist command MUST identify specification,
  clarification, or checklist correction as the next action when blocking
  requirements-quality findings, stale results, unknown references, malformed
  identifiers, or missing review results remain.
- **FR-015**: The checklist command MUST report changed artifacts, preserved
  artifacts, refused artifacts, parsed checklist facts, generated-view state,
  diagnostics, outcome, and next action in the authoritative command report.
- **FR-016**: Machine-readable checklist reports MUST be deterministic for
  identical project state and identical checklist input.
- **FR-017**: Human-readable checklist summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-018**: The checklist command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when checklist sources
  affect generated lifecycle state.
- **FR-019**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-020**: Checklist diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-021**: The checklist command MUST work when Governance is not installed
  or configured.
- **FR-022**: The checklist command MAY expose optional Governance policy
  pointers as compatibility facts, but MUST NOT evaluate routes, evidence
  freshness, profiles, gates, protected-boundary verdicts, or release policy.
- **FR-023**: The feature MUST NOT introduce `plan`, `tasks`, `analyze`,
  evidence update, verify, ship, release, generated agent guidance, or
  Governance enforcement behavior.
- **FR-024**: The checklist command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and
  next action, and does not mutate authored or generated artifacts.

### Key Entities

- **Checklist-Ready Work Item**: The selected lifecycle unit that has a stable
  work id, valid specification facts, clarified prerequisite state, current
  checklist state, and a next expected lifecycle action.
- **Specification Facts**: The existing specification identity, stories,
  requirements, acceptance scenarios, scope boundaries, success criteria, edge
  cases, assumptions, and ambiguity records that provide source material for
  requirements-quality review.
- **Clarification Facts**: The clarification questions, answers, decisions,
  accepted deferrals, and remaining ambiguity state that determine whether the
  work item can be reviewed for planning readiness.
- **Checklist Artifact**: The authored source that records checklist items,
  source links, review results, accepted deferrals, blocking findings,
  non-blocking notes, and lifecycle notes for the selected work item.
- **Checklist Item**: A requirements-quality check with a stable identifier,
  purpose, source links, blocking status, and expected correction guidance.
- **Checklist Result**: The recorded outcome for a checklist item, including
  pass, fail, accepted deferral, stale, or advisory status.
- **Checklist Finding**: A user-correctable readiness issue derived from
  specification, clarification, or checklist facts.
- **Checklist Command Report**: The authoritative result of a checklist command,
  including context, artifact changes, checklist facts, generated-view state,
  diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by checklist sources.
- **Checklist Diagnostic**: A stable finding that explains invalid project
  context, missing prerequisite state, malformed work id, incomplete review,
  unsafe content changes, stale generated views, or optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the checklist command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a clarified work item, a user
  can create a requirements-quality checklist and receive the next lifecycle
  action in one command result.
- **SC-002**: 100% of valid checklist fixture families
  (`checklist-create`, `checklist-rerun-preserves-results`,
  `checklist-adds-missing-items`, `checklist-preserves-stable-ids`,
  `checklist-accepted-deferral`, `checklist-stale-result`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected checklist artifact, selected
  work-id trace, successful outcome, and correct next action.
- **SC-003**: 100% of blocked checklist fixture families (`outside-project`,
  `missing-specification`, `missing-clarification`, `unresolved-ambiguity`,
  `malformed-work-id`, `malformed-checklist`, `duplicate-work-id`,
  `duplicate-checklist-id`, `unknown-source-reference`,
  `checklist-identity-mismatch`, `unsafe-overwrite`, and
  `stale-generated-view`) leave authored checklist content unchanged and
  include at least one actionable diagnostic.
- **SC-004**: Three repeated checklist runs over identical inputs produce
  identical machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite, stale source, and identifier
  renumbering scenarios identify the affected artifact or identifier before
  refusing or marking the result stale.
- **SC-006**: Maintainers can identify the changed artifact, checklist pass
  count, blocking finding count, accepted deferral count, stale result count,
  generated-view state, and next action from the human-readable summary during
  review, and readiness evidence records that review against the
  text-projection output.
- **SC-007**: 100% of failed requirements-quality scenarios create or safely
  update the checklist artifact with failed checklist results and identify
  specification, clarification, or checklist correction as the next action
  instead of allowing planning.
- **SC-008**: Checklist creation and update remain usable without Governance
  installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`, and
  `fsgg-sdd clarify` already create the minimum SDD project and work-item
  state used by this feature.
- The checklist command operates on one selected work item at a time and writes
  the selected work item's checklist artifact under the configured work root.
- The checklist artifact is the durable source for requirements-quality review
  results; the specification remains the authored source for original user
  value, scope, stories, requirements, acceptance scenarios, and ambiguity
  records; clarifications remain the authored source for clarification
  decisions and accepted deferrals.
- A successful checklist-ready result points to `plan` as the next lifecycle
  command, but implementing `plan` is outside this feature.
- Task graph generation, evidence declarations, verification readiness, ship
  readiness, and generated agent guidance belong to later lifecycle features.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
