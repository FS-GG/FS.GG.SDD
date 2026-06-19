# Feature Specification: Tasks Command

**Feature Branch**: `009-tasks-command`

**Created**: 2026-06-19

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface, typed
task artifact, task graph traceability, lifecycle state, command report,
generated-view currency behavior, diagnostics, and optional Governance boundary
facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd tasks`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create A Traceable Task Graph (Priority: P1)

As a project maintainer or coding agent, I need to turn a planned SDD work item
into a typed task graph so that implementation work has stable tasks,
dependencies, owners, required skills, and evidence obligations before work
starts.

**Why this priority**: `fsgg-sdd tasks` is the next native lifecycle action
after `plan`. Without it, users can record planning decisions but cannot
produce the durable task graph that implementation, analysis, evidence,
readiness views, agents, and optional Governance consumers need.

**Independent Test**: Can be tested by running the tasks command in an
initialized SDD project for one planned work id and confirming that the tasks
artifact, task graph facts, lifecycle state, command report, generated-view
state, diagnostics, and next action are produced without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a planned work item with valid
   specification, clarification, checklist, and plan artifacts, **When** the
   user runs the tasks command, **Then** the selected work item has a tasks
   artifact containing identity, source links, task entries, stable task ids,
   owners, dependencies, requirement links, decision links, required skills,
   required evidence obligations, and lifecycle notes.
2. **Given** every blocking planning obligation is represented by a task or an
   accepted deferral that remains visible, **When** the command completes,
   **Then** the command report names the selected work id, changed artifact,
   parsed task facts, task graph readiness, generated-view state, diagnostics,
   outcome, and `analyze` as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user creates a
   task graph, **Then** SDD-only task authoring still succeeds and does not ask
   Governance to evaluate policy, freshness, routes, profiles, gates, or
   enforcement.

---

### User Story 2 - Preserve And Refresh Existing Tasks (Priority: P1)

As a contributor maintaining a work item, I need task reruns to preserve
existing task ids, statuses, ownership, dependencies, and evidence references
while detecting when changed plan facts make tasks stale, so that implementation
work is not silently discarded or misrepresented.

**Why this priority**: Once implementation begins, task ids and status carry
coordination value. Reruns must support safe additions from updated plans
without renumbering existing tasks, losing skip rationales, changing dependency
meaning, or treating stale source links as current.

**Independent Test**: Can be tested by running the tasks command against a work
item with an existing tasks artifact and verifying that compatible new tasks
are added, existing task state remains stable, stale task links are reported,
and conflicting updates block before writing.

**Acceptance Scenarios**:

1. **Given** an existing tasks artifact with stable task ids, dependencies,
   statuses, owners, required skills, and evidence obligations, **When** the
   user reruns the tasks command after compatible planning additions, **Then**
   existing task state is preserved, new required tasks are added, and the
   report shows the safe additions.
2. **Given** a requirement, clarification decision, checklist result, plan
   decision, contract reference, verification obligation, migration note, or
   generated-view impact has changed since a task was recorded, **When** the
   user reruns the tasks command, **Then** the affected task is reported as
   stale or needing review instead of being treated as current.
3. **Given** a proposed update would remove, renumber, reorder destructively,
   or semantically change an existing task, dependency, status, owner, required
   skill, or evidence obligation without a safe replacement path, **When** the
   command validates the update, **Then** it refuses to write and reports the
   conflict as a blocking diagnostic.

---

### User Story 3 - Diagnose Task Readiness Problems (Priority: P2)

As a user or agent, I need invalid or incomplete task requests to fail with
actionable diagnostics so that I can fix the correct lifecycle artifact before
analysis or implementation proceeds.

**Why this priority**: Task defects cascade into implementation work, evidence
declarations, generated readiness views, and optional Governance-compatible
checks. Missing prerequisites, malformed graphs, stale source facts, dependency
cycles, unknown references, or unsafe task updates must be visible before work
starts.

**Independent Test**: Can be tested by invoking the tasks command outside an
SDD project, before planning, with failed planning state, malformed work ids,
malformed task data, duplicate task ids, dependency cycles, unknown source
references, unsupported status changes, missing evidence for completed tasks,
and unsafe overwrite situations, then confirming that no unsafe write occurs
and each result contains a stable diagnostic and correction.

**Acceptance Scenarios**:

1. **Given** the current directory is not an initialized SDD project, **When**
   the user runs the tasks command, **Then** no work artifact is created and
   the report explains how to initialize or select an SDD project.
2. **Given** the selected work id is malformed or has not reached a valid
   planned state, **When** the user runs the tasks command, **Then** no tasks
   artifact is created and the report identifies the missing or invalid
   prerequisite.
3. **Given** the task graph has duplicate task ids, a dependency cycle, unknown
   requirement or decision references, missing source links, unsupported status
   changes, or completed tasks without evidence, **When** the tasks command
   validates readiness, **Then** the report identifies task correction as the
   next action instead of allowing analysis to proceed.
4. **Given** the tasks artifact or generated view is stale, malformed, or
   inconsistent with source data, **When** the command cannot safely refresh
   the lifecycle view, **Then** the report records a stale or blocked
   generated-view diagnostic instead of treating the existing generated file as
   current.

---

### User Story 4 - Keep Task Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need task
command outputs to be deterministic and traceable so that humans, agents, and
downstream tooling all read the same task facts.

**Why this priority**: Task facts feed analysis, implementation, evidence
declarations, verification readiness, ship readiness, generated views, and
optional Governance-compatible contracts. The report shape must be stable
before those stages build on it.

**Independent Test**: Can be tested by running the same tasks request against
the same project state multiple times and confirming that machine-readable
reports are stable, plain text summaries contain no extra facts, and optional
Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and task input, **When** the tasks command
   is run repeatedly, **Then** the machine-readable report is identical for
   each run.
2. **Given** a user requests a human-readable summary, **When** the tasks
   command completes, **Then** the summary reflects the same changed artifacts,
   task counts, dependency state, evidence obligations, diagnostics,
   generated-view state, and next action as the authoritative command report.
3. **Given** optional Governance policy pointers are present in SDD-owned
   sources, **When** the tasks command completes, **Then** the report may expose
   those pointers as compatibility facts but does not interpret or enforce
   Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the plan stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, plan, or tasks artifact.
- The specification, clarification, checklist, or plan artifact is missing,
  malformed, stale, or references unknown lifecycle facts.
- The plan artifact lacks required planning decisions, contract references,
  verification obligations, migration posture, generated-view impact, accepted
  deferrals, or source links needed to derive tasks.
- Existing tasks, dependencies, statuses, owners, required skills, evidence
  obligations, source links, skip rationales, or stable ids would be removed,
  renumbered, or semantically changed by the proposed update.
- Two tasks use the same stable identifier.
- A task dependency references an unknown task, references itself, or creates a
  cycle.
- A task references an unknown requirement, decision, evidence obligation, or
  source artifact.
- A task marked complete lacks supporting evidence declarations when evidence
  facts are present or required for that status.
- A source requirement, clarification decision, checklist result, plan decision,
  contract reference, verification obligation, accepted deferral, or
  generated-view impact changes after a task was recorded.
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

- **FR-001**: The feature MUST provide `fsgg-sdd tasks` as the next native SDD
  lifecycle command after `fsgg-sdd plan`.
- **FR-002**: The tasks command MUST require an initialized SDD project, one
  valid selected work id, valid prerequisite lifecycle artifacts, and a valid
  planned work-item state before it plans any task artifact changes.
- **FR-003**: The tasks command MUST load and validate the selected work item's
  specification, clarification, checklist, and plan facts, including work
  identity, lifecycle state, requirement ids, user-story ids,
  acceptance-scenario ids, clarification decision ids, checklist result ids,
  plan decision ids, contract references, verification obligations, accepted
  deferrals, generated-view impacts, source links, and blocking finding state.
- **FR-004**: The tasks command MUST create a tasks artifact for the selected
  work item when task graph authoring is needed and a safe tasks artifact does
  not already exist.
- **FR-005**: A tasks artifact MUST capture work identity, source artifact
  relationships, task entries, stable task ids, titles, statuses, owners,
  dependencies, requirement links, plan decision links, required skills or
  capability tags, required evidence obligations, skip rationales, and
  lifecycle notes needed before analysis or implementation.
- **FR-006**: The tasks contract MUST expose stable identifiers for task
  entries, with structured links to relevant requirements, acceptance
  scenarios, clarification decisions, checklist results, plan decisions,
  contract references, verification obligations, accepted deferrals, and
  generated-view impacts when known.
- **FR-007**: The tasks command MUST map planned source facts to task entries
  so that every in-scope requirement, required planning decision, contract
  impact, verification obligation, migration note, generated-view impact, and
  accepted deferral has a visible task disposition or accepted deferral.
- **FR-008**: The tasks command MUST distinguish pending tasks, in-progress
  tasks, completed tasks, skipped tasks with rationale, stale tasks, blocking
  task-graph findings, missing task dispositions, and non-blocking advisory
  notes.
- **FR-009**: The tasks command MUST preserve existing authored task content,
  statuses, owners, dependencies, required skills, required evidence
  obligations, skip rationales, and stable identifiers unless it can report a
  safe, non-destructive update.
- **FR-010**: The tasks command MUST mark task entries as stale or needing
  review when the source facts they reference have changed since the task was
  recorded.
- **FR-011**: The tasks command MUST refuse unsafe overwrites, selected-id
  mismatches, duplicated logical work ids, duplicate task ids, dependency
  cycles, unknown source references, missing required identifiers, malformed
  existing task data, and unsupported destructive status changes before any
  authored artifact is changed.
- **FR-012**: The tasks command MUST report completed tasks without required
  evidence as task-readiness defects when the selected work item already
  records completed task state.
- **FR-013**: The tasks command MUST record the selected work item's lifecycle
  state as tasks-ready when all blocking task prerequisites and required task
  dispositions are complete or have accepted deferrals that remain visible to
  later lifecycle stages.
- **FR-014**: The tasks command MUST identify `analyze` as the next lifecycle
  action after a successful tasks-ready result.
- **FR-015**: The tasks command MUST identify specification, clarification,
  checklist, plan, or tasks correction as the next action when blocking source
  defects, stale task entries, unknown references, malformed identifiers,
  dependency cycles, missing task dispositions, or unsupported status changes
  remain.
- **FR-016**: The tasks command MUST report changed artifacts, preserved
  artifacts, refused artifacts, parsed task facts, task graph readiness,
  generated-view state, diagnostics, outcome, and next action in the
  authoritative command report.
- **FR-017**: Machine-readable task reports MUST be deterministic for identical
  project state and identical task input.
- **FR-018**: Human-readable task summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-019**: The tasks command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when task sources affect
  generated lifecycle state.
- **FR-020**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-021**: Task diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-022**: The tasks command MUST support dry-run execution that plans
  proposed authored and generated artifact changes, reports diagnostics and
  next action, and does not mutate authored or generated artifacts.
- **FR-023**: The tasks command MUST work when Governance is not installed or
  configured.
- **FR-024**: The tasks command MAY expose optional Governance policy pointers
  as compatibility facts, but MUST NOT evaluate routes, evidence freshness,
  profiles, gates, protected-boundary verdicts, or release policy.
- **FR-025**: The feature MUST NOT introduce `analyze` implementation, evidence
  update commands, verify, ship, release, generated agent guidance, route
  selection, freshness evaluation, profile adjustment, gate selection, or
  Governance enforcement behavior.

### Key Entities

- **Tasks-Ready Work Item**: The selected lifecycle unit that has a stable work
  id, valid prerequisite artifacts, current task graph state, and a next
  expected lifecycle action.
- **Specification Facts**: The existing specification identity, stories,
  requirements, acceptance scenarios, scope boundaries, success criteria, edge
  cases, assumptions, and ambiguity records that tasks must trace back to.
- **Clarification Facts**: The clarification questions, answers, decisions,
  accepted deferrals, and remaining ambiguity state that shape task scope.
- **Checklist Facts**: The checklist items, review results, accepted
  deferrals, blocking findings, stale result state, and source links that
  determine whether task generation can proceed.
- **Plan Facts**: The plan decisions, contract references, verification
  obligations, migration posture, generated-view impacts, accepted deferrals,
  source snapshots, stale decision state, and planning notes that drive task
  graph authoring.
- **Tasks Artifact**: The authored source that records task entries,
  dependencies, statuses, owners, requirement links, decision links, required
  skills, required evidence obligations, skip rationales, and lifecycle notes
  for the selected work item.
- **Task Entry**: A stable implementation work item with a task id, title,
  status, owner, dependencies, source links, required skills, and required
  evidence obligations.
- **Task Dependency**: A directed relationship showing that one task must be
  completed or reviewed before another task can safely proceed.
- **Evidence Obligation**: A task-level declaration of the implementation,
  verification, synthetic, deferral, or missing-evidence proof that later
  lifecycle stages must resolve.
- **Task Finding**: A user-correctable task-readiness issue derived from source
  artifacts, existing task content, generated-view state, or optional boundary
  facts.
- **Task Command Report**: The authoritative result of a tasks command,
  including context, artifact changes, task facts, graph readiness,
  generated-view state, diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the generated
  lifecycle view affected by task sources.
- **Task Diagnostic**: A stable finding that explains invalid project context,
  missing prerequisite state, malformed work id, incomplete task graph, unsafe
  content changes, stale generated views, or optional boundary issues.
- **Next Lifecycle Action**: The command or correction the user should perform
  after the tasks command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a planned work item, a user can
  create a traceable task graph and receive the next lifecycle action in one
  command result.
- **SC-002**: 100% of valid task fixture families (`tasks-create`,
  `tasks-rerun-preserves-status`, `tasks-adds-missing-items`,
  `tasks-preserves-stable-ids`, `tasks-records-required-skills`,
  `tasks-records-evidence-obligations`, `tasks-accepted-deferral`,
  `dry-run`, `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected tasks artifact or proposed
  dry-run changes, selected work-id trace, successful outcome, and correct next
  action.
- **SC-003**: 100% of blocked task fixture families (`outside-project`,
  `missing-specification`, `missing-clarification`, `missing-checklist`,
  `missing-plan`, `failed-plan`, `malformed-work-id`, `malformed-tasks`,
  `duplicate-work-id`, `duplicate-task-id`, `unknown-source-reference`,
  `dependency-cycle`, `tasks-identity-mismatch`, `unsafe-overwrite`,
  `done-task-missing-evidence`, and `stale-generated-view`) leave authored
  task content unchanged and include at least one actionable diagnostic.
- **SC-004**: Three repeated tasks runs over identical inputs produce identical
  machine-readable command reports.
- **SC-005**: 100% of unsafe overwrite, stale source, dependency-cycle, and
  identifier-renumbering scenarios identify the affected artifact or identifier
  before refusing or marking the task graph stale.
- **SC-006**: Maintainers can identify the changed artifact, task count,
  dependency count, required skill count, required evidence obligation count,
  skipped task count, stale task count, generated-view state, and next action
  from the human-readable summary during review, and readiness evidence records
  that review against the text-projection output.
- **SC-007**: 100% of failed task-readiness scenarios identify specification,
  clarification, checklist, plan, or tasks correction as the next action
  instead of allowing analysis or implementation to proceed.
- **SC-008**: Task creation and update remain usable without Governance
  installed in every no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`,
  `fsgg-sdd clarify`, `fsgg-sdd checklist`, and `fsgg-sdd plan` already create
  the minimum SDD project and work-item state used by this feature.
- The tasks command operates on one selected work item at a time and writes the
  selected work item's tasks artifact under the configured work root.
- The plan artifact is the durable source for planning decisions, contract
  impact, migration posture, verification obligations, and generated-view
  impacts; the tasks artifact is the durable source for task entries, task
  state, dependencies, owners, required skills, and required evidence
  obligations.
- A successful tasks-ready result points to `analyze` as the next lifecycle
  command, but implementing `analyze` is outside this feature.
- Evidence authoring, evidence freshness, verify readiness, ship readiness,
  generated agent guidance, and Governance enforcement belong to later
  lifecycle features or to Governance-owned integrations.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
