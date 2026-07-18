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

module internal SpecifyAuthoring =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

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

    let specificationTemplate request workId changeTier intent =
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
title: {yamlFrontMatterScalar title}
stage: specify
changeTier: {changeTier}
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
        ensureSections MergePolicies.specification specificationSectionText text

    let specificationSummary (facts: SpecificationFacts) : SpecificationSummary =
        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          StoryIds = facts.UserStoryIds |> List.map _.Value |> List.sort
          RequirementIds = facts.RequirementIds |> List.map _.Value |> List.sort
          AcceptanceScenarioIds = facts.AcceptanceScenarioIds |> List.map _.Value |> List.sort
          AmbiguityIds = facts.AmbiguityIds |> List.map _.Value |> List.sort }

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
                let text =
                    specificationTemplate request workId (charteredChangeTier workId model) intent

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
