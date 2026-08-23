namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Artifacts
open Xunit

/// ADR-0063 third instance / FS.GG.SDD#864: the FOURTH scaffold-time enrollment channel — the
/// FS.GG.Rendering-authored product skills delivered as bytes in the pinned `FS.GG.Rendering.Skills`
/// package. The `plan` / `plannedRenderingSkillOutcome` facts exercise the REAL compiled-in bytes,
/// because the criterion this row was filed under is that the channel is OBSERVED rather than
/// declared: `.github#2639` cannot flip `skills.delivery-channels.yml` from `provider-scoped` to
/// `delivered` — a value that repository DEFINES as *"the bytes reach a tree scaffolded through ANY
/// provider"* — on a test that only proves a synthetic manifest parses. The `planFrom` facts inject
/// synthetic manifests to cover the fail-closed classes, which real bytes cannot reach.
module RenderingSkillsTests =

    /// The pinned digest of the delivered `fs-gg-feedback-report` body — the drift-guard golden, and
    /// the exact value `.github`'s `registry/skills.yml` records for the row (`owner:
    /// fs-gg-rendering`, `materializes-when: "always"`). Read from `FS.GG.Rendering.Skills` 0.1.0's
    /// own `skill-manifest.json`.
    let private feedbackReportSha256 =
        "1b6888de4b8ce96f61e7a98c2bb9e0b249cdca2cd90da7bec6884bdccf055fe2"

    let private roots = Fsgg.Schemas.agentSkillRoots

    let private skillPathFor id =
        roots |> List.map (fun root -> $"{root}/skills/{id}/SKILL.md") |> List.sort

    let private gameProfile = Map.ofList [ "profile", "game" ]

    /// The four ids `FS.GG.Game.Skills` 0.7.0 and `FS.GG.Rendering.Skills` 0.1.0 BOTH ship, with
    /// different bodies and the identical `profile in [game, sample-pack]` predicate. `.github`'s
    /// `registry/skills.yml` carries exactly one row for each, `owner: fs-gg-rendering`.
    let private doublyShippedIds =
        [ "fs-gg-collision"; "fs-gg-grids"; "fs-gg-line-drawing"; "fs-gg-visibility" ]

    // ---------- the embedded delivery: the channel is OBSERVED (real bytes) ----------

    // THE ROW'S CENTRAL FACT. `fs-gg-feedback-report` is `materializes-when: "always"`, so it is
    // owed to EVERY scaffold — including one produced through a non-rendering provider, which is the
    // measured shape that made this a defect: `EHotwagner/S.I.R.` (`providerName: fable-game`)
    // received 42 driver paths, 16 process skills, 2 game-skill paths, and ZERO Rendering-owned
    // product skills. An empty parameter map is exactly that scaffold's predicate environment: no
    // `profile`, no rendering provider, nothing rendering-shaped anywhere in the request.
    [<Fact>]
    let ``plan delivers fs-gg-feedback-report to every runtime root with no rendering-shaped input at all`` () =
        let outcome = RenderingSkills.plan Map.empty

        Assert.Contains("fs-gg-feedback-report", outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.PredicateUnevaluatedIds
        Assert.Empty outcome.NamespaceCollisionIds
        Assert.Equal(None, outcome.ManifestError)

        let feedbackPaths =
            outcome.ProvenancePaths
            |> List.map fst
            |> List.filter (fun path -> path.Contains "fs-gg-feedback-report")
            |> List.sort

        for root in roots do
            Assert.Contains($"{root}/skills/fs-gg-feedback-report/SKILL.md", feedbackPaths)
            Assert.Contains($"{root}/skills/fs-gg-feedback-report/scripts/FeedbackReportTool.fs", feedbackPaths)
            Assert.Contains($"{root}/skills/fs-gg-feedback-report/scripts/feedback-tool.fsx", feedbackPaths)

        // Not merely planned — the write carries the delivered bytes, and they are the manifest's.
        let bodies =
            outcome.Writes
            |> List.choose (fun effect ->
                match effect with
                | WriteFile(path, body, _) when path.Contains "fs-gg-feedback-report" -> Some body
                | _ -> None)

        Assert.Equal(List.length roots * 3, List.length bodies)

        Assert.Contains(feedbackReportSha256, bodies |> List.map Fsgg.SkillMirror.sha256)

    [<Fact>]
    let ``the delivered fs-gg-feedback-report digest is pinned to the golden`` () =
        let outcome = RenderingSkills.plan Map.empty

        let recorded =
            outcome.ProvenancePaths
            |> List.filter (fun (path, _) -> path.EndsWith "fs-gg-feedback-report/SKILL.md")
            |> List.map snd
            |> List.distinct

        Assert.Equal<string list>([ feedbackReportSha256 ], recorded)

        Assert.Equal(
            Some "template/feedback-report/skill/",
            outcome.MaterializedSuppliers |> Map.tryFind "fs-gg-feedback-report"
        )

    [<Fact>]
    let ``every shipped delivered body matches its declared sha256 (ADR-0014)`` () =
        let manifestText =
            RenderingSkills.manifestText ()
            |> Option.defaultWith (fun () -> failwith "the FS.GG.Rendering.Skills manifest must be embedded")

        let manifest =
            match GameSkillManifest.tryParse manifestText with
            | Ok manifest -> manifest
            | Error message -> failwithf "embedded manifest must parse: %s" message

        Assert.NotEmpty manifest.Skills
        let bodies = RenderingSkills.embeddedBodies ()

        for entry in manifest.Skills do
            match Map.tryFind entry.Id bodies with
            | Some body -> Assert.Equal(entry.Sha256, Fsgg.SkillMirror.sha256 body)
            | None -> failwithf "the pinned package must ship a body for the declared row %s" entry.Id

    [<Fact>]
    let ``plan on the game profile materializes the profile-gated Rendering rows`` () =
        let outcome = RenderingSkills.plan gameProfile

        for id in
            [ "fs-gg-elmish"
              "fs-gg-feedback-report"
              "fs-gg-game-shell"
              "fs-gg-project"
              "fs-gg-scene"
              "fs-gg-skiaviewer"
              "fs-gg-symbol-design"
              "fs-gg-symbology"
              "fs-gg-testing" ] do
            Assert.Contains(id, outcome.MaterializedIds)

        Assert.Empty outcome.VerifyFailedIds
        Assert.Empty outcome.NamespaceCollisionIds

    [<Fact>]
    let ``an off-profile row is withheld while the always row is not`` () =
        let outcome = RenderingSkills.plan (Map.ofList [ "profile", "headless-scene" ])

        Assert.Contains("fs-gg-feedback-report", outcome.MaterializedIds)
        // `profile in [app, game]` — headless-scene is in neither.
        Assert.DoesNotContain("fs-gg-game-shell", outcome.MaterializedIds)
        Assert.DoesNotContain("fs-gg-ui-widgets", outcome.MaterializedIds)
        Assert.Empty outcome.VerifyFailedIds

    [<Fact>]
    let ``the materialized rendering-skill writes are no-clobber AgentGuidanceTarget`` () =
        let outcome = RenderingSkills.plan gameProfile
        Assert.NotEmpty outcome.Writes

        for effect in outcome.Writes do
            match effect with
            | WriteFile(_, _, kind) -> Assert.Equal(AgentGuidanceTarget, kind)
            | other -> failwithf "expected a WriteFile, got %A" other

    // ---------- schema-v2 sidecars: the closed, verified package set is written ----------

    // `FS.GG.Rendering.Skills` 0.1.0 ships six files that are not `SKILL.md`, and its manifest is
    // schemaVersion 1 — one `sha256` per skill, covering `SKILL.md` alone. ADR-0014 is fail-closed,
    // so an undeclared file has no digest to verify against and must not be written. This fact pins
    // BOTH halves: the files are visible to the channel (so it can say so) and absent from its
    // writes (so nothing unverified reaches a tree).
    [<Fact>]
    let ``a declared sidecar is materialized with its verified skill body`` () =
        let outcome = RenderingSkills.plan gameProfile

        let writtenPaths =
            outcome.Writes
            |> List.choose (fun effect ->
                match effect with
                | WriteFile(path, _, _) -> Some path
                | _ -> None)

        Assert.NotEmpty writtenPaths

        for root in roots do
            Assert.Contains($"{root}/skills/fs-gg-feedback-report/scripts/feedback-tool.fsx", writtenPaths)
            Assert.Contains($"{root}/skills/fs-gg-feedback-report/scripts/FeedbackReportTool.fs", writtenPaths)

    // The report is scoped to rows that actually materialized: a sidecar belonging to a skill this
    // scaffold legitimately did not deliver is not a gap anyone can act on.
    [<Fact>]
    let ``the sidecar report names only skills this plan actually materialized`` () =
        let outcome = RenderingSkills.plan Map.empty
        let materialized = outcome.MaterializedIds |> Set.ofList

        Assert.Empty outcome.UndeliverableSidecars

        for entry in outcome.UndeliverableSidecars do
            let id = entry.Split('/') |> Array.head
            Assert.Contains(id, materialized)

        // `fs-gg-symbol-design` is `profile in [game, sample-pack]`, so on an empty parameter set it
        // is not delivered — and neither are its sidecars reported.
        Assert.DoesNotContain("fs-gg-symbol-design", materialized)

        Assert.DoesNotContain("fs-gg-symbol-design/reference.fsx", outcome.UndeliverableSidecars)

    // ---------- the cross-channel collision: one path, one owner ----------

    [<Fact>]
    let ``the four doubly-shipped ids really are shipped by both pinned packages`` () =
        // The premise of the yield rule, asserted rather than assumed — so the day a producer stops
        // shipping a duplicate, the rule's justification goes red here instead of rotting silently.
        let rendering = RenderingSkills.plan gameProfile |> _.MaterializedIds |> Set.ofList
        let game = GameSkills.plan gameProfile |> _.MaterializedIds |> Set.ofList

        for id in doublyShippedIds do
            Assert.Contains(id, rendering)
            Assert.Contains(id, game)

    [<Fact>]
    let ``plannedRenderingSkillOutcome yields every path the game channel already owns`` () =
        let gameOutcome = HandlersScaffold.plannedGameSkillOutcome [] gameProfile
        let gameKept = gameOutcome.ProvenancePaths |> List.map fst

        let outcome = HandlersScaffold.plannedRenderingSkillOutcome [] gameProfile gameKept

        let renderingPaths = outcome.ProvenancePaths |> List.map fst |> Set.ofList
        let gamePaths = Set.ofList gameKept

        // ONE PATH, ONE OWNER. This is acceptance 3 of FS.GG.SDD#864 stated as a set law: nothing
        // the game channel recorded may also appear under `renderingSkillPaths`, because a path in
        // both is a provenance record that contradicts itself.
        Assert.Empty(Set.intersect renderingPaths gamePaths)

        Assert.Equal<string list>(doublyShippedIds |> List.sort, outcome.YieldedIds)

        for id in doublyShippedIds do
            Assert.DoesNotContain(id, outcome.MaterializedIds)

        // The yield is scoped: the rows only Rendering ships still arrive.
        Assert.Contains("fs-gg-feedback-report", outcome.MaterializedIds)
        Assert.Contains("fs-gg-scene", outcome.MaterializedIds)

    [<Fact>]
    let ``nothing is yielded when the channels do not overlap`` () =
        // On an empty parameter set the game channel materializes nothing (every game row is
        // profile-gated), so the rendering channel takes its `always` row with no yield at all.
        let gameOutcome = HandlersScaffold.plannedGameSkillOutcome [] Map.empty

        let outcome =
            HandlersScaffold.plannedRenderingSkillOutcome [] Map.empty (gameOutcome.ProvenancePaths |> List.map fst)

        Assert.Empty outcome.YieldedIds
        Assert.Contains("fs-gg-feedback-report", outcome.MaterializedIds)

    [<Fact>]
    let ``plannedRenderingSkillOutcome does not over-claim an id the provider already produced`` () =
        let outcome =
            HandlersScaffold.plannedRenderingSkillOutcome
                [ ".agents/skills/fs-gg-feedback-report/SKILL.md" ]
                Map.empty
                []

        // A path the provider itself supplied is preserved by the no-clobber write, not materialized
        // by us — and is therefore never recorded as ours. That is the ordinary sibling behaviour,
        // and it is NOT reported as a cross-channel yield.
        Assert.DoesNotContain(".agents/skills/fs-gg-feedback-report/SKILL.md", outcome.ProvenancePaths |> List.map fst)

        Assert.Empty outcome.YieldedIds

    // ---------- the REPORT, not just the data ----------
    //
    // These exist because the critic on round 1 of PR #882 found the gap and had to close it with a
    // temporary probe: every fact above measured what the channel *decided*, and none named a
    // diagnostic id, so the whole advisory surface could have been deleted with the suite still
    // green. Deciding to withhold a file and TELLING someone are two different behaviours, and on
    // this channel the telling is the point — a withheld sidecar that is never reported is exactly
    // the silent half-truth FS.GG.SDD#864 exists to end.
    //
    // `HandlersScaffold.renderingSkillDiagnostics` is the production projection itself, not a
    // re-implementation of it: it is the function the scaffold finalize and TICK-A paths both call.

    [<Fact>]
    let ``a clean schema-v2 plan reports no sidecar diagnostic`` () =
        let outcome = RenderingSkills.plan Map.empty

        let ids =
            HandlersScaffold.renderingSkillDiagnostics outcome |> List.map (fun d -> d.Id)

        Assert.Empty ids

    [<Fact>]
    let ``every fail-closed class reaches an operator under its own diagnostic id`` () =
        // One id per class, and DISTINCT ids rather than a reused sibling-seam id, so a failure tells
        // an operator which package to go and fix. Each outcome shape is fed to the production
        // projection; the assertion is the id it emits.
        let cases =
            [ { RenderingSkills.empty with
                  ManifestError = Some "boom" },
              "scaffold.renderingSkillManifestMalformed"
              { RenderingSkills.empty with
                  NamespaceCollisionIds = [ "fs-gg-sdd-plan" ] },
              "scaffold.renderingSkillNamespaceCollision"
              { RenderingSkills.empty with
                  VerifyFailedIds = [ "fs-gg-widget" ] },
              "scaffold.renderingSkillVerifyFailed"
              { RenderingSkills.empty with
                  PredicateUnevaluatedIds = [ "fs-gg-widget" ] },
              "scaffold.renderingSkillPredicateUnevaluated"
              { RenderingSkills.empty with
                  YieldedIds = [ "fs-gg-collision" ] },
              "scaffold.renderingSkillChannelYielded"
              { RenderingSkills.empty with
                  UndeliverableSidecars = [ "fs-gg-widget/scripts/x.fsx" ] },
              "scaffold.renderingSkillSidecarsUndeclared" ]

        for outcome, expectedId in cases do
            let ids =
                HandlersScaffold.renderingSkillDiagnostics outcome |> List.map (fun d -> d.Id)

            Assert.Equal<string list>([ expectedId ], ids)

        // Bound against a check that matches nothing: an EMPTY outcome must report NOTHING, so the
        // six assertions above cannot be satisfied by a projection that emits everything always.
        Assert.Empty(HandlersScaffold.renderingSkillDiagnostics RenderingSkills.empty)

        // And the six ids really are six, not one id counted six times.
        Assert.Equal(6, cases |> List.map snd |> List.distinct |> List.length)

    [<Fact>]
    let ``the blocking classes are tool-defect errors and the advisory ones are not`` () =
        // The severity split is a contract, not a detail: an error is `Blocked` at
        // `ReportAssembly.outcome` and exits non-zero, so classifying the two advisories as errors
        // would red every scaffold for a defect in another repository's manifest — a path the
        // operator cannot repair locally.
        let severityOf outcome =
            HandlersScaffold.renderingSkillDiagnostics outcome
            |> List.map (fun d -> d.Severity, d.IsToolDefect)

        Assert.Equal<(Diagnostics.DiagnosticSeverity * bool) list>(
            [ Diagnostics.DiagnosticError, true ],
            severityOf
                { RenderingSkills.empty with
                    VerifyFailedIds = [ "fs-gg-widget" ] }
        )

        Assert.Equal<(Diagnostics.DiagnosticSeverity * bool) list>(
            [ Diagnostics.DiagnosticWarning, false ],
            severityOf
                { RenderingSkills.empty with
                    UndeliverableSidecars = [ "fs-gg-widget/scripts/x.fsx" ] }
        )

        Assert.Equal<(Diagnostics.DiagnosticSeverity * bool) list>(
            [ Diagnostics.DiagnosticWarning, false ],
            severityOf
                { RenderingSkills.empty with
                    YieldedIds = [ "fs-gg-collision" ] }
        )

    // ---------- the fail-closed classes (planFrom, synthetic) ----------

    let private manifestOf (rows: string) =
        Some(sprintf """{ "schemaVersion": 1, "skills": [ %s ] }""" rows)

    let private row id sha predicate =
        sprintf
            """{ "id": "%s", "scope": "product", "sha256": "%s", "materializes-when": "%s", "supplied-by": "template/product-skills/%s/" }"""
            id
            sha
            predicate
            id

    [<Fact>]
    let ``planFrom fails closed when a delivered rendering row has no supplier attribution`` () =
        let body = "rendering skill body\n"
        let digest = Fsgg.SkillMirror.sha256 body

        let unattributed =
            manifestOf (
                sprintf
                    """{ "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always" }"""
                    digest
            )

        let outcome =
            RenderingSkills.planFrom unattributed (Map.ofList [ "fs-gg-widget", body ]) [] Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.MaterializedSuppliers
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom fails closed on a tampered body digest`` () =
        let body = "rendering skill body\n"
        let manifest = manifestOf (row "fs-gg-widget" "deadbeef" "always")

        let outcome =
            RenderingSkills.planFrom manifest (Map.ofList [ "fs-gg-widget", body ]) [] Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom fails closed when the declared body is absent entirely`` () =
        let manifest =
            manifestOf (row "fs-gg-widget" (Fsgg.SkillMirror.sha256 "x") "always")

        let outcome = RenderingSkills.planFrom manifest Map.empty [] Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.Writes

    // FS-GG/FS.GG.SDD#752 AC3 — ONE provenance class, ONE digest domain. `RenderingSkillPaths` is
    // read alongside `DriverPaths` and `GameSkillPaths` by `Drift.ownerSourcedSkillFiles`, so a
    // digest recorded in a different domain would make one field mean two things. Pinned on a CRLF
    // body, the only shape where raw and canonical disagree.
    [<Fact>]
    let ``rendering provenance records the canonical digest, in the same domain as its siblings`` () =
        let body = "widget\r\nbody\r\n"

        let rawSha =
            System.Text.Encoding.UTF8.GetBytes body
            |> System.Security.Cryptography.SHA256.HashData
            |> System.Convert.ToHexString
            |> fun value -> value.ToLowerInvariant()

        let canonical = Fsgg.SkillMirror.sha256 body
        Assert.NotEqual<string>(rawSha, canonical)

        let outcome =
            RenderingSkills.planFrom
                (manifestOf (row "fs-gg-widget" canonical "always"))
                (Map.ofList [ "fs-gg-widget", body ])
                []
                Map.empty

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.MaterializedIds)
        Assert.Equal<string list>([ canonical ], outcome.ProvenancePaths |> List.map snd |> List.distinct)

    [<Theory>]
    [<InlineData("fs-gg-sdd-plan")>]
    [<InlineData("fs-gg-sdd-not-a-real-skill")>]
    let ``planFrom rejects any row in the reserved fs-gg-sdd-* namespace`` (id: string) =
        let body = "x"
        let manifest = manifestOf (row id (Fsgg.SkillMirror.sha256 body) "always")

        let outcome =
            RenderingSkills.planFrom manifest (Map.ofList [ id, body ]) [] Map.empty

        Assert.Equal<string list>([ id ], outcome.NamespaceCollisionIds)
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``planFrom skips a row whose predicate is unevaluable (fail closed, non-blocking)`` () =
        let body = "x"

        let manifest =
            manifestOf (row "fs-gg-widget" (Fsgg.SkillMirror.sha256 body) "sometimes")

        let outcome =
            RenderingSkills.planFrom manifest (Map.ofList [ "fs-gg-widget", body ]) [] gameProfile

        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.PredicateUnevaluatedIds)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom ignores a non-product row`` () =
        let body = "x"

        let processRow =
            sprintf
                """{ "id": "fs-gg-widget", "scope": "process", "sha256": "%s", "materializes-when": "always" }"""
                (Fsgg.SkillMirror.sha256 body)

        let outcome =
            RenderingSkills.planFrom (manifestOf processRow) (Map.ofList [ "fs-gg-widget", body ]) [] Map.empty

        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.VerifyFailedIds

    [<Fact>]
    let ``planFrom surfaces a malformed manifest as a ManifestError, materializing nothing`` () =
        let outcome = RenderingSkills.planFrom (Some "{ not json") Map.empty [] gameProfile
        Assert.True(Option.isSome outcome.ManifestError)
        Assert.Empty outcome.MaterializedIds

    [<Fact>]
    let ``planFrom with no embedded manifest is an inert no-op`` () =
        // A CLI built without the pin must degrade to silence, never to a failure — the same
        // degradation the driver and game seams already promise.
        let outcome = RenderingSkills.planFrom None Map.empty [] gameProfile
        Assert.Empty outcome.MaterializedIds
        Assert.Empty outcome.Writes
        Assert.Empty outcome.UndeliverableSidecars
        Assert.Equal(None, outcome.ManifestError)

    [<Fact>]
    let ``schema-v2 materializes every declared sidecar into every agent root`` () =
        let body = "skill body\n"
        let sidecar = "tool body\n"

        let manifest =
            Some(
                sprintf
                    """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/", "files": [ { "path": "SKILL.md", "sha256": "%s" }, { "path": "scripts/tool.fsx", "sha256": "%s" } ] } ] }"""
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 sidecar)
            )

        let files =
            Map.ofList
                [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body
                  ("fs-gg-widget", "scripts/tool.fsx"), System.Text.Encoding.UTF8.GetBytes sidecar ]

        let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
        Assert.Empty outcome.VerifyFailedIds

        for root in roots do
            Assert.Contains($"{root}/skills/fs-gg-widget/SKILL.md", outcome.ProvenancePaths |> List.map fst)
            Assert.Contains($"{root}/skills/fs-gg-widget/scripts/tool.fsx", outcome.ProvenancePaths |> List.map fst)

    [<Fact>]
    let ``schema-v2 fails closed when its required files set is absent or empty`` () =
        let body = "skill body\n"
        let digest = Fsgg.SkillMirror.sha256 body

        for filesProperty in [ ""; "\"files\": []" ] do
            let separator = if filesProperty = "" then "" else ", "

            let manifest =
                Some(
                    sprintf
                        """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/"%s%s } ] }"""
                        digest
                        separator
                        filesProperty
                )

            let files =
                Map.ofList [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body ]

            let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
            Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
            Assert.Empty outcome.Writes

    [<Fact>]
    let ``schema-v1 retains its implicit canonical SKILL body compatibility path`` () =
        let body = "skill body\n"
        let digest = Fsgg.SkillMirror.sha256 body

        for filesProperty in [ ""; ", \"files\": []" ] do
            let manifest =
                Some(
                    sprintf
                        """{ "schemaVersion": 1, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/"%s } ] }"""
                        digest
                        filesProperty
                )

            let outcome =
                RenderingSkills.planFilesFrom
                    manifest
                    (Map.ofList [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body ])
                    Map.empty

            Assert.Empty outcome.VerifyFailedIds

            for root in roots do
                Assert.Contains($"{root}/skills/fs-gg-widget/SKILL.md", outcome.ProvenancePaths |> List.map fst)

    [<Fact>]
    let ``schema-v2 fails closed when a sidecar is missing or undeclared`` () =
        let body = "skill body\n"
        let sidecar = "tool body\n"

        let manifest =
            Some(
                sprintf
                    """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/", "files": [ { "path": "SKILL.md", "sha256": "%s" }, { "path": "scripts/tool.fsx", "sha256": "%s" } ] } ] }"""
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 sidecar)
            )

        let missing =
            Map.ofList [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body ]

        let extra =
            Map.ofList
                [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body
                  ("fs-gg-widget", "scripts/tool.fsx"), System.Text.Encoding.UTF8.GetBytes sidecar
                  ("fs-gg-widget", "extra.fsx"), System.Text.Encoding.UTF8.GetBytes "x" ]

        for files in [ missing; extra ] do
            let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
            Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
            Assert.Empty outcome.Writes

    [<Fact>]
    let ``schema-v2 fails closed on a duplicate declared path before it can schedule duplicate writes`` () =
        let body = "skill body\n"
        let sidecar = "tool body\n"

        let manifest =
            Some(
                sprintf
                    """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/", "files": [ { "path": "SKILL.md", "sha256": "%s" }, { "path": "scripts/tool.fsx", "sha256": "%s" }, { "path": "scripts/tool.fsx", "sha256": "%s" } ] } ] }"""
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 sidecar)
                    (Fsgg.SkillMirror.sha256 sidecar)
            )

        let files =
            Map.ofList
                [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body
                  ("fs-gg-widget", "scripts/tool.fsx"), System.Text.Encoding.UTF8.GetBytes sidecar ]

        let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
        Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
        Assert.Empty outcome.Writes

    [<Fact>]
    let ``schema-v2 fails closed on absolute traversal and backslash declared paths`` () =
        let body = "skill body\n"
        let sidecar = "tool body\n"
        let digest = Fsgg.SkillMirror.sha256 sidecar

        for path in
            [ "/escape.fsx"
              "../escape.fsx"
              "scripts\\escape.fsx"
              "scripts/./escape.fsx"
              "scripts//escape.fsx" ] do
            let jsonPath = System.Text.Json.JsonSerializer.Serialize path

            let manifest =
                Some(
                    sprintf
                        """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/", "files": [ { "path": "SKILL.md", "sha256": "%s" }, { "path": %s, "sha256": "%s" } ] } ] }"""
                        (Fsgg.SkillMirror.sha256 body)
                        (Fsgg.SkillMirror.sha256 body)
                        jsonPath
                        digest
                )

            let files =
                Map.ofList
                    [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body
                      ("fs-gg-widget", path), System.Text.Encoding.UTF8.GetBytes sidecar ]

            let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
            Assert.Equal<string list>([ "fs-gg-widget" ], outcome.VerifyFailedIds)
            Assert.Empty outcome.Writes

    [<Fact>]
    let ``schema-v2 permits one unique nested skill-relative path`` () =
        let body = "skill body\n"
        let sidecar = "tool body\n"
        let nested = "scripts/validation/tool.fsx"

        let manifest =
            Some(
                sprintf
                    """{ "schemaVersion": 2, "skills": [ { "id": "fs-gg-widget", "scope": "product", "sha256": "%s", "materializes-when": "always", "supplied-by": "template/product-skills/fs-gg-widget/", "files": [ { "path": "SKILL.md", "sha256": "%s" }, { "path": "%s", "sha256": "%s" } ] } ] }"""
                    (Fsgg.SkillMirror.sha256 body)
                    (Fsgg.SkillMirror.sha256 body)
                    nested
                    (Fsgg.SkillMirror.sha256 sidecar)
            )

        let files =
            Map.ofList
                [ ("fs-gg-widget", "SKILL.md"), System.Text.Encoding.UTF8.GetBytes body
                  ("fs-gg-widget", nested), System.Text.Encoding.UTF8.GetBytes sidecar ]

        let outcome = RenderingSkills.planFilesFrom manifest files Map.empty
        Assert.Empty outcome.VerifyFailedIds

        for root in roots do
            Assert.Contains($"{root}/skills/fs-gg-widget/{nested}", outcome.ProvenancePaths |> List.map fst)
