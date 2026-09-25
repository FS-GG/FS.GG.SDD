namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.GenerationSourceContract
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

module GenerationSourceContractTests =
    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-source-contract-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, "producer")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    let private source path bytes =
        { Path = path; Digest = SchemaVersion.sha256Bytes bytes }

    let private contract files =
        { Version = 1; ClosedRoot = "producer"; Policy = ExactBytes; Sources = files }

    let private selected = { ClosedRoot = "producer"; Policy = ExactBytes }

    [<Fact>]
    let ``v1 binds the complete physical source set to immutable bytes`` () =
        withTree (fun root ->
            let bytes = [| 0xefuy; 0xbbuy; 0xbfuy; 65uy; 13uy; 10uy |]
            let file = Path.Combine(root, "producer", "a.txt")
            File.WriteAllBytes(file, bytes)
            match verify root selected (contract [ source "producer/a.txt" bytes ]) with
            | Error reason -> failwithf "valid contract refused: %A" reason
            | Ok captured ->
                Assert.Single captured |> ignore
                let returned = captured.Head.Bytes
                returned.[0] <- 0uy
                Assert.True(captured.Head.Bytes = bytes)
                Assert.Equal(SchemaVersion.sha256Bytes bytes, captured.Head.Digest))

    [<Fact>]
    let ``candidate cannot change producer root or digest policy`` () =
        withTree (fun root ->
            let bytes = Encoding.UTF8.GetBytes "value\r\n"
            File.WriteAllBytes(Path.Combine(root, "producer", "a.txt"), bytes)
            let valid = contract [ source "producer/a.txt" bytes ]
            Assert.Equal(Error WrongRoot, verify root selected { valid with ClosedRoot = "other" })
            Assert.Equal(Error WrongPolicy, verify root selected { valid with Policy = Utf8LfText })
            Assert.Equal(Error(UnsupportedVersion 2), verify root selected { valid with Version = 2 }))

    [<Fact>]
    let ``omitted physical source and stale digest refuse`` () =
        withTree (fun root ->
            let bytes = [| 1uy; 2uy |]
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), bytes)
            let original = contract [ source "producer/a.bin" bytes ]
            File.WriteAllBytes(Path.Combine(root, "producer", "b.bin"), [| 3uy |])
            Assert.Equal(Error(Physical(UnexpectedFile "producer/b.bin")), verify root selected original)
            File.Delete(Path.Combine(root, "producer", "b.bin"))
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), [| 1uy; 4uy |])
            Assert.Equal(Error(DigestDrift "producer/a.bin"), verify root selected original))

    [<Fact>]
    let ``malformed and duplicate candidate rows refuse`` () =
        withTree (fun root ->
            let bytes = [| 1uy |]
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), bytes)
            let valid = source "producer/a.bin" bytes
            let malformed = { valid with Digest = { Algorithm = "sha256"; Value = "ABC" } }
            Assert.Equal(Error(MalformedDigest "producer/a.bin"), verify root selected (contract [ malformed ]))
            Assert.Equal(Error(Physical(DuplicatePath "producer/a.bin")),
                verify root selected (contract [ valid; valid ])))

    [<Fact>]
    let ``contract propagates linked workspace ancestor refusal`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let physical = Path.Combine(root, "physical", "workspace")
                Directory.CreateDirectory(Path.Combine(physical, "producer")) |> ignore
                let bytes = [| 1uy |]
                File.WriteAllBytes(Path.Combine(physical, "producer", "a.bin"), bytes)
                Directory.CreateSymbolicLink(Path.Combine(root, "alias"), Path.Combine(root, "physical")) |> ignore
                let workspace = Path.Combine(root, "alias", "workspace")
                Assert.Equal(Error(Physical(Symlink "..")),
                    verify workspace selected (contract [ source "producer/a.bin" bytes ])))

/// Selection evidence for a later producer-owned multi-root contract. These
/// fixtures do not authorize the single-root verifier to accept work-model paths.
module WorkModelMultiRootSelectionCharacterizationTests =
    type private PerformanceObservation = Observed | NotRead | ReadFailed

    let private workId = "multi-root-fixture"
    let private performancePath = $"readiness/{workId}/performance-evidence.json"

    let private evidence =
        $"""schemaVersion: 1
workId: {workId}
stage: evidence
status: evidenceReady
sourceSpec: work/{workId}/spec.md
sourceClarifications: work/{workId}/clarifications.md
sourceChecklist: work/{workId}/checklist.md
sourcePlan: work/{workId}/plan.md
sourceTasks: work/{workId}/tasks.yml
sourceAnalysis: readiness/{workId}/analysis.json
sourceSnapshots: []
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: [FR-001]
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    checklistResultRefs: []
    planDecisionRefs: [PD-001]
    obligationRefs: [EV001]
    artifacts: [tests/performance.txt]
    sourceRefs:
      - kind: test-output
        path: tests/performance.txt
        result: pass
    performanceBudget:
      artifactPath: {performancePath}
      targetFps: 60
      workloadIds: [normal-play]
      stressWorkloadIds: [pointer-stress]
      workloadDefinitionDigests: [normal-play=sha256:normal-v1, pointer-stress=sha256:stress-v1]
      currencyToken: commit:fixture
      capturedAfterUtc: 2026-08-22T00:00:00Z
      maxP95Ms: 16.67
      maxP99Ms: 25
      maxCatchUpFrames: 0
      measurementScope: normal
      requiredCapability: bounded-headless-update-render
      liveCompositorRequired: false
    result: pass
    synthetic: false
    notes: []
"""

    let private observedRead path text : CommandEffectResult =
        let snapshot = { Path = path; Text = text; RawBytes = None }
        {
            Effect = ReadFile path
            Succeeded = true
            Read = Bytes snapshot
            Snapshot = Some snapshot
            Process = None
            Confirmed = None
            Diagnostic = None
        }

    let private unreadableRead path : CommandEffectResult =
        {
            Effect = ReadFile path
            Succeeded = false
            Read = ReadResult.Unreadable(path, "fixture read failure")
            Snapshot = None
            Process = None
            Confirmed = None
            Diagnostic = None
        }

    let private select performanceObservation =
        let request = TestSupport.request Analyze "."
        let model, _ = FS.GG.SDD.Commands.CommandWorkflow.init request
        let configReads =
            [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
            |> List.map (fun path -> observedRead path "schemaVersion: 1\n")
        let reads =
            match performanceObservation with
            | Observed -> configReads @ [ observedRead performancePath "measured baseline" ]
            | ReadFailed -> configReads @ [ unreadableRead performancePath ]
            | NotRead -> configReads
        let observed = { model with InterpretedEffects = reads }
        ViewGeneration.workModelSnapshots workId None None None None None None (Some evidence) observed

    [<Fact>]
    let ``selected work-model sources span config work and performance roots`` () =
        let paths = select Observed |> List.map _.Path
        Assert.Contains(".fsgg/project.yml", paths)
        Assert.Contains($"work/{workId}/evidence.yml", paths)
        Assert.Contains(performancePath, paths)
        Assert.Equal<string list>([ ".fsgg"; "readiness"; "work" ],
                                  paths |> List.map (fun path -> path.Split('/')[0]) |> List.distinct |> List.sort)

    [<Fact>]
    let ``declared performance artifact absent and unreadable both disappear from selected sources`` () =
        let absentPaths = select NotRead |> List.map _.Path
        let unreadablePaths = select ReadFailed |> List.map _.Path
        Assert.DoesNotContain(performancePath, absentPaths)
        Assert.Equal<string list>(absentPaths, unreadablePaths)
