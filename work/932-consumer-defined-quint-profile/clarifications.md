---
schemaVersion: 1
workId: 932-consumer-defined-quint-profile
title: Consumer Defined Quint Profile
stage: clarify
changeTier: tier1
status: needsAnswers
sourceSpec: work/932-consumer-defined-quint-profile/spec.md
publicOrToolFacingImpact: true
---

# Consumer Defined Quint Profile Clarifications

## Source Specification
- work/932-consumer-defined-quint-profile/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking open: Resolve source ambiguity AMB-001 before checklist.
- CQ-002 [AMB:AMB-002] blocking open: Resolve source ambiguity AMB-002 before checklist.
- CQ-003 [AMB:AMB-003] blocking open: Resolve source ambiguity AMB-003 before checklist.
- CQ-004 [AMB:AMB-004] blocking open: Resolve source ambiguity AMB-004 before checklist.
- CQ-005 [AMB:AMB-005] blocking open: Resolve source ambiguity AMB-005 before checklist.
- CQ-006 [AMB:AMB-006] blocking open: Resolve source ambiguity AMB-006 before checklist.

## Answers
- CQ-001 → add `fsgg-quint-profile/2` and `fsgg-quint-contract/2`; profile 1 and contract 1 remain frozen and dispatch is always explicit.
- CQ-002 → the host supplies source-bound export selectors, while all exported values remain authored in Quint; the closed value vocabulary is bool, int, string, tuple, record, variant, list, set, and map with canonical recursive ordering.
- CQ-003 → profile 2 validates the exact compiler/version envelope, declaration/type/effect tables, source-bound exports, resource ceilings, and exported-value expressions; it does not admit a whole-program digest and does not publish raw expression IR.
- CQ-004 → bindings expose generic generated `QuintValue` and `QuintExport` data plus stable identity literals and canonical JSON, allowing a domain-owned adapter without embedding domain types in SDD.
- CQ-005 → the contract records named verification declarations and finite bounds; test, simulation, and optional model-check execution remain explicit hermetic host/consumer evidence with separate receipts.
- CQ-006 → this item targets the stable 1.5.0 Artifacts/CLI coherent set after merge and requires the installed-package S.I.R. fixture, runtime correspondence, negative controls, and dual-feed verification before Done.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [FR-007]: Introduce profile 2 and compiled-contract 2 as additive explicit identities; never reinterpret or rewrite profile/contract 1.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-003] [FR-004]: Select named exports through source-bound host facts and project their Quint-authored values through a closed recursive canonical value algebra; selectors identify content but carry no semantic values.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-002] [FR-005]: Replace profile 1's exact program allow-list only for profile 2 with exact-version structural tables, export-expression validation, bounded counts/depth/bytes, and fail-closed unknown-field/effect diagnostics.
- **DEC-004** [CQ-004] [AMB:AMB-004] [FR-004]: Generate one domain-neutral value algebra and export table byte-identically for F# and Fable, retaining literal ids and canonical contract JSON for adapter and audit use.
- **DEC-005** [CQ-005] [AMB:AMB-005] [FR-006] [FR-009]: Compile verification intent and bounds as data, while actual Quint test/run/verify execution is an explicitly selected, separately receipted effect-edge obligation.
- **DEC-006** [CQ-006] [AMB:AMB-006] [FR-008] [FR-009] [FR-010]: Publish stable 1.5.0 only after the package-only S.I.R. consumer, native/Fable bindings, runtime replay, mutations, full repository gates, and normalized dual-feed identity pass from the accepted merge.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. All six source ambiguities are resolved by DEC-001 through DEC-006.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 932-consumer-defined-quint-profile`.
