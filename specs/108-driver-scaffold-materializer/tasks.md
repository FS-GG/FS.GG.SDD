# Tasks: Scaffold-Time Materializer for the Delivered `workRoadmap` Driver Skill

**Spec**: `specs/108-driver-scaffold-materializer/spec.md` · **Plan**:
`specs/108-driver-scaffold-materializer/plan.md`

- [ ] T001 Pin `FS.GG.Drivers` `0.1.0` in `Directory.Packages.local.props`; add
      `PackageReference Include="FS.GG.Drivers"` to `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`;
      embed `$(FsggDriversContentDir)/driver-skill-manifest.json` (`LogicalName Driver.manifest`) and
      `$(FsggDriversContentDir)/skills/**/SKILL.md` (`LogicalName Driver.skill/%(RecursiveDir)SKILL.md`)
      as `EmbeddedResource`. (FR-002)
- [ ] T002 `src/FS.GG.SDD.Artifacts/DriverManifest.fs`(+`.fsi`): `DriverManifestEntry`
      (`Id`/`Scope`/`Sha256`/`SuppliedBy`/`MaterializesWhen`), `DriverManifest`
      (`SchemaVersion`/`Skills`), `tryParse: string -> Result<_,string>`; `DriverPredicate.evaluate:
      string -> Set<string> -> bool option` (`always`/`false`/`has <glob>`/`and`/`or`; else `None`).
      (FR-003/FR-004)
- [ ] T003 `src/FS.GG.SDD.Artifacts/ArtifactRef.fs`(+`.fsi`): add `ArtifactOwner.Driver`;
      `ownerValue Driver = "driver"`; `ownerFromValue "driver" = Driver`. (FR-006)
- [ ] T004 `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`(+`.fsi`): additive `DriverPaths:
      ScaffoldProducedPath list`; serialize as `driverPaths` (sorted by path, each with `sha256`);
      `tryParse` defaults owner `Driver`; schema stays **v1**. (FR-006)
- [ ] T005 `src/FS.GG.SDD.Commands/CommandWorkflow/DriverSkills.fs`: read embedded `Driver.manifest`
      + `Driver.skill/<id>/SKILL.md`; content-addressed verify (`SkillMirror.sha256` == row `sha256`,
      else defect); predicate gate via `DriverPredicate.evaluate` over present ids; reject a row whose
      `id` collides with a seeded `fs-gg-sdd-*` name; plan `WriteFile(path, body, AgentGuidanceTarget)`
      per root for materializable+verified rows; expose the planned driver `ScaffoldProducedPath`s (with
      sha256). No new `CommandEffect`. (FR-001/003/004/005/007)
- [ ] T006 `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`: post-instantiation driver tick —
      emit the driver writes, add `DriverPaths` to the provenance record, and add a `driver` block to the
      scaffold `CommandReport` (materialized ids/roots on success; `scaffold.driverVerifyFailed` /
      `scaffold.driverPredicateUnevaluated` on defect). Incomplete ⇒ not reported complete. (FR-001/009)
- [ ] T007 Refresh exclusion: ensure `refresh` treats `DriverPaths` as excluded/externally-owned (never
      regenerated or removed). (FR-006)
- [ ] T008 **[carved to a follow-up item]** `HandlersDoctor.fs`/`HandlersUpgrade.fs` + `Drift.fs`: add the
      expected driver to the doctor read-only drift report and to `upgrade`'s `artifactReSeed` missing-only
      no-clobber backfill. Existing-scaffold transition; strict addition over this PR. (FR-010)
- [ ] T009 Drift guard: test (and/or `registry` sub-verb) asserting the embedded manifest+bodies parse and
      each body's `SkillMirror.sha256` equals its declared row `sha256`; golden on the delivered
      `workRoadmap` sha256. (FR-008)
- [ ] T010 Tests: `DriverManifest`/`DriverPredicate` unit; `ScaffoldProvenance` `DriverPaths` round-trip
      (schema `1`); `DriverSkills` planning (all roots for `always`, none for `false`, tamper⇒defect,
      `fs-gg-sdd-*` id⇒reject, offline witness AC-004); scaffold acceptance (three roots byte-identical,
      provenance owner `driver`, `refresh` leaves untouched). (AC-001..008)
- [ ] T011 `docs/reference/`: document the driver materialization + backfill (update
      `doctor-upgrade.md`; add a short driver reference). No provider/`.github` literal as behavior.
- [ ] T012 Gates: `dotnet test` green · `fantomas` clean · reflection `PublicSurface.baseline` updated ·
      `surface --update` then `surface --check` green · `fsgg-sdd registry validate` on the real
      `registry/skills.yml` still `valid`. Record driven scaffold + refresh verdicts in the PR body.
