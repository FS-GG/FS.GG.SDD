namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

/// 108 / ADR-0054: the delivered driver manifest parser and the `materializes-when` predicate
/// evaluator — the shape the scaffold-time materializer reads, and the fail-closed rule that a
/// predicate this CLI cannot evaluate yields `None` (skip), never a default materialize.
module DriverManifestTests =

    // The delivered FS.GG.Drivers 0.2.0 manifest (verbatim shape), used as the parse fixture.
    // 0.2.0 (#632) adds the second `always` driver, workBoard, alongside workRoadmap.
    let private deliveredManifest =
        """{
  "schemaVersion": 1,
  "skills": [
    {
      "id": "workRoadmap",
      "scope": "driver",
      "sha256": "2b9313bf960ba6df3f5634ba19919f9013a9f6e58d83734f102bfa4705b06812",
      "supplied-by": ".claude/skills/workRoadmap",
      "materializes-when": "always"
    },
    {
      "id": "workBoard",
      "scope": "driver",
      "sha256": "02ccd2a602bc3a0bcca453901b77bcf7085c3d656807494bbcec2077ec3ec665",
      "supplied-by": ".claude/skills/workBoard",
      "materializes-when": "always"
    },
    {
      "id": "drive-board",
      "scope": "operator",
      "sha256": "28edb56f4cc7926ef423f9980ad6161e5d6c18cbe5f6008bc7b3c7981e2c7027",
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
            Assert.Equal(3, List.length manifest.Skills)

            let workRoadmap = manifest.Skills |> List.find (fun s -> s.Id = "workRoadmap")
            Assert.Equal("driver", workRoadmap.Scope)
            Assert.Equal("always", workRoadmap.MaterializesWhen)
            Assert.Equal("2b9313bf960ba6df3f5634ba19919f9013a9f6e58d83734f102bfa4705b06812", workRoadmap.Sha256)
            Assert.Equal(Some ".claude/skills/workRoadmap", workRoadmap.SuppliedBy)

            // #632: workBoard is the second always-on driver 0.2.0 delivers.
            let workBoard = manifest.Skills |> List.find (fun s -> s.Id = "workBoard")
            Assert.Equal("driver", workBoard.Scope)
            Assert.Equal("always", workBoard.MaterializesWhen)
            Assert.Equal("02ccd2a602bc3a0bcca453901b77bcf7085c3d656807494bbcec2077ec3ec665", workBoard.Sha256)
            Assert.Equal(Some ".claude/skills/workBoard", workBoard.SuppliedBy)

            let driveBoard = manifest.Skills |> List.find (fun s -> s.Id = "drive-board")
            Assert.Equal("false", driveBoard.MaterializesWhen)

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
    let ``tryParse drops a row lacking id/sha256/materializes-when rather than materializing it`` () =
        let text =
            """{ "schemaVersion": 1, "skills": [ { "scope": "driver", "sha256": "x" } ] }"""

        match DriverManifest.tryParse text with
        | Error message -> failwithf "expected Ok, got Error %s" message
        | Ok manifest -> Assert.Empty manifest.Skills

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
