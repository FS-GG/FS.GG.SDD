namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open Xunit

/// Feature 060 / #70 (§2.5): malformed cross-references are diagnosed rather than silently
/// dropped, and the schema-version acceptance policy is the single canonical classifier for
/// generated artifacts too.
module MalformedReferenceTests =

    // ----- US1: malformed authored ids yield diagnostics -----

    let private tasksYaml deps =
        $"""schemaVersion: 1
tasks:
  - id: T001
    title: "First"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
  - id: T002
    title: "Second"
    status: pending
    owner: "sdd"
    dependencies: {deps}
    requirements: []
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

    let private tasksSnapshot text : FileSnapshot =
        { Path = "work/060-x/tasks.yml"
          Text = text }

    [<Fact>]
    let ``a malformed task dependency yields a malformedReference diagnostic`` () =
        // `T01` is not the canonical `T00N` shape; previously it was silently dropped.
        match parseTaskFacts (tasksSnapshot (tasksYaml "[T01]")) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Contains(
                facts.Diagnostics,
                fun d -> d.Id = "malformedReference" && d.RelatedIds |> List.contains "T01"
            )

    [<Fact>]
    let ``well-formed task references produce no malformedReference diagnostic`` () =
        match parseTaskFacts (tasksSnapshot (tasksYaml "[T001]")) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts -> Assert.DoesNotContain(facts.Diagnostics, fun d -> d.Id = "malformedReference")

    let private evidenceYaml requirementRefs =
        $"""schemaVersion: 1
workId: 060-x
stage: evidence
status: evidenceReady
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: {requirementRefs}
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    checklistResultRefs: []
    planDecisionRefs: []
    obligationRefs: [EV001]
    artifacts: []
    result: pass
    synthetic: false
    syntheticDisclosure: null
    rationale: null
    owner: null
    scope: null
    laterLifecycleVisibility: null
    notes: []
"""

    let private evidenceSnapshot text : FileSnapshot =
        { Path = "work/060-x/evidence.yml"
          Text = text }

    [<Fact>]
    let ``a malformed evidence requirement ref yields a malformedReference diagnostic`` () =
        match parseEvidenceArtifact (evidenceSnapshot (evidenceYaml "[FR1]")) with
        | Error diagnostics -> failwith $"Evidence should parse: {diagnostics}"
        | Ok facts ->
            Assert.Contains(
                facts.Diagnostics,
                fun d -> d.Id = "malformedReference" && d.RelatedIds |> List.contains "FR1"
            )

    [<Fact>]
    let ``well-formed evidence references produce no malformedReference diagnostic`` () =
        match parseEvidenceArtifact (evidenceSnapshot (evidenceYaml "[FR-001]")) with
        | Error diagnostics -> failwith $"Evidence should parse: {diagnostics}"
        | Ok facts -> Assert.DoesNotContain(facts.Diagnostics, fun d -> d.Id = "malformedReference")

    // ----- US2: one schema-version policy governs generated artifacts too -----

    let private workModelJson schemaVersion =
        $"""{{
  "schemaVersion": {schemaVersion},
  "modelVersion": "1.0.0",
  "workId": "060-x",
  "project": {{ "id": "demo", "defaultWorkRoot": "work" }},
  "sources": [],
  "workItem": {{ "id": "060-x", "title": "X", "stage": "tasks", "changeTier": "tier1", "status": "draft" }},
  "requirements": [],
  "decisions": [],
  "tasks": [],
  "evidence": [],
  "generatedViews": [],
  "diagnostics": [],
  "governanceBoundaries": []
}}"""

    [<Fact>]
    let ``parseWorkModel rejects an unsupported schemaVersion via the canonical classifier`` () =
        match
            parseWorkModel
                { Path = "readiness/060-x/work-model.json"
                  Text = workModelJson 3 }
        with
        | Ok _ -> failwith "schemaVersion 3 should block (it blocks everywhere else)."
        | Error diagnostics -> Assert.NotEmpty diagnostics

    [<Fact>]
    let ``parseWorkModel still accepts schemaVersion 1`` () =
        match
            parseWorkModel
                { Path = "readiness/060-x/work-model.json"
                  Text = workModelJson 1 }
        with
        | Ok model -> Assert.Equal("060-x", model.WorkId)
        | Error diagnostics -> failwith $"schemaVersion 1 should parse: {diagnostics}"

    let private provenanceRecord =
        { SchemaVersion = 1
          Generator = SchemaVersion.currentGeneratorVersion ()
          RequiredMinimumCliVersion = None
          ProviderName = "fixture"
          ProviderContractVersion = "1.0.0"
          TemplateRef = "fsgg-fixture-app"
          Outcome = "providerSucceeded"
          ProducedPaths =
            [ { Path = "src/Product/Program.fs"
                Owner = GeneratedProduct
                Sha256 = None } ]
          MirroredPaths = []
          EffectiveParameters = [] }

    [<Fact>]
    let ``ScaffoldProvenance.tryParse rejects an unsupported schemaVersion via the canonical classifier`` () =
        Assert.Equal(
            None,
            tryParse (
                serialize
                    { provenanceRecord with
                        SchemaVersion = 2 }
            )
        )

    [<Fact>]
    let ``ScaffoldProvenance.tryParse still accepts schemaVersion 1`` () =
        match tryParse (serialize provenanceRecord) with
        | Some parsed -> Assert.Equal("fixture", parsed.ProviderName)
        | None -> failwith "schemaVersion 1 provenance should parse."
