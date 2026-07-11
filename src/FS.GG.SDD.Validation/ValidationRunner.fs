namespace FS.GG.SDD.Validation

open System
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading
open FS.GG.SDD.Artifacts.ReleaseContract
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness

module ValidationRunner =
    // Aliased (not opened) so the command-workflow `init`/`update` do not collide
    // with the validation-harness `init`/`update`.
    module CW = FS.GG.SDD.Commands.CommandWorkflow
    module CR = FS.GG.SDD.Commands.CommandRendering
    module RC = FS.GG.SDD.Artifacts.ReleaseContract

    type RunnerOptions =
        { OnlyMatrix: string option
          Plan: MatrixPlan option
          InjectedDivergences: (string * (string * string) list) list }

    let defaultOptions =
        { OnlyMatrix = None
          Plan = None
          InjectedDivergences = [] }

    // ---- canned fixture content (mirrors the real-fixture drivers in TestSupport;
    // the library cannot reference the test assembly, so the inputs are replicated) ----

    let fixtureWorkId = "020-validation-fixture"
    let fixtureTitle = "Validation Fixture"
    let generator = currentGeneratorVersion ()

    let specifyIntent =
        "value: create a native specify command\nscope: one chartered work item\nrequirement: create a specification artifact with stable ids"

    let passingTaskEvidence =
        "schemaVersion: 1\n"
        + "evidence:\n"
        + ([ 1..6 ]
           |> List.map (fun n ->
               $"  - id: EV00{n}\n    kind: verification\n    subject:\n      type: task\n      id: T00{n}\n    result: pass\n")
           |> String.concat "")

    // ---- command driving (mirrors Program.fs run loop) ----

    let baseRequest command root =
        { Command = command
          ProjectRoot = root
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = false
          GeneratorVersion = generator
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

    let runRequest (request: CommandRequest) = driveToReport request

    let workRequest command root inputText =
        { baseRequest command root with
            WorkId = Some fixtureWorkId
            Title = Some fixtureTitle
            InputText = inputText }

    let runWork command root inputText =
        runRequest (workRequest command root inputText)

    // A fresh temp directory nested under the run root (feature 067 / FR-007). The run root is
    // created once per `run` and deleted in a `finally`, so the ~350 per-run project copies no
    // longer leak. `parent` is that run root.
    let tempDirectory (parent: string) =
        let path = Path.Combine(parent, Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory path |> ignore
        path

    let writeRelative (root: string) (relative: string) (text: string) =
        let absolute =
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))

        match Path.GetDirectoryName absolute with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.WriteAllText(absolute, text)

    let rec private copyDirectory (source: string) (destination: string) =
        Directory.CreateDirectory destination |> ignore

        for file in Directory.GetFiles source do
            File.Copy(
                file,
                Path.Combine(destination, Path.GetFileName file |> Option.ofObj |> Option.defaultValue ""),
                true
            )

        for directory in Directory.GetDirectories source do
            copyDirectory
                directory
                (Path.Combine(destination, Path.GetFileName directory |> Option.ofObj |> Option.defaultValue ""))

    // ---- state ladder ----

    let stateRank state =
        match state with
        | "fresh" -> 0
        | "specified" -> 20
        | "planReady" -> 50
        | "tasksReady" -> 60
        | "blocked" -> 60
        | "analyzed" -> 70
        | "evidenced" -> 80
        | "verified" -> 90
        | "shipped" -> 100
        | _ -> 0

    let commandMinRank command =
        match command with
        | Init -> 0
        | Charter -> 0
        | Specify -> 10
        | Clarify -> 20
        | Checklist -> 30
        | Plan -> 40
        | Tasks -> 50
        | Agents -> 50
        | Refresh -> 50
        | Analyze -> 60
        | Evidence -> 60
        | Verify -> 70
        | Ship -> 80
        | Scaffold -> 0
        | Doctor -> 0
        | Upgrade -> 0
        | Lint -> 0
        | Surface -> 0
        | Help -> 0

    /// FS.GG.SDD#351. Author the fixture's plan the way a human would: keep the id, the refs, and the
    /// kind token — the machine contract the later stages resolve against — and replace only the
    /// prose after it, which is the part that requires judgement and which `analyze` now demands.
    let authorPlanProse root =
        let relative = $"work/{fixtureWorkId}/plan.md"

        let absolute =
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))

        if File.Exists absolute then
            let prose =
                "Authored by the validation fixture: a real decision would say why, not restate the id."

            let authored =
                File.ReadAllText absolute
                |> fun text ->
                    Regex.Replace(
                        text,
                        @"(?m)^(- (?:PD|PC|VO|PM|GV)-\d+\b[^:\r\n]*: )(.+)$",
                        (fun m -> m.Groups[1].Value + prose)
                    )
                |> fun text ->
                    Regex.Replace(
                        text,
                        @"(?m)^(- [A-Z]{2,4}-\d+ acceptedDeferral: )(.+)$",
                        (fun m -> m.Groups[1].Value + prose)
                    )

            File.WriteAllText(absolute, authored)

    /// Build a disposable project at the requested state by driving the real
    /// CommandWorkflow over a temp dir (matrix-runner C-1). `withEvidence = false`
    /// yields the `blocked` ladder (tasks present, passing evidence withheld).
    let buildProjectAt root rank withEvidence =
        runRequest (baseRequest Init root) |> ignore

        if rank >= 20 then
            runWork Charter root None |> ignore
            runWork Specify root (Some specifyIntent) |> ignore

        if rank >= 50 then
            runWork Clarify root None |> ignore
            runWork Checklist root None |> ignore
            runWork Plan root None |> ignore
            // FS.GG.SDD#351: `analyze` now BLOCKS while the plan still holds the prose the scaffold
            // wrote, so this fixture — which drives the real lifecycle — must author it, exactly as
            // an author would. Without this the harness does not fail; it *skips* the cells that
            // need an analyzed-or-later project (6 of them), which is coverage silently going
            // missing rather than going red. Keep the id, the refs, and the kind token (the machine
            // contract); replace only the prose, which is the part that needs judgement.
            authorPlanProse root

        if rank >= 60 then
            runWork Tasks root None |> ignore

            if withEvidence then
                writeRelative root $"work/{fixtureWorkId}/evidence.yml" passingTaskEvidence

        if rank >= 70 then
            runWork Analyze root None |> ignore

        if rank >= 80 then
            runWork Evidence root None |> ignore

        if rank >= 90 then
            runWork Verify root None |> ignore

        if rank >= 100 then
            runWork Ship root None |> ignore

    let buildState runRoot state =
        let root = tempDirectory runRoot
        let withEvidence = state <> "blocked"
        buildProjectAt root (stateRank state) withEvidence
        root

    /// A fully-generated project: ship plus the cross-cutting agents/refresh
    /// generators, so every catalogued generated view is produced.
    let buildFullProject runRoot =
        let root = tempDirectory runRoot
        buildProjectAt root 100 true
        runWork Agents root None |> ignore
        runWork Refresh root None |> ignore
        root

    // ---- projections ----

    let renderProjection projection (report: CommandReport) =
        match projection with
        | Json -> serializeReport report
        // Rich degrades to plain text in this library (no Spectre dependency;
        // research Decision 6) — identical to the CLI's non-interactive degradation.
        | Text
        | Rich -> CR.renderText report

    let hasAnsi (text: string) = text.Contains('')

    // ---- coordinate helpers ----

    let coord name coordinates =
        coordinates
        |> List.tryFind (fun (dimension, _) -> dimension = name)
        |> Option.map snd
        |> Option.defaultValue ""

    let projectionOfValue value =
        match value with
        | "text" -> Text
        | "rich" -> Rich
        | _ -> Json

    // ---- produced-output reproduction (determinism matrix) ----

    let readinessRoot root = Path.Combine(root, "readiness")

    let findProducedFile root (output: string) =
        let readiness = readinessRoot root

        if not (Directory.Exists readiness) then
            None
        else
            let files = Directory.GetFiles(readiness, "*", SearchOption.AllDirectories)
            let normalize (p: string) = p.Replace('\\', '/')

            let matches (suffix: string) =
                files |> Array.tryFind (fun f -> (normalize f).EndsWith(suffix))

            match output with
            | "agent-commands/<target>/guidance.json" ->
                files
                |> Array.tryFind (fun f ->
                    (normalize f).Contains("/agent-commands/")
                    && (normalize f).EndsWith("/guidance.json"))
            | "agent-commands/<target>/commands.md" ->
                files
                |> Array.tryFind (fun f ->
                    (normalize f).Contains("/agent-commands/")
                    && (normalize f).EndsWith("/commands.md"))
            | "agent-commands/<target>/skills.md" ->
                files
                |> Array.tryFind (fun f ->
                    (normalize f).Contains("/agent-commands/")
                    && (normalize f).EndsWith("/skills.md"))
            | other -> matches ("/" + other)

    /// Reproduce one catalogued output's bytes from a built project, or `None` when
    /// the fixture does not produce it.
    let produceOutput root (output: string) =
        match output with
        | "command-report (--json)" -> Some(serializeReport (runWork Ship root None))
        | _ -> findProducedFile root output |> Option.map File.ReadAllText

    // ---- JSON key extraction (baseline conformance) ----
    // Observed inventories walk to full depth (ADR-0002 Gap B finding 6 / #261) via the shared
    // `ReleaseContract.fullDepthKeys`, so nested drift reaches `evaluate`, not only the byte-goldens.

    // ============================================================
    //  cell evaluators
    // ============================================================

    let evaluateLifecycleCell (stateRoots: Map<string, string>) runRoot coordinates =
        let commandValue = coord "command" coordinates
        let projection = projectionOfValue (coord "projection" coordinates)
        let state = coord "state" coordinates

        match parseCommand commandValue with
        | Error message ->
            Fail(failure lifecycleMatrixName coordinates "" message "Add the command to the validation reverse map.")
        | Ok command ->
            if stateRank state < commandMinRank command then
                SkippedWithReason $"{commandValue} is not applicable to a {state} project"
            else
                match Map.tryFind state stateRoots with
                | None -> NotValidated $"state {state} could not be constructed"
                | Some baseRoot ->
                    let cellRoot = tempDirectory runRoot
                    copyDirectory baseRoot cellRoot

                    let inputText = if command = Specify then Some specifyIntent else None
                    let report = runWork command cellRoot inputText
                    let rendered = renderProjection projection report

                    if String.IsNullOrEmpty rendered then
                        Fail(
                            failure
                                lifecycleMatrixName
                                coordinates
                                cellRoot
                                "command produced empty output"
                                "Ensure the command renders a non-empty report for this projection."
                        )
                    elif hasAnsi rendered then
                        Fail(
                            failure
                                lifecycleMatrixName
                                coordinates
                                cellRoot
                                "degraded projection emitted ANSI control codes"
                                "Strip ANSI when output is non-interactive/color-disabled."
                        )
                    else
                        match projection with
                        | Json ->
                            if rendered.Contains("\"schemaVersion\"") then
                                Pass
                            else
                                Fail(
                                    failure
                                        lifecycleMatrixName
                                        coordinates
                                        cellRoot
                                        "JSON report missing schemaVersion"
                                        "Emit a schemaVersion field in the command report."
                                )
                        | _ -> Pass

    /// Determinism cells compare snapshots of every catalogued output, all taken
    /// from the SAME authored sources: `neutral` and `repeat` are two regenerations
    /// under the neutral host (byte-identical reproduction, INV-3); `perturbed` is a
    /// regeneration under a varied locale/time zone/cwd (host-variance, INV-3a).
    let evaluateDeterminismCell
        (neutral: Map<string, string option>)
        (repeat: Map<string, string option>)
        (perturbed: Map<string, string option>)
        (degraded: Map<string, string option>)
        coordinates
        =
        let output = coord "output" coordinates
        let environment = coord "environment" coordinates

        let lookup (snapshot: Map<string, string option>) =
            Map.tryFind output snapshot |> Option.flatten

        let isColorDisabling =
            environment = environmentClassValue ColorDisabled
            || environment = environmentClassValue TermDumb

        match lookup neutral with
        | None -> SkippedWithReason $"output {output} is not produced by the fixture"
        | Some neutralBytes ->
            if hasAnsi neutralBytes then
                Fail(
                    failure
                        determinismMatrixName
                        coordinates
                        output
                        "produced output contains ANSI control codes"
                        "Exclude ANSI styling from the deterministic contract."
                )
            elif isColorDisabling then
                // Assert on the snapshot produced with NO_COLOR / TERM=dumb actually set
                // (feature 067 / FR-009): the output must carry no ANSI and stay byte-identical to
                // the neutral run. Note the enrolled `DeterminismOutputs` are all machine artifacts
                // (persisted JSON/MD views + the --json report), none of which is a rendered
                // text/rich projection, so in practice these bytes never depend on the color env
                // and this cell reproduces `Pass` — it proves env-independence of the *persisted*
                // surface, not of rendered output. The rendered-output ANSI-degradation guarantee
                // lives where color can actually appear: the CLI-process test `ValidateCommandTests`.
                match lookup degraded with
                | Some degradedBytes when hasAnsi degradedBytes ->
                    Fail(
                        failure
                            determinismMatrixName
                            coordinates
                            output
                            "produced output contains ANSI control codes when color is disabled"
                            "Strip ANSI when NO_COLOR / TERM=dumb is set."
                    )
                | Some degradedBytes when degradedBytes = neutralBytes -> Pass
                | Some _ ->
                    Fail(
                        failure
                            determinismMatrixName
                            coordinates
                            output
                            "output differs when the color-disabling environment is applied"
                            "Remove color/TTY-environment dependence from the producer."
                    )
                | None -> SkippedWithReason $"output {output} is not produced with color disabled"
            elif environment = environmentClassValue PerturbedHostEnvironment then
                match lookup perturbed with
                | Some perturbedBytes when perturbedBytes = neutralBytes -> Pass
                | Some _ ->
                    Fail(
                        failure
                            determinismMatrixName
                            coordinates
                            output
                            "output differs under a perturbed host environment"
                            "Remove locale/time-zone/cwd/ordering dependence from the producer."
                    )
                | None -> SkippedWithReason $"output {output} is not produced under the perturbed host"
            else
                match lookup repeat with
                | Some repeatBytes when repeatBytes = neutralBytes -> Pass
                | Some _ ->
                    Fail(
                        failure
                            determinismMatrixName
                            coordinates
                            output
                            "output is not reproduced byte-identically over identical inputs"
                            "Remove ordering/clock/host nondeterminism from the producer."
                    )
                | None -> SkippedWithReason $"output {output} is not produced on reproduction"

    let evaluateBaselineCells (release: ReleaseReadiness) (fullRoot: string) =
        // Build a produced snapshot from the real generated JSON views, reuse
        // ReleaseContract.evaluate, then map drift back to each contract's cell.
        let produced =
            release.Catalog
            |> List.choose (fun entry ->
                match entry.Kind with
                | CommandOutputContract -> None
                | GeneratedViewContract(_, RC.Markdown) -> None
                | GeneratedViewContract(_, RC.Json) ->
                    findProducedFile fullRoot entry.Contract
                    |> Option.map (fun file ->
                        { Contract = entry.Contract
                          Source = entry.SourceArtifact
                          Inventory = fullDepthKeys (File.ReadAllText file) }))

        let diagnostics = evaluate release produced

        let producedContracts =
            produced |> List.map (fun item -> item.Contract) |> Set.ofList

        release.Catalog
        |> List.collect (fun entry ->
            let baselineCoordinates = [ "contract", entry.Contract; "check", "baseline" ]
            let conformanceCoordinates = [ "contract", entry.Contract; "check", "conformance" ]

            let baselineStatus =
                if entry.BaselinePresent then
                    Pass
                else
                    NotValidated $"contract {entry.Contract} has no locking baseline"

            let related =
                diagnostics
                |> List.filter (fun diagnostic -> diagnostic.Message.Contains($"'{entry.Contract}'"))

            let conformanceStatus =
                match related with
                | diagnostic :: _ ->
                    Fail(
                        failure
                            baselineMatrixName
                            conformanceCoordinates
                            entry.SourceArtifact.Path
                            diagnostic.Message
                            diagnostic.Correction
                    )
                | [] ->
                    if Set.contains entry.Contract producedContracts then
                        Pass
                    else
                        SkippedWithReason $"produced artifact for {entry.Contract} not resolved in fixture"

            [ { Coordinates = baselineCoordinates
                Status = baselineStatus }
              { Coordinates = conformanceCoordinates
                Status = conformanceStatus } ])

    let evaluateCompatibilityCells (release: ReleaseReadiness) (fullRoot: string) =
        let handoffContractVersion =
            findProducedFile fullRoot "governance-handoff.json"
            |> Option.bind (fun file ->
                try
                    use document = JsonDocument.Parse(File.ReadAllText file)

                    match document.RootElement.TryGetProperty "contractVersion" with
                    | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
                    | _ -> None
                with _ ->
                    None)

        release.Compatibility
        |> List.collect (fun entry ->
            let handoffCoordinates =
                [ "entry", entry.SddVersionLine; "check", "handoffContractVersion" ]

            let specKitCoordinates = [ "entry", entry.SddVersionLine; "check", "specKitRange" ]

            // Governance compatibility is an optional integration fact: a present,
            // conforming contractVersion passes; absence is a clean skip, never a
            // Fail (FR-005 / INV-8).
            let handoffStatus =
                match entry.GovernanceContractVersionRange, handoffContractVersion with
                | Some _, Some _ -> Pass
                | Some _, None ->
                    SkippedWithReason "no governance-handoff produced; compatibility recorded as optional fact"
                | None, _ -> SkippedWithReason "no governance contract range declared; optional integration fact"

            // SDD emits no Spec-Kit-version artifact, so this is a well-formedness
            // check that the declared range is present and parseable (matrix-runner C-6).
            let specKitStatus =
                if String.IsNullOrWhiteSpace entry.SpecKitRange then
                    SkippedWithReason "no Spec Kit range declared; nothing to validate"
                else
                    Pass

            [ { Coordinates = handoffCoordinates
                Status = handoffStatus }
              { Coordinates = specKitCoordinates
                Status = specKitStatus } ])

    // ---- host perturbation ----

    /// Run `action` with a set of environment variables temporarily overridden, restoring the
    /// originals afterward. Used to produce output snapshots under a genuinely-applied host
    /// condition (feature 067 / FR-009) — e.g. the color-disabling `NO_COLOR` / `TERM=dumb`.
    let withEnvVars (vars: (string * string) list) (action: unit -> 'a) =
        let originals =
            vars |> List.map (fun (key, _) -> key, Environment.GetEnvironmentVariable key)

        try
            vars
            |> List.iter (fun (key, value) -> Environment.SetEnvironmentVariable(key, value))

            action ()
        finally
            originals
            |> List.iter (fun (key, value) -> Environment.SetEnvironmentVariable(key, value))

    let withPerturbedHost (action: unit -> 'a) =
        let originalCulture = Thread.CurrentThread.CurrentCulture
        let originalTz = Environment.GetEnvironmentVariable "TZ"
        let originalCwd = Directory.GetCurrentDirectory()

        // A genuinely different working directory (feature 067 / FR-009): withPerturbedHost's
        // contract is to vary locale + time zone + cwd; cwd was previously not varied, so a
        // cwd-dependent producer would have slipped past the perturbed-host determinism cell.
        let perturbedCwd = Path.GetTempPath()

        try
            Thread.CurrentThread.CurrentCulture <- CultureInfo "de-DE"
            Environment.SetEnvironmentVariable("TZ", "Asia/Kolkata")
            Directory.SetCurrentDirectory perturbedCwd
            action ()
        finally
            Thread.CurrentThread.CurrentCulture <- originalCulture
            Environment.SetEnvironmentVariable("TZ", originalTz)
            Directory.SetCurrentDirectory originalCwd

    // ============================================================
    //  coverage reconciliation (US2 / FR-012 / INV-7)
    // ============================================================

    /// The real command surface, enumerated from an **exhaustive `SddCommand` match**
    /// independent of the declared matrix: adding a DU case is a compile-time break
    /// here that forces the author to cover it (INV-7; no reflection — Constitution IV).
    let realCommandTokens =
        let token command =
            match command with
            | Init -> "init"
            | Charter -> "charter"
            | Specify -> "specify"
            | Clarify -> "clarify"
            | Checklist -> "checklist"
            | Plan -> "plan"
            | Tasks -> "tasks"
            | Analyze -> "analyze"
            | Evidence -> "evidence"
            | Verify -> "verify"
            | Ship -> "ship"
            | Agents -> "agents"
            | Refresh -> "refresh"
            | Scaffold -> "scaffold"
            | Doctor -> "doctor"
            | Upgrade -> "upgrade"
            | Lint -> "lint"
            | Surface -> "surface"
            | Help -> "help"

        // `scaffold` (and, feature 053, `doctor`/`upgrade`) are deliberately excluded from
        // determinism-matrix reconciliation: `scaffold`/`upgrade` spawn an external process
        // (and `upgrade` reads stdin), so they are environment-sensed rather than
        // byte-deterministic; they are covered by their own semantic suites.
        // (Legacy note preserved below.)
        // `scaffold` is deliberately excluded from determinism-matrix reconciliation:
        // its real exercise spawns an external `dotnet new` template engine, so it is
        // environment-sensed rather than byte-deterministic. It is covered by the
        // scaffold semantic suite and the cross-repo Rendering proof, not these
        // matrices (mirrors the harness needing no Governance runtime). The exhaustive
        // `token` match above still forces a compile-time decision for any new case.
        [ Init
          Charter
          Specify
          Clarify
          Checklist
          Plan
          Tasks
          Analyze
          Evidence
          Verify
          Ship
          Agents
          Refresh ]
        // `lint`/`<stage> --explain` are excluded here like scaffold/doctor/upgrade: lint requires
        // an `<artifact>` argument and is covered by its own read-only semantic suite (feature 076).
        // `surface` (feature 086) is likewise excluded — a cross-cutting API-surface baseline verb
        // covered by its own semantic suite (its enumerate/read/write over a workspace tree is not a
        // determinism-matrix stage).
        |> List.map token

    /// Map a produced readiness file to its catalogued generated-view name, or `None`
    /// when it is not a catalogued view (those files are not reconciled here).
    let classifyProducedView (path: string) =
        let normalized = path.Replace('\\', '/')
        let basename = normalized.Substring(normalized.LastIndexOf('/') + 1)

        if normalized.Contains("/agent-commands/") then
            match basename with
            | "guidance.json" -> Some "agent-commands/<target>/guidance.json"
            | "commands.md" -> Some "agent-commands/<target>/commands.md"
            | "skills.md" -> Some "agent-commands/<target>/skills.md"
            | _ -> None
        else
            match basename with
            | "work-model.json"
            | "analysis.json"
            | "verify.json"
            | "ship.json"
            | "ship-verdict.json"
            | "governance-handoff.json"
            | "summary.md" -> Some basename
            | _ -> None

    /// Reconcile declared coverage against the real produced surface, per dimension,
    /// from sources independent of the declared matrix (matrix-runner C-7). An
    /// uncovered real surface is a `CoverageGap`; a declared entry naming a vanished
    /// surface is a detectable `Fail` — the real surface is authoritative.
    let reconcileSurface (plan: MatrixPlan) (release: ReleaseReadiness) (fullRoot: string) =
        let declaredCommands = plan.LifecycleCommands |> List.map commandName |> Set.ofList
        let realCommands = Set.ofList realCommandTokens

        let commandGaps =
            Set.difference realCommands declaredCommands
            |> Set.toList
            |> List.map (fun token ->
                lifecycleMatrixName,
                { Coordinates = [ "command", token ]
                  Status =
                    CoverageGap $"command '{token}' is in the real SddCommand surface but no lifecycle cell covers it" })

        let staleCommands =
            Set.difference declaredCommands realCommands
            |> Set.toList
            |> List.map (fun token ->
                lifecycleMatrixName,
                { Coordinates = [ "command", token ]
                  Status =
                    Fail(
                        failure
                            lifecycleMatrixName
                            [ "command", token ]
                            ""
                            "declared command no longer exists in the real SddCommand surface"
                            "Remove the stale command from the validation plan."
                    ) })

        let declaredContracts = Set.ofList plan.BaselineContracts

        let realContracts =
            release.Catalog |> List.map (fun entry -> entry.Contract) |> Set.ofList

        let contractGaps =
            Set.difference realContracts declaredContracts
            |> Set.toList
            |> List.map (fun contract ->
                baselineMatrixName,
                { Coordinates = [ "contract", contract ]
                  Status =
                    CoverageGap $"catalog contract '{contract}' is in release-readiness but no baseline cell covers it" })

        let staleContracts =
            Set.difference declaredContracts realContracts
            |> Set.toList
            |> List.map (fun contract ->
                baselineMatrixName,
                { Coordinates = [ "contract", contract ]
                  Status =
                    Fail(
                        failure
                            baselineMatrixName
                            [ "contract", contract ]
                            ""
                            "declared contract is absent from the release catalog"
                            "Remove the stale contract or restore it to release-readiness.json."
                    ) })

        let declaredViews = Set.ofList plan.DeterminismOutputs

        let viewGaps =
            if fullRoot = "" then
                []
            else
                let readiness = readinessRoot fullRoot

                if not (Directory.Exists readiness) then
                    []
                else
                    Directory.GetFiles(readiness, "*", SearchOption.AllDirectories)
                    |> Array.toList
                    |> List.choose classifyProducedView
                    |> List.distinct
                    |> List.filter (fun view -> not (Set.contains view declaredViews))
                    |> List.map (fun view ->
                        determinismMatrixName,
                        { Coordinates = [ "output", view ]
                          Status =
                            CoverageGap $"produced view '{view}' is in readiness/ but no determinism cell covers it" })

        commandGaps @ staleCommands @ contractGaps @ staleContracts @ viewGaps

    // ============================================================
    //  run
    // ============================================================

    let isInjected divergences matrixName coordinates =
        divergences
        |> List.exists (fun (name, coords) -> name = matrixName && coords = coordinates)

    let rec run (options: RunnerOptions) : ValidationReport =
        let plan = options.Plan |> Option.defaultValue defaultPlan
        let model, _effects = init plan

        let selected name =
            match options.OnlyMatrix with
            | Some only -> only = name
            | None -> true

        // One temp root per run; every fixture/cell copy nests under it and the whole tree is
        // deleted in the `finally` below, so a run leaves no temp residue (feature 067 / FR-007).
        let runRoot =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-validate-run-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory runRoot |> ignore

        try
            runCore options plan model selected runRoot
        finally
            try
                if Directory.Exists runRoot then
                    Directory.Delete(runRoot, true)
            with _ ->
                ()

    and runCore (options: RunnerOptions) plan model selected runRoot : ValidationReport =
        // Build shared fixtures only for the matrices that will run.
        let stateRoots =
            if selected lifecycleMatrixName then
                plan.States
                |> List.map (fun state -> state, buildState runRoot state)
                |> Map.ofList
            else
                Map.empty

        // One full project (fixed authored sources) feeds the determinism, baseline,
        // and compatibility matrices.
        let neutralRoot =
            if
                selected determinismMatrixName
                || selected baselineMatrixName
                || selected compatibilityMatrixName
            then
                buildFullProject runRoot
            else
                ""

        // Regenerate every catalogued generated view from the SAME authored sources
        // and snapshot the bytes. `neutralSnapshot`/`repeatSnapshot` are two neutral
        // regenerations (reproducibility); `perturbedSnapshot` is one under a varied
        // host (locale/time zone/cwd) — host facts that MUST NOT change output.
        let regenerateAndSnapshot () =
            runWork Analyze neutralRoot None |> ignore
            runWork Verify neutralRoot None |> ignore
            runWork Ship neutralRoot None |> ignore
            runWork Agents neutralRoot None |> ignore
            runWork Refresh neutralRoot None |> ignore

            plan.DeterminismOutputs
            |> List.map (fun output -> output, produceOutput neutralRoot output)
            |> Map.ofList

        let neutralSnapshot, repeatSnapshot, perturbedSnapshot, degradedSnapshot =
            if selected determinismMatrixName then
                let neutral = regenerateAndSnapshot ()
                let repeat = regenerateAndSnapshot ()
                let perturbed = withPerturbedHost regenerateAndSnapshot
                // A snapshot produced with the color-disabling condition genuinely applied
                // (feature 067 / FR-009): the ColorDisabled / TermDumb cells assert on THIS output
                // rather than reusing the neutral one, so a producer that consulted the env would
                // be caught here instead of the cell running a vacuous neutral comparison.
                let degraded = withEnvVars [ "NO_COLOR", "1"; "TERM", "dumb" ] regenerateAndSnapshot

                neutral, repeat, perturbed, degraded
            else
                Map.empty, Map.empty, Map.empty, Map.empty

        let release = currentRelease ()

        // Evaluate every cell of every selected matrix; unselected matrices keep
        // their pending NotValidated status so a partial run never reads as a full
        // pass (INV-1 / FR-007 / C-9).
        let evaluateCell matrixName (cell: MatrixCell) =
            if isInjected options.InjectedDivergences matrixName cell.Coordinates then
                Fail(
                    failure
                        matrixName
                        cell.Coordinates
                        ""
                        "seeded single-cell divergence"
                        "Resolve the seeded regression in this matrix cell."
                )
            elif not (selected matrixName) then
                cell.Status
            elif matrixName = lifecycleMatrixName then
                evaluateLifecycleCell stateRoots runRoot cell.Coordinates
            elif matrixName = determinismMatrixName then
                evaluateDeterminismCell
                    neutralSnapshot
                    repeatSnapshot
                    perturbedSnapshot
                    degradedSnapshot
                    cell.Coordinates
            else
                cell.Status

        // Baseline + compatibility produce their own cell sets (one call each).
        let baselineCells =
            if selected baselineMatrixName then
                evaluateBaselineCells release neutralRoot
            else
                []

        let compatibilityCells =
            if selected compatibilityMatrixName then
                evaluateCompatibilityCells release neutralRoot
            else
                []

        let folded =
            model.Matrices
            |> List.fold
                (fun state matrix ->
                    let precomputed =
                        if matrix.Name = baselineMatrixName then
                            Some baselineCells
                        elif matrix.Name = compatibilityMatrixName then
                            Some compatibilityCells
                        else
                            None

                    match precomputed with
                    | Some cells when selected matrix.Name ->
                        cells
                        |> List.fold
                            (fun current evaluated -> update (CellEvaluated(matrix.Name, evaluated)) current |> fst)
                            state
                    | _ ->
                        matrix.Cells
                        |> List.fold
                            (fun current cell ->
                                let evaluated =
                                    { cell with
                                        Status = evaluateCell matrix.Name cell }

                                update (CellEvaluated(matrix.Name, evaluated)) current |> fst)
                            state)
                model

        // Reconcile declared coverage against the real produced surface (US2 / INV-7).
        let reconciled, _ =
            update (SurfaceReconciled(reconcileSurface plan release neutralRoot)) folded

        let reportModel, _ = update ValidationMsg.BuildReport reconciled

        let report =
            reportModel.Report
            |> Option.defaultWith (fun () ->
                failwith
                    "ValidationRunner.run: invariant violated — validation report model has no Report after ValidationMsg.BuildReport")

        report
