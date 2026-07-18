namespace FS.GG.SDD.Commands

open System
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.Serialization
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.HandlersEarly
open FS.GG.SDD.Commands.Internal.HandlersAnalyze
open FS.GG.SDD.Commands.Internal.HandlersEvidence
open FS.GG.SDD.Commands.Internal.HandlersVerify
open FS.GG.SDD.Commands.Internal.HandlersShip
open FS.GG.SDD.Commands.Internal.HandlersAgents
open FS.GG.SDD.Commands.Internal.HandlersRefresh
open FS.GG.SDD.Commands.Internal.HandlersScaffold
open FS.GG.SDD.Commands.Internal.HandlersDoctor
open FS.GG.SDD.Commands.Internal.HandlersUpgrade
open FS.GG.SDD.Commands.Internal.HandlersLint
open FS.GG.SDD.Commands.Internal.HandlersSurface
open FS.GG.SDD.Commands.Internal.HandlersDependencySurface

module CommandWorkflow =
    // The per-stage summaries a lifecycle command's plan feeds into the command model.
    // Named fields replace the positional 12-tuple that was threaded through every arm of
    // `nextLifecycleEffects` (feature 062): each arm sets only its own stage's field(s) via
    // `{ emptyStagePlan with … }`, so a mis-assignment is a compile error rather than a
    // silent position swap. Module-internal (absent from CommandWorkflow.fsi).
    type StagePlan =
        { Diagnostics: Diagnostic list
          Specification: SpecificationSummary option
          Clarification: ClarificationSummary option
          Checklist: ChecklistSummary option
          Plan: PlanSummary option
          Tasks: TasksSummary option
          Analysis: AnalysisSummary option
          Evidence: EvidenceSummary option
          Verification: VerificationSummary option
          Ship: ShipSummary option
          GeneratedViews: GeneratedViewState list
          PlannedEffects: CommandEffect list }

    let emptyStagePlan =
        { Diagnostics = []
          Specification = None
          Clarification = None
          Checklist = None
          Plan = None
          Tasks = None
          Analysis = None
          Evidence = None
          Verification = None
          Ship = None
          GeneratedViews = []
          PlannedEffects = [] }

    let nextLifecycleEffects model =
        match model.Request.Explain, model.Request.Command with
        // `<stage> --explain` (feature 076, US3): once the single artifact read is in, run the
        // pure LintEngine over the stage's own artifact; advance no state, emit no write.
        | true, (Charter | Specify | Clarify | Checklist | Plan | Tasks | Evidence) ->
            if not (allPlannedReadsInterpreted model) then
                model, []
            else
                computeExplainLint model
        | _ ->

            match model.Request.Command, model.Request.WorkId with
            | (Charter | Specify | Clarify | Checklist | Plan | Tasks | Analyze | Evidence | Verify | Ship), Some workId when
                not (hasPlannedWrite model)
                ->
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    let candidateReads =
                        duplicateCandidateReadEffects workId model
                        // FS.GG.SDD#349: probe every artifact a satisfying evidence declaration cites.
                        // The paths only become known once `evidence.yml` has been read, so they join
                        // the existing second wave.
                        //
                        // `Evidence` and `Verify` only: they are the two stages that evaluate evidence
                        // (the gate + the `ED-`/`TD-` cascades). `Ship` re-reads none of it — it
                        // aggregates the blocking findings already recorded in `verify.json` — so
                        // probing there would read every cited artifact's bytes off disk for a verdict
                        // nothing consults.
                        @ (match model.Request.Command with
                           | Evidence
                           | Verify -> citedArtifactReadEffects workId model
                           | _ -> [])
                        // FS.GG.SDD#350: the `--from-tests` report. `Evidence` only — it is the stage
                        // that RECORDS the receipt. `Verify` re-reads the receipt's report through
                        // `citedArtifactReadEffects` above (the receipt's `source` is a cited path),
                        // so the deleted-report case is caught at the merge boundary without reading
                        // the report's bytes a second time here.
                        @ (match model.Request.Command with
                           | Evidence -> testReportReadEffects model
                           | _ -> [])
                        // FS.GG.SDD#550: the `--sync-observed-run` report. `Evidence` only — it is the
                        // stage that re-stamps the receipt. `Verify` re-reads the receipt's report through
                        // `citedArtifactReadEffects` (the receipt's `source` is a cited path), so a synced
                        // report gone missing is caught at the merge boundary without a second read here.
                        @ (match model.Request.Command with
                           | Evidence -> syncReportReadEffects model
                           | _ -> [])

                    match candidateReads with
                    | _ :: _ ->
                        let effects = appendNewEffects candidateReads model

                        { model with
                            PendingEffects = model.PendingEffects @ effects },
                        effects
                    | [] ->
                        let stagePlan =
                            match model.Request.Command with
                            | Charter ->
                                let diagnostics, specification, generatedViews, effects = computeCharterPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Specify ->
                                let diagnostics, specification, generatedViews, effects = computeSpecifyPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Clarify ->
                                let diagnostics, specification, clarification, generatedViews, effects =
                                    computeClarifyPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Checklist ->
                                let diagnostics, specification, clarification, checklist, generatedViews, effects =
                                    computeChecklistPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Plan ->
                                let diagnostics, specification, clarification, checklist, plan, generatedViews, effects =
                                    computePlanPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Tasks ->
                                let (diagnostics,
                                     specification,
                                     clarification,
                                     checklist,
                                     plan,
                                     tasks,
                                     generatedViews,
                                     effects) =
                                    computeTasksPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    Tasks = tasks
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Analyze ->
                                let (diagnostics,
                                     specification,
                                     clarification,
                                     checklist,
                                     plan,
                                     tasks,
                                     analysis,
                                     generatedViews,
                                     effects) =
                                    computeAnalyzePlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    Tasks = tasks
                                    Analysis = analysis
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Evidence ->
                                let (diagnostics,
                                     specification,
                                     clarification,
                                     checklist,
                                     plan,
                                     tasks,
                                     analysis,
                                     evidence,
                                     generatedViews,
                                     effects) =
                                    computeEvidencePlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    Tasks = tasks
                                    Analysis = analysis
                                    Evidence = evidence
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Verify ->
                                let (diagnostics,
                                     specification,
                                     clarification,
                                     checklist,
                                     plan,
                                     tasks,
                                     analysis,
                                     evidence,
                                     verification,
                                     generatedViews,
                                     effects) =
                                    computeVerifyPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    Tasks = tasks
                                    Analysis = analysis
                                    Evidence = evidence
                                    Verification = verification
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | Ship ->
                                let (diagnostics,
                                     specification,
                                     clarification,
                                     checklist,
                                     plan,
                                     tasks,
                                     analysis,
                                     evidence,
                                     verification,
                                     ship,
                                     generatedViews,
                                     effects) =
                                    computeShipPlan model

                                { emptyStagePlan with
                                    Diagnostics = diagnostics
                                    Specification = specification
                                    Clarification = clarification
                                    Checklist = checklist
                                    Plan = plan
                                    Tasks = tasks
                                    Analysis = analysis
                                    Evidence = evidence
                                    Verification = verification
                                    Ship = ship
                                    GeneratedViews = generatedViews
                                    PlannedEffects = effects }
                            | _ ->
                                { emptyStagePlan with
                                    Diagnostics = model.Diagnostics }

                        let effects = appendNewEffects stagePlan.PlannedEffects model

                        let plannedModel =
                            { model with
                                PendingEffects = model.PendingEffects @ effects
                                Diagnostics = stagePlan.Diagnostics
                                Specification = stagePlan.Specification
                                Clarification = stagePlan.Clarification
                                Checklist = stagePlan.Checklist
                                Plan = stagePlan.Plan
                                Tasks = stagePlan.Tasks
                                Analysis = stagePlan.Analysis
                                Evidence = stagePlan.Evidence
                                Verification = stagePlan.Verification
                                Ship = stagePlan.Ship
                                GeneratedViews = stagePlan.GeneratedViews }

                        plannedModel, effects
            | Agents, Some workId when not (hasPlannedWrite model) ->
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    let candidateReads =
                        appendNewEffects
                            ((duplicateCandidateReadEffects workId model)
                             @ (agentGuidanceCandidateReadEffects workId model))
                            model

                    match candidateReads with
                    | _ :: _ ->
                        { model with
                            PendingEffects = model.PendingEffects @ candidateReads },
                        candidateReads
                    | [] ->
                        let diagnostics, agentGuidance, generatedViews, plannedEffects =
                            computeAgentsPlan model

                        let effects = appendNewEffects plannedEffects model

                        let plannedModel =
                            { model with
                                PendingEffects = model.PendingEffects @ effects
                                Diagnostics = diagnostics
                                AgentGuidance = agentGuidance
                                GeneratedViews = generatedViews }

                        plannedModel, effects
            | Refresh, Some workId when not (hasPlannedWrite model) ->
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    let candidateReads =
                        appendNewEffects
                            ((duplicateCandidateReadEffects workId model)
                             @ (agentGuidanceCandidateReadEffects workId model)
                             // 056: provider-skill bodies for the re-mirror step (two-phase).
                             @ (providerSkillMirrorReads model))
                            model

                    match candidateReads with
                    | _ :: _ ->
                        { model with
                            PendingEffects = model.PendingEffects @ candidateReads },
                        candidateReads
                    | [] ->
                        let diagnostics, refresh, generatedViews, plannedEffects = computeRefreshPlan model
                        // 056: re-mirror the union (re-seed all three roots + fan provider skills
                        // into .claude/.codex) to currency on every refresh, no-clobber (FR-009).
                        let effects =
                            appendNewEffects (plannedEffects @ skillFanoutRefreshEffects model) model

                        let plannedModel =
                            { model with
                                PendingEffects = model.PendingEffects @ effects
                                Diagnostics = diagnostics
                                Refresh = refresh
                                GeneratedViews = generatedViews }

                        plannedModel, effects
            | Scaffold, _ ->
                // Scaffold has its own multi-stage driver (resolve → invoke → diff →
                // provenance); it does not use the generic write-once guard above.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeScaffoldNext model
            | Doctor, _ ->
                // Read-only drift projection (feature 053, US1): resolve the shared drift once
                // the snapshotted reads are in; emit no mutating effect.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeDoctorNext model
            | Upgrade, _ ->
                // The reconciliation verb (feature 053, US2–US4): its own staged driver
                // (resolve drift → per-step Confirm → apply → finalize), re-derived from the log.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeUpgradeNext model
            | Lint, _ ->
                // Read-only pre-flight (feature 076, US1): once the single artifact read is in,
                // run the pure LintEngine and record the summary; emit no mutating effect.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeLintNext model
            | Surface, _ ->
                // API-surface baseline drift (feature 086): once the enumerated roots and the gated
                // per-file bodies are in, compute the drift picture; `--update` also emits the
                // baseline write effects, `--check` emits none.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeSurfaceNext model
            | DependencySurface, _ ->
                // Dependency-surface capture drift (feature 105, Phase 2): once the enumerated root
                // is in, the handler gates its own per-target reads (the committed capture + the
                // real restored surface), then computes drift; `--update` emits capture writes.
                if not (allPlannedReadsInterpreted model) then
                    model, []
                else
                    computeDependencySurfaceNext model
            | _ -> model, []

    let init (request: CommandRequest) =
        let request =
            { request with
                ProjectRoot = normalizeRoot request.ProjectRoot }

        let diagnostics, effects = plan request

        let model: CommandModel =
            { Request = request
              PendingEffects = effects
              InterpretedEffects = []
              Diagnostics = diagnostics
              Specification = None
              Clarification = None
              Checklist = None
              Plan = None
              Tasks = None
              Analysis = None
              Evidence = None
              Verification = None
              Ship = None
              AgentGuidance = None
              Refresh = None
              Scaffold = None
              Doctor = None
              Upgrade = None
              Lint = None
              Surface = None
              DependencySurface = None
              GeneratedViews = []
              Report = None }

        model, effects

    let update (msg: CommandMsg) (model: CommandModel) =
        match msg with
        | EffectInterpreted result ->
            let next =
                { model with
                    InterpretedEffects = model.InterpretedEffects @ [ result ] }

            nextLifecycleEffects next
        | BuildReport ->
            let report = CommandReports.buildReport model
            { model with Report = Some report }, ([]: CommandEffect list)
