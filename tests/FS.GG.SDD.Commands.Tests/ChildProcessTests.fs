namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#212 — the deadlock these tests exist to make unrepresentable.
///
/// `TestShared.ChildProcess` is the one place a test spawns a child. Before this module
/// existed, every call site drained the child's two pipes *sequentially* (`StandardOutput.ReadToEnd()`
/// and only then `StandardError.ReadToEnd()`). That wedges whenever the child's stderr exceeds the OS pipe buffer
/// (64 KiB on Linux): the child blocks in `write(2)` and never exits, so the parent's stdout read
/// never sees EOF and never reaches the stderr read. The `WaitForExit(timeoutMs)` that followed the
/// reads was therefore *unreachable* — the bound meant to catch a hang was dead code, and a hung
/// smoke hung the whole run (observed: 18 minutes, killed by hand).
///
/// `fsgg-sdd` writes Blocked reports to **stderr** and success reports to stdout, so a blocked CLI
/// smoke is exactly the empty-stdout / large-stderr shape that trips this. The observed `refresh`
/// smoke already put 38,589 bytes on stderr — 59% of the buffer.
///
/// Both properties below fail (by hanging) against a sequential drain, and pass in ~a second
/// against the concurrent one. Joins ProcessGlobalEnv: `sh` is PATH-resolved (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module ChildProcessTests =

    /// Twice the 64 KiB Linux pipe buffer, so an undrained pipe is guaranteed to block the child
    /// rather than merely coming close to it.
    let private chunkBytes = 1_024
    let private chunks = 128
    let private floodBytes = chunkBytes * chunks

    /// A child that writes `floodBytes` to **stderr**, nothing to stdout, and then either exits or
    /// hangs forever. SYNTHETIC: `sh` is generic platform tooling standing in for a chatty CLI —
    /// the real one is `fsgg-sdd <stage>` emitting a Blocked report.
    ///
    /// The loop writes an exact byte count with `printf` (a shell builtin) rather than
    /// `yes | head -c`: a pipe would hand `yes` a SIGPIPE whose "broken pipe" message lands on the
    /// very stderr under test, making the captured length non-deterministic.
    let private stderrFlood (thenHang: bool) =
        let flood =
            $"i=0; while [ $i -lt {chunks} ]; do printf '%%0{chunkBytes}d' 0; i=$((i+1)); done >&2"

        let script = if thenHang then $"{flood}; sleep 3600" else flood

        let info = ProcessStartInfo "sh"
        info.ArgumentList.Add "-c"
        info.ArgumentList.Add script
        info

    // The deadlock itself: a child whose stderr overflows the pipe buffer must still run to
    // completion, with both streams captured whole. A sequential drain never returns from here.
    [<Fact; Trait("tier", "slow")>]
    let ``a child flooding stderr with an empty stdout completes instead of deadlocking`` () =
        let completion = TestShared.ChildProcess.runBounded 30_000 (stderrFlood false)

        Assert.Equal(0, completion.ExitCode)
        Assert.Equal("", completion.StandardOutput)
        Assert.Equal(floodBytes, completion.StandardError.Length)

    // The bound is *reachable*: a child that fills a pipe and then never exits is killed at
    // `timeoutMs` and reported as a failure. Under a sequential drain the timeout is dead code and
    // this test hangs forever instead of throwing.
    [<Fact; Trait("tier", "slow")>]
    let ``a hung child with a full stderr pipe is killed at its bound, not waited on forever`` () =
        let elapsed = Stopwatch.StartNew()

        // A *distinct* exception type, so a `sh` that never started cannot satisfy this assertion.
        let ex =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.ChildProcess.runBounded 1_500 (stderrFlood true) |> ignore)

        elapsed.Stop()

        Assert.Contains("timed out after 1500 ms", ex.Message)
        Assert.Equal(TestShared.ChildProcess.ChildOutlivedBound, ex.Reason)

        // Generous: the point is "bounded at all", not the precise bound. A sequential drain never
        // gets here, so any finite number proves the property.
        Assert.True(
            elapsed.ElapsedMilliseconds < 30_000L,
            $"expected the bound to fire promptly; took {elapsed.ElapsedMilliseconds} ms"
        )

    // `WaitForExit(int)` returns at CHILD exit, not at pipe EOF. A grandchild that inherited the
    // write end keeps both readers pending, so reaping them unbounded would relocate the hang from
    // before the wait to after it. The drain is bounded too; this pins that.
    [<Fact; Trait("tier", "slow")>]
    let ``a grandchild still holding the pipes cannot hang the reap after the child exits`` () =
        // The direct `sh` exits immediately; the backgrounded `sleep` inherits stdout/stderr and
        // outlives it, so the reads never reach EOF. SYNTHETIC stand-in for a build server or any
        // daemon a real child leaves behind holding its inherited handles.
        let info = ProcessStartInfo "sh"
        info.ArgumentList.Add "-c"
        info.ArgumentList.Add "sleep 20 & exit 0"

        let elapsed = Stopwatch.StartNew()

        let ex =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.ChildProcess.runBounded 1_000 info |> ignore)

        elapsed.Stop()

        Assert.Contains("pipes were still held", ex.Message)
        Assert.Equal(TestShared.ChildProcess.PipesHeldAfterExit, ex.Reason)

        Assert.True(
            elapsed.ElapsedMilliseconds < 20_000L,
            $"the reap must be bounded, not wait out the grandchild; took {elapsed.ElapsedMilliseconds} ms"
        )

    // A child that cannot be started is a `None` — `Process.Start` *throws* for a missing
    // executable rather than returning null, so that shape has to be folded in, not left to escape.
    [<Fact; Trait("tier", "slow")>]
    let ``an unstartable child yields None rather than throwing`` () =
        let missing = ProcessStartInfo "fsgg-sdd-no-such-executable-212"
        Assert.True((TestShared.ChildProcess.tryRunBounded 5_000 missing).IsNone)

    // ...but `runBounded` — and therefore `git` — turns that `None` into a loud failure. A silent
    // `-1, ""` would let the ADR-0026 gitignore-negation proofs pass without git ever running.
    [<Fact; Trait("tier", "slow")>]
    let ``runBounded turns an unstartable child into a loud failure`` () =
        let missing = ProcessStartInfo "fsgg-sdd-no-such-executable-212"

        let ex =
            Assert.Throws<Exception>(fun () -> TestShared.ChildProcess.runBounded 5_000 missing |> ignore)

        Assert.Contains("Failed to start", ex.Message)
        // The launch reason survives: "no such file" and "exec bit stripped" are different bugs.
        Assert.NotNull(ex.InnerException)

    // SYNTHETIC: retain finite diagnostic prefixes without weakening the timeout or
    // waiting for another drain grace after retirement.
    [<Fact; Trait("tier", "slow")>]
    let ``a timed out child retains bounded stdout and stderr diagnostics`` () =
        let info = ProcessStartInfo "sh"
        info.ArgumentList.Add "-c"

        info.ArgumentList.Add
            "printf 'timeout-stdout-marker'; printf 'timeout-stderr-marker' >&2; i=0; while [ $i -lt 32 ]; do printf '%01024d' 0 >&2; i=$((i+1)); done; sleep 3600"

        let elapsed = Stopwatch.StartNew()

        let ex =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.ChildProcess.runBounded 1_500 info |> ignore)

        elapsed.Stop()
        Assert.Equal(TestShared.ChildProcess.ChildOutlivedBound, ex.Reason)
        Assert.Contains("stdout: timeout-stdout-marker", ex.Message)
        Assert.Contains("stderr: timeout-stderr-marker", ex.Message)
        Assert.Contains("[truncated]", ex.Message)
        Assert.True(ex.Message.Length < 35_000, "captured stream prefixes are finite")

        Assert.True(
            elapsed.ElapsedMilliseconds < 10_000L,
            $"timeout plus one drain grace remains bounded: {elapsed.ElapsedMilliseconds} ms"
        )

        // The actual browser cleanup wrapper must preserve this typed timeout if
        // profile cleanup independently fails, rather than raising the cleanup error.
        let secondary = IO.IOException "synthetic-profile-cleanup-failure"

        let preserved =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.ChildProcess.withCleanupPreservingFailure (fun () -> raise secondary) (fun () -> raise ex)
                |> ignore)

        Assert.Same(ex, preserved)
        Assert.Same(secondary, preserved.Data["ChildProcessCleanupFailure"])
        Assert.Equal(TestShared.ChildProcess.ChildOutlivedBound, preserved.Reason)

        let value, cleanupFailure =
            TestShared.ChildProcess.withCleanupPreservingFailure (fun () -> raise secondary) (fun () ->
                "original-result")

        Assert.Equal("original-result", value)
        Assert.Same(secondary, cleanupFailure.Value)

        let cleanValue, cleanFailure =
            TestShared.ChildProcess.withCleanupPreservingFailure ignore (fun () -> "clean-result")

        Assert.Equal("clean-result", cleanValue)
        Assert.True(cleanFailure.IsNone)

    [<Fact>]
    let ``browser selection honors declarations and only missing executables select another driver`` () =
        let requested = ResizeArray<string>()
        let resolve path =
            requested.Add path
            match path with
            | "/declared/chrome" -> Some "/resolved/chrome"
            | "google-chrome" -> Some "/stable/chrome"
            | "chromium" -> Some "/snapshot/chromium"
            | _ -> None

        Assert.Equal("/resolved/chrome", TestShared.BrowserDriver.select resolve (Some "/declared/chrome"))
        Assert.Equal<string list>([ "/declared/chrome" ], List.ofSeq requested)
        for malformed in [ ""; "google-chrome"; " /declared/chrome"; "/declared/chrome\n" ] do
            Assert.Throws<ArgumentException>(fun () -> TestShared.BrowserDriver.select resolve (Some malformed) |> ignore)
            |> ignore
        requested.Clear()
        Assert.Throws<Exception>(fun () -> TestShared.BrowserDriver.select resolve (Some "/missing/chrome") |> ignore)
        |> ignore
        Assert.Equal<string list>([ "/missing/chrome" ], List.ofSeq requested)
        requested.Clear()
        Assert.Equal("/stable/chrome", TestShared.BrowserDriver.select resolve None)
        Assert.Equal<string list>([ "google-chrome" ], List.ofSeq requested)
        let missingStable path = if path = "google-chrome" then None else resolve path
        Assert.Equal("/snapshot/chromium", TestShared.BrowserDriver.select missingStable None)

    [<Fact>]
    let ``browser version and DOM capture share a deadline and started failures never retry`` () =
        let mutable elapsed = 1_000L
        let calls = ResizeArray<int * string list>()
        let completion: TestShared.ChildProcess.Completion =
            { ExitCode = 0; StandardOutput = "Synthetic Chrome 1"; StandardError = "" }
        let run timeout arguments =
            calls.Add(timeout, arguments)
            elapsed <- 8_000L
            completion
        let observed = ResizeArray<string>()
        TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) run observed.Add [ "--dump-dom" ] |> ignore
        Assert.Equal<(int * string list) list>([ (5_000, [ "--version" ]); (52_000, [ "--dump-dom" ]) ], List.ofSeq calls)
        Assert.Equal<string list>([ "Synthetic Chrome 1" ], List.ofSeq observed)
        calls.Clear()
        elapsed <- 0L
        let expired timeout arguments =
            calls.Add(timeout, arguments)
            elapsed <- 60_000L
            completion
        Assert.Throws<Exception>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) expired ignore [ "--dump-dom" ] |> ignore)
        |> ignore
        Assert.Single(calls) |> ignore
        calls.Clear()
        elapsed <- 60_000L
        Assert.Throws<Exception>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) run ignore [ "--dump-dom" ] |> ignore)
        |> ignore
        Assert.Empty calls
        elapsed <- 0L
        let failure = TestShared.ChildProcess.ChildProcessTimeout(TestShared.ChildProcess.ChildOutlivedBound, "synthetic")
        let failed timeout arguments =
            calls.Add(timeout, arguments)
            raise failure
        let actual = Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) failed ignore [ "--dump-dom" ] |> ignore)
        Assert.Same(failure, actual)
        Assert.Single(calls) |> ignore

        calls.Clear()
        let failedDom timeout arguments =
            calls.Add(timeout, arguments)
            if arguments = [ "--version" ] then completion else raise failure
        let domFailure = Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> 0L) failedDom ignore [ "--dump-dom" ] |> ignore)
        Assert.Same(failure, domFailure)
        Assert.Equal(2, calls.Count)
        calls.Clear()
        let badVersion timeout arguments =
            calls.Add(timeout, arguments)
            { completion with ExitCode = 1; StandardError = "synthetic driver failure" }
        let versionFailure = Assert.Throws<Exception>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> 0L) badVersion ignore [ "--dump-dom" ] |> ignore)
        Assert.Contains("exit 1", versionFailure.Message)
        Assert.Contains("synthetic driver failure", versionFailure.Message)
        Assert.Single(calls) |> ignore
