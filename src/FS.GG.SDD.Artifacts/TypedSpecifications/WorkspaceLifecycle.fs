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
               (character >= '0' && character <= '9')
               || (character >= 'a' && character <= 'f'))

    let nonBlank (code: string) (path: string) (label: string) (value: string) =
        if String.IsNullOrWhiteSpace value then [ diagnostic code path $"{label} is required." ] else []

    let kindValue kind =
        match kind with
        | WorkspaceModuleKind.ProductSpecification -> "product-specification"
        | WorkspaceModuleKind.Decision -> "decision"
        | WorkspaceModuleKind.WorkChange -> "work-change"
        | WorkspaceModuleKind.RepositoryProfile -> "repository-profile"
        | WorkspaceModuleKind.CiObligation -> "ci-obligation"
        | WorkspaceModuleKind.ExternalContract -> "external-contract"
        | WorkspaceModuleKind.EvidenceRequirement -> "evidence-requirement"

    let sortDiagnostics (findings: SpecificationDiagnostic list) =
        findings |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let moduleMap (model: WorkspaceModel) = model.Modules |> List.map (fun item -> idText item.Id, item) |> Map.ofList

    let touched change =
        match change with
        | WorkspaceChange.Upsert item -> Set.singleton(idText item.Id)
        | WorkspaceChange.Remove identifier -> Set.singleton(idText identifier)
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
            item.References |> List.map idText |> List.sort |> writeStringArray writer "references"
            item.Assumptions |> List.sort |> writeStringArray writer "assumptions"
            item.EvidenceObligationIds |> List.map idText |> List.sort |> writeStringArray writer "evidenceObligationIds"
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        stream.ToArray()

    let sha256 (bytes: byte array) =
        SHA256.HashData bytes |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

    let applyChanges (model: WorkspaceModel) (changes: WorkspaceChange list) =
        let folder modules change =
            match change with
            | WorkspaceChange.Upsert item -> modules |> Map.add (idText item.Id) item
            | WorkspaceChange.Remove identifier -> modules |> Map.remove (idText identifier)
            | WorkspaceChange.Rename(fromId, toId) ->
                match modules |> Map.tryFind (idText fromId) with
                | None -> modules
                | Some item -> modules |> Map.remove (idText fromId) |> Map.add (idText toId) { item with Id = toId }

        let modules = changes |> List.fold folder (moduleMap model)
        { model with Revision = model.Revision + 1L; Modules = modules |> Map.toList |> List.map snd }

[<RequireQualifiedAccess>]
module WorkspaceLifecycle =
    open WorkspaceInternals

    let validate model =
        let ids = model.Modules |> List.map (fun item -> idText item.Id)
        let idSet = ids |> Set.ofList

        let evidenceIds =
            model.Modules
            |> List.choose (fun item ->
                if item.Kind = WorkspaceModuleKind.EvidenceRequirement then Some(idText item.Id) else None)
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
                  diagnostic "WORKSPACE-CONTENT-DIGEST" $"{path}/contentSha256" "contentSha256 must be lowercase SHA-256."

              for reference in item.References |> List.map idText |> List.distinct |> List.sort do
                  if not (Set.contains reference idSet) then
                      diagnostic "WORKSPACE-REFERENCE-DANGLING" $"{path}/references" $"Reference '{reference}' does not resolve."

              for evidence in item.EvidenceObligationIds |> List.map idText |> List.distinct |> List.sort do
                  if not (Set.contains evidence evidenceIds) then
                      diagnostic "WORKSPACE-EVIDENCE-DANGLING" $"{path}/evidenceObligationIds" $"Evidence obligation '{evidence}' does not resolve to an evidence module."

              if item.References.Length <> (item.References |> List.distinct).Length then
                  diagnostic "WORKSPACE-REFERENCE-DUPLICATE" $"{path}/references" "References must be unique."

              if item.Assumptions |> List.exists String.IsNullOrWhiteSpace then
                  diagnostic "WORKSPACE-ASSUMPTION-BLANK" $"{path}/assumptions" "Assumptions cannot be blank."

              if item.Assumptions.Length <> (item.Assumptions |> List.distinct).Length then
                  diagnostic "WORKSPACE-ASSUMPTION-DUPLICATE" $"{path}/assumptions" "Assumptions must be unique."

              if item.EvidenceObligationIds.Length <> (item.EvidenceObligationIds |> List.distinct).Length then
                  diagnostic "WORKSPACE-EVIDENCE-DUPLICATE" $"{path}/evidenceObligationIds" "Evidence obligations must be unique." ]
        |> sortDiagnostics

    let canonicalBytes model =
        match validate model with
        | [] -> Ok(canonicalBytesUnchecked model)
        | findings -> Error findings

    let fingerprint model = canonicalBytes model |> Result.map sha256

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
                | None, Some item -> Some { Subject = item.Id; Summary = $"add {kindValue item.Kind}" }
                | Some item, None -> Some { Subject = item.Id; Summary = $"remove {kindValue item.Kind}" }
                | Some oldItem, Some newItem when oldItem.Assumptions <> newItem.Assumptions ->
                    Some { Subject = newItem.Id; Summary = "change assumptions" }
                | Some oldItem, Some newItem when oldItem <> newItem -> Some { Subject = newItem.Id; Summary = $"change {kindValue newItem.Kind}" }
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
              diagnostic "WORKSPACE-PROPOSAL-STALE" "/baseFingerprint" "Proposal does not target the current accepted fingerprint."
          | _ -> ()

          for duplicate in touchedSubjects |> List.countBy id |> List.filter (snd >> ((<) 1)) |> List.map fst do
              diagnostic "WORKSPACE-CHANGE-DUPLICATE" "/changes" $"Proposal edits identity '{duplicate}' more than once."

          for index, change in proposal.Changes |> List.indexed do
              match change with
              | WorkspaceChange.Remove identifier when not (Map.containsKey (idText identifier) acceptedModules) ->
                  diagnostic "WORKSPACE-CHANGE-MISSING" $"/changes/{index}" $"Removed identity '{idText identifier}' is absent from the accepted base."
              | WorkspaceChange.Rename(fromId, _) when not (Map.containsKey (idText fromId) acceptedModules) ->
                  diagnostic "WORKSPACE-CHANGE-MISSING" $"/changes/{index}" $"Renamed identity '{idText fromId}' is absent from the accepted base."
              | WorkspaceChange.Rename(_, toId) when Map.containsKey (idText toId) acceptedModules ->
                  diagnostic "WORKSPACE-RENAME-TARGET" $"/changes/{index}" $"Rename target '{idText toId}' already exists."
              | _ -> ()

          match proposal.EvidenceFingerprint, acceptedFingerprint with
          | Some observed, Ok expected when observed <> expected ->
              diagnostic "WORKSPACE-EVIDENCE-STALE" "/evidenceFingerprint" "Proposal evidence was collected against another accepted fingerprint."
          | Some observed, _ when not (isSha256 observed) ->
              diagnostic "WORKSPACE-EVIDENCE-DIGEST" "/evidenceFingerprint" "Evidence fingerprint must be lowercase SHA-256."
          | _ -> ()

          match proposal.Disposition with
          | ProposalDisposition.AcceptedOpaque opaque ->
              yield! nonBlank "WORKSPACE-OPAQUE-DEBT" "/disposition/debt" "Opaque debt" opaque.Debt
              if List.isEmpty opaque.AffectedSubjects then
                  diagnostic "WORKSPACE-OPAQUE-SUBJECT" "/disposition/affectedSubjects" "AcceptedOpaque requires affected subjects."
              yield! nonBlank "WORKSPACE-OPAQUE-REASON" "/disposition/reason" "Opaque reason" opaque.Reason
              yield! nonBlank "WORKSPACE-OPAQUE-HUMAN" "/disposition/responsibleHuman" "Responsible human" opaque.ResponsibleHuman
              if List.isEmpty opaque.EvidenceRefs || opaque.EvidenceRefs |> List.exists String.IsNullOrWhiteSpace then
                  diagnostic "WORKSPACE-OPAQUE-EVIDENCE" "/disposition/evidenceRefs" "AcceptedOpaque requires evidence references."
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
          yield! nonBlank "WORKSPACE-ACCEPTANCE-TIME" "/acceptance/acceptedAtUtc" "Acceptance time" acceptance.AcceptedAtUtc
          if List.isEmpty acceptance.EvidenceRefs || acceptance.EvidenceRefs |> List.exists String.IsNullOrWhiteSpace then
              diagnostic "WORKSPACE-ACCEPTANCE-EVIDENCE" "/acceptance/evidenceRefs" "Human acceptance requires evidence references." ]

    let reduce accepted proposal acceptance =
        let findings =
            [ yield! validateProposal accepted proposal
              yield! validateAcceptance acceptance
              if not (eligible proposal) then
                  diagnostic "WORKSPACE-PROPOSAL-NONREDUCIBLE" "/disposition" "Only CoherentDelta, NoSemanticChange, and AcceptedOpaque can reduce accepted authority." ]
            |> sortDiagnostics

        if not (List.isEmpty findings) then Error findings
        else
            let candidate = applyChanges accepted proposal.Changes
            match validate candidate with
            | [] -> Ok candidate
            | candidateFindings -> Error candidateFindings

    let reconcile accepted left right =
        let basicFindings = validateProposal accepted left @ validateProposal accepted right
        let leftTouched = left.Changes |> List.collect (touched >> Set.toList) |> Set.ofList
        let rightTouched = right.Changes |> List.collect (touched >> Set.toList) |> Set.ofList

        let overlapFindings =
            Set.intersect leftTouched rightTouched
            |> Set.toList
            |> List.sort
            |> List.map (fun subject -> diagnostic "WORKSPACE-MERGE-OVERLAP" "/changes" $"Both proposals edit '{subject}'.")

        let renamed proposal =
            proposal.Changes
            |> List.choose (function WorkspaceChange.Rename(fromId, _) -> Some(idText fromId) | _ -> None)
            |> Set.ofList

        let removed proposal =
            proposal.Changes
            |> List.choose (function WorkspaceChange.Remove identifier -> Some(idText identifier) | _ -> None)
            |> Set.ofList

        let renameDeleteFindings =
            Set.union (Set.intersect (renamed left) (removed right)) (Set.intersect (renamed right) (removed left))
            |> Set.toList
            |> List.map (fun subject -> diagnostic "WORKSPACE-MERGE-RENAME-DELETE" "/changes" $"Identity '{subject}' is renamed and removed across proposals.")

        let eligibilityFindings =
            [ if not (eligible left) || not (eligible right) then
                  diagnostic "WORKSPACE-MERGE-NONREDUCIBLE" "/disposition" "Both proposals must have an accepting disposition." ]

        match basicFindings @ overlapFindings @ renameDeleteFindings @ eligibilityFindings |> sortDiagnostics with
        | _ :: _ as findings -> Conflicted findings
        | [] ->
            let candidate = applyChanges accepted (left.Changes @ right.Changes)
            match validate candidate with
            | _ :: _ as findings -> Conflicted findings
            | [] ->
                let changes = (left.Changes @ right.Changes) |> List.map changeSummary |> List.map (fun (subject, summary) -> { Subject = subject; Summary = summary })
                Reconciled(candidate, changes)

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
                  diagnostic "WORKSPACE-MIGRATION-BACKEND" "/targetBackend" "The single lifecycle target is quint-specification-v1."
              if not (isSha256 rollbackManifestSha256) then
                  diagnostic "WORKSPACE-MIGRATION-ROLLBACK" "/rollbackManifestSha256" "Rollback manifest must be bound by lowercase SHA-256."
              for duplicate in sources |> List.countBy (fun item -> item.Path) |> List.filter (snd >> ((<) 1)) |> List.map fst do
                  diagnostic "WORKSPACE-MIGRATION-PATH-DUPLICATE" "/sources" $"Source path '{duplicate}' is duplicated."
              for index, source in sortedSources |> List.indexed do
                  let path = $"/sources/{index}"
                  yield! nonBlank "WORKSPACE-MIGRATION-PATH" $"{path}/path" "Source path" source.Path
                  if not (isSha256 source.OriginalSha256) then
                      diagnostic "WORKSPACE-MIGRATION-DIGEST" $"{path}/originalSha256" "Original source must be bound by lowercase SHA-256."
                  match source.Classification, source.TargetPath with
                  | LegacyMigrationClassification.Migrated, None ->
                      diagnostic "WORKSPACE-MIGRATION-TARGET" $"{path}/targetPath" "Migrated sources require a target path."
                  | LegacyMigrationClassification.Ambiguous reason, _ ->
                      diagnostic "WORKSPACE-MIGRATION-DECISION" $"{path}/classification" $"Ambiguous source requires a human decision: {reason}"
                  | LegacyMigrationClassification.Unsupported reason, _ ->
                      diagnostic "WORKSPACE-MIGRATION-UNSUPPORTED" $"{path}/classification" $"Unsupported source prevents migration: {reason}"
                  | LegacyMigrationClassification.Removed, Some _ ->
                      diagnostic "WORKSPACE-MIGRATION-REMOVED-TARGET" $"{path}/targetPath" "Removed sources cannot have a target path."
                  | _ -> ()
              if List.isEmpty decisions then
                  diagnostic "WORKSPACE-MIGRATION-DECISION" "/decisions" "Migration requires an explicit human decision receipt." ]
            |> sortDiagnostics

        { SchemaVersion = 1
          TargetBackend = targetBackend
          Sources = sortedSources
          RollbackManifestSha256 = rollbackManifestSha256
          Decisions = decisions
          ReadyToApply = List.isEmpty findings }, findings
