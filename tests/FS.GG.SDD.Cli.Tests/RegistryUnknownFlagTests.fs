namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Cli
open Xunit

/// ADR-0002 Gap C finding 4 (#203 / FS-GG/FS.GG.SDD#258): the two `registry` subcommands parse with
/// their own scanners outside the lifecycle interpreter and — until this fix — silently dropped an
/// option they could not honor via the `_ :: rest` catch-all. Mirroring #196, an unrecognized option
/// must now block: exit 1, nothing extra written, honoring each subcommand's existing error channel
/// (`skill-manifest` → stderr, stdout clean; `validate` → its stdout verdict). In-process (no
/// subprocess) so the tests stay on the fast tier and observe the "no work performed" invariant directly.
///
/// Serialized via the `Console` collection: the capture swaps the process-global `Console.Out`/`Error`,
/// so it must not run concurrently with any other stream-capturing class.
[<Collection("Console")>]
module RegistryUnknownFlagTests =

    let private captureStderr (thunk: unit -> int) =
        let original = Console.Error
        use writer = new StringWriter()
        Console.SetError writer

        try
            let code = thunk ()
            writer.Flush()
            code, writer.ToString()
        finally
            Console.SetError original

    let private captureStdout (thunk: unit -> int) =
        let original = Console.Out
        use writer = new StringWriter()
        Console.SetOut writer

        try
            let code = thunk ()
            writer.Flush()
            code, writer.ToString()
        finally
            Console.SetOut original

    // ----- registry skill-manifest: unrecognized option → stderr diagnostic + exit 1 -----

    [<Theory>]
    [<InlineData("--bogus")>]
    [<InlineData("-x")>]
    let ``skill-manifest rejects an unrecognized option with exit 1 on stderr`` (option: string) =
        let code, stderr = captureStderr (fun () -> RegistrySkillManifest.run [ option ])

        Assert.Equal(1, code)
        Assert.Contains("unrecognized option", stderr)
        Assert.Contains(option, stderr)

    [<Fact>]
    let ``skill-manifest rejects an unrecognized option even after recognized ones`` () =
        // The `--root` value (`.`) must not be mistaken for an option, and a later `--oops` is still
        // caught — the reject pass is not masked by the recognized `--check`/`--root` tokens.
        let code, stderr =
            captureStderr (fun () -> RegistrySkillManifest.run [ "--check"; "--root"; "."; "--oops" ])

        Assert.Equal(1, code)
        Assert.Contains("unrecognized option", stderr)
        Assert.Contains("--oops", stderr)

    [<Fact>]
    let ``skill-manifest with a --root value is not flagged as an unrecognized option`` () =
        // Regression: `--root <dir>` is recognized and its value is skipped. A relative in-workspace
        // root still fails `--check` (the manifest is absent under it), but with the MISSING/STALE
        // hint — never the unrecognized-option diagnostic.
        let code, stderr =
            captureStderr (fun () -> RegistrySkillManifest.run [ "--check"; "--root"; "some/relative/dir" ])

        Assert.Equal(1, code)
        Assert.DoesNotContain("unrecognized option", stderr)

    // ----- registry validate: unrecognized option → stdout verdict + exit 1 -----

    [<Theory>]
    [<InlineData("--typo")>]
    [<InlineData("-x")>]
    let ``validate rejects an unrecognized option with exit 1 through its stdout verdict`` (option: string) =
        // `validate` always emits a verdict JSON (gate-callable); the unrecognized-option failure
        // surfaces there as `valid:false`, never a silently-dropped token or a crash.
        let code, stdout =
            captureStdout (fun () -> RegistryValidate.run [ "validate"; option; "registry.yml" ])

        Assert.Equal(1, code)
        Assert.Contains("unrecognized option", stdout)
        Assert.Contains(option, stdout)
        Assert.Contains("\"valid\": false", stdout)

    [<Fact>]
    let ``validate with a recognized flag is not flagged as an unrecognized option`` () =
        // Regression: `--json` is recognized, so it passes through to the load/validate path. The
        // absent file fails as a MalformedDocument-class diagnostic — never the unrecognized-option one.
        let missing =
            Path.Combine(Path.GetTempPath(), $"fsgg-sdd-258-{Guid.NewGuid():N}.yml")

        let _, stdout =
            captureStdout (fun () -> RegistryValidate.run [ "validate"; "--json"; missing ])

        Assert.DoesNotContain("unrecognized option", stdout)
        Assert.Contains(missing, stdout)
