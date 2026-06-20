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

    type ChecklistFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          PublicOrToolFacingImpact: bool option }

    type ChecklistSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type ChecklistItem =
        { ItemId: ChecklistItemId
          Text: string
          Blocking: bool
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type ChecklistReviewResult =
        { ResultId: ChecklistResultId
          ItemId: ChecklistItemId option
          Status: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type ChecklistFacts =
        { FrontMatter: ChecklistFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          SourceSnapshots: ChecklistSourceSnapshot list
          Items: ChecklistItem list
          Results: ChecklistReviewResult list
          AcceptedDeferrals: ChecklistReviewResult list
          BlockingFindings: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleResultCount: int
          Diagnostics: Diagnostic list }

    type PlanFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          PublicOrToolFacingImpact: bool option }

    type PlanSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type PlanDecision =
        { DecisionId: PlanDecisionId
          Title: string
          Status: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanContractReference =
        { ContractId: PlanContractReferenceId
          Kind: string
          Target: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type VerificationObligation =
        { ObligationId: VerificationObligationId
          Title: string
          EvidenceKind: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanMigrationNote =
        { MigrationId: PlanMigrationNoteId
          Posture: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type GeneratedViewImpact =
        { ImpactId: GeneratedViewImpactId
          Target: string
          CurrencyBehavior: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type AcceptedPlanDeferral =
        { Id: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanFacts =
        { FrontMatter: PlanFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          SourceSnapshots: PlanSourceSnapshot list
          Decisions: PlanDecision list
          ContractReferences: PlanContractReference list
          VerificationObligations: VerificationObligation list
          MigrationNotes: PlanMigrationNote list
          GeneratedViewImpacts: GeneratedViewImpact list
          AcceptedDeferrals: AcceptedPlanDeferral list
          BlockingFindings: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleDecisionCount: int
          Diagnostics: Diagnostic list }

    type TaskFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          PublicOrToolFacingImpact: bool option }

    type TaskSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type TaskGraphFinding =
        { FindingId: string
          Severity: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

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
        | Stale

    type WorkTask =
        { Id: TaskId
          Title: string
          Status: TaskStatus
          Owner: string
          Dependencies: TaskId list
          Requirements: RequirementId list
          Decisions: DecisionId list
          SourceIds: string list
          RequiredSkills: string list
          RequiredEvidence: EvidenceId list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type TaskFacts =
        { FrontMatter: TaskFrontMatter
          SourceSnapshots: TaskSourceSnapshot list
          Tasks: WorkTask list
          AcceptedDeferrals: string list
          Findings: TaskGraphFinding list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleTaskCount: int
          Diagnostics: Diagnostic list }

    type AnalysisSourceRecord =
        { Path: string
          Kind: string
          Digest: SourceDigest option
          SchemaVersion: int option
          SchemaStatus: string option }

    type AnalysisSourceRelationship =
        { Id: string
          SourcePath: string
          TargetPath: string
          SourceId: string option
          TargetId: string option
          Relationship: string
          State: string
          DiagnosticIds: string list }

    type AnalysisFinding =
        { Id: string
          Category: string
          Severity: string
          State: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type AnalysisReadiness =
        { Status: string
          ReadyCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          StaleSourceCount: int
          MissingDispositionCount: int
          MalformedSourceCount: int
          GeneratedViewFindingCount: int
          AcceptedDeferralCount: int }

    type AnalysisGeneratedViewRecord =
        { Path: string
          Kind: string
          Currency: string
          DiagnosticIds: string list }

    type AnalysisOptionalBoundaryFact =
        { Path: string
          Relationship: string
          RequiredBySdd: bool
          State: string
          DiagnosticIds: string list }

    type AnalysisNextAction =
        { ActionId: string
          Command: string option
          Reason: string }

    type AnalysisView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          SourceRelationships: AnalysisSourceRelationship list
          Readiness: AnalysisReadiness
          Findings: AnalysisFinding list
          GeneratedViews: AnalysisGeneratedViewRecord list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          NextAction: AnalysisNextAction option }

    type EvidenceKind =
        | Implementation
        | Verification
        | Review
        | GeneratedViewEvidence
        | Synthetic
        | Deferral
        | Note
        | Missing

    type EvidenceSubject = { SubjectType: string; Id: string }

    type EvidenceSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type EvidenceSourceReference =
        { ReferenceId: string option
          Kind: string
          Path: string option
          Uri: string option
          Digest: string option
          RelatedSourceId: string option
          Result: string option
          SourceLocation: SourceLocation option }

    type SyntheticDisclosure =
        { StandsInFor: string
          Reason: string }

    type EvidenceDeclaration =
        { Id: EvidenceId
          Kind: EvidenceKind
          Subject: EvidenceSubject
          TaskRefs: TaskId list
          RequirementRefs: RequirementId list
          AcceptanceScenarioRefs: AcceptanceScenarioId list
          ClarificationDecisionRefs: DecisionId list
          ChecklistResultRefs: ChecklistResultId list
          PlanDecisionRefs: PlanDecisionId list
          ObligationRefs: string list
          ArtifactRefs: ArtifactRef list
          SourceRefs: EvidenceSourceReference list
          Result: string
          Synthetic: bool
          SyntheticDisclosure: SyntheticDisclosure option
          Rationale: string option
          Owner: string option
          Scope: string option
          LaterLifecycleVisibility: string option
          Notes: string list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type EvidenceDispositionState =
        | EvidenceSupported
        | EvidenceDeferred
        | EvidenceMissingDisposition
        | EvidenceStale
        | EvidenceSyntheticDisposition
        | EvidenceInvalid
        | EvidenceAdvisory
        | EvidenceBlocking

    type EvidenceObligation =
        { ObligationId: string
          Kind: string
          SourceArtifactPath: string
          SourceId: string option
          LinkedTaskIds: TaskId list
          LinkedRequirementIds: RequirementId list
          LinkedDecisionIds: string list
          ExpectedEvidenceKinds: string list
          RequiredSkillOrCapabilityTags: string list
          Blocking: bool
          Correction: string }

    type EvidenceDisposition =
        { DispositionId: string
          ObligationId: string
          State: EvidenceDispositionState
          EvidenceIds: EvidenceId list
          AffectedTaskIds: TaskId list
          AffectedSourceIds: string list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type EvidenceArtifact =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          SourceTasks: string
          SourceAnalysis: string
          SourceSnapshots: EvidenceSourceSnapshot list
          Evidence: EvidenceDeclaration list
          LifecycleNotes: string list
          Diagnostics: Diagnostic list }

    type RequiredTestDispositionState =
        | TestSatisfied
        | TestDeferred
        | TestMissingDisposition
        | TestStale
        | TestSyntheticDisposition
        | TestInvalid
        | TestAdvisory
        | TestBlocking

    type RequiredTestDisposition =
        { DispositionId: string
          ObligationId: string
          State: RequiredTestDispositionState
          EvidenceIds: EvidenceId list
          AffectedTaskIds: TaskId list
          AffectedRequirementIds: RequirementId list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type SkillVisibilityState =
        | SkillVisible
        | SkillMissing

    type SkillVisibilityFact =
        { Skill: string
          RequiringTaskIds: TaskId list
          Visibility: SkillVisibilityState
          SourceArtifactPath: string
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type VerificationFinding =
        { Id: string
          Severity: string
          Category: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type VerificationStageReadiness = { Stage: string; Status: string }

    type VerificationLifecycleReadiness =
        { Stages: VerificationStageReadiness list
          Status: string }

    type VerificationTaskGraphReadiness =
        { TaskCount: int
          DependencyCount: int
          DependenciesValid: bool
          StatusesValid: bool
          FindingIds: string list }

    type VerificationView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          LifecycleReadiness: VerificationLifecycleReadiness
          TaskGraph: VerificationTaskGraphReadiness
          EvidenceDispositions: EvidenceDisposition list
          TestDispositions: RequiredTestDisposition list
          SkillVisibility: SkillVisibilityFact list
          GeneratedViews: AnalysisGeneratedViewRecord list
          Findings: VerificationFinding list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          Readiness: string }

    type ShipReadinessFinding =
        { Id: string
          Severity: string
          Category: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type ShipLifecycleStageReadiness = { Stage: string; Status: string }

    type ShipVerificationReadinessSummary =
        { Status: string
          BlockingFindingIds: string list
          EvidenceSupportedCount: int
          EvidenceDeferredCount: int
          EvidenceMissingCount: int
          EvidenceStaleCount: int
          EvidenceSyntheticCount: int
          EvidenceInvalidCount: int }

    type ShipView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          LifecycleReadiness: ShipLifecycleStageReadiness list
          VerificationReadiness: ShipVerificationReadinessSummary
          Disposition: string
          GeneratedViews: AnalysisGeneratedViewRecord list
          Findings: ShipReadinessFinding list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          Readiness: string }

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

    let parseSpecificationFrontMatter (snapshot: FileSnapshot) =
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

    let parseSpecificationFacts (snapshot: FileSnapshot) =
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

    let parseClarificationFrontMatter (snapshot: FileSnapshot) =
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

    let parseClarificationFacts (snapshot: FileSnapshot) =
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

    let checklistStandardSections () =
        [ "Source Specification"
          "Source Clarifications"
          "Source Snapshot"
          "Checklist Items"
          "Review Results"
          "Accepted Deferrals"
          "Blocking Findings"
          "Advisory Notes"
          "Lifecycle Notes" ]

    let parseChecklistFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Checklist

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Checklist artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Checklist front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)
                let sourceSpec = tryScalarAt [ "sourceSpec" ] root
                let sourceClarifications = tryScalarAt [ "sourceClarifications" ] root

                match version, workId, stage, sourceSpec, sourceClarifications, versionDiagnostics with
                | Some schema, Some workId, Some stage, Some sourceSpec, Some sourceClarifications, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "needsReview"
                           SourceSpec = sourceSpec
                           SourceClarifications = sourceClarifications
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent
                                 artifact
                                 "Checklist front matter is incomplete."
                                 "Add schemaVersion, workId, title, stage: checklist, changeTier, status, sourceSpec, and sourceClarifications to checklist.md."
                                 [] ])

    let checklistItemIdsInLine line =
        Regex.Matches(line, @"\bCHK-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createChecklistItemId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let checklistResultIdsInLine line =
        Regex.Matches(line, @"\bCR-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createChecklistResultId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let sourceIdsInLine line =
        Regex.Matches(line, @"\b(?:FR|US|AC|SB|AMB|CQ|DEC|CHK)-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let parseChecklistSourceSnapshots text : ChecklistSourceSnapshot list =
        sectionLines "Source Snapshot" text
        |> List.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(
                    line,
                    @"^\s*-\s*([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(\S+)(?:\s+sha256:([a-fA-F0-9]{64}))?(?:\s+schemaVersion:(\d+))?",
                    RegexOptions.IgnoreCase)

            if m.Success then
                let schema =
                    if m.Groups.[4].Success then
                        match Int32.TryParse m.Groups.[4].Value with
                        | true, value -> Some value
                        | _ -> None
                    else
                        None

                Some
                    { Label = m.Groups.[1].Value
                      Path = normalizePath m.Groups.[2].Value
                      Digest = if m.Groups.[3].Success then Some(m.Groups.[3].Value.ToLowerInvariant()) else None
                      SchemaVersion = schema
                      SourceLocation = sourceLocation lineNumber }
            else
                None)

    let parseChecklistItems text =
        sectionLines "Checklist Items" text
        |> List.choose (fun (lineNumber, line) ->
            match checklistItemIdsInLine line |> List.tryHead with
            | Some itemId ->
                let lowered = line.ToLowerInvariant()

                Some
                    { ItemId = itemId
                      Text = cleanAfterId itemId.Value line
                      Blocking = not (lowered.Contains("advisory"))
                      SourceIds = sourceIdsInLine line |> List.filter ((<>) itemId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseChecklistResultsInSection heading text =
        sectionLines heading text
        |> List.choose (fun (lineNumber, line) ->
            match checklistResultIdsInLine line |> List.tryHead with
            | Some resultId ->
                let itemId = checklistItemIdsInLine line |> List.tryHead
                let lowered = line.ToLowerInvariant()
                let status =
                    if lowered.Contains("accepteddeferral") || lowered.Contains("accepted deferral") then "acceptedDeferral"
                    elif lowered.Contains("stale") then "stale"
                    elif lowered.Contains("fail") then "fail"
                    elif lowered.Contains("advisory") then "advisory"
                    elif lowered.Contains("pass") then "pass"
                    else "unknown"

                Some
                    { ResultId = resultId
                      ItemId = itemId
                      Status = status
                      Text = cleanAfterId resultId.Value line
                      SourceIds = sourceIdsInLine line |> List.filter (fun value -> itemId |> Option.exists (fun id -> id.Value = value) |> not)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseNonEmptySectionLines heading text =
        sectionLines heading text
        |> List.choose (fun (_, line) ->
            let trimmed = line.Trim().TrimStart('-', '*').Trim()

            if String.IsNullOrWhiteSpace trimmed
               || trimmed.StartsWith("No ", StringComparison.OrdinalIgnoreCase) then
                None
            else
                Some trimmed)

    let checklistReferenceDiagnostics artifact (items: ChecklistItem list) (results: ChecklistReviewResult list) =
        let knownItems = items |> List.map (fun item -> item.ItemId.Value) |> Set.ofList

        results
        |> List.choose (fun result ->
            match result.ItemId with
            | Some itemId when Set.contains itemId.Value knownItems -> None
            | Some itemId -> Some(Diagnostics.unknownReference artifact itemId.Value "Declare the checklist item before recording a review result for it.")
            | None -> Some(Diagnostics.workModelInconsistent artifact $"Checklist result {result.ResultId.Value} is missing a CHK-### item reference." "Add [CHK:CHK-###] to the review result." [ result.ResultId.Value ]))

    let parseChecklistFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Checklist

        match parseChecklistFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = checklistStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let snapshots = parseChecklistSourceSnapshots text
            let items = parseChecklistItems text
            let reviewResults = parseChecklistResultsInSection "Review Results" text
            let acceptedDeferrals = parseChecklistResultsInSection "Accepted Deferrals" text
            let results = reviewResults @ acceptedDeferrals
            let blockingFindings = parseNonEmptySectionLines "Blocking Findings" text
            let advisoryNotes = parseNonEmptySectionLines "Advisory Notes" text
            let lifecycleNotes = parseNonEmptySectionLines "Lifecycle Notes" text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: ChecklistItemId) -> id.Value) (items |> List.map (fun item -> item.ItemId, item.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: ChecklistResultId) -> id.Value) (results |> List.map (fun result -> result.ResultId, result.SourceLocation))
                  checklistReferenceDiagnostics artifact items results
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Checklist artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to checklist.md before relying on the parsed facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  SourceSnapshots = snapshots |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                  Items = items |> List.sortBy (fun item -> item.ItemId.Value)
                  Results = results |> List.sortBy (fun result -> result.ResultId.Value)
                  AcceptedDeferrals = acceptedDeferrals |> List.sortBy (fun result -> result.ResultId.Value)
                  BlockingFindings = blockingFindings |> List.sort
                  AdvisoryNotes = advisoryNotes |> List.sort
                  LifecycleNotes = lifecycleNotes
                  StaleResultCount = results |> List.filter (fun result -> result.Status = "stale") |> List.length
                  Diagnostics = diagnostics }

    let planStandardSections () =
        [ "Source Snapshot"
          "Plan Scope"
          "Plan Decisions"
          "Contract Impact"
          "Verification Obligations"
          "Migration Posture"
          "Generated View Impact"
          "Accepted Deferrals"
          "Planning Findings"
          "Advisory Notes"
          "Lifecycle Notes" ]

    let parsePlanFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Plan

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Plan artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Plan front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)
                let sourceSpec = tryScalarAt [ "sourceSpec" ] root
                let sourceClarifications = tryScalarAt [ "sourceClarifications" ] root
                let sourceChecklist = tryScalarAt [ "sourceChecklist" ] root

                match version, workId, stage, sourceSpec, sourceClarifications, sourceChecklist, versionDiagnostics with
                | Some schema, Some workId, Some stage, Some sourceSpec, Some sourceClarifications, Some sourceChecklist, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "planned"
                           SourceSpec = sourceSpec
                           SourceClarifications = sourceClarifications
                           SourceChecklist = sourceChecklist
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent
                                 artifact
                                 "Plan front matter is incomplete."
                                 "Add schemaVersion, workId, title, stage: plan, changeTier, status, sourceSpec, sourceClarifications, and sourceChecklist to plan.md."
                                 [] ])

    let planDecisionIdsInLine line =
        Regex.Matches(line, @"\bPD-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanDecisionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planContractReferenceIdsInLine line =
        Regex.Matches(line, @"\bPC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanContractReferenceId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let verificationObligationIdsInLine line =
        Regex.Matches(line, @"\bVO-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createVerificationObligationId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planMigrationNoteIdsInLine line =
        Regex.Matches(line, @"\bPM-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createPlanMigrationNoteId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let generatedViewImpactIdsInLine line =
        Regex.Matches(line, @"\bGV-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createGeneratedViewImpactId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let planSourceIdsInLine line =
        Regex.Matches(line, @"\b(?:FR|US|AC|SB|AMB|CQ|DEC|CHK|CR|PD|PC|VO|PM|GV)-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let parsePlanSourceSnapshots text : PlanSourceSnapshot list =
        sectionLines "Source Snapshot" text
        |> List.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(
                    line,
                    @"^\s*-\s*([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(\S+)(?:\s+sha256:([a-fA-F0-9]{64}))?(?:\s+schemaVersion:(\d+))?",
                    RegexOptions.IgnoreCase)

            if m.Success then
                let schema =
                    if m.Groups.[4].Success then
                        match Int32.TryParse m.Groups.[4].Value with
                        | true, value -> Some value
                        | _ -> None
                    else
                        None

                Some
                    { Label = m.Groups.[1].Value
                      Path = normalizePath m.Groups.[2].Value
                      Digest = if m.Groups.[3].Success then Some(m.Groups.[3].Value.ToLowerInvariant()) else None
                      SchemaVersion = schema
                      SourceLocation = sourceLocation lineNumber }
            else
                None)

    let planDecisionStatus (line: string) =
        let lowered = line.ToLowerInvariant()

        if lowered.Contains("accepteddeferral") || lowered.Contains("accepted deferral") then "acceptedDeferral"
        elif lowered.Contains("stale") || lowered.Contains("needs review") then "stale"
        elif lowered.Contains("incomplete") then "incomplete"
        elif lowered.Contains("advisory") then "advisory"
        elif lowered.Contains("complete") || lowered.Contains("planned") then "complete"
        else "complete"

    let parsePlanDecisions text =
        sectionLines "Plan Decisions" text
        |> List.choose (fun (lineNumber, line) ->
            match planDecisionIdsInLine line |> List.tryHead with
            | Some decisionId ->
                Some
                    { DecisionId = decisionId
                      Title = cleanAfterId decisionId.Value line
                      Status = planDecisionStatus line
                      Text = cleanAfterId decisionId.Value line
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) decisionId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parsePlanContractReferences text =
        sectionLines "Contract Impact" text
        |> List.choose (fun (lineNumber, line) ->
            match planContractReferenceIdsInLine line |> List.tryHead with
            | Some contractId ->
                let text = cleanAfterId contractId.Value line
                let kind =
                    let lowered = text.ToLowerInvariant()
                    if lowered.Contains("command") then "command"
                    elif lowered.Contains("report") then "report"
                    elif lowered.Contains("schema") then "schema"
                    elif lowered.Contains("generated") then "generatedView"
                    else "artifact"

                Some
                    { ContractId = contractId
                      Kind = kind
                      Target = text
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) contractId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseVerificationObligations text =
        sectionLines "Verification Obligations" text
        |> List.choose (fun (lineNumber, line) ->
            match verificationObligationIdsInLine line |> List.tryHead with
            | Some obligationId ->
                let text = cleanAfterId obligationId.Value line
                let lowered = text.ToLowerInvariant()
                let evidenceKind =
                    if lowered.Contains("cli") || lowered.Contains("smoke") then "smoke"
                    elif lowered.Contains("fsi") then "fsi"
                    elif lowered.Contains("semantic") then "semanticTest"
                    elif lowered.Contains("golden") || lowered.Contains("json") then "golden"
                    else "test"

                Some
                    { ObligationId = obligationId
                      Title = text
                      EvidenceKind = evidenceKind
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) obligationId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parsePlanMigrationNotes text =
        sectionLines "Migration Posture" text
        |> List.choose (fun (lineNumber, line) ->
            match planMigrationNoteIdsInLine line |> List.tryHead with
            | Some migrationId ->
                let text = cleanAfterId migrationId.Value line
                let lowered = text.ToLowerInvariant()
                let posture =
                    if lowered.Contains("diagnoseonly") || lowered.Contains("diagnose-only") then "diagnoseOnly"
                    elif lowered.Contains("breaking") then "breaking"
                    elif lowered.Contains("compatible") then "compatible"
                    else "none"

                Some
                    { MigrationId = migrationId
                      Posture = posture
                      Text = text
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) migrationId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseGeneratedViewImpacts text =
        sectionLines "Generated View Impact" text
        |> List.choose (fun (lineNumber, line) ->
            match generatedViewImpactIdsInLine line |> List.tryHead with
            | Some impactId ->
                let text = cleanAfterId impactId.Value line
                let lowered = text.ToLowerInvariant()
                let currency =
                    if lowered.Contains("stale") then "staleDiagnostic"
                    elif lowered.Contains("refresh") then "refresh"
                    else "diagnostic"

                Some
                    { ImpactId = impactId
                      Target = text
                      CurrencyBehavior = currency
                      SourceIds = planSourceIdsInLine line |> List.filter ((<>) impactId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseAcceptedPlanDeferrals text =
        sectionLines "Accepted Deferrals" text
        |> List.choose (fun (lineNumber, line) ->
            let sourceIds = planSourceIdsInLine line

            if List.isEmpty sourceIds then
                None
            else
                Some
                    { Id = sourceIds.Head
                      Text = line.Trim().TrimStart('-', '*').Trim()
                      SourceIds = sourceIds
                      SourceLocation = sourceLocation lineNumber })

    let parsePlanFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Plan

        match parsePlanFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text = (if isNull snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = planStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let snapshots = parsePlanSourceSnapshots text
            let decisions = parsePlanDecisions text
            let contracts = parsePlanContractReferences text
            let obligations = parseVerificationObligations text
            let migrations = parsePlanMigrationNotes text
            let impacts = parseGeneratedViewImpacts text
            let deferrals = parseAcceptedPlanDeferrals text
            let blockingFindings = parseNonEmptySectionLines "Planning Findings" text
            let advisoryNotes = parseNonEmptySectionLines "Advisory Notes" text
            let lifecycleNotes = parseNonEmptySectionLines "Lifecycle Notes" text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: PlanDecisionId) -> id.Value) (decisions |> List.map (fun decision -> decision.DecisionId, decision.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: PlanContractReferenceId) -> id.Value) (contracts |> List.map (fun contract -> contract.ContractId, contract.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: VerificationObligationId) -> id.Value) (obligations |> List.map (fun obligation -> obligation.ObligationId, obligation.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: PlanMigrationNoteId) -> id.Value) (migrations |> List.map (fun migration -> migration.MigrationId, migration.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: GeneratedViewImpactId) -> id.Value) (impacts |> List.map (fun impact -> impact.ImpactId, impact.SourceLocation))
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Plan artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to plan.md before relying on parsed planning facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  SourceSnapshots = snapshots |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                  Decisions = decisions |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  ContractReferences = contracts |> List.sortBy (fun contract -> contract.ContractId.Value)
                  VerificationObligations = obligations |> List.sortBy (fun obligation -> obligation.ObligationId.Value)
                  MigrationNotes = migrations |> List.sortBy (fun migration -> migration.MigrationId.Value)
                  GeneratedViewImpacts = impacts |> List.sortBy (fun impact -> impact.ImpactId.Value)
                  AcceptedDeferrals = deferrals |> List.sortBy (fun deferral -> deferral.Id)
                  BlockingFindings = blockingFindings |> List.sort
                  AdvisoryNotes = advisoryNotes |> List.sort
                  LifecycleNotes = lifecycleNotes
                  StaleDecisionCount = decisions |> List.filter (fun decision -> decision.Status = "stale") |> List.length
                  Diagnostics = diagnostics }

    let parseRequirements (snapshot: FileSnapshot) =
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

    let parseMarkdownRequirementMentions (snapshot: FileSnapshot) =
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

    let parseDecisions (snapshot: FileSnapshot) =
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
        | "stale" -> Stale
        | _ -> Pending

    let taskStatusSourceValue status =
        match status with
        | Pending -> "pending"
        | InProgress -> "in-progress"
        | Done -> "done"
        | Skipped _ -> "skipped"
        | TaskStatus.Stale -> "stale"

    let parseTaskIds values =
        values |> List.choose (Identifiers.createTaskId >> Result.toOption)

    let parseRequirementIds values =
        values |> List.choose (Identifiers.createRequirementId >> Result.toOption)

    let parseDecisionIds values =
        values |> List.choose (Identifiers.createDecisionId >> Result.toOption)

    let parseEvidenceIds values =
        values |> List.choose (Identifiers.createEvidenceId >> Result.toOption)

    let workIdFromTaskPath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then
            parts.[1]
        else
            "unknown-work"

    let parseTaskFrontMatter (artifact: ArtifactRef) (snapshot: FileSnapshot) (root: YamlNode) version =
        let workNode = tryNodeAt [ "work" ] root |> Option.defaultValue root
        let defaultWorkId = workIdFromTaskPath snapshot.Path
        let workIdValue = tryScalarAt [ "id" ] workNode |> Option.defaultValue defaultWorkId
        let workId = Identifiers.createWorkId workIdValue |> Result.toOption |> Option.defaultValue { Value = workIdValue }
        let stage = tryScalarAt [ "stage" ] workNode |> Option.bind (Identifiers.parseStage >> Result.toOption) |> Option.defaultValue LifecycleStage.Tasks

        { SchemaVersion = version
          WorkId = workId
          Title = tryScalarAt [ "title" ] workNode |> Option.defaultValue workId.Value
          Stage = stage
          Status = tryScalarAt [ "status" ] workNode |> Option.defaultValue "tasksReady"
          SourceSpec = tryScalarAt [ "sourceSpec" ] workNode |> Option.defaultValue $"work/{workId.Value}/spec.md"
          SourceClarifications = tryScalarAt [ "sourceClarifications" ] workNode |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
          SourceChecklist = tryScalarAt [ "sourceChecklist" ] workNode |> Option.defaultValue $"work/{workId.Value}/checklist.md"
          SourcePlan = tryScalarAt [ "sourcePlan" ] workNode |> Option.defaultValue $"work/{workId.Value}/plan.md"
          PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] workNode }

    let parseTaskSourceSnapshots root : TaskSourceSnapshot list =
        trySequenceAt [ "sources" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.bind (fun mapping ->
                    match tryScalarAt [ "label" ] mapping, tryScalarAt [ "path" ] mapping with
                    | Some label, Some path ->
                        let schemaVersion =
                            tryScalarAt [ "schemaVersion" ] mapping
                            |> Option.bind (fun value ->
                                match Int32.TryParse value with
                                | true, parsed -> Some parsed
                                | _ -> None)

                        Some
                            ({ Label = label
                               Path = normalizePath path
                               Digest = tryScalarAt [ "digest" ] mapping |> Option.map (fun value -> value.ToLowerInvariant())
                               SchemaVersion = schemaVersion
                               SourceLocation = sourceLocation (index + 1) }
                            : TaskSourceSnapshot)
                    | _ -> None))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue ([] : TaskSourceSnapshot list)

    let parseTaskFindings root =
        trySequenceAt [ "findings" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    let id = tryScalarAt [ "id" ] mapping |> Option.defaultValue (sprintf "TF-%03d" (index + 1))

                    { FindingId = id
                      Severity = tryScalarAt [ "severity" ] mapping |> Option.defaultValue "warning"
                      Text = tryScalarAt [ "text" ] mapping |> Option.defaultValue id
                      SourceIds = scalarList [ "sourceIds" ] mapping
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let taskSchemaDiagnostics artifact (tasks: WorkTask list) =
        let duplicateTasks =
            duplicateScopedDiagnostics artifact (fun (id: TaskId) -> id.Value) (tasks |> List.map (fun task -> task.Id, task.SourceLocation))

        duplicateTasks
        |> List.map (fun diagnostic ->
            Diagnostics.workModelInconsistent
                artifact
                diagnostic.Message
                "Use each task id only once in tasks.yml."
                diagnostic.RelatedIds)

    let parseTaskFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Tasks

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Tasks file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            match version, versionDiagnostics with
            | Some schema, [] ->
                let frontMatter = parseTaskFrontMatter artifact snapshot root schema

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
                                    let status = tryScalarAt [ "status" ] mapping |> Option.map parseTaskStatus |> Option.defaultValue Pending
                                    let skipRationale = tryScalarAt [ "skipRationale" ] mapping
                                    let status =
                                        match status, skipRationale with
                                        | Skipped _, Some rationale -> Skipped rationale
                                        | _ -> status

                                    { Id = id
                                      Title = tryScalarAt [ "title" ] mapping |> Option.defaultValue (Identifiers.taskIdValue id)
                                      Status = status
                                      Owner = tryScalarAt [ "owner" ] mapping |> Option.defaultValue "unassigned"
                                      Dependencies = scalarList [ "dependencies" ] mapping |> parseTaskIds
                                      Requirements = scalarList [ "requirements" ] mapping |> parseRequirementIds
                                      Decisions = scalarList [ "decisions" ] mapping |> parseDecisionIds
                                      SourceIds = scalarList [ "sourceIds" ] mapping |> List.map (fun value -> value.ToUpperInvariant()) |> List.distinct |> List.sort
                                      RequiredSkills = scalarList [ "requiredSkills" ] mapping
                                      RequiredEvidence = scalarList [ "requiredEvidence" ] mapping |> parseEvidenceIds
                                      Source = artifact
                                      SourceLocation = sourceLocation (index + 1) })))
                        |> Seq.choose id
                        |> Seq.toList)
                    |> Option.defaultValue []

                let acceptedDeferrals = scalarList [ "acceptedDeferrals" ] root
                let advisoryNotes = scalarList [ "advisoryNotes" ] root
                let lifecycleNotes = scalarList [ "lifecycleNotes" ] root
                let findings = parseTaskFindings root
                let staleCount = tasks |> List.filter (fun task -> task.Status = TaskStatus.Stale) |> List.length
                let diagnostics = taskSchemaDiagnostics artifact tasks |> Diagnostics.sort

                Ok
                    { FrontMatter = frontMatter
                      SourceSnapshots = parseTaskSourceSnapshots root |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                      Tasks = tasks |> List.sortBy (fun task -> task.Id.Value)
                      AcceptedDeferrals = acceptedDeferrals |> List.sort
                      Findings = findings |> List.sortBy (fun finding -> finding.FindingId)
                      AdvisoryNotes = advisoryNotes |> List.sort
                      LifecycleNotes = lifecycleNotes
                      StaleTaskCount = staleCount
                      Diagnostics = diagnostics }
            | _ -> Error versionDiagnostics

    let tryJsonProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object && element.TryGetProperty(name, &value) then
            Some value
        else
            None

    let jsonString name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Some(value.GetString())
            elif value.ValueKind = JsonValueKind.Null then
                None
            else
                Some(value.ToString()))

    let jsonRequiredString name element =
        jsonString name element |> Option.defaultValue ""

    let jsonInt name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Number then
                match value.TryGetInt32() with
                | true, parsed -> Some parsed
                | _ -> None
            elif value.ValueKind = JsonValueKind.String then
                match Int32.TryParse(value.GetString()) with
                | true, parsed -> Some parsed
                | _ -> None
            else
                None)

    let jsonBool name element =
        tryJsonProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.True then Some true
            elif value.ValueKind = JsonValueKind.False then Some false
            else None)

    let jsonArray name element =
        tryJsonProperty name element
        |> Option.filter (fun value -> value.ValueKind = JsonValueKind.Array)
        |> Option.map (fun value -> value.EnumerateArray() |> Seq.toList)
        |> Option.defaultValue []

    let jsonStringList name element =
        jsonArray name element
        |> List.choose (fun value ->
            if value.ValueKind = JsonValueKind.String then
                Some(value.GetString())
            else
                None)
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> List.sort

    let parseJsonDigest (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.String ->
            let value = element.GetString()
            if String.IsNullOrWhiteSpace value then
                None
            else
                let parts = value.Split([| ':' |], 2)
                if parts.Length = 2 then
                    SchemaVersion.createSourceDigest parts.[0] parts.[1] |> Result.toOption
                else
                    SchemaVersion.createSourceDigest "sha256" value |> Result.toOption
        | JsonValueKind.Object ->
            match jsonString "algorithm" element, jsonString "value" element with
            | Some algorithm, Some value -> SchemaVersion.createSourceDigest algorithm value |> Result.toOption
            | _ -> None
        | _ -> None

    let jsonDigest name element =
        tryJsonProperty name element |> Option.bind parseJsonDigest

    let diagnosticSeverityFromJson value =
        match (if isNull value then "" else value).Trim().ToLowerInvariant() with
        | "error"
        | "blocking" -> Diagnostics.DiagnosticError
        | "warning"
        | "stalesource"
        | "missingdisposition"
        | "malformedsource"
        | "generatedview" -> Diagnostics.DiagnosticWarning
        | _ -> Diagnostics.DiagnosticInfo

    let artifactFromJsonPath path =
        if String.IsNullOrWhiteSpace path then
            None
        else
            ArtifactRef.create (normalizePath path) (ArtifactKind.Other "analysis") ArtifactOwner.Sdd true
            |> Result.toOption

    let parseAnalysisDiagnostic (element: JsonElement) =
        Diagnostics.create
            (jsonRequiredString "id" element)
            (jsonRequiredString "severity" element |> diagnosticSeverityFromJson)
            (jsonString "artifact" element |> Option.orElseWith (fun () -> jsonString "path" element) |> Option.bind artifactFromJsonPath)
            None
            (jsonRequiredString "message" element)
            (jsonRequiredString "correction" element)
            (jsonStringList "relatedIds" element)

    let parseAnalysisSource (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Kind = jsonRequiredString "kind" element
          Digest = jsonDigest "digest" element |> Option.orElseWith (fun () -> jsonDigest "sourceDigest" element)
          SchemaVersion = jsonInt "schemaVersion" element
          SchemaStatus = jsonString "schemaStatus" element }

    let parseAnalysisRelationship (element: JsonElement) =
        { Id = jsonRequiredString "id" element
          SourcePath = normalizePath (jsonRequiredString "sourcePath" element)
          TargetPath = normalizePath (jsonRequiredString "targetPath" element)
          SourceId = jsonString "sourceId" element
          TargetId = jsonString "targetId" element
          Relationship = jsonRequiredString "relationship" element
          State = jsonRequiredString "state" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisFinding (element: JsonElement) =
        { Id = jsonRequiredString "id" element
          Category = jsonRequiredString "category" element
          Severity = jsonRequiredString "severity" element
          State = jsonRequiredString "state" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseAnalysisReadiness (element: JsonElement) =
        { Status = jsonString "status" element |> Option.defaultValue "blocked"
          ReadyCount = jsonInt "readyCount" element |> Option.defaultValue 0
          AdvisoryCount = jsonInt "advisoryCount" element |> Option.defaultValue 0
          WarningCount = jsonInt "warningCount" element |> Option.defaultValue 0
          BlockingCount = jsonInt "blockingCount" element |> Option.defaultValue 0
          StaleSourceCount = jsonInt "staleSourceCount" element |> Option.defaultValue 0
          MissingDispositionCount = jsonInt "missingDispositionCount" element |> Option.defaultValue 0
          MalformedSourceCount = jsonInt "malformedSourceCount" element |> Option.defaultValue 0
          GeneratedViewFindingCount = jsonInt "generatedViewFindingCount" element |> Option.defaultValue 0
          AcceptedDeferralCount = jsonInt "acceptedDeferralCount" element |> Option.defaultValue 0 }

    let parseAnalysisGeneratedView (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Kind = jsonRequiredString "kind" element
          Currency = jsonRequiredString "currency" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisBoundaryFact (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Relationship = jsonRequiredString "relationship" element
          RequiredBySdd = jsonBool "requiredBySdd" element |> Option.defaultValue false
          State = jsonRequiredString "state" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisNextAction (element: JsonElement) =
        { ActionId = jsonRequiredString "actionId" element
          Command = jsonString "command" element
          Reason = jsonRequiredString "reason" element }

    let parseAnalysisView (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.GeneratedView

        try
            use document = JsonDocument.Parse snapshot.Text
            let root = document.RootElement
            let rawVersion = jsonInt "schemaVersion" root |> Option.map string
            let compatibility = SchemaVersion.classifyRaw rawVersion

            match compatibility.Version, compatibility.Status with
            | Some schema, SchemaCompatibilityStatus.Current
            | Some schema, SchemaCompatibilityStatus.Deprecated ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let readiness =
                        tryJsonProperty "readiness" root
                        |> Option.map parseAnalysisReadiness
                        |> Option.defaultValue
                            { Status = jsonString "status" root |> Option.defaultValue "blocked"
                              ReadyCount = 0
                              AdvisoryCount = 0
                              WarningCount = 0
                              BlockingCount = 0
                              StaleSourceCount = 0
                              MissingDispositionCount = 0
                              MalformedSourceCount = 0
                              GeneratedViewFindingCount = 0
                              AcceptedDeferralCount = 0 }

                    let diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue readiness.Status
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          SourceRelationships =
                            jsonArray "sourceRelationships" root
                            |> List.map parseAnalysisRelationship
                            |> List.sortBy (fun relationship -> relationship.Id)
                          Readiness = readiness
                          Findings = jsonArray "findings" root |> List.map parseAnalysisFinding |> List.sortBy (fun finding -> finding.Id)
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          OptionalBoundaryFacts =
                            jsonArray "optionalBoundaryFacts" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics = diagnostics
                          NextAction = tryJsonProperty "nextAction" root |> Option.map parseAnalysisNextAction }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Analysis view identity fields are malformed."
                              "Regenerate analysis.json with a valid workId and stage: analyze."
                              [ workIdText; stageText ] ]
            | _, SchemaCompatibilityStatus.Malformed ->
                Error [ Diagnostics.malformedSchemaVersion artifact "Analysis view is missing or has malformed schemaVersion." ]
            | _, SchemaCompatibilityStatus.Unsupported ->
                Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
            | _, SchemaCompatibilityStatus.Future ->
                Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
        with ex ->
            Error [ Diagnostics.workModelInconsistent artifact $"Analysis view JSON is malformed: {ex.Message}" "Regenerate readiness/<id>/analysis.json with valid JSON." [ snapshot.Path ] ]

    let evidenceDispositionStateFromString (value: string) =
        match (if isNull value then "" else value).Trim().ToLowerInvariant() with
        | "supported" -> EvidenceSupported
        | "deferred" -> EvidenceDeferred
        | "missing" -> EvidenceMissingDisposition
        | "stale" -> EvidenceStale
        | "synthetic" -> EvidenceSyntheticDisposition
        | "invalid" -> EvidenceInvalid
        | "advisory" -> EvidenceAdvisory
        | _ -> EvidenceBlocking

    let requiredTestDispositionStateFromString (value: string) =
        match (if isNull value then "" else value).Trim().ToLowerInvariant() with
        | "satisfied" -> TestSatisfied
        | "deferred" -> TestDeferred
        | "missing" -> TestMissingDisposition
        | "stale" -> TestStale
        | "synthetic" -> TestSyntheticDisposition
        | "invalid" -> TestInvalid
        | "advisory" -> TestAdvisory
        | _ -> TestBlocking

    let skillVisibilityStateFromString (value: string) =
        match (if isNull value then "" else value).Trim().ToLowerInvariant() with
        | "visible" -> SkillVisible
        | _ -> SkillMissing

    let private taskIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createTaskId >> Result.toOption)

    let private evidenceIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createEvidenceId >> Result.toOption)

    let private requirementIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createRequirementId >> Result.toOption)

    let parseVerificationEvidenceDisposition (element: JsonElement) : EvidenceDisposition =
        { DispositionId = jsonRequiredString "id" element
          ObligationId = jsonRequiredString "obligationId" element
          State = jsonRequiredString "state" element |> evidenceDispositionStateFromString
          EvidenceIds = evidenceIdsFromJson "evidenceIds" element
          AffectedTaskIds = taskIdsFromJson "affectedTaskIds" element
          AffectedSourceIds = jsonStringList "affectedSourceIds" element
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationTestDisposition (element: JsonElement) : RequiredTestDisposition =
        { DispositionId = jsonRequiredString "id" element
          ObligationId = jsonRequiredString "obligationId" element
          State = jsonRequiredString "state" element |> requiredTestDispositionStateFromString
          EvidenceIds = evidenceIdsFromJson "evidenceIds" element
          AffectedTaskIds = taskIdsFromJson "affectedTaskIds" element
          AffectedRequirementIds = requirementIdsFromJson "affectedRequirementIds" element
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationSkillVisibility (element: JsonElement) : SkillVisibilityFact =
        { Skill = jsonRequiredString "skill" element
          RequiringTaskIds = taskIdsFromJson "requiringTaskIds" element
          Visibility = jsonRequiredString "visibility" element |> skillVisibilityStateFromString
          SourceArtifactPath = normalizePath (jsonRequiredString "sourceArtifactPath" element)
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationFinding (element: JsonElement) : VerificationFinding =
        { Id = jsonRequiredString "id" element
          Severity = jsonRequiredString "severity" element
          Category = jsonRequiredString "category" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationLifecycleReadiness (element: JsonElement) : VerificationLifecycleReadiness =
        { Stages =
            jsonArray "stages" element
            |> List.map (fun stage ->
                { VerificationStageReadiness.Stage = jsonRequiredString "stage" stage
                  Status = jsonRequiredString "status" stage })
            |> List.sortBy (fun stage -> stage.Stage)
          Status = jsonString "status" element |> Option.defaultValue "blocked" }

    let parseVerificationTaskGraph (element: JsonElement) : VerificationTaskGraphReadiness =
        { TaskCount = jsonInt "taskCount" element |> Option.defaultValue 0
          DependencyCount = jsonInt "dependencyCount" element |> Option.defaultValue 0
          DependenciesValid = jsonBool "dependenciesValid" element |> Option.defaultValue false
          StatusesValid = jsonBool "statusesValid" element |> Option.defaultValue false
          FindingIds = jsonStringList "findingIds" element }

    let parseVerificationView (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.GeneratedView

        try
            use document = JsonDocument.Parse snapshot.Text
            let root = document.RootElement
            let rawVersion = jsonInt "schemaVersion" root |> Option.map string
            let compatibility = SchemaVersion.classifyRaw rawVersion

            match compatibility.Version, compatibility.Status with
            | Some schema, SchemaCompatibilityStatus.Current
            | Some schema, SchemaCompatibilityStatus.Deprecated ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let lifecycleReadiness =
                        tryJsonProperty "lifecycleReadiness" root
                        |> Option.map parseVerificationLifecycleReadiness
                        |> Option.defaultValue { Stages = []; Status = "blocked" }

                    let taskGraph =
                        tryJsonProperty "taskGraph" root
                        |> Option.map parseVerificationTaskGraph
                        |> Option.defaultValue
                            { TaskCount = 0
                              DependencyCount = 0
                              DependenciesValid = false
                              StatusesValid = false
                              FindingIds = [] }

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue "needsVerificationCorrection"
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          LifecycleReadiness = lifecycleReadiness
                          TaskGraph = taskGraph
                          EvidenceDispositions =
                            jsonArray "evidenceDispositions" root
                            |> List.map parseVerificationEvidenceDisposition
                            |> List.sortBy (fun disposition -> disposition.DispositionId)
                          TestDispositions =
                            jsonArray "testDispositions" root
                            |> List.map parseVerificationTestDisposition
                            |> List.sortBy (fun disposition -> disposition.DispositionId)
                          SkillVisibility =
                            jsonArray "skillVisibility" root
                            |> List.map parseVerificationSkillVisibility
                            |> List.sortBy (fun fact -> fact.Skill)
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          Findings = jsonArray "findings" root |> List.map parseVerificationFinding |> List.sortBy (fun finding -> finding.Id)
                          OptionalBoundaryFacts =
                            jsonArray "governanceCompatibility" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort
                          Readiness = jsonString "readiness" root |> Option.defaultValue "needsVerificationCorrection" }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Verification view identity fields are malformed."
                              "Regenerate verify.json with a valid workId and stage: verify."
                              [ workIdText; stageText ] ]
            | _, SchemaCompatibilityStatus.Malformed ->
                Error [ Diagnostics.malformedSchemaVersion artifact "Verification view is missing or has malformed schemaVersion." ]
            | _, SchemaCompatibilityStatus.Unsupported ->
                Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
            | _, SchemaCompatibilityStatus.Future ->
                Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
        with ex ->
            Error [ Diagnostics.workModelInconsistent artifact $"Verification view JSON is malformed: {ex.Message}" "Regenerate readiness/<id>/verify.json with valid JSON." [ snapshot.Path ] ]

    let parseShipFinding (element: JsonElement) : ShipReadinessFinding =
        { Id = jsonRequiredString "id" element
          Severity = jsonRequiredString "severity" element
          Category = jsonRequiredString "category" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseShipLifecycleStage (element: JsonElement) : ShipLifecycleStageReadiness =
        { Stage = jsonRequiredString "stage" element
          Status = jsonRequiredString "status" element }

    let parseShipVerificationReadiness (element: JsonElement) : ShipVerificationReadinessSummary =
        { Status = jsonString "status" element |> Option.defaultValue "needsVerificationCorrection"
          BlockingFindingIds = jsonStringList "blockingFindingIds" element
          EvidenceSupportedCount = jsonInt "evidenceSupportedCount" element |> Option.defaultValue 0
          EvidenceDeferredCount = jsonInt "evidenceDeferredCount" element |> Option.defaultValue 0
          EvidenceMissingCount = jsonInt "evidenceMissingCount" element |> Option.defaultValue 0
          EvidenceStaleCount = jsonInt "evidenceStaleCount" element |> Option.defaultValue 0
          EvidenceSyntheticCount = jsonInt "evidenceSyntheticCount" element |> Option.defaultValue 0
          EvidenceInvalidCount = jsonInt "evidenceInvalidCount" element |> Option.defaultValue 0 }

    let parseShipView (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.GeneratedView

        try
            use document = JsonDocument.Parse snapshot.Text
            let root = document.RootElement
            let rawVersion = jsonInt "schemaVersion" root |> Option.map string
            let compatibility = SchemaVersion.classifyRaw rawVersion

            match compatibility.Version, compatibility.Status with
            | Some schema, SchemaCompatibilityStatus.Current
            | Some schema, SchemaCompatibilityStatus.Deprecated ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let lifecycleReadiness =
                        tryJsonProperty "lifecycleReadiness" root
                        |> Option.map (fun element -> jsonArray "stages" element |> List.map parseShipLifecycleStage)
                        |> Option.defaultValue []
                        |> List.sortBy (fun stage -> stage.Stage)

                    let verificationReadiness =
                        tryJsonProperty "verificationReadiness" root
                        |> Option.map parseShipVerificationReadiness
                        |> Option.defaultValue
                            { Status = "needsVerificationCorrection"
                              BlockingFindingIds = []
                              EvidenceSupportedCount = 0
                              EvidenceDeferredCount = 0
                              EvidenceMissingCount = 0
                              EvidenceStaleCount = 0
                              EvidenceSyntheticCount = 0
                              EvidenceInvalidCount = 0 }

                    let disposition =
                        tryJsonProperty "disposition" root
                        |> Option.bind (fun element -> jsonString "state" element)
                        |> Option.defaultValue "blocked"

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue "needsShipCorrection"
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          LifecycleReadiness = lifecycleReadiness
                          VerificationReadiness = verificationReadiness
                          Disposition = disposition
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          Findings = jsonArray "findings" root |> List.map parseShipFinding |> List.sortBy (fun finding -> finding.Id)
                          OptionalBoundaryFacts =
                            jsonArray "governanceCompatibility" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort
                          Readiness = jsonString "readiness" root |> Option.defaultValue "needsShipCorrection" }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Ship view identity fields are malformed."
                              "Regenerate ship.json with a valid workId and stage: ship."
                              [ workIdText; stageText ] ]
            | _, SchemaCompatibilityStatus.Malformed ->
                Error [ Diagnostics.malformedSchemaVersion artifact "Ship view is missing or has malformed schemaVersion." ]
            | _, SchemaCompatibilityStatus.Unsupported ->
                Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
            | _, SchemaCompatibilityStatus.Future ->
                Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
        with ex ->
            Error [ Diagnostics.workModelInconsistent artifact $"Ship view JSON is malformed: {ex.Message}" "Regenerate readiness/<id>/ship.json with valid JSON." [ snapshot.Path ] ]

    let parseTasks (snapshot: FileSnapshot) =
        parseTaskFacts snapshot |> Result.map (fun facts -> facts.Tasks)

    let parseEvidenceKind (value: string) =
        match if isNull value then "" else value.Trim().ToLowerInvariant() with
        | "implementation" -> Implementation
        | "verification" -> Verification
        | "review" -> Review
        | "generated-view" -> GeneratedViewEvidence
        | "generatedview" -> GeneratedViewEvidence
        | "synthetic" -> Synthetic
        | "deferral" -> Deferral
        | "note" -> Note
        | "missing" -> Missing
        | _ -> Verification

    let parseArtifactRefs values =
        values
        |> List.map (fun path -> artifact path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false)

    let parseEvidenceSourceSnapshots root =
        trySequenceAt [ "sourceSnapshots" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    { Label = tryScalarAt [ "label" ] mapping |> Option.defaultValue ""
                      Path = tryScalarAt [ "path" ] mapping |> Option.defaultValue ""
                      Digest = tryScalarAt [ "digest" ] mapping
                      SchemaVersion =
                        tryScalarAt [ "schemaVersion" ] mapping
                        |> Option.bind (fun value ->
                            match Int32.TryParse value with
                            | true, parsed -> Some parsed
                            | _ -> None)
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let parseEvidenceSourceRefs (mapping: YamlMappingNode) =
        trySequenceAt [ "sourceRefs" ] mapping
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun source ->
                    { ReferenceId = tryScalarAt [ "id" ] source
                      Kind = tryScalarAt [ "kind" ] source |> Option.defaultValue "artifact"
                      Path = tryScalarAt [ "path" ] source
                      Uri = tryScalarAt [ "uri" ] source
                      Digest = tryScalarAt [ "digest" ] source
                      RelatedSourceId = tryScalarAt [ "relatedSourceId" ] source
                      Result = tryScalarAt [ "result" ] source
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let parseSyntheticDisclosure (mapping: YamlMappingNode) =
        match tryScalarAt [ "syntheticDisclosure"; "standsInFor" ] mapping, tryScalarAt [ "syntheticDisclosure"; "reason" ] mapping with
        | Some standsInFor, Some reason when not (String.IsNullOrWhiteSpace standsInFor) && not (String.IsNullOrWhiteSpace reason) ->
            Some { StandsInFor = standsInFor; Reason = reason }
        | _ -> None

    let parseAcceptanceScenarioIds values =
        values |> List.choose (Identifiers.createAcceptanceScenarioId >> Result.toOption)

    let parseChecklistResultIds values =
        values |> List.choose (Identifiers.createChecklistResultId >> Result.toOption)

    let parsePlanDecisionIds values =
        values |> List.choose (Identifiers.createPlanDecisionId >> Result.toOption)

    let workIdFromEvidencePath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then parts.[1] else "unknown-work"

    let parseEvidenceArtifact (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Evidence

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Evidence file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root
            let workIdValue = tryScalarAt [ "workId" ] root |> Option.defaultValue (workIdFromEvidencePath snapshot.Path)
            let workId = Identifiers.createWorkId workIdValue
            let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption) |> Option.defaultValue LifecycleStage.Evidence

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
                                  AcceptanceScenarioRefs = scalarList [ "acceptanceScenarioRefs" ] mapping |> parseAcceptanceScenarioIds
                                  ClarificationDecisionRefs = scalarList [ "clarificationDecisionRefs" ] mapping |> parseDecisionIds
                                  ChecklistResultRefs = scalarList [ "checklistResultRefs" ] mapping |> parseChecklistResultIds
                                  PlanDecisionRefs = scalarList [ "planDecisionRefs" ] mapping |> parsePlanDecisionIds
                                  ObligationRefs = scalarList [ "obligationRefs" ] mapping |> List.distinct |> List.sort
                                  ArtifactRefs = scalarList [ "artifacts" ] mapping |> parseArtifactRefs
                                  SourceRefs = parseEvidenceSourceRefs mapping
                                  Result = tryScalarAt [ "result" ] mapping |> Option.defaultValue "pending"
                                  Synthetic = boolAt [ "synthetic" ] mapping false
                                  SyntheticDisclosure = parseSyntheticDisclosure mapping
                                  Rationale = tryScalarAt [ "rationale" ] mapping
                                  Owner = tryScalarAt [ "owner" ] mapping
                                  Scope = tryScalarAt [ "scope" ] mapping
                                  LaterLifecycleVisibility = tryScalarAt [ "laterLifecycleVisibility" ] mapping
                                  Notes = scalarList [ "notes" ] mapping
                                  Source = artifact
                                  SourceLocation = sourceLocation (index + 1) })))
                    |> Seq.choose id
                    |> Seq.toList)
                |> Option.defaultValue []

            let duplicateDiagnostics =
                evidence
                |> List.groupBy (fun declaration -> declaration.Id.Value)
                |> List.choose (fun (id, declarations) ->
                    if List.length declarations > 1 then
                        Some(Diagnostics.duplicateIdentifier artifact id (declarations |> List.choose (fun declaration -> declaration.SourceLocation)))
                    else
                        None)

            let artifactDiagnostics =
                [ if stage <> LifecycleStage.Evidence then
                      Diagnostics.workModelInconsistent artifact $"Evidence stage '{Identifiers.stageValue stage}' is not 'evidence'." "Set stage: evidence before rerunning." [ Identifiers.stageValue stage ] ]

            match version, workId, versionDiagnostics with
            | Some schema, Ok workId, [] ->
                Ok
                    { SchemaVersion = schema
                      WorkId = workId
                      Stage = stage
                      Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                      SourceSpec = tryScalarAt [ "sourceSpec" ] root |> Option.defaultValue $"work/{workId.Value}/spec.md"
                      SourceClarifications = tryScalarAt [ "sourceClarifications" ] root |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
                      SourceChecklist = tryScalarAt [ "sourceChecklist" ] root |> Option.defaultValue $"work/{workId.Value}/checklist.md"
                      SourcePlan = tryScalarAt [ "sourcePlan" ] root |> Option.defaultValue $"work/{workId.Value}/plan.md"
                      SourceTasks = tryScalarAt [ "sourceTasks" ] root |> Option.defaultValue $"work/{workId.Value}/tasks.yml"
                      SourceAnalysis = tryScalarAt [ "sourceAnalysis" ] root |> Option.defaultValue $"readiness/{workId.Value}/analysis.json"
                      SourceSnapshots = parseEvidenceSourceSnapshots root
                      Evidence = evidence |> List.sortBy (fun declaration -> declaration.Id.Value)
                      LifecycleNotes = scalarList [ "lifecycleNotes" ] root
                      Diagnostics = duplicateDiagnostics @ artifactDiagnostics |> Diagnostics.sort }
            | _ ->
                let workIdDiagnostics =
                    match workId with
                    | Error message -> [ Diagnostics.workModelInconsistent artifact message "Use a valid work id in evidence.yml." [ workIdValue ] ]
                    | Ok _ -> []

                Error
                    (versionDiagnostics
                     @ duplicateDiagnostics
                     @ workIdDiagnostics)

    let parseEvidence (snapshot: FileSnapshot) =
        parseEvidenceArtifact snapshot |> Result.map (fun artifact -> artifact.Evidence)

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
