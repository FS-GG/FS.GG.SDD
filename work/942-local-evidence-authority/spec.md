---
schemaVersion: 1
workId: 942-local-evidence-authority
title: Local Evidence Authority
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Local Evidence Authority Specification

Prose status: specified

## User Value
Authors and reviewers can trust that shipReady evidence remains authoritative in a clean exact-candidate checkout.

## Scope
- SB-001: Validate local evidence provenance and explicit durable external receipts without changing ordinary tracked evidence behavior.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As an author or reviewer, I can trust that a `shipReady` verdict names evidence available from the exact Git candidate or an explicit durable external receipt.
- US-002 (P1): As an author, I receive an actionable diagnostic before ship readiness when local evidence is ignored, untracked, or absent from the candidate.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a real-pass declaration cites an ignored file that exists locally, when evidence, verify, or ship evaluates it, then the command refuses the local authority with a dedicated diagnostic.
- AC-002 [US-001] [FR-001]: Given a real-pass declaration cites an untracked file that exists locally, when evidence, verify, or ship evaluates it, then the command refuses the local authority with a dedicated diagnostic.
- AC-003 [US-001] [FR-002]: Given a real-pass declaration cites a tracked repository-relative artifact, when evidence, verify, or ship evaluates it, then existing supported behavior remains valid.
- AC-004 [US-001] [FR-003]: Given a declaration uses the explicit external `sourceRefs[].uri` receipt form, when evidence, verify, or ship evaluates it, then no local Git-path authority is required.
- AC-005 [US-002] [FR-004]: Given an exact-head source projection omits a cited local artifact, when verification and shipping are evaluated, then `shipReady` is impossible and the report identifies the rejected path and reason.

## Functional Requirements
- FR-001: The evidence authority boundary MUST classify every local path used by a real non-synthetic pass and refuse paths that Git reports as ignored or untracked. (covers AC-001, AC-002)
- FR-002: The authority check MUST preserve ordinary tracked repository-relative evidence, including tracked work-package, test, source, specification, and documentation artifacts. (covers AC-003)
- FR-003: A `sourceRefs` entry carrying a durable external `uri` MUST remain an explicit non-local receipt and MUST NOT be treated as an untracked local file. (covers AC-004)
- FR-004: `verify` and `ship` MUST apply the same local-authority classification as `evidence`, fail closed when Git provenance cannot be established, and prevent `shipReady` when any required local evidence is unavailable from the candidate. (covers AC-005)
- FR-005: The implementation MUST include inversion controls for ignored-existing, untracked-existing, missing-on-clean-checkout, tracked-work-evidence positive, and explicit-external-receipt positive cases. (covers AC-001, AC-002, AC-003, AC-004, AC-005)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Adds dedicated evidence-authority diagnostics to JSON/text/rich command reports without changing persisted evidence schema version 1.
- Clarifies that `sourceRefs[].uri` is the explicit durable external-receipt form; local paths remain repository-relative and candidate-tracked.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 942-local-evidence-authority`.
