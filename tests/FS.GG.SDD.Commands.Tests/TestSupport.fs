namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.CommandTypes

module TestSupport =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let rec findRepoRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        else
            match directory.Parent with
            | null -> failwith "Could not locate repository root."
            | parent -> findRepoRoot parent

    let repoRoot = findRepoRoot (DirectoryInfo AppContext.BaseDirectory)

    let tempDirectory () =
        let path = Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory path |> ignore
        path

    let request (command: SddCommand) (root: string) =
        { Command = command
          ProjectRoot = root
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = false
          OverwritePolicy = RefuseUnsafe
          GeneratorVersion = SchemaVersionModule.currentGeneratorVersion() }

    let readRelative (root: string) (path: string) =
        File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
