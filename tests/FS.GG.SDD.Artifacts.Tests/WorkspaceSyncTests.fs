namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module WorkspaceSyncTests =
    let private source =
        { Path = "model.md"; Start = { Line = 1; Column = 1 }; End = { Line = 1; Column = 20 } }

    let private row id kind references assumptions evidence : QuintModelCatalogueEntry =
        {
            Id = id
            Kind = kind
            ExportId = id + "-EXPORT"
            Value =
                QuintRecord
                    [
                        "id", QuintString id
                        "kind", QuintString kind
                        "references", QuintList(List.map QuintString references)
                        "assumptions", QuintList(List.map QuintString assumptions)
                        "evidenceObligationIds", QuintList(List.map QuintString evidence)
                    ]
            Source = source
        }

    let private contract rows =
        {
            Schema = QuintContractV2.schema
            Profile = QuintGeneralProfile.identity
            Specification = "Workspace"
            Exports =
                rows
                |> List.map (fun item ->
                    { Id = item.ExportId; ModuleName = "Workspace"; DeclarationName = item.Id; Value = item.Value; Source = source })
            Catalogue = rows
            ActionEffects = []
            Relationships =
                [ { FromId = "PROD-001"; Kind = QuintRelationshipKind.VerifiedBy; ToId = "EVID-001" } ]
            VerificationProfiles = []
            Bounds = []
            Impacts = []
            Compatibility = []
            Digests = [ { Name = "source"; Sha256 = String.replicate 64 "a" } ]
        }

    let private validRows =
        [
            row "PROD-001" "product-specification" [] [ "mode=safe" ] [ "EVID-001" ]
            row "EVID-001" "evidence-requirement" [] [ "mode=safe" ] []
        ]

    let private expectOk = function Ok value -> value | Error findings -> failwithf "%A" findings
    let private codes (result: Result<WorkspaceModel, SpecificationDiagnostic list>) =
        match result with Ok _ -> [] | Error findings -> findings |> List.map _.Code

    [<Fact>]
    let ``accepted profile-2 rows derive a deterministic two-module workspace and never select zero work`` () =
        let accepted = WorkspaceSync.deriveModel 7L (contract validRows) |> expectOk
        let repeated = WorkspaceSync.deriveModel 7L (contract validRows) |> expectOk
        Assert.Equal(2, accepted.Modules.Length)
        Assert.Equal(WorkspaceLifecycle.fingerprint accepted, WorkspaceLifecycle.fingerprint repeated)
        let selection = WorkspaceSync.select None accepted (contract validRows) []
        Assert.Equal(CorrespondenceScope.All, selection.Scope)
        Assert.Equal(1, selection.ObligationIds.Length)
        let unknown = WorkspaceSync.select (Some accepted) accepted (contract validRows) [ "src/unknown.fs" ]
        Assert.Equal(CorrespondenceScope.All, unknown.Scope)
        Assert.True(selection.ObligationIds = unknown.ObligationIds)
        let unresolved =
            { contract validRows with
                Relationships = [ { FromId = "UNKNOWN"; Kind = QuintRelationshipKind.VerifiedBy; ToId = "EVID-001" } ] }
        let incomplete = WorkspaceSync.select (Some accepted) accepted unresolved []
        Assert.Equal(CorrespondenceScope.All, incomplete.Scope)
        Assert.Contains("does not resolve", incomplete.Reason)

    [<Fact>]
    let ``missing fields, conflicting assumptions and absent evidence refuse authority join`` () =
        let missing =
            match validRows.Head.Value with
            | QuintRecord fields -> { validRows.Head with Value = QuintRecord(List.filter (fst >> ((<>) "references")) fields) }
            | _ -> failwith "fixture"
        Assert.Contains("SYNC-FIELD-MISSING", WorkspaceSync.deriveModel 1L (contract [ missing; validRows[1] ]) |> codes)

        let conflicting =
            row "EVID-001" "evidence-requirement" [] [ "mode=unsafe" ] []
        Assert.Contains("SYNC-ASSUMPTION-CONFLICT", WorkspaceSync.deriveModel 1L (contract [ validRows.Head; conflicting ]) |> codes)

        let noEvidence =
            { contract [ row "PROD-001" "product-specification" [] [ "mode=safe" ] [] ] with Relationships = [] }
        Assert.Contains("SYNC-OBLIGATION-MISSING", WorkspaceSync.deriveModel 1L noEvidence |> codes)

        let unknownKind = row "OTHER-001" "product-specfication" [] [] []
        Assert.Contains("SYNC-KIND-UNKNOWN", WorkspaceSync.deriveModel 1L (contract (validRows @ [ unknownKind ])) |> codes)

    [<Fact>]
    let ``production adapter refuses a selected workspace without its replay subject`` () =
        let noProduct =
            { SchemaVersion = 1; Revision = 0L
              Modules =
                [ { Id = SpecificationId.create "EVID-001" |> Result.defaultWith failwith
                    Kind = WorkspaceModuleKind.EvidenceRequirement
                    ContentSha256 = String.replicate 64 "a"
                    References = []; Assumptions = []; EvidenceObligationIds = [] } ] }
        match WorkspaceReducerAdapter.checkStaleBase noProduct with
        | Ok() -> failwith "missing adapter subject passed"
        | Error findings -> Assert.Contains("SYNC-ADAPTER-SUBJECT", findings |> List.map _.Code)
