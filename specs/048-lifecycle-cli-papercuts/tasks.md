---
description: "Task list for Lifecycle/CLI Semantics Papercuts"
---

# Tasks: Lifecycle/CLI Semantics Papercuts

**Feature**: `048-lifecycle-cli-papercuts` | **Branch**: `048-lifecycle-cli-papercuts`

**Input**: Design documents from `specs/048-lifecycle-cli-papercuts/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included and **mandatory** — Constitution Principle VI requires failing-first
test evidence for every papercut. Two existing tests encode the current buggy behavior and
are rewritten (see T002, T009).

**Tier**: Spec overall tier is **T1 (contracted)** — the only public-surface change is the
additive `CommandReport.Help` jsonField + `HelpSummary` type + new `CommandHelp` module
(US4). All phases inherit T1, so no per-task `[T1]/[T2]` annotation is emitted.

**Elmish/MVU applicability**: All five fixes are **pure transitions over already-loaded
snapshots** with **no new I/O edge** (plan Constitution V: PASS). §3.1/§3.2/§3.3/§3.4 are
pure functions of source bytes; §3.5 help is pure CLI dispatch + pure projection over
`CommandReport`. No new `.fsi` Model/Msg/Effect/interpreter boundary is introduced; the only
`.fsi` deltas are the additive report type/module in US4. Principle IV (no new I/O edge) is
therefore **not applicable** to any story beyond the existing interpreter.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (usually a
  different file).
- **[Story]**: `[US1]`..`[US4]` maps the task to its user story.
- Status: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (with rationale).

## Path Conventions

Single-project layout (existing): `src/`, `tests/`, `docs/` at repository root.

---

## Phase 1: Setup

**Purpose**: Establish a known-green baseline before changing behavior.

- [X] T001 Build the solution and run the focused suites that gate this feature, recording
  the current state of the two tests slated for rewrite (T002, T009) as the failing-first
  baseline: `dotnet build FS.GG.SDD.sln` then `dotnet test tests/FS.GG.SDD.Commands.Tests`,
  `dotnet test tests/FS.GG.SDD.Artifacts.Tests`, `dotnet test tests/FS.GG.SDD.Cli.Tests`,
  `dotnet test tests/FS.GG.SDD.Validation.Tests`.

---

## Phase 2: Foundational

**Purpose**: Cross-story prerequisites.

**None.** The five papercuts are independent seams located in Phase 0 (research.md D1–D5);
each user story can be implemented and tested in isolation. The only cross-story coupling is
mechanical: US4 adds the always-present `help` jsonField, which appends `"help": null` to
**every** command's JSON golden (including the checklist/ship goldens touched by US1/US2).
That golden regeneration is sequenced into Polish (T024) to run once after all behavior
changes land — it is not a blocking prerequisite for any story's logic.

**Checkpoint**: Foundation ready — all four user stories can begin (in parallel if staffed).

---

## Phase 3: User Story 1 — Fix-and-re-run reflects the fix truthfully (Priority: P1) 🎯 MVP

**Covers**: §3.1 stale `checklist` rows (FR-001, SC-001) and §3.2 `specify` silent no-op
(FR-002, SC-002).

**Goal**: After an author corrects an authored source and re-runs the stage, the report and
rendered artifact reflect the corrected source — never a stale prior verdict.

**Independent Test**: Drive a work item to a failing/passing checklist state; change the
underlying source so the verdict should flip; re-run; assert the report and `checklist.md`
both reflect the current source with no superseded rows. For `specify`: edit `spec.md` at
`status: specified`, re-run, assert the report is never a bare ambiguous `NoChange`.

### Tests for User Story 1 (write first; must FAIL before T006/T007)

- [X] T002 [US1] **Rewrite** the append-stale test to assert purge-and-re-derive in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` (currently `:186-200`,
  `checklist appends safe missing requirement item and marks prior result stale`): after a
  stale re-run, **zero** rows derived from the superseded snapshot survive and the report
  matches current sources (contract: checklist-rerun-semantics.md).
- [X] T003 [US1] Add a **partial-fix** test in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`: fix one of several `fail`
  requirements, re-run → corrected requirement flips to `pass`, still-failing requirements
  remain `fail`, status `needsCorrection` (per-requirement re-evaluation).
- [X] T004 [P] [US1] Add an **unchanged-re-run determinism** test in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`: re-run with no source change →
  `outcome: noChange`, byte-identical output, rows preserved (FR-012).
- [X] T005 [P] [US1] Add an **edited-content re-run** test in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs`: with a spec at
  `status: specified`, edit `spec.md`, re-run `specify` → the report either reflects the
  re-parsed facts **or** carries the deterministic statement that `specify` promotes only
  the first draft and that downstream stages read the live file (SC-002); never a bare
  ambiguous `NoChange`.

### Implementation for User Story 1

- [X] T006 [P] [US1] §3.1 — In `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs`
  (`checklistDiagnosticsTextAndSummary`, `:347-417`): when `sourceSnapshotStale` is true,
  **purge all machine-derived result rows and re-derive the full set** from current sources
  via the `plannedChecklistReviews` derivation used by the fresh `checklistTemplate` path
  (no `existingSourceIds` filter), then rewrite `## Source Snapshot` to the current digests.
  Preserve authored, non-derived sections (`ensureChecklistSections`). Leave the
  snapshot-current preserve/`noChange` path unchanged.
- [X] T007 [P] [US1] §3.2 — In `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingEarly.fs`
  (specify path, summary built `~:489-528`): when `Command = Specify` and the outcome is
  `NoChange` over an existing spec, populate a deterministic `NextAction`/advisory fact
  stating `specify` promotes only the first draft and that `spec.md` is read live by
  downstream stages (`clarify`, `checklist`, …). No authored bytes are written or
  re-promoted (data-model §2; research D2).

**Checkpoint**: US1 fully functional — `checklist` re-run purges stale rows; `specify`
re-run is never silently ambiguous. MVP deliverable.

---

## Phase 4: User Story 2 — A correct, complete run ends clean (Priority: P2)

**Covers**: §3.4 self-inflicted `staleGeneratedView` on `verify`/`ship` (FR-005/006/007,
SC-004).

**Goal**: A fully-correct `verify`/`ship` run ends clean — no `staleGeneratedView` advisory
whose sole cause is the stage writing its own readiness file — while genuine upstream
staleness is still reported.

**Independent Test**: Drive a work item to a verification/ship-ready state with a current
work model; run the stage; assert no self-inflicted advisory. Then edit an upstream authored
source and re-run; assert the advisory still fires.

### Tests for User Story 2 (write first; must FAIL before T011)

- [X] T008 [P] [US2] Add a clean-run test in
  `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`: `verify` on a current work model
  carries **no** `staleGeneratedView` advisory and does not end `SucceededWithWarnings` for
  that cause.
- [X] T009 [US2] **Rewrite** the advisory expectation in
  `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs` (currently `:80-83`, expecting an
  `advisory` disposition): clean `ship` on a current work model reports `shipReady`. Add a
  **genuine-upstream-staleness** case: edit `spec.md` after generation, re-run → `verify`/
  `ship` still emit `staleGeneratedView` (FR-007).
- [X] T010 [P] [US2] Add a snapshot-set-parity expectation in
  `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`: the currency-check input
  set equals the generation source set (incl. `plan.md`/`charter.md`); a model generated and
  checked over the full set reports clean.

### Implementation for User Story 2

- [X] T011 [US2] §3.4 — In `src/FS.GG.SDD.Commands/CommandWorkflow/ViewGeneration.fs`
  (`existingGeneratedViewDiagnostic`, `currentSnapshots` at `:452-461`): add `planPath workId`
  and `charterPath workId` so `currentSnapshots` mirrors the exact authored-source set used
  by `workModelSnapshots` (`:476-502`). Do **not** touch the staleness predicate
  (`generatorStale || sourceStale || outputDigestStale`). Genuine source-digest drift still
  flags via `sourceStale` (contract: workmodel-currency-snapshot-set.md).

**Checkpoint**: US1 + US2 work independently — clean verify/ship need no trailing `refresh`;
real staleness still reported.

---

## Phase 5: User Story 3 — "No open questions" without blocking (Priority: P3)

**Covers**: §3.3 ambiguity-disclaimer blocks `clarify` (FR-003/004, SC-003).

**Goal**: A "none outstanding" note under `## Ambiguities`, prose or bullet, never becomes a
blocking ambiguity, while genuine `AMB-###` ambiguities still block.

**Independent Test**: Author a spec whose `## Ambiguities` states "none outstanding" as a
bullet; run `clarify` → no block. Replace with a real `- AMB-001 …` → still blocks; mixed
content blocks on the real one only.

### Tests for User Story 3 (write first; must FAIL before T014)

- [X] T012 [P] [US3] Add parse-level cases in
  `tests/FS.GG.SDD.Artifacts.Tests/SpecificationArtifactTests.fs`: bullet disclaimer
  (`- None outstanding`) under `## Ambiguities` → `AmbiguityIds = []`; prose disclaimer
  (regression) → `[]`; genuine `- AMB-001 …` → one `AmbiguityId`; mixed (disclaimer +
  `- AMB-001 …`) → only the real id (contract: ambiguity-disclaimer.md table).
- [X] T013 [P] [US3] Add command-level cases in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`: disclaimer-bullet spec → `clarify`
  proceeds, no blocking ambiguity; genuine `AMB-001` spec → `clarify` still blocks
  (`BlockingAmbiguityCount > 0`).

### Implementation for User Story 3

- [X] T014 [US3] §3.3 — In
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Specification.fs`: teach `missingIdDiagnostics`
  (`:84-102`) and the `## Ambiguities` id extraction (`:176`) to recognize a
  **no-outstanding sentinel** — after stripping an optional leading `- `, trimmed text empty
  or matching the existing disclaimer convention (case-insensitive `StartsWith "No "` /
  "none outstanding") used by `parseNonEmptySectionLines` (`Internal.fs:211-218`). Sentinel
  lines are exempt from the "every bullet needs an `AMB-###`" rule and yield no
  `AmbiguityId`; reuse/extend the `Internal.fs` convention (a shared helper is acceptable).
  Lines bearing `AMB-###` and non-sentinel id-less bullets are unchanged.

**Checkpoint**: US1 + US2 + US3 independently functional.

---

## Phase 6: User Story 4 — `--help` discoverability (Priority: P3)

**Covers**: §3.5 `--help` returns `unknownCommand` (FR-008–011, SC-005/006).

**Goal**: `fsgg-sdd --help`, `-h`, `help`, and `<command> --help` return usage/flag content
through the standard three-way projection and exit 0; genuinely unknown commands still report
`unknownCommand`.

**Independent Test**: Invoke top-level and per-command `--help`; assert usage/flag content
and exit 0 with zero `unknownCommand`. `frobnicate` and `frobnicate --help` → `unknownCommand`
exit 1.

### Contract / `.fsi` surface for User Story 4 (Principle III)

- [X] T015 [US4] Add the additive report types and field in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` and `CommandTypes.fs`: `HelpFlag`,
  `HelpCommandEntry`, `HelpScope` (`TopLevel | Command of string`), `HelpSummary`, and
  `CommandReport.Help: HelpSummary option` (data-model §5). `schemaVersion` stays `1`.
- [X] T016 [US4] Create the new module `src/FS.GG.SDD.Commands/CommandHelp.fsi` and
  `CommandHelp.fs`: static `globalFlags`, `commandEntries` (14 `SddCommand` cases + the
  CLI-level peers `version`/`validate`/`registry`), `commandFlags: SddCommand -> HelpFlag list`,
  `topLevelHelp: GeneratorVersion -> HelpSummary`, `commandHelp: SddCommand -> HelpSummary`.
  All content static (no clock/path/env) for determinism. Register the file in the project's
  `.fsproj` compile order before `CommandSerialization.fs`/`CommandRendering.fs`.

### Tests for User Story 4 (write first; must FAIL before T019–T022)

- [X] T017 [P] [US4] Create `tests/FS.GG.SDD.Commands.Tests/HelpCommandTests.fs`: top-level
  `--help`/`-h`/`help` → `HelpScope = TopLevel`, command list includes every lifecycle
  command + `version`/`validate`/`registry` + global flags, `outcome = NoChange`, exit 0;
  `<command> --help` for each command → `HelpScope = Command name` with that command's flags;
  `frobnicate` and `frobnicate --help` → `unknownCommand` exit 1 (FR-011). Include a
  **help-precedence** case (spec edge): `fsgg-sdd --help --json` and `fsgg-sdd verify --help`
  resolve to help and never fall through to command execution or `unknownCommand`.
- [X] T018 [P] [US4] Create `tests/FS.GG.SDD.Cli.Tests/HelpRenderingTests.fs`: `--help --json`
  (canonical, byte-identical across runs), `--text` (portable plain text), and `--rich`
  (Spectre projection deriving from text, adding/dropping no facts) projections; `--rich`
  degrades to zero-ANSI under non-interactive / `NO_COLOR` / `TERM=dumb` (FR-010).

### Implementation for User Story 4

- [X] T019 [P] [US4] In `src/FS.GG.SDD.Commands/CommandSerialization.fs`: add `writeHelp`
  following the `writeScaffold` convention — emit the `help` object when `Some`, `null` when
  `None`, always present.
- [X] T020 [P] [US4] In `src/FS.GG.SDD.Commands/CommandRendering.fs`: emit help lines from
  `renderText` when `Help` is present (usage, commands, global flags, command flags); the
  `--rich` projection auto-derives from the text projection.
- [X] T021 [US4] In `src/FS.GG.SDD.Cli/Program.fs`: add a help dispatch branch as a peer of
  `--version`/`validate` (around `:139` where `--help`/`-h` currently fall through to
  `printUnknown`). Top-level `--help`/`-h`/`help` (no command) → `topLevelHelp`
  (envelope `Command = Init`); `<known> --help`/`-h` → detect help in `rest`, build
  `commandHelp`; unknown command (± `--help`) → `unknownCommand` exit 1 (FR-011). Build via
  `buildReport` (no diagnostics → `NoChange` → exit 0) and route to **stdout** via
  `resolve format (detectCapabilities()) report`.
- [X] T022 [US4] Update `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` for the new
  `CommandHelp` module and the `CommandTypes` additions; run the surface-baseline test to
  confirm only the intended additive surface changed.

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Catalog/docs conformance, golden regeneration, and end-to-end validation.

- [X] T023 [P] Document the additive `help` field in `docs/release/schema-reference.md` and
  add a `help` entry to the `command-report (--json)` `inventory[]` in
  `docs/release/release-readiness.json` (`kind: jsonField`, `stability: additiveOptional`,
  `schemaVersion: 1`) so the doc ⇄ JSON ⇄ produced-artifact conformance test agrees.
- [X] T024 Regenerate the affected JSON goldens once all behavior changes land: every
  command's golden gains `"help": null` (mechanical, additive), plus the rewritten
  checklist (T006) and ship (T011) goldens and the new `help` golden. Depends on T015,
  T019, T006, T011. Run through `CommandReportJsonTests` so byte-stability is enforced.
- [X] T025 [P] Run the determinism gate:
  `dotnet test tests/FS.GG.SDD.Validation.Tests` (`DeterminismMatrixTests`,
  `CommandReportJsonTests`) — confirm default/`--json` and `--text` are byte-stable for all
  changed commands (FR-012) and the new help projection.
- [X] T026 Full suite green: `dotnet test FS.GG.SDD.sln`. No task left `[X]` over a failing
  assertion (Principle V).
- [X] T027 [P] Execute `specs/048-lifecycle-cli-papercuts/quickstart.md` US1–US4 scenarios
  against the built `fsgg-sdd` (checklist purge-and-re-derive, specify live-read statement,
  clean verify/ship, disclaimer-bullet `clarify`, top-level + per-command `--help`,
  `frobnicate --help` still `unknownCommand`, `NO_COLOR` zero-ANSI) and record evidence.
- [X] T028 [P] Confirm no Governance contract or dependency was touched (FR-013) and the
  Coordination board item stays `Repo Scope: sdd` — a grep/diff check over the changed files
  for any Governance/provider/rendering identity (should be none).

---

## Dependencies & Execution Order

### Phase order

- **Setup (Phase 1)** → no dependencies.
- **Foundational (Phase 2)** → none (the stories are independent seams).
- **User Stories (Phases 3–6)** → may all begin after Setup; they touch disjoint files and
  can run fully in parallel if staffed.
- **Polish (Phase 7)** → after the stories whose output it consolidates (see T024 deps).

### User-story independence

- **US1 (P1)** — `ParsingMid.fs` + `ParsingEarly.fs`; ChecklistCommandTests + SpecifyCommandTests.
- **US2 (P2)** — `ViewGeneration.fs`; Verify/Ship/GeneratedModelCurrency tests.
- **US3 (P3)** — `Specification.fs` (+ `Internal.fs` helper); Specification/Clarify tests.
- **US4 (P3)** — `CommandTypes`, `CommandHelp` (new), `CommandSerialization`,
  `CommandRendering`, `Program.fs`; Help command + rendering tests + baseline.

No story depends on another story's logic. The **only** cross-story sequencing is in Polish:
T024 (golden regen) waits on US4's field addition (T015/T019) and the US1/US2 content
rewrites (T006/T011) so all goldens are regenerated once.

### Within each story

- Tests are written first and must FAIL before implementation (Principle VI).
- US4: `.fsi`/module surface (T015, T016) before serialization/rendering/dispatch
  (T019–T021); baseline (T022) after the module exists.

### Parallel opportunities

- US1 T004/T005, US2 T008/T010, US3 T012/T013, US4 T017/T018 — tests in different files.
- US4 T019/T020 — serialization vs rendering, different files.
- Across stories: US1, US2, US3, US4 implementation can proceed concurrently.
- Polish T023/T025/T027/T028 are independent of each other.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → baseline green.
2. Phase 3 US1 (the P1, most-dangerous-class fixes: stale checklist rows + specify no-op).
3. **Stop and validate** US1 independently (quickstart §3.1/§3.2).
4. Ship the MVP increment.

### Incremental delivery

US1 (P1) → US2 (P2) → US3 (P3) → US4 (P3), each tested independently, then Polish to
regenerate goldens and run the determinism/conformance gates once.

---

## Notes

- `[P]` = different files, no incomplete-task dependency in the phase.
- Never mark a failing task `[X]`; narrow scope and document rather than weakening an
  assertion (skill discipline).
- The two rewritten tests (T002, T009) encode current buggy behavior — they must first fail
  against the new expectation, then pass after T006/T011.
- The `evidence.missingRequiredEvidence` diagnostic note from prior memory is unrelated here;
  no evidence-obligation surface changes in this feature.
