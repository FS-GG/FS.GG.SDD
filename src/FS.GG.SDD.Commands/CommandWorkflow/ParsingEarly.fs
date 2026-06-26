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
module internal ParsingEarly =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

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
        let value = if String.IsNullOrEmpty value then "" else value.Trim()

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
        let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")
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
        let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            specificationStandardSections ()
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
                match parseWorkItemMetadata snapshot with
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
                      match parseProjectConfig snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedProjectConfig snapshot.Path
                  | None -> ()

                  match sdd with
                  | Some snapshot ->
                      match parseSddLifecyclePolicy snapshot with
                      | Ok _ -> ()
                      | Error _ -> yield malformedSddConfig snapshot.Path
                  | None -> ()

                  match agents with
                  | Some snapshot ->
                      match parseAgentGuidanceConfig snapshot with
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

        match parseSpecificationFacts snapshot with
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
                // Whether the spec's own diagnostics block rewriting its sections — a
                // content decision distinct from the handler effect-gate `hasBlocking`
                // single-sourced in `runHandler`.
                let sectionsBlocked = allDiagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                let text = if sectionsBlocked then existing.Text else ensureSpecificationSections existing.Text

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
        let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            clarificationStandardSections ()
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

        match parseClarificationFacts snapshot with
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
        Regex.Replace((if String.IsNullOrEmpty text then "" else text).Trim().ToLowerInvariant(), @"\s+", " ")

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
            let normalized = (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")
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

