---
description: "Task list for feature 063 — surface skillDriftPaths + correct stale report/doc surfaces"
---

# Tasks: Surface skillDriftPaths + correct stale report/doc surfaces

**Input**: Design docs in `specs/063-doctor-drift-and-doc-currency/`
(spec, plan, research, data-model, contracts/projection-contract, quickstart)

**Overall tier**: Tier 1 for the code portion (deliberate text/correction/NextAction
changes); Tier 2 for docs.

**Tests**: Included (Principle VI + the pin test in SC-003).

**MVU note**: Not applicable — pure rendering + static string/list edits.

**Ordering**: US1 (the bug, MVP) → US2 (content corrections) → US3 (docs) → gate.
Every task leaves the build green.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Anchor

- [X] T001 [P] [US1] Confirm branch green before edits: `dotnet build -c Release && dotnet test`.

---

## Phase 2: User Story 1 — drift visible in text + rich (Priority: P1) 🎯 MVP

**Goal**: `doctor`/`upgrade` text & rich projections render `skillDriftPaths`; JSON unchanged.

- [X] T002 [US1] In `src/FS.GG.SDD.Commands/CommandRendering.fs` `renderText`, add to the
  `doctor` block (after `MissingArtifactPaths`) `doctorSkillDrifts: <count>` + one
  `doctorSkillDrift: <path>` per sorted path; add the `upgrade` equivalent
  (`upgradeSkillDrifts`/`upgradeSkillDrift`). Mirror the `missingArtifacts` shape exactly
  (count always emitted; per-path only when non-empty).
- [X] T003 [P] [US1] Extend `tests/FS.GG.SDD.Cli.Tests/RemediationProjectionTests.fs`:
  with non-empty `SkillDriftPaths`, each path appears in `--text` and `--rich`; JSON is
  byte-identical (`serializeReport` unchanged); empty list emits only the count line.
- [X] T004 [US1] No golden update needed — RemediationProjectionTests use `Contains` (not
  exact-match goldens); no doctor/upgrade text golden pins the block. JSON goldens untouched (confirmed).

**Checkpoint**: drift is visible to humans; JSON byte-identical. MVP.

---

## Phase 3: User Story 2 — report content tells the truth (Priority: P2)

- [X] T005 [US2] `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs`:
  extend the `unknownCommand` correction to all 18 accepted commands (init, charter,
  specify, clarify, checklist, plan, tasks, analyze, evidence, verify, ship, agents,
  refresh, scaffold, doctor, upgrade, validate, registry).
- [X] T006 [P] [US2] Pin test (`tests/FS.GG.SDD.Commands.Tests/`): assert the
  `unknownCommand` correction contains each of the 18 command tokens (fails if a command
  is added without updating the correction — SC-003).
- [X] T007 [US2] `src/FS.GG.SDD.Commands/CommandReports/NextActionRouting.fs`: add
  `.agents/skills` to the reseed `NextAction` affected-paths list (kept sorted); assert
  in a Commands test that all three skill roots are present (SC-004).
- [X] T008 [US2] `src/FS.GG.SDD.Commands/CommandReports/ReportAssembly.fs`: add a comment
  at the `ProjectRoot = "."` site documenting the determinism rationale (decoupled from
  the request's possibly-absolute root). No value change (FR-007).
- [X] T009 [US2] No golden update needed — no golden pins the exact `unknownCommand` correction
  string or reseed `NextAction` paths (472 Commands tests stayed green after the edits).

**Checkpoint**: diagnostics/report content are accurate; only enumerated goldens changed.

---

## Phase 4: User Story 3 — docs match the product (Priority: P2)

- [X] T010 [P] [US3] `README.md` + `docs/quickstart.md`: add `doctor` and `upgrade` to the
  described command set.
- [X] T011 [P] [US3] `docs/index.md`: link `reference/doctor-upgrade.md`; remove the
  "starts as an empty Spec Kit product scaffold" claim (product source + tests now exist).
- [X] T012 [P] [US3] `DEVELOPING.md`: list all five projects incl. `FS.GG.Contracts`; name
  the correct props file for the warning ratchet.
- [X] T013 [P] [US3] `.github/workflows/release.yml`: remove/correct the stale hardcoded
  versions in the header comment.

**Checkpoint**: docs describe the shipped product.

---

## Phase 5: Gate

- [X] T014 [US2] Full `dotnet test` green; confirm only the enumerated text/correction/
  NextAction goldens changed and the JSON goldens are untouched (FR-004/FR-012).
- [X] T015 [P] Run `fsgg-sdd validate` → `overallPassed` (SC-006).
- [X] T016 Run `quickstart.md` end-to-end as the acceptance pass.

---

## Dependencies

- Phase 2 (US1), Phase 3 (US2), Phase 4 (US3) are largely independent (different files);
  US3 is pure docs. Phase 5 after all.
- Within US1: T002 → T003/T004. Within US2: T005→T006, T007 self-contained, T008 trivial.

## Suggested MVP

Phase 1 + Phase 2 (US1) — the drift-visibility bug fix. US2/US3 are incremental
correctness + docs.

## Notes

- The one reclassification: `projectRoot` is intentional determinism (comment only) —
  do not change its value.
- CLAUDE/AGENTS drift-guard is out of scope (separate follow-up).


## Implementation notes (2026-07-03)

- **Result**: full suite green — Contracts 86, Artifacts 175, Acceptance 33 (+3 skips), Validation 18, Cli 87 (+3 new drift tests), Commands 474 (+2 new: unknownCommand pin, reseed NextAction). `fsgg-sdd validate` overallPassed (332/0). **Zero golden baselines changed** — JSON is unchanged and nothing pinned the text block/correction/NextAction (FR-012 trivially satisfied).
- **One reclassification**: `projectRoot = "."` is intentional determinism (tests pass an absolute temp-dir root; echoing it would break reproducible JSON) — documented at the code site, not changed. Issue #73's flag on it was a false positive.
- **Rich = text-derived**: adding the drift lines to `renderText` surfaced them in both text and rich via the `Rendering.fs` details table; the rich renderer splits `key: value` into table cells, so rich assertions check value fragments, not the literal `key: value` line.
