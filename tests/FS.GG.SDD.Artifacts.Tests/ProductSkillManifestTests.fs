namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

/// ADR-0063 tail / skill-union coherence: the product `skill-manifest.json` union codec. SDD, the
/// sole materialize authority, folds the driver + owner-sourced skills it lays down into the
/// provider-shipped product manifest, so the consumer skill-union gate sees no [dangling] skill.
module ProductSkillManifestTests =

    // A provider-shipped product manifest: one product skill, carrying the full shipped field shape
    // (including `supplied-by`, which a faithful round-trip must preserve).
    let private providerManifest =
        """{
  "schemaVersion": 1,
  "skills": [
    {
      "id": "fs-gg-elmish",
      "scope": "product",
      "sha256": "aaaa",
      "resolvablePath": ".agents/skills/fs-gg-elmish/SKILL.md",
      "materializes-when": "profile in [app, game]",
      "supplied-by": "template/product-skills/fs-gg-elmish/"
    }
  ]
}
"""

    // FS.GG.SDD#727 shipped ADR-0017 v2: the same row, plus the COMPLETE file set. This is the
    // document #739 was filed about — `amend` re-emitted the v2 header over rows stripped of it.
    let private providerManifestV2 =
        """{
  "schemaVersion": 2,
  "skills": [
    {
      "id": "fs-gg-elmish",
      "scope": "product",
      "sha256": "aaaa",
      "resolvablePath": ".agents/skills/fs-gg-elmish/SKILL.md",
      "materializes-when": "profile in [app, game]",
      "supplied-by": "template/product-skills/fs-gg-elmish/",
      "files": [
        { "path": "SKILL.md", "sha256": "aaaa" },
        { "path": "references/deep-detail.md", "sha256": "dddd" },
        { "path": "agents/reviewer.yaml", "sha256": "eeee" }
      ]
    }
  ]
}
"""

    let private file path sha256 : ProductSkillManifest.ProductManifestFile = { Path = path; Sha256 = sha256 }

    let private addition id scope sha256 : ProductSkillManifest.ProductManifestEntry =
        { Id = id
          Scope = scope
          Sha256 = sha256
          ResolvablePath = Some(".agents/skills/" + id + "/SKILL.md")
          MaterializesWhen = "always"
          SuppliedBy = None
          Files = [ file "SKILL.md" sha256 ] }

    let private parsed text =
        match ProductSkillManifest.tryParse text with
        | Ok result -> result
        | Error message -> failwith $"Expected the manifest to parse: {message}"

    let private amended existingText additions =
        match ProductSkillManifest.amend existingText additions with
        | Ok text -> text
        | Error refusal -> failwith $"Expected the amend to succeed, got %A{refusal}"

    let private entryFor id (entries: ProductSkillManifest.ProductManifestEntry list) =
        entries |> List.find (fun e -> e.Id = id)

    [<Fact>]
    let ``amend folds additions in, preserves the provider row, sorts by id`` () =
        let text =
            amended
                providerManifest
                [ addition "workRoadmap" "process" "bbbb"
                  addition "fs-gg-playtest" "product" "cccc" ]

        let _, entries = parsed text

        let ids = entries |> List.map (fun e -> e.Id)
        // All three declared, id-sorted, deterministic.
        Assert.Equal<string list>([ "fs-gg-elmish"; "fs-gg-playtest"; "workRoadmap" ], ids)

        // The provider row is preserved verbatim — its predicate and supplied-by survive.
        let elmish = entryFor "fs-gg-elmish" entries
        Assert.Equal("profile in [app, game]", elmish.MaterializesWhen)
        Assert.Equal(Some "template/product-skills/fs-gg-elmish/", elmish.SuppliedBy)

        // The additions carry their digest and the canonical `always` predicate.
        let roadmap = entryFor "workRoadmap" entries
        Assert.Equal("bbbb", roadmap.Sha256)
        Assert.Equal("always", roadmap.MaterializesWhen)
        Assert.Equal(Some ".agents/skills/workRoadmap/SKILL.md", roadmap.ResolvablePath)

    [<Fact>]
    let ``amend never duplicates an already-declared id (existing declaration wins)`` () =
        // A provider that already declares `fs-gg-elmish`: an addition of the same id is dropped, so
        // the provider's authoritative digest/predicate is not clobbered.
        let _, entries =
            parsed (amended providerManifest [ addition "fs-gg-elmish" "product" "zzzz" ])

        let elmish = entries |> List.filter (fun e -> e.Id = "fs-gg-elmish")
        Assert.Single(elmish) |> ignore
        Assert.Equal("aaaa", elmish.Head.Sha256) // the provider's digest, not the addition's

    [<Fact>]
    let ``amend fails closed on an unparseable provider manifest (never overwrites with a guess)`` () =
        match ProductSkillManifest.amend "{ not valid json" [ addition "workRoadmap" "process" "bbbb" ] with
        | Error(ProductSkillManifest.ManifestUnparseable _) -> ()
        | other -> failwith $"Expected ManifestUnparseable, got %A{other}"

    [<Fact>]
    let ``serialize is deterministic, sorted, with a single trailing LF`` () =
        let entries =
            [ addition "b-skill" "product" "222"; addition "a-skill" "product" "111" ]

        let text = ProductSkillManifest.serialize 1 entries
        Assert.EndsWith("}\n", text)
        Assert.False(text.EndsWith("}\n\n"))
        // a-skill sorts before b-skill regardless of input order.
        Assert.True(text.IndexOf("a-skill") < text.IndexOf("b-skill"))

    // ----- FS.GG.SDD#739: the v2 file set survives the union -----

    // AC5, and the RED leg the whole item turns on. Before #739 this assertion could not fail,
    // because `tryParse` never read `files` and `serialize` never wrote one: the amended document
    // came out `"schemaVersion": 2` with rows carrying no file set at all, and every other test in
    // this file stayed green while it happened. This is the check that reds on that.
    [<Fact>]
    let ``amend preserves a v2 provider row's declared file set`` () =
        let text = amended providerManifestV2 [ addition "workRoadmap" "process" "bbbb" ]

        let version, entries = parsed text
        Assert.Equal(2, version)

        let elmish = entryFor "fs-gg-elmish" entries

        // Every declared file survives, with its digest, sorted by path (the deterministic shape
        // `SkillManifestJson` emits and `skill-union-assert.sh` reads).
        Assert.Equal<ProductSkillManifest.ProductManifestFile list>(
            [ file "SKILL.md" "aaaa"
              file "agents/reviewer.yaml" "eeee"
              file "references/deep-detail.md" "dddd" ],
            elmish.Files
        )

        // The header's claim is now backed by the rows: `files` is literally present.
        Assert.Contains("\"files\"", text)

    // AC1: a v2 document survives parse → amend → serialize byte-for-byte in its canonical form.
    // Amending with an addition already declared changes nothing, so the ONLY difference a second
    // pass could show is a property the codec silently dropped.
    [<Fact>]
    let ``a v2 manifest round-trips byte-for-byte through amend`` () =
        let canonical =
            let version, entries = parsed providerManifestV2
            ProductSkillManifest.serialize version entries

        // Anchored to the RAW provider document, not just to `canonical`: a codec that dropped
        // `files` on BOTH sides would round-trip perfectly and still be the #739 defect. `dddd` and
        // `eeee` exist nowhere but inside the declared file set.
        Assert.Contains("\"dddd\"", canonical)
        Assert.Contains("\"eeee\"", canonical)

        // An addition whose id the provider already declares is dropped, so this is a pure re-emit.
        let reEmitted = amended canonical [ addition "fs-gg-elmish" "product" "zzzz" ]

        Assert.Equal(canonical, reEmitted)
        Assert.Equal(canonical, amended reEmitted [])

    // AC2's codec half: at v2 every row declares a file set, so an addition without one is REFUSED
    // rather than folded in to produce a v2 document with v1 rows — #739 by its second route.
    [<Fact>]
    let ``amend refuses a v2 fold-in whose additions carry no file set`` () =
        let fileless =
            { addition "workRoadmap" "process" "bbbb" with
                Files = [] }

        match ProductSkillManifest.amend providerManifestV2 [ fileless ] with
        | Error(ProductSkillManifest.AdditionsMissingFileSet(2, [ "workRoadmap" ])) -> ()
        | other -> failwith $"Expected AdditionsMissingFileSet, got %A{other}"

    // AC3: v1 is untouched by all of the above. A v1 document amended with additions that DO carry a
    // file set stays v1 — it never grows a v2 property — and its own rows are unchanged.
    [<Fact>]
    let ``a v1 manifest still round-trips as v1, gaining no files property`` () =
        let text = amended providerManifest [ addition "workRoadmap" "process" "bbbb" ]

        let version, entries = parsed text
        Assert.Equal(1, version)
        Assert.DoesNotContain("\"files\"", text)

        for entry in entries do
            Assert.Empty(entry.Files)

        // And the pure v1 re-emit is stable.
        let canonical =
            let version, entries = parsed providerManifest
            ProductSkillManifest.serialize version entries

        Assert.Equal(canonical, amended canonical [ addition "fs-gg-elmish" "product" "zzzz" ])

    // AC4's codec half: a schema this codec cannot re-emit faithfully is refused by NAME, so the
    // caller can say which one and why. `tryParse` stays tolerant — reading a future document to
    // inspect it loses nothing; only rewriting it does.
    [<Fact>]
    let ``amend refuses a schemaVersion it cannot round-trip, while tryParse still reads it`` () =
        let future =
            providerManifestV2.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3")

        let version, entries = parsed future
        Assert.Equal(3, version)
        Assert.Equal(1, List.length entries)

        match ProductSkillManifest.amend future [ addition "workRoadmap" "process" "bbbb" ] with
        | Error(ProductSkillManifest.SchemaVersionUnroundTrippable 3) -> ()
        | other -> failwith $"Expected SchemaVersionUnroundTrippable 3, got %A{other}"

    // A declared `files` array that cannot be read is fail-closed at the PARSE, not a dropped row:
    // rewriting a manifest from the half of it that parsed is precisely the wrong-document outcome.
    [<Fact>]
    let ``tryParse refuses a files row missing its digest, rather than dropping it`` () =
        let broken =
            """{
  "schemaVersion": 2,
  "skills": [
    { "id": "fs-gg-elmish", "scope": "product", "sha256": "aaaa", "materializes-when": "always",
      "files": [ { "path": "SKILL.md" } ] }
  ]
}
"""

        match ProductSkillManifest.tryParse broken with
        | Error message -> Assert.Contains("sha256", message)
        | Ok _ -> failwith "Expected an unreadable `files` row to fail the parse, not be dropped."
