namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Buffers.Binary
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

[<Struct>]
type SpecificationId = private SpecificationId of string

[<RequireQualifiedAccess>]
module SpecificationId =
    let create (value: string) =
        let valid character =
            (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9')
            || character = '-'

        if String.IsNullOrWhiteSpace value || value.Length < 5 then
            Error "Specification identifiers require at least five uppercase ASCII characters."
        elif value |> Seq.exists (valid >> not) then
            Error "Specification identifiers use uppercase ASCII letters, digits, and hyphens."
        elif
            value.StartsWith("-", StringComparison.Ordinal)
            || value.EndsWith("-", StringComparison.Ordinal)
            || value.Contains("--", StringComparison.Ordinal)
        then
            Error "Specification identifiers cannot begin/end with or repeat a hyphen."
        else
            Ok(SpecificationId value)

    let value (SpecificationId value) = value

type SourceLocation = { Line: int; Column: int }

type SpecificationProvenance =
    { Agent: string
      Session: string
      SourcePath: string
      SourceRevision: string
      AuthoredAtUtc: string }

type EvidenceObligation =
    { Id: SpecificationId
      Kind: string
      Description: string }

type EvidenceReceipt =
    { ObligationId: SpecificationId
      Kind: string
      EvidenceRef: string }

type SpecificationDiagnostic =
    { Code: string
      Path: string
      Message: string
      Location: SourceLocation option }

type SpecificationModel<'extension> =
    { Identity: SpecificationId
      SchemaVersion: int
      Provenance: SpecificationProvenance
      Intent: string
      EvidenceObligations: EvidenceObligation list
      Extension: 'extension }

type ExtensionContract<'extension> =
    { Kind: string
      SchemaVersion: int
      Validate: EvidenceObligation list -> 'extension -> SpecificationDiagnostic list
      EncodeCanonical: 'extension -> byte array
      WriteJson: Utf8JsonWriter -> 'extension -> unit
      DecodeJson: JsonElement -> Result<'extension, SpecificationDiagnostic list>
      ProjectMarkdown: 'extension -> string list }

type CompiledSpecification<'extension> =
    { Model: SpecificationModel<'extension>
      NormalizedBytes: byte array
      Fingerprint: string }

type SemanticChange =
    { Path: string
      Summary: string
      BeforeFingerprint: string
      AfterFingerprint: string }

type SemanticDiff =
    | Equivalent
    | Changed of SemanticChange list

type EvidenceValidation =
    { Satisfied: SpecificationId list
      Diagnostics: SpecificationDiagnostic list }

type SpecificationProjection =
    { Markdown: string
      Json: string
      SourceFingerprint: string
      GeneratedFingerprint: string }

type ProjectionObservation =
    | Missing
    | Unreadable of detail: string
    | Content of text: string

type MigrationReason =
    | UnresolvedReference
    | UnknownSemanticHeading
    | UnsupportedSchemaVersion
    | MalformedConstruct

type MigrationFinding =
    { Code: string
      Reason: MigrationReason
      Message: string
      Location: SourceLocation }

type MigrationOutcome<'model> =
    | Migrated of 'model
    | Ambiguous of MigrationFinding list
    | Unsupported of MigrationFinding list

module private Kernel =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = None }

    let located code path message location : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = Some location }

    let sortDiagnostics (diagnostics: SpecificationDiagnostic list) =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let blank code path name (value: string) =
        if String.IsNullOrWhiteSpace value then
            [ diagnostic code path $"%s{name} is required." ]
        else
            []

    let lowercaseHex (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && (value.Length = 40 || value.Length = 64)
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let frameBytes (bytes: byte array) =
        let length = Array.zeroCreate<byte> 4
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length)
        Array.concat [ length; bytes ]

    let frameText (value: string) =
        value |> Encoding.UTF8.GetBytes |> frameBytes

    let int32 value =
        let bytes = Array.zeroCreate<byte> 4
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value)
        bytes

    let sha256Bytes (bytes: byte array) =
        SHA256.HashData bytes
        |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture))
        |> String.concat ""

    let sha256Text (text: string) =
        text |> Encoding.UTF8.GetBytes |> sha256Bytes

    let evidenceBytes (obligations: EvidenceObligation list) =
        let rows =
            obligations
            |> List.sortBy (fun item -> SpecificationId.value item.Id)
            |> List.collect (fun item ->
                [ frameText (SpecificationId.value item.Id)
                  frameText item.Kind
                  frameText item.Description ])

        Array.concat (int32 obligations.Length :: rows)

    let normalizedBytes (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        Array.concat
            [ frameText "fsgg-typed-specification/v1"
              frameText (SpecificationId.value model.Identity)
              int32 model.SchemaVersion
              frameText model.Provenance.SourcePath
              frameText model.Provenance.SourceRevision
              evidenceBytes model.EvidenceObligations
              frameText contract.Kind
              int32 contract.SchemaVersion
              contract.EncodeCanonical model.Extension |> frameBytes ]

    let validateEnvelope (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        let provenance = model.Provenance
        let evidence = model.EvidenceObligations

        [ if model.SchemaVersion <> 1 then
              yield
                  diagnostic
                      "SPEC-SCHEMA-UNSUPPORTED"
                      "/schemaVersion"
                      "Only specification schema version 1 is supported."

          yield! blank "SPEC-CONTRACT-KIND" "/extensionKind" "Extension kind" contract.Kind

          if contract.SchemaVersion <= 0 then
              yield
                  diagnostic
                      "SPEC-CONTRACT-SCHEMA"
                      "/extensionSchemaVersion"
                      "Extension schema version must be positive."

          yield! blank "SPEC-PROVENANCE-AGENT" "/provenance/agent" "Provenance agent" provenance.Agent
          yield! blank "SPEC-PROVENANCE-SESSION" "/provenance/session" "Provenance session" provenance.Session
          yield! blank "SPEC-PROVENANCE-SOURCE" "/provenance/sourcePath" "Provenance source path" provenance.SourcePath

          if not (lowercaseHex provenance.SourceRevision) then
              yield
                  diagnostic
                      "SPEC-PROVENANCE-REVISION"
                      "/provenance/sourceRevision"
                      "Source revision must be a 40- or 64-character lowercase hexadecimal digest."

          match
              DateTimeOffset.TryParse(
                  provenance.AuthoredAtUtc,
                  CultureInfo.InvariantCulture,
                  DateTimeStyles.RoundtripKind
              )
          with
          | true, _ -> ()
          | _ ->
              yield
                  diagnostic
                      "SPEC-PROVENANCE-TIME"
                      "/provenance/authoredAtUtc"
                      "Authored time must be an ISO-8601 instant."

          yield! blank "SPEC-INTENT-REQUIRED" "/intent" "Authoring intent" model.Intent

          for index, obligation in evidence |> List.indexed do
              yield!
                  blank
                      "SPEC-EVIDENCE-KIND-REQUIRED"
                      $"/evidenceObligations/%d{index}/kind"
                      "Evidence kind"
                      obligation.Kind

              yield!
                  blank
                      "SPEC-EVIDENCE-DESCRIPTION-REQUIRED"
                      $"/evidenceObligations/%d{index}/description"
                      "Evidence description"
                      obligation.Description

          for duplicate, count in evidence |> List.countBy _.Id do
              if count > 1 then
                  yield
                      diagnostic
                          "SPEC-EVIDENCE-ID-DUPLICATE"
                          "/evidenceObligations"
                          $"Evidence obligation '%s{SpecificationId.value duplicate}' is declared more than once."

          yield! contract.Validate evidence model.Extension ]
        |> sortDiagnostics

    let writeObligation (writer: Utf8JsonWriter) (obligation: EvidenceObligation) =
        writer.WriteStartObject()
        writer.WriteString("id", SpecificationId.value obligation.Id)
        writer.WriteString("kind", obligation.Kind)
        writer.WriteString("description", obligation.Description)
        writer.WriteEndObject()

    let serializeModel (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-specification/v1")
        writer.WriteNumber("schemaVersion", model.SchemaVersion)
        writer.WriteString("identity", SpecificationId.value model.Identity)
        writer.WriteStartObject("provenance")
        writer.WriteString("agent", model.Provenance.Agent)
        writer.WriteString("session", model.Provenance.Session)
        writer.WriteString("sourcePath", model.Provenance.SourcePath)
        writer.WriteString("sourceRevision", model.Provenance.SourceRevision)
        writer.WriteString("authoredAtUtc", model.Provenance.AuthoredAtUtc)
        writer.WriteEndObject()
        writer.WriteString("intent", model.Intent)
        writer.WriteStartArray("evidenceObligations")

        model.EvidenceObligations
        |> List.sortBy (fun item -> SpecificationId.value item.Id)
        |> List.iter (writeObligation writer)

        writer.WriteEndArray()
        writer.WriteString("extensionKind", contract.Kind)
        writer.WriteNumber("extensionSchemaVersion", contract.SchemaVersion)
        writer.WritePropertyName("extension")
        contract.WriteJson writer model.Extension
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let tryProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let requiredString path (name: string) (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String ->
            match value.GetString() |> Option.ofObj with
            | Some text -> Ok text
            | None -> Error [ diagnostic "SPEC-CODEC-TYPE" path $"Property '%s{name}' cannot be null." ]
        | Some _ -> Error [ diagnostic "SPEC-CODEC-TYPE" path $"Property '%s{name}' must be a string." ]
        | None -> Error [ diagnostic "SPEC-CODEC-REQUIRED" path $"Property '%s{name}' is required." ]

    let requiredInt path (name: string) (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Ok number
            | _ -> Error [ diagnostic "SPEC-CODEC-TYPE" path $"Property '%s{name}' must be a 32-bit integer." ]
        | Some _ -> Error [ diagnostic "SPEC-CODEC-TYPE" path $"Property '%s{name}' must be an integer." ]
        | None -> Error [ diagnostic "SPEC-CODEC-REQUIRED" path $"Property '%s{name}' is required." ]

    let combine6 a b c d e f construct =
        match a, b, c, d, e, f with
        | Ok av, Ok bv, Ok cv, Ok dv, Ok ev, Ok fv -> Ok(construct av bv cv dv ev fv)
        | _ ->
            [ a; b; c; d; e; f ]
            |> List.collect (function
                | Error errors -> errors
                | Ok _ -> [])
            |> Error

[<RequireQualifiedAccess>]
module SpecificationCompiler =
    let validate contract model = Kernel.validateEnvelope contract model

    let normalize contract model =
        match validate contract model with
        | [] -> Ok(Kernel.normalizedBytes contract model)
        | diagnostics -> Error diagnostics

    let fingerprint contract model =
        normalize contract model |> Result.map Kernel.sha256Bytes

    let compile contract model =
        normalize contract model
        |> Result.map (fun bytes ->
            { Model = model
              NormalizedBytes = bytes
              Fingerprint = Kernel.sha256Bytes bytes })

    let semanticDiff contract before after =
        let diagnostics =
            validate contract before @ validate contract after |> Kernel.sortDiagnostics

        if not (List.isEmpty diagnostics) then
            Error diagnostics
        else
            let digest = Kernel.sha256Bytes
            let text value = Kernel.frameText value |> digest
            let integer value = Kernel.int32 value |> digest

            let evidence model =
                Kernel.evidenceBytes model.EvidenceObligations |> digest

            let extension model =
                contract.EncodeCanonical model.Extension |> digest

            let changes: SemanticChange list =
                [ if before.Identity <> after.Identity then
                      yield
                          { Path = "/identity"
                            Summary = "Specification identity changed."
                            BeforeFingerprint = text (SpecificationId.value before.Identity)
                            AfterFingerprint = text (SpecificationId.value after.Identity) }

                  if before.SchemaVersion <> after.SchemaVersion then
                      yield
                          { Path = "/schemaVersion"
                            Summary = "Specification schema version changed."
                            BeforeFingerprint = integer before.SchemaVersion
                            AfterFingerprint = integer after.SchemaVersion }

                  if before.Provenance.SourcePath <> after.Provenance.SourcePath then
                      yield
                          { Path = "/provenance/sourcePath"
                            Summary = "Authoritative source path changed."
                            BeforeFingerprint = text before.Provenance.SourcePath
                            AfterFingerprint = text after.Provenance.SourcePath }

                  if before.Provenance.SourceRevision <> after.Provenance.SourceRevision then
                      yield
                          { Path = "/provenance/sourceRevision"
                            Summary = "Authoritative source revision changed."
                            BeforeFingerprint = text before.Provenance.SourceRevision
                            AfterFingerprint = text after.Provenance.SourceRevision }

                  if evidence before <> evidence after then
                      yield
                          { Path = "/evidenceObligations"
                            Summary = "Evidence obligations changed."
                            BeforeFingerprint = evidence before
                            AfterFingerprint = evidence after }

                  if extension before <> extension after then
                      yield
                          { Path = "/extension"
                            Summary = "Typed extension semantics changed."
                            BeforeFingerprint = extension before
                            AfterFingerprint = extension after } ]

            Ok(if List.isEmpty changes then Equivalent else Changed changes)

[<RequireQualifiedAccess>]
module SpecificationCodec =
    let serialize (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        match SpecificationCompiler.validate contract model with
        | [] -> Ok(Kernel.serializeModel contract model)
        | diagnostics -> Error diagnostics

    let deserialize (contract: ExtensionContract<'extension>) (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error [ Kernel.diagnostic "SPEC-CODEC-TYPE" "/" "Specification JSON root must be an object." ]
            else
                let known =
                    set
                        [ "schema"
                          "schemaVersion"
                          "identity"
                          "provenance"
                          "intent"
                          "evidenceObligations"
                          "extensionKind"
                          "extensionSchemaVersion"
                          "extension" ]

                let unknown =
                    root.EnumerateObject()
                    |> Seq.choose (fun property ->
                        if Set.contains property.Name known then
                            None
                        else
                            Some(
                                Kernel.diagnostic
                                    "SPEC-CODEC-UNKNOWN-FIELD"
                                    $"/%s{property.Name}"
                                    $"Unknown specification envelope field '%s{property.Name}'."
                            ))
                    |> List.ofSeq

                if not (List.isEmpty unknown) then
                    Error(Kernel.sortDiagnostics unknown)
                else
                    let schema = Kernel.requiredString "/schema" "schema" root
                    let schemaVersion = Kernel.requiredInt "/schemaVersion" "schemaVersion" root
                    let identityText = Kernel.requiredString "/identity" "identity" root
                    let intent = Kernel.requiredString "/intent" "intent" root
                    let extensionKind = Kernel.requiredString "/extensionKind" "extensionKind" root

                    let extensionVersion =
                        Kernel.requiredInt "/extensionSchemaVersion" "extensionSchemaVersion" root

                    let provenance =
                        match Kernel.tryProperty "provenance" root with
                        | Some value when value.ValueKind = JsonValueKind.Object ->
                            Kernel.combine6
                                (Kernel.requiredString "/provenance/agent" "agent" value)
                                (Kernel.requiredString "/provenance/session" "session" value)
                                (Kernel.requiredString "/provenance/sourcePath" "sourcePath" value)
                                (Kernel.requiredString "/provenance/sourceRevision" "sourceRevision" value)
                                (Kernel.requiredString "/provenance/authoredAtUtc" "authoredAtUtc" value)
                                (Ok "")
                                (fun agent session sourcePath sourceRevision authoredAtUtc _ ->
                                    { Agent = agent
                                      Session = session
                                      SourcePath = sourcePath
                                      SourceRevision = sourceRevision
                                      AuthoredAtUtc = authoredAtUtc })
                        | Some _ ->
                            Error [ Kernel.diagnostic "SPEC-CODEC-TYPE" "/provenance" "Provenance must be an object." ]
                        | None ->
                            Error [ Kernel.diagnostic "SPEC-CODEC-REQUIRED" "/provenance" "Provenance is required." ]

                    let evidence =
                        match Kernel.tryProperty "evidenceObligations" root with
                        | Some value when value.ValueKind = JsonValueKind.Array ->
                            value.EnumerateArray()
                            |> Seq.mapi (fun index item ->
                                if item.ValueKind <> JsonValueKind.Object then
                                    Error
                                        [ Kernel.diagnostic
                                              "SPEC-CODEC-TYPE"
                                              $"/evidenceObligations/%d{index}"
                                              "Evidence obligation must be an object." ]
                                else
                                    match
                                        Kernel.requiredString $"/evidenceObligations/%d{index}/id" "id" item,
                                        Kernel.requiredString $"/evidenceObligations/%d{index}/kind" "kind" item,
                                        Kernel.requiredString
                                            $"/evidenceObligations/%d{index}/description"
                                            "description"
                                            item
                                    with
                                    | Ok idText, Ok kind, Ok description ->
                                        match SpecificationId.create idText with
                                        | Ok identifier ->
                                            Ok
                                                { Id = identifier
                                                  Kind = kind
                                                  Description = description }
                                        | Error message ->
                                            Error
                                                [ Kernel.diagnostic
                                                      "SPEC-ID-MALFORMED"
                                                      $"/evidenceObligations/%d{index}/id"
                                                      message ]
                                    | a, b, c ->
                                        [ a; b; c ]
                                        |> List.collect (function
                                            | Error errors -> errors
                                            | Ok _ -> [])
                                        |> Error)
                            |> List.ofSeq
                            |> fun rows ->
                                let errors =
                                    rows
                                    |> List.collect (function
                                        | Error findings -> findings
                                        | Ok _ -> [])

                                if List.isEmpty errors then
                                    Ok(
                                        rows
                                        |> List.choose (function
                                            | Ok row -> Some row
                                            | _ -> None)
                                    )
                                else
                                    Error errors
                        | Some _ ->
                            Error
                                [ Kernel.diagnostic
                                      "SPEC-CODEC-TYPE"
                                      "/evidenceObligations"
                                      "Evidence obligations must be an array." ]
                        | None ->
                            Error
                                [ Kernel.diagnostic
                                      "SPEC-CODEC-REQUIRED"
                                      "/evidenceObligations"
                                      "Evidence obligations are required." ]

                    let extension =
                        match Kernel.tryProperty "extension" root with
                        | Some value -> contract.DecodeJson(value.Clone())
                        | None ->
                            Error [ Kernel.diagnostic "SPEC-CODEC-REQUIRED" "/extension" "Extension is required." ]

                    let basicErrors =
                        [ schema |> Result.map ignore
                          schemaVersion |> Result.map ignore
                          identityText |> Result.map ignore
                          provenance |> Result.map ignore
                          intent |> Result.map ignore
                          evidence |> Result.map ignore
                          extensionKind |> Result.map ignore
                          extensionVersion |> Result.map ignore
                          extension |> Result.map ignore ]
                        |> List.collect (function
                            | Error errors -> errors
                            | Ok _ -> [])

                    if not (List.isEmpty basicErrors) then
                        Error(Kernel.sortDiagnostics basicErrors)
                    else
                        let schemaValue = Result.defaultValue "" schema
                        let versionValue = Result.defaultValue 0 schemaVersion
                        let kindValue = Result.defaultValue "" extensionKind
                        let extensionVersionValue = Result.defaultValue 0 extensionVersion

                        let contractErrors =
                            [ if schemaValue <> "fsgg.typed-specification/v1" then
                                  yield
                                      Kernel.diagnostic
                                          "SPEC-CODEC-SCHEMA"
                                          "/schema"
                                          "Specification schema marker is unsupported."
                              if versionValue <> 1 then
                                  yield
                                      Kernel.diagnostic
                                          "SPEC-SCHEMA-UNSUPPORTED"
                                          "/schemaVersion"
                                          "Only specification schema version 1 is supported."
                              if kindValue <> contract.Kind then
                                  yield
                                      Kernel.diagnostic
                                          "SPEC-EXTENSION-KIND"
                                          "/extensionKind"
                                          $"Expected extension kind '%s{contract.Kind}'."
                              if extensionVersionValue <> contract.SchemaVersion then
                                  yield
                                      Kernel.diagnostic
                                          "SPEC-EXTENSION-SCHEMA"
                                          "/extensionSchemaVersion"
                                          $"Expected extension schema version %d{contract.SchemaVersion}." ]

                        match SpecificationId.create (Result.defaultValue "" identityText) with
                        | Error message ->
                            Error(
                                Kernel.diagnostic "SPEC-ID-MALFORMED" "/identity" message :: contractErrors
                                |> Kernel.sortDiagnostics
                            )
                        | Ok identity when not (List.isEmpty contractErrors) ->
                            Error(Kernel.sortDiagnostics contractErrors)
                        | Ok identity ->
                            let model =
                                { Identity = identity
                                  SchemaVersion = versionValue
                                  Provenance = Result.defaultWith (fun _ -> failwith "validated") provenance
                                  Intent = Result.defaultValue "" intent
                                  EvidenceObligations = Result.defaultValue [] evidence
                                  Extension = Result.defaultWith (fun _ -> failwith "validated") extension }

                            match SpecificationCompiler.validate contract model with
                            | [] -> Ok model
                            | diagnostics -> Error diagnostics
        with :? JsonException as error ->
            Error
                [ Kernel.located
                      "SPEC-CODEC-MALFORMED"
                      "/"
                      "Specification JSON is malformed."
                      { Line = int (error.LineNumber.GetValueOrDefault()) + 1
                        Column = int (error.BytePositionInLine.GetValueOrDefault()) + 1 } ]

[<RequireQualifiedAccess>]
module SpecificationProjection =
    let private marker = "<!-- fsgg-typed-specification/v1 -->"
    let private sourcePrefix = "<!-- source-fingerprint: "
    let private generatedPrefix = "<!-- generated-fingerprint: "

    let private markerValue prefix (line: string) =
        if
            line.StartsWith(prefix, StringComparison.Ordinal)
            && line.EndsWith(" -->", StringComparison.Ordinal)
        then
            Some(line.Substring(prefix.Length, line.Length - prefix.Length - 4))
        else
            None

    let generate contract model =
        match SpecificationCompiler.fingerprint contract model, SpecificationCodec.serialize contract model with
        | Ok sourceFingerprint, Ok modelJson ->
            let evidenceLines =
                model.EvidenceObligations
                |> List.sortBy (fun item -> SpecificationId.value item.Id)
                |> List.map (fun item ->
                    $"- `%s{SpecificationId.value item.Id}` (`%s{item.Kind}`): %s{item.Description}")
                |> function
                    | [] -> [ "- None." ]
                    | lines -> lines

            let body =
                [ $"# Specification %s{SpecificationId.value model.Identity}"
                  ""
                  $"- Schema: `%d{model.SchemaVersion}`"
                  $"- Extension: `%s{contract.Kind}/%d{contract.SchemaVersion}`"
                  $"- Source: `%s{model.Provenance.SourcePath}@%s{model.Provenance.SourceRevision}`"
                  ""
                  "## Intent"
                  ""
                  model.Intent
                  ""
                  "## Evidence obligations"
                  "" ]
                @ evidenceLines
                @ [ ""; "## Extension"; "" ]
                @ contract.ProjectMarkdown model.Extension
                |> String.concat "\n"

            let generatedFingerprint = Kernel.sha256Text body

            let markdown =
                String.concat
                    "\n"
                    [ marker
                      $"%s{sourcePrefix}%s{sourceFingerprint} -->"
                      $"%s{generatedPrefix}%s{generatedFingerprint} -->"
                      body
                      "" ]

            use modelDocument = JsonDocument.Parse modelJson
            use stream = new MemoryStream()
            use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
            writer.WriteStartObject()
            writer.WriteString("schema", "fsgg.typed-specification-projection/v1")
            writer.WriteString("modelId", SpecificationId.value model.Identity)
            writer.WriteString("sourceFingerprint", sourceFingerprint)
            writer.WriteString("generatedFingerprint", generatedFingerprint)
            writer.WritePropertyName("model")
            modelDocument.RootElement.WriteTo writer
            writer.WriteEndObject()
            writer.Flush()
            let json = Encoding.UTF8.GetString(stream.ToArray()) + "\n"

            Ok
                { Markdown = markdown
                  Json = json
                  SourceFingerprint = sourceFingerprint
                  GeneratedFingerprint = generatedFingerprint }
        | Error diagnostics, _
        | _, Error diagnostics -> Error diagnostics

    let private readObservation kind =
        function
        | Missing ->
            Error
                [ Kernel.diagnostic "SPEC-PROJECTION-MISSING" $"/projection/%s{kind}" $"%s{kind} projection is missing." ]
        | Unreadable detail ->
            Error
                [ Kernel.diagnostic
                      "SPEC-PROJECTION-UNREADABLE"
                      $"/projection/%s{kind}"
                      $"%s{kind} projection is unreadable: %s{detail}" ]
        | Content text -> Ok text

    let validateMarkdown contract model observation =
        match readObservation "markdown" observation, generate contract model with
        | Error diagnostics, _ -> diagnostics
        | _, Error diagnostics -> diagnostics
        | Ok text, Ok expected ->
            let lines = text.Replace("\r\n", "\n").Split('\n')

            if lines.Length < 4 then
                [ Kernel.diagnostic
                      "SPEC-PROJECTION-MALFORMED"
                      "/projection/markdown"
                      "Markdown projection markers are incomplete." ]
            elif lines[0] <> marker then
                let code =
                    if lines[0].StartsWith("<!-- fsgg-typed-specification/", StringComparison.Ordinal) then
                        "SPEC-PROJECTION-VERSION"
                    else
                        "SPEC-PROJECTION-MALFORMED"

                [ Kernel.diagnostic
                      code
                      "/projection/markdown/schema"
                      "Markdown projection schema marker is missing or unsupported." ]
            else
                match markerValue sourcePrefix lines[1], markerValue generatedPrefix lines[2] with
                | Some source, Some generated ->
                    let body =
                        lines
                        |> Array.skip 3
                        |> String.concat "\n"
                        |> fun value -> value.TrimEnd('\n')

                    [ if source <> expected.SourceFingerprint then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-STALE"
                                  "/projection/markdown/sourceFingerprint"
                                  "Markdown projection was generated from a different specification fingerprint."
                      if
                          generated <> Kernel.sha256Text body
                          || generated <> expected.GeneratedFingerprint
                      then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-DIRECT-EDIT"
                                  "/projection/markdown/generatedFingerprint"
                                  "Markdown projection body differs from its generated source." ]
                    |> Kernel.sortDiagnostics
                | _ ->
                    [ Kernel.diagnostic
                          "SPEC-PROJECTION-MALFORMED"
                          "/projection/markdown"
                          "Markdown projection fingerprint markers are malformed." ]

    let validateJson contract model observation =
        match readObservation "json" observation, generate contract model with
        | Error diagnostics, _ -> diagnostics
        | _, Error diagnostics -> diagnostics
        | Ok text, Ok expected ->
            try
                use document = JsonDocument.Parse text
                let root = document.RootElement

                match
                    Kernel.requiredString "/projection/json/schema" "schema" root,
                    Kernel.requiredString "/projection/json/sourceFingerprint" "sourceFingerprint" root,
                    Kernel.requiredString "/projection/json/generatedFingerprint" "generatedFingerprint" root,
                    Kernel.tryProperty "model" root
                with
                | Ok schema, Ok source, Ok generated, Some embedded ->
                    [ if schema <> "fsgg.typed-specification-projection/v1" then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-VERSION"
                                  "/projection/json/schema"
                                  "JSON projection schema is unsupported."
                      if source <> expected.SourceFingerprint then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-STALE"
                                  "/projection/json/sourceFingerprint"
                                  "JSON projection was generated from a different specification fingerprint."
                      if generated <> expected.GeneratedFingerprint then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-DIRECT-EDIT"
                                  "/projection/json/generatedFingerprint"
                                  "JSON projection fingerprint differs from the generated source."
                      if text.Replace("\r\n", "\n") <> expected.Json then
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-DIRECT-EDIT"
                                  "/projection/json"
                                  "JSON projection bytes differ from the deterministic generated projection."
                      match SpecificationCodec.deserialize contract (embedded.GetRawText()) with
                      | Error _ ->
                          yield
                              Kernel.diagnostic
                                  "SPEC-PROJECTION-DIRECT-EDIT"
                                  "/projection/json/model"
                                  "JSON projection embeds a malformed or edited model."
                      | Ok embeddedModel ->
                          match SpecificationCompiler.semanticDiff contract model embeddedModel with
                          | Ok Equivalent -> ()
                          | _ ->
                              yield
                                  Kernel.diagnostic
                                      "SPEC-PROJECTION-DIRECT-EDIT"
                                      "/projection/json/model"
                                      "JSON projection embeds different model semantics." ]
                    |> Kernel.sortDiagnostics
                | _ ->
                    [ Kernel.diagnostic
                          "SPEC-PROJECTION-MALFORMED"
                          "/projection/json"
                          "JSON projection is missing required fields." ]
            with :? JsonException ->
                [ Kernel.diagnostic "SPEC-PROJECTION-MALFORMED" "/projection/json" "JSON projection is malformed." ]

[<RequireQualifiedAccess>]
module SpecificationEvidence =
    let validate obligations receipts =
        let obligationsById = obligations |> List.groupBy _.Id |> Map.ofList
        let receiptsById = receipts |> List.groupBy _.ObligationId |> Map.ofList

        let diagnostics =
            [ for id, rows in obligationsById |> Map.toList do
                  if rows.Length > 1 then
                      yield
                          Kernel.diagnostic
                              "SPEC-EVIDENCE-OBLIGATION-DUPLICATE"
                              "/evidenceObligations"
                              $"Obligation '%s{SpecificationId.value id}' is declared more than once."

              for id, rows in receiptsById |> Map.toList do
                  match Map.tryFind id obligationsById with
                  | None ->
                      yield
                          Kernel.diagnostic
                              "SPEC-EVIDENCE-UNKNOWN"
                              "/evidenceReceipts"
                              $"Receipt references unknown obligation '%s{SpecificationId.value id}'."
                  | Some obligationsForId ->
                      if rows.Length > 1 then
                          yield
                              Kernel.diagnostic
                                  "SPEC-EVIDENCE-DUPLICATE"
                                  "/evidenceReceipts"
                                  $"Obligation '%s{SpecificationId.value id}' has duplicate receipts."

                      let expectedKind = obligationsForId.Head.Kind

                      for row in rows do
                          if row.Kind <> expectedKind then
                              yield
                                  Kernel.diagnostic
                                      "SPEC-EVIDENCE-KIND"
                                      "/evidenceReceipts"
                                      $"Receipt for '%s{SpecificationId.value id}' has kind '%s{row.Kind}', expected '%s{expectedKind}'."

                          if String.IsNullOrWhiteSpace row.EvidenceRef then
                              yield
                                  Kernel.diagnostic
                                      "SPEC-EVIDENCE-REF-REQUIRED"
                                      "/evidenceReceipts"
                                      $"Receipt for '%s{SpecificationId.value id}' requires an evidence reference."

              for id, rows in obligationsById |> Map.toList do
                  let expectedKind = rows.Head.Kind

                  let satisfied =
                      receiptsById
                      |> Map.tryFind id
                      |> Option.defaultValue []
                      |> List.exists (fun receipt ->
                          receipt.Kind = expectedKind
                          && not (String.IsNullOrWhiteSpace receipt.EvidenceRef))

                  if not satisfied then
                      yield
                          Kernel.diagnostic
                              "SPEC-EVIDENCE-MISSING"
                              "/evidenceObligations"
                              $"Obligation '%s{SpecificationId.value id}' has no matching receipt." ]
            |> Kernel.sortDiagnostics

        let satisfied =
            obligationsById
            |> Map.toList
            |> List.choose (fun (id, rows) ->
                let expectedKind = rows.Head.Kind

                let valid =
                    receiptsById
                    |> Map.tryFind id
                    |> Option.defaultValue []
                    |> List.exists (fun receipt ->
                        receipt.Kind = expectedKind
                        && not (String.IsNullOrWhiteSpace receipt.EvidenceRef))

                if valid then Some id else None)
            |> List.sortBy SpecificationId.value

        { Satisfied = satisfied
          Diagnostics = diagnostics }
