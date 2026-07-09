namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Artifacts.Diagnostics
open Xunit

/// FS-GG/FS.GG.SDD#250 (Gap C finding 7 / #203, ADR-0002 invariant 4): the top-level dispatch has
/// no `try/catch` around the pure plan/update/serialize pipeline, so a throw that escapes it used
/// to print a raw CLR stack trace and exit with the default unhandled code — violating the "never a
/// raw stack trace; distinguish malformed input (exit 1) from tool defect (exit 2)" doctrine.
///
/// `Program.guarded` wraps the dispatch: any escape becomes a deterministic `unhandledException`
/// tool-defect report (exit 2) projected through the normal three views, with the stack trace
/// swallowed. Tested with an injected throwing dispatch because the real pipeline is too defensive
/// to throw on demand end-to-end.
///
/// Serialized via the `Console` collection: the capture swaps the process-global `Console.Error`,
/// so it must not run concurrently with any other stderr-capturing class (e.g.
/// `RegistrySkillManifestContainmentTests`).
[<Collection("Console")>]
module ExceptionBackstopTests =

    // The backstop reports the escape on stderr; capture it without disturbing the shared console.
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

    let private boom (_: string list) : int =
        raise (InvalidOperationException "kaboom-42")

    [<Fact>]
    let ``a throw escaping the dispatch is classified as a tool defect (exit 2)`` () =
        let code, stderr = captureStderr (fun () -> Program.guarded boom [])

        Assert.Equal(2, code)
        Assert.Contains("unhandledException", stderr)

    [<Fact>]
    let ``the backstop never leaks a raw CLR stack trace`` () =
        let _, stderr = captureStderr (fun () -> Program.guarded boom [])

        // A leaked .NET stack trace has frame lines beginning with "   at "; the backstop carries
        // only the exception *message*, never the stack.
        Assert.DoesNotContain("   at ", stderr)
        Assert.DoesNotContain("InvalidOperationException", stderr)
        // The message *is* surfaced (diagnosability without a leak).
        Assert.Contains("kaboom-42", stderr)

    [<Fact>]
    let ``the normal (non-throwing) path is passed through unchanged`` () =
        // guarded returns the dispatch's exit code verbatim and writes nothing of its own.
        let code, stderr =
            captureStderr (fun () -> Program.guarded (fun _ -> 7) [ "anything" ])

        Assert.Equal(7, code)
        Assert.Equal("", stderr)

    [<Theory>]
    [<InlineData("--json")>]
    [<InlineData("--text")>]
    [<InlineData("--rich")>]
    let ``the backstop honors the output-format flags and still exits 2`` (formatFlag: string) =
        let code, stderr = captureStderr (fun () -> Program.guarded boom [ formatFlag ])

        Assert.Equal(2, code)
        // The human-facing message is present in every projection (--rich degrades to plain text
        // over the redirected sink); only the JSON view carries the raw diagnostic id.
        Assert.Contains("unexpected internal error", stderr)

    // ----- the pure diagnostic constructor -----

    [<Fact>]
    let ``unhandledException is a tool defect carrying the exception message`` () =
        let diagnostic = CommandReports.unhandledException "kaboom-42"

        Assert.Equal("unhandledException", diagnostic.Id)
        Assert.Equal(DiagnosticSeverity.DiagnosticError, diagnostic.Severity)
        Assert.True(diagnostic.IsToolDefect)
        Assert.Contains("kaboom-42", diagnostic.Message)
