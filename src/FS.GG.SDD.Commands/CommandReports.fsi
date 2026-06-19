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
    val unsafeOverwrite: path: string -> Diagnostic
    val toolDefect: path: string option -> message: string -> Diagnostic
    val buildReport: model: CommandModel -> CommandReport
    val exitCodeForReport: report: CommandReport -> int
