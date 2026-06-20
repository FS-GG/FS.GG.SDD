namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion

module GenerationManifest =
    type GeneratedViewKind =
        | WorkModel
        | Analysis
        | Verify
        | Ship
        | Summary
        | AgentCommands
        | Other of string

    type GeneratedViewCurrencyStatus =
        | CurrencyCurrent
        | CurrencyMissing
        | CurrencyStale
        | CurrencyMalformed

    type SourceIdentity =
        { Artifact: ArtifactRef
          Digest: SourceDigest
          SchemaVersion: SchemaVersion option
          SchemaStatus: SchemaCompatibilityStatus
          RawSchemaVersion: string option }

    type GenerationManifest =
        { View: ArtifactRef
          Kind: GeneratedViewKind
          SchemaVersion: SchemaVersion
          Generator: GeneratorVersion
          Sources: SourceIdentity list
          OutputDigest: OutputDigest option
          Currency: GeneratedViewCurrencyStatus
          Diagnostics: Diagnostic list }

    type GeneratedWorkModelMetadata =
        { Path: string
          SchemaVersion: SchemaVersion option
          ModelVersion: string option
          Generator: GeneratorVersion option
          Sources: SourceIdentity list
          OutputDigest: OutputDigest option }

    let viewKindValue kind =
        match kind with
        | WorkModel -> "workModel"
        | Analysis -> "analysis"
        | Verify -> "verify"
        | Ship -> "ship"
        | Summary -> "summary"
        | AgentCommands -> "agentCommands"
        | Other value -> value

    let currencyStatusValue status =
        match status with
        | CurrencyCurrent -> "current"
        | CurrencyMissing -> "missing"
        | CurrencyStale -> "stale"
        | CurrencyMalformed -> "malformed"

    let expectedWorkModelOutputPath (workId: string) = $"readiness/{workId}/work-model.json"

    let expectedSummaryOutputPath (workId: string) = $"readiness/{workId}/summary.md"

    let createWorkModelManifest viewPath generatorVersion sources outputDigest =
        let view =
            match ArtifactRef.create viewPath ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
            | Ok value -> value
            | Error message -> invalidArg (nameof viewPath) message

        { View = view
          Kind = WorkModel
          SchemaVersion = SchemaVersion.create 1
          Generator = generatorVersion
          Sources = sources |> List.sortBy (fun source -> source.Artifact.Path)
          OutputDigest = outputDigest
          Currency = CurrencyCurrent
          Diagnostics = [] }

    let createSummaryManifest viewPath generatorVersion sources outputDigest =
        let view =
            match ArtifactRef.create viewPath ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
            | Ok value -> value
            | Error message -> invalidArg (nameof viewPath) message

        { View = view
          Kind = Summary
          SchemaVersion = SchemaVersion.create 1
          Generator = generatorVersion
          Sources = sources |> List.sortBy (fun source -> source.Artifact.Path)
          OutputDigest = outputDigest
          Currency = CurrencyCurrent
          Diagnostics = [] }

    let isStale (currentSources: SourceIdentity list) (manifest: GenerationManifest) =
        let expected =
            currentSources
            |> List.map (fun source -> source.Artifact.Path, source.Digest.Value)
            |> Map.ofList

        manifest.Sources
        |> List.exists (fun source ->
            match Map.tryFind source.Artifact.Path expected with
            | Some digest -> digest <> source.Digest.Value
            | None -> true)

    let artifact path =
        match ArtifactRef.create path ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
        | Ok value -> value
        | Error message -> invalidArg (nameof path) message

    let tryProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) then Some value else None

    let stringProperty (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Some(value.GetString())
            else
                None)

    let intProperty (name: string) (element: JsonElement) =
        tryProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Number then
                match value.TryGetInt32() with
                | true, number -> Some number
                | _ -> None
            else
                None)

    let parseDigest (element: JsonElement) =
        match stringProperty "algorithm" element, stringProperty "value" element with
        | Some algorithm, Some value -> SchemaVersion.createSourceDigest algorithm value |> Result.toOption
        | _ -> None

    let parseOutputDigest (element: JsonElement) =
        match stringProperty "algorithm" element, stringProperty "value" element with
        | Some algorithm, Some value -> SchemaVersion.createOutputDigest algorithm value |> Result.toOption
        | _ -> None

    let parseSource (element: JsonElement) =
        match stringProperty "path" element, tryProperty "digest" element with
        | Some path, Some digestElement ->
            let sourceArtifact =
                match ArtifactRef.create path (ArtifactKind.Other "generatedSource") ArtifactOwner.Sdd true with
                | Ok value -> value
                | Error _ -> artifact path

            let rawSchema =
                intProperty "schemaVersion" element |> Option.map string

            let compatibility = SchemaVersion.classifyRaw rawSchema

            parseDigest digestElement
            |> Option.map (fun digest ->
                let source: SourceIdentity =
                    { Artifact = sourceArtifact
                      Digest = digest
                      SchemaVersion = compatibility.Version
                      SchemaStatus = compatibility.Status
                      RawSchemaVersion = rawSchema }

                source)
        | _ -> None

    let parseGenerator (element: JsonElement) =
        match stringProperty "id" element, stringProperty "version" element with
        | Some id, Some version -> SchemaVersion.createGeneratorVersion id version |> Result.toOption
        | _ -> None

    let parseWorkModelMetadata (path: string) (json: string) =
        let generatedArtifact = artifact path

        try
            use document = JsonDocument.Parse json
            let root = document.RootElement

            let schema =
                intProperty "schemaVersion" root
                |> Option.map SchemaVersion.create

            let modelVersion = stringProperty "modelVersion" root

            let metadata =
                tryProperty "generatedViews" root
                |> Option.bind (fun generatedViews ->
                    if generatedViews.ValueKind = JsonValueKind.Array then
                        generatedViews.EnumerateArray()
                        |> Seq.tryFind (fun view -> stringProperty "kind" view = Some "workModel")
                    else
                        None)

            match metadata with
            | None ->
                Error
                    [ Diagnostics.staleGeneratedView
                          generatedArtifact
                          "Generated work-model JSON does not contain work-model metadata."
                          "Regenerate the view with a generatedViews workModel entry." ]
            | Some view ->
                let generator =
                    tryProperty "generator" view |> Option.bind parseGenerator

                let sources =
                    tryProperty "sources" view
                    |> Option.map (fun sources ->
                        if sources.ValueKind = JsonValueKind.Array then
                            sources.EnumerateArray() |> Seq.choose parseSource |> Seq.toList
                        else
                            [])
                    |> Option.defaultValue []

                let outputDigest =
                    tryProperty "outputDigest" view
                    |> Option.bind (fun value ->
                        match value.ValueKind with
                        | JsonValueKind.Object -> parseOutputDigest value
                        | _ -> None)

                Ok
                    { Path = stringProperty "path" view |> Option.defaultValue path
                      SchemaVersion = schema
                      ModelVersion = modelVersion
                      Generator = generator
                      Sources = sources
                      OutputDigest = outputDigest }
        with ex ->
            Error
                [ Diagnostics.staleGeneratedView
                      generatedArtifact
                      $"Generated work-model JSON could not be parsed: {ex.Message}"
                      "Regenerate the view with valid deterministic JSON." ]
