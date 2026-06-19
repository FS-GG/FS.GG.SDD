namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module TextProjectionTests =
    [<Fact>]
    let ``text projection summarizes report facts only`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.request Init root with DryRun = true; OutputFormat = Text }
        let model, effects = init request

        let report =
            interpretAll root true effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
            |> fun state -> update BuildReport state |> fst
            |> fun state -> state.Report.Value

        let text = renderText report

        Assert.Contains("command: init", text)
        Assert.Contains("outcome: succeeded", text)
        Assert.Contains($"changedArtifacts: {List.length report.ChangedArtifacts}", text)

    [<Fact>]
    let ``charter text projection summarizes report facts only`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request =
            { TestSupport.charterRequest root "004-charter-command" "Charter Command" with
                DryRun = true
                OutputFormat = Text }

        let report = TestSupport.runRequest request
        let text = renderText report

        Assert.Contains("command: charter", text)
        Assert.Contains($"outcome: {outcomeValue report.Outcome}", text)
        Assert.Contains($"changedArtifacts: {List.length report.ChangedArtifacts}", text)
        Assert.Contains($"generatedViews: {List.length report.GeneratedViews}", text)
        Assert.Contains($"diagnostics: {List.length report.Diagnostics}", text)

    [<Fact>]
    let ``specify text projection includes specification counts from report`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore
        let request =
            { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with
                DryRun = true
                OutputFormat = Text }

        let report = TestSupport.runRequest request
        let text = renderText report
        let specification = report.Specification.Value

        Assert.Contains("command: specify", text)
        Assert.Contains($"outcome: {outcomeValue report.Outcome}", text)
        Assert.Contains($"specificationRequirements: {List.length specification.RequirementIds}", text)
        Assert.Contains($"unresolvedAmbiguities: {specification.UnresolvedAmbiguityCount}", text)

    [<Fact>]
    let ``clarify text projection includes clarification counts from report`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root "006-clarify-command" "Clarify Command" |> ignore
        TestSupport.runRequest { TestSupport.specifyRequest root "006-clarify-command" "Clarify Command" with InputText = Some TestSupport.specifyIntentWithAmbiguity } |> ignore
        let request =
            { TestSupport.clarifyRequest root "006-clarify-command" "Clarify Command" with
                DryRun = true
                OutputFormat = Text }

        let report = TestSupport.runRequest request
        let text = renderText report
        let clarification = report.Clarification.Value

        Assert.Contains("command: clarify", text)
        Assert.Contains($"outcome: {outcomeValue report.Outcome}", text)
        Assert.Contains($"clarificationQuestions: {List.length clarification.QuestionIds}", text)
        Assert.Contains($"clarificationDecisions: {List.length clarification.DecisionIds}", text)
        Assert.Contains($"remainingAmbiguities: {clarification.RemainingAmbiguityCount}", text)
