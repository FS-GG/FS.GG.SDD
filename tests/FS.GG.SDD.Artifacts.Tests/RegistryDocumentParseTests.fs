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
            document.Repos |> List.map (fun r -> r.Id)
        )
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

        let gateSet =
            document.Contracts
            |> List.find (fun c -> c.Id = "governance-reference-gate-set")

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

    // --- The three-state `consumers` read (FS.GG.SDD#508) ---
    //
    // This is where the collapse lived: `scalarList` ends in `Option.defaultValue []`, so an
    // absent key and a present `[]` arrived at the validator as the same value. The edge was
    // untested in BOTH directions — nothing asserted anything about `consumers` parsing at
    // all — which is how the collapse survived long enough to block a real package's row.

    /// Parses one contract's `consumers:` through the real YAML edge, in a document
    /// otherwise minimal. `body` is spliced in verbatim so each case names its own bytes.
    let private consumersOf (body: string) : Registry.ConsumerDeclaration =
        let temp =
            Path.Combine(Path.GetTempPath(), $"fsgg-registry-consumers-{System.Guid.NewGuid():N}.yml")

        let text =
            "schemaVersion: 1\n"
            + "repos:\n  sdd:\n    name: FS.GG.SDD\n    role: r\n"
            + "contracts:\n"
            + "  - id: alpha\n    version: \"1.0.0\"\n    owner: sdd\n    surface: s\n"
            + body

        File.WriteAllText(temp, text)

        try
            match RegistryDocument.load temp with
            | Ok document -> (document.Contracts |> List.find (fun c -> c.Id = "alpha")).Consumers
            | Error error -> failwith $"Expected Ok, got Error: {error.Message}"
        finally
            File.Delete temp

    [<Fact>]
    let ``an explicit empty consumers parses as Declared [] - not Unspecified`` () =
        Assert.Equal(Registry.ConsumersDeclared [], consumersOf "    consumers: []\n")

    [<Fact>]
    let ``an ABSENT consumers parses as Unspecified - not Declared []`` () =
        Assert.Equal(Registry.ConsumersUnspecified, consumersOf "")

    /// The whole feature in one assertion. Before #508 both of these produced `[]` and the
    /// validator could not tell a deliberate "nothing consumes this" from a forgotten line.
    [<Fact>]
    let ``absent and empty consumers do NOT parse to the same value`` () =
        Assert.NotEqual(consumersOf "", consumersOf "    consumers: []\n")

    [<Fact>]
    let ``a populated consumers parses in file order`` () =
        Assert.Equal(Registry.ConsumersDeclared [ "sdd"; "ghost" ], consumersOf "    consumers: [sdd, ghost]\n")

    /// A scalar where a list belongs — the likely typo. It must NOT read as absent (which
    /// would send the author hunting for a line that is right there) and must NOT read as
    /// empty (which, now that empty is legal, would pass the typo off as a real assertion).
    [<Fact>]
    let ``a scalar consumers parses as Malformed, carrying its text`` () =
        Assert.Equal(Registry.ConsumersMalformed "'sdd'", consumersOf "    consumers: sdd\n")

    /// An explicit key with no value. The key is there; the list is not. The honest empty is
    /// `[]`, and requiring it to be written is the point — same call `parseMirrored` makes.
    [<Fact>]
    let ``a null consumers parses as Malformed, not as an empty declaration`` () =
        Assert.Equal(Registry.ConsumersMalformed "<null>", consumersOf "    consumers:\n")

    [<Fact>]
    let ``a mapping consumers parses as Malformed, described by kind`` () =
        Assert.Equal(Registry.ConsumersMalformed "<mapping>", consumersOf "    consumers:\n      sdd: yes\n")

    /// Blanks are passed through rather than filtered, so `validateDocument`'s `isBlank` arm
    /// can report them. Filtering would shorten `[""]` to `[]` — promoting a blank entry into
    /// a deliberate "nothing consumes this", which is the collapse one level down.
    [<Fact>]
    let ``a blank consumers entry survives the edge for the validator to report`` () =
        Assert.Equal(Registry.ConsumersDeclared [ "" ], consumersOf "    consumers: [\"\"]\n")

    // --- The three-state `wire-contract` read (FS.GG.SDD#589 / ADR-0052) ---
    //
    // Same shape as `consumers`: presence on the KEY, an unknown/blank provenance and a
    // non-mapping value are MALFORMED (each its own text), and the three known provenances
    // parse into their `WireContract` cases carrying their fields verbatim for the validator.

    /// Parses one contract's `wire-contract:` through the real YAML edge. `body` is a
    /// `consumers` line plus whatever wire block the case declares, spliced verbatim so each
    /// case names its own bytes. (`consumers: []` keeps the doc otherwise coherent.)
    let private wireOf (body: string) : Registry.WireContractDeclaration =
        let temp =
            Path.Combine(Path.GetTempPath(), $"fsgg-registry-wire-{System.Guid.NewGuid():N}.yml")

        let text =
            "schemaVersion: 1\n"
            + "repos:\n  sdd:\n    name: FS.GG.SDD\n    role: r\n"
            + "contracts:\n"
            + "  - id: alpha\n    version: \"1.0.0\"\n    owner: sdd\n    surface: s\n    consumers: []\n"
            + body

        File.WriteAllText(temp, text)

        try
            match RegistryDocument.load temp with
            | Ok document -> (document.Contracts |> List.find (fun c -> c.Id = "alpha")).WireContract
            | Error error -> failwith $"Expected Ok, got Error: {error.Message}"
        finally
            File.Delete temp

    [<Fact>]
    let ``an ABSENT wire-contract parses as Unspecified`` () =
        Assert.Equal(Registry.WireUnspecified, wireOf "")

    [<Fact>]
    let ``a vendored-proto parses into VendoredProto carrying upstream and its own version`` () =
        let body =
            "    wire-contract:\n"
            + "      provenance: vendored-proto\n"
            + "      upstream: Blizzard/s2client-proto\n"
            + "      upstream-version: \"5.0.12\"\n"

        Assert.Equal(Registry.WireDeclared(Registry.VendoredProto("Blizzard/s2client-proto", "5.0.12")), wireOf body)

    [<Fact>]
    let ``an owned-proto parses into OwnedProto carrying the proto path`` () =
        let body =
            "    wire-contract:\n      provenance: owned-proto\n      proto: protos/bar.proto\n"

        Assert.Equal(Registry.WireDeclared(Registry.OwnedProto "protos/bar.proto"), wireOf body)

    [<Fact>]
    let ``a code-first-protobuf-net parses into CodeFirstProtobufNet carrying the surface`` () =
        let body =
            "    wire-contract:\n      provenance: code-first-protobuf-net\n      surface: src/FS.GG.Net/Wire.fsi\n"

        Assert.Equal(Registry.WireDeclared(Registry.CodeFirstProtobufNet "src/FS.GG.Net/Wire.fsi"), wireOf body)

    /// An unknown provenance has no honest declared value (the union is closed), so it is
    /// Malformed carrying the offending token — never guessed, never dropped.
    [<Fact>]
    let ``an unknown provenance parses as Malformed, carrying the token`` () =
        let body = "    wire-contract:\n      provenance: grpc\n"
        Assert.Equal(Registry.WireMalformed "unknown provenance 'grpc'", wireOf body)

    /// A mapping with no `provenance:` — the key is there, the discriminator is not.
    [<Fact>]
    let ``a wire-contract with no provenance parses as Malformed`` () =
        let body = "    wire-contract:\n      upstream: x\n"
        Assert.Equal(Registry.WireMalformed "<no provenance>", wireOf body)

    /// A scalar where a mapping belongs — the likely typo. Must not read as absent.
    [<Fact>]
    let ``a scalar wire-contract parses as Malformed, carrying its text`` () =
        Assert.Equal(Registry.WireMalformed "'sc2'", wireOf "    wire-contract: sc2\n")

    /// An explicit key with no value.
    [<Fact>]
    let ``a null wire-contract parses as Malformed, not as absent`` () =
        Assert.Equal(Registry.WireMalformed "<null>", wireOf "    wire-contract:\n")

    /// A sequence where a mapping belongs.
    [<Fact>]
    let ``a sequence wire-contract parses as Malformed, described by kind`` () =
        Assert.Equal(Registry.WireMalformed "<sequence>", wireOf "    wire-contract: [a, b]\n")

    /// Absent and a declared provenance do NOT parse to the same value — the collapse this
    /// three-state model makes unrepresentable.
    [<Fact>]
    let ``absent and a declared wire-contract do NOT parse to the same value`` () =
        let declared =
            "    wire-contract:\n      provenance: owned-proto\n      proto: p.proto\n"

        Assert.NotEqual(wireOf "", wireOf declared)

    // --- FS.GG.SDD#789: the decode seam reaches the registry documents ---
    //
    // FS.GG.SDD#748 routed `CommandEffects`' read through `SkillMirror.decodeBody`. The
    // registry documents did not read through it — they called `File.ReadAllText`, which
    // SUBSTITUTES `U+FFFD` for a sequence it cannot decode and returns a string. So a
    // mis-encoded registry was parsed from text nobody authored and reported VALID: ids,
    // scopes and owners silently rewritten and then validated as though typed that way.
    // Nothing is hashed at this edge, so this is not the digest collision #737/#748 are
    // about; it is the same representation gap reached from the parsing side.

    let private skillsFixturePath =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "registry", "skills.yml")

    /// First index of `needle` in `haystack`, or -1.
    let private indexOfBytes (haystack: byte array) (needle: byte array) =
        let limit = haystack.Length - needle.Length

        let rec search i =
            if i > limit then -1
            elif Array.sub haystack i needle.Length = needle then i
            else search (i + 1)

        search 0

    /// A copy of `sourcePath` with ONE invalid UTF-8 byte — `0x80`, a lone continuation —
    /// spliced in immediately after `marker`, which must name a point inside a scalar VALUE.
    ///
    /// The splice point is chosen so the file is still PERFECTLY well-formed YAML once
    /// `File.ReadAllText` substitutes `U+FFFD` for the byte: that is precisely what made the
    /// old `valid: true` so convincing, and a fixture that merely broke the YAML would test
    /// the malformed-document path instead of this one.
    ///
    /// Returns the temp path and the byte offset at which the invalid sequence begins — the
    /// offset the refusal is required to name.
    let private mangleAfter (sourcePath: string) (marker: string) =
        let bytes = File.ReadAllBytes sourcePath
        let markerBytes = System.Text.Encoding.UTF8.GetBytes marker
        let at = indexOfBytes bytes markerBytes

        Assert.True(at >= 0, $"marker '{marker}' must occur in {sourcePath}")
        let offset = at + markerBytes.Length

        let mangled = Array.concat [ bytes[.. offset - 1]; [| 0x80uy |]; bytes[offset..] ]

        let temp =
            Path.Combine(Path.GetTempPath(), $"fsgg-registry-789-{System.Guid.NewGuid():N}.yml")

        File.WriteAllBytes(temp, mangled)
        temp, offset

    /// The marker each fixture is mangled after. BOTH name a point inside a scalar VALUE that
    /// the loader tolerates any text in, so the substituted document parses AND validates
    /// clean — the premise legs below prove exactly that, per fixture.
    ///
    /// This choice is load-bearing and easy to get wrong. Splicing after `schemaVersion: 1`
    /// looks equivalent and is not: `Int32.TryParse "1�"` fails, so the OLD code already
    /// rejected that file, and a leg built on it would go red before the change for the wrong
    /// reason — never witnessing the substituted-parse defect at all.
    let private dependencyMarker = "name: FS.GG.SDD"

    /// A skill entry's `owner:`, in the first row of the real catalog. `validateSkillRegistry`
    /// requires only that `owner` be non-blank, so `fs-gg-sdd�` sails through — which is
    /// precisely the issue's complaint: "owners containing an invalid sequence are silently
    /// rewritten and then validated as though the author had typed them".
    let private catalogMarker = "scope: process, owner: fs-gg-sdd"

    /// Writes `text` out as genuine UTF-8 and hands the path to `assertion` — the substituted
    /// document, as the OLD load edge saw it.
    let private withSubstituted (text: string) (assertion: string -> unit) =
        let path =
            Path.Combine(Path.GetTempPath(), $"fsgg-registry-789-subst-{System.Guid.NewGuid():N}.yml")

        File.WriteAllText(path, text)

        try
            assertion path
        finally
            File.Delete path

    /// The premise for the DEPENDENCY registry, pinned so it cannot quietly stop being true:
    /// the mangled fixture is not merely well-formed after substitution, it validates CLEAN.
    ///
    /// Without this, the legs below could be passing for the wrong reason — refusing a broken
    /// document rather than a mis-encoded one — and the defect they guard would be untested.
    [<Fact>]
    let ``the mangled registry validates CLEAN after substitution - why it used to pass`` () =
        let temp, _ = mangleAfter fixturePath dependencyMarker

        try
            let substituted = File.ReadAllText temp
            Assert.Contains("�", substituted)

            withSubstituted substituted (fun path ->
                match RegistryDocument.load path with
                | Error error -> Assert.True(false, $"expected the substituted text to load, got: {error.Message}")
                | Ok document -> Assert.Equal(Registry.Valid, Registry.validateDocument document))
        finally
            File.Delete temp

    /// The same premise for the CATALOG, which is the document the issue is actually about.
    /// A mis-encoded `skills.yml` used to load with a `U+FFFD` in an owner and validate clean.
    [<Fact>]
    let ``the mangled catalog validates CLEAN after substitution - why it used to pass`` () =
        let temp, _ = mangleAfter skillsFixturePath catalogMarker

        try
            let substituted = File.ReadAllText temp
            Assert.Contains("�", substituted)

            withSubstituted substituted (fun path ->
                Assert.Equal(SkillRegistryDocument.SkillRegistry, SkillRegistryDocument.detectKind path)

                match SkillRegistryDocument.load path with
                | Error error -> Assert.True(false, $"expected the substituted catalog to load, got: {error.Message}")
                | Ok document ->
                    // The owner really is the mangled one, and it validated anyway.
                    Assert.Contains("�", (List.head document.Skills).Owner)
                    Assert.Equal(Registry.Valid, Registry.validateSkillRegistry document))
        finally
            File.Delete temp

    /// AC1/AC4. Before the change this returned `Ok` — the substituted document parsed and
    /// validated clean.
    [<Fact>]
    let ``a dependency registry whose bytes are not valid UTF-8 is refused, not parsed`` () =
        let temp, _ = mangleAfter fixturePath dependencyMarker

        try
            match RegistryDocument.load temp with
            | Error _ -> ()
            | Ok _ -> Assert.True(false, "Expected Error for a registry whose bytes do not decode.")
        finally
            File.Delete temp

    /// AC2. The refusal names the file and the byte offset, and must not be mistakable for a
    /// YAML syntax error — the document IS well-formed YAML after substitution, so an author
    /// told "syntax error" would go hunting for something that is not there.
    ///
    /// The offset is asserted with its LABEL, not as a bare number: a bare decimal would also
    /// match a line, a column, or a digit run inside the temp path's GUID.
    [<Fact>]
    let ``the refusal names the file and the byte offset, and is not a YAML syntax error`` () =
        let temp, offset = mangleAfter fixturePath dependencyMarker

        try
            match RegistryDocument.load temp with
            | Ok _ -> Assert.True(false, "Expected Error for a registry whose bytes do not decode.")
            | Error error ->
                Assert.Equal(temp, error.Path)
                Assert.Contains(temp, error.Message)
                Assert.Contains($"byte offset {offset}", error.Message)
                Assert.DoesNotContain("YAML syntax error", error.Message)
                // The repair, which the offset alone does not imply.
                Assert.Contains("Re-encode", error.Message)
        finally
            File.Delete temp

    /// AC1 for the org skill catalog — the document the issue is actually about.
    [<Fact>]
    let ``a skill catalog whose bytes are not valid UTF-8 is refused, not parsed`` () =
        let temp, offset = mangleAfter skillsFixturePath catalogMarker

        try
            match SkillRegistryDocument.load temp with
            | Ok _ -> Assert.True(false, "Expected Error for a skill catalog whose bytes do not decode.")
            | Error error ->
                Assert.Contains(temp, error.Message)
                Assert.Contains($"byte offset {offset}", error.Message)
                Assert.DoesNotContain("YAML syntax error", error.Message)
        finally
            File.Delete temp

    /// The `detectKind` probe (the `:104` site) decided the document's KIND from substituted
    /// text too — pre-change it read this file as a `SkillRegistry`. It stays conservative by
    /// contract — an unreadable file is `DependencyRegistry` — and "unreadable" now correctly
    /// includes "does not decode". Either arm refuses, so no mis-encoded catalog can reach a
    /// `valid` verdict through the dispatch.
    [<Fact>]
    let ``detectKind reports an undecodable catalog as DependencyRegistry, and that path refuses it`` () =
        let temp, _ = mangleAfter skillsFixturePath catalogMarker

        try
            Assert.Equal(SkillRegistryDocument.DependencyRegistry, SkillRegistryDocument.detectKind temp)

            match RegistryDocument.load temp with
            | Error _ -> ()
            | Ok _ -> Assert.True(false, "the dependency path detectKind routes to must refuse it too.")
        finally
            File.Delete temp

    /// AC5. The no-op is really guaranteed by `decodeBody`'s own differential test —
    /// `FS.GG.Contracts.Tests/SkillMirrorTests`, ``decodeBody is File_ReadAllText for every
    /// body that decodes`` — which pins the equality character-for-character across BOMs,
    /// CRLF, NULs and multibyte content. This leg pins the consequence AT THIS EDGE: a
    /// preamble the load path must still strip, parsed to the same document with the same
    /// verdict.
    [<Fact>]
    let ``a registry that DOES decode parses identically, BOM included - a pure no-op`` () =
        let canonical = loadFixture ()
        let utf8Bom = [| 0xEFuy; 0xBBuy; 0xBFuy |]

        let temp =
            Path.Combine(Path.GetTempPath(), $"fsgg-registry-789-noop-{System.Guid.NewGuid():N}.yml")

        File.WriteAllBytes(temp, Array.append utf8Bom (File.ReadAllBytes fixturePath))

        try
            match RegistryDocument.load temp with
            | Error error -> Assert.True(false, $"expected Ok for a BOM'd copy, got Error: {error.Message}")
            | Ok document ->
                Assert.Equal<Registry.RegistryDocument>(canonical, document)
                Assert.Equal(Registry.Valid, Registry.validateDocument document)
        finally
            File.Delete temp

    /// AC5 for the catalog: the real `skills.yml` still detects as a skill registry and still
    /// validates clean. (Its parsed CONTENT is pinned in `SkillRegistryDocumentParseTests`;
    /// re-asserting the row count here would redden two files for one catalog edit.)
    [<Fact>]
    let ``the real skill catalog still detects, loads, and validates unchanged`` () =
        Assert.Equal(SkillRegistryDocument.SkillRegistry, SkillRegistryDocument.detectKind skillsFixturePath)

        match SkillRegistryDocument.load skillsFixturePath with
        | Error error -> Assert.True(false, $"expected Ok for the real catalog, got Error: {error.Message}")
        | Ok document -> Assert.Equal(Registry.Valid, Registry.validateSkillRegistry document)
