namespace FS.GG.SDD.Commands.Tests

open System
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Artifacts
open Xunit

/// 108 / ADR-0054: the scaffold-time driver materializer. The delivered `FS.GG.Drivers`
/// bytes are embedded, content-addressed against their manifest sha256 (ADR-0014), and gated by
/// `materializes-when`. Enforcement lives in this consumer (ADR-0061): tamper, id collision, and
/// unevaluable predicates all fail closed. The `plan` tests exercise the real compiled-in bytes;
/// the `planFrom` tests inject synthetic manifests to cover the fail-closed classes.
module DriverSkillsTests =

    // The pinned digests of the delivered driver bodies (the drift-guard goldens).
    let private workRoadmapSha256 =
        "715609ab4d97337ee5250fb31e57159fb5d7b99a8c4ead0b712fd8c8c50b1677"

    // work-board ships in FS.GG.Drivers 0.8.0, `materializes-when: always` like work-roadmap.
    let private workBoardSha256 =
        "7b3668c5137e6dc9de9f008f45aa55623abb8b4bc8ea18715fcd9ce584ce694b"

    // padd-item is the product-workspace board filer added by FS.GG.Drivers 0.8.0 (#703).
    let private paddItemSha256 =
        "4daf167ef061d9a27504ad212e4c9c42321f597c64143953b0c666f072092d9e"

    let private roots = [ ".agents"; ".claude"; ".codex" ]

    let private deliveredFiles =
        Map.ofList
            [ "padd-item", [ "SKILL.md"; "agents/openai.yaml" ]
              "work-board",
              [ "SKILL.md"
                "agents/openai.yaml"
                "references/backlog-triage.md"
                "references/deep-detail.md"
                "references/host-loop.md"
                "references/workspace-scope.md" ]
              "work-roadmap",
              [ "SKILL.md"
                "agents/openai.yaml"
                "references/deep-detail.md"
                "references/host-loop.md"
                "references/roadmap-ledger.md" ] ]

    let private driverPathFor id =
        [ for root in roots do
              for relativePath in deliveredFiles[id] do
                  yield $"{root}/skills/{id}/{relativePath}" ]
        |> List.sort

    // The union of driver targets across ids, id-sorted then root-sorted — the deterministic
    // shape both `MaterializedIds` (id-sorted) and the mirrored provenance paths take.
    let private driverPathsFor ids =
        ids |> List.collect driverPathFor |> List.sort

    // ---------- the embedded delivery (real bytes) ----------

    // All three `always` driver rows FS.GG.Drivers 0.8.0 ships materialize; the operator-scoped
    // rows (`drive-board`, `p-add`, `cut-nuget-release`) do not — asserted separately below.
    [<Fact>]
    let ``plan materializes the delivered always-on drivers into all three roots`` () =
        let outcome = DriverSkills.plan Set.empty

        Assert.Equal<string list>([ "padd-item"; "work-board"; "work-roadmap" ], outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds
        Assert.Equal(None, outcome.ManifestError)

        let writtenPaths = outcome.ProvenancePaths |> List.map fst |> List.sort
        Assert.Equal<string list>(driverPathsFor [ "padd-item"; "work-board"; "work-roadmap" ], writtenPaths)

    [<Theory>]
    [<InlineData("drive-board")>]
    [<InlineData("p-add")>]
    [<InlineData("cut-nuget-release")>]
    let ``plan does not materialize operator rows (materializes-when false)`` (id: string) =
        let outcome = DriverSkills.plan Set.empty
        Assert.DoesNotContain(id, outcome.MaterializedIds)
        Assert.DoesNotContain(outcome.ProvenancePaths |> List.map fst, fun (p: string) -> p.Contains id)

    [<Fact>]
    let ``the materialized driver writes are no-clobber AgentGuidanceTarget`` () =
        let outcome = DriverSkills.plan Set.empty
        Assert.NotEmpty outcome.Writes

        for effect in outcome.Writes do
            match effect with
            | WriteFile(_, _, kind) -> Assert.Equal(AgentGuidanceTarget, kind)
            | SetExecutable _ -> ()
            | other -> failwithf "expected a WriteFile, got %A" other

    // ---------- the content-addressed drift guard (FR-008) ----------

    [<Fact>]
    let ``the embedded driver manifest parses and every shipped body matches its declared sha256`` () =
        let manifestText =
            DriverSkills.manifestText ()
            |> Option.defaultWith (fun () -> failwith "the driver manifest must be embedded")

        let manifest =
            match DriverManifest.tryParse manifestText with
            | Ok manifest -> manifest
            | Error message -> failwithf "embedded manifest must parse: %s" message

        let bodies = DriverSkills.embeddedBodies ()

        // Every shipped body must hash to the sha256 its manifest row declares (ADR-0014).
        for entry in manifest.Skills do
            match Map.tryFind entry.Id bodies with
            | Some body -> Assert.Equal(entry.Sha256, Fsgg.SkillMirror.sha256 body)
            | None -> () // a row whose bytes are not shipped (e.g. drive-board) is not verifiable here

    [<Fact>]
    let ``the delivered driver digests are pinned to the goldens`` () =
        let outcome = DriverSkills.plan Set.empty
        let shas = outcome.ProvenancePaths |> List.map snd |> Set.ofList
        Assert.Contains(paddItemSha256, shas)
        Assert.Contains(workRoadmapSha256, shas)
        Assert.Contains(workBoardSha256, shas)
        Assert.Equal(13, shas.Count)

    // ---------- the fail-closed classes (planFrom, synthetic) ----------

    let private manifestOf (rows: string) =
        Some(sprintf """{ "schemaVersion": 1, "skills": [ %s ] }""" rows)

    let private row id sha predicate =
        sprintf """{ "id": "%s", "scope": "driver", "sha256": "%s", "materializes-when": "%s" }""" id sha predicate

    let private rawSha (bytes: byte array) =
        SHA256.HashData bytes
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let private v2Fixture () =
        let skill = Encoding.UTF8.GetBytes "# driver\n"
        let script = Encoding.UTF8.GetBytes "#!/bin/sh\nexit 0\n"
        let skillSha = rawSha skill
        let scriptSha = rawSha script

        let filesJson =
            $"""[{{"path":"SKILL.md","sha256":"{skillSha}","executable":false}},{{"path":"scripts/run.sh","sha256":"{scriptSha}","executable":true}}]"""

        let treeSha = filesJson |> Encoding.UTF8.GetBytes |> rawSha

        let manifest =
            $"""{{"schemaVersion":2,"skills":[{{"id":"driver","scope":"driver","sha256":"{skillSha}","tree-sha256":"{treeSha}","files":{filesJson},"materializes-when":"always"}}]}}"""

        manifest, Map.ofList [ ("driver", "SKILL.md"), skill; ("driver", "scripts/run.sh"), script ]

    [<Fact>]
    let ``planFilesFrom writes every verified file and preserves declared executable mode`` () =
        let manifest, files = v2Fixture ()
        let outcome = DriverSkills.planFilesFrom (Some manifest) files Set.empty

        Assert.Equal<string list>([ "driver" ], outcome.MaterializedIds)
        Assert.Equal(6, outcome.ProvenancePaths.Length)

        Assert.Equal(
            3,
            outcome.Writes
            |> List.filter (function
                | SetExecutable path when path.EndsWith("/scripts/run.sh") -> true
                | _ -> false)
            |> List.length
        )

    [<Fact>]
    let ``planFilesFrom rejects missing extra tampered and unreadable directory members`` () =
        let manifest, files = v2Fixture ()

        let cases =
            [ files |> Map.remove ("driver", "scripts/run.sh")
              files |> Map.add ("driver", "extra.txt") (Encoding.UTF8.GetBytes "extra")
              files
              |> Map.add ("driver", "scripts/run.sh") (Encoding.UTF8.GetBytes "tampered")
              files |> Map.add ("driver", "scripts/run.sh") [| 0xffuy |] ]

        for invalid in cases do
            let outcome = DriverSkills.planFilesFrom (Some manifest) invalid Set.empty
            Assert.Equal<string list>([ "driver" ], outcome.VerifyFailedIds)
            Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom fails closed on a tampered body digest`` () =
        let body = "driver body\n"
        // A manifest claiming a digest that the body does not hash to.
        let manifest = manifestOf (row "someDriver" "deadbeef" "always")
        let bodies = Map.ofList [ "someDriver", body ]
        let outcome = DriverSkills.planFrom manifest bodies Set.empty

        Assert.Equal<string list>([ "someDriver" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    // The whole `fs-gg-sdd-*` namespace is reserved — a prefix guard, not just the 16 seeded ids —
    // so both a real seeded id and a non-seeded `fs-gg-sdd-*` id are rejected (FR-007).
    [<Theory>]
    [<InlineData("fs-gg-sdd-plan")>]
    [<InlineData("fs-gg-sdd-not-a-real-skill")>]
    let ``planFrom rejects any row in the reserved fs-gg-sdd-* namespace`` (id: string) =
        let body = "x"
        let manifest = manifestOf (row id (Fsgg.SkillMirror.sha256 body) "always")
        let outcome = DriverSkills.planFrom manifest (Map.ofList [ id, body ]) Set.empty

        Assert.Equal<string list>([ id ], outcome.NamespaceCollisionIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom skips a row whose predicate is unevaluable (fail closed, non-blocking)`` () =
        let body = "x"

        let manifest =
            manifestOf (row "someDriver" (Fsgg.SkillMirror.sha256 body) "sometimes")

        let outcome =
            DriverSkills.planFrom manifest (Map.ofList [ "someDriver", body ]) Set.empty

        Assert.Equal<string list>([ "someDriver" ], outcome.PredicateUnevaluatedIds)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom materializes a composed has-predicate only when both families are present`` () =
        let body = "x"
        let sha = Fsgg.SkillMirror.sha256 body

        let manifest =
            manifestOf (row "someDriver" sha "has fs-gg-sdd-* and has fs-gg-feedback-*")

        let bodies = Map.ofList [ "someDriver", body ]

        let present = Set.ofList [ "fs-gg-sdd-plan"; "fs-gg-feedback-report" ]
        let hit = DriverSkills.planFrom manifest bodies present
        Assert.Equal<string list>([ "someDriver" ], hit.MaterializedIds)

        let miss = DriverSkills.planFrom manifest bodies (Set.ofList [ "fs-gg-sdd-plan" ])
        Assert.Empty miss.MaterializedIds

    [<Fact>]
    let ``planFrom surfaces a malformed manifest as a ManifestError, materializing nothing`` () =
        let outcome = DriverSkills.planFrom (Some "{ not json") Map.empty Set.empty
        Assert.True(Option.isSome outcome.ManifestError)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom with no embedded manifest is an inert no-op`` () =
        let outcome = DriverSkills.planFrom None Map.empty Set.empty
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes
        Assert.Equal(None, outcome.ManifestError)

    // ---------- the scaffold seam: no-clobber honesty against provider output ----------

    [<Fact>]
    let ``plannedDriverOutcome materializes the always-on drivers when the provider produced none`` () =
        let outcome = HandlersScaffold.plannedDriverOutcome []
        Assert.Contains("padd-item", outcome.MaterializedIds)
        Assert.Contains("work-board", outcome.MaterializedIds)
        Assert.Contains("work-roadmap", outcome.MaterializedIds)
        // Thirteen declared files across the three `always` drivers × three roots.
        Assert.Equal(39, outcome.ProvenancePaths |> List.length)

    // FR-005/FR-009: a provider that shipped its own `work-roadmap` (its `.agents` skill, mirrored to
    // the other roots by the preceding tick) already occupies that driver's targets — the no-clobber
    // write preserves the provider's, so the driver must not claim those paths (no over-claim, no
    // double owner-claim). The other always-on drivers the provider did not ship still
    // materializes into all three roots — no-clobber is per-id, not all-or-nothing.
    [<Fact>]
    let ``plannedDriverOutcome does not over-claim a driver id the provider already produced`` () =
        let outcome =
            HandlersScaffold.plannedDriverOutcome [ ".agents/skills/work-roadmap/SKILL.md" ]

        Assert.DoesNotContain("work-roadmap", outcome.MaterializedIds)
        Assert.Equal<string list>([ "padd-item"; "work-board" ], outcome.MaterializedIds)

        let writtenPaths = outcome.ProvenancePaths |> List.map fst |> List.sort
        Assert.Equal<string list>(driverPathsFor [ "padd-item"; "work-board" ], writtenPaths)
        Assert.Equal(24, outcome.Writes |> List.length)
