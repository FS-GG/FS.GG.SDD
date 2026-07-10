namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
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

    // --- Regression: FS.GG.SDD#197 — staleness is not divergence, and neither one gates ---
    //
    // Both targets are rendered from one NormalizedGuidanceModel and stamped with one recomputed digest,
    // so they are equivalent by construction. The old guard compared each target's recorded digest against
    // the recomputed one — i.e. staleness — and blocked, refusing the regeneration its own remediation
    // demanded. It also could not have worked as named: a recorded digest cannot distinguish an older
    // generation vintage from an out-of-band edit.

    /// Stale both targets the way an ordinary authored edit does: retitle a task, rebuild the model.
    let private retitleTaskAndRebuildWorkModel root =
        let tasksPath = $"work/{workId}/tasks.yml"
        let tasks = TestSupport.readRelative root tasksPath

        // Titles render bare under the codec's minimal quoting (FS.GG.SDD#260).
        let retitled =
            tasks.Replace("title: Implement requirement FR-001", "title: Implement the FR-001 requirement")

        Assert.True(
            tasks <> retitled,
            "Fixture drift: the generated tasks.yml no longer contains the FR-001 task title."
        )

        TestSupport.writeRelative root tasksPath retitled
        TestSupport.runVerify root workId title |> ignore

    let private behaviorDigestOf root target =
        (readManifest root target).BehaviorModelDigest.Value

    /// Corrupt a target's recorded behaviorModelDigest so it disagrees with the shared model.
    let private tamperBehaviorDigest root target =
        let guidance = $"readiness/{workId}/agent-commands/{target}/guidance.json"

        let tampered =
            System.Text.RegularExpressions.Regex.Replace(
                TestSupport.readRelative root guidance,
                "(\"behaviorModelDigest\": \\{\\s*\"algorithm\": \"sha256\",\\s*\"value\": \")[a-f0-9]{64}",
                "${1}" + System.String('a', 64)
            )

        TestSupport.writeRelative root guidance tampered

    // FS.GG.SDD#197: this used to assert `Blocked`. It no longer blocks, deliberately. A recorded digest
    // cannot tell "rendered from an older work model" from "edited out of band" — an interrupted `agents`
    // run leaves precisely this state — so divergence is reported and healed, never gated on.
    [<Fact>]
    let ``agents reports divergent existing guidance as an advisory and regenerates it`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        tamperBehaviorDigest root "codex"

        let report = TestSupport.runAgents root workId
        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)

        let divergence =
            report.Diagnostics |> List.filter (fun d -> d.Id = "agents.behaviorDivergence")

        Assert.Single divergence |> ignore
        Assert.Equal(FS.GG.SDD.Artifacts.Diagnostics.DiagnosticSeverity.DiagnosticWarning, divergence.Head.Severity)

        match report.AgentGuidance with
        | Some summary -> Assert.Contains("codex", summary.DivergentTargetIds)
        | None -> failwith "Expected agent-guidance summary."

        // Healed: the tampered manifest was rewritten and both targets agree again.
        Assert.Equal<string>(
            (readManifest root "claude").BehaviorModelDigest.Value,
            (readManifest root "codex").BehaviorModelDigest.Value
        )

        Assert.Equal(CommandOutcome.NoChange, (TestSupport.runAgents root workId).Outcome)

    // The state an interrupted `agents` run leaves: one target written at the new vintage, one still at
    // the old one. Nothing was modified outside the generator, and it must self-heal at exit 0.
    [<Fact>]
    let ``agents self-heals vintage skew between targets without blocking`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        let codexGuidance = $"{codexRoot}/guidance.json"
        let staleVintage = TestSupport.readRelative root codexGuidance

        retitleTaskAndRebuildWorkModel root
        TestSupport.runAgents root workId |> ignore // both targets advance to the new vintage
        TestSupport.writeRelative root codexGuidance staleVintage // ...but codex's write "never landed"

        let report = TestSupport.runAgents root workId
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)

        Assert.Equal<string>(
            (readManifest root "claude").BehaviorModelDigest.Value,
            (readManifest root "codex").BehaviorModelDigest.Value
        )

    // The JSON automation contract: a non-current view always names its own cause. `refresh` discards the
    // agents diagnostics and keeps only these ids, so an empty list leaves the author with nothing.
    [<Fact>]
    let ``agents attaches each view's cause to its generatedViews diagnosticIds`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        tamperBehaviorDigest root "codex"

        let report = TestSupport.runAgents root workId

        let idsFor target =
            report.GeneratedViews
            |> List.find (fun view -> view.Path.Contains($"/{target}/"))
            |> fun view -> view.DiagnosticIds

        Assert.Equal<string list>([ "agents.behaviorDivergence" ], idsFor "codex")
        Assert.Empty(idsFor "claude") // current: nothing to say

    // ADR-0002 Gap B, finding 4: guidance.json is a durable machine contract, so it must record its
    // own diagnostics — not hardcode an empty array while the command report carries real ones. A
    // clean first generation has nothing to say and stays byte-empty.
    [<Fact>]
    let ``a cleanly generated guidance manifest records an empty diagnostics array`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        Assert.Empty (readManifest root "claude").Diagnostics
        Assert.Empty (readManifest root "codex").Diagnostics

    // The durable guidance.json now carries the same cause the command projects into the view's
    // `diagnosticIds`: a regenerated divergent target records its divergence, an untouched current
    // target records nothing. Previously both were silently written empty.
    [<Fact>]
    let ``a regenerated view records its own diagnostics in the durable guidance manifest`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        tamperBehaviorDigest root "codex"

        TestSupport.runAgents root workId |> ignore

        let codexDiagnostics = (readManifest root "codex").Diagnostics
        Assert.Contains(codexDiagnostics, (fun d -> d.Id = "agents.behaviorDivergence"))
        Assert.Empty (readManifest root "claude").Diagnostics

    [<Fact>]
    let ``agents regenerates equally stale guidance on both targets instead of blocking`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        let staleClaude = behaviorDigestOf root "claude"
        let staleCodex = behaviorDigestOf root "codex"

        retitleTaskAndRebuildWorkModel root
        let report = TestSupport.runAgents root workId

        // Staleness regenerates. It does not block, and it is not divergence.
        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, (fun d -> d.Id = "agents.behaviorDivergence"))
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "agents.staleGeneratedGuidance"))

        match report.AgentGuidance with
        | Some summary -> Assert.Empty summary.DivergentTargetIds
        | None -> failwith "Expected agent-guidance summary."

        // Both targets were actually rewritten, and still agree with each other.
        let freshClaude = behaviorDigestOf root "claude"
        let freshCodex = behaviorDigestOf root "codex"
        Assert.NotEqual<string>(staleClaude, freshClaude)
        Assert.NotEqual<string>(staleCodex, freshCodex)
        Assert.Equal<string>(freshClaude, freshCodex)

    [<Fact>]
    let ``agents converges to no change after regenerating stale guidance`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        retitleTaskAndRebuildWorkModel root
        TestSupport.runAgents root workId |> ignore

        // The old guard never self-healed: a second run blocked exactly like the first.
        let report = TestSupport.runAgents root workId
        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        TestSupport.assertAgentGuidanceSummary report "agentGuidanceReady" "generated-current"

    [<Fact>]
    let ``refresh regenerates stale agent guidance rather than silently preserving it`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        let stale = behaviorDigestOf root "claude"

        retitleTaskAndRebuildWorkModel root
        TestSupport.runRefresh root workId |> ignore

        // On main, refresh reported `succeeded` while leaving this digest untouched.
        Assert.NotEqual<string>(stale, behaviorDigestOf root "claude")

    [<Fact>]
    let ``agents reports divergence once, without a redundant staleness warning`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore
        tamperBehaviorDigest root "codex"

        let report = TestSupport.runAgents root workId

        let divergence =
            report.Diagnostics |> List.filter (fun d -> d.Id = "agents.behaviorDivergence")

        // One condition, one diagnostic — previously 2x divergence + 2x staleness for one cause.
        Assert.Single divergence |> ignore
        Assert.DoesNotContain(report.Diagnostics, (fun d -> d.Id = "agents.staleGeneratedGuidance"))
        Assert.Equal<string list>([ "codex" ], divergence.Head.RelatedIds)

    [<Fact>]
    let ``agents names only the behavior-divergent target, not one merely stale beside it`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runAgents root workId |> ignore

        // claude: corrupt only its recorded SOURCE digest. Its behaviorModelDigest still matches the
        // shared model, so it is stale — not divergent. (`digest` first hit is sources[0].digest.)
        let claudeGuidance = $"{claudeRoot}/guidance.json"

        let claudeStale =
            System.Text.RegularExpressions
                .Regex("(\"digest\": \\{\\s*\"algorithm\": \"sha256\",\\s*\"value\": \")[a-f0-9]{64}")
                .Replace(TestSupport.readRelative root claudeGuidance, "${1}" + System.String('b', 64), 1)

        TestSupport.writeRelative root claudeGuidance claudeStale

        // codex: corrupt its behaviorModelDigest. This one really does diverge.
        tamperBehaviorDigest root "codex"

        let report = TestSupport.runAgents root workId
        Assert.Equal(0, exitCodeForReport report)

        let divergence =
            report.Diagnostics |> List.filter (fun d -> d.Id = "agents.behaviorDivergence")

        // Only codex diverges. Naming claude would accuse a target whose behavior digest is correct.
        Assert.Single divergence |> ignore
        Assert.Equal<string list>([ "codex" ], divergence.Head.RelatedIds)

        match report.AgentGuidance with
        | Some summary -> Assert.Equal<string list>([ "codex" ], summary.DivergentTargetIds)
        | None -> failwith "Expected agent-guidance summary."

        // claude is stale and still says so — it is not silenced by codex's divergence.
        let stale =
            report.Diagnostics
            |> List.filter (fun d -> d.Id = "agents.staleGeneratedGuidance")

        Assert.Single stale |> ignore
        Assert.Equal<string list>([ "claude" ], stale.Head.RelatedIds)

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

    [<Fact; Trait("tier", "slow")>]
    let ``agents CLI smoke generates guidance and exits zero`` () =
        let root = initializedVerifiedProject ()
        let exitCode, stdout, _ = runAgentsCli root []
        Assert.Equal(0, exitCode)
        Assert.Contains("agentGuidance", stdout)
        Assert.True(TestSupport.existsRelative root $"{claudeRoot}/guidance.json")

    [<Fact; Trait("tier", "slow")>]
    let ``agents CLI text smoke surfaces summary facts`` () =
        let root = initializedVerifiedProject ()
        let exitCode, stdout, _ = runAgentsCli root [ "--text" ]
        Assert.Equal(0, exitCode)
        Assert.Contains("agentsDisposition: generated-current", stdout)

    // FS.GG.SDD#197: the in-process report and the serialized CLI contract are different surfaces.
    // On main this exited 1 and wrote nothing after an ordinary authored edit.
    [<Fact; Trait("tier", "slow")>]
    let ``agents CLI smoke regenerates stale guidance and exits zero`` () =
        let root = initializedVerifiedProject ()
        runAgentsCli root [] |> ignore
        retitleTaskAndRebuildWorkModel root

        let exitCode, stdout, _ = runAgentsCli root []
        Assert.Equal(0, exitCode)
        Assert.DoesNotContain("agents.behaviorDivergence", stdout)

    // --- FS.GG.SDD#340: agentRootResolvesWithinProject must reject the SAME paths as the
    //     authoritative PathContainment.escapesRoot, including a mid-path `..` escape. ---

    let private withinProject workId raw =
        FS.GG.SDD.Commands.Internal.HandlersAgents.agentRootResolvesWithinProject workId raw

    [<Fact>]
    let ``agentRootResolvesWithinProject rejects a mid-path .. escape`` () =
        // On main this returned true ("within project") and the `..` was then resolved out of tree
        // by the downstream Path.Combine + GetFullPath.
        Assert.False(withinProject "014-agent-guidance" "foo/../../../etc")

    [<Fact>]
    let ``agentRootResolvesWithinProject rejects a rooted path`` () =
        Assert.False(withinProject "014-agent-guidance" "/etc/passwd")

    [<Fact>]
    let ``agentRootResolvesWithinProject rejects a leading .. escape`` () =
        Assert.False(withinProject "014-agent-guidance" "../escape")

    [<Fact>]
    let ``agentRootResolvesWithinProject rejects whitespace`` () =
        Assert.False(withinProject "014-agent-guidance" "   ")

    [<Fact>]
    let ``agentRootResolvesWithinProject accepts a contained relative root`` () =
        Assert.True(withinProject "014-agent-guidance" "readiness/014-agent-guidance/agent-commands/claude")

    [<Fact>]
    let ``agentRootResolvesWithinProject substitutes {workId} before the containment check`` () =
        Assert.True(withinProject "014-agent-guidance" "readiness/{workId}/agent-commands/claude")
        // A `..` that only appears after substitution is still rejected.
        Assert.False(withinProject ".." "readiness/{workId}/agent-commands/claude")
