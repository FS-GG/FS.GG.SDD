namespace FS.GG.SDD.TestShared

open System
open System.Diagnostics
open System.IO
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

        /// Start `startInfo` and run it to completion under `timeoutMs`, capturing both streams.
        /// `None` when the child could not be started. Redirection is forced on here so a caller
        /// cannot opt out of the concurrent drain that makes the bound reachable.
        let tryRunBounded (timeoutMs: int) (startInfo: ProcessStartInfo) : Completion option =
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.UseShellExecute <- false

            // A missing executable *throws* (Win32Exception) rather than returning null, so both
            // failure shapes have to be folded into `None` — matching `AcceptanceSupport`.
            let started =
                try
                    Process.Start startInfo |> Option.ofObj
                with _ ->
                    None

            match started with
            | None -> None
            | Some started ->
                use proc = started

                // Both readers are in flight BEFORE the wait, so neither pipe can back up and
                // stall the child. This is what keeps `WaitForExit timeoutMs` reachable.
                let stdout = proc.StandardOutput.ReadToEndAsync()
                let stderr = proc.StandardError.ReadToEndAsync()

                if not (proc.WaitForExit timeoutMs) then
                    try
                        proc.Kill(entireProcessTree = true)
                    with _ ->
                        ()

                    let commandLine =
                        String.concat " " (startInfo.FileName :: List.ofSeq startInfo.ArgumentList)

                    failwithf "Child process timed out after %d ms: %s" timeoutMs commandLine

                // `WaitForExit(int)` does not itself flush the async readers (unlike the
                // parameterless overload), so reap them explicitly. The child has exited, both
                // pipes are at EOF, and these complete immediately.
                Some
                    { ExitCode = proc.ExitCode
                      StandardOutput = stdout.GetAwaiter().GetResult()
                      StandardError = stderr.GetAwaiter().GetResult() }

        /// As `tryRunBounded`, but a child that cannot be started is a test failure rather than a
        /// value to branch on — the shape nearly every call site wants.
        let runBounded (timeoutMs: int) (startInfo: ProcessStartInfo) : Completion =
            match tryRunBounded timeoutMs startInfo with
            | Some completion -> completion
            | None -> failwithf "Failed to start `%s`." startInfo.FileName

        /// Run real `git` in a directory and return (exitCode, trimmed stdout). An unstartable
        /// `git` reports `-1, ""` so a probe can branch on it. Was duplicated verbatim (and
        /// deadlock-prone) in ScaffoldCommandTests and GitignoreNegationTests.
        let git (root: string) (args: string list) =
            let info = ProcessStartInfo(FileName = "git", WorkingDirectory = root)
            args |> List.iter info.ArgumentList.Add

            match tryRunBounded 60_000 info with
            | None -> -1, ""
            | Some completion -> completion.ExitCode, completion.StandardOutput.Trim()

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
