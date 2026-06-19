namespace FS.GG.SDD.Artifacts

module Identifiers =
    type WorkId = { Value: string }

    type LifecycleStage =
        | Charter
        | Specify
        | Clarify
        | Checklist
        | Plan
        | Tasks
        | Analyze
        | Implement
        | Evidence
        | Verify
        | Ship

    type RequirementId = { Value: string }
    type UserStoryId = { Value: string }
    type AcceptanceScenarioId = { Value: string }
    type ScopeBoundaryId = { Value: string }
    type AmbiguityId = { Value: string }
    type ClarificationQuestionId = { Value: string }
    type DecisionId = { Value: string }
    type ChecklistItemId = { Value: string }
    type ChecklistResultId = { Value: string }
    type PlanDecisionId = { Value: string }
    type PlanContractReferenceId = { Value: string }
    type VerificationObligationId = { Value: string }
    type PlanMigrationNoteId = { Value: string }
    type GeneratedViewImpactId = { Value: string }
    type TaskId = { Value: string }
    type EvidenceId = { Value: string }

    val createWorkId: value: string -> Result<WorkId, string>
    val workIdValue: workId: WorkId -> string
    val parseStage: value: string -> Result<LifecycleStage, string>
    val stageValue: stage: LifecycleStage -> string
    val allStages: unit -> LifecycleStage list
    val createRequirementId: value: string -> Result<RequirementId, string>
    val createUserStoryId: value: string -> Result<UserStoryId, string>
    val createAcceptanceScenarioId: value: string -> Result<AcceptanceScenarioId, string>
    val createScopeBoundaryId: value: string -> Result<ScopeBoundaryId, string>
    val createAmbiguityId: value: string -> Result<AmbiguityId, string>
    val createClarificationQuestionId: value: string -> Result<ClarificationQuestionId, string>
    val createDecisionId: value: string -> Result<DecisionId, string>
    val createChecklistItemId: value: string -> Result<ChecklistItemId, string>
    val createChecklistResultId: value: string -> Result<ChecklistResultId, string>
    val createPlanDecisionId: value: string -> Result<PlanDecisionId, string>
    val createPlanContractReferenceId: value: string -> Result<PlanContractReferenceId, string>
    val createVerificationObligationId: value: string -> Result<VerificationObligationId, string>
    val createPlanMigrationNoteId: value: string -> Result<PlanMigrationNoteId, string>
    val createGeneratedViewImpactId: value: string -> Result<GeneratedViewImpactId, string>
    val createTaskId: value: string -> Result<TaskId, string>
    val createEvidenceId: value: string -> Result<EvidenceId, string>
    val requirementIdValue: id: RequirementId -> string
    val userStoryIdValue: id: UserStoryId -> string
    val acceptanceScenarioIdValue: id: AcceptanceScenarioId -> string
    val scopeBoundaryIdValue: id: ScopeBoundaryId -> string
    val ambiguityIdValue: id: AmbiguityId -> string
    val clarificationQuestionIdValue: id: ClarificationQuestionId -> string
    val decisionIdValue: id: DecisionId -> string
    val checklistItemIdValue: id: ChecklistItemId -> string
    val checklistResultIdValue: id: ChecklistResultId -> string
    val planDecisionIdValue: id: PlanDecisionId -> string
    val planContractReferenceIdValue: id: PlanContractReferenceId -> string
    val verificationObligationIdValue: id: VerificationObligationId -> string
    val planMigrationNoteIdValue: id: PlanMigrationNoteId -> string
    val generatedViewImpactIdValue: id: GeneratedViewImpactId -> string
    val taskIdValue: id: TaskId -> string
    val evidenceIdValue: id: EvidenceId -> string
