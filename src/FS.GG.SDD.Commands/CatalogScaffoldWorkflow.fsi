namespace FS.GG.SDD.Commands

/// Preparation confers no tool, package, mutation or lifecycle authority.
module CatalogScaffoldWorkflow =
    type CatalogSelection =
        {
            CatalogBytes: byte array
            ExpectedRawDigest: string
            Provider: string option
            Overrides: (string * string) list
        }

    type CatalogPreview =
        {
            Status: string
            Catalog: Fsgg.ProviderCatalog.Catalog
            Selected: Fsgg.ProviderCatalog.PreparedConfiguration option
        }

    val prepare: selection: CatalogSelection -> Result<CatalogPreview, Fsgg.ProviderCatalog.Diagnostic list>

    /// Explicit selected inputs; this request cannot grant process or mutation authority.
    type CatalogScaffoldRequest =
        { Selection: CatalogSelection
          TargetRoot: string
          TemplateArchive: string
          ExpectedArchiveDigest: string
          PolicyBytes: byte array
          ExpectedPolicyDigest: string
          SelectedPlatform: string
          PreflightTimeoutSeconds: int
          ScaffoldTimeoutSeconds: int
          DryRun: bool }

    type Phase = Preparing | Preflighting | Staging | Composing | Committing | Cleaning | Terminal
    type CommitOutcome = NotAttempted | Committed | Refused | Unknown
    type Outcome = Pending | Prepared | Succeeded | Failed | CleanupUnknown
    /// Durable state is interpreted only by the owning edge; pure state is not native evidence.
    type Model
    type Msg =
        | InputsVerified of CatalogPreview * CatalogScaffoldPolicy.Policy * FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.ArchiveIdentity
        | PreflightObserved of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation
        | TemplateObserved of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation
        | WorkspaceComposed of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord
        | CommitObserved of CommitOutcome
        | RefusalObserved of Fsgg.ProviderCatalog.Diagnostic list
        | CleanupObserved of childrenTerminal: bool * stagingRetired: bool
    type Effect = ReadAndVerifyInputs | ObservePreflight | StageTemplate | ComposeWorkspace | CommitWorkspace | RetireOwnedOperation
    val init: request: CatalogScaffoldRequest -> Model * Effect list
    val update: message: Msg -> model: Model -> Model * Effect list
    val phase: model: Model -> Phase
    val outcome: model: Model -> Outcome
    val diagnostics: model: Model -> Fsgg.ProviderCatalog.Diagnostic list
    val provenance: model: Model -> FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord option
    /// Selected inputs are data, never runtime admission evidence.
    val request: model: Model -> CatalogScaffoldRequest
    val verifiedInputs: model: Model -> (CatalogPreview * CatalogScaffoldPolicy.Policy * FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.ArchiveIdentity) option
    val preflightObservation: model: Model -> FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation option
    val templateObservation: model: Model -> FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation option
    val commitOutcome: model: Model -> CommitOutcome
