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
open FS.GG.SDD.Artifacts.LifecycleArtifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.Serialization
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes

module CommandWorkflow =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers
    module LifecycleArtifactsModule = FS.GG.SDD.Artifacts.LifecycleArtifacts
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module SerializationModule = FS.GG.SDD.Artifacts.Serialization
    module WorkModelModule = FS.GG.SDD.Artifacts.WorkModel

    let normalizeRoot (path: string) =
        if String.IsNullOrWhiteSpace path then "." else path.Trim()

    let normalizeRelativePath (path: string) =
        (if isNull path then "" else path.Trim().Replace('\\', '/')).TrimStart('/')

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
          ReadFile(GenerationManifestModule.expectedSummaryOutputPath workId)
          EnumerateDirectory "work" ]

    let workIdDiagnostics (request: CommandRequest) =
        match request.Command, request.WorkId with
        | Init, _ -> []
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
            | command, _ -> [ unsupportedCommand command ], []

    let effectKey effect =
        match effect with
        | ReadFile path -> "read:" + normalizeRelativePath path
        | EnumerateDirectory path -> "enumerate:" + normalizeRelativePath path
        | CreateDirectory path -> "mkdir:" + normalizeRelativePath path
        | WriteFile(path, _, kind) -> $"write:{normalizeRelativePath path}:{writeKindValue kind}"
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
        let value = if isNull value then "" else value.Trim()
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
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")
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

    let parseCharterFrontMatter path text =
        match splitFrontMatter text with
        | None -> Error(malformedCharterFrontMatter path "Charter is missing YAML front matter.")
        | Some(yaml, _) ->
            match tryScalar "schemaVersion" yaml, tryScalar "workId" yaml, tryScalar "title" yaml, tryScalar "stage" yaml, tryScalar "changeTier" yaml, tryScalar "status" yaml with
            | Some schemaVersion, Some workId, Some title, Some stage, Some changeTier, Some status ->
                Ok
                    { SchemaVersion = schemaVersion
                      WorkId = workId
                      Title = title
                      Stage = stage
                      ChangeTier = changeTier
                      Status = status }
            | _ -> Error(malformedCharterFrontMatter path "Charter front matter is incomplete.")

    let titleFromWorkId (workId: string) =
        workId.Split('-', StringSplitOptions.RemoveEmptyEntries)
        |> Array.skipWhile (fun part -> part |> Seq.forall Char.IsDigit)
        |> fun parts -> if Array.isEmpty parts then workId.Split('-', StringSplitOptions.RemoveEmptyEntries) else parts
        |> Array.map (fun part ->
            if part.Length = 0 then part
            else Char.ToUpperInvariant(part.[0]).ToString() + part.Substring(1))
        |> String.concat " "

    let requestTitle (request: CommandRequest) workId =
        request.Title
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.map _.Trim()
        |> Option.defaultValue (titleFromWorkId workId)

    let charterTemplate request workId =
        let title = requestTitle request workId

        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# {title} Charter

## Identity
- Work id: `{workId}`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Capture the work item's local principles before specification begins.

## Scope Boundaries
- Keep SDD lifecycle ownership separate from optional Governance enforcement.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work {workId}`.
"""

    type SpecificationIntent =
        { UserValue: string option
          Scope: string list
          NonGoals: string list
          Stories: string list
          Requirements: string list
          AcceptanceScenarios: string list
          Ambiguities: string list
          Impact: string list }

    let emptySpecificationIntent =
        { UserValue = None
          Scope = []
          NonGoals = []
          Stories = []
          Requirements = []
          AcceptanceScenarios = []
          Ambiguities = []
          Impact = [] }

    let appendIntent (label: string) (value: string) (intent: SpecificationIntent) =
        let value = if isNull value then "" else value.Trim()

        if String.IsNullOrWhiteSpace value then
            intent
        else
            match label with
            | "value"
            | "user value"
            | "goal"
            | "intent" -> { intent with UserValue = Some value }
            | "scope"
            | "in scope"
            | "in-scope" -> { intent with Scope = intent.Scope @ [ value ] }
            | "non-goal"
            | "non goal"
            | "out of scope"
            | "out-of-scope" -> { intent with NonGoals = intent.NonGoals @ [ value ] }
            | "story"
            | "user story" -> { intent with Stories = intent.Stories @ [ value ] }
            | "requirement"
            | "functional requirement"
            | "fr" -> { intent with Requirements = intent.Requirements @ [ value ] }
            | "acceptance"
            | "acceptance scenario"
            | "scenario" -> { intent with AcceptanceScenarios = intent.AcceptanceScenarios @ [ value ] }
            | "ambiguity"
            | "question" -> { intent with Ambiguities = intent.Ambiguities @ [ value ] }
            | "impact"
            | "public or tool-facing impact" -> { intent with Impact = intent.Impact @ [ value ] }
            | _ ->
                match intent.UserValue with
                | None -> { intent with UserValue = Some value }
                | Some _ -> intent

    let normalizeSpecificationIntent (request: CommandRequest) =
        let input = request.InputText |> Option.defaultValue ""

        let parsed =
            input.Replace("\r\n", "\n").Split('\n')
            |> Array.fold
                (fun intent line ->
                    let trimmed = line.Trim().TrimStart('-', '*').Trim()
                    let separator = trimmed.IndexOf(':')

                    if separator > 0 then
                        let label = trimmed.Substring(0, separator).Trim().ToLowerInvariant()
                        let value = trimmed.Substring(separator + 1).Trim()
                        appendIntent label value intent
                    elif String.IsNullOrWhiteSpace trimmed then
                        intent
                    else
                        appendIntent "value" trimmed intent)
                emptySpecificationIntent

        let missing =
            [ if Option.isNone parsed.UserValue then "user value"
              if List.isEmpty parsed.Scope then "scope"
              if List.isEmpty parsed.Requirements then "measurable requirement" ]

        parsed, missing

    let numberedId prefix index = sprintf "%s-%03d" prefix (index + 1)

    let specificationTemplate request workId intent =
        let title = requestTitle request workId
        let userValue = intent.UserValue |> Option.defaultValue $"Specify work item {workId}."
        let scope = if List.isEmpty intent.Scope then [ "Author one chartered SDD work item specification." ] else intent.Scope
        let nonGoals =
            if List.isEmpty intent.NonGoals then
                [ "Do not implement later lifecycle commands or Governance enforcement in this specification." ]
            else
                intent.NonGoals

        let requirements = if List.isEmpty intent.Requirements then [ "Create a specification artifact with stable ids." ] else intent.Requirements
        let stories = if List.isEmpty intent.Stories then [ $"As a maintainer, I can specify {title} after chartering the work item." ] else intent.Stories
        let acceptanceScenarios =
            if List.isEmpty intent.AcceptanceScenarios then
                [ "Given a chartered work item, when specify runs with intent, then spec.md is created with stable ids." ]
            else
                intent.AcceptanceScenarios

        let scopeLines =
            scope
            |> List.mapi (fun index text ->
                let id = numberedId "SB" index
                $"- {id}: {text}")
            |> String.concat "\n"

        let nonGoalLines =
            nonGoals
            |> List.mapi (fun index text ->
                let id = numberedId "SB" (index + List.length scope)
                $"- {id}: {text}")
            |> String.concat "\n"

        let storyLines =
            stories
            |> List.mapi (fun index text ->
                let id = numberedId "US" index
                $"- {id} (P1): {text}")
            |> String.concat "\n"

        let acceptanceLines =
            acceptanceScenarios
            |> List.mapi (fun index text ->
                let id = numberedId "AC" index
                $"- {id} [US-001] [FR-001]: {text}")
            |> String.concat "\n"

        let requirementLines =
            requirements
            |> List.mapi (fun index text ->
                let id = numberedId "FR" index
                $"- {id}: {text} (Stories: US-001; Acceptance: AC-001)")
            |> String.concat "\n"

        let ambiguityLines =
            if List.isEmpty intent.Ambiguities then
                "No material ambiguities recorded."
            else
                intent.Ambiguities
                |> List.mapi (fun index text ->
                    let id = numberedId "AMB" index
                    $"- {id} open: {text}")
                |> String.concat "\n"

        let impactLines =
            if List.isEmpty intent.Impact then
                "- This specification is an SDD lifecycle artifact and command-report contract input."
            else
                intent.Impact |> List.map (fun text -> $"- {text}") |> String.concat "\n"

        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# {title} Specification

Prose status: specified

## User Value
{userValue}

## Scope
{scopeLines}

## Non-Goals
{nonGoalLines}

## User Stories
{storyLines}

## Acceptance Scenarios
{acceptanceLines}

## Functional Requirements
{requirementLines}

## Ambiguities
{ambiguityLines}

## Public Or Tool-Facing Impact
{impactLines}

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work {workId}`.
"""

    let hasSection (heading: string) (text: string) =
        Regex.IsMatch(text, $"(?m)^##\\s+{Regex.Escape heading}\\s*$")

    let sectionText workId heading =
        match heading with
        | "Identity" -> $"## Identity\n- Work id: `{workId}`\n- Lifecycle stage: charter\n- Status: chartered\n"
        | "Principles" -> "## Principles\n- Capture the work item's local principles before specification begins.\n"
        | "Scope Boundaries" -> "## Scope Boundaries\n- Keep SDD lifecycle ownership separate from optional Governance enforcement.\n"
        | "Policy Pointers" -> "## Policy Pointers\n- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.\n- Governance files are optional compatibility pointers and are not evaluated by this command.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd specify --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensureStandardSections workId text =
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")
        let required = [ "Identity"; "Principles"; "Scope Boundaries"; "Policy Pointers"; "Lifecycle Notes" ]

        let missing =
            required
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix =
                missing
                |> List.map (sectionText workId)
                |> String.concat "\n"

            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let specificationSectionText heading =
        match heading with
        | "User Value" -> "## User Value\n"
        | "Scope" -> "## Scope\n"
        | "Non-Goals" -> "## Non-Goals\n"
        | "User Stories" -> "## User Stories\n"
        | "Acceptance Scenarios" -> "## Acceptance Scenarios\n"
        | "Functional Requirements" -> "## Functional Requirements\n"
        | "Ambiguities" -> "## Ambiguities\n"
        | "Public Or Tool-Facing Impact" -> "## Public Or Tool-Facing Impact\n"
        | "Lifecycle Notes" -> "## Lifecycle Notes\n"
        | _ -> $"## {heading}\n"

    let ensureSpecificationSections text =
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")

        let missing =
            LifecycleArtifactsModule.specificationStandardSections ()
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map specificationSectionText |> String.concat "\n"
            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let candidateSnapshots model =
        model.InterpretedEffects
        |> List.choose (fun result ->
            match result.Effect, result.Snapshot with
            | ReadFile path, Some snapshot
                when path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase)
                     || path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) ->
                Some({ snapshot with Path = normalizeRelativePath path })
            | _ -> None)

    let duplicateWorkIdDiagnostics workId model =
        candidateSnapshots model
        |> List.choose (fun snapshot ->
            if snapshot.Path.StartsWith($"work/{workId}/", StringComparison.OrdinalIgnoreCase) then
                None
            elif snapshot.Path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase) then
                match parseCharterFrontMatter snapshot.Path snapshot.Text with
                | Ok frontMatter when String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase) -> Some snapshot.Path
                | _ -> None
            elif snapshot.Path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then
                match LifecycleArtifactsModule.parseWorkItemMetadata snapshot with
                | Ok metadata when String.Equals(metadata.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase) -> Some snapshot.Path
                | _ -> None
            else
                None)
        |> function
            | [] -> []
            | paths -> [ duplicateWorkId workId paths ]

    let projectDiagnostics model =
        let project = snapshot ".fsgg/project.yml" model
        let sdd = snapshot ".fsgg/sdd.yml" model
        let agents = snapshot ".fsgg/agents.yml" model

        match project, sdd, agents with
        | None, None, None -> [ outsideProject() ]
        | _ ->
            let missing =
                [ if Option.isNone project then missingProjectConfig ".fsgg/project.yml"
                  if Option.isNone sdd then missingSddConfig ".fsgg/sdd.yml"
                  if Option.isNone agents then missingAgentsConfig ".fsgg/agents.yml" ]

            let malformed =
                [ match project with
                  | Some snapshot ->
                      match LifecycleArtifactsModule.parseProjectConfig snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedProjectConfig snapshot.Path
                  | None -> ()

                  match sdd with
                  | Some snapshot ->
                      match LifecycleArtifactsModule.parseSddLifecyclePolicy snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedSddConfig snapshot.Path
                  | None -> ()

                  match agents with
                  | Some snapshot ->
                      match LifecycleArtifactsModule.parseAgentGuidanceConfig snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedAgentsConfig snapshot.Path
                  | None -> () ]

            missing @ malformed

    let charterDiagnosticsAndText request workId model =
        let path = charterPath workId

        match snapshot path model with
        | None -> [], charterTemplate request workId
        | Some existing ->
            match parseCharterFrontMatter path existing.Text with
            | Error diagnostic -> [ diagnostic ], existing.Text
            | Ok frontMatter when frontMatter.SchemaVersion <> "1" ->
                [ malformedCharterFrontMatter path $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ], existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ malformedCharterFrontMatter path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ], existing.Text
            | Ok _ when existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) ->
                [ unsafeOverwrite path ], existing.Text
            | Ok _ -> [], ensureStandardSections workId existing.Text

    let charterPrerequisiteDiagnosticsAndText workId model =
        let path = charterPath workId

        match snapshot path model with
        | None -> [ missingCharterPrerequisite path $"Charter prerequisite '{path}' is missing." ], None
        | Some existing ->
            match parseCharterFrontMatter path existing.Text with
            | Error _ -> [ missingCharterPrerequisite path "Charter prerequisite front matter is malformed." ], Some existing.Text
            | Ok frontMatter when frontMatter.SchemaVersion <> "1" ->
                [ missingCharterPrerequisite path $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ], Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ missingCharterPrerequisite path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ], Some existing.Text
            | Ok _ -> [], Some existing.Text

    let specificationSummary (facts: SpecificationFacts) : SpecificationSummary =
        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          StoryIds = facts.UserStoryIds |> List.map _.Value |> List.sort
          RequirementIds = facts.RequirementIds |> List.map _.Value |> List.sort
          AcceptanceScenarioIds = facts.AcceptanceScenarioIds |> List.map _.Value |> List.sort
          AmbiguityIds = facts.AmbiguityIds |> List.map _.Value |> List.sort
          UnresolvedAmbiguityCount = facts.UnresolvedAmbiguityCount }

    let mapSpecificationDiagnostics (path: string) (diagnostics: Diagnostic list) : Diagnostic list =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateSpecificationId path id
            | "unknownReference", id :: _ -> unknownSpecificationReference path id
            | "workModelInconsistent", idFamily :: _ when idFamily.EndsWith("###", StringComparison.Ordinal) -> missingSpecificationId path idFamily
            | _ -> diagnostic)

    let parseSpecificationForCommand path text : Result<SpecificationFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match LifecycleArtifactsModule.parseSpecificationFacts snapshot with
        | Error diagnostics -> Error(mapSpecificationDiagnostics path diagnostics)
        | Ok facts ->
            let diagnostics = mapSpecificationDiagnostics path facts.Diagnostics
            Ok(facts, diagnostics)

    let specificationDiagnosticsTextAndSummary request workId model =
        let path = specPath workId

        match snapshot path model with
        | None ->
            let intent, missingFacts = normalizeSpecificationIntent request

            if not (List.isEmpty missingFacts) then
                [ missingSpecificationIntent path missingFacts ], None, None
            else
                let text = specificationTemplate request workId intent

                match parseSpecificationForCommand path text with
                | Error diagnostics -> diagnostics, Some text, None
                | Ok(facts, diagnostics) -> diagnostics, Some text, Some(specificationSummary facts)
        | Some existing ->
            let unsafe =
                if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                    [ unsafeOverwrite path ]
                else
                    []

            match parseSpecificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic ->
                        if diagnostic.Id = "workModelInconsistent" || diagnostic.Id = "malformedSchemaVersion" then
                            malformedSpecificationFrontMatter path diagnostic.Message
                        else
                            diagnostic)

                unsafe @ mapped |> DiagnosticsModule.sort, Some existing.Text, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedSpecificationFrontMatter path $"Specification schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          specificationIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Specify then
                          malformedSpecificationFrontMatter path $"Specification stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'specify'." ]

                let allDiagnostics = unsafe @ identityDiagnostics @ diagnostics |> DiagnosticsModule.sort
                let hasBlocking = allDiagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                let text = if hasBlocking then existing.Text else ensureSpecificationSections existing.Text

                let summary =
                    match parseSpecificationForCommand path text with
                    | Ok(nextFacts, _) -> Some(specificationSummary nextFacts)
                    | Error _ -> Some(specificationSummary facts)

                allDiagnostics, Some text, summary

    let specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model =
        let path = specPath workId

        match snapshot path model with
        | None ->
            [ missingSpecificationPrerequisite path $"Specification prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseSpecificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedSpecificationFacts path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedSpecificationFacts path $"Specification schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          specificationIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Specify then
                          missingSpecificationPrerequisite path $"Specification stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'specify'." ]

                let mappedDiagnostics =
                    diagnostics
                    |> List.map (fun diagnostic ->
                        match diagnostic.Id, diagnostic.RelatedIds with
                        | "duplicateSpecificationId", _
                        | "unknownSpecificationReference", _
                        | "missingSpecificationId", _ -> diagnostic
                        | _ -> malformedSpecificationFacts path diagnostic.Message)

                let allDiagnostics = identityDiagnostics @ mappedDiagnostics |> DiagnosticsModule.sort
                allDiagnostics, Some existing.Text, Some(specificationSummary facts), Some facts

    let clarificationSectionText workId heading =
        match heading with
        | "Source Specification" -> $"## Source Specification\n- {specPath workId}\n"
        | "Clarification Questions" -> "## Clarification Questions\nNo clarification questions recorded.\n"
        | "Answers" -> "## Answers\nNo clarification answers recorded.\n"
        | "Decisions" -> "## Decisions\nNo concrete decisions recorded.\n"
        | "Accepted Deferrals" -> "## Accepted Deferrals\nNo accepted deferrals recorded.\n"
        | "Remaining Ambiguity" -> "## Remaining Ambiguity\nNo blocking ambiguity remains.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd checklist --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensureClarificationSections workId text =
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")

        let missing =
            LifecycleArtifactsModule.clarificationStandardSections ()
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map (clarificationSectionText workId) |> String.concat "\n"
            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let nextScopedIndex prefix (text: string) =
        Regex.Matches(text, $@"\b{Regex.Escape prefix}-(\d{{3,}})\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m ->
            match Int32.TryParse m.Groups.[1].Value with
            | true, value -> Some value
            | _ -> None)
        |> Seq.fold max 0
        |> (+) 1

    let scopedId prefix index = sprintf "%s-%03d" prefix index

    let clarificationSummary (facts: ClarificationFacts) : ClarificationSummary =
        let answeredQuestionIds =
            [ facts.Answers |> List.choose (fun answer -> answer.QuestionId |> Option.map _.Value)
              facts.Decisions |> List.collect (fun decision -> decision.SourceQuestionIds |> List.map _.Value)
              facts.AcceptedDeferrals |> List.collect (fun decision -> decision.SourceQuestionIds |> List.map _.Value) ]
            |> List.concat
            |> List.distinct
            |> List.sort

        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          QuestionIds = facts.Questions |> List.map (fun question -> question.QuestionId.Value) |> List.sort
          AnsweredQuestionIds = answeredQuestionIds
          DecisionIds = facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value) |> List.sort
          AcceptedDeferralIds = facts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value) |> List.sort
          RemainingAmbiguityCount = facts.RemainingAmbiguity.Length
          BlockingAmbiguityCount = facts.BlockingAmbiguityCount }

    let mapClarificationDiagnostics (path: string) (diagnostics: Diagnostic list) =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateClarificationId path id
            | "unknownReference", id :: _ -> unknownClarificationReference path id
            | "workModelInconsistent", _ -> malformedClarificationFrontMatter path diagnostic.Message
            | "malformedSchemaVersion", _ -> malformedClarificationFrontMatter path diagnostic.Message
            | "unsupportedSchemaVersion", _ -> malformedClarificationFrontMatter path diagnostic.Message
            | "futureSchemaVersion", _ -> malformedClarificationFrontMatter path diagnostic.Message
            | _ -> diagnostic)

    let parseClarificationForCommand path text : Result<ClarificationFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match LifecycleArtifactsModule.parseClarificationFacts snapshot with
        | Error diagnostics -> Error(mapClarificationDiagnostics path diagnostics)
        | Ok facts ->
            let diagnostics = mapClarificationDiagnostics path facts.Diagnostics
            Ok(facts, diagnostics)

    let inputLines (request: CommandRequest) =
        request.InputText
        |> Option.defaultValue ""
        |> fun text -> text.Replace("\r\n", "\n").Split('\n')
        |> Array.map (fun line -> line.Trim().TrimStart('-', '*').Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    let idMatches pattern (text: string) =
        Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let answerKindValue (line: string) =
        let lowered = line.ToLowerInvariant()

        if lowered.Contains("accepted deferral") || lowered.Contains("defer") then
            "acceptedDeferral"
        elif lowered.Contains("still open") || lowered.Contains("unresolved") then
            "stillOpen"
        else
            "decision"

    let answerTextForReference (referenceId: string) (line: string) =
        let index = line.IndexOf(referenceId, StringComparison.OrdinalIgnoreCase)

        if index >= 0 then
            line.Substring(index + referenceId.Length).Trim().TrimStart(':', '-', ' ').Trim()
        else
            line.Trim()

    let knownQuestionIdForAmbiguity (ambiguityIndex: int) (existingQuestions: ClarificationQuestion list) (ambiguityValue: string) =
        existingQuestions
        |> List.tryFind (fun question ->
            question.SourceAmbiguityIds
            |> List.exists (fun ambiguity -> String.Equals(ambiguity.Value, ambiguityValue, StringComparison.OrdinalIgnoreCase)))
        |> Option.map (fun question -> question.QuestionId.Value)
        |> Option.defaultValue (scopedId "CQ" (ambiguityIndex + 1))

    let existingResolutionTextForAmbiguity (facts: ClarificationFacts option) ambiguityValue =
        facts
        |> Option.bind (fun facts ->
            (facts.Decisions @ facts.AcceptedDeferrals)
            |> List.tryFind (fun decision ->
                decision.SourceAmbiguityIds
                |> List.exists (fun ambiguity -> String.Equals(ambiguity.Value, ambiguityValue, StringComparison.OrdinalIgnoreCase)))
            |> Option.map (fun decision -> decision.DecisionId.Value, decision.Text))

    let normalizeDecisionText (text: string) =
        Regex.Replace((if isNull text then "" else text).Trim().ToLowerInvariant(), @"\s+", " ")

    type PlannedClarificationAnswer =
        { AmbiguityId: string
          QuestionId: string
          DecisionId: string option
          Kind: string
          Text: string }

    let unknownReferenceDiagnostics path (specFacts: SpecificationFacts) existingQuestions lines =
        let knownAmbiguities = specFacts.AmbiguityIds |> List.map _.Value |> Set.ofList
        let knownRequirements = specFacts.RequirementIds |> List.map _.Value |> Set.ofList
        let knownStories = specFacts.UserStoryIds |> List.map _.Value |> Set.ofList
        let knownScenarios = specFacts.AcceptanceScenarioIds |> List.map _.Value |> Set.ofList

        let generatedQuestionIds =
            specFacts.AmbiguityIds
            |> List.mapi (fun index _ -> scopedId "CQ" (index + 1))
            |> Set.ofList

        let knownQuestions =
            existingQuestions
            |> List.map (fun (question: ClarificationQuestion) -> question.QuestionId.Value)
            |> Set.ofList
            |> Set.union generatedQuestionIds

        let check pattern known =
            lines
            |> List.collect (idMatches pattern)
            |> List.distinct
            |> List.choose (fun id -> if Set.contains id known then None else Some(unknownClarificationReference path id))

        [ check @"\bAMB-\d{3,}\b" knownAmbiguities
          check @"\bFR-\d{3,}\b" knownRequirements
          check @"\bUS-\d{3,}\b" knownStories
          check @"\bAC-\d{3,}\b" knownScenarios
          check @"\bCQ-\d{3,}\b" knownQuestions ]
        |> List.concat

    let plannedClarificationAnswers (path: string) (request: CommandRequest) (specFacts: SpecificationFacts) (existingFacts: ClarificationFacts option) =
        let lines = inputLines request
        let existingQuestions = existingFacts |> Option.map _.Questions |> Option.defaultValue []

        let unknownReferences = unknownReferenceDiagnostics path specFacts existingQuestions lines

        let unresolvedAmbiguities =
            specFacts.AmbiguityIds
            |> List.mapi (fun index ambiguity ->
                let ambiguityValue = ambiguity.Value
                let existingResolution = existingResolutionTextForAmbiguity existingFacts ambiguityValue

                let matchingLine =
                    lines
                    |> List.tryFind (fun line ->
                        line.IndexOf(ambiguityValue, StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf(knownQuestionIdForAmbiguity index existingQuestions ambiguityValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    |> Option.orElseWith (fun () ->
                        if specFacts.AmbiguityIds.Length = 1 && lines.Length = 1 && List.isEmpty (idMatches @"\b(?:AMB|CQ|FR|US|AC)-\d{3,}\b" lines.Head) then
                            Some lines.Head
                        else
                            None)

                index, ambiguityValue, existingResolution, matchingLine)

        let missing =
            unresolvedAmbiguities
            |> List.choose (fun (_, ambiguityValue, existingResolution, matchingLine) ->
                match existingResolution, matchingLine with
                | None, None -> Some ambiguityValue
                | _ -> None)

        let missingDiagnostics =
            if not (List.isEmpty missing) && not (List.isEmpty specFacts.AmbiguityIds) then
                [ missingClarificationAnswer path missing ]
            else
                []

        let mutable nextDecision = 1

        existingFacts
        |> Option.iter (fun facts ->
            let text =
                (facts.Decisions @ facts.AcceptedDeferrals)
                |> List.map (fun decision -> decision.DecisionId.Value)
                |> String.concat "\n"

            nextDecision <- nextScopedIndex "DEC" text)

        let answers =
            unresolvedAmbiguities
            |> List.choose (fun (index, ambiguityValue, existingResolution, matchingLine) ->
                match matchingLine with
                | None -> None
                | Some line ->
                    let text = answerTextForReference ambiguityValue line
                    let kind = answerKindValue line

                    match existingResolution with
                    | Some(_, existingText) when normalizeDecisionText existingText <> normalizeDecisionText text ->
                        None
                    | Some _ ->
                        None
                    | None ->
                        let decisionId =
                            if kind = "stillOpen" then
                                None
                            else
                                let id = scopedId "DEC" nextDecision
                                nextDecision <- nextDecision + 1
                                Some id

                        Some
                            ({ AmbiguityId = ambiguityValue
                               QuestionId = knownQuestionIdForAmbiguity index existingQuestions ambiguityValue
                               DecisionId = decisionId
                               Kind = kind
                               Text = if String.IsNullOrWhiteSpace text then line else text }
                            : PlannedClarificationAnswer))

        let conflictDiagnostics =
            unresolvedAmbiguities
            |> List.choose (fun (_, ambiguityValue, existingResolution, matchingLine) ->
                match existingResolution, matchingLine with
                | Some(decisionId, existingText), Some line ->
                    let text = answerTextForReference ambiguityValue line
                    if normalizeDecisionText existingText <> normalizeDecisionText text then
                        Some(unsafeDecisionChange path decisionId)
                    else
                        None
                | _ -> None)

        answers, unknownReferences @ missingDiagnostics @ conflictDiagnostics

    let renderQuestionLine questionId ambiguityId =
        $"- {questionId} [AMB:{ambiguityId}] blocking open: Resolve source ambiguity {ambiguityId} before checklist."

    let renderAnswerLine (answer: PlannedClarificationAnswer) =
        let label =
            match answer.Kind with
            | "acceptedDeferral" -> "accepted deferral"
            | "stillOpen" -> "still open"
            | _ -> "decision"

        $"- {answer.QuestionId} [AMB:{answer.AmbiguityId}] {label}: {answer.Text}"

    let renderDecisionLine (answer: PlannedClarificationAnswer) =
        match answer.DecisionId with
        | Some decisionId when answer.Kind = "acceptedDeferral" ->
            Some $"- {decisionId} [{answer.QuestionId}] [AMB:{answer.AmbiguityId}]: {answer.Text}"
        | Some decisionId ->
            Some $"- {decisionId} [{answer.QuestionId}] [AMB:{answer.AmbiguityId}]: {answer.Text}"
        | None -> None

    let renderRemainingLine (answer: PlannedClarificationAnswer) =
        if answer.Kind = "stillOpen" then
            Some $"- {answer.AmbiguityId} [{answer.QuestionId}] blocking: {answer.Text}"
        else
            None

    let clarificationTemplate request workId (specFacts: SpecificationFacts) answers =
        let title = requestTitle request workId
        let status = if answers |> List.exists (fun answer -> answer.Kind = "stillOpen") then "needsAnswers" else "clarified"

        let questionLines =
            specFacts.AmbiguityIds
            |> List.mapi (fun index ambiguity -> renderQuestionLine (scopedId "CQ" (index + 1)) ambiguity.Value)
            |> fun lines -> if List.isEmpty lines then [ "No clarification questions recorded." ] else lines

        let answerLines =
            if List.isEmpty answers then
                [ "No clarification answers recorded." ]
            else
                answers |> List.map renderAnswerLine

        let concreteDecisionLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "decision")
            |> List.choose renderDecisionLine
            |> fun lines -> if List.isEmpty lines then [ "No concrete decisions recorded." ] else lines

        let deferralLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "acceptedDeferral")
            |> List.choose renderDecisionLine
            |> fun lines -> if List.isEmpty lines then [ "No accepted deferrals recorded." ] else lines

        let remainingLines =
            answers
            |> List.choose renderRemainingLine
            |> fun lines -> if List.isEmpty lines then [ "No blocking ambiguity remains." ] else lines

        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: clarify
changeTier: tier1
status: {status}
sourceSpec: {specPath workId}
publicOrToolFacingImpact: true
---

# {title} Clarifications

## Source Specification
- {specPath workId}

## Clarification Questions
{String.concat "\n" questionLines}

## Answers
{String.concat "\n" answerLines}

## Decisions
{String.concat "\n" concreteDecisionLines}

## Accepted Deferrals
{String.concat "\n" deferralLines}

## Remaining Ambiguity
{String.concat "\n" remainingLines}

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work {workId}`.
"""

    let appendToSection heading lines text =
        if List.isEmpty lines then
            text
        else
            let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")
            let split = normalized.Split('\n') |> Array.toList
            let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

            match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
            | None ->
                let sectionBody = String.concat "\n" lines
                let suffix = $"## {heading}\n{sectionBody}"
                $"{normalized.TrimEnd()}\n\n{suffix}\n"
            | Some start ->
                let next =
                    split
                    |> List.mapi (fun index line -> index, line)
                    |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                    |> Option.map fst
                    |> Option.defaultValue split.Length

                let before = split |> List.take next
                let after = split |> List.skip next
                (before @ lines @ after) |> String.concat "\n"

    let appendClarificationAnswers (existingText: string) (answers: PlannedClarificationAnswer list) =
        let questionLines =
            answers
            |> List.filter (fun answer -> not (Regex.IsMatch(existingText, $@"\b{Regex.Escape answer.QuestionId}\b", RegexOptions.IgnoreCase)))
            |> List.map (fun answer -> renderQuestionLine answer.QuestionId answer.AmbiguityId)

        let answerLines = answers |> List.map renderAnswerLine

        let decisionLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "decision")
            |> List.choose renderDecisionLine

        let deferralLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "acceptedDeferral")
            |> List.choose renderDecisionLine

        let remainingLines = answers |> List.choose renderRemainingLine

        existingText
        |> appendToSection "Clarification Questions" questionLines
        |> appendToSection "Answers" answerLines
        |> appendToSection "Decisions" decisionLines
        |> appendToSection "Accepted Deferrals" deferralLines
        |> appendToSection "Remaining Ambiguity" remainingLines

    let clarificationDiagnosticsTextAndSummary request workId specFacts model =
        let path = clarificationPath workId

        match snapshot path model with
        | None ->
            let answers, answerDiagnostics = plannedClarificationAnswers path request specFacts None

            if answerDiagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError) then
                answerDiagnostics |> DiagnosticsModule.sort, None, None
            else
                let text = clarificationTemplate request workId specFacts answers

                match parseClarificationForCommand path text with
                | Error diagnostics -> diagnostics, Some text, None
                | Ok(facts, diagnostics) ->
                    let unresolved =
                        if facts.BlockingAmbiguityCount > 0 then
                            [ unresolvedBlockingAmbiguity path (facts.RemainingAmbiguity |> List.choose (fun item -> item.AmbiguityId |> Option.map _.Value)) ]
                        else
                            []

                    diagnostics @ unresolved |> DiagnosticsModule.sort, Some text, Some(clarificationSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            else
                match parseClarificationForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        [ if existingFacts.FrontMatter.SchemaVersion.Major <> 1 then
                              malformedClarificationFrontMatter path $"Clarification schemaVersion '{existingFacts.FrontMatter.SchemaVersion.Major}' is not supported."
                          if not (String.Equals(existingFacts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                              clarificationIdentityMismatch path workId existingFacts.FrontMatter.WorkId.Value
                          if existingFacts.FrontMatter.Stage <> LifecycleStage.Clarify then
                              malformedClarificationFrontMatter path $"Clarification stage '{IdentifiersModule.stageValue existingFacts.FrontMatter.Stage}' is not 'clarify'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedClarificationFrontMatter path $"Clarification sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'." ]

                    let ensuredText =
                        if List.isEmpty identityDiagnostics then
                            ensureClarificationSections workId existing.Text
                        else
                            existing.Text

                    let existingFactsForAnswers =
                        match parseClarificationForCommand path ensuredText with
                        | Ok(facts, _) -> Some facts
                        | Error _ -> Some existingFacts

                    let answers, answerDiagnostics = plannedClarificationAnswers path request specFacts existingFactsForAnswers

                    let proposedText =
                        if List.isEmpty identityDiagnostics && List.isEmpty answerDiagnostics then
                            appendClarificationAnswers ensuredText answers
                        else
                            existing.Text

                    let parsedProposed =
                        parseClarificationForCommand path proposedText

                    match parsedProposed with
                    | Error diagnostics ->
                        identityDiagnostics @ diagnostics @ answerDiagnostics |> DiagnosticsModule.sort, Some proposedText, None
                    | Ok(facts, proposedDiagnostics) ->
                        let diagnostics =
                            identityDiagnostics @ proposedDiagnostics @ answerDiagnostics
                            |> DiagnosticsModule.sort

                        diagnostics, Some proposedText, Some(clarificationSummary facts)

    let clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model =
        let path = clarificationPath workId

        match snapshot path model with
        | None ->
            [ missingClarificationPrerequisite path $"Clarification prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseClarificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedClarificationFrontMatter path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedClarificationFrontMatter path $"Clarification schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          clarificationIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Clarify then
                          missingClarificationPrerequisite path $"Clarification stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'clarify'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedClarificationFrontMatter path $"Clarification sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'." ]

                let blocking =
                    if facts.BlockingAmbiguityCount > 0 then
                        [ unresolvedBlockingAmbiguity path (facts.RemainingAmbiguity |> List.choose (fun item -> item.AmbiguityId |> Option.map _.Value)) ]
                    else
                        []

                let allDiagnostics = identityDiagnostics @ diagnostics @ blocking |> DiagnosticsModule.sort
                allDiagnostics, Some existing.Text, Some(clarificationSummary facts), Some facts

    let checklistSectionText workId heading =
        match heading with
        | "Source Specification" -> $"## Source Specification\n- {specPath workId}\n"
        | "Source Clarifications" -> $"## Source Clarifications\n- {clarificationPath workId}\n"
        | "Source Snapshot" -> "## Source Snapshot\n"
        | "Checklist Items" -> "## Checklist Items\nNo checklist items recorded.\n"
        | "Review Results" -> "## Review Results\nNo review results recorded.\n"
        | "Accepted Deferrals" -> "## Accepted Deferrals\nNo accepted checklist deferrals recorded.\n"
        | "Blocking Findings" -> "## Blocking Findings\nNo blocking findings recorded.\n"
        | "Advisory Notes" -> "## Advisory Notes\nNo advisory notes recorded.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd plan --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensureChecklistSections workId text =
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")

        let missing =
            LifecycleArtifactsModule.checklistStandardSections ()
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map (checklistSectionText workId) |> String.concat "\n"
            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let checklistSummary (facts: ChecklistFacts) : ChecklistSummary =
        let results = facts.Results

        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          SourceClarifications = facts.FrontMatter.SourceClarifications
          ItemIds = facts.Items |> List.map (fun item -> item.ItemId.Value) |> List.sort
          ResultIds = results |> List.map (fun result -> result.ResultId.Value) |> List.sort
          PassedCount = results |> List.filter (fun result -> result.Status = "pass") |> List.length
          FailedBlockingCount = results |> List.filter (fun result -> result.Status = "fail") |> List.length
          AcceptedDeferralCount = results |> List.filter (fun result -> result.Status = "acceptedDeferral") |> List.length
          StaleResultCount = facts.StaleResultCount
          AdvisoryCount = results |> List.filter (fun result -> result.Status = "advisory") |> List.length }

    let mapChecklistDiagnostics (path: string) (diagnostics: Diagnostic list) =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateChecklistId path id
            | "unknownReference", id :: _ -> unknownChecklistSourceReference path id
            | "workModelInconsistent", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "malformedSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "unsupportedSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "futureSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | _ -> diagnostic)

    let parseChecklistForCommand path text : Result<ChecklistFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match LifecycleArtifactsModule.parseChecklistFacts snapshot with
        | Error diagnostics -> Error(mapChecklistDiagnostics path diagnostics)
        | Ok facts ->
            let diagnostics = mapChecklistDiagnostics path facts.Diagnostics
            Ok(facts, diagnostics)

    type PlannedChecklistReview =
        { ItemId: string
          ResultId: string
          SourceIds: string list
          Status: string
          Text: string
          Correction: string option
          Blocking: bool }

    let sourceSnapshotLine label path text =
        let digest = (SchemaVersionModule.sha256Text text).Value
        $"- {label}: {path} sha256:{digest} schemaVersion:1"

    let requirementCoverage (specFacts: SpecificationFacts) requirementId =
        specFacts.RequirementReferences
        |> List.filter (fun reference -> reference.RequirementId.Value = requirementId)
        |> List.collect (fun reference -> reference.AcceptanceScenarioIds |> List.map _.Value)
        |> List.distinct
        |> List.sort

    let plannedChecklistReviews (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) existingFacts =
        let existingSourceIds =
            existingFacts
            |> Option.map (fun facts ->
                facts.Items
                |> List.collect (fun item -> item.SourceIds)
                |> Set.ofList)
            |> Option.defaultValue Set.empty

        let existingText =
            existingFacts
            |> Option.map (fun facts ->
                [ facts.Items |> List.map (fun item -> item.ItemId.Value)
                  facts.Results |> List.map (fun result -> result.ResultId.Value) ]
                |> List.concat
                |> String.concat "\n")
            |> Option.defaultValue ""

        let mutable nextItem = nextScopedIndex "CHK" existingText
        let mutable nextResult = nextScopedIndex "CR" existingText

        let allocate sourceIds status text correction blocking =
            let itemId = scopedId "CHK" nextItem
            let resultId = scopedId "CR" nextResult
            nextItem <- nextItem + 1
            nextResult <- nextResult + 1

            { ItemId = itemId
              ResultId = resultId
              SourceIds = sourceIds
              Status = status
              Text = text
              Correction = correction
              Blocking = blocking }

        let requirementReviews =
            specFacts.RequirementIds
            |> List.choose (fun requirement ->
                if Set.contains requirement.Value existingSourceIds then
                    None
                else
                    let coverage = requirementCoverage specFacts requirement.Value
                    let hasCoverage = not (List.isEmpty coverage)

                    allocate
                        (requirement.Value :: coverage)
                        (if hasCoverage then "pass" else "fail")
                        (if hasCoverage then
                             $"Requirement {requirement.Value} is testable and linked to acceptance coverage."
                         else
                             $"Requirement {requirement.Value} is missing acceptance coverage.")
                        (if hasCoverage then
                             None
                         else
                             Some $"Add an acceptance scenario for {requirement.Value} or narrow the requirement.")
                        true
                    |> Some)

        let deferralReviews =
            clarificationFacts.AcceptedDeferrals
            |> List.choose (fun decision ->
                if Set.contains decision.DecisionId.Value existingSourceIds then
                    None
                else
                    allocate
                        [ decision.DecisionId.Value ]
                        "acceptedDeferral"
                        $"Accepted deferral {decision.DecisionId.Value} remains visible to planning."
                        None
                        false
                    |> Some)

        requirementReviews @ deferralReviews

    let renderChecklistItemLine review =
        let source = review.SourceIds |> List.map (fun id -> $"[{id}]") |> String.concat " "
        let kind = if review.Blocking then "blocking" else "advisory"
        $"- {review.ItemId} {source} {kind}: {review.Text}".Replace("  ", " ")

    let renderChecklistResultLine review =
        let source = review.SourceIds |> List.map (fun id -> $"[{id}]") |> String.concat " "
        let correction = review.Correction |> Option.map (fun text -> $" Correction: {text}") |> Option.defaultValue ""
        $"- {review.ResultId} [CHK:{review.ItemId}] {source} {review.Status}: {review.Text}{correction}".Replace("  ", " ")

    let renderChecklistDeferralLine review =
        let source = review.SourceIds |> List.map (fun id -> $"[{id}]") |> String.concat " "
        $"- {review.ResultId} [CHK:{review.ItemId}] {source} acceptedDeferral: {review.Text}".Replace("  ", " ")

    let renderBlockingFindingLine review =
        match review.Correction with
        | Some correction -> $"- {review.ResultId} [{review.ItemId}] {review.Text} Correction: {correction}"
        | None -> $"- {review.ResultId} [{review.ItemId}] {review.Text}"

    let checklistTemplate
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (reviews: PlannedChecklistReview list)
        =
        let title = requestTitle request workId
        let failures = reviews |> List.filter (fun review -> review.Status = "fail")
        let status = if List.isEmpty failures then "checklistReady" else "needsCorrection"
        let itemLines = reviews |> List.map renderChecklistItemLine
        let resultLines = reviews |> List.filter (fun review -> review.Status <> "acceptedDeferral") |> List.map renderChecklistResultLine
        let deferralLines = reviews |> List.filter (fun review -> review.Status = "acceptedDeferral") |> List.map renderChecklistDeferralLine
        let findingLines = failures |> List.map renderBlockingFindingLine

        let itemText = if List.isEmpty itemLines then "No checklist items recorded." else String.concat "\n" itemLines
        let resultText = if List.isEmpty resultLines then "No review results recorded." else String.concat "\n" resultLines
        let deferralText = if List.isEmpty deferralLines then "No accepted checklist deferrals recorded." else String.concat "\n" deferralLines
        let findingText = if List.isEmpty findingLines then "No blocking findings recorded." else String.concat "\n" findingLines
        let advisoryText =
            if List.isEmpty clarificationFacts.AcceptedDeferrals then
                "No advisory notes recorded."
            else
                "- Accepted clarification deferrals remain visible before planning."

        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: checklist
changeTier: tier1
status: {status}
sourceSpec: {specPath workId}
sourceClarifications: {clarificationPath workId}
publicOrToolFacingImpact: true
---

# {title} Checklist

Prose status: {status}

## Source Specification
- {specPath workId}

## Source Clarifications
- {clarificationPath workId}

## Source Snapshot
{sourceSnapshotLine "spec" (specPath workId) specText}
{sourceSnapshotLine "clarifications" (clarificationPath workId) clarificationText}

## Checklist Items
{itemText}

## Review Results
{resultText}

## Accepted Deferrals
{deferralText}

## Blocking Findings
{findingText}

## Advisory Notes
{advisoryText}

## Lifecycle Notes
- Specification requirements reviewed: {specFacts.RequirementIds.Length}.
- Clarification decisions reviewed: {clarificationFacts.Decisions.Length}.
- Next lifecycle action: `fsgg-sdd plan --work {workId}`.
"""

    let knownChecklistSourceIds (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts option) =
        [ specFacts.RequirementIds |> List.map _.Value
          specFacts.UserStoryIds |> List.map _.Value
          specFacts.AcceptanceScenarioIds |> List.map _.Value
          specFacts.ScopeBoundaryIds |> List.map _.Value
          specFacts.AmbiguityIds |> List.map _.Value
          clarificationFacts.Questions |> List.map (fun question -> question.QuestionId.Value)
          clarificationFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value)
          checklistFacts |> Option.map (fun facts -> facts.Items |> List.map (fun item -> item.ItemId.Value)) |> Option.defaultValue [] ]
        |> List.concat
        |> Set.ofList

    let unknownChecklistReferences (path: string) (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts option) =
        match checklistFacts with
        | None -> []
        | Some facts ->
            let known = knownChecklistSourceIds specFacts clarificationFacts checklistFacts

            [ facts.Items |> List.collect (fun item -> item.SourceIds)
              facts.Results |> List.collect (fun result -> result.SourceIds) ]
            |> List.concat
            |> List.distinct
            |> List.choose (fun id ->
                if Set.contains id known then
                    None
                else
                    Some(unknownChecklistSourceReference path id))

    let sourceSnapshotStale (currentSpecText: string) (currentClarificationText: string) (existingFacts: ChecklistFacts) =
        let current =
            [ specPath existingFacts.FrontMatter.WorkId.Value, (SchemaVersionModule.sha256Text currentSpecText).Value
              clarificationPath existingFacts.FrontMatter.WorkId.Value, (SchemaVersionModule.sha256Text currentClarificationText).Value ]
            |> Map.ofList

        existingFacts.SourceSnapshots
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path current with
            | Some recorded, Some actual -> not (String.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
            | _ -> false)

    let appendChecklistReviews (existingText: string) (reviews: PlannedChecklistReview list) =
        let itemLines = reviews |> List.map renderChecklistItemLine
        let resultLines = reviews |> List.filter (fun review -> review.Status <> "acceptedDeferral") |> List.map renderChecklistResultLine
        let deferralLines = reviews |> List.filter (fun review -> review.Status = "acceptedDeferral") |> List.map renderChecklistDeferralLine
        let findingLines = reviews |> List.filter (fun review -> review.Status = "fail") |> List.map renderBlockingFindingLine

        existingText
        |> appendToSection "Checklist Items" itemLines
        |> appendToSection "Review Results" resultLines
        |> appendToSection "Accepted Deferrals" deferralLines
        |> appendToSection "Blocking Findings" findingLines

    let appendStaleChecklistResult existingText (facts: ChecklistFacts) =
        match facts.Items |> List.tryHead, facts.Results |> List.tryHead with
        | Some item, Some result when facts.Results |> List.exists (fun result -> result.Status = "stale") |> not ->
            let resultId = scopedId "CR" (nextScopedIndex "CR" existingText)
            let line = $"- {resultId} [CHK:{item.ItemId.Value}] stale: Source specification or clarification changed since {result.ResultId.Value} was recorded."
            appendToSection "Review Results" [ line ] existingText
        | _ -> existingText

    let checklistQualityDiagnostics (path: string) (reviews: PlannedChecklistReview list) =
        reviews
        |> List.filter (fun review -> review.Status = "fail")
        |> List.map (fun review ->
            failedRequirementsQuality
                path
                review.Text
                (review.Correction |> Option.defaultValue "Correct the source requirement or checklist review before planning.")
                (review.SourceIds @ [ review.ItemId; review.ResultId ] |> List.distinct |> List.sort))

    let checklistDiagnosticsTextAndSummary
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        model
        =
        let path = checklistPath workId
        let baseReviews = plannedChecklistReviews specFacts clarificationFacts None

        match snapshot path model with
        | None ->
            let text = checklistTemplate request workId specText clarificationText specFacts clarificationFacts baseReviews

            match parseChecklistForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let qualityDiagnostics = checklistQualityDiagnostics (specPath workId) baseReviews
                diagnostics @ qualityDiagnostics |> DiagnosticsModule.sort, Some text, Some(checklistSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            elif existing.Text.Contains("<!-- fsgg-sdd: unsafe-result-change -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeChecklistResultChange path "CR-001" ], Some existing.Text, None
            else
                match parseChecklistForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        [ if existingFacts.FrontMatter.SchemaVersion.Major <> 1 then
                              malformedChecklistFrontMatter path $"Checklist schemaVersion '{existingFacts.FrontMatter.SchemaVersion.Major}' is not supported."
                          if not (String.Equals(existingFacts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                              checklistIdentityMismatch path workId existingFacts.FrontMatter.WorkId.Value
                          if existingFacts.FrontMatter.Stage <> LifecycleStage.Checklist then
                              malformedChecklistFrontMatter path $"Checklist stage '{IdentifiersModule.stageValue existingFacts.FrontMatter.Stage}' is not 'checklist'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedChecklistFrontMatter path $"Checklist sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedChecklistFrontMatter path $"Checklist sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'." ]

                    let unknownDiagnostics = unknownChecklistReferences path specFacts clarificationFacts (Some existingFacts)
                    let blockingParserDiagnostics = identityDiagnostics @ existingDiagnostics @ unknownDiagnostics |> DiagnosticsModule.sort
                    let hasBlockingParserDiagnostics = blockingParserDiagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        blockingParserDiagnostics, Some existing.Text, None
                    else
                        let ensuredText = ensureChecklistSections workId existing.Text
                        let plannedReviews = plannedChecklistReviews specFacts clarificationFacts (Some existingFacts)

                        let withReviews = appendChecklistReviews ensuredText plannedReviews
                        let stale = sourceSnapshotStale specText clarificationText existingFacts
                        let proposedText = if stale then appendStaleChecklistResult withReviews existingFacts else withReviews

                        match parseChecklistForCommand path proposedText with
                        | Error diagnostics -> diagnostics, Some proposedText, None
                        | Ok(proposedFacts, proposedDiagnostics) ->
                            let staleDiagnostics =
                                if stale then
                                    [ staleChecklistResult path (existingFacts.Results |> List.map (fun result -> result.ResultId.Value)) ]
                                else
                                    []

                            let qualityDiagnostics = checklistQualityDiagnostics (specPath workId) plannedReviews

                            blockingParserDiagnostics @ proposedDiagnostics @ staleDiagnostics @ qualityDiagnostics
                            |> DiagnosticsModule.sort,
                            Some proposedText,
                            Some(checklistSummary proposedFacts)

    let checklistPrerequisiteDiagnosticsTextSummaryAndFacts workId (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) model =
        let path = checklistPath workId

        match snapshot path model with
        | None ->
            [ missingChecklistPrerequisite path $"Checklist prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseChecklistForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedChecklistFrontMatter path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedChecklistFrontMatter path $"Checklist schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          checklistIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Checklist then
                          missingChecklistPrerequisite path $"Checklist stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'checklist'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedChecklistFrontMatter path $"Checklist sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedChecklistFrontMatter path $"Checklist sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'." ]

                let unknownDiagnostics = unknownChecklistReferences path specFacts clarificationFacts (Some facts)

                let readinessDiagnostics =
                    [ if not (String.Equals(facts.FrontMatter.Status, "checklistReady", StringComparison.OrdinalIgnoreCase)) then
                          failedChecklistPrerequisite path $"Checklist status '{facts.FrontMatter.Status}' is not checklistReady." [ facts.FrontMatter.Status ]

                      let failed =
                          facts.Results
                          |> List.filter (fun result -> result.Status = "fail")
                          |> List.map (fun result -> result.ResultId.Value)

                      if not (List.isEmpty failed) then
                          failedChecklistPrerequisite path "Checklist contains failed blocking results." failed

                      let stale =
                          facts.Results
                          |> List.filter (fun result -> result.Status = "stale")
                          |> List.map (fun result -> result.ResultId.Value)

                      if not (List.isEmpty stale) then
                          failedChecklistPrerequisite path "Checklist contains stale review results." stale

                      let findings =
                          facts.BlockingFindings
                          |> List.filter (fun finding -> not (finding.StartsWith("No ", StringComparison.OrdinalIgnoreCase)))

                      if not (List.isEmpty findings) then
                          failedChecklistPrerequisite path "Checklist contains blocking findings." findings ]

                let allDiagnostics = identityDiagnostics @ diagnostics @ unknownDiagnostics @ readinessDiagnostics |> DiagnosticsModule.sort
                allDiagnostics, Some existing.Text, Some(checklistSummary facts), Some facts

    let planSectionText workId heading =
        match heading with
        | "Source Snapshot" -> "## Source Snapshot\n"
        | "Plan Scope" -> "## Plan Scope\nNo additional plan scope recorded.\n"
        | "Plan Decisions" -> "## Plan Decisions\nNo plan decisions recorded.\n"
        | "Contract Impact" -> "## Contract Impact\nNo contract references recorded.\n"
        | "Verification Obligations" -> "## Verification Obligations\nNo verification obligations recorded.\n"
        | "Migration Posture" -> "## Migration Posture\nNo migration notes recorded.\n"
        | "Generated View Impact" -> "## Generated View Impact\nNo generated-view impacts recorded.\n"
        | "Accepted Deferrals" -> "## Accepted Deferrals\nNo accepted plan deferrals recorded.\n"
        | "Planning Findings" -> "## Planning Findings\nNo blocking planning findings recorded.\n"
        | "Advisory Notes" -> "## Advisory Notes\nNo advisory notes recorded.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd tasks --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensurePlanSections workId text =
        let normalized = (if isNull text then "" else text).Replace("\r\n", "\n")

        let missing =
            LifecycleArtifactsModule.planStandardSections ()
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map (planSectionText workId) |> String.concat "\n"
            let trimmed = normalized.TrimEnd()
            $"{trimmed}\n\n{suffix}"

    let planSummary (facts: PlanFacts) : PlanSummary =
        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          SourceClarifications = facts.FrontMatter.SourceClarifications
          SourceChecklist = facts.FrontMatter.SourceChecklist
          DecisionIds = facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value) |> List.sort
          ContractReferenceIds = facts.ContractReferences |> List.map (fun reference -> reference.ContractId.Value) |> List.sort
          VerificationObligationIds = facts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value) |> List.sort
          MigrationNoteIds = facts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value) |> List.sort
          GeneratedViewImpactIds = facts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value) |> List.sort
          AcceptedDeferralCount = facts.AcceptedDeferrals.Length
          StaleDecisionCount = facts.StaleDecisionCount
          BlockingFindingCount = facts.BlockingFindings.Length
          AdvisoryCount = facts.AdvisoryNotes.Length }

    let mapPlanDiagnostics (path: string) (diagnostics: Diagnostic list) =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicatePlanId path id
            | "unknownReference", id :: _ -> unknownPlanSourceReference path id
            | "workModelInconsistent", _
            | "malformedSchemaVersion", _
            | "unsupportedSchemaVersion", _
            | "futureSchemaVersion", _ -> malformedPlanFrontMatter path diagnostic.Message
            | _ -> diagnostic)

    let parsePlanForCommand path text : Result<PlanFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match LifecycleArtifactsModule.parsePlanFacts snapshot with
        | Error diagnostics -> Error(mapPlanDiagnostics path diagnostics)
        | Ok facts ->
            let diagnostics = mapPlanDiagnostics path facts.Diagnostics
            Ok(facts, diagnostics)

    type PlannedPlanEntries =
        { DecisionLines: string list
          ContractLines: string list
          ObligationLines: string list
          MigrationLines: string list
          ImpactLines: string list
          DeferralLines: string list
          FindingLines: string list
          AdvisoryLines: string list }

    let emptyPlanEntries =
        { DecisionLines = []
          ContractLines = []
          ObligationLines = []
          MigrationLines = []
          ImpactLines = []
          DeferralLines = []
          FindingLines = []
          AdvisoryLines = [] }

    let planIdsText (facts: PlanFacts option) =
        facts
        |> Option.map (fun (facts: PlanFacts) ->
            [ facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
              facts.ContractReferences |> List.map (fun reference -> reference.ContractId.Value)
              facts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value)
              facts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value)
              facts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value) ]
            |> List.concat
            |> String.concat "\n")
        |> Option.defaultValue ""

    let requirementScenarioIds (specFacts: SpecificationFacts) (requirementId: string) =
        specFacts.RequirementReferences
        |> List.tryFind (fun reference -> reference.RequirementId.Value = requirementId)
        |> Option.map (fun reference -> reference.AcceptanceScenarioIds |> List.map _.Value)
        |> Option.defaultValue []

    let existingPlanSourceIds (facts: PlanFacts option) : Set<string> =
        match facts with
        | None -> Set.empty
        | Some (facts: PlanFacts) ->
            [ facts.Decisions |> List.collect (fun decision -> decision.SourceIds)
              facts.ContractReferences |> List.collect (fun reference -> reference.SourceIds)
              facts.VerificationObligations |> List.collect (fun obligation -> obligation.SourceIds)
              facts.MigrationNotes |> List.collect (fun note -> note.SourceIds)
              facts.GeneratedViewImpacts |> List.collect (fun impact -> impact.SourceIds)
              facts.AcceptedDeferrals |> List.collect (fun deferral -> deferral.SourceIds) ]
            |> List.concat
            |> Set.ofList

    let lineRefs (ids: string list) =
        ids |> List.distinct |> List.sort |> List.map (fun id -> $"[{id}]") |> String.concat " "

    let plannedPlanEntries workId (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts) (existingFacts: PlanFacts option) =
        let existingSources = existingPlanSourceIds existingFacts
        let nextDecision = ref (nextScopedIndex "PD" (planIdsText existingFacts))
        let nextContract = ref (nextScopedIndex "PC" (planIdsText existingFacts))
        let nextObligation = ref (nextScopedIndex "VO" (planIdsText existingFacts))
        let nextMigration = ref (nextScopedIndex "PM" (planIdsText existingFacts))
        let nextImpact = ref (nextScopedIndex "GV" (planIdsText existingFacts))

        let allocate (prefix: string) (next: int ref) =
            let id = scopedId prefix next.Value
            next.Value <- next.Value + 1
            id

        let requirementDecisionLines =
            specFacts.RequirementIds
            |> List.choose (fun requirement ->
                if Set.contains requirement.Value existingSources then
                    None
                else
                    let id = allocate "PD" nextDecision
                    let scenarios = requirementScenarioIds specFacts requirement.Value
                    let refs = requirement.Value :: scenarios
                    Some $"- {id} {lineRefs refs} complete: Plan requirement {requirement.Value} through the plan command contract.")

        let deferralDecisionLines =
            (clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value))
            @ (checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value))
            |> List.distinct
            |> List.choose (fun sourceId ->
                if Set.contains sourceId existingSources then
                    None
                else
                    let id = allocate "PD" nextDecision
                    Some $"- {id} [{sourceId}] acceptedDeferral: Accepted deferral {sourceId} remains visible to task generation.")

        let firstDecision =
            existingFacts
            |> Option.bind (fun (facts: PlanFacts) -> facts.Decisions |> List.tryHead |> Option.map (fun decision -> decision.DecisionId.Value))
            |> Option.orElseWith (fun () ->
                (requirementDecisionLines @ deferralDecisionLines)
                |> List.tryHead
                |> Option.bind (fun line -> Regex.Match(line, @"\bPD-\d{3,}\b").Value |> function "" -> None | value -> Some value))
            |> Option.defaultValue "PD-001"

        let contractLines =
            if existingFacts |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.ContractReferences)) then
                []
            else
                let id = allocate "PC" nextContract
                [ $"- {id} [{firstDecision}] command report: fsgg-sdd plan, {planPath workId}, and command-report JSON are tool-facing and compatibility-preserving." ]

        let contractId =
            existingFacts
            |> Option.bind (fun (facts: PlanFacts) -> facts.ContractReferences |> List.tryHead |> Option.map (fun contract -> contract.ContractId.Value))
            |> Option.orElseWith (fun () ->
                contractLines
                |> List.tryHead
                |> Option.bind (fun line -> Regex.Match(line, @"\bPC-\d{3,}\b").Value |> function "" -> None | value -> Some value))
            |> Option.defaultValue "PC-001"

        let obligationLines =
            if existingFacts |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.VerificationObligations)) then
                []
            else
                let id = allocate "VO" nextObligation
                [ $"- {id} [{firstDecision}] [{contractId}] semanticTest: Run focused command tests, FSI/prelude evidence, and CLI smoke evidence before task generation." ]

        let migrationLines =
            if existingFacts |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.MigrationNotes)) then
                []
            else
                let id = allocate "PM" nextMigration
                [ $"- {id} [{contractId}] diagnoseOnly: Plan schemaVersion 1 is accepted; unsupported plan schemas diagnose before write." ]

        let impactLines =
            if existingFacts |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.GeneratedViewImpacts)) then
                []
            else
                let id = allocate "GV" nextImpact
                [ $"- {id} [{firstDecision}] workModel: readiness/{workId}/work-model.json refreshes from current plan sources or reports staleGeneratedView." ]

        let deferralLines =
            (clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value))
            @ (checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value))
            |> List.distinct
            |> List.choose (fun sourceId ->
                if Set.contains sourceId existingSources then
                    None
                else
                    Some $"- {sourceId} acceptedDeferral: Deferral remains visible to tasks and evidence.")

        { emptyPlanEntries with
            DecisionLines = requirementDecisionLines @ deferralDecisionLines
            ContractLines = contractLines
            ObligationLines = obligationLines
            MigrationLines = migrationLines
            ImpactLines = impactLines
            DeferralLines = deferralLines }

    let sourceSnapshotLines workId specText clarificationText checklistText planText =
        let lines =
            [ sourceSnapshotLine "spec" (specPath workId) specText
              sourceSnapshotLine "clarifications" (clarificationPath workId) clarificationText
              sourceSnapshotLine "checklist" (checklistPath workId) checklistText ]

        match planText with
        | Some text -> lines @ [ sourceSnapshotLine "plan" (planPath workId) text ]
        | None -> lines

    let planTemplate
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        =
        let title = requestTitle request workId
        let entries = plannedPlanEntries workId specFacts clarificationFacts checklistFacts None
        let decisions = if List.isEmpty entries.DecisionLines then [ "- PD-001 complete: Planning scope is recorded for the selected work item." ] else entries.DecisionLines
        let contracts = if List.isEmpty entries.ContractLines then [ "- PC-001 [PD-001] artifact: No additional contract impact recorded." ] else entries.ContractLines
        let obligations = if List.isEmpty entries.ObligationLines then [ "- VO-001 [PD-001] test: Run focused command tests before tasks." ] else entries.ObligationLines
        let migrations = if List.isEmpty entries.MigrationLines then [ "- PM-001 [PC-001] diagnoseOnly: No migration is required beyond schemaVersion 1 diagnostics." ] else entries.MigrationLines
        let impacts = if List.isEmpty entries.ImpactLines then [ $"- GV-001 [PD-001] workModel: readiness/{workId}/work-model.json records current plan sources." ] else entries.ImpactLines
        let deferrals = if List.isEmpty entries.DeferralLines then [ "No accepted plan deferrals recorded." ] else entries.DeferralLines

        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: plan
changeTier: tier1
status: planned
sourceSpec: {specPath workId}
sourceClarifications: {clarificationPath workId}
sourceChecklist: {checklistPath workId}
publicOrToolFacingImpact: true
---

# {title} Plan

Prose status: planned

## Source Snapshot
{String.concat "\n" (sourceSnapshotLines workId specText clarificationText checklistText None)}

## Plan Scope
- Work item {workId} is planned from the current specification, clarification, and checklist facts.
- Requirement count: {specFacts.RequirementIds.Length}.
- Clarification decision count: {clarificationFacts.Decisions.Length}.
- Checklist result count: {checklistFacts.Results.Length}.

## Plan Decisions
{String.concat "\n" decisions}

## Contract Impact
{String.concat "\n" contracts}

## Verification Obligations
{String.concat "\n" obligations}

## Migration Posture
{String.concat "\n" migrations}

## Generated View Impact
{String.concat "\n" impacts}

## Accepted Deferrals
{String.concat "\n" deferrals}

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work {workId}`.
"""

    let knownPlanSourceIds (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts) (planFacts: PlanFacts) =
        [ specFacts.RequirementIds |> List.map _.Value
          specFacts.UserStoryIds |> List.map _.Value
          specFacts.AcceptanceScenarioIds |> List.map _.Value
          specFacts.ScopeBoundaryIds |> List.map _.Value
          specFacts.AmbiguityIds |> List.map _.Value
          clarificationFacts.Questions |> List.map (fun question -> question.QuestionId.Value)
          clarificationFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value)
          checklistFacts.Items |> List.map (fun item -> item.ItemId.Value)
          checklistFacts.Results |> List.map (fun result -> result.ResultId.Value)
          planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
          planFacts.ContractReferences |> List.map (fun reference -> reference.ContractId.Value)
          planFacts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value)
          planFacts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value)
          planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value) ]
        |> List.concat
        |> Set.ofList

    let unknownPlanReferences path specFacts clarificationFacts checklistFacts planFacts =
        let known = knownPlanSourceIds specFacts clarificationFacts checklistFacts planFacts

        [ planFacts.Decisions |> List.collect (fun decision -> decision.SourceIds)
          planFacts.ContractReferences |> List.collect (fun reference -> reference.SourceIds)
          planFacts.VerificationObligations |> List.collect (fun obligation -> obligation.SourceIds)
          planFacts.MigrationNotes |> List.collect (fun note -> note.SourceIds)
          planFacts.GeneratedViewImpacts |> List.collect (fun impact -> impact.SourceIds)
          planFacts.AcceptedDeferrals |> List.collect (fun deferral -> deferral.SourceIds) ]
        |> List.concat
        |> List.distinct
        |> List.choose (fun id ->
            if Set.contains id known then
                None
            else
                Some(unknownPlanSourceReference path id))

    let planSourceSnapshotStale workId specText clarificationText checklistText (existingFacts: PlanFacts) =
        let current =
            [ specPath workId, (SchemaVersionModule.sha256Text specText).Value
              clarificationPath workId, (SchemaVersionModule.sha256Text clarificationText).Value
              checklistPath workId, (SchemaVersionModule.sha256Text checklistText).Value ]
            |> Map.ofList

        existingFacts.SourceSnapshots
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path current with
            | Some recorded, Some actual -> not (String.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
            | _ -> false)

    let appendPlanEntries existingText entries =
        existingText
        |> appendToSection "Plan Decisions" entries.DecisionLines
        |> appendToSection "Contract Impact" entries.ContractLines
        |> appendToSection "Verification Obligations" entries.ObligationLines
        |> appendToSection "Migration Posture" entries.MigrationLines
        |> appendToSection "Generated View Impact" entries.ImpactLines
        |> appendToSection "Accepted Deferrals" entries.DeferralLines
        |> appendToSection "Planning Findings" entries.FindingLines
        |> appendToSection "Advisory Notes" entries.AdvisoryLines

    let appendStalePlanDecision existingText (facts: PlanFacts) =
        if facts.Decisions |> List.exists (fun decision -> decision.Status = "stale") then
            existingText
        else
            let decisionId = scopedId "PD" (nextScopedIndex "PD" existingText)
            let sourceDecision =
                facts.Decisions
                |> List.tryHead
                |> Option.map (fun decision -> $"[{decision.DecisionId.Value}] ")
                |> Option.defaultValue ""

            let line = $"- {decisionId} {sourceDecision}stale: Source specification, clarification, or checklist facts changed since prior plan decisions were recorded."
            appendToSection "Plan Decisions" [ line ] existingText

    let planDiagnosticsTextAndSummary
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        model
        =
        let path = planPath workId

        match snapshot path model with
        | None ->
            let text = planTemplate request workId specText clarificationText checklistText specFacts clarificationFacts checklistFacts

            match parsePlanForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let unknownDiagnostics = unknownPlanReferences path specFacts clarificationFacts checklistFacts facts
                diagnostics @ unknownDiagnostics |> DiagnosticsModule.sort, Some text, Some(planSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            elif existing.Text.Contains("<!-- fsgg-sdd: unsafe-decision-change -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafePlanDecisionChange path "PD-001" ], Some existing.Text, None
            else
                match parsePlanForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        [ if existingFacts.FrontMatter.SchemaVersion.Major <> 1 then
                              malformedPlanFrontMatter path $"Plan schemaVersion '{existingFacts.FrontMatter.SchemaVersion.Major}' is not supported."
                          if not (String.Equals(existingFacts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                              planIdentityMismatch path workId existingFacts.FrontMatter.WorkId.Value
                          if existingFacts.FrontMatter.Stage <> LifecycleStage.Plan then
                              malformedPlanFrontMatter path $"Plan stage '{IdentifiersModule.stageValue existingFacts.FrontMatter.Stage}' is not 'plan'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedPlanFrontMatter path $"Plan sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedPlanFrontMatter path $"Plan sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceChecklist, checklistPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedPlanFrontMatter path $"Plan sourceChecklist '{existingFacts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'." ]

                    let unknownDiagnostics = unknownPlanReferences path specFacts clarificationFacts checklistFacts existingFacts
                    let blockingParserDiagnostics = identityDiagnostics @ existingDiagnostics @ unknownDiagnostics |> DiagnosticsModule.sort
                    let hasBlockingParserDiagnostics = blockingParserDiagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        blockingParserDiagnostics, Some existing.Text, Some(planSummary existingFacts)
                    else
                        let ensuredText = ensurePlanSections workId existing.Text
                        let entries = plannedPlanEntries workId specFacts clarificationFacts checklistFacts (Some existingFacts)
                        let withEntries = appendPlanEntries ensuredText entries
                        let stale = planSourceSnapshotStale workId specText clarificationText checklistText existingFacts
                        let proposedText = if stale then appendStalePlanDecision withEntries existingFacts else withEntries

                        match parsePlanForCommand path proposedText with
                        | Error diagnostics -> diagnostics, Some proposedText, None
                        | Ok(proposedFacts, proposedDiagnostics) ->
                            let staleDiagnostics =
                                if stale then
                                    [ stalePlanDecision path (existingFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)) ]
                                else
                                    []

                            let unknownDiagnostics = unknownPlanReferences path specFacts clarificationFacts checklistFacts proposedFacts

                            blockingParserDiagnostics @ proposedDiagnostics @ unknownDiagnostics @ staleDiagnostics
                            |> DiagnosticsModule.sort,
                            Some proposedText,
                            Some(planSummary proposedFacts)

    let planPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts model =
        let path = planPath workId

        match snapshot path model with
        | None ->
            [ missingPlanPrerequisite path $"Plan prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parsePlanForCommand path existing.Text with
            | Error diagnostics ->
                let mapped = diagnostics |> List.map (fun diagnostic -> malformedPlanFrontMatter path diagnostic.Message)
                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedPlanFrontMatter path $"Plan schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          planIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Plan then
                          missingPlanPrerequisite path $"Plan stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'plan'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedPlanFrontMatter path $"Plan sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedPlanFrontMatter path $"Plan sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceChecklist, checklistPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedPlanFrontMatter path $"Plan sourceChecklist '{facts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'." ]

                let unknownDiagnostics = unknownPlanReferences path specFacts clarificationFacts checklistFacts facts

                let readinessDiagnostics =
                    [ if not (String.Equals(facts.FrontMatter.Status, "planned", StringComparison.OrdinalIgnoreCase)) then
                          failedPlanPrerequisite path $"Plan status '{facts.FrontMatter.Status}' is not planned." [ facts.FrontMatter.Status ]

                      let stale =
                          facts.Decisions
                          |> List.filter (fun decision -> decision.Status = "stale")
                          |> List.map (fun decision -> decision.DecisionId.Value)

                      if not (List.isEmpty stale) then
                          failedPlanPrerequisite path "Plan contains stale decisions." stale

                      let incomplete =
                          facts.Decisions
                          |> List.filter (fun decision -> decision.Status = "incomplete")
                          |> List.map (fun decision -> decision.DecisionId.Value)

                      if not (List.isEmpty incomplete) then
                          failedPlanPrerequisite path "Plan contains incomplete decisions." incomplete

                      let findings =
                          facts.BlockingFindings
                          |> List.filter (fun finding -> not (finding.StartsWith("No ", StringComparison.OrdinalIgnoreCase)))

                      if not (List.isEmpty findings) then
                          failedPlanPrerequisite path "Plan contains blocking planning findings." findings ]

                let allDiagnostics =
                    identityDiagnostics @ diagnostics @ unknownDiagnostics @ readinessDiagnostics
                    |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(planSummary facts), Some facts

    let tasksSummary (facts: TaskFacts) : TasksSummary =
        let statusCount predicate =
            facts.Tasks |> List.filter (fun task -> predicate task.Status) |> List.length

        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          SourceClarifications = facts.FrontMatter.SourceClarifications
          SourceChecklist = facts.FrontMatter.SourceChecklist
          SourcePlan = facts.FrontMatter.SourcePlan
          TaskIds = facts.Tasks |> List.map (fun task -> task.Id.Value) |> List.sort
          DependencyCount = facts.Tasks |> List.sumBy (fun task -> task.Dependencies.Length)
          RequiredSkillCount = facts.Tasks |> List.collect (fun task -> task.RequiredSkills) |> List.distinct |> List.length
          RequiredEvidenceCount = facts.Tasks |> List.collect (fun task -> task.RequiredEvidence |> List.map _.Value) |> List.distinct |> List.length
          PendingCount = statusCount ((=) TaskStatus.Pending)
          InProgressCount = statusCount ((=) TaskStatus.InProgress)
          DoneCount = statusCount ((=) TaskStatus.Done)
          SkippedCount =
            facts.Tasks
            |> List.filter (fun task ->
                match task.Status with
                | TaskStatus.Skipped _ -> true
                | _ -> false)
            |> List.length
          StaleCount = facts.StaleTaskCount
          AcceptedDeferralCount = facts.AcceptedDeferrals.Length
          BlockingFindingCount = facts.Findings |> List.filter (fun finding -> finding.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)) |> List.length
          AdvisoryCount = facts.AdvisoryNotes.Length }

    let parseTasksForCommand path text : Result<TaskFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match LifecycleArtifactsModule.parseTaskFacts snapshot with
        | Error diagnostics -> Error(diagnostics |> List.map (fun diagnostic -> malformedTasksArtifact path diagnostic.Message))
        | Ok facts ->
            let diagnostics =
                facts.Diagnostics
                |> List.map (fun diagnostic ->
                    match diagnostic.Id, diagnostic.RelatedIds with
                    | "workModelInconsistent", id :: _ when id.StartsWith("T", StringComparison.OrdinalIgnoreCase) -> duplicateTaskId path id
                    | "duplicateIdentifier", id :: _ -> duplicateTaskId path id
                    | _ -> diagnostic)

            Ok(facts, diagnostics)

    let yamlString (value: string) =
        let text = if isNull value then "" else value
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    let yamlInlineList (values: string list) =
        match values |> List.distinct |> List.sort with
        | [] -> "[]"
        | values -> values |> List.map yamlString |> String.concat ", " |> fun text -> $"[{text}]"

    let taskStatusYaml (status: TaskStatus) =
        match status with
        | TaskStatus.Pending -> "pending"
        | TaskStatus.InProgress -> "in-progress"
        | TaskStatus.Done -> "done"
        | TaskStatus.Skipped _ -> "skipped"
        | TaskStatus.Stale -> "stale"

    let taskEvidenceId index =
        match IdentifiersModule.createEvidenceId (sprintf "EV%03d" index) with
        | Ok id -> id
        | Error message -> failwith message

    let taskArtifactRef workId =
        match FS.GG.SDD.Artifacts.ArtifactRef.create (tasksPath workId) ArtifactKind.Tasks ArtifactOwner.Sdd true with
        | Ok artifact -> artifact
        | Error message -> failwith message

    let taskId index =
        match IdentifiersModule.createTaskId (sprintf "T%03d" index) with
        | Ok id -> id
        | Error message -> failwith message

    let taskIdNumber (id: TaskId) =
        let digits = Regex.Match(id.Value, @"\d+").Value

        match Int32.TryParse digits with
        | true, value -> value
        | _ -> 0

    let nextTaskNumber (existing: WorkTask list) =
        existing |> List.map (fun task -> taskIdNumber task.Id) |> List.fold max 0 |> (+) 1

    let plannedTask
        (workId: string)
        (sourceIds: string list)
        (title: string)
        (requirements: RequirementId list)
        (decisions: DecisionId list)
        (dependencies: TaskId list)
        (skills: string list)
        evidenceIndex
        idIndex
        : WorkTask =
        { Id = taskId idIndex
          Title = title
          Status = TaskStatus.Pending
          Owner = "sdd"
          Dependencies = dependencies
          Requirements = requirements
          Decisions = decisions
          SourceIds = sourceIds |> List.distinct |> List.sort
          RequiredSkills = skills |> List.distinct |> List.sort
          RequiredEvidence = [ taskEvidenceId evidenceIndex ]
          Source = taskArtifactRef workId
          SourceLocation = None }

    let plannedTasks
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (existingFacts: TaskFacts option)
        =
        let existingSources =
            existingFacts
            |> Option.map (fun facts -> facts.Tasks |> List.collect (fun task -> task.SourceIds) |> Set.ofList)
            |> Option.defaultValue Set.empty

        let existingTasks : WorkTask list =
            existingFacts |> Option.map (fun (facts: TaskFacts) -> facts.Tasks) |> Option.defaultValue []
        let mutable nextId = nextTaskNumber existingTasks
        let mutable evidenceIndex = nextId

        let allocate
            (sourceIds: string list)
            (title: string)
            (requirements: RequirementId list)
            (decisions: DecisionId list)
            (dependencies: TaskId list)
            (skills: string list)
            : WorkTask =
            let id = nextId
            nextId <- nextId + 1
            let evidence = evidenceIndex
            evidenceIndex <- evidenceIndex + 1
            plannedTask planFacts.FrontMatter.WorkId.Value sourceIds title requirements decisions dependencies skills evidence id

        let maybeTask
            (sourceIds: string list)
            (title: string)
            (requirements: RequirementId list)
            (decisions: DecisionId list)
            (dependencies: TaskId list)
            (skills: string list)
            : WorkTask option =
            let key = sourceIds |> List.tryHead

            match key with
            | Some key when Set.contains key existingSources -> None
            | _ -> Some(allocate sourceIds title requirements decisions dependencies skills)

        let requirementTasks : WorkTask list =
            specFacts.RequirementIds
            |> List.choose (fun requirement ->
                let acceptance =
                    specFacts.RequirementReferences
                    |> List.filter (fun reference -> reference.RequirementId.Value = requirement.Value)
                    |> List.collect (fun reference -> reference.AcceptanceScenarioIds |> List.map _.Value)
                    |> List.distinct
                    |> List.sort

                maybeTask
                    (requirement.Value :: acceptance)
                    $"Implement requirement {requirement.Value}"
                    [ requirement ]
                    []
                    []
                    [ "fsharp"; "speckit-implement" ])

        let primaryDependency : TaskId list =
            existingTasks
            |> List.tryHead
            |> Option.map (fun task -> [ task.Id ])
            |> Option.orElseWith (fun () -> requirementTasks |> List.tryHead |> Option.map (fun task -> [ task.Id ]))
            |> Option.defaultValue []

        let planDecisionTasks =
            planFacts.Decisions
            |> List.choose (fun decision ->
                maybeTask
                    (decision.DecisionId.Value :: decision.SourceIds)
                    $"Implement plan decision {decision.DecisionId.Value}"
                    []
                    []
                    primaryDependency
                    [ "fsharp"; "speckit-implement" ])

        let contractTasks =
            planFacts.ContractReferences
            |> List.choose (fun contract ->
                maybeTask
                    (contract.ContractId.Value :: contract.SourceIds)
                    $"Update contract surface {contract.ContractId.Value}"
                    []
                    []
                    primaryDependency
                    [ "fsharp" ])

        let obligationTasks =
            planFacts.VerificationObligations
            |> List.choose (fun obligation ->
                maybeTask
                    (obligation.ObligationId.Value :: obligation.SourceIds)
                    $"Record verification evidence {obligation.ObligationId.Value}"
                    []
                    []
                    primaryDependency
                    [ "xunit"; "readiness-evidence" ])

        let migrationTasks =
            planFacts.MigrationNotes
            |> List.choose (fun migration ->
                maybeTask
                    (migration.MigrationId.Value :: migration.SourceIds)
                    $"Handle migration posture {migration.MigrationId.Value}"
                    []
                    []
                    primaryDependency
                    [ "schema-versioning" ])

        let generatedViewTasks =
            planFacts.GeneratedViewImpacts
            |> List.choose (fun impact ->
                maybeTask
                    (impact.ImpactId.Value :: impact.SourceIds)
                    $"Refresh generated view impact {impact.ImpactId.Value}"
                    []
                    []
                    primaryDependency
                    [ "deterministic-json" ])

        let deferralTasks =
            [ clarificationFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.DecisionId.Value)
              checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value)
              planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
            |> List.concat
            |> List.distinct
            |> List.choose (fun id ->
                maybeTask
                    [ id ]
                    $"Keep accepted deferral {id} visible"
                    []
                    []
                    primaryDependency
                    [ "traceability" ])

        requirementTasks
        @ planDecisionTasks
        @ contractTasks
        @ obligationTasks
        @ migrationTasks
        @ generatedViewTasks
        @ deferralTasks

    let currentTaskSourceDigests workId specText clarificationText checklistText planText =
        [ "spec", specPath workId, specText
          "clarifications", clarificationPath workId, clarificationText
          "checklist", checklistPath workId, checklistText
          "plan", planPath workId, planText ]
        |> List.map (fun (label, path, text) -> label, path, (SchemaVersionModule.sha256Text text).Value)

    let taskSourceSnapshotStale workId specText clarificationText checklistText planText (existingFacts: TaskFacts) =
        let current =
            currentTaskSourceDigests workId specText clarificationText checklistText planText
            |> List.map (fun (_, path, digest) -> path, digest)
            |> Map.ofList

        existingFacts.SourceSnapshots
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path current with
            | Some recorded, Some actual -> not (String.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
            | _ -> false)

    let markTasksStale (tasks: WorkTask list) : WorkTask list =
        tasks
        |> List.map (fun task ->
            match task.Status with
            | TaskStatus.Done
            | TaskStatus.Skipped _ -> task
            | TaskStatus.Stale -> task
            | TaskStatus.Pending
            | TaskStatus.InProgress -> { task with Status = TaskStatus.Stale })

    let taskFrontMatterText request workId =
        let title = requestTitle request workId

        $"""schemaVersion: 1
work:
  id: {workId}
  title: {yamlString title}
  stage: tasks
  status: tasksReady
  sourceSpec: {specPath workId}
  sourceClarifications: {clarificationPath workId}
  sourceChecklist: {checklistPath workId}
  sourcePlan: {planPath workId}
  publicOrToolFacingImpact: true
"""

    let renderTaskSourceSnapshots workId specText clarificationText checklistText planText =
        currentTaskSourceDigests workId specText clarificationText checklistText planText
        |> List.map (fun (label, path, digest) ->
            $"""  - label: {label}
    path: {path}
    digest: {digest}
    schemaVersion: 1""")
        |> String.concat "\n"

    let renderTask (task: WorkTask) =
        let skip =
            match task.Status with
            | TaskStatus.Skipped rationale -> $"\n    skipRationale: {yamlString rationale}"
            | _ -> ""

        let dependencyIds = task.Dependencies |> List.map (fun (id: TaskId) -> id.Value)
        let requirementIds = task.Requirements |> List.map (fun (id: RequirementId) -> id.Value)
        let decisionIds = task.Decisions |> List.map (fun (id: DecisionId) -> id.Value)
        let evidenceIds = task.RequiredEvidence |> List.map (fun (id: EvidenceId) -> id.Value)

        $"""  - id: {task.Id.Value}
    title: {yamlString task.Title}
    status: {taskStatusYaml task.Status}
    owner: {yamlString task.Owner}
    dependencies: {dependencyIds |> yamlInlineList}
    requirements: {requirementIds |> yamlInlineList}
    decisions: {decisionIds |> yamlInlineList}
    sourceIds: {task.SourceIds |> yamlInlineList}
    requiredSkills: {task.RequiredSkills |> yamlInlineList}
    requiredEvidence: {evidenceIds |> yamlInlineList}{skip}"""

    let renderFindingsBlock (findings: TaskGraphFinding list) =
        match findings with
        | [] -> "findings: []"
        | findings ->
            let lines =
                findings
                |> List.map (fun (finding: TaskGraphFinding) ->
                    $"""  - id: {finding.FindingId}
    severity: {finding.Severity}
    text: {yamlString finding.Text}
    sourceIds: {finding.SourceIds |> yamlInlineList}""")
                |> String.concat "\n"

            $"findings:\n{lines}"

    let renderScalarBlock (name: string) (values: string list) =
        match values |> List.distinct |> List.sort with
        | [] -> $"{name}: []"
        | values ->
            let lines = values |> List.map (fun value -> $"  - {yamlString value}") |> String.concat "\n"
            $"{name}:\n{lines}"

    let tasksArtifactText
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (tasks: WorkTask list)
        (acceptedDeferrals: string list)
        (findings: TaskGraphFinding list)
        (advisoryNotes: string list)
        (lifecycleNotes: string list)
        =
        let taskLines =
            match tasks with
            | [] -> "[]"
            | tasks -> tasks |> List.sortBy (fun task -> task.Id.Value) |> List.map renderTask |> String.concat "\n"

        let lifecycle =
            if List.isEmpty lifecycleNotes then
                [ $"Next lifecycle action: fsgg-sdd analyze --work {workId}." ]
            else
                lifecycleNotes

        $"""{taskFrontMatterText request workId}
sources:
{renderTaskSourceSnapshots workId specText clarificationText checklistText planText}
tasks:
{taskLines}
{renderScalarBlock "acceptedDeferrals" acceptedDeferrals}
{renderFindingsBlock findings}
{renderScalarBlock "advisoryNotes" advisoryNotes}
{renderScalarBlock "lifecycleNotes" lifecycle}
"""

    let knownTaskSourceIds (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts) (planFacts: PlanFacts) =
        [ specFacts.RequirementIds |> List.map (fun id -> id.Value)
          specFacts.UserStoryIds |> List.map (fun id -> id.Value)
          specFacts.AcceptanceScenarioIds |> List.map (fun id -> id.Value)
          specFacts.ScopeBoundaryIds |> List.map (fun id -> id.Value)
          specFacts.AmbiguityIds |> List.map (fun id -> id.Value)
          clarificationFacts.Questions |> List.map (fun (question: ClarificationQuestion) -> question.QuestionId.Value)
          clarificationFacts.Decisions |> List.map (fun (decision: ClarificationDecisionFact) -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals |> List.map (fun (decision: ClarificationDecisionFact) -> decision.DecisionId.Value)
          checklistFacts.Items |> List.map (fun (item: ChecklistItem) -> item.ItemId.Value)
          checklistFacts.Results |> List.map (fun (result: ChecklistReviewResult) -> result.ResultId.Value)
          planFacts.Decisions |> List.map (fun (decision: PlanDecision) -> decision.DecisionId.Value)
          planFacts.ContractReferences |> List.map (fun (reference: PlanContractReference) -> reference.ContractId.Value)
          planFacts.VerificationObligations |> List.map (fun (obligation: VerificationObligation) -> obligation.ObligationId.Value)
          planFacts.MigrationNotes |> List.map (fun (migration: PlanMigrationNote) -> migration.MigrationId.Value)
          planFacts.GeneratedViewImpacts |> List.map (fun (impact: GeneratedViewImpact) -> impact.ImpactId.Value)
          planFacts.AcceptedDeferrals |> List.map (fun (deferral: AcceptedPlanDeferral) -> deferral.Id) ]
        |> List.concat
        |> Set.ofList

    let taskDependencyCycleDiagnostics path (tasks: WorkTask list) =
        let dependencyMap =
            tasks
            |> List.map (fun task -> task.Id.Value, (task.Dependencies |> List.map (fun (id: TaskId) -> id.Value)))
            |> Map.ofList

        let rec visit trail id =
            if List.contains id trail then
                Some(List.rev (id :: trail))
            else
                dependencyMap
                |> Map.tryFind id
                |> Option.defaultValue []
                |> List.tryPick (visit (id :: trail))

        dependencyMap
        |> Map.toList
        |> List.choose (fun (id, _) -> visit [] id)
        |> List.distinct
        |> List.map (taskDependencyCycle path)

    let taskValidationDiagnostics
        (path: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (evidence: EvidenceDeclaration list)
        (facts: TaskFacts)
        : Diagnostic list =
        let knownTasks = facts.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList
        let knownSources = knownTaskSourceIds specFacts clarificationFacts checklistFacts planFacts
        let evidenceTaskRefs = evidence |> List.collect (fun evidence -> evidence.TaskRefs |> List.map (fun (id: TaskId) -> id.Value)) |> Set.ofList

        let duplicateDiagnostics =
            facts.Diagnostics
            |> List.map (fun diagnostic ->
                match diagnostic.Id, diagnostic.RelatedIds with
                | "duplicateTaskId", _ -> diagnostic
                | _, id :: _ when id.StartsWith("T", StringComparison.OrdinalIgnoreCase) -> duplicateTaskId path id
                | _ -> malformedTasksArtifact path diagnostic.Message)

        let unknownSources =
            facts.Tasks
            |> List.collect (fun task -> task.SourceIds)
            |> List.distinct
            |> List.choose (fun id ->
                if Set.contains id knownSources then
                    None
                else
                    Some(unknownTaskSourceReference path id))

        let unknownDependencies =
            facts.Tasks
            |> List.collect (fun task -> task.Dependencies |> List.map (fun (id: TaskId) -> id.Value))
            |> List.distinct
            |> List.choose (fun id ->
                if Set.contains id knownTasks then
                    None
                else
                    Some(unknownTaskDependency path id))

        let selfDependencies =
            facts.Tasks
            |> List.choose (fun task ->
                if task.Dependencies |> List.exists (fun dep -> dep.Value = task.Id.Value) then
                    Some(taskDependencyCycle path [ task.Id.Value; task.Id.Value ])
                else
                    None)

        let skippedWithoutRationale =
            facts.Tasks
            |> List.choose (fun task ->
                match task.Status with
                | TaskStatus.Skipped rationale when String.IsNullOrWhiteSpace rationale || rationale = "No rationale provided." -> Some task.Id.Value
                | _ -> None)

        let doneMissingEvidence =
            facts.Tasks
            |> List.choose (fun task ->
                match task.Status with
                | TaskStatus.Done when not (Set.contains task.Id.Value evidenceTaskRefs) -> Some task.Id.Value
                | _ -> None)

        [ duplicateDiagnostics
          unknownSources
          unknownDependencies
          selfDependencies
          taskDependencyCycleDiagnostics path facts.Tasks
          if not (List.isEmpty skippedWithoutRationale) then [ skippedTaskMissingRationale path skippedWithoutRationale ] else []
          if not (List.isEmpty doneMissingEvidence) then [ doneTaskMissingEvidence path doneMissingEvidence ] else [] ]
        |> List.concat
        |> DiagnosticsModule.sort

    let parseEvidenceForCommand (workId: string) model : EvidenceDeclaration list * Diagnostic list =
        match snapshot (evidencePath workId) model with
        | None -> [], []
        | Some snapshot ->
            match LifecycleArtifactsModule.parseEvidence snapshot with
            | Ok evidence -> evidence, []
            | Error diagnostics -> [], diagnostics

    let tasksDiagnosticsTextAndSummary
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        model
        =
        let path = tasksPath workId
        let evidence, evidenceDiagnostics = parseEvidenceForCommand workId model

        match snapshot path model with
        | None ->
            let tasks = plannedTasks specFacts clarificationFacts checklistFacts planFacts None
            let acceptedDeferrals =
                [ clarificationFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.DecisionId.Value)
                  checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value)
                  planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
                |> List.concat
                |> List.distinct
                |> List.sort

            let advisory = [ "Optional Governance pointers remain compatibility facts only." ]
            let text = tasksArtifactText request workId specText clarificationText checklistText planText tasks acceptedDeferrals [] advisory []

            match parseTasksForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let validationDiagnostics = taskValidationDiagnostics path specFacts clarificationFacts checklistFacts planFacts evidence facts
                diagnostics @ validationDiagnostics @ evidenceDiagnostics |> DiagnosticsModule.sort, Some text, Some(tasksSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            elif existing.Text.Contains("<!-- fsgg-sdd: unsafe-status-change -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeTaskStatusChange path "T001" ], Some existing.Text, None
            else
                match parseTasksForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        [ if existingFacts.FrontMatter.SchemaVersion.Major <> 1 then
                              malformedTasksArtifact path $"Tasks schemaVersion '{existingFacts.FrontMatter.SchemaVersion.Major}' is not supported."
                          if not (String.Equals(existingFacts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                              tasksIdentityMismatch path workId existingFacts.FrontMatter.WorkId.Value
                          if existingFacts.FrontMatter.Stage <> LifecycleStage.Tasks then
                              malformedTasksArtifact path $"Tasks stage '{IdentifiersModule.stageValue existingFacts.FrontMatter.Stage}' is not 'tasks'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedTasksArtifact path $"Tasks sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedTasksArtifact path $"Tasks sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourceChecklist, checklistPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedTasksArtifact path $"Tasks sourceChecklist '{existingFacts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'."
                          if not (String.Equals(normalizeRelativePath existingFacts.FrontMatter.SourcePlan, planPath workId, StringComparison.OrdinalIgnoreCase)) then
                              malformedTasksArtifact path $"Tasks sourcePlan '{existingFacts.FrontMatter.SourcePlan}' does not match '{planPath workId}'." ]

                    let hasBlockingParserDiagnostics =
                        identityDiagnostics @ existingDiagnostics
                        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        identityDiagnostics @ existingDiagnostics |> DiagnosticsModule.sort, Some existing.Text, Some(tasksSummary existingFacts)
                    else
                        let additions = plannedTasks specFacts clarificationFacts checklistFacts planFacts (Some existingFacts)
                        let stale = taskSourceSnapshotStale workId specText clarificationText checklistText planText existingFacts
                        let existingTasks = if stale then markTasksStale existingFacts.Tasks else existingFacts.Tasks
                        let mergedTasks = existingTasks @ additions
                        let staleFindings =
                            if stale then
                                [ { FindingId = "TF-001"
                                    Severity = "warning"
                                    Text = "Task source snapshots are stale."
                                    SourceIds = existingFacts.Tasks |> List.map (fun task -> task.Id.Value) |> List.sort
                                    SourceLocation = None } ]
                            else
                                []

                        let text =
                            if List.isEmpty additions && not stale then
                                existing.Text
                            else
                                tasksArtifactText
                                    request
                                    workId
                                    specText
                                    clarificationText
                                    checklistText
                                    planText
                                    mergedTasks
                                    existingFacts.AcceptedDeferrals
                                    (existingFacts.Findings @ staleFindings)
                                    existingFacts.AdvisoryNotes
                                    existingFacts.LifecycleNotes

                        match parseTasksForCommand path text with
                        | Error diagnostics -> diagnostics, Some text, None
                        | Ok(facts, proposedDiagnostics) ->
                            let validationDiagnostics = taskValidationDiagnostics path specFacts clarificationFacts checklistFacts planFacts evidence facts
                            let staleDiagnostics =
                                if stale then
                                    [ staleTask path (existingFacts.Tasks |> List.map (fun task -> task.Id.Value)) ]
                                else
                                    []

                            identityDiagnostics
                            @ existingDiagnostics
                            @ proposedDiagnostics
                            @ validationDiagnostics
                            @ staleDiagnostics
                            @ evidenceDiagnostics
                            |> DiagnosticsModule.sort,
                            Some text,
                            Some(tasksSummary facts)

    let tasksPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts planFacts model =
        let path = tasksPath workId
        let evidence, evidenceDiagnostics = parseEvidenceForCommand workId model

        match snapshot path model with
        | None -> [ missingTasksPrerequisite path $"Tasks prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseTasksForCommand path existing.Text with
            | Error diagnostics -> diagnostics, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    [ if facts.FrontMatter.SchemaVersion.Major <> 1 then
                          malformedTasksArtifact path $"Tasks schemaVersion '{facts.FrontMatter.SchemaVersion.Major}' is not supported."
                      if not (String.Equals(facts.FrontMatter.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
                          tasksIdentityMismatch path workId facts.FrontMatter.WorkId.Value
                      if facts.FrontMatter.Stage <> LifecycleStage.Tasks then
                          missingTasksPrerequisite path $"Tasks stage '{IdentifiersModule.stageValue facts.FrontMatter.Stage}' is not 'tasks'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceSpec, specPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedTasksArtifact path $"Tasks sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceClarifications, clarificationPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedTasksArtifact path $"Tasks sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourceChecklist, checklistPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedTasksArtifact path $"Tasks sourceChecklist '{facts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'."
                      if not (String.Equals(normalizeRelativePath facts.FrontMatter.SourcePlan, planPath workId, StringComparison.OrdinalIgnoreCase)) then
                          malformedTasksArtifact path $"Tasks sourcePlan '{facts.FrontMatter.SourcePlan}' does not match '{planPath workId}'."
                      if not (String.Equals(facts.FrontMatter.Status, "tasksReady", StringComparison.OrdinalIgnoreCase)) then
                          failedTasksPrerequisite path $"Tasks status '{facts.FrontMatter.Status}' is not tasksReady." [ facts.FrontMatter.Status ] ]

                let staleDiagnostics =
                    if taskSourceSnapshotStale workId "" "" "" "" facts then
                        []
                    else
                        []

                let taskDiagnostics = taskValidationDiagnostics path specFacts clarificationFacts checklistFacts planFacts evidence facts

                let graphDiagnostics =
                    [ let staleIds =
                          facts.Tasks
                          |> List.filter (fun task -> task.Status = TaskStatus.Stale)
                          |> List.map (fun task -> task.Id.Value)

                      if not (List.isEmpty staleIds) then
                          staleTask path staleIds

                      let blockingFindings =
                          facts.Findings
                          |> List.filter (fun finding -> finding.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
                          |> List.map (fun finding -> finding.FindingId)

                      if not (List.isEmpty blockingFindings) then
                          failedTasksPrerequisite path "Tasks contain blocking findings." blockingFindings ]

                let allDiagnostics =
                    identityDiagnostics @ diagnostics @ staleDiagnostics @ taskDiagnostics @ graphDiagnostics @ evidenceDiagnostics
                    |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(tasksSummary facts), Some facts

    let allTaskDispositionIds (facts: TaskFacts) =
        [ facts.Tasks |> List.collect (fun task -> task.SourceIds)
          facts.Tasks |> List.collect (fun task -> task.Requirements |> List.map _.Value)
          facts.Tasks |> List.collect (fun task -> task.Decisions |> List.map _.Value)
          facts.AcceptedDeferrals ]
        |> List.concat
        |> List.map (fun value -> value.ToUpperInvariant())
        |> Set.ofList

    let missingDispositionDiagnostics workId (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts) (planFacts: PlanFacts) (taskFacts: TaskFacts) =
        let dispositions = allTaskDispositionIds taskFacts

        let required =
            [ specFacts.RequirementIds |> List.map _.Value
              specFacts.AcceptanceScenarioIds |> List.map _.Value
              clarificationFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
              clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value)
              checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value)
              planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
              planFacts.ContractReferences |> List.map (fun contract -> contract.ContractId.Value)
              planFacts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value)
              planFacts.MigrationNotes |> List.map (fun migration -> migration.MigrationId.Value)
              planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value)
              planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
            |> List.concat
            |> List.distinct
            |> List.sort

        let missing =
            required
            |> List.filter (fun id -> not (Set.contains (id.ToUpperInvariant()) dispositions))

        if List.isEmpty missing then [] else [ missingDisposition (tasksPath workId) missing ]

    type AnalysisRelationshipDraft =
        { SourcePath: string
          TargetPath: string
          SourceId: string option
          TargetId: string option
          Relationship: string
          State: string
          DiagnosticIds: string list }

    let relationship
        (sourcePath: string)
        (targetPath: string)
        (sourceId: string option)
        (targetId: string option)
        (relationship: string)
        (state: string)
        (diagnosticIds: string list)
        : AnalysisRelationshipDraft
        =
        { SourcePath = sourcePath
          TargetPath = targetPath
          SourceId = sourceId
          TargetId = targetId
          Relationship = relationship
          State = state
          DiagnosticIds = diagnosticIds }

    let analysisRelationships workId (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) (checklistFacts: ChecklistFacts) (planFacts: PlanFacts) (taskFacts: TaskFacts) =
        let taskDispositionIds = allTaskDispositionIds taskFacts

        let dispositionRelationships (sourcePath: string) (relationshipName: string) (ids: string list) =
            ids
            |> List.map (fun id ->
                let current = Set.contains (id.ToUpperInvariant()) taskDispositionIds
                relationship sourcePath (tasksPath workId) (Some id) None relationshipName (if current then "current" else "missing") (if current then [] else [ "missingDisposition" ]))

        [ [ relationship (specPath workId) (clarificationPath workId) None None "sourceSpec" "current" []
            relationship (specPath workId) (checklistPath workId) None None "checklistSourceSpec" "current" []
            relationship (clarificationPath workId) (checklistPath workId) None None "checklistSourceClarifications" "current" []
            relationship (specPath workId) (planPath workId) None None "planSourceSpec" "current" []
            relationship (clarificationPath workId) (planPath workId) None None "planSourceClarifications" "current" []
            relationship (checklistPath workId) (planPath workId) None None "planSourceChecklist" "current" []
            relationship (specPath workId) (tasksPath workId) None None "taskSourceSpec" "current" []
            relationship (clarificationPath workId) (tasksPath workId) None None "taskSourceClarifications" "current" []
            relationship (checklistPath workId) (tasksPath workId) None None "taskSourceChecklist" "current" []
            relationship (planPath workId) (tasksPath workId) None None "taskSourcePlan" "current" [] ]
          dispositionRelationships (specPath workId) "requirementDisposition" (specFacts.RequirementIds |> List.map _.Value)
          dispositionRelationships (specPath workId) "acceptanceDisposition" (specFacts.AcceptanceScenarioIds |> List.map _.Value)
          dispositionRelationships (clarificationPath workId) "clarificationDisposition" (clarificationFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value))
          dispositionRelationships (checklistPath workId) "checklistDeferralDisposition" (checklistFacts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value))
          dispositionRelationships (planPath workId) "planDecisionDisposition" (planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value))
          dispositionRelationships (planPath workId) "contractDisposition" (planFacts.ContractReferences |> List.map (fun contract -> contract.ContractId.Value))
          dispositionRelationships (planPath workId) "verificationDisposition" (planFacts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value))
          dispositionRelationships (planPath workId) "migrationDisposition" (planFacts.MigrationNotes |> List.map (fun migration -> migration.MigrationId.Value))
          dispositionRelationships (planPath workId) "generatedViewDisposition" (planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value))
          taskFacts.Tasks
          |> List.collect (fun task ->
              task.Dependencies
              |> List.map (fun dependency -> relationship (tasksPath workId) (tasksPath workId) (Some task.Id.Value) (Some dependency.Value) "taskDependency" "current" [])) ]
        |> List.concat
        |> List.sortBy (fun relationship -> relationship.SourcePath, relationship.Relationship, relationship.SourceId, relationship.TargetId)

    let analysisSourceFromSnapshot (path: string) (text: string) : GeneratedViewSource =
        { Path = path
          Digest = Some(SchemaVersionModule.sha256Text text)
          SchemaVersion = Some 1
          SchemaStatus = Some "current" }

    let analysisSources workId workModelJson specText clarificationText checklistText planText tasksText model : GeneratedViewSource list =
        [ snapshot ".fsgg/project.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/sdd.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/agents.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          Some(analysisSourceFromSnapshot (specPath workId) specText)
          Some(analysisSourceFromSnapshot (clarificationPath workId) clarificationText)
          Some(analysisSourceFromSnapshot (checklistPath workId) checklistText)
          Some(analysisSourceFromSnapshot (planPath workId) planText)
          Some(analysisSourceFromSnapshot (tasksPath workId) tasksText)
          workModelJson |> Option.map (analysisSourceFromSnapshot (workModelPath workId)) ]
        |> List.choose id
        |> List.sortBy (fun source -> source.Path)

    let analysisSourceKind (path: string) =
        if path = ".fsgg/project.yml" then "projectConfig"
        elif path = ".fsgg/sdd.yml" then "sddConfig"
        elif path = ".fsgg/agents.yml" then "agentsConfig"
        elif path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then "specification"
        elif path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then "clarification"
        elif path.EndsWith("/checklist.md", StringComparison.OrdinalIgnoreCase) then "checklist"
        elif path.EndsWith("/plan.md", StringComparison.OrdinalIgnoreCase) then "plan"
        elif path.EndsWith("/tasks.yml", StringComparison.OrdinalIgnoreCase) then "tasks"
        elif path.EndsWith("/work-model.json", StringComparison.OrdinalIgnoreCase) then "workModel"
        else "source"

    let writeStringArray (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray(name)
        values |> List.sort |> List.iter (fun value -> writer.WriteStringValue(value: string))
        writer.WriteEndArray()

    let writeDigestObject (writer: Utf8JsonWriter) (name: string) (digest: SourceDigest option) =
        match digest with
        | Some digest ->
            writer.WriteStartObject(name)
            writer.WriteString("algorithm", digest.Algorithm)
            writer.WriteString("value", digest.Value)
            writer.WriteEndObject()
        | None -> writer.WriteNull name

    let writeAnalysisDiagnosticJson (writer: Utf8JsonWriter) (diagnostic: Diagnostic) =
        writer.WriteStartObject()
        writer.WriteString("id", diagnostic.Id)
        writer.WriteString("severity", DiagnosticsModule.severityValue diagnostic.Severity)
        match diagnostic.Artifact with
        | Some artifact -> writer.WriteString("artifact", artifact.Path)
        | None -> writer.WriteNull "artifact"
        writer.WriteString("message", diagnostic.Message)
        writer.WriteString("correction", diagnostic.Correction)
        writeStringArray writer "relatedIds" diagnostic.RelatedIds
        writer.WriteEndObject()

    let analysisFindingSeverity (diagnostic: Diagnostic) =
        match diagnostic.Id with
        | "missingDisposition" -> "missingDisposition"
        | "staleTask"
        | "stalePlanDecision"
        | "staleChecklistResult"
        | "staleGeneratedView" -> "staleSource"
        | "malformedGeneratedView"
        | "malformedAnalysisView"
        | "malformedTasksArtifact"
        | "malformedPlanFrontMatter"
        | "malformedChecklistFrontMatter"
        | "malformedClarificationFrontMatter"
        | "malformedSpecificationFacts"
        | "malformedSpecificationFrontMatter" -> "malformedSource"
        | "blockedGeneratedViewRefresh" -> "generatedView"
        | _ ->
            match diagnostic.Severity with
            | DiagnosticSeverity.DiagnosticError -> "blocking"
            | DiagnosticSeverity.DiagnosticWarning -> "warning"
            | DiagnosticSeverity.DiagnosticInfo -> "advisory"

    let analysisFindingCategory severity =
        match severity with
        | "missingDisposition" -> "missingDisposition"
        | "staleSource" -> "staleSource"
        | "malformedSource" -> "malformedSource"
        | "generatedView" -> "generatedView"
        | "blocking" -> "blocking"
        | "warning" -> "warning"
        | _ -> "advisory"

    let diagnosticPath (diagnostic: Diagnostic) =
        diagnostic.Artifact |> Option.map _.Path |> Option.defaultValue ""

    let analysisFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic ->
            let severity = analysisFindingSeverity diagnostic
            let id = sprintf "AF%03d" (index + 1)
            id, diagnostic, severity)

    let countFindings severity (findings: (string * Diagnostic * string) list) =
        findings |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity) |> List.length

    let analysisReadiness (acceptedDeferralCount: int) (relationships: AnalysisRelationshipDraft list) (diagnostics: Diagnostic list) =
        let findings = analysisFindings diagnostics
        let blockingCount = countFindings "blocking" findings
        let missingDispositionCount = countFindings "missingDisposition" findings
        let staleSourceCount = countFindings "staleSource" findings
        let malformedSourceCount = countFindings "malformedSource" findings
        let generatedViewFindingCount = countFindings "generatedView" findings
        let warningCount = countFindings "warning" findings
        let advisoryCount = countFindings "advisory" findings + acceptedDeferralCount

        let status =
            if blockingCount > 0 || missingDispositionCount > 0 || malformedSourceCount > 0 then "needsCorrection"
            elif staleSourceCount > 0 || generatedViewFindingCount > 0 then "needsGeneratedViewRefresh"
            else "implementationReady"

        { WorkId = ""
          Stage = "analyze"
          Status = status
          AnalysisPath = ""
          SourceCount = 0
          SourceRelationshipCount = List.length relationships
          ReadyFindingCount = if status = "implementationReady" then List.length relationships else 0
          AdvisoryCount = advisoryCount
          WarningCount = warningCount
          BlockingCount = blockingCount
          StaleSourceCount = staleSourceCount
          MissingDispositionCount = missingDispositionCount
          MalformedSourceCount = malformedSourceCount
          GeneratedViewFindingCount = generatedViewFindingCount
          AcceptedDeferralCount = acceptedDeferralCount
          Readiness = status }

    let analysisGeneratedViewState
        (path: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (outputDigest: OutputDigest option)
        (currency: GeneratedViewCurrency)
        (diagnosticIds: string list)
        : GeneratedViewState
        =
        { Path = path
          Kind = "analysis"
          SchemaVersion = Some 1
          Generator = Some generator
          Sources = sources |> List.sortBy _.Path
          OutputDigest = outputDigest
          Currency = currency
          DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }

    let analysisBoundaryFacts () : GovernanceCompatibilityFact list =
        [ { Path = ".fsgg/policy.yml"; Relationship = "optionalGovernancePolicy"; RequiredBySdd = false; State = "notEvaluated"; DiagnosticIds = [] }
          { Path = ".fsgg/capabilities.yml"; Relationship = "optionalGovernanceCapabilities"; RequiredBySdd = false; State = "notEvaluated"; DiagnosticIds = [] }
          { Path = ".fsgg/tooling.yml"; Relationship = "optionalGovernanceTooling"; RequiredBySdd = false; State = "notEvaluated"; DiagnosticIds = [] } ]

    let analysisJson
        (workId: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (relationships: AnalysisRelationshipDraft list)
        (readiness: AnalysisSummary)
        (diagnostics: Diagnostic list)
        (generatedViews: GeneratedViewState list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        let findings = analysisFindings diagnostics

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("stage", "analyze")
        writer.WriteString("status", readiness.Readiness)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")
        writer.WriteStartArray("sources")
        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            writer.WriteStartObject()
            writer.WriteString("path", source.Path)
            writer.WriteString("kind", analysisSourceKind source.Path)
            writeDigestObject writer "digest" source.Digest
            match source.SchemaVersion with
            | Some version -> writer.WriteNumber("schemaVersion", version)
            | None -> writer.WriteNull "schemaVersion"
            match source.SchemaStatus with
            | Some status -> writer.WriteString("schemaStatus", status)
            | None -> writer.WriteNull "schemaStatus"
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("sourceRelationships")
        relationships
        |> List.mapi (fun index relationship -> sprintf "AR%03d" (index + 1), relationship)
        |> List.iter (fun (id, relationship) ->
            writer.WriteStartObject()
            writer.WriteString("id", id)
            writer.WriteString("sourcePath", relationship.SourcePath)
            writer.WriteString("targetPath", relationship.TargetPath)
            match relationship.SourceId with
            | Some value -> writer.WriteString("sourceId", value)
            | None -> writer.WriteNull "sourceId"
            match relationship.TargetId with
            | Some value -> writer.WriteString("targetId", value)
            | None -> writer.WriteNull "targetId"
            writer.WriteString("relationship", relationship.Relationship)
            writer.WriteString("state", relationship.State)
            writeStringArray writer "diagnosticIds" relationship.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartObject("readiness")
        writer.WriteString("status", readiness.Readiness)
        writer.WriteNumber("readyCount", readiness.ReadyFindingCount)
        writer.WriteNumber("advisoryCount", readiness.AdvisoryCount)
        writer.WriteNumber("warningCount", readiness.WarningCount)
        writer.WriteNumber("blockingCount", readiness.BlockingCount)
        writer.WriteNumber("staleSourceCount", readiness.StaleSourceCount)
        writer.WriteNumber("missingDispositionCount", readiness.MissingDispositionCount)
        writer.WriteNumber("malformedSourceCount", readiness.MalformedSourceCount)
        writer.WriteNumber("generatedViewFindingCount", readiness.GeneratedViewFindingCount)
        writer.WriteNumber("acceptedDeferralCount", readiness.AcceptedDeferralCount)
        writer.WriteEndObject()
        writer.WriteStartArray("findings")
        findings
        |> List.iter (fun (id, diagnostic, severity) ->
            writer.WriteStartObject()
            writer.WriteString("id", id)
            writer.WriteString("category", analysisFindingCategory severity)
            writer.WriteString("severity", severity)
            writer.WriteString("state", if severity = "ready" then "closed" else "open")
            writer.WriteString("path", diagnosticPath diagnostic)
            writeStringArray writer "relatedIds" diagnostic.RelatedIds
            writer.WriteString("message", diagnostic.Message)
            writer.WriteString("correction", diagnostic.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("generatedViews")
        generatedViews
        |> List.sortBy (fun view -> view.Path)
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("path", view.Path)
            writer.WriteString("kind", view.Kind)
            writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("optionalBoundaryFacts")
        analysisBoundaryFacts()
        |> List.iter (fun fact ->
            writer.WriteStartObject()
            writer.WriteString("path", fact.Path)
            writer.WriteString("relationship", fact.Relationship)
            writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
            writer.WriteString("state", fact.State)
            writeStringArray writer "diagnosticIds" fact.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        diagnostics |> DiagnosticsModule.sort |> List.iter (writeAnalysisDiagnosticJson writer)
        writer.WriteEndArray()
        writer.WriteStartObject("nextAction")
        if readiness.Readiness = "implementationReady" then
            writer.WriteString("actionId", "analysis.next.implement")
            writer.WriteNull("command")
            writer.WriteString("reason", "Lifecycle sources are current and ready for implementation.")
        else
            writer.WriteString("actionId", "correctBlockingDiagnostics")
            writer.WriteNull("command")
            writer.WriteString("reason", "Analysis found lifecycle diagnostics that must be corrected before implementation.")
        writer.WriteEndObject()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let existingAnalysisDiagnostic workId model =
        let path = analysisPath workId

        match snapshot path model with
        | None -> None
        | Some existing ->
            match LifecycleArtifactsModule.parseAnalysisView existing with
            | Error diagnostics ->
                diagnostics
                |> List.tryHead
                |> Option.map (fun diagnostic -> malformedAnalysisView path diagnostic.Message)
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                Some(analysisIdentityMismatch path workId view.WorkId.Value)
            | Ok _ -> None

    let analysisPlan
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (tasksText: string)
        (workModelJson: string option)
        (relationships: AnalysisRelationshipDraft list)
        (diagnostics: Diagnostic list)
        (generatedViews: GeneratedViewState list)
        (model: CommandModel)
        =
        let path = analysisPath workId
        let sources = analysisSources workId workModelJson specText clarificationText checklistText planText tasksText model
        let acceptedDeferralCount =
            diagnostics
            |> List.collect (fun diagnostic -> diagnostic.RelatedIds)
            |> List.filter (fun id -> id.StartsWith("DEC-", StringComparison.OrdinalIgnoreCase) || id.StartsWith("CR-", StringComparison.OrdinalIgnoreCase))
            |> List.distinct
            |> List.length

        let readiness = analysisReadiness acceptedDeferralCount relationships diagnostics
        let summary =
            { readiness with
                WorkId = workId
                AnalysisPath = path
                SourceCount = List.length sources }

        let text = analysisJson workId model.Request.GeneratorVersion sources relationships summary diagnostics generatedViews
        let outputDigest = SchemaVersionModule.outputSha256Text text
        let view = analysisGeneratedViewState path model.Request.GeneratorVersion sources (Some outputDigest) GeneratedViewCurrency.Current []
        summary, text, view

    let sourceFromEntry (entry: SourceEntry) =
        { Path = entry.Path
          Digest = Some entry.SourceDigest
          SchemaVersion = if entry.SchemaVersion <= 0 then None else Some entry.SchemaVersion
          SchemaStatus = Some entry.SchemaStatus }

    let charterSource path text =
        { Path = path
          Digest = Some(SchemaVersionModule.sha256Text text)
          SchemaVersion = Some 1
          SchemaStatus = Some "current" }

    let generatedViewState path generator sources outputDigest currency diagnosticIds =
        { Path = path
          Kind = "workModel"
          SchemaVersion = Some 1
          Generator = Some generator
          Sources = sources |> List.sortBy _.Path
          OutputDigest = outputDigest
          Currency = currency
          DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }

    let existingGeneratedViewDiagnostic workId path model =
        match snapshot path model with
        | None -> None
        | Some generated ->
            match GenerationManifestModule.parseWorkModelMetadata path generated.Text with
            | Error _ -> Some(malformedGeneratedView path)
            | Ok _ ->
                let currentSnapshots =
                    [ snapshot ".fsgg/project.yml" model
                      snapshot ".fsgg/sdd.yml" model
                      snapshot ".fsgg/agents.yml" model
                      snapshot (specPath workId) model
                      snapshot (clarificationPath workId) model
                      snapshot (checklistPath workId) model
                      snapshot (tasksPath workId) model
                      snapshot (evidencePath workId) model
                      Some generated ]
                    |> List.choose id

                match SerializationModule.checkGeneratedWorkModelCurrency currentSnapshots workId model.Request.GeneratorVersion with
                | [] -> None
                | _ ->
                    Some(
                        commandDiagnostic
                            "staleGeneratedView"
                            DiagnosticSeverity.DiagnosticWarning
                            (Some path)
                            $"Generated view '{path}' is stale."
                            "Regenerate readiness/<id>/work-model.json from current lifecycle sources."
                            [ path ])

    let workModelSnapshots workId charterText specText clarificationText checklistText planText tasksText evidenceText model =
        [ snapshot ".fsgg/project.yml" model
          snapshot ".fsgg/sdd.yml" model
          snapshot ".fsgg/agents.yml" model
          specText
          |> Option.map (fun text -> { Path = specPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (specPath workId) model)
          clarificationText
          |> Option.map (fun text -> { Path = clarificationPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (clarificationPath workId) model)
          checklistText
          |> Option.map (fun text -> { Path = checklistPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (checklistPath workId) model)
          planText
          |> Option.map (fun text -> { Path = planPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (planPath workId) model)
          tasksText
          |> Option.map (fun text -> { Path = tasksPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (tasksPath workId) model)
          evidenceText
          |> Option.map (fun text -> { Path = evidencePath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (evidencePath workId) model)
          charterText
          |> Option.map (fun text -> { Path = charterPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (charterPath workId) model) ]
        |> List.choose id
        |> List.map (fun snapshot -> { snapshot with Path = normalizeRelativePath snapshot.Path })

    let generatedViewPlan
        (request: CommandRequest)
        (workId: string)
        (charterText: string option)
        (specText: string option)
        (clarificationText: string option)
        (checklistText: string option)
        (planText: string option)
        (tasksText: string option)
        (evidenceText: string option)
        (commandDiagnostics: Diagnostic list)
        (model: CommandModel)
        =
        let path = workModelPath workId
        let currentDiagnostic = existingGeneratedViewDiagnostic workId path model
        let blockingCommandIds =
            commandDiagnostics
            |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            |> List.map _.Id

        if not (List.isEmpty blockingCommandIds) then
            let sources =
                [ charterText |> Option.map (fun text -> charterSource (charterPath workId) text)
                  specText |> Option.map (fun text -> charterSource (specPath workId) text)
                  clarificationText |> Option.map (fun text -> charterSource (clarificationPath workId) text)
                  checklistText |> Option.map (fun text -> charterSource (checklistPath workId) text)
                  planText |> Option.map (fun text -> charterSource (planPath workId) text)
                  tasksText |> Option.map (fun text -> charterSource (tasksPath workId) text) ]
                |> List.choose id

            let view = generatedViewState path request.GeneratorVersion sources None GeneratedViewCurrency.Blocked blockingCommandIds
            currentDiagnostic |> Option.toList, view, []
        else
            let snapshots = workModelSnapshots workId charterText specText clarificationText checklistText planText tasksText evidenceText model

            let result =
                SerializationModule.generateWorkModel
                    { WorkId = workId
                      Snapshots = snapshots
                      GeneratorVersion = request.GeneratorVersion
                      ExpectedOutputPath = Some path }

            let blockingModelDiagnostics = WorkModelModule.blockingDiagnostics result.Model

            if List.isEmpty blockingModelDiagnostics then
                let sources = result.Model.Sources |> List.map sourceFromEntry
                let diagnosticIds = currentDiagnostic |> Option.map (fun diagnostic -> [ diagnostic.Id ]) |> Option.defaultValue []
                let view = generatedViewState path request.GeneratorVersion sources (Some result.OutputDigest) GeneratedViewCurrency.Current diagnosticIds
                let effects = [ CreateDirectory(readinessDirectory workId); WriteFile(path, result.Json, GeneratedView) ]
                currentDiagnostic |> Option.toList, view, effects
            else
                let existing = snapshot path model
                let currency =
                    match existing, currentDiagnostic with
                    | None, _ -> GeneratedViewCurrency.Missing
                    | Some _, Some diagnostic when diagnostic.Id = "malformedGeneratedView" -> GeneratedViewCurrency.Malformed
                    | Some _, Some diagnostic when diagnostic.Id = "staleGeneratedView" -> GeneratedViewCurrency.Stale
                    | Some _, _ -> GeneratedViewCurrency.Blocked

                let diagnostic =
                    match existing with
                    | None -> None
                    | Some _ -> Some(blockedGeneratedViewRefresh path (blockingModelDiagnostics |> List.map _.Id))

                let diagnostics = [ currentDiagnostic; diagnostic ] |> List.choose id
                let diagnosticIds = diagnostics |> List.map _.Id
                let sources =
                    [ charterText |> Option.map (fun text -> charterSource (charterPath workId) text)
                      specText |> Option.map (fun text -> charterSource (specPath workId) text)
                      clarificationText |> Option.map (fun text -> charterSource (clarificationPath workId) text)
                      checklistText |> Option.map (fun text -> charterSource (checklistPath workId) text)
                      planText |> Option.map (fun text -> charterSource (planPath workId) text)
                      tasksText |> Option.map (fun text -> charterSource (tasksPath workId) text) ]
                    |> List.choose id

                let view = generatedViewState path request.GeneratorVersion sources None currency diagnosticIds
                diagnostics, view, []

    let charterWriteEffects workId text =
        [ CreateDirectory($"work/{workId}")
          WriteFile(charterPath workId, text, AuthoredSource) ]

    let computeCharterPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let charterDiagnostics, charterText = charterDiagnosticsAndText model.Request workId model
            let commandDiagnostics = projectDiagnostics @ duplicateDiagnostics @ charterDiagnostics |> DiagnosticsModule.sort
            let generatedDiagnostics, generatedView, generatedEffects = generatedViewPlan model.Request workId (Some charterText) None None None None None None commandDiagnostics model
            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let effects =
                if hasBlocking then
                    []
                else
                    charterWriteEffects workId charterText @ generatedEffects

            diagnostics, None, [ generatedView ], effects

    let computeSpecifyPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let charterDiagnostics, charterText = charterPrerequisiteDiagnosticsAndText workId model
            let specificationDiagnostics, specText, specification = specificationDiagnosticsTextAndSummary model.Request workId model
            let commandDiagnostics = projectDiagnostics @ duplicateDiagnostics @ charterDiagnostics @ specificationDiagnostics |> DiagnosticsModule.sort

            let generatedDiagnostics, generatedView, generatedEffects =
                match charterText with
                | Some text -> generatedViewPlan model.Request workId (Some text) specText None None None None None commandDiagnostics model
                | None ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let specificationEffects =
                match specText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(specPath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then
                    []
                else
                    specificationEffects @ generatedEffects

            diagnostics, specification, [ generatedView ], effects

    let computeClarifyPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

            let clarificationDiagnostics, clarificationText, clarification =
                match specFacts with
                | Some facts -> clarificationDiagnosticsTextAndSummary model.Request workId facts model
                | None -> [], None, None

            let commandDiagnostics =
                projectDiagnostics @ duplicateDiagnostics @ specificationDiagnostics @ clarificationDiagnostics
                |> DiagnosticsModule.sort

            let generatedDiagnostics, generatedView, generatedEffects =
                match specText with
                | Some text ->
                    let charterText = snapshot (charterPath workId) model |> Option.map _.Text
                    generatedViewPlan model.Request workId charterText (Some text) clarificationText None None None None commandDiagnostics model
                | None ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let clarificationEffects =
                match clarificationText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(clarificationPath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then
                    []
                else
                    clarificationEffects @ generatedEffects

            diagnostics, specification, clarification, [ generatedView ], effects

    let computeChecklistPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
            let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

            let checklistDiagnostics, checklistText, checklist =
                match specText, clarificationText, specFacts, clarificationFacts with
                | Some specText, Some clarificationText, Some specFacts, Some clarificationFacts ->
                    checklistDiagnosticsTextAndSummary model.Request workId specText clarificationText specFacts clarificationFacts model
                | _ -> [], None, None

            let commandDiagnostics =
                projectDiagnostics @ duplicateDiagnostics @ specificationDiagnostics @ clarificationDiagnostics @ checklistDiagnostics
                |> DiagnosticsModule.sort

            let generatedDiagnostics, generatedView, generatedEffects =
                match specText, clarificationText with
                | Some specText, Some clarificationText ->
                    let charterText = snapshot (charterPath workId) model |> Option.map _.Text
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) checklistText None None None commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let checklistEffects =
                match checklistText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(checklistPath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then
                    []
                else
                    checklistEffects @ generatedEffects

            diagnostics, specification, clarification, checklist, [ generatedView ], effects

    let computePlanPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
            let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

            let checklistDiagnostics, checklistText, checklist, checklistFacts =
                match specFacts, clarificationFacts with
                | Some specFacts, Some clarificationFacts ->
                    checklistPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts model
                | _ -> [], None, None, None

            let planDiagnostics, planText, plan =
                match specText, clarificationText, checklistText, specFacts, clarificationFacts, checklistFacts with
                | Some specText, Some clarificationText, Some checklistText, Some specFacts, Some clarificationFacts, Some checklistFacts ->
                    planDiagnosticsTextAndSummary model.Request workId specText clarificationText checklistText specFacts clarificationFacts checklistFacts model
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
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) planText None None commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let planEffects =
                match planText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(planPath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then
                    []
                else
                    planEffects @ generatedEffects

            diagnostics, specification, clarification, checklist, plan, [ generatedView ], effects

    let computeTasksPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
            let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

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

            let taskDiagnostics, taskText, tasks =
                match specText, clarificationText, checklistText, planText, specFacts, clarificationFacts, checklistFacts, planFacts with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts ->
                    tasksDiagnosticsTextAndSummary model.Request workId specText clarificationText checklistText planText specFacts clarificationFacts checklistFacts planFacts model
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
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) (Some planText) taskText None commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let taskEffects =
                match taskText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(tasksPath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then
                    []
                else
                    taskEffects @ generatedEffects

            diagnostics, specification, clarification, checklist, plan, tasks, [ generatedView ], effects

    let workModelJsonFromGeneratedEffects workId effects model =
        effects
        |> List.tryPick (function
            | WriteFile(path, text, GeneratedView) when normalizeRelativePath path = workModelPath workId -> Some text
            | _ -> None)
        |> Option.orElseWith (fun () -> snapshot (workModelPath workId) model |> Option.map _.Text)

    let computeAnalyzePlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
            let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

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
                    tasksPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts planFacts model
                | _ -> [], None, None, None

            let dispositionDiagnostics =
                match specFacts, clarificationFacts, checklistFacts, planFacts, taskFacts with
                | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts, Some taskFacts ->
                    missingDispositionDiagnostics workId specFacts clarificationFacts checklistFacts planFacts taskFacts
                | _ -> []

            let analysisViewDiagnostics = existingAnalysisDiagnostic workId model |> Option.toList

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
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) (Some planText) (Some taskText) None commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let analysisSummary, analysisView, analysisEffects =
                match specText, clarificationText, checklistText, planText, taskText, specFacts, clarificationFacts, checklistFacts, planFacts, taskFacts with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText, Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts, Some taskFacts ->
                    let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model
                    let relationships = analysisRelationships workId specFacts clarificationFacts checklistFacts planFacts taskFacts
                    let generatedViewsForAnalysis = [ workModelView ]
                    let summary, text, view = analysisPlan workId specText clarificationText checklistText planText taskText workModelJson relationships diagnostics generatedViewsForAnalysis model
                    let effects =
                        if hasBlocking then
                            []
                        else
                            [ CreateDirectory(readinessDirectory workId)
                              WriteFile(analysisPath workId, text, GeneratedView) ]

                    Some summary, Some view, effects
                | _ ->
                    None, None, []

            let generatedViews = [ Some workModelView; analysisView ] |> List.choose id

            let effects =
                if hasBlocking then
                    []
                else
                    workModelEffects @ analysisEffects

            diagnostics, specification, clarification, checklist, plan, tasks, analysisSummary, generatedViews, effects

    type EvidenceDispositionDraft =
        { ObligationId: string
          State: string
          EvidenceIds: string list
          TaskIds: string list
          DiagnosticIds: string list }

    let evidenceKindSourceValue kind =
        match kind with
        | EvidenceKind.Implementation -> "implementation"
        | EvidenceKind.Verification -> "verification"
        | EvidenceKind.Review -> "review"
        | EvidenceKind.GeneratedViewEvidence -> "generated-view"
        | EvidenceKind.Synthetic -> "synthetic"
        | EvidenceKind.Deferral -> "deferral"
        | EvidenceKind.Note -> "note"
        | EvidenceKind.Missing -> "missing"

    let allowedEvidenceResults =
        [ "pass"; "fail"; "deferred"; "missing"; "stale"; "advisory"; "blocked" ] |> Set.ofList

    let normalizedEvidenceResult (result: string) =
        (if isNull result then "" else result.Trim().ToLowerInvariant())

    let evidenceAnalysisSummary path (view: AnalysisView) : AnalysisSummary =
        { WorkId = view.WorkId.Value
          Stage = IdentifiersModule.stageValue view.Stage
          Status = view.Status
          AnalysisPath = path
          SourceCount = view.Sources.Length
          SourceRelationshipCount = view.SourceRelationships.Length
          ReadyFindingCount = view.Readiness.ReadyCount
          AdvisoryCount = view.Readiness.AdvisoryCount
          WarningCount = view.Readiness.WarningCount
          BlockingCount = view.Readiness.BlockingCount
          StaleSourceCount = view.Readiness.StaleSourceCount
          MissingDispositionCount = view.Readiness.MissingDispositionCount
          MalformedSourceCount = view.Readiness.MalformedSourceCount
          GeneratedViewFindingCount = view.Readiness.GeneratedViewFindingCount
          AcceptedDeferralCount = view.Readiness.AcceptedDeferralCount
          Readiness = view.Readiness.Status }

    let analysisPrerequisiteDiagnosticsSummaryAndText workId model =
        let path = analysisPath workId

        match snapshot path model with
        | None -> [ missingAnalysisPrerequisite path $"Analysis prerequisite '{path}' is missing." ], None, None
        | Some existing ->
            match LifecycleArtifactsModule.parseAnalysisView existing with
            | Error diagnostics ->
                diagnostics |> List.map (fun diagnostic -> malformedAnalysisView path diagnostic.Message), Some existing.Text, None
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ analysisIdentityMismatch path workId view.WorkId.Value ], Some existing.Text, Some(evidenceAnalysisSummary path view)
            | Ok view when not (String.Equals(view.Readiness.Status, "implementationReady", StringComparison.OrdinalIgnoreCase)) ->
                [ analysisNotReady path view.Readiness.Status ], Some existing.Text, Some(evidenceAnalysisSummary path view)
            | Ok view -> [], Some existing.Text, Some(evidenceAnalysisSummary path view)

    let mapEvidenceDiagnostics path (diagnostics: Diagnostic list) : Diagnostic list =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateEvidenceId path id
            | "workModelInconsistent", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "malformedSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "unsupportedSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "futureSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | _ -> diagnostic)

    let parseEvidenceArtifactForCommand path text : Result<EvidenceArtifact * Diagnostic list, Diagnostic list> =
        match LifecycleArtifactsModule.parseEvidenceArtifact { Path = path; Text = text } with
        | Ok artifact -> Ok(artifact, mapEvidenceDiagnostics path artifact.Diagnostics)
        | Error diagnostics -> Error(mapEvidenceDiagnostics path diagnostics)

    let parseExistingEvidence workId (model: CommandModel) : EvidenceArtifact option * Diagnostic list * string option =
        let path = evidencePath workId

        snapshot path model
        |> Option.map (fun snapshot ->
            match parseEvidenceArtifactForCommand path snapshot.Text with
            | Ok(artifact, diagnostics) -> Some artifact, diagnostics, Some snapshot.Text
            | Error diagnostics -> None, diagnostics, Some snapshot.Text)
        |> Option.defaultValue (None, [], None)

    let parseInputEvidence workId (request: CommandRequest) : EvidenceArtifact option * Diagnostic list =
        let path = evidencePath workId

        request.InputText
        |> Option.map (fun text ->
            match parseEvidenceArtifactForCommand path text with
            | Ok(artifact, diagnostics) -> Some artifact, diagnostics
            | Error diagnostics -> None, diagnostics)
        |> Option.defaultValue (None, [])

    let evidenceSourceSnapshot label path text : EvidenceSourceSnapshot =
        { Label = label
          Path = path
          Digest = Some((SchemaVersionModule.sha256Text text).Value)
          SchemaVersion = Some 1
          SourceLocation = None }

    let currentEvidenceSourceSnapshots workId specText clarificationText checklistText planText tasksText analysisText : EvidenceSourceSnapshot list =
        [ evidenceSourceSnapshot "spec" (specPath workId) specText
          evidenceSourceSnapshot "clarifications" (clarificationPath workId) clarificationText
          evidenceSourceSnapshot "checklist" (checklistPath workId) checklistText
          evidenceSourceSnapshot "plan" (planPath workId) planText
          evidenceSourceSnapshot "tasks" (tasksPath workId) tasksText
          evidenceSourceSnapshot "analysis" (analysisPath workId) analysisText ]

    let evidenceSourceSnapshotStale (current: EvidenceSourceSnapshot list) (recorded: EvidenceSourceSnapshot list) =
        let currentMap =
            current
            |> List.choose (fun snapshot -> snapshot.Digest |> Option.map (fun digest -> snapshot.Path, digest))
            |> Map.ofList

        recorded
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path currentMap with
            | Some recordedDigest, Some currentDigest -> not (String.Equals(recordedDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
            | Some _, None -> true
            | _ -> false)

    let declarationMeaningKey (declaration: EvidenceDeclaration) =
        (evidenceKindSourceValue declaration.Kind,
         declaration.Subject.SubjectType,
         declaration.Subject.Id,
         declaration.TaskRefs |> List.map _.Value |> List.sort,
         declaration.RequirementRefs |> List.map _.Value |> List.sort,
         declaration.ObligationRefs |> List.sort,
         declaration.SourceRefs |> List.map (fun source -> source.Kind, source.Path, source.Uri, source.Result) |> List.sort,
         normalizedEvidenceResult declaration.Result,
         declaration.Synthetic,
         declaration.SyntheticDisclosure |> Option.map (fun disclosure -> disclosure.StandsInFor, disclosure.Reason),
         declaration.Rationale,
         declaration.Owner,
         declaration.Scope,
         declaration.LaterLifecycleVisibility)

    let evidenceObligations (taskFacts: TaskFacts) : EvidenceObligation list =
        taskFacts.Tasks
        |> List.collect (fun task ->
            let ids =
                if List.isEmpty task.RequiredEvidence && task.Status = TaskStatus.Done then
                    [ $"task.{task.Id.Value}.completion" ]
                else
                    task.RequiredEvidence |> List.map _.Value

            ids
            |> List.map (fun id ->
                { ObligationId = id
                  Kind = "taskEvidence"
                  SourceArtifactPath = task.Source.Path
                  SourceId = Some task.Id.Value
                  LinkedTaskIds = [ task.Id ]
                  LinkedRequirementIds = task.Requirements
                  LinkedDecisionIds = task.Decisions |> List.map _.Value
                  ExpectedEvidenceKinds = [ "implementation"; "verification"; "deferral"; "synthetic" ]
                  RequiredSkillOrCapabilityTags = task.RequiredSkills
                  Blocking = true
                  Correction = $"Add evidence declaration {id} or an accepted deferral linked to {task.Id.Value}." }))

    let skeletonEvidenceDeclaration workId (obligation: EvidenceObligation) =
        let evidenceId =
            match IdentifiersModule.createEvidenceId obligation.ObligationId with
            | Ok id -> id
            | Error _ -> taskEvidenceId 1

        let taskRefs = obligation.LinkedTaskIds
        let subject =
            match taskRefs with
            | task :: _ -> { SubjectType = "task"; Id = task.Value }
            | [] -> { SubjectType = "obligation"; Id = obligation.ObligationId }

        { Id = evidenceId
          Kind = EvidenceKind.Missing
          Subject = subject
          TaskRefs = taskRefs
          RequirementRefs = obligation.LinkedRequirementIds
          AcceptanceScenarioRefs = []
          ClarificationDecisionRefs = []
          ChecklistResultRefs = []
          PlanDecisionRefs = []
          ObligationRefs = [ obligation.ObligationId ]
          ArtifactRefs = []
          SourceRefs = []
          Result = "missing"
          Synthetic = false
          SyntheticDisclosure = None
          Rationale = None
          Owner = None
          Scope = None
          LaterLifecycleVisibility = None
          Notes = [ "Evidence required before verify." ]
          Source =
            match FS.GG.SDD.Artifacts.ArtifactRef.create (evidencePath workId) ArtifactKind.Evidence ArtifactOwner.Sdd true with
            | Ok artifact -> artifact
            | Error message -> failwith message
          SourceLocation = None }

    let mergeEvidenceArtifacts
        (workId: string)
        (existing: EvidenceArtifact option)
        (input: EvidenceArtifact option)
        (obligations: EvidenceObligation list)
        : EvidenceArtifact * Diagnostic list =
        match existing, input with
        | Some existingArtifact, Some inputArtifact ->
            let existingById =
                existingArtifact.Evidence
                |> List.map (fun declaration -> declaration.Id.Value, declaration)
                |> Map.ofList

            let mutable unsafeIds = []

            let additions : EvidenceDeclaration list =
                inputArtifact.Evidence
                |> List.choose (fun declaration ->
                    match Map.tryFind declaration.Id.Value existingById with
                    | None -> Some declaration
                    | Some existingDeclaration ->
                        if declarationMeaningKey declaration = declarationMeaningKey existingDeclaration then
                            None
                        else
                            unsafeIds <- declaration.Id.Value :: unsafeIds
                            None)

            let diagnostics =
                if List.isEmpty unsafeIds then [] else [ unsafeEvidenceUpdate (evidencePath workId) (unsafeIds |> List.distinct |> List.sort) ]

            ({ existingArtifact with Evidence = (existingArtifact.Evidence @ additions) |> List.sortBy (fun declaration -> declaration.Id.Value) } : EvidenceArtifact), diagnostics
        | Some existingArtifact, None -> existingArtifact, []
        | None, Some inputArtifact -> inputArtifact, []
        | None, None ->
            let workIdValue =
                IdentifiersModule.createWorkId workId
                |> Result.defaultWith failwith

            ({ SchemaVersion = SchemaVersionModule.create 1
               WorkId = workIdValue
               Stage = LifecycleStage.Evidence
               Status = "needsEvidence"
               SourceSpec = specPath workId
               SourceClarifications = clarificationPath workId
               SourceChecklist = checklistPath workId
               SourcePlan = planPath workId
               SourceTasks = tasksPath workId
               SourceAnalysis = analysisPath workId
               SourceSnapshots = []
               Evidence = obligations |> List.choose (fun obligation -> if obligation.ObligationId.StartsWith("EV", StringComparison.OrdinalIgnoreCase) then Some(skeletonEvidenceDeclaration workId obligation) else None)
               LifecycleNotes = [ "Next lifecycle action: verify after evidence is supported or deferred." ]
               Diagnostics = [] } : EvidenceArtifact),
            []

    let evidenceValidationDiagnostics
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (taskFacts: TaskFacts)
        (currentSnapshots: EvidenceSourceSnapshot list)
        (artifact: EvidenceArtifact)
        =
        let path = evidencePath workId
        let knownTasks = taskFacts.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList
        let knownRequirements = specFacts.RequirementIds |> List.map _.Value |> Set.ofList
        let knownScenarios = specFacts.AcceptanceScenarioIds |> List.map _.Value |> Set.ofList
        let knownClarifications =
            [ clarificationFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
              clarificationFacts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value) ]
            |> List.concat
            |> Set.ofList
        let knownChecklistResults = checklistFacts.Results |> List.map (fun result -> result.ResultId.Value) |> Set.ofList
        let knownPlanDecisions = planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value) |> Set.ofList
        let knownObligations =
            [ planFacts.VerificationObligations |> List.map (fun obligation -> obligation.ObligationId.Value)
              taskFacts.Tasks |> List.collect (fun task -> task.RequiredEvidence |> List.map _.Value) ]
            |> List.concat
            |> Set.ofList

        let unknowns =
            artifact.Evidence
            |> List.collect (fun declaration ->
                [ declaration.TaskRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownTasks))
                  declaration.RequirementRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownRequirements))
                  declaration.AcceptanceScenarioRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownScenarios))
                  declaration.ClarificationDecisionRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownClarifications))
                  declaration.ChecklistResultRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownChecklistResults))
                  declaration.PlanDecisionRefs |> List.map _.Value |> List.filter (fun id -> not (Set.contains id knownPlanDecisions))
                  declaration.ObligationRefs |> List.filter (fun id -> not (Set.contains id knownObligations) && not (id.StartsWith("EV", StringComparison.OrdinalIgnoreCase))) ]
                |> List.concat)
            |> List.distinct
            |> List.sort

        let unsupportedResults =
            artifact.Evidence
            |> List.map (fun declaration -> declaration.Result)
            |> List.map normalizedEvidenceResult
            |> List.filter (fun result -> not (Set.contains result allowedEvidenceResults))
            |> List.distinct
            |> List.sort

        let undisclosedSynthetic =
            artifact.Evidence
            |> List.filter (fun declaration -> declaration.Synthetic && Option.isNone declaration.SyntheticDisclosure)
            |> List.map (fun declaration -> declaration.Id.Value)

        let missingDeferralFields =
            artifact.Evidence
            |> List.filter (fun declaration ->
                declaration.Kind = EvidenceKind.Deferral
                || normalizedEvidenceResult declaration.Result = "deferred")
            |> List.filter (fun declaration ->
                Option.isNone declaration.Rationale
                || Option.isNone declaration.Owner
                || Option.isNone declaration.Scope
                || Option.isNone declaration.LaterLifecycleVisibility)
            |> List.map (fun declaration -> declaration.Id.Value)

        [ if not (String.Equals(artifact.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
              evidenceIdentityMismatch path workId artifact.WorkId.Value
          if artifact.Stage <> LifecycleStage.Evidence then
              malformedEvidenceArtifact path $"Evidence stage '{IdentifiersModule.stageValue artifact.Stage}' is not 'evidence'."
          if normalizeRelativePath artifact.SourceSpec <> specPath workId then
              malformedEvidenceArtifact path $"Evidence sourceSpec '{artifact.SourceSpec}' does not match '{specPath workId}'."
          if normalizeRelativePath artifact.SourceTasks <> tasksPath workId then
              malformedEvidenceArtifact path $"Evidence sourceTasks '{artifact.SourceTasks}' does not match '{tasksPath workId}'."
          if normalizeRelativePath artifact.SourceAnalysis <> analysisPath workId then
              malformedEvidenceArtifact path $"Evidence sourceAnalysis '{artifact.SourceAnalysis}' does not match '{analysisPath workId}'."
          if not (List.isEmpty unknowns) then
              unknownEvidenceReference path (String.concat "," unknowns)
          if not (List.isEmpty unsupportedResults) then
              unsupportedEvidenceResultState path unsupportedResults
          if not (List.isEmpty undisclosedSynthetic) then
              undisclosedSyntheticEvidence path undisclosedSynthetic
          if not (List.isEmpty missingDeferralFields) then
              missingDeferralRationale path missingDeferralFields
          if evidenceSourceSnapshotStale currentSnapshots artifact.SourceSnapshots then
              staleEvidenceSource path (artifact.SourceSnapshots |> List.map (fun snapshot -> snapshot.Label) |> List.filter (String.IsNullOrWhiteSpace >> not)) ]

    let evidenceDispositions (obligations: EvidenceObligation list) (artifact: EvidenceArtifact) : EvidenceDispositionDraft list =
        obligations
        |> List.mapi (fun index obligation ->
            let matches : EvidenceDeclaration list =
                artifact.Evidence
                |> List.filter (fun declaration ->
                    declaration.Id.Value = obligation.ObligationId
                    || declaration.ObligationRefs |> List.exists (fun id -> String.Equals(id, obligation.ObligationId, StringComparison.OrdinalIgnoreCase))
                    || declaration.TaskRefs
                       |> List.exists (fun taskId ->
                           obligation.LinkedTaskIds |> List.exists (fun linked -> linked.Value = taskId.Value)))

            let state, diagnostics =
                if List.isEmpty matches then
                    "missing", [ "evidence.missingRequiredEvidence" ]
                elif matches |> List.exists (fun declaration -> declaration.Synthetic && Option.isNone declaration.SyntheticDisclosure) then
                    "invalid", [ "evidence.undisclosedSyntheticEvidence" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        (declaration.Kind = EvidenceKind.Deferral || normalizedEvidenceResult declaration.Result = "deferred")
                        && (Option.isNone declaration.Rationale
                            || Option.isNone declaration.Owner
                            || Option.isNone declaration.Scope
                            || Option.isNone declaration.LaterLifecycleVisibility))
                then
                    "invalid", [ "evidence.missingDeferralRationale" ]
                elif matches |> List.exists (fun declaration -> not (Set.contains (normalizedEvidenceResult declaration.Result) allowedEvidenceResults)) then
                    "invalid", [ "evidence.unsupportedResultState" ]
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass" && declaration.Synthetic) then
                    "synthetic", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass") then
                    "supported", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "deferred" || declaration.Kind = EvidenceKind.Deferral) then
                    "deferred", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "stale") then
                    "stale", [ "evidence.staleEvidence" ]
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "advisory") then
                    "advisory", []
                else
                    "blocking", [ "evidence.missingRequiredEvidence" ]

            ({ ObligationId = obligation.ObligationId
               State = state
               EvidenceIds = matches |> List.map (fun declaration -> declaration.Id.Value) |> List.distinct |> List.sort
               TaskIds = obligation.LinkedTaskIds |> List.map _.Value |> List.sort
               DiagnosticIds = diagnostics |> List.distinct |> List.sort } : EvidenceDispositionDraft))

    let evidenceDispositionDiagnostics path (dispositions: EvidenceDispositionDraft list) =
        let idsFor state =
            dispositions
            |> List.filter (fun disposition -> disposition.State = state)
            |> List.map _.ObligationId
            |> List.distinct
            |> List.sort

        [ let missing = idsFor "missing"
          if not (List.isEmpty missing) then
              missingRequiredEvidence path missing

          let stale = idsFor "stale"
          if not (List.isEmpty stale) then
              staleEvidence path stale ]

    let evidenceSummary workId (artifact: EvidenceArtifact) (dispositions: EvidenceDispositionDraft list) : EvidenceSummary =
        let count state = dispositions |> List.filter (fun disposition -> disposition.State = state) |> List.length
        let blockingCount = count "missing" + count "invalid" + count "blocking"
        let warningCount = count "stale"
        let readiness =
            if blockingCount > 0 then "needsEvidenceCorrection"
            elif warningCount > 0 then "needsEvidenceReview"
            else "evidenceReady"

        { WorkId = workId
          Stage = "evidence"
          Status = readiness
          EvidencePath = evidencePath workId
          DeclarationIds = artifact.Evidence |> List.map (fun declaration -> declaration.Id.Value) |> List.distinct |> List.sort
          DeclarationCount = artifact.Evidence.Length
          ObligationCount = dispositions.Length
          SupportedCount = count "supported"
          DeferredCount = count "deferred"
          MissingCount = count "missing"
          StaleCount = count "stale"
          SyntheticCount = count "synthetic"
          InvalidCount = count "invalid"
          AdvisoryCount = count "advisory"
          BlockingCount = blockingCount
          SourceSnapshotCount = artifact.SourceSnapshots.Length
          Readiness = readiness }

    let renderEvidenceSourceSnapshot (snapshot: EvidenceSourceSnapshot) =
        let digest = snapshot.Digest |> Option.defaultValue ""
        let schema = snapshot.SchemaVersion |> Option.map string |> Option.defaultValue "1"

        $"""  - label: {snapshot.Label}
    path: {snapshot.Path}
    digest: {digest}
    schemaVersion: {schema}"""

    let renderEvidenceSourceRefs (refs: EvidenceSourceReference list) =
        match refs with
        | [] -> "    sourceRefs: []"
        | refs ->
            let lines =
                refs
                |> List.map (fun ref ->
                    let pathLine = ref.Path |> Option.map (fun path -> $"\n        path: {yamlString path}") |> Option.defaultValue ""
                    let uriLine = ref.Uri |> Option.map (fun uri -> $"\n        uri: {yamlString uri}") |> Option.defaultValue ""
                    let resultLine = ref.Result |> Option.map (fun result -> $"\n        result: {yamlString result}") |> Option.defaultValue ""
                    $"      - kind: {yamlString ref.Kind}{pathLine}{uriLine}{resultLine}")
                |> String.concat "\n"

            $"    sourceRefs:\n{lines}"

    let renderOptionalScalar name value =
        match value with
        | Some value -> $"    {name}: {yamlString value}"
        | None -> $"    {name}: null"

    let renderSyntheticDisclosure (disclosure: SyntheticDisclosure option) =
        match disclosure with
        | Some disclosure ->
            $"""    syntheticDisclosure:
      standsInFor: {yamlString disclosure.StandsInFor}
      reason: {yamlString disclosure.Reason}"""
        | None -> "    syntheticDisclosure: null"

    let renderEvidenceDeclaration (declaration: EvidenceDeclaration) =
        let taskRefs = declaration.TaskRefs |> List.map _.Value
        let requirementRefs = declaration.RequirementRefs |> List.map _.Value
        let acceptanceRefs = declaration.AcceptanceScenarioRefs |> List.map _.Value
        let clarificationRefs = declaration.ClarificationDecisionRefs |> List.map _.Value
        let checklistRefs = declaration.ChecklistResultRefs |> List.map _.Value
        let planRefs = declaration.PlanDecisionRefs |> List.map _.Value
        let artifactRefs = declaration.ArtifactRefs |> List.map _.Path

        $"""  - id: {declaration.Id.Value}
    kind: {evidenceKindSourceValue declaration.Kind}
    subject:
      type: {yamlString declaration.Subject.SubjectType}
      id: {yamlString declaration.Subject.Id}
    taskRefs: {taskRefs |> yamlInlineList}
    requirementRefs: {requirementRefs |> yamlInlineList}
    acceptanceScenarioRefs: {acceptanceRefs |> yamlInlineList}
    clarificationDecisionRefs: {clarificationRefs |> yamlInlineList}
    checklistResultRefs: {checklistRefs |> yamlInlineList}
    planDecisionRefs: {planRefs |> yamlInlineList}
    obligationRefs: {declaration.ObligationRefs |> yamlInlineList}
    artifacts: {artifactRefs |> yamlInlineList}
{renderEvidenceSourceRefs declaration.SourceRefs}
    result: {normalizedEvidenceResult declaration.Result}
    synthetic: {if declaration.Synthetic then "true" else "false"}
{renderSyntheticDisclosure declaration.SyntheticDisclosure}
{renderOptionalScalar "rationale" declaration.Rationale}
{renderOptionalScalar "owner" declaration.Owner}
{renderOptionalScalar "scope" declaration.Scope}
{renderOptionalScalar "laterLifecycleVisibility" declaration.LaterLifecycleVisibility}
    notes: {declaration.Notes |> yamlInlineList}"""

    let evidenceArtifactText workId (artifact: EvidenceArtifact) (summary: EvidenceSummary) =
        let sourceSnapshots =
            match artifact.SourceSnapshots with
            | [] -> "sourceSnapshots: []"
            | snapshots -> snapshots |> List.sortBy (fun snapshot -> snapshot.Path, snapshot.Label) |> List.map renderEvidenceSourceSnapshot |> String.concat "\n" |> fun text -> $"sourceSnapshots:\n{text}"

        let evidence =
            match artifact.Evidence with
            | [] -> "evidence: []"
            | evidence -> evidence |> List.sortBy (fun declaration -> declaration.Id.Value) |> List.map renderEvidenceDeclaration |> String.concat "\n" |> fun text -> $"evidence:\n{text}"

        $"""schemaVersion: 1
workId: {workId}
stage: evidence
status: {summary.Readiness}
sourceSpec: {specPath workId}
sourceClarifications: {clarificationPath workId}
sourceChecklist: {checklistPath workId}
sourcePlan: {planPath workId}
sourceTasks: {tasksPath workId}
sourceAnalysis: {analysisPath workId}
{sourceSnapshots}
{evidence}
{renderScalarBlock "lifecycleNotes" [ "Next lifecycle action: verify." ]}
"""

    let computeEvidencePlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
            let specificationDiagnostics, specText, specification, specFacts = specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model
            let clarificationDiagnostics, clarificationText, clarification, clarificationFacts = clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model

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
                    tasksPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts planFacts model
                | _ -> [], None, None, None

            let analysisDiagnostics, analysisText, analysis =
                analysisPrerequisiteDiagnosticsSummaryAndText workId model

            let existingArtifact, existingDiagnostics, _ = parseExistingEvidence workId model
            let inputArtifact, inputDiagnostics = parseInputEvidence workId model.Request

            let evidenceArtifact, mergeDiagnostics, evidenceText, evidenceSummary =
                match specText, clarificationText, checklistText, planText, taskText, analysisText, specFacts, clarificationFacts, checklistFacts, planFacts, taskFacts with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText, Some analysisText, Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts, Some taskFacts ->
                    let currentSnapshots = currentEvidenceSourceSnapshots workId specText clarificationText checklistText planText taskText analysisText
                    let obligations = evidenceObligations taskFacts
                    let merged, mergeDiagnostics = mergeEvidenceArtifacts workId existingArtifact inputArtifact obligations
                    let artifact = { merged with SourceSnapshots = currentSnapshots }
                    let validationDiagnostics = evidenceValidationDiagnostics workId specFacts clarificationFacts checklistFacts planFacts taskFacts currentSnapshots artifact
                    let dispositions = evidenceDispositions obligations artifact
                    let dispositionDiagnostics = evidenceDispositionDiagnostics (evidencePath workId) dispositions
                    let summary = evidenceSummary workId artifact dispositions
                    let text = evidenceArtifactText workId artifact summary
                    Some artifact, mergeDiagnostics @ validationDiagnostics @ dispositionDiagnostics, Some text, Some summary
                | _ ->
                    None, [], None, None

            let commandDiagnostics =
                projectDiagnostics
                @ duplicateDiagnostics
                @ specificationDiagnostics
                @ clarificationDiagnostics
                @ checklistDiagnostics
                @ planDiagnostics
                @ taskDiagnostics
                @ analysisDiagnostics
                @ existingDiagnostics
                @ inputDiagnostics
                @ mergeDiagnostics
                |> DiagnosticsModule.sort

            let generatedDiagnostics, generatedView, generatedEffects =
                match specText, clarificationText, checklistText, planText, taskText with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText ->
                    let charterText = snapshot (charterPath workId) model |> Option.map _.Text
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) (Some planText) (Some taskText) evidenceText commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids =
                        commandDiagnostics
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.map _.Id

                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            let evidenceEffects =
                match evidenceText with
                | Some text when not hasBlocking -> [ CreateDirectory($"work/{workId}"); WriteFile(evidencePath workId, text, AuthoredSource) ]
                | _ -> []

            let effects =
                if hasBlocking then []
                else evidenceEffects @ generatedEffects

            diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidenceSummary, [ generatedView ], effects

    // ---- Verify command ----

    type VerifyEvidenceDispositionView =
        { Id: string
          ObligationId: string
          State: string
          EvidenceIds: string list
          TaskIds: string list
          SourceIds: string list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type VerifyTestDispositionView =
        { Id: string
          ObligationId: string
          State: string
          EvidenceIds: string list
          TaskIds: string list
          RequirementIds: string list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type VerifySkillView =
        { Skill: string
          RequiringTaskIds: string list
          Visibility: string
          SourceArtifactPath: string
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    let dispositionSeverity state =
        match state with
        | "missing"
        | "blocking"
        | "invalid" -> "blocking"
        | "stale" -> "warning"
        | "deferred"
        | "synthetic"
        | "advisory" -> "advisory"
        | _ -> "ready"

    let verifySourceKind (path: string) =
        if path = ".fsgg/project.yml" then "projectConfig"
        elif path = ".fsgg/sdd.yml" then "sddConfig"
        elif path = ".fsgg/agents.yml" then "agentsConfig"
        elif path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then "specification"
        elif path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then "clarification"
        elif path.EndsWith("/checklist.md", StringComparison.OrdinalIgnoreCase) then "checklist"
        elif path.EndsWith("/plan.md", StringComparison.OrdinalIgnoreCase) then "plan"
        elif path.EndsWith("/tasks.yml", StringComparison.OrdinalIgnoreCase) then "tasks"
        elif path.EndsWith("/evidence.yml", StringComparison.OrdinalIgnoreCase) then "evidence"
        elif path.EndsWith("/analysis.json", StringComparison.OrdinalIgnoreCase) then "analysis"
        elif path.EndsWith("/work-model.json", StringComparison.OrdinalIgnoreCase) then "workModel"
        else "source"

    let verifySources workId specText clarificationText checklistText planText tasksText evidenceText analysisText workModelJson model : GeneratedViewSource list =
        [ snapshot ".fsgg/project.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/sdd.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/agents.yml" model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          Some(analysisSourceFromSnapshot (specPath workId) specText)
          Some(analysisSourceFromSnapshot (clarificationPath workId) clarificationText)
          Some(analysisSourceFromSnapshot (checklistPath workId) checklistText)
          Some(analysisSourceFromSnapshot (planPath workId) planText)
          Some(analysisSourceFromSnapshot (tasksPath workId) tasksText)
          Some(analysisSourceFromSnapshot (evidencePath workId) evidenceText)
          analysisText |> Option.map (analysisSourceFromSnapshot (analysisPath workId))
          workModelJson |> Option.map (analysisSourceFromSnapshot (workModelPath workId)) ]
        |> List.choose id
        |> List.sortBy (fun source -> source.Path)

    let verifyFindingSeverity (diagnostic: Diagnostic) =
        match analysisFindingSeverity diagnostic with
        | "blocking"
        | "missingDisposition"
        | "malformedSource" -> "blocking"
        | "staleSource"
        | "generatedView"
        | "warning" -> "warning"
        | _ -> "advisory"

    let verifyFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic -> sprintf "VF%03d" (index + 1), diagnostic, verifyFindingSeverity diagnostic)

    let verifyEvidenceDispositionViews (drafts: EvidenceDispositionDraft list) =
        drafts
        |> List.map (fun draft ->
            let severity = dispositionSeverity draft.State

            { Id = "ED-" + draft.ObligationId
              ObligationId = draft.ObligationId
              State = draft.State
              EvidenceIds = draft.EvidenceIds
              TaskIds = draft.TaskIds
              SourceIds = []
              Severity = severity
              DiagnosticIds = draft.DiagnosticIds
              Correction = if severity = "ready" then "" else $"Resolve evidence obligation {draft.ObligationId}." })
        |> List.sortBy (fun view -> view.Id)

    let verifyTestDispositionViews (taskFacts: TaskFacts) (artifact: EvidenceArtifact) =
        taskFacts.Tasks
        |> List.collect (fun task -> task.RequiredEvidence |> List.map (fun ev -> ev.Value, task))
        |> List.groupBy fst
        |> List.map (fun (obligationId, entries) ->
            let tasks = entries |> List.map snd

            let matches =
                artifact.Evidence
                |> List.filter (fun declaration ->
                    declaration.Id.Value = obligationId
                    || declaration.ObligationRefs |> List.exists (fun id -> String.Equals(id, obligationId, StringComparison.OrdinalIgnoreCase)))

            let state, diagnostics =
                if List.isEmpty matches then
                    "missing", [ "verify.missingRequiredTest" ]
                elif matches |> List.exists (fun declaration -> declaration.Synthetic && Option.isNone declaration.SyntheticDisclosure) then
                    "invalid", [ "evidence.undisclosedSyntheticEvidence" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        (declaration.Kind = EvidenceKind.Deferral || normalizedEvidenceResult declaration.Result = "deferred")
                        && (Option.isNone declaration.Rationale
                            || Option.isNone declaration.Owner
                            || Option.isNone declaration.Scope
                            || Option.isNone declaration.LaterLifecycleVisibility))
                then
                    "invalid", [ "evidence.missingDeferralRationale" ]
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass" && declaration.Synthetic) then
                    "synthetic", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass") then
                    "satisfied", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "deferred" || declaration.Kind = EvidenceKind.Deferral) then
                    "deferred", []
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "stale") then
                    "stale", [ "verify.staleRequiredTest" ]
                elif matches |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "advisory") then
                    "advisory", []
                else
                    "missing", [ "verify.missingRequiredTest" ]

            let severity = dispositionSeverity state

            { Id = "TD-" + obligationId
              ObligationId = obligationId
              State = state
              EvidenceIds = matches |> List.map (fun declaration -> declaration.Id.Value) |> List.distinct |> List.sort
              TaskIds = tasks |> List.map (fun task -> task.Id.Value) |> List.distinct |> List.sort
              RequirementIds = tasks |> List.collect (fun task -> task.Requirements |> List.map _.Value) |> List.distinct |> List.sort
              Severity = severity
              DiagnosticIds = diagnostics
              Correction = if severity = "ready" then "" else $"Record a verifying test for {obligationId}." })
        |> List.sortBy (fun view -> view.Id)

    let verifySkillViews workId (taskFacts: TaskFacts) (evidenceDrafts: EvidenceDispositionDraft list) =
        let taskStates =
            evidenceDrafts
            |> List.collect (fun draft -> draft.TaskIds |> List.map (fun taskId -> taskId, draft.State))
            |> List.groupBy fst
            |> List.map (fun (taskId, entries) -> taskId, entries |> List.map snd)
            |> Map.ofList

        let blockingStates = set [ "missing"; "blocking"; "invalid" ]

        taskFacts.Tasks
        |> List.collect (fun task -> task.RequiredSkills |> List.map (fun skill -> skill, task))
        |> List.groupBy fst
        |> List.map (fun (skill, entries) ->
            let tasks = entries |> List.map snd

            let visible =
                tasks
                |> List.forall (fun task ->
                    match Map.tryFind task.Id.Value taskStates with
                    | Some states -> not (states |> List.exists (fun state -> Set.contains state blockingStates))
                    | None -> true)

            { Skill = skill
              RequiringTaskIds = tasks |> List.map (fun task -> task.Id.Value) |> List.distinct |> List.sort
              Visibility = if visible then "visible" else "missing"
              SourceArtifactPath = tasksPath workId
              Severity = if visible then "ready" else "blocking"
              DiagnosticIds = if visible then [] else [ "evidence.missingRequiredSkill" ]
              Correction = if visible then "" else $"Make required skill '{skill}' visible through lifecycle artifacts or supporting evidence." })
        |> List.sortBy (fun view -> view.Skill)

    let existingVerifyDiagnostic workId model =
        let path = verifyPath workId

        match snapshot path model with
        | None -> None
        | Some existing ->
            match LifecycleArtifactsModule.parseVerificationView existing with
            | Error diagnostics ->
                diagnostics
                |> List.tryHead
                |> Option.map (fun diagnostic -> malformedVerificationView path diagnostic.Message)
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                Some(verifyIdentityMismatch path workId view.WorkId.Value)
            | Ok _ -> None

    let verifyGeneratedViewState
        (path: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (outputDigest: OutputDigest option)
        (currency: GeneratedViewCurrency)
        (diagnosticIds: string list)
        : GeneratedViewState
        =
        { Path = path
          Kind = "verification"
          SchemaVersion = Some 1
          Generator = Some generator
          Sources = sources |> List.sortBy _.Path
          OutputDigest = outputDigest
          Currency = currency
          DiagnosticIds = diagnosticIds |> List.distinct |> List.sort }

    let verifyJson
        (workId: string)
        (generator: GeneratorVersion)
        (readiness: string)
        (sources: GeneratedViewSource list)
        (lifecycleStages: (string * string) list)
        (lifecycleStatus: string)
        (taskCount: int)
        (dependencyCount: int)
        (dependenciesValid: bool)
        (statusesValid: bool)
        (taskFindingIds: string list)
        (evidenceViews: VerifyEvidenceDispositionView list)
        (testViews: VerifyTestDispositionView list)
        (skillViews: VerifySkillView list)
        (generatedViews: GeneratedViewState list)
        (diagnostics: Diagnostic list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        let findings = verifyFindings diagnostics

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("stage", "verify")
        writer.WriteString("status", readiness)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")
        writer.WriteStartArray("sources")
        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            writer.WriteStartObject()
            writer.WriteString("path", source.Path)
            writer.WriteString("kind", verifySourceKind source.Path)
            writeDigestObject writer "digest" source.Digest
            match source.SchemaVersion with
            | Some version -> writer.WriteNumber("schemaVersion", version)
            | None -> writer.WriteNull "schemaVersion"
            match source.SchemaStatus with
            | Some status -> writer.WriteString("schemaStatus", status)
            | None -> writer.WriteNull "schemaStatus"
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartObject("lifecycleReadiness")
        writer.WriteString("status", lifecycleStatus)
        writer.WriteStartArray("stages")
        lifecycleStages
        |> List.sortBy fst
        |> List.iter (fun (stage, status) ->
            writer.WriteStartObject()
            writer.WriteString("stage", stage)
            writer.WriteString("status", status)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.WriteStartObject("taskGraph")
        writer.WriteNumber("taskCount", taskCount)
        writer.WriteNumber("dependencyCount", dependencyCount)
        writer.WriteBoolean("dependenciesValid", dependenciesValid)
        writer.WriteBoolean("statusesValid", statusesValid)
        writeStringArray writer "findingIds" taskFindingIds
        writer.WriteEndObject()
        writer.WriteStartArray("evidenceDispositions")
        evidenceViews
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("id", view.Id)
            writer.WriteString("obligationId", view.ObligationId)
            writer.WriteString("state", view.State)
            writeStringArray writer "evidenceIds" view.EvidenceIds
            writeStringArray writer "affectedTaskIds" view.TaskIds
            writeStringArray writer "affectedSourceIds" view.SourceIds
            writer.WriteString("severity", view.Severity)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteString("correction", view.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("testDispositions")
        testViews
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("id", view.Id)
            writer.WriteString("obligationId", view.ObligationId)
            writer.WriteString("state", view.State)
            writeStringArray writer "evidenceIds" view.EvidenceIds
            writeStringArray writer "affectedTaskIds" view.TaskIds
            writeStringArray writer "affectedRequirementIds" view.RequirementIds
            writer.WriteString("severity", view.Severity)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteString("correction", view.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("skillVisibility")
        skillViews
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("skill", view.Skill)
            writeStringArray writer "requiringTaskIds" view.RequiringTaskIds
            writer.WriteString("visibility", view.Visibility)
            writer.WriteString("sourceArtifactPath", view.SourceArtifactPath)
            writer.WriteString("severity", view.Severity)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteString("correction", view.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("generatedViews")
        generatedViews
        |> List.sortBy (fun view -> view.Path)
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("path", view.Path)
            writer.WriteString("kind", view.Kind)
            writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("findings")
        findings
        |> List.iter (fun (id, diagnostic, severity) ->
            writer.WriteStartObject()
            writer.WriteString("id", id)
            writer.WriteString("severity", severity)
            writer.WriteString("category", severity)
            writer.WriteString("path", diagnosticPath diagnostic)
            writeStringArray writer "relatedIds" diagnostic.RelatedIds
            writer.WriteString("message", diagnostic.Message)
            writer.WriteString("correction", diagnostic.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("governanceCompatibility")
        analysisBoundaryFacts()
        |> List.iter (fun fact ->
            writer.WriteStartObject()
            writer.WriteString("path", fact.Path)
            writer.WriteString("relationship", fact.Relationship)
            writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
            writer.WriteString("state", fact.State)
            writeStringArray writer "diagnosticIds" fact.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        diagnostics |> DiagnosticsModule.sort |> List.iter (writeAnalysisDiagnosticJson writer)
        writer.WriteEndArray()
        writer.WriteString("readiness", readiness)
        writer.WriteStartObject("nextAction")
        if readiness = "verificationReady" then
            writer.WriteString("actionId", "verify.next.ship")
            writer.WriteNull("command")
            writer.WriteString("reason", "Verification readiness is current and ready for ship.")
        else
            writer.WriteString("actionId", "correctBlockingDiagnostics")
            writer.WriteNull("command")
            writer.WriteString("reason", "Verification found lifecycle diagnostics that must be corrected before ship.")
        writer.WriteEndObject()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let computeVerifyPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
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

            let analysisDiagnostics, analysisText, analysis = analysisPrerequisiteDiagnosticsSummaryAndText workId model
            let existingEvidenceArtifact, existingEvidenceDiagnostics, evidenceText = parseExistingEvidence workId model

            let evidencePresenceDiagnostics =
                match existingEvidenceArtifact, snapshot (evidencePath workId) model with
                | None, None -> [ missingEvidencePrerequisite (evidencePath workId) $"Evidence prerequisite '{evidencePath workId}' is missing." ]
                | _ -> []

            let verifyViewDiagnostics = existingVerifyDiagnostic workId model |> Option.toList

            let verificationDiagnostics, evidenceSummaryOpt, evidenceViews, testViews, skillViews =
                match specFacts, clarificationFacts, checklistFacts, planFacts, taskFacts, specText, clarificationText, checklistText, planText, taskText, analysisText, existingEvidenceArtifact with
                | Some specFacts, Some clarificationFacts, Some checklistFacts, Some planFacts, Some taskFacts, Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText, Some analysisText, Some artifact ->
                    let currentSnapshots = currentEvidenceSourceSnapshots workId specText clarificationText checklistText planText taskText analysisText
                    let validationDiagnostics = evidenceValidationDiagnostics workId specFacts clarificationFacts checklistFacts planFacts taskFacts currentSnapshots artifact
                    let obligations = evidenceObligations taskFacts
                    let dispositions = evidenceDispositions obligations artifact
                    let dispositionDiagnostics = evidenceDispositionDiagnostics (evidencePath workId) dispositions
                    let evidenceViews = verifyEvidenceDispositionViews dispositions
                    let testViews = verifyTestDispositionViews taskFacts artifact
                    let skillViews = verifySkillViews workId taskFacts dispositions

                    let testDiagnostics =
                        let missing = testViews |> List.filter (fun view -> view.State = "missing") |> List.map _.ObligationId |> List.sort
                        let stale = testViews |> List.filter (fun view -> view.State = "stale") |> List.map _.ObligationId |> List.sort

                        [ if not (List.isEmpty missing) then missingRequiredTest (tasksPath workId) missing
                          if not (List.isEmpty stale) then staleRequiredTest (tasksPath workId) stale ]

                    let skillDiagnostics =
                        let missing = skillViews |> List.filter (fun view -> view.Visibility = "missing") |> List.map _.Skill |> List.sort
                        if not (List.isEmpty missing) then [ missingRequiredSkill (tasksPath workId) missing ] else []

                    let summary = evidenceSummary workId artifact dispositions
                    validationDiagnostics @ dispositionDiagnostics @ testDiagnostics @ skillDiagnostics, Some summary, evidenceViews, testViews, skillViews
                | _ -> [], None, [], [], []

            let commandDiagnostics =
                projectDiagnostics
                @ duplicateDiagnostics
                @ specificationDiagnostics
                @ clarificationDiagnostics
                @ checklistDiagnostics
                @ planDiagnostics
                @ taskDiagnostics
                @ analysisDiagnostics
                @ existingEvidenceDiagnostics
                @ evidencePresenceDiagnostics
                @ verifyViewDiagnostics
                @ verificationDiagnostics
                |> DiagnosticsModule.sort

            let generatedDiagnostics, workModelView, workModelEffects =
                match specText, clarificationText, checklistText, planText, taskText with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText ->
                    let charterText = snapshot (charterPath workId) model |> Option.map _.Text
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) (Some planText) (Some taskText) evidenceText commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids = commandDiagnostics |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError) |> List.map _.Id
                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            let readiness = if hasBlocking then "needsVerificationCorrection" else "verificationReady"

            let verificationSummary, verifyView, verifyEffects =
                match specText, clarificationText, checklistText, planText, taskText, analysisText, taskFacts with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText, Some analysisText, Some taskFacts ->
                    let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model
                    let sources = verifySources workId specText clarificationText checklistText planText taskText (evidenceText |> Option.defaultValue "") (Some analysisText) workModelJson model

                    let lifecycleStages =
                        [ "specify", (if Option.isSome specFacts then "current" else "missing")
                          "clarify", (if Option.isSome clarificationFacts then "current" else "missing")
                          "checklist", (if Option.isSome checklistFacts then "current" else "missing")
                          "plan", (if Option.isSome planFacts then "current" else "missing")
                          "tasks", "current"
                          "analyze", (analysis |> Option.map (fun summary -> summary.Readiness) |> Option.defaultValue "missing")
                          "evidence", (evidenceSummaryOpt |> Option.map (fun summary -> summary.Readiness) |> Option.defaultValue "missing") ]

                    let dependencyCount = taskFacts.Tasks |> List.collect (fun task -> task.Dependencies) |> List.length
                    let dependencyDiagnosticIds = set [ "unknownTaskDependency"; "taskDependencyCycle" ]
                    let statusDiagnosticIds = set [ "unsafeTaskStatusChange"; "skippedTaskMissingRationale" ]
                    let dependenciesValid = not (diagnostics |> List.exists (fun diagnostic -> Set.contains diagnostic.Id dependencyDiagnosticIds))
                    let statusesValid = not (diagnostics |> List.exists (fun diagnostic -> Set.contains diagnostic.Id statusDiagnosticIds))
                    let taskFindingIds = taskFacts.Findings |> List.map (fun finding -> finding.FindingId) |> List.sort

                    let generatedViewsForVerify =
                        [ workModelView
                          analysis
                          |> Option.map (fun _ -> verifyGeneratedViewState (analysisPath workId) model.Request.GeneratorVersion [] None GeneratedViewCurrency.Current [])
                          |> Option.defaultValue (verifyGeneratedViewState (analysisPath workId) model.Request.GeneratorVersion [] None GeneratedViewCurrency.Missing []) ]

                    let text =
                        verifyJson
                            workId
                            model.Request.GeneratorVersion
                            readiness
                            sources
                            lifecycleStages
                            (if hasBlocking then "needsCorrection" else "implementationReady")
                            taskFacts.Tasks.Length
                            dependencyCount
                            dependenciesValid
                            statusesValid
                            taskFindingIds
                            evidenceViews
                            testViews
                            skillViews
                            generatedViewsForVerify
                            diagnostics

                    let outputDigest = SchemaVersionModule.outputSha256Text text
                    let view = verifyGeneratedViewState (verifyPath workId) model.Request.GeneratorVersion sources (Some outputDigest) GeneratedViewCurrency.Current []

                    let findings = verifyFindings diagnostics
                    let findingCount severity = findings |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity) |> List.length
                    let evidenceCount state = evidenceViews |> List.filter (fun view -> view.State = state) |> List.length
                    let testCount state = testViews |> List.filter (fun view -> view.State = state) |> List.length

                    let summary : VerificationSummary =
                        { WorkId = workId
                          Stage = "verify"
                          Status = readiness
                          VerifyPath = verifyPath workId
                          FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                          ReadyFindingCount = if readiness = "verificationReady" then evidenceViews.Length + testViews.Length else findingCount "ready"
                          AdvisoryCount = findingCount "advisory"
                          WarningCount = findingCount "warning"
                          BlockingCount = findingCount "blocking"
                          ObligationCount = evidenceViews.Length + testViews.Length
                          EvidenceSupportedCount = evidenceCount "supported"
                          EvidenceDeferredCount = evidenceCount "deferred"
                          EvidenceMissingCount = evidenceCount "missing"
                          EvidenceStaleCount = evidenceCount "stale"
                          EvidenceSyntheticCount = evidenceCount "synthetic"
                          EvidenceInvalidCount = evidenceCount "invalid"
                          TestSatisfiedCount = testCount "satisfied"
                          TestDeferredCount = testCount "deferred"
                          TestMissingCount = testCount "missing"
                          TestStaleCount = testCount "stale"
                          TestInvalidCount = testCount "invalid"
                          SkillVisibleCount = skillViews |> List.filter (fun view -> view.Visibility = "visible") |> List.length
                          SkillMissingCount = skillViews |> List.filter (fun view -> view.Visibility = "missing") |> List.length
                          SourceSnapshotCount = sources.Length
                          Readiness = readiness }

                    let effects =
                        if hasBlocking then
                            []
                        else
                            [ CreateDirectory(readinessDirectory workId); WriteFile(verifyPath workId, text, GeneratedView) ]

                    Some summary, Some view, effects
                | _ -> None, None, []

            let generatedViews = [ Some workModelView; verifyView ] |> List.choose id

            let effects =
                if hasBlocking then [] else workModelEffects @ verifyEffects

            diagnostics, specification, clarification, checklist, plan, tasks, analysis, evidenceSummaryOpt, verificationSummary, generatedViews, effects

    // ---- Ship command ----

    let shipGeneratedViewState
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

    let shipEvidenceStateValue (state: EvidenceDispositionState) =
        match state with
        | EvidenceSupported -> "supported"
        | EvidenceDeferred -> "deferred"
        | EvidenceMissingDisposition -> "missing"
        | EvidenceStale -> "stale"
        | EvidenceSyntheticDisposition -> "synthetic"
        | EvidenceInvalid -> "invalid"
        | EvidenceAdvisory -> "advisory"
        | EvidenceBlocking -> "blocking"

    let shipFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic -> sprintf "SF%03d" (index + 1), diagnostic, verifyFindingSeverity diagnostic)

    let existingShipDiagnostic workId model =
        let path = shipPath workId

        match snapshot path model with
        | None -> None
        | Some existing ->
            match LifecycleArtifactsModule.parseShipView existing with
            | Error diagnostics ->
                diagnostics
                |> List.tryHead
                |> Option.map (fun diagnostic -> malformedShipView path diagnostic.Message)
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                Some(shipIdentityMismatch path workId view.WorkId.Value)
            | Ok _ -> None

    let shipVerificationPrerequisite workId model =
        let path = verifyPath workId

        match snapshot path model with
        | None -> [ missingVerificationPrerequisite path $"Verification prerequisite '{path}' is missing." ], None
        | Some existing ->
            match LifecycleArtifactsModule.parseVerificationView existing with
            | Error diagnostics -> (diagnostics |> List.map (fun diagnostic -> malformedVerificationView path diagnostic.Message)), None
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ verifyIdentityMismatch path workId view.WorkId.Value ], Some view
            | Ok view ->
                let notReady =
                    if not (String.Equals(view.Readiness, "verificationReady", StringComparison.OrdinalIgnoreCase)) then
                        [ verificationNotReady path view.Readiness ]
                    else
                        []

                let blockingFindingIds =
                    view.Findings
                    |> List.filter (fun finding -> String.Equals(finding.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
                    |> List.map (fun finding -> finding.Id)
                    |> List.sort

                let failed =
                    if not (List.isEmpty blockingFindingIds) then [ failedVerification path blockingFindingIds ] else []

                notReady @ failed, Some view

    let shipJson
        (workId: string)
        (generator: GeneratorVersion)
        (readiness: string)
        (disposition: string)
        (sources: GeneratedViewSource list)
        (lifecycleStages: (string * string) list)
        (lifecycleStatus: string)
        (verificationView: VerificationView option)
        (verificationStatus: string)
        (generatedViews: GeneratedViewState list)
        (diagnostics: Diagnostic list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        let findings = shipFindings diagnostics

        let evidenceCount state =
            match verificationView with
            | Some view -> view.EvidenceDispositions |> List.filter (fun disposition -> disposition.State = state) |> List.length
            | None -> 0

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("stage", "ship")
        writer.WriteString("status", readiness)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")
        writer.WriteStartArray("sources")
        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            writer.WriteStartObject()
            writer.WriteString("path", source.Path)
            writer.WriteString("kind", verifySourceKind source.Path)
            writeDigestObject writer "digest" source.Digest
            match source.SchemaVersion with
            | Some version -> writer.WriteNumber("schemaVersion", version)
            | None -> writer.WriteNull "schemaVersion"
            match source.SchemaStatus with
            | Some status -> writer.WriteString("schemaStatus", status)
            | None -> writer.WriteNull "schemaStatus"
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartObject("lifecycleReadiness")
        writer.WriteString("status", lifecycleStatus)
        writer.WriteStartArray("stages")
        lifecycleStages
        |> List.sortBy fst
        |> List.iter (fun (stage, status) ->
            writer.WriteStartObject()
            writer.WriteString("stage", stage)
            writer.WriteString("status", status)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.WriteStartObject("verificationReadiness")
        writer.WriteString("status", verificationStatus)
        writeStringArray
            writer
            "blockingFindingIds"
            (match verificationView with
             | Some view ->
                 view.Findings
                 |> List.filter (fun finding -> String.Equals(finding.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
                 |> List.map (fun finding -> finding.Id)
             | None -> [])
        writer.WriteNumber("evidenceSupportedCount", evidenceCount EvidenceSupported)
        writer.WriteNumber("evidenceDeferredCount", evidenceCount EvidenceDeferred)
        writer.WriteNumber("evidenceMissingCount", evidenceCount EvidenceMissingDisposition)
        writer.WriteNumber("evidenceStaleCount", evidenceCount EvidenceStale)
        writer.WriteNumber("evidenceSyntheticCount", evidenceCount EvidenceSyntheticDisposition)
        writer.WriteNumber("evidenceInvalidCount", evidenceCount EvidenceInvalid)
        writer.WriteEndObject()
        writer.WriteStartArray("evidenceDispositions")
        (match verificationView with
         | Some view -> view.EvidenceDispositions
         | None -> [])
        |> List.sortBy (fun disposition -> disposition.DispositionId)
        |> List.iter (fun disposition ->
            writer.WriteStartObject()
            writer.WriteString("id", disposition.DispositionId)
            writer.WriteString("obligationId", disposition.ObligationId)
            writer.WriteString("state", shipEvidenceStateValue disposition.State)
            writer.WriteString("severity", disposition.Severity)
            writeStringArray writer "diagnosticIds" disposition.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("generatedViews")
        generatedViews
        |> List.sortBy (fun view -> view.Path)
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("path", view.Path)
            writer.WriteString("kind", view.Kind)
            writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartObject("disposition")
        writer.WriteString("state", disposition)
        writeStringArray writer "blockingFindingIds" (findings |> List.filter (fun (_, _, severity) -> severity = "blocking") |> List.map (fun (id, _, _) -> id))
        writeStringArray writer "warningFindingIds" (findings |> List.filter (fun (_, _, severity) -> severity = "warning") |> List.map (fun (id, _, _) -> id))
        writeStringArray writer "advisoryFindingIds" (findings |> List.filter (fun (_, _, severity) -> severity = "advisory") |> List.map (fun (id, _, _) -> id))
        writeStringArray writer "contributingStages" (lifecycleStages |> List.filter (fun (_, status) -> status <> "ready") |> List.map fst)
        writer.WriteString("correction", if disposition = "shipReady" then "" else "Resolve the blocking ship-readiness findings before the protected-boundary handoff.")
        writer.WriteEndObject()
        writer.WriteStartArray("findings")
        findings
        |> List.iter (fun (id, diagnostic, severity) ->
            writer.WriteStartObject()
            writer.WriteString("id", id)
            writer.WriteString("severity", severity)
            writer.WriteString("category", severity)
            writer.WriteString("path", diagnosticPath diagnostic)
            writeStringArray writer "relatedIds" diagnostic.RelatedIds
            writer.WriteString("message", diagnostic.Message)
            writer.WriteString("correction", diagnostic.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("governanceCompatibility")
        analysisBoundaryFacts()
        |> List.iter (fun fact ->
            writer.WriteStartObject()
            writer.WriteString("path", fact.Path)
            writer.WriteString("relationship", fact.Relationship)
            writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
            writer.WriteString("state", fact.State)
            writeStringArray writer "diagnosticIds" fact.DiagnosticIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        diagnostics |> DiagnosticsModule.sort |> List.iter (writeAnalysisDiagnosticJson writer)
        writer.WriteEndArray()
        writer.WriteString("readiness", readiness)
        writer.WriteStartObject("nextAction")
        if readiness = "shipReady" then
            writer.WriteString("actionId", "ship.next.protectedBoundary")
            writer.WriteNull("command")
            writer.WriteString("reason", "Ship readiness is current and ready for the protected-boundary handoff.")
        else
            writer.WriteString("actionId", "correctBlockingDiagnostics")
            writer.WriteNull("command")
            writer.WriteString("reason", "Ship found lifecycle diagnostics that must be corrected before the protected-boundary handoff.")
        writer.WriteEndObject()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let computeShipPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, None, None, None, None, None, None, None, None, [], []
        | Some workId ->
            let projectDiagnostics = projectDiagnostics model
            let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
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

            let analysisDiagnostics, analysisText, analysis = analysisPrerequisiteDiagnosticsSummaryAndText workId model
            let existingEvidenceArtifact, existingEvidenceDiagnostics, evidenceText = parseExistingEvidence workId model

            let evidencePresenceDiagnostics =
                match existingEvidenceArtifact, snapshot (evidencePath workId) model with
                | None, None -> [ missingEvidencePrerequisite (evidencePath workId) $"Evidence prerequisite '{evidencePath workId}' is missing." ]
                | _ -> []

            let verificationPrereqDiagnostics, verificationView = shipVerificationPrerequisite workId model
            let shipViewDiagnostics = existingShipDiagnostic workId model |> Option.toList

            let commandDiagnostics =
                projectDiagnostics
                @ duplicateDiagnostics
                @ specificationDiagnostics
                @ clarificationDiagnostics
                @ checklistDiagnostics
                @ planDiagnostics
                @ taskDiagnostics
                @ analysisDiagnostics
                @ existingEvidenceDiagnostics
                @ evidencePresenceDiagnostics
                @ verificationPrereqDiagnostics
                @ shipViewDiagnostics
                |> DiagnosticsModule.sort

            let generatedDiagnostics, workModelView, workModelEffects =
                match specText, clarificationText, checklistText, planText, taskText with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText ->
                    let charterText = snapshot (charterPath workId) model |> Option.map _.Text
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) (Some checklistText) (Some planText) (Some taskText) evidenceText commandDiagnostics model
                | _ ->
                    let path = workModelPath workId
                    let ids = commandDiagnostics |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError) |> List.map _.Id
                    [], generatedViewState path model.Request.GeneratorVersion [] None GeneratedViewCurrency.Blocked ids, []

            let diagnostics = commandDiagnostics @ generatedDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            let readiness = if hasBlocking then "needsShipCorrection" else "shipReady"

            let disposition =
                if hasBlocking then "blocked"
                elif diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticWarning) then "advisory"
                else "shipReady"

            let verificationStatus =
                verificationView
                |> Option.map (fun view -> view.Readiness)
                |> Option.defaultValue "needsVerificationCorrection"

            let analysisViewState =
                analysis
                |> Option.map (fun _ -> shipGeneratedViewState (analysisPath workId) "analysis" model.Request.GeneratorVersion [] None GeneratedViewCurrency.Current [])
                |> Option.defaultValue (shipGeneratedViewState (analysisPath workId) "analysis" model.Request.GeneratorVersion [] None GeneratedViewCurrency.Missing [])

            let verifyViewState =
                match verificationView with
                | Some _ -> shipGeneratedViewState (verifyPath workId) "verification" model.Request.GeneratorVersion [] None GeneratedViewCurrency.Current []
                | None -> shipGeneratedViewState (verifyPath workId) "verification" model.Request.GeneratorVersion [] None GeneratedViewCurrency.Missing []

            let stageStatus present = if present then "ready" else "missing"

            let lifecycleStages =
                [ "specify", stageStatus (Option.isSome specFacts)
                  "clarify", stageStatus (Option.isSome clarificationFacts)
                  "checklist", stageStatus (Option.isSome checklistFacts)
                  "plan", stageStatus (Option.isSome planFacts)
                  "tasks", stageStatus (Option.isSome taskFacts)
                  "analyze", (match analysis with Some summary -> (if summary.Readiness = "implementationReady" then "ready" else "blocked") | None -> "missing")
                  "evidence", stageStatus (Option.isSome existingEvidenceArtifact)
                  "verify", (if verificationStatus = "verificationReady" then "ready" else "blocked") ]

            let shipSummaryOpt, shipView, shipEffects =
                match specText, clarificationText, checklistText, planText, taskText, analysisText with
                | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText, Some analysisText ->
                    let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model
                    let baseSources = verifySources workId specText clarificationText checklistText planText taskText (evidenceText |> Option.defaultValue "") (Some analysisText) workModelJson model

                    let sources =
                        baseSources
                        @ (snapshot (verifyPath workId) model |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text) |> Option.toList)
                        |> List.sortBy (fun source -> source.Path)

                    let generatedViewsForShip = [ workModelView; analysisViewState; verifyViewState ]

                    let text =
                        shipJson
                            workId
                            model.Request.GeneratorVersion
                            readiness
                            disposition
                            sources
                            lifecycleStages
                            (if hasBlocking then "needsShipCorrection" else "shipReady")
                            verificationView
                            verificationStatus
                            generatedViewsForShip
                            diagnostics

                    let outputDigest = SchemaVersionModule.outputSha256Text text
                    let view = shipGeneratedViewState (shipPath workId) "ship" model.Request.GeneratorVersion sources (Some outputDigest) GeneratedViewCurrency.Current []

                    let findings = shipFindings diagnostics
                    let findingCount severity = findings |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity) |> List.length

                    let evidenceCount state =
                        match verificationView with
                        | Some v -> v.EvidenceDispositions |> List.filter (fun disposition -> disposition.State = state) |> List.length
                        | None -> 0

                    let summary : ShipSummary =
                        { WorkId = workId
                          Stage = "ship"
                          Status = readiness
                          ShipPath = shipPath workId
                          FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                          ReadyFindingCount = findingCount "ready"
                          AdvisoryCount = findingCount "advisory"
                          WarningCount = findingCount "warning"
                          BlockingCount = findingCount "blocking"
                          Disposition = disposition
                          LifecycleStageReadiness = lifecycleStages
                          VerificationReadiness = verificationStatus
                          EvidenceSupportedCount = evidenceCount EvidenceSupported
                          EvidenceDeferredCount = evidenceCount EvidenceDeferred
                          EvidenceMissingCount = evidenceCount EvidenceMissingDisposition
                          EvidenceStaleCount = evidenceCount EvidenceStale
                          EvidenceSyntheticCount = evidenceCount EvidenceSyntheticDisposition
                          EvidenceInvalidCount = evidenceCount EvidenceInvalid
                          GeneratedViewState = (if hasBlocking then "blocked" else "current")
                          SourceSnapshotCount = sources.Length
                          Readiness = readiness }

                    let effects =
                        if hasBlocking then
                            []
                        else
                            [ CreateDirectory(readinessDirectory workId); WriteFile(shipPath workId, text, GeneratedView) ]

                    Some summary, Some view, effects
                | _ -> None, None, []

            let generatedViews =
                [ Some workModelView; Some analysisViewState; Some verifyViewState; shipView ] |> List.choose id

            let effects =
                if hasBlocking then [] else workModelEffects @ shipEffects

            diagnostics, specification, clarification, checklist, plan, tasks, analysis, None, None, shipSummaryOpt, generatedViews, effects

    // ---- Agents command (cross-cutting generated agent guidance) ----

    let resolveGeneratedRoot workId (raw: string) =
        normalizeRelativePath ((if isNull raw then "" else raw).Replace("{workId}", workId))

    let agentRootResolvesWithinProject workId (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            false
        else
            let substituted = raw.Replace("{workId}", workId)

            not (Path.IsPathRooted substituted)
            && not ((resolveGeneratedRoot workId raw).StartsWith("..", StringComparison.Ordinal))

    let agentsConfigOpt model =
        match snapshot ".fsgg/agents.yml" model with
        | Some snap ->
            match LifecycleArtifactsModule.parseAgentGuidanceConfig snap with
            | Ok config -> Some config
            | Error _ -> None
        | None -> None

    let agentGuidanceCandidateReadEffects workId model =
        match agentsConfigOpt model with
        | None -> []
        | Some config ->
            let already = plannedReadPaths model |> Set.ofList

            config.Targets
            |> List.map (fun target -> (resolveGeneratedRoot workId target.GeneratedRoot) + "/guidance.json")
            |> List.filter (fun path -> not (Set.contains (normalizeRelativePath path) already))
            |> List.distinct
            |> List.sort
            |> List.map ReadFile

    let agentGuidanceManifestJson
        (workId: string)
        (targetId: string)
        (generator: GeneratorVersion)
        (workModelP: string)
        (sourceDigest: SourceDigest)
        (behaviorDigest: SourceDigest)
        (commands: GuidanceCommandEntry list)
        (skills: GuidanceSkillEntry list)
        (renderedFiles: (string * string) list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("targetId", targetId)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")
        writer.WriteBoolean("generated", true)
        writer.WriteStartArray("sources")
        writer.WriteStartObject()
        writer.WriteString("path", workModelP)
        writer.WriteString("kind", "workModel")
        writeDigestObject writer "digest" (Some sourceDigest)
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("schemaStatus", "current")
        writer.WriteEndObject()
        writer.WriteEndArray()
        writeDigestObject writer "behaviorModelDigest" (Some sourceDigest |> Option.map (fun _ -> behaviorDigest))
        writer.WriteStartArray("commands")
        commands
        |> List.sortBy (fun command -> command.Id)
        |> List.iter (fun command ->
            writer.WriteStartObject()
            writer.WriteString("id", command.Id)
            writer.WriteString("title", command.Title)
            writer.WriteString("stage", command.Stage)
            writer.WriteString("purpose", command.Purpose)
            writeStringArray writer "relatedIds" command.RelatedIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("skills")
        skills
        |> List.sortBy (fun skill -> skill.Id)
        |> List.iter (fun skill ->
            writer.WriteStartObject()
            writer.WriteString("id", skill.Id)
            writer.WriteString("title", skill.Title)
            writer.WriteString("capability", skill.Capability)
            writeStringArray writer "relatedIds" skill.RelatedIds
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("renderedFiles")
        renderedFiles
        |> List.sortBy fst
        |> List.iter (fun (path, kind) ->
            writer.WriteStartObject()
            writer.WriteString("path", path)
            writer.WriteString("kind", kind)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let agentCommandsMarkdown (workId: string) (targetId: string) (commands: GuidanceCommandEntry list) =
        let builder = StringBuilder()
        builder.AppendLine($"# Agent commands for {targetId} (generated)") |> ignore
        builder.AppendLine("") |> ignore
        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the") |> ignore
        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.") |> ignore
        builder.AppendLine("") |> ignore

        commands
        |> List.sortBy (fun command -> command.Id)
        |> List.iter (fun command ->
            let related = String.concat ", " command.RelatedIds
            builder.AppendLine($"## {command.Id} — {command.Title}") |> ignore
            builder.AppendLine($"- Stage: {command.Stage}") |> ignore
            builder.AppendLine($"- Purpose: {command.Purpose}") |> ignore
            builder.AppendLine($"- Related: {related}") |> ignore
            builder.AppendLine("") |> ignore)

        builder.ToString()

    let agentSkillsMarkdown (workId: string) (targetId: string) (skills: GuidanceSkillEntry list) =
        let builder = StringBuilder()
        builder.AppendLine($"# Agent skills for {targetId} (generated)") |> ignore
        builder.AppendLine("") |> ignore
        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the") |> ignore
        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.") |> ignore
        builder.AppendLine("") |> ignore

        skills
        |> List.sortBy (fun skill -> skill.Id)
        |> List.iter (fun skill ->
            let related = String.concat ", " skill.RelatedIds
            builder.AppendLine($"## {skill.Id} — {skill.Title}") |> ignore
            builder.AppendLine($"- Capability: {skill.Capability}") |> ignore
            builder.AppendLine($"- Related: {related}") |> ignore
            builder.AppendLine("") |> ignore)

        builder.ToString()

    let agentGuidanceFindingSeverity (diagnostic: Diagnostic) =
        match diagnostic.Severity with
        | DiagnosticSeverity.DiagnosticError -> "blocking"
        | DiagnosticSeverity.DiagnosticWarning -> "warning"
        | DiagnosticSeverity.DiagnosticInfo -> "advisory"

    let agentGuidanceFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic -> sprintf "GF%03d" (index + 1), diagnostic, agentGuidanceFindingSeverity diagnostic)

    let computeAgentsPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, [], []
        | Some workId ->
            let request = model.Request
            let projectDiags = projectDiagnostics model
            let duplicateDiags = duplicateWorkIdDiagnostics workId model
            let configOpt = agentsConfigOpt model

            let configDiags =
                match configOpt with
                | None -> []
                | Some config ->
                    let noTargets =
                        if List.isEmpty config.Targets then [ agentsNoTargets ".fsgg/agents.yml" ] else []

                    let invalidTargets =
                        config.Targets
                        |> List.filter (fun target -> not (agentRootResolvesWithinProject workId target.GeneratedRoot))
                        |> List.map (fun target -> agentsInvalidGeneratedRoot ".fsgg/agents.yml" target.Id)

                    let invalidWorkModel =
                        if agentRootResolvesWithinProject workId config.WorkModelPath then
                            []
                        else
                            [ agentsInvalidGeneratedRoot ".fsgg/agents.yml" "workModel" ]

                    noTargets @ invalidTargets @ invalidWorkModel

            let workModelP = workModelPath workId
            let workModelSnap = snapshot workModelP model

            let workModelDiags, workModelOpt =
                match workModelSnap with
                | None -> [ agentsMissingWorkModel workModelP ], None
                | Some snap ->
                    match WorkModelModule.parseWorkModel snap with
                    | Error errs -> (errs |> List.map (fun diagnostic -> agentsMalformedWorkModel workModelP diagnostic.Message)), None
                    | Ok wm when not (String.Equals(wm.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                        [ agentsWorkModelIdentityMismatch workModelP workId wm.WorkId ], None
                    | Ok wm ->
                        let embedded = wm.Diagnostics

                        let unknownRefs =
                            embedded
                            |> List.filter (fun diagnostic -> diagnostic.Id.StartsWith("unknownReference", StringComparison.OrdinalIgnoreCase))
                            |> List.collect (fun diagnostic ->
                                match diagnostic.RelatedIds with
                                | [] -> [ agentsUnknownSourceReference workModelP diagnostic.Id ]
                                | ids -> ids |> List.map (agentsUnknownSourceReference workModelP))

                        let staleMarkers =
                            if embedded |> List.exists (fun diagnostic -> diagnostic.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) >= 0) then
                                [ agentsStaleWorkModel workModelP ]
                            else
                                []

                        let otherBlocking =
                            embedded
                            |> List.filter (fun diagnostic ->
                                diagnostic.Severity = DiagnosticSeverity.DiagnosticError
                                && not (diagnostic.Id.StartsWith("unknownReference", StringComparison.OrdinalIgnoreCase))
                                && diagnostic.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) < 0)

                        let blockedDiag =
                            if List.isEmpty otherBlocking then
                                []
                            else
                                [ agentsBlockedWorkModel workModelP (otherBlocking |> List.map (fun diagnostic -> diagnostic.Id) |> List.distinct |> List.sort) ]

                        let gateDiags = unknownRefs @ staleMarkers @ blockedDiag

                        if List.isEmpty gateDiags then [], Some wm else gateDiags, None

            let workModelText = workModelSnap |> Option.map (fun snap -> snap.Text) |> Option.defaultValue ""
            let sourceDigest = SchemaVersionModule.sha256Text workModelText
            let equivalenceRequired = configOpt |> Option.map (fun config -> config.RequireEquivalentClaudeAndCodexBehavior) |> Option.defaultValue true

            let targetResults =
                match configOpt, workModelOpt with
                | Some config, Some wm ->
                    let guidanceModel = WorkModelModule.deriveGuidanceModel wm
                    let behaviorDigest = WorkModelModule.behaviorModelDigest guidanceModel

                    config.Targets
                    |> List.sortBy (fun target -> target.Id)
                    |> List.map (fun target ->
                        let root = resolveGeneratedRoot workId target.GeneratedRoot
                        let guidancePath = root + "/guidance.json"
                        let commandsPath = root + "/commands.md"
                        let skillsPath = root + "/skills.md"
                        let renderedFiles = [ commandsPath, "commands"; skillsPath, "skills" ]
                        let manifestJson = agentGuidanceManifestJson workId target.Id request.GeneratorVersion workModelP sourceDigest behaviorDigest guidanceModel.Commands guidanceModel.Skills renderedFiles
                        let commandsMd = agentCommandsMarkdown workId target.Id guidanceModel.Commands
                        let skillsMd = agentSkillsMarkdown workId target.Id guidanceModel.Skills

                        let currency, targetDiags, divergent =
                            match snapshot guidancePath model with
                            | None -> GeneratedViewCurrency.Missing, [], false
                            | Some existing ->
                                match LifecycleArtifactsModule.parseGeneratedAgentGuidance existing with
                                | Error errs ->
                                    let message = errs |> List.tryHead |> Option.map (fun diagnostic -> diagnostic.Message) |> Option.defaultValue "Generated agent guidance is malformed."
                                    GeneratedViewCurrency.Malformed, [ agentsMalformedGeneratedGuidance guidancePath message ], false
                                | Ok manifest ->
                                    let recordedDigest = manifest.Sources |> List.tryPick (fun source -> source.Digest) |> Option.map (fun digest -> digest.Value)
                                    let digestMatches = recordedDigest = Some sourceDigest.Value
                                    let behaviorMatches = String.Equals(manifest.BehaviorModelDigest.Value, behaviorDigest.Value, StringComparison.OrdinalIgnoreCase)
                                    let divergent = equivalenceRequired && not behaviorMatches

                                    if digestMatches && behaviorMatches then
                                        GeneratedViewCurrency.Current, [], false
                                    else
                                        let staleDiag = [ agentsStaleGeneratedGuidance guidancePath target.Id ]
                                        let divergenceDiag = if divergent then [ agentsBehaviorDivergence guidancePath [ target.Id ] ] else []
                                        GeneratedViewCurrency.Stale, (staleDiag @ divergenceDiag), divergent

                        {| TargetId = target.Id
                           Root = root
                           GuidancePath = guidancePath
                           ManifestJson = manifestJson
                           CommandsPath = commandsPath
                           CommandsMd = commandsMd
                           SkillsPath = skillsPath
                           SkillsMd = skillsMd
                           Currency = currency
                           Diagnostics = targetDiags
                           Divergent = divergent |})
                | _ -> []

            let targetDiagnostics = targetResults |> List.collect (fun result -> result.Diagnostics)
            let baseDiagnostics = projectDiags @ duplicateDiags @ configDiags @ workModelDiags
            let diagnostics = baseDiagnostics @ targetDiagnostics |> DiagnosticsModule.sort
            let hasBlocking = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            let hasWarning = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticWarning)

            let divergentTargetIds =
                targetResults |> List.filter (fun result -> result.Divergent) |> List.map (fun result -> result.TargetId) |> List.distinct |> List.sort

            let generatedTargetIds = targetResults |> List.map (fun result -> result.TargetId) |> List.distinct |> List.sort
            let generatedRoots = targetResults |> List.map (fun result -> result.Root) |> List.distinct |> List.sort

            let refusedTargetIds =
                targetResults
                |> List.filter (fun result -> result.Currency = GeneratedViewCurrency.Malformed)
                |> List.map (fun result -> result.TargetId)
                |> List.distinct
                |> List.sort

            let effects =
                if hasBlocking then
                    []
                else
                    targetResults
                    |> List.collect (fun result ->
                        match result.Currency with
                        | GeneratedViewCurrency.Current -> []
                        | _ ->
                            [ CreateDirectory result.Root
                              WriteFile(result.GuidancePath, result.ManifestJson, GeneratedView)
                              WriteFile(result.CommandsPath, result.CommandsMd, GeneratedView)
                              WriteFile(result.SkillsPath, result.SkillsMd, GeneratedView) ])

            let generatedViews =
                targetResults
                |> List.map (fun result ->
                    shipGeneratedViewState
                        result.GuidancePath
                        "agent-commands"
                        request.GeneratorVersion
                        [ { Path = workModelP; Digest = Some sourceDigest; SchemaVersion = Some 1; SchemaStatus = Some "current" } ]
                        None
                        (if hasBlocking && (result.Currency = GeneratedViewCurrency.Missing || result.Currency = GeneratedViewCurrency.Stale) then GeneratedViewCurrency.Blocked else result.Currency)
                        (result.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)))

            let disposition =
                if hasBlocking then "blocked"
                elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale) && request.DryRun then "stale"
                elif hasWarning then "advisory"
                else "generated-current"

            let readiness = if hasBlocking then "needsAgentGuidanceCorrection" else "agentGuidanceReady"
            let findings = agentGuidanceFindings diagnostics
            let findingCount severity = findings |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity) |> List.length

            let generatedViewState =
                if hasBlocking then "blocked"
                elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale) then "stale"
                elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Missing) then "missing"
                elif List.isEmpty targetResults then "missing"
                else "current"

            let summary: AgentGuidanceSummary =
                { WorkId = workId
                  Stage = "agents"
                  Status = disposition
                  GeneratedRoots = generatedRoots
                  GeneratedTargetIds = generatedTargetIds
                  RefusedTargetIds = refusedTargetIds
                  FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                  ReadyFindingCount = if disposition = "generated-current" then List.length generatedTargetIds else 0
                  AdvisoryCount = findingCount "advisory"
                  WarningCount = findingCount "warning"
                  BlockingCount = findingCount "blocking"
                  Disposition = disposition
                  EquivalenceRequired = equivalenceRequired
                  DivergentTargetIds = divergentTargetIds
                  GeneratedViewState = generatedViewState
                  SourceSnapshotCount = (if Option.isSome workModelSnap then 1 else 0)
                  Readiness = readiness }

            diagnostics, Some summary, generatedViews, effects

    // --- refresh orchestration (cross-cutting; reuses the per-view generators) ---

    let refreshCanonicalViews = [ "work-model"; "analysis"; "verify"; "ship"; "agent-commands"; "summary" ]

    let refreshSummaryMarkdown
        (workId: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (stage: string)
        (outcomeText: string)
        (disposition: string)
        (perViewState: (string * string) list)
        (diagnostics: Diagnostic list)
        (nextActionText: string)
        =
        let body = StringBuilder()
        body.AppendLine($"# Readiness Summary — {workId}") |> ignore
        body.AppendLine("") |> ignore
        body.AppendLine($"**Lifecycle stage**: {stage}  **Outcome**: {outcomeText}  **Disposition**: {disposition}") |> ignore
        body.AppendLine("") |> ignore
        body.AppendLine("## Generated-view currency") |> ignore
        body.AppendLine("| View | State |") |> ignore
        body.AppendLine("|---|---|") |> ignore
        perViewState |> List.iter (fun (view, state) -> body.AppendLine($"| {view} | {state} |") |> ignore)
        body.AppendLine("") |> ignore
        body.AppendLine("## Diagnostics") |> ignore

        if List.isEmpty diagnostics then
            body.AppendLine("None") |> ignore
        else
            diagnostics
            |> DiagnosticsModule.sort
            |> List.iter (fun diagnostic ->
                let path = diagnostic.Artifact |> Option.map (fun artifact -> artifact.Path) |> Option.defaultValue "-"
                body.AppendLine($"- {diagnostic.Id} ({DiagnosticsModule.severityValue diagnostic.Severity}) {path}: {diagnostic.Message} — {diagnostic.Correction}") |> ignore)

        body.AppendLine("") |> ignore
        body.AppendLine("## Next action") |> ignore
        body.AppendLine(nextActionText) |> ignore
        let bodyText = body.ToString()
        let bodyDigest = (SchemaVersionModule.sha256Text bodyText).Value

        let header = StringBuilder()
        header.AppendLine("<!-- GENERATED by fsgg-sdd refresh — DO NOT EDIT.") |> ignore
        header.AppendLine($"     view: summary  schemaVersion: 1  generator: {generator.Id}/{generator.Version}") |> ignore
        header.AppendLine("     sources:") |> ignore

        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            let digest = source.Digest |> Option.map (fun value -> value.Value) |> Option.defaultValue "none"
            let schema = source.SchemaVersion |> Option.map string |> Option.defaultValue "none"
            let status = source.SchemaStatus |> Option.defaultValue "unknown"
            header.AppendLine($"       - {source.Path}  digest:{digest}  schema:{schema}({status})") |> ignore)

        header.AppendLine($"     outputDigest: {bodyDigest} -->") |> ignore
        header.AppendLine("") |> ignore
        header.ToString() + bodyText

    let computeRefreshPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, [], []
        | Some workId ->
            let request = model.Request
            let summaryPath = GenerationManifestModule.expectedSummaryOutputPath workId
            let projectDiags = projectDiagnostics model
            let duplicateDiags = duplicateWorkIdDiagnostics workId model

            let baseDiags = model.Diagnostics @ projectDiags @ duplicateDiags

            let baseBlocking =
                baseDiags |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            // The authored sources the structured views are derived from.
            let authoredSourcePaths = [ specPath workId; tasksPath workId; evidencePath workId ]

            let authoredPreserved =
                [ charterPath workId
                  specPath workId
                  clarificationPath workId
                  checklistPath workId
                  planPath workId
                  tasksPath workId
                  evidencePath workId
                  ".fsgg/project.yml"
                  ".fsgg/sdd.yml"
                  ".fsgg/agents.yml" ]
                |> List.filter (fun path -> Option.isSome (snapshot path model))

            if baseBlocking then
                let perViewState = refreshCanonicalViews |> List.map (fun view -> view, "blocked")

                let summary: RefreshSummary =
                    { WorkId = workId
                      Stage = "refresh"
                      Status = "blocked"
                      SummaryPath = summaryPath
                      RefreshedViewIds = []
                      AlreadyCurrentViewIds = []
                      BlockedViewIds = refreshCanonicalViews
                      NotApplicableViewIds = []
                      PreservedAuthoredPaths = authoredPreserved
                      FindingIds = baseDiags |> List.map (fun diagnostic -> diagnostic.Id) |> List.distinct |> List.sort
                      AdvisoryCount = 0
                      WarningCount = 0
                      BlockingCount = baseDiags |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError) |> List.length
                      Disposition = refreshDispositionValue RefreshBlocked
                      PerViewState = perViewState
                      SourceSnapshotCount = 0
                      Readiness = "needsRefreshCorrection" }

                (baseDiags |> DiagnosticsModule.sort), Some summary, [], []
            else
                let writeTextIn (effects: CommandEffect list) path =
                    effects
                    |> List.tryPick (function
                        | WriteFile(p, text, _) when normalizeRelativePath p = normalizeRelativePath path -> Some text
                        | _ -> None)

                let injectSnapshot path text (m: CommandModel) =
                    let key = readEffectKey path
                    let injected: CommandEffectResult =
                        { Effect = ReadFile path
                          Succeeded = true
                          Snapshot = Some { Path = path; Text = text }
                          Diagnostic = None }

                    { m with
                        InterpretedEffects =
                            (m.InterpretedEffects |> List.filter (fun result -> effectKey result.Effect <> key))
                            @ [ injected ] }

                let textOf path = snapshot path model |> Option.map (fun snap -> snap.Text)

                let parsesAsJson (text: string) =
                    try
                        use _ = System.Text.Json.JsonDocument.Parse text
                        true
                    with _ -> false

                // 1. Regenerate the normalized work model from its current declared sources.
                //    Reusing the same generator the lifecycle uses keeps output byte-identical.
                let wmDiags, wmView, wmEffects =
                    generatedViewPlan
                        request
                        workId
                        None
                        (textOf (specPath workId))
                        (textOf (clarificationPath workId))
                        (textOf (checklistPath workId))
                        (textOf (planPath workId))
                        (textOf (tasksPath workId))
                        (textOf (evidencePath workId))
                        []
                        model

                let wmWriteText = writeTextIn wmEffects (workModelPath workId)

                // Refreshed | already-current | blocked for the work model.
                let wmClass =
                    match wmWriteText with
                    | None -> "blocked"
                    | Some text ->
                        match snapshot (workModelPath workId) model with
                        | Some existing when existing.Text = text -> "already-current"
                        | _ -> "refreshed"

                let wmChanged = wmClass = "refreshed"

                // 2. Regenerate agent guidance from the refreshed work model (declared
                //    source-of order: the work model feeds agent guidance).
                let modelForAgents =
                    match wmWriteText with
                    | Some text when wmClass <> "blocked" -> injectSnapshot (workModelPath workId) text model
                    | _ -> model

                let _agDiag, _, agViews, agEffects =
                    if wmClass = "blocked" then [], None, [], [] else computeAgentsPlan modelForAgents

                let agentGuidancePaths = agViews |> List.map (fun view -> view.Path)

                let agentApplicable =
                    match agentsConfigOpt model with
                    | Some config -> not (List.isEmpty config.Targets)
                    | None -> false

                let agentBlocked =
                    wmClass = "blocked" || (agViews |> List.exists (fun view -> view.Currency = GeneratedViewCurrency.Blocked))

                let agentGuidanceWriteText path = writeTextIn agEffects path

                let agentClass =
                    if not agentApplicable then "not-applicable"
                    elif agentBlocked then "blocked"
                    elif List.isEmpty agentGuidancePaths then "blocked"
                    elif
                        agentGuidancePaths
                        |> List.forall (fun path ->
                            match agentGuidanceWriteText path with
                            | Some text -> (snapshot path model |> Option.map (fun snap -> snap.Text)) = Some text
                            | None -> true)
                    then "already-current"
                    else "refreshed"

                // 3. Evaluate currency of the structured downstream views (analysis,
                //    verify, ship). These are reported, not destructively regenerated:
                //    re-running their generators out of lifecycle order invalidates the
                //    evidence freshness they were verified against. If the work model
                //    changed, they are reported stale and point back to the responsible
                //    lifecycle command.
                let downstreamClass path =
                    if wmClass = "blocked" then "blocked"
                    else
                        match snapshot path model with
                        | None -> "missing"
                        | Some snap when not (parsesAsJson snap.Text) -> "malformed"
                        | Some _ -> if wmChanged then "stale" else "already-current"

                let anClass = downstreamClass (analysisPath workId)
                let veClass = downstreamClass (verifyPath workId)
                let shClass = downstreamClass (shipPath workId)

                let structuredClasses =
                    [ "work-model", wmClass; "analysis", anClass; "verify", veClass; "ship", shClass ]

                let isClean state = state = "refreshed" || state = "already-current"
                let structuredAllClean = structuredClasses |> List.forall (fun (_, state) -> isClean state)
                let structuredNoneClean = structuredClasses |> List.forall (fun (_, state) -> not (isClean state))

                // --- refresh-specific diagnostics ---
                let missingAuthored = authoredSourcePaths |> List.filter (fun path -> Option.isNone (snapshot path model))

                let workModelDiags =
                    if wmClass = "blocked" then
                        match missingAuthored with
                        | missing :: _ -> [ refreshMissingSource (workModelPath workId) missing ]
                        | [] -> [ refreshMalformedSource (workModelPath workId) (specPath workId) $"A declared source for '{workModelPath workId}' is malformed or schema-incompatible." ]
                    elif wmChanged then
                        match snapshot (workModelPath workId) model with
                        | Some existing ->
                            match GenerationManifestModule.parseWorkModelMetadata (workModelPath workId) existing.Text with
                            | Error _ -> [ refreshMalformedGeneratedView (workModelPath workId) $"Generated view '{workModelPath workId}' was unreadable and was refreshed from current sources." ]
                            | Ok _ -> []
                        | None -> []
                    else
                        []

                let downstreamDiags =
                    [ analysisPath workId, anClass; verifyPath workId, veClass; shipPath workId, shClass ]
                    |> List.collect (fun (viewPath, state) ->
                        match state with
                        | "blocked" -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | "stale" -> [ refreshStaleView viewPath [ workModelPath workId ] ]
                        | "malformed" -> [ refreshMalformedGeneratedView viewPath $"Generated view '{viewPath}' is malformed; re-run the responsible lifecycle command." ]
                        | "missing" -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | _ -> [])

                let summaryRenderable = structuredAllClean

                let summaryDiags =
                    if summaryRenderable then
                        []
                    else
                        let related =
                            structuredClasses
                            |> List.filter (fun (_, state) -> not (isClean state))
                            |> List.map fst

                        [ refreshUnrenderableSummary summaryPath related ]

                let refreshDiags = workModelDiags @ downstreamDiags @ summaryDiags

                let allDiags =
                    (baseDiags @ refreshDiags)
                    |> List.distinctBy (fun diagnostic -> diagnostic.Id, diagnostic.Message)
                    |> DiagnosticsModule.sort

                // --- summary projection ---
                let structuredSourcePaths =
                    [ workModelPath workId; analysisPath workId; verifyPath workId; shipPath workId ] @ agentGuidancePaths

                let summarySources =
                    structuredSourcePaths
                    |> List.choose (fun path ->
                        let textOpt =
                            match writeTextIn (wmEffects @ agEffects) path with
                            | Some text -> Some text
                            | None -> snapshot path model |> Option.map (fun snap -> snap.Text)

                        textOpt
                        |> Option.map (fun text ->
                            { Path = path
                              Digest = Some(SchemaVersionModule.sha256Text text)
                              SchemaVersion = Some 1
                              SchemaStatus = Some "current" }))

                let stageText = "refresh"
                let outcomeText = if structuredAllClean && agentClass <> "blocked" then "succeeded" else "succeededWithWarnings"

                let disposition =
                    if wmClass = "blocked" || structuredNoneClean then RefreshBlocked
                    elif structuredAllClean && (agentClass = "refreshed" || agentClass = "already-current" || agentClass = "not-applicable") then RefreshedCurrent
                    else PartiallyBlocked

                let dispositionValue = refreshDispositionValue disposition

                // currency word per view for the report and summary table
                let viewWord state =
                    match state with
                    | "refreshed"
                    | "already-current" -> "current"
                    | other -> other

                let perViewState =
                    [ "work-model", viewWord wmClass
                      "analysis", viewWord anClass
                      "verify", viewWord veClass
                      "ship", viewWord shClass
                      "agent-commands", viewWord agentClass
                      "summary", (if summaryRenderable then "current" else "blocked") ]

                let summaryClass, summaryEffects, summaryViewState =
                    if not summaryRenderable then
                        "blocked", [], None
                    else
                        let nextActionText =
                            match disposition with
                            | RefreshedCurrent -> "Generated views are current; rely on the refreshed readiness for the selected work item."
                            | _ -> "Correct the named source or upstream view, then re-run fsgg-sdd refresh."

                        let text =
                            refreshSummaryMarkdown workId request.GeneratorVersion summarySources stageText outcomeText dispositionValue perViewState allDiags nextActionText

                        let cls =
                            match snapshot summaryPath model with
                            | Some existing when existing.Text = text -> "already-current"
                            | _ -> "refreshed"

                        let effects = [ CreateDirectory(readinessDirectory workId); WriteFile(summaryPath, text, GeneratedView) ]

                        let view =
                            { Path = summaryPath
                              Kind = "summary"
                              SchemaVersion = Some 1
                              Generator = Some request.GeneratorVersion
                              Sources = summarySources
                              OutputDigest = None
                              Currency = GeneratedViewCurrency.Current
                              DiagnosticIds = [] }

                        cls, effects, Some view

                let classifyToBucket viewId state buckets =
                    let refreshed, current, blocked, na = buckets
                    match state with
                    | "refreshed" -> viewId :: refreshed, current, blocked, na
                    | "already-current" -> refreshed, viewId :: current, blocked, na
                    | "not-applicable" -> refreshed, current, blocked, viewId :: na
                    | _ -> refreshed, current, viewId :: blocked, na

                let refreshedViewIds, alreadyCurrentViewIds, blockedViewIds, notApplicableViewIds =
                    [ "work-model", wmClass; "analysis", anClass; "verify", veClass; "ship", shClass; "agent-commands", agentClass; "summary", summaryClass ]
                    |> List.fold (fun acc (viewId, state) -> classifyToBucket viewId state acc) ([], [], [], [])

                let findingSeverityCount severity =
                    refreshDiags |> List.filter (fun diagnostic -> diagnostic.Severity = severity) |> List.length

                let sourceSnapshotCount =
                    [ workModelPath workId; analysisPath workId; verifyPath workId; shipPath workId ]
                    |> List.filter (fun path -> Option.isSome (snapshot path model))
                    |> List.length

                let summaryRecord: RefreshSummary =
                    { WorkId = workId
                      Stage = stageText
                      Status = dispositionValue
                      SummaryPath = summaryPath
                      RefreshedViewIds = refreshedViewIds |> List.sort
                      AlreadyCurrentViewIds = alreadyCurrentViewIds |> List.sort
                      BlockedViewIds = blockedViewIds |> List.sort
                      NotApplicableViewIds = notApplicableViewIds |> List.sort
                      PreservedAuthoredPaths = authoredPreserved |> List.sort
                      FindingIds = refreshDiags |> List.map (fun diagnostic -> diagnostic.Id) |> List.distinct |> List.sort
                      AdvisoryCount = findingSeverityCount DiagnosticSeverity.DiagnosticInfo
                      WarningCount = findingSeverityCount DiagnosticSeverity.DiagnosticWarning
                      BlockingCount = findingSeverityCount DiagnosticSeverity.DiagnosticError
                      Disposition = dispositionValue
                      PerViewState = perViewState
                      SourceSnapshotCount = sourceSnapshotCount
                      Readiness = if disposition = RefreshBlocked then "needsRefreshCorrection" else "refreshReady" }

                // --- canonical generated-view set ---
                let currencyOf state =
                    match state with
                    | "refreshed"
                    | "already-current" -> GeneratedViewCurrency.Current
                    | "stale" -> GeneratedViewCurrency.Stale
                    | "malformed" -> GeneratedViewCurrency.Malformed
                    | "missing" -> GeneratedViewCurrency.Missing
                    | _ -> GeneratedViewCurrency.Blocked

                let downstreamView path kind state =
                    { Path = path
                      Kind = kind
                      SchemaVersion = Some 1
                      Generator = Some request.GeneratorVersion
                      Sources = []
                      OutputDigest = None
                      Currency = currencyOf state
                      DiagnosticIds = [] }

                let workModelViewState = { wmView with Currency = currencyOf wmClass }

                let agentViewStates =
                    agViews
                    |> List.map (fun view ->
                        let state =
                            match agentGuidanceWriteText view.Path with
                            | Some text -> if (snapshot view.Path model |> Option.map (fun snap -> snap.Text)) = Some text then "already-current" else "refreshed"
                            | None -> "blocked"

                        { view with Currency = currencyOf state })

                let generatedViews =
                    [ workModelViewState
                      downstreamView (analysisPath workId) "analysis" anClass
                      downstreamView (verifyPath workId) "verification" veClass
                      downstreamView (shipPath workId) "ship" shClass ]
                    @ agentViewStates
                    @ (summaryViewState |> Option.toList)

                let dedupEffects effects =
                    effects
                    |> List.fold
                        (fun (seen, acc) effect ->
                            let key = effectKey effect
                            if Set.contains key seen then seen, acc else Set.add key seen, acc @ [ effect ])
                        (Set.empty, [])
                    |> snd

                let effects = dedupEffects (wmEffects @ agEffects @ summaryEffects)

                // wmDiags are the reused generator's own staleness heuristics about the
                // prior on-disk work model; refresh reports its own per-view diagnostics
                // (allDiags), so the generator's internal diagnostics are not surfaced.
                ignore wmDiags

                allDiags, Some summaryRecord, generatedViews, effects

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
