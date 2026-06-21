# Tasks: Rich Spectre.Console Rendering of the `validation-report`

**Feature**: `021-rich-validation-report`
**Input**: Design documents from `/specs/021-rich-validation-report/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

This repository follows standard Spec Kit: `tasks.md` is an authored checklist.
Phases run in sequence; tasks within a phase marked `[P]` may run in parallel
(no incomplete in-phase dependency). Standard Spec Kit reserves `[P]` for
different-file tasks; where same-file test tasks below carry `[P]` it means
"independent test bodies, co-authored in one edit to the shared file" — the
parallelism is in authoring, not in concurrent file writes. Tier is **T1** for the whole
feature (plan: contracted change — two new public `Rendering` functions); no
per-task tier annotations needed. Ordering follows the Constitution's
spec → `.fsi` → tests → impl discipline (Principle I) and the user-story priorities.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped with rationale on the line

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Register the one new test file so the tests-first phase compiles.

- [X] T001 Add `ValidationRichRenderingTests.fs` as a compile item in
  `tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj`, ordered before the
  existing `ValidateCommandTests.fs` entry (create the empty `module` file so the
  project builds). No other project or package change — the CLI project already
  references `Spectre.Console` and `FS.GG.SDD.Validation` (plan: "No new package
  reference").

**Checkpoint**: Solution still builds (`dotnet build -c Release FS.GG.SDD.sln`).

---

## Phase 2: Foundational — Contract / Signature (Blocking Prerequisites)

**Purpose**: Author the public surface before any test or implementation
(Principle I & III). BLOCKS Phases 3–5.

- [X] T002 [US1] Declare the two new functions in
  `src/FS.GG.SDD.Cli/Rendering.fsi`, after the existing `resolve` entry, with
  doc-comments mirroring the `CommandReport` equivalents:
  - `val renderValidationRichTo: console: IAnsiConsole -> report: ValidationReport -> unit`
  - `val resolveValidation: format: OutputFormat -> capabilities: TerminalCapabilities -> report: ValidationReport -> RichRenderResult`

  Add the `open FS.GG.SDD.Validation.ValidationContracts` needed for
  `ValidationReport`. Signatures verbatim from
  `contracts/validation-rich-rendering-projection.md` and data-model.md.
- [X] T003 [US1] Add the two matching lines to
  `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` (kept sorted):
  `FS.GG.SDD.Cli.Rendering.renderValidationRichTo` and
  `FS.GG.SDD.Cli.Rendering.resolveValidation`. (Guards Principle III via
  `SurfaceBaselineTests.fs`; the existing test will fail until the `.fs` provides
  these — expected.)

- [X] T003a [US1] Exercise the two new signatures in F# Interactive / a prelude
  before any `.fs` body (Constitution Principle I.3): load `Rendering.fsi`'s shape
  against a hand-built `ValidationReport` to confirm `renderValidationRichTo` and
  `resolveValidation` are callable and well-typed at the boundary. Capture the
  short transcript for the T016 evidence bundle. (Lightweight exploration, not the
  post-impl surface transcript.)

**Checkpoint**: `.fsi` + baseline express the full added surface and the shape is
FSI-validated; the project does not yet compile against `.fs` (functions
unimplemented) — that gap is closed in Phase 3/4.

---

## Phase 3: User Story 1 — Triage a run at a glance (Priority: P1) 🎯 MVP

**Goal**: Render the `validation-report` richly — verdict, summary counts,
per-matrix rollup, and every non-passing cell with coordinates / status /
diagnostic — as a pure projection over the report.

**Independent Test**: Render a fixture report with a mix of statuses to a
color-off `StringWriter`-backed Spectre console and confirm the verdict, the five
summary counts, each matrix name, and each non-passing cell's coordinates + status
(+ diagnostic for `fail`) are present, with no invented facts.

### Tests for User Story 1 (write first; must FAIL before T008) ⚠️

- [X] T004 [P] [US1] In
  `tests/FS.GG.SDD.Cli.Tests/ValidationRichRenderingTests.fs`, add the color-off
  Spectre console harness (reuse the `AnsiSupport.No` / `ColorSystemSupport.NoColors`
  / fixed-width `StringWriter` pattern from `RichRenderingTests.fs`) and a
  `ValidationReport` fixture builder producing a mix of `Pass` / `Fail` /
  `SkippedWithReason` / `CoverageGap` / `NotValidated` cells across ≥2 matrices.
- [X] T005 [P] [US1] Projection-completeness test (INV-5 / C-2 / SC-004): assert the
  rendering of `renderValidationRichTo` contains the verdict indicator, all five
  summary counts (passed/failed/skipped/coverageGaps/notValidated), each matrix's
  name and its dimensions, and every non-passing cell's coordinates + status token;
  for each `Fail` cell, its diagnostic message. Assert `pass` cells are NOT
  enumerated individually and that no coordinate/fact absent from the fixture
  appears. Assert `schemaVersion`/`generatorVersion` absence is NOT a failure.
- [X] T006 [P] [US1] Verdict + status-differentiation tests (INV-6 / C-3 / FR-007):
  (a) an all-pass fixture renders a **passed** verdict with the rollup and no
  invented diagnostics; (b) a fixture whose only non-passing cells are
  `coverageGap`/`notValidated` renders a **not-passed** verdict with those cells
  emphasized as failing, visually distinct from `skippedWithReason`.
- [X] T007 [P] [US1] Single-failing-cell isolation test (C-2 verification bullet):
  with exactly one forced `Fail` cell, assert its coordinates appear and sibling
  cells in the same matrix do not falsely render as failing.

### Implementation for User Story 1

- [X] T008 [US1] Implement `renderValidationRichTo` in
  `src/FS.GG.SDD.Cli/Rendering.fs` (after the existing `renderRichTo`): pure over
  the report, writing only to the supplied `IAnsiConsole`. Render (1) a colored
  verdict rule/header from `Summary.OverallPassed`; (2) the five summary counts;
  (3) a per-matrix rollup row (name, dimensions, per-status counts); (4) the
  non-passing cells per matrix with `dim=value, …` coordinates, status token, and —
  for `Fail` — the diagnostic message; apply the status styling map from
  data-model.md (`Fail`/`CoverageGap`/`NotValidated` red-family emphasis;
  `SkippedWithReason` yellow/grey non-failing; `Pass` summarized only). Surface
  populated `Sensed` fields only if `Some` (C-6); never require them. Makes
  T005–T007 pass.

**Checkpoint**: US1 tests green; rich rendering is faithful and complete over a
report. `resolveValidation` and CLI wiring still pending.

---

## Phase 4: User Story 2 — Safe, clean output in non-interactive contexts (Priority: P1)

**Goal**: Degrade rich to the exact plain-text projection (zero ANSI) when
non-interactive or color-disabled, and prove the JSON/text/`sensed` automation
contract is byte-identical to before this feature.

**Independent Test**: `resolveValidation Rich` with non-interactive or color-off
capabilities returns exactly `renderText report` with `UsedRichRendering=false`
and no ESC byte; `serialize`/`renderText` bytes and the `sensed` null fence are
unchanged.

### Tests for User Story 2 (write first; must FAIL before T011) ⚠️

- [X] T009 [P] [US2] In `ValidationRichRenderingTests.fs`, degradation + parity
  tests (INV-2 / C-4): `resolveValidation Rich` with `IsInteractive=false`
  returns `renderText report` with `UsedRichRendering=false` and zero `0x1B`
  bytes; same with `ColorEnabled=false`; with interactive+color it returns rich
  output with `UsedRichRendering=true`. `resolveValidation Json` returns
  `serialize report` and `Text` returns `renderText report`, both byte-for-byte
  with `UsedRichRendering=false`.
- [X] T010 [P] [US2] Automation-invariance test (INV-1 / INV-3 / C-5 / SC-002):
  assert `serialize report` and `renderText report` are unchanged across a
  `resolveValidation` call (no mutation), and that the serialized JSON keeps the
  `sensed` block normalized to `null`. (No golden-file change — rich is excluded
  from deterministic contracts, FR-008.)

### Implementation for User Story 2

- [X] T011 [US2] Implement `resolveValidation` in `src/FS.GG.SDD.Cli/Rendering.fs`,
  mirroring `resolve`: `Json -> { Text = serialize report; UsedRichRendering = false }`;
  `Text -> { Text = renderText report; UsedRichRendering = false }`;
  `Rich` with `IsInteractive && ColorEnabled` -> render via
  `renderValidationRichTo` into an internal `StringWriter`-backed console and
  return `{ Text = …; UsedRichRendering = true }`; `Rich` otherwise ->
  `{ Text = renderText report; UsedRichRendering = false }`. Makes T009–T010 pass;
  `SurfaceBaselineTests` (T003) now also passes.

**Checkpoint**: US1 + US2 complete at the `Rendering` module level; the public
surface compiles and all Rendering tests are green. CLI not yet wired.

---

## Phase 5: User Story 3 — Choose the right format for the task (Priority: P2)

**Goal**: Wire `fsgg-sdd validate` so `--rich` actually renders richly on
interactive stdout (no longer degrade-to-text), while `--json`/`--text` and
`--out` stay deterministic and stream/exit parity holds.

**Independent Test**: Invoke `validate` three ways (`--json`, `--text`, `--rich`)
against the real host binary; each prints the matching projection on stdout with
the same exit code; `--rich` redirected contains zero ANSI; `--rich --out`
persists deterministic text.

### Tests for User Story 3 (write first; must FAIL before T013) ⚠️

- [X] T012 [P] [US3] In `tests/FS.GG.SDD.Cli.Tests/ValidateCommandTests.fs`, add
  end-to-end host-binary tests (cheap `--matrix compatibility`): `--rich`
  redirected to a captured stream contains zero `0x1B` bytes and equals the
  `--text` output; `--json` bytes (incl. `"startedAtUtc": null` sensed fence) are
  unchanged from the existing byte-stability expectation; exit code is identical
  across `--json`/`--text`/`--rich` and non-zero for the single-matrix (partial)
  run; `--rich --out <path>` writes a file with zero ANSI equal to the text
  projection (FR-010). (Stream parity: all three write to **stdout**, SC-005.)

### Implementation for User Story 3

- [X] T013 [US3] Rewrite `printValidate` in `src/FS.GG.SDD.Cli/Program.fs` to
  resolve stdout via the new path: select the format with
  `Rendering.selectFormat rest`, compute the stdout rendering with
  `(Rendering.resolveValidation format (Rendering.detectCapabilities ()) report).Text`,
  and write it to `Console.Out`. Keep `--out` deterministic: persist
  `serialize report` for `Json`/default else `renderText report` (never rich ANSI)
  per `contracts/validation-output-format-selection.md`. Preserve the exit rule
  (`0` iff `Summary.OverallPassed`). Remove the old "`--rich` degrades to text"
  branch and its comment. Makes T012 pass.

**Checkpoint**: All three formats selectable; `--rich` renders richly interactively
and degrades safely. Feature behavior complete.

---

## Phase 6: Polish — Agent/Doc Alignment & Verification Evidence

**Purpose**: One-contract agent/human alignment (Principle VII / FR-011) and the
constitution's evidence obligations.

- [X] T014 [P] Update agent surfaces so the deferred-`--rich` note for `validate`
  becomes the now-available rich format, equivalently across:
  `CLAUDE.md` (the `validate` bullet that currently records `--rich` deferred),
  `AGENTS.md`, and both `fs-gg-sdd-project` skill files (Claude + Codex). Do not
  add rendering-specific package names/templates/paths to generic SDD behavior
  (CLAUDE.md constraint).
- [X] T015 [P] Update product docs: the `validate` command contract note in
  `docs/` that records `--rich` as deferred (and any output-format table) to
  describe the available rich projection; add a short release-notes line that
  `validate --rich` now renders richly (additive `Rendering` surface, no schema
  bump). Confirm `docs/release/schema-reference.md`'s `validation-report` declared
  exception is unaffected (rich is not a persisted/catalogued artifact).
- [X] T016 Run the full verification gate and capture evidence: Release build
  (`dotnet build -c Release FS.GG.SDD.sln`) + full suite
  (`dotnet test -c Release`); FSI public-surface transcript proving
  `renderValidationRichTo` / `resolveValidation` are visible; disposable-directory
  CLI smoke per `quickstart.md` Scenarios 1–5 (interactive `--rich`, redirected
  no-ANSI, `NO_COLOR`/`TERM=dumb` fallback, `--json` byte-stability + sensed fence,
  `--rich --out` deterministic); a performance note that the JSON/automation path
  is unchanged. Record SDD↔Governance boundary review (rendering is CLI-local; no
  Governance runtime/verdict) and artifact traceability into `readiness/`. Record
  the FR-009 negative-requirement check explicitly: no `validation-report` schema/
  field/matrix/lifecycle-stage change and no `CommandReport` diff (the `Rendering`
  surface gain is purely additive — confirmed against `PublicSurface.baseline`).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no deps; start immediately.
- **Phase 2 (Contract/Signature)** → after Phase 1; **BLOCKS** Phases 3–5
  (the `.fsi` and baseline must exist first — Principle I/III).
- **Phase 3 (US1)** → after Phase 2.
- **Phase 4 (US2)** → after Phase 2; T011 depends on T008 (`resolveValidation`
  calls `renderValidationRichTo` for the rich branch), so run US1 before US2's
  implementation.
- **Phase 5 (US3)** → after Phase 4 (wiring calls `resolveValidation`).
- **Phase 6 (Polish)** → after Phases 3–5; T016 last (it verifies everything).

### Within a story

- Tests (T004–T007, T009–T010, T012) are authored first and must FAIL before the
  matching implementation task (T008, T011, T013) — Principle VI.

### Parallel opportunities

- T004–T007 ([P], US1 tests) — same file (`ValidationRichRenderingTests.fs`),
  independent test bodies; co-author in one edit, then implement T008 once.
- T009–T010 ([P], US2 tests) — same file, co-author together.
- T014 and T015 ([P]) — distinct files (agent surfaces vs docs); genuinely
  concurrent.

## Elmish/MVU applicability

Per plan Constitution Check (Principle V): capability detection and console
writing stay at the CLI edge (already isolated); `report -> rich string` is **pure**
over the report. The validation sweep's own MVU boundary (`ValidationRunner`) is
untouched. No new `.fsi` Model/Msg/Effect contract is introduced by this
presentation-only feature; the I/O-boundary obligation is satisfied by keeping
`renderValidationRichTo` pure and confining `detectCapabilities`/`Console.Out` to
`Program.fs`.

---

## Summary

- **Total tasks**: 17 across 6 phases.
- **Per user story**: US1 = 6 (T004–T008 + T003a FSI exercise), US2 = 3
  (T009–T011), US3 = 2 (T012–T013); plus Setup 1 (T001), Foundational 3
  (T002–T003 + T003a), Polish 3 (T014–T016).
- **Parallel opportunities**: T004–T007, T009–T010, T014–T015.
- **Suggested MVP**: Phase 1 + Phase 2 + Phase 3 (US1) — a faithful rich rendering
  over the report, testable in isolation. US2 (safe degradation + invariance) and
  US3 (CLI wiring) complete the shippable feature.

## Completion notes (2026-06-21)

All 17 tasks complete. Full gate green: `dotnet build -c Release FS.GG.SDD.sln`
(0 errors) and `dotnet test -c Release FS.GG.SDD.sln` — **437 tests pass**
(Artifacts 103, Validation 18, Commands 265, Cli 51); both public-surface baselines
green. Evidence bundle under `readiness/021-rich-validation-report/`
(`evidence.yml`, `cli-smoke.md`, `fsi-validation-rich-surface.txt`,
`rich-tty-capture.ansi`).

**Latent bug found + fixed during T016.** Exercising the real rich path under a
pseudo-TTY (`script`) crashed: `Console.WindowWidth` reports `0` there, and
`Profile.Width <- 0` throws `Console width must be greater than zero`. The same
defect existed in the feature-019 `resolve` (CommandReport). Both `resolve` and the
new `resolveValidation` now guard `Some width when width > 0`. Presentation-edge fix
only; no contract/schema/JSON byte change (FR-009 re-confirmed against
`PublicSurface.baseline`: the only surface delta is the two declared functions).
