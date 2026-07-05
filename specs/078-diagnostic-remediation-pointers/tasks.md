---

description: "Task list for feature 078 — blocking diagnostics point to their shipped example / grammar section"
---

# Tasks: Blocking diagnostics point to their shipped example / grammar section

**Input**: Design documents from `specs/078-diagnostic-remediation-pointers/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/remediation-pointer.md

**Overall tier**: **T1** (command output contract — correction strings on the shared diagnostic
surface — plus a new internal module and shipped-doc artifacts). Per-phase `[T2]` annotations mark
tasks with no contract change; tier omitted where it matches T1. Tests are REQUIRED (Constitution
Principle VI) and must FAIL before their implementation task.

**MVU note (Principle V)**: no `Model`/`Msg`/`Effect`. Diagnostic construction is already pure;
the pointer suffix is a pure function of the diagnostic id, embedded as string constants. Product
code performs **no** filesystem I/O to resolve pointers — the example/anchor existence checks live
only in the guard **test**.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file / independent)
- **[US1]/[US2]/[US3]**: owning user story
- Exact file paths are given so each task is executable without extra context.

---

## Phase 1: Setup — `RemediationPointers` module skeleton (`.fsi` first)

**Purpose**: Establish the module and its signature before any consumer/test references it
(Constitution Principle I).

- [X] T001 Add `src/FS.GG.SDD.Commands/CommandReports/RemediationPointers.fsi` declaring the
  `internal` surface: the `RemediationPointer` record (`Example: string option`,
  `Anchor: string option`), `val registry: Map<string, RemediationPointer>`, and
  `val suffixFor: id: string -> string`. Doc-comment the invariants from
  `contracts/remediation-pointer.md`.
- [X] T002 Add empty-bodied `src/FS.GG.SDD.Commands/CommandReports/RemediationPointers.fs`
  (registry `Map.empty`, `suffixFor _ = ""`) so the project compiles; register **both** files in
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` immediately **before**
  `CommandReports/DiagnosticConstructors.fs` (compile order matters in F#).

**Checkpoint**: solution builds with the new module present but inert.

---

## Phase 2: US3 (P1) — pointer-resolution guard test [tests first]

**Purpose**: Encode FR-006 / US3 as the failing guard that drives the registry contents. Written
before Phase 3 fills the registry (TDD).

- [X] T003 [US3] Add `tests/FS.GG.SDD.Commands.Tests/RemediationPointersTests.fs` asserting, over
  `RemediationPointers.registry`: (a) **coverage** — every entry renders a non-empty `suffixFor`;
  (b) **example resolves** — every `Example` path exists on disk under `TestSupport.repoRoot`;
  (c) **anchor resolves** — every `Anchor` slug is in the GitHub-slug set computed from the live
  `##`/`###` headings of `docs/reference/authoring-contracts.md`; (d) **determinism** — `suffixFor`
  output contains no absolute path / digit-timestamp / machine-dependent text. Include a small
  in-test `slugify` mirroring GitHub's algorithm (lowercase, drop backticks/punctuation except
  `-`, spaces→`-`). Register the file in `FS.GG.SDD.Commands.Tests.fsproj`.
- [X] T004 [US3] Add to the same test the **containment** and **non-interference** assertions
  (contract invariants 5–6): for a representative covered id per stage, the constructed diagnostic's
  `Correction` ends with `suffixFor id`; and for a representative **non-covered** id
  (`outsideProject`, `missingWorkId`, `toolDefect`), the `Correction` contains no
  `docs/examples/lifecycle-artifacts/` or `authoring-contracts.md#` substring (FR-008).

**Checkpoint**: T003/T004 FAIL (registry empty). This is the executable definition of done for the
covered set.

---

## Phase 3: US1 (P1) — enumerate the registry and wire the suffix into covered corrections

**Purpose**: Deliver the core value — every covered blocking correction gains a resolving pointer.

- [X] T005 [US1] Confirm the exact GitHub slugs for the two backtick headings in
  `docs/reference/authoring-contracts.md`: `` ## `evidence.yml` declarations `` and
  `` ## `specify --input` intent facts ``. Record the confirmed slugs
  (`evidenceyml-declarations`, `specify---input-intent-facts`) as the constants used in T006; if the
  T003 slugifier disagrees, reconcile the slugifier and the constant together.
- [X] T006 [US1] Implement `RemediationPointers.fs`: populate `registry` with the full per-diagnostic
  mapping from `data-model.md` (all rows, all seven stages incl. the `*(agg)*` blocks and the
  `*IdentityMismatch` rows), and implement `suffixFor` per `contracts/remediation-pointer.md`
  (both/one/neither rendering; `""` for unknown ids). Paths are repo-relative POSIX; anchors are the
  full `docs/reference/authoring-contracts.md#<slug>`.
- [X] T007 [US1] In `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs`, append
  `RemediationPointers.suffixFor <id>` (space-separated) to the `correction` of **each covered
  constructor** listed in `data-model.md`. Do this by threading through the shared
  `errorDiagnostic`/`errorForPath`/`errorForRef` helpers keyed on the id, or per call-site — but
  leave every **non-covered** constructor's correction byte-identical (T004 guards this).
- [X] T008 [P] [US1] Add per-stage correction assertions where the stage command tests live
  (`ClarifyCommandTests.fs` scenario for `missingClarificationAnswer`, `ChecklistCommandTests.fs`
  for `failedChecklistPrerequisite`, `EvidenceCommandTests.fs` for
  `evidence.undisclosedSyntheticEvidence`, `PlanCommandTests.fs` for `malformedPlanFrontMatter`,
  `SpecifyCommandTests.fs` for a specify id block) — each asserting the exact expected example path
  and anchor appear in the correction (spec US1 scenarios 1–4, SC-004).

**Checkpoint**: T003/T004 anchor+containment+non-interference assertions pass; example-existence
assertions still fail until Phase 4 lands the three new examples.

---

## Phase 4: US2 (P2) — ship the three missing example artifacts

**Purpose**: Give charter/specify/plan an example to cite so every stage's example pointer resolves
(FR-004/FR-005), and complete the guard's example-existence check.

- [X] T009 [P] [US2] [T2] Author `docs/examples/lifecycle-artifacts/spec.md`: a complete specify
  artifact — required front matter (`stage: specify`), stable US/FR/AC ids, `#specify --input`-shaped
  intent facts, all required sections — that parses via `Specification.parseSpecificationFacts` with
  **zero blocking diagnostics**. Include the established header comment linking
  `authoring-contracts.md` and `[[fs-gg-sdd-specify]]`.
- [X] T010 [P] [US2] [T2] Author `docs/examples/lifecycle-artifacts/plan.md`: a complete plan
  artifact — plan front matter incl. `sourceSpec`/`sourceClarifications`/`sourceChecklist`, valid
  PD/PC/source ids, required sections — parsing via `Plan.parsePlanFacts` with zero blocking
  diagnostics. Header comment linking `authoring-contracts.md` and `[[fs-gg-sdd-plan]]`.
- [X] T011 [P] [US2] [T2] Author `docs/examples/lifecycle-artifacts/charter.md`: a complete charter
  — required front matter (`stage: charter`) plus scope/policy sections — validating via the
  Commands charter front-matter parser with zero blocking diagnostics. Header comment linking
  `authoring-contracts.md` and `[[fs-gg-sdd-charter]]`.
- [X] T012 [US2] Extend `tests/FS.GG.SDD.Artifacts.Tests/ExampleArtifactsContractTests.fs` with two
  cases: `spec.md` parses via `Specification.parseSpecificationFacts` (no blocking diagnostics) and
  `plan.md` via `Plan.parsePlanFacts` (no blocking diagnostics), mirroring the existing four.
- [X] T013 [US2] Add `tests/FS.GG.SDD.Commands.Tests/CharterExampleContractTests.fs` validating
  `charter.md` through the Commands charter front-matter parser (`EarlyStageAuthoring`), asserting no
  blocking (error-severity) diagnostics. Register in the test project.

**Checkpoint**: all seven examples validate; the T003 example-existence assertions now pass — the
guard is fully green (SC-002, SC-003).

---

## Phase 5: Polish — goldens, projections, quickstart, docs currency

**Purpose**: Re-derive goldens for the intended correction delta, confirm projection/lint parity,
and prove FR-008 byte-stability.

- [X] T014 Regenerate/update any JSON or text goldens that embed a **covered** correction (e.g.
  `tests/FS.GG.SDD.Commands.Tests/goldens/**`, rich/text rendering snapshots) to include the new
  suffix; confirm goldens for **non-covered** diagnostics are byte-unchanged (git diff on them must
  be empty — FR-008 / SC-005).
- [X] T015 [P] Confirm projection & lint parity (FR-009): a `--text` and a `--rich` run of a covered
  block show the same pointer, and `fsgg-sdd lint` on the same artifact surfaces the identical
  correction. Add/extend an assertion in the Cli rich/lint tests if not already covered by T008.
- [X] T016 [P] Run the `fsgg-sdd validate` determinism/degradation matrix (or its test) to confirm
  no ANSI-only/JSON-only fact was introduced and output is byte-stable across runs (FR-007).
- [X] T017 [P] [T2] Walk `specs/078-diagnostic-remediation-pointers/quickstart.md` steps 2–5
  against a scratch work item; confirm the canonical TD1 case (SC-004) and the non-covered case
  (step 5) behave as documented.
- [X] T018 [T2] Refresh public-surface/currency guards: `dotnet test` green end-to-end; if the
  internal module unexpectedly widened a baseline, reconcile
  `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` (expected: no change — module is internal).

**Checkpoint**: full suite green; goldens reviewed; determinism confirmed.

---

## Dependencies

- Phase 1 (T001–T002) blocks everything (module must exist).
- Phase 2 (T003–T004) is written before Phase 3 and defines the covered set contract.
- T006 depends on T005 (confirmed slugs); T007 depends on T006; T008 depends on T007.
- The **example-existence** half of the T003 guard depends on Phase 4 (T009–T011).
- T012/T013 depend on their example files (T009/T010 and T011 respectively).
- Phase 5 depends on Phases 3–4 complete.

## Parallel opportunities

- T009, T010, T011 (three independent example files) run in parallel.
- T008 per-stage assertions are independent once T007 lands.
- T015, T016, T017 (verification checks) run in parallel in Phase 5.

## Task count per story

- **US1** (P1, core pointers): T005–T008 (4) + shares T003/T004 guard.
- **US2** (P2, examples): T009–T013 (5).
- **US3** (P1, non-dangling guard): T003–T004 (2), completed by Phase 4.
- Setup/Polish (shared): T001–T002, T014–T018 (7).

## Suggested MVP scope

**US1 + US3** (Phases 1–3 plus the anchor/containment guard) is a shippable increment: every
covered blocking correction points to a resolving grammar anchor, guarded against rot. US2 (the
three new examples) upgrades the pointers to also cite an example for charter/specify/plan and turns
the example-existence guard green — deliver it in the same PR to satisfy the "both when both exist"
rule (clarify Q2) for all seven stages.

## Deviations from plan (recorded during implementation)

All 18 tasks are `[X]`; the following implementation choices differ from the task text and are
recorded here for honesty (Constitution VI):

- **T001 — no separate `.fsi`.** `RemediationPointers` is a `module internal` with **no** `.fsi`,
  matching its sibling `DiagnosticConstructors` (also internal, no `.fsi`). Constitution III's
  `.fsi` requirement is for *public* modules; this module is reached only via
  `InternalsVisibleTo("FS.GG.SDD.Commands.Tests")`. Simpler and locally consistent (Principle IV).
- **T007 — single chokepoint.** The suffix is appended once, in the shared `commandDiagnostic`
  helper every constructor funnels through, keyed on the diagnostic id via `suffixFor` (which
  returns `""` for non-covered ids). One edit wires all covered corrections and provably leaves
  non-covered corrections byte-identical (FR-008).
- **T008 — assertions consolidated.** The per-stage correction assertions live in the
  `RemediationPointersTests` containment test (a representative covered diagnostic per stage ends
  with its exact example+anchor pointer) rather than scattered across the command test files, plus a
  real-CLI US1 exercise captured at `readiness/078-diagnostic-remediation-pointers/us1-cli-evidence.txt`
  (`specify` → `missingSpecificationIntent` JSON carries the pointer; `outsideProject` stays clean).
- **Covered set is error-severity only (44 ids).** The four Warning-severity `stale*` diagnostics
  (`staleChecklistResult`, `stalePlanDecision`, `staleTask`, `verify.staleRequiredTest`) were dropped
  from the data-model set — the spec scopes the pointer requirement to blocking (error) diagnostics.
- **T014 — golden update was a no-op.** No stored golden/fixture embeds a covered correction
  (verified by grep + a green full suite), so no golden needed regenerating; non-covered corrections
  are byte-identical.
- **FR-009 refined to match reality.** The pointer surfaces in the `--json` automation contract
  (the default, and what agents read); `--text`/`--rich` are summaries that do not print
  per-diagnostic corrections. `fsgg-sdd lint` (076) carries its **own** grammar pointer to the
  **same** `authoring-contracts.md` anchors (coherence verified: `acceptance-coverage-line`,
  `clarify-decision-tag-resolution`, `per-stage-front-matter`, `stable-id-declarations`), so this
  feature does not re-plumb lint. Spec/contract/quickstart were updated accordingly.
