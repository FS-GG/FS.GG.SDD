namespace FS.GG.SDD.Artifacts.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.TestShared
open Xunit

module TestSupport =
    // Delegates to the shared primitive (feature 067 / FR-010).
    let findRepoRoot = TestShared.findRepoRoot
    let repoRoot = TestShared.repoRoot

    let fixtureDirectory name =
        Path.Combine(repoRoot, "tests", "fixtures", "sdd-artifact-model", name)

    let normalizedFixtureDirectory name =
        Path.Combine(repoRoot, "tests", "fixtures", "normalized-work-model", name)

    let relativePath root path =
        Path.GetRelativePath(root, path).Replace('\\', '/')

    let snapshots name =
        let root = fixtureDirectory name

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path ->
            ({ Path = relativePath root path
               Text = File.ReadAllText path }
            : FileSnapshot))
        |> Seq.toList

    let normalizedSnapshots name =
        let root = normalizedFixtureDirectory name

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path ->
            ({ Path = relativePath root path
               Text = File.ReadAllText path }
            : FileSnapshot))
        |> Seq.toList

    let snapshot name path =
        snapshots name |> List.find (fun snapshot -> snapshot.Path = path)

    let model name =
        Serialization.normalizeSnapshotsToWorkModel (snapshots name) "001-sdd-artifact-model"

    let normalizedModel name =
        Serialization.normalizeSnapshotsToWorkModel (normalizedSnapshots name) "002-normalized-work-model"

    let generationRequest name =
        ({ WorkId = "002-normalized-work-model"
           Snapshots = normalizedSnapshots name
           GeneratorVersion = SchemaVersion.currentGeneratorVersion ()
           ExpectedOutputPath = None }
        : WorkModel.WorkModelGenerationRequest)

    let generationResult name =
        Serialization.generateWorkModel (generationRequest name)

    let currencyDiagnostics name =
        Serialization.checkGeneratedWorkModelCurrency
            (normalizedSnapshots name)
            "002-normalized-work-model"
            (SchemaVersion.currentGeneratorVersion ())

    let assertDiagnostic id (model: WorkModel.WorkModel) =
        let seen =
            String.Join(", ", model.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id))

        Assert.True(
            model.Diagnostics |> List.exists (fun diagnostic -> diagnostic.Id = id),
            $"Expected diagnostic '{id}' but saw: {seen}"
        )

    let assertNoBlockingDiagnostics (model: WorkModel.WorkModel) =
        let blocking = WorkModel.blockingDiagnostics model
        let seen = String.Join(", ", blocking |> List.map (fun diagnostic -> diagnostic.Id))
        Assert.True(List.isEmpty blocking, $"Expected no blocking diagnostics but saw: {seen}")
