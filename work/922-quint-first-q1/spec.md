---
schemaVersion: 1
workId: 922-quint-first-q1
title: "Qualify Quint-first authoring across requirements, S.I.R., and coordination"
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Qualify Quint-first authoring across requirements, S.I.R., and coordination Specification

Prose status: specified

## User Value
Decide with reproducible cross-domain evidence whether literate Quint is acceptable as the next Typed SDD authoring authority.

## Scope
- SB-001: Non-production documents, tests, work packages, readiness receipts, and a separately owned test-only S.I.R. correspondence child.
- SB-002: The producer experiment owns a pinned literate extractor candidate, Quint compiler, closed profile proposal, stable compiled-contract proposal, ITF/replay envelope, three vertical slices, deterministic harness, and evidence report.
- SB-003: The consumer correspondence proof remains in `EHotwagner/S.I.R.#353`; this repository records only immutable request/response fingerprints and a generic toy adapter.

## Non-Goals
- SB-004: Do not change `fsharp-specification-v1`, public APIs, package versions, provider floors, lifecycle defaults, or historical P0–P4 evidence.
- SB-005: Do not implement the Q2 hermetic compiler/profile adapter, Q3 backend/publication, Q4 S.I.R. migration, or GS2 coordination product.
- SB-006: Do not expose raw Quint IR or a copied Quint expression tree as an FS-GG public contract.

## User Stories
- US-001 (P1): As a user, I can decide with reproducible cross-domain evidence whether literate Quint is acceptable as the next Typed SDD authoring authority.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-005]: Given the exact candidate bundle, when the qualification harness runs, then all three extracted module sets typecheck and their named examples/properties pass with exact receipts.
- AC-002 [US-001] [FR-002]: Given each extraction corruption class, when the harness runs, then it fails before Quint execution with a source-located diagnostic naming the violated binding.
- AC-003 [US-001] [FR-003] [FR-009]: Given the three literate documents and semantic diffs, when fresh domain and architecture/tooling critics review them without author explanation, then each records prose clarity, semantic traceability, counterexample readability, and hidden-prose findings against exact bytes.
- AC-004 [US-001] [FR-004]: Given a passing producer candidate but absent, stale, or mismatched S.I.R. evidence, when acceptance is evaluated, then the verdict remains refused.
- AC-005 [US-001] [FR-006] [FR-007]: Given the pinned upstream kit and a moving/latest substitution, when guidance fixtures run, then the pinned corpus is evaluated and the substitution is rejected before use.
- AC-006 [US-001] [FR-008]: Given injected safety, retry, stale-observation, ordering, liveness, mapping, or implementation defects, when the affected controls run, then each defect produces a non-zero, actionable first-divergence or property failure.

## Functional Requirements
- FR-001: Three literate slices typecheck and exercise positive and injected-negative cases under exact tool and source fingerprints. (Stories: US-001; Acceptance: AC-001)
- FR-002: Deterministic extraction rejects missing, reordered, duplicate, stale, and hand-edited generated modules. (Stories: US-001; Acceptance: AC-001)
- FR-003: Independent domain and architecture reviewers evaluate traceability and readability without relying on author explanation. (Stories: US-001; Acceptance: AC-001)
- FR-004: The producer and S.I.R. child agree on exact model, trace, adapter, implementation, seed, bounds, toolchain, profile, and compiled-contract fingerprints before acceptance. (Stories: US-001; Acceptance: AC-001)
- FR-005: The requirements/evidence, S.I.R.-rule, and coordination-process slices are literate Markdown with ordered named `quint` blocks; prose contains no requirement, action, invariant, evidence obligation, or binding absent from the executable catalogue. (Acceptance: AC-001)
- FR-006: The exact `quint-co/quint-llm-kit` commit and every evaluated file are content-digested, Apache-2.0 attributed, and dispositioned as adopted, adapted, or rejected; no moving installer or branch is executed. (Acceptance: AC-005)
- FR-007: The report evaluates standalone language/modeling/execution skills, witnesses, trace explanations, transition labels, type/listener coverage, Choreo versus plain Quint, and fast-run versus model-checking tiers. (Acceptance: AC-005)
- FR-008: The coordination slice covers retry, stale observation, lost update, double apply, ordering, deadlock, safety, liveness, and convergence; the S.I.R. slice defines stable actions and observable states without duplicating interpreter logic. (Acceptance: AC-006)
- FR-009: Measurements bind exact dependency identities and report source/generated/IR sizes, typecheck/run/verify time, diagnostics, reproducibility, upgrade sensitivity, semantic-diff usefulness, and reviewer readability findings. (Acceptance: AC-003)
- FR-010: The proposed profile is closed and versioned, separates production meaning from finite bounds, refuses unsupported constructs, and derives stable IDs from explicit typed catalogue data rather than compiler node IDs. (Acceptance: AC-001, AC-002)
- FR-011: The proposed language-neutral compiled contract contains only stable integration identities, source locations, relationships, verification profiles, bounded domains, digests, and compatibility metadata; it contains no arbitrary Quint expression encoding. (Acceptance: AC-001)
- FR-012: Success emits an explicit candidate verdict and exact fingerprint manifest but changes no authority; Q2 remains blocked until a separately reviewed ADR-0077 amendment accepts the authoring and fingerprint contract. (Acceptance: AC-004)

## Ambiguities
- AMB-001: Which exact literate extractor candidate is sufficiently hermetic and source-located for Q1?
- AMB-002: Which exact Quint release and backend can reproducibly exercise run, test, verify, and ITF on the available CI platforms?
- AMB-003: Does the coordination process benefit from Choreo enough to justify its extra dependency and generated-contract complexity?
- AMB-004: What minimum committed witness set lets S.I.R. implement its real-interpreter child without copying transition semantics?

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 922-quint-first-q1`.
