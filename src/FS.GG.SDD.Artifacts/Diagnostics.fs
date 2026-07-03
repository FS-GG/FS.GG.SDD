namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef

module Diagnostics =
    type DiagnosticSeverity =
        | DiagnosticError
        | DiagnosticWarning
        | DiagnosticInfo

    type SourceLocation =
        { Line: int option; Column: int option }

    type Diagnostic =
        { Id: string
          Severity: DiagnosticSeverity
          Artifact: ArtifactRef option
          Location: SourceLocation option
          Message: string
          Correction: string
          RelatedIds: string list
          IsToolDefect: bool }

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
          RelatedIds = relatedIds
          IsToolDefect = false }

    let markToolDefect (diagnostic: Diagnostic) = { diagnostic with IsToolDefect = true }

    let signalsStaleView (diagnostic: Diagnostic) =
        diagnostic.Id.IndexOf("stale", System.StringComparison.OrdinalIgnoreCase) >= 0

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
        create
            "malformedSchemaVersion"
            DiagnosticError
            (Some artifact)
            None
            message
            "Add schemaVersion: 1 to the structured artifact."
            []

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
        create
            "unknownReference"
            DiagnosticError
            (Some artifact)
            None
            $"Reference '{id}' does not resolve."
            correction
            [ id ]

    // A declared cross-reference whose value is not a well-formed id of its kind (e.g. a task
    // dependency `T01` instead of `T001`). Previously such values were silently dropped by the
    // `Result.toOption` id parsers, so the malformed edge never reached referenceDiagnostics —
    // a dropped dependency could flip verify readiness (#70/§2.5). Blocking, like unknownReference.
    let malformedReference artifact (kind: string) (value: string) =
        create
            "malformedReference"
            DiagnosticError
            (Some artifact)
            None
            $"Reference '{value}' is not a well-formed {kind} id."
            $"Use a canonical {kind} id, or remove the reference."
            [ value ]

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

    let scaffoldRef (path: string) =
        match ArtifactRef.create path (ArtifactKind.Other "scaffold") ArtifactOwner.Sdd false with
        | Ok artifact -> Some artifact
        | Error _ -> None

    let scaffoldProviderMissing () =
        create
            "scaffold.providerMissing"
            DiagnosticError
            None
            None
            "No template provider was selected for scaffold."
            "Pass `--provider <name>`; for the SDD skeleton only, use `fsgg-sdd init`."
            []

    let scaffoldProviderUnknown name =
        create
            "scaffold.providerUnknown"
            DiagnosticError
            (scaffoldRef ".fsgg/providers.yml")
            None
            $"No provider named '{name}' is registered."
            $"Register '{name}' in `.fsgg/providers.yml` or correct the `--provider` name."
            [ name ]

    let scaffoldProviderVersionUnsupported name declaredVersion supportedRange =
        create
            "scaffold.providerVersionUnsupported"
            DiagnosticError
            (scaffoldRef ".fsgg/providers.yml")
            None
            $"Provider '{name}' declares contract version '{declaredVersion}'; supported range is '{supportedRange}'."
            "Upgrade FS.GG.SDD or the provider so the declared contract version falls within the supported range."
            [ name; declaredVersion; supportedRange ]

    let scaffoldProviderParamMissing name (missingKeys: string list) =
        let keys = missingKeys |> List.sort
        let rendered = String.concat ", " keys

        create
            "scaffold.providerParamMissing"
            DiagnosticError
            (scaffoldRef ".fsgg/providers.yml")
            None
            $"Provider '{name}' requires parameter(s) with no supplied value: {rendered}."
            "Supply each missing parameter with `--param <key>=<value>`."
            (name :: keys)

    let scaffoldTargetCollision (paths: string list) =
        let ordered = paths |> List.sort

        create
            "scaffold.targetCollision"
            DiagnosticError
            None
            None
            $"Target is not empty; {List.length ordered} existing path(s) would be overwritten."
            "Re-run with `--force` to materialize into a non-empty target."
            ordered

    let scaffoldProviderEmpty name =
        create
            "scaffold.providerEmpty"
            DiagnosticInfo
            None
            None
            $"Provider '{name}' ran successfully but produced no files."
            "No action required; the provider produced an empty scaffold."
            [ name ]

    let scaffoldProviderFailed name (exitCode: int) =
        create
            "scaffold.providerFailed"
            DiagnosticError
            None
            None
            $"Provider '{name}' exited {exitCode}."
            "Inspect the provider's captured output in the scaffold report (`providerInvocation.commandLine` / `.standardOutput` / `.standardError`), fix the provider, then re-run scaffold. Any partial output is listed in the produced paths."
            [ name; string exitCode ]
        |> markToolDefect

    let scaffoldProviderUnavailable name =
        create
            "scaffold.providerUnavailable"
            DiagnosticError
            None
            None
            $"Could not run provider '{name}' (`dotnet`/template engine not found)."
            "Install the .NET SDK and the named template, then re-run scaffold. The attempted command line and launch error are in the scaffold report (`providerInvocation.commandLine` / `.standardError`)."
            [ name ]
        |> markToolDefect

    let scaffoldProviderWroteSddTree (paths: string list) =
        let ordered = paths |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.providerWroteSddTree"
            DiagnosticError
            None
            None
            $"Provider wrote into SDD-owned tree(s): {rendered}."
            "Fix the provider so it materializes only into the product target; SDD state was not modified. The provider's captured output is in the scaffold report (`providerInvocation.standardOutput` / `.standardError`)."
            ordered
        |> markToolDefect

    // 056 (FR-012): a `ReadFile`/`WriteFile` fault during the post-instantiation skill
    // fan-out. Finalizes as a non-success scaffold at exit 2 (the tool-defect class), so an
    // incomplete fan-out is never reported complete. Additive observability id only.
    let scaffoldMirrorFailed (paths: string list) =
        let ordered = paths |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.mirrorFailed"
            DiagnosticError
            None
            None
            $"The skill fan-out could not mirror the union into every agent root (failed path(s): {rendered})."
            "Resolve the filesystem issue (permissions / read-only target), then re-run scaffold; the fan-out was not completed and was not recorded as complete."
            ordered
        |> markToolDefect

    let scaffoldProvenanceMalformed path =
        create
            "scaffold.provenanceMalformed"
            DiagnosticError
            (scaffoldRef path)
            None
            $"`{path}` is unreadable scaffold provenance."
            "Repair or remove the malformed scaffold-provenance file before re-scaffolding or refreshing."
            [ path ]

    // Feature 052: describe how far the installed CLI is behind the minimum. Only
    // ever called when installed < minimum (compare = Some -1), so the most-significant
    // differing component's delta is positive. `Fsgg.Version.compare` yields only the
    // sign, so the "amount behind" is derived from the parsed component records (A1/U1).
    let private describeCliGap (installed: string) (minimum: string) =
        let unit (n: int) (name: string) =
            $"""behind by {n} {name} version{(if n = 1 then "" else "s")}"""

        match Fsgg.Version.tryParse installed, Fsgg.Version.tryParse minimum with
        | Some i, Some m ->
            if m.Major <> i.Major then
                unit (m.Major - i.Major) "major"
            elif m.Minor <> i.Minor then
                unit (m.Minor - i.Minor) "minor"
            else
                unit (m.Patch - i.Patch) "patch"
        | _ -> "behind by an unknown amount"

    let scaffoldCliBehindMinimum (installed: string) (minimum: string) =
        create
            "scaffold.cliBehindMinimum"
            DiagnosticInfo
            None
            None
            $"Installed fsgg-sdd {installed} is behind the provider-declared minimum coherent version {minimum} ({describeCliGap installed minimum}). Seeded skills / early-stage guidance from newer CLIs may be missing."
            "Upgrade the fsgg-sdd CLI, then re-run `fsgg-sdd init` to re-seed the fs-gg-sdd-* skills and .fsgg/early-stage-guidance.md (idempotent, no-clobber). Note: fsgg-sdd refresh does not re-seed."
            []

    let scaffoldProviderMinimumMalformed (rawMinimum: string) =
        create
            "scaffold.providerMinimumMalformed"
            DiagnosticWarning
            None
            None
            $"Provider-declared minimum coherent fsgg-sdd version `{rawMinimum}` is not a valid major.minor.patch version; the CLI coherence check was skipped and no minimum was recorded."
            "Fix the `minimumCliVersion` value in the provider registry (`.fsgg/providers.yml`) to a valid major.minor.patch version."
            []

    let scaffoldRepoInitSkippedExistingRepository () =
        create
            "scaffold.repoInitSkippedExistingRepository"
            DiagnosticInfo
            None
            None
            "Target is already inside a git work tree; repository initialization was skipped."
            "Left the existing repository untouched; no nested repo created."
            []

    let scaffoldRepoInitSkippedGitUnavailable () =
        create
            "scaffold.repoInitSkippedGitUnavailable"
            DiagnosticInfo
            None
            None
            "git is not available; repository initialization was skipped."
            "Install git and re-run, or run `git init` yourself; scaffold otherwise succeeded."
            []

    let scaffoldScriptsNotMadeExecutable (paths: string list) =
        let ordered = paths |> List.sort

        create
            "scaffold.scriptsNotMadeExecutable"
            DiagnosticInfo
            None
            None
            $"{List.length ordered} produced script(s) could not be made executable."
            "Set the executable bit manually (e.g. on a read-only or non-Unix filesystem)."
            ordered

    // Feature 053: `fsgg-sdd doctor` drift advisory. Warning severity so the read-only
    // report resolves to `succeededWithWarnings` when drift is present, while staying
    // non-blocking (doctor always exits 0).
    let doctorDriftDetected () =
        create
            "doctor.driftDetected"
            DiagnosticWarning
            None
            None
            "The scaffold has drifted from its coherent set (CLI behind the declared minimum and/or seeded artifacts missing)."
            "Run `fsgg-sdd upgrade` to reconcile each step interactively, or `fsgg-sdd upgrade --yes` non-interactively."
            []

    // Non-interactive `upgrade` without `--yes`: a user-input refusal (exit 1). Never
    // blocks on a prompt and makes zero writes (FR-012 / SC-004).
    let upgradeNonInteractiveNoYes () =
        create
            "upgrade.nonInteractiveNoYes"
            DiagnosticError
            None
            None
            "`fsgg-sdd upgrade` needs interactive confirmation, but input is not interactive and `--yes` was not passed; nothing was changed."
            "Re-run interactively, or pass `--yes` to apply the reconciliation without prompting."
            []

    // A confirmed CLI self-update process errored: a step defect (exit 2 via the typed
    // `IsToolDefect` bit); the reconciliation is reported incomplete (FR-013).
    let upgradeSelfUpdateFailed (exitCode: int) =
        create
            "upgrade.selfUpdateFailed"
            DiagnosticError
            None
            None
            $"The CLI self-update step failed (`dotnet tool update` exited {exitCode}); residual drift remains."
            "Update the fsgg-sdd tool manually (e.g. `dotnet tool update`), then re-run `fsgg-sdd doctor` to confirm."
            [ string exitCode ]
        |> markToolDefect

    // A confirmed re-pin/re-seed write failed: a step defect (exit 2); the
    // reconciliation is reported incomplete (FR-013 / SC-006).
    let upgradeStepFailed (stepId: string) =
        create
            "upgrade.stepFailed"
            DiagnosticError
            None
            None
            $"Reconciliation step '{stepId}' failed to apply; residual drift remains."
            "Inspect the failure, correct the environment, and re-run `fsgg-sdd upgrade`."
            [ stepId ]
        |> markToolDefect

    // Partial apply: one or more steps were declined and drift remains (US2-AC4). A
    // non-blocking warning (exit 0); a subsequent `doctor` still shows the drift.
    let upgradeResidualDrift (stepIds: string list) =
        let ordered = stepIds |> List.sort

        create
            "upgrade.residualDrift"
            DiagnosticWarning
            None
            None
            "Some reconciliation steps were skipped; the scaffold is not fully coherent."
            "Re-run `fsgg-sdd upgrade` and confirm the skipped step(s), or `fsgg-sdd doctor` to review the residual drift."
            ordered

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
        diagnostics
        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticError)
