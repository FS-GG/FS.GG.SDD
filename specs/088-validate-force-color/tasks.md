---
description: "Task list for 088 — force-color override + capture-safe Markdown report card"
---

# Tasks: Force-Color Override + Capture-Safe Markdown Report Card

**Input**: Design documents from `specs/088-validate-force-color/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (public CLI surface + new public functions + `.fsi` + surface baseline). Tests are REQUIRED (Constitution VI).

**Organization**: Phases run in sequence; `[P]` tasks within a phase touch different files and may run in parallel. Stories: `[US1]` force-color, `[US2]` Markdown card, `[US3]` precedence & invariants.

**MVU note**: `validate` is a thin CLI verb over pure projections; env/flag reads are the only edge reads (existing `Program.fs` boundary). No new `Model`/`Msg`/`Effect` boundary is introduced, so per-Principle-V no MVU-contract tasks apply; the pure-projection tests below are the evidence.

---

## Phase 1: Setup

- [X] T001 Confirm build baseline is green before changes: `dotnet build FS.GG.SDD.sln` and `dotnet test tests/FS.GG.SDD.Cli.Tests tests/FS.GG.SDD.Validation.Tests` (record the pre-change pass count for regression comparison).

---

## Phase 2: Foundational — public `.fsi` contracts (BLOCKS all stories)

**Purpose**: Declare the public surface before bodies (Constitution I & III). No behavior yet.

- [X] T002 [P] In `src/FS.GG.SDD.Validation/ValidationContracts.fsi`, declare `val renderMarkdown: report: ValidationReport -> string` (peer of `serialize`/`renderText`), with a doc comment: deterministic, ANSI-free Markdown report card.
- [X] T003 [P] In `src/FS.GG.SDD.Cli/Rendering.fsi`: (a) change `detectCapabilities` to `val detectCapabilities: forceColor: bool -> outputRedirected: bool -> TerminalCapabilities`; (b) add `val forceColorRequested: args: string list -> bool`; (c) add `type ValidationFormat = Standard of OutputFormat | MarkdownCard` and `val selectValidationFormat: args: string list -> ValidationFormat`.

**Checkpoint**: Signatures compile-declared (bodies still to come; project will not build until Phase 3/4 bodies land — that is expected).

---

## Phase 3: User Story 1 — Force-color override (Priority: P1) 🎯 MVP

**Goal**: `FORCE_COLOR`/`--force-color` re-enable rich ANSI on a redirected/`TERM=dumb` sink, uniformly across `--rich`-capable commands; `NO_COLOR` always wins.

**Independent Test**: `FORCE_COLOR=1 fsgg-sdd validate --rich | cat` emits ANSI; adding `NO_COLOR=1` makes it plain.

### Tests for US1 (write first, ensure they FAIL)

- [X] T004 [P] [US1] New `tests/FS.GG.SDD.Cli.Tests/ForceColorTests.fs`: `forceColorRequested` truth over `FORCE_COLOR` values (unset/``/`0` → false; `1`/`true`/`always` → true) and `--force-color` flag; set/restore env around each case.
- [X] T005 [P] [US1] In `ForceColorTests.fs`: `detectCapabilities` effective-capability truth table (contracts/color-gate.md) — redirected+force → `IsInteractive`; `TERM=dumb`+force → `ColorEnabled`; `NO_COLOR`+force → not `ColorEnabled`; `Width` stays `None` when redirected even with force.
- [X] T006 [US1] In `tests/FS.GG.SDD.Cli.Tests/DegradationTests.fs`: add a redirected-but-forced capability and assert `resolve`/`resolveValidation` `Rich` render richly (ANSI `` present); with `NO_COLOR` they degrade to zero-ANSI plain text byte-identical to `--text`.

### Implementation for US1

- [X] T007 [US1] In `src/FS.GG.SDD.Cli/Rendering.fs`: add `forceColorEnv` (boolean-ish over `FORCE_COLOR`) and `forceColorRequested args = forceColorEnv() || List.contains "--force-color" args`.
- [X] T008 [US1] In `Rendering.fs` `detectCapabilities`: add the `forceColor` param and compute effective `IsInteractive = (not outputRedirected) || forceColor` and `ColorEnabled = (not noColorPresent) && ((not dumbTerminal) || forceColor)`; keep `Width` on the raw `outputRedirected`. Comment the `NO_COLOR > force > sensing` precedence.
- [X] T009 [US1] Thread the signal into every `detectCapabilities` call site so the gate is uniform (FR-005): `src/FS.GG.SDD.Cli/Program.fs` lines ~112 (validate), ~170 (help), ~212 & ~259 (generic command path), and `src/FS.GG.SDD.Cli/RegistryValidate.fs` ~156 — pass `forceColorRequested <that command's args>`.

**Checkpoint**: US1 tests green; `FORCE_COLOR`/`--force-color` work across commands; `NO_COLOR` wins.

---

## Phase 4: User Story 2 — Capture-safe Markdown report card (Priority: P1)

**Goal**: `validate --markdown` emits a deterministic, ANSI-free Markdown projection of the `validation-report` with fact-parity to `--rich`.

**Independent Test**: `fsgg-sdd validate --markdown` twice → byte-identical Markdown, zero ANSI, verdict + five counts + non-passing cells present.

### Tests for US2 (write first, ensure they FAIL)

- [X] T010 [P] [US2] New `tests/FS.GG.SDD.Validation.Tests/ValidationMarkdownTests.fs`: build a report with a passing matrix + a matrix with one `Fail` (reuse the suite's report builder); assert `renderMarkdown` contains `# Validation Report`, `**Verdict:** not passed`, the Summary table with the five counts, the Matrices rollup, and the failing cell bullet with coordinates + `**fail**` + message; passing matrix shows `All evaluated cells pass.`.
- [X] T011 [P] [US2] In `ValidationMarkdownTests.fs`: determinism (two calls byte-identical); zero ANSI (no ``); all-pass report → `**Verdict:** passed`, no cell enumerated, still a well-formed non-empty document; report with absent `schemaVersion`/`generatorVersion` renders without error and omits them.
- [X] T012 [P] [US2] In `ValidationMarkdownTests.fs`: `|` in a matrix name is escaped `\|` inside the rollup table (table not broken).

### Implementation for US2

- [X] T013 [US2] In `src/FS.GG.SDD.Validation/ValidationContracts.fs`: implement `renderMarkdown` per contracts/markdown-report-card.md — title, verdict line, Summary table, Matrices rollup (matrices sorted by name; reuse the per-status count logic), Non-passing cells section (cells sorted by coordinate text; `cellStatusToken`-equivalent tokens; detail suffix; `All evaluated cells pass.` when none), `|`-escaping for table cells, no sensed/width/ANSI.

**Checkpoint**: US2 unit tests green; Markdown card is deterministic and parity-complete.

---

## Phase 5: User Story 3 — Selection precedence, `--out`, and invariants (Priority: P2)

**Goal**: `--markdown` participates in `--rich > --markdown > --text > --json > default`; `--out` persists the card; JSON/text/default bytes, exit code, and routing are unchanged.

**Independent Test**: `validate --markdown --text --json` → Markdown; `validate --rich --markdown` → Rich; default vs `--json` byte-identical; `FORCE_COLOR=1 validate --json` == `validate --json`.

### Tests for US3 (write first, ensure they FAIL)

- [X] T014 [P] [US3] In `tests/FS.GG.SDD.Cli.Tests/FormatSelectionTests.fs`: `selectValidationFormat` returns the precedence winner for each single flag, for `--rich --markdown` (→ `Standard Rich`), `--markdown --text` and `--markdown --json` (→ `MarkdownCard`), and no-flag (→ `Standard Json`).
- [X] T015 [P] [US3] In `tests/FS.GG.SDD.Cli.Tests/ValidationRichRenderingTests.fs`: through the validate rendering path, `MarkdownCard` selection yields `renderMarkdown` bytes; `Standard Json`/`Text` remain byte-identical to `serialize`/`renderText`; force-color present vs absent leaves JSON/text/markdown bytes identical.
- [X] T016 [US3] Add a CLI-invocation test (reuse the process/harness pattern used for existing `validate` CLI tests, or a `printValidate`-level test if one exists): `validate --markdown --out <tmp>` writes the Markdown card and exits 0/1 on verdict only; a bad `--out` path exits 1 with a stderr diagnostic (unchanged behavior), stdout still carries the report.

### Implementation for US3

- [X] T017 [US3] In `Rendering.fs`, implement `selectValidationFormat` with the 4-way precedence (rich > markdown > text > json > default).
- [X] T018 [US3] In `src/FS.GG.SDD.Cli/Program.fs` `printValidate`: replace `selectFormat`/`resolveValidation` wiring with `selectValidationFormat rest` → `MarkdownCard` renders `ValidationContracts.renderMarkdown report`; `Standard fmt` renders `(resolveValidation fmt caps report).Text`. Extend `--out` persistence: `MarkdownCard -> renderMarkdown`, `Standard Json -> serialize`, else `renderText`. Keep the exit-code and stream-routing logic exactly as-is.

**Checkpoint**: All three stories green; contracts unchanged.

---

## Phase 6: Polish & Cross-Cutting

- [X] T019 [P] Refresh `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` for the changed `detectCapabilities` arity and new public members (`forceColorRequested`, `selectValidationFormat`, `ValidationFormat`, and `ValidationContracts.renderMarkdown`); run `SurfaceBaselineTests` green.
- [X] T020 [P] Update `tests/FS.GG.SDD.Cli.Tests/HelpRenderingTests.fs` arity-1 `detectCapabilities true/false` calls to `detectCapabilities false true/false` (force-color off) so the suite compiles.
- [X] T021 [P] Document `--force-color`, `FORCE_COLOR` (boolean-ish, `NO_COLOR` wins), and `--markdown` in the validate command reference doc under `docs/` (locate the existing validate/CLI reference; if the `fs-gg-sdd-validate` skill note enumerates projections, add `--markdown` there too).
- [X] T022 Run `specs/088-validate-force-color/quickstart.md` scenarios 1–9 against the built CLI and confirm expected outcomes; run the `fsgg-sdd validate` self-check and the full `dotnet test` suite.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (`.fsi` contracts, blocking)** → **Phases 3/4 (US1, US2 — independent, may proceed in parallel)** → **Phase 5 (US3 — depends on US1 gate + US2 renderer + the selector)** → **Phase 6 (Polish)**.
- Within a story: tests before implementation (Constitution VI); the `.fsi` (Phase 2) before bodies.
- T009 depends on T007/T008 (helper + signature). T018 depends on T013 (renderMarkdown) and T017 (selector). T019/T020 depend on the final public surface (after T008/T017/T013).

## Parallel Opportunities

- Phase 2: T002 ∥ T003 (different files).
- Phase 3 vs Phase 4: US1 and US2 are independent once Phase 2 lands (different files: `Rendering.fs`/`Program.fs` vs `ValidationContracts.fs`).
- Test authoring within a story: T004∥T005, T010∥T011∥T012, T014∥T015 (different assertions/files).
- Phase 6: T019 ∥ T020 ∥ T021.

## MVP Scope

Either P1 story is independently shippable and resolves half of FS.GG.SDD#172. Suggested MVP: **US1 (force-color)** — smallest surface, immediately unblocks the human `--rich` report under an agent harness. Full issue closure needs **US1 + US2**.
