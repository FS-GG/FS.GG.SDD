---
schemaVersion: 1
workId: 924-quint-backed-typed-sdd-v2
title: Publish the Quint-backed Typed SDD v2 migration
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/924-quint-backed-typed-sdd-v2/spec.md
publicOrToolFacingImpact: true
---

# Publish the Quint-backed Typed SDD v2 migration Clarifications

## Source Specification
- work/924-quint-backed-typed-sdd-v2/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001]: Which additive public API preserves the existing v1 F# record and callers?
- CQ-002 [AMB:AMB-002]: How does the CLI resolve exact tools without shipping or acquiring moving executables?
- CQ-003 [AMB:AMB-003]: What exact rollback authority must migration retain and authenticate?
- CQ-004 [AMB:AMB-004]: What closed change classes select the five CI verification rungs?
- CQ-005 [AMB:AMB-005]: How can both-feed identity remain exact when nuget.org adds a signature entry?
- CQ-006 [AMB:AMB-006]: Which release actions occur only after merge and how does the item remain incomplete until they finish?

## Answers
- CQ-001 → keep the existing v1 record and functions unchanged; add separate v2 authority types plus an explicit discriminated decoder/dispatcher.
- CQ-002 → require a caller-selected local content-addressed cache, validate Q1/Q2 manifest identities, and build/validate the qualified `lmt` only from the pinned source/toolchain; no command performs acquisition.
- CQ-003 → retain every v1 path and byte plus a canonical inventory digest inside an authenticated rollback directory referenced by manifest v2; rollback validates before and after its atomic replacement.
- CQ-004 → use prose-only, structural/typecheck, test/simulation, model-check and full-corpus rungs; any unknown, selector, schema, profile, compiler or toolchain input broadens to full-corpus.
- CQ-005 → compare every ZIP entry name and byte except `.signature.p7s`, while separately recording the pre-push nupkg SHA and feed download metadata.
- CQ-006 → the feature PR implements and tests release automation; only the independently accepted merge may be tagged/published, and delivery obligations keep the item open until both feeds and public installs are verified.

## Decisions
- DEC-001 [CQ-001] [AMB:AMB-001] [FR-001] [FR-010]: Preserve the manifest-v1 public surface unchanged and introduce separate manifest-v2 authority types with an explicit version/backend dispatcher; record growth on the v1 F# record is forbidden.
- DEC-002 [CQ-002] [AMB:AMB-002] [FR-002] [FR-006]: The lifecycle receives an explicit local cache root, validates exact objects and executes only the pinned tool plan; tool acquisition, a bundled moving binary and hidden F# fallback are forbidden.
- DEC-003 [CQ-003] [AMB:AMB-003] [FR-004] [FR-005]: Migration snapshots and authenticates the complete v1 authority before commit; preflight is read-only and rollback is one fail-safe tree transaction that restores exact bytes and deletes v2 outputs.
- DEC-004 [CQ-004] [AMB:AMB-004] [FR-007]: Selective CI is a closed fail-safe mapping over compiled-contract impacts plus changed authority surfaces; unclassified and compiler-sensitive facts always select the full Q1 corpus and replay/parity suite.
- DEC-005 [CQ-005] [AMB:AMB-005] [FR-011]: Feed equivalence means identical ordered non-signature ZIP entries and bytes, with `.signature.p7s` the sole permitted feed-added difference and every exclusion named in the receipt.
- DEC-006 [CQ-006] [AMB:AMB-006] [FR-011]: Stable publication is a post-merge delivery obligation bound to the accepted merge/tag; the item cannot receive Done until feed readback and clean installed-consumer receipts exist.

## Accepted Deferrals
- None.

## Remaining Ambiguity
- None. All six source ambiguities are resolved by DEC-001 through DEC-006.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 924-quint-backed-typed-sdd-v2`.
