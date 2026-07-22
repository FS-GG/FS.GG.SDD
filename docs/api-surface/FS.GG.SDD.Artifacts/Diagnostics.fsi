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
          // A tool/provider defect (as opposed to malformed user input). When a command
          // is blocked and any diagnostic carries this bit, the exit code escalates to 2
          // (the tool-defect class). Set at construction by the defect-producing
          // constructors via `markToolDefect`; replaces the old hand-maintained
          // `providerDefectIds` id set. Not serialized (round-tripped diagnostics carry
          // `false`); the exit-code decision only ever reads freshly-built diagnostics.
          IsToolDefect: bool
          // A stable, machine-readable defect sub-classifier owned by the producing parser (see
          // `DefectTags`), used to disambiguate a generic diagnostic id without prose-matching the
          // human `Message` across an assembly boundary. Set via `withDefectTag`; keyed on by
          // `LintEngine.classify`. NOT serialized (round-tripped diagnostics carry `None`).
          DefectTag: string option }

    /// Stable defect sub-classifier tags — the contract between the lifecycle parsers that stamp
    /// them (via `withDefectTag`) and `LintEngine.classify` that keys on them. Reword a diagnostic
    /// `Message` freely without dropping its lint class.
    [<RequireQualifiedAccess>]
    module DefectTags =
        /// A lifecycle artifact's required `---` front-matter block is missing a required field.
        [<Literal>]
        val FrontMatterIncomplete: string = "frontMatterIncomplete"

        /// A Functional-Requirements / Acceptance-Scenarios list item is missing its stable
        /// FR-###/AC-### id — the load-bearing coverage-line grammar defect.
        [<Literal>]
        val CoverageStableId: string = "coverageStableId"

    val severityValue: severity: DiagnosticSeverity -> string
    val severityRank: severity: DiagnosticSeverity -> int

    /// Mark a diagnostic as a tool/provider defect (escalates a blocked command to exit 2).
    val markToolDefect: diagnostic: Diagnostic -> Diagnostic

    /// Stamp a stable defect sub-classifier tag (see `DefectTags`) that downstream classification
    /// keys on instead of the message prose.
    val withDefectTag: tag: string -> diagnostic: Diagnostic -> Diagnostic

    /// The single predicate deciding whether a diagnostic signals a stale generated view.
    /// Keyed on the id (the only field that survives a work-model round-trip), so it is
    /// usable on diagnostics read back from a persisted work-model. Replaces the scattered
    /// `Id.IndexOf("stale")` substring test in the agent-refresh path.
    val signalsStaleView: diagnostic: Diagnostic -> bool

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

    /// A YAML syntax error in an authored artifact, positioned at the parser's mark.
    /// A non-positive `line` means the parser could not place the error; the diagnostic
    /// then carries the message without a source location.
    val malformedYaml: artifact: ArtifactRef -> message: string -> line: int -> column: int -> Diagnostic
    val deprecatedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val unsupportedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val futureSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val duplicateIdentifier: artifact: ArtifactRef -> id: string -> locations: SourceLocation list -> Diagnostic
    val unknownReference: artifact: ArtifactRef -> id: string -> correction: string -> Diagnostic
    val malformedReference: artifact: ArtifactRef -> kind: string -> value: string -> Diagnostic

    /// FS.GG.SDD#359 / #365: a cited artifact path that escapes the repository. Malformed user
    /// input (`IsToolDefect = false`), never a tool defect.
    val malformedArtifactPath: artifact: ArtifactRef -> value: string -> Diagnostic

    /// FS.GG.SDD#569 (feature 105): a `framework:` / `blocked-on-framework:` reference token that is
    /// not the `<PackageId>[@<version>]#<symbol>` grammar. Malformed user input, blocking.
    val malformedFrameworkReference: artifact: ArtifactRef -> value: string -> Diagnostic
    val requirementNotTyped: artifact: ArtifactRef -> id: string -> correction: string -> Diagnostic

    val workModelInconsistent:
        artifact: ArtifactRef -> message: string -> correction: string -> relatedIds: string list -> Diagnostic

    val missingChecklistBackReference: artifact: ArtifactRef -> resultId: string -> Diagnostic

    val proseStructuredMismatch: artifact: ArtifactRef -> message: string -> correction: string -> Diagnostic
    val staleGeneratedView: artifact: ArtifactRef -> message: string -> correction: string -> Diagnostic
    val missingGeneratedWorkModel: artifact: ArtifactRef -> expectedPath: string -> Diagnostic
    val malformedDigest: artifact: ArtifactRef -> value: string -> Diagnostic

    // Scaffold (`fsgg-sdd scaffold`) diagnostics. User-input ids resolve to exit 1;
    // provider-defect ids (`scaffold.providerFailed` / `providerUnavailable` /
    // `providerWroteSddTree`) carry the tool-defect class consumed at exit 2.
    val scaffoldProviderMissing: unit -> Diagnostic
    val scaffoldProviderUnknown: name: string -> Diagnostic

    val scaffoldProviderVersionUnsupported:
        name: string -> declaredVersion: string -> supportedRange: string -> Diagnostic

    val scaffoldProviderParamMissing: name: string -> missingKeys: string list -> Diagnostic

    /// The product name reduces to no valid F# identifier (feature 080, FR-009).
    /// User-input class → exit 1; blocks before provider invocation.
    val scaffoldNameUnrepresentable: name: string -> Diagnostic

    /// An author `--param` key that would inject a `dotnet new` built-in option (empty,
    /// dash-prefixed, or a reserved long-option name) rather than a template symbol.
    /// User-input class → exit 1; blocks before provider invocation.
    val scaffoldInvalidParamKey: keys: string list -> Diagnostic

    val scaffoldTargetCollision: paths: string list -> Diagnostic
    val scaffoldProviderEmpty: name: string -> Diagnostic
    val scaffoldProviderFailed: name: string -> exitCode: int -> Diagnostic
    val scaffoldProviderUnavailable: name: string -> Diagnostic
    val scaffoldProviderWroteSddTree: paths: string list -> Diagnostic
    val scaffoldMirrorFailed: paths: string list -> Diagnostic
    // 108 / ADR-0054: the scaffold-time driver materializer's fail-closed diagnostics.
    val scaffoldDriverVerifyFailed: ids: string list -> Diagnostic
    val scaffoldDriverPredicateUnevaluated: ids: string list -> Diagnostic
    val scaffoldDriverNamespaceCollision: ids: string list -> Diagnostic
    val scaffoldDriverManifestMalformed: message: string -> Diagnostic
    val scaffoldGameSkillVerifyFailed: ids: string list -> Diagnostic
    val scaffoldGameSkillPredicateUnevaluated: ids: string list -> Diagnostic
    val scaffoldGameSkillNamespaceCollision: ids: string list -> Diagnostic
    val scaffoldGameSkillManifestMalformed: message: string -> Diagnostic
    val scaffoldProvenanceMalformed: path: string -> Diagnostic

    // Feature 052 CLI-coherence advisories: both non-blocking (Info/Warning), so the
    // scaffold's outcome classification and exit code are unchanged vs an up-to-date run.
    val scaffoldCliBehindMinimum: installed: string -> minimum: string -> Diagnostic
    val scaffoldProviderMinimumMalformed: rawMinimum: string -> Diagnostic

    // Post-instantiation advisory facts (FR-010): non-fatal, never change the exit
    // code or flip the scaffold to failed/incomplete.
    val scaffoldRepoInitSkippedExistingRepository: unit -> Diagnostic
    val scaffoldRepoInitSkippedGitUnavailable: unit -> Diagnostic
    val scaffoldToolManifestSkippedExisting: path: string -> Diagnostic
    val scaffoldScriptsNotMadeExecutable: paths: string list -> Diagnostic

    // Remediation (`fsgg-sdd doctor` / `fsgg-sdd upgrade`) diagnostics (feature 053,
    // data-model E8). `doctor.driftDetected` is a non-blocking warning (doctor always
    // exits 0). `upgrade.nonInteractiveNoYes` is a user-input refusal (exit 1);
    // `upgrade.selfUpdateFailed` / `upgrade.stepFailed` are step defects escalated to
    // exit 2 via the typed `IsToolDefect` bit (`markToolDefect`); `upgrade.residualDrift`
    // is a non-blocking warning.
    val doctorDriftDetected: unit -> Diagnostic
    val upgradeNonInteractiveNoYes: unit -> Diagnostic
    val upgradeSelfUpdateFailed: exitCode: int -> Diagnostic
    val upgradeStepFailed: stepId: string -> Diagnostic
    val upgradeResidualDrift: stepIds: string list -> Diagnostic

    /// Feature 086: one or more committed `.fsi` surface baselines are missing or byte-differing
    /// from the authored source signature. `DiagnosticError` — `fsgg-sdd surface --check` exits 1.
    val surfaceDrift: missingCount: int -> driftedCount: int -> paths: string list -> Diagnostic

    /// Feature 086: committed baselines with no corresponding source `.fsi`. `DiagnosticWarning`
    /// (advisory, exit 0) — never auto-removed in this version.
    val surfaceOrphanBaseline: paths: string list -> Diagnostic

    /// FS-GG/FS.GG.SDD#185: a `surface` root `--param` (`sourceRoot`/`baselineRoot`) resolves outside
    /// the workspace root — it is absolute, or carries a `..` segment. `DiagnosticError` —
    /// `fsgg-sdd surface` exits 1 and plans no effect of any kind against the escaping root.
    /// `value` is the raw param, never the normalized one (normalization would hide a leading `/`).
    val surfaceRootEscape: param: string -> value: string -> Diagnostic

    /// Feature 094 (FS-GG/.github ADR-0025 reconcile step 3a): a classified shipped-surface mutation
    /// implies a coherent-set version bump on the workspace's version axis. `DiagnosticWarning` —
    /// advisory, never changes the exit code (FR-008/FR-013), because SDD cannot see the previously
    /// *published* version and so cannot prove the bump was not already applied in this change.
    /// Emitted iff `requiredBump` is `major` or `minor`. When the axis is unresolved the message
    /// names the `--param` override that would resolve it (FR-010). Detect-and-remediate: the axis
    /// is never written (ADR-0009).
    val surfaceVersionBumpRequired:
        verdict: string ->
        axisFile: string ->
        axisProperty: string ->
        axisState: string ->
        currentVersion: string option ->
        requiredBump: string ->
        suggestedVersion: string option ->
            Diagnostic

    /// Feature 105/109 (ADR-0004 D2): a required `dependency-surface` capture is missing or
    /// disagrees with the package's freshly-read real surface. `DiagnosticError` so
    /// `dependency-surface --check` exits 1 and fails CI; `--update` reconciles instead.
    val dependencySurfaceDrift: packages: string list -> Diagnostic

    /// Feature 105, Phase 2 (ADR-0004 D3): a package's real surface could not be read (not restored,
    /// or the assembly could not be loaded), so drift cannot be judged. Advisory
    /// (`DiagnosticWarning`, exit 0) — "could not look" is never a negative verdict (ADR-0002 /
    /// #266). `packages` are the affected `<PackageId>@<Version>` ids.
    val dependencySurfaceUnavailable: packages: string list -> Diagnostic

    /// Feature 105, Phase 2 (FS.GG.SDD#185 discipline): the `dependency-surface` `--param
    /// baselineRoot` resolves outside the workspace root. `DiagnosticError` (exit 1); planning is
    /// refused wholesale so nothing outside the root is read or written. `value` is the RAW param.
    val dependencySurfaceRootEscape: value: string -> Diagnostic

    /// The canonical diagnostic ordering — and the set seam. Structurally identical diagnostics
    /// are collapsed to one (#193): a diagnostic with both a prereq producer and a downstream
    /// backstop producer would otherwise appear twice, indistinguishable in every projection.
    val sort: diagnostics: Diagnostic list -> Diagnostic list
    val hasBlocking: diagnostics: Diagnostic list -> bool
