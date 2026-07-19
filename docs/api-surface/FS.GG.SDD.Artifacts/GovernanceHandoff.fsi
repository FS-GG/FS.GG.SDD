namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel

/// Pure projection of the normalized work model plus verify/ship readiness into the
/// versioned, optional `readiness/<id>/governance-handoff.json` view consumed by
/// FS.GG.Governance. SDD declares facts only: no effective/autoSynthetic taint, no
/// route/profile/gate selection, no freshness computation, no pass/fail verdict.
///
/// NOTE (layering): the cross-repo contract describes `fromWorkModel` as folding over
/// `ShipSummary`/`VerificationSummary`, but those types live in `FS.GG.SDD.Commands`,
/// which depends on this assembly. To keep the projection pure and avoid a circular
/// dependency, the readiness facts and `.fsgg` presence are pre-extracted by the
/// Commands layer into the Artifacts-native `ReadinessFacts` / `GovernanceConfigPresence`
/// passed in here. The projection stays a total fold; the boundary is unchanged.
module GovernanceHandoff =
    /// The five declared evidence states only. `AutoSynthetic` is computed-only on the
    /// consumer (`Kernel.Evidence.effective`) and is intentionally absent here.
    type DeclaredEvidenceState =
        | Pending
        | Real
        | Synthetic
        | Failed
        | Skipped

    type EvidenceNode =
        { Id: string
          State: DeclaredEvidenceState
          Rationale: string option }

    type EvidenceEdge =
        { Dependent: string
          Dependency: string }

    type EvidenceProjection =
        { Nodes: EvidenceNode list
          Dependencies: EvidenceEdge list }

    type GovernedReference =
        { Path: string
          Owner: string
          Relationship: string
          Kind: string option
          Operation: string option }

    type GovernanceConfigPresence =
        { PolicyPresent: bool
          PolicyPointer: string option
          CapabilitiesPresent: bool
          CapabilitiesPointer: string option
          ToolingPresent: bool
          ToolingPointer: string option }

    /// Merge-boundary readiness mirrored from `ShipSummary`/`VerificationSummary`.
    /// Advisory inputs to a Governance decision, never a verdict.
    type ReadinessFacts =
        {
            ShipDisposition: string
            VerificationReadiness: string
            AdvisoryCount: int
            WarningCount: int
            BlockingCount: int
            /// WI-4 (ADR-0048): classified `{gameplay}` FR obligations left unmet at the merge boundary —
            /// the aggregate a Governance gate binds to block-on-ship. `0` when no FR is classified.
            ClassifiedObligationsUnmet: int
            BlockingDiagnosticIds: string list
            PerViewState: (string * string) list
        }

    type GovernanceHandoff =
        { SchemaVersion: int
          ContractVersion: string
          GeneratorVersion: GeneratorVersion
          WorkId: string
          Sources: SourceIdentity list
          Evidence: EvidenceProjection
          GovernedReferences: GovernedReference list
          GovernanceConfig: GovernanceConfigPresence
          Readiness: ReadinessFacts
          Diagnostics: Diagnostic list }

    /// Serialized token for a declared evidence state (identical to `Kernel.Json`).
    val declaredEvidenceStateValue: state: DeclaredEvidenceState -> string

    /// Total mapping from an SDD `EvidenceEntry` result + synthetic flag to a declared
    /// evidence state (D2 table). Exposed for exhaustive mapping-table tests (SC-004).
    val mapEvidenceState: result: string -> synthetic: bool -> DeclaredEvidenceState

    /// True when the declared evidence result denotes Governance-owned staleness, which
    /// the projection surfaces as a `staleEvidence` diagnostic over the base state.
    val isStaleEvidenceResult: result: string -> bool

    /// `.fsgg`-absent baseline: all-false presence with omitted pointers (FR-011).
    val emptyGovernanceConfig: GovernanceConfigPresence

    /// Build a contributing-source identity from a path and its serialized text
    /// (sha256 digest, schemaVersion 1), for the handoff `sources[]` provenance list.
    val sourceIdentity: path: string -> text: string -> SourceIdentity

    /// Total, deterministic fold producing the handoff envelope. Nodes, edges,
    /// references, and diagnostics are ordered deterministically by id/path.
    val fromWorkModel:
        model: WorkModel ->
        sources: SourceIdentity list ->
        config: GovernanceConfigPresence ->
        readiness: ReadinessFacts ->
        generator: GeneratorVersion ->
            GovernanceHandoff

    /// Canonical, byte-stable JSON serialization (fixed key order; no clocks,
    /// durations, host paths, or ANSI styling).
    val toJson: handoff: GovernanceHandoff -> string
