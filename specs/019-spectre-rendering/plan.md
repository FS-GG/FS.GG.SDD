# Implementation Plan: Rich Spectre.Console CLI Rendering

**Branch**: `019-spectre-rendering` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-spectre-rendering/spec.md`

**Change Tier**: Tier 1 (contracted change: extends the public `OutputFormat`
type, adds a CLI rendering module with `.fsi`, and adds CLI format flags).

## Summary

Add a third, human-oriented projection of the existing `CommandReport` to the
`fsgg-sdd` CLI: a rich Spectre.Console rendering selectable with `--rich`,
alongside the unchanged deterministic JSON default and the existing plain-text
projection. The rich renderer is a **pure projection over the same report object**
that JSON and plain text use — it adds no facts, drops no facts, changes no JSON
byte or exit code, and degrades to plain text (zero ANSI) whenever output is
non-interactive or color is disabled. This delivers the one remaining SDD-owned
roadmap item (Phase 13's "Spectre.Console projections," deferred by feature 018).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (repo default), `LangVersion=preview`.

**Primary Dependencies**: `Spectre.Console` (new, CLI project only, pinned
centrally in `Directory.Packages.props`); existing `FS.GG.SDD.Commands`,
`FSharp.Core`. `System.Text.Json` path unaffected.

**Storage**: N/A — no persisted artifacts. Rich output is ephemeral terminal
presentation only.

**Testing**: xUnit. New `FS.GG.SDD.Cli.Tests` project (ProjectReference to the CLI
exe) renders to a `StringWriter`-backed Spectre `IAnsiConsole` with a fixed,
color-off profile; mirrors the coverage style of the existing
`FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.

**Target Platform**: Cross-platform terminal CLI (Linux/macOS/Windows).

**Project Type**: Single repository, library + CLI host. Rendering lives at the
CLI edge.

**Performance Goals**: No measurable change to the JSON/automation path (rich is
opt-in). Rich rendering is interactive-use latency (sub-100ms for a single
report) — not a hot path.

**Constraints**: JSON stays the deterministic automation contract; rich output is
excluded from golden/snapshot determinism (it varies with terminal width/color).
Zero ANSI sequences in degraded/redirected output.

**Scale/Scope**: All 13 command report shapes (init, charter, specify, clarify,
checklist, plan, tasks, analyze, evidence, verify, ship, agents, refresh) plus the
unknown-command/error report. One new public DU case, one new CLI module + `.fsi`,
one new test project, one package reference.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | PASS | `.fsi` for the new `Rendering` module and updated `CommandTypes.fsi` authored before `.fs`; semantic tests through the public surface precede implementation. |
| II. Structured artifacts are the machine contract | PASS | Rich is presentation only; JSON remains authoritative for tools. Plan explicitly states JSON wins and rich is never a tool contract (data-model INV-1, INV-4). |
| III. Visibility in `.fsi` | PASS | New `Rendering.fsi`; `CommandTypes.fsi` updated for the `Rich` case; public-surface baseline refreshed. |
| IV. Idiomatic simplicity | PASS | One DU case + one CLI module; Spectre.Console is the named, idiomatic .NET console library (simpler than hand-rolled ANSI). No complex F# language features introduced. |
| V. Elmish/MVU boundary for I/O | PASS | Capability detection + console writing are isolated at the CLI edge; `report -> renderable/string` is pure and testable. |
| VI. Test evidence mandatory | PASS | New tests fail before / pass after: projection completeness, automation invariance (JSON/text bytes unchanged), no-ANSI-on-degradation, stream/exit parity. Rich content asserted with color-off fixed-width profile (real Spectre console, no mocks). |
| VII. Agent + human one contract | PASS | Claude and Codex guidance + format docs updated equivalently (FR-010). |
| VIII. Observability & safe failure | PASS | Undetectable/unsupported terminal capability degrades explicitly to plain text rather than emitting corrupt output. |

**Result**: PASS — no violations; Complexity Tracking not required.

Lifecycle-feature plan checklist (Development Workflow):

- **Authored artifacts**: none changed (no authored-source schema touched).
- **Structured machine contracts**: `CommandReport` / `serializeReport` unchanged;
  only the `OutputFormat` enum gains a presentation case.
- **Generated views**: none changed; rich output is not a generated view.
- **Schema version & migration posture**: no schema version bump
  (no `CommandReport` field change, additive enum case only). No migration note
  required beyond release-notes mention of the new format.
- **Agent-facing behavior (Claude & Codex)**: both describe the new `--rich`
  format and degradation rule identically.
- **Optional Governance integration**: none; rendering is SDD-CLI-local.
- **Tests/fixtures for stale or conflicting artifacts**: N/A (no new artifacts);
  invariance tests guard the existing JSON/text contracts.

## Project Structure

### Documentation (this feature)

```text
specs/019-spectre-rendering/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 types & projection mapping
├── quickstart.md        # Phase 1 validation scenarios
├── contracts/
│   ├── output-format-selection.md
│   └── rich-rendering-projection.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/          # unchanged
├── FS.GG.SDD.Commands/
│   ├── CommandTypes.fsi          # CHANGED: OutputFormat gains `Rich`; outputFormatValue maps it
│   ├── CommandTypes.fs           # CHANGED: same
│   └── CommandRendering.fs       # unchanged (renderText reused as fallback)
└── FS.GG.SDD.Cli/
    ├── FS.GG.SDD.Cli.fsproj      # CHANGED: + Spectre.Console PackageReference, + Rendering.fs(i)
    ├── Rendering.fsi             # NEW: TerminalCapabilities, RichRenderResult, detect/resolve/renderRichTo
    ├── Rendering.fs              # NEW: pure rich projection + edge capability detection
    └── Program.fs               # CHANGED: parse --rich/--json, resolve format, write via resolve

tests/
├── FS.GG.SDD.Artifacts.Tests/   # unchanged
├── FS.GG.SDD.Commands.Tests/    # may add OutputFormat round-trip assertion for `Rich`
└── FS.GG.SDD.Cli.Tests/         # NEW: rich projection / invariance / no-ANSI / stream-parity tests
    └── FS.GG.SDD.Cli.Tests.fsproj

Directory.Packages.props          # CHANGED: + <PackageVersion Include="Spectre.Console" .../>
FS.GG.SDD.sln                     # CHANGED: + FS.GG.SDD.Cli.Tests project
```

**Structure Decision**: Single-repo library + CLI layout (existing). The rich
renderer lives in the **CLI executable** so Spectre.Console never enters the
packable `FS.GG.SDD.Commands` surface (research Decision 1). A new
`FS.GG.SDD.Cli.Tests` project tests the renderer through its public module.

## Phase 0 — Research

Complete. See [research.md](./research.md): renderer location (CLI edge),
Spectre.Console choice + restore connectivity (verified reachable), format
selection & degradation table, determinism boundary, testing approach for a
non-deterministic renderer, and agent-contract alignment. No `NEEDS CLARIFICATION`
remain.

## Phase 1 — Design & Contracts

Complete:

- [data-model.md](./data-model.md) — `OutputFormat.Rich`, `TerminalCapabilities`,
  `RichRenderResult`, the report→rich projection mapping, and invariants INV-1…5.
- [contracts/output-format-selection.md](./contracts/output-format-selection.md)
  — CLI flag precedence and capability-degradation table; stream/exit parity.
- [contracts/rich-rendering-projection.md](./contracts/rich-rendering-projection.md)
  — new public `Rendering` module surface and behavioral contract C-1…5.
- [quickstart.md](./quickstart.md) — six runnable validation scenarios.

Agent context update: CLAUDE.md plan pointer updated to this plan; the
`speckit.agent-context.update` after_plan hook (optional) may refresh the managed
section.

## Phase 2 — Task planning approach (preview only)

`/speckit-tasks` will generate `tasks.md`. Expected shape, ordered by the
Constitution's spec→fsi→tests→impl discipline and the US priorities:

1. **Setup**: add `Spectre.Console` to `Directory.Packages.props`; add
   PackageReference + `Rendering.fs(i)` compile items to the CLI project; scaffold
   `FS.GG.SDD.Cli.Tests` and add it to the solution.
2. **Contract/signature (US1, US3)**: extend `OutputFormat` in `CommandTypes.fsi`
   (+`outputFormatValue`); author `Rendering.fsi`.
3. **Tests-first (US1, US2, US3 — [P] parallelizable)**: automation-invariance
   (JSON/text bytes unchanged, report object unchanged), projection-completeness,
   no-ANSI-on-degradation, stream/exit parity.
4. **Implementation (US1)**: `renderRichTo` rich projection over all report
   sections; (US2) `detectCapabilities` + `resolve` degradation; wire `Program.fs`
   format parsing and write path.
5. **Agent/doc alignment (US3/FR-010)**: update CLAUDE.md/AGENTS.md + Codex skill
   and any format docs; note the new format in release notes.
6. **Verification/evidence**: Release build, full suite, FSI public-surface
   transcript for the new module, disposable-directory CLI smoke for
   `--rich`/`--json`/`--text` incl. redirected + `NO_COLOR`, performance note,
   SDD/Governance boundary review, artifact traceability → `readiness/`.

## Complexity Tracking

No constitution violations require justification. Spectre.Console is an additive
runtime dependency on the CLI edge only; it reduces rather than adds complexity
versus hand-rolled ANSI, and does not touch the core library surface.
