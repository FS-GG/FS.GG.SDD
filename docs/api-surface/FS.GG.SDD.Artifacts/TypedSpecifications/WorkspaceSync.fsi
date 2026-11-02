namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Why a sync plan checks all accepted obligations or a proven subset.
type WorkspaceSyncSelection =
    { Scope: CorrespondenceScope; Reason: string; ObligationIds: SpecificationId list }

[<RequireQualifiedAccess>]
module WorkspaceSync =
    /// Join explicitly promoted profile-2 workspace records to the existing accepted model.
    /// The caller supplies the revision from the accepted authority transaction.
    val deriveModel:
        revision: int64 -> contract: QuintCompiledContractV2 -> Result<WorkspaceModel, SpecificationDiagnostic list>

    /// Select a safe execution scope. Unknown source/dependency changes require all obligations.
    val select:
        previous: WorkspaceModel option ->
        accepted: WorkspaceModel ->
        contract: QuintCompiledContractV2 ->
        changedPaths: string list ->
            WorkspaceSyncSelection
