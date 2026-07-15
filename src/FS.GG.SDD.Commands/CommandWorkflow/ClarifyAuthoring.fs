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

module internal ClarifyAuthoring =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers

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
        ensureSections MergePolicies.clarifications (clarificationSectionText workId) text

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

    let answerKindValue (line: string) =
        let lowered = line.ToLowerInvariant()

        // Author `--input` is freeform, so the answer's kind is inferred from its words.
        // Two rules keep that inference honest: a state word counts only as a whole
        // word/phrase (so `still open` is not matched inside `distill opens`), mirroring
        // the Artifacts `answerKind` parser's word-boundary discipline; and a word that
        // is directly negated names the state only to reject it (`cannot defer`,
        // `no longer still open`), so it does not select that state. The negation guard
        // is extra here because this reads freeform author prose, not an already-labelled
        // artifact line. `defer` is matched as a stem (defer/deferral/deferred).
        let negators = @"(?:not|no longer|cannot|can'?t|can not|never|won'?t|will not)"

        let says (pattern: string) =
            Regex.IsMatch(lowered, @"\b" + pattern, RegexOptions.IgnoreCase)
            && not (Regex.IsMatch(lowered, @"\b" + negators + @"\s+" + pattern, RegexOptions.IgnoreCase))

        if says @"accepted deferral\b" || says "defer" then
            "acceptedDeferral"
        elif says @"still open\b" || says @"unresolved\b" then
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
        // The clarifications file is *about* a specific spec — its own `sourceSpec:` line says so —
        // so it inherits that spec's title rather than re-deriving one from the work id (#164). An
        // explicit `--title` still wins; a blank spec title still falls back to the humanized id.
        // Feature 089's blocked-seed path made this load-bearing: it emits a skeleton on a run where
        // the author has no reason to pass `--title` at all.
        let title = titleFromSpec request specFacts workId

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
title: {yamlFrontMatterScalar title}
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

    // --- Retirement passes (feature 089, FR-018 / FR-019) -----------------------------------
    // `appendClarificationAnswers` only ever *appends* to a section. That is fine while the tool
    // writes the whole artifact in one shot, but from 089 a blocked `clarify` seeds a skeleton
    // whose Remaining Ambiguity section lists every unanswered ambiguity as blocking, and whose
    // other sections carry empty-state placeholders. A later `clarify --input` that answers those
    // ambiguities must retire what it superseded — otherwise the recorded decision lands, the
    // blocking line survives, `clarify` reports `succeeded` with a non-zero blocking count, and
    // `checklist` blocks two stages later (research D4, verified against the pre-089 CLI).

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

        let bodies =
            Map
                [ "Clarification Questions", questionLines
                  "Answers", answerLines
                  "Decisions", decisionLines
                  "Accepted Deferrals", deferralLines ]

        // `Remaining Ambiguity` is the one appended section with a conditional pass of its own, so it
        // is appended below rather than in the fold. The rest come from the policy, in policy order.
        let unconditional =
            MergePolicy.appendedSections MergePolicies.clarifications
            |> List.filter (fun heading -> heading <> "Remaining Ambiguity")

        let appended =
            existingText
            |> appendToSections bodies unconditional
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
