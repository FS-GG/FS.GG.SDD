namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

/// ADR-0063 / FS.GG.SDD#623: the delivered owner-skill manifest parser (reads the three-state
/// `mirrored` field on top of the driver shape) and the parameter `materializes-when` predicate
/// evaluator — the `profile in [..]` grammar the scaffold-time materializer reads, and the
/// fail-closed rule that a predicate this CLI cannot evaluate yields `None` (skip), never a
/// default materialize.
module GameSkillManifestTests =

    // A representative delivered manifest (FS.GG.Game.Skills 0.1.0 shape): a mirrored:false product
    // row (delivered), a mirrored:true product row (listed but delivered via the provider mirror),
    // and a row with no `mirrored` key (unclassified).
    let private manifest =
        """{
  "schemaVersion": 1,
  "skills": [
    {
      "id": "fs-gg-playtest",
      "scope": "product",
      "sha256": "0541a9f30328732d998dfd0bb5a1e79d8887d7cf2f3b42cc03324f0de5adbb41",
      "mirrored": false,
      "resolvablePath": ".agents/skills/fs-gg-playtest/SKILL.md",
      "materializes-when": "profile in [game, sample-pack]",
      "supplied-by": "template/product-skills/fs-gg-playtest/"
    },
    {
      "id": "fs-gg-audio",
      "scope": "product",
      "sha256": "e9fff88a3be86c2d95829357ffc180e3c53c18f2872b4f84e792de5a24f73bb7",
      "mirrored": true,
      "materializes-when": "profile in [app, sample-pack, game]"
    },
    {
      "id": "fs-gg-unclassified",
      "scope": "product",
      "sha256": "abc",
      "materializes-when": "always"
    }
  ]
}"""

    [<Fact>]
    let ``tryParse reads the three-state mirrored field and rows verbatim`` () =
        match GameSkillManifest.tryParse manifest with
        | Error message -> failwithf "expected Ok, got Error %s" message
        | Ok parsed ->
            Assert.Equal(1, parsed.SchemaVersion)
            Assert.Equal(3, List.length parsed.Skills)

            let delivered = parsed.Skills |> List.find (fun s -> s.Id = "fs-gg-playtest")
            Assert.Equal("product", delivered.Scope)
            Assert.Equal(Some false, delivered.Mirrored)
            Assert.Equal("profile in [game, sample-pack]", delivered.MaterializesWhen)
            Assert.Equal(Some "template/product-skills/fs-gg-playtest/", delivered.SuppliedBy)

            let mirroredRow = parsed.Skills |> List.find (fun s -> s.Id = "fs-gg-audio")
            Assert.Equal(Some true, mirroredRow.Mirrored)

            // Absent `mirrored:` ⇒ None (unclassified), never coerced to false.
            let unclassified = parsed.Skills |> List.find (fun s -> s.Id = "fs-gg-unclassified")
            Assert.Equal(None, unclassified.Mirrored)

    [<Fact>]
    let ``tryParse fails on a missing schemaVersion`` () =
        match GameSkillManifest.tryParse """{ "skills": [] }""" with
        | Ok _ -> failwith "expected Error for a missing schemaVersion"
        | Error _ -> ()

    [<Fact>]
    let ``tryParse fails on malformed JSON`` () =
        match GameSkillManifest.tryParse "{ not json" with
        | Ok _ -> failwith "expected Error for malformed JSON"
        | Error _ -> ()

    [<Fact>]
    let ``tryParse drops a row lacking id/sha256/materializes-when`` () =
        let text =
            """{ "schemaVersion": 1, "skills": [ { "scope": "product", "sha256": "x" } ] }"""

        match GameSkillManifest.tryParse text with
        | Error message -> failwithf "expected Ok, got Error %s" message
        | Ok parsed -> Assert.Empty parsed.Skills

    // ---------- ProductPredicate ----------

    let private paramsOf (pairs: (string * string) list) = Map.ofList pairs

    [<Theory>]
    [<InlineData("always", true)>]
    [<InlineData("false", false)>]
    let ``evaluate resolves the literal predicates`` (predicate: string) (expected: bool) =
        Assert.Equal(Some expected, ProductPredicate.evaluate predicate Map.empty)

    [<Fact>]
    let ``evaluate resolves an in-list membership over the parameter set`` () =
        Assert.Equal(Some true, ProductPredicate.evaluate "profile in [game, sample-pack]" (paramsOf [ "profile", "game" ]))
        Assert.Equal(Some true, ProductPredicate.evaluate "profile in [game, sample-pack]" (paramsOf [ "profile", "sample-pack" ]))
        Assert.Equal(Some false, ProductPredicate.evaluate "profile in [game, sample-pack]" (paramsOf [ "profile", "app" ]))

    [<Fact>]
    let ``evaluate treats an unset parameter as matching no declared value`` () =
        // An absent `profile` reads as the empty string, so an `in [..]` test is false (not None).
        Assert.Equal(Some false, ProductPredicate.evaluate "profile in [game, sample-pack]" Map.empty)

    [<Fact>]
    let ``evaluate resolves equality and inequality atoms`` () =
        Assert.Equal(Some true, ProductPredicate.evaluate "profile == game" (paramsOf [ "profile", "game" ]))
        Assert.Equal(Some false, ProductPredicate.evaluate "profile == game" (paramsOf [ "profile", "app" ]))
        Assert.Equal(Some true, ProductPredicate.evaluate "profile != app" (paramsOf [ "profile", "game" ]))
        Assert.Equal(Some true, ProductPredicate.evaluate "feedback == true" (paramsOf [ "feedback", "true" ]))

    [<Fact>]
    let ``evaluate resolves composed and/or over parameters`` () =
        let p = paramsOf [ "profile", "game"; "feedback", "true" ]
        Assert.Equal(Some true, ProductPredicate.evaluate "profile in [game] and feedback == true" p)
        Assert.Equal(Some false, ProductPredicate.evaluate "profile in [game] and feedback == false" p)
        Assert.Equal(Some true, ProductPredicate.evaluate "profile == app or profile == game" p)

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("sometimes")>]
    [<InlineData("profile in [game] and x == y or z == w")>]
    [<InlineData("count(x) > 2")>]
    let ``evaluate returns None for a predicate it cannot evaluate (fail closed)`` (predicate: string) =
        Assert.Equal(None, ProductPredicate.evaluate predicate (paramsOf [ "profile", "game" ]))
