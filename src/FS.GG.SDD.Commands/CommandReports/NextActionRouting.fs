namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

[<AutoOpen>]
module internal NextActionRouting =
    // The best-effort "next authoring command" for the early-stage NextAction, derived
    // only from which pre-work-model stages already exist (the advisory's relatedIds).
    // request.Command is Agents/Refresh here, whose nextLifecycleCommand is None, so the
    // next step is computed from the present stages instead.
    let earlyStageNextCommand (presentStages: string list) =
        let present stage = List.contains stage presentStages

        if present "checklist" then Plan
        elif present "clarify" then Checklist
        elif present "specify" then Clarify
        elif present "charter" then Specify
        else Charter

    let nextAction
        (diagnostics: Diagnostic list)
        (reportOutcome: CommandOutcome)
        (request: CommandRequest)
        (checklist: ChecklistSummary option)
        (plan: PlanSummary option)
        (tasks: TasksSummary option)
        (analysis: AnalysisSummary option)
        (evidence: EvidenceSummary option)
        (verification: VerificationSummary option)
        (ship: ShipSummary option)
        (agentGuidance: AgentGuidanceSummary option)
        (refresh: RefreshSummary option)
        =
        // Feature 076: `fsgg-sdd lint` and `<stage> --explain` are read-only pre-flights that
        // advance no lifecycle state — they never emit a NextAction (the lint defects are the
        // output, not a blocked lifecycle step).
        if request.Command = Lint || request.Explain then
            None
        else

            let blocking =
                diagnostics
                |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                |> List.map (fun diagnostic -> diagnostic.Id)
                |> List.distinct
                |> List.sort

            if not (List.isEmpty blocking) then
                let ids = blocking |> Set.ofList

                if ids |> Set.contains "stalePlanSnapshot" then
                    // Feature 090 (#163), FR-010. A stale plan snapshot has one recovery, and it is
                    // the same one from `plan`, `tasks`, and `analyze` — so this is deliberately not
                    // gated on `request.Command`, and it precedes the generic
                    // `correctBlockingDiagnostics` fallback below (which would otherwise swallow it,
                    // `stalePlanSnapshot` being an error). RequiredArtifacts names the changed
                    // sources — the diagnostic's RelatedIds — alongside the plan itself, because
                    // reviewing them against the recorded decisions is exactly what
                    // `--accept-upstream` asserts the operator has done.
                    let changedSources =
                        diagnostics
                        |> List.tryFind (fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")
                        |> Option.map (fun diagnostic -> diagnostic.RelatedIds)
                        |> Option.defaultValue []

                    Some
                        { ActionId = "plan.acceptUpstream"
                          Command = Some Plan
                          WorkId = request.WorkId
                          Reason =
                            "The plan's recorded source snapshot is stale. Review the recorded plan decisions against the changed sources, then re-run fsgg-sdd plan --accept-upstream."
                          RequiredArtifacts =
                            (plan
                             |> Option.map (fun summary -> [ $"work/{summary.WorkId}/plan.md" ])
                             |> Option.defaultValue [])
                            @ changedSources
                          // `blocking`, not `[ "stalePlanSnapshot" ]`: this branch fires whenever the
                          // snapshot is stale, which may be *alongside* an unrelated blocker. Naming
                          // only the snapshot would drop the co-occurring ids from the JSON contract
                          // that agents drive off, sending them round the loop once per hidden
                          // blocker. The generic fallback below reports the full set; so do we.
                          BlockingDiagnosticIds = blocking }
                else

                    let correctionCommand =
                        match request.Command with
                        | Plan -> planCorrectionCommand diagnostics
                        | Tasks -> tasksCorrectionCommand diagnostics
                        | Analyze -> tasksCorrectionCommand diagnostics
                        | Verify -> verifyCorrectionCommand diagnostics
                        | Ship -> shipCorrectionCommand diagnostics
                        | Agents ->
                            if
                                ids |> Set.contains "agents.staleWorkModel"
                                || ids |> Set.contains "agents.malformedWorkModel"
                                || ids |> Set.contains "agents.blockedWorkModel"
                            then
                                Some Verify
                            else
                                None
                        | Evidence ->
                            if
                                ids |> Set.contains "evidence.missingAnalysisPrerequisite"
                                || ids |> Set.contains "evidence.analysisNotReady"
                                || ids |> Set.contains "malformedAnalysisView"
                                || ids |> Set.contains "analysisIdentityMismatch"
                            then
                                Some Analyze
                            elif
                                ids |> Set.contains "missingTasksPrerequisite"
                                || ids |> Set.contains "malformedTasksArtifact"
                                || ids |> Set.contains "tasksIdentityMismatch"
                                || ids |> Set.contains "evidence.missingRequiredSkill"
                            then
                                Some Tasks
                            elif
                                ids
                                |> Set.exists (fun id -> id.StartsWith("evidence.", StringComparison.OrdinalIgnoreCase))
                            then
                                Some Evidence
                            else
                                None
                        | Refresh -> None
                        // Feature 053: a blocked `upgrade` (a failed step, or the non-interactive
                        // refusal) points back at `upgrade` (re-run interactively / with `--yes`).
                        | Upgrade -> Some Upgrade
                        | _ -> None

                    Some
                        { ActionId = "correctBlockingDiagnostics"
                          Command = correctionCommand
                          WorkId = request.WorkId
                          Reason = "The command is blocked by diagnostics."
                          RequiredArtifacts = []
                          BlockingDiagnosticIds = blocking }
            elif
                diagnostics
                |> List.exists (fun diagnostic -> diagnostic.Id = "scaffold.cliBehindMinimum")
            then
                // Feature 052 (US3 / FR-008 / D8): the behind-minimum advisory carries a
                // non-blocking pointer to the SUPPORTED re-seed path. The remedy is
                // `fsgg-sdd init` (idempotent, no-clobber) — NOT `refresh`, which does not
                // re-seed. Names the seeded skill subtrees + early-stage guidance, sorted.
                Some
                    { ActionId = "reseedSeededSkills"
                      Command = Some Init
                      WorkId = request.WorkId
                      Reason =
                        "Installed fsgg-sdd is behind the provider-declared minimum. Upgrade the CLI, then re-run `fsgg-sdd init` to re-seed the fs-gg-sdd-* skills and .fsgg/early-stage-guidance.md (idempotent, no-clobber). Note: fsgg-sdd refresh does not re-seed."
                      RequiredArtifacts =
                        // All three seeded-skill roots (056: the neutral .agents/skills too) + early-stage guidance.
                        [ ".claude/skills"
                          ".codex/skills"
                          ".agents/skills"
                          ".fsgg/early-stage-guidance.md" ]
                        |> List.sort
                      BlockingDiagnosticIds = [] }
            elif
                diagnostics
                |> List.exists (fun diagnostic ->
                    diagnostic.Id = "agents.earlyStageGuidance"
                    || diagnostic.Id = "refresh.earlyStageGuidance")
            then
                // Early-stage (FR-004/FR-005/FR-010b): a navigable next step that routes the
                // author to the seeded static guidance and names the next authoring command,
                // computed from the pre-work-model stages that already exist.
                let presentStages =
                    diagnostics
                    |> List.tryPick (fun diagnostic ->
                        if
                            diagnostic.Id = "agents.earlyStageGuidance"
                            || diagnostic.Id = "refresh.earlyStageGuidance"
                        then
                            Some diagnostic.RelatedIds
                        else
                            None)
                    |> Option.defaultValue []

                Some
                    { ActionId = "earlyStageGuidance"
                      Command = Some(earlyStageNextCommand presentStages)
                      WorkId = request.WorkId
                      Reason =
                        "No work model exists yet; follow .fsgg/early-stage-guidance.md for the pre-work-model stages (charter, specify, clarify, checklist)."
                      RequiredArtifacts = [ ".fsgg/early-stage-guidance.md" ]
                      BlockingDiagnosticIds = [] }
            elif
                request.Command = Plan
                && diagnostics
                   |> List.exists (fun diagnostic -> diagnostic.Id = "stalePlanDecision")
            then
                Some
                    { ActionId = "plan.correctStaleDecisions"
                      Command = Some Plan
                      WorkId = request.WorkId
                      Reason = "Plan decisions need review before task generation."
                      RequiredArtifacts =
                        plan
                        |> Option.map (fun summary -> [ $"work/{summary.WorkId}/plan.md" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [ "stalePlanDecision" ] }
            elif
                request.Command = Tasks
                && diagnostics |> List.exists (fun diagnostic -> diagnostic.Id = "staleTask")
            then
                Some
                    { ActionId = "tasks.correctStaleTasks"
                      Command = Some Tasks
                      WorkId = request.WorkId
                      Reason = "Task source links need review before analysis."
                      RequiredArtifacts =
                        tasks
                        |> Option.map (fun summary -> [ $"work/{summary.WorkId}/tasks.yml" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [ "staleTask" ] }
            elif
                checklist
                |> Option.exists (fun summary -> summary.FailedBlockingCount > 0 || summary.StaleResultCount > 0)
            then
                let summary = checklist.Value

                let ids =
                    diagnostics
                    |> List.choose (fun diagnostic ->
                        if
                            diagnostic.Id = "failedRequirementsQuality"
                            || diagnostic.Id = "staleChecklistResult"
                        then
                            Some diagnostic.Id
                        else
                            None)
                    |> List.distinct
                    |> List.sort

                Some
                    { ActionId = "correctBlockingDiagnostics"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Checklist has requirements-quality findings or stale review results."
                      RequiredArtifacts =
                        [ summary.SourceSpec
                          summary.SourceClarifications
                          $"work/{summary.WorkId}/checklist.md" ]
                        |> List.sort
                      BlockingDiagnosticIds = ids }
            elif request.Command = Analyze then
                Some
                    { ActionId = "analysis.next.implement"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Lifecycle sources are current and ready for implementation."
                      RequiredArtifacts =
                        analysis
                        |> Option.map (fun summary -> [ summary.AnalysisPath ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Evidence then
                Some
                    { ActionId = "evidence.next.verify"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Evidence declarations are current and ready for verification."
                      RequiredArtifacts =
                        evidence
                        |> Option.map (fun summary ->
                            [ summary.EvidencePath; $"readiness/{summary.WorkId}/work-model.json" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Verify then
                Some
                    { ActionId = "verify.next.ship"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Verification readiness is current and ready for ship."
                      RequiredArtifacts =
                        verification
                        |> Option.map (fun summary ->
                            [ summary.VerifyPath; $"readiness/{summary.WorkId}/work-model.json" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Ship then
                Some
                    { ActionId = "ship.next.protectedBoundary"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Ship readiness is current and ready for the protected-boundary handoff."
                      RequiredArtifacts =
                        ship
                        |> Option.map (fun summary ->
                            [ summary.ShipPath; $"readiness/{summary.WorkId}/work-model.json" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Agents then
                Some
                    { ActionId = "agentsGenerated"
                      Command = None
                      WorkId = request.WorkId
                      Reason = "Generated agent guidance is current; regenerate when the work model changes."
                      RequiredArtifacts =
                        agentGuidance
                        |> Option.map (fun summary -> summary.GeneratedRoots)
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Refresh then
                let warningBlocked =
                    refresh
                    |> Option.map (fun summary -> summary.BlockedViewIds)
                    |> Option.defaultValue []

                if not (List.isEmpty warningBlocked) then
                    Some
                        { ActionId = "refresh.correctBlockedViews"
                          Command = None
                          WorkId = request.WorkId
                          Reason =
                            "Some generated views could not be refreshed; correct the named source or upstream view."
                          RequiredArtifacts = warningBlocked |> List.sort
                          BlockingDiagnosticIds = [] }
                else
                    Some
                        { ActionId = "refreshGenerated"
                          Command = None
                          WorkId = request.WorkId
                          Reason =
                            "Generated views are current; rely on the refreshed readiness for the selected work item."
                          RequiredArtifacts =
                            refresh
                            |> Option.map (fun summary -> [ summary.SummaryPath ])
                            |> Option.defaultValue []
                          BlockingDiagnosticIds = [] }
            elif request.Command = Specify && reportOutcome = CommandOutcome.NoChange then
                // §3.2 (FR-002, SC-002): an edited-but-section-complete spec re-run makes no
                // authored write. Rather than a bare, ambiguous NoChange, state the authoritative
                // rule — specify promotes only the first draft; spec.md is now authoritative and
                // is read live by downstream stages — so the author knows the edit is consumed.
                Some
                    { ActionId = "specify.next.clarify"
                      Command = Some Clarify
                      WorkId = request.WorkId
                      Reason =
                        "specify promotes only the first-draft specification; spec.md is now authoritative and is read live by downstream stages (clarify, checklist, …). Edit spec.md directly — re-running specify does not re-promote it."
                      RequiredArtifacts =
                        request.WorkId
                        |> Option.map (fun workId -> [ $"work/{workId}/charter.md"; $"work/{workId}/spec.md" ])
                        |> Option.defaultValue []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Doctor then
                // Read-only report: point drift at `upgrade`, or state coherence (FR-002/FR-005).
                let coherent = reportOutcome = CommandOutcome.NoChange

                Some
                    { ActionId =
                        if coherent then
                            "doctor.coherent"
                        else
                            "doctor.next.upgrade"
                      Command = if coherent then None else Some Upgrade
                      WorkId = None
                      Reason =
                        if coherent then
                            "Scaffold is coherent — nothing to reconcile."
                        else
                            "Drift detected; run `fsgg-sdd upgrade` to reconcile each step interactively (or `fsgg-sdd upgrade --yes` non-interactively)."
                      RequiredArtifacts = []
                      BlockingDiagnosticIds = [] }
            elif request.Command = Upgrade then
                // Non-blocking upgrade outcomes (blocked ones are handled above): residual drift
                // → re-run upgrade; applied → confirm with doctor; no-op → already coherent.
                match reportOutcome with
                | CommandOutcome.SucceededWithWarnings ->
                    Some
                        { ActionId = "upgrade.residualDrift"
                          Command = Some Upgrade
                          WorkId = None
                          Reason =
                            "Some reconciliation steps were skipped; residual drift remains. Re-run `fsgg-sdd upgrade` and confirm them to finish."
                          RequiredArtifacts = []
                          BlockingDiagnosticIds = [] }
                | CommandOutcome.Succeeded ->
                    Some
                        { ActionId = "upgrade.next.doctor"
                          Command = Some Doctor
                          WorkId = None
                          Reason =
                            "Reconciliation applied; run `fsgg-sdd doctor` to confirm coherence (a CLI self-update takes effect on the next invocation)."
                          RequiredArtifacts = []
                          BlockingDiagnosticIds = [] }
                | _ ->
                    Some
                        { ActionId = "upgrade.alreadyCoherent"
                          Command = None
                          WorkId = None
                          Reason = "Already coherent — nothing to reconcile."
                          RequiredArtifacts = []
                          BlockingDiagnosticIds = [] }
            else
                match nextLifecycleCommand request.Command with
                | Some command ->
                    let requiredArtifacts =
                        match request.Command, request.WorkId with
                        | Charter, Some workId -> [ $"work/{workId}/charter.md" ]
                        | Specify, Some workId -> [ $"work/{workId}/charter.md"; $"work/{workId}/spec.md" ]
                        | Clarify, Some workId -> [ $"work/{workId}/spec.md"; $"work/{workId}/clarifications.md" ]
                        | Checklist, Some workId ->
                            [ $"work/{workId}/spec.md"
                              $"work/{workId}/clarifications.md"
                              $"work/{workId}/checklist.md" ]
                        | Plan, Some workId -> [ $"work/{workId}/plan.md" ]
                        | Tasks, Some workId -> [ $"work/{workId}/tasks.yml" ]
                        | _ -> []

                    Some
                        { ActionId = "nextLifecycleCommand"
                          Command = Some command
                          WorkId = request.WorkId
                          Reason = $"Command '{commandName request.Command}' completed."
                          RequiredArtifacts = requiredArtifacts
                          BlockingDiagnosticIds = [] }
                | None -> None
