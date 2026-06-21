---
description: "Task list for Release and Distribution Readiness"
---

# Tasks: Release and Distribution Readiness

## 📊 Progress — ✅ 26 / 26 complete (100%)

> Legend: ✅ done · 🟡 in progress · ⬜ not started · ⏭️ skipped

| Phase | Tasks | Status |
|---|---|---|
| 1 · Setup & grounding | T001–T002 | ✅✅ |
| 2 · Foundational (version single-source + `ReleaseContract` FSI + serialization) | T003–T007 | ✅✅✅✅✅ |
| 3 · US1 version identity + compatibility matrix + versioning policy (P1, MVP) | T008–T012 | ✅✅✅✅✅ |
| 4 · US2 schema-reference catalog + conformance (P2) | T013–T016 | ✅✅✅✅ |
| 5 · US3 locked baselines + readiness check (P2) | T017–T020 | ✅✅✅✅ |
| 6 · US4 CLI install + migration notes (P3) | T021–T023 | ✅✅✅ |
| 7 · Polish, boundary exclusion & validation | T024–T026 | ✅✅✅ |

> **Outcome (2026-06-21):** all 26 tasks complete. `dotnet build -c Release` clean;
> `dotnet test -c Release` → **368 passed / 0 failed** (Artifacts 103, Commands 265).
> Real-evidence smoke: `fsgg-sdd --version` → `0.2.0`; `dotnet pack` →
> `FS.GG.SDD.Cli.0.2.0.nupkg` (`packageType DotnetTool`). Evidence transcript:
> [`readiness/evidence.md`](readiness/evidence.md).

## Inventory & version reality (T001/T002 findings)

- **Public generated views** (`GenerationManifest.GeneratedViewKind`, all
  `schemaVersion 1`): `WorkModel`→`work-model.json`, `Analysis`→`analysis.json`,
  `Verify`→`verify.json`, `Ship`→`ship.json`, `Summary`→`summary.md` (Markdown),
  `AgentCommands`→`agent-commands/<target>/{guidance.json, commands.md, skills.md}`,
  `GovernanceHandoff`→`governance-handoff.json` (`schemaVersion 1`, **`contractVersion 1.0.0`**).
- **Command output:** `CommandReport` via `CommandSerialization.serializeReport` —
  `schemaVersion 1`, `reportVersion 1.0.0`. Top-level field inventories were captured
  empirically from a real lifecycle run and locked in the catalog (conformance test
  `T015` re-verifies against reality).
- **Version baseline → reconciled:** packages diverged (`Artifacts 0.1.11`,
  `Commands 0.1.10`, `Cli 0.1.10`, `currentGeneratorVersion 0.2.0`). Reconciled to a
  single `Directory.Build.props <Version> = 0.2.0` (matches the generator version; a
  minor/additive bump over the `0.1.x` line). **Release classification:** adds public
  surface, breaks no existing contract ⇒ **additive / minor, no migration note** (T023
  confirms `migrations[] = []`).

**Input**: Design documents from `specs/018-release-readiness/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/release-readiness.md, contracts/versioning-policy.md,
contracts/schema-reference.md, quickstart.md

**Change Tier**: Tier 1 (contracted change: adds a public schema, a public F#
module, and CLI tool-packaging metadata). Additive only — no authored-source
schema change, no lifecycle-stage addition, no existing-contract break. All
phases match the spec's overall tier; no per-task `[T1]`/`[T2]` annotations
needed.

**Nature of this feature**: Freezes, versions, documents, and locks the existing
public contracts. It **adds** one pure public F# surface
(`FS.GG.SDD.Artifacts/ReleaseContract.fsi/.fs`), one SDD-owned machine artifact
(`docs/release/release-readiness.json`, `schemaVersion 1`), four docs surfaces,
and locking baselines. It adds **no** new lifecycle stage, **no** new `fsgg-sdd`
command, and changes **no** authored-source or existing generated-view schema
(FR-013). It is entirely SDD-owned: no Governance gate/route/profile/freshness/
publish/provenance logic (FR-014).

**Tests**: Test tasks here are first-class spec deliverables (Constitution VI).
SC-002..SC-006 and FR-007/008/012/015 are *defined* by assertions — coverage,
conformance, determinism (byte-identity), drift detection, migration-note
obligation, and boundary-exclusion. They are core implementation, not optional
scaffolding.

**Elmish/MVU applicability (Principle IV/V)**: The release-readiness evaluation
is a **pure** validator (records + DUs + a total fold over already-produced
artifacts) — explicitly exempt from MVU under Principle V. The release contract
is a static repo-level machine artifact, **not** a per-work-item lifecycle write,
so it does **not** go through the `ship`/`refresh` MVU boundary. **No new MVU
boundary is introduced** and the evidence-obligations tasks reflect that.

---

## Phase 1: Setup & grounding

**Purpose**: Confirm the real public-output surface this feature must document and
lock before writing any contract.

- [X] **T001** Inventory the exact public outputs and their current versions.
  Enumerate every `GeneratedViewKind` (`WorkModel`, `Analysis`, `Verify`, `Ship`,
  `Summary`, `AgentCommands`, `GovernanceHandoff`) from
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`, the public `--json`
  `CommandReport` surface from `src/FS.GG.SDD.Commands/CommandSerialization.fsi`
  + `CommandTypes.fsi`, and the actual `schemaVersion`/`contractVersion` each
  emits. Record findings as a short note at the top of `tasks.md` (deviations
  section) so the catalog in T013 starts from reality, not the plan's placeholders.

- [X] **T002** [P] Capture the current divergent versions as the migration
  baseline: `Artifacts 0.1.11`, `Commands 0.1.10`, `Cli 0.1.10`,
  `currentGeneratorVersion 0.2.0`. Decide the single reconciled `<Version>` for
  the release and note it (input to T003). **Classify this very release under the
  new policy** (resolves analysis I2): it adds public surface but breaks no
  existing contract ⇒ **additive / minor bump, no migration note**. Record that
  classification so T023 can confirm no migration note is required. No code change
  in this task.

**Checkpoint**: real surface + version reality known.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: One version source and the `ReleaseContract` public surface +
serialization that every user story builds on.

**⚠️ No user-story work (Phases 3+) begins until this phase is complete.**

- [X] **T003** Centralize the package version: add a single `<Version>` to
  `Directory.Build.props` and remove the per-project `<Version>` from
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`,
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`, and
  `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`. Verify all three inherit it
  (`dotnet build`; check assembly versions).

- [X] **T004** Reconcile the generator version with the package version source in
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` (`currentGeneratorVersion`) so it
  derives from / matches the single `<Version>` (assembly informational version)
  rather than the hardcoded `0.2.0`. Update `SchemaVersion.fsi` only if the
  signature changes. After T003.

- [X] **T005** Author `src/FS.GG.SDD.Artifacts/ReleaseContract.fsi` (FSI-first,
  Principle I/III): public types `PackageVersionIdentity`, `ReleaseChannel`,
  `ChangeClass`, `StabilityClass`, `ContractFormat`, `ContractKind` (carrying the
  `GeneratedViewKind` + `ContractFormat`, or `CommandOutput`), `InventoryItem`
  (`InventoryKind = JsonField | MarkdownSection`), `CompatibilityMatrixEntry`,
  `SchemaReferenceEntry`, `MigrationNoteRef`, the `ReleaseReadiness` envelope, the
  `ProducedArtifact` snapshot type, and the projection + pure
  `evaluate : ReleaseReadiness -> ProducedArtifact list -> Diagnostic list`
  signature (per [data-model.md](data-model.md) — the JSON/Markdown split and
  pinned `evaluate` input resolve analysis U1/U2/U3). Exercise the shapes in
  FSI/prelude before `.fs`.

- [X] **T006** Implement `src/FS.GG.SDD.Artifacts/ReleaseContract.fs`: pure
  constructors/projection over the version identity + catalog, and canonical
  serialization of `ReleaseReadiness` to JSON through the existing
  `Serialization` module (stable key order, no clock/path/ANSI — FR-008). Wire
  the two new files into `FS.GG.SDD.Artifacts.fsproj` compile order (after
  `SchemaVersion`, alongside `GenerationManifest`).

- [X] **T007** [P] Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  to add the new `ReleaseContract` public surface, and confirm
  `SurfaceBaselineTests` passes (Principle III; the reflection test covers the
  `FS.GG.SDD.Artifacts` namespace). After T005/T006.

**Checkpoint**: one version, a compiling pure release-contract surface, green
surface baseline.

---

## Phase 3: User Story 1 — Versioned SDD + compatibility contract (Priority: P1) 🎯 MVP

**Goal**: A consumer can determine the release's version, its Spec Kit range, and
its optional Governance handoff `contractVersion` range, and a maintainer can map
any change class to a bump rule — without reading source.

**Independent test**: read `release-readiness.json` + the versioning policy and
confirm the version identity, compatibility matrix, and change-class→bump mapping
are all present and unambiguous (SC-001).

- [X] **T008** [US1] Build the `ReleaseReadiness` value for this release (identity
  + `compatibility[]` with `SpecKitRange` and optional
  `GovernanceContractVersionRange`) via `ReleaseContract` and serialize it to the
  authoritative `docs/release/release-readiness.json` (FR-002, FR-003). Governance
  range is an optional fact and may be `null`.

- [X] **T009** [P] [US1] Author `docs/release/versioning-policy.md` as a
  projection of [contracts/versioning-policy.md](contracts/versioning-policy.md):
  the change-class→bump table (Breaking→major+note, Additive→minor,
  Clarifying→patch), pre-1.0 semantics, and schema-vs-contract-version divergence
  rule (FR-001).

- [X] **T010** [P] [US1] Author `docs/release/compatibility-matrix.md` as a
  human projection of the `compatibility[]` array in `release-readiness.json`
  (FR-002), stating the Governance range is an optional integration fact.

- [X] **T011** [US1] Test (`tests/FS.GG.SDD.Artifacts.Tests/ReleaseContractTests.fs`):
  `release-readiness.json` round-trips through `ReleaseContract`; `identity.version`
  equals the single `<Version>` and is consistent with `generatorVersion`;
  `channel` is derived correctly (major 0 ⇒ preRelease); the compatibility entry
  carries a Spec Kit range and tolerates a `null` Governance range (FR-002/003,
  SC-001). After T008.

- [X] **T012** [P] [US1] Test: the versioning-policy doc agrees with the policy of
  record — every `ChangeClass` maps to exactly one bump and the migration-note
  obligation matches `Breaking ⇒ required` / additive ⇒ none (FR-001; supports
  US1 acceptance scenarios 2–4).

**Checkpoint**: MVP — a citable, machine-readable version + compatibility +
policy contract exists and is tested.

---

## Phase 4: User Story 2 — Documented public schemas (Priority: P2)

**Goal**: Every public generated view and `--json` report has an authoritative,
versioned, stability-classified schema reference that is a projection of the
structured contract.

**Independent test**: pick any public output; find its catalog entry (version,
field inventory, determinism, stability, source) and confirm a produced artifact
conforms (SC-002/SC-003).

- [X] **T013** [US2] Populate the `catalog[]` in `release-readiness.json` (via
  `ReleaseContract`) with one `SchemaReferenceEntry` per public output found in
  T001, **counting each `agent-commands/` sub-file separately** (`guidance.json`
  = JSON machine contract; `commands.md`/`skills.md` = Markdown projections) and
  treating `summary.md` as a Markdown projection (resolves analysis U1/U2). Each
  JSON entry carries `schemaVersion` (+ `contractVersion` for
  `governance-handoff.json`) with a JSON-field inventory; each Markdown entry
  carries the generator version with a section inventory. All carry a determinism
  guarantee, `StabilityClass`, and a `SourceArtifact` back-reference
  (FR-004/FR-005).

- [X] **T014** [P] [US2] Author `docs/release/schema-reference.md` as a projection
  of `catalog[]` (FR-005) — the human-readable table from
  [contracts/schema-reference.md](contracts/schema-reference.md).

- [X] **T015** [US2] Conformance test
  (`tests/FS.GG.SDD.Commands.Tests/ReleaseConformanceTests.fs`): run a real
  lifecycle fixture, then for each catalog entry assert the produced artifact has
  no undocumented public field and no documented field absent — produced artifact
  authoritative (FR-015, SC-003). After T013.

- [X] **T016** [P] [US2] Test: `docs/release/schema-reference.md` and
  `release-readiness.json` agree (doc is a projection; structured wins on
  disagreement) — FR-005/FR-015 drift guard. After T013/T014.

**Checkpoint**: 100% of public outputs documented and proven to conform.

---

## Phase 5: User Story 3 — Catch breaking changes before release (Priority: P2)

**Goal**: Locked baselines + a readiness check make the stability contract
enforceable: any unaccounted public-contract change fails fast with an actionable
diff, and gaps report not-ready.

**Independent test**: introduce a breaking change → baseline fails with a diff;
regenerate intentionally → updates cleanly; produce baselines twice → byte
identical (SC-004/SC-005).

- [X] **T017** [US3] Add golden baselines for public schemas and representative
  produced artifacts + `--json` reports under
  `tests/FS.GG.SDD.Artifacts.Tests/baselines/` and
  `tests/FS.GG.SDD.Commands.Tests/baselines/`, captured from a real fixture run
  (not synthesized). Add baseline tests that fail with an actionable diff naming
  the changed contract (FR-006/FR-007, SC-004).

- [X] **T018** [US3] Implement the pure `evaluate` readiness check in
  `ReleaseContract.fs` (signature from T005): given the catalog + produced
  artifacts, return a `Diagnostic` for any public output with no entry, no
  `SourceArtifact`, or `BaselinePresent = false`, and any field-level drift
  (FR-012/FR-015).

- [X] **T019** [US3] Coverage test
  (`tests/FS.GG.SDD.Artifacts.Tests/ReleaseReadinessCheckTests.fs`): enumerate
  every `GeneratedViewKind` + the `--json` report and assert `evaluate` reports
  not-ready when an entry or baseline is missing, and ready when complete (FR-012,
  SC-002). After T013/T017/T018.

- [X] **T020** [P] [US3] Determinism test
  (`tests/FS.GG.SDD.Commands.Tests/ReleaseDeterminismTests.fs`): serialize
  `release-readiness.json` and regenerate the baselines twice over identical
  inputs and assert byte-identity; assert no clock/duration/host-path/ordering/
  ANSI content (FR-008, SC-005).

**Checkpoint**: drift is mechanically detected; readiness fails by absence, never
passes by it.

---

## Phase 6: User Story 4 — Install the CLI & migrate across releases (Priority: P3)

**Goal**: A new user installs and runs `fsgg-sdd` through `ship` in a clean
environment with no FS.GG knowledge and no Governance runtime; breaking releases
carry migration notes.

**Independent test**: follow the install docs in a clean env to reach `ship`;
confirm each breaking release has a migration note and additive-only releases
have none (SC-007/SC-006).

- [X] **T021** [US4] Make the CLI a .NET tool: add `<PackAsTool>true</PackAsTool>`
  and `<ToolCommandName>fsgg-sdd</ToolCommandName>` to
  `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`; add a `--version` path in
  `Program.fs` reporting the single version (FR-011). **Buildable slice of SC-007**
  (resolves analysis C2): add a test asserting `dotnet pack` produces a tool
  package and `fsgg-sdd --version` prints the single reconciled version. The
  end-to-end `dotnet tool install` from a public registry is **out of scope**
  (registry/signing are Governance/release-ops) and validated manually per
  quickstart Scenario 6, not as an automated gate.

- [X] **T022** [P] [US4] Author `docs/release/installation.md`: `dotnet tool
  install` → `fsgg-sdd --version` → `init … ship` in a clean environment, no
  prior FS.GG knowledge, no Governance runtime (FR-011, SC-007). Note registry
  account/signing/provenance are out of scope (Governance/release-ops).

- [X] **T023** [US4] Add the migration-note machinery:
  `docs/release/migrations/README.md` (obligation + index) and `TEMPLATE.md`;
  represent notes as `MigrationNoteRef` in the contract. Test
  (`tests/FS.GG.SDD.Artifacts.Tests/ReleaseContractTests.fs`): a `Breaking`
  release requires a `MigrationNoteRef`; an additive-only release must not have
  one (FR-009/FR-010, SC-006). **Confirm this release carries no migration note**
  per its additive classification from T002 (resolves analysis I2). After T008.

**Checkpoint**: install path documented + tool-packable; migration obligation
encoded and tested.

---

## Phase 7: Polish, boundary exclusion & validation

- [X] **T024** Boundary- & scope-exclusion test
  (`tests/FS.GG.SDD.Commands.Tests/ReleaseDeterminismTests.fs` or a dedicated
  file): two assertions.
  - **Governance boundary (FR-014, SC-008)**: no Governance
    gate/route/profile/freshness/publish/provenance vocabulary appears in
    `release-readiness.json` or any produced artifact. **Whitelist** the
    pre-existing declared facts that legitimately name Governance: the optional
    `contractVersion` range and the `CommandReport.GovernanceCompatibility`
    field (the 017 declared compat fact) — assert against the *gate-logic*
    vocabulary, not the word "Governance", so the test does not false-positive
    on the existing compat surface (resolves analysis I1).
  - **No-scope-creep guard (FR-013)**: assert this feature changed no
    authored-source schema and added no `GeneratedViewKind` case or lifecycle
    stage — e.g. `nextLifecycleCommand` is unchanged and the set of
    `GeneratedViewKind` values matches the pre-018 set (resolves analysis C1).

- [X] **T025** [P] Update `README.md` "Current State"/"Scope" to mention the
  release-readiness contract, `docs/release/` docs, and `dotnet tool` install;
  cross-link from `docs/index.md`. Docs only — no second source of truth.

- [X] **T026** Run the full validation per [quickstart.md](quickstart.md):
  `dotnet build -c Release` + `dotnet test` (all existing + new suites green),
  record evidence under `specs/018-release-readiness/readiness/` (CLI `--version`
  smoke, byte-identity double-run, conformance, coverage). **Record SC-007 as
  partially manual** (resolves analysis C2): the automated portion is the
  `dotnet pack` / `--version` assertion from T021; the clean-environment
  `dotnet tool install … → init … ship` walk is captured as a manual evidence
  transcript, not an automated gate. Update the Progress table and any forced
  deviations.

---

## Dependencies & parallelism

> **Note on "phase"** (resolves analysis T1): the task phases below (Phase 1–7)
> are *implementation* phases and are independent of the *design* phases in
> `plan.md` (Phase 0 research / Phase 1 design). They do not correspond.

- **Sequential phases**: 1 → 2 → (3, 4, 5, 6 may interleave once Phase 2 is done)
  → 7. Phases 3–6 all depend only on the Phase 2 foundation; within the catalog,
  T013 (US2) is a prerequisite for the conformance/coverage tests (T015, T016,
  T019) and the readiness check exercising real entries.
- **Cross-task**: T004 after T003; T006/T007 after T005; T011 after T008; T015/
  T016/T019 after T013; T019 also after T017/T018; T023 after T008; T026 last.
- **Parallel-safe `[P]`**: T002, T007, T009, T010, T012, T014, T016, T020, T022,
  T025 (distinct files, no incomplete-task dependency in their phase).

## Task counts

- US1: 5 (T008–T012) · US2: 4 (T013–T016) · US3: 4 (T017–T020) ·
  US4: 3 (T021–T023) · Setup/Foundational/Polish: 10 (T001–T007, T024–T026).
- **Total: 26.**

## Suggested MVP

**Phase 1 + Phase 2 + Phase 3 (US1, T001–T012)** — a single machine-readable
version identity, a compatibility matrix, and a documented versioning policy: the
citable release contract every other story builds on.
