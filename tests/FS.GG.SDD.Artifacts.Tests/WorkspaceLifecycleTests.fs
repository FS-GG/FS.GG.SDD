namespace FS.GG.SDD.Artifacts.Tests

open System
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module WorkspaceLifecycleTests =
    let private id value = SpecificationId.create value |> Result.defaultWith failwith
    let private hash character = String(character, 64)
    let private expectError result =
        match result with
        | Error findings -> findings
        | Ok _ -> failwith "expected an error result"

    let private moduleOf identifier kind digest references assumptions evidence =
        { Id = id identifier
          Kind = kind
          ContentSha256 = digest
          References = references |> List.map id
          Assumptions = assumptions
          EvidenceObligationIds = evidence |> List.map id }

    let private accepted =
        { SchemaVersion = 1
          Revision = 7L
          Modules =
            [ moduleOf "PROD-001" WorkspaceModuleKind.ProductSpecification (hash 'a') [] [ "users require deterministic state" ] [ "EVID-001" ]
              moduleOf "EVID-001" WorkspaceModuleKind.EvidenceRequirement (hash 'b') [] [] []
              moduleOf "DECIS-001" WorkspaceModuleKind.Decision (hash 'c') [ "PROD-001" ] [] [] ] }

    let private proposal changes disposition =
        { SchemaVersion = 1
          IssueRef = "FS-GG/FS.GG.SDD#927"
          ProseSha256 = hash 'd'
          BaseFingerprint = WorkspaceLifecycle.fingerprint accepted |> Result.defaultWith (sprintf "%A" >> failwith)
          AuthoringDepth = AuthoringDepth.Freeform
          Changes = changes
          Disposition = disposition
          EvidenceFingerprint = None }

    let private receipt =
        { AcceptedBy = "maintainer@example.invalid"
          EvidenceRefs = [ "github:review/1" ]
          AcceptedAtUtc = "2026-09-15T12:00:00Z" }

    [<Fact>]
    let ``all seven module kinds have stable canonical fingerprints`` () =
        let kinds =
            [ WorkspaceModuleKind.ProductSpecification
              WorkspaceModuleKind.Decision
              WorkspaceModuleKind.WorkChange
              WorkspaceModuleKind.RepositoryProfile
              WorkspaceModuleKind.CiObligation
              WorkspaceModuleKind.ExternalContract
              WorkspaceModuleKind.EvidenceRequirement ]

        let model =
            { SchemaVersion = 1
              Revision = 1L
              Modules =
                kinds
                |> List.mapi (fun index kind ->
                    moduleOf (sprintf "MODUL-%03d" (index + 1)) kind (hash (char (int 'a' + (index % 6)))) [] [] []) }

        Assert.Empty(WorkspaceLifecycle.validate model)
        Assert.Equal(WorkspaceLifecycle.fingerprint model, WorkspaceLifecycle.fingerprint { model with Modules = List.rev model.Modules })

    [<Fact>]
    let ``proposal inspection never mutates accepted authority`` () =
        let before = WorkspaceLifecycle.fingerprint accepted
        let candidate = proposal [] (ProposalDisposition.Ambiguous "needs classification")
        Assert.Empty(WorkspaceLifecycle.validateProposal accepted candidate)
        let after = WorkspaceLifecycle.fingerprint accepted
        Assert.Equal(before, after)

    [<Fact>]
    let ``accepted opaque requires complete debt ownership and evidence`` () =
        let opaque =
            { Debt = ""
              AffectedSubjects = []
              Reason = ""
              ResponsibleHuman = ""
              EvidenceRefs = [] }

        let findings = proposal [] (ProposalDisposition.AcceptedOpaque opaque) |> WorkspaceLifecycle.validateProposal accepted
        Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-OPAQUE-DEBT")
        Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-OPAQUE-EVIDENCE")

    [<Fact>]
    let ``disjoint exact-base proposals reconcile deterministically`` () =
        let product = moduleOf "PROD-001" WorkspaceModuleKind.ProductSpecification (hash 'e') [] [ "users require deterministic state" ] [ "EVID-001" ]
        let decision = moduleOf "DECIS-002" WorkspaceModuleKind.Decision (hash 'f') [ "PROD-001" ] [] []
        let left = proposal [ WorkspaceChange.Upsert product ] ProposalDisposition.CoherentDelta
        let right = proposal [ WorkspaceChange.Upsert decision ] ProposalDisposition.CoherentDelta

        match WorkspaceLifecycle.reconcile accepted left right with
        | Reconciled(model, changes) ->
            Assert.Equal(8L, model.Revision)
            Assert.Equal(4, model.Modules.Length)
            Assert.Equal(2, changes.Length)
        | Conflicted findings -> Assert.Fail $"unexpected conflict: {findings}"

    [<Fact>]
    let ``overlap rename-delete dangling and stale inputs fail closed`` () =
        let changedA = moduleOf "PROD-001" WorkspaceModuleKind.ProductSpecification (hash 'e') [] [] [ "EVID-001" ]
        let changedB = moduleOf "PROD-001" WorkspaceModuleKind.ProductSpecification (hash 'f') [] [] [ "EVID-001" ]
        let overlap = WorkspaceLifecycle.reconcile accepted (proposal [ WorkspaceChange.Upsert changedA ] ProposalDisposition.CoherentDelta) (proposal [ WorkspaceChange.Upsert changedB ] ProposalDisposition.CoherentDelta)
        match overlap with
        | Conflicted findings -> Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-MERGE-OVERLAP")
        | _ -> Assert.Fail "expected overlap conflict"

        let dangling = proposal [ WorkspaceChange.Remove(id "PROD-001") ] ProposalDisposition.CoherentDelta
        let findings = WorkspaceLifecycle.reduce accepted dangling receipt |> expectError
        Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-REFERENCE-DANGLING")

        let renameDelete = WorkspaceLifecycle.reconcile accepted (proposal [ WorkspaceChange.Rename(id "PROD-001", id "PROD-002") ] ProposalDisposition.CoherentDelta) dangling
        match renameDelete with
        | Conflicted findings -> Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-MERGE-RENAME-DELETE")
        | _ -> Assert.Fail "expected rename/delete conflict"

        let stale = { proposal [] ProposalDisposition.CoherentDelta with BaseFingerprint = hash '0' }
        Assert.Contains(WorkspaceLifecycle.validateProposal accepted stale, fun item -> item.Code = "WORKSPACE-PROPOSAL-STALE")

    [<Fact>]
    let ``only eligible human-accepted proposals reduce exact-base state`` () =
        let added = moduleOf "WORKC-001" WorkspaceModuleKind.WorkChange (hash 'e') [ "PROD-001" ] [] []
        let coherent = proposal [ WorkspaceChange.Upsert added ] ProposalDisposition.CoherentDelta
        let reduced = WorkspaceLifecycle.reduce accepted coherent receipt |> Result.defaultWith (sprintf "%A" >> failwith)
        Assert.Equal(8L, reduced.Revision)
        Assert.Contains(reduced.Modules, fun item -> item.Id = id "WORKC-001")

        let ambiguous = proposal [] (ProposalDisposition.Ambiguous "unresolved")
        Assert.Contains(WorkspaceLifecycle.reduce accepted ambiguous receipt |> expectError, fun item -> item.Code = "WORKSPACE-PROPOSAL-NONREDUCIBLE")

    [<Fact>]
    let ``semantic diff exposes changed assumptions and duplicate edits refuse`` () =
        let changed =
            { accepted with
                Modules =
                    accepted.Modules
                    |> List.map (fun item ->
                        if item.Id = id "PROD-001" then { item with Assumptions = [ "a newer assumption" ] }
                        else item) }

        let diff = WorkspaceLifecycle.semanticDiff accepted changed |> Result.defaultWith (sprintf "%A" >> failwith)
        Assert.Contains(diff, fun item -> item.Subject = id "PROD-001" && item.Summary = "change assumptions")

        let duplicate =
            proposal
                [ WorkspaceChange.Remove(id "DECIS-001")
                  WorkspaceChange.Remove(id "DECIS-001") ]
                ProposalDisposition.CoherentDelta

        Assert.Contains(WorkspaceLifecycle.validateProposal accepted duplicate, fun item -> item.Code = "WORKSPACE-CHANGE-DUPLICATE")

    [<Fact>]
    let ``migration stays dry-run until every source and decision is complete`` () =
        let sources =
            [ { Path = ".fsgg/sdd.yml"
                OriginalSha256 = hash 'a'
                Lifecycle = LegacyLifecycle.Sdd
                Classification = LegacyMigrationClassification.Migrated
                TargetPath = Some ".fsgg/typed-sdd/manifest.json" }
              { Path = "specs/legacy/spec.md"
                OriginalSha256 = hash 'b'
                Lifecycle = LegacyLifecycle.SpecKit
                Classification = LegacyMigrationClassification.Ambiguous "heading has two meanings"
                TargetPath = None } ]

        let incomplete, findings = WorkspaceMigration.plan "quint-specification-v1" (hash 'c') sources []
        Assert.False incomplete.ReadyToApply
        Assert.Contains(findings, fun item -> item.Code = "WORKSPACE-MIGRATION-DECISION")

        let completeSources =
            sources
            |> List.map (fun item ->
                if item.Path.StartsWith("specs/", StringComparison.Ordinal) then
                    { item with Classification = LegacyMigrationClassification.Preserved }
                else item)

        let complete, completeFindings = WorkspaceMigration.plan "quint-specification-v1" (hash 'c') completeSources [ receipt ]
        Assert.True complete.ReadyToApply
        Assert.Empty completeFindings
