namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#212 — the deadlock these tests exist to make unrepresentable.
///
/// `TestShared.ChildProcess` is the one place a test spawns a child. Before it existed, every call
/// site drained the child's two pipes *sequentially* (`StandardOutput.ReadToEnd()` and only then
/// `StandardError.ReadToEnd()`). That wedges whenever the child's stderr exceeds the OS pipe buffer
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

        let ex =
            Assert.Throws<Exception>(fun () -> TestShared.ChildProcess.runBounded 1_500 (stderrFlood true) |> ignore)

        elapsed.Stop()

        Assert.Contains("timed out after 1500 ms", ex.Message)

        // Generous: the point is "bounded at all", not the precise bound. A sequential drain never
        // gets here, so any finite number proves the property.
        Assert.True(
            elapsed.ElapsedMilliseconds < 30_000L,
            $"expected the bound to fire promptly; took {elapsed.ElapsedMilliseconds} ms"
        )

    // A child that cannot be started is a `None`, not an exception — the shape `git` branches on.
    [<Fact>]
    let ``an unstartable child yields None rather than throwing`` () =
        let missing = ProcessStartInfo "fsgg-sdd-no-such-executable-212"
        Assert.True((TestShared.ChildProcess.tryRunBounded 5_000 missing).IsNone)
