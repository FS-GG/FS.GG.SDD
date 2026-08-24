---
schemaVersion: 1
workId: typed-specification-kernel-p2
title: Typed Specification Kernel P2
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/typed-specification-kernel-p2/spec.md
sourceClarifications: work/typed-specification-kernel-p2/clarifications.md
sourceChecklist: work/typed-specification-kernel-p2/checklist.md
publicOrToolFacingImpact: true
---

# Typed Specification Kernel P2 Plan

Prose status: planned

## Source Snapshot
- spec: work/typed-specification-kernel-p2/spec.md sha256:e5661d8470b325bfc8861fff4e8d93afa1d3e4796b2451cb6571abf3880e0b2b schemaVersion:1
- clarifications: work/typed-specification-kernel-p2/clarifications.md sha256:a0ed95dfdeb5295b5e3ef77741d0ba8f71611417f4aa7ba2ffd412131882df9f schemaVersion:1
- checklist: work/typed-specification-kernel-p2/checklist.md sha256:2ca19d14a82e73f3f8b27241da7644b940eae892e07990b559ded33357bb0771 schemaVersion:1

## Plan Scope
Tier 1 additive API in `FS.GG.SDD.Artifacts` on .NET 10. The feature adds pure
F# model/compiler/codec/projection/migration modules and tests; it changes no CLI
state machine or existing persisted lifecycle artifact.

Public signatures are authored first in
`TypedSpecifications/SpecificationKernel.fsi` and
`TypedSpecifications/RequirementsExtension.fsi`. Semantic tests compile only
through those signatures before the `.fs` implementations land.

The generic seam is `SpecificationModel<'extension>` paired with one explicit
`ExtensionContract<'extension>`. A consuming domain owns its concrete extension
type and contract, so static typing composes without `obj`, runtime reflection,
or an FS-GG-wide closed union. A model contains one domain extension value; a
domain needing composition defines its own product/DU and contract.

Canonical binary framing and deterministic JSON use BCL primitives already in
the package. No new runtime dependency is introduced. Migration uses a bounded
line-oriented parser for the current Standard SDD heading/list grammar and
returns a value only; accepting/writing that value remains a caller action.

Constitution check: principles I-III are satisfied by spec → `.fsi` → public
semantic tests → `.fs` and committed API baselines. Principle IV is satisfied by
records, DUs, generic functions, and explicit contracts. Principle V does not
require MVU because every new operation is pure. Principles VI-VIII are covered
by mutation controls, shared authoring guidance, and stable distinct diagnostics.

## Plan Decisions
- PD-001 [AC-001] [FR-001] [DEC-002] complete: Declare private-constructed `SpecificationId`, provenance, evidence-obligation, diagnostic, source-location, and generic `SpecificationModel<'extension>` records; semantic identity excludes author/session/time/intent exactly as clarified.
- PD-002 [AC-001] [FR-002] [DEC-001] complete: Declare `ExtensionContract<'extension>` with explicit kind/version, validate, canonical-encode, decode, Markdown projection, and evidence extraction functions; compiler functions accept the concrete contract directly and never store heterogeneous extensions.
- PD-003 [AC-002] [FR-003] [DEC-002] complete: Normalize by length-framing the fixed envelope and contract-owned canonical extension bytes, sorting evidence obligations by ID, then hash the bytes with lowercase SHA-256.
- PD-004 [AC-002] [FR-004] complete: Emit deterministic schema-v1 JSON in fixed property order with the extension as contract-owned JSON; decode through `JsonDocument`, reject unknown envelope fields and unsupported versions, and delegate only the `extension` element.
- PD-005 [AC-003] [FR-005] complete: Compare normalized semantic components and return `Equivalent` or path-addressed changes for identity, schema, authoritative source, evidence, and extension; author/session/time/intent changes remain equivalent.
- PD-006 [AC-004] [FR-006] complete: Model requirements as stable IDs over user value, scope boundaries, user stories, requirements, acceptance criteria, ambiguity/decision state, and evidence bindings, keeping every relationship inspectable.
- PD-007 [AC-004] [FR-007] complete: Accumulate envelope and extension findings, detect duplicate IDs and unresolved references, and sort/distinct diagnostics by path/code/message so one repair pass sees every detectable problem.
- PD-008 [AC-005] [FR-008] [DEC-003] complete: Parse only current Standard SDD front matter, required headings, and stable-ID list grammar; return `Migrated`, `Ambiguous`, or `Unsupported` with line/column-bearing reasons and expose no write function.
- PD-009 [AC-006] [FR-009] complete: Derive deterministic Markdown and JSON projection receipts from the normalized model, stamping schema, model ID, source fingerprint, and generated-body fingerprint.
- PD-010 [AC-006] [FR-010] complete: Validate `Missing`, `Unreadable`, or content observations and emit distinct stable failures for missing, unreadable, malformed markers/JSON, unsupported projection version, stale source fingerprint, and direct-edited generated body.
- PD-011 [AC-007] [FR-011] complete: Match evidence receipts to obligation IDs and expected kinds, returning satisfied IDs plus stable missing, duplicate, unknown, and kind-mismatch diagnostics without importing Governance.
- PD-012 [AC-008] [FR-012] [DEC-001] complete: Add the modules only to `FS.GG.SDD.Artifacts`, retain its existing BCL/YamlDotNet/FS.GG.Contracts dependency closure, and prove clean consumption from the packed nupkg.
- PD-013 [AC-008] [FR-013] complete: Add public-surface, validation aggregation, authoring equivalence, codec round-trip, semantic-noise, migration, projection, evidence, package-isolation, and deliberate inversion tests in the existing Artifacts test project.
- PD-014 [AC-009] [FR-014] complete: Extend the byte-identical Claude/Codex authoring-contract skill with the typed inspect/question/proposal/diff/evidence loop and regenerate the skill-manifest digest from its canonical source.
- PD-015 [AC-010] [FR-015] [DEC-004] complete: Bump the coherent producer version to `1.3.0-preview.1`, update release readiness/compatibility records, pack from the candidate, and declare publication plus clean-feed consumption as post-merge delivery obligations.
- PD-016 [AC-010] [FR-016] complete: Gate the public surface and package contents against `SIR`, `RuleDefinition`, `RuleSpecification`, gameplay, and registered-algorithm symbols; the requirements extension is the only P2 domain extension.
- PD-017 [AC-010] [FR-017] complete: Keep all existing command reports, schemas, defaults, and lifecycle artifacts byte-compatible; only new public types/docs/tests and the coherent preview version are added.

## Contract Impact
- PC-001 [PD-001] [PD-002] publicApi: New `.fsi` contracts under `FS.GG.SDD.Artifacts.TypedSpecifications`; API baselines and XML docs are authoritative public-surface records.
- PC-002 [PD-004] [PD-008] schema: Specification JSON, projection receipts, and migration outcomes are versioned v1 contracts with fail-closed unsupported-version behavior.
- PC-003 [PD-015] package: `FS.GG.SDD.Artifacts@1.3.0-preview.1` is publish-before-flip authority; downstream registry and S.I.R updates may reference it only after feed verification.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PC-001] semanticTest: Compile an F# consumer prelude through the `.fsi` surface before implementation and then run the same prelude against the built assembly.
- VO-002 [PD-003] [PD-004] [PD-005] propertyTest: Prove equivalent authoring bytes, deterministic repeat output, supported codec round-trip, authoring-noise equivalence, and semantic-change detection.
- VO-003 [PD-006] [PD-007] [PD-011] semanticTest: Exercise every reference/error class and invert duplicate/reference/evidence guards to record named red controls.
- VO-004 [PD-008] [PD-009] [PD-010] fixtureTest: Use real Markdown/projection fixtures for migrated, ambiguous, unsupported, missing, unreadable, malformed, stale, direct-edit, and wrong-version paths.
- VO-005 [PD-012] [PD-013] [PD-016] packageTest: Pack the exact candidate, restore a clean consumer using only that nupkg, execute it, and assert the dependency/package/public-symbol census excludes S.I.R and coordination.
- VO-006 [PD-014] [PD-017] compatibilityTest: Verify Claude/Codex skill bytes and manifest digest, existing Artifacts tests, full solution build/test, API surface, and unchanged command/schema fixtures.
- VO-007 [PD-015] [PC-003] releaseTest: After merge, publish to required feeds, compare/download exact artifacts, run clean public consumption, then update the contract registry in a separately claimed producer/registry item.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-002] additive: Existing Standard SDD Markdown remains authoritative and unchanged; the new adapter performs read-only analysis and reports explicit typed outcomes.
- PM-002 [PC-002] noGuess: Unknown semantic headings, unresolved references, and unsupported versions never become canonical defaults; they retain typed reason and source location.
- PM-003 [PC-003] publishBeforeFlip: Preview publication is reversible by retaining existing package pins; no consumer source is rewritten in P2.

## Generated View Impact
- GV-001 [PD-009] specificationProjection: New Markdown/JSON views are derived from normalized models and fail currency checks independently for stale source and direct edits.
- GV-002 [PD-014] agentGuidance: Claude and Codex authoring-contract skills remain byte-identical and the tracked skill manifest receives the new canonical digest.
- GV-003 [PD-015] releaseReadiness: Compatibility matrix, release-readiness JSON, its test baseline, API surface, and SDD readiness views reflect the coherent preview identity.

## Accepted Deferrals
- None. P3 re-adoption, P4 lifecycle selection, and M-series extensions are
  separately ordered roadmap milestones, not unfinished P2 requirements.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- No Governance runtime is required; Governance may later evaluate evidence
  emitted through an explicit optional boundary.
- `FS.GG.SDD.sln` needs no project addition because DEC-001 uses the existing
  Artifacts package and test project.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work typed-specification-kernel-p2`.
