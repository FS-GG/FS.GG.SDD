namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module internal Internal =
    let normalizePath (path: string | null) =
        (Option.ofObj path |> Option.defaultValue "").Trim().Replace('\\', '/').TrimStart('/')

    let artifact path kind owner requiredBySdd =
        match FS.GG.SDD.Artifacts.ArtifactRef.create (normalizePath path) kind owner requiredBySdd with
        | Ok value -> value
        | Error message -> invalidArg (nameof path) message

    let sourceArtifact path kind = artifact path kind Sdd true

    let parseYaml (text: string | null) =
        let stream = YamlStream()
        use reader = new StringReader(Option.ofObj text |> Option.defaultValue "")

        // A malformed authored document (tab indentation, a duplicate key, bad
        // syntax) makes YamlDotNet throw YamlException. Every Result-returning
        // lifecycle parser treats None as an absent/unparseable document and
        // surfaces a diagnostic (exit 1), which honors the malformed-input ->
        // diagnostic doctrine instead of crashing through the parser.
        try
            stream.Load reader

            if stream.Documents.Count = 0 then
                None
            else
                Some stream.Documents.[0].RootNode
        with :? YamlDotNet.Core.YamlException ->
            None

    let tryMapping (node: YamlNode) =
        match node with
        | :? YamlMappingNode as mapping -> Some mapping
        | _ -> None

    let trySequence (node: YamlNode) =
        match node with
        | :? YamlSequenceNode as sequence -> Some sequence
        | _ -> None

    let tryScalar (node: YamlNode) =
        match node with
        | :? YamlScalarNode as scalar -> Some(Option.ofObj scalar.Value |> Option.defaultValue "")
        | _ -> None

    let tryChild key (mapping: YamlMappingNode) =
        mapping.Children
        |> Seq.tryPick (fun pair ->
            match pair.Key with
            | :? YamlScalarNode as scalar when scalar.Value = key -> Some pair.Value
            | _ -> None)

    let rec tryScalarAt keys node =
        match keys with
        | [] -> tryScalar node
        | key :: rest ->
            node
            |> tryMapping
            |> Option.bind (tryChild key)
            |> Option.bind (tryScalarAt rest)

    let tryNodeAt keys node =
        let rec loop remaining current =
            match remaining with
            | [] -> Some current
            | key :: rest ->
                current
                |> tryMapping
                |> Option.bind (tryChild key)
                |> Option.bind (loop rest)

        loop keys node

    let trySequenceAt keys node =
        tryNodeAt keys node |> Option.bind trySequence

    let scalarListFromNode node =
        node
        |> trySequence
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.choose tryScalar
            |> Seq.map (fun value -> value.Trim())
            |> Seq.filter (String.IsNullOrWhiteSpace >> not)
            |> Seq.toList)
        |> Option.defaultValue []

    let scalarList keys node =
        tryNodeAt keys node |> Option.map scalarListFromNode |> Option.defaultValue []

    let boolAt keys node defaultValue =
        match tryScalarAt keys node with
        | Some value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> true
        | Some value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> false
        | _ -> defaultValue

    let schemaVersion (artifact: ArtifactRef) (root: YamlNode) =
        let raw = tryScalarAt [ "schemaVersion" ] root
        let compatibility = SchemaVersion.classifyRaw raw

        match compatibility.Status with
        | SchemaCompatibilityStatus.Current
        | SchemaCompatibilityStatus.Deprecated -> compatibility.Version, []
        | SchemaCompatibilityStatus.Malformed ->
            let message =
                if String.IsNullOrWhiteSpace compatibility.RawValue then
                    $"Artifact '{artifact.Path}' is missing schemaVersion."
                else
                    defaultArg compatibility.MigrationHint "Schema version is malformed."

            None, [ Diagnostics.malformedSchemaVersion artifact message ]
        | SchemaCompatibilityStatus.Unsupported ->
            compatibility.Version, [ Diagnostics.unsupportedSchemaVersion artifact compatibility.RawValue ]
        | SchemaCompatibilityStatus.Future ->
            compatibility.Version, [ Diagnostics.futureSchemaVersion artifact compatibility.RawValue ]

    let requiredScalar artifact label keys root =
        match tryScalarAt keys root with
        | Some value when not (String.IsNullOrWhiteSpace value) -> Ok value
        | _ ->
            let dottedPath = String.concat "." keys
            Error
                [ Diagnostics.workModelInconsistent
                      artifact
                      $"Required field '{label}' is missing."
                      $"Add '{dottedPath}' to '{artifact.Path}'."
                      [ label ] ]

    let combine errors =
        errors |> List.collect id

    let proseStatus (text: string) =
        Regex.Match(text, @"(?im)^Prose status:\s*(\S+)\s*$")
        |> fun m -> if m.Success then Some m.Groups.[1].Value else None

    let sourceLocation line = Some { Line = Some line; Column = Some 1 }

    let hasHeading (heading: string) (text: string) =
        Regex.IsMatch(text, $"(?m)^##\\s+{Regex.Escape heading}\\s*$")

    let boolScalarAt keys root =
        match tryScalarAt keys root with
        | Some value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> Some true
        | Some value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> Some false
        | _ -> None

    let sectionLines (heading: string) (text: string) =
        let normalized = text.Replace("\r\n", "\n")
        let lines = normalized.Split('\n')

        let start =
            lines
            |> Array.tryFindIndex (fun line -> Regex.IsMatch(line, $"^##\\s+{Regex.Escape heading}\\s*$"))

        match start with
        | None -> []
        | Some index ->
            lines.[index + 1 ..]
            |> Array.takeWhile (fun line -> not (Regex.IsMatch(line, "^##\\s+")))
            |> Array.mapi (fun offset line -> index + offset + 2, line)
            |> Array.toList

    let scopedIdLocations (pattern: string) (createId: string -> Result<'id, string>) (lines: (int * string) list) =
        lines
        |> List.toArray
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.collect (fun (_, (lineNumber, line)) ->
            Regex.Matches(line, pattern, RegexOptions.IgnoreCase)
            |> Seq.cast<Match>
            |> Seq.choose (fun m ->
                match createId m.Value with
                | Ok id -> Some(id, sourceLocation lineNumber)
                | Error _ -> None)
            |> Seq.toArray)
        |> Array.toList

    let scopedIdLocationsInSections headings pattern createId text =
        headings
        |> List.collect (fun heading -> sectionLines heading text)
        |> scopedIdLocations pattern createId

    let duplicateScopedDiagnostics artifact (idValue: 'id -> string) (values: ('id * SourceLocation option) list) =
        values
        |> List.groupBy (fst >> idValue)
        |> List.choose (fun (id, group) ->
            if List.length group > 1 then
                Some(Diagnostics.duplicateIdentifier artifact id (group |> List.choose snd))
            else
                None)

    let idsInLine pattern createId line =
        Regex.Matches(line, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> createId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> string id)
        |> Seq.toList

    let cleanAfterId (idValue: string) (line: string) =
        let index = line.IndexOf(idValue, StringComparison.OrdinalIgnoreCase)

        if index < 0 then
            line.Trim().TrimStart('-', '*').Trim()
        else
            line.Substring(index + idValue.Length).Trim().TrimStart(':', '-', ' ').Trim()

    // A "no-outstanding" sentinel line: after stripping an optional leading bullet marker,
    // the trimmed text is empty or matches the disclaimer convention used by
    // `parseNonEmptySectionLines` (case-insensitive `No `) plus an explicit "none" phrasing
    // (`None`, `None outstanding`). Used to exempt such lines from the "every bullet needs a
    // stable id" rule under `## Ambiguities` (§3.3).
    let isNoOutstandingSentinel (line: string) =
        let trimmed = line.Trim().TrimStart('-', '*').Trim()

        String.IsNullOrWhiteSpace trimmed
        || trimmed.StartsWith("No ", StringComparison.OrdinalIgnoreCase)
        || trimmed.StartsWith("None", StringComparison.OrdinalIgnoreCase)

    let parseNonEmptySectionLines heading text =
        sectionLines heading text
        |> List.choose (fun (_, line) ->
            let trimmed = line.Trim().TrimStart('-', '*').Trim()

            if String.IsNullOrWhiteSpace trimmed
               || trimmed.StartsWith("No ", StringComparison.OrdinalIgnoreCase) then
                None
            else
                Some trimmed)

    let parseTaskIds values =
        values |> List.choose (Identifiers.createTaskId >> Result.toOption)

    let parseRequirementIds values =
        values |> List.choose (Identifiers.createRequirementId >> Result.toOption)

    let parseDecisionIds values =
        values |> List.choose (Identifiers.createDecisionId >> Result.toOption)

    let parseEvidenceIds values =
        values |> List.choose (Identifiers.createEvidenceId >> Result.toOption)

    let tryJsonProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let jsonString name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Option.ofObj (value.GetString())
            elif value.ValueKind = JsonValueKind.Null then
                None
            else
                Some(value.ToString()))

    let jsonRequiredString name element =
        jsonString name element |> Option.defaultValue ""

    let jsonInt name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Number then
                match value.TryGetInt32() with
                | true, parsed -> Some parsed
                | _ -> None
            elif value.ValueKind = JsonValueKind.String then
                match Int32.TryParse(Option.ofObj (value.GetString()) |> Option.defaultValue "") with
                | true, parsed -> Some parsed
                | _ -> None
            else
                None)

    let jsonBool name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.True then Some true
            elif value.ValueKind = JsonValueKind.False then Some false
            else None)

    let jsonArray name element =
        tryJsonProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Array)
        |> Option.map (fun value -> value.EnumerateArray() |> Seq.toList)
        |> Option.defaultValue []

    let jsonStringList name element =
        jsonArray name element
        |> List.choose (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Option.ofObj (value.GetString())
            else
                None)
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> List.sort

    let parseJsonDigest (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.String ->
            let value = Option.ofObj (element.GetString()) |> Option.defaultValue ""
            if String.IsNullOrWhiteSpace value then
                None
            else
                let parts = value.Split([| ':' |], 2)
                if parts.Length = 2 then
                    SchemaVersion.createSourceDigest parts.[0] parts.[1] |> Result.toOption
                else
                    SchemaVersion.createSourceDigest "sha256" value |> Result.toOption
        | JsonValueKind.Object ->
            match jsonString "algorithm" element, jsonString "value" element with
            | Some algorithm, Some value -> SchemaVersion.createSourceDigest algorithm value |> Result.toOption
            | _ -> None
        | _ -> None

    let jsonDigest name element =
        tryJsonProperty name element |> Option.bind parseJsonDigest

    let diagnosticSeverityFromJson (value: string | null) =
        match (Option.ofObj value |> Option.defaultValue "").Trim().ToLowerInvariant() with
        | "error"
        | "blocking" -> Diagnostics.DiagnosticError
        | "warning"
        | "stalesource"
        | "missingdisposition"
        | "malformedsource"
        | "generatedview" -> Diagnostics.DiagnosticWarning
        | _ -> Diagnostics.DiagnosticInfo

    let artifactFromJsonPath path =
        if String.IsNullOrWhiteSpace path then
            None
        else
            ArtifactRef.create (normalizePath path) (ArtifactKind.Other "analysis") ArtifactOwner.Sdd true
            |> Result.toOption

    let parseAcceptanceScenarioIds values =
        values |> List.choose (Identifiers.createAcceptanceScenarioId >> Result.toOption)

    let parseChecklistResultIds values =
        values |> List.choose (Identifiers.createChecklistResultId >> Result.toOption)

    let parsePlanDecisionIds values =
        values |> List.choose (Identifiers.createPlanDecisionId >> Result.toOption)

    // The shared parse → classify-schema → match → error-arm skeleton for the four
    // JSON-backed lifecycle view parsers. Takes the snapshot's `path`/`text` directly
    // rather than a `FileSnapshot` record because `FileSnapshot` is defined in
    // LifecycleArtifacts/Core.fs, which compiles after this module.
    let parseJsonView
        (label: string)
        (malformedJsonCorrection: string)
        (build: ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>)
        (path: string)
        (text: string)
        : Result<'view, Diagnostic list> =
        let artifact = sourceArtifact path ArtifactKind.GeneratedView

        try
            use document = JsonDocument.Parse text
            let root = document.RootElement
            let rawVersion = jsonInt "schemaVersion" root |> Option.map string
            let compatibility = SchemaVersion.classifyRaw rawVersion

            match compatibility.Version, compatibility.Status with
            | Some schema, SchemaCompatibilityStatus.Current
            | Some schema, SchemaCompatibilityStatus.Deprecated -> build artifact schema root
            | _, SchemaCompatibilityStatus.Malformed
            | None, SchemaCompatibilityStatus.Current
            | None, SchemaCompatibilityStatus.Deprecated ->
                Error [ Diagnostics.malformedSchemaVersion artifact $"{label} is missing or has malformed schemaVersion." ]
            | _, SchemaCompatibilityStatus.Unsupported ->
                Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
            | _, SchemaCompatibilityStatus.Future ->
                Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
        with ex ->
            Error
                [ Diagnostics.workModelInconsistent
                      artifact
                      $"{label} JSON is malformed: {ex.Message}"
                      malformedJsonCorrection
                      [ path ] ]
