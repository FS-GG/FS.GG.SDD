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
- CQ-001 [AMB:AMB-001] blocking answered: Which of the three admissible outcomes does this work take — `evidence` scaffolds while analysis is blocked, the work model derives from an incomplete `evidence.yml`, or neither changes and the deadlock is documented? And are they in fact mutually exclusive?
- CQ-002 [AMB:AMB-002] blocking answered: Does the demoted condition keep the `unknownReference` id at a lower severity, or take a new id of its own?
- CQ-003 [AMB:AMB-003] blocking answered: How does `refresh` come to hold the at-fault source path that the work-model generator's own diagnostics carry?

## Answers
- CQ-001 → BOTH of the first two, because measurement refutes the premise that they are alternatives. The deadlock has TWO locks, and the report — reasonably, from what it could see — described only the first. Lock 1: the work model refuses to derive from an `evidence.yml` that is merely incomplete. Lock 2: `mergeEvidenceArtifacts` deliberately "does not invent additions beside authored declarations", so `evidence` will not add the one declaration that is missing. Opening lock 1 alone gets `analyze` to `implementationReady` and gets `evidence` PAST its readiness gate — and then `evidence` refuses anyway with `evidence.missingRequiredEvidence [EV031]` and writes nothing. Opening lock 2 alone never reaches the merge at all, because `evidence` is still refused at `analysisNotReady` upstream. Neither alone converges; both together do, and the regression coverage pins the conjunction by inverting each lock separately and observing which tests go red. Why lock 1 is opened at the work model rather than by letting `evidence` run while blocked: the work model is a normalization of the authored artifacts, and `evidence.yml` is a declared source of it. A task requiring an evidence id `evidence.yml` has not yet declared is a true, complete fact about the lifecycle, and normalizing a true fact is what the model is for. Refusing to build it because a DOWNSTREAM stage has not run inverts the direction the lifecycle itself declares. Two measurements settle it. First, the ABSENT case already behaves this way: with `evidence.yml` deleted entirely, `analyze` reports `implementationReady` and `evidence` scaffolds all thirty declarations — so "evidence.yml does not declare this obligation yet" is already a state the lifecycle derives through, and the deadlock fires only in the strictly SMALLER case where thirty of thirty-one are declared. Second, the enforcement is not the work model's to hold: `evidence` already reports the exact undeclared id in the very report where it refuses, and `verify` and `ship` refuse on unmet obligations, so demoting the work-model edge moves no gate — it deletes a redundant, coarser copy of a check that survives downstream and names the id. Why lock 2 is opened by seeding rather than by relaxing a gate: the no-clobber rule protects AUTHORED declarations from being rewritten, and seeding writes declarations for obligations no declaration names, which clobbers nothing. It is the same skeleton, from the same seeder, that the fresh-file path already writes for every obligation at once; it starts at `kind/result: missing`, so it can never make an obligation verification-ready. And it is exactly what an author was previously forced to hand-write, with nothing in the tool ever suggesting it.
- CQ-002 → A new id. Demoting `unknownReference` in place would make one id mean two different things — "this reference names something that will never exist" and "this reference names something a later stage will author" — and every consumer that keys on the id, rather than on the severity, would silently inherit the ambiguity. A distinct id also lets the correction say the one thing the author actually needs, which is the name of the command that closes the gap. `unknownReference` keeps its meaning and its severity on all three upstream edges.
- CQ-003 → Widen the generator's return with an explicit, typed list of the at-fault source paths. The alternative — appending paths to an existing diagnostic's `relatedIds` next to its diagnostic ids — would leave `refresh` sniffing one untyped list for two different kinds of value, which is how the placeholder survived in the first place. The cost is nine call sites that bind and discard one extra element, which is mechanical and checked by the compiler.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [FR-003] [FR-006] [AC-001] [AC-003] [AC-006]: The `tasks.yml` -> `evidence.yml` reference edge stops blocking work-model derivation. It is the only reference in `tasks.yml` that points at an artifact a LATER lifecycle stage authors; the other three point upstream at artifacts that already exist. An unresolved DOWNSTREAM reference is an incomplete lifecycle and must derive; an unresolved UPSTREAM reference is an inconsistent one and must still block. That boundary — direction of the edge, not identity of the artifact — is the rule this work implements, and it is stated in the correction the author reads.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-001] [FR-002] [AC-001] [AC-002]: The demoted condition takes a new warning-severity diagnostic id whose artifact is `evidence.yml` — the artifact that must change — and whose `relatedIds` carry the undeclared evidence id and the citing `tasks.yml`. `unknownReference` and `workModelInconsistent` are unchanged in id, severity, message and behaviour on every other edge.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-004] [FR-005] [AC-004] [AC-005]: The work-model generation seam returns the at-fault source paths as a typed value, and `refresh` consumes it in place of its hard-coded argument. Where that list is empty, `refresh` reports that it could not attribute the blockage rather than naming an arbitrary artifact — "could not look" is never a negative verdict about a particular file (ADR-0002).
- **DEC-004** [CQ-001] [FR-003] [AC-003]: No gate is deleted. The regression coverage asserts positively that an obligation which is never declared still stops `evidence`, `verify` and `ship`, so the relocation in DEC-001 is shown to have moved the check rather than removed it.
- **DEC-005** [CQ-001] [AMB:AMB-001] [FR-006] [FR-009] [AC-006] [AC-009]: `evidence` seeds a `result: missing` skeleton into an already-authored `evidence.yml` for every obligation it declares nothing for, and reports the seeded ids rather than doing it silently — this is the one path on which SDD adds lines to a file the author owns. Together with DEC-001 this is what makes the documented sequence converge; the report's either/or is recorded as refuted rather than quietly widened, because a reader who returns to this package needs to know that one fix was measured insufficient rather than merely chosen against.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001, AMB-002 and AMB-003 are resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 869-refresh-evidence-deadlock`.
