namespace FS.GG.SDD.Commands

open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts

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
            Catalog: Catalog
            Selected: PreparedConfiguration option
        }

    let prepare (selection: CatalogSelection) =
        match ProviderCatalogIntegrity.verify selection.ExpectedRawDigest selection.CatalogBytes with
        | Error diagnostics -> Error diagnostics
        | Ok catalog ->
            match selection.Provider with
            | None when not selection.Overrides.IsEmpty ->
                Error
                    [
                        {
                            Code = "catalog.providerRequired"
                            Path = "$.provider"
                            Message = "Parameters require an explicit provider selection."
                        }
                    ]
            | None ->
                Ok
                    {
                        Status = "prepared"
                        Catalog = catalog
                        Selected = None
                    }
            | Some provider ->
                Fsgg.ProviderCatalog.resolve catalog provider selection.Overrides
                |> Result.map (fun selected ->
                    {
                        Status = "prepared"
                        Catalog = catalog
                        Selected = Some selected
                    })

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
    type Model = private { Request: CatalogScaffoldRequest; Phase: Phase; Outcome: Outcome; Diagnostics: Fsgg.ProviderCatalog.Diagnostic list; Inputs: (CatalogPreview * CatalogScaffoldPolicy.Policy * FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.ArchiveIdentity) option; Preflight: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation option; Template: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation option; Provenance: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord option; Commit: CommitOutcome }
    type Msg =
        | InputsVerified of CatalogPreview * CatalogScaffoldPolicy.Policy * FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.ArchiveIdentity
        | PreflightObserved of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation
        | TemplateObserved of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation
        | WorkspaceComposed of FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord
        | CommitObserved of CommitOutcome
        | RefusalObserved of Fsgg.ProviderCatalog.Diagnostic list
        | CleanupObserved of childrenTerminal: bool * stagingRetired: bool
    type Effect = ReadAndVerifyInputs | ObservePreflight | StageTemplate | ComposeWorkspace | CommitWorkspace | RetireOwnedOperation

    // Messages describe observations supplied by the owning edge; pure state is never runtime evidence.
    let private diagnostic code message : Fsgg.ProviderCatalog.Diagnostic =
        { Code = code; Path = "$.scaffold"; Message = message }
    // Preserve declaration bytes across both caller mutation and public observation.
    // Null declaration input is refused before copying; failed-state storage supplies
    // no verification or authority and uses an empty array rather than a nullable field.
    let private copyRequest (request: CatalogScaffoldRequest) =
        let copyBytes (bytes: byte array) = if obj.ReferenceEquals(bytes,null) then [||] else Array.copy bytes
        { request with
            Selection = { request.Selection with CatalogBytes = copyBytes request.Selection.CatalogBytes }
            PolicyBytes = copyBytes request.PolicyBytes }

    let init (request: CatalogScaffoldRequest) : Model * Effect list =
        let missingBytes = obj.ReferenceEquals(request.Selection.CatalogBytes,null) || obj.ReferenceEquals(request.PolicyBytes,null)
        let request = copyRequest request
        let invalid =
            missingBytes || request.PreflightTimeoutSeconds <= 0 || request.ScaffoldTimeoutSeconds <= 0 || request.Selection.Provider.IsNone
            || System.String.IsNullOrWhiteSpace request.TargetRoot || System.String.IsNullOrWhiteSpace request.TemplateArchive
        let model: Model =
            {
                Request = request
                Phase = (if invalid then Terminal else Preparing)
                Outcome = (if invalid then Failed else Pending)
                Diagnostics = (if invalid then [diagnostic "catalog.requestInvalid" "Explicit provider and positive separate budgets are required."] else [])
                Inputs = None
                Preflight = None
                Template = None
                Provenance = None
                Commit = NotAttempted
            }
        let effects: Effect list = (if invalid then [] else [ReadAndVerifyInputs])
        model, effects
    let update (message: Msg) (model: Model) : Model * Effect list =
        let refuse (diagnostics: Fsgg.ProviderCatalog.Diagnostic list) =
            let refused: Model = { model with Phase = Cleaning; Diagnostics = model.Diagnostics @ diagnostics; Outcome = Failed }
            let effects: Effect list = (if model.Phase = Cleaning then [] else [RetireOwnedOperation])
            refused, effects
        match model.Phase, message with
        | Terminal, _ -> model, []
        | _, RefusalObserved diagnostics -> refuse diagnostics
        | Preparing, InputsVerified(preview, policy, archive) ->
            if model.Request.PreflightTimeoutSeconds > policy.MaximumPreflightSeconds || model.Request.ScaffoldTimeoutSeconds > policy.MaximumScaffoldSeconds then
                refuse [diagnostic "catalog.budgetRefused" "Requested budgets exceed independently selected policy."]
            else
                match preview.Selected with
                | None -> refuse [diagnostic "catalog.providerRequired" "An explicit provider is required."]
                | Some selected ->
                    match ProviderCapabilityAdmission.resolve policy model.Request.SelectedPlatform selected.Descriptor with
                    | Error diagnostics -> refuse diagnostics
                    | Ok _ ->
                        if model.Request.DryRun then
                            { model with Inputs = Some(preview,policy,archive); Phase = Terminal; Outcome = Prepared }, []
                        else
                            { model with Inputs = Some(preview,policy,archive); Phase = Preflighting }, [ObservePreflight]
        | Preflighting, PreflightObserved observation ->
            let succeeded = observation.Result = "succeeded" && observation.Platform = model.Request.SelectedPlatform
                            && not observation.Invocations.IsEmpty
                            && (observation.Invocations |> List.forall (fun invocation -> invocation.ExitCode = 0))
            if succeeded then { model with Preflight = Some observation; Phase = Staging }, [StageTemplate]
            else refuse [diagnostic "catalog.preflightRefused" "Preflight did not observe success for the selected platform."]
        | Staging, TemplateObserved observation ->
            let succeeded = observation.Result = "succeeded" && observation.Platform = model.Request.SelectedPlatform
                            && not observation.Invocations.IsEmpty
                            && observation.ConsumedArchiveDigest = model.Request.ExpectedArchiveDigest
                            && (observation.Invocations |> List.forall (fun invocation -> invocation.ExitCode = 0))
            let matchesPreflight =
                match model.Preflight with
                | Some preflight ->
                    observation.Platform = preflight.Platform
                    && observation.Tools = preflight.Tools
                    && observation.Transport = preflight.Transport
                    && observation.ConsumedArchiveDigest = preflight.ConsumedArchiveDigest
                    && List.length observation.Invocations > List.length preflight.Invocations
                    && List.take (List.length preflight.Invocations) observation.Invocations = preflight.Invocations
                | None -> false

            if succeeded && matchesPreflight then { model with Template = Some observation; Phase = Composing }, [ComposeWorkspace]
            else refuse [diagnostic "catalog.templateRefused" "Template transport did not observe success for the selected archive and platform."]
        | Composing, WorkspaceComposed provenance ->
            let matchesObservedFacts =
                match model.Template with
                | None -> false
                | Some observed ->
                    // Composition adds final SDD/skill hashes and may compose the legacy
                    // root .gitignore, the sole source skill manifest and exact tool manifest. The edge verifies
                    // actual shared-amend/merge bytes; process/tool/platform/archive facts stay unchanged.
                    let projected = { provenance.Observation with ProducedPaths = observed.ProducedPaths }
                    projected = observed
                    && (observed.ProducedPaths
                        |> List.forall (fun path ->
                            provenance.Observation.ProducedPaths
                            |> List.exists (fun composed ->
                                composed.Path = path.Path
                                && composed.Owner = path.Owner
                                && (composed.Sha256 = path.Sha256
                                    || path.Path = ".gitignore"
                                    || path.Path = ".agents/skills/skill-manifest.json"
                                    || path.Path = ".config/dotnet-tools.json"))))

            match CatalogScaffoldProvenance.serialize provenance with
            | Error diagnostics -> refuse diagnostics
            | Ok _ when provenance.Declaration.RawCatalogDigest <> model.Request.Selection.ExpectedRawDigest
                        || provenance.Declaration.Archive.Digest <> model.Request.ExpectedArchiveDigest
                        || provenance.Declaration.Policy.Digest <> model.Request.ExpectedPolicyDigest ->
                refuse [diagnostic "catalog.provenanceMismatch" "Composed provenance differs from the selected raw input identities."]
            | Ok _ when not matchesObservedFacts ->
                refuse [diagnostic "catalog.observationMismatch" "Composed provenance differs from the observed template/preflight facts or immutable produced payload."]
            | Ok _ ->
                let matchesSelectedInputs =
                    match model.Inputs with
                    | Some(preview, policy, archive) ->
                        match preview.Selected with
                        | Some selected ->
                            let declaration = provenance.Declaration
                            declaration.CatalogId = preview.Catalog.Id
                            && declaration.CatalogRevision = preview.Catalog.Revision
                            && declaration.CatalogDigest = preview.Catalog.Digest
                            && declaration.Descriptor = selected.Descriptor
                            && declaration.EffectiveParameters = selected.EffectiveParameters
                            && declaration.RawProductName = selected.RawProductName
                            && declaration.PackageIdentity = selected.PackageIdentity
                            && declaration.CodeIdentifier = selected.CodeIdentifier
                            && declaration.Archive = archive
                            && declaration.Policy.Id = policy.Identity.Id
                            && declaration.Policy.Version = policy.Identity.Version
                            && declaration.Policy.Digest = policy.Identity.Digest
                            && declaration.EvidenceMap.Id = policy.EvidenceMapIdentity.Id
                            && declaration.EvidenceMap.Version = policy.EvidenceMapIdentity.Version
                            && declaration.EvidenceMap.Digest = policy.EvidenceMapIdentity.Digest
                            && declaration.Budgets.RequestedPreflightSeconds = model.Request.PreflightTimeoutSeconds
                            && declaration.Budgets.RequestedScaffoldSeconds = model.Request.ScaffoldTimeoutSeconds
                            && declaration.Budgets.AdmittedPreflightSeconds = model.Request.PreflightTimeoutSeconds
                            && declaration.Budgets.AdmittedScaffoldSeconds = model.Request.ScaffoldTimeoutSeconds
                        | None -> false
                    | None -> false

                if matchesSelectedInputs then
                    { model with Provenance = Some provenance; Phase = Committing }, [CommitWorkspace]
                else
                    refuse [diagnostic "catalog.provenanceMismatch" "Composed provenance differs from independently verified selection, policy, archive or budgets."]
        | Committing, CommitObserved commit -> { model with Commit = commit; Phase = Cleaning }, [RetireOwnedOperation]
        | Cleaning, CleanupObserved(childrenTerminal, stagingRetired) ->
            let outcome =
                if not childrenTerminal || not stagingRetired || model.Commit = Unknown then CleanupUnknown
                elif model.Outcome = Failed || not model.Diagnostics.IsEmpty then Failed
                elif model.Commit = Committed then Succeeded
                else Failed
            { model with Outcome = outcome; Phase = Terminal }, []
        | _ -> refuse [diagnostic "catalog.unexpectedObservation" "Observation does not match the current operation phase."]
    let phase (model: Model) = model.Phase
    let outcome (model: Model) = model.Outcome
    let diagnostics (model: Model) = model.Diagnostics
    let provenance (model: Model) = model.Provenance
    let request (model: Model) = copyRequest model.Request
    let verifiedInputs (model: Model) = model.Inputs
    let preflightObservation (model: Model) = model.Preflight
    let templateObservation (model: Model) = model.Template
    let commitOutcome (model: Model) = model.Commit
