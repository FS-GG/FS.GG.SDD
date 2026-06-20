# Phase 1 Data Model: Generated-View Refresh

This document maps the spec's Key Entities to concrete F# contracts. Where an
existing contract already covers an entity it is reused; new types are introduced
only for the refresh aggregate and the `summary.md` projection. All public
additions land in `.fsi` files before `.fs` bodies.

## Reused contracts (no change)

| Spec entity | Existing contract | Module |
|---|---|---|
| Generated View | `GenerationManifest`, `SourceIdentity`, `GeneratedViewKind` (incl. `Summary`) | `FS.GG.SDD.Artifacts.GenerationManifest` |
| Generated-View State | `GeneratedViewState`, `GeneratedViewCurrency` (`Current\|Missing\|Stale\|Malformed\|Blocked`), `GeneratedViewSource` | `FS.GG.SDD.Commands.CommandTypes` |
| Generated-View Diagnostic | `Diagnostic` + `CommandReports` builders | `FS.GG.SDD.Artifacts.Diagnostics` / `CommandReports` |
| Optional Boundary Fact | `GovernanceCompatibilityFact` | `CommandTypes` |
| Refresh Report (envelope) | `CommandReport` (`ChangedArtifacts`, `GeneratedViews`, `Diagnostics`, `GovernanceCompatibility`, `NextAction`, `Outcome`) | `CommandTypes` |
| Refresh Request | `CommandRequest` (`Command = Refresh`, `WorkId`, `DryRun`, `OverwritePolicy`, `GeneratorVersion`) | `CommandTypes` |

Staleness is computed by `GenerationManifest.isStale` over `SourceIdentity`
source digests and schema status — by source/generator digest, not file
presence.

## New contracts

### `SddCommand.Refresh`

Add a `Refresh` case to the `SddCommand` union (`CommandTypes.fsi`). Updates:

- `commandName Refresh = "refresh"`
- `commandStage Refresh = "refresh"`
- `parseCommand "refresh" = Ok Refresh`
- `nextLifecycleCommand Refresh = None` (cross-cutting, like `Agents`)

### `RefreshDisposition`

```fsharp
type RefreshDisposition =
    | RefreshedCurrent      // all applicable views current after refresh
    | PartiallyBlocked      // some views refreshed/current, >=1 blocked
    | Blocked               // project/source/id invalid or no view refreshable
```

Serialized values: `refreshed-current`, `partially-blocked`, `blocked`
(`refreshDispositionValue` helper, mirroring `guidanceDispositionValue`).

Mapping (FR-014): invalid project context, empty/malformed/mismatched/duplicate
work id, or no refreshable view → `Blocked`; all applicable views `Current`
after refresh → `RefreshedCurrent`; otherwise `PartiallyBlocked`.

### `RefreshSummary`

Aggregate refresh facts, paralleling `ShipSummary` / `AgentGuidanceSummary`,
added to `CommandReport` as `Refresh: RefreshSummary option`.

```fsharp
type RefreshSummary =
    { WorkId: string
      Stage: string
      Status: string
      SummaryPath: string                       // readiness/<id>/summary.md
      RefreshedViewIds: string list             // views regenerated this run
      AlreadyCurrentViewIds: string list        // views found current, untouched
      BlockedViewIds: string list               // views that could not refresh
      NotApplicableViewIds: string list         // e.g. agent-commands w/ no targets
      PreservedAuthoredPaths: string list        // authored sources left unchanged
      FindingIds: string list
      AdvisoryCount: int
      WarningCount: int
      BlockingCount: int
      Disposition: string                        // refreshDispositionValue
      PerViewState: (string * string) list       // viewKind -> currency value
      SourceSnapshotCount: int
      Readiness: string }
```

`PerViewState` lists every SDD-owned view kind (`work-model`, `analysis`,
`verify`, `ship`, `agent-commands`, `summary`) with its `GeneratedViewCurrency`
value, so the report and `summary.md` present identical per-view state (SC-005,
SC-008). The detailed `GeneratedViewState` records remain in the report's
`GeneratedViews` list.

### Summary projection contract (`GenerationManifest` additions)

`GeneratedViewKind.Summary` already exists. Add helpers (in
`GenerationManifest.fsi`) to support the new projection:

- `expectedSummaryOutputPath: workId: string -> string` →
  `readiness/<id>/summary.md`
- `createSummaryManifest: viewPath -> generatorVersion -> sources -> outputDigest
  -> GenerationManifest` (sources are the structured readiness views the summary
  projects)

The rendered `summary.md` carries a generated-marker header (manifest fields:
view kind `summary`, schema version 1, generator, source paths + digests) and a
body projecting lifecycle state, per-view currency, diagnostics, outcome, and
next action. Rendering reads only structured readiness data (FR-006).

## New diagnostics (`CommandReports.fsi`)

Reuse where they fit: `outsideProject`, `missingProjectConfig`,
`malformedProjectConfig`, `malformedWorkId`, `duplicateWorkId`,
`unknownSourceReference`-style, `malformedGeneratedView`,
`blockedGeneratedViewRefresh`.

Add refresh-specific builders (stable ids, affected view + source/upstream,
severity, message, correction — FR-020):

| Builder | Purpose | FR |
|---|---|---|
| `refreshOutsideProject` / reuse `outsideProject` | not an initialized SDD project | FR-002, US-edge |
| `refreshMissingSource: viewPath -> sourcePath -> Diagnostic` | a view's declared source is missing | FR-009, FR-010 |
| `refreshMalformedSource: viewPath -> sourcePath -> message -> Diagnostic` | a view's source is malformed/schema-incompatible | FR-009 |
| `refreshStaleView: viewPath -> sourcePaths -> Diagnostic` | recorded digests/schema/generator no longer match sources | FR-008 |
| `refreshMalformedGeneratedView: viewPath -> message -> Diagnostic` | existing generated view unreadable/malformed | FR-009 |
| `refreshBlockedUpstreamView: viewPath -> upstreamViewPath -> Diagnostic` | dependent view blocked on an un-current upstream view | FR-011 |
| `refreshUnrenderableSummary: summaryPath -> relatedIds -> Diagnostic` | structured data for summary missing/stale/blocked | FR-005, US3-3 |

## State transitions (per view, within one refresh run)

```text
existing view + current sources, digests match      -> Current  (already-current, no write)
sources changed / digests mismatch                  -> Stale    -> regenerate -> Current (refreshed)
view file absent                                     -> Missing  -> regenerate -> Current (refreshed)
existing view file unreadable/malformed             -> Malformed-> regenerate from sources -> Current
source missing/malformed/stale                      -> Blocked  (no write; diagnostic)
upstream view not brought to Current                -> Blocked  (no write; names upstream)
agent-commands with no configured targets           -> NotApplicable (no write; no diagnostic)
```

Dependent views are evaluated only after their declared upstream view's terminal
state is known; an upstream `Blocked`/`Malformed` that cannot reach `Current`
forces dependents to `Blocked` (FR-010, FR-011). The summary is rendered last
from whatever structured state exists; if its required structured inputs are
blocked it is reported `Blocked` and not rendered from unusable data (US3-3).

## Validation rules

- Refresh requires an initialized project and exactly one valid `WorkId`
  (FR-002, FR-015).
- Empty/malformed work id, selected-id mismatch with the work model, and
  duplicated logical work ids block refresh (FR-015).
- A view is never fabricated or refreshed from a missing/malformed/blocked source
  (FR-010); the views that *can* refresh still do (US2-2).
- Authored lifecycle artifacts and `.fsgg/*.yml` are never created, updated,
  reordered, normalized, or removed (FR-012); they appear in
  `PreservedAuthoredPaths` with `Operation = Preserve/NoChange`.
- Outputs are deterministic for identical state and input (FR-018); text and
  `summary.md` are projections that add no facts (FR-006, FR-019).
