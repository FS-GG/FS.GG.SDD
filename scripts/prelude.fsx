#r "nuget: YamlDotNet, 16.3.0"
#r "../src/FS.GG.SDD.Artifacts/bin/Release/net10.0/FS.GG.SDD.Artifacts.dll"

open System
open System.IO
open FS.GG.SDD.Artifacts

let repoRoot =
    let rec findRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        elif isNull directory.Parent then
            failwith "Could not locate repository root."
        else
            findRoot directory.Parent

    findRoot (DirectoryInfo(Environment.CurrentDirectory))

let fixtureRoot =
    Path.Combine(repoRoot, "tests", "fixtures", "sdd-artifact-model", "valid-work-item")

let snapshots =
    Directory.EnumerateFiles(fixtureRoot, "*", SearchOption.AllDirectories)
    |> Seq.map (fun path ->
        ({ Path = Path.GetRelativePath(fixtureRoot, path).Replace('\\', '/')
           Text = File.ReadAllText path }
        : LifecycleArtifacts.FileSnapshot))
    |> Seq.toList

let workId =
    Identifiers.createWorkId "001-sdd-artifact-model"
    |> Result.defaultWith failwith

printfn "workId=%s" (Identifiers.workIdValue workId)

let model =
    Serialization.normalizeSnapshotsToWorkModel snapshots "001-sdd-artifact-model"

printfn "modelVersion=%s" model.ModelVersion
printfn "blockingDiagnostics=%d" (WorkModel.blockingDiagnostics model |> List.length)
printfn "requirements=%s" (model.Requirements |> List.map _.Id |> String.concat ",")
printfn "tasks=%s" (model.Tasks |> List.map _.Id |> String.concat ",")
printfn "governanceBoundaries=%s" (model.GovernanceBoundaries |> List.map _.Path |> String.concat ",")

let json = Serialization.serializeWorkModel model
printfn "jsonBytes=%d" (Text.Encoding.UTF8.GetByteCount json)
