# Tasks: Clean SDD's own committed readiness tree

**Branch**: `074-clean-sdd-readiness` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

- [ ] **T001** (FR-004, AC-004, D4) — **Baseline green.** Clean `dotnet build -c Release` and full
  `dotnet test` before any change, to establish the suite is green pre-cleanup (so a later failure
  is attributable to the cleanup, proving/refuting a hidden fixture dependency).

- [ ] **T002** (FR-001, AC-001, D1/D2) — Add the role-based rule to root `.gitignore`: a commented
  block referencing ADR-0018 / `docs/reference/artifact-taxonomy.md` plus the line
  `specs/*/readiness/`. Verify `git check-ignore specs/001-sdd-artifact-model/readiness/build.txt`
  is ignored and `git check-ignore readiness/019-spectre-rendering/evidence.yml` is **not**.

- [ ] **T003** (FR-002/003, AC-002/003, D3) — Untrack the regenerable tree:
  `git rm --cached -- $(git ls-files 'specs/*/readiness/*')` (214 files). Confirm
  `git ls-files 'specs/*/readiness/*'` == 0 and `git ls-files 'readiness/*'` == 11; `git status`
  clean (the working-tree copies are now ignored).

- [ ] **T004** (FR-004, AC-004, D4) — **Regeneration/no-loss proof.** Re-run the full test suite
  (all projects) after the untrack; confirm green with no new failures — the executable proof that
  no removed file was a live fixture (esp. the two evidence tests that reference
  `specs/011-.../readiness/…` paths as parsed strings).

- [ ] **T005** (FR-005, AC-005, D5) — **Minimal-diff audit.** Confirm `git diff`/`git status`
  touch only `.gitignore`, the 214 index removals, and `specs/074-…/` sources — no `.fs`, schema,
  golden fixture, `release-readiness.json`, or `CLAUDE.md` change. Commit and open the PR
  (`Closes #112`).
