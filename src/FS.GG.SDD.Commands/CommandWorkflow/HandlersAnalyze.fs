namespace FS.GG.SDD.Commands.Internal

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
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites
open FS.GG.SDD.Commands.Internal.HandlersEarly

module internal HandlersAnalyze =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let computeAnalyzePlan model =
        let ((specification, clarification, checklist, plan, tasks, analysisSummary),
             diagnostics,
             generatedViews,
             effects) =
            runHandler model (None, None, None, None, None, None) (fun workId ->
                let projectDiagnostics = projectDiagnostics model
                let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
                let prereqs = resolvePrerequisites workId model

                let specificationDiagnostics, specText, specification, specFacts =
                    prereqs.SpecificationDiagnostics,
                    prereqs.SpecificationText,
                    prereqs.Specification,
                    prereqs.SpecificationFacts

                let clarificationDiagnostics, clarificationText, clarification, clarificationFacts =
                    prereqs.ClarificationDiagnostics,
                    prereqs.ClarificationText,
                    prereqs.Clarification,
                    prereqs.ClarificationFacts

                let checklistDiagnostics, checklistText, checklist, checklistFacts =
                    prereqs.ChecklistDiagnostics, prereqs.ChecklistText, prereqs.Checklist, prereqs.ChecklistFacts

                let planDiagnostics, planText, plan, planFacts =
                    prereqs.PlanDiagnostics, prereqs.PlanText, prereqs.Plan, prereqs.PlanFacts

                let taskDiagnostics, taskText, tasks, taskFacts =
                    prereqs.TaskDiagnostics, prereqs.TaskText, prereqs.Tasks, prereqs.TaskFacts

                let dispositionDiagnostics =
                    match specFacts, clarificationFacts, checklistFacts, planFacts, taskFacts with
                    | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts, Some taskFacts ->
                        missingDispositionDiagnostics
                            workId
                            specFacts
                            clarificationFacts
                            checklistFacts
                            planFacts
                            taskFacts
                    | _ -> []

                let analysisViewDiagnostics =
                    existingAnalysisDiagnostic workId model |> Option.toList

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    @ taskDiagnostics
                    @ dispositionDiagnostics
                    @ analysisViewDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, workModelView, workModelEffects =
                    match specText, clarificationText, checklistText, planText, taskText with
                    | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some specText)
                            (Some clarificationText)
                            (Some checklistText)
                            (Some planText)
                            (Some taskText)
                            None
                            commandDiagnostics
                            model
                    | _ -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                commandDiagnostics @ generatedDiagnostics,
                (fun hasBlocking diagnostics ->
                    let analysisSummary, analysisView, analysisEffects =
                        match
                            specText,
                            clarificationText,
                            checklistText,
                            planText,
                            taskText,
                            specFacts,
                            clarificationFacts,
                            checklistFacts,
                            planFacts,
                            taskFacts
                        with
                        | Some specText,
                          Some clarificationText,
                          Some checklistText,
                          Some planText,
                          Some taskText,
                          Some specFacts,
                          Some clarificationFacts,
                          Some checklistFacts,
                          Some planFacts,
                          Some taskFacts ->
                            let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model

                            let relationships =
                                analysisRelationships
                                    workId
                                    specFacts
                                    clarificationFacts
                                    checklistFacts
                                    planFacts
                                    taskFacts

                            let generatedViewsForAnalysis = [ workModelView ]

                            let summary, text, view =
                                analysisPlan
                                    workId
                                    specText
                                    clarificationText
                                    checklistText
                                    planText
                                    taskText
                                    workModelJson
                                    relationships
                                    diagnostics
                                    generatedViewsForAnalysis
                                    model

                            let effects =
                                if hasBlocking then
                                    []
                                else
                                    [ CreateDirectory(readinessDirectory workId)
                                      WriteFile(analysisPath workId, text, GeneratedView) ]

                            Some summary, Some view, effects
                        | _ -> None, None, []

                    let generatedViews = [ Some workModelView; analysisView ] |> List.choose id

                    (specification, clarification, checklist, plan, tasks, analysisSummary),
                    generatedViews,
                    workModelEffects,
                    analysisEffects))

        diagnostics, specification, clarification, checklist, plan, tasks, analysisSummary, generatedViews, effects
