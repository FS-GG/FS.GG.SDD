# Implementation Plan: Rich Spectre.Console Rendering of the `validation-report`

**Branch**: `021-rich-validation-report` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/021-rich-validation-report/spec.md`

**Change Tier**: Tier 1 (contracted change: adds two public functions to the CLI
`Rendering` module surface — `renderValidationRichTo` and `resolveValidation` —
and changes `fsgg-sdd validate --rich` from "degrade to text" to "render richly").
No `validation-report` schema, field, matrix, lifecycle stage, or `CommandReport`
change (FR-009).

## Summary

Deliver the one SDD-owned deferral remaining after features 019 and 020: a rich,
human-oriented Spectre.Console rendering of the `validation-report` emitted by
`fsgg-sdd validate`. Today `validate --rich` silently degrades to `--text`
(feature 020 research Decision 6 scoped rich out because the `validation-report`
is a contract distinct from `CommandReport`). This feature adds the rich
projection at the **CLI/presentation edge** — exactly where feature 019 added
`--rich` for the per-command `CommandReport` — so the `FS.GG.SDD.Validation`
library and its `validation-report` JSON contract stay Spectre-free and
byte-for-byte unchanged.

The rich renderer is a **pure projection over the same `ValidationReport` object**
the JSON and plain-text projections use. It adds no facts and invents none,
changes no JSON byte / `sensed` fence / exit code / stream, and degrades to the
existing plain-text projection (zero ANSI) whenever output is non-interactive or
color is disabled. It surfaces the overall verdict, the summary counts, a
per-matrix status rollup, and every non-passing cell (with matrix, coordinates,
status, and failure diagnostic), emphasizing the failing categories (`fail`,
`coverageGap`, `notValidated`) that drive the non-zero exit distinctly from
`skippedWithReason` and `pass`.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (repo default), `LangVersion=preview`.

**Primary Dependencies**: `Spectre.Console` (already referenced by the CLI project
and pinned centrally in `Directory.Packages.props` since feature 019); existing
`FS.GG.SDD.Validation` (consumed unchanged for `ValidationReport`, `serialize`,
`renderText`), `FS.GG.SDD.Commands` (for the existing `OutputFormat` type and the
`Rendering` module this feature extends), `FSharp.Core`. No new package reference.

**Storage**: N/A — no persisted artifacts produced by the rich path. `--out`
continues to persist a deterministic projection (JSON or plain text); rich is
ephemeral interactive-stdout presentation only (FR-010).

**Testing**: xUnit in the existing `FS.GG.SDD.Cli.Tests` project. Rich content is
asserted by rendering to a `StringWriter`-backed Spectre `IAnsiConsole` with a
fixed, color-off, fixed-width profile (the established pattern in
`RichRenderingTests.fs`/`DegradationTests.fs`). End-to-end exit/stream/no-ANSI
behavior is exercised via the real host binary as in `ValidateCommandTests.fs`.

**Target Platform**: Cross-platform terminal CLI (Linux/macOS/Windows).

**Project Type**: Single repository, library + CLI host. The validation rich
renderer lives at the CLI edge alongside the existing `CommandReport` renderer.

**Performance Goals**: No measurable change to the JSON/automation path (rich is
opt-in and selected after the report is already computed). Rich rendering is
interactive-use latency over an already-materialized report (sub-100ms) — not a
hot path; the expensive work is the validation sweep itself, which is unchanged.

**Constraints**: JSON stays the sole deterministic automation contract; the rich
projection is excluded from every golden/snapshot contract (it varies with
terminal width/color). Zero ANSI sequences in degraded/redirected output. The
`sensed` fence and `serialize` bytes are unchanged for identical inputs.

**Scale/Scope**: The four declared matrices (`lifecycle-output`, `determinism`,
`baseline-conformance`, `compatibility`) each with a dimensions list and an
enumerated cell cross-product; five cell statuses (`pass`, `fail`,
`skippedWithReason`, `coverageGap`, `notValidated`). Two new public CLI functions
+ their `.fsi` entries + baseline lines; one rewritten `printValidate` dispatch in
`Program.fs`; agent/doc alignment. No new project, no new package.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | PASS | New entries in `Rendering.fsi` (`renderValidationRichTo`, `resolveValidation`) authored before `.fs`; semantic tests through the public surface precede implementation. |
| II. Structured artifacts are the machine contract | PASS | Rich is presentation only; the `validation-report` JSON remains authoritative for automation. Plan states JSON wins, rich is never a tool contract and never persisted (data-model INV-1/INV-4; FR-003/FR-008/FR-010). |
| III. Visibility in `.fsi` | PASS | `Rendering.fsi` gains the two functions; `PublicSurface.baseline` refreshed to match (SurfaceBaselineTests). |
| IV. Idiomatic simplicity | PASS | Reuses the existing Spectre-based `Rendering` module, `TerminalCapabilities`, `RichRenderResult`, `selectFormat`, `detectCapabilities`. No new package, project, or complex F# feature. |
| V. Elmish/MVU boundary for I/O | PASS | Capability detection + console writing stay at the CLI edge (already isolated); `report -> rich string` is pure over the report. The validation sweep's own MVU boundary (`ValidationRunner`) is untouched. |
| VI. Test evidence mandatory | PASS | New tests fail before / pass after: projection-completeness over `ValidationReport`, automation-invariance (`serialize`/`renderText` bytes and `sensed` fence unchanged), no-ANSI-on-degradation, status differentiation, stream/exit parity, single-failing-cell isolation. Real color-off Spectre console, no mocks. |
| VII. Agent + human one contract | PASS | CLAUDE.md, AGENTS.md, and both `fs-gg-sdd-project` skills + `docs/` updated equivalently so the deferred `--rich` note becomes "available for `validate`" (FR-011). |
| VIII. Observability & safe failure | PASS | Non-interactive/color-disabled terminals degrade explicitly to the plain-text projection rather than emitting corrupt ANSI; rich never alters the deterministic contract or exit code. |

**Result**: PASS — no violations; Complexity Tracking not required.

Lifecycle-feature plan checklist (Development Workflow):

- **Authored artifacts**: none changed (no authored-source schema touched).
- **Structured machine contracts**: `ValidationReport` / `serialize` / `renderText`
  / the `sensed` fence unchanged; `CommandReport` untouched; `validate` stays a
  CLI-level command (not a `SddCommand`).
- **Generated views**: none changed; rich output is not a generated view and is
  not added to `release-readiness.json` (the `validation-report` declared
  exception in `docs/release/schema-reference.md` is unaffected).
- **Schema version & migration posture**: no schema version bump — no
  `validation-report` field change; the CLI `Rendering` surface gains two
  functions (additive). No migration note required beyond a release-notes mention
  that `validate --rich` now renders richly.
- **Agent-facing behavior (Claude & Codex)**: both surfaces and the docs describe
  the now-available rich format for `fsgg-sdd validate` identically (FR-011).
- **Optional Governance integration**: none; rendering is SDD-CLI-local. No
  Governance runtime or verdict involved.
- **Tests/fixtures for stale or conflicting artifacts**: N/A (no new artifacts);
  invariance tests guard the existing JSON/text/sensed contracts.

## Project Structure

### Documentation (this feature)

```text
specs/021-rich-validation-report/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 types & projection mapping
├── quickstart.md        # Phase 1 validation scenarios
├── contracts/
│   ├── validation-output-format-selection.md
│   └── validation-rich-rendering-projection.md
├── checklists/
│   └── requirements.md  # (existing; from /speckit-specify)
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Validation/         # UNCHANGED: ValidationReport / serialize / renderText
│                                 #   stay Spectre-free and byte-for-byte unchanged
├── FS.GG.SDD.Commands/           # UNCHANGED: OutputFormat already has Rich/Text/Json
└── FS.GG.SDD.Cli/
    ├── FS.GG.SDD.Cli.fsproj      # UNCHANGED: already references Spectre.Console + Validation
    ├── Rendering.fsi             # CHANGED: + renderValidationRichTo, + resolveValidation
    ├── Rendering.fs              # CHANGED: + validation rich projection + resolve
    └── Program.fs               # CHANGED: printValidate uses selectFormat + resolveValidation
                                  #   for stdout; --out persists deterministic projection only

tests/
└── FS.GG.SDD.Cli.Tests/
    ├── FS.GG.SDD.Cli.Tests.fsproj           # CHANGED: + new test file compile item
    ├── ValidationRichRenderingTests.fs      # NEW: projection completeness / status
    │                                        #   differentiation / verdict / no-ANSI / invariance
    ├── ValidateCommandTests.fs              # CHANGED: + end-to-end --rich redirected (no ANSI)
    └── PublicSurface.baseline               # CHANGED: + two new Rendering entries
```

**Structure Decision**: Single-repo library + CLI layout (existing). The
validation rich renderer is added to the **CLI executable's** existing `Rendering`
module so Spectre.Console never enters the packable, deliberately Spectre-free
`FS.GG.SDD.Validation` surface (spec Assumption; research Decision 1). It reuses
the feature-019 `TerminalCapabilities` / `RichRenderResult` / `selectFormat` /
`detectCapabilities` primitives rather than introducing parallel ones.

## Phase 0 — Research

Complete. See [research.md](./research.md): renderer location (extend the CLI
`Rendering` module, not the Spectre-free Validation library); reuse vs. duplicate
of the format-selection/capability primitives; the `validate`-specific stdout vs.
`--out` persistence split; how `--rich --out` resolves (persist deterministic
text, render rich to stdout); projection-completeness strategy over the structured
report; the determinism boundary and the optional (currently-unpopulated) `sensed`
block; and agent-contract alignment. No `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

Complete:

- [data-model.md](./data-model.md) — the consumed `ValidationReport` shape, the
  report→rich projection mapping per matrix/cell/status, the reused
  `TerminalCapabilities`/`RichRenderResult` types, and invariants INV-1…INV-6.
- [contracts/validation-output-format-selection.md](./contracts/validation-output-format-selection.md)
  — `validate` flag precedence, the capability-degradation table, the stdout vs.
  `--out` persistence rule, and stream/exit parity.
- [contracts/validation-rich-rendering-projection.md](./contracts/validation-rich-rendering-projection.md)
  — the two new public `Rendering` functions and behavioral contract C-1…C-6.
- [quickstart.md](./quickstart.md) — runnable validation scenarios mapping to the
  acceptance criteria and success criteria.

Agent context update: CLAUDE.md plan pointer updated to this plan; the
`speckit.agent-context.update` after_plan hook (optional) may refresh the managed
section.

## Phase 2 — Task planning approach (preview only)

`/speckit-tasks` will generate `tasks.md`. Expected shape, ordered by the
Constitution's spec→fsi→tests→impl discipline and the US priorities:

1. **Contract/signature (US1, US3)**: add `renderValidationRichTo` and
   `resolveValidation` to `Rendering.fsi`; refresh `PublicSurface.baseline`.
2. **Tests-first (US1, US2, US3 — [P] parallelizable)**: automation-invariance
   (`serialize`/`renderText` bytes + `sensed` fence unchanged; resolve never
   mutates the report); projection-completeness (verdict, summary counts, each
   matrix name+dimensions, every non-passing cell's coordinates+status, failure
   diagnostics; no invented facts; `schemaVersion`/`generatorVersion` not
   required); status differentiation (`fail`/`coverageGap`/`notValidated` distinct
   from `skippedWithReason`/`pass`); no-ANSI-on-degradation; single-failing-cell
   isolation; stream/exit parity.
3. **Implementation (US1)**: `renderValidationRichTo` over verdict + summary +
   per-matrix rollup + non-passing cells; (US2) `resolveValidation` degradation
   reusing `TerminalCapabilities`; wire `Program.fs` `printValidate` to
   `selectFormat` + `resolveValidation` for stdout while keeping `--out`
   deterministic.
4. **Agent/doc alignment (US3/FR-011)**: update CLAUDE.md, AGENTS.md, both
   `fs-gg-sdd-project` skills, and `docs/` so the deferred `--rich` note becomes
   the available rich format for `validate`; note the change in release notes.
5. **Verification/evidence**: Release build, full suite, FSI public-surface
   transcript for the two new functions, disposable-directory CLI smoke for
   `validate --rich`/`--json`/`--text` incl. redirected + `NO_COLOR`, performance
   note, SDD/Governance boundary review, artifact traceability → `readiness/`.

## Complexity Tracking

No constitution violations require justification. This feature adds no package, no
project, and no new contract — it extends an existing Spectre-based CLI module with
two functions and flips `validate --rich` from degrade-to-text to render-rich,
keeping the validation library and its JSON contract untouched.
