namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Config
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open Xunit

module ScaffoldProvenanceTests =
    let private snapshot text : FileSnapshot =
        { Path = ".fsgg/providers.yml"
          Text = text }

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

            let productName =
                descriptor.Parameters |> List.find (fun p -> p.Key = "productName")

            Assert.True(productName.Required)
            Assert.Equal(None, productName.Default)
            let license = descriptor.Parameters |> List.find (fun p -> p.Key = "license")
            Assert.False(license.Required)
            Assert.Equal(Some "MIT", license.Default)
        | other -> failwith $"Expected one descriptor, got {other}."

    // T009 (aligned with FS.GG.Templates#43 / ADR-0008): the coherent-set minimum is the
    // nested `minimumFsggSdd.version` scalar; SDD reads it verbatim into the descriptor.
    [<Fact>]
    let ``parseProviderRegistry reads the nested minimumFsggSdd version when declared`` () =
        let registry =
            """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
    minimumFsggSdd:
      version: "0.3.0"
      requires: "Feature 049 + 051"
"""

        match parseProviderRegistry (snapshot registry) with
        | Ok [ descriptor ] -> Assert.Equal(Some "0.3.0", descriptor.MinimumCliVersion)
        | other -> failwith $"Expected one descriptor, got {other}."

    // T009: absent minimumFsggSdd → None; never affects entry-drop (entry is kept).
    [<Fact>]
    let ``parseProviderRegistry defaults the minimum to None when the axis is absent`` () =
        match parseProviderRegistry (snapshot validRegistry) with
        | Ok [ descriptor ] -> Assert.Equal(None, descriptor.MinimumCliVersion)
        | other -> failwith $"Expected one descriptor, got {other}."

    // T009: the real coherent-set state today — the axis is declared but `version` is null
    // (PENDING PUBLISH). SDD reads None and degrades to "no minimum"; the entry is kept.
    [<Fact>]
    let ``parseProviderRegistry reads None when minimumFsggSdd version is null`` () =
        let registry =
            """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
    minimumFsggSdd:
      version: null
      requires: "Feature 049 + 051"
"""

        match parseProviderRegistry (snapshot registry) with
        | Ok [ descriptor ] -> Assert.Equal(None, descriptor.MinimumCliVersion)
        | other -> failwith $"Expected one descriptor kept, got {other}."

    // T009: a malformed nested version is read verbatim (validity is decided only at
    // comparison, not at read time) and does NOT drop the entry.
    [<Fact>]
    let ``parseProviderRegistry keeps the entry and reads a malformed minimum verbatim`` () =
        let registry =
            """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
    minimumFsggSdd:
      version: not-a-version
"""

        match parseProviderRegistry (snapshot registry) with
        | Ok [ descriptor ] -> Assert.Equal(Some "not-a-version", descriptor.MinimumCliVersion)
        | other -> failwith $"Expected one descriptor kept, got {other}."

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
          RequiredMinimumCliVersion = None
          ProviderName = "fixture"
          ProviderContractVersion = "1.0.0"
          TemplateRef = "fsgg-fixture-app"
          Outcome = "providerSucceeded"
          ProducedPaths =
            [ { Path = "src/Product/Program.fs"
                Owner = GeneratedProduct
                Sha256 = None }
              { Path = "src/Product/App.fsproj"
                Owner = GeneratedProduct
                Sha256 = None } ]
          MirroredPaths = []
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
                parsed.EffectiveParameters
            )
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

    // --- Feature 052: additive requiredMinimumCliVersion ---

    // T011: round-trips with a declared minimum (Some) and without (None).
    [<Fact>]
    let ``serialize then tryParse round-trips requiredMinimumCliVersion Some`` () =
        let withMin =
            { record with
                RequiredMinimumCliVersion = Some "0.3.0" }

        match tryParse (serialize withMin) with
        | Some parsed -> Assert.Equal(Some "0.3.0", parsed.RequiredMinimumCliVersion)
        | None -> failwith "Expected the with-minimum record to round-trip."

    [<Fact>]
    let ``serialize emits requiredMinimumCliVersion as null when None`` () =
        let json =
            serialize
                { record with
                    RequiredMinimumCliVersion = None }

        Assert.Contains("\"requiredMinimumCliVersion\": null", json)

        match tryParse json with
        | Some parsed -> Assert.Equal(None, parsed.RequiredMinimumCliVersion)
        | None -> failwith "Expected the null-minimum document to round-trip."

    // T011: emitted immediately after the generator object, before providerName.
    [<Fact>]
    let ``serialize emits requiredMinimumCliVersion immediately after generator`` () =
        let json =
            serialize
                { record with
                    RequiredMinimumCliVersion = Some "0.3.0" }

        Assert.True(json.IndexOf "\"generator\"" < json.IndexOf "\"requiredMinimumCliVersion\"")
        Assert.True(json.IndexOf "\"requiredMinimumCliVersion\"" < json.IndexOf "\"providerName\"")

    // T011: byte-stability across two runs (US1 scenario 3).
    [<Fact>]
    let ``serialize is byte-stable with a declared minimum`` () =
        let withMin =
            { record with
                RequiredMinimumCliVersion = Some "0.3.0" }

        Assert.Equal(serialize withMin, serialize withMin)

    // T011: BACK-COMPAT — a v1 document WITHOUT the field parses with None (existing
    // readers ignore the unknown key; records written before this field still parse).
    [<Fact>]
    let ``tryParse defaults requiredMinimumCliVersion to None when the key is absent`` () =
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
        | Some parsed -> Assert.Equal(None, parsed.RequiredMinimumCliVersion)
        | None -> failwith "A v1 document without requiredMinimumCliVersion must still parse."

    // --- 056 T008 (P10 / FR-007): additive mirroredPaths, schema stays v1 ---

    let private mirroredRecord =
        { record with
            ProducedPaths =
                [ { Path = ".agents/skills/fs-gg-elmish/SKILL.md"
                    Owner = GeneratedProduct
                    Sha256 = None } ]
            MirroredPaths =
                [ { Path = ".codex/skills/fs-gg-elmish/SKILL.md"
                    Owner = Mirrored
                    Sha256 = None }
                  { Path = ".claude/skills/fs-gg-elmish/SKILL.md"
                    Owner = Mirrored
                    Sha256 = None } ] }

    [<Fact>]
    let ``serialize then tryParse round-trips mirroredPaths and keeps schemaVersion 1`` () =
        match tryParse (serialize mirroredRecord) with
        | Some parsed ->
            Assert.Equal(1, parsed.SchemaVersion)
            // Sorted ascending by path; owner mirrored preserved.
            Assert.Equal<string list>(
                [ ".claude/skills/fs-gg-elmish/SKILL.md"
                  ".codex/skills/fs-gg-elmish/SKILL.md" ],
                parsed.MirroredPaths |> List.map (fun p -> p.Path)
            )

            Assert.True(parsed.MirroredPaths |> List.forall (fun p -> p.Owner = Mirrored))
        | None -> failwith "Expected the mirrored record to round-trip."

    [<Fact>]
    let ``serialize emits mirroredPaths sorted immediately after producedPaths`` () =
        let json = serialize mirroredRecord
        Assert.True(json.IndexOf "\"producedPaths\"" < json.IndexOf "\"mirroredPaths\"")
        Assert.True(json.IndexOf "\"mirroredPaths\"" < json.IndexOf "\"effectiveParameters\"")
        // .claude sorts before .codex.
        Assert.True(json.IndexOf ".claude/skills/fs-gg-elmish" < json.IndexOf ".codex/skills/fs-gg-elmish")
        Assert.Contains("\"owner\": \"mirrored\"", json)
        // The seeded fs-gg-sdd-* namespace never appears in the mirror record.
        Assert.DoesNotContain("fs-gg-sdd-", json)

    [<Fact>]
    let ``mirrored owner appears only inside mirroredPaths never in producedPaths`` () =
        let json = serialize mirroredRecord

        let producedSegment =
            json.Substring(
                json.IndexOf "\"producedPaths\"",
                json.IndexOf "\"mirroredPaths\"" - json.IndexOf "\"producedPaths\""
            )

        Assert.DoesNotContain("mirrored", producedSegment)

    [<Fact>]
    let ``tryParse defaults mirroredPaths to empty when the key is absent`` () =
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
        | Some parsed -> Assert.Equal<ScaffoldProducedPath list>([], parsed.MirroredPaths)
        | None -> failwith "A v1 document without mirroredPaths must still parse."

    [<Fact>]
    let ``serialize emits an empty mirroredPaths array when nothing mirrored`` () =
        let json = serialize record
        Assert.Contains("\"mirroredPaths\": []", json)

    [<Fact>]
    let ``tryParse on malformed JSON yields None`` () =
        Assert.Equal(None, tryParse "{ not json")

    [<Fact>]
    let ``tryParse on an unsupported schema yields None`` () =
        let json =
            (serialize record).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")

        Assert.Equal(None, tryParse json)

    // --- 057 (ADR-0014 §Decision 3): additive per-path sha256, schema stays v1 ---

    let private hashedRecord =
        { record with
            ProducedPaths =
                [ { Path = ".agents/skills/fs-gg-elmish/SKILL.md"
                    Owner = GeneratedProduct
                    Sha256 = Some "abc123" } ]
            MirroredPaths =
                [ { Path = ".claude/skills/fs-gg-elmish/SKILL.md"
                    Owner = Mirrored
                    Sha256 = Some "def456" } ] }

    [<Fact>]
    let ``serialize then tryParse round-trips per-path sha256 and keeps schemaVersion 1`` () =
        match tryParse (serialize hashedRecord) with
        | Some parsed ->
            Assert.Equal(1, parsed.SchemaVersion)
            Assert.Equal(Some "abc123", (parsed.ProducedPaths |> List.head).Sha256)
            Assert.Equal(Some "def456", (parsed.MirroredPaths |> List.head).Sha256)
        | None -> failwith "Expected the hashed record to round-trip."

    [<Fact>]
    let ``serialize omits sha256 for digest-free paths so 1_0_0 output is byte-identical`` () =
        // Every path in `record` has Sha256 = None, so no sha256 key may appear.
        let json = serialize record
        Assert.DoesNotContain("sha256", json)

    [<Fact>]
    let ``tryParse defaults sha256 to None when the key is absent`` () =
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
        | Some parsed -> Assert.Equal(None, (parsed.ProducedPaths |> List.head).Sha256)
        | None -> failwith "A v1 document without sha256 must still parse."
