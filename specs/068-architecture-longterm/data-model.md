# Phase 1 Data Model: Architecture longer-term cleanups (068)

This feature introduces **no persisted schema or public type**. Every construct
below is internal to `FS.GG.SDD.Commands` (namespace
`FS.GG.SDD.Commands.Internal`, no `.fsi` surface) or a test-only guard. Their
*serialized* projections reproduce today's exact strings.

## Introduced internal types

### `RefreshViewState` (new, internal — Decision 2)

Replaces the raw `string` view-currency values threaded through
`HandlersRefresh.fs`.

| DU case | Wire token (`toToken`) | Replaces string |
|---|---|---|
| `Blocked` | `"blocked"` | `"blocked"` |
| `Refreshed` | `"refreshed"` | `"refreshed"` |
| `Stale` | `"stale"` | `"stale"` |
| `AlreadyCurrent` | `"already-current"` | `"already-current"` |
| `Current` | `"current"` | `"current"` |
| `NotApplicable` | `"na"` | `"na"` |

- `toToken : RefreshViewState -> string` is the **single** projection point to the
  wire strings (FR-005). Exhaustive match; adding a case forces a token decision.
- Where a state flows to the existing `GeneratedViewCurrency` DU (e.g.
  `HandlersRefresh.fs:682`), map `RefreshViewState → GeneratedViewCurrency` at that
  boundary rather than duplicating the concept.

### `UpgradeStepOutcome` (new, internal — Decision 2)

Replaces the `string` `Drift.Step.Outcome` field and the upgrade outcome literals.

| DU case | Wire token (`toToken`) | Replaces string |
|---|---|---|
| `WouldApply` | `"wouldApply"` | `"wouldApply"` |
| `Applied` | `"applied"` | `"applied"` |
| `Failed` | `"failed"` | `"failed"` |
| `Skipped` | `"skipped"` | `"skipped"` |

- `Drift.Step.Outcome : UpgradeStepOutcome` (was `string`). `Drift.Step` is
  internal (`Drift` module has no `.fsi`); the change is invisible to any contract.
- `toToken : UpgradeStepOutcome -> string` is the single projection to the report
  wire strings (FR-005).

**Invariant (both DUs):** `toToken` is a total function whose outputs are exactly
today's literals. A golden/deterministic-fixture diff (FR-010) is the mechanical
proof that no byte changed.

## Introduced internal functions (Decision 1 — envelope)

### `writeReadinessEnvelope` (new, internal, `ViewGeneration.fs`)

```
writeReadinessEnvelope
  (workId: string)
  (viewKind: string)            // "analyze" | "verify" | "ship"
  (readiness: string)
  (generator: GeneratorVersion)
  (sourceKind: string -> string)
  (sources: GeneratedViewSource list)
  (writeBody: Utf8JsonWriter -> unit)
  : string                      // the finalized, UTF-8-decoded JSON document
```

Owns the invariant frame only: `MemoryStream`/`Utf8JsonWriter(Indented=true)`
lifecycle, opening object, `writeViewPreamble`, `writeSourcesArray`, then
`writeBody`, then terminal `WriteEndObject` + flush + decode. Each of the three
`*Json` functions becomes a thin caller supplying its ordered `writeBody`.

### `writeGovernanceReadinessTail` (new, internal, `ViewGeneration.fs`)

The identical verify+ship tail: `writeGeneratedViewsArray` →
`writeReadinessFindings` → `writeBoundaryFacts "governanceCompatibility"` →
`writeViewDiagnostics`. Called from the verify and ship bodies; analysis keeps its
own (different order + `"optionalBoundaryFacts"` key).

## Renamed modules (Decision 4 — names confirmed in implementation)

| Current file/module | Responsibility (to confirm by reading) | Candidate name |
|---|---|---|
| `ParsingEarly` | charter/spec/clarify/checklist front-matter + early artifacts | `SpecStageParsing` |
| `ParsingMid` | plan/analyze/evidence parsing | `PlanStageParsing` |
| `ParsingTasks` | task-graph parsing | `TaskGraphParsing` |

Rename is name-only; `.fsproj` compile order preserved.

## De-AutoOpen (Decision 3)

No type change. 15 modules lose `[<AutoOpen>]`; call sites gain explicit
`open <Module>` or qualified access. `Drift` and `SeededSkills` already model the
target pattern.

## Purity ledger (Decision 5)

| Site | Action | Observable? |
|---|---|---|
| `SeededSkills.seededSkills` (eager `let`, static-init `failwithf`) | make `lazy`/function; actionable missing-resource message; re-point drift guard | No (all resources embedded in every real build) |
| `Foundation.projectIdFromRoot` (ambient cwd on `"."`) | resolve root to absolute at the edge before the planner | No (real calls pass concrete roots; id output unchanged) |
| `RegistryDocument.load` (file IO in Artifacts) | **document as intentional edge** (comment + ledger); relocation deferred as Tier-1 | No |

## Test-only construct (Decision 6)

**CLAUDE↔AGENTS byte-identity guard** — a new fact asserting
`File.ReadAllText("CLAUDE.md") = File.ReadAllText("AGENTS.md")`, placed with the
existing repo doc-contract guards. `CLAUDE.md` = authored source; `AGENTS.md` =
mirrored copy, reconciled to identical content as part of this feature.
