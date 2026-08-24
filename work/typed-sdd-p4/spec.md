---
schemaVersion: 1
workId: typed-sdd-p4
title: Typed SDD additive lifecycle backend
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Typed SDD additive lifecycle backend Specification

Prose status: specified

## User Value
Publish Typed SDD as an additive installed-artifact lifecycle lane.

## Scope
- SB-001: Lifecycle choice, representation backend, compiler, authoring, inspection, migration, provenance, skills, refresh, upgrade, doctor, readiness, ship, fixtures, and producer documentation.

## Non-Goals
- SB-002: Do not change the omitted lifecycle default; P5 owns that decision and rollout.
- SB-003: Do not implement the deferred Typed SDD constitution/Governance integration.
- SB-004: Do not move S.I.R. rule vocabulary or interpreters into FS.GG.SDD.
- SB-005: Do not remove Standard SDD, Freeform, or the already-scheduled legacy `spec-kit` compatibility token.

## User Stories
- US-001 (P1): As a workspace author, I can select `typed-sdd` and use one familiar SDD stage sequence
  whose canonical authority is an inspectable compiled F# specification model.
- US-002 (P1): As an operator, I can migrate a Standard SDD item only after inspecting an explicit
  preservation/ambiguity report and accepting the proposed semantic diff.
- US-003 (P1): As a provider maintainer, I can consume one published lifecycle/backend contract and
  prove `none`, `sdd`, and `typed-sdd` without copying stage instructions or depending on S.I.R.
- US-004 (P2): As a maintainer, I receive stable diagnostics that distinguish identity, compatibility,
  authority, freshness, direct-edit, and authoring-agent failures.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given no lifecycle value, when selection resolves, then it resolves to `sdd`;
  explicit `none`, `sdd`, `typed-sdd`, and legacy `spec-kit` remain distinct stable values.
- AC-002 [US-001] [FR-002]: Given a clean directory and installed published artifacts only, when an
  agent authors a Typed SDD specification and runs every lifecycle stage through ship, then the canonical
  model, normalized AST, authoring receipt, projections, readiness, and ship output are coherent.
- AC-003 [US-002] [FR-003]: Given supported Standard SDD artifacts, when migration is analyzed, then it
  returns `Migrated` with a semantic diff and rollback boundary but writes nothing until `--accept`.
- AC-004 [US-002] [FR-003]: Given prose requiring judgement or a construct outside the supported
  requirements extension, when migration is analyzed, then it returns `Ambiguous` or `Unsupported`
  with stable locations and leaves every source byte unchanged.
- AC-005 [US-003] [FR-001] [FR-002]: Given each supported provider/profile fixture, when lifecycle is
  explicitly `none`, `sdd`, or `typed-sdd`, then the value survives creation, provenance, refresh,
  upgrade, doctor, generated guidance, readiness, and ship without aliasing or fallback.
- AC-006 [US-004] [FR-004]: Given each required negative control, when doctor/readiness/ship runs, then
  wrong lifecycle, missing compiler, stale projection, unsupported extension, direct canonical edit,
  and unavailable agent produce six distinct actionable diagnostic IDs.

## Functional Requirements
- FR-001: Explicit none, sdd, and typed-sdd are preserved; omitted lifecycle resolves to sdd; spec-kit gains no behavior. (Stories: US-001; Acceptance: AC-001)
- FR-002: A clean consumer using only published packages and tools authors a canonical F# model, compiles it, completes all lifecycle stages, refreshes, upgrades, and ships. (Stories: US-001; Acceptance: AC-001)
- FR-003: Standard SDD migration returns Migrated, Ambiguous, or Unsupported, emits semantic diff and rollback facts, and writes only after explicit acceptance. (Stories: US-002; Acceptance: AC-003, AC-004)
- FR-004: Wrong lifecycle, missing compiler, stale projection, unsupported extension, direct edit, and unavailable agent each produce a distinct actionable diagnostic. (Stories: US-004; Acceptance: AC-006)
- FR-005: Standard SDD and Typed SDD reuse one ordered stage contract and one stage-skill instruction corpus; provenance selects a representation backend that owns source, projections, and checks. (Stories: US-001, US-003; Acceptance: AC-002, AC-005) (covers AC-002, AC-005)
- FR-006: Typed SDD provenance binds lifecycle value, compiler/package identity, extension identity, canonical source path/digest, normalized model digest, authoring receipt, projection digests, and rollback source digest. (Stories: US-001, US-004; Acceptance: AC-002, AC-006) (covers AC-002, AC-006)
- FR-007: `author` and `inspect` expose installed skill operations over the package-owned specification kernel and requirements extension; authored source is F#, normalized JSON is generated, and Markdown cannot be ingested as Typed SDD authority. (Stories: US-001, US-003; Acceptance: AC-002) (covers AC-002)
- FR-008: Refresh and upgrade preserve the selected lane, content identity, supported consumer extension nodes, and canonical source; neither operation silently rewrites authored specification meaning. (Stories: US-001, US-003; Acceptance: AC-005) (covers AC-005)
- FR-009: Producer tests include clean installed-package consumption and subject mutations for lifecycle aliasing, compiler absence, identity mismatch, unsupported extension, projection staleness, direct edit, migration ambiguity, pre-accept write, and unavailable authoring agent. (Stories: US-003, US-004; Acceptance: AC-001 through AC-006) (covers AC-001, AC-002, AC-003, AC-004, AC-005, AC-006)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work typed-sdd-p4`.
