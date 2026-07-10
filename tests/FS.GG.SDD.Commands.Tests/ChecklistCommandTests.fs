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
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.Create
        )

        Assert.Equal(Some Plan, report.NextAction |> Option.bind _.Command)
        Assert.Equal(Some "CHK-001", report.Checklist |> Option.bind (fun summary -> summary.ItemIds |> List.tryHead))
        Assert.Equal(1, report.Checklist.Value.PassedCount)

    // #105 Trap 3 (no `checklisted` intermediate state exists): a clean review auto-writes
    // `status: checklistReady` directly — there is no manual transition to author. Locked so a
    // future refactor cannot silently reintroduce an unpromoted intermediate status.
    [<Fact>]
    let ``checklist clean review auto-writes status checklistReady`` () =
        let root = initializedClarifiedProject ()

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("status: checklistReady", checklist)
        Assert.DoesNotContain("status: checklisted", checklist)

    [<Fact>]
    let ``checklist creation does not require Governance files`` () =
        let root = initializedClarifiedProject ()

        let report = TestSupport.runChecklist root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    // FR-002: a genuine blocking ambiguity blocks checklist with a correction that names the
    // recognized `## Remaining Ambiguity` grammar, so the author can self-clear the gate.
    [<Fact>]
    let ``checklist blocking ambiguity correction names the remaining-ambiguity grammar`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.writeRelative
            root
            specPath
            (coverageSpec "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)")

        let blockingClarification =
            clarifiedNoAmbiguity.Replace("No blocking ambiguity remains.", "- AMB-001: The scoring rule is unresolved.")

        TestSupport.writeRelative root clarificationPath blockingClarification

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "unresolvedBlockingAmbiguity")

        Assert.Contains("## Remaining Ambiguity", diagnostic.Correction)
        Assert.Contains("disclaimer", diagnostic.Correction)

    [<Fact>]
    let ``checklist missing specification blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.False(TestSupport.existsRelative root checklistPath)

    [<Fact>]
    let ``checklist missing clarification blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationPrerequisite")
        Assert.False(TestSupport.existsRelative root checklistPath)

    [<Fact>]
    let ``checklist failed requirements quality writes findings and does not advance to plan`` () =
        let root = TestSupport.tempDirectory ()
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

    // Feature 082 (#146, FR-002/FR-003, SC-001/SC-003): on the "not stale" re-run path the
    // tool must NOT re-ingest its own prior CHK/CR rows as authored input. A tool-injected
    // verdict that the current sources no longer justify is re-derived away, not preserved.
    // Here FR-001 is covered (so its derived verdict is `pass`); we tamper the derived result
    // to a `fail` blocking verdict WITHOUT changing spec/clarify (snapshot still matches → the
    // not-stale path). Before this feature the tampered row was preserved and re-counted as
    // blocking; now it is reclaimed by re-derivation.
    [<Fact>]
    let ``checklist re-run re-derives machine rows and drops an orphaned blocking verdict`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.writeRelative
            root
            specPath
            (coverageSpec "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)")

        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity
        TestSupport.runChecklist root workId title |> ignore

        let tampered =
            (TestSupport.readRelative root checklistPath)
                .Replace(
                    "pass: Requirement FR-001 is testable and linked to acceptance coverage.",
                    "fail: Requirement FR-001 is missing acceptance coverage."
                )

        TestSupport.writeRelative root checklistPath tampered

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(0, report.Checklist.Value.FailedBlockingCount)
        Assert.Equal(1, report.Checklist.Value.PassedCount)

        Assert.DoesNotContain(
            "fail: Requirement FR-001 is missing acceptance coverage.",
            TestSupport.readRelative root checklistPath
        )

    [<Fact>]
    let ``checklist rerun preserves authored content and stable results`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let authored =
            TestSupport.readRelative root checklistPath
            + "\nUser-authored checklist prose stays here.\n"

        TestSupport.writeRelative root checklistPath authored

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root checklistPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.NoChange
        )

    // §3.1 (FR-001, SC-001): a stale re-run purges every machine-derived result row and
    // re-derives the full set from current sources — no superseded row or stale advisory
    // survives. (Rewritten from the prior append-stale expectation.)
    [<Fact>]
    let ``checklist stale rerun purges superseded rows and re-derives from current sources`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.writeRelative
            root
            specPath
            (coverageSpec "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)")

        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity
        TestSupport.runChecklist root workId title |> ignore

        // Author corrects/extends the source so the verdict set should change.
        TestSupport.writeRelative
            root
            specPath
            (coverageSpec
                "- FR-001: The command creates checklist output. (Stories: US-001; Acceptance: AC-001)\n- FR-002: The command links coverage. (Stories: US-001; Acceptance: AC-001)")

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
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.writeRelative
            root
            specPath
            (coverageSpec "- FR-001: First requirement.\n- FR-002: Second requirement.")

        TestSupport.writeRelative root clarificationPath clarifiedNoAmbiguity
        let firstReport = TestSupport.runChecklist root workId title
        Assert.Equal(2, firstReport.Checklist.Value.FailedBlockingCount)

        // Fix FR-001 only by giving it acceptance coverage.
        TestSupport.writeRelative
            root
            specPath
            (coverageSpec
                "- FR-001: First requirement. (Stories: US-001; Acceptance: AC-001)\n- FR-002: Second requirement.")

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = checklistPath && change.Operation = ArtifactOperation.NoChange
        )

    [<Fact>]
    let ``checklist identity mismatch blocks without mutating existing checklist`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let original =
            (TestSupport.readRelative root checklistPath).Replace($"workId: {workId}", "workId: 999-other-work")

        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "checklistIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist duplicate id blocks without mutating existing checklist`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let original =
            (TestSupport.readRelative root checklistPath).Replace("- CHK-001", "- CHK-001\n- CHK-001")

        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateChecklistId")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist unknown source reference blocks without mutation`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let original =
            (TestSupport.readRelative root checklistPath).Replace("[FR-001]", "[FR-999]")

        TestSupport.writeRelative root checklistPath original

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownChecklistSourceReference")
        Assert.Equal(original, TestSupport.readRelative root checklistPath)

    [<Fact>]
    let ``checklist unsafe overwrite marker blocks without mutation`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let original =
            TestSupport.readRelative root checklistPath
            + "\n<!-- fsgg-sdd: unsafe-overwrite -->\n"

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = checklistPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``checklist refreshes generated work model when source data is valid`` () =
        let root = initializedClarifiedProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources |> List.exists (fun source -> source.Path = checklistPath)
        )

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

    // Feature 081 (#144): a review result missing its [CHK:CHK-###] back-reference is reported
    // by its own diagnostic naming that cause — NOT malformedChecklistFrontMatter (the problem
    // is not front matter at all).
    [<Fact>]
    let ``checklist missing back-reference reports missingChecklistBackReference not front matter`` () =
        let root = initializedClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore

        let withBadResult =
            (TestSupport.readRelative root checklistPath)
                .Replace(
                    "## Review Results\n",
                    "## Review Results\n- CR-909 [FR-001] pass: A result with no back-reference.\n"
                )

        TestSupport.writeRelative root checklistPath withBadResult

        let report = TestSupport.runChecklist root workId title
        let ids = report.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)

        Assert.Contains("missingChecklistBackReference", ids)
        Assert.DoesNotContain("malformedChecklistFrontMatter", ids)

    // ---- #306: the between-requirements incoherence prompt --------------------------------------

    let private visualSurfaceClarifiedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.declareVisualSurface root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        root

    /// FR-007: one advisory row, over the whole requirement set, naming the defect class that no
    /// per-requirement review can reach.
    [<Fact>]
    let ``checklist prompts for between-requirement incoherence when a visual surface is declared`` () =
        let root = visualSurfaceClarifiedProject ()

        let report = TestSupport.runChecklist root workId title
        let checklist = TestSupport.readRelative root checklistPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("incoherence that exists only BETWEEN them", checklist)
        Assert.Contains("draw order versus geometry", checklist)
        // The row references the requirement set whose conjunction is unreviewed...
        Assert.Contains("[FR-001] advisory: Requirements are reviewed for incoherence", checklist)
        // ...and it is ADVISORY, never blocking: the checklist re-derives rows from source on every
        // run, so a blocking row an author reviewed and passed would reappear and dead-end `plan`.
        Assert.DoesNotContain("blocking: Requirements are reviewed for incoherence", checklist)

    /// FR-007: undeclared workspaces see no such row (SC-001 at the checklist seam).
    [<Fact>]
    let ``checklist derives no incoherence prompt when nothing is declared`` () =
        let root = initializedClarifiedProject ()

        TestSupport.runChecklist root workId title |> ignore
        let checklist = TestSupport.readRelative root checklistPath

        Assert.DoesNotContain("incoherence", checklist)

    /// The advisory row is a re-derived, tool-owned section: a second run reproduces it byte for byte.
    [<Fact>]
    let ``checklist re-derives the incoherence prompt idempotently`` () =
        let root = visualSurfaceClarifiedProject ()
        TestSupport.runChecklist root workId title |> ignore
        let first = TestSupport.readRelative root checklistPath

        let report = TestSupport.runChecklist root workId title
        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(first, TestSupport.readRelative root checklistPath)
