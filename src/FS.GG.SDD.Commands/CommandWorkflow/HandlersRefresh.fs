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
open FS.GG.SDD.Commands.Internal.HandlersShip
open FS.GG.SDD.Commands.Internal.HandlersAgents

module internal HandlersRefresh =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module ShipModule = FS.GG.SDD.Artifacts.Ship

    // Feature 068 / US2 (2b): the internal per-view currency classification, formerly raw strings
    // woven through computeRefreshPlan (the review's worst complexity hotspot). Purely internal
    // working state — it lives on no DTO (RefreshSummary.PerViewState stays a (string * string)
    // list), so this adds no .fsi/baseline surface. RequireQualifiedAccess is required because
    // Blocked/Missing/Stale/Malformed collide with GeneratedViewCurrency's cases.
    [<RequireQualifiedAccess>]
    type private ViewCurrencyClass =
        | Refreshed
        | AlreadyCurrent
        | Blocked
        | NotApplicable
        | Missing
        | Malformed
        | Stale
        | EarlyStage

    /// Clean = regenerated this run or already up to date.
    let private viewCurrencyIsClean =
        function
        | ViewCurrencyClass.Refreshed
        | ViewCurrencyClass.AlreadyCurrent -> true
        | _ -> false

    /// The display word emitted in perViewState / summary.md — the two clean states collapse to
    /// "current"; the rest pass through as their kebab spelling (byte-identical to the former
    /// `viewWord`). Formerly the raw strings; the mapping is the wire contract.
    let private viewCurrencyDisplay =
        function
        | ViewCurrencyClass.Refreshed
        | ViewCurrencyClass.AlreadyCurrent -> "current"
        | ViewCurrencyClass.Blocked -> "blocked"
        | ViewCurrencyClass.NotApplicable -> "not-applicable"
        | ViewCurrencyClass.Missing -> "missing"
        | ViewCurrencyClass.Malformed -> "malformed"
        | ViewCurrencyClass.Stale -> "stale"
        | ViewCurrencyClass.EarlyStage -> "early-stage"

    /// Projection onto the persisted GeneratedViewCurrency DU for the canonical view set
    /// (byte-identical to the former `currencyOf`).
    let private viewCurrencyToGenerated =
        function
        | ViewCurrencyClass.Refreshed
        | ViewCurrencyClass.AlreadyCurrent -> GeneratedViewCurrency.Current
        | ViewCurrencyClass.Stale -> GeneratedViewCurrency.Stale
        | ViewCurrencyClass.Malformed -> GeneratedViewCurrency.Malformed
        | ViewCurrencyClass.Missing -> GeneratedViewCurrency.Missing
        | _ -> GeneratedViewCurrency.Blocked

    // --- refresh orchestration (cross-cutting; reuses the per-view generators) ---

    let refreshCanonicalViews =
        [ "work-model"
          "analysis"
          "verify"
          "ship"
          "ship-verdict"
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

        SeededSkills.skillEffects () @ reMirror

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
                    refreshCanonicalViews
                    |> List.map (fun view -> view, viewCurrencyDisplay ViewCurrencyClass.EarlyStage)

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
                      Disposition = refreshDispositionValue RefreshDisposition.EarlyStage
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

                // Feature 095 (FS.GG.SDD#188): `downstreamClass` validates each structured downstream
                // view with one of these. They share a shape so the validator can be a parameter rather
                // than a branch on the artifact's identity — which keeps "analysis and verify are
                // unchanged" (FR-002) a call-site fact instead of a runtime accident.
                let parsesAsJsonSnap (snap: FileSnapshot) = parsesAsJson snap.Text

                /// STRICTLY STRONGER than `parsesAsJsonSnap`: a non-JSON body fails inside
                /// `parseJsonView` before any field is read, so this subsumes the weaker check rather
                /// than supplementing it. Syntax is not the contract — `ship.json`'s contract is a
                /// schema, and a future `schemaVersion` / bad `workId` / unparseable `stage` is
                /// malformed *as a ship view* however well-formed its JSON. `parseShipView` takes the
                /// `FileSnapshot` we already hold, so this costs no re-read.
                ///
                /// This adopts the artifact layer's schema-compatibility policy VERBATIM — it does not
                /// invent one. `parseJsonView` builds the view for `Current` *and* `Deprecated` status,
                /// so a deprecated-but-supported `ship.json` still reports `current` (FR-016, and a test
                /// pins it). Only what the artifact layer already refuses to read — `Unsupported`,
                /// `Future`, a missing/unparseable `schemaVersion`, or a structurally invalid view —
                /// becomes `Malformed` here.
                let parsesAsShipView (snap: FileSnapshot) =
                    ShipModule.parseShipView snap |> Result.isOk

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
                    | None -> ViewCurrencyClass.Blocked
                    | Some text ->
                        match snapshot (workModelPath workId) model with
                        | Some existing when existing.Text = text -> ViewCurrencyClass.AlreadyCurrent
                        | _ -> ViewCurrencyClass.Refreshed

                let wmChanged = wmClass = ViewCurrencyClass.Refreshed

                // 2. Regenerate agent guidance from the refreshed work model (declared
                //    source-of order: the work model feeds agent guidance).
                let modelForAgents =
                    match wmWriteText with
                    | Some text when wmClass <> ViewCurrencyClass.Blocked ->
                        injectSnapshot (workModelPath workId) text model
                    | _ -> model

                let _agDiag, _, agViews, agEffects =
                    if wmClass = ViewCurrencyClass.Blocked then
                        [], None, [], []
                    else
                        computeAgentsPlan modelForAgents

                let agentGuidancePaths = agViews |> List.map (fun view -> view.Path)

                let agentApplicable =
                    match agentsConfigOpt model with
                    | Some config -> not (List.isEmpty config.Targets)
                    | None -> false

                let agentBlocked =
                    wmClass = ViewCurrencyClass.Blocked
                    || (agViews
                        |> List.exists (fun view -> view.Currency = GeneratedViewCurrency.Blocked))

                let agentGuidanceWriteText path = writeTextIn agEffects path

                let agentClass =
                    if not agentApplicable then
                        ViewCurrencyClass.NotApplicable
                    elif agentBlocked then
                        ViewCurrencyClass.Blocked
                    elif List.isEmpty agentGuidancePaths then
                        ViewCurrencyClass.Blocked
                    elif
                        agentGuidancePaths
                        |> List.forall (fun path ->
                            match agentGuidanceWriteText path with
                            | Some text -> (snapshot path model |> Option.map (fun snap -> snap.Text)) = Some text
                            | None -> true)
                    then
                        ViewCurrencyClass.AlreadyCurrent
                    else
                        ViewCurrencyClass.Refreshed

                // 3. Evaluate currency of the structured downstream views (analysis,
                //    verify, ship). These are reported, not destructively regenerated:
                //    re-running their generators out of lifecycle order invalidates the
                //    evidence freshness they were verified against. If the work model
                //    changed, they are reported stale and point back to the responsible
                //    lifecycle command.
                //    `isValid` is per-artifact (feature 095): each view is validated against ITS OWN
                //    contract, so the `Malformed` word lands on the artifact that is actually malformed.
                //    Note the validator never runs when the work model is blocked — the short-circuit
                //    below precedes it.
                let downstreamClass (isValid: FileSnapshot -> bool) path =
                    if wmClass = ViewCurrencyClass.Blocked then
                        ViewCurrencyClass.Blocked
                    else
                        match snapshot path model with
                        | None -> ViewCurrencyClass.Missing
                        | Some snap when not (isValid snap) -> ViewCurrencyClass.Malformed
                        | Some _ ->
                            if wmChanged then
                                ViewCurrencyClass.Stale
                            else
                                ViewCurrencyClass.AlreadyCurrent

                // analysis/verify keep the weaker JSON-syntax gate: each would need its own oracle,
                // its own state matrix, and its own regression sweep (feature 095, spec §Out of Scope).
                let anClass = downstreamClass parsesAsJsonSnap (analysisPath workId)
                let veClass = downstreamClass parsesAsJsonSnap (verifyPath workId)

                // `ship.json` is validated as a SHIP VIEW. Before feature 095 this was `parsesAsJson`,
                // so a valid-JSON/invalid-view source read as `AlreadyCurrent` — reporting `ship: current`
                // about a file that does not parse, and then stamping `Malformed` on the well-formed
                // COMMITTED verdict when the projection below inevitably failed. Both facts inverted.
                let shClass = downstreamClass parsesAsShipView (shipPath workId)

                // Governance handoff currency. The handoff is a pure projection over the work
                // model + verify/ship, so refresh CAN faithfully regenerate it — but only when its
                // ship source is itself current (regenerating against a stale ship would mix
                // versions). When ship is stale/missing/blocked, the handoff inherits that state;
                // when ship is clean, the handoff is re-projected and restored.
                //
                // Feature 095 (FR-017): `Malformed` is the one class the handoff must NOT inherit.
                // `Malformed` is a statement about a file's own bytes, and the handoff's bytes are fine —
                // it is its SOURCE that will not parse. The handoff is therefore `Blocked`: "cannot be
                // refreshed until upstream `ship.json` is current", which is exactly true. This is the
                // same false-attribution that FR-004 removes from `ship-verdict`, one artifact over; it
                // would be incoherent to fix the verdict and leave the handoff lying in the same report.
                let govView, govEffects, govClass =
                    let inheritShip () =
                        let inherited =
                            if shClass = ViewCurrencyClass.Malformed then
                                ViewCurrencyClass.Blocked
                            else
                                shClass

                        None, [], inherited

                    if wmClass = ViewCurrencyClass.Blocked then
                        None, [], ViewCurrencyClass.Blocked
                    else
                        match shClass with
                        | ViewCurrencyClass.AlreadyCurrent ->
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
                                    | Some snap when snap.Text = json -> view, [], ViewCurrencyClass.AlreadyCurrent
                                    | _ -> view, effects, ViewCurrencyClass.Refreshed
                                | None -> None, [], ViewCurrencyClass.Blocked
                            | _ -> None, [], ViewCurrencyClass.Missing
                        | _ -> inheritShip ()

                // Ship verdict currency (feature 092 / ADR-0026). Same shape and same gate as the
                // handoff above: a pure projection over ship.json, so refresh CAN faithfully
                // regenerate it — but only when its ship source is itself current. Re-projecting
                // against a stale ship would commit a verdict for inputs that no longer exist.
                // Unlike the handoff it needs no work model, so it depends on shClass alone.
                //
                // One asymmetry with the handoff, because the verdict is DURABLE: `ship.json` is
                // gitignored and the verdict is committed, so a fresh clone has the verdict WITHOUT its
                // source. Inheriting `Missing` there would report the one artifact that survived the
                // clone as absent. A present-but-unrefreshable verdict is `Blocked` (upstream), never
                // `Missing`.
                //
                // Feature 095 (FS.GG.SDD#188) removed a second asymmetry. `shClass` used to come from
                // `parsesAsJson`, weaker than `parseShipView`: a valid-JSON/invalid-view `ship.json`
                // reached the `AlreadyCurrent` arm below, failed to project, and was reported as a
                // MALFORMED VERDICT — a false fact about a well-formed committed artifact, alongside a
                // `ship: current` that was equally false. `shClass` now uses `parsesAsShipView`, so such
                // a source is `Malformed` at its own row and the verdict falls through to `Blocked`:
                // present, but its source cannot be trusted, so refresh cannot assess it.
                let verdictOnDisk = snapshot (shipVerdictPath workId) model

                // Each reported class must be true *of the verdict*, not merely inherited from ship:
                // the verdict is the one readiness view a reader sees in git, so a wrong word here is
                // a wrong fact about a committed artifact.
                let verdictView, verdictEffects, verdictClass =
                    match shClass, verdictOnDisk with
                    | ViewCurrencyClass.AlreadyCurrent, _ ->
                        match textOf (shipPath workId) with
                        | Some shipText ->
                            let view, effects, jsonOpt =
                                shipVerdictEmission workId request.GeneratorVersion shipText

                            match jsonOpt with
                            | Some json ->
                                match verdictOnDisk with
                                | Some snap when snap.Text = json -> view, [], ViewCurrencyClass.AlreadyCurrent
                                | _ -> view, effects, ViewCurrencyClass.Refreshed
                            // UNREACHABLE since feature 095, retained for match totality. Reaching this
                            // arm requires `shClass = AlreadyCurrent`, which now implies `parseShipView`
                            // returned `Ok` (see `parsesAsShipView` above). `shipVerdictEmission` derives
                            // `jsonOpt` from that same oracle over the same text (HandlersShip.fs:205),
                            // so it cannot be `None` here. This was the line that stamped `Malformed` on
                            // the well-formed committed verdict; the fix is that no input reaches it.
                            | None -> None, [], ViewCurrencyClass.Malformed
                        // UNREACHABLE, retained for match totality (F# cannot prove the implication).
                        // `shClass = AlreadyCurrent` is produced only by `downstreamClass`'s `Some snap`
                        // branch, so `snapshot (shipPath workId) model` returned `Some`. `textOf` maps
                        // over that same `snapshot`/`model`, so it cannot be `None`. Since feature 095
                        // the arm is doubly unreachable: `AlreadyCurrent` additionally implies the
                        // snapshot parsed as a ship view.
                        | None -> None, [], ViewCurrencyClass.Missing
                    // Present, and its source moved under it: the committed verdict no longer matches
                    // the authored inputs. That is `Stale` — the same word `ship` gets — and the
                    // remediation is the same: re-run `ship`. Reporting `Blocked` here would say
                    // "refresh could not proceed" about the ordinary edit-then-refresh path.
                    | ViewCurrencyClass.Stale, Some _ -> None, [], ViewCurrencyClass.Stale
                    // Present, but the source cannot be read or trusted: refresh cannot tell whether
                    // the committed verdict is current.
                    | _, Some _ -> None, [], ViewCurrencyClass.Blocked
                    // Absent. Whatever ails the source, the fact about the verdict is that it is
                    // missing — this is also the fresh-clone-without-a-verdict state.
                    | _, None -> None, [], ViewCurrencyClass.Missing

                let structuredClasses =
                    [ "work-model", wmClass
                      "analysis", anClass
                      "verify", veClass
                      "ship", shClass
                      // The verdict joins the structured set: it is committed, so a refresh that
                      // cannot bring it to currency must not report "refreshed-current".
                      "ship-verdict", verdictClass ]

                let isClean = viewCurrencyIsClean

                let structuredAllClean =
                    structuredClasses |> List.forall (fun (_, state) -> isClean state)

                let structuredNoneClean =
                    structuredClasses |> List.forall (fun (_, state) -> not (isClean state))

                // --- refresh-specific diagnostics ---
                let missingAuthored =
                    authoredSourcePaths
                    |> List.filter (fun path -> Option.isNone (snapshot path model))

                let workModelDiags =
                    if wmClass = ViewCurrencyClass.Blocked then
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
                        | ViewCurrencyClass.Blocked -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | ViewCurrencyClass.Stale -> [ refreshStaleView viewPath [ workModelPath workId ] ]
                        | ViewCurrencyClass.Malformed ->
                            [ refreshMalformedGeneratedView
                                  viewPath
                                  $"Generated view '{viewPath}' is malformed; re-run the responsible lifecycle command." ]
                        | ViewCurrencyClass.Missing -> [ refreshBlockedUpstreamView viewPath (workModelPath workId) ]
                        | _ -> [])

                // The verdict's upstream is `ship.json`, not the work model. Without this row a committed
                // artifact that refresh cannot bring to currency would carry no diagnostic and the run
                // would report success (feature 092).
                let verdictDiags =
                    let verdictPath = shipVerdictPath workId

                    match verdictClass with
                    // UNREACHABLE since feature 095 (see the `verdictClass` match): a source that fails
                    // ship-view parsing now lands the verdict on `Blocked`, and `malformed` is reported
                    // against `ship.json` itself by `downstreamDiags` above. Retained for totality.
                    | ViewCurrencyClass.Malformed ->
                        [ refreshMalformedGeneratedView
                              verdictPath
                              $"Source '{shipPath workId}' did not parse as a ship view, so '{verdictPath}' could not be re-projected; re-run `fsgg-sdd ship`." ]
                    | ViewCurrencyClass.Blocked -> [ refreshBlockedUpstreamView verdictPath (shipPath workId) ]
                    | ViewCurrencyClass.Stale -> [ refreshStaleView verdictPath [ shipPath workId ] ]
                    // Feature 095 (FS.GG.SDD#188): an absent verdict is `Missing` whatever ails its
                    // source — the currency word describes the ARTIFACT, and the artifact is absent. But
                    // "absent" alone does not choose a SEVERITY, so this row consults the source's class.
                    //
                    // A `Stale` source means the ordinary edit-then-refresh (or fresh-clone-then-edit)
                    // path, whose remediation is the plain `re-run ship` — identical to the case where
                    // the verdict is present, which emits `refreshStaleView` (a warning) in the arm above.
                    // Emitting `blockedUpstreamView` (an error) here made two states that differ only in
                    // whether a file exists report different severities for the same underlying fact.
                    //
                    // The diagnostic hangs on the verdict's own row and names `ship.json` as the source to
                    // re-run; `downstreamDiags` above already carries `ship.json`'s own `staleView` row.
                    //
                    // Any other source class (missing / malformed / blocked) means refresh genuinely
                    // cannot assess the verdict against an upstream it cannot read: still an error.
                    | ViewCurrencyClass.Missing when shClass = ViewCurrencyClass.Stale ->
                        [ refreshStaleView verdictPath [ shipPath workId ] ]
                    | ViewCurrencyClass.Missing -> [ refreshBlockedUpstreamView verdictPath (shipPath workId) ]
                    | _ -> []

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

                let refreshDiags = workModelDiags @ downstreamDiags @ verdictDiags @ summaryDiags

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
                    if structuredAllClean && agentClass <> ViewCurrencyClass.Blocked then
                        "succeeded"
                    else
                        "succeededWithWarnings"

                let disposition =
                    if wmClass = ViewCurrencyClass.Blocked || structuredNoneClean then
                        RefreshBlocked
                    elif
                        structuredAllClean
                        && (agentClass = ViewCurrencyClass.Refreshed
                            || agentClass = ViewCurrencyClass.AlreadyCurrent
                            || agentClass = ViewCurrencyClass.NotApplicable)
                    then
                        RefreshedCurrent
                    else
                        PartiallyBlocked

                let dispositionValue = refreshDispositionValue disposition

                // currency word per view for the report and summary table
                let perViewState =
                    [ "work-model", viewCurrencyDisplay wmClass
                      "analysis", viewCurrencyDisplay anClass
                      "verify", viewCurrencyDisplay veClass
                      "ship", viewCurrencyDisplay shClass
                      "ship-verdict", viewCurrencyDisplay verdictClass
                      "governance-handoff", viewCurrencyDisplay govClass
                      "agent-commands", viewCurrencyDisplay agentClass
                      "summary", (if summaryRenderable then "current" else "blocked") ]

                let summaryClass, summaryEffects, summaryViewState =
                    if not summaryRenderable then
                        ViewCurrencyClass.Blocked, [], None
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
                            | Some existing when existing.Text = text -> ViewCurrencyClass.AlreadyCurrent
                            | _ -> ViewCurrencyClass.Refreshed

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
                    | ViewCurrencyClass.Refreshed -> viewId :: refreshed, current, blocked, na
                    | ViewCurrencyClass.AlreadyCurrent -> refreshed, viewId :: current, blocked, na
                    | ViewCurrencyClass.NotApplicable -> refreshed, current, blocked, viewId :: na
                    | _ -> refreshed, current, viewId :: blocked, na

                let refreshedViewIds, alreadyCurrentViewIds, blockedViewIds, notApplicableViewIds =
                    [ "work-model", wmClass
                      "analysis", anClass
                      "verify", veClass
                      "ship", shClass
                      "ship-verdict", verdictClass
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
                let downstreamView path kind state =
                    { Path = path
                      Kind = kind
                      SchemaVersion = Some 1
                      Generator = Some request.GeneratorVersion
                      Sources = []
                      OutputDigest = None
                      Currency = viewCurrencyToGenerated state
                      DiagnosticIds = [] }

                let workModelViewState =
                    { wmView with
                        Currency = viewCurrencyToGenerated wmClass }

                let agentViewStates =
                    agViews
                    |> List.map (fun view ->
                        let state =
                            match agentGuidanceWriteText view.Path with
                            | Some text ->
                                if (snapshot view.Path model |> Option.map (fun snap -> snap.Text)) = Some text then
                                    ViewCurrencyClass.AlreadyCurrent
                                else
                                    ViewCurrencyClass.Refreshed
                            | None -> ViewCurrencyClass.Blocked

                        { view with
                            Currency = viewCurrencyToGenerated state })

                let governanceHandoffViewState =
                    match govView with
                    | Some view ->
                        { view with
                            Currency = viewCurrencyToGenerated govClass }
                    | None -> downstreamView (governanceHandoffPath workId) "governance-handoff" govClass

                let shipVerdictViewState =
                    match verdictView with
                    | Some view ->
                        { view with
                            Currency = viewCurrencyToGenerated verdictClass }
                    | None -> downstreamView (shipVerdictPath workId) "ship-verdict" verdictClass

                let generatedViews =
                    [ workModelViewState
                      downstreamView (analysisPath workId) "analysis" anClass
                      downstreamView (verifyPath workId) "verification" veClass
                      downstreamView (shipPath workId) "ship" shClass
                      shipVerdictViewState
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

                let effects =
                    dedupEffects (wmEffects @ agEffects @ verdictEffects @ govEffects @ summaryEffects)

                // wmDiags are the reused generator's own staleness heuristics about the
                // prior on-disk work model; refresh reports its own per-view diagnostics
                // (allDiags), so the generator's internal diagnostics are not surfaced.
                ignore wmDiags

                allDiags, Some summaryRecord, generatedViews, effects
