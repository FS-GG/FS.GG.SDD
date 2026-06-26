namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.SchemaVersion

module ScaffoldProvenance =
    type ScaffoldProducedPath =
        { Path: string
          Owner: ArtifactOwner }

    type ScaffoldProvenanceRecord =
        { SchemaVersion: int
          Generator: GeneratorVersion
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPath list }

    let provenancePath = ".fsgg/scaffold-provenance.json"

    let ownerFromValue (value: string) =
        match value with
        | "sdd" -> ArtifactOwner.Sdd
        | "governance" -> ArtifactOwner.Governance
        | "rendering" -> ArtifactOwner.Rendering
        | _ -> ArtifactOwner.GeneratedProduct

    let serialize (record: ScaffoldProvenanceRecord) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", record.SchemaVersion)
        writer.WriteStartObject("generator")
        writer.WriteString("id", record.Generator.Id)
        writer.WriteString("version", record.Generator.Version)
        writer.WriteEndObject()
        writer.WriteString("providerName", record.ProviderName)
        writer.WriteString("providerContractVersion", record.ProviderContractVersion)
        writer.WriteString("templateRef", record.TemplateRef)
        writer.WriteString("outcome", record.Outcome)
        writer.WriteStartArray("producedPaths")

        record.ProducedPaths
        |> List.sortBy (fun produced -> produced.Path)
        |> List.iter (fun produced ->
            writer.WriteStartObject()
            writer.WriteString("path", produced.Path)
            writer.WriteString("owner", ownerValue produced.Owner)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let tryParse (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | Some version when SchemaVersion.isSupported (SchemaVersion.create version) ->
                match tryJsonProperty "generator" root with
                | Some generatorElement ->
                    let generatorId = jsonString "id" generatorElement
                    let generatorVersion = jsonString "version" generatorElement
                    let providerName = jsonString "providerName" root
                    let providerContractVersion = jsonString "providerContractVersion" root
                    let templateRef = jsonString "templateRef" root
                    let outcome = jsonString "outcome" root

                    match generatorId, generatorVersion, providerName, providerContractVersion, templateRef, outcome with
                    | Some generatorId, Some generatorVersion, Some providerName, Some providerContractVersion, Some templateRef, Some outcome ->
                        let producedPaths =
                            jsonArray "producedPaths" root
                            |> List.choose (fun element ->
                                match jsonString "path" element with
                                | Some path when not (String.IsNullOrWhiteSpace path) ->
                                    Some
                                        { Path = path
                                          Owner = jsonString "owner" element |> Option.map ownerFromValue |> Option.defaultValue ArtifactOwner.GeneratedProduct }
                                | _ -> None)

                        Some
                            { SchemaVersion = version
                              Generator = { Id = generatorId; Version = generatorVersion }
                              ProviderName = providerName
                              ProviderContractVersion = providerContractVersion
                              TemplateRef = templateRef
                              Outcome = outcome
                              ProducedPaths = producedPaths }
                    | _ -> None
                | None -> None
            | _ -> None
        with _ -> None
