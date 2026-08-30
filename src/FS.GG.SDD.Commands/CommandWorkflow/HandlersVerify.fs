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
open FS.GG.SDD.Commands.Internal.TaskGraphAuthoring
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites
open FS.GG.SDD.Commands.Internal.HandlersEarly
open FS.GG.SDD.Commands.Internal.HandlersEvidence

module internal HandlersVerify =
    // Pure `Path` string ops only — the effectful `File`/`Directory` surface stays at the
    // `CommandEffects` edge and is deliberately kept out of scope in the MVU pure core.
    type private Path = System.IO.Path

    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    // Governance#366 owns the fsharp-public-surface/v1 evaluator.  This consumer deliberately
    // accepts only its persisted receipt: duplicating the capabilities/policy parser here would
    // create a second authority and let the two products disagree about a glob or exemption.
    let fsharpSurfaceReceiptPath = "readiness/fsharp-public-surface.json"

    let fsharpSurfaceReceiptDiagnostics workId (specFacts: SpecificationFacts option) model =
        let publicImpactFromFacts =
            specFacts
            |> Option.exists (fun facts ->
                facts.FrontMatter.ChangeTier.Equals("tier1", StringComparison.OrdinalIgnoreCase)
                || facts.FrontMatter.PublicOrToolFacingImpact = Some true)

        // Keep the receipt gate fail-closed even when a malformed/stale specification could not
        // yield typed facts. Its raw front matter is still sufficient to identify a claimed
        // Tier-1/public-impact surface; never turn that parse failure into a clean ship verdict.
        let publicImpactFromSource =
            snapshot (specPath workId) model
            |> Option.exists (fun spec ->
                spec.Text.Contains("changeTier: tier1", StringComparison.OrdinalIgnoreCase)
                || spec.Text.Contains("publicOrToolFacingImpact: true", StringComparison.OrdinalIgnoreCase))

        let publicImpact = publicImpactFromFacts || publicImpactFromSource

        // A malformed/incomplete optional capabilities file remains compatible with the historic
        // SDD-only route. Only a declared block-on-ship surface adopts this producer obligation.
        // We deliberately do not parse its policy here: the typed producer receipt remains the
        // authority for the glob, applicability, cardinality, and policy freshness.
        let governanceConfigured =
            (snapshot ".fsgg/capabilities.yml" model
             |> Option.exists (fun capabilities ->
                 capabilities.Text.Contains("block-on-ship", StringComparison.OrdinalIgnoreCase)))
            || snapshot fsharpSurfaceReceiptPath model |> Option.isSome

        if not publicImpact || not governanceConfigured then
            []
        else
            let malformed message =
                [ errorForPath
                      "verify.fsharpSurfaceReceiptMalformed"
                      fsharpSurfaceReceiptPath
                      $"Work item '{workId}' has public-impact/Tier-1 F# obligations, but Governance public-surface receipt is invalid: {message}"
                      "Run the Governance F# public-surface producer and commit a valid fsharp-public-surface/v1 receipt; malformed input never certifies ship readiness." ]

            match snapshot fsharpSurfaceReceiptPath model with
            | None ->
                [ errorForPath
                      "verify.fsharpSurfaceReceiptMissing"
                      fsharpSurfaceReceiptPath
                      $"Work item '{workId}' has public-impact/Tier-1 F# obligations and declares Governance capabilities, but no public-surface receipt was found."
                      "Run the Governance F# public-surface producer. A block-on-ship F# surface must be evaluated before SDD can certify this work." ]
            | Some receipt ->
                try
                    use document = JsonDocument.Parse receipt.Text
                    let root = document.RootElement

                    let stringField (name: string) =
                        match root.TryGetProperty name with
                        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj
                        | _ -> None

                    let boolField (name: string) =
                        match root.TryGetProperty name with
                        | true, value when value.ValueKind = JsonValueKind.True || value.ValueKind = JsonValueKind.False ->
                            Some(value.GetBoolean())
                        | _ -> None

                    let intField (name: string) =
                        match root.TryGetProperty name with
                        | true, value when value.ValueKind = JsonValueKind.Number ->
                            value.TryGetInt32()
                            |> function
                                | true, n -> Some n
                                | _ -> None
                        | _ -> None

                    match
                        intField "schemaVersion",
                        stringField "kind",
                        stringField "maturity",
                        stringField "applicability",
                        boolField "applicable",
                        stringField "declaredGlob",
                        intField "matchedModuleCount",
                        stringField "cardinality",
                        stringField "malformed"
                    with
                    | Some 1,
                      Some "fsharp-public-surface",
                      Some maturity,
                      Some applicability,
                      Some applicable,
                      Some glob,
                      Some count,
                      Some cardinality,
                      malformedValue ->
                        if malformedValue.IsSome then
                            malformed "the receipt reports malformed source or policy input"
                        elif maturity <> "block-on-ship" then
                            // This receipt is a real fact, but not the declared blocking surface.
                            []
                        elif not applicable then
                            if applicability = "not-applicable" then
                                []
                            else
                                malformed "a non-applicable receipt lacks the explicit not-applicable disposition"
                        elif count = 0 && cardinality = "zero" then
                            [ errorForPath
                                  "verify.emptyBlockingFSharpSurface"
                                  fsharpSurfaceReceiptPath
                                  $"Work item '{workId}' has public-impact/Tier-1 F# obligations, but block-on-ship surface glob '{glob}' matched zero signatures. Declare the public F# API in compiled .fsi files or record a validated non-applicability disposition."
                                  "Author/update the compiled .fsi signature surface before implementation hardens the public contract, then regenerate the Governance receipt." ]
                        elif
                            count < 0
                            || (count = 1 && cardinality <> "one")
                            || (count > 1 && cardinality <> "many")
                        then
                            malformed "matchedModuleCount and cardinality disagree"
                        else
                            []
                    | _ -> malformed "expected fsharp-public-surface/v1 fields are missing or have the wrong type"
                with ex ->
                    malformed ex.Message


    type VerifyEvidenceDispositionView =
        {
            Id: string
            ObligationId: string
            State: string
            /// FS.GG.SDD#398 (FR-003): the attestation basis, carried from the draft so `ship` can
            /// count it without re-deriving the rule. FS.GG.SDD#350 made it answerable — `true` when
            /// the obligation is backed by an `observedRun` receipt SDD parsed from a runner's report.
            Observed: bool
            /// WI-4 (ADR-0048): is this the disposition of a classified `{gameplay}` FR obligation?
            /// Carried from the draft and serialized into verify.json so `ship` and Governance count
            /// "classified-FR obligations unmet" over the committed dispositions without re-deriving it.
            ClassifiedRequirement: bool
            JourneyRequirement: bool
            /// FS.GG.SDD#865: is this a RECORD-discharged obligation, whose `Observed` above was earned
            /// by a `recordReceipt` rather than an `observedRun`? Carried from the draft and serialized
            /// into verify.json so `ship` partitions its shortfall report by class without re-deriving
            /// it from tasks it no longer holds at the merge boundary.
            RecordRequirement: bool
            EvidenceIds: string list
            TaskIds: string list
            SourceIds: string list
            Severity: string
            DiagnosticIds: string list
            Correction: string
        }

    type VerifyTestDispositionView =
        {
            Id: string
            ObligationId: string
            State: string
            /// FS.GG.SDD#398. The `TD-` mirror of the `ED-` attestation basis. This one matters most:
            /// the counter it feeds is called `verifyTestSatisfied`, and — despite the name — nothing
            /// here ever observed a test. Same rule object (`obligationIsObserved`), so `ED-` and `TD-`
            /// cannot drift on what "observed" means, exactly as #349 did for "cited".
            Observed: bool
            /// FS.GG.SDD#865: the `TD-` mirror of the record class. When `true` this obligation's
            /// shortfall state is `unrecorded`, never `unobserved`.
            RecordRequirement: bool
            EvidenceIds: string list
            TaskIds: string list
            RequirementIds: string list
            Severity: string
            DiagnosticIds: string list
            Correction: string
        }

    type VerifySkillView =
        { Skill: string
          RequiringTaskIds: string list
          Visibility: string
          SourceArtifactPath: string
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    let dispositionSeverity state =
        match state with
        | "missing"
        | "blocking"
        // FS.GG.SDD#350 / ADR-0035 stage 3. Reachable ONLY under `--require-observed`, so this arm
        // is inert by default and changes no existing byte. It is `blocking` rather than `warning`
        // deliberately: the whole point of the disposition is that an unobserved pass does not
        // satisfy, and a warning that still satisfies is the disclosure we already shipped (#398).
        | "unobserved"
        | "invalid" -> "blocking"
        | "stale" -> "warning"
        | "deferred"
        | "synthetic"
        | "advisory" -> "advisory"
        | _ -> "ready"

    let verifySourceKind (path: string) =
        if path = ".fsgg/project.yml" then
            "projectConfig"
        elif path = ".fsgg/sdd.yml" then
            "sddConfig"
        elif path = ".fsgg/agents.yml" then
            "agentsConfig"
        elif path.EndsWith("/spec.md", StringComparison.OrdinalIgnoreCase) then
            "specification"
        elif path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then
            "clarification"
        elif path.EndsWith("/checklist.md", StringComparison.OrdinalIgnoreCase) then
            "checklist"
        elif path.EndsWith("/plan.md", StringComparison.OrdinalIgnoreCase) then
            "plan"
        elif path.EndsWith("/tasks.yml", StringComparison.OrdinalIgnoreCase) then
            "tasks"
        elif path.EndsWith("/evidence.yml", StringComparison.OrdinalIgnoreCase) then
            "evidence"
        elif path.EndsWith("/analysis.json", StringComparison.OrdinalIgnoreCase) then
            "analysis"
        elif path.EndsWith("/work-model.json", StringComparison.OrdinalIgnoreCase) then
            "workModel"
        else
            "source"

    let verifySources
        workId
        specText
        clarificationText
        checklistText
        planText
        tasksText
        evidenceText
        analysisText
        workModelJson
        model
        : GeneratedViewSource list =
        [ snapshot ".fsgg/project.yml" model
          |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/sdd.yml" model
          |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          snapshot ".fsgg/agents.yml" model
          |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
          Some(analysisSourceFromSnapshot (specPath workId) specText)
          Some(analysisSourceFromSnapshot (clarificationPath workId) clarificationText)
          Some(analysisSourceFromSnapshot (checklistPath workId) checklistText)
          Some(analysisSourceFromSnapshot (planPath workId) planText)
          Some(analysisSourceFromSnapshot (tasksPath workId) tasksText)
          Some(analysisSourceFromSnapshot (evidencePath workId) evidenceText)
          analysisText |> Option.map (analysisSourceFromSnapshot (analysisPath workId))
          workModelJson |> Option.map (analysisSourceFromSnapshot (workModelPath workId)) ]
        |> List.choose id
        |> List.sortBy (fun source -> source.Path)

    let verifyFindingSeverity (diagnostic: Diagnostic) =
        match analysisFindingSeverity diagnostic with
        | "blocking"
        | "missingDisposition"
        | "malformedSource" -> "blocking"
        | "staleSource"
        | "generatedView"
        | "warning" -> "warning"
        | _ -> "advisory"

    let verifyFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic ->
            sprintf "VF%03d" (index + 1), diagnostic, verifyFindingSeverity diagnostic)

    // Feature 096 (issue #189): `affectedSourceIds` shipped as a hard-coded `[]`, so it carried no
    // information for any obligation. An evidence obligation is built per task per `requiredEvidence`
    // entry (`evidenceObligations taskFacts`, which sets `LinkedTaskIds = [ task.Id ]`), so today
    // `draft.TaskIds` is a singleton and the sources an obligation affects are the reference lineage
    // of that one task — the union of its three reference fields. The fold over `TaskIds` below is
    // written for the list, not the singleton, so a future many-tasks-per-obligation shape needs no
    // change here. `sourceIds` is the only field able to express an id with no typed counterpart
    // (AC-/CR-/GV-/PC-/PD-/PM-/SB-/VO-).
    //
    // The union lives here at the consumer, NOT in Task.fs's parser: `WorkTask.SourceIds` is what
    // `taskValidationDiagnostics.unknownSources` gates on, so unioning at parse would retroactively
    // subject `requirements:`/`decisions:` to a validation they have never faced, turning an untouched,
    // green `tasks.yml` red with no schemaVersion signal. `WorkModel.deriveGuidanceModel` unions the
    // same three fields for the same reason. This mirrors `verifyTestDispositionViews` below, which
    // already resolves its `RequirementIds` through `taskFacts`.
    let verifyEvidenceDispositionViews (taskFacts: TaskFacts) (drafts: EvidenceDispositionDraft list) =
        let tasksById =
            taskFacts.Tasks |> List.map (fun task -> task.Id.Value, task) |> Map.ofList

        // A draft naming a task absent from `taskFacts` is not currently reachable (drafts are built
        // from those same facts), but degrade to skipping it rather than throwing — Principle VIII.
        let affectedSourceIds (taskIds: string list) =
            taskIds
            |> List.choose (fun taskId -> Map.tryFind taskId tasksById)
            |> List.collect (fun task ->
                task.SourceIds
                @ (task.Requirements |> List.map _.Value)
                @ (task.Decisions |> List.map _.Value))
            |> List.distinct
            |> List.sort

        drafts
        |> List.map (fun draft ->
            let severity = dispositionSeverity draft.State

            { Id = "ED-" + draft.ObligationId
              ObligationId = draft.ObligationId
              State = draft.State
              Observed = draft.Observed
              ClassifiedRequirement = draft.ClassifiedRequirement
              JourneyRequirement = draft.JourneyRequirement
              RecordRequirement = draft.RecordRequirement
              EvidenceIds = draft.EvidenceIds
              TaskIds = draft.TaskIds
              SourceIds = affectedSourceIds draft.TaskIds
              Severity = severity
              DiagnosticIds = draft.DiagnosticIds
              Correction =
                if severity = "ready" then
                    ""
                else
                    $"Resolve evidence obligation {draft.ObligationId}." })
        |> List.sortBy (fun view -> view.Id)

    let verifyTestDispositionViews
        (taskFacts: TaskFacts)
        // FS.GG.SDD#349: the `TD-` mirror of the cited-artifact rule. `evidence` declares and
        // `verify` is the merge-boundary gate, so an artifact deleted *after* evidence was authored
        // must still be caught here — otherwise the check only ever fires at authoring time and a
        // stale citation walks straight past the boundary that matters (FR-004, US2).
        (artifactExists: string -> bool)
        // #709/#835: normal and production-journey receipts bind exact raw report bytes.
        (artifactBytes: string -> byte array option)
        // FS.GG.SDD#350 / ADR-0035 stage 3: `verify --require-observed`. `false` (the default) leaves
        // this function byte-for-byte what it was — the `unobserved` arm below is unreachable and a
        // pass satisfies on the author's word, exactly as it does today.
        (requireObserved: bool)
        (artifact: EvidenceArtifact)
        =
        taskFacts.Tasks
        |> List.collect (fun task -> task.RequiredEvidence |> List.map (fun ev -> ev.Value, task))
        |> List.groupBy fst
        |> List.map (fun (obligationId, entries) ->
            let tasks = entries |> List.map snd

            // FS.GG.SDD#865. Derived from the same union of task tags the `ED-` obligation minter reads,
            // through the same `dischargeClassFromTags` meaning-site, so the two ladders cannot disagree
            // about which class an obligation is in — the discipline #349 established for "cited" and
            // #350 for "observed", applied to "what could discharge this".
            let recordDischarged =
                tasks
                |> List.collect (fun task -> task.RequiredSkills)
                |> dischargeClassFromTags
                |> isRecordDischargeClass

            let matches =
                artifact.Evidence
                |> List.filter (fun declaration ->
                    declaration.Id.Value = obligationId
                    || declaration.ObligationRefs
                       |> List.exists (fun id -> String.Equals(id, obligationId, StringComparison.OrdinalIgnoreCase)))

            let state, diagnostics =
                if List.isEmpty matches then
                    "missing", [ "verify.missingRequiredTest" ]
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
                // #306: mirror the `ED-` cascade in `evidenceDispositions` — a visual-inspection
                // obligation that passes without naming a rendered artifact is invalid, not satisfied.
                elif
                    isVisualInspectionTagged (tasks |> List.collect (fun task -> task.RequiredSkills))
                    && matches |> List.exists passesWithoutRenderedArtifact
                then
                    "invalid", [ "evidence.missingVisualInspectionArtifact" ]
                // #349: mirror the `ED-` cascade — a pass citing an artifact that is not on disk is
                // invalid, not satisfied. Same rule object (`missingCitedArtifacts`), so `ED-` and
                // `TD-` cannot drift on what counts as supported.
                elif
                    matches
                    |> List.exists (fun declaration ->
                        not (List.isEmpty (missingCitedArtifacts artifactExists declaration)))
                then
                    "invalid", [ "evidence.artifactNotFound" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        declaration.ObservedRun.IsSome
                        && not (observedRunIsCurrent artifactBytes declaration))
                then
                    "invalid", [ "evidence.observedRunStale" ]
                // #865: mirror the `ED-` record arms, in the same order and with the same guards, so
                // `ED-` and `TD-` cannot drift on when a record receipt is malformed versus stale.
                elif
                    matches
                    |> List.exists (fun declaration ->
                        declaration.RecordReceipt
                        |> Option.exists (fun receipt -> Option.isSome (recordReceiptInconsistency receipt)))
                then
                    "invalid", [ "evidence.recordReceiptInvalid" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        declaration.RecordReceipt
                        |> Option.exists (fun receipt ->
                            Option.isNone (recordReceiptInconsistency receipt)
                            && not (recordReceiptIsCurrent artifactBytes declaration)))
                then
                    "invalid", [ "evidence.recordReceiptStale" ]
                elif
                    matches
                    |> List.exists (fun declaration ->
                        normalizedEvidenceResult declaration.Result = "pass" && declaration.Synthetic)
                then
                    "synthetic", []
                // WI-4 (ADR-0048): mirror the `ED-` cascade — a classified {gameplay} FR obligation is
                // satisfied only by a real, non-synthetic test KIND. A non-synthetic pass of a non-test
                // kind (e.g. `implementation`) leaves the required test UNMET, not satisfied, so `ED-`
                // and `TD-` cannot drift on what discharges a gameplay obligation (as for the #306
                // visual arm above). A synthetic-only pass already fell to `synthetic`.
                elif
                    isProductionJourneyTagged (tasks |> List.collect (fun task -> task.RequiredSkills))
                    && matches
                       |> List.exists (fun declaration ->
                           normalizedEvidenceResult declaration.Result = "pass"
                           && not declaration.Synthetic)
                    && not (matches |> List.exists (journeyReceiptReportIsCurrent artifactBytes))
                then
                    if matches |> List.exists hasValidJourneyReceipt then
                        "invalid", [ "evidence.productionJourneyReceiptStale" ]
                    else
                        "invalid", [ "evidence.productionJourneyReceiptInvalid" ]
                elif
                    isGameplayTestTagged (tasks |> List.collect (fun task -> task.RequiredSkills))
                    && matches
                       |> List.exists (fun declaration ->
                           normalizedEvidenceResult declaration.Result = "pass"
                           && not declaration.Synthetic)
                    && not (matches |> List.exists (satisfiesRequiredEvidenceKinds realTestEvidenceKinds))
                then
                    "invalid", [ "evidence.classifiedRequirementTestObligationUnmet" ]
                // FS.GG.SDD#350 / ADR-0035 stage 3 — the defect this whole issue names. It sits
                // IMMEDIATELY above `satisfied` and intercepts exactly the passes that would have
                // reached it, which is why the ordering is load-bearing rather than cosmetic.
                //
                // `obligationIsObserved` is `forall` over the real passes (see `Evidence.fs`), so an
                // obligation backed by one observed run AND one hand-asserted pass is NOT observed —
                // the receipt cannot launder the assertion beside it. Negating it here inherits that
                // fail-closed reading for free, which is the reason to consume the shared rule rather
                // than restate it: `ED-`, `TD-`, `ship`, and the committed verdict cannot drift on
                // what "observed" means.
                //
                // A disclosed `synthetic` pass never arrives (the arm above took it), and a deferral
                // never claims a pass at all — so neither is punished for a run it never asserted.
                //
                // The `ED-` ladder is deliberately NOT given this arm, and the asymmetry is the
                // design rather than an oversight. ADR-0035 §2 scopes `unobserved` to a TEST
                // obligation ("a test obligation cannot reach `satisfied` on `result: pass` alone"),
                // and the two ladders answer two different questions: `ED-` asks "is there evidence
                // for this obligation?" and keeps saying `supported`, carrying the basis in its
                // `Observed` flag (the #398 disclosure split); `TD-` asks "did an observed run
                // discharge the required test?" and is the one that gates. `ship` then re-asserts the
                // receipt against that `Observed` flag. Giving `ED-` an `unobserved` STATE would
                // instead change a persisted enum on the governance-handoff surface — a schema
                // change this stage deliberately does not make.
                //
                // FS.GG.SDD#865 SPLIT THIS ARM IN TWO, and the split is the fix. As written above, the
                // arm asked "was a run observed?" of EVERY obligation — including obligations no run
                // could ever discharge, for which the answer is `false` by construction and stays
                // `false` forever. `verify` then blocked permanently and `ship` was unreachable; two
                // `.github` items merged with the gate red rather than fabricate a receipt.
                //
                // So the guard is now the obligation's own declared class, and the consequence follows
                // it: a record obligation lacking its record is `unrecorded` and is told to name the
                // record, and a test obligation lacking its run keeps exactly the state, diagnostic and
                // wording it has always had. Every remark below about ordering, about `forall`, about
                // synthetics and deferrals never arriving here, holds unchanged for both arms — they
                // consume the same `obligationDischarged` rule, which dispatches once in `Artifacts`.
                elif
                    requireObserved
                    && recordDischarged
                    && matches
                       |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass")
                    && not (obligationDischarged Evidence.recordDischargeClass matches)
                then
                    "unrecorded", [ "verify.unrecordedRequiredRecord" ]
                elif
                    requireObserved
                    && not recordDischarged
                    && matches
                       |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass")
                    && not (obligationDischarged Evidence.testDischargeClass matches)
                then
                    "unobserved", [ "verify.unobservedRequiredTest" ]
                elif
                    matches
                    |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "pass")
                then
                    "satisfied", []
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
                    "stale", [ "verify.staleRequiredTest" ]
                elif
                    matches
                    |> List.exists (fun declaration -> normalizedEvidenceResult declaration.Result = "advisory")
                then
                    "advisory", []
                else
                    "missing", [ "verify.missingRequiredTest" ]

            let severity = dispositionSeverity state

            { Id = "TD-" + obligationId
              ObligationId = obligationId
              State = state
              // #865: the same one kind-directed rule the `ED-` site consumes, so the `TD-` view cannot
              // call an obligation discharged that `ED-` calls unobserved, or the reverse.
              Observed =
                state = "satisfied"
                && obligationDischarged
                    (if recordDischarged then
                         Evidence.recordDischargeClass
                     else
                         Evidence.testDischargeClass)
                    matches
              RecordRequirement = recordDischarged
              EvidenceIds =
                matches
                |> List.map (fun declaration -> declaration.Id.Value)
                |> List.distinct
                |> List.sort
              TaskIds = tasks |> List.map (fun task -> task.Id.Value) |> List.distinct |> List.sort
              RequirementIds =
                tasks
                |> List.collect (fun task -> task.Requirements |> List.map _.Value)
                |> List.distinct
                |> List.sort
              Severity = severity
              DiagnosticIds = diagnostics
              Correction =
                if severity = "ready" then
                    ""
                else
                    $"Record a verifying test for {obligationId}." })
        |> List.sortBy (fun view -> view.Id)

    let verifySkillViews workId (taskFacts: TaskFacts) (evidenceDrafts: EvidenceDispositionDraft list) =
        let taskStates =
            evidenceDrafts
            |> List.collect (fun draft -> draft.TaskIds |> List.map (fun taskId -> taskId, draft.State))
            |> List.groupBy fst
            |> List.map (fun (taskId, entries) -> taskId, entries |> List.map snd)
            |> Map.ofList

        let blockingStates = set [ "missing"; "blocking"; "invalid" ]

        taskFacts.Tasks
        |> List.collect (fun task -> task.RequiredSkills |> List.map (fun skill -> skill, task))
        |> List.groupBy fst
        |> List.map (fun (skill, entries) ->
            let tasks = entries |> List.map snd

            let visible =
                tasks
                |> List.forall (fun task ->
                    match Map.tryFind task.Id.Value taskStates with
                    | Some states -> not (states |> List.exists (fun state -> Set.contains state blockingStates))
                    | None -> true)

            { Skill = skill
              RequiringTaskIds = tasks |> List.map (fun task -> task.Id.Value) |> List.distinct |> List.sort
              Visibility = if visible then "visible" else "missing"
              SourceArtifactPath = tasksPath workId
              Severity = if visible then "ready" else "blocking"
              DiagnosticIds = if visible then [] else [ "evidence.missingRequiredSkill" ]
              Correction =
                if visible then
                    ""
                else
                    $"Make required skill '{skill}' visible through lifecycle artifacts or supporting evidence." })
        |> List.sortBy (fun view -> view.Skill)

    let existingVerifyDiagnostic workId model =
        existingViewIdentityDiagnostic
            parseVerificationView
            (fun view -> view.WorkId.Value)
            malformedVerificationView
            verifyIdentityMismatch
            (verifyPath workId)
            workId
            model

    let verifyJson
        (workId: string)
        (generator: GeneratorVersion)
        (readiness: string)
        (sources: GeneratedViewSource list)
        (lifecycleStages: (string * string) list)
        (lifecycleStatus: string)
        (taskCount: int)
        (dependencyCount: int)
        (dependenciesValid: bool)
        (statusesValid: bool)
        (taskFindingIds: string list)
        (evidenceViews: VerifyEvidenceDispositionView list)
        (testViews: VerifyTestDispositionView list)
        (skillViews: VerifySkillView list)
        (generatedViews: GeneratedViewState list)
        (diagnostics: Diagnostic list)
        =
        let findings = verifyFindings diagnostics

        writeReadinessEnvelope workId "verify" readiness generator verifySourceKind sources (fun writer ->
            writeLifecycleReadiness writer lifecycleStatus lifecycleStages
            writer.WriteStartObject("taskGraph")
            writer.WriteNumber("taskCount", taskCount)
            writer.WriteNumber("dependencyCount", dependencyCount)
            writer.WriteBoolean("dependenciesValid", dependenciesValid)
            writer.WriteBoolean("statusesValid", statusesValid)
            writeStringArray writer "findingIds" taskFindingIds
            writer.WriteEndObject()
            writer.WriteStartArray("evidenceDispositions")

            evidenceViews
            |> List.iter (fun view ->
                writer.WriteStartObject()
                writer.WriteString("id", view.Id)
                writer.WriteString("obligationId", view.ObligationId)
                writer.WriteString("state", view.State)
                writer.WriteBoolean("observed", view.Observed)
                writer.WriteBoolean("classifiedRequirement", view.ClassifiedRequirement)
                writer.WriteBoolean("journeyRequirement", view.JourneyRequirement)
                writer.WriteBoolean("recordRequirement", view.RecordRequirement)
                writeStringArray writer "evidenceIds" view.EvidenceIds
                writeStringArray writer "affectedTaskIds" view.TaskIds
                writeStringArray writer "affectedSourceIds" view.SourceIds
                writer.WriteString("severity", view.Severity)
                writeStringArray writer "diagnosticIds" view.DiagnosticIds
                writer.WriteString("correction", view.Correction)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteStartArray("testDispositions")

            testViews
            |> List.iter (fun view ->
                writer.WriteStartObject()
                writer.WriteString("id", view.Id)
                writer.WriteString("obligationId", view.ObligationId)
                writer.WriteString("state", view.State)
                writer.WriteBoolean("observed", view.Observed)
                writer.WriteBoolean("recordRequirement", view.RecordRequirement)
                writeStringArray writer "evidenceIds" view.EvidenceIds
                writeStringArray writer "affectedTaskIds" view.TaskIds
                writeStringArray writer "affectedRequirementIds" view.RequirementIds
                writer.WriteString("severity", view.Severity)
                writeStringArray writer "diagnosticIds" view.DiagnosticIds
                writer.WriteString("correction", view.Correction)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteStartArray("skillVisibility")

            skillViews
            |> List.iter (fun view ->
                writer.WriteStartObject()
                writer.WriteString("skill", view.Skill)
                writeStringArray writer "requiringTaskIds" view.RequiringTaskIds
                writer.WriteString("visibility", view.Visibility)
                writer.WriteString("sourceArtifactPath", view.SourceArtifactPath)
                writer.WriteString("severity", view.Severity)
                writeStringArray writer "diagnosticIds" view.DiagnosticIds
                writer.WriteString("correction", view.Correction)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writeGeneratedViewsArray writer generatedViews
            writeGovernanceReadinessTail writer findings diagnostics readiness

            writeNextAction
                writer
                (readiness = "verificationReady")
                "verify.next.ship"
                "Verification readiness is current and ready for ship."
                "Verification found lifecycle diagnostics that must be corrected before ship.")

    let computeVerifyPlan model =
        let ((specification, clarification, checklist, plan, tasks, analysis, evidenceSummaryOpt, verificationSummary),
             diagnostics,
             generatedViews,
             effects) =
            runHandler model (None, None, None, None, None, None, None, None) (fun workId ->
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

                let existingEvidenceArtifact, existingEvidenceDiagnostics, evidenceText =
                    parseExistingEvidence workId model

                let evidencePresenceDiagnostics =
                    match existingEvidenceArtifact, snapshot (evidencePath workId) model with
                    | None, None ->
                        [ missingEvidencePrerequisite
                              (evidencePath workId)
                              $"Evidence prerequisite '{evidencePath workId}' is missing." ]
                    | _ -> []

                let verifyViewDiagnostics = existingVerifyDiagnostic workId model |> Option.toList

                let fsharpSurfaceDiagnostics =
                    fsharpSurfaceReceiptDiagnostics workId specFacts model

                let verificationDiagnostics, evidenceSummaryOpt, evidenceViews, testViews, skillViews =
                    match
                        specFacts,
                        clarificationFacts,
                        checklistFacts,
                        planFacts,
                        taskFacts,
                        specText,
                        clarificationText,
                        checklistText,
                        planText,
                        taskText,
                        analysisText,
                        existingEvidenceArtifact
                    with
                    | Some specFacts,
                      Some clarificationFacts,
                      Some checklistFacts,
                      Some planFacts,
                      Some taskFacts,
                      Some specText,
                      Some clarificationText,
                      Some checklistText,
                      Some planText,
                      Some taskText,
                      Some analysisText,
                      Some artifact ->
                        let currentSnapshots =
                            currentEvidenceSourceSnapshots
                                workId
                                specText
                                clarificationText
                                checklistText
                                planText
                                taskText
                                analysisText

                        let validationDiagnostics =
                            let performanceIntent =
                                specText
                                |> splitFrontMatter
                                |> Option.bind (fun (yaml, _) ->
                                    parsePerformanceIntentYaml yaml |> Result.toOption |> Option.flatten)

                            evidenceValidationDiagnostics
                                workId
                                specFacts
                                clarificationFacts
                                checklistFacts
                                planFacts
                                taskFacts
                                performanceIntent
                                currentSnapshots
                                (citedArtifactExists model)
                                (fun artifactPath -> snapshot artifactPath model |> Option.map _.Text)
                                (fun artifactPath -> snapshot artifactPath model |> Option.bind _.RawBytes)
                                artifact

                        let authorityDiagnostics = localArtifactAuthorityDiagnostics workId model artifact

                        let obligations = evidenceObligations taskFacts

                        let dispositions =
                            evidenceDispositions
                                obligations
                                (citedArtifactIsUsable model)
                                (fun artifactPath -> snapshot artifactPath model |> Option.bind _.RawBytes)
                                artifact

                        let dispositionDiagnostics =
                            evidenceDispositionDiagnostics (evidencePath workId) dispositions

                        let evidenceViews = verifyEvidenceDispositionViews taskFacts dispositions

                        let testViews =
                            verifyTestDispositionViews
                                taskFacts
                                (citedArtifactIsUsable model)
                                (fun artifactPath -> snapshot artifactPath model |> Option.bind _.RawBytes)
                                model.Request.RequireObserved
                                artifact

                        let skillViews = verifySkillViews workId taskFacts dispositions

                        let testDiagnostics =
                            let missing =
                                testViews
                                |> List.filter (fun view -> view.State = "missing")
                                |> List.map _.ObligationId
                                |> List.sort

                            let stale =
                                testViews
                                |> List.filter (fun view -> view.State = "stale")
                                |> List.map _.ObligationId
                                |> List.sort

                            // FS.GG.SDD#350: empty unless `--require-observed` — the state is
                            // unreachable without it, so this list is the flag's only blocking effect.
                            let unobserved =
                                testViews
                                |> List.filter (fun view -> view.State = "unobserved")
                                |> List.map _.ObligationId
                                |> List.sort

                            // FS.GG.SDD#865: the record class's own shortfall list, so the two are
                            // reported separately rather than one standing in for the other.
                            let unrecorded =
                                testViews
                                |> List.filter (fun view -> view.State = "unrecorded")
                                |> List.map _.ObligationId
                                |> List.sort

                            [ if not (List.isEmpty missing) then
                                  missingRequiredTest (tasksPath workId) missing
                              if not (List.isEmpty unobserved) then
                                  unobservedRequiredTest (tasksPath workId) unobserved
                              if not (List.isEmpty unrecorded) then
                                  unrecordedRequiredRecord (tasksPath workId) unrecorded
                              if not (List.isEmpty stale) then
                                  staleRequiredTest (tasksPath workId) stale ]

                        let skillDiagnostics =
                            let missing =
                                skillViews
                                |> List.filter (fun view -> view.Visibility = "missing")
                                |> List.map _.Skill
                                |> List.sort

                            if not (List.isEmpty missing) then
                                [ missingRequiredSkill (tasksPath workId) missing ]
                            else
                                []

                        let summary = evidenceSummary workId artifact dispositions

                        validationDiagnostics
                        @ authorityDiagnostics
                        @ dispositionDiagnostics
                        @ testDiagnostics
                        @ skillDiagnostics,
                        Some summary,
                        evidenceViews,
                        testViews,
                        skillViews
                    | _ -> [], None, [], [], []

                let commandDiagnostics =
                    projectDiagnostics
                    @ duplicateDiagnostics
                    @ specificationDiagnostics
                    @ clarificationDiagnostics
                    @ checklistDiagnostics
                    @ planDiagnostics
                    @ taskDiagnostics
                    @ analysisDiagnostics
                    @ existingEvidenceDiagnostics
                    @ evidencePresenceDiagnostics
                    @ verifyViewDiagnostics
                    @ fsharpSurfaceDiagnostics
                    @ verificationDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, workModelView, workModelEffects, _ =
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

                commandDiagnostics @ generatedDiagnostics,
                (fun hasBlocking diagnostics ->
                    let readiness =
                        if hasBlocking then
                            "needsVerificationCorrection"
                        else
                            "verificationReady"

                    let verificationSummary, verifyView, verifyEffects =
                        match
                            specText, clarificationText, checklistText, planText, taskText, analysisText, taskFacts
                        with
                        | Some specText,
                          Some clarificationText,
                          Some checklistText,
                          Some planText,
                          Some taskText,
                          Some analysisText,
                          Some taskFacts ->
                            let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model

                            let sources =
                                verifySources
                                    workId
                                    specText
                                    clarificationText
                                    checklistText
                                    planText
                                    taskText
                                    (evidenceText |> Option.defaultValue "")
                                    (Some analysisText)
                                    workModelJson
                                    model

                            let lifecycleStages =
                                [ "specify", (if Option.isSome specFacts then "current" else "missing")
                                  "clarify",
                                  (if Option.isSome clarificationFacts then
                                       "current"
                                   else
                                       "missing")
                                  "checklist",
                                  (if Option.isSome checklistFacts then
                                       "current"
                                   else
                                       "missing")
                                  "plan", (if Option.isSome planFacts then "current" else "missing")
                                  "tasks", "current"
                                  "analyze",
                                  (analysis
                                   |> Option.map (fun summary -> summary.Readiness)
                                   |> Option.defaultValue "missing")
                                  "evidence",
                                  (evidenceSummaryOpt
                                   |> Option.map (fun summary -> summary.Readiness)
                                   |> Option.defaultValue "missing") ]

                            let dependencyCount =
                                taskFacts.Tasks |> List.collect (fun task -> task.Dependencies) |> List.length

                            let dependencyDiagnosticIds = set [ "unknownTaskDependency"; "taskDependencyCycle" ]
                            let statusDiagnosticIds = set [ "skippedTaskMissingRationale" ]

                            let dependenciesValid =
                                not (
                                    diagnostics
                                    |> List.exists (fun diagnostic ->
                                        Set.contains diagnostic.Id dependencyDiagnosticIds)
                                )

                            let statusesValid =
                                not (
                                    diagnostics
                                    |> List.exists (fun diagnostic -> Set.contains diagnostic.Id statusDiagnosticIds)
                                )

                            let taskFindingIds =
                                taskFacts.Findings |> List.map (fun finding -> finding.FindingId) |> List.sort

                            let generatedViewsForVerify =
                                [ workModelView
                                  analysis
                                  |> Option.map (fun _ ->
                                      generatedViewState
                                          (analysisPath workId)
                                          "verification"
                                          model.Request.GeneratorVersion
                                          []
                                          GeneratedViewCurrency.Current
                                          [])
                                  |> Option.defaultValue (
                                      generatedViewState
                                          (analysisPath workId)
                                          "verification"
                                          model.Request.GeneratorVersion
                                          []
                                          GeneratedViewCurrency.Missing
                                          []
                                  ) ]

                            let text =
                                verifyJson
                                    workId
                                    model.Request.GeneratorVersion
                                    readiness
                                    sources
                                    lifecycleStages
                                    (if hasBlocking then
                                         "needsCorrection"
                                     else
                                         "implementationReady")
                                    taskFacts.Tasks.Length
                                    dependencyCount
                                    dependenciesValid
                                    statusesValid
                                    taskFindingIds
                                    evidenceViews
                                    testViews
                                    skillViews
                                    generatedViewsForVerify
                                    diagnostics

                            let view =
                                generatedViewState
                                    (verifyPath workId)
                                    "verification"
                                    model.Request.GeneratorVersion
                                    sources
                                    GeneratedViewCurrency.Current
                                    []

                            let findings = verifyFindings diagnostics

                            let findingCount severity =
                                findings
                                |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity)
                                |> List.length

                            let evidenceCount state =
                                evidenceViews |> List.filter (fun view -> view.State = state) |> List.length

                            let testCount state =
                                testViews |> List.filter (fun view -> view.State = state) |> List.length

                            // #398: the attestation split, for BOTH disposition families. Each is
                            // *derived* from the per-obligation `Observed` fact rather than asserted,
                            // so the day #350 makes `Evidence.isObserved` say `true` they move on
                            // their own. Bound once each, so `supported = selfAttested + observed`
                            // (and its `satisfied` twin) is visible here rather than implied by two
                            // independent expressions below.
                            let evidenceSupported = evidenceCount "supported"
                            let testSatisfied = testCount "satisfied"

                            let evidenceObservedCount =
                                evidenceViews
                                |> List.filter (fun view -> view.State = "supported" && view.Observed)
                                |> List.length

                            let testObservedCount =
                                testViews
                                |> List.filter (fun view -> view.State = "satisfied" && view.Observed)
                                |> List.length

                            let summary: VerificationSummary =
                                { WorkId = workId
                                  Stage = "verify"
                                  Status = readiness
                                  VerifyPath = verifyPath workId
                                  FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                                  ReadyFindingCount =
                                    if readiness = "verificationReady" then
                                        evidenceViews.Length + testViews.Length
                                    else
                                        findingCount "ready"
                                  AdvisoryCount = findingCount "advisory"
                                  WarningCount = findingCount "warning"
                                  BlockingCount = findingCount "blocking"
                                  ObligationCount = evidenceViews.Length + testViews.Length
                                  EvidenceSupportedCount = evidenceSupported
                                  EvidenceSelfAttestedCount = evidenceSupported - evidenceObservedCount
                                  EvidenceObservedCount = evidenceObservedCount
                                  EvidenceDeferredCount = evidenceCount "deferred"
                                  EvidenceMissingCount = evidenceCount "missing"
                                  EvidenceStaleCount = evidenceCount "stale"
                                  EvidenceSyntheticCount = evidenceCount "synthetic"
                                  EvidenceInvalidCount = evidenceCount "invalid"
                                  TestSatisfiedCount = testSatisfied
                                  TestSelfAttestedCount = testSatisfied - testObservedCount
                                  TestObservedCount = testObservedCount
                                  TestDeferredCount = testCount "deferred"
                                  TestMissingCount = testCount "missing"
                                  TestStaleCount = testCount "stale"
                                  TestInvalidCount = testCount "invalid"
                                  // WI-4 (ADR-0048): carry the evidence stage's classified-FR unmet
                                  // aggregate through unchanged — verify reports the same number ship binds.
                                  ClassifiedObligationsUnmetCount =
                                    evidenceSummaryOpt
                                    |> Option.map (fun summary -> summary.ClassifiedObligationsUnmetCount)
                                    |> Option.defaultValue 0
                                  JourneyObligationsUnmetCount =
                                    evidenceSummaryOpt
                                    |> Option.map (fun summary -> summary.JourneyObligationsUnmetCount)
                                    |> Option.defaultValue 0
                                  SkillVisibleCount =
                                    skillViews
                                    |> List.filter (fun view -> view.Visibility = "visible")
                                    |> List.length
                                  SkillMissingCount =
                                    skillViews
                                    |> List.filter (fun view -> view.Visibility = "missing")
                                    |> List.length
                                  SourceSnapshotCount = sources.Length
                                  Readiness = readiness }

                            let effects =
                                if hasBlocking then
                                    []
                                else
                                    [ CreateDirectory(readinessDirectory workId)
                                      WriteFile(verifyPath workId, text, GeneratedView) ]

                            Some summary, Some view, effects
                        | _ -> None, None, []

                    let generatedViews = [ Some workModelView; verifyView ] |> List.choose id

                    (specification,
                     clarification,
                     checklist,
                     plan,
                     tasks,
                     analysis,
                     evidenceSummaryOpt,
                     verificationSummary),
                    generatedViews,
                    workModelEffects,
                    verifyEffects))

        diagnostics,
        specification,
        clarification,
        checklist,
        plan,
        tasks,
        analysis,
        evidenceSummaryOpt,
        verificationSummary,
        generatedViews,
        effects
