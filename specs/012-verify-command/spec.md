# Feature Specification: Verify Command

**Feature Branch**: `012-verify-command`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
verification generated view, task/evidence/test/skill readiness facts, command
report, generated-view currency behavior, diagnostics, and optional Governance
boundary facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: continue Phase 6 task/evidence/verify
readiness by adding `fsgg-sdd verify` to run selected SDD-owned checks and
emit `readiness/<id>/verify.json`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify Evidence-Ready Work (Priority: P1)

As a project maintainer or coding agent, I need to verify an evidence-ready SDD
work item so that task completion, required tests, evidence declarations, and
generated readiness state are checked before the work can move toward ship
readiness.

**Why this priority**: `fsgg-sdd evidence` records what supports completed
work, but users still need an SDD-owned verification result that decides
whether the selected work item is ready for the merge-readiness stage. Without
this slice, completed tasks and evidence declarations remain authored claims
with no generated verification view.

**Independent Test**: Can be tested by running the verify command in an
initialized SDD project for one evidence-ready work item and confirming that
verification readiness, generated-view state, task/evidence/test/skill
findings, diagnostics, and next action are produced without requiring
Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and an evidence-ready work item with
   valid specification, clarification, checklist, plan, tasks, analysis, and
   evidence artifacts, **When** the user runs the verify command, **Then** the
   selected work item has a generated verification view at
   `readiness/<id>/verify.json` that records source relationships, source
   currency, task graph readiness, evidence dispositions, required test
   coverage, required skill visibility, SDD-owned verification findings, and
   lifecycle readiness.
2. **Given** every in-scope task, required evidence obligation, required test
   obligation, accepted deferral, generated view, and required skill or
   capability tag has a current supported disposition, **When** the command
   completes, **Then** the command report names the selected work id,
   generated verification artifact, parsed source facts, generated-view state,
   diagnostics, outcome, and `ship` as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user verifies a
   work item, **Then** SDD-only verification still succeeds and does not ask
   Governance to evaluate evidence freshness, routes, profiles, gates, audit,
   release policy, or protected-boundary enforcement.

---

### User Story 2 - Find Blocking Readiness Gaps (Priority: P1)

As a contributor preparing work for review, I need verification to identify
the exact task, evidence declaration, required test, skill requirement, or
generated view that is not ready so that I can fix the right lifecycle source.

**Why this priority**: Verification is the SDD-owned readiness check after
implementation evidence is recorded. If missing tests, stale evidence,
invalid task state, missing required skills, stale generated views, or
unaccepted deferrals are not reported precisely, ship readiness could be based
on incomplete work.

**Independent Test**: Can be tested by running the verify command against work
items with known readiness defects and verifying that no generated
verification view is treated as current until the report identifies the
affected artifact, identifier, severity, and correction.

**Acceptance Scenarios**:

1. **Given** a task graph has duplicate ids, unknown dependencies, unsupported
   status transitions, missing owners, missing requirement links, missing
   required evidence, missing required tests, or stale source snapshots,
   **When** verification runs, **Then** the report identifies the affected
   task or source artifact and blocks verification readiness.
2. **Given** evidence is missing, stale, synthetic without disclosure,
   deferred without an accepted rationale, linked to an unknown source, or
   insufficient for a required obligation, **When** verification evaluates
   readiness, **Then** the report identifies the evidence declaration,
   obligation, affected task, and correction before allowing ship readiness.
3. **Given** a task requires Claude, Codex, or another agent capability that is
   not visible in the lifecycle artifacts, **When** verification checks skill
   readiness, **Then** the report identifies the missing skill or capability
   tag and the task that depends on it.
4. **Given** a generated work model, analysis view, or verification view is
   missing, stale, malformed, or blocked by invalid source data, **When** the
   command evaluates generated-view currency, **Then** the report records a
   generated-view diagnostic instead of treating the existing generated file as
   current.

---

### User Story 3 - Preserve Authored Lifecycle Sources (Priority: P2)

As a user or agent, I need verification to be a non-destructive readiness
check so that diagnostics and generated views never silently rewrite authored
specifications, plans, tasks, or evidence declarations.

**Why this priority**: Verification must be safe to run repeatedly during
authoring, review, and CI. It should inspect authored lifecycle intent and
refresh generated readiness facts, not change the user's task or evidence
claims.

**Independent Test**: Can be tested by running verify with valid, blocked, and
dry-run scenarios and confirming that authored lifecycle artifacts remain
unchanged while generated verification output and reports reflect the current
source state.

**Acceptance Scenarios**:

1. **Given** authored lifecycle artifacts already exist, **When** the user runs
   the verify command, **Then** the command does not create, update, reorder,
   normalize, or remove authored source artifacts.
2. **Given** a generated verification view can be safely refreshed, **When**
   the command runs normally, **Then** only generated readiness artifacts are
   created or updated and the report records the generated artifact operation.
3. **Given** the user requests a dry run, **When** verification completes,
   **Then** proposed generated changes, diagnostics, readiness state, and next
   action are reported without modifying authored or generated artifacts.

---

### User Story 4 - Keep Verification Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need
verification outputs to be deterministic and traceable so that humans, agents,
and downstream tooling all read the same readiness facts.

**Why this priority**: Verification facts feed ship readiness, generated
summaries, and optional Governance consumers. The SDD verification contract
must be stable before those stages can rely on it.

**Independent Test**: Can be tested by running the same verification request
against the same project state multiple times and confirming that generated
verification data and machine-readable reports are stable, plain text
summaries contain no extra facts, and optional Governance references remain
advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and verification input, **When** the
   verify command is run repeatedly, **Then** generated verification data and
   machine-readable reports are identical for each run.
2. **Given** a user requests a human-readable summary, **When** the verify
   command completes, **Then** the summary reflects the same readiness state,
   finding counts, evidence dispositions, generated-view state, diagnostics,
   outcome, and next action as the authoritative command report.
3. **Given** optional Governance policy, capability, tooling, freshness, route,
   profile, gate, audit, or enforcement pointers are present in SDD-owned
   sources, **When** verification completes, **Then** the report may expose
   those pointers as compatibility facts but does not interpret or enforce
   Governance-owned decisions.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the evidence
  stage.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, plan, tasks, analysis, or
  evidence artifact.
- The specification, clarification, checklist, plan, tasks, analysis, or
  evidence artifact is missing, malformed, stale, or references unknown
  lifecycle facts.
- The task graph contains duplicate task ids, unknown dependencies, dependency
  cycles, unsupported status transitions, missing owners, missing requirement
  links, stale source snapshots, missing required skills, or missing required
  evidence obligations.
- A required test obligation is missing, stale, not linked to the affected
  task or requirement, synthetic without disclosure, or covered only by an
  unaccepted deferral.
- Evidence references an unknown task, requirement, acceptance scenario,
  clarification decision, checklist result, plan decision, generated view, or
  source artifact.
- A completed task lacks real evidence, an accepted deferral, or a synthetic
  disclosure that remains visible to later lifecycle stages.
- A task requires an agent skill or capability tag that is missing from the
  lifecycle artifacts available to the selected project.
- A source requirement, clarification decision, checklist result, plan
  decision, task, required skill, required evidence obligation, required test
  obligation, analysis finding, or evidence declaration changes after a later
  artifact captured a source snapshot.
- Required project settings or artifact layout settings exist but are
  malformed, stale, or point to missing lifecycle roots.
- A generated work model, analysis view, verification view, or summary exists
  but its source digests, schema version, or generator identity no longer match
  the current authored sources.
- The user requests a dry run and expects proposed verification changes and
  diagnostics without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for SDD-owned verification readiness and
  compatibility facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd verify` as the next native
  SDD lifecycle command after `fsgg-sdd evidence`.
- **FR-002**: The verify command MUST require an initialized SDD project, one
  valid selected work id, valid prerequisite lifecycle artifacts, and an
  evidence-ready work-item state before it reports ship readiness.
- **FR-003**: The verify command MUST load and validate the selected work
  item's specification, clarification, checklist, plan, tasks, analysis, and
  evidence facts, including work identity, lifecycle state, requirement ids,
  user-story ids, acceptance-scenario ids, clarification decision ids,
  checklist result ids, plan decision ids, task ids, task dependencies, task
  owners, task statuses, required skills, required evidence obligations,
  required test obligations, accepted deferrals, source snapshots, evidence
  declarations, evidence dispositions, and blocking finding state.
- **FR-004**: The verify command MUST generate or refresh the selected work
  item's `readiness/<id>/verify.json` view when valid source data exists,
  verification output is needed, and the run is not a dry run.
- **FR-005**: The verification view MUST capture work identity, source artifact
  relationships, source digests, schema versions, generator identity,
  lifecycle stage readiness, task graph readiness, evidence dispositions,
  required test dispositions, required skill visibility, generated-view
  currency, optional boundary facts, and diagnostics.
- **FR-006**: The verification contract MUST expose stable identifiers for
  verification findings, with structured links to affected requirements,
  acceptance scenarios, clarification decisions, checklist results, plan
  decisions, tasks, evidence obligations, test obligations, required skills,
  evidence declarations, generated views, accepted deferrals, or source
  artifacts when known.
- **FR-007**: The verify command MUST validate task graph structure,
  dependencies, ids, owners, source links, required skills, required evidence,
  required tests, and status transitions before reporting verification
  readiness.
- **FR-008**: The verify command MUST evaluate required SDD-owned test and
  evidence obligations derived from lifecycle rules, planning decisions, task
  metadata, changed artifact impact, accepted deferrals, and generated-view
  impacts.
- **FR-009**: The verify command MUST check that required Claude, Codex, or
  capability-tagged skills are visible in lifecycle artifacts before
  agent-driven task work is treated as verification-ready.
- **FR-010**: The verify command MUST map every completed task and required
  obligation to a current disposition of supported, deferred, missing, stale,
  synthetic, invalid, advisory, or blocking.
- **FR-011**: The verify command MUST distinguish real evidence, accepted
  deferrals, missing evidence, stale evidence, synthetic evidence with
  disclosure, synthetic evidence without disclosure, invalid references,
  missing required tests, stale required tests, missing skills, advisory notes,
  and blocking verification findings.
- **FR-012**: The verify command MUST NOT create, update, reorder, normalize,
  or remove authored lifecycle artifacts.
- **FR-013**: The verify command MUST mark verification results as stale or
  blocked when source facts have changed since prerequisite generated views,
  task snapshots, analysis findings, or evidence declarations were recorded.
- **FR-014**: The verify command MUST block ship readiness for selected-id
  mismatches, duplicated logical work ids, malformed prerequisite artifacts,
  unresolved blocking analysis findings, failed task graph validation, missing
  required skills, missing required tests, stale evidence, missing evidence,
  undisclosed synthetic evidence, invalid deferrals, unknown references,
  malformed generated views, and unsafe generated-view refresh conditions.
- **FR-015**: The verify command MUST record the selected work item's
  lifecycle state as verification-ready only when all blocking SDD-owned task,
  evidence, test, skill, generated-view, and prerequisite analysis findings are
  resolved or have accepted deferrals that remain visible to later lifecycle
  stages.
- **FR-016**: The verify command MUST identify `ship` as the next lifecycle
  action after a successful verification-ready result.
- **FR-017**: The verify command MUST identify implementation continuation,
  evidence correction, task correction, analysis rerun, generated-view
  refresh, missing-skill correction, required-test correction, or prerequisite
  lifecycle correction as the next action when blocking verification defects
  remain.
- **FR-018**: The verify command MUST report changed generated artifacts,
  preserved authored artifacts, refused generated artifacts, parsed source
  facts, task graph finding counts, evidence disposition counts, test
  disposition counts, skill visibility counts, generated-view state,
  diagnostics, outcome, and next action in the authoritative command report.
- **FR-019**: Machine-readable verification reports and generated verification
  views MUST be deterministic for identical project state and identical
  verification input.
- **FR-020**: Human-readable verification summaries MUST be projections of the
  same authoritative command report and MUST NOT introduce separate lifecycle
  facts.
- **FR-021**: The verify command MUST refresh or explicitly diagnose the
  selected work item's generated work-model and analysis views when
  verification depends on current normalized lifecycle state.
- **FR-022**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-023**: Verification diagnostics MUST use stable identifiers and include
  the affected artifact, severity, explanation, and user-correctable action.
- **FR-024**: Dry-run verification requests MUST report proposed generated
  artifact changes, diagnostics, readiness state, and next action without
  modifying authored or generated artifacts.
- **FR-025**: The verify command MUST work when Governance is not installed or
  configured.
- **FR-026**: Optional Governance policy, capability, tooling, freshness,
  routing, profile, gate, audit, enforcement, and release facts MUST remain
  advisory compatibility facts in SDD verification reports and MUST NOT be
  interpreted as SDD-owned enforcement decisions.
- **FR-027**: The feature MUST NOT introduce ship readiness, Governance
  effective-evidence freshness, route selection, profile adjustment, gate
  selection, protected-boundary enforcement, audit verdicts, release gating, or
  generated agent guidance behavior.

### Key Entities

- **Evidence-Ready Work Item**: The selected lifecycle unit that has a stable
  work id, valid prerequisite artifacts, current analysis state, authored
  evidence declarations, and a next expected lifecycle action.
- **Task Graph State**: The current task ids, dependencies, owners, statuses,
  requirement links, decision links, source snapshots, required skills,
  required evidence, required tests, and accepted deferrals that verification
  checks before ship readiness.
- **Required Verification Obligation**: A test, evidence, skill, generated
  view, or lifecycle proof point derived from SDD rules, planning decisions,
  task metadata, changed artifact impact, accepted deferrals, or generated-view
  impacts.
- **Evidence Disposition**: The current state of an evidence obligation, such
  as supported, deferred, missing, stale, synthetic, invalid, advisory, or
  blocking.
- **Required Test Disposition**: The current state of a required verification
  obligation, such as satisfied, deferred, missing, stale, synthetic, invalid,
  advisory, or blocking.
- **Skill Visibility Fact**: A declared skill or capability tag needed by a
  task, plus whether it is visible in the SDD lifecycle artifacts available to
  the selected project.
- **Verification View**: The generated `readiness/<id>/verify.json` artifact
  that records SDD-owned verification readiness, source currency, generated
  view state, task/evidence/test/skill findings, and diagnostics for the
  selected work item.
- **Verification Finding**: A stable user-correctable readiness result derived
  from lifecycle source artifacts, evidence declarations, task graph state,
  required tests, required skills, generated-view state, or optional boundary
  facts.
- **Verify Command Report**: The authoritative result of a verify command,
  including context, generated artifact changes, source summaries, readiness
  findings, generated-view state, diagnostics, outcome, and next action.
- **Verification Diagnostic**: A stable finding that explains invalid project
  context, missing prerequisite state, malformed lifecycle sources, stale
  generated views, task/evidence/test/skill readiness defects, or optional
  boundary issues.
- **Optional Boundary Fact**: An advisory SDD report fact that exposes
  Governance-compatible context without evaluating freshness, routing,
  profiles, gates, audit, enforcement, or release policy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with an evidence-ready work item, a
  user can generate SDD verification readiness and receive the next lifecycle
  action in one command result.
- **SC-002**: 100% of valid verification fixture families
  (`verify-create`, `verify-rerun-current`, `verify-preserves-authored`,
  `verify-refreshes-work-model`, `verify-refreshes-analysis`,
  `verify-accepted-deferral`, `dry-run`, `deterministic-report`,
  `text-projection`, and no-Governance `governance-boundary`) produce the
  expected verification view or proposed dry-run changes, selected work-id
  trace, successful outcome, and correct next action.
- **SC-003**: 100% of blocked verification fixture families
  (`outside-project`, `missing-specification`, `missing-clarification`,
  `missing-checklist`, `missing-plan`, `missing-tasks`, `missing-analysis`,
  `missing-evidence`, `failed-analysis`, `failed-tasks`,
  `malformed-work-id`, `malformed-verify-view`, `duplicate-work-id`,
  `unknown-source-reference`, `dependency-cycle`, `unsupported-task-status`,
  `missing-required-skill`, `missing-required-test`,
  `missing-required-evidence`, `stale-analysis`, `stale-tasks`,
  `stale-evidence`, `undisclosed-synthetic-evidence`, `invalid-deferral`,
  and `stale-generated-view`) leave authored lifecycle content unchanged and
  include at least one actionable diagnostic.
- **SC-004**: Three repeated verification runs over identical inputs produce
  identical generated verification views and machine-readable command reports.
- **SC-005**: 100% of missing evidence, stale evidence, missing required test,
  missing required skill, invalid task graph, unknown reference,
  undisclosed synthetic evidence, invalid deferral, malformed generated-view,
  and stale generated-view scenarios identify the affected artifact or
  identifier before blocking ship readiness.
- **SC-006**: Dry-run verification requests change 0 authored or generated
  files while still reporting proposed generated artifacts, diagnostics,
  readiness state, and next action.
- **SC-007**: Maintainers can identify the generated verification artifact,
  ready-finding count, advisory count, warning count, blocking count, evidence
  disposition counts, test disposition counts, skill visibility counts,
  generated-view state, and next action from the human-readable summary during
  review.
- **SC-008**: Verification remains usable without Governance installed in
  every no-Governance validation scenario.

## Assumptions

- The next applicable SDD-owned item from
  `docs/initial-implementation-plan.md` is `fsgg-sdd verify`, which follows
  the implemented `011-evidence-command` slice and covers the remaining
  SDD-owned Phase 6 readiness checks needed before ship readiness.
- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`,
  `fsgg-sdd clarify`, `fsgg-sdd checklist`, `fsgg-sdd plan`,
  `fsgg-sdd tasks`, `fsgg-sdd analyze`, and `fsgg-sdd evidence` already
  create the minimum SDD project and work-item state used by this feature.
- The verify command operates on one selected work item at a time and writes
  the generated verification view under the configured readiness root.
- The specification, clarification, checklist, plan, tasks, analysis, and
  evidence artifacts remain the authored or generated prerequisite sources of
  truth; the verification view is a generated view over those sources and is
  not an authored source of lifecycle intent.
- Required skill availability is evaluated from SDD lifecycle artifacts and
  declared agent/capability metadata visible to the project, not from live
  network discovery or Governance gate enforcement.
- A successful verification-ready result points to `ship` as the next
  lifecycle action, but producing ship readiness is outside this feature.
- Effective evidence freshness, route selection, profile adjustment, gate
  selection, protected-boundary enforcement, audit verdicts, and release policy
  remain Governance-owned concerns.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD
  remains independently usable without Governance installed.
