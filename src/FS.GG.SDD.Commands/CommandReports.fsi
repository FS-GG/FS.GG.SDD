namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes

module CommandReports =
    val commandDiagnostic:
        id: string ->
        severity: DiagnosticSeverity ->
        path: string option ->
        message: string ->
        correction: string ->
        relatedIds: string list ->
            Diagnostic

    val unknownCommand: value: string -> Diagnostic
    val malformedWorkId: value: string -> Diagnostic
    val missingWorkId: command: SddCommand -> Diagnostic
    val unsupportedCommand: command: SddCommand -> Diagnostic
    val outsideProject: unit -> Diagnostic
    val missingProjectConfig: path: string -> Diagnostic
    val malformedProjectConfig: path: string -> Diagnostic
    val missingSddConfig: path: string -> Diagnostic
    val malformedSddConfig: path: string -> Diagnostic
    val missingAgentsConfig: path: string -> Diagnostic
    val malformedAgentsConfig: path: string -> Diagnostic
    val duplicateWorkId: workId: string -> paths: string list -> Diagnostic
    val missingCharterPrerequisite: path: string -> message: string -> Diagnostic
    val charterIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedCharterFrontMatter: path: string -> message: string -> Diagnostic
    val missingSpecificationIntent: path: string -> missingFacts: string list -> Diagnostic
    val missingSpecificationPrerequisite: path: string -> message: string -> Diagnostic
    val specificationIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedSpecificationFrontMatter: path: string -> message: string -> Diagnostic
    val malformedSpecificationFacts: path: string -> message: string -> Diagnostic
    val duplicateSpecificationId: path: string -> id: string -> Diagnostic
    val missingSpecificationId: path: string -> idFamily: string -> Diagnostic
    val unknownSpecificationReference: path: string -> id: string -> Diagnostic
    val missingClarificationAnswer: path: string -> missingIds: string list -> Diagnostic
    val missingClarificationPrerequisite: path: string -> message: string -> Diagnostic
    val clarificationIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedClarificationFrontMatter: path: string -> message: string -> Diagnostic
    val duplicateClarificationId: path: string -> id: string -> Diagnostic
    val unknownClarificationReference: path: string -> id: string -> Diagnostic
    val unsafeDecisionChange: path: string -> id: string -> Diagnostic
    val unresolvedBlockingAmbiguity: path: string -> ids: string list -> Diagnostic
    val failedRequirementsQuality: path: string -> message: string -> correction: string -> relatedIds: string list -> Diagnostic
    val checklistIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedChecklistFrontMatter: path: string -> message: string -> Diagnostic
    val duplicateChecklistId: path: string -> id: string -> Diagnostic
    val unknownChecklistSourceReference: path: string -> id: string -> Diagnostic
    val staleChecklistResult: path: string -> resultIds: string list -> Diagnostic
    val unsafeChecklistResultChange: path: string -> id: string -> Diagnostic
    val missingChecklistPrerequisite: path: string -> message: string -> Diagnostic
    val failedChecklistPrerequisite: path: string -> message: string -> relatedIds: string list -> Diagnostic
    val planIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedPlanFrontMatter: path: string -> message: string -> Diagnostic
    val duplicatePlanId: path: string -> id: string -> Diagnostic
    val unknownPlanSourceReference: path: string -> id: string -> Diagnostic
    val stalePlanDecision: path: string -> decisionIds: string list -> Diagnostic
    val unsafePlanDecisionChange: path: string -> id: string -> Diagnostic
    val missingPlanPrerequisite: path: string -> message: string -> Diagnostic
    val failedPlanPrerequisite: path: string -> message: string -> relatedIds: string list -> Diagnostic
    val tasksIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedTasksArtifact: path: string -> message: string -> Diagnostic
    val duplicateTaskId: path: string -> id: string -> Diagnostic
    val unknownTaskSourceReference: path: string -> id: string -> Diagnostic
    val unknownTaskDependency: path: string -> id: string -> Diagnostic
    val taskDependencyCycle: path: string -> ids: string list -> Diagnostic
    val staleTask: path: string -> taskIds: string list -> Diagnostic
    val unsafeTaskStatusChange: path: string -> id: string -> Diagnostic
    val doneTaskMissingEvidence: path: string -> ids: string list -> Diagnostic
    val skippedTaskMissingRationale: path: string -> ids: string list -> Diagnostic
    val missingTasksPrerequisite: path: string -> message: string -> Diagnostic
    val failedTasksPrerequisite: path: string -> message: string -> relatedIds: string list -> Diagnostic
    val analysisIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedAnalysisView: path: string -> message: string -> Diagnostic
    val missingAnalysisPrerequisite: path: string -> message: string -> Diagnostic
    val analysisNotReady: path: string -> readiness: string -> Diagnostic
    val evidenceIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedEvidenceArtifact: path: string -> message: string -> Diagnostic
    val duplicateEvidenceId: path: string -> id: string -> Diagnostic
    val unknownEvidenceReference: path: string -> id: string -> Diagnostic
    val missingRequiredEvidence: path: string -> ids: string list -> Diagnostic
    val staleEvidence: path: string -> ids: string list -> Diagnostic
    val staleEvidenceSource: path: string -> ids: string list -> Diagnostic
    val undisclosedSyntheticEvidence: path: string -> ids: string list -> Diagnostic
    val missingDeferralRationale: path: string -> ids: string list -> Diagnostic
    val missingRequiredSkill: path: string -> ids: string list -> Diagnostic
    val unsupportedEvidenceResultState: path: string -> states: string list -> Diagnostic
    val unsafeEvidenceUpdate: path: string -> ids: string list -> Diagnostic
    val missingDisposition: path: string -> ids: string list -> Diagnostic
    val unsafeOverwrite: path: string -> Diagnostic
    val malformedGeneratedView: path: string -> Diagnostic
    val blockedGeneratedViewRefresh: path: string -> relatedIds: string list -> Diagnostic
    val missingEvidencePrerequisite: path: string -> message: string -> Diagnostic
    val verifyIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedVerificationView: path: string -> message: string -> Diagnostic
    val missingRequiredTest: path: string -> ids: string list -> Diagnostic
    val staleRequiredTest: path: string -> ids: string list -> Diagnostic
    val toolDefect: path: string option -> message: string -> Diagnostic
    val buildReport: model: CommandModel -> CommandReport
    val exitCodeForReport: report: CommandReport -> int
