# Feature Specification: Generated-View Refresh

**Feature Branch**: `015-refresh-command`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: native SDD command surface, the
`readiness/<id>/summary.md` generated view, cross-view refresh of the SDD-owned
generated views derived from declared sources, generated-view currency
behavior, stale-view diagnostics, command report, deterministic JSON and text
projection, and optional Governance boundary facts)

**Input**: User description: "Start the next item on the implementation plan.
With the `charter -> ship` lifecycle and Phase 8 agent guidance complete, the
next SDD-owned item is Phase 7 of `docs/initial-implementation-plan.md`: add an
SDD refresh path for the lifecycle generated views (`work-model.json`,
`analysis.json`, `verify.json`, `ship.json`, `summary.md`, and
`agent-commands/`), emit stale-view diagnostics when generated views are older
than their declared sources, and produce the human-readable
`readiness/<id>/summary.md` rendered from structured readiness data, while
keeping authored sources authoritative and Governance-owned boundary
enforcement out of scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Refresh a Work Item's Generated Views From Declared Sources (Priority: P1)

As a project maintainer or coding agent, I need to refresh the selected work
item's SDD-owned generated views from its current declared sources in one
command so that the normalized work model, analysis, verify, ship, and agent
guidance all reflect the latest authored lifecycle artifacts without re-authoring
any of them. (The human-readable `summary.md` projection over those views is the
subject of User Story 3 and is refreshed in the same run once that story lands.)

**Why this priority**: The lifecycle authoring commands (`charter` through
`ship`) and the agent-guidance generator each produce a generated view, but a
user who edits authored sources has no single, deterministic way to bring every
SDD-owned generated view back into currency. Without this slice, generated views
drift one at a time, and consumers, agents, and CI cannot trust that the
readiness artifacts as a set match the current sources.

**Independent Test**: Can be tested by running the refresh command in an
initialized SDD project with a valid work item and confirming that every
refreshable SDD-owned generated view is regenerated from the current declared
sources, that each regenerated view records its source relationships and
generator identity, and that the command reports the refreshed views, the
generated-view state, diagnostics, outcome, and next action without requiring
Governance.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and a work item whose authored sources
   are valid, **When** the user runs the refresh command, **Then** the SDD-owned
   generated views for that work item are regenerated from the current declared
   sources, each regenerated view records its source paths, source digests,
   schema versions, and generator identity, and the command report names the
   selected work id, the refreshed views, the generated-view state, diagnostics,
   outcome, and next action.
2. **Given** a work item where some declared sources have changed since the
   generated views were last produced, **When** the user runs the refresh
   command, **Then** the command refreshes the affected views from the changed
   sources and reports which views were refreshed and which were already current.
3. **Given** optional Governance files are absent, **When** the user refreshes
   generated views, **Then** SDD-only refresh still succeeds and does not ask
   Governance to evaluate evidence freshness, routes, profiles, gates, audit,
   release policy, or protected-boundary enforcement.

---

### User Story 2 - Detect Stale and Unrefreshable Generated Views (Priority: P1)

As a maintainer or agent, I need stale generated views and views that cannot be
refreshed to be reported precisely so that I can correct the right source before
a human, agent, or CI consumer trusts a readiness artifact that no longer matches
its declared sources.

**Why this priority**: Generated views are outputs; their presence is not proof
of currency. If a view's recorded source digests, schema version, or generator
identity no longer match the current sources, or if a view cannot be refreshed
because its upstream source is missing, malformed, or blocked, that must be a
visible finding rather than a silent pass. Detecting staleness is the core value
of a refresh command.

**Independent Test**: Can be tested by running the command against work items
whose generated views are missing, stale, malformed, or blocked by invalid
upstream sources and verifying that no view is reported as current until the
report identifies the affected view, source, severity, and correction.

**Acceptance Scenarios**:

1. **Given** a generated view whose recorded source digests, schema version, or
   generator identity no longer match the current declared sources, **When** the
   command evaluates currency, **Then** the report records a stale-view
   diagnostic naming the affected view and source instead of treating the
   existing file as current.
2. **Given** a generated view whose upstream source is missing, malformed, or
   blocked by invalid data, **When** the command runs, **Then** the report
   records a blocked or missing generated-view diagnostic for that view, refreshes
   the views that can be refreshed, and does not fabricate a view from an unusable
   source.
3. **Given** a generated view that depends on another generated view that could
   not be brought to currency, **When** the command runs, **Then** the dependent
   view is reported as blocked and the report names the upstream view that must be
   corrected first.

---

### User Story 3 - Produce a Human-Readable Readiness Summary (Priority: P2)

As a maintainer, reviewer, or agent, I need a human-readable
`readiness/<id>/summary.md` rendered from the structured readiness data so that a
person can review the work item's lifecycle state, generated-view currency, and
outstanding diagnostics without reading raw JSON, while the summary stays a
projection that never introduces facts the structured artifacts do not contain.

**Why this priority**: The structured readiness views are the machine contract,
but reviewers and agents need a single readable surface that reflects them. The
summary must be generated from the same structured data so it cannot become a
competing source of truth, and it must be refreshed alongside the other views so
it never describes stale state.

**Independent Test**: Can be tested by running the command and confirming that
`readiness/<id>/summary.md` is generated as a projection of the structured
readiness data, is marked as generated with its source relationships, and
contains the same lifecycle state, generated-view currency, diagnostics, outcome,
and next action as the authoritative command report and the structured views it
summarizes.

**Acceptance Scenarios**:

1. **Given** a work item with current structured readiness views, **When** the
   user runs the refresh command, **Then** `readiness/<id>/summary.md` is
   generated or refreshed as a projection of the structured readiness data, is
   marked as generated, and records the sources it was rendered from.
2. **Given** the structured readiness views report outstanding diagnostics or
   stale upstream views, **When** the summary is generated, **Then** the summary
   reflects the same diagnostics, generated-view state, outcome, and next action
   as the authoritative command report and adds no facts that are absent from the
   structured views.
3. **Given** the structured readiness data needed to render the summary is
   missing, stale, or blocked, **When** the command runs, **Then** the report
   records a generated-view diagnostic for the summary and does not render a
   summary from unusable data.

---

### User Story 4 - Keep Authored Sources Authoritative and Refresh Safe to Repeat (Priority: P2)

As a user or agent, I need generated-view refresh to be a non-destructive,
repeatable generator so that running it during authoring, review, or CI only
writes generated views and never rewrites, reorders, or removes authored
lifecycle artifacts, and so a dry run can preview the refresh without changing
anything.

**Why this priority**: Per the constitution, generated views are outputs and
authored artifacts are authoritative. Refresh must be safe to run repeatedly and
must make it possible to preview what it would regenerate before it writes, so it
can run in read-only review and CI contexts.

**Independent Test**: Can be tested by running the command in current, stale, and
dry-run scenarios and confirming that authored lifecycle artifacts remain
byte-unchanged while generated views and reports reflect the current source
state, and that a dry run changes no files.

**Acceptance Scenarios**:

1. **Given** authored lifecycle artifacts already exist, **When** the user runs
   the refresh command, **Then** the command does not create, update, reorder,
   normalize, or remove any authored source artifact.
2. **Given** generated views can be safely refreshed, **When** the command runs
   normally, **Then** only generated views under the configured generated roots
   are created or updated, each is marked as generated with source digests, and
   the report records each generated-view operation.
3. **Given** the user requests a dry run, **When** the command completes, **Then**
   the proposed generated-view changes, diagnostics, generated-view state, and
   next action are reported without modifying any authored or generated artifact.

---

### User Story 5 - Keep Refreshed Views Deterministic and Traceable (Priority: P3)

As a CI operator, maintainer, or optional Governance integrator, I need refreshed
generated views and the command report to be deterministic and traceable so that
humans, agents, CI, and downstream tooling read the same readiness state with
explicit provenance.

**Why this priority**: Refresh is the surface that keeps the full set of
generated views in agreement with declared sources. Its outputs must be stable
and self-describing before consumers rely on a single refresh to certify
currency.

**Independent Test**: Can be tested by running the same refresh request against
the same project state multiple times and confirming that the regenerated views
and the machine-readable report are identical for each run, that each refreshed
view identifies its sources and generator identity, that the text projection adds
no facts, and that optional Governance references remain advisory.

**Acceptance Scenarios**:

1. **Given** identical project state and identical refresh input, **When** the
   command is run repeatedly, **Then** the regenerated views and machine-readable
   report are identical for each run.
2. **Given** a user requests a human-readable summary of the command result,
   **When** the command completes, **Then** the summary reflects the same
   refreshed views, generated-view state, diagnostics, outcome, and next action as
   the authoritative command report.
3. **Given** optional Governance policy, capability, tooling, route, profile,
   gate, audit, or enforcement pointers are present in SDD-owned sources, **When**
   the command completes, **Then** the report may expose those pointers as
   compatibility facts but does not interpret or enforce Governance-owned
   decisions, including stale-view blocking at a protected boundary.

### Edge Cases

- The command is run before `fsgg-sdd init` has created the SDD project
  skeleton.
- The selected work id is empty, malformed, duplicated, or inconsistent with the
  normalized work model.
- One or more declared authored sources for the work item are missing, malformed,
  stale, or schema-incompatible, so some views cannot be refreshed.
- A generated view exists but its recorded source digests, schema version, or
  generator identity no longer match the current declared sources.
- A generated view is missing entirely and must be produced for the first time.
- A generated view is malformed or unreadable and must be regenerated rather than
  trusted.
- A generated view depends on another generated view that could not be brought to
  currency, so the dependent view is blocked.
- The structured readiness data required to render `summary.md` is missing,
  stale, or blocked.
- A work item has no agent-guidance configuration or no configured agent targets,
  so the agent-commands view is not applicable to refresh.
- The user requests a dry run and expects proposed refresh changes and
  diagnostics without modifying authored or generated artifacts.
- Lifecycle sources change after a view captured its source snapshot, leaving the
  view stale until the next refresh.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must remain responsible only for SDD-owned generated-view refresh and
  compatibility facts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide `fsgg-sdd refresh` as a native SDD command
  that refreshes the SDD-owned generated views for a selected work item from the
  work item's declared sources, without introducing a new lifecycle authoring
  stage between any existing stages.
- **FR-002**: The command MUST require an initialized SDD project and one valid
  selected work id before it refreshes any generated view.
- **FR-003**: The command MUST refresh the SDD-owned generated views derived from
  the work item's declared sources, covering at least the normalized work model,
  analysis, verify, ship, agent guidance, and human-readable summary views, using
  the same deterministic generators that originally produce them.
- **FR-004**: The command MUST regenerate each refreshable generated view from
  its current declared sources rather than from a prior generated view's cached
  content, except where one generated view is the declared source of another.
- **FR-005**: The command MUST produce or refresh `readiness/<id>/summary.md` as a
  human-readable projection rendered from the structured readiness data, marked as
  generated, recording the sources it was rendered from.
- **FR-006**: The human-readable summary MUST NOT introduce lifecycle, currency,
  diagnostic, outcome, or next-action facts that are absent from the structured
  readiness views it projects.
- **FR-007**: Each refreshed generated view MUST be marked as generated and MUST
  record its source relationships, source digests, schema versions, and generator
  identity so it can be traced back to declared sources.
- **FR-008**: The command MUST evaluate the currency of each SDD-owned generated
  view and MUST report a stale-view diagnostic when a view's recorded source
  digests, schema version, or generator identity no longer match the current
  declared sources.
- **FR-009**: The command MUST distinguish current, missing, stale, malformed, and
  blocked generated-view states and MUST name the affected view and the source or
  upstream view that needs correction when available.
- **FR-010**: The command MUST refresh the views that can be refreshed even when
  other views are blocked, and MUST NOT fabricate or refresh a view from a
  missing, malformed, or blocked source.
- **FR-011**: When a generated view depends on another generated view that could
  not be brought to currency, the command MUST report the dependent view as
  blocked and name the upstream view to correct first.
- **FR-012**: The command MUST NOT create, update, reorder, normalize, or remove
  authored lifecycle artifacts or authored configuration.
- **FR-013**: Refreshed generated views MUST NOT become a second source of truth;
  authored lifecycle artifacts and the normalized work model remain authoritative
  and the refreshed views are projections over them.
- **FR-014**: The command MUST map the selected work item's refresh result to a
  current disposition of refreshed-current, partially-blocked, or blocked based on
  project context validity, source validity, and per-view generated-view state.
- **FR-015**: The command MUST block refresh of a view for invalid project
  context, an empty or malformed selected id, a selected-id mismatch, duplicated
  logical work ids, missing or malformed declared sources, unknown references,
  malformed existing generated views, and unsafe refresh conditions.
- **FR-016**: The command MUST identify continuing the lifecycle or relying on the
  refreshed readiness as the next action after a refreshed-current result, and
  MUST identify source correction, upstream-view correction, or re-running the
  responsible lifecycle command as the next action when blocking findings remain.
- **FR-017**: The command MUST report refreshed views, already-current views,
  blocked views, preserved authored artifacts, the per-view generated-view state,
  diagnostics, outcome, and next action in the authoritative command report.
- **FR-018**: Machine-readable refresh reports and regenerated generated views
  MUST be deterministic for identical project state and identical refresh input.
- **FR-019**: Human-readable refresh summaries MUST be projections of the same
  authoritative command report and MUST NOT introduce separate refresh facts.
- **FR-020**: Generated-view diagnostics MUST use stable identifiers and include
  the affected view, the affected source or upstream view when available,
  severity, explanation, and a user-correctable action.
- **FR-021**: Dry-run refresh requests MUST report proposed generated-view
  changes, diagnostics, generated-view state, and next action without modifying
  any authored or generated artifact.
- **FR-022**: The command MUST work when Governance is not installed or
  configured.
- **FR-023**: Optional Governance policy, capability, tooling, routing, profile,
  gate, audit, enforcement, and release facts MUST remain advisory compatibility
  facts in refresh reports and MUST NOT be interpreted as SDD-owned enforcement
  decisions, including stale-view blocking at a protected boundary.
- **FR-024**: The feature MUST NOT introduce Governance effective-evidence
  freshness, route selection, profile adjustment, gate selection,
  protected-boundary enforcement, audit verdicts, or release gating behavior.

### Key Entities

- **Refresh Request**: The selected work id and run options (including dry run)
  that ask the command to bring the work item's SDD-owned generated views to
  currency.
- **Generated View**: An SDD-owned readiness output for the work item, such as the
  normalized work model, analysis, verify, ship, agent-commands, or summary view,
  marked as generated and carrying source relationships, source digests, schema
  versions, and generator identity.
- **Generated-View State**: The current disposition of a single generated view,
  such as current, missing, stale, malformed, or blocked.
- **Refresh Disposition**: The overall state of the work item's refresh, such as
  refreshed-current, partially-blocked, or blocked.
- **Readiness Summary**: The generated `readiness/<id>/summary.md` projection
  rendered from the structured readiness data, marked as generated, that presents
  lifecycle state, generated-view currency, diagnostics, outcome, and next action.
- **Refresh Report**: The authoritative result of a refresh command, including
  context, refreshed views, already-current views, blocked views, preserved
  authored artifacts, per-view generated-view state, diagnostics, outcome, and
  next action.
- **Generated-View Diagnostic**: A stable finding that explains invalid project
  context, a missing or malformed source, a missing, stale, malformed, or blocked
  generated view, an unrefreshable dependent view, or an optional boundary issue.
- **Optional Boundary Fact**: An advisory SDD report fact that exposes
  Governance-compatible context without evaluating freshness, routing, profiles,
  gates, audit, enforcement, release policy, or stale-view blocking.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an initialized SDD project with a valid work item and valid
  sources, a user can refresh all SDD-owned generated views for that work item and
  receive the per-view generated-view state and next action in one command result.
- **SC-002**: 100% of valid refresh fixture families (`refresh-current`,
  `refresh-stale-views`, `refresh-missing-view`, `refresh-summary`,
  `refresh-preserves-authored`, `refresh-no-agent-targets`, `dry-run`,
  `deterministic-report`, `text-projection`, and no-Governance
  `governance-boundary`) produce the expected refreshed views or proposed dry-run
  changes, selected work-id trace, per-view generated-view state, successful or
  partially-blocked outcome, and correct next action.
- **SC-003**: 100% of blocked refresh fixture families (`outside-project`,
  `malformed-work-id`, `duplicate-work-id`, `missing-source`, `malformed-source`,
  `stale-source`, `unknown-source-reference`, `malformed-generated-view`, and
  `blocked-upstream-view`) leave authored content unchanged and include at least
  one actionable diagnostic.
- **SC-004**: Three repeated refresh runs over identical inputs produce identical
  regenerated views and machine-readable command reports.
- **SC-005**: 100% of stale-view, missing-view, malformed-view, missing-source,
  malformed-source, and blocked-upstream scenarios identify the affected view and
  the affected source or upstream view before reporting any view as current.
- **SC-006**: Dry-run refresh requests change 0 authored or generated files while
  still reporting proposed generated-view changes, diagnostics, generated-view
  state, and next action.
- **SC-007**: Maintainers can identify the refreshed views, already-current views,
  blocked views, per-view generated-view state, and next action from the
  human-readable summary during review.
- **SC-008**: `readiness/<id>/summary.md` is generated from the structured
  readiness data, is marked as generated, identifies its sources, and contains no
  lifecycle, currency, diagnostic, outcome, or next-action fact that is absent
  from the structured readiness views.
- **SC-009**: Generated-view refresh remains usable without Governance installed
  in every no-Governance validation scenario.

## Assumptions

- With the `charter -> ship` lifecycle and Phase 8 agent guidance complete, the
  next SDD-owned item in `docs/initial-implementation-plan.md` is the Phase 7 SDD
  refresh path for lifecycle generated views.
- The artifact-model library already defines the generation manifest shape
  (source, generated view, renderer, generator version, source digest, output
  digest, and currency gate) and the per-view generators that the lifecycle
  commands and the agent-guidance command already use; this feature adds the
  command that refreshes those views together and produces the human-readable
  summary, rather than introducing new generated-view contracts.
- The command operates on one selected work item at a time and writes generated
  views under each view's configured generated root.
- `readiness/<id>/summary.md` is the SDD-produced human-readable projection of the
  structured readiness data and is the responsibility of this command on the SDD
  side; Governance may render its own gate/route/audit summaries separately.
- Refresh regenerates SDD-owned generated views from current declared authored
  sources and does not re-author specifications, plans, tasks, or evidence; where
  one generated view (such as the normalized work model) is the declared source of
  another (such as agent guidance), refresh brings the upstream view to currency
  first.
- The agent-commands view is refreshed only when an agent-guidance configuration
  and at least one configured target exist; otherwise it is reported as not
  applicable rather than blocked.
- Stale generated views are reported by SDD as findings; blocking stale views at a
  protected boundary remains a Governance-owned enforcement concern.
- Generated lifecycle views are outputs; their presence alone is not proof of
  currency.
- Effective evidence freshness, route selection, profile adjustment, gate
  selection, protected-boundary enforcement, audit verdicts, and release policy
  remain Governance-owned concerns.
- Optional Governance files may be referenced for compatibility, but SDD remains
  independently usable without Governance installed.
