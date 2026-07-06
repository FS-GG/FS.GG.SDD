# Tasks: Dev-Repo Provenance (085)

All tasks complete. Ordered as implemented; `[X]` = done.

## Artifacts layer — the shape

- [X] T001 Add `devRepoOutcome`, `isDevRepo`, `devRepoRecord` to `ScaffoldProvenance.fsi`.
- [X] T002 Implement them in `ScaffoldProvenance.fs` (no serializer/parser change).
- [X] T003 `ScaffoldProvenanceTests.fs`: dev-repo round-trip, `isDevRepo` true/false, empty-pin +
  `devRepoInit` shape, byte-identity. (FR-001/003/004/007)

## Commands layer — init writes the anchor

- [X] T004 `Foundation.fs`: `devRepoProvenance` (producedPaths = `Drift.expectedArtifactPaths`,
  owner `Sdd`) + `initProvenanceEffect`. (FR-002)
- [X] T005 `Foundation.fs`: append the write to the `Init` dispatch only — NOT `initEffects`.
  (FR-006)
- [X] T006 `InitCommandTests.fs`: init writes a parseable dev-repo document over the seeded
  skeleton (owner `sdd`); byte-identical across runs. (FR-001/002/007)

## Commands layer — doctor/upgrade recognition

- [X] T007 `Drift.fs`: report `ProviderName = None` for a dev-repo record in the `Some` branch.
  (FR-005)
- [X] T008 `RemediationCommandTests.fs` (`DriftTests`): dev-repo engages reconciliation
  (`HasProvenance`, `ProviderName = None`, `coherentByAbsence`, `NoTarget` re-pin, coherent when
  seeded); re-seeds a missing seed without a provider. (FR-005)

## Regression / surface

- [X] T009 `ScaffoldCommandTests.fs`: exclude the provenance anchor from the two init-vs-scaffold
  skeleton comparisons (init writes dev-repo provenance; scaffold writes provider provenance).
  (SC-003)
- [X] T010 Refresh `PublicSurface.baseline` (+`devRepoRecord`, +`isDevRepo`). (SC-002)
- [X] T011 Full solution build + test green (Artifacts, Commands, Cli, Acceptance, Validation).
