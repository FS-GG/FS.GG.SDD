namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Canonical UTF-8 Markdown input. Text carries no BOM and uses LF line endings.
type QuintMarkdownSource =
    { Path: string
      Text: string
      Sha256: string }

/// One ordered literate Quint fence declared by the author.
type QuintFence =
    { Ordinal: int
      Target: string
      ModuleName: string
      SourceRange: QuintSourceRange
      ContentSha256: string }

/// Authoritative ordered binding between one Markdown source and generated modules.
type QuintFenceManifest =
    { Schema: string
      SourcePath: string
      SourceSha256: string
      Fences: QuintFence list }

/// Digest receipt for one generated module.
type QuintGeneratedModule =
    { Target: string
      Sha256: string
      Bytes: int64 }

/// Two clean extraction observations. Warnings are retained and treated as errors.
type QuintExtractionObservation =
    { First: QuintGeneratedModule list
      Second: QuintGeneratedModule list
      Warnings: string list }

/// A stable Markdown binding for a generated range or diagnostic using the profile's inclusive range type.
type QuintSourceBinding =
    { FenceOrdinal: int
      Range: QuintSourceRange }

/// One generated-module range mapped to canonical Markdown.
type QuintSourceMapEntry =
    { Target: string
      GeneratedRange: QuintSourceRange
      Source: QuintSourceBinding }

/// Versioned deterministic source map with no host path or compiler node identity.
type QuintSourceMap =
    { Schema: string
      SourceSha256: string
      Entries: QuintSourceMapEntry list }

[<RequireQualifiedAccess>]
module QuintSource =
    /// Stable fence-manifest schema identity.
    val fenceManifestSchema: string

    /// Stable source-map schema identity.
    val sourceMapSchema: string

    /// Decode canonical UTF-8 Markdown, refusing BOMs, invalid UTF-8, CR line endings, and unsafe paths.
    val createMarkdown: path: string -> bytes: byte array -> Result<QuintMarkdownSource, SpecificationDiagnostic list>

    /// Validate source identity, ordered fences, safe plain `.qnt` targets, and source ranges.
    val validateManifest: source: QuintMarkdownSource -> manifest: QuintFenceManifest -> SpecificationDiagnostic list

    /// Validate warnings-as-errors and byte-identical results from two isolated extractions.
    val validateExtraction:
        source: QuintMarkdownSource ->
        manifest: QuintFenceManifest ->
        observation: QuintExtractionObservation ->
            SpecificationDiagnostic list

    /// Emit deterministic UTF-8 canonical JSON for an ordered fence manifest.
    val encodeFenceManifest: manifest: QuintFenceManifest -> byte array

    /// Return lowercase SHA-256 over canonical fence-manifest bytes.
    val fenceManifestFingerprint: manifest: QuintFenceManifest -> string

    /// Validate all generated and canonical ranges and their fence bindings.
    val validateSourceMap:
        source: QuintMarkdownSource ->
        manifest: QuintFenceManifest ->
        sourceMap: QuintSourceMap ->
            SpecificationDiagnostic list

    /// Emit deterministic UTF-8 canonical JSON in generated-range order.
    val encodeSourceMap: sourceMap: QuintSourceMap -> byte array

    /// Strictly decode source-map v1; unknown fields and schema versions fail closed.
    val decodeSourceMap: bytes: byte array -> Result<QuintSourceMap, SpecificationDiagnostic list>

    /// Resolve the first source binding containing a generated position.
    val tryResolve:
        target: string -> position: QuintSourcePosition -> sourceMap: QuintSourceMap -> QuintSourceBinding option
