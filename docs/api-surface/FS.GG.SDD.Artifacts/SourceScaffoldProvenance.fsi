namespace FS.GG.SDD.Artifacts

/// Schema3 joins a source observation to the unchanged schema2 execution/ownership record.
/// Structural parsing does not establish authentic acquisition, execution or host qualification.
module SourceScaffoldProvenance =
    type Record =
        {
            SchemaVersion: int
            Source: ProviderSource.Observation
            Catalog: CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord
        }

    /// Refuses source/catalog/archive mismatches and malformed nested legacy records.
    val parse: text: string -> Result<Record, Fsgg.ProviderCatalog.Diagnostic list>
    val serialize: record: Record -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
    val ownershipProjection: record: Record -> ScaffoldProvenance.ScaffoldProvenanceRecord
