namespace FS.GG.SDD.Artifacts

open System
open YamlDotNet.RepresentationModel

// Field-list-driven codec — the Gap-A invariant (FS.GG.SDD#201, ADR-0002).
// Reuses the Internal YAML helpers (AutoOpen, same namespace/assembly):
// `parseYamlDocument`, `tryMapping`, `tryScalarAt`, `tryScalarNonNullAt`, `scalarList`.
[<RequireQualifiedAccess>]
module ArtifactCodec =

    [<NoEquality; NoComparison>]
    type FieldCodec<'M> =
        { Key: string
          Read: YamlMappingNode -> 'M -> Result<'M, string>
          Write: 'M -> string option }

    // --- minimal, round-trip-safe YAML scalar quoting ---
    // A plain scalar is emitted bare only when it cannot be misread on the way
    // back: it must not be a null token (else a bare `null` would read as absence
    // — the #180 corruption), must start with an alphanumeric (no leading YAML
    // indicator such as `-`), must contain only inoffensive characters, and must
    // have no trailing space (which YAML would strip). Everything else is
    // double-quoted with `\` and `"` escaped, which round-trips exactly.
    let private nullTokens = set [ ""; "~"; "null"; "Null"; "NULL" ]

    let private isSafePlain (v: string) =
        v.Length > 0
        && not (nullTokens.Contains v)
        && Char.IsLetterOrDigit v.[0]
        && v.[v.Length - 1] <> ' '
        && v
           |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = ' ' || c = '.' || c = '_' || c = '/' || c = '-')

    let private yamlScalar (v: string) =
        if isSafePlain v then
            v
        else
            "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    // --- field constructors ---
    let optionalScalar (key: string) (get: 'M -> string option) (set: string option -> 'M -> 'M) : FieldCodec<'M> =
        { Key = key
          // null-aware: a bare `null`/`~`/empty plain scalar -> None; a quoted
          // "null" keeps its style and reads as Some "null".
          Read = fun mapping model -> Ok(set (tryScalarNonNullAt [ key ] (mapping :> YamlNode)) model)
          Write = fun model -> get model |> Option.map (fun v -> $"{key}: {yamlScalar v}") }

    let requiredScalar (key: string) (get: 'M -> string) (set: string -> 'M -> 'M) : FieldCodec<'M> =
        { Key = key
          Read =
            fun mapping model ->
                match tryScalarAt [ key ] (mapping :> YamlNode) with
                | Some value -> Ok(set value model)
                | None -> Error $"required field '{key}' is missing"
          Write = fun model -> Some $"{key}: {yamlScalar (get model)}" }

    let defaultedScalar
        (key: string)
        (fallback: string)
        (get: 'M -> string)
        (set: string -> 'M -> 'M)
        : FieldCodec<'M> =
        { Key = key
          // Reads the key, or `fallback` when the key is absent — never errors. Mirrors the
          // `tryScalarAt |> Option.defaultValue` reader for keys like evidence `sourceRef.kind`.
          Read =
            fun mapping model ->
                match tryScalarAt [ key ] (mapping :> YamlNode) with
                | Some value -> Ok(set value model)
                | None -> Ok(set fallback model)
          Write = fun model -> Some $"{key}: {yamlScalar (get model)}" }

    let inlineList (key: string) (get: 'M -> string list) (set: string list -> 'M -> 'M) : FieldCodec<'M> =
        { Key = key
          Read = fun mapping model -> Ok(set (scalarList [ key ] (mapping :> YamlNode)) model)
          Write =
            fun model ->
                match get model with
                | [] -> None
                | items -> Some(sprintf "%s: [%s]" key (items |> List.map yamlScalar |> String.concat ", ")) }

    let private renderInlineAlways (key: string) (items: string list) =
        match items |> List.distinct |> List.sort with
        | [] -> sprintf "%s: []" key
        | items -> sprintf "%s: [%s]" key (items |> List.map yamlScalar |> String.concat ", ")

    let alwaysInlineList (key: string) (get: 'M -> string list) (set: string list -> 'M -> 'M) : FieldCodec<'M> =
        // Like `inlineList` but for a fixed-shape record where the key is always present: an empty
        // list renders `key: []` rather than omitting the line. Distinct+sorted on write (matching
        // the legacy `yamlInlineList`); reader preserves order.
        { Key = key
          Read = fun mapping model -> Ok(set (scalarList [ key ] (mapping :> YamlNode)) model)
          Write = fun model -> Some(renderInlineAlways key (get model)) }

    let refList
        (key: string)
        (create: string -> Result<'id, 'e>)
        (value: 'id -> string)
        (get: 'M -> 'id list)
        (set: 'id list -> 'M -> 'M)
        : FieldCodec<'M> =
        // A typed-id list. The read is lenient — parse each token, drop malformed, order preserved
        // (mirroring `parseTaskIds`/etc.); the malformed-ref DIAGNOSTICS stay the semantic layer's
        // job (computed via `malformedRefs`), so nothing is silently lost, the codec owns only the
        // value round-trip. Always present (`key: []` when empty), distinct+sorted on write.
        { Key = key
          Read =
            fun mapping model ->
                Ok(
                    set
                        (scalarList [ key ] (mapping :> YamlNode)
                         |> List.choose (create >> Result.toOption))
                        model
                )
          Write = fun model -> Some(renderInlineAlways key (get model |> List.map value)) }

    let mappedScalar
        (key: string)
        (toStr: 'a -> string)
        (ofStr: string -> 'a)
        (get: 'M -> 'a)
        (set: 'a -> 'M -> 'M)
        : FieldCodec<'M> =
        // A scalar mapped through a total pair: render via `toStr`, read via `ofStr` (which must be
        // total — it supplies a default for an unrecognised token, like the lenient `parseEvidenceKind`).
        // An absent key keeps the seed value. Always writes.
        { Key = key
          Read =
            fun mapping model ->
                match tryScalarAt [ key ] (mapping :> YamlNode) with
                | Some value -> Ok(set (ofStr value) model)
                | None -> Ok model
          Write = fun model -> Some $"{key}: {yamlScalar (toStr (get model))}" }

    let boolScalar (key: string) (fallback: bool) (get: 'M -> bool) (set: bool -> 'M -> 'M) : FieldCodec<'M> =
        // Mirrors `boolAt`: only an explicit case-insensitive `true`/`false` is honoured; anything
        // else (absent, null, junk) reads as `fallback`. Always writes `key: true|false`.
        { Key = key
          Read =
            fun mapping model ->
                match tryScalarAt [ key ] (mapping :> YamlNode) with
                | Some value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> Ok(set true model)
                | Some value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> Ok(set false model)
                | _ -> Ok(set fallback model)
          Write = fun model -> Some(sprintf "%s: %b" key (get model)) }

    let scalarBlock (key: string) (get: 'M -> string list) (set: string list -> 'M -> 'M) : FieldCodec<'M> =
        { Key = key
          Read = fun mapping model -> Ok(set (scalarList [ key ] (mapping :> YamlNode)) model)
          Write =
            fun model ->
                match get model with
                | [] -> None
                | items ->
                    let lines = items |> List.map (fun v -> $"  - {yamlScalar v}")
                    Some(key + ":\n" + String.concat "\n" lines) }

    // --- fold (decode) / map (render) over the one shared field list ---
    let keys (fields: FieldCodec<'M> list) =
        fields |> List.map (fun field -> field.Key)

    let private parseMapping (fields: FieldCodec<'M> list) (seed: 'M) (mapping: YamlMappingNode) : Result<'M, string> =
        (Ok seed, fields)
        ||> List.fold (fun acc field -> acc |> Result.bind (fun model -> field.Read mapping model))

    let render (fields: FieldCodec<'M> list) (model: 'M) : string =
        fields |> List.choose (fun field -> field.Write model) |> String.concat "\n"

    // --- relative-indentation helpers for nested/list combinators ---
    // A field's `Write` returns column-0 text; these shift a rendered sub-block down one level.
    // `indentLines` prepends `n` spaces to every line; `listItemLines` frames a block as one YAML
    // block-sequence item (first line on the `- ` marker, the rest aligned two spaces deeper), both
    // at a base of `n` spaces. Rendered blocks never contain blank lines, so no empty-line guard.
    let private indentLines (n: int) (text: string) =
        let pad = String(' ', n)
        text.Split('\n') |> Array.map (fun line -> pad + line) |> String.concat "\n"

    let private listItemLines (n: int) (text: string) =
        let pad = String(' ', n)
        let cont = String(' ', n + 2)

        text.Split('\n')
        |> Array.mapi (fun i line -> if i = 0 then $"{pad}- {line}" else $"{cont}{line}")
        |> String.concat "\n"

    let nested
        (key: string)
        (subFields: FieldCodec<'S> list)
        (subSeed: 'S)
        (get: 'M -> 'S)
        (set: 'S -> 'M -> 'M)
        : FieldCodec<'M> =
        // An always-present nested mapping: `key:` then the sub-record's fields indented two spaces.
        // An absent key keeps the seed sub-record.
        { Key = key
          Read =
            fun mapping model ->
                match tryNodeAt [ key ] (mapping :> YamlNode) |> Option.bind tryMapping with
                | Some sub -> parseMapping subFields subSeed sub |> Result.map (fun s -> set s model)
                | None -> Ok model
          Write = fun model -> Some(key + ":\n" + indentLines 2 (render subFields (get model))) }

    let optionalNestedVia
        (key: string)
        (subFields: FieldCodec<'D> list)
        (subSeed: 'D)
        (lift: 'D -> 'F option)
        (lower: 'F -> 'D)
        (get: 'M -> 'F option)
        (set: 'F option -> 'M -> 'M)
        : FieldCodec<'M> =
        // An optional nested mapping decoded through a draft: read the sub-mapping into `'D` (e.g. an
        // option-carrying draft that reads null-aware), then `lift` it to the model field `'F option`
        // — returning `None` rejects the draft (e.g. a blank synthetic disclosure, keeping the
        // undisclosed-synthetic gate honest, FS.GG.SDD#180). `lower` projects the field back to the
        // draft for rendering. Omits the key entirely when the field is `None`.
        { Key = key
          Read =
            fun mapping model ->
                match tryNodeAt [ key ] (mapping :> YamlNode) |> Option.bind tryMapping with
                | Some sub ->
                    parseMapping subFields subSeed sub
                    |> Result.map (fun draft -> set (lift draft) model)
                | None -> Ok(set None model)
          Write =
            fun model ->
                get model
                |> Option.map (fun field -> key + ":\n" + indentLines 2 (render subFields (lower field))) }

    let recordList
        (key: string)
        (subFields: FieldCodec<'S> list)
        (subSeed: 'S)
        (get: 'M -> 'S list)
        (set: 'S list -> 'M -> 'M)
        : FieldCodec<'M> =
        // A block sequence of sub-records, always present (`key: []` when empty). Each element
        // decodes via `foldInto subFields subSeed`; a malformed element is dropped (it never yields
        // a mapping), mirroring the legacy per-record parsers.
        { Key = key
          Read =
            fun mapping model ->
                match trySequenceAt [ key ] (mapping :> YamlNode) with
                | None -> Ok(set [] model)
                | Some sequence ->
                    let items =
                        sequence.Children
                        |> Seq.choose tryMapping
                        |> Seq.choose (fun sub -> parseMapping subFields subSeed sub |> Result.toOption)
                        |> Seq.toList

                    Ok(set items model)
          Write =
            fun model ->
                match get model with
                | [] -> Some(sprintf "%s: []" key)
                | items ->
                    let lines =
                        items
                        |> List.map (fun item -> listItemLines 2 (render subFields item))
                        |> String.concat "\n"

                    Some(key + ":\n" + lines) }

    let decode (fields: FieldCodec<'M> list) (seed: 'M) (text: string) : Result<'M, string> =
        match parseYamlDocument text with
        | YamlEmpty -> Error "document is empty"
        | YamlMalformed(message, line, column) -> Error $"YAML syntax error at line {line}, column {column}: {message}"
        | YamlRoot node ->
            match tryMapping node with
            | Some mapping -> parseMapping fields seed mapping
            | None -> Error "document root is not a YAML mapping"

    let foldInto (fields: FieldCodec<'M> list) (seed: 'M) (mapping: YamlMappingNode) : Result<'M, string> =
        parseMapping fields seed mapping
