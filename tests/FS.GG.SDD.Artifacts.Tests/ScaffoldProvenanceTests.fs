namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Config
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open Xunit

module ScaffoldProvenanceTests =
    let private snapshot text : FileSnapshot = { Path = ".fsgg/providers.yml"; Text = text }

    let private validRegistry =
        """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
    parameters:
      - key: productName
        required: true
      - key: license
        required: false
        default: MIT
"""

    [<Fact>]
    let ``parseProviderRegistry reads a valid descriptor with parameters`` () =
        match parseProviderRegistry (snapshot validRegistry) with
        | Ok [ descriptor ] ->
            Assert.Equal("fixture", descriptor.Name)
            Assert.Equal("1.0.0", descriptor.ContractVersion)
            Assert.Equal("fsgg-fixture-app", descriptor.TemplateId)
            Assert.Equal("/abs/path/ok", descriptor.Source)
            Assert.Equal(2, descriptor.Parameters.Length)
            let productName = descriptor.Parameters |> List.find (fun p -> p.Key = "productName")
            Assert.True(productName.Required)
            Assert.Equal(None, productName.Default)
            let license = descriptor.Parameters |> List.find (fun p -> p.Key = "license")
            Assert.False(license.Required)
            Assert.Equal(Some "MIT", license.Default)
        | other -> failwith $"Expected one descriptor, got {other}."

    [<Fact>]
    let ``parseProviderRegistry on empty input is malformed`` () =
        match parseProviderRegistry (snapshot "") with
        | Error diagnostics -> Assert.Contains(diagnostics, fun d -> d.Id = "malformedSchemaVersion")
        | Ok _ -> failwith "Expected malformed registry."

    [<Fact>]
    let ``parseProviderRegistry rejects an out-of-range schema version`` () =
        let registry = "schemaVersion: 9\nproviders: []\n"

        match parseProviderRegistry (snapshot registry) with
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun d -> d.Id = "futureSchemaVersion" || d.Id = "unsupportedSchemaVersion")
        | Ok _ -> failwith "Expected an out-of-range schema diagnostic."

    let private record =
        { SchemaVersion = 1
          Generator = SchemaVersion.currentGeneratorVersion ()
          ProviderName = "fixture"
          ProviderContractVersion = "1.0.0"
          TemplateRef = "fsgg-fixture-app"
          Outcome = "providerSucceeded"
          ProducedPaths =
            [ { Path = "src/Product/Program.fs"; Owner = GeneratedProduct }
              { Path = "src/Product/App.fsproj"; Owner = GeneratedProduct } ] }

    [<Fact>]
    let ``serialize then tryParse round-trips the record`` () =
        match tryParse (serialize record) with
        | Some parsed ->
            Assert.Equal(record.ProviderName, parsed.ProviderName)
            Assert.Equal(record.ProviderContractVersion, parsed.ProviderContractVersion)
            Assert.Equal(record.TemplateRef, parsed.TemplateRef)
            Assert.Equal(record.Outcome, parsed.Outcome)
            Assert.Equal<string list>(
                [ "src/Product/App.fsproj"; "src/Product/Program.fs" ],
                parsed.ProducedPaths |> List.map (fun p -> p.Path)
            )
        | None -> failwith "Expected provenance to round-trip."

    [<Fact>]
    let ``serialize is byte-stable and sorts produced paths`` () =
        let first = serialize record
        let second = serialize record
        Assert.Equal(first, second)
        // App.fsproj sorts before Program.fs regardless of source order.
        Assert.True(first.IndexOf "App.fsproj" < first.IndexOf "Program.fs")
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``tryParse on malformed JSON yields None`` () =
        Assert.Equal(None, tryParse "{ not json")

    [<Fact>]
    let ``tryParse on an unsupported schema yields None`` () =
        let json = (serialize record).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")
        Assert.Equal(None, tryParse json)
