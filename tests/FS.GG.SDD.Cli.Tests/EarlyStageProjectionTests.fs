namespace FS.GG.SDD.Cli.Tests

open System.IO
open Spectre.Console
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// T016 (US2): the navigable early-stage agents report projects faithfully through all
/// three surfaces — default/--json, --text, and --rich — with rich adding/dropping no
/// facts and degrading to zero ANSI when non-interactive.
module EarlyStageProjectionTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private earlyStageAgentsReport () =
        let root = Commands.tempDirectory ()
        Commands.initializeProject root
        Commands.runCharter root "054-early" "Early Shape" |> ignore
        Commands.runAgents root "054-early"

    let private renderRich (report: CommandReport) =
        let writer = new StringWriter()
        let settings = AnsiConsoleSettings()
        settings.Ansi <- AnsiSupport.No
        settings.ColorSystem <- ColorSystemSupport.NoColors
        settings.Out <- new AnsiConsoleOutput(writer)
        let console = AnsiConsole.Create settings
        console.Profile.Capabilities.Ansi <- false
        console.Profile.Width <- 120
        renderRichTo console report
        writer.ToString()

    [<Fact>]
    let ``early-stage agents report carries the advisory and pointer in JSON`` () =
        let json = serializeReport (earlyStageAgentsReport ())
        Assert.Contains("agents.earlyStageGuidance", json)
        Assert.Contains("early-stage", json)
        Assert.Contains("earlyStageGuidance", json)

    [<Fact>]
    let ``early-stage agents report surfaces the disposition and next action in text`` () =
        let text = renderText (earlyStageAgentsReport ())
        Assert.Contains("agentsDisposition: early-stage", text)
        Assert.Contains("nextAction: earlyStageGuidance", text)

    [<Fact>]
    let ``early-stage agents report renders rich with zero ANSI and the same facts`` () =
        let report = earlyStageAgentsReport ()
        let rich = renderRich report
        Assert.False(System.String.IsNullOrWhiteSpace rich)
        Assert.Contains(outcomeValue report.Outcome, rich)
        Assert.Contains("early-stage", rich)
        // Degrades to zero ANSI when non-interactive/color-disabled.
        Assert.False(rich |> Seq.exists (fun c -> c = '\u001b'), "Rich output must emit zero ANSI escapes when non-interactive.")
