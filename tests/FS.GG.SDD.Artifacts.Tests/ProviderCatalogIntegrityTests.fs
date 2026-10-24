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
        let node = JsonNode.Parse(authored ()) |> nonNull
        node["digest"] <- JsonValue.Create catalog.Digest

        for descriptor in (node["providers"] |> nonNull).AsArray() do
            let d = nonNull descriptor
            let id = (d["id"] |> nonNull).GetValue<string>()

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
        let node =
            JsonNode.Parse(Encoding.UTF8.GetString(CatalogIntegrityFixture.bytes ()))
            |> nonNull

        let first = ((node["providers"] |> nonNull).AsArray()[0] |> nonNull)
        first["help"] <- JsonValue.Create "tampered"
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

        Assert.True(ProviderCatalogIntegrity.descriptorBytes d = ProviderCatalogIntegrity.descriptorBytes altered)

        Assert.True(
            ProviderCatalogIntegrity.catalogBytes catalog =
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

    // FS.GG.SDD#1127: the canonical writer emits "default": null for an absent default, which
    // the parser used to refuse, so a catalog with a default-less parameter had no round trip.
    [<Fact>]
    let ``a parameter without a default round trips through the canonical writer and parser`` () =
        let sealedCatalog = CatalogIntegrityFixture.sealedCatalog ()
        let descriptor = sealedCatalog.Providers.Head
        let parameter = descriptor.Parameters.Head
        let withoutDefault =
            { descriptor with
                Parameters = { parameter with Default = None } :: descriptor.Parameters.Tail }

        let resealed =
            { withoutDefault with
                DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes withoutDefault) }

        let catalog = { sealedCatalog with Providers = resealed :: sealedCatalog.Providers.Tail }
        let sealedAgain = { catalog with Digest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.catalogBytes catalog) }
        let canonical = ProviderCatalogIntegrity.catalogBytes sealedAgain
        Assert.Contains("\"default\":null", Encoding.UTF8.GetString canonical)
        // The canonical bytes carry no digest member; declare it as the authored fixture does.
        let declared = JsonNode.Parse(Encoding.UTF8.GetString canonical) |> nonNull
        declared["digest"] <- JsonValue.Create sealedAgain.Digest
        let text = declared.ToJsonString()

        // Parsing the canonical bytes yields the absent default and the same semantic identity.
        match ProviderCatalog.parse text with
        | Error errors -> failwithf "Canonical writer output must parse: %A" errors
        | Ok parsed ->
            let reparsed = parsed.Providers.Head.Parameters |> List.find (fun p -> p.Key = parameter.Key)
            Assert.Equal<string option>(None, reparsed.Default)

        // The full semantic verification (descriptor and catalog digests over re-encoded content) holds.
        let bytes = Encoding.UTF8.GetBytes text
        match ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest bytes) bytes with
        | Ok verified -> Assert.Equal<string option>(None, (verified.Providers.Head.Parameters |> List.find (fun p -> p.Key = parameter.Key)).Default)
        | Error errors -> failwithf "The canonical writer output must verify: %A" errors

        // An absent member and an explicit null are the same declaration and the same digest.
        let node = JsonNode.Parse(text) |> nonNull
        let first = ((node["providers"] |> nonNull).AsArray()[0] |> nonNull)
        let nodes = (first["parameters"] |> nonNull).AsArray() |> Seq.map (fun n -> (nonNull n).AsObject()) |> Seq.toList
        let target = nodes |> List.find (fun n -> (n["key"] |> nonNull).GetValue<string>() = parameter.Key)
        Assert.True(target.ContainsKey "default" && isNull target["default"])
        target.Remove "default" |> ignore
        match ProviderCatalog.parse (node.ToJsonString()) with
        | Error errors -> failwithf "An absent default must still parse: %A" errors
        | Ok parsed ->
            let reparsed = parsed.Providers.Head.Parameters |> List.find (fun p -> p.Key = parameter.Key)
            Assert.Equal<string option>(None, reparsed.Default)
        let absentBytes = Encoding.UTF8.GetBytes(node.ToJsonString())
        Assert.True(Result.isOk (ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest absentBytes) absentBytes))

    [<Fact>]
    let ``a non scalar default still refuses`` () =
        let text = Encoding.UTF8.GetString(CatalogIntegrityFixture.bytes ())
        let node = JsonNode.Parse(text) |> nonNull
        let first = ((node["providers"] |> nonNull).AsArray()[0] |> nonNull)
        let firstParameter = ((first["parameters"] |> nonNull).AsArray()[0] |> nonNull).AsObject()
        firstParameter["default"] <- JsonNode.Parse "[\"a\"]"
        Assert.True(Result.isError (ProviderCatalog.parse (node.ToJsonString())))

