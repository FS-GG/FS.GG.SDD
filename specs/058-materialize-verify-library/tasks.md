# Tasks: One materialize-and-verify library + content-aware drift (P1)

Issue FS-GG/FS.GG.SDD#61 · ADR-0014 P1.

## Phase A — The library in FS.GG.Contracts

- [x] **T001** New `SkillMirror.fs`/`.fsi` (module `Fsgg.SkillMirror`): `providerSourceRoot`,
      `sha256`, `skillPath`, `skillIdOfPath`, `mirrorTargetRoots`, `retargetSkillPath`,
      `MirrorWrite`, `mirror`. (FR-001)
- [x] **T002** Same module: `ExpectedSkill`, `ActualCopy`, `SkillDrift`, `verify`. (FR-001)
- [x] **T003** Add `SkillMirror.fs`/`.fsi` to `FS.GG.Contracts.fsproj` compile order (after
      `Schemas.fs`); `ContractVersion.fs` + `.fsproj` `1.3.0 → 1.4.0`. (FR-009)
- [x] **T004** `FS.GG.Contracts.Tests`: unit tests for `mirror` (one write per root, sorted),
      `verify` (missing / divergent / hash-mismatch / clean), `sha256`/`skillIdOfPath`/`retargetSkillPath`.

## Phase B — Route the fan-outs through the library

- [x] **T005** `FS.GG.SDD.Commands.fsproj`: add direct `ProjectReference` to `FS.GG.Contracts`.
- [x] **T006** `SeededSkills.fs`: `skillEffects` computes destinations via `SkillMirror.mirror
      agentSkillRoots` (delete the hardcoded three-root list); effects byte-identical. (FR-002)
- [x] **T007** `HandlersScaffold.fs`: replace `agentsSkillsPrefix`/`mirrorTargetsFor`/
      `plannedMirroredPaths` and the mirror write loop with `SkillMirror`
      (`providerSourceRoot`/`mirrorTargetRoots`/`retargetSkillPath`); populate produced+mirrored
      `Sha256 = Some (SkillMirror.sha256 body)` for skill paths. (FR-003/FR-005)
- [x] **T008** `HandlersRefresh.fs`: route the re-mirror through `retargetSkillPath` over
      `mirrorTargetRoots agentSkillRoots` (delete inline `.claude`/`.codex` writes). (FR-004)
- [x] **T009** Build + run 056 scaffold/refresh/seeded byte-identity tests — confirm unchanged (SC-003).

## Phase C — Content-aware drift

- [x] **T010** `CommandTypes.fsi`/`.fs`: add `SkillDriftPaths: string list` to `DriftReport`(internal),
      `DoctorSummary`, `UpgradeSummary`. `CommandSerialization.fs`: emit it after
      `missingArtifactPaths`. (FR-006)
- [x] **T011** `Drift.fs`: build `expected` (process from `SeededSkills.seededSkills` digest =
      `SkillMirror.sha256 body`; product from provenance skill ids + recorded `sha256`) and `actual`
      (each skill × root body from snapshots); call `SkillMirror.verify agentSkillRoots`; fold into
      `SkillDriftPaths` + `IsCoherent`. Retain presence-only `MissingArtifactPaths`. (FR-006)
- [x] **T012** `Foundation.fs` + `HandlersDoctor.fs`/`HandlersUpgrade.fs`: provenance-driven phase-2
      read of product-skill copies across `agentSkillRoots` (read-gate), then compute; `doctor` stays
      write-free; thread the snapshot bodies into `Drift.compute`. (FR-007/FR-008)
- [x] **T013** `Diagnostics.fs`: reuse/extend the `doctor.driftDetected` advisory to fire on skill
      drift; `upgrade` residual-drift/hint reflect un-repaired content drift. (FR-007)

## Phase D — Tests + baseline + version

- [x] **T014** `RemediationSupport.fs`: `makeFixture` writes the **canonical** seeded body for seeded
      SKILL.md paths (so coherent fixtures stay coherent under content-aware verify); non-skill paths
      unchanged. Add a fixture with a provider skill across the three roots.
- [x] **T015** New `doctor`/`upgrade` content-drift tests: edited root copy (divergence) + deleted
      provider copy (skill loss) both detected; `upgrade --yes` re-seeds missing → residual reduced.
      **Red-then-green.** (SC-005/FR-010)
- [x] **T016** `ScaffoldProvenanceTests`/`ScaffoldCommandTests`: assert produced/mirrored skill paths
      carry a non-empty correct `sha256`; non-skill paths carry none. (SC-004)
- [x] **T017** Regenerate `PublicSurface.baseline` (`FSGG_UPDATE_BASELINE=1`); confirm additive-only.
      `ContractVersionTests` `1.3.0 → 1.4.0`. (SC-001)
- [x] **T018** `Directory.Build.local.props` `<Version>` `0.4.0 → 0.5.0`; update version-pinned
      tests/fixtures (ScaffoldCliCoherence/AgentsCommand) as needed. (FR-009)
- [x] **T019** `dotnet build` + `dotnet test` green; `/verify` doctor/upgrade/scaffold end-to-end. (FR-010)

## Phase E — Land

- [x] **T020** Flip board item #61 → Done (note the separate `0.5.0` publish-before-flip tracker);
      commit/merge/push.
