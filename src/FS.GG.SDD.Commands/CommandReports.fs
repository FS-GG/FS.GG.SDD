namespace FS.GG.SDD.Commands

open System
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

module CommandReports =
    module ArtifactRefModule = FS.GG.SDD.Artifacts.ArtifactRef
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let artifactForPath (path: string) =
        match ArtifactRefModule.create path (ArtifactKind.Other "command") ArtifactOwner.Sdd true with
        | Ok artifact -> Some artifact
        | Error _ -> None

    let commandDiagnostic id severity path message correction relatedIds =
        DiagnosticsModule.create id severity (path |> Option.bind artifactForPath) None message correction relatedIds

    let unknownCommand (value: string) =
        commandDiagnostic
            "unknownCommand"
            DiagnosticSeverity.DiagnosticError
            None
            $"Unknown SDD command '{value}'."
            "Use one of: init, charter, specify, clarify, checklist, plan, tasks, analyze, evidence, verify, ship."
            []

    let malformedWorkId (value: string) =
        commandDiagnostic
            "malformedWorkId"
            DiagnosticSeverity.DiagnosticError
            None
            $"Work id '{value}' is malformed."
            "Use a stable lowercase work id such as 003-native-sdd-lifecycle-commands."
            [ value ]

    let missingWorkId (command: SddCommand) =
        commandDiagnostic
            "missingWorkId"
            DiagnosticSeverity.DiagnosticError
            None
            $"Command '{commandName command}' requires --work."
            "Pass --work <id> for work-item lifecycle commands."
            [ commandName command ]

    let unsupportedCommand (command: SddCommand) =
        commandDiagnostic
            "unsupportedLifecycleCommand"
            DiagnosticSeverity.DiagnosticError
            None
            $"Command '{commandName command}' is declared but not implemented in the current MVP slice."
            "Use an implemented lifecycle command in this slice; later lifecycle commands remain pending in tasks.md."
            [ commandName command ]

    let outsideProject () =
        commandDiagnostic
            "outsideProject"
            DiagnosticSeverity.DiagnosticError
            (Some ".fsgg/project.yml")
            "The current directory is not an initialized FS.GG.SDD project."
            "Run fsgg-sdd init or pass --root for an initialized SDD project."
            []

    let missingProjectConfig path =
        commandDiagnostic
            "missingProjectConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Required project config '{path}' is missing."
            "Run fsgg-sdd init or restore the SDD project configuration."
            [ path ]

    let malformedProjectConfig path =
        commandDiagnostic
            "malformedProjectConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Project config '{path}' is malformed."
            "Fix schemaVersion, project.id, project.defaultWorkRoot, sdd.config, and sdd.agents before authoring a charter."
            [ path ]

    let missingSddConfig path =
        commandDiagnostic
            "missingSddConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Required SDD config '{path}' is missing."
            "Restore .fsgg/sdd.yml before authoring a charter."
            [ path ]

    let malformedSddConfig path =
        commandDiagnostic
            "malformedSddConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"SDD config '{path}' is malformed."
            "Fix the SDD lifecycle policy before authoring a charter."
            [ path ]

    let missingAgentsConfig path =
        commandDiagnostic
            "missingAgentsConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Required agent config '{path}' is missing."
            "Restore .fsgg/agents.yml before authoring a charter."
            [ path ]

    let malformedAgentsConfig path =
        commandDiagnostic
            "malformedAgentsConfig"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Agent config '{path}' is malformed."
            "Fix .fsgg/agents.yml before authoring a charter."
            [ path ]

    let duplicateWorkId workId paths =
        commandDiagnostic
            "duplicateWorkId"
            DiagnosticSeverity.DiagnosticError
            None
            $"Work id '{workId}' is declared by more than one work artifact."
            "Keep one authored source for the selected work id and move or rename the duplicate."
            (workId :: (paths |> List.sort))

    let missingCharterPrerequisite path message =
        commandDiagnostic
            "missingCharterPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd charter for the selected work item before running fsgg-sdd specify."
            [ path ]

    let charterIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "charterIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Charter work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move the charter under the matching work id or update its front matter before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedCharterFrontMatter path message =
        commandDiagnostic
            "malformedCharterFrontMatter"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Add schemaVersion, workId, title, stage, changeTier, and status front matter before rerunning."
            [ path ]

    let missingSpecificationIntent path missingFacts =
        let missingText = String.concat ", " missingFacts

        commandDiagnostic
            "missingSpecificationIntent"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Specification intent is missing required facts: {missingText}."
            "Provide input with value, scope, and requirement facts before creating a new specification."
            missingFacts

    let missingSpecificationPrerequisite path message =
        commandDiagnostic
            "missingSpecificationPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd specify for the selected work item before running fsgg-sdd clarify."
            [ path ]

    let specificationIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "specificationIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Specification work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move the specification under the matching work id or update its front matter before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedSpecificationFrontMatter path message =
        commandDiagnostic
            "malformedSpecificationFrontMatter"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Add schemaVersion, workId, title, stage: specify, changeTier, and status front matter before rerunning."
            [ path ]

    let malformedSpecificationFacts path message =
        commandDiagnostic
            "malformedSpecificationFacts"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Fix specification ids, references, and required sections before recording clarification decisions."
            [ path ]

    let duplicateSpecificationId path id =
        commandDiagnostic
            "duplicateSpecificationId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Specification identifier '{id}' is declared more than once."
            "Rename one duplicate identifier and update all structured references before rerunning."
            [ id ]

    let missingSpecificationId path idFamily =
        commandDiagnostic
            "missingSpecificationId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Specification content is missing a required {idFamily} stable id."
            "Add stable story, requirement, scenario, scope, or ambiguity ids before rerunning."
            [ idFamily ]

    let unknownSpecificationReference path id =
        commandDiagnostic
            "unknownSpecificationReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Specification reference '{id}' does not resolve."
            "Declare the referenced specification id or remove the stale structured link before rerunning."
            [ id ]

    let missingClarificationAnswer path missingIds =
        let missingText = String.concat ", " (missingIds |> List.sort)

        commandDiagnostic
            "missingClarificationAnswer"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Clarification input is missing answers for blocking ambiguity: {missingText}."
            "Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity."
            missingIds

    let missingClarificationPrerequisite path message =
        commandDiagnostic
            "missingClarificationPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd clarify for the selected work item before running fsgg-sdd checklist."
            [ path ]

    let clarificationIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "clarificationIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Clarification work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move clarifications.md under the matching work id or update its front matter before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedClarificationFrontMatter path message =
        commandDiagnostic
            "malformedClarificationFrontMatter"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Add schemaVersion, workId, title, stage: clarify, changeTier, status, and sourceSpec front matter before rerunning."
            [ path ]

    let duplicateClarificationId path id =
        commandDiagnostic
            "duplicateClarificationId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Clarification identifier '{id}' is declared more than once."
            "Rename one duplicate clarification question or decision id and update references before rerunning."
            [ id ]

    let unknownClarificationReference path id =
        commandDiagnostic
            "unknownClarificationReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Clarification reference '{id}' does not resolve in the selected specification or clarification artifact."
            "Reference a known AMB, CQ, FR, US, or AC id, or remove the stale clarification link."
            [ id ]

    let unsafeDecisionChange path id =
        commandDiagnostic
            "unsafeDecisionChange"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Clarification decision '{id}' would be changed by this rerun."
            "Preserve existing decisions and add a new decision id for a replacement path."
            [ id ]

    let unresolvedBlockingAmbiguity path ids =
        commandDiagnostic
            "unresolvedBlockingAmbiguity"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Blocking ambiguity remains unresolved after clarification planning."
            "Resolve each blocking ambiguity with a concrete decision or accepted deferral before moving to checklist."
            ids

    let failedRequirementsQuality path message correction relatedIds =
        commandDiagnostic
            "failedRequirementsQuality"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            message
            correction
            relatedIds

    let checklistIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "checklistIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Checklist work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move checklist.md under the matching work id or update its front matter before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedChecklistFrontMatter path message =
        commandDiagnostic
            "malformedChecklistFrontMatter"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Add schemaVersion, workId, title, stage: checklist, changeTier, status, sourceSpec, and sourceClarifications front matter before rerunning."
            [ path ]

    let duplicateChecklistId path id =
        commandDiagnostic
            "duplicateChecklistId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Checklist identifier '{id}' is declared more than once."
            "Rename one duplicate checklist item or result id and update references before rerunning."
            [ id ]

    let unknownChecklistSourceReference path id =
        commandDiagnostic
            "unknownChecklistSourceReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Checklist reference '{id}' does not resolve in the selected specification, clarification, or checklist item set."
            "Reference a known FR, US, AC, SB, AMB, CQ, DEC, or CHK id, or remove the stale checklist link."
            [ id ]

    let staleChecklistResult path resultIds =
        commandDiagnostic
            "staleChecklistResult"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more checklist results were reviewed against older source snapshots."
            "Review the stale checklist results against the current specification and clarification sources."
            resultIds

    let unsafeChecklistResultChange path id =
        commandDiagnostic
            "unsafeChecklistResultChange"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Checklist result '{id}' would be changed by this rerun."
            "Preserve the existing result and add a new result or mark it stale before changing the review decision."
            [ id ]

    let missingChecklistPrerequisite path message =
        commandDiagnostic
            "missingChecklistPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd checklist for the selected work item before running fsgg-sdd plan."
            [ path ]

    let failedChecklistPrerequisite path message relatedIds =
        commandDiagnostic
            "failedChecklistPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Correct blocking checklist findings, stale review results, or unresolved deferrals before planning."
            relatedIds

    let planIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "planIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Plan work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move plan.md under the matching work id or update its front matter before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedPlanFrontMatter path message =
        commandDiagnostic
            "malformedPlanFrontMatter"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Add schemaVersion, workId, title, stage: plan, status, sourceSpec, sourceClarifications, and sourceChecklist front matter before rerunning."
            [ path ]

    let duplicatePlanId path id =
        commandDiagnostic
            "duplicatePlanId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Plan identifier '{id}' is declared more than once."
            "Rename one duplicate planning identifier and update all structured references before rerunning."
            [ id ]

    let unknownPlanSourceReference path id =
        commandDiagnostic
            "unknownPlanSourceReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Plan reference '{id}' does not resolve in the selected specification, clarification, checklist, or plan artifact."
            "Reference a known FR, US, AC, SB, AMB, CQ, DEC, CHK, CR, PD, PC, VO, PM, or GV id, or remove the stale plan link."
            [ id ]

    let stalePlanDecision path decisionIds =
        commandDiagnostic
            "stalePlanDecision"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more plan decisions were recorded against older source snapshots."
            "Review the stale plan decisions before treating the plan as ready for task generation."
            decisionIds

    let unsafePlanDecisionChange path id =
        commandDiagnostic
            "unsafePlanDecisionChange"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Plan decision '{id}' would be changed by this rerun."
            "Preserve existing plan decisions and add a new decision id for the replacement path."
            [ id ]

    let missingPlanPrerequisite path message =
        commandDiagnostic
            "missingPlanPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd plan for the selected work item before running fsgg-sdd tasks."
            [ path ]

    let failedPlanPrerequisite path message relatedIds =
        commandDiagnostic
            "failedPlanPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Correct blocking planning findings, stale decisions, or malformed plan data before task generation."
            relatedIds

    let tasksIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "tasksIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Tasks work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move tasks.yml under the matching work id or update its work.id before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedTasksArtifact path message =
        commandDiagnostic
            "malformedTasksArtifact"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Fix schemaVersion, work identity, source links, task ids, dependencies, and status fields before rerunning."
            [ path ]

    let duplicateTaskId path id =
        commandDiagnostic
            "duplicateTaskId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Task id '{id}' is declared more than once."
            "Rename one duplicate task id and update dependency and evidence references before rerunning."
            [ id ]

    let unknownTaskSourceReference path id =
        commandDiagnostic
            "unknownTaskSourceReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Task source reference '{id}' does not resolve in the selected lifecycle artifacts."
            "Reference a known FR, AC, DEC, PD, PC, VO, PM, GV, CHK, or CR id, or remove the stale task link."
            [ id ]

    let unknownTaskDependency path id =
        commandDiagnostic
            "unknownTaskDependency"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Task dependency '{id}' does not resolve."
            "Declare the dependency task id or remove the dependency edge."
            [ id ]

    let taskDependencyCycle path ids =
        let cycleText = String.concat " -> " ids

        commandDiagnostic
            "taskDependencyCycle"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Task dependency cycle detected: {cycleText}."
            "Remove one dependency edge so the task graph is acyclic."
            ids

    let staleTask path taskIds =
        commandDiagnostic
            "staleTask"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more task entries were recorded against older source snapshots."
            "Review stale tasks and rerun fsgg-sdd tasks after updating their source links."
            taskIds

    let unsafeTaskStatusChange path id =
        commandDiagnostic
            "unsafeTaskStatusChange"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Task '{id}' has a status change marker that this command will not overwrite."
            "Preserve existing task state and record replacement work as a new task id."
            [ id ]

    let doneTaskMissingEvidence path ids =
        commandDiagnostic
            "doneTaskMissingEvidence"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "One or more completed tasks are missing required evidence declarations."
            "Add work/<id>/evidence.yml entries for completed tasks or move the tasks back to pending."
            ids

    let skippedTaskMissingRationale path ids =
        commandDiagnostic
            "skippedTaskMissingRationale"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "One or more skipped tasks are missing skip rationale."
            "Add skipRationale for every skipped task before treating the task graph as ready."
            ids

    let missingTasksPrerequisite path message =
        commandDiagnostic
            "missingTasksPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd tasks for the selected work item before running fsgg-sdd analyze."
            [ path ]

    let failedTasksPrerequisite path message relatedIds =
        commandDiagnostic
            "failedTasksPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Correct tasks.yml or rerun fsgg-sdd tasks before treating the work item as implementation-ready."
            relatedIds

    let analysisIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "analysisIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Analysis view work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Regenerate the analysis view for the selected work id."
            [ expectedWorkId; actualWorkId ]

    let malformedAnalysisView path message =
        commandDiagnostic
            "malformedAnalysisView"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            message
            "Regenerate readiness/<id>/analysis.json from current lifecycle sources."
            [ path ]

    let missingAnalysisPrerequisite path message =
        commandDiagnostic
            "evidence.missingAnalysisPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd analyze for the selected work item before recording evidence."
            [ path ]

    let analysisNotReady path readiness =
        commandDiagnostic
            "evidence.analysisNotReady"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Analysis readiness '{readiness}' is not implementationReady."
            "Correct analysis findings and rerun fsgg-sdd analyze before recording evidence."
            [ readiness ]

    let evidenceIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "evidence.identityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Evidence work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Move evidence.yml under the matching work id or update its structured work id before rerunning."
            [ expectedWorkId; actualWorkId ]

    let malformedEvidenceArtifact path message =
        commandDiagnostic
            "evidence.malformedEvidenceArtifact"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Fix schemaVersion, workId, stage, status, source links, evidence ids, result states, and disclosure fields before rerunning."
            [ path ]

    let duplicateEvidenceId path id =
        commandDiagnostic
            "evidence.duplicateEvidenceId"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Evidence id '{id}' is declared more than once."
            "Rename duplicate evidence declarations and keep stable ids unique within the selected evidence artifact."
            [ id ]

    let unknownEvidenceReference path id =
        commandDiagnostic
            "evidence.unknownReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Evidence reference '{id}' does not resolve in the selected lifecycle artifacts."
            "Reference a known task, requirement, decision, obligation, source artifact, or generated view, or remove the stale evidence link."
            [ id ]

    let missingRequiredEvidence path ids =
        commandDiagnostic
            "evidence.missingRequiredEvidence"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "One or more required evidence obligations are missing current evidence or accepted deferral."
            "Add evidence declarations or accepted deferrals linked to the missing obligation ids."
            ids

    let staleEvidence path ids =
        commandDiagnostic
            "evidence.staleEvidence"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more evidence declarations need review against current lifecycle facts."
            "Review stale evidence declarations and record a compatible update before verification."
            ids

    let staleEvidenceSource path ids =
        commandDiagnostic
            "evidence.staleEvidenceSource"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more evidence source snapshots no longer match current source digests."
            "Rerun the evidence command after reviewing the changed source artifacts."
            ids

    let undisclosedSyntheticEvidence path ids =
        commandDiagnostic
            "evidence.undisclosedSyntheticEvidence"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Synthetic evidence is missing disclosure of the real path it stands in for."
            "Add syntheticDisclosure.standsInFor and syntheticDisclosure.reason to every synthetic declaration."
            ids

    let missingDeferralRationale path ids =
        commandDiagnostic
            "evidence.missingDeferralRationale"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Accepted deferral evidence is missing rationale, owner, scope, or later lifecycle visibility."
            "Add rationale, owner, scope, and laterLifecycleVisibility to every deferral declaration."
            ids

    let missingRequiredSkill path ids =
        commandDiagnostic
            "evidence.missingRequiredSkill"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Completed work references required skills without visible evidence support."
            "Add evidence linked to the required task skill or move the task back to pending until the skill-backed work is complete."
            ids

    let unsupportedEvidenceResultState path states =
        commandDiagnostic
            "evidence.unsupportedResultState"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Evidence contains unsupported result states."
            "Use pass, fail, deferred, missing, stale, advisory, or blocked for evidence result."
            states

    let unsafeEvidenceUpdate path ids =
        commandDiagnostic
            "evidence.unsafeUpdate"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "The proposed evidence update would change existing declaration meaning."
            "Preserve existing declaration ids and meanings; append a compatible new declaration instead."
            ids

    let missingDisposition path ids =
        commandDiagnostic
            "missingDisposition"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "One or more lifecycle facts have no current task disposition."
            "Update tasks.yml or rerun fsgg-sdd tasks after correcting the source artifact."
            ids

    let unsafeOverwrite (path: string) =
        commandDiagnostic
            "unsafeOverwrite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "The command would overwrite existing authored content."
            "Review the existing file and choose an explicit safe update path before rerunning."
            [ path ]

    let malformedGeneratedView path =
        commandDiagnostic
            "malformedGeneratedView"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            $"Generated view '{path}' is malformed and will be refreshed when source data is valid."
            "Regenerate readiness/<id>/work-model.json from current lifecycle sources."
            [ path ]

    let blockedGeneratedViewRefresh path relatedIds =
        commandDiagnostic
            "blockedGeneratedViewRefresh"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            $"Generated view '{path}' cannot be refreshed from the current lifecycle sources."
            "Fix the named lifecycle diagnostics before treating the generated view as current."
            (path :: relatedIds)

    let missingEvidencePrerequisite path message =
        commandDiagnostic
            "verify.missingEvidencePrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd evidence for the selected work item before running fsgg-sdd verify."
            [ path ]

    let verifyIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "verify.identityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Verification view work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Regenerate the verification view for the selected work id."
            [ expectedWorkId; actualWorkId ]

    let malformedVerificationView path message =
        commandDiagnostic
            "verify.malformedVerificationView"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Remove or repair the malformed readiness/<id>/verify.json before refreshing the verification view."
            [ path ]

    let missingRequiredTest path ids =
        commandDiagnostic
            "verify.missingRequiredTest"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "One or more required test obligations are missing satisfying evidence or an accepted deferral."
            "Add verification evidence or an accepted deferral linked to the missing required test obligations."
            ids

    let staleRequiredTest path ids =
        commandDiagnostic
            "verify.staleRequiredTest"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            "One or more required test obligations were satisfied against older lifecycle sources."
            "Re-run the verifying tests and record current evidence before treating the work item as verification-ready."
            ids

    let toolDefect (path: string option) (message: string) =
        commandDiagnostic
            "toolDefect"
            DiagnosticSeverity.DiagnosticError
            path
            message
            "Inspect the command failure and fix the tool or environment before rerunning."
            []

    let missingVerificationPrerequisite path message =
        commandDiagnostic
            "ship.missingVerificationPrerequisite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Run fsgg-sdd verify for the selected work item before running fsgg-sdd ship."
            [ path ]

    let verificationNotReady path (status: string) =
        commandDiagnostic
            "ship.verificationNotReady"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Verification view reports '{status}' rather than a verification-ready status."
            "Resolve the verification findings and rerun fsgg-sdd verify before ship."
            [ path ]

    let failedVerification path ids =
        commandDiagnostic
            "ship.failedVerification"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Verification view reports unresolved blocking findings that must be corrected before ship."
            "Correct the underlying verification findings and rerun fsgg-sdd verify before ship."
            ids

    let staleVerificationView path ids =
        commandDiagnostic
            "ship.staleVerificationView"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Verification view source digests no longer match the current lifecycle sources."
            "Rerun fsgg-sdd verify to refresh the verification view before ship."
            ids

    let shipIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "ship.identityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Ship view work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Regenerate the ship view for the selected work id."
            [ expectedWorkId; actualWorkId ]

    let malformedShipView path message =
        commandDiagnostic
            "ship.malformedShipView"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Remove or repair the malformed readiness/<id>/ship.json before refreshing the ship view."
            [ path ]

    let agentsNoTargets path =
        commandDiagnostic
            "agents.noTargets"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Agent guidance configuration declares no agent targets."
            "Declare at least one agent target (for example claude or codex) in .fsgg/agents.yml."
            [ path ]

    let agentsInvalidGeneratedRoot path targetId =
        commandDiagnostic
            "agents.invalidGeneratedRoot"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Agent guidance target '{targetId}' has a work-model path or generated root that does not resolve within the project."
            "Point the work-model path and each target generated root at a location inside the project."
            [ targetId ]

    let agentsWorkModelIdentityMismatch path expectedWorkId actualWorkId =
        commandDiagnostic
            "agents.workModelIdentityMismatch"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Work model work id '{actualWorkId}' does not match selected work id '{expectedWorkId}'."
            "Select the work id that matches the normalized work model, or regenerate the work model."
            [ expectedWorkId; actualWorkId ]

    let agentsMissingWorkModel path =
        commandDiagnostic
            "agents.missingWorkModel"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Normalized work model '{path}' is missing."
            "Run fsgg-sdd verify or ship for the selected work item to generate the work model before generating agent guidance."
            [ path ]

    let agentsMalformedWorkModel path message =
        commandDiagnostic
            "agents.malformedWorkModel"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Regenerate readiness/<id>/work-model.json from current lifecycle sources before generating agent guidance."
            [ path ]

    let agentsStaleWorkModel path =
        commandDiagnostic
            "agents.staleWorkModel"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Normalized work model source digests no longer match the current lifecycle sources."
            "Rerun fsgg-sdd verify or ship to refresh the work model before generating agent guidance."
            [ path ]

    let agentsBlockedWorkModel path relatedIds =
        commandDiagnostic
            "agents.blockedWorkModel"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Normalized work model is blocked by invalid lifecycle source data."
            "Resolve the work-model diagnostics and refresh the work model before generating agent guidance."
            relatedIds

    let agentsUnknownSourceReference path id =
        commandDiagnostic
            "agents.unknownSourceReference"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            $"Work model references unknown lifecycle fact '{id}'."
            "Correct the lifecycle source or regenerate the work model so all references resolve."
            [ id ]

    let agentsMalformedGeneratedGuidance path message =
        commandDiagnostic
            "agents.malformedGeneratedGuidance"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            message
            "Remove or repair the malformed generated guidance.json before refreshing agent guidance."
            [ path ]

    let agentsStaleGeneratedGuidance path targetId =
        commandDiagnostic
            "agents.staleGeneratedGuidance"
            DiagnosticSeverity.DiagnosticWarning
            (Some path)
            $"Generated agent guidance for target '{targetId}' no longer matches the current normalized work model."
            "Regenerate agent guidance so the generated view matches the current work model."
            [ targetId ]

    let agentsBehaviorDivergence path targetIds =
        commandDiagnostic
            "agents.behaviorDivergence"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Configured agent targets would describe divergent workflow behavior for the same lifecycle model."
            "Regenerate the divergent target guidance from the shared normalized work model so Claude and Codex behavior matches."
            targetIds

    let agentsUnsafeGeneratedViewRefresh path relatedIds =
        commandDiagnostic
            "agents.unsafeGeneratedViewRefresh"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "Generated agent guidance cannot be safely refreshed in this run."
            "Resolve the underlying generated-view diagnostics before refreshing agent guidance."
            relatedIds

    let refreshMissingSource viewPath sourcePath =
        commandDiagnostic
            "refresh.missingSource"
            DiagnosticSeverity.DiagnosticError
            (Some viewPath)
            $"Generated view '{viewPath}' cannot be refreshed because declared source '{sourcePath}' is missing."
            "Restore or author the missing declared source before refreshing the generated view."
            [ sourcePath ]

    let refreshMalformedSource viewPath sourcePath message =
        commandDiagnostic
            "refresh.malformedSource"
            DiagnosticSeverity.DiagnosticError
            (Some viewPath)
            message
            "Repair the malformed or schema-incompatible declared source before refreshing the generated view."
            [ sourcePath ]

    let refreshStaleView viewPath sourcePaths =
        commandDiagnostic
            "refresh.staleView"
            DiagnosticSeverity.DiagnosticWarning
            (Some viewPath)
            $"Generated view '{viewPath}' no longer matches its current declared sources."
            "Refresh the generated view from its current declared sources."
            sourcePaths

    let refreshMalformedGeneratedView viewPath message =
        commandDiagnostic
            "refresh.malformedGeneratedView"
            DiagnosticSeverity.DiagnosticWarning
            (Some viewPath)
            message
            "Regenerate the malformed generated view from its current declared sources."
            [ viewPath ]

    let refreshBlockedUpstreamView viewPath upstreamViewPath =
        commandDiagnostic
            "refresh.blockedUpstreamView"
            DiagnosticSeverity.DiagnosticError
            (Some viewPath)
            $"Generated view '{viewPath}' cannot be refreshed until upstream view '{upstreamViewPath}' is current."
            "Bring the named upstream generated view to currency before refreshing this dependent view."
            [ upstreamViewPath ]

    let refreshUnrenderableSummary summaryPath relatedIds =
        commandDiagnostic
            "refresh.unrenderableSummary"
            DiagnosticSeverity.DiagnosticError
            (Some summaryPath)
            $"Readiness summary '{summaryPath}' cannot be rendered because its required structured readiness data is missing, stale, or blocked."
            "Bring the structured readiness views the summary projects to currency before rendering the summary."
            relatedIds

    let changeFromEffectResult (request: CommandRequest) (result: CommandEffectResult) =
        match result.Effect with
        | CreateDirectory path ->
            let operation =
                if result.Succeeded then
                    match result.Snapshot with
                    | Some _ -> ArtifactOperation.NoChange
                    | None -> ArtifactOperation.Create
                else
                    ArtifactOperation.Refuse

            Some
                { Path = path
                  Kind = "directory"
                  Ownership = "sdd"
                  Operation = operation
                  BeforeDigest = None
                  AfterDigest = None
                  SafeWriteDecision =
                    if not result.Succeeded then "refused"
                    elif request.DryRun && operation <> ArtifactOperation.NoChange then "dryRunOnly"
                    elif operation = ArtifactOperation.NoChange then "preserveExisting"
                    else "safe"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | WriteFile(path, text, kind) ->
            let operation =
                if result.Succeeded then
                    match result.Snapshot with
                    | Some snapshot when snapshot.Text = text -> ArtifactOperation.NoChange
                    | Some _ -> ArtifactOperation.Update
                    | None -> ArtifactOperation.Create
                else
                    ArtifactOperation.Refuse

            let beforeDigest =
                result.Snapshot
                |> Option.map (fun snapshot -> SchemaVersionModule.sha256Text snapshot.Text)

            let afterDigest =
                if result.Succeeded then Some(SchemaVersionModule.sha256Text text) else None

            Some
                { Path = path
                  Kind = writeKindValue kind
                  Ownership = if kind = GeneratedView then "generated" else "authored"
                  Operation = operation
                  BeforeDigest = beforeDigest
                  AfterDigest = afterDigest
                  SafeWriteDecision =
                    if not result.Succeeded then "refused"
                    elif request.DryRun && operation <> ArtifactOperation.NoChange then "dryRunOnly"
                    elif operation = ArtifactOperation.NoChange then "preserveExisting"
                    elif kind = GeneratedView then "refreshGeneratedView"
                    else "safe"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | ReadFile _
        | EnumerateDirectory _
        | EmitStdout _
        | EmitStderr _
        | SetExitCode _ -> None

    let governanceCompatibility : GovernanceCompatibilityFact list =
        [ { Path = ".fsgg/policy.yml"
            Relationship = "optionalGovernancePolicy"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/capabilities.yml"
            Relationship = "optionalGovernanceCapabilities"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/tooling.yml"
            Relationship = "optionalGovernanceTooling"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] } ]

    let planCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if Set.contains "missingSpecificationPrerequisite" ids
           || Set.contains "malformedSpecificationFacts" ids
           || Set.contains "specificationIdentityMismatch" ids then
            Some Specify
        elif Set.contains "missingClarificationPrerequisite" ids
             || Set.contains "malformedClarificationFrontMatter" ids
             || Set.contains "clarificationIdentityMismatch" ids then
            Some Clarify
        elif Set.contains "missingChecklistPrerequisite" ids
             || Set.contains "failedChecklistPrerequisite" ids
             || Set.contains "checklistIdentityMismatch" ids
             || Set.contains "malformedChecklistFrontMatter" ids
             || Set.contains "duplicateChecklistId" ids
             || Set.contains "unknownChecklistSourceReference" ids then
            Some Checklist
        elif Set.contains "planIdentityMismatch" ids
             || Set.contains "malformedPlanFrontMatter" ids
             || Set.contains "duplicatePlanId" ids
             || Set.contains "unknownPlanSourceReference" ids
             || Set.contains "stalePlanDecision" ids
             || Set.contains "unsafePlanDecisionChange" ids then
            Some Plan
        else
            None

    let tasksCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if Set.contains "missingSpecificationPrerequisite" ids
           || Set.contains "malformedSpecificationFacts" ids
           || Set.contains "specificationIdentityMismatch" ids then
            Some Specify
        elif Set.contains "missingClarificationPrerequisite" ids
             || Set.contains "malformedClarificationFrontMatter" ids
             || Set.contains "clarificationIdentityMismatch" ids then
            Some Clarify
        elif Set.contains "missingChecklistPrerequisite" ids
             || Set.contains "failedChecklistPrerequisite" ids
             || Set.contains "checklistIdentityMismatch" ids
             || Set.contains "malformedChecklistFrontMatter" ids
             || Set.contains "duplicateChecklistId" ids
             || Set.contains "unknownChecklistSourceReference" ids then
            Some Checklist
        elif Set.contains "missingPlanPrerequisite" ids
             || Set.contains "failedPlanPrerequisite" ids
             || Set.contains "planIdentityMismatch" ids
             || Set.contains "malformedPlanFrontMatter" ids
             || Set.contains "duplicatePlanId" ids
             || Set.contains "unknownPlanSourceReference" ids then
            Some Plan
        elif Set.contains "tasksIdentityMismatch" ids
             || Set.contains "malformedTasksArtifact" ids
             || Set.contains "duplicateTaskId" ids
             || Set.contains "unknownTaskSourceReference" ids
             || Set.contains "unknownTaskDependency" ids
             || Set.contains "taskDependencyCycle" ids
             || Set.contains "unsafeTaskStatusChange" ids
             || Set.contains "doneTaskMissingEvidence" ids
             || Set.contains "skippedTaskMissingRationale" ids
             || Set.contains "missingTasksPrerequisite" ids
             || Set.contains "failedTasksPrerequisite" ids
             || Set.contains "missingDisposition" ids then
            Some Tasks
        elif Set.contains "malformedAnalysisView" ids
             || Set.contains "analysisIdentityMismatch" ids
             || Set.contains "blockedGeneratedViewRefresh" ids
             || Set.contains "malformedGeneratedView" ids then
            None
        else
            None

    let verifyCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if ids |> Set.contains "evidence.missingAnalysisPrerequisite"
           || ids |> Set.contains "evidence.analysisNotReady"
           || ids |> Set.contains "malformedAnalysisView"
           || ids |> Set.contains "analysisIdentityMismatch" then
            Some Analyze
        elif ids |> Set.contains "missingTasksPrerequisite"
             || ids |> Set.contains "malformedTasksArtifact"
             || ids |> Set.contains "tasksIdentityMismatch"
             || ids |> Set.contains "duplicateTaskId"
             || ids |> Set.contains "unknownTaskDependency"
             || ids |> Set.contains "taskDependencyCycle"
             || ids |> Set.contains "unsupportedTaskStatus"
             || ids |> Set.contains "evidence.missingRequiredSkill" then
            Some Tasks
        elif ids |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase) || id.StartsWith("verify.", StringComparison.OrdinalIgnoreCase)) then
            Some Evidence
        else
            None

    let shipCorrectionCommand (diagnostics: Diagnostic list) =
        let ids = diagnostics |> List.map _.Id |> Set.ofList

        if ids |> Set.contains "ship.missingVerificationPrerequisite"
           || ids |> Set.contains "ship.verificationNotReady"
           || ids |> Set.contains "ship.failedVerification"
           || ids |> Set.contains "ship.staleVerificationView"
           || ids |> Set.contains "verify.identityMismatch"
           || ids |> Set.contains "verify.malformedVerificationView" then
            Some Verify
        elif ids |> Set.contains "evidence.missingAnalysisPrerequisite"
             || ids |> Set.contains "evidence.analysisNotReady"
             || ids |> Set.contains "malformedAnalysisView"
             || ids |> Set.contains "analysisIdentityMismatch" then
            Some Analyze
        elif ids |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase)) then
            Some Evidence
        else
            None

    let nextAction
        (diagnostics: Diagnostic list)
        (request: CommandRequest)
        (checklist: ChecklistSummary option)
        (plan: PlanSummary option)
        (tasks: TasksSummary option)
        (analysis: AnalysisSummary option)
        (evidence: EvidenceSummary option)
        (verification: VerificationSummary option)
        (ship: ShipSummary option)
        (agentGuidance: AgentGuidanceSummary option)
        (refresh: RefreshSummary option)
        =
        let blocking =
            diagnostics
            |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            |> List.map (fun diagnostic -> diagnostic.Id)
            |> List.distinct
            |> List.sort

        if not (List.isEmpty blocking) then
            let ids = blocking |> Set.ofList

            let correctionCommand =
                match request.Command with
                | Plan -> planCorrectionCommand diagnostics
                | Tasks -> tasksCorrectionCommand diagnostics
                | Analyze -> tasksCorrectionCommand diagnostics
                | Verify -> verifyCorrectionCommand diagnostics
                | Ship -> shipCorrectionCommand diagnostics
                | Agents ->
                    if ids |> Set.contains "agents.missingWorkModel"
                       || ids |> Set.contains "agents.staleWorkModel"
                       || ids |> Set.contains "agents.malformedWorkModel"
                       || ids |> Set.contains "agents.blockedWorkModel" then
                        Some Verify
                    else
                        None
                | Evidence ->
                    if ids |> Set.contains "evidence.missingAnalysisPrerequisite"
                       || ids |> Set.contains "evidence.analysisNotReady"
                       || ids |> Set.contains "malformedAnalysisView"
                       || ids |> Set.contains "analysisIdentityMismatch" then
                        Some Analyze
                    elif ids |> Set.contains "missingTasksPrerequisite"
                         || ids |> Set.contains "malformedTasksArtifact"
                         || ids |> Set.contains "tasksIdentityMismatch"
                         || ids |> Set.contains "evidence.missingRequiredSkill" then
                        Some Tasks
                    elif ids |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase)) then
                        Some Evidence
                    else
                        None
                | Refresh -> None
                | _ -> None

            Some
                { ActionId = "correctBlockingDiagnostics"
                  Command = correctionCommand
                  WorkId = request.WorkId
                  Reason = "The command is blocked by diagnostics."
                  RequiredArtifacts = []
                  BlockingDiagnosticIds = blocking }
        elif request.Command = Plan && diagnostics |> List.exists (fun diagnostic -> diagnostic.Id = "stalePlanDecision") then
            Some
                { ActionId = "plan.correctStaleDecisions"
                  Command = Some Plan
                  WorkId = request.WorkId
                  Reason = "Plan decisions need review before task generation."
                  RequiredArtifacts = plan |> Option.map (fun summary -> [ $"work/{summary.WorkId}/plan.md" ]) |> Option.defaultValue []
                  BlockingDiagnosticIds = [ "stalePlanDecision" ] }
        elif request.Command = Tasks && diagnostics |> List.exists (fun diagnostic -> diagnostic.Id = "staleTask") then
            Some
                { ActionId = "tasks.correctStaleTasks"
                  Command = Some Tasks
                  WorkId = request.WorkId
                  Reason = "Task source links need review before analysis."
                  RequiredArtifacts = tasks |> Option.map (fun summary -> [ $"work/{summary.WorkId}/tasks.yml" ]) |> Option.defaultValue []
                  BlockingDiagnosticIds = [ "staleTask" ] }
        elif
            checklist
            |> Option.exists (fun summary -> summary.FailedBlockingCount > 0 || summary.StaleResultCount > 0)
        then
            let summary = checklist.Value
            let ids =
                diagnostics
                |> List.choose (fun diagnostic ->
                    if diagnostic.Id = "failedRequirementsQuality" || diagnostic.Id = "staleChecklistResult" then
                        Some diagnostic.Id
                    else
                        None)
                |> List.distinct
                |> List.sort

            Some
                { ActionId = "correctBlockingDiagnostics"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Checklist has requirements-quality findings or stale review results."
                  RequiredArtifacts = [ summary.SourceSpec; summary.SourceClarifications; $"work/{summary.WorkId}/checklist.md" ] |> List.sort
                  BlockingDiagnosticIds = ids }
        elif request.Command = Analyze then
            Some
                { ActionId = "analysis.next.implement"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Lifecycle sources are current and ready for implementation."
                  RequiredArtifacts = analysis |> Option.map (fun summary -> [ summary.AnalysisPath ]) |> Option.defaultValue []
                  BlockingDiagnosticIds = [] }
        elif request.Command = Evidence then
            Some
                { ActionId = "evidence.next.verify"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Evidence declarations are current and ready for verification."
                  RequiredArtifacts =
                    evidence
                    |> Option.map (fun summary -> [ summary.EvidencePath; $"readiness/{summary.WorkId}/work-model.json" ])
                    |> Option.defaultValue []
                  BlockingDiagnosticIds = [] }
        elif request.Command = Verify then
            Some
                { ActionId = "verify.next.ship"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Verification readiness is current and ready for ship."
                  RequiredArtifacts =
                    verification
                    |> Option.map (fun summary -> [ summary.VerifyPath; $"readiness/{summary.WorkId}/work-model.json" ])
                    |> Option.defaultValue []
                  BlockingDiagnosticIds = [] }
        elif request.Command = Ship then
            Some
                { ActionId = "ship.next.protectedBoundary"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Ship readiness is current and ready for the protected-boundary handoff."
                  RequiredArtifacts =
                    ship
                    |> Option.map (fun summary -> [ summary.ShipPath; $"readiness/{summary.WorkId}/work-model.json" ])
                    |> Option.defaultValue []
                  BlockingDiagnosticIds = [] }
        elif request.Command = Agents then
            Some
                { ActionId = "agentsGenerated"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "Generated agent guidance is current; regenerate when the work model changes."
                  RequiredArtifacts =
                    agentGuidance
                    |> Option.map (fun summary -> summary.GeneratedRoots)
                    |> Option.defaultValue []
                  BlockingDiagnosticIds = [] }
        elif request.Command = Refresh then
            let warningBlocked =
                refresh
                |> Option.map (fun summary -> summary.BlockedViewIds)
                |> Option.defaultValue []

            if not (List.isEmpty warningBlocked) then
                Some
                    { ActionId = "refresh.correctBlockedViews"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Some generated views could not be refreshed; correct the named source or upstream view."
                      RequiredArtifacts = warningBlocked |> List.sort
                      BlockingDiagnosticIds = [] }
            else
                Some
                    { ActionId = "refreshGenerated"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Generated views are current; rely on the refreshed readiness for the selected work item."
                      RequiredArtifacts =
                        refresh
                        |> Option.map (fun summary -> [ summary.SummaryPath ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
        else
            match nextLifecycleCommand request.Command with
            | Some command ->
                let requiredArtifacts =
                    match request.Command, request.WorkId with
                    | Charter, Some workId -> [ $"work/{workId}/charter.md" ]
                    | Specify, Some workId -> [ $"work/{workId}/charter.md"; $"work/{workId}/spec.md" ]
                    | Clarify, Some workId -> [ $"work/{workId}/spec.md"; $"work/{workId}/clarifications.md" ]
                    | Checklist, Some workId -> [ $"work/{workId}/spec.md"; $"work/{workId}/clarifications.md"; $"work/{workId}/checklist.md" ]
                    | Plan, Some workId -> [ $"work/{workId}/plan.md" ]
                    | Tasks, Some workId -> [ $"work/{workId}/tasks.yml" ]
                    | _ -> []

                Some
                    { ActionId = "nextLifecycleCommand"
                      Command = Some command
                      WorkId = request.WorkId
                      Reason = $"Command '{commandName request.Command}' completed."
                      RequiredArtifacts = requiredArtifacts
                      BlockingDiagnosticIds = [] }
            | None -> None

    let outcome (diagnostics: Diagnostic list) (changes: ArtifactChange list) =
        if diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError) then
            CommandOutcome.Blocked
        elif diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticWarning) then
            CommandOutcome.SucceededWithWarnings
        elif List.isEmpty changes then
            CommandOutcome.NoChange
        elif changes |> List.forall (fun change -> change.Operation = ArtifactOperation.NoChange || change.Operation = ArtifactOperation.Preserve) then
            CommandOutcome.NoChange
        else
            CommandOutcome.Succeeded

    let sortChanges (changes: ArtifactChange list) =
        changes
        |> List.sortBy (fun change -> change.Path, artifactOperationValue change.Operation, change.Ownership)

    let sortGovernance (facts: GovernanceCompatibilityFact list) =
        facts |> List.sortBy (fun fact -> fact.Path)

    let buildReport (model: CommandModel) =
        let effectDiagnostics =
            model.InterpretedEffects
            |> List.choose (fun result -> result.Diagnostic)

        let diagnostics =
            model.Diagnostics @ effectDiagnostics
            |> DiagnosticsModule.sort

        let changes =
            model.InterpretedEffects
            |> List.choose (changeFromEffectResult model.Request)
            |> sortChanges

        { SchemaVersion = 1
          ReportVersion = "1.0.0"
          Command = model.Request.Command
          ProjectRoot = "."
          OutputFormat = model.Request.OutputFormat
          DryRun = model.Request.DryRun
          OverwritePolicy = model.Request.OverwritePolicy
          Outcome = outcome diagnostics changes
          WorkId = model.Request.WorkId
          ChangedArtifacts = changes
          Specification = model.Specification
          Clarification = model.Clarification
          Checklist = model.Checklist
          Plan = model.Plan
          Tasks = model.Tasks
          Analysis = model.Analysis
          Evidence = model.Evidence
          Verification = model.Verification
          Ship = model.Ship
          AgentGuidance = model.AgentGuidance
          Refresh = model.Refresh
          GeneratedViews = model.GeneratedViews |> List.sortBy (fun view -> view.Path)
          Diagnostics = diagnostics
          GovernanceCompatibility = sortGovernance governanceCompatibility
          NextAction = nextAction diagnostics model.Request model.Checklist model.Plan model.Tasks model.Analysis model.Evidence model.Verification model.Ship model.AgentGuidance model.Refresh }

    let exitCodeForReport (report: CommandReport) =
        match report.Outcome with
        | CommandOutcome.Blocked ->
            if report.Diagnostics |> List.exists (fun d -> d.Id = "toolDefect") then 2 else 1
        | CommandOutcome.Succeeded
        | CommandOutcome.SucceededWithWarnings
        | CommandOutcome.NoChange -> 0
