# Implementation Plan: Release and Distribution Readiness

**Branch**: `018-release-readiness` | **Date**: 2026-06-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/018-release-readiness/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/018-release-readiness`.

## ✅ Implementation Status — SHIPPED (2026-06-21)

> Legend: 🟢 done & verified · 🟡 in progress · 🔴 blocked · ⚪ not started

| Slice | State | Evidence |
|---|---|---|
| 🟢 **Foundational** — single `<Version>` source + reconciled generator version + `ReleaseContract` `.fsi`/`.fs` | **DONE** | `Directory.Build.props <Version> = 0.2.0`; `currentGeneratorVersion` derives from assembly informational version; surface baseline updated. |
| 🟢 **US1 (P1/MVP)** — version identity + compatibility matrix + versioning policy | **DONE** | `docs/release/release-readiness.json` (`identity.version 0.2.0`, `channel preRelease`); `docs/release/versioning-policy.md`, `compatibility-matrix.md`. |
| 🟢 **US2 (P2)** — schema-reference catalog (10 outputs) + conformance | **DONE** | `catalog[]` covers all 7 `GeneratedViewKind` + `--json` report; `ReleaseConformanceTests` proves produced artifacts conform. |
| 🟢 **US3 (P2)** — locked baselines + pure readiness check + determinism | **DONE** | golden `release-readiness.json` baseline; `evaluate` check; byte-identity double-run tests. |
| 🟢 **US4 (P3)** — `dotnet tool` CLI + `--version` + migration-note machinery | **DONE** | `PackAsTool`/`ToolCommandName=fsgg-sdd`; `fsgg-sdd --version → 0.2.0`; `dotnet pack → DotnetTool` package; `docs/release/migrations/{README,TEMPLATE}.md`. |
| 🟢 **Boundary & validation** — Governance-exclusion + no-scope-creep + full suite | **DONE** | `ReleaseBoundaryTests`; `dotnet test -c Release` → **368 passed / 0 failed**. |

**All 26 tasks complete** — see [tasks.md](tasks.md) and the evidence transcript
[readiness/evidence.md](readiness/evidence.md). This release is **additive / minor**
(adds public surface, breaks no existing contract) ⇒ no migration note
(`migrations[] = []`).

## 🔗 Governance integration status (FS.GG.Governance, as of 2026-06-21)

The SDD→Governance seam this feature documents (the optional
`governanceContractVersionRange` compatibility fact) is current and **one-directional**:

| Aspect | State | Detail |
|---|---|---|
| 🟢 Handoff contract accepted | **v1.0.0** | Governance ADR `docs/decisions/0002-sdd-governance-handoff-contract.md` (Accepted 2026-06-20) pins `contractVersion 1.0.0` / `schemaVersion 1`, confirming `deferred → skipped` with **no version bump**. SDD's `compatibility[].governanceContractVersionRange = "1.x"` is consistent. |
| 🟢 Governance shipped through | **F021** | Latest merged feature: deterministic `gates.json` projection (merge `3f093af`). **F022 `fsgg route`** (first composed host edge) is in progress / uncommitted. 21 features shipped; repo mid-Phase 2. |
| 🔴 Handoff **consumer** wiring | **QUEUED — not implemented** | `grep` over Governance `src/`+`tests/` returns **zero** references to `governance-handoff`. No handoff reader, no `Evidence.build` adapter, no gate/fence decision consuming `readiness/<id>/governance-handoff.json` exists. All four consumer work items are tracked queued in ADR 0002, gated behind unstarted **Phase 5** (enforcement / effective-evidence). |
| 🟢 SDD-side boundary holds | **DONE** | SDD emits declared facts only; no Governance gate/route/profile/freshness vocabulary in any produced artifact (`ReleaseBoundaryTests` / `SC-008`). SDD builds, tests, packs, and installs with **no Governance runtime** present. |

**Narrative:** FS.GG.Governance has shipped through **F021** (deterministic
`gates.json`; merge `3f093af`), with **F022 `fsgg route`** the first composed host
edge in progress. Governance has **accepted the handoff contract at v1.0.0** (ADR
0002, 2026-06-20) with the `deferred → skipped` mapping and no `contractVersion`
bump. However the **consumer** of `readiness/<id>/governance-handoff.json` remains
**unimplemented** — no handoff reader, `Evidence.build` adapter, or gate/fence
decision exists in Governance `src/` (grep-confirmed zero references); the four
consumer work items stay queued in ADR 0002 behind unstarted Phase 5
enforcement/effective-evidence work. The seam is therefore validated against stable
Governance target shapes but not yet round-tripped — it stays one-directional and
optional, exactly as this SDD-owned feature requires. (The vendored snapshot at
`docs/reference/FS.GG.Governance/` is design-docs-only from 2026-06-18 and predates
all of this — the sibling repo is authoritative.)

## Summary

Deliver the SDD-owned slice of **Phase 13: Release And Distribution Readiness**.
Every SDD lifecycle phase (artifact model through the 017 Governance handoff) is
shipped and deterministic; this feature *freezes, versions, documents, and locks*
the public contracts that result, without adding a lifecycle stage or changing
any authored-source schema (FR-013).

It delivers four things:

1. **A versioning policy + machine-readable version/compatibility identity**
   (US1). A single semantic-version source of truth for the `FS.GG.SDD.*`
   packages and the `fsgg-sdd` CLI, a documented map from change-class
   (additive vs breaking, across schema/command/CLI surfaces) to a bump rule,
   and a structured `release-readiness.json` contract carrying the release's
   version identity plus a compatibility matrix (supported Spec Kit range and
   *optional* Governance handoff `contractVersion` range).

2. **An authoritative schema reference** (US2) for every public generated
   readiness view (`work-model.json`, `analysis.json`, `verify.json`,
   `ship.json`, `governance-handoff.json`, `summary.md`, and the
   `agent-commands/` outputs) and every public `--json` command-output report:
   schema version, field inventory, determinism guarantee, and per-contract
   stability classification — authored as a *projection* of the structured
   contracts, never a second source of truth (FR-005).

3. **Locked public-contract baselines + a release-readiness check** (US3). Golden
   fixtures over public schemas, representative produced artifacts, and `--json`
   reports — alongside the existing public-`.fsi` surface baselines — plus a
   coverage check that reports any public surface lacking a schema entry or a
   baseline as *not-ready* (FR-012), and a conformance check that makes
   docs-vs-reality drift a detectable failure (FR-015).

4. **Installation + migration documentation** (US4). `dotnet tool`-based install
   guidance that takes a new user from nothing to `fsgg-sdd ship` with no prior
   FS.GG knowledge and no Governance runtime, plus a migration-note obligation
   and template for backward-incompatible releases.

The headline design artifact is the **release contract**
([contracts/release-readiness.md](contracts/release-readiness.md)): the SDD-owned
schema for the version identity, compatibility matrix, and schema-reference
catalog. The new public F# surface is a small pure projection module
(`ReleaseContract`) plus serialization; there is **no new MVU workflow** (the
release-readiness evaluation is a pure fold over already-produced artifacts,
hosted by tests rather than a runtime lifecycle command — see Constitution
Check V). The only build-surface change is making the CLI a proper .NET tool
(`PackAsTool` / `ToolCommandName = fsgg-sdd`) and centralizing the package
version. This feature is **entirely SDD-owned**: it defines, computes, or
enforces **no** Governance release gate, route, profile, freshness, or
publish/provenance rule (FR-014); Governance compatibility appears only as an
optional integration fact.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0` (`Directory.Build.props`,
deterministic build already enabled).

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts` (`SchemaVersion`,
`GenerationManifest`, `WorkModel`, `GovernanceHandoff`, `ArtifactRef`,
`Diagnostics`, canonical `Serialization`) and `FS.GG.SDD.Commands`
(`CommandReports`, `CommandSerialization`, `CommandTypes.CommandReport`). No new
package dependency. **No compile-time dependency on any FS.GG.Governance
package** — the Governance handoff `contractVersion` range is recorded as a
declared string fact, not by referencing Governance code (CLAUDE.md "explicit,
versioned, optional"; Constitution engineering constraints).

**Storage**: Filesystem only. The release contract is authored as a checked-in
machine artifact (`docs/release/release-readiness.json`) and validated by tests;
golden baselines live under `tests/` next to the existing
`PublicSurface.baseline` fixtures. No new runtime write path through the command
effect interpreter (deliberate — see Constitution Check V).

**Schema/Migration**: New SDD-owned schema `release-readiness.json`,
`schemaVersion = 1`. It carries the package/CLI **semantic version** and a
**compatibility matrix** (Spec Kit range, optional Governance handoff
`contractVersion` range) and a **schema-reference catalog** of every public
contract with its `schemaVersion`/`contractVersion` and stability class. Existing
generated-view and authored-source schemas are **unchanged** (FR-013) — this
feature only documents and locks them. Migration posture for the new schema:
additive field additions bump its `schemaVersion` minor; breaking shape changes
bump `schemaVersion` major and require a migration note (eating its own dog
food).

**Versioning baseline**: Today the package versions diverge
(`Artifacts 0.1.11`, `Commands 0.1.10`, `Cli 0.1.10`) and
`currentGeneratorVersion` hardcodes `0.2.0` independently — exactly the
unmanaged-version problem US1/FR-003 names. This feature establishes one
semantic-version source (`Directory.Build.props`) consumed by all three packages
and reconciles the generator version, then locks it via the release contract.

**Testing**: `dotnet test` with xUnit over real disposable-project fixtures
(reusing `TestSupport`). New suites: golden-fixture baselines over public schemas
/ produced artifacts / `--json` reports; a coverage test asserting every
`GeneratedViewKind` and every public `CommandReport` shape has a schema-reference
entry **and** a baseline; a conformance test comparing a produced artifact from a
real lifecycle run against its documented schema entry; determinism (double-run
byte-identity) tests; and a boundary-exclusion test asserting no Governance
gate/route/profile/freshness vocabulary appears in any produced artifact.

**Target Platform**: .NET CLI tool (`dotnet tool install`), cross-platform; docs
target a clean environment with no FS.GG repo checkout and no Governance runtime.

**Project Type**: Single solution — packable F# libraries + CLI tool. Existing
`src/` + `tests/` layout retained.

**Performance Goals**: Not performance-sensitive; the release contract and
baselines are small. Determinism (byte-identity) is the hard requirement
(FR-008/SC-005), not throughput.

**Constraints**: All produced/locked artifacts MUST be byte-stable for identical
inputs and MUST exclude clocks, durations, host paths, ordering nondeterminism,
and ANSI styling (FR-008). Docs are projections; the structured/produced artifact
is authoritative on any disagreement (FR-005/FR-015).

**Scale/Scope**: 7 generated-view kinds + the public `--json` command reports +
3 packages + 1 CLI. One new pure module, one new machine artifact, four docs
surfaces, four new test suites.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec → FSI → Tests → Impl | New public surface (`ReleaseContract`) sketched as `.fsi` first; exercised before `.fs` hardens; semantic tests through the public surface. | ✅ PASS — `.fsi` precedes `.fs`; projection is FSI-exercisable. |
| II. Structured artifacts are the machine contract | `release-readiness.json` is the machine contract; the versioning policy, compatibility matrix, and schema reference are Markdown **projections** of it; disagreement is a detectable failure with the structured artifact authoritative. | ✅ PASS — FR-005/FR-015 encode "structured wins". |
| III. Visibility in `.fsi`, surface baselines | `ReleaseContract.fsi` is the sole public declaration; `PublicSurface.baseline` (Artifacts) updated in the same change; new golden baselines added. | ✅ PASS. |
| IV. Idiomatic simplicity | Records + DUs (stability class, change class) + pure folds; no custom operators, reflection, or CE machinery. | ✅ PASS. |
| V. Elmish/MVU boundary for stateful/I/O work | The release-readiness evaluation is a **pure** projection/fold over already-produced artifacts; it adds **no** new multi-step state and **no** new external-I/O workflow. It is hosted by the test suite (and reuses the existing read path for the conformance check), so no new MVU boundary is introduced. | ✅ PASS — pure data model + validator, explicitly exempt under Principle V ("simple pure … validators do not need MVU ceremony"). No new lifecycle command (FR-013). |
| VI. Test evidence mandatory | Golden/snapshot coverage for the new public schema and locked contracts; failing-before/passing-after baselines; real fixtures over mocks. | ✅ PASS. |
| VII. Agent & human share one contract | No new lifecycle command, so no new agent surface; docs are projections, not a second source. Claude/Codex context updated only to reference this plan. | ✅ PASS. |
| VIII. Observability & safe failure | Release-readiness check emits actionable diagnostics (which contract lacks a schema entry/baseline) and an actionable diff on drift; optional Governance compatibility degrades to a declared fact, never a hard dependency. | ✅ PASS. |

**Change tier**: **Tier 1 (contracted change)** — adds a public schema, a public
F# module, and CLI tool-packaging metadata. Additive only: no authored-source
schema change, no lifecycle-stage addition, no existing-contract break. Requires
spec, plan, tasks, `.fsi`, tests, docs, and a migration-posture statement (all
planned).

**Engineering constraints**: `net10.0` ✅; `FS.GG.SDD.*` namespace ✅; `fsgg-sdd`
CLI name reused (no rename) ✅; SDD remains usable with no Governance installed
✅; no FS.GG.Rendering package IDs/templates/docs URLs introduced ✅.

**Result**: PASS — no violations; Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/018-release-readiness/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── release-readiness.md       # SDD-owned release contract schema (machine contract)
│   ├── schema-reference.md        # Catalog: every public contract → version/stability/source
│   └── versioning-policy.md       # Change-class → bump-rule mapping (policy of record)
├── checklists/          # (already present)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/
│   ├── ReleaseContract.fsi          # NEW — public types + projection signatures
│   ├── ReleaseContract.fs           # NEW — pure projection/serialization impl
│   ├── SchemaVersion.fs(i)          # EDIT — reconcile generator version with package version source
│   └── … (unchanged)
├── FS.GG.SDD.Commands/              # unchanged surface; referenced for CommandReport inventory
└── FS.GG.SDD.Cli/
    └── FS.GG.SDD.Cli.fsproj         # EDIT — PackAsTool / ToolCommandName=fsgg-sdd; version from props

Directory.Build.props                # EDIT — single <Version> source consumed by all packages

docs/release/
├── installation.md                  # NEW — dotnet tool install → fsgg-sdd ship (clean env)
├── versioning-policy.md             # NEW — projection of contracts/versioning-policy.md
├── compatibility-matrix.md          # NEW — projection of release-readiness.json
├── schema-reference.md              # NEW — projection of the schema-reference catalog
├── release-readiness.json           # NEW — the machine contract (authoritative)
└── migrations/
    ├── README.md                    # NEW — migration-note obligation + index
    └── TEMPLATE.md                  # NEW — per-release breaking-change note template

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   ├── ReleaseContractTests.fs      # NEW — projection, determinism, schema-version
│   ├── ReleaseReadinessCheckTests.fs# NEW — coverage (every view+report has entry+baseline)
│   ├── PublicSurface.baseline       # EDIT — add ReleaseContract surface
│   └── baselines/                   # NEW — golden fixtures for public schemas
└── FS.GG.SDD.Commands.Tests/
    ├── ReleaseConformanceTests.fs   # NEW — produced artifact conforms to documented schema
    ├── ReleaseDeterminismTests.fs   # NEW — double-run byte-identity; boundary-exclusion
    └── baselines/                   # NEW — golden --json reports + produced-artifact fixtures
```

**Structure Decision**: Reuse the existing single-solution `src/`+`tests/`
layout. The new public surface lands in `FS.GG.SDD.Artifacts` (where
`SchemaVersion`/`GenerationManifest` already live), keeping the version/contract
vocabulary in one package. Docs live under a new `docs/release/` so the
human-facing projections and the authoritative `release-readiness.json` sit
together. Golden baselines live beside the existing `PublicSurface.baseline`
fixtures in each test project.

## Phase 0 — Research

See [research.md](research.md). Decisions resolved:

1. **Version single-source** — centralize the semantic version in
   `Directory.Build.props` (`<Version>`); each `.fsproj` drops its local
   `<Version>`; `currentGeneratorVersion` derives from the same value via assembly
   metadata so the generator and package versions can never silently diverge.
2. **Release-readiness as pure check, not a lifecycle command** — keeps FR-013
   (no new stage) and Constitution V (no gratuitous MVU); the check is a fold the
   test suite hosts.
3. **SemVer mapping** — major = breaking public schema/command/CLI change;
   minor = additive; patch = clarifying/no-contract-change; pre-1.0 semantics
   stated explicitly (the current `0.x` line: minor may break, documented).
4. **Stability classification vocabulary** — `Stable | AdditiveOptional |
   Experimental` per contract/field.
5. **Determinism strategy** — reuse the canonical `Serialization` ordering; assert
   double-run byte-identity; strip clocks/paths/ANSI (already excluded by existing
   generators).
6. **CLI distribution** — `dotnet tool` (`PackAsTool`), `ToolCommandName=fsgg-sdd`;
   public-registry account/signing/trusted-publishing explicitly out of scope
   (Governance/release-ops).

## Phase 1 — Design & Contracts

Outputs: [data-model.md](data-model.md), [contracts/](contracts/),
[quickstart.md](quickstart.md). Highlights:

- **`ReleaseContract` types**: `PackageVersionIdentity`,
  `CompatibilityMatrixEntry { SpecKitRange; GovernanceContractVersionRange option }`,
  `StabilityClass`, `ChangeClass`, `SchemaReferenceEntry { Contract; SchemaVersion;
  ContractVersion option; Stability; Determinism; SourceArtifact: ArtifactRef }`,
  and a top-level `ReleaseReadiness` envelope (`SchemaVersion`, version identity,
  matrix, catalog) with canonical serialization.
- **Release-readiness check** (pure): given the produced artifacts under
  `readiness/<id>/` and the catalog, return diagnostics for any public contract
  missing an entry or a baseline, and any documented field absent / undocumented
  field present (FR-012/FR-015).
- **Contracts**: the JSON schema for `release-readiness.json`; the schema-reference
  catalog; the versioning policy of record.

**Agent context update**: repoint the plan reference between the `<!-- SPECKIT
START -->` / `<!-- SPECKIT END -->` markers in `CLAUDE.md` to this plan.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
