namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open Xunit

module EvidenceArtifactTests =
    let evidencePath = "work/011-evidence-command/evidence.yml"

    // DELIBERATELY VERBOSE (feature 091). This fixture keeps the explicit
    // `syntheticDisclosure/rationale/owner/scope/laterLifecycleVisibility: null` lines that the
    // writer no longer emits, because it exercises the *reader's* backward compatibility with
    // files written by an older CLI or authored by hand (FR-005). Do not "helpfully" slim it —
    // `slimEvidenceYaml` below is the writer-shaped counterpart, and the two are asserted equal.
    let validEvidenceYaml =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
sourceSpec: work/011-evidence-command/spec.md
sourceClarifications: work/011-evidence-command/clarifications.md
sourceChecklist: work/011-evidence-command/checklist.md
sourcePlan: work/011-evidence-command/plan.md
sourceTasks: work/011-evidence-command/tasks.yml
sourceAnalysis: readiness/011-evidence-command/analysis.json
sourceSnapshots:
  - label: tasks
    path: work/011-evidence-command/tasks.yml
    digest: 0123456789abcdef
    schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: [FR-001]
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    checklistResultRefs: []
    planDecisionRefs: [PD-001]
    obligationRefs: [EV001]
    artifacts: [specs/011-evidence-command/readiness/command-evidence-tests.txt]
    sourceRefs:
      - kind: test-output
        path: specs/011-evidence-command/readiness/command-evidence-tests.txt
        result: pass
    result: pass
    synthetic: false
    syntheticDisclosure: null
    rationale: null
    owner: null
    scope: null
    laterLifecycleVisibility: null
    notes: []
lifecycleNotes:
  - Next lifecycle action: verify.
"""

    /// The same document in the shape feature 091's writer emits: the five always-null optional
    /// keys are omitted rather than written as `null`. Byte-for-byte identical to
    /// `validEvidenceYaml` apart from those five lines.
    let slimEvidenceYaml =
        validEvidenceYaml
            .Replace("    syntheticDisclosure: null\n", "")
            .Replace("    rationale: null\n", "")
            .Replace("    owner: null\n", "")
            .Replace("    scope: null\n", "")
            .Replace("    laterLifecycleVisibility: null\n", "")

    let private declarationsOf label text =
        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> artifact.Evidence
        | Error diagnostics -> failwith $"Expected the {label} evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact reads schema version 1 shape`` () =
        let snapshot =
            { Path = evidencePath
              Text = validEvidenceYaml }

        match parseEvidenceArtifact snapshot with
        | Ok artifact ->
            Assert.Equal("011-evidence-command", artifact.WorkId.Value)
            Assert.Equal(LifecycleStage.Evidence, artifact.Stage)
            Assert.Equal("evidenceReady", artifact.Status)
            Assert.Single(artifact.SourceSnapshots) |> ignore
            let declaration = Assert.Single(artifact.Evidence)
            Assert.Equal("EV001", declaration.Id.Value)
            Assert.Equal(EvidenceKind.Verification, declaration.Kind)
            Assert.True(declaration.ObligationRefs = [ "EV001" ])
            Assert.False(declaration.Synthetic)
            Assert.Empty(artifact.Diagnostics)
        | Error diagnostics -> failwith $"Expected evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact reads a bare null optional scalar as None`` () =
        // A bare `null` is the *absence* of a value, so it must round-trip back to None —
        // otherwise a re-run rewrites `null` → the quoted string `"null"` (issue #161).
        match
            parseEvidenceArtifact
                { Path = evidencePath
                  Text = validEvidenceYaml }
        with
        | Ok artifact ->
            let declaration = Assert.Single(artifact.Evidence)
            Assert.Equal(None, declaration.Rationale)
            Assert.Equal(None, declaration.Owner)
            Assert.Equal(None, declaration.Scope)
            Assert.Equal(None, declaration.LaterLifecycleVisibility)
        | Error diagnostics -> failwith $"Expected evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact keeps a quoted "null" optional scalar as the literal string`` () =
        // A *quoted* "null" is a real string value, not absence — it must survive as Some "null".
        let text =
            validEvidenceYaml
                .Replace("rationale: null", "rationale: \"null\"")
                .Replace("owner: null", "owner: \"null\"")

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact ->
            let declaration = Assert.Single(artifact.Evidence)
            Assert.Equal(Some "null", declaration.Rationale)
            Assert.Equal(Some "null", declaration.Owner)
        | Error diagnostics -> failwith $"Expected evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact reads an omitted optional key identically to an explicit null`` () =
        // Feature 091 / FR-005 / SC-003. The writer stops emitting the five always-null optional
        // keys. That is only a *serialization* change — not a schema change — because the reader
        // already collapses "key absent" (tryChild -> None) and "key present, plain null"
        // (isPlainNullScalar -> None) to the same value. This test is the load-bearing proof, and
        // it passes against the UNMODIFIED reader: if it ever fails, omission is a breaking change
        // and schemaVersion must be bumped.
        let verbose = declarationsOf "verbose" validEvidenceYaml
        let slim = declarationsOf "slim" slimEvidenceYaml

        // Guard the fixture itself: the two texts must actually differ by exactly the five lines.
        Assert.Equal(5, validEvidenceYaml.Split('\n').Length - slimEvidenceYaml.Split('\n').Length)
        Assert.DoesNotContain("rationale:", slimEvidenceYaml)
        Assert.DoesNotContain("syntheticDisclosure:", slimEvidenceYaml)

        Assert.Equal<EvidenceDeclaration list>(verbose, slim)

    [<Fact>]
    let ``parseEvidenceArtifact reads every plain null token and an omitted key as None`` () =
        // FR-005 / FR-006 boundary, pinned against the unmodified reader: `null`, `Null`, `NULL`,
        // `~`, an empty value, and an absent key are all *absence*. Only a quoted "null" is a value.
        let rationaleOf text =
            (declarationsOf "variant" text |> List.exactlyOne).Rationale

        for token in [ "null"; "Null"; "NULL"; "~"; "" ] do
            let text = validEvidenceYaml.Replace("rationale: null", $"rationale: {token}")
            Assert.Equal(None, rationaleOf text)

        Assert.Equal(None, rationaleOf slimEvidenceYaml)
        Assert.Equal(Some "null", rationaleOf (validEvidenceYaml.Replace("rationale: null", "rationale: \"null\"")))

    let private singleDeclarationOf label text =
        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> Assert.Single(artifact.Evidence)
        | Error diagnostics -> failwith $"Expected the {label} evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact reads a nested bare-null syntheticDisclosure as None`` () =
        // FS.GG.SDD#180: a bare `standsInFor: null` / `reason: null` is absence, so the whole
        // disclosure reads back as None and the undisclosed-synthetic gate can fire. Before the fix
        // these parsed to Some "null", silently disclosing nothing while satisfying the gate.
        for token in [ "null"; "Null"; "NULL"; "~"; "" ] do
            let text =
                validEvidenceYaml.Replace(
                    "    syntheticDisclosure: null\n",
                    $"    syntheticDisclosure:\n      standsInFor: {token}\n      reason: {token}\n"
                )

            Assert.Equal(None, (singleDeclarationOf $"bare-{token}" text).SyntheticDisclosure)

    [<Fact>]
    let ``parseEvidenceArtifact keeps a quoted "null" syntheticDisclosure as a real disclosure`` () =
        // The other side of FS.GG.SDD#180: a *quoted* "null" is a genuine string, so the disclosure
        // is present and survives — only a bare null is absence.
        let text =
            validEvidenceYaml.Replace(
                "    syntheticDisclosure: null\n",
                "    syntheticDisclosure:\n      standsInFor: \"null\"\n      reason: \"null\"\n"
            )

        Assert.Equal(
            Some
                { StandsInFor = "null"
                  Reason = "null" },
            (singleDeclarationOf "quoted" text).SyntheticDisclosure
        )

    [<Fact>]
    let ``parseEvidenceArtifact reads bare-null sourceRef scalars as None`` () =
        // FS.GG.SDD#180/#181: every optional sourceRef scalar is null-aware, so a bare-null field is
        // absence and the round-trip renderer omits it rather than re-emitting the string "null".
        let text =
            validEvidenceYaml.Replace(
                "    sourceRefs:\n"
                + "      - kind: test-output\n"
                + "        path: specs/011-evidence-command/readiness/command-evidence-tests.txt\n"
                + "        result: pass\n",
                "    sourceRefs:\n"
                + "      - kind: test-output\n"
                + "        id: null\n"
                + "        path: null\n"
                + "        uri: null\n"
                + "        digest: null\n"
                + "        relatedSourceId: null\n"
                + "        result: null\n"
            )

        let reference =
            Assert.Single((singleDeclarationOf "bare-null-refs" text).SourceRefs)

        Assert.Equal("test-output", reference.Kind)
        Assert.Equal(None, reference.ReferenceId)
        Assert.Equal(None, reference.Path)
        Assert.Equal(None, reference.Uri)
        Assert.Equal(None, reference.Digest)
        Assert.Equal(None, reference.RelatedSourceId)
        Assert.Equal(None, reference.Result)

    let private singleSnapshotOf text =
        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> Assert.Single(artifact.SourceSnapshots)
        | Error diagnostics -> failwith $"Expected evidence artifact to parse, got {diagnostics}."

    [<Fact>]
    let ``parseEvidenceArtifact reads an absent snapshot digest as None, never Some ""`` () =
        // FS.GG.SDD#182. Snapshot `digest` is `string option` because absence is meaningful:
        // "not snapshotted" is not "the empty digest". Read null-unaware, `digest: ` yields
        // Some "", and `evidenceSourceSnapshotStale` compares Some "" against the real digest
        // as a mismatch — a permanent, unfixable `evidence.staleEvidenceSource` on every run.
        // Plain null tokens and a plain empty value: absence, via `tryScalarNonNullAt`.
        for token in [ "null"; "Null"; "NULL"; "~"; "" ] do
            let text = validEvidenceYaml.Replace("digest: 0123456789abcdef", $"digest: {token}")
            Assert.Equal(None, (singleSnapshotOf text).Digest)

        // `isPlainNullScalar` deliberately only nulls *Plain* scalars, so a QUOTED empty digest
        // still reaches the reader as `Some ""`. An empty string is never a real digest — unlike
        // an empty `rationale` — so the blank filter collapses these to absence too. Without it
        // `digest: ''` is a permanent `evidence.staleEvidenceSource` that no re-run can clear,
        // and re-rendering it emits the trailing-whitespace `digest: ` line FR-004 forbids.
        for token in [ "''"; "\"\""; "\"   \"" ] do
            let text = validEvidenceYaml.Replace("digest: 0123456789abcdef", $"digest: {token}")
            Assert.Equal(None, (singleSnapshotOf text).Digest)

        // Symmetry with the declaration optionals: a *quoted* "null" is still a real value.
        let quoted =
            validEvidenceYaml.Replace("digest: 0123456789abcdef", "digest: \"null\"")

        Assert.Equal(Some "null", (singleSnapshotOf quoted).Digest)

    [<Fact>]
    let ``parseEvidenceArtifact reads an absent snapshot schemaVersion as None, never an invented 1`` () =
        // FS.GG.SDD#182, the reader half of the "absence is not a value" pair. `SchemaVersion`
        // is `int option` precisely so a source that declared none stays undeclared.
        for token in [ "null"; "Null"; "NULL"; "~"; "" ] do
            let text =
                validEvidenceYaml.Replace("schemaVersion: 1\nevidence:", $"schemaVersion: {token}\nevidence:")

            Assert.Equal(None, (singleSnapshotOf text).SchemaVersion)

        Assert.Equal(Some 1, (singleSnapshotOf validEvidenceYaml).SchemaVersion)

    [<Fact>]
    let ``parseEvidenceArtifact reports duplicate evidence ids as artifact diagnostics`` () =
        let text =
            validEvidenceYaml.Replace(
                "evidence:\n  - id: EV001",
                "evidence:\n  - id: EV001\n    kind: verification\n    subject:\n      type: task\n      id: T002\n    result: pass\n  - id: EV001"
            )

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> Assert.Contains(artifact.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateIdentifier")
        | Error diagnostics -> failwith $"Expected duplicate ids to be artifact diagnostics, got {diagnostics}."

    // ---- FS.GG.SDD#306: the visual-inspection obligation --------------------------------------

    [<Fact>]
    let ``visualInspectionSkill is the tag the task generator stamps`` () =
        Assert.Equal("visual-inspection", visualInspectionSkill)

    [<Theory>]
    [<InlineData("visual-inspection", true)>]
    [<InlineData("Visual-Inspection", true)>]
    [<InlineData("visual inspection", false)>]
    [<InlineData("implementation", false)>]
    let ``isVisualInspectionTagged matches the tag case-insensitively`` (tag: string) (expected: bool) =
        Assert.Equal(expected, isVisualInspectionTagged [ "fsharp"; tag ])

    [<Fact>]
    let ``isVisualInspectionTagged is false for an untagged task`` () =
        Assert.False(isVisualInspectionTagged [])
        Assert.False(isVisualInspectionTagged [ "fsharp"; "implementation" ])

    // FR-004: a rendered artifact is an `artifacts:` entry, or a `sourceRefs[]` `path`/`uri`.
    // The scaffolded declaration has neither, which is exactly the state the gate must reject
    // once an author flips it to a non-synthetic pass.
    [<Fact>]
    let ``namesRenderedArtifact accepts an artifacts entry`` () =
        let declaration = singleDeclarationOf "valid" validEvidenceYaml
        Assert.True(namesRenderedArtifact declaration)

    [<Fact>]
    let ``namesRenderedArtifact accepts a sourceRefs path with no artifacts entry`` () =
        let text =
            validEvidenceYaml.Replace(
                "    artifacts: [specs/011-evidence-command/readiness/command-evidence-tests.txt]\n",
                "    artifacts: []\n"
            )

        Assert.True(namesRenderedArtifact (singleDeclarationOf "sourceRef-only" text))

    [<Fact>]
    let ``namesRenderedArtifact rejects a declaration naming nothing`` () =
        let text =
            validEvidenceYaml
                .Replace(
                    "    artifacts: [specs/011-evidence-command/readiness/command-evidence-tests.txt]\n",
                    "    artifacts: []\n"
                )
                .Replace(
                    "    sourceRefs:\n      - kind: test-output\n        path: specs/011-evidence-command/readiness/command-evidence-tests.txt\n        result: pass\n",
                    "    sourceRefs: []\n"
                )

        Assert.False(namesRenderedArtifact (singleDeclarationOf "bare" text))

    // ---------------------------------------------------------------------------------------------
    // FS.GG.SDD#349 — the cited-artifact rule, and the census guard that keeps the shipped example
    // from rotting back into fiction.
    // ---------------------------------------------------------------------------------------------

    /// FR-002: both path-bearing buckets are cited paths — `artifacts:` AND `sourceRefs[].path` —
    /// because `namesRenderedArtifact` discharges an obligation from either one.
    [<Fact>]
    let ``citedArtifactPaths reads both the artifacts and sourceRefs buckets`` () =
        let cited = citedArtifactPaths (singleDeclarationOf "both" validEvidenceYaml)

        Assert.Contains("specs/011-evidence-command/readiness/command-evidence-tests.txt", cited)

    /// FR-002: a `uri` is not a local file. It must never become a probed path, or the gate would
    /// refuse every declaration pointing at a CI run.
    [<Fact>]
    let ``citedArtifactPaths never yields a sourceRefs uri`` () =
        let text =
            validEvidenceYaml
                .Replace(
                    "    artifacts: [specs/011-evidence-command/readiness/command-evidence-tests.txt]\n",
                    "    artifacts: []\n"
                )
                .Replace(
                    "    sourceRefs:\n      - kind: test-output\n        path: specs/011-evidence-command/readiness/command-evidence-tests.txt\n        result: pass\n",
                    "    sourceRefs:\n      - kind: test-output\n        uri: https://ci.example/run/1\n        result: pass\n"
                )

        Assert.Empty(citedArtifactPaths (singleDeclarationOf "uri-only" text))

    /// FR-001: a satisfying declaration whose cited path is absent yields that path.
    [<Fact>]
    let ``missingCitedArtifacts reports a cited path that does not exist`` () =
        let declaration = singleDeclarationOf "missing" validEvidenceYaml
        let missing = missingCitedArtifacts (fun _ -> false) declaration

        Assert.Contains("specs/011-evidence-command/readiness/command-evidence-tests.txt", missing)

    /// FR-006: only `pass` ∧ ¬`synthetic` is held to the rule. A deferral legitimately cites an
    /// artifact that does not exist yet, and must not be reported missing.
    [<Fact>]
    let ``missingCitedArtifacts holds only satisfying declarations to the rule`` () =
        let deferred =
            validEvidenceYaml.Replace("    result: pass\n", "    result: deferred\n")

        Assert.Empty(missingCitedArtifacts (fun _ -> false) (singleDeclarationOf "deferred" deferred))

    /// SC-002 / FR-008: the census. Every path the SHIPPED example cites must exist in the corpus.
    ///
    /// At `d21774d` this repository cited 29 artifact paths and 29 of them did not exist — including
    /// all six in the example below, the corpus this product publishes to teach evidence authoring.
    /// This test is the guard: the example cannot go back to citing files it does not ship.
    [<Fact>]
    let ``the shipped example cites only artifacts that exist`` () =
        let corpus =
            System.IO.Path.Combine(TestSupport.repoRoot, "docs", "examples", "lifecycle-artifacts")

        let text =
            System.IO.File.ReadAllText(System.IO.Path.Combine(corpus, "evidence.yml"))

        let declarations =
            match
                parseEvidence
                    { Path = "docs/examples/lifecycle-artifacts/evidence.yml"
                      Text = text }
            with
            | Ok declarations -> declarations
            | Error diagnostics -> failwith $"the shipped example does not parse: %A{diagnostics}"

        let missing =
            declarations
            |> List.collect (
                missingCitedArtifacts (fun path -> System.IO.File.Exists(System.IO.Path.Combine(corpus, path)))
            )
            |> List.distinct
            |> List.sort

        Assert.Empty(missing)

    // FS.GG.SDD#359 / #365 — the containment rule for CITED paths, at the parse layer.
    //
    // Before: `artifacts:` went through `Internal.artifact`, which RAISED on a `..`. The
    // ArgumentException escaped the pure parse (and, in the CLI, the pure `update`), so the author's
    // own bad path was reported to them as a tool defect. These tests are the failure leg: the parse
    // must be TOTAL and must NAME the offending path.
    let private diagnosticsOf text =
        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> artifact.Diagnostics
        | Error diagnostics -> diagnostics

    let private citedPathYaml field value =
        match field with
        | "artifacts" ->
            $"schemaVersion: 1\nevidence:\n  - id: EV001\n    kind: verification\n    subject:\n      type: task\n      id: T001\n    result: pass\n    synthetic: false\n    artifacts: [{value}]\n"
        | _ ->
            $"schemaVersion: 1\nevidence:\n  - id: EV001\n    kind: verification\n    subject:\n      type: task\n      id: T001\n    result: pass\n    synthetic: false\n    sourceRefs:\n      - kind: verification\n        path: {value}\n"

    [<Theory>]
    [<InlineData("artifacts")>]
    [<InlineData("sourceRefs")>]
    let ``a '..' in a cited path is a parse diagnostic, not an exception`` (field: string) =
        let escaping = "../../../etc/passwd"

        // The assertion is as much that this does not THROW as that it diagnoses.
        let diagnostics = diagnosticsOf (citedPathYaml field escaping)

        let malformed = diagnostics |> List.filter (fun d -> d.Id = "malformedArtifactPath")

        Assert.NotEmpty malformed

        // Malformed USER INPUT, never a tool defect (Constitution VIII) — and it names the path,
        // because "which path is wrong" is the one fact the author needs.
        Assert.All(
            malformed,
            fun d ->
                Assert.False d.IsToolDefect
                Assert.Equal(Diagnostics.DiagnosticError, d.Severity)
                Assert.Contains(escaping, d.RelatedIds)
        )

    [<Fact>]
    let ``an escaping sourceRefs path is never offered to the existence probe`` () =
        // FS.GG.SDD#365: `citedArtifactPaths` feeds the #349 existence gate. An escaping path must be
        // excluded from it, so no probe is ever PLANNED for a path outside the workspace — otherwise
        // an out-of-repo file that happens to exist (/etc/passwd) discharges the gate.
        let declarations =
            match
                parseEvidenceArtifact
                    { Path = evidencePath
                      Text = citedPathYaml "sourceRefs" "../../../../../../../../etc/passwd" }
            with
            | Ok artifact -> artifact.Evidence
            | Error diagnostics -> failwith $"expected a parse with diagnostics, got %A{diagnostics}"

        let cited = declarations |> List.collect citedArtifactPaths

        Assert.DoesNotContain("../../../../../../../../etc/passwd", cited)
        Assert.All(cited, fun path -> Assert.True(citedPathIsContained path))

    [<Fact>]
    let ``a contained cited path still parses and is still probed`` () =
        // The green path is unaffected: a legal repository-relative path survives both buckets.
        let text = citedPathYaml "artifacts" "evidence/frame.png"
        let diagnostics = diagnosticsOf text

        Assert.DoesNotContain(diagnostics, fun d -> d.Id = "malformedArtifactPath")

        let cited =
            match parseEvidenceArtifact { Path = evidencePath; Text = text } with
            | Ok artifact -> artifact.Evidence |> List.collect citedArtifactPaths
            | Error diagnostics -> failwith $"expected the contained path to parse: %A{diagnostics}"

        Assert.Contains("evidence/frame.png", cited)
