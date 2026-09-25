namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelCandidateInventoryPreview
open Xunit

module WorkModelCandidateOmissionTests =
    let private workId = "sample"
    let private selectedPath = "work/sample/spec.md"
    let private otherPath = "work/other/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-candidate-omission-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/other")) |> ignore
            File.WriteAllText(Path.Combine(root, selectedPath), TestSupport.validSpec workId "Selected")
            try action root
            finally Directory.Delete(root, true)

    let private selectedCapture root =
        match captureSelectedFile root selectedPath with
        | Ok file -> file
        | Error reason -> failwithf "selected pinned read refused: %A" reason

    [<Fact>]
    let ``red-before omitted physical duplicate yields pure false green but closed root refuses`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, otherPath), TestSupport.validSpec workId "Duplicate")
            let supplied = [ selectedCapture root ]
            match verify workId supplied with
            | Error reason -> failwithf "omitted-candidate control refused: %A" reason
            | Ok preview -> Assert.Equal<string list>([ selectedPath ], preview.CandidatePaths)
            Assert.Equal(Error(UnexpectedFile otherPath),
                         capture root "work" [ selectedPath ] ExactBytes)
            let complete =
                match capture root "work" [ selectedPath; otherPath ] ExactBytes with
                | Ok files -> files
                | Error reason -> failwithf "closed-root control refused: %A" reason
            Assert.Equal(Error(DuplicateWorkId [ otherPath ]), verify workId complete))

    [<Fact>]
    let ``stale pathname roster omits candidate created before physical capture`` () =
        fixture (fun root ->
            let declaredBefore =
                Directory.EnumerateFiles(Path.Combine(root, "work"), "*", SearchOption.AllDirectories)
                |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/'))
                |> Seq.toList
            Assert.Equal<string list>([ selectedPath ], declaredBefore)
            File.WriteAllText(Path.Combine(root, otherPath), TestSupport.validSpec workId "Late")
            match verify workId [ selectedCapture root ] with
            | Error reason -> failwithf "stale-roster pure control refused: %A" reason
            | Ok _ -> ()
            Assert.Equal(Error(UnexpectedFile otherPath),
                         capture root "work" declaredBefore ExactBytes))

    [<Fact>]
    let ``complete closed root with unrelated candidate remains acceptable`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, otherPath), TestSupport.validSpec "other" "Unrelated")
            match capture root "work" [ selectedPath; otherPath ] ExactBytes with
            | Error reason -> failwithf "unrelated closed root refused: %A" reason
            | Ok complete ->
                match verify workId complete with
                | Error reason -> failwithf "unrelated candidate refused: %A" reason
                | Ok preview -> Assert.Equal(2, preview.CandidatePaths.Length))
