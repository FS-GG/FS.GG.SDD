---
schemaVersion: 1
workId: 927-single-quint-lifecycle
title: Single Quint-backed workspace lifecycle
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/927-single-quint-lifecycle/spec.md
publicOrToolFacingImpact: true
---

# Single Quint-backed workspace lifecycle Clarifications

## Source Specification
- work/927-single-quint-lifecycle/spec.md

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
- DEC-001 [FR-008]: Use one cohesive shared-state Quint record; proposals operate against accepted state and do not exchange messages, so Choreo would add no semantic value.
- DEC-002 [FR-002] [FR-003]: Preserve the maintainer-approved six dispositions exactly. ImplementationChange, OperationalChange, and EvidenceChange are represented as reasons within NoSemanticChange rather than rival semantic dispositions.
- DEC-003 [FR-007] [FR-009]: New omitted paths converge on typed-sdd/quint. Legacy tokens remain explicit compatibility selections and never alias to the new default.
- DEC-004 [FR-004]: A human acceptance receipt is mandatory for CoherentDelta, NoSemanticChange, and AcceptedOpaque; all other dispositions are non-reducible.
- DEC-005 [FR-006]: Migration planning is pure and dry-run-first. Effectful application remains at the existing atomic Typed SDD migration boundary.

## Accepted Deferrals
- Downstream default activation waits for actual `OperatingV2`; producer implementation and publication do not.
- Reconciliation/correspondence agent workflows owned by child issue #934 follow the accepted producer surface and do not block defining it.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 927-single-quint-lifecycle`.
