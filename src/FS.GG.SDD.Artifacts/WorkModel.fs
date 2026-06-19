namespace FS.GG.SDD.Artifacts

open System
open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.LifecycleArtifacts
open FS.GG.SDD.Artifacts.SchemaVersion

module WorkModel =
    type ProjectSummary = { Id: string; DefaultWorkRoot: string }

    type SourceEntry =
        { Path: string
          Kind: string
          Owner: string
          SchemaVersion: int
          SourceDigest: SourceDigest }

    type WorkItemSummary =
        { Id: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    type RequirementEntry = { Id: string; Title: string; Text: string; Source: string }
    type DecisionEntry = { Id: string; Title: string; Decision: string; Source: string }

    type TaskEntry =
        { Id: string
          Title: string
          Status: string
          Owner: string
          Dependencies: string list
          Requirements: string list
          Decisions: string list
          RequiredSkills: string list
          RequiredEvidence: string list
          Source: string }

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
          Source: string }

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

    let taskStatusValue status =
        match status with
        | Pending -> "pending"
        | InProgress -> "in-progress"
        | Done -> "done"
        | Skipped _ -> "skipped"

    let evidenceKindValue kind =
        match kind with
        | Implementation -> "implementation"
        | Verification -> "verification"
        | Synthetic -> "synthetic"
        | Deferral -> "deferral"
        | Missing -> "missing"

    let sourceEntries (parsed: ParsedWorkItem) =
        parsed.Sources
        |> List.map (fun source ->
            { Path = source.Artifact.Path
              Kind = ArtifactRef.kindValue source.Artifact.Kind
              Owner = ArtifactRef.ownerValue source.Artifact.Owner
              SchemaVersion = 1
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
                let same = values |> List.filter (fun value -> String.Equals(idSelector value, id, StringComparison.OrdinalIgnoreCase))

                if same.Length > 1 then
                    Some(Diagnostics.duplicateIdentifier artifact id (same |> List.choose locationSelector))
                else
                    None)

    let unknown id artifact correction =
        Diagnostics.unknownReference artifact id correction

    let referenceDiagnostics (parsed: ParsedWorkItem) =
        let requirementIds = parsed.Requirements |> List.map (fun requirement -> requirement.Id.Value) |> Set.ofList
        let decisionIds = parsed.Decisions |> List.map (fun decision -> decision.Id.Value) |> Set.ofList
        let taskIds = parsed.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList
        let evidenceIds = parsed.Evidence |> List.map (fun evidence -> evidence.Id.Value) |> Set.ofList

        let taskDiagnostics =
            parsed.Tasks
            |> List.collect (fun task ->
                let artifact = task.Source

                [ task.Requirements
                  |> List.choose (fun id ->
                      if Set.contains id.Value requirementIds then None else Some(unknown id.Value artifact "Declare the requirement in spec.md or update the task reference."))
                  task.Decisions
                  |> List.choose (fun id ->
                      if Set.contains id.Value decisionIds then None else Some(unknown id.Value artifact "Declare the decision in plan or clarification artifacts, or update the task reference."))
                  task.Dependencies
                  |> List.choose (fun id ->
                      if Set.contains id.Value taskIds then None else Some(unknown id.Value artifact "Declare the dependency task or remove the dependency."))
                  task.RequiredEvidence
                  |> List.choose (fun id ->
                      if Set.contains id.Value evidenceIds then None else Some(unknown id.Value artifact "Declare the evidence id in evidence.yml or update requiredEvidence.")) ]
                |> List.concat)

        let evidenceDiagnostics =
            parsed.Evidence
            |> List.collect (fun evidence ->
                let artifact = evidence.Source

                [ evidence.TaskRefs
                  |> List.choose (fun id ->
                      if Set.contains id.Value taskIds then None else Some(unknown id.Value artifact "Declare the task in tasks.yml or update the evidence subject."))
                  evidence.RequirementRefs
                  |> List.choose (fun id ->
                      if Set.contains id.Value requirementIds then None else Some(unknown id.Value artifact "Declare the requirement in spec.md or update the evidence reference.")) ]
                |> List.concat)

        taskDiagnostics @ evidenceDiagnostics

    let cycleDiagnostics (parsed: ParsedWorkItem) =
        let taskArtifact =
            parsed.Tasks
            |> List.tryHead
            |> Option.map (fun task -> task.Source)

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

                if root.TryGetProperty("generatedViews", &generatedViews) && generatedViews.ValueKind = JsonValueKind.Array then
                    let stale =
                        generatedViews.EnumerateArray()
                        |> Seq.exists (fun view ->
                            let mutable generator = Unchecked.defaultof<JsonElement>
                            let mutable sources = Unchecked.defaultof<JsonElement>
                            let generatorStale =
                                if view.TryGetProperty("generator", &generator) then
                                    let mutable version = Unchecked.defaultof<JsonElement>
                                    generator.TryGetProperty("version", &version)
                                    && version.GetString() <> "0.1.0"
                                else
                                    false

                            let sourceStale =
                                if view.TryGetProperty("sources", &sources) && sources.ValueKind = JsonValueKind.Array then
                                    sources.EnumerateArray()
                                    |> Seq.exists (fun source ->
                                        let mutable path = Unchecked.defaultof<JsonElement>
                                        let mutable digest = Unchecked.defaultof<JsonElement>
                                        let mutable digestValue = Unchecked.defaultof<JsonElement>

                                        if source.TryGetProperty("path", &path)
                                           && source.TryGetProperty("digest", &digest)
                                           && digest.TryGetProperty("value", &digestValue) then
                                            match Map.tryFind (path.GetString()) sourceMap with
                                            | Some current -> current <> digestValue.GetString()
                                            | None -> true
                                        else
                                            false)
                                else
                                    false

                            generatorStale || sourceStale)

                    if stale then
                        let artifact =
                            match FS.GG.SDD.Artifacts.ArtifactRef.create snapshot.Path ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
                            | Ok value -> value
                            | Error message -> invalidArg (nameof snapshot.Path) message

                        Some(Diagnostics.staleGeneratedView artifact "Generated view metadata no longer matches current sources." "Regenerate the view from current source digests and generator version.")
                    else
                        None
                else
                    None
            with _ ->
                let artifact =
                    match FS.GG.SDD.Artifacts.ArtifactRef.create snapshot.Path ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
                    | Ok value -> value
                    | Error message -> invalidArg (nameof snapshot.Path) message

                Some(Diagnostics.staleGeneratedView artifact "Generated view JSON could not be parsed." "Regenerate the view with valid JSON."))

    let proseDiagnostics (parsed: ParsedWorkItem) =
        match parsed.Metadata.ProseStatus with
        | Some prose when not (String.Equals(prose, parsed.Metadata.Status, StringComparison.OrdinalIgnoreCase)) ->
            let artifact =
                match parsed.Sources |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Spec) with
                | Some source -> source.Artifact
                | None ->
                    match FS.GG.SDD.Artifacts.ArtifactRef.create $"work/{parsed.WorkId.Value}/spec.md" ArtifactKind.Spec ArtifactOwner.Sdd true with
                    | Ok value -> value
                    | Error message -> invalidArg "spec" message

            [ Diagnostics.proseStructuredMismatch artifact "Markdown prose status disagrees with structured work metadata." "Use structured metadata for executable decisions and update prose to match." ]
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
                        [ task.Id.Value ])
            | _ -> None)

    let validationDiagnostics (parsed: ParsedWorkItem) =
        let specArtifact =
            match parsed.Sources |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Spec) with
            | Some source -> source.Artifact
            | None ->
                match FS.GG.SDD.Artifacts.ArtifactRef.create $"work/{parsed.WorkId.Value}/spec.md" ArtifactKind.Spec ArtifactOwner.Sdd true with
                | Ok value -> value
                | Error message -> invalidArg "spec" message

        let taskArtifact =
            match parsed.Sources |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Tasks) with
            | Some source -> source.Artifact
            | None ->
                match FS.GG.SDD.Artifacts.ArtifactRef.create $"work/{parsed.WorkId.Value}/tasks.yml" ArtifactKind.Tasks ArtifactOwner.Sdd true with
                | Ok value -> value
                | Error message -> invalidArg "tasks" message

        let evidenceArtifact =
            match parsed.Sources |> List.tryFind (fun source -> source.Artifact.Kind = ArtifactKind.Evidence) with
            | Some source -> source.Artifact
            | None ->
                match FS.GG.SDD.Artifacts.ArtifactRef.create $"work/{parsed.WorkId.Value}/evidence.yml" ArtifactKind.Evidence ArtifactOwner.Sdd true with
                | Ok value -> value
                | Error message -> invalidArg "evidence" message

        [ duplicateDiagnostics specArtifact (fun (item: Requirement) -> item.Id.Value) (fun item -> item.SourceLocation) parsed.Requirements
          duplicateDiagnostics specArtifact (fun (item: Decision) -> item.Id.Value) (fun item -> item.SourceLocation) parsed.Decisions
          duplicateDiagnostics taskArtifact (fun (item: WorkTask) -> item.Id.Value) (fun item -> item.SourceLocation) parsed.Tasks
          duplicateDiagnostics evidenceArtifact (fun (item: EvidenceDeclaration) -> item.Id.Value) (fun item -> item.SourceLocation) parsed.Evidence
          referenceDiagnostics parsed
          cycleDiagnostics parsed
          proseDiagnostics parsed
          staleDiagnostics parsed
          missingEvidenceDiagnostics parsed ]
        |> List.concat

    let generatedViews (parsed: ParsedWorkItem) =
        let generator =
            SchemaVersion.createGeneratorVersion "FS.GG.SDD.Artifacts" "0.1.0"
            |> function
                | Ok value -> value
                | Error _ -> { Id = "FS.GG.SDD.Artifacts"; Version = "0.1.0" }

        GenerationManifest.createWorkModelManifest $"readiness/{parsed.WorkId.Value}/work-model.json" generator parsed.Sources None
        |> List.singleton

    let fromParsedWorkItem (parsed: ParsedWorkItem) =
        let diagnostics = parsed.Diagnostics @ validationDiagnostics parsed |> Diagnostics.sort

        { SchemaVersion = 1
          ModelVersion = "1.0.0"
          WorkId = parsed.WorkId.Value
          Project =
            { Id = parsed.Project |> Option.map (fun project -> project.ProjectId) |> Option.defaultValue "unknown"
              DefaultWorkRoot = parsed.Project |> Option.map (fun project -> project.DefaultWorkRoot) |> Option.defaultValue "work" }
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
                { Id = requirement.Id.Value
                  Title = requirement.Title
                  Text = requirement.Text
                  Source = requirement.Source.Path })
            |> List.sortBy (fun requirement -> requirement.Id)
          Decisions =
            parsed.Decisions
            |> List.map (fun decision ->
                { Id = decision.Id.Value
                  Title = decision.Title
                  Decision = decision.Decision
                  Source = decision.Source.Path })
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
                  RequiredSkills = task.RequiredSkills |> List.sort
                  RequiredEvidence = task.RequiredEvidence |> List.map (fun id -> id.Value) |> List.sort
                  Source = task.Source.Path })
            |> List.sortBy (fun task -> task.Id)
          Evidence =
            parsed.Evidence
            |> List.map (fun evidence ->
                { Id = evidence.Id.Value
                  Kind = evidenceKindValue evidence.Kind
                  SubjectType = evidence.Subject.SubjectType
                  SubjectId = evidence.Subject.Id
                  TaskRefs = evidence.TaskRefs |> List.map (fun id -> id.Value) |> List.distinct |> List.sort
                  RequirementRefs = evidence.RequirementRefs |> List.map (fun id -> id.Value) |> List.distinct |> List.sort
                  ArtifactRefs = evidence.ArtifactRefs |> List.map (fun artifact -> artifact.Path) |> List.sort
                  Result = evidence.Result
                  Synthetic = evidence.Synthetic
                  Source = evidence.Source.Path })
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

    let blockingDiagnostics model =
        model.Diagnostics |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticError)

    let governanceBoundaryEntries model = model.GovernanceBoundaries
