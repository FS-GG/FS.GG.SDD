namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelCandidateInventoryPreview
open Xunit

module WorkModelLateCandidateTreeTests =
    let private selected = "work/z/spec.md"
    let private late = "work/a/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-late-candidate-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work/a")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/z")) |> ignore
            File.WriteAllText(Path.Combine(root, selected), TestSupport.validSpec "z" "Selected")
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before late candidate in previously visited child escapes one pinned tree pass`` () =
        fixture (fun root ->
            let mutable calls = 0
            let afterOpen path =
                Assert.Equal(selected, path)
                calls <- calls + 1
                File.WriteAllText(Path.Combine(root, late), TestSupport.validSpec "z" "Late duplicate")
            let first =
                match capturePinnedWithHooks ignore afterOpen root "work" [ selected ] ExactBytes with
                | Ok files -> files
                | Error reason -> failwithf "late-addition characterization refused: %A" reason
            Assert.Equal(1, calls)
            Assert.Equal<string list>([ selected ], first |> List.map _.Path)
            match verify "z" first with
            | Error reason -> failwithf "omitted late candidate unexpectedly refused: %A" reason
            | Ok _ -> ()
            Assert.Equal(Error(UnexpectedFile late), capture root "work" [ selected ] ExactBytes))

    [<Fact>]
    let ``candidate present before traversal refuses declared incomplete tree`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, late), TestSupport.validSpec "z" "Existing duplicate")
            Assert.Equal(Error(UnexpectedFile late), capture root "work" [ selected ] ExactBytes)
            match capture root "work" [ selected; late ] ExactBytes with
            | Error reason -> failwithf "complete candidate control refused: %A" reason
            | Ok files -> Assert.Equal(Error(DuplicateWorkId [ late ]), verify "z" files))
