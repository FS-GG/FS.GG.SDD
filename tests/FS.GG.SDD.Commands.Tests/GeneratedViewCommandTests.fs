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
