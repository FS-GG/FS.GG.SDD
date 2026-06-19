namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open Xunit

module GeneratedViewCommandTests =
    let workId = "004-charter-command"
    let title = "Charter Command"
    let workModelPath = $"readiness/{workId}/work-model.json"

    [<Fact>]
    let ``charter reports missing generated work model when source data is incomplete`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root

        let report = TestSupport.runCharter root workId title

        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Missing
            && view.DiagnosticIds = [])
        Assert.False(TestSupport.existsRelative root workModelPath)

    [<Fact>]
    let ``charter refreshes generated work model when source data is valid`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.OutputDigest.IsSome
            && view.Sources |> List.exists (fun source -> source.Path = $"work/{workId}/charter.md"))

    [<Fact>]
    let ``charter reports malformed generated work model before refresh`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title
        TestSupport.writeRelative root workModelPath "{ malformed json"

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedGeneratedView")
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.DiagnosticIds |> List.contains "malformedGeneratedView")
        Assert.Contains("\"workId\": \"004-charter-command\"", TestSupport.readRelative root workModelPath)
