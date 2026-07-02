# Tasks: Skill-manifest contract types (P0.D0.2)

Issue FS-GG/FS.GG.SDD#60 · ADR-0014. All changes additive.

## Phase A — Contract types in FS.GG.Contracts

- [ ] **T001** `Schemas.fsi`/`Schemas.fs`: add `SkillScope` (`Process | Product`),
      `SkillManifestEntry`, `SkillManifest`. (FR-001/002/003)
- [ ] **T002** `Schemas.fsi`/`Schemas.fs`: add `agentSkillRoots = [".claude"; ".codex"; ".agents"]`.
      (FR-004)
- [ ] **T003** `Schemas.fsi`/`Schemas.fs`: add `skillManifestVersion = 1` and register
      `skill-manifest` in `entries` (owner SDD); update the `.fsi` "All 10 named schemas" doc to 11.
      (FR-005)
- [ ] **T004** `Schemas.fsi`/`Schemas.fs`: add `Sha256: string option` to `ScaffoldProducedPathEntry`.
      (FR-006)
- [ ] **T005** `ContractVersion.fs` + `FS.GG.Contracts.fsproj`: `1.2.0 → 1.3.0`. (FR-008)

## Phase B — Runtime provenance additive field

- [ ] **T006** `ScaffoldProvenance.fs`: add `Sha256: string option` to `ScaffoldProducedPath`;
      serialize omits key when `None`, emits `"sha256"` when `Some`; parse reads optional `sha256`
      (absent/blank ⇒ `None`), for both `producedPaths` and `mirroredPaths`. (FR-006)
- [ ] **T007** `HandlersScaffold.fs`: set `Sha256 = None` in the produced/mirrored record literals
      (no digest computed — P1). (FR-009)

## Phase C — Tests + golden baseline

- [ ] **T008** `SchemaVersionConstantTests.fs`: entries 10 → 11; assert `skill-manifest` present,
      owner SDD; assert `skillManifestVersion = 1`; keep the scaffold-provenance-stays-v1 guard.
      (SC-002/SC-004)
- [ ] **T009** `ContractVersionTests.fs`: `1.2.0 → 1.3.0` (+ changelog comment). (SC-001)
- [ ] **T010** `ScaffoldProvenanceTests.fs`: add `Sha256 = None` to existing literals; add tests —
      sha256 round-trips when present; absent ⇒ `None`; digest-free serialize is byte-identical
      (no `sha256` key). (SC-003)
- [ ] **T011** Regenerate `PublicSurface.baseline` (`FSGG_UPDATE_BASELINE=1`); confirm additive-only
      diff. (SC-001)
- [ ] **T012** `dotnet build` + `dotnet test` green. (SC-005)

## Phase D — Cross-repo registry (.github)

- [ ] **T013** `FS-GG/.github` `registry/dependencies.yml`: `scaffold-provenance.version`
      `1.0.0 → 1.1.0`; prepend the `updated:` annotation. Update `docs/registry/compatibility.md`
      (versioned-contracts row). Validate with `fsgg-sdd registry validate` → `"valid": true`. Open
      PR closing #60. (FR-007/SC-004)

## Phase E — Land

- [ ] **T014** Flip board item #60 → Done; commit/merge/push.
