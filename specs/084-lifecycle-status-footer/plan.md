# Implementation Plan: Lifecycle-Status Footer

**Branch**: `084-lifecycle-status-footer` | **Date**: 2026-07-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/084-lifecycle-status-footer/spec.md`

## Summary

Add a standardized lifecycle-status footer to every `fsgg-sdd` command. The footer shows the full charter→…→ship rail with each stage sensed done/current/next/pending/blocked from artifacts on disk, plus work id, "N of M" position, outcome, and next command; on a blocked/failed outcome it surfaces the blocking diagnostic's message + correction and the next-action command as the "explanation + options" — all from facts the report already carries.

Technical approach: introduce a new additive `LifecycleStatus` fact on `CommandReport`, populated in the one central `buildReport` assembly point by a **pure fold over `model.InterpretedEffects`** (each stage's artifact `ReadFile` effect whose `Snapshot = Some` ⟺ that stage is produced). File sensing is added as `ReadFile` **effects at the MVU edge** (Principle V), never ad-hoc `File.Exists` in pure code. The fact is serialized in `--json`, rendered as a deterministic plain footer in `--text`, and as a color-coded Spectre panel in `--rich` — which degrades to the text footer automatically because the rich path only runs when interactive+color-enabled. The failure explanation/options are a **projection** derived at render time from the existing `Diagnostics` + `NextAction`; they are **not** a new structured field (honors FR-017 / no second source of truth).

## Technical Context

**Language/Version**: F# on .NET, `net10.0`

**Primary Dependencies**: Spectre.Console (rich presentation edge only — `src/FS.GG.SDD.Cli/Rendering.fs`); `System.Text.Json` (report serialization, `CommandSerialization.fs`). No new package.

**Storage**: Read-only sensing of on-disk lifecycle artifacts under `work/<id>/…` and `readiness/<id>/…` (no writes; no schema changes to any persisted artifact).

**Testing**: xUnit — `tests/FS.GG.SDD.Commands.Tests` (sensing/derivation, text-footer golden, parity), `tests/FS.GG.SDD.Cli.Tests` (rich footer, degradation).

**Target Platform**: Cross-platform CLI (Linux/macOS/Windows).

**Project Type**: Single F# CLI project family (`FS.GG.SDD.*`).

**Performance Goals**: Negligible — at most 10 additional `ReadFile` effects per command (deduped against effects already emitted); derivation is an O(stages) pure fold. Deterministic for a given on-disk state.

**Constraints**: `--json` stays a byte-stable additive contract; `--rich` adds/drops no facts vs `--text`/`--json` and degrades to zero color/box control sequences; additive-only (no existing field removed/renamed).

**Scale/Scope**: 10 lifecycle stages; one work item in scope per invocation; every command surface (17 commands) gains the footer.

**Change Tier**: **Tier 1** (command output contract change). Requires spec, plan, tasks, `.fsi` updates, tests, docs (schema-reference + release-readiness), and versioning/migration notes.

**Versioning posture (resolves a spec/repo conflict — CONFIRM)**: The command-report contract is **AdditiveOptional** and `schemaVersion` is **Stable** (`docs/release/schema-reference.md`), with precedent (features 053 `doctor`/`upgrade`, 076 `lint`) adding fields **without** bumping `schemaVersion`. Therefore `lifecycleStatus` is added as an **additive optional field**; `schemaVersion` stays **1**; the field is recorded in the schema-reference field inventory and `release-readiness.json` `catalog[].inventory`; and the semantic **`reportVersion` is bumped `1.0.0` → `1.1.0`** to carry the "a version moves" traceability intent of FR-006 without violating the Stable-schemaVersion policy. This deviates from the literal wording of FR-006/SC-005 ("increment the schema version"); the spec's Clarifications section records the reconciliation. See `research.md` §1.

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-checked post-Phase 1. No violations.*

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS. Spec + clarifications complete. Plan adds `.fsi` for the new `LifecycleStatus` module and updates `CommandTypes.fsi`, `CommandSerialization.fsi`, `CommandRendering.fsi`, `Rendering.fsi` before `.fs`. Semantic tests through the public surface precede implementation. |
| **II. Structured Artifacts Are the Machine Contract** | PASS. `lifecycleStatus` (JSON) is authoritative; the text/rich footers are non-authoritative projections. Where prose "done" could disagree with data, the structured stage state wins; the footer never asserts a fact absent from the report. |
| **III. Visibility Lives in `.fsi`** | PASS. New module `CommandReports/LifecycleStatus.fsi`; surface baselines updated for every touched public module. |
| **IV. Idiomatic Simplicity** | PASS. Records + a small `StageState` DU + a pure fold. No custom operators, reflection, SRTP, or CE machinery. |
| **V. Elmish/MVU Is the Boundary for I/O** | PASS — load-bearing. Sensing is expressed as `ReadFile`/`EnumerateDirectory` `CommandEffect`s emitted in the pure plan step and interpreted at the edge; `buildReport` stays pure, folding `model.InterpretedEffects` (`Snapshot = Some` ⟺ artifact exists). No filesystem call is added to pure code. |
| **VI. Test Evidence Is Mandatory** | PASS. Real-filesystem fixtures for staged/partial/non-contiguous work items; a golden fixture for the deterministic `--text` footer (command output contract); parity + degradation tests; blocked-outcome tests. |
| **VII. Agent And Human Workflows Share One Contract** | PASS. The same `lifecycleStatus` fact drives json/text/rich; no second source of truth; failure options reuse existing `Diagnostics`/`NextAction`. |
| **VIII. Observability And Safe Failure** | PASS. Presence-only sensing never re-parses/validates stage artifacts, so a malformed artifact cannot crash the footer; missing artifacts degrade to `pending`; no work id in scope renders a coherent "no work" footer. |

**Gate result: PASS.** Complexity Tracking is empty (no violations to justify).

## Project Structure

### Documentation (this feature)

```text
specs/084-lifecycle-status-footer/
├── plan.md              # this file
├── research.md          # Phase 0 — decisions (versioning, sensing seam, derivation rules, colors, footer format)
├── data-model.md        # Phase 1 — LifecycleStatus / StageEntry / StageState
├── contracts/
│   └── lifecycle-status.md   # Phase 1 — json field schema + text footer format + rich description + failure-projection rule
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── checklists/
│   └── requirements.md  # from /speckit-specify (16/16)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fs / .fsi                     # + StageState, StageEntry, LifecycleStatus types; + CommandReport.LifecycleStatus field
├── CommandReports/
│   ├── LifecycleStatus.fs / .fsi              # NEW — pure derivation: stage paths → sensed states from InterpretedEffects
│   ├── ReportAssembly.fs                      # populate report.LifecycleStatus (one place); bump reportVersion → 1.1.0
│   └── NextActionRouting.fs                   # (unchanged; reused for next command + blocking diagnostic ids)
├── CommandWorkflow/
│   └── Foundation.fs                          # lifecycleSensingReadEffects helper; extend stage→artifact-path for analyze/verify/ship/work-model
├── CommandSerialization.fs / .fsi             # serialize "lifecycleStatus" object (additive)
└── CommandRendering.fs / .fsi                 # append deterministic text footer (final element)

src/FS.GG.SDD.Cli/
└── Rendering.fs / .fsi                        # append color-coded Spectre panel (final element); reuses degrade-to-text path

docs/release/
├── schema-reference.md                        # record lifecycleStatus in the command-report inventory; note reportVersion 1.1.0
└── release-readiness.json                     # catalog[].inventory += lifecycleStatus (lockstep)

tests/FS.GG.SDD.Commands.Tests/                # sensing/derivation, text-footer golden, json↔text parity, blocked-outcome
tests/FS.GG.SDD.Cli.Tests/                     # rich footer content, non-interactive degradation == text (zero ANSI)
```

**Structure Decision**: Single project family (existing `FS.GG.SDD.*`). The change is concentrated: one new pure module (`LifecycleStatus`), one additive record field populated in one place (`buildReport`), one sensing-effect helper at the MVU edge (`Foundation`), and three projection touch-points (serialize/text/rich). No new project, no new dependency.

## Complexity Tracking

No constitution violations — section intentionally empty.
