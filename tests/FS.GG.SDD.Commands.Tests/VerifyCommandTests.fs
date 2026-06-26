namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module VerifyCommandTests =
    let workId = "012-verify-command"
    let title = "Verify Command"
    let verifyPath = $"readiness/{workId}/verify.json"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let evidencePath = $"work/{workId}/evidence.yml"
    let tasksPath = $"work/{workId}/tasks.yml"
    let specPath = $"work/{workId}/spec.md"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedEvidencedProject () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeEvidencedProject root workId title
        root

    let runVerifyCli root extraArgs =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.WorkingDirectory <- TestSupport.repoRoot
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.ArgumentList.Add("run")
        startInfo.ArgumentList.Add("--project")
        startInfo.ArgumentList.Add("src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj")
        startInfo.ArgumentList.Add("-c")
        startInfo.ArgumentList.Add("Release")
        startInfo.ArgumentList.Add("--no-build")
        startInfo.ArgumentList.Add("--")
        startInfo.ArgumentList.Add("verify")
        startInfo.ArgumentList.Add("--root")
        startInfo.ArgumentList.Add(root)
        startInfo.ArgumentList.Add("--work")
        startInfo.ArgumentList.Add(workId)

        extraArgs |> List.iter startInfo.ArgumentList.Add

        match Process.Start(startInfo) with
        | null -> failwith "Failed to start CLI process."
        | cliProcess ->
            use cliProcess = cliProcess
            let stdout = cliProcess.StandardOutput.ReadToEnd()
            let stderr = cliProcess.StandardError.ReadToEnd()

            if not (cliProcess.WaitForExit(30000)) then
                try
                    cliProcess.Kill(entireProcessTree = true)
                with _ ->
                    ()

                failwith "CLI smoke process timed out."

            { ExitCode = cliProcess.ExitCode
              StdOut = stdout
              StdErr = stderr }

    // --- User Story 1: verify evidence-ready work ---

    [<Fact>]
    let ``verify creates generated verification view with real filesystem evidence`` () =
        let root = initializedEvidencedProject ()

        let report = TestSupport.runVerify root workId title
        let verifyJson = TestSupport.readRelative root verifyPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertVerificationSummary report "verificationReady"
        Assert.True(TestSupport.existsRelative root verifyPath)
        Assert.Contains("\"stage\": \"verify\"", verifyJson)
        Assert.Contains("\"evidenceDispositions\"", verifyJson)
        Assert.Contains("\"testDispositions\"", verifyJson)
        Assert.Contains("\"skillVisibility\"", verifyJson)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = verifyPath && change.Kind = "generatedView")
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = verifyPath && view.Kind = "verification")
        Assert.Equal(Some "verify.next.ship", report.NextAction |> Option.map _.ActionId)

        match parseVerificationView { Path = verifyPath; Text = verifyJson } with
        | Ok view ->
            Assert.Equal("verificationReady", view.Readiness)
            Assert.Equal(workId, view.WorkId.Value)
        | Error diagnostics -> failwith $"Generated verification view did not parse: {diagnostics}."

    [<Fact>]
    let ``verify next action lists verify and work-model artifacts`` () =
        let root = initializedEvidencedProject ()

        let report = TestSupport.runVerify root workId title

        match report.NextAction with
        | Some action ->
            Assert.Equal("verify.next.ship", action.ActionId)
            Assert.Equal(None, action.Command)
            Assert.Contains(verifyPath, action.RequiredArtifacts)
            Assert.Contains(workModelPath, action.RequiredArtifacts)
        | None -> failwith "Expected a next action."

    [<Fact>]
    let ``verify report shape exposes verification summary`` () =
        let root = initializedEvidencedProject ()
        let report = TestSupport.runVerify root workId title
        let json = serializeReport report

        Assert.Contains("\"verification\"", json)
        Assert.Contains("\"changedArtifacts\"", json)
        Assert.Contains("\"generatedViews\"", json)
        Assert.Contains("\"diagnostics\"", json)
        Assert.Contains("\"governanceCompatibility\"", json)
        Assert.Contains("\"nextAction\"", json)

    // --- User Story 2: find blocking readiness gaps ---

    [<Fact>]
    let ``verify missing evidence blocks without verification write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeAnalyzedProject root workId title
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "verify.missingEvidencePrerequisite")
        Assert.False(TestSupport.existsRelative root verifyPath)

    [<Fact>]
    let ``verify missing analysis blocks without verification write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeTasksReadyProject root workId title

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.missingAnalysisPrerequisite")
        Assert.False(TestSupport.existsRelative root verifyPath)

    [<Fact>]
    let ``verify blocks malformed existing verification view without overwrite`` () =
        let root = initializedEvidencedProject ()
        TestSupport.writeRelative root verifyPath "{ not valid verify json"
        let before = TestSupport.readRelative root verifyPath

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "verify.malformedVerificationView")
        Assert.Equal(before, TestSupport.readRelative root verifyPath)

    [<Fact>]
    let ``verify outside project blocks`` () =
        let root = TestSupport.tempDirectory()

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "outsideProject")

    // --- User Story 3: preserve authored lifecycle sources ---

    [<Fact>]
    let ``verify preserves authored lifecycle sources`` () =
        let root = initializedEvidencedProject ()
        let specBefore = TestSupport.readRelative root specPath
        let tasksBefore = TestSupport.readRelative root tasksPath
        let evidenceBefore = TestSupport.readRelative root evidencePath

        TestSupport.runVerify root workId title |> ignore

        Assert.Equal(specBefore, TestSupport.readRelative root specPath)
        Assert.Equal(tasksBefore, TestSupport.readRelative root tasksPath)
        Assert.Equal(evidenceBefore, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``verify dry run reports generated change without mutation`` () =
        let root = initializedEvidencedProject ()
        let request =
            { TestSupport.verifyRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root verifyPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = verifyPath && change.SafeWriteDecision = "dryRunOnly")

    [<Fact>]
    let ``verify rerun over unchanged sources reports no change`` () =
        let root = initializedEvidencedProject ()

        TestSupport.runVerify root workId title |> ignore
        let first = TestSupport.readRelative root verifyPath
        let rerun = TestSupport.runVerify root workId title
        let second = TestSupport.readRelative root verifyPath

        Assert.NotEqual(CommandOutcome.Blocked, rerun.Outcome)
        Assert.Equal(first, second)
        Assert.Contains(rerun.ChangedArtifacts, fun change -> change.Path = verifyPath && change.Operation = ArtifactOperation.NoChange)

    // --- User Story 4: keep verification output traceable ---

    [<Fact>]
    let ``verify does not require Governance files`` () =
        let root = initializedEvidencedProject ()

        let report = TestSupport.runVerify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.DoesNotContain(serializeReport report, "route")
        Assert.DoesNotContain(serializeReport report, "\"ship\"")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated")

    [<Fact>]
    let ``verify deterministic JSON report is byte stable`` () =
        let root = initializedEvidencedProject ()
        let request =
            { TestSupport.verifyRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"verify\"", first)
        Assert.Contains("\"verification\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``verify text projection uses report facts`` () =
        let root = initializedEvidencedProject ()
        let report = TestSupport.runVerify root workId title
        let text = renderText report

        Assert.Contains("command: verify", text)
        Assert.Contains($"verifyPath: {verifyPath}", text)
        Assert.Contains("verificationReadiness: verificationReady", text)
        Assert.Contains("nextAction: verify.next.ship", text)

    [<Fact>]
    let ``verify create and rerun complete under local harness budget`` () =
        let root = initializedEvidencedProject ()

        let createWatch = Stopwatch.StartNew()
        let createReport = TestSupport.runVerify root workId title
        createWatch.Stop()

        let rerunWatch = Stopwatch.StartNew()
        let rerunReport = TestSupport.runVerify root workId title
        rerunWatch.Stop()

        Assert.NotEqual(CommandOutcome.Blocked, createReport.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, rerunReport.Outcome)
        Assert.True(createWatch.Elapsed.TotalSeconds < 2.0, $"Create took {createWatch.Elapsed}.")
        Assert.True(rerunWatch.Elapsed.TotalSeconds < 2.0, $"Rerun took {rerunWatch.Elapsed}.")

    [<Fact>]
    let ``verify CLI JSON smoke creates verification view`` () =
        let root = initializedEvidencedProject ()

        let result = runVerifyCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"verify\"", result.StdOut)
        Assert.Contains("\"verification\"", result.StdOut)
        Assert.Contains("\"verify.next.ship\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root verifyPath)

    [<Fact>]
    let ``verify CLI dry run smoke avoids generated mutation`` () =
        let root = initializedEvidencedProject ()

        let result = runVerifyCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"verify\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.False(TestSupport.existsRelative root verifyPath)

    [<Fact>]
    let ``verify CLI text smoke renders human projection`` () =
        let root = initializedEvidencedProject ()

        let result = runVerifyCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: verify", result.StdOut)
        Assert.Contains("verificationReadiness: verificationReady", result.StdOut)
        Assert.Contains("nextAction: verify.next.ship", result.StdOut)
        Assert.Equal("", result.StdErr)
