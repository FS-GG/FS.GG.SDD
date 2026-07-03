namespace FS.GG.SDD.Commands

open System
open System.IO
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
open FS.GG.SDD.Commands.Internal

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
        match model.Request.Command, model.Request.WorkId with
        | (Charter | Specify | Clarify | Checklist | Plan | Tasks | Analyze | Evidence | Verify | Ship), Some workId when not (hasPlannedWrite model) ->
            if not (allPlannedReadsInterpreted model) then
                model, []
            else
                let candidateReads = duplicateCandidateReadEffects workId model

                match candidateReads with
                | _ :: _ ->
                    let effects = appendNewEffects candidateReads model
                    { model with PendingEffects = model.PendingEffects @ effects }, effects
                | [] ->
                    let stagePlan =
                        match model.Request.Command with
                        | Charter ->
                            let diagnostics, specification, generatedViews, effects = computeCharterPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Specify ->
                            let diagnostics, specification, generatedViews, effects = computeSpecifyPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Clarify ->
                            let diagnostics, specification, clarification, generatedViews, effects = computeClarifyPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Checklist ->
                            let diagnostics, specification, clarification, checklist, generatedViews, effects = computeChecklistPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Plan ->
                            let diagnostics, specification, clarification, checklist, plan, generatedViews, effects = computePlanPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Tasks ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, generatedViews, effects = computeTasksPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; Tasks = tasks; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Analyze ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, generatedViews, effects = computeAnalyzePlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; Tasks = tasks; Analysis = analysis; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Evidence ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, generatedViews, effects = computeEvidencePlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; Tasks = tasks; Analysis = analysis; Evidence = evidence; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Verify ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, verification, generatedViews, effects = computeVerifyPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; Tasks = tasks; Analysis = analysis; Evidence = evidence; Verification = verification; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | Ship ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, verification, ship, generatedViews, effects = computeShipPlan model
                            { emptyStagePlan with Diagnostics = diagnostics; Specification = specification; Clarification = clarification; Checklist = checklist; Plan = plan; Tasks = tasks; Analysis = analysis; Evidence = evidence; Verification = verification; Ship = ship; GeneratedViews = generatedViews; PlannedEffects = effects }
                        | _ -> { emptyStagePlan with Diagnostics = model.Diagnostics }

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
                    appendNewEffects ((duplicateCandidateReadEffects workId model) @ (agentGuidanceCandidateReadEffects workId model)) model

                match candidateReads with
                | _ :: _ ->
                    { model with PendingEffects = model.PendingEffects @ candidateReads }, candidateReads
                | [] ->
                    let diagnostics, agentGuidance, generatedViews, plannedEffects = computeAgentsPlan model
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
                    { model with PendingEffects = model.PendingEffects @ candidateReads }, candidateReads
                | [] ->
                    let diagnostics, refresh, generatedViews, plannedEffects = computeRefreshPlan model
                    // 056: re-mirror the union (re-seed all three roots + fan provider skills
                    // into .claude/.codex) to currency on every refresh, no-clobber (FR-009).
                    let effects = appendNewEffects (plannedEffects @ skillFanoutRefreshEffects model) model

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
            if not (allPlannedReadsInterpreted model) then model, []
            else computeScaffoldNext model
        | Doctor, _ ->
            // Read-only drift projection (feature 053, US1): resolve the shared drift once
            // the snapshotted reads are in; emit no mutating effect.
            if not (allPlannedReadsInterpreted model) then model, []
            else computeDoctorNext model
        | Upgrade, _ ->
            // The reconciliation verb (feature 053, US2–US4): its own staged driver
            // (resolve drift → per-step Confirm → apply → finalize), re-derived from the log.
            if not (allPlannedReadsInterpreted model) then model, []
            else computeUpgradeNext model
        | _ -> model, []

    let init (request: CommandRequest) =
        let request = { request with ProjectRoot = normalizeRoot request.ProjectRoot }
        let diagnostics, effects = plan request

        let model : CommandModel =
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
            { model with Report = Some report }, ([] : CommandEffect list)
