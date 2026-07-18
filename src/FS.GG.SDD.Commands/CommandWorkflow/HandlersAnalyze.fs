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
open FS.GG.SDD.Commands.Internal.ChecklistAuthoring
open FS.GG.SDD.Commands.Internal.PlanAuthoring
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites
open FS.GG.SDD.Commands.Internal.HandlersEarly

module internal HandlersAnalyze =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    // Feature 105, Phase 3 (ADR-0004 D2/D3). The second-wave reads the framework-reference check
    // needs: the committed capture for each `framework:` reference the plan cites, keyed by the
    // resolved `<PackageId>@<version>` (explicit `@version`, else the CPM pin read in the first
    // wave). Mirrors `citedArtifactReadEffects` — the paths only become known once `plan.md` has
    // been read, so they join the existing second wave. A missing capture reads to `Snapshot = None`,
    // which the oracle treats as "unavailable" (advisory), so no probe is needed.
    let frameworkCaptureReadEffects workId (model: CommandModel) : CommandEffect list =
        match snapshot (planPath workId) model with
        | None -> []
        | Some planSnapshot ->
            match parsePlanFacts planSnapshot with
            | Error _ -> []
            | Ok planFacts ->
                // Reads already planned drop out, so once the captures are in this returns `[]` and
                // the second-wave loop proceeds to `computeAnalyzePlan` (mirrors
                // `citedArtifactReadEffects`). Without this the candidate list never empties.
                let alreadyPlanned = plannedReadPaths model |> Set.ofList

                let pinTexts =
                    [ "Directory.Packages.local.props"; "Directory.Packages.props" ]
                    |> List.choose (fun path -> snapshot path model |> Option.map (fun snap -> snap.Text))

                planFacts.FrameworkApiReferences
                |> List.choose (fun reference ->
                    resolveFrameworkVersion reference pinTexts
                    |> Option.map (fun version ->
                        DependencySurface.capturePath
                            DependencySurface.defaultBaselineRoot
                            reference.PackageId
                            version))
                |> List.map normalizeRelativePath
                |> List.filter (fun path -> not (Set.contains path alreadyPlanned))
                |> List.distinct
                |> List.map ReadFile

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

                // Feature 105, Phase 3 (ADR-0004 D3). Resolve each plan `framework:` reference against
                // the pinned package's COMMITTED captured surface (feature 105 Phase 2). The oracle is
                // bound here at the edge — it reads only the committed capture (already interpreted in
                // the second read wave) and the CPM pin — while the verdict rule stays pure. Fail-open:
                // no capture ⇒ advisory, never a false block (ADR-0002 / #266).
                let frameworkReferenceDiagnostics =
                    match planFacts with
                    | Some planFacts ->
                        let pinTexts =
                            [ "Directory.Packages.local.props"; "Directory.Packages.props" ]
                            |> List.choose (fun path -> snapshot path model |> Option.map (fun snap -> snap.Text))

                        let resolve (reference: FrameworkApiReference) =
                            let resolvedVersion = resolveFrameworkVersion reference pinTexts

                            let symbols =
                                resolvedVersion
                                |> Option.bind (fun version ->
                                    snapshot
                                        (DependencySurface.capturePath
                                            DependencySurface.defaultBaselineRoot
                                            reference.PackageId
                                            version)
                                        model)
                                |> Option.bind (fun snap ->
                                    match DependencySurface.tryParse snap.Text with
                                    | Ok capture -> Some(DependencySurface.symbolSet capture)
                                    | Error _ -> None)

                            (resolvedVersion |> Option.defaultValue "unresolved"), symbols

                        ViewGeneration.frameworkReferenceDiagnostics resolve (planPath workId) planFacts
                    | None -> []

                // FS.GG.SDD#351. `analyze` is the right and only place for this: it is the last gate
                // before implementation, it already holds every authored artifact, and one
                // `DiagnosticError` here means no `analysis.json` is written — so `evidence` refuses
                // (`evidence.analysisNotReady`), and `verify` and `ship` never run. A lifecycle walked
                // on untouched scaffold output therefore cannot reach `shipReady`, which is the
                // acceptance criterion, satisfied at a single point rather than re-litigated per stage.
                let unauthoredScaffoldDiagnostics =
                    match specFacts, clarificationFacts, checklistFacts, planText with
                    | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planText ->
                        match unauthoredPlanLines workId specFacts clarificationFacts checklistFacts planText with
                        | [] -> []
                        | ids -> [ unauthoredScaffoldContent (planPath workId) ids ]
                    | _ -> []

                let commandDiagnostics =
                    projectDiagnostics
                    @ unauthoredScaffoldDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    @ taskDiagnostics
                    @ dispositionDiagnostics
                    @ frameworkReferenceDiagnostics
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
