namespace FS.GG.SDD.Artifacts

/// Raw file identity and canonical semantic identity are checked independently.
module ProviderCatalogIntegrity =
    val digest: bytes: byte array -> string
    /// Canonical compact JSON; excludes only the descriptor's own digest field.
    val descriptorBytes: descriptor: Fsgg.ProviderCatalog.Descriptor -> byte array
    /// Canonical compact JSON; excludes only the catalog's own digest field.
    val catalogBytes: catalog: Fsgg.ProviderCatalog.Catalog -> byte array
    /// Complete canonical descriptor projection, including its declared digest.
    val descriptorJson: descriptor: Fsgg.ProviderCatalog.Descriptor -> string

    val verify:
        expectedRawDigest: string ->
        bytes: byte array ->
            Result<Fsgg.ProviderCatalog.Catalog, Fsgg.ProviderCatalog.Diagnostic list>
