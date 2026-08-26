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
