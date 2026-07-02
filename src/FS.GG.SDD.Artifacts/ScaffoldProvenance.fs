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
          Owner: ArtifactOwner
          // Additive (contract 1.1.0, ADR-0014 §Decision 3): the per-skill/per-path
          // content digest. `None` ⇒ no digest recorded (a 1.0.0 document, or a path
          // not yet hashed — digest population is P1). Serialized only when `Some`.
          Sha256: string option }

    type ScaffoldProvenanceRecord =
        { SchemaVersion: int
          Generator: GeneratorVersion
          RequiredMinimumCliVersion: string option
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPath list
          MirroredPaths: ScaffoldProducedPath list
          EffectiveParameters: (string * string) list }

    let provenancePath = ".fsgg/scaffold-provenance.json"

    let ownerFromValue (value: string) =
        match value with
        | "sdd" -> ArtifactOwner.Sdd
        | "governance" -> ArtifactOwner.Governance
        | "rendering" -> ArtifactOwner.Rendering
        | "mirrored" -> ArtifactOwner.Mirrored
        | _ -> ArtifactOwner.GeneratedProduct

    // Additive (contract 1.1.0, ADR-0014): emit `sha256` only when a digest was
    // recorded, so digest-free provenance (every path today) stays byte-identical to
    // 1.0.0 output. Blank digests are treated as absent.
    let private writeSha256 (writer: Utf8JsonWriter) (sha256: string option) =
        match sha256 with
        | Some value when not (String.IsNullOrWhiteSpace value) -> writer.WriteString("sha256", value)
        | _ -> ()

    // Absent, null, or blank `sha256` ⇒ `None` (a 1.0.0 document has no digest).
    let private readSha256 (element: JsonElement) =
        match jsonString "sha256" element with
        | Some value when not (String.IsNullOrWhiteSpace value) -> Some value
        | _ -> None

    let serialize (record: ScaffoldProvenanceRecord) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", record.SchemaVersion)
        writer.WriteStartObject("generator")
        writer.WriteString("id", record.Generator.Id)
        writer.WriteString("version", record.Generator.Version)
        writer.WriteEndObject()

        // Additive optional field (feature 052 E1): always present as string-or-null,
        // immediately after `generator`, so the required minimum sits beside the
        // producing CLI version. `None` ⇒ null (never fabricated).
        match record.RequiredMinimumCliVersion with
        | Some value -> writer.WriteString("requiredMinimumCliVersion", value)
        | None -> writer.WriteNull("requiredMinimumCliVersion")

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
            writeSha256 writer produced.Sha256
            writer.WriteEndObject())

        writer.WriteEndArray()

        // 056: additive mirror record — the `.claude`/`.codex` fan-out copies of the
        // provider's `.agents/skills/*` skills, owner `mirrored`. Sorted by path,
        // immediately after `producedPaths`; empty array when nothing was mirrored
        // (schema stays v1). `"mirrored"` appears only inside this array.
        writer.WriteStartArray("mirroredPaths")

        record.MirroredPaths
        |> List.sortBy (fun mirrored -> mirrored.Path)
        |> List.iter (fun mirrored ->
            writer.WriteStartObject()
            writer.WriteString("path", mirrored.Path)
            writer.WriteString("owner", ownerValue mirrored.Owner)
            writeSha256 writer mirrored.Sha256
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("effectiveParameters")

        record.EffectiveParameters
        |> List.sortBy fst
        |> List.iter (fun (key, value) ->
            writer.WriteStartObject()
            writer.WriteString("key", key)
            writer.WriteString("value", value)
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
                                          Owner = jsonString "owner" element |> Option.map ownerFromValue |> Option.defaultValue ArtifactOwner.GeneratedProduct
                                          Sha256 = readSha256 element }
                                | _ -> None)

                        // 056: additive mirror record. Absent/null ⇒ `[]`, so provenance
                        // written before this field still parses (schema stays v1).
                        let mirroredPaths =
                            jsonArray "mirroredPaths" root
                            |> List.choose (fun element ->
                                match jsonString "path" element with
                                | Some path when not (String.IsNullOrWhiteSpace path) ->
                                    Some
                                        { Path = path
                                          Owner = jsonString "owner" element |> Option.map ownerFromValue |> Option.defaultValue ArtifactOwner.Mirrored
                                          Sha256 = readSha256 element }
                                | _ -> None)

                        // Additive optional field (D3): absent ⇒ `[]`, so provenance
                        // written before `effectiveParameters` still parses.
                        let effectiveParameters =
                            jsonArray "effectiveParameters" root
                            |> List.choose (fun element ->
                                match jsonString "key" element, jsonString "value" element with
                                | Some key, Some value -> Some(key, value)
                                | Some key, None -> Some(key, "")
                                | _ -> None)

                        // Additive optional (feature 052 E1): absent key or JSON null
                        // ⇒ `None`, so records written before this field still parse.
                        let requiredMinimumCliVersion = jsonString "requiredMinimumCliVersion" root

                        Some
                            { SchemaVersion = version
                              Generator = { Id = generatorId; Version = generatorVersion }
                              RequiredMinimumCliVersion = requiredMinimumCliVersion
                              ProviderName = providerName
                              ProviderContractVersion = providerContractVersion
                              TemplateRef = templateRef
                              Outcome = outcome
                              ProducedPaths = producedPaths
                              MirroredPaths = mirroredPaths
                              EffectiveParameters = effectiveParameters }
                    | _ -> None
                | None -> None
            | _ -> None
        with _ -> None
