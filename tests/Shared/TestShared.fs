namespace FS.GG.SDD.TestShared

open System
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
    /// A 6-hour age fallback catches the rare pid-reuse straggler.
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
        let absolute =
            Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

        match Path.GetDirectoryName absolute with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.WriteAllText(absolute, text)

    /// Unified public-surface baseline verification (feature 067 / FR-005). Set
    /// `FSGG_UPDATE_BASELINE=1` to re-capture intentionally; otherwise assert the captured surface
    /// equals the committed baseline (blank-line-filtered, sorted). This is exactly the behavior
    /// the Contracts.Tests baseline test carried before — now shared by all five baseline tests.
    module SurfaceBaseline =
        let verify (baselinePath: string) (capture: unit -> string array) =
            let actual = capture () |> Array.sort

            let committed () =
                File.ReadAllLines baselinePath
                |> Array.filter (String.IsNullOrWhiteSpace >> not)
                |> Array.sort

            // Regenerate only when the surface content actually changed, so re-running the switch
            // on an unchanged surface never reorders an existing (possibly non-canonical) baseline
            // — the committed baselines stay byte-identical (feature 067 / FR-006). On a genuine
            // change it rewrites the baseline in canonical sorted order.
            if Environment.GetEnvironmentVariable "FSGG_UPDATE_BASELINE" = "1" && actual <> committed () then
                File.WriteAllLines(baselinePath, actual)

            Assert.Equal<string array>(committed (), actual)

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
                taskIds count
                |> List.mapi (fun i taskId -> evidenceEntry (i + 1) taskId)

            "schemaVersion: 1\nevidence:\n" + String.concat "\n" entries + "\n"
