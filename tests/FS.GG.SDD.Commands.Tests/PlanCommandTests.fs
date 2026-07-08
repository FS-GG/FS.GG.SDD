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
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = planPath && change.Operation = ArtifactOperation.Create
        )

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

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``plan missing specification blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan missing clarification blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan missing checklist blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingChecklistPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    [<Fact>]
    let ``plan failed checklist blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root specPath missingCoverageSpec
        TestSupport.writeValidClarification root workId title
        TestSupport.runChecklist root workId title |> ignore

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "failedChecklistPrerequisite")
        Assert.False(TestSupport.existsRelative root planPath)

    // #105 Trap 3 / FR-003: a non-checklistReady status points the author at re-running
    // `fsgg-sdd checklist` (which auto-promotes) rather than at hand-editing the status.
    [<Fact>]
    let ``plan non-ready checklist status correction names the auto-promotion route`` () =
        let root = initializedChecklistReadyProject ()

        let tampered =
            (TestSupport.readRelative root checklistPath).Replace("status: checklistReady", "status: needsCorrection")

        TestSupport.writeRelative root checklistPath tampered

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "failedChecklistPrerequisite")

        Assert.Contains("checklistReady", diagnostic.Correction)
        Assert.Contains("fsgg-sdd checklist", diagnostic.Correction)
        Assert.Contains("do not hand-edit", diagnostic.Correction)

    [<Fact>]
    let ``plan rerun preserves authored content and stable ids`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let authored =
            TestSupport.readRelative root planPath
            + "\nUser-authored plan prose stays here.\n"

        TestSupport.writeRelative root planPath authored

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root planPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = planPath && change.Operation = ArtifactOperation.NoChange
        )

    // ---------------------------------------------------------------------------------------
    // Feature 090 (#163): plan owns its `## Source Snapshot` and never writes authored prose.
    // ---------------------------------------------------------------------------------------

    /// Drive a work item to a planned state, then edit `spec.md` so the plan's recorded digests
    /// go stale. Returns (root, the exact `plan.md` bytes before the stale re-run).
    let private plannedThenSpecEdited () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let before = TestSupport.readRelative root planPath

        let updatedSpec =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Ambiguities",
                    "- FR-002: New requirement links to AC-001. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
                )

        TestSupport.writeRelative root specPath updatedSpec
        root, before

    let private acceptUpstream root =
        TestSupport.runRequest
            { TestSupport.planRequest root workId title with
                AcceptUpstream = true }

    /// C2 / FR-001..FR-003 / SC-002. The regression this feature exists to prevent. Before the
    /// change this run exited 0, appended a synthesized `PD-### … stale:` line to the operator's
    /// `## Plan Decisions`, and left the digests stale.
    [<Fact>]
    let ``stale upstream blocks plan and leaves plan.md byte-identical`` () =
        let root, before = plannedThenSpecEdited ()

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(before, TestSupport.readRelative root planPath)
        Assert.Empty(report.ChangedArtifacts)

        let stale =
            report.Diagnostics
            |> List.filter (fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

        Assert.Single(stale) |> ignore
        Assert.Equal<string list>([ specPath ], stale.Head.RelatedIds)
        Assert.Equal(Some "plan.acceptUpstream", report.NextAction |> Option.map _.ActionId)

    /// C14 / FR-001. The tool never authors a `PD-###` line, on any path.
    [<Fact>]
    let ``stale plan re-run never appends a synthesized stale decision`` () =
        let root, _ = plannedThenSpecEdited ()

        TestSupport.runPlan root workId title |> ignore
        Assert.DoesNotContain("stale:", TestSupport.readRelative root planPath)

        acceptUpstream root |> ignore
        Assert.DoesNotContain("stale:", TestSupport.readRelative root planPath)

    /// C3 / FR-004 / SC-002. `--accept-upstream` refreshes the snapshot, appends the derived rows
    /// `plan` has always appended for genuinely-new upstream ids, and alters no pre-existing line.
    [<Fact>]
    let ``accept-upstream refreshes the snapshot without altering authored lines`` () =
        let root, before = plannedThenSpecEdited ()

        let report = acceptUpstream root
        let after = TestSupport.readRelative root planPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.NotEqual<string>(before, after)
        Assert.Contains("FR-002", after) // the derived append; pre-existing behavior, not the defect

        // Every line of the pre-run plan that was NOT a Source Snapshot digest row survives verbatim.
        let snapshotRow (line: string) = line.Contains "sha256"

        let survivors =
            before.Replace("\r\n", "\n").Split('\n')
            |> Array.filter (fun line -> not (snapshotRow line))

        let afterLines = after.Replace("\r\n", "\n").Split('\n') |> Set.ofArray

        for line in survivors do
            Assert.True(afterLines.Contains line, $"accept-upstream altered or removed authored line: {line}")

        // and the snapshot really did move
        Assert.NotEqual<string list>(
            before.Replace("\r\n", "\n").Split('\n')
            |> Array.filter snapshotRow
            |> Array.toList,
            after.Replace("\r\n", "\n").Split('\n')
            |> Array.filter snapshotRow
            |> Array.toList
        )

    /// C4 / FR-005. The flag is a no-op on a plan whose snapshot is already current.
    [<Fact>]
    let ``accept-upstream on a current snapshot is a no-op`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore
        let before = TestSupport.readRelative root planPath

        let report = acceptUpstream root

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(before, TestSupport.readRelative root planPath)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

    /// C5 / FR-007. Creation is not a stale path; the flag is inert there.
    [<Fact>]
    let ``accept-upstream on the creation path matches a bare plan`` () =
        let bare = initializedChecklistReadyProject ()
        let bareReport = TestSupport.runPlan bare workId title

        let accepted = initializedChecklistReadyProject ()
        let acceptedReport = acceptUpstream accepted

        Assert.Equal(bareReport.Outcome, acceptedReport.Outcome)
        Assert.Equal(TestSupport.readRelative bare planPath, TestSupport.readRelative accepted planPath)

    /// C6 / FR-006. `--accept-upstream` accepts the upstream; it does not force a write.
    [<Fact>]
    let ``accept-upstream does not override an unrelated blocking diagnostic`` () =
        let root, _ = plannedThenSpecEdited ()

        let corrupted =
            (TestSupport.readRelative root planPath)
                .Replace($"sourceSpec: {specPath}", "sourceSpec: work/other/spec.md")

        TestSupport.writeRelative root planPath corrupted

        let report = acceptUpstream root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(corrupted, TestSupport.readRelative root planPath)
        Assert.Empty(report.ChangedArtifacts)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedPlanFrontMatter")

    /// C7 / FR-002 / FR-014. All changed sources are named, ordinally sorted.
    [<Fact>]
    let ``stalePlanSnapshot names every changed source in ordinal order`` () =
        let root, _ = plannedThenSpecEdited ()

        TestSupport.writeRelative
            root
            clarificationPath
            (TestSupport.readRelative root clarificationPath + "\n<!-- touched -->\n")

        let report = TestSupport.runPlan root workId title

        let stale =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

        Assert.Equal<string list>([ clarificationPath; specPath ], stale.RelatedIds)

        Assert.Equal<string list>(
            List.sortWith (fun a b -> String.CompareOrdinal(a, b)) stale.RelatedIds,
            stale.RelatedIds
        )

    /// C8 / C9 / FR-008. Downstream stages detect the drift from the digests, not from a marker
    /// `plan` used to inject into the operator's prose.
    [<Fact>]
    let ``tasks and analyze block on a stale plan snapshot`` () =
        let root, _ = plannedThenSpecEdited ()

        for report in
            [ TestSupport.runTasks root workId title
              TestSupport.runAnalyze root workId title ] do
            Assert.Equal(CommandOutcome.Blocked, report.Outcome)
            Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")
            Assert.Equal(Some "plan.acceptUpstream", report.NextAction |> Option.map _.ActionId)

    /// C10 / FR-008. Accepting the upstream is the operator's gesture at `plan`, never an implicit
    /// downstream one.
    [<Fact>]
    let ``accept-upstream is not honored by tasks`` () =
        let root, _ = plannedThenSpecEdited ()

        let report =
            TestSupport.runRequest
                { TestSupport.tasksRequest root workId title with
                    AcceptUpstream = true }

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

    /// C11 / FR-016. An absent recorded *digest* is not evidence of change: an old plan must not
    /// become blocked on upgrade.
    ///
    /// The row must keep its `spec:` label and path so `parsePlanSourceSnapshots` still yields a
    /// `PlanSourceSnapshot` with `Digest = None` — dropping the label makes the line unparseable,
    /// which exercises the *empty-list* path instead and passes vacuously.
    [<Fact>]
    let ``a snapshot entry without a digest is not stale`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let plan = TestSupport.readRelative root planPath

        let digestless =
            plan.Replace("\r\n", "\n").Split('\n')
            |> Array.map (fun line ->
                if line.Contains specPath && line.Contains "sha256" then
                    $"- spec: {specPath}"
                else
                    line)
            |> String.concat "\n"

        TestSupport.writeRelative root planPath digestless

        // the digest-less row really did survive parsing (guards against a vacuous pass)
        Assert.Contains($"- spec: {specPath}", TestSupport.readRelative root planPath)

        TestSupport.writeRelative
            root
            specPath
            ((TestSupport.readRelative root specPath).Replace("## Ambiguities", "<!-- moved -->\n\n## Ambiguities"))

        let report = TestSupport.runPlan root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

    /// Review finding. Gating the snapshot rewrite on `stale` made an empty `## Source Snapshot`
    /// unrecoverable — nothing recorded means nothing "changed", so the refresh never ran, the
    /// section stayed empty, and FR-008/SC-004 were permanently disabled for that plan while
    /// `--accept-upstream` reported `noChange`. An operator escaping a block by deleting the rows
    /// must be able to re-establish the baseline.
    [<Fact>]
    let ``accept-upstream re-establishes an emptied source snapshot`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let emptied =
            let lines =
                (TestSupport.readRelative root planPath).Replace("\r\n", "\n").Split('\n')

            let mutable inSnapshot = false

            [ for line in lines do
                  if line.StartsWith "## Source Snapshot" then
                      inSnapshot <- true
                      yield line
                  elif inSnapshot && line.StartsWith "## " then
                      inSnapshot <- false
                      yield line
                  elif inSnapshot && line.TrimStart().StartsWith "- " then
                      () // drop every recorded digest row
                  else
                      yield line ]
            |> String.concat "\n"

        TestSupport.writeRelative root planPath emptied
        Assert.DoesNotContain("sha256", TestSupport.readRelative root planPath)

        // the guard is currently blind: nothing recorded, so nothing is stale
        TestSupport.writeRelative
            root
            specPath
            ((TestSupport.readRelative root specPath).Replace("## Ambiguities", "<!-- moved -->\n\n## Ambiguities"))

        // one gesture re-establishes it...
        let accepted = acceptUpstream root
        Assert.NotEqual(CommandOutcome.Blocked, accepted.Outcome)
        Assert.Contains("sha256", TestSupport.readRelative root planPath)

        // ...and the drift guard works again
        TestSupport.writeRelative
            root
            specPath
            ((TestSupport.readRelative root specPath).Replace("<!-- moved -->", "<!-- moved again -->"))

        let report = TestSupport.runPlan root workId title
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

    /// Review finding, FR-008. `resolvePrerequisites` is shared: `evidence`, `verify`, and `ship`
    /// fold the same `PlanDiagnostics` list into their reports. A stale plan snapshot must NOT brick
    /// the back half of the lifecycle — `tasks` and `analyze` derive from the plan, those three do not.
    [<Fact>]
    let ``a stale plan snapshot does not block evidence verify or ship`` () =
        let root, _ = plannedThenSpecEdited ()

        for report in
            [ TestSupport.runEvidence root workId title
              TestSupport.runVerify root workId title
              TestSupport.runShip root workId title ] do
            Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

    /// C13 / FR-009. The safety net for *authored* staleness survives: `plan` warns, `tasks` blocks.
    [<Fact>]
    let ``an operator-authored stale decision still warns at plan and blocks at tasks`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let authored =
            (TestSupport.readRelative root planPath)
                .Replace("## Contract Impact", "- PD-900 stale: I need to revisit this.\n\n## Contract Impact")

        TestSupport.writeRelative root planPath authored

        let planReport = TestSupport.runPlan root workId title
        Assert.Contains(planReport.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanDecision")

        let tasksReport = TestSupport.runTasks root workId title
        Assert.Equal(CommandOutcome.Blocked, tasksReport.Outcome)

        Assert.Contains(
            tasksReport.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "failedPlanPrerequisite"
                && diagnostic.Message = "Plan contains stale decisions."
        )

    /// T011. The bool predicate and the changed-path projection can never disagree.
    [<Fact>]
    let ``changed source paths agree with the staleness predicate`` () =
        let root, _ = plannedThenSpecEdited ()

        // stale: non-empty changed set, predicate true
        let staleReport = TestSupport.runPlan root workId title

        Assert.Contains(staleReport.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")

        // accepted: empty changed set, predicate false
        acceptUpstream root |> ignore
        let currentReport = TestSupport.runPlan root workId title

        Assert.DoesNotContain(currentReport.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")
        Assert.NotEqual(CommandOutcome.Blocked, currentReport.Outcome)

    /// FR-011 / US4. The authoring-window advisory is emitted exactly once on a successful plan
    /// (creation and coherent re-run), names the three snapshotted sources, and adds a fact — not
    /// an outcome. It must never appear on the blocked path, where the plan snapshotted nothing.
    [<Fact>]
    let ``plan announces the authoring window without changing the outcome`` () =
        let root = initializedChecklistReadyProject ()

        let created = TestSupport.runPlan root workId title

        let advisories =
            created.Diagnostics
            |> List.filter (fun diagnostic -> diagnostic.Id = "planAuthoringWindow")

        Assert.Single(advisories) |> ignore
        Assert.Equal(FS.GG.SDD.Artifacts.Diagnostics.DiagnosticInfo, advisories.Head.Severity)
        Assert.Equal<string list>([ specPath; clarificationPath; checklistPath ], advisories.Head.RelatedIds)

        // adds a fact, not an outcome: creation still Succeeded, a coherent re-run still NoChange
        Assert.Equal(CommandOutcome.Succeeded, created.Outcome)
        let rerun = TestSupport.runPlan root workId title
        Assert.Equal(CommandOutcome.NoChange, rerun.Outcome)
        // every listed artifact is NoChange — the advisory wrote nothing
        Assert.All(rerun.ChangedArtifacts, fun change -> Assert.Equal(ArtifactOperation.NoChange, change.Operation))
        Assert.Contains(rerun.Diagnostics, fun diagnostic -> diagnostic.Id = "planAuthoringWindow")

        // never on the blocked re-run path
        let blocked, _ = plannedThenSpecEdited ()
        let blockedReport = TestSupport.runPlan blocked workId title
        Assert.Equal(CommandOutcome.Blocked, blockedReport.Outcome)
        Assert.DoesNotContain(blockedReport.Diagnostics, fun diagnostic -> diagnostic.Id = "planAuthoringWindow")

        // ...nor on a blocked CREATION, where no plan.md is written at all: an advisory claiming the
        // plan "snapshotted its sources" would assert a file that does not exist (review finding).
        let fresh = initializedChecklistReadyProject ()

        TestSupport.writeRelative
            fresh
            checklistPath
            ((TestSupport.readRelative fresh checklistPath)
                .Replace("## Blocking Findings", "- CHK-900: covers DEC-999.\n\n## Blocking Findings"))

        let blockedCreate = TestSupport.runPlan fresh workId title

        if blockedCreate.Outcome = CommandOutcome.Blocked then
            Assert.DoesNotContain(blockedCreate.Diagnostics, fun diagnostic -> diagnostic.Id = "planAuthoringWindow")

    /// Review finding. The `plan.acceptUpstream` NextAction fires whenever the snapshot is stale,
    /// which may be alongside an unrelated blocker. It must report the FULL blocking set, not just
    /// its own id — an agent driving off `BlockingDiagnosticIds` would otherwise loop once per
    /// hidden blocker.
    [<Fact>]
    let ``plan.acceptUpstream reports every co-occurring blocking diagnostic`` () =
        let root, _ = plannedThenSpecEdited ()

        // add a second, unrelated blocker: a plan decision referencing an id nothing declares
        let withUnknownRef =
            (TestSupport.readRelative root planPath)
                .Replace("## Contract Impact", "- PD-901 [FR-404] complete: dangling reference.\n\n## Contract Impact")

        TestSupport.writeRelative root planPath withUnknownRef

        let report = TestSupport.runTasks root workId title
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let action = report.NextAction.Value
        Assert.Equal("plan.acceptUpstream", action.ActionId)
        Assert.Contains("stalePlanSnapshot", action.BlockingDiagnosticIds)

        // every blocking diagnostic id is named, not just stalePlanSnapshot
        let blockingIds =
            report.Diagnostics
            |> List.filter (fun d -> d.Severity = FS.GG.SDD.Artifacts.Diagnostics.DiagnosticError)
            |> List.map _.Id
            |> List.distinct
            |> List.sort

        Assert.Equal<string list>(blockingIds, action.BlockingDiagnosticIds)
        Assert.True(blockingIds.Length > 1, $"expected a co-occurring blocker, got {blockingIds}")

    /// FR-014 / SC-005. Both new paths are byte-deterministic: identical inputs, identical report.
    /// The blocked path writes nothing, so re-running it feeds the same input twice. The
    /// `--accept-upstream` path *does* mutate, so determinism is checked across two independently
    /// constructed fixtures rather than by re-running against the file it just rewrote.
    [<Fact>]
    let ``blocked and accept-upstream paths are deterministic`` () =
        let blockedA, _ = plannedThenSpecEdited ()
        let first = TestSupport.runPlan blockedA workId title |> serializeReport
        let second = TestSupport.runPlan blockedA workId title |> serializeReport
        Assert.Equal(first, second)

        let acceptA, _ = plannedThenSpecEdited ()
        let acceptB, _ = plannedThenSpecEdited ()
        Assert.Equal(acceptUpstream acceptA |> serializeReport, acceptUpstream acceptB |> serializeReport)

        // and re-accepting an already-current plan is an idempotent no-op (FR-005)
        Assert.Equal(CommandOutcome.NoChange, (acceptUpstream acceptA).Outcome)

    [<Fact>]
    let ``plan identity mismatch blocks without mutating existing plan`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let original =
            (TestSupport.readRelative root planPath).Replace($"workId: {workId}", "workId: 999-other-work")

        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "planIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan duplicate id blocks without mutating existing plan`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let original =
            (TestSupport.readRelative root planPath).Replace("- PD-001", "- PD-001\n- PD-001")

        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicatePlanId")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan unknown source reference blocks without mutation`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let original =
            (TestSupport.readRelative root planPath).Replace("[FR-001]", "[FR-999]")

        TestSupport.writeRelative root planPath original

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownPlanSourceReference")
        Assert.Equal(original, TestSupport.readRelative root planPath)

    [<Fact>]
    let ``plan unsafe overwrite marker blocks without mutation`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.runPlan root workId title |> ignore

        let original =
            TestSupport.readRelative root planPath
            + "\n<!-- fsgg-sdd: unsafe-overwrite -->\n"

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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = planPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``plan refreshes generated work model when source data is valid`` () =
        let root = initializedChecklistReadyProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runPlan root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources |> List.exists (fun source -> source.Path = planPath)
        )

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

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Relationship = "optionalGovernancePolicy" && fact.State = "notEvaluated"
        )

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
