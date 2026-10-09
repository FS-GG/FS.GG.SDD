namespace ProviderAuthoring

/// Pure typed declaration helpers; preparation does not execute or certify a provider.
module Authoring =
    val catalog: executable: string -> Fsgg.ProviderCatalog.Catalog
    val seal: catalog: Fsgg.ProviderCatalog.Catalog -> byte array
    val validate: policyBytes: byte array -> catalogBytes: byte array -> unit

    type Inputs =
        {
            TemplateRoot: string
            FixtureExecutable: string
            TestInput: string
            Policy: string
            Output: string
        }

    /// Writes an existing manifest shape into an absent output directory; no provider execution.
    val emit: inputs: Inputs -> unit
    /// Reads strict production schema2 and writes the maintained driver's summary.
    val check: provenancePath: string -> summaryPath: string -> unit
