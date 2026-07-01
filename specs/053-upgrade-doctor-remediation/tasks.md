---
description: "Task list for feature 053 — diff-driven remediation verbs (doctor / upgrade)"
---

# Tasks: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

**Input**: Design documents from `specs/053-upgrade-doctor-remediation/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R12), data-model.md (E1–E8),
contracts/ (doctor-command, upgrade-command, confirm-effect, drift-model), quickstart.md (A–H)

**Change tier**: **Tier 1 (contracted change)** — two new commands, two new report blocks, a new
`Confirm` effect + edge-interpreter behavior, agent-skill/docs contract. All tasks are T1 unless
annotated `[T2]`.

**Tests**: Requested by the spec/plan — golden json fixtures, a write-audit, a scripted-stdin
confirmation harness, and drift-model unit tests. Test tasks are included and, per Constitution I &
VI, written **fail-first** before the implementation they cover.

**Elmish/MVU**: This is an I/O-bearing feature. The new `Confirm` effect gets explicit `.fsi`
contract, pure-transition, emitted-effect, and real-interpreter (scripted-stdin) evidence tasks
(Phase 4). `doctor` is a pure read-only projection over snapshotted read effects.

> **⚠ Pre-implement gate (spec Assumptions + plan §"Two scope forks"):** three research items —
> **R4** (self-update↔re-seed binary identity), **R5** (diff-rendering fidelity / `DiffPreview`
> determinism), **R6** (template re-pin scope) — are marked *to be confirmed in `/speckit-clarify`*.
> The tasks below encode the plan's **recommended** resolutions; **run `/speckit-clarify` before
> `/speckit-implement`** and revisit T012/T031/T032 (R4/R6) and T021/T034 (R5) if any resolution
> changes.

## Implementation status (2026-07-01)

All 48 tasks are complete; the full solution builds and the whole suite is green (770
passing, 3 network-gated acceptance skips). End-to-end quickstart evidence (scenarios
A/B/C/F/G, exit codes) is captured in `quickstart-evidence.txt`.

**Deviations from the literal task text (all keep the intent + real-evidence discipline):**

- **T001–T005 (fixtures).** The behind / coherent / no-minimum / no-provenance / author-edited
  scaffolds are built as **real on-disk temp fixtures** by `tests/FS.GG.SDD.Commands.Tests/`
  `RemediationSupport.fs` (no mocks) rather than committed static `tests/fixtures/<name>/` dirs
  — self-contained, parameterized by the installed CLI version, reused across the doctor/upgrade
  suites (`RemediationCommandTests.fs`).
- **`Confirm` edge (T027).** The interpreter takes no extra `isInteractive` parameter: a
  `Confirm` is only ever *emitted* on the interactive path (the pure core refuses a
  non-interactive run without `--yes` up front, and `--yes` applies directly), so the edge
  reads stdin guarded only by `DryRun`; redirected/EOF stdin returns null → declined, never a
  hang. This kept `interpret`/`interpretAll` signatures and all existing callers unchanged.
- **T030 step-defect** is exercised with a **real deterministic write failure** (a directory
  placed where a missing SKILL.md must be written) rather than relying on `dotnet tool update`
  failing (it returns 0 in the local environment).
- **T041** is a report-level standing guard (`only upgrade carries a remediation summary`)
  backed by construction: only `HandlersUpgrade` emits the self-update `RunProcess`/re-seed
  writes, and `CommandWorkflow` routes only `Upgrade` to it.
- **T045 migration note.** Per the repo's obligation, an **additive** release records the
  change as a paragraph in `docs/release/migrations/README.md` (not a `<version>.md` file);
  `release-readiness.json` + the code catalog + `schema-reference.md` were updated for the
  additive `doctor`/`upgrade` report blocks and the golden baseline regenerated.
- **Agent surfaces (T047):** `CLAUDE.md`, `AGENTS.md`, and the `fs-gg-sdd-getting-started` /
  `fs-gg-sdd-refresh-agents` skills (both `.claude` and `.codex`, byte-identical per the drift
  guard) gained the `doctor`/`upgrade` verbs; `docs/reference/doctor-upgrade.md` added.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (usually a
  different file).
- **[Story]**: `US1`–`US4`, or blank for setup/foundational/polish.
- Every task names an exact file path.

---

## Phase 1: Setup (Shared Test Fixtures)

**Purpose**: The real-filesystem fixtures every story's tests read (plan Testing; quickstart
Fixtures). No mocks — fixtures are on-disk scaffold shapes. Paths match `quickstart.md`
(`tests/fixtures/<name>/`).

- [X] T001 [P] Create `tests/fixtures/behind-scaffold/` — `.fsgg/scaffold-provenance.json` with a
  producing CLI **below** the minimum, `.fsgg/providers.yml` declaring `minimumFsggSdd.version`, and a
  subset of `fs-gg-sdd-*` skills **missing** from `.claude/skills/**` and `.codex/skills/**`.
- [X] T002 [P] Create `tests/fixtures/coherent-scaffold/` — CLI at/above minimum, all seeded artifacts
  present (`.claude`/`.codex` skills + `.fsgg/early-stage-guidance.md`).
- [X] T003 [P] Create `tests/fixtures/no-minimum-scaffold/` — provider declares no `minimumFsggSdd`;
  all artifacts present (FR-016 coherent-by-absence).
- [X] T004 [P] Create `tests/fixtures/no-provenance/` — a bare `init` skeleton with **no**
  `.fsgg/scaffold-provenance.json` (FR-015 degradation).
- [X] T005 [P] Create `tests/fixtures/author-edited/` — a behind scaffold where one **present** seeded
  artifact (e.g. `.claude/skills/fs-gg-sdd-lifecycle/SKILL.md`) carries author edits (no-clobber
  target for US4-AC2).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared type surface + the pure drift model that BOTH `doctor` (US1) and `upgrade` (US2)
depend on. The `Confirm` effect and interactivity edge are **not** here — they are upgrade-only and
live in Phase 4 to keep US1 an independently shippable MVP.

**⚠️ CRITICAL**: No user-story handler work can begin until this phase is complete.

- [X] T006 Add `Doctor` and `Upgrade` cases to `SddCommand` in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` then `CommandTypes.fs`, with `parseCommand "doctor"`/
  `"upgrade"`, `commandName`, `commandStage`, and `nextLifecycleCommand Doctor = None` /
  `nextLifecycleCommand Upgrade = None` (data-model E1, FR-001/FR-006). `.fsi` first.
- [X] T007 Add additive `CommandRequest` inputs `AssumeYes: bool` (default `false`) and
  `IsInteractive: bool` in `src/FS.GG.SDD.Commands/CommandTypes.fsi` then `CommandTypes.fs`, ignored
  by non-`upgrade` commands (data-model E2). `.fsi` first.
- [X] T008 [P] Add the `ReconciliationStep` value type (`StepId`/`Kind`/`DiffPreview`/`Outcome`/
  `TargetPaths`) to `src/FS.GG.SDD.Commands/CommandTypes.fsi` then `CommandTypes.fs` (data-model E6).
- [X] T009 Add the additive `DoctorSummary` and `UpgradeSummary` records (data-model E4/E5) and their
  optional fields on `CommandReport` and `CommandModel` in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` then `CommandTypes.fs`. Depends on T008.
- [X] T010 [P] Add `doctor.driftDetected`, `upgrade.nonInteractiveNoYes`, `upgrade.selfUpdateFailed`,
  `upgrade.stepFailed`, `upgrade.residualDrift` to `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` then
  `Diagnostics.fs`; add the two `upgrade.*` exit-2 ids to the `providerDefectIds` set (data-model E8,
  R10). `.fsi` first.
- [X] T011 [P] Write **fail-first** unit tests for the pure drift model in
  `tests/FS.GG.SDD.Commands.Tests/DriftTests.fs`: the CLI-axis matrix (`behind`/`atOrAbove`/
  `coherentByAbsence`/`undeterminable`), the artifact-axis (expected = `SeededSkills.skillNames` ×
  `.claude`/`.codex` + `.fsgg/early-stage-guidance.md`, sorted missing), previewed steps
  (`wouldApply`/`noTarget`), `IsCoherent`, and `HasProvenance = false` (drift-model contract; R2/R3/
  R5/R6/R12). These MUST fail (no `Drift` module yet).
- [X] T012 Implement the pure, no-I/O `Drift` module in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Drift.fs`: consumes `provenance`/`descriptor`/
  `installedVersion`/`presentArtifacts`, reuses `Fsgg.Version` + 052 minimum-reading (live descriptor
  wins over recorded), emits the CLI axis, sorted `MissingArtifactPaths`, and the previewed
  `ReconciliationStep` list. **R5/R6:** `DiffPreview`/`templateRePin` follow the plan's recommended
  resolution (re-pin usually `noTarget`, value-agnostic; `DiffPreview` deterministic) — revisit if
  clarify changes it. Add to `FS.GG.SDD.Commands.fsproj` before the handlers. Makes T011 pass.
  Depends on T008, T009.
- [X] T013 Add `doctor`/`upgrade` top-level help entries and per-command flags (`--yes`, `--root`) in
  `src/FS.GG.SDD.Commands/CommandHelp.fsi` then `CommandHelp.fs` (plan Phase 2 step 1).
- [X] T014 Refresh `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` and
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` for the **Phase-2** additive public surface
  from T006–T010 (Constitution III). Depends on T006–T010. *(The Phase-4 `Confirm`/interactivity
  additions are baselined separately in T038.)*

**Checkpoint**: Types, diagnostics, help, and the shared `Drift` model exist and are unit-tested.
US1 and US2 handlers can now proceed.

---

## Phase 3: User Story 1 — `doctor` reports drift with zero writes (Priority: P1) 🎯 MVP

**Goal**: `fsgg-sdd doctor` gives a complete read-only drift picture (CLI vs minimum, missing
artifacts, dry-run preview) and writes nothing.

**Independent Test**: In `behind-scaffold`, run `doctor` → it names installed/required/behind-by,
lists missing seeded artifacts, emits `previewSteps`, leaves the tree byte-identical, exits 0
(quickstart A/B/C).

### Tests for User Story 1 (fail-first) ⚠️

- [X] T015 [P] [US1] Write **fail-first** golden tests in
  `tests/FS.GG.SDD.Commands.Tests/DoctorCommandTests.fs`: byte-stable json `DoctorSummary` for
  `behind-scaffold` (installed/required/behind-by/missing/previewSteps), `coherent-scaffold`
  (`isCoherent: true`, `NoChange`), `no-minimum-scaffold` (`CliAxis: coherentByAbsence`, FR-016), and
  `no-provenance` (`HasProvenance: false`) — all exit 0 (US1-AC1/AC3, FR-002/FR-016).
- [X] T016 [P] [US1] Write **fail-first** write-audit test in
  `tests/FS.GG.SDD.Commands.Tests/DoctorWriteAuditTests.fs` asserting `doctor` plans **only** read
  effects (`ReadFile`/`EnumerateDirectory`) — never `WriteFile`/`RunProcess`/`SetExecutable`/
  `Confirm` — on every fixture (FR-002, SC-001).
- [X] T017 [P] [US1] Write **fail-first** three-projection parity test in
  `tests/FS.GG.SDD.Cli.Tests/DoctorProjectionTests.fs`: json/text/`NO_COLOR` rich carry the identical
  fact set and rich changes no json byte and emits zero ANSI (US1-AC4, SC-007, FR-014).

### Implementation for User Story 1

- [X] T018 [US1] Add `doctorReadEffects` (plan read effects for provenance, provider registry, and
  each expected artifact path) in `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, sourcing the
  expected set from `CommandWorkflow/SeededSkills.fs` (R3). Depends on T012.
- [X] T019 [US1] Implement the read-only staged driver in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersDoctor.fs`: gather the snapshotted reads → call
  `Drift` → build `DoctorSummary`; emit no mutating effect. Add to `FS.GG.SDD.Commands.fsproj`.
  Depends on T012, T018.
- [X] T020 [US1] Add the `Doctor` branch to `nextLifecycleEffects` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` (own driver, like `Scaffold`). Depends on T019.
- [X] T021 [US1] Emit the byte-deterministic `DoctorSummary` json block (hand-ordered fields; sorted
  `MissingArtifactPaths`/`TargetPaths`; deterministic `DiffPreview` per R5) in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs`. Makes T015 pass. Depends on T009.
- [X] T022 [US1] Add the `DoctorSummary` text projection lines in
  `src/FS.GG.SDD.Commands/CommandRendering.fs` (rich derives automatically). Depends on T009.
- [X] T023 [US1] Map `doctor` outcome (`NoChange` / `SucceededWithWarnings`, never `Blocked`) and its
  `NextAction` in `src/FS.GG.SDD.Commands/CommandReports.fs` (contract: always exit 0). Depends on T019.
- [X] T024 [US1] Wire `doctor` parsing/dispatch and `--root` into `src/FS.GG.SDD.Cli/Program.fs`.
  Makes T015–T017 pass end-to-end. Depends on T020–T023.

**Checkpoint**: `fsgg-sdd doctor` fully works and is independently demoable (the MVP).

---

## Phase 4: User Story 2 — `upgrade` reconciles with confirmable diffs (Priority: P1)

**Goal**: `fsgg-sdd upgrade` reconciles a behind scaffold across up to three steps (self-update,
re-pin, re-seed), each shown as a diff and applied only after per-step confirmation.

**Independent Test**: In `behind-scaffold`, `printf 'y\ny\ny\n' | fsgg-sdd upgrade` applies each step
after its `y`; a subsequent `doctor` reports coherent; nothing landed silently (quickstart D/E).

> **Story split (D1):** T032 implements **only** the interactive-confirm path + finalize. The
> `--yes` short-circuit and non-interactive-refusal rows of the decision table are owned by **US3**
> (T040), so ownership of each behavior is singular.

### Elmish/MVU: the `Confirm` effect (`.fsi` contract → pure transition → edge interpreter)

- [X] T025 [US2] Add the `Confirm of stepId: string * prompt: string` case to `CommandEffect` and the
  additive `Confirmed: bool option` field (default `None`) to `CommandEffectResult` in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` then `CommandTypes.fs` (data-model E3, confirm-effect
  contract). `.fsi` first.
- [X] T026 [P] [US2] Write **fail-first** edge-interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/ConfirmEffectTests.fs`: `DryRun` → `Confirmed = Some false` and no
  mutation; `IsInteractive` scripted stdin `y`/`yes` → `Some true`, anything else → `Some false`; and
  an additive-regression check that every existing effect result still carries `Confirmed = None`
  (confirm-effect contract, Constitution VI).
- [X] T027 [US2] Implement the `Confirm` edge interpreter case in
  `src/FS.GG.SDD.Commands/CommandEffects.fsi` then `CommandEffects.fs`: `DryRun`→`Some false`;
  `IsInteractive`→write prompt + read one line from `Console.In` (`y`/`yes` case-insensitive); the
  prompt text stays out of the deterministic json (presentation only). Makes T026 pass. Depends on T025.
- [X] T028 [US2] Thread interactivity into the edge: add an input-interactivity signal
  (`Console.IsInputRedirected`) to `src/FS.GG.SDD.Cli/Rendering.fsi`/`Rendering.fs`
  `detectCapabilities`, and parse `--yes` + compute `IsInteractive` into `CommandRequest` in
  `src/FS.GG.SDD.Cli/Program.fs` (confirm-effect contract). Depends on T007.

### Tests for User Story 2 (fail-first) ⚠️

- [X] T029 [P] [US2] Write **fail-first** scripted-stdin handler tests in
  `tests/FS.GG.SDD.Cli.Tests/UpgradeInteractiveTests.fs` (synthetic stdin disclosed in the test
  names): confirm-all → all steps `applied`, `mode: "interactive"`, subsequent `doctor` coherent
  (US2-AC1/AC2); decline-one → declined step `skipped` with no write, `appliedStepIds` vs
  `skippedStepIds` distinguished, `residualDrift: true`, exit 0 (US2-AC4); already-coherent → no-op
  `AlreadyCoherent`, exit 0 (US2-AC3); **no-provenance → no-op "nothing to reconcile", zero writes,
  exit 0 (FR-015)**.
- [X] T030 [P] [US2] Write **fail-first** step-defect test in
  `tests/FS.GG.SDD.Commands.Tests/UpgradeCommandTests.fs`: a confirmed step that fails to apply (e.g.
  self-update process error) → `FailedStepIds` non-empty, `ResidualDrift: true`, `Blocked` with an
  `upgrade.*` id, **exit 2**, never reported complete (US2-AC5, SC-006, FR-013). Include golden json
  for `UpgradeSummary`.

### Implementation for User Story 2

- [X] T031 [US2] Add `upgradeReadEffects` (same reads as doctor) and the step-apply effect builders in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`: `cliSelfUpdate` →
  `RunProcess("dotnet",["tool";"update";…])`; `artifactReSeed` → the existing `init` no-clobber
  `AgentGuidanceTarget` seeding effects for the **missing** paths only (R8); `templateRePin` →
  `WriteFile(".fsgg/providers.yml", …)` when a value-agnostic drift signal exists, else `noTarget`
  (R6). Depends on T012.
- [X] T032 [US2] Implement the **interactive-confirm path + finalize** in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersUpgrade.fs`: re-derive the next step from
  `model.InterpretedEffects` (like `HandlersScaffold`, no new state field) — emit one `Confirm` per
  step and apply it iff `Confirmed = Some true` (`Mode = "interactive"`) — then finalize
  `UpgradeSummary` with applied/skipped/failed/residual accounting. **Does not** implement the
  `--yes`/non-interactive rows (owned by US3/T040). Add to `FS.GG.SDD.Commands.fsproj`. Makes
  T029/T030 pass. Depends on T027, T028, T031.
- [X] T033 [US2] Add the `Upgrade` branch to `nextLifecycleEffects` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` (own driver). Depends on T032.
- [X] T034 [US2] Emit the byte-deterministic `UpgradeSummary` json block (deterministic `DiffPreview`
  per R5) in `src/FS.GG.SDD.Commands/CommandSerialization.fs` and its text lines in
  `src/FS.GG.SDD.Commands/CommandRendering.fs`. Depends on T009.
- [X] T035 [US2] Map `upgrade` outcome + exit taxonomy in
  `src/FS.GG.SDD.Commands/CommandReports.fs`: `Succeeded`(0)/`SucceededWithWarnings`(0)/`Blocked`
  exit 2 for `upgrade.selfUpdateFailed`/`upgrade.stepFailed` via `providerDefectIds`, `NoChange`
  already-coherent (0); `NextActionHint` (upgrade-command contract, R10). Depends on T010, T032.
- [X] T036 [US2] Wire `upgrade` parsing/dispatch (`--root`, `--yes`) into
  `src/FS.GG.SDD.Cli/Program.fs`, tolerating the `Confirm` loop in `interpretUntilIdle`. Makes the
  US2 tests pass end-to-end. Depends on T033–T035.
- [X] T037 [P] [US2] Write **fail-first** upgrade three-projection parity test in
  `tests/FS.GG.SDD.Cli.Tests/UpgradeProjectionTests.fs`: json/text/`NO_COLOR` rich carry the identical
  `UpgradeSummary` fact set; rich changes no json byte and emits zero ANSI (SC-007, FR-014). Depends
  on T034.
- [X] T038 [US2] Refresh `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` **and**
  `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` for the **Phase-4** public-surface additions —
  the `Confirm` case + `Confirmed` field (T025, `CommandTypes.fsi`/`CommandEffects.fsi`) and the
  input-interactivity signal on `Rendering.fsi` (T028) (Constitution III — a Tier-1 surface change
  with a stale baseline is incomplete). Depends on T025, T027, T028.

**Checkpoint**: `fsgg-sdd upgrade` reconciles interactively; US1+US2 both work independently and all
public-surface baselines are current.

---

## Phase 5: User Story 3 — Explicit non-interactive apply (`--yes`) (Priority: P2)

**Goal**: `--yes` applies without prompting; a non-interactive run *without* `--yes` refuses (zero
writes, no hang), and the non-interactive apply is triggered by that flag alone — never implicitly.

**Independent Test**: `fsgg-sdd upgrade --yes < /dev/null` reconciles without prompting; plain
`fsgg-sdd upgrade < /dev/null` exits 1 with a `--yes` pointer, writes nothing, does not hang
(quickstart F/G).

- [X] T039 [P] [US3] Write **fail-first** tests in
  `tests/FS.GG.SDD.Cli.Tests/UpgradeNonInteractiveTests.fs`: `--yes` non-interactive →
  `mode: "assumeYes"`, applied without prompting, exit 0 (US3-AC1); non-interactive **without**
  `--yes` → `upgrade.nonInteractiveNoYes`, `mode: "refusedNonInteractive"`, **zero writes**, no
  prompt-hang, **exit 1** (US3-AC2, SC-004, FR-012).
- [X] T040 [US3] Implement the **`--yes` short-circuit and non-interactive-refusal rows** of the
  `HandlersUpgrade` decision table in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersUpgrade.fs`
  (the rows T032 deliberately left to US3, per D1): `AssumeYes = true` → apply each step directly,
  **no `Confirm` emitted**, `Mode = "assumeYes"`; `AssumeYes = false && IsInteractive = false` →
  emit `upgrade.nonInteractiveNoYes`, no `Confirm`, no write, `Mode = "refusedNonInteractive"`,
  exit 1. Makes T039 pass. Depends on T032.
- [X] T041 [P] [US3] Write a **fail-first** cross-command guarantee test in
  `tests/FS.GG.SDD.Commands.Tests/RemediationSideEffectAuditTests.fs`: `scaffold`/`refresh`/`agents`/a
  lifecycle stage never emit a self-update `RunProcess` or a re-seed write — only `upgrade` mutates
  for remediation (US3-AC3, SC-003, FR-008). A standing guard.

**Checkpoint**: The explicit-flag contract is enforced and audited.

---

## Phase 6: User Story 4 — Governed state & CI pins protected (Priority: P2)

**Goal**: `upgrade` writes only consumer-owned `.fsgg/providers.yml` + the re-seeded missing paths;
never governed registry/provider-descriptor state; re-seed is no-clobber.

**Independent Test**: `fsgg-sdd upgrade --yes` on `author-edited` leaves the edited present artifact
byte-unchanged and touches no governed file (quickstart H).

- [X] T042 [P] [US4] Write **fail-first** write-audit test in
  `tests/FS.GG.SDD.Commands.Tests/UpgradeWriteAuditTests.fs`: the only mutations from a full `upgrade`
  are `.fsgg/providers.yml` (when re-pin has a target) and the re-seeded **missing** skeleton paths;
  zero writes to governed registry/provider-descriptor state (US4-AC1, SC-005).
- [X] T043 [P] [US4] Write **fail-first** no-clobber test in
  `tests/FS.GG.SDD.Commands.Tests/UpgradeNoClobberTests.fs` using `author-edited`: re-seed
  materializes only missing artifacts; the present author-edited artifact is byte-unchanged
  (US4-AC2, FR-010).
- [X] T044 [US4] Verify (and adjust if needed) the re-seed builder in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs` / `HandlersUpgrade.fs` uses the `init`
  `canOverwrite`-guarded `AgentGuidanceTarget` writes so present artifacts are never overwritten and
  only consumer paths are written. Makes T042/T043 pass. Depends on T031, T032.

**Checkpoint**: All four stories independently functional and audited.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Docs, agent-surface contract parity (Constitution VII), and end-to-end validation.

- [X] T045 [P] Add the additive-report-blocks migration note in
  `docs/release/migrations/` (new file) covering the additive `DoctorSummary`/`UpgradeSummary` blocks
  and `CommandEffectResult.Confirmed` (plan Phase 2 step 8, Tier-1 migration requirement).
- [X] T046 [P] Add `doctor`/`upgrade` usage reference under `docs/reference/`, noting `upgrade` is the
  **only** mutating remediation verb and that CI keeps pinning via `.config/dotnet-tools.json`
  (US4-AC3, FR-012).
- [X] T047 Update both agent surfaces equivalently (Constitution VII): add the `doctor`/`upgrade`
  verbs to the `fs-gg-sdd-refresh-agents` and `fs-gg-sdd-getting-started` skills and to
  `CLAUDE.md`/`AGENTS.md` guidance, keeping Claude and Codex aligned. Depends on T046.
- [X] T048 Run the `quickstart.md` scenarios A–H plus the step-defect and no-provenance cases against
  the built CLI; record real evidence (exit codes + sha snapshots) for the evidence obligations.
  Depends on all prior phases.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately; all `[P]`.
- **Foundational (Phase 2)**: needs the fixtures only for the Drift unit test (T011); its type/
  diagnostic tasks are independent. **Blocks all user stories.**
- **US1 (Phase 3)**: after Foundational. The MVP — independently shippable.
- **US2 (Phase 4)**: after Foundational. Adds the `Confirm` effect + interactivity edge; independent
  of US1 at runtime (shares only the `Drift` model and report plumbing). Ends with a baseline refresh
  (T038) for its own surface additions.
- **US3 (Phase 5)**: after US2 — implements the `--yes`/non-interactive rows T032 left to it (D1).
- **US4 (Phase 6)**: after US2 (audits `upgrade`'s writes). Independent of US3.
- **Polish (Phase 7)**: after the stories it documents/validates.

### Within a story

- Tests (fail-first) are written before the implementation they cover; `.fsi` before `.fs`.
- `Drift` (T012) before both handlers. Read-effect builders before handlers before
  `nextLifecycleEffects` branches before serialization/rendering/reports before Cli wiring.
- Public-surface baselines are refreshed after each surface-changing phase: **T014** (Phase 2),
  **T038** (Phase 4).

### Parallel opportunities

- **Phase 1**: T001–T005 all parallel.
- **Phase 2**: T008, T010, T011 parallel; T006/T007/T009 serialize on `CommandTypes`.
- **Phase 3**: the three test tasks T015–T017 parallel; then implementation.
- **Phase 4**: T026 parallel with other files; T029/T030/T037 parallel test authoring.
- **Phase 5/6**: test tasks (T039, T041, T042, T043) parallel.
- **Phase 7**: T045/T046 parallel; T047 after T046.

---

## Implementation Strategy

- **MVP** = Phase 1 + Phase 2 + Phase 3 (US1 `doctor`). Ship/demo the always-safe read-only drift
  report first, then validate independently.
- **Increment 2** = Phase 4 (US2 `upgrade`) — the cure. Then US3 (explicit `--yes` contract) and US4
  (ownership/no-clobber audit) as P2 guarantees. Polish last.

## Notes

- `[P]` = different files, no dependency on another incomplete task in the same phase.
- Never mark a failing task `[X]`; never weaken an assertion to green a build.
- **Pre-implement clarify gate:** three scope forks — **R4** (self-update↔re-seed binary identity),
  **R5** (diff-rendering fidelity / `DiffPreview` determinism), **R6** (template re-pin) — are routed
  to `/speckit-clarify` by the spec Assumptions and plan. Tasks implement the plan's recommended
  resolutions (re-seed materializes the running binary's skeleton; re-pin usually `noTarget` and
  value-agnostic; `DiffPreview` deterministic). **Run `/speckit-clarify` first**; revisit T012/T031/
  T032 (R4/R6) and T021/T034 (R5) if any resolution changes.
- **Baseline discipline (Constitution III):** T014 refreshes the Phase-2 surface; **T038** refreshes
  the Phase-4 `Confirm`/`Confirmed`/input-interactivity surface across the Commands **and** Cli
  baselines. A Tier-1 surface change with a stale baseline is incomplete.
- **Decision-table ownership (D1):** T032 owns the interactive path + finalize only; T040 (US3) owns
  the `--yes` short-circuit and the non-interactive refusal — no shared/duplicated implementation.
- Elmish/MVU evidence for the `Confirm` effect: `.fsi` (T025), pure transition + emitted-effect
  (T026, T029), real interpreter via scripted stdin (T027, T029). `doctor` is a pure projection —
  its evidence is the golden json + zero-write audit (T015/T016).
