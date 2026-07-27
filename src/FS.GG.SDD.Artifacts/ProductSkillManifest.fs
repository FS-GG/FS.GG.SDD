namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json

module ProductSkillManifest =
    type ProductManifestFile = { Path: string; Sha256: string }

    type ProductManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          ResolvablePath: string option
          MaterializesWhen: string
          SuppliedBy: string option
          Files: ProductManifestFile list }

    type AmendRefusal =
        | ManifestUnparseable of message: string
        | SchemaVersionUnroundTrippable of schemaVersion: int
        | AdditionsMissingFileSet of schemaVersion: int * ids: string list

    // The highest ADR-0017 product-manifest schema this codec can read AND re-emit without
    // losing a declared property. v1 = the six scalar row properties; v2 (FS.GG.SDD#727) adds
    // the complete per-file digest set. A HIGHER version may carry row properties `tryParse`
    // does not model, and re-serializing would drop them silently — `amend` refuses instead
    // (FS.GG.SDD#739); `tryParse` stays tolerant, because READING a future document to inspect
    // it loses nothing.
    let private highestRoundTrippableSchemaVersion = 2

    // v2 declares `files`; v1 has no such property, so a v1 document that grew one would be
    // v1-in-the-header-only — the mirror image of the defect #739 closes. One predicate, used by
    // both the writer and the addition-completeness check, so they cannot disagree.
    let private declaresFiles schemaVersion = schemaVersion >= 2

    let private parseFiles (element: JsonElement) : Result<ProductManifestFile list, string> =
        match tryJsonProperty "files" element with
        | None -> Ok []
        | Some files when files.ValueKind <> JsonValueKind.Array -> Error "'files' must be an array"
        | Some files ->
            let folder (acc: Result<ProductManifestFile list, string>) (row: JsonElement) =
                acc
                |> Result.bind (fun rows ->
                    let field name =
                        jsonString name row
                        |> Option.map (fun value -> value.Trim())
                        |> Option.filter (String.IsNullOrWhiteSpace >> not)

                    match field "path", field "sha256" with
                    | Some path, Some sha256 -> Ok(rows @ [ { Path = path; Sha256 = sha256 } ])
                    | None, _ -> Error "a 'files' row is missing 'path'"
                    | _, None -> Error "a 'files' row is missing 'sha256'")

            (Ok [], files.EnumerateArray() |> Seq.toList) ||> List.fold folder

    let tryParse (text: string) : Result<int * ProductManifestEntry list, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | None -> Error "skill-manifest.json: missing or non-integer 'schemaVersion'."
            | Some version ->
                let folder (acc: Result<ProductManifestEntry list, string>) (element: JsonElement) =
                    acc
                    |> Result.bind (fun entries ->
                        match jsonString "id" element with
                        | Some id when not (String.IsNullOrWhiteSpace id) ->
                            // A declared `files` array that cannot be read is FAIL-CLOSED, not a
                            // dropped row: a manifest whose file set is unreadable must never be
                            // rewritten from the half of it that parsed (FS.GG.SDD#739).
                            parseFiles element
                            |> Result.mapError (fun message -> $"skill-manifest.json: skill '{id.Trim()}': {message}.")
                            |> Result.map (fun files ->
                                entries
                                @ [ { Id = id.Trim()
                                      Scope =
                                        jsonString "scope" element |> Option.defaultValue "" |> (fun s -> s.Trim())
                                      Sha256 =
                                        jsonString "sha256" element |> Option.defaultValue "" |> (fun s -> s.Trim())
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
                                        |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                      Files = files } ])
                        | _ -> Ok entries)

                (Ok [], jsonArray "skills" root)
                ||> List.fold folder
                |> Result.map (fun skills -> version, skills)
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

            // ADR-0017 v2 (FS.GG.SDD#727) — emitted LAST and only at v2, for the reason
            // SkillManifestJson states: v2 is a pure APPEND to each v1 row, so a positional or
            // regex reader still sees the v1 document it already parses. Gating on the version
            // (not on `Files` being non-empty) is what keeps AC3 true — a v1 document amended by
            // this codec comes out v1, byte-for-byte the shape it went in as, even when the
            // additions carry a file set v1 has nowhere to put.
            if declaresFiles schemaVersion then
                writer.WriteStartArray("files")

                entry.Files
                |> List.sortBy (fun file -> file.Path)
                |> List.iter (fun file ->
                    writer.WriteStartObject()
                    writer.WriteString("path", file.Path)
                    writer.WriteString("sha256", file.Sha256)
                    writer.WriteEndObject())

                writer.WriteEndArray()

            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        // Trailing LF so the artifact is POSIX-clean; Utf8JsonWriter emits `\n` for indentation
        // (not Environment.NewLine), so the bytes are platform-stable.
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let amend (existingText: string) (additions: ProductManifestEntry list) : Result<string, AmendRefusal> =
        match tryParse existingText with
        | Error message -> Error(ManifestUnparseable message)
        | Ok(schemaVersion, existing) ->
            if schemaVersion > highestRoundTrippableSchemaVersion then
                // The header would keep asserting a schema whose rows this codec cannot carry —
                // the #739 defect one version further on. Refuse, LOUDLY (the caller must
                // diagnose): a wrong document is worse than a missing amend, and a missing amend
                // that nobody is told about is how #739 got written in the first place.
                Error(SchemaVersionUnroundTrippable schemaVersion)
            else
                let existingIds = existing |> List.map (fun entry -> entry.Id) |> Set.ofList

                let newEntries =
                    additions |> List.filter (fun entry -> not (existingIds.Contains entry.Id))

                let incomplete =
                    if declaresFiles schemaVersion then
                        newEntries
                        |> List.filter (fun entry -> List.isEmpty entry.Files)
                        |> List.map (fun entry -> entry.Id)
                        |> List.sort
                    else
                        []

                if not (List.isEmpty incomplete) then
                    // Folding these in would produce the v2-in-the-header-only document by the
                    // OTHER route #739 names: some rows with `files`, some without. The caller
                    // owns the complete file set of everything it materializes, so a row without
                    // one is a caller defect, and it is refused rather than half-declared.
                    Error(AdditionsMissingFileSet(schemaVersion, incomplete))
                else
                    Ok(serialize schemaVersion (existing @ newEntries))
