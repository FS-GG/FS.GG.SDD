namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli
open Xunit

/// FS-GG/FS.GG.SDD#196: `fsgg-sdd` accepted any unknown option, ignored it, and proceeded with
/// defaults — `init --project-root /tmp/b` seeded the *current* directory and reported
/// `outcome: succeeded`, `diagnostics: []`, exit 0. An agent driving the `--json` contract could
/// not tell "the flag was honored" from "the flag was dropped".
///
/// The residue now blocks: one `unknownOption` per unclaimed token, exit 1, zero writes. Three
/// axes are pinned here — the scanner (which tokens are residue), the correction (recognized
/// tokens + near-miss), and the end-to-end effect (nothing written, nothing on stdout).
module UnknownOptionTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private everyCommand =
        [ Init
          Charter
          Specify
          Clarify
          Checklist
          Plan
          Tasks
          Analyze
          Evidence
          Verify
          Ship
          Agents
          Refresh
          Scaffold
          Doctor
          Upgrade
          Lint
          Surface ]

    // ----- the scanner -----

    [<Fact>]
    let ``a recognized option is not residue`` () =
        Assert.Empty(Options.unrecognized Init [ "--root"; "/tmp/b"; "--json" ])

    [<Fact>]
    let ``an unrecognized option is residue`` () =
        Assert.Equal<string list>([ "--project-root" ], Options.unrecognized Init [ "--project-root"; "/tmp/b" ])

    [<Fact>]
    let ``every unrecognized option is reported, in order of appearance`` () =
        Assert.Equal<string list>(
            [ "--alpha"; "--omega" ],
            Options.unrecognized Charter [ "--alpha"; "--work"; "x"; "--omega" ]
        )

    // The value of a valued option is skipped, exactly as `optionValue` consumes it, so a value
    // that happens to look like a flag is never mistaken for residue.
    [<Fact>]
    let ``a valued option's argument is never residue even when it looks like a flag`` () =
        Assert.Empty(Options.unrecognized Charter [ "--work"; "x"; "--title"; "--rich" ])

    [<Fact>]
    let ``a valued option with no argument is not residue`` () =
        Assert.Empty(Options.unrecognized Charter [ "--work" ])

    // `lint`'s artifact is a positional; positionals carry no dash and are never residue.
    [<Fact>]
    let ``lint's positional artifact is not residue`` () =
        Assert.Empty(Options.unrecognized Lint [ "--text"; "work/x/checklist.md" ])

    [<Fact>]
    let ``repeated --input and --param are recognized`` () =
        Assert.Empty(Options.unrecognized Specify [ "--input"; "value: v"; "--input"; "scope: s" ])
        Assert.Empty(Options.unrecognized Scaffold [ "--param"; "a=1"; "--param"; "b=2" ])

    // The reported defect in miniature: a plausible-looking flag that belongs to a *different*
    // command is residue, not a silent default.
    [<Fact>]
    let ``an option belonging to another command is residue`` () =
        Assert.Equal<string list>([ "--provider" ], Options.unrecognized Init [ "--provider"; "rendering" ])
        Assert.Equal<string list>([ "--accept-upstream" ], Options.unrecognized Tasks [ "--accept-upstream" ])

    // `--dry-run` is honored by the effect interpreter for every command, and `--explain` is
    // answered on every command (feature 076), so neither may be rejected anywhere.
    [<Fact>]
    let ``--dry-run and --explain are recognized by every command`` () =
        for command in everyCommand do
            Assert.Empty(Options.unrecognized command [ "--dry-run"; "--explain" ])

    [<Fact>]
    let ``the format and color flags are recognized by every command`` () =
        for command in everyCommand do
            Assert.Empty(Options.unrecognized command [ "--json"; "--text"; "--rich"; "--force-color"; "--root"; "." ])

    // ----- the help/parser mirror -----

    /// The dangerous drift direction: `CommandHelp` advertises a flag the parser would now
    /// *reject*. Recognition is deliberately wider than the help (`--dry-run`, `--explain`), so
    /// this is containment, not equality.
    [<Fact>]
    let ``every flag CommandHelp advertises is recognized by its command`` () =
        let advertised (command: SddCommand) =
            CommandHelp.globalFlags @ CommandHelp.commandFlags command
            |> List.collect (fun entry -> entry.Name.Split(", ") |> List.ofArray)
            |> List.map (fun name -> name.Trim())

        for command in everyCommand do
            let recognized = Options.recognizedTokens command

            for name in advertised command do
                Assert.True(
                    List.contains name recognized,
                    $"`fsgg-sdd {commandName command} --help` advertises '{name}', which the parser now rejects."
                )

    // ----- the near-miss suggestion -----

    [<Fact>]
    let ``the reported typo suggests the real flag`` () =
        Assert.Equal(Some "--root", Options.suggestion Init "--project-root")

    [<Fact>]
    let ``a one-edit typo suggests the real flag`` () =
        Assert.Equal(Some "--dry-run", Options.suggestion Init "--dryrun")
        Assert.Equal(Some "--work", Options.suggestion Charter "--wrok")

    /// Distance must dominate containment: `--forcecolor` is one edit from `--force-color` and
    /// merely *contains* the shorter `--force`.
    [<Fact>]
    let ``a near-miss prefers the closest flag over a contained shorter one`` () =
        Assert.Equal(Some "--force-color", Options.suggestion Scaffold "--forcecolor")

    [<Fact>]
    let ``an unrelated token suggests nothing`` () =
        Assert.Equal(None, Options.suggestion Init "--frobnicate")

    [<Fact>]
    let ``the suggestion is deterministic`` () =
        let first = Options.suggestion Scaffold "--parameter"
        let second = Options.suggestion Scaffold "--parameter"
        Assert.Equal(first, second)
        Assert.Equal(Some "--param", first)

    // ----- the diagnostic -----

    [<Fact>]
    let ``unknownOption names the token, the command, and every recognized option`` () =
        let diagnostic =
            unknownOption
                Init
                "--project-root"
                (Options.recognizedTokens Init)
                (Options.suggestion Init "--project-root")

        Assert.Equal("unknownOption", diagnostic.Id)
        Assert.Contains("'--project-root'", diagnostic.Message)
        Assert.Contains("'init'", diagnostic.Message)
        Assert.Contains("Did you mean '--root'?", diagnostic.Correction)
        Assert.Equal<string list>([ "--project-root" ], diagnostic.RelatedIds)

        for token in Options.recognizedTokens Init do
            Assert.Contains(token, diagnostic.Correction)

    /// The `unknownCommand` correction is pinned to the command list the same way (feature 063):
    /// a correction that drifts from what the parser accepts is worse than none.
    [<Fact>]
    let ``the unknownOption correction cannot drift from the parser`` () =
        for command in everyCommand do
            let recognized = Options.recognizedTokens command
            let correction = (unknownOption command "--x" recognized None).Correction

            for token in recognized do
                Assert.Contains(token, correction)

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

    let private withTempDirs (body: string -> string -> unit) =
        let root = Path.Combine(Path.GetTempPath(), $"fsgg-196-{Guid.NewGuid():N}")
        let cwd = Path.Combine(root, "a")
        let named = Path.Combine(root, "b")
        Directory.CreateDirectory cwd |> ignore
        Directory.CreateDirectory named |> ignore

        try
            body cwd named
        finally
            try
                Directory.Delete(root, true)
            with _ ->
                ()

    /// The issue's acceptance #1, verbatim: exit 1, one `unknownOption`, and **nothing written**
    /// to the current directory or the one the dropped flag named.
    [<Fact>]
    let ``CLI init --project-root exits 1 and writes nothing anywhere`` () =
        withTempDirs (fun cwd named ->
            let result = runHostIn cwd [ "init"; "--project-root"; named ]

            Assert.Equal(1, result.ExitCode)
            Assert.Contains("unknownOption", result.StdErr)
            Assert.Contains("--project-root", result.StdErr)
            // The apostrophes around `--root` are `'` in the JSON contract; the human
            // spelling is asserted against the `--text` projection below.
            Assert.Contains("Did you mean", result.StdErr)
            Assert.Equal("", result.StdOut)
            Assert.Empty(Directory.EnumerateFileSystemEntries cwd)
            Assert.Empty(Directory.EnumerateFileSystemEntries named))

    /// The control: the flag the author meant still seeds the directory it names.
    [<Fact>]
    let ``CLI init --root seeds the named directory and exits 0`` () =
        withTempDirs (fun cwd named ->
            let result = runHostIn cwd [ "init"; "--root"; named ]

            Assert.Equal(0, result.ExitCode)
            Assert.True(Directory.Exists(Path.Combine(named, ".fsgg")))
            Assert.Empty(Directory.EnumerateFileSystemEntries cwd))

    /// Mirrors FR-011 for unknown commands: a token the CLI cannot honor is never masked by a
    /// help flag. The correction carries the recognized options, so no information is lost.
    [<Fact>]
    let ``CLI unknown option with --help still resolves to unknownOption exit 1`` () =
        withTempDirs (fun cwd _ ->
            let result = runHostIn cwd [ "verify"; "--bogus"; "--help" ]

            Assert.Equal(1, result.ExitCode)
            Assert.Contains("unknownOption", result.StdErr)
            Assert.DoesNotContain("\"scope\": \"command\"", result.StdOut))

    [<Fact>]
    let ``the unknown-option report projects to text and rich`` () =
        withTempDirs (fun cwd _ ->
            let asText = runHostIn cwd [ "init"; "--project-root"; "/x"; "--text" ]
            Assert.Equal(1, asText.ExitCode)
            Assert.Contains("blocked: init", asText.StdErr)
            Assert.Contains("Unknown option '--project-root'", asText.StdErr)

            // Redirected sink ⇒ rich degrades to zero-ANSI plain text; the facts are unchanged.
            let asRich = runHostIn cwd [ "init"; "--project-root"; "/x"; "--rich" ]
            Assert.Equal(1, asRich.ExitCode)
            Assert.Contains("Unknown option '--project-root'", asRich.StdErr)
            Assert.False(asRich.StdErr |> Seq.exists (fun c -> c = char 0x1b)))

    /// Acceptance #2: a currently-valid invocation exits exactly as it does today.
    [<Fact>]
    let ``a valid invocation is unaffected by the residue check`` () =
        withTempDirs (fun cwd _ ->
            Assert.Equal(0, (runHostIn cwd [ "init"; "--root"; cwd ]).ExitCode)
            Assert.Equal(0, (runHostIn cwd [ "charter"; "--work"; "demo"; "--title"; "Demo" ]).ExitCode)
            Assert.Equal(0, (runHostIn cwd [ "verify"; "--help" ]).ExitCode))

    /// The sharp case for treating `--explain` as globally recognized: on a command with no
    /// primary artifact it must still reach `explainUnsupported` (feature 076), not be rejected
    /// as residue. `LintCommandTests` owns the no-mutation half; this pins which diagnostic wins.
    [<Fact>]
    let ``--explain on a command with no primary artifact still reaches explainUnsupported`` () =
        withTempDirs (fun cwd _ ->
            let result = runHostIn cwd [ "analyze"; "--explain"; "--work"; "demo" ]

            Assert.NotEqual(0, result.ExitCode)
            Assert.Contains("explainUnsupported", result.StdErr + result.StdOut)
            Assert.DoesNotContain("unknownOption", result.StdErr + result.StdOut))
