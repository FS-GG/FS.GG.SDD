namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open Fsgg
open FS.GG.SDD.Artifacts
open FS.GG.SDD.TestShared
open Xunit

/// Feature 104: the YAML `load` edge for the org skill catalog (`registry/skills.yml`).
///
/// The load edge is where the fail-open would actually be committed. `Internal.boolAt`'s
/// final arm — `| _ -> defaultValue` — maps an ABSENT key and an UNPARSEABLE value onto
/// the same result, so reaching for it here would coerce `absent → false` at the one point
/// where nobody downstream could tell. These tests exist to make that impossible to
/// reintroduce quietly.
module SkillRegistryDocumentParseTests =

    let private fixturePath =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "registry", "skills.yml")

    let private write (body: string) =
        let path = Path.Combine(TestShared.tempDirectory (), "skills.yml")
        File.WriteAllText(path, body)
        path

    let private loadOrFail path =
        match SkillRegistryDocument.load path with
        | Ok document -> document
        | Error error -> failwith $"expected Ok, got Error: {error.Message}"

    let private oneRow (mirroredLine: string) =
        write
            $"""schemaVersion: 1
skills:
  - id: fs-gg-ai
    scope: product
    owner: fs-gg-game
    source: FS.GG.Game/template/product-skills/fs-gg-ai/SKILL.md
    sha256: {String.replicate 64 "a"}
{mirroredLine}"""

    let private mirroredOf path =
        (loadOrFail path).Skills |> List.exactlyOne |> (fun s -> s.Mirrored)

    // --- The real catalog (AC-001). ---

    [<Fact>]
    let ``load reads the real org catalog into the typed model`` () =
        let document = loadOrFail fixturePath

        Assert.Equal(1, document.SchemaVersion)
        Assert.Equal<string list>([ "profile"; "lifecycle"; "feedback"; "designSystem" ], document.Parameters)
        Assert.Equal(41, List.length document.Skills)

    /// The catalog's live shape: 4 declared-true, 4 declared-false, and **33 unclassified**.
    /// That 33 is the number the whole feature turns on — under a `bool` with a `false`
    /// default they would all read as "no mirror obligation", confidently and wrongly.
    [<Fact>]
    let ``the real catalog carries 4 true, 4 false, and 33 UNSPECIFIED - not 37 false`` () =
        let document = loadOrFail fixturePath

        let count declaration =
            document.Skills
            |> List.filter (fun s -> s.Mirrored = declaration)
            |> List.length

        Assert.Equal(4, count (Registry.MirrorDeclared true))
        Assert.Equal(4, count (Registry.MirrorDeclared false))
        Assert.Equal(33, count Registry.MirrorUnspecified)

        // and nothing in the real catalog is unparseable.
        Assert.Empty(
            document.Skills
            |> List.filter (fun s ->
                match s.Mirrored with
                | Registry.MirrorMalformed _ -> true
                | _ -> false)
        )

    /// SC: the real catalog validates clean through the pure validator — the parity evidence.
    [<Fact>]
    let ``the real catalog validates clean`` () =
        Assert.Equal(Registry.Valid, Registry.validateSkillRegistry (loadOrFail fixturePath))

    // --- The three states (FR-002 / FR-003). ---

    [<Fact>]
    let ``an absent mirrored key loads as Unspecified - NOT Declared false`` () =
        let mirrored = mirroredOf (oneRow "")

        Assert.Equal(Registry.MirrorUnspecified, mirrored)
        Assert.NotEqual(Registry.MirrorDeclared false, mirrored)

    [<Theory>]
    [<InlineData("    mirrored: true", true)>]
    [<InlineData("    mirrored: True", true)>]
    [<InlineData("    mirrored: TRUE", true)>]
    [<InlineData("    mirrored: false", false)>]
    [<InlineData("    mirrored: False", false)>]
    let ``a plain boolean loads as Declared`` (line: string, expected: bool) =
        Assert.Equal(Registry.MirrorDeclared expected, mirroredOf (oneRow line))

    /// A QUOTED `"true"` is the string "true", not a verdict. PyYAML yields `str` for it and
    /// `.github`'s check rejects any non-`bool` — so accepting it here would put the two
    /// validators at odds about the canonical file, which is worse than having only one.
    [<Theory>]
    [<InlineData "    mirrored: \"true\"">]
    [<InlineData "    mirrored: 'false'">]
    let ``a QUOTED boolean is malformed, not declared`` (line: string) =
        match mirroredOf (oneRow line) with
        | Registry.MirrorMalformed _ -> ()
        | other -> failwith $"a quoted boolean must not be read as a verdict, got {other}"

    /// The raw text of a quoted verdict KEEPS its quotes, because the quoting is the whole
    /// reason it was refused. Without this, the diagnostic reads `present but not a boolean:
    /// 'true'` and the author cannot act on it — the message shows them the word `true` and
    /// no hint of what is wrong with it.
    [<Fact>]
    let ``a quoted verdict keeps its quotes in the raw text, so the diagnostic is actionable`` () =
        Assert.Equal(Registry.MirrorMalformed "\"true\"", mirroredOf (oneRow "    mirrored: \"true\""))

        match Registry.validateSkillRegistry (loadOrFail (oneRow "    mirrored: \"true\"")) with
        | Registry.Invalid [ d ] -> Assert.Contains("'\"true\"'", d.Message)
        | other -> failwith $"expected one diagnostic disclosing the quoting, got {other}"

    [<Theory>]
    [<InlineData "    mirrored: yes">]
    [<InlineData "    mirrored: 1">]
    [<InlineData "    mirrored: maybe">]
    let ``a non-boolean scalar is Malformed, carrying its raw text`` (line: string) =
        let raw = line.Substring(line.IndexOf(':') + 1).Trim()

        Assert.Equal(Registry.MirrorMalformed raw, mirroredOf (oneRow line))

    /// The subtle one: `tryScalarAt` returns `None` for a present-but-non-scalar value just
    /// as it does for an absent key. Reading a `mirrored: [a, b]` as "absent" would be a
    /// SILENT SKIP — the exact shape FR-003 forbids — so presence is decided on the KEY.
    [<Fact>]
    let ``a non-scalar mirrored is Malformed, NOT read as absent`` () =
        let mirrored = mirroredOf (oneRow "    mirrored: [a, b]")

        Assert.Equal(Registry.MirrorMalformed "<sequence>", mirrored)
        Assert.NotEqual(Registry.MirrorUnspecified, mirrored)

    /// An explicit `mirrored:` with no value is a key with no verdict — malformed, not absent.
    /// `.github`'s check decides presence with `"mirrored" in row` and rejects non-booleans,
    /// so a null verdict is malformed there too.
    [<Fact>]
    let ``an explicit null mirrored is Malformed, NOT Unspecified`` () =
        let mirrored = mirroredOf (oneRow "    mirrored:")

        Assert.Equal(Registry.MirrorMalformed "", mirrored)
        Assert.NotEqual(Registry.MirrorUnspecified, mirrored)

    // --- Load failures stay load failures (FR-007 / Constitution VIII). ---

    [<Fact>]
    let ``a missing file is an Error, not an exception`` () =
        match SkillRegistryDocument.load (Path.Combine(TestShared.tempDirectory (), "nope.yml")) with
        | Error error -> Assert.Contains("not found", error.Message)
        | Ok _ -> failwith "expected Error for a missing file"

    [<Fact>]
    let ``malformed YAML is an Error, not an exception`` () =
        match SkillRegistryDocument.load (write "schemaVersion: 1\nskills: [unclosed") with
        | Error error -> Assert.Contains("syntax error", error.Message)
        | Ok _ -> failwith "expected Error for malformed YAML"

    [<Fact>]
    let ``a missing schemaVersion is an Error`` () =
        match SkillRegistryDocument.load (write "skills: []") with
        | Error error -> Assert.Contains("schemaVersion", error.Message)
        | Ok _ -> failwith "expected Error for a missing schemaVersion"

    // --- detectKind (FR-005): dispatch on SHAPE, and stay conservative. ---

    [<Fact>]
    let ``detectKind sees the skill catalog by its root skills key`` () =
        Assert.Equal(SkillRegistryDocument.SkillRegistry, SkillRegistryDocument.detectKind fixturePath)

    /// The dependency registry must keep taking the path it always took — `.github`'s
    /// contract-coherence gate runs `registry validate` on it on every push.
    [<Fact>]
    let ``detectKind leaves the dependency registry alone`` () =
        let dependencies =
            Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "registry", "dependencies.yml")

        Assert.Equal(SkillRegistryDocument.DependencyRegistry, SkillRegistryDocument.detectKind dependencies)

    /// Detection must never INVENT a diagnostic. An unreadable/malformed file is reported as
    /// the dependency kind so it flows into the existing path and yields the existing load
    /// error — a parse failure is not a content diagnostic (Constitution VIII).
    [<Theory>]
    [<InlineData "not: [valid">]
    [<InlineData "">]
    [<InlineData "- a plain sequence root">]
    let ``detectKind falls back to the dependency registry for anything it cannot read`` (body: string) =
        Assert.Equal(SkillRegistryDocument.DependencyRegistry, SkillRegistryDocument.detectKind (write body))

    [<Fact>]
    let ``detectKind on a missing file does not throw`` () =
        Assert.Equal(
            SkillRegistryDocument.DependencyRegistry,
            SkillRegistryDocument.detectKind (Path.Combine(TestShared.tempDirectory (), "nope.yml"))
        )
