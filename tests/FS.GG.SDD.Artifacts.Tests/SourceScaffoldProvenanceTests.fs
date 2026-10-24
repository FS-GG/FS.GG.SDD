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
