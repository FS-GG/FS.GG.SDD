namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

// §3.5 (FR-008–011, SC-005/006): the help report projects three ways (json/text/rich); rich
// degrades to zero-ANSI when non-interactive or color-disabled. The apphost smokes exercise
// the real CLI dispatch end-to-end (vertical slice).
module HelpRenderingTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let escChar = char 0x1b

    let private generator = SchemaVersionModule.currentGeneratorVersion()
    let private topLevel = helpReport (Commands.request Init ".") (CommandHelp.topLevelHelp generator)
    let private commandHelp = helpReport (Commands.request Verify ".") (CommandHelp.commandHelp Verify)

    let private interactiveColor = { IsInteractive = true; ColorEnabled = true; Width = Some 100; IsInputInteractive = true }
    let private nonInteractive = { interactiveColor with IsInteractive = false }
    let private colorDisabled = { interactiveColor with ColorEnabled = false }

    // ----- projections (in-process, deterministic) -----

    [<Fact>]
    let ``help --json is canonical and byte-identical across runs`` () =
        let first = serializeReport topLevel
        let second = serializeReport topLevel
        Assert.Equal(first, second)
        Assert.Contains("\"scope\": \"topLevel\"", first)
        Assert.Contains("\"usage\"", first)
        Assert.Contains("\"globalFlags\"", first)

    [<Fact>]
    let ``command help --json names the command scope`` () =
        let json = serializeReport commandHelp
        Assert.Contains("\"scope\": \"command\"", json)
        Assert.Contains("\"command\": \"verify\"", json)

    [<Fact>]
    let ``help --text is a portable plain-text projection`` () =
        let text = renderText topLevel
        Assert.Contains("helpScope: topLevel", text)
        Assert.Contains("usage:", text)
        Assert.Contains("command init:", text)
        Assert.Contains("globalFlag --root", text)

    [<Fact>]
    let ``help --rich renders richly when interactive and color enabled and adds no facts`` () =
        let result = resolve Rich interactiveColor topLevel
        Assert.True(result.UsedRichRendering)
        // Rich derives from the text projection — every text fact is present.
        Assert.Contains("topLevel", result.Text)

    [<Fact>]
    let ``help --rich degrades to zero-ANSI plain text when non-interactive`` () =
        let result = resolve Rich nonInteractive topLevel
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText topLevel, result.Text)
        Assert.False(result.Text |> Seq.exists (fun c -> c = escChar))

    // #68: capabilities are sensed for the stream a report actually routes to. A redirected
    // sink (Blocked → stderr under `--rich 2>err.log`) must sense non-interactive with no
    // width so Rich degrades to zero-ANSI plain text; a live sink stays interactive.
    [<Fact>]
    let ``detectCapabilities follows the target sink's redirection`` () =
        Assert.False((detectCapabilities true).IsInteractive)
        Assert.Equal(None, (detectCapabilities true).Width)
        Assert.True((detectCapabilities false).IsInteractive)

    [<Fact>]
    let ``a report resolves Rich to zero-ANSI plain text when its sink is redirected`` () =
        let result = resolve Rich (detectCapabilities true) commandHelp
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText commandHelp, result.Text)
        Assert.False(result.Text |> Seq.exists (fun c -> c = escChar))

    [<Fact>]
    let ``help --rich degrades to zero-ANSI plain text when color disabled`` () =
        let result = resolve Rich colorDisabled topLevel
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText topLevel, result.Text)
        Assert.False(result.Text |> Seq.exists (fun c -> c = escChar))

    // ----- apphost dispatch smokes (real CLI, end-to-end) -----

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then "Release" else "Debug"

    // The native apphost is invoked directly (not via `dotnet <dll>`): the dotnet muxer
    // intercepts a leading `--help`, so only the apphost exercises top-level help honestly.
    let private apphost =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli")

    let private runHost (env: (string * string) list) (args: string list) =
        let startInfo = ProcessStartInfo(apphost)
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false
        env |> List.iter (fun (key, value) -> startInfo.Environment[key] <- value)

        use proc =
            match Process.Start startInfo with
            | null -> failwith "Failed to start the apphost."
            | started -> started

        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit(30000) |> ignore
        {| ExitCode = proc.ExitCode; StdOut = stdout; StdErr = stderr |}

    [<Fact>]
    let ``CLI top-level --help exits 0 with top-level help on stdout`` () =
        let result = runHost [] [ "--help" ]
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"scope\": \"topLevel\"", result.StdOut)
        Assert.DoesNotContain("unknownCommand", result.StdOut)

    [<Fact>]
    let ``CLI --help --json resolves to help and never falls through`` () =
        let result = runHost [] [ "--help"; "--json" ]
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"scope\": \"topLevel\"", result.StdOut)

    [<Fact>]
    let ``CLI command --help exits 0 with that command's help`` () =
        let result = runHost [] [ "verify"; "--help" ]
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"command\": \"verify\"", result.StdOut)

    [<Fact>]
    let ``CLI unknown command with --help still resolves to unknownCommand exit 1`` () =
        let result = runHost [] [ "frobnicate"; "--help" ]
        Assert.Equal(1, result.ExitCode)
        Assert.Contains("unknownCommand", result.StdErr)

    [<Fact>]
    let ``CLI help --rich under NO_COLOR emits zero ANSI`` () =
        let result = runHost [ "NO_COLOR", "1" ] [ "--help"; "--rich" ]
        Assert.Equal(0, result.ExitCode)
        Assert.False(result.StdOut |> Seq.exists (fun c -> c = escChar))
