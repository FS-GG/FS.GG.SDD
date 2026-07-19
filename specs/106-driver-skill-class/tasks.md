# Tasks: The Driver Skill Class, Known But Not Yet Enforced

**Spec**: `specs/106-driver-skill-class/spec.md` · **Plan**: `specs/106-driver-skill-class/plan.md`

- [ ] T001 Add `"driver"` to the private `skillScopes` set in `src/FS.GG.Contracts/Registry.fs`, and
      enumerate all three scopes in the `UnknownComponent` message (`… expected 'process', 'product', or
      'driver'.`). Loosening only — no `.fsi`, load-edge, CLI, or `schemaVersion` change (FR-001, FR-002).
- [ ] T002 Tests in `tests/FS.GG.Contracts.Tests/SkillRegistryValidatorTests.fs`: extend the existing
      `the declared scopes are accepted` Theory with `driver` (AC-001/AC-004); add a `driver` row with
      `owner: .github` + composed `materializes-when` → `Valid` (AC-002/FR-003); assert the unknown-scope
      message now names `driver` (AC-003/FR-004). Each row mutation-checked per plan §Verification.
- [ ] T003 Drive the real CLI: `fsgg-sdd registry validate` against a `driver`-row document **and**
      the real `registry/skills.yml` (must stay `valid`); record both verdicts + exit codes in the PR
      body (plan §Verification — driven, not just asserted).
- [ ] T004 `dotnet test` green · `fantomas` clean · `PublicSurface` + `surface --check` green (no public
      surface moved, so both must stay untouched).
- [ ] T005 [second PR] Cut the single combined CLI release: bump `Directory.Build.local.props`
      `0.16.0 → 0.17.0`, merge, push `v0.17.0`. Do **not** pay the `mirrored` 1→2 bump (step 2).
