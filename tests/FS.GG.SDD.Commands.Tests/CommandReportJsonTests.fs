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

    [<Fact>]
    let ``charter deterministic JSON is byte stable`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request = { TestSupport.charterRequest root "004-charter-command" "Charter Command" with DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"charter\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``specify deterministic JSON includes specification object`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore
        let request = { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"specify\"", first)
        Assert.Contains("\"specification\"", first)
        Assert.Contains("\"requirementIds\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)
