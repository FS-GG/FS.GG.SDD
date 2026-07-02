namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open Xunit

/// CLI smoke for `fsgg-sdd validate` (cli-validate-command). Invokes the real host
/// binary so the dispatch, projections, and exit code are exercised end-to-end.
module ValidateCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then "Release" else "Debug"

    let private cliDll =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli.dll")

    let private runCli (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false
        // Suppress .NET host banner / telemetry / color so the captured stdout is
        // exactly the report (no host-emitted ANSI noise).
        startInfo.Environment["DOTNET_NOLOGO"] <- "1"
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] <- "1"
        startInfo.Environment["NO_COLOR"] <- "1"

        use proc =
            match Process.Start startInfo with
            | null -> failwith "Failed to start the dotnet process."
            | started -> started

        let stdout = proc.StandardOutput.ReadToEnd()
        proc.StandardError.ReadToEnd() |> ignore
        proc.WaitForExit()
        stdout, proc.ExitCode

    /// As `runCli` but also returns captured stderr — for the user-input diagnostic path
    /// (#68). Reads stdout concurrently so neither pipe can block the child.
    let private runCliFull (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false
        startInfo.Environment["DOTNET_NOLOGO"] <- "1"
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] <- "1"
        startInfo.Environment["NO_COLOR"] <- "1"

        use proc =
            match Process.Start startInfo with
            | null -> failwith "Failed to start the dotnet process."
            | started -> started

        let stdoutTask = proc.StandardOutput.ReadToEndAsync()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        stdoutTask.GetAwaiter().GetResult(), stderr, proc.ExitCode

    // `--matrix compatibility` is the cheapest matrix (no per-state lifecycle builds).
    [<Fact>]
    let ``validate --json emits a schemaVersion 1 report`` () =
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        Assert.Contains("\"schemaVersion\": 1", stdout)

    [<Fact>]
    let ``validate --text carries no ANSI when redirected`` () =
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        // Ordinal scan: xUnit's string DoesNotContain is culture-aware and treats the
        // ESC control char as ignorable, so it would spuriously "find" it anywhere.
        Assert.False(stdout |> Seq.exists (fun c -> int c = 27), "report text contains an ANSI escape")

    [<Fact>]
    let ``a single-matrix run reports the others as notValidated with a non-zero exit`` () =
        // INV-1 / FR-007: restricting to one matrix never reads as a full pass.
        let stdout, exitCode = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        Assert.Contains("notValidated", stdout)
        Assert.NotEqual(0, exitCode)

    // #68: a bad `--out` path is user input, not a tool defect — it must surface as a stderr
    // diagnostic + exit 1, never a raw stack trace, while the stdout report contract still emits.
    [<Fact>]
    let ``validate --out to a path under a missing directory fails cleanly without a stack trace`` () =
        let badPath = Path.Combine(Commands.tempDirectory (), "missing-dir", "report.json")
        let stdout, stderr, exitCode = runCliFull [ "validate"; "--matrix"; "compatibility"; "--json"; "--out"; badPath ]

        Assert.Equal(1, exitCode)
        Assert.Contains("cannot write --out", stderr)
        Assert.DoesNotContain("Unhandled exception", stderr)
        // No stack frame leaked (the raw exception path prints "   at <frame>").
        Assert.False(stderr.Contains("   at ", StringComparison.Ordinal), "a stack frame leaked to stderr")
        // The stdout automation contract is emitted regardless of the --out failure.
        Assert.Contains("\"schemaVersion\": 1", stdout)

    // ----- T012: --rich end-to-end (degrades to text when redirected) -----

    [<Fact>]
    let ``validate --rich redirected carries zero ANSI and equals --text`` () =
        // Redirected stdout (+ NO_COLOR from runCli) degrades rich to the exact plain
        // text projection: zero ANSI, byte-identical to --text (FR-005 / C-4 / SC-005).
        let richOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--rich" ]
        let textOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        Assert.False(richOut |> Seq.exists (fun c -> int c = 27), "rich output contains an ANSI escape")
        Assert.Equal(textOut, richOut)

    [<Fact>]
    let ``validate --json keeps the sensed fence normalized to null`` () =
        // FR-003 / INV-3: the rich feature changes no JSON byte; the sensed block
        // stays null in the automation contract.
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        Assert.Contains("\"startedAtUtc\": null", stdout)
        Assert.Contains("\"durationMs\": null", stdout)
        Assert.Contains("\"host\": null", stdout)

    [<Fact>]
    let ``validate exit code is identical across --json, --text, and --rich`` () =
        // SC-005: format selection changes presentation only, never the exit code.
        let _, jsonExit = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        let _, textExit = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        let _, richExit = runCli [ "validate"; "--matrix"; "compatibility"; "--rich" ]
        Assert.Equal(jsonExit, textExit)
        Assert.Equal(textExit, richExit)
        // Partial (single-matrix) run never reads as a full pass.
        Assert.NotEqual(0, jsonExit)

    [<Fact>]
    let ``validate --rich --out persists deterministic text with zero ANSI`` () =
        // FR-010: --out never receives rich ANSI; it persists the deterministic
        // plain-text projection (equal to --text stdout).
        let outPath = Path.Combine(Path.GetTempPath(), $"validate-rich-{System.Guid.NewGuid():N}.txt")

        try
            let _, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--rich"; "--out"; outPath ]
            let persisted = File.ReadAllText outPath
            let textOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
            Assert.False(persisted |> Seq.exists (fun c -> int c = 27), "persisted --out contains an ANSI escape")
            // runCli appends a trailing newline to stdout; compare on trimmed content.
            Assert.Equal(textOut.TrimEnd(), persisted.TrimEnd())
        finally
            if File.Exists outPath then File.Delete outPath
