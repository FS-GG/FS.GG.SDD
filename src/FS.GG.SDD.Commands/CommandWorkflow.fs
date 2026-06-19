namespace FS.GG.SDD.Commands

open System
open System.IO
open System.Text.RegularExpressions
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
    let tasksPath workId = $"work/{workId}/tasks.yml"
    let evidencePath workId = $"work/{workId}/evidence.yml"
    let workModelPath workId = GenerationManifestModule.expectedWorkModelOutputPath workId
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

    let workModelSnapshots workId charterText specText clarificationText checklistText model =
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
          snapshot (tasksPath workId) model
          snapshot (evidencePath workId) model
          charterText
          |> Option.map (fun text -> { Path = charterPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (charterPath workId) model) ]
        |> List.choose id
        |> List.map (fun snapshot -> { snapshot with Path = normalizeRelativePath snapshot.Path })

    let generatedViewPlan request workId charterText specText clarificationText checklistText commandDiagnostics model =
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
                  checklistText |> Option.map (fun text -> charterSource (checklistPath workId) text) ]
                |> List.choose id

            let view = generatedViewState path request.GeneratorVersion sources None GeneratedViewCurrency.Blocked blockingCommandIds
            currentDiagnostic |> Option.toList, view, []
        else
            let snapshots = workModelSnapshots workId charterText specText clarificationText checklistText model

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
                      checklistText |> Option.map (fun text -> charterSource (checklistPath workId) text) ]
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
            let generatedDiagnostics, generatedView, generatedEffects = generatedViewPlan model.Request workId (Some charterText) None None None commandDiagnostics model
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
                | Some text -> generatedViewPlan model.Request workId (Some text) specText None None commandDiagnostics model
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
                    generatedViewPlan model.Request workId charterText (Some text) clarificationText None commandDiagnostics model
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
                    generatedViewPlan model.Request workId charterText (Some specText) (Some clarificationText) checklistText commandDiagnostics model
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

    let nextLifecycleEffects model =
        match model.Request.Command, model.Request.WorkId with
        | (Charter | Specify | Clarify | Checklist), Some workId when not (hasPlannedWrite model) ->
            if not (allPlannedReadsInterpreted model) then
                model, []
            else
                let candidateReads = duplicateCandidateReadEffects workId model

                match candidateReads with
                | _ :: _ ->
                    let effects = appendNewEffects candidateReads model
                    { model with PendingEffects = model.PendingEffects @ effects }, effects
                | [] ->
                    let diagnostics, specification, clarification, generatedViews, plannedEffects =
                        match model.Request.Command with
                        | Charter ->
                            let diagnostics, specification, generatedViews, effects = computeCharterPlan model
                            diagnostics, specification, None, generatedViews, effects
                        | Specify ->
                            let diagnostics, specification, generatedViews, effects = computeSpecifyPlan model
                            diagnostics, specification, None, generatedViews, effects
                        | Clarify ->
                            let diagnostics, specification, clarification, generatedViews, effects = computeClarifyPlan model
                            diagnostics, specification, clarification, generatedViews, effects
                        | Checklist ->
                            let diagnostics, specification, clarification, checklist, generatedViews, effects = computeChecklistPlan model
                            diagnostics, specification, clarification, generatedViews, effects
                        | _ -> model.Diagnostics, None, None, [], []

                    let checklist =
                        match model.Request.Command with
                        | Checklist ->
                            let _, _, _, checklist, _, _ = computeChecklistPlan model
                            checklist
                        | _ -> None

                    let effects = appendNewEffects plannedEffects model

                    let plannedModel =
                        { model with
                            PendingEffects = model.PendingEffects @ effects
                            Diagnostics = diagnostics
                            Specification = specification
                            Clarification = clarification
                            Checklist = checklist
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
