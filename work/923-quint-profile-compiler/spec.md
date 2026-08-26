---
schemaVersion: 1
workId: 923-quint-profile-compiler
title: Build the hermetic Quint profile and compiled-contract boundary
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Build the hermetic Quint profile and compiled-contract boundary Specification

Prose status: specified

## User Value
A package-only consumer can deterministically compile accepted literate Quint into a stable FS-GG contract and replay envelope without a source-project shortcut or network install.

## Scope
- SB-001: The exact Q1-accepted `fsgg-quint-profile/1`, extractor/Quint/tool closure, deterministic source mapping, stable compiled contract, generated F#/Fable bindings, ITF/replay codecs, diagnostics, semantic diff, and golden positive/negative fixtures.
- SB-002: Public APIs live in `FS.GG.SDD.Artifacts`; the pinned Quint JSON IR remains an internal adapter input and is represented in tests only by captured exact-version fixtures.
- SB-003: The compiler consumes caller-supplied content-addressed offline cache entries. Provisioning may populate that cache outside compilation, but compilation itself performs no network access or moving install.

## Non-Goals
- SB-004: Do not implement or publish the `quint-specification-v1` lifecycle backend, manifest-v2 author/inspect/migrate/rollback commands, package release, registry/provider floors, or workspace default; Q3 owns them.
- SB-005: Do not embed product business logic or product adapters, expose raw Quint IR, serialize arbitrary Quint expressions, or create a second transition semantics.
- SB-006: Do not reinterpret or retire existing `fsharp-specification-v1` authorities or modify historical Q1/P0-P4 evidence.

## User Stories
- US-001 (P1): As a package consumer, I can compile one accepted literate Quint document into deterministic generated modules and a stable FS-GG contract using only verified offline inputs.
- US-002 (P1): As an author, I receive source-located diagnostics at the literate Markdown binding for unsupported profile constructs, wrong versions, stale generation, and malformed catalogues.
- US-003 (P1): As a product maintainer, I can decode fingerprinted ITF traces through a generic replay envelope and report the first model-observable divergence without importing product logic into FS.GG.SDD.
- US-004 (P2): As a reviewer, I can compare semantic fingerprints and generated F#/Fable bindings without depending on compiler-owned Quint node identities.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002] [FR-010]: Given a clean package-only fixture, a complete preseeded cache, and each Q1 literate slice, when compilation runs twice in isolated directories, then generated modules, source maps, contract bytes, bindings, and receipts are byte-identical and no network process is requested.
- AC-002 [US-002] [FR-003] [FR-004] [FR-009]: Given a wrong tool/profile/schema version, missing cache object, incomplete Apalache tree, occupied server endpoint, unsupported Quint construct, unsafe fence target, or stale source map, when validation runs, then it fails closed with a stable code and literate source location naming the violated binding.
- AC-003 [US-001] [US-004] [FR-005] [FR-006] [FR-007]: Given accepted compiler output, when the adapter compiles it, then canonical contract bytes contain only stable catalogue/integration facts, generated F# and Fable bindings agree, and semantic diff ignores provenance noise while reporting every integration-meaning change.
- AC-004 [US-003] [FR-008]: Given the reviewed S.I.R. witness and a deliberately divergent observation, when the generic ITF/replay codec evaluates them, then the positive trace preserves exact identity and the mutation reports the first divergent step, action, source binding, expected state, and observed state.
- AC-005 [US-001] [US-002] [FR-011] [FR-012]: Given independently authored mutations for extraction, versioning, profile, contract, source-map, cache, guidance, replay, and binding classes, when the Q2 harness runs, then every mutation fails for its intended diagnostic class and the unmodified Q1 corpus passes.
- AC-006 [US-004] [FR-013] [FR-014]: Given a package build and public-surface review, when native and Fable contract suites run, then their canonical JSON and identifier projections agree, `.fsi` baselines are current, and no Q3 backend/publication/default behavior is present.

## Functional Requirements
- FR-001: The library MUST represent the exact extractor, Go, Quint, evaluator, JRE, Apalache, Node/Ajv, guidance, and profile identities as a canonical toolchain manifest and validate every file/tree/closure digest before use. (Stories: US-001; Acceptance: AC-001, AC-002)
- FR-002: The extraction boundary MUST accept one canonical UTF-8 Markdown source plus an ordered fence manifest, require plain safe `.qnt` targets, treat warnings as errors, bind source/fence/module bytes, and prove two isolated extractions agree. (Stories: US-001, US-002; Acceptance: AC-001, AC-002)
- FR-003: A deterministic source-map codec MUST map generated module ranges and diagnostics back to canonical Markdown path/line/column ranges without publishing compiler node ids or host paths. (Stories: US-002; Acceptance: AC-002)
- FR-004: The profile validator MUST accept only the Q1-demonstrated `fsgg-quint-profile/1` subset and catalogue rules, validate exact Quint 0.32.0 typed/effect output, and reject unknown constructs, arbitrary-expression lowering, invalid ids/references, and undeclared semantics with source-located diagnostics. (Stories: US-002; Acceptance: AC-002)
- FR-005: The compiled-contract v1 codec MUST emit canonical language-neutral bytes containing stable identities, source locations, reads/writes/subjects, relationships, verification profiles, finite bounds, impact/compatibility metadata, and semantic digests, with no arbitrary Quint expression or raw IR field. (Stories: US-001, US-004; Acceptance: AC-003)
- FR-006: Generated F# and Fable bindings MUST derive only from compiled-contract v1, use deterministic collision-refusing identifiers and ordering, and project byte-equivalent canonical JSON for the same contract. (Stories: US-004; Acceptance: AC-003, AC-006)
- FR-007: The semantic fingerprint/diff API MUST bind source, fence manifest, generated modules, tool/profile identities, and contract bytes; it MUST classify stable integration changes independently of provenance/session/time noise and refuse incompatible schema/profile comparisons. (Stories: US-004; Acceptance: AC-003)
- FR-008: The generic ITF/replay API MUST validate trace/action/state identity, seed/bounds/tool/profile/contract digests, adapter and implementation fingerprints, canonical state ordering, and first-divergence diagnostics without containing product transition logic. (Stories: US-003; Acceptance: AC-004)
- FR-009: Diagnostics MUST use stable codes, deterministic ordering, JSON-pointer or literate source paths, expected/actual version or digest, and actionable correction; absence, unreadability, mismatch, unsupported input, and tool failure MUST remain distinct. (Stories: US-002; Acceptance: AC-002)
- FR-010: A package-only acceptance fixture MUST compile all three exact Q1 slices and replay the reviewed S.I.R. witness from installed artifacts with a preseeded offline cache, no source-project reference, no network install, and byte-identical repeated output. (Stories: US-001, US-003; Acceptance: AC-001, AC-004)
- FR-011: The verification harness MUST include independent negative controls for missing/reordered/duplicate/stale fences, unsafe targets, hand-edited modules, wrong/incomplete tool closure, latest/moving guidance, wrong Quint/profile/schema, unsupported IR, malformed/duplicate contract facts, stale source maps, identifier collisions, replay mismatch, and hidden product semantics. (Stories: US-002; Acceptance: AC-005)
- FR-012: The exact Apache-2.0 guidance snapshot MUST remain separately content-addressed and optional; runtime-specific orchestration, moving installers, and incompatible latest-Quint behavior MUST not enter canonical compilation. (Stories: US-001; Acceptance: AC-005)
- FR-013: Every public Q2 type/function MUST be declared in `.fsi`, have native semantic tests and current surface baselines, and preserve the repository's pure-core/effect-edge boundary. (Stories: US-004; Acceptance: AC-006)
- FR-014: Q2 MUST change no lifecycle backend identity, author/inspect/migrate/rollback command, manifest authority, public package version, provider floor, registry row, consumer source, or default; it produces implementation-ready library contracts for Q3 publication. (Stories: US-004; Acceptance: AC-006)

## Ambiguities
- AMB-001: How is the untagged, module-less `lmt` source made reproducible without embedding a moving Go install?
- AMB-002: Which boundary consumes Quint's unstable typed JSON IR without making that IR a public or committed authority?
- AMB-003: Does Q2 execute tools directly, or define a pure compilation plan interpreted by a later host?
- AMB-004: Which generated binding surface belongs in the packable artifact without prematurely creating the Q3 lifecycle backend?

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 923-quint-profile-compiler`.
