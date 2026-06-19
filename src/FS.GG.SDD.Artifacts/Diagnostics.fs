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

    let severityValue severity =
        match severity with
        | DiagnosticError -> "error"
        | DiagnosticWarning -> "warning"
        | DiagnosticInfo -> "info"

    let severityRank severity =
        match severity with
        | DiagnosticError -> 0
        | DiagnosticWarning -> 1
        | DiagnosticInfo -> 2

    let create id severity artifact location message correction relatedIds =
        { Id = id
          Severity = severity
          Artifact = artifact
          Location = location
          Message = message
          Correction = correction
          RelatedIds = relatedIds }

    let missingArtifact artifact correction =
        create
            "missingArtifact"
            DiagnosticError
            (Some artifact)
            None
            $"Required artifact '{artifact.Path}' is missing."
            correction
            [ artifact.Path ]

    let malformedSchemaVersion artifact message =
        create "malformedSchemaVersion" DiagnosticError (Some artifact) None message "Add schemaVersion: 1 to the structured artifact." []

    let deprecatedSchemaVersion artifact value =
        create
            "deprecatedSchemaVersion"
            DiagnosticWarning
            (Some artifact)
            None
            $"Schema version '{value}' is deprecated."
            "Migrate the artifact to schemaVersion: 1 before the deprecated version is removed."
            [ value; "supported:1" ]

    let unsupportedSchemaVersion artifact value =
        create
            "unsupportedSchemaVersion"
            DiagnosticError
            (Some artifact)
            None
            $"Schema version '{value}' is not supported by this contract."
            "Use schemaVersion: 1 or add a documented migration path."
            [ value; "supported:1" ]

    let futureSchemaVersion artifact value =
        create
            "futureSchemaVersion"
            DiagnosticError
            (Some artifact)
            None
            $"Schema version '{value}' is newer than this generator understands."
            "Use a newer FS.GG.SDD.Artifacts generator or downgrade the artifact schema to 1."
            [ value; "supported:1" ]

    let duplicateIdentifier artifact id locations =
        let firstLocation = locations |> List.tryHead

        create
            "duplicateIdentifier"
            DiagnosticError
            (Some artifact)
            firstLocation
            $"Identifier '{id}' is declared more than once."
            "Rename one identifier and update all references."
            [ id ]

    let unknownReference artifact id correction =
        create "unknownReference" DiagnosticError (Some artifact) None $"Reference '{id}' does not resolve." correction [ id ]

    let requirementNotTyped artifact id correction =
        create
            "requirementNotTyped"
            DiagnosticError
            (Some artifact)
            None
            $"Requirement or acceptance criterion '{id}' appears in Markdown but is absent from the structured requirement set."
            correction
            [ id ]

    let workModelInconsistent artifact message correction relatedIds =
        create "workModelInconsistent" DiagnosticError (Some artifact) None message correction relatedIds

    let proseStructuredMismatch artifact message correction =
        create "proseStructuredMismatch" DiagnosticWarning (Some artifact) None message correction []

    let staleGeneratedView artifact message correction =
        create "staleGeneratedView" DiagnosticError (Some artifact) None message correction [ artifact.Path ]

    let missingGeneratedWorkModel artifact expectedPath =
        create
            "missingGeneratedWorkModel"
            DiagnosticError
            (Some artifact)
            None
            $"Generated work model '{expectedPath}' is missing."
            "Generate readiness/<id>/work-model.json from the current lifecycle sources before treating the view as current."
            [ expectedPath ]

    let malformedDigest artifact value =
        create
            "malformedDigest"
            DiagnosticError
            (Some artifact)
            None
            $"Digest '{value}' is malformed."
            "Use lowercase sha256 hex digests generated from normalized source bytes."
            [ value ]

    let locationKey location =
        match location with
        | Some loc -> defaultArg loc.Line 0, defaultArg loc.Column 0
        | None -> 0, 0

    let sort diagnostics =
        diagnostics
        |> List.sortBy (fun diagnostic ->
            let path =
                diagnostic.Artifact
                |> Option.map (fun artifact -> artifact.Path)
                |> Option.defaultValue ""

            let line, column = locationKey diagnostic.Location
            severityRank diagnostic.Severity, diagnostic.Id, path, line, column, diagnostic.Message)

    let hasBlocking diagnostics =
        diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticError)
