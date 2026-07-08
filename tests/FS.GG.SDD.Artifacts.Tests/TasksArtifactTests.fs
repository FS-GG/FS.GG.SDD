namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module TasksArtifactTests =
    let taskText =
        """schemaVersion: 1
work:
  id: 009-tasks-command
  title: "Tasks Command"
  stage: tasks
  status: tasksReady
  sourceSpec: work/009-tasks-command/spec.md
  sourceClarifications: work/009-tasks-command/clarifications.md
  sourceChecklist: work/009-tasks-command/checklist.md
  sourcePlan: work/009-tasks-command/plan.md
  publicOrToolFacingImpact: true
sources:
  - label: spec
    path: work/009-tasks-command/spec.md
    digest: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
    schemaVersion: 1
  - label: plan
    path: work/009-tasks-command/plan.md
    digest: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
    schemaVersion: 1
tasks:
  - id: T001
    title: "Implement requirement FR-001"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: [DEC-001]
    sourceIds: [FR-001, AC-001, PD-001, VO-001]
    requiredSkills: [fsharp, speckit-implement]
    requiredEvidence: [EV001]
  - id: T002
    title: "Record verification evidence VO-001"
    status: stale
    owner: "sdd"
    dependencies: [T001]
    requirements: []
    decisions: []
    sourceIds: [VO-001]
    requiredSkills: [automated-tests]
    requiredEvidence: [EV002]
acceptedDeferrals:
  - "DEC-002"
findings:
  - id: TF-001
    severity: warning
    text: "Task source snapshots are stale."
    sourceIds: [T001]
advisoryNotes:
  - "Optional Governance pointers remain compatibility facts only."
lifecycleNotes:
  - "Next lifecycle action: fsgg-sdd analyze --work 009-tasks-command."
"""

    let snapshot text =
        ({ Path = "work/009-tasks-command/tasks.yml"
           Text = text }
        : FileSnapshot)

    [<Fact>]
    let ``Task parser extracts root metadata sources graph links and readiness counts`` () =
        match parseTaskFacts (snapshot taskText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("009-tasks-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Tasks, facts.FrontMatter.Stage)
            Assert.Equal("tasksReady", facts.FrontMatter.Status)
            Assert.Equal(2, facts.SourceSnapshots.Length)
            Assert.Equal<string list>([ "T001"; "T002" ], facts.Tasks |> List.map (fun task -> task.Id.Value))
            Assert.Equal<string list>([ "FR-001" ], facts.Tasks.Head.Requirements |> List.map (fun id -> id.Value))

            // Feature 093 / FS.GG.SDD#164: `SourceIds` is now the derived union of the authored
            // `sourceIds:` and the typed `requirements:`/`decisions:`. T001 authors `decisions: [DEC-001]`
            // but omits DEC-001 from its `sourceIds:` — so before this change, `evidence` and `verify`
            // (which read only `SourceIds`) never saw the decision this task disposes.
            Assert.Equal<string list>(
                [ "AC-001"; "DEC-001"; "FR-001"; "PD-001"; "VO-001" ],
                facts.Tasks.Head.SourceIds
            )
            Assert.Equal<string list>([ "EV001" ], facts.Tasks.Head.RequiredEvidence |> List.map (fun id -> id.Value))
            Assert.Equal(1, facts.AcceptedDeferrals.Length)
            Assert.Equal(1, facts.Findings.Length)
            Assert.Equal(1, facts.StaleTaskCount)

    [<Fact>]
    let ``Task parser remains compatible with minimal version one task files`` () =
        let minimal =
            """schemaVersion: 1
tasks:
  - id: T001
    title: "Existing implementation task"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

        match parseTaskFacts (snapshot minimal) with
        | Error diagnostics -> failwith $"Minimal v1 tasks should parse: {diagnostics}"
        | Ok facts ->
            Assert.Equal("009-tasks-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal("work/009-tasks-command/plan.md", facts.FrontMatter.SourcePlan)
            Assert.Single facts.Tasks |> ignore

    [<Fact>]
    let ``Task parser reports duplicate task ids`` () =
        let broken = taskText.Replace("id: T002", "id: T001")

        match parseTaskFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts -> Assert.Contains(facts.Diagnostics, fun diagnostic -> diagnostic.Id = "workModelInconsistent")

    [<Fact>]
    let ``Task parser diagnoses unsupported schema versions`` () =
        let broken = taskText.Replace("schemaVersion: 1", "schemaVersion: 2")

        match parseTaskFacts (snapshot broken) with
        | Ok _ -> failwith "Unsupported schema version should block parsing."
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unsupportedSchemaVersion")

    // ---------------------------------------------------------------------------------------------
    // Feature 093 / FS.GG.SDD#164 (FS.GG.Game feedback §WD3). `sourceIds:` and `decisions:` were two
    // fields for one fact, and four consumers disagreed about which was canonical:
    //
    //   analyze         reads SourceIds ∪ Requirements ∪ Decisions
    //   evidence/verify read SourceIds ONLY
    //   agent guidance  read Requirements @ Decisions ONLY
    //
    // The shipped `docs/examples/lifecycle-artifacts/tasks.yml` authors the typed fields and omits
    // `sourceIds:` entirely — so a task in the documented shape was invisible to evidence and verify.
    // The typed fields are now canonical and `SourceIds` is their derived union.
    // ---------------------------------------------------------------------------------------------

    let private oneTask (body: string) =
        $"""schemaVersion: 1
work:
  id: 009-tasks-command
  title: "Tasks Command"
  stage: tasks
  status: tasksReady
  sourceSpec: work/009-tasks-command/spec.md
  sourceClarifications: work/009-tasks-command/clarifications.md
  sourceChecklist: work/009-tasks-command/checklist.md
  sourcePlan: work/009-tasks-command/plan.md
  publicOrToolFacingImpact: true
sources: []
tasks:
{body}
acceptedDeferrals: []
findings: []
"""

    let private firstTask text : WorkTask =
        match parseTaskFacts (snapshot text) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts -> facts.Tasks.Head

    let private sourceIdsOf text = (firstTask text).SourceIds

    /// FR-016. A task authored the way the shipped example documents — typed refs, no `sourceIds:` —
    /// derives its `SourceIds` from those refs.
    [<Fact>]
    let ``a task authored with typed refs and no sourceIds derives them`` () =
        let sourceIds =
            oneTask
                """  - id: T001
    title: "Typed refs only"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: [DEC-001]
    requiredSkills: [fsharp]
    requiredEvidence: [EV001]"""
            |> sourceIdsOf

        Assert.Equal<string list>([ "DEC-001"; "FR-001" ], sourceIds)

    /// FR-017. An explicit `sourceIds:` entry the typed fields cannot express — a scope boundary — is
    /// retained in the union, never discarded. The derivation is a strict widening.
    [<Fact>]
    let ``an explicit sourceIds entry is retained in the union`` () =
        let sourceIds =
            oneTask
                """  - id: T001
    title: "Explicit plus typed"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: [DEC-001]
    sourceIds: [SB-002]
    requiredSkills: [fsharp]
    requiredEvidence: [EV001]"""
            |> sourceIdsOf

        Assert.Equal<string list>([ "DEC-001"; "FR-001"; "SB-002" ], sourceIds)

    /// A malformed token in a typed field is not a reference — `parseRequirementIds` drops it (and
    /// `malformedRefs` has already diagnosed it), so it must not leak into the derived union either.
    [<Fact>]
    let ``a malformed typed ref does not enter the derived sourceIds`` () =
        let sourceIds =
            oneTask
                """  - id: T001
    title: "Malformed ref"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-1]
    decisions: []
    sourceIds: [SB-002]
    requiredSkills: [fsharp]
    requiredEvidence: [EV001]"""
            |> sourceIdsOf

        Assert.Equal<string list>([ "SB-002" ], sourceIds)

    /// FR-022. `allTaskDispositionIds` already computed `SourceIds ∪ Requirements ∪ Decisions`, so the
    /// derived `SourceIds` *is* that union and its output set is identical by construction. This pins it.
    [<Fact>]
    let ``the derived union equals the disposition set the analyzer already computed`` () =
        let text =
            oneTask
                """  - id: T001
    title: "Everything"
    status: pending
    owner: "sdd"
    dependencies: []
    requirements: [FR-001]
    decisions: [DEC-001]
    sourceIds: [SB-002]
    requiredSkills: [fsharp]
    requiredEvidence: [EV001]"""

        let task = firstTask text

        let legacyUnion =
            (task.SourceIds
             @ (task.Requirements |> List.map _.Value)
             @ (task.Decisions |> List.map _.Value))
            |> List.map _.ToUpperInvariant()
            |> Set.ofList

        Assert.Equal<Set<string>>(legacyUnion, task.SourceIds |> Set.ofList)
