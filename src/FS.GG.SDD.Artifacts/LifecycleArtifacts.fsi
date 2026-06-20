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

    val standardArtifactContracts: unit -> LifecycleArtifactContract list
    val parseProjectConfig: snapshot: FileSnapshot -> Result<ProjectLifecycleConfig, Diagnostic list>
    val parseSddLifecyclePolicy: snapshot: FileSnapshot -> Result<SddLifecyclePolicy, Diagnostic list>
    val parseAgentGuidanceConfig: snapshot: FileSnapshot -> Result<AgentGuidanceConfig, Diagnostic list>
    val parseWorkItemMetadata: snapshot: FileSnapshot -> Result<WorkItemMetadata, Diagnostic list>
    val specificationStandardSections: unit -> string list
    val parseSpecificationFacts: snapshot: FileSnapshot -> Result<SpecificationFacts, Diagnostic list>
    val clarificationStandardSections: unit -> string list
    val parseClarificationFacts: snapshot: FileSnapshot -> Result<ClarificationFacts, Diagnostic list>
    val checklistStandardSections: unit -> string list
    val parseChecklistFacts: snapshot: FileSnapshot -> Result<ChecklistFacts, Diagnostic list>
    val planStandardSections: unit -> string list
    val parsePlanFacts: snapshot: FileSnapshot -> Result<PlanFacts, Diagnostic list>
    val parseTaskFacts: snapshot: FileSnapshot -> Result<TaskFacts, Diagnostic list>
    val parseAnalysisView: snapshot: FileSnapshot -> Result<AnalysisView, Diagnostic list>
    val parseVerificationView: snapshot: FileSnapshot -> Result<VerificationView, Diagnostic list>
    val parseShipView: snapshot: FileSnapshot -> Result<ShipView, Diagnostic list>
    val parseEvidenceArtifact: snapshot: FileSnapshot -> Result<EvidenceArtifact, Diagnostic list>
    val parseRequirements: snapshot: FileSnapshot -> Requirement list
    val parseDecisions: snapshot: FileSnapshot -> Decision list
    val parseTasks: snapshot: FileSnapshot -> Result<WorkTask list, Diagnostic list>
    val parseEvidence: snapshot: FileSnapshot -> Result<EvidenceDeclaration list, Diagnostic list>
    val loadWorkItemFromSnapshots: snapshots: FileSnapshot list -> workId: string -> ParsedWorkItem
