namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module SourceScaffoldProvenanceTests =
    // The existing complete synthetic schema2 fixture supplies codec data, not acquisition evidence.
    let synthetic () : SourceScaffoldProvenance.Record =
        let catalog = CatalogScaffoldProvenanceTests.synthetic ()
        let hash = "sha256:" + System.String('a', 64)

        {
            SchemaVersion = 3
            Catalog = catalog
            Source =
                {
                    Schema = "fsgg.provider-source-observation/1"
                    SourceSetSha256 = hash
                    AcquisitionPolicySha256 = hash
                    Source =
                        {
                            Id = "synthetic-author"
                            Origin = ProviderSource.Explicit
                            Locator = ProviderSource.LocalDirectory "/synthetic/source"
                            ManifestSha256 = hash
                        }
                    Revision = "synthetic-revision"
                    Acquisition = ProviderSource.LocalCopy
                    ResolvedUri = None
                    Redirects = []
                    NetworkHops = []
                    CatalogSha256 = catalog.Declaration.RawCatalogDigest
                    TemplateSource = catalog.Declaration.Descriptor.TemplateSource
                    ArchiveSha256 = catalog.Declaration.Archive.Digest
                }
        }

    let unwrap = function
        | Ok value -> value
        | Error errors -> failwithf "%A" errors

    [<Fact>]
    let ``schema3 preserves every existing ownership field and the strict nested execution record`` () =
        let value = synthetic ()
        let text = SourceScaffoldProvenance.serialize value |> unwrap
        let parsed = SourceScaffoldProvenance.parse text |> unwrap
        Assert.Equal(value, parsed)
        let document = ScaffoldProvenanceDocument.parse text |> unwrap
        Assert.Equal(value.Catalog.Ownership, ScaffoldProvenanceDocument.ownershipProjection document)

        match document with
        | ScaffoldProvenanceDocument.SourceCatalog record -> Assert.Equal(value.Source, record.Source)
        | _ -> failwith "Source provenance must not masquerade as schema1 or schema2."

    [<Fact>]
    let ``source catalog archive and descriptor joins are determining`` () =
        let value = synthetic ()
        let other = "sha256:" + System.String('b', 64)

        for source in
            [
                { value.Source with CatalogSha256 = other }
                { value.Source with ArchiveSha256 = other }
                { value.Source with TemplateSource = "another-template" }
            ] do
            match SourceScaffoldProvenance.serialize { value with Source = source } with
            | Error errors -> Assert.NotEmpty errors
            | Ok _ -> failwith "A changed determining source join must refuse."

    [<Fact>]
    let ``cache observation cannot fabricate a current network hop`` () =
        let value = synthetic ()
        let cached = { value.Source with Acquisition = ProviderSource.CacheRead }
        Assert.Empty(ProviderSource.validateObservation cached)
        let source =
            { cached with
                NetworkHops = [ { Uri = "https://example.org/source.zip"; Address = "8.8.8.8"; StatusCode = 200 } ]
            }

        Assert.NotEmpty(ProviderSource.validateObservation source)

    [<Fact>]
    let ``malformed source provenance never falls back to a valid nested legacy record`` () =
        let text = SourceScaffoldProvenance.serialize (synthetic ()) |> unwrap

        for malformed in
            [
                text.Replace("\"schemaVersion\":3", "\"schemaVersion\":99")
                text.Replace("\"schemaVersion\":3", "\"schemaVersion\":3,\"schemaVersion\":3")
                text.Insert(1, "\"unrecognized\":true,")
            ] do
            match ScaffoldProvenanceDocument.parse malformed with
            | Error errors -> Assert.NotEmpty errors
            | Ok _ -> failwith "Malformed source provenance selected a fallback."

    [<Fact>]
    let ``source wrapper does not change legacy ownership or public document constructors`` () =
        let value = synthetic ()
        let legacy = ScaffoldProvenanceDocument.Legacy value.Catalog.Ownership
        let catalog = ScaffoldProvenanceDocument.Catalog value.Catalog
        let sourced = ScaffoldProvenanceDocument.SourceCatalog value
        for document in [ legacy; catalog; sourced ] do
            Assert.Equal(value.Catalog.Ownership, ScaffoldProvenanceDocument.ownershipProjection document)

    [<Fact>]
    let ``nested catalog corruption remains an error even when source fields are intact`` () =
        let value = synthetic ()
        let altered = { value with Catalog = { value.Catalog with SchemaVersion = 1 } }
        match SourceScaffoldProvenance.serialize altered with
        | Error errors -> Assert.NotEmpty errors
        | Ok _ -> failwith "The source wrapper bypassed the strict existing schema2 contract."

    [<Fact>]
    let ``current network observation joins every hop to its actual final response`` () =
        let original = (synthetic ()).Source
        let initialUri = "https://example.org/source.zip"
        let finalUri = "https://example.org/retained.zip"
        let remote =
            { original.Source with
                Locator = ProviderSource.HttpsEnvelope(initialUri, original.Revision, original.ArchiveSha256) }
        let observation =
            { original with
                Source = remote
                Acquisition = ProviderSource.HttpsFetch
                ResolvedUri = Some finalUri
                Redirects = [ finalUri ]
                NetworkHops =
                    [
                        { Uri = initialUri; Address = "8.8.8.8"; StatusCode = 302 }
                        { Uri = finalUri; Address = "8.8.4.4"; StatusCode = 200 }
                    ] }
        Assert.Empty(ProviderSource.validateObservation observation)
        let firstHop = List.head observation.NetworkHops
        let lastHop = List.last observation.NetworkHops
        for altered in
            [
                { observation with ResolvedUri = None }
                { observation with ResolvedUri = Some initialUri }
                { observation with Redirects = [] }
                { observation with NetworkHops = List.rev observation.NetworkHops }
                { observation with NetworkHops = [ { firstHop with StatusCode = 200 }; lastHop ] }
                { observation with NetworkHops = [ firstHop; { lastHop with Address = "127.0.0.1" } ] }
                { observation with Acquisition = ProviderSource.CacheRead }
            ] do
            Assert.NotEmpty(ProviderSource.validateObservation altered)
