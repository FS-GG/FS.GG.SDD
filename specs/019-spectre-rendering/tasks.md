---
description: "Task list for feature 019 — Rich Spectre.Console CLI Rendering"
---

# Tasks: Rich Spectre.Console CLI Rendering

**Input**: Design documents from `/specs/019-spectre-rendering/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included. Constitution Principle VI (test evidence mandatory) and the
plan's Constitution Check require failing-before / passing-after tests for
projection completeness, automation invariance, no-ANSI degradation, and
stream/exit parity. Write each test before its implementation and confirm it
fails first.

**Change Tier**: Tier 1 (extends public `OutputFormat`, adds a CLI `Rendering`
module with `.fsi`, adds CLI format flags). All tasks inherit Tier 1; no
per-task `[T2]` annotations.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `[US1]`/`[US2]`/`[US3]` traceability; blank for shared work
- Phases run in sequence; tasks within a phase may run in parallel where marked

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the Spectre.Console dependency and scaffold the new test project.

- [X] T001 Add `<PackageVersion Include="Spectre.Console" Version="..." />` (pin a
  current release) to `Directory.Packages.props`.
- [X] T002 In `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`, add
  `<PackageReference Include="Spectre.Console" />` and compile items
  `Rendering.fsi` then `Rendering.fs` (ordered before `Program.fs`). Depends on
  T001.
- [X] T003 [P] Scaffold `tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj`
  (`IsPackable=false`; ProjectReference to `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`
  and `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`; PackageReferences
  `FSharp.Core`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`),
  mirroring `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj`.
- [X] T004 Add `FS.GG.SDD.Cli` (if not already in the solution) and
  `FS.GG.SDD.Cli.Tests` projects to `FS.GG.SDD.sln`. Depends on T003.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Author the `.fsi` contracts and a compiling stub so the suite builds
and new tests can fail before implementation (Constitution Principle I/III). No
user story can begin until this phase is complete.

**⚠️ CRITICAL**: Blocks all user stories.

- [X] T005 Add `Rich` case to `OutputFormat` in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` (the `val outputFormatValue` signature
  is unchanged).
- [X] T006 In `src/FS.GG.SDD.Commands/CommandTypes.fs`, add the `Rich` case to
  `OutputFormat` and map `Rich -> "rich"` in `outputFormatValue` (data-model
  "Changed/new types"). Depends on T005.
- [X] T007 Author `src/FS.GG.SDD.Cli/Rendering.fsi`: module
  `FS.GG.SDD.Cli.Rendering` with `TerminalCapabilities`
  (`IsInteractive`/`ColorEnabled`/`Width: int option`), `RichRenderResult`
  (`Text`/`UsedRichRendering`), `val detectCapabilities`, `val renderRichTo`, and
  `val resolve` exactly per `contracts/rich-rendering-projection.md`.
- [X] T008 Add a compiling stub `src/FS.GG.SDD.Cli/Rendering.fs` implementing the
  `Rendering.fsi` signatures with placeholder bodies (e.g. `failwith`/minimal
  returns) so `FS.GG.SDD.sln` builds and the new tests link and fail. Depends on
  T007.
- [X] T009 Confirm public-surface baseline coverage: build and run
  `SurfaceBaselineTests`; if the `OutputFormat.Rich` change or new module surfaces
  in `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline`, refresh the baseline.
  Depends on T006.
- [X] T009a Scaffold `tests/FS.GG.SDD.Cli.Tests/SurfaceBaselineTests.fs` and a
  `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` for the CLI project's first
  public module `FS.GG.SDD.Cli.Rendering`, mirroring
  `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs` and generating the
  baseline from the authored `Rendering.fsi` (Constitution Principle III —
  surface-area baselines MUST be maintained for public modules once code exists).
  Depends on T007, T008.

**Checkpoint**: Solution builds; new test project links against the stub renderer.

---

## Phase 3: User Story 1 — Read lifecycle output at a glance (Priority: P1) 🎯 MVP

**Goal**: A rich, color-coded terminal projection of every `CommandReport` that
represents every populated field and invents none.

**Independent Test**: Render a report to a `StringWriter`-backed Spectre
`IAnsiConsole` (fixed-width, color-off profile) and confirm outcome state,
diagnostics-by-severity, generated-view currency, and next command all appear,
all sourced from the report.

### Tests for User Story 1 (write first, confirm failing)

- [X] T010 [P] [US1] Projection-completeness test in
  `tests/FS.GG.SDD.Cli.Tests/RichRenderingTests.fs`: render a populated report and
  assert every populated `CommandReport` field (Command/WorkId/DryRun, Outcome,
  ChangedArtifacts, the `Some` stage summary, GeneratedViews, Diagnostics,
  NextAction, GovernanceCompatibility) is represented and no foreign fact appears
  (INV-5, C-3, SC-004).
- [X] T011 [P] [US1] Section-rendering test in
  `tests/FS.GG.SDD.Cli.Tests/RichRenderingTests.fs`: assert the rich output visually
  distinguishes the outcome state, diagnostics grouped by `DiagnosticSeverity`,
  generated-view currency (stale emphasized), and the next lifecycle command
  (FR-007); and that a no-diagnostics report and a many-diagnostics report both
  render without inventing/dropping facts (edge cases).
- [X] T012 [P] [US1] Purity test in
  `tests/FS.GG.SDD.Cli.Tests/RichRenderingTests.fs`: `renderRichTo` mutates only the
  supplied console — `serializeReport report` bytes and the report object are
  unchanged before/after a render (C-4).
- [X] T012a [P] [US1] Report-shape coverage test in
  `tests/FS.GG.SDD.Cli.Tests/RichRenderingTests.fs`: render a representative report
  for each populated stage summary (`Specification` … `Refresh`), an agents/refresh
  report whose `NextAction` is `None` (asserting no next lifecycle stage is implied
  — spec edge case "commands that are not lifecycle stages"), and a `Blocked`/error
  report, confirming each shape renders without throwing and without
  inventing/dropping facts (FR-001, INV-5). (The unparseable/unknown-command and
  no-args bootstrap paths stay JSON-to-stderr and are out of scope here per the
  output-format-selection contract.)
- [X] T012b [P] [US1] Width-adaptation test in
  `tests/FS.GG.SDD.Cli.Tests/RichRenderingTests.fs`: render to color-off
  `IAnsiConsole` profiles at a very narrow fixed width and at an unset/unknown width
  and assert the output is non-empty, contains the outcome and next command, and
  never throws — the layout adapts or degrades rather than failing (spec edge case
  "terminal width is unknown or very narrow"; `TerminalCapabilities.Width`).

### Implementation for User Story 1

- [X] T013 [US1] Implement `renderRichTo: IAnsiConsole -> CommandReport -> unit`
  in `src/FS.GG.SDD.Cli/Rendering.fs` per the data-model projection mapping: header
  (Command/WorkId/DryRun), outcome badge (green/yellow/red/dim), changed-artifact
  count+list, the populated per-stage panel, generated-view currency table,
  diagnostics table grouped by severity, NextAction callout, and optional
  GovernanceCompatibility section. Pure over the report. Depends on T010–T012b.

**Checkpoint**: Rich rendering complete and asserted via a real color-off Spectre
console; US1 testable independently.

---

## Phase 4: User Story 2 — Safe, clean output in non-interactive contexts (Priority: P1)

**Goal**: Selecting Rich never leaks ANSI into redirected/piped/color-off output,
never changes JSON bytes/exit codes, and never changes stream routing.

**Independent Test**: Redirect a `--rich` run to a file and confirm zero ANSI and
byte-identity with `--text`; confirm default JSON bytes/exit code are unchanged.

### Tests for User Story 2 (write first, confirm failing)

- [X] T014 [P] [US2] Automation-invariance test in
  `tests/FS.GG.SDD.Cli.Tests/DegradationTests.fs`: for the same report,
  `serializeReport` bytes and the `CommandReport` object are identical regardless
  of `OutputFormat`, and `resolve Json`/`resolve Text` return the existing JSON /
  `renderText` projections with `UsedRichRendering=false` (INV-1, C-2, SC-002).
- [X] T015 [P] [US2] No-ANSI degradation test in
  `tests/FS.GG.SDD.Cli.Tests/DegradationTests.fs`: `resolve Rich caps report` with
  `IsInteractive=false` or `ColorEnabled=false` returns `UsedRichRendering=false`
  and `Text` byte-identical to `CommandRendering.renderText report` with zero
  `\x1b`; and `renderRichTo` to a color-off `IAnsiConsole` emits zero ANSI
  sequences (INV-2, C-1, C-5, SC-003).
- [X] T016 [P] [US2] Stream/exit-parity test in
  `tests/FS.GG.SDD.Cli.Tests/DegradationTests.fs`: for a `Blocked` report and a
  succeeding report, the chosen stream (stderr vs stdout) and
  `exitCodeForReport report` are identical across `Json`, `Text`, and `Rich`
  (INV-3, C-1, SC-005). Assert the routing rule that backs Program.fs.

### Implementation for User Story 2

- [X] T017 [US2] Implement `detectCapabilities: unit -> TerminalCapabilities` in
  `src/FS.GG.SDD.Cli/Rendering.fs`: `IsInteractive` from
  `Console.IsOutputRedirected`; `ColorEnabled` false when `NO_COLOR` is present
  (any value) or `TERM=dumb`; `Width` from the console when known else `None`
  (output-format-selection contract). Isolated impure edge step.
- [X] T018 [US2] Implement `resolve: OutputFormat -> TerminalCapabilities ->
  CommandReport -> RichRenderResult` in `src/FS.GG.SDD.Cli/Rendering.fs`: Json →
  `serializeReport`; Text → `renderText`; Rich → rich (via a color-on
  StringWriter-backed `IAnsiConsole` whose profile width is set from
  `capabilities.Width` when `Some` and left at the Spectre default when `None`)
  only when `IsInteractive && ColorEnabled`, else `renderText` with
  `UsedRichRendering=false` (C-1, C-2). Depends on T013, T017.
- [X] T019 [US2] Wire `src/FS.GG.SDD.Cli/Program.fs`: detect capabilities, call
  `resolve` for the parsed format, and write the result — `Blocked` outcome to
  stderr, all others to stdout — replacing the current
  `if format = Text then renderText else serializeReport` branch; preserve
  `exitCodeForReport` and the unknown-command/no-args JSON-to-stderr paths.
  Depends on T018.

**Checkpoint**: US1 + US2 both work; redirected/`NO_COLOR` output is clean and the
JSON contract is provably unchanged.

---

## Phase 5: User Story 3 — Choose the right format for the task (Priority: P2)

**Goal**: Explicit, predictable selection among `--json`, `--text`, and `--rich`
with defined precedence; each yields the corresponding projection of the same
report.

**Independent Test**: Invoke one command three ways (`--json`, `--text`,
`--rich`) and confirm each returns the matching projection with consistent
outcome and exit code; confirm precedence when multiple flags are passed.

### Tests for User Story 3 (write first, confirm failing)

- [X] T020 [P] [US3] Flag-precedence test in
  `tests/FS.GG.SDD.Cli.Tests/FormatSelectionTests.fs`: the format parser resolves
  none→`Json`, `--json`→`Json`, `--text`→`Text`, `--rich`→`Rich`, and with
  multiple flags applies precedence `--rich` > `--text` > `--json` > default
  (output-format-selection contract).
- [X] T021 [P] [US3] Format-equivalence test in
  `tests/FS.GG.SDD.Cli.Tests/FormatSelectionTests.fs`: for the same report, the
  three formats project the same report with consistent outcome and exit code;
  explicit `--json` equals the default bytes (quickstart Scenarios 4–5).

### Implementation for User Story 3

- [X] T022 [US3] Update the format parser in `src/FS.GG.SDD.Cli/Program.fs`
  (currently `outputFormat = if hasFlag "--text" then Text else Json`) to also
  accept `--json` and `--rich` and apply the precedence
  `--rich` > `--text` > `--json` > default. Coordinates with the write path from
  T019 (same file). Depends on T019.

**Checkpoint**: All three formats are explicitly selectable with defined
precedence; all stories independently functional.

---

## Phase 6: Polish, Agent/Doc Alignment & Verification

**Purpose**: Keep both agent surfaces and docs consistent (FR-010), then produce
real evidence per the plan's Phase 6.

### Agent + documentation alignment (FR-010, Principle VII)

- [X] T023 [P] Update `CLAUDE.md` and `AGENTS.md` to describe the three output
  formats (`--json` default, `--text`, `--rich`) and the non-interactive/`NO_COLOR`
  degradation rule, equivalently on both surfaces.
- [X] T024 [P] Update both the Claude skill
  (`.claude/skills/fs-gg-sdd-project/SKILL.md`) and the Codex skill
  (`.codex/skills/fs-gg-sdd-project/SKILL.md`) guidance for output formats to match
  CLAUDE.md/AGENTS.md exactly, equivalently on both surfaces (keep Claude and Codex
  behavior aligned, Principle VII).
- [X] T025 [P] Update product docs (`docs/quickstart.md` and any format reference)
  and add a release-notes mention of the new `--rich` format; do not add
  rendering-specific package names/paths to generic SDD behavior docs.

### Verification & evidence (per plan Phase 6)

- [X] T026 Release build + full suite: `dotnet build -c Release FS.GG.SDD.sln`
  and `dotnet test -c Release FS.GG.SDD.sln`; confirm all new tests pass and every
  prior test still passes (quickstart Build & test; SC-002).
- [X] T027 Capture an FSI public-surface transcript for the new
  `FS.GG.SDD.Cli.Rendering` module and confirm the CLI
  `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` (from T009a) is current,
  alongside the existing `FS.GG.SDD.Commands.Tests` baseline for the
  `OutputFormat.Rich` change (Principle III). Depends on T026, T009a.
- [X] T028 Disposable-directory CLI smoke for `--rich`/`--json`/`--text` including
  redirected (`> out.txt`, `grep -c $'\x1b'` → 0), `NO_COLOR=1`, and a `Blocked`
  outcome to stderr — quickstart Scenarios 1–6; also confirm no golden/snapshot
  fixture captures rich output (FR-008). Depends on T026.
- [X] T029 Performance note: confirm no measurable change to the JSON/automation
  path (rich is opt-in), recorded with the evidence.
- [X] T030 SDD/Governance boundary review (rendering is SDD-CLI-local; no
  Governance route/audit/profile/freshness concerns introduced) and artifact
  traceability for this feature into `readiness/`.
- [X] T031 Evidence obligations + Principle IV/V note: record declared
  `evidence.yml` for this feature and note that no new MVU `Model`/`Msg` loop is
  introduced — Principle V is satisfied by isolating the impure
  `detectCapabilities`/console-write edge while `renderRichTo`/`resolve` stay pure
  (data-model; plan Constitution Check V). Depends on T026, T028.

---

## Dependencies & Execution Order

### Phase order

1. **Setup (Phase 1)** — no dependencies.
2. **Foundational (Phase 2)** — depends on Setup; BLOCKS all user stories.
3. **User Stories (Phases 3–5)** — depend on Foundational. US1 (P1) is the MVP;
   US2 (P1) builds on the US1 renderer (T018 needs T013); US3 (P2) builds on the
   US2 write path (T022 needs T019).
4. **Polish (Phase 6)** — depends on the desired stories being complete.

### Cross-phase dependencies (beyond plain phase ordering)

- T013 (US1 render) is consumed by T018 (US2 resolve) and T019 (US2 wiring).
- T019 (US2 wiring) precedes T022 (US3 parser precedence) — same file
  `Program.fs`.
- T009/T009a/T027 baseline tasks depend on a build of the changed surface
  (T009a scaffolds the CLI project's first public-surface baseline).

### Within each user story

- Tests are authored first and must FAIL before the implementation task.
- The compiling stub `Rendering.fs` (T008) exists so tests link before T013/T017/
  T018 fill in real behavior.

### Parallel opportunities

- T003 is `[P]` within Setup.
- All US1 tests (T010–T012b), all US2 tests (T014–T016), and all US3 tests
  (T020–T021) are `[P]` within their story (independent assertions; US1/US2 share
  one test file, US3 a separate file).
- Agent/doc tasks T023–T025 are `[P]` (different files).
- With Foundational done, US1 and US3's *test authoring* can proceed in parallel,
  but US2's `resolve` and US3's parser depend on US1's renderer / US2's wiring as
  noted above.

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (fsi + stub, suite builds).
2. Phase 3 US1 → rich rendering of every report field, asserted on a real
   color-off Spectre console.
3. **STOP and VALIDATE**: projection completeness + purity green.

### Incremental delivery

1. Setup + Foundational → buildable foundation.
2. US1 → rich projection exists (MVP).
3. US2 → safe degradation + automation invariance + stream/exit parity.
4. US3 → explicit format selection with precedence.
5. Phase 6 → agent/doc alignment, Release build, smoke, evidence.

---

## Notes

- `[P]` = different files or independent assertions, no incomplete-task
  dependency within the phase.
- Never mark a failing task `[X]`; narrow scope and document rather than weaken an
  assertion (Status legend / discipline).
- Rich output is presentation-only: it is excluded from any deterministic golden/
  snapshot contract (FR-008); determinism guarantees apply to JSON only.

## Completion notes (implementation evidence)

- Spectre.Console pinned at `0.57.0`; renderer lives in `FS.GG.SDD.Cli`
  (`Rendering.fsi`/`Rendering.fs`); core packable library is untouched.
- The format parser is the pure `Rendering.selectFormat`; `Program.fs` calls it
  plus `detectCapabilities` + `resolve`. CLI public-surface baseline added
  (`tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline`, 4 vals). The Commands
  surface baseline was unchanged (adding `OutputFormat.Rich` adds no static
  method), so T009 required no refresh.
- Full Release suite green: **400 tests** (Artifacts 103, **Cli 32**,
  Commands 265); both public-surface baselines pass.
- Vertical-slice evidence under `readiness/019-spectre-rendering/`:
  `cli-smoke.md` (the `fsgg-sdd` binary: default==`--json`, `--rich`-redirected
  ==`--text` with 0 ESC, `NO_COLOR` 0 ESC, Blocked→stderr, exit unchanged),
  `fsi-rich-surface.txt` (real forced-color rich ANSI through public
  `renderRichTo`), `evidence.yml` (declared evidence). The 32 CLI tests exercise
  the renderer through a real color-off Spectre console (no mocks). The live
  interactive-TTY path can only be observed through the public Spectre surface in
  this headless environment, not a PTY — disclosed here, no synthetic evidence
  used.
- T025: the feature is **additive-only**, so per `docs/release/migrations/README.md`
  no migration note is required; the new `--rich` format is documented in
  `docs/quickstart.md` (Output formats), `CLAUDE.md`, `AGENTS.md`, and both
  `fs-gg-sdd-project` skills, equivalently.
- Principle IV: not applicable — no new MVU `Model`/`Msg` loop; the impure edge
  (`detectCapabilities`, console write) is isolated while `renderRichTo`/`resolve`
  stay pure (T031).
