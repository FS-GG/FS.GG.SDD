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

    type SpecificationFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          PublicOrToolFacingImpact: bool option }

    type SpecificationRequirementReference =
        { RequirementId: RequirementId
          StoryIds: UserStoryId list
          AcceptanceScenarioIds: AcceptanceScenarioId list
          SourceLocation: SourceLocation option }

    type SpecificationFacts =
        { FrontMatter: SpecificationFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          UserStoryIds: UserStoryId list
          RequirementIds: RequirementId list
          AcceptanceScenarioIds: AcceptanceScenarioId list
          ScopeBoundaryIds: ScopeBoundaryId list
          AmbiguityIds: AmbiguityId list
          RequirementReferences: SpecificationRequirementReference list
          UnresolvedAmbiguityCount: int
          Diagnostics: Diagnostic list }

    type ClarificationDecisionKind =
        | ConcreteDecision
        | AcceptedDeferral

    type ClarificationAnswerKind =
        | DecisionAnswer
        | AcceptedDeferralAnswer
        | StillOpenAnswer
        | NoteAnswer

    type ClarificationFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          PublicOrToolFacingImpact: bool option }

    type ClarificationQuestion =
        { QuestionId: ClarificationQuestionId
          Prompt: string
          SourceAmbiguityIds: AmbiguityId list
          RelatedRequirementIds: RequirementId list
          RelatedStoryIds: UserStoryId list
          RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
          Blocking: bool
          State: string
          SourceLocation: SourceLocation option }

    type ClarificationAnswer =
        { QuestionId: ClarificationQuestionId option
          AmbiguityIds: AmbiguityId list
          Text: string
          Kind: ClarificationAnswerKind
          SourceLocation: SourceLocation option }

    type ClarificationDecisionFact =
        { DecisionId: DecisionId
          Title: string
          Kind: ClarificationDecisionKind
          Text: string
          Rationale: string option
          SourceQuestionIds: ClarificationQuestionId list
          SourceAmbiguityIds: AmbiguityId list
          RelatedRequirementIds: RequirementId list
          RelatedStoryIds: UserStoryId list
          RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
          SourceLocation: SourceLocation option }

    type RemainingAmbiguity =
        { AmbiguityId: AmbiguityId option
          QuestionId: ClarificationQuestionId option
          State: string
          Explanation: string
          RequiredCorrection: string
          SourceLocation: SourceLocation option }

    type ClarificationFacts =
        { FrontMatter: ClarificationFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          Questions: ClarificationQuestion list
          Answers: ClarificationAnswer list
          Decisions: ClarificationDecisionFact list
          AcceptedDeferrals: ClarificationDecisionFact list
          RemainingAmbiguity: RemainingAmbiguity list
          BlockingAmbiguityCount: int
          Diagnostics: Diagnostic list }

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

    type MarkdownRequirementMention =
        { Id: string
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
          MarkdownRequirementMentions: MarkdownRequirementMention list
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

    let schemaVersion (artifact: ArtifactRef) (root: YamlNode) =
        let raw = tryScalarAt [ "schemaVersion" ] root
        let compatibility = SchemaVersion.classifyRaw raw

        match compatibility.Status with
        | SchemaCompatibilityStatus.Current
        | SchemaCompatibilityStatus.Deprecated -> compatibility.Version, []
        | SchemaCompatibilityStatus.Malformed ->
            let message =
                if String.IsNullOrWhiteSpace compatibility.RawValue then
                    $"Artifact '{artifact.Path}' is missing schemaVersion."
                else
                    defaultArg compatibility.MigrationHint "Schema version is malformed."

            None, [ Diagnostics.malformedSchemaVersion artifact message ]
        | SchemaCompatibilityStatus.Unsupported ->
            compatibility.Version, [ Diagnostics.unsupportedSchemaVersion artifact compatibility.RawValue ]
        | SchemaCompatibilityStatus.Future ->
            compatibility.Version, [ Diagnostics.futureSchemaVersion artifact compatibility.RawValue ]

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

    let parseProjectConfig (snapshot: FileSnapshot) =
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

    let parseSddLifecyclePolicy (snapshot: FileSnapshot) =
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

    let parseAgentGuidanceConfig (snapshot: FileSnapshot) =
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

    let specificationStandardSections () =
        [ "User Value"
          "Scope"
          "Non-Goals"
          "User Stories"
          "Acceptance Scenarios"
          "Functional Requirements"
          "Ambiguities"
          "Public Or Tool-Facing Impact"
          "Lifecycle Notes" ]

    let hasHeading (heading: string) (text: string) =
        Regex.IsMatch(text, $"(?m)^##\\s+{Regex.Escape heading}\\s*$")

    let boolScalarAt keys root =
        match tryScalarAt keys root with
        | Some value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> Some true
        | Some value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> Some false
        | _ -> None

    let parseSpecificationFrontMatter snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Specification is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Specification front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)

                match version, workId, stage, versionDiagnostics with
                | Some schema, Some workId, Some stage, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent artifact "Specification front matter is incomplete." "Add schemaVersion, workId, title, stage, changeTier, and status to spec front matter." [] ])

    let sectionLines (heading: string) (text: string) =
        let normalized = text.Replace("\r\n", "\n")
        let lines = normalized.Split('\n')

        let start =
            lines
            |> Array.tryFindIndex (fun line -> Regex.IsMatch(line, $"^##\\s+{Regex.Escape heading}\\s*$"))

        match start with
        | None -> []
        | Some index ->
            lines.[index + 1 ..]
            |> Array.takeWhile (fun line -> not (Regex.IsMatch(line, "^##\\s+")))
            |> Array.mapi (fun offset line -> index + offset + 2, line)
            |> Array.toList

    let scopedIdLocations (pattern: string) (createId: string -> Result<'id, string>) (lines: (int * string) list) =
        lines
        |> List.toArray
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.collect (fun (_, (lineNumber, line)) ->
            Regex.Matches(line, pattern, RegexOptions.IgnoreCase)
            |> Seq.cast<Match>
            |> Seq.choose (fun m ->
                match createId m.Value with
                | Ok id -> Some(id, sourceLocation lineNumber)
                | Error _ -> None)
            |> Seq.toArray)
        |> Array.toList

    let scopedIdLocationsInSections headings pattern createId text =
        headings
        |> List.collect (fun heading -> sectionLines heading text)
        |> scopedIdLocations pattern createId

    let duplicateScopedDiagnostics artifact (idValue: 'id -> string) (values: ('id * SourceLocation option) list) =
        values
        |> List.groupBy (fst >> idValue)
        |> List.choose (fun (id, group) ->
            if List.length group > 1 then
                Some(Diagnostics.duplicateIdentifier artifact id (group |> List.choose snd))
            else
                None)

    let missingIdDiagnostics artifact (text: string) =
        let missing (heading: string) (pattern: string) (relatedId: string) =
            sectionLines heading text
            |> List.choose (fun (lineNumber, line) ->
                if Regex.IsMatch(line, @"^\s*-\s+\S", RegexOptions.IgnoreCase)
                   && not (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)) then
                    Some(
                        Diagnostics.workModelInconsistent
                            artifact
                            $"Specification list item in '{heading}' is missing a required stable id."
                            $"Add a stable {relatedId} id to the list item before rerunning."
                            [ relatedId ])
                else
                    None)

        [ missing "User Stories" @"\bUS-\d{3,}\b" "US-###"
          missing "Acceptance Scenarios" @"\bAC-\d{3,}\b" "AC-###"
          missing "Functional Requirements" @"\bFR-\d{3,}\b" "FR-###"
          missing "Ambiguities" @"\bAMB-\d{3,}\b" "AMB-###" ]
        |> List.concat

    let requirementReferences (text: string) : SpecificationRequirementReference list =
        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m = Regex.Match(line, @"^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createRequirementId m.Groups.[1].Value with
                | Ok requirementId ->
                    let storyIds =
                        Regex.Matches(line, @"\bUS-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.choose (fun value -> Identifiers.createUserStoryId value.Value |> Result.toOption)
                        |> Seq.distinctBy (fun id -> id.Value)
                        |> Seq.toList

                    let acceptanceScenarioIds =
                        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.choose (fun value -> Identifiers.createAcceptanceScenarioId value.Value |> Result.toOption)
                        |> Seq.distinctBy (fun id -> id.Value)
                        |> Seq.toList

                    Some
                        { RequirementId = requirementId
                          StoryIds = storyIds
                          AcceptanceScenarioIds = acceptanceScenarioIds
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let unknownSpecificationReferences
        artifact
        (stories: (UserStoryId * SourceLocation option) list)
        (acceptanceScenarios: (AcceptanceScenarioId * SourceLocation option) list)
        (references: SpecificationRequirementReference list)
        =
        let storyIds = stories |> List.map (fun (id, _) -> id.Value) |> Set.ofList
        let acceptanceIds = acceptanceScenarios |> List.map (fun (id, _) -> id.Value) |> Set.ofList

        references
        |> List.collect (fun reference ->
            [ reference.StoryIds
              |> List.choose (fun id ->
                  if Set.contains id.Value storyIds then
                      None
                  else
                      Some(Diagnostics.unknownReference artifact id.Value "Declare the user story id in the specification or remove the requirement link."))
              reference.AcceptanceScenarioIds
              |> List.choose (fun id ->
                  if Set.contains id.Value acceptanceIds then
                      None
                  else
                      Some(Diagnostics.unknownReference artifact id.Value "Declare the acceptance scenario id in the specification or remove the requirement link.")) ]
            |> List.concat)

    let parseSpecificationFacts snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        match parseSpecificationFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, body) ->
            let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = specificationStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let stories = scopedIdLocationsInSections [ "User Stories" ] @"\bUS-\d{3,}\b" Identifiers.createUserStoryId text
            let requirements = scopedIdLocationsInSections [ "Functional Requirements" ] @"\bFR-\d{3,}\b" Identifiers.createRequirementId text
            let acceptanceScenarios = scopedIdLocationsInSections [ "Acceptance Scenarios" ] @"\bAC-\d{3,}\b" Identifiers.createAcceptanceScenarioId text
            let scopeBoundaries = scopedIdLocationsInSections [ "Scope"; "Non-Goals" ] @"\bSB-\d{3,}\b" Identifiers.createScopeBoundaryId text
            let ambiguities = scopedIdLocationsInSections [ "Ambiguities" ] @"\bAMB-\d{3,}\b" Identifiers.createAmbiguityId text
            let references = requirementReferences text

            let unresolvedAmbiguityCount =
                body.Split('\n')
                |> Array.filter (fun line ->
                    Regex.IsMatch(line, @"\bAMB-\d{3,}\b", RegexOptions.IgnoreCase)
                    && not (Regex.IsMatch(line, @"\b(resolved|deferred)\b", RegexOptions.IgnoreCase)))
                |> Array.length

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: UserStoryId) -> id.Value) stories
                  duplicateScopedDiagnostics artifact (fun (id: RequirementId) -> id.Value) requirements
                  duplicateScopedDiagnostics artifact (fun (id: AcceptanceScenarioId) -> id.Value) acceptanceScenarios
                  duplicateScopedDiagnostics artifact (fun (id: ScopeBoundaryId) -> id.Value) scopeBoundaries
                  duplicateScopedDiagnostics artifact (fun (id: AmbiguityId) -> id.Value) ambiguities
                  missingIdDiagnostics artifact text
                  unknownSpecificationReferences artifact stories acceptanceScenarios references ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  UserStoryIds = stories |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  RequirementIds = requirements |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  AcceptanceScenarioIds = acceptanceScenarios |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  ScopeBoundaryIds = scopeBoundaries |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  AmbiguityIds = ambiguities |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  RequirementReferences = references |> List.sortBy (fun reference -> reference.RequirementId.Value)
                  UnresolvedAmbiguityCount = unresolvedAmbiguityCount
                  Diagnostics = diagnostics }

    let clarificationStandardSections () =
        [ "Source Specification"
          "Clarification Questions"
          "Answers"
          "Decisions"
          "Accepted Deferrals"
          "Remaining Ambiguity"
          "Lifecycle Notes" ]

    let parseClarificationFrontMatter snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Clarifications

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Clarification artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Clarification front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)
                let sourceSpec = tryScalarAt [ "sourceSpec" ] root

                match version, workId, stage, sourceSpec, versionDiagnostics with
                | Some schema, Some workId, Some stage, Some sourceSpec, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "needsAnswers"
                           SourceSpec = sourceSpec
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent
                                 artifact
                                 "Clarification front matter is incomplete."
                                 "Add schemaVersion, workId, title, stage: clarify, changeTier, status, and sourceSpec to clarifications.md."
                                 [] ])

    let idsInLine pattern createId line =
        Regex.Matches(line, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> createId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> string id)
        |> Seq.toList

    let questionIdsInLine line =
        Regex.Matches(line, @"\bCQ-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createClarificationQuestionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let ambiguityIdsInLine line =
        Regex.Matches(line, @"\bAMB-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createAmbiguityId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let requirementIdsInLine line =
        Regex.Matches(line, @"\bFR-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createRequirementId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let storyIdsInLine line =
        Regex.Matches(line, @"\bUS-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createUserStoryId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let acceptanceScenarioIdsInLine line =
        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createAcceptanceScenarioId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let decisionIdsInLine line =
        Regex.Matches(line, @"\bDEC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createDecisionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let cleanAfterId (idValue: string) (line: string) =
        let index = line.IndexOf(idValue, StringComparison.OrdinalIgnoreCase)

        if index < 0 then
            line.Trim().TrimStart('-', '*').Trim()
        else
            line.Substring(index + idValue.Length).Trim().TrimStart(':', '-', ' ').Trim()

    let cleanDecisionText text =
        Regex.Replace(text, @"^(?:\[[^\]]+\]\s*)+:\s*", "", RegexOptions.CultureInvariant).Trim()

    let parseClarificationQuestions text =
        sectionLines "Clarification Questions" text
        |> List.choose (fun (lineNumber, line) ->
            match questionIdsInLine line |> List.tryHead with
            | Some questionId ->
                let lowered = line.ToLowerInvariant()

                Some
                    { QuestionId = questionId
                      Prompt = cleanAfterId questionId.Value line
                      SourceAmbiguityIds = ambiguityIdsInLine line
                      RelatedRequirementIds = requirementIdsInLine line
                      RelatedStoryIds = storyIdsInLine line
                      RelatedAcceptanceScenarioIds = acceptanceScenarioIdsInLine line
                      Blocking = not (lowered.Contains("non-blocking"))
                      State =
                        if lowered.Contains("answered") then "answered"
                        elif lowered.Contains("deferred") then "deferred"
                        else "open"
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let answerKind (line: string) =
        let lowered = line.ToLowerInvariant()

        if lowered.Contains("accepted deferral") || lowered.Contains("defer") then AcceptedDeferralAnswer
        elif lowered.Contains("still open") || lowered.Contains("unresolved") then StillOpenAnswer
        elif lowered.Contains("note") then NoteAnswer
        else DecisionAnswer

    let parseClarificationAnswers text =
        sectionLines "Answers" text
        |> List.choose (fun (lineNumber, line) ->
            let question = questionIdsInLine line |> List.tryHead
            let ambiguities = ambiguityIdsInLine line

            if Option.isNone question && List.isEmpty ambiguities then
                None
            else
                Some
                    { QuestionId = question
                      AmbiguityIds = ambiguities
                      Text = line.Trim().TrimStart('-', '*').Trim()
                      Kind = answerKind line
                      SourceLocation = sourceLocation lineNumber })

    let parseClarificationDecisionsInSection heading kind text =
        sectionLines heading text
        |> List.choose (fun (lineNumber, line) ->
            match decisionIdsInLine line |> List.tryHead with
            | Some decisionId ->
                let decisionText = cleanAfterId decisionId.Value line |> cleanDecisionText

                Some
                    { DecisionId = decisionId
                      Title = decisionText
                      Kind = kind
                      Text = decisionText
                      Rationale = None
                      SourceQuestionIds = questionIdsInLine line
                      SourceAmbiguityIds = ambiguityIdsInLine line
                      RelatedRequirementIds = requirementIdsInLine line
                      RelatedStoryIds = storyIdsInLine line
                      RelatedAcceptanceScenarioIds = acceptanceScenarioIdsInLine line
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseRemainingAmbiguity text =
        sectionLines "Remaining Ambiguity" text
        |> List.choose (fun (lineNumber, line) ->
            let ambiguity = ambiguityIdsInLine line |> List.tryHead
            let question = questionIdsInLine line |> List.tryHead

            if Option.isNone ambiguity && Option.isNone question then
                None
            else
                let lowered = line.ToLowerInvariant()
                let state =
                    if lowered.Contains("accepted deferral") || lowered.Contains("deferred") then "acceptedDeferral"
                    elif lowered.Contains("non-blocking") then "nonBlocking"
                    else "blocking"

                Some
                    { AmbiguityId = ambiguity
                      QuestionId = question
                      State = state
                      Explanation = line.Trim().TrimStart('-', '*').Trim()
                      RequiredCorrection =
                        if state = "blocking" then "Provide a concrete decision, accepted deferral, or mark the ambiguity non-blocking."
                        else "Keep the ambiguity visible to later lifecycle stages."
                      SourceLocation = sourceLocation lineNumber })

    let parseClarificationFacts snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Clarifications

        match parseClarificationFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = clarificationStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let questions = parseClarificationQuestions text
            let answers = parseClarificationAnswers text
            let decisions = parseClarificationDecisionsInSection "Decisions" ConcreteDecision text
            let deferrals = parseClarificationDecisionsInSection "Accepted Deferrals" AcceptedDeferral text
            let remaining = parseRemainingAmbiguity text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: ClarificationQuestionId) -> id.Value) (questions |> List.map (fun q -> q.QuestionId, q.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: DecisionId) -> id.Value) ((decisions @ deferrals) |> List.map (fun decision -> decision.DecisionId, decision.SourceLocation))
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Clarification artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to clarifications.md before relying on the parsed facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  Questions = questions |> List.sortBy (fun question -> question.QuestionId.Value)
                  Answers = answers
                  Decisions = decisions |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  AcceptedDeferrals = deferrals |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  RemainingAmbiguity = remaining
                  BlockingAmbiguityCount = remaining |> List.filter (fun item -> item.State = "blocking") |> List.length
                  Diagnostics = diagnostics }

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
                    let acceptanceCriteria =
                        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
                        |> Seq.distinct
                        |> Seq.toList

                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Text = m.Groups.[2].Value.Trim()
                          AcceptanceCriteria = acceptanceCriteria
                          Priority = None
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let parseMarkdownRequirementMentions snapshot =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec
        let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.collect (fun (lineNumber, line) ->
            Regex.Matches(line, @"\b(?:FR|AC)-\d{3,}\b", RegexOptions.IgnoreCase)
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                { Id = m.Value.ToUpperInvariant()
                  Source = artifact
                  SourceLocation = sourceLocation lineNumber })
            |> Seq.toArray)
        |> Array.toList

    let parseDecisions snapshot =
        let kind =
            let path = normalizePath snapshot.Path

            if path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then
                ArtifactKind.Clarifications
            else
                ArtifactKind.Spec

        let artifact = sourceArtifact snapshot.Path kind
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

    let parseTasks (snapshot: FileSnapshot) =
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

    let parseEvidence (snapshot: FileSnapshot) =
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

    let sourceIdentity snapshot kind =
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
