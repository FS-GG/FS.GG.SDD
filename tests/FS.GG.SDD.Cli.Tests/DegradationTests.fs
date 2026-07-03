namespace FS.GG.SDD.Cli.Tests

open Spectre.Console
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

module DegradationTests =
    /// The ANSI escape (ESC, 0x1B) that must never appear in degraded output.
    let escChar = char 0x1b

    let interactiveColor =
        { IsInteractive = true
          ColorEnabled = true
          Width = Some 100
          IsInputInteractive = true }

    let nonInteractive =
        { interactiveColor with
            IsInteractive = false }

    let colorDisabled =
        { interactiveColor with
            ColorEnabled = false }

    let sample = RichRenderingTests.sampleReport

    [<Fact>]
    let ``T014 resolve never mutates the report or its JSON`` () =
        let before = serializeReport sample
        resolve Json interactiveColor sample |> ignore
        resolve Text interactiveColor sample |> ignore
        resolve Rich interactiveColor sample |> ignore
        let after = serializeReport sample
        Assert.Equal(before, after)

    [<Fact>]
    let ``T014 resolve Json returns the JSON projection`` () =
        let result = resolve Json interactiveColor sample
        Assert.Equal(serializeReport sample, result.Text)
        Assert.False(result.UsedRichRendering)

    [<Fact>]
    let ``T014 resolve Text returns the plain-text projection`` () =
        let result = resolve Text nonInteractive sample
        Assert.Equal(renderText sample, result.Text)
        Assert.False(result.UsedRichRendering)

    [<Fact>]
    let ``T015 Rich degrades to plain text when non-interactive`` () =
        let result = resolve Rich nonInteractive sample
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText sample, result.Text)
        Assert.False((result.Text).Contains escChar)

    [<Fact>]
    let ``T015 Rich degrades to plain text when color disabled`` () =
        let result = resolve Rich colorDisabled sample
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText sample, result.Text)
        Assert.False((result.Text).Contains escChar)

    [<Fact>]
    let ``T015 Rich renders richly when interactive and color enabled`` () =
        let result = resolve Rich interactiveColor sample
        Assert.True(result.UsedRichRendering)
        Assert.Contains(outcomeValue sample.Outcome, result.Text)

    [<Fact>]
    let ``T015 renderRichTo to a color-off console emits zero ANSI`` () =
        let text = RichRenderingTests.render sample
        Assert.False((text).Contains escChar)

    // ----- Stream/exit parity (T016): routing depends only on Outcome, never format. -----

    type Stream =
        | Stdout
        | Stderr

    /// The routing rule that backs Program.fs: Blocked -> stderr, else stdout.
    let streamFor (report: CommandReport) =
        if report.Outcome = CommandOutcome.Blocked then
            Stderr
        else
            Stdout

    let blocked =
        { sample with
            Outcome = CommandOutcome.Blocked }

    let succeeding =
        { sample with
            Outcome = CommandOutcome.Succeeded }

    [<Fact>]
    let ``T016 stream routing is identical across formats`` () =
        for report in [ blocked; succeeding ] do
            let viaJson = streamFor report
            let viaText = streamFor report
            let viaRich = streamFor report
            Assert.Equal(viaJson, viaText)
            Assert.Equal(viaText, viaRich)

    [<Fact>]
    let ``T016 blocked routes to stderr and others to stdout`` () =
        Assert.Equal(Stderr, streamFor blocked)
        Assert.Equal(Stdout, streamFor succeeding)

    [<Fact>]
    let ``T016 exit code depends only on the report not the format`` () =
        for report in [ blocked; succeeding ] do
            let code = exitCodeForReport report
            // resolving in any format leaves the report (and thus its exit code) unchanged
            resolve Json interactiveColor report |> ignore
            resolve Text interactiveColor report |> ignore
            resolve Rich interactiveColor report |> ignore
            Assert.Equal(code, exitCodeForReport report)

    [<Fact>]
    let ``exit code escalates to 2 for any tool-defect diagnostic without a registry`` () =
        // A blocked report whose only diagnostic is a *newly invented* defect id — present in no
        // hand-maintained list — still exits 2. This is the silent-demotion the old providerDefectIds
        // set could cause; the typed IsToolDefect bit removes it (feature 062, SC-003).
        let invented =
            RichRenderingTests.diag "brand.newDefectNeverRegistered" DiagnosticError "boom"
            |> markToolDefect

        Assert.Equal(
            2,
            exitCodeForReport
                { blocked with
                    Diagnostics = [ invented ] }
        )

        // The same id without the bit is a user-input failure → exit 1.
        let userInput =
            RichRenderingTests.diag "brand.newDefectNeverRegistered" DiagnosticError "boom"

        Assert.Equal(
            1,
            exitCodeForReport
                { blocked with
                    Diagnostics = [ userInput ] }
        )

        // A non-blocked outcome ignores the bit entirely → exit 0.
        Assert.Equal(
            0,
            exitCodeForReport
                { succeeding with
                    Diagnostics = [ invented ] }
        )
