namespace FS.GG.SDD.Commands

open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts

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
            Catalog: Catalog
            Selected: PreparedConfiguration option
        }

    let prepare (selection: CatalogSelection) =
        match ProviderCatalogIntegrity.verify selection.ExpectedRawDigest selection.CatalogBytes with
        | Error diagnostics -> Error diagnostics
        | Ok catalog ->
            match selection.Provider with
            | None when not selection.Overrides.IsEmpty ->
                Error
                    [
                        {
                            Code = "catalog.providerRequired"
                            Path = "$.provider"
                            Message = "Parameters require an explicit provider selection."
                        }
                    ]
            | None ->
                Ok
                    {
                        Status = "prepared"
                        Catalog = catalog
                        Selected = None
                    }
            | Some provider ->
                Fsgg.ProviderCatalog.resolve catalog provider selection.Overrides
                |> Result.map (fun selected ->
                    {
                        Status = "prepared"
                        Catalog = catalog
                        Selected = Some selected
                    })
