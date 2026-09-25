namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelBlockingDiagnosticsPreview
open FS.GG.SDD.Commands.Internal
open Xunit

module WorkModelBlockingDiagnosticsPreviewTests =
    let private workId = "sample"
    let private outputPath = "readiness/sample/work-model.json"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-blocking-preview-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let bodies =
                    [ ".fsgg/project.yml", "schemaVersion: 1\n"
                      ".fsgg/sdd.yml", "schemaVersion: 1\n"
                      ".fsgg/agents.yml", "schemaVersion: 1\n"
                      "work/sample/spec.md", TestSupport.validSpec workId "Sample" ]
                let selected: FileSnapshot list =
                    bodies |> List.map (fun (path, body) ->
                        File.WriteAllBytes(Path.Combine(root, path), Encoding.UTF8.GetBytes body)
                        { Path = path; Text = body; RawBytes = None })
                let candidate: Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let capture () =
                    FS.GG.SDD.Commands.WorkModelSourceBundle.verifyFromPinnedCoreSources
                        root workId selected candidate
                let generated =
                    Serialization.generateWorkModel
                        { WorkId = workId; Snapshots = selected
                          GeneratorVersion = SchemaVersion.currentGeneratorVersion ()
                          ExpectedOutputPath = Some outputPath }
                action selected candidate capture generated
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before exact current-generator preview accepts a semantically blocked model`` () =
        fixture (fun selected candidate capture generated ->
            let blocked = WorkModel.blockingDiagnostics generated.Model
            Assert.NotEmpty blocked
            match FS.GG.SDD.Commands.WorkModelCurrentGeneratorPreview.verifyWithCapture
                      capture workId outputPath generated.Json selected candidate with
            | Error reason -> failwithf "red-before blocked model refused: %A" reason
            | Ok preview -> Assert.Equal(outputPath, preview.OutputPath)
            let mutable calls = 0
            let counted () = calls <- calls + 1; capture ()
            match verifyWithCapture counted workId outputPath generated.Json selected candidate with
            | Error(BlockingDiagnostics ids) ->
                Assert.NotEmpty ids
                Assert.Equal<string list>(blocked |> List.map _.Id |> List.distinct |> List.sort, ids)
            | outcome -> failwithf "unexpected blocking preview result: %A" outcome
            Assert.Equal(0, calls))

    [<Fact>]
    let ``nonblocking normalized fixture passes source-only physical preview`` () =
        if OperatingSystem.IsLinux() then
            let sourceRoot =
                Path.Combine(TestSupport.repoRoot, "tests/fixtures/normalized-work-model/valid-work-item")
            let root = Path.Combine(Path.GetTempPath(), "sdd-blocking-valid-" + Guid.NewGuid().ToString("N"))
            let id = "002-normalized-work-model"
            let path = $"readiness/{id}/work-model.json"
            let paths =
                [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml"
                  $"work/{id}/spec.md"; $"work/{id}/tasks.yml"; $"work/{id}/evidence.yml" ]
            Directory.CreateDirectory root |> ignore
            try
                let selected: FileSnapshot list =
                    paths |> List.map (fun (relative: string) ->
                        let original = File.ReadAllBytes(Path.Combine(sourceRoot, relative))
                        let target = Path.Combine(root, relative)
                        Directory.CreateDirectory(
                            Path.GetDirectoryName target
                            |> Option.ofObj
                            |> Option.defaultWith (fun () -> failwith "fixture path has no parent")) |> ignore
                        File.WriteAllBytes(target, original)
                        let body = File.ReadAllText target
                        { Path = relative
                          Text = if relative.EndsWith("/evidence.yml", StringComparison.Ordinal) then
                                     ViewGeneration.evidenceTextForWorkModel body
                                 else body
                          RawBytes = None })
                let candidate: Candidate =
                    { Version = 2; WorkId = id
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let generated =
                    Serialization.generateWorkModel
                        { WorkId = id; Snapshots = selected
                          GeneratorVersion = SchemaVersion.currentGeneratorVersion ()
                          ExpectedOutputPath = Some path }
                Assert.Empty(WorkModel.blockingDiagnostics generated.Model)
                match verifyPhysical root id path generated.Json selected candidate with
                | Error reason -> failwithf "valid normalized physical preview refused: %A" reason
                | Ok preview -> Assert.Equal(path, preview.OutputPath)
            finally Directory.Delete(root, true)
