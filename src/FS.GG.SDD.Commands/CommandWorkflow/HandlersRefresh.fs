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

[<AutoOpen>]
module internal HandlersRefresh =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    // --- refresh orchestration (cross-cutting; reuses the per-view generators) ---

    let refreshCanonicalViews =
        [ "work-model"
          "analysis"
          "verify"
          "ship"
          "governance-handoff"
          "agent-commands"
          "summary" ]

    // 056 skill fan-out re-mirror (FR-009): refresh brings the multi-root union to
    // currency independent of the work-model views. The non-reserved provider skills under
    // the provider-owned source root (the reserved `fs-gg-sdd-*` namespace is SDD's, seeded
    // separately) are discovered from the enumerated listing and mirrored byte-identically
    // into every other declared root. 058/ADR-0014 §Decision 5: the destination roots derive
    // from the one `agentSkillRoots` constant through the shared `SkillMirror`, not a hardcoded
    // `.claude`/`.codex` list. Re-mirror is no-clobber (`AgentGuidanceTarget`), so a deleted
    // copy is refilled and an author edit is preserved — the same policy as the seeded re-seed.
    let private agentsSkillsRoot = Fsgg.SkillMirror.providerSourceRoot + "/skills"

    let private providerSkillFilesFromListing model =
        (directoryListing agentsSkillsRoot model).Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (fun path ->
            path.StartsWith(agentsSkillsRoot + "/", StringComparison.Ordinal)
            && not (
                path.StartsWith(Fsgg.SkillMirror.providerSourceRoot + "/skills/fs-gg-sdd-", StringComparison.Ordinal)
            ))
        |> Array.sort
        |> Array.toList

    /// Two-phase candidate reads (like `duplicateCandidateReadEffects`): once `.agents/skills`
    /// is enumerated, read each non-reserved provider skill body not yet snapshotted, so the
    /// re-mirror step has the exact bytes to fan out.
    let providerSkillMirrorReads model =
        providerSkillFilesFromListing model
        |> List.filter (fun path -> Option.isNone (snapshot path model))
        |> List.map ReadFile

    /// The re-seed (all three roots, no-clobber) + re-mirror (provider copies into
    /// `.claude`/`.codex`, no-clobber) effects that keep the union current on every refresh.
    let skillFanoutRefreshEffects model =
        let mirrorTargetRoots =
            Fsgg.SkillMirror.mirrorTargetRoots Fsgg.Schemas.agentSkillRoots

        let reMirror =
            providerSkillFilesFromListing model
            |> List.collect (fun src ->
                match snapshot src model with
                | Some snap ->
                    mirrorTargetRoots
                    |> List.map (fun targetRoot ->
                        WriteFile(Fsgg.SkillMirror.retargetSkillPath targetRoot src, snap.Text, AgentGuidanceTarget))
                | None -> [])

        SeededSkills.skillEffects @ reMirror

    let refreshSummaryMarkdown
        (workId: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (stage: string)
        (outcomeText: string)
        (disposition: string)
        (perViewState: (string * string) list)
        (diagnostics: Diagnostic list)
        (nextActionText: string)
        =
        let body = StringBuilder()
        body.AppendLine($"# Readiness Summary — {workId}") |> ignore
        body.AppendLine("") |> ignore

        body.AppendLine($"**Lifecycle stage**: {stage}  **Outcome**: {outcomeText}  **Disposition**: {disposition}")
        |> ignore

        body.AppendLine("") |> ignore
        body.AppendLine("## Generated-view currency") |> ignore
        body.AppendLine("| View | State |") |> ignore
        body.AppendLine("|---|---|") |> ignore

        perViewState
        |> List.iter (fun (view, state) -> body.AppendLine($"| {view} | {state} |") |> ignore)

        body.AppendLine("") |> ignore
        body.AppendLine("## Diagnostics") |> ignore

        if List.isEmpty diagnostics then
            body.AppendLine("None") |> ignore
        else
            diagnostics
            |> DiagnosticsModule.sort
            |> List.iter (fun diagnostic ->
                let path =
                    diagnostic.Artifact
                    |> Option.map (fun artifact -> artifact.Path)
                    |> Option.defaultValue "-"

                body.AppendLine(
                    $"- {diagnostic.Id} ({DiagnosticsModule.severityValue diagnostic.Severity}) {path}: {diagnostic.Message} — {diagnostic.Correction}"
                )
                |> ignore)

        body.AppendLine("") |> ignore
        body.AppendLine("## Next action") |> ignore
        body.AppendLine(nextActionText) |> ignore
        let bodyText = body.ToString()
        let bodyDigest = (SchemaVersionModule.sha256Text bodyText).Value

        let header = StringBuilder()
        header.AppendLine("<!-- GENERATED by fsgg-sdd refresh — DO NOT EDIT.") |> ignore

        header.AppendLine($"     view: summary  schemaVersion: 1  generator: {generator.Id}/{generator.Version}")
        |> ignore

        header.AppendLine("     sources:") |> ignore

        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            let digest =
                source.Digest
                |> Option.map (fun value -> value.Value)
                |> Option.defaultValue "none"

            let schema = source.SchemaVersion |> Option.map string |> Option.defaultValue "none"
            let status = source.SchemaStatus |> Option.defaultValue "unknown"

            header.AppendLine($"       - {source.Path}  digest:{digest}  schema:{schema}({status})")
            |> ignore)

        header.AppendLine($"     outputDigest: {bodyDigest} -->") |> ignore
        header.AppendLine("") |> ignore
        header.ToString() + bodyText

    let computeRefreshPlan model =
        match model.Request.WorkId with
        | None -> model.Diagnostics, None, [], []
        | Some workId ->
            let request = model.Request
            let summaryPath = GenerationManifestModule.expectedSummaryOutputPath workId
            let projectDiags = projectDiagnostics model
            let duplicateDiags = duplicateWorkIdDiagnostics workId model

            // Scaffold-produced files are externally owned (FR-007): they are never SDD
            // generated views, so they are excluded from the refresh ledger by
            // construction. Malformed provenance is surfaced and treated as absent
            // (fail-safe) rather than silently regenerating anything (SC-007).
            let provenanceDiags =
                match snapshot ScaffoldProvenance.provenancePath model with
                | Some provenanceSnapshot ->
                    match ScaffoldProvenance.tryParse provenanceSnapshot.Text with
                    | Some _ -> []
                    | None -> [ scaffoldProvenanceMalformed ScaffoldProvenance.provenancePath ]
                | None -> []

            let baseDiags = model.Diagnostics @ projectDiags @ duplicateDiags @ provenanceDiags

            let baseBlocking =
                baseDiags
                |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)

            // The authored sources the structured views are derived from.
            let authoredSourcePaths = [ specPath workId; tasksPath workId; evidencePath workId ]

            let authoredPreserved =
                [ charterPath workId
                  specPath workId
                  clarificationPath workId
                  checklistPath workId
                  planPath workId
                  tasksPath workId
                  evidencePath workId
                  ".fsgg/project.yml"
                  ".fsgg/sdd.yml"
                  ".fsgg/agents.yml" ]
                |> List.filter (fun path -> Option.isSome (snapshot path model))

            if baseBlocking then
                let perViewState = refreshCanonicalViews |> List.map (fun view -> view, "blocked")

                let summary: RefreshSummary =
                    { WorkId = workId
                      Stage = "refresh"
                      Status = "blocked"
                      SummaryPath = summaryPath
                      RefreshedViewIds = []
                      AlreadyCurrentViewIds = []
                      BlockedViewIds = refreshCanonicalViews
                      NotApplicableViewIds = []
                      PreservedAuthoredPaths = authoredPreserved
                      FindingIds =
                        baseDiags
                        |> List.map (fun diagnostic -> diagnostic.Id)
                        |> List.distinct
                        |> List.sort
                      AdvisoryCount = 0
                      WarningCount = 0
                      BlockingCount =
                        baseDiags
                        |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
                        |> List.length
                      Disposition = refreshDispositionValue RefreshBlocked
                      PerViewState = perViewState
                      SourceSnapshotCount = 0
                      Readiness = "needsRefreshCorrection" }

                (baseDiags |> DiagnosticsModule.sort), Some summary, [], []
            elif
                Option.isNone (snapshot (workModelPath workId) model)
                && (authoredSourcePaths
                    |> List.exists (fun path -> Option.isNone (snapshot path model)))
            then
                // Early-stage (FR-005/FR-010b): the work model is absent AND its authored
                // sources have not been written yet — the expected pre-work-model state, not
                // a defect. Report a navigable advisory that points to the seeded static
                // guidance; regenerate nothing and write no view (FR-008/FR-011). A *present*
                // work model whose source was later deleted (workModelAbsent = false), or a
                // malformed source, is NOT early-stage and still blocks in the else arm.
                let earlyDiag = refreshEarlyStageGuidance (earlyStagePresentStages workId model)

                let perViewState =
                    refreshCanonicalViews |> List.map (fun view -> view, "early-stage")

                let summary: RefreshSummary =
                    { WorkId = workId
                      Stage = "refresh"
                      Status = "early-stage"
                      SummaryPath = summaryPath
                      RefreshedViewIds = []
                      AlreadyCurrentViewIds = []
                      BlockedViewIds = []
                      NotApplicableViewIds = refreshCanonicalViews
                      PreservedAuthoredPaths = authoredPreserved
                      FindingIds = [ earlyDiag.Id ]
                      AdvisoryCount = 1
                      WarningCount = 0
                      BlockingCount = 0
                      Disposition = "early-stage"
                      PerViewState = perViewState
                      SourceSnapshotCount = 0
                      Readiness = "refreshEarlyStage" }

                ((baseDiags @ [ earlyDiag ]) |> DiagnosticsModule.sort), Some summary, [], []
            else
                let writeTextIn (effects: CommandEffect list) path =
                    effects
                    |> List.tryPick (function
                        | WriteFile(p, text, _) when normalizeRelativePath p = normalizeRelativePath path -> Some text
                        | _ -> None)

                let injectSnapshot path text (m: CommandModel) =
                    let key = readEffectKey path

                    let injected: CommandEffectResult =
                        { Effect = ReadFile path
                          Succeeded = true
                          Snapshot = Some { Path = path; Text = text }
                          Process = None
                          Confirmed = None
                          Diagnostic = None }

                    { m with
                        InterpretedEffects =
                            (m.InterpretedEffects
                             |> List.filter (fun result -> effectKey result.Effect <> key))
                            @ [ injected ] }

                let textOf path =
                    snapshot path model |> Option.map (fun snap -> snap.Text)

                let parsesAsJson (text: string) =
                    try
                        use _ = System.Text.Json.JsonDocument.Parse text
                        true
                    with _ ->
                        false

                // 1. Regenerate the normalized work model from its current declared sources.
                //    Reusing the same generator the lifecycle uses keeps output byte-identical.
                let wmDiags, wmView, wmEffects =
                    generatedViewPlan
                        request
                        workId
                        None
                        (textOf (specPath workId))
                        (textOf (clarificationPath workId))
                        (textOf (checklistPath workId))
                        (textOf (planPath workId))
                        (textOf (tasksPath workId))
                        (textOf (evidencePath workId))
                        []
                        model

                let wmWriteText = writeTextIn wmEffects (workModelPath workId)

                // Refreshed | already-current | blocked for the work model.
                let wmClass =
                    match wmWriteText with
                    | None -> "blocked"
                    | Some text ->
                        match snapshot (workModelPath workId) model with
                        | Some existing when existing.Text = text -> "already-current"
                        | _ -> "refreshed"

                let wmChanged = wmClass = "refreshed"

                // 2. Regenerate agent guidance from the refreshed work model (declared
                //    source-of order: the work model feeds agent guidance).
                let modelForAgents =
                    match wmWriteText with
                    | Some text when wmClass <> "blocked" -> injectSnapshot (workModelPath workId) text model
                    | _ -> model

                let _agDiag, _, agViews, agEffects =
                    if wmClass = "blocked" then
                        [], None, [], []
                    else
                        computeAgentsPlan modelForAgents

                let agentGuidancePaths = agViews |> List.map (fun view -> view.Path)

                let agentApplicable =
                    match agentsConfigOpt model with
                    | Some config -> not (List.isEmpty config.Targets)
                    | None -> false

                let agentBlocked =
                    wmClass = "blocked"
                    || (agViews
                        |> List.exists (fun view -> view.Currency = GeneratedViewCurrency.Blocked))

                let agentGuidanceWriteText path = writeTextIn agEffects path

                let agentClass =
                    if not agentApplicable then
                        "not-applicable"
                    elif agentBlocked then
                        "blocked"
                    elif List.isEmpty agentGuidancePaths then
                        "blocked"
                    elif
                        agentGuidancePaths
                        |> List.forall (fun path ->
                            match agentGuidanceWriteText path with
                            | Some text -> (snapshot path model |> Option.map (fun snap -> snap.Text)) = Some text
                            | None -> true)
                    then
                        "already-current"
                    else
                        "refreshed"

                // 3. Evaluate currency of the structured downstream views (analysis,
                //    verify, ship). These are reported, not destructively regenerated:
                //    re-running their generators out of lifecycle order invalidates the
                //    evidence freshness they were verified against. If the work model
                //    changed, they are reported stale and point back to the responsible
                //    lifecycle command.
                let downstreamClass path =
                    if wmClass = "blocked" then
                        "blocked"
                    else
                        match snapshot path model with
                        | None -> "missing"
                        | Some snap when not (parsesAsJson snap.Text) -> "malformed"
                        | Some _ -> if wmChanged then "stale" else "already-current"

                let anClass = downstreamClass (analysisPath workId)
                let veClass = downstreamClass (verifyPath workId)
                let shClass = downstreamClass (shipPath workId)

                // Governance handoff currency. The handoff is a pure projection over the work
                // model + verify/ship, so refresh CAN faithfully regenerate it — but only when its
                // ship source is itself current (regenerating against a stale ship would mix
                // versions). When ship is stale/missing/malformed/blocked, the handoff inherits
                // that state; when ship is clean, the handoff is re-projected and restored.
                let govView, govEffects, govClass =
                    let inheritShip () = None, [], shClass

                    if wmClass = "blocked" then
                        None, [], "blocked"
                    else
                        match shClass with
                        | "already-current" ->
                            let wmText =
                                wmWriteText |> Option.orElseWith (fun () -> textOf (workModelPath workId))

                            match wmText, textOf (shipPath workId) with
                            | Some _, Some shipText ->
                                let view, effects, jsonOpt =
                                    governanceHandoffEmission
                                        workId
                                        request.GeneratorVersion
                                        wmText
                                        (textOf (verifyPath workId))
                                        shipText
                                        (governanceConfigPresence model)

                                match jsonOpt with
                                | Some json ->
                                    let existing = snapshot (governanceHandoffPath workId) model

                                    match existing with
                                    | Some snap when snap.Text = json -> view, [], "already-current"
                                    | _ -> view, effects, "refreshed"
                                | None -> None, [], "blocked"
                            | _ -> None, [], "missing"
                        | _ -> inheritShip ()

                let structuredClasses =
                    [ "work-model", wmClass
                      "analysis", anClass
                      "verify", veClass
                      "ship", shClass ]

                let isClean state =
                    state = "refreshed" || state = "already-current"

                let structuredAllClean =
                    structuredClasses |> List.forall (fun (_, state) -> isClean state)

                let structuredNoneClean =
                    structuredClasses |> List.forall (fun (_, state) -> not (isClean state))

                // --- refresh-specific diagnostics ---
                let missingAuthored =
                    authoredSourcePaths
                    |> List.filter (fun path -> Option.isNone (snapshot path model))

                let workModelDiags =
                    if wmClass = "blocked" then
                        match missingAuthored with
                        | missing :: _ -> [ refreshMissingSource (workModelPath workId) missing ]
                        | [] ->
                            [ refreshMalformedSource
                                  (workModelPath workId)
                                  (specPath workId)
                                  $"A declared source for '{workModelPath workId}' is malformed or schema-incompatible." ]
                    elif wmChanged then
                        match snapshot (workModelPath workId) model with
                        | Some existing ->
                            match
                                GenerationManifestModule.parseWorkModelMetadata (workModelPath workId) existing.Text
                            with
                            | Error _ ->
                                [ refreshMalformedGeneratedView
                                      (workModelPath workId)
                                      $"Generated view '{workModelPath workId}' was unreadable and was refreshed from current sources." ]
                            | Ok _ -> []
                        | None -> []
                    else
                        []

                let downstreamDiags =
                    [ analysisPath workId, anClass
                      verifyPath workId, veClass
                      shipPath workId, shClass ]
                    |> List.collect (fun (viewPath, state) ->
                        match state with
                        | "blocked" -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | "stale" -> [ refreshStaleView viewPath [ workModelPath workId ] ]
                        | "malformed" ->
                            [ refreshMalformedGeneratedView
                                  viewPath
                                  $"Generated view '{viewPath}' is malformed; re-run the responsible lifecycle command." ]
                        | "missing" -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | _ -> [])

                let summaryRenderable = structuredAllClean

                let summaryDiags =
                    if summaryRenderable then
                        []
                    else
                        let related =
                            structuredClasses
                            |> List.filter (fun (_, state) -> not (isClean state))
                            |> List.map fst

                        [ refreshUnrenderableSummary summaryPath related ]

                let refreshDiags = workModelDiags @ downstreamDiags @ summaryDiags

                let allDiags =
                    (baseDiags @ refreshDiags)
                    |> List.distinctBy (fun diagnostic -> diagnostic.Id, diagnostic.Message)
                    |> DiagnosticsModule.sort

                // --- summary projection ---
                let structuredSourcePaths =
                    [ workModelPath workId
                      analysisPath workId
                      verifyPath workId
                      shipPath workId ]
                    @ agentGuidancePaths

                let summarySources =
                    structuredSourcePaths
                    |> List.choose (fun path ->
                        let textOpt =
                            match writeTextIn (wmEffects @ agEffects) path with
                            | Some text -> Some text
                            | None -> snapshot path model |> Option.map (fun snap -> snap.Text)

                        textOpt
                        |> Option.map (fun text ->
                            { Path = path
                              Digest = Some(SchemaVersionModule.sha256Text text)
                              SchemaVersion = Some 1
                              SchemaStatus = Some "current" }))

                let stageText = "refresh"

                let outcomeText =
                    if structuredAllClean && agentClass <> "blocked" then
                        "succeeded"
                    else
                        "succeededWithWarnings"

                let disposition =
                    if wmClass = "blocked" || structuredNoneClean then
                        RefreshBlocked
                    elif
                        structuredAllClean
                        && (agentClass = "refreshed"
                            || agentClass = "already-current"
                            || agentClass = "not-applicable")
                    then
                        RefreshedCurrent
                    else
                        PartiallyBlocked

                let dispositionValue = refreshDispositionValue disposition

                // currency word per view for the report and summary table
                let viewWord state =
                    match state with
                    | "refreshed"
                    | "already-current" -> "current"
                    | other -> other

                let perViewState =
                    [ "work-model", viewWord wmClass
                      "analysis", viewWord anClass
                      "verify", viewWord veClass
                      "ship", viewWord shClass
                      "governance-handoff", viewWord govClass
                      "agent-commands", viewWord agentClass
                      "summary", (if summaryRenderable then "current" else "blocked") ]

                let summaryClass, summaryEffects, summaryViewState =
                    if not summaryRenderable then
                        "blocked", [], None
                    else
                        let nextActionText =
                            match disposition with
                            | RefreshedCurrent ->
                                "Generated views are current; rely on the refreshed readiness for the selected work item."
                            | _ -> "Correct the named source or upstream view, then re-run fsgg-sdd refresh."

                        let text =
                            refreshSummaryMarkdown
                                workId
                                request.GeneratorVersion
                                summarySources
                                stageText
                                outcomeText
                                dispositionValue
                                perViewState
                                allDiags
                                nextActionText

                        let cls =
                            match snapshot summaryPath model with
                            | Some existing when existing.Text = text -> "already-current"
                            | _ -> "refreshed"

                        let effects =
                            [ CreateDirectory(readinessDirectory workId)
                              WriteFile(summaryPath, text, GeneratedView) ]

                        let view =
                            { Path = summaryPath
                              Kind = "summary"
                              SchemaVersion = Some 1
                              Generator = Some request.GeneratorVersion
                              Sources = summarySources
                              OutputDigest = None
                              Currency = GeneratedViewCurrency.Current
                              DiagnosticIds = [] }

                        cls, effects, Some view

                let classifyToBucket viewId state buckets =
                    let refreshed, current, blocked, na = buckets

                    match state with
                    | "refreshed" -> viewId :: refreshed, current, blocked, na
                    | "already-current" -> refreshed, viewId :: current, blocked, na
                    | "not-applicable" -> refreshed, current, blocked, viewId :: na
                    | _ -> refreshed, current, viewId :: blocked, na

                let refreshedViewIds, alreadyCurrentViewIds, blockedViewIds, notApplicableViewIds =
                    [ "work-model", wmClass
                      "analysis", anClass
                      "verify", veClass
                      "ship", shClass
                      "governance-handoff", govClass
                      "agent-commands", agentClass
                      "summary", summaryClass ]
                    |> List.fold (fun acc (viewId, state) -> classifyToBucket viewId state acc) ([], [], [], [])

                let findingSeverityCount severity =
                    refreshDiags
                    |> List.filter (fun diagnostic -> diagnostic.Severity = severity)
                    |> List.length

                let sourceSnapshotCount =
                    [ workModelPath workId
                      analysisPath workId
                      verifyPath workId
                      shipPath workId ]
                    |> List.filter (fun path -> Option.isSome (snapshot path model))
                    |> List.length

                let summaryRecord: RefreshSummary =
                    { WorkId = workId
                      Stage = stageText
                      Status = dispositionValue
                      SummaryPath = summaryPath
                      RefreshedViewIds = refreshedViewIds |> List.sort
                      AlreadyCurrentViewIds = alreadyCurrentViewIds |> List.sort
                      BlockedViewIds = blockedViewIds |> List.sort
                      NotApplicableViewIds = notApplicableViewIds |> List.sort
                      PreservedAuthoredPaths = authoredPreserved |> List.sort
                      FindingIds =
                        refreshDiags
                        |> List.map (fun diagnostic -> diagnostic.Id)
                        |> List.distinct
                        |> List.sort
                      AdvisoryCount = findingSeverityCount DiagnosticSeverity.DiagnosticInfo
                      WarningCount = findingSeverityCount DiagnosticSeverity.DiagnosticWarning
                      BlockingCount = findingSeverityCount DiagnosticSeverity.DiagnosticError
                      Disposition = dispositionValue
                      PerViewState = perViewState
                      SourceSnapshotCount = sourceSnapshotCount
                      Readiness =
                        if disposition = RefreshBlocked then
                            "needsRefreshCorrection"
                        else
                            "refreshReady" }

                // --- canonical generated-view set ---
                let currencyOf state =
                    match state with
                    | "refreshed"
                    | "already-current" -> GeneratedViewCurrency.Current
                    | "stale" -> GeneratedViewCurrency.Stale
                    | "malformed" -> GeneratedViewCurrency.Malformed
                    | "missing" -> GeneratedViewCurrency.Missing
                    | _ -> GeneratedViewCurrency.Blocked

                let downstreamView path kind state =
                    { Path = path
                      Kind = kind
                      SchemaVersion = Some 1
                      Generator = Some request.GeneratorVersion
                      Sources = []
                      OutputDigest = None
                      Currency = currencyOf state
                      DiagnosticIds = [] }

                let workModelViewState =
                    { wmView with
                        Currency = currencyOf wmClass }

                let agentViewStates =
                    agViews
                    |> List.map (fun view ->
                        let state =
                            match agentGuidanceWriteText view.Path with
                            | Some text ->
                                if (snapshot view.Path model |> Option.map (fun snap -> snap.Text)) = Some text then
                                    "already-current"
                                else
                                    "refreshed"
                            | None -> "blocked"

                        { view with
                            Currency = currencyOf state })

                let governanceHandoffViewState =
                    match govView with
                    | Some view ->
                        { view with
                            Currency = currencyOf govClass }
                    | None -> downstreamView (governanceHandoffPath workId) "governance-handoff" govClass

                let generatedViews =
                    [ workModelViewState
                      downstreamView (analysisPath workId) "analysis" anClass
                      downstreamView (verifyPath workId) "verification" veClass
                      downstreamView (shipPath workId) "ship" shClass
                      governanceHandoffViewState ]
                    @ agentViewStates
                    @ (summaryViewState |> Option.toList)

                let dedupEffects effects =
                    effects
                    |> List.fold
                        (fun (seen, acc) effect ->
                            let key = effectKey effect

                            if Set.contains key seen then
                                seen, acc
                            else
                                Set.add key seen, acc @ [ effect ])
                        (Set.empty, [])
                    |> snd

                let effects = dedupEffects (wmEffects @ agEffects @ govEffects @ summaryEffects)

                // wmDiags are the reused generator's own staleness heuristics about the
                // prior on-disk work model; refresh reports its own per-view diagnostics
                // (allDiags), so the generator's internal diagnostics are not surfaced.
                ignore wmDiags

                allDiags, Some summaryRecord, generatedViews, effects
