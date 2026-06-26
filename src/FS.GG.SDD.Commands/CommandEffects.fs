namespace FS.GG.SDD.Commands

open System
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
          Diagnostic = None }

    let failure (effect: CommandEffect) (snapshot: FileSnapshot option) diagnostic =
        { Effect = effect
          Succeeded = false
          Snapshot = snapshot
          Diagnostic = Some diagnostic }

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
