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
