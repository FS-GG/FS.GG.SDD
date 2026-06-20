# Feature Specification: Ship Command

**Feature Branch**: `013-ship-command`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
ship-readiness generated view, merge-boundary readiness facts, command report,
generated-view currency behavior, diagnostics, and optional Governance boundary
facts)

**Input**: User description: "Next applicable SDD-owned item in
`docs/initial-implementation-plan.md`: continue Phase 6 task/evidence/verify/ship
readiness by adding `fsgg-sdd ship` to produce SDD merge-boundary readiness in
`readiness/<id>/ship.json`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ship-Ready a Verification-Ready Work Item (Priority: P1)

As a project maintainer or coding agent, I need to produce SDD merge-boundary
readiness for a verification-ready work item so that a single deterministic
`readiness/<id>/ship.json` view records whether the selected work item is ready
to hand to a protected boundary, without SDD enforcing the merge itself.

**Why this priority**: `fsgg-sdd verify` decides whether a work item passed
SDD-owned verification, but users still need an SDD-owned merge-boundary
readiness artifact that aggregates the full lifecycle into one stable result for
CI, agents, and optional Governance consumers. Without this slice, a
verification-ready work item has no generated ship-readiness view to point at the
protected boundary.

**Independent Test**: Can be tested by running the ship command in an
initialized SDD project for one verification-ready work item and confirming that
ship readiness, generated-view state, aggregated lifecycle readiness, blockers,
warnings, diagnostics, and next action are produced without requiring
Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a verification-ready work item with
   valid specification, clarification, checklist, plan, tasks, analysis,
   evidence, and verification artifacts, **When** the user runs the ship
   command, **Then** the selected work item has a generated ship-readiness view
   at `readiness/<id>/ship.json` that records source relationships, source
   currency, aggregated lifecycle stage readiness, verification readiness,
   evidence dispositions, blocking findings, warnings, SDD-owned ship-readiness
   outcome, and merge-boundary readiness facts.
2. **Given** every prerequisite lifecycle stage, generated view, verification
   finding, and evidence disposition is current and supported, **When** the
   command completes, **Then** the command report names the selected work id,
   generated ship-readiness artifact, parsed source facts, generated-view state,
   blocker and warning counts, diagnostics, outcome, and the protected-boundary
   handoff as the next lifecycle action.
3. **Given** optional Governance files are absent, **When** the user ships a
   work item, **Then** SDD-only ship readiness still succeeds and does not ask
   Governance to evaluate evidence freshness, routes, profiles, gates, audit,
   release policy, or protected-boundary enforcement.

---

### User Story 2 - Block Merge-Boundary Readiness on Lifecycle Gaps (Priority: P1)

As a contributor preparing work for merge, I need ship readiness to identify the
exact lifecycle stage, verification finding, evidence disposition, or generated
view that is not ready so that I can fix the right lifecycle source before the
work reaches a protected boundary.

**Why this priority**: Ship readiness is the SDD-owned merge-boundary check
after verification. If a missing or stale prerequisite, an unresolved blocking
verification finding, stale evidence, or a stale generated view is not reported
precisely, a work item could be presented as merge-ready when its lifecycle is
incomplete.

**Independent Test**: Can be tested by running the ship command against work
items with known readiness defects and verifying that no generated ship view is
treated as ready until the report identifies the affected artifact, identifier,
severity, and correction.

**Acceptance Scenarios**:

1. **Given** a prerequisite lifecycle artifact or generated view is missing,
   malformed, stale, or blocked, or the selected work item is not
   verification-ready, **When** ship runs, **Then** the report identifies the
   affected stage or source artifact and blocks ship readiness.
2. **Given** the verification view reports unresolved blocking findings, missing
   required tests, missing required skills, stale evidence, missing evidence,
   undisclosed synthetic evidence, or invalid deferrals, **When** ship
   aggregates readiness, **Then** the report surfaces the underlying blocking
   findings and blocks ship readiness without re-deriving Governance-owned
   enforcement.
3. **Given** a generated work model, analysis view, verification view, or ship
   view is missing, stale, malformed, or blocked by invalid source data,
   **When** the command evaluates generated-view currency, **Then** the report
   records a generated-view diagnostic instead of treating the existing
   generated file as ready.

---

### User Story 3 - Preserve Authored Lifecycle Sources (Priority: P2)

As a user or agent, I need ship readiness to be a non-destructive
merge-boundary check so that diagnostics and generated views never silently
rewrite authored specifications, plans, tasks, evidence declarations, or
verification intent.

**Why this priority**: Ship readiness must be safe to run repeatedly during
authoring, review, and CI. It should aggregate authored lifecycle intent and
refresh generated readiness facts, not change the user's lifecycle claims.

**Independent Test**: Can be tested by running ship with ready, blocked, and
dry-run scenarios and confirming that authored lifecycle artifacts remain
unchanged while generated ship output and reports reflect the current source
state.

**Acceptance Scenarios**:

1. **Given** authored lifecycle artifacts already exist, **When** the user runs
   the ship command, **Then** the command does not create, update, reorder,
   normalize, or remove authored source artifacts.
2. **Given** a generated ship view can be safely refreshed, **When** the command
   runs normally, **Then** only generated readiness artifacts are created or
   updated and the report records the generated artifact operation.
3. **Given** the user requests a dry run, **When** ship completes, **Then**
   proposed generated changes, diagnostics, readiness state, and next action are
   reported without modifying authored or generated artifacts.

---

### User Story 4 - Keep Ship Output Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need ship
outputs to be deterministic and traceable so that humans, agents, and downstream
protected-boundary tooling all read the same merge-boundary readiness facts.

**Why this priority**: Ship readiness is the SDD-owned handoff to optional
Governance protected-boundary enforcement and to CI. The SDD ship contract must
be stable before those consumers can rely on it.

**Independent Test**: Can be tested by running the same ship request against the
same project state multiple times and confirming that generated ship data and
machine-readable reports are stable, plain text summaries contain no extra
facts, and optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and ship input, **When** the ship command
   is run repeatedly, **Then** generated ship data and machine-readable reports
   are identical for each run.
2. **Given** a user requests a human-readable summary, **When** the ship command
   completes, **Then** the summary reflects the same readiness state, blocker
   counts, warning counts, evidence dispositions, generated-view state,
   diagnostics, outcome, and next action as the authoritative command report.
3. **Given** optional Governance policy, capability, tooling, freshness, route,
   profile, gate, audit, or enforcement pointers are present in SDD-owned
   sources, **When** ship completes, **Then** the report may expose those
   pointers as compatibility facts but does not interpret or enforce
   Governance-owned protected-boundary decisions.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The command is run for a work item that has not completed the verify stage,
  or whose verification view reports unresolved blocking findings.
- The selected work id is empty, malformed, duplicated, or inconsistent with an
  existing specification, clarification, checklist, plan, tasks, analysis,
  evidence, or verification artifact.
- The specification, clarification, checklist, plan, tasks, analysis, evidence,
  or verification artifact is missing, malformed, stale, or references unknown
  lifecycle facts.
- The verification view is missing, malformed, stale, schema-incompatible, or
  reports a not-verification-ready outcome.
- A prerequisite generated view (work model, analysis, or verification) is
  missing, stale, malformed, or blocked by invalid source data.
- Evidence or verification findings change after the verification view captured
  a source snapshot, leaving ship readiness stale.
- An accepted deferral that was visible at verification is no longer visible or
  is no longer accepted at ship time.
- Required project settings or artifact layout settings exist but are malformed,
  stale, or point to missing lifecycle roots.
- A generated ship view exists but its source digests, schema version, or
  generator identity no longer match the current authored or generated sources.
- The user requests a dry run and expects proposed ship changes and diagnostics
  without modifying authored or generated artifacts.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for SDD-owned ship readiness and compatibility
  facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd ship` as the next native SDD
  lifecycle command after `fsgg-sdd verify`.
- **FR-002**: The ship command MUST require an initialized SDD project, one
  valid selected work id, valid prerequisite lifecycle artifacts, and a
  verification-ready work-item state before it reports merge-boundary readiness.
- **FR-003**: The ship command MUST load and validate the selected work item's
  specification, clarification, checklist, plan, tasks, analysis, evidence, and
  verification facts, including work identity, lifecycle stage readiness,
  verification readiness, blocking finding state, evidence dispositions, accepted
  deferrals, source snapshots, and source currency.
- **FR-004**: The ship command MUST generate or refresh the selected work item's
  `readiness/<id>/ship.json` view when valid source data exists, ship output is
  needed, and the run is not a dry run.
- **FR-005**: The ship-readiness view MUST capture work identity, source artifact
  relationships, source digests, schema versions, generator identity, aggregated
  lifecycle stage readiness, verification readiness, evidence dispositions,
  generated-view currency, blocking findings, warnings, SDD-owned ship-readiness
  outcome, optional boundary facts, and diagnostics.
- **FR-006**: The ship contract MUST expose stable identifiers for ship-readiness
  findings, with structured links to the affected lifecycle stage, verification
  finding, evidence disposition, generated view, accepted deferral, or source
  artifact when known.
- **FR-007**: The ship command MUST aggregate prerequisite lifecycle readiness
  from the normalized work model, analysis view, and verification view rather
  than re-deriving task, evidence, test, or skill dispositions that the verify
  stage owns.
- **FR-008**: The ship command MUST map the selected work item to a current
  ship-readiness disposition of ship-ready, blocked, stale, or advisory based on
  aggregated lifecycle, verification, evidence, and generated-view state.
- **FR-009**: The ship command MUST NOT create, update, reorder, normalize, or
  remove authored lifecycle artifacts.
- **FR-010**: The ship command MUST mark ship readiness as stale or blocked when
  source facts have changed since the prerequisite generated views, verification
  view, task snapshots, analysis findings, or evidence declarations were
  recorded.
- **FR-011**: The ship command MUST block merge-boundary readiness for
  selected-id mismatches, duplicated logical work ids, malformed prerequisite
  artifacts, a missing or not-ready verification view, unresolved blocking
  verification findings, stale or missing evidence, undisclosed synthetic
  evidence, invalid deferrals, unknown references, malformed generated views, and
  unsafe generated-view refresh conditions.
- **FR-012**: The ship command MUST record the selected work item's lifecycle
  state as ship-ready only when all blocking SDD-owned lifecycle, verification,
  evidence, and generated-view findings are resolved or have accepted deferrals
  that remain visible to later lifecycle and protected-boundary stages.
- **FR-013**: The ship command MUST identify the protected-boundary handoff as
  the next lifecycle action after a successful ship-ready result, while
  explicitly leaving protected-boundary enforcement to Governance.
- **FR-014**: The ship command MUST identify verification rerun, evidence
  correction, prerequisite lifecycle correction, generated-view refresh, or
  stale-source correction as the next action when blocking ship-readiness defects
  remain.
- **FR-015**: The ship command MUST report changed generated artifacts,
  preserved authored artifacts, refused generated artifacts, parsed source facts,
  aggregated lifecycle stage readiness, blocking finding counts, warning counts,
  evidence disposition counts, generated-view state, diagnostics, outcome, and
  next action in the authoritative command report.
- **FR-016**: Machine-readable ship reports and generated ship views MUST be
  deterministic for identical project state and identical ship input.
- **FR-017**: Human-readable ship summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate lifecycle facts.
- **FR-018**: The ship command MUST refresh the selected work item's generated
  work-model view and MUST explicitly diagnose the currency of the analysis and
  verification views (which it never regenerates) when ship readiness depends on
  current normalized lifecycle state.
- **FR-019**: Generated-view diagnostics MUST distinguish missing, stale,
  malformed, and blocked states and MUST name the source artifact that needs
  correction when available.
- **FR-020**: Ship diagnostics MUST use stable identifiers and include the
  affected artifact, severity, explanation, and user-correctable action.
- **FR-021**: Dry-run ship requests MUST report proposed generated artifact
  changes, diagnostics, readiness state, and next action without modifying
  authored or generated artifacts.
- **FR-022**: The ship command MUST work when Governance is not installed or
  configured.
- **FR-023**: Optional Governance policy, capability, tooling, freshness,
  routing, profile, gate, audit, enforcement, and release facts MUST remain
  advisory compatibility facts in SDD ship reports and MUST NOT be interpreted as
  SDD-owned enforcement decisions.
- **FR-024**: The feature MUST NOT introduce Governance effective-evidence
  freshness, route selection, profile adjustment, gate selection,
  protected-boundary enforcement, audit verdicts, release gating, or generated
  agent guidance behavior.

### Key Entities

- **Verification-Ready Work Item**: The selected lifecycle unit that has a stable
  work id, valid prerequisite artifacts, current analysis and verification state,
  authored evidence declarations, and a next expected lifecycle action of ship.
- **Aggregated Lifecycle Readiness**: The combined readiness of each prerequisite
  lifecycle stage (charter, specify, clarify, checklist, plan, tasks, analysis,
  evidence, verify) as reflected in the normalized work model and generated
  views, summarized for the merge boundary.
- **Ship-Readiness Disposition**: The current state of the selected work item at
  the merge boundary, such as ship-ready, blocked, stale, or advisory.
- **Ship-Readiness View**: The generated `readiness/<id>/ship.json` artifact that
  records SDD-owned merge-boundary readiness, source currency, generated-view
  state, aggregated lifecycle and verification readiness, blockers, warnings, and
  diagnostics for the selected work item.
- **Ship-Readiness Finding**: A stable user-correctable readiness result derived
  from aggregated lifecycle stages, the verification view, evidence dispositions,
  generated-view state, or optional boundary facts.
- **Ship Command Report**: The authoritative result of a ship command, including
  context, generated artifact changes, source summaries, aggregated readiness,
  blockers, warnings, generated-view state, diagnostics, outcome, and next
  action.
- **Ship Diagnostic**: A stable finding that explains invalid project context,
  missing prerequisite state, a not-ready verification view, malformed lifecycle
  sources, stale generated views, ship-readiness defects, or optional boundary
  issues.
- **Optional Boundary Fact**: An advisory SDD report fact that exposes
  Governance-compatible context without evaluating freshness, routing, profiles,
  gates, audit, enforcement, or release policy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a verification-ready work item,
  a user can generate SDD merge-boundary readiness and receive the next lifecycle
  action in one command result.
- **SC-002**: 100% of valid ship fixture families (`ship-create`,
  `ship-rerun-current`, `ship-preserves-authored`, `ship-refreshes-work-model`,
  `ship-refreshes-verification`, `ship-accepted-deferral`, `dry-run`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected ship view or proposed dry-run
  changes, selected work-id trace, successful outcome, and correct next action.
- **SC-003**: 100% of blocked ship fixture families (`outside-project`,
  `missing-specification`, `missing-clarification`, `missing-checklist`,
  `missing-plan`, `missing-tasks`, `missing-analysis`, `missing-evidence`,
  `missing-verification`, `failed-verification`, `not-verification-ready`,
  `malformed-work-id`, `malformed-ship-view`, `duplicate-work-id`,
  `unknown-source-reference`, `stale-analysis`, `stale-evidence`,
  `stale-verification`, `undisclosed-synthetic-evidence`, `invalid-deferral`,
  and `stale-generated-view`) leave authored lifecycle content unchanged and
  include at least one actionable diagnostic.
- **SC-004**: Three repeated ship runs over identical inputs produce identical
  generated ship views and machine-readable command reports.
- **SC-005**: 100% of missing verification, not-ready verification, stale
  evidence, missing evidence, unknown reference, undisclosed synthetic evidence,
  invalid deferral, malformed generated-view, and stale generated-view scenarios
  identify the affected artifact or identifier before blocking merge-boundary
  readiness.
- **SC-006**: Dry-run ship requests change 0 authored or generated files while
  still reporting proposed generated artifacts, diagnostics, readiness state, and
  next action.
- **SC-007**: Maintainers can identify the generated ship artifact, ready-finding
  count, advisory count, warning count, blocking count, evidence disposition
  counts, generated-view state, and next action from the human-readable summary
  during review.
- **SC-008**: Ship readiness remains usable without Governance installed in every
  no-Governance validation scenario.

## Assumptions

- The next applicable SDD-owned item from `docs/initial-implementation-plan.md`
  is `fsgg-sdd ship`, which follows the implemented `012-verify-command` slice
  and covers the remaining SDD-owned Phase 6 merge-boundary readiness work.
- `fsgg-sdd init`, `fsgg-sdd charter`, `fsgg-sdd specify`, `fsgg-sdd clarify`,
  `fsgg-sdd checklist`, `fsgg-sdd plan`, `fsgg-sdd tasks`, `fsgg-sdd analyze`,
  `fsgg-sdd evidence`, and `fsgg-sdd verify` already create the minimum SDD
  project and work-item state used by this feature.
- The ship command operates on one selected work item at a time and writes the
  generated ship view under the configured readiness root.
- The specification, clarification, checklist, plan, tasks, analysis, evidence,
  and verification artifacts remain the authored or generated prerequisite
  sources of truth; the ship view is a generated view over those sources and is
  not an authored source of lifecycle intent.
- Ship readiness aggregates the verification view rather than re-running the
  task, evidence, test, and skill checks that the verify stage owns.
- A successful ship-ready result points to the protected-boundary handoff as the
  next lifecycle action, but enforcing the protected boundary is outside this
  feature and remains Governance-owned.
- Effective evidence freshness, route selection, profile adjustment, gate
  selection, protected-boundary enforcement, audit verdicts, and release policy
  remain Governance-owned concerns.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Optional Governance files may be referenced for compatibility, but SDD remains
  independently usable without Governance installed.
