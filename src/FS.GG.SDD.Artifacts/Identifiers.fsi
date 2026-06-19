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
    type DecisionId = { Value: string }
    type TaskId = { Value: string }
    type EvidenceId = { Value: string }

    val createWorkId: value: string -> Result<WorkId, string>
    val workIdValue: workId: WorkId -> string
    val parseStage: value: string -> Result<LifecycleStage, string>
    val stageValue: stage: LifecycleStage -> string
    val allStages: unit -> LifecycleStage list
    val createRequirementId: value: string -> Result<RequirementId, string>
    val createDecisionId: value: string -> Result<DecisionId, string>
    val createTaskId: value: string -> Result<TaskId, string>
    val createEvidenceId: value: string -> Result<EvidenceId, string>
    val requirementIdValue: id: RequirementId -> string
    val decisionIdValue: id: DecisionId -> string
    val taskIdValue: id: TaskId -> string
    val evidenceIdValue: id: EvidenceId -> string
