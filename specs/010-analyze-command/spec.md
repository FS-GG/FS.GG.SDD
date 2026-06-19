# Feature Specification: Analyze Command

**Feature Branch**: `010-analyze-command`

**Created**: 2026-06-19

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
analysis generated view, cross-artifact consistency diagnostics, lifecycle
readiness state, command report, generated-view currency behavior, and optional
Governance boundary facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: add `fsgg-sdd analyze`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Analyze Lifecycle Consistency (Priority: P1)

As a project maintainer or coding agent, I need to analyze a tasks-ready SDD
work item across its lifecycle artifacts so that inconsistencies are found
before implementation work starts.

**Why this priority**: `fsgg-sdd analyze` is the next native lifecycle action
after `tasks`. Without it, users can create a task graph but cannot get a
single SDD-owned consistency result that explains whether requirements,
clarifications, checklist findings, planning decisions, task links, and
generated views agree.

**Independent Test**: Can be tested by running the analyze command in an
initialized SDD project for one tasks-ready work id and confirming that the
analysis view, lifecycle consistency facts, command report, generated-view
state, diagnostics, and next action are produced without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a tasks-ready work item with valid
   specification, clarification, checklist, plan, and tasks artifacts, **When**
   the user runs the analyze command, **Then** the selected work item has a
   generated analysis view at `readiness/<id>/analysis.json` that records
   source relationships, source currency, cross-artifact consistency findings,
   task-readiness findings, diagnostics, and lifecycle readiness.
2. **Given** every in-scope lifecycle source is current and consistent, **When**
   the command completes, **Then** the command report names the selected work
   id, generated analysis artifact, parsed source facts, generated-view state,
   diagnostics, outcome, and implementation as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user analyzes a
   work item, **Then** SDD-only analysis still succeeds and does not ask
   Governance to evaluate policy, freshness, routes, profiles, gates, or
   enforcement.

---

### User Story 2 - Report Cross-Artifact Defects (Priority: P1)

As a contributor maintaining a work item, I need analysis to identify exactly
which authored artifact or generated view is inconsistent so that I can correct
the lifecycle source instead of guessing.

**Why this priority**: Analysis is the last SDD consistency check before
implementation starts. If stale source snapshots, missing dispositions,
unknown references, unresolved ambiguity, failed checklist findings, stale plan
decisions, stale tasks, dependency defects, or generated-view drift are not
reported precisely, implementation work can proceed from a false premise.

**Independent Test**: Can be tested by running the analyze command against
work items with known lifecycle defects and verifying that no generated
analysis view is treated as current until the report identifies the affected
artifact, identifier, severity, and correction.

**Acceptance Scenarios**:

1. **Given** a requirement, acceptance scenario, clarification decision,
   checklist result, plan decision, contract reference, verification
   obligation, task, dependency, required skill, required evidence obligation,
   or accepted deferral references an unknown or stale source, **When** the
   user runs analysis, **Then** the report identifies the affected source,
   identifier, and correction before allowing implementation readiness.
2. **Given** the work item has unresolved ambiguity, failed requirements-quality
   results, incomplete planning decisions, missing task dispositions, stale
   tasks, dependency cycles, unsupported task states, or malformed source
   metadata, **When** analysis evaluates readiness, **Then** the report blocks
   implementation readiness and identifies the lifecycle command or artifact
   that should be corrected.
3. **Given** the generated work model or generated analysis view is missing,
   stale, malformed, or blocked by invalid source data, **When** the command
   evaluates generated-view currency, **Then** the report records a generated
   view diagnostic instead of treating the existing generated file as current.

---

### User Story 3 - Preserve Authored Sources During Analysis (Priority: P2)

As a user or agent, I need analysis to be a non-destructive readiness check so
that diagnostics and generated views never silently rewrite authored lifecycle
intent.

**Why this priority**: Analyze should inspect and generate readiness facts, not
change the meaning of specifications, clarifications, checklists, plans, or
tasks. Users must be able to run it repeatedly during authoring and in CI
without losing source edits or task state.

**Independent Test**: Can be tested by running analyze with valid, blocked,
and dry-run scenarios and confirming that authored lifecycle artifacts remain
unchanged while generated analysis output and reports reflect the current
source state.

**Acceptance Scenarios**:

1. **Given** authored lifecycle artifacts already exist, **When** the user runs
   the analyze command, **Then** the command does not create, update, reorder,
   or normalize authored source artifacts.
2. **Given** a generated analysis view can be safely refreshed, **When** the
   command runs normally, **Then** only generated readiness artifacts are
   created or updated and the report records the generated artifact operation.
3. **Given** the user requests a dry run, **When** analysis completes, **Then**
   proposed generated changes, diagnostics, and next action are reported
   without modifying authored or generated artifacts.

---

### User Story 4 - Keep Analysis Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need
analysis outputs to be deterministic and traceable so that humans, agents, and
downstream tooling all read the same readiness facts.

**Why this priority**: Analysis facts feed implementation decisions, later
evidence declarations, verification readiness, ship readiness, generated
summaries, and optional Governance-compatible checks. The report shape and
generated view must be stable before those stages build on it.

**Independent Test**: Can be tested by running the same analysis request
against the same project state multiple times and confirming that generated
analysis data and machine-readable reports are stable, plain text summaries
contain no extra facts, and optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and analysis input, **When** the analyze
   command is run repeatedly, **Then** generated analysis data and
   machine-readable reports are identical for each run.
2. **Given** a user requests a human-readable summary, **When** the analyze
   command completes, **Then** the summary reflects the same consistency
   counts, readiness state, generated-view state, diagnostics, and next action
   as the authoritative command report.
3. **Given** optional Governance policy, capability, or tooling pointers are
   present in SDD-owned sources, **When** analysis completes, **Then** the report
   may expose those pointers as compatibility facts but does not interpret or
   enforce Governance policy.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the tasks stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, plan, or tasks artifact.
- The specification, clarification, checklist, plan, or tasks artifact is
  missing, malformed, stale, or references unknown lifecycle facts.
- The checklist artifact contains unresolved blocking failures, stale results,
  or accepted deferrals that are not visible to later lifecycle stages.
- The plan artifact contains incomplete decisions, stale source snapshots,
  missing contract references, missing verification obligations, unresolved
  migration posture, or generated-view impacts that are not represented in
  tasks.
- The tasks artifact contains duplicate task ids, unknown dependencies,
  dependency cycles, unsupported task states, missing source links, stale task
  entries, missing dispositions, or completed tasks without required evidence
  where such state is already recorded.
- A requirement, acceptance scenario, clarification decision, checklist result,
  plan decision, contract reference, verification obligation, task,
  dependency, evidence obligation, or accepted deferral changes after a later
  artifact captured a source snapshot.
- Required project settings or artifact layout settings exist but are
  malformed, stale, or point to missing lifecycle roots.
- A generated work model or analysis view exists but its source digests, schema
  version, or generator identity no longer match the current authored sources.
- The user requests a dry run and expects proposed generated-view changes and
  diagnostics without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for lifecycle analysis and compatibility facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd analyze` as the next native
  SDD lifecycle command after `fsgg-sdd tasks`.
- **FR-002**: The analyze command MUST require an initialized SDD project, one
  valid selected work id, valid prerequisite lifecycle artifacts, and a
  tasks-ready work-item state before it reports implementation readiness.
- **FR-003**: The analyze command MUST load and validate the selected work
  item's specification, clarification, checklist, plan, and tasks facts,
  including work identity, lifecycle state, requirement ids, user-story ids,
  acceptance-scenario ids, clarification decision ids, checklist result ids,
  plan decision ids, contract references, verification obligations, migration
  notes, generated-view impacts, task ids, task dependencies, required skills,
  required evidence obligations, accepted deferrals, source snapshots, and
  blocking finding state.
- **FR-004**: The analyze command MUST generate or refresh the selected work
  item's `readiness/<id>/analysis.json` view when valid source data exists and
  generated analysis is needed.
- **FR-005**: The analysis view MUST capture work identity, source artifact
  relationships, source digests, schema versions, generator identity,
  lifecycle stage readiness, cross-artifact consistency findings,
  task-readiness findings, generated-view currency, optional boundary facts,
  and diagnostics.
- **FR-006**: The analysis contract MUST expose stable identifiers for analysis
  findings, with structured links to affected requirements, acceptance
  scenarios, clarification decisions, checklist results, plan decisions,
  contract references, verification obligations, tasks, dependencies,
  generated views, accepted deferrals, or source artifacts when known.
- **FR-007**: The analyze command MUST verify that every in-scope requirement,
  acceptance scenario, clarification decision, checklist result, plan decision,
  contract reference, verification obligation, migration note,
  generated-view impact, accepted deferral, task dependency, required skill,
  and required evidence obligation has a visible current disposition.
- **FR-008**: The analyze command MUST distinguish ready findings, advisory
  findings, warnings, blocking findings, stale source findings, missing
  disposition findings, malformed source findings, and generated-view currency
  findings.
- **FR-009**: The analyze command MUST NOT create, update, reorder, normalize,
  or remove authored lifecycle artifacts.
- **FR-010**: The analyze command MUST mark analysis results as stale or
  blocked when source facts have changed since the generated analysis view or
  prerequisite generated work model was recorded.
- **FR-011**: The analyze command MUST block implementation readiness for
  selected-id mismatches, duplicated logical work ids, malformed prerequisite
  artifacts, unresolved blocking ambiguity, failed blocking checklist results,
  stale checklist results, incomplete plan decisions, stale plan decisions,
  missing task dispositions, stale tasks, duplicate task ids, unknown
  references, dependency cycles, unsupported task states, malformed generated
  views, and unsafe generated-view refresh conditions.
- **FR-012**: The analyze command MUST report completed tasks without required
  evidence as analysis findings when completed task state or evidence
  obligations are already recorded.
- **FR-013**: The analyze command MUST record the selected work item's
  lifecycle state as implementation-ready only when all blocking lifecycle
  consistency findings are resolved or have accepted deferrals that remain
  visible to later lifecycle stages.
- **FR-014**: The analyze command MUST identify implementation as the next
  lifecycle action after a successful implementation-ready result.
- **FR-015**: The analyze command MUST identify specification, clarification,
  checklist, plan, tasks, or generated-view correction as the next action when
  blocking source defects, unresolved ambiguity, failed checklist results,
  stale planning decisions, stale tasks, unknown references, malformed
  identifiers, missing dispositions, dependency defects, or stale generated
  views remain.
- **FR-016**: The analyze command MUST report changed generated artifacts,
  preserved authored artifacts, refused generated artifacts, parsed source
  facts, consistency finding counts, generated-view state, diagnostics,
  outcome, and next action in the authoritative command report.
- **FR-017**: Machine-readable analysis reports and generated analysis views
  MUST be deterministic for identical project state and identical analysis
  input.
- **FR-018**: Human-readable analysis summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-019**: The analyze command MUST refresh or explicitly diagnose the
  generated work-model view for the selected work item when analysis depends on
  current normalized lifecycle state.
- **FR-020**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-021**: Analysis diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-022**: The analyze command MUST support dry-run execution that reports
  proposed generated artifact changes, diagnostics, readiness state, and next
  action without mutating authored or generated artifacts.
- **FR-023**: The analyze command MUST work when Governance is not installed or
  configured.
- **FR-024**: The analyze command MAY expose optional Governance policy,
  capability, or tooling pointers as compatibility facts, but MUST NOT evaluate
  routes, evidence freshness, profiles, gates, protected-boundary verdicts, or
  release policy.
- **FR-025**: The feature MUST NOT introduce implementation execution,
  evidence update commands, verify, ship, release, generated agent guidance,
  route selection, freshness evaluation, profile adjustment, gate selection, or
  Governance enforcement behavior.

### Key Entities

- **Tasks-Ready Work Item**: The selected lifecycle unit that has a stable work
  id, valid prerequisite artifacts, current task graph state, and a next
  expected lifecycle action.
- **Specification Facts**: The existing specification identity, stories,
  requirements, acceptance scenarios, scope boundaries, success criteria, edge
  cases, assumptions, and ambiguity records that analysis checks for current
  downstream dispositions.
- **Clarification Facts**: The clarification questions, answers, decisions,
  accepted deferrals, and remaining ambiguity state that analysis checks
  against requirements, checklist results, plan decisions, and tasks.
- **Checklist Facts**: The checklist items, review results, accepted
  deferrals, blocking findings, stale result state, and source links that
  determine whether planning and task work can be trusted.
- **Plan Facts**: The plan decisions, contract references, verification
  obligations, migration posture, generated-view impacts, accepted deferrals,
  source snapshots, stale decision state, and planning notes that analysis
  checks against task dispositions.
- **Task Facts**: The task entries, dependencies, statuses, owners,
  requirement links, decision links, required skills, required evidence
  obligations, skip rationales, stale task state, and lifecycle notes that
  determine implementation readiness.
- **Analysis View**: The generated `readiness/<id>/analysis.json` artifact
  that records cross-artifact consistency diagnostics, source currency,
  generated-view state, and implementation-readiness facts for the selected
  work item.
- **Analysis Finding**: A stable user-correctable consistency or readiness
  result derived from lifecycle source artifacts, task graph state,
  generated-view state, or optional boundary facts.
- **Analysis Command Report**: The authoritative result of an analyze command,
  including context, generated artifact changes, source summaries, consistency
  findings, generated-view state, diagnostics, outcome, and next action.
- **Generated Work-Model State**: The reported currency of the normalized
  lifecycle view that analysis depends on.
- **Analysis Diagnostic**: A stable finding that explains invalid project
  context, missing prerequisite state, malformed lifecycle sources, stale
  generated views, cross-artifact inconsistency, task-readiness defects, or
  optional boundary issues.
- **Next Lifecycle Action**: The command, correction, or implementation step
  the user should perform after the analyze command result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a tasks-ready work item, a
  user can generate cross-artifact analysis and receive the next lifecycle
  action in one command result.
- **SC-002**: 100% of valid analysis fixture families
  (`analysis-create`, `analysis-rerun-current`, `analysis-preserves-authored`,
  `analysis-refreshes-work-model`, `analysis-accepted-deferral`, `dry-run`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected analysis view or proposed
  dry-run changes, selected work-id trace, successful outcome, and correct next
  action.
- **SC-003**: 100% of blocked analysis fixture families (`outside-project`,
  `missing-specification`, `missing-clarification`, `missing-checklist`,
  `missing-plan`, `missing-tasks`, `failed-checklist`, `failed-plan`,
  `failed-tasks`, `malformed-work-id`, `malformed-analysis`,
  `duplicate-work-id`, `unknown-source-reference`, `dependency-cycle`,
  `stale-plan`, `stale-tasks`, `analysis-identity-mismatch`,
  `done-task-missing-evidence`, and `stale-generated-view`) leave authored
  lifecycle content unchanged and include at least one actionable diagnostic.
- **SC-004**: Three repeated analysis runs over identical inputs produce
  identical generated analysis views and machine-readable command reports.
- **SC-005**: 100% of unresolved ambiguity, failed checklist, stale plan,
  stale task, unknown reference, dependency-cycle, missing-disposition, and
  malformed generated-view scenarios identify the affected artifact or
  identifier before blocking implementation readiness.
- **SC-006**: Maintainers can identify the generated analysis artifact,
  ready-finding count, advisory count, warning count, blocking count, stale
  source count, missing disposition count, generated-view state, and next
  action from the human-readable summary during review, and readiness evidence
  records that review against the text-projection output.
- **SC-007**: 100% of failed analysis-readiness scenarios identify
  specification, clarification, checklist, plan, tasks, or generated-view
  correction as the next action instead of allowing implementation readiness.
- **SC-008**: Analysis remains usable without Governance installed in every
  no-Governance validation scenario.

## Assumptions

- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`,
  `fsgg-sdd clarify`, `fsgg-sdd checklist`, `fsgg-sdd plan`, and
  `fsgg-sdd tasks` already create the minimum SDD project and work-item state
  used by this feature.
- The analyze command operates on one selected work item at a time and writes
  the generated analysis view under the configured readiness root.
- The specification, clarification, checklist, plan, and tasks artifacts remain
  the authored sources of truth; the analysis view is a generated view over
  those sources and is not an authored source of lifecycle intent.
- A successful implementation-ready result points to implementation as the
  next lifecycle action, but executing implementation work is outside this
  feature.
- Evidence authoring, evidence freshness, verify readiness, ship readiness,
  generated agent guidance, and Governance enforcement belong to later
  lifecycle features or to Governance-owned integrations.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
