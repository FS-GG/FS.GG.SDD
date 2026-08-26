---
schemaVersion: 1
workId: 923-quint-profile-compiler
title: Build the hermetic Quint profile and compiled-contract boundary
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/923-quint-profile-compiler/spec.md
sourceClarifications: work/923-quint-profile-compiler/clarifications.md
sourceChecklist: work/923-quint-profile-compiler/checklist.md
publicOrToolFacingImpact: true
---

# Build the hermetic Quint profile and compiled-contract boundary Plan

Prose status: planned

## Source Snapshot
- spec: work/923-quint-profile-compiler/spec.md sha256:64b1ea75a3e5dcf96bde6a00c3360f54dba902014516ef72541f9432f783a161 schemaVersion:1
- clarifications: work/923-quint-profile-compiler/clarifications.md sha256:ac81ffb17f102ff7db028dd064d36e15638afc536110634cd0a2da8a4e7a49c0 schemaVersion:1
- checklist: work/923-quint-profile-compiler/checklist.md sha256:d32d896ec48672b3c17be97438a3f5562bf35c553222cfcee1e1577c41510652 schemaVersion:1

## Plan Scope
- Work item 923-quint-profile-compiler is planned from the current specification, clarification, and checklist facts.
- Requirement count: 14.
- Clarification decision count: 4.
- Checklist result count: 14.

## Plan Decisions
- PD-001 [AC-001] [AC-002] [FR-001] [FR-009] [DEC-001] [DEC-003] complete: Add an `.fsi`-first `QuintToolchain` domain with the exact Q1 tool, source, tree, archive, evaluator, profile, and guidance identities; validate caller-supplied content-addressed cache observations through a pure plan/receipt boundary and keep absent, unreadable, incomplete, mismatched, occupied-endpoint, and failed-process diagnostics distinct.
- PD-002 [AC-001] [AC-002] [FR-002] [FR-003] complete: Add an `.fsi`-first `QuintSource` domain for canonical UTF-8 Markdown, ordered safe fence targets, generated-module receipts, and a canonical source-map codec whose public coordinates contain no host path or compiler node identity.
- PD-003 [AC-002] [AC-003] [FR-004] [DEC-002] complete: Add a private Quint 0.32.0 typed/effect JSON adapter over captured fixtures and project only the closed `fsgg-quint-profile/1` catalogue into public facts; refuse unsupported constructs, invalid identities/references, arbitrary-expression lowering, and undeclared semantics before contract construction.
- PD-004 [AC-003] [FR-005] [DEC-002] complete: Add an `.fsi`-first `QuintContract` domain and canonical JSON codec for compiled-contract v1. The closed schema carries stable catalogue, integration, compatibility, finite-bound, source-location, and semantic-digest facts and has no arbitrary-expression or raw-IR escape hatch.
- PD-005 [AC-003] [AC-006] [FR-006] [DEC-004] complete: Generate deterministic collision-refusing F# contract bindings and a Fable-compatible projection exclusively from compiled-contract v1; package the Fable project/source assets and require byte-equivalent canonical JSON and identifier ordering across runtimes.
- PD-006 [AC-003] [FR-007] complete: Add semantic fingerprint and diff APIs that bind the source, fence manifest, generated modules, exact tool/profile identities, and contract bytes while ignoring session/time/provenance noise and refusing incompatible schema or profile comparisons.
- PD-007 [AC-004] [FR-008] complete: Add an `.fsi`-first `QuintReplay` domain for generic fingerprinted ITF traces, canonical state ordering, implementation observations, and deterministic first-divergence reports; encode no product transition function or product adapter.
- PD-008 [AC-001] [AC-004] [FR-010] [DEC-003] complete: Drive package-only acceptance through an injected local process edge using already-resolved cache objects. Compile all three exact Q1 slices twice in isolated directories and replay the reviewed S.I.R. witness without a project reference, acquisition step, or network request.
- PD-009 [AC-005] [FR-011] complete: Build positive golden fixtures and independent mutations for every extraction, version, profile, contract, source-map, cache, guidance, binding, and replay failure class; each mutation must fail for its intended stable diagnostic rather than by incidental parse failure.
- PD-010 [AC-005] [FR-012] [DEC-001] complete: Package the exact reviewed Apache-2.0 `lmt` source/license identity receipt and optional guidance identity separately from canonical compilation; represent moving installers, branches, and incompatible latest-tool behavior only as refused inputs.
- PD-011 [AC-006] [FR-013] complete: Keep every public value behind authored `.fsi` declarations, pure-core/effect-edge APIs, native semantic tests, current reflection/API baselines, and Fable compile/parity coverage.
- PD-012 [AC-006] [FR-014] [DEC-004] complete: Confine Q2 to the Artifacts library, tests, reference documentation, and package content. Add no lifecycle backend, author/inspect/migrate/rollback command, manifest authority, provider/registry/default change, consumer pin, or public release.

## Contract Impact
- PC-001 [PD-001] [PD-002] [PD-003] public surface: `FS.GG.SDD.Artifacts.TypedSpecifications` gains additive `.fsi`-declared Quint toolchain, source/profile, diagnostics, and source-map contracts; exact adapter JSON remains private.
- PC-002 [PD-004] [PD-006] public surface and canonical artifact: `compiled-contract-v1` plus its canonical codec, fingerprint, and semantic-diff APIs become the stable language-neutral boundary.
- PC-003 [PD-005] package content: The Artifacts package gains deterministic generated-binding and Fable-compatible source assets derived only from compiled-contract v1.
- PC-004 [PD-007] public surface and replay artifact: Generic ITF/replay envelopes and first-divergence diagnostics become additive contracts without product semantics.
- PC-005 [PD-001] [PD-008] offline effect contract: Compilation plans name exact cache objects and local process requests; observations and receipts must bind those requests completely and no acquisition/network success exists.
- PC-006 [PD-010] package content: Exact reviewed `lmt` source/license and optional guidance identity receipts are content-addressed package assets, separate from compiler authority.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PC-001] semanticTest: Validate the exact Q1 identities, safe deterministic extraction/source maps, the accepted Quint 0.32.0 profile corpus, and source-located negative controls for every refusal class.
- VO-002 [PD-004] [PD-006] [PC-002] contractTest: Round-trip canonical compiled-contract bytes, reject unknown/duplicate/expression-bearing facts, prove deterministic semantic fingerprints, and classify only integration-meaning changes.
- VO-003 [PD-005] [PD-011] [PC-003] semanticTest: Compile native and Fable projections, compare canonical JSON and identifiers, check `.fsi`/reflection/API baselines, and inspect packed assets.
- VO-004 [PD-007] [PC-004] semanticTest: Replay the reviewed S.I.R. trace byte-identically and prove a deliberately wrong implementation observation reports the exact first divergent step, action, source binding, expected state, and actual state.
- VO-005 [PD-008] [PC-005] integrationTest: From installed package artifacts and a preseeded cache, run all Q1 slices twice in clean isolated directories, compare every output byte, and prove no network/acquisition request is representable.
- VO-006 [PD-009] mutationTest: Execute independent mutations across extraction, closure, profile, contract, source-map, guidance, bindings, and replay; require each intended stable diagnostic.
- VO-007 [PD-010] [PC-006] supplyChain: Recompute packaged source/license/identity receipts and prove moving branch/latest-tool substitution is refused.
- VO-008 [PD-012] documentationReview: Review the public surface, package entries, repository diff, and reference documentation to prove Q3 lifecycle/publication/default behavior remains absent.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] [PC-002] additive: Q2 adds library contracts and package content without changing existing `fsharp-specification-v1` meaning or persisted lifecycle schemas.
- PM-002 [PC-005] diagnoseOnly: Missing, stale, wrong-version, or incomplete offline inputs produce diagnostics and no output; compilation never acquires or repairs tools.
- PM-003 [PC-003] [PC-004] forwardOnly: Generated-binding and replay schemas begin at explicit v1 identities; incompatible schema/profile values are refused rather than heuristically migrated.

## Generated View Impact
- GV-001 [PD-001] [PD-012] workModel: `readiness/923-quint-profile-compiler/work-model.json` refreshes from the authored Q2 sources, reaches implementation-ready before production edits, and reports stale inputs rather than certifying them.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 923-quint-profile-compiler`.
