namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.TestShared
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

    // Runs in a temp working dir so the `analyze --explain` no-mutation check can't touch the repo.
    let private runCliIn (workDir: string) (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.WorkingDirectory <- workDir
        startInfo.Environment["DOTNET_NOLOGO"] <- "1"
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        startInfo.Environment["NO_COLOR"] <- "1"

        let completion = TestShared.ChildProcess.runBounded 60_000 startInfo
        completion.StandardOutput, completion.StandardError, completion.ExitCode

    // The default runner works out of the repo root and returns combined output + exit code.
    let private runCli (args: string list) =
        let stdout, stderr, code = runCliIn Commands.repoRoot args
        stdout + stderr, code

    let private fixture name =
        $"tests/fixtures/lint/broken-all/{name}"

    let private example name =
        $"docs/examples/lifecycle-artifacts/{name}"

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
    let ``FR-010 text projection carries the lint facts (kind, outcome, pointer)`` () =
        let stdout, code = runCli [ "lint"; fixture "checklist.md"; "--text" ]
        Assert.Equal(1, code)
        Assert.Contains("lintOutcome: defectsFound", stdout)
        Assert.Contains("lintKind: checklist", stdout)
        Assert.Contains("lintDefect: duplicateId", stdout)

    // ---- Regression: review findings on PR #134 ----

    // A defect-bearing lint is a successful read-only result; its --json must land on STDOUT,
    // not stderr, so `lint --json > file` captures the report.
    [<Fact>]
    let ``lint --json with defects writes the report to stdout`` () =
        let stdout, _stderr, code =
            runCliIn Commands.repoRoot [ "lint"; fixture "checklist.md"; "--json" ]

        Assert.Equal(1, code)
        Assert.Contains("\"lint\"", stdout)
        Assert.Contains("duplicateId", stdout)

    // The artifact positional resolves even when a flag precedes it.
    [<Fact>]
    let ``lint resolves the artifact when a flag precedes it`` () =
        let _, code = runCli [ "lint"; "--text"; fixture "checklist.md" ]
        Assert.Equal(1, code)

    // `<stage> --explain` shares the 0/1/2 mapping: a missing stage artifact is unusable -> 2.
    [<Fact>]
    let ``stage --explain on a missing artifact exits 2`` () =
        let tmp = Commands.tempDirectory ()

        let _, _, code =
            runCliIn tmp [ "clarify"; "--explain"; "--work"; "x"; "--root"; tmp ]

        Assert.Equal(2, code)

    // `<stage> --explain` with defects is a non-blocking dry run: NextAction is none.
    [<Fact>]
    let ``stage --explain with defects reports no NextAction`` () =
        let tmp = Commands.tempDirectory ()

        let broken =
            File.ReadAllText(Path.Combine(Commands.repoRoot, fixture "clarifications.md"))

        Commands.writeRelative tmp "work/x/clarifications.md" broken

        let stdout, _stderr, code =
            runCliIn tmp [ "clarify"; "--explain"; "--work"; "x"; "--root"; tmp; "--json" ]

        Assert.Equal(1, code)
        Assert.Contains("\"nextAction\": null", stdout)

    // `--explain` on a command with no primary artifact must NOT run (and mutate) the stage.
    [<Fact>]
    let ``analyze --explain is rejected and writes no readiness artifact`` () =
        let tmp = Commands.tempDirectory ()

        let _, _, code =
            runCliIn tmp [ "analyze"; "--explain"; "--work"; "x"; "--root"; tmp ]

        Assert.NotEqual(0, code)
        Assert.False(Directory.Exists(Path.Combine(tmp, "readiness")))
