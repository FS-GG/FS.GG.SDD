---
description: "Task list for Bootstrap and Migration Experience"
---

# Tasks: Bootstrap and Migration Experience

## 📊 Progress — ✅ 20 / 20 complete (100%)

> Legend: ✅ done · 🟡 in progress · ⬜ not started · ⏭️ skipped

| Phase | Tasks | Status |
|---|---|---|
| 1 · Setup | T001–T002 | ✅✅ |
| 2 · Foundational (stage map) | T003 | ✅ |
| 3 · US1 quickstart | T004–T005 | ✅✅ |
| 4 · US2 lifecycle smoke | T006–T012 | ✅✅✅✅✅✅✅ |
| 5 · US3 migration guide | T013–T014 | ✅✅ |
| 6 · US4 adoption note | T015 | ✅ |
| 7 · Polish & validation | T016–T020 | ✅✅✅✅✅ |

**Evidence:** full suite green at **318 tests** (235 command + 83 artifacts), no
`src/`/`.fsi` surface change (`SurfaceBaselineTests` unmodified). New
`LifecycleSmokeTests.fs` adds 12 passing in-process smoke cases. Real CLI process
smoke (`readiness/cli-smoke.txt`) reaches `shipReady` with no Governance.
Validation notes in `readiness/quickstart-validation.md`; suite output in
`readiness/full-suite.txt`.

**Input**: Design documents from `specs/016-bootstrap-migration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/consumer-docs.md, contracts/lifecycle-smoke.md,
contracts/bootstrap-assertions.md

**Change Tier**: Tier 1 (contracted). All phases match the spec's overall tier;
no per-task `[T1]`/`[T2]` annotations are needed.

**Nature of this feature**: This feature **adds no new lifecycle stage, command,
`.fsi` surface, authored-source schema, or generated-view schema** (FR-012). It
ships three consumer documentation surfaces under `docs/` and one automated
in-process lifecycle smoke that pins the documented behavior to real command
output. The only F# change is the new test file; `src/` is untouched.

**Tests**: The lifecycle smoke (US2) is a first-class spec deliverable
(FR-004..FR-006, FR-011, FR-013, FR-014), not optional test scaffolding — its
tasks are core implementation. No other test tasks are added because no new
public surface is introduced.

**Elmish/MVU applicability (Principle IV/V)**: Not applicable as new work. The
feature introduces no new stateful or I/O workflow; the smoke drives the
**existing** MVU command workflow through the existing `TestSupport`
init/update/effect interpreter helpers, and the CLI process evidence uses the
shipped executable boundary. No separate MVU-contract task is needed; this is
realized by the smoke harness (T007) and the evidence-capture tasks (T012, T018).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another
  incomplete task in this phase)
- **[Story]**: `US1`..`US4`; unlabeled tasks are shared/cross-cutting
- Paths are repository-relative from `/home/developer/projects/FS.GG.SDD`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the baseline and the conventions the deliverables must match.

- [x] T001 Confirm the full test suite is green at baseline by running
  `dotnet test FS.GG.SDD.sln` and recording the pass count; this is the
  regression baseline the new smoke must preserve (no `src/` change is
  permitted, so `SurfaceBaselineTests` and all command tests must stay green).
- [x] T002 [P] Capture the shared documentation conventions for the three new
  `docs/*.md` files: FsDocs-style frontmatter (`title`, `category`,
  `categoryindex`, `index`, `description`) consistent with
  `docs/initial-implementation-plan.md`; repository-relative `/` paths; no
  FS.GG.Rendering package names, templates, or docs URLs; no monorepo/runtime-
  template assumptions (per contracts/consumer-docs.md "Shared rules"). Record
  the next free `index`/`categoryindex` values by inspecting existing `docs/`
  frontmatter so the new docs slot in cleanly.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the single authoritative stage map that both the
quickstart (US1) and the smoke (US2) consume, so the docs and the test cannot
drift from each other or from command behavior (FR-014).

**⚠️ CRITICAL**: T003 must complete before US1 (T004) and US2 (T006–T007) so the
canonical order, per-stage source/view, and emitted next-action pointers are
derived from the real command source rather than restated by hand in two places.

- [x] T003 Derive the authoritative per-stage reference from the command source
  under `src/FS.GG.SDD.Commands/` (read-only; no `src/` edits): for each
  lifecycle stage `charter → specify → clarify → checklist → plan → tasks →
  analyze → evidence → verify → ship`, record (a) the authored source path it
  writes under `work/<id>/` (`charter.md`, `spec.md`, `clarifications.md`,
  `checklist.md`, `plan.md` + `contracts/`, `tasks.yml`, `evidence.yml`), (b) the
  generated readiness view it refreshes or reports under `readiness/<id>/`
  (`analysis.json`, `verify.json`, `ship.json`, `work-model.json`,
  `summary.md`, `agent-commands/<target>/`), and (c) the exact emitted
  `nextAction` / `nextLifecycleCommand` value, including the cross-cutting
  `agents` and `refresh` generators (`nextLifecycleCommand = None`). Capture this
  table as a working note appended to `specs/016-bootstrap-migration/research.md`
  (scratch grounding for this feature; not a shipped artifact). This is the
  shared source of truth for T004, T006, and T007.

**Checkpoint**: Canonical stage map confirmed against real command output — both
P1 stories can proceed without restating ordering independently.

---

## Phase 3: User Story 1 - Init Through Ship Without Governance (Priority: P1) 🎯 MVP

**Goal**: A consumer quickstart that walks a new user from `fsgg-sdd init`
through `fsgg-sdd ship` for one work item with no Governance gate runtime, naming
for each stage the authored source written and the generated readiness view
refreshed or reported, and showing where `fsgg-sdd agents`/`fsgg-sdd refresh`
bring agent guidance and `summary.md` to currency.

**Independent Test**: Follow `docs/quickstart.md` in an empty directory with no
Governance files; confirm the lifecycle proceeds in canonical order, each stage
names its authored source and generated readiness view, and the run yields the
SDD-owned readiness artifacts without Governance (FR-001, FR-002, FR-003, SC-001,
SC-008).

- [x] T004 [US1] Author `docs/quickstart.md` using the T003 stage map and the
  T002 conventions, with the sections required by contracts/consumer-docs.md:
  (1) Prerequisites — the SDD CLI only, with an explicit statement that no
  Governance gate runtime, FS.GG.Rendering package, or monorepo checkout is
  required (FR-013); (2) `fsgg-sdd init` — the skeleton created (`.fsgg` config,
  `work/`, `readiness/`, agent guidance targets) and the next action; (3) the
  lifecycle in canonical order `charter → specify → clarify → checklist → plan →
  tasks → analyze → evidence → verify → ship`, each stage naming its authored
  source, its generated readiness view, and the command's emitted next action
  (FR-002); (4) cross-cutting `fsgg-sdd agents` and `fsgg-sdd refresh` bringing
  agent guidance and `readiness/<id>/summary.md` to currency
  (`nextLifecycleCommand = None`); (5) Result — the readiness artifacts
  (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`, `summary.md`,
  `agent-commands/`) and a pointer to the Governance-owned protected-boundary
  handoff as an optional next step (FR-003).
- [x] T005 [US1] In `docs/quickstart.md`, frame every generated readiness view
  as an output whose currency comes from running `fsgg-sdd refresh`, not from
  file presence alone (FR-015), and frame all Governance references as optional
  compatibility facts — SDD never evaluates or enforces routing, freshness,
  profiles, gates, audit, or release decisions (FR-016). Verify the documented
  stage order and next-action pointers match the T003 map verbatim (FR-014).

**Checkpoint**: `docs/quickstart.md` is a complete, self-consistent init→ship
walkthrough; its claims will be pinned to command output by the US2 smoke.

---

## Phase 4: User Story 2 - Automated No-Governance Lifecycle Smoke (Priority: P1)

**Goal**: An automated in-process smoke that creates a disposable SDD project,
drives `init → … → ship` plus `agents` and `refresh` over the existing command
workflow, asserts each stage's authored source and generated readiness view,
asserts the documented next-action chain, asserts no Governance is required, and
asserts determinism across two runs.

**Independent Test**: Run `dotnet test` and confirm `LifecycleSmokeTests` creates
a temp project, runs the full lifecycle plus generators, asserts the expected
readiness artifacts, and completes with zero Governance policy/capability/tooling
files present (FR-004..FR-006, FR-011, FR-013, FR-014, SC-002, SC-003).

> Depends on T003 (canonical map) for the next-action chain assertion. All
> assertion tasks T008–T012 edit the same new file `LifecycleSmokeTests.fs` and
> therefore run **sequentially after** the harness task T007, not in parallel.

- [x] T006 [US2] Register the new test file in
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj`: add
  `<Compile Include="LifecycleSmokeTests.fs" />` immediately before
  `SurfaceBaselineTests.fs` (after `GovernanceBoundaryCommandTests.fs`), and
  create the empty `tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs` with
  the module header and xUnit/`TestSupport` opens, reusing the existing harness
  unchanged.
- [x] T007 [US2] Implement the happy-path lifecycle drive in
  `LifecycleSmokeTests.fs`: from `TestSupport.tempDirectory()` create one
  disposable project and one work id, then call `initializeProject → runCharter
  → runSpecify → runClarify → runChecklist → runPlan → runTasks → runAnalyze →
  runEvidence → runVerify → runShip`, then `runAgents` and `runRefresh`. Assert
  each stage returns a success / non-blocked outcome and writes its authored
  source and generated readiness view per contracts/lifecycle-smoke.md §1
  (charter→`work/<id>/charter.md` … evidence→`evidence.yml`;
  analyze→`readiness/<id>/analysis.json`; verify→`verify.json`; ship→`ship.json`;
  `work-model.json` present and current; agents→`agent-commands/<target>/` for
  each configured target; refresh→`summary.md` plus a current cross-view report).
- [x] T008 [US2] Add the canonical-order + next-action-chain assertion (FR-014,
  family C) to `LifecycleSmokeTests.fs`: assert each stage's emitted `nextAction`
  / `nextLifecycleCommand` matches the T003-documented quickstart chain
  (`charter→specify→…→ship`; `analyze→evidence→verify→ship`; `agents` and
  `refresh` cross-cutting with `nextLifecycleCommand = None`), so any behavioral
  change to ordering or pointers breaks this assertion and forces a doc update.
- [x] T009 [US2] Add the no-Governance assertion (FR-005, family A) and the
  well-formed-readiness assertion (contracts/lifecycle-smoke.md §5) to
  `LifecycleSmokeTests.fs`: after a full run, assert no `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml` exists or was required; assert
  each readiness JSON parses and carries its generation manifest (sources, source
  digests, schema version, generator identity) and that `summary.md` is marked
  generated and records its sources.
- [x] T010 [US2] Add the determinism assertion (FR-006, family B) to
  `LifecycleSmokeTests.fs`: run the full lifecycle twice over two temp projects
  with identical authored inputs and assert byte-identical machine-readable
  readiness (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`, and
  the refresh report JSON) after path-normalizing the temp root. Exclude implicit
  clocks, durations, terminal width, ANSI styling, directory enumeration order,
  host path separators, random values, and absolute host paths from the compared
  output (determinism constraints).
- [x] T011 [US2] Add the Governance-present-but-incomplete case (FR-011, family
  D) and the no-Rendering / no-monorepo assertion (FR-013, family E) to
  `LifecycleSmokeTests.fs`: with deliberately present-but-incomplete
  `.fsgg/policy.yml` / `.fsgg/capabilities.yml` / `.fsgg/tooling.yml` placed in a
  temp project, assert every SDD lifecycle command still succeeds and performs no
  Governance evaluation/enforcement; assert the run depends only on the SDD
  projects, referencing no FS.GG.Rendering package, monorepo checkout, or runtime
  product template (FR-016, SC-006, SC-007).
- [x] T012 [US2] Run `dotnet test FS.GG.SDD.sln` and confirm the new smoke cases
  pass within the <10s in-process budget and the full suite (including
  `SurfaceBaselineTests`) remains green, proving no `src/`/`.fsi` surface drift
  (FR-012). Capture the run output as readiness evidence under
  `specs/016-bootstrap-migration/readiness/`.

**Checkpoint**: The documented bootstrap experience is automatically verified and
cannot silently diverge from command behavior. US1 + US2 together form the
shippable P1 MVP.

---

## Phase 5: User Story 3 - Migrate an Existing Spec Kit Project (Priority: P2)

**Goal**: Additive, non-destructive migration guidance that maps an existing
Spec Kit project's `specs/` and `.specify/` artifacts onto native SDD `.fsgg` and
`work/<id>` sources while preserving standard Spec Kit as a valid workflow.

**Independent Test**: Apply the guidance against a representative Spec Kit project
and confirm the documented steps add SDD artifacts while leaving `specs/` and
`.specify/` content unchanged, with standard Spec Kit still valid afterward and
re-application safe (FR-007..FR-009, SC-005).

- [x] T013 [US3] Author `docs/migration-from-spec-kit.md` per
  contracts/consumer-docs.md and family F, with sections: (1) Starting point — an
  existing Spec Kit project with `specs/` and `.specify/`; (2) Additive setup —
  run `fsgg-sdd init` to create `.fsgg`, `work/`, `readiness/` without touching
  `specs/` or `.specify/`; (3) Artifact mapping table — Spec Kit feature
  artifacts (`spec.md`, `plan.md`, clarifications, checklist, `tasks.md`,
  evidence) → native `work/<id>/` authored sources, authored **through the
  `fsgg-sdd` commands** (no new migration command, FR-012); (4) No-equivalent
  handling — represent in the nearest SDD source or explicitly defer, never
  delete authored content; (5) Coexistence — standard Spec Kit remains valid and
  the steps are safe to re-apply (FR-008, FR-009). Use the T002 frontmatter
  conventions.
- [x] T014 [US3] Verify the migration guide by inspection against
  contracts/consumer-docs.md and family F: every documented step is additive
  (`init` plus authoring via `fsgg-sdd` commands), no step deletes, rewrites,
  reorders, or normalizes `specs/` or `.specify/` content (FR-008), the re-apply
  safety and represent-or-defer guidance are present (FR-009), and no step
  implies a new command performs migration (FR-012). Record the review as
  quickstart-validation evidence.

**Checkpoint**: Existing Spec Kit users have a documented, non-destructive
adoption path; the repository's own Spec Kit workflow is unaffected.

---

## Phase 6: User Story 4 - Adopt Optional Governance After Init (Priority: P3)

**Goal**: Documentation that Governance policy/capability/tooling files are added
after `fsgg-sdd init` as an optional, additive layer that never changes SDD
command usability.

**Independent Test**: Follow the documented adoption steps in an SDD-initialized
project and confirm adding the optional Governance files does not change SDD
command behavior, and SDD commands still run when those files are absent or
incomplete (FR-010, FR-011, SC-006). The smoke's family D case (T011) provides
the behavioral backbone for this doc claim.

- [x] T015 [US4] Author `docs/adopting-governance.md` per
  contracts/consumer-docs.md with sections: (1) after `fsgg-sdd init`, Governance
  owners may add `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and
  `.fsgg/tooling.yml` (FR-010); (2) Usability guarantee — every SDD lifecycle
  command stays usable whether those files are present, absent, or incomplete
  (FR-011); (3) Boundary — SDD reports readiness while Governance owns routing,
  effective-evidence freshness, profiles, gates, audit, and release enforcement,
  and SDD does not evaluate or enforce them (FR-016). Present all Governance
  references as advisory compatibility facts only; use T002 frontmatter
  conventions.

**Checkpoint**: All four user stories are independently documented/verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cross-links, executable-path evidence, and final validation. These
touch shared files (`docs/index.md`, `README.md`) and run after the per-story
docs exist to avoid edit conflicts.

- [x] T016 Update `docs/index.md` to link all three new docs
  (`quickstart.md`, `migration-from-spec-kit.md`, `adopting-governance.md`)
  without removing or restructuring existing content (contracts/consumer-docs.md
  "Cross-links").
- [x] T017 [P] Update the `README.md` Workflow section to link the quickstart and
  the migration guide, without removing or restructuring existing content.
- [x] T018 [P] Capture CLI process evidence: run the shipped `FS.GG.SDD.Cli`
  executable `init` through `ship` over a disposable directory with JSON output,
  per contracts/lifecycle-smoke.md "CLI process evidence", and save the transcript
  as `specs/016-bootstrap-migration/readiness/cli-smoke.txt` to prove the
  executable path (Constitution VI). This is evidence, not part of the
  deterministic assertion suite.
- [x] T019 Run quickstart validation: follow `docs/quickstart.md` end-to-end and
  confirm SC-001/SC-004/SC-007/SC-008 — the documented stages and next-action
  pointers match the smoke's asserted chain (T008), and the walkthrough completes
  with no Governance, FS.GG.Rendering, or monorepo checkout. Record the result as
  readiness evidence under `specs/016-bootstrap-migration/readiness/`.
- [x] T020 Final regression: run `dotnet test FS.GG.SDD.sln` once more and
  confirm the full suite is green with the new smoke included and `src/`/`.fsi`
  surface unchanged (`SurfaceBaselineTests` unmodified), satisfying FR-012 and the
  Constitution VI green-suite requirement.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2 / T003)**: depends on Setup; **blocks** the next-action
  accuracy of US1 (T004–T005) and US2 (T007–T008).
- **US1 (Phase 3)** and **US2 (Phase 4)**: both P1; both depend on T003. They can
  proceed in parallel (different files: `docs/quickstart.md` vs.
  `LifecycleSmokeTests.fs`), but the smoke (US2) is what *verifies* US1, so US1's
  final wording (T005) should be reconciled against the smoke's asserted chain.
- **US3 (Phase 5)** and **US4 (Phase 6)**: depend only on Setup/Foundational; each
  is an independent doc and can run in parallel with each other and with US1/US2.
- **Polish (Phase 7)**: depends on all desired story docs existing (T016/T017
  link them) and on the smoke (T018–T020 capture/validate).

### Within US2 (single file)

- T006 (scaffold + fsproj) → T007 (harness/happy path) → T008, T009, T010, T011
  (assertion families) → T012 (run + evidence). T008–T011 edit the same file and
  run sequentially; they are **not** `[P]`.

### Parallel opportunities

- T002 [P] runs alongside T001.
- After T003: `docs/quickstart.md` (US1), `docs/migration-from-spec-kit.md`
  (US3), and `docs/adopting-governance.md` (US4) are independent files and can be
  authored in parallel; the US2 smoke can be built in parallel with all three.
- In Polish, T017 and T018 are [P] (README vs. CLI evidence capture); T016 edits
  `docs/index.md` and the final validations T019/T020 run last.

---

## Implementation Strategy

### MVP (P1: US1 + US2)

1. Phase 1 Setup → Phase 2 Foundational (T003 canonical map).
2. US1 (`docs/quickstart.md`) and US2 (`LifecycleSmokeTests.fs`) together: the
   doc states the bootstrap experience and the smoke pins it to command output.
3. **STOP and VALIDATE**: `dotnet test` green with the smoke; quickstart followed
   end-to-end with no Governance. This is the shippable bootstrap promise.

### Incremental delivery

1. MVP (US1 + US2) → the verified no-Governance init→ship experience.
2. Add US3 migration guide → existing Spec Kit users get a non-destructive path.
3. Add US4 adoption note → optional Governance-after-init boundary documented.
4. Polish: cross-links, CLI evidence, final validation.

---

## Notes

- No `src/` module, `.fsi` signature, public API baseline, or structured contract
  changes (FR-012). The only F# change is the new `LifecycleSmokeTests.fs`; keep
  `SurfaceBaselineTests` green and unmodified.
- Markdown docs are authoring surfaces (Constitution II); the schema-versioned
  readiness views remain the machine contract the smoke asserts over.
- Keep the quickstart, migration guide, and adoption note free of FS.GG.Rendering
  package names, templates, and docs URLs (CLAUDE.md boundary; FR-013).
- Never mark a failing task `[X]`. Never weaken a smoke assertion to green the
  build — narrow scope and document it. Mark `[-]` with written rationale if a
  task is skipped.
