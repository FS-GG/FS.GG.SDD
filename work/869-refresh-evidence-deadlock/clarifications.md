---
schemaVersion: 1
workId: 869-refresh-evidence-deadlock
title: Refresh Evidence Deadlock
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/869-refresh-evidence-deadlock/spec.md
publicOrToolFacingImpact: true
---

# Refresh Evidence Deadlock Clarifications

## Source Specification
- work/869-refresh-evidence-deadlock/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking answered: Which of the three admissible outcomes does this work take — `evidence` scaffolds while analysis is blocked, the work model derives from an incomplete `evidence.yml`, or neither changes and the deadlock is documented?
- CQ-002 [AMB:AMB-002] blocking answered: Does the demoted condition keep the `unknownReference` id at a lower severity, or take a new id of its own?
- CQ-003 [AMB:AMB-003] blocking answered: How does `refresh` come to hold the at-fault source path that the work-model generator's own diagnostics carry?

## Answers
- CQ-001 → Derive the work model from an `evidence.yml` that is merely incomplete. Measured, all three outcomes break the cycle, so the choice is made on which one is TRUE of the artifacts rather than on which one is smallest. The work model is a normalization of the authored artifacts, and `evidence.yml` is a declared source of it; a task requiring an evidence id that `evidence.yml` has not yet declared is a true, complete fact about the lifecycle, and normalizing a true fact is what the model is for. Refusing to build it because a DOWNSTREAM stage has not run inverts the direction the lifecycle itself declares — the work model is upstream of `evidence`, yet today it demands `evidence`'s output. Two further measurements settle it. First, the absent case already behaves this way: with `evidence.yml` deleted entirely, `analyze` reports `implementationReady` and `evidence` scaffolds all thirty declarations, so "evidence.yml does not declare this obligation yet" is ALREADY a state the lifecycle derives through — the deadlock fires only in the strictly SMALLER case where thirty of thirty-one are declared. Second, the enforcement is not the work model's: `evidence` already reports the exact undeclared id as `evidence.missingRequiredEvidence [EV031]` in the very report where it refuses, and `verify` and `ship` refuse on unmet obligations too, so demoting the work-model edge moves no gate — it deletes a redundant, coarser copy of a check that survives downstream and names the id.
- CQ-002 → A new id. Demoting `unknownReference` in place would make one id mean two different things — "this reference names something that will never exist" and "this reference names something a later stage will author" — and every consumer that keys on the id, rather than on the severity, would silently inherit the ambiguity. A distinct id also lets the correction say the one thing the author actually needs, which is the name of the command that closes the gap. `unknownReference` keeps its meaning and its severity on all three upstream edges.
- CQ-003 → Widen the generator's return with an explicit, typed list of the at-fault source paths. The alternative — appending paths to an existing diagnostic's `relatedIds` next to its diagnostic ids — would leave `refresh` sniffing one untyped list for two different kinds of value, which is how the placeholder survived in the first place. The cost is nine call sites that bind and discard one extra element, which is mechanical and checked by the compiler.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [FR-003] [FR-006] [AC-001] [AC-003] [AC-006]: The `tasks.yml` -> `evidence.yml` reference edge stops blocking work-model derivation. It is the only reference in `tasks.yml` that points at an artifact a LATER lifecycle stage authors; the other three point upstream at artifacts that already exist. An unresolved DOWNSTREAM reference is an incomplete lifecycle and must derive; an unresolved UPSTREAM reference is an inconsistent one and must still block. That boundary — direction of the edge, not identity of the artifact — is the rule this work implements, and it is stated in the correction the author reads.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-001] [FR-002] [AC-001] [AC-002]: The demoted condition takes a new warning-severity diagnostic id whose artifact is `evidence.yml` — the artifact that must change — and whose `relatedIds` carry the undeclared evidence id and the citing `tasks.yml`. `unknownReference` and `workModelInconsistent` are unchanged in id, severity, message and behaviour on every other edge.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-004] [FR-005] [AC-004] [AC-005]: The work-model generation seam returns the at-fault source paths as a typed value, and `refresh` consumes it in place of its hard-coded argument. Where that list is empty, `refresh` reports that it could not attribute the blockage rather than naming an arbitrary artifact — "could not look" is never a negative verdict about a particular file (ADR-0002).
- **DEC-004** [CQ-001] [FR-003] [AC-003]: No gate is deleted. The regression coverage asserts positively that an obligation which is never declared still stops `evidence`, `verify` and `ship`, so the relocation in DEC-001 is shown to have moved the check rather than removed it.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001, AMB-002 and AMB-003 are resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 869-refresh-evidence-deadlock`.
