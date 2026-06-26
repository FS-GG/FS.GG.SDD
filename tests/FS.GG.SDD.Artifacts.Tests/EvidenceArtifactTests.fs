namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open Xunit

module EvidenceArtifactTests =
    let evidencePath = "work/011-evidence-command/evidence.yml"

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
    let ``parseEvidenceArtifact reports duplicate evidence ids as artifact diagnostics`` () =
        let text =
            validEvidenceYaml
                .Replace(
                    "evidence:\n  - id: EV001",
                    "evidence:\n  - id: EV001\n    kind: verification\n    subject:\n      type: task\n      id: T002\n    result: pass\n  - id: EV001")

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact ->
            Assert.Contains(artifact.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateIdentifier")
        | Error diagnostics -> failwith $"Expected duplicate ids to be artifact diagnostics, got {diagnostics}."
