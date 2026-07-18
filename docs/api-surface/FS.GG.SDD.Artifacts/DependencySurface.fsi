namespace FS.GG.SDD.Artifacts

/// The `dependency-surface` capture artifact (feature 105, Phase 2; design of record
/// ADR-0004 D2). A committed, provenance-stamped snapshot of a pinned framework
/// package's authoritative public surface, read from the **real restored package** —
/// never a vendored `.fsi` snapshot, because trusting a stale snapshot is the RM2
/// incident this feature defeats. `analyze` (Phase 3) resolves a plan's `framework:`
/// references against this committed capture, offline and deterministically; the
/// `dependency-surface` verb owns the nondeterministic restore + surface read that
/// produces it, so the hermetic inner loop never restores.
///
/// The capture is content-addressed: `Sha256` is the digest of the canonical symbol
/// set, so drift is a hash disagreeing with a freshly-read surface — the same
/// committed-baseline + drift-guard idiom as `surface` and `skill-manifest`.
module DependencySurface =

    /// One captured public API symbol — a module-qualified value/member name, e.g.
    /// `SkiaViewer.runAppWithPersistence`. Opaque text; the capture never interprets it.
    type CapturedSymbol = string

    /// A `dependency-surface` capture (schema v1): the authoritative public surface of
    /// `<PackageId>@<Version>`, content-addressed by `Sha256` over its canonical symbol set.
    type DependencySurfaceCapture =
        {
            SchemaVersion: int
            PackageId: string
            Version: string
            /// The feed/source the surface was restored from — provenance only (a feed URL,
            /// or `nuget-cache` when read from the global packages folder). Never resolved
            /// against; recorded so a capture is auditable.
            CapturedFrom: string
            /// Lowercase-hex SHA-256 over the canonical (sorted, deduplicated, `\n`-joined)
            /// symbol set. Drift = this hash disagreeing with a freshly-read surface.
            Sha256: string
            /// The public symbols, sorted and deduplicated.
            Symbols: CapturedSymbol list
        }

    /// The current capture schema version.
    val schemaVersion: int

    /// The default committed baseline root for captures (`docs/dependency-surface`).
    val defaultBaselineRoot: string

    /// The content digest of a symbol set: lowercase-hex SHA-256 over the sorted,
    /// deduplicated symbols joined by `\n`. Single-sourced so `create` and any verifier agree.
    val symbolDigest: symbols: CapturedSymbol list -> string

    /// Build a capture from a freshly-read symbol set: sorts + deduplicates the symbols and
    /// stamps the content `Sha256`. The one constructor, so every capture is canonical.
    val create:
        packageId: string ->
        version: string ->
        capturedFrom: string ->
        symbols: CapturedSymbol list ->
            DependencySurfaceCapture

    /// The committed capture path for a package/version:
    /// `<baselineRoot>/<PackageId>/<Version>.json`. Purely structural — generic SDD embeds
    /// no package literal (FR-009).
    val capturePath: baselineRoot: string -> packageId: string -> version: string -> string

    /// Serialize a capture to its canonical deterministic JSON (2-space indent, sorted
    /// symbols, trailing LF) — the committed, reconcilable artifact bytes.
    val serialize: capture: DependencySurfaceCapture -> string

    /// Parse a committed capture. `Error` on malformed JSON, a blocking schema version, or a
    /// missing/empty required field (`packageId`, `version`, `sha256`).
    val tryParse: text: string -> Result<DependencySurfaceCapture, string>

    /// The symbol set of a capture — the existence oracle Phase 3 resolves references against.
    val symbolSet: capture: DependencySurfaceCapture -> Set<CapturedSymbol>

    /// Extract the capturable public surface of a *loaded* assembly: every public type's short
    /// name, plus every public module static member and every record/DU public instance property
    /// rendered as `<TypeName>.<MemberName>` — the module-qualified form a plan's `framework:`
    /// reference cites. Reflection-tolerant: a type whose dependencies fail to load is skipped
    /// rather than throwing (mirrors the internal `PublicSurface` capture), so a partially
    /// resolvable package still yields a surface. Result is sorted and deduplicated.
    ///
    /// This is the surface-read logic, kept here (single-sourced and unit-testable against any
    /// loaded assembly). The edge only *loads* the restored package assembly and calls this.
    val symbolsFromAssembly: assembly: System.Reflection.Assembly -> CapturedSymbol list
