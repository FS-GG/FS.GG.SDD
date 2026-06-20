# Data Model: Governance Readiness Handoff Contract

The handoff is a pure projection over the existing normalized `WorkModel` plus the
verify/ship readiness summaries. New public F# types live in
`FS.GG.SDD.Artifacts/GovernanceHandoff.fsi`. JSON shape and field rules are in
[contracts/governance-handoff.md](contracts/governance-handoff.md); the
field→consumer mapping is in
[contracts/integration-requirements.md](contracts/integration-requirements.md).

## Entities

### GovernanceHandoff (root)

| Field | Type | Source / Rule |
|---|---|---|
| `SchemaVersion` | `int` (=1) | constant; document shape version |
| `ContractVersion` | `string` (="1.0.0") | constant; cross-repo contract version |
| `GeneratorVersion` | `GeneratorVersion` | from generator metadata |
| `WorkId` | `string` | from `WorkModel.WorkId` |
| `Sources` | `SourceIdentity list` | `GenerationManifest.SourceIdentity` (path + digest + schema version) of work-model/verify/ship |
| `Evidence` | `EvidenceProjection` | from `WorkModel` evidence + task graph |
| `GovernedReferences` | `GovernedReference list` | from `WorkModel.GovernanceBoundaries` + `ArtifactChange` |
| `GovernanceConfig` | `GovernanceConfigPresence` | `.fsgg` file presence + pointers |
| `Readiness` | `ReadinessFacts` | from `ShipSummary` / `VerificationSummary` |
| `Diagnostics` | `Diagnostic list` | existing `WorkModel` diagnostics carried verbatim (prose/structured-conflict, evidence cycle) + projection-emitted `staleEvidence`; deterministic order by id |

### EvidenceProjection

| Field | Type | Rule |
|---|---|---|
| `Nodes` | `EvidenceNode list` | evidence entries + participating tasks; deterministic order by id |
| `Dependencies` | `EvidenceEdge list` | derived edges; deterministic order |

### EvidenceNode

| Field | Type | Rule |
|---|---|---|
| `Id` | `string` | namespaced: `evidence:<EvidenceId>` or `task:<TaskId>` |
| `State` | `DeclaredEvidenceState` | mapped per D2; one of `Pending`/`Real`/`Synthetic`/`Failed`/`Skipped` |
| `Rationale` | `string option` | `EvidenceEntry.Rationale` for synthetic/skipped |

`DeclaredEvidenceState` is a closed DU of the **five declared** states only
(no `AutoSynthetic` — computed-only on the consumer). Serialized tokens:
`pending`/`real`/`synthetic`/`failed`/`skipped` (identical to `Kernel.Json`).

### EvidenceEdge

| Field | Type | Rule |
|---|---|---|
| `Dependent` | `string` | node id that rests on `Dependency` |
| `Dependency` | `string` | node id depended upon |

Edge semantics match `Kernel.Evidence.build`: `(dependent, dependency)`.

### GovernedReference

| Field | Type | Source |
|---|---|---|
| `Path` | `string` | normalized repo-relative governed/changed path |
| `Owner` | `string` | `GovernanceBoundaryEntry.Owner` |
| `Relationship` | `string` | `GovernanceBoundaryEntry.Relationship` |
| `Kind` | `string option` | `ArtifactChange.Kind` |
| `Operation` | `string option` | `ArtifactChange.Operation` |

### GovernanceConfigPresence

| Field | Type | Rule |
|---|---|---|
| `PolicyPresent` / `PolicyPointer` | `bool` / `string option` | `.fsgg/policy.yml` |
| `CapabilitiesPresent` / `CapabilitiesPointer` | `bool` / `string option` | `.fsgg/capabilities.yml` |
| `ToolingPresent` / `ToolingPointer` | `bool` / `string option` | `.fsgg/tooling.yml` |

All-false with omitted pointers when absent (FR-011).

### ReadinessFacts

| Field | Type | Source |
|---|---|---|
| `ShipDisposition` | `string` | `ShipSummary.Disposition` |
| `VerificationReadiness` | `string` | `ShipSummary.VerificationReadiness` |
| `AdvisoryCount`/`WarningCount`/`BlockingCount` | `int` | `ShipSummary` counts |
| `BlockingDiagnosticIds` | `string list` | blocking finding/diagnostic ids |
| `PerViewState` | `(string * string) list` | `(view, currency)` for each contributing generated view (`work-model.json`, `verify.json`, `ship.json`), currency from the standard `GenerationManifest.SourceIdentity` digest comparison at production time; deterministic order by view name |

These are advisory facts, never a verdict (FR-008).

## Derivations (pure)

- `fromWorkModel : WorkModel -> ShipSummary -> VerificationSummary -> GovernanceConfigPresence -> GeneratorVersion -> GovernanceHandoff`
  — total fold; deterministic ordering of nodes/edges/references/diagnostics by id/path.
- `toJson : GovernanceHandoff -> string` — canonical, byte-stable serialization
  via the existing `Serialization` module.
- Evidence state mapping is a total function over SDD evidence results (D2 table),
  covered by tests (SC-004).

## Manifest integration

- `GenerationManifest.GeneratedViewKind` gains a `GovernanceHandoff` case.
- `expectedGovernanceHandoffOutputPath : workId:string -> string` →
  `readiness/<id>/governance-handoff.json`.
- Currency uses the standard `SourceIdentity` digest comparison; `refresh`
  regenerates the view (FR-012).

## Invariants (asserted by tests)

1. No node carries `AutoSynthetic`; no `"autoSynthetic"` token in output (SC-005).
2. No selected route, gate id, profile, severity, or pass/fail verdict appears
   (SC-005, boundary-exclusion).
3. Byte-identical output for identical source trees (SC-003).
4. Produced successfully with no `.fsgg` Governance files present (SC-002).
5. Authored sources byte-identical before/after production and refresh (SC-007).
6. Every edge endpoint is present in `Nodes` (so the consumer's `build` never
   returns `UnknownNode` for an SDD-produced handoff).
7. Existing `WorkModel` diagnostics are carried into `Diagnostics` verbatim — SDD
   surfaces the prose/structured-conflict and evidence-cycle diagnostics rather
   than resolving them (spec edge cases); SDD does not pre-reject a cycle.
