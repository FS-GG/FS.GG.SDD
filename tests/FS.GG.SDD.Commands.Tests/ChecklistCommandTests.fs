namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module ChecklistCommandTests =
    let workId = "007-checklist-command"
    let title = "Checklist Command"
    let specPath = $"work/{workId}/spec.md"
    let clarificationPath = $"work/{workId}/clarifications.md"
    let checklistPath = $"work/{workId}/checklist.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedClarifiedProject () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore
        TestSupport.runRequest { TestSupport.clarifyRequest root workId title with InputText = None } |> ignore
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
Review requirements before planning.

## Scope
- SB-001: Add checklist command behavior.

## Non-Goals
- SB-002: Do not add plan behavior.

## User Stories
- US-001 (P1): As a maintainer, I can review requirements.

## Acceptance Scenarios
No acceptance scenarios recorded.

## Functional Requirements
- FR-001: The command creates checklist output.

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Checklist report JSON is tool-facing.

## Lifecycle Notes
- Next lifecycle action: clarify.
"""

    let coverageSpec (requirements: string) =
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
Review requirements before planning.

## Scope
- SB-001: Add checklist command behavior.

## Non-Goals
- SB-002: Do not add plan behavior.

## User Stories
- US-001 (P1): As a maintainer, I can review requirements.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Requirement is exercised end-to-end.

## Functional Requirements
{requirements}

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Checklist report JSON is tool-facing.

## Lifecycle Notes
- Next lifecycle action: clarify.
"""

    let clarifiedNoAmbiguity =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: {specPath}
publicOrToolFacingImpact: true
---

# {title} Clarifications

## Source Specification
- {specPath}

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
No concrete decisions recorded.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: checklist.
"""

    [<Fact>]
    let ``checklist creates authored checklist with real filesystem evidence`` () =
        let root = initializedClarifiedProject ()

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: checklist", checklist)
        Assert.Contains("## Checklist Items", checklist)
        Assert.Contains("CHK-001", checklist)
        Assert.Contains("CR-001", checklist)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.Create)
        Assert.Equal(Some Plan, report.NextAction |> Option.bind _.Command)
        Assert.Equal(Some "CHK-001", report.Checklist |> Option.bind (fun summary -> summary.ItemIds |> List.tryHead))
        Assert.Equal(1, report.Checklist.Value.PassedCount)

    [<Fact>]
    let ``checklist creation does not require Governance files`` () =
        let root = initializedClarifiedProject ()

        let report = TestSupport.runChecklist root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated")

    [<Fact>]
    let ``checklist missing specification blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.False(TestSupport.existsRelative root checklistPath)

    [<Fact>]
    let ``checklist missing clarification blocks before authored write`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationPrerequisite")
        Assert.False(TestSupport.existsRelative root checklistPath)

    [<Fact>]
    let ``checklist failed requirements quality writes findings and does not advance to plan`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeRelative root specPath missingCoverageSpec
        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains("fail: Requirement FR-001 is missing acceptance coverage.", checklist)
        // FR-007: the missing-coverage correction shows the exact expected coverage form inline.
        Assert.Contains("Add a coverage line for FR-001", checklist)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "failedRequirementsQuality")
        Assert.Equal(Some "correctBlockingDiagnostics", report.NextAction |> Option.map _.ActionId)
        Assert.True(report.Checklist.Value.FailedBlockingCount > 0)

    [<Fact>]
    let ``checklist rerun preserves authored content and stable results`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let authored = TestSupport.readRelative root checklistPath + "\nUser-authored checklist prose stays here.\n"
        TestSupport.writeRelative root checklistPath authored

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root checklistPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.NoChange)

    // §3.1 (FR-001, SC-001): a stale re-run purges every machine-derived result row and
    // re-derives the full set from current sources — no superseded row or stale advisory
    // survives. (Rewritten from the prior append-stale expectation.)
    [<Fact>]
    let ``checklist stale rerun purges superseded rows and re-derives from current sources`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeRelative root specPath (coverageSpec "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)")
        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity
        TestSupport.runChecklist root workId title |> ignore

        // Author corrects/extends the source so the verdict set should change.
        TestSupport.writeRelative root specPath (coverageSpec "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)\n- FR-002: The command links coverage. (Stories: US-001; Acceptance: AC-001)")

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.Contains("FR-002", checklist)
        Assert.DoesNotContain("stale:", checklist)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleChecklistResult")
        Assert.Equal(0, report.Checklist.Value.StaleResultCount)
        // The report reflects the current sources: two passing requirements, no failures.
        Assert.Equal(2, report.Checklist.Value.PassedCount)
        Assert.Equal(0, report.Checklist.Value.FailedBlockingCount)

    // §3.1 partial fix: re-evaluation is per-requirement — the corrected requirement flips
    // to pass while the still-failing one remains fail.
    [<Fact>]
    let ``checklist partial fix flips corrected requirement and keeps still-failing fail`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeRelative root specPath (coverageSpec "- FR-001: First requirement.\n- FR-002: Second requirement.")
        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity
        let firstReport = TestSupport.runChecklist root workId title
        Assert.Equal(2, firstReport.Checklist.Value.FailedBlockingCount)

        // Fix FR-001 only by giving it acceptance coverage.
        TestSupport.writeRelative root specPath (coverageSpec "- FR-001: First requirement. (Stories: US-001; Acceptance: AC-001)\n- FR-002: Second requirement.")

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.Equal(1, report.Checklist.Value.PassedCount)
        Assert.Equal(1, report.Checklist.Value.FailedBlockingCount)
        Assert.Contains("fail: Requirement FR-002 is missing acceptance coverage.", checklist)
        Assert.DoesNotContain("fail: Requirement FR-001 is missing acceptance coverage.", checklist)

    // §3.1 determinism (FR-012): an unchanged re-run preserves rows, reports noChange, and
    // is byte-identical.
    [<Fact>]
    let ``checklist unchanged rerun is no change and byte identical`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let firstFile = TestSupport.readRelative root checklistPath

        let report = TestSupport.runChecklist root workId title
        let secondFile = TestSupport.readRelative root checklistPath

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(firstFile, secondFile)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.NoChange)

    [<Fact>]
    let ``checklist identity mismatch blocks without mutating existing checklist`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let original = (TestSupport.readRelative root checklistPath).Replace($"workId: {workId}", "workId: 999-other-work")
        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "checklistIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist duplicate id blocks without mutating existing checklist`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let original = (TestSupport.readRelative root checklistPath).Replace("- CHK-001", "- CHK-001\n- CHK-001")
        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateChecklistId")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist unknown source reference blocks without mutation`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let original = (TestSupport.readRelative root checklistPath).Replace("[FR-001]", "[FR-999]")
        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownChecklistSourceReference")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist unsafe overwrite marker blocks without mutation`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let original = TestSupport.readRelative root checklistPath + "\n<!-- fsgg-sdd: unsafe-overwrite -->\n"
        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeOverwrite")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist dry run reports proposed changes without mutation`` () =
        let root = initializedClarifiedProject ()
        let request =
            { TestSupport.checklistRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root checklistPath)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = checklistPath && change.SafeWriteDecision = "dryRunOnly")

    [<Fact>]
    let ``checklist refreshes generated work model when source data is valid`` () =
        let root = initializedClarifiedProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)
        Assert.Contains(report.GeneratedViews, fun view ->
            view.Path = workModelPath
            && view.Currency = GeneratedViewCurrency.Current
            && view.Sources |> List.exists (fun source -> source.Path = checklistPath))

    [<Fact>]
    let ``checklist deterministic JSON is byte stable`` () =
        let root = initializedClarifiedProject ()
        let request =
            { TestSupport.checklistRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"checklist\"", first)
        Assert.Contains("\"checklist\"", first)
        Assert.DoesNotContain(root, first)
