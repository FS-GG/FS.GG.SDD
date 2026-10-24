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

    [<Theory>]
    [<InlineData("a", "a/b.nupkg")>]
    [<InlineData("A/b.nupkg", "a")>]
    [<InlineData("SOURCE.JSON/catalog", "fixture.nupkg")>]
    let ``manifest refuses file ancestor collisions including its fixed manifest`` catalogPath archivePath =
        let candidate =
            { manifest with
                CatalogPath = catalogPath
                Members = [ { manifest.Members.Head with Path = catalogPath }; { List.last manifest.Members with Path = archivePath } ]
                Archives = [ { manifest.Archives.Head with Path = archivePath } ] }
        Assert.Contains(ProviderSource.validateManifest candidate, fun error -> error.Code = "source.memberCollision")

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

    [<Fact>]
    let ``valid whitespace change still requires the actual policy raw digest`` () =
        let original = ProviderSource.serializePolicy policy |> unwrap |> bytes
        let spaced = Array.concat [ bytes " \n"; original; bytes "\n" ]
        Assert.Equal<string list>(
            [ "source.rawDigestMismatch" ],
            codes (ProviderSource.parsePolicy (ProviderSource.sha256 original) spaced)
        )
        Assert.Equal(policy, ProviderSource.parsePolicy (ProviderSource.sha256 spaced) spaced |> unwrap)

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

    [<Fact>]
    let ``same length mutation of captured source bytes fails the digest join`` () =
        let changed = Array.copy catalogBytes
        changed[0] <- changed[0] ^^^ 0xffuy
        Assert.Contains(
            "source.memberDigestMismatch",
            codes (ProviderSource.verifyMembers manifest [ "catalog.yml", changed; "fixture.nupkg", archiveBytes ])
        )

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
    let ``resolved special addresses cannot become allowed by hostname spelling`` (address: string) =
        Assert.False(ProviderSource.isPublicAddress(IPAddress.Parse address))

    [<Fact>]
    let ``policy cannot widen frozen source limits or consume cleanup reserve`` () =
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with AcquisitionSeconds = 15 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with AcquisitionSeconds = 181 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with MaximumRedirects = 4 })
        Assert.NotEmpty(ProviderSource.validatePolicy { policy with MaximumExpandedBytes = ProviderSource.limits.MaximumExpandedBytes + 1L })

    [<Theory>]
    [<InlineData("60.0")>]
    [<InlineData("6e1")>]
    [<InlineData("-0")>]
    [<InlineData("\"60\"")>]
    let ``policy integer grammar cannot hide a different lexical number`` token =
        let text = ProviderSource.serializePolicy policy |> unwrap
        let changed = text.Replace("\"acquisitionSeconds\":60", "\"acquisitionSeconds\":" + token)
        Assert.NotEqual(text, changed)
        let raw = bytes changed
        Assert.NotEmpty(codes (ProviderSource.parsePolicy (ProviderSource.sha256 raw) raw))

    [<Fact>]
    let ``strict UTF8 does not replace malformed bytes or accept a BOM`` () =
        let valid = ProviderSource.serializeSet sourceSet |> unwrap |> bytes
        for raw in [ Array.append [| 0xefuy; 0xbbuy; 0xbfuy |] valid; [| 0xffuy; 0xfeuy |] ] do
            Assert.NotEmpty(codes (ProviderSource.parseSet (ProviderSource.sha256 raw) raw))

    [<Fact>]
    let ``source reference kind chooses a closed object shape`` () =
        for locator in
            [
                "{\"kind\":\"local-directory\",\"path\":\"/selected/source\",\"revision\":\"extra\"}"
                "{\"kind\":\"local-directory\"}"
                "{\"kind\":\"https-envelope\",\"path\":\"/selected/source\"}"
            ] do
            let raw =
                bytes (
                    "{\"schema\":\"fsgg.provider-source-set/1\",\"sources\":[{\"id\":\"outside\",\"origin\":\"explicit\",\"locator\":"
                    + locator + ",\"manifestSha256\":\"" + digest + "\"}]}"
                )
            Assert.NotEmpty(codes (ProviderSource.parseSet (ProviderSource.sha256 raw) raw))

    [<Theory>]
    [<InlineData("CON")>]
    [<InlineData("aux.txt")>]
    [<InlineData("nested/Lpt1.log")>]
    [<InlineData("nested/file.")>]
    [<InlineData("nested/file ")>]
    [<InlineData("nested//file")>]
    let ``member path portability is checked before current host filesystem semantics`` path =
        let first = List.head manifest.Members
        let altered =
            { manifest with
                CatalogPath = path
                Members = [ { first with Path = path }; List.last manifest.Members ] }
        Assert.NotEmpty(ProviderSource.validateManifest altered)

    [<Fact>]
    let ``manifest cannot use its externally hashed source json as a payload role`` () =
        let first = List.head manifest.Members
        let altered =
            { manifest with
                CatalogPath = "source.json"
                Members = [ { first with Path = "source.json" }; List.last manifest.Members ] }
        Assert.NotEmpty(ProviderSource.validateManifest altered)

    [<Fact>]
    let ``one member cannot satisfy both catalog and archive roles`` () =
        let altered = { manifest with Archives = [ { TemplateSource = "opaque-fixture"; Path = manifest.CatalogPath } ] }
        Assert.NotEmpty(ProviderSource.validateManifest altered)

    [<Fact>]
    let ``every source population is bounded even when all identities are unique`` () =
        let many = [ for index in 1..17 -> { local with Id = string index } ]
        Assert.NotEmpty(ProviderSource.validateSet { sourceSet with Sources = many })
        Assert.NotEmpty(ProviderSource.validateSet { sourceSet with Sources = [] })

    [<Theory>]
    [<InlineData("192.0.2.1")>]
    [<InlineData("198.51.100.1")>]
    [<InlineData("203.0.113.1")>]
    [<InlineData("198.18.0.1")>]
    [<InlineData("100.64.0.1")>]
    [<InlineData("240.0.0.1")>]
    [<InlineData("2001:db8::1")>]
    [<InlineData("::ffff:192.0.2.1")>]
    let ``nonpublic special use ranges remain refused after DNS resolution`` (address: string) =
        Assert.False(ProviderSource.isPublicAddress(IPAddress.Parse address))

    [<Fact>]
    let ``public address positives are explicit without proving a network exchange`` () =
        Assert.True(ProviderSource.isPublicAddress(IPAddress.Parse "8.8.8.8"))
        Assert.True(ProviderSource.isPublicAddress(IPAddress.Parse "2606:4700:4700::1111"))

    [<Fact>]
    let ``cache identity includes pinned remote envelope bytes`` () =
        let remote = { local with Locator = ProviderSource.HttpsEnvelope("https://example.org/a.zip", "r1", digest) }
        let changedEnvelope = { remote with Locator = ProviderSource.HttpsEnvelope("https://example.org/a.zip", "r1", otherDigest) }
        Assert.NotEqual(ProviderSource.cacheKey remote, ProviderSource.cacheKey changedEnvelope)
