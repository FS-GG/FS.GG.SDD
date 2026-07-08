# Implementation Plan: Authored-Artifact Codec and Round-Trip Property

**Branch**: `item/201-sdd-epic-gap-a-authored-artifact-round-t` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/096-authored-artifact-codec/spec.md`

**Tracks**: FS.GG.SDD#201 (Gap A) · Umbrella: **ADR-0002** (`docs/decisions/0002-retire-defect-classes-via-structural-invariants.md`), invariant 1 · Closes #180, #181, #182

## Summary

Every authored YAML artifact (`evidence.yml`, `tasks.yml`) is **parsed** in
`FS.GG.SDD.Artifacts` and **rendered** in `FS.GG.SDD.Commands` by two
independent hand-maintained mirrors — a `tryScalarAt` reader and a
string-interpolation writer in a different assembly. Nothing makes the two field
sets agree, so they silently diverge: `sourceRefs[]` reads 6 fields and writes 4
(#181), absent optionals render as invented defaults (#182), and a bare-null
scalar reads as the string `"null"` and defeats the undisclosed-synthetic gate
(#180). Only 6 of ~123 scalar reads are null-aware.

This feature makes read/write asymmetry **structurally impossible** rather than
fixing today's instances one at a time. Each authored artifact gets a single
**field-list-driven codec**: one `fields` list per artifact, over which *both*
`parse` and `render` iterate, so a field cannot be read without being written or
written without being read. Optional scalars are read null-aware by default, so
a bare-null YAML token is `None` uniformly. An **FsCheck round-trip property**
`parse(render(m)) = m` per artifact turns any future asymmetry into a red test.

Three decisions carry the design (research R2–R4):

1. **A field-list codec, not a generic/reflective one.** One `fields` list of
   `FieldCodec` records per artifact; `parse` folds readers, `render` maps
   writers. Records + functions only — no SRTP, reflection, or type provider
   (Principle IV). A test enumerates the codec's field set against the record's
   labels so a new field with no codec entry fails the build (FR-007).
2. **Authored fields round-trip; tool-owned fields regenerate — the partition is
   explicit.** The property holds over the authored subset. Source snapshots and
   digests are tool-computed and excluded by construction, not by silent
   overwrite. `lifecycleNotes`, `sourceRefs[].{id,digest,relatedSourceId}`,
   `tasks.yml` `title`/`publicOrToolFacingImpact` move from "silently
   regenerated" to "authored, round-tripped" — that is the bug fix.
3. **Optional-scalar reads are null-aware by default**, collapsing the 6/123
   split. The existing `isPlainNullScalar` (`Internal.fs:85-93`) backs the
   codec's optional reader, so `null`/`Null`/`NULL`/`~`/empty → `None`
   everywhere, and the gate at both evidence and verify fires (FR-003/FR-004).

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`, `LangVersion=preview`)

**Primary Dependencies**: BCL + YamlDotNet (already referenced, via
`Internal.parseYaml`). **New test-only dependency: FsCheck** (xUnit
integration) — the repo has no property-based testing today (research R5).
No new `src/` dependency.

**Storage**: N/A. The change is to how `evidence.yml`/`tasks.yml` are read and
written; no new persisted artifact, no schema-version change (the on-disk YAML
shape is unchanged except that authored fields previously dropped are now
retained).

**Testing**: xUnit + FsCheck.
`tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`,
`TaskArtifactTests.fs` (parser + codec round-trip properties);
`tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`,
`TasksCommandTests.fs`, `VerifyCommandTests.fs` (re-render preservation, the
bare-null gate at both stages). Real filesystem fixtures per Principle VI.

**Target Platform**: CLI (`fsgg-sdd`), Linux/macOS/Windows

**Project Type**: CLI + libraries (Elmish/MVU command workflow)

**Performance Goals**: N/A. The codec is O(fields) per artifact, same order as
the hand-written path it replaces.

**Constraints**: Byte-idempotence preserved for unchanged authored files
(FR-008). No JSON-contract/exit-code/routing change except the #180 gate now
firing (FR-009). No provider literal enters `src/**` (unchanged; not relevant
here).

**Scale/Scope**: Two authored artifacts. The codec and its round-trip property
are the deliverable; the emitters in `HandlersEvidence.fs`/`TaskGraphAuthoring.fs`
and the readers in `Evidence.fs`/`Task.fs` are refactored onto it.

## Constitution Check

*GATE: passed before Phase 0; re-checked after Phase 1 design (below).*

| Principle | Status | Notes |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | PASS | The codec's public surface (`ArtifactCodec`) is declared in a new `.fsi` before its `.fs`; the round-trip properties are authored before the emitters are refactored onto the codec. |
| **II. Structured Artifacts Are the Machine Contract** | PASS | This feature *strengthens* the contract: the codec is the single authority for each authored artifact's field set. The round-trip property is the machine check that prose and structure agree. |
| **III. Visibility Lives in `.fsi`** | PASS | The codec module carries an `.fsi`; the per-artifact `fields` lists stay internal. `PublicSurface.baseline` updated if the Artifacts surface changes. |
| **IV. Idiomatic Simplicity** | PASS | Records + a list of field descriptors + `List.fold`/`List.map`. **No SRTP, reflection, type provider, or computation expression.** The reflective/generic-codec alternative is explicitly rejected in research R2. No justification section required. |
| **V. Elmish/MVU Is the Boundary** | PASS | Pure functions: `parse : string -> Result<Model,_>` and `render : Model -> string`. No IO; the existing `WriteFile`/`ReadFile` effects are unchanged. The codec sits in the pure core. |
| **VI. Test Evidence Is Mandatory** | PASS | Property tests (`parse(render(m)) = m`) + real-fixture command tests that fail before the change (a fully-populated `sourceRef` loses fields today) and pass after. FsCheck generators disclosed in test names. |
| **VII. Agent And Human Workflows Share One Contract** | PASS | No agent-surface change; the fix restores authored content the agent/human wrote. No skill change. |
| **VIII. Observability And Safe Failure** | PASS | The #180 gate bypass is closed — synthetic evidence with a bare-null disclosure now blocks (exit 1) at both evidence and verify, per Principle VIII's "synthetic evidence must not masquerade as real". |

**Change tier: Tier 1.** The #180 gate now fires (a behavior change), and a new
test dependency (FsCheck) is added. Requires spec, plan, tasks, `.fsi`, tests,
docs, and a **migration note**: a workspace currently carrying
`syntheticDisclosure: { standsInFor: null, reason: null }` was silently passing
the evidence gate and will now block — the correct outcome, disclosed in
`docs/release/migrations/`. The on-disk YAML shape is otherwise unchanged; no
`schemaVersion` bump.

**Complexity Tracking**: nothing to declare. No constitutional deviation is
requested. FsCheck is a test-only dependency; property-based testing is the
mechanism FR-005/FR-007 require and is justified in research R5.

## Project Structure

### Documentation (this feature)

```text
specs/096-authored-artifact-codec/
├── plan.md              # This file
├── research.md          # Phase 0 — R1–R7 verified findings
├── data-model.md        # Phase 1 — the codec type + authored/tool-owned field partition
├── quickstart.md        # Phase 1 — how to drive and verify
├── contracts/
│   └── artifact-codec.md # Phase 1 — the ArtifactCodec interface sketch (.fsi shape)
├── spec.md              # The specification (FR-001..009, SC-001..004)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
├── LifecycleArtifacts/
│   ├── ArtifactCodec.fs / .fsi   # NEW — FieldCodec, the fold/map primitives, optional null-aware readers
│   ├── Evidence.fs               # parser refactored onto the evidence `fields` list
│   ├── Task.fs                   # parser refactored onto the tasks `fields` list
│   └── Internal.fs               # isPlainNullScalar reused; no new reader class
└── (no schema-version change)

src/FS.GG.SDD.Commands/CommandWorkflow/
├── HandlersEvidence.fs           # renderEvidence* refactored onto the evidence codec (writes = codec)
├── TaskGraphAuthoring.fs         # tasks.yml emitter refactored onto the tasks codec
└── HandlersVerify.fs             # bare-null gate parity (reuses the codec's null-aware read)

tests/FS.GG.SDD.Artifacts.Tests/
├── EvidenceArtifactTests.fs      # round-trip property + fully-populated sourceRef fixture
├── TaskArtifactTests.fs          # round-trip property + title/impact preservation
└── ArtifactCodecTests.fs         # NEW — field-set-vs-record coupling test (FR-007)

tests/FS.GG.SDD.Commands.Tests/
├── EvidenceCommandTests.fs       # re-render preserves lifecycleNotes + sourceRef provenance
├── TasksCommandTests.fs          # re-run preserves title + publicOrToolFacingImpact:false
└── VerifyCommandTests.fs         # bare-null synthetic disclosure blocks at verify too
```

**Structure Decision**: The codec lives in `FS.GG.SDD.Artifacts`
(`LifecycleArtifacts/`), the assembly that already owns the parsers, so parse
and render can be defined over one `fields` list. The *renderers* currently in
`FS.GG.SDD.Commands` are refactored to call the codec's `render` — this is the
seam that removes the cross-assembly divergence at its root. No new project.

## Sequencing

**Implementation is `Blocked by` FS.GG.SDD#189.** `fsgg-coord overlap
FS.GG.SDD#201 FS.GG.SDD#189` reports **OVERLAP** on `HandlersEvidence.fs`,
`HandlersVerify.fs`, `TaskGraphAuthoring.fs`, and `tests/FS.GG.SDD.Artifacts.Tests`.
This authored slice (`specs/096/**`, `docs/decisions/0002`) is **DISJOINT** from
all in-flight items and proceeds now; the Phase 2+ source refactor rebases on
#189 once it merges (which also settles the `SourceIds`/`Requirements`/`Decisions`
union at the consumers — a related but separate invariant). Re-run `overlap`
before starting implementation (ADR-0021).

## Complexity Tracking

> No violations to justify. The one non-default choice — adding FsCheck — is a
> test dependency required by FR-005/FR-007 and introduces no `src/` complexity.
