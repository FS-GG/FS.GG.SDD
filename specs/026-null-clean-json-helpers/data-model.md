# Data Model: Null-Clean JSON Access + Warnings-as-Errors Gate

**Feature**: 026-null-clean-json-helpers | **Date**: 2026-06-26 | **Phase**: 1

This feature introduces **no runtime data entities** — it changes how existing code
handles nullability and tightens a build property. In place of a domain data model,
this document captures the two artifacts that *are* contracts here: the **warning
taxonomy** (what must reach zero) and the **null-boundary map** (where each
nullable originates and how it is resolved). Both are the inputs to `/speckit-tasks`.

## Warning taxonomy (target: all → 0)

| Category | Baseline unique sites | Target | Source shape | Resolution idiom |
|---|---|---|---|---|
| FS3261 (nullness) | 283 (275 src + 8 test) | 0 | nullable expression / `isNull` on non-nullable / incompatible nullability | `Option.ofObj`, `string \| null` param, null pattern-match (research D3) |
| FS0025 (incomplete match) | 0 (cleared by R4) | 0 (held) | n/a | gate prevents regression only |
| Any other category | 0 | n/a — not gated | n/a | scoped gate excludes them (research D1) |

State transition of the build: `warnings-ignored (false gate)` → *(Story 1: sites→0)* → `warnings-clean` → *(Story 2: add WarningsAsErrors)* → `warnings-gated (regressions fail build)`.

## Null-boundary map (where nulls enter, where they are resolved)

Ordered by site concentration; "resolve at" names the single edit point that clears
the cluster (and its downstream propagation where applicable).

| Cluster (file) | Sites | Nullable origin | Resolve at |
|---|---|---|---|
| `LifecycleArtifacts/Internal.fs` | 8 | `JsonElement.GetString()` | `jsonString`/`jsonStringList`/`parseJsonDigest` → `Option.ofObj` (also clears parser propagation) |
| `Analysis.fs` / `Verify.fs` / `Ship.fs` / `Guidance.fs` | 44/43/19/17 | values flowing from `Internal` helpers into non-nullable record fields | mostly cleared by the `Internal` fix; residual local pattern-matches in each `build` callback |
| `WorkModel.fs` | 53 | JSON access + string handling on the work-model build path | `Option.ofObj` / `String.IsNullOrEmpty`; may reuse `Internal` idioms (compiles after `Internal`) |
| `ReleaseContract.fs` | 25 | string + a few non-string (`string \| null`) | inline idiom per site (compiles after `Internal`) |
| `ValidationContracts.fs` | 14 | JSON/string in the Validation assembly | inline idiom; optional assembly-local `module internal` helper |
| `GenerationManifest.fs` | 10 | string/digest handling; **compiles before `Internal`** | fix in place (no shared helper reachable) |
| `SchemaVersion.fs` | 8 | string handling; **compiles before `Internal`** | fix in place |
| `Commands` (`Foundation`/`ParsingEarly`/`ParsingMid`/`ParsingTasks`/`HandlersShip`/`HandlersEvidence`/`HandlersAgents`) | ~17 | string/JSON in the Commands assembly | inline idiom per site |
| `ValidationRunner.fs` | 3 | string + `Process`/path | inline idiom |
| Artifacts long tail (`Core`/`Evidence`/`Task`/`Plan`/`Specification`/`Clarification`/`Checklist`/`RequirementModel`/`ArtifactRef`/`Identifiers`) | 1–4 each | string handling | inline idiom per site |
| test projects (`ValidateCommandTests`/`TestSupport`/`LifecycleSmokeTests`/`IsolationTests` + `Process \| null`/`DirectoryInfo \| null`) | 8 | BCL nullable returns in test setup | inline idiom per site |

## Invariants (must hold after the change)

- **INV-1 (behavior-preserving)**: every null→default substitution reproduces the
  prior value exactly. `Option.ofObj x |> Option.defaultValue ""` ≡ existing
  `if isNull x then "" else x`. Verified by the full test suite + byte-identical
  `--json` output.
- **INV-2 (zero count)**: `dotnet build -c Release --no-incremental` emits 0 FS3261
  and 0 FS0025 across all `src` and test projects.
- **INV-3 (no public surface motion)**: no `.fsi` file and no surface baseline
  changes; any new helper is `[<AutoOpen>] module internal` with no signature file.
- **INV-4 (scoped gate)**: only FS3261 and FS0025 are promoted to errors; no other
  category begins failing the build.
- **INV-5 (no silent suppression)**: any `#nowarn`/`[<SuppressMessage>]` introduced
  (expected: none) is explicitly enumerated here and justified, so the gate still
  fails on new, unsuppressed sites.

## Enumerated suppressions (FR-009)

_None — expected to remain empty. Every baseline message shape maps to an idiomatic
fix (research D3–D5). If implementation discovers an intractable BCL site, list it
here with file:line and justification before merge._
