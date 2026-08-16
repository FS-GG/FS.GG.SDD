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

    /// FS.GG.SDD#869. A `tasks.yml` task requires an evidence id that `evidence.yml` does not
    /// declare.
    ///
    /// This is the ONE reference in `tasks.yml` that points DOWNSTREAM — at an artifact a LATER
    /// lifecycle stage authors. The other three (requirements, decisions, task dependencies) point
    /// UPSTREAM at artifacts that already exist, so an unresolved edge there is a genuine
    /// inconsistency with no later stage to close it, and it rightly blocks with
    /// `unknownReference`. An unresolved edge HERE is an INCOMPLETE lifecycle, not an inconsistent
    /// one, and blocking it deadlocked the lifecycle outright: the work model refused to derive
    /// until `evidence` had run, `evidence` refused to run until `analyze` was
    /// `implementationReady`, and `analyze` could not be `implementationReady` while the work model
    /// would not derive. No ordering of the documented commands escaped it.
    ///
    /// Warning severity, so `WorkModel.blockingDiagnostics` does not pick it up and the model
    /// derives. That relocates no gate and deletes none: an obligation that is never declared is
    /// still refused by `evidence.missingRequiredEvidence` and by the `verify`/`ship`
    /// unmet-obligation checks, each of which names the same id. The check that is dropped here is
    /// a redundant, coarser copy that named only the file.
    ///
    /// The artifact is `evidence.yml` — the file that must change — not the `tasks.yml` that cites
    /// the id, because a diagnostic names where the failure LIVES, not where it was DETECTED
    /// (`.github#266`). `citedBy` carries the detecting artifact so neither fact is lost.
    let undeclaredEvidenceObligation artifact (id: string) (citedBy: string) =
        create
            "undeclaredEvidenceObligation"
            DiagnosticWarning
            (Some artifact)
            None
            $"Evidence obligation '{id}' is required by '{citedBy}' but is not declared in '{artifact.Path}'."
            "Run `fsgg-sdd evidence --work <id>` to scaffold the missing declaration, or drop the id from the task's requiredEvidence."
            [ id; citedBy ]

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

    // 108 / ADR-0054 (FR-003): a delivered driver body failed its content-addressed check (its
    // `sha256` disagreed with the manifest, or its body was absent). Fail closed — the driver is
    // not written. A corrupt embedded set is a CLI build/packaging defect (the drift guard,
    // FR-008, is the release-time gate), so this is the tool-defect class.
    let scaffoldDriverVerifyFailed (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.driverVerifyFailed"
            DiagnosticError
            None
            None
            $"Driver skill(s) failed the content-addressed verify and were not materialized: {rendered}."
            "The embedded driver bytes do not match their manifest sha256 (a CLI packaging defect). Rebuild/republish the CLI from a coherent `FS.GG.Drivers` pin; the driver was not written and was not recorded as materialized."
            ordered
        |> markToolDefect

    // 108 / ADR-0054 (FR-004): a driver row's `materializes-when` predicate is a form the
    // materializer does not evaluate, so the row is skipped (never materialized by default).
    // Non-blocking advisory — the scaffold otherwise succeeds.
    let scaffoldDriverPredicateUnevaluated (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.driverPredicateUnevaluated"
            DiagnosticWarning
            None
            None
            $"Driver skill(s) skipped — their `materializes-when` predicate was not evaluable by this CLI: {rendered}."
            "Upgrade `fsgg-sdd` to a version that understands the predicate, or adjust the delivered manifest; the row was skipped (fail-closed), not materialized."
            ordered

    // 108 / ADR-0054 (FR-007): a driver row's id collides with the reserved seeded `fs-gg-sdd-*`
    // namespace. Rejected so a driver can never shadow the SDD-owned skeleton. Tool-defect class.
    let scaffoldDriverNamespaceCollision (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.driverNamespaceCollision"
            DiagnosticError
            None
            None
            $"Driver skill(s) rejected — their id collides with the reserved seeded `fs-gg-sdd-*` namespace: {rendered}."
            "A driver skill may not shadow an SDD-owned seeded skill. Fix the delivered `FS.GG.Drivers` manifest to use a non-reserved id; the row was not materialized."
            ordered
        |> markToolDefect

    // 108 / ADR-0054: the embedded driver manifest could not be parsed — a CLI packaging defect.
    let scaffoldDriverManifestMalformed (message: string) =
        create
            "scaffold.driverManifestMalformed"
            DiagnosticError
            None
            None
            $"The embedded driver manifest is malformed: {message}."
            "Rebuild/republish the CLI from a coherent `FS.GG.Drivers` pin; no driver was materialized."
            [ message ]
        |> markToolDefect

    // ADR-0063 / FS.GG.SDD#623 (FR-003): a delivered owner-skill body failed its content-addressed
    // check (its `sha256` disagreed with the manifest, or its body was absent). Fail closed — the
    // skill is not written. A corrupt embedded set is a CLI build/packaging defect (the package's
    // stage-time drift guard is the release gate), so this is the tool-defect class.
    let scaffoldGameSkillVerifyFailed (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.gameSkillVerifyFailed"
            DiagnosticError
            None
            None
            $"Owner-sourced skill(s) failed the content-addressed verify and were not materialized: {rendered}."
            "The embedded owner-skill bytes do not match their manifest sha256 (a CLI packaging defect). Rebuild/republish the CLI from a coherent the owner-skills package pin; the skill was not written and was not recorded as materialized."
            ordered
        |> markToolDefect

    // ADR-0063 / FS.GG.SDD#623 (FR-004): a owner-skill row's `materializes-when` predicate is a form
    // the materializer does not evaluate, so the row is skipped (never materialized by default).
    // Non-blocking advisory — the scaffold otherwise succeeds.
    let scaffoldGameSkillPredicateUnevaluated (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.gameSkillPredicateUnevaluated"
            DiagnosticWarning
            None
            None
            $"Owner-sourced skill(s) skipped — their `materializes-when` predicate was not evaluable by this CLI: {rendered}."
            "Upgrade `fsgg-sdd` to a version that understands the predicate, or adjust the delivered manifest; the row was skipped (fail-closed), not materialized."
            ordered

    // ADR-0063 / FS.GG.SDD#623: a owner-skill row's id collides with the reserved seeded
    // `fs-gg-sdd-*` namespace. Rejected so a delivered skill can never shadow the SDD-owned
    // skeleton. Tool-defect class.
    let scaffoldGameSkillNamespaceCollision (ids: string list) =
        let ordered = ids |> List.sort
        let rendered = String.concat ", " ordered

        create
            "scaffold.gameSkillNamespaceCollision"
            DiagnosticError
            None
            None
            $"Owner-sourced skill(s) rejected — their id collides with the reserved seeded `fs-gg-sdd-*` namespace: {rendered}."
            "An owner-sourced skill may not shadow an SDD-owned seeded skill. Fix the delivered the owner-skills package manifest to use a non-reserved id; the row was not materialized."
            ordered
        |> markToolDefect

    // ADR-0063 / FS.GG.SDD#623: the embedded owner-skill manifest could not be parsed — a CLI
    // packaging defect.
    let scaffoldGameSkillManifestMalformed (message: string) =
        create
            "scaffold.gameSkillManifestMalformed"
            DiagnosticError
            None
            None
            $"The embedded owner-skill manifest is malformed: {message}."
            "Rebuild/republish the CLI from a coherent the owner-skills package pin; no owner-sourced skill was materialized."
            [ message ]
        |> markToolDefect

    // ADR-0063 tail / FS.GG.SDD#739: `ProductSkillManifest.amend` DECLINED to rewrite the
    // provider-shipped product `skill-manifest.json`, so the driver + owner-sourced skills this
    // scaffold materialized are not declared in it and the consumer skill-union gate will read them
    // as `[dangling]`.
    //
    // WHY THIS EXISTS AT ALL. Refusing is the right half — a manifest whose header asserts a
    // completeness its rows do not carry is the defect #739 is named for, and a wrong document is
    // worse than a missing amend. SILENT was the wrong half: the call site mapped the refusal to
    // `[], skillDigests` and emitted nothing, so an incomplete union left the tool with no trace at
    // all and surfaced two repos away, as a red composition gate with no local cause.
    //
    // WARNING, DELIBERATELY, NOT ERROR. A `DiagnosticError` is `Blocked` (`ReportAssembly.outcome`)
    // and exits 1/2, which would red a scaffold that succeeded at everything it owns — the tree is
    // complete, only the union is not — on a path that is green today. #739 asks for the fact to be
    // SAID, not for a new blocking policy; the hard stop for an incomplete union already exists
    // downstream, in the consumer gate. So this states the cause, names the undeclared ids, and
    // leaves the exit code alone.
    let scaffoldProductManifestAmendRefused (path: string) (reason: string) (remedy: string) (details: string list) =
        create
            "scaffold.productManifestAmendRefused"
            DiagnosticWarning
            (scaffoldRef path)
            None
            $"`{path}` was left unamended — {reason}. The skills this scaffold materialized are therefore not declared in it, and a consumer skill-union check will read them as dangling."
            remedy
            details

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

    // ── The read edge's third state (FS.GG.SDD#745, decision FS.GG.SDD#754) ───────────────────
    //
    // Before #754 the core had two read states — bytes, or nothing — and *nothing* meant ABSENT.
    // A file that exists and cannot be read collapsed into the absent branch, and several verdict
    // folds take their candidate set from a directory listing but their comparison from the read,
    // so an absent subject is not a finding: it is silently NOT CHECKED, i.e. a pass. `ReadResult`
    // gives the third state a name; these three diagnostics are how it reaches an operator.
    //
    // Three, not one, because the three situations have different blocking polarity and a single
    // id would have to pick one of them and be wrong about the other two.
    //
    // FS.GG.SDD#748 adds a FOURTH, and on a different axis from that one: `undecodableFile` shares
    // `unreadableFile`'s polarity exactly. It is separate because the two findings send the
    // operator somewhere different — the same reason `unlistableDirectory` is separate — and the
    // remedy is the whole of it. `chmod +r` is not merely unhelpful for a file whose bytes are not
    // valid UTF-8; it is an instruction the operator can carry out in full and still be looking at
    // an unchanged failure.

    /// A file that EXISTS could not be read (permissions, an IO fault, a device error). The
    /// per-file fact, emitted at the effect edge for every refused read in every lane, so the
    /// operator always learns WHICH file and WHY.
    ///
    /// `DiagnosticWarning`, deliberately: the read edge does not know what the lane was going to
    /// do with the bytes. Making this alone blocking would make one unreadable file fatal to
    /// `doctor`, which is documented read-only and exit 0 — the alternative #754 considered and
    /// rejected. The BLOCK belongs to the verdict fold that was relying on the bytes
    /// (`unreadableSubject`), which is emitted beside this one.
    ///
    /// Not a tool defect. Nothing about the tool is broken; exit 2 would say it is.
    let unreadableFile (path: string) (reason: string) =
        create
            "unreadableFile"
            DiagnosticWarning
            None
            None
            $"`{path}` exists but could not be read: {reason}"
            $"Restore read access to `{path}` (e.g. `chmod +r`) and re-run. A file the tool could not read is never counted as checked and never treated as unchanged."
            [ path ]

    /// A file that exists and OPENS could not be DECODED: its bytes are not a valid body in any
    /// encoding the read seam selects (FS.GG.SDD#748, ADR-0014 §Decision 3 clause (c)).
    /// `byteOffset` is where the first invalid sequence begins, counted within the file with the
    /// preamble included, exactly as `SkillMirror.BodyRefusalReason.NotDecodable` reports it.
    ///
    /// The finding this replaces was not a diagnostic at all — it was a PASS. `File.ReadAllText`
    /// substitutes `U+FFFD` for a sequence it cannot decode and returns a string, so the read
    /// succeeded, the digest was taken over the substitution, and any two files whose invalid bytes
    /// substituted alike shared one digest. FS.GG.SDD#737 gave the library a `decodeBody` that
    /// refuses instead; this is the caller's half, and it is the half that can name the file
    /// (#737 AC2 — the library never sees one).
    ///
    /// `DiagnosticWarning`, and the block belongs to `unreadableSubject`, for exactly the reasons
    /// `unreadableFile` is: the read edge does not know what the lane meant to do with the bytes,
    /// and one such file must not be fatal to `doctor` (read-only, exit 0 — decision #754).
    ///
    /// Distinct from `unreadableFile` because the REPAIR is disjoint from it. An operator told to
    /// `chmod +r` a file whose mode is already `0644` has been sent to fix something that is not
    /// broken; the file needs re-encoding, and nothing about its permissions will ever say so.
    ///
    /// Not a tool defect. A mis-encoded file in the workspace is an authoring accident the operator
    /// can fix, and exit 2 would accuse the tool of being broken over it.
    let undecodableFile (path: string) (byteOffset: int) =
        create
            "undecodableFile"
            DiagnosticWarning
            None
            None
            $"`{path}` exists but its bytes are not a decodable body: the first invalid sequence begins at byte offset {byteOffset}."
            $"Re-encode `{path}` as UTF-8 (or a well-formed UTF-16/UTF-32 with its BOM) and re-run. A body the tool could not decode is never hashed, never counted as checked, and never treated as unchanged — decoding it with replacement characters would give two different files one digest."
            [ path ]

    /// A directory tree was listed and the listing is INCOMPLETE: one or more directories beneath
    /// `root` could not be opened, so what lies under them was never observed (FS.GG.SDD#743).
    /// `entries` is `(path, reason)` per skipped directory; `RelatedIds` are those paths, sorted.
    ///
    /// The sibling of `unreadableFile`, and `DiagnosticWarning` for the same reason: the read edge
    /// does not know what the lane meant to do with the listing, and making it fatal here would
    /// wedge `doctor` — documented read-only and exit 0 — on one mode bit. The BLOCK is
    /// `unreadableSubject`, emitted by the verdict fold that consumed the listing, which sees the
    /// skipped directories through `ReadResult.Truncated`.
    ///
    /// Distinct from `unreadableFile` because the finding is genuinely different and so is the
    /// repair: the entries that WERE listable are still reported on, and a directory needs
    /// traversal (`+rx`), not merely read, to be listed. Saying "this file could not be read"
    /// about a partially-listed tree would misdescribe both halves.
    ///
    /// Not a tool defect. Before #743 this landed in `interpret`'s outer handler as `toolDefect`
    /// at exit 2 — the tool accused of being broken over a permissions accident.
    let unlistableDirectory (root: string) (entries: (string * string) list) =
        let ordered = entries |> List.distinctBy fst |> List.sortBy fst

        let detail =
            ordered
            |> List.map (fun (path, reason) -> $"`{path}` ({reason})")
            |> String.concat "; "

        create
            "unlistableDirectory"
            DiagnosticWarning
            None
            None
            $"`{root}` was listed only in part: {List.length ordered} director(y|ies) beneath it could not be opened — {detail}"
            $"Restore traversal access (e.g. `chmod +rx`) to the listed director(y|ies) and re-run. What could be listed is still reported on; what could not is never counted as checked and never treated as unchanged."
            (ordered |> List.map fst)

    /// One or more subjects a VERDICT was responsible for could not be read, so the verdict cannot
    /// be coherent. `DiagnosticError` (exit 1) — this is `.github#266` on the read edge: *"I could
    /// not evaluate this"* is not *"I evaluated it and it passed."*
    ///
    /// Emitted by the gate lanes (`surface --check/--update`, `dependency-surface`) alongside the
    /// per-file `unreadableFile` warnings, which carry the reasons. `RelatedIds` are the paths.
    ///
    /// Not a tool defect: a permissions accident in the workspace is an environment fault the
    /// operator can fix, not a broken tool, so it must not be laundered into exit 2.
    let unreadableSubject (command: string) (paths: string list) =
        let ordered = paths |> List.sort

        create
            "unreadableSubject"
            DiagnosticError
            None
            None
            $"`{command}` could not read {List.length ordered} of the file(s) it must compare, so its verdict cannot be coherent."
            $"Restore read access to the listed file(s) and re-run `fsgg-sdd {command}`. Until then the verdict is withheld rather than reported as a pass."
            ordered

    /// A write was refused because its DESTINATION could not be read. The tool decides whether it
    /// may replace an existing file from that file's current bytes (`canOverwrite`), so without
    /// them the decision is undecidable and the only fail-closed answer is to refuse.
    ///
    /// `DiagnosticError` (exit 1) so the run blocks rather than reporting a write that did not
    /// happen — but explicitly NOT a tool defect, which is the whole point of the separate id:
    /// before #745 this arm threw into the interpreter's outer handler and surfaced as `toolDefect`
    /// at exit 2, i.e. `upgrade`/`charter` over a mode-000 target accused the tool of being broken.
    let unreadableWriteTarget (path: string) (reason: string) =
        create
            "unreadableWriteTarget"
            DiagnosticError
            None
            None
            $"Refusing to write `{path}`: it exists and could not be read ({reason}), so whether replacing it is safe cannot be decided."
            $"Restore read access to `{path}` (e.g. `chmod +r`) and re-run. The tool never replaces bytes it could not read."
            [ path ]

    /// The `undecodableFile` sibling of `unreadableWriteTarget` (FS.GG.SDD#748): a write was refused
    /// because its DESTINATION exists and its bytes do not DECODE, so `canOverwrite` — which decides
    /// from the destination's current text — has no current text to decide from.
    ///
    /// Separate from `unreadableWriteTarget` for the reason `undecodableFile` is separate from
    /// `unreadableFile`, and the write edge is where getting it wrong costs the most: the operator is
    /// being told the tool will not overwrite their file, so the one thing the message must get right
    /// is what to do about it. `chmod +r` against a file whose mode is already `0644` is a complete
    /// instruction that changes nothing, and the run refuses identically on the next attempt.
    ///
    /// `DiagnosticError` (exit 1), never a tool defect — the same class as its sibling.
    let undecodableWriteTarget (path: string) (byteOffset: int) =
        create
            "undecodableWriteTarget"
            DiagnosticError
            None
            None
            $"Refusing to write `{path}`: it exists and its bytes are not a decodable body (first invalid sequence at byte offset {byteOffset}), so whether replacing it is safe cannot be decided."
            $"Re-encode `{path}` as UTF-8 (or a well-formed UTF-16/UTF-32 with its BOM) and re-run. The tool never replaces bytes it could not decode — comparing against a decoder's `U+FFFD` substitution would make a file that differs look unchanged."
            [ path ]

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
    // package's real restored surface, or an authored target has no committed capture.
    // `DiagnosticError` so `dependency-surface --check` exits 1 and fails CI; `--update` reconciles
    // instead. RelatedIds carry the affected `<Pkg>@<ver>` ids.
    let dependencySurfaceDrift (packages: string list) =
        create
            "dependencySurface.drift"
            DiagnosticError
            None
            None
            $"{List.length packages} required dependency-surface capture(s) are missing or disagree with the package's real restored surface."
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
