# Tasks: Troubleshooting skill, advertised `--text`, shipped examples

**Branch**: `071-troubleshooting-and-examples` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

- [ ] **T001** (FR-004) — Author `docs/examples/lifecycle-artifacts/` with one
  coherent work item's `clarifications.md`, `checklist.md`, `tasks.yml`,
  `evidence.yml`, based on known-valid fixture content. Verify each parses via a
  throwaway harness.

- [ ] **T002** (FR-004, AC-3) — Add `ExampleArtifactsContractTests` parsing each
  shipped example through its live public parser; assert `Ok` and no blocking
  diagnostics. Depends on T001.

- [ ] **T003** (FR-005, AC-4) — Replace the dead `tests/fixtures/…` reference in
  `fs-gg-sdd-tasks` (Sources) with the shipped example path + a complete inline
  `tasks.yml` example in the skill body. Sweep all seeded skills for any other
  non-shipped path citation and repoint. Mirror to `.codex`.

- [ ] **T004** (FR-002) — Author `fs-gg-sdd-troubleshooting/SKILL.md` (`.claude` +
  `.codex`, byte-identical): the `--text` recipe, the counter→meaning→next-action
  table, and links to [[fs-gg-sdd-authoring-contracts]] and the per-stage skills.

- [ ] **T005** (FR-001) — Grow the seeded set 15 → 16:
  `SeededSkills.skillNames` (+ comment); `Commands.fsproj` `<EmbeddedResource>`;
  `SeededSkillsTests` `45 → 48`; `CompositionAcceptanceTests` expected list.
  Depends on T004.

- [ ] **T006** (FR-003) — Add the `--text`-as-diagnostic one-liner + the
  troubleshooting family entry to `fs-gg-sdd-lifecycle` (`.claude` + `.codex`).

- [ ] **T007** (FR-001, AC-5) — Update `CLAUDE.md` + `AGENTS.md` "15 … skills (10
  stage + 5 cross-cutting)" → "16 … (… + 6 cross-cutting)" naming troubleshooting;
  keep the two byte-identical.

- [ ] **T008** (SC-001/SC-002, AC-1) — Verify: full `dotnet test`, fantomas, and an
  end-to-end `init` into a scratch dir asserting the troubleshooting skill seeds
  across roots and no seeded skill cites a non-shipped path. Depends on all above.
