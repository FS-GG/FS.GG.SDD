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
module internal Foundation =
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

    let normalizeRoot (path: string) =
        if String.IsNullOrWhiteSpace path then "." else path.Trim()

    let normalizeRelativePath (path: string) =
        (if String.IsNullOrEmpty path then "" else path.Trim().Replace('\\', '/')).TrimStart('/')

    let projectIdFromRoot (root: string) =
        let name = DirectoryInfo(normalizeRoot root).Name
        if String.IsNullOrWhiteSpace name then "sdd-project" else name.ToLowerInvariant()

    let projectConfigText (projectId: string) =
        $"""schemaVersion: 1
project:
  id: {projectId}
  defaultWorkRoot: work
sdd:
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml
"""

    let sddConfigText =
        """schemaVersion: 1
lifecycle:
  stages: [charter, specify, clarify, checklist, plan, tasks, analyze]
artifacts:
  workRoot: work
  readinessRoot: readiness
generatedViews:
  requireSourceDigests: true
  requireGeneratorVersion: true
  staleBehavior: diagnostic
"""

    let agentsConfigText =
        """schemaVersion: 1
agents:
  - id: claude
    guidancePath: CLAUDE.md
    generatedRoot: readiness/{workId}/agent-commands/claude
  - id: codex
    guidancePath: AGENTS.md
    generatedRoot: readiness/{workId}/agent-commands/codex
sourceModel:
  workModel: readiness/{workId}/work-model.json
policy:
  generatedGuidanceIsAuthority: false
  requireEquivalentClaudeAndCodexBehavior: true
"""

    let agentGuidance (name: string) =
        $"""# {name} SDD guidance

This file is an SDD lifecycle guidance target. Generated agent guidance is a
projection over `.fsgg/agents.yml` and readiness data; it is not a second source
of truth.
"""

    let initEffects (request: CommandRequest) =
        let projectId = projectIdFromRoot request.ProjectRoot

        [ CreateDirectory ".fsgg"
          CreateDirectory "work"
          CreateDirectory "readiness"
          WriteFile(".fsgg/project.yml", projectConfigText projectId, StructuredSource)
          WriteFile(".fsgg/sdd.yml", sddConfigText, StructuredSource)
          WriteFile(".fsgg/agents.yml", agentsConfigText, StructuredSource)
          WriteFile("AGENTS.md", agentGuidance "Codex", AgentGuidanceTarget)
          WriteFile("CLAUDE.md", agentGuidance "Claude", AgentGuidanceTarget) ]

    let charterPath workId = $"work/{workId}/charter.md"
    let specPath workId = $"work/{workId}/spec.md"
    let clarificationPath workId = $"work/{workId}/clarifications.md"
    let checklistPath workId = $"work/{workId}/checklist.md"
    let planPath workId = $"work/{workId}/plan.md"
    let tasksPath workId = $"work/{workId}/tasks.yml"
    let evidencePath workId = $"work/{workId}/evidence.yml"
    let workModelPath workId = GenerationManifestModule.expectedWorkModelOutputPath workId
    let analysisPath workId = $"readiness/{workId}/analysis.json"
    let verifyPath workId = $"readiness/{workId}/verify.json"
    let shipPath workId = $"readiness/{workId}/ship.json"
    let readinessDirectory workId = $"readiness/{workId}"

    let charterReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let clarifyReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let checklistReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let planReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let tasksReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(charterPath workId)
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let analyzeReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(workModelPath workId)
          ReadFile(analysisPath workId)
          EnumerateDirectory "work" ]

    let evidenceReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(workModelPath workId)
          ReadFile(analysisPath workId)
          ReadFile(evidencePath workId)
          EnumerateDirectory "work" ]

    let verifyReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          EnumerateDirectory "work" ]

    let shipReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          // Optional Governance config: presence-only detection for the handoff (FR-011).
          ReadFile ".fsgg/policy.yml"
          ReadFile ".fsgg/capabilities.yml"
          ReadFile ".fsgg/tooling.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          ReadFile(shipPath workId)
          EnumerateDirectory "work" ]

    let agentsReadEffects workId =
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          ReadFile(workModelPath workId)
          EnumerateDirectory "work" ]

    let refreshReadEffects workId =
        // NOTE: charter.md is intentionally not read. The reused analyze/verify/ship
        // generators do not read charter standalone, so reading it here would make
        // refresh regenerate a different work model than the lifecycle, breaking
        // idempotency. Refresh never writes charter regardless.
        [ ReadFile ".fsgg/project.yml"
          ReadFile ".fsgg/sdd.yml"
          ReadFile ".fsgg/agents.yml"
          // Optional Governance config: presence-only detection for the handoff (FR-011).
          ReadFile ".fsgg/policy.yml"
          ReadFile ".fsgg/capabilities.yml"
          ReadFile ".fsgg/tooling.yml"
          ReadFile(specPath workId)
          ReadFile(clarificationPath workId)
          ReadFile(checklistPath workId)
          ReadFile(planPath workId)
          ReadFile(tasksPath workId)
          ReadFile(evidencePath workId)
          ReadFile(analysisPath workId)
          ReadFile(workModelPath workId)
          ReadFile(verifyPath workId)
          ReadFile(shipPath workId)
          // Scaffold provenance: provider-produced paths are excluded from refresh.
          ReadFile ".fsgg/scaffold-provenance.json"
          ReadFile(GenerationManifestModule.expectedGovernanceHandoffOutputPath workId)
          ReadFile(GenerationManifestModule.expectedSummaryOutputPath workId)
          EnumerateDirectory "work" ]

    let scaffoldReadEffects =
        // Provider registry + a before-snapshot of the target root (for the produced
        // diff and the non-empty-target collision guard).
        [ ReadFile ".fsgg/providers.yml"
          EnumerateDirectory "" ]

    let workIdDiagnostics (request: CommandRequest) =
        match request.Command, request.WorkId with
        | Init, _ -> []
        // Scaffold is cross-cutting and operates on --root, not a work item.
        | Scaffold, _ -> []
        | _, None -> [ missingWorkId request.Command ]
        | _, Some value ->
            match IdentifiersModule.createWorkId value with
            | Ok _ -> []
            | Error _ -> [ malformedWorkId value ]

    let plan (request: CommandRequest) =
        let diagnostics = workIdDiagnostics request

        if not (List.isEmpty diagnostics) then
            diagnostics, []
        else
            match request.Command, request.WorkId with
            | Init, _ -> [], initEffects request
            | Charter, Some workId
            | Specify, Some workId -> [], charterReadEffects workId
            | Clarify, Some workId -> [], clarifyReadEffects workId
            | Checklist, Some workId -> [], checklistReadEffects workId
            | Plan, Some workId -> [], planReadEffects workId
            | Tasks, Some workId -> [], tasksReadEffects workId
            | Analyze, Some workId -> [], analyzeReadEffects workId
            | Evidence, Some workId -> [], evidenceReadEffects workId
            | Verify, Some workId -> [], verifyReadEffects workId
            | Ship, Some workId -> [], shipReadEffects workId
            | Agents, Some workId -> [], agentsReadEffects workId
            | Refresh, Some workId -> [], refreshReadEffects workId
            | Scaffold, _ -> [], scaffoldReadEffects
            | command, _ -> [ unsupportedCommand command ], []

    let effectKey effect =
        match effect with
        | ReadFile path -> "read:" + normalizeRelativePath path
        | EnumerateDirectory path -> "enumerate:" + normalizeRelativePath path
        | CreateDirectory path -> "mkdir:" + normalizeRelativePath path
        | WriteFile(path, _, kind) -> $"write:{normalizeRelativePath path}:{writeKindValue kind}"
        | RunProcess(command, args, workingDir) ->
            let renderedArgs = String.concat " " args
            $"run:{command} {renderedArgs}@{normalizeRelativePath workingDir}"
        | EmitStdout text -> "stdout:" + text
        | EmitStderr text -> "stderr:" + text
        | SetExitCode code -> "exit:" + string code

    let readEffectKey path = "read:" + normalizeRelativePath path

    let hasPlanned key model =
        model.PendingEffects |> List.exists (fun effect -> effectKey effect = key)

    let hasInterpreted key model =
        model.InterpretedEffects |> List.exists (fun result -> effectKey result.Effect = key)

    let hasPlannedWrite model =
        model.PendingEffects
        |> List.exists (function
            | CreateDirectory _
            | WriteFile _ -> true
            | _ -> false)

    let appendNewEffects effects model =
        let existing = model.PendingEffects |> List.map effectKey |> Set.ofList

        effects
        |> List.filter (fun effect -> not (Set.contains (effectKey effect) existing))

    let snapshot path model =
        let key = readEffectKey path

        model.InterpretedEffects
        |> List.tryPick (fun result ->
            if effectKey result.Effect = key then
                result.Snapshot
            else
                None)

    let directoryListing path model =
        let key = "enumerate:" + normalizeRelativePath path

        model.InterpretedEffects
        |> List.tryPick (fun result ->
            if effectKey result.Effect = key then
                result.Snapshot |> Option.map _.Text
            else
                None)
        |> Option.defaultValue ""

    let plannedReadPaths model =
        model.PendingEffects
        |> List.choose (function
            | ReadFile path -> Some(normalizeRelativePath path)
            | _ -> None)

    let allPlannedReadsInterpreted model =
        model.PendingEffects
        |> List.filter (function
            | ReadFile _
            | EnumerateDirectory _ -> true
            | _ -> false)
        |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

    let duplicateCandidateReadEffects workId model =
        let selectedPrefix = $"work/{workId}/"
        let already = plannedReadPaths model |> Set.ofList

        directoryListing "work" model
        |> fun text -> text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (fun path ->
            (path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase)
             || path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase))
            && not (path.StartsWith(selectedPrefix, StringComparison.OrdinalIgnoreCase))
            && not (Set.contains path already))
        |> Array.sort
        |> Array.map ReadFile
        |> Array.toList

    let stripQuotes (value: string) =
        let value = if String.IsNullOrEmpty value then "" else value.Trim()
        value.Trim([| '"'; '\'' |])

    let tryScalar key (yaml: string) =
        let pattern = $"(?m)^\\s*{Regex.Escape key}\\s*:\\s*(.*?)\\s*$"
        let m = Regex.Match(yaml, pattern)

        if m.Success then
            let value = stripQuotes m.Groups.[1].Value
            if String.IsNullOrWhiteSpace value then None else Some value
        else
            None

    let splitFrontMatter (text: string) =
        let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")
        let lines = normalized.Split('\n')

        if lines.Length > 0 && lines.[0].Trim() = "---" then
            lines
            |> Array.mapi (fun index line -> index, line)
            |> Array.tryFind (fun (index, line) -> index > 0 && line.Trim() = "---")
            |> Option.map (fun (index, _) ->
                let yaml = lines.[1 .. index - 1] |> String.concat "\n"
                let body = lines.[index + 1 ..] |> String.concat "\n"
                yaml, body)
        else
            None

    type CharterFrontMatter =
        { SchemaVersion: string
          WorkId: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    let generatedViewState
        (path: string)
        (kind: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (outputDigest: OutputDigest option)
        (currency: GeneratedViewCurrency)
        (diagnosticIds: string list)
        : GeneratedViewState
        =
        { Path = path
          Kind = kind
          SchemaVersion = Some 1
          Generator = Some generator
          Sources = sources |> List.sortBy _.Path
          OutputDigest = outputDigest
          Currency = currency
          DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }

    let blockingDiagnosticIds (diagnostics: Diagnostic list) : string list =
        diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
        |> List.map _.Id

    let blockedWorkModelView (path: string) (generator: GeneratorVersion) (blockingIds: string list) : GeneratedViewState =
        generatedViewState path "workModel" generator [] None GeneratedViewCurrency.Blocked blockingIds

