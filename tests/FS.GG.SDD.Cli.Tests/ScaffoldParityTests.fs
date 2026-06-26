namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// SC-006 / Scenario E: the scaffold report is fact-identical across `--json`,
/// `--text`, and `--rich`; `--rich` redirected equals `--text`; and the rich path
/// changes no JSON byte. Built from a constructed report (no template engine).
module ScaffoldParityTests =
    let private interactiveColor = { IsInteractive = true; ColorEnabled = true; Width = Some 100 }
    let private nonInteractive = { interactiveColor with IsInteractive = false }

    let private scaffoldSummary: ScaffoldSummary =
        { ProviderName = Some "fixture"
          ProviderContractVersion = Some "1.0.0"
          Outcome = "providerSucceeded"
          SkeletonCreated = true
          ProviderInvoked = true
          ProducedPathCount = 2
          ProducedPaths = [ "App.fsproj"; "Program.fs" ]
          NextActionHint = "SDD skeleton ready; begin the lifecycle at charter." }

    let private report: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Scaffold
            Outcome = CommandOutcome.Succeeded
            Specification = None
            Scaffold = Some scaffoldSummary }

    [<Fact>]
    let ``scaffold json projection equals serializeReport and the rich path changes no byte`` () =
        let before = serializeReport report
        resolve Rich interactiveColor report |> ignore
        let after = serializeReport report
        Assert.Equal(before, after)
        Assert.Equal(serializeReport report, (resolve Json interactiveColor report).Text)

    [<Fact>]
    let ``scaffold rich redirected equals the text projection`` () =
        let result = resolve Rich nonInteractive report
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText report, result.Text)

    [<Fact>]
    let ``scaffold facts appear in every projection`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich interactiveColor report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("providerSucceeded", projection)

        Assert.Contains("\"producedPaths\"", json)
        Assert.Contains("scaffoldProducedPath: App.fsproj", text)
        Assert.Contains("App.fsproj", rich)
