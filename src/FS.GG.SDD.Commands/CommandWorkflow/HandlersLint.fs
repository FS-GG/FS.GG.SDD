namespace FS.GG.SDD.Commands.Internal

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

/// `fsgg-sdd lint <artifact>` handler (feature 076, US1). Strictly read-only, mirroring
/// `doctor`: the `plan` stage emitted a single `ReadFile <artifact>` effect; this driver
/// runs the pure `LintEngine` over the snapshot and records the `LintSummary`. It emits
/// **no** mutating effect (FR-008) — a write-audit over a lint run finds only the one
/// `ReadFile`.
module internal HandlersLint =

    // An unusable-input result (FR-011: exit 2) — a missing/unreadable/unsupplied artifact.
    let private unusable (path: string) (message: string) (correction: string) : LintSummary =
        let diag =
            create "lintUnusableInput" DiagnosticError None None message correction []

        { ArtifactPath = path
          Kind = LintArtifactKind.Unrecognized
          Defects =
            [ { Class = Unresolvable
                Diagnostic = diag
                GrammarPointer = None } ]
          Outcome = UnusableInput }

    let computeLintNext model =
        match model.Lint with
        | Some _ -> model, []
        | None ->
            let summary =
                match model.Request.Artifact with
                | None ->
                    unusable
                        ""
                        "No artifact path was supplied to lint."
                        "Run `fsgg-sdd lint <artifact>` with a path to an authored SDD artifact."
                | Some path ->
                    match snapshot path model with
                    | Some snap -> LintEngine.lint snap
                    | None ->
                        unusable
                            path
                            $"The artifact '{path}' is missing or could not be read."
                            "Check the path and rerun `fsgg-sdd lint <artifact>`."

            // Surface the lint defects as command diagnostics too, so the report Outcome and
            // the text/rich projections see them (the bespoke exitCodeForLint reads Lint.Outcome).
            let diagnostics = summary.Defects |> List.map (fun defect -> defect.Diagnostic)

            { model with
                Lint = Some summary
                Diagnostics = model.Diagnostics @ diagnostics },
            []

    // `<stage> --explain` (feature 076, US3): the same pre-flight, run over the stage's own
    // primary artifact as a non-blocking dry run — no mutation, no state advance.
    let computeExplainLint model =
        match model.Lint with
        | Some _ -> model, []
        | None ->
            let summary =
                match
                    model.Request.WorkId
                    |> Option.bind (fun workId -> stagePrimaryArtifactPath model.Request.Command workId)
                with
                | Some path ->
                    match snapshot path model with
                    | Some snap -> LintEngine.lint snap
                    | None ->
                        unusable
                            path
                            $"The stage artifact '{path}' is missing or could not be read."
                            "Author the stage's artifact before running `<stage> --explain`."
                | None ->
                    unusable
                        ""
                        "This command has no primary artifact to --explain."
                        "Use `--explain` on an authoring stage (charter/specify/clarify/checklist/plan/tasks/evidence)."

            let diagnostics = summary.Defects |> List.map (fun defect -> defect.Diagnostic)

            { model with
                Lint = Some summary
                Diagnostics = model.Diagnostics @ diagnostics },
            []
