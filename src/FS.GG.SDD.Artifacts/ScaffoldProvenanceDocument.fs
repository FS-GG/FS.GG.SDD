namespace FS.GG.SDD.Artifacts

open System.Text.Json

module ScaffoldProvenanceDocument =
    type Document =
        | Legacy of ScaffoldProvenance.ScaffoldProvenanceRecord
        | Catalog of CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord

    let ownershipProjection document =
        match document with
        | Legacy record -> record
        | Catalog record -> record.Ownership

    let parse text =
        let error message : Fsgg.ProviderCatalog.Diagnostic =
            {
                Code = "provenance.malformed"
                Path = "$.schemaVersion"
                Message = message
            }

        try
            use doc = JsonDocument.Parse(text: string)

            let versions =
                doc.RootElement.EnumerateObject()
                |> Seq.filter (fun p -> p.Name = "schemaVersion")
                |> Seq.toList

            match versions with
            | [ property ] when property.Value.ValueKind = JsonValueKind.Number ->
                let mutable version = 0

                if
                    not (property.Value.TryGetInt32(&version))
                    || property.Value.GetRawText() <> string version
                then
                    Error [ error "Expected canonical schema integer." ]
                else
                    match version with
                    | 1 ->
                        match ScaffoldProvenance.tryParse text with
                        | Some record -> Ok(Legacy record)
                        | None -> Error [ error "Malformed legacy provenance." ]
                    | 2 -> CatalogScaffoldProvenance.parse text |> Result.map Catalog
                    | _ -> Error [ error "Unsupported provenance schema; no legacy fallback is permitted." ]
            | _ -> Error [ error "Provenance requires exactly one schemaVersion." ]
        with
        | :? JsonException -> Error [ error "Malformed provenance JSON." ]
        | :? System.InvalidOperationException -> Error [ error "Expected provenance object." ]
