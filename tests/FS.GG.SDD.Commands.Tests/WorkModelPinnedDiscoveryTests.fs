namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands
open Xunit

module WorkModelPinnedDiscoveryTests =
    module Physical = GenerationSourceSnapshot
    module Inventory = WorkModelCandidateInventoryPreview
    module Repeated = WorkModelRepeatedCandidateTreePreview

    let private workId = "z"
    let private selected = "work/z/spec.md"
    let private other = "work/a/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-discovery-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work/a")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/z")) |> ignore
            File.WriteAllText(Path.Combine(root, selected), TestSupport.validSpec workId "Selected")
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``physical discovery refuses a duplicate hidden from supplied inventory`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec workId "Duplicate")
            let selectedOnly =
                match Physical.captureSelectedFile root selected with
                | Ok file -> [ file ]
                | Error reason -> failwithf "Selected source refused: %A" reason
            // Independent red-before control: a caller can omit the duplicate.
            match Inventory.verify workId selectedOnly with
            | Ok preview -> Assert.Equal<string list>([ selected ], preview.CandidatePaths)
            | Error reason -> failwithf "Pure omission control refused: %A" reason
            Assert.Equal(Error(Repeated.FirstInventory(Inventory.DuplicateWorkId [ other ])),
                         Repeated.verifyPhysicalDiscovered root workId))

    [<Fact>]
    let ``unrelated candidate is discovered without a caller file list`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec "a" "Unrelated")
            match Repeated.verifyPhysicalDiscovered root workId with
            | Ok preview -> Assert.Equal<string list>([ other; selected ], preview.CandidatePaths)
            | Error reason -> failwithf "Discovered stable tree refused: %A" reason)

    [<Fact>]
    let ``missing selected spec refuses after physical discovery`` () =
        fixture (fun root ->
            File.Delete(Path.Combine(root, selected))
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec "a" "Unrelated")
            Assert.Equal(Error(Repeated.FirstInventory(Inventory.MissingSelectedSpec selected)),
                         Repeated.verifyPhysicalDiscovered root workId))

    [<Fact>]
    let ``late candidate in held earlier child refuses discovery`` () =
        fixture (fun root ->
            let afterRead path =
                Assert.Equal(selected, path)
                File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec workId "Late")
            let capture () =
                Physical.capturePinnedDiscoveredWithReadHook afterRead root "work" Physical.ExactBytes
            Assert.Equal(Error(Repeated.FirstCapture(Physical.DirectoryUnstable "work/a")),
                         Repeated.verifyWithCapture capture workId))

    [<Fact>]
    let ``linked discovered candidate refuses pinned first pass`` () =
        fixture (fun root ->
            File.CreateSymbolicLink(Path.Combine(root, other), Path.Combine(root, selected)) |> ignore
            Assert.Equal(Error(Repeated.FirstCapture(Physical.Symlink other)),
                         Repeated.verifyPhysicalDiscovered root workId))
