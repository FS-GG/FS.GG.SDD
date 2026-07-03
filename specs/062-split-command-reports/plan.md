# Implementation Plan: Split CommandReports and type the defect/summary contracts

**Branch**: `062-split-command-reports` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/062-split-command-reports/spec.md`

## Summary

Make three failure-prone, stringly-typed report mechanisms compiler-enforced and
split the 1,524-line `CommandReports.fs` into cohesive units, holding the external
CLI contract (JSON bytes, exit codes, stream routing, text/rich projections)
invariant:

1. **Typed tool-defect bit** — add `IsToolDefect: bool` to the `Diagnostic` record,
   set by the seven defect-producing constructors via a `markToolDefect`
   combinator; `exitCodeForReport` reads the bit and the hand-maintained
   `providerDefectIds` string set is deleted.
2. **Typed staleness predicate** — replace `HandlersAgents`'s
   `diagnostic.Id.IndexOf("stale")` substring test with a single centralized
   `Diagnostics.signalsStaleView` predicate.
3. **Named per-stage summaries** — replace the positional 12-tuple threaded through
   `CommandWorkflow.nextLifecycleEffects` with a defaulted `StagePlan` record where
   each command arm sets only its own fields.
4. **Module split** — carve diagnostic construction, next-action/correction
   routing, and report+exit-code assembly into three units behind a thin
   `CommandReports` facade that keeps the module's public surface (and
   `PublicSurface.baseline`) byte-identical.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (repo default).

**Primary Dependencies**: `System.Text.Json` (`Utf8JsonWriter`) for the
deterministic JSON contract; Spectre.Console for `--rich` (untouched here).

**Storage**: N/A — no persisted-artifact schema changes. Provenance/work-model
schemas stay at their current versions.

**Testing**: xUnit across `tests/FS.GG.SDD.*.Tests` — golden/determinism suites,
`PublicSurface.baseline` surface tests, `DiagnosticTests`, `CommandWorkflowTests`,
plus `fsgg-sdd validate`.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single solution, multi-project (`Artifacts` → `Commands` →
`Cli` / `Validation`). This feature touches `FS.GG.SDD.Artifacts` (the `Diagnostic`
type) and `FS.GG.SDD.Commands` (reports, workflow, handlers).

**Performance Goals**: No change; refactor is behaviour-preserving.

**Constraints**: Default/`--json` and `--text` output MUST be byte-identical
before/after (FR-007/FR-008); exit codes and stdout/stderr routing identical
(FR-009); golden output baselines pass unmodified (FR-010).

**Scale/Scope**: ~90 diagnostic constructors, 1 record-field addition, 2 new
helper functions, 1 deleted string set, 1 substring test replaced, 1 tuple→record,
and a 4-file module reorg. Repo-local; no cross-repo contract impact.

## Change Classification

**Tier 1 (contracted change).** The `Diagnostic` public record gains a field and
the `Diagnostics` module gains two functions (`markToolDefect`,
`signalsStaleView`) — a deliberate, minimal public-type change, and the *only*
sanctioned surface delta (FR-011). Everything user-/tool-visible (output bytes,
exit codes, streams, persisted schemas, the `CommandReports` module surface) is
held invariant. `.fsi` files are updated first per Principle I/III.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS (planned). The new
  `Diagnostic.IsToolDefect`, `markToolDefect`, `signalsStaleView`, and the
  `StagePlan` record are sketched in `.fsi`/data-model before `.fs` bodies;
  semantic tests (typed-defect exit code, id-independent staleness, tuple→record
  equivalence) precede implementation.
- **II. Structured Artifacts Are the Machine Contract**: PASS. No structured
  artifact schema changes; the JSON writer is not touched, so serialized bytes are
  unchanged (the new record field is never written — verified by golden tests).
- **III. Visibility Lives in `.fsi`**: PASS. All new public members are added to
  `Diagnostics.fsi`; the split's internal modules expose only what the
  `CommandReports` facade needs, and `CommandReports.fsi` is unchanged.
- **IV. Idiomatic Simplicity**: PASS. Removes a hand-maintained set and a positional
  12-tuple; the facade re-export is mechanical and `.fsi`-checked.
- **V. Elmish/MVU boundary**: PASS. No effect/interpreter changes; exit-code and
  report assembly stay pure over the model.
- **VI. Test Evidence Is Mandatory**: PASS (planned). Real behavioural tests, no
  synthetic evidence; existing golden suites are the regression net.
- **VII. Agent & Human Workflows Share One Contract**: PASS. No command/skill
  surface change; Claude and Codex behaviour unchanged.
- **VIII. Observability & Safe Failure**: PASS. Exit-code semantics preserved
  exactly; the typed bit removes a silent-demotion failure mode (strictly safer).

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/062-split-command-reports/
├── plan.md              # This file
├── research.md          # Phase 0 — the four design decisions + the round-trip finding
├── data-model.md        # Phase 1 — Diagnostic.IsToolDefect, StagePlan, module layout
├── contracts/
│   └── report-contract.md   # Invariants held + the sanctioned new typed surface
├── quickstart.md        # Phase 1 — validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks (not created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
├── Diagnostics.fsi          # + IsToolDefect field; + markToolDefect; + signalsStaleView
├── Diagnostics.fs           # set field via markToolDefect on the 7 defect ctors; add predicate
└── WorkModel.fs             # parseEmbeddedDiagnostic literal gains IsToolDefect = false

src/FS.GG.SDD.Commands/
├── CommandReports.fsi       # UNCHANGED public surface (facade re-exports)
├── CommandReports.fs        # thin facade: re-export constructors + assembly
├── CommandReports/          # NEW subfolder (mirrors the CommandWorkflow/ pattern)
│   ├── DiagnosticConstructors.fsi/.fs   # the ~90 command diagnostic constructors
│   ├── NextActionRouting.fsi/.fs        # outcome + nextAction elif cascade
│   └── ReportAssembly.fsi/.fs           # buildReport, helpReport, exitCodeForReport (reads IsToolDefect)
├── CommandWorkflow.fs       # 12-tuple → StagePlan record in nextLifecycleEffects
├── CommandTypes.fs(i)       # + StagePlan record type + emptyStagePlan (or co-located in CommandWorkflow)
└── CommandWorkflow/
    └── HandlersAgents.fs    # IndexOf("stale") → Diagnostics.signalsStaleView
```

**Structure Decision**: Follow the existing `CommandWorkflow/` precedent — a
sibling `CommandReports/` subfolder of internal modules, with the original
`CommandReports.fs`/`.fsi` retained as the public facade so every `open
FS.GG.SDD.Commands.CommandReports` call site and the `PublicSurface.baseline`
remain untouched (FR-010/FR-011). Compile order in `FS.GG.SDD.Commands.fsproj`:
`DiagnosticConstructors` → `NextActionRouting` → `ReportAssembly` →
`CommandReports` (facade), inserted where `CommandReports.fs` sits today (before
`CommandHelp` and the `CommandWorkflow/` tree).

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Out of Scope

- The deeper hotspot splits noted in issue #72 (`computeRefreshPlan` ~480 lines,
  `computeVerifyPlan`/`computeShipPlan` and their 11-way Some-tuple matches) beyond
  the mechanical `StagePlan` substitution — a separate work item.
- The sibling `HandlersAgents` `Id.StartsWith("unknownReference")` prefix match —
  adjacent to FR-004 but not the `"stale"` decision path; left as-is.
- Any change to persisted artifact schemas or the JSON writer.
