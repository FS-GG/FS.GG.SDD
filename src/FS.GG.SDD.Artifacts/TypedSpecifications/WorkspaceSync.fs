namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Security.Cryptography
open System.Text

type WorkspaceSyncSelection =
    { Scope: CorrespondenceScope; Reason: string; ObligationIds: SpecificationId list }

[<RequireQualifiedAccess>]
module WorkspaceSync =
    let private diagnostic code path message : SpecificationDiagnostic =
        { Code = code; Path = path; Message = message; Location = None }

    let private kind = function
        | "product-specification" -> Some WorkspaceModuleKind.ProductSpecification
        | "decision" -> Some WorkspaceModuleKind.Decision
        | "work-change" -> Some WorkspaceModuleKind.WorkChange
        | "repository-profile" -> Some WorkspaceModuleKind.RepositoryProfile
        | "ci-obligation" -> Some WorkspaceModuleKind.CiObligation
        | "external-contract" -> Some WorkspaceModuleKind.ExternalContract
        | "evidence-requirement" -> Some WorkspaceModuleKind.EvidenceRequirement
        | _ -> None

    let private stringList path field fields =
        match List.tryFind (fst >> ((=) field)) fields with
        | Some(_, QuintList values) ->
            let parsed =
                values |> List.choose (function QuintString value -> Some value | _ -> None)
            if parsed.Length = values.Length then Ok parsed
            else Error [ diagnostic "SYNC-FIELD-TYPE" (path + "/" + field) "Expected a list of strings." ]
        | _ -> Error [ diagnostic "SYNC-FIELD-MISSING" (path + "/" + field) "A declared string list is required." ]

    let private ids path field fields =
        stringList path field fields
        |> Result.bind (fun values ->
            let parsed = values |> List.map SpecificationId.create
            let errors =
                List.zip values parsed
                |> List.choose (function
                    | value, Error reason -> Some(diagnostic "SYNC-ID-INVALID" (path + "/" + field) $"{value}: {reason}")
                    | _ -> None)
            if List.isEmpty errors then Ok(parsed |> List.choose (function Ok value -> Some value | _ -> None))
            else Error errors)

    let private moduleOf (canonical: string) (row: QuintModelCatalogueEntry) =
        let path = "/catalogue/" + row.Id
        match kind row.Kind with
        | None -> Error [ diagnostic "SYNC-KIND-UNKNOWN" (path + "/kind") $"Unsupported workspace kind '{row.Kind}'." ]
        | Some moduleKind ->
            match SpecificationId.create row.Id, row.Value with
            | Error reason, _ -> Error [ diagnostic "SYNC-ID-INVALID" (path + "/id") reason ]
            | _, QuintRecord fields ->
                let content =
                    match List.tryFind (fst >> ((=) "id")) fields, List.tryFind (fst >> ((=) "kind")) fields with
                    | Some(_, QuintString id), Some(_, QuintString role) when id = row.Id && role = row.Kind -> Ok()
                    | _ -> Error [ diagnostic "SYNC-ROW-MISMATCH" path "Promoted record id and kind must match its catalogue row." ]
                match content, ids path "references" fields, stringList path "assumptions" fields, ids path "evidenceObligationIds" fields with
                | Ok(), Ok references, Ok assumptions, Ok evidence ->
                    let id = SpecificationId.create row.Id |> Result.defaultWith failwith
                    let bytes = Encoding.UTF8.GetBytes(row.Id + "\n" + canonical)
                    let digest = SHA256.HashData bytes |> Convert.ToHexString |> _.ToLowerInvariant()
                    Ok { Id = id; Kind = moduleKind; ContentSha256 = digest; References = references; Assumptions = assumptions; EvidenceObligationIds = evidence }
                | _ ->
                    let errors =
                        [
                            match content with Error value -> yield! value | _ -> ()
                            match ids path "references" fields with Error value -> yield! value | _ -> ()
                            match stringList path "assumptions" fields with Error value -> yield! value | _ -> ()
                            match ids path "evidenceObligationIds" fields with Error value -> yield! value | _ -> ()
                        ]
                    Error errors
            | _, _ -> Error [ diagnostic "SYNC-ROW-TYPE" path "Workspace catalogue value must be an explicit record." ]

    let deriveModel revision contract =
        let contractFindings =
            QuintContractV2.validate contract
            |> List.map (fun item -> diagnostic item.Code item.Path item.Message)
        match QuintContractV2.serializeCanonical contract with
        | Error findings -> Error(contractFindings @ (findings |> List.map (fun item -> diagnostic item.Code item.Path item.Message)))
        | Ok canonical ->
            let rows = contract.Catalogue |> List.filter (fun item -> kind item.Kind |> Option.isSome)
            let unexpected =
                contract.Catalogue
                |> List.filter (fun item ->
                    kind item.Kind |> Option.isNone
                    && not (List.contains item.Kind [ "verifiedBy"; "verification"; "bound" ]))
            let parsed = rows |> List.map (moduleOf canonical)
            let findings =
                [
                    yield! contractFindings
                    if List.isEmpty rows then
                        diagnostic "SYNC-MODULES-MISSING" "/catalogue" "At least one explicit workspace module is required."
                    for item in unexpected do
                        diagnostic "SYNC-KIND-UNKNOWN" ("/catalogue/" + item.Id + "/kind") $"Unsupported catalogue kind '{item.Kind}'."
                    for item in parsed do
                        match item with Error errors -> yield! errors | _ -> ()
                ]
            if not (List.isEmpty findings) then Error findings
            else
                let model =
                    { SchemaVersion = 1; Revision = revision; Modules = parsed |> List.choose (function Ok value -> Some value | _ -> None) }
                let assumptions =
                    model.Modules
                    |> List.collect (fun item ->
                        item.Assumptions
                        |> List.map (fun value -> SpecificationId.value item.Id, value))
                let assumptionFindings =
                    [
                        if not (model.Modules |> List.exists (fun item -> item.Kind = WorkspaceModuleKind.EvidenceRequirement)) then
                            diagnostic "SYNC-OBLIGATION-MISSING" "/modules" "At least one declared evidence obligation is required."
                        for moduleId, value in assumptions do
                            if not (value.Contains('=') && value.IndexOf('=') > 0 && value.IndexOf('=') < value.Length - 1) then
                                diagnostic "SYNC-ASSUMPTION-FORM" ("/modules/" + moduleId + "/assumptions") "Assumptions must declare name=value."
                        for key, values in
                            assumptions
                            |> List.choose (fun (_, value) ->
                                let separator = value.IndexOf('=')
                                if separator > 0 && separator < value.Length - 1 then
                                    Some(value.Substring(0, separator), value.Substring(separator + 1))
                                else None)
                            |> List.groupBy fst do
                            if (values |> List.map snd |> List.distinct |> List.length) > 1 then
                                diagnostic "SYNC-ASSUMPTION-CONFLICT" "/modules" $"Modules disagree on assumption '{key}'."
                    ]
                match WorkspaceLifecycle.validate model @ assumptionFindings with
                | [] -> Ok model
                | errors -> Error errors

    let select (previous: WorkspaceModel option) (accepted: WorkspaceModel) (contract: QuintCompiledContractV2) (changedPaths: string list) =
        let all =
            accepted.Modules
            |> List.filter (fun item -> item.Kind = WorkspaceModuleKind.EvidenceRequirement)
            |> List.map _.Id
            |> List.sortBy SpecificationId.value
        let knownIds = accepted.Modules |> List.map (fun item -> SpecificationId.value item.Id) |> Set.ofList
        let unresolvedRelationship =
            contract.Relationships
            |> List.exists (fun item -> not (Set.contains item.FromId knownIds && Set.contains item.ToId knownIds))
        let reason =
            if previous.IsNone then "No accepted baseline; run every obligation."
            elif not (List.isEmpty changedPaths) then "Source, test or build inputs changed; dependency closure is not proven, so run every obligation."
            elif unresolvedRelationship then "A relationship does not resolve to an accepted module; run every obligation."
            else "Run every obligation until an exact input-closure optimization is qualified."
        { Scope = CorrespondenceScope.All; Reason = reason; ObligationIds = all }
