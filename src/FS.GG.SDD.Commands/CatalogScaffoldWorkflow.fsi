namespace FS.GG.SDD.Commands

/// Preparation confers no tool, package, mutation or lifecycle authority.
module CatalogScaffoldWorkflow =
    type CatalogSelection =
        {
            CatalogBytes: byte array
            ExpectedRawDigest: string
            Provider: string option
            Overrides: (string * string) list
        }

    type CatalogPreview =
        {
            Status: string
            Catalog: Fsgg.ProviderCatalog.Catalog
            Selected: Fsgg.ProviderCatalog.PreparedConfiguration option
        }

    val prepare: selection: CatalogSelection -> Result<CatalogPreview, Fsgg.ProviderCatalog.Diagnostic list>
