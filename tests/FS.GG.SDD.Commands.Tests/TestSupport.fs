namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.Text.RegularExpressions
open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.TestShared

module TestSupport =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    // Shared primitives now live in TestShared (feature 067 / FR-010); these delegate so the many
    // TestSupport.* call sites (and the Cli/Validation projects that link this file) stay stable.
    let findRepoRoot = TestShared.findRepoRoot
    let repoRoot = TestShared.repoRoot

    /// The configuration these tests were built under — "Release" in CI (gate.yml /
    /// release.yml build `-c Release`), "Debug" for a local `dotnet test`. Mirrors the
    /// proven Cli.Tests/ValidateCommandTests detection so the CLI smokes exercise the
    /// binary that was actually built for this run.
    let cliConfiguration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then
            "Release"
        else
            "Debug"

    /// The built fsgg-sdd host binary for the current configuration. A ProjectReference
    /// from the test project guarantees it exists before any smoke runs, so smokes invoke
    /// the real binary directly rather than `dotnet run --no-build` — which fails when only
    /// Debug was built, or silently exercises a stale Release binary.
    let cliDll =
        Path.Combine(repoRoot, "src", "FS.GG.SDD.Cli", "bin", cliConfiguration, "net10.0", "FS.GG.SDD.Cli.dll")

    /// Invoke the real host CLI directly and capture (exitCode, stdout, stderr). Kills the
    /// process and fails on timeout so a hung smoke never wedges the suite — see
    /// `TestShared.ChildProcess`, which drains both pipes concurrently and is what makes
    /// `timeoutMs` reachable at all (FS.GG.SDD#212).
    let runCliRaw (timeoutMs: int) (args: string list) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.WorkingDirectory <- repoRoot
        startInfo.ArgumentList.Add cliDll
        args |> List.iter startInfo.ArgumentList.Add

        let completion = TestShared.ChildProcess.runBounded timeoutMs startInfo
        completion.ExitCode, completion.StandardOutput, completion.StandardError

    let tempDirectory = TestShared.tempDirectory

    let request (command: SddCommand) (root: string) =
        { Command = command
          ProjectRoot = root
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = false
          GeneratorVersion = SchemaVersionModule.currentGeneratorVersion ()
          Provider = None
          Parameters = []
          Force = false
          TemplateUpdate = true
          AssumeYes = false
          IsInteractive = false
          Artifact = None
          Explain = false
          FromTests = None
          SurfaceUpdate = false
          AcceptUpstream = false }

    let readRelative (root: string) (path: string) =
        File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    let writeRelative = TestShared.writeRelative

    let existsRelative (root: string) (path: string) =
        File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    let runRequest request =
        let model, effects = init request

        let rec interpretUntilIdle state pending =
            match pending with
            | [] -> state
            | effects ->
                let results = interpretAll request.ProjectRoot request.DryRun effects

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (currentState, accumulatedEffects) result ->
                            let updatedState, producedEffects = update (EffectInterpreted result) currentState
                            updatedState, accumulatedEffects @ producedEffects)
                        (state, [])

                interpretUntilIdle nextState nextEffects

        let finalModel =
            interpretUntilIdle model effects |> fun state -> update BuildReport state |> fst

        finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)

    let initializeProject root =
        request Init root |> runRequest |> ignore

    let charterRequest root workId title =
        { request Charter root with
            WorkId = Some workId
            Title = Some title }

    let runCharter root workId title =
        charterRequest root workId title |> runRequest

    let specifyIntent =
        "value: create a native specify command\nscope: one chartered work item\nrequirement: create a specification artifact with stable ids"

    let specifyRequest root workId title =
        { request Specify root with
            WorkId = Some workId
            Title = Some title
            InputText = Some specifyIntent }

    let runSpecify root workId title =
        specifyRequest root workId title |> runRequest

    let clarifyIntent = "AMB-001: Clarification decisions live in clarifications.md."

    let specifyIntentWithAmbiguity =
        "value: create a native clarify command\nscope: one specified work item\nrequirement: create a clarification artifact with stable ids\nambiguity: where should durable clarification decisions be recorded?"

    let clarifyRequest root workId title =
        { request Clarify root with
            WorkId = Some workId
            Title = Some title
            InputText = Some clarifyIntent }

    let runClarify root workId title =
        clarifyRequest root workId title |> runRequest

    let checklistRequest root workId title =
        { request Checklist root with
            WorkId = Some workId
            Title = Some title }

    let runChecklist root workId title =
        checklistRequest root workId title |> runRequest

    let planRequest root workId title =
        { request Plan root with
            WorkId = Some workId
            Title = Some title }

    let runPlan root workId title =
        planRequest root workId title |> runRequest

    let tasksRequest root workId title =
        { request Tasks root with
            WorkId = Some workId
            Title = Some title }

    let runTasks root workId title =
        tasksRequest root workId title |> runRequest

    let analyzeRequest root workId title =
        { request Analyze root with
            WorkId = Some workId
            Title = Some title }

    let runAnalyze root workId title =
        analyzeRequest root workId title |> runRequest

    let evidenceRequest root workId title =
        { request Evidence root with
            WorkId = Some workId
            Title = Some title }

    let runEvidence root workId title =
        evidenceRequest root workId title |> runRequest

    let verifyRequest root workId title =
        { request Verify root with
            WorkId = Some workId
            Title = Some title }

    let runVerify root workId title =
        verifyRequest root workId title |> runRequest

    let shipRequest root workId title =
        { request Ship root with
            WorkId = Some workId
            Title = Some title }

    let runShip root workId title =
        shipRequest root workId title |> runRequest

    let agentsRequest root workId =
        { request Agents root with
            WorkId = Some workId }

    let runAgents root workId = agentsRequest root workId |> runRequest

    let refreshRequest root workId =
        { request Refresh root with
            WorkId = Some workId }

    let runRefresh root workId =
        refreshRequest root workId |> runRequest

    let assertRefreshDisposition (report: CommandReport) disposition =
        match report.Refresh with
        | Some summary ->
            if summary.Disposition <> disposition then
                failwith $"Expected refresh disposition {disposition}, got {summary.Disposition}."
        | None -> failwith "Expected refresh summary."

    let refreshViewState (report: CommandReport) view =
        match report.Refresh with
        | Some summary ->
            summary.PerViewState
            |> List.tryFind (fun (name, _) -> name = view)
            |> Option.map snd
            |> Option.defaultWith (fun () -> failwith $"Expected per-view state for {view}.")
        | None -> failwith "Expected refresh summary."

    /// FS.GG.SDD#351: author the plan, the way a human would.
    ///
    /// `plan` scaffolds every entry — `- PD-001 [FR-001] [AC-001] complete: Plan requirement FR-001
    /// through the plan command contract.` — and since #351 `analyze` BLOCKS while that prose is
    /// still sitting there. It has to: a decision-shaped hole with an id is not a decision, and the
    /// scaffold carries the refs by construction, so the traceability chain used to close with zero
    /// human authorship.
    ///
    /// So the fixture must do what an author does: keep the id and the refs and the kind token — the
    /// machine contract — and replace the *prose* after it, which is the part that requires judgement.
    /// That is exactly what the regex preserves and replaces.
    ///
    /// Every `initialize*Project` fixture chains through here, which is why ~169 tests went green
    /// again from this one edit rather than from 169.
    let authorPlanProse root workId =
        let path = $"work/{workId}/plan.md"

        let prose =
            "Authored by the test fixture: a real decision would say why, not restate the id."

        let authored =
            readRelative root path
            |> fun text ->
                Regex.Replace(
                    text,
                    @"(?m)^(- (?:PD|PC|VO|PM|GV)-\d+\b[^:\r\n]*: )(.+)$",
                    fun m -> m.Groups[1].Value + prose
                )
            // The Accepted Deferrals section keys its rows on the SOURCE id (`- DEC-002
            // acceptedDeferral: …`), not a `PD-###`, so the pattern above misses them and they stay
            // scaffold — which `analyze` then, correctly, blocks on. Only the fixtures whose clarify
            // carries an accepted deferral ever reach that line, which is why it surfaced in exactly
            // three tests and not the other 160.
            |> fun text ->
                Regex.Replace(
                    text,
                    @"(?m)^(- [A-Z]{2,4}-\d+ acceptedDeferral: )(.+)$",
                    fun m -> m.Groups[1].Value + prose
                )

        writeRelative root path authored

    let initializePlanReadyProject root workId title =
        initializeProject root
        runCharter root workId title |> ignore
        runSpecify root workId title |> ignore

        runRequest
            { clarifyRequest root workId title with
                InputText = None }
        |> ignore

        runChecklist root workId title |> ignore
        runPlan root workId title |> ignore
        authorPlanProse root workId

    // The T001..T005 evidence ladder is derived once in TestShared (feature 067 / FR-011).
    // Five, not six: the plan scaffold derives `PD-001` mirroring `FR-001`'s own refs, and since
    // #310 `tasks` folds that PD into the requirement task rather than deriving a duplicate task
    // for it. A sixth entry would reference a task the graph no longer contains and every
    // downstream stage would block on `evidence.unknownReference`.
    /// Bound once: the declaration and the artifacts it cites must be built from the SAME count.
    /// Two literals would let the fixture cite five proving tests while writing four — a
    /// cite-vs-write divergence, which is the exact defect #355 exists to remove.
    let ladderTaskCount = 5

    let passingTaskEvidence =
        TestShared.EvidenceLadder.passingTaskEvidence ladderTaskCount

    /// FS.GG.SDD#355: writes the evidence AND the artifacts it cites, together.
    ///
    /// They are inseparable now. Since #349, a `result: pass` citing a file that is not on disk is
    /// refused, so a fixture that wrote only the declaration would be asserting the very defect this
    /// item exists to remove. Every caller goes through here, which is why none of them had to change.
    let writePassingTaskEvidenceFor root workId =
        writeRelative root $"work/{workId}/evidence.yml" passingTaskEvidence
        TestShared.EvidenceLadder.writeArtifacts root ladderTaskCount

    let countOccurrences (needle: string) (haystack: string) =
        if needle = "" then
            0
        else
            let mutable count = 0
            let mutable index = haystack.IndexOf(needle, StringComparison.Ordinal)

            while index >= 0 do
                count <- count + 1
                index <- haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal)

            count

    /// FS.GG.SDD#306: declare `project.visualSurface` on an already-initialized `.fsgg/project.yml`.
    /// `checklist`, `tasks`, and `evidence` each already request that read effect, so the flag rides
    /// an existing read. Must be called after `initializeProject` and before `charter`.
    let declareVisualSurface root =
        let projectYml = readRelative root ".fsgg/project.yml"

        writeRelative
            root
            ".fsgg/project.yml"
            (projectYml.Replace("  defaultWorkRoot: work", "  defaultWorkRoot: work\n  visualSurface: true"))

    let initializeTasksReadyProject root workId title =
        initializePlanReadyProject root workId title
        runTasks root workId title |> ignore
        writePassingTaskEvidenceFor root workId

    let initializeAnalyzedProject root workId title =
        initializeTasksReadyProject root workId title
        runAnalyze root workId title |> ignore

    let initializeEvidencedProject root workId title =
        initializeAnalyzedProject root workId title
        runEvidence root workId title |> ignore

    let initializeVerifiedProject root workId title =
        initializeEvidencedProject root workId title
        runVerify root workId title |> ignore

    let validSpec workId title =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: draft
---

# {title} Specification

- FR-001: The selected work item has one typed requirement.
"""

    let validTasks =
        """schemaVersion: 1
tasks:
  - id: T001
    title: Implement selected lifecycle work
    status: pending
    owner: sdd
    dependencies: []
    requirements: [FR-001]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

    let validEvidence =
        """schemaVersion: 1
evidence: []
"""

    let writeValidWorkSources root workId title =
        writeRelative root $"work/{workId}/spec.md" (validSpec workId title)
        writeRelative root $"work/{workId}/tasks.yml" validTasks
        writeRelative root $"work/{workId}/evidence.yml" validEvidence

    let writeValidTasksAndEvidence root =
        writeRelative root "work/005-specify-command/tasks.yml" validTasks
        writeRelative root "work/005-specify-command/evidence.yml" validEvidence

    let writeValidTasksAndEvidenceFor root workId =
        writeRelative root $"work/{workId}/tasks.yml" validTasks
        writeRelative root $"work/{workId}/evidence.yml" validEvidence

    let validClarification workId title =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/{workId}/spec.md
publicOrToolFacingImpact: true
---

# {title} Clarifications

## Source Specification
- work/{workId}/spec.md

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

    let writeValidClarification root workId title =
        writeRelative root $"work/{workId}/clarifications.md" (validClarification workId title)

    let writeExistingChecklist root workId text =
        writeRelative root $"work/{workId}/checklist.md" text

    let dryRunDigest text = SchemaVersionModule.sha256Text text

    let assertChecklistSummary (report: CommandReport) itemCount resultCount =
        match report.Checklist with
        | Some summary ->
            if summary.ItemIds.Length <> itemCount || summary.ResultIds.Length <> resultCount then
                failwith
                    $"Expected checklist summary {itemCount}/{resultCount}, got {summary.ItemIds.Length}/{summary.ResultIds.Length}."
        | None -> failwith "Expected checklist summary."

    let assertPlanSummary (report: CommandReport) decisionCount contractCount obligationCount =
        match report.Plan with
        | Some summary ->
            if
                summary.DecisionIds.Length <> decisionCount
                || summary.ContractReferenceIds.Length <> contractCount
                || summary.VerificationObligationIds.Length <> obligationCount
            then
                failwith
                    $"Expected plan summary {decisionCount}/{contractCount}/{obligationCount}, got {summary.DecisionIds.Length}/{summary.ContractReferenceIds.Length}/{summary.VerificationObligationIds.Length}."
        | None -> failwith "Expected plan summary."

    let assertTasksSummary (report: CommandReport) taskCount dependencyCount requiredEvidenceCount =
        match report.Tasks with
        | Some summary ->
            if
                summary.TaskIds.Length <> taskCount
                || summary.DependencyCount <> dependencyCount
                || summary.RequiredEvidenceCount <> requiredEvidenceCount
            then
                failwith
                    $"Expected task summary {taskCount}/{dependencyCount}/{requiredEvidenceCount}, got {summary.TaskIds.Length}/{summary.DependencyCount}/{summary.RequiredEvidenceCount}."
        | None -> failwith "Expected tasks summary."

    let assertAnalysisSummary (report: CommandReport) readiness =
        match report.Analysis with
        | Some summary ->
            if summary.Readiness <> readiness then
                failwith $"Expected analysis readiness {readiness}, got {summary.Readiness}."
        | None -> failwith "Expected analysis summary."

    let assertEvidenceSummary (report: CommandReport) readiness =
        match report.Evidence with
        | Some summary ->
            if summary.Readiness <> readiness then
                failwith $"Expected evidence readiness {readiness}, got {summary.Readiness}."
        | None -> failwith "Expected evidence summary."

    let assertVerificationSummary (report: CommandReport) readiness =
        match report.Verification with
        | Some summary ->
            if summary.Readiness <> readiness then
                failwith $"Expected verification readiness {readiness}, got {summary.Readiness}."
        | None -> failwith "Expected verification summary."

    let assertShipSummary (report: CommandReport) readiness disposition =
        match report.Ship with
        | Some summary ->
            if summary.Readiness <> readiness || summary.Disposition <> disposition then
                failwith
                    $"Expected ship readiness/disposition {readiness}/{disposition}, got {summary.Readiness}/{summary.Disposition}."
        | None -> failwith "Expected ship summary."

    let assertAgentGuidanceSummary (report: CommandReport) readiness disposition =
        match report.AgentGuidance with
        | Some summary ->
            if summary.Readiness <> readiness || summary.Disposition <> disposition then
                failwith
                    $"Expected agent-guidance readiness/disposition {readiness}/{disposition}, got {summary.Readiness}/{summary.Disposition}."
        | None -> failwith "Expected agent-guidance summary."
