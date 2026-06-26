# Implementation Plan: Extract a prerequisite combinator and shared handler shell

**Branch**: `024-prerequisite-combinator` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-prerequisite-combinator/spec.md`

## Summary

Remove the copy-pasted handler skeleton from `CommandWorkflow.fs` by extracting
two internal helpers:

1. A **prerequisite resolver** that runs the monotonic lifecycle cascade
   (specification → clarification → checklist → plan → tasks) **once**,
   short-circuiting when an upstream artifact is absent and threading each parsed
   fact forward, and returns a single `PrerequisiteResolution` record. The twelve
   handlers slice the prefix they need (field access) instead of each re-rolling a
   nested `match` 0–6 levels deep.
2. A **handler shell** (`runHandler`) that owns the missing-`WorkId` guard, the
   `DiagnosticsModule.sort`, the `hasBlocking` computation, and the
   "emit effects only when not blocking" gate. Each stage supplies only its
   artifact-build / view-render logic and a `hasBlocking -> CommandEffect list`
   write-effects thunk.

This is a Tier 2 internal refactor: the `CommandWorkflow.fsi` surface
(`init`/`update`) is unchanged, behavior is byte-for-byte preserved, and the
existing 438-test suite is the regression gate. Net handler LOC shrinks; no new
public surface, no new warnings.

## Technical Context

**Language/Version**: F# on .NET (`net10.0`)

**Primary Dependencies**: in-repo `FS.GG.SDD.Artifacts` (the
`*Facts` types — `SpecificationFacts`, `ClarificationFacts`, `ChecklistFacts`,
`PlanFacts`, `TaskFacts`), `DiagnosticsModule.sort`, `CommandTypes`
(`CommandModel`, `CommandEffect`, `Diagnostic`, `GeneratedView`). No new
dependencies.

**Storage**: N/A (pure planning functions over an in-memory `CommandModel`)

**Testing**: existing xUnit suite (438 tests, the post-R4 baseline) in the repo
test projects; the per-command handler suites (charter … refresh) are the
behavioral regression gate. No new tests are required (behavior-preserving; no
new arm — contrast R4's one totality assertion).

**Target Platform**: Linux (CI + dev); platform-agnostic library code.

**Project Type**: Single library (`FS.GG.SDD.Commands`) within the SDD CLI
product. The change is confined to `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Performance Goals**: N/A — the resolver computes the same cascade the handlers
already compute; no extra parsing, no hot path touched. (The resolver computes
the *maximal* prefix each calling handler needs, never more than the handler did.)

**Constraints**: No `CommandWorkflow.fsi` change (FR-007); deterministic JSON
output for downstream views MUST stay byte-identical (SC-004); FS0025 stays at 0
(R4, FR-010/SC-005); FS3261 (nullness) counts MUST NOT change except by pure
relocation (FR-010).

**Scale/Scope**: One file (`CommandWorkflow.fs`, 6,837 lines). Two helpers added;
twelve `compute*Plan` handlers rewritten to use them. The ~10+ duplicated
`hasBlocking` expressions collapse to one; the 5-/6-deep nested cascades in
`computeAnalyzePlan` / `computeEvidencePlan` collapse to record-field access.
No `.fsi`, no fixtures, no test-source changes beyond mechanical call-site updates.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 2 (internal change)** — implementation cleanup with no
user-visible or tool-visible contract change. Requires spec and tests; signatures
and baselines remain unchanged.

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ Pass | No public surface added. The resolver and shell are internal `let` bindings inside `module CommandWorkflow`, omitted from the 7-line `.fsi`. The two public entrypoints (`init`/`update`) are unchanged, so the FSI step is a no-op confirmation. Tests already exist and are the gate. |
| II. Structured artifacts are the contract | ✅ Pass | No artifact schema, layout, or generated-view contract changes. The same diagnostics, artifacts, and views are produced from the same inputs in the same order. |
| III. Visibility lives in `.fsi` | ✅ Pass | The new helpers are not re-exported by `CommandWorkflow.fsi`, so they are already private to the module — no top-level `private` modifier needed (Principle III). No surface-area baseline changes. |
| IV. Idiomatic simplicity is the default | ✅ Pass | Plain F#: one record type (`PrerequisiteResolution`), one resolver function, one higher-order `runHandler` taking ordinary function values. No custom operators, SRTP, reflection, type providers, CEs, or active patterns. The one generic parameter (the per-stage summaries payload) is ordinary value generalization. See Complexity Tracking — no entry needed. |
| V. Elmish/MVU boundary | ✅ Pass | The MVU boundary is **unchanged**: `init`/`update`/`CommandEffect` keep their shapes. The refactor reorganizes the pure `plan`-time computation *inside* `update`'s effect-free planning path; it adds no I/O and moves no effect interpretation. |
| VI. Test evidence is mandatory | ✅ Pass | Behavior is unchanged, so the existing 438-test suite must stay green unchanged (FR-009/SC-001). No genuinely new behavior is introduced (no new arm), so no new assertion is required or permitted beyond mechanical call-site updates. |
| VII. Agent and human workflows share one contract | ✅ Pass / N/A | No agent-facing artifact or skill contract changes. |
| VIII. Observability and safe failure | ✅ Pass | All existing diagnostics (ids, severities, sort order) are preserved exactly; the short-circuit semantics that decide which diagnostics surface are moved verbatim into the resolver. No diagnostic is added, dropped, or reordered. |

**Gate result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/024-prerequisite-combinator/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output — design decisions for the two helpers
├── data-model.md        # Phase 1 output — PrerequisiteResolution + shell signatures
├── quickstart.md        # Phase 1 output — how to validate the refactor
├── contracts/
│   ├── prerequisite-resolver.md   # Internal resolver contract (Phase 1 output)
│   └── run-handler.md             # Internal handler-shell contract (Phase 1 output)
├── checklists/
│   └── requirements.md  # (pre-existing, from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandWorkflow.fsi          # public surface (init/update) — UNCHANGED
└── CommandWorkflow.fs           # ALL changes confined here:
    ├── (new) PrerequisiteResolution record + resolvePrerequisites
    ├── (new) runHandler shell
    ├── computeCharterPlan … computeRefreshPlan  # rewritten to use both
    └── plan / init / update                     # dispatch unchanged in shape
```

**Structure Decision**: Single library, **no new files and no `.fsi` change**.
Both helpers are added as ordinary internal `let`/`type` bindings inside the
existing `module CommandWorkflow` in `CommandWorkflow.fs`, declared *before* the
first handler (`computeCharterPlan`, currently line 3714) and *after* the existing
prerequisite helpers (`specificationPrerequisite…` line 923 … `tasksPrerequisite…`
line 3068) they orchestrate. Because the module is `.fsi`-guarded and neither
helper is re-exported, they add zero public surface (Principle III). Each
handler's body is rewritten to (a) read its prerequisite prefix from
`resolvePrerequisites` and (b) wrap its tail in `runHandler`; the `plan` dispatch
that destructures each handler's return tuple (line 6701-6732) is updated only if
the handler's internal return arity changes (see research.md decision R-3).

## Complexity Tracking

> No Constitution Check violations. This section intentionally left empty.
