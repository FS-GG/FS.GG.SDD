namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val standardArtifactContracts: unit -> LifecycleArtifactContract list
    val parseProjectConfig: snapshot: FileSnapshot -> Result<ProjectLifecycleConfig, Diagnostic list>
    val parseSddLifecyclePolicy: snapshot: FileSnapshot -> Result<SddLifecyclePolicy, Diagnostic list>
    val parseAgentGuidanceConfig: snapshot: FileSnapshot -> Result<AgentGuidanceConfig, Diagnostic list>
    val parseWorkItemMetadata: snapshot: FileSnapshot -> Result<WorkItemMetadata, Diagnostic list>
    val specificationStandardSections: unit -> string list
    val parseSpecificationFacts: snapshot: FileSnapshot -> Result<SpecificationFacts, Diagnostic list>
    val clarificationStandardSections: unit -> string list
    val parseClarificationFacts: snapshot: FileSnapshot -> Result<ClarificationFacts, Diagnostic list>
    val parseRequirements: snapshot: FileSnapshot -> Requirement list
    val parseDecisions: snapshot: FileSnapshot -> Decision list
    val parseTasks: snapshot: FileSnapshot -> Result<WorkTask list, Diagnostic list>
    val parseEvidence: snapshot: FileSnapshot -> Result<EvidenceDeclaration list, Diagnostic list>
    val loadWorkItemFromSnapshots: snapshots: FileSnapshot list -> workId: string -> ParsedWorkItem
