---
schemaVersion: 1
workId: 924-quint-backed-typed-sdd-v2
title: Publish the Quint-backed Typed SDD v2 migration
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/924-quint-backed-typed-sdd-v2/spec.md
sourceClarifications: work/924-quint-backed-typed-sdd-v2/clarifications.md
sourceChecklist: work/924-quint-backed-typed-sdd-v2/checklist.md
publicOrToolFacingImpact: true
---

# Publish the Quint-backed Typed SDD v2 migration Plan

Prose status: planned

## Source Snapshot
- spec: work/924-quint-backed-typed-sdd-v2/spec.md sha256:5700cbd8c5d7ddb9741a18e8764558108ef69b6ee93445fd9087eb12f92640d1 schemaVersion:1
- clarifications: work/924-quint-backed-typed-sdd-v2/clarifications.md sha256:7ea3edf666f95f369924d749a57d797f5e8accf39ede4397c3a9a8877fcea401 schemaVersion:1
- checklist: work/924-quint-backed-typed-sdd-v2/checklist.md sha256:8f368e1581633afff31ceb9d222b1c479225363a9b6994c295ce10f64bc52184 schemaVersion:1

## Plan Scope
- Work item 924-quint-backed-typed-sdd-v2 is planned from the current specification, clarification, and checklist facts.
- Requirement count: 12.
- Clarification decision count: 6.
- Checklist result count: 12.

## Plan Decisions
- PD-001 [AC-001] [AC-002] [FR-001] complete: Leave the existing `TypedAuthorityManifest` and v1 decoder byte/API-stable; add separate v2 records and a `TypedAuthority = V1 | V2` discriminator whose decoder validates version/backend before any file-derived decision.
- PD-002 [AC-001] [AC-003] [FR-002] complete: Add a pure host-plan contract beside Q2 and one CLI effect interpreter that validates the local cache, reproduces/validates `lmt`, runs the pinned extractor/Quint twice, hashes all outputs, and supplies Q2 observations; process and cache facts are injectable for deterministic tests.
- PD-003 [AC-001] [AC-005] [FR-003] complete: Stage all v2 authority outputs under a sibling transaction directory, validate the complete staged manifest/receipt, then perform one rename-based commit with a pre-state recovery journal; never stream writes into the live authority.
- PD-004 [AC-002] [AC-005] [FR-004] complete: Parse only the canonical JSON payload embedded in v1 `specification.fsx`, lower the supported typed lifecycle model to fixed literate Quint templates, and return Migrated/Ambiguous/Unsupported preflight facts before staging any write.
- PD-005 [AC-002] [AC-005] [FR-005] complete: Store the complete v1 path/byte inventory in `.fsgg/typed-sdd-rollback/v1/` with canonical digests bound by manifest v2; rollback validates it, stages restoration, atomically swaps trees, then proves exact post-state.
- PD-006 [AC-001] [AC-003] [FR-006] complete: Centralize explicit authority resolution and replace direct `specification.fsx`/`dotnet fsi` assumptions in TypedSdd and shared command workflows; preserve current v1 behavior and `LifecycleLane.TypedSdd` compatibility while dispatching execution by authority backend.
- PD-007 [AC-004] [FR-007] complete: Add a closed `QuintVerificationSelector` over compiled impacts plus changed paths with five ordered rungs; union selects the strongest rung, and any unknown/schema/profile/compiler/toolchain/selector fact returns FullCorpus.
- PD-008 [AC-005] [FR-008] complete: Define stable `typedSdd.v2.*` diagnostics at the pure boundary and preserve absence/unreadable/incomplete/mismatch/tool-failure/transaction-recovery distinctions through JSON, text and rich projections.
- PD-009 [AC-003] [AC-005] [FR-009] complete: Extend the package acceptance harness with a fresh installed CLI consumer, complete local feed, fresh caches and poisoned proxies; execute author/inspect/migrate/rollback, Q2 replay and Fable parity, then prove replay/tool/network/source-reference inversions red.
- PD-010 [AC-006] [FR-010] complete: Author `.fsi` first for every new Artifacts/CLI surface, add semantic and public-surface baselines, keep changes additive on the 1.x line, and promote Artifacts plus CLI coherently from preview to stable 1.4.0.
- PD-011 [AC-006] [FR-011] complete: Build release artifacts once, bind tag/head/nuspec repository metadata and payload manifest, publish GitHub Packages then nuget.org, download both with retry, compare all entries except `.signature.p7s`, execute clean installs, and emit one durable release receipt.
- PD-012 [AC-006] [FR-012] complete: Update both packaged and embedded author/inspect/migrate skill sources plus lifecycle/reference/release docs, exact pin/attribution, and generated projections; add bounded guards proving no provider/registry/consumer/default flip enters the diff.

## Contract Impact
- PC-001 [PD-001] [PD-002] [PD-006] publicApi: additive manifest-v2, authority-dispatch, host-plan and selector contracts in `.fsi`; the manifest-v1 public API and serialized bytes remain unchanged.
- PC-002 [PD-003] [PD-004] [PD-005] commandReport: author/inspect/migrate/rollback reports gain additive v2 authority, preflight, transaction and rollback facts with stable diagnostics.
- PC-003 [PD-009] [PD-010] [PD-011] packageRelease: Artifacts and CLI form one stable 1.4.0 coherent set whose installed and dual-feed payload contracts are verified.

## Verification Obligations
- VO-001 [PD-001] [PD-006] [PC-001] compatibilityTest: Exact v1 golden inspect bytes, existing test suite, API baselines and a no-file-inference mutant remain green/red as appropriate.
- VO-002 [PD-002] [PD-003] [PD-008] runtimeRoute: Two isolated exact-cache author runs agree byte-for-byte; wrong/missing/incomplete cache, process substitution and partial-commit mutants fail with named diagnostics and unchanged live trees.
- VO-003 [PD-004] [PD-005] [PC-002] migrationTest: v1 preflight writes zero bytes, accepted migration is deterministic, ambiguous/unsupported inputs refuse, rollback restores exact inventory, and corrupt rollback material blocks without mutation.
- VO-004 [PD-007] selectorMutation: Every mapping rung has positive and inverted controls; unknown and selector/compiler/profile/toolchain changes prove FullCorpus.
- VO-005 [PD-009] [PC-003] installedPackage: Fresh-cache/no-network installed CLI and Artifacts packages execute the full lifecycle plus replay and Fable/Node parity without source/project shortcuts.
- VO-006 [PD-010] [PD-011] [PD-012] releaseGate: Full repository tests, formatting, API/dependency surfaces, package contents, guidance drift, no-Q6-authority guard, exact source anchors, both-feed normalized identity and clean public installs all pass.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] [PC-002] additiveVersioned: Manifest v1 remains a permanent readable authority; manifest v2 is opt-in for new explicit authoring and an explicit transactional migration target with authenticated rollback.

## Generated View Impact
- GV-001 [PD-001] [PD-010] apiSurface: new `.fsi` files/changes require current `docs/api-surface` mirrors and package version classification.
- GV-002 [PD-012] skillViews: packaged skill sources and embedded/materialized SDD-owned copies remain byte-coherent under their existing generators/drift guards.
- GV-003 [PD-011] releaseReadiness: release catalog, compatibility/versioning baselines, package payload manifests and post-publish receipt projections bind the stable coherent set.
- GV-004 workModel: readiness/924-quint-backed-typed-sdd-v2/work-model.json refreshes from current lifecycle sources or reports stale generated evidence.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 924-quint-backed-typed-sdd-v2`.
