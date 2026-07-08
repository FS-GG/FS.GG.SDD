namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text.Json
open FS.GG.SDD.Commands.Internal
open Xunit

/// Feature 073 (ADR-0018) + feature 092 (ADR-0026) drift guards. Two invariants:
///   (T001/FR-002) each of the taxonomy doc's machine-emitted `readiness/<id>/…` lists is exactly
///   the corresponding partition of the `generatedView` catalog of `release-readiness.json` —
///   regenerable when `durableGenerated` is false, durable-generated when true. The doc is a
///   drift-guarded projection, never a second source of truth.
///   (T002/FR-003/FR-005) `init` seeds a no-clobber `.gitignore` whose bytes equal
///   `Foundation.gitignoreSeedText`, and the taxonomy doc's seed fragment stays in sync.
///
/// NOTE: byte-equality against the seed constant proves the constant was *copied*, never that it
/// *works*. That the `!readiness/*/ship-verdict.json` negation actually fires is proved only by
/// `GitignoreNegationTests`, which runs real git (feature 092 / research D1-D2).
module ArtifactTaxonomyTests =

    let private taxonomyDoc =
        TestSupport.readRelative TestSupport.repoRoot "docs/reference/artifact-taxonomy.md"

    let private releaseReadiness =
        TestSupport.readRelative TestSupport.repoRoot "docs/release/release-readiness.json"

    // The generated-view source-artifact paths in the release catalog, partitioned on the
    // `durableGenerated` flag (ADR-0026 §4: the doc's tables stay catalog-derived).
    let private catalogGeneratedViewPaths (durableGenerated: bool) : Set<string> =
        use doc = JsonDocument.Parse releaseReadiness

        doc.RootElement.GetProperty("catalog").EnumerateArray()
        |> Seq.choose (fun entry ->
            let isDurable =
                match entry.TryGetProperty "durableGenerated" with
                | true, value -> value.GetBoolean()
                | _ -> false

            match entry.TryGetProperty "sourceArtifact" with
            | true, src when isDurable = durableGenerated ->
                match src.TryGetProperty "kind" with
                | true, k when String.Equals(k.GetString(), "generatedView") ->
                    Option.ofObj (src.GetProperty("path").GetString())
                | _ -> None
            | _ -> None)
        |> Set.ofSeq

    /// The doc's `readiness/<id>/…` lines within one `## `-delimited section. Section-scoped so the
    /// durable-generated table and the regenerable block are distinguishable projections rather than
    /// one merged list.
    let private docReadinessPathsInSection (headingPrefix: string) : Set<string> =
        let lines = taxonomyDoc.Replace("\r\n", "\n").Split('\n')

        let start =
            lines
            |> Array.tryFindIndex (fun l -> l.StartsWith("## " + headingPrefix, StringComparison.Ordinal))

        match start with
        | None -> Set.empty
        | Some startIndex ->
            let rest = lines |> Array.skip (startIndex + 1)

            let length =
                rest
                |> Array.tryFindIndex (fun l -> l.StartsWith("## ", StringComparison.Ordinal))
                |> Option.defaultValue rest.Length

            rest
            |> Array.take length
            |> Array.map (fun l -> l.Trim())
            |> Array.filter (fun l -> l.StartsWith "readiness/<id>/")
            |> Set.ofArray

    [<Fact>]
    let ``taxonomy regenerable readiness list equals the non-durable generatedView catalog`` () =
        let expected = catalogGeneratedViewPaths false
        let actual = docReadinessPathsInSection "Regenerable"

        Assert.False(Set.isEmpty expected, "expected a non-empty regenerable generatedView catalog")

        Assert.True(
            (expected = actual),
            $"artifact-taxonomy.md regenerable readiness list diverged from release-readiness.json.\n"
            + $"missing from doc: {Set.difference expected actual}\n"
            + $"extra in doc:     {Set.difference actual expected}"
        )

    [<Fact>]
    let ``taxonomy durable-generated list equals the durable generatedView catalog`` () =
        let expected = catalogGeneratedViewPaths true
        let actual = docReadinessPathsInSection "Durable generated"

        Assert.False(Set.isEmpty expected, "expected a non-empty durableGenerated generatedView catalog")

        Assert.True(
            (expected = actual),
            $"artifact-taxonomy.md durable-generated list diverged from release-readiness.json.\n"
            + $"missing from doc: {Set.difference expected actual}\n"
            + $"extra in doc:     {Set.difference actual expected}"
        )

    [<Fact>]
    let ``the two taxonomy partitions are disjoint and cover every catalogued generated view`` () =
        // The partition must be total: a generated view that slips out of both tables would be
        // invisible to the drift guard — the rot ADR-0018 pinned this doc against.
        let regenerable = catalogGeneratedViewPaths false
        let durable = catalogGeneratedViewPaths true

        Assert.Empty(Set.intersect regenerable durable)

        use doc = JsonDocument.Parse releaseReadiness

        let allGeneratedViews =
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

        Assert.Equal<Set<string>>(allGeneratedViews, Set.union regenerable durable)

    [<Fact>]
    let ``the ship verdict is the only durable-generated readiness view`` () =
        Assert.Equal<Set<string>>(Set.ofList [ "readiness/<id>/ship-verdict.json" ], catalogGeneratedViewPaths true)

    [<Fact>]
    let ``init seeds a no-clobber .gitignore equal to the seed constant`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let seeded = TestSupport.readRelative root ".gitignore"

        Assert.Equal(Foundation.gitignoreSeedText.Replace("\r\n", "\n"), seeded.Replace("\r\n", "\n"))

        // The *contents* rule plus its negation. `Assert.Contains "readiness/*/"` — the pre-092
        // assertion — is a substring of `readiness/*/*` and would pass with the negation absent,
        // present-but-inert, or correct. Assert the exact lines instead (research D2).
        let lines =
            seeded.Replace("\r\n", "\n").Split('\n') |> Array.map (fun l -> l.Trim())

        Assert.Contains("readiness/*/*", lines)
        Assert.Contains("!readiness/*/ship-verdict.json", lines)
        Assert.DoesNotContain("readiness/*/", lines) // the bare directory rule must be gone

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
