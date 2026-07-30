namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Artifacts
open Xunit

/// ADR-0063 / FS.GG.SDD#623: the scaffold-time owner-skill materializer. The delivered
/// FS.GG.Game.Skills bytes are embedded, content-addressed against their manifest sha256
/// (ADR-0014), and gated by a parameter `materializes-when` predicate (`profile in [..]`).
/// Enforcement lives in this consumer (ADR-0061): tamper, id collision, and unevaluable predicates
/// all fail closed; every product-scoped row is delivered and the retired legacy `mirrored`
/// classification is ignored. The `plan` tests exercise the real compiled-in bytes;
/// the `planFrom` tests inject synthetic manifests to cover the fail-closed classes.
module GameSkillsTests =

    // The pinned digest of the delivered fs-gg-playtest body (the drift-guard golden).
    let private playtestSha256 =
        "f070f0719dde93b55d1a41ea8aa881b90e41cd20a20e558771d87ef3fe28f642"

    let private roots = [ ".agents"; ".claude" ]

    let private skillPathFor id =
        roots |> List.map (fun root -> $"{root}/skills/{id}/SKILL.md") |> List.sort

    let private gameProfile = Map.ofList [ "profile", "game" ]

    // ---------- the embedded delivery (real bytes) ----------

    [<Fact>]
    let ``plan materializes the delivered fs-gg-playtest into both runtime roots on the game profile`` () =
        let outcome = GameSkills.plan gameProfile

        Assert.Contains("fs-gg-playtest", outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds
        Assert.Equal(None, outcome.ManifestError)

        let playtestPaths =
            outcome.ProvenancePaths
            |> List.map fst
            |> List.filter (fun p -> p.Contains "fs-gg-playtest")
            |> List.sort

        Assert.Equal<string list>(skillPathFor "fs-gg-playtest", playtestPaths)

    [<Fact>]
    let ``plan materializes every product skill on the game profile (FS.GG.Game.Skills 0.6.0)`` () =
        // FS.GG.Game.Skills 0.6.0 supplies every product-scoped owner skill. A game-profile
        // scaffold materializes all fourteen, content-addressed.
        let outcome = GameSkills.plan gameProfile

        for id in
            [ "fs-gg-ai"
              "fs-gg-audio"
              "fs-gg-ballistics"
              "fs-gg-collision"
              "fs-gg-effects"
              "fs-gg-game-core"
              "fs-gg-grids"
              "fs-gg-line-drawing"
              "fs-gg-mapcraft"
              "fs-gg-model-swap"
              "fs-gg-persistence"
              "fs-gg-physics"
              "fs-gg-playtest"
              "fs-gg-visibility" ] do
            Assert.Contains(id, outcome.MaterializedIds)

        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds

    [<Fact>]
    let ``plan on the app profile materializes only the app-gated fs-gg-audio`` () =
        // Owner-authored (0.2.0): fs-gg-audio declares `profile in [app, sample-pack, game]`, so an
        // app scaffold gets it; every other delivered row is gated to `profile in [game, sample-pack]`
        // and is filtered out. The predicate gate is what keeps the game-only rows off an app scaffold
        // while every other product row remains game/sample-pack gated.
        let outcome = GameSkills.plan (Map.ofList [ "profile", "app" ])
        Assert.Contains("fs-gg-audio", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-game-core", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-persistence", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-model-swap", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-playtest", outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds

    [<Fact>]
    let ``the materialized owner-skill writes are no-clobber AgentGuidanceTarget`` () =
        let outcome = GameSkills.plan gameProfile
        Assert.NotEmpty outcome.Writes

        for effect in outcome.Writes do
            match effect with
            | WriteFile(_, _, kind) -> Assert.Equal(AgentGuidanceTarget, kind)
            | other -> failwithf "expected a WriteFile, got %A" other

    // ---------- the content-addressed drift guard ----------

    [<Fact>]
    let ``every shipped delivered body matches its declared sha256 (ADR-0014)`` () =
        let manifestText =
            GameSkills.manifestText ()
            |> Option.defaultWith (fun () -> failwith "the owner-skill manifest must be embedded")

        let manifest =
            match GameSkillManifest.tryParse manifestText with
            | Ok manifest -> manifest
            | Error message -> failwithf "embedded manifest must parse: %s" message

        let bodies = GameSkills.embeddedBodies ()

        // Every delivered product row's shipped body must hash to its declared sha256.
        for entry in manifest.Skills do
            match Map.tryFind entry.Id bodies with
            | Some body -> Assert.Equal(entry.Sha256, Fsgg.SkillMirror.sha256 body)
            | None -> ()

    [<Fact>]
    let ``the delivered fs-gg-playtest digest is pinned to the golden`` () =
        let outcome = GameSkills.plan gameProfile

        let playtestSha =
            outcome.ProvenancePaths
            |> List.filter (fun (path, _) -> path.Contains "fs-gg-playtest")
            |> List.map snd
            |> List.distinct

        Assert.Equal<string list>([ playtestSha256 ], playtestSha)

    // ---------- the fail-closed classes (planFrom, synthetic) ----------

    let private manifestOf (rows: string) =
        Some(sprintf """{ "schemaVersion": 1, "skills": [ %s ] }""" rows)

    // A delivered product row. The legacy `mirrored` field is deliberately absent.
    let private row id sha predicate =
        sprintf """{ "id": "%s", "scope": "product", "sha256": "%s", "materializes-when": "%s" }""" id sha predicate

    [<Fact>]
    let ``planFrom fails closed on a tampered body digest`` () =
        let body = "owner skill body\n"
        // A manifest claiming a digest that the body does not hash to.
        let manifest = manifestOf (row "fs-gg-widget" "deadbeef" "always")
        let bodies = Map.ofList [ "fs-gg-widget", body ]
        let outcome = GameSkills.planFrom manifest bodies Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    // FS-GG/FS.GG.SDD#752 AC3 — ONE provenance class, ONE digest domain. `GameSkillPaths` and its
    // sibling `DriverPaths` are read together by `Drift` and by
    // `HandlersUpgrade.preservedFilesVerified`, so a digest function that differed between them
    // would make one field mean two things — which it did, and which is why #751 had to accept
    // either domain downstream. Pinned on a CRLF body, the only shape where the two disagree.
    [<Fact>]
    let ``game-skill provenance records the canonical digest, in the same domain as DriverPaths`` () =
        let body = "widget\r\nbody\r\n"

        let rawSha =
            System.Text.Encoding.UTF8.GetBytes body
            |> System.Security.Cryptography.SHA256.HashData
            |> System.Convert.ToHexString
            |> fun value -> value.ToLowerInvariant()

        let canonical = Fsgg.SkillMirror.sha256 body
        Assert.NotEqual<string>(rawSha, canonical)

        let outcome =
            GameSkills.planFrom
                (manifestOf (row "fs-gg-widget" canonical "always"))
                (Map.ofList [ "fs-gg-widget", body ])
                Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.MaterializedIds)

        let recorded = outcome.ProvenancePaths |> List.map snd |> List.distinct
        Assert.Equal<string list>([ canonical ], recorded)

    [<Fact>]
    let ``planFrom ignores the retired mirrored classification for a product row`` () =
        let body = "owner skill body\n"

        let mirroredTrue =
            sprintf
                """{ "id": "fs-gg-audio", "scope": "product", "sha256": "%s", "mirrored": true, "materializes-when": "always" }"""
                (Fsgg.SkillMirror.sha256 body)

        let outcome =
            GameSkills.planFrom (manifestOf mirroredTrue) (Map.ofList [ "fs-gg-audio", body ]) Map.empty

        Assert.Equal<string list>([ "fs-gg-audio" ], outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds

    [<Theory>]
    [<InlineData("fs-gg-sdd-plan")>]
    [<InlineData("fs-gg-sdd-not-a-real-skill")>]
    let ``planFrom rejects any row in the reserved fs-gg-sdd-* namespace`` (id: string) =
        let body = "x"
        let manifest = manifestOf (row id (Fsgg.SkillMirror.sha256 body) "always")
        let outcome = GameSkills.planFrom manifest (Map.ofList [ id, body ]) Map.empty

        Assert.Equal<string list>([ id ], outcome.NamespaceCollisionIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom skips a row whose predicate is unevaluable (fail closed, non-blocking)`` () =
        let body = "x"

        let manifest =
            manifestOf (row "fs-gg-widget" (Fsgg.SkillMirror.sha256 body) "sometimes")

        let outcome =
            GameSkills.planFrom manifest (Map.ofList [ "fs-gg-widget", body ]) gameProfile

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.PredicateUnevaluatedIds)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom materializes a profile-gated row only on a matching profile`` () =
        let body = "x"
        let sha = Fsgg.SkillMirror.sha256 body
        let manifest = manifestOf (row "fs-gg-widget" sha "profile in [game, sample-pack]")
        let bodies = Map.ofList [ "fs-gg-widget", body ]

        let hit = GameSkills.planFrom manifest bodies gameProfile
        Assert.Equal<string list>([ "fs-gg-widget" ], hit.MaterializedIds)

        let miss = GameSkills.planFrom manifest bodies (Map.ofList [ "profile", "app" ])
        Assert.Empty miss.MaterializedIds

    [<Fact>]
    let ``planFrom surfaces a malformed manifest as a ManifestError, materializing nothing`` () =
        let outcome = GameSkills.planFrom (Some "{ not json") Map.empty gameProfile
        Assert.True(Option.isSome outcome.ManifestError)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom with no embedded manifest is an inert no-op`` () =
        let outcome = GameSkills.planFrom None Map.empty gameProfile
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes
        Assert.Equal(None, outcome.ManifestError)

    // ---------- the scaffold seam: no-clobber honesty against provider output ----------

    [<Fact>]
    let ``plannedGameSkillOutcome materializes fs-gg-playtest on the game profile when the provider produced no such skill``
        ()
        =
        let outcome = HandlersScaffold.plannedGameSkillOutcome [] gameProfile
        Assert.Contains("fs-gg-playtest", outcome.MaterializedIds)

        Assert.Equal(
            2,
            outcome.ProvenancePaths
            |> List.filter (fun (p, _) -> p.Contains "fs-gg-playtest")
            |> List.length
        )

    [<Fact>]
    let ``plannedGameSkillOutcome does not over-claim an id the provider already produced`` () =
        let outcome =
            HandlersScaffold.plannedGameSkillOutcome [ ".agents/skills/fs-gg-playtest/SKILL.md" ] gameProfile

        Assert.DoesNotContain("fs-gg-playtest", outcome.MaterializedIds)

    [<Fact>]
    let ``plannedGameSkillOutcome on the app profile materializes only the app-gated fs-gg-audio`` () =
        // fs-gg-audio's owner-authored predicate includes `app` (0.2.0), so it materializes on an app
        // scaffold; the game-gated rows do not. See the `plan on the app profile` fact above.
        let outcome =
            HandlersScaffold.plannedGameSkillOutcome [] (Map.ofList [ "profile", "app" ])

        Assert.Contains("fs-gg-audio", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-game-core", outcome.MaterializedIds)
        Assert.NotEmpty outcome.Writes
