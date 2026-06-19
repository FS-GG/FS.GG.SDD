namespace FS.GG.SDD.Artifacts

open System.Text.RegularExpressions

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

    let createWorkId (value: string) =
        let value = if isNull value then "" else value.Trim()

        if Regex.IsMatch(value, @"^(?:\d+[a-z0-9]*|[a-z][a-z0-9]*)(?:-[a-z0-9]+)*$") then
            Ok ({ Value = value } : WorkId)
        else
            Error "Work id must be lowercase kebab-case and start with a digit sequence or approved slug."

    let workIdValue (workId: WorkId) = workId.Value

    let parseStage (value: string) =
        match if isNull value then "" else value.Trim().ToLowerInvariant() with
        | "charter" -> Ok Charter
        | "specify" -> Ok Specify
        | "clarify" -> Ok Clarify
        | "checklist" -> Ok Checklist
        | "plan" -> Ok Plan
        | "tasks" -> Ok Tasks
        | "analyze" -> Ok Analyze
        | "implement" -> Ok Implement
        | "evidence" -> Ok Evidence
        | "verify" -> Ok Verify
        | "ship" -> Ok Ship
        | other -> Error $"Unknown lifecycle stage '{other}'."

    let stageValue stage =
        match stage with
        | Charter -> "charter"
        | Specify -> "specify"
        | Clarify -> "clarify"
        | Checklist -> "checklist"
        | Plan -> "plan"
        | Tasks -> "tasks"
        | Analyze -> "analyze"
        | Implement -> "implement"
        | Evidence -> "evidence"
        | Verify -> "verify"
        | Ship -> "ship"

    let allStages () =
        [ Charter
          Specify
          Clarify
          Checklist
          Plan
          Tasks
          Analyze
          Implement
          Evidence
          Verify
          Ship ]

    let createScopedId (label: string) (pattern: string) (value: string) =
        let value = if isNull value then "" else value.Trim()

        if Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase) then
            Ok value
        else
            Error $"{label} must match {pattern}."

    let createRequirementId (value: string) =
        createScopedId "Requirement id" @"^FR-\d{3,}$" value
        |> Result.map (fun value -> { RequirementId.Value = value.ToUpperInvariant() })

    let createUserStoryId (value: string) =
        createScopedId "User story id" @"^US-\d{3,}$" value
        |> Result.map (fun value -> { UserStoryId.Value = value.ToUpperInvariant() })

    let createAcceptanceScenarioId (value: string) =
        createScopedId "Acceptance scenario id" @"^AC-\d{3,}$" value
        |> Result.map (fun value -> { AcceptanceScenarioId.Value = value.ToUpperInvariant() })

    let createScopeBoundaryId (value: string) =
        createScopedId "Scope boundary id" @"^SB-\d{3,}$" value
        |> Result.map (fun value -> { ScopeBoundaryId.Value = value.ToUpperInvariant() })

    let createAmbiguityId (value: string) =
        createScopedId "Ambiguity id" @"^AMB-\d{3,}$" value
        |> Result.map (fun value -> { AmbiguityId.Value = value.ToUpperInvariant() })

    let createClarificationQuestionId (value: string) =
        createScopedId "Clarification question id" @"^CQ-\d{3,}$" value
        |> Result.map (fun value -> { ClarificationQuestionId.Value = value.ToUpperInvariant() })

    let createDecisionId (value: string) =
        createScopedId "Decision id" @"^DEC-\d{3,}$" value
        |> Result.map (fun value -> { DecisionId.Value = value.ToUpperInvariant() })

    let createTaskId (value: string) =
        createScopedId "Task id" @"^T\d{3,}$" value
        |> Result.map (fun value -> { TaskId.Value = value.ToUpperInvariant() })

    let createEvidenceId (value: string) =
        createScopedId "Evidence id" @"^EV\d{3,}$" value
        |> Result.map (fun value -> { EvidenceId.Value = value.ToUpperInvariant() })

    let requirementIdValue (id: RequirementId) = id.Value
    let userStoryIdValue (id: UserStoryId) = id.Value
    let acceptanceScenarioIdValue (id: AcceptanceScenarioId) = id.Value
    let scopeBoundaryIdValue (id: ScopeBoundaryId) = id.Value
    let ambiguityIdValue (id: AmbiguityId) = id.Value
    let clarificationQuestionIdValue (id: ClarificationQuestionId) = id.Value
    let decisionIdValue (id: DecisionId) = id.Value
    let taskIdValue (id: TaskId) = id.Value
    let evidenceIdValue (id: EvidenceId) = id.Value
