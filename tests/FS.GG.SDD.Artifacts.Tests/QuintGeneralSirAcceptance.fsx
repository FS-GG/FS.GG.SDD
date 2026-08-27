open System
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Artifacts.TypedSpecifications

let fail message =
    failwith ("PROFILE-2-SIR-REFUSAL: " + message)

let expect label =
    function
    | Ok value -> value
    | Error findings -> fail (sprintf "%s: %A" label findings)

let sha256 (text: string) =
    text
    |> Encoding.UTF8.GetBytes
    |> SHA256.HashData
    |> Convert.ToHexString
    |> _.ToLowerInvariant()

let sha256Bytes (bytes: byte array) =
    bytes |> SHA256.HashData |> Convert.ToHexString |> _.ToLowerInvariant()

let range path startLine startColumn endLine endColumn : QuintSourceRange =
    { Path = path
      Start =
        { Line = startLine
          Column = startColumn }
      End = { Line = endLine; Column = endColumn } }

let parseFences (source: QuintMarkdownSource) =
    let lines = source.Text.Split('\n')
    let mutable cursor = 0
    let mutable ordinal = 0
    let mutable generatedLines = Map.empty<string, int>
    let fences = ResizeArray<QuintFence>()
    let maps = ResizeArray<QuintSourceMapEntry>()

    while cursor < lines.Length do
        let header = lines[cursor]

        if
            header.StartsWith("```quint ", StringComparison.Ordinal)
            && header.EndsWith(" +=", StringComparison.Ordinal)
        then
            let target = header.Substring(9, header.Length - 12)

            let closing =
                [ cursor + 1 .. lines.Length - 1 ]
                |> List.find (fun index -> lines[index] = "```")

            let contentLines = lines[cursor + 1 .. closing - 1]
            let content = String.Join("\n", contentLines) + "\n"

            let moduleName =
                contentLines
                |> Array.pick (fun line ->
                    let trimmed = line.TrimStart()

                    if trimmed.StartsWith("module ", StringComparison.Ordinal) then
                        let suffix = trimmed.Substring(7)
                        Some(suffix.Split([| ' '; '{' |], StringSplitOptions.RemoveEmptyEntries)[0])
                    else
                        None)

            let firstGeneratedLine = Map.tryFind target generatedLines |> Option.defaultValue 1
            let lastGeneratedLine = firstGeneratedLine + contentLines.Length - 1
            let lastColumn = max 1 contentLines[contentLines.Length - 1].Length

            fences.Add
                { Ordinal = ordinal
                  Target = target
                  ModuleName = moduleName
                  SourceRange = range source.Path (cursor + 1) 1 (closing + 1) 3
                  ContentSha256 = sha256 content }

            maps.Add
                { Target = target
                  GeneratedRange = range target firstGeneratedLine 1 lastGeneratedLine lastColumn
                  Source =
                    { FenceOrdinal = ordinal
                      Range = range source.Path (cursor + 2) 1 closing lastColumn } }

            generatedLines <- Map.add target (lastGeneratedLine + 1) generatedLines
            ordinal <- ordinal + 1
            cursor <- closing + 1
        else
            cursor <- cursor + 1

    List.ofSeq fences, List.ofSeq maps

let cacheObservation id =
    let requirement =
        QuintToolchain.general.Components
        |> List.collect _.Objects
        |> List.find (fun item -> item.Id = id)

    { Id = requirement.Id
      Kind = requirement.Kind
      State = Present(requirement.Sha256, requirement.Bytes, true) }

let request step objectId arguments =
    { StepId = step
      ExecutableObjectId = objectId
      Arguments = arguments
      Environment = []
      WorkingDirectory = "isolated-run" }

match fsi.CommandLineArgs |> Array.skip 1 with
| [| typedEffectPath; selectorPath; markdownPath; generatedPath; outputRoot |] ->
    let selectors =
        File.ReadAllText selectorPath
        |> QuintGeneralBindingManifest.deserialize
        |> expect "selector manifest"

    let typedEffect = File.ReadAllText typedEffectPath

    let catalogue =
        QuintGeneralProfile.adaptTypedEffectJson
            { Profile = selectors.Profile
              QuintVersion = QuintGeneralProfile.quintVersion
              TypedEffectJson = typedEffect
              ExportBindings = selectors.Exports
              ActionBindings = selectors.Actions }
        |> expect "typed/effect adaptation"

    let rules =
        catalogue.Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Rules")

    let properties =
        catalogue.Catalogue
        |> List.filter (fun row -> row.ExportId = "EXPORT-Properties")

    let relationships =
        catalogue.Catalogue
        |> List.filter (fun row -> row.ExportId = "EXPORT-Relationships")

    let bounds =
        catalogue.Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Bounds")

    let verifications =
        catalogue.Catalogue
        |> List.filter (fun row -> row.ExportId = "EXPORT-Verifications")

    let impacts =
        catalogue.Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Impacts")

    let compatibility =
        catalogue.Catalogue
        |> List.filter (fun row -> row.ExportId = "EXPORT-Compatibility")

    if
        rules.Length <> 16
        || properties.Length <> 7
        || relationships.Length <> 14
        || bounds.Length <> 4
        || verifications.Length <> 3
        || impacts.Length <> 1
        || compatibility.Length <> 1
        || catalogue.ActionEffects.Length <> 5
    then
        fail (
            sprintf
                "expected 16 rules, 7 properties, 14 relationships, 4 bounds, 3 verifications, one impact/compatibility, and 5 actions; got %d/%d/%d/%d/%d/%d/%d/%d"
                rules.Length
                properties.Length
                relationships.Length
                bounds.Length
                verifications.Length
                impacts.Length
                compatibility.Length
                catalogue.ActionEffects.Length
        )

    let logicalPath = "tests/fixtures/quint-general-sir/sir-combat.md"

    let source =
        File.ReadAllBytes markdownPath
        |> QuintSource.createMarkdown logicalPath
        |> expect "source"

    let fences, sourceMaps = parseFences source
    let generatedBytes = File.ReadAllBytes generatedPath

    let requests =
        [ request "extract" "lmt-binary" [ logicalPath ]
          request "typecheck" "quint-binary" [ "typecheck"; "sir-combat.qnt"; "--out=typed.json" ] ]

    let compilationInput: QuintGeneralObservedCompilation =
        { ModuleName = selectors.ModuleName
          Toolchain = QuintToolchain.general
          Cache = [ cacheObservation "lmt-binary"; cacheObservation "quint-binary" ]
          ProcessRequests = requests
          Endpoint = Available
          ProcessObservations =
            requests
            |> List.map (fun item ->
                { StepId = item.StepId
                  Outcome = Succeeded })
          Source = source
          FenceManifest =
            { Schema = QuintSource.fenceManifestSchema
              SourcePath = source.Path
              SourceSha256 = source.Sha256
              Fences = fences }
          Extraction =
            { First =
                [ { Target = "sir-combat.qnt"
                    Sha256 = sha256Bytes generatedBytes
                    Bytes = int64 generatedBytes.Length } ]
              Second =
                [ { Target = "sir-combat.qnt"
                    Sha256 = sha256Bytes generatedBytes
                    Bytes = int64 generatedBytes.Length } ]
              Warnings = [] }
          SourceMap =
            { Schema = QuintSource.sourceMapSchema
              SourceSha256 = source.Sha256
              Entries = sourceMaps }
          TypedEffect =
            { Profile = selectors.Profile
              QuintVersion = QuintGeneralProfile.quintVersion
              TypedEffectJson = typedEffect
              ExportBindings = selectors.Exports
              ActionBindings = selectors.Actions }
          Metadata =
            { Specification = "SirCombat"
              Relationships = []
              VerificationProfiles = []
              Bounds = []
              Impacts = []
              Compatibility = []
              Digests =
                [ { Name = "typed-effect"
                    Sha256 = sha256 typedEffect } ] } }

    let compiled =
        QuintCompiler.compileGeneralObserved compilationInput
        |> expect "observed compilation"

    if
        compiled.Contract.Relationships.Length <> 14
        || compiled.Contract.VerificationProfiles.Length <> 3
        || compiled.Contract.Bounds.Length <> 4
        || compiled.Contract.Impacts.Length <> 1
        || compiled.Contract.Compatibility.Length <> 1
    then
        fail "dedicated contract facts were not derived from Quint catalogue rows"

    let semanticSidecar =
        { compilationInput with
            Metadata =
                { compilationInput.Metadata with
                    Relationships =
                        [ { FromId = "COMBAT-DAMAGE-001"
                            Kind = Requires
                            ToId = "COMBAT-TRACE-002" } ] } }

    match QuintCompiler.compileGeneralObserved semanticSidecar with
    | Error findings when
        findings
        |> List.exists (fun finding -> finding.Code = "QUINT-COMPILER-SEMANTIC-SIDECAR")
        ->
        ()
    | other -> fail (sprintf "semantic sidecar mutation was not refused: %A" other)

    let forgedSelectorRange =
        let first = compilationInput.TypedEffect.ExportBindings.Head

        { compilationInput with
            TypedEffect =
                { compilationInput.TypedEffect with
                    ExportBindings =
                        { first with
                            Source =
                                { first.Source with
                                    Start = { Line = 9999; Column = 1 }
                                    End = { Line = 10000; Column = 1 } } }
                        :: compilationInput.TypedEffect.ExportBindings.Tail } }

    match QuintCompiler.compileGeneralObserved forgedSelectorRange with
    | Error findings when
        findings
        |> List.exists (fun finding -> finding.Code = "QUINT-COMPILER-SOURCE-BINDING")
        ->
        ()
    | other -> fail (sprintf "forged selector range was not refused: %A" other)

    let overbroadSelectorRange =
        let first = compilationInput.TypedEffect.ExportBindings.Head

        { compilationInput with
            TypedEffect =
                { compilationInput.TypedEffect with
                    ExportBindings =
                        { first with
                            Source = compilationInput.SourceMap.Entries.Head.Source.Range }
                        :: compilationInput.TypedEffect.ExportBindings.Tail } }

    match QuintCompiler.compileGeneralObserved overbroadSelectorRange with
    | Error findings when
        findings
        |> List.exists (fun finding -> finding.Code = "QUINT-COMPILER-SOURCE-BINDING")
        ->
        ()
    | other -> fail (sprintf "overbroad selector range was not refused: %A" other)

    let canonical = compiled.CanonicalContract
    let bindings = compiled.Bindings

    Directory.CreateDirectory outputRoot |> ignore
    File.WriteAllText(Path.Combine(outputRoot, "contract.json"), canonical)
    File.WriteAllText(Path.Combine(outputRoot, "bindings.fs"), bindings.FSharpSource)
    File.WriteAllText(Path.Combine(outputRoot, "bindings.fable.fs"), bindings.FableSource)

    File.WriteAllText(
        Path.Combine(outputRoot, "native.txt"),
        String.concat
            "\n"
            [ bindings.ContractFingerprint
              String.concat "," (rules |> List.map _.Id)
              canonical ]
        + "\n"
    )

    printfn
        "PROFILE-2-SIR-ACCEPTED: rules=16 properties=7 relationships=14 bounds=4 verifications=3 impacts=1 compatibility=1 actions=5 fingerprint=%s"
        bindings.ContractFingerprint
| _ -> fail "expected <typed-effect.json> <profile-bindings.json> <markdown> <generated-qnt> <output-root>"
