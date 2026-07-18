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

module internal ChecklistAuthoring =
    // Pure `Path` string ops only — the effectful `File`/`Directory` surface stays at the
    // `CommandEffects` edge and is deliberately kept out of scope in the MVU pure core.
    type private Path = System.IO.Path

    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

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
        ensureSections MergePolicies.checklist (checklistSectionText workId) text

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
          AcceptedDeferralCount =
            results
            |> List.filter (fun result -> result.Status = "acceptedDeferral")
            |> List.length
          StaleResultCount = facts.StaleResultCount
          AdvisoryCount = results |> List.filter (fun result -> result.Status = "advisory") |> List.length }

    let mapChecklistDiagnostics (path: string) (diagnostics: Diagnostic list) =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateChecklistId path id
            | "unknownReference", id :: _ -> unknownChecklistSourceReference path id
            | "missingChecklistBackReference", id :: _ -> missingChecklistBackReference path id
            | "missingChecklistBackReference", [] -> missingChecklistBackReference path diagnostic.Message
            | "workModelInconsistent", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "malformedSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "unsupportedSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | "futureSchemaVersion", _ -> malformedChecklistFrontMatter path diagnostic.Message
            | _ -> diagnostic)

    let parseChecklistForCommand path text : Result<ChecklistFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match parseChecklistFacts snapshot with
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

    // §3.1 (082): reviews are always derived fresh from the current sources — the tool never
    // seeds derivation with prior CHK/CR rows, so there is no `existingFacts` dedup. Every
    // requirement/deferral gets a re-derived verdict on every run (#146).
    let plannedChecklistReviews
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (visualSurface: bool)
        =
        let mutable nextItem = nextScopedIndex "CHK" ""
        let mutable nextResult = nextScopedIndex "CR" ""

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
            |> List.map (fun requirement ->
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
                         Some
                             $"Add a coverage line for {requirement.Value}: \"- {requirement.Value}: <text> (covers AC-###)\" on a single list item — a bold \"**{requirement.Value}**\" or a colon-less line is not recognized.")
                    true)

        let deferralReviews =
            clarificationFacts.AcceptedDeferrals
            |> List.map (fun decision ->
                allocate
                    [ decision.DecisionId.Value ]
                    "acceptedDeferral"
                    $"Accepted deferral {decision.DecisionId.Value} remains visible to planning."
                    None
                    false)

        // #306: the requirement reviews above each check ONE requirement. A spec incoherence that
        // exists only as pixels lives in the conjunction of two — no single requirement references
        // it, so no per-requirement review can reach it. Name the class, once, over the whole
        // requirement set.
        //
        // ADVISORY, never blocking. Reviews are re-derived from source on every run and prior CHK/CR
        // rows are never re-ingested (082, #146), so a blocking row an author reviewed and passed
        // would reappear on the next run and dead-end the lifecycle. This row prompts; the blocking
        // gate is the visual-inspection obligation, two stages later at `evidence`.
        let incoherenceReviews =
            let requirementIds = specFacts.RequirementIds |> List.map _.Value

            if not visualSurface || List.isEmpty requirementIds then
                []
            else
                [ allocate
                      requirementIds
                      "advisory"
                      "Requirements are reviewed for incoherence that exists only BETWEEN them, which no single-requirement review can reach: draw order versus geometry, overlapping bands, and z-order versus collision bounds."
                      (Some
                          "Render one representative frame, look at it, and declare the rendered artifact against this work item's visual-inspection obligation.")
                      false ]

        requirementReviews @ deferralReviews @ incoherenceReviews

    let renderChecklistItemLine review =
        let source = review.SourceIds |> List.map (fun id -> $"[{id}]") |> String.concat " "
        let kind = if review.Blocking then "blocking" else "advisory"
        $"- {review.ItemId} {source} {kind}: {review.Text}".Replace("  ", " ")

    let renderChecklistResultLine review =
        let source = review.SourceIds |> List.map (fun id -> $"[{id}]") |> String.concat " "

        let correction =
            review.Correction
            |> Option.map (fun text -> $" Correction: {text}")
            |> Option.defaultValue ""

        $"- {review.ResultId} [CHK:{review.ItemId}] {source} {review.Status}: {review.Text}{correction}"
            .Replace("  ", " ")

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

        let status =
            if List.isEmpty failures then
                "checklistReady"
            else
                "needsCorrection"

        let itemLines = reviews |> List.map renderChecklistItemLine

        let resultLines =
            reviews
            |> List.filter (fun review -> review.Status <> "acceptedDeferral")
            |> List.map renderChecklistResultLine

        let deferralLines =
            reviews
            |> List.filter (fun review -> review.Status = "acceptedDeferral")
            |> List.map renderChecklistDeferralLine

        let findingLines = failures |> List.map renderBlockingFindingLine

        let itemText =
            if List.isEmpty itemLines then
                "No checklist items recorded."
            else
                String.concat "\n" itemLines

        let resultText =
            if List.isEmpty resultLines then
                "No review results recorded."
            else
                String.concat "\n" resultLines

        let deferralText =
            if List.isEmpty deferralLines then
                "No accepted checklist deferrals recorded."
            else
                String.concat "\n" deferralLines

        let findingText =
            if List.isEmpty findingLines then
                "No blocking findings recorded."
            else
                String.concat "\n" findingLines

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

    let knownChecklistSourceIds
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts option)
        =
        [ specFacts.RequirementIds |> List.map _.Value
          specFacts.UserStoryIds |> List.map _.Value
          specFacts.AcceptanceScenarioIds |> List.map _.Value
          specFacts.ScopeBoundaryIds |> List.map _.Value
          specFacts.AmbiguityIds |> List.map _.Value
          clarificationFacts.Questions
          |> List.map (fun question -> question.QuestionId.Value)
          clarificationFacts.Decisions
          |> List.map (fun decision -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals
          |> List.map (fun decision -> decision.DecisionId.Value)
          checklistFacts
          |> Option.map (fun facts -> facts.Items |> List.map (fun item -> item.ItemId.Value))
          |> Option.defaultValue [] ]
        |> List.concat
        |> Set.ofList

    let unknownChecklistReferences
        (path: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts option)
        =
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

    // §3.1 (082): re-derive every machine-derived section from current sources, rewriting the
    // `## Source Snapshot` digests. Authored, non-derived sections are preserved by
    // `ensureChecklistSections` (the caller passes the ensured text). No prior tool-injected
    // row survives — a verdict exists iff current sources justify it (#146, SC-001). This runs
    // on every re-run, not only when the source snapshot changed.
    let rederiveChecklist
        workId
        (specText: string)
        (clarificationText: string)
        (reviews: PlannedChecklistReview list)
        (ensuredText: string)
        =
        let placeholder text lines =
            if List.isEmpty lines then [ text ] else lines

        let itemLines = reviews |> List.map renderChecklistItemLine

        let resultLines =
            reviews
            |> List.filter (fun review -> review.Status <> "acceptedDeferral")
            |> List.map renderChecklistResultLine

        let deferralLines =
            reviews
            |> List.filter (fun review -> review.Status = "acceptedDeferral")
            |> List.map renderChecklistDeferralLine

        let findingLines =
            reviews
            |> List.filter (fun review -> review.Status = "fail")
            |> List.map renderBlockingFindingLine

        let snapshotLines =
            [ sourceSnapshotLine "spec" (specPath workId) specText
              sourceSnapshotLine "clarifications" (clarificationPath workId) clarificationText ]

        let bodies =
            Map
                [ "Source Snapshot", snapshotLines
                  "Checklist Items", placeholder "No checklist items recorded." itemLines
                  "Review Results", placeholder "No review results recorded." resultLines
                  "Accepted Deferrals", placeholder "No accepted checklist deferrals recorded." deferralLines
                  "Blocking Findings", placeholder "No blocking findings recorded." findingLines ]

        ensuredText
        |> replaceSectionBodies bodies (MergePolicy.rederivedSections MergePolicies.checklist)

    let checklistQualityDiagnostics (path: string) (reviews: PlannedChecklistReview list) =
        reviews
        |> List.filter (fun review -> review.Status = "fail")
        |> List.map (fun review ->
            failedRequirementsQuality
                path
                review.Text
                (review.Correction
                 |> Option.defaultValue "Correct the source requirement or checklist review before planning.")
                (review.SourceIds @ [ review.ItemId; review.ResultId ]
                 |> List.distinct
                 |> List.sort))

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

        // #306: `checklistReadEffects` already requests `.fsgg/project.yml`; the visual-surface
        // declaration rides that read. No new I/O edge, and an absent/malformed config declares none.
        let visualSurface =
            snapshot ".fsgg/project.yml" model
            |> Option.bind (fun projectSnapshot ->
                match parseProjectConfig projectSnapshot with
                | Ok config -> Some config
                | Error _ -> None)
            |> Option.map _.VisualSurface
            |> Option.defaultValue false

        let baseReviews = plannedChecklistReviews specFacts clarificationFacts visualSurface

        match snapshot path model with
        | None ->
            let text =
                checklistTemplate request workId specText clarificationText specFacts clarificationFacts baseReviews

            match parseChecklistForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let qualityDiagnostics = checklistQualityDiagnostics (specPath workId) baseReviews
                diagnostics @ qualityDiagnostics |> DiagnosticsModule.sort, Some text, Some(checklistSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            else
                // FS.GG.SDD#572. Scaffold any missing machine-derived section HEADING before the
                // blocking parse, rather than blocking on it: `checklist` re-derives those sections'
                // CONTENT wholesale on every run and already carries `ensureChecklistSections` for
                // exactly that — so a missing `## Accepted Deferrals` heading (etc.) it is about to
                // populate must not be a `workModelInconsistent` block. Ensuring is idempotent and
                // touches neither front matter nor authored content, so every real diagnostic
                // (identity, duplicate ids, unknown refs) still fires; only the missing-heading
                // blocker dissolves. Downstream stages keep the strict parse (they consume, not
                // regenerate), so a hand-mangled checklist reaching `plan` still blocks there.
                let ensuredText = ensureChecklistSections workId existing.Text

                match parseChecklistForCommand path ensuredText with
                | Error diagnostics -> diagnostics, Some ensuredText, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        frontMatterIdentityDiagnostics
                            "Checklist"
                            LifecycleStage.Checklist
                            "checklist"
                            malformedChecklistFrontMatter
                            checklistIdentityMismatch
                            malformedChecklistFrontMatter
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
                                malformedChecklistFrontMatter
                                    path
                                    $"Checklist sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceClarifications,
                                        clarificationPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedChecklistFrontMatter
                                    path
                                    $"Checklist sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'." ]

                    let unknownDiagnostics =
                        unknownChecklistReferences path specFacts clarificationFacts (Some existingFacts)

                    let blockingParserDiagnostics =
                        identityDiagnostics @ existingDiagnostics @ unknownDiagnostics
                        |> DiagnosticsModule.sort

                    let hasBlockingParserDiagnostics =
                        blockingParserDiagnostics
                        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        blockingParserDiagnostics, Some existing.Text, None
                    else
                        // `ensuredText` was scaffolded above (FS.GG.SDD#572), so the missing-heading
                        // block can no longer reach here; the re-derive works from it directly.

                        // §3.1 (082, #146): re-derive the machine-derived sections from the
                        // current sources on EVERY run — never re-ingest prior CHK/CR rows as
                        // authored input. A verdict exists iff the current sources justify it,
                        // so an orphaned tool-injected row is reclaimed, not preserved (FR-002,
                        // FR-003). Authored sections are preserved by `ensureChecklistSections`
                        // and the Source Snapshot is refreshed; unchanged sources re-derive to
                        // identical bytes → noChange (FR-008).
                        let reviews = plannedChecklistReviews specFacts clarificationFacts visualSurface

                        let proposedText =
                            rederiveChecklist workId specText clarificationText reviews ensuredText

                        match parseChecklistForCommand path proposedText with
                        | Error diagnostics -> diagnostics, Some proposedText, None
                        | Ok(proposedFacts, proposedDiagnostics) ->
                            let qualityDiagnostics = checklistQualityDiagnostics (specPath workId) reviews

                            blockingParserDiagnostics @ proposedDiagnostics @ qualityDiagnostics
                            |> DiagnosticsModule.sort,
                            Some proposedText,
                            Some(checklistSummary proposedFacts)

    let checklistPrerequisiteDiagnosticsTextSummaryAndFacts
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        model
        =
        let path = checklistPath workId

        match snapshot path model with
        | None -> [ missingChecklistPrerequisite path $"Checklist prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseChecklistForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedChecklistFrontMatter path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Checklist"
                        LifecycleStage.Checklist
                        "checklist"
                        malformedChecklistFrontMatter
                        checklistIdentityMismatch
                        missingChecklistPrerequisite
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
                            malformedChecklistFrontMatter
                                path
                                $"Checklist sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceClarifications,
                                    clarificationPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedChecklistFrontMatter
                                path
                                $"Checklist sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'." ]

                let unknownDiagnostics =
                    unknownChecklistReferences path specFacts clarificationFacts (Some facts)

                let readinessDiagnostics =
                    [ if
                          not (
                              String.Equals(
                                  facts.FrontMatter.Status,
                                  "checklistReady",
                                  StringComparison.OrdinalIgnoreCase
                              )
                          )
                      then
                          failedChecklistPrerequisite
                              path
                              $"Checklist status '{facts.FrontMatter.Status}' is not checklistReady."
                              [ facts.FrontMatter.Status ]

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

                      // `BlockingFindings` is already sentinel-free (the parser drops
                      // no-outstanding disclaimers); filtering `StartsWith "No "` here would
                      // wrongly re-drop a genuine finding like "No tests cover FR-003".
                      let findings = facts.BlockingFindings

                      if not (List.isEmpty findings) then
                          failedChecklistPrerequisite path "Checklist contains blocking findings." findings ]

                let allDiagnostics =
                    identityDiagnostics @ diagnostics @ unknownDiagnostics @ readinessDiagnostics
                    |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(checklistSummary facts), Some facts
