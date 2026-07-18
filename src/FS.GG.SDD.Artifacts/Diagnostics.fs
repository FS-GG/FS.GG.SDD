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
          IsToolDefect: bool
          // A stable, machine-readable defect sub-classifier owned by the producing parser,
          // used to disambiguate a generic diagnostic id (e.g. `workModelInconsistent`, which
          // covers several distinct grammar defects) without cross-assembly prose-matching on
          // the human `Message`. Set at construction via `withDefectTag`; keyed on by
          // `LintEngine.classify`. Like `IsToolDefect`, NOT serialized (round-tripped
          // diagnostics carry `None`) — every consumer classifies freshly-built diagnostics.
          DefectTag: string option }

    // Stable defect sub-classifier tags (see `Diagnostic.DefectTag`). These are the contract
    // between the lifecycle parsers that stamp them and `LintEngine.classify` that keys on them;
    // reword a diagnostic's `Message` freely without dropping its lint class.
    [<RequireQualifiedAccess>]
    module DefectTags =
        /// A lifecycle artifact's required `---` front-matter block is missing a required field.
        [<Literal>]
        let FrontMatterIncomplete = "frontMatterIncomplete"

        /// A Functional-Requirements / Acceptance-Scenarios list item is missing its stable
        /// FR-###/AC-### id — the load-bearing coverage-line grammar defect.
        [<Literal>]
        let CoverageStableId = "coverageStableId"

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
          IsToolDefect = false
          DefectTag = None }

    let markToolDefect (diagnostic: Diagnostic) = { diagnostic with IsToolDefect = true }

    /// Stamp a stable defect sub-classifier tag (see `DefectTags`) the producing parser owns
    /// and downstream classification keys on — decoupling the lint class from the message prose.
    let withDefectTag (tag: string) (diagnostic: Diagnostic) =
        { diagnostic with DefectTag = Some tag }

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

    /// A YAML syntax error in an authored artifact, positioned at the parser's mark.
    /// A parser that cannot place the error (YamlDotNet leaves the mark at 0) reports
    /// the message without a location rather than pointing at a line that does not exist.
    let malformedYaml artifact (message: string) (line: int) (column: int) =
        let located = line > 0

        let position = if located then $" at line {line}, column {column}" else ""

        create
            "malformedYaml"
            DiagnosticError
            (Some artifact)
            (if located then
                 Some
                     { Line = Some line
                       Column = Some column }
             else
                 None)
            $"Artifact '{artifact.Path}' has a YAML syntax error{position}: {message}"
            "Correct the YAML syntax at the reported position; the document could not be parsed."
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

    /// FS.GG.SDD#359 / #365. A cited artifact path that is not repository-relative. This is MALFORMED
    /// USER INPUT — the author wrote the path — so it is a `DiagnosticError` naming the offending
    /// value (`create` stamps `IsToolDefect = false`), not an escaped `ArgumentException` reported to
    /// them as a bug in SDD.
    let malformedArtifactPath artifact (value: string) =
        create
            "malformedArtifactPath"
            DiagnosticError
            (Some artifact)
            None
            $"Cited artifact path '{value}' is not repository-relative — it must stay inside the repository and contain no '..' segment."
            "Cite the artifact by its repository-relative path, or remove the reference. A path outside the workspace proves nothing and is never read."
            [ value ]

    /// FS.GG.SDD#569 (feature 105). A `framework:` / `blocked-on-framework:` reference whose token
    /// is not the well-formed `<PackageId>[@<version>]#<symbol>` grammar. This is MALFORMED USER
    /// INPUT — the author wrote the token — so it is a `DiagnosticError` naming the offending value
    /// (never a silent non-match, which would let a mis-typed reference read as "no reference at all"
    /// and pass plan-time resolution unchecked, FR-003).
    let malformedFrameworkReference artifact (value: string) =
        create
            "malformedFrameworkReference"
            DiagnosticError
            (Some artifact)
            None
            $"Framework reference '{value}' is not well-formed — expected '<PackageId>[@<version>]#<symbol>'."
            "Write the reference as '<PackageId>[@<version>]#<symbol>' (version optional; it defaults to the pinned package version), or remove it."
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

    // Feature 081 (#144): a checklist review result missing its [CHK:CHK-###] item back-reference
    // is a body/back-reference defect, NOT a front-matter defect — it gets its own id so the
    // diagnostic names its real cause instead of misdirecting to front matter.
    let missingChecklistBackReference artifact resultId =
        create
            "missingChecklistBackReference"
            DiagnosticError
            (Some artifact)
            None
            $"Checklist review result {resultId} is missing its [CHK:CHK-###] item back-reference."
            "Add [CHK:CHK-###] naming the checklist item this review result covers."
            [ resultId ]

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

    let scaffoldNameUnrepresentable (name: string) =
        create
            "scaffold.nameUnrepresentable"
            DiagnosticError
            (scaffoldRef ".fsgg/providers.yml")
            None
            $"Product name '{name}' contains no character valid in an F# identifier, so no namespace can be derived."
            "Choose a product name containing at least one letter, digit, or underscore."
            [ name ]

    let scaffoldInvalidParamKey (keys: string list) =
        let ordered = keys |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.invalidParamKey"
            DiagnosticError
            None
            None
            $"`--param` key(s) would inject a `dotnet new` built-in option instead of forwarding a template symbol: {rendered}."
            "Rename each parameter to a non-empty template symbol name that is not dash-prefixed and does not shadow a `dotnet new` option (e.g. not `force`, `output`, `name`, `language`)."
            ordered

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

    let scaffoldToolManifestSkippedExisting (path: string) =
        create
            "scaffold.toolManifestSkippedExisting"
            DiagnosticInfo
            None
            None
            "A dotnet tool manifest already exists; the fsgg-sdd pin was not written."
            "Left the existing manifest untouched; add or update the fsgg-sdd entry yourself if it is absent or stale."
            [ path ]

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

    // Feature 086: a committed `.fsi` surface baseline is missing or has drifted from its authored
    // source signature. A `DiagnosticError` so `fsgg-sdd surface --check` exits 1 and fails CI;
    // `--update` never emits it (it reconciles instead). RelatedIds carry the offending paths.
    let surfaceDrift (missingCount: int) (driftedCount: int) (paths: string list) =
        create
            "surface.drift"
            DiagnosticError
            None
            None
            $"API-surface baselines have drifted: {missingCount} missing, {driftedCount} differing from the authored `.fsi`."
            "Run `fsgg-sdd surface --update` to refresh the `docs/api-surface/**` baselines, then commit."
            (paths |> List.sort)

    // Feature 086: a baseline `.fsi` under the baseline root has no corresponding authored source
    // signature. Advisory (`DiagnosticWarning`, exit 0) in both modes — this version has no delete
    // effect, so removing a stale baseline stays a manual author action. RelatedIds carry the paths.
    let surfaceOrphanBaseline (paths: string list) =
        create
            "surface.orphanBaseline"
            DiagnosticWarning
            None
            None
            $"{List.length paths} committed API-surface baseline(s) have no corresponding source `.fsi`."
            "Remove the stale baseline file(s) under the baseline root if the source was intentionally deleted."
            (paths |> List.sort)

    // FS-GG/FS.GG.SDD#185: a `surface` root `--param` resolves outside the workspace root — an
    // absolute path, or one with a `..` segment. `DiagnosticError` (exit 1): `surface` documents both
    // roots as workspace-contained, `--check` as strictly read-only, and `--update` as writing only
    // under the baseline root. Blocking is the only way those statements are true. Planning is
    // refused wholesale — no read, no enumerate, no write — so nothing outside the root is ever
    // opened. One diagnostic per offending param, so both are named when both escape.
    //
    // ⚠ `value` is the RAW param, never `normalizeRelativePath value`: normalization ends in
    // `.TrimStart('/')`, which would render `/etc/passwd` as the innocuous `etc/passwd` in the very
    // message meant to name the escape.
    let surfaceRootEscape (param: string) (value: string) =
        create
            "surface.rootEscape"
            DiagnosticError
            None
            None
            $"`--param {param}={value}` resolves outside the workspace root. `surface` reads and writes only within the root it was given."
            $"Point `{param}` at a path inside the workspace root — no leading `/` and no `..` segment."
            []

    // Feature 094 (FS-GG/.github ADR-0025 reconcile step 3a): a classified shipped-surface mutation
    // implies a coherent-set version bump. `DiagnosticWarning`, never blocking (FR-008/FR-013): SDD
    // reads the *declared* axis, not the previously *published* version, so it cannot prove the bump
    // was not already applied in this change. The message is therefore a prompt the operator
    // confirms, not an accusation (FR-009). When the axis is unresolved, the remediation names both
    // `--param` overrides that would resolve it (FR-010) — the diagnostic cannot tell a missing file
    // from a missing property, so it offers both rather than guessing.
    let surfaceVersionBumpRequired
        (verdict: string)
        (axisFile: string)
        (axisProperty: string)
        (axisState: string)
        (currentVersion: string option)
        (requiredBump: string)
        (suggestedVersion: string option)
        =
        let axis = $"`{axisFile}:{axisProperty}`"

        let message, remediation =
            match currentVersion, suggestedVersion with
            | Some current, Some suggested ->
                $"Shipped-surface mutation classified `{verdict}`. The coherent-set version axis {axis} reads `{current}`; a {requiredBump} bump to `{suggested}` is required — unless it is already applied in this change.",
                $"Set `{axisProperty}` to `{suggested}` in `{axisFile}` if the bump is not already applied in this change. `fsgg-sdd` does not write the version axis (ADR-0009: detect-and-remediate)."
            | _ ->
                $"Shipped-surface mutation classified `{verdict}`. A {requiredBump} bump of the coherent-set version is required, but the version axis {axis} could not be resolved (`{axisState}`).",
                $"Point `fsgg-sdd surface` at the axis with `--param versionAxisFile=<file>` and `--param versionAxisProperty=<property>`, then apply the {requiredBump} bump yourself. `fsgg-sdd` does not write the version axis (ADR-0009: detect-and-remediate)."

        create "surface.versionBumpRequired" DiagnosticWarning None None message remediation []

    // Feature 105, Phase 2 (ADR-0004 D2): a committed dependency-surface capture disagrees with the
    // package's real restored surface. `DiagnosticError` so `dependency-surface --check` exits 1 and
    // fails CI; `--update` reconciles instead. RelatedIds carry the drifted `<Pkg>@<ver>` ids.
    let dependencySurfaceDrift (packages: string list) =
        create
            "dependencySurface.drift"
            DiagnosticError
            None
            None
            $"{List.length packages} committed dependency-surface capture(s) disagree with the package's real restored surface."
            "Run `fsgg-sdd dependency-surface --update` to refresh the `docs/dependency-surface/**` captures from the restored packages, then commit."
            (packages |> List.sort)

    // Feature 105, Phase 2 (ADR-0004 D3): a package's real surface could not be read (not restored,
    // or the assembly could not be loaded). Advisory (`DiagnosticWarning`, exit 0) — "could not
    // look" is never a negative verdict (ADR-0002 / #266). RelatedIds carry the affected ids.
    let dependencySurfaceUnavailable (packages: string list) =
        create
            "dependencySurface.unavailable"
            DiagnosticWarning
            None
            None
            $"{List.length packages} dependency-surface package(s) could not be read from the restored surface; drift was not judged for them."
            "Restore the package(s) (a normal `dotnet restore`/build) so the real surface is present, then re-run `fsgg-sdd dependency-surface`."
            (packages |> List.sort)

    // Feature 105, Phase 2 (FS.GG.SDD#185 discipline): `--param baselineRoot` resolves outside the
    // workspace root (absolute, or a `..` segment). `DiagnosticError` (exit 1). `value` is the RAW
    // param, never normalized — normalization strips a leading `/` and would hide the escape.
    let dependencySurfaceRootEscape (value: string) =
        create
            "dependencySurface.rootEscape"
            DiagnosticError
            None
            None
            $"`--param baselineRoot={value}` resolves outside the workspace root. `dependency-surface` reads and writes only within the root it was given."
            "Point `baselineRoot` at a path inside the workspace root — no leading `/` and no `..` segment."
            []

    let locationKey location =
        match location with
        | Some loc -> defaultArg loc.Line 0, defaultArg loc.Column 0
        | None -> 0, 0

    // #193: the canonical ordering seam is also the *set* seam. A diagnostic list is a set:
    // two structurally identical diagnostics are indistinguishable in every projection (json,
    // text, rich, analysis findings), so a second copy carries no information — it only inflates
    // the report's `diagnostics` count and mints a phantom second `AF###` finding in
    // `analysis.json`. Duplicates arise wherever a diagnostic has both a prereq producer and a
    // downstream backstop producer (`missingDisposition` is emitted by the `tasks`-stage
    // validation *and* by `analyze`'s backstop). Deduping here, at the single seam every
    // producer already funnels through, closes the class rather than the instance.
    //
    // `List.distinct` (full structural equality), never a key projection: `IsToolDefect` is not
    // serialized but does escalate a blocked command's exit code to 2, so collapsing two
    // diagnostics that differ *only* there would make the exit code depend on which copy
    // survived. Full equality drops only copies that are identical in every respect.
    let sort diagnostics =
        diagnostics
        |> List.distinct
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
