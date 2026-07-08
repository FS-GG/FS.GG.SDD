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
open FS.GG.SDD.Commands.Internal.EarlyStageAuthoring

module internal ChecklistPlanAuthoring =
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
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            checklistStandardSections ()
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
    let plannedChecklistReviews (specFacts: SpecificationFacts) (clarificationFacts: ClarificationFacts) =
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

        requirementReviews @ deferralReviews

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

        ensuredText
        |> replaceSectionBody "Source Snapshot" snapshotLines
        |> replaceSectionBody "Checklist Items" (placeholder "No checklist items recorded." itemLines)
        |> replaceSectionBody "Review Results" (placeholder "No review results recorded." resultLines)
        |> replaceSectionBody
            "Accepted Deferrals"
            (placeholder "No accepted checklist deferrals recorded." deferralLines)
        |> replaceSectionBody "Blocking Findings" (placeholder "No blocking findings recorded." findingLines)

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
        let baseReviews = plannedChecklistReviews specFacts clarificationFacts

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
                match parseChecklistForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
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
                        let ensuredText = ensureChecklistSections workId existing.Text

                        // §3.1 (082, #146): re-derive the machine-derived sections from the
                        // current sources on EVERY run — never re-ingest prior CHK/CR rows as
                        // authored input. A verdict exists iff the current sources justify it,
                        // so an orphaned tool-injected row is reclaimed, not preserved (FR-002,
                        // FR-003). Authored sections are preserved by `ensureChecklistSections`
                        // and the Source Snapshot is refreshed; unchanged sources re-derive to
                        // identical bytes → noChange (FR-008).
                        let reviews = plannedChecklistReviews specFacts clarificationFacts

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
        let normalized =
            (if String.IsNullOrEmpty text then "" else text).Replace("\r\n", "\n")

        let missing =
            planStandardSections ()
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
          DecisionIds =
            facts.Decisions
            |> List.map (fun decision -> decision.DecisionId.Value)
            |> List.sort
          ContractReferenceIds =
            facts.ContractReferences
            |> List.map (fun reference -> reference.ContractId.Value)
            |> List.sort
          VerificationObligationIds =
            facts.VerificationObligations
            |> List.map (fun obligation -> obligation.ObligationId.Value)
            |> List.sort
          MigrationNoteIds =
            facts.MigrationNotes
            |> List.map (fun note -> note.MigrationId.Value)
            |> List.sort
          GeneratedViewImpactIds =
            facts.GeneratedViewImpacts
            |> List.map (fun impact -> impact.ImpactId.Value)
            |> List.sort
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

        match parsePlanFacts snapshot with
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
              facts.ContractReferences
              |> List.map (fun reference -> reference.ContractId.Value)
              facts.VerificationObligations
              |> List.map (fun obligation -> obligation.ObligationId.Value)
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
        | Some(facts: PlanFacts) ->
            [ facts.Decisions |> List.collect (fun decision -> decision.SourceIds)
              facts.ContractReferences |> List.collect (fun reference -> reference.SourceIds)
              facts.VerificationObligations
              |> List.collect (fun obligation -> obligation.SourceIds)
              facts.MigrationNotes |> List.collect (fun note -> note.SourceIds)
              facts.GeneratedViewImpacts |> List.collect (fun impact -> impact.SourceIds)
              facts.AcceptedDeferrals |> List.collect (fun deferral -> deferral.SourceIds) ]
            |> List.concat
            |> Set.ofList

    let lineRefs (ids: string list) =
        ids
        |> List.distinct
        |> List.sort
        |> List.map (fun id -> $"[{id}]")
        |> String.concat " "

    let plannedPlanEntries
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (existingFacts: PlanFacts option)
        =
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

                    Some
                        $"- {id} {lineRefs refs} complete: Plan requirement {requirement.Value} through the plan command contract.")

        let deferralDecisionLines =
            (clarificationFacts.AcceptedDeferrals
             |> List.map (fun decision -> decision.DecisionId.Value))
            @ (checklistFacts.AcceptedDeferrals
               |> List.map (fun result -> result.ResultId.Value))
            |> List.distinct
            |> List.choose (fun sourceId ->
                if Set.contains sourceId existingSources then
                    None
                else
                    let id = allocate "PD" nextDecision

                    Some
                        $"- {id} [{sourceId}] acceptedDeferral: Accepted deferral {sourceId} remains visible to task generation.")

        let firstDecision =
            existingFacts
            |> Option.bind (fun (facts: PlanFacts) ->
                facts.Decisions
                |> List.tryHead
                |> Option.map (fun decision -> decision.DecisionId.Value))
            |> Option.orElseWith (fun () ->
                (requirementDecisionLines @ deferralDecisionLines)
                |> List.tryHead
                |> Option.bind (fun line ->
                    Regex.Match(line, @"\bPD-\d{3,}\b").Value
                    |> function
                        | "" -> None
                        | value -> Some value))
            |> Option.defaultValue "PD-001"

        let contractLines =
            if
                existingFacts
                |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.ContractReferences))
            then
                []
            else
                let id = allocate "PC" nextContract
                [ $"- {id} [{firstDecision}] command report: fsgg-sdd plan, {planPath workId}, and command-report JSON are tool-facing and compatibility-preserving." ]

        let contractId =
            existingFacts
            |> Option.bind (fun (facts: PlanFacts) ->
                facts.ContractReferences
                |> List.tryHead
                |> Option.map (fun contract -> contract.ContractId.Value))
            |> Option.orElseWith (fun () ->
                contractLines
                |> List.tryHead
                |> Option.bind (fun line ->
                    Regex.Match(line, @"\bPC-\d{3,}\b").Value
                    |> function
                        | "" -> None
                        | value -> Some value))
            |> Option.defaultValue "PC-001"

        let obligationLines =
            if
                existingFacts
                |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.VerificationObligations))
            then
                []
            else
                let id = allocate "VO" nextObligation
                [ $"- {id} [{firstDecision}] [{contractId}] semanticTest: Run focused command tests, FSI/prelude evidence, and CLI smoke evidence before task generation." ]

        let migrationLines =
            if
                existingFacts
                |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.MigrationNotes))
            then
                []
            else
                let id = allocate "PM" nextMigration
                [ $"- {id} [{contractId}] diagnoseOnly: Plan schemaVersion 1 is accepted; unsupported plan schemas diagnose before write." ]

        let impactLines =
            if
                existingFacts
                |> Option.exists (fun (facts: PlanFacts) -> not (List.isEmpty facts.GeneratedViewImpacts))
            then
                []
            else
                let id = allocate "GV" nextImpact
                [ $"- {id} [{firstDecision}] workModel: readiness/{workId}/work-model.json refreshes from current plan sources or reports staleGeneratedView." ]

        let deferralLines =
            (clarificationFacts.AcceptedDeferrals
             |> List.map (fun decision -> decision.DecisionId.Value))
            @ (checklistFacts.AcceptedDeferrals
               |> List.map (fun result -> result.ResultId.Value))
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

        let entries =
            plannedPlanEntries workId specFacts clarificationFacts checklistFacts None

        let decisions =
            if List.isEmpty entries.DecisionLines then
                [ "- PD-001 complete: Planning scope is recorded for the selected work item." ]
            else
                entries.DecisionLines

        let contracts =
            if List.isEmpty entries.ContractLines then
                [ "- PC-001 [PD-001] artifact: No additional contract impact recorded." ]
            else
                entries.ContractLines

        let obligations =
            if List.isEmpty entries.ObligationLines then
                [ "- VO-001 [PD-001] test: Run focused command tests before tasks." ]
            else
                entries.ObligationLines

        let migrations =
            if List.isEmpty entries.MigrationLines then
                [ "- PM-001 [PC-001] diagnoseOnly: No migration is required beyond schemaVersion 1 diagnostics." ]
            else
                entries.MigrationLines

        let impacts =
            if List.isEmpty entries.ImpactLines then
                [ $"- GV-001 [PD-001] workModel: readiness/{workId}/work-model.json records current plan sources." ]
            else
                entries.ImpactLines

        let deferrals =
            if List.isEmpty entries.DeferralLines then
                [ "No accepted plan deferrals recorded." ]
            else
                entries.DeferralLines

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

    let knownPlanSourceIds
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
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
          checklistFacts.Items |> List.map (fun item -> item.ItemId.Value)
          checklistFacts.Results |> List.map (fun result -> result.ResultId.Value)
          planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
          planFacts.ContractReferences
          |> List.map (fun reference -> reference.ContractId.Value)
          planFacts.VerificationObligations
          |> List.map (fun obligation -> obligation.ObligationId.Value)
          planFacts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value)
          planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value) ]
        |> List.concat
        |> Set.ofList

    let unknownPlanReferences path specFacts clarificationFacts checklistFacts planFacts =
        let known = knownPlanSourceIds specFacts clarificationFacts checklistFacts planFacts

        [ planFacts.Decisions |> List.collect (fun decision -> decision.SourceIds)
          planFacts.ContractReferences
          |> List.collect (fun reference -> reference.SourceIds)
          planFacts.VerificationObligations
          |> List.collect (fun obligation -> obligation.SourceIds)
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

    let private currentPlanSourceDigests workId specText clarificationText checklistText =
        [ specPath workId, (SchemaVersionModule.sha256Text specText).Value
          clarificationPath workId, (SchemaVersionModule.sha256Text clarificationText).Value
          checklistPath workId, (SchemaVersionModule.sha256Text checklistText).Value ]
        |> Map.ofList

    let planSourceSnapshotStale workId specText clarificationText checklistText (existingFacts: PlanFacts) =
        sourceDigestsStale
            (existingFacts.SourceSnapshots
             |> List.map (fun snapshot -> snapshot.Path, snapshot.Digest))
            (currentPlanSourceDigests workId specText clarificationText checklistText)

    // Feature 090 (#163). The sibling of `planSourceSnapshotStale`: *which* recorded sources moved,
    // ordinally sorted so the diagnostic's RelatedIds are deterministic (FR-002/FR-014). The
    // predicate below MUST stay identical to `Foundation.sourceDigestsStale`'s inner test — an
    // absent recorded digest is not-stale (FR-016: an old plan does not become blocked on upgrade),
    // and a recorded path with no current digest is not-stale (the source is *missing*, which the
    // `missing…Prerequisite` diagnostics already report; `stalePlanSnapshot` must not mask them).
    // `changedPlanSourcePaths <> [] ⟺ planSourceSnapshotStale` is pinned by a test.
    let changedPlanSourcePaths workId specText clarificationText checklistText (existingFacts: PlanFacts) =
        let current = currentPlanSourceDigests workId specText clarificationText checklistText

        existingFacts.SourceSnapshots
        |> List.choose (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path current with
            | Some recorded, Some actual when not (String.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase)) ->
                Some snapshot.Path
            | _ -> None)
        |> List.distinct
        |> List.sortWith (fun left right -> String.CompareOrdinal(left, right))

    // Feature 090 (#163). Re-baseline the plan's own snapshot, mirroring `rederiveChecklist`'s
    // `replaceSectionBody "Source Snapshot"`. Touches no other section: the plan's authored prose is
    // not this function's business, and `plan` has no other tool-writable region.
    let refreshPlanSnapshot workId specText clarificationText checklistText (text: string) =
        text
        |> replaceSectionBody "Source Snapshot" (sourceSnapshotLines workId specText clarificationText checklistText None)

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

    // Feature 090 (#163) removed `appendStalePlanDecision`. It synthesized a
    // `- PD-00N [DEC-00M] stale: Source specification, clarification, or checklist facts changed …`
    // line and appended it to the operator's `## Plan Decisions` — a diagnostic wearing a decision's
    // clothes, written into a file the artifact model classifies `AuthoredSource`. The parser then
    // read it back as a decision with `Status = "stale"`, so `tasks` blocked two stages later on
    // `failedPlanPrerequisite`. It is replaced by the `stalePlanSnapshot` DiagnosticError, which
    // `runHandler`'s effect gate turns into a zero-write block at `plan` itself.

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

        // FR-011. Emitted on every successful `plan` (creation and coherent re-run alike): the plan
        // has frozen the upstream authoring window, and a later edit needs `--accept-upstream`.
        // DiagnosticInfo, so the outcome/exit/changedArtifacts are untouched.
        let authoringWindow =
            [ planAuthoringWindow path [ specPath workId; clarificationPath workId; checklistPath workId ] ]

        match snapshot path model with
        | None ->
            let text =
                planTemplate
                    request
                    workId
                    specText
                    clarificationText
                    checklistText
                    specFacts
                    clarificationFacts
                    checklistFacts

            match parsePlanForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let unknownDiagnostics =
                    unknownPlanReferences path specFacts clarificationFacts checklistFacts facts

                diagnostics @ unknownDiagnostics @ authoringWindow |> DiagnosticsModule.sort,
                Some text,
                Some(planSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            else
                match parsePlanForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        frontMatterIdentityDiagnostics
                            "Plan"
                            LifecycleStage.Plan
                            "plan"
                            malformedPlanFrontMatter
                            planIdentityMismatch
                            malformedPlanFrontMatter
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
                                malformedPlanFrontMatter
                                    path
                                    $"Plan sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceClarifications,
                                        clarificationPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedPlanFrontMatter
                                    path
                                    $"Plan sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceChecklist,
                                        checklistPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedPlanFrontMatter
                                    path
                                    $"Plan sourceChecklist '{existingFacts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'." ]

                    let unknownDiagnostics =
                        unknownPlanReferences path specFacts clarificationFacts checklistFacts existingFacts

                    let blockingParserDiagnostics =
                        identityDiagnostics @ existingDiagnostics @ unknownDiagnostics
                        |> DiagnosticsModule.sort

                    let hasBlockingParserDiagnostics =
                        blockingParserDiagnostics
                        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        blockingParserDiagnostics, Some existing.Text, Some(planSummary existingFacts)
                    else

                    let stale =
                        planSourceSnapshotStale workId specText clarificationText checklistText existingFacts

                    if stale && not request.AcceptUpstream then
                        // Feature 090 (#163), FR-002/FR-003. The recorded snapshot no longer matches
                        // its sources. Return `existing.Text` *verbatim* — not an entries-appended or
                        // snapshot-refreshed variant — so that even if a caller ignored the effect
                        // gate, the bytes would be identical. `runHandler` discards the write anyway
                        // because `stalePlanSnapshot` is a DiagnosticError. The operator reviews the
                        // recorded decisions against the changed sources, then re-runs with
                        // `--accept-upstream`; nothing is written into the authored plan to say so.
                        let changed =
                            changedPlanSourcePaths workId specText clarificationText checklistText existingFacts

                        (blockingParserDiagnostics @ [ stalePlanSnapshot path changed ])
                        |> DiagnosticsModule.sort,
                        Some existing.Text,
                        Some(planSummary existingFacts)
                    else

                    let ensuredText = ensurePlanSections workId existing.Text

                    let entries =
                        plannedPlanEntries workId specFacts clarificationFacts checklistFacts (Some existingFacts)

                    let withEntries = appendPlanEntries ensuredText entries

                    // FR-004. `stale` here implies `request.AcceptUpstream`: the operator's explicit
                    // gesture. Rewrite the plan's own `## Source Snapshot` body and nothing else.
                    // When not stale this is inert, so `--accept-upstream` on a current plan is a
                    // no-op (FR-005) rather than a gratuitous rewrite.
                    let proposedText =
                        if stale then
                            refreshPlanSnapshot workId specText clarificationText checklistText withEntries
                        else
                            withEntries

                    match parsePlanForCommand path proposedText with
                    | Error diagnostics -> diagnostics, Some proposedText, None
                    | Ok(proposedFacts, proposedDiagnostics) ->
                        // FR-009. `stalePlanDecision` no longer reports digest drift — that is now
                        // `stalePlanSnapshot`. It reports the *authored* case: an operator-written
                        // `stale:` marker in `## Plan Decisions`, which `tasks` blocks on. Warning
                        // severity, so `plan` still advances; `tasks` is where it becomes fatal.
                        let authoredStaleDecisions =
                            proposedFacts.Decisions
                            |> List.filter (fun decision -> decision.Status = "stale")
                            |> List.map (fun decision -> decision.DecisionId.Value)

                        let staleDiagnostics =
                            if List.isEmpty authoredStaleDecisions then
                                []
                            else
                                [ stalePlanDecision path authoredStaleDecisions ]

                        let unknownDiagnostics =
                            unknownPlanReferences path specFacts clarificationFacts checklistFacts proposedFacts

                        blockingParserDiagnostics
                        @ proposedDiagnostics
                        @ unknownDiagnostics
                        @ staleDiagnostics
                        @ authoringWindow
                        |> DiagnosticsModule.sort,
                        Some proposedText,
                        Some(planSummary proposedFacts)

    let planPrerequisiteDiagnosticsTextSummaryAndFacts workId specFacts clarificationFacts checklistFacts model =
        let path = planPath workId

        match snapshot path model with
        | None -> [ missingPlanPrerequisite path $"Plan prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parsePlanForCommand path existing.Text with
            | Error diagnostics ->
                let mapped =
                    diagnostics
                    |> List.map (fun diagnostic -> malformedPlanFrontMatter path diagnostic.Message)

                mapped, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Plan"
                        LifecycleStage.Plan
                        "plan"
                        malformedPlanFrontMatter
                        planIdentityMismatch
                        missingPlanPrerequisite
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
                            malformedPlanFrontMatter
                                path
                                $"Plan sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceClarifications,
                                    clarificationPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedPlanFrontMatter
                                path
                                $"Plan sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceChecklist,
                                    checklistPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedPlanFrontMatter
                                path
                                $"Plan sourceChecklist '{facts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'." ]

                let unknownDiagnostics =
                    unknownPlanReferences path specFacts clarificationFacts checklistFacts facts

                let readinessDiagnostics =
                    [ if
                          not (String.Equals(facts.FrontMatter.Status, "planned", StringComparison.OrdinalIgnoreCase))
                      then
                          failedPlanPrerequisite
                              path
                              $"Plan status '{facts.FrontMatter.Status}' is not planned."
                              [ facts.FrontMatter.Status ]

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

                      // `BlockingFindings` is already sentinel-free (the parser drops
                      // no-outstanding disclaimers); filtering `StartsWith "No "` here would
                      // wrongly re-drop a genuine finding like "No tests cover FR-003".
                      let findings = facts.BlockingFindings

                      if not (List.isEmpty findings) then
                          failedPlanPrerequisite path "Plan contains blocking planning findings." findings ]

                // Feature 090 (#163), FR-008. `tasks` and `analyze` read the plan as a prerequisite.
                // `plan` no longer injects a `stale:` decision marker for them to key on, so they
                // detect the digest drift themselves — otherwise an operator who edits `spec.md` and
                // skips straight to `tasks` would generate a task graph against a plan that no
                // longer matches its sources. The upstream texts come straight off `model`, exactly
                // as this function already reads `plan.md`, so nothing here changes shape and
                // `Prerequisites.fs` is untouched.
                //
                // `request.AcceptUpstream` is deliberately NOT consulted: accepting the upstream is
                // the operator's gesture at `plan`, never an implicit downstream one. A missing
                // upstream source yields `None` here and no snapshot diagnostic — that case is a
                // *missing* prerequisite, already reported by the specification/clarification/
                // checklist diagnostics, and `stalePlanSnapshot` must not mask it.
                let snapshotDiagnostics =
                    let textAt pathOf = snapshot (pathOf workId) model |> Option.map _.Text

                    match textAt specPath, textAt clarificationPath, textAt checklistPath with
                    | Some specText, Some clarificationText, Some checklistText ->
                        match changedPlanSourcePaths workId specText clarificationText checklistText facts with
                        | [] -> []
                        | changed -> [ stalePlanSnapshot path changed ]
                    | _ -> []

                let allDiagnostics =
                    identityDiagnostics
                    @ diagnostics
                    @ unknownDiagnostics
                    @ readinessDiagnostics
                    @ snapshotDiagnostics
                    |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(planSummary facts), Some facts
