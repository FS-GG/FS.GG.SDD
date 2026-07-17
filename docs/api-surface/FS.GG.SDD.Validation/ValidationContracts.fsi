namespace FS.GG.SDD.Validation

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion

/// Deterministic, machine-readable contract for the exhaustive validation sweep
/// (`fsgg-sdd validate`). The `validation-report` JSON is the authoritative
/// automation contract; `renderText` is a portable plain-text projection of the
/// same facts. Sensed wall-clock / duration / host facts are fenced into a single
/// `sensed` object excluded from the deterministic comparison (FR-007 / INV-2 / INV-5).
module ValidationContracts =
    /// The four visible per-cell outcomes (data-model Decision 7). Exactly one per
    /// evaluated cell; the three non-pass-by-default states are distinct (INV-6).
    type CellStatus =
        | Pass
        | Fail of Diagnostic
        | SkippedWithReason of reason: string
        | CoverageGap of surface: string
        | NotValidated of reason: string

    /// One coordinate in a matrix (dimension name -> value, in `Dimensions` order)
    /// plus the recorded status once evaluated.
    type MatrixCell =
        { Coordinates: (string * string) list
          Status: CellStatus }

    /// One declared broad-coverage matrix: a name, ordered dimension names, and the
    /// enumerated cross-product of evaluated cells (FR-001).
    type Matrix =
        { Name: string
          Dimensions: string list
          Cells: MatrixCell list }

    /// A determinism/degradation dimension value (FR-003) plus host-variance
    /// determinism (INV-3a). The first four are color/TTY degradation classes;
    /// `PerturbedHostEnvironment` asserts byte-identical output under varied
    /// locale / time zone / working directory / ordering.
    type EnvironmentClass =
        | ColorDisabled
        | TermDumb
        | NonInteractiveRedirected
        | Interactive
        | PerturbedHostEnvironment

    /// Operational triage facts, explicitly excluded from the deterministic
    /// comparison (FR-007 / INV-5). Normalized to `null` in golden fixtures.
    type SensedMetadata =
        { StartedAtUtc: string option
          DurationMs: int option
          Host: string option }

    type ReportSummary =
        { Passed: int
          Failed: int
          Skipped: int
          CoverageGaps: int
          NotValidated: int
          OverallPassed: bool }

    /// The single deterministic machine-readable report (FR-006 / FR-007).
    type ValidationReport =
        { SchemaVersion: int
          GeneratorVersion: GeneratorVersion
          Matrices: Matrix list
          Summary: ReportSummary
          Sensed: SensedMetadata }

    /// Declared matrix names (the four broad matrices).
    val lifecycleMatrixName: string
    val determinismMatrixName: string
    val baselineMatrixName: string
    val compatibilityMatrixName: string
    val matrixNames: string list

    /// Serialized token for an environment class.
    val environmentClassValue: environment: EnvironmentClass -> string

    /// Empty sensed block (all fields normalized to `None`).
    val emptySensed: SensedMetadata

    /// Build an actionable failure diagnostic identifying the matrix, the cell
    /// coordinates, and the affected contract/artifact (FR-006).
    val failure:
        matrix: string ->
        coordinates: (string * string) list ->
        artifactPath: string ->
        message: string ->
        correction: string ->
            Diagnostic

    /// Count cells per status across all matrices and derive `OverallPassed`
    /// (`false` if any `Fail` / `CoverageGap` / `NotValidated`) — INV-6.
    val summarize: matrices: Matrix list -> ReportSummary

    /// Canonical, deterministic JSON serialization: stable key/cell order, sensed
    /// normalized to `null`, no clock / duration / host path / ANSI outside `sensed`
    /// (INV-2 / INV-5; validation-report C-1).
    val serialize: report: ValidationReport -> string

    /// Portable plain-text projection of the same report (no ANSI).
    val renderText: report: ValidationReport -> string

    /// Deterministic, ANSI-free Markdown "report card" projection of the same report
    /// (feature 088 / FS.GG.SDD#172): a heading, the verdict, a summary table of the
    /// five counts, a per-matrix rollup table, and every non-passing cell as a bullet
    /// (passing cells summarized). Fact-parity with the rich projection; carries no
    /// sensed / wall-clock / width data, so it is byte-identical across runs and safe to
    /// capture into a log or file.
    val renderMarkdown: report: ValidationReport -> string

    /// Parse the canonical JSON back into a `ValidationReport` (round-trip).
    val parse: json: string -> Result<ValidationReport, string>
