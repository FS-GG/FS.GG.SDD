# Tasks: A Typed Assertion Over the Skill Registry

**Spec**: `specs/104-typed-skill-registry/spec.md` · **Plan**: `specs/104-typed-skill-registry/plan.md`

- [ ] T001 Model the three-state `mirrored` in `src/FS.GG.Contracts/Registry.fsi` + `Registry.fs`:
      `MirrorDeclaration` (`MirrorUnspecified` / `MirrorDeclared of bool` / `MirrorMalformed of raw`),
      `SkillRegistryEntry`, `SkillRegistryDocument`. Absent must not be able to *become* `false`
      (FR-001, FR-002).
- [ ] T002 Add the `MalformedField of fieldName: string` rule to `RegistryRule`, so a present-but-
      unparseable value has a diagnostic class distinct from `MissingField` (FR-003, FR-006).
- [ ] T003 Implement the pure `validateSkillRegistry` in `src/FS.GG.Contracts/Registry.fs` — document
      order, BCL-only, no I/O; the per-row rules of FR-006 (FR-004, FR-006).
- [ ] T004 [P] Update `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` for the new public surface.
- [ ] T005 Add the YAML load edge `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/SkillRegistryDocument.fs(i)`
      with `load` + `detectKind`. Parse `mirrored` three-state — **do not** reach for `Internal.boolAt`,
      whose `| _ -> defaultValue` arm is the exact coercion this feature forbids (FR-003, FR-007).
- [ ] T006 Register the new files in `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` (after
      `RegistryDocument.fs`; F# compile order is significant).
- [ ] T007 Dispatch on document kind in `src/FS.GG.SDD.Cli/RegistryValidate.fs` — a root `skills:` key
      selects the skill registry; everything else keeps today's behaviour byte-for-byte (FR-005).
- [ ] T008 [P] Check in a fixture of the **real** org `registry/skills.yml` under
      `tests/FS.GG.Contracts.Tests/` so AC-001 pins the actual catalog, not a toy.
- [ ] T009 Tests in `tests/FS.GG.Contracts.Tests/SkillRegistryValidatorTests.fs` — the nine rows of the
      plan's verification table. Each rule mutation-checked (AC-001..AC-008).
- [ ] T010 Tests for the load edge in `tests/FS.GG.SDD.Artifacts.Tests/` — absent vs `false` vs malformed
      vs non-scalar; malformed YAML → `Error`, never an exception (FR-003, FR-007).
- [ ] T011 Drive the real CLI end-to-end against the real `skills.yml` **and** `dependencies.yml`;
      record both verdicts and exit codes in the PR body (plan §Verification — driven, not just asserted).
- [ ] T012 `dotnet test` green · `fantomas` clean · `PublicSurface` green.
