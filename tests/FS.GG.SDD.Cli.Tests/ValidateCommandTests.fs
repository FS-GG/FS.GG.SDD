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

        use proc = Process.Start startInfo
        let stdout = proc.StandardOutput.ReadToEnd()
        proc.StandardError.ReadToEnd() |> ignore
        proc.WaitForExit()
        stdout, proc.ExitCode

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
