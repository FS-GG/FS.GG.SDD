namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

[<RequireQualifiedAccess>]
type WorkspaceModuleKind =
    | ProductSpecification
    | Decision
    | WorkChange
    | RepositoryProfile
    | CiObligation
    | ExternalContract
    | EvidenceRequirement

type WorkspaceModule =
    { Id: SpecificationId
      Kind: WorkspaceModuleKind
      ContentSha256: string
      References: SpecificationId list
      Assumptions: string list
      EvidenceObligationIds: SpecificationId list }

type WorkspaceModel =
    { SchemaVersion: int
      Revision: int64
      Modules: WorkspaceModule list }

[<RequireQualifiedAccess>]
type AuthoringDepth =
    | Freeform
    | StructuredSdd
    | DirectQuint

[<RequireQualifiedAccess>]
type WorkspaceChange =
    | Upsert of WorkspaceModule
    | Remove of SpecificationId
    | Rename of fromId: SpecificationId * toId: SpecificationId

type OpaqueAcceptance =
    { Debt: string
      AffectedSubjects: SpecificationId list
      Reason: string
      ResponsibleHuman: string
      EvidenceRefs: string list }

[<RequireQualifiedAccess>]
type ProposalDisposition =
    | CoherentDelta
    | NoSemanticChange of reason: string
    | AcceptedOpaque of OpaqueAcceptance
    | Ambiguous of reason: string
    | Contradictory of reason: string
    | Stale of reason: string

type ChangeProposal =
    { SchemaVersion: int
      IssueRef: string
      ProseSha256: string
      BaseFingerprint: string
      AuthoringDepth: AuthoringDepth
      Changes: WorkspaceChange list
      Disposition: ProposalDisposition
      EvidenceFingerprint: string option }

type HumanAcceptance =
    { AcceptedBy: string
      EvidenceRefs: string list
      AcceptedAtUtc: string }

type WorkspaceSemanticChange =
    { Subject: SpecificationId
      Summary: string }

type WorkspaceReconciliation =
    | Reconciled of WorkspaceModel * WorkspaceSemanticChange list
    | Conflicted of SpecificationDiagnostic list

module private WorkspaceInternals =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = None }

    let idText = SpecificationId.value

    let isSha256 (value: string) =
        value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let nonBlank (code: string) (path: string) (label: string) (value: string) =
        if String.IsNullOrWhiteSpace value then
            [ diagnostic code path $"{label} is required." ]
        else
            []

    let kindValue kind =
        match kind with
        | WorkspaceModuleKind.ProductSpecification -> "product-specification"
        | WorkspaceModuleKind.Decision -> "decision"
        | WorkspaceModuleKind.WorkChange -> "work-change"
        | WorkspaceModuleKind.RepositoryProfile -> "repository-profile"
        | WorkspaceModuleKind.CiObligation -> "ci-obligation"
        | WorkspaceModuleKind.ExternalContract -> "external-contract"
        | WorkspaceModuleKind.EvidenceRequirement -> "evidence-requirement"

    let parseKind (path: string) =
        function
        | "product-specification" -> WorkspaceModuleKind.ProductSpecification
        | "decision" -> WorkspaceModuleKind.Decision
        | "work-change" -> WorkspaceModuleKind.WorkChange
        | "repository-profile" -> WorkspaceModuleKind.RepositoryProfile
        | "ci-obligation" -> WorkspaceModuleKind.CiObligation
        | "external-contract" -> WorkspaceModuleKind.ExternalContract
        | "evidence-requirement" -> WorkspaceModuleKind.EvidenceRequirement
        | value -> raise (JsonException($"{path}: unknown module kind '{value}'."))

    let sortDiagnostics (findings: SpecificationDiagnostic list) =
        findings |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let moduleMap (model: WorkspaceModel) =
        model.Modules |> List.map (fun item -> idText item.Id, item) |> Map.ofList

    let touched change =
        match change with
        | WorkspaceChange.Upsert item -> Set.singleton (idText item.Id)
        | WorkspaceChange.Remove identifier -> Set.singleton (idText identifier)
        | WorkspaceChange.Rename(fromId, toId) -> Set.ofList [ idText fromId; idText toId ]

    let changeSummary change =
        match change with
        | WorkspaceChange.Upsert item -> item.Id, $"upsert {kindValue item.Kind}"
        | WorkspaceChange.Remove identifier -> identifier, "remove"
        | WorkspaceChange.Rename(fromId, toId) -> fromId, $"rename to {idText toId}"

    let writeStringArray (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray name
        values |> List.iter writer.WriteStringValue
        writer.WriteEndArray()

    let canonicalBytesUnchecked (model: WorkspaceModel) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", model.SchemaVersion)
        writer.WriteNumber("revision", model.Revision)
        writer.WriteStartArray("modules")

        model.Modules
        |> List.sortBy (fun item -> idText item.Id)
        |> List.iter (fun item ->
            writer.WriteStartObject()
            writer.WriteString("id", idText item.Id)
            writer.WriteString("kind", kindValue item.Kind)
            writer.WriteString("contentSha256", item.ContentSha256)

            item.References
            |> List.map idText
            |> List.sort
            |> writeStringArray writer "references"

            item.Assumptions |> List.sort |> writeStringArray writer "assumptions"

            item.EvidenceObligationIds
            |> List.map idText
            |> List.sort
            |> writeStringArray writer "evidenceObligationIds"

            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        stream.ToArray()

    let checkFields path allowed (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            raise (JsonException($"{path}: expected object."))

        let names = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList

        match names |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
        | Some(name, _) -> raise (JsonException($"{path}/{name}: duplicate field."))
        | None -> ()

        match names |> List.tryFind (fun name -> not (Set.contains name allowed)) with
        | Some name -> raise (JsonException($"{path}/{name}: unknown field."))
        | None -> ()

    let property (name: string) (element: JsonElement) =
        match element.TryGetProperty name with
        | true, value -> value
        | _ -> raise (JsonException($"Missing required field '{name}'."))

    let stringProperty (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.String then
            raise (JsonException($"Field '{name}' must be a string."))

        value.GetString()
        |> Option.ofObj
        |> Option.defaultWith (fun () -> raise (JsonException($"Field '{name}' is null.")))

    let int64Property (name: string) (element: JsonElement) =
        match (property name element).TryGetInt64() with
        | true, value -> value
        | _ -> raise (JsonException($"Field '{name}' must be an integer."))

    let stringArray (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Array then
            raise (JsonException($"Field '{name}' must be an array."))

        value.EnumerateArray()
        |> Seq.map (fun item ->
            if item.ValueKind <> JsonValueKind.String then
                raise (JsonException($"Field '{name}' must contain strings."))

            item.GetString()
            |> Option.ofObj
            |> Option.defaultWith (fun () -> raise (JsonException($"Field '{name}' contains null."))))
        |> Seq.toList

    let parseId (path: string) (value: string) =
        SpecificationId.create value
        |> Result.defaultWith (fun message -> raise (JsonException($"{path}: {message}")))

    let moduleFromJson path (element: JsonElement) =
        checkFields
            path
            (set
                [ "id"
                  "kind"
                  "contentSha256"
                  "references"
                  "assumptions"
                  "evidenceObligationIds" ])
            element

        { Id = stringProperty "id" element |> parseId (path + "/id")
          Kind = stringProperty "kind" element |> parseKind (path + "/kind")
          ContentSha256 = stringProperty "contentSha256" element
          References = stringArray "references" element |> List.map (parseId (path + "/references"))
          Assumptions = stringArray "assumptions" element
          EvidenceObligationIds =
            stringArray "evidenceObligationIds" element
            |> List.map (parseId (path + "/evidenceObligationIds")) }

    let moduleToJson (writer: Utf8JsonWriter) (item: WorkspaceModule) =
        writer.WriteStartObject()
        writer.WriteString("id", idText item.Id)
        writer.WriteString("kind", kindValue item.Kind)
        writer.WriteString("contentSha256", item.ContentSha256)

        item.References
        |> List.map idText
        |> List.sort
        |> writeStringArray writer "references"

        item.Assumptions |> List.sort |> writeStringArray writer "assumptions"

        item.EvidenceObligationIds
        |> List.map idText
        |> List.sort
        |> writeStringArray writer "evidenceObligationIds"

        writer.WriteEndObject()

    let jsonFailure code (ex: exn) =
        Error [ diagnostic code "/" ex.Message ]

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let applyChanges (model: WorkspaceModel) (changes: WorkspaceChange list) =
        let folder modules change =
            match change with
            | WorkspaceChange.Upsert item -> modules |> Map.add (idText item.Id) item
            | WorkspaceChange.Remove identifier -> modules |> Map.remove (idText identifier)
            | WorkspaceChange.Rename(fromId, toId) ->
                match modules |> Map.tryFind (idText fromId) with
                | None -> modules
                | Some item ->
                    modules
                    |> Map.remove (idText fromId)
                    |> Map.add (idText toId) { item with Id = toId }

        let modules = changes |> List.fold folder (moduleMap model)

        { model with
            Revision = model.Revision + 1L
            Modules = modules |> Map.toList |> List.map snd }

[<RequireQualifiedAccess>]
module WorkspaceLifecycle =
    open WorkspaceInternals

    let validate model =
        let ids = model.Modules |> List.map (fun item -> idText item.Id)
        let idSet = ids |> Set.ofList

        let evidenceIds =
            model.Modules
            |> List.choose (fun item ->
                if item.Kind = WorkspaceModuleKind.EvidenceRequirement then
                    Some(idText item.Id)
                else
                    None)
            |> Set.ofList

        [ if model.SchemaVersion <> 1 then
              diagnostic "WORKSPACE-SCHEMA-UNSUPPORTED" "/schemaVersion" "Workspace schemaVersion must be 1."
          if model.Revision < 0L then
              diagnostic "WORKSPACE-REVISION" "/revision" "Workspace revision cannot be negative."

          for duplicate in ids |> List.countBy id |> List.filter (snd >> ((<) 1)) |> List.map fst do
              diagnostic "WORKSPACE-ID-DUPLICATE" "/modules" $"Module id '{duplicate}' is duplicated."

          for index, item in model.Modules |> List.indexed do
              let path = $"/modules/{index}"

              if not (isSha256 item.ContentSha256) then
                  diagnostic
                      "WORKSPACE-CONTENT-DIGEST"
                      $"{path}/contentSha256"
                      "contentSha256 must be lowercase SHA-256."

              for reference in item.References |> List.map idText |> List.distinct |> List.sort do
                  if not (Set.contains reference idSet) then
                      diagnostic
                          "WORKSPACE-REFERENCE-DANGLING"
                          $"{path}/references"
                          $"Reference '{reference}' does not resolve."

              for evidence in item.EvidenceObligationIds |> List.map idText |> List.distinct |> List.sort do
                  if not (Set.contains evidence evidenceIds) then
                      diagnostic
                          "WORKSPACE-EVIDENCE-DANGLING"
                          $"{path}/evidenceObligationIds"
                          $"Evidence obligation '{evidence}' does not resolve to an evidence module."

              if item.References.Length <> (item.References |> List.distinct).Length then
                  diagnostic "WORKSPACE-REFERENCE-DUPLICATE" $"{path}/references" "References must be unique."

              if item.Assumptions |> List.exists String.IsNullOrWhiteSpace then
                  diagnostic "WORKSPACE-ASSUMPTION-BLANK" $"{path}/assumptions" "Assumptions cannot be blank."

              if item.Assumptions.Length <> (item.Assumptions |> List.distinct).Length then
                  diagnostic "WORKSPACE-ASSUMPTION-DUPLICATE" $"{path}/assumptions" "Assumptions must be unique."

              if
                  item.EvidenceObligationIds.Length
                  <> (item.EvidenceObligationIds |> List.distinct).Length
              then
                  diagnostic
                      "WORKSPACE-EVIDENCE-DUPLICATE"
                      $"{path}/evidenceObligationIds"
                      "Evidence obligations must be unique." ]
        |> sortDiagnostics

    let canonicalBytes model =
        match validate model with
        | [] -> Ok(canonicalBytesUnchecked model)
        | findings -> Error findings

    let fingerprint model =
        canonicalBytes model |> Result.map sha256

    let serializeModel model =
        canonicalBytes model
        |> Result.map (fun bytes -> Encoding.UTF8.GetString(bytes) + "\n")

    let deserializeModel (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement
            checkFields "" (set [ "schemaVersion"; "revision"; "modules" ]) root
            let modules = property "modules" root

            if modules.ValueKind <> JsonValueKind.Array then
                raise (JsonException("Field 'modules' must be an array."))

            let model =
                { SchemaVersion = int (int64Property "schemaVersion" root)
                  Revision = int64Property "revision" root
                  Modules =
                    modules.EnumerateArray()
                    |> Seq.mapi (fun index item -> moduleFromJson $"/modules/{index}" item)
                    |> Seq.toList }

            match validate model with
            | [] -> Ok model
            | findings -> Error findings
        with
        | :? JsonException as ex -> jsonFailure "WORKSPACE-CODEC-INVALID" ex
        | :? FormatException as ex -> jsonFailure "WORKSPACE-CODEC-INVALID" ex

    let private depthValue =
        function
        | AuthoringDepth.Freeform -> "freeform"
        | AuthoringDepth.StructuredSdd -> "structured-sdd"
        | AuthoringDepth.DirectQuint -> "direct-quint"

    let private parseDepth =
        function
        | "freeform" -> AuthoringDepth.Freeform
        | "structured-sdd" -> AuthoringDepth.StructuredSdd
        | "direct-quint" -> AuthoringDepth.DirectQuint
        | value -> raise (JsonException($"/authoringDepth: unknown authoring depth '{value}'."))

    let private writeDisposition (writer: Utf8JsonWriter) =
        function
        | ProposalDisposition.CoherentDelta -> writer.WriteStringValue("coherent-delta")
        | ProposalDisposition.NoSemanticChange reason ->
            writer.WriteStartObject()
            writer.WriteString("kind", "no-semantic-change")
            writer.WriteString("reason", reason)
            writer.WriteEndObject()
        | ProposalDisposition.AcceptedOpaque opaque ->
            writer.WriteStartObject()
            writer.WriteString("kind", "accepted-opaque")
            writer.WriteString("debt", opaque.Debt)

            opaque.AffectedSubjects
            |> List.map idText
            |> List.sort
            |> writeStringArray writer "affectedSubjects"

            writer.WriteString("reason", opaque.Reason)
            writer.WriteString("responsibleHuman", opaque.ResponsibleHuman)
            opaque.EvidenceRefs |> List.sort |> writeStringArray writer "evidenceRefs"
            writer.WriteEndObject()
        | ProposalDisposition.Ambiguous reason ->
            writer.WriteStartObject()
            writer.WriteString("kind", "ambiguous")
            writer.WriteString("reason", reason)
            writer.WriteEndObject()
        | ProposalDisposition.Contradictory reason ->
            writer.WriteStartObject()
            writer.WriteString("kind", "contradictory")
            writer.WriteString("reason", reason)
            writer.WriteEndObject()
        | ProposalDisposition.Stale reason ->
            writer.WriteStartObject()
            writer.WriteString("kind", "stale")
            writer.WriteString("reason", reason)
            writer.WriteEndObject()

    let private parseDisposition (element: JsonElement) =
        if element.ValueKind = JsonValueKind.String then
            match element.GetString() with
            | "coherent-delta" -> ProposalDisposition.CoherentDelta
            | value -> raise (JsonException($"/disposition: unknown disposition '{value}'."))
        else
            let kind = stringProperty "kind" element

            match kind with
            | "accepted-opaque" ->
                checkFields
                    "/disposition"
                    (set
                        [ "kind"
                          "debt"
                          "affectedSubjects"
                          "reason"
                          "responsibleHuman"
                          "evidenceRefs" ])
                    element

                ProposalDisposition.AcceptedOpaque
                    { Debt = stringProperty "debt" element
                      AffectedSubjects =
                        stringArray "affectedSubjects" element
                        |> List.map (parseId "/disposition/affectedSubjects")
                      Reason = stringProperty "reason" element
                      ResponsibleHuman = stringProperty "responsibleHuman" element
                      EvidenceRefs = stringArray "evidenceRefs" element }
            | "no-semantic-change"
            | "ambiguous"
            | "contradictory"
            | "stale" ->
                checkFields "/disposition" (set [ "kind"; "reason" ]) element
                let reason = stringProperty "reason" element

                match kind with
                | "no-semantic-change" -> ProposalDisposition.NoSemanticChange reason
                | "ambiguous" -> ProposalDisposition.Ambiguous reason
                | "contradictory" -> ProposalDisposition.Contradictory reason
                | _ -> ProposalDisposition.Stale reason
            | value -> raise (JsonException($"/disposition/kind: unknown disposition '{value}'."))

    let serializeProposal (accepted: WorkspaceModel) (proposal: ChangeProposal) =
        let preflight =
            [ yield! validate accepted
              if proposal.SchemaVersion <> 1 then
                  diagnostic "WORKSPACE-PROPOSAL-SCHEMA" "/schemaVersion" "Proposal schemaVersion must be 1."
              match fingerprint accepted with
              | Ok expected when expected <> proposal.BaseFingerprint ->
                  diagnostic
                      "WORKSPACE-PROPOSAL-STALE"
                      "/baseFingerprint"
                      "Proposal does not target the current accepted fingerprint."
              | _ -> () ]
            |> sortDiagnostics

        match preflight with
        | _ :: _ as findings -> Error findings
        | [] ->
            use stream = new MemoryStream()
            use writer = new Utf8JsonWriter(stream)
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", proposal.SchemaVersion)
            writer.WriteString("issueRef", proposal.IssueRef)
            writer.WriteString("proseSha256", proposal.ProseSha256)
            writer.WriteString("baseFingerprint", proposal.BaseFingerprint)
            writer.WriteString("authoringDepth", depthValue proposal.AuthoringDepth)
            writer.WriteStartArray("changes")

            proposal.Changes
            |> List.sortBy (touched >> Set.toList)
            |> List.iter (fun change ->
                writer.WriteStartObject()

                match change with
                | WorkspaceChange.Upsert item ->
                    writer.WriteString("kind", "upsert")
                    writer.WritePropertyName("module")
                    moduleToJson writer item
                | WorkspaceChange.Remove identifier ->
                    writer.WriteString("kind", "remove")
                    writer.WriteString("id", idText identifier)
                | WorkspaceChange.Rename(fromId, toId) ->
                    writer.WriteString("kind", "rename")
                    writer.WriteString("fromId", idText fromId)
                    writer.WriteString("toId", idText toId)

                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WritePropertyName("disposition")
            writeDisposition writer proposal.Disposition

            match proposal.EvidenceFingerprint with
            | Some value -> writer.WriteString("evidenceFingerprint", value)
            | None -> writer.WriteNull("evidenceFingerprint")

            writer.WriteEndObject()
            writer.Flush()
            Ok(Encoding.UTF8.GetString(stream.ToArray()) + "\n")

    let deserializeProposal (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            checkFields
                ""
                (set
                    [ "schemaVersion"
                      "issueRef"
                      "proseSha256"
                      "baseFingerprint"
                      "authoringDepth"
                      "changes"
                      "disposition"
                      "evidenceFingerprint" ])
                root

            let changesElement = property "changes" root

            if changesElement.ValueKind <> JsonValueKind.Array then
                raise (JsonException("Field 'changes' must be an array."))

            let changes =
                changesElement.EnumerateArray()
                |> Seq.mapi (fun index item ->
                    let path = $"/changes/{index}"
                    let kind = stringProperty "kind" item

                    match kind with
                    | "upsert" ->
                        checkFields path (set [ "kind"; "module" ]) item
                        WorkspaceChange.Upsert(moduleFromJson (path + "/module") (property "module" item))
                    | "remove" ->
                        checkFields path (set [ "kind"; "id" ]) item
                        WorkspaceChange.Remove(stringProperty "id" item |> parseId (path + "/id"))
                    | "rename" ->
                        checkFields path (set [ "kind"; "fromId"; "toId" ]) item

                        WorkspaceChange.Rename(
                            stringProperty "fromId" item |> parseId (path + "/fromId"),
                            stringProperty "toId" item |> parseId (path + "/toId")
                        )
                    | value -> raise (JsonException($"{path}/kind: unknown change '{value}'.")))
                |> Seq.toList

            let evidence = property "evidenceFingerprint" root

            let proposal =
                { SchemaVersion = int (int64Property "schemaVersion" root)
                  IssueRef = stringProperty "issueRef" root
                  ProseSha256 = stringProperty "proseSha256" root
                  BaseFingerprint = stringProperty "baseFingerprint" root
                  AuthoringDepth = stringProperty "authoringDepth" root |> parseDepth
                  Changes = changes
                  Disposition = property "disposition" root |> parseDisposition
                  EvidenceFingerprint =
                    if evidence.ValueKind = JsonValueKind.Null then
                        None
                    elif evidence.ValueKind = JsonValueKind.String then
                        evidence.GetString() |> Option.ofObj
                    else
                        raise (JsonException("Field 'evidenceFingerprint' must be a string or null.")) }

            Ok proposal
        with
        | :? JsonException as ex -> jsonFailure "WORKSPACE-PROPOSAL-CODEC-INVALID" ex
        | :? FormatException as ex -> jsonFailure "WORKSPACE-PROPOSAL-CODEC-INVALID" ex

    let semanticDiff before after =
        match validate before @ validate after |> sortDiagnostics with
        | _ :: _ as findings -> Error findings
        | [] ->
            let beforeMap = moduleMap before
            let afterMap = moduleMap after

            let keys =
                Set.union (beforeMap |> Map.keys |> Set.ofSeq) (afterMap |> Map.keys |> Set.ofSeq)
                |> Set.toList
                |> List.sort

            keys
            |> List.choose (fun key ->
                match Map.tryFind key beforeMap, Map.tryFind key afterMap with
                | None, Some item ->
                    Some
                        { Subject = item.Id
                          Summary = $"add {kindValue item.Kind}" }
                | Some item, None ->
                    Some
                        { Subject = item.Id
                          Summary = $"remove {kindValue item.Kind}" }
                | Some oldItem, Some newItem when oldItem.Assumptions <> newItem.Assumptions ->
                    Some
                        { Subject = newItem.Id
                          Summary = "change assumptions" }
                | Some oldItem, Some newItem when oldItem <> newItem ->
                    Some
                        { Subject = newItem.Id
                          Summary = $"change {kindValue newItem.Kind}" }
                | _ -> None)
            |> Ok

    let validateProposal accepted proposal =
        let acceptedFingerprint = fingerprint accepted
        let acceptedModules = moduleMap accepted
        let touchedSubjects = proposal.Changes |> List.collect (touched >> Set.toList)

        [ yield! validate accepted
          if proposal.SchemaVersion <> 1 then
              diagnostic "WORKSPACE-PROPOSAL-SCHEMA" "/schemaVersion" "Proposal schemaVersion must be 1."
          yield! nonBlank "WORKSPACE-PROPOSAL-ISSUE" "/issueRef" "Issue reference" proposal.IssueRef
          if not (isSha256 proposal.ProseSha256) then
              diagnostic "WORKSPACE-PROPOSAL-PROSE" "/proseSha256" "proseSha256 must be lowercase SHA-256."

          match acceptedFingerprint with
          | Ok expected when proposal.BaseFingerprint <> expected ->
              diagnostic
                  "WORKSPACE-PROPOSAL-STALE"
                  "/baseFingerprint"
                  "Proposal does not target the current accepted fingerprint."
          | _ -> ()

          for duplicate in
              touchedSubjects
              |> List.countBy id
              |> List.filter (snd >> ((<) 1))
              |> List.map fst do
              diagnostic
                  "WORKSPACE-CHANGE-DUPLICATE"
                  "/changes"
                  $"Proposal edits identity '{duplicate}' more than once."

          for index, change in proposal.Changes |> List.indexed do
              match change with
              | WorkspaceChange.Remove identifier when not (Map.containsKey (idText identifier) acceptedModules) ->
                  diagnostic
                      "WORKSPACE-CHANGE-MISSING"
                      $"/changes/{index}"
                      $"Removed identity '{idText identifier}' is absent from the accepted base."
              | WorkspaceChange.Rename(fromId, _) when not (Map.containsKey (idText fromId) acceptedModules) ->
                  diagnostic
                      "WORKSPACE-CHANGE-MISSING"
                      $"/changes/{index}"
                      $"Renamed identity '{idText fromId}' is absent from the accepted base."
              | WorkspaceChange.Rename(_, toId) when Map.containsKey (idText toId) acceptedModules ->
                  diagnostic
                      "WORKSPACE-RENAME-TARGET"
                      $"/changes/{index}"
                      $"Rename target '{idText toId}' already exists."
              | _ -> ()

          match proposal.EvidenceFingerprint, acceptedFingerprint with
          | Some observed, Ok expected when observed <> expected ->
              diagnostic
                  "WORKSPACE-EVIDENCE-STALE"
                  "/evidenceFingerprint"
                  "Proposal evidence was collected against another accepted fingerprint."
          | Some observed, _ when not (isSha256 observed) ->
              diagnostic
                  "WORKSPACE-EVIDENCE-DIGEST"
                  "/evidenceFingerprint"
                  "Evidence fingerprint must be lowercase SHA-256."
          | _ -> ()

          match proposal.Disposition with
          | ProposalDisposition.AcceptedOpaque opaque ->
              yield! nonBlank "WORKSPACE-OPAQUE-DEBT" "/disposition/debt" "Opaque debt" opaque.Debt

              if List.isEmpty opaque.AffectedSubjects then
                  diagnostic
                      "WORKSPACE-OPAQUE-SUBJECT"
                      "/disposition/affectedSubjects"
                      "AcceptedOpaque requires affected subjects."

              yield! nonBlank "WORKSPACE-OPAQUE-REASON" "/disposition/reason" "Opaque reason" opaque.Reason

              yield!
                  nonBlank
                      "WORKSPACE-OPAQUE-HUMAN"
                      "/disposition/responsibleHuman"
                      "Responsible human"
                      opaque.ResponsibleHuman

              if
                  List.isEmpty opaque.EvidenceRefs
                  || opaque.EvidenceRefs |> List.exists String.IsNullOrWhiteSpace
              then
                  diagnostic
                      "WORKSPACE-OPAQUE-EVIDENCE"
                      "/disposition/evidenceRefs"
                      "AcceptedOpaque requires evidence references."
          | ProposalDisposition.NoSemanticChange reason ->
              yield! nonBlank "WORKSPACE-NO-CHANGE-REASON" "/disposition/reason" "No-semantic-change reason" reason

              if not (List.isEmpty proposal.Changes) then
                  diagnostic "WORKSPACE-NO-CHANGE-DELTA" "/changes" "NoSemanticChange cannot carry semantic changes."
          | ProposalDisposition.Ambiguous reason
          | ProposalDisposition.Contradictory reason
          | ProposalDisposition.Stale reason ->
              yield! nonBlank "WORKSPACE-DISPOSITION-REASON" "/disposition/reason" "Disposition reason" reason
          | ProposalDisposition.CoherentDelta -> () ]
        |> sortDiagnostics

    let private eligible proposal =
        match proposal.Disposition with
        | ProposalDisposition.CoherentDelta
        | ProposalDisposition.NoSemanticChange _
        | ProposalDisposition.AcceptedOpaque _ -> true
        | _ -> false

    let private validateAcceptance acceptance =
        [ yield! nonBlank "WORKSPACE-ACCEPTANCE-HUMAN" "/acceptance/acceptedBy" "Accepting human" acceptance.AcceptedBy
          yield!
              nonBlank
                  "WORKSPACE-ACCEPTANCE-TIME"
                  "/acceptance/acceptedAtUtc"
                  "Acceptance time"
                  acceptance.AcceptedAtUtc
          if
              List.isEmpty acceptance.EvidenceRefs
              || acceptance.EvidenceRefs |> List.exists String.IsNullOrWhiteSpace
          then
              diagnostic
                  "WORKSPACE-ACCEPTANCE-EVIDENCE"
                  "/acceptance/evidenceRefs"
                  "Human acceptance requires evidence references." ]

    let reduce accepted proposal acceptance =
        let findings =
            [ yield! validateProposal accepted proposal
              yield! validateAcceptance acceptance
              if not (eligible proposal) then
                  diagnostic
                      "WORKSPACE-PROPOSAL-NONREDUCIBLE"
                      "/disposition"
                      "Only CoherentDelta, NoSemanticChange, and AcceptedOpaque can reduce accepted authority." ]
            |> sortDiagnostics

        if not (List.isEmpty findings) then
            Error findings
        else
            let candidate = applyChanges accepted proposal.Changes

            match validate candidate with
            | [] -> Ok candidate
            | candidateFindings -> Error candidateFindings

    let reconcile accepted left right =
        let basicFindings = validateProposal accepted left @ validateProposal accepted right
        let leftTouched = left.Changes |> List.collect (touched >> Set.toList) |> Set.ofList

        let rightTouched =
            right.Changes |> List.collect (touched >> Set.toList) |> Set.ofList

        let overlapFindings =
            Set.intersect leftTouched rightTouched
            |> Set.toList
            |> List.sort
            |> List.map (fun subject ->
                diagnostic "WORKSPACE-MERGE-OVERLAP" "/changes" $"Both proposals edit '{subject}'.")

        let renamed proposal =
            proposal.Changes
            |> List.choose (function
                | WorkspaceChange.Rename(fromId, _) -> Some(idText fromId)
                | _ -> None)
            |> Set.ofList

        let removed proposal =
            proposal.Changes
            |> List.choose (function
                | WorkspaceChange.Remove identifier -> Some(idText identifier)
                | _ -> None)
            |> Set.ofList

        let renameDeleteFindings =
            Set.union (Set.intersect (renamed left) (removed right)) (Set.intersect (renamed right) (removed left))
            |> Set.toList
            |> List.map (fun subject ->
                diagnostic
                    "WORKSPACE-MERGE-RENAME-DELETE"
                    "/changes"
                    $"Identity '{subject}' is renamed and removed across proposals.")

        let eligibilityFindings =
            [ if not (eligible left) || not (eligible right) then
                  diagnostic
                      "WORKSPACE-MERGE-NONREDUCIBLE"
                      "/disposition"
                      "Both proposals must have an accepting disposition." ]

        match
            basicFindings @ overlapFindings @ renameDeleteFindings @ eligibilityFindings
            |> sortDiagnostics
        with
        | _ :: _ as findings -> Conflicted findings
        | [] ->
            let candidate = applyChanges accepted (left.Changes @ right.Changes)

            match validate candidate with
            | _ :: _ as findings -> Conflicted findings
            | [] ->
                let changes =
                    (left.Changes @ right.Changes)
                    |> List.map changeSummary
                    |> List.map (fun (subject, summary) -> { Subject = subject; Summary = summary })
                    |> List.sortBy (fun item -> idText item.Subject, item.Summary)

                Reconciled(candidate, changes)

[<RequireQualifiedAccess>]
type CorrespondenceObservationKind =
    | GeneratedContract
    | SourceBinding
    | Test
    | EvidenceReceipt

[<RequireQualifiedAccess>]
type CorrespondenceObservationState =
    | Observed
    | Missing
    | Contradicted of reason: string
    | Ambiguous of reason: string
    | Unsupported of reason: string

type CorrespondenceObservation =
    { ObligationId: SpecificationId
      Kind: CorrespondenceObservationKind
      AcceptedFingerprint: string
      SubjectFingerprint: string option
      State: CorrespondenceObservationState
      SourceBindings: string list
      TestBindings: string list
      EvidenceRefs: string list
      Explanation: string }

[<RequireQualifiedAccess>]
type CorrespondenceStatus =
    | Satisfied
    | Missing
    | Stale
    | Contradicted
    | Ambiguous
    | Unsupported
    | Unobserved

type CorrespondenceEntry =
    { ObligationId: SpecificationId
      Status: CorrespondenceStatus
      SourceBindings: string list
      TestBindings: string list
      EvidenceRefs: string list
      Explanation: string }

[<RequireQualifiedAccess>]
type CorrespondenceScope =
    | All
    | ImpactedBy of changedSubjectIds: string list

type CorrespondenceReport =
    { Schema: string
      AcceptedFingerprint: string
      ObservationFingerprint: string
      Scope: CorrespondenceScope
      Entries: CorrespondenceEntry list
      Diagnostics: SpecificationDiagnostic list }

module private CorrespondenceInternals =
    open WorkspaceInternals

    let kindValue =
        function
        | CorrespondenceObservationKind.GeneratedContract -> "generated-contract"
        | CorrespondenceObservationKind.SourceBinding -> "source-binding"
        | CorrespondenceObservationKind.Test -> "test"
        | CorrespondenceObservationKind.EvidenceReceipt -> "evidence-receipt"

    let parseKind =
        function
        | "generated-contract" -> CorrespondenceObservationKind.GeneratedContract
        | "source-binding" -> CorrespondenceObservationKind.SourceBinding
        | "test" -> CorrespondenceObservationKind.Test
        | "evidence-receipt" -> CorrespondenceObservationKind.EvidenceReceipt
        | value -> raise (JsonException($"Unknown observation kind '{value}'."))

    let statusValue =
        function
        | CorrespondenceStatus.Satisfied -> "satisfied"
        | CorrespondenceStatus.Missing -> "missing"
        | CorrespondenceStatus.Stale -> "stale"
        | CorrespondenceStatus.Contradicted -> "contradicted"
        | CorrespondenceStatus.Ambiguous -> "ambiguous"
        | CorrespondenceStatus.Unsupported -> "unsupported"
        | CorrespondenceStatus.Unobserved -> "unobserved"

    let stateValue =
        function
        | CorrespondenceObservationState.Observed -> "observed", None
        | CorrespondenceObservationState.Missing -> "missing", None
        | CorrespondenceObservationState.Contradicted reason -> "contradicted", Some reason
        | CorrespondenceObservationState.Ambiguous reason -> "ambiguous", Some reason
        | CorrespondenceObservationState.Unsupported reason -> "unsupported", Some reason

    let parseState value reason =
        match value, reason with
        | "observed", None -> CorrespondenceObservationState.Observed
        | "missing", None -> CorrespondenceObservationState.Missing
        | "contradicted", Some text -> CorrespondenceObservationState.Contradicted text
        | "ambiguous", Some text -> CorrespondenceObservationState.Ambiguous text
        | "unsupported", Some text -> CorrespondenceObservationState.Unsupported text
        | _ -> raise (JsonException("Observation state and reason are inconsistent."))

    let writeObservation (writer: Utf8JsonWriter) (item: CorrespondenceObservation) =
        let state, reason = stateValue item.State
        writer.WriteStartObject()
        writer.WriteString("obligationId", idText item.ObligationId)
        writer.WriteString("kind", kindValue item.Kind)
        writer.WriteString("acceptedFingerprint", item.AcceptedFingerprint)

        match item.SubjectFingerprint with
        | Some value -> writer.WriteString("subjectFingerprint", value)
        | None -> writer.WriteNull("subjectFingerprint")

        writer.WriteString("state", state)

        match reason with
        | Some value -> writer.WriteString("reason", value)
        | None -> writer.WriteNull("reason")

        item.SourceBindings |> List.sort |> writeStringArray writer "sourceBindings"
        item.TestBindings |> List.sort |> writeStringArray writer "testBindings"
        item.EvidenceRefs |> List.sort |> writeStringArray writer "evidenceRefs"
        writer.WriteString("explanation", item.Explanation)
        writer.WriteEndObject()

    let observationBytes (observations: CorrespondenceObservation list) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.workspace-correspondence-observations/v1")
        writer.WriteStartArray("observations")

        observations
        |> List.sortBy (fun item -> idText item.ObligationId, kindValue item.Kind)
        |> List.iter (writeObservation writer)

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        stream.ToArray()

    let distinctSorted (values: string list) = values |> List.distinct |> List.sort

    let stateReason =
        function
        | CorrespondenceObservationState.Contradicted reason
        | CorrespondenceObservationState.Ambiguous reason
        | CorrespondenceObservationState.Unsupported reason -> Some reason
        | _ -> None

[<RequireQualifiedAccess>]
module WorkspaceCorrespondence =
    open WorkspaceInternals
    open CorrespondenceInternals

    let private inputFindings
        (accepted: WorkspaceModel)
        (contract: QuintCompiledContractV2)
        (observations: CorrespondenceObservation list)
        (acceptedFingerprint: string)
        =
        let contractFindings =
            QuintContractV2.validate contract
            |> List.map (fun item -> diagnostic item.Code item.Path item.Message)

        let acceptedIds =
            accepted.Modules |> List.map (fun item -> idText item.Id) |> Set.ofList

        let catalogueIds = contract.Catalogue |> List.map _.Id |> Set.ofList
        let missingCatalogue = Set.difference acceptedIds catalogueIds

        [ yield! WorkspaceLifecycle.validate accepted
          yield! contractFindings
          for identifier in missingCatalogue |> Set.toList |> List.sort do
              diagnostic
                  "CORRESPONDENCE-CATALOGUE-INCOMPLETE"
                  "/contract/catalogue"
                  $"Accepted module '{identifier}' is absent from the compiled catalogue."
          for (obligation, kind), count in
              observations
              |> List.countBy (fun item -> idText item.ObligationId, kindValue item.Kind)
              |> List.filter (fun (_, count) -> count > 1) do
              diagnostic
                  "CORRESPONDENCE-OBSERVATION-DUPLICATE"
                  "/observations"
                  $"Observation '{obligation}/{kind}' is duplicated {count} times."
          for index, item in observations |> List.indexed do
              let path = $"/observations/{index}"

              if item.AcceptedFingerprint <> acceptedFingerprint then
                  diagnostic
                      "CORRESPONDENCE-FINGERPRINT-FORGED"
                      (path + "/acceptedFingerprint")
                      "Observation is not bound to the accepted workspace fingerprint."

              if not (Set.contains (idText item.ObligationId) acceptedIds) then
                  diagnostic
                      "CORRESPONDENCE-OBLIGATION-UNKNOWN"
                      (path + "/obligationId")
                      $"Unknown obligation '{idText item.ObligationId}'."

              match item.SubjectFingerprint with
              | Some value when not (isSha256 value) ->
                  diagnostic
                      "CORRESPONDENCE-SUBJECT-DIGEST"
                      (path + "/subjectFingerprint")
                      "Subject fingerprint must be lowercase SHA-256."
              | _ -> ()

              if String.IsNullOrWhiteSpace item.Explanation then
                  diagnostic "CORRESPONDENCE-EXPLANATION" (path + "/explanation") "Observation explanation is required."

              match stateReason item.State with
              | Some reason when String.IsNullOrWhiteSpace reason ->
                  diagnostic "CORRESPONDENCE-STATE-REASON" (path + "/reason") "Non-observed states require a reason."
              | _ -> ()

              for name, values in
                  [ "sourceBindings", item.SourceBindings
                    "testBindings", item.TestBindings
                    "evidenceRefs", item.EvidenceRefs ] do
                  if values |> List.exists String.IsNullOrWhiteSpace then
                      diagnostic "CORRESPONDENCE-BINDING-BLANK" (path + "/" + name) "Bindings cannot be blank."

                  if values.Length <> (values |> List.distinct).Length then
                      diagnostic "CORRESPONDENCE-BINDING-DUPLICATE" (path + "/" + name) "Bindings must be unique." ]
        |> sortDiagnostics

    let private impactedObligations
        (accepted: WorkspaceModel)
        (contract: QuintCompiledContractV2)
        (changed: string list)
        =
        let edges =
            contract.Relationships
            |> List.collect (fun item -> [ item.FromId, item.ToId; item.ToId, item.FromId ])
            |> List.groupBy fst
            |> List.map (fun (key, rows) -> key, (rows |> List.map snd |> Set.ofList))
            |> Map.ofList

        let seeds =
            Set.ofList changed
            |> Set.union (
                contract.Impacts
                |> List.choose (fun item ->
                    if List.contains item.SubjectId changed then
                        Some item.SubjectId
                    else
                        None)
                |> Set.ofList
            )

        let rec visit frontier visited =
            if Set.isEmpty frontier then
                visited
            else
                let node = Set.minElement frontier
                let rest = Set.remove node frontier

                if Set.contains node visited then
                    visit rest visited
                else
                    let neighbours = Map.tryFind node edges |> Option.defaultValue Set.empty
                    visit (Set.union rest neighbours) (Set.add node visited)

        let reachable = visit seeds Set.empty

        let direct =
            accepted.Modules
            |> List.filter (fun item -> Set.contains (idText item.Id) reachable)
            |> List.collect _.EvidenceObligationIds
            |> List.map idText
            |> Set.ofList

        Set.union reachable direct

    let private classify (expected: string) (observations: CorrespondenceObservation list) =
        let sources = observations |> List.collect _.SourceBindings |> distinctSorted
        let tests = observations |> List.collect _.TestBindings |> distinctSorted
        let evidence = observations |> List.collect _.EvidenceRefs |> distinctSorted
        let explanations = observations |> List.map _.Explanation |> distinctSorted
        let states = observations |> List.map _.State
        let kinds = observations |> List.map _.Kind |> Set.ofList

        let allKinds =
            set
                [ CorrespondenceObservationKind.GeneratedContract
                  CorrespondenceObservationKind.SourceBinding
                  CorrespondenceObservationKind.Test
                  CorrespondenceObservationKind.EvidenceReceipt ]

        let status, explanation =
            if List.isEmpty observations then
                CorrespondenceStatus.Unobserved, "No implementation observation was supplied."
            elif
                states
                |> List.exists (function
                    | CorrespondenceObservationState.Unsupported _ -> true
                    | _ -> false)
            then
                CorrespondenceStatus.Unsupported, String.concat "; " explanations
            elif
                states
                |> List.exists (function
                    | CorrespondenceObservationState.Ambiguous _ -> true
                    | _ -> false)
            then
                CorrespondenceStatus.Ambiguous, String.concat "; " explanations
            elif
                states
                |> List.exists (function
                    | CorrespondenceObservationState.Contradicted _ -> true
                    | _ -> false)
            then
                CorrespondenceStatus.Contradicted, String.concat "; " explanations
            elif
                observations
                |> List.exists (fun item -> item.SubjectFingerprint |> Option.exists ((<>) expected))
            then
                CorrespondenceStatus.Stale, "At least one observation targets an earlier subject fingerprint."
            elif
                states |> List.exists ((=) CorrespondenceObservationState.Missing)
                || not (Set.isSubset allKinds kinds)
                || List.isEmpty sources
                || List.isEmpty tests
                || List.isEmpty evidence
            then
                CorrespondenceStatus.Missing, "The observation set is incomplete for this obligation."
            else
                CorrespondenceStatus.Satisfied, String.concat "; " explanations

        { ObligationId =
            observations
            |> List.tryHead
            |> Option.map _.ObligationId
            |> Option.defaultWith (fun () -> failwith "obligation supplied separately")
          Status = status
          SourceBindings = sources
          TestBindings = tests
          EvidenceRefs = evidence
          Explanation = explanation }

    let evaluate
        (accepted: WorkspaceModel)
        (contract: QuintCompiledContractV2)
        (observations: CorrespondenceObservation list)
        (scope: CorrespondenceScope)
        =
        match WorkspaceLifecycle.fingerprint accepted with
        | Error findings -> Error findings
        | Ok acceptedFingerprint ->
            let findings = inputFindings accepted contract observations acceptedFingerprint

            if not (List.isEmpty findings) then
                Error findings
            else
                let obligationModules =
                    accepted.Modules
                    |> List.filter (fun item -> item.Kind = WorkspaceModuleKind.EvidenceRequirement)

                let selected =
                    match scope with
                    | CorrespondenceScope.All ->
                        obligationModules |> List.map (fun item -> idText item.Id) |> Set.ofList
                    | CorrespondenceScope.ImpactedBy changed -> impactedObligations accepted contract changed

                let entries =
                    obligationModules
                    |> List.filter (fun item -> Set.contains (idText item.Id) selected)
                    |> List.sortBy (fun item -> idText item.Id)
                    |> List.map (fun obligation ->
                        let matching =
                            observations |> List.filter (fun item -> item.ObligationId = obligation.Id)

                        if List.isEmpty matching then
                            { ObligationId = obligation.Id
                              Status = CorrespondenceStatus.Unobserved
                              SourceBindings = []
                              TestBindings = []
                              EvidenceRefs = []
                              Explanation = "No implementation observation was supplied." }
                        else
                            classify obligation.ContentSha256 matching)

                Ok
                    { Schema = "fsgg.workspace-correspondence-report/v1"
                      AcceptedFingerprint = acceptedFingerprint
                      ObservationFingerprint = observationBytes observations |> sha256
                      Scope = scope
                      Entries = entries
                      Diagnostics = [] }

    let deserializeObservations (text: string) =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement
            checkFields "" (set [ "schema"; "observations" ]) root

            if stringProperty "schema" root <> "fsgg.workspace-correspondence-observations/v1" then
                raise (JsonException("Unsupported correspondence observation schema."))

            let items = property "observations" root

            if items.ValueKind <> JsonValueKind.Array then
                raise (JsonException("Field 'observations' must be an array."))

            items.EnumerateArray()
            |> Seq.mapi (fun index item ->
                let path = $"/observations/{index}"

                checkFields
                    path
                    (set
                        [ "obligationId"
                          "kind"
                          "acceptedFingerprint"
                          "subjectFingerprint"
                          "state"
                          "reason"
                          "sourceBindings"
                          "testBindings"
                          "evidenceRefs"
                          "explanation" ])
                    item

                let optional name =
                    let value = property name item

                    if value.ValueKind = JsonValueKind.Null then
                        None
                    elif value.ValueKind = JsonValueKind.String then
                        value.GetString() |> Option.ofObj
                    else
                        raise (JsonException($"{path}/{name}: expected string or null."))

                { ObligationId = stringProperty "obligationId" item |> parseId (path + "/obligationId")
                  Kind = stringProperty "kind" item |> parseKind
                  AcceptedFingerprint = stringProperty "acceptedFingerprint" item
                  SubjectFingerprint = optional "subjectFingerprint"
                  State = parseState (stringProperty "state" item) (optional "reason")
                  SourceBindings = stringArray "sourceBindings" item
                  TestBindings = stringArray "testBindings" item
                  EvidenceRefs = stringArray "evidenceRefs" item
                  Explanation = stringProperty "explanation" item })
            |> Seq.toList
            |> Ok
        with
        | :? JsonException as ex -> jsonFailure "CORRESPONDENCE-CODEC-INVALID" ex
        | :? FormatException as ex -> jsonFailure "CORRESPONDENCE-CODEC-INVALID" ex

    let serializeReport (report: CorrespondenceReport) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", report.Schema)
        writer.WriteString("acceptedFingerprint", report.AcceptedFingerprint)
        writer.WriteString("observationFingerprint", report.ObservationFingerprint)
        writer.WriteStartObject("scope")

        match report.Scope with
        | CorrespondenceScope.All ->
            writer.WriteString("kind", "all")
            writeStringArray writer "changedSubjectIds" []
        | CorrespondenceScope.ImpactedBy changed ->
            writer.WriteString("kind", "impacted-by")

            changed
            |> List.distinct
            |> List.sort
            |> writeStringArray writer "changedSubjectIds"

        writer.WriteEndObject()
        writer.WriteStartArray("entries")

        for item in report.Entries |> List.sortBy (fun item -> idText item.ObligationId) do
            writer.WriteStartObject()
            writer.WriteString("obligationId", idText item.ObligationId)
            writer.WriteString("status", statusValue item.Status)
            item.SourceBindings |> writeStringArray writer "sourceBindings"
            item.TestBindings |> writeStringArray writer "testBindings"
            item.EvidenceRefs |> writeStringArray writer "evidenceRefs"
            writer.WriteString("explanation", item.Explanation)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")

        for item in report.Diagnostics do
            writer.WriteStartObject()
            writer.WriteString("code", item.Code)
            writer.WriteString("path", item.Path)
            writer.WriteString("message", item.Message)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let renderPlain (report: CorrespondenceReport) =
        [ yield $"correspondence {report.AcceptedFingerprint} observations {report.ObservationFingerprint}"
          for item in report.Entries do
              yield $"{idText item.ObligationId}: {statusValue item.Status} - {item.Explanation}" ]
        |> String.concat "\n"
        |> fun value -> value + "\n"

    let renderRich (report: CorrespondenceReport) =
        [ yield "# Workspace correspondence"
          yield ""
          yield $"Accepted fingerprint: `{report.AcceptedFingerprint}`"
          yield $"Observation fingerprint: `{report.ObservationFingerprint}`"
          yield ""
          for item in report.Entries do
              let sources = String.concat ", " item.SourceBindings
              let tests = String.concat ", " item.TestBindings
              let evidence = String.concat ", " item.EvidenceRefs
              yield $"## {idText item.ObligationId} — {statusValue item.Status}"
              yield ""
              yield item.Explanation
              yield $"Sources: {sources}"
              yield $"Tests: {tests}"
              yield $"Evidence: {evidence}"
              yield "" ]
        |> String.concat "\n"
        |> fun value -> value + "\n"

[<RequireQualifiedAccess>]
type LegacyLifecycle =
    | NoneLifecycle
    | Sdd
    | TypedSdd
    | SpecKit

[<RequireQualifiedAccess>]
type LegacyMigrationClassification =
    | Migrated
    | Ambiguous of reason: string
    | Unsupported of reason: string
    | Preserved
    | Removed

type LegacySourceInventory =
    { Path: string
      OriginalSha256: string
      Lifecycle: LegacyLifecycle
      Classification: LegacyMigrationClassification
      TargetPath: string option }

type WorkspaceMigrationPlan =
    { SchemaVersion: int
      TargetBackend: string
      Sources: LegacySourceInventory list
      RollbackManifestSha256: string
      Decisions: HumanAcceptance list
      ReadyToApply: bool }

[<RequireQualifiedAccess>]
module WorkspaceMigration =
    open WorkspaceInternals

    let plan targetBackend rollbackManifestSha256 sources decisions =
        let sortedSources = sources |> List.sortBy (fun item -> item.Path)

        let findings =
            [ yield! nonBlank "WORKSPACE-MIGRATION-BACKEND" "/targetBackend" "Target backend" targetBackend
              if targetBackend <> "quint-specification-v1" then
                  diagnostic
                      "WORKSPACE-MIGRATION-BACKEND"
                      "/targetBackend"
                      "The single lifecycle target is quint-specification-v1."
              if not (isSha256 rollbackManifestSha256) then
                  diagnostic
                      "WORKSPACE-MIGRATION-ROLLBACK"
                      "/rollbackManifestSha256"
                      "Rollback manifest must be bound by lowercase SHA-256."
              for duplicate in
                  sources
                  |> List.countBy (fun item -> item.Path)
                  |> List.filter (snd >> ((<) 1))
                  |> List.map fst do
                  diagnostic "WORKSPACE-MIGRATION-PATH-DUPLICATE" "/sources" $"Source path '{duplicate}' is duplicated."
              for index, source in sortedSources |> List.indexed do
                  let path = $"/sources/{index}"
                  yield! nonBlank "WORKSPACE-MIGRATION-PATH" $"{path}/path" "Source path" source.Path

                  if not (isSha256 source.OriginalSha256) then
                      diagnostic
                          "WORKSPACE-MIGRATION-DIGEST"
                          $"{path}/originalSha256"
                          "Original source must be bound by lowercase SHA-256."

                  match source.Classification, source.TargetPath with
                  | LegacyMigrationClassification.Migrated, None ->
                      diagnostic
                          "WORKSPACE-MIGRATION-TARGET"
                          $"{path}/targetPath"
                          "Migrated sources require a target path."
                  | LegacyMigrationClassification.Ambiguous reason, _ ->
                      diagnostic
                          "WORKSPACE-MIGRATION-DECISION"
                          $"{path}/classification"
                          $"Ambiguous source requires a human decision: {reason}"
                  | LegacyMigrationClassification.Unsupported reason, _ ->
                      diagnostic
                          "WORKSPACE-MIGRATION-UNSUPPORTED"
                          $"{path}/classification"
                          $"Unsupported source prevents migration: {reason}"
                  | LegacyMigrationClassification.Removed, Some _ ->
                      diagnostic
                          "WORKSPACE-MIGRATION-REMOVED-TARGET"
                          $"{path}/targetPath"
                          "Removed sources cannot have a target path."
                  | _ -> ()
              if List.isEmpty decisions then
                  diagnostic
                      "WORKSPACE-MIGRATION-DECISION"
                      "/decisions"
                      "Migration requires an explicit human decision receipt." ]
            |> sortDiagnostics

        { SchemaVersion = 1
          TargetBackend = targetBackend
          Sources = sortedSources
          RollbackManifestSha256 = rollbackManifestSha256
          Decisions = decisions
          ReadyToApply = List.isEmpty findings },
        findings
