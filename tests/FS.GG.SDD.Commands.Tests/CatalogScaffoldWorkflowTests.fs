namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CatalogScaffoldWorkflow
open Xunit

module CatalogScaffoldWorkflowTests =
    let bytes () =
        let text =
            File.ReadAllText(
                Path.Combine(
                    TestSupport.repoRoot,
                    "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
                )
            )

        let catalog =
            match ProviderCatalog.parse text with
            | Ok c -> c
            | Error e -> failwithf "%A" e

        let ds =
            catalog.Providers
            |> List.map (fun d ->
                { d with
                    DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d)
                })

        let c = { catalog with Providers = ds }
        let node = JsonNode.Parse text
        node["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.catalogBytes c))

        for p in node["providers"].AsArray() do
            let id = p["id"].GetValue<string>()
            p["descriptorDigest"] <- JsonValue.Create((ds |> List.find (fun d -> d.Id = id)).DescriptorDigest)

        Encoding.UTF8.GetBytes(node.ToJsonString())

    [<Fact>]
    let ``catalog-only fifth provider appears and selected identities retain raw values`` () =
        let bytes = bytes ()

        let selection =
            {
                CatalogBytes = bytes
                ExpectedRawDigest = ProviderCatalogIntegrity.digest bytes
                Provider = None
                Overrides = []
            }

        match prepare selection with
        | Error e -> failwithf "%A" e
        | Ok preview ->
            Assert.Equal("prepared", preview.Status)
            Assert.Equal(5, preview.Catalog.Providers.Length)
            let fifth = preview.Catalog.Providers |> List.last

            match
                prepare
                    { selection with
                        Provider = Some fifth.Id
                        Overrides = [ "raw", "Awkward name!?"; "package", "example.org/explicit/module" ]
                    }
            with
            | Error e -> failwithf "%A" e
            | Ok selected ->
                Assert.Equal("Awkward name!?", selected.Selected.Value.RawProductName)
                Assert.Equal("example.org/explicit/module", selected.Selected.Value.PackageIdentity)
                Assert.Equal("ExplicitCode", selected.Selected.Value.CodeIdentifier)

        Assert.True(
            Result.isError (
                prepare
                    { selection with
                        Overrides = [ "raw", "ignored" ]
                    }
            )
        )

    [<Fact>]
    let ``malformed or future provenance blocks refresh and preserves original files`` () =
        for document in [ "{\"schemaVersion\":2}"; "{\"schemaVersion\":99}"; "{not json" ] do
            let root = TestSupport.tempDirectory ()

            TestSupport.request FS.GG.SDD.Commands.CommandTypes.Init root
            |> TestSupport.runRequest
            |> ignore

            let path = Path.Combine(root, ScaffoldProvenance.provenancePath)
            File.WriteAllText(path, document)

            let before =
                Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                |> Array.map (fun p -> p, File.ReadAllBytes p)
                |> Map.ofArray

            let report = TestSupport.runRefresh root "missing-work"
            Assert.Contains(report.Diagnostics, fun d -> d.Id = "provenance.malformed")

            let lifecycleReport = TestSupport.runCharter root "missing-work" "Blocked catalog document"
            Assert.Contains(lifecycleReport.Diagnostics, fun d -> d.Id = "provenance.malformed")

            for KeyValue(path, bytes) in before do
                Assert.Equal<byte>(bytes, File.ReadAllBytes path)
