namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Text.Json

type QuintMarkdownSource =
    { Path: string
      Text: string
      Sha256: string }

type QuintFence =
    { Ordinal: int
      Target: string
      ModuleName: string
      SourceRange: QuintSourceRange
      ContentSha256: string }

type QuintFenceManifest =
    { Schema: string
      SourcePath: string
      SourceSha256: string
      Fences: QuintFence list }

type QuintGeneratedModule =
    { Target: string
      Sha256: string
      Bytes: int64 }

type QuintExtractionObservation =
    { First: QuintGeneratedModule list
      Second: QuintGeneratedModule list
      Warnings: string list }

type QuintSourceBinding =
    { FenceOrdinal: int
      Range: QuintSourceRange }

type QuintSourceMapEntry =
    { Target: string
      GeneratedRange: QuintSourceRange
      Source: QuintSourceBinding }

type QuintSourceMap =
    { Schema: string
      SourceSha256: string
      Entries: QuintSourceMapEntry list }

module private QuintSourceInternal =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = None }

    let located code path message (position: QuintSourcePosition) : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location =
            Some
                { Line = position.Line
                  Column = position.Column } }

    let sortDiagnostics (diagnostics: SpecificationDiagnostic list) =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture))
        |> String.concat ""

    let isSha256 (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let comparePosition (left: QuintSourcePosition) (right: QuintSourcePosition) =
        compare (left.Line, left.Column) (right.Line, right.Column)

    let validRange (range: QuintSourceRange) =
        range.Start.Line > 0
        && range.Start.Column > 0
        && range.End.Line > 0
        && range.End.Column > 0
        && comparePosition range.Start range.End <= 0

    let contains (position: QuintSourcePosition) (range: QuintSourceRange) =
        comparePosition range.Start position <= 0
        && comparePosition position range.End <= 0

    let positionExists (text: string) (position: QuintSourcePosition) =
        let lines = text.Split('\n')

        position.Line > 0
        && position.Line <= lines.Length
        && position.Column > 0
        && position.Column <= max 1 lines[position.Line - 1].Length

    let isSafeRelativePath (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && not (IO.Path.IsPathRooted value)
        && not (value.Contains('\\'))
        && value.Split('/')
           |> Array.forall (fun segment -> not (String.IsNullOrWhiteSpace segment) && segment <> "." && segment <> "..")

    let isSafeTarget (value: string) =
        isSafeRelativePath value
        && not (value.Contains('/'))
        && value.EndsWith(".qnt", StringComparison.Ordinal)
        && value.Length > 4

    let writePosition (writer: Utf8JsonWriter) position =
        writer.WriteStartObject()
        writer.WriteNumber("line", position.Line)
        writer.WriteNumber("column", position.Column)
        writer.WriteEndObject()

    let writeRange (writer: Utf8JsonWriter) (range: QuintSourceRange) =
        writer.WriteStartObject()
        writer.WriteString("path", range.Path)
        writer.WritePropertyName("start")
        writePosition writer range.Start
        writer.WritePropertyName("end")
        writePosition writer range.End
        writer.WriteEndObject()

    let encode write value =
        use stream = new IO.MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false))
        write writer value
        writer.Flush()
        stream.ToArray()

    let writeFenceManifest (writer: Utf8JsonWriter) (manifest: QuintFenceManifest) =
        writer.WriteStartObject()
        writer.WriteString("schema", manifest.Schema)
        writer.WriteString("sourcePath", manifest.SourcePath)
        writer.WriteString("sourceSha256", manifest.SourceSha256)
        writer.WritePropertyName("fences")
        writer.WriteStartArray()

        for fence in manifest.Fences |> List.sortBy (fun item -> item.Ordinal) do
            writer.WriteStartObject()
            writer.WriteNumber("ordinal", fence.Ordinal)
            writer.WriteString("target", fence.Target)
            writer.WriteString("moduleName", fence.ModuleName)
            writer.WritePropertyName("sourceRange")
            writeRange writer fence.SourceRange
            writer.WriteString("contentSha256", fence.ContentSha256)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()

    let entrySortKey (entry: QuintSourceMapEntry) =
        entry.Target,
        entry.GeneratedRange.Start.Line,
        entry.GeneratedRange.Start.Column,
        entry.GeneratedRange.End.Line,
        entry.GeneratedRange.End.Column,
        entry.Source.FenceOrdinal

    let writeSourceMap (writer: Utf8JsonWriter) (sourceMap: QuintSourceMap) =
        writer.WriteStartObject()
        writer.WriteString("schema", sourceMap.Schema)
        writer.WriteString("sourceSha256", sourceMap.SourceSha256)
        writer.WritePropertyName("entries")
        writer.WriteStartArray()

        for entry in sourceMap.Entries |> List.sortBy entrySortKey do
            writer.WriteStartObject()
            writer.WriteString("target", entry.Target)
            writer.WritePropertyName("generatedRange")
            writeRange writer entry.GeneratedRange
            writer.WritePropertyName("source")
            writer.WriteStartObject()
            writer.WriteNumber("fenceOrdinal", entry.Source.FenceOrdinal)
            writer.WritePropertyName("range")
            writeRange writer entry.Source.Range
            writer.WriteEndObject()
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()

    let requireFields path expected (element: JsonElement) =
        let names =
            element.EnumerateObject()
            |> Seq.map (fun property -> property.Name)
            |> Seq.toList

        let actual = names |> Set.ofList

        if actual = expected && names.Length = expected.Count then
            Ok()
        else
            Error(
                diagnostic
                    "QUINT-SOURCE-MAP-FIELDS-INVALID"
                    path
                    "Source-map object fields do not match the closed v1 schema."
            )

    let readString (name: string) (element: JsonElement) : string =
        match element.GetProperty(name).GetString() with
        | null -> raise (JsonException($"Property '%s{name}' must be a string."))
        | value -> value

    let readPosition path (element: JsonElement) =
        match requireFields path (Set.ofList [ "line"; "column" ]) element with
        | Error error -> Error error
        | Ok() ->
            try
                Ok
                    { Line = element.GetProperty("line").GetInt32()
                      Column = element.GetProperty("column").GetInt32() }
            with _ ->
                Error(
                    diagnostic
                        "QUINT-SOURCE-MAP-VALUE-INVALID"
                        path
                        "Source-map position must contain integer line and column values."
                )

    let readRange path (element: JsonElement) =
        match requireFields path (Set.ofList [ "path"; "start"; "end" ]) element with
        | Error error -> Error error
        | Ok() ->
            match
                readPosition (path + "/start") (element.GetProperty("start")),
                readPosition (path + "/end") (element.GetProperty("end"))
            with
            | Ok start, Ok finish ->
                Ok
                    { Path = readString "path" element
                      Start = start
                      End = finish }
            | Error error, _
            | _, Error error -> Error error

[<RequireQualifiedAccess>]
module QuintSource =
    let fenceManifestSchema = "fsgg.quint.fence-manifest/v1"
    let sourceMapSchema = "fsgg.quint.source-map/v1"

    let createMarkdown path (bytes: byte array) =
        let diagnostics =
            [ if not (QuintSourceInternal.isSafeRelativePath path) then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-PATH-UNSAFE"
                          "/source/path"
                          "Canonical Markdown path must be a safe repository-relative path."

              if bytes.Length >= 3 && bytes[0] = 0xEFuy && bytes[1] = 0xBBuy && bytes[2] = 0xBFuy then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-BOM-REFUSED"
                          "/source"
                          "Canonical UTF-8 Markdown must not contain a byte-order mark." ]

        let decoded =
            try
                Ok(UTF8Encoding(false, true).GetString bytes)
            with :? DecoderFallbackException ->
                Error(
                    QuintSourceInternal.diagnostic
                        "QUINT-SOURCE-UTF8-INVALID"
                        "/source"
                        "Canonical Markdown is not valid UTF-8."
                )

        match decoded with
        | Error error -> Error(QuintSourceInternal.sortDiagnostics (error :: diagnostics))
        | Ok text ->
            let allDiagnostics =
                [ yield! diagnostics

                  if text.Contains('\r') then
                      yield
                          QuintSourceInternal.diagnostic
                              "QUINT-SOURCE-LINE-ENDINGS-NONCANONICAL"
                              "/source"
                              "Canonical Markdown must use LF line endings." ]
                |> QuintSourceInternal.sortDiagnostics

            match allDiagnostics with
            | [] ->
                Ok
                    { Path = path
                      Text = text
                      Sha256 = QuintSourceInternal.sha256 bytes }
            | errors -> Error errors

    let validateManifest (source: QuintMarkdownSource) (manifest: QuintFenceManifest) =
        [ if manifest.Schema <> fenceManifestSchema then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-FENCE-MANIFEST-SCHEMA-MISMATCH"
                      "/schema"
                      $"Expected '%s{fenceManifestSchema}' but found '%s{manifest.Schema}'."

          if not (QuintSourceInternal.isSafeRelativePath source.Path) then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-PATH-UNSAFE"
                      "/source/path"
                      "Canonical Markdown path must be a safe repository-relative path."

          if source.Text.Contains('\r') then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-LINE-ENDINGS-NONCANONICAL"
                      "/source"
                      "Canonical Markdown must use LF line endings."

          let actualSourceSha =
              source.Text |> Encoding.UTF8.GetBytes |> QuintSourceInternal.sha256

          if
              not (QuintSourceInternal.isSha256 source.Sha256)
              || actualSourceSha <> source.Sha256
          then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-DIGEST-MISMATCH"
                      "/source/sha256"
                      "Canonical Markdown text does not match its SHA-256 receipt."

          if manifest.SourcePath <> source.Path then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-FENCE-SOURCE-PATH-MISMATCH"
                      "/sourcePath"
                      $"Expected canonical source path '%s{source.Path}' but found '%s{manifest.SourcePath}'."

          if
              not (QuintSourceInternal.isSha256 manifest.SourceSha256)
              || manifest.SourceSha256 <> source.Sha256
          then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-FENCE-SOURCE-DIGEST-MISMATCH"
                      "/sourceSha256"
                      $"Fence manifest does not bind canonical source SHA-256 '%s{source.Sha256}'."

          for moduleName, fences in
              manifest.Fences
              |> List.groupBy (fun item -> item.ModuleName)
              |> List.filter (fun (_, fences) -> fences |> List.map _.Target |> List.distinct |> List.length > 1) do
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-FENCE-MODULE-DUPLICATE"
                      "/fences"
                      $"Quint module '%s{moduleName}' is declared for more than one generated target."

          for index, fence in manifest.Fences |> List.indexed do
              let path = $"/fences/%d{index}"

              if fence.Ordinal <> index then
                  yield
                      QuintSourceInternal.located
                          "QUINT-FENCE-ORDER-MISMATCH"
                          (path + "/ordinal")
                          $"Fence ordinal must be %d{index} in document order."
                          fence.SourceRange.Start

              if not (QuintSourceInternal.isSafeTarget fence.Target) then
                  yield
                      QuintSourceInternal.located
                          "QUINT-FENCE-TARGET-UNSAFE"
                          (path + "/target")
                          "Fence target must be one plain non-empty '.qnt' filename."
                          fence.SourceRange.Start

              if String.IsNullOrWhiteSpace fence.ModuleName then
                  yield
                      QuintSourceInternal.located
                          "QUINT-FENCE-MODULE-REQUIRED"
                          (path + "/moduleName")
                          "Fence module name is required."
                          fence.SourceRange.Start

              if
                  not (String.IsNullOrWhiteSpace fence.ModuleName)
                  && (not (Char.IsAsciiLetter fence.ModuleName[0])
                      || fence.ModuleName
                         |> Seq.exists (fun character -> not (Char.IsAsciiLetterOrDigit character || character = '_')))
              then
                  yield
                      QuintSourceInternal.located
                          "QUINT-FENCE-MODULE-INVALID"
                          (path + "/moduleName")
                          "Fence module name must begin with an ASCII letter and contain only ASCII letters, digits, or underscore."
                          fence.SourceRange.Start

              if not (QuintSourceInternal.validRange fence.SourceRange) then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-FENCE-SOURCE-RANGE-INVALID"
                          (path + "/sourceRange")
                          "Fence source range must be a positive, non-empty, inclusive range."

              if
                  QuintSourceInternal.validRange fence.SourceRange
                  && (not (QuintSourceInternal.positionExists source.Text fence.SourceRange.Start)
                      || not (QuintSourceInternal.positionExists source.Text fence.SourceRange.End))
              then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-FENCE-SOURCE-RANGE-OUTSIDE-DOCUMENT"
                          (path + "/sourceRange")
                          "Fence source range must be contained by canonical Markdown text."

              if fence.SourceRange.Path <> source.Path then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-FENCE-SOURCE-RANGE-PATH-MISMATCH"
                          (path + "/sourceRange/path")
                          "Fence source range must name the canonical Markdown path."

              if not (QuintSourceInternal.isSha256 fence.ContentSha256) then
                  yield
                      QuintSourceInternal.located
                          "QUINT-FENCE-CONTENT-DIGEST-INVALID"
                          (path + "/contentSha256")
                          "Fence content SHA-256 must be lowercase hexadecimal."
                          fence.SourceRange.Start ]
        |> QuintSourceInternal.sortDiagnostics

    let validateExtraction source manifest observation =
        let expectedTargets =
            manifest.Fences |> List.map (fun item -> item.Target) |> List.distinct

        let validatePass (passName: string) (modules: QuintGeneratedModule list) =
            [ for target, _ in
                  modules
                  |> List.countBy (fun item -> item.Target)
                  |> List.filter (fun (_, count) -> count > 1) do
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-EXTRACTION-TARGET-DUPLICATE"
                          $"/extraction/%s{passName}"
                          $"Generated target '%s{target}' is duplicated."

              if (modules |> List.map (fun item -> item.Target)) <> expectedTargets then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-EXTRACTION-TARGET-ORDER-MISMATCH"
                          $"/extraction/%s{passName}"
                          "Generated targets must exactly match fence document order."

              for index, item in modules |> List.indexed do
                  if not (QuintSourceInternal.isSha256 item.Sha256) then
                      yield
                          QuintSourceInternal.diagnostic
                              "QUINT-EXTRACTION-MODULE-DIGEST-INVALID"
                              $"/extraction/%s{passName}/%d{index}/sha256"
                              "Generated module SHA-256 must be lowercase hexadecimal."

                  if item.Bytes < 0L then
                      yield
                          QuintSourceInternal.diagnostic
                              "QUINT-EXTRACTION-MODULE-SIZE-INVALID"
                              $"/extraction/%s{passName}/%d{index}/bytes"
                              "Generated module byte count cannot be negative." ]

        [ yield! validateManifest source manifest
          yield! validatePass "first" observation.First
          yield! validatePass "second" observation.Second

          for index, warning in observation.Warnings |> List.indexed do
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-EXTRACTION-WARNING"
                      $"/extraction/warnings/%d{index}"
                      $"Extractor warning is an error: %s{warning}"

          if observation.First <> observation.Second then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-EXTRACTION-NONDETERMINISTIC"
                      "/extraction"
                      "Two clean isolated extractions did not produce byte-identical ordered module receipts." ]
        |> QuintSourceInternal.sortDiagnostics

    let encodeFenceManifest manifest =
        QuintSourceInternal.encode QuintSourceInternal.writeFenceManifest manifest

    let fenceManifestFingerprint manifest =
        manifest |> encodeFenceManifest |> QuintSourceInternal.sha256

    let decodeFenceManifest (bytes: byte array) =
        try
            use document = JsonDocument.Parse bytes
            let root = document.RootElement

            match
                QuintSourceInternal.requireFields "/" (set [ "schema"; "sourcePath"; "sourceSha256"; "fences" ]) root
            with
            | Error finding -> Error [ finding ]
            | Ok() when root.GetProperty("fences").ValueKind <> JsonValueKind.Array ->
                Error
                    [ QuintSourceInternal.diagnostic
                          "QUINT-FENCE-MANIFEST-VALUE-INVALID"
                          "/fences"
                          "Fence manifest fences must be an array." ]
            | Ok() ->
                let decoded =
                    root.GetProperty("fences").EnumerateArray()
                    |> Seq.mapi (fun index item ->
                        let path = $"/fences/{index}"

                        match
                            QuintSourceInternal.requireFields
                                path
                                (set [ "ordinal"; "target"; "moduleName"; "sourceRange"; "contentSha256" ])
                                item
                        with
                        | Error finding -> Error finding
                        | Ok() ->
                            match
                                QuintSourceInternal.readRange (path + "/sourceRange") (item.GetProperty("sourceRange"))
                            with
                            | Error finding -> Error finding
                            | Ok sourceRange ->
                                try
                                    Ok
                                        { Ordinal = item.GetProperty("ordinal").GetInt32()
                                          Target = QuintSourceInternal.readString "target" item
                                          ModuleName = QuintSourceInternal.readString "moduleName" item
                                          SourceRange = sourceRange
                                          ContentSha256 = QuintSourceInternal.readString "contentSha256" item }
                                with ex ->
                                    Error(
                                        QuintSourceInternal.diagnostic
                                            "QUINT-FENCE-MANIFEST-VALUE-INVALID"
                                            path
                                            ex.Message
                                    ))
                    |> Seq.toList

                match
                    decoded
                    |> List.choose (function
                        | Error finding -> Some finding
                        | _ -> None)
                with
                | [] ->
                    Ok
                        { Schema = QuintSourceInternal.readString "schema" root
                          SourcePath = QuintSourceInternal.readString "sourcePath" root
                          SourceSha256 = QuintSourceInternal.readString "sourceSha256" root
                          Fences =
                            decoded
                            |> List.choose (function
                                | Ok fence -> Some fence
                                | _ -> None) }
                | findings -> Error(QuintSourceInternal.sortDiagnostics findings)
        with ex ->
            Error [ QuintSourceInternal.diagnostic "QUINT-FENCE-MANIFEST-MALFORMED" "/" ex.Message ]

    let validateSourceMap source manifest sourceMap =
        let fences =
            manifest.Fences |> List.map (fun item -> item.Ordinal, item) |> Map.ofList

        [ yield! validateManifest source manifest

          if sourceMap.Schema <> sourceMapSchema then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-MAP-SCHEMA-MISMATCH"
                      "/schema"
                      $"Expected '%s{sourceMapSchema}' but found '%s{sourceMap.Schema}'."

          if
              not (QuintSourceInternal.isSha256 sourceMap.SourceSha256)
              || sourceMap.SourceSha256 <> source.Sha256
          then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-MAP-DIGEST-MISMATCH"
                      "/sourceSha256"
                      "Source map does not bind the canonical Markdown digest."

          let canonicalEntries =
              sourceMap.Entries |> List.sortBy QuintSourceInternal.entrySortKey

          if sourceMap.Entries <> canonicalEntries then
              yield
                  QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-MAP-ORDER-MISMATCH"
                      "/entries"
                      "Source-map entries must be in canonical generated-range order."

          for target, entries in sourceMap.Entries |> List.groupBy (fun item -> item.Target) do
              let ordered = entries |> List.sortBy QuintSourceInternal.entrySortKey

              for previous, current in ordered |> List.pairwise do
                  if
                      QuintSourceInternal.comparePosition current.GeneratedRange.Start previous.GeneratedRange.End
                      <= 0
                  then
                      yield
                          QuintSourceInternal.diagnostic
                              "QUINT-SOURCE-MAP-GENERATED-RANGE-OVERLAP"
                              "/entries"
                              $"Generated ranges for target '%s{target}' overlap."

          for index, entry in sourceMap.Entries |> List.indexed do
              let path = $"/entries/%d{index}"

              if not (QuintSourceInternal.isSafeTarget entry.Target) then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-TARGET-UNSAFE"
                          (path + "/target")
                          "Source-map target must be one plain '.qnt' filename."

              if not (QuintSourceInternal.validRange entry.GeneratedRange) then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-GENERATED-RANGE-INVALID"
                          (path + "/generatedRange")
                          "Generated range must be positive, non-empty, and inclusive."

              if entry.GeneratedRange.Path <> entry.Target then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-GENERATED-PATH-MISMATCH"
                          (path + "/generatedRange/path")
                          "Generated range path must equal its plain Quint target."

              if not (QuintSourceInternal.validRange entry.Source.Range) then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-SOURCE-RANGE-INVALID"
                          (path + "/source/range")
                          "Canonical source range must be positive, non-empty, and inclusive."

              if entry.Source.Range.Path <> source.Path then
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-PATH-MISMATCH"
                          (path + "/source/range/path")
                          "Source-map binding must name the canonical Markdown path."

              match Map.tryFind entry.Source.FenceOrdinal fences with
              | None ->
                  yield
                      QuintSourceInternal.diagnostic
                          "QUINT-SOURCE-MAP-FENCE-UNKNOWN"
                          (path + "/source/fenceOrdinal")
                          $"Fence ordinal %d{entry.Source.FenceOrdinal} is not declared by the manifest."
              | Some fence ->
                  if fence.Target <> entry.Target then
                      yield
                          QuintSourceInternal.diagnostic
                              "QUINT-SOURCE-MAP-FENCE-TARGET-MISMATCH"
                              (path + "/target")
                              "Source-map target does not match its bound fence target."

                  if
                      QuintSourceInternal.comparePosition fence.SourceRange.Start entry.Source.Range.Start > 0
                      || QuintSourceInternal.comparePosition entry.Source.Range.End fence.SourceRange.End > 0
                  then
                      yield
                          QuintSourceInternal.located
                              "QUINT-SOURCE-MAP-RANGE-OUTSIDE-FENCE"
                              (path + "/source/range")
                              "Source-map range must be contained by its bound fence range."
                              entry.Source.Range.Start ]
        |> QuintSourceInternal.sortDiagnostics

    let encodeSourceMap sourceMap =
        QuintSourceInternal.encode QuintSourceInternal.writeSourceMap sourceMap

    let decodeSourceMap (bytes: byte array) =
        try
            use document = JsonDocument.Parse(bytes)
            let root = document.RootElement

            match QuintSourceInternal.requireFields "/" (Set.ofList [ "schema"; "sourceSha256"; "entries" ]) root with
            | Error error -> Error [ error ]
            | Ok() ->
                let schema = QuintSourceInternal.readString "schema" root
                let sourceSha = QuintSourceInternal.readString "sourceSha256" root

                if schema <> sourceMapSchema then
                    Error
                        [ QuintSourceInternal.diagnostic
                              "QUINT-SOURCE-MAP-SCHEMA-MISMATCH"
                              "/schema"
                              $"Expected '%s{sourceMapSchema}' but found '%s{schema}'." ]
                else
                    let mutable errors = []

                    let entries =
                        root.GetProperty("entries").EnumerateArray()
                        |> Seq.mapi (fun index element ->
                            let path = $"/entries/%d{index}"

                            match
                                QuintSourceInternal.requireFields
                                    path
                                    (Set.ofList [ "target"; "generatedRange"; "source" ])
                                    element
                            with
                            | Error error ->
                                errors <- error :: errors
                                None
                            | Ok() ->
                                try
                                    let sourceElement = element.GetProperty("source")

                                    match
                                        QuintSourceInternal.requireFields
                                            (path + "/source")
                                            (Set.ofList [ "fenceOrdinal"; "range" ])
                                            sourceElement,
                                        QuintSourceInternal.readRange
                                            (path + "/generatedRange")
                                            (element.GetProperty("generatedRange")),
                                        QuintSourceInternal.readRange
                                            (path + "/source/range")
                                            (sourceElement.GetProperty("range"))
                                    with
                                    | Ok(), Ok generatedRange, Ok sourceRange ->
                                        Some
                                            { Target = QuintSourceInternal.readString "target" element
                                              GeneratedRange = generatedRange
                                              Source =
                                                { FenceOrdinal = sourceElement.GetProperty("fenceOrdinal").GetInt32()
                                                  Range = sourceRange } }
                                    | results ->
                                        match results with
                                        | Error error, _, _
                                        | _, Error error, _
                                        | _, _, Error error -> errors <- error :: errors
                                        | _ -> ()

                                        None
                                with _ ->
                                    errors <-
                                        QuintSourceInternal.diagnostic
                                            "QUINT-SOURCE-MAP-VALUE-INVALID"
                                            path
                                            "Source-map entry contains a missing or wrongly typed value."
                                        :: errors

                                    None)
                        |> Seq.choose id
                        |> Seq.toList

                    match QuintSourceInternal.sortDiagnostics errors with
                    | [] ->
                        let sourceMap =
                            { Schema = schema
                              SourceSha256 = sourceSha
                              Entries = entries }

                        if encodeSourceMap sourceMap <> bytes then
                            Error
                                [ QuintSourceInternal.diagnostic
                                      "QUINT-SOURCE-MAP-NONCANONICAL"
                                      "/"
                                      "Source-map JSON must use the canonical v1 byte encoding." ]
                        else
                            Ok sourceMap
                    | diagnostics -> Error diagnostics
        with _ ->
            Error
                [ QuintSourceInternal.diagnostic
                      "QUINT-SOURCE-MAP-JSON-INVALID"
                      "/"
                      "Source-map bytes are not valid closed-schema JSON." ]

    let tryResolve target position sourceMap =
        sourceMap.Entries
        |> List.sortBy QuintSourceInternal.entrySortKey
        |> List.tryPick (fun entry ->
            if
                entry.Target = target
                && QuintSourceInternal.contains position entry.GeneratedRange
            then
                Some entry.Source
            else
                None)
