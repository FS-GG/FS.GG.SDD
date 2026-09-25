namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open Xunit

module WorkModelCandidateBundleJoinTests =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Inventory = WorkModelCandidateInventoryPreview
    module Joint = WorkModelCandidateBundleJoinPreview

    let private workId = "sample"
    let private spec = "work/sample/spec.md"
    let private tasks = "work/sample/tasks.yml"
    let private other = "work/other/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-bundle-tree-join-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/other")) |> ignore
            try
                let bodies =
                    [ ".fsgg/project.yml", "schemaVersion: 1\n"
                      ".fsgg/sdd.yml", "schemaVersion: 1\n"
                      ".fsgg/agents.yml", "schemaVersion: 1\n"
                      spec, TestSupport.validSpec workId "Selected"
                      tasks, "schemaVersion: 1\ntasks: []\n" ]
                let selected: FileSnapshot list =
                    bodies |> List.map (fun (path, body) ->
                        File.WriteAllText(Path.Combine(root, path), body)
                        { Path = path; Text = body; RawBytes = None })
                let candidate: Bundle.Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                action root selected candidate
            finally Directory.Delete(root, true)

    let private bundle root selected candidate =
        match Bundle.verifyFromPinnedCoreSources root workId selected candidate with
        | Ok files -> files
        | Error reason -> failwithf "Bundle fixture refused: %A" reason

    let private tree root =
        match Physical.capturePinnedDiscovered root "work" Physical.ExactBytes with
        | Ok files -> files
        | Error reason -> failwithf "Tree fixture refused: %A" reason

    [<Fact>]
    let ``separately green changed spec refuses exact overlap`` () =
        fixture (fun root selected candidate ->
            let before = bundle root selected candidate
            File.WriteAllText(Path.Combine(root, spec), TestSupport.validSpec workId "Changed")
            let after = tree root
            // Red-before: each existing verifier succeeds on its own captured instant.
            Assert.True(Result.isOk(Bundle.verify workId selected before candidate))
            Assert.True(Result.isOk(Inventory.verify workId after))
            Assert.Equal(Error(Joint.RawMismatch spec),
                         Joint.verifyWithCapture (fun () -> Ok before) (fun () -> Ok after)
                                                 workId selected candidate))

    [<Fact>]
    let ``changed noncandidate work source also refuses exact overlap`` () =
        fixture (fun root selected candidate ->
            let before = bundle root selected candidate
            File.WriteAllText(Path.Combine(root, tasks), "schemaVersion: 1\ntasks: [changed]\n")
            let after = tree root
            Assert.True(Result.isOk(Inventory.verify workId after))
            Assert.Equal(Error(Joint.RawMismatch tasks),
                         Joint.verifyWithCapture (fun () -> Ok before) (fun () -> Ok after)
                                                 workId selected candidate))

    [<Fact>]
    let ``missing selected work file from tree refuses overlap`` () =
        fixture (fun root selected candidate ->
            let before = bundle root selected candidate
            File.Delete(Path.Combine(root, tasks))
            let after = tree root
            Assert.Equal(Error(Joint.MissingInTree tasks),
                         Joint.verifyWithCapture (fun () -> Ok before) (fun () -> Ok after)
                                                 workId selected candidate))

    [<Fact>]
    let ``stable bundle and complete work tree join with unrelated candidate`` () =
        fixture (fun root selected candidate ->
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec "other" "Other")
            match Joint.verifyPhysical root workId selected candidate with
            | Ok preview ->
                Assert.Equal<string list>([ other; spec ], preview.CandidatePaths)
                Assert.Equal(5, preview.BundlePaths.Length)
            | Error reason -> failwithf "Stable joint preview refused: %A" reason)

    [<Fact>]
    let ``physical duplicate candidate refuses inventory stage`` () =
        fixture (fun root selected candidate ->
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec workId "Duplicate")
            Assert.Equal(Error(Joint.TreeInventory(Inventory.DuplicateWorkId [ other ])),
                         Joint.verifyPhysical root workId selected candidate))
