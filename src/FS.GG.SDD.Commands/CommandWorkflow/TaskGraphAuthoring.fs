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

module internal TaskGraphAuthoring =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module IdentifiersModule = FS.GG.SDD.Artifacts.Identifiers
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    // The required test skill for verification-obligation tasks is framework-aware.
    // SDD keeps no closed allow-list: a declared framework is trusted and normalized
    // (trim -> invariant-culture lowercase -> collapse internal whitespace runs to `-`),
    // and an absent/blank declaration degrades to a neutral, non-misleading skill.
    let neutralTestSkill = "automated-tests"

    let resolveTestSkill (declared: string option) : string =
        match declared with
        | Some raw when not (String.IsNullOrWhiteSpace raw) ->
            let lowered = raw.Trim().ToLowerInvariant()
            Regex.Replace(lowered, @"\s+", "-")
        | _ -> neutralTestSkill

    let tasksSummary (facts: TaskFacts) : TasksSummary =
        let statusCount predicate =
            facts.Tasks |> List.filter (fun task -> predicate task.Status) |> List.length

        { WorkId = facts.FrontMatter.WorkId.Value
          Stage = IdentifiersModule.stageValue facts.FrontMatter.Stage
          Status = facts.FrontMatter.Status
          SourceSpec = facts.FrontMatter.SourceSpec
          SourceClarifications = facts.FrontMatter.SourceClarifications
          SourceChecklist = facts.FrontMatter.SourceChecklist
          SourcePlan = facts.FrontMatter.SourcePlan
          TaskIds = facts.Tasks |> List.map (fun task -> task.Id.Value) |> List.sort
          DependencyCount = facts.Tasks |> List.sumBy (fun task -> task.Dependencies.Length)
          RequiredSkillCount =
            facts.Tasks
            |> List.collect (fun task -> task.RequiredSkills)
            |> List.distinct
            |> List.length
          RequiredEvidenceCount =
            facts.Tasks
            |> List.collect (fun task -> task.RequiredEvidence |> List.map _.Value)
            |> List.distinct
            |> List.length
          PendingCount = statusCount ((=) TaskStatus.Pending)
          InProgressCount = statusCount ((=) TaskStatus.InProgress)
          DoneCount = statusCount ((=) TaskStatus.Done)
          SkippedCount =
            facts.Tasks
            |> List.filter (fun task ->
                match task.Status with
                | TaskStatus.Skipped _ -> true
                | _ -> false)
            |> List.length
          StaleCount = facts.StaleTaskCount
          AcceptedDeferralCount = facts.AcceptedDeferrals.Length
          BlockingFindingCount =
            facts.Findings
            |> List.filter (fun finding -> finding.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            |> List.length
          AdvisoryCount = facts.AdvisoryNotes.Length }

    let parseTasksForCommand path text : Result<TaskFacts * Diagnostic list, Diagnostic list> =
        let snapshot = { Path = path; Text = text }

        match parseTaskFacts snapshot with
        | Error diagnostics ->
            Error(
                diagnostics
                |> List.map (fun diagnostic -> malformedTasksArtifact path diagnostic.Message)
            )
        | Ok facts ->
            let diagnostics =
                facts.Diagnostics
                |> List.map (fun diagnostic ->
                    match diagnostic.Id, diagnostic.RelatedIds with
                    | "workModelInconsistent", id :: _ when id.StartsWith("T", StringComparison.OrdinalIgnoreCase) ->
                        duplicateTaskId path id
                    | "duplicateIdentifier", id :: _ -> duplicateTaskId path id
                    | _ -> diagnostic)

            Ok(facts, diagnostics)

    let yamlString (value: string) =
        let text = if String.IsNullOrEmpty value then "" else value
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    let yamlInlineList (values: string list) =
        match values |> List.distinct |> List.sort with
        | [] -> "[]"
        | values -> values |> List.map yamlString |> String.concat ", " |> (fun text -> $"[{text}]")

    let taskStatusYaml (status: TaskStatus) =
        match status with
        | TaskStatus.Pending -> "pending"
        | TaskStatus.InProgress -> "in-progress"
        | TaskStatus.Done -> "done"
        | TaskStatus.Skipped _ -> "skipped"
        | TaskStatus.Stale -> "stale"

    let taskEvidenceId index =
        let candidate = sprintf "EV%03d" index

        match IdentifiersModule.createEvidenceId candidate with
        | Ok id -> id
        | Error message ->
            failwithf "taskEvidenceId: invariant violated — constructed evidence id %s rejected: %s" candidate message

    let taskArtifactRef workId =
        let path = tasksPath workId

        match FS.GG.SDD.Artifacts.ArtifactRef.create path ArtifactKind.Tasks ArtifactOwner.Sdd true with
        | Ok artifact -> artifact
        | Error message ->
            failwithf "taskArtifactRef: invariant violated — tasks artifact path %s rejected: %s" path message

    let taskId index =
        let candidate = sprintf "T%03d" index

        match IdentifiersModule.createTaskId candidate with
        | Ok id -> id
        | Error message ->
            failwithf "taskId: invariant violated — constructed task id %s rejected: %s" candidate message

    let taskIdNumber (id: TaskId) =
        let digits = Regex.Match(id.Value, @"\d+").Value

        match Int32.TryParse digits with
        | true, value -> value
        | _ -> 0

    let nextTaskNumber (existing: WorkTask list) =
        existing
        |> List.map (fun task -> taskIdNumber task.Id)
        |> List.fold max 0
        |> (+) 1

    let plannedTask
        (workId: string)
        (sourceIds: string list)
        (title: string)
        (requirements: RequirementId list)
        (decisions: DecisionId list)
        (dependencies: TaskId list)
        (skills: string list)
        evidenceIndex
        idIndex
        : WorkTask =
        { Id = taskId idIndex
          Title = title
          Status = TaskStatus.Pending
          Owner = "sdd"
          Dependencies = dependencies
          Requirements = requirements
          Decisions = decisions
          SourceIds = sourceIds |> List.distinct |> List.sort
          RequiredSkills = skills |> List.distinct |> List.sort
          RequiredEvidence = [ taskEvidenceId evidenceIndex ]
          Source = taskArtifactRef workId
          SourceLocation = None }

    let plannedTasks
        (declaredTestFramework: string option)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (existingFacts: TaskFacts option)
        =
        let existingSources =
            existingFacts
            |> Option.map (fun facts -> facts.Tasks |> List.collect (fun task -> task.SourceIds) |> Set.ofList)
            |> Option.defaultValue Set.empty

        let existingTasks: WorkTask list =
            existingFacts
            |> Option.map (fun (facts: TaskFacts) -> facts.Tasks)
            |> Option.defaultValue []

        let mutable nextId = nextTaskNumber existingTasks
        let mutable evidenceIndex = nextId

        let allocate
            (sourceIds: string list)
            (title: string)
            (requirements: RequirementId list)
            (decisions: DecisionId list)
            (dependencies: TaskId list)
            (skills: string list)
            : WorkTask =
            let id = nextId
            nextId <- nextId + 1
            let evidence = evidenceIndex
            evidenceIndex <- evidenceIndex + 1

            plannedTask
                planFacts.FrontMatter.WorkId.Value
                sourceIds
                title
                requirements
                decisions
                dependencies
                skills
                evidence
                id

        let maybeTask
            (sourceIds: string list)
            (title: string)
            (requirements: RequirementId list)
            (decisions: DecisionId list)
            (dependencies: TaskId list)
            (skills: string list)
            : WorkTask option =
            let key = sourceIds |> List.tryHead

            match key with
            | Some key when Set.contains key existingSources -> None
            | _ -> Some(allocate sourceIds title requirements decisions dependencies skills)

        let requirementTasks: WorkTask list =
            specFacts.RequirementIds
            |> List.choose (fun requirement ->
                let acceptance =
                    specFacts.RequirementReferences
                    |> List.filter (fun reference -> reference.RequirementId.Value = requirement.Value)
                    |> List.collect (fun reference -> reference.AcceptanceScenarioIds |> List.map _.Value)
                    |> List.distinct
                    |> List.sort

                maybeTask
                    (requirement.Value :: acceptance)
                    $"Implement requirement {requirement.Value}"
                    [ requirement ]
                    []
                    []
                    [ "fsharp"; "speckit-implement" ])

        let primaryDependency: TaskId list =
            existingTasks
            |> List.tryHead
            |> Option.map (fun task -> [ task.Id ])
            |> Option.orElseWith (fun () -> requirementTasks |> List.tryHead |> Option.map (fun task -> [ task.Id ]))
            |> Option.defaultValue []

        // Every RESOLVED clarify decision must be disposed by a task (analyze's `required`
        // set demands it). Deferrals get their own task below; a resolved DEC-### previously
        // got none, so `analyze` blocked two stages downstream with `missingDisposition`
        // exactly when `clarify` did its job (#162). Route each to its own disposing task,
        // carrying the id in the typed `decisions:` field and `sourceIds` (mirrors how a
        // requirement task carries its RequirementId in both).
        let clarificationDecisionTasks =
            clarificationFacts.Decisions
            |> List.choose (fun decision ->
                // Deliberately no requirement refs (#164). Threading the decision line's FR ids in here
                // was tried and dropped: `requirementIdsInLine` scans the whole line, prose included, so
                // `DEC-001: supersedes the old FR-099 behaviour` put FR-099 into the task's `requirements:`.
                // The same exploration also unioned `requirements:` into `SourceIds` in the parser, which
                // would have made `unknownTaskSourceReference` block `tasks` on output the tool had just
                // generated — telling the author to fix tasks.yml, a file it regenerates.
                //
                // Feature 096 (issue #189): that parser union was never adopted — `Task.fs`'s parser was
                // tried and deferred, not merged, so it reads `sourceIds` verbatim; `unknownSources` gates
                // the authored `sourceIds:` alone and a prose-scraped FR in `requirements:` cannot leak in.
                // The conclusion is unchanged and the stakes are higher: a prose mention is not a structured
                // reference, and consumers now union
                // all three reference fields (`WorkModel.deriveGuidanceModel`,
                // `HandlersVerify.verifyEvidenceDispositionViews`), so a scraped FR-099 would leak into the
                // generated agent guidance and into `verify.json`'s `affectedSourceIds`. The decision's refs
                // reach traceability through `work-model.json`'s `requirementRefs`/`storyRefs`/
                // `acceptanceRefs` instead, which is what "thread all refs through traceability" asked for.
                maybeTask
                    [ decision.DecisionId.Value ]
                    $"Implement clarification decision {decision.DecisionId.Value}"
                    []
                    [ decision.DecisionId ]
                    primaryDependency
                    [ "fsharp"; "speckit-implement" ])

        let planDecisionTasks =
            planFacts.Decisions
            |> List.choose (fun decision ->
                maybeTask
                    (decision.DecisionId.Value :: decision.SourceIds)
                    $"Implement plan decision {decision.DecisionId.Value}"
                    []
                    []
                    primaryDependency
                    [ "fsharp"; "speckit-implement" ])

        let contractTasks =
            planFacts.ContractReferences
            |> List.choose (fun contract ->
                maybeTask
                    (contract.ContractId.Value :: contract.SourceIds)
                    $"Update contract surface {contract.ContractId.Value}"
                    []
                    []
                    primaryDependency
                    [ "fsharp" ])

        let obligationTasks =
            planFacts.VerificationObligations
            |> List.choose (fun obligation ->
                maybeTask
                    (obligation.ObligationId.Value :: obligation.SourceIds)
                    $"Record verification evidence {obligation.ObligationId.Value}"
                    []
                    []
                    primaryDependency
                    [ resolveTestSkill declaredTestFramework; "readiness-evidence" ])

        let migrationTasks =
            planFacts.MigrationNotes
            |> List.choose (fun migration ->
                maybeTask
                    (migration.MigrationId.Value :: migration.SourceIds)
                    $"Handle migration posture {migration.MigrationId.Value}"
                    []
                    []
                    primaryDependency
                    [ "schema-versioning" ])

        let generatedViewTasks =
            planFacts.GeneratedViewImpacts
            |> List.choose (fun impact ->
                maybeTask
                    (impact.ImpactId.Value :: impact.SourceIds)
                    $"Refresh generated view impact {impact.ImpactId.Value}"
                    []
                    []
                    primaryDependency
                    [ "deterministic-json" ])

        let deferralTasks =
            [ clarificationFacts.AcceptedDeferrals
              |> List.map (fun deferral -> deferral.DecisionId.Value)
              checklistFacts.AcceptedDeferrals
              |> List.map (fun result -> result.ResultId.Value)
              planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
            |> List.concat
            |> List.distinct
            |> List.choose (fun id ->
                maybeTask [ id ] $"Keep accepted deferral {id} visible" [] [] primaryDependency [ "traceability" ])

        requirementTasks
        @ clarificationDecisionTasks
        @ planDecisionTasks
        @ contractTasks
        @ obligationTasks
        @ migrationTasks
        @ generatedViewTasks
        @ deferralTasks

    let currentTaskSourceDigests workId specText clarificationText checklistText planText =
        [ "spec", specPath workId, specText
          "clarifications", clarificationPath workId, clarificationText
          "checklist", checklistPath workId, checklistText
          "plan", planPath workId, planText ]
        |> List.map (fun (label, path, text) -> label, path, (SchemaVersionModule.sha256Text text).Value)

    // §082 (#147): merge the re-derived graph with the prior file. `tasks.yml` legitimately
    // MIXES tool-derived tasks with hand-authored ones (custom titles, requirements/decisions),
    // so the merge is four-way, keyed on the deterministic task Title (SourceIds are stored
    // sorted, so the title is the stable identity for a derived task):
    //   1. Matched (derived title == a prior title): the derived task inherits the prior task's
    //      authored `status`/`owner`, its stable `T###` id, and its still-LIVE authored
    //      disposition refs (requirements/decisions/sourceIds unioned with the derived set).
    //   2. New (derived, no prior match): a fresh id above every prior id (deps remapped, since
    //      `plannedTasks` embeds fresh ids in `Dependencies`).
    //   3. Kept authored (prior, no derived match, still carries a LIVE disposition ref): an
    //      author-written task — preserved verbatim, its refs filtered to live ids.
    //   4. Dropped (prior, no derived match, no live ref): a genuine orphan whose source is gone.
    // A retired `Stale` status is never carried (it was a tool signal, not authored state).
    let mergeAuthoredTaskState (liveIds: Set<string>) (prior: WorkTask list) (derived: WorkTask list) : WorkTask list =
        let priorByTitle = prior |> List.map (fun task -> task.Title, task) |> Map.ofList
        let derivedTitles = derived |> List.map (fun task -> task.Title) |> Set.ofList

        let isLive (value: string) =
            Set.contains (value.ToUpperInvariant()) liveIds

        let mutable nextNew = nextTaskNumber prior
        let mutable claimed = Set.empty

        // Assign each derived task its final id, claiming each prior id at most once so two
        // derived tasks that share a Title (e.g. a duplicated plan decision) can never both
        // inherit the same `T###`. Record the matched prior (if any) for the merge below.
        let assigned =
            derived
            |> List.map (fun task ->
                match Map.tryFind task.Title priorByTitle with
                | Some priorTask when not (Set.contains priorTask.Id.Value claimed) ->
                    claimed <- Set.add priorTask.Id.Value claimed
                    task.Id.Value, priorTask.Id, Some priorTask, task
                | _ ->
                    let id = taskId nextNew
                    nextNew <- nextNew + 1
                    task.Id.Value, id, None, task)

        let remap =
            assigned
            |> List.map (fun (freshId, finalId, _, _) -> freshId, finalId)
            |> Map.ofList

        let mergedDerived =
            assigned
            |> List.map (fun (_, finalId, matchedPrior, task) ->
                let dependencies =
                    task.Dependencies
                    |> List.map (fun dep -> Map.tryFind dep.Value remap |> Option.defaultValue dep)

                // Evidence obligations mirror the id (EV### tracks T###), so bind them to the
                // FINAL id — a matched task keeps its prior EV### alongside its prior T###, and a
                // new task's EV### is as unique as its id (no shift when derivation order changes).
                let requiredEvidence = [ taskEvidenceId (taskIdNumber finalId) ]

                match matchedPrior with
                | Some priorTask ->
                    let carriedStatus =
                        match priorTask.Status with
                        | TaskStatus.Stale -> task.Status
                        | authored -> authored

                    let requirements =
                        task.Requirements
                        @ (priorTask.Requirements |> List.filter (fun id -> isLive id.Value))
                        |> List.distinctBy (fun id -> id.Value)

                    let decisions =
                        task.Decisions
                        @ (priorTask.Decisions |> List.filter (fun id -> isLive id.Value))
                        |> List.distinctBy (fun id -> id.Value)

                    let sourceIds =
                        task.SourceIds @ (priorTask.SourceIds |> List.filter isLive)
                        |> List.distinct
                        |> List.sort

                    { task with
                        Id = finalId
                        Dependencies = dependencies
                        RequiredEvidence = requiredEvidence
                        Status = carriedStatus
                        Owner = priorTask.Owner
                        Requirements = requirements
                        Decisions = decisions
                        SourceIds = sourceIds }
                | None ->
                    { task with
                        Id = finalId
                        Dependencies = dependencies
                        RequiredEvidence = requiredEvidence })

        // The dispositions the re-derived graph already covers. An unmatched prior task is kept
        // only if it UNIQUELY covers a live disposition the derived graph misses (an authored
        // task the tool can't derive, e.g. a hand-authored `decisions: [DEC-###]`); a task whose
        // only live refs are already covered by derivation is a redundant orphan and is dropped.
        let derivedCoverage =
            mergedDerived
            |> List.collect (fun task ->
                (task.Requirements |> List.map (fun id -> id.Value))
                @ (task.Decisions |> List.map (fun id -> id.Value))
                @ task.SourceIds)
            |> List.map (fun value -> value.ToUpperInvariant())
            |> Set.ofList

        let keptAuthored =
            prior
            |> List.filter (fun priorTask -> not (Set.contains priorTask.Title derivedTitles))
            |> List.choose (fun priorTask ->
                let requirements = priorTask.Requirements |> List.filter (fun id -> isLive id.Value)
                let decisions = priorTask.Decisions |> List.filter (fun id -> isLive id.Value)
                let sourceIds = priorTask.SourceIds |> List.filter isLive

                let coversUnmet =
                    (requirements |> List.map (fun id -> id.Value))
                    @ (decisions |> List.map (fun id -> id.Value))
                    @ sourceIds
                    |> List.exists (fun value -> not (Set.contains (value.ToUpperInvariant()) derivedCoverage))

                if not coversUnmet then
                    None
                else
                    let carriedStatus =
                        match priorTask.Status with
                        | TaskStatus.Stale -> TaskStatus.Pending
                        | authored -> authored

                    Some
                        { priorTask with
                            Requirements = requirements
                            Decisions = decisions
                            SourceIds = sourceIds
                            Status = carriedStatus })

        let merged = mergedDerived @ keptAuthored

        // Prune any dependency that points at a task the merge dropped — a kept authored task
        // may still reference a now-orphaned prior task, and a dangling ref would fail the task
        // graph's `unknownTaskDependency` validation on an otherwise-clean re-run.
        let survivingIds = merged |> List.map (fun task -> task.Id.Value) |> Set.ofList

        merged
        |> List.map (fun task ->
            { task with
                Dependencies =
                    task.Dependencies
                    |> List.filter (fun dep -> Set.contains dep.Value survivingIds) })

    let taskFrontMatterText request workId (existingFrontMatter: TaskFrontMatter option) =
        // The authored `title` and `publicOrToolFacingImpact` round-trip on a re-run
        // (FS.GG.SDD#181): a prior file's values win, and only a freshly-created file falls back to
        // the request-derived title and the `true` default. Previously both were unconditionally
        // regenerated, so a re-run reverted a custom title to the humanized id and flipped
        // `publicOrToolFacingImpact: false` back to `true`.
        let title =
            existingFrontMatter
            |> Option.map (fun front -> front.Title)
            |> Option.defaultValue (requestTitle request workId)

        let publicOrToolFacingImpact =
            existingFrontMatter
            |> Option.bind (fun front -> front.PublicOrToolFacingImpact)
            |> Option.defaultValue true

        $"""schemaVersion: 1
work:
  id: {workId}
  title: {yamlString title}
  stage: tasks
  status: tasksReady
  sourceSpec: {specPath workId}
  sourceClarifications: {clarificationPath workId}
  sourceChecklist: {checklistPath workId}
  sourcePlan: {planPath workId}
  publicOrToolFacingImpact: {if publicOrToolFacingImpact then "true" else "false"}
"""

    let renderTaskSourceSnapshots workId specText clarificationText checklistText planText =
        currentTaskSourceDigests workId specText clarificationText checklistText planText
        |> List.map (fun (label, path, digest) ->
            $"""  - label: {label}
    path: {path}
    digest: {digest}
    schemaVersion: 1""")
        |> String.concat "\n"

    let renderTask (task: WorkTask) =
        let skip =
            match task.Status with
            | TaskStatus.Skipped rationale -> $"\n    skipRationale: {yamlString rationale}"
            | _ -> ""

        let dependencyIds = task.Dependencies |> List.map (fun (id: TaskId) -> id.Value)

        let requirementIds =
            task.Requirements |> List.map (fun (id: RequirementId) -> id.Value)

        let decisionIds = task.Decisions |> List.map (fun (id: DecisionId) -> id.Value)

        let evidenceIds =
            task.RequiredEvidence |> List.map (fun (id: EvidenceId) -> id.Value)

        $"""  - id: {task.Id.Value}
    title: {yamlString task.Title}
    status: {taskStatusYaml task.Status}
    owner: {yamlString task.Owner}
    dependencies: {dependencyIds |> yamlInlineList}
    requirements: {requirementIds |> yamlInlineList}
    decisions: {decisionIds |> yamlInlineList}
    sourceIds: {task.SourceIds |> yamlInlineList}
    requiredSkills: {task.RequiredSkills |> yamlInlineList}
    requiredEvidence: {evidenceIds |> yamlInlineList}{skip}"""

    let renderFindingsBlock (findings: TaskGraphFinding list) =
        match findings with
        | [] -> "findings: []"
        | findings ->
            let lines =
                findings
                |> List.map (fun (finding: TaskGraphFinding) ->
                    $"""  - id: {finding.FindingId}
    severity: {finding.Severity}
    text: {yamlString finding.Text}
    sourceIds: {finding.SourceIds |> yamlInlineList}""")
                |> String.concat "\n"

            $"findings:\n{lines}"

    let renderScalarBlock (name: string) (values: string list) =
        match values |> List.distinct |> List.sort with
        | [] -> $"{name}: []"
        | values ->
            let lines =
                values
                |> List.map (fun value -> $"  - {yamlString value}")
                |> String.concat "\n"

            $"{name}:\n{lines}"

    let tasksArtifactText
        (request: CommandRequest)
        (workId: string)
        (existingFrontMatter: TaskFrontMatter option)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (tasks: WorkTask list)
        (acceptedDeferrals: string list)
        (findings: TaskGraphFinding list)
        (advisoryNotes: string list)
        (lifecycleNotes: string list)
        =
        let tasksBlock =
            match tasks with
            | [] -> "tasks: []"
            | tasks ->
                let taskLines =
                    tasks
                    |> List.sortBy (fun task -> task.Id.Value)
                    |> List.map renderTask
                    |> String.concat "\n"

                $"tasks:\n{taskLines}"

        let lifecycle =
            if List.isEmpty lifecycleNotes then
                [ $"Next lifecycle action: fsgg-sdd analyze --work {workId}." ]
            else
                lifecycleNotes

        $"""{taskFrontMatterText request workId existingFrontMatter}
sources:
{renderTaskSourceSnapshots workId specText clarificationText checklistText planText}
{tasksBlock}
{renderScalarBlock "acceptedDeferrals" acceptedDeferrals}
{renderFindingsBlock findings}
{renderScalarBlock "advisoryNotes" advisoryNotes}
{renderScalarBlock "lifecycleNotes" lifecycle}
"""

    let knownTaskSourceIds
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        =
        [ specFacts.RequirementIds |> List.map (fun id -> id.Value)
          specFacts.UserStoryIds |> List.map (fun id -> id.Value)
          specFacts.AcceptanceScenarioIds |> List.map (fun id -> id.Value)
          specFacts.ScopeBoundaryIds |> List.map (fun id -> id.Value)
          specFacts.AmbiguityIds |> List.map (fun id -> id.Value)
          clarificationFacts.Questions
          |> List.map (fun (question: ClarificationQuestion) -> question.QuestionId.Value)
          clarificationFacts.Decisions
          |> List.map (fun (decision: ClarificationDecisionFact) -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals
          |> List.map (fun (decision: ClarificationDecisionFact) -> decision.DecisionId.Value)
          checklistFacts.Items
          |> List.map (fun (item: ChecklistItem) -> item.ItemId.Value)
          checklistFacts.Results
          |> List.map (fun (result: ChecklistReviewResult) -> result.ResultId.Value)
          planFacts.Decisions
          |> List.map (fun (decision: PlanDecision) -> decision.DecisionId.Value)
          planFacts.ContractReferences
          |> List.map (fun (reference: PlanContractReference) -> reference.ContractId.Value)
          planFacts.VerificationObligations
          |> List.map (fun (obligation: VerificationObligation) -> obligation.ObligationId.Value)
          planFacts.MigrationNotes
          |> List.map (fun (migration: PlanMigrationNote) -> migration.MigrationId.Value)
          planFacts.GeneratedViewImpacts
          |> List.map (fun (impact: GeneratedViewImpact) -> impact.ImpactId.Value)
          planFacts.AcceptedDeferrals
          |> List.map (fun (deferral: AcceptedPlanDeferral) -> deferral.Id) ]
        |> List.concat
        |> Set.ofList

    let taskDependencyCycleDiagnostics path (tasks: WorkTask list) =
        let dependencyMap =
            tasks
            |> List.map (fun task -> task.Id.Value, (task.Dependencies |> List.map (fun (id: TaskId) -> id.Value)))
            |> Map.ofList

        let rec visit trail id =
            if List.contains id trail then
                Some(List.rev (id :: trail))
            else
                dependencyMap
                |> Map.tryFind id
                |> Option.defaultValue []
                |> List.tryPick (visit (id :: trail))

        dependencyMap
        |> Map.toList
        |> List.choose (fun (id, _) -> visit [] id)
        |> List.distinct
        |> List.map (taskDependencyCycle path)

    // The single universe of lifecycle-fact ids the current sources require a task to
    // dispose. `analyze`'s completeness check, the `tasks` re-derive `liveIds` filter, and
    // the `tasks`-stage fail-fast disposition check ALL read from this one list so the two
    // formerly-duplicated copies can never drift apart again (the drift that let a resolved
    // clarify decision be `required` by analyze yet never derived by tasks — #162).
    let requiredDispositionIds
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        : string list =
        [ specFacts.RequirementIds |> List.map _.Value
          specFacts.AcceptanceScenarioIds |> List.map _.Value
          clarificationFacts.Decisions
          |> List.map (fun decision -> decision.DecisionId.Value)
          clarificationFacts.AcceptedDeferrals
          |> List.map (fun decision -> decision.DecisionId.Value)
          checklistFacts.AcceptedDeferrals
          |> List.map (fun result -> result.ResultId.Value)
          planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
          planFacts.ContractReferences
          |> List.map (fun contract -> contract.ContractId.Value)
          planFacts.VerificationObligations
          |> List.map (fun obligation -> obligation.ObligationId.Value)
          planFacts.MigrationNotes
          |> List.map (fun migration -> migration.MigrationId.Value)
          planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value)
          planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
        |> List.concat

    // Every id a task graph currently disposes: a task's sourceIds, its typed
    // requirement/decision refs, plus file-level accepted deferrals. Upper-cased for the
    // case-insensitive membership test against `requiredDispositionIds`.
    let allTaskDispositionIds (facts: TaskFacts) =
        [ facts.Tasks |> List.collect (fun task -> task.SourceIds)
          facts.Tasks |> List.collect (fun task -> task.Requirements |> List.map _.Value)
          facts.Tasks |> List.collect (fun task -> task.Decisions |> List.map _.Value)
          facts.AcceptedDeferrals ]
        |> List.concat
        |> List.map (fun value -> value.ToUpperInvariant())
        |> Set.ofList

    // Required dispositions the task graph does NOT cover (sorted, distinct). Empty means the
    // graph is disposition-complete. Shared by the `tasks` fail-fast check and `analyze`.
    let missingDispositionIds
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (facts: TaskFacts)
        : string list =
        let dispositions = allTaskDispositionIds facts

        requiredDispositionIds specFacts clarificationFacts checklistFacts planFacts
        |> List.distinct
        |> List.sort
        |> List.filter (fun id -> not (Set.contains (id.ToUpperInvariant()) dispositions))

    let taskValidationDiagnostics
        (path: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (evidence: EvidenceDeclaration list)
        (facts: TaskFacts)
        : Diagnostic list =
        let knownTasks = facts.Tasks |> List.map (fun task -> task.Id.Value) |> Set.ofList

        let knownSources =
            knownTaskSourceIds specFacts clarificationFacts checklistFacts planFacts

        let evidenceTaskRefs =
            evidence
            |> List.collect (fun evidence -> evidence.TaskRefs |> List.map (fun (id: TaskId) -> id.Value))
            |> Set.ofList

        let duplicateDiagnostics =
            facts.Diagnostics
            |> List.map (fun diagnostic ->
                match diagnostic.Id, diagnostic.RelatedIds with
                | "duplicateTaskId", _ -> diagnostic
                | _, id :: _ when id.StartsWith("T", StringComparison.OrdinalIgnoreCase) -> duplicateTaskId path id
                | _ -> malformedTasksArtifact path diagnostic.Message)

        let unknownSources =
            facts.Tasks
            |> List.collect (fun task -> task.SourceIds)
            |> List.distinct
            |> List.choose (fun id ->
                if Set.contains id knownSources then
                    None
                else
                    Some(unknownTaskSourceReference path id))

        let unknownDependencies =
            facts.Tasks
            |> List.collect (fun task -> task.Dependencies |> List.map (fun (id: TaskId) -> id.Value))
            |> List.distinct
            |> List.choose (fun id ->
                if Set.contains id knownTasks then
                    None
                else
                    Some(unknownTaskDependency path id))

        let selfDependencies =
            facts.Tasks
            |> List.choose (fun task ->
                if task.Dependencies |> List.exists (fun dep -> dep.Value = task.Id.Value) then
                    Some(taskDependencyCycle path [ task.Id.Value; task.Id.Value ])
                else
                    None)

        let skippedWithoutRationale =
            facts.Tasks
            |> List.choose (fun task ->
                match task.Status with
                | TaskStatus.Skipped rationale when
                    String.IsNullOrWhiteSpace rationale || rationale = "No rationale provided."
                    ->
                    Some task.Id.Value
                | _ -> None)

        let doneMissingEvidence =
            facts.Tasks
            |> List.choose (fun task ->
                match task.Status with
                | TaskStatus.Done when not (Set.contains task.Id.Value evidenceTaskRefs) -> Some task.Id.Value
                | _ -> None)

        // Fail fast HERE, at the stage that builds the graph, not two stages later at
        // `analyze` (#162). The generator now derives a task for every required id, so a
        // freshly re-derived graph never trips this; it fires only when an authored edit to
        // tasks.yml drops a live disposition — and the diagnostic points at the concrete fix.
        let missingDispositions =
            match missingDispositionIds specFacts clarificationFacts checklistFacts planFacts facts with
            | [] -> []
            | missing -> [ missingDisposition path missing ]

        [ duplicateDiagnostics
          unknownSources
          unknownDependencies
          selfDependencies
          taskDependencyCycleDiagnostics path facts.Tasks
          missingDispositions
          if not (List.isEmpty skippedWithoutRationale) then
              [ skippedTaskMissingRationale path skippedWithoutRationale ]
          else
              []
          if not (List.isEmpty doneMissingEvidence) then
              [ doneTaskMissingEvidence path doneMissingEvidence ]
          else
              [] ]
        |> List.concat
        |> DiagnosticsModule.sort

    let parseEvidenceForCommand (workId: string) model : EvidenceDeclaration list * Diagnostic list =
        match snapshot (evidencePath workId) model with
        | None -> [], []
        | Some snapshot ->
            match parseEvidence snapshot with
            | Ok evidence -> evidence, []
            | Error diagnostics -> [], diagnostics

    let tasksDiagnosticsTextAndSummary
        (request: CommandRequest)
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        model
        =
        let path = tasksPath workId
        let evidence, evidenceDiagnostics = parseEvidenceForCommand workId model

        // The declared test framework rides the already-loaded `.fsgg/project.yml`
        // read effect; no new I/O edge. Absent/malformed config => neutral skill.
        let declaredTestFramework =
            snapshot ".fsgg/project.yml" model
            |> Option.bind (fun projectSnapshot ->
                match parseProjectConfig projectSnapshot with
                | Ok config -> config.TestFramework
                | Error _ -> None)

        match snapshot path model with
        | None ->
            let tasks =
                plannedTasks declaredTestFramework specFacts clarificationFacts checklistFacts planFacts None

            let acceptedDeferrals =
                [ clarificationFacts.AcceptedDeferrals
                  |> List.map (fun deferral -> deferral.DecisionId.Value)
                  checklistFacts.AcceptedDeferrals
                  |> List.map (fun result -> result.ResultId.Value)
                  planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
                |> List.concat
                |> List.distinct
                |> List.sort

            let advisory = [ "Optional Governance pointers remain compatibility facts only." ]

            let text =
                tasksArtifactText
                    request
                    workId
                    None
                    specText
                    clarificationText
                    checklistText
                    planText
                    tasks
                    acceptedDeferrals
                    []
                    advisory
                    []

            match parseTasksForCommand path text with
            | Error diagnostics -> diagnostics, Some text, None
            | Ok(facts, diagnostics) ->
                let validationDiagnostics =
                    taskValidationDiagnostics path specFacts clarificationFacts checklistFacts planFacts evidence facts

                diagnostics @ validationDiagnostics @ evidenceDiagnostics
                |> DiagnosticsModule.sort,
                Some text,
                Some(tasksSummary facts)
        | Some existing ->
            if existing.Text.Contains("<!-- fsgg-sdd: unsafe-overwrite -->", StringComparison.OrdinalIgnoreCase) then
                [ unsafeOverwrite path ], Some existing.Text, None
            else
                match parseTasksForCommand path existing.Text with
                | Error diagnostics -> diagnostics, Some existing.Text, None
                | Ok(existingFacts, existingDiagnostics) ->
                    let identityDiagnostics =
                        frontMatterIdentityDiagnostics
                            "Tasks"
                            LifecycleStage.Tasks
                            "tasks"
                            malformedTasksArtifact
                            tasksIdentityMismatch
                            malformedTasksArtifact
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
                                malformedTasksArtifact
                                    path
                                    $"Tasks sourceSpec '{existingFacts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceClarifications,
                                        clarificationPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedTasksArtifact
                                    path
                                    $"Tasks sourceClarifications '{existingFacts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourceChecklist,
                                        checklistPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedTasksArtifact
                                    path
                                    $"Tasks sourceChecklist '{existingFacts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'."
                            if
                                not (
                                    String.Equals(
                                        normalizeRelativePath existingFacts.FrontMatter.SourcePlan,
                                        planPath workId,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            then
                                malformedTasksArtifact
                                    path
                                    $"Tasks sourcePlan '{existingFacts.FrontMatter.SourcePlan}' does not match '{planPath workId}'." ]

                    let hasBlockingParserDiagnostics =
                        identityDiagnostics @ existingDiagnostics
                        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

                    if hasBlockingParserDiagnostics then
                        identityDiagnostics @ existingDiagnostics |> DiagnosticsModule.sort,
                        Some existing.Text,
                        Some(tasksSummary existingFacts)
                    else
                        // §082 (#147): re-derive the full task graph from current sources on
                        // EVERY run, then merge authored status/owner + the stable T### id from
                        // the prior file. A newly-added source (e.g. a plan decision disposition)
                        // therefore appears (SC-002), an orphaned task is dropped, and the run
                        // never reports stale-and-unchanged (FR-004/FR-005). Derived rows are
                        // reclaimed — prior tool-injected rows are never re-ingested (FR-002).
                        let derived =
                            plannedTasks
                                declaredTestFramework
                                specFacts
                                clarificationFacts
                                checklistFacts
                                planFacts
                                None

                        // The universe of ids the current sources can dispose (the ONE shared
                        // `requiredDispositionIds` list analyze's completeness check also reads).
                        // A prior authored task or ref is "live" iff it names one of these;
                        // anything else is a dead orphan and is dropped on re-derive.
                        let liveIds =
                            requiredDispositionIds specFacts clarificationFacts checklistFacts planFacts
                            |> List.map (fun value -> value.ToUpperInvariant())
                            |> Set.ofList

                        let mergedTasks = mergeAuthoredTaskState liveIds existingFacts.Tasks derived

                        let acceptedDeferrals =
                            [ clarificationFacts.AcceptedDeferrals
                              |> List.map (fun deferral -> deferral.DecisionId.Value)
                              checklistFacts.AcceptedDeferrals
                              |> List.map (fun result -> result.ResultId.Value)
                              planFacts.AcceptedDeferrals |> List.map (fun deferral -> deferral.Id) ]
                            |> List.concat
                            |> List.distinct
                            |> List.sort

                        // Findings are re-derived (the create path writes none); this retires the
                        // carried-forward TF-001 stale finding. Authored file-level notes persist.
                        let text =
                            tasksArtifactText
                                request
                                workId
                                (Some existingFacts.FrontMatter)
                                specText
                                clarificationText
                                checklistText
                                planText
                                mergedTasks
                                acceptedDeferrals
                                []
                                existingFacts.AdvisoryNotes
                                existingFacts.LifecycleNotes

                        match parseTasksForCommand path text with
                        | Error diagnostics -> diagnostics, Some text, None
                        | Ok(facts, proposedDiagnostics) ->
                            let validationDiagnostics =
                                taskValidationDiagnostics
                                    path
                                    specFacts
                                    clarificationFacts
                                    checklistFacts
                                    planFacts
                                    evidence
                                    facts

                            identityDiagnostics
                            @ existingDiagnostics
                            @ proposedDiagnostics
                            @ validationDiagnostics
                            @ evidenceDiagnostics
                            |> DiagnosticsModule.sort,
                            Some text,
                            Some(tasksSummary facts)

    let tasksPrerequisiteDiagnosticsTextSummaryAndFacts
        workId
        specFacts
        clarificationFacts
        checklistFacts
        planFacts
        model
        =
        let path = tasksPath workId
        let evidence, evidenceDiagnostics = parseEvidenceForCommand workId model

        match snapshot path model with
        | None -> [ missingTasksPrerequisite path $"Tasks prerequisite '{path}' is missing." ], None, None, None
        | Some existing ->
            match parseTasksForCommand path existing.Text with
            | Error diagnostics -> diagnostics, Some existing.Text, None, None
            | Ok(facts, diagnostics) ->
                let identityDiagnostics =
                    frontMatterIdentityDiagnostics
                        "Tasks"
                        LifecycleStage.Tasks
                        "tasks"
                        malformedTasksArtifact
                        tasksIdentityMismatch
                        missingTasksPrerequisite
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
                            malformedTasksArtifact
                                path
                                $"Tasks sourceSpec '{facts.FrontMatter.SourceSpec}' does not match '{specPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceClarifications,
                                    clarificationPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedTasksArtifact
                                path
                                $"Tasks sourceClarifications '{facts.FrontMatter.SourceClarifications}' does not match '{clarificationPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourceChecklist,
                                    checklistPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedTasksArtifact
                                path
                                $"Tasks sourceChecklist '{facts.FrontMatter.SourceChecklist}' does not match '{checklistPath workId}'."
                        if
                            not (
                                String.Equals(
                                    normalizeRelativePath facts.FrontMatter.SourcePlan,
                                    planPath workId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            malformedTasksArtifact
                                path
                                $"Tasks sourcePlan '{facts.FrontMatter.SourcePlan}' does not match '{planPath workId}'."
                        if
                            not (
                                String.Equals(
                                    facts.FrontMatter.Status,
                                    "tasksReady",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        then
                            failedTasksPrerequisite
                                path
                                $"Tasks status '{facts.FrontMatter.Status}' is not tasksReady."
                                [ facts.FrontMatter.Status ] ]

                let taskDiagnostics =
                    taskValidationDiagnostics path specFacts clarificationFacts checklistFacts planFacts evidence facts

                let graphDiagnostics =
                    [ let staleIds =
                          facts.Tasks
                          |> List.filter (fun task -> task.Status = TaskStatus.Stale)
                          |> List.map (fun task -> task.Id.Value)

                      if not (List.isEmpty staleIds) then
                          staleTask path staleIds

                      let blockingFindings =
                          facts.Findings
                          |> List.filter (fun finding ->
                              finding.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
                          |> List.map (fun finding -> finding.FindingId)

                      if not (List.isEmpty blockingFindings) then
                          failedTasksPrerequisite path "Tasks contain blocking findings." blockingFindings ]

                let allDiagnostics =
                    identityDiagnostics
                    @ diagnostics
                    @ taskDiagnostics
                    @ graphDiagnostics
                    @ evidenceDiagnostics
                    |> DiagnosticsModule.sort

                allDiagnostics, Some existing.Text, Some(tasksSummary facts), Some facts
