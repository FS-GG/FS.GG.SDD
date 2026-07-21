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

    let private addition id scope sha256 : ProductSkillManifest.ProductManifestEntry =
        { Id = id
          Scope = scope
          Sha256 = sha256
          ResolvablePath = Some(".agents/skills/" + id + "/SKILL.md")
          MaterializesWhen = "always"
          SuppliedBy = None }

    [<Fact>]
    let ``amend folds additions in, preserves the provider row, sorts by id`` () =
        let amended =
            ProductSkillManifest.amend
                providerManifest
                [ addition "workRoadmap" "process" "bbbb"
                  addition "fs-gg-playtest" "product" "cccc" ]

        match amended with
        | None -> failwith "Expected the amend to succeed on a well-formed provider manifest."
        | Some text ->
            let _, entries =
                match ProductSkillManifest.tryParse text with
                | Ok result -> result
                | Error message -> failwith $"Expected the amended manifest to parse: {message}"

            let ids = entries |> List.map (fun e -> e.Id)
            // All three declared, id-sorted, deterministic.
            Assert.Equal<string list>([ "fs-gg-elmish"; "fs-gg-playtest"; "workRoadmap" ], ids)

            // The provider row is preserved verbatim — its predicate and supplied-by survive.
            let elmish = entries |> List.find (fun e -> e.Id = "fs-gg-elmish")
            Assert.Equal("profile in [app, game]", elmish.MaterializesWhen)
            Assert.Equal(Some "template/product-skills/fs-gg-elmish/", elmish.SuppliedBy)

            // The additions carry their digest and the canonical `always` predicate.
            let roadmap = entries |> List.find (fun e -> e.Id = "workRoadmap")
            Assert.Equal("bbbb", roadmap.Sha256)
            Assert.Equal("always", roadmap.MaterializesWhen)
            Assert.Equal(Some ".agents/skills/workRoadmap/SKILL.md", roadmap.ResolvablePath)

    [<Fact>]
    let ``amend never duplicates an already-declared id (existing declaration wins)`` () =
        // A provider that already declares `fs-gg-elmish`: an addition of the same id is dropped, so
        // the provider's authoritative digest/predicate is not clobbered.
        let amended =
            ProductSkillManifest.amend providerManifest [ addition "fs-gg-elmish" "product" "zzzz" ]
            |> Option.defaultWith (fun () -> failwith "Expected the amend to succeed.")

        let _, entries =
            ProductSkillManifest.tryParse amended
            |> function
                | Ok result -> result
                | Error message -> failwith message

        let elmish = entries |> List.filter (fun e -> e.Id = "fs-gg-elmish")
        Assert.Single(elmish) |> ignore
        Assert.Equal("aaaa", elmish.Head.Sha256) // the provider's digest, not the addition's

    [<Fact>]
    let ``amend fails closed on an unparseable provider manifest (never overwrites with a guess)`` () =
        Assert.Equal(None, ProductSkillManifest.amend "{ not valid json" [ addition "workRoadmap" "process" "bbbb" ])

    [<Fact>]
    let ``serialize is deterministic, sorted, with a single trailing LF`` () =
        let entries =
            [ addition "b-skill" "product" "222"; addition "a-skill" "product" "111" ]

        let text = ProductSkillManifest.serialize 1 entries
        Assert.EndsWith("}\n", text)
        Assert.False(text.EndsWith("}\n\n"))
        // a-skill sorts before b-skill regardless of input order.
        Assert.True(text.IndexOf("a-skill") < text.IndexOf("b-skill"))
