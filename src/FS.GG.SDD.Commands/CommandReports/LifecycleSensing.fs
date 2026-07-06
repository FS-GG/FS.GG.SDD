namespace FS.GG.SDD.Commands.Internal

open FS.GG.SDD.Commands.CommandTypes

// Feature 084: single source of truth for the lifecycle stage -> artifact path map, the
// on-disk sensing read-effects, and the pure derivation of the lifecycle-status footer fact.
// Compiled before ReportAssembly (which derives the fact from interpreted effects) and before
// Foundation (which plans the sensing reads at the MVU edge), so both share ONE path map and
// cannot drift. Internal module, no separate .fsi (matches the sibling ReportAssembly /
// RemediationPointers / DiagnosticConstructors convention; not part of the public surface).
module internal LifecycleSensing =

    // Canonical stage artifact paths. Foundation's own path builders delegate to these so the
    // paths that are SENSED (here) are byte-identical to the paths other commands already READ.
    let charterPath workId = $"work/{workId}/charter.md"
    let specPath workId = $"work/{workId}/spec.md"
    let clarificationPath workId = $"work/{workId}/clarifications.md"
    let checklistPath workId = $"work/{workId}/checklist.md"
    let planPath workId = $"work/{workId}/plan.md"
    let tasksPath workId = $"work/{workId}/tasks.yml"
    let evidencePath workId = $"work/{workId}/evidence.yml"
    let analysisPath workId = $"readiness/{workId}/analysis.json"
    let verifyPath workId = $"readiness/{workId}/verify.json"
    let shipPath workId = $"readiness/{workId}/ship.json"

    /// The ten lifecycle stages in canonical order, each with its sensing path builder.
    let stages: (SddCommand * (string -> string)) list =
        [ Charter, charterPath
          Specify, specPath
          Clarify, clarificationPath
          Checklist, checklistPath
          Plan, planPath
          Tasks, tasksPath
          Analyze, analysisPath
          Evidence, evidencePath
          Verify, verifyPath
          Ship, shipPath ]

    let totalStages = List.length stages

    /// The ten lifecycle stages are commands; the cross-cutting verbs are not.
    let isLifecycleStageCommand (command: SddCommand) =
        match command with
        | Charter
        | Specify
        | Clarify
        | Checklist
        | Plan
        | Tasks
        | Analyze
        | Evidence
        | Verify
        | Ship -> true
        | Init
        | Agents
        | Refresh
        | Scaffold
        | Doctor
        | Upgrade
        | Lint -> false

    /// The sensing effects: recursive enumerations of `work/` and `readiness/`. Deliberately NOT
    /// per-file `ReadFile`s — the readiness/work-model generators read artifact *content* via
    /// `ReadFile` snapshots, so adding those would leak a stage's content into a generated view.
    /// Directory listings carry no content, so sensing stays presence-only and side-effect free on
    /// generation. Emitted at the pure plan step (Foundation) and interpreted at the edge (Principle V).
    let lifecycleSensingEffects (_workId: string) : CommandEffect list =
        [ EnumerateDirectory "work"; EnumerateDirectory "readiness" ]

    // Every path listed by any interpreted directory enumeration (paths are root-relative and
    // forward-slashed, e.g. `work/<id>/charter.md`). Presence-only — never re-parses or
    // freshness-checks the artifact (Principle VIII / FR-004).
    let private sensedPaths (interpreted: CommandEffectResult list) : Set<string> =
        interpreted
        |> List.collect (fun result ->
            match result.Effect, result.Snapshot with
            | EnumerateDirectory _, Some snapshot ->
                snapshot.Text.Split([| '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.toList
            | _ -> [])
        |> Set.ofList

    /// Pure derivation of the lifecycle-status fact (research.md §4 rules). No filesystem access:
    /// the sensing already happened in the interpreter; this folds its snapshots.
    let deriveFromEffects
        (command: SddCommand)
        (workId: string option)
        (outcome: CommandOutcome)
        (interpreted: CommandEffectResult list)
        : LifecycleStatus =

        let isStage = isLifecycleStageCommand command
        let successor = nextLifecycleCommand command
        let listed = sensedPaths interpreted

        let doneOf (pathOf: string -> string) =
            match workId with
            | Some id -> Set.contains (pathOf id) listed
            | None -> false

        let baseEntries =
            stages
            |> List.mapi (fun index (stageCommand, pathOf) ->
                let ordinal = index + 1
                let isDone = doneOf pathOf

                let state =
                    if isStage then
                        if outcome = CommandOutcome.Blocked && stageCommand = command then
                            StageState.Blocked
                        elif stageCommand = command then
                            StageState.Current
                        elif isDone then
                            StageState.Done
                        elif successor = Some stageCommand then
                            StageState.Next
                        else
                            StageState.Pending
                    elif isDone then
                        StageState.Done
                    else
                        StageState.Pending

                { Command = stageCommand
                  Ordinal = ordinal
                  State = state })

        // Cross-cutting commands have no Current; mark the lowest-ordinal Pending stage as Next.
        let entries =
            if isStage then
                baseEntries
            else
                match baseEntries |> List.tryFindIndex (fun entry -> entry.State = StageState.Pending) with
                | Some pendingIndex ->
                    baseEntries
                    |> List.mapi (fun index entry ->
                        if index = pendingIndex then
                            { entry with State = StageState.Next }
                        else
                            entry)
                | None -> baseEntries

        let currentOrdinal =
            if isStage then
                entries
                |> List.tryFind (fun entry -> entry.Command = command)
                |> Option.map (fun entry -> entry.Ordinal)
            else
                entries
                |> List.filter (fun entry -> entry.State = StageState.Done)
                |> List.length
                |> Some

        // `nextCommand` always matches the rail's `Next` cell, so the footer never recommends a
        // stage it also shows as `done`: for a lifecycle stage the successor is the `Next` cell only
        // when its artifact is absent (a re-run whose successor already exists yields no `Next`, so
        // `nextCommand` is None); for a cross-cutting command it is the lowest-ordinal pending stage.
        let nextCommand =
            entries
            |> List.tryFind (fun entry -> entry.State = StageState.Next)
            |> Option.map (fun entry -> entry.Command)

        { WorkId = workId
          Stages = entries
          CurrentOrdinal = currentOrdinal
          TotalStages = totalStages
          Outcome = outcome
          NextCommand = nextCommand
          IsLifecycleStage = isStage }
