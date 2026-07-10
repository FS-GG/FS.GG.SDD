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
open FS.GG.SDD.Commands.Internal.TaskGraphAuthoring
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites

module internal HandlersEvidence =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    type EvidenceDispositionDraft =
        { ObligationId: string
          State: string
          EvidenceIds: string list
          TaskIds: string list
          DiagnosticIds: string list }

    // `evidenceKindSourceValue`/`allowedEvidenceResults`/`normalizedEvidenceResult` moved to
    // FS.GG.SDD.Artifacts (Evidence.fs) so the shared codec can drive both directions; call sites
    // here and in HandlersVerify resolve them via AutoOpen (FS.GG.SDD#260).

    let evidenceAnalysisSummary path (view: AnalysisView) : AnalysisSummary =
        { WorkId = view.WorkId.Value
          Stage = IdentifiersModule.stageValue view.Stage
          Status = view.Status
          AnalysisPath = path
          SourceCount = view.Sources.Length
          SourceRelationshipCount = view.SourceRelationships.Length
          ReadyFindingCount = view.Readiness.ReadyCount
          AdvisoryCount = view.Readiness.AdvisoryCount
          WarningCount = view.Readiness.WarningCount
          BlockingCount = view.Readiness.BlockingCount
          StaleSourceCount = view.Readiness.StaleSourceCount
          MissingDispositionCount = view.Readiness.MissingDispositionCount
          MalformedSourceCount = view.Readiness.MalformedSourceCount
          GeneratedViewFindingCount = view.Readiness.GeneratedViewFindingCount
          AcceptedDeferralCount = view.Readiness.AcceptedDeferralCount
          Readiness = view.Readiness.Status }

    let analysisPrerequisiteDiagnosticsSummaryAndText workId model =
        let path = analysisPath workId

        match snapshot path model with
        | None -> [ missingAnalysisPrerequisite path $"Analysis prerequisite '{path}' is missing." ], None, None
        | Some existing ->
            match parseAnalysisView existing with
            | Error diagnostics ->
                diagnostics
                |> List.map (fun diagnostic -> malformedAnalysisView path diagnostic.Message),
                Some existing.Text,
                None
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ analysisIdentityMismatch path workId view.WorkId.Value ],
                Some existing.Text,
                Some(evidenceAnalysisSummary path view)
            | Ok view when
                not (String.Equals(view.Readiness.Status, "implementationReady", StringComparison.OrdinalIgnoreCase))
                ->
                [ analysisNotReady path view.Readiness.Status ],
                Some existing.Text,
                Some(evidenceAnalysisSummary path view)
            | Ok view -> [], Some existing.Text, Some(evidenceAnalysisSummary path view)

    let mapEvidenceDiagnostics path (diagnostics: Diagnostic list) : Diagnostic list =
        diagnostics
        |> List.map (fun diagnostic ->
            match diagnostic.Id, diagnostic.RelatedIds with
            | "duplicateIdentifier", id :: _ -> duplicateEvidenceId path id
            | "workModelInconsistent", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "malformedSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "unsupportedSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | "futureSchemaVersion", _ -> malformedEvidenceArtifact path diagnostic.Message
            | _ -> diagnostic)

    let parseEvidenceArtifactForCommand path text : Result<EvidenceArtifact * Diagnostic list, Diagnostic list> =
        match parseEvidenceArtifact { Path = path; Text = text } with
        | Ok artifact -> Ok(artifact, mapEvidenceDiagnostics path artifact.Diagnostics)
        | Error diagnostics -> Error(mapEvidenceDiagnostics path diagnostics)

    let parseExistingEvidence workId (model: CommandModel) : EvidenceArtifact option * Diagnostic list * string option =
        let path = evidencePath workId

        snapshot path model
        |> Option.map (fun snapshot ->
            match parseEvidenceArtifactForCommand path snapshot.Text with
            | Ok(artifact, diagnostics) -> Some artifact, diagnostics, Some snapshot.Text
            | Error diagnostics -> None, diagnostics, Some snapshot.Text)
        |> Option.defaultValue (None, [], None)

    let parseInputEvidence workId (request: CommandRequest) : EvidenceArtifact option * Diagnostic list =
        let path = evidencePath workId

        request.InputText
        |> Option.map (fun text ->
            match parseEvidenceArtifactForCommand path text with
            | Ok(artifact, diagnostics) -> Some artifact, diagnostics
            | Error diagnostics -> None, diagnostics)
        |> Option.defaultValue (None, [])

    let evidenceSourceSnapshot label path text : EvidenceSourceSnapshot =
        { Label = label
          Path = path
          Digest = Some((SchemaVersionModule.sha256Text text).Value)
          SchemaVersion = Some 1
          SourceLocation = None }

    let currentEvidenceSourceSnapshots
        workId
        specText
        clarificationText
        checklistText
        planText
        tasksText
        analysisText
        : EvidenceSourceSnapshot list =
        [ evidenceSourceSnapshot "spec" (specPath workId) specText
          evidenceSourceSnapshot "clarifications" (clarificationPath workId) clarificationText
          evidenceSourceSnapshot "checklist" (checklistPath workId) checklistText
          evidenceSourceSnapshot "plan" (planPath workId) planText
          evidenceSourceSnapshot "tasks" (tasksPath workId) tasksText
          evidenceSourceSnapshot "analysis" (analysisPath workId) analysisText ]

    let evidenceSourceSnapshotStale (current: EvidenceSourceSnapshot list) (recorded: EvidenceSourceSnapshot list) =
        let currentMap =
            current
            |> List.choose (fun snapshot -> snapshot.Digest |> Option.map (fun digest -> snapshot.Path, digest))
            |> Map.ofList

        recorded
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path currentMap with
            | Some recordedDigest, Some currentDigest ->
                not (String.Equals(recordedDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
            | Some _, None -> true
            | _ -> false)

    let declarationMeaningKey (declaration: EvidenceDeclaration) =
        (evidenceKindSourceValue declaration.Kind,
         declaration.Subject.SubjectType,
         declaration.Subject.Id,
         declaration.TaskRefs |> List.map _.Value |> List.sort,
         declaration.RequirementRefs |> List.map _.Value |> List.sort,
         declaration.ObligationRefs |> List.sort,
         declaration.SourceRefs
         |> List.map (fun source -> source.Kind, source.Path, source.Uri, source.Result)
         |> List.sort,
         normalizedEvidenceResult declaration.Result,
         declaration.Synthetic,
         declaration.SyntheticDisclosure
         |> Option.map (fun disclosure -> disclosure.StandsInFor, disclosure.Reason),
         declaration.Rationale,
         declaration.Owner,
         declaration.Scope,
         declaration.LaterLifecycleVisibility)

    let evidenceObligations (taskFacts: TaskFacts) : EvidenceObligation list =
        taskFacts.Tasks
        |> List.collect (fun task ->
            let ids =
                if List.isEmpty task.RequiredEvidence && task.Status = TaskStatus.Done then
                    [ $"task.{task.Id.Value}.completion" ]
                else
                    task.RequiredEvidence |> List.map _.Value

            ids
            |> List.map (fun id ->
                { ObligationId = id
                  Kind = "taskEvidence"
                  SourceArtifactPath = task.Source.Path
                  SourceId = Some task.Id.Value
                  LinkedTaskIds = [ task.Id ]
                  LinkedRequirementIds = task.Requirements
                  LinkedDecisionIds = task.Decisions |> List.map _.Value
                  // Feature 077: carry the task's full source-id lineage so the scaffolded
                  // declaration can recover the plan-decision (and FR-via-plan) origin that
                  // task.Requirements/task.Decisions omit for a plan-decision task.
                  //
                  // Feature 096 (issue #189): do NOT "fix" this to
                  // `task.SourceIds ∪ requirements ∪ decisions`. It has been proposed twice and is a
                  // no-op both times: `LinkedSourceIds` has exactly one consumer — `routeSourceRefs`
                  // below — and that call site already unions this field with `LinkedRequirementIds`
                  // and `LinkedDecisionIds`. Widening here would change no emitted byte. The blind
                  // consumers were `WorkModel.deriveGuidanceModel` and `HandlersVerify`, both fixed
                  // at their own seams; `evidence` was never blind.
                  LinkedSourceIds = task.SourceIds
                  ExpectedEvidenceKinds = [ "implementation"; "verification"; "deferral"; "synthetic" ]
                  RequiredSkillOrCapabilityTags = task.RequiredSkills
                  Blocking = true
                  Correction =
                    $"Add evidence {id} for {task.Id.Value} with result: pass and synthetic: false (a synthetic pass does not satisfy it), or an accepted deferral linked to {task.Id.Value}." }))
        // Spec 096 AC-005 (issue #225): an obligation id required by two tasks must yield ONE
        // obligation carrying the union of both lineages — not a duplicate per task. The
        // `List.collect` above emits one draft per (task, requiredEvidence) pair, so two tasks that
        // share `requiredEvidence: [EV-001]` would otherwise produce two obligations with the same
        // `ObligationId`, scaffolding duplicate `EV-001` declarations and duplicate `ED-EV-001`
        // disposition rows, and leaving `verifyEvidenceDispositionViews.affectedSourceIds`' union
        // fold (written for many task ids) unreachable. Group by the obligation id and union the
        // lineage — the same shape `verifyTestDispositionViews` reaches with its `groupBy` on the
        // obligation id, so `TD-`/`ED-` merge identically. Scalar fields (`Kind`, source, correction,
        // …) are per-first-task and not read downstream; `List.distinct` preserves first-occurrence
        // (task) order, so a single-task obligation passes through byte-identically.
        |> List.groupBy (fun obligation -> obligation.ObligationId)
        |> List.map (fun (_, group) ->
            { List.head group with
                LinkedTaskIds = group |> List.collect _.LinkedTaskIds |> List.distinct
                LinkedRequirementIds = group |> List.collect _.LinkedRequirementIds |> List.distinct
                LinkedDecisionIds = group |> List.collect _.LinkedDecisionIds |> List.distinct
                LinkedSourceIds = group |> List.collect _.LinkedSourceIds |> List.distinct
                RequiredSkillOrCapabilityTags = group |> List.collect _.RequiredSkillOrCapabilityTags |> List.distinct })

    // Feature 077 (issue #124): route an obligation's origin lineage into the declaration's
    // `requirementRefs` / `planDecisionRefs` buckets by the shared id grammar
    // (Identifiers.create*). Scope is deliberately those two buckets — the ids the issue asks
    // scaffolding to preserve — so a plan-decision obligation recovers its `PD-###` and the
    // `FR-###` it traces to. Other lineage ids (`AC-`/`DEC-`/`CR-`/`PC-`/`VO-`/`PM-`/`GV-`/…) are
    // left unrouted: the acceptance/clarification/checklist buckets stay empty on scaffolds (as
    // before), so scaffolding does not widen the evidence stage's unknown-reference validation
    // surface beyond the requirement/plan-decision origin the author actually classifies against.
    // Routing never errors on an unmatched id (Principle VIII); each bucket is de-duplicated and
    // sorted for deterministic, idempotent output (FR-005 / SC-005).
    let routeSourceRefs (ids: string list) =
        let pick create =
            ids
            |> List.choose (fun id ->
                match create id with
                | Ok typed -> Some typed
                | Error _ -> None)
            |> List.distinct

        {| Requirements =
            pick IdentifiersModule.createRequirementId
            |> List.sortBy (fun (id: RequirementId) -> id.Value)
           PlanDecisions =
            pick IdentifiersModule.createPlanDecisionId
            |> List.sortBy (fun (id: PlanDecisionId) -> id.Value) |}

    // Feature 077: `evidence --from-tests <path>` pre-maps each newly scaffolded obligation to a
    // verification-kind source pointing at the proving test path. `None` (or a blank value) ⇒ no
    // source seeded, so scaffolding output is unchanged aside from the routed refs. The path is a
    // declared pointer; its on-disk existence/freshness is a verify-stage concern, not evaluated
    // here (the evidence stage declares; verify validates).
    let fromTestsSourceRefs (fromTests: string option) : EvidenceSourceReference list =
        match fromTests |> Option.map (fun path -> path.Trim()) with
        | Some path when path <> "" ->
            [ { ReferenceId = None
                Kind = "verification"
                Path = Some path
                Uri = None
                Digest = None
                RelatedSourceId = None
                Result = None
                SourceLocation = None } ]
        | _ -> []

    let skeletonEvidenceDeclaration workId (fromTests: string option) (obligation: EvidenceObligation) =
        let evidenceId =
            match IdentifiersModule.createEvidenceId obligation.ObligationId with
            | Ok id -> id
            | Error _ -> taskEvidenceId 1

        // Feature 077: classify the union of every id the obligation carries — its source-id
        // lineage plus the requirement/decision refs it already holds — so a plan-decision
        // obligation recovers its PD id and the FR it traces to (both live in SourceIds, while
        // task.Requirements/task.Decisions are empty for that task). Subsumes the previous
        // requirement-only derivation; other ref buckets keep their prior (empty) scaffold value.
        let routed =
            routeSourceRefs (
                obligation.LinkedSourceIds
                @ (obligation.LinkedRequirementIds |> List.map _.Value)
                @ obligation.LinkedDecisionIds
            )

        let taskRefs = obligation.LinkedTaskIds

        let subject =
            match taskRefs with
            | task :: _ ->
                { SubjectType = "task"
                  Id = task.Value }
            | [] ->
                { SubjectType = "obligation"
                  Id = obligation.ObligationId }

        { Id = evidenceId
          Kind = EvidenceKind.Missing
          Subject = subject
          TaskRefs = taskRefs
          RequirementRefs = routed.Requirements
          AcceptanceScenarioRefs = []
          ClarificationDecisionRefs = []
          ChecklistResultRefs = []
          PlanDecisionRefs = routed.PlanDecisions
          ObligationRefs = [ obligation.ObligationId ]
          ArtifactRefs = []
          // #306: `--from-tests` seeds a proving TEST path onto each newly scaffolded obligation.
          // A visual-inspection obligation is discharged by a rendered frame, not by a test file,
          // and `namesRenderedArtifact` cannot tell the two apart — so seeding one here would
          // pre-satisfy the artifact gate with the wrong kind of proof the moment the author flipped
          // `result: pass`. It is left unseeded: the author names the image, deliberately.
          SourceRefs =
            if isVisualInspectionTagged obligation.RequiredSkillOrCapabilityTags then
                []
            else
                fromTestsSourceRefs fromTests
          Result = "missing"
          Synthetic = false
          SyntheticDisclosure = None
          Rationale = None
          Owner = None
          Scope = None
          LaterLifecycleVisibility = None
          Notes = [ "Evidence required before verify." ]
          Source =
            match
                FS.GG.SDD.Artifacts.ArtifactRef.create
                    (evidencePath workId)
                    ArtifactKind.Evidence
                    ArtifactOwner.Sdd
                    true
            with
            | Ok artifact -> artifact
            | Error message ->
                failwithf
                    "evidence obligation source: invariant violated — evidence artifact path %s rejected: %s"
                    (evidencePath workId)
                    message
          SourceLocation = None }

    // The default next-action note a freshly-created evidence.yml carries. On a re-run the author's
    // own `lifecycleNotes` are preserved verbatim (FS.GG.SDD#181); this is only the seed for a file
    // that declared none, and the empty-list fallback in `evidenceArtifactText` re-injects it.
    let private defaultEvidenceLifecycleNote = "Next lifecycle action: verify."

    let mergeEvidenceArtifacts
        (workId: string)
        (fromTests: string option)
        (existing: EvidenceArtifact option)
        (input: EvidenceArtifact option)
        (obligations: EvidenceObligation list)
        : EvidenceArtifact * Diagnostic list =
        match existing, input with
        | Some existingArtifact, Some inputArtifact ->
            let existingById =
                existingArtifact.Evidence
                |> List.map (fun declaration -> declaration.Id.Value, declaration)
                |> Map.ofList

            let mutable unsafeIds = []

            let additions: EvidenceDeclaration list =
                inputArtifact.Evidence
                |> List.choose (fun declaration ->
                    match Map.tryFind declaration.Id.Value existingById with
                    | None -> Some declaration
                    | Some existingDeclaration ->
                        if declarationMeaningKey declaration = declarationMeaningKey existingDeclaration then
                            None
                        else
                            unsafeIds <- declaration.Id.Value :: unsafeIds
                            None)

            let diagnostics =
                if List.isEmpty unsafeIds then
                    []
                else
                    [ unsafeEvidenceUpdate (evidencePath workId) (unsafeIds |> List.distinct |> List.sort) ]

            ({ existingArtifact with
                Evidence =
                    (existingArtifact.Evidence @ additions)
                    |> List.sortBy (fun declaration -> declaration.Id.Value) }
            : EvidenceArtifact),
            diagnostics
        | Some existingArtifact, None -> existingArtifact, []
        | None, Some inputArtifact -> inputArtifact, []
        | None, None ->
            let workIdValue =
                match IdentifiersModule.createWorkId workId with
                | Ok value -> value
                | Error message ->
                    failwithf
                        "mergeEvidenceArtifacts: invariant violated — pre-validated work id %s rejected: %s"
                        workId
                        message

            ({ SchemaVersion = SchemaVersionModule.create 1
               WorkId = workIdValue
               Stage = LifecycleStage.Evidence
               Status = "needsEvidence"
               SourceSpec = specPath workId
               SourceClarifications = clarificationPath workId
               SourceChecklist = checklistPath workId
               SourcePlan = planPath workId
               SourceTasks = tasksPath workId
               SourceAnalysis = analysisPath workId
               SourceSnapshots = []
               Evidence =
                 obligations
                 |> List.choose (fun obligation ->
                     if obligation.ObligationId.StartsWith("EV", StringComparison.OrdinalIgnoreCase) then
                         Some(skeletonEvidenceDeclaration workId fromTests obligation)
                     else
                         None)
               LifecycleNotes = [ defaultEvidenceLifecycleNote ]
               Diagnostics = [] }
            : EvidenceArtifact),
            []

    /// `artifact` must be the artifact as **recorded** (parsed from disk / merged), never one whose
    /// `SourceSnapshots` have been re-stamped to `currentSnapshots` — the staleEvidenceSource check
    /// reads `artifact.SourceSnapshots` as the recorded side of the comparison.
    let evidenceValidationDiagnostics
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (taskFacts: TaskFacts)
        (currentSnapshots: EvidenceSourceSnapshot list)
        (artifact: EvidenceArtifact)
        =
        let path = evidencePath workId

        let knownTasks =
            taskFacts.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList

        let knownRequirements = specFacts.RequirementIds |> List.map _.Value |> Set.ofList

        let knownScenarios =
            specFacts.AcceptanceScenarioIds |> List.map _.Value |> Set.ofList

        let knownClarifications =
            [ clarificationFacts.Decisions
              |> List.map (fun decision -> decision.DecisionId.Value)
              clarificationFacts.AcceptedDeferrals
              |> List.map (fun decision -> decision.DecisionId.Value) ]
            |> List.concat
            |> Set.ofList

        let knownChecklistResults =
            checklistFacts.Results
            |> List.map (fun result -> result.ResultId.Value)
            |> Set.ofList

        let knownPlanDecisions =
            planFacts.Decisions
            |> List.map (fun decision -> decision.DecisionId.Value)
            |> Set.ofList

        let knownObligations =
            [ planFacts.VerificationObligations
              |> List.map (fun obligation -> obligation.ObligationId.Value)
              taskFacts.Tasks
              |> List.collect (fun task -> task.RequiredEvidence |> List.map _.Value) ]
            |> List.concat
            |> Set.ofList

        let unknowns =
            artifact.Evidence
            |> List.collect (fun declaration ->
                [ declaration.TaskRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownTasks))
                  declaration.RequirementRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownRequirements))
                  declaration.AcceptanceScenarioRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownScenarios))
                  declaration.ClarificationDecisionRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownClarifications))
                  declaration.ChecklistResultRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownChecklistResults))
                  declaration.PlanDecisionRefs
                  |> List.map _.Value
                  |> List.filter (fun id -> not (Set.contains id knownPlanDecisions))
                  declaration.ObligationRefs
                  |> List.filter (fun id ->
                      not (Set.contains id knownObligations)
                      && not (id.StartsWith("EV", StringComparison.OrdinalIgnoreCase))) ]
                |> List.concat)
            |> List.distinct
            |> List.sort

        let unsupportedResults =
            artifact.Evidence
            |> List.map (fun declaration -> declaration.Result)
            |> List.map normalizedEvidenceResult
            |> List.filter (fun result -> not (Set.contains result allowedEvidenceResults))
            |> List.distinct
            |> List.sort

        let undisclosedSynthetic =
            artifact.Evidence
            |> List.filter (fun declaration -> declaration.Synthetic && Option.isNone declaration.SyntheticDisclosure)
            |> List.map (fun declaration -> declaration.Id.Value)

        let missingDeferralFields =
            artifact.Evidence
            |> List.filter (fun declaration ->
                declaration.Kind = EvidenceKind.Deferral
                || normalizedEvidenceResult declaration.Result = "deferred")
            |> List.filter (fun declaration ->
                Option.isNone declaration.Rationale
                || Option.isNone declaration.Owner
                || Option.isNone declaration.Scope
                || Option.isNone declaration.LaterLifecycleVisibility)
            |> List.map (fun declaration -> declaration.Id.Value)

        // #306: this gate runs BEFORE the writer, so it has no `EvidenceObligation` list to read the
        // visual-inspection tag off (`evidenceDispositions` does, and applies the same rule to the
        // disposition). Recover the obligation ids from the tasks that mint them: a task tagged
        // `visual-inspection` obligates each id in its `requiredEvidence`.
        let visualObligationIds =
            taskFacts.Tasks
            |> List.filter (fun task -> isVisualInspectionTagged task.RequiredSkills)
            |> List.collect (fun task -> task.RequiredEvidence |> List.map _.Value)
            |> Set.ofList

        let namesVisualObligation (declaration: EvidenceDeclaration) =
            Set.contains declaration.Id.Value visualObligationIds
            || declaration.ObligationRefs
               |> List.exists (fun id ->
                   visualObligationIds
                   |> Set.exists (fun obligationId ->
                       String.Equals(id, obligationId, StringComparison.OrdinalIgnoreCase)))

        let missingVisualArtifacts =
            artifact.Evidence
            |> List.filter (fun declaration ->
                namesVisualObligation declaration && passesWithoutRenderedArtifact declaration)
            |> List.map (fun declaration -> declaration.Id.Value)

        [ if not (String.Equals(artifact.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) then
              evidenceIdentityMismatch path workId artifact.WorkId.Value
          if artifact.Stage <> LifecycleStage.Evidence then
              malformedEvidenceArtifact
                  path
                  $"Evidence stage '{IdentifiersModule.stageValue artifact.Stage}' is not 'evidence'."
          if normalizeRelativePath artifact.SourceSpec <> specPath workId then
              malformedEvidenceArtifact
                  path
                  $"Evidence sourceSpec '{artifact.SourceSpec}' does not match '{specPath workId}'."
          if normalizeRelativePath artifact.SourceTasks <> tasksPath workId then
              malformedEvidenceArtifact
                  path
                  $"Evidence sourceTasks '{artifact.SourceTasks}' does not match '{tasksPath workId}'."
          if normalizeRelativePath artifact.SourceAnalysis <> analysisPath workId then
              malformedEvidenceArtifact
                  path
                  $"Evidence sourceAnalysis '{artifact.SourceAnalysis}' does not match '{analysisPath workId}'."
          if not (List.isEmpty unknowns) then
              unknownEvidenceReference path (String.concat "," unknowns)
          if not (List.isEmpty unsupportedResults) then
              unsupportedEvidenceResultState path unsupportedResults
          if not (List.isEmpty undisclosedSynthetic) then
              undisclosedSyntheticEvidence path undisclosedSynthetic
          if not (List.isEmpty missingDeferralFields) then
              missingDeferralRationale path missingDeferralFields
          if not (List.isEmpty missingVisualArtifacts) then
              missingVisualInspectionArtifact path missingVisualArtifacts
          if evidenceSourceSnapshotStale currentSnapshots artifact.SourceSnapshots then
              staleEvidenceSource
                  path
                  (artifact.SourceSnapshots
                   |> List.map (fun snapshot -> snapshot.Label)
                   |> List.filter (String.IsNullOrWhiteSpace >> not)) ]

    let evidenceDispositions
        (obligations: EvidenceObligation list)
        (artifact: EvidenceArtifact)
        : EvidenceDispositionDraft list =
        obligations
        |> List.mapi (fun index obligation ->
            // Issue #230: match a declaration to a *nameable* (`EV###`) obligation by obligation id
            // ONLY (`id` or `obligationRefs`), byte-for-byte mirroring `verifyTestDispositionViews`
            // above. `ED-` used to carry a third `TaskRefs`-overlap clause for every obligation — a
            // declaration referencing one of the obligation's `LinkedTaskIds` matched even without
            // naming the obligation. #225 unioned `LinkedTaskIds` across the tasks sharing an
            // obligation (to mirror `TD-`), which widened that clause to span *all* of a shared
            // obligation's tasks: a declaration referencing only task `T1` then silently satisfied a
            // `T1`+`T2` `EV###` obligation, hiding `T2`'s uncovered gap and passing verify (the
            // collapse also propagated through `verifySkillViews`). #225's stated model is "one merged
            // obligation, satisfied once — mirror `TD-`", and the `TaskRefs` clause was exactly the
            // piece `TD-` lacked, so for `EV###` obligations dropping it finishes the mirror: a
            // scaffolded `EV###` declaration always carries `obligationRefs: [<obligationId>]`, so it
            // still matches id-first, and only a purely hand-authored declaration that references a
            // task but never names the obligation changes — it must now name the obligation.
            //
            // The one exception is a `task.{id}.completion` obligation (minted at the `evidenceObligations`
            // `List.isEmpty task.RequiredEvidence && Done` branch above): the one obligation kind `TD-`
            // lacks and that *no* declaration can name — it is never scaffolded (the `StartsWith("EV")`
            // filter below skips it), its id is not a valid evidence `id` (`^EV\d{3,}$`), and naming it
            // in `obligationRefs` trips `evidence.unknownReference`. Its only satisfaction route is a
            // task reference, and its id embeds the task id so `LinkedTaskIds` is always a singleton —
            // the task-ref match is exact, with none of the shared-`EV###` widening #230 removes. So
            // keep the `TaskRefs` clause, scoped to completion obligations only.
            let isCompletionObligation =
                obligation.ObligationId.StartsWith("task.", StringComparison.Ordinal)
                && obligation.ObligationId.EndsWith(".completion", StringComparison.Ordinal)

            let matches: EvidenceDeclaration list =
                artifact.Evidence
                |> List.filter (fun declaration ->
                    declaration.Id.Value = obligation.ObligationId
                    || declaration.ObligationRefs
                       |> List.exists (fun id ->
                           String.Equals(id, obligation.ObligationId, StringComparison.OrdinalIgnoreCase))
                    || (isCompletionObligation
                        && declaration.TaskRefs
                           |> List.exists (fun taskId ->
                               obligation.LinkedTaskIds
                               |> List.exists (fun linked -> linked.Value = taskId.Value))))

            let state, diagnostics =
                if List.isEmpty matches then
                    "missing", [ "evidence.missingRequiredEvidence" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        declaration.Synthetic && Option.isNone declaration.SyntheticDisclosure)
                then
                    "invalid", [ "evidence.undisclosedSyntheticEvidence" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        (declaration.Kind = EvidenceKind.Deferral
                         || normalizedEvidenceResult declaration.Result = "deferred")
                        && (Option.isNone declaration.Rationale
                            || Option.isNone declaration.Owner
                            || Option.isNone declaration.Scope
                            || Option.isNone declaration.LaterLifecycleVisibility))
                then
                    "invalid", [ "evidence.missingDeferralRationale" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        not (Set.contains (normalizedEvidenceResult declaration.Result) allowedEvidenceResults))
                then
                    "invalid", [ "evidence.unsupportedResultState" ]
                // #306: a visual-inspection obligation is satisfied only by a rendered artifact plus
                // an explicit disposition. The `not declaration.Synthetic` guard keeps a disclosed
                // synthetic pass falling through to `"synthetic"` below (unsatisfying, but honest and
                // not a defect) rather than being reclassified `"invalid"`. A deferral never reaches
                // here — its result is `deferred`, not `pass`.
                elif
                    isVisualInspectionTagged obligation.RequiredSkillOrCapabilityTags
                    && matches |> List.exists passesWithoutRenderedArtifact
                then
                    "invalid", [ "evidence.missingVisualInspectionArtifact" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        normalizedEvidenceResult declaration.Result = "pass" && declaration.Synthetic)
                then
                    "synthetic", []
                elif
                    matches
                    |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass")
                then
                    "supported", []
                elif
                    matches
                    |> List.exists (fun declaration ->
                        normalizedEvidenceResult declaration.Result = "deferred"
                        || declaration.Kind = EvidenceKind.Deferral)
                then
                    "deferred", []
                elif
                    matches
                    |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "stale")
                then
                    "stale", [ "evidence.staleEvidence" ]
                elif
                    matches
                    |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "advisory")
                then
                    "advisory", []
                else
                    "blocking", [ "evidence.missingRequiredEvidence" ]

            ({ ObligationId = obligation.ObligationId
               State = state
               EvidenceIds =
                 matches
                 |> List.map (fun declaration -> declaration.Id.Value)
                 |> List.distinct
                 |> List.sort
               TaskIds = obligation.LinkedTaskIds |> List.map _.Value |> List.sort
               DiagnosticIds = diagnostics |> List.distinct |> List.sort }
            : EvidenceDispositionDraft))

    let evidenceDispositionDiagnostics path (dispositions: EvidenceDispositionDraft list) =
        let idsFor state =
            dispositions
            |> List.filter (fun disposition -> disposition.State = state)
            |> List.map _.ObligationId
            |> List.distinct
            |> List.sort

        [ let missing = idsFor "missing"

          if not (List.isEmpty missing) then
              missingRequiredEvidence path missing

          let stale = idsFor "stale"

          if not (List.isEmpty stale) then
              staleEvidence path stale ]

    let evidenceSummary
        workId
        (artifact: EvidenceArtifact)
        (dispositions: EvidenceDispositionDraft list)
        : EvidenceSummary =
        let count state =
            dispositions
            |> List.filter (fun disposition -> disposition.State = state)
            |> List.length

        let blockingCount = count "missing" + count "invalid" + count "blocking"
        let warningCount = count "stale"

        let readiness =
            if blockingCount > 0 then "needsEvidenceCorrection"
            elif warningCount > 0 then "needsEvidenceReview"
            else "evidenceReady"

        { WorkId = workId
          Stage = "evidence"
          Status = readiness
          EvidencePath = evidencePath workId
          DeclarationIds =
            artifact.Evidence
            |> List.map (fun declaration -> declaration.Id.Value)
            |> List.distinct
            |> List.sort
          DeclarationCount = artifact.Evidence.Length
          ObligationCount = dispositions.Length
          SupportedCount = count "supported"
          DeferredCount = count "deferred"
          MissingCount = count "missing"
          StaleCount = count "stale"
          SyntheticCount = count "synthetic"
          InvalidCount = count "invalid"
          AdvisoryCount = count "advisory"
          BlockingCount = blockingCount
          SourceSnapshotCount = artifact.SourceSnapshots.Length
          Readiness = readiness }

    let renderEvidenceSourceSnapshot (snapshot: EvidenceSourceSnapshot) =
        // Absence is absence (FS.GG.SDD#182): an unset digest omits its line rather than
        // emitting a trailing-whitespace `digest: `, and an unset schemaVersion omits its
        // line rather than inventing `1` for a source that declared none. Same
        // omit-when-`None` convention as `renderOptionalScalar` below.
        let optionalFields =
            [ snapshot.Digest |> Option.map (fun digest -> $"    digest: {digest}")
              snapshot.SchemaVersion
              |> Option.map (fun schema -> $"    schemaVersion: {schema}") ]
            |> List.choose (Option.map (fun line -> "\n" + line))
            |> String.concat ""

        $"""  - label: {snapshot.Label}
    path: {snapshot.Path}{optionalFields}"""

    // The authored artifact tail — the `evidence` block sequence and `lifecycleNotes` — is now codec-
    // driven too (FS.GG.SDD#260): `id` is the first `declarationFields` key, so `recordList` frames
    // each declaration as `  - id: …`; an empty list renders `evidence: []`. The front matter and
    // `sourceSnapshots` above it stay tool-owned (canonical / recomputed), so they are hand-rendered.
    let private evidenceArtifactAuthoredFields: ArtifactCodec.FieldCodec<EvidenceArtifact> list =
        [ ArtifactCodec.recordList
              "evidence"
              EvidenceCodec.declarationFields
              EvidenceCodec.declarationSeed
              (fun artifact -> artifact.Evidence |> List.sortBy (fun declaration -> declaration.Id.Value))
              (fun value artifact -> { artifact with Evidence = value })
          ArtifactCodec.scalarBlock
              "lifecycleNotes"
              // An empty authored list seeds the canonical next-action note (unchanged behaviour).
              (fun artifact ->
                  if List.isEmpty artifact.LifecycleNotes then
                      [ defaultEvidenceLifecycleNote ]
                  else
                      artifact.LifecycleNotes)
              (fun value artifact -> { artifact with LifecycleNotes = value }) ]

    let evidenceArtifactText workId (artifact: EvidenceArtifact) (summary: EvidenceSummary) =
        let sourceSnapshots =
            match artifact.SourceSnapshots with
            | [] -> "sourceSnapshots: []"
            | snapshots ->
                snapshots
                |> List.sortBy (fun snapshot -> snapshot.Path, snapshot.Label)
                |> List.map renderEvidenceSourceSnapshot
                |> String.concat "\n"
                |> fun text -> $"sourceSnapshots:\n{text}"

        $"""schemaVersion: 1
workId: {workId}
stage: evidence
status: {summary.Readiness}
sourceSpec: {specPath workId}
sourceClarifications: {clarificationPath workId}
sourceChecklist: {checklistPath workId}
sourcePlan: {planPath workId}
sourceTasks: {tasksPath workId}
sourceAnalysis: {analysisPath workId}
{sourceSnapshots}
{ArtifactCodec.render evidenceArtifactAuthoredFields artifact}
"""

    let computeEvidencePlan model =
        let ((specification, clarification, checklist, plan, tasks, analysis, evidenceSummary),
             diagnostics,
             generatedViews,
             effects) =
            runHandler model (None, None, None, None, None, None, None) (fun workId ->
                let projectDiagnostics = projectDiagnostics model
                let duplicateDiagnostics = duplicateWorkIdDiagnostics workId model
                let prereqs = resolvePrerequisites workId model

                let specificationDiagnostics, specText, specification, specFacts =
                    prereqs.SpecificationDiagnostics,
                    prereqs.SpecificationText,
                    prereqs.Specification,
                    prereqs.SpecificationFacts

                let clarificationDiagnostics, clarificationText, clarification, clarificationFacts =
                    prereqs.ClarificationDiagnostics,
                    prereqs.ClarificationText,
                    prereqs.Clarification,
                    prereqs.ClarificationFacts

                let checklistDiagnostics, checklistText, checklist, checklistFacts =
                    prereqs.ChecklistDiagnostics, prereqs.ChecklistText, prereqs.Checklist, prereqs.ChecklistFacts

                let planDiagnostics, planText, plan, planFacts =
                    prereqs.PlanDiagnostics, prereqs.PlanText, prereqs.Plan, prereqs.PlanFacts

                let taskDiagnostics, taskText, tasks, taskFacts =
                    prereqs.TaskDiagnostics, prereqs.TaskText, prereqs.Tasks, prereqs.TaskFacts

                let analysisDiagnostics, analysisText, analysis =
                    analysisPrerequisiteDiagnosticsSummaryAndText workId model

                let existingArtifact, existingDiagnostics, _ = parseExistingEvidence workId model
                let inputArtifact, inputDiagnostics = parseInputEvidence workId model.Request

                let evidenceArtifact, mergeDiagnostics, evidenceText, evidenceSummary =
                    match
                        specText,
                        clarificationText,
                        checklistText,
                        planText,
                        taskText,
                        analysisText,
                        specFacts,
                        clarificationFacts,
                        checklistFacts,
                        planFacts,
                        taskFacts
                    with
                    | Some specText,
                      Some clarificationText,
                      Some checklistText,
                      Some planText,
                      Some taskText,
                      Some analysisText,
                      Some specFacts,
                      Some clarificationFacts,
                      Some checklistFacts,
                      Some planFacts,
                      Some taskFacts ->
                        let currentSnapshots =
                            currentEvidenceSourceSnapshots
                                workId
                                specText
                                clarificationText
                                checklistText
                                planText
                                taskText
                                analysisText

                        let obligations = evidenceObligations taskFacts

                        let merged, mergeDiagnostics =
                            mergeEvidenceArtifacts
                                workId
                                model.Request.FromTests
                                existingArtifact
                                inputArtifact
                                obligations

                        // `artifact` re-stamps the snapshots to the sources as they are now; it is what
                        // gets written back. Validation must see `merged` — the artifact as recorded on
                        // disk — because `evidenceSourceSnapshotStale` compares the recorded digests
                        // against `currentSnapshots`. Passing `artifact` compares `currentSnapshots`
                        // against itself and the staleEvidenceSource branch is dead (#216). The two
                        // differ only in `SourceSnapshots`, so every other check is unaffected.
                        let artifact =
                            { merged with
                                SourceSnapshots = currentSnapshots }

                        let validationDiagnostics =
                            evidenceValidationDiagnostics
                                workId
                                specFacts
                                clarificationFacts
                                checklistFacts
                                planFacts
                                taskFacts
                                currentSnapshots
                                merged

                        let dispositions = evidenceDispositions obligations artifact

                        let dispositionDiagnostics =
                            evidenceDispositionDiagnostics (evidencePath workId) dispositions

                        let summary = evidenceSummary workId artifact dispositions
                        let text = evidenceArtifactText workId artifact summary

                        Some artifact,
                        mergeDiagnostics @ validationDiagnostics @ dispositionDiagnostics,
                        Some text,
                        Some summary
                    | _ -> None, [], None, None

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    @ taskDiagnostics
                    @ analysisDiagnostics
                    @ existingDiagnostics
                    @ inputDiagnostics
                    @ mergeDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, generatedView, generatedEffects =
                    match specText, clarificationText, checklistText, planText, taskText with
                    | Some specText, Some clarificationText, Some checklistText, Some planText, Some taskText ->
                        let charterText = snapshot (charterPath workId) model |> Option.map _.Text

                        generatedViewPlan
                            model.Request
                            workId
                            charterText
                            (Some specText)
                            (Some clarificationText)
                            (Some checklistText)
                            (Some planText)
                            (Some taskText)
                            evidenceText
                            commandDiagnostics
                            model
                    | _ -> blockedWorkModelPlan workId commandDiagnostics model.Request.GeneratorVersion

                let evidenceEffects =
                    match evidenceText with
                    | Some text ->
                        [ CreateDirectory($"work/{workId}")
                          WriteFile(evidencePath workId, text, HybridArtifact MergePolicies.evidence) ]
                    | None -> []

                commandDiagnostics @ generatedDiagnostics,
                (fun _ _ ->
                    (specification, clarification, checklist, plan, tasks, analysis, evidenceSummary),
                    [ generatedView ],
                    evidenceEffects,
                    generatedEffects))

        diagnostics,
        specification,
        clarification,
        checklist,
        plan,
        tasks,
        analysis,
        evidenceSummary,
        generatedViews,
        effects

// ---- Verify command ----
