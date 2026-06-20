# Feature Specification: Bootstrap and Migration Experience

**Feature Branch**: `016-bootstrap-migration`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: establishes the user-facing
end-to-end bootstrap/quickstart contract, the migration path from an existing
Spec Kit project to native SDD artifacts, the documented optional
Governance-after-init integration boundary, and an automated no-Governance
lifecycle smoke harness over the existing `fsgg-sdd` command surface and artifact
layout. It adds no new lifecycle stage, command, or authored-source schema.)

**Input**: User description: "Start the next item on the implementation plan.
With the `charter -> ship` lifecycle, the cross-cutting `agents` and `refresh`
generators, and Phases 7 and 8 complete, the next SDD-owned item in
`docs/initial-implementation-plan.md` is Phase 9: Bootstrap And Migration
Experience. Make FS.GG.SDD usable for new products and existing Spec Kit
projects: provide an end-to-end quickstart from `fsgg-sdd init` through
`fsgg-sdd ship` that runs without the Governance gate runtime installed, add an
automated lifecycle smoke that creates a temporary SDD project and runs the
lifecycle without Governance, provide migration guidance from existing Spec Kit
projects to native SDD artifacts while preserving standard Spec Kit as a valid
workflow, and document how Governance policy/capability/tooling files can be
added after SDD initialization. Keep runtime product templates and
FS.GG.Rendering template-provider delegation optional and out of scope, and keep
Governance-owned routing, freshness, profiles, gates, and enforcement out of
scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Take a Work Item From Init Through Ship Without Governance (Priority: P1)

As a new FS.GG.SDD user starting from an empty directory with no Governance
runtime installed, I need a single end-to-end quickstart that walks me from
`fsgg-sdd init` through `fsgg-sdd ship` so that I can spec-drive a work item to
merge-boundary readiness and obtain the deterministic SDD readiness artifacts
without first learning hidden FS.GG repository knowledge or installing the
Governance gate runtime.

**Why this priority**: The product's central promise is that it is useful to
consumers before Governance is installed. A new user has no guided path that
shows the lifecycle as one continuous walkthrough. Without this slice, the
lifecycle commands exist but the bootstrap experience that ties them into a
usable product does not, and the acceptance bar item "start as a greenfield
project and spec-drive work through ship" is unmet.

**Independent Test**: Can be tested by following the quickstart in an empty
directory with no Governance files present and confirming that the lifecycle
proceeds in canonical order from `fsgg-sdd init` through `fsgg-sdd ship`, that
each stage names the authored source it writes and the generated readiness view
it refreshes or reports, and that the run produces the SDD-owned readiness
artifacts (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`,
`summary.md`, and agent guidance) without requiring Governance.

**Acceptance Scenarios**:

1. **Given** an empty directory and no installed Governance gate runtime,
   **When** a new user follows the quickstart, **Then** `fsgg-sdd init` creates
   the SDD project skeleton (`.fsgg` configuration, work root, readiness root,
   and agent guidance targets) and the quickstart directs the user to the first
   lifecycle stage for a selected work item.
2. **Given** an initialized SDD project, **When** the user follows the quickstart
   through the lifecycle, **Then** the stages are presented in canonical order
   (charter, specify, clarify, checklist, plan, tasks, analyze, evidence, verify,
   ship), and for each stage the quickstart names the authored source written and
   the generated readiness view refreshed or reported.
3. **Given** the user reaches the end of the quickstart, **When** `fsgg-sdd ship`
   completes, **Then** the work item has produced the SDD-owned readiness
   artifacts, and the quickstart shows how the cross-cutting `fsgg-sdd agents`
   and `fsgg-sdd refresh` generators bring the agent guidance and human-readable
   summary to currency, all without Governance installed.

---

### User Story 2 - Verify the End-to-End Lifecycle With an Automated No-Governance Smoke (Priority: P1)

As a maintainer or CI operator, I need an automated smoke that creates a
disposable SDD project and runs the full lifecycle from `fsgg-sdd init` through
`fsgg-sdd ship` without the Governance gate runtime installed so that the
documented bootstrap experience is verified to work and cannot silently drift
from command behavior.

**Why this priority**: A quickstart that is not exercised by an automated test
decays as the commands evolve. The smoke is the verifiable backbone of the
bootstrap promise: it proves a new project can be created and advanced through
ship with no Governance, and it produces the real evidence that the lifecycle is
usable standalone. Without it, the quickstart is an unverified claim.

**Independent Test**: Can be tested by running the automated smoke and confirming
that it creates a temporary SDD project, runs the lifecycle init-through-ship
plus the `agents` and `refresh` generators, asserts the expected readiness
artifacts are produced, and completes with zero Governance policy, capability, or
tooling files present.

**Acceptance Scenarios**:

1. **Given** a disposable working directory with no Governance files, **When**
   the smoke runs, **Then** it executes `fsgg-sdd init` through `fsgg-sdd ship`
   plus the `agents` and `refresh` generators and asserts that each stage
   succeeds and produces its expected authored source and generated readiness
   view.
2. **Given** the smoke has completed, **When** its assertions are evaluated,
   **Then** the run confirms the SDD-owned readiness artifacts exist and are
   well-formed, and confirms that no Governance policy, capability, tooling,
   route, profile, gate, audit, or enforcement behavior was required.
3. **Given** the smoke is run twice over identical inputs, **When** the
   machine-readable readiness outputs are compared, **Then** they are identical,
   demonstrating the documented lifecycle is deterministic.

---

### User Story 3 - Migrate an Existing Spec Kit Project to Native SDD Artifacts (Priority: P2)

As a maintainer of an existing standard Spec Kit project, I need migration
guidance that maps my Spec Kit feature directories and specs onto native SDD
`.fsgg` and `work/<id>` artifacts so that I can adopt the SDD lifecycle
incrementally while keeping standard Spec Kit as a valid workflow and without
deleting or rewriting my existing authored content.

**Why this priority**: Existing Spec Kit users are the primary adoption path, but
they have no documented, non-destructive route to native SDD artifacts. The
migration must be additive and must preserve Spec Kit so adopters are never
forced into a risky rewrite. This unblocks adoption without sacrificing the
repository's own Spec Kit workflow.

**Independent Test**: Can be tested by applying the migration guidance against a
representative existing Spec Kit project and confirming that the documented steps
add SDD artifacts (`.fsgg` configuration and `work/<id>` sources) while leaving
the existing `specs/` and `.specify/` content unchanged, with standard Spec Kit
still a valid workflow afterward.

**Acceptance Scenarios**:

1. **Given** an existing Spec Kit project with `specs/` and `.specify/` content,
   **When** the maintainer follows the migration guidance, **Then** the guidance
   describes additive steps that create the SDD project skeleton and map Spec Kit
   feature artifacts onto `.fsgg` and `work/<id>` sources without removing or
   rewriting existing Spec Kit content.
2. **Given** the migration has been applied, **When** the maintainer continues
   working, **Then** standard Spec Kit remains a valid development workflow and
   the native SDD lifecycle commands are also available over the migrated
   artifacts.
3. **Given** a Spec Kit artifact has no direct SDD equivalent, **When** the
   maintainer reaches that step, **Then** the migration guidance explains how to
   represent or defer it rather than instructing destructive removal of authored
   content.

---

### User Story 4 - Adopt Optional Governance After SDD Initialization (Priority: P3)

As a maintainer who initialized SDD first, I need documentation of how optional
Governance policy, capability, and tooling files can be added after
`fsgg-sdd init` so that I can layer protected-boundary rigor later without
breaking SDD usability or being forced to install Governance up front.

**Why this priority**: The product is designed to be useful before Governance and
strict at protected boundaries once Governance is adopted. The bootstrap
experience must document that Governance is an optional, additive layer so that
adopters understand SDD stays usable whether Governance files are present,
absent, or incomplete.

**Independent Test**: Can be tested by following the documented Governance
adoption steps in an SDD-initialized project and confirming that adding the
optional Governance files does not change the behavior or usability of the
SDD-owned lifecycle commands, and that SDD commands still run when those files
are absent or incomplete.

**Acceptance Scenarios**:

1. **Given** an SDD-initialized project with no Governance files, **When** the
   maintainer follows the documented adoption steps, **Then** the guidance shows
   that Governance policy, capability, and tooling files are added after SDD
   initialization as an optional, additive layer.
2. **Given** Governance files are present, absent, or incomplete, **When** the
   maintainer runs the SDD lifecycle commands, **Then** every SDD-owned command
   remains usable, and SDD does not evaluate or enforce Governance-owned routing,
   freshness, profiles, gates, audit, or release decisions.
3. **Given** the documentation describes the Governance boundary, **When** a
   reader reviews it, **Then** Governance integration references are presented as
   optional compatibility facts rather than required SDD behavior.

### Edge Cases

- The quickstart is followed in a directory that already contains SDD or Spec Kit
  artifacts, so init must preserve existing user files rather than overwrite
  them.
- The smoke runs in an environment where the Governance gate runtime is not
  installed at all and must still complete the full lifecycle.
- The smoke runs twice and must produce identical machine-readable readiness
  outputs to demonstrate determinism.
- A Spec Kit project being migrated contains feature artifacts that have no direct
  native SDD equivalent.
- A Spec Kit project being migrated has already partially adopted SDD artifacts,
  so migration steps must be safe to re-apply without clobbering authored content.
- Optional Governance files are added after init, then later become incomplete or
  are removed, and SDD lifecycle commands must remain usable in every case.
- The quickstart references generated readiness views, which are outputs whose
  currency depends on running refresh rather than on the files merely existing.
- A reader follows the bootstrap without FS.GG.Rendering, a monorepo checkout, or
  any runtime product templates available.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide an end-to-end quickstart that walks a new
  user from `fsgg-sdd init` through `fsgg-sdd ship` for a selected work item using
  only the SDD command surface, with no Governance gate runtime installed.
- **FR-002**: The quickstart MUST present the lifecycle stages in their canonical
  order (charter, specify, clarify, checklist, plan, tasks, analyze, evidence,
  verify, ship) and, for each stage, identify the authored source it writes and
  the generated readiness view it refreshes or reports.
- **FR-003**: The quickstart MUST show how the SDD-owned readiness artifacts
  (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`, `summary.md`,
  and the agent guidance views) are produced, and MUST show where the
  cross-cutting `fsgg-sdd agents` and `fsgg-sdd refresh` generators bring agent
  guidance and the human-readable summary to currency.
- **FR-004**: The feature MUST provide an automated smoke that creates a
  disposable SDD project, runs the lifecycle from `fsgg-sdd init` through
  `fsgg-sdd ship` plus the `agents` and `refresh` generators, and asserts that
  each stage succeeds and produces its expected authored source and generated
  readiness view.
- **FR-005**: The automated smoke MUST run without the Governance gate runtime
  installed and MUST NOT require any Governance policy, capability, or tooling
  files to complete the lifecycle.
- **FR-006**: The automated smoke MUST verify that re-running the lifecycle over
  identical inputs produces identical machine-readable readiness outputs.
- **FR-007**: The feature MUST provide migration guidance from an existing
  standard Spec Kit project to native SDD artifacts, mapping Spec Kit feature
  directories and specs onto `.fsgg` configuration and `work/<id>` authored
  sources.
- **FR-008**: The migration guidance MUST preserve standard Spec Kit as a valid
  development workflow and MUST NOT require deleting, rewriting, reordering, or
  normalizing existing `specs/` or `.specify/` authored content.
- **FR-009**: The migration guidance MUST be additive and safe to re-apply, and
  MUST explain how to represent or defer Spec Kit artifacts that have no direct
  native SDD equivalent rather than instructing destructive removal.
- **FR-010**: The feature MUST document how optional Governance policy,
  capability, and tooling files can be added after `fsgg-sdd init` as an additive
  layer without changing the behavior or usability of the SDD-owned lifecycle
  commands.
- **FR-011**: The documented Governance adoption MUST keep every SDD lifecycle
  command usable whether Governance files are present, absent, or incomplete, and
  MUST present Governance integration references as optional compatibility facts.
- **FR-012**: The feature MUST NOT introduce a new lifecycle stage, a new
  `fsgg-sdd` command, or a new authored-source schema; it documents and verifies
  the existing command surface and artifact layout.
- **FR-013**: The bootstrap and migration deliverables MUST NOT assume
  FS.GG.Rendering, a monorepo checkout, or runtime product templates; runtime and
  template-provider delegation remains optional and out of scope for this feature.
- **FR-014**: The quickstart and smoke MUST reflect the same canonical stage order
  and next-action pointers that the lifecycle commands emit, so the documentation
  cannot silently diverge from command behavior.
- **FR-015**: Where the quickstart, smoke, or migration guidance reference
  generated readiness views, they MUST treat those views as outputs whose currency
  comes from running refresh, not from file presence alone.
- **FR-016**: The feature MUST NOT introduce Governance effective-evidence
  freshness, route selection, profile adjustment, gate selection,
  protected-boundary enforcement, audit verdicts, or release gating behavior.

### Key Entities

- **Quickstart Walkthrough**: The documented end-to-end path that takes a new
  user from `fsgg-sdd init` through `fsgg-sdd ship`, naming each stage's authored
  source and generated readiness view, runnable without Governance.
- **Lifecycle Smoke Run**: An automated run that creates a disposable SDD project
  and executes the lifecycle init-through-ship plus the `agents` and `refresh`
  generators, asserting expected artifacts and determinism without Governance.
- **Migration Guide**: The documented, additive, non-destructive mapping from an
  existing Spec Kit project's `specs/` and `.specify/` artifacts onto native SDD
  `.fsgg` and `work/<id>` sources, preserving standard Spec Kit.
- **SDD Project Skeleton**: The `.fsgg` configuration, work root, readiness root,
  and agent guidance targets created by `fsgg-sdd init` that the quickstart and
  migration build upon.
- **Optional Governance Adoption Note**: The documentation describing how
  Governance policy, capability, and tooling files are added after SDD
  initialization as an optional, additive compatibility layer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Starting from an empty directory with no Governance gate runtime
  installed, a new user can follow the quickstart to take a work item from
  `fsgg-sdd init` through `fsgg-sdd ship` and obtain the SDD-owned readiness
  artifacts in one continuous walkthrough.
- **SC-002**: The automated lifecycle smoke creates a disposable project and
  completes `fsgg-sdd init` through `fsgg-sdd ship` plus the `agents` and
  `refresh` generators with zero Governance policy, capability, or tooling files
  present.
- **SC-003**: Two smoke runs over identical inputs produce byte-identical
  machine-readable readiness outputs.
- **SC-004**: 100% of the lifecycle stages named in the quickstart match the
  canonical order and the next-action pointers emitted by the corresponding
  commands.
- **SC-005**: Applying the migration guidance against an existing Spec Kit project
  adds SDD artifacts while leaving existing `specs/` and `.specify/` content
  unchanged, with 0 deletions or rewrites of authored Spec Kit content.
- **SC-006**: Every SDD lifecycle command remains runnable in the documented
  adoption scenario whether Governance files are present, absent, or incomplete.
- **SC-007**: The quickstart and smoke complete with no FS.GG.Rendering package,
  monorepo checkout, or runtime product templates available.
- **SC-008**: Following the quickstart, a user can identify for each lifecycle
  stage the authored source written and the generated readiness view refreshed or
  reported.

## Assumptions

- With the `charter -> ship` lifecycle, the cross-cutting `agents` and `refresh`
  generators, and Phases 7 and 8 complete, the next SDD-owned item in
  `docs/initial-implementation-plan.md` is Phase 9: Bootstrap And Migration
  Experience.
- `fsgg-sdd init` already creates the SDD project skeleton (`.fsgg/project.yml`,
  `.fsgg/sdd.yml`, `.fsgg/agents.yml`, the work root, the readiness root, and the
  Claude/Codex agent guidance targets); this feature documents and verifies the
  end-to-end bootstrap experience over that skeleton rather than re-implementing
  init.
- The lifecycle commands `charter` through `ship`, plus the cross-cutting
  `fsgg-sdd agents` and `fsgg-sdd refresh` generators, are implemented and produce
  the SDD-owned readiness views referenced by the quickstart and smoke.
- Runtime product templates and FS.GG.Rendering template-provider delegation for
  generating product runtime code are optional and out of scope for this feature;
  SDD owns the lifecycle skeleton only, and runtime ownership stays outside SDD.
- Governance policy, capability, and tooling files are Governance-owned and
  optional; SDD remains independently usable when they are absent or incomplete,
  and Governance-owned routing, freshness, profiles, gates, audit, and
  enforcement remain out of SDD scope.
- The quickstart, smoke, and migration guidance target the existing `fsgg-sdd`
  command family and the existing `.fsgg` plus `work/<id>` artifact layout; no
  new command, lifecycle stage, or authored-source schema is introduced.
- "Disposable" project for the smoke means a temporary working directory created
  and cleaned up by the test, with no dependency on the surrounding repository's
  Governance state.
