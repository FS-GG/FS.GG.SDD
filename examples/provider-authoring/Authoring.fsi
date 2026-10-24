namespace ProviderAuthoring

/// Pure typed declaration helpers; preparation does not execute or certify a provider.
module Authoring =
    /// The profiles the sample can author: "test" (one test:test capability) and "build-test-package".
    val profiles: string list
    /// The provider id a profile declares.
    val providerId: profile: string -> string
    val catalogFor: profile: string -> executable: string -> Fsgg.ProviderCatalog.Catalog
    val catalog: executable: string -> Fsgg.ProviderCatalog.Catalog
    val seal: catalog: Fsgg.ProviderCatalog.Catalog -> byte array
    val validateFor: profile: string -> policyBytes: byte array -> catalogBytes: byte array -> unit
    val validate: policyBytes: byte array -> catalogBytes: byte array -> unit

    type Inputs =
        {
            TemplateRoot: string
            FixtureExecutable: string
            TestInput: string
            Policy: string
            Output: string
            /// "test" (the command-line default) or "build-test-package".
            Profile: string
        }

    /// Writes an existing manifest shape into an absent output directory; no provider execution.
    val emit: inputs: Inputs -> unit
    /// Reads strict production schema2 and writes the maintained driver's summary.
    val check: provenancePath: string -> summaryPath: string -> unit
