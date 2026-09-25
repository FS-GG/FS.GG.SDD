namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelJointPreview
open Xunit

module WorkModelJointPreviewTests =
    let private workId = "sample"
    let private outputPath = "readiness/sample/work-model.json"
    let private projectPath = ".fsgg/project.yml"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-joint-preview-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let bodies =
                    [ projectPath, "schemaVersion: 1\n"
                      ".fsgg/sdd.yml", "schemaVersion: 1\n"
                      ".fsgg/agents.yml", "schemaVersion: 1\n"
                      "work/sample/spec.md", TestSupport.validSpec workId "Sample" ]
                let selected: FileSnapshot list =
                    bodies |> List.map (fun (path, body) ->
                        let bytes = Encoding.UTF8.GetBytes body
                        File.WriteAllBytes(Path.Combine(root, path), bytes)
                        { Path = path; Text = body; RawBytes = None })
                let candidate: Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let generator = SchemaVersion.currentGeneratorVersion ()
                let json =
                    Serialization.generateWorkModel
                        { WorkId = workId; Snapshots = selected
                          GeneratorVersion = generator; ExpectedOutputPath = Some outputPath }
                    |> _.Json
                let capture () =
                    FS.GG.SDD.Commands.WorkModelSourceBundle.verifyFromPinnedCoreSources
                        root workId selected candidate
                action root selected candidate generator json capture
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before temporal-only preview accepts stale proposed JSON`` () =
        fixture (fun _ selected candidate generator json capture ->
            let stale = json.Replace("\"modelVersion\": \"1.2.0\"", "\"modelVersion\": \"9.9.9\"")
            Assert.NotEqual(json, stale)
            match FS.GG.SDD.Commands.WorkModelTemporalCapturePreview.verifyWithCapture
                      capture workId selected candidate with
            | Error reason -> failwithf "temporal-only control refused: %A" reason
            | Ok _ -> ()
            let mutable calls = 0
            let counted () = calls <- calls + 1; capture ()
            Assert.Equal(Error(Proposed FS.GG.SDD.Commands.WorkModelExactOutputPreview.OutputDrift),
                         verifyWithCapture counted workId outputPath stale selected candidate generator)
            Assert.Equal(0, calls))

    [<Fact>]
    let ``red-before exact single capture accepts source changed after capture`` () =
        fixture (fun root selected candidate generator json capture ->
            let first =
                match capture () with
                | Ok files -> files
                | Error reason -> failwithf "initial capture refused: %A" reason
            match FS.GG.SDD.Commands.WorkModelExactOutputPreview.prepare
                      workId outputPath json selected candidate generator with
            | Error reason -> failwithf "exact preparation refused: %A" reason
            | Ok prepared ->
                match FS.GG.SDD.Commands.WorkModelExactOutputPreview.verifyCaptured prepared first with
                | Error reason -> failwithf "single-capture control refused: %A" reason
                | Ok _ -> ()
            File.WriteAllText(Path.Combine(root, projectPath), "changed\n")
            let mutable calls = 0
            let interleaved () =
                calls <- calls + 1
                if calls = 1 then Ok first else capture ()
            match verifyWithCapture interleaved workId outputPath json selected candidate generator with
            | Error(SecondCapture(TextDrift path)) -> Assert.Equal(projectPath, path)
            | outcome -> failwithf "unexpected joint result: %A" outcome
            Assert.Equal(2, calls))

    [<Fact>]
    let ``individually valid BOM change refuses raw-byte drift`` () =
        fixture (fun root selected candidate generator json capture ->
            let first =
                match capture () with
                | Ok files -> files
                | Error reason -> failwithf "initial capture refused: %A" reason
            let original = File.ReadAllBytes(Path.Combine(root, projectPath))
            File.WriteAllBytes(Path.Combine(root, projectPath),
                               Array.append [| 0xefuy; 0xbbuy; 0xbfuy |] original)
            let mutable calls = 0
            let interleaved () =
                calls <- calls + 1
                if calls = 1 then Ok first else capture ()
            Assert.Equal(Error(RawChanged projectPath),
                         verifyWithCapture interleaved workId outputPath json selected candidate generator)
            Assert.Equal(2, calls))

    [<Fact>]
    let ``stable physical capture yields proposed output digest without bytes or effects`` () =
        fixture (fun root selected candidate generator json _ ->
            match verifyPhysical root workId outputPath json selected candidate generator with
            | Error reason -> failwithf "stable joint preview refused: %A" reason
            | Ok preview ->
                Assert.Equal(outputPath, preview.OutputPath)
                Assert.Equal(SchemaVersion.outputSha256Text json, preview.OutputDigest)
                Assert.Equal(4, preview.SourcePaths.Length))
