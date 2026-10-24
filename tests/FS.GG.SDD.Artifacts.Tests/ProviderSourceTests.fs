namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Net
open System.Text
open FS.GG.SDD.Artifacts
open Xunit

module ProviderSourceTests =
    // Synthetic declaration tests establish codec/selection behavior, never installed provider acceptance.
    let digest = "sha256:" + String.replicate 64 "a"
    let otherDigest = "sha256:" + String.replicate 64 "b"
    let bytes value = Encoding.UTF8.GetBytes(value: string)

    let local: ProviderSource.Reference =
        {
            Id = "independent-author"
            Origin = ProviderSource.Explicit
            Locator = ProviderSource.LocalDirectory "/selected/source"
            ManifestSha256 = digest
        }

    let sourceSet: ProviderSource.SourceSet =
        { Schema = "fsgg.provider-source-set/1"; Sources = [ local ] }

    let policy: ProviderSource.Policy =
        {
            Schema = "fsgg.provider-source-policy/1"
            AllowedOrigins = [ "https://example.org" ]
            MaximumRedirects = 0
            AcquisitionSeconds = 60
            MaximumTransferBytes = 1048576L
            MaximumExpandedBytes = 2097152L
        }

    let catalogBytes = bytes "synthetic catalog bytes"
    let archiveBytes = bytes "synthetic opaque package bytes"

    let manifest: ProviderSource.Manifest =
        {
            Schema = "fsgg.provider-source/1"
            Id = local.Id
            Revision = "immutable-revision"
            CatalogPath = "catalog.yml"
            Members =
                [
                    { Path = "catalog.yml"; Bytes = int64 catalogBytes.Length; Sha256 = ProviderSource.sha256 catalogBytes }
                    { Path = "fixture.nupkg"; Bytes = int64 archiveBytes.Length; Sha256 = ProviderSource.sha256 archiveBytes }
                ]
            Archives = [ { TemplateSource = "opaque-fixture"; Path = "fixture.nupkg" } ]
        }

    let members = [ "catalog.yml", catalogBytes; "fixture.nupkg", archiveBytes ]

    let codes result =
        match result with
        | Ok _ -> []
        | Error errors -> errors |> List.map (fun (error: Fsgg.ProviderCatalog.Diagnostic) -> error.Code)

    let unwrap = function
        | Ok value -> value
        | Error errors -> failwithf "%A" errors

    [<Fact>]
    let ``typed authoring roundtrips exact source declarations without effects`` () =
        let sourceText = ProviderSource.serializeSet sourceSet |> unwrap |> bytes
        Assert.Equal(sourceSet, ProviderSource.parseSet (ProviderSource.sha256 sourceText) sourceText |> unwrap)
        let manifestText = ProviderSource.serializeManifest manifest |> unwrap |> bytes
        Assert.Equal(manifest, ProviderSource.parseManifest (ProviderSource.sha256 manifestText) manifestText |> unwrap)
        let policyText = ProviderSource.serializePolicy policy |> unwrap |> bytes
        Assert.Equal(policy, ProviderSource.parsePolicy (ProviderSource.sha256 policyText) policyText |> unwrap)

    [<Fact>]
    let ``source set rejects duplicate identities even for identical entries`` () =
        let errors = ProviderSource.validateSet { sourceSet with Sources = [ local; local ] }
        Assert.Contains(errors, fun error -> error.Code = "source.duplicateId")

    [<Fact>]
    let ``different source layers cannot silently shadow an identical provider`` () =
        let catalog = CatalogIntegrityFixture.sealedCatalog ()
        let workspace = { local with Id = "workspace-author"; Origin = ProviderSource.Workspace }
        Assert.Contains(
            "source.providerCollision",
            codes (ProviderSource.orderCatalogs [ local, catalog; workspace, catalog ])
        )

    [<Fact>]
    let ``origin ordering is deterministic without changing descriptor payloads`` () =
        let catalog = CatalogIntegrityFixture.sealedCatalog ()
        let one (descriptor: Fsgg.ProviderCatalog.Descriptor) =
            let value = { catalog with Providers = [ descriptor ] }
            { value with Digest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.catalogBytes value) }

        let first = one catalog.Providers[0]
        let second = one catalog.Providers[1]
        let builtin = { local with Id = "z-builtin"; Origin = ProviderSource.BuiltIn }
        let result = ProviderSource.orderCatalogs [ local, first; builtin, second ] |> unwrap
        Assert.Equal<string list>([ "z-builtin"; local.Id ], result |> List.map (fun (source, _) -> source.Id))
        Assert.Equal(second, result.Head |> snd)
        Assert.Equal(first, result.Tail.Head |> snd)

    [<Fact>]
    let ``raw set digest is checked before interpretation`` () =
        let malformed = bytes "not json"
        Assert.Equal<string list>([ "source.rawDigestMismatch" ], codes (ProviderSource.parseSet digest malformed))

    [<Theory>]
    [<InlineData("{\"schema\":\"fsgg.provider-source-set/1\",\"schema\":\"fsgg.provider-source-set/1\",\"sources\":[]}")>]
    [<InlineData("{\"schema\":\"fsgg.provider-source-set/1\",\"sources\":[],\"execute\":\"echo unwanted\"}")>]
    let ``duplicate and unknown JSON properties refuse the whole set`` text =
        let data = bytes text
        Assert.NotEmpty(codes (ProviderSource.parseSet (ProviderSource.sha256 data) data))

    [<Fact>]
    let ``member verification rejects extra missing changed and duplicate content`` () =
        Assert.Equal<Result<unit, Fsgg.ProviderCatalog.Diagnostic list>>(Ok(), ProviderSource.verifyMembers manifest members)
        Assert.NotEmpty(codes (ProviderSource.verifyMembers manifest (members @ [ "extra", bytes "x" ])))
        Assert.NotEmpty(codes (ProviderSource.verifyMembers manifest [ List.head members ]))
        Assert.NotEmpty(codes (ProviderSource.verifyMembers manifest [ "catalog.yml", bytes "changed"; List.last members ]))
        Assert.NotEmpty(codes (ProviderSource.verifyMembers manifest (members @ [ List.head members ])))

    [<Theory>]
    [<InlineData("../escape")>]
    [<InlineData("/absolute")>]
    [<InlineData("nested/../../escape")>]
    [<InlineData("C:\\escape")>]
    [<InlineData("nested\\escape")>]
    let ``manifest paths cannot escape or acquire alternate platform meanings`` path =
        let changed = { manifest with CatalogPath = path }
        Assert.NotEmpty(ProviderSource.validateManifest changed)

    [<Fact>]
    let ``portable case collision refuses before platform selection`` () =
        let first = List.head manifest.Members
        let collision = { first with Path = "CATALOG.YML" }
        Assert.NotEmpty(ProviderSource.validateManifest { manifest with Members = manifest.Members @ [ collision ] })

    [<Fact>]
    let ``cache identity follows exact bytes not mutable local pathname`` () =
        let moved = { local with Locator = ProviderSource.LocalDirectory "/another/path" }
        Assert.Equal(ProviderSource.cacheKey local, ProviderSource.cacheKey moved)
        Assert.NotEqual(ProviderSource.cacheKey local, ProviderSource.cacheKey { local with ManifestSha256 = otherDigest })

    [<Theory>]
    [<InlineData("http://example.org/source.zip")>]
    [<InlineData("https://user:password@example.org/source.zip")>]
    [<InlineData("https://example.org:8443/source.zip")>]
    [<InlineData("https://example.org/source.zip#fragment")>]
    [<InlineData("https://other.example/source.zip")>]
    [<InlineData("https://127.0.0.1/source.zip")>]
    let ``first acquisition policy rejects unsupported URI authority`` uri =
        Assert.NotEmpty(codes (ProviderSource.validateUri policy uri))

    [<Fact>]
    let ``redirects require explicit finite same origin permission`` () =
        let original = Uri "https://example.org/one.zip"
        Assert.NotEmpty(codes (ProviderSource.validateRedirect policy original 0 "https://example.org/two.zip"))
        let admitted = { policy with MaximumRedirects = 1 }
        Assert.Empty(codes (ProviderSource.validateRedirect admitted original 0 "https://example.org/two.zip"))
        Assert.NotEmpty(codes (ProviderSource.validateRedirect admitted original 1 "https://example.org/three.zip"))
        Assert.NotEmpty(codes (ProviderSource.validateRedirect admitted original 0 "https://other.example/two.zip"))

    [<Theory>]
    [<InlineData("127.0.0.1")>]
    [<InlineData("10.1.2.3")>]
    [<InlineData("169.254.169.254")>]
    [<InlineData("172.16.0.1")>]
    [<InlineData("192.168.1.1")>]
    [<InlineData("0.0.0.0")>]
    [<InlineData("224.0.0.1")>]
    [<InlineData("::1")>]
    [<InlineData("fe80::1")>]
    [<InlineData("fc00::1")>]
    [<InlineData("::ffff:127.0.0.1")>]
    let ``resolved special addresses cannot become allowed by hostname spelling`` address =
        Assert.False(ProviderSource.isPublicAddress(IPAddress.Parse address))

    [<Fact>]
    let ``policy cannot widen frozen source limits or consume cleanup reserve`` () =
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with AcquisitionSeconds = 15 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with AcquisitionSeconds = 181 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with MaximumRedirects = 4 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with MaximumExpandedBytes = ProviderSource.limits.MaximumExpandedBytes + 1L })
