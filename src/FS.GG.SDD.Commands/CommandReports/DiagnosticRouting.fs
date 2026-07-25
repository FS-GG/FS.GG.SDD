namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes

/// Maps diagnostic families to the lifecycle command that can repair them.
/// Diagnostic construction stays separate from this next-action policy.
module internal DiagnosticRouting =
    let private ids (diagnostics: Diagnostic list) =
        diagnostics |> List.map _.Id |> Set.ofList

    let private containsAny candidates values =
        candidates |> List.exists (fun id -> Set.contains id values)

    let planCorrection diagnostics =
        let values = ids diagnostics

        if
            containsAny
                [ "missingSpecificationPrerequisite"
                  "malformedSpecificationFacts"
                  "specificationIdentityMismatch" ]
                values
        then
            Some Specify
        elif
            containsAny
                [ "missingClarificationPrerequisite"
                  "malformedClarificationFrontMatter"
                  "clarificationIdentityMismatch" ]
                values
        then
            Some Clarify
        elif
            containsAny
                [ "missingChecklistPrerequisite"
                  "failedChecklistPrerequisite"
                  "checklistIdentityMismatch"
                  "malformedChecklistFrontMatter"
                  "missingChecklistBackReference"
                  "duplicateChecklistId"
                  "unknownChecklistSourceReference" ]
                values
        then
            Some Checklist
        elif
            containsAny
                [ "planIdentityMismatch"
                  "malformedPlanFrontMatter"
                  "duplicatePlanId"
                  "unknownPlanSourceReference"
                  "stalePlanDecision" ]
                values
        then
            Some Plan
        else
            None

    let tasksCorrection diagnostics =
        let values = ids diagnostics

        if
            containsAny
                [ "missingSpecificationPrerequisite"
                  "malformedSpecificationFacts"
                  "specificationIdentityMismatch" ]
                values
        then
            Some Specify
        elif
            containsAny
                [ "missingClarificationPrerequisite"
                  "malformedClarificationFrontMatter"
                  "clarificationIdentityMismatch" ]
                values
        then
            Some Clarify
        elif
            containsAny
                [ "missingChecklistPrerequisite"
                  "failedChecklistPrerequisite"
                  "checklistIdentityMismatch"
                  "malformedChecklistFrontMatter"
                  "missingChecklistBackReference"
                  "duplicateChecklistId"
                  "unknownChecklistSourceReference" ]
                values
        then
            Some Checklist
        elif
            containsAny
                [ "missingPlanPrerequisite"
                  "failedPlanPrerequisite"
                  "planIdentityMismatch"
                  "malformedPlanFrontMatter"
                  "duplicatePlanId"
                  "unknownPlanSourceReference" ]
                values
        then
            Some Plan
        elif
            containsAny
                [ "tasksIdentityMismatch"
                  "malformedTasksArtifact"
                  "duplicateTaskId"
                  "unknownTaskSourceReference"
                  "unknownTaskDependency"
                  "taskDependencyCycle"
                  "doneTaskMissingEvidence"
                  "skippedTaskMissingRationale"
                  "missingTasksPrerequisite"
                  "failedTasksPrerequisite"
                  "missingDisposition" ]
                values
        then
            Some Tasks
        else
            None

    let verifyCorrection diagnostics =
        let values = ids diagnostics

        if
            containsAny
                [ "evidence.missingAnalysisPrerequisite"
                  "evidence.analysisNotReady"
                  "malformedAnalysisView"
                  "analysisIdentityMismatch" ]
                values
        then
            Some Analyze
        elif
            containsAny
                [ "missingTasksPrerequisite"
                  "malformedTasksArtifact"
                  "tasksIdentityMismatch"
                  "duplicateTaskId"
                  "unknownTaskDependency"
                  "taskDependencyCycle"
                  "evidence.missingRequiredSkill" ]
                values
        then
            Some Tasks
        elif
            values
            |> Set.exists (fun id ->
                id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("verify.", StringComparison.OrdinalIgnoreCase))
        then
            Some Evidence
        else
            None

    let shipCorrection diagnostics =
        let values = ids diagnostics

        if
            containsAny
                [ "ship.missingVerificationPrerequisite"
                  "ship.verificationNotReady"
                  "ship.failedVerification"
                  "verify.identityMismatch"
                  "verify.malformedVerificationView" ]
                values
        then
            Some Verify
        elif
            containsAny
                [ "evidence.missingAnalysisPrerequisite"
                  "evidence.analysisNotReady"
                  "malformedAnalysisView"
                  "analysisIdentityMismatch" ]
                values
        then
            Some Analyze
        elif
            values
            |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase))
        then
            Some Evidence
        else
            None
