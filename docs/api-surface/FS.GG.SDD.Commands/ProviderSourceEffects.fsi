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
            /// Inspection has no target; scaffold supplies its target before source-cache effects.
            TargetRoot: string option
            Offline: bool
        }

    type Operation
    type PreparedSet
    type SelectedSource

    type InputFile = { AbsolutePath: string; Bytes: int64; Sha256: string }

    /// Whole-catalog inspection has no selected provider/archive and grants no execution authority.
    type SourcePreview =
        {
            Source: FS.GG.SDD.Artifacts.ProviderSource.Reference
            Revision: string
            SourceSetSha256: string
            AcquisitionPolicySha256: string
            Acquisition: FS.GG.SDD.Artifacts.ProviderSource.Acquisition
            ResolvedUri: string option
            Redirects: string list
            NetworkHops: FS.GG.SDD.Artifacts.ProviderSource.NetworkHop list
            Catalog: CatalogScaffoldWorkflow.CatalogPreview
        }

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
    /// Passive wait retains original ownership; it neither renews cleanup time nor retries acquisition.
    val waitForSettled: operation: Operation -> Async<Observation>
    val release: operation: Operation -> Result<unit, Fsgg.ProviderCatalog.Diagnostic list>

    /// Immutable captured views, ordered by origin then ordinal source ID; providers are ordinally ordered.
    /// Catalog.Selected is None. Performs no filesystem/network/tool work or provider resolution.
    /// Whole-set collision validation has already succeeded before a PreparedSet can exist.
    val inspect: prepared: PreparedSet -> SourcePreview list

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
    /// Exact selected provider and ordinal literal overrides, with defensive catalog-byte copy.
    val catalogSelection: source: SelectedSource -> CatalogScaffoldWorkflow.CatalogSelection
    /// None is inspection-only and cannot be used by prepareFromSource to create a target later.
    val targetRoot: source: SelectedSource -> string option

    /// The existing scaffold caller captures these through its held input path and original budget.
    val inputFiles: source: SelectedSource -> InputFile list
    /// Same-assembly custody join; this path is sensed again under the scaffold's original phase.
    val internal cacheRoot: source: SelectedSource -> string

    /// Same closed ZIP32 decoder used by acquisition; pure byte input, no I/O or trust decision.
    val internal inspectEnvelope:
        maximumExpandedBytes: int64 -> bytes: byte array ->
            Result<(string * byte array) list, Fsgg.ProviderCatalog.Diagnostic list>

    /// Pure complete-closure join over those captured bytes, not a new unbounded filesystem read.
    /// A prepared handle is not enduring authority over a mutable pathname.
    val verifyCaptured:
        source: SelectedSource -> captured: (string * byte array) list ->
            Result<unit, Fsgg.ProviderCatalog.Diagnostic list>
