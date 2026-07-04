# Tasks: Clarify/checklist silent-failure grammars

**Branch**: `070-clarify-checklist-grammars` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Dependency-ordered. Each task names its requirement(s) and the real evidence.

- [ ] **T001** (FR-001) — Fix `Clarification.parseRemainingAmbiguity`
  (`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Clarification.fs:262`) to drop
  lines where `isNoOutstandingSentinel line` is true before classifying, so a
  disclaimer contributes 0 to `BlockingAmbiguityCount`. Evidence: unit test on
  `parseClarificationFacts` (AC-1 disclaimer → 0; AC-2 genuine → 1).

- [ ] **T002** (FR-001) — Add artifact-level semantic tests
  (`tests/FS.GG.SDD.Artifacts.Tests`) for the disclaimer-vs-genuine boundary:
  `- None. AMB-… resolved` → `BlockingAmbiguityCount = 0`; `- AMB-001: … unclear.`
  → `1`; `- No remaining ambiguities; AMB-001 resolved.` → `0`; `- No decision yet
  on AMB-001.` → `1` (not a disclaimer noun). Depends on T001.

- [ ] **T003** (FR-002/FR-003) — Enrich `correction` for
  `unresolvedBlockingAmbiguity` and `failedChecklistPrerequisite` in
  `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs`; update any
  snapshot/golden pinning the old text. Evidence: assertion tests on the emitted
  `correction` (AC-3).

- [ ] **T004** (FR-004) — Add the two empty-section contract sections + tagged
  accepted/rejected fences to `docs/reference/authoring-contracts.md`
  (`remaining-ambiguity:disclaimer|blocking`, `blocking-findings:disclaimer|finding`).

- [ ] **T005** (FR-004, AC-4/AC-6) — Extend
  `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` with drift-guard
  facts running each tagged clarify example through `Clarification.parseClarificationFacts`
  (disclaimer → 0 blocking / genuine → >0) and each checklist example through the
  live `Checklist` parser (disclaimer dropped / finding kept). Locks Trap-2.
  Depends on T004.

- [ ] **T006** (FR-005, AC-5) — Update `fs-gg-sdd-clarify` and
  `fs-gg-sdd-checklist` SKILL.md at the authored source (`.claude/skills/...`),
  add the stage empty-section rule + (checklist) the auto-write of
  `checklistReady`; mirror byte-identically to `.codex/skills/...`. Evidence: the
  skill-mirror / seeded-skill drift guard green.

- [ ] **T007** (AC-6, Trap-3 lock) — Add a test asserting a clean `checklist`
  writes `status: checklistReady` (no `checklisted` intermediate) and that a
  non-ready status yields the enriched `failedChecklistPrerequisite` correction.

- [ ] **T008** (SC-001/SC-003) — End-to-end verify on a scratch work item: the
  reproduced Trap-1 fixture goes `clarify → checklist → plan` from blocked to
  clean; run full `dotnet test FS.GG.SDD.sln`; confirm 0 new warnings. Depends on
  all above.
