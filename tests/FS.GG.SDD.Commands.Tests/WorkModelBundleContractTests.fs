namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.GenerationSourceContract
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.Internal
open Xunit

module WorkModelBundleContractTests =
    module Bundle = FS.GG.SDD.Commands.WorkModelSourceBundle

    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-work-model-bundle-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "work", "sample")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before single-root contract accepts config while work source differs`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
                for path in config do File.WriteAllBytes(Path.Combine(root, path), [| 1uy |])
                File.WriteAllBytes(Path.Combine(root, "work/sample/spec.md"), Encoding.UTF8.GetBytes "changed")
                let selected: Selection = { ClosedRoot = ".fsgg"; Policy = ExactBytes }
                let candidate: Contract =
                    { Version = 1
                      ClosedRoot = ".fsgg"
                      Policy = ExactBytes
                      Sources = config |> List.map (fun path -> { Path = path; Digest = SchemaVersion.sha256Bytes [| 1uy |] }) }
                match verify root selected candidate with
                | Error refusal -> failwithf "control unexpectedly refused: %A" refusal
                | Ok files -> Assert.Equal(3, files.Length))

    let private fixture action =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                Directory.CreateDirectory(Path.Combine(root, "readiness", "sample")) |> ignore
                let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
                let work = [ "work/sample/spec.md"; "work/sample/evidence.yml" ]
                let performance = "readiness/sample/performance-evidence.json"
                let evidence =
                    "sourceSnapshots:\n  - path: stale\nevidence:\n  - id: EV001\n"
                let bodies =
                    [ yield! config |> List.map (fun path -> path, "schemaVersion: 1\n")
                      "work/sample/spec.md", "# source\r\n"
                      "work/sample/evidence.yml", evidence
                      performance, "measured\n" ]
                for path, body in bodies do File.WriteAllText(Path.Combine(root, path), body)
                let selected: FileSnapshot list =
                    bodies
                    |> List.map (fun (path, body) ->
                        { Path = path
                          Text = if path = "work/sample/evidence.yml" then
                                     ViewGeneration.evidenceTextForWorkModel body
                                 else body
                          RawBytes = Some(Encoding.UTF8.GetBytes body) })
                let captureRoot closedRoot paths =
                    match capture root closedRoot paths ExactBytes with
                    | Ok files -> files
                    | Error refusal -> failwithf "fixture capture refused: %A" refusal
                let physical =
                    captureRoot ".fsgg" config
                    @ captureRoot "work/sample" work
                    @ captureRoot "readiness/sample" [ performance ]
                let candidate: Bundle.Candidate =
                    { Version = 2
                      WorkId = "sample"
                      Sources =
                        selected
                        |> List.map (fun source ->
                            { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                action root selected physical candidate)

    [<Fact>]
    let ``three selected roots bind captured bytes and projected evidence digest`` () =
        fixture (fun _ selected physical candidate ->
            let evidence = selected |> List.find (fun source -> source.Path = "work/sample/evidence.yml")
            Assert.Equal("sourceSnapshots: []\nevidence:\n  - id: EV001\n", evidence.Text)
            match Bundle.verify "sample" selected physical candidate with
            | Error refusal -> failwithf "valid bundle refused: %A" refusal
            | Ok files -> Assert.Equal(6, files.Length))

    [<Fact>]
    let ``missing and extra physical sources refuse`` () =
        fixture (fun root selected physical candidate ->
            let absent = physical |> List.filter (fun file -> file.Path <> "work/sample/spec.md")
            Assert.Equal(Error(Bundle.MissingPhysical "work/sample/spec.md"),
                         Bundle.verify "sample" selected absent candidate)
            let extraPath = "work/sample/plan.md"
            File.WriteAllText(Path.Combine(root, extraPath), "plan")
            let extra =
                match capture root "work/sample" [ "work/sample/spec.md"; "work/sample/evidence.yml"; extraPath ] ExactBytes with
                | Ok files -> files |> List.find (fun file -> file.Path = extraPath)
                | Error refusal -> failwithf "extra fixture refused: %A" refusal
            Assert.Equal(Error(Bundle.UnexpectedPhysical extraPath),
                         Bundle.verify "sample" selected (physical @ [ extra ]) candidate))

    [<Fact>]
    let ``missing candidate and case alias refuse`` () =
        fixture (fun _ selected physical candidate ->
            let absent = { candidate with Sources = candidate.Sources |> List.filter (fun row -> row.Path <> "work/sample/spec.md") }
            Assert.Equal(Error(Bundle.MissingCandidate "work/sample/spec.md"),
                         Bundle.verify "sample" selected physical absent)
            let spec = candidate.Sources |> List.find (fun row -> row.Path = "work/sample/spec.md")
            let alias = { spec with Path = "work/sample/Spec.md" }
            Assert.Equal(Error(Bundle.DuplicateCandidate "work/sample/Spec.md"),
                         Bundle.verify "sample" selected physical { candidate with Sources = candidate.Sources @ [ alias ] }))

    [<Fact>]
    let ``wrong selected text raw bytes and candidate digest refuse`` () =
        fixture (fun _ selected physical candidate ->
            let change path edit =
                selected |> List.map (fun source -> if source.Path = path then edit source else source)
            let spec = "work/sample/spec.md"
            let changedText = change spec (fun source -> { source with Text = "forged" })
            Assert.Equal(Error(Bundle.TextDrift spec), Bundle.verify "sample" changedText physical candidate)
            let changedRaw = change spec (fun source -> { source with RawBytes = Some [| 0uy |] })
            Assert.Equal(Error(Bundle.RawDrift spec), Bundle.verify "sample" changedRaw physical candidate)
            let wrongDigest =
                let rows =
                    candidate.Sources |> List.map (fun row ->
                        if row.Path = spec then { row with Digest = SchemaVersion.sha256Text "wrong" } else row)
                { candidate with Sources = rows }
            Assert.Equal(Error(Bundle.DigestDrift spec), Bundle.verify "sample" selected physical wrongDigest))

    [<Fact>]
    let ``unprojected evidence and wrong work identity refuse`` () =
        fixture (fun _ selected physical candidate ->
            let evidence = "work/sample/evidence.yml"
            let unprojected =
                selected |> List.map (fun source ->
                    if source.Path = evidence then
                        { source with Text = "sourceSnapshots:\n  - path: stale\nevidence:\n  - id: EV001\n" }
                    else source)
            Assert.Equal(Error(Bundle.TextDrift evidence), Bundle.verify "sample" unprojected physical candidate)
            Assert.Equal(Error Bundle.WrongWorkId,
                         Bundle.verify "sample" selected physical { candidate with WorkId = "other" })
            Assert.Equal(Error(Bundle.UnsupportedVersion 1),
                         Bundle.verify "sample" selected physical { candidate with Version = 1 }))

    [<Fact>]
    let ``unselected charter and unsafe candidate path refuse`` () =
        fixture (fun _ selected physical candidate ->
            let charter = { selected.Head with Path = "work/sample/charter.md" }
            Assert.Equal(Error(Bundle.InvalidSelectionPath "work/sample/charter.md"),
                         Bundle.verify "sample" (charter :: selected) physical candidate)
            let forged = { candidate.Sources.Head with Path = "work/sample/../escape" }
            Assert.Equal(Error(Bundle.InvalidCandidatePath "work/sample/../escape"),
                         Bundle.verify "sample" selected physical { candidate with Sources = forged :: candidate.Sources }))

    [<Fact>]
    let ``readiness selection without evidence source refuses`` () =
        fixture (fun _ selected physical candidate ->
            let withoutEvidence = selected |> List.filter (fun source -> source.Path <> "work/sample/evidence.yml")
            Assert.Equal(Error(Bundle.MissingRequired "work/sample/evidence.yml"),
                         Bundle.verify "sample" withoutEvidence physical candidate))

    [<Fact>]
    let ``malformed digest and selected case alias refuse`` () =
        fixture (fun _ selected physical candidate ->
            let spec = "work/sample/spec.md"
            let badDigest =
                candidate.Sources
                |> List.map (fun row ->
                    if row.Path = spec then { row with Digest = { Algorithm = "sha256"; Value = "ABC" } }
                    else row)
            Assert.Equal(Error(Bundle.MalformedDigest spec),
                         Bundle.verify "sample" selected physical { candidate with Sources = badDigest })
            let original = selected |> List.find (fun source -> source.Path = spec)
            let alias = { original with Path = "work/sample/Spec.md" }
            Assert.Equal(Error(Bundle.InvalidSelectionPath "work/sample/Spec.md"),
                         Bundle.verify "sample" (selected @ [ alias ]) physical candidate))
