namespace FS.GG.SDD.Artifacts

/// A malformed/new schema never selects the legacy route or an absent provenance fallback.
module ScaffoldProvenanceDocument =
    type Document =
        | Legacy of ScaffoldProvenance.ScaffoldProvenanceRecord
        | Catalog of CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord

    val parse: text: string -> Result<Document, Fsgg.ProviderCatalog.Diagnostic list>
    val ownershipProjection: document: Document -> ScaffoldProvenance.ScaffoldProvenanceRecord
