namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module AgentsCommandTests =
    let workId = "014-agent-guidance"
    let title = "Agent Guidance"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let claudeRoot = $"readiness/{workId}/agent-commands/claude"
    let codexRoot = $"readiness/{workId}/agent-commands/codex"

    let initializedVerifiedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        root

    let readManifest root target =
        let path = $"readiness/{workId}/agent-commands/{target}/guidance.json"

        match
            parseGeneratedAgentGuidance
                { Path = path
                  Text = TestSupport.readRelative root path }
        with
        | Ok manifest -> manifest
        | Error diagnostics -> failwith $"Expected a well-formed {target} manifest, got: {diagnostics}"

    // --- User Story 1: generate per-target guidance from the work model ---

    [<Fact>]
    let ``agents generates per-target guidance with real filesystem evidence`` () =
        let root = initializedVerifiedProject ()
        let report = TestSupport.runAgents root workId

        TestSupport.assertAgentGuidanceSummary report "agentGuidanceReady" "generated-current"

        for target in [ "claude"; "codex" ] do
            for file in [ "guidance.json"; "commands.md"; "skills.md" ] do
                Assert.True(
                    TestSupport.existsRelative root $"readiness/{workId}/agent-commands/{target}/{file}",
                    $"Expected generated {target}/{file}."
                )

        match report.AgentGuidance with
        | Some summary ->
            Assert.Equal<string list>([ "claude"; "codex" ], summary.GeneratedTargetIds)
            Assert.Empty summary.DivergentTargetIds
            Assert.True summary.EquivalenceRequired
            Assert.Equal(1, summary.SourceSnapshotCount)
        | None -> failwith "Expected agent-guidance summary."

        match report.NextAction with
        | Some action ->
            Assert.Equal("agentsGenerated", action.ActionId)
            Assert.Equal(None, action.Command)
        | None -> failwith "Expected next action."

    [<Fact>]
    let ``agents marks generated manifests with sources and generator identity`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        let manifest = readManifest root "claude"
        Assert.True manifest.Generated
        Assert.Equal(workId, manifest.WorkId.Value)
        Assert.Equal("claude", manifest.TargetId)
        Assert.NotEmpty manifest.Sources
        Assert.Contains(manifest.Sources, (fun source -> source.Path = workModelPath))
        Assert.NotEmpty manifest.Commands

    [<Fact>]
    let ``agents derives equal behavior digest across claude and codex`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        let claude = readManifest root "claude"
        let codex = readManifest root "codex"
        Assert.Equal(claude.BehaviorModelDigest.Value, codex.BehaviorModelDigest.Value)

    [<Fact>]
    let ``agents succeeds without governance installed`` () =
        let root = initializedVerifiedProject ()
        let report = TestSupport.runAgents root workId

        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        // Governance compatibility facts are advisory and not evaluated.
        Assert.All(report.GovernanceCompatibility, (fun fact -> Assert.Equal("notEvaluated", fact.State)))

    // --- User Story 2: detect missing / malformed / divergent ---

    // An early-only fixture: a chartered work item with no work model yet.
    let earlyStageProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        root

    // T010 (US2 / FR-004/006/008/011, SC-002): an *absent* work model at an early stage is
    // a navigable advisory, not a dead-end block — exit 0, the early-stage label, best-effort
    // facts (which artifacts exist), a pointer NextAction, and zero digest-stamped views.
    [<Fact>]
    let ``agents emits navigable early-stage guidance when the work model is absent`` () =
        let root = earlyStageProject ()

        let report = TestSupport.runAgents root workId

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "agents.earlyStageGuidance"))
        Assert.DoesNotContain(report.Diagnostics, (fun d -> d.Id = "agents.missingWorkModel"))
        TestSupport.assertAgentGuidanceSummary report "agentGuidanceEarlyStage" "early-stage"

        // Best-effort facts derived only from artifacts that exist: charter is present.
        let early =
            report.Diagnostics |> List.find (fun d -> d.Id = "agents.earlyStageGuidance")

        Assert.Contains("charter", early.RelatedIds)
        Assert.DoesNotContain("specify", early.RelatedIds)

        // A pointer NextAction routing to the seeded static guidance + next authoring command.
        match report.NextAction with
        | Some action ->
            Assert.Equal("earlyStageGuidance", action.ActionId)
            Assert.Equal(Some Specify, action.Command)
            Assert.Contains(".fsgg/early-stage-guidance.md", action.RequiredArtifacts)
        | None -> failwith "Expected an early-stage next action."

        // No digest-stamped view written (FR-008/FR-011).
        Assert.False(TestSupport.existsRelative root $"{claudeRoot}/guidance.json")
        Assert.False(TestSupport.existsRelative root $"{codexRoot}/guidance.json")

    [<Fact>]
    let ``agents blocks on malformed work model`` () =
        let root = initializedVerifiedProject ()
        TestSupport.writeRelative root workModelPath "{ not valid json"

        let report = TestSupport.runAgents root workId
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "agents.malformedWorkModel"))

    [<Fact>]
    let ``agents blocks divergent existing guidance when equivalence is required`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        // Corrupt the codex behavior digest so it diverges from the shared model.
        let codexGuidance = $"{codexRoot}/guidance.json"
        let original = TestSupport.readRelative root codexGuidance

        let tampered =
            System.Text.RegularExpressions.Regex.Replace(
                original,
                "(\"behaviorModelDigest\": \\{\\s*\"algorithm\": \"sha256\",\\s*\"value\": \")[a-f0-9]{64}",
                "${1}" + System.String('a', 64)
            )

        TestSupport.writeRelative root codexGuidance tampered

        let report = TestSupport.runAgents root workId
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "agents.behaviorDivergence"))

        match report.AgentGuidance with
        | Some summary -> Assert.Contains("codex", summary.DivergentTargetIds)
        | None -> failwith "Expected agent-guidance summary."

    [<Fact>]
    let ``agents refuses malformed existing guidance manifest`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        TestSupport.writeRelative root $"{claudeRoot}/guidance.json" "{ not json"

        let report = TestSupport.runAgents root workId
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "agents.malformedGeneratedGuidance"))

        match report.AgentGuidance with
        | Some summary -> Assert.Contains("claude", summary.RefusedTargetIds)
        | None -> failwith "Expected agent-guidance summary."

    // --- User Story 3: preservation and dry-run ---

    [<Fact>]
    let ``agents preserves authored sources and hand-owned guidance files`` () =
        let root = initializedVerifiedProject ()

        let preserved =
            [ "CLAUDE.md"
              "AGENTS.md"
              ".fsgg/agents.yml"
              $"work/{workId}/spec.md"
              $"work/{workId}/tasks.yml" ]

        let before =
            preserved |> List.map (fun path -> path, TestSupport.readRelative root path)

        TestSupport.runAgents root workId |> ignore

        for (path, text) in before do
            Assert.Equal(text, TestSupport.readRelative root path)

    [<Fact>]
    let ``agents dry-run writes zero files but reports proposed changes`` () =
        let root = initializedVerifiedProject ()

        let report =
            TestSupport.runRequest
                { TestSupport.agentsRequest root workId with
                    DryRun = true }

        Assert.False(TestSupport.existsRelative root $"{claudeRoot}/guidance.json")
        Assert.False(TestSupport.existsRelative root $"{codexRoot}/guidance.json")
        Assert.NotEmpty report.ChangedArtifacts
        Assert.All(report.ChangedArtifacts, (fun change -> Assert.Equal("dryRunOnly", change.SafeWriteDecision)))

    [<Fact>]
    let ``agents rerun over unchanged work model reports no change`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        let report = TestSupport.runAgents root workId

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Empty report.ChangedArtifacts
        TestSupport.assertAgentGuidanceSummary report "agentGuidanceReady" "generated-current"

    // --- User Story 4: determinism and traceability ---

    [<Fact>]
    let ``agents produces byte-identical manifests across regenerated runs`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        let first = TestSupport.readRelative root $"{claudeRoot}/guidance.json"

        Directory.Delete(Path.Combine(root, claudeRoot.Replace('/', Path.DirectorySeparatorChar)), true)
        Directory.Delete(Path.Combine(root, codexRoot.Replace('/', Path.DirectorySeparatorChar)), true)
        TestSupport.runAgents root workId |> ignore
        let second = TestSupport.readRelative root $"{claudeRoot}/guidance.json"

        Assert.Equal(first, second)

    // T018 (US3 / SC-004): the early-stage report is byte-identical across repeated runs.
    [<Fact>]
    let ``agents early-stage report is deterministic for identical state`` () =
        let root = earlyStageProject ()
        let first = serializeReport (TestSupport.runAgents root workId)
        let second = serializeReport (TestSupport.runAgents root workId)
        Assert.Equal(first, second)

    [<Fact>]
    let ``agents report JSON is deterministic for identical state`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        let first = serializeReport (TestSupport.runAgents root workId)
        let second = serializeReport (TestSupport.runAgents root workId)
        Assert.Equal(first, second)

    // --- CLI smoke (real host entry point) ---

    let runAgentsCli root extraArgs =
        [ "agents"; "--root"; root; "--work"; workId ] @ extraArgs
        |> TestSupport.runCliRaw 120000

    [<Fact>]
    let ``agents CLI smoke generates guidance and exits zero`` () =
        let root = initializedVerifiedProject ()
        let exitCode, stdout, _ = runAgentsCli root []
        Assert.Equal(0, exitCode)
        Assert.Contains("agentGuidance", stdout)
        Assert.True(TestSupport.existsRelative root $"{claudeRoot}/guidance.json")

    [<Fact>]
    let ``agents CLI text smoke surfaces summary facts`` () =
        let root = initializedVerifiedProject ()
        let exitCode, stdout, _ = runAgentsCli root [ "--text" ]
        Assert.Equal(0, exitCode)
        Assert.Contains("agentsDisposition: generated-current", stdout)
