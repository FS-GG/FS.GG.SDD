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

    /// A stderr sink that throws on every write — the broken-pipe (EPIPE) / render-fault shape that
    /// can make the backstop's *own* projection throw (#252 item 1).
    type private ThrowingWriter() =
        inherit TextWriter()
        override _.Encoding = System.Text.Encoding.UTF8
        override _.Write(_: char) : unit = raise (IOException "Broken pipe")
        override _.Write(_: string) : unit = raise (IOException "Broken pipe")
        override _.WriteLine(_: string) : unit = raise (IOException "Broken pipe")

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

    // #252 item 2: the backstop's NextAction must report the defect, not advise the agent to
    // "correct" an internal crash. The JSON view (the automation contract) carries `reportToolDefect`,
    // never the generic `correctBlockingDiagnostics`.
    [<Fact>]
    let ``the backstop NextAction reports the tool defect rather than advising a correction`` () =
        let _, stderr = captureStderr (fun () -> Program.guarded boom [ "--json" ])

        Assert.Contains("reportToolDefect", stderr)
        Assert.DoesNotContain("correctBlockingDiagnostics", stderr)

    // #252 item 1 (defense-in-depth): the recovery path is itself unguarded. If projecting the defect
    // report throws — a broken stderr pipe, a `--rich` render fault — the exception used to escape
    // `guarded` and print the exact raw CLR stack trace the backstop exists to prevent, on the very
    // path meant to enforce "never a raw stack trace". The last-resort inner guard must swallow it and
    // still return the tool-defect exit code.
    [<Fact>]
    let ``a throw in the recovery path itself never escapes and still exits 2`` () =
        let original = Console.Error
        Console.SetError(new ThrowingWriter())

        try
            // Every stderr write throws, so `printUnhandled` throws while reporting the primary defect;
            // `guarded` must not propagate that (the assertion is that this call returns at all) and
            // must still yield the tool-defect exit code.
            let code = Program.guarded boom []
            Assert.Equal(2, code)
        finally
            Console.SetError original

    // ----- the pure diagnostic constructor -----

    [<Fact>]
    let ``unhandledException is a tool defect carrying the exception message`` () =
        let diagnostic = CommandReports.unhandledException "kaboom-42"

        Assert.Equal("unhandledException", diagnostic.Id)
        Assert.Equal(DiagnosticSeverity.DiagnosticError, diagnostic.Severity)
        Assert.True(diagnostic.IsToolDefect)
        Assert.Contains("kaboom-42", diagnostic.Message)
