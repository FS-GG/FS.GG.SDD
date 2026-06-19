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
