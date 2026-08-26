---
schemaVersion: 1
workId: 922-quint-first-q1
title: "Qualify Quint-first authoring across requirements, S.I.R., and coordination"
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/922-quint-first-q1/spec.md
publicOrToolFacingImpact: true
---

# Qualify Quint-first authoring across requirements, S.I.R., and coordination Clarifications

## Source Specification
- work/922-quint-first-q1/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking open: Resolve source ambiguity AMB-001 before checklist.
- CQ-002 [AMB:AMB-002] blocking open: Resolve source ambiguity AMB-002 before checklist.
- CQ-003 [AMB:AMB-003] blocking open: Resolve source ambiguity AMB-003 before checklist.
- CQ-004 [AMB:AMB-004] blocking open: Resolve source ambiguity AMB-004 before checklist.

## Answers
- CQ-001 → Evaluate `github.com/driusan/lmt` at commit `62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c` using the kit-aligned Go 1.24.1 toolchain. The commit has no release artifact or `go.mod`; Q1 may qualify its behavior but Q2 must supply a reviewed content-addressed build closure or reject it.
- CQ-002 → Pin Quint `v0.32.0`; on Linux amd64 use release asset digest `sha256:939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f`. Exercise TypeScript and Rust simulation where supported, and record Apalache/JRE model-checking as a separately cached tier rather than silently downloading it.
- CQ-003 → Use plain Quint for Q1 coordination. The process models one authority/receipt state machine rather than communicating network peers; Choreo adds message topology and supply-chain cost without improving the demonstrated compiled-contract boundary. Revisit only if a later canonical model needs explicit peer messaging.
- CQ-004 → Commit one reviewed positive witness and at least one smallest failing-prefix mutation per slice. The S.I.R. child consumes the exact stable action/observation vocabulary and fingerprints, while larger sampled traces remain CI artifacts.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-002] [FR-005]: Q1 evaluates exact upstream `lmt` commit `62fe18f…`; generated targets and a source-map manifest are bound to ordered fence bytes, and missing/reordered/duplicate/stale/edited cases must fail.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-001] [FR-009]: Q1 pins Quint v0.32.0 and exact platform asset digests. A different version or digest is a distinct candidate, never an automatic upgrade.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-007] [FR-008]: The coordination slice uses plain Quint. Choreo is explicitly rejected for this slice after a dependency/readability comparison, not ignored.
- **DEC-004** [CQ-004] [AMB:AMB-004] [FR-004] [FR-008]: The committed witness contract is a stable action sequence plus expected observable states and exact fingerprints; it contains no adapter implementation or copied combat rule.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001, AMB-002, AMB-003, and AMB-004 are resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 922-quint-first-q1`.
