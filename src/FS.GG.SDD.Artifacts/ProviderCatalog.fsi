namespace FS.GG.SDD.Artifacts

/// Strict in-memory schema-2 YAML decoding; legacy registry/schema readers are unchanged.
module ProviderCatalog =
    /// Decode and validate one catalog, without reading files or resolving transports.
    val parse: text: string -> Result<Fsgg.ProviderCatalog.Catalog, Fsgg.ProviderCatalog.Diagnostic list>
