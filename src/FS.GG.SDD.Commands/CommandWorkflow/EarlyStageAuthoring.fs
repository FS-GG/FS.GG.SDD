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

module internal EarlyStageAuthoring =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

    let parseCharterFrontMatter path text =
        match splitFrontMatter text with
        | None -> Error(malformedCharterFrontMatter path "Charter is missing YAML front matter.")
        | Some(yaml, _) ->
            match
                tryScalar "schemaVersion" yaml,
                tryScalar "workId" yaml,
                tryScalar "title" yaml,
                tryScalar "stage" yaml,
                tryScalar "changeTier" yaml,
                tryScalar "status" yaml
            with
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
        |> fun parts ->
            if Array.isEmpty parts then
                workId.Split('-', StringSplitOptions.RemoveEmptyEntries)
            else
                parts
        |> Array.map (fun part ->
            if part.Length = 0 then
                part
            else
                Char.ToUpperInvariant(part.[0]).ToString() + part.Substring(1))
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
            | "in-scope" ->
                { intent with
                    Scope = intent.Scope @ [ value ] }
            | "non-goal"
            | "non goal"
            | "out of scope"
            | "out-of-scope" ->
                { intent with
                    NonGoals = intent.NonGoals @ [ value ] }
            | "story"
            | "user story" ->
                { intent with
                    Stories = intent.Stories @ [ value ] }
            | "requirement"
            | "functional requirement"
            | "fr" ->
                { intent with
                    Requirements = intent.Requirements @ [ value ] }
            | "acceptance"
            | "acceptance scenario"
            | "scenario" ->
                { intent with
                    AcceptanceScenarios = intent.AcceptanceScenarios @ [ value ] }
            | "ambiguity"
            | "question" ->
                { intent with
                    Ambiguities = intent.Ambiguities @ [ value ] }
            | "impact"
            | "public or tool-facing impact" ->
                { intent with
                    Impact = intent.Impact @ [ value ] }
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
            [ if Option.isNone parsed.UserValue then
                  "user value"
              if List.isEmpty parsed.Scope then
                  "scope"
              if List.isEmpty parsed.Requirements then
                  "measurable requirement" ]

        parsed, missing

    let numberedId prefix index = sprintf "%s-%03d" prefix (index + 1)

    // --- Feature-shaped specify seed (feature 089 / FS-GG/FS.GG.SDD#174 §WD7) --------------
    // When the author supplies no story or acceptance scenario, `specify` used to seed two
    // sentences about the SDD process ("As a maintainer, I can specify X after chartering the
    // work item."). They read as boilerplate to delete. These three total helpers derive a
    // feature-shaped placeholder from the only feature-describing facts available at seeding
    // time: the required `value:` intent fact, and the invocation title. (The charter title is
    // NOT reachable here — `requestTitle` reads this invocation's `--title`, else the humanized
    // work id — so the user value is what carries the meaning. See research D1.)
    // Applied to the seeded lines only, never to author-supplied scope/requirement/non-goal text.

    /// Drop at most one trailing period so an interpolated value never yields `..`.
    let trimTrailingPeriod (text: string) =
        let trimmed = text.Trim()

        if trimmed.EndsWith('.') then
            trimmed.Substring(0, trimmed.Length - 1).TrimEnd()
        else
            trimmed

    /// Lowercase the first letter only when it starts an ordinary capitalized word, so
    /// "Let a player…" reads mid-sentence while "MP4 export…" is left intact.
    let decapitalizeFirst (text: string) =
        if text.Length > 1 && Char.IsUpper text[0] && Char.IsLower text[1] then
            string (Char.ToLowerInvariant text[0]) + text.Substring(1)
        else
            text

    /// Author text is interpolated into lines that the artifact parsers scan for stable-id
    /// cross-references. Rewrite any id-shaped token (`FR-002`) by replacing its hyphen with a
    /// space (`FR 002`) so a seeded `US-001`/`AC-001` line cannot manufacture a reference the
    /// author never intended (FR-017).
    ///
    /// Case-INSENSITIVE, and matched against the same id families the parsers scan for: every id
    /// scanner in the artifact layer (`idMatches`, `scopedIdLocations`, the specification's
    /// unresolved-ambiguity count) uses `RegexOptions.IgnoreCase`, so a case-sensitive rewrite here
    /// would let `amb-001` in the author's user value survive into the seed and be counted as a
    /// real ambiguity reference.
    let neutralizeIds (text: string) =
        Regex.Replace(text, @"\b(AMB|CQ|DEC|FR|US|AC|SB|PD)-(\d{3,})\b", "$1 $2", RegexOptions.IgnoreCase)

    /// The capability clause of the seeded story: the author's user value, made to read after
    /// "I can ". Neutralize BEFORE decapitalizing: decapitalizing first can turn `Amb-001` into
    /// `amb-001`, and only a case-insensitive rewrite would still catch it.
    let seedCapability (userValue: string) =
        userValue |> trimTrailingPeriod |> neutralizeIds |> decapitalizeFirst

    let specificationTemplate request workId intent =
        let title = requestTitle request workId

        let userValue =
            intent.UserValue |> Option.defaultValue $"Specify work item {workId}."

        let scope =
            if List.isEmpty intent.Scope then
                [ "Author one chartered SDD work item specification." ]
            else
                intent.Scope

        let nonGoals =
            if List.isEmpty intent.NonGoals then
                [ "Do not implement later lifecycle commands or Governance enforcement in this specification." ]
            else
                intent.NonGoals

        let requirements =
            if List.isEmpty intent.Requirements then
                [ "Create a specification artifact with stable ids." ]
            else
                intent.Requirements

        // Feature-shaped placeholder seeds (089 §WD7). The ids and every cross-reference below
        // are unchanged; only the prose after the `:` moves, so `checklist` coverage and the
        // `plan`/`tasks` back-references still resolve. Neither seed names the SDD process.
        let capability = seedCapability userValue
        let seedTitle = neutralizeIds title

        let stories =
            if List.isEmpty intent.Stories then
                [ $"As a user, I can {capability}." ]
            else
                intent.Stories

        let acceptanceScenarios =
            if List.isEmpty intent.AcceptanceScenarios then
                [ $"Given {seedTitle} is available, when the user exercises it, then they can {capability}." ]
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
        | "Scope Boundaries" ->
            "## Scope Boundaries\n- Keep SDD lifecycle ownership separate from optional Governance enforcement.\n"
        | "Policy Pointers" ->
            "## Policy Pointers\n- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.\n- Governance files are optional compatibility pointers and are not evaluated by this command.\n"
        | "Lifecycle Notes" -> $"## Lifecycle Notes\n- Next lifecycle action: `fsgg-sdd specify --work {workId}`.\n"
        | _ -> $"## {heading}\n"

    let ensureStandardSections workId text =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let required =
            [ "Identity"
              "Principles"
              "Scope Boundaries"
              "Policy Pointers"
              "Lifecycle Notes" ]

        let missing =
            required |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix = missing |> List.map (sectionText workId) |> String.concat "\n"

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
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

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
            | ReadFile path, Some snapshot when
                path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase)
                ->
                Some(
                    { snapshot with
                        Path = normalizeRelativePath path }
                )
            | _ -> None)

    let duplicateWorkIdDiagnostics workId model =
        candidateSnapshots model
        |> List.choose (fun snapshot ->
            if snapshot.Path.StartsWith($"work/{workId}/", StringComparison.OrdinalIgnoreCase) then
                None
            elif snapshot.Path.EndsWith("/charter.md", StringComparison.OrdinalIgnoreCase) then
                match parseCharterFrontMatter snapshot.Path snapshot.Text with
                | Ok frontMatter when String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase) ->
                    Some snapshot.Path
                | _ -> None
            elif snapshot.Path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then
                match parseWorkItemMetadata snapshot with
                | Ok metadata when String.Equals(metadata.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase) ->
                    Some snapshot.Path
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
        | None, None, None -> [ outsideProject () ]
        | _ ->
            let missing =
                [ if Option.isNone project then
                      missingProjectConfig ".fsgg/project.yml"
                  if Option.isNone sdd then
                      missingSddConfig ".fsgg/sdd.yml"
                  if Option.isNone agents then
                      missingAgentsConfig ".fsgg/agents.yml" ]

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
                [ malformedCharterFrontMatter
                      path
                      $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ],
                existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ malformedCharterFrontMatter path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ],
                existing.Text
            | Ok _ when
                existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase)
                ->
                [ unsafeOverwrite path ], existing.Text
            | Ok _ -> [], ensureStandardSections workId existing.Text

    let charterPrerequisiteDiagnosticsAndText workId model =
        let path = charterPath workId

        match snapshot path model with
        | None -> [ missingCharterPrerequisite path $"Charter prerequisite '{path}' is missing." ], None
        | Some existing ->
            match parseCharterFrontMatter path existing.Text with
            | Error _ ->
                [ missingCharterPrerequisite path "Charter prerequisite front matter is malformed." ],
                Some existing.Text
            | Ok frontMatter when frontMatter.SchemaVersion <> "1" ->
                [ missingCharterPrerequisite
                      path
                      $"Charter schemaVersion '{frontMatter.SchemaVersion}' is not supported." ],
                Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ charterIdentityMismatch path workId frontMatter.WorkId ], Some existing.Text
            | Ok frontMatter when not (String.Equals(frontMatter.Stage, "charter", StringComparison.OrdinalIgnoreCase)) ->
                [ missingCharterPrerequisite path $"Charter stage '{frontMatter.Stage}' is not 'charter'." ],
                Some existing.Text
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
            | "workModelInconsistent", idFamily :: _ when idFamily.EndsWith("###", StringComparison.Ordinal) ->
                missingSpecificationId path idFamily
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
                if
                    existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase)
                then
                    [ unsafeOverwrite path ]
                else
                    []

            match parseSpecificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic ->
                        if
                            diagnostic.Id = "workModelInconsistent"
                            || diagnostic.Id = "malformedSchemaVersion"
                        then
                            malformedSpecificationFrontMatter path diagnostic.Message
                        else
                            diagnostic)

                unsafe @ mapped |> DiagnosticsModule.sort, Some existing.Text, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Specification"
                        LifecycleStage.Specify
                        "specify"
                        malformedSpecificationFrontMatter
                        specificationIdentityMismatch
                        malformedSpecificationFrontMatter
                        path
                        workId
                        facts.FrontMatter.SchemaVersion.Major
                        facts.FrontMatter.WorkId.Value
                        facts.FrontMatter.Stage

                let allDiagnostics =
                    unsafe @ identityDiagnostics @ diagnostics |> DiagnosticsModule.sort
                // Whether the spec's own diagnostics block rewriting its sections — a
                // content decision distinct from the handler effect-gate `hasBlocking`
                // single-sourced in `runHandler`.
                let sectionsBlocked =
                    allDiagnostics
                    |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                let text =
                    if sectionsBlocked then
                        existing.Text
                    else
                        ensureSpecificationSections existing.Text

                let summary =
                    match parseSpecificationForCommand path text with
                    | Ok(nextFacts, _) -> Some(specificationSummary nextFacts)
                    | Error _ -> Some(specificationSummary facts)

                allDiagnostics, Some text, summary

    let specificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model =
        let path = specPath workId

        match snapshot path model with
        | None ->
            [ missingSpecificationPrerequisite path $"Specification prerequisite '{path}' is missing." ],
            None,
            None,
            None
        | Some existing ->
            match parseSpecificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedSpecificationFacts path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Specification"
                        LifecycleStage.Specify
                        "specify"
                        malformedSpecificationFacts
                        specificationIdentityMismatch
                        missingSpecificationPrerequisite
                        path
                        workId
                        facts.FrontMatter.SchemaVersion.Major
                        facts.FrontMatter.WorkId.Value
                        facts.FrontMatter.Stage

                let mappedDiagnostics =
                    diagnostics
                    |> List.map (fun diagnostic ->
                        match diagnostic.Id, diagnostic.RelatedIds with
                        | "duplicateSpecificationId", _
                        | "unknownSpecificationReference", _
                        | "missingSpecificationId", _ -> diagnostic
                        | _ -> malformedSpecificationFacts path diagnostic.Message)

                let allDiagnostics =
                    identityDiagnostics @ mappedDiagnostics |> DiagnosticsModule.sort

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
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            clarificationStandardSections ()
            |> List.filter (fun heading -> not (hasSection heading normalized))

        if List.isEmpty missing then
            normalized
        else
            let suffix =
                missing |> List.map (clarificationSectionText workId) |> String.concat "\n"

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
            [ facts.Answers
              |> List.choose (fun answer -> answer.QuestionId |> Option.map _.Value)
              facts.Decisions
              |> List.collect (fun decision -> decision.SourceQuestionIds |> List.map _.Value)
              facts.AcceptedDeferrals
              |> List.collect (fun decision -> decision.SourceQuestionIds |> List.map _.Value) ]
            |> List.concat
            |> List.distinct
            |> List.sort

        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          QuestionIds =
            facts.Questions
            |> List.map (fun question -> question.QuestionId.Value)
            |> List.sort
          AnsweredQuestionIds = answeredQuestionIds
          DecisionIds =
            facts.Decisions
            |> List.map (fun decision -> decision.DecisionId.Value)
            |> List.sort
          AcceptedDeferralIds =
            facts.AcceptedDeferrals
            |> List.map (fun decision -> decision.DecisionId.Value)
            |> List.sort
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

    let knownQuestionIdForAmbiguity
        (ambiguityIndex: int)
        (existingQuestions: ClarificationQuestion list)
        (ambiguityValue: string)
        =
        existingQuestions
        |> List.tryFind (fun question ->
            question.SourceAmbiguityIds
            |> List.exists (fun ambiguity ->
                String.Equals(ambiguity.Value, ambiguityValue, StringComparison.OrdinalIgnoreCase)))
        |> Option.map (fun question -> question.QuestionId.Value)
        |> Option.defaultValue (scopedId "CQ" (ambiguityIndex + 1))

    let existingResolutionTextForAmbiguity (facts: ClarificationFacts option) ambiguityValue =
        facts
        |> Option.bind (fun facts ->
            (facts.Decisions @ facts.AcceptedDeferrals)
            |> List.tryFind (fun decision ->
                decision.SourceAmbiguityIds
                |> List.exists (fun ambiguity ->
                    String.Equals(ambiguity.Value, ambiguityValue, StringComparison.OrdinalIgnoreCase)))
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

        let knownScenarios =
            specFacts.AcceptanceScenarioIds |> List.map _.Value |> Set.ofList

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
            |> List.choose (fun id ->
                if Set.contains id known then
                    None
                else
                    Some(unknownClarificationReference path id))

        [ check @"\bAMB-\d{3,}\b" knownAmbiguities
          check @"\bFR-\d{3,}\b" knownRequirements
          check @"\bUS-\d{3,}\b" knownStories
          check @"\bAC-\d{3,}\b" knownScenarios
          check @"\bCQ-\d{3,}\b" knownQuestions ]
        |> List.concat

    let plannedClarificationAnswers
        (path: string)
        (request: CommandRequest)
        (specFacts: SpecificationFacts)
        (existingFacts: ClarificationFacts option)
        =
        let lines = inputLines request

        let existingQuestions =
            existingFacts |> Option.map _.Questions |> Option.defaultValue []

        let unknownReferences =
            unknownReferenceDiagnostics path specFacts existingQuestions lines

        let unresolvedAmbiguities =
            specFacts.AmbiguityIds
            |> List.mapi (fun index ambiguity ->
                let ambiguityValue = ambiguity.Value

                let existingResolution =
                    existingResolutionTextForAmbiguity existingFacts ambiguityValue

                let matchingLine =
                    lines
                    |> List.tryFind (fun line ->
                        line.IndexOf(ambiguityValue, StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf(
                            knownQuestionIdForAmbiguity index existingQuestions ambiguityValue,
                            StringComparison.OrdinalIgnoreCase
                           )
                           >= 0)
                    |> Option.orElseWith (fun () ->
                        if
                            specFacts.AmbiguityIds.Length = 1
                            && lines.Length = 1
                            && List.isEmpty (idMatches @"\b(?:AMB|CQ|FR|US|AC)-\d{3,}\b" lines.Head)
                        then
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
                    | Some _ -> None
                    | None ->
                        let decisionId =
                            if kind = "stillOpen" then
                                None
                            else
                                let id = scopedId "DEC" nextDecision
                                nextDecision <- nextDecision + 1
                                Some id

                        Some(
                            { AmbiguityId = ambiguityValue
                              QuestionId = knownQuestionIdForAmbiguity index existingQuestions ambiguityValue
                              DecisionId = decisionId
                              Kind = kind
                              Text = if String.IsNullOrWhiteSpace text then line else text }
                            : PlannedClarificationAnswer
                        ))

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
        | Some decisionId -> Some $"- {decisionId} [{answer.QuestionId}] [AMB:{answer.AmbiguityId}]: {answer.Text}"
        | None -> None

    let renderRemainingLine (answer: PlannedClarificationAnswer) =
        if answer.Kind = "stillOpen" then
            Some $"- {answer.AmbiguityId} [{answer.QuestionId}] blocking: {answer.Text}"
        else
            None

    // --- Blocked-clarify skeleton (feature 089 / FS-GG/FS.GG.SDD#174 §WD5) ------------------
    // The empty-state bodies the template renders. They are placeholders the operator (or a later
    // `clarify --input`) replaces — see `retirePlaceholders`, which removes one once its section
    // holds a real entry. `noBlockingAmbiguityRemains` is deliberately NOT in this set: it is a
    // meaningful sentinel that `isNoOutstandingSentinel` exempts from the blocking count.
    let noClarificationQuestions = "No clarification questions recorded."
    let noClarificationAnswers = "No clarification answers recorded."
    let noConcreteDecisions = "No concrete decisions recorded."
    let noAcceptedDeferrals = "No accepted deferrals recorded."
    let noBlockingAmbiguityRemains = "No blocking ambiguity remains."

    let emptyStatePlaceholders =
        [ noClarificationQuestions
          noClarificationAnswers
          noConcreteDecisions
          noAcceptedDeferrals ]

    /// An ambiguity is *resolved* only by a concrete decision or an accepted deferral. A
    /// `stillOpen` answer records that it is unresolved; it does not resolve it.
    let resolvesAmbiguity (answer: PlannedClarificationAnswer) =
        answer.Kind = "decision" || answer.Kind = "acceptedDeferral"

    /// The declared ambiguities that carry no resolution this run, in declaration order.
    let unresolvedAmbiguityValues (specFacts: SpecificationFacts) answers =
        let resolved =
            answers
            |> List.filter resolvesAmbiguity
            |> List.map (fun answer -> answer.AmbiguityId)
            |> Set.ofList

        specFacts.AmbiguityIds
        |> List.mapi (fun index ambiguity -> index, ambiguity.Value)
        |> List.filter (fun (_, value) -> not (resolved.Contains value))

    /// The Remaining Ambiguity line for an unresolved ambiguity. A `stillOpen` answer keeps its
    /// own text (today's `renderRemainingLine` output, so no existing golden moves); an ambiguity
    /// with no answer at all — the skeleton case — gets a generic explanation, because
    /// `SpecificationFacts` carries ambiguity *ids* only and never their prose (research D5).
    ///
    /// The wording is load-bearing. `parseRemainingAmbiguity` classifies a line by scanning it for
    /// `accepted deferral` / `defer` (⇒ acceptedDeferral) and `non-blocking` (⇒ nonBlocking), so an
    /// explanation that merely *names* those resolutions as options would parse as one of them and
    /// silently drop the ambiguity from `BlockingAmbiguityCount` — letting `checklist` pass with the
    /// ambiguity unanswered. Keep this text free of `defer` and `non-blocking`.
    let renderUnresolvedLine (answers: PlannedClarificationAnswer list) questionId ambiguityValue =
        answers
        |> List.tryFind (fun answer -> answer.AmbiguityId = ambiguityValue && answer.Kind = "stillOpen")
        |> Option.bind renderRemainingLine
        |> Option.defaultValue
            $"- {ambiguityValue} [{questionId}] blocking: Unanswered. Resolve source ambiguity {ambiguityValue} before checklist."

    let clarificationTemplate request workId (specFacts: SpecificationFacts) answers =
        let title = requestTitle request workId

        // Derived from the declared ambiguities that carry no resolution — NOT from the presence
        // of a `stillOpen` answer (089 §WD5). With zero answers the old rule produced
        // `status: clarified` and "No blocking ambiguity remains.", which is exactly the state a
        // blocked run is in: a file asserting the work is clarified while the command blocks.
        let unresolved = unresolvedAmbiguityValues specFacts answers

        let status =
            if List.isEmpty unresolved then
                "clarified"
            else
                "needsAnswers"

        let questionLines =
            specFacts.AmbiguityIds
            |> List.mapi (fun index ambiguity -> renderQuestionLine (scopedId "CQ" (index + 1)) ambiguity.Value)
            |> fun lines ->
                if List.isEmpty lines then
                    [ noClarificationQuestions ]
                else
                    lines

        let answerLines =
            if List.isEmpty answers then
                [ noClarificationAnswers ]
            else
                answers |> List.map renderAnswerLine

        let concreteDecisionLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "decision")
            |> List.choose renderDecisionLine
            |> fun lines ->
                if List.isEmpty lines then
                    [ noConcreteDecisions ]
                else
                    lines

        let deferralLines =
            answers
            |> List.filter (fun answer -> answer.Kind = "acceptedDeferral")
            |> List.choose renderDecisionLine
            |> fun lines ->
                if List.isEmpty lines then
                    [ noAcceptedDeferrals ]
                else
                    lines

        let remainingLines =
            unresolved
            |> List.map (fun (index, ambiguityValue) ->
                renderUnresolvedLine answers (scopedId "CQ" (index + 1)) ambiguityValue)
            |> fun lines ->
                if List.isEmpty lines then
                    [ noBlockingAmbiguityRemains ]
                else
                    lines

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
            let normalized =
                (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

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

    // Replace the body of an existing section (the lines between its heading and the next
    // `##` heading) with the supplied lines, preserving the heading and the blank-line
    // separators. If the section is absent it is appended. Used to purge and re-derive
    // machine-generated checklist sections on a stale re-run (§3.1).
    let replaceSectionBody heading (bodyLines: string list) (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let split = normalized.Split('\n') |> Array.toList
        let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

        match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
        | None ->
            let sectionBody = String.concat "\n" bodyLines
            $"{normalized.TrimEnd()}\n\n## {heading}\n{sectionBody}\n"
        | Some start ->
            let next =
                split
                |> List.mapi (fun index line -> index, line)
                |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                |> Option.map fst
                |> Option.defaultValue split.Length

            let before = split |> List.take (start + 1)
            let after = split |> List.skip next
            // Re-insert a single blank-line separator before the next heading (or trailing).
            (before @ bodyLines @ [ "" ] @ after) |> String.concat "\n"

    // --- Retirement passes (feature 089, FR-018 / FR-019) -----------------------------------
    // `appendClarificationAnswers` only ever *appends* to a section. That is fine while the tool
    // writes the whole artifact in one shot, but from 089 a blocked `clarify` seeds a skeleton
    // whose Remaining Ambiguity section lists every unanswered ambiguity as blocking, and whose
    // other sections carry empty-state placeholders. A later `clarify --input` that answers those
    // ambiguities must retire what it superseded — otherwise the recorded decision lands, the
    // blocking line survives, `clarify` reports `succeeded` with a non-zero blocking count, and
    // `checklist` blocks two stages later (research D4, verified against the pre-089 CLI).

    /// Rewrite the body of an existing section (the lines between its heading and the next `##`)
    /// through `transform`, which sees only the section's content lines (blanks stripped) and
    /// returns the lines to keep. Blank lines are passed through to `transform` and preserved, so a
    /// retirement pass removes exactly the lines it targets and reformats nothing else (FR-014); a
    /// single blank-line separator is normalized before the next heading. A section that is absent,
    /// or one whose transform keeps every line, is left byte-identical.
    let transformSectionBody heading (transform: string list -> string list) (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let split = normalized.Split('\n') |> Array.toList
        let headingPattern = $"^##\\s+{Regex.Escape heading}\\s*$"

        match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
        | None -> normalized
        | Some start ->
            let next =
                split
                |> List.mapi (fun index line -> index, line)
                |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                |> Option.map fst
                |> Option.defaultValue split.Length

            let before = split |> List.take (start + 1)
            let after = split |> List.skip next
            let body = split |> List.skip (start + 1) |> List.take (next - start - 1)

            // Blank lines ride through the transform untouched: a retirement pass drops the lines it
            // targets and nothing else. Stripping them here would reflow the operator's prose —
            // collapsing paragraph breaks, and blank lines inside a fenced block — on every pass
            // (FR-014), and would make an otherwise byte-identical re-run report a changed artifact.
            let kept = transform body

            let trimmed =
                kept |> List.rev |> List.skipWhile String.IsNullOrWhiteSpace |> List.rev

            // Restore the single blank-line separator only when a following heading needs one.
            let separator = if List.isEmpty after then [] else [ "" ]

            let rebuilt = if List.isEmpty trimmed then [] else trimmed @ separator

            if rebuilt = body then
                normalized
            else
                (before @ rebuilt @ after) |> String.concat "\n"

    let isSentinelLine (line: string) =
        String.Equals(line.Trim(), noBlockingAmbiguityRemains, StringComparison.OrdinalIgnoreCase)

    let isPlaceholderLine (line: string) =
        emptyStatePlaceholders
        |> List.exists (fun placeholder -> String.Equals(line.Trim(), placeholder, StringComparison.OrdinalIgnoreCase))

    /// The ambiguity a Remaining Ambiguity line is *about*: its first `AMB-###` id, mirroring
    /// `parseRemainingAmbiguity`, which classifies a line by `ambiguityIdsInLine |> List.tryHead`.
    /// A line may legitimately mention other ids in its prose ("blocked on the AMB-002 decision");
    /// only the anchor identifies the line's subject.
    let remainingLineAnchor (line: string) =
        idMatches @"\bAMB-\d{3,}\b" line |> List.tryHead

    /// Same, for a line that names only a question id — `parseRemainingAmbiguity` counts those as
    /// blocking too, so retirement must be able to reach them.
    let remainingLineQuestionAnchor (line: string) =
        idMatches @"\bCQ-\d{3,}\b" line |> List.tryHead

    /// FR-018. Drop each Remaining Ambiguity line whose subject now carries a concrete decision or
    /// an accepted deferral, and restore the sentinel when nothing unresolved is left. Lines for
    /// still-unresolved ambiguities — including operator-authored prose — are untouched.
    let retireResolvedRemaining (answers: PlannedClarificationAnswer list) (text: string) =
        let resolvedAnswers = answers |> List.filter resolvesAmbiguity

        let resolvedAmbiguities =
            resolvedAnswers
            |> List.map (fun answer -> answer.AmbiguityId.ToUpperInvariant())
            |> Set.ofList

        let resolvedQuestions =
            resolvedAnswers
            |> List.map (fun answer -> answer.QuestionId.ToUpperInvariant())
            |> Set.ofList

        if Set.isEmpty resolvedAmbiguities then
            text
        else
            text
            |> transformSectionBody "Remaining Ambiguity" (fun content ->
                let isResolved line =
                    // `idMatches` already upper-cases what it returns, matching the sets above.
                    // Match the line's SUBJECT, never any id it merely mentions — otherwise an
                    // operator's "AMB-001 blocked on the AMB-002 decision" is deleted the moment
                    // AMB-002 is answered, destroying the explanation of a still-blocking item.
                    match remainingLineAnchor line with
                    | Some ambiguity -> resolvedAmbiguities.Contains ambiguity
                    | None ->
                        // No ambiguity id: a question-id-only line still blocks, so retire it when
                        // its question was answered.
                        match remainingLineQuestionAnchor line with
                        | Some question -> resolvedQuestions.Contains question
                        | None -> false

                let survivors =
                    content
                    |> List.filter (fun line ->
                        String.IsNullOrWhiteSpace line
                        || (not (isSentinelLine line) && not (isResolved line)))

                // "Empty" means no CONTENT line survived; blank lines carried through do not count.
                if survivors |> List.forall String.IsNullOrWhiteSpace then
                    [ noBlockingAmbiguityRemains ]
                else
                    survivors)

    /// FR-019. Once a section holds a real entry, retire its empty-state placeholder. The
    /// Remaining Ambiguity sentinel is governed by `retireResolvedRemaining`, not by this pass.
    let retirePlaceholders (text: string) =
        let drop content =
            let isRealEntry line =
                not (String.IsNullOrWhiteSpace line) && not (isPlaceholderLine line)

            if content |> List.exists isRealEntry then
                content |> List.filter (isPlaceholderLine >> not)
            else
                content

        [ "Clarification Questions"; "Answers"; "Decisions"; "Accepted Deferrals" ]
        |> List.fold (fun acc heading -> transformSectionBody heading drop acc) text

    /// A real Remaining Ambiguity line and the "nothing remains" sentinel cannot both be true.
    let retireStaleSentinel (text: string) =
        text
        |> transformSectionBody "Remaining Ambiguity" (fun content ->
            let isRealEntry line =
                not (String.IsNullOrWhiteSpace line) && not (isSentinelLine line)

            if content |> List.exists isRealEntry then
                content |> List.filter (isSentinelLine >> not)
            else
                content)

    /// FR-020. Once nothing blocks, the front matter must not still say `needsAnswers` — the
    /// skeleton this command seeded says so, and leaving it stale reproduces the very
    /// self-contradiction FR-007/FR-008 exist to prevent.
    ///
    /// Scoped deliberately: rewrite ONLY the exact value this tool writes (`needsAnswers`), and
    /// only inside the leading front-matter block. An operator who chose some other status keeps
    /// it (FR-014) — the command corrects its own bookkeeping, it does not overwrite authorship.
    let retireStaleStatus (text: string) =
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let split = normalized.Split('\n') |> Array.toList

        let remainingResolved =
            let content =
                let headingPattern = @"^##\s+Remaining Ambiguity\s*$"

                match split |> List.tryFindIndex (fun line -> Regex.IsMatch(line, headingPattern)) with
                | None -> []
                | Some start ->
                    let next =
                        split
                        |> List.mapi (fun index line -> index, line)
                        |> List.tryFind (fun (index, line) -> index > start && Regex.IsMatch(line, "^##\\s+"))
                        |> Option.map fst
                        |> Option.defaultValue split.Length

                    split
                    |> List.skip (start + 1)
                    |> List.take (next - start - 1)
                    |> List.filter (String.IsNullOrWhiteSpace >> not)

            // Non-empty AND all sentinel. `List.forall` is vacuously true on an empty list, so an
            // absent or hand-emptied Remaining Ambiguity section would otherwise be read as proof
            // that everything was resolved. Absence of evidence is not evidence of resolution.
            not (List.isEmpty content) && content |> List.forall isSentinelLine

        // The front matter is the leading `---` … `---` block; never rewrite a `status:` in the body.
        let frontMatterEnd =
            match split with
            | first :: _ when first.Trim() = "---" ->
                split
                |> List.mapi (fun index line -> index, line)
                |> List.tryFind (fun (index, line) -> index > 0 && line.Trim() = "---")
                |> Option.map fst
            | _ -> None

        match frontMatterEnd with
        | Some fenceIndex when remainingResolved ->
            split
            |> List.mapi (fun index line ->
                if index < fenceIndex && Regex.IsMatch(line, @"^status:\s*needsAnswers\s*$") then
                    "status: clarified"
                else
                    line)
            |> String.concat "\n"
        | _ -> normalized

    let appendClarificationAnswers (existingText: string) (answers: PlannedClarificationAnswer list) =
        let questionLines =
            answers
            |> List.filter (fun answer ->
                not (Regex.IsMatch(existingText, $@"\b{Regex.Escape answer.QuestionId}\b", RegexOptions.IgnoreCase)))
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

        let appended =
            existingText
            |> appendToSection "Clarification Questions" questionLines
            |> appendToSection "Answers" answerLines
            |> appendToSection "Decisions" decisionLines
            |> appendToSection "Accepted Deferrals" deferralLines
            // Append first, THEN retire the sentinel. Retiring first is a no-op — the section holds
            // only the sentinel at that point, so there is no non-sentinel line to trigger the drop —
            // and the new blocking line lands beside "No blocking ambiguity remains."
            |> (if List.isEmpty remainingLines then
                    id
                else
                    appendToSection "Remaining Ambiguity" remainingLines >> retireStaleSentinel)

        // Retire what these answers superseded, so the artifact never contradicts itself
        // (FR-018/FR-019/FR-020). Order matters: `retireResolvedRemaining` drops the resolved lines
        // and restores the sentinel in one pass, and `retireStaleStatus` reads that settled section.
        appended
        |> retireResolvedRemaining answers
        |> retirePlaceholders
        |> retireStaleStatus

    let clarificationDiagnosticsTextAndSummary request workId specFacts model =
        let path = clarificationPath workId

        match snapshot path model with
        | None ->
            let answers, answerDiagnostics =
                plannedClarificationAnswers path request specFacts None

            // The seed text a blocked run writes (089 FR-006). Threaded on its OWN channel, kept
            // separate from the `text` result that feeds `generatedViewPlan` — passing the skeleton
            // there would change a blocked run's reported GeneratedViewState (research D8). Seeded
            // only from this branch, i.e. only when no `clarifications.md` exists, which is what
            // makes "never clobber an existing artifact" true by construction (FR-011/FR-014).
            let seedTextIfParses text =
                match parseClarificationForCommand path text with
                | Error _ -> None // never seed a skeleton that would not parse (FR-009)
                | Ok _ -> Some text

            if
                answerDiagnostics
                |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            then
                // Blocked on unanswered ambiguities: diagnostics, outcome, and exit code are
                // unchanged (FR-010); the only delta is that the skeleton now lands (§WD5).
                let skeleton = clarificationTemplate request workId specFacts answers

                answerDiagnostics |> DiagnosticsModule.sort, None, None, seedTextIfParses skeleton
            else
                let text = clarificationTemplate request workId specFacts answers

                match parseClarificationForCommand path text with
                | Error diagnostics -> diagnostics, Some text, None, None
                | Ok(facts, diagnostics) ->
                    let unresolved =
                        if facts.BlockingAmbiguityCount > 0 then
                            [ unresolvedBlockingAmbiguity
                                  path
                                  (facts.RemainingAmbiguity
                                   |> List.choose (fun item -> item.AmbiguityId |> Option.map _.Value)) ]
                        else
                            []

                    // A `stillOpen` answer blocks too, and today writes nothing. Seed it as well:
                    // same file-absent state, same operator need.
                    let seed = if List.isEmpty unresolved then None else Some text

                    diagnostics @ unresolved |> DiagnosticsModule.sort,
                    Some text,
                    Some(clarificationSummary facts),
                    seed
        // An artifact already exists: it is the operator's. Every arm below returns `None` for the
        // seed channel, so no skeleton can ever overwrite it (FR-011/FR-014).
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None, None
            else
                match parseClarificationForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        frontMatterIdentityDiagnostics
                            "Clarification"
                            LifecycleStage.Clarify
                            "clarify"
                            malformedClarificationFrontMatter
                            clarificationIdentityMismatch
                            malformedClarificationFrontMatter
                            path
                            workId
                            existingFacts.FrontMatter.SchemaVersion.Major
                            existingFacts.FrontMatter.WorkId.Value
                            existingFacts.FrontMatter.Stage
                        @ [ if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceSpec,
                                        specPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedClarificationFrontMatter
                                    path
                                    $"Clarification sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'." ]

                    let ensuredText =
                        if List.isEmpty identityDiagnostics then
                            ensureClarificationSections workId existing.Text
                        else
                            existing.Text

                    let existingFactsForAnswers =
                        match parseClarificationForCommand path ensuredText with
                        | Ok(facts, _) -> Some facts
                        | Error _ -> Some existingFacts

                    let answers, answerDiagnostics =
                        plannedClarificationAnswers path request specFacts existingFactsForAnswers

                    let proposedText =
                        if List.isEmpty identityDiagnostics && List.isEmpty answerDiagnostics then
                            appendClarificationAnswers ensuredText answers
                        else
                            existing.Text

                    let parsedProposed = parseClarificationForCommand path proposedText

                    match parsedProposed with
                    | Error diagnostics ->
                        identityDiagnostics @ diagnostics @ answerDiagnostics |> DiagnosticsModule.sort,
                        Some proposedText,
                        None,
                        None
                    | Ok(facts, proposedDiagnostics) ->
                        let diagnostics =
                            identityDiagnostics @ proposedDiagnostics @ answerDiagnostics
                            |> DiagnosticsModule.sort

                        diagnostics, Some proposedText, Some(clarificationSummary facts), None

    let clarificationPrerequisiteDiagnosticsTextSummaryAndFacts workId model =
        let path = clarificationPath workId

        match snapshot path model with
        | None ->
            [ missingClarificationPrerequisite path $"Clarification prerequisite '{path}' is missing." ],
            None,
            None,
            None
        | Some existing ->
            match parseClarificationForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedClarificationFrontMatter path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Clarification"
                        LifecycleStage.Clarify
                        "clarify"
                        malformedClarificationFrontMatter
                        clarificationIdentityMismatch
                        missingClarificationPrerequisite
                        path
                        workId
                        facts.FrontMatter.SchemaVersion.Major
                        facts.FrontMatter.WorkId.Value
                        facts.FrontMatter.Stage
                    @ [ if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceSpec,
                                    specPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedClarificationFrontMatter
                                path
                                $"Clarification sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'." ]

                let blocking =
                    if facts.BlockingAmbiguityCount > 0 then
                        [ unresolvedBlockingAmbiguity
                              path
                              (facts.RemainingAmbiguity
                               |> List.choose (fun item -> item.AmbiguityId |> Option.map _.Value)) ]
                    else
                        []

                let allDiagnostics =
                    identityDiagnostics @ diagnostics @ blocking |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(clarificationSummary facts), Some facts
