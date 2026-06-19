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
    val specificationIdentityMismatch: path: string -> expectedWorkId: string -> actualWorkId: string -> Diagnostic
    val malformedSpecificationFrontMatter: path: string -> message: string -> Diagnostic
    val duplicateSpecificationId: path: string -> id: string -> Diagnostic
    val missingSpecificationId: path: string -> idFamily: string -> Diagnostic
    val unknownSpecificationReference: path: string -> id: string -> Diagnostic
    val unsafeOverwrite: path: string -> Diagnostic
    val malformedGeneratedView: path: string -> Diagnostic
    val blockedGeneratedViewRefresh: path: string -> relatedIds: string list -> Diagnostic
    val toolDefect: path: string option -> message: string -> Diagnostic
    val buildReport: model: CommandModel -> CommandReport
    val exitCodeForReport: report: CommandReport -> int
