namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Buffers.Binary
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

type QuintRelationshipKind = Requires | VerifiedBy | ImplementedBy | Reads | Writes

type QuintRelationship = { FromId: string; Kind: QuintRelationshipKind; ToId: string }

type QuintVerificationProfile =
    { Id: string
      Kind: string
      SubjectIds: string list
      BoundIds: string list }

type QuintFiniteBound = { Id: string; Minimum: int64; Maximum: int64 }
type QuintImpact = { SubjectId: string; Category: string; Detail: string }
type QuintCompatibility = { Surface: string; Requirement: string; Detail: string }
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

type QuintContractDiagnostic = { Code: string; Path: string; Message: string; Correction: string }
type QuintContractChange = { Path: string; BeforeSha256: string; AfterSha256: string }
type QuintContractDiff = Equivalent | Changed of QuintContractChange list

module private ContractCore =
    let schema = "fsgg.quint.compiled-contract/v1"
    let digestPattern = new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)
    let idPattern = new Regex("^[A-Z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$", RegexOptions.CultureInvariant)

    let diagnostic code path message correction : QuintContractDiagnostic =
        { Code = code; Path = path; Message = message; Correction = correction }

    let sorted (findings: QuintContractDiagnostic list) = findings |> List.distinct |> List.sortBy (fun item -> item.Path, item.Code, item.Message)
    let kindText = function Requirement -> "requirement" | StateVariable -> "stateVariable" | Action -> "action" | Invariant -> "invariant" | TemporalProperty -> "temporalProperty" | Evidence -> "evidence" | Implementation -> "implementation" | ExternalSubject -> "externalSubject"
    let relationText = function Requires -> "requires" | VerifiedBy -> "verifiedBy" | ImplementedBy -> "implementedBy" | Reads -> "reads" | Writes -> "writes"

    let parseKind path = function
        | "requirement" -> Requirement | "stateVariable" -> StateVariable | "action" -> QuintCatalogueKind.Action
        | "invariant" -> Invariant | "temporalProperty" -> TemporalProperty | "evidence" -> Evidence
        | "implementation" -> Implementation | "externalSubject" -> ExternalSubject
        | value -> raise (JsonException($"%s{path}: unsupported catalogue kind '%s{value}'."))

    let parseRelation path = function
        | "requires" -> Requires | "verifiedBy" -> VerifiedBy | "implementedBy" -> ImplementedBy
        | "reads" -> Reads | "writes" -> Writes
        | value -> raise (JsonException($"%s{path}: unsupported relationship kind '%s{value}'."))

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture)) |> String.concat ""

    let sha256Text (value: string) = value |> Encoding.UTF8.GetBytes |> sha256
    let normalizeDigest (value: string) = if value.StartsWith("sha256:", StringComparison.Ordinal) then value.Substring(7) else value
    let validDigest value = digestPattern.IsMatch(normalizeDigest value)

    let validate contract =
        let catalogueIds = contract.Catalogue |> List.map _.Id |> Set.ofList
        let boundIds = contract.Bounds |> List.map _.Id |> Set.ofList
        [ if contract.Schema <> schema then
              yield diagnostic "QUINT-CONTRACT-SCHEMA" "/schema" $"Expected '%s{schema}', got '%s{contract.Schema}'." "Use compiled-contract v1." 
          if contract.Profile <> QuintProfile.identity then
              yield diagnostic "QUINT-CONTRACT-PROFILE" "/profile" $"Expected '%s{QuintProfile.identity}', got '%s{contract.Profile}'." "Compile with the exact profile 1 adapter."
          if not (idPattern.IsMatch contract.Specification) then
              yield diagnostic "QUINT-CONTRACT-ID" "/specification" "Specification is not a stable identity." "Use an uppercase-leading explicit identity."
          if List.isEmpty contract.Catalogue then
              yield diagnostic "QUINT-CONTRACT-CATALOGUE-EMPTY" "/catalogue" "The catalogue is empty." "Declare all stable integration identities."
          for (kind, id), rows in contract.Catalogue |> List.groupBy (fun row -> row.Kind, row.Id) do
              if rows.Length > 1 then yield diagnostic "QUINT-CONTRACT-CATALOGUE-DUPLICATE" "/catalogue" $"Catalogue identity '%s{kindText kind}:%s{id}' occurs more than once." "Keep one row per (kind,id)."
          for id, rows in contract.ActionEffects |> List.groupBy _.ActionId do
              if rows.Length > 1 then yield diagnostic "QUINT-CONTRACT-EFFECT-DUPLICATE" "/actionEffects" $"Action '%s{id}' has multiple effect rows." "Keep one effect row per action."
          for index, relation in contract.Relationships |> List.indexed do
              if not (catalogueIds.Contains relation.FromId) then yield diagnostic "QUINT-CONTRACT-REFERENCE" $"/relationships/%d{index}/from" $"'%s{relation.FromId}' is not declared." "Reference a catalogue identity."
              if not (catalogueIds.Contains relation.ToId) then yield diagnostic "QUINT-CONTRACT-REFERENCE" $"/relationships/%d{index}/to" $"'%s{relation.ToId}' is not declared." "Reference a catalogue identity."
          for index, profile in contract.VerificationProfiles |> List.indexed do
              if not (idPattern.IsMatch profile.Id) then yield diagnostic "QUINT-CONTRACT-ID" $"/verificationProfiles/%d{index}/id" "Verification profile id is invalid." "Use an explicit stable identity."
              for subject in profile.SubjectIds do if not (catalogueIds.Contains subject) then yield diagnostic "QUINT-CONTRACT-REFERENCE" $"/verificationProfiles/%d{index}/subjectIds" $"'%s{subject}' is not declared." "Reference a catalogue identity."
              for bound in profile.BoundIds do if not (boundIds.Contains bound) then yield diagnostic "QUINT-CONTRACT-BOUND-REFERENCE" $"/verificationProfiles/%d{index}/boundIds" $"'%s{bound}' is not declared." "Reference a finite bound."
          for id, rows in contract.Bounds |> List.groupBy _.Id do
              if rows.Length > 1 then yield diagnostic "QUINT-CONTRACT-BOUND-DUPLICATE" "/bounds" $"Bound '%s{id}' occurs more than once." "Keep one bound per id."
          for index, bound in contract.Bounds |> List.indexed do
              if not (idPattern.IsMatch bound.Id) then yield diagnostic "QUINT-CONTRACT-ID" $"/bounds/%d{index}/id" "Bound id is invalid." "Use an explicit stable identity."
              if bound.Minimum < 0L || bound.Maximum < bound.Minimum then yield diagnostic "QUINT-CONTRACT-BOUND" $"/bounds/%d{index}" "Finite bound is negative or reversed." "Use 0 <= minimum <= maximum."
          for index, impact in contract.Impacts |> List.indexed do
              if not (catalogueIds.Contains impact.SubjectId) then yield diagnostic "QUINT-CONTRACT-REFERENCE" $"/impacts/%d{index}/subjectId" $"'%s{impact.SubjectId}' is not declared." "Reference a catalogue identity."
          for name, rows in contract.Digests |> List.groupBy _.Name do
              if rows.Length > 1 then yield diagnostic "QUINT-CONTRACT-DIGEST-DUPLICATE" "/digests" $"Digest '%s{name}' occurs more than once." "Keep one digest per semantic input."
          for index, digest in contract.Digests |> List.indexed do
              if String.IsNullOrWhiteSpace digest.Name then yield diagnostic "QUINT-CONTRACT-DIGEST-NAME" $"/digests/%d{index}/name" "Digest name is empty." "Name the semantic input."
              if not (validDigest digest.Sha256) then yield diagnostic "QUINT-CONTRACT-DIGEST" $"/digests/%d{index}/sha256" "Digest is not lowercase SHA-256." "Provide 64 lowercase hexadecimal characters." ]
        |> sorted

    let writeStrings (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray(name); values |> List.distinct |> List.sort |> List.iter writer.WriteStringValue; writer.WriteEndArray()

    let writeRange (writer: Utf8JsonWriter) (source: QuintSourceRange) =
        writer.WriteStartObject("source"); writer.WriteString("path", source.Path)
        writer.WriteNumber("startLine", source.Start.Line); writer.WriteNumber("startColumn", source.Start.Column)
        writer.WriteNumber("endLine", source.End.Line); writer.WriteNumber("endColumn", source.End.Column); writer.WriteEndObject()

    let serializeUnchecked contract =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject(); writer.WriteString("schema", contract.Schema); writer.WriteString("profile", contract.Profile); writer.WriteString("specification", contract.Specification)
        writer.WriteStartArray("catalogue")
        contract.Catalogue |> List.sortBy (fun row -> kindText row.Kind, row.Id) |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("id", row.Id); writer.WriteString("kind", kindText row.Kind); writeRange writer row.Source; writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("actionEffects")
        contract.ActionEffects |> List.sortBy _.ActionId |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("actionId", row.ActionId); writeStrings writer "reads" row.Reads; writeStrings writer "writes" row.Writes; writeStrings writer "subjects" row.Subjects; writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("relationships")
        contract.Relationships |> List.sortBy (fun row -> row.FromId, relationText row.Kind, row.ToId) |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("from", row.FromId); writer.WriteString("kind", relationText row.Kind); writer.WriteString("to", row.ToId); writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("verificationProfiles")
        contract.VerificationProfiles |> List.sortBy _.Id |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("id", row.Id); writer.WriteString("kind", row.Kind); writeStrings writer "subjectIds" row.SubjectIds; writeStrings writer "boundIds" row.BoundIds; writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("bounds")
        contract.Bounds |> List.sortBy _.Id |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("id", row.Id); writer.WriteNumber("minimum", row.Minimum); writer.WriteNumber("maximum", row.Maximum); writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("impacts")
        contract.Impacts |> List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail) |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("subjectId", row.SubjectId); writer.WriteString("category", row.Category); writer.WriteString("detail", row.Detail); writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("compatibility")
        contract.Compatibility |> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail) |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("surface", row.Surface); writer.WriteString("requirement", row.Requirement); writer.WriteString("detail", row.Detail); writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteStartArray("digests")
        contract.Digests |> List.sortBy _.Name |> List.iter (fun row -> writer.WriteStartObject(); writer.WriteString("name", row.Name); writer.WriteString("sha256", normalizeDigest row.Sha256); writer.WriteEndObject())
        writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush(); Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let requireObject (path: string) (element: JsonElement) = if element.ValueKind <> JsonValueKind.Object then raise(JsonException($"%s{path}: expected object."))
    let checkFields (path: string) (allowed: Set<string>) (element: JsonElement) =
        requireObject path element
        let names = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        match names |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with Some(name, _) -> raise(JsonException($"%s{path}/%s{name}: duplicate field.")) | None -> ()
        match names |> List.tryFind (fun name -> not (Set.contains name allowed)) with Some name -> raise(JsonException($"%s{path}/%s{name}: unknown or expression-bearing field.")) | None -> ()

    let prop (name: string) (element: JsonElement) = match element.TryGetProperty name with true, value -> value | _ -> raise(JsonException($"Missing required field '%s{name}'."))
    let str (name: string) (element: JsonElement) =
        let value = prop name element
        if value.ValueKind <> JsonValueKind.String then
            raise(JsonException($"Field '%s{name}' must be a string."))
        match value.GetString() with
        | null -> raise(JsonException($"Field '%s{name}' must be a non-null string."))
        | text -> text
    let int64 (name: string) (element: JsonElement) = let value = prop name element in match value.TryGetInt64() with true, number -> number | _ -> raise(JsonException($"Field '%s{name}' must be an integer."))
    let array (name: string) (element: JsonElement) = let value = prop name element in if value.ValueKind <> JsonValueKind.Array then raise(JsonException($"Field '%s{name}' must be an array.")) else value.EnumerateArray() |> Seq.toList
    let strings (name: string) (element: JsonElement) =
        array name element
        |> List.map (fun value ->
            if value.ValueKind <> JsonValueKind.String then
                raise(JsonException($"Field '%s{name}' must contain strings."))
            match value.GetString() with
            | null -> raise(JsonException($"Field '%s{name}' must contain non-null strings."))
            | text -> text)
    let range (path: string) (element: JsonElement) =
        let source = prop "source" element
        checkFields (path + "/source") (Set.ofList [ "path"; "startLine"; "startColumn"; "endLine"; "endColumn" ]) source
        { Path = str "path" source; Start = { Line = int (int64 "startLine" source); Column = int (int64 "startColumn" source) }; End = { Line = int (int64 "endLine" source); Column = int (int64 "endColumn" source) } }

    let decode (text: string) =
        use document = JsonDocument.Parse text
        let root = document.RootElement
        checkFields "" (Set.ofList [ "schema"; "profile"; "specification"; "catalogue"; "actionEffects"; "relationships"; "verificationProfiles"; "bounds"; "impacts"; "compatibility"; "digests" ]) root
        let catalogue = array "catalogue" root |> List.mapi (fun i row -> let path = $"/catalogue/%d{i}" in checkFields path (Set.ofList [ "id"; "kind"; "source" ]) row; { Id = str "id" row; Kind = parseKind (path + "/kind") (str "kind" row); Source = range path row })
        let effects = array "actionEffects" root |> List.mapi (fun i row -> checkFields $"/actionEffects/%d{i}" (Set.ofList [ "actionId"; "reads"; "writes"; "subjects" ]) row; { ActionId = str "actionId" row; Reads = strings "reads" row; Writes = strings "writes" row; Subjects = strings "subjects" row })
        let relationships = array "relationships" root |> List.mapi (fun i row -> let path = $"/relationships/%d{i}" in checkFields path (Set.ofList [ "from"; "kind"; "to" ]) row; { FromId = str "from" row; Kind = parseRelation (path + "/kind") (str "kind" row); ToId = str "to" row })
        let profiles = array "verificationProfiles" root |> List.mapi (fun i row -> checkFields $"/verificationProfiles/%d{i}" (Set.ofList [ "id"; "kind"; "subjectIds"; "boundIds" ]) row; { Id = str "id" row; Kind = str "kind" row; SubjectIds = strings "subjectIds" row; BoundIds = strings "boundIds" row })
        let bounds = array "bounds" root |> List.mapi (fun i row -> checkFields $"/bounds/%d{i}" (Set.ofList [ "id"; "minimum"; "maximum" ]) row; { Id = str "id" row; Minimum = int64 "minimum" row; Maximum = int64 "maximum" row })
        let impacts = array "impacts" root |> List.mapi (fun i row -> checkFields $"/impacts/%d{i}" (Set.ofList [ "subjectId"; "category"; "detail" ]) row; { SubjectId = str "subjectId" row; Category = str "category" row; Detail = str "detail" row })
        let compatibility = array "compatibility" root |> List.mapi (fun i row -> checkFields $"/compatibility/%d{i}" (Set.ofList [ "surface"; "requirement"; "detail" ]) row; { Surface = str "surface" row; Requirement = str "requirement" row; Detail = str "detail" row })
        let digests = array "digests" root |> List.mapi (fun i row -> checkFields $"/digests/%d{i}" (Set.ofList [ "name"; "sha256" ]) row; { Name = str "name" row; Sha256 = str "sha256" row })
        { Schema = str "schema" root; Profile = str "profile" root; Specification = str "specification" root; Catalogue = catalogue; ActionEffects = effects; Relationships = relationships; VerificationProfiles = profiles; Bounds = bounds; Impacts = impacts; Compatibility = compatibility; Digests = digests }

[<RequireQualifiedAccess>]
module QuintContract =
    let schema = ContractCore.schema
    let validate contract =
        let profileFindings = QuintProfile.validate { Profile = contract.Profile; QuintVersion = QuintProfile.quintVersion; Entries = contract.Catalogue; ActionEffects = contract.ActionEffects }
        let mapped = profileFindings |> List.map (fun item -> { Code = item.Code; Path = item.Path; Message = item.Message; Correction = item.Correction })
        ContractCore.sorted (ContractCore.validate contract @ mapped)

    let serializeCanonical contract =
        match validate contract with [] -> Ok(ContractCore.serializeUnchecked contract) | findings -> Error findings

    let deserialize text =
        try
            let contract = ContractCore.decode text
            match validate contract with [] -> Ok contract | findings -> Error findings
        with :? JsonException as ex ->
            Error [ ContractCore.diagnostic "QUINT-CONTRACT-MALFORMED" "/" ex.Message "Emit exact compiled-contract v1 JSON with no unknown, duplicate, AST, IR, or expression fields." ]

    let fingerprint inputs =
        let named = [ "/sourceSha256", inputs.SourceSha256; "/fenceManifestSha256", inputs.FenceManifestSha256; "/generatedModulesSha256", inputs.GeneratedModulesSha256; "/toolchainSha256", inputs.ToolchainSha256 ]
        let findings = named |> List.choose (fun (path, value) -> if ContractCore.validDigest value then None else Some(ContractCore.diagnostic "QUINT-FINGERPRINT-DIGEST" path "Fingerprint input is not lowercase SHA-256." "Bind the exact content-addressed input digest."))
        match findings, serializeCanonical inputs.Contract with
        | [], Ok contract ->
            let frame (value: string) =
                let bytes = Encoding.UTF8.GetBytes value
                let length = Array.zeroCreate<byte> 4
                BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length)
                Array.append length bytes

            [ "fsgg.quint.compilation-fingerprint/v1"; ContractCore.normalizeDigest inputs.SourceSha256; ContractCore.normalizeDigest inputs.FenceManifestSha256; ContractCore.normalizeDigest inputs.GeneratedModulesSha256; ContractCore.normalizeDigest inputs.ToolchainSha256; contract ]
            |> List.collect (frame >> Array.toList) |> List.toArray |> ContractCore.sha256 |> Ok
        | _, Error contractFindings -> Error(ContractCore.sorted (findings @ contractFindings))
        | _, _ -> Error(ContractCore.sorted findings)

    let semanticDiff before after =
        let incompatible =
            [ if before.Schema <> after.Schema then yield ContractCore.diagnostic "QUINT-DIFF-SCHEMA" "/schema" "Contracts use incompatible schemas." "Compare contracts with the same compiled-contract schema."
              if before.Profile <> after.Profile then yield ContractCore.diagnostic "QUINT-DIFF-PROFILE" "/profile" "Contracts use incompatible profiles." "Compare contracts with the same Quint profile." ]
        if not (List.isEmpty incompatible) then Error incompatible
        else
            match validate before, validate after with
            | [], [] ->
                let beforeHash = ContractCore.serializeUnchecked before |> ContractCore.sha256Text
                let afterHash = ContractCore.serializeUnchecked after |> ContractCore.sha256Text
                let components =
                    [ "/specification", before.Specification <> after.Specification
                      "/catalogue", before.Catalogue <> after.Catalogue
                      "/actionEffects", before.ActionEffects <> after.ActionEffects
                      "/relationships", before.Relationships <> after.Relationships
                      "/verificationProfiles", before.VerificationProfiles <> after.VerificationProfiles
                      "/bounds", before.Bounds <> after.Bounds
                      "/impacts", before.Impacts <> after.Impacts
                      "/compatibility", before.Compatibility <> after.Compatibility
                      "/digests", before.Digests <> after.Digests ]
                let changes =
                    components
                    |> List.choose (fun (path, changed) ->
                        if changed then Some { Path = path; BeforeSha256 = beforeHash; AfterSha256 = afterHash }
                        else None)
                if List.isEmpty changes then Ok Equivalent else Ok(Changed changes)
            | beforeFindings, afterFindings -> Error(ContractCore.sorted (beforeFindings @ afterFindings))
