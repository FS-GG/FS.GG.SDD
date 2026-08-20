---
schemaVersion: 1
workId: 886-comment-quality-policy
title: Comment Quality Policy
stage: clarify
changeTier: tier1
status: needsAnswers
sourceSpec: work/886-comment-quality-policy/spec.md
publicOrToolFacingImpact: true
---

# Comment Quality Policy Clarifications

## Source Specification
- work/886-comment-quality-policy/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking: Must all policy projections be byte-identical, or is equivalent complete wording sufficient?
- CQ-002 [AMB:AMB-002] blocking: Does the existing no-clobber test adequately protect this policy change?

## Answers
- CQ-001: The embedded init seed and authoritative constitution-content contract must remain byte-equivalent where the existing test defines that contract; the repository's active constitutions may use context-appropriate framing but must carry every obligation.
- CQ-002: The existing authored-constitution preservation test exercises the exact init write boundary and remains sufficient when run with the changed seed.

## Decisions
- DEC-001 [CQ-001] [AMB:AMB-001] [FR-002] [AC-002]: Preserve the existing byte-equivalence assertion for the authoritative contract and embedded seed, and assert complete obligation parity for producer constitutions through exact focused tests.
- DEC-002 [CQ-002] [AMB:AMB-002] [FR-003] [AC-003]: Retain and rerun the existing no-clobber regression; no new migration behavior or separate migration test is introduced.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001 and AMB-002 are resolved by DEC-001 and DEC-002.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 886-comment-quality-policy`.
