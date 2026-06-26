# Implementation Plan: Null-Clean JSON Access + Warnings-as-Errors Gate

**Branch**: `026-null-clean-json-helpers` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-null-clean-json-helpers/spec.md` (roadmap item **R5**, §3 of `docs/reports/2026-06-26-074428-refactor-analysis.md`)

## Summary

Drive the FS3261 (nullness) warning count across the solution to **0**, then flip
on a scoped `WarningsAsErrors=FS3261;FS0025` gate in `Directory.Build.props` so the
clean state cannot silently re-accumulate. The cleanup is behavior-preserving:
every nullable value is resolved with an idiomatic F# nullness idiom
(`Option.ofObj`, a `string | null` parameter annotation, or explicit null
pattern-matching) at the **lowest shared boundary available in each assembly** —
above all the `System.Text.Json` `GetString()` boundary in the Artifacts
`Internal` helper layer, which feeds the four view-parser `build` callbacks. No
public `.fsi` signatures, surface baselines, schemas, generated views, or command
contracts change; the only contract-adjacent edit is the build property. The
existing test suite plus byte-identical `--json` output are the regression gates.

Measured baseline (clean Release build of this branch's merge base): **952** raw
FS3261 emissions = **283** unique sites (**275** in `src`, **8** in tests) across
four assemblies; **0** FS0025 (cleared by R4); **no other warning category is
emitted at all**, so the scoped gate promotes exactly the two intended categories
and SC-006 holds by construction.

## Technical Context

**Language/Version**: F# on .NET (`net10.0`), `<LangVersion>preview</LangVersion>`, `<Nullable>enable</Nullable>` (already set in `Directory.Build.props`).

**Primary Dependencies**: `System.Text.Json` (`JsonElement.GetString()` is the dominant nullable source), `YamlDotNet`, BCL string/path/process APIs that return `string | null`. In-repo: the R3/R4 `module internal Internal` JSON-access helpers (`jsonString`, `jsonRequiredString`, `jsonInt`, `jsonStringList`, `parseJsonDigest`, `parseJsonView`).

**Storage**: N/A (in-memory parsing over `FileSnapshot` text).

**Testing**: existing xUnit suites (438 tests across Artifacts/Commands/Cli/Validation test projects). The full suite is the behavioral gate; byte-identical `--json` output for charter/analyze/refresh is the determinism gate. One small assertion may be added proving the gate fails on an injected warning is **not** an automated unit test (it is a build-level check documented in `quickstart.md`), so no new runtime test is required, though one optional null-coalescing unit test may be added (SC-005 evidence).

**Target Platform**: Linux (CI + dev); platform-agnostic library + CLI code.

**Project Type**: Single .NET solution, four `src` assemblies (`FS.GG.SDD.Artifacts` → `…Commands` → `…Cli`/`…Validation`) plus test projects. Layering is one-way and unchanged.

**Performance Goals**: N/A — no runtime hot path is touched; null-coalescing is O(1) per field and already present as `if isNull` in many sites.

**Constraints**: No public `.fsi`/baseline change (FR-008, Tier 2); deterministic `--json` output byte-identical (FR-003/SC-004); gate scoped to FS3261+FS0025 only (FR-006/SC-006); test projects included (FR-007); any intractable site uses an explicit, enumerated suppression rather than an emitted warning (FR-009).

**Scale/Scope**: 283 unique sites in ~33 files across 4 `src` assemblies + 4 test projects. Concentrations: `WorkModel.fs` (53), `Analysis.fs` (44), `Verify.fs` (43), `ReleaseContract.fs` (25), `Ship.fs` (19), `Guidance.fs` (17), `ValidationContracts.fs` (14), `GenerationManifest.fs` (10), `SchemaVersion.fs`/`Internal.fs` (8 each); a long tail of 1–6 each. Plus one 1-line edit to `Directory.Build.props`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: **Tier 2 (internal change)** — implementation cleanup plus a build-configuration tightening, with no user-visible or tool-visible contract change. Requires spec and tests; signatures and baselines remain unchanged.

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Impl | ✅ Pass | No public surface added or changed. Null handling is internal; the FSI step is a no-op confirmation that no `.fsi` moves. Existing tests are the gate. |
| II. Structured artifacts are the contract | ✅ Pass | Same JSON parsed into the same records with identical ordering/diagnostics. Null→"" coalescing reproduces the existing `if isNull value then ""` behavior exactly. |
| III. Visibility lives in `.fsi` | ✅ Pass | All null-handling helpers/edits stay `internal` (or are inline idioms). No public module added, so no `.fsi` and no surface baseline changes. Any new helper lives in an `[<AutoOpen>] module internal` with no signature file. |
| IV. Idiomatic simplicity is the default | ✅ Pass | Uses built-in F# nullness idioms (`Option.ofObj`, `string | null` annotations, null pattern-matching). No custom operators, SRTP, reflection, CEs, or active patterns. Replaces ad-hoc `if isNull` with one idiom. |
| V. Elmish/MVU boundary | ✅ Pass / N/A | Touches pure parsers, data models, and serializers — explicitly exempt from MVU ceremony. The MVU `init`/`update` surface is untouched. |
| VI. Test evidence is mandatory | ✅ Pass | Behavior is unchanged ⇒ the full existing suite must stay green (FR-003/SC-004). The gate's effectiveness (SC-003) is proven by a documented build-level inject-and-fail check in `quickstart.md`; an optional null-coalescing unit test may add direct evidence. |
| VII. Agent and human workflows share one contract | ✅ Pass / N/A | No agent-facing artifact or skill contract changes. |
| VIII. Observability and safe failure | ✅ Pass | Strictly improves safe failure: the gate converts a silently-ignored class of latent correctness warnings into a fail-fast build error, and removes alert-blindness so a *new* warning is visible. No diagnostic behavior changes. |

**Gate result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/026-null-clean-json-helpers/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output — decisions D1–D7
├── data-model.md        # Phase 1 output — warning taxonomy + null-boundary map
├── quickstart.md        # Phase 1 output — build/verify/inject-defect validation guide
├── contracts/
│   └── warnings-gate.md # Build-config contract + null-handling convention
├── checklists/
│   └── requirements.md  # (pre-existing, from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
Directory.Build.props                       # ADD <WarningsAsErrors>FS3261;FS0025</WarningsAsErrors> (Story 2, last)

src/FS.GG.SDD.Artifacts/                     # 200+ sites — the bulk
├── SchemaVersion.fs            (8)          # compiles BEFORE Internal — fix in place, no shared helper
├── GenerationManifest.fs       (10)         # compiles BEFORE Internal — fix in place
├── LifecycleArtifacts/
│   ├── Internal.fs             (8)          # JSON-access boundary — centralize GetString() coalescing here
│   ├── Analysis.fs (44) / Verify.fs (43) / Ship.fs (19) / Guidance.fs (17)  # parser build callbacks
│   └── Core/Evidence/Task/Plan/Specification/Clarification/Checklist/RequirementModel (1 each)
├── WorkModel.fs                (53)         # compiles AFTER Internal — may reuse Internal idioms
├── ReleaseContract.fs          (25)         # incl. a few non-string (string|null) sites
├── ValidationContracts.fs? → see Validation
├── ArtifactRef.fs / Identifiers.fs (3–4)

src/FS.GG.SDD.Commands/                      # ~20 sites
├── CommandWorkflow/Foundation.fs (3) / ParsingEarly.fs (6) / ParsingMid.fs (2) / ParsingTasks.fs (1)
└── …/HandlersShip.fs (3) / HandlersEvidence.fs (1) / HandlersAgents.fs (1)

src/FS.GG.SDD.Validation/                    # ~17 sites
├── ValidationContracts.fs      (14)
└── ValidationRunner.fs         (3)

tests/ (4 projects)                          # 8 sites — included in cleanup + gate
├── ValidateCommandTests.fs (4) / TestSupport.fs (2) / LifecycleSmokeTests.fs (1)
└── IsolationTests.fs (1)  + a few non-string (Process|null, DirectoryInfo|null)
```

**Structure Decision**: Single solution; the cleanup edits existing files in place — **no new files, no compile-order changes, no new public modules**. The one structural lever is *where* each null is resolved:

1. **`Directory.Build.props`** gains exactly one property (`WarningsAsErrors`), inherited by every `src` and test project, so the gate is uniform. This edit lands **last** (after the count is 0) per the spec's sequencing.
2. **Artifacts JSON boundary** (`LifecycleArtifacts/Internal.fs`): coalesce `JsonElement.GetString()` to a clean `string option`/`string` once in `jsonString`/`jsonStringList`/`parseJsonDigest`; this clears the helper sites and the downstream "compatible nullability" propagation in the four parser `build` callbacks (`Analysis`/`Verify`/`Ship`/`Guidance`).
3. **Files that compile before `Internal`** (`SchemaVersion.fs`, `GenerationManifest.fs`) and **other assemblies** (`Commands`, `Validation`) cannot see the Artifacts `Internal` module, so they are fixed with the same inline idioms in place (no cross-assembly plumbing, no `InternalsVisibleTo`). Where one assembly has a dense cluster (`ValidationContracts.fs`), an `[<AutoOpen>] module internal` null helper local to that assembly is acceptable, but is not required if inline idioms are clearer.

## Complexity Tracking

> No Constitution Check violations. This section intentionally left empty.
