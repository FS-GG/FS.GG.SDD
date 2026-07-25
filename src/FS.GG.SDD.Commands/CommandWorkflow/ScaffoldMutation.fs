namespace FS.GG.SDD.Commands.Internal

open System
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

        path.StartsWith(".fsgg/", StringComparison.Ordinal)
        || path.StartsWith("work/", StringComparison.Ordinal)
        || path.StartsWith("readiness/", StringComparison.Ordinal)
        || path.StartsWith(".claude/skills/", StringComparison.Ordinal)
        || path.StartsWith(".codex/skills/", StringComparison.Ordinal)
        || path.StartsWith(".agents/skills/fs-gg-sdd-", StringComparison.Ordinal)

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

    let toolManifestText (version: string) =
        let quotedVersion = JsonSerializer.Serialize(version)

        String.Join(
            "\n",
            [ "{"
              "  \"version\": 1,"
              "  \"isRoot\": true,"
              "  \"tools\": {"
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
