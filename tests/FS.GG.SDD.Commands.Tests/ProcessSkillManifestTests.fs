namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.Internal
open Xunit

/// 072 (ADR-0017 P2 / FS.GG.SDD#109): the emitted process `skill-manifest` and its
/// drift guard. The committed `.claude/skills/skill-manifest.json` is pinned to the
/// seeded fs-gg-sdd-* set (ids == SeededSkills.skillNames), to the authored SKILL.md
/// bytes (per-entry sha256 recomputed from disk), and to a fresh serialization
/// (staleness), so it can never silently drift the way `.github`'s bootstrapped
/// registry rows did. Real-filesystem reads against the repo tree.
module ProcessSkillManifestTests =

    // THE TRACKED SOURCE ROOT, SPELLED OUT (FS.GG.SDD#771). This was `.agents` — the
    // provider-source root of the orchestrated scaffold lane, which ADR-0067 §6 turns into
    // a generated VIEW of `.claude/skills`: untracked, git-ignored, and absent in a bare
    // checkout. A test that read the manifest through the view would pass locally and fail
    // by NOT FINDING A FILE anywhere that had not run `scripts/skill-view generate` — the
    // silent-absence class the ADR-0067 rewrite exists to remove, and FS-GG/.github#1715's
    // blocker B5 one layer down.
    //
    // Deliberately a LITERAL and not `List.head Fsgg.Schemas.agentSkillRoots` (the
    // derivation `RegistrySkillManifest.manifestPath` and `materialize-skill-roots.fsx`
    // share): a guard that re-derives the path from the code it guards cannot notice the
    // path moving.
    //
    // WHAT THIS SPELLING ACTUALLY GUARDS, STATED PRECISELY. It reds if the committed file
    // goes ABSENT or STALE at this path — not if the derivation moves and a byte-identical
    // copy is written to the new home, which would leave this green. The tripwire for the
    // derivation itself is `FS.GG.Contracts.Tests.SchemaVersionConstantTests`'s
    // `agentSkillRoots is the declared three-root set`, which pins the whole list INCLUDING
    // ITS ORDER, so the head cannot move silently in the first place. Two guards, two
    // subjects; this one is about the artifact, that one is about the declaration.
    let private committedPath =
        Path.Combine(TestSupport.repoRoot, ".claude", "skills", "skill-manifest.json")

    // Normalize CRLF → LF so the guard tolerates a `core.autocrlf` checkout of the
    // LF-authored artifact (matching `Fsgg.SkillMirror.sha256` / feature 070 and the
    // `registry skill-manifest --check` comparison), rather than spuriously reddening.
    let private committedText () =
        File.ReadAllText(committedPath).Replace("\r\n", "\n")

    let private committedDoc () = JsonDocument.Parse(committedText ())

    let private skills () =
        [ for entry in committedDoc().RootElement.GetProperty("skills").EnumerateArray() -> entry ]

    let private prop (name: string) (entry: JsonElement) =
        match entry.GetProperty(name).GetString() with
        | null -> ""
        | value -> value

    // Same null-safety as `prop`, for the nested `files[]` rows (ADR-0017 v2, FS.GG.SDD#727).
    let private fileProp (name: string) (file: JsonElement) =
        match file.GetProperty(name).GetString() with
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

    // ---------- FR-003 / AC-003: schema v2, org-consumable shape ----------

    [<Fact>]
    let ``manifest declares schemaVersion 2 and the resolvable path shape`` () =
        let root = committedDoc().RootElement
        Assert.Equal(Fsgg.Schemas.skillManifestVersion, root.GetProperty("schemaVersion").GetInt32())
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32())

        for entry in skills () do
            let id = prop "id" entry
            Assert.Equal($".agents/skills/{id}/SKILL.md", prop "resolvablePath" entry)

    // ---------- ADR-0017 v2 / FS.GG.SDD#727: the COMPLETE declared file set ----------

    // Coverage is total or the document is a lie: a v2 manifest that declares a skill and omits
    // its file set claims a completeness it does not carry. `SkillManifestV2` makes that
    // unrepresentable in the type; this asserts it on the ARTIFACT, which is what other repos read.
    [<Fact>]
    let ``every entry declares a non-empty file set`` () =
        for entry in skills () do
            let files = [ for f in entry.GetProperty("files").EnumerateArray() -> f ]
            Assert.NotEmpty files

    // The superset invariant that keeps v2 readable by a v1 consumer: the row-level `sha256` is
    // RETAINED and still means the `SKILL.md` digest, so it cannot drift from the `files[]` row
    // that now also carries it. If these two ever disagree the document says two things at once.
    [<Fact>]
    let ``each entry's SKILL_md file row carries the same digest as its top-level sha256`` () =
        for entry in skills () do
            let files = [ for f in entry.GetProperty("files").EnumerateArray() -> f ]

            let skillMd = files |> List.find (fun f -> fileProp "path" f = "SKILL.md")

            Assert.Equal(prop "sha256" entry, fileProp "sha256" skillMd)

    // Every declared file digest is recomputed FROM DISK, the same way the top-level `sha256`
    // already was — so the file set is pinned to authored bytes and not merely to itself.
    [<Fact>]
    let ``each declared file digest matches the authored bytes on disk`` () =
        for entry in skills () do
            let id = prop "id" entry

            for f in entry.GetProperty("files").EnumerateArray() do
                let rel = fileProp "path" f

                let authored =
                    File.ReadAllText(Path.Combine(TestSupport.repoRoot, ".claude", "skills", id, rel))

                Assert.Equal(Fsgg.SkillMirror.sha256 authored, fileProp "sha256" f)

    // The declared set is the WHOLE set. A file present under the authored skill directory but
    // absent from `files[]` would be a file no digest authorises, which is exactly the state v2
    // exists to make impossible — and the state `SkillMirror.verifyFileSet` reds on.
    [<Fact>]
    let ``the declared file set equals what the authored skill directory actually carries`` () =
        for entry in skills () do
            let id = prop "id" entry
            let dir = Path.Combine(TestSupport.repoRoot, ".claude", "skills", id)

            let onDisk =
                Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                |> Seq.map (fun f -> Path.GetRelativePath(dir, f).Replace('\\', '/'))
                |> List.ofSeq
                |> List.sort

            let declaredPaths =
                [ for f in entry.GetProperty("files").EnumerateArray() -> fileProp "path" f ]
                |> List.sort

            Assert.Equal<string list>(onDisk, declaredPaths)

    // v2 IS A SUPERSET OF v1 ON THE WIRE, and the property ORDER is part of that promise. Every v1
    // key keeps its v1 position and `files` is appended, so a reader that scans these rows
    // positionally sees the v1 document it already parses with trailing content it ignores. Some
    // readers in the org FAIL OPEN when a row stops matching, so an insertion here would drop rows
    // out of another repo's guard silently rather than reddening it.
    [<Fact>]
    let ``v1 keys keep their v1 order and files is appended last`` () =
        for entry in skills () do
            let names = [ for p in entry.EnumerateObject() -> p.Name ]

            Assert.Equal<string list>(
                [ "id"; "scope"; "sha256"; "resolvablePath"; "materializes-when"; "files" ],
                names
            )

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
