namespace FS.GG.SDD.Artifacts

open System
open YamlDotNet.RepresentationModel

// Field-list-driven codec — the Gap-A invariant (FS.GG.SDD#201, ADR-0002).
// Reuses the Internal YAML helpers (AutoOpen, same namespace/assembly):
// `parseYaml`, `tryMapping`, `tryScalarAt`, `tryScalarNonNullAt`, `scalarList`.
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

    let decode (fields: FieldCodec<'M> list) (seed: 'M) (text: string) : Result<'M, string> =
        match parseYaml text with
        | None -> Error "document is empty or not parseable YAML"
        | Some node ->
            match tryMapping node with
            | Some mapping -> parseMapping fields seed mapping
            | None -> Error "document root is not a YAML mapping"

    let foldInto (fields: FieldCodec<'M> list) (seed: 'M) (mapping: YamlMappingNode) : Result<'M, string> =
        parseMapping fields seed mapping
