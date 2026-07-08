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

        /// A child that ran to completion within its bound.
        type Completion =
            { ExitCode: int
              StandardOutput: string
              StandardError: string }

        /// A child that outlived its bound — either it never exited, or it exited but something it
        /// spawned still holds the pipes open. A distinct type from the plain `exn` a failed *start*
        /// raises, so a test asserting the bound cannot be satisfied by a child that never ran.
        type ChildProcessTimeout(message: string) =
            inherit exn(message)

        /// Grace period for draining the pipes once the child has exited, and for reaping a child we
        /// just signalled. Deliberately a small *constant* rather than "whatever is left of
        /// `timeoutMs`": once the child is gone the drain is instantaneous (the reads are already at
        /// EOF), so the only thing this bounds is a surviving grandchild holding the write end. A
        /// remaining-budget bound would silently make the worst-case wall clock `2 × timeoutMs`,
        /// i.e. the number a call site writes would stop being the number enforced. Worst case here
        /// is `timeoutMs + drainGraceMs`.
        let private drainGraceMs = 5_000

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
        let private runCore (timeoutMs: int) (startInfo: ProcessStartInfo) : Result<Completion, exn> =
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
                    // Best-effort: a tree we cannot fully kill (a descendant already reparented, or
                    // one that raced us) throws, and there is nothing further to do about it.
                    try
                        proc.Kill(entireProcessTree = true)
                    with _ ->
                        ()

                    // `Kill` only signals. Reap, so the child cannot outlive the test that spawned it.
                    proc.WaitForExit drainGraceMs |> ignore

                    raise (ChildProcessTimeout $"Child process timed out after {timeoutMs} ms: {commandLine}")

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
                            $"Child process exited but its output pipes were still held {drainGraceMs} ms later "
                            + $"(an orphaned grandchild inherited them): {commandLine}"
                        )
                    )

                Ok
                    { ExitCode = proc.ExitCode
                      StandardOutput = stdout.Result
                      StandardError = stderr.Result }

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

        let private evidenceEntry index (taskId: string) =
            sprintf
                "  - id: EV%03d\n    kind: verification\n    subject:\n      type: task\n      id: %s\n    result: pass"
                index
                taskId

        /// evidence.yml declaring `result: pass` verification for each of `T001..T00count`.
        let passingTaskEvidence count =
            let entries =
                taskIds count |> List.mapi (fun i taskId -> evidenceEntry (i + 1) taskId)

            "schemaVersion: 1\nevidence:\n" + String.concat "\n" entries + "\n"
