namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open System.Text
open System.Text.Json.Nodes
open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts
open Xunit

module CatalogIntegrityFixture =
    let authored () =
        File.ReadAllText(
            Path.Combine(
                TestSupport.repoRoot,
                "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
            )
        )

    let parsed () =
        match ProviderCatalog.parse (authored ()) with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    let sealedCatalog () =
        let catalog = parsed ()

        let descriptors =
            catalog.Providers
            |> List.map (fun d ->
                { d with
                    DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d)
                })

        let c = { catalog with Providers = descriptors }

        { c with
            Digest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.catalogBytes c)
        }

    let bytes () =
        let catalog = sealedCatalog ()
        let node = JsonNode.Parse(authored ())
        node["digest"] <- JsonValue.Create catalog.Digest

        for d in node["providers"].AsArray() do
            let id = d["id"].GetValue<string>()

            d["descriptorDigest"] <-
                JsonValue.Create((catalog.Providers |> List.find (fun p -> p.Id = id)).DescriptorDigest)

        Encoding.UTF8.GetBytes(node.ToJsonString())

module ProviderCatalogIntegrityTests =
    [<Fact>]
    let ``verified semantic identity survives formatting while raw identity changes`` () =
        let bytes = CatalogIntegrityFixture.bytes ()
        let raw = ProviderCatalogIntegrity.digest bytes
        let verify = ProviderCatalogIntegrity.verify raw bytes
        Assert.True(Result.isOk verify)
        let spaced = Encoding.UTF8.GetBytes(" \n" + Encoding.UTF8.GetString bytes + "\n")
        Assert.NotEqual(raw, ProviderCatalogIntegrity.digest spaced)
        Assert.True(Result.isError (ProviderCatalogIntegrity.verify raw spaced))
        Assert.True(Result.isOk (ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest spaced) spaced))

    [<Fact>]
    let ``semantic tamper refuses even after caller updates the raw digest`` () =
        let node = JsonNode.Parse(Encoding.UTF8.GetString(CatalogIntegrityFixture.bytes ()))
        node["providers"][0]["help"] <- JsonValue.Create "tampered"
        let bytes = Encoding.UTF8.GetBytes(node.ToJsonString())

        match ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest bytes) bytes with
        | Error errors -> Assert.Contains(errors, fun e -> e.Code = "catalog.descriptorDigestMismatch")
        | Ok _ -> failwith "Tamper must refuse."

    [<Fact>]
    let ``canonical projections sort declarations but preserve literal argv and Unicode`` () =
        let catalog = CatalogIntegrityFixture.sealedCatalog ()
        let d = catalog.Providers.Head

        let altered =
            { d with
                Parameters = List.rev d.Parameters
                Tools = List.rev d.Tools
                Capabilities = List.rev d.Capabilities
                Platforms = List.rev d.Platforms
            }

        Assert.Equal<byte>(ProviderCatalogIntegrity.descriptorBytes d, ProviderCatalogIntegrity.descriptorBytes altered)

        Assert.Equal<byte>(
            ProviderCatalogIntegrity.catalogBytes catalog,
            ProviderCatalogIntegrity.catalogBytes
                { catalog with
                    Providers = List.rev catalog.Providers
                }
        )

        let special = { d with Help = "é<\n\u0001" }

        let projected =
            Encoding.UTF8.GetString(ProviderCatalogIntegrity.descriptorBytes special)

        Assert.Contains("\"help\":\"\\u00E9\\u003C\\n\\u0001\"", projected)
        Assert.Contains("\"default\":null", projected)
        Assert.False(projected.EndsWith("\n"))

        let changed =
            { d with
                Capabilities =
                    d.Capabilities
                    |> List.map (fun c ->
                        match c.Binding with
                        | SemanticOnly -> c
                        | Command(cmd, limits) ->
                            { c with
                                Binding =
                                    Command(
                                        { cmd with
                                            Arguments = List.rev cmd.Arguments
                                        },
                                        limits
                                    )
                            })
            }

        Assert.NotEqual(
            ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d),
            ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes changed)
        )

    [<Fact>]
    let ``invalid UTF8 and malformed hashes refuse without mutation`` () =
        let bytes = [| 0xffuy |]
        Assert.True(Result.isError (ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest bytes) bytes))
        Assert.True(Result.isError (ProviderCatalogIntegrity.verify "invalid" (CatalogIntegrityFixture.bytes ())))
