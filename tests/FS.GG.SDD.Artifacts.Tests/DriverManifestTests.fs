namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

/// 108 / ADR-0054: the delivered driver manifest parser and the `materializes-when` predicate
/// evaluator — the shape the scaffold-time materializer reads, and the fail-closed rule that a
/// predicate this CLI cannot evaluate yields `None` (skip), never a default materialize.
module DriverManifestTests =

    // The delivered FS.GG.Drivers 0.8.0 manifest shape, used as the parse fixture.
    let private deliveredManifest =
        """{
  "schemaVersion": 1,
  "skills": [
    {
      "id": "work-roadmap",
      "scope": "driver",
      "sha256": "715609ab4d97337ee5250fb31e57159fb5d7b99a8c4ead0b712fd8c8c50b1677",
      "supplied-by": ".claude/skills/work-roadmap",
      "materializes-when": "always"
    },
    {
      "id": "work-board",
      "scope": "driver",
      "sha256": "7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b",
      "supplied-by": ".claude/skills/work-board",
      "materializes-when": "always"
    },
    {
      "id": "padd-item",
      "scope": "driver",
      "sha256": "4daf167ef061d9a27504ad212e4c9c42321f597c64143953b0c666f072092d9e",
      "supplied-by": ".claude/skills/padd-item",
      "materializes-when": "always"
    },
    {
      "id": "p-add",
      "scope": "operator",
      "sha256": "44de53fbacc74f6e8be0227cd45549f93a14ee9f3b12a39c63859ebf6d4a1f9e",
      "supplied-by": ".claude/skills/p-add",
      "materializes-when": "false"
    },
    {
      "id": "cut-nuget-release",
      "scope": "operator",
      "sha256": "1dd3dd74f875d01330002bafd7c26f872e0167d8caaba44978187869f42459df",
      "supplied-by": ".claude/skills/cut-nuget-release",
      "materializes-when": "false"
    },
    {
      "id": "drive-board",
      "scope": "operator",
      "sha256": "4ccacf65786b14ad5917981f0fb0a6a4d17aa62bb2fda65fbd1cef00fda3bac6",
      "supplied-by": ".claude/skills/drive-board",
      "materializes-when": "false"
    }
  ]
}"""

    [<Fact>]
    let ``tryParse reads the delivered manifest rows verbatim`` () =
        match DriverManifest.tryParse deliveredManifest with
        | Error message -> failwithf "expected Ok, got Error %s" message
        | Ok manifest ->
            Assert.Equal(1, manifest.SchemaVersion)
            Assert.Equal(6, List.length manifest.Skills)

            let workRoadmap = manifest.Skills |> List.find (fun s -> s.Id = "work-roadmap")
            Assert.Equal("driver", workRoadmap.Scope)
            Assert.Equal("always", workRoadmap.MaterializesWhen)
            Assert.Equal("715609ab4d97337ee5250fb31e57159fb5d7b99a8c4ead0b712fd8c8c50b1677", workRoadmap.Sha256)
            Assert.Equal(Some ".claude/skills/work-roadmap", workRoadmap.SuppliedBy)

            let workBoard = manifest.Skills |> List.find (fun s -> s.Id = "work-board")
            Assert.Equal("driver", workBoard.Scope)
            Assert.Equal("always", workBoard.MaterializesWhen)
            Assert.Equal("7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b", workBoard.Sha256)
            Assert.Equal(Some ".claude/skills/work-board", workBoard.SuppliedBy)

            let paddItem = manifest.Skills |> List.find (fun s -> s.Id = "padd-item")
            Assert.Equal("driver", paddItem.Scope)
            Assert.Equal("always", paddItem.MaterializesWhen)
            Assert.Equal("4daf167ef061d9a27504ad212e4c9c42321f597c64143953b0c666f072092d9e", paddItem.Sha256)
            Assert.Equal(Some ".claude/skills/padd-item", paddItem.SuppliedBy)

            for id in [ "drive-board"; "p-add"; "cut-nuget-release" ] do
                let operator = manifest.Skills |> List.find (fun s -> s.Id = id)
                Assert.Equal("operator", operator.Scope)
                Assert.Equal("false", operator.MaterializesWhen)

    [<Fact>]
    let ``tryParse fails on a missing schemaVersion`` () =
        match DriverManifest.tryParse """{ "skills": [] }""" with
        | Ok _ -> failwith "expected Error for a missing schemaVersion"
        | Error _ -> ()

    [<Fact>]
    let ``tryParse fails on malformed JSON`` () =
        match DriverManifest.tryParse "{ not json" with
        | Ok _ -> failwith "expected Error for malformed JSON"
        | Error _ -> ()

    [<Fact>]
    let ``tryParse fails closed on a row lacking id or materialization contract`` () =
        let text =
            """{ "schemaVersion": 1, "skills": [ { "scope": "driver", "sha256": "x" } ] }"""

        match DriverManifest.tryParse text with
        | Error _ -> ()
        | Ok _ -> failwith "expected malformed row to fail the manifest"

    let private schemaV2 fileRows treeSha =
        $"""{{ "schemaVersion": 2, "skills": [
  {{ "id": "work-board", "scope": "driver",
     "sha256": "7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b",
     "tree-sha256": "{treeSha}",
     "files": {fileRows},
     "materializes-when": "always" }}
] }}"""

    [<Fact>]
    let ``tryParse reads and binds the complete schema-v2 file directory`` () =
        let files =
            """[{"path":"SKILL.md","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":false},{"path":"references/host-loop.md","sha256":"d75f4e3ba80e6cf75b3f3fef5fe3d26eec86c117c0f21d8eeb4d8aed5982d325","executable":false}]"""

        let tree = "3947b090bc0b14f914137026960f29a890e2b81fea11ac44f37e08f8a131660f"

        match DriverManifest.tryParse (schemaV2 files tree) with
        | Error message -> failwithf "expected schema v2 to parse: %s" message
        | Ok manifest ->
            let entry = Assert.Single manifest.Skills
            Assert.Equal(Some tree, entry.TreeSha256)
            Assert.Equal<string list>([ "SKILL.md"; "references/host-loop.md" ], entry.Files |> List.map _.Path)

    [<Theory>]
    [<InlineData("""[{"path":"../escape","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":false}]""")>]
    [<InlineData("""[{"path":"SKILL.md","sha256":"bad","executable":false}]""")>]
    [<InlineData("""[{"path":"SKILL.md","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":"false"}]""")>]
    let ``tryParse fails closed on malformed schema-v2 file rows`` (files: string) =
        match DriverManifest.tryParse (schemaV2 files (String.replicate 64 "0")) with
        | Error _ -> ()
        | Ok _ -> failwith "expected malformed schema-v2 file row to fail"

    [<Fact>]
    let ``tryParse rejects duplicate file paths before tree verification`` () =
        let files =
            """[{"path":"SKILL.md","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":false},{"path":"SKILL.md","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":false}]"""

        match DriverManifest.tryParse (schemaV2 files (String.replicate 64 "0")) with
        | Error message -> Assert.Contains("duplicate", message)
        | Ok _ -> failwith "expected duplicate path to fail"

    [<Fact>]
    let ``tryParse rejects a tree digest that does not bind the declared files`` () =
        let files =
            """[{"path":"SKILL.md","sha256":"7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b","executable":false}]"""

        match DriverManifest.tryParse (schemaV2 files (String.replicate 64 "0")) with
        | Error message -> Assert.Contains("tree-sha256", message)
        | Ok _ -> failwith "expected mismatched tree digest to fail"

    // ---------- DriverPredicate ----------

    [<Theory>]
    [<InlineData("always", true)>]
    [<InlineData("false", false)>]
    let ``evaluate resolves the delivered literal predicates`` (predicate: string) (expected: bool) =
        Assert.Equal(Some expected, DriverPredicate.evaluate predicate Set.empty)

    [<Fact>]
    let ``evaluate resolves a has atom against the present id set`` () =
        let present = Set.ofList [ "fs-gg-sdd-plan"; "fs-gg-feedback-report" ]
        Assert.Equal(Some true, DriverPredicate.evaluate "has fs-gg-sdd-plan" present)
        Assert.Equal(Some false, DriverPredicate.evaluate "has fs-gg-absent" present)

    [<Fact>]
    let ``evaluate resolves a trailing-glob has atom by prefix`` () =
        let present = Set.ofList [ "fs-gg-feedback-report" ]
        Assert.Equal(Some true, DriverPredicate.evaluate "has fs-gg-feedback-*" present)
        Assert.Equal(Some false, DriverPredicate.evaluate "has fs-gg-nope-*" present)

    [<Fact>]
    let ``evaluate resolves the composed AND driver shape`` () =
        let present = Set.ofList [ "fs-gg-sdd-plan"; "fs-gg-feedback-report" ]
        Assert.Equal(Some true, DriverPredicate.evaluate "has fs-gg-sdd-* and has fs-gg-feedback-*" present)

        Assert.Equal(Some false, DriverPredicate.evaluate "has fs-gg-sdd-* and has fs-gg-missing-*" present)

    [<Fact>]
    let ``evaluate resolves an OR of has atoms`` () =
        let present = Set.ofList [ "fs-gg-feedback-report" ]
        Assert.Equal(Some true, DriverPredicate.evaluate "has fs-gg-absent or has fs-gg-feedback-report" present)

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("sometimes")>]
    [<InlineData("has a and has b or has c")>]
    [<InlineData("count(x) > 2")>]
    let ``evaluate returns None for a predicate it cannot evaluate (fail closed)`` (predicate: string) =
        Assert.Equal(None, DriverPredicate.evaluate predicate (Set.ofList [ "a"; "b"; "c" ]))
