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
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites
open FS.GG.SDD.Commands.Internal.HandlersEarly
open FS.GG.SDD.Commands.Internal.HandlersEvidence
open FS.GG.SDD.Commands.Internal.HandlersVerify

module internal HandlersShip =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module GovernanceHandoffModule = FS.GG.SDD.Artifacts.GovernanceHandoff
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module ShipModule = FS.GG.SDD.Artifacts.Ship
    module ShipVerdictModule = FS.GG.SDD.Artifacts.ShipVerdict
    module WorkModelModule = FS.GG.SDD.Artifacts.WorkModel

    // ---- Ship command ----

    /// FS.GG.SDD#398. The attestation split, folded once. `ship` READS the per-obligation basis
    /// `verify` recorded — it never re-derives the rule — so the day #350 makes an obligation
    /// observed, every counter here moves without `ship` being touched.
    ///
    /// Returns `supported, selfAttested, observed`, with `supported = selfAttested + observed` true
    /// by construction rather than by two call sites agreeing.
    let shipEvidenceAttestationCounts (verificationView: VerificationView option) =
        let dispositions =
            match verificationView with
            | Some view -> view.EvidenceDispositions
            | None -> []

        let supported =
            dispositions
            |> List.filter (fun disposition -> disposition.State = EvidenceSupported)

        let observed = supported |> List.filter _.Observed |> List.length

        supported.Length, supported.Length - observed, observed

    let shipEvidenceStateValue (state: EvidenceDispositionState) =
        match state with
        | EvidenceSupported -> "supported"
        | EvidenceDeferred -> "deferred"
        | EvidenceMissingDisposition -> "missing"
        | EvidenceStale -> "stale"
        | EvidenceSyntheticDisposition -> "synthetic"
        | EvidenceInvalid -> "invalid"
        | EvidenceAdvisory -> "advisory"
        | EvidenceBlocking -> "blocking"

    // ---- Governance handoff (generated readiness view consumed by FS.GG.Governance) ----

    let governanceHandoffPath workId =
        GenerationManifestModule.expectedGovernanceHandoffOutputPath workId

    /// Presence-only detection of optional `.fsgg` Governance config from read snapshots.
    /// SDD never parses Governance config semantics (FR-011).
    let governanceConfigPresence model : GovernanceHandoffModule.GovernanceConfigPresence =
        let present path = snapshot path model |> Option.isSome

        let pointer path =
            if present path then Some path else None

        { PolicyPresent = present ".fsgg/policy.yml"
          PolicyPointer = pointer ".fsgg/policy.yml"
          CapabilitiesPresent = present ".fsgg/capabilities.yml"
          CapabilitiesPointer = pointer ".fsgg/capabilities.yml"
          ToolingPresent = present ".fsgg/tooling.yml"
          ToolingPointer = pointer ".fsgg/tooling.yml" }

    /// Derive the handoff's advisory readiness facts from the SDD-owned ship.json text.
    /// Both ship (emission) and refresh (regeneration) parse the same ship.json, so the
    /// regenerated handoff is byte-identical to the emitted one when sources are unchanged.
    let parseShipReadinessFacts
        (shipText: string)
        (perViewState: (string * string) list)
        : GovernanceHandoffModule.ReadinessFacts =
        try
            use document = System.Text.Json.JsonDocument.Parse shipText
            let root = document.RootElement

            let tryObj (element: System.Text.Json.JsonElement) (name: string) =
                match element.TryGetProperty name with
                | true, value when value.ValueKind = System.Text.Json.JsonValueKind.Object -> Some value
                | _ -> None

            let strField (element: System.Text.Json.JsonElement option) (name: string) =
                match element with
                | Some e ->
                    match e.TryGetProperty name with
                    | true, value when value.ValueKind = System.Text.Json.JsonValueKind.String ->
                        value.GetString() |> Option.ofObj |> Option.defaultValue ""
                    | _ -> ""
                | None -> ""

            let idsField (element: System.Text.Json.JsonElement option) (name: string) =
                match element with
                | Some e ->
                    match e.TryGetProperty name with
                    | true, value when value.ValueKind = System.Text.Json.JsonValueKind.Array ->
                        [ for item in value.EnumerateArray() do
                              if item.ValueKind = System.Text.Json.JsonValueKind.String then
                                  yield (item.GetString() |> Option.ofObj |> Option.defaultValue "") ]
                    | _ -> []
                | None -> []

            let dispositionEl = tryObj root "disposition"
            let verificationEl = tryObj root "verificationReadiness"
            let blocking = idsField dispositionEl "blockingFindingIds"

            { ShipDisposition = strField dispositionEl "state"
              VerificationReadiness = strField verificationEl "status"
              AdvisoryCount = (idsField dispositionEl "advisoryFindingIds").Length
              WarningCount = (idsField dispositionEl "warningFindingIds").Length
              BlockingCount = blocking.Length
              BlockingDiagnosticIds = blocking |> List.sort
              PerViewState = perViewState }
        with _ ->
            { ShipDisposition = ""
              VerificationReadiness = ""
              AdvisoryCount = 0
              WarningCount = 0
              BlockingCount = 0
              BlockingDiagnosticIds = []
              PerViewState = perViewState }

    /// Project the handoff and produce (generated-view state, write effect, json text). Pure over
    /// the work-model JSON, verify/ship texts, and config presence; readiness is parsed from ship.json.
    let governanceHandoffEmission
        workId
        (generator: GeneratorVersion)
        (workModelJson: string option)
        (verifyText: string option)
        (shipText: string)
        (config: GovernanceHandoffModule.GovernanceConfigPresence)
        : GeneratedViewState option * CommandEffect list * string option =
        match workModelJson with
        | Some wmJson ->
            match
                WorkModelModule.parseWorkModel
                    { Path = workModelPath workId
                      Text = wmJson }
            with
            | Ok workModel ->
                // The handoff's own three-source currency (identical for ship and a clean refresh).
                let perViewState =
                    [ "ship.json", "current"
                      "verify.json",
                      (match verifyText with
                       | Some _ -> "current"
                       | None -> "missing")
                      "work-model.json", "current" ]
                    |> List.sortBy fst

                let readiness = parseShipReadinessFacts shipText perViewState

                let handoffSources =
                    [ Some(GovernanceHandoffModule.sourceIdentity (workModelPath workId) wmJson)
                      verifyText
                      |> Option.map (GovernanceHandoffModule.sourceIdentity (verifyPath workId))
                      Some(GovernanceHandoffModule.sourceIdentity (shipPath workId) shipText) ]
                    |> List.choose id

                let viewSources =
                    [ Some(analysisSourceFromSnapshot (workModelPath workId) wmJson)
                      verifyText |> Option.map (analysisSourceFromSnapshot (verifyPath workId))
                      Some(analysisSourceFromSnapshot (shipPath workId) shipText) ]
                    |> List.choose id

                let handoff =
                    GovernanceHandoffModule.fromWorkModel workModel handoffSources config readiness generator

                let handoffJson = GovernanceHandoffModule.toJson handoff

                let view =
                    generatedViewState
                        (governanceHandoffPath workId)
                        "governance-handoff"
                        generator
                        viewSources
                        GeneratedViewCurrency.Current
                        []

                Some view, [ WriteFile(governanceHandoffPath workId, handoffJson, GeneratedView) ], Some handoffJson
            | Error _ -> None, [], None
        | None -> None, [], None

    // ---- Ship verdict (feature 092 / ADR-0026: the committed merge-boundary projection) ----

    let shipVerdictPath workId =
        GenerationManifestModule.expectedShipVerdictOutputPath workId

    /// Project the compact verdict and produce (generated-view state, write effect, json text).
    /// Pure over the `ship.json` text alone — unlike the handoff, it needs no work model.
    ///
    /// This is the *single* projection `ship` and `refresh` both call, which is what makes the
    /// two producers byte-identical by construction rather than by golden-file coincidence
    /// (FR-007). A second "equivalent" writer in `HandlersRefresh` would satisfy today's golden
    /// and drift tomorrow.
    let shipVerdictEmission
        workId
        (generator: GeneratorVersion)
        (shipText: string)
        : GeneratedViewState option * CommandEffect list * string option =
        match
            ShipModule.parseShipView
                { Path = shipPath workId
                  Text = shipText }
        with
        | Ok view ->
            let verdictJson = ShipVerdictModule.toJson (ShipVerdictModule.fromShipView view)

            let viewSources = [ analysisSourceFromSnapshot (shipPath workId) shipText ]

            let state =
                generatedViewState
                    (shipVerdictPath workId)
                    "ship-verdict"
                    generator
                    viewSources
                    GeneratedViewCurrency.Current
                    []

            Some state, [ WriteFile(shipVerdictPath workId, verdictJson, GeneratedView) ], Some verdictJson
        | Error _ -> None, [], None

    let shipFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic ->
            sprintf "SF%03d" (index + 1), diagnostic, verifyFindingSeverity diagnostic)

    let existingShipDiagnostic workId model =
        let path = shipPath workId

        match snapshot path model with
        | None -> None
        | Some existing ->
            match parseShipView existing with
            | Error diagnostics ->
                diagnostics
                |> List.tryHead
                |> Option.map (fun diagnostic -> malformedShipView path diagnostic.Message)
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                Some(shipIdentityMismatch path workId view.WorkId.Value)
            | Ok _ -> None

    let shipVerificationPrerequisite workId model =
        let path = verifyPath workId

        match snapshot path model with
        | None -> [ missingVerificationPrerequisite path $"Verification prerequisite '{path}' is missing." ], None
        | Some existing ->
            match parseVerificationView existing with
            | Error diagnostics ->
                (diagnostics
                 |> List.map (fun diagnostic -> malformedVerificationView path diagnostic.Message)),
                None
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                [ verifyIdentityMismatch path workId view.WorkId.Value ], Some view
            | Ok view ->
                let notReady =
                    if not (String.Equals(view.Readiness, "verificationReady", StringComparison.OrdinalIgnoreCase)) then
                        [ verificationNotReady path view.Readiness ]
                    else
                        []

                let blockingFindingIds =
                    view.Findings
                    |> List.filter (fun finding ->
                        String.Equals(finding.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
                    |> List.map (fun finding -> finding.Id)
                    |> List.sort

                let failed =
                    if not (List.isEmpty blockingFindingIds) then
                        [ failedVerification path blockingFindingIds ]
                    else
                        []

                notReady @ failed, Some view

    let shipJson
        (workId: string)
        (generator: GeneratorVersion)
        (readiness: string)
        (disposition: string)
        (sources: GeneratedViewSource list)
        (lifecycleStages: (string * string) list)
        (lifecycleStatus: string)
        (verificationView: VerificationView option)
        (verificationStatus: string)
        (generatedViews: GeneratedViewState list)
        (diagnostics: Diagnostic list)
        =
        let findings = shipFindings diagnostics

        let evidenceCount state =
            match verificationView with
            | Some view ->
                view.EvidenceDispositions
                |> List.filter (fun disposition -> disposition.State = state)
                |> List.length
            | None -> 0

        let _, evidenceSelfAttestedCount, evidenceObservedCount =
            shipEvidenceAttestationCounts verificationView

        writeReadinessEnvelope workId "ship" readiness generator verifySourceKind sources (fun writer ->
            writeLifecycleReadiness writer lifecycleStatus lifecycleStages
            writer.WriteStartObject("verificationReadiness")
            writer.WriteString("status", verificationStatus)

            writeStringArray
                writer
                "blockingFindingIds"
                (match verificationView with
                 | Some view ->
                     view.Findings
                     |> List.filter (fun finding ->
                         String.Equals(finding.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
                     |> List.map (fun finding -> finding.Id)
                 | None -> [])

            writer.WriteNumber("evidenceSupportedCount", evidenceCount EvidenceSupported)
            writer.WriteNumber("evidenceSelfAttestedCount", evidenceSelfAttestedCount)
            writer.WriteNumber("evidenceObservedCount", evidenceObservedCount)
            writer.WriteNumber("evidenceDeferredCount", evidenceCount EvidenceDeferred)
            writer.WriteNumber("evidenceMissingCount", evidenceCount EvidenceMissingDisposition)
            writer.WriteNumber("evidenceStaleCount", evidenceCount EvidenceStale)
            writer.WriteNumber("evidenceSyntheticCount", evidenceCount EvidenceSyntheticDisposition)
            writer.WriteNumber("evidenceInvalidCount", evidenceCount EvidenceInvalid)
            writer.WriteEndObject()
            writer.WriteStartArray("evidenceDispositions")

            (match verificationView with
             | Some view -> view.EvidenceDispositions
             | None -> [])
            |> List.sortBy (fun disposition -> disposition.DispositionId)
            |> List.iter (fun disposition ->
                writer.WriteStartObject()
                writer.WriteString("id", disposition.DispositionId)
                writer.WriteString("obligationId", disposition.ObligationId)
                writer.WriteString("state", shipEvidenceStateValue disposition.State)
                writer.WriteBoolean("observed", disposition.Observed)
                writer.WriteString("severity", disposition.Severity)
                writeStringArray writer "diagnosticIds" disposition.DiagnosticIds
                writer.WriteEndObject())

            writer.WriteEndArray()
            writeGeneratedViewsArray writer generatedViews
            writer.WriteStartObject("disposition")
            writer.WriteString("state", disposition)

            writeStringArray
                writer
                "blockingFindingIds"
                (findings
                 |> List.filter (fun (_, _, severity) -> severity = "blocking")
                 |> List.map (fun (id, _, _) -> id))

            writeStringArray
                writer
                "warningFindingIds"
                (findings
                 |> List.filter (fun (_, _, severity) -> severity = "warning")
                 |> List.map (fun (id, _, _) -> id))

            writeStringArray
                writer
                "advisoryFindingIds"
                (findings
                 |> List.filter (fun (_, _, severity) -> severity = "advisory")
                 |> List.map (fun (id, _, _) -> id))

            writeStringArray
                writer
                "contributingStages"
                (lifecycleStages
                 |> List.filter (fun (_, status) -> status <> "ready")
                 |> List.map fst)

            writer.WriteString(
                "correction",
                if disposition = "shipReady" then
                    ""
                else
                    "Resolve the blocking ship-readiness findings before the protected-boundary handoff."
            )

            writer.WriteEndObject()
            writeGovernanceReadinessTail writer findings diagnostics readiness

            writeNextAction
                writer
                (readiness = "shipReady")
                "ship.next.protectedBoundary"
                "Ship readiness is current and ready for the protected-boundary handoff."
                "Ship found lifecycle diagnostics that must be corrected before the protected-boundary handoff.")

    let computeShipPlan model =
        let ((specification, clarification, checklist, plan, tasks, analysis, shipSummaryOpt),
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

                let existingEvidenceArtifact, existingEvidenceDiagnostics, evidenceText =
                    parseExistingEvidence workId model

                let evidencePresenceDiagnostics =
                    match existingEvidenceArtifact, snapshot (evidencePath workId) model with
                    | None, None ->
                        [ missingEvidencePrerequisite
                              (evidencePath workId)
                              $"Evidence prerequisite '{evidencePath workId}' is missing." ]
                    | _ -> []

                let verificationPrereqDiagnostics, verificationView =
                    shipVerificationPrerequisite workId model

                let shipViewDiagnostics = existingShipDiagnostic workId model |> Option.toList

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
                    @ verificationPrereqDiagnostics
                    @ shipViewDiagnostics
                    |> DiagnosticsModule.sort

                let generatedDiagnostics, workModelView, workModelEffects =
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
                    let readiness = if hasBlocking then "needsShipCorrection" else "shipReady"

                    let disposition =
                        if hasBlocking then
                            "blocked"
                        elif
                            diagnostics
                            |> List.exists (fun diagnostic ->
                                diagnostic.Severity = DiagnosticSeverity.DiagnosticWarning)
                        then
                            "advisory"
                        else
                            "shipReady"

                    let verificationStatus =
                        verificationView
                        |> Option.map (fun view -> view.Readiness)
                        |> Option.defaultValue "needsVerificationCorrection"

                    let analysisViewState =
                        analysis
                        |> Option.map (fun _ ->
                            generatedViewState
                                (analysisPath workId)
                                "analysis"
                                model.Request.GeneratorVersion
                                []
                                GeneratedViewCurrency.Current
                                [])
                        |> Option.defaultValue (
                            generatedViewState
                                (analysisPath workId)
                                "analysis"
                                model.Request.GeneratorVersion
                                []
                                GeneratedViewCurrency.Missing
                                []
                        )

                    let verifyViewState =
                        match verificationView with
                        | Some _ ->
                            generatedViewState
                                (verifyPath workId)
                                "verification"
                                model.Request.GeneratorVersion
                                []
                                GeneratedViewCurrency.Current
                                []
                        | None ->
                            generatedViewState
                                (verifyPath workId)
                                "verification"
                                model.Request.GeneratorVersion
                                []
                                GeneratedViewCurrency.Missing
                                []

                    let stageStatus present = if present then "ready" else "missing"

                    let lifecycleStages =
                        [ "specify", stageStatus (Option.isSome specFacts)
                          "clarify", stageStatus (Option.isSome clarificationFacts)
                          "checklist", stageStatus (Option.isSome checklistFacts)
                          "plan", stageStatus (Option.isSome planFacts)
                          "tasks", stageStatus (Option.isSome taskFacts)
                          "analyze",
                          (match analysis with
                           | Some summary ->
                               (if summary.Readiness = "implementationReady" then
                                    "ready"
                                else
                                    "blocked")
                           | None -> "missing")
                          "evidence", stageStatus (Option.isSome existingEvidenceArtifact)
                          "verify",
                          (if verificationStatus = "verificationReady" then
                               "ready"
                           else
                               "blocked") ]

                    let shipSummaryOpt, shipView, shipHandoffView, shipVerdictView, shipEffects =
                        match specText, clarificationText, checklistText, planText, taskText, analysisText with
                        | Some specText,
                          Some clarificationText,
                          Some checklistText,
                          Some planText,
                          Some taskText,
                          Some analysisText ->
                            let workModelJson = workModelJsonFromGeneratedEffects workId workModelEffects model

                            let baseSources =
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

                            let sources =
                                baseSources
                                @ (snapshot (verifyPath workId) model
                                   |> Option.map (fun snap -> analysisSourceFromSnapshot snap.Path snap.Text)
                                   |> Option.toList)
                                |> List.sortBy (fun source -> source.Path)

                            let generatedViewsForShip = [ workModelView; analysisViewState; verifyViewState ]

                            let text =
                                shipJson
                                    workId
                                    model.Request.GeneratorVersion
                                    readiness
                                    disposition
                                    sources
                                    lifecycleStages
                                    (if hasBlocking then "needsShipCorrection" else "shipReady")
                                    verificationView
                                    verificationStatus
                                    generatedViewsForShip
                                    diagnostics

                            let view =
                                generatedViewState
                                    (shipPath workId)
                                    "ship"
                                    model.Request.GeneratorVersion
                                    sources
                                    GeneratedViewCurrency.Current
                                    []

                            let findings = shipFindings diagnostics

                            let findingCount severity =
                                findings
                                |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity)
                                |> List.length

                            let evidenceCount state =
                                match verificationView with
                                | Some v ->
                                    v.EvidenceDispositions
                                    |> List.filter (fun disposition -> disposition.State = state)
                                    |> List.length
                                | None -> 0

                            let _, evidenceSelfAttestedCount, evidenceObservedCount =
                                shipEvidenceAttestationCounts verificationView

                            let summary: ShipSummary =
                                { WorkId = workId
                                  Stage = "ship"
                                  Status = readiness
                                  ShipPath = shipPath workId
                                  FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                                  ReadyFindingCount = findingCount "ready"
                                  AdvisoryCount = findingCount "advisory"
                                  WarningCount = findingCount "warning"
                                  BlockingCount = findingCount "blocking"
                                  Disposition = disposition
                                  LifecycleStageReadiness = lifecycleStages
                                  VerificationReadiness = verificationStatus
                                  EvidenceSupportedCount = evidenceCount EvidenceSupported
                                  EvidenceSelfAttestedCount = evidenceSelfAttestedCount
                                  EvidenceObservedCount = evidenceObservedCount
                                  EvidenceDeferredCount = evidenceCount EvidenceDeferred
                                  EvidenceMissingCount = evidenceCount EvidenceMissingDisposition
                                  EvidenceStaleCount = evidenceCount EvidenceStale
                                  EvidenceSyntheticCount = evidenceCount EvidenceSyntheticDisposition
                                  EvidenceInvalidCount = evidenceCount EvidenceInvalid
                                  GeneratedViewState = (if hasBlocking then "blocked" else "current")
                                  SourceSnapshotCount = sources.Length
                                  Readiness = readiness }

                            // --- Governance handoff: additive, emitted alongside ship.json ---
                            let handoffView, handoffEffects, _ =
                                if hasBlocking then
                                    None, [], None
                                else
                                    governanceHandoffEmission
                                        workId
                                        model.Request.GeneratorVersion
                                        workModelJson
                                        (snapshot (verifyPath workId) model |> Option.map _.Text)
                                        text
                                        (governanceConfigPresence model)

                            // --- Ship verdict: the committed merge-boundary projection (ADR-0026).
                            // Inside the same `not hasBlocking` gate as ship.json, so an incomplete
                            // ship is never recorded as a verdict (FR-005) without a new branch.
                            let verdictView, verdictEffects, _ =
                                if hasBlocking then
                                    None, [], None
                                else
                                    shipVerdictEmission workId model.Request.GeneratorVersion text

                            let effects =
                                if hasBlocking then
                                    []
                                else
                                    [ CreateDirectory(readinessDirectory workId)
                                      WriteFile(shipPath workId, text, GeneratedView) ]
                                    @ verdictEffects
                                    @ handoffEffects

                            Some summary, Some view, handoffView, verdictView, effects
                        | _ -> None, None, None, None, []

                    let generatedViews =
                        [ Some workModelView
                          Some analysisViewState
                          Some verifyViewState
                          shipView
                          shipVerdictView
                          shipHandoffView ]
                        |> List.choose id

                    (specification, clarification, checklist, plan, tasks, analysis, shipSummaryOpt),
                    generatedViews,
                    workModelEffects,
                    shipEffects))

        diagnostics,
        specification,
        clarification,
        checklist,
        plan,
        tasks,
        analysis,
        None,
        None,
        shipSummaryOpt,
        generatedViews,
        effects
