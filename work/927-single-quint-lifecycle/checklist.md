---
schemaVersion: 1
workId: 927-single-quint-lifecycle
title: Single Quint-backed workspace lifecycle
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/927-single-quint-lifecycle/spec.md
sourceClarifications: work/927-single-quint-lifecycle/clarifications.md
publicOrToolFacingImpact: true
---

# Single Quint-backed workspace lifecycle Checklist

Prose status: checklistReady

## Source Snapshot
- spec: work/927-single-quint-lifecycle/spec.md sha256:bc9271fa0dad56388caade41928dfa07a15b33a1e0367a6a4d30166a395b3eed schemaVersion:1
- clarifications: work/927-single-quint-lifecycle/clarifications.md sha256:76f57836cdfd3dfe60eec538231959bdf5190a35f50c2d79a8687a551734f250 schemaVersion:1

## Source Specification
- work/927-single-quint-lifecycle/spec.md

## Source Clarifications
- work/927-single-quint-lifecycle/clarifications.md

## Checklist Items
- CHK-001 [FR-001] [AC-001] [AC-004] blocking: Seven module kinds, stable identities, canonical bytes, and fingerprints are explicit and testable.
- CHK-002 [FR-002] [FR-003] [AC-001] [AC-002] blocking: Proposal identity, all six dispositions, and opaque-debt evidence are explicit and testable.
- CHK-003 [FR-004] [FR-005] [AC-001] [AC-002] blocking: Non-mutating proposal operations and fail-closed semantic reconciliation are explicit and testable.
- CHK-004 [FR-006] [FR-007] [AC-003] blocking: Dry-run migration, complete classification, original digests, rollback, and legacy token compatibility are explicit and testable.
- CHK-005 [FR-008] [AC-001] [AC-002] blocking: The single literate Quint model has reachable actions and named safety invariants.
- CHK-006 [FR-009] [AC-004] blocking: Omitted Quint selection and explicit F# compatibility are explicit and testable.
- CHK-007 [FR-010] [AC-001] [AC-002] [AC-003] [AC-004] blocking: All projections and installed guidance share the public typed kernel.

## Review Results
- CR-001 [CHK:CHK-001] pass: Closed module vocabulary and deterministic fingerprint obligation are bounded.
- CR-002 [CHK:CHK-002] pass: Disposition and AcceptedOpaque metadata requirements match the maintainer decision.
- CR-003 [CHK:CHK-003] pass: Reduction eligibility, exact-base checks, and conflict classes are unambiguous.
- CR-004 [CHK:CHK-004] pass: Migration is pure until a complete, human-decided plan reaches the existing atomic boundary.
- CR-005 [CHK:CHK-005] pass: Plain shared-state Quint is the smallest faithful concurrency model.
- CR-006 [CHK:CHK-006] pass: Default and compatibility behavior are independently observable.
- CR-007 [CHK:CHK-007] pass: The typed package remains the only semantic reducer.

## Accepted Deferrals
- Template and wizard default activation waits for actual `OperatingV2` and public-package qualification.

## Blocking Findings
No blocking findings recorded.

## Advisory Notes
- The downstream `OperatingV2` gate is an accepted rollout deferral, not a producer blocker.

## Lifecycle Notes
- Requirements reviewed: 10.
- Next lifecycle action: `fsgg-sdd plan --work 927-single-quint-lifecycle`.
