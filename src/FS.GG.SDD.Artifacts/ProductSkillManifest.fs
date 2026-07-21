namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json

module ProductSkillManifest =
    type ProductManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          ResolvablePath: string option
          MaterializesWhen: string
          SuppliedBy: string option }

    let tryParse (text: string) : Result<int * ProductManifestEntry list, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | None -> Error "skill-manifest.json: missing or non-integer 'schemaVersion'."
            | Some version ->
                let skills =
                    jsonArray "skills" root
                    |> List.choose (fun element ->
                        match jsonString "id" element with
                        | Some id when not (String.IsNullOrWhiteSpace id) ->
                            Some
                                { Id = id.Trim()
                                  Scope = jsonString "scope" element |> Option.defaultValue "" |> (fun s -> s.Trim())
                                  Sha256 = jsonString "sha256" element |> Option.defaultValue "" |> (fun s -> s.Trim())
                                  ResolvablePath =
                                    jsonString "resolvablePath" element
                                    |> Option.map (fun s -> s.Trim())
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                  MaterializesWhen =
                                    jsonString "materializes-when" element
                                    |> Option.map (fun s -> s.Trim())
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                    |> Option.defaultValue "always"
                                  SuppliedBy =
                                    jsonString "supplied-by" element
                                    |> Option.map (fun s -> s.Trim())
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not) }
                        | _ -> None)

                Ok(version, skills)
        with ex ->
            Error(sprintf "skill-manifest.json: %s" ex.Message)

    let serialize (schemaVersion: int) (entries: ProductManifestEntry list) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", schemaVersion)
        writer.WriteStartArray("skills")

        // Sorted by id so the emitted bytes are deterministic and reconcilable — the same
        // discipline SkillManifestJson and the provider's own generator keep.
        entries
        |> List.sortBy (fun entry -> entry.Id)
        |> List.iter (fun entry ->
            writer.WriteStartObject()
            writer.WriteString("id", entry.Id)
            writer.WriteString("scope", entry.Scope)
            writer.WriteString("sha256", entry.Sha256)

            match entry.ResolvablePath with
            | Some path -> writer.WriteString("resolvablePath", path)
            | None -> ()

            writer.WriteString("materializes-when", entry.MaterializesWhen)

            match entry.SuppliedBy with
            | Some supplier -> writer.WriteString("supplied-by", supplier)
            | None -> ()

            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        // Trailing LF so the artifact is POSIX-clean; Utf8JsonWriter emits `\n` for indentation
        // (not Environment.NewLine), so the bytes are platform-stable.
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let amend (existingText: string) (additions: ProductManifestEntry list) : string option =
        match tryParse existingText with
        | Error _ -> None
        | Ok(schemaVersion, existing) ->
            let existingIds = existing |> List.map (fun entry -> entry.Id) |> Set.ofList

            let newEntries =
                additions |> List.filter (fun entry -> not (existingIds.Contains entry.Id))

            Some(serialize schemaVersion (existing @ newEntries))
