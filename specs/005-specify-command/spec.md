# Feature Specification: Specify Command

**Feature Branch**: `005-specify-command`

**Created**: 2026-06-19

**Status**: Ready for Planning

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
work-item specification artifact, typed requirement/story contract,
lifecycle state, command report, generated-view currency behavior,
diagnostics, and optional Governance boundary facts)

**Input**: User description: "Next item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd specify`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create A Work Specification (Priority: P1)

As a project maintainer or coding agent, I need to create the specification for
a chartered SDD work item so that user value, scope, non-goals, requirements,
stories, and acceptance criteria become the source of truth for later
clarification, checklist, planning, tasks, evidence, and readiness views.

**Why this priority**: `fsgg-sdd specify` is the next native lifecycle action
after `charter`. Without it, users cannot advance a work item from principles
and boundaries into testable product intent through the native command surface.

**Independent Test**: Can be tested by running the specify command in an
initialized SDD project for one chartered work id and confirming that the
specification artifact, lifecycle state, command report, generated-view state,
diagnostics, and next action are produced without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a chartered work id with no existing
   specification, **When** the user supplies specification intent, **Then** the
   selected work item has a specification artifact containing identity, user
   value, scope, non-goals, stories, requirements, acceptance criteria,
   ambiguity state, and lifecycle notes.
2. **Given** a specification is created successfully, **When** the command
   completes, **Then** the command report names the selected work id, changed
   artifact, parsed specification facts, generated-view state, diagnostics,
   outcome, and `clarify` as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user creates a
   specification, **Then** SDD-only specification authoring still succeeds and
   does not ask Governance to evaluate policy, freshness, routes, profiles,
   gates, or enforcement.

---

### User Story 2 - Preserve And Refine Existing Specification Content (Priority: P1)

As a contributor maintaining an existing work item, I need specification reruns
to preserve authored intent and report unsafe changes before they happen so
that users and agents can refine a spec without losing prior decisions.

**Why this priority**: The specification is an authored source. It is also the
contract that later lifecycle stages depend on, so reruns must be predictable
and non-destructive.

**Independent Test**: Can be tested by running the specify command against a
work item that already has a specification and verifying that safe additions
are reported, unchanged content is preserved, stable ids remain stable, and
unsafe conflicts block writes.

**Acceptance Scenarios**:

1. **Given** an existing specification with user-authored stories,
   requirements, non-goals, and acceptance scenarios, **When** the user reruns
   the specify command with compatible intent, **Then** existing content and
   stable ids are preserved and the report shows any safe additions.
2. **Given** an existing specification whose recorded work identity conflicts
   with the selected work id, **When** the user runs the specify command,
   **Then** the command refuses to write and reports the mismatch as a blocking
   diagnostic.
3. **Given** an existing specification can be safely completed with missing
   standard sections or missing typed identifiers, **When** the user runs the
   specify command, **Then** the command records the safe additions and reports
   exactly which sections or identifiers changed.

---

### User Story 3 - Diagnose Invalid Or Incomplete Specification Requests (Priority: P2)

As a user or agent, I need invalid specify requests to fail with actionable
diagnostics so that I can fix the correct lifecycle artifact before
clarification or planning starts.

**Why this priority**: Specification defects cascade into unclear plans, task
graphs, generated views, and evidence obligations. The specify stage must make
missing or conflicting intent visible early.

**Independent Test**: Can be tested by invoking the specify command outside an
SDD project, before chartering, with malformed work ids, missing intent,
malformed specification data, duplicate ids, and unsafe overwrite situations,
then confirming that no unsafe write occurs and each result contains a stable
diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the specify command, **Then** no work artifact is created and
   the report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed or not chartered, **When** the
   user runs the specify command, **Then** no specification artifact is created
   and the report identifies the missing or invalid prerequisite.
3. **Given** required specification intent is missing, **When** the user runs
   the specify command for a new specification, **Then** the report explains
   which user-value, scope, or requirement input is needed before creation can
   continue.
4. **Given** the specification or generated view is stale, malformed, or
   inconsistent with source data, **When** the command cannot safely refresh
   the lifecycle view, **Then** the report records a stale or blocked
   generated-view diagnostic instead of treating the existing generated file as
   current.

---

### User Story 4 - Keep Specification Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need specify
command outputs to be deterministic and traceable so that humans, agents, and
downstream tooling all read the same lifecycle facts.

**Why this priority**: The specification feeds clarification, checklist,
planning, task generation, evidence, generated views, and optional Governance
contracts. Its report shape must be stable before those stages build on it.

**Independent Test**: Can be tested by running the same specify request against
the same project state multiple times and confirming that machine-readable
reports are stable, plain text summaries contain no extra facts, and optional
Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and specification input, **When** the
   specify command is run repeatedly, **Then** the machine-readable report is
   identical for each run.
2. **Given** a user requests a human-readable summary, **When** the specify
   command completes, **Then** the summary reflects the same changed artifacts,
   parsed facts, diagnostics, generated-view state, and next action as the
   authoritative command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the specify command completes, **Then** the report may
   expose those pointers as compatibility facts but does not interpret or
   enforce Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the charter stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification artifact.
- The user attempts to create a new specification without enough intent to
  name user value, scope, and at least one measurable requirement.
- Existing user-authored stories, requirements, acceptance scenarios, non-goals,
  or ambiguity records would be removed or renumbered by the proposed update.
- Requirement ids, story ids, acceptance-scenario ids, or ambiguity ids are
  duplicated, missing where required, or referenced by generated data that no
  longer matches the authored specification.
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

- **FR-001**: The feature MUST provide `fsgg-sdd specify` as the next native SDD
  lifecycle command after `fsgg-sdd charter`.
- **FR-002**: The specify command MUST require an initialized SDD project, one
  valid selected work id, and a chartered work-item state before it plans any
  specification artifact changes.
- **FR-003**: The specify command MUST create a specification artifact for the
  selected work item when one does not exist and enough user intent is
  provided.
- **FR-004**: A specification artifact MUST capture work identity, user value,
  scope, non-goals, user stories, acceptance scenarios, measurable
  requirements, ambiguity state, public or tool-facing impact, and lifecycle
  notes needed before clarification and planning.
- **FR-005**: The specification contract MUST expose stable identifiers for
  stories, requirements, acceptance scenarios, ambiguity records, and explicit
  reference links so generated models, later lifecycle stages, and diagnostics
  can point to the same facts. Requirement reference links are structured
  associations to `US-###` story ids or `AC-###` acceptance-scenario ids;
  free-form prose references are not authoritative for tools.
- **FR-006**: The specify command MUST preserve existing authored specification
  content and stable identifiers unless it can report a safe, non-destructive
  update.
- **FR-007**: The specify command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate typed identifiers,
  missing required identifiers, and malformed existing specification data
  before any authored artifact is changed.
- **FR-008**: The specify command MUST record the selected work item's
  lifecycle state as specified when specification creation or update succeeds.
- **FR-009**: The specify command MUST identify `clarify` as the next lifecycle
  action after a successful specification result.
- **FR-010**: The specify command MUST identify specification correction as the
  next action when required sections, typed identifiers, or minimum measurable
  requirements are still missing.
- **FR-011**: The specify command MUST record unresolved ambiguity as explicit
  ambiguity records instead of hiding material uncertainty in prose.
- **FR-012**: The specify command MUST report changed artifacts, preserved
  artifacts, refused artifacts, parsed specification facts, generated-view
  state, diagnostics, outcome, and next action in the authoritative command
  report.
- **FR-013**: Machine-readable specify reports MUST be deterministic for
  identical project state and identical specification input.
- **FR-014**: Human-readable specify summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-015**: The specify command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when specification
  sources affect generated lifecycle state.
- **FR-016**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-017**: Specify diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-018**: The specify command MUST work when Governance is not installed or
  configured.
- **FR-019**: The specify command MAY expose optional Governance policy pointers
  as compatibility facts, but MUST NOT evaluate routes, evidence freshness,
  profiles, gates, protected-boundary verdicts, or release policy.
- **FR-020**: The feature MUST NOT introduce `clarify`, `checklist`, `plan`,
  `tasks`, `analyze`, evidence update, verify, ship, release, generated agent
  guidance, or Governance enforcement behavior.
- **FR-021**: The specify command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and
  next action, and does not mutate authored or generated artifacts.

### Key Entities

- **Specified Work Item**: The selected lifecycle unit that has a stable work
  id, chartered prerequisite state, current specification state, and a next
  expected lifecycle action.
- **Specification Artifact**: The authored source that records value, scope,
  non-goals, stories, requirements, acceptance criteria, ambiguity state, and
  lifecycle notes for the selected work item.
- **Specification Intent**: The user-supplied statement of desired outcome,
  problem, or change that seeds or updates the specification artifact.
- **User Story**: A user-centered scenario with priority, rationale,
  independent test guidance, and acceptance scenarios.
- **Requirement**: A measurable capability, constraint, or outcome with a
  stable identifier and explicit structured links to relevant stories or
  acceptance scenarios.
- **Acceptance Scenario**: A testable expected behavior that demonstrates a
  user story or requirement from an initial state through an observable result.
- **Scope Boundary**: An explicit inclusion, exclusion, or ownership line that
  prevents the specification from drifting into unrelated product or Governance
  concerns.
- **Ambiguity Record**: A structured note that captures material uncertainty to
  resolve in the clarification stage or accept as a documented deferral.
- **Specify Command Report**: The authoritative result of a specify command,
  including context, artifact changes, parsed facts, generated-view state,
  diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by the specification source.
- **Specify Diagnostic**: A stable finding that explains invalid project
  context, missing charter prerequisite, malformed work id, incomplete intent,
  unsafe content changes, stale generated views, or optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the specify command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a chartered work item, a user
  can create a new specification and receive the next lifecycle action in one
  command result.
- **SC-002**: 100% of valid specify fixture families (`specify-create`,
  `specify-rerun-preserves-content`, `specify-adds-missing-sections`,
  `specify-preserves-stable-ids`, `deterministic-report`,
  `text-projection`, and no-Governance `governance-boundary`) produce the
  expected specification artifact, selected work-id trace, successful outcome,
  and `clarify` next action.
- **SC-003**: 100% of blocked specify fixture families (`outside-project`,
  `missing-charter`, `missing-intent`, `malformed-work-id`,
  `malformed-specification`, `duplicate-work-id`, `duplicate-spec-id`,
  `specification-identity-mismatch`, `unsafe-overwrite`, and
  `stale-generated-view`) leave authored specification content unchanged and
  include at least one actionable diagnostic.
- **SC-004**: Three repeated specify runs over identical inputs produce
  identical machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite and identifier-renumbering scenarios
  identify the affected artifact or identifier before refusing the change.
- **SC-006**: Maintainers can identify the changed artifact, parsed
  requirements, unresolved ambiguity count, blocking diagnostic,
  generated-view state, and next action from the human-readable summary during
  review, and readiness evidence records that review against the
  text-projection output.
- **SC-007**: Specification creation and update remain usable without
  Governance installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init` and `fsgg-sdd charter` already create the minimum SDD project
  and work-item state used by this feature.
- The specify command operates on one selected work item at a time and writes
  the selected work item's specification artifact under the configured work
  root.
- A successful specify result points to `clarify` as the next lifecycle command,
  but implementing `clarify` is outside this feature.
- Requirements-quality checklist generation and validation belong to the later
  checklist command.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
