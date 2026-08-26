---
schemaVersion: 1
workId: 923-quint-profile-compiler
title: Build the hermetic Quint profile and compiled-contract boundary
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/923-quint-profile-compiler/spec.md
publicOrToolFacingImpact: true
---

# Build the hermetic Quint profile and compiled-contract boundary Clarifications

## Source Specification
- work/923-quint-profile-compiler/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking open: Resolve the exact offline `lmt` source/build/cache contract.
- CQ-002 [AMB:AMB-002] blocking open: Resolve ownership and versioning of the Quint typed-IR adapter.
- CQ-003 [AMB:AMB-003] blocking open: Resolve the pure/effect boundary for tool execution in Q2.
- CQ-004 [AMB:AMB-004] blocking open: Resolve the generated F#/Fable binding and Q3 boundary.

## Answers
- CQ-001 → Package the reviewed `lmt` source bytes and license/identity receipt as content in `FS.GG.SDD.Artifacts`; validate the exact Go archive and cached built binary digests. Compilation never runs `go install` or resolves a branch. A host may provision the cache separately, but the plan refuses absent or mismatched objects.
- CQ-002 → Keep `quint-0.32.0` typed JSON in an internal adapter module and captured test fixtures. Public inputs/outputs are the closed profile catalogue, source map, canonical compiled contract, diagnostics, and receipts. Another Quint version requires a distinct adapter identity and compatibility decision.
- CQ-003 → Q2 exposes a deterministic pure compilation plan plus typed observations/receipts. A narrow injected effect interpreter may execute already-resolved local binaries for package-only acceptance, but acquisition/network behavior is outside compilation and never represented as success.
- CQ-004 → Q2 generates stable contract-domain F# source plus an equivalent Fable-compatible source projection and tests both. It does not wire those bindings into lifecycle commands, manifests, scaffolding, migration, provider floors, or package publication; Q3 owns that integration.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [FR-002]: Use a content-addressed offline cache contract and package the exact reviewed `lmt` source identity; reject moving Go/Quint/tool installation.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-003] [FR-004] [FR-005]: Make the Quint 0.32.0 typed-IR reader a private adapter. Stable public authority begins at explicit catalogue/source-map facts and canonical compiled-contract v1.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-001] [FR-009] [FR-010]: Separate pure plan/validation from the local process edge. Tool output is accepted only with a complete request/observation/receipt binding and no network effect.
- **DEC-004** [CQ-004] [AMB:AMB-004] [FR-006] [FR-013] [FR-014]: Generate contract-only F#/Fable bindings in Q2 and leave lifecycle backend integration/publication entirely to Q3.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001, AMB-002, AMB-003, and AMB-004 are resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 923-quint-profile-compiler`.
