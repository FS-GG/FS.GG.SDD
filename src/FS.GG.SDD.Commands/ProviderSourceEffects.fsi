namespace FS.GG.SDD.Commands

/// Data-only source acquisition. Scaffold execution remains owned by CatalogScaffoldEffects.
module ProviderSourceEffects =
    type Request =
        {
            SourceSetBytes: byte array
            ExpectedSourceSetSha256: string
            PolicyBytes: byte array
            ExpectedPolicySha256: string
            CacheRoot: string
            Offline: bool
        }

    type Operation
    type PreparedSet
    type SelectedSource

    type InputFile = { AbsolutePath: string; Bytes: int64; Sha256: string }

    type Observation =
        {
            Phase: ProviderSourceWorkflow.Phase
            Outcome: ProviderSourceWorkflow.Outcome
            Diagnostics: Fsgg.ProviderCatalog.Diagnostic list
            Settled: bool
        }

    /// Constructs custody before I/O; validates the explicit request without acquiring data.
    val prepare:
        request: Request ->
        cancellationToken: System.Threading.CancellationToken ->
            Result<Operation, Fsgg.ProviderCatalog.Diagnostic list>

    /// Consumes one original acquisition window. Unknown cleanup cannot return a prepared set.
    val run: operation: Operation -> Async<Result<PreparedSet, Fsgg.ProviderCatalog.Diagnostic list>>
    val observe: operation: Operation -> Observation
    val release: operation: Operation -> Result<unit, Fsgg.ProviderCatalog.Diagnostic list>

    /// Selects an existing verified provider without probing tools or executing its declarations.
    val select:
        prepared: PreparedSet -> provider: string ->
        overrides: (string * string) list ->
            Result<SelectedSource, Fsgg.ProviderCatalog.Diagnostic list>

    val catalogBytes: source: SelectedSource -> byte array
    val catalogSha256: source: SelectedSource -> string
    val archivePath: source: SelectedSource -> string
    val archiveSha256: source: SelectedSource -> string
    val sourceObservation: source: SelectedSource -> FS.GG.SDD.Artifacts.ProviderSource.Observation

    /// The existing scaffold caller captures these through its held input path and original budget.
    val inputFiles: source: SelectedSource -> InputFile list

    /// Pure complete-closure join over those captured bytes, not a new unbounded filesystem read.
    /// A prepared handle is not enduring authority over a mutable pathname.
    val verifyCaptured:
        source: SelectedSource -> captured: (string * byte array) list ->
            Result<unit, Fsgg.ProviderCatalog.Diagnostic list>
