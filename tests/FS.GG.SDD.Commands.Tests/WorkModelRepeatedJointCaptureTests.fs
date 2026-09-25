namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open Xunit

module WorkModelRepeatedJointCaptureTests =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Joint = WorkModelCandidateBundleJoinPreview
    module Repeated = WorkModelRepeatedJointCapturePreview

    let private workId = "sample"
    let private project = ".fsgg/project.yml"
    let private spec = "work/sample/spec.md"
    let private other = "work/other/spec.md"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-repeat-joint-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/other")) |> ignore
            try
                let bodies =
                    [ project, "schemaVersion: 1\n"
                      ".fsgg/sdd.yml", "schemaVersion: 1\n"
                      ".fsgg/agents.yml", "schemaVersion: 1\n"
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

    let private captureBundle root selected candidate () =
        Bundle.verifyFromPinnedCoreSources root workId selected candidate

    let private captureTree root () =
        Physical.capturePinnedDiscovered root "work" Physical.ExactBytes

    let private prefixBom root path =
        let absolute = Path.Combine(root, path)
        let raw = File.ReadAllBytes absolute
        File.WriteAllBytes(absolute, Array.append [| 0xefuy; 0xbbuy; 0xbfuy |] raw)

    [<Fact>]
    let ``red-before one joined observation stays green after returned project changes`` () =
        fixture (fun root selected candidate ->
            let treeThenChange () =
                let result = captureTree root ()
                prefixBom root project
                result
            match Joint.verifyWithCapture (captureBundle root selected candidate)
                                          treeThenChange workId selected candidate with
            | Ok _ -> ()
            | Error reason -> failwithf "Single joined observation refused: %A" reason)

    [<Fact>]
    let ``second joined observation refuses raw config change with same selected text`` () =
        fixture (fun root selected candidate ->
            let mutable treeCalls = 0
            let treeThenChange () =
                treeCalls <- treeCalls + 1
                let result = captureTree root ()
                if treeCalls = 1 then prefixBom root project
                result
            Assert.Equal(Error(Repeated.BundleRawChanged project),
                         Repeated.verifyWithCapture (captureBundle root selected candidate)
                                                    treeThenChange workId selected candidate)
            Assert.Equal(2, treeCalls))

    [<Fact>]
    let ``second joined observation refuses raw work change with same decoded text`` () =
        fixture (fun root selected candidate ->
            let mutable treeCalls = 0
            let treeThenChange () =
                treeCalls <- treeCalls + 1
                let result = captureTree root ()
                if treeCalls = 1 then prefixBom root spec
                result
            Assert.Equal(Error(Repeated.BundleRawChanged spec),
                         Repeated.verifyWithCapture (captureBundle root selected candidate)
                                                    treeThenChange workId selected candidate)
            Assert.Equal(2, treeCalls))

    [<Fact>]
    let ``second joined observation refuses late unrelated candidate roster`` () =
        fixture (fun root selected candidate ->
            let mutable treeCalls = 0
            let treeThenChange () =
                treeCalls <- treeCalls + 1
                let result = captureTree root ()
                if treeCalls = 1 then
                    File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec "other" "Other")
                result
            Assert.Equal(Error Repeated.TreeRosterChanged,
                         Repeated.verifyWithCapture (captureBundle root selected candidate)
                                                    treeThenChange workId selected candidate)
            Assert.Equal(2, treeCalls))

    [<Fact>]
    let ``stable repeated physical join returns paths only`` () =
        fixture (fun root selected candidate ->
            File.WriteAllText(Path.Combine(root, other), TestSupport.validSpec "other" "Other")
            match Repeated.verifyPhysical root workId selected candidate with
            | Ok(Repeated.ObservedAgreement preview) ->
                Assert.Equal<string list>([ other; spec ], preview.CandidatePaths)
                Assert.Equal(4, preview.BundlePaths.Length)
            | Error reason -> failwithf "Stable repeated join refused: %A" reason)
