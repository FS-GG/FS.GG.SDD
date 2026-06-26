# Implementation Plan: Collapse Diagnostic Builder + Unify JSON Serializers

**Branch**: `027-unify-diagnostic-serializers` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/027-unify-diagnostic-serializers/spec.md`

## Summary

Roadmap item **R6** (refactor analysis §5.2 + §5.4). A behavior-preserving,
Tier 2 internal refactor in two ordered halves:

1. **Story 1 — one builder (§5.2, `CommandReports.fs`).** Collapse the ~113 named
   command-diagnostic constructors so the error-severity convention and the
   structurally-identical family shapes (`missing*`, `malformed*`, `duplicate*`,
   `unknown*`, `stale*`, `unsafe*`/`failed*`) live in one place, while the 14
   warning constructors keep warning severity. The ~99 hand-spelled
   `DiagnosticSeverity.DiagnosticError` literals collapse to the builder default.
2. **Story 2 — one set of writer primitives (§5.4).** Unify the JSON writer
   bodies duplicated across `CommandSerialization.fs` (Commands) and
   `Serialization.fs` (Artifacts) — `writeDiagnostic`, `writeOutputDigest`, and
   the `writeStringList` / digest / location variants — into one shared low-level
   writer module consumed by both serializers, with the two legitimate
   divergences (string-list ordering; `option` vs bare digest) parameterized.

The binding gate is **byte-identical** output: every command `--json` report and
the serialized work-model JSON match the pre-change baseline byte-for-byte, the
438-test suite stays green, and the public `.fsi` files and surface-area
baselines of the three named modules stay byte-stable. The change only alters
*how* a diagnostic is built and *where* a writer lives, never *what* is emitted.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution default).

**Primary Dependencies**: `System.Text.Json` (`Utf8JsonWriter`) for the writer
primitives; no new dependencies.

**Storage**: N/A (in-memory serialization to string; no persistence change).

**Testing**: xUnit. Existing suites: `FS.GG.SDD.Artifacts.Tests`,
`FS.GG.SDD.Commands.Tests`, `FS.GG.SDD.Cli.Tests`,
`FS.GG.SDD.Validation.Tests`. Surface guard: reflection-based
`SurfaceBaselineTests` comparing assembly public statics to a checked-in
`PublicSurface.baseline` per assembly. Determinism guard:
`ReleaseDeterminismTests` and per-command golden assertions.

**Target Platform**: Linux/dev CLI (`fsgg-sdd`).

**Project Type**: Single multi-project CLI solution
(`Artifacts` → `Commands` → `Cli`, one-way layering).

**Performance Goals**: N/A — refactor; no hot-path change. Net `src` line count
should *decrease* (≈90 LOC est.).

**Constraints**:
- Byte-identical `--json` and work-model JSON vs pre-change baseline (FR-006).
- Byte-stable public `.fsi` + surface-area baselines for `CommandReports`,
  `CommandSerialization`, `Serialization` (FR-007, SC-005).
- One-way `Artifacts → Commands` layering preserved; no new export on the three
  named entry-point `.fsi` files (FR-008).
- No new warning category in the Release build (R5 FS3261/FS0025 gate; SC-007).

**Scale/Scope**: 2 source files refactored in place (`CommandReports.fs` ~1,477
lines; `CommandSerialization.fs` 455 lines), 1 source file edited
(`Serialization.fs` 357 lines), and at most 1 new shared writer module +
`.fsi`. No spec/schema/command/artifact-layout change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Tests → Impl | ✅ | Spec exists; the `.fsi` surfaces of the three named modules are held **byte-identical** (no signature motion). Any new shared writer module gets its own `.fsi` sketched first (Phase 1 contract). Existing semantic + golden tests are the test layer; they must stay green and byte-identical. |
| II. Structured artifacts are the contract | ✅ | The JSON the writers emit *is* a machine contract; this change holds it byte-fixed. No new authoritative artifact. |
| III. Visibility lives in `.fsi` | ✅ | The shared writers are exposed only via a dedicated `.fsi` (not via top-level `internal`/`private` modifiers). The three named entry-point `.fsi` files are unchanged. See Phase 0 for the namespace decision that keeps the guarded baseline byte-identical. |
| IV. Idiomatic simplicity | ✅ | The change *removes* duplication; the builder and shared writers are plain functions with explicit parameters (ordering, digest-shape) — no new abstraction machinery, operators, or SRTP. |
| V. Elmish/MVU boundary | ✅ (N/A) | Pure functions only (diagnostic construction, JSON writing). No state/I-O boundary touched. |
| VI. Test evidence mandatory | ✅ | Behavior-preserving, so the evidence is *negative*: the existing golden/determinism/surface tests must stay green and byte-identical. A pre-change captured baseline is the fixture; new assertions are added only if a gap is found (Phase 1). |
| VII. Agent + human share one contract | ✅ (N/A) | No agent-command or generated-view change. |
| VIII. Observability + safe failure | ✅ | Diagnostic ids/messages/corrections are held fixed (FR-009); the collapse cannot silently drop or reword a diagnostic. |

**Change Tier**: Tier 2 (internal change). No public API, schema,
generated-view, command, artifact-layout, or agent-skill contract change.
Requires spec + tests; signatures and baselines remain unchanged. **PASS** — no
violations; Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/027-unify-diagnostic-serializers/
├── plan.md              # This file
├── research.md          # Phase 0 — mechanism + byte-identical risk decisions
├── data-model.md        # Phase 1 — builder + shared-writer "entities"
├── quickstart.md        # Phase 1 — byte-identical verification recipe
├── contracts/           # Phase 1 — internal contract sketches
│   ├── shared-json-writers.fsi.md   # shared writer module signature
│   └── diagnostic-builder.md        # builder convention + family helpers
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/
│   ├── Diagnostics.fs / .fsi          # unchanged (create stores relatedIds verbatim)
│   ├── Serialization.fs               # EDIT: writers consume shared module
│   ├── Serialization.fsi              # UNCHANGED (byte-stable entry points)
│   └── Json/ (new)                    # NEW shared low-level writer module + .fsi
│       ├── JsonWriters.fs             #   writeStringList', writeDigestObject,
│       └── JsonWriters.fsi            #   writeLocationObject, writeDiagnostic'
└── FS.GG.SDD.Commands/
    ├── CommandReports.fs              # EDIT: collapse onto builder + family helpers
    ├── CommandReports.fsi             # UNCHANGED (every named fn keeps its signature)
    ├── CommandSerialization.fs        # EDIT: writers consume shared module
    └── CommandSerialization.fsi       # UNCHANGED (byte-stable serializeReport)

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   ├── PublicSurface.baseline         # MUST stay byte-identical
│   └── SurfaceBaselineTests.fs        # reflects EXACT namespace "FS.GG.SDD.Artifacts"
├── FS.GG.SDD.Commands.Tests/
│   ├── PublicSurface.baseline         # MUST stay byte-identical
│   └── *CommandTests.fs / ReleaseDeterminismTests.fs  # byte-identical gate
└── FS.GG.SDD.Cli.Tests/ , FS.GG.SDD.Validation.Tests/ # full-matrix regression
```

**Structure Decision**: Single existing multi-project solution; the refactor is
in place. The only structural addition is one shared low-level writer module in
the **Artifacts** assembly (the lower layer both serializers can reach without
violating the one-way `Artifacts → Commands` dependency). Its namespace is
chosen in Phase 0 so the guarded `FS.GG.SDD.Artifacts` surface baseline stays
byte-identical.

## Complexity Tracking

> No constitution violations — section intentionally empty.
