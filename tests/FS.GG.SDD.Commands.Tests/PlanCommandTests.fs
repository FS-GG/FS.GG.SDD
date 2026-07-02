namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module PlanCommandTests =
    let workId = "008-plan-command"
    let title = "Plan Command"
    let specPath = $"work/{workId}/spec.md"
    let clarificationPath = $"work/{workId}/clarifications.md"
    let checklistPath = $"work/{workId}/checklist.md"
    let planPath = $"work/{workId}/plan.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedChecklistReadyProject () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore
        TestSupport.runRequest { TestSupport.clarifyRequest root workId title with InputText = None } |> ignore
        TestSupport.runChecklist root workId title |> ignore
        root

    let missingCoverageSpec =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# {title} Specification

Prose status: specified

## User Value
Plan checklist-ready work.

## Scope
- SB-001: Add plan command behavior.

## Non-Goals
- SB-002: Do not add task generation.

## User Stories
- US-001 (P1): As a maintainer, I can plan checklist-ready work.

## Acceptance Scenarios
No acceptance scenarios recorded.

## Functional Requirements
- FR-001: The command creates plan output.

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Plan report JSON is tool-facing.

## Lifecycle Notes
- Next lifecycle action: clarify.
"""

    [<Fact>]
    let ``plan creates authored plan with real filesystem evidence`` () =
        let root = initializedChecklistReadyProject ()

        let report = TestSupport.runPlan root workId title
        let plan = TestSupport.readRelative root planPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: plan", plan)
        Assert.Contains("## Plan Decisions", plan)
        Assert.Contains("PD-001", plan)
        Assert.Contains("PC-001", plan)
        Assert.Contains("VO-001", plan)
        Assert.Contains("PM-001", plan)
        Assert.Contains("GV-001", plan)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = planPath && change.Operation = ArtifactOperation.Create)
        Assert.Equal(Some Tasks, report.NextAction |> Option.bind _.Command)
        TestSupport.assertPlanSummary report 1 1 1

    [<Fact>]
    let ``plan creation does not require Governance files`` () =
        let root = initializedChecklistReadyProject ()

        let report = TestSupport.runPlan root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated")

    [<Fact>]
    let ``plan missing specification blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan missing clarification blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan missing checklist blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore
        TestSupport.runRequest { TestSupport.clarifyRequest root workId title with InputText = None } |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingChecklistPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan failed checklist blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeRelative root specPath missingCoverageSpec
        TestSupport.writeValidClarification root workId title
        TestSupport.runChecklist root workId title |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "failedChecklistPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan rerun preserves authored content and stable ids`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let authored = TestSupport.readRelative root planPath + "\nUser-authored plan prose stays here.\n"
        TestSupport.writeRelative root planPath authored

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root planPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = planPath && change.Operation = ArtifactOperation.NoChange)

    [<Fact>]
    let ``plan appends safe missing requirement and marks source decisions stale`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let updatedSpec = (TestSupport.readRelative root specPath).Replace("## Ambiguities", "- FR-002: New requirement links to AC-001. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities")
        TestSupport.writeRelative root specPath updatedSpec

        let report = TestSupport.runPlan root workId title
        let plan = TestSupport.readRelative root planPath

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains("FR-002", plan)
        Assert.Contains("stale:", plan)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanDecision")
        Assert.Equal(Some "plan.correctStaleDecisions", report.NextAction |> Option.map _.ActionId)
        Assert.True(report.Plan.Value.StaleDecisionCount > 0)

    [<Fact>]
    let ``plan identity mismatch blocks without mutating existing plan`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let original = (TestSupport.readRelative root planPath).Replace($"workId: {workId}", "workId: 999-other-work")
        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "planIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan duplicate id blocks without mutating existing plan`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let original = (TestSupport.readRelative root planPath).Replace("- PD-001", "- PD-001\n- PD-001")
        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicatePlanId")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan unknown source reference blocks without mutation`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let original = (TestSupport.readRelative root planPath).Replace("[FR-001]", "[FR-999]")
        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownPlanSourceReference")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan unsafe overwrite marker blocks without mutation`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let original = TestSupport.readRelative root planPath + "\n<!-- fsgg-sdd: unsafe-overwrite -->\n"
        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeOverwrite")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan dry run reports proposed changes without mutation`` () =
        let root = initializedChecklistReadyProject ()
        let request =
            { TestSupport.planRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root planPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = planPath && change.SafeWriteDecision = "dryRunOnly")

    [<Fact>]
    let ``plan refreshes generated work model when source data is valid`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.Sources |> List.exists (fun source -> source.Path = planPath))

    [<Fact>]
    let ``plan deterministic JSON is byte stable`` () =
        let root = initializedChecklistReadyProject ()
        let request =
            { TestSupport.planRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"plan\"", first)
        Assert.Contains("\"plan\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``plan text projection uses report facts`` () =
        let root = initializedChecklistReadyProject ()
        let report = TestSupport.runPlan root workId title
        let text = renderText report

        Assert.Contains("command: plan", text)
        Assert.Contains("planDecisions: 1", text)
        Assert.Contains("planContractReferences: 1", text)
        Assert.Contains("planVerificationObligations: 1", text)
        Assert.Contains("nextAction: nextLifecycleCommand", text)

    [<Fact>]
    let ``plan governance boundary remains advisory only`` () =
        let root = initializedChecklistReadyProject ()
        let report = TestSupport.runPlan root workId title
        let json = serializeReport report

        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Relationship = "optionalGovernancePolicy" && fact.State = "notEvaluated")
        Assert.DoesNotContain("\"route\"", json)
        Assert.DoesNotContain("\"freshness\"", json)
        Assert.DoesNotContain("\"profile\"", json)
        Assert.DoesNotContain("\"gate\"", json)
        Assert.DoesNotContain("\"audit\"", json)

    [<Fact>]
    let ``plan create and rerun complete under local harness budget`` () =
        let root = initializedChecklistReadyProject ()

        let createWatch = Stopwatch.StartNew()
        let createReport = TestSupport.runPlan root workId title
        createWatch.Stop()

        let rerunWatch = Stopwatch.StartNew()
        let rerunReport = TestSupport.runPlan root workId title
        rerunWatch.Stop()

        Assert.NotEqual(CommandOutcome.Blocked, createReport.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, rerunReport.Outcome)
        Assert.True(createWatch.Elapsed < TimeSpan.FromSeconds 2.0, $"Create took {createWatch.Elapsed}.")
        Assert.True(rerunWatch.Elapsed < TimeSpan.FromSeconds 2.0, $"Rerun took {rerunWatch.Elapsed}.")
