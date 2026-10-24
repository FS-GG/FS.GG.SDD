namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json

module SourceScaffoldProvenance =
    type Record =
        { SchemaVersion: int
          Source: ProviderSource.Observation
          Catalog: CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord }

    let private refusal message : Fsgg.ProviderCatalog.Diagnostic list =
        [ { Code = "source.provenanceMismatch"; Path = "$"; Message = message } ]

    let private validate (record: Record) =
        let catalog = record.Catalog
        if record.SchemaVersion <> 3 then Error(refusal "Expected source provenance schema3.")
        elif record.Source.CatalogSha256 <> catalog.Declaration.RawCatalogDigest
             || record.Source.TemplateSource <> catalog.Declaration.Descriptor.TemplateSource
             || record.Source.ArchiveSha256 <> catalog.Declaration.Archive.Digest
             || record.Source.ArchiveSha256 <> catalog.Observation.ConsumedArchiveDigest then
            Error(refusal "Source raw catalog, template and archive identities must join the strict nested execution record.")
        else
            match ProviderSource.serializeObservation record.Source, CatalogScaffoldProvenance.serialize catalog with
            | Error errors, _ | _, Error errors -> Error errors
            | Ok source, Ok catalog -> Ok(source, catalog)

    let ownershipProjection (record: Record) = record.Catalog.Ownership

    let serialize record =
        validate record |> Result.map (fun (source, catalog) ->
            use stream = new MemoryStream()
            use writer = new Utf8JsonWriter(stream)
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", 3)
            writer.WritePropertyName "source"
            use sourceDocument = JsonDocument.Parse source
            sourceDocument.RootElement.WriteTo writer
            writer.WritePropertyName "catalog"
            use catalogDocument = JsonDocument.Parse catalog
            catalogDocument.RootElement.WriteTo writer
            writer.WriteEndObject()
            writer.Flush()
            Encoding.UTF8.GetString(stream.ToArray()))

    let parse (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement
            if root.ValueKind <> JsonValueKind.Object then Error(refusal "Expected a source provenance object.")
            else
                let names = root.EnumerateObject() |> Seq.map _.Name |> Seq.toList
                if names.Length <> 3 || Set.ofList names <> set [ "schemaVersion"; "source"; "catalog" ]
                   || root.GetProperty("schemaVersion").GetRawText() <> "3" then
                    Error(refusal "Source provenance requires exactly schemaVersion3, source and catalog.")
                else
                    match ProviderSource.parseObservation (root.GetProperty("source").GetRawText()),
                          CatalogScaffoldProvenance.parse (root.GetProperty("catalog").GetRawText()) with
                    | Error errors, _ | _, Error errors -> Error errors
                    | Ok source, Ok catalog ->
                        let record = { SchemaVersion = 3; Source = source; Catalog = catalog }
                        validate record |> Result.map (fun _ -> record)
        with
        | :? JsonException -> Error(refusal "Malformed source provenance JSON.")
        | :? InvalidOperationException -> Error(refusal "Unexpected source provenance value kind.")
