namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module Clarification =
    type ClarificationDecisionKind =
        | ConcreteDecision
        | AcceptedDeferral

    type ClarificationAnswerKind =
        | DecisionAnswer
        | AcceptedDeferralAnswer
        | StillOpenAnswer
        | NoteAnswer

    type ClarificationFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          PublicOrToolFacingImpact: bool option }

    type ClarificationQuestion =
        { QuestionId: ClarificationQuestionId
          Prompt: string
          SourceAmbiguityIds: AmbiguityId list
          RelatedRequirementIds: RequirementId list
          RelatedStoryIds: UserStoryId list
          RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
          Blocking: bool
          State: string
          SourceLocation: SourceLocation option }

    type ClarificationAnswer =
        { QuestionId: ClarificationQuestionId option
          AmbiguityIds: AmbiguityId list
          Text: string
          Kind: ClarificationAnswerKind
          SourceLocation: SourceLocation option }

    type ClarificationDecisionFact =
        { DecisionId: DecisionId
          Title: string
          Kind: ClarificationDecisionKind
          Text: string
          Rationale: string option
          SourceQuestionIds: ClarificationQuestionId list
          SourceAmbiguityIds: AmbiguityId list
          RelatedRequirementIds: RequirementId list
          RelatedStoryIds: UserStoryId list
          RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
          SourceLocation: SourceLocation option }

    type RemainingAmbiguity =
        { AmbiguityId: AmbiguityId option
          QuestionId: ClarificationQuestionId option
          State: string
          Explanation: string
          RequiredCorrection: string
          SourceLocation: SourceLocation option }

    type ClarificationFacts =
        { FrontMatter: ClarificationFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          Questions: ClarificationQuestion list
          Answers: ClarificationAnswer list
          Decisions: ClarificationDecisionFact list
          AcceptedDeferrals: ClarificationDecisionFact list
          RemainingAmbiguity: RemainingAmbiguity list
          BlockingAmbiguityCount: int
          Diagnostics: Diagnostic list }

    let clarificationStandardSections () =
        [ "Source Specification"
          "Clarification Questions"
          "Answers"
          "Decisions"
          "Accepted Deferrals"
          "Remaining Ambiguity"
          "Lifecycle Notes" ]

    let parseClarificationFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Clarifications

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Clarification artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Clarification front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)
                let sourceSpec = tryScalarAt [ "sourceSpec" ] root

                match version, workId, stage, sourceSpec, versionDiagnostics with
                | Some schema, Some workId, Some stage, Some sourceSpec, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "needsAnswers"
                           SourceSpec = sourceSpec
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent
                                 artifact
                                 "Clarification front matter is incomplete."
                                 "Add schemaVersion, workId, title, stage: clarify, changeTier, status, and sourceSpec to clarifications.md."
                                 [] ])

    let questionIdsInLine line =
        Regex.Matches(line, @"\bCQ-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createClarificationQuestionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let ambiguityIdsInLine line =
        Regex.Matches(line, @"\bAMB-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createAmbiguityId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let requirementIdsInLine line =
        Regex.Matches(line, @"\bFR-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createRequirementId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let storyIdsInLine line =
        Regex.Matches(line, @"\bUS-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createUserStoryId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let acceptanceScenarioIdsInLine line =
        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createAcceptanceScenarioId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let decisionIdsInLine line =
        Regex.Matches(line, @"\bDEC-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createDecisionId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let cleanDecisionText text =
        Regex.Replace(text, @"^(?:\[[^\]]+\]\s*)+:\s*", "", RegexOptions.CultureInvariant).Trim()

    let parseClarificationQuestions text =
        sectionLines "Clarification Questions" text
        |> List.choose (fun (lineNumber, line) ->
            match questionIdsInLine line |> List.tryHead with
            | Some questionId ->
                let lowered = line.ToLowerInvariant()

                Some
                    { QuestionId = questionId
                      Prompt = cleanAfterId questionId.Value line
                      SourceAmbiguityIds = ambiguityIdsInLine line
                      RelatedRequirementIds = requirementIdsInLine line
                      RelatedStoryIds = storyIdsInLine line
                      RelatedAcceptanceScenarioIds = acceptanceScenarioIdsInLine line
                      Blocking = not (Regex.IsMatch(lowered, @"\bnon-?blocking\b"))
                      State =
                        if containsWord "answered" lowered then "answered"
                        elif containsWord "deferred" lowered then "deferred"
                        else "open"
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let answerKind (line: string) =
        let lowered = line.ToLowerInvariant()

        if containsWord "accepted deferral" lowered || Regex.IsMatch(lowered, @"\bdefer") then
            AcceptedDeferralAnswer
        elif containsWord "still open" lowered || containsWord "unresolved" lowered then
            StillOpenAnswer
        elif containsWord "note" lowered then
            NoteAnswer
        else
            DecisionAnswer

    let parseClarificationAnswers text =
        sectionLines "Answers" text
        |> List.choose (fun (lineNumber, line) ->
            let question = questionIdsInLine line |> List.tryHead
            let ambiguities = ambiguityIdsInLine line

            if Option.isNone question && List.isEmpty ambiguities then
                None
            else
                Some
                    { QuestionId = question
                      AmbiguityIds = ambiguities
                      Text = line.Trim().TrimStart('-', '*').Trim()
                      Kind = answerKind line
                      SourceLocation = sourceLocation lineNumber })

    let parseClarificationDecisionsInSection heading kind text =
        sectionLines heading text
        |> List.choose (fun (lineNumber, line) ->
            match decisionIdsInLine line |> List.tryHead with
            | Some decisionId ->
                let decisionText = cleanAfterId decisionId.Value line |> cleanDecisionText

                Some
                    { DecisionId = decisionId
                      Title = decisionText
                      Kind = kind
                      Text = decisionText
                      Rationale = None
                      SourceQuestionIds = questionIdsInLine line
                      SourceAmbiguityIds = ambiguityIdsInLine line
                      RelatedRequirementIds = requirementIdsInLine line
                      RelatedStoryIds = storyIdsInLine line
                      RelatedAcceptanceScenarioIds = acceptanceScenarioIdsInLine line
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseRemainingAmbiguity text =
        sectionLines "Remaining Ambiguity" text
        |> List.choose (fun (lineNumber, line) ->
            let ambiguity = ambiguityIdsInLine line |> List.tryHead
            let question = questionIdsInLine line |> List.tryHead

            if Option.isNone ambiguity && Option.isNone question then
                None
            else
                let lowered = line.ToLowerInvariant()
                let state =
                    if lowered.Contains("accepted deferral") || lowered.Contains("deferred") then "acceptedDeferral"
                    elif lowered.Contains("non-blocking") then "nonBlocking"
                    else "blocking"

                Some
                    { AmbiguityId = ambiguity
                      QuestionId = question
                      State = state
                      Explanation = line.Trim().TrimStart('-', '*').Trim()
                      RequiredCorrection =
                        if state = "blocking" then "Provide a concrete decision, accepted deferral, or mark the ambiguity non-blocking."
                        else "Keep the ambiguity visible to later lifecycle stages."
                      SourceLocation = sourceLocation lineNumber })

    let parseClarificationFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Clarifications

        match parseClarificationFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text = (if String.IsNullOrEmpty snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = clarificationStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let questions = parseClarificationQuestions text
            let answers = parseClarificationAnswers text
            let decisions = parseClarificationDecisionsInSection "Decisions" ConcreteDecision text
            let deferrals = parseClarificationDecisionsInSection "Accepted Deferrals" AcceptedDeferral text
            let remaining = parseRemainingAmbiguity text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: ClarificationQuestionId) -> id.Value) (questions |> List.map (fun q -> q.QuestionId, q.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: DecisionId) -> id.Value) ((decisions @ deferrals) |> List.map (fun decision -> decision.DecisionId, decision.SourceLocation))
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Clarification artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to clarifications.md before relying on the parsed facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  Questions = questions |> List.sortBy (fun question -> question.QuestionId.Value)
                  Answers = answers
                  Decisions = decisions |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  AcceptedDeferrals = deferrals |> List.sortBy (fun decision -> decision.DecisionId.Value)
                  RemainingAmbiguity = remaining
                  BlockingAmbiguityCount = remaining |> List.filter (fun item -> item.State = "blocking") |> List.length
                  Diagnostics = diagnostics }
