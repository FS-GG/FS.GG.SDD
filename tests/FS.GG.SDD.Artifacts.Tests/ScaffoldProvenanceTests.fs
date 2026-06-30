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
              { Path = "src/Product/App.fsproj"; Owner = GeneratedProduct } ]
          EffectiveParameters = [ "variant", "alpha"; "productName", "Demo" ] }

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

    // T005 (050 FR-003): the effective parameters round-trip, preserving order and content.
    [<Fact>]
    let ``serialize then tryParse round-trips effectiveParameters in key order`` () =
        match tryParse (serialize record) with
        | Some parsed ->
            // The record declared them unsorted; serialize sorts ascending by key, so the
            // parsed list is the sorted set (productName before variant).
            Assert.Equal<(string * string) list>(
                [ "productName", "Demo"; "variant", "alpha" ],
                parsed.EffectiveParameters)
        | None -> failwith "Expected provenance to round-trip effectiveParameters."

    // T005 (050 D3): a v1 document WITHOUT effectiveParameters parses to [] (backward compat) —
    // a provenance file written before this field still parses.
    [<Fact>]
    let ``tryParse defaults effectiveParameters to empty when the key is absent`` () =
        let withoutField =
            """{
  "schemaVersion": 1,
  "generator": { "id": "fsgg-sdd", "version": "0.0.0" },
  "providerName": "fixture",
  "providerContractVersion": "1.0.0",
  "templateRef": "fsgg-fixture-app",
  "outcome": "providerSucceeded",
  "producedPaths": [ { "path": "App.fsproj", "owner": "generatedProduct" } ]
}"""

        match tryParse withoutField with
        | Some parsed -> Assert.Equal<(string * string) list>([], parsed.EffectiveParameters)
        | None -> failwith "A v1 document without effectiveParameters must still parse."

    // T005 (050 FR-003): byte-exact emission — effectiveParameters is the LAST field, after
    // producedPaths, sorted ascending by key, as {key,value} objects.
    [<Fact>]
    let ``serialize emits effectiveParameters sorted after producedPaths`` () =
        let json = serialize record
        Assert.True(json.IndexOf "\"producedPaths\"" < json.IndexOf "\"effectiveParameters\"")
        // Sorted ascending by key: productName precedes variant.
        Assert.True(json.IndexOf "\"productName\"" < json.IndexOf "\"variant\"")
        Assert.Contains("\"key\": \"productName\"", json)
        Assert.Contains("\"value\": \"Demo\"", json)
        Assert.Contains("\"key\": \"variant\"", json)
        Assert.Contains("\"value\": \"alpha\"", json)

    // T005 (050 FR-003): an empty effective map serializes as [] and round-trips.
    [<Fact>]
    let ``serialize emits an empty effectiveParameters array when none forwarded`` () =
        let json = serialize { record with EffectiveParameters = [] }
        Assert.Contains("\"effectiveParameters\": []", json)
        match tryParse json with
        | Some parsed -> Assert.Equal<(string * string) list>([], parsed.EffectiveParameters)
        | None -> failwith "Expected the empty-effective document to round-trip."

    [<Fact>]
    let ``tryParse on malformed JSON yields None`` () =
        Assert.Equal(None, tryParse "{ not json")

    [<Fact>]
    let ``tryParse on an unsupported schema yields None`` () =
        let json = (serialize record).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")
        Assert.Equal(None, tryParse json)
