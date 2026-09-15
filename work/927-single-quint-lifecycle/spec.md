---
schemaVersion: 1
workId: 927-single-quint-lifecycle
title: Single Quint-backed workspace lifecycle
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Single Quint-backed workspace lifecycle Specification

Prose status: specified

## User Value
Every new FS-GG workspace starts on one Quint-backed Typed SDD authority while freeform prose, structured SDD, and direct Quint remain low-friction authoring depths over the same model.

## Scope
- SB-001: Add a versioned `WorkspaceModel` covering product specification, decisions, work changes, repository profile, CI obligations, external contracts, and evidence requirements.
- SB-002: Add revision-bound issue `ChangeProposal` validation, readable semantic diff, deterministic three-way reconciliation, and human-authorized reduction.
- SB-003: Add dry-run-first migration inventory for `none`, `sdd`, `typed-sdd`, and `spec-kit`, preserving original byte identities and rollback evidence.
- SB-004: Make Quint the Typed SDD backend default while retaining explicit F# v1 inspection and rollback during the declared compatibility window.

## Non-Goals
- SB-005: Issue creation is never blocked by formalization and never mutates the accepted model.
- SB-006: This producer PR does not itself flip Templates or wizard defaults, mutate a coordination epoch, or claim `OperatingV2`.
- SB-007: The model does not duplicate GitHub identities or product implementation logic.

## User Stories
- US-001 (P1): As a prose-first user, I can open and work an issue before its semantic classification is resolved.
- US-002 (P1): As a reviewer, I can see an exact-base semantic diff and explicitly accept only coherent or named opaque changes.
- US-003 (P1): As an existing workspace owner, I can preview migration, preserve every original byte identity, and roll back without silent reinterpretation.
- US-004 (P1): As a new workspace owner, I receive the same Quint-backed lifecycle regardless of selected authoring depth.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-004]: Filing a proposal leaves the accepted fingerprint unchanged; ambiguity, contradiction, and stale bases remain explicit non-accepting states.
- AC-002 [US-002] [FR-002] [FR-003] [FR-005]: Disjoint exact-base deltas reconcile deterministically; overlaps, dangling identities, rename/delete conflicts, changed assumptions, and stale evidence refuse reduction.
- AC-003 [US-003] [FR-006] [FR-007]: Each legacy source is classified as Migrated, Ambiguous, Unsupported, Preserved, or Removed and the plan binds original digests plus rollback inventory before any write.
- AC-004 [US-004] [FR-008] [FR-009]: Freeform, structured, and direct-Quint projections resolve to the same schema and omitted Typed SDD authoring selects Quint; explicit v1 remains inspectable.

## Functional Requirements
- FR-001: The public contract MUST represent all seven workspace module kinds with stable IDs and deterministic canonical fingerprints. (Stories: US-001, US-004; Acceptance: AC-001, AC-004)
- FR-002: Every proposal MUST bind an exact issue identity, prose digest, base fingerprint, authoring depth, and one disposition: CoherentDelta, NoSemanticChange, AcceptedOpaque, Ambiguous, Contradictory, or Stale. (Stories: US-001, US-002; Acceptance: AC-001, AC-002)
- FR-003: AcceptedOpaque MUST name debt, affected subjects, reason, responsible human, and evidence; it may never be inferred from a failed formalization. (Stories: US-002; Acceptance: AC-002)
- FR-004: Creating, classifying, or reconciling a proposal MUST NOT mutate accepted authority. Only explicit human acceptance may reduce an eligible exact-base proposal. (Stories: US-001, US-002; Acceptance: AC-001, AC-002)
- FR-005: Semantic reconciliation MUST deterministically detect overlapping edits, duplicate declarations, removed/renamed identities, dangling references, changed assumptions, stale bases, and stale evidence. (Stories: US-002; Acceptance: AC-002)
- FR-006: Migration MUST be dry-run-first, bind original bytes by digest, retain rollback inventory, and refuse writes until every source classification and human decision is complete. (Stories: US-003; Acceptance: AC-003)
- FR-007: The compatibility window MUST preserve inspection and explicit migration of none, sdd, typed-sdd, and spec-kit without token aliasing or destructive scaffold reruns. (Stories: US-003; Acceptance: AC-003)
- FR-008: The package MUST carry one literate Quint lifecycle model with reachable proposal and acceptance actions plus invariants for immutable issue filing, exact-base acceptance, coherent accepted authority, and monotonic revisions. (Stories: US-001, US-002; Acceptance: AC-001, AC-002)
- FR-009: Typed SDD authoring and migration MUST select quint-specification-v1 when backend is omitted, while explicit fsharp-specification-v1 remains supported for compatibility. (Stories: US-004; Acceptance: AC-004)
- FR-010: CLI, JSON, plain/rich projection, installed skills, and package-only tests MUST share the typed kernel and stable diagnostics; no skill may implement a second semantic reducer. (Stories: US-001, US-002, US-003, US-004; Acceptance: AC-001, AC-002, AC-003, AC-004)

## Ambiguities
No blocking ambiguity remains. The accepted issue decision supplies the disposition vocabulary and freeform guarantee; this work chooses plain shared-state Quint because there is one accepted authority and no inter-actor message protocol.

## Public Or Tool-Facing Impact
- Adds public workspace lifecycle/proposal/migration contracts and changes the omitted Typed SDD backend.
- This is a breaking stable-line behavior change and therefore prepares SDD 2.0.0 with a migration note.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 927-single-quint-lifecycle`.
