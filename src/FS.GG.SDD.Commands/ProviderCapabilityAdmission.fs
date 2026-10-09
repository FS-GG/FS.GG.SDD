namespace FS.GG.SDD.Commands

open System
open FS.GG.Governance.Config
open FS.GG.Governance.Config.CapabilityBindings
open Fsgg.ProviderCatalog

module ProviderCapabilityAdmission =
    exception private Refused of Fsgg.ProviderCatalog.Diagnostic
    let private refuse path message =
        raise (Refused { Code = "catalog.admissionInvalid"; Path = path; Message = message })
    let private governed allowRoot path (raw: string) =
        if String.IsNullOrWhiteSpace raw || raw.Contains '\\' || raw.StartsWith("/", StringComparison.Ordinal)
           || (raw.Length > 1 && raw.[1] = ':') || (raw |> Seq.exists Char.IsControl) then
            refuse path "Expected a governed relative path."
        let parts = raw.Split('/')
        if parts |> Array.exists (fun p -> p = ".." || p = "") then refuse path "Traversal or empty path segments are not admitted."
        if not allowRoot && (parts |> Array.forall (fun p -> p = ".")) then refuse path "An output file path is required."
        Model.GovernedPath raw

    let request (policy: CatalogScaffoldPolicy.Policy) selectedPlatform (descriptor: Fsgg.ProviderCatalog.Descriptor) =
        try
            if obj.ReferenceEquals(policy, null) || obj.ReferenceEquals(descriptor, null) then refuse "$" "Original policy and descriptor are required."
            if descriptor.ContractVersion <> "3.0.0" then refuse "$.descriptor.contractVersion" "Only descriptor protocol 3.0.0 maps to the neutral admission contract."
            let environments = policy.EnvironmentBindings |> List.map (fun e -> e.Id, e.Environment)
            if environments.Length <> (environments |> List.map fst |> List.distinct).Length then refuse "$.policy.environments" "Conflicting environment identities."
            let formats = policy.EvidenceMappings |> List.map (fun f -> f.Format, { CapabilityBindings.EvidenceFormat.Id = f.Id; Version = f.Version })
            if formats.Length <> (formats |> List.map fst |> List.distinct).Length then refuse "$.policy.evidenceMappings" "Conflicting evidence mapping keys."
            let evidence =
                descriptor.Evidence |> List.map (fun e ->
                    let format = formats |> List.tryFind (fun (key, _) -> key = e.Format) |> Option.map snd
                    match format with
                    | None -> refuse ("$.descriptor.evidence." + e.Id + ".format") "Missing exact opaque evidence-format mapping."
                    | Some format -> { CapabilityBindings.EvidenceBinding.Id = e.Id; Format = format; OutputPath = governed false ("$.descriptor.evidence." + e.Id + ".path") e.Path })
            let bindings =
                descriptor.Capabilities |> List.map (fun c ->
                    let binding, limits =
                        match c.Binding with
                        | Fsgg.ProviderCatalog.SemanticOnly -> CapabilityBindings.SemanticOnly, None
                        | Fsgg.ProviderCatalog.Command(command, declared) ->
                            let cost =
                                match declared.CostClass with
                                | "cheap" -> Model.Cheap | "medium" -> Model.Medium | "high" -> Model.High | "exhaustive" -> Model.Exhaustive
                                | _ -> refuse ("$.descriptor.capabilities." + c.Id + ".cost") "Unknown cost class."
                            let admitted = declared.EnvironmentIds |> List.map (fun id ->
                                match environments |> List.tryFind (fun (key, _) -> key = id) with
                                | Some (_, environment) -> environment
                                | None -> refuse ("$.descriptor.capabilities." + c.Id + ".environments") "Environment is not independently mapped.")
                            if declared.TimeoutSeconds <= 0 then refuse ("$.descriptor.capabilities." + c.Id + ".timeout") "A positive timeout is required."
                            let limits: CapabilityBindings.CommandLimits =
                                { WorkingDirectory = Some(governed true ("$.descriptor.capabilities." + c.Id + ".workingDirectory") declared.WorkingDirectory)
                                  Timeout = Some(Model.TimeoutLimit declared.TimeoutSeconds)
                                  Cost = Some cost
                                  Environments = admitted }
                            CapabilityBindings.Command command, Some limits
                    { CapabilityBindings.CapabilityBinding.CapabilityId = c.Id; Required = c.Required; Platforms = c.Platforms
                      ToolIds = c.ToolIds; EvidenceIds = c.EvidenceIds; Binding = binding; Limits = limits })
            Ok { CapabilityBindings.ResolutionRequest.ContractVersion = "1.0.0"; SelectedPlatform = selectedPlatform
                 RequiredCapabilityIds = policy.RequiredCapabilityIds @ (descriptor.Capabilities |> List.filter _.Required |> List.map _.Id) |> List.distinct |> List.sort
                 SemanticOnlyObligationIds = policy.SemanticOnlyCapabilityIds; KnownPlatforms = policy.KnownPlatforms
                 SupportedEnvironments = policy.EnvironmentBindings |> List.map _.Environment |> List.distinct
                 SupportedEvidenceFormats = policy.SupportedEvidenceFormats
                 Tools = descriptor.Tools |> List.filter (fun t -> List.contains selectedPlatform t.Platforms) |> List.map (fun t -> { CapabilityBindings.ToolBinding.Id = t.Id; ExactVersion = t.Version })
                 Evidence = evidence; Bindings = bindings }
        with Refused diagnostic -> Error [diagnostic]

    let resolve policy selectedPlatform descriptor =
        request policy selectedPlatform descriptor
        |> Result.bind (fun original ->
            match CapabilityBindings.resolve original with
            | CapabilityBindings.Resolved resolved -> Ok resolved
            | CapabilityBindings.Rejected diagnostics ->
                diagnostics |> List.map (fun d ->
                    { Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.admission." + string d.Code
                      Path = "$.admission." + d.CapabilityId + "." + d.Field; Message = d.Message }) |> Error)
