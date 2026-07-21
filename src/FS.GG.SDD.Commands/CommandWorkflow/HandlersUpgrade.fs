namespace FS.GG.SDD.Commands.Internal

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.HandlersDoctor

/// `fsgg-sdd upgrade` handler (feature 053, US2–US4). The reconciliation verb: it re-derives
/// its next step from the interpreted-effect log each `nextLifecycleEffects` tick (like
/// `HandlersScaffold`, no new model state field). Only the *actionable* preview steps
/// (`Outcome = "wouldApply"`) are reconciled; each is shown as a diff and applied only after
/// its own `Confirm` (interactive) or under `--yes`. `upgrade` is the **only** command that
/// mutates for remediation (FR-006/FR-008): the CLI self-update `RunProcess`, the no-clobber
/// re-seed `WriteFile`s (init's `AgentGuidanceTarget` effects for the *missing* paths only,
/// R8/FR-010), and (value-agnostically inert here, R6) the consumer-only re-pin.
module internal HandlersUpgrade =

    // SDD's own tool package id (not a provider/template/rendering literal). The self-update
    // is orchestrated at the same `RunProcess` edge scaffold uses; the updated binary takes
    // effect on the next invocation (spec Assumption / R4).
    let selfUpdatePackageId = "FS.GG.SDD.Cli"

    let selfUpdateEffect =
        RunProcess("dotnet", [ "tool"; "update"; selfUpdatePackageId; "--global" ], "")

    let private isSelfUpdate effect =
        match effect with
        | RunProcess("dotnet", "tool" :: "update" :: _, _) -> true
        | _ -> false

    // No-clobber re-seed: the init `AgentGuidanceTarget` WriteFile effects for the MISSING
    // seeded paths only (R8/FR-010). Filtering `initEffects` to the missing set — and the
    // interpreter's `canOverwrite AgentGuidanceTarget` refusal on a present file — doubly
    // guarantees a present, author-edited artifact is never overwritten (US4-AC2).
    let reSeedEffects (request: CommandRequest) (missing: string list) =
        let missingSet = missing |> List.map normalizeRelativePath |> Set.ofList

        initEffects request
        |> List.filter (fun effect ->
            match effect with
            | WriteFile(path, _, _) -> Set.contains (normalizeRelativePath path) missingSet
            | _ -> false)

    // ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced (driver + product classes) skill copies among a
    // re-seed step's targets, reconstructed as no-clobber writes from the SAME embedded, content-
    // addressed plan the drift preview used (`Drift.ownerSourcedBackfill`, filtered to the step's
    // declared targets), so the applied bytes are exactly the verified bytes previewed — a backfill
    // can never write an unverified body (ADR-0014). Empty when the scaffold has no provenance or no
    // owner package is embedded. These are disjoint from `reSeedEffects` (which reconstructs only the
    // SEEDED init writes among the targets), so the two together cover every re-seed target once.
    let ownerBackfillEffects model (targets: string list) =
        match resolveProvenance model with
        | Some record ->
            Drift.ownerSourcedBackfill record
            |> List.filter (fun (path, _) -> List.contains path targets)
            |> List.map (fun (path, body) -> WriteFile(path, body, AgentGuidanceTarget))
        | None -> []

    let applyEffectsFor model (request: CommandRequest) (step: ReconciliationStep) =
        match step.StepId with
        | ReconciliationStepId.CliSelfUpdate -> [ selfUpdateEffect ]
        | ReconciliationStepId.ArtifactReSeed ->
            reSeedEffects request step.TargetPaths
            @ ownerBackfillEffects model step.TargetPaths
        // templateRePin is `noTarget` in this feature (R6) and never actionable.
        | ReconciliationStepId.TemplateRePin -> []

    let private confirmPrompt (step: ReconciliationStep) =
        $"Apply {reconciliationStepIdValue step.StepId}?\n{step.DiffPreview}\n[y/N] "

    // A confirm decision from the interpreted-effect log for one step, or None when the
    // step's Confirm has not been interpreted yet.
    let private confirmDecision model stepId : bool option =
        model.InterpretedEffects
        |> List.tryPick (fun result ->
            match result.Effect with
            | Confirm(s, _) when s = stepId -> result.Confirmed
            | _ -> None)

    let private confirmPlanned model stepId =
        model.PendingEffects
        |> List.exists (fun effect ->
            match effect with
            | Confirm(s, _) when s = stepId -> true
            | _ -> false)

    // The applied/failed outcome of a step's apply effects, read from the interpreted log:
    // a process step is `applied` iff it started and exited 0; a write step iff it succeeded.
    let private applyOutcome model (effects: CommandEffect list) =
        let ok =
            effects
            |> List.forall (fun effect ->
                match
                    model.InterpretedEffects
                    |> List.tryFind (fun result -> effectKey result.Effect = effectKey effect)
                with
                | Some result ->
                    match result.Effect with
                    | RunProcess _ ->
                        match result.Process with
                        | Some { Started = true; ExitCode = 0 } -> true
                        | _ -> false
                    | _ -> result.Succeeded
                | None -> false)

        if ok then
            ReconciliationOutcome.Applied
        else
            ReconciliationOutcome.Failed

    type private StepProgress =
        | Resolved of ReconciliationOutcome
        | EmitEffects of CommandEffect list
        | Awaiting

    let private applyStage model (request: CommandRequest) (step: ReconciliationStep) =
        let effects = applyEffectsFor model request step

        let allInterpreted =
            effects |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

        let anyPlanned =
            effects |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        if List.isEmpty effects then
            Resolved ReconciliationOutcome.Applied
        elif allInterpreted then
            Resolved(applyOutcome model effects)
        elif anyPlanned then
            Awaiting
        else
            EmitEffects effects

    let private stepProgress model (request: CommandRequest) (step: ReconciliationStep) =
        if request.AssumeYes then
            // `--yes`: apply each step directly — no `Confirm` emitted (FR-011).
            applyStage model request step
        else
            match confirmDecision model (reconciliationStepIdValue step.StepId) with
            | Some true -> applyStage model request step
            | Some false -> Resolved ReconciliationOutcome.Skipped
            | None ->
                if confirmPlanned model (reconciliationStepIdValue step.StepId) then
                    Awaiting
                else
                    EmitEffects [ Confirm(reconciliationStepIdValue step.StepId, confirmPrompt step) ]

    let private selfUpdateExitCode model =
        model.InterpretedEffects
        |> List.tryPick (fun result -> if isSelfUpdate result.Effect then result.Process else None)
        |> function
            | Some processResult -> processResult.ExitCode
            | None -> -1

    let private noOpSummary (request: CommandRequest) (drift: Drift.DriftReport) hint : UpgradeSummary =
        { HasProvenance = drift.HasProvenance
          Mode = if request.AssumeYes then "assumeYes" else "interactive"
          AlreadyCoherent = true
          Steps = drift.Steps
          AppliedStepIds = []
          SkippedStepIds = []
          FailedStepIds = []
          SkillDriftPaths = drift.SkillDriftPaths
          ResidualDrift = false
          NextActionHint = hint }

    let private finalizeApply
        model
        (request: CommandRequest)
        (drift: Drift.DriftReport)
        (actionable: ReconciliationStep list)
        =
        let outcomes =
            actionable
            |> List.map (fun step ->
                let outcome =
                    match stepProgress model request step with
                    | Resolved value -> value
                    | _ -> ReconciliationOutcome.Failed

                step.StepId, outcome)

        let ofOutcome value =
            outcomes |> List.filter (fun (_, o) -> o = value) |> List.map fst |> List.sort

        let applied = ofOutcome ReconciliationOutcome.Applied
        let skipped = ofOutcome ReconciliationOutcome.Skipped
        let failed = ofOutcome ReconciliationOutcome.Failed

        let resolvedMap = Map.ofList outcomes

        let steps =
            drift.Steps
            |> List.map (fun step ->
                match Map.tryFind step.StepId resolvedMap with
                | Some outcome -> { step with Outcome = outcome }
                | None -> step)

        // 058/ADR-0014 P1: the content drift `upgrade` does NOT repair in this phase — a
        // present-but-divergent or hash-mismatched copy, and product-skill loss (advisory
        // first, no clobber). Missing *seeded* copies are excluded ONLY when the `artifactReSeed`
        // step actually applied (a skipped/failed re-seed leaves them outstanding, so they stay
        // in the advisory surface).
        let repairedMissing =
            if List.contains ReconciliationStepId.ArtifactReSeed applied then
                drift.MissingArtifactPaths
            else
                []

        let unrepairedSkillDrift =
            drift.SkillDriftPaths
            |> List.filter (fun path -> not (List.contains path repairedMissing))

        // FR-013: never report an incomplete reconciliation as complete. A skipped or
        // failed step leaves residual drift; so does a self-update that "applied" (the
        // running binary is unchanged until the next invocation, R4); so does un-repaired
        // content drift (advisory in P1).
        let residualDrift =
            not (List.isEmpty skipped)
            || not (List.isEmpty failed)
            || List.contains ReconciliationStepId.CliSelfUpdate applied
            || not (List.isEmpty unrepairedSkillDrift)

        let diagnostics =
            if not (List.isEmpty failed) then
                failed
                |> List.map (fun stepId ->
                    if stepId = ReconciliationStepId.CliSelfUpdate then
                        upgradeSelfUpdateFailed (selfUpdateExitCode model)
                    else
                        upgradeStepFailed (reconciliationStepIdValue stepId))
            elif not (List.isEmpty skipped) then
                [ upgradeResidualDrift (skipped |> List.map reconciliationStepIdValue) ]
            else
                []

        let hint =
            if not (List.isEmpty failed) then
                "A confirmed step failed; inspect the failure and re-run `fsgg-sdd upgrade`."
            elif not (List.isEmpty skipped) then
                "Re-run `fsgg-sdd upgrade` and confirm the skipped step(s) to finish reconciling."
            elif not (List.isEmpty unrepairedSkillDrift) then
                "Skill content drift detected (advisory); some copies diverge from their canonical body — re-scaffold or restore the canonical skill sources."
            elif residualDrift then
                "The CLI self-update takes effect on the next invocation; re-run `fsgg-sdd doctor` afterwards to confirm coherence."
            else
                "Reconciliation complete; run `fsgg-sdd doctor` to confirm coherence."

        let summary: UpgradeSummary =
            { HasProvenance = drift.HasProvenance
              Mode = if request.AssumeYes then "assumeYes" else "interactive"
              AlreadyCoherent = false
              Steps = steps
              AppliedStepIds = applied
              SkippedStepIds = skipped
              FailedStepIds = failed
              SkillDriftPaths = unrepairedSkillDrift
              ResidualDrift = residualDrift
              NextActionHint = hint }

        { model with
            Upgrade = Some summary
            Diagnostics = model.Diagnostics @ diagnostics },
        []

    let computeUpgradeNext model =
        match model.Upgrade with
        | Some _ -> model, []
        | None ->

            // 058/ADR-0014 P1: bring the provider product-skill copies into snapshots (read-only)
            // before the content-addressed drift is computed — the same provenance-driven gate
            // `doctor` uses, shared via `HandlersDoctor.skillReadGate`.
            match skillReadGate model with
            | Some effects ->
                if List.isEmpty effects then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
            | None ->
                let request = model.Request
                let drift = computeDrift model

                let actionable =
                    drift.Steps
                    |> List.filter (fun step -> step.Outcome = ReconciliationOutcome.WouldApply)

                // #313: provenance-less no longer implies nothing-to-reconcile. The CLI axis is a
                // fact about the installed tool, so an unmet `sdd.minToolVersion` floor makes the
                // self-update step actionable even here. Absent a floor there is no actionable
                // step and this is the unchanged "nothing to reconcile" no-op.
                if not drift.HasProvenance && List.isEmpty actionable then
                    { model with
                        Upgrade = Some(noOpSummary request drift "No scaffold provenance — nothing to reconcile.") },
                    []
                elif drift.IsCoherent then
                    { model with
                        Upgrade = Some(noOpSummary request drift "Already coherent — nothing to reconcile.") },
                    []
                elif List.isEmpty actionable then
                    // 058/ADR-0014 P1: the only drift is advisory content drift (a divergent/
                    // hash-mismatched copy or product-skill loss) with NO applicable step. There is
                    // nothing to confirm or `--yes`, so this is NOT a non-interactive refusal — it is
                    // reported advisory (exit 0, residual), so CI's `upgrade` doesn't dead-end at exit 1.
                    let summary: UpgradeSummary =
                        { HasProvenance = true
                          Mode = if request.AssumeYes then "assumeYes" else "interactive"
                          AlreadyCoherent = false
                          Steps = drift.Steps
                          AppliedStepIds = []
                          SkippedStepIds = []
                          FailedStepIds = []
                          SkillDriftPaths = drift.SkillDriftPaths
                          ResidualDrift = true
                          NextActionHint =
                            "Skill content drift detected (advisory); some copies diverge from their canonical body — re-scaffold or restore the canonical skill sources." }

                    { model with Upgrade = Some summary }, []
                elif not request.AssumeYes && not request.IsInteractive then
                    // FR-012 / SC-004: non-interactive without `--yes` refuses up front when there IS
                    // actionable reconciliation — zero writes, no `Confirm`, no prompt-hang (exit 1).
                    let summary: UpgradeSummary =
                        { HasProvenance = drift.HasProvenance
                          Mode = "refusedNonInteractive"
                          AlreadyCoherent = false
                          Steps = drift.Steps
                          AppliedStepIds = []
                          SkippedStepIds = []
                          FailedStepIds = []
                          SkillDriftPaths = drift.SkillDriftPaths
                          ResidualDrift = true
                          NextActionHint =
                            "Re-run `fsgg-sdd upgrade` interactively, or pass `--yes` to apply without prompting." }

                    { model with
                        Upgrade = Some summary
                        Diagnostics = model.Diagnostics @ [ upgradeNonInteractiveNoYes () ] },
                    []
                else
                    let rec walk steps =
                        match steps with
                        | [] -> None
                        | step :: rest ->
                            match stepProgress model request step with
                            | Resolved _ -> walk rest
                            | EmitEffects effects -> Some(Choice1Of2 effects)
                            | Awaiting -> Some(Choice2Of2())

                    match walk actionable with
                    | Some(Choice1Of2 effects) ->
                        { model with
                            PendingEffects = model.PendingEffects @ effects },
                        effects
                    | Some(Choice2Of2()) -> model, []
                    | None -> finalizeApply model request drift actionable
