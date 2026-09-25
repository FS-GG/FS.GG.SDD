namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelRepeatedCandidateTreePreview
open Xunit

module WorkModelRepeatedCandidateTreePreviewTests =
    let private workId = "z"
    let private selected = "work/z/spec.md"
    let private earlier = "work/a/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-repeat-work-tree-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work/a")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/z")) |> ignore
            File.WriteAllText(Path.Combine(root, selected), TestSupport.validSpec workId "Selected")
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before second full-tree pass refuses late candidate missed by first`` () =
        fixture (fun root ->
            let mutable calls = 0
            let afterOpen path =
                Assert.Equal(selected, path)
                File.WriteAllText(Path.Combine(root, earlier), TestSupport.validSpec workId "Late")
            let capture () =
                calls <- calls + 1
                if calls = 1 then
                    capturePinnedWithHooks ignore afterOpen root "work" [ selected ] ExactBytes
                else
                    capturePinnedWithHooks ignore ignore root "work" [ selected ] ExactBytes
            Assert.Equal(Error(SecondCapture(UnexpectedFile earlier)),
                         verifyWithCapture capture workId)
            Assert.Equal(2, calls))

    [<Fact>]
    let ``stable full work tree with unrelated candidate succeeds read only`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, earlier), TestSupport.validSpec "a" "Unrelated")
            match verifyPhysical root workId [ selected; earlier ] with
            | Error reason -> failwithf "stable tree refused: %A" reason
            | Ok preview -> Assert.Equal<string list>([ earlier; selected ], preview.CandidatePaths))

    [<Fact>]
    let ``two individually valid full-tree passes refuse changed raw bytes`` () =
        fixture (fun root ->
            let first =
                match capturePinnedWithHooks ignore ignore root "work" [ selected ] ExactBytes with
                | Ok files -> files
                | Error reason -> failwithf "first capture refused: %A" reason
            File.WriteAllText(Path.Combine(root, selected), TestSupport.validSpec workId "Changed")
            let mutable calls = 0
            let capture () =
                calls <- calls + 1
                if calls = 1 then Ok first
                else capturePinnedWithHooks ignore ignore root "work" [ selected ] ExactBytes
            Assert.Equal(Error(RawChanged selected), verifyWithCapture capture workId)
            Assert.Equal(2, calls))

    [<Fact>]
    let ``different supplied rosters refuse even when both inventories parse`` () =
        fixture (fun root ->
            let first =
                match capturePinnedWithHooks ignore ignore root "work" [ selected ] ExactBytes with
                | Ok files -> files
                | Error reason -> failwithf "first capture refused: %A" reason
            File.WriteAllText(Path.Combine(root, earlier), TestSupport.validSpec "a" "Unrelated")
            let second =
                match capturePinnedWithHooks ignore ignore root "work" [ selected; earlier ] ExactBytes with
                | Ok files -> files
                | Error reason -> failwithf "second capture refused: %A" reason
            let mutable calls = 0
            let capture () =
                calls <- calls + 1
                Ok(if calls = 1 then first else second)
            Assert.Equal(Error RosterChanged, verifyWithCapture capture workId))

    [<Fact>]
    let ``linked candidate refuses through pinned first pass`` () =
        fixture (fun root ->
            File.CreateSymbolicLink(Path.Combine(root, earlier), Path.Combine(root, selected)) |> ignore
            Assert.Equal(Error(FirstCapture(Symlink earlier)),
                         verifyPhysical root workId [ selected; earlier ]))
