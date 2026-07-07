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
          IsToolDefect: bool }

    val severityValue: severity: DiagnosticSeverity -> string
    val severityRank: severity: DiagnosticSeverity -> int

    /// Mark a diagnostic as a tool/provider defect (escalates a blocked command to exit 2).
    val markToolDefect: diagnostic: Diagnostic -> Diagnostic

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
    val deprecatedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val unsupportedSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val futureSchemaVersion: artifact: ArtifactRef -> value: string -> Diagnostic
    val duplicateIdentifier: artifact: ArtifactRef -> id: string -> locations: SourceLocation list -> Diagnostic
    val unknownReference: artifact: ArtifactRef -> id: string -> correction: string -> Diagnostic
    val malformedReference: artifact: ArtifactRef -> kind: string -> value: string -> Diagnostic
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

    val scaffoldTargetCollision: paths: string list -> Diagnostic
    val scaffoldProviderEmpty: name: string -> Diagnostic
    val scaffoldProviderFailed: name: string -> exitCode: int -> Diagnostic
    val scaffoldProviderUnavailable: name: string -> Diagnostic
    val scaffoldProviderWroteSddTree: paths: string list -> Diagnostic
    val scaffoldMirrorFailed: paths: string list -> Diagnostic
    val scaffoldProvenanceMalformed: path: string -> Diagnostic

    // Feature 052 CLI-coherence advisories: both non-blocking (Info/Warning), so the
    // scaffold's outcome classification and exit code are unchanged vs an up-to-date run.
    val scaffoldCliBehindMinimum: installed: string -> minimum: string -> Diagnostic
    val scaffoldProviderMinimumMalformed: rawMinimum: string -> Diagnostic

    // Post-instantiation advisory facts (FR-010): non-fatal, never change the exit
    // code or flip the scaffold to failed/incomplete.
    val scaffoldRepoInitSkippedExistingRepository: unit -> Diagnostic
    val scaffoldRepoInitSkippedGitUnavailable: unit -> Diagnostic
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

    val sort: diagnostics: Diagnostic list -> Diagnostic list
    val hasBlocking: diagnostics: Diagnostic list -> bool
