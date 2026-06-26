# Implementation Plan: Extract a shared JSON view-parser skeleton (total matches)

**Branch**: `023-extract-json-view-parser` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-extract-json-view-parser/spec.md`

## Summary

Collapse the four near-identical JSON-backed lifecycle view parsers
(`parseAnalysisView`, `parseVerificationView`, `parseShipView`,
`parseGeneratedAgentGuidance`) onto a single internal `parseJsonView` skeleton
that owns the shared `parse-document → classify-schema → match → identity →
error-arm` structure. Each parser keeps only its artifact-specific success body
(identity validation + record/field construction), passed to the skeleton as a
`build` callback. The skeleton's `version, status` match is made **total**,
which removes the four latent `MatchFailureException` sites (the `(None, Current)`
/ `(None, Deprecated)` combinations) and clears all four FS0025 incomplete-match
warnings in one move. This is a Tier 2 internal refactor: no public `.fsi`
changes, no behavior changes, byte-identical view output, and the existing
437-test suite is the regression gate.

## Technical Context

**Language/Version**: F# on .NET (`net10.0`)

**Primary Dependencies**: `System.Text.Json` (`JsonDocument`/`JsonElement`),
in-repo `SchemaVersion`, `Diagnostics`, `ArtifactRef`, `Identifiers` modules;
the R3-introduced `module internal Internal` shared-helper layer.

**Storage**: N/A (pure parsers over in-memory `FileSnapshot` text)

**Testing**: existing xUnit suite (437 tests) in the repo test projects; the
view-parser suites (analysis / verify / ship / agent-guidance) are the
behavioral regression gate. SC-005 may add one optional totality assertion.

**Target Platform**: Linux (CI + dev); platform-agnostic library code.

**Project Type**: Single library (`FS.GG.SDD.Artifacts`) within the SDD CLI
product. The change is confined to `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/`.

**Performance Goals**: N/A — parser cost is unchanged; no hot path touched.

**Constraints**: No public `.fsi` signature changes (FR-007); deterministic JSON
output for downstream views MUST stay byte-identical (SC-004); zero FS0025
warnings across `src` after the change (FR-005/SC-001); FS3261 (nullness) counts
MUST NOT change except by pure relocation (FR-009).

**Scale/Scope**: 4 parser bodies (~70 LOC of duplicated skeleton) collapse to one
shared function plus four small `build` callbacks. Files touched: `Internal.fs`
(skeleton added) and `Analysis.fs` / `Verify.fs` / `Ship.fs` / `Guidance.fs`
(bodies rewritten to call the skeleton). No `.fsi`, no fixtures, no test sources
beyond an optional new totality assertion.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 2 (internal change)** — implementation cleanup with no
user-visible or tool-visible contract change. Requires spec and tests;
signatures and baselines remain unchanged.

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ Pass | No public surface added. The skeleton lives in `module internal Internal` (no `.fsi`); the four public `.fsi` entrypoints are unchanged, so the FSI step is a no-op confirmation, not a new sketch. Tests already exist and are the gate. |
| II. Structured artifacts are the contract | ✅ Pass | No artifact schema, layout, or generated-view contract changes. The same JSON is parsed into the same records with identical ordering/diagnostics. |
| III. Visibility lives in `.fsi` | ✅ Pass | `parseJsonView` is added to the existing `module internal Internal` (AutoOpen, no signature file) — it is not public surface. The four parser `.fsi` files keep their exact `val` signatures (FR-007). No baseline changes. |
| IV. Idiomatic simplicity is the default | ✅ Pass | The skeleton is a plain parameterized function taking a `build` callback (a function value) plus two strings. No custom operators, SRTP, reflection, type providers, CEs, or active patterns. Generic over the view type via ordinary value generalization. |
| V. Elmish/MVU boundary | ✅ Pass / N/A | These are simple pure parsers, explicitly exempt from MVU ceremony ("Simple pure parsers, data models, and validators do not need MVU ceremony"). |
| VI. Test evidence is mandatory | ✅ Pass | Behavior is unchanged, so the existing 437-test suite must stay green unchanged (FR-008/SC-002). The one new defensible behavior — the previously-unreachable `(None, Current/Deprecated)` arm now returns a malformed-schema `Error` instead of throwing — is covered by an optional constructed-input totality assertion (SC-005). |
| VII. Agent and human workflows share one contract | ✅ Pass / N/A | No agent-facing artifact or skill contract changes. |
| VIII. Observability and safe failure | ✅ Pass | A latent runtime `MatchFailureException` (tool defect) is replaced by a defined malformed-schema-version diagnostic (malformed user input), strictly improving safe-failure behavior. All existing diagnostics are preserved. |

**Gate result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/023-extract-json-view-parser/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── parse-json-view.md   # Internal skeleton contract (Phase 1 output)
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
├── SchemaVersion.fs(i)          # classifyRaw, SchemaCompatibilityStatus (unchanged)
├── Diagnostics.fs(i)            # malformed/unsupported/future/workModelInconsistent (unchanged)
└── LifecycleArtifacts/
    ├── Internal.fs              # module internal Internal — ADD parseJsonView skeleton here
    ├── Analysis.fs              # parseAnalysisView body → calls parseJsonView (Analysis.fsi unchanged)
    ├── Verify.fs                # parseVerificationView body → calls parseJsonView (Verify.fsi unchanged)
    ├── Ship.fs                  # parseShipView body → calls parseJsonView (Ship.fsi unchanged)
    └── Guidance.fs              # parseGeneratedAgentGuidance body → calls parseJsonView (Guidance.fsi unchanged)
```

**Structure Decision**: Single library, no new files. The skeleton is added to
the existing R3 shared-helper module `Internal` in
`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`, which is `[<AutoOpen>]
module internal Internal`, has no `.fsi` (so it adds zero public surface), and is
already compiled (fsproj line 20) ahead of all four parsers (lines 39–48) and
after `SchemaVersion`/`Diagnostics`/`ArtifactRef` (lines 12–19). All four parsers
already `open` the namespace and auto-see `Internal`, so no `open`/fsproj edits
are needed. Each parser's `.fs` body is rewritten to delegate to the skeleton;
none of the four `.fsi` files change.

## Complexity Tracking

> No Constitution Check violations. This section intentionally left empty.
