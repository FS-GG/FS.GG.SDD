namespace FS.GG.SDD.Artifacts.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open Xunit

module TestSupport =
    let rec findRepoRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        elif isNull directory.Parent then
            failwith "Could not locate repository root."
        else
            findRepoRoot directory.Parent

    let repoRoot = findRepoRoot (DirectoryInfo AppContext.BaseDirectory)

    let fixtureDirectory name =
        Path.Combine(repoRoot, "tests", "fixtures", "sdd-artifact-model", name)

    let relativePath root path =
        Path.GetRelativePath(root, path).Replace('\\', '/')

    let snapshots name =
        let root = fixtureDirectory name

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path -> ({ Path = relativePath root path; Text = File.ReadAllText path } : LifecycleArtifacts.FileSnapshot))
        |> Seq.toList

    let snapshot name path =
        snapshots name
        |> List.find (fun snapshot -> snapshot.Path = path)

    let model name =
        Serialization.normalizeSnapshotsToWorkModel (snapshots name) "001-sdd-artifact-model"

    let assertDiagnostic id (model: WorkModel.WorkModel) =
        let seen = String.Join(", ", model.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id))
        Assert.True(
            model.Diagnostics |> List.exists (fun diagnostic -> diagnostic.Id = id),
            $"Expected diagnostic '{id}' but saw: {seen}"
        )

    let assertNoBlockingDiagnostics (model: WorkModel.WorkModel) =
        let blocking = WorkModel.blockingDiagnostics model
        let seen = String.Join(", ", blocking |> List.map (fun diagnostic -> diagnostic.Id))
        Assert.True(List.isEmpty blocking, $"Expected no blocking diagnostics but saw: {seen}")
