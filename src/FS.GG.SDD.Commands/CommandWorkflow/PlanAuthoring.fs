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
open FS.GG.SDD.Commands.Internal.ChecklistAuthoring

module internal PlanAuthoring =
    // Pure `Path` string ops only — the effectful `File`/`Directory` surface stays at the
    // `CommandEffects` edge and is deliberately kept out of scope in the MVU pure core.
    type private Path = System.IO.Path

    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

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
        ensureSections MergePolicies.plan (planSectionText workId) text

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
          // The subset of `DecisionLines` that is auto-generated deferral mirrors (#649): one
          // `PD-### [DEC-###] acceptedDeferral: Accepted deferral DEC-### remains visible to task
          // generation.` per accepted deferral. Carried out separately so the #351 unauthored-scaffold
          // gate can exempt them — a deferral mirror restates an already-authored deferral, it is not a
          // design decision the author must write, so requiring them to rewrite it is the very
          // busywork #646 flags. Requirement-decision scaffold lines stay gated.
          DeferralDecisionLines: string list
          ContractLines: string list
          ObligationLines: string list
          MigrationLines: string list
          ImpactLines: string list
          DeferralLines: string list
          FindingLines: string list
          AdvisoryLines: string list }

    let emptyPlanEntries =
        { DecisionLines = []
          DeferralDecisionLines = []
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
            DeferralDecisionLines = deferralDecisionLines
            ContractLines = contractLines
            ObligationLines = obligationLines
            MigrationLines = migrationLines
            ImpactLines = impactLines
            DeferralLines = deferralLines }

    /// FS.GG.SDD#351 — the unauthored-scaffold rule, stated once, beside the generator it re-derives.
    ///
    /// `plan` scaffolds a decision per requirement, plus a contract / obligation / migration /
    /// generated-view row, each carrying its `[FR-###] [AC-###]` refs BY CONSTRUCTION. So the
    /// FR→plan→task→evidence traceability chain closes with zero human authorship: the gates check
    /// that the ids line up, and the scaffold generates ids that line up. The check and the thing
    /// being checked had the same author.
    ///
    /// The detector is the generator. `plannedPlanEntries` is pure in the four facts `analyze`
    /// already holds, so we re-derive exactly what `plan` WOULD have written and ask whether the
    /// authored plan still contains it, verbatim. No marker for an author to forget to delete, no
    /// schema field, and — the point — the reference is the scaffold's own output, so the rule cannot
    /// drift from the thing it is detecting. (Same idiom as the shipped example's
    /// "fixpoint of its generators" test: re-derive, then compare.)
    ///
    /// Conservative by construction: a line the author has touched at all no longer matches, so this
    /// only ever fires on prose the tool wrote and the human never read.
    ///
    /// It compares the line MINUS its entry id, and that is load-bearing rather than fastidious. We
    /// re-derive from a blank slate (`None`), so our ids always count from `PD-001`; `plan` assigns
    /// its ids incrementally (`Some existingFacts` → `nextScopedIndex`, appending only for sources it
    /// has not already covered). The two agree only while the plan was written in one pass. Insert a
    /// requirement ABOVE an existing one and re-run `plan` and they diverge — the plan holds
    /// `PD-002 [FR-001] …` where we derive `PD-001 [FR-001] …` — so a whole-line comparison would
    /// match nothing, report nothing, and pass a plan that is scaffold top to bottom. That is the
    /// very fail-open this gate exists to close, so the id is exactly the part we must NOT key on.
    /// The refs and the prose are what the tool authored; they are what we compare, and the id we
    /// report is the one the PLAN carries, so the diagnostic names the entry the author must go and
    /// find.
    let unauthoredPlanLines
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planText: string)
        =
        let seeded =
            plannedPlanEntries workId specFacts clarificationFacts checklistFacts None

        let idOf (line: string) =
            // "- PD-001 [FR-001] complete: ..." -> "PD-001"
            line.TrimStart('-', ' ').Split(' ') |> Array.tryHead |> Option.defaultValue line

        // "- PD-001 [FR-001] complete: ..." -> "[FR-001] complete: ..." — everything the scaffold
        // wrote except the id it happened to number the entry with.
        //
        // TrimEnd is load-bearing, not tidiness: surrounding whitespace is not authorship. Both sides
        // of the comparison are normalized here, so a scaffold line that picked up a trailing space —
        // a markdownlint pass, an editor save, a copy-paste — still matches the line the tool wrote.
        // Without it the gate fails OPEN on a whitespace-only edit: the plan is scaffold top to
        // bottom, nothing matches, `analyze` writes analysis.json, and the lifecycle proceeds on
        // prose no human ever read. That is the exact fail-open this gate exists to close.
        let bodyOf (line: string) =
            let entry = line.TrimStart('-', ' ').TrimEnd()

            match entry.IndexOf ' ' with
            | -1 -> entry
            | space -> entry.Substring(space + 1)

        // #649: a pure deferral mirror (`DeferralDecisionLines`) is auto-generated boilerplate that
        // restates an already-authored accepted deferral — the author is not expected to rewrite it,
        // and `tasks` folds it into the keep-visible obligation rather than deriving a second one. So
        // it is EXEMPT from the unauthored-scaffold gate: leaving it verbatim is correct, not a
        // missing decision. Requirement-decision scaffold lines (`DecisionLines` minus the mirrors)
        // stay gated — a placeholder FR decision is still prose no human authored.
        let exemptBodies = seeded.DeferralDecisionLines |> List.map bodyOf |> Set.ofList

        let seededBodies =
            seeded.DecisionLines
            @ seeded.ContractLines
            @ seeded.ObligationLines
            @ seeded.MigrationLines
            @ seeded.ImpactLines
            @ seeded.DeferralLines
            |> List.map bodyOf
            |> Set.ofList
            |> fun bodies -> Set.difference bodies exemptBodies

        planText.Split '\n'
        |> Array.map (fun line -> line.TrimEnd '\r')
        |> Array.filter (fun line -> line.TrimStart().StartsWith "-")
        |> Array.filter (fun line -> seededBodies.Contains(bodyOf line))
        |> Array.map idOf
        |> Array.distinct
        |> Array.sort
        |> List.ofArray

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
        (changeTier: string)
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
changeTier: {changeTier}
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
        let current =
            currentPlanSourceDigests workId specText clarificationText checklistText

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
        |> replaceSectionBody
            "Source Snapshot"
            (sourceSnapshotLines workId specText clarificationText checklistText None)

    let appendPlanEntries existingText entries =
        let bodies =
            Map
                [ "Plan Decisions", entries.DecisionLines
                  "Contract Impact", entries.ContractLines
                  "Verification Obligations", entries.ObligationLines
                  "Migration Posture", entries.MigrationLines
                  "Generated View Impact", entries.ImpactLines
                  "Accepted Deferrals", entries.DeferralLines
                  "Planning Findings", entries.FindingLines
                  "Advisory Notes", entries.AdvisoryLines ]

        existingText
        |> appendToSections bodies (MergePolicy.appendedSections MergePolicies.plan)

    // Feature 090 (#163) removed `appendStalePlanDecision`. It synthesized a
    // `- PD-00N [DEC-00M] stale: Source specification, clarification, or checklist facts changed …`
    // line and appended it to the operator's `## Plan Decisions` — a diagnostic wearing a decision's
    // clothes, appended to a section of `plan.md` the artifact model reserves to the author. The parser then
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

        // FR-011. Emitted on every *successful* `plan` (creation and coherent re-run alike): the plan
        // has frozen the upstream authoring window, and a later edit needs `--accept-upstream`.
        // DiagnosticInfo, so the outcome/exit/changedArtifacts are untouched.
        //
        // Gated on the accompanying diagnostics being non-blocking. `runHandler` discards the write
        // when any of them is a DiagnosticError, so on a blocked run no plan.md exists — and an
        // advisory claiming "Plan snapshotted its sources" would be asserting a snapshot that is not
        // on disk, with a `--accept-upstream` correction that is nonsense against a file that was
        // never written.
        let authoringWindowIfSucceeded (accompanying: Diagnostic list) =
            if
                accompanying
                |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            then
                []
            else
                [ planAuthoringWindow path [ specPath workId; clarificationPath workId; checklistPath workId ] ]

        match snapshot path model with
        | None ->
            let text =
                planTemplate
                    request
                    workId
                    (charteredChangeTier workId model)
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

                let created = diagnostics @ unknownDiagnostics

                created @ authoringWindowIfSucceeded created |> DiagnosticsModule.sort,
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

                        // Derive the bool from the path list rather than computing both: they must never
                        // disagree, and computing them separately hashed all three sources twice.
                        let changed =
                            changedPlanSourcePaths workId specText clarificationText checklistText existingFacts

                        let stale = not (List.isEmpty changed)

                        if stale && not request.AcceptUpstream then
                            // Feature 090 (#163), FR-002/FR-003. The recorded snapshot no longer matches
                            // its sources. Return `existing.Text` *verbatim* — not an entries-appended or
                            // snapshot-refreshed variant — so that even if a caller ignored the effect
                            // gate, the bytes would be identical. `runHandler` discards the write anyway
                            // because `stalePlanSnapshot` is a DiagnosticError. The operator reviews the
                            // recorded decisions against the changed sources, then re-runs with
                            // `--accept-upstream`; nothing is written into the authored plan to say so.
                            (blockingParserDiagnostics @ [ stalePlanSnapshot path changed ])
                            |> DiagnosticsModule.sort,
                            Some existing.Text,
                            Some(planSummary existingFacts)
                        else

                            let ensuredText = ensurePlanSections workId existing.Text

                            let entries =
                                plannedPlanEntries
                                    workId
                                    specFacts
                                    clarificationFacts
                                    checklistFacts
                                    (Some existingFacts)

                            let withEntries = appendPlanEntries ensuredText entries

                            // FR-004. Rewrite the plan's own `## Source Snapshot` body — and nothing else —
                            // whenever the operator asks for it with `--accept-upstream`.
                            //
                            // Gating this on `stale` instead would leave the snapshot unrecoverable once it
                            // is empty: `changedPlanSourcePaths` folds over the *recorded* rows, so a plan
                            // with no recorded digests is never "stale", so the refresh never runs, so it
                            // stays empty — permanently disabling FR-008/SC-004 for that plan. An operator
                            // who deleted the rows to escape a block (or hand-authored a plan without them)
                            // could never re-establish the baseline, and `--accept-upstream` would report
                            // `noChange` while silently doing nothing.
                            //
                            // Writing whenever the flag is passed also keeps FR-005 intact: the rendered
                            // lines are a pure function of the current sources, so on an already-current
                            // snapshot the bytes are identical and the run still reports `noChange`.
                            let proposedText =
                                if request.AcceptUpstream then
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

                                let rerun =
                                    blockingParserDiagnostics
                                    @ proposedDiagnostics
                                    @ unknownDiagnostics
                                    @ staleDiagnostics

                                rerun @ authoringWindowIfSucceeded rerun |> DiagnosticsModule.sort,
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
                // Scoped to `tasks` and `analyze` ON PURPOSE. `resolvePrerequisites` is shared, and
                // `evidence`, `verify`, and `ship` fold this same `PlanDiagnostics` list into their
                // command diagnostics. Emitting unconditionally would make a one-word typo in
                // `spec.md` block the entire back half of the lifecycle — `runHandler`'s effect gate
                // would discard the writes, so `readiness/<id>/verify.json` and `ship.json` would
                // never be emitted — and would route the operator to re-run `plan` at ship time,
                // appending derived rows to an implemented plan out of lifecycle order. `tasks` and
                // `analyze` are the stages that *derive* from the plan; they are the ones that must
                // not consume a stale one.
                //
                // `request.AcceptUpstream` is deliberately NOT consulted: accepting the upstream is
                // the operator's gesture at `plan`, never an implicit downstream one. A missing
                // upstream source yields `None` here and no snapshot diagnostic — that case is a
                // *missing* prerequisite, already reported by the specification/clarification/
                // checklist diagnostics, and `stalePlanSnapshot` must not mask it.
                let snapshotDiagnostics =
                    let derivesFromPlan =
                        match model.Request.Command with
                        | Tasks
                        | Analyze -> true
                        | _ -> false

                    let textAt pathOf =
                        snapshot (pathOf workId) model |> Option.map _.Text

                    if not derivesFromPlan then
                        []
                    else
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
