namespace FS.GG.SDD.Commands.Tests

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
        "2b9313bf960ba6df3f5634ba19919f9013a9f6e58d83734f102bfa4705b06812"

    // workBoard ships in FS.GG.Drivers 0.2.0 (#632), `materializes-when: always` like workRoadmap.
    let private workBoardSha256 =
        "02ccd2a602bc3a0bcca453901b77bcf7085c3d656807494bbcec2077ec3ec665"

    let private roots = [ ".agents"; ".claude"; ".codex" ]

    let private driverPathFor id =
        roots |> List.map (fun root -> $"{root}/skills/{id}/SKILL.md") |> List.sort

    // The union of driver targets across ids, id-sorted then root-sorted — the deterministic
    // shape both `MaterializedIds` (id-sorted) and the mirrored provenance paths take.
    let private driverPathsFor ids =
        ids |> List.collect driverPathFor |> List.sort

    // ---------- the embedded delivery (real bytes) ----------

    // Both `always` driver rows FS.GG.Drivers 0.2.0 ships (#632) materialize; the operator-scoped
    // `drive-board` row (materializes-when: false) does not — asserted separately below.
    [<Fact>]
    let ``plan materializes the delivered always-on drivers into all three roots`` () =
        let outcome = DriverSkills.plan Set.empty

        // id-sorted: "workBoard" < "workRoadmap".
        Assert.Equal<string list>([ "workBoard"; "workRoadmap" ], outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds
        Assert.Equal(None, outcome.ManifestError)

        let writtenPaths = outcome.ProvenancePaths |> List.map fst |> List.sort
        Assert.Equal<string list>(driverPathsFor [ "workBoard"; "workRoadmap" ], writtenPaths)

    [<Fact>]
    let ``plan does not materialize the drive-board operator row (materializes-when false)`` () =
        let outcome = DriverSkills.plan Set.empty
        Assert.DoesNotContain("drive-board", outcome.MaterializedIds)
        Assert.DoesNotContain(outcome.ProvenancePaths |> List.map fst, fun (p: string) -> p.Contains "drive-board")

    [<Fact>]
    let ``the materialized driver writes are no-clobber AgentGuidanceTarget`` () =
        let outcome = DriverSkills.plan Set.empty
        Assert.NotEmpty outcome.Writes

        for effect in outcome.Writes do
            match effect with
            | WriteFile(_, _, kind) -> Assert.Equal(AgentGuidanceTarget, kind)
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
        let shas = outcome.ProvenancePaths |> List.map snd |> List.distinct |> List.sort
        // sorted hex: "02cc…" (workBoard) < "2b93…" (workRoadmap).
        Assert.Equal<string list>([ workBoardSha256; workRoadmapSha256 ], shas)

    // ---------- the fail-closed classes (planFrom, synthetic) ----------

    let private manifestOf (rows: string) =
        Some(sprintf """{ "schemaVersion": 1, "skills": [ %s ] }""" rows)

    let private row id sha predicate =
        sprintf """{ "id": "%s", "scope": "driver", "sha256": "%s", "materializes-when": "%s" }""" id sha predicate

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
        Assert.Contains("workBoard", outcome.MaterializedIds)
        Assert.Contains("workRoadmap", outcome.MaterializedIds)
        // two `always` drivers × three roots.
        Assert.Equal(6, outcome.ProvenancePaths |> List.length)

    // FR-005/FR-009: a provider that shipped its own `workRoadmap` (its `.agents` skill, mirrored to
    // the other roots by the preceding tick) already occupies that driver's targets — the no-clobber
    // write preserves the provider's, so the driver must not claim those paths (no over-claim, no
    // double owner-claim). The OTHER always-on driver (workBoard) the provider did NOT ship still
    // materializes into all three roots — no-clobber is per-id, not all-or-nothing.
    [<Fact>]
    let ``plannedDriverOutcome does not over-claim a driver id the provider already produced`` () =
        let outcome =
            HandlersScaffold.plannedDriverOutcome [ ".agents/skills/workRoadmap/SKILL.md" ]

        Assert.DoesNotContain("workRoadmap", outcome.MaterializedIds)
        Assert.Equal<string list>([ "workBoard" ], outcome.MaterializedIds)

        let writtenPaths = outcome.ProvenancePaths |> List.map fst |> List.sort
        Assert.Equal<string list>(driverPathFor "workBoard", writtenPaths)
        Assert.Equal(3, outcome.Writes |> List.length)
