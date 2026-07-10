namespace FS.GG.SDD.Artifacts

open System
open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

module WorkModel =
    type ProjectSummary = { Id: string; DefaultWorkRoot: string }

    type SourceEntry =
        { Path: string
          Kind: string
          Owner: string
          SchemaVersion: int
          RawSchemaVersion: string option
          SchemaStatus: string
          SourceDigest: SourceDigest }

    type WorkItemSummary =
        { Id: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    type RequirementEntry =
        { Id: string
          Title: string
          Text: string
          AcceptanceCriteria: string list
          Priority: string option
          Source: string
          SourceLocation: SourceLocation option
          LinkedTaskIds: string list
          LinkedEvidenceIds: string list }

    type DecisionEntry =
        { Id: string
          Title: string
          Decision: string
          RequirementRefs: string list
          StoryRefs: string list
          AcceptanceRefs: string list
          Source: string
          SourceLocation: SourceLocation option
          LinkedTaskIds: string list }

    type TaskEntry =
        { Id: string
          Title: string
          Status: string
          Owner: string
          Dependencies: string list
          Requirements: string list
          Decisions: string list
          SourceIds: string list
          RequiredSkills: string list
          RequiredEvidence: string list
          Source: string
          SourceLocation: SourceLocation option }

    type EvidenceEntry =
        { Id: string
          Kind: string
          SubjectType: string
          SubjectId: string
          TaskRefs: string list
          RequirementRefs: string list
          ArtifactRefs: string list
          Result: string
          Synthetic: bool
          Rationale: string option
          Source: string
          SourceLocation: SourceLocation option }

    type GovernanceBoundaryEntry =
        { Path: string
          Owner: string
          RequiredBySdd: bool
          Relationship: string }

    type WorkModel =
        { SchemaVersion: int
          ModelVersion: string
          WorkId: string
          Project: ProjectSummary
          Sources: SourceEntry list
          WorkItem: WorkItemSummary
          Requirements: RequirementEntry list
          Decisions: DecisionEntry list
          Tasks: TaskEntry list
          Evidence: EvidenceEntry list
          GeneratedViews: GenerationManifest list
          Diagnostics: Diagnostic list
          GovernanceBoundaries: GovernanceBoundaryEntry list }

    type WorkModelGenerationRequest =
        { WorkId: string
          Snapshots: FileSnapshot list
          GeneratorVersion: GeneratorVersion
          ExpectedOutputPath: string option }

    type WorkModelGenerationResult =
        { WorkId: string
          OutputPath: string
          Model: WorkModel
          Json: string
          OutputDigest: OutputDigest
          Diagnostics: Diagnostic list }

    let taskStatusValue status =
        match status with
        | Pending -> "pending"
        | InProgress -> "in-progress"
        | Done -> "done"
        | Skipped _ -> "skipped"
        | TaskStatus.Stale -> "stale"

    let evidenceKindValue kind =
        match kind with
        | Implementation -> "implementation"
        | Verification -> "verification"
        | Review -> "review"
        | GeneratedViewEvidence -> "generated-view"
        | Synthetic -> "synthetic"
        | Deferral -> "deferral"
        | Note -> "note"
        | Missing -> "missing"

    let sourceEntries (parsed: ParsedWorkItem) =
        parsed.Sources
        |> List.map (fun source ->
            { Path = source.Artifact.Path
              Kind = ArtifactRef.kindValue source.Artifact.Kind
              Owner = ArtifactRef.ownerValue source.Artifact.Owner
              SchemaVersion =
                source.SchemaVersion
                |> Option.map (fun version -> version.Major)
                |> Option.defaultValue 0
              RawSchemaVersion = source.RawSchemaVersion
              SchemaStatus = SchemaVersion.statusValue source.SchemaStatus
              SourceDigest = source.Digest })
        |> List.sortBy (fun source -> source.Path)

    let duplicateDiagnostics artifact (idSelector: 'a -> string) locationSelector values =
        values
        |> List.groupBy (idSelector >> fun value -> value.ToUpperInvariant())
        |> List.choose (fun (_, group) ->
            match group with
            | [] -> None
            | first :: _ ->
                let id = idSelector first

                let same =
                    values
                    |> List.filter (fun value ->
                        String.Equals(idSelector value, id, StringComparison.OrdinalIgnoreCase))

                if same.Length > 1 then
                    Some(Diagnostics.duplicateIdentifier artifact id (same |> List.choose locationSelector))
                else
                    None)

    let unknown id artifact correction =
        [ Diagnostics.unknownReference artifact id correction
          Diagnostics.workModelInconsistent artifact $"Reference '{id}' does not resolve." correction [ id ] ]

    let referenceDiagnostics (parsed: ParsedWorkItem) =
        let requirementIds =
            parsed.Requirements
            |> List.map (fun requirement -> requirement.Id.Value)
            |> Set.ofList

        let decisionIds =
            parsed.Decisions |> List.map (fun decision -> decision.Id.Value) |> Set.ofList

        let taskIds = parsed.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList

        let evidenceIds =
            parsed.Evidence |> List.map (fun evidence -> evidence.Id.Value) |> Set.ofList

        let taskDiagnostics =
            parsed.Tasks
            |> List.collect (fun task ->
                let artifact = task.Source

                [ task.Requirements
                  |> List.collect (fun id ->
                      if Set.contains id.Value requirementIds then
                          []
                      else
                          unknown id.Value artifact "Declare the requirement in spec.md or update the task reference.")
                  task.Decisions
                  |> List.collect (fun id ->
                      if Set.contains id.Value decisionIds then
                          []
                      else
                          unknown
                              id.Value
                              artifact
                              "Declare the decision in plan or clarification artifacts, or update the task reference.")
                  task.Dependencies
                  |> List.collect (fun id ->
                      if Set.contains id.Value taskIds then
                          []
                      else
                          unknown id.Value artifact "Declare the dependency task or remove the dependency.")
                  task.RequiredEvidence
                  |> List.collect (fun id ->
                      if Set.contains id.Value evidenceIds then
                          []
                      else
                          unknown
                              id.Value
                              artifact
                              "Declare the evidence id in evidence.yml or update requiredEvidence.") ]
                |> List.concat)

        let evidenceDiagnostics =
            parsed.Evidence
            |> List.collect (fun evidence ->
                let artifact = evidence.Source

                [ evidence.TaskRefs
                  |> List.collect (fun id ->
                      if Set.contains id.Value taskIds then
                          []
                      else
                          unknown id.Value artifact "Declare the task in tasks.yml or update the evidence subject.")
                  evidence.RequirementRefs
                  |> List.collect (fun id ->
                      if Set.contains id.Value requirementIds then
                          []
                      else
                          unknown
                              id.Value
                              artifact
                              "Declare the requirement in spec.md or update the evidence reference.") ]
                |> List.concat)

        taskDiagnostics @ evidenceDiagnostics

    let cycleDiagnostics (parsed: ParsedWorkItem) =
        let taskArtifact =
            parsed.Tasks |> List.tryHead |> Option.map (fun task -> task.Source)

        let dependencyMap =
            parsed.Tasks
            |> List.map (fun task -> task.Id.Value, (task.Dependencies |> List.map (fun dep -> dep.Value)))
            |> Map.ofList

        let rec visit path id =
            if List.contains id path then
                Some(List.rev (id :: path))
            else
                dependencyMap
                |> Map.tryFind id
                |> Option.defaultValue []
                |> List.tryPick (visit (id :: path))

        dependencyMap
        |> Map.toList
        |> List.tryPick (fst >> visit [])
        |> Option.bind (fun cycle ->
            let cycleText = String.concat " -> " cycle

            taskArtifact
            |> Option.map (fun artifact ->
                Diagnostics.workModelInconsistent
                    artifact
                    $"Task dependency cycle detected: {cycleText}."
                    "Remove one dependency edge so the task graph is acyclic."
                    cycle))
        |> Option.toList

    let staleDiagnostics (parsed: ParsedWorkItem) =
        let sourceMap =
            parsed.Sources
            |> List.map (fun source -> source.Artifact.Path, source.Digest.Value)
            |> Map.ofList

        parsed.ExistingGeneratedViews
        |> List.choose (fun snapshot ->
            try
                use document = JsonDocument.Parse snapshot.Text
                let root = document.RootElement
                let mutable generatedViews = Unchecked.defaultof<JsonElement>

                if
                    root.TryGetProperty("generatedViews", &generatedViews)
                    && generatedViews.ValueKind = JsonValueKind.Array
                then
                    let stale =
                        generatedViews.EnumerateArray()
                        |> Seq.exists (fun view ->
                            let mutable generator = Unchecked.defaultof<JsonElement>
                            let mutable sources = Unchecked.defaultof<JsonElement>

                            let generatorStale =
                                if view.TryGetProperty("generator", &generator) then
                                    let mutable version = Unchecked.defaultof<JsonElement>

                                    generator.TryGetProperty("version", &version)
                                    && version.GetString() <> (SchemaVersion.currentGeneratorVersion ()).Version
                                else
                                    false

                            let sourceStale =
                                if
                                    view.TryGetProperty("sources", &sources)
                                    && sources.ValueKind = JsonValueKind.Array
                                then
                                    sources.EnumerateArray()
                                    |> Seq.exists (fun source ->
                                        let mutable path = Unchecked.defaultof<JsonElement>
                                        let mutable digest = Unchecked.defaultof<JsonElement>
                                        let mutable digestValue = Unchecked.defaultof<JsonElement>

                                        if
                                            source.TryGetProperty("path", &path)
                                            && source.TryGetProperty("digest", &digest)
                                            && digest.TryGetProperty("value", &digestValue)
                                        then
                                            match Map.tryFind (path.GetString()) sourceMap with
                                            | Some current ->
                                                current
                                                <> (Option.ofObj (digestValue.GetString()) |> Option.defaultValue "")
                                            | None -> true
                                        else
                                            false)
                                else
                                    false

                            generatorStale || sourceStale)

                    if stale then
                        let artifact =
                            match
                                FS.GG.SDD.Artifacts.ArtifactRef.create
                                    snapshot.Path
                                    ArtifactKind.GeneratedView
                                    ArtifactOwner.Sdd
                                    true
                            with
                            | Ok value -> value
                            | Error message -> invalidArg (nameof snapshot.Path) message

                        Some(
                            Diagnostics.staleGeneratedView
                                artifact
                                "Generated view metadata no longer matches current sources."
                                "Regenerate the view from current source digests and generator version."
                        )
                    else
                        None
                else
                    None
            with _ ->
                let artifact =
                    match
                        FS.GG.SDD.Artifacts.ArtifactRef.create
                            snapshot.Path
                            ArtifactKind.GeneratedView
                            ArtifactOwner.Sdd
                            true
                    with
                    | Ok value -> value
                    | Error message -> invalidArg (nameof snapshot.Path) message

                Some(
                    Diagnostics.staleGeneratedView
                        artifact
                        "Generated view JSON could not be parsed."
                        "Regenerate the view with valid JSON."
                ))

    let proseDiagnostics (parsed: ParsedWorkItem) =
        match parsed.Metadata.ProseStatus with
        | Some prose when not (String.Equals(prose, parsed.Metadata.Status, StringComparison.OrdinalIgnoreCase)) ->
            let artifact =
                match
                    parsed.Sources
                    |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Spec)
                with
                | Some source -> source.Artifact
                | None ->
                    match
                        FS.GG.SDD.Artifacts.ArtifactRef.create
                            $"work/{parsed.WorkId.Value}/spec.md"
                            ArtifactKind.Spec
                            ArtifactOwner.Sdd
                            true
                    with
                    | Ok value -> value
                    | Error message -> invalidArg "spec" message

            [ Diagnostics.proseStructuredMismatch
                  artifact
                  "Markdown prose status disagrees with structured work metadata."
                  "Use structured metadata for executable decisions and update prose to match." ]
        | _ -> []

    let missingEvidenceDiagnostics (parsed: ParsedWorkItem) =
        let evidenceTaskRefs =
            parsed.Evidence
            |> List.collect (fun evidence -> evidence.TaskRefs |> List.map (fun id -> id.Value))
            |> Set.ofList

        parsed.Tasks
        |> List.choose (fun task ->
            match task.Status with
            | Done when not (Set.contains task.Id.Value evidenceTaskRefs) ->
                Some(
                    Diagnostics.workModelInconsistent
                        task.Source
                        $"Task {task.Id.Value} is done but has no evidence declaration."
                        "Add evidence.yml verification for the done task or set the task status back to pending."
                        [ task.Id.Value ]
                )
            | _ -> None)

    let requirementTypingDiagnostics (parsed: ParsedWorkItem) =
        let typed =
            parsed.Requirements
            |> List.collect (fun requirement -> requirement.Id.Value :: requirement.AcceptanceCriteria)
            |> List.map (fun id -> id.ToUpperInvariant())
            |> Set.ofList

        parsed.MarkdownRequirementMentions
        |> List.filter (fun mention -> not (Set.contains mention.Id typed))
        |> List.distinctBy (fun mention -> mention.Id)
        |> List.map (fun mention ->
            Diagnostics.requirementNotTyped
                mention.Source
                mention.Id
                "Declare the id in the structured requirement set or remove the stale Markdown reference.")

    let schemaCompatibilityDiagnostics (parsed: ParsedWorkItem) =
        parsed.Sources
        |> List.choose (fun source ->
            match source.SchemaStatus, source.RawSchemaVersion with
            | SchemaCompatibilityStatus.Deprecated, Some raw ->
                Some(Diagnostics.deprecatedSchemaVersion source.Artifact raw)
            | _ -> None)

    let validationDiagnostics (parsed: ParsedWorkItem) =
        let specArtifact =
            match
                parsed.Sources
                |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Spec)
            with
            | Some source -> source.Artifact
            | None ->
                match
                    FS.GG.SDD.Artifacts.ArtifactRef.create
                        $"work/{parsed.WorkId.Value}/spec.md"
                        ArtifactKind.Spec
                        ArtifactOwner.Sdd
                        true
                with
                | Ok value -> value
                | Error message -> invalidArg "spec" message

        let taskArtifact =
            match
                parsed.Sources
                |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Tasks)
            with
            | Some source -> source.Artifact
            | None ->
                match
                    FS.GG.SDD.Artifacts.ArtifactRef.create
                        $"work/{parsed.WorkId.Value}/tasks.yml"
                        ArtifactKind.Tasks
                        ArtifactOwner.Sdd
                        true
                with
                | Ok value -> value
                | Error message -> invalidArg "tasks" message

        let evidenceArtifact =
            match
                parsed.Sources
                |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Evidence)
            with
            | Some source -> source.Artifact
            | None ->
                match
                    FS.GG.SDD.Artifacts.ArtifactRef.create
                        $"work/{parsed.WorkId.Value}/evidence.yml"
                        ArtifactKind.Evidence
                        ArtifactOwner.Sdd
                        true
                with
                | Ok value -> value
                | Error message -> invalidArg "evidence" message

        [ duplicateDiagnostics
              specArtifact
              (fun (item: Requirement) -> item.Id.Value)
              (fun item -> item.SourceLocation)
              parsed.Requirements
          duplicateDiagnostics
              specArtifact
              (fun (item: Decision) -> item.Id.Value)
              (fun item -> item.SourceLocation)
              parsed.Decisions
          duplicateDiagnostics
              taskArtifact
              (fun (item: WorkTask) -> item.Id.Value)
              (fun item -> item.SourceLocation)
              parsed.Tasks
          duplicateDiagnostics
              evidenceArtifact
              (fun (item: EvidenceDeclaration) -> item.Id.Value)
              (fun item -> item.SourceLocation)
              parsed.Evidence
          referenceDiagnostics parsed
          cycleDiagnostics parsed
          proseDiagnostics parsed
          staleDiagnostics parsed
          missingEvidenceDiagnostics parsed
          requirementTypingDiagnostics parsed
          schemaCompatibilityDiagnostics parsed ]
        |> List.concat

    let generatedViews (parsed: ParsedWorkItem) =
        let generator = SchemaVersion.currentGeneratorVersion ()

        GenerationManifest.createWorkModelManifest
            (GenerationManifest.expectedWorkModelOutputPath parsed.WorkId.Value)
            generator
            parsed.Sources
            None
        |> List.singleton

    let fromParsedWorkItem (parsed: ParsedWorkItem) =
        let diagnostics =
            parsed.Diagnostics @ validationDiagnostics parsed |> Diagnostics.sort

        { SchemaVersion = 1
          ModelVersion = "1.0.0"
          WorkId = parsed.WorkId.Value
          Project =
            { Id =
                parsed.Project
                |> Option.map (fun project -> project.ProjectId)
                |> Option.defaultValue "unknown"
              DefaultWorkRoot =
                parsed.Project
                |> Option.map (fun project -> project.DefaultWorkRoot)
                |> Option.defaultValue "work" }
          Sources = sourceEntries parsed
          WorkItem =
            { Id = parsed.WorkId.Value
              Title = parsed.Metadata.Title
              Stage = Identifiers.stageValue parsed.Metadata.Stage
              ChangeTier = parsed.Metadata.ChangeTier
              Status = parsed.Metadata.Status }
          Requirements =
            parsed.Requirements
            |> List.map (fun requirement ->
                let linkedTaskIds =
                    parsed.Tasks
                    |> List.filter (fun task ->
                        task.Requirements |> List.exists (fun id -> id.Value = requirement.Id.Value))
                    |> List.map (fun task -> task.Id.Value)
                    |> List.sort

                let linkedEvidenceIds =
                    parsed.Evidence
                    |> List.filter (fun evidence ->
                        evidence.RequirementRefs
                        |> List.exists (fun id -> id.Value = requirement.Id.Value))
                    |> List.map (fun evidence -> evidence.Id.Value)
                    |> List.sort

                { Id = requirement.Id.Value
                  Title = requirement.Title
                  Text = requirement.Text
                  AcceptanceCriteria = requirement.AcceptanceCriteria
                  Priority = requirement.Priority
                  Source = requirement.Source.Path
                  SourceLocation = requirement.SourceLocation
                  LinkedTaskIds = linkedTaskIds
                  LinkedEvidenceIds = linkedEvidenceIds })
            |> List.sortBy (fun requirement -> requirement.Id)
          Decisions =
            parsed.Decisions
            |> List.map (fun decision ->
                let linkedTaskIds =
                    parsed.Tasks
                    |> List.filter (fun task -> task.Decisions |> List.exists (fun id -> id.Value = decision.Id.Value))
                    |> List.map (fun task -> task.Id.Value)
                    |> List.sort

                { Id = decision.Id.Value
                  Title = decision.Title
                  Decision = decision.Decision
                  RequirementRefs = decision.RequirementRefs |> List.map _.Value
                  StoryRefs = decision.StoryRefs |> List.map _.Value
                  AcceptanceRefs = decision.AcceptanceRefs |> List.map _.Value
                  Source = decision.Source.Path
                  SourceLocation = decision.SourceLocation
                  LinkedTaskIds = linkedTaskIds })
            |> List.sortBy (fun decision -> decision.Id)
          Tasks =
            parsed.Tasks
            |> List.map (fun task ->
                { Id = task.Id.Value
                  Title = task.Title
                  Status = taskStatusValue task.Status
                  Owner = task.Owner
                  Dependencies = task.Dependencies |> List.map (fun id -> id.Value) |> List.sort
                  Requirements = task.Requirements |> List.map (fun id -> id.Value) |> List.sort
                  Decisions = task.Decisions |> List.map (fun id -> id.Value) |> List.sort
                  SourceIds = task.SourceIds |> List.sort
                  RequiredSkills = task.RequiredSkills |> List.sort
                  RequiredEvidence = task.RequiredEvidence |> List.map (fun id -> id.Value) |> List.sort
                  Source = task.Source.Path
                  SourceLocation = task.SourceLocation })
            |> List.sortBy (fun task -> task.Id)
          Evidence =
            parsed.Evidence
            |> List.map (fun evidence ->
                { Id = evidence.Id.Value
                  Kind = evidenceKindValue evidence.Kind
                  SubjectType = evidence.Subject.SubjectType
                  SubjectId = evidence.Subject.Id
                  TaskRefs = evidence.TaskRefs |> List.map (fun id -> id.Value) |> List.distinct |> List.sort
                  RequirementRefs =
                    evidence.RequirementRefs
                    |> List.map (fun id -> id.Value)
                    |> List.distinct
                    |> List.sort
                  ArtifactRefs = evidence.ArtifactRefs |> List.map (fun artifact -> artifact.Path) |> List.sort
                  Result = evidence.Result
                  Synthetic = evidence.Synthetic
                  Rationale = evidence.Rationale
                  Source = evidence.Source.Path
                  SourceLocation = evidence.SourceLocation })
            |> List.sortBy (fun evidence -> evidence.Id)
          GeneratedViews = generatedViews parsed
          Diagnostics = diagnostics
          GovernanceBoundaries =
            parsed.GovernanceBoundaries
            |> List.map (fun artifact ->
                { Path = artifact.Path
                  Owner = ArtifactRef.ownerValue artifact.Owner
                  RequiredBySdd = artifact.RequiredBySdd
                  Relationship = "optionalCompatibilityBoundary" })
            |> List.sortBy (fun boundary -> boundary.Path) }

    let blockingDiagnostics (model: WorkModel) =
        model.Diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticError)

    let governanceBoundaryEntries (model: WorkModel) = model.GovernanceBoundaries

    type NormalizedGuidanceModel =
        { WorkId: string
          Stage: string
          Commands: GuidanceCommandEntry list
          Skills: GuidanceSkillEntry list
          SourceIdentities: string list }

    // ---- work-model.json reader (the agent-guidance derivation source) ----

    let jmProp (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let jmString name element =
        jmProp name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Option.ofObj (value.GetString())
            else
                None)
        |> Option.defaultValue ""

    let jmInt name element =
        jmProp name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Number then
                match value.TryGetInt32() with
                | true, parsed -> Some parsed
                | _ -> None
            else
                None)

    // Reverse of `writeLocation`: a `null`/absent `sourceLocation` reads as `None`; an object reads
    // its `line`/`column` back as `int option`s (each independently `null`-tolerant, mirroring the
    // writer). Restoring this closes the last field-collapse hole in the work-model round-trip — the
    // sibling of the #241/#266 source/view/boundary round-trip guards — where `parseWorkModel`
    // hardcoded `SourceLocation = None`, so a serialize→parse→serialize cycle silently dropped every
    // populated location to `null` in the agents/refresh/ship generators that build through this
    // parser (FS.GG.SDD#342, item 2 of #338; root cause of #266/#242/#215-class field loss).
    let jmLocation name element : SourceLocation option =
        jmProp name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Object)
        |> Option.map (fun location ->
            ({ Line = jmInt "line" location
               Column = jmInt "column" location }
            : SourceLocation))

    let jmArray name element =
        jmProp name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Array)
        |> Option.map (fun value -> value.EnumerateArray() |> Seq.toList)
        |> Option.defaultValue []

    let jmStringList name element =
        jmArray name element
        |> List.choose (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Option.ofObj (value.GetString())
            else
                None)
        |> List.filter (String.IsNullOrWhiteSpace >> not)

    // Canonicalize a reference id list read verbatim from `work-model.json` to the same
    // upper-invariant form the in-memory path produces, so downstream unions
    // (`deriveGuidanceModel.relatedIds`) dedupe case-insensitively (#215). The two helpers mirror
    // the two in-memory shapes exactly, adding no new asymmetry: `sourceIds` is uppercased, deduped
    // and sorted (Task.fs `parseTaskFacts`), while the typed fields arrive uppercased through
    // `Identifiers.create*` and are sorted but not deduped.
    let upperSourceIds (ids: string list) =
        ids |> List.map (fun id -> id.ToUpperInvariant()) |> List.distinct |> List.sort

    let upperTypedIds (ids: string list) =
        ids |> List.map (fun id -> id.ToUpperInvariant()) |> List.sort

    let jmSeverity (value: string) =
        match value.Trim().ToLowerInvariant() with
        | "error" -> DiagnosticError
        | "warning" -> DiagnosticWarning
        | _ -> DiagnosticInfo

    let parseEmbeddedDiagnostic (element: JsonElement) : Diagnostic =
        { Id = jmString "id" element
          Severity = jmSeverity (jmString "severity" element)
          Artifact = None
          Location = None
          Message = jmString "message" element
          Correction = jmString "correction" element
          RelatedIds = jmStringList "relatedIds" element
          // Round-tripped diagnostics carry no defect bit — it is not serialized and the
          // exit-code decision never reads parsed diagnostics (see feature 062 research).
          IsToolDefect = false }

    // ---- FS-GG/FS.GG.SDD#266 (ADR-0002 Gap D, finding 2) round-trip helpers ----
    // `parseWorkModel` used to hardcode `Sources = []` and `GeneratedViews = []`, so a model rebuilt
    // through the parser (the `agents`/`refresh` generators, which build via `parseWorkModel`) lost
    // its source set and `deriveGuidanceModel.sourceIdentities` collapsed to a singleton. These parse
    // the persisted arrays back, mirroring the #242 fix for `governanceBoundaries`. `SourceDigest` and
    // `OutputDigest` share a shape, so the record reads are annotated to disambiguate them.

    let jmSourceDigest name element : SourceDigest =
        jmProp name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Object)
        |> Option.map (fun digest ->
            ({ Algorithm = jmString "algorithm" digest
               Value = jmString "value" digest }
            : SourceDigest))
        |> Option.defaultValue ({ Algorithm = "sha256"; Value = "" }: SourceDigest)

    let jmOutputDigest name element : OutputDigest option =
        jmProp name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Object)
        |> Option.map (fun digest ->
            ({ Algorithm = jmString "algorithm" digest
               Value = jmString "value" digest }
            : OutputDigest))

    // Reverse of `GenerationManifest.viewKindValue` / `currencyStatusValue`. Total: an unrecognized
    // kind round-trips through `Other`, and any currency string outside the four known values reads as
    // malformed (the serializer only ever writes the four).
    let jmViewKind (value: string) : GeneratedViewKind =
        match value with
        | "workModel" -> GenerationManifest.WorkModel
        | "analysis" -> GenerationManifest.Analysis
        | "verify" -> GenerationManifest.Verify
        | "ship" -> GenerationManifest.Ship
        | "shipVerdict" -> GenerationManifest.ShipVerdict
        | "summary" -> GenerationManifest.Summary
        | "agentCommands" -> GenerationManifest.AgentCommands
        | "governance-handoff" -> GenerationManifest.GovernanceHandoff
        | other -> GenerationManifest.Other other

    let jmCurrency (value: string) : GeneratedViewCurrencyStatus =
        match value with
        | "current" -> CurrencyCurrent
        | "missing" -> CurrencyMissing
        | "stale" -> CurrencyStale
        | _ -> CurrencyMalformed

    let parseSourceEntry (item: JsonElement) : SourceEntry =
        { Path = jmString "path" item
          Kind = jmString "kind" item
          Owner = jmString "owner" item
          SchemaVersion = jmInt "schemaVersion" item |> Option.defaultValue 0
          RawSchemaVersion =
            (match jmString "rawSchemaVersion" item with
             | "" -> None
             | value -> Some value)
          SchemaStatus = jmString "schemaStatus" item
          SourceDigest = jmSourceDigest "sourceDigest" item }

    // `writeManifestSource` persists only path/digest/schemaVersion; `SchemaStatus` and
    // `RawSchemaVersion` are not serialized, so they default here — re-serialization stays
    // byte-identical. Making those fields representable across the seam is the Gap A codec's job (#201).
    let parseManifestSource (item: JsonElement) : SourceIdentity option =
        match
            FS.GG.SDD.Artifacts.ArtifactRef.create
                (jmString "path" item)
                (ArtifactKind.Other "source")
                ArtifactOwner.Sdd
                true
        with
        | Error _ -> None
        | Ok artifact ->
            Some
                { Artifact = artifact
                  Digest = jmSourceDigest "digest" item
                  SchemaVersion = jmInt "schemaVersion" item |> Option.map SchemaVersion.create
                  SchemaStatus = Current
                  RawSchemaVersion = None }

    let parseGeneratedView (item: JsonElement) : GenerationManifest option =
        match
            FS.GG.SDD.Artifacts.ArtifactRef.create
                (jmString "path" item)
                ArtifactKind.GeneratedView
                ArtifactOwner.Sdd
                true
        with
        | Error _ -> None
        | Ok view ->
            Some
                { View = view
                  Kind = jmViewKind (jmString "kind" item)
                  SchemaVersion = SchemaVersion.create (jmInt "schemaVersion" item |> Option.defaultValue 1)
                  Generator =
                    jmProp "generator" item
                    |> Option.map (fun generator ->
                        ({ Id = jmString "id" generator
                           Version = jmString "version" generator }
                        : GeneratorVersion))
                    |> Option.defaultValue (SchemaVersion.currentGeneratorVersion ())
                  Sources =
                    jmArray "sources" item
                    |> List.choose parseManifestSource
                    |> List.sortBy (fun source -> source.Artifact.Path)
                  OutputDigest = jmOutputDigest "outputDigest" item
                  Currency = jmCurrency (jmString "currency" item)
                  Diagnostics = [] }

    let parseWorkModel (snapshot: FileSnapshot) : Result<WorkModel, Diagnostic list> =
        let artifact =
            match
                FS.GG.SDD.Artifacts.ArtifactRef.create snapshot.Path ArtifactKind.GeneratedView ArtifactOwner.Sdd true
            with
            | Ok value -> value
            | Error message -> invalidArg (nameof snapshot.Path) message

        try
            use document = JsonDocument.Parse snapshot.Text
            let root = document.RootElement
            let schemaVersion = jmInt "schemaVersion" root

            // One schema-version policy for every artifact (#70/§2.5): route the generated
            // work model's schemaVersion through the canonical classifier instead of a local
            // `version >= 1` check that would accept a schemaVersion-2/3 model here while it
            // blocks everywhere else. Accepts current/deprecated, rejects unsupported/future.
            match schemaVersion with
            | Some version when not (SchemaVersion.isBlocking (SchemaVersion.classifyRaw (Some(string version)))) ->
                let workItem = jmProp "workItem" root

                let stage = workItem |> Option.map (jmString "stage") |> Option.defaultValue ""

                let workId =
                    match jmString "workId" root with
                    | "" -> workItem |> Option.map (jmString "id") |> Option.defaultValue ""
                    | value -> value

                if String.IsNullOrWhiteSpace workId then
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Work model is missing a workId."
                              "Regenerate readiness/<id>/work-model.json from current lifecycle sources."
                              [ snapshot.Path ] ]
                else
                    Ok
                        { SchemaVersion = version
                          ModelVersion = jmString "modelVersion" root
                          WorkId = workId
                          Project =
                            jmProp "project" root
                            |> Option.map (fun project ->
                                { Id = jmString "id" project
                                  DefaultWorkRoot = jmString "defaultWorkRoot" project })
                            |> Option.defaultValue
                                { Id = "unknown"
                                  DefaultWorkRoot = "work" }
                          Sources =
                            jmArray "sources" root
                            |> List.map parseSourceEntry
                            |> List.sortBy (fun source -> source.Path)
                          WorkItem =
                            workItem
                            |> Option.map (fun item ->
                                { Id = jmString "id" item
                                  Title = jmString "title" item
                                  Stage = jmString "stage" item
                                  ChangeTier = jmString "changeTier" item
                                  Status = jmString "status" item })
                            |> Option.defaultValue
                                { Id = workId
                                  Title = workId
                                  Stage = stage
                                  ChangeTier = "tier1"
                                  Status = "draft" }
                          Requirements =
                            jmArray "requirements" root
                            |> List.map (fun item ->
                                { Id = jmString "id" item
                                  Title = jmString "title" item
                                  Text = jmString "text" item
                                  AcceptanceCriteria = jmStringList "acceptanceCriteria" item
                                  Priority =
                                    (match jmString "priority" item with
                                     | "" -> None
                                     | value -> Some value)
                                  Source = jmString "source" item
                                  SourceLocation = jmLocation "sourceLocation" item
                                  LinkedTaskIds = jmStringList "linkedTaskIds" item |> List.sort
                                  LinkedEvidenceIds = jmStringList "linkedEvidenceIds" item |> List.sort })
                            |> List.sortBy (fun requirement -> requirement.Id)
                          Decisions =
                            jmArray "decisions" root
                            |> List.map (fun item ->
                                { Id = jmString "id" item
                                  Title = jmString "title" item
                                  Decision = jmString "decision" item
                                  RequirementRefs = jmStringList "requirementRefs" item |> List.sort
                                  StoryRefs = jmStringList "storyRefs" item |> List.sort
                                  AcceptanceRefs = jmStringList "acceptanceRefs" item |> List.sort
                                  Source = jmString "source" item
                                  SourceLocation = jmLocation "sourceLocation" item
                                  LinkedTaskIds = jmStringList "linkedTaskIds" item |> List.sort })
                            |> List.sortBy (fun decision -> decision.Id)
                          Tasks =
                            jmArray "tasks" root
                            |> List.map (fun item ->
                                { Id = jmString "id" item
                                  Title = jmString "title" item
                                  Status = jmString "status" item
                                  Owner = jmString "owner" item
                                  Dependencies = jmStringList "dependencies" item |> List.sort
                                  // Upper-normalize the three reference fields to mirror the in-memory
                                  // path. `deriveGuidanceModel` unions all three and dedupes with a
                                  // case-sensitive `List.distinct`, so a hand-edited `work-model.json`
                                  // mixing `requirements: ["FR-001"]` with `sourceIds: ["fr-001"]` would
                                  // otherwise yield a duplicated `relatedIds` coverage clause and a
                                  // `behaviorModelDigest` no normalized re-run reproduces (#215).
                                  Requirements = jmStringList "requirements" item |> upperTypedIds
                                  Decisions = jmStringList "decisions" item |> upperTypedIds
                                  SourceIds = jmStringList "sourceIds" item |> upperSourceIds
                                  RequiredSkills = jmStringList "requiredSkills" item |> List.sort
                                  RequiredEvidence = jmStringList "requiredEvidence" item |> List.sort
                                  Source = jmString "source" item
                                  SourceLocation = jmLocation "sourceLocation" item })
                            |> List.sortBy (fun task -> task.Id)
                          Evidence =
                            jmArray "evidence" root
                            |> List.map (fun item ->
                                { Id = jmString "id" item
                                  Kind = jmString "kind" item
                                  SubjectType = jmString "subjectType" item
                                  SubjectId = jmString "subjectId" item
                                  TaskRefs = jmStringList "taskRefs" item |> List.sort
                                  RequirementRefs = jmStringList "requirementRefs" item |> List.sort
                                  ArtifactRefs = jmStringList "artifactRefs" item |> List.sort
                                  Result = jmString "result" item
                                  Synthetic =
                                    (jmProp "synthetic" item
                                     |> Option.exists (fun value -> value.ValueKind = JsonValueKind.True))
                                  Rationale =
                                    (match jmString "rationale" item with
                                     | "" -> None
                                     | value -> Some value)
                                  Source = jmString "source" item
                                  SourceLocation = jmLocation "sourceLocation" item })
                            |> List.sortBy (fun evidence -> evidence.Id)
                          GeneratedViews = jmArray "generatedViews" root |> List.choose parseGeneratedView
                          Diagnostics =
                            jmArray "diagnostics" root
                            |> List.map parseEmbeddedDiagnostic
                            |> Diagnostics.sort
                          GovernanceBoundaries =
                            jmArray "governanceBoundaries" root
                            |> List.map (fun item ->
                                { Path = jmString "path" item
                                  Owner = jmString "owner" item
                                  RequiredBySdd =
                                    (jmProp "requiredBySdd" item
                                     |> Option.exists (fun value -> value.ValueKind = JsonValueKind.True))
                                  Relationship = jmString "relationship" item })
                            |> List.sortBy (fun boundary -> boundary.Path) }
            | _ ->
                Error
                    [ Diagnostics.malformedSchemaVersion
                          artifact
                          "Work model is missing or has malformed schemaVersion." ]
        with ex ->
            Error
                [ Diagnostics.workModelInconsistent
                      artifact
                      $"Work model JSON is malformed: {ex.Message}"
                      "Regenerate readiness/<id>/work-model.json with valid JSON."
                      [ snapshot.Path ] ]

    // ---- normalized guidance model derivation (pure over the work model) ----

    let deriveGuidanceModel (model: WorkModel) : NormalizedGuidanceModel =
        let commands =
            model.Tasks
            |> List.map (fun task ->
                // Feature 096 (issue #189): union all three reference fields. `sourceIds` is the only
                // way to express an id with no typed field (SB-/PD-/VO-/AC-/CR-/GV-/PC-/PM-), so
                // omitting it silently dropped an author's scope boundary before the agent ever saw
                // it. The union belongs here at the consumer, NOT in Task.fs's parser: `SourceIds` is
                // what `taskValidationDiagnostics.unknownSources` gates on, so unioning at parse would
                // retroactively validate `requirements:`/`decisions:` and turn an untouched, green
                // `tasks.yml` red with no schemaVersion signal. `HandlersVerify` unions the same three
                // fields for the same reason. Distinct+sort keeps the derivation deterministic and
                // collapses the DEC-### that `clarificationDecisionTasks` writes into two fields.
                let relatedIds =
                    (task.Requirements @ task.Decisions @ task.SourceIds)
                    |> List.distinct
                    |> List.sort

                let purpose =
                    match relatedIds with
                    | [] -> $"Carry out lifecycle task {task.Id} ({task.Status})."
                    | ids ->
                        let coverage = String.concat ", " ids
                        $"Carry out lifecycle task {task.Id} ({task.Status}) covering {coverage}."

                { Id = task.Id
                  Title = task.Title
                  Stage = model.WorkItem.Stage
                  Purpose = purpose
                  RelatedIds = relatedIds })
            |> List.sortBy (fun command -> command.Id)

        let skills =
            model.Tasks
            |> List.collect (fun task -> task.RequiredSkills |> List.map (fun skill -> skill, task.Id))
            |> List.groupBy fst
            |> List.map (fun (skill, pairs) ->
                let taskIds = pairs |> List.map snd |> List.distinct |> List.sort
                let taskList = String.concat ", " taskIds

                { Id = skill
                  Title = skill
                  Capability = $"Required by tasks: {taskList}."
                  RelatedIds = taskIds })
            |> List.sortBy (fun skill -> skill.Id)

        let sourceIdentities =
            model.Sources
            |> List.map (fun source -> source.Path)
            |> List.append [ GenerationManifest.expectedWorkModelOutputPath model.WorkId ]
            |> List.distinct
            |> List.sort

        { WorkId = model.WorkId
          Stage = model.WorkItem.Stage
          Commands = commands
          Skills = skills
          SourceIdentities = sourceIdentities }

    let behaviorModelDigest (model: NormalizedGuidanceModel) : SourceDigest =
        let commandText =
            model.Commands
            |> List.map (fun command ->
                String.concat
                    "|"
                    [ command.Id
                      command.Title
                      command.Stage
                      command.Purpose
                      String.concat "," command.RelatedIds ])
            |> String.concat "\n"

        let skillText =
            model.Skills
            |> List.map (fun skill ->
                String.concat "|" [ skill.Id; skill.Title; skill.Capability; String.concat "," skill.RelatedIds ])
            |> String.concat "\n"

        let canonical =
            String.concat "\n" [ model.WorkId; model.Stage; "commands"; commandText; "skills"; skillText ]

        SchemaVersion.sha256Text canonical
