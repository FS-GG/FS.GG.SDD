# Tasks: Emit the `fs-gg-sdd-*` process skill-manifest

**Branch**: `072-process-skill-manifest` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

- [ ] **T001** (FR-007, AC-002/005/006/007) — Author `ProcessSkillManifestTests`
  **RED-first**: id set == `SeededSkills.skillNames` (16; troubleshooting in, project
  out); each `sha256` == `SkillMirror.sha256` of the on-disk authored `SKILL.md`;
  every `scope == process` & `materializes-when == always`; `schemaVersion == 1`;
  committed bytes == fresh serialization (staleness); grammar canonical + no
  `(`/`&&`/`||`/quotes; two serializations byte-identical, entries sorted by `id`,
  LF. Fails (no code, no file).

- [ ] **T002** (FR-001/002/008, D5) — Add pure `processSkillManifest : unit ->
  SkillManifest` (Commands) from `seededSkills ()`: `Id=name; Scope=Process;
  Sha256=SkillMirror.sha256 body; Body=None; ResolvablePath=Some
  ".agents/skills/<name>/SKILL.md"`, ordered by `skillNames`. Reuses the existing
  hasher + skill set (no new source of truth).

- [ ] **T003** (FR-003/004/005, D2/D3, AC-003/004/005) — Add
  `SkillManifestJson.serialize : SkillManifest -> string` (+ `.fsi`) in
  `FS.GG.SDD.Artifacts` (ScaffoldProvenance house style): emit `{ schemaVersion,
  skills:[{ id, scope, sha256, resolvablePath, materializes-when }] }`, sorted by
  `id`, `materializes-when: "always"` constant for `Process`, `body`/`supplied-by`
  omitted, LF, deterministic. Depends on T002. Unit-test shape/determinism.

- [ ] **T004** (FR-006, D1) — Wire `registry skill-manifest` sub-verb at the
  `registry` dispatch (`Program.fs` → `RegistryValidate`/new
  `RegistrySkillManifest`): `--check` (regenerate → compare to committed → non-zero +
  diff on drift), `--write` (write `.agents/skills/skill-manifest.json`), bare (print
  JSON). Outside the `CommandReport` contract, deterministic. Depends on T003.

- [ ] **T005** (FR-006, AC-006) — Generate + commit
  `.agents/skills/skill-manifest.json` via `registry skill-manifest --write`. Depends
  on T004. Greens T001.

- [ ] **T006** (NFR-002) — Doc notes: `CLAUDE.md`/`AGENTS.md` boundary — SDD emits the
  process producer manifest at `.agents/skills/skill-manifest.json` (process-only,
  all `scope: process`); kept **byte-identical**. One-liner where the skill-manifest
  contract is described (spec-057 / lifecycle skill if applicable).

- [ ] **T007** (AC-002/007, cross-repo) — Verify (real evidence): full `dotnet test`;
  `registry skill-manifest --check` exits 0 on the committed file and non-zero after
  a deliberate `SKILL.md` body edit (RED demo), reverted; re-confirm the 15 shared
  digests still equal the registry's provisional values and `fs-gg-sdd-troubleshooting`
  (`03c65640…`) is the 16th. fantomas clean. Depends on all above.

- [ ] **T008** (cross-repo close) — Comment on #109 / notify `.github`: manifest
  emitted; 15 digests match provisional (clean promote); troubleshooting is a new
  16th row (15→16 registry drift). Board: In review at PR-open; `Closes #109`.
