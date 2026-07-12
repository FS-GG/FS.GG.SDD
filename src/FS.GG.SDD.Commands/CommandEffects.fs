namespace FS.GG.SDD.Commands

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes

module CommandEffects =
    let fullPath (projectRoot: string) (relativePath: string) =
        Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))

    let parentDirectory (path: string) =
        match Path.GetDirectoryName path with
        | null
        | "" -> "."
        | parent -> parent

    /// Commit `text` to `absolute` so that no reader ever observes a partial write.
    ///
    /// `File.WriteAllText` opens with `FileMode.Create`: the destination is truncated to zero and then
    /// refilled. Anything reading in between — an agent harness, a file watcher, a second `fsgg-sdd`
    /// process — sees a prefix. That is how a `spec.md` was briefly observable holding only its
    /// boilerplate `FR-001` placeholder (FS.GG.SDD#164, FS.GG.Audio feedback §3.9).
    ///
    /// Instead: fill a sibling temp file, then rename it over the destination. A *sibling* shares the
    /// destination's volume, which is exactly what makes the rename atomic (`rename(2)` /
    /// `MoveFileEx(MOVEFILE_REPLACE_EXISTING)`). `File.Replace` would also be atomic but requires the
    /// destination to already exist, and `WriteFile` must create-or-replace uniformly.
    ///
    /// The temp never survives the call: `finally` removes it on any failure, so a crashed write leaves
    /// the destination's prior bytes intact and no residue. The leading `.` keeps it out of the
    /// `readiness/**` and `work/**` globs even inside the crash window, and the GUID never reaches any
    /// report, digest, or artifact — determinism contracts observe committed bytes only.
    ///
    /// The rename *replaces the destination's inode*, so without care the temp's mode (whatever the
    /// process umask yields, typically `0644`) would silently become the artifact's mode — where
    /// `File.WriteAllText` used to preserve it by writing through the existing inode. That regresses in
    /// both directions: a `chmod +x` script loses its exec bit, and a deliberately `0600` artifact
    /// becomes world-readable. So carry the destination's mode onto the temp before the rename.
    ///
    /// Two inode-identity consequences remain, both accepted: a symlink at `absolute` is *replaced*
    /// rather than written through, and a hardlink elsewhere stops tracking the file. No SDD artifact
    /// path is a symlink or a hardlink.
    let private writeFileAtomic (absolute: string) (text: string) =
        let directory = parentDirectory absolute

        let temp =
            Path.Combine(directory, $".{Path.GetFileName absolute}.{Guid.NewGuid():N}.tmp")

        try
            File.WriteAllText(temp, text)

            if not (OperatingSystem.IsWindows()) && File.Exists absolute then
                File.SetUnixFileMode(temp, File.GetUnixFileMode absolute)

            File.Move(temp, absolute, true)
        finally
            // `File.Delete` is a no-op on a missing path, and a successful `File.Move` has already
            // unlinked the temp. Swallow a cleanup failure so it cannot replace the in-flight write
            // exception — the caller's `toolDefect` must report why the *write* failed, not why the
            // cleanup did.
            try
                File.Delete temp
            with _ ->
                ()

    let snapshotIfExists (projectRoot: string) (path: string) =
        let absolute = fullPath projectRoot path

        if File.Exists absolute then
            Some(
                { Path = path
                  Text = File.ReadAllText absolute }
                : FileSnapshot
            )
        else
            None

    let directorySnapshot (projectRoot: string) (path: string) =
        let absolute = fullPath projectRoot path

        if Directory.Exists absolute then
            let entries =
                Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories)
                |> Seq.map (fun file -> Path.GetRelativePath(projectRoot, file).Replace('\\', '/'))
                |> Seq.sort
                |> String.concat "\n"

            Some({ Path = path; Text = entries }: FileSnapshot)
        else
            None

    /// The tag decides. An absent file is always writable, and an identical rewrite is a no-op
    /// whatever the kind. Otherwise only the two tool-owned kinds may replace bytes: a
    /// `GeneratedView` the tool alone produces, and a `HybridArtifact` whose text is already the
    /// merge of re-derived tool regions with the author's preserved ones. `AuthoredSource` joins
    /// `StructuredSource` and `AgentGuidanceTarget` in refusing — the tool never writes authored
    /// prose, so an effect that claims it is a tool defect, caught here rather than on disk.
    let canOverwrite (kind: ArtifactWriteKind) (existing: FileSnapshot option) (text: string) =
        match existing, kind with
        | None, _ -> true
        | Some snapshot, _ when snapshot.Text = text -> true
        | Some _, HybridArtifact _ -> true
        | Some _, GeneratedView -> true
        | Some _, _ -> false

    let success (effect: CommandEffect) (snapshot: FileSnapshot option) =
        { Effect = effect
          Succeeded = true
          Snapshot = snapshot
          Process = None
          Confirmed = None
          Diagnostic = None }

    let failure (effect: CommandEffect) (snapshot: FileSnapshot option) diagnostic =
        { Effect = effect
          Succeeded = false
          Snapshot = snapshot
          Process = None
          Confirmed = None
          Diagnostic = Some diagnostic }

    // The per-stream retention bound for captured provider stdout/stderr (feature 054,
    // E4). Content beyond this many characters is drained (deadlock-safe) but neither
    // retained nor buffered, so a runaway child cannot exhaust parent memory (#68); the
    // stream's truncation flag records that a tail was dropped (FR-005).
    let providerOutputCapChars = 65536

    // The wall-clock ceiling for a single child process (#68). `dotnet new install/update/
    // create`, `dotnet tool update`, and the git probe all launch at this edge; without a
    // bound a wedged child hangs the CLI forever. The default is generous enough for a cold
    // network restore; `FSGG_SDD_PROCESS_TIMEOUT_MS` overrides it (a test uses a tiny value
    // to exercise the kill path). A non-positive / unparseable value falls back to the default.
    let private defaultProcessTimeoutMs = 600_000

    // The synthesized exit code reported when a child is killed on timeout: a nonzero,
    // fail-closed value (the conventional `timeout(1)` code) so handlers classify a hung
    // process as a provider/step failure rather than mistaking it for success.
    let private processTimeoutExitCode = 124

    let processTimeoutMs () =
        match Int32.TryParse(Environment.GetEnvironmentVariable "FSGG_SDD_PROCESS_TIMEOUT_MS") with
        | true, ms when ms > 0 -> ms
        | _ -> defaultProcessTimeoutMs

    // Drain a redirected stream to EOF (so the child never blocks on a full pipe, R1) while
    // retaining at most the cap in memory (R2/#68): bytes past the cap are counted for the
    // truncation flag but discarded, not buffered. Reads in bounded chunks; runs as a hot
    // task so both streams drain concurrently with `WaitForExit`.
    let private readCappedAsync (reader: TextReader) : System.Threading.Tasks.Task<string * bool> =
        task {
            let builder = System.Text.StringBuilder()
            let buffer = Array.zeroCreate<char> 8192
            let mutable truncated = false
            let mutable reading = true

            while reading do
                let! read = reader.ReadAsync(buffer, 0, buffer.Length)

                if read = 0 then
                    reading <- false
                else
                    let remaining = providerOutputCapChars - builder.Length

                    if remaining > 0 then
                        builder.Append(buffer, 0, min read remaining) |> ignore

                    if read > remaining then
                        truncated <- true

            return builder.ToString(), truncated
        }

    // Edge interpreter for `RunProcess`: launches a real child process, captures its
    // exit code and (bounded) stdout/stderr, and snapshots the working directory
    // afterwards so the handler can diff produced paths. Honors DryRun (plans without
    // spawning). Both streams are read concurrently before `WaitForExit` so a child that
    // fills one pipe while the parent bounds the other cannot deadlock (R1); the retained
    // content is capped per stream (R2) and decoded as UTF-8 with replacement so non-UTF-8
    // / binary bytes cannot throw or corrupt the JSON report (R9).
    let runProcess
        (projectRoot: string)
        (effect: CommandEffect)
        (command: string)
        (args: string list)
        (workingDir: string)
        =
        let absolute = fullPath projectRoot workingDir
        Directory.CreateDirectory absolute |> ignore

        // Non-throwing UTF-8 decode: invalid bytes become replacement characters (CAP-5).
        let decoding = System.Text.UTF8Encoding(false, false)

        let startInfo =
            ProcessStartInfo(
                FileName = command,
                WorkingDirectory = absolute,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = decoding,
                StandardErrorEncoding = decoding
            )

        args |> List.iter startInfo.ArgumentList.Add

        // The fully-resolved command line as executed (program + args) — FR-001.
        let commandLine = String.concat " " (command :: args)

        try
            use proc = Process.Start startInfo

            match proc with
            | null ->
                { Effect = effect
                  Succeeded = true
                  Snapshot = None
                  Process =
                    Some
                        { Started = false
                          ExitCode = -1
                          Command = commandLine
                          StandardOutput = ""
                          StandardOutputTruncated = false
                          StandardError = ""
                          StandardErrorTruncated = false }
                  Confirmed = None
                  Diagnostic = None }
            | proc ->
                // Read both pipes concurrently so neither can block the child (R1); retain at
                // most the cap per stream (R2).
                let stdoutTask = readCappedAsync proc.StandardOutput
                let stderrTask = readCappedAsync proc.StandardError
                let timeoutMs = processTimeoutMs ()

                if proc.WaitForExit timeoutMs then
                    // Exited within the bound: reap the reader tasks and report normally.
                    let stdout, stdoutTruncated = stdoutTask.GetAwaiter().GetResult()
                    let stderr, stderrTruncated = stderrTask.GetAwaiter().GetResult()

                    { Effect = effect
                      Succeeded = true
                      Snapshot = directorySnapshot projectRoot workingDir
                      Process =
                        Some
                            { Started = true
                              ExitCode = proc.ExitCode
                              Command = commandLine
                              StandardOutput = stdout
                              StandardOutputTruncated = stdoutTruncated
                              StandardError = stderr
                              StandardErrorTruncated = stderrTruncated }
                      Confirmed = None
                      Diagnostic = None }
                else
                    // Timed out: kill the whole tree, reap, and report a fail-closed nonzero
                    // exit so the handler classifies it as a provider/step failure (#68) — an
                    // incomplete run is never mistaken for success. The termination note is
                    // appended to stderr so the report can explain the failure.
                    (try
                        proc.Kill true
                     with _ ->
                         ())

                    proc.WaitForExit()
                    let stdout, stdoutTruncated = stdoutTask.GetAwaiter().GetResult()
                    let capturedErr, stderrTruncated = stderrTask.GetAwaiter().GetResult()

                    let timeoutNote =
                        $"fsgg-sdd: process timed out after {timeoutMs} ms and was terminated: {commandLine}"

                    let stderr =
                        if String.IsNullOrEmpty capturedErr then
                            timeoutNote
                        else
                            capturedErr + "\n" + timeoutNote

                    { Effect = effect
                      Succeeded = true
                      Snapshot = directorySnapshot projectRoot workingDir
                      Process =
                        Some
                            { Started = true
                              ExitCode = processTimeoutExitCode
                              Command = commandLine
                              StandardOutput = stdout
                              StandardOutputTruncated = stdoutTruncated
                              StandardError = stderr
                              StandardErrorTruncated = stderrTruncated }
                      Confirmed = None
                      Diagnostic = None }
        with ex ->
            // The provider engine/command could not be launched: surfaced as
            // scaffold.providerUnavailable by the handler (Started = false). The launch
            // error is retained on StandardError so the report can explain the failure (R4).
            { Effect = effect
              Succeeded = true
              Snapshot = None
              Process =
                Some
                    { Started = false
                      ExitCode = -1
                      Command = commandLine
                      StandardOutput = ""
                      StandardOutputTruncated = false
                      StandardError = ex.Message
                      StandardErrorTruncated = false }
              Confirmed = None
              Diagnostic = None }

    // Edge interpreter for `Confirm` (feature 053, confirm-effect contract). Under `DryRun`
    // it never mutates and never reads stdin (`Some false`). Otherwise it writes the step
    // diff/prompt and reads one line from `Console.In` (`y`/`yes`, case-insensitive →
    // confirmed). A `Confirm` is only ever emitted on the interactive path (the pure core
    // refuses a non-interactive run without `--yes` up front, and `--yes` applies directly
    // without emitting `Confirm`), so this stdin read is only reached interactively; EOF/
    // redirected-empty stdin returns null → declined, never a hang. The prompt text is
    // presentation-only and excluded from the deterministic json; the decision
    // (`Confirmed`) is the contract-relevant fact. The prompt is written to **stderr** (not
    // stdout) so `fsgg-sdd upgrade > out.json` from a TTY cannot prepend prompt bytes to the
    // deterministic JSON report on stdout (#68).
    let confirm (dryRun: bool) (effect: CommandEffect) (prompt: string) =
        let decision =
            if dryRun then
                false
            else
                Console.Error.Write prompt
                Console.Error.Flush()

                match (Option.ofObj (Console.In.ReadLine()) |> Option.defaultValue "").Trim().ToLowerInvariant() with
                | "y"
                | "yes" -> true
                | _ -> false

        { Effect = effect
          Succeeded = true
          Snapshot = None
          Process = None
          Confirmed = Some decision
          Diagnostic = None }

    let interpret (projectRoot: string) (dryRun: bool) (effect: CommandEffect) =
        try
            match effect with
            | ReadFile path ->
                match snapshotIfExists projectRoot path with
                | Some snapshot -> success effect (Some snapshot)
                | None -> success effect None
            | EnumerateDirectory path ->
                match directorySnapshot projectRoot path with
                | Some snapshot -> success effect (Some snapshot)
                | None -> success effect None
            | CreateDirectory path ->
                let absolute = fullPath projectRoot path

                let existing =
                    if Directory.Exists absolute then
                        Some({ Path = path; Text = "<directory>" }: FileSnapshot)
                    else
                        None

                if not dryRun then
                    Directory.CreateDirectory absolute |> ignore

                success effect existing
            | WriteFile(path, text, kind) ->
                let existing = snapshotIfExists projectRoot path

                // The bytes are already on disk. Skip the commit entirely rather than re-committing
                // identical content: `writeFileAtomic` renames a fresh inode over the destination, so a
                // no-op write would still unlink the old inode — replacing a symlink with a regular file,
                // detaching hardlinks, and churning every inode-tracking watcher on an unchanged
                // `refresh`. The truncating write it replaced had no such side effect, so this keeps a
                // no-op run genuinely no-op. `ArtifactOperation.NoChange` is unchanged: it is derived from
                // `existing` at report assembly and never depended on the write happening.
                let unchanged =
                    match existing with
                    | Some snapshot -> snapshot.Text = text
                    | None -> false

                if canOverwrite kind existing text then
                    if not dryRun && not unchanged then
                        let absolute = fullPath projectRoot path
                        Directory.CreateDirectory(parentDirectory absolute) |> ignore
                        writeFileAtomic absolute text

                    success effect existing
                else
                    failure effect existing (unsafeOverwrite path)
            | RunProcess(command, args, workingDir) ->
                if dryRun then
                    success effect None
                else
                    runProcess projectRoot effect command args workingDir
            | SetExecutable path ->
                if dryRun then
                    success effect None
                else
                    try
                        let absolute = fullPath projectRoot path

                        let executable =
                            File.GetUnixFileMode absolute
                            ||| UnixFileMode.UserExecute
                            ||| UnixFileMode.GroupExecute
                            ||| UnixFileMode.OtherExecute

                        File.SetUnixFileMode(absolute, executable)
                        success effect None
                    with _ ->
                        // Read-only FS, non-Unix host, or a missing file: reported as a
                        // skipped/partial make-executable (FR-005, US2-AC3), never a tool
                        // defect. Caught here so the outer handler never escalates it.
                        { Effect = effect
                          Succeeded = false
                          Snapshot = None
                          Process = None
                          Confirmed = None
                          Diagnostic = None }
            | Confirm(_, prompt) -> confirm dryRun effect prompt
        with ex ->
            let path = CommandTypes.effectPath effect
            failure effect None (toolDefect path ex.Message)

    let interpretAll (projectRoot: string) (dryRun: bool) (effects: CommandEffect list) =
        effects |> List.map (interpret projectRoot dryRun)

    /// Drive an MVU command to its final report: initialize, then interpret produced effects
    /// and fold their interpreted results back through `update` until no effects remain, build
    /// the report, and resolve it. This is the single canonical run loop shared by the CLI
    /// entry point and the validation harness (feature 061 / issue #71) — previously duplicated
    /// verbatim, where any divergence silently changed validate-vs-CLI behavior.
    let driveToReport (request: CommandRequest) : CommandReport =
        let model, effects = CommandWorkflow.init request

        let rec interpretUntilIdle state pendingEffects =
            match pendingEffects with
            | [] -> state
            | current ->
                let results = interpretAll request.ProjectRoot request.DryRun current

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (currentState, accumulatedEffects) result ->
                            let updatedState, producedEffects =
                                CommandWorkflow.update (EffectInterpreted result) currentState

                            updatedState, accumulatedEffects @ producedEffects)
                        (state, [])

                interpretUntilIdle nextState nextEffects

        let finalModel =
            interpretUntilIdle model effects
            |> fun state -> CommandWorkflow.update BuildReport state |> fst

        finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)
