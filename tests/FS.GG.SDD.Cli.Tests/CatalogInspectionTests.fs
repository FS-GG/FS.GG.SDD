namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.Tests
open Xunit

[<Collection("ProcessGlobalEnv")>]
module CatalogInspectionTests =
    let catalogBytes () =
        let authored =
            File.ReadAllText(
                Path.Combine(
                    TestSupport.repoRoot,
                    "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
                )
            )

        let catalog =
            match ProviderCatalog.parse authored with
            | Ok c -> c
            | Error errors -> failwithf "%A" errors

        let descriptors =
            catalog.Providers
            |> List.map (fun d ->
                { d with
                    DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d)
                })

        let node = JsonNode.Parse authored |> nonNull

        node["digest"] <-
            JsonValue.Create(
                ProviderCatalogIntegrity.digest (
                    ProviderCatalogIntegrity.catalogBytes { catalog with Providers = descriptors }
                )
            )

        for providerNode in (node["providers"] |> nonNull).AsArray() do
            let provider = nonNull providerNode
            let id = (provider["id"] |> nonNull).GetValue<string>()

            provider["descriptorDigest"] <-
                JsonValue.Create((descriptors |> List.find (fun d -> d.Id = id)).DescriptorDigest)

        Encoding.UTF8.GetBytes(node.ToJsonString())

    let capture args =
        let previous = Console.Out
        use writer = new StringWriter()

        try
            Console.SetOut writer
            let code = FS.GG.SDD.Cli.Catalog.run args
            code, writer.ToString()
        finally
            Console.SetOut previous

    [<Fact>]
    let ``real file inspection discovers all provider metadata and writes nothing`` () =
        let root = TestSupport.tempDirectory ()
        let path = Path.Combine(root, "catalog.json")
        let bytes = catalogBytes ()
        File.WriteAllBytes(path, bytes)

        let args =
            [
                "inspect"
                "--catalog"
                path
                "--catalog-sha256"
                ProviderCatalogIntegrity.digest bytes
            ]

        let code, json = capture args
        Assert.Equal(0, code)
        use doc = JsonDocument.Parse json
        Assert.Equal("prepared", doc.RootElement.GetProperty("status").GetString())
        Assert.Equal(5, doc.RootElement.GetProperty("providers").GetArrayLength())
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("observations").ValueKind)
        Assert.Single(Directory.GetFiles(root)) |> ignore
        Assert.True(bytes = File.ReadAllBytes path)
        let textCode, text = capture (args @ [ "--text" ])
        let richCode, rich = capture (args @ [ "--rich" ])
        Assert.Equal(textCode, richCode)
        Assert.Equal(text, rich)
        Assert.DoesNotContain("\u001b", rich)

    [<Fact>]
    let ``raw digest unknown option and malformed parameter failures remain located`` () =
        for args, expected in
            [
                [ "inspect" ], "catalog.selectionMissing"
                [ "inspect"; "--catalog" ], "catalog.missingOptionValue"
                [ "inspect"; "--future" ], "catalog.unknownOption"
                [ "inspect"; "--catalog"; "a"; "--catalog"; "b" ], "catalog.duplicateOption"
            ] do
            let code, json = capture args
            Assert.Equal(1, code)
            Assert.Contains(expected, json)

    [<Fact>]
    let ``catalog scaffold route refuses before filesystem or process effects`` () =
        let root = TestSupport.tempDirectory ()
        let before = Directory.GetFileSystemEntries(root)
        let previous = Console.Out
        use writer = new StringWriter()

        try
            Console.SetOut writer
            let code = Program.run [ "scaffold"; "--catalog"; "nonexistent"; "--root"; root ]
            Assert.Equal(1, code)
            Assert.Contains("catalog.scaffoldUnavailable", writer.ToString())
        finally
            Console.SetOut previous

        Assert.True(before = Directory.GetFileSystemEntries root)
