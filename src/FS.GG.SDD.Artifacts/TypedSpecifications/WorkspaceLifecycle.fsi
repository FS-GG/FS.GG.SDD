namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Stable semantic roles that together form one accepted workspace authority.
[<RequireQualifiedAccess>]
type WorkspaceModuleKind =
    | ProductSpecification
    | Decision
    | WorkChange
    | RepositoryProfile
    | CiObligation
    | ExternalContract
    | EvidenceRequirement

/// One content-addressed module in the accepted workspace model.
type WorkspaceModule =
    { Id: SpecificationId
      Kind: WorkspaceModuleKind
      ContentSha256: string
      References: SpecificationId list
      Assumptions: string list
      EvidenceObligationIds: SpecificationId list }

/// The single revisioned semantic authority for one workspace.
type WorkspaceModel =
    { SchemaVersion: int
      Revision: int64
      Modules: WorkspaceModule list }

/// Authoring depth is a projection choice, not a distinct lifecycle.
[<RequireQualifiedAccess>]
type AuthoringDepth =
    | Freeform
    | StructuredSdd
    | DirectQuint

/// Explicit change operations retain rename and removal intent.
[<RequireQualifiedAccess>]
type WorkspaceChange =
    | Upsert of WorkspaceModule
    | Remove of SpecificationId
    | Rename of fromId: SpecificationId * toId: SpecificationId

/// Mandatory metadata for a deliberately human-accepted opaque change.
type OpaqueAcceptance =
    { Debt: string
      AffectedSubjects: SpecificationId list
      Reason: string
      ResponsibleHuman: string
      EvidenceRefs: string list }

/// Maintainer-approved semantic disposition vocabulary.
[<RequireQualifiedAccess>]
type ProposalDisposition =
    | CoherentDelta
    | NoSemanticChange of reason: string
    | AcceptedOpaque of OpaqueAcceptance
    | Ambiguous of reason: string
    | Contradictory of reason: string
    | Stale of reason: string

/// A proposal is bound to an issue, authored prose, and one exact accepted base.
type ChangeProposal =
    { SchemaVersion: int
      IssueRef: string
      ProseSha256: string
      BaseFingerprint: string
      AuthoringDepth: AuthoringDepth
      Changes: WorkspaceChange list
      Disposition: ProposalDisposition
      EvidenceFingerprint: string option }

/// Human authority required before an eligible proposal can reduce accepted state.
type HumanAcceptance =
    { AcceptedBy: string
      EvidenceRefs: string list
      AcceptedAtUtc: string }

/// A stable readable semantic change.
type WorkspaceSemanticChange =
    { Subject: SpecificationId
      Summary: string }

/// Deterministic reconciliation result; conflicts never contain a candidate model.
type WorkspaceReconciliation =
    | Reconciled of WorkspaceModel * WorkspaceSemanticChange list
    | Conflicted of SpecificationDiagnostic list

[<RequireQualifiedAccess>]
module WorkspaceLifecycle =
    /// Validate identities, hashes, references, assumptions, and evidence links.
    val validate: model: WorkspaceModel -> SpecificationDiagnostic list

    /// Encode semantic state in deterministic canonical JSON bytes.
    val canonicalBytes: model: WorkspaceModel -> Result<byte array, SpecificationDiagnostic list>

    /// Return lowercase SHA-256 over canonical semantic bytes.
    val fingerprint: model: WorkspaceModel -> Result<string, SpecificationDiagnostic list>

    /// Encode one valid workspace model as deterministic canonical JSON.
    val serializeModel: model: WorkspaceModel -> Result<string, SpecificationDiagnostic list>

    /// Decode the exact workspace-model v1 JSON contract; unknown and duplicate fields fail closed.
    val deserializeModel: text: string -> Result<WorkspaceModel, SpecificationDiagnostic list>

    /// Encode one valid exact-base proposal as deterministic canonical JSON.
    val serializeProposal:
        accepted: WorkspaceModel -> proposal: ChangeProposal -> Result<string, SpecificationDiagnostic list>

    /// Decode the exact change-proposal v1 JSON contract; unknown and duplicate fields fail closed.
    val deserializeProposal: text: string -> Result<ChangeProposal, SpecificationDiagnostic list>

    /// Return a stable readable semantic diff.
    val semanticDiff:
        before: WorkspaceModel ->
        after: WorkspaceModel ->
            Result<WorkspaceSemanticChange list, SpecificationDiagnostic list>

    /// Validate a proposal without changing accepted authority.
    val validateProposal: accepted: WorkspaceModel -> proposal: ChangeProposal -> SpecificationDiagnostic list

    /// Reconcile two exact-base proposals without changing accepted authority.
    val reconcile: accepted: WorkspaceModel -> left: ChangeProposal -> right: ChangeProposal -> WorkspaceReconciliation

    /// Apply one eligible exact-base proposal only with explicit human acceptance.
    val reduce:
        accepted: WorkspaceModel ->
        proposal: ChangeProposal ->
        acceptance: HumanAcceptance ->
            Result<WorkspaceModel, SpecificationDiagnostic list>

/// The kind of implementation evidence represented by one correspondence observation.
[<RequireQualifiedAccess>]
type CorrespondenceObservationKind =
    | GeneratedContract
    | SourceBinding
    | Test
    | EvidenceReceipt

/// A producer's explicit interpretation of one fingerprint-bound observation.
[<RequireQualifiedAccess>]
type CorrespondenceObservationState =
    | Observed
    | Missing
    | Contradicted of reason: string
    | Ambiguous of reason: string
    | Unsupported of reason: string

/// One immutable implementation observation offered to correspondence evaluation.
type CorrespondenceObservation =
    { ObligationId: SpecificationId
      Kind: CorrespondenceObservationKind
      AcceptedFingerprint: string
      SubjectFingerprint: string option
      State: CorrespondenceObservationState
      SourceBindings: string list
      TestBindings: string list
      EvidenceRefs: string list
      Explanation: string }

/// Closed, non-collapsing correspondence outcome vocabulary.
[<RequireQualifiedAccess>]
type CorrespondenceStatus =
    | Satisfied
    | Missing
    | Stale
    | Contradicted
    | Ambiguous
    | Unsupported
    | Unobserved

/// One accepted obligation and its complete implementation correspondence result.
type CorrespondenceEntry =
    { ObligationId: SpecificationId
      Status: CorrespondenceStatus
      SourceBindings: string list
      TestBindings: string list
      EvidenceRefs: string list
      Explanation: string }

/// Select either every obligation or the obligations impacted by changed subjects.
[<RequireQualifiedAccess>]
type CorrespondenceScope =
    | All
    | ImpactedBy of changedSubjectIds: string list

/// One deterministic report derived from accepted authority and immutable observations.
type CorrespondenceReport =
    { Schema: string
      AcceptedFingerprint: string
      ObservationFingerprint: string
      Scope: CorrespondenceScope
      Entries: CorrespondenceEntry list
      Diagnostics: SpecificationDiagnostic list }

[<RequireQualifiedAccess>]
module WorkspaceCorrespondence =
    /// Validate all inputs and derive correspondence without mutating accepted authority.
    val evaluate:
        accepted: WorkspaceModel ->
        contract: QuintCompiledContractV2 ->
        observations: CorrespondenceObservation list ->
        scope: CorrespondenceScope ->
            Result<CorrespondenceReport, SpecificationDiagnostic list>

    /// Decode exact correspondence-observation-set v1 JSON and reject unknown or duplicate fields.
    val deserializeObservations: text: string -> Result<CorrespondenceObservation list, SpecificationDiagnostic list>

    /// Encode the stable machine projection from the typed report.
    val serializeReport: report: CorrespondenceReport -> string

    /// Render a stable compact human projection from the typed report.
    val renderPlain: report: CorrespondenceReport -> string

    /// Render a navigable human projection without adding stored coverage authority.
    val renderRich: report: CorrespondenceReport -> string

/// Explicit legacy lifecycle identities; values never alias one another.
[<RequireQualifiedAccess>]
type LegacyLifecycle =
    | NoneLifecycle
    | Sdd
    | TypedSdd
    | SpecKit

/// Complete dry-run classification for one legacy source.
[<RequireQualifiedAccess>]
type LegacyMigrationClassification =
    | Migrated
    | Ambiguous of reason: string
    | Unsupported of reason: string
    | Preserved
    | Removed

/// One immutable source observation and its intended migration outcome.
type LegacySourceInventory =
    { Path: string
      OriginalSha256: string
      Lifecycle: LegacyLifecycle
      Classification: LegacyMigrationClassification
      TargetPath: string option }

/// Pure migration plan. Application is allowed only when ReadyToApply is true.
type WorkspaceMigrationPlan =
    { SchemaVersion: int
      TargetBackend: string
      Sources: LegacySourceInventory list
      RollbackManifestSha256: string
      Decisions: HumanAcceptance list
      ReadyToApply: bool }

[<RequireQualifiedAccess>]
module WorkspaceMigration =
    /// Build a deterministic dry-run plan; incomplete or unsafe inputs fail closed.
    val plan:
        targetBackend: string ->
        rollbackManifestSha256: string ->
        sources: LegacySourceInventory list ->
        decisions: HumanAcceptance list ->
            WorkspaceMigrationPlan * SpecificationDiagnostic list
