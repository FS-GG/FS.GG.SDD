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

    /// Return a stable readable semantic diff.
    val semanticDiff:
        before: WorkspaceModel -> after: WorkspaceModel -> Result<WorkspaceSemanticChange list, SpecificationDiagnostic list>

    /// Validate a proposal without changing accepted authority.
    val validateProposal: accepted: WorkspaceModel -> proposal: ChangeProposal -> SpecificationDiagnostic list

    /// Reconcile two exact-base proposals without changing accepted authority.
    val reconcile:
        accepted: WorkspaceModel -> left: ChangeProposal -> right: ChangeProposal -> WorkspaceReconciliation

    /// Apply one eligible exact-base proposal only with explicit human acceptance.
    val reduce:
        accepted: WorkspaceModel -> proposal: ChangeProposal -> acceptance: HumanAcceptance -> Result<WorkspaceModel, SpecificationDiagnostic list>

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
