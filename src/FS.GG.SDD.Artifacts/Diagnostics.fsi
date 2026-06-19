namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef

module Diagnostics =
    type DiagnosticSeverity =
        | DiagnosticError
        | DiagnosticWarning
        | DiagnosticInfo

    type SourceLocation = { Line: int option; Column: int option }

    type Diagnostic =
        { Id: string
          Severity: DiagnosticSeverity
          Artifact: ArtifactRef option
          Location: SourceLocation option
          Message: string
          Correction: string
          RelatedIds: string list }

    val severityValue: severity: DiagnosticSeverity -> string
    val severityRank: severity: DiagnosticSeverity -> int

    val create:
        id: string ->
        severity: DiagnosticSeverity ->
        artifact: ArtifactRef option ->
        location: SourceLocation option ->
        message: string ->
        correction: string ->
        relatedIds: string list ->
            Diagnostic

    val missingArtifact: artifact: ArtifactRef -> correction: string -> Diagnostic
    val malformedSchemaVersion: artifact: ArtifactRef -> message: string -> Diagnostic
    val deprecatedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val unsupportedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val futureSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val duplicateIdentifier: artifact: ArtifactRef -> id: string -> locations: SourceLocation list -> Diagnostic
    val unknownReference: artifact: ArtifactRef -> id: string -> correction: string -> Diagnostic
    val requirementNotTyped: artifact: ArtifactRef -> id: string -> correction: string -> Diagnostic
    val workModelInconsistent: artifact: ArtifactRef -> message: string -> correction: string -> relatedIds: string list -> Diagnostic
    val proseStructuredMismatch: artifact: ArtifactRef -> message: string -> correction: string -> Diagnostic
    val staleGeneratedView: artifact: ArtifactRef -> message: string -> correction: string -> Diagnostic
    val missingGeneratedWorkModel: artifact: ArtifactRef -> expectedPath: string -> Diagnostic
    val malformedDigest: artifact: ArtifactRef -> value: string -> Diagnostic
    val sort: diagnostics: Diagnostic list -> Diagnostic list
    val hasBlocking: diagnostics: Diagnostic list -> bool
