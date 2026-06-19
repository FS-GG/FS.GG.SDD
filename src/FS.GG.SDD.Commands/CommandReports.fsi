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
    val unsafeOverwrite: path: string -> Diagnostic
    val malformedGeneratedView: path: string -> Diagnostic
    val blockedGeneratedViewRefresh: path: string -> relatedIds: string list -> Diagnostic
    val toolDefect: path: string option -> message: string -> Diagnostic
    val buildReport: model: CommandModel -> CommandReport
    val exitCodeForReport: report: CommandReport -> int
