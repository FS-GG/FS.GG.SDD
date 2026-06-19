# Feature Specification: Native SDD Lifecycle Commands

**Feature Branch**: `003-native-sdd-lifecycle-commands`

**Created**: 2026-06-19

**Status**: Ready for Planning

**Change Tier**: Tier 1 (contracted change: native SDD command surface,
lifecycle authoring behavior, deterministic command reports, generated-view
currency behavior, diagnostics, and optional Governance boundary contracts)

**Input**: User description: "Create the next actionable SDD feature spec from
`docs/initial-implementation-plan.md`. Phase 2 is Governance-owned, Phase 3 is
complete, and the next SDD-owned roadmap item is Phase 4: Native SDD Lifecycle
Commands."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start An SDD Project Locally (Priority: P1)

As a project maintainer, I need to initialize an SDD-governed project skeleton
without hidden FS.GG repository knowledge so that a new product can begin the
native SDD lifecycle before any product source code exists.

**Why this priority**: Project initialization is the entry point for every
later lifecycle command. Without it, users cannot reliably create the authored
sources, work root, readiness root, and agent guidance targets that commands
depend on.

**Independent Test**: Can be tested by initializing an empty target directory
and confirming that the required SDD-owned project files, work root, readiness
root, and command report are created without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an empty target directory, **When** a user initializes it as an SDD
   project, **Then** the project has SDD-owned project configuration, a work
   root, an initial readiness area, agent guidance targets, and a command report
   that names every created artifact.
2. **Given** a target directory that already contains unrelated user files,
   **When** initialization runs, **Then** user files are preserved and any
   conflicting lifecycle paths are reported before they are overwritten.
3. **Given** Governance is not installed, **When** initialization runs, **Then**
   the SDD skeleton is still usable and optional Governance files are not
   required.

---

### User Story 2 - Author Work Through The Lifecycle (Priority: P1)

As a contributor or coding agent, I need native SDD commands for charter,
specify, clarify, checklist, plan, tasks, and analyze so that work can advance
from intent to implementation-ready tasks through one lifecycle contract.

**Why this priority**: The product value of FS.GG.SDD is the ability to guide
work through the SDD lifecycle while keeping Markdown authoring, structured
artifacts, generated views, and diagnostics aligned.

**Independent Test**: Can be tested by creating one work item and advancing it
from charter through analysis, verifying after each stage that the expected
authored artifact, structured lifecycle data, generated-view status, and
command diagnostics are present.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project, **When** a user creates or updates a
   charter, **Then** the project records lifecycle principles, scope
   boundaries, and policy pointers in the SDD-owned artifact contract.
2. **Given** an initialized SDD project, **When** a user specifies and clarifies
   a work item, **Then** user value, scope, non-goals, requirements,
   acceptance scenarios, ambiguity records, and clarification answers are
   linked to the same work id.
3. **Given** a specified work item, **When** a user runs checklist, plan, and
   tasks commands, **Then** requirements quality, technical planning decisions,
   task graph entries, dependencies, required skills, and required evidence
   expectations are recorded as lifecycle artifacts.
4. **Given** a work item has spec, clarification, checklist, plan, and task
   artifacts, **When** analysis runs, **Then** cross-artifact consistency
   diagnostics explain whether the work is ready for implementation planning or
   which artifact the user must fix.

---

### User Story 3 - Trust Command Reports And Generated-View Currency (Priority: P2)

As a CI operator, contributor, or agent, I need every lifecycle command to
return stable machine-readable results and human-readable summaries from the
same facts so that automation and humans do not disagree.

**Why this priority**: Native commands become public workflow contracts. Their
outputs must be deterministic, explainable, and safe to consume before later
verify, ship, refresh, and agent-guidance features build on them.

**Independent Test**: Can be tested by running each command against identical
fixtures three times, comparing command reports for byte-stable content, and
confirming that plain text summaries project the same result without becoming
the automation contract.

**Acceptance Scenarios**:

1. **Given** identical lifecycle inputs, **When** any native SDD command is run
   repeatedly with machine-readable output requested, **Then** the report is
   byte-stable and does not depend on implicit clocks, terminal width, ANSI
   output, or directory enumeration order.
2. **Given** a command updates authored lifecycle sources, **When** sufficient
   valid data exists to refresh generated SDD views, **Then** the command
   reports the refreshed views, source digests, generator version, and current
   currency state.
3. **Given** a command cannot refresh a generated SDD view because sources are
   missing, malformed, or conflicting, **When** the command completes, **Then**
   it reports a stale-view diagnostic instead of treating an existing generated
   file as current.
4. **Given** a user requests plain text output, **When** a command completes,
   **Then** the text summarizes the same command result and diagnostics as the
   machine report without introducing separate lifecycle facts.

---

### User Story 4 - Keep SDD Separate From Governance Enforcement (Priority: P3)

As an SDD maintainer or Governance integrator, I need native SDD commands to
emit optional readiness facts without taking ownership of route selection,
freshness policy, profiles, or protected-boundary enforcement.

**Why this priority**: SDD must remain independently useful while preserving the
boundary that keeps Governance responsible for rule evaluation and gate
decisions.

**Independent Test**: Can be tested by running lifecycle commands in projects
with and without optional Governance configuration and confirming that SDD
reports lifecycle facts and compatibility boundaries without requiring or
executing Governance gate behavior.

**Acceptance Scenarios**:

1. **Given** optional Governance configuration exists beside SDD artifacts,
   **When** native SDD commands run, **Then** SDD reports only lifecycle
   compatibility facts and does not select routes, profiles, freshness states,
   or protected-boundary verdicts.
2. **Given** optional Governance configuration is absent, **When** native SDD
   commands run, **Then** SDD lifecycle authoring, generated-view currency, and
   diagnostics continue to work.
3. **Given** a user needs merge-boundary or release enforcement, **When** they
   review this command feature, **Then** it is clear that evidence freshness,
   verify readiness, ship readiness, and protected-branch verdicts belong to
   later SDD or Governance phases.

### Edge Cases

- Initialization is requested for a non-empty directory with existing lifecycle
  files, unrelated user files, or stale generated views.
- A lifecycle command is run outside an SDD project or against an unknown work
  id.
- A requested work id is malformed, already exists with conflicting metadata,
  or differs between Markdown and structured lifecycle data.
- A command would overwrite user-authored prose, structured data, generated
  views, or agent guidance targets without an explicit safe update path.
- Required prior lifecycle artifacts are missing, malformed, stale, or refer to
  unknown requirements, decisions, tasks, artifacts, skills, or evidence.
- Generated SDD views exist but record stale source digests, incompatible schema
  versions, or an older generator version.
- Plain text output is requested in a terminal with wrapping or formatting
  limitations while the machine-readable report must remain deterministic.
- Optional Governance files are present, absent, malformed, or incomplete; SDD
  must not evaluate route, profile, freshness, or gate semantics.
- Multiple lifecycle commands are run in sequence and the later command must
  report that an earlier artifact is incomplete rather than silently guessing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST define native SDD lifecycle commands for project
  initialization, charter authoring, specification authoring, clarification,
  checklist validation, planning, task generation, and cross-artifact analysis.
- **FR-002**: Native SDD commands MUST operate over the SDD-owned `.fsgg` and
  `work/<id>` lifecycle contract and MUST treat schema-versioned structured
  artifacts as the machine contract while preserving Markdown as an authoring
  surface.
- **FR-003**: Project initialization MUST create the minimum SDD skeleton needed
  for lifecycle authoring: project lifecycle settings, SDD lifecycle policy,
  agent guidance targets, a work root, an initial readiness area, and a command
  report that traces created artifacts.
- **FR-004**: Charter, specify, clarify, checklist, plan, and tasks commands
  MUST create or update the corresponding lifecycle artifacts for one selected
  work item without losing existing user-authored content.
- **FR-005**: Analyze MUST check consistency across SDD-owned project settings,
  work-item metadata, specification, clarifications, checklist results, plan,
  contracts, and task graph artifacts.
- **FR-006**: Each command MUST return a command report that includes command
  identity, selected project or work item, lifecycle stage, changed artifacts,
  generated-view currency, diagnostics, outcome, and next expected lifecycle
  action.
- **FR-007**: Machine-readable command reports MUST be deterministic for
  identical inputs and MUST exclude implicit clocks, terminal formatting, ANSI
  styling, and host-specific ordering from authoritative content.
- **FR-008**: Plain text command output MUST be a projection of the same command
  report used for automation and MUST NOT introduce separate lifecycle facts.
- **FR-009**: Stateful or file-changing command behavior MUST have a testable
  boundary that separates durable lifecycle state, requested user action,
  requested effects, transition result, diagnostics, and final command report.
- **FR-010**: Commands MUST refresh generated SDD views when enough valid source
  data exists and MUST report stale-view diagnostics when generated views are
  missing, stale, blocked by malformed input, or blocked by conflicting source
  facts.
- **FR-011**: Command diagnostics MUST use stable ids, severities, affected
  artifacts, source locations when available, explanations, and expected
  corrections.
- **FR-012**: Commands MUST refuse unsafe overwrites and MUST report which
  authored or generated artifact would be affected before requiring user action.
- **FR-013**: Commands MUST remain usable without the Governance gate runtime
  installed.
- **FR-014**: Commands MAY expose optional Governance compatibility facts, but
  MUST NOT perform route selection, profile adjustment, evidence freshness
  evaluation, protected-boundary gate enforcement, or Governance policy
  interpretation.
- **FR-015**: The feature MUST define how command workflow behavior keeps Claude
  and Codex guidance targets equivalent when those targets are generated or
  refreshed by later features.
- **FR-016**: This feature MUST NOT introduce task/evidence update commands,
  SDD verify or ship readiness commands, full generated-view refresh commands,
  generated agent guidance, product runtime templates, rendering assets, package
  release behavior, or Governance enforcement.

### Key Entities

- **SDD Project**: A repository or directory initialized with SDD lifecycle
  settings, artifact layout, work root, readiness root, and agent guidance
  targets.
- **Lifecycle Command**: A user-facing SDD action that initializes a project or
  advances one work item through charter, specify, clarify, checklist, plan,
  tasks, or analyze stages.
- **Command Report**: The authoritative result of a command, containing
  selected context, changed artifacts, generated-view state, diagnostics,
  outcome, and next action.
- **Command Diagnostic**: A stable finding that explains malformed input,
  missing artifacts, unsafe writes, stale views, consistency failures, or
  optional boundary issues.
- **Command Workflow State**: The durable lifecycle facts a command reads
  before deciding which changes or diagnostics to produce.
- **Requested Effect**: A file, generated-view, or process action a command asks
  to perform after validating lifecycle state and user intent.
- **Work Item**: The selected lifecycle unit being authored or analyzed, with a
  stable id and artifacts under the configured work root.
- **Lifecycle Artifact**: An SDD-owned authored source, structured record, or
  generated view that participates in the lifecycle command contract.
- **Generated SDD View**: A readiness artifact derived from lifecycle sources,
  including work-model and analysis views introduced before or during this
  command phase.
- **Plain Text Projection**: Human-readable command output rendered from a
  command report without becoming a second source of truth.
- **Agent Guidance Target**: A Claude, Codex, or future-agent target named by
  SDD lifecycle settings and kept equivalent by later generation features.
- **Governance Compatibility Boundary**: Optional readiness facts or artifact
  references that Governance may consume without making SDD responsible for
  routing, freshness, profiles, or gate enforcement.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An empty target directory can be initialized into an SDD project
  with 100% of the required SDD-owned project files, work root, readiness root,
  and agent guidance targets present, without requiring Governance.
- **SC-002**: A representative work item can be advanced from charter through
  analysis with every command producing the expected authored artifact,
  generated-view status, diagnostic set, and next-action report.
- **SC-003**: For identical lifecycle inputs, every native SDD command's
  machine-readable report is byte-identical across three consecutive runs.
- **SC-004**: 100% of fixtures for outside-project use, duplicate or malformed
  work ids, missing prerequisites, malformed artifacts, unsafe overwrite
  attempts, unknown references, and stale generated views produce stable
  diagnostics with expected corrections.
- **SC-005**: When lifecycle sources are valid, commands refresh all generated
  SDD views they are responsible for; when sources are invalid, commands report
  the stale or blocked view and name the source artifact that must be fixed.
- **SC-006**: A user can trace any command result from the command report to the
  affected authored artifacts, generated views, source digests, and diagnostics
  in under 10 minutes using the recorded report data.
- **SC-007**: A Governance boundary review finds zero command requirements that
  implement route selection, profile adjustment, evidence freshness evaluation,
  protected-boundary gate enforcement, or Governance policy interpretation.

## Assumptions

- This specification covers Phase 4, Native SDD Lifecycle Commands, from
  `docs/initial-implementation-plan.md`.
- Phase 2 in the roadmap is owned by FS.GG.Governance and remains outside this
  SDD repository; Phase 3, Normalized Work Model, is already complete.
- The existing SDD artifact model and normalized work model are the foundation
  for command contracts and generated-view currency checks.
- The native command work root follows the SDD `.fsgg` plus `work/<id>` source
  model, while this repository may continue using standard Spec Kit to develop
  the product itself.
- The initialization command in this phase creates the minimum lifecycle
  skeleton. Full bootstrap templates, migration guidance, and product-template
  provider behavior are later roadmap work.
- Task/evidence update, verify readiness, ship readiness, full refresh,
  generated agent guidance, and release distribution are later roadmap phases.
- SDD command output uses stable machine-readable reports as the automation
  contract and plain text as presentation over the same report.
- SDD must remain useful when Governance is absent, and optional Governance
  configuration never becomes required for this command phase.
