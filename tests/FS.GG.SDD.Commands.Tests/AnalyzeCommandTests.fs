namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module AnalyzeCommandTests =
    let workId = "010-analyze-command"
    let title = "Analyze Command"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let tasksPath = $"work/{workId}/tasks.yml"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedTasksReadyProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title
        root

    let runAnalyzeCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "analyze"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 30000

        { ExitCode = exitCode
          StdOut = stdout
          StdErr = stderr }

    [<Fact>]
    let ``analyze creates generated analysis view with real filesystem evidence`` () =
        let root = initializedTasksReadyProject ()

        let report = TestSupport.runAnalyze root workId title
        let analysisJson = TestSupport.readRelative root analysisPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertAnalysisSummary report "implementationReady"
        Assert.True(TestSupport.existsRelative root analysisPath)
        Assert.Contains("\"stage\": \"analyze\"", analysisJson)
        Assert.Contains("\"sourceRelationships\"", analysisJson)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = analysisPath && change.Kind = "generatedView"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = analysisPath && view.Kind = "analysis")
        Assert.Equal(Some "analysis.next.implement", report.NextAction |> Option.map _.ActionId)

        match
            parseAnalysisView
                { Path = analysisPath
                  Text = analysisJson }
        with
        | Ok view -> Assert.Equal("implementationReady", view.Readiness.Status)
        | Error diagnostics -> failwith $"Generated analysis view did not parse: {diagnostics}."

    [<Fact>]
    let ``analyze does not require Governance files`` () =
        let root = initializedTasksReadyProject ()

        let report = TestSupport.runAnalyze root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.DoesNotContain(serializeReport report, "route")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``analyze missing tasks blocks without analysis write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializePlanReadyProject root workId title

        let report = TestSupport.runAnalyze root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingTasksPrerequisite")
        Assert.False(TestSupport.existsRelative root analysisPath)

    [<Fact>]
    let ``analyze dry run reports generated changes without mutation`` () =
        let root = initializedTasksReadyProject ()

        let request =
            { TestSupport.analyzeRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root analysisPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = analysisPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``analyze preserves authored task source`` () =
        let root = initializedTasksReadyProject ()
        let before = TestSupport.readRelative root tasksPath

        TestSupport.runAnalyze root workId title |> ignore

        Assert.Equal(before, TestSupport.readRelative root tasksPath)

    [<Fact>]
    let ``analyze deterministic JSON report is byte stable`` () =
        let root = initializedTasksReadyProject ()

        let request =
            { TestSupport.analyzeRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"analyze\"", first)
        Assert.Contains("\"analysis\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``analyze text projection uses report facts`` () =
        let root = initializedTasksReadyProject ()
        let report = TestSupport.runAnalyze root workId title
        let text = renderText report

        Assert.Contains("command: analyze", text)
        Assert.Contains("analysisPath:", text)
        Assert.Contains("analysisReadiness: implementationReady", text)
        Assert.Contains("nextAction: analysis.next.implement", text)

    [<Fact>]
    let ``analyze create and rerun complete under local harness budget`` () =
        let root = initializedTasksReadyProject ()

        let createWatch = Stopwatch.StartNew()
        let createReport = TestSupport.runAnalyze root workId title
        createWatch.Stop()

        let rerunWatch = Stopwatch.StartNew()
        let rerunReport = TestSupport.runAnalyze root workId title
        rerunWatch.Stop()

        Assert.NotEqual(CommandOutcome.Blocked, createReport.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, rerunReport.Outcome)
        Assert.True(createWatch.Elapsed.TotalSeconds < 2.0, $"Create took {createWatch.Elapsed}.")
        Assert.True(rerunWatch.Elapsed.TotalSeconds < 2.0, $"Rerun took {rerunWatch.Elapsed}.")

    [<Fact>]
    let ``analyze CLI JSON smoke creates analysis view`` () =
        let root = initializedTasksReadyProject ()

        let result = runAnalyzeCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"analyze\"", result.StdOut)
        Assert.Contains("\"analysis\"", result.StdOut)
        Assert.Contains("\"analysis.next.implement\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root analysisPath)

    [<Fact>]
    let ``analyze CLI dry run smoke avoids generated mutation`` () =
        let root = initializedTasksReadyProject ()

        let result = runAnalyzeCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"analyze\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.False(TestSupport.existsRelative root analysisPath)

    [<Fact>]
    let ``analyze CLI text smoke renders human projection`` () =
        let root = initializedTasksReadyProject ()

        let result = runAnalyzeCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: analyze", result.StdOut)
        Assert.Contains("analysisReadiness: implementationReady", result.StdOut)
        Assert.Contains("nextAction: analysis.next.implement", result.StdOut)
        Assert.Equal("", result.StdErr)
