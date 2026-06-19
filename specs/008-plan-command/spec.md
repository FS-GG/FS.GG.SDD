# Feature Specification: Plan Command

**Feature Branch**: `008-plan-command`

**Created**: 2026-06-19

**Status**: Ready for Implementation

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
technical plan artifact, plan decision traceability, lifecycle state, command
report, generated-view currency behavior, diagnostics, and optional Governance
boundary facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd plan`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create A Technical Plan (Priority: P1)

As a project maintainer or coding agent, I need to create a technical plan for
a checklist-ready SDD work item so that approved requirements-quality facts are
turned into an explicit delivery plan before tasks are generated.

**Why this priority**: `fsgg-sdd plan` is the next native lifecycle action
after `checklist`. Without it, users can approve requirements quality but
cannot record the artifact contracts, risks, verification obligations,
migration posture, and lifecycle decisions that task generation depends on.

**Independent Test**: Can be tested by running the plan command in an
initialized SDD project for one checklist-ready work id and confirming that the
plan artifact, plan facts, lifecycle state, command report, generated-view
state, diagnostics, and next action are produced without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a checklist-ready work item with a
   valid specification, clarification decisions, and current checklist results,
   **When** the user runs the plan command, **Then** the selected work item has
   a plan artifact containing identity, source links, scope summary, contract
   impact, design decisions, verification obligations, risk notes, migration
   posture, generated-view impact, and lifecycle notes.
2. **Given** all blocking checklist findings are passed or have accepted
   deferrals that remain visible, **When** the command completes, **Then** the
   command report names the selected work id, changed artifact, parsed plan
   facts, generated-view state, diagnostics, outcome, and `tasks` as the next
   lifecycle action.
3. **Given** optional Governance files are absent, **When** the user creates a
   plan, **Then** SDD-only planning still succeeds and does not ask Governance
   to evaluate policy, freshness, routes, profiles, gates, or enforcement.

---

### User Story 2 - Preserve And Refresh Planning Decisions (Priority: P1)

As a contributor maintaining an existing work item, I need plan reruns to
preserve prior planning decisions while detecting when changed source facts make
those decisions stale, so that later task and evidence work does not rely on an
outdated plan.

**Why this priority**: The plan becomes the durable bridge between
requirements-quality review and task generation. Reruns must support safe
additions without silently changing plan decisions, verification obligations,
or contract references that later lifecycle stages may reference.

**Independent Test**: Can be tested by running the plan command against a work
item with an existing plan artifact and verifying that compatible new facts are
added, existing plan decisions remain stable, stale decisions are reported, and
conflicting updates block before writing.

**Acceptance Scenarios**:

1. **Given** an existing plan artifact with stable decision, contract, and
   verification-obligation references, **When** the user reruns the plan
   command after compatible source additions, **Then** existing references are
   preserved, new required planning entries are added, and the report shows the
   safe additions.
2. **Given** a requirement, clarification decision, checklist result, or
   accepted deferral has changed since a plan decision was recorded, **When**
   the user reruns the plan command, **Then** the affected plan decision is
   reported as stale or needing review instead of being treated as current.
3. **Given** a proposed update would remove, renumber, or semantically change
   an existing plan decision, contract reference, or verification obligation
   without a safe replacement path, **When** the command validates the update,
   **Then** it refuses to write and reports the conflict as a blocking
   diagnostic.

---

### User Story 3 - Diagnose Planning Readiness Problems (Priority: P2)

As a user or agent, I need invalid or incomplete plan requests to fail with
actionable diagnostics so that I can fix the correct lifecycle artifact before
tasks are generated.

**Why this priority**: Planning defects cascade into task graphs, evidence
declarations, generated readiness views, and optional Governance-compatible
checks. Missing prerequisites, failed checklist results, stale source facts, or
unsafe plan updates must be visible before task generation starts.

**Independent Test**: Can be tested by invoking the plan command outside an SDD
project, before checklist readiness, with failed checklist results, malformed
work ids, malformed plan data, duplicate plan identifiers, missing contract
impact, stale source snapshots, and unsafe overwrite situations, then
confirming that no unsafe write occurs and each result contains a stable
diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the plan command, **Then** no work artifact is created and the
   report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed or has not reached a valid
   checklist-ready state, **When** the user runs the plan command, **Then** no
   plan artifact is created and the report identifies the missing or invalid
   prerequisite.
3. **Given** checklist results still contain blocking failures, stale review
   results, unknown references, or unresolved deferrals that block planning,
   **When** the plan command evaluates readiness, **Then** the report identifies
   checklist correction as the next action instead of allowing task generation.
4. **Given** the plan artifact or generated view is stale, malformed, or
   inconsistent with source data, **When** the command cannot safely refresh the
   lifecycle view, **Then** the report records a stale or blocked
   generated-view diagnostic instead of treating the existing generated file as
   current.

---

### User Story 4 - Keep Plan Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need plan
command outputs to be deterministic and traceable so that humans, agents, and
downstream tooling all read the same planning facts.

**Why this priority**: Plan facts feed task generation, evidence declarations,
analysis, verification, ship readiness, and optional Governance-compatible
contracts. The report shape must be stable before those stages build on it.

**Independent Test**: Can be tested by running the same plan request against the
same project state multiple times and confirming that machine-readable reports
are stable, plain text summaries contain no extra facts, and optional
Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and plan input, **When** the plan command
   is run repeatedly, **Then** the machine-readable report is identical for
   each run.
2. **Given** a user requests a human-readable summary, **When** the plan
   command completes, **Then** the summary reflects the same changed artifacts,
   plan decisions, verification obligations, diagnostics, generated-view state,
   and next action as the authoritative command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the plan command completes, **Then** the report may expose
   those pointers as compatibility facts but does not interpret or enforce
   Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the checklist
  stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, or plan artifact.
- The specification, clarification, or checklist artifact is missing,
  malformed, or references unknown lifecycle facts.
- The checklist artifact contains failed blocking checks, stale review results,
  missing required results, or accepted deferrals that must remain visible in
  the plan.
- The plan cannot identify contract impact, verification obligations,
  migration posture, generated-view impact, or lifecycle risks required for
  task generation.
- Existing plan decisions, contract references, verification obligations,
  source links, or stable ids would be removed, renumbered, or semantically
  changed by the proposed update.
- Two plan decisions, contract references, or verification obligations use the
  same stable identifier.
- A source requirement, acceptance scenario, clarification decision, checklist
  result, or accepted deferral changes after a plan decision was recorded.
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

- **FR-001**: The feature MUST provide `fsgg-sdd plan` as the next native SDD
  lifecycle command after `fsgg-sdd checklist`.
- **FR-002**: The plan command MUST require an initialized SDD project, one
  valid selected work id, a valid specification, valid clarification decisions,
  and a checklist-ready work-item state before it plans any plan artifact
  changes.
- **FR-003**: The plan command MUST load and validate the selected work item's
  specification, clarification, and checklist facts, including work identity,
  lifecycle state, requirement ids, user-story ids, acceptance-scenario ids,
  clarification decision ids, checklist result ids, source links, accepted
  deferrals, and blocking finding state.
- **FR-004**: The plan command MUST create a plan artifact for the selected
  work item when planning is needed and a safe plan artifact does not already
  exist.
- **FR-005**: A plan artifact MUST capture work identity, source specification
  relationship, source clarification relationship, source checklist
  relationship, plan scope, contract impact, design decisions, verification
  obligations, risk or complexity notes, migration posture, generated-view
  impact, accepted deferrals, and lifecycle notes needed before task
  generation.
- **FR-006**: The plan contract MUST expose stable identifiers for plan
  decisions, contract references, verification obligations, migration notes,
  and generated-view impacts, with structured links to relevant requirements,
  acceptance scenarios, clarification decisions, checklist results, or accepted
  deferrals when known.
- **FR-007**: The plan command MUST map checklist-ready source facts to planning
  entries so that each blocking requirement, accepted deferral, and relevant
  contract impact has a visible planning disposition.
- **FR-008**: The plan command MUST distinguish complete planning decisions,
  incomplete planning decisions, accepted deferrals, stale plan decisions,
  blocking readiness findings, and non-blocking advisory notes.
- **FR-009**: The plan command MUST preserve existing authored plan content,
  plan decisions, verification obligations, contract references, and stable
  identifiers unless it can report a safe, non-destructive update.
- **FR-010**: The plan command MUST mark plan decisions as stale or needing
  review when the source facts they reference have changed since the decision
  was recorded.
- **FR-011**: The plan command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate plan identifiers, unknown
  source references, missing required identifiers, and malformed existing plan
  data before any authored artifact is changed.
- **FR-012**: The plan command MUST record the selected work item's lifecycle
  state as planned when all blocking planning prerequisites and required plan
  entries are complete or have accepted deferrals that remain visible to task
  generation.
- **FR-013**: The plan command MUST identify `tasks` as the next lifecycle
  action after a successful planned result.
- **FR-014**: The plan command MUST identify specification, clarification,
  checklist, or plan correction as the next action when blocking source
  defects, failed checklist results, stale plan decisions, unknown references,
  malformed identifiers, or missing planning entries remain.
- **FR-015**: The plan command MUST report changed artifacts, preserved
  artifacts, refused artifacts, parsed plan facts, generated-view state,
  diagnostics, outcome, and next action in the authoritative command report.
- **FR-016**: Machine-readable plan reports MUST be deterministic for identical
  project state and identical plan input.
- **FR-017**: Human-readable plan summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-018**: The plan command MUST refresh or explicitly diagnose the generated
  work-model view for the selected work item when plan sources affect generated
  lifecycle state.
- **FR-019**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-020**: Plan diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-021**: The plan command MUST work when Governance is not installed or
  configured.
- **FR-022**: The plan command MAY expose optional Governance policy pointers as
  compatibility facts, but MUST NOT evaluate routes, evidence freshness,
  profiles, gates, protected-boundary verdicts, or release policy.
- **FR-023**: The feature MUST NOT introduce `tasks`, `analyze`, evidence
  update, verify, ship, release, generated agent guidance, route selection,
  freshness evaluation, profile adjustment, gate selection, or Governance
  enforcement behavior.
- **FR-024**: The plan command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and
  next action, and does not mutate authored or generated artifacts.

### Key Entities

- **Planned Work Item**: The selected lifecycle unit that has a stable work id,
  valid prerequisite artifacts, current plan state, and a next expected
  lifecycle action.
- **Specification Facts**: The existing specification identity, stories,
  requirements, acceptance scenarios, scope boundaries, success criteria, edge
  cases, assumptions, and ambiguity records that provide source material for
  planning.
- **Clarification Facts**: The clarification questions, answers, decisions,
  accepted deferrals, and remaining ambiguity state that shape planning
  decisions.
- **Checklist Facts**: The checklist items, review results, accepted deferrals,
  blocking findings, stale result state, and source links that determine
  whether planning can proceed.
- **Plan Artifact**: The authored source that records plan scope, source links,
  design decisions, contract references, verification obligations, migration
  posture, generated-view impact, accepted deferrals, risks, and lifecycle
  notes for the selected work item.
- **Plan Decision**: A durable planning choice or accepted deferral with a
  stable identifier and links to the source facts that required the decision.
- **Contract Reference**: A plan entry that identifies a public, tool-facing, or
  generated artifact contract affected by the work item.
- **Verification Obligation**: A plan entry that states what evidence later
  stages must provide before implementation or readiness can be trusted.
- **Plan Finding**: A user-correctable planning issue derived from source
  artifacts, existing plan content, generated-view state, or optional boundary
  facts.
- **Plan Command Report**: The authoritative result of a plan command,
  including context, artifact changes, plan facts, generated-view state,
  diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by plan sources.
- **Plan Diagnostic**: A stable finding that explains invalid project context,
  missing prerequisite state, malformed work id, incomplete planning entries,
  unsafe content changes, stale generated views, or optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the plan command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a checklist-ready work item, a
  user can create a technical plan and receive the next lifecycle action in one
  command result.
- **SC-002**: 100% of valid plan fixture families (`plan-create`,
  `plan-rerun-preserves-decisions`, `plan-adds-missing-entries`,
  `plan-preserves-stable-ids`, `plan-accepted-deferral`,
  `plan-stale-decision`, `dry-run`, `deterministic-report`,
  `text-projection`, and no-Governance `governance-boundary`) produce the
  expected plan artifact or proposed dry-run changes, selected work-id trace,
  successful outcome, and correct next action.
- **SC-003**: 100% of blocked plan fixture families (`outside-project`,
  `missing-specification`, `missing-clarification`, `missing-checklist`,
  `failed-checklist`, `malformed-work-id`, `malformed-plan`,
  `duplicate-work-id`, `duplicate-plan-id`, `unknown-source-reference`,
  `plan-identity-mismatch`, `unsafe-overwrite`, and `stale-generated-view`)
  leave authored plan content unchanged and include at least one actionable
  diagnostic.
- **SC-004**: Three repeated plan runs over identical inputs produce identical
  machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite, stale source, and identifier
  renumbering scenarios identify the affected artifact or identifier before
  refusing or marking the plan decision stale.
- **SC-006**: Maintainers can identify the changed artifact, plan decision
  count, contract reference count, verification obligation count, accepted
  deferral count, stale decision count, generated-view state, and next action
  from the human-readable summary during review, and readiness evidence records
  that review against the text-projection output.
- **SC-007**: 100% of failed planning-readiness scenarios identify
  specification, clarification, checklist, or plan correction as the next action
  instead of allowing task generation.
- **SC-008**: Plan creation and update remain usable without Governance
  installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`,
  `fsgg-sdd clarify`, and `fsgg-sdd checklist` already create the minimum SDD
  project and work-item state used by this feature.
- The plan command operates on one selected work item at a time and writes the
  selected work item's plan artifact under the configured work root.
- The plan artifact is the durable source for planning decisions, contract
  impact, migration posture, and verification obligations; the specification
  remains the authored source for user value and requirements; clarifications
  remain the authored source for decisions and accepted deferrals; the
  checklist remains the authored source for requirements-quality review
  results.
- A successful planned result points to `tasks` as the next lifecycle command,
  but implementing `tasks` is outside this feature.
- Task graph generation, evidence declarations, analysis, verification
  readiness, ship readiness, and generated agent guidance belong to later
  lifecycle features.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
