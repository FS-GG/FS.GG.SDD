# Implementation Plan: Surface Drift Classification (additive vs breaking)

**Branch**: `087-surface-drift-classification` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/087-surface-drift-classification/spec.md`

## Summary

Extend the feature-086 `fsgg-sdd surface` handler so that every **drifted** file (a committed
`docs/api-surface/<Pkg>/<Name>.fsi` baseline that exists and differs byte-for-byte from its
authored `src/<Pkg>/<Name>.fsi`) is also **classified** additive / breaking / cosmetic by
comparing the *member declarations* parsed from the two signature texts. The handler already reads
both texts in its body-read gate and pairs them in `computeSummary`; classification is a pure
function over those pairs — no new effect, no new file read, no persisted-artifact change. The
verdict rolls up to a run-level classification + a recommended coherent-set bump
(breaking→major, additive→minor, cosmetic→none), carried as an additive `Classification` fact on
`SurfaceSummary` and projected through json (deterministic) / text / rich (auto-derived from the
text lines). Classification is advisory: it emits **no** new diagnostic and changes **no** exit
code — a drifted tree still exits 1 under `--check` exactly as in feature 086.

## Technical Context

**Language/Version**: F# (.NET), same toolchain as the rest of FS.GG.SDD.

**Primary Dependencies**: None new. Reuses the MVU command workflow (`HandlersSurface`,
`Foundation` snapshot/enumerate helpers), `CommandSerialization` (Utf8JsonWriter), and
`CommandRendering` (plain-text projection; rich auto-derives). No Spectre-specific code needed
(rich scrapes the `key: value` text lines).

**Storage**: None. In-memory command report only; writes no new on-disk artifact (FR-014).

**Testing**: xUnit. New cases in `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs`
(classification behavior) and `tests/FS.GG.SDD.Cli.Tests/SurfaceProjectionTests.fs`
(projection parity + determinism). Pure member-extraction/classification gets focused unit tests.

**Target Platform**: CLI (`fsgg-sdd`), any scaffolded workspace.

**Project Type**: CLI / compiler-adjacent tooling (single solution).

**Performance Goals**: Negligible — classification runs only over the already-read drifted set
(typically a handful of `.fsi`), pure text tokenization.

**Constraints**: Deterministic default projection (byte-identical repeat runs); generic-SDD
purity (no provider/package literal; `<Pkg>` derived structurally); no exit-code change; no
schema change.

**Scale/Scope**: One handler extension + one new report sub-record + serializer/renderer lines +
tests. ~1 source file of new pure logic, additive edits to 4–5 existing files, plus their `.fsi`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation.** Honored: the ordering below updates the
  hand-authored `.fsi` signatures (`CommandTypes.fsi`, and any `Foundation`/serialization signature
  touched) and writes the failing tests *before* the implementation. PASS.
- **Authoritative-data / no-schema-drift.** The `.fsi` **text** remains the single source of truth
  for the surface (consistent with feature 086); classification derives from it and introduces no
  competing structured artifact. The new fact is an additive in-memory report field only — no
  persisted schema. PASS.
- **Json-is-contract / plain+rich-are-projections.** Classification is emitted once into the report;
  json is the deterministic contract, text is a projection, rich auto-derives from the text lines
  and must add/drop no fact and emit zero ANSI when redirected. PASS (covered by SC-005 tests).
- **Governance independence.** No Governance runtime dependency; the verdict is advisory and
  Governance/publishing consume it downstream (sibling issue #171). PASS.
- **Generic-SDD purity.** No provider/package literal; `<Pkg>` derived structurally as in 086. PASS.

No violations — no Complexity-Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/087-surface-drift-classification/
├── plan.md              # This file
├── research.md          # Phase 0 — member-extraction approach + decisions
├── data-model.md        # Phase 1 — SurfaceClassification / ClassifiedEntry shapes
├── quickstart.md        # Phase 1 — how to run and read the classification
├── checklists/
│   └── requirements.md   # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fsi                     # + SurfaceClassification / ClassifiedEntry types; + Classification field on SurfaceSummary
├── CommandTypes.fs                      # mirror the above (record defs)
├── CommandSerialization.fs              # writeSurface: emit the "classification" sub-object (deterministic)
├── CommandRendering.fs                  # renderText: emit surfaceClassification* key:value lines (rich auto-derives)
└── CommandWorkflow/
    ├── Foundation.fs                    # (maybe) the pure SurfaceClassify module: comment-strip, member tokenize, diff→verdict
    ├── Foundation.fsi                   # mirror any newly-public helper signatures
    └── HandlersSurface.fs               # computeSummary: classify the drifted pairs, attach Classification

tests/FS.GG.SDD.Commands.Tests/
└── SurfaceCommandTests.fs               # additive/breaking/cosmetic/scope/mixed/fallback cases

tests/FS.GG.SDD.Cli.Tests/
└── SurfaceProjectionTests.fs            # json/text/rich parity + json determinism for classification
```

No `contracts/` directory: this feature adds no new external/versioned contract — the change is a
report-internal additive fact. (The downstream registry/consumer reconcile is ADR-0025's `.github`
slice, out of scope here.)

## Phase 0 — Research (see research.md)

Key decisions resolved:

1. **Member extraction from `.fsi` text.** A pure, line-based tokenizer: strip `//`/`///` line
   comments and `(* … *)` block comments, drop blank lines, collapse internal whitespace, and treat
   each remaining significant line as a *member token*. Compare token **sets** between baseline and
   source so ordering and comments never register as change. This satisfies additive (only-added),
   breaking (a prior token gone), and cosmetic (equal token sets, differing bytes) without a full
   F# parser, and is deterministic. Rationale + alternatives in research.md.
2. **Where classification runs.** Inside `HandlersSurface.computeSummary`, over the existing
   `classified` pairs restricted to the `drifted` set (both texts `Some`, and unequal). No new
   effect/read — the gate already snapshotted both bodies.
3. **Conservative fallback (FR-011).** A drifted source whose text is non-empty but yields **zero**
   member tokens is treated as unparseable → `breaking` with `unparseableFallback = true`.
4. **Advisory, not gating (FR-008).** Classification emits no diagnostic; the exit code stays
   governed solely by feature-086 drift. Verified by exit-code assertions across all classes.

## Phase 1 — Design (see data-model.md, quickstart.md)

- **Report shape.** Add `SurfaceClassification` (`Verdict`, `RecommendedBump`, `Entries`) and
  `ClassifiedEntry` (`Path`, `Classification`, `RecommendedBump`, `AddedMembers`,
  `RemovedOrChangedMembers`, `UnparseableFallback`) records; add `Classification:
  SurfaceClassification` to `SurfaceSummary` (always present; empty entries + `none`/`none` when no
  drift). Strings for the enum-like fields keep serialization stable and mirror feature-086 style.
- **Pure core.** A `SurfaceClassify` module (in `Foundation` or a small dedicated file): `members:
  string -> Set<string>`, `classifyPair: baselineText -> sourceText -> ClassifiedEntry-parts`,
  `rollup: ClassifiedEntry list -> Verdict * Bump`. Severity order breaking ≻ additive ≻ cosmetic;
  bump map breaking→major / additive→minor / cosmetic→none / none→none.
- **Serialization.** Extend `writeSurface` with a nested `classification` object; entries sorted by
  path, member lists sorted — deterministic (FR-012).
- **Text + rich.** Add `surfaceClassificationVerdict`, `surfaceClassificationBump`,
  `surfaceClassified` (count) and per-entry `surfaceClassified: <path>=<class> (<bump>)` lines to
  `renderText`; rich auto-derives its table from these `key: value` lines (no bespoke rich block).
- **Re-check Constitution:** unchanged — all gates still PASS after design.

## Complexity Tracking

No entries — no constitution gate is being relaxed.
