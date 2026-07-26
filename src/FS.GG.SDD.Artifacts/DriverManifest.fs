namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

module DriverManifest =
    type DriverManifestFile =
        { Path: string
          Sha256: string
          Executable: bool }

    type DriverManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          TreeSha256: string option
          Files: DriverManifestFile list
          SuppliedBy: string option
          MaterializesWhen: string }

    type DriverManifest =
        { SchemaVersion: int
          Skills: DriverManifestEntry list }

    let private isSha256 (value: string) =
        value.Length = 64
        && value
           |> Seq.forall (fun c -> (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))

    let private isSafeRelativePath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (path.StartsWith("/", StringComparison.Ordinal))
        && not (path.Contains('\\'))
        && not (Path.IsPathRooted path)
        && (path.Split('/')
            |> Array.forall (fun segment ->
                segment <> ""
                && segment <> "."
                && segment <> ".."
                && not (segment.Contains(':'))))

    let private canonicalFilesJson (files: DriverManifestFile list) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartArray()

        for file in files do
            writer.WriteStartObject()
            writer.WriteString("path", file.Path)
            writer.WriteString("sha256", file.Sha256)
            writer.WriteBoolean("executable", file.Executable)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.Flush()
        stream.ToArray()

    let private rawSha256 (bytes: byte array) =
        SHA256.HashData bytes |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

    let private requiredString name (element: JsonElement) =
        match
            jsonString name element
            |> Option.map (fun value -> value.Trim())
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
        with
        | Some value -> Ok value
        | None -> Error $"missing or blank '{name}'"

    let private parseV2Files id (element: JsonElement) =
        match tryJsonProperty "files" element with
        | None -> Error $"driver '{id}': missing 'files' array."
        | Some filesElement when filesElement.ValueKind <> JsonValueKind.Array ->
            Error $"driver '{id}': 'files' must be an array."
        | Some filesElement ->
            let rows = filesElement.EnumerateArray() |> Seq.toList

            let folder (result: Result<DriverManifestFile list, string>) row =
                result
                |> Result.bind (fun files ->
                    match requiredString "path" row, requiredString "sha256" row, tryJsonProperty "executable" row with
                    | Ok path, Ok sha256, Some executable when executable.ValueKind = JsonValueKind.True || executable.ValueKind = JsonValueKind.False ->
                        if not (isSafeRelativePath path) then
                            Error $"driver '{id}': unsafe file path '{path}'."
                        elif not (isSha256 sha256) then
                            Error $"driver '{id}/{path}': 'sha256' must be 64 lowercase hexadecimal characters."
                        elif files |> List.exists (fun file -> file.Path = path) then
                            Error $"driver '{id}': duplicate file path '{path}'."
                        else
                            Ok(
                                files
                                @ [ { Path = path
                                      Sha256 = sha256
                                      Executable = executable.GetBoolean() } ]
                            )
                    | Error message, _, _ -> Error $"driver '{id}': {message}."
                    | _, Error message, _ -> Error $"driver '{id}': {message}."
                    | _, _, _ -> Error $"driver '{id}': file 'executable' must be boolean.")

            (Ok [], rows) ||> List.fold folder
            |> Result.bind (fun files ->
                if List.isEmpty files then
                    Error $"driver '{id}': 'files' must not be empty."
                elif not (files |> List.exists (fun file -> file.Path = "SKILL.md")) then
                    Error $"driver '{id}': 'files' must contain SKILL.md."
                elif files <> (files |> List.sortBy (fun file -> file.Path)) then
                    Error $"driver '{id}': 'files' must be sorted by path."
                else
                    Ok files)

    let private parseEntry schemaVersion (element: JsonElement) =
        match requiredString "id" element, requiredString "sha256" element, requiredString "materializes-when" element with
        | Ok id, Ok sha256, Ok materializesWhen ->
            if schemaVersion >= 2 then
                if not (isSha256 sha256) then
                    Error $"driver '{id}': 'sha256' must be 64 lowercase hexadecimal characters."
                else
                    match requiredString "tree-sha256" element, parseV2Files id element with
                    | Ok treeSha256, Ok files when not (isSha256 treeSha256) ->
                        Error $"driver '{id}': 'tree-sha256' must be 64 lowercase hexadecimal characters."
                    | Ok treeSha256, Ok files ->
                        let computed = files |> canonicalFilesJson |> rawSha256

                        if computed <> treeSha256 then
                            Error $"driver '{id}': files manifest digest {computed} != tree-sha256 {treeSha256}."
                        else
                            Ok
                                { Id = id
                                  Scope = jsonString "scope" element |> Option.defaultValue "" |> fun value -> value.Trim()
                                  Sha256 = sha256
                                  TreeSha256 = Some treeSha256
                                  Files = files
                                  SuppliedBy =
                                    jsonString "supplied-by" element
                                    |> Option.map (fun value -> value.Trim())
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                  MaterializesWhen = materializesWhen }
                    | Error message, _ -> Error $"driver '{id}': {message}."
                    | _, Error message -> Error message
            else
                Ok
                    { Id = id
                      Scope = jsonString "scope" element |> Option.defaultValue "" |> fun value -> value.Trim()
                      Sha256 = sha256
                      TreeSha256 = None
                      Files =
                        [ { Path = "SKILL.md"
                            Sha256 = sha256
                            Executable = false } ]
                      SuppliedBy =
                        jsonString "supplied-by" element
                        |> Option.map (fun value -> value.Trim())
                        |> Option.filter (String.IsNullOrWhiteSpace >> not)
                      MaterializesWhen = materializesWhen }
        | Error message, _, _ -> Error message
        | _, Error message, _ -> Error message
        | _, _, Error message -> Error message

    let tryParse (text: string) : Result<DriverManifest, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | None -> Error "driver-skill-manifest.json: missing or non-integer 'schemaVersion'."
            | Some version when version < 1 || version > 2 ->
                Error $"driver-skill-manifest.json: unsupported schemaVersion {version}."
            | Some version ->
                match tryJsonProperty "skills" root with
                | None -> Error "driver-skill-manifest.json: missing 'skills' array."
                | Some skillsElement when skillsElement.ValueKind <> JsonValueKind.Array ->
                    Error "driver-skill-manifest.json: 'skills' must be an array."
                | Some skillsElement ->
                    let parsed =
                        (Ok [], skillsElement.EnumerateArray() |> Seq.toList)
                        ||> List.fold (fun result element ->
                            result
                            |> Result.bind (fun skills ->
                                parseEntry version element
                                |> Result.bind (fun entry ->
                                    if skills |> List.exists (fun skill -> skill.Id = entry.Id) then
                                        Error $"duplicate driver id '{entry.Id}'."
                                    else
                                        Ok(skills @ [ entry ]))))

                    parsed
                    |> Result.map (fun skills ->
                        { SchemaVersion = version
                          Skills = skills })
        with ex ->
            Error(sprintf "driver-skill-manifest.json: %s" ex.Message)

module DriverPredicate =
    // A single `has <glob>` / `always` / `false` atom. `<glob>` is an exact id or a trailing-`*`
    // prefix (spelled out — no interior glob matcher, matching the touch-set grammar's spirit).
    let private evaluateAtom (presentIds: Set<string>) (atom: string) : bool option =
        let atom = atom.Trim()

        if atom = "always" then
            Some true
        elif atom = "false" then
            Some false
        elif atom.StartsWith("has ", StringComparison.Ordinal) then
            let pattern = atom.Substring(4).Trim()

            if String.IsNullOrWhiteSpace pattern then
                None
            elif pattern.EndsWith("*", StringComparison.Ordinal) then
                let prefix = pattern.Substring(0, pattern.Length - 1)

                Some(
                    presentIds
                    |> Set.exists (fun id -> id.StartsWith(prefix, StringComparison.Ordinal))
                )
            else
                Some(presentIds.Contains pattern)
        else
            None

    let evaluate (predicate: string) (presentIds: Set<string>) : bool option =
        let predicate = predicate.Trim()
        let hasAnd = predicate.Contains(" and ")
        let hasOr = predicate.Contains(" or ")

        let combine (separator: string) (fold: bool option list -> bool option) =
            let results =
                predicate.Split([| separator |], StringSplitOptions.None)
                |> Array.toList
                |> List.map (evaluateAtom presentIds)

            if results |> List.exists Option.isNone then
                None
            else
                fold results

        if String.IsNullOrWhiteSpace predicate then
            None
        elif hasAnd && hasOr then
            // Mixed connectives — precedence is ambiguous without parentheses; fail closed
            // rather than guess (FR-004).
            None
        elif hasAnd then
            combine " and " (fun rs -> Some(rs |> List.forall (fun r -> r = Some true)))
        elif hasOr then
            combine " or " (fun rs -> Some(rs |> List.exists (fun r -> r = Some true)))
        else
            evaluateAtom presentIds predicate
