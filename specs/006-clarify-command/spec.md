# Feature Specification: Clarify Command

**Feature Branch**: `006-clarify-command`

**Created**: 2026-06-19

**Status**: Ready for Planning

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
clarification artifact, decision traceability, lifecycle state, command report,
generated-view currency behavior, diagnostics, and optional Governance boundary
facts)

**Input**: User description: "Next item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd clarify`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resolve Specification Ambiguity (Priority: P1)

As a project maintainer or coding agent, I need to resolve material ambiguity in
a specified SDD work item so that the work item can move from specification
intent into requirements-quality checking without losing the original authored
specification context.

**Why this priority**: `fsgg-sdd clarify` is the next native lifecycle action
after `specify`. Without it, users cannot turn open ambiguity records into
durable clarification decisions before checklist, planning, tasks, evidence,
and readiness views depend on those facts.

**Independent Test**: Can be tested by running the clarify command in an
initialized SDD project for one specified work id with open ambiguity records
and confirming that the clarification artifact, decision facts, lifecycle
state, command report, generated-view state, diagnostics, and next action are
produced without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a specified work item with open
   ambiguity records, **When** the user supplies clarification answers, **Then**
   the selected work item has a clarification artifact containing identity,
   source ambiguity links, questions, answers, durable decisions, remaining
   ambiguity state, and lifecycle notes.
2. **Given** all blocking ambiguity for a specified work item has been answered,
   **When** the command completes, **Then** the command report names the
   selected work id, changed artifact, parsed clarification facts,
   generated-view state, diagnostics, outcome, and `checklist` as the next
   lifecycle action.
3. **Given** optional Governance files are absent, **When** the user records
   clarification answers, **Then** SDD-only clarification authoring still
   succeeds and does not ask Governance to evaluate policy, freshness, routes,
   profiles, gates, or enforcement.

---

### User Story 2 - Preserve And Extend Clarification Decisions (Priority: P1)

As a contributor maintaining an existing work item, I need clarification reruns
to preserve previous answers and decision identifiers so that later lifecycle
artifacts keep stable references to the choices that shaped the work.

**Why this priority**: Clarification answers become durable decisions. Reruns
must allow safe additions without silently changing the meaning of decisions
that plans, tasks, diagnostics, and generated views may already reference.

**Independent Test**: Can be tested by running the clarify command against a
work item with an existing clarification artifact and verifying that compatible
new answers are added, existing decisions remain stable, and conflicting
updates block before writing.

**Acceptance Scenarios**:

1. **Given** an existing clarification artifact with answered questions and
   decision ids, **When** the user reruns the clarify command with compatible
   additional answers, **Then** existing answers and decision ids are preserved
   and the report shows the safe additions.
2. **Given** a proposed answer changes an existing clarification decision
   without an explicit safe replacement path, **When** the user runs the clarify
   command, **Then** the command refuses to write and reports the conflict as a
   blocking diagnostic.
3. **Given** an existing clarification artifact can be safely completed with
   missing standard sections or missing stable identifiers, **When** the user
   runs the clarify command, **Then** the command records the safe additions and
   reports exactly which sections or identifiers changed.

---

### User Story 3 - Diagnose Missing Or Invalid Clarification Context (Priority: P2)

As a user or agent, I need invalid clarify requests to fail with actionable
diagnostics so that I can fix the correct lifecycle artifact before checklist or
planning starts.

**Why this priority**: Clarification decisions are consumed by later lifecycle
stages. Missing specification prerequisites, malformed identifiers, or
unresolved blocking ambiguity must be visible before implementation work is
planned.

**Independent Test**: Can be tested by invoking the clarify command outside an
SDD project, before specification, with malformed work ids, answers for unknown
ambiguity ids, duplicate decision ids, missing answers, malformed
clarification data, and stale generated views, then confirming that no unsafe
write occurs and each result contains a stable diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the clarify command, **Then** no work artifact is created and
   the report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed or has no valid specification,
   **When** the user runs the clarify command, **Then** no clarification
   artifact is created and the report identifies the missing or invalid
   prerequisite.
3. **Given** the user supplies answers that do not reference known open
   ambiguities or clarification questions, **When** the command validates the
   request, **Then** it refuses the update and reports the unknown reference
   before any authored artifact changes.
4. **Given** the clarification artifact or generated view is stale, malformed,
   or inconsistent with source data, **When** the command cannot safely refresh
   the lifecycle view, **Then** the report records a stale or blocked
   generated-view diagnostic instead of treating the existing generated file as
   current.

---

### User Story 4 - Keep Clarification Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need clarify
command outputs to be deterministic and traceable so that humans, agents, and
downstream tooling all read the same lifecycle facts.

**Why this priority**: Clarification decisions affect checklist, plans, tasks,
evidence, generated work models, and optional Governance-compatible checks. The
report shape must be stable before those stages build on it.

**Independent Test**: Can be tested by running the same clarify request against
the same project state multiple times and confirming that machine-readable
reports are stable, plain text summaries contain no extra facts, and optional
Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and clarification input, **When** the
   clarify command is run repeatedly, **Then** the machine-readable report is
   identical for each run.
2. **Given** a user requests a human-readable summary, **When** the clarify
   command completes, **Then** the summary reflects the same changed artifacts,
   clarification decisions, remaining ambiguity, diagnostics, generated-view
   state, and next action as the authoritative command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the clarify command completes, **Then** the report may
   expose those pointers as compatibility facts but does not interpret or
   enforce Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the specification
  stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification or clarification artifact.
- The specification has no open ambiguity records; the command must report
  whether the work item is already ready for checklist or still missing
  required specification facts.
- The user supplies no answers when unresolved blocking ambiguity remains.
- The user supplies an answer for an unknown ambiguity id, requirement id, user
  story id, acceptance-scenario id, or clarification question id.
- Existing clarification answers, decisions, accepted deferrals, or stable ids
  would be removed, renumbered, or semantically changed by the proposed update.
- Two clarification questions or decisions use the same stable identifier.
- A clarification answer resolves an ambiguity as an accepted deferral rather
  than a concrete decision, and later stages need that deferral to remain
  visible.
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

- **FR-001**: The feature MUST provide `fsgg-sdd clarify` as the next native
  SDD lifecycle command after `fsgg-sdd specify`.
- **FR-002**: The clarify command MUST require an initialized SDD project, one
  valid selected work id, and a valid specified work-item state before it plans
  any clarification artifact changes.
- **FR-003**: The clarify command MUST load and validate the selected work
  item's specification facts, including work identity, lifecycle state,
  requirement ids, user-story ids, acceptance-scenario ids, scope-boundary ids,
  and ambiguity ids.
- **FR-004**: The clarify command MUST create a clarification artifact for the
  selected work item when clarification answers, open ambiguity records, or
  missing clarification state require one.
- **FR-005**: A clarification artifact MUST capture work identity, source
  specification relationship, clarification questions, answers, durable
  decisions, accepted deferrals, remaining ambiguity state, and lifecycle notes
  needed before checklist and planning.
- **FR-006**: The clarification contract MUST expose stable identifiers for
  clarification questions and clarification decisions, with structured links to
  source ambiguity records, requirements, user stories, or acceptance scenarios
  when known.
- **FR-007**: The clarify command MUST turn unresolved material ambiguity from
  the specification into user-correctable clarification questions instead of
  hiding uncertainty in prose.
- **FR-008**: The clarify command MUST distinguish concrete decisions, accepted
  deferrals, still-open questions, and non-blocking notes.
- **FR-009**: The clarify command MUST preserve existing authored
  clarification content and stable identifiers unless it can report a safe,
  non-destructive update.
- **FR-010**: The clarify command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate clarification ids, unknown
  source references, missing required identifiers, and malformed existing
  clarification data before any authored artifact is changed.
- **FR-011**: The clarify command MUST record the selected work item's
  lifecycle state as clarified when all blocking ambiguity has a concrete
  decision or accepted deferral.
- **FR-012**: The clarify command MUST identify `checklist` as the next
  lifecycle action after a successful clarified result.
- **FR-013**: The clarify command MUST identify clarification correction or
  additional answers as the next action when blocking ambiguity, unknown
  references, malformed identifiers, or missing answers remain.
- **FR-014**: The clarify command MUST report changed artifacts, preserved
  artifacts, refused artifacts, parsed clarification facts, generated-view
  state, diagnostics, outcome, and next action in the authoritative command
  report.
- **FR-015**: Machine-readable clarify reports MUST be deterministic for
  identical project state and identical clarification input.
- **FR-016**: Human-readable clarify summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-017**: The clarify command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when clarification
  sources affect generated lifecycle state.
- **FR-018**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-019**: Clarify diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-020**: The clarify command MUST work when Governance is not installed or
  configured.
- **FR-021**: The clarify command MAY expose optional Governance policy pointers
  as compatibility facts, but MUST NOT evaluate routes, evidence freshness,
  profiles, gates, protected-boundary verdicts, or release policy.
- **FR-022**: The feature MUST NOT introduce `checklist`, `plan`, `tasks`,
  `analyze`, evidence update, verify, ship, release, generated agent guidance,
  or Governance enforcement behavior.
- **FR-023**: The clarify command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and
  next action, and does not mutate authored or generated artifacts.

### Key Entities

- **Clarified Work Item**: The selected lifecycle unit that has a stable work
  id, specified prerequisite state, current clarification state, and a next
  expected lifecycle action.
- **Specification Facts**: The existing specification identity, stories,
  requirements, acceptance scenarios, scope boundaries, and ambiguity records
  that provide the context for clarification.
- **Clarification Artifact**: The authored source that records clarification
  questions, answers, decisions, accepted deferrals, remaining ambiguity, and
  lifecycle notes for the selected work item.
- **Clarification Question**: A user-correctable question derived from a
  material ambiguity or missing lifecycle decision, with a stable identifier and
  links to the relevant specification facts when known.
- **Clarification Answer**: The user-provided response to a clarification
  question, including enough context to decide whether the answer resolves,
  defers, or leaves ambiguity open.
- **Clarification Decision**: A durable choice or accepted deferral derived from
  a clarification answer and referenced by later lifecycle stages.
- **Remaining Ambiguity**: Any blocking or non-blocking uncertainty that remains
  after clarification answers are applied.
- **Clarify Command Report**: The authoritative result of a clarify command,
  including context, artifact changes, clarification facts, generated-view
  state, diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by clarification sources.
- **Clarify Diagnostic**: A stable finding that explains invalid project
  context, missing specification prerequisite, malformed work id, incomplete
  answers, unknown references, unsafe content changes, stale generated views,
  or optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the clarify command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a specified work item, a user
  can record clarification answers and receive the next lifecycle action in one
  command result.
- **SC-002**: 100% of valid clarify fixture families (`clarify-create`,
  `clarify-rerun-preserves-decisions`, `clarify-adds-missing-sections`,
  `clarify-preserves-stable-ids`, `clarify-accepted-deferral`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected clarification artifact, selected
  work-id trace, successful outcome, and correct next action.
- **SC-003**: 100% of blocked clarify fixture families (`outside-project`,
  `missing-specification`, `missing-answer`, `malformed-work-id`,
  `malformed-clarification`, `duplicate-work-id`, `duplicate-clarification-id`,
  `unknown-ambiguity-reference`, `clarification-identity-mismatch`,
  `unsafe-overwrite`, and `stale-generated-view`) leave authored
  clarification content unchanged and include at least one actionable
  diagnostic.
- **SC-004**: Three repeated clarify runs over identical inputs produce
  identical machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite, decision-change, and identifier
  renumbering scenarios identify the affected artifact or identifier before
  refusing the change.
- **SC-006**: Maintainers can identify the changed artifact, parsed decisions,
  accepted deferral count, remaining ambiguity count, blocking diagnostic,
  generated-view state, and next action from the human-readable summary during
  review, and readiness evidence records that review against the
  text-projection output.
- **SC-007**: Clarification creation and update remain usable without
  Governance installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init`, `fsgg-sdd charter`, and `fsgg-sdd specify` already create
  the minimum SDD project and work-item state used by this feature.
- The clarify command operates on one selected work item at a time and writes
  the selected work item's clarification artifact under the configured work
  root.
- The clarification artifact is the durable source for clarification decisions;
  the specification remains the authored source for original user value, scope,
  stories, requirements, acceptance scenarios, and ambiguity records.
- A successful clarified result points to `checklist` as the next lifecycle
  command, but implementing `checklist` is outside this feature.
- Requirements-quality checklist generation and validation belong to the later
  checklist command.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
