namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.GenerationSourceContract
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module WorkModelBundleContractTests =
    module Bundle = FS.GG.SDD.Commands.WorkModelSourceBundle

    let private performancePlan performanceRead =
        let workId = "preoutput-performance"
        let path = "tests/performance.txt"
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeEvidencedProject root workId "Performance"
        let source path = TestSupport.readRelative root path
        let evidence =
            (source $"work/{workId}/evidence.yml").Replace(
                "    result: pass\n    synthetic: false",
                $"""    performanceBudget:
      artifactPath: {path}
      targetFps: 60
      workloadIds: [normal-play]
      stressWorkloadIds: [pointer-stress]
      workloadDefinitionDigests: [normal-play=sha256:normal-v1, pointer-stress=sha256:stress-v1]
      currencyToken: commit:fixture
      capturedAfterUtc: 2026-08-22T00:00:00Z
      maxP95Ms: 16.67
      maxP99Ms: 25
      maxCatchUpFrames: 0
      measurementScope: normal
      requiredCapability: bounded-headless-update-render
      liveCompositorRequired: false
    result: pass
    synthetic: false""")
        Assert.Contains("performanceBudget:", evidence)
        let read path : CommandEffectResult =
            let snapshot = { Path = path; Text = source path; RawBytes = None }
            { Effect = ReadFile path; Succeeded = true; Read = Bytes snapshot
              Snapshot = Some snapshot; Process = None; Confirmed = None; Diagnostic = None }
        let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
        let model, _ = FS.GG.SDD.Commands.CommandWorkflow.init (TestSupport.request Analyze ".")
        let request = TestSupport.request Analyze "."
        let performanceResult: CommandEffectResult =
            let snapshot =
                match performanceRead with
                | Bytes snapshot -> Some snapshot
                | _ -> None
            { Effect = ReadFile path; Succeeded = Option.isSome snapshot
              Read = performanceRead; Snapshot = snapshot
              Process = None; Confirmed = None; Diagnostic = None }
        let diagnostics, view, effects, _ =
            ViewGeneration.generatedViewPlan request workId None
                (Some(source $"work/{workId}/spec.md"))
                (Some(source $"work/{workId}/clarifications.md"))
                (Some(source $"work/{workId}/checklist.md"))
                (Some(source $"work/{workId}/plan.md"))
                (Some(source $"work/{workId}/tasks.yml"))
                (Some evidence) []
                { model with InterpretedEffects = (config |> List.map read) @ [ performanceResult ] }
        diagnostics, view, effects

    [<Fact>]
    let ``absent declared performance refuses before output planning`` () =
        let diagnostics, view, effects = performancePlan Absent
        Assert.Empty effects
        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "missingPerformanceSource")
        Assert.Contains("missingPerformanceSource", view.DiagnosticIds)

    [<Fact>]
    let ``unreadable declared performance refuses before output planning`` () =
        let diagnostics, view, effects =
            performancePlan (Unreadable("tests/performance.txt", "controlled failure"))
        Assert.Empty effects
        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unreadablePerformanceSource")
        Assert.Contains("unreadablePerformanceSource", view.DiagnosticIds)

    [<Fact>]
    let ``observed declared performance does not trigger source-read refusal`` () =
        let path = "tests/performance.txt"
        let snapshot = { Path = path; Text = "measured\n"; RawBytes = None }
        let diagnostics, view, _ = performancePlan (Bytes snapshot)
        Assert.DoesNotContain(diagnostics, fun diagnostic ->
            diagnostic.Id = "missingPerformanceSource" || diagnostic.Id = "unreadablePerformanceSource")
        Assert.DoesNotContain("missingPerformanceSource", view.DiagnosticIds)
        Assert.DoesNotContain("unreadablePerformanceSource", view.DiagnosticIds)

    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-work-model-bundle-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "work", "sample")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``red-before single-root contract accepts config while work source differs`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
                for path in config do File.WriteAllBytes(Path.Combine(root, path), [| 1uy |])
                File.WriteAllBytes(Path.Combine(root, "work/sample/spec.md"), Encoding.UTF8.GetBytes "changed")
                let selected: Selection = { ClosedRoot = ".fsgg"; Policy = ExactBytes }
                let candidate: Contract =
                    { Version = 1
                      ClosedRoot = ".fsgg"
                      Policy = ExactBytes
                      Sources = config |> List.map (fun path -> { Path = path; Digest = SchemaVersion.sha256Bytes [| 1uy |] }) }
                match verify root selected candidate with
                | Error refusal -> failwithf "control unexpectedly refused: %A" refusal
                | Ok files -> Assert.Equal(3, files.Length))

    let private fixtureWithPerformance performance action =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let performanceDirectory =
                    Path.GetDirectoryName(Path.Combine(root, performance))
                    |> Option.ofObj
                    |> Option.defaultWith (fun () -> failwith "fixture path has no parent")
                Directory.CreateDirectory(performanceDirectory) |> ignore
                let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
                let work = [ "work/sample/spec.md"; "work/sample/evidence.yml" ]
                let evidence =
                    $"""schemaVersion: 1
workId: sample
stage: evidence
status: evidenceReady
sourceSnapshots:
  - path: stale
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
    artifacts: [{performance}]
    sourceRefs:
      - kind: test-output
        path: {performance}
        result: pass
    performanceBudget:
      artifactPath: {performance}
      targetFps: 60
      workloadIds: [normal-play]
      stressWorkloadIds: [pointer-stress]
      workloadDefinitionDigests: [normal-play=sha256:normal-v1, pointer-stress=sha256:stress-v1]
      currencyToken: commit:fixture
      capturedAfterUtc: 2026-08-22T00:00:00Z
      maxP95Ms: 16.67
      maxP99Ms: 25
      maxCatchUpFrames: 0
      measurementScope: normal
      requiredCapability: bounded-headless-update-render
      liveCompositorRequired: false
    result: pass
    synthetic: false
    notes: []
"""
                let bodies =
                    [ yield! config |> List.map (fun path -> path, "schemaVersion: 1\n")
                      "work/sample/spec.md", "# source\r\n"
                      "work/sample/evidence.yml", evidence
                      performance, "measured\n" ]
                for path, body in bodies do File.WriteAllText(Path.Combine(root, path), body)
                let selected: FileSnapshot list =
                    bodies
                    |> List.map (fun (path, body) ->
                        { Path = path
                          Text = if path = "work/sample/evidence.yml" then
                                     ViewGeneration.evidenceTextForWorkModel body
                                 else body
                          RawBytes = Some(Encoding.UTF8.GetBytes body) })
                let captureRoot closedRoot paths =
                    match capture root closedRoot paths ExactBytes with
                    | Ok files -> files
                    | Error refusal -> failwithf "fixture capture refused: %A" refusal
                let physical =
                    captureRoot ".fsgg" config
                    @ captureRoot "work/sample" work
                    @ captureRoot (if performance.StartsWith("readiness/") then "readiness/sample" else "tests") [ performance ]
                let candidate: Bundle.Candidate =
                    { Version = 2
                      WorkId = "sample"
                      Sources =
                        selected
                        |> List.map (fun source ->
                            { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
                action root selected physical candidate)

    let private fixture action =
        fixtureWithPerformance "readiness/sample/performance-evidence.json" action

    [<Fact>]
    let ``red-before supplied performance capture remains green after physical byte change`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
            File.WriteAllText(Path.Combine(root, "tests/performance.txt"), "changed\n")
            match Bundle.verify "sample" selected physical candidate with
            | Ok _ -> ()
            | Error refusal -> failwithf "pure supplied-capture control unexpectedly refused: %A" refusal)

    let private coreWithoutPerformance (physical: CapturedFile list) =
        physical |> List.filter (fun source -> source.Path <> "tests/performance.txt")

    [<Fact>]
    let ``pinned performance join accepts selected file with unrelated parent siblings`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                File.WriteAllText(Path.Combine(root, "tests/unrelated.txt"), "other")
                File.CreateSymbolicLink(Path.Combine(root, "tests/shortcut.txt"),
                                        Path.Combine(root, "tests/performance.txt")) |> ignore
                match Bundle.verifyWithPinnedPerformance root "sample" selected
                          (coreWithoutPerformance physical) candidate with
                | Ok files -> Assert.Equal(6, files.Length)
                | Error reason -> failwithf "selected-file join refused valid source: %A" reason)

    [<Fact>]
    let ``pinned performance join refuses physical byte change after supplied capture`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                File.WriteAllText(Path.Combine(root, "tests/performance.txt"), "changed\n")
                Assert.Equal(Error(Bundle.RawDrift "tests/performance.txt"),
                             Bundle.verifyWithPinnedPerformance root "sample" selected
                                 (coreWithoutPerformance physical) candidate))

    [<Fact>]
    let ``pinned performance join refuses linked selected file and case alias`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                let performance = Path.Combine(root, "tests/performance.txt")
                File.Delete performance
                File.CreateSymbolicLink(performance, Path.Combine(root, "work/sample/spec.md")) |> ignore
                Assert.Equal(Error(Bundle.Physical(Symlink "tests/performance.txt")),
                             Bundle.verifyWithPinnedPerformance root "sample" selected
                                 (coreWithoutPerformance physical) candidate))
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                File.WriteAllText(Path.Combine(root, "tests/Performance.txt"), "alias")
                Assert.Equal(Error(Bundle.Physical(DuplicatePath "tests/performance.txt")),
                             Bundle.verifyWithPinnedPerformance root "sample" selected
                                 (coreWithoutPerformance physical) candidate))

    [<Fact>]
    let ``pinned performance join refuses omitted selected declaration`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                let selected = selected |> List.filter (fun source -> source.Path <> "tests/performance.txt")
                Assert.Equal(Error(Bundle.MissingPerformanceSelection "tests/performance.txt"),
                             Bundle.verifyWithPinnedPerformance root "sample" selected
                                 (coreWithoutPerformance physical) candidate))

    [<Fact>]
    let ``red-before stale supplied core capture stays green after source changes`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                File.WriteAllText(Path.Combine(root, "work/sample/spec.md"), "changed core\n")
                match Bundle.verifyWithPinnedPerformance root "sample" selected
                          (coreWithoutPerformance physical) candidate with
                | Ok _ -> ()
                | Error reason -> failwithf "stale-core characterization unexpectedly refused: %A" reason)

    [<Fact>]
    let ``red-before omitted physical work source stays green with supplied core captures`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
                File.WriteAllText(Path.Combine(root, "work/sample/tasks.yml"), "schemaVersion: 1\ntasks: []\n")
                match Bundle.verifyWithPinnedPerformance root "sample" selected
                          (coreWithoutPerformance physical) candidate with
                | Ok _ -> ()
                | Error reason -> failwithf "omitted-core characterization unexpectedly refused: %A" reason)

    [<Fact>]
    let ``pinned core discovery accepts selected sources with unrelated siblings`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.WriteAllText(Path.Combine(root, ".fsgg/constitution.md"), "unselected")
                File.WriteAllText(Path.Combine(root, "work/sample/charter.md"), "unselected")
                match Bundle.verifyFromPinnedCoreSources root "sample" selected candidate with
                | Ok files -> Assert.Equal(6, files.Length)
                | Error reason -> failwithf "recognized core source set refused: %A" reason)

    [<Fact>]
    let ``pinned core discovery refuses changed previously supplied source`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.WriteAllText(Path.Combine(root, "work/sample/spec.md"), "changed core\n")
                Assert.Equal(Error(Bundle.RawDrift "work/sample/spec.md"),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))

    [<Fact>]
    let ``pinned core discovery binds required config independently of candidate rows`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "changed config\n")
                Assert.Equal(Error(Bundle.RawDrift ".fsgg/project.yml"),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                let omitted = ".fsgg/agents.yml"
                let selected = selected |> List.filter (fun source -> source.Path <> omitted)
                let candidate = { candidate with
                                    Sources = candidate.Sources |> List.filter (fun source -> source.Path <> omitted) }
                Assert.Equal(Error(Bundle.MissingRequired omitted),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))

    [<Fact>]
    let ``pinned core discovery refuses omitted present optional work source`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.WriteAllText(Path.Combine(root, "work/sample/tasks.yml"), "schemaVersion: 1\ntasks: []\n")
                Assert.Equal(Error(Bundle.UnexpectedPhysical "work/sample/tasks.yml"),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))

    [<Fact>]
    let ``pinned core discovery refuses optional linked source and case alias`` () =
        if OperatingSystem.IsLinux() then
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.CreateSymbolicLink(Path.Combine(root, "work/sample/tasks.yml"),
                                        Path.Combine(root, "work/sample/spec.md")) |> ignore
                Assert.Equal(Error(Bundle.Physical(Symlink "work/sample/tasks.yml")),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))
            fixtureWithPerformance "tests/performance.txt" (fun root selected _ candidate ->
                File.WriteAllText(Path.Combine(root, "work/sample/Tasks.yml"), "alias")
                Assert.Equal(Error(Bundle.Physical(DuplicatePath "work/sample/tasks.yml")),
                             Bundle.verifyFromPinnedCoreSources root "sample" selected candidate))

    [<Fact>]
    let ``declared performance outside readiness binds as a selected source`` () =
        fixtureWithPerformance "tests/performance.txt" (fun _ selected physical candidate ->
            match Bundle.verify "sample" selected physical candidate with
            | Error refusal -> failwithf "declared external-root source refused: %A" refusal
            | Ok files -> Assert.Equal(6, files.Length))

    [<Fact>]
    let ``omitted declared performance refuses even if candidate and captures omit it`` () =
        fixture (fun _ selected physical candidate ->
            let performance = "readiness/sample/performance-evidence.json"
            let selected = selected |> List.filter (fun source -> source.Path <> performance)
            let physical = physical |> List.filter (fun source -> source.Path <> performance)
            let candidate = { candidate with Sources = candidate.Sources |> List.filter (fun source -> source.Path <> performance) }
            Assert.Equal(Error(Bundle.MissingPerformanceSelection performance),
                         Bundle.verify "sample" selected physical candidate))

    [<Fact>]
    let ``producer selects observed performance path outside readiness`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ _ _ ->
            let path = "tests/performance.txt"
            let body = File.ReadAllText(Path.Combine(root, "work/sample/evidence.yml"))
            let snapshot = { Path = path; Text = "measured\n"; RawBytes = None }
            let observed: CommandEffectResult =
                { Effect = ReadFile path
                  Succeeded = true
                  Read = Bytes snapshot
                  Snapshot = Some snapshot
                  Process = None
                  Confirmed = None
                  Diagnostic = None }
            let model, _ = FS.GG.SDD.Commands.CommandWorkflow.init (TestSupport.request Analyze ".")
            let selected =
                ViewGeneration.performanceEvidenceSnapshots "sample" (Some body)
                    { model with InterpretedEffects = [ observed ] }
            Assert.Equal<string list>([ path ], selected |> List.map _.Path))

    let private selectedByProducer root performanceRead =
        let read path text : CommandEffectResult =
            let snapshot = { Path = path; Text = text; RawBytes = None }
            { Effect = ReadFile path
              Succeeded = true
              Read = Bytes snapshot
              Snapshot = Some snapshot
              Process = None
              Confirmed = None
              Diagnostic = None }
        let performancePath = "tests/performance.txt"
        let performanceSnapshot =
            match performanceRead with
            | Bytes snapshot -> Some snapshot
            | _ -> None
        let result: CommandEffectResult =
            { Effect = ReadFile performancePath
              Succeeded = Option.isSome performanceSnapshot || performanceRead = Absent
              Read = performanceRead
              Snapshot = performanceSnapshot
              Process = None
              Confirmed = None
              Diagnostic = None }
        let config =
            [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
            |> List.map (fun path -> read path (File.ReadAllText(Path.Combine(root, path))))
        let model, _ = FS.GG.SDD.Commands.CommandWorkflow.init (TestSupport.request Analyze ".")
        ViewGeneration.workModelSnapshots "sample" None
            (Some(File.ReadAllText(Path.Combine(root, "work/sample/spec.md"))))
            None None None None
            (Some(File.ReadAllText(Path.Combine(root, "work/sample/evidence.yml"))))
            { model with InterpretedEffects = config @ [ result ] }

    [<Fact>]
    let ``observed declared performance is selected and bundle accepted`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ physical _ ->
            let path = "tests/performance.txt"
            let observed = { Path = path; Text = File.ReadAllText(Path.Combine(root, path)); RawBytes = None }
            let selected = selectedByProducer root (Bytes observed)
            Assert.Contains(path, selected |> List.map _.Path)
            let candidate: Bundle.Candidate =
                { Version = 2
                  WorkId = "sample"
                  Sources = selected |> List.map (fun source ->
                      { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
            match Bundle.verify "sample" selected physical candidate with
            | Error refusal -> failwithf "observed complete selection refused: %A" refusal
            | Ok files -> Assert.Equal(6, files.Length))

    [<Fact>]
    let ``absent declared performance is omitted by producer and refused by bundle`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ physical _ ->
            let selected = selectedByProducer root Absent
            let path = "tests/performance.txt"
            Assert.DoesNotContain(path, selected |> List.map _.Path)
            let candidate: Bundle.Candidate =
                { Version = 2
                  WorkId = "sample"
                  Sources = selected |> List.map (fun source ->
                      { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
            let captured = physical |> List.filter (fun source -> source.Path <> path)
            Assert.Equal(Error(Bundle.MissingPerformanceSelection path),
                         Bundle.verify "sample" selected captured candidate))

    [<Fact>]
    let ``unreadable declared performance is omitted by producer and refused by bundle`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ physical _ ->
            let path = "tests/performance.txt"
            let selected = selectedByProducer root (Unreadable(path, "controlled failure"))
            Assert.DoesNotContain(path, selected |> List.map _.Path)
            let candidate: Bundle.Candidate =
                { Version = 2
                  WorkId = "sample"
                  Sources = selected |> List.map (fun source ->
                      { Path = source.Path; Digest = SchemaVersion.sha256Text source.Text }) }
            let captured = physical |> List.filter (fun source -> source.Path <> path)
            Assert.Equal(Error(Bundle.MissingPerformanceSelection path),
                         Bundle.verify "sample" selected captured candidate))

    [<Fact>]
    let ``unbound performance selection and malformed physical evidence refuse`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
            let unbound =
                selected |> List.map (fun source ->
                    if source.Path = "tests/performance.txt" then { source with Path = "tests/other.txt" }
                    else source)
            Assert.Equal(Error(Bundle.InvalidSelectionPath "tests/other.txt"),
                         Bundle.verify "sample" unbound physical candidate)
            File.WriteAllText(Path.Combine(root, "work/sample/evidence.yml"), "not: [valid")
            let alteredEvidence =
                match capture root "work/sample" [ "work/sample/spec.md"; "work/sample/evidence.yml" ] ExactBytes with
                | Ok files -> files |> List.find (fun file -> file.Path = "work/sample/evidence.yml")
                | Error refusal -> failwithf "malformed-evidence bytes could not be captured: %A" refusal
            let alteredPhysical =
                physical |> List.map (fun file ->
                    if file.Path = "work/sample/evidence.yml" then alteredEvidence else file)
            Assert.Equal(Error Bundle.MalformedEvidence,
                         Bundle.verify "sample" selected alteredPhysical candidate))

    [<Fact>]
    let ``arbitrary performance parent with unrelated sibling cannot be a closed root`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
            let sibling = "tests/unrelated.txt"
            File.WriteAllText(Path.Combine(root, sibling), "not a work-model source")
            Assert.Equal(Error(UnexpectedFile sibling),
                         capture root "tests" [ "tests/performance.txt" ] ExactBytes)
            let fullParent =
                match capture root "tests" [ "tests/performance.txt"; sibling ] ExactBytes with
                | Ok files -> files
                | Error refusal -> failwithf "complete parent control refused: %A" refusal
            let otherRoots = physical |> List.filter (fun file -> file.Path <> "tests/performance.txt")
            Assert.Equal(Error(Bundle.UnexpectedPhysical sibling),
                         Bundle.verify "sample" selected (otherRoots @ fullParent) candidate))

    [<Fact>]
    let ``unselected symlink in arbitrary performance parent refuses root capture`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ _ _ ->
            let link = Path.Combine(root, "tests", "shortcut.txt")
            File.CreateSymbolicLink(link, Path.Combine(root, "tests", "performance.txt")) |> ignore
            Assert.Equal(Error(Symlink "tests/shortcut.txt"),
                         capture root "tests" [ "tests/performance.txt" ] ExactBytes))

    [<Fact>]
    let ``case alias in arbitrary performance parent refuses root capture`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root _ _ _ ->
            let alias = "tests/Performance.txt"
            File.WriteAllText(Path.Combine(root, alias), "alias")
            Assert.Equal(Error(DuplicatePath "tests/performance.txt"),
                         capture root "tests" [ "tests/performance.txt" ] ExactBytes))

    [<Fact>]
    let ``selected-file capture binds crowded performance parent to complete bundle`` () =
        fixtureWithPerformance "tests/performance.txt" (fun root selected physical candidate ->
            File.WriteAllText(Path.Combine(root, "tests", "unrelated.txt"), "other")
            File.CreateSymbolicLink(Path.Combine(root, "tests", "shortcut.txt"),
                                    Path.Combine(root, "tests", "performance.txt")) |> ignore
            let performance =
                match captureSelectedFile root "tests/performance.txt" with
                | Ok file -> file
                | Error refusal -> failwithf "selected-file capture refused: %A" refusal
            let otherRoots = physical |> List.filter (fun file -> file.Path <> "tests/performance.txt")
            match Bundle.verify "sample" selected (otherRoots @ [ performance ]) candidate with
            | Error refusal -> failwithf "complete bundle refused: %A" refusal
            | Ok files -> Assert.Equal(6, files.Length))

    [<Fact>]
    let ``three selected roots bind captured bytes and projected evidence digest`` () =
        fixture (fun _ selected physical candidate ->
            let evidence = selected |> List.find (fun source -> source.Path = "work/sample/evidence.yml")
            Assert.Contains("sourceSnapshots: []\nevidence:\n  - id: EV001", evidence.Text)
            match Bundle.verify "sample" selected physical candidate with
            | Error refusal -> failwithf "valid bundle refused: %A" refusal
            | Ok files -> Assert.Equal(6, files.Length))

    [<Fact>]
    let ``missing and extra physical sources refuse`` () =
        fixture (fun root selected physical candidate ->
            let absent = physical |> List.filter (fun file -> file.Path <> "work/sample/spec.md")
            Assert.Equal(Error(Bundle.MissingPhysical "work/sample/spec.md"),
                         Bundle.verify "sample" selected absent candidate)
            let extraPath = "work/sample/plan.md"
            File.WriteAllText(Path.Combine(root, extraPath), "plan")
            let extra =
                match capture root "work/sample" [ "work/sample/spec.md"; "work/sample/evidence.yml"; extraPath ] ExactBytes with
                | Ok files -> files |> List.find (fun file -> file.Path = extraPath)
                | Error refusal -> failwithf "extra fixture refused: %A" refusal
            Assert.Equal(Error(Bundle.UnexpectedPhysical extraPath),
                         Bundle.verify "sample" selected (physical @ [ extra ]) candidate))

    [<Fact>]
    let ``missing candidate and case alias refuse`` () =
        fixture (fun _ selected physical candidate ->
            let absent = { candidate with Sources = candidate.Sources |> List.filter (fun row -> row.Path <> "work/sample/spec.md") }
            Assert.Equal(Error(Bundle.MissingCandidate "work/sample/spec.md"),
                         Bundle.verify "sample" selected physical absent)
            let spec = candidate.Sources |> List.find (fun row -> row.Path = "work/sample/spec.md")
            let alias = { spec with Path = "work/sample/Spec.md" }
            Assert.Equal(Error(Bundle.DuplicateCandidate "work/sample/Spec.md"),
                         Bundle.verify "sample" selected physical { candidate with Sources = candidate.Sources @ [ alias ] }))

    [<Fact>]
    let ``wrong selected text raw bytes and candidate digest refuse`` () =
        fixture (fun _ selected physical candidate ->
            let change path edit =
                selected |> List.map (fun source -> if source.Path = path then edit source else source)
            let spec = "work/sample/spec.md"
            let changedText = change spec (fun source -> { source with Text = "forged" })
            Assert.Equal(Error(Bundle.TextDrift spec), Bundle.verify "sample" changedText physical candidate)
            let changedRaw = change spec (fun source -> { source with RawBytes = Some [| 0uy |] })
            Assert.Equal(Error(Bundle.RawDrift spec), Bundle.verify "sample" changedRaw physical candidate)
            let wrongDigest =
                let rows =
                    candidate.Sources |> List.map (fun row ->
                        if row.Path = spec then { row with Digest = SchemaVersion.sha256Text "wrong" } else row)
                { candidate with Sources = rows }
            Assert.Equal(Error(Bundle.DigestDrift spec), Bundle.verify "sample" selected physical wrongDigest))

    [<Fact>]
    let ``unprojected evidence and wrong work identity refuse`` () =
        fixture (fun _ selected physical candidate ->
            let evidence = "work/sample/evidence.yml"
            let unprojected =
                selected |> List.map (fun source ->
                    if source.Path = evidence then
                        { source with Text = Encoding.UTF8.GetString(source.RawBytes.Value) }
                    else source)
            Assert.Equal(Error(Bundle.TextDrift evidence), Bundle.verify "sample" unprojected physical candidate)
            Assert.Equal(Error Bundle.WrongWorkId,
                         Bundle.verify "sample" selected physical { candidate with WorkId = "other" })
            Assert.Equal(Error(Bundle.UnsupportedVersion 1),
                         Bundle.verify "sample" selected physical { candidate with Version = 1 }))

    [<Fact>]
    let ``unselected charter and unsafe candidate path refuse`` () =
        fixture (fun _ selected physical candidate ->
            let charter = { selected.Head with Path = "work/sample/charter.md" }
            Assert.Equal(Error(Bundle.InvalidSelectionPath "work/sample/charter.md"),
                         Bundle.verify "sample" (charter :: selected) physical candidate)
            let forged = { candidate.Sources.Head with Path = "work/sample/../escape" }
            Assert.Equal(Error(Bundle.InvalidCandidatePath "work/sample/../escape"),
                         Bundle.verify "sample" selected physical { candidate with Sources = forged :: candidate.Sources }))

    [<Fact>]
    let ``readiness selection without evidence source refuses`` () =
        fixture (fun _ selected physical candidate ->
            let withoutEvidence = selected |> List.filter (fun source -> source.Path <> "work/sample/evidence.yml")
            Assert.Equal(Error(Bundle.MissingRequired "work/sample/evidence.yml"),
                         Bundle.verify "sample" withoutEvidence physical candidate))

    [<Fact>]
    let ``malformed digest and selected case alias refuse`` () =
        fixture (fun _ selected physical candidate ->
            let spec = "work/sample/spec.md"
            let badDigest =
                candidate.Sources
                |> List.map (fun row ->
                    if row.Path = spec then { row with Digest = { Algorithm = "sha256"; Value = "ABC" } }
                    else row)
            Assert.Equal(Error(Bundle.MalformedDigest spec),
                         Bundle.verify "sample" selected physical { candidate with Sources = badDigest })
            let original = selected |> List.find (fun source -> source.Path = spec)
            let alias = { original with Path = "work/sample/Spec.md" }
            Assert.Equal(Error(Bundle.InvalidSelectionPath "work/sample/Spec.md"),
                         Bundle.verify "sample" (selected @ [ alias ]) physical candidate))
