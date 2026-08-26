namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Text.Json
open System.Text.RegularExpressions

type QuintSourcePosition = { Line: int; Column: int }

type QuintSourceRange =
    { Path: string
      Start: QuintSourcePosition
      End: QuintSourcePosition }

type QuintCatalogueKind =
    | Requirement
    | StateVariable
    | Action
    | Invariant
    | TemporalProperty
    | Evidence
    | Implementation
    | ExternalSubject

type QuintCatalogueEntry =
    { Id: string
      Kind: QuintCatalogueKind
      Source: QuintSourceRange }

type QuintActionEffect =
    { ActionId: string
      Reads: string list
      Writes: string list
      Subjects: string list }

type QuintProfileCatalogue =
    { Profile: string
      QuintVersion: string
      Entries: QuintCatalogueEntry list
      ActionEffects: QuintActionEffect list }

type QuintProfileDiagnostic =
    { Code: string
      Path: string
      Message: string
      Correction: string
      Source: QuintSourceRange option }

module private ProfileCore =
    let profile = "fsgg-quint-profile/1"
    let version = "0.32.0"

    let diagnostic code path message correction source : QuintProfileDiagnostic =
        { Code = code
          Path = path
          Message = message
          Correction = correction
          Source = source }

    let sorted diagnostics =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let validId = Regex("^[A-Z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$", RegexOptions.CultureInvariant)

    let safeMarkdownPath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (IO.Path.IsPathRooted path)
        && path.EndsWith(".md", StringComparison.Ordinal)
        && not (path.Contains('\\'))
        && not (path.Split('/') |> Array.exists (fun segment -> segment = "" || segment = "." || segment = ".."))

    let kindText = function
        | Requirement -> "requirement"
        | StateVariable -> "stateVariable"
        | Action -> "action"
        | Invariant -> "invariant"
        | TemporalProperty -> "temporalProperty"
        | Evidence -> "evidence"
        | Implementation -> "implementation"
        | ExternalSubject -> "externalSubject"

    let parseKind = function
        | "requirement" -> Ok Requirement
        | "stateVariable" -> Ok StateVariable
        | "action" -> Ok Action
        | "invariant" -> Ok Invariant
        | "temporalProperty" -> Ok TemporalProperty
        | "evidence" -> Ok Evidence
        | "implementation" -> Ok Implementation
        | "externalSubject" -> Ok ExternalSubject
        | value -> Error value

    let validate catalogue =
        [ if catalogue.Profile <> profile then
              yield diagnostic "QUINT-PROFILE-IDENTITY" "/profile" $"Expected '%s{profile}', got '%s{catalogue.Profile}'." "Compile with the exact profile 1 manifest." None

          if catalogue.QuintVersion <> version then
              yield diagnostic "QUINT-PROFILE-VERSION" "/quintVersion" $"Expected Quint %s{version}, got '%s{catalogue.QuintVersion}'." "Use the content-addressed Quint 0.32.0 tool." None

          for index, entry in catalogue.Entries |> List.indexed do
              let path = $"/entries/%d{index}"
              if not (validId.IsMatch entry.Id) then
                  yield diagnostic "QUINT-PROFILE-ID" (path + "/id") $"'%s{entry.Id}' is not an explicit profile identity." "Use an uppercase-leading stable catalogue id." (Some entry.Source)
              if not (safeMarkdownPath entry.Source.Path) then
                  yield diagnostic "QUINT-PROFILE-SOURCE-PATH" (path + "/source/path") $"'%s{entry.Source.Path}' is not a safe relative Markdown path." "Use a canonical relative .md path without dot segments." (Some entry.Source)
              if entry.Source.Start.Line < 1 || entry.Source.Start.Column < 1 || entry.Source.End.Line < entry.Source.Start.Line || (entry.Source.End.Line = entry.Source.Start.Line && entry.Source.End.Column < entry.Source.Start.Column) then
                  yield diagnostic "QUINT-PROFILE-SOURCE-RANGE" (path + "/source") "The source range is not positive and ordered." "Bind the fact to an inclusive ordered literate source range." (Some entry.Source)

          for (kind, id), rows in catalogue.Entries |> List.groupBy (fun item -> item.Kind, item.Id) do
              if rows.Length > 1 then
                  yield diagnostic "QUINT-PROFILE-ID-DUPLICATE" "/entries" $"Catalogue identity '%s{kindText kind}:%s{id}' occurs more than once." "Declare each (kind,id) exactly once." (rows |> List.tryHead |> Option.map _.Source)

          let entryIds = catalogue.Entries |> List.map _.Id |> Set.ofList
          let actionIds = catalogue.Entries |> List.choose (fun row -> if row.Kind = Action then Some row.Id else None) |> Set.ofList
          for index, effect in catalogue.ActionEffects |> List.indexed do
              let path = $"/actionEffects/%d{index}"
              if not (actionIds.Contains effect.ActionId) then
                  yield diagnostic "QUINT-PROFILE-ACTION-REFERENCE" (path + "/actionId") $"Action '%s{effect.ActionId}' is not declared." "Reference one action catalogue row." None
              for field, ids in [ "reads", effect.Reads; "writes", effect.Writes; "subjects", effect.Subjects ] do
                  for id in ids do
                      if not (entryIds.Contains id) then
                          yield diagnostic "QUINT-PROFILE-REFERENCE" (path + "/" + field) $"Catalogue identity '%s{id}' is not declared." "Declare the referenced identity in the catalogue." None
                  if (ids |> List.distinct).Length <> ids.Length then
                      yield diagnostic "QUINT-PROFILE-REFERENCE-DUPLICATE" (path + "/" + field) "The semantic set contains a duplicate identity." "Remove duplicate identities." None

          for actionId, rows in catalogue.ActionEffects |> List.groupBy _.ActionId do
              if rows.Length > 1 then
                  yield diagnostic "QUINT-PROFILE-EFFECT-DUPLICATE" "/actionEffects" $"Action '%s{actionId}' has more than one effect row." "Emit exactly one effect row per action." None ]
        |> sorted

    let properties path allowed (element: JsonElement) =
        let names = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        [ for name, count in names |> List.countBy id do
              if count > 1 then
                  yield diagnostic "QUINT-IR-DUPLICATE-FIELD" (path + "/" + name) $"Field '%s{name}' occurs more than once." "Emit each JSON property exactly once." None
          for name in names |> List.distinct do
              if not (Set.contains name allowed) then
                  yield diagnostic "QUINT-IR-UNSUPPORTED-FIELD" (path + "/" + name) $"Field '%s{name}' is outside the exact Quint 0.32 adapter shape." "Remove unsupported constructs; arbitrary expressions and raw IR are not contract facts." None ]

    let tryProperty (name: string) (element: JsonElement) =
        let mutable found = Unchecked.defaultof<JsonElement>
        if element.TryGetProperty(name, &found) then Some found else None

    let stringAt path name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String -> Ok(value.GetString())
        | Some _ -> Error(diagnostic "QUINT-IR-TYPE" (path + "/" + name) "Expected a string." "Emit the exact Quint 0.32 adapter field type." None)
        | None -> Error(diagnostic "QUINT-IR-REQUIRED" (path + "/" + name) $"Required field '%s{name}' is absent." "Emit the complete typed/effect record." None)

    let intAt path name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Ok number
            | _ -> Error(diagnostic "QUINT-IR-TYPE" (path + "/" + name) "Expected a 32-bit integer." "Emit a source coordinate integer." None)
        | Some _ -> Error(diagnostic "QUINT-IR-TYPE" (path + "/" + name) "Expected an integer." "Emit a source coordinate integer." None)
        | None -> Error(diagnostic "QUINT-IR-REQUIRED" (path + "/" + name) $"Required field '%s{name}' is absent." "Emit the complete source binding." None)

    let stringsAt path name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Array ->
            let values = value.EnumerateArray() |> Seq.toList
            if values |> List.forall (fun item -> item.ValueKind = JsonValueKind.String) then Ok(values |> List.map _.GetString())
            else Error(diagnostic "QUINT-IR-TYPE" (path + "/" + name) "Expected an array of strings." "Emit stable catalogue identities only." None)
        | Some _ -> Error(diagnostic "QUINT-IR-TYPE" (path + "/" + name) "Expected an array." "Emit a semantic identity set." None)
        | None -> Ok []

[<RequireQualifiedAccess>]
module QuintProfile =
    let identity = ProfileCore.profile
    let quintVersion = ProfileCore.version
    let validate catalogue = ProfileCore.validate catalogue

    let adaptTypedEffectJson (canonicalSourcePath: string) (typedEffectJson: string) =
        let fail diagnostic = Error [ diagnostic ]
        try
            use document = JsonDocument.Parse typedEffectJson
            let root = document.RootElement
            if root.ValueKind <> JsonValueKind.Object then
                fail (ProfileCore.diagnostic "QUINT-IR-ROOT" "/" "Typed/effect JSON must be an object." "Emit the exact Quint 0.32 adapter envelope." None)
            else
                let mutable diagnostics = ProfileCore.properties "" (Set.ofList [ "quintVersion"; "profile"; "declarations" ]) root
                match ProfileCore.stringAt "" "quintVersion" root, ProfileCore.stringAt "" "profile" root, ProfileCore.tryProperty "declarations" root with
                | Ok version, Ok profile, Some declarations when declarations.ValueKind = JsonValueKind.Array ->
                    let mutable entries = []
                    let mutable effects = []
                    for index, declaration in declarations.EnumerateArray() |> Seq.indexed do
                        let path = $"/declarations/%d{index}"
                        if declaration.ValueKind <> JsonValueKind.Object then
                            diagnostics <- ProfileCore.diagnostic "QUINT-IR-TYPE" path "Declaration must be an object." "Emit one exact declaration record." None :: diagnostics
                        else
                            diagnostics <- ProfileCore.properties path (Set.ofList [ "id"; "kind"; "source"; "reads"; "writes"; "subjects" ]) declaration @ diagnostics
                            match ProfileCore.stringAt path "id" declaration, ProfileCore.stringAt path "kind" declaration, ProfileCore.tryProperty "source" declaration with
                            | Ok id, Ok kindText, Some source when source.ValueKind = JsonValueKind.Object ->
                                diagnostics <- ProfileCore.properties (path + "/source") (Set.ofList [ "startLine"; "startColumn"; "endLine"; "endColumn" ]) source @ diagnostics
                                match ProfileCore.parseKind kindText, ProfileCore.intAt (path + "/source") "startLine" source, ProfileCore.intAt (path + "/source") "startColumn" source, ProfileCore.intAt (path + "/source") "endLine" source, ProfileCore.intAt (path + "/source") "endColumn" source with
                                | Ok kind, Ok sl, Ok sc, Ok el, Ok ec ->
                                    let range = { Path = canonicalSourcePath; Start = { Line = sl; Column = sc }; End = { Line = el; Column = ec } }
                                    entries <- { Id = id; Kind = kind; Source = range } :: entries
                                    if kind = Action then
                                        match ProfileCore.stringsAt path "reads" declaration, ProfileCore.stringsAt path "writes" declaration, ProfileCore.stringsAt path "subjects" declaration with
                                        | Ok reads, Ok writes, Ok subjects -> effects <- { ActionId = id; Reads = List.sort reads; Writes = List.sort writes; Subjects = List.sort subjects } :: effects
                                        | results ->
                                            for result in [ match results with a, b, c -> a; b; c ] do
                                                match result with Error finding -> diagnostics <- finding :: diagnostics | Ok _ -> ()
                                | Error unsupported, _, _, _, _ -> diagnostics <- ProfileCore.diagnostic "QUINT-IR-UNSUPPORTED-KIND" (path + "/kind") $"Declaration kind '%s{unsupported}' is outside profile 1." "Use only the closed demonstrated catalogue kinds." None :: diagnostics
                                | _ -> diagnostics <- ProfileCore.diagnostic "QUINT-IR-SOURCE" (path + "/source") "Source coordinates are incomplete or invalid." "Emit all four integer source coordinates." None :: diagnostics
                            | _ -> diagnostics <- ProfileCore.diagnostic "QUINT-IR-DECLARATION" path "Declaration identity, kind, or source is incomplete." "Emit the exact declaration record." None :: diagnostics
                    let catalogue = { Profile = profile; QuintVersion = version; Entries = List.sortBy (fun row -> ProfileCore.kindText row.Kind, row.Id) entries; ActionEffects = List.sortBy _.ActionId effects }
                    let findings = ProfileCore.sorted (diagnostics @ ProfileCore.validate catalogue)
                    if List.isEmpty findings then Ok catalogue else Error findings
                | _, _, Some _ -> fail (ProfileCore.diagnostic "QUINT-IR-TYPE" "/declarations" "Declarations must be an array." "Emit the exact Quint 0.32 adapter envelope." None)
                | _ -> fail (ProfileCore.diagnostic "QUINT-IR-REQUIRED" "/" "The typed/effect envelope is incomplete." "Emit quintVersion, profile, and declarations." None)
        with :? JsonException as ex ->
            fail (ProfileCore.diagnostic "QUINT-IR-MALFORMED" "/" ex.Message "Emit valid UTF-8 JSON from exact Quint 0.32 typed/effect output." None)
