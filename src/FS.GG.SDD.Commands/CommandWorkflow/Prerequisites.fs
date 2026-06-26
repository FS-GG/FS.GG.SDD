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

[<AutoOpen>]
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
        let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
        let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

        let checklistDiagnostics, checklistText, checklist, checklistFacts =
            match specFacts, clarificationFacts with
            | Some specFacts, Some clarificationFacts -> checklistPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts model
            | _ -> [], None, None, None

        let planDiagnostics, planText, plan, planFacts =
            match specFacts, clarificationFacts, checklistFacts with
            | Some specFacts, Some clarificationFacts, Some checklistFacts -> planPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts model
            | _ -> [], None, None, None

        let taskDiagnostics, taskText, tasks, taskFacts =
            match specFacts, clarificationFacts, checklistFacts, planFacts with
            | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts -> tasksPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts planFacts model
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
    // write effects, and generated-view effects. The continuation lets handlers
    // whose view content depends on hasBlocking (verify/ship readiness, analyze
    // write gating) read it from the single source instead of recomputing it.
    let runHandler (model: CommandModel) (empty: 'summaries) (body: string -> Diagnostic list * (bool -> Diagnostic list -> 'summaries * GeneratedViewState list * CommandEffect list * CommandEffect list)) =
        match model.Request.WorkId with
        | None -> empty, model.Diagnostics, [], []
        | Some workId ->
            let commandAndGeneratedDiagnostics, resume = body workId
            let diagnostics = commandAndGeneratedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            let summaries, generatedViews, writeEffects, generatedEffects = resume hasBlocking diagnostics
            let effects = if hasBlocking then [] else writeEffects @ generatedEffects
            summaries, diagnostics, generatedViews, effects

