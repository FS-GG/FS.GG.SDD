namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module CommandReportJsonTests =
    let dryRunReport () =
        let root = Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "lifecycle-commands", "deterministic-report")
        let request = { TestSupport.request Init root with DryRun = true }
        let model, effects = init request

        interpretAll root true effects
        |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
        |> fun state -> update BuildReport state |> fst
        |> fun state -> state.Report.Value

    [<Fact>]
    let ``deterministic JSON excludes absolute project root`` () =
        let first = dryRunReport() |> serializeReport
        let second = dryRunReport() |> serializeReport

        Assert.Equal(first, second)
        Assert.Contains("\"projectRoot\": \".\"", first)
        Assert.DoesNotContain(TestSupport.repoRoot, first)
        Assert.DoesNotContain("timestamp", first)
