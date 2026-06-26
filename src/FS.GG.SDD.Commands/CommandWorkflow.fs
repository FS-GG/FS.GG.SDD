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
                    let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, verification, ship, generatedViews, plannedEffects =
                        match model.Request.Command with
                        | Charter ->
                            let diagnostics, specification, generatedViews, effects = computeCharterPlan model
                            diagnostics, specification, None, None, None, None, None, None, None, None, generatedViews, effects
                        | Specify ->
                            let diagnostics, specification, generatedViews, effects = computeSpecifyPlan model
                            diagnostics, specification, None, None, None, None, None, None, None, None, generatedViews, effects
                        | Clarify ->
                            let diagnostics, specification, clarification, generatedViews, effects = computeClarifyPlan model
                            diagnostics, specification, clarification, None, None, None, None, None, None, None, generatedViews, effects
                        | Checklist ->
                            let diagnostics, specification, clarification, checklist, generatedViews, effects = computeChecklistPlan model
                            diagnostics, specification, clarification, checklist, None, None, None, None, None, None, generatedViews, effects
                        | Plan ->
                            let diagnostics, specification, clarification, checklist, plan, generatedViews, effects = computePlanPlan model
                            diagnostics, specification, clarification, checklist, plan, None, None, None, None, None, generatedViews, effects
                        | Tasks ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, generatedViews, effects = computeTasksPlan model
                            diagnostics, specification, clarification, checklist, plan, tasks, None, None, None, None, generatedViews, effects
                        | Analyze ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, generatedViews, effects = computeAnalyzePlan model
                            diagnostics, specification, clarification, checklist, plan, tasks, analysis, None, None, None, generatedViews, effects
                        | Evidence ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, generatedViews, effects = computeEvidencePlan model
                            diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, None, None, generatedViews, effects
                        | Verify ->
                            let diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, verification, generatedViews, effects = computeVerifyPlan model
                            diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidence, verification, None, generatedViews, effects
                        | Ship ->
                            computeShipPlan model
                        | _ -> model.Diagnostics, None, None, None, None, None, None, None, None, None, [], []

                    let effects = appendNewEffects plannedEffects model

                    let plannedModel =
                        { model with
                            PendingEffects = model.PendingEffects @ effects
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
                            GeneratedViews = generatedViews }

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
                    appendNewEffects ((duplicateCandidateReadEffects workId model) @ (agentGuidanceCandidateReadEffects workId model)) model

                match candidateReads with
                | _ :: _ ->
                    { model with PendingEffects = model.PendingEffects @ candidateReads }, candidateReads
                | [] ->
                    let diagnostics, refresh, generatedViews, plannedEffects = computeRefreshPlan model
                    let effects = appendNewEffects plannedEffects model

                    let plannedModel =
                        { model with
                            PendingEffects = model.PendingEffects @ effects
                            Diagnostics = diagnostics
                            Refresh = refresh
                            GeneratedViews = generatedViews }

                    plannedModel, effects
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
        | LoadProject
        | LoadWorkItem
        | ApplyUserIntent
        | PlanGeneratedViewRefresh -> model, ([] : CommandEffect list)
