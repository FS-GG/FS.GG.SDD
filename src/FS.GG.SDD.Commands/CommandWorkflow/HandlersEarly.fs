namespace FS.GG.SDD.Commands.Internal

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
open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring
open FS.GG.SDD.Commands.Internal.CharterAuthoring
open FS.GG.SDD.Commands.Internal.SpecifyAuthoring
open FS.GG.SDD.Commands.Internal.ClarifyAuthoring
open FS.GG.SDD.Commands.Internal.ChecklistAuthoring
open FS.GG.SDD.Commands.Internal.PlanAuthoring
open FS.GG.SDD.Commands.Internal.TaskGraphAuthoring
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites

module internal HandlersEarly =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let computeCharterPlan model =
        let specification, diagnostics, generatedViews, effects =
            runHandler model None (fun workId ->
                let projectDiagnostics = projectDiagnostics model
                let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model

                let charterDiagnostics, charterText =
                    charterDiagnosticsAndText model.Request workId model

                let commandDiagnostics =
                    projectDiagnostics @ duplicateDiagnostics @ charterDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    generatedViewPlan
                        model.Request
                        workId
                        (Some charterText)
                        None
                        None
                        None
                        None
                        None
                        None
                        commandDiagnostics
                        model

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ -> None, [ generatedView ], charterWriteEffects workId charterText, generatedEffects))

        diagnostics, specification, generatedViews, effects

    let computeSpecifyPlan model =
        let specification, diagnostics, generatedViews, effects =
            runHandler model None (fun workId ->
                let projectDiagnostics = projectDiagnostics model
                let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model

                let charterDiagnostics, charterText =
                    charterPrerequisiteDiagnosticsAndText workId model

                let specificationDiagnostics, specText, specification =
                    specificationDiagnosticsTextAndSummary model.Request workId model

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ charterDiagnostics
                    @ specificationDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match charterText with
                    | Some text ->
                        generatedViewPlan
                            model.Request
                            workId
                            (Some text)
                            specText
                            None
                            None
                            None
                            None
                            None
                            commandDiagnostics
                            model
                    | None -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let specificationEffects =
                    match specText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(specPath workId, text, HybridArtifact MergePolicies.specification) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ -> specification, [ generatedView ], specificationEffects, generatedEffects))

        diagnostics, specification, generatedViews, effects

    let computeClarifyPlan model =
        let (specification, clarification), diagnostics, generatedViews, effects =
            // The one handler that uses the H-4 carve-out: a blocked `clarify` seeds the
            // `clarifications.md` skeleton rather than leaving an empty work directory (089 §WD5).
            runHandlerWithBlockedSeed model (None, None) (fun workId ->
                let projectDiagnostics = projectDiagnostics model
                let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
                let prereqs = resolvePrerequisites workId model

                let specificationDiagnostics, specText, specification, specFacts =
                    prereqs.SpecificationDiagnostics,
                    prereqs.SpecificationText,
                    prereqs.Specification,
                    prereqs.SpecificationFacts

                let clarificationDiagnostics, clarificationText, clarification, clarificationSeedText =
                    match specFacts with
                    // A specification that failed to parse yields no questions to seed (FR-012).
                    | None -> [], None, None, None
                    | Some facts -> clarificationDiagnosticsTextAndSummary model.Request workId facts model

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match specText with
                    | Some text ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some text)
                            clarificationText
                            None
                            None
                            None
                            None
                            commandDiagnostics
                            model
                    | None -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let clarificationEffects =
                    match clarificationText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(clarificationPath workId, text, HybridArtifact MergePolicies.clarifications) ]
                    | None -> []

                // Rides the blocked-seed channel, so it survives the H-4 gate — and nothing else
                // does: `generatedEffects` stay gated, so a blocked clarify writes no work model.
                // Exactly one effect: the interpreter's WriteFile already creates parent
                // directories, so a CreateDirectory here would only add a `noChange` directory
                // entry to `changedArtifacts` (FR-010 pins the blocked run at one changed artifact).
                let blockedSeedEffects =
                    match clarificationSeedText with
                    | Some text ->
                        [ WriteFile(clarificationPath workId, text, HybridArtifact MergePolicies.clarifications) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ ->
                    (specification, clarification),
                    [ generatedView ],
                    clarificationEffects,
                    generatedEffects,
                    blockedSeedEffects))

        diagnostics, specification, clarification, generatedViews, effects

    let computeChecklistPlan model =
        let (specification, clarification, checklist), diagnostics, generatedViews, effects =
            runHandler model (None, None, None) (fun workId ->
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

                let checklistDiagnostics, checklistText, checklist =
                    match specText, clarificationText, specFacts, clarificationFacts with
                    | Some specText, Some clarificationText, Some specFacts, Some clarificationFacts ->
                        checklistDiagnosticsTextAndSummary
                            model.Request
                            workId
                            specText
                            clarificationText
                            specFacts
                            clarificationFacts
                            model
                    | _ -> [], None, None

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match specText, clarificationText with
                    | Some specText, Some clarificationText ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some specText)
                            (Some clarificationText)
                            checklistText
                            None
                            None
                            None
                            commandDiagnostics
                            model
                    | _ -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let checklistEffects =
                    match checklistText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(checklistPath workId, text, HybridArtifact MergePolicies.checklist) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ ->
                    (specification, clarification, checklist), [ generatedView ], checklistEffects, generatedEffects))

        diagnostics, specification, clarification, checklist, generatedViews, effects

    let computePlanPlan model =
        let (specification, clarification, checklist, plan), diagnostics, generatedViews, effects =
            runHandler model (None, None, None, None) (fun workId ->
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

                let planDiagnostics, planText, plan =
                    match
                        specText, clarificationText, checklistText, specFacts, clarificationFacts, checklistFacts
                    with
                    | Some specText,
                      Some clarificationText,
                      Some checklistText,
                      Some specFacts,
                      Some clarificationFacts,
                      Some checklistFacts ->
                        planDiagnosticsTextAndSummary
                            model.Request
                            workId
                            specText
                            clarificationText
                            checklistText
                            specFacts
                            clarificationFacts
                            checklistFacts
                            model
                    | _ -> [], None, None

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match specText, clarificationText, checklistText with
                    | Some specText, Some clarificationText, Some checklistText ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some specText)
                            (Some clarificationText)
                            (Some checklistText)
                            planText
                            None
                            None
                            commandDiagnostics
                            model
                    | _ -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let planEffects =
                    match planText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(planPath workId, text, HybridArtifact MergePolicies.plan) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ ->
                    (specification, clarification, checklist, plan), [ generatedView ], planEffects, generatedEffects))

        diagnostics, specification, clarification, checklist, plan, generatedViews, effects

    let computeTasksPlan model =
        let (specification, clarification, checklist, plan, tasks), diagnostics, generatedViews, effects =
            runHandler model (None, None, None, None, None) (fun workId ->
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

                let taskDiagnostics, taskText, tasks =
                    match
                        specText,
                        clarificationText,
                        checklistText,
                        planText,
                        specFacts,
                        clarificationFacts,
                        checklistFacts,
                        planFacts
                    with
                    | Some specText,
                      Some clarificationText,
                      Some checklistText,
                      Some planText,
                      Some specFacts,
                      Some clarificationFacts,
                      Some checklistFacts,
                      Some planFacts ->
                        tasksDiagnosticsTextAndSummary
                            model.Request
                            workId
                            specText
                            clarificationText
                            checklistText
                            planText
                            specFacts
                            clarificationFacts
                            checklistFacts
                            planFacts
                            model
                    | _ -> [], None, None

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    @ taskDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match specText, clarificationText, checklistText, planText with
                    | Some specText, Some clarificationText, Some checklistText, Some planText ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some specText)
                            (Some clarificationText)
                            (Some checklistText)
                            (Some planText)
                            taskText
                            None
                            commandDiagnostics
                            model
                    | _ -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let taskEffects =
                    match taskText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(tasksPath workId, text, HybridArtifact MergePolicies.tasks) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ ->
                    (specification, clarification, checklist, plan, tasks),
                    [ generatedView ],
                    taskEffects,
                    generatedEffects))

        diagnostics, specification, clarification, checklist, plan, tasks, generatedViews, effects

    let workModelJsonFromGeneratedEffects workId effects model =
        effects
        |> List.tryPick (function
            | WriteFile(path, text, GeneratedView) when normalizeRelativePath path = workModelPath workId -> Some text
            | _ -> None)
        |> Option.orElseWith (fun () -> snapshot (workModelPath workId) model |> Option.map _.Text)
