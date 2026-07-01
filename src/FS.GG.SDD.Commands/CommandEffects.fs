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

    let snapshotIfExists (projectRoot: string) (path: string) =
        let absolute = fullPath projectRoot path
        if File.Exists absolute then
            Some({ Path = path; Text = File.ReadAllText absolute } : FileSnapshot)
        else
            None

    let directorySnapshot (projectRoot: string) (path: string) =
        let absolute = fullPath projectRoot path

        if Directory.Exists absolute then
            let entries =
                Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories)
                |> Seq.map (fun file ->
                    Path.GetRelativePath(projectRoot, file).Replace('\\', '/'))
                |> Seq.sort
                |> String.concat "\n"

            Some({ Path = path; Text = entries } : FileSnapshot)
        else
            None

    let canOverwrite (kind: ArtifactWriteKind) (existing: FileSnapshot option) (text: string) =
        match existing, kind with
        | None, _ -> true
        | Some snapshot, _ when snapshot.Text = text -> true
        | Some _, AuthoredSource -> true
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

    // Edge interpreter for `RunProcess`: launches a real child process, captures the
    // exit code, and snapshots the working directory afterwards so the handler can
    // diff produced paths. Honors DryRun (plans without spawning). Process
    // stdout/stderr are drained to avoid deadlock but excluded from the contract.
    let runProcess (projectRoot: string) (effect: CommandEffect) (command: string) (args: string list) (workingDir: string) =
        let absolute = fullPath projectRoot workingDir
        Directory.CreateDirectory absolute |> ignore

        let startInfo =
            ProcessStartInfo(
                FileName = command,
                WorkingDirectory = absolute,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            )

        args |> List.iter startInfo.ArgumentList.Add

        try
            use proc = Process.Start startInfo

            match proc with
            | null ->
                { Effect = effect
                  Succeeded = true
                  Snapshot = None
                  Process = Some { Started = false; ExitCode = -1 }
                  Confirmed = None
                  Diagnostic = None }
            | proc ->
                proc.StandardOutput.ReadToEnd() |> ignore
                proc.StandardError.ReadToEnd() |> ignore
                proc.WaitForExit()

                { Effect = effect
                  Succeeded = true
                  Snapshot = directorySnapshot projectRoot workingDir
                  Process = Some { Started = true; ExitCode = proc.ExitCode }
                  Confirmed = None
                  Diagnostic = None }
        with _ ->
            // The provider engine/command could not be launched: surfaced as
            // scaffold.providerUnavailable by the handler (Started = false).
            { Effect = effect
              Succeeded = true
              Snapshot = None
              Process = Some { Started = false; ExitCode = -1 }
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
    // (`Confirmed`) is the contract-relevant fact.
    let confirm (dryRun: bool) (effect: CommandEffect) (prompt: string) =
        let decision =
            if dryRun then
                false
            else
                Console.Out.Write prompt
                Console.Out.Flush()

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
                        Some({ Path = path; Text = "<directory>" } : FileSnapshot)
                    else
                        None

                if not dryRun then
                    Directory.CreateDirectory absolute |> ignore

                success effect existing
            | WriteFile(path, text, kind) ->
                let existing = snapshotIfExists projectRoot path

                if canOverwrite kind existing text then
                    if not dryRun then
                        let absolute = fullPath projectRoot path
                        Directory.CreateDirectory(parentDirectory absolute) |> ignore
                        File.WriteAllText(absolute, text)

                    success effect existing
                else
                    failure effect existing (unsafeOverwrite path)
            | RunProcess(command, args, workingDir) ->
                if dryRun then success effect None
                else runProcess projectRoot effect command args workingDir
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
            | EmitStdout text ->
                if not dryRun then Console.Out.Write text
                success effect None
            | EmitStderr text ->
                if not dryRun then Console.Error.Write text
                success effect None
            | SetExitCode code ->
                if not dryRun then Environment.ExitCode <- code
                success effect None
        with ex ->
            let path = CommandTypes.effectPath effect
            failure effect None (toolDefect path ex.Message)

    let interpretAll (projectRoot: string) (dryRun: bool) (effects: CommandEffect list) =
        effects |> List.map (interpret projectRoot dryRun)
