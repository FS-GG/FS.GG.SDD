namespace FS.GG.SDD.TestShared

open System
open System.Diagnostics
open System.IO
open System.Threading.Tasks
open Xunit

/// Feature 067 (FR-005 / FR-007 / FR-010 / FR-011): the single home for cross-project test
/// primitives that were previously copy-pasted per assembly. Linked into every test project via
/// `<Compile Include="../Shared/TestShared.fs" />`; each project's own `TestSupport` delegates to
/// these so its many call sites stay stable. No production code depends on this.
module TestShared =

    /// Repository root — walk up to the solution file. Single definition (was duplicated ×4).
    let rec findRepoRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        else
            match directory.Parent with
            | null -> failwith "Could not locate repository root."
            | parent -> findRepoRoot parent

    let repoRoot = findRepoRoot (DirectoryInfo AppContext.BaseDirectory)

    /// Per-run temp root. Every `tempDirectory ()` nests under it, and the whole tree is deleted
    /// once at process exit (feature 067 / FR-007/FR-008) — failure-safe (runs even when a test
    /// throws) and cheap (one recursive delete, not ~800). Each `dotnet test` assembly is its own
    /// process, so this cleans up per-assembly.
    let private tempRootPrefix = "fsgg-sdd-tests-"

    /// Non-null (Path.GetFileName is nullable under `Nullable enable`).
    let private nonNull (value: string | null) =
        value |> Option.ofObj |> Option.defaultValue ""

    /// Delete a tree best-effort, per child, clearing read-only attributes first. `Directory.Delete
    /// (_, true)` aborts the whole sweep the moment it meets one read-only or still-held entry — and
    /// the scaffold fixtures leave read-only `.git` objects — which is why a single run root could
    /// orphan hundreds of subdirectories. Deleting each child independently means one stuck entry
    /// costs only itself.
    let rec private deleteTree (dir: string) =
        if Directory.Exists dir then
            for file in Directory.EnumerateFiles dir do
                try
                    File.SetAttributes(file, FileAttributes.Normal)
                    File.Delete file
                with _ ->
                    ()

            for child in Directory.EnumerateDirectories dir do
                deleteTree child

            try
                Directory.Delete(dir, false)
            with _ ->
                ()

    /// True when no live process owns this pid. `Process.GetProcessById` throws `ArgumentException`
    /// when the pid is not running — cross-platform, no `/proc` assumption.
    let private processIsDead (pid: int) =
        try
            use _ = System.Diagnostics.Process.GetProcessById pid
            false
        with _ ->
            true

    /// Reclaim run roots left by earlier test processes that have since exited. Each root is tagged
    /// with its owning process id (`fsgg-sdd-tests-<pid>-<guid>`); a root is swept only when that
    /// process is gone, so this is race-safe against sibling test assemblies running concurrently —
    /// their pid is still alive and their root is left untouched. This is the reliable cleanup path:
    /// under the VSTest host, process-exit handlers cannot finish deleting a large tree, so residue
    /// is instead reclaimed here at the start of the next run (self-healing, never unbounded).
    /// A root whose pid was reused by an unrelated live process is left in place (a rare, harmless
    /// leak); the 6-hour age fallback below only applies to roots whose name has no parseable pid.
    /// The owning process id encoded in `fsgg-sdd-tests-<pid>-<guid>`, if parseable.
    let private ownerPid (dir: string) =
        let segments = (Path.GetFileName dir |> nonNull).Split('-')

        if segments.Length >= 5 then
            match Int32.TryParse segments.[3] with
            | true, pid -> Some pid
            | _ -> None
        else
            None

    let private sweepDeadRoots () =
        try
            let ageCutoff = DateTime.UtcNow.AddHours(-6.0)

            for dir in Directory.EnumerateDirectories(Path.GetTempPath(), tempRootPrefix + "*") do
                try
                    let dead =
                        match ownerPid dir with
                        | Some pid -> processIsDead pid
                        | None -> Directory.GetLastWriteTimeUtc dir < ageCutoff

                    if dead then
                        deleteTree dir
                with _ ->
                    ()
        with _ ->
            ()

    let runTempRoot =
        sweepDeadRoots ()

        let root =
            Path.Combine(
                Path.GetTempPath(),
                sprintf "%s%d-%s" tempRootPrefix (Environment.ProcessId) (Guid.NewGuid().ToString("N"))
            )

        Directory.CreateDirectory root |> ignore
        root

    // Best-effort immediate cleanup on a clean exit (small roots); large roots the host kills early
    // are reclaimed by the next run's sweepDeadRoots.
    do AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> deleteTree runTempRoot)

    /// A fresh temp directory nested under the per-run root (swept en masse at exit). Signature
    /// matches the former per-project `tempDirectory` so call sites are unchanged.
    let tempDirectory () =
        let path = Path.Combine(runTempRoot, Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory path |> ignore
        path

    /// Write text to a root-relative path, creating parent directories. Single definition
    /// (was duplicated ×2 in the test tree).
    let writeRelative (root: string) (path: string) (text: string) =
        let absolute = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

        match Path.GetDirectoryName absolute with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.WriteAllText(absolute, text)

    /// The single bounded, deadlock-free way for a test to run a child process (FS.GG.SDD#212).
    ///
    /// A child's stdout and stderr are two independent OS pipes with a small kernel buffer (64 KiB
    /// on Linux). Draining them *sequentially* — `StandardOutput.ReadToEnd()` and only then
    /// `StandardError.ReadToEnd()` — deadlocks whenever the child's stderr exceeds that buffer: the
    /// child blocks in `write(2)` on stderr and can never exit, so the parent's stdout read never
    /// sees EOF and never reaches the stderr read. Worse, any `WaitForExit(timeoutMs)` placed after
    /// the reads is then *unreachable*, so the timeout that was supposed to bound the hang is dead
    /// code and the test run wedges forever (observed: an 18-minute hang on a blocked `fsgg-sdd
    /// refresh`, whose Blocked report puts ~38 KiB on stderr and nothing on stdout).
    ///
    /// So this module owns the invariant rather than restating it per call site: both pipes are
    /// redirected here, drained *concurrently*, and the wait is always bounded. A child that
    /// outlives `timeoutMs` is killed (whole tree) and reported as a failure, not a hang.
    module ChildProcess =

        open System.Collections.Generic
        open System.Security.Cryptography
        open System.Text
        open System.Threading
        open FS.GG.Execution.Contracts
        open FS.GG.Execution.Processes

        /// A child that ran to completion within its bound.
        type Completion =
            {
                ExitCode: int
                StandardOutput: string
                StandardError: string
            }

        /// Why a bounded run gave up. These are different events, and a caller can need to tell
        /// them apart: a *run smoke* (launch an app, require it to survive a grace window) reads
        /// `ChildOutlivedBound` as the healthy answer, whereas `PipesHeldAfterExit` means the
        /// child is already gone and its exit code was never observed — never a success.
        type TimeoutReason =
            /// The child never exited within `timeoutMs`; best-effort retirement was requested.
            /// The exception carries any unresolved caller custody independently.
            | ChildOutlivedBound
            /// The child exited, but an orphaned grandchild still held its output pipes when the
            /// drain grace elapsed, so the captured streams — and the exit code they belong to —
            /// are untrustworthy and were abandoned.
            | PipesHeldAfterExit

        /// A child that outlived its bound — either it never exited, or it exited but something it
        /// spawned still holds the pipes open. A distinct type from the plain `exn` a failed *start*
        /// raises, so a test asserting the bound cannot be satisfied by a child that never ran.
        type ChildProcessTimeout(reason: TimeoutReason, message: string) =
            inherit exn(message)

            /// Which bound fired. Callers that treat "still running" as success must branch on this
            /// rather than on `Message`, so a held-pipes abandonment is never read as a clean run.
            member _.Reason = reason

        /// Grace period for draining the pipes once the child has exited, and for reaping a child we
        /// just signalled. Deliberately a small *constant* rather than "whatever is left of
        /// `timeoutMs`": once the child is gone the drain is instantaneous (the reads are already at
        /// EOF), so the only thing this bounds is a surviving grandchild holding the write end. A
        /// remaining-budget bound would silently make the worst-case wall clock `2 × timeoutMs`,
        /// i.e. the number a call site writes would stop being the number enforced. Worst case here
        /// is `timeoutMs + drainGraceMs`.
        let private drainGraceMs = 5_000

        /// Keep an action failure primary when resource cleanup also fails. A returned
        /// value carries a cleanup failure separately so callers can retain both diagnostics.
        let withCleanupPreservingFailure (cleanup: unit -> unit) (action: unit -> 'a) : 'a * exn option =
            let cleanupFailure () =
                try
                    cleanup ()
                    None
                with error ->
                    Some error

            try
                let value = action ()
                value, cleanupFailure ()
            with original ->
                match cleanupFailure () with
                | None -> ()
                | Some secondary ->
                    original.Data["ChildProcessCleanupFailure"] <- secondary

                    // Diagnostic output is secondary too: a closed stderr must not hide
                    // the original failure. Keep the emitted cleanup message finite.
                    try
                        let message = secondary.Message

                        Console.Error.WriteLine(
                            "Child cleanup also failed: " + message.Substring(0, min 1_024 message.Length)
                        )
                    with _ ->
                        ()

                reraise ()

        /// An output or custody failure is distinct from a work timeout: smoke callers
        /// sometimes interpret ChildOutlivedBound as the expected live-app result.
        type ChildProcessCaptureFailure(message: string) =
            inherit exn(message)

        type private RetainedCall =
            { Session: IProcessSession
              mutable RunTask: Task<ExecutionResult> option }

        // Sessions own their native handles and outstanding tasks. Failed local release
        // remains visible for this test assembly's lifetime instead of detaching a reaper.
        let private retained = Dictionary<Guid, RetainedCall>()
        let private retentionGate = obj()
        let private maximumRetainedCalls = 16
        let private factory =
            ProcessSessions.createFactoryWithRetirement RetirementPolicy.BestEffortProcessTree

        let private release (call: RetainedCall) =
            match call.RunTask with
            | None -> false
            | Some run when not run.IsCompleted -> false
            | Some _ ->
                match call.Session.TryRelease() with
                | ResourcesReleased ->
                    lock retentionGate (fun () -> retained.Remove call.Session.Identity.InvocationId |> ignore)
                    true
                | ReleaseRefused _ -> false

        /// Actual caller-owned sessions still awaiting lawful local release. This is
        /// test-lifetime custody, not a count of descendants or containment proof.
        let retainedSessionCount () = lock retentionGate (fun () -> retained.Count)

        let private resolveExecutable (name: string) =
            let candidates =
                if Path.IsPathFullyQualified name then [ name ]
                elif name.Contains(Path.DirectorySeparatorChar) then [ Path.GetFullPath name ]
                else
                    let path = Environment.GetEnvironmentVariable "PATH" |> Option.ofObj |> Option.defaultValue ""
                    path.Split(Path.PathSeparator)
                    |> Array.map (fun directory -> Path.GetFullPath(Path.Combine(directory, name)))
                    |> Array.toList

            candidates
            |> List.tryFind File.Exists
            |> Option.defaultWith (fun () ->
                raise (System.ComponentModel.Win32Exception(2, $"Executable was not found: {name}")))

        let private sourceDigest () =
            File.ReadAllBytes(Path.Combine(repoRoot, "tests", "Shared", "TestShared.fs"))
            |> SHA256.HashData
            |> Convert.ToHexStringLower

        /// Fixed retained-byte allocations within the producer's combined256KiB cap.
        type CaptureProfile =
            | Balanced
            | StdoutReport

        let private commandSpec profile (info: ProcessStartInfo) =
            if not (String.IsNullOrEmpty info.Arguments) then
                invalidArg "startInfo" "Bounded test commands require a literal ArgumentList."

            if info.RedirectStandardInput then
                invalidArg "startInfo" "Bounded test commands require closed standard input."

            for encoding in [ info.StandardOutputEncoding; info.StandardErrorEncoding ] do
                match Option.ofObj encoding with
                | Some value when value.CodePage <> Encoding.UTF8.CodePage ->
                    invalidArg "startInfo" "Bounded test command capture requires UTF-8."
                | _ -> ()

            let stdoutBytes, stderrBytes =
                match profile with
                | Balanced -> 128 * 1024, 128 * 1024
                | StdoutReport -> 192 * 1024, 64 * 1024
            { Executable = resolveExecutable info.FileName
              Arguments = List.ofSeq info.ArgumentList
              WorkingDirectory =
                if String.IsNullOrEmpty info.WorkingDirectory then Environment.CurrentDirectory
                else Path.GetFullPath info.WorkingDirectory
              Environment =
                info.Environment
                |> Seq.choose (fun pair -> pair.Value |> Option.ofObj |> Option.map (fun value -> pair.Key, value))
                |> Map.ofSeq |> Replace
              Input = Closed
              Capture =
                { TotalBytes = 512L * 1024L; TerminalReserveBytes = 2048
                  StdoutPrefixBytes = stdoutBytes; StdoutTailBytes = 0
                  StderrPrefixBytes = stderrBytes; StderrTailBytes = 0
                  ReadChunkBytes = 4096; MaximumFrameBytes = 16384; MaximumIncompleteLineBytes = 16384 }
              Capabilities = [ { Capability = BoundedReturnWithRetainedCustody; Required = true } ]
              Resources =
                { MemoryBytes = None; CpuQuota = None; CpuAffinity = None
                  MaxKernelTasks = None; MaxOwnedProcesses = None; MaxClrProcesses = None } }

        let private runPrepared timeoutMs (command: CommandSpec) =
            let domain = { Id = Guid.NewGuid(); Frequency = Stopwatch.Frequency }
            let clock =
                { new IMonotonicClock with
                    member _.Now() = { Domain = domain; Ticks = Stopwatch.GetTimestamp() } }
            let budget =
                match Budget.create (clock.Now()) (TimeSpan.FromMilliseconds(float (int64 timeoutMs + int64 drainGraceMs)))
                                      (TimeSpan.FromMilliseconds(float drainGraceMs)) with
                | Ok value -> value
                | Error reason -> invalidArg "timeoutMs" ($"Invalid child budget: {reason}")
            let description = Budget.describe budget
            let profile = $"sdd-test-bcl-tree-v1;work+5000ms;stdout={command.Capture.StdoutPrefixBytes};stderr={command.Capture.StderrPrefixBytes};256KiB-retained;512KiB-charged;retained-custody"
            let identity =
                { InvocationId = Guid.NewGuid(); Execution = None; LaunchOrdinal = 0UL
                  SourceDigest = sourceDigest(); ProfileId = "sdd-test-bcl-tree-v1"
                  ProfileDigest = Encoding.UTF8.GetBytes profile |> SHA256.HashData |> Convert.ToHexStringLower }
            let owner =
                { OwnerId = $"sdd-test-assembly:{Environment.ProcessId}"
                  Evidence = { Kind = "caller-lifetime"; Reference = identity.InvocationId.ToString("D"); Digest = None } }
            let prepared =
                lock retentionGate (fun () ->
                    // Reserve caller custody before preparation, so a full registry
                    // never discards a prepared session, even without a launch effect.
                    let releasable = retained.Values |> Seq.toArray
                    for previous in releasable do release previous |> ignore
                    if retained.Count >= maximumRetainedCalls then
                        raise (ChildProcessCaptureFailure "Test assembly has unresolved child custody; launch refused.")
                    match factory.Prepare(command, identity, budget, clock, owner) with
                    | Error errors -> Error(exn ($"Child command preparation refused: {errors}"))
                    | Ok session ->
                        let call = { Session = session; RunTask = None }
                        retained.Add(identity.InvocationId, call)
                        Ok call)
            match prepared with
            | Error error -> Error error
            | Ok call ->
                let session = call.Session
                call.RunTask <- Some(session.RunAsync CancellationToken.None)
                let mutable observed = session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
                let mutable released = false
                let mutable doneWaiting = false
                let mutable earlyExit: (int * int64) option = None
                let mutable captureFailure = false
                let mutable workTimedOut = false
                let observeExit () =
                    match observed.Exit with
                    | Known(Exited code, stamp) when earlyExit.IsNone ->
                        match stamp.At with
                        | Some at when at.Ticks <= description.WorkExpiresAt -> earlyExit <- Some(code, at.Ticks)
                        | _ -> ()
                    | _ -> ()
                while not doneWaiting do
                    let now = clock.Now()
                    observeExit ()
                    if earlyExit.IsSome then workTimedOut <- false
                    let omitted = observed.Stdout.Totals.Omitted > 0L || observed.Stderr.Totals.Omitted > 0L
                    let explicitCaptureFailure =
                        match observed.Stop with
                        | Requested(OutputLimit, _) | Requested(SinkFailure, _) -> true
                        | _ -> false
                    if (omitted || explicitCaptureFailure) && not workTimedOut then
                        captureFailure <- true
                        session.RequestStopAsync(OutputLimit, CancellationToken.None).GetAwaiter().GetResult() |> ignore
                    if earlyExit.IsNone && now.Ticks >= description.WorkExpiresAt && not captureFailure
                       && (match observed.Launch with Started _ -> true | _ -> false) then
                        workTimedOut <- true
                    released <- release call
                    if released then
                        // Completion may settle between this poll and TryRelease.
                        // Classify the final authoritative observation, never a stale poll.
                        observed <- session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()
                        observeExit ()
                        if earlyExit.IsSome then workTimedOut <- false
                    let drainExpired =
                        match earlyExit with
                        | Some(_, at) -> float (now.Ticks - at) / float domain.Frequency * 1000.0 >= float drainGraceMs
                        | None -> false
                    doneWaiting <- released || drainExpired || now.Ticks >= description.TotalExpiresAt
                                   || (match observed.Launch with Ambiguous _ -> true | _ -> false)
                    if not doneWaiting then
                        Thread.Sleep 5
                        observed <- session.ObserveAsync(CancellationToken.None).GetAwaiter().GetResult()

                // The last successful release can reveal output arriving between polls.
                // Recheck capture refusal on that final snapshot before any success result.
                match observed.Stop with
                | Requested(OutputLimit, _) | Requested(SinkFailure, _) ->
                    // The producer retains the first stop cause. A polling deadline
                    // cannot relabel an earlier output failure as a live-app timeout.
                    captureFailure <- true
                    workTimedOut <- false
                | _ ->
                    captureFailure <- captureFailure || observed.Stdout.Totals.Omitted > 0L || observed.Stderr.Totals.Omitted > 0L
                let text (capture: StreamCapture) =
                    use bytes = new MemoryStream(capture.Prefix.ToArray(), false)
                    use reader = new StreamReader(bytes, Encoding.UTF8, true)
                    reader.ReadToEnd()
                let prefix (capture: StreamCapture) =
                    let value = text capture
                    let truncated = value.Length > 16384 || capture.Totals.Omitted > 0L
                    value.Substring(0, min 16384 value.Length) + (if truncated then " [truncated]" else "")
                let diagnostic = $"\nstdout: {prefix observed.Stdout}\nstderr: {prefix observed.Stderr}"
                let fail (error: exn) =
                    error.Data["ChildProcessSessionRetained"] <- not released
                    error.Data["ChildProcessCleanup"] <- observed.Cleanup.Disposition.ToString()
                    observed.FirstFailure |> Option.iter (fun cause ->
                        error.Data["ChildProcessFirstCause"] <- cause.Diagnostic.Code)
                    raise error
                if workTimedOut then
                    fail (ChildProcessTimeout(ChildOutlivedBound, $"Child process timed out after {timeoutMs} ms: {command.Executable}" + diagnostic))
                elif captureFailure then
                    fail (ChildProcessCaptureFailure("Child output exceeded bounded capture or a reader failed." + diagnostic))
                else
                    match observed.Launch with
                    | Refused refusal when released -> Error(exn ($"{refusal.Code}: {refusal.Message}"))
                    | _ ->
                        let eof capture = match capture.Reader with Known(Eof, _) -> true | _ -> false
                        match earlyExit with
                        | Some(code, _) when eof observed.Stdout && eof observed.Stderr && released ->
                            Ok { ExitCode = code; StandardOutput = text observed.Stdout; StandardError = text observed.Stderr }
                        | Some _ when not (eof observed.Stdout && eof observed.Stderr) ->
                            fail (ChildProcessTimeout(PipesHeldAfterExit, $"Child process exited but its output pipes were still held {drainGraceMs} ms later: {command.Executable}"))
                        | _ -> fail (ChildProcessCaptureFailure("Child outcome or local release remains unresolved." + diagnostic))

        /// Start the child, folding both failure shapes into `Error`: a missing executable *throws*
        /// (`Win32Exception`) rather than returning null, and a misconfigured `ProcessStartInfo`
        /// throws `InvalidOperationException`. The reason is preserved — "no such file" and "exec
        /// bit stripped" are different bugs and a triager needs to tell them apart.
        let private tryStart (startInfo: ProcessStartInfo) : Result<Process, exn> =
            try
                match Process.Start startInfo with
                | null -> Error(exn "Process.Start returned null.")
                | proc -> Ok proc
            with ex ->
                Error ex

        /// Run `startInfo`'s child to completion under `timeoutMs`, capturing both streams.
        /// `Error` only when the child could not be *started*; a child that cannot be bounded raises
        /// `ChildProcessTimeout`. Redirection is forced on here so a caller cannot opt out of the
        /// concurrent drain that makes the bound reachable.
        let private legacyRunCore (timeoutMs: int) (startInfo: ProcessStartInfo) : Result<Completion, exn> =
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.UseShellExecute <- false

            match tryStart startInfo with
            | Error ex -> Error ex
            | Ok started ->
                use proc = started

                let commandLine =
                    String.concat " " (startInfo.FileName :: List.ofSeq startInfo.ArgumentList)

                // Both readers are in flight BEFORE the wait, so neither pipe can back up and
                // stall the child. This is what keeps `WaitForExit timeoutMs` reachable.
                let stdout = proc.StandardOutput.ReadToEndAsync()
                let stderr = proc.StandardError.ReadToEndAsync()

                if not (proc.WaitForExit timeoutMs) then
                    let retirement = Stopwatch.StartNew()

                    // Best-effort: a tree we cannot fully kill (a descendant already reparented, or
                    // one that raced us) throws, and there is nothing further to do about it.
                    try
                        proc.Kill(entireProcessTree = true)
                    with _ ->
                        ()

                    // `Kill` only signals. Reap, so the child cannot outlive the test that spawned it.
                    proc.WaitForExit drainGraceMs |> ignore

                    // Reap and diagnostics share the same grace. A held or faulted reader
                    // cannot add another wait or replace the original timeout reason.
                    let remainingMs = max 0 (drainGraceMs - int retirement.ElapsedMilliseconds)

                    try
                        Task.WhenAll(stdout, stderr).Wait remainingMs |> ignore
                    with _ ->
                        ()

                    let capturedPrefix (reader: Task<string>) =
                        if reader.IsCompletedSuccessfully then
                            let text = reader.Result
                            let limit = 16_384
                            let prefix = text.Substring(0, min limit text.Length)

                            if text.Length > limit then
                                prefix + " [truncated]"
                            else
                                prefix
                        else
                            "[unavailable within drain grace]"

                    let diagnostics =
                        $"\nstdout: {capturedPrefix stdout}\nstderr: {capturedPrefix stderr}"

                    raise (
                        ChildProcessTimeout(
                            ChildOutlivedBound,
                            $"Child process timed out after {timeoutMs} ms: {commandLine}" + diagnostics
                        )
                    )

                // `WaitForExit(int)` returns when the CHILD exits — not when the pipes reach EOF, and
                // unlike the parameterless overload it does not flush the async readers. A grandchild
                // that inherited the write end therefore keeps both reads pending, so an *unbounded*
                // reap here would silently relocate the very hang this module exists to prevent.
                //
                // We cannot kill that grandchild: the child is already gone, so `Kill(entireProcessTree)`
                // has no tree to walk and the orphan has been reparented away from us. All we can
                // honestly do is refuse to wait for it, abandon the readers, and say so.
                if not (Task.WhenAll(stdout, stderr).Wait drainGraceMs) then
                    raise (
                        ChildProcessTimeout(
                            PipesHeldAfterExit,
                            $"Child process exited but its output pipes were still held {drainGraceMs} ms later "
                            + $"(an orphaned grandchild inherited them): {commandLine}"
                        )
                    )

                Ok
                    {
                        ExitCode = proc.ExitCode
                        StandardOutput = stdout.Result
                        StandardError = stderr.Result
                    }

        /// Run the shared session while preserving the public start/timeout/held-pipe
        /// adapter. A successful result requires complete capture and lawful local release.
        let private runSharedCore profile (timeoutMs: int) (startInfo: ProcessStartInfo) : Result<Completion, exn> =
            let command =
                try
                    if timeoutMs <= 0 then invalidArg "timeoutMs" "Child work timeout must be positive."
                    Ok(commandSpec profile startInfo)
                with error -> Error error
            match command with
            | Error error -> Error error
            | Ok value -> runPrepared timeoutMs value

        // The producer's qualified profile is Linux-only. Retain the previously
        // supported portable runner until those platforms have their own accepted
        // session backend; a Linux pass cannot remove their existing behavior.
        let private runCore timeoutMs startInfo =
            if OperatingSystem.IsLinux() then runSharedCore Balanced timeoutMs startInfo
            else legacyRunCore timeoutMs startInfo

        /// Run a child under `timeoutMs`. `None` when it could not be started — the shape a caller
        /// that wants to *probe* for an executable needs.
        let tryRunBounded (timeoutMs: int) (startInfo: ProcessStartInfo) : Completion option =
            runCore timeoutMs startInfo |> Result.toOption

        /// As `tryRunBounded`, but a child that cannot be started is a test failure rather than a
        /// value to branch on — the shape nearly every call site wants. The launch failure's reason
        /// is carried through, not flattened.
        let runBounded (timeoutMs: int) (startInfo: ProcessStartInfo) : Completion =
            match runCore timeoutMs startInfo with
            | Ok completion -> completion
            | Error ex -> raise (exn ($"Failed to start `{startInfo.FileName}`: {ex.Message}", ex))

        /// Select an explicit output contract without changing the balanced default.
        /// Omitted bytes remain a failure; this never grants a larger combined allocation.
        let runBoundedWithCapture profile timeoutMs (startInfo: ProcessStartInfo) : Completion =
            let result =
                if OperatingSystem.IsLinux() then runSharedCore profile timeoutMs startInfo
                else legacyRunCore timeoutMs startInfo
            match result with
            | Ok completion -> completion
            | Error ex -> raise (exn ($"Failed to start `{startInfo.FileName}`: {ex.Message}", ex))

        /// Run real `git` in a directory and return (exitCode, trimmed stdout). Was duplicated
        /// verbatim (and deadlock-prone) in ScaffoldCommandTests and GitignoreNegationTests.
        ///
        /// An unstartable `git` is a hard failure, not the old `-1, ""`. Callers read the exit code
        /// to decide truth (`check-ignore -q` → is it ignored; `rev-parse --is-inside-work-tree` →
        /// are we in a work tree), and each treats non-zero as a *meaningful answer*. Handing them a
        /// synthetic `-1` when git is merely absent would let the load-bearing ADR-0026 negation
        /// proofs pass green without git ever having run. (The old `| null ->` branch never fired:
        /// a missing executable throws.)
        let git (root: string) (args: string list) =
            let info = ProcessStartInfo(FileName = "git", WorkingDirectory = root)
            args |> List.iter info.ArgumentList.Add

            let completion = runBounded 60_000 info
            completion.ExitCode, completion.StandardOutput.Trim()

    /// Browser selection is explicit before launch; a started failure never selects another driver.
    module BrowserDriver =
        let select (resolve: string -> string option) (declared: string option) =
            match declared with
            | Some path ->
                if
                    String.IsNullOrWhiteSpace path
                    || path <> path.Trim()
                    || not (Path.IsPathFullyQualified path)
                    || Seq.exists Char.IsControl path
                then
                    invalidArg "CHROME_BIN" "CHROME_BIN must declare an absolute executable path."

                match resolve path with
                | Some executable -> executable
                | None -> failwith $"Declared CHROME_BIN executable is missing: {path}"
            | None ->
                [ "google-chrome"; "chromium" ]
                |> List.tryPick resolve
                |> Option.defaultWith (fun () -> failwith "Neither google-chrome nor chromium exists on PATH.")

        let resolveExecutable (path: string) =
            let candidates =
                if Path.IsPathFullyQualified path then
                    [ path ]
                else
                    match Environment.GetEnvironmentVariable "PATH" with
                    | null -> []
                    | searchPath ->
                        searchPath.Split(Path.PathSeparator)
                        |> Array.filter Path.IsPathFullyQualified
                        |> Array.map (fun directory -> Path.Combine(directory, path))
                        |> Array.toList

            candidates
            |> List.tryFind File.Exists
            |> Option.map (fun candidate ->
                let file = FileInfo candidate

                match file.ResolveLinkTarget true with
                | null -> file.FullName
                | target -> target.FullName)

        /// Selection, version observation and DOM capture share one budget. Only a failing child
        /// retires under ChildProcess's single drain grace; no second driver or deadline is created.
        let observeAndRun
            (elapsedMilliseconds: unit -> int64)
            (run: int -> string list -> ChildProcess.Completion)
            (observe: string -> unit)
            (arguments: string list)
            =
            let remaining () =
                let value = 60_000L - elapsedMilliseconds ()

                if value <= 0L then
                    failwith "Browser's 60000 ms total budget is exhausted."

                int value

            let version = run (min 5_000 (remaining ())) [ "--version" ]

            if version.ExitCode <> 0 || String.IsNullOrWhiteSpace version.StandardOutput then
                let prefix (value: string) =
                    value.Substring(0, min 1_024 value.Length)

                failwith
                    $"Selected browser failed its bounded version observation (exit {version.ExitCode}). stdout: {prefix version.StandardOutput}; stderr: {prefix version.StandardError}"

            let text = version.StandardOutput.Trim()
            observe (text.Substring(0, min 1_024 text.Length))
            run (remaining ()) arguments

    /// Unified public-surface baseline verification (feature 067 / FR-005). Set
    /// `FSGG_UPDATE_BASELINE=1` to re-capture intentionally; otherwise assert the captured surface
    /// equals the committed baseline (blank-line-filtered, sorted). This is exactly the behavior
    /// the Contracts.Tests baseline test carried before — now shared by all five baseline tests.
    module SurfaceBaseline =
        let verify (baselinePath: string) (capture: unit -> string array) =
            // Filter the same way as the committed side so a re-baseline round-trips exactly even if
            // a capture ever emitted a blank entry (today none do).
            let actual =
                capture () |> Array.filter (String.IsNullOrWhiteSpace >> not) |> Array.sort

            let committed () =
                File.ReadAllLines baselinePath
                |> Array.filter (String.IsNullOrWhiteSpace >> not)
                |> Array.sort

            // Regenerate only when the surface content actually changed, so re-running the switch
            // on an unchanged surface never reorders an existing (possibly non-canonical) baseline
            // — the committed baselines stay byte-identical (feature 067 / FR-006). On a genuine
            // change it rewrites the baseline in canonical sorted order.
            if
                Environment.GetEnvironmentVariable "FSGG_UPDATE_BASELINE" = "1"
                && actual <> committed ()
            then
                File.WriteAllLines(baselinePath, actual)

            Assert.Equal<string array>(committed (), actual)

    /// Byte-exact update-or-assert for a whole generated document (e.g. a readiness JSON view),
    /// where field order and every byte are load-bearing — the whole-document analogue of
    /// `SurfaceBaseline` (feature 068 / US1 / FR-002-003). `FSGG_UPDATE_BASELINE=1` re-captures the
    /// committed golden intentionally; otherwise assert the produced document is byte-identical to it.
    /// Reuses the same regeneration switch as every other baseline so re-pinning is one ritual.
    module Golden =
        let verify (goldenPath: string) (produce: unit -> string) =
            let actual = produce ()
            let committed () = File.ReadAllText goldenPath

            // Regenerate only on a genuine change so re-running the switch on an unchanged view never
            // rewrites the committed golden (stays byte-identical, mirroring SurfaceBaseline).
            if
                Environment.GetEnvironmentVariable "FSGG_UPDATE_BASELINE" = "1"
                && (not (File.Exists goldenPath) || actual <> committed ())
            then
                match Path.GetDirectoryName goldenPath with
                | null -> ()
                | dir -> Directory.CreateDirectory dir |> ignore

                File.WriteAllText(goldenPath, actual)

            Assert.True(
                File.Exists goldenPath,
                $"Missing golden {goldenPath}; run the test with FSGG_UPDATE_BASELINE=1 to capture it."
            )

            Assert.Equal(committed (), actual)

    /// The evidence "ladder": task ids `T001..T00n` derived from a range once (feature 067 /
    /// FR-011) instead of being hand-copied, plus the passing-evidence document that satisfies
    /// them. Byte-compatible with the former hardcoded `T001..T006` literal.
    module EvidenceLadder =
        let taskIds count =
            [ for i in 1..count -> sprintf "T%03d" i ]

        /// FS.GG.SDD#355. The proving test each ladder obligation cites.
        ///
        /// Before #349 this fixture declared `result: pass` and named NO artifact at all — the
        /// canonical "passing evidence" every smoke test ships was a bare self-attestation, and it
        /// was what taught the shape. Citing an artifact makes the fixture demonstrate a *satisfied*
        /// obligation rather than a merely *declared* one; `writeArtifacts` is what keeps the citation
        /// honest, because #349's gate now refuses a pass whose cited file is not on disk.
        let artifactPath (taskId: string) =
            sprintf "tests/EvidenceLadder.Tests/%sTests.fs" taskId

        let artifactPaths count = taskIds count |> List.map artifactPath

        /// The body of a proving test. Not compiled by any project — it stands in for the real test
        /// an author's obligation would cite, and it must EXIST, which is the whole point.
        let artifactText (taskId: string) =
            sprintf
                "module EvidenceLadder.Tests.%sTests\n\n// The proving test cited by the evidence ladder's obligation for %s.\n// It exists because `fsgg-sdd evidence` refuses a `result: pass` that cites a file which does\n// not (FS.GG.SDD#349) — a fixture that cited fiction is what #355 was filed about.\n"
                taskId
                taskId

        let private evidenceEntry index (taskId: string) =
            sprintf
                "  - id: EV%03d\n    kind: verification\n    subject:\n      type: task\n      id: %s\n    artifacts: [%s]\n    result: pass"
                index
                taskId
                (artifactPath taskId)

        /// evidence.yml declaring `result: pass` verification for each of `T001..T00count`, each
        /// citing the proving test at `artifactPath`. The citation only holds if the caller also
        /// materializes those files — see `writeArtifacts`.
        let passingTaskEvidence count =
            let entries =
                taskIds count |> List.mapi (fun i taskId -> evidenceEntry (i + 1) taskId)

            "schemaVersion: 1\nevidence:\n" + String.concat "\n" entries + "\n"

        /// Materialize every artifact `passingTaskEvidence count` cites, under `root`.
        /// Paired with it: declaring the citation without writing the file is exactly the defect.
        let writeArtifacts (root: string) count =
            // The directory is derived from the same literal `artifactPath` builds from, rather than
            // via `Path.GetDirectoryName` (which is nullable, and nullness warnings are errors here).
            let directory = Path.Combine(root, "tests", "EvidenceLadder.Tests")
            Directory.CreateDirectory directory |> ignore

            for taskId in taskIds count do
                File.WriteAllText(Path.Combine(root, artifactPath taskId), artifactText taskId)
