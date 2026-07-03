namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
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
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeEvidencedProject root workId title
        root

    let runVerifyCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "verify"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 30000

        { ExitCode = exitCode
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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = verifyPath && change.Kind = "generatedView"
        )

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

    // §3.4 (FR-005, SC-004): a verify run over a current work model carries no
    // self-inflicted staleGeneratedView advisory and does not end SucceededWithWarnings
    // for that cause.
    [<Fact>]
    let ``verify on current work model carries no self-inflicted staleGeneratedView`` () =
        let root = initializedEvidencedProject ()

        let report = TestSupport.runVerify root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")
        Assert.NotEqual(CommandOutcome.SucceededWithWarnings, report.Outcome)

    // §3.4 genuine staleness (FR-007): editing an upstream authored source after the work
    // model was generated still flags staleGeneratedView — real staleness is not suppressed.
    [<Fact>]
    let ``verify still flags genuine upstream staleness`` () =
        let root = initializedEvidencedProject ()
        TestSupport.runVerify root workId title |> ignore

        let edited =
            (TestSupport.readRelative root specPath)
            + "\n\nAuthor edited the spec after generation.\n"

        TestSupport.writeRelative root specPath edited

        let report = TestSupport.runVerify root workId title

        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")

    // --- User Story 2: find blocking readiness gaps ---

    [<Fact>]
    let ``verify missing evidence blocks without verification write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "verify.missingEvidencePrerequisite")
        Assert.False(TestSupport.existsRelative root verifyPath)

    [<Fact>]
    let ``verify missing analysis blocks without verification write`` () =
        let root = TestSupport.tempDirectory ()
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
        let root = TestSupport.tempDirectory ()

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = verifyPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``verify rerun over unchanged sources reports no change`` () =
        let root = initializedEvidencedProject ()

        TestSupport.runVerify root workId title |> ignore
        let first = TestSupport.readRelative root verifyPath
        let rerun = TestSupport.runVerify root workId title
        let second = TestSupport.readRelative root verifyPath

        Assert.NotEqual(CommandOutcome.Blocked, rerun.Outcome)
        Assert.Equal(first, second)

        Assert.Contains(
            rerun.ChangedArtifacts,
            fun change -> change.Path = verifyPath && change.Operation = ArtifactOperation.NoChange
        )

    // --- User Story 4: keep verification output traceable ---

    [<Fact>]
    let ``verify does not require Governance files`` () =
        let root = initializedEvidencedProject ()

        let report = TestSupport.runVerify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.DoesNotContain(serializeReport report, "route")
        Assert.DoesNotContain(serializeReport report, "\"ship\"")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

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

    // --- User Story 1 scenario 2 (FR-008): the missing-required-skill obligation
    // re-keys to the declared framework, and supplying evidence clears it. ---

    let private declareTestFramework root (framework: string) =
        let projectYml = TestSupport.readRelative root ".fsgg/project.yml"

        let declared =
            projectYml.Replace("  defaultWorkRoot: work", $"  defaultWorkRoot: work\n  testFramework: {framework}")

        TestSupport.writeRelative root ".fsgg/project.yml" declared

    let private obligationTaskId (tasksYml: string) =
        let lines = tasksYml.Split('\n')

        let titleIndex =
            lines
            |> Array.findIndex (fun line -> line.Contains("Record verification evidence"))

        let idLine =
            lines.[..titleIndex]
            |> Array.rev
            |> Array.find (fun line -> line.Contains("- id: "))

        idLine.Substring(idLine.IndexOf("- id: ") + 6).Trim()

    // The generated obligation evidence id for task `T00n` is `EV00n` (same index),
    // so the synthetic evidence id must track the task number — otherwise a stray
    // id collides with another task's obligation and silently satisfies it.
    let private evidenceCovering (taskIds: string list) =
        let entries =
            taskIds
            |> List.map (fun taskId ->
                let evidenceId = "EV" + taskId.Substring(1)

                sprintf
                    "  - id: %s\n    kind: verification\n    subject:\n      type: task\n      id: %s\n    result: pass\n"
                    evidenceId
                    taskId)
            |> String.concat ""

        "schemaVersion: 1\nevidence:\n" + entries

    [<Fact>]
    let ``verify re-keys missing required skill to the declared framework`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        declareTestFramework root "expecto"
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore

        let tasksReport = TestSupport.runTasks root workId title
        let allTaskIds = tasksReport.Tasks.Value.TaskIds
        let obligationId = obligationTaskId (TestSupport.readRelative root tasksPath)

        // Evidence covers every task except the verification-obligation task, so
        // its required test skill is missing — keyed to `expecto`, never `xunit`.
        let withoutObligation = allTaskIds |> List.filter (fun id -> id <> obligationId)
        TestSupport.writeRelative root evidencePath (evidenceCovering withoutObligation)
        TestSupport.runAnalyze root workId title |> ignore
        let missingReport = TestSupport.runVerify root workId title

        let missingSkillDiagnostic =
            missingReport.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "evidence.missingRequiredSkill")

        Assert.Contains("expecto", missingSkillDiagnostic.RelatedIds)
        Assert.DoesNotContain("xunit", missingSkillDiagnostic.RelatedIds)

        // Supplying evidence that covers the verification-obligation task makes the
        // `expecto` skill visible and clears the obligation.
        TestSupport.writeRelative root evidencePath (evidenceCovering allTaskIds)
        TestSupport.runVerify root workId title |> ignore
        let clearedJson = TestSupport.readRelative root verifyPath

        Assert.Contains("\"skill\": \"expecto\"", clearedJson)
        Assert.DoesNotContain("evidence.missingRequiredSkill", clearedJson)
        Assert.DoesNotContain("xunit", clearedJson)
