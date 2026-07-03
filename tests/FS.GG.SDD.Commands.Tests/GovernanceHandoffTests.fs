namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Artifacts.GovernanceHandoff
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module GovernanceHandoffTests =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let workId = "017-governance-handoff"
    let title = "Governance Handoff"
    let handoffPath = $"readiness/{workId}/governance-handoff.json"
    let shipPath = $"readiness/{workId}/ship.json"
    let verifyPath = $"readiness/{workId}/verify.json"

    // ---- shared fixtures ----

    let shippedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        root

    let readHandoff root =
        TestSupport.readRelative root handoffPath

    let parse (text: string) = JsonDocument.Parse(text).RootElement
    let prop (name: string) (element: JsonElement) = element.GetProperty name
    let str (name: string) (element: JsonElement) = (element.GetProperty name).GetString()

    let arr (name: string) (element: JsonElement) =
        (element.GetProperty name).EnumerateArray() |> Seq.toList

    // ---- projection-level builders (the projection is the unit under test) ----

    let generator = SchemaVersionModule.currentGeneratorVersion ()

    let emptyModel: WorkModel =
        { SchemaVersion = 1
          ModelVersion = "1.0"
          WorkId = workId
          Project =
            { Id = "fsgg-sdd"
              DefaultWorkRoot = "work" }
          Sources = []
          WorkItem =
            { Id = workId
              Title = title
              Stage = "ship"
              ChangeTier = "tier1"
              Status = "draft" }
          Requirements = []
          Decisions = []
          Tasks = []
          Evidence = []
          GeneratedViews = []
          Diagnostics = []
          GovernanceBoundaries = [] }

    let mkEvidence id result synthetic taskRefs rationale : EvidenceEntry =
        { Id = id
          Kind = "verification"
          SubjectType = "task"
          SubjectId = taskRefs |> List.tryHead |> Option.defaultValue ""
          TaskRefs = taskRefs
          RequirementRefs = []
          ArtifactRefs = []
          Result = result
          Synthetic = synthetic
          Rationale = rationale
          Source = $"work/{workId}/evidence.yml"
          SourceLocation = None }

    let mkTask id status deps requiredEvidence : TaskEntry =
        { Id = id
          Title = id
          Status = status
          Owner = "sdd"
          Dependencies = deps
          Requirements = []
          Decisions = []
          SourceIds = []
          RequiredSkills = []
          RequiredEvidence = requiredEvidence
          Source = $"work/{workId}/tasks.yml"
          SourceLocation = None }

    let mkBoundary path owner relationship : GovernanceBoundaryEntry =
        { Path = path
          Owner = owner
          RequiredBySdd = false
          Relationship = relationship }

    let readinessFacts disposition verification blockingIds : ReadinessFacts =
        { ShipDisposition = disposition
          VerificationReadiness = verification
          AdvisoryCount = 0
          WarningCount = 0
          BlockingCount = List.length blockingIds
          BlockingDiagnosticIds = blockingIds
          PerViewState =
            [ "ship.json", "current"
              "verify.json", "current"
              "work-model.json", "current" ] }

    let cleanReadiness = readinessFacts "shipReady" "verificationReady" []

    let project model config readiness =
        fromWorkModel model [] config readiness generator

    // =====================================================================
    // US1 — versioned envelope, no Governance, determinism, byte-identity
    // =====================================================================

    [<Fact>]
    let ``US1 ship emits a versioned governance handoff envelope with sources and digests`` () =
        let root = shippedProject ()
        Assert.True(TestSupport.existsRelative root handoffPath)
        let doc = parse (readHandoff root)
        Assert.Equal(1, (prop "schemaVersion" doc).GetInt32())
        Assert.Equal("1.0.0", str "contractVersion" doc)
        Assert.False(System.String.IsNullOrWhiteSpace(str "generatorVersion" doc))
        Assert.Equal(workId, str "workId" doc)
        let sources = arr "sources" doc
        Assert.Equal(3, List.length sources)

        sources
        |> List.iter (fun source ->
            Assert.StartsWith("sha256:", str "digest" source)
            Assert.False(System.String.IsNullOrWhiteSpace(str "path" source)))

    [<Fact>]
    let ``US1 ship reports the handoff as a generated view of kind governance-handoff`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        let report = TestSupport.runShip root workId title

        let handoffView =
            report.GeneratedViews |> List.tryFind (fun view -> view.Path = handoffPath)

        match handoffView with
        | Some view ->
            Assert.Equal("governance-handoff", view.Kind)
            Assert.Equal(GeneratedViewCurrency.Current, view.Currency)
        | None -> failwith "Expected a governance-handoff generated view in the ship report."

    [<Fact>]
    let ``US1 handoff omits Governance config when no fsgg files are present (SC-002)`` () =
        let root = shippedProject ()
        let doc = parse (readHandoff root)
        let config = prop "governanceConfig" doc
        Assert.False((prop "policyPresent" config).GetBoolean())
        Assert.False((prop "capabilitiesPresent" config).GetBoolean())
        Assert.False((prop "toolingPresent" config).GetBoolean())
        // pointers omitted entirely when absent (FR-011)
        Assert.False(config.TryGetProperty("policyPointer") |> fst)
        Assert.False(config.TryGetProperty("capabilitiesPointer") |> fst)
        Assert.False(config.TryGetProperty("toolingPointer") |> fst)

    [<Fact>]
    let ``US1 handoff is byte-identical across two productions over an identical source tree (SC-003)`` () =
        // Same project tree (same project id, same authored sources): re-producing the handoff is
        // byte-stable. (A fresh `init` mints a random project id, so a different tree differs by design.)
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        let first = readHandoff root
        TestSupport.runShip root workId title |> ignore
        let second = readHandoff root
        Assert.Equal(first, second)

    [<Fact>]
    let ``US1 ship leaves authored sources byte-identical (SC-007)`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title

        let authored =
            [ ".fsgg/project.yml"
              ".fsgg/sdd.yml"
              ".fsgg/agents.yml"
              $"work/{workId}/spec.md"
              $"work/{workId}/tasks.yml"
              $"work/{workId}/evidence.yml" ]
            |> List.filter (TestSupport.existsRelative root)

        let before =
            authored |> List.map (fun path -> path, TestSupport.readRelative root path)

        TestSupport.runShip root workId title |> ignore

        let after =
            before |> List.map (fun (path, _) -> path, TestSupport.readRelative root path)

        Assert.Equal<(string * string) list>(before, after)

    // =====================================================================
    // US2 — declared evidence states + dependency edges, no computed taint
    // =====================================================================

    [<Fact>]
    let ``US2 shipped handoff carries evidence nodes and directed edges with every endpoint present`` () =
        let root = shippedProject ()
        let doc = parse (readHandoff root)
        let evidence = prop "evidence" doc
        let nodeIds = arr "nodes" evidence |> List.map (str "id") |> Set.ofList
        Assert.NotEmpty nodeIds
        let edges = arr "dependencies" evidence
        Assert.NotEmpty edges
        // every edge endpoint resolves to a node (invariant 6)
        edges
        |> List.iter (fun edge ->
            Assert.Contains(str "dependent" edge, nodeIds)
            Assert.Contains(str "dependency" edge, nodeIds))

    [<Fact>]
    let ``US2 evidence-state mapping is total over the SDD result space (SC-004)`` () =
        let expect result synthetic expected =
            Assert.Equal(expected, mapEvidenceState result synthetic)

        // synthetic dominates the result token
        for result in [ "supported"; "pass"; "missing"; "failed"; "stale"; "anything" ] do
            expect result true Synthetic

        expect "supported" false Real
        expect "passed" false Real
        expect "pass" false Real
        expect "real" false Real
        expect "verified" false Real
        expect "deferred" false Skipped
        expect "accepted-deferral" false Skipped
        expect "failed" false Failed
        expect "invalid" false Failed
        expect "stale" false Real
        expect "missing" false Pending
        expect "none" false Pending
        expect "not-started" false Pending
        expect "pending" false Pending
        expect "" false Pending
        // total: an unrecognized token still maps (never throws)
        expect "totally-unknown-token" false Pending
        Assert.True(isStaleEvidenceResult "stale")
        Assert.False(isStaleEvidenceResult "supported")

    [<Fact>]
    let ``US2 synthetic declaration does not taint a real dependent and emits no autoSynthetic (SC-005)`` () =
        let model =
            { emptyModel with
                Tasks = [ mkTask "T001" "done" [] [ "EVS"; "EVR" ] ]
                Evidence =
                    [ mkEvidence "EVS" "supported" true [ "T001" ] (Some "stub data")
                      mkEvidence "EVR" "supported" false [ "T001" ] None ] }

        let handoff = project model emptyGovernanceConfig cleanReadiness

        let nodeState id =
            handoff.Evidence.Nodes
            |> List.find (fun node -> node.Id = id)
            |> fun node -> node.State

        Assert.Equal(Synthetic, nodeState "evidence:EVS")
        Assert.Equal(Real, nodeState "evidence:EVR")
        // the real dependent is NOT downgraded; SDD computes no taint closure
        Assert.DoesNotContain("autoSynthetic", toJson handoff)

    [<Fact>]
    let ``US2 existing diagnostics and a declared cycle are carried verbatim, not pre-rejected (invariant 7)`` () =
        let conflict =
            DiagnosticsModule.create
                "proseStructuredMismatch"
                DiagnosticWarning
                None
                None
                "prose disagrees with structure"
                "trust the structured artifact"
                []

        let model =
            { emptyModel with
                Diagnostics = [ conflict ]
                // a declared dependency cycle T001 -> T002 -> T001
                Tasks = [ mkTask "T001" "todo" [ "T002" ] []; mkTask "T002" "todo" [ "T001" ] [] ] }

        let handoff = project model emptyGovernanceConfig cleanReadiness
        Assert.Contains(handoff.Diagnostics, fun d -> d.Id = "proseStructuredMismatch")
        // the cycle edges are carried as-is (SDD does not reject)
        Assert.Contains(handoff.Evidence.Dependencies, fun e -> e.Dependent = "task:T001" && e.Dependency = "task:T002")
        Assert.Contains(handoff.Evidence.Dependencies, fun e -> e.Dependent = "task:T002" && e.Dependency = "task:T001")

    [<Fact>]
    let ``US2 declared-stale evidence keeps its base state and surfaces a staleEvidence diagnostic`` () =
        let model =
            { emptyModel with
                Tasks = [ mkTask "T001" "done" [] [ "EV1" ] ]
                Evidence = [ mkEvidence "EV1" "stale" false [ "T001" ] None ] }

        let handoff = project model emptyGovernanceConfig cleanReadiness
        let node = handoff.Evidence.Nodes |> List.find (fun n -> n.Id = "evidence:EV1")
        Assert.Equal(Real, node.State)
        Assert.Contains(handoff.Diagnostics, fun d -> d.Id = "staleEvidence")
        Assert.DoesNotContain("autoSynthetic", toJson handoff)

    // =====================================================================
    // US3 — governed/routing references + .fsgg presence, no route selection
    // =====================================================================

    [<Fact>]
    let ``US3 governed references are projected from boundaries with stable, sorted identity`` () =
        let model =
            { emptyModel with
                GovernanceBoundaries =
                    [ mkBoundary "src/Zeta.fs" "sdd" "authored"
                      mkBoundary "src/Alpha.fs" "governance" "governed" ] }

        let handoff = project model emptyGovernanceConfig cleanReadiness

        Assert.Equal<string list>(
            [ "src/Alpha.fs"; "src/Zeta.fs" ],
            handoff.GovernedReferences |> List.map (fun reference -> reference.Path)
        )

        let alpha =
            handoff.GovernedReferences
            |> List.find (fun reference -> reference.Path = "src/Alpha.fs")

        Assert.Equal("governance", alpha.Owner)
        Assert.Equal("governed", alpha.Relationship)

    [<Fact>]
    let ``US3 fsgg pointers are referenced when present and reported absent without failure`` () =
        let present: GovernanceConfigPresence =
            { PolicyPresent = true
              PolicyPointer = Some ".fsgg/policy.yml"
              CapabilitiesPresent = false
              CapabilitiesPointer = None
              ToolingPresent = false
              ToolingPointer = None }

        let json = toJson (project emptyModel present cleanReadiness)
        let config = prop "governanceConfig" (parse json)
        Assert.True((prop "policyPresent" config).GetBoolean())
        Assert.Equal(".fsgg/policy.yml", str "policyPointer" config)
        Assert.False((prop "capabilitiesPresent" config).GetBoolean())
        Assert.False(config.TryGetProperty("capabilitiesPointer") |> fst)

    [<Fact>]
    let ``US3 shipped handoff selects no route, profile, gate, or enforcement (SC-005)`` () =
        let json = readHandoff (shippedProject ())

        for forbidden in
            [ "autoSynthetic"
              "route"
              "profile"
              "gate"
              "enforcement"
              "verdict"
              "capabilityVerdict"
              "matchedGlob" ] do
            Assert.DoesNotContain(forbidden, json)

    // =====================================================================
    // US4 — merge-boundary readiness as advisory facts, never a verdict
    // =====================================================================

    [<Fact>]
    let ``US4 ship-ready readiness carries the disposition and zero blocking ids`` () =
        let handoff = project emptyModel emptyGovernanceConfig cleanReadiness
        Assert.Equal("shipReady", handoff.Readiness.ShipDisposition)
        Assert.Empty handoff.Readiness.BlockingDiagnosticIds
        Assert.Equal(0, handoff.Readiness.BlockingCount)

    [<Fact>]
    let ``US4 ship-blocked readiness carries blocking ids without refusing or asserting a verdict`` () =
        let blocked =
            readinessFacts
                "needsShipCorrection"
                "needsVerificationCorrection"
                [ "evidenceNotSynthetic"; "contractsCurrent" ]
        // the projection is total: it produces a handoff for a blocked work item, it does not refuse
        let handoff = project emptyModel emptyGovernanceConfig blocked
        Assert.Equal("needsShipCorrection", handoff.Readiness.ShipDisposition)

        Assert.Equal<string list>(
            [ "contractsCurrent"; "evidenceNotSynthetic" ],
            handoff.Readiness.BlockingDiagnosticIds |> List.sort
        )

        let json = toJson handoff
        // advisory facts only — no pass/fail/enforcement verdict token
        for forbidden in [ "passVerdict"; "failVerdict"; "enforcement"; "\"pass\""; "\"fail\"" ] do
            Assert.DoesNotContain(forbidden, json)

    // =====================================================================
    // US5 — stale detection + refresh currency
    // =====================================================================

    let shipJsonDigest root =
        (SchemaVersionModule.sha256Text (TestSupport.readRelative root shipPath)).Value

    let handoffShipSourceDigest root =
        let doc = parse (readHandoff root)

        arr "sources" doc
        |> List.find (fun source -> str "path" source = shipPath)
        |> str "digest"

    [<Fact>]
    let ``US5 a modified contributing source makes the handoff stale and refresh restores it (SC-006)`` () =
        let root = shippedProject ()
        // recorded digest matches the live ship.json at production time
        Assert.Equal($"sha256:{shipJsonDigest root}", handoffShipSourceDigest root)

        // mutate a contributing source (ship.json) so its live digest changes
        let mutated = (TestSupport.readRelative root shipPath).TrimEnd() + "\n"
        TestSupport.writeRelative root shipPath mutated

        // the handoff's recorded source digest no longer matches the live source — stale (AC1)
        Assert.NotEqual($"sha256:{shipJsonDigest root}", handoffShipSourceDigest root)

        // refresh regenerates the handoff against the current source — current again (AC2)
        let report = TestSupport.runRefresh root workId
        Assert.Equal("current", TestSupport.refreshViewState report "governance-handoff")
        Assert.Equal($"sha256:{shipJsonDigest root}", handoffShipSourceDigest root)

    [<Fact>]
    let ``US5 a missing handoff is regenerated by refresh and reported current (AC3)`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, handoffPath.Replace('/', Path.DirectorySeparatorChar)))
        Assert.False(TestSupport.existsRelative root handoffPath)

        let report = TestSupport.runRefresh root workId
        Assert.True(TestSupport.existsRelative root handoffPath)
        Assert.Equal("current", TestSupport.refreshViewState report "governance-handoff")

    [<Fact>]
    let ``US5 refresh preserves authored sources byte-identical (SC-007)`` () =
        let root = shippedProject ()

        let authored =
            [ $"work/{workId}/spec.md"
              $"work/{workId}/tasks.yml"
              $"work/{workId}/evidence.yml" ]
            |> List.filter (TestSupport.existsRelative root)

        let before = authored |> List.map (TestSupport.readRelative root)
        TestSupport.runRefresh root workId |> ignore
        let after = authored |> List.map (TestSupport.readRelative root)
        Assert.Equal<string list>(before, after)
