---
schemaVersion: 1
workId: 932-consumer-defined-quint-profile
title: Consumer Defined Quint Profile
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/932-consumer-defined-quint-profile/spec.md
sourceClarifications: work/932-consumer-defined-quint-profile/clarifications.md
sourceChecklist: work/932-consumer-defined-quint-profile/checklist.md
publicOrToolFacingImpact: true
---

# Consumer Defined Quint Profile Plan

Prose status: planned

## Source Snapshot
- spec: work/932-consumer-defined-quint-profile/spec.md sha256:e263837c915da8a6745f61b18b8f2a8a02d3c0225a46da5cf17443f3c02fa962 schemaVersion:1
- clarifications: work/932-consumer-defined-quint-profile/clarifications.md sha256:96fdc058311330e3d136f998141b76d30707e22fbfb6fe1d23b5c5a878f1a56e schemaVersion:1
- checklist: work/932-consumer-defined-quint-profile/checklist.md sha256:8ca82dcec78b6cc2ada4b2728ff7f91129411045b1af3f3a9f363e9b421f5995 schemaVersion:1

## Plan Scope
- Work item 932-consumer-defined-quint-profile is planned from the current specification, clarification, and checklist facts.
- Requirement count: 10.
- Clarification decision count: 6.
- Checklist result count: 10.

## Plan Decisions
- PD-001 [AC-001] [AC-004] [FR-001] [DEC-001] complete: Preserve every existing profile-1 type and member; add `QuintGeneralProfile.identity = "fsgg-quint-profile/2"`, distinct observation/export types, and explicit version dispatch rather than widening `QuintProfile.identity`.
- PD-002 [AC-001] [AC-003] [FR-002] [DEC-003] complete: Adapt exact Quint 0.32.0 profile-2 output by validating the compiler envelope plus declaration/type/effect tables and source-bound export declarations; parse only exported constant expressions and stable action effects, never require a whole-program digest or publish raw IR.
- PD-003 [AC-002] [FR-003] [DEC-002] complete: Add a closed `QuintModelValue` algebra for bool, bounded int64, string, tuple, record, variant, list, set, and map; export selectors carry module/declaration/source identity only, while recursive values come exclusively from the Quint program.
- PD-004 [AC-002] [FR-004] [DEC-004] complete: Add compiled-contract v2 and v2 binding APIs beside v1; contract v2 carries ordinal exports, promoted catalogue rows, action effects, relationships, verification profiles, bounds, impacts, compatibility, and digests, and generated sources define a domain-neutral value DU plus export table and identity literals.
- PD-005 [AC-003] [FR-005] [DEC-003] complete: Enforce 16 MiB typed/effect input, 4,096 declarations/effects/source bindings, 256 exports, 100,000 exported value nodes, depth 32, 64 KiB strings, canonical set/map ordering, unique export/catalogue identities, safe ranges, and deterministic fail-closed diagnostics for every exceeded or unknown boundary.
- PD-006 [AC-003] [FR-006] [DEC-005] complete: Add a separate general observed-compilation input/output path and receipt schema, then make the CLI effect host branch only on the manifest-declared profile; bind the same profile/schema through extraction, adaptation, contract, bindings, authority artifacts, inspection, migration, rollback, and stale-product checks.
- PD-007 [AC-004] [FR-007] [DEC-001] complete: Retain the profile-1 implementation as a frozen code path and add byte-golden tests over its three admitted typed-effect fixtures, contract JSON, generated sources, receipt, diagnostics, manifest validation, and lifecycle routes before any shared refactor.
- PD-008 [AC-005] [FR-008] [DEC-006] complete: Add a package-only consumer fixture containing the full S.I.R. literate combat model, compile twice from an exact preseeded cache with poisoned proxies, consume only packed 1.5.0 Artifacts/CLI outputs, and prove no source load, project reference, sibling checkout, or moving acquisition occurs.
- PD-009 [AC-005] [FR-009] [DEC-005] complete: Run the S.I.R. model's six named witnesses and 64×8 seeded simulation, replay generated ITF states through the production interpreter adapter, compile generated bindings natively and with Fable, and independently invert profile identity, freshness, armour semantics, suppression guard, action mapping, observable mapping, and interpreter result.
- PD-010 [AC-006] [FR-010] [DEC-006] complete: Declare all public additions in `.fsi` first, exercise them through an FSI/prelude before `.fs`, update API baselines and both packaged skill sources, document supported values/bounds/external algorithms, bump the coherent set to stable 1.5.0, and use the existing build-once dual-feed release gate after merge.

## Contract Impact
- PC-001 [PD-001] [PD-002] publicApi: Add profile-2 observation/export/value types and `QuintGeneralProfile` APIs in `QuintProfile.fsi`; all profile-1 signatures remain byte-identical.
- PC-002 [PD-003] [PD-004] publicApi: Add compiled-contract-v2 and generated-binding-v2 types/functions in `QuintContract.fsi` and `QuintBindings.fsi`, without changing v1 record shapes or serializer bytes.
- PC-003 [PD-006] publicApi: Add general observed compilation and receipt composition in `QuintCompiler.fsi` plus explicit profile dispatch facts in `TypedLifecycleV2.fsi`; manifests remain schema version 2 and name the selected profile.
- PC-004 [PD-008] [PD-010] packageRelease: Artifacts and CLI publish coherently as stable 1.5.0; installed consumers see only package APIs, generated files, and deterministic diagnostics.

## Verification Obligations
- VO-001 [PD-001] [PD-007] [PC-001] compatibilityTest: Snapshot every profile-1 public/output surface before refactoring and prove the complete existing Q1/Q2/Q3 suite stays byte-exact.
- VO-002 [PD-002] [PD-003] [PD-005] [PC-001] semanticTest: Use real Quint 0.32.0 typed/effect fixtures for each supported value/effect form and red fixtures for unknown structure, substitution, duplicate bindings, unsafe ranges, noncanonical ordering, and every resource ceiling.
- VO-003 [PD-004] [PC-002] semanticTest: Round-trip contract v2 canonical JSON, compare semantic diffs, generate native/Fable sources twice, compile both, and reject identifier/value collisions and expression-bearing payloads.
- VO-004 [PD-006] [PC-003] runtimeRoute: Drive the pure compilation composition and real CLI effect host through both explicit profiles; prove missing/wrong tools, profiles, products, process observations, and partial transactions fail atomically with stable diagnostics.
- VO-005 [PD-008] [PD-009] [PC-004] installedPackage: From a clean package-only fixture, compile the complete S.I.R. model twice, execute witnesses/simulation, build both generated bindings, replay sampled traces, and observe all seven independent mutations red.
- VO-006 [PD-010] [PC-001] [PC-002] [PC-003] [PC-004] releaseGate: Run formatting, full tests, public/dependency surface checks, package contents, skill drift, exact source anchors, fresh installs, build-once source binding, normalized dual-feed identity, tag, and durable release receipts.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] [PC-002] additiveVersioned: Profile 1 and compiled-contract v1 remain permanent readable/generatable contracts; profile 2 and contract v2 are explicit opt-in additions with no automatic reinterpretation.
- PM-002 [PC-003] explicitProfileMigration: Existing manifest-v2 authorities retain their current profile; migration to profile 2 requires an explicit preflight/transaction and authenticated rollback through the existing Q3 lifecycle.

## Generated View Impact
- GV-001 [PD-001] [PD-010] apiSurface: `docs/api-surface` mirrors all changed `.fsi` declarations and compatibility classification proves additive 1.x evolution.
- GV-002 [PD-004] [PD-006] typedAuthority: Compiled contract, generated F#/Fable bindings, compilation receipt, manifest-v2 artifact inventory, and source maps become profile-dispatched and freshness-bound.
- GV-003 [PD-010] skillViews: `skills/typed-sdd-author` and `skills/typed-sdd-migrate` plus embedded/materialized mirrors remain byte-coherent and teach both explicit profiles.
- GV-004 [PD-010] releaseReadiness: compatibility baselines, package payload manifests, release catalog, dual-feed comparison, and post-publish receipt bind stable 1.5.0.
- GV-005 workModel: `readiness/932-consumer-defined-quint-profile/work-model.json` and analysis/verify/ship views refresh from the current lifecycle sources or report stale generated evidence.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 932-consumer-defined-quint-profile`.
