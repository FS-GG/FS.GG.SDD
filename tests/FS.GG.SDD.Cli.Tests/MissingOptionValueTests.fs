namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli
open Xunit

/// FS-GG/FS.GG.SDD#264 (ADR-0002 Gap C finding 6): the dangling-value sibling of the unknown-option
/// class #196. A value-taking option supplied with no following value used to read as `None` and
/// silently fall back to its default — `charter --work` (trailing) ran against no work id, a trailing
/// `--root` defaulted to `.`, `evidence --from-tests` mapped no proving test — so a plausible-looking
/// run landed against the wrong (defaulted) input with `diagnostics: []`, exit 0.
///
/// It now blocks: a single `missingOptionValue`, exit 1, zero writes. Three axes are pinned here —
/// the scanner (which option, if any, is missing its value), the diagnostic, and the end-to-end
/// effect (nothing written, nothing on stdout).
module MissingOptionValueTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    // ----- the scanner -----

    // The reported defect in miniature: a trailing valued option is the only shape that reads as
    // absent, because `optionValue` consumes the *next* token as the value.
    [<Fact>]
    let ``a trailing valued option is missing its value`` () =
        Assert.Equal(Some "--work", Options.missingValue Charter [ "--work" ])

    [<Fact>]
    let ``the missing option is the trailing one, not an earlier satisfied one`` () =
        Assert.Equal(Some "--title", Options.missingValue Charter [ "--work"; "demo"; "--title" ])

    // A valued option *followed* by any token is satisfied — its value may even look like a flag,
    // exactly as `optionValue` consumes it — so `--title --rich` is never flagged (mirrors #196).
    [<Fact>]
    let ``a valued option whose value looks like a flag is not missing`` () =
        Assert.Equal(None, Options.missingValue Charter [ "--work"; "demo"; "--title"; "--rich" ])

    [<Fact>]
    let ``a fully satisfied argv has no missing value`` () =
        Assert.Equal(None, Options.missingValue Charter [ "--work"; "demo"; "--title"; "Demo" ])

    // A flag carries no value, so a trailing flag is never a missing-value defect.
    [<Fact>]
    let ``a trailing flag is not a missing value`` () =
        Assert.Equal(None, Options.missingValue Plan [ "--work"; "x"; "--accept-upstream" ])
        Assert.Equal(None, Options.missingValue Charter [ "--json" ])

    // The global `--root` is valued for every command; a trailing `--root` used to default to `.`.
    [<Fact>]
    let ``a trailing global --root is missing its value`` () =
        Assert.Equal(Some "--root", Options.missingValue Verify [ "--work"; "x"; "--root" ])

    // The repeatable options: a satisfied `--param`/`--input` is fine; only a trailing one is missing.
    [<Fact>]
    let ``a repeatable option is missing only when it trails without a value`` () =
        Assert.Equal(None, Options.missingValue Scaffold [ "--param"; "a=1"; "--param"; "b=2" ])
        Assert.Equal(Some "--param", Options.missingValue Scaffold [ "--param"; "a=1"; "--param" ])
        Assert.Equal(Some "--input", Options.missingValue Specify [ "--input"; "value: v"; "--input" ])

    // An unknown token is residue (`unrecognized`'s job), not a missing value — the two classes are
    // orthogonal, and a trailing *unknown* option is never reported here.
    [<Fact>]
    let ``an unrecognized trailing option is not a missing value`` () =
        Assert.Equal(None, Options.missingValue Init [ "--project-root" ])

    // Bare `-` and the POSIX `--` separator are option syntax, not valued options.
    [<Fact>]
    let ``the end-of-options separator is not a missing value`` () =
        Assert.Equal(None, Options.missingValue Lint [ "--" ])
        Assert.Equal(None, Options.missingValue Lint [ "--"; "work/x/spec.md" ])

    [<Fact>]
    let ``an empty argv has no missing value`` () =
        Assert.Equal(None, Options.missingValue Charter [])

    // ----- the diagnostic -----

    [<Fact>]
    let ``missingOptionValue names the option and the command`` () =
        let diagnostic = missingOptionValue Charter "--work"

        Assert.Equal("missingOptionValue", diagnostic.Id)
        Assert.Contains("'--work'", diagnostic.Message)
        Assert.Contains("'charter'", diagnostic.Message)
        Assert.Contains("--work", diagnostic.Correction)
        Assert.Equal<string list>([ "--work" ], diagnostic.RelatedIds)

    // ----- apphost dispatch (real CLI, end-to-end) -----

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then
            "Release"
        else
            "Debug"

    let private apphost =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli")

    let private runHostIn (workingDirectory: string) (args: string list) =
        let startInfo = ProcessStartInfo(apphost)
        args |> List.iter startInfo.ArgumentList.Add
        startInfo.WorkingDirectory <- workingDirectory
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false

        use proc =
            match Process.Start startInfo with
            | null -> failwith "Failed to start the apphost."
            | started -> started

        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit(30000) |> ignore

        {| ExitCode = proc.ExitCode
           StdOut = stdout
           StdErr = stderr |}

    let private withInitializedDir (body: string -> unit) =
        let root = Path.Combine(Path.GetTempPath(), $"fsgg-264-{Guid.NewGuid():N}")
        Directory.CreateDirectory root |> ignore

        try
            runHostIn root [ "init"; "--root"; root ] |> ignore
            body root
        finally
            try
                Directory.Delete(root, true)
            with _ ->
                ()

    /// The issue's acceptance: a trailing valued option exits 1 with one `missingOptionValue`, and
    /// nothing on stdout — instead of running against the defaulted input and reporting `succeeded`.
    [<Fact>]
    let ``CLI charter --work with no value exits 1 with missingOptionValue and nothing on stdout`` () =
        withInitializedDir (fun root ->
            let result = runHostIn root [ "charter"; "--work" ]

            Assert.Equal(1, result.ExitCode)
            Assert.Contains("missingOptionValue", result.StdErr)
            Assert.Contains("--work", result.StdErr)
            Assert.Equal("", result.StdOut))

    /// The dangerous case in miniature: without the guard this seeded a charter for the *defaulted*
    /// work id; now it blocks and writes no work artifact.
    [<Fact>]
    let ``CLI charter --work with no value writes no work artifact`` () =
        withInitializedDir (fun root ->
            let workDir = Path.Combine(root, "work")

            let before =
                if Directory.Exists workDir then
                    Directory.GetFileSystemEntries workDir
                else
                    [||]

            runHostIn root [ "charter"; "--work" ] |> ignore

            let after =
                if Directory.Exists workDir then
                    Directory.GetFileSystemEntries workDir
                else
                    [||]

            Assert.Equal<string[]>(before, after))

    /// The control: the same option with a value is honored exactly as before.
    [<Fact>]
    let ``CLI charter --work with a value is unaffected by the guard`` () =
        withInitializedDir (fun root ->
            let result = runHostIn root [ "charter"; "--work"; "demo"; "--title"; "Demo" ]
            Assert.Equal(0, result.ExitCode))

    /// A value that looks like a flag is still consumed as the value (no false positive), exactly as
    /// `--title --rich` did before #264.
    [<Fact>]
    let ``CLI a valued option whose value looks like a flag is not rejected`` () =
        withInitializedDir (fun root ->
            let result = runHostIn root [ "charter"; "--work"; "demo"; "--title"; "--rich" ]
            Assert.DoesNotContain("missingOptionValue", result.StdErr + result.StdOut))

    /// The global `--root` is valued for every command; a trailing `--root` used to default silently
    /// to `.` and run against the wrong directory. It now blocks like any other missing value.
    [<Fact>]
    let ``CLI a trailing global --root blocks instead of defaulting to the current directory`` () =
        withInitializedDir (fun root ->
            let result = runHostIn root [ "verify"; "--work"; "x"; "--root" ]

            Assert.Equal(1, result.ExitCode)
            Assert.Contains("missingOptionValue", result.StdErr))

    /// Mirrors FR-011: malformed argv is never masked by a help flag. `--help` is present, but the
    /// trailing valued option still blocks (the missing-value defect wins over the help branch).
    [<Fact>]
    let ``CLI a trailing valued option is not masked by --help`` () =
        withInitializedDir (fun root ->
            let result = runHostIn root [ "charter"; "--help"; "--work" ]

            Assert.Equal(1, result.ExitCode)
            Assert.Contains("missingOptionValue", result.StdErr))

    [<Fact>]
    let ``the missing-value report projects to text and rich`` () =
        withInitializedDir (fun root ->
            // The format flag precedes the trailing valued option, so `--work` genuinely trails with
            // nothing to consume (`--work --text` would consume `--text` as the value, exactly as
            // `optionValue` does — that is the value-looks-like-a-flag case, not a missing value).
            let asText = runHostIn root [ "charter"; "--text"; "--work" ]
            Assert.Equal(1, asText.ExitCode)
            Assert.Contains("blocked: charter", asText.StdErr)
            Assert.Contains("Option '--work'", asText.StdErr)

            // Redirected sink ⇒ rich degrades to zero-ANSI plain text; the facts are unchanged.
            let asRich = runHostIn root [ "charter"; "--rich"; "--work" ]
            Assert.Equal(1, asRich.ExitCode)
            Assert.Contains("Option '--work'", asRich.StdErr)
            Assert.False(asRich.StdErr |> Seq.exists (fun c -> c = char 0x1b)))
