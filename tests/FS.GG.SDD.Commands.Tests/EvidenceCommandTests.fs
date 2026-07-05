namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module EvidenceCommandTests =
    let workId = "011-evidence-command"
    let title = "Evidence Command"
    let evidencePath = $"work/{workId}/evidence.yml"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let workModelPath = $"readiness/{workId}/work-model.json"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedAnalyzedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        root

    let runEvidenceCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "evidence"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 30000

        { ExitCode = exitCode
          StdOut = stdout
          StdErr = stderr }

    let undisclosedSyntheticInput =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV999
    kind: synthetic
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    sourceRefs:
      - kind: transcript
        path: specs/011-evidence-command/readiness/synthetic-fsi.txt
        result: pass
    result: pass
    synthetic: true
"""

    [<Fact>]
    let ``evidence missingRequiredEvidence correction shows the non-synthetic pass form`` () =
        // FR-008: the surfaced unsatisfied-obligation diagnostic shows what satisfies it.
        let diagnostic =
            FS.GG.SDD.Commands.CommandReports.missingRequiredEvidence evidencePath [ "EV001" ]

        Assert.Equal("evidence.missingRequiredEvidence", diagnostic.Id)
        Assert.Contains("result: pass", diagnostic.Correction)
        Assert.Contains("synthetic: false", diagnostic.Correction)

    [<Fact>]
    let ``evidence creates authored evidence artifact with real filesystem evidence`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Contains("stage: evidence", evidence)
        Assert.Contains("status: evidenceReady", evidence)
        Assert.Contains($"sourceAnalysis: {analysisPath}", evidence)
        Assert.Contains("sourceSnapshots:", evidence)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.Kind = "authoredSource"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Equal(Some "evidence.next.verify", report.NextAction |> Option.map _.ActionId)

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> Assert.Equal("evidenceReady", artifact.Status)
        | Error diagnostics -> failwith $"Generated evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence missing analysis blocks without authored evidence write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title
        let before = TestSupport.readRelative root evidencePath

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.missingAnalysisPrerequisite")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence does not require Governance files`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        Assert.DoesNotContain(serializeReport report, "route")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``evidence dry run reports authored update without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``evidence blocks undisclosed synthetic evidence without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                InputText = Some undisclosedSyntheticInput }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.undisclosedSyntheticEvidence")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence deterministic JSON is byte stable`` () =
        let root = initializedAnalyzedProject ()

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"evidence\"", first)
        Assert.Contains("\"evidence\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``evidence text projection uses report facts`` () =
        let root = initializedAnalyzedProject ()
        let report = TestSupport.runEvidence root workId title
        let text = renderText report

        Assert.Contains("command: evidence", text)
        Assert.Contains($"evidencePath: {evidencePath}", text)
        Assert.Contains("evidenceReadiness: evidenceReady", text)
        Assert.Contains("nextAction: evidence.next.verify", text)

    [<Fact>]
    let ``evidence CLI JSON smoke creates evidence artifact`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"evidence.next.verify\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root evidencePath)

    [<Fact>]
    let ``evidence CLI dry run smoke avoids authored mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let result = runEvidenceCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence CLI text smoke renders human projection`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: evidence", result.StdOut)
        Assert.Contains("evidenceReadiness: evidenceReady", result.StdOut)
        Assert.Contains("nextAction: evidence.next.verify", result.StdOut)
        Assert.Equal("", result.StdErr)

    // Feature 077 (issue #124): auto-generated obligations must carry their originating
    // requirement / plan-decision refs so an author can classify each obligation from the
    // evidence.yml entry alone — no join back to tasks.yml by title. The default lifecycle graph
    // produced by initializeAnalyzedProject includes a plan-decision task
    // (`Implement plan decision PD-001`, sourceIds ["AC-001","FR-001","PD-001"]).
    //
    // initializeTasksReadyProject seeds a passing evidence.yml (byte-compatible authored fixture),
    // which the evidence stage correctly no-clobbers; to exercise fresh *scaffolding* we remove
    // that seed first so the merge takes the scaffold-from-obligations path.
    let private scaffoldFreshEvidence root =
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))
        TestSupport.runEvidence root workId title |> ignore
        let evidence = TestSupport.readRelative root evidencePath

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"Scaffolded evidence artifact did not parse: {diagnostics}."

    let private declarationForTask (artifact: EvidenceArtifact) taskId =
        artifact.Evidence
        |> List.tryFind (fun declaration -> declaration.Subject.Id = taskId)
        |> Option.defaultWith (fun () -> failwith $"No scaffolded obligation for task {taskId}.")

    [<Fact>]
    let ``evidence plan-decision obligation preserves planDecisionRefs and recovers requirementRefs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // T002 is the `Implement plan decision PD-001` task: its task.Requirements / task.Decisions
        // are empty, so before the fix both requirementRefs and planDecisionRefs scaffolded empty.
        let declaration = declarationForTask artifact "T002"

        Assert.Equal<string list>([ "PD-001" ], declaration.PlanDecisionRefs |> List.map _.Value)
        // The PD→FR linkage is recovered from the plan decision's own source lineage (FR-001).
        Assert.Equal<string list>([ "FR-001" ], declaration.RequirementRefs |> List.map _.Value)

    [<Fact>]
    let ``evidence requirement obligation still carries requirementRefs (no regression)`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // T001 is the `Implement requirement FR-001` task.
        let declaration = declarationForTask artifact "T001"

        Assert.Equal<string list>([ "FR-001" ], declaration.RequirementRefs |> List.map _.Value)
        Assert.Empty(declaration.PlanDecisionRefs)

    [<Fact>]
    let ``evidence obligation refs never misroute a plan decision into clarification refs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // PD-001 is a plan decision, not a clarification decision (DEC-###) — it must land only in
        // planDecisionRefs, never clarificationDecisionRefs.
        let declaration = declarationForTask artifact "T002"

        Assert.Empty(declaration.ClarificationDecisionRefs)
        Assert.Contains("PD-001", declaration.PlanDecisionRefs |> List.map _.Value)

    [<Fact>]
    let ``evidence scaffolding preserves refs deterministically across fresh runs`` () =
        let rootA = initializedAnalyzedProject ()
        let rootB = initializedAnalyzedProject ()

        let refsOf root =
            scaffoldFreshEvidence root
            |> fun artifact ->
                artifact.Evidence
                |> List.map (fun d ->
                    d.Subject.Id, d.RequirementRefs |> List.map _.Value, d.PlanDecisionRefs |> List.map _.Value)

        // Two fresh scaffolds from identical inputs route identical, sorted, de-duplicated refs.
        Assert.Equal<(string * string list * string list) list>(refsOf rootA, refsOf rootB)

    [<Fact>]
    let ``evidence does not clobber authored declarations on an existing evidence file`` () =
        // The seeded passing evidence.yml carries no ref fields; a re-run must preserve the
        // authored declarations (FR-006 no-clobber) rather than re-scaffolding populated refs over
        // them. sourceSnapshots legitimately refresh, so compare declarations, not raw bytes.
        let root = initializedAnalyzedProject ()

        let declarations () =
            match
                parseEvidenceArtifact
                    { Path = evidencePath
                      Text = TestSupport.readRelative root evidencePath }
            with
            | Ok artifact -> artifact.Evidence
            | Error diagnostics -> failwith $"Evidence did not parse: {diagnostics}."

        let planRefsForT002 () =
            declarations ()
            |> List.find (fun declaration -> declaration.Subject.Id = "T002")
            |> fun declaration -> declaration.PlanDecisionRefs

        // The authored T002 obligation has no plan-decision ref. A re-run must NOT re-scaffold
        // PD-001 over the authored declaration — scaffolding is confined to obligations with no
        // authored evidence (FR-006 no-clobber).
        Assert.Empty(planRefsForT002 ())
        TestSupport.runEvidence root workId title |> ignore
        Assert.Empty(planRefsForT002 ())

    // Feature 077 / US2: `evidence --from-tests <path>` pre-maps each newly scaffolded obligation
    // to a verification-kind source pointing at the proving test file. Additive and inert when
    // absent; the path is a declared pointer (existence/freshness is a verify-stage concern).
    let private scaffoldFreshWith root fromTests =
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        { TestSupport.evidenceRequest root workId title with
            FromTests = fromTests }
        |> TestSupport.runRequest
        |> ignore

        let evidence = TestSupport.readRelative root evidencePath

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"Scaffolded evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence --from-tests seeds a verification source on every scaffolded obligation`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root (Some "tests/FS.GG.SDD.Foo.Tests")

        Assert.NotEmpty(artifact.Evidence)

        for declaration in artifact.Evidence do
            Assert.Contains(
                declaration.SourceRefs,
                fun source -> source.Kind = "verification" && source.Path = Some "tests/FS.GG.SDD.Foo.Tests"
            )

    [<Fact>]
    let ``evidence without --from-tests seeds no source (inert)`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root None

        for declaration in artifact.Evidence do
            Assert.Empty(declaration.SourceRefs)

    [<Fact>]
    let ``evidence --from-tests with a blank value is treated as absent`` () =
        // A blank value carries no proving path; it is inert rather than seeding an empty source.
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root (Some "   ")

        for declaration in artifact.Evidence do
            Assert.Empty(declaration.SourceRefs)

    [<Fact>]
    let ``evidence CLI --from-tests threads the path onto scaffolded sources`` () =
        // Real CLI parse + handler: `evidence --from-tests <path>` reaches the scaffolded evidence.
        let root = initializedAnalyzedProject ()
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        let result = runEvidenceCli root [ "--from-tests"; "tests/FS.GG.SDD.Foo.Tests" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("tests/FS.GG.SDD.Foo.Tests", TestSupport.readRelative root evidencePath)
