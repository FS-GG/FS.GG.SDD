namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelCurrentGeneratorPreview
open Xunit

module WorkModelCurrentGeneratorPreviewTests =
    let private workId = "sample"
    let private outputPath = "readiness/sample/work-model.json"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-generator-authority-" + Guid.NewGuid().ToString("N"))
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
                let generate generator =
                    Serialization.generateWorkModel
                        { WorkId = workId; Snapshots = selected
                          GeneratorVersion = generator; ExpectedOutputPath = Some outputPath }
                    |> _.Json
                action root selected candidate capture generate
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before jointly forged proposal and caller generator pass current joint preview`` () =
        fixture (fun _ selected candidate capture generate ->
            let current = SchemaVersion.currentGeneratorVersion ()
            let forged = { current with Version = current.Version + ".foreign" }
            let proposal = generate forged
            Assert.NotEqual(generate current, proposal)
            match FS.GG.SDD.Commands.WorkModelJointPreview.verifyWithCapture
                      capture workId outputPath proposal selected candidate forged with
            | Error reason -> failwithf "red-before forged pair refused: %A" reason
            | Ok preview -> Assert.Equal(outputPath, preview.OutputPath)
            let mutable calls = 0
            let counted () = calls <- calls + 1; capture ()
            Assert.Equal(
                Error(Joint(WorkModelJointPreview.Proposed WorkModelExactOutputPreview.OutputDrift)),
                verifyWithCapture counted workId outputPath proposal selected candidate)
            Assert.Equal(0, calls))

    [<Fact>]
    let ``foreign generator id with current version also refuses before capture`` () =
        fixture (fun _ selected candidate capture generate ->
            let current = SchemaVersion.currentGeneratorVersion ()
            let forged = { current with Id = "foreign.generator" }
            let proposal = generate forged
            let mutable calls = 0
            let counted () = calls <- calls + 1; capture ()
            Assert.Equal(
                Error(Joint(WorkModelJointPreview.Proposed WorkModelExactOutputPreview.OutputDrift)),
                verifyWithCapture counted workId outputPath proposal selected candidate)
            Assert.Equal(0, calls))

    [<Fact>]
    let ``current assembly generator and pinned sources produce digest-only preview`` () =
        fixture (fun root selected candidate _ generate ->
            let proposal = generate (SchemaVersion.currentGeneratorVersion ())
            match verifyPhysical root workId outputPath proposal selected candidate with
            | Error reason -> failwithf "current generator refused: %A" reason
            | Ok preview ->
                Assert.Equal(outputPath, preview.OutputPath)
                Assert.Equal(SchemaVersion.outputSha256Text proposal, preview.OutputDigest)
                Assert.Equal(4, preview.SourcePaths.Length))
