namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open Xunit

/// CLI smoke for `fsgg-sdd lint` (feature 076). Invokes the real host binary so the
/// dispatch, the bespoke 0/1/2 exit mapping (SC-006), and the three projections (FR-010)
/// are exercised end-to-end.
module LintCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then
            "Release"
        else
            "Debug"

    let private cliDll =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli.dll")

    let private runCli (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.WorkingDirectory <- Commands.repoRoot
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false
        startInfo.Environment["DOTNET_NOLOGO"] <- "1"
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        startInfo.Environment["NO_COLOR"] <- "1"

        use proc =
            match Process.Start startInfo with
            | null -> failwith "Failed to start the dotnet process."
            | started -> started

        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        stdout + stderr, proc.ExitCode

    let private fixture name = $"tests/fixtures/lint/broken-all/{name}"
    let private example name = $"docs/examples/lifecycle-artifacts/{name}"

    // ---- SC-006: 0 clean / 1 defects / 2 unusable input ----

    [<Fact>]
    let ``SC-006 clean artifact exits 0`` () =
        let _, code = runCli [ "lint"; example "clarifications.md" ]
        Assert.Equal(0, code)

    [<Fact>]
    let ``SC-006 defect-bearing artifact exits 1`` () =
        let _, code = runCli [ "lint"; fixture "checklist.md" ]
        Assert.Equal(1, code)

    [<Fact>]
    let ``SC-006 missing artifact exits 2`` () =
        let _, code = runCli [ "lint"; "tests/fixtures/lint/does-not-exist.md" ]
        Assert.Equal(2, code)

    [<Fact>]
    let ``SC-006 unrecognized artifact kind exits 2`` () =
        let _, code = runCli [ "lint"; "README.md" ]
        Assert.Equal(2, code)

    [<Fact>]
    let ``SC-006 no artifact argument exits 2`` () =
        let _, code = runCli [ "lint" ]
        Assert.Equal(2, code)

    // ---- FR-010: the lint block appears in the json / text projections ----

    [<Fact>]
    let ``FR-010 json projection carries the lint block with the defect class`` () =
        let stdout, _ = runCli [ "lint"; fixture "checklist.md"; "--json" ]
        Assert.Contains("\"lint\"", stdout)
        Assert.Contains("duplicateId", stdout)
        Assert.Contains("grammarPointer", stdout)

    [<Fact>]
    let ``FR-010 text projection renders`` () =
        let stdout, code = runCli [ "lint"; fixture "checklist.md"; "--text" ]
        Assert.Equal(1, code)
        Assert.Contains("lint", stdout)
