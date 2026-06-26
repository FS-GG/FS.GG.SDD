# Phase 1 Data Model: Scaffold Runnable Products via Template Providers

Entities, schema versions, the outcome state machine, and the diagnostics catalog.
Structured artifacts are authoritative (constitution II). Where prose and structured data
could disagree, the rule is stated per entity.

## Entity overview

| Entity | Kind | Authoritative form | Schema | Owner |
|---|---|---|---|---|
| Provider Descriptor | selection/config | `.fsgg/providers.yml` entry (or `--provider`/`--param`) | v1 | author / provider |
| Provider Contract | agreement | [contracts/template-provider-contract.md](./contracts/template-provider-contract.md) | v1 | SDD (generic) |
| Scaffold Request | invocation input | in-memory (derived) → `dotnet new` args | v1 | SDD |
| Scaffold Result | invocation output | in-memory (derived from diff + exit) | v1 | SDD |
| Scaffold Provenance Record | persisted artifact | `.fsgg/scaffold-provenance.json` | v1 | SDD writes; marks files external |
| Scaffold Summary | report projection | `CommandReport.Scaffold` (JSON/text/rich) | report v | SDD |

## 1. Provider Descriptor

How a provider is named and resolved. Supplied by the author and/or provider, **never**
hardcoded in SDD (FR-002). Resolution precedence: explicit `--provider <name>` selects a
named entry in `.fsgg/providers.yml`; `--param k=v` (repeatable) overrides/augments that
entry's parameters.

Fields (per registry entry):

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | yes | the reference used by `--provider` |
| `contractVersion` | string (`"1.0.0"`) | yes | the SDD provider-contract version this provider implements; validated against SDD's supported range **before** invocation |
| `templateId` | string | yes | `dotnet new` short name / identity (e.g. provider-owned `fs-gg-ui`) — opaque to SDD |
| `source` | string | yes | how to acquire the template: a NuGet package id **or** a local path for `dotnet new install` — opaque to SDD |
| `parameters` | list of `{ key; required: bool; default?: string }` | no | declared params; missing required → `scaffold.providerParamMissing` |

Conflict rule: if a `--param` names a key not declared by the descriptor, it is passed
through (providers may accept undeclared params); if a declared **required** param has no
value from descriptor default or `--param`, scaffold blocks before invocation.

F# shape (in `Artifacts/LifecycleArtifacts/Config.fs(i)`):

```fsharp
type ProviderParameterSpec =
    { Key: string; Required: bool; Default: string option }

type ProviderDescriptor =
    { Name: string
      ContractVersion: string
      TemplateId: string
      Source: string
      Parameters: ProviderParameterSpec list }
```

## 2. Scaffold Request (derived, in-memory)

The resolved invocation input. Not persisted; flows from `plan` to the `RunProcess`
effect. Carries the generic contract inputs (FR-001): a target directory + named
parameters + the supported contract-version range SDD will accept.

```fsharp
type ScaffoldRequest =
    { TargetDirectory: string          // absolute, normalized
      Provider: ProviderDescriptor
      Parameters: Map<string, string>  // descriptor defaults overlaid with --param
      SupportedContractRange: string   // e.g. ">=1.0.0 <2.0.0"
      Force: bool }                     // --force opt-in for non-empty target
```

`dotnet new` projection: `dotnet new <TemplateId> -o <TargetDirectory>
-p:<key>=<value> …` plus `--force` iff `Force`. (The exact arg builder lives in
`HandlersScaffold.fs`; the contract doc defines the mapping abstractly so a non-`dotnet
new` provider can satisfy it.)

## 3. Scaffold Result (derived, in-memory)

Computed from the before/after directory diff and the process exit code.

```fsharp
type ScaffoldOutcome =
    | ProviderSucceeded          // exit 0, produced ≥ 1 path
    | ProviderSucceededEmpty     // exit 0, produced 0 paths
    | ProviderNotRun             // resolution/validation failed before invocation
    | ProviderFailed             // exit ≠ 0 or process error

type ScaffoldResult =
    { Outcome: ScaffoldOutcome
      ProducedPaths: string list          // sorted; relative to project root
      SddTreeIntrusions: string list      // produced paths under .fsgg/ work/ readiness/
      Collisions: string list             // pre-existing paths the provider would overwrite
      ProviderExitCode: int option
      DiagnosticIds: string list }
```

## 4. Scaffold Provenance Record (`.fsgg/scaffold-provenance.json`, schema v1)

Persisted, schema-versioned (constitution II), byte-deterministic. Records who produced
what and that ongoing ownership is **outside** SDD (FR-006). Consistent with how SDD
marks generated views, but with the inverted meaning: these paths are *externally owned*
and must **not** be regenerated.

```fsharp
type ScaffoldProvenanceRecord =
    { SchemaVersion: int                  // 1
      Generator: GeneratorVersion         // FS.GG.SDD.Artifacts/<version>
      ProviderName: string
      ProviderContractVersion: string
      TemplateRef: string                 // descriptor templateId (opaque echo)
      ProducedPaths: ScaffoldProducedPath list
      Outcome: string }                   // outcomeValue of ScaffoldOutcome

and ScaffoldProducedPath =
    { Path: string                        // relative, project-root anchored
      Owner: ArtifactOwner }              // always GeneratedProduct -> "generatedProduct"
```

JSON shape and key order: see
[contracts/scaffold-provenance.schema.md](./contracts/scaffold-provenance.schema.md).
`ProducedPaths` sorted by `Path`; no timestamps, no absolute paths.

Conflict rule (prose↔structured): the provenance JSON is authoritative for ownership and
refresh exclusion. If the file is malformed, `refresh`/`scaffold` emit
`scaffold.provenanceMalformed` and treat the file as absent (fail-safe: nothing is
silently regenerated), surfacing the diagnostic rather than guessing.

## 5. Scaffold Summary (report projection)

Added to `CommandReport` (and `CommandModel`) as `Scaffold: ScaffoldSummary option`,
serialized in all three projections (FR-012), fact-identical across them (SC-006).

```fsharp
type ScaffoldSummary =
    { ProviderName: string option         // None when providerMissing
      ProviderContractVersion: string option
      Outcome: string                     // outcomeValue ScaffoldOutcome
      SkeletonCreated: bool               // FR-009: was the SDD skeleton established?
      ProviderInvoked: bool               // distinguishes "not run" from "ran/failed"
      ProducedPathCount: int
      ProducedPaths: string list          // sorted; mirrored as ArtifactChange entries
      NextActionHint: string }            // info: "skeleton ready; begin lifecycle at charter"
```

Note: produced paths also appear in `CommandReport.ChangedArtifacts` with
`Ownership = "generatedProduct"` and an operation of `Create` (or `Refuse` on a refused
collision), keeping the report's artifact ledger complete. Provider stdout/stderr is
**not** a summary field (non-deterministic); it appears only in diagnostic messages.

## Outcome → CommandOutcome → exit code

Reuses the existing `CommandOutcome` and `exitCodeForReport` convention (errors →
Blocked; `toolDefect`-class → exit 2; user-input block → exit 1; clean → 0).

| Scenario | ScaffoldOutcome | CommandOutcome | Exit | Headline diagnostic |
|---|---|---|---|---|
| Happy path | ProviderSucceeded | Succeeded | 0 | — |
| Provider produced nothing | ProviderSucceededEmpty | SucceededWithWarnings | 0 | `scaffold.providerEmpty` (info) |
| No `--provider` | ProviderNotRun | Blocked | 1 | `scaffold.providerMissing` |
| Unknown provider name | ProviderNotRun | Blocked | 1 | `scaffold.providerUnknown` |
| Unsupported contract version | ProviderNotRun | Blocked | 1 | `scaffold.providerVersionUnsupported` |
| Missing required param | ProviderNotRun | Blocked | 1 | `scaffold.providerParamMissing` |
| Non-empty target, no `--force` | ProviderNotRun | Blocked | 1 | `scaffold.targetCollision` |
| Provider failed mid-run | ProviderFailed | Blocked | 2 | `scaffold.providerFailed` |
| `dotnet`/engine absent | ProviderFailed | Blocked | 2 | `scaffold.providerUnavailable` |
| Wrote into SDD tree | ProviderFailed | Blocked | 2 | `scaffold.providerWroteSddTree` |
| Malformed provenance (refresh/repeat) | n/a | Blocked | 1 | `scaffold.provenanceMalformed` |

In **every** Blocked case the summary reports `SkeletonCreated` and `ProviderInvoked`
truthfully, so an incomplete scaffold is never presented as complete (FR-009). When a
provider fails mid-run, partial `ProducedPaths` are still listed.

## Diagnostics catalog (new `scaffold.*` ids)

Classified per constitution VIII (malformed user input vs tool/provider defect). User-input
ids resolve at exit 1; provider-defect ids carry the `toolDefect`-style class → exit 2.

| Id | Severity | Class | Correction guidance |
|---|---|---|---|
| `scaffold.providerMissing` | error | user | "Pass `--provider <name>`; for skeleton-only use `fsgg-sdd init`." |
| `scaffold.providerUnknown` | error | user | "No provider named '<name>'. Register it in `.fsgg/providers.yml` or correct the name." |
| `scaffold.providerVersionUnsupported` | error | user/provider | "Provider declares contract <v>; supported range is <range>. Upgrade SDD or the provider." |
| `scaffold.providerParamMissing` | error | user | "Provider requires param '<key>'. Supply `--param <key>=<value>`." |
| `scaffold.targetCollision` | error | user | "Target not empty; <n> path(s) would be overwritten. Re-run with `--force` to proceed." (per-path listed) |
| `scaffold.providerEmpty` | info | provider | "Provider ran successfully but produced no files." |
| `scaffold.providerFailed` | error | provider | "Provider exited <code>. <captured stderr summary>. Inspect/fix the provider, then rerun." |
| `scaffold.providerUnavailable` | error | provider | "Could not run the provider (`dotnet`/template engine not found). Install the .NET SDK or the template." |
| `scaffold.providerWroteSddTree` | error | provider | "Provider wrote into SDD-owned tree(s): <paths>. Fix the provider; SDD state was not modified." (per-path listed) |
| `scaffold.provenanceMalformed` | error | user | "`.fsgg/scaffold-provenance.json` is unreadable. Repair or remove it before re-scaffolding/refreshing." |

## Refresh interaction (FR-007 / SC-007)

`HandlersRefresh.computeRefreshPlan` gains a provenance read: every `ProducedPath` in
`.fsgg/scaffold-provenance.json` is added to an **externally-owned exclusion set**.
Excluded paths are never classified `stale`/`missing`/`malformed` and never regenerated;
they do not appear in the refresh generated-view ledger. If provenance is absent, refresh
behaves exactly as today (additive, backward-compatible).

## Public surface impact (Tier 1, constitution III)

New public bindings declared in `.fsi` and added to baselines:
- `CommandTypes.fsi`: `SddCommand.Scaffold`; `CommandEffect.RunProcess`; `ScaffoldSummary`;
  `CommandReport.Scaffold`; updated `commandName`/`commandStage`/`parseCommand`/
  `nextLifecycleCommand` signatures (values, same types).
- `ArtifactRef`: no change (`GeneratedProduct` already public).
- `ScaffoldProvenance.fsi` (new module): record types + `serialize`/`tryParse`.
- `Config.fsi`: `ProviderDescriptor`, `ProviderParameterSpec`, registry parse function.
- `Diagnostics.fsi`: the ten `scaffold.*` factories.
- `CommandReports.fsi`/`CommandSerialization.fsi`/`CommandRendering`/`Cli/Rendering.fsi`:
  scaffold summary writers/renderers as needed.

All four `PublicSurface.baseline` files are updated as part of this change.
