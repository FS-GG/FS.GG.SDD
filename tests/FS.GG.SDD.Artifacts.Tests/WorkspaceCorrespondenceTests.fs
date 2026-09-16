namespace FS.GG.SDD.Artifacts.Tests

open System
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module WorkspaceCorrespondenceTests =
    let private id value =
        SpecificationId.create value |> Result.defaultWith failwith

    let private hash character = String(character, 64)

    let private moduleOf identifier kind digest evidence =
        {
            Id = id identifier
            Kind = kind
            ContentSha256 = digest
            References = []
            Assumptions = []
            EvidenceObligationIds = evidence |> List.map id
        }

    let private accepted =
        {
            SchemaVersion = 1
            Revision = 1L
            Modules =
                [
                    moduleOf "PROD-001" WorkspaceModuleKind.ProductSpecification (hash 'a') [ "EVID-001" ]
                    for index, character in [ 'b'; 'c'; 'd'; 'e'; 'f'; '1'; '2' ] |> List.indexed do
                        moduleOf
                            (sprintf "EVID-%03d" (index + 1))
                            WorkspaceModuleKind.EvidenceRequirement
                            (hash character)
                            []
                ]
        }

    let private fingerprint =
        WorkspaceLifecycle.fingerprint accepted
        |> Result.defaultWith (sprintf "%A" >> failwith)

    let private source =
        {
            Path = "model.qnt.md"
            Start = { Line = 1; Column = 1 }
            End = { Line = 1; Column = 10 }
        }

    let private contract =
        let exports: QuintGeneralExport list =
            accepted.Modules
            |> List.map (fun item ->
                {
                    Id = SpecificationId.value item.Id + "-EXPORT"
                    ModuleName = "Workspace"
                    DeclarationName = SpecificationId.value item.Id
                    Value = QuintString item.ContentSha256
                    Source = source
                })

        {
            Schema = QuintContractV2.schema
            Profile = QuintGeneralProfile.identity
            Specification = "Workspace"
            Exports = exports
            Catalogue =
                List.map2
                    (fun (item: WorkspaceModule) (export: QuintGeneralExport) ->
                        {
                            Id = SpecificationId.value item.Id
                            Kind = "workspace-module"
                            ExportId = export.Id
                            Value = export.Value
                            Source = source
                        })
                    accepted.Modules
                    exports
            ActionEffects = []
            Relationships =
                [
                    {
                        FromId = "PROD-001"
                        Kind = QuintRelationshipKind.VerifiedBy
                        ToId = "EVID-001"
                    }
                ]
            VerificationProfiles = []
            Bounds = []
            Impacts =
                [
                    {
                        SubjectId = "PROD-001"
                        Category = "implementation"
                        Detail = "EVID-001"
                    }
                ]
            Compatibility = []
            Digests = [ { Name = "source"; Sha256 = hash 'f' } ]
        }

    let private observation obligation kind state subject =
        {
            ObligationId = id obligation
            Kind = kind
            AcceptedFingerprint = fingerprint
            SubjectFingerprint = subject
            State = state
            SourceBindings = [ "src/Feature.fs:10" ]
            TestBindings = [ "tests/FeatureTests.fs:20" ]
            EvidenceRefs = [ "ci:run/1" ]
            Explanation = $"{obligation} observation"
        }

    [<Fact>]
    let ``workspace codecs are canonical strict and round trip`` () =
        let encoded =
            WorkspaceLifecycle.serializeModel accepted
            |> Result.defaultWith (sprintf "%A" >> failwith)

        let decoded =
            WorkspaceLifecycle.deserializeModel encoded
            |> Result.defaultWith (sprintf "%A" >> failwith)

        Assert.Equal(WorkspaceLifecycle.fingerprint accepted, WorkspaceLifecycle.fingerprint decoded)

        Assert.Equal(
            encoded,
            WorkspaceLifecycle.serializeModel
                { accepted with
                    Modules = List.rev accepted.Modules
                }
            |> Result.defaultWith (sprintf "%A" >> failwith)
        )

        let unknown = encoded.Replace("\"revision\":1", "\"revision\":1,\"invented\":true")

        match WorkspaceLifecycle.deserializeModel unknown with
        | Error findings -> Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-CODEC-INVALID")
        | Ok _ -> Assert.Fail "unknown fields must fail closed"

    [<Fact>]
    let ``reconciliation is order independent and conflicts have no candidate`` () =
        let proposal change =
            {
                SchemaVersion = 1
                IssueRef = "FS-GG/FS.GG.SDD#934"
                ProseSha256 = hash '1'
                BaseFingerprint = fingerprint
                AuthoringDepth = AuthoringDepth.DirectQuint
                Changes = [ change ]
                Disposition = ProposalDisposition.CoherentDelta
                EvidenceFingerprint = None
            }

        let left =
            proposal (WorkspaceChange.Upsert(moduleOf "DECIS-001" WorkspaceModuleKind.Decision (hash '2') []))

        let right =
            proposal (WorkspaceChange.Upsert(moduleOf "WORKC-001" WorkspaceModuleKind.WorkChange (hash '3') []))

        match WorkspaceLifecycle.reconcile accepted left right, WorkspaceLifecycle.reconcile accepted right left with
        | Reconciled(a, ac), Reconciled(b, bc) ->
            Assert.Equal(WorkspaceLifecycle.canonicalBytes a, WorkspaceLifecycle.canonicalBytes b)
            Assert.True((ac: WorkspaceSemanticChange list) = bc)
        | _ -> Assert.Fail "disjoint exact-base proposals should reconcile"

        match WorkspaceLifecycle.reconcile accepted left left with
        | Conflicted findings -> Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-MERGE-OVERLAP")
        | Reconciled _ -> Assert.Fail "a conflict must never expose a candidate"

    [<Fact>]
    let ``correspondence preserves all seven outcomes from one typed report`` () =
        let kinds =
            [
                CorrespondenceObservationKind.GeneratedContract
                CorrespondenceObservationKind.SourceBinding
                CorrespondenceObservationKind.Test
                CorrespondenceObservationKind.EvidenceReceipt
            ]

        let observations =
            [
                for kind in kinds do
                    yield observation "EVID-001" kind CorrespondenceObservationState.Observed (Some(hash 'b'))
                yield
                    observation
                        "EVID-002"
                        CorrespondenceObservationKind.SourceBinding
                        CorrespondenceObservationState.Missing
                        (Some(hash 'c'))
                yield
                    observation
                        "EVID-003"
                        CorrespondenceObservationKind.SourceBinding
                        CorrespondenceObservationState.Observed
                        (Some(hash '0'))
                yield
                    observation
                        "EVID-004"
                        CorrespondenceObservationKind.SourceBinding
                        (CorrespondenceObservationState.Contradicted "implementation disagrees")
                        (Some(hash 'e'))
                yield
                    observation
                        "EVID-005"
                        CorrespondenceObservationKind.SourceBinding
                        (CorrespondenceObservationState.Ambiguous "two bindings")
                        (Some(hash 'f'))
                yield
                    observation
                        "EVID-006"
                        CorrespondenceObservationKind.SourceBinding
                        (CorrespondenceObservationState.Unsupported "foreign runtime")
                        (Some(hash '1'))
            ]

        let report =
            WorkspaceCorrespondence.evaluate accepted contract observations CorrespondenceScope.All
            |> Result.defaultWith (sprintf "%A" >> failwith)

        let statuses = report.Entries |> List.map _.Status |> Set.ofList
        Assert.Equal(7, statuses.Count)
        Assert.Contains(CorrespondenceStatus.Satisfied, statuses)
        Assert.Contains(CorrespondenceStatus.Unobserved, statuses)

        let satisfied =
            report.Entries
            |> List.find (fun item -> item.Status = CorrespondenceStatus.Satisfied)

        Assert.Equal(hash 'b', satisfied.ExpectedFingerprint)
        Assert.Equal<string list>([ hash 'b' ], satisfied.ObservedFingerprints)
        Assert.Contains($"schema:{QuintContractV2.schema}", report.Provenance)
        Assert.Contains(report.Diagnostics, fun item -> item.Code = "CORRESPONDENCE-OBLIGATION-UNSATISFIED")
        Assert.Equal(WorkspaceCorrespondence.serializeReport report, WorkspaceCorrespondence.serializeReport report)
        Assert.Contains("\"expectedFingerprint\"", WorkspaceCorrespondence.serializeReport report)
        Assert.Contains("EVID-001: satisfied", WorkspaceCorrespondence.renderPlain report)
        Assert.Contains("## EVID-001 — satisfied", WorkspaceCorrespondence.renderRich report)

    [<Fact>]
    let ``observations target evidence obligations and bind non-missing subjects`` () =
        let wrongKind =
            observation
                "PROD-001"
                CorrespondenceObservationKind.SourceBinding
                CorrespondenceObservationState.Observed
                (Some(hash 'a'))

        let unbound =
            observation "EVID-001" CorrespondenceObservationKind.Test CorrespondenceObservationState.Observed None

        match WorkspaceCorrespondence.evaluate accepted contract [ wrongKind; unbound ] CorrespondenceScope.All with
        | Error findings ->
            Assert.Contains(findings, fun item -> item.Code = "CORRESPONDENCE-OBLIGATION-UNKNOWN")
            Assert.Contains(findings, fun item -> item.Code = "CORRESPONDENCE-SUBJECT-DIGEST")
        | Ok _ -> Assert.Fail "invalid observation targets must fail closed"

    [<Fact>]
    let ``forged global input fails closed even for selective correspondence`` () =
        let forged =
            { observation
                  "EVID-001"
                  CorrespondenceObservationKind.Test
                  CorrespondenceObservationState.Observed
                  (Some(hash 'b')) with
                AcceptedFingerprint = hash '0'
            }

        match
            WorkspaceCorrespondence.evaluate
                accepted
                contract
                [ forged ]
                (CorrespondenceScope.ImpactedBy [ "PROD-001" ])
        with
        | Error findings -> Assert.Contains(findings, fun item -> item.Code = "CORRESPONDENCE-FINGERPRINT-FORGED")
        | Ok _ -> Assert.Fail "selective evaluation must retain global integrity failures"

    [<Fact>]
    let ``selective correspondence follows compiled impact relationships`` () =
        let report =
            WorkspaceCorrespondence.evaluate accepted contract [] (CorrespondenceScope.ImpactedBy [ "PROD-001" ])
            |> Result.defaultWith (sprintf "%A" >> failwith)

        Assert.Single(report.Entries) |> ignore
        Assert.Equal(id "EVID-001", report.Entries.Head.ObligationId)
