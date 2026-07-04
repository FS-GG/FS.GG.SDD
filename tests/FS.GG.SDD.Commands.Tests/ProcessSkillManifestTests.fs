namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.Internal
open Xunit

/// 072 (ADR-0017 P2 / FS.GG.SDD#109): the emitted process `skill-manifest` and its
/// drift guard. The committed `.agents/skills/skill-manifest.json` is pinned to the
/// seeded fs-gg-sdd-* set (ids == SeededSkills.skillNames), to the authored SKILL.md
/// bytes (per-entry sha256 recomputed from disk), and to a fresh serialization
/// (staleness), so it can never silently drift the way `.github`'s bootstrapped
/// registry rows did. Real-filesystem reads against the repo tree.
module ProcessSkillManifestTests =

    let private committedPath =
        Path.Combine(TestSupport.repoRoot, ".agents", "skills", "skill-manifest.json")

    let private committedText () = File.ReadAllText committedPath

    let private committedDoc () = JsonDocument.Parse(committedText ())

    let private skills () =
        [ for entry in committedDoc().RootElement.GetProperty("skills").EnumerateArray() -> entry ]

    let private prop (name: string) (entry: JsonElement) =
        match entry.GetProperty(name).GetString() with
        | null -> ""
        | value -> value

    // ---------- FR-001 / AC-001: membership == the seeded set ----------

    [<Fact>]
    let ``manifest ids equal the seeded skill set exactly`` () =
        let ids = skills () |> List.map (prop "id") |> List.sort
        Assert.Equal<string list>(SeededSkills.skillNames, ids)

    [<Fact>]
    let ``manifest includes troubleshooting and excludes the product-internal project skill`` () =
        let ids = skills () |> List.map (prop "id") |> Set.ofList
        Assert.Contains("fs-gg-sdd-troubleshooting", ids)
        Assert.DoesNotContain("fs-gg-sdd-project", ids)
        Assert.Equal(16, ids.Count)

    // ---------- FR-002 / AC-002: each sha256 == canonical digest of the authored SKILL.md ----------

    [<Fact>]
    let ``each entry sha256 matches the canonical digest of its authored SKILL.md`` () =
        for entry in skills () do
            let id = prop "id" entry

            let authored =
                File.ReadAllText(Path.Combine(TestSupport.repoRoot, ".claude", "skills", id, "SKILL.md"))

            Assert.Equal(Fsgg.SkillMirror.sha256 authored, prop "sha256" entry)

    // ---------- FR-002/FR-004 / AC-002/AC-004: scope + canonical materializes-when ----------

    [<Fact>]
    let ``every entry is scope process with the canonical always predicate`` () =
        for entry in skills () do
            Assert.Equal("process", prop "scope" entry)
            let mw = prop "materializes-when" entry
            Assert.Equal("always", mw)
            // ADR-0017 canonical grammar — never the C-style form that broke Rendering#77.
            Assert.DoesNotContain("(", mw)
            Assert.DoesNotContain("&&", mw)
            Assert.DoesNotContain("||", mw)
            Assert.DoesNotContain("\"", mw)

    // ---------- FR-003 / AC-003: schema v1, org-consumable shape ----------

    [<Fact>]
    let ``manifest declares schemaVersion 1 and the resolvable path shape`` () =
        let root = committedDoc().RootElement
        Assert.Equal(Fsgg.Schemas.skillManifestVersion, root.GetProperty("schemaVersion").GetInt32())
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32())

        for entry in skills () do
            let id = prop "id" entry
            Assert.Equal($".agents/skills/{id}/SKILL.md", prop "resolvablePath" entry)

    // ---------- FR-005 / AC-005: determinism + sort order + LF ----------

    [<Fact>]
    let ``serialization is deterministic, sorted by id, and LF`` () =
        let a = SkillManifestJson.serialize (ProcessSkillManifest.build ())
        let b = SkillManifestJson.serialize (ProcessSkillManifest.build ())
        Assert.Equal(a, b)
        Assert.DoesNotContain("\r", a)

        let ids = skills () |> List.map (prop "id")
        Assert.Equal<string list>(List.sort ids, ids)

    // ---------- FR-006/FR-007d / AC-006/AC-007: the staleness guard ----------

    [<Fact>]
    let ``the committed manifest is byte-identical to a fresh generation`` () =
        let fresh = SkillManifestJson.serialize (ProcessSkillManifest.build ())
        Assert.Equal(fresh, committedText ())
