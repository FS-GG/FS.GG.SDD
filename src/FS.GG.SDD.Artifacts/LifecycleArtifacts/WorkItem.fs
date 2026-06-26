namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module WorkItem =
    type ParsedWorkItem =
        { WorkId: WorkId
          Project: ProjectLifecycleConfig option
          SddPolicy: SddLifecyclePolicy option
          Agents: AgentGuidanceConfig option
          Metadata: WorkItemMetadata
          Requirements: Requirement list
          Decisions: Decision list
          Tasks: WorkTask list
          Evidence: EvidenceDeclaration list
          MarkdownRequirementMentions: MarkdownRequirementMention list
          Sources: SourceIdentity list
          ExistingGeneratedViews: FileSnapshot list
          GovernanceBoundaries: ArtifactRef list
          Diagnostics: Diagnostic list }

    let requiredFiles workId =
        [ ".fsgg/project.yml", ArtifactKind.ProjectConfig
          ".fsgg/sdd.yml", ArtifactKind.SddConfig
          ".fsgg/agents.yml", ArtifactKind.AgentsConfig
          $"work/{workId}/spec.md", ArtifactKind.Spec
          $"work/{workId}/tasks.yml", ArtifactKind.Tasks
          $"work/{workId}/evidence.yml", ArtifactKind.Evidence ]

    let rawSchemaVersion (snapshot: FileSnapshot) kind =
        match kind with
        | ArtifactKind.Spec ->
            snapshot
            |> frontMatter
            |> Option.bind (fun (yaml, _) -> parseYaml yaml)
            |> Option.bind (tryScalarAt [ "schemaVersion" ])
        | _ ->
            if snapshot.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) then
                snapshot
                |> frontMatter
                |> Option.bind (fun (yaml, _) -> parseYaml yaml)
                |> Option.bind (tryScalarAt [ "schemaVersion" ])
            else
                try
                    snapshot.Text
                    |> parseYaml
                    |> Option.bind (tryScalarAt [ "schemaVersion" ])
                with _ ->
                    None

    let sourceIdentity (snapshot: FileSnapshot) kind =
        let source = sourceArtifact snapshot.Path kind
        let compatibility = rawSchemaVersion snapshot kind |> SchemaVersion.classifyRaw

        { Artifact = source
          Digest = SchemaVersion.sha256Text snapshot.Text
          SchemaVersion = compatibility.Version
          SchemaStatus = compatibility.Status
          RawSchemaVersion =
            if String.IsNullOrWhiteSpace compatibility.RawValue then
                None
            else
                Some compatibility.RawValue }

    let defaultMetadata workId =
        let parsed =
            match Identifiers.createWorkId workId with
            | Ok value -> value
            | Error _ -> { Value = workId }

        { SchemaVersion = SchemaVersion.create 1
          WorkId = parsed
          Title = workId
          Stage = LifecycleStage.Plan
          ChangeTier = "tier1"
          Status = "draft"
          ProseStatus = None }

    let loadWorkItemFromSnapshots (snapshots: FileSnapshot list) workId =
        let normalized =
            snapshots
            |> List.map (fun snapshot -> { snapshot with Path = normalizePath snapshot.Path })

        let byPath = normalized |> List.map (fun snapshot -> snapshot.Path, snapshot) |> Map.ofList

        let missingDiagnostics =
            requiredFiles workId
            |> List.choose (fun (path, kind) ->
                if Map.containsKey path byPath then
                    None
                else
                    Some(Diagnostics.missingArtifact (sourceArtifact path kind) $"Create '{path}' for work item '{workId}'."))

        let parse path parser =
            Map.tryFind path byPath
            |> Option.map parser

        let collect result =
            match result with
            | Some(Ok value) -> Some value, []
            | Some(Error diagnostics) -> None, diagnostics
            | None -> None, []

        let project, projectDiagnostics = parse ".fsgg/project.yml" parseProjectConfig |> collect
        let sdd, sddDiagnostics = parse ".fsgg/sdd.yml" parseSddLifecyclePolicy |> collect
        let agents, agentDiagnostics = parse ".fsgg/agents.yml" parseAgentGuidanceConfig |> collect
        let metadata, metadataDiagnostics =
            match parse $"work/{workId}/spec.md" parseWorkItemMetadata with
            | Some(Ok value) -> value, []
            | Some(Error diagnostics) -> defaultMetadata workId, diagnostics
            | None -> defaultMetadata workId, []

        let specSnapshot = Map.tryFind $"work/{workId}/spec.md" byPath
        let clarificationSnapshot = Map.tryFind $"work/{workId}/clarifications.md" byPath
        let requirements = specSnapshot |> Option.map parseRequirements |> Option.defaultValue []
        let requirementMentions = specSnapshot |> Option.map parseMarkdownRequirementMentions |> Option.defaultValue []
        let decisions =
            [ specSnapshot
              clarificationSnapshot ]
            |> List.choose id
            |> List.collect parseDecisions
            |> List.distinctBy (fun decision -> decision.Id.Value)
            |> List.sortBy (fun decision -> decision.Id.Value)

        let tasks, taskDiagnostics =
            match parse $"work/{workId}/tasks.yml" parseTasks with
            | Some(Ok value) -> value, []
            | Some(Error diagnostics) -> [], diagnostics
            | None -> [], []

        let evidence, evidenceDiagnostics =
            match parse $"work/{workId}/evidence.yml" parseEvidence with
            | Some(Ok value) -> value, []
            | Some(Error diagnostics) -> [], diagnostics
            | None -> [], []

        let kindFor path =
            requiredFiles workId
            |> List.tryFind (fun (candidate, _) -> candidate = path)
            |> Option.map snd
            |> Option.defaultValue
                (if path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then ArtifactKind.Clarifications
                 elif path.EndsWith("/checklist.md", StringComparison.OrdinalIgnoreCase) then ArtifactKind.Checklist
                 elif path.EndsWith("/plan.md", StringComparison.OrdinalIgnoreCase) then ArtifactKind.Plan
                 elif path.Contains("/readiness/") || path.StartsWith("readiness/") then ArtifactKind.GeneratedView
                 else ArtifactKind.Other "source")

        let sources =
            normalized
            |> List.filter (fun snapshot ->
                not (snapshot.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                && not (snapshot.Path.EndsWith("manifest.yml", StringComparison.OrdinalIgnoreCase)))
            |> List.map (fun snapshot -> sourceIdentity snapshot (kindFor snapshot.Path))
            |> List.sortBy (fun source -> source.Artifact.Path)

        let generatedViews =
            normalized
            |> List.filter (fun snapshot -> snapshot.Path.StartsWith($"readiness/{workId}/", StringComparison.OrdinalIgnoreCase))

        let governanceBoundaries =
            [ project |> Option.bind (fun project -> project.GovernancePolicyPath)
              project |> Option.bind (fun project -> project.GovernanceCapabilitiesPath)
              project |> Option.bind (fun project -> project.GovernanceToolingPath) ]
            |> List.choose id
            |> List.map FS.GG.SDD.Artifacts.ArtifactRef.optionalGovernanceBoundary
            |> List.sortBy (fun artifact -> artifact.Path)

        let parsedWorkId =
            match Identifiers.createWorkId workId with
            | Ok value -> value
            | Error _ -> metadata.WorkId

        let selectedWorkItemDiagnostics =
            if metadata.WorkId.Value = parsedWorkId.Value then
                []
            else
                let specArtifact = sourceArtifact $"work/{workId}/spec.md" ArtifactKind.Spec

                [ Diagnostics.workModelInconsistent
                      specArtifact
                      $"Selected work id '{parsedWorkId.Value}' does not match spec front matter workId '{metadata.WorkId.Value}'."
                      "Move the source under the matching work id or update spec front matter to the selected work id."
                      [ parsedWorkId.Value; metadata.WorkId.Value ] ]

        { WorkId = parsedWorkId
          Project = project
          SddPolicy = sdd
          Agents = agents
          Metadata = metadata
          Requirements = requirements
          Decisions = decisions
          Tasks = tasks
          Evidence = evidence
          MarkdownRequirementMentions = requirementMentions
          Sources = sources
          ExistingGeneratedViews = generatedViews
          GovernanceBoundaries = governanceBoundaries
          Diagnostics =
            missingDiagnostics
            @ projectDiagnostics
            @ sddDiagnostics
            @ agentDiagnostics
            @ metadataDiagnostics
            @ selectedWorkItemDiagnostics
            @ taskDiagnostics
            @ evidenceDiagnostics }
