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
        "7eb9f056104bdbcfdcbd6a73cc82199f481b46864f762a8f0f041d6757529c4f"

    // work-board ships in FS.GG.Drivers 0.8.0, `materializes-when: always` like work-roadmap.
    let private workBoardSha256 =
        "0e44ecccfb46537cdb40296c7351dd08a0e5494ea6144ff13ceefd27872a8855"

    // padd-item is the product-workspace board filer added by FS.GG.Drivers 0.8.0 (#703).
    let private paddItemSha256 =
        "028316b22d32384d3b7c3f0bccac4191e0b16dfc7595f769a1a93510218277af"

    let private roots = [ ".agents"; ".claude" ]

    let private deliveredFiles =
        Map.ofList
            [ "padd-item", [ "SKILL.md"; "agents/openai.yaml" ]
              "work-board",
              [ "SKILL.md"
                "agents/openai.yaml"
                "references/backlog-triage.md"
                "references/deep-detail.md"
                "references/feedback-contract.md"
                "references/host-loop.md"
                "references/workspace-scope.md"
                "scripts/validate-feedback-state.py" ]
              "work-roadmap",
              [ "SKILL.md"
                "agents/openai.yaml"
                "references/deep-detail.md"
                "references/feedback-contract.md"
                "references/host-loop.md"
                "references/roadmap-ledger.md"
                "scripts/validate-feedback-state.py" ] ]

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

    // All `always` driver rows FS.GG.Drivers 0.8.0 ships materialize; the operator-scoped
    // rows (`drive-board`, `p-add`, `cut-nuget-release`) do not — asserted separately below.
    [<Fact>]
    let ``plan materializes the delivered always-on drivers into both runtime roots`` () =
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
        // The two driver feedback validators are byte-identical, so 17 files yield 16 distinct digests.
        Assert.Equal(16, shas.Count)

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
        Assert.Equal(4, outcome.ProvenancePaths.Length)

        Assert.Equal(
            2,
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

    // ---------- FS-GG/FS.GG.SDD#752 — the two digest domains, and which one is recorded ----------
    //
    // `files[].sha256` is RAW at schema v2 (the org producer writes `hashlib.sha256(raw)` and
    // documents it as a byte-integrity record for a materialized tree) and CANONICAL at schema v1
    // (which has no per-file digest at all, so the projected `SKILL.md` row is synthesized from the
    // row's canonical-body `sha256`). Both are right for what they record; what was wrong was that
    // the transport digest was then piped into PROVENANCE, where a later reader can only ever
    // reproduce the canonical one — a read seam returns text, not bytes.
    //
    // The two shapes below are the only ones where the domains diverge at all. Every file shipped
    // in `FS.GG.Drivers` today is LF with no BOM, so no fixture built from the real package can
    // reach this; these build the shapes explicitly.

    let private bomPrefix = [| 0xEFuy; 0xBBuy; 0xBFuy |]

    /// A v2 row whose two files are exactly the shapes with two different digests: one CRLF, one
    /// BOM-prefixed. Returns the manifest, the byte map, and the DECODED bodies a read seam yields.
    let private v2NonLfFixture () =
        let skill = Encoding.UTF8.GetBytes "# driver\r\nsecond line\r\n"
        let aux = Array.append bomPrefix (Encoding.UTF8.GetBytes "aux body\n")
        let skillSha = rawSha skill
        let auxSha = rawSha aux

        let filesJson =
            $"""[{{"path":"SKILL.md","sha256":"{skillSha}","executable":false}},{{"path":"references/aux.md","sha256":"{auxSha}","executable":false}}]"""

        let treeSha = filesJson |> Encoding.UTF8.GetBytes |> rawSha

        // The row's skill-level `sha256` is the CANONICAL digest of `SKILL.md` — the producer's
        // other normalization, on the same row, which is exactly the split #752 opened with.
        let skillCanonical = Fsgg.SkillMirror.sha256 "# driver\r\nsecond line\r\n"

        let manifest =
            $"""{{"schemaVersion":2,"skills":[{{"id":"driver","scope":"driver","sha256":"{skillCanonical}","tree-sha256":"{treeSha}","files":{filesJson},"materializes-when":"always"}}]}}"""

        manifest, Map.ofList [ ("driver", "SKILL.md"), skill; ("driver", "references/aux.md"), aux ]

    // AC2: a v2 row is verified in the RAW domain — CRLF and BOM included, un-normalized — so the
    // closed transport still catches a CRLF-mangled or BOM-stripped delivery.
    [<Fact>]
    let ``a v2 row verifies CRLF and BOM files in the RAW domain`` () =
        let manifest, files = v2NonLfFixture ()
        let outcome = DriverSkills.planFilesFrom (Some manifest) files Set.empty

        Assert.Equal<string list>([ "driver" ], outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds

    // AC2, the other direction: a v2 row is NOT accepted in the canonical domain. The domain is a
    // property of the row, not "whichever one happens to match" — that latitude is precisely what
    // #751 had to allow downstream and what AC4 removes.
    [<Fact>]
    let ``a v2 row whose files digest is CANONICAL rather than raw fails closed`` () =
        let skill = Encoding.UTF8.GetBytes "# driver\r\n"
        let canonical = Fsgg.SkillMirror.sha256 "# driver\r\n"

        let filesJson =
            $"""[{{"path":"SKILL.md","sha256":"{canonical}","executable":false}}]"""

        let treeSha = filesJson |> Encoding.UTF8.GetBytes |> rawSha

        let manifest =
            $"""{{"schemaVersion":2,"skills":[{{"id":"driver","scope":"driver","sha256":"{canonical}","tree-sha256":"{treeSha}","files":{filesJson},"materializes-when":"always"}}]}}"""

        // The premise: the two domains really do disagree on this body, so the case is not vacuous.
        Assert.NotEqual<string>(rawSha skill, canonical)

        let outcome =
            DriverSkills.planFilesFrom (Some manifest) (Map.ofList [ ("driver", "SKILL.md"), skill ]) Set.empty

        Assert.Equal<string list>([ "driver" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.Writes

    // AC2 for the OTHER schema: a v1 row carries only a canonical-body digest, so that is the domain
    // it is verified in — deliberately different from v2, because there is no raw digest in a v1
    // document to use. Recorded here rather than left to be inferred from `TreeSha256.IsSome`.
    [<Fact>]
    let ``a v1 row verifies a CRLF body in the CANONICAL domain and not the raw one`` () =
        let body = "driver body\r\n"
        let canonical = Fsgg.SkillMirror.sha256 body
        let raw = rawSha (Encoding.UTF8.GetBytes body)
        Assert.NotEqual<string>(raw, canonical)

        let accepted =
            DriverSkills.planFrom
                (manifestOf (row "someDriver" canonical "always"))
                (Map.ofList [ "someDriver", body ])
                Set.empty

        Assert.Equal<string list>([ "someDriver" ], accepted.MaterializedIds)

        let refused =
            DriverSkills.planFrom
                (manifestOf (row "someDriver" raw "always"))
                (Map.ofList [ "someDriver", body ])
                Set.empty

        Assert.Equal<string list>([ "someDriver" ], refused.VerifyFailedIds)

    // THE FIX ITSELF (AC4's premise). Provenance is the WORKSPACE record — "does the file on disk
    // still match what scaffold wrote?" — and it is answered later against a body read back through
    // `SkillMirror.decodeBody`, which strips the BOM and returns text. So the recorded digest is
    // `SkillMirror.sha256` of the body written, NOT the transport digest the manifest carried. For
    // a CRLF or BOM-prefixed file those are different values, and recording the transport one made
    // a drift report no consumer could ever clear.
    [<Fact>]
    let ``provenance records the canonical digest of the written body, not the raw transport digest`` () =
        let manifest, files = v2NonLfFixture ()
        let outcome = DriverSkills.planFilesFrom (Some manifest) files Set.empty

        let recorded path =
            outcome.ProvenancePaths
            |> List.filter (fun (p, _) -> p.EndsWith(path, StringComparison.Ordinal))
            |> List.map snd
            |> List.distinct

        // One value per file across all three roots — the shape a real record carries.
        Assert.Equal<string list>([ Fsgg.SkillMirror.sha256 "# driver\r\nsecond line\r\n" ], recorded "/SKILL.md")
        Assert.Equal<string list>([ Fsgg.SkillMirror.sha256 "aux body\n" ], recorded "/references/aux.md")

        // And that is genuinely NOT what the manifest declared, for either file — otherwise this
        // case would pass just as well against the code it replaces.
        for _, sha in outcome.ProvenancePaths do
            Assert.NotEqual<string>(rawSha (Array.append bomPrefix (Encoding.UTF8.GetBytes "aux body\n")), sha)
            Assert.NotEqual<string>(rawSha (Encoding.UTF8.GetBytes "# driver\r\nsecond line\r\n"), sha)

    // AC6, settled affirmatively. The materialized body is what `SkillMirror.decodeBody` yields, so
    // a BOM in the delivered bytes does NOT survive into the workspace as a `U+FEFF` character.
    // That is what makes the recorded domain reproducible from the read seam for EVERY file: write
    // the BOM through and the body written and the body read back differ by one character, and no
    // digest over them could ever agree.
    [<Fact>]
    let ``a BOM-prefixed delivered file is materialized BOM-free`` () =
        let manifest, files = v2NonLfFixture ()
        let outcome = DriverSkills.planFilesFrom (Some manifest) files Set.empty

        let auxBodies =
            outcome.Writes
            |> List.choose (function
                | WriteFile(path, body, _) when path.EndsWith("/references/aux.md", StringComparison.Ordinal) ->
                    Some body
                | _ -> None)

        Assert.Equal(2, auxBodies.Length)

        for body in auxBodies do
            Assert.Equal<string>("aux body\n", body)
            // Named explicitly, because an invisible leading `U+FEFF` is exactly the kind of
            // difference an equality assertion's failure message is hard to read.
            Assert.NotEqual('\uFEFF', body[0])

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
        // Seventeen declared files across the three `always` drivers × two runtime roots.
        Assert.Equal(34, outcome.ProvenancePaths |> List.length)

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
        Assert.Equal(20, outcome.Writes |> List.length)
