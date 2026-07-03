namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module EvidenceCommandTests =
    let workId = "011-evidence-command"
    let title = "Evidence Command"
    let evidencePath = $"work/{workId}/evidence.yml"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let workModelPath = $"readiness/{workId}/work-model.json"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedAnalyzedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        root

    let runEvidenceCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "evidence"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 30000

        { ExitCode = exitCode
          StdOut = stdout
          StdErr = stderr }

    let undisclosedSyntheticInput =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV999
    kind: synthetic
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    sourceRefs:
      - kind: transcript
        path: specs/011-evidence-command/readiness/synthetic-fsi.txt
        result: pass
    result: pass
    synthetic: true
"""

    [<Fact>]
    let ``evidence missingRequiredEvidence correction shows the non-synthetic pass form`` () =
        // FR-008: the surfaced unsatisfied-obligation diagnostic shows what satisfies it.
        let diagnostic =
            FS.GG.SDD.Commands.CommandReports.missingRequiredEvidence evidencePath [ "EV001" ]

        Assert.Equal("evidence.missingRequiredEvidence", diagnostic.Id)
        Assert.Contains("result: pass", diagnostic.Correction)
        Assert.Contains("synthetic: false", diagnostic.Correction)

    [<Fact>]
    let ``evidence creates authored evidence artifact with real filesystem evidence`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Contains("stage: evidence", evidence)
        Assert.Contains("status: evidenceReady", evidence)
        Assert.Contains($"sourceAnalysis: {analysisPath}", evidence)
        Assert.Contains("sourceSnapshots:", evidence)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.Kind = "authoredSource"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Equal(Some "evidence.next.verify", report.NextAction |> Option.map _.ActionId)

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> Assert.Equal("evidenceReady", artifact.Status)
        | Error diagnostics -> failwith $"Generated evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence missing analysis blocks without authored evidence write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title
        let before = TestSupport.readRelative root evidencePath

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.missingAnalysisPrerequisite")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence does not require Governance files`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.DoesNotContain(serializeReport report, "route")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``evidence dry run reports authored update without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``evidence blocks undisclosed synthetic evidence without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                InputText = Some undisclosedSyntheticInput }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.undisclosedSyntheticEvidence")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence deterministic JSON is byte stable`` () =
        let root = initializedAnalyzedProject ()

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"evidence\"", first)
        Assert.Contains("\"evidence\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``evidence text projection uses report facts`` () =
        let root = initializedAnalyzedProject ()
        let report = TestSupport.runEvidence root workId title
        let text = renderText report

        Assert.Contains("command: evidence", text)
        Assert.Contains($"evidencePath: {evidencePath}", text)
        Assert.Contains("evidenceReadiness: evidenceReady", text)
        Assert.Contains("nextAction: evidence.next.verify", text)

    [<Fact>]
    let ``evidence CLI JSON smoke creates evidence artifact`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"evidence.next.verify\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root evidencePath)

    [<Fact>]
    let ``evidence CLI dry run smoke avoids authored mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let result = runEvidenceCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence CLI text smoke renders human projection`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: evidence", result.StdOut)
        Assert.Contains("evidenceReadiness: evidenceReady", result.StdOut)
        Assert.Contains("nextAction: evidence.next.verify", result.StdOut)
        Assert.Equal("", result.StdErr)
