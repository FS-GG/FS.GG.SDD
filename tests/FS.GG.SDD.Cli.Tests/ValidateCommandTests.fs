namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.TestShared
open Xunit

/// CLI smoke for `fsgg-sdd validate` (cli-validate-command). Invokes the real host
/// binary so the dispatch, projections, and exit code are exercised end-to-end.
module ValidateCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then
            "Release"
        else
            "Debug"

    let private cliDll =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli.dll")

    /// `dotnet <cliDll> <args>` with the .NET host banner / telemetry / color suppressed, so the
    /// captured stdout is exactly the report (no host-emitted ANSI noise).
    let private validateStartInfo (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.Environment["DOTNET_NOLOGO"] <- "1"
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] <- "1"
        startInfo.Environment["NO_COLOR"] <- "1"
        startInfo

    // A full `validate` matrix is the slowest thing these tests spawn; the bound only has to rule
    // out a wedge, not police the runtime.
    let private validateTimeoutMs = 300_000

    let private runCli (args: string list) =
        let completion =
            TestShared.ChildProcess.runBounded validateTimeoutMs (validateStartInfo args)

        completion.StandardOutput, completion.ExitCode

    /// As `runCli` but also returns captured stderr — for the user-input diagnostic path (#68).
    let private runCliFull (args: string list) =
        let completion =
            TestShared.ChildProcess.runBounded validateTimeoutMs (validateStartInfo args)

        completion.StandardOutput, completion.StandardError, completion.ExitCode

    /// As `runCliFull` but runs the child in `workDir`, so a *relative* `--out` resolves there. The
    /// containment guard (#256) refuses absolute/`..` paths, so persisted-output tests write a bare
    /// relative filename into an isolated working directory rather than an absolute temp path.
    let private runCliFullIn (workDir: string) (args: string list) =
        let startInfo = validateStartInfo args
        startInfo.WorkingDirectory <- workDir

        let completion = TestShared.ChildProcess.runBounded validateTimeoutMs startInfo
        completion.StandardOutput, completion.StandardError, completion.ExitCode

    // `--matrix compatibility` is the cheapest matrix (no per-state lifecycle builds).
    [<Fact; Trait("tier", "slow")>]
    let ``validate --json emits a schemaVersion 1 report`` () =
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        Assert.Contains("\"schemaVersion\": 1", stdout)

    [<Fact; Trait("tier", "slow")>]
    let ``validate --text carries no ANSI when redirected`` () =
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        // Ordinal scan: xUnit's string DoesNotContain is culture-aware and treats the
        // ESC control char as ignorable, so it would spuriously "find" it anywhere.
        Assert.False(stdout |> Seq.exists (fun c -> int c = 27), "report text contains an ANSI escape")

    [<Fact; Trait("tier", "slow")>]
    let ``a single-matrix run reports the others as notValidated with a non-zero exit`` () =
        // INV-1 / FR-007: restricting to one matrix never reads as a full pass.
        let stdout, exitCode = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        Assert.Contains("notValidated", stdout)
        Assert.NotEqual(0, exitCode)

    // #68: a bad `--out` path is user input, not a tool defect — it must surface as a stderr
    // diagnostic + exit 1, never a raw stack trace, while the stdout report contract still emits.
    [<Fact; Trait("tier", "slow")>]
    let ``validate --out to a path under a missing directory fails cleanly without a stack trace`` () =
        // A *relative* missing-directory path (resolved against an isolated working dir): it passes
        // the containment guard (#256) and reaches the DirectoryNotFoundException handler this test
        // exists to cover (#68) — an absolute path would now be short-circuited by the guard instead.
        let workDir = Commands.tempDirectory ()
        let badPath = Path.Combine("missing-dir", "report.json")

        let stdout, stderr, exitCode =
            runCliFullIn workDir [ "validate"; "--matrix"; "compatibility"; "--json"; "--out"; badPath ]

        Assert.Equal(1, exitCode)
        Assert.Contains("cannot write --out", stderr)
        Assert.DoesNotContain("Unhandled exception", stderr)
        // No stack frame leaked (the raw exception path prints "   at <frame>").
        Assert.False(stderr.Contains("   at ", StringComparison.Ordinal), "a stack frame leaked to stderr")
        // The stdout automation contract is emitted regardless of the --out failure.
        Assert.Contains("\"schemaVersion\": 1", stdout)

    // ADR-0002 Gap C finding 1 (FS-GG/FS.GG.SDD#256): an `--out` path that is absolute or carries a
    // `..` segment escapes the workspace root. It is refused *before* the write — exit 1, a stderr
    // diagnostic, and no file created — while the stdout report contract still emits. Parity with the
    // `surface`/`registry` root-escape guards (#185/#237).
    [<Fact; Trait("tier", "slow")>]
    let ``validate --out to an absolute path is refused as a root escape without writing`` () =
        // The target directory is writable, so a refused write proves the containment guard rather
        // than a coincidental IO failure: an absolute path trips `IsPathRooted` on the raw value.
        let escapePath =
            Path.Combine(Commands.tempDirectory (), $"escape-{System.Guid.NewGuid():N}.json")

        try
            let stdout, stderr, exitCode =
                runCliFull [ "validate"; "--matrix"; "compatibility"; "--json"; "--out"; escapePath ]

            Assert.Equal(1, exitCode)
            Assert.Contains("escapes the workspace root", stderr)
            Assert.False(File.Exists escapePath, "the escaping --out path was written despite the guard")
            // The stdout automation contract is emitted regardless of the refused --out.
            Assert.Contains("\"schemaVersion\": 1", stdout)
            // No stack frame leaked (user-input failure, not a tool defect).
            Assert.False(stderr.Contains("   at ", StringComparison.Ordinal), "a stack frame leaked to stderr")
        finally
            if File.Exists escapePath then
                File.Delete escapePath

    [<Fact; Trait("tier", "slow")>]
    let ``validate --out with a .. segment is refused as a root escape`` () =
        let dotDotPath = $"../escape-{System.Guid.NewGuid():N}.json"

        let _, stderr, exitCode =
            runCliFull [ "validate"; "--matrix"; "compatibility"; "--json"; "--out"; dotDotPath ]

        Assert.Equal(1, exitCode)
        Assert.Contains("escapes the workspace root", stderr)

    // ----- T012: --rich end-to-end (degrades to text when redirected) -----

    [<Fact; Trait("tier", "slow")>]
    let ``validate --rich redirected carries zero ANSI and equals --text`` () =
        // Redirected stdout (+ NO_COLOR from runCli) degrades rich to the exact plain
        // text projection: zero ANSI, byte-identical to --text (FR-005 / C-4 / SC-005).
        let richOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--rich" ]
        let textOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        Assert.False(richOut |> Seq.exists (fun c -> int c = 27), "rich output contains an ANSI escape")
        Assert.Equal(textOut, richOut)

    [<Fact; Trait("tier", "slow")>]
    let ``validate --json keeps the sensed fence normalized to null`` () =
        // FR-003 / INV-3: the rich feature changes no JSON byte; the sensed block
        // stays null in the automation contract.
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        Assert.Contains("\"startedAtUtc\": null", stdout)
        Assert.Contains("\"durationMs\": null", stdout)
        Assert.Contains("\"host\": null", stdout)

    [<Fact; Trait("tier", "slow")>]
    let ``validate exit code is identical across --json, --text, and --rich`` () =
        // SC-005: format selection changes presentation only, never the exit code.
        let _, jsonExit = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        let _, textExit = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        let _, richExit = runCli [ "validate"; "--matrix"; "compatibility"; "--rich" ]
        Assert.Equal(jsonExit, textExit)
        Assert.Equal(textExit, richExit)
        // Partial (single-matrix) run never reads as a full pass.
        Assert.NotEqual(0, jsonExit)

    [<Fact; Trait("tier", "slow")>]
    let ``validate --rich --out persists deterministic text with zero ANSI`` () =
        // FR-010: --out never receives rich ANSI; it persists the deterministic
        // plain-text projection (equal to --text stdout). A relative --out under an isolated working
        // dir (the containment guard #256 refuses absolute paths).
        let workDir = Commands.tempDirectory ()
        let outName = $"validate-rich-{System.Guid.NewGuid():N}.txt"

        let _, _, _ =
            runCliFullIn workDir [ "validate"; "--matrix"; "compatibility"; "--rich"; "--out"; outName ]

        let persisted = File.ReadAllText(Path.Combine(workDir, outName))
        let textOut, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--text" ]
        Assert.False(persisted |> Seq.exists (fun c -> int c = 27), "persisted --out contains an ANSI escape")
        // runCli appends a trailing newline to stdout; compare on trimmed content.
        Assert.Equal(textOut.TrimEnd(), persisted.TrimEnd())

    // ----- Feature 088 / FS.GG.SDD#172: Markdown report card + force-color -----

    /// As `runCli` but forces color and does NOT set NO_COLOR — for the force-color path.
    let private runCliForced (args: string list) =
        let startInfo = validateStartInfo args
        startInfo.Environment["FORCE_COLOR"] <- "1"
        startInfo.Environment.Remove "NO_COLOR" |> ignore

        let completion = TestShared.ChildProcess.runBounded validateTimeoutMs startInfo
        completion.StandardOutput, completion.ExitCode

    [<Fact; Trait("tier", "slow")>]
    let ``validate --markdown emits a capture-safe report card with zero ANSI`` () =
        let stdout, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--markdown" ]
        Assert.Contains("# Validation Report", stdout)
        Assert.Contains("**Verdict:**", stdout)
        Assert.Contains("| passed | failed | skipped | coverageGaps | notValidated |", stdout)
        Assert.False(stdout |> Seq.exists (fun c -> int c = 27), "markdown output contains an ANSI escape")

    [<Fact; Trait("tier", "slow")>]
    let ``validate --markdown is byte-identical across runs`` () =
        let first, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--markdown" ]
        let second, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--markdown" ]
        Assert.Equal(first, second)

    [<Fact; Trait("tier", "slow")>]
    let ``validate --markdown --out persists the card and exits on verdict only`` () =
        // A relative --out under an isolated working dir (the containment guard #256 refuses absolute).
        let workDir = Commands.tempDirectory ()
        let outName = $"validate-md-{System.Guid.NewGuid():N}.md"

        let stdout, _, exitCode =
            runCliFullIn workDir [ "validate"; "--matrix"; "compatibility"; "--markdown"; "--out"; outName ]

        let persisted = File.ReadAllText(Path.Combine(workDir, outName))
        Assert.Contains("# Validation Report", persisted)
        Assert.Equal(stdout.TrimEnd(), persisted.TrimEnd())
        // Partial (single-matrix) run never reads as a full pass — exit reflects the verdict.
        Assert.NotEqual(0, exitCode)

    [<Fact; Trait("tier", "slow")>]
    let ``validate exit code is identical across --markdown and --json`` () =
        let _, jsonExit = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        let _, mdExit = runCli [ "validate"; "--matrix"; "compatibility"; "--markdown" ]
        Assert.Equal(jsonExit, mdExit)

    [<Fact; Trait("tier", "slow")>]
    let ``FORCE_COLOR re-enables rich ANSI on a redirected stdout`` () =
        // Captured (piped) stdout is not a TTY, so --rich normally degrades; FORCE_COLOR
        // bypasses the sensing and emits real ANSI (the core #172 remedy).
        let forced, _ = runCliForced [ "validate"; "--matrix"; "compatibility"; "--rich" ]
        Assert.True(forced |> Seq.exists (fun c -> int c = 27), "forced --rich output carries no ANSI")

    [<Fact; Trait("tier", "slow")>]
    let ``force-color leaves the --json bytes unchanged`` () =
        let plain, _ = runCli [ "validate"; "--matrix"; "compatibility"; "--json" ]
        let forced, _ = runCliForced [ "validate"; "--matrix"; "compatibility"; "--json" ]
        Assert.Equal(plain.TrimEnd(), forced.TrimEnd())
