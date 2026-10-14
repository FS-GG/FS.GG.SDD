namespace FS.GG.SDD.Commands

open System
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Security.Cryptography
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef

module OwnerGuidance =
    type Declaration =
        {
            Id: string
            SourcePath: string
            Targets: string list
            RequiredPaths: string list
        }

    type Write =
        {
            Path: string
            Text: string
            ExpectedBytes: byte array option
        }

    type Plan =
        {
            Writes: Write list
            Problems: string list
            DescriptorDigest: string option
            FragmentDigests: (string * string) list
            MissingTargets: string list
        }

    let descriptorPath = ".fsgg/owner-guidance.json"
    let utf8 = UTF8Encoding(false, true)

    let digest (bytes: byte array) =
        SHA256.HashData bytes |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

    let isContainedPath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && path.Length <= 1024
        && path = path.Trim()
        && not (path.Contains('\\'))
        && not (System.IO.Path.IsPathRooted path)
        && (path.Split('/')
            |> Array.forall (fun s ->
                s <> ""
                && s = s.Trim()
                && s <> "."
                && s <> ".."
                && not (s.Contains(':'))
                && not (s |> Seq.exists Char.IsControl)))

    let validId (id: string) =
        Regex.IsMatch(id, "^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)

    let members (expected: Set<string>) (element: JsonElement) =
        let actual = element.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Seq.toList

        if actual.Length <> expected.Count || Set.ofList actual <> expected then
            failwith "unknown, duplicate or missing descriptor member"

    let strings maxCount (element: JsonElement) =
        let values =
            element.EnumerateArray()
            |> Seq.map (fun p -> p.GetString() |> Option.ofObj |> Option.defaultValue "")
            |> Seq.toList

        if
            values.Length > maxCount
            || List.isEmpty values
            || values.Length <> (Set.ofList values).Count
            || (values |> List.exists String.IsNullOrWhiteSpace)
        then
            failwith "lists must be bounded, non-empty and unique"

        values |> List.sort

    let parse (text: string) =
        try
            if utf8.GetByteCount text > 65536 then
                failwith "descriptor exceeds 64 KiB"

            use doc = JsonDocument.Parse text
            let root = doc.RootElement
            members (Set.ofList [ "schema"; "guidance" ]) root

            if root.GetProperty("schema").GetString() <> "fsgg.owner-guidance/1" then
                failwith "unsupported schema"

            let rows = root.GetProperty("guidance").EnumerateArray() |> Seq.toList

            if rows.Length > 16 || List.isEmpty rows then
                failwith "guidance must contain 1–16 declarations"

            let declarations =
                rows
                |> List.map (fun row ->
                    members (Set.ofList [ "id"; "sourcePath"; "targets"; "requiredPaths" ]) row
                    let id = row.GetProperty("id").GetString() |> Option.ofObj |> Option.defaultValue ""

                    let path =
                        row.GetProperty("sourcePath").GetString()
                        |> Option.ofObj
                        |> Option.defaultValue ""

                    let targets = strings 16 (row.GetProperty("targets"))
                    let required = strings 32 (row.GetProperty("requiredPaths"))

                    if
                        not (validId id)
                        || not (isContainedPath path)
                        || (required |> List.exists (isContainedPath >> not))
                    then
                        failwith "unsafe identity or path"

                    {
                        Id = id
                        SourcePath = path
                        Targets = targets
                        RequiredPaths = required
                    })

            if
                declarations.Length
                <> (declarations |> List.map (fun d -> d.Id) |> Set.ofList).Count
            then
                failwith "duplicate guidance id"

            Ok(declarations |> List.sortBy (fun d -> d.Id))
        with ex ->
            Error $"owner-guidance: {ex.Message}"

    let merge (id: string) (fragment: string) (text: string) =
        if not (validId id) then
            Error "unsafe guidance id"
        elif
            fragment.Contains("<!--", StringComparison.Ordinal)
            && Regex.IsMatch(fragment, "<!--\\s*[a-z][a-z0-9-]*:(begin|end)\\s*-->")
        then
            Error "fragment contains guidance markers"
        else
            let first = $"<!-- {id}:begin -->"
            let last = $"<!-- {id}:end -->"

            let occurrences token =
                Regex.Matches(text, Regex.Escape token) |> Seq.cast<Match> |> Seq.toList

            let starts, ends = occurrences first, occurrences last

            let newline =
                if text.Contains("\r\n", StringComparison.Ordinal) then
                    "\r\n"
                else
                    "\n"

            let body = fragment.Replace("\r\n", "\n").TrimEnd('\r', '\n').Replace("\n", newline)
            let block = first + newline + body + newline + last

            let markers =
                Regex.Matches(text, "<!--\\s*([a-z][a-z0-9-]*):(begin|end)\\s*-->")
                |> Seq.cast<Match>
                |> Seq.toList

            let mutable active = None
            let mutable malformed = false

            for marker in markers do
                let markerId = marker.Groups.[1].Value

                match marker.Groups.[2].Value, active with
                | "begin", None -> active <- Some markerId
                | "end", Some current when current = markerId -> active <- None
                | _ -> malformed <- true

                if markerId = id && marker.Value <> first && marker.Value <> last then
                    malformed <- true

            if Option.isSome active then
                malformed <- true

            match starts, ends with
            | _ when malformed -> Error "nested, partial or noncanonical guidance markers"
            | [], [] ->
                // Marker-like partial edits must be repaired by the author before an append.
                if text.Contains($"<!-- {id}:", StringComparison.Ordinal) then
                    Error "partial guidance marker"
                else
                    Ok(
                        text
                        + (if text = "" || text.EndsWith("\n", StringComparison.Ordinal) then
                               ""
                           else
                               newline)
                        + block
                        + newline
                    )
            | [ a ], [ b ] when a.Index < b.Index ->
                let region = text.Substring(a.Index + a.Length, b.Index - a.Index - a.Length)

                let otherMarkers =
                    Regex.IsMatch(region, "<!--\\s*[a-z][a-z0-9-]*:(begin|end)\\s*-->")

                let remainder = text.Remove(a.Index, b.Index + b.Length - a.Index)

                if otherMarkers || remainder.Contains($"<!-- {id}:", StringComparison.Ordinal) then
                    Error "nested or partial guidance markers"
                else
                    Ok(text.Substring(0, a.Index) + block + text.Substring(b.Index + b.Length))
            | _ -> Error "duplicate, unpaired or reversed guidance markers"

    let declarations (files: Map<string, byte array>) =
        match Map.tryFind descriptorPath files with
        | None -> Ok []
        | Some bytes ->
            try
                parse (utf8.GetString bytes)
            with ex ->
                Error $"owner-guidance: {ex.Message}"

    let targetMap (agents: AgentGuidanceConfig) =
        let pairs = agents.Targets |> List.map (fun t -> t.Id, t.GuidancePath)

        if pairs.Length <> (pairs |> List.map fst |> Set.ofList).Count then
            Error "duplicate configured agent id"
        elif
            pairs
            |> List.exists (fun (_, p) ->
                not (isContainedPath p)
                || p.Contains('/')
                || not (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        then
            Error "agent targets must be contained root Markdown files"
        elif
            pairs.Length
            <> (pairs
                |> List.map (fun (_, path: string) -> path.ToUpperInvariant())
                |> Set.ofList)
                .Count
        then
            Error "configured target collision"
        else
            Ok(Map.ofList pairs)

    let requiredReads (verifiedFiles: Map<string, byte array>) (agents: AgentGuidanceConfig) =
        let files = verifiedFiles

        match declarations files with
        | Error e -> Error e
        | Ok [] -> Ok []
        | Ok rows ->
            match targetMap agents with
            | Error e -> Error e
            | Ok targets ->
                let missingTarget =
                    rows
                    |> List.collect (fun d -> d.Targets)
                    |> List.tryFind (fun t -> not (Map.containsKey t targets))

                let ownerPaths =
                    descriptorPath
                    :: (rows |> List.collect (fun d -> d.SourcePath :: d.RequiredPaths))
                    |> List.distinct

                match missingTarget, ownerPaths |> List.tryFind (fun p -> not (Map.containsKey p files)) with
                | Some t, _ -> Error $"undeclared configured agent: {t}"
                | _, Some p -> Error $"prerequisite is outside the verified selected owner payload: {p}"
                | None, None ->
                    Ok(
                        ownerPaths
                        @ (rows
                           |> List.collect (fun d -> d.Targets |> List.map (fun t -> Map.find t targets)))
                        |> List.distinct
                        |> List.sort
                    )

    let plan
        (verifiedFiles: Map<string, byte array>)
        (agents: AgentGuidanceConfig)
        (observed: Map<string, Result<FileSnapshot option, string>>)
        =
        let files = verifiedFiles

        let initial =
            {
                Writes = []
                Problems = []
                DescriptorDigest = Map.tryFind descriptorPath files |> Option.map digest
                FragmentDigests = []
                MissingTargets = []
            }

        match requiredReads files agents, declarations files with
        | Error e, _
        | _, Error e -> { initial with Problems = [ e ] }
        | Ok [], _ -> initial
        | Ok reads, Ok rows ->
            let mutable problems = []

            let lookup path =
                match Map.tryFind path observed with
                | Some(Ok value) -> value
                | Some(Error e) ->
                    problems <- problems @ [ $"{path}: {e}" ]
                    None
                | None ->
                    problems <- problems @ [ $"{path}: not observed" ]
                    None

            let snapshots = reads |> List.map (fun p -> p, lookup p) |> Map.ofList

            let required =
                descriptorPath
                :: (rows |> List.collect (fun d -> d.SourcePath :: d.RequiredPaths))
                |> List.distinct

            for path in required do
                match Map.find path snapshots with
                | Some snap when snap.RawBytes = Some(Map.find path files) -> ()
                | _ -> problems <- problems @ [ $"{path}: installed owner bytes missing or differing; preserved" ]

            let targets = targetMap agents |> Result.defaultValue Map.empty
            let mutable outputs = Map.empty
            let mutable fragments = []
            let mutable missingTargets = []

            for row in rows do
                try
                    let bytes = Map.find row.SourcePath files

                    if bytes.Length > 32768 then
                        failwith "fragment exceeds 32 KiB"

                    let fragment = utf8.GetString bytes

                    if fragment.StartsWith("\uFEFF", StringComparison.Ordinal) then
                        failwith "fragment must be UTF-8 without BOM"

                    fragments <- fragments @ [ row.Id, digest bytes ]

                    for target in row.Targets do
                        let path = Map.find target targets
                        let snap = Map.find path snapshots

                        match snap with
                        | None ->
                            match Map.tryFind path observed with
                            | Some(Ok None) ->
                                missingTargets <- path :: missingTargets
                                problems <- problems @ [ $"{path}: root guidance is missing; seed it first" ]
                            | _ -> () // The lookup already named the unreadable/unobserved subject.

                        | Some existing ->
                            let expectedUtf8 = utf8.GetBytes existing.Text

                            let isUtf8 =
                                existing.RawBytes
                                |> Option.exists (fun bytes ->
                                    bytes = expectedUtf8
                                    || bytes = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] expectedUtf8)

                            if not isUtf8 then
                                problems <- problems @ [ $"{path}: root guidance must be exact UTF-8 text" ]

                            let text = Map.tryFind path outputs |> Option.defaultValue existing.Text

                            match merge row.Id fragment text with
                            | Ok merged -> outputs <- Map.add path merged outputs
                            | Error e -> problems <- problems @ [ $"{path}: {e}" ]
                with ex ->
                    problems <- problems @ [ $"{row.SourcePath}: {ex.Message}" ]

            let writes =
                outputs
                |> Map.toList
                |> List.choose (fun (path, text) ->
                    match Map.find path snapshots with
                    | Some snap when snap.Text <> text ->
                        Some
                            {
                                Path = path
                                Text = text
                                ExpectedBytes = snap.RawBytes
                            }
                    | _ -> None)

            { initial with
                Writes = (if List.isEmpty problems then writes else [])
                Problems = (problems |> List.distinct)
                FragmentDigests = fragments
                MissingTargets = missingTargets |> List.distinct |> List.sort
            }
