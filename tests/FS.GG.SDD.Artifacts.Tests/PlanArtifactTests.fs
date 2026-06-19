namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module PlanArtifactTests =
    let planText =
        """---
schemaVersion: 1
workId: 008-plan-command
title: Plan Command
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/008-plan-command/spec.md
sourceClarifications: work/008-plan-command/clarifications.md
sourceChecklist: work/008-plan-command/checklist.md
publicOrToolFacingImpact: true
---

# Plan Command Plan

Prose status: planned

## Source Snapshot
- spec: work/008-plan-command/spec.md sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa schemaVersion:1
- clarifications: work/008-plan-command/clarifications.md sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb schemaVersion:1
- checklist: work/008-plan-command/checklist.md sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc schemaVersion:1

## Plan Scope
- Work item 008-plan-command is planned.

## Plan Decisions
- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans.
- PD-002 [CR-002] acceptedDeferral: Accepted checklist deferral remains visible.
- PD-003 [PD-001] stale: Source facts changed after PD-001 was recorded.

## Contract Impact
- PC-001 [PD-001] command report: fsgg-sdd plan JSON is tool-facing.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run command tests and CLI smoke evidence.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: Plan schemaVersion 1 is accepted.

## Generated View Impact
- GV-001 [PD-001] workModel: readiness/008-plan-command/work-model.json refreshes from plan sources.

## Accepted Deferrals
- CR-002 acceptedDeferral: Deferral remains visible to tasks and evidence.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: tasks.
"""

    let snapshot text =
        ({ Path = "work/008-plan-command/plan.md"
           Text = text }
        : LifecycleArtifacts.FileSnapshot)

    [<Fact>]
    let ``Plan parser extracts front matter snapshots ids deferrals and stale counts`` () =
        match LifecycleArtifacts.parsePlanFacts (snapshot planText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("008-plan-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Plan, facts.FrontMatter.Stage)
            Assert.Empty(facts.MissingStandardSections)
            Assert.Equal<string list>([ "PD-001"; "PD-002"; "PD-003" ], facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value))
            Assert.Equal<string list>([ "PC-001" ], facts.ContractReferences |> List.map (fun reference -> reference.ContractId.Value))
            Assert.Equal<string list>([ "VO-001" ], facts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value))
            Assert.Equal<string list>([ "PM-001" ], facts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value))
            Assert.Equal<string list>([ "GV-001" ], facts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value))
            Assert.Equal(3, facts.SourceSnapshots.Length)
            Assert.Equal(1, facts.AcceptedDeferrals.Length)
            Assert.Equal(1, facts.StaleDecisionCount)

    [<Fact>]
    let ``Plan parser reports duplicate plan ids`` () =
        let broken = planText.Replace("- PD-001 [FR-001]", "- PD-001 [FR-001]\n- PD-001 [FR-001]")

        match LifecycleArtifacts.parsePlanFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Contains(facts.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateIdentifier")

    [<Fact>]
    let ``Plan parser diagnoses unsupported schema versions`` () =
        let broken = planText.Replace("schemaVersion: 1", "schemaVersion: 2")

        match LifecycleArtifacts.parsePlanFacts (snapshot broken) with
        | Ok _ -> failwith "Unsupported schema version should block parsing."
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unsupportedSchemaVersion")
