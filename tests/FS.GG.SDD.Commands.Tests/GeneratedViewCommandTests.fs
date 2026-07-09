namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open Xunit

module GeneratedViewCommandTests =
    let workId = "004-charter-command"
    let title = "Charter Command"
    let workModelPath = $"readiness/{workId}/work-model.json"

    [<Fact>]
    let ``charter reports missing generated work model when source data is incomplete`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runCharter root workId title

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Missing
                && view.DiagnosticIds = []
        )

        Assert.False(TestSupport.existsRelative root workModelPath)

    [<Fact>]
    let ``verify surfaces work-model diagnostics rather than silently skipping the write`` () =
        // Regression for FS.GG.SDD#191. At `verify` (where the work model is built) a blocking
        // work model with no work-model.json on disk used to drop the blocking reasons and
        // report succeeded with an empty diagnostics list while writing no view. It must
        // instead surface them — Constitution VIII: operationally significant events produce
        // actionable diagnostics.
        let verifyWorkId = "012-verify-command"
        let verifyTitle = "Verify Command"
        let verifyWorkModelPath = $"readiness/{verifyWorkId}/work-model.json"
        let tasksPath = $"work/{verifyWorkId}/tasks.yml"

        let root = TestSupport.tempDirectory ()
        TestSupport.initializeEvidencedProject root verifyWorkId verifyTitle

        // Point an authored task at an undeclared decision, so the derived work model now
        // blocks on `unknownReference`/`workModelInconsistent` while `verify`'s own
        // command-level source-reference check (which covers requirement/source ids but not
        // `decisions:`) still passes — the exact shape of the shipped-example silent skip.
        // Then remove the clean work-model.json the setup wrote, reproducing "blocking model,
        // no view on disk".
        let tasks = TestSupport.readRelative root tasksPath
        TestSupport.writeRelative root tasksPath (tasks.Replace("decisions: []", "decisions: [DEC-999]"))

        System.IO.File.Delete(
            System.IO.Path.Combine(root, verifyWorkModelPath.Replace('/', System.IO.Path.DirectorySeparatorChar))
        )

        Assert.False(TestSupport.existsRelative root verifyWorkModelPath)

        let report = TestSupport.runVerify root verifyWorkId verifyTitle

        // The blocking reason itself must travel with the surfaced diagnostic, not just a
        // generic "blocked" marker — the author needs to know *what* to fix.
        Assert.Contains(
            report.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "workModelNotGenerated"
                && diagnostic.RelatedIds |> List.contains "workModelInconsistent"
        )

        Assert.NotEqual(CommandOutcome.Succeeded, report.Outcome)
        Assert.NotEqual(CommandOutcome.NoChange, report.Outcome)
        Assert.False(TestSupport.existsRelative root verifyWorkModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = verifyWorkModelPath
                && view.Currency = GeneratedViewCurrency.Missing
                && view.DiagnosticIds |> List.contains "workModelNotGenerated"
        )

    [<Fact>]
    let ``authoring stages stay silent when the work model blocks and no view exists`` () =
        // The complement of the verify test: at a pre-verify authoring stage the work model is
        // a side view, so a blocking model with no view on disk is reported through the view
        // state (Missing) but owes no diagnostic — surfacing the always-on authoring-stage case
        // awaits the reference-resolution work in FS.GG.SDD#204.
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root $"work/{workId}/spec.md" (TestSupport.validSpec workId title)

        TestSupport.writeRelative
            root
            $"work/{workId}/tasks.yml"
            """schemaVersion: 1
tasks:
  - id: T001
    title: Implement selected lifecycle work
    status: pending
    owner: sdd
    dependencies: []
    requirements: [FR-404]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

        TestSupport.writeRelative root $"work/{workId}/evidence.yml" TestSupport.validEvidence

        let report = TestSupport.runCharter root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "workModelNotGenerated")
        Assert.False(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view -> view.Path = workModelPath && view.Currency = GeneratedViewCurrency.Missing
        )

    [<Fact>]
    let ``charter refreshes generated work model when source data is valid`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.OutputDigest.IsSome
                && view.Sources
                   |> List.exists (fun source -> source.Path = $"work/{workId}/charter.md")
        )

    [<Fact>]
    let ``charter reports malformed generated work model before refresh`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title
        TestSupport.writeRelative root workModelPath "{ malformed json"

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedGeneratedView")

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.DiagnosticIds |> List.contains "malformedGeneratedView"
        )

        Assert.Contains("\"workId\": \"004-charter-command\"", TestSupport.readRelative root workModelPath)

    [<Fact>]
    let ``specify reports missing generated work model when later sources are incomplete`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore

        let report = TestSupport.runSpecify root "005-specify-command" "Specify Command"

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/005-specify-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Missing
                && view.Sources
                   |> List.exists (fun source -> source.Path = "work/005-specify-command/spec.md")
        )

    [<Fact>]
    let ``specify refreshes generated work model and reports malformed existing view`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore
        TestSupport.writeValidTasksAndEvidence root
        TestSupport.writeRelative root "readiness/005-specify-command/work-model.json" "{ malformed json"

        let report = TestSupport.runSpecify root "005-specify-command" "Specify Command"

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedGeneratedView")

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/005-specify-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Current
                && view.DiagnosticIds |> List.contains "malformedGeneratedView"
        )

    [<Fact>]
    let ``clarify reports missing generated work model when later sources are incomplete`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "006-clarify-command" "Clarify Command" |> ignore

        TestSupport.runRequest
            { TestSupport.specifyRequest root "006-clarify-command" "Clarify Command" with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }
        |> ignore

        let report = TestSupport.runClarify root "006-clarify-command" "Clarify Command"

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/006-clarify-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Missing
                && view.Sources
                   |> List.exists (fun source -> source.Path = "work/006-clarify-command/clarifications.md")
        )

    [<Fact>]
    let ``clarify refreshes generated work model and reports malformed existing view`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "006-clarify-command" "Clarify Command" |> ignore

        TestSupport.runRequest
            { TestSupport.specifyRequest root "006-clarify-command" "Clarify Command" with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }
        |> ignore

        TestSupport.writeValidTasksAndEvidenceFor root "006-clarify-command"
        TestSupport.writeRelative root "readiness/006-clarify-command/work-model.json" "{ malformed json"

        let report = TestSupport.runClarify root "006-clarify-command" "Clarify Command"

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedGeneratedView")

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/006-clarify-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Current
                && view.DiagnosticIds |> List.contains "malformedGeneratedView"
        )

    [<Fact>]
    let ``checklist reports missing generated work model when later sources are incomplete`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.runCharter root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runSpecify root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root "007-checklist-command" "Checklist Command" with
                InputText = None }
        |> ignore

        let report =
            TestSupport.runChecklist root "007-checklist-command" "Checklist Command"

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/007-checklist-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Missing
                && view.Sources
                   |> List.exists (fun source -> source.Path = "work/007-checklist-command/checklist.md")
        )

    [<Fact>]
    let ``checklist refreshes generated work model and reports malformed existing view`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.runCharter root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runSpecify root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root "007-checklist-command" "Checklist Command" with
                InputText = None }
        |> ignore

        TestSupport.writeValidTasksAndEvidenceFor root "007-checklist-command"
        TestSupport.writeRelative root "readiness/007-checklist-command/work-model.json" "{ malformed json"

        let report =
            TestSupport.runChecklist root "007-checklist-command" "Checklist Command"

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedGeneratedView")

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/007-checklist-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Current
                && view.DiagnosticIds |> List.contains "malformedGeneratedView"
        )

    [<Fact>]
    let ``plan reports generated work model state and includes plan source`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "008-plan-command" "Plan Command" |> ignore
        TestSupport.runSpecify root "008-plan-command" "Plan Command" |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root "008-plan-command" "Plan Command" with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root "008-plan-command" "Plan Command" |> ignore
        TestSupport.writeValidTasksAndEvidenceFor root "008-plan-command"

        let report = TestSupport.runPlan root "008-plan-command" "Plan Command"

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/008-plan-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources
                   |> List.exists (fun source -> source.Path = "work/008-plan-command/plan.md")
        )

    [<Fact>]
    let ``tasks refreshes generated work model and includes task source`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializePlanReadyProject root "009-tasks-command" "Tasks Command"
        TestSupport.writePassingTaskEvidenceFor root "009-tasks-command"

        let report = TestSupport.runTasks root "009-tasks-command" "Tasks Command"

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = "readiness/009-tasks-command/work-model.json"
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources
                   |> List.exists (fun source -> source.Path = "work/009-tasks-command/tasks.yml")
        )
