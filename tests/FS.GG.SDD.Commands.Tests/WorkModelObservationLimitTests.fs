namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open Xunit

module WorkModelObservationLimitTests =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Repeated = WorkModelRepeatedJointCapturePreview

    let private workId = "sample"
    let private project = ".fsgg/project.yml"
    let private original = "schemaVersion: 1\n"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-observation-limit-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let bodies =
                    [ project, original
                      ".fsgg/sdd.yml", original
                      ".fsgg/agents.yml", original
                      "work/sample/spec.md", TestSupport.validSpec workId "Selected" ]
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

    let private bundle root selected candidate () =
        Bundle.verifyFromPinnedCoreSources root workId selected candidate

    let private tree root () =
        Physical.capturePinnedDiscovered root "work" Physical.ExactBytes

    [<Fact>]
    let ``ABA between complete pairs is unseen by four raw observations`` () =
        fixture (fun root selected candidate ->
            let mutable calls = 0
            let abaTree () =
                calls <- calls + 1
                let observed = tree root ()
                if calls = 1 then
                    File.WriteAllText(Path.Combine(root, project), "transient\n")
                    File.WriteAllText(Path.Combine(root, project), original)
                observed
            match Repeated.verifyWithCapture (bundle root selected candidate)
                                             abaTree workId selected candidate with
            | Ok(Repeated.ObservedAgreement preview) ->
                Assert.Equal(4, preview.BundlePaths.Length)
            | Error reason -> failwithf "ABA observation refused: %A" reason
            Assert.Equal(2, calls))

    [<Fact>]
    let ``source changed after final tree capture remains unseen`` () =
        fixture (fun root selected candidate ->
            let mutable calls = 0
            let finalChangeTree () =
                calls <- calls + 1
                let observed = tree root ()
                if calls = 2 then File.WriteAllText(Path.Combine(root, project), "later\n")
                observed
            match Repeated.verifyWithCapture (bundle root selected candidate)
                                             finalChangeTree workId selected candidate with
            | Ok(Repeated.ObservedAgreement preview) ->
                Assert.Equal(4, preview.BundlePaths.Length)
            | Error reason -> failwithf "Post-check observation refused: %A" reason
            Assert.Equal("later\n", File.ReadAllText(Path.Combine(root, project)))
            Assert.Equal(2, calls))
