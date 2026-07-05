---
description: "Task list for feature 079 — fs-gg-sdd-evidence at-scale evidence guidance"
---

# Tasks: fs-gg-sdd-evidence skill — honest partial/at-scale evidence, bulk authoring, deferrals-first-class

**Input**: Design documents in `specs/079-evidence-at-scale-guidance/`
(spec.md, plan.md, research.md, data-model.md, contracts/skill-edit-contract.md, quickstart.md)

**Scope**: documentation-only edit to one authored skill body + re-pin of its two derived
surfaces. No `src/**` behavior, schema, field, flag, stream, or exit-code change (FR-005/FR-009).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe within its phase (no dependency on another incomplete task in the phase).
- **[Story]**: `US1`..`US4` per spec; `SETUP`/`POLISH` for cross-cutting.
- Exact file paths are given for each task.

## Phase 1 — Author the guidance (the single authored source)

All edits target `.claude/skills/fs-gg-sdd-evidence/SKILL.md` (the one canonical body). These are
sub-edits of the same file, so they are **not** parallel to each other; do them in one editing
pass. Constitution Principle IV/V: this is pure documentation — no MVU/`.fsi` obligations apply.

- [X] T001 [US1] In `.claude/skills/fs-gg-sdd-evidence/SKILL.md`, add an **at-scale classification
      workflow** block: how to sweep an auto-expanded obligation graph (the 18→85 case) and mark
      each obligation real-pass (`result: pass`, `synthetic: false`) vs deferral, explicitly keyed
      on the carried `requirementRefs`/`planDecisionRefs` origin refs (no `tasks.yml` title-join),
      grounded in the `result: pass ∧ synthetic: false` rule. Place after the existing
      satisfaction-rule / origin-refs sections. *(FR-001, FR-002; contract C1.1)*
- [X] T002 [US2] In the same file, add/strengthen a **"deferrals are first-class, not failures"**
      statement: a deferral (`result: deferred` / `kind: deferral`) is honest and accepted, a
      shippable work item may carry declared deferrals, and it is preferable to a synthetic pass
      (which never satisfies). Place adjacent to the satisfaction rule / real-vs-synthetic
      discipline. *(FR-003; contract C1.2)*
- [X] T003 [US3] In the same file, add a **bulk-authoring pattern** subsection near the
      command/`--from-tests` docs: fill a large obligation set using only already-shipped
      affordances (`evidence --from-tests <path>`, the carried origin refs, ordinary scripted
      edits) with an explicit honesty caveat — every obligation still needs an individual honest
      classification, never a blanket `pass`. *(FR-004; contract C1.3)*
- [X] T004 [US1][US2][US3] Review the edited body against FR-005/FR-010: it introduces no
      unshipped flag/field/`kind`/`result`/stream/exit, reinforces the satisfaction rule, and
      links (not restates) `fs-gg-sdd-authoring-contracts`, `fs-gg-sdd-verify`, `fs-gg-sdd-tasks`.
      Confirm the frontmatter `description` stays a single line within conventions (edit only if
      warranted). *(FR-005, FR-010; contract C1.4, C1.5)*

## Phase 2 — Re-pin the derived surfaces

Depends on Phase 1 (the body is final).

- [X] T005 [US4] Copy the edited canonical body byte-for-byte over the mirror:
      `.claude/skills/fs-gg-sdd-evidence/SKILL.md` → `.codex/skills/fs-gg-sdd-evidence/SKILL.md`.
      Verify with `diff` (empty output). *(FR-006; contract C2.1)*
- [X] T006 [US4] Regenerate the process manifest sha256:
      `dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write`, updating the
      `fs-gg-sdd-evidence` `sha256` in `.agents/skills/skill-manifest.json`. Confirm the row set
      and schema (v1) are otherwise unchanged. *(FR-007, FR-009; contract C3.1, C3.2)*

## Phase 3 — Verify guards & no collateral change (evidence obligations)

Depends on Phase 2. Real evidence per Constitution Principle VI is the green guard suite; no
synthetic stand-ins.

- [X] T007 [P] [US4] `dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --check`
      exits 0. *(contract C3.1)*
- [X] T008 [P] [US4] `dotnet test tests/FS.GG.Contracts.Tests` green — agent-surface-drift and
      skill-mirror guards pass. *(FR-006, FR-008; contract C2.1, C4.2)*
- [X] T009 [P] [US4] `dotnet test tests/FS.GG.SDD.Commands.Tests` green — process-skill-manifest
      guard + seeding tests pass (embedded resource re-links to the edited body). *(FR-007, FR-008;
      contract C3.1, C3.2, C4.1, C4.3)*
- [X] T010 [US4] Full solution build + `dotnet test` at root green; then `git diff --stat` shows
      changes limited to the two SKILL.md copies, the one manifest `sha256`, the
      `specs/079-evidence-at-scale-guidance/` docs, and the `CLAUDE.md` SPECKIT pointer. No
      `src/**` behavior, other skill body, or schema/fixture churn. *(FR-005, FR-008, FR-009,
      SC-005; contract C4.2, C5.1, C5.2)*

## Phase 4 — Lifecycle close-out (polish)

- [X] T011 [POLISH] Walk `quickstart.md` sections 1–5 end-to-end and check each box; record any
      deviation. *(SC-001..SC-005)*
- [ ] T012 [POLISH] Commit on `079-evidence-at-scale-guidance`, open a PR (`Closes #126`, references
      epic #127), and after merge close out the board item with
      `scripts/fsgg-coord done FS.GG.SDD#126 --flip` (rolls the epic #127 up once this last child
      is Done).

## Dependencies

- Phase 1 (T001–T004) → Phase 2 (T005–T006) → Phase 3 (T007–T010) → Phase 4 (T011–T012).
- T005 depends on the final body from T001–T004. T006 depends on T005 (manifest hashes the final
  on-disk body; mirror must already match). T007–T009 depend on T006. T010 depends on T007–T009.

## Parallel opportunities

- Within Phase 3, T007/T008/T009 are independent read-only checks — `[P]`.
- Phase 1 sub-edits touch the same file and are **not** parallel; do them in one pass.

## MVP scope

US1 + US2 (at-scale classification + deferrals-first-class framing, T001–T002) is the minimum that
delivers the issue's core value; US3 (bulk pattern) and US4 (surface re-pin/guards) complete it.
In practice all four ship together since the derived-surface re-pin (US4) is mandatory for any body
edit to build.

## Task count

- US1: T001, T004 (shared) · US2: T002, T004 · US3: T003, T004 · US4: T005–T010 · POLISH: T011–T012.
- Total: 12 tasks.
