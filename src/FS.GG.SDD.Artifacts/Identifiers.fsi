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
    val createTaskId: value: string -> Result<TaskId, string>
    val createEvidenceId: value: string -> Result<EvidenceId, string>
    val requirementIdValue: id: RequirementId -> string
    val userStoryIdValue: id: UserStoryId -> string
    val acceptanceScenarioIdValue: id: AcceptanceScenarioId -> string
    val scopeBoundaryIdValue: id: ScopeBoundaryId -> string
    val ambiguityIdValue: id: AmbiguityId -> string
    val clarificationQuestionIdValue: id: ClarificationQuestionId -> string
    val decisionIdValue: id: DecisionId -> string
    val taskIdValue: id: TaskId -> string
    val evidenceIdValue: id: EvidenceId -> string
