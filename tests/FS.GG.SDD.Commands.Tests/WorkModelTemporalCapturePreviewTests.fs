namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelTemporalCapturePreview
open Xunit

module WorkModelTemporalCapturePreviewTests =
    let private workId = "sample"
    let private projectPath = ".fsgg/project.yml"
    let private sddPath = ".fsgg/sdd.yml"

    let private fixture selectedSdd action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-temporal-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let bodies =
                    [ projectPath, "A0"
                      sddPath, "B0"
                      ".fsgg/agents.yml", "C"
                      "work/sample/spec.md", TestSupport.validSpec workId "Sample" ]
                for path, text in bodies do File.WriteAllText(Path.Combine(root, path), text)
                let selected: FileSnapshot list =
                    bodies |> List.map (fun (path, text) ->
                        { Path = path
                          Text = if path = sddPath then selectedSdd else text
                          RawBytes = None })
                let candidate: Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let captureOne path =
                    match captureSelectedFile root path with
                    | Ok file -> file
                    | Error reason -> failwithf "pinned fixture capture refused: %A" reason
                action root selected candidate captureOne
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before individually valid captures can describe no common instant`` () =
        fixture "B1" (fun root selected candidate captureOne ->
            let project = captureOne projectPath // A0 while sdd is B0.
            File.WriteAllText(Path.Combine(root, projectPath), "A1")
            File.WriteAllText(Path.Combine(root, sddPath), "B1")
            let mixed =
                [ project; captureOne sddPath; captureOne ".fsgg/agents.yml"
                  captureOne "work/sample/spec.md" ]
            // The first transition changes A before B, so A0/B1 never existed together.
            match FS.GG.SDD.Commands.WorkModelSourceBundle.verify workId selected mixed candidate with
            | Error reason -> failwithf "mixed-instant red-before control refused: %A" reason
            | Ok _ -> ()
            let mutable calls = 0
            let capture () =
                calls <- calls + 1
                if calls = 1 then Ok mixed
                else FS.GG.SDD.Commands.WorkModelSourceBundle.verifyFromPinnedCoreSources
                         root workId selected candidate
            match verifyWithCapture capture workId selected candidate with
            | Error(SecondPass reason) -> Assert.Equal(TextDrift projectPath, reason)
            | outcome -> failwithf "unexpected temporal result: %A" outcome
            Assert.Equal(2, calls))

    [<Fact>]
    let ``stable pinned sources produce read-only temporal preview`` () =
        fixture "B0" (fun root selected candidate _ ->
            match verifyPhysicalStable root workId selected candidate with
            | Error reason -> failwithf "stable sources refused: %A" reason
            | Ok preview ->
                Assert.Equal(workId, preview.WorkId)
                Assert.Equal(4, preview.Sources.Length))

    [<Fact>]
    let ``raw BOM change between individually valid passes refuses`` () =
        fixture "B0" (fun root selected candidate _ ->
            let first =
                match FS.GG.SDD.Commands.WorkModelSourceBundle.verifyFromPinnedCoreSources
                          root workId selected candidate with
                | Ok files -> files
                | Error reason -> failwithf "first valid pass refused: %A" reason
            File.WriteAllBytes(Path.Combine(root, projectPath),
                               Array.append [| 0xefuy; 0xbbuy; 0xbfuy |] (Encoding.UTF8.GetBytes "A0"))
            let second =
                match FS.GG.SDD.Commands.WorkModelSourceBundle.verifyFromPinnedCoreSources
                          root workId selected candidate with
                | Ok files -> files
                | Error reason -> failwithf "second decoded-text pass refused: %A" reason
            let mutable calls = 0
            let capture () =
                calls <- calls + 1
                Ok(if calls = 1 then first else second)
            Assert.Equal(Error(RawChanged projectPath),
                         verifyWithCapture capture workId selected candidate))
