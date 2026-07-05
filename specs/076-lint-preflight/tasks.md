---
description: "Task list for 076-lint-preflight"
---

# Tasks: Pre-flight authoring lint

**Input**: Design documents from `specs/076-lint-preflight/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/lint-cli.md, contracts/lint-report.md

**Change tier**: Tier 1 (new verb + flag + JSON report projection + additive request fields).

## Conventions

- **[P]** — parallel-safe (different files, no dependency on another incomplete task in this phase).
- **[US1/US2/US3]** — the user story a task serves. Unmarked = shared/foundational.
- Status: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale).
- Discipline: `.fsi` before `.fs` (Constitution III); failing tests before the body (I/VI); the
  lint I/O path goes through the existing MVU edge — reuse `CommandEffect.ReadFile` /
  `CommandEffects.interpret`, add **no** new effect or interpreter (V).

---

## Phase 1: Setup & public contracts (`.fsi` first) — Foundational

**Purpose**: Sketch the public surface before any body. Blocks all stories.

- [X] T001 Add the `Lint` case to `SddCommand` in `src/FS.GG.SDD.Commands/CommandTypes.fsi` and
  `CommandTypes.fs`: the `"lint" -> Ok Lint` arm in `parseCommand`, `commandName Lint = "lint"`,
  `commandStage Lint = None`, and `nextLifecycleCommand Lint = None` (cross-cutting, FR-009).
- [X] T002 [P] Add additive fields `Artifact: string option` and `Explain: bool` to
  `CommandRequest` in `CommandTypes.fsi`/`CommandTypes.fs` (default `None`/`false`; ignored by
  other commands, mirroring `Provider`/`AssumeYes`). CLI wiring (positional + `--explain`) done in T017/T021.
- [X] T003 Declare the lint data model in `CommandTypes.fsi`/`.fs`: `LintArtifactKind` (renamed
  from `ArtifactKind` to avoid the `Artifacts.ArtifactRef.ArtifactKind` collision, `RequireQualifiedAccess`),
  `LintDefectClass`, `LintOutcome`, `GrammarPointer`, `LintDefect`, and `LintSummary`; added
  `Lint: LintSummary option` to `CommandReport` and `CommandModel`. Full solution builds; 512 Commands tests green (no regression).
> **Verified during implementation (I1 resolved):** by reading the parsers, the diagnostics split as:
> front-matter-incompleteness / duplicate-id / schema / spec-side malformed-coverage-shape
> (`Specification.missingIdDiagnostics`) all live in the parser's `facts.Diagnostics` (single-artifact,
> directly surfaceable); blocking-ambiguity is a single-artifact **count** (`ClarificationFacts.BlockingAmbiguityCount`)
> from which lint synthesizes a `MissingDecisionTag` defect; **checklist FR→AC coverage
> (`failedRequirementsQuality`) is command-layer + cross-artifact and is OUT of lint's single-artifact
> scope** (that reconciliation stays in `checklist`/`analyze`, per research D4). FR-003 is therefore
> anchored to the **spec-side** malformed coverage-line shape, which is real and single-artifact.

- [X] T004 [P] Add `src/FS.GG.SDD.Commands/CommandWorkflow/LintEngine.fsi` — the public signature
  for `detectKind`, `lint` (snapshot → `LintSummary`), and the pure `grammarPointer` lookup.
- [X] T005 Build the solution with the new signatures + `failwith "not implemented"` bodies so the
  surface compiles before logic (Constitution I step 2).

---

## Phase 2: Grammar-pointer map + drift guard — Foundational (supports FR-007)

**Purpose**: The one genuinely-new contract (id/class → doc anchor). Independent of the engine
body, so it lands first and its guard is available to US1.

- [X] T006 Verify/normalize the target anchors in `docs/reference/authoring-contracts.md`
  (`acceptance-coverage-line`, `clarify-decision-tag-resolution`, `per-stage-front-matter`) and the
  tagged fences (`coverage:accepted`, `clarify-decision:resolved`, `front-matter:<stage>`) exist;
  add any missing heading/tag. **(I2)** Ensure the `DuplicateId` pointer targets an *honest*
  anchor for the id-declaration/duplicate rule (add a dedicated heading if the duplicate-id rule is
  not already documented under `per-stage-front-matter`), so the pointer is not misleading.
- [X] T007 Implement the pure `grammarPointer : LintDefectClass -> GrammarPointer option` table in
  `src/FS.GG.SDD.Commands/CommandWorkflow/LintEngine.fs` (map per `contracts/lint-report.md`).
- [X] T008 Add `tests/FS.GG.SDD.Commands.Tests/LintGrammarPointerTests.fs` — assert every mapped
  `anchor` matches a real heading and every `exampleTag` a real tagged fence in
  `authoring-contracts.md` (mirror `AuthoringDocsContractTests` block extraction). **Fails until
  T006/T007 land** (FR-007 drift guard).

---

## Phase 3: US1 (P1) — pre-flight one artifact, four classes caught — **MVP**

**Purpose**: The core value. Ship this and the feature is viable.

### Failing evidence first (Constitution VI)

- [X] T009 [P] [US1] Add broken fixtures under `tests/fixtures/lint/`: `broken-all/checklist.md`
  (malformed coverage line + duplicate id + incomplete front matter), `broken-all/clarifications.md`
  (missing decision tag), a combined manifest for the SC-001 4/4 assertion, and
  `unparseable/garbage.md`.
- [X] T010 [US1] Add `tests/FS.GG.SDD.Commands.Tests/LintTests.fs`: (a) SC-001 — the fixture set
  yields all four `LintDefectClass`es, each with non-empty `Correction` + `GrammarPointer`;
  (b) kind auto-detection (front-matter `stage:` then filename); (c) FR-014 all-at-once (no
  first-defect stop); (d) FR-015 unparseable ⇒ single `Parse`/`Unresolvable` defect;
  (e) SC-005 determinism (byte-identical JSON across two runs); (f) FR-017 — every reported
  `LintDefect.Diagnostic.Severity = Error` (no warnings). **Fails until T011–T013.**
- [X] T011 [US1] Add `tests/FS.GG.SDD.Commands.Tests/LintExitCodeTests.fs` — assert
  `exitCodeForLint` gives 0 (clean) / 1 (defects) / 2 (unusable input) per SC-006. **Fails until
  T015.**

### Implementation

- [X] T012 [US1] Implement `detectKind` in `LintEngine.fs` (D5): parse `Core.frontMatter` `stage:`
  (closed vocab) → kind; else filename/extension (`evidence.yml`/`tasks.yml`/`clarifications.md`/
  `checklist.md`); else `UnusableInput`.
- [X] T013 [US1] Implement `lint` in `LintEngine.fs`: route the snapshot to the live parser(s)
  (`Specification`/`Clarification`/`Checklist`/`Plan`/`Task`/`Evidence`/`WorkItemMetadata`),
  collect their `Error`-severity `Diagnostic list` (D3), classify each into `LintDefectClass`,
  attach `grammarPointer`, order by `(Line, Column, Id)`, and build `LintSummary` with
  `Outcome ∈ {Clean, DefectsFound, UnusableInput}`. Reuse parsers unchanged — derive no grammar.
  **(I1)** For FR-003, confirm the coverage-line defect is detectable **single-artifact** via the
  checklist's own shape check (`missingIdDiagnostics` / strict coverage regex); if the only
  detector is the cross-artifact `requirementCoverage`, add a small single-artifact shape helper
  rather than pulling in the spec — lint stays single-artifact (research D4).
- [X] T014 [US1] Add the read-only MVU handler
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersLint.fs` mirroring `HandlersDoctor`: plan a
  single `ReadFile request.Artifact` effect; on the interpreted snapshot call `LintEngine.lint`;
  assemble `LintSummary` + `Diagnostics` onto the `CommandReport`; emit **no** write. Wire
  `computeLintNext` into `CommandWorkflow.fs` dispatch and the `Lint` `NextAction` in
  `CommandReports/NextActionRouting.fs` (fix-guidance when defects, else `None`).
- [X] T015 [US1] Add emitted-effect assertion to `LintTests.fs` (Constitution V): drive
  `HandlersLint` and assert the effect log contains exactly one `ReadFile` and **no** `WriteFile`/
  `CreateDirectory`/`RunProcess` (proves read-only, FR-008).
- [X] T016 [US1] Serialize `LintSummary` in `CommandSerialization.fs` (deterministic JSON contract)
  and render it in `CommandRendering.fs` (`--text`) and CLI `Rendering.fs` (`--rich`, degrade-safe,
  no JSON-byte change).
- [X] T017 [US1] CLI wiring in `src/FS.GG.SDD.Cli/Program.fs`: parse `lint <artifact>` positional
  onto `CommandRequest.Artifact`; add the bespoke `exitCodeForLint report` branch applied when
  `command = Lint`, leaving all other commands on `exitCodeForReport` (D6).
- [X] T018 [US1] Add CLI golden tests in `tests/FS.GG.SDD.Cli.Tests/` — `lint <broken>` json + text
  projections (stable golden), and the exit codes end-to-end.

**US1 checkpoint**: `fsgg-sdd lint <artifact>` reports the four classes with fix hint + pointer,
read-only, exit 0/1/2. MVP shippable.

---

## Phase 4: US2 (P2) — canonical examples lint clean (no false positives)

**Purpose**: Trust/adoption — zero false positives on the grammar of record.

- [X] T019 [US2] Extend `tests/FS.GG.SDD.Artifacts.Tests/ExampleArtifactsContractTests.fs` (or a new
  `LintCleanExamplesTests.fs` in Commands.Tests driving the CLI) to lint each
  `docs/examples/lifecycle-artifacts/{checklist.md,clarifications.md,evidence.yml,tasks.yml}` and
  assert `LintOutcome = Clean`, zero defects, exit 0 (FR-013/SC-002).
- [X] T020 [US2] If T019 surfaces any false positive, fix the classification/kind-detection in
  `LintEngine.fs` (not the example) so the canonical grammar of record stays clean; re-run.

**US2 checkpoint**: canonical examples pass clean; lint is safe to run as a gate.

---

## Phase 5: US3 (P3) — `<stage> --explain` non-blocking dry run

**Purpose**: In-stage discoverability; strictly additive over US1's engine.

- [X] T021 [US3] Parse `--explain` in `src/FS.GG.SDD.Cli/Program.fs` onto `CommandRequest.Explain`;
  apply the `exitCodeForLint` branch when `request.Explain` too.
- [X] T022 [US3] In the stage handlers (`CommandWorkflow.fs` dispatch for clarify/checklist/specify/
  plan), when `request.Explain` is set: run `LintEngine.lint` over the stage's own artifact, gate
  **off** every mutating effect, and force `NextAction = None` (FR-016 non-blocking dry run).
- [X] T023 [US3] Add tests in `tests/FS.GG.SDD.Commands.Tests/LintTests.fs` (or `LintExplainTests.fs`):
  `<stage> --explain` yields the same defect set as `lint` on that artifact, emits no write effect,
  advances no state (`NextAction = None`), and follows the 0/1/2 exit mapping.

**US3 checkpoint**: authors can pre-flight in-line via `<stage> --explain`.

---

## Phase 6: Polish — docs, skills, baselines, green

- [X] T024 [P] Add `docs/reference/lint.md` — surfaces, the four defect classes, exit codes, and the
  grammar-pointer behavior. **(U1)** Enumerate the exact stage verbs that accept `--explain`
  (clarify/checklist/specify/plan) and note which artifacts `lint <artifact>` recognizes.
- [X] T025 Point the `fs-gg-sdd-troubleshooting` and `fs-gg-sdd-authoring-contracts` skill bodies at
  `lint`/`<stage> --explain` as the pre-flight, byte-identically in `.claude/skills/…` **and**
  `.codex/skills/…`; if a seeded body changes, regenerate the skill-manifest
  (`fsgg-sdd registry skill-manifest --write`, confirm `--check` exits 0).
- [X] T026 Update public surface-area baselines for the changed modules (`CommandTypes`,
  `LintEngine`) per Constitution III.
- [X] T027 Run the `quickstart.md` SC-001..006 checks and a full `dotnet test FS.GG.SDD.sln`; all
  green (LintTests, LintExitCodeTests, LintGrammarPointerTests, extended
  ExampleArtifactsContractTests, CLI golden). Fix or narrow-and-document any red.

---

## Dependencies (beyond phase order)

- T001–T005 (contracts) block everything.
- T006 → T007 → T008 (pointer map before its guard); T007 blocks T013's pointer attach.
- T009 (fixtures) blocks T010/T011/T018/T019/T023 (tests that consume them).
- T012 → T013 → T014 → T017 (detect → engine → handler → CLI); T013 needs T007.
- T015 depends on T014; T016 depends on T013; T018 depends on T016+T017.
- US2 (Phase 4) and US3 (Phase 5) depend on the US1 engine (T013/T014).
- T025 (skills) → T026 (baselines) → T027 (final green).

## Parallel opportunities

- Phase 1: T002 ∥ T004 (distinct files) after T001.
- Phase 3: T009 (fixtures) ∥ writing T010/T011 test skeletons; T024 (docs) is `[P]` any time.
- Phases 4 and 5 are independent of each other once US1 lands (could run in parallel).

## MVP scope

**User Story 1 (Phase 3)** — `fsgg-sdd lint <artifact>` catching all four defect classes with fix
hints + grammar pointers, read-only, exit 0/1/2. US2 (clean-pass guarantee) and US3 (`--explain`)
are additive increments.

## Task counts

- Foundational (Phase 1–2): 8 tasks (T001–T008)
- US1 (Phase 3, MVP): 10 tasks (T009–T018)
- US2 (Phase 4): 2 tasks (T019–T020)
- US3 (Phase 5): 3 tasks (T021–T023)
- Polish (Phase 6): 4 tasks (T024–T027)
- **Total: 27 tasks**
