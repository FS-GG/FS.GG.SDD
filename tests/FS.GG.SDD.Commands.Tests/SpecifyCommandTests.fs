namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module SpecifyCommandTests =
    let workId = "005-specify-command"
    let title = "Specify Command"
    let charterPath = $"work/{workId}/charter.md"
    let specPath = $"work/{workId}/spec.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedCharteredProject () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        root

    let existingSpecification stage workIdValue =
        $"""---
schemaVersion: 1
workId: {workIdValue}
title: {title}
stage: {stage}
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# {title} Specification

Prose status: specified

## User Value
Existing user value remains.

## Scope
- SB-001: Existing scope remains.

## Non-Goals
- SB-002: Existing non-goal remains.

## User Stories
- US-001 (P1): Existing story remains.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Existing scenario remains.

## Functional Requirements
- FR-001: Existing requirement remains. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Existing impact remains.

## Lifecycle Notes
- Existing lifecycle note remains.
"""

    [<Fact>]
    let ``specify creates authored specification with real filesystem evidence`` () =
        let root = initializedCharteredProject ()

        let report = TestSupport.runSpecify root workId title
        let spec = TestSupport.readRelative root specPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: specify", spec)
        Assert.Contains("## User Value", spec)
        Assert.Contains("## Functional Requirements", spec)
        Assert.Contains("- FR-001:", spec)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = specPath && change.Operation = ArtifactOperation.Create)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath && view.Currency = GeneratedViewCurrency.Missing)
        Assert.Equal(Some Clarify, report.NextAction |> Option.bind _.Command)
        Assert.Contains(specPath, report.NextAction.Value.RequiredArtifacts)
        Assert.Equal(Some "FR-001", report.Specification |> Option.bind (fun summary -> summary.RequirementIds |> List.tryHead))

    [<Fact>]
    let ``specify creation does not require Governance files`` () =
        let root = initializedCharteredProject ()

        let report = TestSupport.runSpecify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated")

    [<Fact>]
    let ``specify rerun preserves authored content`` () =
        let root = initializedCharteredProject ()
        TestSupport.runSpecify root workId title |> ignore
        let authored = TestSupport.readRelative root specPath + "\nUser-authored prose stays here.\n"
        TestSupport.writeRelative root specPath authored

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root specPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = specPath && change.Operation = ArtifactOperation.NoChange)

    // §3.2 (FR-002, SC-002): an edited-but-section-complete spec re-run is never a bare,
    // ambiguous NoChange — the report carries the deterministic statement that specify
    // promotes only the first draft and that spec.md is read live by downstream stages.
    [<Fact>]
    let ``specify edited rerun reports live-read statement and is never bare NoChange`` () =
        let root = initializedCharteredProject ()
        TestSupport.runSpecify root workId title |> ignore
        let edited =
            (TestSupport.readRelative root specPath)
                .Replace("Prose status: specified", "Prose status: specified\nAuthor added a clarifying sentence.")
        TestSupport.writeRelative root specPath edited

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.True(report.NextAction.IsSome)
        let action = report.NextAction.Value
        Assert.Equal("specify.next.clarify", action.ActionId)
        Assert.Equal(Some Clarify, action.Command)
        Assert.Contains("read live", action.Reason)

    [<Fact>]
    let ``specify safely appends missing standard sections`` () =
        let root = initializedCharteredProject ()
        let partial = (existingSpecification "specify" workId).Replace("## Ambiguities\nNo material ambiguities recorded.\n\n", "")
        TestSupport.writeRelative root specPath partial

        let report = TestSupport.runSpecify root workId title
        let after = TestSupport.readRelative root specPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("Existing user value remains.", after)
        Assert.Contains("## Ambiguities", after)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = specPath && change.Operation = ArtifactOperation.Update)

    [<Fact>]
    let ``specify identity mismatch blocks before authored write`` () =
        let root = initializedCharteredProject ()
        let original = existingSpecification "specify" "999-other-work"
        TestSupport.writeRelative root specPath original

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "specificationIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root specPath)

    [<Fact>]
    let ``specify missing charter blocks before specification write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingCharterPrerequisite")
        Assert.False(TestSupport.existsRelative root specPath)

    [<Fact>]
    let ``specify missing intent blocks new specification`` () =
        let root = initializedCharteredProject ()
        let request = { TestSupport.specifyRequest root workId title with InputText = None }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationIntent")
        // FR-009: the correction shows the exact labeled-line --input form the parser accepts.
        Assert.Contains(
            report.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "missingSpecificationIntent"
                && diagnostic.Correction.Contains("value:")
                && diagnostic.Correction.Contains("scope:")
                && diagnostic.Correction.Contains("requirement:"))
        Assert.False(TestSupport.existsRelative root specPath)

    [<Fact>]
    let ``specify malformed and duplicate ids block before authored write`` () =
        let root = initializedCharteredProject ()
        let original =
            (existingSpecification "specify" workId)
                .Replace("- US-001 (P1): Existing story remains.", "- US-001 (P1): Existing story remains.\n- US-001 (P1): Duplicate story.")
                .Replace("Stories: US-001; Acceptance: AC-001", "Stories: US-999; Acceptance: AC-999")

        TestSupport.writeRelative root specPath original

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateSpecificationId")
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownSpecificationReference")
        Assert.Equal(original, TestSupport.readRelative root specPath)

    [<Fact>]
    let ``specify dry run reports proposed changes without mutation`` () =
        let root = initializedCharteredProject ()
        let request = { TestSupport.specifyRequest root workId title with DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root specPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = specPath && change.SafeWriteDecision = "dryRunOnly")

    [<Fact>]
    let ``specify refreshes generated work model when source data is valid`` () =
        let root = initializedCharteredProject ()
        TestSupport.writeValidTasksAndEvidence root

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.Sources |> List.exists (fun source -> source.Path = specPath))

    [<Fact>]
    let ``specify deterministic JSON is byte stable`` () =
        let root = initializedCharteredProject ()
        let request = { TestSupport.specifyRequest root workId title with DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"specify\"", first)
        Assert.Contains("\"specification\"", first)
        Assert.DoesNotContain(root, first)
