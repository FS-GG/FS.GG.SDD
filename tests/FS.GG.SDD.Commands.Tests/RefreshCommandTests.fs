namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module RefreshCommandTests =
    let workId = "015-refresh-command"
    let title = "Generated-View Refresh"
    let summaryPath = $"readiness/{workId}/summary.md"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let verifyPath = $"readiness/{workId}/verify.json"
    let shipPath = $"readiness/{workId}/ship.json"
    let claudeGuidance = $"readiness/{workId}/agent-commands/claude/guidance.json"

    // A project whose structured SDD-owned views (work-model, analysis, verify, ship)
    // all exist and are mutually consistent.
    let shippedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        root

    // A project whose full SDD-owned generated-view set (incl. agent guidance and
    // summary) already exists and is current.
    let fullyGeneratedProject () =
        let root = shippedProject ()
        TestSupport.runAgents root workId |> ignore
        TestSupport.runRefresh root workId |> ignore
        root

    // --- User Story 1: orchestrated refresh of the structured views ---

    [<Fact>]
    let ``refresh regenerates the structured SDD-owned views from current sources`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        TestSupport.assertRefreshDisposition report "refreshed-current"
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        for path in [ workModelPath; analysisPath; verifyPath; shipPath; claudeGuidance ] do
            Assert.True(TestSupport.existsRelative root path, $"Expected generated view {path}.")

        // agent-commands/summary did not exist on a shipped project -> refreshed this run.
        match report.Refresh with
        | Some summary ->
            Assert.Contains("agent-commands", summary.RefreshedViewIds)
            Assert.Contains("summary", summary.RefreshedViewIds)
            // the structured views already existed and are current -> already-current.
            Assert.Contains("work-model", summary.AlreadyCurrentViewIds)
            Assert.Contains("analysis", summary.AlreadyCurrentViewIds)
            Assert.Contains("verify", summary.AlreadyCurrentViewIds)
            Assert.Contains("ship", summary.AlreadyCurrentViewIds)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh reports every applicable view current after the run`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        for view in [ "work-model"; "analysis"; "verify"; "ship"; "agent-commands"; "summary" ] do
            Assert.Equal("current", TestSupport.refreshViewState report view)

    [<Fact>]
    let ``refresh records generated views with sources and generator identity`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        let summaryView = report.GeneratedViews |> List.tryFind (fun v -> v.Path = summaryPath)
        match summaryView with
        | Some view ->
            Assert.Equal("summary", view.Kind)
            Assert.NotEmpty view.Sources
            Assert.True(view.Generator.IsSome, "Expected a generator identity on the summary view.")
        | None -> failwith "Expected a summary generated-view entry."

    [<Fact>]
    let ``refresh succeeds without governance installed`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.All(report.GovernanceCompatibility, (fun fact -> Assert.Equal("notEvaluated", fact.State)))

    [<Fact>]
    let ``refresh report exposes the refresh block and generated views`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.True(report.Refresh.IsSome, "Expected report.Refresh.")
        Assert.NotEmpty report.GeneratedViews
        let json = serializeReport report
        Assert.Contains("\"refresh\"", json)
        Assert.Contains("\"perViewState\"", json)

    // --- 033 US3: the seeded constitution behaves like authored content ---

    let private editedConstitution =
        "# Our Ratified Constitution\n\nProject-specific principles ratified by this team.\n"

    // T010 (US3-AC1 / FR-008): an author-edited .fsgg/constitution.md survives a re-init
    // under the same no-clobber policy as the other authored skeleton files; the report
    // records it refused, not overwritten.
    [<Fact>]
    let ``re-init preserves an author-edited constitution`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.request Init root |> TestSupport.runRequest |> ignore
        TestSupport.writeRelative root ".fsgg/constitution.md" editedConstitution

        let report = TestSupport.request Init root |> TestSupport.runRequest

        Assert.Equal(editedConstitution, TestSupport.readRelative root ".fsgg/constitution.md")
        let change =
            report.ChangedArtifacts
            |> List.tryFind (fun c -> c.Path = ".fsgg/constitution.md")
            |> Option.defaultWith (fun () -> failwith "Expected a changed-artifact entry for the constitution.")
        Assert.Equal(ArtifactOperation.Refuse, change.Operation)
        Assert.Equal("refused", change.SafeWriteDecision)

    // T010 (US3-AC2 / FR-009/SC-004): refresh leaves an author-modified constitution
    // byte-unchanged and never reports it as a generated view, a blocked view, or a
    // generatedProduct path.
    [<Fact>]
    let ``refresh leaves the author-modified constitution untouched`` () =
        let root = TestSupport.tempDirectory ()
        let wid = "033-constitution-demo"
        TestSupport.request Init root |> TestSupport.runRequest |> ignore
        TestSupport.writeValidWorkSources root wid "Constitution demo"
        TestSupport.writeRelative root ".fsgg/constitution.md" editedConstitution

        let report = TestSupport.runRefresh root wid

        Assert.Equal(editedConstitution, TestSupport.readRelative root ".fsgg/constitution.md")
        Assert.DoesNotContain(report.GeneratedViews, fun v -> v.Path = ".fsgg/constitution.md")
        match report.Refresh with
        | Some summary ->
            Assert.DoesNotContain(".fsgg/constitution.md", summary.RefreshedViewIds)
            Assert.DoesNotContain(".fsgg/constitution.md", summary.BlockedViewIds)
        | None -> failwith "Expected a refresh summary."

    // --- 049: early-stage navigable refresh (no work model yet) ---

    // An early-only fixture: a chartered work item with no work model and no authored
    // sources yet.
    let earlyStageProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        root

    // T012 (US2 / FR-005/006/011, SC-002): the pre-work-model state is a recognized,
    // navigable advisory — exit 0, refresh.earlyStageGuidance, a pointer NextAction, and
    // no view written — not a bare refresh.blockedUpstreamView dead end.
    [<Fact>]
    let ``refresh reports a navigable early-stage state when the work model is absent`` () =
        let root = earlyStageProject ()

        let report = TestSupport.runRefresh root workId

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.earlyStageGuidance"))
        Assert.DoesNotContain(report.Diagnostics, (fun d -> d.Id = "refresh.blockedUpstreamView"))
        TestSupport.assertRefreshDisposition report "early-stage"

        match report.NextAction with
        | Some action ->
            Assert.Equal("earlyStageGuidance", action.ActionId)
            Assert.Contains(".fsgg/early-stage-guidance.md", action.RequiredArtifacts)
        | None -> failwith "Expected an early-stage next action."

        // No view regenerated or written (FR-005/006/011).
        Assert.False(TestSupport.existsRelative root summaryPath)
        Assert.False(TestSupport.existsRelative root workModelPath)
        match report.Refresh with
        | Some summary -> Assert.Empty summary.RefreshedViewIds
        | None -> failwith "Expected a refresh summary."

    // T018 (US3 / SC-004): the early-stage refresh report is byte-identical across runs.
    [<Fact>]
    let ``refresh early-stage report is deterministic for identical state`` () =
        let root = earlyStageProject ()
        let first = serializeReport (TestSupport.runRefresh root workId)
        let second = serializeReport (TestSupport.runRefresh root workId)
        Assert.Equal(first, second)

    // --- User Story 2: detect stale / unrefreshable views ---

    [<Fact>]
    let ``refresh blocks views whose declared source is missing`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.missingSource"))
        Assert.Equal("blocked", TestSupport.refreshViewState report "work-model")

    [<Fact>]
    let ``refresh names the upstream view when a dependent view is blocked`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.blockedUpstreamView"))

    [<Fact>]
    let ``refresh refreshes a malformed existing generated view from current sources`` () =
        let root = shippedProject ()
        TestSupport.writeRelative root workModelPath "{ not valid json"

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.malformedGeneratedView"))
        // The malformed view is regenerated from current sources, not left malformed.
        Assert.Equal("current", TestSupport.refreshViewState report "work-model")

    // --- User Story 3: human-readable summary projection ---

    [<Fact>]
    let ``refresh renders a generated summary projection of the structured readiness data`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.True(TestSupport.existsRelative root summaryPath, "Expected summary.md.")
        let summaryText = TestSupport.readRelative root summaryPath
        Assert.Contains("GENERATED by fsgg-sdd refresh", summaryText)
        Assert.Contains("DO NOT EDIT", summaryText)
        Assert.Contains($"# Readiness Summary — {workId}", summaryText)

    [<Fact>]
    let ``refresh summary per-view table matches the report per-view state`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId
        let summaryText = TestSupport.readRelative root summaryPath

        match report.Refresh with
        | Some summary ->
            for (view, state) in summary.PerViewState do
                Assert.Contains($"| {view} | {state} |", summaryText)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh blocks the summary and records a diagnostic when structured inputs are unusable`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.unrenderableSummary"))
        Assert.Equal("blocked", TestSupport.refreshViewState report "summary")
        Assert.False(TestSupport.existsRelative root summaryPath, "Summary must not be written from unusable data.")

    // --- User Story 4: authored sources authoritative, refresh repeatable ---

    [<Fact>]
    let ``refresh preserves authored sources and hand-owned guidance files`` () =
        let root = shippedProject ()
        let preserved =
            [ "CLAUDE.md"; "AGENTS.md"; ".fsgg/agents.yml"; ".fsgg/project.yml"
              $"work/{workId}/spec.md"; $"work/{workId}/tasks.yml"; $"work/{workId}/evidence.yml" ]
        let before = preserved |> List.map (fun path -> path, TestSupport.readRelative root path)

        TestSupport.runRefresh root workId |> ignore

        for (path, text) in before do
            Assert.Equal(text, TestSupport.readRelative root path)

    [<Fact>]
    let ``refresh dry-run writes zero files but reports proposed changes`` () =
        let root = shippedProject ()
        let report = TestSupport.runRequest { TestSupport.refreshRequest root workId with DryRun = true }

        // Views that did not yet exist must not be created by a dry run.
        Assert.False(TestSupport.existsRelative root summaryPath)
        Assert.False(TestSupport.existsRelative root claudeGuidance)
        Assert.NotEmpty report.ChangedArtifacts
        // Every proposed create/update is reported as a dry-run-only change (no file mutated).
        let mutations =
            report.ChangedArtifacts
            |> List.filter (fun change -> change.Operation = ArtifactOperation.Create || change.Operation = ArtifactOperation.Update)
        Assert.NotEmpty mutations
        Assert.All(mutations, (fun change -> Assert.Equal("dryRunOnly", change.SafeWriteDecision)))

    [<Fact>]
    let ``refresh rerun over a current project reports no change`` () =
        let root = fullyGeneratedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        // Refresh always re-plans deterministic generated writes; on a current project
        // every one resolves to NoChange/Preserve (nothing is actually rewritten).
        Assert.All(
            report.ChangedArtifacts,
            (fun change ->
                Assert.True(
                    change.Operation = ArtifactOperation.NoChange || change.Operation = ArtifactOperation.Preserve,
                    $"Expected no mutation for {change.Path}, got {change.Operation}.")))
        TestSupport.assertRefreshDisposition report "refreshed-current"

    // --- User Story 5: determinism and traceability ---

    [<Fact>]
    let ``refresh produces a byte-identical report across repeated runs`` () =
        let root = fullyGeneratedProject ()
        let first = serializeReport (TestSupport.runRefresh root workId)
        let second = serializeReport (TestSupport.runRefresh root workId)
        Assert.Equal(first, second)

    [<Fact>]
    let ``refresh produces a byte-identical summary across repeated runs`` () =
        let root = fullyGeneratedProject ()
        let first = TestSupport.readRelative root summaryPath
        File.Delete(Path.Combine(root, summaryPath.Replace('/', Path.DirectorySeparatorChar)))
        TestSupport.runRefresh root workId |> ignore
        let second = TestSupport.readRelative root summaryPath
        Assert.Equal(first, second)

    [<Fact>]
    let ``refresh text projection surfaces refresh facts present in the report`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId
        let text = renderText report

        Assert.Contains("refreshDisposition: refreshed-current", text)
        Assert.Contains("refreshView.work-model: current", text)

    // --- CLI smoke (real host entry point) ---

    let runRefreshCli root extraArgs =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.WorkingDirectory <- TestSupport.repoRoot
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        [ "run"; "--project"; "src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj"; "-c"; "Release"; "--no-build"; "--"; "refresh"; "--root"; root; "--work"; workId ]
        |> List.iter startInfo.ArgumentList.Add
        extraArgs |> List.iter startInfo.ArgumentList.Add

        match Process.Start startInfo with
        | null -> failwith "Failed to start CLI process."
        | cliProcess ->
            use cliProcess = cliProcess
            let stdout = cliProcess.StandardOutput.ReadToEnd()
            let stderr = cliProcess.StandardError.ReadToEnd()
            cliProcess.WaitForExit(120000) |> ignore
            cliProcess.ExitCode, stdout, stderr

    [<Fact>]
    let ``refresh CLI smoke regenerates views and exits zero`` () =
        let root = shippedProject ()
        let exitCode, stdout, _ = runRefreshCli root []
        Assert.Equal(0, exitCode)
        Assert.Contains("refresh", stdout)
        Assert.True(TestSupport.existsRelative root summaryPath)

    [<Fact>]
    let ``refresh CLI text smoke surfaces refresh disposition`` () =
        let root = shippedProject ()
        let exitCode, stdout, _ = runRefreshCli root [ "--text" ]
        Assert.Equal(0, exitCode)
        Assert.Contains("refreshDisposition: refreshed-current", stdout)

    [<Fact>]
    let ``refresh CLI dry-run smoke writes nothing`` () =
        let root = shippedProject ()
        let exitCode, _, _ = runRefreshCli root [ "--dry-run" ]
        Assert.Equal(0, exitCode)
        Assert.False(TestSupport.existsRelative root summaryPath)
