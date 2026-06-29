namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open Fsgg
open FS.GG.SDD.Artifacts
open Xunit

/// Feature 042 US1: the YAML `load` edge turns the real on-disk registry file into
/// the typed `Fsgg.Registry.RegistryDocument`, tolerating its many extra/unknown
/// keys, never throwing on bad input — and the parsed real fixture validates clean
/// through the pure `Registry.validateDocument` (SC-001/SC-005, the parity evidence).
module RegistryDocumentParseTests =

    let private fixturePath =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "registry", "dependencies.yml")

    let private loadFixture () =
        match RegistryDocument.load fixturePath with
        | Ok document -> document
        | Error error -> failwith $"Expected Ok loading the canonical fixture, got Error: {error.Message}"

    // --- Real fixture parses, order-preserving, tolerant of unknown keys ---

    [<Fact>]
    let ``load reads the canonical fixture into the typed model`` () =
        let document = loadFixture ()

        Assert.Equal(1, document.SchemaVersion)
        // repos mapping preserved in declaration order.
        Assert.Equal<string list>(
            [ "sdd"; "rendering"; "governance"; "templates" ],
            document.Repos |> List.map (fun r -> r.Id))
        // contracts list preserved in file order (first and last).
        Assert.Equal("scaffold-provider", (List.head document.Contracts).Id)
        Assert.Equal("shared-build-config", (List.last document.Contracts).Id)
        // dependency edges and coherence entries are present (extra keys tolerated).
        Assert.Equal(5, document.Dependencies.Length)
        Assert.Contains("registry-validator-typed", document.Coherence |> List.map (fun c -> c.Id))

    [<Fact>]
    let ``load preserves contract package-version and range when present`` () =
        let document = loadFixture ()
        let handoff = document.Contracts |> List.find (fun c -> c.Id = "governance-handoff")
        Assert.Equal(Some "1.x", handoff.Range)

        let contracts = document.Contracts |> List.find (fun c -> c.Id = "fsgg-contracts")
        Assert.Equal(Some "1.1.0", contracts.PackageVersion)

    // --- The real fixture validates clean: path-in/verdict-out parity (SC-001/SC-005) ---

    [<Fact>]
    let ``the parsed canonical fixture validates clean`` () =
        Assert.Equal(Registry.Valid, Registry.validateDocument (loadFixture ()))

    // --- Feature 045: the canonical fixture carries the 4-segment
    // governance-reference-gate-set@1.2.1.1 (ADR-0007) and it validates clean,
    // proving the widening end-to-end over the real on-disk registry (FR-005, SC-001). ---

    [<Fact>]
    let ``the 4-segment governance-reference-gate-set 1.2.1.1 is carried and validates clean`` () =
        let document = loadFixture ()
        let gateSet = document.Contracts |> List.find (fun c -> c.Id = "governance-reference-gate-set")
        Assert.Equal("1.2.1.1", gateSet.Version)
        Assert.Equal(Some "1.2.1.1", gateSet.PackageVersion)
        Assert.Equal(Registry.Valid, Registry.validateDocument document)

    // --- Determinism (FR-007 / SC-004): repeated load + validate are identical ---

    [<Fact>]
    let ``load then validate is deterministic across repeated runs`` () =
        let first = RegistryDocument.load fixturePath
        let second = RegistryDocument.load fixturePath
        Assert.Equal(first, second)

        match first, second with
        | Ok a, Ok b -> Assert.Equal(Registry.validateDocument a, Registry.validateDocument b)
        | _ -> Assert.True(false, "Expected both loads to succeed.")

    // --- Safe load failure: never throws (Constitution VIII / US1-S3) ---

    [<Fact>]
    let ``missing file returns Error, not an exception`` () =
        match RegistryDocument.load (fixturePath + ".does-not-exist") with
        | Error _ -> ()
        | Ok _ -> Assert.True(false, "Expected Error for a missing file.")

    [<Fact>]
    let ``malformed YAML returns Error, not an exception`` () =
        let temp = Path.Combine(Path.GetTempPath(), "fsgg-registry-malformed.yml")
        File.WriteAllText(temp, "schemaVersion: 1\nrepos: [unclosed\n")

        try
            match RegistryDocument.load temp with
            | Error _ -> ()
            | Ok _ -> Assert.True(false, "Expected Error for malformed YAML.")
        finally
            File.Delete temp

    [<Fact>]
    let ``non-integer schemaVersion returns Error`` () =
        let temp = Path.Combine(Path.GetTempPath(), "fsgg-registry-badschema.yml")
        File.WriteAllText(temp, "schemaVersion: notanint\nrepos: {}\n")

        try
            match RegistryDocument.load temp with
            | Error _ -> ()
            | Ok _ -> Assert.True(false, "Expected Error for non-integer schemaVersion.")
        finally
            File.Delete temp
