namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelSourceBundle
open FS.GG.SDD.Commands.WorkModelExactOutputPreview
open Xunit

module WorkModelExactOutputPreviewTests =
    let private workId = "sample"
    let private outputPath = "readiness/sample/work-model.json"

    let private insertBefore (needle: string) (insertion: string) (text: string) =
        let index = text.IndexOf(needle, StringComparison.Ordinal)
        Assert.True(index >= 0, $"fixture JSON lacks '{needle}'")
        text.Insert(index, insertion)

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-exact-output-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/sample")) |> ignore
            try
                let paths =
                    [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml"
                      "work/sample/spec.md" ]
                let selected: FileSnapshot list =
                    paths
                    |> List.map (fun path ->
                        let text =
                            if path = "work/sample/spec.md" then TestSupport.validSpec workId "Sample"
                            else "schemaVersion: 1\n"
                        let bytes = Encoding.UTF8.GetBytes text
                        File.WriteAllBytes(Path.Combine(root, path), bytes)
                        { Path = path; Text = text; RawBytes = Some bytes })
                let candidate: Candidate =
                    { Version = 2; WorkId = workId
                      Sources = selected |> List.map (fun source ->
                          { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                let generator = SchemaVersion.currentGeneratorVersion ()
                let generated =
                    Serialization.generateWorkModel
                        { WorkId = workId; Snapshots = selected
                          GeneratorVersion = generator; ExpectedOutputPath = Some outputPath }
                let captureRoot closedRoot declared =
                    match capture root closedRoot declared ExactBytes with
                    | Ok files -> files
                    | Error reason -> failwithf "fixture capture failed: %A" reason
                let captured =
                    captureRoot ".fsgg" paths.[0..2]
                    @ captureRoot "work/sample" [ paths.[3] ]
                action selected candidate generator generated.Json captured
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before changed body with unchanged source rows passes typed source preview`` () =
        fixture (fun selected candidate generator json _ ->
            let changed = json.Replace("\"modelVersion\": \"1.2.0\"", "\"modelVersion\": \"9.9.9\"")
            Assert.NotEqual(json, changed)
            match FS.GG.SDD.Commands.WorkModelVerificationWave.prepare
                      workId outputPath changed selected candidate with
            | Error reason -> failwithf "source-only red-before control refused: %A" reason
            | Ok _ -> ()
            Assert.Equal(Error OutputDrift,
                         prepare workId outputPath changed selected candidate generator))

    [<Fact>]
    let ``red-before typed preview selects a later duplicate JSON property`` () =
        fixture (fun selected candidate generator json _ ->
            let duplicate =
                insertBefore "\"workId\": \"sample\""
                    "\"workId\": \"other\",\n  " json
            match FS.GG.SDD.Commands.WorkModelVerificationWave.prepare
                      workId outputPath duplicate selected candidate with
            | Error reason -> failwithf "duplicate-property control unexpectedly refused: %A" reason
            | Ok _ -> ()
            Assert.Equal(Error AmbiguousJson,
                         prepare workId outputPath duplicate selected candidate generator))

    [<Fact>]
    let ``duplicate root sources and nested case-alias property names refuse`` () =
        fixture (fun selected candidate generator json _ ->
            let rootDuplicate = insertBefore "\"sources\": [" "\"sources\": [],\n  " json
            Assert.Equal(Error AmbiguousJson,
                         prepare workId outputPath rootDuplicate selected candidate generator)
            let nestedAlias =
                insertBefore "\"sourceDigest\": {" "\"SOURCEDIGEST\": {},\n      " json
            Assert.Equal(Error AmbiguousJson,
                         prepare workId outputPath nestedAlias selected candidate generator))

    [<Fact>]
    let ``wrong independent generator version refuses exact output`` () =
        fixture (fun selected candidate generator json _ ->
            let wrong = { generator with Version = generator.Version + ".wrong" }
            Assert.Equal(Error OutputDrift,
                         prepare workId outputPath json selected candidate wrong))

    [<Fact>]
    let ``changed selected source refuses stale proposed output before physical comparison`` () =
        fixture (fun selected candidate generator json _ ->
            let changed =
                selected |> List.map (fun source ->
                    if source.Path = "work/sample/spec.md" then
                        { source with Text = source.Text.Replace("Sample", "Altered") }
                    else source)
            Assert.Equal(Error OutputDrift,
                         prepare workId outputPath json changed candidate generator))

    [<Fact>]
    let ``exact generated bytes and captured sources yield read-only preview`` () =
        fixture (fun selected candidate generator json captured ->
            match prepare workId outputPath json selected candidate generator with
            | Error reason -> failwithf "exact output refused: %A" reason
            | Ok prepared ->
                match verifyCaptured prepared captured with
                | Error reason -> failwithf "exact capture refused: %A" reason
                | Ok preview -> Assert.Equal(outputPath, preview.OutputPath))
