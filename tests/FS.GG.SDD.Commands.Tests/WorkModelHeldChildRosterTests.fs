namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelRepeatedCandidateTreePreview
open Xunit

module WorkModelHeldChildRosterTests =
    let private selected = "work/z/spec.md"
    let private late = "work/a/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-held-child-roster-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work/a")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/z")) |> ignore
            File.WriteAllText(Path.Combine(root, selected), TestSupport.validSpec "z" "Selected")
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``transient late candidate must refuse before it is removed between full tree passes`` () =
        fixture (fun root ->
            let mutable calls = 0
            let afterOpen path =
                Assert.Equal(selected, path)
                File.WriteAllText(Path.Combine(root, late), TestSupport.validSpec "z" "Late")
            let capture () =
                calls <- calls + 1
                if calls = 1 then
                    let result = capturePinnedWithHooks ignore afterOpen root "work" [ selected ] ExactBytes
                    File.Delete(Path.Combine(root, late))
                    result
                else capturePinnedWithHooks ignore ignore root "work" [ selected ] ExactBytes
            Assert.Equal(Error(FirstCapture(DirectoryUnstable "work/a")),
                         verifyWithCapture capture "z")
            Assert.Equal(1, calls))

    [<Fact>]
    let ``late empty child in previously visited directory refuses`` () =
        fixture (fun root ->
            let afterOpen path =
                Assert.Equal(selected, path)
                Directory.CreateDirectory(Path.Combine(root, "work/a/late")) |> ignore
            Assert.Equal(Error(DirectoryUnstable "work/a"),
                         capturePinnedWithHooks ignore afterOpen root "work" [ selected ] ExactBytes))

    [<Fact>]
    let ``stable nested child and unrelated candidate still capture`` () =
        fixture (fun root ->
            Directory.CreateDirectory(Path.Combine(root, "work/a/nested")) |> ignore
            let unrelated = "work/a/nested/spec.md"
            File.WriteAllText(Path.Combine(root, unrelated), TestSupport.validSpec "a" "Other")
            match capturePinnedWithHooks ignore ignore root "work" [ selected; unrelated ] ExactBytes with
            | Error reason -> failwithf "stable nested tree refused: %A" reason
            | Ok files -> Assert.Equal<string list>([ unrelated; selected ], files |> List.map _.Path))
