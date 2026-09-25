namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open Xunit

module WorkModelMixedInstantCharacterizationTests =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Repeated = WorkModelRepeatedJointCapturePreview

    let private workId = "sample"
    let private project = ".fsgg/project.yml"
    let private sdd = ".fsgg/sdd.yml"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-mixed-instant-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let bodies =
                    [ project, "A0"
                      sdd, "B0"
                      ".fsgg/agents.yml", "C"
                      "work/sample/spec.md", TestSupport.validSpec workId "Selected" ]
                for path, body in bodies do File.WriteAllText(Path.Combine(root, path), body)
                let selected: FileSnapshot list =
                    bodies |> List.map (fun (path, body) ->
                        { Path = path; Text = (if path = sdd then "B1" else body); RawBytes = None })
                let candidate: Bundle.Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                action root selected candidate
            finally Directory.Delete(root, true)

    let private tree root () =
        Physical.capturePinnedDiscovered root "work" Physical.ExactBytes

    [<Fact>]
    let ``four matching pinned pairs can describe project and sdd values with no common instant`` () =
        fixture (fun root selected candidate ->
            let states = ResizeArray<string * string>()
            let mutable projectValue = "A0"
            let mutable sddValue = "B0"
            let record () = states.Add(projectValue, sddValue)
            let setProject (value: string) =
                File.WriteAllText(Path.Combine(root, project), value)
                projectValue <- value
                record ()
            let setSdd (value: string) =
                File.WriteAllText(Path.Combine(root, sdd), value)
                sddValue <- value
                record ()
            record ()
            let mutable bundleCalls = 0
            let mutable treeCalls = 0
            let captureBundle () =
                bundleCalls <- bundleCalls + 1
                let afterCapture path =
                    if path = project then
                        // Project A0 was pinned before sdd becomes B1.
                        setProject "A1"
                        setSdd "B1"
                    elif path = sdd then
                        // Restore in this order so A0/B1 never occurs.
                        setSdd "B0"
                        setProject "A0"
                let result =
                    Bundle.verifyFromPinnedCoreSourcesWithHook afterCapture
                        root workId selected candidate
                match result with
                | Ok files ->
                    let text path =
                        files |> List.find (fun file -> file.Path = path)
                        |> fun file -> Encoding.UTF8.GetString(file.Bytes)
                    Assert.Equal("A0", text project)
                    Assert.Equal("B1", text sdd)
                | Error reason -> failwithf "Mixed pinned capture refused: %A" reason
                result
            let captureTree () = treeCalls <- treeCalls + 1; tree root ()
            match Repeated.verifyWithCapture captureBundle captureTree workId selected candidate with
            | Ok(Repeated.ObservedAgreement preview) -> Assert.Equal(4, preview.BundlePaths.Length)
            | Error reason -> failwithf "Repeated mixed capture refused: %A" reason
            Assert.Equal(2, bundleCalls)
            Assert.Equal(2, treeCalls)
            Assert.DoesNotContain(("A0", "B1"), states)
            Assert.Equal(("A0", "B0"), states.[states.Count - 1]))

    [<Fact>]
    let ``persistent source transition refuses the second bundle`` () =
        fixture (fun root selected candidate ->
            let mutable bundleCalls = 0
            let captureBundle () =
                bundleCalls <- bundleCalls + 1
                let afterCapture path =
                    if bundleCalls = 1 && path = project then
                        File.WriteAllText(Path.Combine(root, project), "A1")
                        File.WriteAllText(Path.Combine(root, sdd), "B1")
                Bundle.verifyFromPinnedCoreSourcesWithHook afterCapture
                    root workId selected candidate
            match Repeated.verifyWithCapture captureBundle (tree root)
                                             workId selected candidate with
            | Error(Repeated.SecondBundleCapture(Bundle.TextDrift path)) -> Assert.Equal(project, path)
            | result -> failwithf "Expected persistent-change refusal, got %A" result
            Assert.Equal(2, bundleCalls))
