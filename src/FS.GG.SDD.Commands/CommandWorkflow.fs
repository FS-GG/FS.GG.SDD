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

    let workModelSnapshots workId charterText specText model =
        [ snapshot ".fsgg/project.yml" model
          snapshot ".fsgg/sdd.yml" model
          snapshot ".fsgg/agents.yml" model
          specText
          |> Option.map (fun text -> { Path = specPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (specPath workId) model)
          snapshot (tasksPath workId) model
          snapshot (evidencePath workId) model
          Some({ Path = charterPath workId; Text = charterText }) ]
        |> List.choose id
        |> List.map (fun snapshot -> { snapshot with Path = normalizeRelativePath snapshot.Path })

    let generatedViewPlan request workId charterText specText commandDiagnostics model =
        let path = workModelPath workId
        let currentDiagnostic = existingGeneratedViewDiagnostic workId path model
        let blockingCommandIds =
            commandDiagnostics
            |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            |> List.map _.Id

        if not (List.isEmpty blockingCommandIds) then
            let sources =
                [ Some(charterSource (charterPath workId) charterText)
                  specText |> Option.map (fun text -> charterSource (specPath workId) text) ]
                |> List.choose id

            let view = generatedViewState path request.GeneratorVersion sources None GeneratedViewCurrency.Blocked blockingCommandIds
            currentDiagnostic |> Option.toList, view, []
        else
            let snapshots = workModelSnapshots workId charterText specText model

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
                    [ Some(charterSource (charterPath workId) charterText)
                      specText |> Option.map (fun text -> charterSource (specPath workId) text) ]
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
            let generatedDiagnostics, generatedView, generatedEffects = generatedViewPlan model.Request workId charterText None commandDiagnostics model
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
                | Some text -> generatedViewPlan model.Request workId text specText commandDiagnostics model
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

    let nextLifecycleEffects model =
        match model.Request.Command, model.Request.WorkId with
        | (Charter | Specify), Some workId when not (hasPlannedWrite model) ->
            if not (allPlannedReadsInterpreted model) then
                model, []
            else
                let candidateReads = duplicateCandidateReadEffects workId model

                match candidateReads with
                | _ :: _ ->
                    let effects = appendNewEffects candidateReads model
                    { model with PendingEffects = model.PendingEffects @ effects }, effects
                | [] ->
                    let diagnostics, specification, generatedViews, plannedEffects =
                        match model.Request.Command with
                        | Charter -> computeCharterPlan model
                        | Specify -> computeSpecifyPlan model
                        | _ -> model.Diagnostics, None, [], []

                    let effects = appendNewEffects plannedEffects model

                    let plannedModel =
                        { model with
                            PendingEffects = model.PendingEffects @ effects
                            Diagnostics = diagnostics
                            Specification = specification
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
