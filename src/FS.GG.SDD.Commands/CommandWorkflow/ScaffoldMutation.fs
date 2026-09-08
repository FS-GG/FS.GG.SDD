namespace FS.GG.SDD.Commands.Internal

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

/// Pure ownership and file-mutation policy for scaffold. Process orchestration remains
/// in the handler; this service decides which paths may change and renders owned files.
module internal ScaffoldMutation =
    let contractMajor (version: string) =
        match version.Trim().Trim('"').Split('.') with
        | parts when parts.Length >= 1 ->
            match Int32.TryParse parts.[0] with
            | true, value -> Some value
            | _ -> None
        | _ -> None

    let isSddTree (path: string) =
        let path = normalizeRelativePath path

        let isMirroredRuntimeRoot =
            Fsgg.Schemas.agentSkillRoots
            |> List.filter (fun root -> root <> Fsgg.SkillMirror.providerSourceRoot)
            |> List.exists (fun root -> path.StartsWith(root + "/skills/", StringComparison.Ordinal))

        path.StartsWith(".fsgg/", StringComparison.Ordinal)
        || path.StartsWith("work/", StringComparison.Ordinal)
        || path.StartsWith("readiness/", StringComparison.Ordinal)
        || isMirroredRuntimeRoot
        || path.StartsWith(Fsgg.SkillMirror.providerSourceRoot + "/skills/fs-gg-sdd-", StringComparison.Ordinal)

    let isSddOwned path =
        let path = normalizeRelativePath path
        isSddTree path || path = "AGENTS.md" || path = "CLAUDE.md"

    let parseListing (text: string) =
        text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Set.ofArray

    let collisionPaths (listing: string) =
        parseListing listing
        |> Set.filter (isSddOwned >> not)
        |> Set.toList
        |> List.sort

    let skeletonFiles (effects: CommandEffect list) =
        effects
        |> List.choose (function
            | WriteFile(path, _, _) -> Some(normalizeRelativePath path)
            | _ -> None)
        |> Set.ofList

    let toolManifestPath = ".config/dotnet-tools.json"

    [<Literal>]
    let coordinationToolVersion = "0.87.0"

    let private writeToolEntry (writer: Utf8JsonWriter) ((packageId: string), (version: string), (command: string)) =
        writer.WritePropertyName(packageId)
        writer.WriteStartObject()
        writer.WriteString("version", version)
        writer.WriteStartArray("commands")
        writer.WriteStringValue(command)
        writer.WriteEndArray()
        writer.WriteEndObject()

    let private expectedToolEntries sddVersion =
        [ "fs.gg.coord.cli", coordinationToolVersion, "fsgg-coord-engine"
          "fs.gg.sdd.cli", sddVersion, "fsgg-sdd" ]

    let private ownedEntryConflict (tools: JsonElement) ((packageId: string), (version: string), (command: string)) =
        match tools.TryGetProperty packageId with
        | false, _ -> None
        | true, entry when entry.ValueKind <> JsonValueKind.Object ->
            Some $"owned tool entry '{packageId}' must be an object"
        | true, entry ->
            let versionMatches =
                match entry.TryGetProperty "version" with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() = version
                | _ -> false

            let commandsMatch =
                match entry.TryGetProperty "commands" with
                | true, value when value.ValueKind = JsonValueKind.Array ->
                    let commands = value.EnumerateArray() |> Seq.toList

                    commands.Length = 1
                    && commands.Head.ValueKind = JsonValueKind.String
                    && commands.Head.GetString() = command
                | _ -> false

            if versionMatches && commandsMatch then
                None
            else
                Some $"owned tool entry '{packageId}' conflicts with required {version} / {command}"

    /// Add only SDD-owned tool entries to a valid co-tenant manifest. Existing owned entries must
    /// already be exact; an old or differently-shaped one is an explicit conflict, never authority
    /// to overwrite somebody else's selection. Unrelated entries and root metadata are replayed
    /// through System.Text.Json unchanged in meaning.
    let mergeToolManifestText (sddVersion: string) (existingText: string) =
        try
            use document = JsonDocument.Parse existingText
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error "tool manifest root must be an object"
            else
                match root.TryGetProperty "version", root.TryGetProperty "isRoot", root.TryGetProperty "tools" with
                | (true, manifestVersion), (true, isRoot), (true, tools) when
                    manifestVersion.ValueKind = JsonValueKind.Number
                    && manifestVersion.TryGetInt32() = (true, 1)
                    && (isRoot.ValueKind = JsonValueKind.True || isRoot.ValueKind = JsonValueKind.False)
                    && tools.ValueKind = JsonValueKind.Object
                    ->
                    let expected = expectedToolEntries sddVersion
                    let conflicts = expected |> List.choose (ownedEntryConflict tools)

                    if not (List.isEmpty conflicts) then
                        Error(String.concat "; " conflicts)
                    else
                        let missing =
                            expected
                            |> List.filter (fun (packageId, _, _) ->
                                match tools.TryGetProperty packageId with
                                | true, _ -> false
                                | false, _ -> true)

                        if List.isEmpty missing then
                            Ok None
                        else
                            use stream = new MemoryStream()
                            use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
                            writer.WriteStartObject()

                            for property in root.EnumerateObject() do
                                if property.Name = "tools" then
                                    writer.WritePropertyName("tools")
                                    writer.WriteStartObject()
                                    tools.EnumerateObject() |> Seq.iter (fun tool -> tool.WriteTo writer)
                                    missing |> List.iter (writeToolEntry writer)
                                    writer.WriteEndObject()
                                else
                                    property.WriteTo writer

                            writer.WriteEndObject()
                            writer.Flush()
                            Ok(Some(Encoding.UTF8.GetString(stream.ToArray()) + "\n"))
                | _ -> Error "tool manifest requires version: 1, boolean isRoot, and an object-valued tools property"
        with :? JsonException as error ->
            Error $"tool manifest is not valid JSON: {error.Message}"

    let toolManifestText (version: string) =
        let quotedVersion = JsonSerializer.Serialize(version)
        let quotedCoordVersion = JsonSerializer.Serialize(coordinationToolVersion)

        String.Join(
            "\n",
            [ "{"
              "  \"version\": 1,"
              "  \"isRoot\": true,"
              "  \"tools\": {"
              "    \"fs.gg.coord.cli\": {"
              $"      \"version\": {quotedCoordVersion},"
              "      \"commands\": ["
              "        \"fsgg-coord-engine\""
              "      ]"
              "    },"
              "    \"fs.gg.sdd.cli\": {"
              $"      \"version\": {quotedVersion},"
              "      \"commands\": ["
              "        \"fsgg-sdd\""
              "      ]"
              "    }"
              "  }"
              "}"
              "" ]
        )
