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

module internal Prerequisites =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    // Single source of the monotonic lifecycle prerequisite cascade
    // (specification -> clarification -> checklist -> plan -> tasks). The cascade,
    // its ordered short-circuit, and its fact threading live here once; every
    // cascade-consuming handler reads the prefix it needs from this record instead
    // of re-rolling a nested match. Per-stage diagnostic lists are returned
    // unsorted and unconcatenated: concatenation order and the single
    // DiagnosticsModule.sort stay at each handler call site (resolver contract C-5).
    type PrerequisiteResolution =
        { SpecificationDiagnostics: Diagnostic list
          SpecificationText: string option
          Specification: SpecificationSummary option
          SpecificationFacts: SpecificationFacts option
          ClarificationDiagnostics: Diagnostic list
          ClarificationText: string option
          Clarification: ClarificationSummary option
          ClarificationFacts: ClarificationFacts option
          ChecklistDiagnostics: Diagnostic list
          ChecklistText: string option
          Checklist: ChecklistSummary option
          ChecklistFacts: ChecklistFacts option
          PlanDiagnostics: Diagnostic list
          PlanText: string option
          Plan: PlanSummary option
          PlanFacts: PlanFacts option
          TaskDiagnostics: Diagnostic list
          TaskText: string option
          Tasks: TasksSummary option
          TaskFacts: TaskFacts option }

    let resolvePrerequisites workId model : PrerequisiteResolution =
        let specificationDiagnostics, specText, specification, specFacts =
            specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

        let clarificationDiagnostics, clarificationText, clarification, clarificationFacts =
            clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

        let checklistDiagnostics, checklistText, checklist, checklistFacts =
            match specFacts, clarificationFacts with
            | Some specFacts, Some clarificationFacts ->
                checklistPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts model
            | _ -> [], None, None, None

        let planDiagnostics, planText, plan, planFacts =
            match specFacts, clarificationFacts, checklistFacts with
            | Some specFacts, Some clarificationFacts, Some checklistFacts ->
                planPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts model
            | _ -> [], None, None, None

        let taskDiagnostics, taskText, tasks, taskFacts =
            match specFacts, clarificationFacts, checklistFacts, planFacts with
            | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts ->
                tasksPrerequisiteDiagnosticsTextSummaryAndFacts
                    workId
                    specFacts
                    clarificationFacts
                    checklistFacts
                    planFacts
                    model
            | _ -> [], None, None, None

        { SpecificationDiagnostics = specificationDiagnostics
          SpecificationText = specText
          Specification = specification
          SpecificationFacts = specFacts
          ClarificationDiagnostics = clarificationDiagnostics
          ClarificationText = clarificationText
          Clarification = clarification
          ClarificationFacts = clarificationFacts
          ChecklistDiagnostics = checklistDiagnostics
          ChecklistText = checklistText
          Checklist = checklist
          ChecklistFacts = checklistFacts
          PlanDiagnostics = planDiagnostics
          PlanText = planText
          Plan = plan
          PlanFacts = planFacts
          TaskDiagnostics = taskDiagnostics
          TaskText = taskText
          Tasks = tasks
          TaskFacts = taskFacts }

    // Shared handler shell: owns the four steps identical across every handler —
    // the missing-WorkId guard (H-1), the final DiagnosticsModule.sort (H-3), the
    // single hasBlocking computation (H-2), and the not-blocking effect gate (H-4).
    // The handler's `body` returns its pre-sort command+generated diagnostics (in
    // call-site order) paired with a continuation that, given hasBlocking and the
    // sorted diagnostics, yields the stage-specific summaries, generated views,
    // write effects, generated-view effects, and blocked-seed effects. The
    // continuation lets handlers whose view content depends on hasBlocking
    // (verify/ship readiness, analyze write gating) read it from the single source
    // instead of recomputing it.
    //
    // H-4 CARVE-OUT (feature 089). `blockedSeedEffects` is the single, named exception
    // to "blocked ⇒ zero writes". It exists so a blocked `clarify` can seed the
    // `clarifications.md` skeleton the operator is meant to fill in, instead of leaving an
    // empty work directory and a diagnostic (FS-GG/FS.GG.SDD#174 §WD5). Exactly one handler
    // — `computeClarifyPlan` — ever returns a non-empty value for it; every other handler
    // reaches this shell through `runHandler` below, which supplies `[]`.
    //
    // Note what stays gated: on a blocking run `generatedEffects` are still discarded, so a
    // blocked command never writes a generated view (no `readiness/<id>/work-model.json`).
    // Only the seed escapes, and only for the one handler that asks. Do not widen this.
    let runHandlerWithBlockedSeed
        (model: CommandModel)
        (empty: 'summaries)
        (body:
            string
                -> Diagnostic list *
                (bool
                    -> Diagnostic list
                    -> 'summaries *
                    GeneratedViewState list *
                    CommandEffect list *
                    CommandEffect list *
                    CommandEffect list))
        =
        match model.Request.WorkId with
        | None -> empty, model.Diagnostics, [], []
        | Some workId ->
            let commandAndGeneratedDiagnostics, resume = body workId
            let diagnostics = commandAndGeneratedDiagnostics |> DiagnosticsModule.sort

            let hasBlocking =
                diagnostics
                |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let summaries, generatedViews, writeEffects, generatedEffects, blockedSeedEffects =
                resume hasBlocking diagnostics

            let effects =
                if hasBlocking then
                    blockedSeedEffects
                else
                    writeEffects @ generatedEffects

            summaries, diagnostics, generatedViews, effects

    // The ordinary shell: no handler may write anything on a blocking run (H-4).
    let runHandler
        (model: CommandModel)
        (empty: 'summaries)
        (body:
            string
                -> Diagnostic list *
                (bool
                    -> Diagnostic list
                    -> 'summaries * GeneratedViewState list * CommandEffect list * CommandEffect list))
        =
        runHandlerWithBlockedSeed model empty (fun workId ->
            let diagnostics, resume = body workId

            diagnostics,
            (fun hasBlocking sorted ->
                let summaries, generatedViews, writeEffects, generatedEffects =
                    resume hasBlocking sorted

                summaries, generatedViews, writeEffects, generatedEffects, []))
