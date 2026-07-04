namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text.Json
open FS.GG.SDD.Commands.Internal
open Xunit

/// Feature 073 (ADR-0018) drift guards. Two invariants:
///   (T001/FR-002) the taxonomy doc's regenerable `readiness/<id>/…` list is exactly the
///   `generatedView` catalog of `release-readiness.json` — the doc is a drift-guarded
///   projection, never a second source of truth.
///   (T002/FR-003/FR-005) `init` seeds a no-clobber `.gitignore` whose bytes equal
///   `Foundation.gitignoreSeedText`, and the taxonomy doc's seed fragment stays in sync.
module ArtifactTaxonomyTests =

    let private taxonomyDoc =
        TestSupport.readRelative TestSupport.repoRoot "docs/reference/artifact-taxonomy.md"

    let private releaseReadiness =
        TestSupport.readRelative TestSupport.repoRoot "docs/release/release-readiness.json"

    // The generated-view source-artifact paths in the release catalog — the authoritative
    // regenerable readiness set.
    let private catalogGeneratedViewPaths () : Set<string> =
        use doc = JsonDocument.Parse releaseReadiness

        doc.RootElement.GetProperty("catalog").EnumerateArray()
        |> Seq.choose (fun entry ->
            match entry.TryGetProperty "sourceArtifact" with
            | true, src ->
                match src.TryGetProperty "kind" with
                | true, k when String.Equals(k.GetString(), "generatedView") ->
                    Option.ofObj (src.GetProperty("path").GetString())
                | _ -> None
            | _ -> None)
        |> Set.ofSeq

    // Every `readiness/<id>/…` path the taxonomy doc lists as regenerable.
    let private docReadinessPaths () : Set<string> =
        taxonomyDoc.Replace("\r\n", "\n").Split('\n')
        |> Array.map (fun l -> l.Trim())
        |> Array.filter (fun l -> l.StartsWith "readiness/<id>/")
        |> Set.ofArray

    [<Fact>]
    let ``taxonomy regenerable readiness list equals the release-readiness generatedView catalog`` () =
        let expected = catalogGeneratedViewPaths ()
        let actual = docReadinessPaths ()

        Assert.False(Set.isEmpty expected, "expected a non-empty generatedView catalog")

        Assert.True(
            (expected = actual),
            $"artifact-taxonomy.md regenerable readiness list diverged from release-readiness.json.\n"
            + $"missing from doc: {Set.difference expected actual}\n"
            + $"extra in doc:     {Set.difference actual expected}"
        )

    [<Fact>]
    let ``init seeds a no-clobber .gitignore equal to the seed constant`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let seeded = TestSupport.readRelative root ".gitignore"

        Assert.Equal(Foundation.gitignoreSeedText.Replace("\r\n", "\n"), seeded.Replace("\r\n", "\n"))
        Assert.Contains("readiness/*/", seeded)

    [<Fact>]
    let ``second init does not clobber an existing .gitignore`` () =
        let root = TestSupport.tempDirectory ()
        let custom = "# author owns this\nmy-secret/\n"
        File.WriteAllText(Path.Combine(root, ".gitignore"), custom)

        TestSupport.initializeProject root

        Assert.Equal(custom, (TestSupport.readRelative root ".gitignore").Replace("\r\n", "\n"))

    [<Fact>]
    let ``taxonomy doc seed fragment stays byte-identical to the seed constant`` () =
        // The doc embeds the seed fragment as a fenced block; assert the constant's content
        // appears verbatim so the doc and the seed cannot drift apart.
        let normalizedDoc = taxonomyDoc.Replace("\r\n", "\n")

        let normalizedSeed =
            Foundation.gitignoreSeedText.Replace("\r\n", "\n").TrimEnd('\n')

        Assert.Contains(normalizedSeed, normalizedDoc)
