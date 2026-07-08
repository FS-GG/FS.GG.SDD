namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#212 — the deadlock these tests exist to make unrepresentable.
///
/// `TestShared.ChildProcess` is the one place the Commands and Cli test assemblies spawn a child.
/// (`FS.GG.SDD.Acceptance.Tests` still hand-rolls its own spawn; it already drains concurrently, so
/// it never had *this* bug, but it should migrate — see #217.) Before this module
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
    [<Fact>]
    let ``a child flooding stderr with an empty stdout completes instead of deadlocking`` () =
        let completion = TestShared.ChildProcess.runBounded 30_000 (stderrFlood false)

        Assert.Equal(0, completion.ExitCode)
        Assert.Equal("", completion.StandardOutput)
        Assert.Equal(floodBytes, completion.StandardError.Length)

    // The bound is *reachable*: a child that fills a pipe and then never exits is killed at
    // `timeoutMs` and reported as a failure. Under a sequential drain the timeout is dead code and
    // this test hangs forever instead of throwing.
    [<Fact>]
    let ``a hung child with a full stderr pipe is killed at its bound, not waited on forever`` () =
        let elapsed = Stopwatch.StartNew()

        // A *distinct* exception type, so a `sh` that never started cannot satisfy this assertion.
        let ex =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.ChildProcess.runBounded 1_500 (stderrFlood true) |> ignore)

        elapsed.Stop()

        Assert.Contains("timed out after 1500 ms", ex.Message)

        // Generous: the point is "bounded at all", not the precise bound. A sequential drain never
        // gets here, so any finite number proves the property.
        Assert.True(
            elapsed.ElapsedMilliseconds < 30_000L,
            $"expected the bound to fire promptly; took {elapsed.ElapsedMilliseconds} ms"
        )

    // `WaitForExit(int)` returns at CHILD exit, not at pipe EOF. A grandchild that inherited the
    // write end keeps both readers pending, so reaping them unbounded would relocate the hang from
    // before the wait to after it. The drain is bounded too; this pins that.
    [<Fact>]
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

        Assert.True(
            elapsed.ElapsedMilliseconds < 20_000L,
            $"the reap must be bounded, not wait out the grandchild; took {elapsed.ElapsedMilliseconds} ms"
        )

    // A child that cannot be started is a `None` — `Process.Start` *throws* for a missing
    // executable rather than returning null, so that shape has to be folded in, not left to escape.
    [<Fact>]
    let ``an unstartable child yields None rather than throwing`` () =
        let missing = ProcessStartInfo "fsgg-sdd-no-such-executable-212"
        Assert.True((TestShared.ChildProcess.tryRunBounded 5_000 missing).IsNone)

    // ...but `runBounded` — and therefore `git` — turns that `None` into a loud failure. A silent
    // `-1, ""` would let the ADR-0026 gitignore-negation proofs pass without git ever running.
    [<Fact>]
    let ``runBounded turns an unstartable child into a loud failure`` () =
        let missing = ProcessStartInfo "fsgg-sdd-no-such-executable-212"

        let ex =
            Assert.Throws<Exception>(fun () -> TestShared.ChildProcess.runBounded 5_000 missing |> ignore)

        Assert.Contains("Failed to start", ex.Message)
        // The launch reason survives: "no such file" and "exec bit stripped" are different bugs.
        Assert.NotNull(ex.InnerException)
