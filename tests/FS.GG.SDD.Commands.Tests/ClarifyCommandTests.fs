namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module ClarifyCommandTests =
    let workId = "006-clarify-command"
    let title = "Clarify Command"
    let specPath = $"work/{workId}/spec.md"
    let clarificationPath = $"work/{workId}/clarifications.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedSpecifiedProject () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore

        let specifyRequest =
            { TestSupport.specifyRequest root workId title with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }

        TestSupport.runRequest specifyRequest |> ignore
        root

    let initializedSpecifiedProjectWithoutAmbiguity () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore
        root

    let runClarifyWith input root =
        let request =
            { TestSupport.clarifyRequest root workId title with
                InputText = input }

        TestSupport.runRequest request

    [<Fact>]
    let ``clarify creates authored clarification with real filesystem evidence`` () =
        let root = initializedSpecifiedProject ()

        let report = TestSupport.runClarify root workId title
        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: clarify", clarification)
        Assert.Contains("## Clarification Questions", clarification)
        Assert.Contains("CQ-001", clarification)
        Assert.Contains("DEC-001", clarification)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.Create)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath && view.Currency = GeneratedViewCurrency.Missing)
        Assert.Equal(Some Checklist, report.NextAction |> Option.bind _.Command)
        Assert.Equal(Some "CQ-001", report.Clarification |> Option.bind (fun summary -> summary.QuestionIds |> List.tryHead))
        Assert.Equal(Some "DEC-001", report.Clarification |> Option.bind (fun summary -> summary.DecisionIds |> List.tryHead))

    [<Fact>]
    let ``clarify creation does not require Governance files`` () =
        let root = initializedSpecifiedProject ()

        let report = TestSupport.runClarify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated")

    [<Fact>]
    let ``clarify handles specified work item with no open ambiguity`` () =
        let root = initializedSpecifiedProjectWithoutAmbiguity ()

        let report = runClarifyWith None root
        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("No clarification questions recorded.", clarification)
        Assert.Empty(report.Clarification.Value.QuestionIds)
        Assert.Empty(report.Clarification.Value.DecisionIds)
        Assert.Equal(Some Checklist, report.NextAction |> Option.bind _.Command)

    [<Fact>]
    let ``clarify rerun preserves authored content and stable decisions`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore
        let authored = TestSupport.readRelative root clarificationPath + "\nUser-authored clarification prose stays here.\n"
        TestSupport.writeRelative root clarificationPath authored

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root clarificationPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.NoChange)

    [<Fact>]
    let ``clarify safely appends missing standard sections`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore
        let partial = (TestSupport.readRelative root clarificationPath).Replace("## Accepted Deferrals\nNo accepted deferrals recorded.\n\n", "")
        TestSupport.writeRelative root clarificationPath partial

        let report = TestSupport.runClarify root workId title
        let after = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("## Accepted Deferrals", after)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.Update)

    [<Fact>]
    let ``clarify records accepted deferral as durable decision fact`` () =
        let root = initializedSpecifiedProject ()

        let report = runClarifyWith (Some "AMB-001 accepted deferral: Defer checklist output naming to the checklist feature.") root
        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("## Accepted Deferrals", clarification)
        Assert.Contains("DEC-001", clarification)
        Assert.Equal(Some "DEC-001", report.Clarification |> Option.bind (fun summary -> summary.AcceptedDeferralIds |> List.tryHead))
        Assert.Empty(report.Clarification.Value.DecisionIds)

    [<Fact>]
    let ``clarify missing answer blocks before authored write`` () =
        let root = initializedSpecifiedProject ()

        let report = runClarifyWith None root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationAnswer")
        Assert.False(TestSupport.existsRelative root clarificationPath)

    [<Fact>]
    let ``clarify unknown reference blocks before authored write`` () =
        let root = initializedSpecifiedProject ()

        let report = runClarifyWith (Some "AMB-999: Use an unknown ambiguity.") root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownClarificationReference")
        Assert.False(TestSupport.existsRelative root clarificationPath)

    [<Fact>]
    let ``clarify identity mismatch blocks before authored write`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore
        let original = (TestSupport.readRelative root clarificationPath).Replace($"workId: {workId}", "workId: 999-other-work")
        TestSupport.writeRelative root clarificationPath original

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "clarificationIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root clarificationPath)

    [<Fact>]
    let ``clarify unsafe decision change blocks without mutating existing clarification`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore
        let before = TestSupport.readRelative root clarificationPath

        let report = runClarifyWith (Some "AMB-001: Record the decision somewhere else.") root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeDecisionChange")
        Assert.Equal(before, TestSupport.readRelative root clarificationPath)

    [<Fact>]
    let ``clarify dry run reports proposed changes without mutation`` () =
        let root = initializedSpecifiedProject ()
        let request =
            { TestSupport.clarifyRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root clarificationPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = clarificationPath && change.SafeWriteDecision = "dryRunOnly")

    [<Fact>]
    let ``clarify refreshes generated work model when source data is valid`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.Sources |> List.exists (fun source -> source.Path = clarificationPath))

    [<Fact>]
    let ``clarify deterministic JSON is byte stable`` () =
        let root = initializedSpecifiedProject ()
        let request =
            { TestSupport.clarifyRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"clarify\"", first)
        Assert.Contains("\"clarification\"", first)
        Assert.DoesNotContain(root, first)
