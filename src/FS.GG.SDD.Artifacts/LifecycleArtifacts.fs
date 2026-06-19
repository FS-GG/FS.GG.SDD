namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

module LifecycleArtifacts =
    type FileSnapshot = { Path: string; Text: string }

    type ProjectLifecycleConfig =
        { SchemaVersion: SchemaVersion
          ProjectId: string
          DefaultWorkRoot: string
          SddConfigPath: string
          AgentsConfigPath: string
          GovernancePolicyPath: string option
          GovernanceCapabilitiesPath: string option
          GovernanceToolingPath: string option }

    type SddLifecyclePolicy =
        { SchemaVersion: SchemaVersion
          Stages: LifecycleStage list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    type AgentGuidanceTarget = { Id: string; GuidancePath: string; GeneratedRoot: string }

    type AgentGuidanceConfig =
        { SchemaVersion: SchemaVersion
          Targets: AgentGuidanceTarget list
          WorkModelPath: string
          GeneratedGuidanceIsAuthority: bool
          RequireEquivalentClaudeAndCodexBehavior: bool }

    type WorkItemMetadata =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          ProseStatus: string option }

    type Requirement =
        { Id: RequirementId
          Title: string
          Text: string
          AcceptanceCriteria: string list
          Priority: string option
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type Decision =
        { Id: DecisionId
          Title: string
          Decision: string
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type TaskStatus =
        | Pending
        | InProgress
        | Done
        | Skipped of string

    type WorkTask =
        { Id: TaskId
          Title: string
          Status: TaskStatus
          Owner: string
          Dependencies: TaskId list
          Requirements: RequirementId list
          Decisions: DecisionId list
          RequiredSkills: string list
          RequiredEvidence: EvidenceId list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type EvidenceKind =
        | Implementation
        | Verification
        | Synthetic
        | Deferral
        | Missing

    type EvidenceSubject = { SubjectType: string; Id: string }

    type EvidenceDeclaration =
        { Id: EvidenceId
          Kind: EvidenceKind
          Subject: EvidenceSubject
          TaskRefs: TaskId list
          RequirementRefs: RequirementId list
          ArtifactRefs: ArtifactRef list
          Result: string
          Synthetic: bool
          Rationale: string option
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type LifecycleArtifactContract =
        { Artifact: ArtifactRef
          Purpose: string
          SourceOfTruth: string
          StructuredContract: string
          GeneratedViewRelationship: string
          StaleBehavior: string
          DiagnosticFamily: string list }

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
          Sources: SourceIdentity list
          ExistingGeneratedViews: FileSnapshot list
          GovernanceBoundaries: ArtifactRef list
          Diagnostics: Diagnostic list }

    let normalizePath (path: string) =
        (if isNull path then "" else path.Trim().Replace('\\', '/')).TrimStart('/')

    let artifact path kind owner requiredBySdd =
        match FS.GG.SDD.Artifacts.ArtifactRef.create (normalizePath path) kind owner requiredBySdd with
        | Ok value -> value
        | Error message -> invalidArg (nameof path) message

    let sourceArtifact path kind = artifact path kind Sdd true

    let parseYaml text =
        let stream = YamlStream()
        use reader = new StringReader(if isNull text then "" else text)
        stream.Load reader

        if stream.Documents.Count = 0 then
            None
        else
            Some stream.Documents.[0].RootNode

    let tryMapping (node: YamlNode) =
        match node with
        | :? YamlMappingNode as mapping -> Some mapping
        | _ -> None

    let trySequence (node: YamlNode) =
        match node with
        | :? YamlSequenceNode as sequence -> Some sequence
        | _ -> None

    let tryScalar (node: YamlNode) =
        match node with
        | :? YamlScalarNode as scalar -> Some(if isNull scalar.Value then "" else scalar.Value)
        | _ -> None

    let tryChild key (mapping: YamlMappingNode) =
        mapping.Children
        |> Seq.tryPick (fun pair ->
            match pair.Key with
            | :? YamlScalarNode as scalar when scalar.Value = key -> Some pair.Value
            | _ -> None)

    let rec tryScalarAt keys node =
        match keys with
        | [] -> tryScalar node
        | key :: rest ->
            node
            |> tryMapping
            |> Option.bind (tryChild key)
            |> Option.bind (tryScalarAt rest)

    let tryNodeAt keys node =
        let rec loop remaining current =
            match remaining with
            | [] -> Some current
            | key :: rest ->
                current
                |> tryMapping
                |> Option.bind (tryChild key)
                |> Option.bind (loop rest)

        loop keys node

    let trySequenceAt keys node =
        tryNodeAt keys node |> Option.bind trySequence

    let scalarListFromNode node =
        node
        |> trySequence
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.choose tryScalar
            |> Seq.map (fun value -> value.Trim())
            |> Seq.filter (String.IsNullOrWhiteSpace >> not)
            |> Seq.toList)
        |> Option.defaultValue []

    let scalarList keys node =
        tryNodeAt keys node |> Option.map scalarListFromNode |> Option.defaultValue []

    let boolAt keys node defaultValue =
        match tryScalarAt keys node with
        | Some value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> true
        | Some value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> false
        | _ -> defaultValue

    let schemaVersion artifact root =
        match tryScalarAt [ "schemaVersion" ] root with
        | None ->
            None, [ Diagnostics.malformedSchemaVersion artifact $"Artifact '{artifact.Path}' is missing schemaVersion." ]
        | Some raw ->
            match SchemaVersion.parse raw with
            | Error message -> None, [ Diagnostics.malformedSchemaVersion artifact message ]
            | Ok version when not (SchemaVersion.isSupported version) ->
                Some version, [ Diagnostics.unsupportedSchemaVersion artifact raw ]
            | Ok version -> Some version, []

    let requiredScalar artifact label keys root =
        match tryScalarAt keys root with
        | Some value when not (String.IsNullOrWhiteSpace value) -> Ok value
        | _ ->
            let dottedPath = String.concat "." keys
            Error
                [ Diagnostics.workModelInconsistent
                      artifact
                      $"Required field '{label}' is missing."
                      $"Add '{dottedPath}' to '{artifact.Path}'."
                      [ label ] ]

    let combine errors =
        errors |> List.collect id

    let standardArtifactContracts () =
        let row path kind purpose generated diagnostics =
            { Artifact = sourceArtifact path kind
              Purpose = purpose
              SourceOfTruth = path
              StructuredContract = "schemaVersion: 1 structured lifecycle data"
              GeneratedViewRelationship = generated
              StaleBehavior = "staleGeneratedView diagnostic when source digest, schema version, generator version, or output digest differs"
              DiagnosticFamily = diagnostics }

        [ row ".fsgg/project.yml" ArtifactKind.ProjectConfig "Project identity and lifecycle roots." "Contributes project identity to readiness/<id>/work-model.json." [ "missingArtifact"; "malformedSchemaVersion" ]
          row ".fsgg/sdd.yml" ArtifactKind.SddConfig "SDD lifecycle policy and artifact layout." "Contributes lifecycle policy to work-model, analysis, verify, and ship views." [ "missingArtifact"; "malformedSchemaVersion"; "unsupportedSchemaVersion" ]
          row ".fsgg/agents.yml" ArtifactKind.AgentsConfig "Agent guidance targets for Claude and Codex." "Contributes to readiness/<id>/agent-commands/." [ "missingArtifact"; "staleGeneratedView" ]
          row "work/<id>/charter.md" (ArtifactKind.Other "charter") "Optional local charter and boundary statement." "Contributes authored context to readiness summaries when present." [ "missingArtifact"; "proseStructuredMismatch" ]
          row "work/<id>/spec.md" ArtifactKind.Spec "User value, requirements, scenarios, and structured work metadata." "Sources requirements and work metadata in work-model.json." [ "missingArtifact"; "requirementNotTyped"; "proseStructuredMismatch" ]
          row "work/<id>/clarifications.md" ArtifactKind.Clarifications "Clarification answers and material ambiguity decisions." "Sources decision entries in work-model.json." [ "missingArtifact"; "unknownReference" ]
          row "work/<id>/checklist.md" ArtifactKind.Checklist "Requirements-quality review checklist." "Feeds analysis and verify readiness views." [ "missingArtifact"; "workModelInconsistent" ]
          row "work/<id>/plan.md" ArtifactKind.Plan "Technical plan, contracts, risks, and verification strategy." "Sources decisions and plan obligations." [ "missingArtifact"; "unknownReference"; "proseStructuredMismatch" ]
          row "work/<id>/contracts/" ArtifactKind.Contracts "Public and tool-facing contracts attached to the plan." "Referenced by rule contracts and work-model sources." [ "missingArtifact"; "unknownReference" ]
          row "work/<id>/tasks.yml" ArtifactKind.Tasks "Typed implementation task graph." "Sources task entries in work-model.json and verify readiness." [ "missingArtifact"; "duplicateIdentifier"; "unknownReference"; "workModelInconsistent" ]
          row "work/<id>/evidence.yml" ArtifactKind.Evidence "Implementation and verification evidence declarations." "Sources evidence entries in work-model.json and ship readiness." [ "missingArtifact"; "unknownReference"; "workModelInconsistent" ]
          row "readiness/<id>/work-model.json" ArtifactKind.GeneratedView "Deterministic normalized lifecycle contract." "Generated from SDD sources and used by tools and agents." [ "staleGeneratedView"; "malformedDigest" ]
          row "readiness/<id>/analysis.json" ArtifactKind.GeneratedView "Cross-artifact consistency diagnostics." "Generated from normalized work model diagnostics." [ "staleGeneratedView" ]
          row "readiness/<id>/verify.json" ArtifactKind.GeneratedView "SDD verification readiness facts." "Generated from work model and evidence declarations." [ "staleGeneratedView" ]
          row "readiness/<id>/ship.json" ArtifactKind.GeneratedView "Merge-boundary SDD readiness facts." "Generated from verify readiness and evidence declarations." [ "staleGeneratedView" ]
          row "readiness/<id>/summary.md" ArtifactKind.GeneratedView "Human-readable readiness summary." "Rendered projection over structured readiness facts." [ "staleGeneratedView" ]
          row "readiness/<id>/agent-commands/" ArtifactKind.GeneratedView "Generated Claude/Codex command guidance." "Projection from lifecycle model, never authority." [ "staleGeneratedView" ] ]

    let parseProjectConfig snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.ProjectConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Project config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let fields =
                [ requiredScalar artifact "project.id" [ "project"; "id" ] root
                  requiredScalar artifact "project.defaultWorkRoot" [ "project"; "defaultWorkRoot" ] root
                  requiredScalar artifact "sdd.config" [ "sdd"; "config" ] root
                  requiredScalar artifact "sdd.agents" [ "sdd"; "agents" ] root ]

            let fieldDiagnostics =
                fields
                |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)
                |> combine

            match version, fields, versionDiagnostics @ fieldDiagnostics with
            | Some schema, [ Ok projectId; Ok workRoot; Ok sddPath; Ok agentsPath ], [] ->
                Ok
                    { SchemaVersion = schema
                      ProjectId = projectId
                      DefaultWorkRoot = workRoot
                      SddConfigPath = sddPath
                      AgentsConfigPath = agentsPath
                      GovernancePolicyPath = tryScalarAt [ "governance"; "policy" ] root
                      GovernanceCapabilitiesPath = tryScalarAt [ "governance"; "capabilities" ] root
                      GovernanceToolingPath = tryScalarAt [ "governance"; "tooling" ] root }
            | _ -> Error(versionDiagnostics @ fieldDiagnostics)

    let parseSddLifecyclePolicy snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.SddConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "SDD config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root
            let stageResults = scalarList [ "lifecycle"; "stages" ] root |> List.map Identifiers.parseStage
            let stageDiagnostics =
                stageResults
                |> List.choose (function
                    | Ok _ -> None
                    | Error message -> Some(Diagnostics.workModelInconsistent artifact message "Use one of the standard SDD lifecycle stage ids." []))

            match version with
            | Some schema when List.isEmpty versionDiagnostics && List.isEmpty stageDiagnostics ->
                Ok
                    { SchemaVersion = schema
                      Stages = stageResults |> List.choose (function Ok stage -> Some stage | Error _ -> None)
                      WorkRoot = tryScalarAt [ "artifacts"; "workRoot" ] root |> Option.defaultValue "work"
                      ReadinessRoot = tryScalarAt [ "artifacts"; "readinessRoot" ] root |> Option.defaultValue "readiness"
                      RequireSourceDigests = boolAt [ "generatedViews"; "requireSourceDigests" ] root true
                      RequireGeneratorVersion = boolAt [ "generatedViews"; "requireGeneratorVersion" ] root true
                      StaleBehavior = tryScalarAt [ "generatedViews"; "staleBehavior" ] root |> Option.defaultValue "diagnostic" }
            | _ -> Error(versionDiagnostics @ stageDiagnostics)

    let parseAgentGuidanceConfig snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.AgentsConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Agent config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let targets =
                trySequenceAt [ "agents" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.choose (fun node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            match tryScalarAt [ "id" ] mapping, tryScalarAt [ "guidancePath" ] mapping, tryScalarAt [ "generatedRoot" ] mapping with
                            | Some id, Some guidancePath, Some generatedRoot ->
                                Some { Id = id; GuidancePath = guidancePath; GeneratedRoot = generatedRoot }
                            | _ -> None))
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some schema, [] ->
                Ok
                    { SchemaVersion = schema
                      Targets = targets
                      WorkModelPath = tryScalarAt [ "sourceModel"; "workModel" ] root |> Option.defaultValue "readiness/{workId}/work-model.json"
                      GeneratedGuidanceIsAuthority = boolAt [ "policy"; "generatedGuidanceIsAuthority" ] root false
                      RequireEquivalentClaudeAndCodexBehavior = boolAt [ "policy"; "requireEquivalentClaudeAndCodexBehavior" ] root true }
            | _ -> Error versionDiagnostics

    let frontMatter (snapshot: FileSnapshot) : (string * string) option =
        let normalized = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
        let lines = normalized.Split('\n')

        if lines.Length > 0 && lines.[0].Trim() = "---" then
            let endIndex =
                lines
                |> Array.mapi (fun index line -> index, line)
                |> Array.tryFind (fun (index, line) -> index > 0 && line.Trim() = "---")
                |> Option.map fst

            match endIndex with
            | Some index ->
                let yaml = lines.[1 .. index - 1] |> String.concat "\n"
                let body = lines.[index + 1 ..] |> String.concat "\n"
                Some(yaml, body)
            | None -> None
        else
            None

    let proseStatus (text: string) =
        Regex.Match(text, @"(?im)^Prose status:\s*(\S+)\s*$")
        |> fun m -> if m.Success then Some m.Groups.[1].Value else None

    let parseWorkItemMetadata (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Work item spec is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Work item front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)

                match version, workId, stage, versionDiagnostics with
                | Some schema, Some workId, Some stage, [] ->
                    Ok
                        { SchemaVersion = schema
                          WorkId = workId
                          Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                          Stage = stage
                          ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                          Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                          ProseStatus = proseStatus body }
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent artifact "Work item metadata is incomplete." "Add workId, title, stage, changeTier, and status to spec front matter." [] ])

    let sourceLocation line = Some { Line = Some line; Column = Some 1 }

    let parseRequirements snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec
        let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m = Regex.Match(line, @"^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createRequirementId m.Groups.[1].Value with
                | Ok id ->
                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Text = m.Groups.[2].Value.Trim()
                          AcceptanceCriteria = []
                          Priority = None
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let parseDecisions snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec
        let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m = Regex.Match(line, @"^\s*-\s*(DEC-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createDecisionId m.Groups.[1].Value with
                | Ok id ->
                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Decision = m.Groups.[2].Value.Trim()
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let parseTaskStatus (value: string) =
        match if isNull value then "" else value.Trim().ToLowerInvariant() with
        | "pending" -> Pending
        | "in-progress" -> InProgress
        | "done" -> Done
        | "skipped" -> Skipped "No rationale provided."
        | _ -> Pending

    let parseTaskIds values =
        values |> List.choose (Identifiers.createTaskId >> Result.toOption)

    let parseRequirementIds values =
        values |> List.choose (Identifiers.createRequirementId >> Result.toOption)

    let parseDecisionIds values =
        values |> List.choose (Identifiers.createDecisionId >> Result.toOption)

    let parseEvidenceIds values =
        values |> List.choose (Identifiers.createEvidenceId >> Result.toOption)

    let parseTasks snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Tasks

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Tasks file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let tasks =
                trySequenceAt [ "tasks" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.mapi (fun index node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            tryScalarAt [ "id" ] mapping
                            |> Option.bind (Identifiers.createTaskId >> Result.toOption)
                            |> Option.map (fun id ->
                                { Id = id
                                  Title = tryScalarAt [ "title" ] mapping |> Option.defaultValue (Identifiers.taskIdValue id)
                                  Status = tryScalarAt [ "status" ] mapping |> Option.map parseTaskStatus |> Option.defaultValue Pending
                                  Owner = tryScalarAt [ "owner" ] mapping |> Option.defaultValue "unassigned"
                                  Dependencies = scalarList [ "dependencies" ] mapping |> parseTaskIds
                                  Requirements = scalarList [ "requirements" ] mapping |> parseRequirementIds
                                  Decisions = scalarList [ "decisions" ] mapping |> parseDecisionIds
                                  RequiredSkills = scalarList [ "requiredSkills" ] mapping
                                  RequiredEvidence = scalarList [ "requiredEvidence" ] mapping |> parseEvidenceIds
                                  Source = artifact
                                  SourceLocation = sourceLocation (index + 1) })))
                    |> Seq.choose id
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some _, [] -> Ok tasks
            | _ -> Error versionDiagnostics

    let parseEvidenceKind (value: string) =
        match if isNull value then "" else value.Trim().ToLowerInvariant() with
        | "implementation" -> Implementation
        | "verification" -> Verification
        | "synthetic" -> Synthetic
        | "deferral" -> Deferral
        | "missing" -> Missing
        | _ -> Verification

    let parseArtifactRefs values =
        values
        |> List.map (fun path -> artifact path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false)

    let parseEvidence snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Evidence

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Evidence file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let evidence =
                trySequenceAt [ "evidence" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.mapi (fun index node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            tryScalarAt [ "id" ] mapping
                            |> Option.bind (Identifiers.createEvidenceId >> Result.toOption)
                            |> Option.map (fun id ->
                                let subjectType = tryScalarAt [ "subject"; "type" ] mapping |> Option.defaultValue "task"
                                let subjectId = tryScalarAt [ "subject"; "id" ] mapping |> Option.defaultValue ""
                                let taskRefs =
                                    match subjectType with
                                    | "task" -> subjectId :: scalarList [ "taskRefs" ] mapping
                                    | _ -> scalarList [ "taskRefs" ] mapping
                                    |> parseTaskIds

                                let requirementRefs =
                                    match subjectType with
                                    | "requirement" -> subjectId :: scalarList [ "requirementRefs" ] mapping
                                    | _ -> scalarList [ "requirementRefs" ] mapping
                                    |> parseRequirementIds

                                { Id = id
                                  Kind = tryScalarAt [ "kind" ] mapping |> Option.map parseEvidenceKind |> Option.defaultValue Verification
                                  Subject = { SubjectType = subjectType; Id = subjectId }
                                  TaskRefs = taskRefs
                                  RequirementRefs = requirementRefs
                                  ArtifactRefs = scalarList [ "artifacts" ] mapping |> parseArtifactRefs
                                  Result = tryScalarAt [ "result" ] mapping |> Option.defaultValue "pending"
                                  Synthetic = boolAt [ "synthetic" ] mapping false
                                  Rationale = tryScalarAt [ "rationale" ] mapping
                                  Source = artifact
                                  SourceLocation = sourceLocation (index + 1) })))
                    |> Seq.choose id
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some _, [] -> Ok evidence
            | _ -> Error versionDiagnostics

    let requiredFiles workId =
        [ ".fsgg/project.yml", ArtifactKind.ProjectConfig
          ".fsgg/sdd.yml", ArtifactKind.SddConfig
          ".fsgg/agents.yml", ArtifactKind.AgentsConfig
          $"work/{workId}/spec.md", ArtifactKind.Spec
          $"work/{workId}/tasks.yml", ArtifactKind.Tasks
          $"work/{workId}/evidence.yml", ArtifactKind.Evidence ]

    let sourceIdentity snapshot kind =
        let source = sourceArtifact snapshot.Path kind
        { Artifact = source; Digest = SchemaVersion.sha256Text snapshot.Text }

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

    let loadWorkItemFromSnapshots snapshots workId =
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
        let requirements = specSnapshot |> Option.map parseRequirements |> Option.defaultValue []
        let decisions = specSnapshot |> Option.map parseDecisions |> Option.defaultValue []

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
            |> Option.defaultValue (if path.Contains("/readiness/") || path.StartsWith("readiness/") then ArtifactKind.GeneratedView else ArtifactKind.Other "source")

        let sources =
            normalized
            |> List.filter (fun snapshot -> not (snapshot.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
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

        { WorkId = parsedWorkId
          Project = project
          SddPolicy = sdd
          Agents = agents
          Metadata = metadata
          Requirements = requirements
          Decisions = decisions
          Tasks = tasks
          Evidence = evidence
          Sources = sources
          ExistingGeneratedViews = generatedViews
          GovernanceBoundaries = governanceBoundaries
          Diagnostics =
            missingDiagnostics
            @ projectDiagnostics
            @ sddDiagnostics
            @ agentDiagnostics
            @ metadataDiagnostics
            @ taskDiagnostics
            @ evidenceDiagnostics }
