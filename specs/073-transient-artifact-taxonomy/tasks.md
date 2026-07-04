# Tasks: Transient SDD artifact taxonomy + seeded `.gitignore`

**Branch**: `073-transient-artifact-taxonomy` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

- [x] **T001** (FR-002/005, AC-002, D3) — **RED-first** drift-guard test
  `ArtifactTaxonomyDriftTests`: parse `docs/reference/artifact-taxonomy.md`'s regenerable
  `readiness/<id>/…` list and assert it **equals** the set of `sourceArtifact.path` values in
  `docs/release/release-readiness.json` `catalog[]` where `sourceArtifact.kind == "generatedView"`
  (9 today). Fails first (doc absent).

- [x] **T002** (FR-003/005, AC-003/006, D1/D2) — **RED-first** seed test
  `GitignoreSeedTests`: (a) `initEffects` emits exactly one
  `WriteFile(".gitignore", body, AgentGuidanceTarget)`; (b) `body == Foundation.gitignoreSeedText`;
  (c) the seed text contains the regenerable `readiness/<id>/` role rule and the D2 generic outputs,
  LF-terminated, byte-stable. Fails first (no constant, no write).

- [x] **T003** (FR-003, D1/D2) — Add `gitignoreSeedText` module constant (exact bytes per plan D2:
  SDD-transient headline rule + generic `bin/ obj/ artifacts/ TestResults/ .tmp/ nuget-cache/`) near
  the seed-body constants in `Foundation.fs`; add
  `WriteFile(".gitignore", gitignoreSeedText, AgentGuidanceTarget)` to `initEffects` in the
  `AgentGuidanceTarget` block (with the constitution/early-stage writes, before `SeededSkills`).
  Greens T002. `scaffold` inherits it via reused `initEffects` (FR-004, no code there).

- [x] **T004** (FR-004, AC-005) — Add `".gitignore"` to `Drift.expectedArtifactPaths`; confirm
  `doctor` lists it in the expected-vs-present set and `upgrade` no-clobber re-seeds it when missing.
  Update `expectedArtifactCount`-dependent assertions.

- [x] **T005** (FR-001, AC-001, D3/D4) — Author `docs/reference/artifact-taxonomy.md`: durable class
  (authored lifecycle sources + authored contracts/schemas + Governance record-of-record) and
  regenerable class (the catalog-derived `readiness/<id>/…` list + baselines/snapshots + diagnostics
  + `nuget-cache/`), each with example paths; the role-based ignore convention; the D4
  presence-not-content limitation; and a copy-paste `.gitignore` fragment for existing repos. Greens
  T001.

- [x] **T006** (FR-001) — Link the taxonomy doc from `docs/reference/README.md` starting points and
  the top-level `README.md`/quickstart reference index (wherever `authoring-contracts.md` /
  `doctor-upgrade.md` are surfaced).

- [x] **T007** (FR-006, AC-004/007) — Regression sweep: update `init`/`scaffold`/`doctor` golden or
  snapshot fixtures that enumerate the seeded set or `expectedArtifactCount`; verify no JSON-contract
  byte, exit-code, or stream-routing change for any command output; confirm scaffold provenance does
  **not** record `.gitignore` under `generatedProduct`. Full `dotnet test` green.

- [x] **T008** (roadmap) — Update `docs/release/release-readiness.json` **only if** a catalog entry is
  added/renamed by this work (expected: none — taxonomy is descriptive). Otherwise no-op; the T001
  guard is the currency check. Confirm `CLAUDE.md` boundary prose still accurate (seeded skeleton set
  now includes `.gitignore`).
