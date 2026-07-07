# Implementation Plan: API Surface Drift Check

**Branch**: `086-api-surface-drift-check` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/086-api-surface-drift-check/spec.md`

## Summary

Add a cross-cutting `fsgg-sdd surface` command that enforces the FS-GG API-surface baseline convention in a scaffolded workspace: enumerate every authored `src/**/*.fsi`, map each to a mirrored baseline under `docs/api-surface/`, and compare byte-for-byte. `surface --check` (the default) is read-only and exits 1 on any missing/drifted baseline so it fails CI; `surface --update` refreshes missing/differing baselines (only rewriting on genuine change) and exits 0. Orphan baselines are surfaced as warnings (no delete). The command carries a new additive `Surface` fact on the command report, projected through the standard json/text/rich edge.

Technical approach: mirror the existing read-only `doctor` command exactly. Enumeration and file reads are expressed as `EnumerateDirectory`/`ReadFile` `CommandEffect`s emitted in the pure plan step and interpreted at the MVU edge (Principle V); the pure handler `computeSurfaceNext` folds the interpreted snapshots into the drift set, sets `model.Surface`, and — for `--update` — emits `WriteFile` effects for the changed baselines. `--check` emits no writes. The source-of-truth is the `.fsi` **text** (copy/diff), never assembly reflection. The `<Pkg>` segment is derived structurally from the src-relative path — no provider/package literal is embedded (generic-SDD purity). `nextLifecycleCommand Surface = None`; `Surface` is exempted from the `--work` requirement.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: `System.IO` via the existing `CommandEffect` interpreter edge (enumerate/read/write); Spectre.Console at the rich presentation edge only (`CommandRendering`/CLI); `System.Text.Json` (`CommandSerialization`). No new package.

**Storage**: Read-only enumeration + reads of `src/**/*.fsi` and `docs/api-surface/**/*.fsi` for `--check`; `--update` additionally writes baseline `.fsi` files under `docs/api-surface/`. No on-disk artifact-schema change.

**Testing**: xUnit — `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs` (drift detected, coherent, update writes, zero-write on check, json determinism, orphan warning), CLI projection tests in `tests/FS.GG.SDD.Cli.Tests` (text/rich parity + degradation).

**Target Platform**: Cross-platform CLI (Linux/macOS/Windows).

**Project Type**: Single F# CLI project family (`FS.GG.SDD.*`) — library + cli.

**Performance Goals**: O(number of `.fsi` files) enumerate + read + byte-compare; a few dozen files per workspace. Deterministic for a given on-disk state; `--check` performs zero writes.

**Constraints**: `--json` is a byte-stable deterministic contract (stable file + field ordering); `--rich` adds/drops no facts vs `--text`/`--json` and degrades to zero color/box control sequences; byte-for-byte comparison with no normalization; no provider/package-specific literal.

**Change Tier**: **Tier 1** (new command + command-output contract addition). Requires spec, plan, tasks, `.fsi` updates, tests, help, gate wiring, and versioning notes.

**Versioning posture**: The command-report contract is **AdditiveOptional** with a **Stable** `schemaVersion`. Adding the `surface` command and the additive `Surface` field on `CommandReport`/`CommandModel` follows the `doctor`/`upgrade`/`lint` precedent: `schemaVersion` stays `1`, the field is recorded in the schema-reference field inventory and `release-readiness.json`, and the semantic package/report version takes a minor bump. No new cross-repo handoff contract.

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-checked post-design. No violations.*

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS. Spec + clarifications complete. Plan adds `.fsi` (the `Surface` DU arm + `SurfaceSummary` type in `CommandTypes.fsi`, plus `CommandWorkflow.fsi`/`CommandSerialization.fsi`/`CommandRendering.fsi`/`CommandHelp.fsi` surface updates) before any `.fs` body; semantic tests through the public surface precede implementation. |
| **II. Structured Artifacts Are the Machine Contract** | PASS. The `--json` `surface` object is authoritative; the text/rich reports are non-authoritative projections carrying the same facts. |
| **III. Visibility Lives in `.fsi`** | PASS. Every touched public module's surface baseline is refreshed (`FSGG_UPDATE_BASELINE=1`). |
| **IV. Idiomatic Simplicity** | PASS. A `SurfaceSummary` record + a small status DU + a pure fold; enumerate/compare/copy. No reflection, no custom operators, no CE machinery. |
| **V. Elmish/MVU Is the Boundary for I/O** | PASS — load-bearing. Enumeration + reads are `CommandEffect`s emitted in the pure plan step and interpreted at the edge; `computeSurfaceNext` stays pure; `--update` writes are emitted as `WriteFile` effects, never ad-hoc `File.Copy` in pure code. `--check` emits zero write effects (FR-004). |
| **VI. Test Evidence Is Mandatory** | PASS. Real-filesystem fixtures for matched / missing / drifted / orphan; a zero-write assertion for `--check`; an idempotence assertion for `--update`; json determinism. |
| **VII. Agent And Human Workflows Share One Contract** | PASS. One `Surface` fact drives json/text/rich; exit code derives from the same drift diagnostics. |
| **VIII. Observability And Safe Failure** | PASS. A missing source root or zero `.fsi` files degrades to a coherent empty report (exit 0), not a crash; drift is a first-class `DiagnosticError`; orphans are honest warnings that never flip the exit code. |

**Gate result: PASS.** Complexity Tracking is empty (no violations to justify).

## Project Structure

### Documentation (this feature)

```text
specs/086-api-surface-drift-check/
├── spec.md              # feature spec (this feature)
├── plan.md              # this file
└── tasks.md             # phased task list
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fs / .fsi                     # + Surface arm on SddCommand; + SurfaceSummary record + SurfaceEntry;
│                                              #   + Surface field on CommandReport and CommandModel; --check/--update
│                                              #   request handling; arms in commandName/commandStage/parseCommand;
│                                              #   nextLifecycleCommand Surface = None
├── CommandWorkflow/
│   ├── HandlersSurface.fs                     # NEW (registered after HandlersDoctor) — pure computeSurfaceNext:
│   │                                          #   enumerate src/**/*.fsi, gate source+baseline reads, compute drift set,
│   │                                          #   emit WriteFile effects for --update, set model.Surface
│   └── Foundation.fs                          # `plan` arm (initial enumerate/read effects); exempt Surface in
│                                              #   workIdDiagnostics (no --work required — cross-cutting)
├── CommandWorkflow.fs / .fsi                  # dispatch arm | Surface, _ -> computeSurfaceNext
├── CommandReports/
│   └── ReportAssembly.fs                      # copy model.Surface → report.Surface; drift → exit 1 (DiagnosticError)
├── CommandSerialization.fs / .fsi             # writeSurface JSON block + call (deterministic ordering)
├── CommandRendering.fs / .fsi                 # text block for the surface summary (rich auto-derives from text)
└── CommandHelp.fs / .fsi                      # help entry + flags for `surface` (--check, --update)

src/FS.GG.SDD.Artifacts/
└── Diagnostics.fs                             # surface.drift (DiagnosticError → exit 1); surface.baselineMissing;
                                               #   surface.orphanBaseline (DiagnosticWarning)

tests/FS.GG.SDD.Commands.Tests/
└── SurfaceCommandTests.fs                     # NEW (mirror DoctorCommandTests): drift, coherent, update writes,
                                               #   zero-write on check, orphan warning, json determinism
tests/FS.GG.SDD.Cli.Tests/                     # text/rich projection parity + degradation

.github/workflows/gate.yml                     # add a `surface --check` step/job (model after build-config-drift)
CLAUDE.md / AGENTS.md                          # boundary docs + Claude/Codex command help kept in lockstep
```

**Structure Decision**: Single project family (existing `FS.GG.SDD.*`). The command mirrors the read-only `doctor` command: one new pure handler module (`HandlersSurface`), one additive report field populated in `ReportAssembly`, enumerate/read effects at the `Foundation` edge, three projection touch-points (serialize/text/rich), a help entry, and diagnostic constructors. No new project, no new dependency.

## Phased delivery

1. **Contract sketch (FSI-first)** — declare the `Surface` arm, `SurfaceSummary`/`SurfaceEntry`, and the report/model field in `.fsi` before any body.
2. **Foundational (blocking)** — types, the enumerate/read plan arm, the `--work` exemption, the pure `computeSurfaceNext` drift core, and `--json` serialization.
3. **US1 (P1, MVP)** — `--check` verdict + exit-1-on-drift + zero-write; drift diagnostics.
4. **US2 (P1)** — `--update` write effects (idempotent, changed-path report, exit 0).
5. **US3 (P2)** — text/rich parity + degradation, orphan warnings, `--param` root overrides.
6. **Polish** — help, gate wiring, surface baselines, docs, full-suite green.

## Risks

- **`--update` writing outside the baseline root.** Mitigation: baseline paths are derived by mirroring the src-relative path under the resolved baseline root; a guard test asserts every `WriteFile` effect targets a path under the baseline root only.
- **Non-determinism from filesystem enumeration order.** Mitigation: entries are sorted by src-relative path before folding/serializing (FR-010); a determinism test asserts byte-identical `--json` across runs.
- **Normalization creep.** Mitigation: comparison is explicit raw-byte equality with a test covering a CRLF-vs-LF-only difference reported as `drifted`.

## Complexity Tracking

No constitution violations — section intentionally empty.
