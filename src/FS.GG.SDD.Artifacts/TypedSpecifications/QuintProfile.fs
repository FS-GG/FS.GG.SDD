namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
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
    | ReachabilityProperty
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

type QuintCatalogueSourceBinding =
    { ModuleName: string
      CatalogueName: string
      Id: string
      Kind: QuintCatalogueKind
      Source: QuintSourceRange }

type QuintTypedEffectObservation =
    { Profile: string
      QuintVersion: string
      TypedEffectJson: string
      SourceBindings: QuintCatalogueSourceBinding list }

type QuintModelValue =
    | QuintBool of bool
    | QuintInt of int64
    | QuintString of string
    | QuintTuple of QuintModelValue list
    | QuintRecord of (string * QuintModelValue) list
    | QuintVariant of tag: string * value: QuintModelValue option
    | QuintList of QuintModelValue list
    | QuintSet of QuintModelValue list
    | QuintMap of (QuintModelValue * QuintModelValue) list

type QuintGeneralExportBinding =
    { Id: string
      ModuleName: string
      DeclarationName: string
      PromoteCatalogueRows: bool
      Source: QuintSourceRange }

type QuintModelCatalogueEntry =
    { Id: string
      Kind: string
      ExportId: string
      Value: QuintModelValue
      Source: QuintSourceRange }

type QuintGeneralExport =
    { Id: string
      ModuleName: string
      DeclarationName: string
      Value: QuintModelValue
      Source: QuintSourceRange }

type QuintGeneralProfileCatalogue =
    { Profile: string
      QuintVersion: string
      Exports: QuintGeneralExport list
      Catalogue: QuintModelCatalogueEntry list
      ActionEffects: QuintActionEffect list }

type QuintGeneralTypedEffectObservation =
    { Profile: string
      QuintVersion: string
      TypedEffectJson: string
      ExportBindings: QuintGeneralExportBinding list
      ActionBindings: QuintCatalogueSourceBinding list }

type QuintGeneralBindingManifest =
    { Schema: string
      Profile: string
      ModuleName: string
      Exports: QuintGeneralExportBinding list
      Actions: QuintCatalogueSourceBinding list }

module private ProfileCore =
    let profile = "fsgg-quint-profile/1"
    let version = "0.32.0"

    // Quint's node identities are deliberately not projected into the public contract, but
    // the internal adapter still has to bind every compiler-owned relation and hidden
    // declaration to the exact Q1-qualified program. These are the byte identities emitted
    // by pinned Quint 0.32.0 for the three accepted Q1 slices; accepting a merely
    // shape-compatible types/effects table would make those relations decorative.
    let admittedTypedEffectDigests =
        Map.ofList
            [ "RequirementsSlice", "6a7c4dd891a2b46753491b71cf3090ccf3449c97756e49055499463042e12af4"
              "SirDamageSlice", "34fa4c442985cb4bd7d29e76e32c55ff0c909c12578623851989d4e64b0fd6de"
              "CoordinationSlice", "d55b93de59a0b287e87cb9ae60f74d22a9548bb545fc2ba0829c62b75bc588df" ]

    let sha256Text (text: string) =
        text
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> _.ToLowerInvariant()

    let diagnostic code path message correction source : QuintProfileDiagnostic =
        { Code = code
          Path = path
          Message = message
          Correction = correction
          Source = source }

    let sorted findings =
        findings
        |> List.distinct
        |> List.sortBy (fun finding -> finding.Path, finding.Code, finding.Message)

    let validId =
        Regex("^[A-Z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$", RegexOptions.CultureInvariant)

    let safePath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (IO.Path.IsPathRooted path)
        && path.EndsWith(".md", StringComparison.Ordinal)
        && not (path.Contains('\\'))
        && not (
            path.Split('/')
            |> Array.exists (fun part -> part = "" || part = "." || part = "..")
        )

    let kindText =
        function
        | Requirement -> "requirement"
        | StateVariable -> "stateVariable"
        | Action -> "action"
        | Invariant -> "invariant"
        | TemporalProperty -> "temporalProperty"
        | ReachabilityProperty -> "reachabilityProperty"
        | Evidence -> "evidence"
        | Implementation -> "implementation"
        | ExternalSubject -> "externalSubject"

    let validate (catalogue: QuintProfileCatalogue) =
        [ if catalogue.Profile <> profile then
              yield
                  diagnostic
                      "QUINT-PROFILE-IDENTITY"
                      "/profile"
                      $"Expected '%s{profile}', got '%s{catalogue.Profile}'."
                      "Bind profile 1."
                      None
          if catalogue.QuintVersion <> version then
              yield
                  diagnostic
                      "QUINT-PROFILE-VERSION"
                      "/quintVersion"
                      $"Expected Quint %s{version}, got '%s{catalogue.QuintVersion}'."
                      "Use the pinned compiler."
                      None
          for index, entry in List.indexed catalogue.Entries do
              let path = $"/entries/%d{index}"

              if not (validId.IsMatch entry.Id) then
                  yield
                      diagnostic
                          "QUINT-PROFILE-ID"
                          (path + "/id")
                          $"'%s{entry.Id}' is not a stable identity."
                          "Use an uppercase-leading identity."
                          (Some entry.Source)

              if not (safePath entry.Source.Path) then
                  yield
                      diagnostic
                          "QUINT-PROFILE-SOURCE-PATH"
                          (path + "/source/path")
                          "Source path is not a safe relative Markdown path."
                          "Use QuintSource's canonical path."
                          (Some entry.Source)

              if
                  entry.Source.Start.Line < 1
                  || entry.Source.Start.Column < 1
                  || entry.Source.End.Line < entry.Source.Start.Line
                  || (entry.Source.End.Line = entry.Source.Start.Line
                      && entry.Source.End.Column < entry.Source.Start.Column)
              then
                  yield
                      diagnostic
                          "QUINT-PROFILE-SOURCE-RANGE"
                          (path + "/source")
                          "Source range is not positive and ordered."
                          "Use QuintSource's exact range."
                          (Some entry.Source)
          for (kind, id), rows in catalogue.Entries |> List.groupBy (fun row -> row.Kind, row.Id) do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-PROFILE-ID-DUPLICATE"
                          "/entries"
                          $"'%s{kindText kind}:%s{id}' occurs more than once."
                          "Declare each row once."
                          (Some rows.Head.Source)
          let actions =
              catalogue.Entries
              |> List.choose (fun row -> if row.Kind = Action then Some row.Id else None)
              |> Set.ofList

          for index, effect in List.indexed catalogue.ActionEffects do
              let path = $"/actionEffects/%d{index}"

              if not (actions.Contains effect.ActionId) then
                  yield
                      diagnostic
                          "QUINT-PROFILE-ACTION-REFERENCE"
                          (path + "/actionId")
                          "Effect action is not declared."
                          "Reference an action row."
                          None

              for field, values in [ "reads", effect.Reads; "writes", effect.Writes; "subjects", effect.Subjects ] do
                  for value in values do
                      if not (validId.IsMatch value) then
                          yield
                              diagnostic
                                  "QUINT-PROFILE-REFERENCE"
                                  (path + "/" + field)
                                  $"'%s{value}' is not a stable identity."
                                  "Use semantic identities, not node ids."
                                  None

                  if List.length (List.distinct values) <> values.Length then
                      yield
                          diagnostic
                              "QUINT-PROFILE-REFERENCE-DUPLICATE"
                              (path + "/" + field)
                              "Effect set contains duplicates."
                              "Remove duplicates."
                              None

          for actionId, rows in catalogue.ActionEffects |> List.groupBy _.ActionId do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-PROFILE-EFFECT-DUPLICATE"
                          "/actionEffects"
                          $"'%s{actionId}' has multiple rows."
                          "Emit one row."
                          None ]
        |> sorted

    let tryProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let fields path required allowed (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            [ diagnostic "QUINT-IR-TYPE" path "Expected an object." "Use exact compiler output." None ]
        else
            let names = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList

            [ for name, count in List.countBy id names do
                  if count > 1 then
                      yield
                          diagnostic
                              "QUINT-IR-DUPLICATE-FIELD"
                              (path + "/" + name)
                              "Duplicate JSON field."
                              "Use unmodified compiler output."
                              None
              for name in List.distinct names do
                  if not (Set.contains name allowed) then
                      yield
                          diagnostic
                              "QUINT-IR-UNSUPPORTED-FIELD"
                              (path + "/" + name)
                              $"Field '%s{name}' is not in the exact shape."
                              "Use unmodified Quint 0.32.0 output."
                              None
              for name in required do
                  if not (List.contains name names) then
                      yield
                          diagnostic
                              "QUINT-IR-REQUIRED"
                              (path + "/" + name)
                              $"Field '%s{name}' is absent."
                              "Use complete compiler output."
                              None ]

    let stringAt path name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String ->
            match value.GetString() with
            | null ->
                Error
                    [ diagnostic
                          "QUINT-IR-TYPE"
                          (path + "/" + name)
                          "Expected a non-null string."
                          "Use unmodified compiler output."
                          None ]
            | text -> Ok text
        | Some _ ->
            Error
                [ diagnostic
                      "QUINT-IR-TYPE"
                      (path + "/" + name)
                      "Expected a string."
                      "Use unmodified compiler output."
                      None ]
        | None ->
            Error
                [ diagnostic
                      "QUINT-IR-REQUIRED"
                      (path + "/" + name)
                      "Required string is absent."
                      "Use complete compiler output."
                      None ]

    let intAt path name element =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt64() with
            | true, number -> Ok number
            | _ ->
                Error
                    [ diagnostic
                          "QUINT-IR-TYPE"
                          (path + "/" + name)
                          "Expected integer node id."
                          "Use unmodified output."
                          None ]
        | _ ->
            Error
                [ diagnostic
                      "QUINT-IR-TYPE"
                      (path + "/" + name)
                      "Expected integer node id."
                      "Use unmodified output."
                      None ]

    let literal path element =
        let findings =
            fields path (Set.ofList [ "id"; "kind"; "value" ]) (Set.ofList [ "id"; "kind"; "value" ]) element

        match stringAt path "kind" element, stringAt path "value" element, intAt path "id" element with
        | Ok "str", Ok value, Ok _ when List.isEmpty findings -> Ok value
        | Ok kind, _, _ when kind <> "str" ->
            Error
                [ diagnostic
                      "QUINT-IR-EXPRESSION-KIND"
                      (path + "/kind")
                      $"Expected str, got '%s{kind}'."
                      "Use string catalogue values."
                      None ]
        | _ -> Error findings

    let app path opcode element =
        let required = Set.ofList [ "id"; "kind"; "opcode"; "args" ]
        let findings = fields path required required element

        match
            stringAt path "kind" element,
            stringAt path "opcode" element,
            tryProperty "args" element,
            intAt path "id" element
        with
        | Ok "app", Ok actual, Some args, Ok _ when
            actual = opcode && args.ValueKind = JsonValueKind.Array && List.isEmpty findings
            ->
            Ok(args.EnumerateArray() |> Seq.toList)
        | Ok "app", Ok actual, _, _ when actual <> opcode ->
            Error
                [ diagnostic
                      "QUINT-IR-UNSUPPORTED-OPCODE"
                      (path + "/opcode")
                      $"Expected '%s{opcode}', got '%s{actual}'."
                      "Use the explicit profile expression."
                      None ]
        | Ok kind, _, _, _ when kind <> "app" ->
            Error
                [ diagnostic
                      "QUINT-IR-EXPRESSION-KIND"
                      (path + "/kind")
                      $"Expression '%s{kind}' is not admitted."
                      "Use an explicit profile expression."
                      None ]
        | _, _, Some args, _ when args.ValueKind <> JsonValueKind.Array ->
            Error [ diagnostic "QUINT-IR-TYPE" (path + "/args") "Args must be an array." "Use unmodified output." None ]
        | _ -> Error findings

    let record path expected element =
        match app path "Rec" element with
        | Error errors -> Error errors
        | Ok args when args.Length % 2 <> 0 ->
            Error
                [ diagnostic
                      "QUINT-IR-RECORD-SHAPE"
                      (path + "/args")
                      "Record key/value arguments are unpaired."
                      "Use a closed record."
                      None ]
        | Ok args ->
            let pairs =
                args
                |> List.chunkBySize 2
                |> List.mapi (fun index pair ->
                    match literal ($"%s{path}/args/%d{index * 2}") pair[0] with
                    | Ok name -> Ok(name, pair[1])
                    | Error errors -> Error errors)

            let errors =
                pairs
                |> List.collect (function
                    | Error errors -> errors
                    | _ -> [])

            let values =
                pairs
                |> List.choose (function
                    | Ok pair -> Some pair
                    | _ -> None)

            let names = List.map fst values

            let shape =
                [ for name, count in List.countBy id names do
                      if count > 1 then
                          yield
                              diagnostic
                                  "QUINT-IR-RECORD-DUPLICATE"
                                  path
                                  $"Field '%s{name}' is duplicated."
                                  "Use each field once."
                                  None
                  for name in names do
                      if not (Set.contains name expected) then
                          yield
                              diagnostic
                                  "QUINT-IR-RECORD-FIELD"
                                  path
                                  $"Field '%s{name}' is outside the profile row."
                                  "Remove unsupported semantics."
                                  None
                  for name in expected do
                      if not (List.contains name names) then
                          yield
                              diagnostic
                                  "QUINT-IR-RECORD-REQUIRED"
                                  path
                                  $"Field '%s{name}' is absent."
                                  "Emit the closed row."
                                  None ]

            if List.isEmpty (errors @ shape) then
                Ok(Map.ofList values)
            else
                Error(errors @ shape)

    let strings path element =
        match app path "Set" element with
        | Error errors -> Error errors
        | Ok items ->
            let parsed =
                items |> List.mapi (fun index item -> literal ($"%s{path}/args/%d{index}") item)

            let errors =
                parsed
                |> List.collect (function
                    | Error errors -> errors
                    | _ -> [])

            if List.isEmpty errors then
                Ok(
                    parsed
                    |> List.choose (function
                        | Ok value -> Some value
                        | _ -> None)
                )
            else
                Error errors

    let getString path name (values: Map<string, JsonElement>) =
        literal (path + "/" + name) values[name]

    type RawRow =
        { ModuleName: string
          CatalogueName: string
          Id: string
          Kind: QuintCatalogueKind
          Reads: string list
          Writes: string list }

    let simpleRow moduleName catalogueName kind expected path element =
        match record path expected element with
        | Error errors -> Error errors
        | Ok values ->
            match getString path "id" values with
            | Error errors -> Error errors
            | Ok id ->
                Ok
                    { ModuleName = moduleName
                      CatalogueName = catalogueName
                      Id = id
                      Kind = kind
                      Reads = []
                      Writes = [] }

    let propertyRow moduleName catalogueName path element =
        match record path (Set.ofList [ "id"; "kind" ]) element with
        | Error errors -> Error errors
        | Ok values ->
            match getString path "id" values, getString path "kind" values with
            | Ok id, Ok "invariant" ->
                Ok
                    { ModuleName = moduleName
                      CatalogueName = catalogueName
                      Id = id
                      Kind = Invariant
                      Reads = []
                      Writes = [] }
            | Ok id, Ok "temporal" ->
                Ok
                    { ModuleName = moduleName
                      CatalogueName = catalogueName
                      Id = id
                      Kind = TemporalProperty
                      Reads = []
                      Writes = [] }
            | Ok id, Ok "reachability" ->
                Ok
                    { ModuleName = moduleName
                      CatalogueName = catalogueName
                      Id = id
                      Kind = ReachabilityProperty
                      Reads = []
                      Writes = [] }
            | Ok _, Ok kind ->
                Error
                    [ diagnostic
                          "QUINT-IR-PROPERTY-KIND"
                          (path + "/kind")
                          $"Property kind '%s{kind}' is not admitted."
                          "Use invariant, temporal, or reachability."
                          None ]
            | Error errors, _
            | _, Error errors -> Error errors

    let actionRow moduleName catalogueName path withArgument element =
        let expected =
            if withArgument then
                Set.ofList [ "id"; "argument"; "reads"; "writes" ]
            else
                Set.ofList [ "id"; "reads"; "writes" ]

        match record path expected element with
        | Error errors -> Error errors
        | Ok values ->
            match
                getString path "id" values,
                strings (path + "/reads") values["reads"],
                strings (path + "/writes") values["writes"]
            with
            | Ok id, Ok reads, Ok writes ->
                Ok
                    { ModuleName = moduleName
                      CatalogueName = catalogueName
                      Id = id
                      Kind = Action
                      Reads = List.sort reads
                      Writes = List.sort writes }
            | a, b, c ->
                Error
                    [ for result in [ Result.map ignore a; Result.map ignore b; Result.map ignore c ] do
                          match result with
                          | Error errors -> yield! errors
                          | _ -> () ]

    let numericTable (path: string) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            [ diagnostic "QUINT-IR-TYPE" path "Expected an id-indexed object." "Use unmodified output." None ]
        else
            [ if not (element.EnumerateObject() |> Seq.isEmpty) then
                  ()
              else
                  yield
                      diagnostic
                          "QUINT-IR-TABLE-EMPTY"
                          path
                          "Compiler-owned type/effect evidence is empty."
                          "Use complete Quint 0.32.0 typecheck output."
                          None

              for item in element.EnumerateObject() do
                  match Int64.TryParse(item.Name, NumberStyles.None, CultureInfo.InvariantCulture) with
                  | true, _ when item.Value.ValueKind = JsonValueKind.Object -> ()
                  | true, _ ->
                      yield
                          diagnostic
                              "QUINT-IR-TYPE"
                              (path + "/" + item.Name)
                              "Table value must be an object."
                              "Use unmodified output."
                              None
                  | _ ->
                      yield
                          diagnostic
                              "QUINT-IR-TABLE-KEY"
                              (path + "/" + item.Name)
                              "Table key must be a decimal node id."
                              "Use unmodified output."
                              None ]

    let private intrinsicOpcodes =
        Set.ofList
            [ "Rec"
              "Set"
              "Tup"
              "actionAll"
              "actionAny"
              "and"
              "assign"
              "contains"
              "eq"
              "eventually"
              "exists"
              "expect"
              "field"
              "iadd"
              "igte"
              "ilt"
              "ilte"
              "implies"
              "isub"
              "ite"
              "neq"
              "not"
              "oneOf"
              "then"
              "to"
              "union"
              "variant"
              "weakFair" ]

    let private kindShape kind =
        match kind with
        | "app" -> Some(Set.ofList [ "id"; "kind"; "opcode"; "args" ], Set.ofList [ "id"; "kind"; "opcode"; "args" ])
        | "arrow" -> Some(Set.ofList [ "kind"; "params"; "result" ], Set.ofList [ "kind"; "params"; "result" ])
        | "bool"
        | "int"
        | "str" -> Some(Set.ofList [ "kind" ], Set.ofList [ "id"; "kind"; "value" ])
        | "concrete" -> Some(Set.ofList [ "kind" ], Set.ofList [ "kind"; "components"; "stateVariables" ])
        | "const" -> Some(Set.ofList [ "id"; "kind"; "name" ], Set.ofList [ "id"; "kind"; "name" ])
        | "def" ->
            Some(
                Set.ofList [ "id"; "kind"; "name"; "qualifier"; "expr" ],
                Set.ofList
                    [ "id"
                      "kind"
                      "name"
                      "qualifier"
                      "expr"
                      "depth"
                      "hidden"
                      "importedFrom"
                      "shadowing"
                      "typeAnnotation" ]
            )
        | "empty" -> Some(Set.ofList [ "kind" ], Set.ofList [ "kind" ])
        | "import" ->
            Some(
                Set.ofList [ "id"; "kind"; "defName"; "protoName" ],
                Set.ofList [ "id"; "kind"; "defName"; "protoName" ]
            )
        | "lambda" ->
            Some(
                Set.ofList [ "id"; "kind"; "params"; "qualifier"; "expr" ],
                Set.ofList [ "id"; "kind"; "params"; "qualifier"; "expr" ]
            )
        | "let" -> Some(Set.ofList [ "id"; "kind"; "opdef"; "expr" ], Set.ofList [ "id"; "kind"; "opdef"; "expr" ])
        | "name" -> Some(Set.ofList [ "id"; "kind"; "name" ], Set.ofList [ "id"; "kind"; "name" ])
        | "oper" -> Some(Set.ofList [ "kind"; "args"; "res" ], Set.ofList [ "kind"; "args"; "res" ])
        | "param" ->
            Some(Set.ofList [ "id"; "kind"; "name" ], Set.ofList [ "id"; "kind"; "name"; "depth"; "typeAnnotation" ])
        | "read"
        | "temporal"
        | "update" -> Some(Set.ofList [ "kind"; "entity" ], Set.ofList [ "kind"; "entity" ])
        | "rec"
        | "sum"
        | "tup" -> Some(Set.ofList [ "kind"; "fields" ], Set.ofList [ "id"; "kind"; "fields" ])
        | "row" -> Some(Set.ofList [ "kind"; "fields"; "other" ], Set.ofList [ "kind"; "fields"; "other" ])
        | "set" -> Some(Set.ofList [ "id"; "kind"; "elem" ], Set.ofList [ "id"; "kind"; "elem" ])
        | "typedef" ->
            Some(
                Set.ofList [ "id"; "kind"; "name"; "type"; "depth" ],
                Set.ofList [ "id"; "kind"; "name"; "type"; "depth" ]
            )
        | "union" -> Some(Set.ofList [ "kind"; "entities" ], Set.ofList [ "kind"; "entities" ])
        | "var" ->
            Some(
                Set.ofList [ "id"; "kind"; "name"; "depth"; "typeAnnotation" ],
                Set.ofList [ "id"; "kind"; "name"; "depth"; "typeAnnotation"; "hidden"; "importedFrom" ]
            )
        | "variable" -> Some(Set.ofList [ "kind"; "name" ], Set.ofList [ "kind"; "name" ])
        | _ -> None

    let private untaggedShapes =
        Set.ofList
            [ Set.ofList [ "id"; "name"; "declarations" ]
              Set.ofList [ "fieldName"; "fieldType" ]
              Set.ofList [ "id"; "name" ]
              Set.ofList [ "id"; "name"; "typeAnnotation" ]
              Set.ofList [ "name"; "reference" ]
              Set.ofList [ "rowVariables"; "type"; "typeVariables" ]
              Set.ofList [ "effect"; "effectVariables"; "entityVariables" ] ]

    let rec validateClosedNode definitions path (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.indexed
            |> Seq.collect (fun (index, item) -> validateClosedNode definitions $"%s{path}/%d{index}" item)
            |> Seq.toList
        | JsonValueKind.Object ->
            let properties = element.EnumerateObject() |> Seq.toList
            let names = properties |> List.map _.Name |> Set.ofList

            let shapeFindings =
                match tryProperty "kind" element with
                | Some value when value.ValueKind = JsonValueKind.String ->
                    let kind = value.GetString() |> Option.ofObj |> Option.defaultValue ""

                    match kindShape kind with
                    | Some(required, allowed) -> fields path required allowed element
                    | None ->
                        [ diagnostic
                              "QUINT-IR-UNSUPPORTED-KIND"
                              (path + "/kind")
                              $"IR kind '%s{kind}' is outside fsgg-quint-profile/1."
                              "Use only the Q1-qualified Quint 0.32.0 subset."
                              None ]
                | Some _ ->
                    [ diagnostic
                          "QUINT-IR-TYPE"
                          (path + "/kind")
                          "IR kind must be a string."
                          "Use unmodified compiler output."
                          None ]
                | None when properties |> List.forall (fun item -> Int64.TryParse(item.Name) |> fst) -> []
                | None when Set.contains names untaggedShapes -> []
                | None ->
                    [ diagnostic
                          "QUINT-IR-UNSUPPORTED-SHAPE"
                          path
                          "Object shape is outside the exact Quint 0.32.0 profile boundary."
                          "Use only the Q1-qualified IR shape."
                          None ]

            let opcodeFindings =
                match tryProperty "kind" element, tryProperty "opcode" element with
                | Some kind, Some opcode when
                    kind.ValueKind = JsonValueKind.String
                    && kind.GetString() = "app"
                    && opcode.ValueKind = JsonValueKind.String
                    ->
                    let value = opcode.GetString() |> Option.ofObj |> Option.defaultValue ""

                    if Set.contains value intrinsicOpcodes || Set.contains value definitions then
                        []
                    else
                        [ diagnostic
                              "QUINT-IR-UNSUPPORTED-OPCODE"
                              (path + "/opcode")
                              $"Opcode '%s{value}' is neither a qualified intrinsic nor a resolved local definition."
                              "Use only the Q1-qualified Quint subset."
                              None ]
                | _ -> []

            let nested =
                properties
                |> List.collect (fun item -> validateClosedNode definitions (path + "/" + item.Name) item.Value)

            shapeFindings @ opcodeFindings @ nested
        | _ -> []

    let numericKeys (element: JsonElement) =
        if element.ValueKind = JsonValueKind.Object then
            element.EnumerateObject() |> Seq.map _.Name |> Set.ofSeq
        else
            Set.empty

    let rowKey (row: RawRow) =
        row.ModuleName, row.CatalogueName, row.Kind, row.Id

    let bindingKey (binding: QuintCatalogueSourceBinding) =
        binding.ModuleName, binding.CatalogueName, binding.Kind, binding.Id

[<RequireQualifiedAccess>]
module QuintProfile =
    let identity = ProfileCore.profile
    let quintVersion = ProfileCore.version
    let validate catalogue = ProfileCore.validate catalogue

    let adaptTypedEffectJson (observation: QuintTypedEffectObservation) =
        let identityFindings =
            [ if String.IsNullOrWhiteSpace observation.Profile then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-PROFILE-IDENTITY-MISSING"
                          "/profile"
                          "Profile binding is absent."
                          "Bind profile 1 out of band."
                          None
              elif observation.Profile <> ProfileCore.profile then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-PROFILE-IDENTITY"
                          "/profile"
                          "Profile binding is wrong."
                          "Bind profile 1."
                          None
              if String.IsNullOrWhiteSpace observation.QuintVersion then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-PROFILE-VERSION-MISSING"
                          "/quintVersion"
                          "Compiler version binding is absent."
                          "Record the pinned binary's --version."
                          None
              elif observation.QuintVersion <> ProfileCore.version then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-PROFILE-VERSION"
                          "/quintVersion"
                          "Compiler version binding is wrong."
                          "Use Quint 0.32.0."
                          None ]

        if not (List.isEmpty identityFindings) then
            Error(ProfileCore.sorted identityFindings)
        else
            try
                use document = JsonDocument.Parse observation.TypedEffectJson
                let root = document.RootElement

                if root.ValueKind <> JsonValueKind.Object then
                    Error
                        [ ProfileCore.diagnostic
                              "QUINT-IR-ROOT"
                              "/"
                              "Typecheck output must be an object."
                              "Use exact typecheck --out JSON."
                              None ]
                else
                    let rootFields =
                        Set.ofList [ "stage"; "modules"; "table"; "types"; "effects"; "warnings"; "errors" ]

                    let mutable findings = ProfileCore.fields "" rootFields rootFields root

                    match ProfileCore.stringAt "" "stage" root with
                    | Ok "typechecking" -> ()
                    | Ok stage ->
                        findings <-
                            ProfileCore.diagnostic
                                "QUINT-IR-STAGE"
                                "/stage"
                                $"Unexpected stage '%s{stage}'."
                                "Use completed typechecking output."
                                None
                            :: findings
                    | Error errors -> findings <- errors @ findings

                    for name in [ "errors"; "warnings" ] do
                        match ProfileCore.tryProperty name root with
                        | Some value when value.ValueKind = JsonValueKind.Array && value.GetArrayLength() = 0 -> ()
                        | Some value when value.ValueKind = JsonValueKind.Array ->
                            findings <-
                                ProfileCore.diagnostic
                                    (if name = "errors" then
                                         "QUINT-IR-COMPILER-ERROR"
                                     else
                                         "QUINT-IR-COMPILER-WARNING")
                                    ("/" + name)
                                    $"Compiler output contains %s{name}."
                                    "Resolve all compiler diagnostics."
                                    None
                                :: findings
                        | Some _ ->
                            findings <-
                                ProfileCore.diagnostic
                                    "QUINT-IR-TYPE"
                                    ("/" + name)
                                    "Expected an array."
                                    "Use unmodified output."
                                    None
                                :: findings
                        | None -> ()

                    let definitions =
                        match ProfileCore.tryProperty "modules" root with
                        | Some modules when modules.ValueKind = JsonValueKind.Array ->
                            modules.EnumerateArray()
                            |> Seq.collect (fun item ->
                                match ProfileCore.tryProperty "declarations" item with
                                | Some declarations when declarations.ValueKind = JsonValueKind.Array ->
                                    declarations.EnumerateArray()
                                    |> Seq.choose (fun declaration ->
                                        match
                                            ProfileCore.stringAt "" "kind" declaration,
                                            ProfileCore.stringAt "" "name" declaration
                                        with
                                        | Ok "def", Ok name -> Some name
                                        | _ -> None)
                                | _ -> Seq.empty)
                            |> Set.ofSeq
                        | _ -> Set.empty

                    for name in [ "modules"; "table"; "types"; "effects" ] do
                        match ProfileCore.tryProperty name root with
                        | Some value ->
                            findings <- ProfileCore.validateClosedNode definitions ("/" + name) value @ findings
                        | None -> ()

                    for name in [ "table"; "types"; "effects" ] do
                        match ProfileCore.tryProperty name root with
                        | Some value -> findings <- ProfileCore.numericTable ("/" + name) value @ findings
                        | None -> ()

                    match ProfileCore.tryProperty "types" root, ProfileCore.tryProperty "effects" root with
                    | Some types, Some effects when ProfileCore.numericKeys types <> ProfileCore.numericKeys effects ->
                        findings <-
                            ProfileCore.diagnostic
                                "QUINT-IR-EFFECT-TYPE-COVERAGE"
                                "/effects"
                                "Compiler type and effect evidence do not cover the same node identities."
                                "Use one complete Quint 0.32.0 typecheck observation."
                                None
                            :: findings
                    | _ -> ()

                    let mutable rows: ProfileCore.RawRow list = []

                    match ProfileCore.tryProperty "modules" root with
                    | Some modules when modules.ValueKind = JsonValueKind.Array ->
                        for moduleIndex, moduleElement in modules.EnumerateArray() |> Seq.indexed do
                            let modulePath = $"/modules/%d{moduleIndex}"
                            let moduleFields = Set.ofList [ "id"; "name"; "declarations" ]
                            findings <- ProfileCore.fields modulePath moduleFields moduleFields moduleElement @ findings

                            match
                                ProfileCore.stringAt modulePath "name" moduleElement,
                                ProfileCore.intAt modulePath "id" moduleElement,
                                ProfileCore.tryProperty "declarations" moduleElement
                            with
                            | Ok moduleName, Ok _, Some declarations when declarations.ValueKind = JsonValueKind.Array ->
                                let definitions =
                                    declarations.EnumerateArray()
                                    |> Seq.choose (fun declaration ->
                                        match
                                            ProfileCore.stringAt "" "kind" declaration,
                                            ProfileCore.stringAt "" "name" declaration,
                                            ProfileCore.tryProperty "expr" declaration
                                        with
                                        | Ok "def", Ok name, Some expression -> Some(name, expression)
                                        | _ -> None)
                                    |> Map.ofSeq

                                for declarationIndex, declaration in declarations.EnumerateArray() |> Seq.indexed do
                                    let path = $"%s{modulePath}/declarations/%d{declarationIndex}"

                                    match
                                        ProfileCore.stringAt path "kind" declaration,
                                        ProfileCore.stringAt path "name" declaration,
                                        ProfileCore.stringAt path "qualifier" declaration,
                                        ProfileCore.tryProperty "expr" declaration
                                    with
                                    | Ok "def", Ok catalogueName, Ok "pureval", Some expression ->
                                        if
                                            catalogueName = "requirements"
                                            || catalogueName = "evidenceCatalogue"
                                            || catalogueName = "actionCatalogue"
                                            || catalogueName = "actions"
                                            || catalogueName = "propertyCatalogue"
                                            || catalogueName.EndsWith("Catalogue", StringComparison.Ordinal)
                                        then
                                            let exactDeclarationFields =
                                                Set.ofList [ "id"; "kind"; "name"; "qualifier"; "expr" ]

                                            findings <-
                                                ProfileCore.fields
                                                    path
                                                    exactDeclarationFields
                                                    exactDeclarationFields
                                                    declaration
                                                @ findings

                                            match ProfileCore.intAt (path + "/expr") "id" expression with
                                            | Ok expressionId ->
                                                let key = expressionId.ToString(CultureInfo.InvariantCulture)

                                                for tableName in [ "types"; "effects" ] do
                                                    match ProfileCore.tryProperty tableName root with
                                                    | Some table when Set.contains key (ProfileCore.numericKeys table) ->
                                                        ()
                                                    | _ ->
                                                        findings <-
                                                            ProfileCore.diagnostic
                                                                "QUINT-IR-CATALOGUE-EVIDENCE"
                                                                ($"/%s{tableName}/%s{key}")
                                                                $"Catalogue '%s{catalogueName}' has no compiler %s{tableName} evidence."
                                                                "Use complete typecheck output from the same module."
                                                                None
                                                            :: findings
                                            | Error errors -> findings <- errors @ findings

                                        let parseRows parser =
                                            match ProfileCore.app (path + "/expr") "Set" expression with
                                            | Error errors -> Error errors
                                            | Ok elements ->
                                                let parsed = elements |> List.mapi parser

                                                let errors =
                                                    parsed
                                                    |> List.collect (function
                                                        | Error errors -> errors
                                                        | _ -> [])

                                                if List.isEmpty errors then
                                                    Ok(
                                                        parsed
                                                        |> List.choose (function
                                                            | Ok row -> Some row
                                                            | _ -> None)
                                                    )
                                                else
                                                    Error errors

                                        let result =
                                            match catalogueName with
                                            | "requirements" ->
                                                Some(
                                                    parseRows (fun index item ->
                                                        match
                                                            ProfileCore.stringAt "" "kind" item,
                                                            ProfileCore.stringAt "" "name" item
                                                        with
                                                        | Ok "name", Ok itemName ->
                                                            match Map.tryFind itemName definitions with
                                                            | Some resolved ->
                                                                ProfileCore.simpleRow
                                                                    moduleName
                                                                    catalogueName
                                                                    Requirement
                                                                    (Set.ofList [ "id"; "evidenceId"; "priority" ])
                                                                    ($"%s{path}/expr/args/%d{index}")
                                                                    resolved
                                                            | None ->
                                                                Error
                                                                    [ ProfileCore.diagnostic
                                                                          "QUINT-IR-NAME-REFERENCE"
                                                                          path
                                                                          "Requirement record reference does not resolve."
                                                                          "Use a local pure val record."
                                                                          None ]
                                                        | _ ->
                                                            ProfileCore.simpleRow
                                                                moduleName
                                                                catalogueName
                                                                Requirement
                                                                (Set.ofList [ "id"; "evidenceId"; "priority" ])
                                                                ($"%s{path}/expr/args/%d{index}")
                                                                item)
                                                )
                                            | "evidenceCatalogue" ->
                                                Some(
                                                    parseRows (fun index item ->
                                                        ProfileCore.simpleRow
                                                            moduleName
                                                            catalogueName
                                                            Evidence
                                                            (Set.ofList [ "id"; "kind"; "required" ])
                                                            ($"%s{path}/expr/args/%d{index}")
                                                            item)
                                                )
                                            | "actionCatalogue"
                                            | "actions" ->
                                                Some(
                                                    parseRows (fun index item ->
                                                        ProfileCore.actionRow
                                                            moduleName
                                                            catalogueName
                                                            ($"%s{path}/expr/args/%d{index}")
                                                            (catalogueName = "actions")
                                                            item)
                                                )
                                            | "propertyCatalogue" ->
                                                Some(
                                                    parseRows (fun index item ->
                                                        ProfileCore.propertyRow
                                                            moduleName
                                                            catalogueName
                                                            ($"%s{path}/expr/args/%d{index}")
                                                            item)
                                                )
                                            | other when other.EndsWith("Catalogue", StringComparison.Ordinal) ->
                                                Some(
                                                    Error
                                                        [ ProfileCore.diagnostic
                                                              "QUINT-IR-UNKNOWN-CATALOGUE"
                                                              (path + "/name")
                                                              $"Catalogue '%s{other}' is outside profile 1."
                                                              "Use the closed profile catalogue names."
                                                              None ]
                                                )
                                            | _ -> None

                                        match result with
                                        | Some(Ok parsed) -> rows <- parsed @ rows
                                        | Some(Error errors) -> findings <- errors @ findings
                                        | None -> ()
                                    | _ -> ()
                            | _, _, Some declarations when declarations.ValueKind <> JsonValueKind.Array ->
                                findings <-
                                    ProfileCore.diagnostic
                                        "QUINT-IR-TYPE"
                                        (modulePath + "/declarations")
                                        "Declarations must be an array."
                                        "Use unmodified output."
                                        None
                                    :: findings
                            | _ -> ()
                    | Some _ ->
                        findings <-
                            ProfileCore.diagnostic
                                "QUINT-IR-TYPE"
                                "/modules"
                                "Modules must be an array."
                                "Use unmodified output."
                                None
                            :: findings
                    | None -> ()

                    let groups =
                        observation.SourceBindings |> List.groupBy ProfileCore.bindingKey |> Map.ofList

                    for key, bindings in Map.toList groups do
                        if bindings.Length <> 1 then
                            findings <-
                                ProfileCore.diagnostic
                                    "QUINT-IR-SOURCE-BINDING-DUPLICATE"
                                    "/sourceBindings"
                                    $"Binding '%A{key}' is duplicated."
                                    "Emit one QuintSource binding per row."
                                    None
                                :: findings

                    let rowKeys = rows |> List.map ProfileCore.rowKey |> Set.ofList

                    for binding in observation.SourceBindings do
                        if not (Set.contains (ProfileCore.bindingKey binding) rowKeys) then
                            findings <-
                                ProfileCore.diagnostic
                                    "QUINT-IR-SOURCE-BINDING-UNUSED"
                                    "/sourceBindings"
                                    $"Binding for '%s{binding.Id}' has no compiler row."
                                    "Regenerate bindings from the same extraction."
                                    (Some binding.Source)
                                :: findings

                    let entries =
                        rows
                        |> List.choose (fun row ->
                            match Map.tryFind (ProfileCore.rowKey row) groups with
                            | Some [ binding ] ->
                                Some
                                    { Id = row.Id
                                      Kind = row.Kind
                                      Source = binding.Source }
                            | _ ->
                                findings <-
                                    ProfileCore.diagnostic
                                        "QUINT-IR-SOURCE-BINDING-REQUIRED"
                                        "/sourceBindings"
                                        $"Quint 0.32.0 has no source coordinates; '%s{row.ModuleName}/%s{row.CatalogueName}/%s{row.Id}' needs a QuintSource binding."
                                        "Supply the exact literate range; never infer it from node ids."
                                        None
                                    :: findings

                                None)
                        |> List.sortBy (fun row -> ProfileCore.kindText row.Kind, row.Id)

                    let effects =
                        rows
                        |> List.choose (fun row ->
                            if row.Kind = Action then
                                Some
                                    { ActionId = row.Id
                                      Reads = row.Reads
                                      Writes = row.Writes
                                      Subjects = [] }
                            else
                                None)
                        |> List.sortBy _.ActionId

                    let catalogue =
                        { Profile = observation.Profile
                          QuintVersion = observation.QuintVersion
                          Entries = entries
                          ActionEffects = effects }

                    let bindingModules =
                        observation.SourceBindings |> List.map _.ModuleName |> List.distinct

                    match bindingModules with
                    | [ moduleName ] ->
                        let actual = ProfileCore.sha256Text observation.TypedEffectJson

                        match Map.tryFind moduleName ProfileCore.admittedTypedEffectDigests with
                        | Some expected when actual = expected -> ()
                        | Some expected ->
                            findings <-
                                ProfileCore.diagnostic
                                    "QUINT-IR-SEMANTIC-DIGEST"
                                    "/"
                                    $"Typed/effect semantics for '%s{moduleName}' do not match the exact Q1-qualified Quint 0.32.0 observation (expected %s{expected}, actual %s{actual})."
                                    "Regenerate this exact Q1 slice with the content-addressed Quint 0.32.0 toolchain; do not substitute type/effect relations or hidden declarations."
                                    (observation.SourceBindings |> List.tryHead |> Option.map _.Source)
                                :: findings
                        | None ->
                            findings <-
                                ProfileCore.diagnostic
                                    "QUINT-IR-SEMANTIC-PROGRAM"
                                    "/sourceBindings"
                                    $"Module '%s{moduleName}' is not one of the three Q1-qualified semantic programs."
                                    "Use RequirementsSlice, SirDamageSlice, or CoordinationSlice from the accepted Q1 corpus."
                                    (observation.SourceBindings |> List.tryHead |> Option.map _.Source)
                                :: findings
                    | _ -> ()

                    let all = ProfileCore.sorted (findings @ ProfileCore.validate catalogue)
                    if List.isEmpty all then Ok catalogue else Error all
            with :? JsonException as ex ->
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-IR-MALFORMED"
                          "/"
                          ex.Message
                          "Use valid exact typecheck --out JSON."
                          None ]

module private GeneralProfileCore =
    let profile = "fsgg-quint-profile/2"
    let version = "0.32.0"
    let maxTypedEffectBytes = 16 * 1024 * 1024
    let maxDeclarations = 4096
    let maxBindings = 4096
    let maxExports = 256
    let maxValueNodes = 100000
    let maxValueDepth = 32
    let maxStringBytes = 64 * 1024

    let sourceOfExport (binding: QuintGeneralExportBinding) = Some binding.Source
    let sourceOfAction (binding: QuintCatalogueSourceBinding) = Some binding.Source

    let validBindingId =
        Regex("^[A-Za-z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$", RegexOptions.CultureInvariant)

    let sourceFindings path (id: string) (source: QuintSourceRange) =
        [ if not (validBindingId.IsMatch id) then
              yield
                  ProfileCore.diagnostic
                      "QUINT-GENERAL-BINDING-ID"
                      (path + "/id")
                      $"'%s{id}' is not a stable binding identity."
                      "Use a letter-leading identity containing letters, digits, dots, or hyphens."
                      (Some source)

          if not (ProfileCore.safePath source.Path) then
              yield
                  ProfileCore.diagnostic
                      "QUINT-GENERAL-SOURCE-PATH"
                      (path + "/source/path")
                      "Source path is not a safe relative Markdown path."
                      "Use QuintSource's canonical Markdown path."
                      (Some source)

          if
              source.Start.Line < 1
              || source.Start.Column < 1
              || source.End.Line < source.Start.Line
              || (source.End.Line = source.Start.Line && source.End.Column < source.Start.Column)
          then
              yield
                  ProfileCore.diagnostic
                      "QUINT-GENERAL-SOURCE-RANGE"
                      (path + "/source")
                      "Source range is not positive and ordered."
                      "Use QuintSource's exact range."
                      (Some source) ]

    let private kindFields =
        Map.ofList
            [ "app", Set.ofList [ "args"; "id"; "kind"; "opcode" ]
              "arrow", Set.ofList [ "kind"; "params"; "result" ]
              "bool", Set.ofList [ "id"; "kind"; "value" ]
              "concrete", Set.ofList [ "components"; "kind"; "stateVariables" ]
              "const", Set.ofList [ "id"; "kind"; "name" ]
              "def",
              Set.ofList
                  [ "depth"
                    "expr"
                    "hidden"
                    "id"
                    "importedFrom"
                    "kind"
                    "name"
                    "qualifier"
                    "shadowing"
                    "typeAnnotation" ]
              "empty", Set.ofList [ "kind" ]
              "import", Set.ofList [ "defName"; "id"; "kind"; "protoName" ]
              "int", Set.ofList [ "id"; "kind"; "value" ]
              "lambda", Set.ofList [ "expr"; "id"; "kind"; "params"; "qualifier" ]
              "let", Set.ofList [ "expr"; "id"; "kind"; "opdef" ]
              "list", Set.ofList [ "elem"; "id"; "kind" ]
              "name", Set.ofList [ "id"; "kind"; "name" ]
              "oper", Set.ofList [ "args"; "kind"; "res" ]
              "param", Set.ofList [ "depth"; "id"; "kind"; "name"; "shadowing"; "typeAnnotation" ]
              "read", Set.ofList [ "entity"; "kind" ]
              "rec", Set.ofList [ "fields"; "id"; "kind" ]
              "row", Set.ofList [ "fields"; "kind"; "other" ]
              "set", Set.ofList [ "elem"; "id"; "kind" ]
              "str", Set.ofList [ "id"; "kind"; "value" ]
              "sum", Set.ofList [ "fields"; "id"; "kind" ]
              "temporal", Set.ofList [ "entity"; "kind" ]
              "tup", Set.ofList [ "fields"; "id"; "kind" ]
              "typedef", Set.ofList [ "depth"; "id"; "kind"; "name"; "type" ]
              "union", Set.ofList [ "entities"; "kind" ]
              "update", Set.ofList [ "entity"; "kind" ]
              "var", Set.ofList [ "depth"; "hidden"; "id"; "importedFrom"; "kind"; "name"; "typeAnnotation" ]
              "variable", Set.ofList [ "kind"; "name" ] ]

    let private untaggedFields =
        Set.ofList
            [ Set.ofList [ "id"; "name"; "declarations" ]
              Set.ofList [ "fieldName"; "fieldType" ]
              Set.ofList [ "id"; "name" ]
              Set.ofList [ "id"; "name"; "typeAnnotation" ]
              Set.ofList [ "name"; "reference" ]
              Set.ofList [ "rowVariables"; "type"; "typeVariables" ]
              Set.ofList [ "effect"; "effectVariables"; "entityVariables" ] ]

    let rec closedShape path (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.indexed
            |> Seq.collect (fun (index, item) -> closedShape $"%s{path}/%d{index}" item)
            |> Seq.toList
        | JsonValueKind.Object ->
            let properties = element.EnumerateObject() |> Seq.toList
            let names = properties |> List.map _.Name |> Set.ofList

            let own =
                match ProfileCore.tryProperty "kind" element with
                | Some kind when kind.ValueKind = JsonValueKind.String ->
                    let value = kind.GetString() |> Option.ofObj |> Option.defaultValue ""

                    match Map.tryFind value kindFields with
                    | Some allowed ->
                        Set.difference names allowed
                        |> Set.toList
                        |> List.map (fun name ->
                            ProfileCore.diagnostic
                                "QUINT-GENERAL-UNSUPPORTED-FIELD"
                                (path + "/" + name)
                                $"Field '%s{name}' is not part of the exact Quint 0.32.0 '%s{value}' shape."
                                "Use unmodified exact compiler output."
                                None)
                    | None ->
                        [ ProfileCore.diagnostic
                              "QUINT-GENERAL-UNSUPPORTED-KIND"
                              (path + "/kind")
                              $"Compiler kind '%s{value}' is outside the profile-2 boundary."
                              "Use a supported Quint 0.32.0 construct."
                              None ]
                | Some _ ->
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-IR-TYPE"
                          (path + "/kind")
                          "Compiler kind must be a string."
                          "Use unmodified exact compiler output."
                          None ]
                | None when properties |> List.forall (fun item -> Int64.TryParse(item.Name) |> fst) -> []
                | None when Set.contains names untaggedFields -> []
                | None ->
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-UNSUPPORTED-SHAPE"
                          path
                          "Object shape is outside the exact Quint 0.32.0 profile boundary."
                          "Use unmodified exact compiler output."
                          None ]

            own
            @ (properties
               |> List.collect (fun item -> closedShape (path + "/" + item.Name) item.Value))
        | _ -> []

    let valueNodeCount value =
        let rec count =
            function
            | QuintBool _
            | QuintInt _
            | QuintString _ -> 1
            | QuintTuple values
            | QuintList values
            | QuintSet values -> 1 + (values |> List.sumBy count)
            | QuintRecord fields -> 1 + (fields |> List.sumBy (snd >> count))
            | QuintVariant(_, value) -> 1 + (value |> Option.map count |> Option.defaultValue 0)
            | QuintMap entries -> 1 + (entries |> List.sumBy (fun (key, value) -> count key + count value))

        count value

    let sortKey value =
        let rec loop =
            function
            | QuintBool value -> if value then "b:1" else "b:0"
            | QuintInt value -> "i:" + value.ToString("+0000000000000000000;-0000000000000000000", CultureInfo.InvariantCulture)
            | QuintString value -> "s:" + value
            | QuintTuple values -> "t:[" + (values |> List.map loop |> String.concat ",") + "]"
            | QuintRecord fields ->
                "r:{" + (fields |> List.map (fun (name, item) -> name + "=" + loop item) |> String.concat ",") + "}"
            | QuintVariant(tag, value) -> "v:" + tag + ":" + (value |> Option.map loop |> Option.defaultValue "")
            | QuintList values -> "l:[" + (values |> List.map loop |> String.concat ",") + "]"
            | QuintSet values -> "e:[" + (values |> List.map loop |> String.concat ",") + "]"
            | QuintMap entries ->
                "m:[" + (entries |> List.map (fun (key, value) -> loop key + "=" + loop value) |> String.concat ",") + "]"

        loop value

    let stringValue path (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.String ->
            match element.GetString() |> Option.ofObj with
            | Some value when Encoding.UTF8.GetByteCount value > maxStringBytes ->
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-RESOURCE-STRING"
                          path
                          "Exported string exceeds 64 KiB."
                          "Reduce the exported string."
                          None ]
            | Some value -> Ok value
            | None ->
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-EXPORT-EXPRESSION"
                          path
                          "Expected a non-null string literal."
                          "Export only closed profile-2 values."
                          None ]
        | _ ->
            Error
                [ ProfileCore.diagnostic
                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                      path
                      "Expected a non-null string literal."
                      "Export only closed profile-2 values."
                      None ]

    let value path (root: JsonElement) =
        let mutable nodes = 0

        let rec parse depth path (element: JsonElement) =
            nodes <- nodes + 1

            if nodes > maxValueNodes then
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-RESOURCE-NODES"
                          path
                          "Exported values exceed 100,000 nodes."
                          "Reduce the exported value graph."
                          None ]
            elif depth > maxValueDepth then
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-RESOURCE-DEPTH"
                          path
                          "Exported value nesting exceeds depth 32."
                          "Flatten the exported value."
                          None ]
            else
                match ProfileCore.tryProperty "kind" element with
                | Some kindElement ->
                    match stringValue (path + "/kind") kindElement with
                    | Error errors -> Error errors
                    | Ok "bool" ->
                        match ProfileCore.tryProperty "value" element with
                        | Some value when value.ValueKind = JsonValueKind.True -> Ok(QuintBool true)
                        | Some value when value.ValueKind = JsonValueKind.False -> Ok(QuintBool false)
                        | _ ->
                            Error
                                [ ProfileCore.diagnostic
                                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                                      (path + "/value")
                                      "Boolean literal is absent."
                                      "Export only closed literals."
                                      None ]
                    | Ok "int" ->
                        match ProfileCore.tryProperty "value" element with
                        | Some value ->
                            match value.TryGetInt64() with
                            | true, number -> Ok(QuintInt number)
                            | _ ->
                                Error
                                    [ ProfileCore.diagnostic
                                          "QUINT-GENERAL-EXPORT-INTEGER"
                                          (path + "/value")
                                          "Integer is outside signed 64-bit bounds."
                                          "Use a bounded int64 export."
                                          None ]
                        | None ->
                            Error
                                [ ProfileCore.diagnostic
                                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                                      (path + "/value")
                                      "Integer literal is absent."
                                      "Export only closed literals."
                                      None ]
                    | Ok "str" ->
                        match ProfileCore.tryProperty "value" element with
                        | Some value -> stringValue (path + "/value") value |> Result.map QuintString
                        | None ->
                            Error
                                [ ProfileCore.diagnostic
                                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                                      (path + "/value")
                                      "String literal is absent."
                                      "Export only closed literals."
                                      None ]
                    | Ok "app" ->
                        match ProfileCore.tryProperty "opcode" element, ProfileCore.tryProperty "args" element with
                        | Some opcodeElement, Some argsElement when argsElement.ValueKind = JsonValueKind.Array ->
                            match stringValue (path + "/opcode") opcodeElement with
                            | Error errors -> Error errors
                            | Ok opcode ->
                                let args = argsElement.EnumerateArray() |> Seq.toList

                                let parseAll offset items =
                                    items
                                    |> List.mapi (fun index item -> parse (depth + 1) ($"%s{path}/args/%d{index + offset}") item)
                                    |> List.fold
                                        (fun state item ->
                                            match state, item with
                                            | Ok values, Ok value -> Ok(value :: values)
                                            | Error left, Error right -> Error(left @ right)
                                            | Error errors, _
                                            | _, Error errors -> Error errors)
                                        (Ok [])
                                    |> Result.map List.rev

                                match opcode with
                                | "List" -> parseAll 0 args |> Result.map QuintList
                                | "Set" ->
                                    parseAll 0 args
                                    |> Result.map (List.sortBy sortKey >> List.distinct >> QuintSet)
                                | "Tup" -> parseAll 0 args |> Result.map QuintTuple
                                | "Rec" when args.Length % 2 = 0 ->
                                    args
                                    |> List.chunkBySize 2
                                    |> List.mapi (fun index pair ->
                                        match ProfileCore.tryProperty "value" pair[0] with
                                        | Some key ->
                                            match stringValue ($"%s{path}/args/%d{index * 2}/value") key with
                                            | Ok name ->
                                                parse (depth + 1) ($"%s{path}/args/%d{index * 2 + 1}") pair[1]
                                                |> Result.map (fun value -> name, value)
                                            | Error errors -> Error errors
                                        | None ->
                                            Error
                                                [ ProfileCore.diagnostic
                                                      "QUINT-GENERAL-EXPORT-RECORD"
                                                      ($"%s{path}/args/%d{index * 2}")
                                                      "Record key is not a string literal."
                                                      "Use closed record field names."
                                                      None ])
                                    |> List.fold
                                        (fun state item ->
                                            match state, item with
                                            | Ok values, Ok value -> Ok(value :: values)
                                            | Error left, Error right -> Error(left @ right)
                                            | Error errors, _
                                            | _, Error errors -> Error errors)
                                        (Ok [])
                                    |> Result.bind (fun fields ->
                                        let fields = List.rev fields
                                        let names = List.map fst fields

                                        if names.Length <> (names |> List.distinct |> List.length) then
                                            Error
                                                [ ProfileCore.diagnostic
                                                      "QUINT-GENERAL-EXPORT-RECORD-DUPLICATE"
                                                      path
                                                      "Record field is duplicated."
                                                      "Declare each record field once."
                                                      None ]
                                        else
                                            fields |> List.sortBy fst |> QuintRecord |> Ok)
                                | "variant" when args.Length = 2 ->
                                    match ProfileCore.tryProperty "value" args[0] with
                                    | Some tagElement ->
                                        match stringValue (path + "/args/0/value") tagElement with
                                        | Error errors -> Error errors
                                        | Ok tag ->
                                            parse (depth + 1) (path + "/args/1") args[1]
                                            |> Result.map (fun value ->
                                                match value with
                                                | QuintTuple [] -> QuintVariant(tag, None)
                                                | value -> QuintVariant(tag, Some value))
                                    | None ->
                                        Error
                                            [ ProfileCore.diagnostic
                                                  "QUINT-GENERAL-EXPORT-VARIANT"
                                                  (path + "/args/0")
                                                  "Variant tag is not a string literal."
                                                  "Use a closed variant tag."
                                                  None ]
                                | "Map" when args.Length % 2 = 0 ->
                                    parseAll 0 args
                                    |> Result.bind (fun values ->
                                        values
                                        |> List.chunkBySize 2
                                        |> List.map (fun pair -> pair[0], pair[1])
                                        |> List.sortBy (fst >> sortKey)
                                        |> fun entries ->
                                            let duplicateKeys =
                                                entries
                                                |> List.groupBy (fst >> sortKey)
                                                |> List.exists (fun (_, rows) -> rows.Length > 1)

                                            if duplicateKeys then
                                                Error
                                                    [ ProfileCore.diagnostic
                                                          "QUINT-GENERAL-EXPORT-MAP-DUPLICATE"
                                                          path
                                                          "Map contains a duplicate canonical key."
                                                          "Declare each map key once."
                                                          None ]
                                            else
                                                Ok(QuintMap entries))
                                | _ ->
                                    Error
                                        [ ProfileCore.diagnostic
                                              "QUINT-GENERAL-EXPORT-EXPRESSION"
                                              (path + "/opcode")
                                              $"Opcode '%s{opcode}' is not an exportable constant."
                                              "Export only closed profile-2 values."
                                              None ]
                        | _ ->
                            Error
                                [ ProfileCore.diagnostic
                                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                                      path
                                      "Application opcode or args are malformed."
                                      "Use exact Quint 0.32.0 output."
                                      None ]
                    | Ok kind ->
                        Error
                            [ ProfileCore.diagnostic
                                  "QUINT-GENERAL-EXPORT-EXPRESSION"
                                  (path + "/kind")
                                  $"Expression kind '%s{kind}' is not exportable."
                                  "Export only closed profile-2 values."
                                  None ]
                | None ->
                    Error
                        [ ProfileCore.diagnostic
                              "QUINT-GENERAL-EXPORT-EXPRESSION"
                              (path + "/kind")
                              "Expression kind is absent."
                              "Use exact Quint 0.32.0 output."
                              None ]

        parse 0 path root

    let recordField name =
        function
        | QuintRecord fields -> fields |> List.tryFind (fst >> (=) name) |> Option.map snd
        | _ -> None

    let promote (binding: QuintGeneralExportBinding) value =
        let rows =
            match value with
            | QuintList rows
            | QuintSet rows -> Ok rows
            | _ ->
                Error
                    [ ProfileCore.diagnostic
                          "QUINT-GENERAL-CATALOGUE-SHAPE"
                          "/exportBindings"
                          $"Export '%s{binding.Id}' must be a list or set to promote catalogue rows."
                          "Export records carrying string id and kind fields."
                          (sourceOfExport binding) ]

        rows
        |> Result.bind (fun rows ->
            rows
            |> List.mapi (fun index row ->
                match recordField "id" row, recordField "kind" row with
                | Some(QuintString id), Some(QuintString kind) when ProfileCore.validId.IsMatch id ->
                    Ok
                        { Id = id
                          Kind = kind
                          ExportId = binding.Id
                          Value = row
                          Source = binding.Source }
                | _ ->
                    Error
                        [ ProfileCore.diagnostic
                              "QUINT-GENERAL-CATALOGUE-ROW"
                              ($"/exports/%s{binding.Id}/%d{index}")
                              "Promoted row requires a stable string id and non-null string kind."
                              "Add id and kind fields to the exported record."
                              (sourceOfExport binding) ])
            |> List.fold
                (fun state item ->
                    match state, item with
                    | Ok rows, Ok row -> Ok(row :: rows)
                    | Error left, Error right -> Error(left @ right)
                    | Error errors, _
                    | _, Error errors -> Error errors)
                (Ok [])
            |> Result.map (List.sortBy _.Id))

    let rec stateVariables (element: JsonElement) =
        [ if element.ValueKind = JsonValueKind.Object then
              match ProfileCore.tryProperty "stateVariables" element with
              | Some values when values.ValueKind = JsonValueKind.Array ->
                  for value in values.EnumerateArray() do
                      match ProfileCore.tryProperty "name" value with
                      | Some name ->
                          match stringValue "/effects/stateVariables/name" name with
                          | Ok text -> yield text
                          | _ -> ()
                      | None -> ()
              | _ -> ()

              for property in element.EnumerateObject() do
                  yield! stateVariables property.Value
          elif element.ValueKind = JsonValueKind.Array then
              for item in element.EnumerateArray() do
                  yield! stateVariables item ]
        |> List.distinct
        |> List.sort

    let actionEffect (effects: JsonElement) (binding: QuintCatalogueSourceBinding) declarationId =
        match ProfileCore.tryProperty (string declarationId) effects with
        | None ->
            Error
                [ ProfileCore.diagnostic
                      "QUINT-GENERAL-ACTION-EFFECT"
                      "/effects"
                      $"Action '%s{binding.Id}' has no exact effect row."
                      "Use typecheck output from the same source."
                      (sourceOfAction binding) ]
        | Some row ->
            let reads =
                [ match ProfileCore.tryProperty "effect" row with
                  | Some effect ->
                      let rec components (element: JsonElement) =
                          [ if element.ValueKind = JsonValueKind.Object then
                                match ProfileCore.tryProperty "kind" element with
                                | Some kind ->
                                    match stringValue "/effects/kind" kind with
                                    | Ok "read" -> yield! stateVariables element
                                    | _ -> ()
                                | None -> ()

                                for property in element.EnumerateObject() do
                                    yield! components property.Value
                            elif element.ValueKind = JsonValueKind.Array then
                                for item in element.EnumerateArray() do
                                    yield! components item ]

                      yield! components effect
                  | None -> () ]
                |> List.distinct
                |> List.sort

            let writes =
                [ match ProfileCore.tryProperty "effect" row with
                  | Some effect ->
                      let rec components (element: JsonElement) =
                          [ if element.ValueKind = JsonValueKind.Object then
                                match ProfileCore.tryProperty "kind" element with
                                | Some kind ->
                                    match stringValue "/effects/kind" kind with
                                    | Ok "update" -> yield! stateVariables element
                                    | _ -> ()
                                | None -> ()

                                for property in element.EnumerateObject() do
                                    yield! components property.Value
                            elif element.ValueKind = JsonValueKind.Array then
                                for item in element.EnumerateArray() do
                                    yield! components item ]

                      yield! components effect
                  | None -> () ]
                |> List.distinct
                |> List.sort

            Ok
                { ActionId = binding.Id
                  Reads = reads
                  Writes = writes
                  Subjects = [] }

[<RequireQualifiedAccess>]
module QuintGeneralProfile =
    let identity = GeneralProfileCore.profile
    let quintVersion = GeneralProfileCore.version

    let adaptTypedEffectJson (observation: QuintGeneralTypedEffectObservation) =
        let mutable findings = []

        findings <-
            (observation.ExportBindings
             |> List.indexed
             |> List.collect (fun (index, binding) ->
                 GeneralProfileCore.sourceFindings $"/exportBindings/%d{index}" binding.Id binding.Source))
            @ (observation.ActionBindings
               |> List.indexed
               |> List.collect (fun (index, binding) ->
                   GeneralProfileCore.sourceFindings $"/actionBindings/%d{index}" binding.Id binding.Source))

        if observation.Profile <> identity then
            findings <-
                ProfileCore.diagnostic
                    "QUINT-PROFILE-IDENTITY"
                    "/profile"
                    $"Expected '%s{identity}', got '%s{observation.Profile}'."
                    "Select the explicit general profile."
                    None
                :: findings

        if observation.QuintVersion <> quintVersion then
            findings <-
                ProfileCore.diagnostic
                    "QUINT-PROFILE-VERSION"
                    "/quintVersion"
                    $"Expected Quint %s{quintVersion}, got '%s{observation.QuintVersion}'."
                    "Use the pinned compiler."
                    None
                :: findings

        if Encoding.UTF8.GetByteCount observation.TypedEffectJson > GeneralProfileCore.maxTypedEffectBytes then
            findings <-
                ProfileCore.diagnostic
                    "QUINT-GENERAL-RESOURCE-BYTES"
                    "/"
                    "Typed/effect JSON exceeds 16 MiB."
                    "Reduce the model or split the authority."
                    None
                :: findings

        if observation.ExportBindings.Length > GeneralProfileCore.maxExports then
            findings <-
                ProfileCore.diagnostic
                    "QUINT-GENERAL-RESOURCE-EXPORTS"
                    "/exportBindings"
                    "More than 256 exports were declared."
                    "Reduce the exported declaration set."
                    None
                :: findings

        if observation.ActionBindings.Length > GeneralProfileCore.maxBindings then
            findings <-
                ProfileCore.diagnostic
                    "QUINT-GENERAL-RESOURCE-BINDINGS"
                    "/actionBindings"
                    "More than 4,096 action bindings were declared."
                    "Reduce the action binding set."
                    None
                :: findings

        try
            use document = JsonDocument.Parse observation.TypedEffectJson
            let root = document.RootElement

            let rootFindings =
                ProfileCore.fields
                    "/"
                    (Set.ofList [ "stage"; "modules"; "table"; "types"; "effects"; "errors"; "warnings" ])
                    (Set.ofList [ "stage"; "modules"; "table"; "types"; "effects"; "errors"; "warnings" ])
                    root

            findings <- rootFindings @ findings

            match ProfileCore.tryProperty "stage" root with
            | Some stage ->
                match GeneralProfileCore.stringValue "/stage" stage with
                | Ok "typechecking" -> ()
                | _ ->
                    findings <-
                        ProfileCore.diagnostic
                            "QUINT-GENERAL-STAGE"
                            "/stage"
                            "Expected completed typechecking output."
                            "Run Quint 0.32.0 typecheck --out."
                            None
                        :: findings
            | None -> ()

            for field in [ "errors"; "warnings" ] do
                match ProfileCore.tryProperty field root with
                | Some values when values.ValueKind = JsonValueKind.Array && values.GetArrayLength() = 0 -> ()
                | Some _ ->
                    findings <-
                        ProfileCore.diagnostic
                            (if field = "errors" then
                                 "QUINT-GENERAL-COMPILER-ERRORS"
                             else
                                 "QUINT-GENERAL-COMPILER-WARNINGS")
                            ("/" + field)
                            $"Compiler output contains %s{field}."
                            "Correct the model before compilation."
                            None
                        :: findings
                | None -> ()

            for field in [ "modules"; "table"; "types"; "effects" ] do
                match ProfileCore.tryProperty field root with
                | Some value -> findings <- GeneralProfileCore.closedShape ("/" + field) value @ findings
                | None -> ()

            for field in [ "table"; "types"; "effects" ] do
                match ProfileCore.tryProperty field root with
                | Some value ->
                    findings <- ProfileCore.numericTable ("/" + field) value @ findings

                    if
                        value.ValueKind = JsonValueKind.Object
                        && (value.EnumerateObject() |> Seq.length) > GeneralProfileCore.maxDeclarations
                    then
                        findings <-
                            ProfileCore.diagnostic
                                "QUINT-GENERAL-RESOURCE-TABLE"
                                ("/" + field)
                                $"Compiler table '%s{field}' exceeds 4,096 rows."
                                "Reduce the model declaration and expression count."
                                None
                            :: findings
                | None -> ()

            match ProfileCore.tryProperty "types" root, ProfileCore.tryProperty "effects" root with
            | Some types, Some effects when ProfileCore.numericKeys types <> ProfileCore.numericKeys effects ->
                findings <-
                    ProfileCore.diagnostic
                        "QUINT-GENERAL-EFFECT-TYPE-COVERAGE"
                        "/effects"
                        "Compiler type and effect tables do not cover the same node identities."
                        "Use one complete Quint 0.32.0 typecheck observation."
                        None
                    :: findings
            | _ -> ()

            let declarations =
                match ProfileCore.tryProperty "modules" root with
                | Some modules when modules.ValueKind = JsonValueKind.Array ->
                    [ for moduleElement in modules.EnumerateArray() do
                          match
                              ProfileCore.tryProperty "name" moduleElement,
                              ProfileCore.tryProperty "declarations" moduleElement
                          with
                          | Some name, Some values when values.ValueKind = JsonValueKind.Array ->
                              match GeneralProfileCore.stringValue "/modules/name" name with
                              | Ok moduleName ->
                                  for declaration in values.EnumerateArray() do
                                      yield moduleName, declaration.Clone()
                              | Error errors -> findings <- errors @ findings
                          | _ -> () ]
                | _ -> []

            if declarations.Length > GeneralProfileCore.maxDeclarations then
                findings <-
                    ProfileCore.diagnostic
                        "QUINT-GENERAL-RESOURCE-DECLARATIONS"
                        "/modules"
                        "More than 4,096 declarations were emitted."
                        "Reduce the model declaration count."
                        None
                    :: findings

            let namedDeclarations =
                declarations
                |> List.choose (fun (moduleName, declaration) ->
                    match ProfileCore.tryProperty "name" declaration with
                    | Some name ->
                        match GeneralProfileCore.stringValue "/modules/declarations/name" name with
                        | Ok declarationName -> Some((moduleName, declarationName), declaration)
                        | _ -> None
                    | None -> None)
                |> List.groupBy fst
                |> Map.ofList

            let resolve source key =
                match Map.tryFind key namedDeclarations with
                | Some [ _, declaration ] -> Ok declaration
                | Some _ ->
                    Error
                        [ ProfileCore.diagnostic
                              "QUINT-GENERAL-DECLARATION-DUPLICATE"
                              "/modules"
                              $"Declaration '%s{fst key}.%s{snd key}' is duplicated."
                              "Declare the selected name once."
                              source ]
                | None ->
                    Error
                        [ ProfileCore.diagnostic
                              "QUINT-GENERAL-DECLARATION-MISSING"
                              "/modules"
                              $"Declaration '%s{fst key}.%s{snd key}' is absent."
                              "Bind an exported declaration from the same source."
                              source ]

            let exports =
                observation.ExportBindings
                |> List.map (fun binding ->
                    resolve (GeneralProfileCore.sourceOfExport binding) (binding.ModuleName, binding.DeclarationName)
                    |> Result.bind (fun declaration ->
                        match ProfileCore.tryProperty "qualifier" declaration, ProfileCore.tryProperty "expr" declaration with
                        | Some qualifier, Some expression ->
                            match GeneralProfileCore.stringValue "/modules/declarations/qualifier" qualifier with
                            | Ok("pureval" | "val") ->
                                GeneralProfileCore.value "/modules/declarations/expr" expression
                                |> Result.map (fun value ->
                                    { Id = binding.Id
                                      ModuleName = binding.ModuleName
                                      DeclarationName = binding.DeclarationName
                                      Value = value
                                      Source = binding.Source })
                            | _ ->
                                Error
                                    [ ProfileCore.diagnostic
                                          "QUINT-GENERAL-EXPORT-EXPRESSION"
                                          "/modules/declarations/qualifier"
                                          $"Export '%s{binding.Id}' is not a value declaration."
                                          "Export only val or pure val declarations."
                                          (GeneralProfileCore.sourceOfExport binding) ]
                        | _ ->
                            Error
                                [ ProfileCore.diagnostic
                                      "QUINT-GENERAL-EXPORT-EXPRESSION"
                                      "/modules/declarations"
                                      $"Export '%s{binding.Id}' has no closed value expression."
                                      "Export only value declarations."
                                      (GeneralProfileCore.sourceOfExport binding) ]))

            let acceptedExports: QuintGeneralExport list =
                exports |> List.choose (function Ok value -> Some value | _ -> None) |> List.sortBy _.Id
            findings <- exports |> List.collect (function Error errors -> errors | _ -> []) |> (@) findings

            if (acceptedExports |> List.sumBy (_.Value >> GeneralProfileCore.valueNodeCount)) > GeneralProfileCore.maxValueNodes then
                findings <-
                    ProfileCore.diagnostic
                        "QUINT-GENERAL-RESOURCE-NODES"
                        "/exports"
                        "Exported values exceed 100,000 aggregate nodes."
                        "Reduce the exported value graph."
                        None
                    :: findings

            let catalogue =
                observation.ExportBindings
                |> List.choose (fun (binding: QuintGeneralExportBinding) ->
                    if binding.PromoteCatalogueRows then
                        acceptedExports
                        |> List.tryFind (fun item -> item.Id = binding.Id)
                        |> Option.map (fun item -> GeneralProfileCore.promote binding item.Value)
                    else
                        None)

            let acceptedCatalogue =
                catalogue |> List.collect (function Ok values -> values | _ -> []) |> List.sortBy _.Id

            findings <- catalogue |> List.collect (function Error errors -> errors | _ -> []) |> (@) findings

            let effectsElement = ProfileCore.tryProperty "effects" root

            let actionEffects =
                observation.ActionBindings
                |> List.map (fun binding ->
                    if binding.Kind <> Action then
                        Error
                            [ ProfileCore.diagnostic
                                  "QUINT-GENERAL-ACTION-BINDING"
                                  "/actionBindings"
                                  $"Binding '%s{binding.Id}' is not an action."
                                  "Use Action kind for action bindings."
                                  (GeneralProfileCore.sourceOfAction binding) ]
                    else
                        resolve (GeneralProfileCore.sourceOfAction binding) (binding.ModuleName, binding.CatalogueName)
                        |> Result.bind (fun declaration ->
                            match
                                ProfileCore.tryProperty "qualifier" declaration,
                                ProfileCore.tryProperty "id" declaration,
                                effectsElement
                            with
                            | Some qualifier, Some id, Some effects ->
                                match GeneralProfileCore.stringValue "/modules/declarations/qualifier" qualifier, id.TryGetInt64() with
                                | Ok "action", (true, declarationId) ->
                                    GeneralProfileCore.actionEffect effects binding declarationId
                                | Ok _, _ ->
                                    Error
                                        [ ProfileCore.diagnostic
                                              "QUINT-GENERAL-ACTION-BINDING"
                                              "/modules/declarations/qualifier"
                                              $"Binding '%s{binding.Id}' does not select an action declaration."
                                              "Bind a declaration with the action qualifier."
                                              (GeneralProfileCore.sourceOfAction binding) ]
                                | _ ->
                                    Error
                                        [ ProfileCore.diagnostic
                                              "QUINT-GENERAL-ACTION-EFFECT"
                                              "/modules/declarations/id"
                                              "Action declaration id is malformed."
                                              "Use exact compiler output."
                                              (GeneralProfileCore.sourceOfAction binding) ]
                            | _ ->
                                Error
                                    [ ProfileCore.diagnostic
                                          "QUINT-GENERAL-ACTION-EFFECT"
                                          "/effects"
                                          "Action effect table is absent."
                                          "Use complete typecheck output."
                                          (GeneralProfileCore.sourceOfAction binding) ]))

            let acceptedEffects =
                actionEffects |> List.choose (function Ok value -> Some value | _ -> None) |> List.sortBy _.ActionId

            findings <- actionEffects |> List.collect (function Error errors -> errors | _ -> []) |> (@) findings

            let duplicate ids code path label =
                ids
                |> List.groupBy id
                |> List.choose (fun (id, rows) ->
                    if rows.Length > 1 then
                        Some(ProfileCore.diagnostic code path $"%s{label} '%s{id}' is duplicated." "Use each identity once." None)
                    else
                        None)

            findings <-
                duplicate (acceptedExports |> List.map _.Id) "QUINT-GENERAL-EXPORT-DUPLICATE" "/exports" "Export"
                @ duplicate (acceptedCatalogue |> List.map _.Id) "QUINT-GENERAL-CATALOGUE-DUPLICATE" "/catalogue" "Catalogue identity"
                @ duplicate (acceptedEffects |> List.map _.ActionId) "QUINT-GENERAL-ACTION-DUPLICATE" "/actionEffects" "Action"
                @ findings

            let all = ProfileCore.sorted findings

            if List.isEmpty all then
                Ok
                    { Profile = observation.Profile
                      QuintVersion = observation.QuintVersion
                      Exports = acceptedExports
                      Catalogue = acceptedCatalogue
                      ActionEffects = acceptedEffects }
            else
                Error all
        with :? JsonException as ex ->
            Error
                [ ProfileCore.diagnostic
                      "QUINT-IR-MALFORMED"
                      "/"
                      ex.Message
                      "Use valid exact typecheck --out JSON."
                      None ]

[<RequireQualifiedAccess>]
module QuintGeneralBindingManifest =
    let schema = "fsgg.quint.general-bindings/v1"

    let private findings (manifest: QuintGeneralBindingManifest) =
        [ if manifest.Schema <> schema then
              yield
                  ProfileCore.diagnostic
                      "QUINT-GENERAL-BINDINGS-SCHEMA"
                      "/schema"
                      $"Expected '%s{schema}', got '%s{manifest.Schema}'."
                      "Use the profile-2 binding manifest schema."
                      None
          if manifest.Profile <> QuintGeneralProfile.identity then
              yield
                  ProfileCore.diagnostic
                      "QUINT-PROFILE-IDENTITY"
                      "/profile"
                      $"Expected '%s{QuintGeneralProfile.identity}', got '%s{manifest.Profile}'."
                      "Select the explicit general profile."
                      None
          if String.IsNullOrWhiteSpace manifest.ModuleName then
              yield
                  ProfileCore.diagnostic
                      "QUINT-GENERAL-BINDINGS-MODULE"
                      "/moduleName"
                      "Generated binding module name is absent."
                      "Retain the exact generated module name."
                      None
          yield!
              manifest.Exports
              |> List.indexed
              |> List.collect (fun (index, binding) ->
                  GeneralProfileCore.sourceFindings $"/exports/%d{index}" binding.Id binding.Source)
          yield!
              manifest.Actions
              |> List.indexed
              |> List.collect (fun (index, binding) ->
                  GeneralProfileCore.sourceFindings $"/actions/%d{index}" binding.Id binding.Source)
          for id, rows in manifest.Exports |> List.groupBy _.Id do
              if rows.Length > 1 then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-GENERAL-EXPORT-DUPLICATE"
                          "/exports"
                          $"Export '%s{id}' is duplicated."
                          "Declare each export once."
                          None
          for id, rows in manifest.Actions |> List.groupBy _.Id do
              if rows.Length > 1 then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-GENERAL-ACTION-DUPLICATE"
                          "/actions"
                          $"Action '%s{id}' is duplicated."
                          "Declare each action once."
                          None
          for binding in manifest.Actions do
              if binding.Kind <> Action then
                  yield
                      ProfileCore.diagnostic
                          "QUINT-GENERAL-ACTION-BINDING"
                          "/actions"
                          $"Binding '%s{binding.Id}' is not an action."
                          "Use Action kind for action bindings."
                          (Some binding.Source) ]
        |> ProfileCore.sorted

    let private writePosition (writer: Utf8JsonWriter) (name: string) (position: QuintSourcePosition) =
        writer.WriteStartObject(name)
        writer.WriteNumber("line", position.Line)
        writer.WriteNumber("column", position.Column)
        writer.WriteEndObject()

    let private writeSource (writer: Utf8JsonWriter) (source: QuintSourceRange) =
        writer.WriteStartObject("source")
        writer.WriteString("path", source.Path)
        writePosition writer "start" source.Start
        writePosition writer "end" source.End
        writer.WriteEndObject()

    let serializeCanonical manifest =
        match findings manifest with
        | errors when not (List.isEmpty errors) -> Error errors
        | _ ->
            use stream = new IO.MemoryStream()
            use writer = new Utf8JsonWriter(stream)
            writer.WriteStartObject()
            writer.WriteString("schema", manifest.Schema)
            writer.WriteString("profile", manifest.Profile)
            writer.WriteString("moduleName", manifest.ModuleName)
            writer.WriteStartArray("exports")

            manifest.Exports
            |> List.sortBy _.Id
            |> List.iter (fun binding ->
                writer.WriteStartObject()
                writer.WriteString("id", binding.Id)
                writer.WriteString("module", binding.ModuleName)
                writer.WriteString("declaration", binding.DeclarationName)
                writer.WriteBoolean("promoteCatalogueRows", binding.PromoteCatalogueRows)
                writeSource writer binding.Source
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteStartArray("actions")

            manifest.Actions
            |> List.sortBy _.Id
            |> List.iter (fun binding ->
                writer.WriteStartObject()
                writer.WriteString("id", binding.Id)
                writer.WriteString("module", binding.ModuleName)
                writer.WriteString("declaration", binding.CatalogueName)
                writeSource writer binding.Source
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteEndObject()
            writer.Flush()
            Ok(Encoding.UTF8.GetString(stream.ToArray()) + "\n")

    let deserialize (text: string) =
        let malformed message =
            Error
                [ ProfileCore.diagnostic
                      "QUINT-GENERAL-BINDINGS-MALFORMED"
                      "/"
                      message
                      "Use canonical profile-2 binding manifest JSON."
                      None ]

        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            let exact path expected (element: JsonElement) =
                if element.ValueKind <> JsonValueKind.Object then
                    raise (JsonException($"%s{path}: expected object."))

                let actual = element.EnumerateObject() |> Seq.map _.Name |> Set.ofSeq
                if actual <> expected then raise (JsonException($"%s{path}: fields do not match schema."))

            let string (name: string) (element: JsonElement) =
                let value = element.GetProperty(name)
                if value.ValueKind <> JsonValueKind.String then raise (JsonException($"/%s{name}: expected string."))
                value.GetString() |> Option.ofObj |> Option.defaultWith (fun () -> raise (JsonException("null string")))

            let position path (element: JsonElement) =
                exact path (Set.ofList [ "line"; "column" ]) element
                { Line = element.GetProperty("line").GetInt32()
                  Column = element.GetProperty("column").GetInt32() }

            let source path (element: JsonElement) =
                exact path (Set.ofList [ "path"; "start"; "end" ]) element
                { Path = string "path" element
                  Start = position (path + "/start") (element.GetProperty("start"))
                  End = position (path + "/end") (element.GetProperty("end")) }

            let array (name: string) (element: JsonElement) =
                let value = element.GetProperty(name)
                if value.ValueKind <> JsonValueKind.Array then raise (JsonException($"/%s{name}: expected array."))
                value.EnumerateArray() |> Seq.toList

            exact "/" (Set.ofList [ "schema"; "profile"; "moduleName"; "exports"; "actions" ]) root

            let exports =
                array "exports" root
                |> List.mapi (fun index element ->
                    let path = $"/exports/%d{index}"
                    exact path (Set.ofList [ "id"; "module"; "declaration"; "promoteCatalogueRows"; "source" ]) element
                    let promote = element.GetProperty("promoteCatalogueRows")
                    if promote.ValueKind <> JsonValueKind.True && promote.ValueKind <> JsonValueKind.False then
                        raise (JsonException(path + "/promoteCatalogueRows: expected boolean."))
                    { Id = string "id" element
                      ModuleName = string "module" element
                      DeclarationName = string "declaration" element
                      PromoteCatalogueRows = promote.GetBoolean()
                      Source = source (path + "/source") (element.GetProperty("source")) })

            let actions =
                array "actions" root
                |> List.mapi (fun index element ->
                    let path = $"/actions/%d{index}"
                    exact path (Set.ofList [ "id"; "module"; "declaration"; "source" ]) element
                    { ModuleName = string "module" element
                      CatalogueName = string "declaration" element
                      Id = string "id" element
                      Kind = Action
                      Source = source (path + "/source") (element.GetProperty("source")) })

            let manifest =
                { Schema = string "schema" root
                  Profile = string "profile" root
                  ModuleName = string "moduleName" root
                  Exports = exports
                  Actions = actions }

            match findings manifest with
            | [] -> Ok manifest
            | errors -> Error errors
        with
        | :? JsonException as ex -> malformed ex.Message
        | :? InvalidOperationException as ex -> malformed ex.Message
