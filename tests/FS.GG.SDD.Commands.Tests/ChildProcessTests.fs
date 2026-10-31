namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Threading
open System.Threading.Tasks
open FS.GG.Execution.Contracts
open FS.GG.Execution.Processes
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
        if OperatingSystem.IsLinux() then Assert.True(ex.Data["ChildProcessSessionRetained"] :?> bool)

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

    // Literal arguments/environment and nonzero exits exercise the actual package
    // session path; shell policy remains explicit in this synthetic command.
    [<Fact; Trait("tier", "slow")>]
    let ``shared session preserves literal argv environment and nonzero exit`` () =
        let before = TestShared.ChildProcess.retainedSessionCount ()
        let info = ProcessStartInfo "sh"
        info.ArgumentList.Add "-c"
        info.ArgumentList.Add "printf '%s|%s|%s' \"$1\" \"$SDD_PROCESS_LITERAL\" \"${HOME+x}\"; printf 'literal-stderr' >&2; exit 7"
        info.ArgumentList.Add "synthetic"
        info.ArgumentList.Add "space ; $value"
        info.Environment.Clear()
        info.Environment["SDD_PROCESS_LITERAL"] <- "environment $literal"
        let completion = TestShared.ChildProcess.runBounded 5_000 info
        Assert.Equal(7, completion.ExitCode)
        Assert.Equal("space ; $value|environment $literal|", completion.StandardOutput)
        Assert.Equal("literal-stderr", completion.StandardError)
        Assert.Equal(before, TestShared.ChildProcess.retainedSessionCount ())

    // Preserve the ordinary StreamReader BOM contract after shared byte capture.
    // The input is finite synthetic UTF-8 output from the actual child process.
    [<Fact; Trait("tier", "slow")>]
    let ``shared byte capture preserves the existing BOM-aware text decoder`` () =
        let info = ProcessStartInfo "sh"
        info.ArgumentList.Add "-c"
        info.ArgumentList.Add "printf '\\357\\273\\277decoded-text'"
        let completion = TestShared.ChildProcess.runBounded 5_000 info
        Assert.Equal("decoded-text", completion.StandardOutput)

    // One byte beyond the per-stream allocation must refuse complete capture;
    // silently returning a truncated successful Completion would corrupt callers.
    [<Fact; Trait("tier", "slow")>]
    let ``output omitted by finite shared capture is never a successful completion or live app timeout`` () =
        if OperatingSystem.IsLinux() then
            let info = stderrFlood false
            info.ArgumentList.Clear()
            info.ArgumentList.Add "-c"
            info.ArgumentList.Add "i=0; while [ $i -lt 128 ]; do printf '%01024d' 0; i=$((i+1)); done >&2; printf X >&2; sleep 3600"
            let elapsed = Stopwatch.StartNew()
            let failure =
                Assert.Throws<TestShared.ChildProcess.ChildProcessCaptureFailure>(fun () ->
                    TestShared.ChildProcess.runBounded 5_000 info |> ignore)
            elapsed.Stop()
            Assert.Contains("bounded capture", failure.Message)
            Assert.Contains("[truncated]", failure.Message)
            Assert.True(failure.Message.Length < 35_000)
            Assert.False(failure.Data["ChildProcessSessionRetained"] :?> bool)
            Assert.True(elapsed.ElapsedMilliseconds < 10_000L)

    // A finite child can exit with its overflow in the final observation. Complete
    // exit/EOF must never turn that omitted byte into a successful result.
    [<Fact; Trait("tier", "slow")>]
    let ``finite child overflow remains capture failure after exit and release`` () =
        if OperatingSystem.IsLinux() then
            let info = ProcessStartInfo "sh"
            info.ArgumentList.Add "-c"
            info.ArgumentList.Add "i=0; while [ $i -lt 128 ]; do printf '%01024d' 0; i=$((i+1)); done; printf X"
            let failure =
                Assert.Throws<TestShared.ChildProcess.ChildProcessCaptureFailure>(fun () ->
                    TestShared.ChildProcess.runBounded 5_000 info |> ignore)
            Assert.Contains("bounded capture", failure.Message)
            Assert.Contains("[truncated]", failure.Message)
            Assert.False(failure.Data["ChildProcessSessionRetained"] :?> bool)

    // Command validation occurs before any session launch and never guesses the
    // shell/Windows quoting semantics of ProcessStartInfo.Arguments.
    [<Fact>]
    let ``raw argument strings are refused before a bounded child launch`` () =
        if OperatingSystem.IsLinux() then
            let before = TestShared.ChildProcess.retainedSessionCount ()
            let info = ProcessStartInfo("sh", "-c exit")
            Assert.True((TestShared.ChildProcess.tryRunBounded 5_000 info).IsNone)
            Assert.Equal(before, TestShared.ChildProcess.retainedSessionCount ())

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
            Assert.Throws<ArgumentException>(fun () ->
                TestShared.BrowserDriver.select resolve (Some malformed) |> ignore)
            |> ignore

        requested.Clear()

        Assert.Throws<Exception>(fun () -> TestShared.BrowserDriver.select resolve (Some "/missing/chrome") |> ignore)
        |> ignore

        Assert.Equal<string list>([ "/missing/chrome" ], List.ofSeq requested)
        requested.Clear()
        Assert.Equal("/stable/chrome", TestShared.BrowserDriver.select resolve None)
        Assert.Equal<string list>([ "google-chrome" ], List.ofSeq requested)

        let missingStable path =
            if path = "google-chrome" then None else resolve path

        Assert.Equal("/snapshot/chromium", TestShared.BrowserDriver.select missingStable None)

    [<Fact>]
    let ``browser version and DOM capture share a deadline and started failures never retry`` () =
        let mutable elapsed = 1_000L
        let calls = ResizeArray<int * string list>()

        let completion: TestShared.ChildProcess.Completion =
            {
                ExitCode = 0
                StandardOutput = "Synthetic Chrome 1"
                StandardError = ""
            }

        let run timeout arguments =
            calls.Add(timeout, arguments)
            elapsed <- 8_000L
            completion

        let observed = ResizeArray<string>()

        TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) run observed.Add [ "--dump-dom" ]
        |> ignore

        Assert.Equal<(int * string list) list>(
            [ (5_000, [ "--version" ]); (52_000, [ "--dump-dom" ]) ],
            List.ofSeq calls
        )

        Assert.Equal<string list>([ "Synthetic Chrome 1" ], List.ofSeq observed)
        calls.Clear()
        elapsed <- 0L

        let expired timeout arguments =
            calls.Add(timeout, arguments)
            elapsed <- 60_000L
            completion

        Assert.Throws<Exception>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) expired ignore [ "--dump-dom" ]
            |> ignore)
        |> ignore

        Assert.Single(calls) |> ignore
        calls.Clear()
        elapsed <- 60_000L

        Assert.Throws<Exception>(fun () ->
            TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) run ignore [ "--dump-dom" ]
            |> ignore)
        |> ignore

        Assert.Empty calls
        elapsed <- 0L

        let failure =
            TestShared.ChildProcess.ChildProcessTimeout(TestShared.ChildProcess.ChildOutlivedBound, "synthetic")

        let failed timeout arguments =
            calls.Add(timeout, arguments)
            raise failure

        let actual =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.BrowserDriver.observeAndRun (fun () -> elapsed) failed ignore [ "--dump-dom" ]
                |> ignore)

        Assert.Same(failure, actual)
        Assert.Single(calls) |> ignore

        calls.Clear()

        let failedDom timeout arguments =
            calls.Add(timeout, arguments)

            if arguments = [ "--version" ] then
                completion
            else
                raise failure

        let domFailure =
            Assert.Throws<TestShared.ChildProcess.ChildProcessTimeout>(fun () ->
                TestShared.BrowserDriver.observeAndRun (fun () -> 0L) failedDom ignore [ "--dump-dom" ]
                |> ignore)

        Assert.Same(failure, domFailure)
        Assert.Equal(2, calls.Count)
        calls.Clear()

        let badVersion timeout arguments =
            calls.Add(timeout, arguments)

            { completion with
                ExitCode = 1
                StandardError = "synthetic driver failure"
            }

        let versionFailure =
            Assert.Throws<Exception>(fun () ->
                TestShared.BrowserDriver.observeAndRun (fun () -> 0L) badVersion ignore [ "--dump-dom" ]
                |> ignore)

        Assert.Contains("exit 1", versionFailure.Message)
        Assert.Contains("synthetic driver failure", versionFailure.Message)
        Assert.Single(calls) |> ignore

    // This pilot deliberately uses the raw public factory. The SDD adapter owns
    // text decoding; adding the producer's text API must not change its byte path.
    // Sessions and original Run tasks remain rooted after a failed assertion
    // until accountable testhost/outer cleanup observes both.
    type private PilotCall =
        { Session: IProcessSession
          OriginalBudget: ExecutionBudget
          mutable RunTask: Task<ExecutionResult> option }

    let private pilotCustody = ResizeArray<PilotCall>()

    let private prepareRawPilot policy script workMs =
        let domain = { Id = Guid.NewGuid(); Frequency = Stopwatch.Frequency }
        let clock =
            { new IMonotonicClock with
                member _.Now() = { Domain = domain; Ticks = Stopwatch.GetTimestamp() } }
        let budget =
            Budget.create (clock.Now()) (TimeSpan.FromMilliseconds(float (workMs + 5_000)))
                (TimeSpan.FromMilliseconds 5_000.)
            |> function Ok value -> value | Error error -> failwithf "%A" error
        let digest =
            File.ReadAllBytes(Path.Combine(TestShared.repoRoot, "tests/FS.GG.SDD.Commands.Tests/ChildProcessTests.fs"))
            |> SHA256.HashData |> Convert.ToHexStringLower
        let identity =
            { InvocationId = Guid.NewGuid(); Execution = None; LaunchOrdinal = 0UL
              SourceDigest = digest; ProfileId = "sdd-raw-consumer-pilot"; ProfileDigest = digest }
        let owner =
            { OwnerId = $"sdd-pilot-testhost:{Environment.ProcessId}"
              Evidence = { Kind = "caller-lifetime"; Reference = identity.InvocationId.ToString("D"); Digest = None } }
        let command: CommandSpec =
            { Executable = "/bin/sh"; Arguments = [ "-c"; script ]; WorkingDirectory = Path.GetTempPath()
              Environment = Replace Map.empty; Input = Closed
              Capture =
                { TotalBytes = 4096L; TerminalReserveBytes = 256
                  StdoutPrefixBytes = 1024; StdoutTailBytes = 256
                  StderrPrefixBytes = 1024; StderrTailBytes = 256
                  ReadChunkBytes = 512; MaximumFrameBytes = 1024; MaximumIncompleteLineBytes = 1024 }
              Capabilities = [ { Capability = BoundedReturnWithRetainedCustody; Required = true } ]
              Resources =
                { MemoryBytes = None; CpuQuota = None; CpuAffinity = None; MaxKernelTasks = None
                  MaxOwnedProcesses = None; MaxClrProcesses = None } }
        let session =
            (ProcessSessions.createFactoryWithRetirement policy).Prepare(command, identity, budget, clock, owner)
            |> function Ok value -> value | Error errors -> failwithf "%A" errors
        let call = { Session = session; OriginalBudget = budget; RunTask = None }
        pilotCustody.Add call
        call, budget, clock

    let private runRawPilot (call: PilotCall) cancellation =
        Assert.True(call.RunTask.IsNone, "pilot must request only one original Run task")
        let running = call.Session.RunAsync cancellation
        call.RunTask <- Some running
        running

    let private releaseRawPilot (call: PilotCall) budget (clock: IMonotonicClock) =
        Assert.Equal(Budget.describe call.OriginalBudget, Budget.describe budget)
        let running =
            call.RunTask
            |> Option.defaultWith (fun () -> failwith "No original Run task requested; pilot custody remains retained")
        let session = call.Session
        let mutable result = session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
        while not running.IsCompleted
              || not (match result.Cleanup.LocalTasks with Known(Settled, _) -> true | _ -> false) do
            let delay =
                Budget.clip (clock.Now()) Cleanup (TimeSpan.FromMilliseconds 5.) budget
                |> function Ok value -> value | Error error -> failwithf "%A" error
            Assert.True(delay > TimeSpan.Zero, "original pilot cleanup budget expired; session remains retained")
            Thread.Sleep delay
            result <- session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
        // Await only the observed-complete original task; never infer its result
        // from session cleanup or replace it with a second Run invocation.
        running.GetAwaiter().GetResult() |> ignore
        Assert.Equal(ResourcesReleased, session.TryRelease())
        pilotCustody.Remove call |> ignore
        Assert.Equal(Budget.describe budget, Budget.describe result.OriginalBudget)
        result

    [<Fact; Trait("tier", "slow")>]
    let ``raw consumer pilot retains undecodable transport bytes without text API admission`` () =
        if OperatingSystem.IsLinux() then
            let call, budget, clock =
                prepareRawPilot RetirementPolicy.BestEffortProcessTree "printf '\\377\\300A'" 5_000
            runRawPilot call CancellationToken.None |> ignore
            let result = releaseRawPilot call budget clock
            Assert.Equal<byte>([| 255uy; 192uy; 65uy |], result.Stdout.Prefix.ToArray())
            Assert.True(result.FirstFailure.IsNone)
            Assert.Equal(NotRequested, result.Stop)
            Assert.True(match result.Exit with Known(Exited 0, _) -> true | _ -> false)
            Assert.True(match result.Cleanup.WorkloadEmpty with Unknown _ -> true | _ -> false)

    [<Theory; Trait("tier", "slow")>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    let ``raw consumer pilot keeps cancellation and deadline causes under original custody`` cancelled =
        if OperatingSystem.IsLinux() then
            let call, budget, clock =
                prepareRawPilot RetirementPolicy.BestEffortProcessTree "printf ready; exec /bin/sleep 20" 500
            let session = call.Session
            use cancellation = new CancellationTokenSource()
            runRawPilot call cancellation.Token |> ignore
            let mutable observed = session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
            while not (match observed.Launch with Started _ -> true | _ -> false) do
                let delay =
                    Budget.clip (clock.Now()) Work (TimeSpan.FromMilliseconds 5.) budget
                    |> function Ok value -> value | Error error -> failwithf "%A" error
                Assert.True(delay > TimeSpan.Zero, "pilot never observed launch within original work budget")
                Thread.Sleep delay
                observed <- session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
            if cancelled then
                cancellation.Cancel()
            let result = releaseRawPilot call budget clock
            let expected = if cancelled then CallerCancellation else WorkDeadline
            Assert.True(match result.Stop with Requested(reason, _) -> reason = expected | _ -> false)
            Assert.True(match result.Cleanup.WorkloadEmpty with Unknown _ -> true | _ -> false)

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    let ``raw consumer pilot cannot release prepared no Run custody as terminal cleanup`` tree =
        if OperatingSystem.IsLinux() then
            let policy = if tree then RetirementPolicy.BestEffortProcessTree else RetirementPolicy.DirectChild
            let call, budget, clock = prepareRawPilot policy "exit 73" 5_000
            Assert.True(call.RunTask.IsNone)
            let refusal = Assert.Throws<Exception>(fun () -> releaseRawPilot call budget clock |> ignore)
            Assert.Equal("No original Run task requested; pilot custody remains retained", refusal.Message)
            Assert.True(pilotCustody |> Seq.exists (fun retained -> obj.ReferenceEquals(retained, call)))
            Assert.True(call.RunTask.IsNone)
            let prepared = call.Session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal(Prepared, prepared.Phase)
            Assert.Equal(NotStarted, prepared.Launch)
            Assert.False(match prepared.Cleanup.LocalTasks with Known(Settled, _) -> true | _ -> false)
            Assert.Equal(Budget.describe budget, Budget.describe prepared.OriginalBudget)
            runRawPilot call (CancellationToken(true)) |> ignore
            let result = releaseRawPilot call budget clock
            Assert.Equal(NotStarted, result.Launch)
            Assert.Equal(NoEffect, result.Cleanup.Custody)
