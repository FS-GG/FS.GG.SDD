namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Buffers.Binary
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

type QuintRelationshipKind =
    | Requires
    | VerifiedBy
    | ImplementedBy
    | Reads
    | Writes

type QuintRelationship =
    { FromId: string
      Kind: QuintRelationshipKind
      ToId: string }

type QuintVerificationProfile =
    { Id: string
      Kind: string
      SubjectIds: string list
      BoundIds: string list }

type QuintFiniteBound =
    { Id: string
      Minimum: int64
      Maximum: int64 }

type QuintImpact =
    { SubjectId: string
      Category: string
      Detail: string }

type QuintCompatibility =
    { Surface: string
      Requirement: string
      Detail: string }

type QuintSemanticDigest = { Name: string; Sha256: string }

type QuintCompiledContract =
    { Schema: string
      Profile: string
      Specification: string
      Catalogue: QuintCatalogueEntry list
      ActionEffects: QuintActionEffect list
      Relationships: QuintRelationship list
      VerificationProfiles: QuintVerificationProfile list
      Bounds: QuintFiniteBound list
      Impacts: QuintImpact list
      Compatibility: QuintCompatibility list
      Digests: QuintSemanticDigest list }

type QuintFingerprintInputs =
    { SourceSha256: string
      FenceManifestSha256: string
      GeneratedModulesSha256: string
      ToolchainSha256: string
      Contract: QuintCompiledContract }

type QuintContractDiagnostic =
    { Code: string
      Path: string
      Message: string
      Correction: string }

type QuintContractChange =
    { Path: string
      BeforeSha256: string
      AfterSha256: string }

type QuintContractDiff =
    | Equivalent
    | Changed of QuintContractChange list

module private ContractCore =
    let schema = "fsgg.quint.compiled-contract/v1"
    let digestPattern = new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)

    let idPattern =
        new Regex("^[A-Z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$", RegexOptions.CultureInvariant)

    let diagnostic code path message correction : QuintContractDiagnostic =
        { Code = code
          Path = path
          Message = message
          Correction = correction }

    let sorted (findings: QuintContractDiagnostic list) =
        findings
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

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

    let relationText =
        function
        | Requires -> "requires"
        | VerifiedBy -> "verifiedBy"
        | ImplementedBy -> "implementedBy"
        | Reads -> "reads"
        | Writes -> "writes"

    let parseKind path =
        function
        | "requirement" -> Requirement
        | "stateVariable" -> StateVariable
        | "action" -> QuintCatalogueKind.Action
        | "invariant" -> Invariant
        | "temporalProperty" -> TemporalProperty
        | "reachabilityProperty" -> ReachabilityProperty
        | "evidence" -> Evidence
        | "implementation" -> Implementation
        | "externalSubject" -> ExternalSubject
        | value -> raise (JsonException($"%s{path}: unsupported catalogue kind '%s{value}'."))

    let parseRelation path =
        function
        | "requires" -> Requires
        | "verifiedBy" -> VerifiedBy
        | "implementedBy" -> ImplementedBy
        | "reads" -> Reads
        | "writes" -> Writes
        | value -> raise (JsonException($"%s{path}: unsupported relationship kind '%s{value}'."))

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture))
        |> String.concat ""

    let sha256Text (value: string) =
        value |> Encoding.UTF8.GetBytes |> sha256

    let normalizeDigest (value: string) =
        if value.StartsWith("sha256:", StringComparison.Ordinal) then
            value.Substring(7)
        else
            value

    let validDigest value =
        digestPattern.IsMatch(normalizeDigest value)

    let validate contract =
        let catalogueIds = contract.Catalogue |> List.map _.Id |> Set.ofList
        let boundIds = contract.Bounds |> List.map _.Id |> Set.ofList

        [ if contract.Schema <> schema then
              yield
                  diagnostic
                      "QUINT-CONTRACT-SCHEMA"
                      "/schema"
                      $"Expected '%s{schema}', got '%s{contract.Schema}'."
                      "Use compiled-contract v1."
          if contract.Profile <> QuintProfile.identity then
              yield
                  diagnostic
                      "QUINT-CONTRACT-PROFILE"
                      "/profile"
                      $"Expected '%s{QuintProfile.identity}', got '%s{contract.Profile}'."
                      "Compile with the exact profile 1 adapter."
          if not (idPattern.IsMatch contract.Specification) then
              yield
                  diagnostic
                      "QUINT-CONTRACT-ID"
                      "/specification"
                      "Specification is not a stable identity."
                      "Use an uppercase-leading explicit identity."
          if List.isEmpty contract.Catalogue then
              yield
                  diagnostic
                      "QUINT-CONTRACT-CATALOGUE-EMPTY"
                      "/catalogue"
                      "The catalogue is empty."
                      "Declare all stable integration identities."
          for (kind, id), rows in contract.Catalogue |> List.groupBy (fun row -> row.Kind, row.Id) do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-CATALOGUE-DUPLICATE"
                          "/catalogue"
                          $"Catalogue identity '%s{kindText kind}:%s{id}' occurs more than once."
                          "Keep one row per (kind,id)."
          for id, rows in contract.ActionEffects |> List.groupBy _.ActionId do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-EFFECT-DUPLICATE"
                          "/actionEffects"
                          $"Action '%s{id}' has multiple effect rows."
                          "Keep one effect row per action."
          for index, relation in contract.Relationships |> List.indexed do
              if not (catalogueIds.Contains relation.FromId) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-REFERENCE"
                          $"/relationships/%d{index}/from"
                          $"'%s{relation.FromId}' is not declared."
                          "Reference a catalogue identity."

              if not (catalogueIds.Contains relation.ToId) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-REFERENCE"
                          $"/relationships/%d{index}/to"
                          $"'%s{relation.ToId}' is not declared."
                          "Reference a catalogue identity."
          for index, profile in contract.VerificationProfiles |> List.indexed do
              if not (idPattern.IsMatch profile.Id) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-ID"
                          $"/verificationProfiles/%d{index}/id"
                          "Verification profile id is invalid."
                          "Use an explicit stable identity."

              for subject in profile.SubjectIds do
                  if not (catalogueIds.Contains subject) then
                      yield
                          diagnostic
                              "QUINT-CONTRACT-REFERENCE"
                              $"/verificationProfiles/%d{index}/subjectIds"
                              $"'%s{subject}' is not declared."
                              "Reference a catalogue identity."

              for bound in profile.BoundIds do
                  if not (boundIds.Contains bound) then
                      yield
                          diagnostic
                              "QUINT-CONTRACT-BOUND-REFERENCE"
                              $"/verificationProfiles/%d{index}/boundIds"
                              $"'%s{bound}' is not declared."
                              "Reference a finite bound."
          for id, rows in contract.Bounds |> List.groupBy _.Id do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-BOUND-DUPLICATE"
                          "/bounds"
                          $"Bound '%s{id}' occurs more than once."
                          "Keep one bound per id."
          for index, bound in contract.Bounds |> List.indexed do
              if not (idPattern.IsMatch bound.Id) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-ID"
                          $"/bounds/%d{index}/id"
                          "Bound id is invalid."
                          "Use an explicit stable identity."

              if bound.Minimum < 0L || bound.Maximum < bound.Minimum then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-BOUND"
                          $"/bounds/%d{index}"
                          "Finite bound is negative or reversed."
                          "Use 0 <= minimum <= maximum."
          for index, impact in contract.Impacts |> List.indexed do
              if not (catalogueIds.Contains impact.SubjectId) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-REFERENCE"
                          $"/impacts/%d{index}/subjectId"
                          $"'%s{impact.SubjectId}' is not declared."
                          "Reference a catalogue identity."
          for name, rows in contract.Digests |> List.groupBy _.Name do
              if rows.Length > 1 then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-DIGEST-DUPLICATE"
                          "/digests"
                          $"Digest '%s{name}' occurs more than once."
                          "Keep one digest per semantic input."
          for index, digest in contract.Digests |> List.indexed do
              if String.IsNullOrWhiteSpace digest.Name then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-DIGEST-NAME"
                          $"/digests/%d{index}/name"
                          "Digest name is empty."
                          "Name the semantic input."

              if not (validDigest digest.Sha256) then
                  yield
                      diagnostic
                          "QUINT-CONTRACT-DIGEST"
                          $"/digests/%d{index}/sha256"
                          "Digest is not lowercase SHA-256."
                          "Provide 64 lowercase hexadecimal characters." ]
        |> sorted

    let writeStrings (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray(name)
        values |> List.distinct |> List.sort |> List.iter writer.WriteStringValue
        writer.WriteEndArray()

    let writeRange (writer: Utf8JsonWriter) (source: QuintSourceRange) =
        writer.WriteStartObject("source")
        writer.WriteString("path", source.Path)
        writer.WriteNumber("startLine", source.Start.Line)
        writer.WriteNumber("startColumn", source.Start.Column)
        writer.WriteNumber("endLine", source.End.Line)
        writer.WriteNumber("endColumn", source.End.Column)
        writer.WriteEndObject()

    let serializeUnchecked contract =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", contract.Schema)
        writer.WriteString("profile", contract.Profile)
        writer.WriteString("specification", contract.Specification)
        writer.WriteStartArray("catalogue")

        contract.Catalogue
        |> List.sortBy (fun row -> kindText row.Kind, row.Id)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteString("kind", kindText row.Kind)
            writeRange writer row.Source
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("actionEffects")

        contract.ActionEffects
        |> List.sortBy _.ActionId
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("actionId", row.ActionId)
            writeStrings writer "reads" row.Reads
            writeStrings writer "writes" row.Writes
            writeStrings writer "subjects" row.Subjects
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("relationships")

        contract.Relationships
        |> List.sortBy (fun row -> row.FromId, relationText row.Kind, row.ToId)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("from", row.FromId)
            writer.WriteString("kind", relationText row.Kind)
            writer.WriteString("to", row.ToId)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("verificationProfiles")

        contract.VerificationProfiles
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteString("kind", row.Kind)
            writeStrings writer "subjectIds" row.SubjectIds
            writeStrings writer "boundIds" row.BoundIds
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("bounds")

        contract.Bounds
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteNumber("minimum", row.Minimum)
            writer.WriteNumber("maximum", row.Maximum)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("impacts")

        contract.Impacts
        |> List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("subjectId", row.SubjectId)
            writer.WriteString("category", row.Category)
            writer.WriteString("detail", row.Detail)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("compatibility")

        contract.Compatibility
        |> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("surface", row.Surface)
            writer.WriteString("requirement", row.Requirement)
            writer.WriteString("detail", row.Detail)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("digests")

        contract.Digests
        |> List.sortBy _.Name
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("name", row.Name)
            writer.WriteString("sha256", normalizeDigest row.Sha256)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let requireObject (path: string) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            raise (JsonException($"%s{path}: expected object."))

    let checkFields (path: string) (allowed: Set<string>) (element: JsonElement) =
        requireObject path element
        let names = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList

        match names |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
        | Some(name, _) -> raise (JsonException($"%s{path}/%s{name}: duplicate field."))
        | None -> ()

        match names |> List.tryFind (fun name -> not (Set.contains name allowed)) with
        | Some name -> raise (JsonException($"%s{path}/%s{name}: unknown or expression-bearing field."))
        | None -> ()

    let prop (name: string) (element: JsonElement) =
        match element.TryGetProperty name with
        | true, value -> value
        | _ -> raise (JsonException($"Missing required field '%s{name}'."))

    let str (name: string) (element: JsonElement) =
        let value = prop name element

        if value.ValueKind <> JsonValueKind.String then
            raise (JsonException($"Field '%s{name}' must be a string."))

        match value.GetString() with
        | null -> raise (JsonException($"Field '%s{name}' must be a non-null string."))
        | text -> text

    let int64 (name: string) (element: JsonElement) =
        let value = prop name element in

        match value.TryGetInt64() with
        | true, number -> number
        | _ -> raise (JsonException($"Field '%s{name}' must be an integer."))

    let array (name: string) (element: JsonElement) =
        let value = prop name element in

        if value.ValueKind <> JsonValueKind.Array then
            raise (JsonException($"Field '%s{name}' must be an array."))
        else
            value.EnumerateArray() |> Seq.toList

    let strings (name: string) (element: JsonElement) =
        array name element
        |> List.map (fun value ->
            if value.ValueKind <> JsonValueKind.String then
                raise (JsonException($"Field '%s{name}' must contain strings."))

            match value.GetString() with
            | null -> raise (JsonException($"Field '%s{name}' must contain non-null strings."))
            | text -> text)

    let range (path: string) (element: JsonElement) =
        let source = prop "source" element

        checkFields
            (path + "/source")
            (Set.ofList [ "path"; "startLine"; "startColumn"; "endLine"; "endColumn" ])
            source

        { Path = str "path" source
          Start =
            { Line = int (int64 "startLine" source)
              Column = int (int64 "startColumn" source) }
          End =
            { Line = int (int64 "endLine" source)
              Column = int (int64 "endColumn" source) } }

    let decode (text: string) =
        use document = JsonDocument.Parse text
        let root = document.RootElement

        checkFields
            ""
            (Set.ofList
                [ "schema"
                  "profile"
                  "specification"
                  "catalogue"
                  "actionEffects"
                  "relationships"
                  "verificationProfiles"
                  "bounds"
                  "impacts"
                  "compatibility"
                  "digests" ])
            root

        let catalogue =
            array "catalogue" root
            |> List.mapi (fun i row ->
                let path = $"/catalogue/%d{i}" in
                checkFields path (Set.ofList [ "id"; "kind"; "source" ]) row

                { Id = str "id" row
                  Kind = parseKind (path + "/kind") (str "kind" row)
                  Source = range path row })

        let effects =
            array "actionEffects" root
            |> List.mapi (fun i row ->
                checkFields $"/actionEffects/%d{i}" (Set.ofList [ "actionId"; "reads"; "writes"; "subjects" ]) row

                { ActionId = str "actionId" row
                  Reads = strings "reads" row
                  Writes = strings "writes" row
                  Subjects = strings "subjects" row })

        let relationships =
            array "relationships" root
            |> List.mapi (fun i row ->
                let path = $"/relationships/%d{i}" in
                checkFields path (Set.ofList [ "from"; "kind"; "to" ]) row

                { FromId = str "from" row
                  Kind = parseRelation (path + "/kind") (str "kind" row)
                  ToId = str "to" row })

        let profiles =
            array "verificationProfiles" root
            |> List.mapi (fun i row ->
                checkFields $"/verificationProfiles/%d{i}" (Set.ofList [ "id"; "kind"; "subjectIds"; "boundIds" ]) row

                { Id = str "id" row
                  Kind = str "kind" row
                  SubjectIds = strings "subjectIds" row
                  BoundIds = strings "boundIds" row })

        let bounds =
            array "bounds" root
            |> List.mapi (fun i row ->
                checkFields $"/bounds/%d{i}" (Set.ofList [ "id"; "minimum"; "maximum" ]) row

                { Id = str "id" row
                  Minimum = int64 "minimum" row
                  Maximum = int64 "maximum" row })

        let impacts =
            array "impacts" root
            |> List.mapi (fun i row ->
                checkFields $"/impacts/%d{i}" (Set.ofList [ "subjectId"; "category"; "detail" ]) row

                { SubjectId = str "subjectId" row
                  Category = str "category" row
                  Detail = str "detail" row })

        let compatibility =
            array "compatibility" root
            |> List.mapi (fun i row ->
                checkFields $"/compatibility/%d{i}" (Set.ofList [ "surface"; "requirement"; "detail" ]) row

                { Surface = str "surface" row
                  Requirement = str "requirement" row
                  Detail = str "detail" row })

        let digests =
            array "digests" root
            |> List.mapi (fun i row ->
                checkFields $"/digests/%d{i}" (Set.ofList [ "name"; "sha256" ]) row

                { Name = str "name" row
                  Sha256 = str "sha256" row })

        { Schema = str "schema" root
          Profile = str "profile" root
          Specification = str "specification" root
          Catalogue = catalogue
          ActionEffects = effects
          Relationships = relationships
          VerificationProfiles = profiles
          Bounds = bounds
          Impacts = impacts
          Compatibility = compatibility
          Digests = digests }

[<RequireQualifiedAccess>]
module QuintContract =
    let schema = ContractCore.schema

    let validate contract =
        let profileFindings =
            QuintProfile.validate
                { Profile = contract.Profile
                  QuintVersion = QuintProfile.quintVersion
                  Entries = contract.Catalogue
                  ActionEffects = contract.ActionEffects }

        let mapped =
            profileFindings
            |> List.map (fun item ->
                { Code = item.Code
                  Path = item.Path
                  Message = item.Message
                  Correction = item.Correction })

        ContractCore.sorted (ContractCore.validate contract @ mapped)

    let serializeCanonical contract =
        match validate contract with
        | [] -> Ok(ContractCore.serializeUnchecked contract)
        | findings -> Error findings

    let deserialize text =
        try
            let contract = ContractCore.decode text

            match validate contract with
            | [] -> Ok contract
            | findings -> Error findings
        with :? JsonException as ex ->
            Error
                [ ContractCore.diagnostic
                      "QUINT-CONTRACT-MALFORMED"
                      "/"
                      ex.Message
                      "Emit exact compiled-contract v1 JSON with no unknown, duplicate, AST, IR, or expression fields." ]

    let fingerprint inputs =
        let named =
            [ "/sourceSha256", inputs.SourceSha256
              "/fenceManifestSha256", inputs.FenceManifestSha256
              "/generatedModulesSha256", inputs.GeneratedModulesSha256
              "/toolchainSha256", inputs.ToolchainSha256 ]

        let findings =
            named
            |> List.choose (fun (path, value) ->
                if ContractCore.validDigest value then
                    None
                else
                    Some(
                        ContractCore.diagnostic
                            "QUINT-FINGERPRINT-DIGEST"
                            path
                            "Fingerprint input is not lowercase SHA-256."
                            "Bind the exact content-addressed input digest."
                    ))

        match findings, serializeCanonical inputs.Contract with
        | [], Ok contract ->
            let frame (value: string) =
                let bytes = Encoding.UTF8.GetBytes value
                let length = Array.zeroCreate<byte> 4
                BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length)
                Array.append length bytes

            [ "fsgg.quint.compilation-fingerprint/v1"
              ContractCore.normalizeDigest inputs.SourceSha256
              ContractCore.normalizeDigest inputs.FenceManifestSha256
              ContractCore.normalizeDigest inputs.GeneratedModulesSha256
              ContractCore.normalizeDigest inputs.ToolchainSha256
              contract ]
            |> List.collect (frame >> Array.toList)
            |> List.toArray
            |> ContractCore.sha256
            |> Ok
        | _, Error contractFindings -> Error(ContractCore.sorted (findings @ contractFindings))
        | _, _ -> Error(ContractCore.sorted findings)

    let semanticDiff before after =
        let incompatible =
            [ if before.Schema <> after.Schema then
                  yield
                      ContractCore.diagnostic
                          "QUINT-DIFF-SCHEMA"
                          "/schema"
                          "Contracts use incompatible schemas."
                          "Compare contracts with the same compiled-contract schema."
              if before.Profile <> after.Profile then
                  yield
                      ContractCore.diagnostic
                          "QUINT-DIFF-PROFILE"
                          "/profile"
                          "Contracts use incompatible profiles."
                          "Compare contracts with the same Quint profile." ]

        if not (List.isEmpty incompatible) then
            Error incompatible
        else
            match validate before, validate after with
            | [], [] ->
                let beforeHash = ContractCore.serializeUnchecked before |> ContractCore.sha256Text
                let afterHash = ContractCore.serializeUnchecked after |> ContractCore.sha256Text

                let normalizeStrings values = values |> List.distinct |> List.sort

                let normalizeEffects (values: QuintActionEffect list) =
                    values
                    |> List.map (fun row ->
                        { row with
                            Reads = normalizeStrings row.Reads
                            Writes = normalizeStrings row.Writes
                            Subjects = normalizeStrings row.Subjects })
                    |> List.sortBy _.ActionId

                let normalizeProfiles (values: QuintVerificationProfile list) =
                    values
                    |> List.map (fun row ->
                        { row with
                            SubjectIds = normalizeStrings row.SubjectIds
                            BoundIds = normalizeStrings row.BoundIds })
                    |> List.sortBy _.Id

                let components =
                    [ ("/specification", before.Specification <> after.Specification)
                      ("/catalogue",
                       List.sortBy
                           (fun (row: QuintCatalogueEntry) -> ContractCore.kindText row.Kind, row.Id)
                           before.Catalogue
                       <> List.sortBy
                           (fun (row: QuintCatalogueEntry) -> ContractCore.kindText row.Kind, row.Id)
                           after.Catalogue)
                      ("/actionEffects", normalizeEffects before.ActionEffects <> normalizeEffects after.ActionEffects)
                      ("/relationships",
                       List.sortBy
                           (fun row -> row.FromId, ContractCore.relationText row.Kind, row.ToId)
                           before.Relationships
                       <> List.sortBy
                           (fun row -> row.FromId, ContractCore.relationText row.Kind, row.ToId)
                           after.Relationships)
                      ("/verificationProfiles",
                       normalizeProfiles before.VerificationProfiles
                       <> normalizeProfiles after.VerificationProfiles)
                      ("/bounds", List.sortBy _.Id before.Bounds <> List.sortBy _.Id after.Bounds)
                      ("/impacts",
                       List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail) before.Impacts
                       <> List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail) after.Impacts)
                      ("/compatibility",
                       List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail) before.Compatibility
                       <> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail) after.Compatibility)
                      ("/digests", List.sortBy _.Name before.Digests <> List.sortBy _.Name after.Digests) ]

                let changes =
                    components
                    |> List.choose (fun (path, changed) ->
                        if changed then
                            Some
                                { Path = path
                                  BeforeSha256 = beforeHash
                                  AfterSha256 = afterHash }
                        else
                            None)

                if List.isEmpty changes then
                    Ok Equivalent
                else
                    Ok(Changed changes)
            | beforeFindings, afterFindings -> Error(ContractCore.sorted (beforeFindings @ afterFindings))

type QuintCompiledContractV2 =
    { Schema: string
      Profile: string
      Specification: string
      Exports: QuintGeneralExport list
      Catalogue: QuintModelCatalogueEntry list
      ActionEffects: QuintActionEffect list
      Relationships: QuintRelationship list
      VerificationProfiles: QuintVerificationProfile list
      Bounds: QuintFiniteBound list
      Impacts: QuintImpact list
      Compatibility: QuintCompatibility list
      Digests: QuintSemanticDigest list }

module private ContractV2Core =
    let schema = "fsgg.quint.compiled-contract/v2"

    let rec valueKey =
        function
        | QuintBool value -> if value then "b:1" else "b:0"
        | QuintInt value -> "i:" + value.ToString("+0000000000000000000;-0000000000000000000", CultureInfo.InvariantCulture)
        | QuintString value -> "s:" + value
        | QuintTuple values -> "t:[" + (values |> List.map valueKey |> String.concat ",") + "]"
        | QuintRecord fields ->
            "r:{" + (fields |> List.map (fun (name, value) -> name + "=" + valueKey value) |> String.concat ",") + "}"
        | QuintVariant(tag, value) -> "v:" + tag + ":" + (value |> Option.map valueKey |> Option.defaultValue "")
        | QuintList values -> "l:[" + (values |> List.map valueKey |> String.concat ",") + "]"
        | QuintSet values -> "e:[" + (values |> List.map valueKey |> String.concat ",") + "]"
        | QuintMap entries ->
            "m:[" + (entries |> List.map (fun (key, value) -> valueKey key + "=" + valueKey value) |> String.concat ",") + "]"

    let normalizeValue =
        let rec normalize =
            function
            | QuintTuple values -> QuintTuple(List.map normalize values)
            | QuintRecord fields ->
                fields |> List.map (fun (name, value) -> name, normalize value) |> List.sortBy fst |> QuintRecord
            | QuintVariant(tag, value) -> QuintVariant(tag, Option.map normalize value)
            | QuintList values -> QuintList(List.map normalize values)
            | QuintSet values ->
                values |> List.map normalize |> List.sortBy valueKey |> List.distinct |> QuintSet
            | QuintMap entries ->
                entries
                |> List.map (fun (key, value) -> normalize key, normalize value)
                |> List.sortBy (fst >> valueKey)
                |> QuintMap
            | value -> value

        normalize

    let writeRange (writer: Utf8JsonWriter) (source: QuintSourceRange) =
        ContractCore.writeRange writer source

    let rec writeValue (writer: Utf8JsonWriter) value =
        writer.WriteStartObject()

        match normalizeValue value with
        | QuintBool value ->
            writer.WriteString("kind", "bool")
            writer.WriteBoolean("value", value)
        | QuintInt value ->
            writer.WriteString("kind", "int")
            writer.WriteNumber("value", value)
        | QuintString value ->
            writer.WriteString("kind", "string")
            writer.WriteString("value", value)
        | QuintTuple values
        | QuintList values
        | QuintSet values as collection ->
            let kind =
                match collection with
                | QuintTuple _ -> "tuple"
                | QuintList _ -> "list"
                | _ -> "set"

            writer.WriteString("kind", kind)
            writer.WriteStartArray("items")
            values |> List.iter (writeValue writer)
            writer.WriteEndArray()
        | QuintRecord fields ->
            writer.WriteString("kind", "record")
            writer.WriteStartArray("fields")

            fields
            |> List.sortBy fst
            |> List.iter (fun (name, value) ->
                writer.WriteStartObject()
                writer.WriteString("name", name)
                writer.WritePropertyName("value")
                writeValue writer value
                writer.WriteEndObject())

            writer.WriteEndArray()
        | QuintVariant(tag, value) ->
            writer.WriteString("kind", "variant")
            writer.WriteString("tag", tag)

            match value with
            | Some value ->
                writer.WritePropertyName("value")
                writeValue writer value
            | None -> writer.WriteNull("value")
        | QuintMap entries ->
            writer.WriteString("kind", "map")
            writer.WriteStartArray("entries")

            entries
            |> List.sortBy (fst >> valueKey)
            |> List.iter (fun (key, value) ->
                writer.WriteStartObject()
                writer.WritePropertyName("key")
                writeValue writer key
                writer.WritePropertyName("value")
                writeValue writer value
                writer.WriteEndObject())

            writer.WriteEndArray()

        writer.WriteEndObject()

    let readValue =
        let rec read path (element: JsonElement) =
            ContractCore.requireObject path element
            let kind = ContractCore.str "kind" element

            match kind with
            | "bool" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "value" ]) element
                let value = ContractCore.prop "value" element

                match value.ValueKind with
                | JsonValueKind.True -> QuintBool true
                | JsonValueKind.False -> QuintBool false
                | _ -> raise (JsonException($"%s{path}/value: expected boolean."))
            | "int" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "value" ]) element
                QuintInt(ContractCore.int64 "value" element)
            | "string" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "value" ]) element
                QuintString(ContractCore.str "value" element)
            | "tuple"
            | "list"
            | "set" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "items" ]) element

                let values =
                    ContractCore.array "items" element
                    |> List.mapi (fun index item -> read ($"%s{path}/items/%d{index}") item)

                match kind with
                | "tuple" -> QuintTuple values
                | "list" -> QuintList values
                | _ -> values |> List.sortBy valueKey |> List.distinct |> QuintSet
            | "record" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "fields" ]) element

                ContractCore.array "fields" element
                |> List.mapi (fun index field ->
                    let fieldPath = $"%s{path}/fields/%d{index}"
                    ContractCore.checkFields fieldPath (Set.ofList [ "name"; "value" ]) field
                    ContractCore.str "name" field, read (fieldPath + "/value") (ContractCore.prop "value" field))
                |> List.sortBy fst
                |> QuintRecord
            | "variant" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "tag"; "value" ]) element
                let value = ContractCore.prop "value" element

                QuintVariant(
                    ContractCore.str "tag" element,
                    if value.ValueKind = JsonValueKind.Null then None else Some(read (path + "/value") value)
                )
            | "map" ->
                ContractCore.checkFields path (Set.ofList [ "kind"; "entries" ]) element

                ContractCore.array "entries" element
                |> List.mapi (fun index entry ->
                    let entryPath = $"%s{path}/entries/%d{index}"
                    ContractCore.checkFields entryPath (Set.ofList [ "key"; "value" ]) entry
                    read (entryPath + "/key") (ContractCore.prop "key" entry),
                    read (entryPath + "/value") (ContractCore.prop "value" entry))
                |> List.sortBy (fst >> valueKey)
                |> QuintMap
            | other -> raise (JsonException($"%s{path}/kind: unsupported value kind '%s{other}'."))

        read

    let writeCommon (writer: Utf8JsonWriter) (contract: QuintCompiledContractV2) =
        writer.WriteStartArray("actionEffects")

        contract.ActionEffects
        |> List.sortBy _.ActionId
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("actionId", row.ActionId)
            ContractCore.writeStrings writer "reads" row.Reads
            ContractCore.writeStrings writer "writes" row.Writes
            ContractCore.writeStrings writer "subjects" row.Subjects
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("relationships")

        contract.Relationships
        |> List.sortBy (fun row -> row.FromId, ContractCore.relationText row.Kind, row.ToId)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("from", row.FromId)
            writer.WriteString("kind", ContractCore.relationText row.Kind)
            writer.WriteString("to", row.ToId)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("verificationProfiles")

        contract.VerificationProfiles
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteString("kind", row.Kind)
            ContractCore.writeStrings writer "subjectIds" row.SubjectIds
            ContractCore.writeStrings writer "boundIds" row.BoundIds
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("bounds")

        contract.Bounds
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteNumber("minimum", row.Minimum)
            writer.WriteNumber("maximum", row.Maximum)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("impacts")

        contract.Impacts
        |> List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("subjectId", row.SubjectId)
            writer.WriteString("category", row.Category)
            writer.WriteString("detail", row.Detail)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("compatibility")

        contract.Compatibility
        |> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail)
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("surface", row.Surface)
            writer.WriteString("requirement", row.Requirement)
            writer.WriteString("detail", row.Detail)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("digests")

        contract.Digests
        |> List.sortBy _.Name
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("name", row.Name)
            writer.WriteString("sha256", ContractCore.normalizeDigest row.Sha256)
            writer.WriteEndObject())

        writer.WriteEndArray()

    let serializeUnchecked (contract: QuintCompiledContractV2) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", contract.Schema)
        writer.WriteString("profile", contract.Profile)
        writer.WriteString("specification", contract.Specification)
        writer.WriteStartArray("exports")

        contract.Exports
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteString("module", row.ModuleName)
            writer.WriteString("declaration", row.DeclarationName)
            writer.WritePropertyName("value")
            writeValue writer row.Value
            writeRange writer row.Source
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("catalogue")

        contract.Catalogue
        |> List.sortBy _.Id
        |> List.iter (fun row ->
            writer.WriteStartObject()
            writer.WriteString("id", row.Id)
            writer.WriteString("kind", row.Kind)
            writer.WriteString("exportId", row.ExportId)
            writer.WritePropertyName("value")
            writeValue writer row.Value
            writeRange writer row.Source
            writer.WriteEndObject())

        writer.WriteEndArray()
        writeCommon writer contract
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let validate (contract: QuintCompiledContractV2) =
        let catalogueIds = contract.Catalogue |> List.map _.Id |> Set.ofList
        let exportIds = contract.Exports |> List.map _.Id |> Set.ofList
        let subjectIds = Set.union catalogueIds exportIds
        let boundIds = contract.Bounds |> List.map _.Id |> Set.ofList

        [ if contract.Schema <> schema then
              yield
                  ContractCore.diagnostic
                      "QUINT-CONTRACT-SCHEMA"
                      "/schema"
                      $"Expected '%s{schema}', got '%s{contract.Schema}'."
                      "Use compiled-contract v2."
          if contract.Profile <> QuintGeneralProfile.identity then
              yield
                  ContractCore.diagnostic
                      "QUINT-CONTRACT-PROFILE"
                      "/profile"
                      $"Expected '%s{QuintGeneralProfile.identity}', got '%s{contract.Profile}'."
                      "Compile with the general profile adapter."
          if not (ContractCore.idPattern.IsMatch contract.Specification) then
              yield ContractCore.diagnostic "QUINT-CONTRACT-ID" "/specification" "Specification is invalid." "Use a stable identity."
          for id, rows in contract.Exports |> List.groupBy _.Id do
              if rows.Length > 1 then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-EXPORT-DUPLICATE" "/exports" $"Export '%s{id}' is duplicated." "Keep one export."
          for id, rows in contract.Catalogue |> List.groupBy _.Id do
              if rows.Length > 1 then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-CATALOGUE-DUPLICATE" "/catalogue" $"Catalogue identity '%s{id}' is duplicated." "Keep one row."
          for index, row in contract.Catalogue |> List.indexed do
              if not (exportIds.Contains row.ExportId) then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-REFERENCE" $"/catalogue/%d{index}/exportId" $"'%s{row.ExportId}' is not exported." "Reference an export."
          for index, relation in contract.Relationships |> List.indexed do
              if not (subjectIds.Contains relation.FromId) || not (subjectIds.Contains relation.ToId) then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-REFERENCE" $"/relationships/%d{index}" "Relationship reference is not declared." "Reference an export or catalogue identity."
          for index, profile in contract.VerificationProfiles |> List.indexed do
              for subject in profile.SubjectIds do
                  if not (subjectIds.Contains subject) then
                      yield ContractCore.diagnostic "QUINT-CONTRACT-REFERENCE" $"/verificationProfiles/%d{index}/subjectIds" $"'%s{subject}' is not declared." "Reference an export or catalogue identity."
              for bound in profile.BoundIds do
                  if not (boundIds.Contains bound) then
                      yield ContractCore.diagnostic "QUINT-CONTRACT-BOUND-REFERENCE" $"/verificationProfiles/%d{index}/boundIds" $"'%s{bound}' is not declared." "Reference a finite bound."
          for index, bound in contract.Bounds |> List.indexed do
              if bound.Minimum < 0L || bound.Maximum < bound.Minimum then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-BOUND" $"/bounds/%d{index}" "Finite bound is negative or reversed." "Use 0 <= minimum <= maximum."
          for index, digest in contract.Digests |> List.indexed do
              if not (ContractCore.validDigest digest.Sha256) then
                  yield ContractCore.diagnostic "QUINT-CONTRACT-DIGEST" $"/digests/%d{index}/sha256" "Digest is not lowercase SHA-256." "Provide 64 lowercase hexadecimal characters." ]
        |> ContractCore.sorted

    let decode (text: string) =
        use document = JsonDocument.Parse text
        let root = document.RootElement

        ContractCore.checkFields
            ""
            (Set.ofList
                [ "schema"; "profile"; "specification"; "exports"; "catalogue"; "actionEffects"; "relationships"
                  "verificationProfiles"; "bounds"; "impacts"; "compatibility"; "digests" ])
            root

        let exports =
            ContractCore.array "exports" root
            |> List.mapi (fun index row ->
                let path = $"/exports/%d{index}"
                ContractCore.checkFields path (Set.ofList [ "id"; "module"; "declaration"; "value"; "source" ]) row

                { Id = ContractCore.str "id" row
                  ModuleName = ContractCore.str "module" row
                  DeclarationName = ContractCore.str "declaration" row
                  Value = readValue (path + "/value") (ContractCore.prop "value" row)
                  Source = ContractCore.range path row })

        let catalogue =
            ContractCore.array "catalogue" root
            |> List.mapi (fun index row ->
                let path = $"/catalogue/%d{index}"
                ContractCore.checkFields path (Set.ofList [ "id"; "kind"; "exportId"; "value"; "source" ]) row

                { Id = ContractCore.str "id" row
                  Kind = ContractCore.str "kind" row
                  ExportId = ContractCore.str "exportId" row
                  Value = readValue (path + "/value") (ContractCore.prop "value" row)
                  Source = ContractCore.range path row })

        let effects =
            ContractCore.array "actionEffects" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/actionEffects/%d{index}" (Set.ofList [ "actionId"; "reads"; "writes"; "subjects" ]) row
                { ActionId = ContractCore.str "actionId" row
                  Reads = ContractCore.strings "reads" row
                  Writes = ContractCore.strings "writes" row
                  Subjects = ContractCore.strings "subjects" row })

        let relationships =
            ContractCore.array "relationships" root
            |> List.mapi (fun index row ->
                let path = $"/relationships/%d{index}"
                ContractCore.checkFields path (Set.ofList [ "from"; "kind"; "to" ]) row
                { FromId = ContractCore.str "from" row
                  Kind = ContractCore.parseRelation (path + "/kind") (ContractCore.str "kind" row)
                  ToId = ContractCore.str "to" row })

        let profiles =
            ContractCore.array "verificationProfiles" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/verificationProfiles/%d{index}" (Set.ofList [ "id"; "kind"; "subjectIds"; "boundIds" ]) row
                { Id = ContractCore.str "id" row
                  Kind = ContractCore.str "kind" row
                  SubjectIds = ContractCore.strings "subjectIds" row
                  BoundIds = ContractCore.strings "boundIds" row })

        let bounds =
            ContractCore.array "bounds" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/bounds/%d{index}" (Set.ofList [ "id"; "minimum"; "maximum" ]) row
                { Id = ContractCore.str "id" row
                  Minimum = ContractCore.int64 "minimum" row
                  Maximum = ContractCore.int64 "maximum" row })

        let impacts =
            ContractCore.array "impacts" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/impacts/%d{index}" (Set.ofList [ "subjectId"; "category"; "detail" ]) row
                { SubjectId = ContractCore.str "subjectId" row
                  Category = ContractCore.str "category" row
                  Detail = ContractCore.str "detail" row })

        let compatibility =
            ContractCore.array "compatibility" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/compatibility/%d{index}" (Set.ofList [ "surface"; "requirement"; "detail" ]) row
                { Surface = ContractCore.str "surface" row
                  Requirement = ContractCore.str "requirement" row
                  Detail = ContractCore.str "detail" row })

        let digests =
            ContractCore.array "digests" root
            |> List.mapi (fun index row ->
                ContractCore.checkFields $"/digests/%d{index}" (Set.ofList [ "name"; "sha256" ]) row
                { Name = ContractCore.str "name" row
                  Sha256 = ContractCore.str "sha256" row })

        { Schema = ContractCore.str "schema" root
          Profile = ContractCore.str "profile" root
          Specification = ContractCore.str "specification" root
          Exports = exports
          Catalogue = catalogue
          ActionEffects = effects
          Relationships = relationships
          VerificationProfiles = profiles
          Bounds = bounds
          Impacts = impacts
          Compatibility = compatibility
          Digests = digests }

[<RequireQualifiedAccess>]
module QuintContractV2 =
    let schema = ContractV2Core.schema
    let validate contract = ContractV2Core.validate contract

    let serializeCanonical contract =
        match validate contract with
        | [] -> Ok(ContractV2Core.serializeUnchecked contract)
        | findings -> Error findings

    let deserialize text =
        try
            let contract = ContractV2Core.decode text
            match validate contract with [] -> Ok contract | findings -> Error findings
        with :? JsonException as ex ->
            Error
                [ ContractCore.diagnostic
                      "QUINT-CONTRACT-MALFORMED"
                      "/"
                      ex.Message
                      "Emit exact compiled-contract v2 JSON with no unknown, duplicate, IR, or expression fields." ]

    let semanticDiff before after =
        if before.Schema <> after.Schema then
            Error [ ContractCore.diagnostic "QUINT-DIFF-SCHEMA" "/schema" "Contracts use incompatible schemas." "Compare contract v2 values." ]
        elif before.Profile <> after.Profile then
            Error [ ContractCore.diagnostic "QUINT-DIFF-PROFILE" "/profile" "Contracts use incompatible profiles." "Compare the same explicit profile." ]
        else
            match serializeCanonical before, serializeCanonical after with
            | Ok left, Ok right when left = right -> Ok Equivalent
            | Ok left, Ok right ->
                Ok(
                    Changed
                        [ { Path = "/"
                            BeforeSha256 = ContractCore.sha256Text left
                            AfterSha256 = ContractCore.sha256Text right } ]
                )
            | Error left, Error right -> Error(ContractCore.sorted (left @ right))
            | Error errors, _
            | _, Error errors -> Error errors
