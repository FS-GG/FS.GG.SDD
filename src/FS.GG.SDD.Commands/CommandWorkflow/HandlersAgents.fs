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
open FS.GG.SDD.Commands.Internal.ViewGeneration
open FS.GG.SDD.Commands.Internal.Prerequisites

module internal HandlersAgents =
    // Pure in-memory ops only (`Path` string ops; `MemoryStream` for JSON framing) — the effectful
    // `File`/`Directory` surface stays at the `CommandEffects` edge, deliberately out of scope here.
    type private Path = System.IO.Path
    type private MemoryStream = System.IO.MemoryStream

    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module WorkModelModule = FS.GG.SDD.Artifacts.WorkModel

    // ---- Agents command (cross-cutting generated agent guidance) ----

    let resolveGeneratedRoot workId (raw: string) =
        normalizeRelativePath ((if String.IsNullOrEmpty raw then "" else raw).Replace("{workId}", workId))

    let agentRootResolvesWithinProject workId (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            false
        else
            // Delegate to the single authoritative containment predicate so the agents command
            // rejects the SAME paths as every other seam. The old `StartsWith "..")` check ran
            // against `resolveGeneratedRoot`, which does not collapse `..` segments, so a mid-path
            // escape (`foo/../../../etc`) was accepted as "within project" and then resolved out of
            // tree by the downstream `Path.Combine` + `GetFullPath` (#340). `escapesRoot` rejects any
            // `..` segment (and any rooted path) on the RAW, substituted string.
            not (PathContainment.escapesRoot (raw.Replace("{workId}", workId)))

    let agentsConfigOpt model =
        match snapshot ".fsgg/agents.yml" model with
        | Some snap ->
            match parseAgentGuidanceConfig snap with
            | Ok config -> Some config
            | Error _ -> None
        | None -> None

    let agentGuidanceCandidateReadEffects workId model =
        match agentsConfigOpt model with
        | None -> []
        | Some config ->
            let already = plannedReadPaths model |> Set.ofList

            config.Targets
            |> List.map (fun target -> (resolveGeneratedRoot workId target.GeneratedRoot) + "/guidance.json")
            |> List.filter (fun path -> not (Set.contains (normalizeRelativePath path) already))
            |> List.distinct
            |> List.sort
            |> List.map ReadFile

    let agentGuidanceManifestJson
        (workId: string)
        (targetId: string)
        (generator: GeneratorVersion)
        (workModelP: string)
        (sourceDigest: SourceDigest)
        (behaviorDigest: SourceDigest)
        (commands: GuidanceCommandEntry list)
        (skills: GuidanceSkillEntry list)
        (renderedFiles: (string * string) list)
        (diagnostics: Diagnostic list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("targetId", targetId)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")
        writer.WriteBoolean("generated", true)
        writer.WriteStartArray("sources")
        writer.WriteStartObject()
        writer.WriteString("path", workModelP)
        writer.WriteString("kind", "workModel")
        writeDigestObject writer "digest" (Some sourceDigest)
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("schemaStatus", "current")
        writer.WriteEndObject()
        writer.WriteEndArray()
        writeDigestObject writer "behaviorModelDigest" (Some sourceDigest |> Option.map (fun _ -> behaviorDigest))
        writer.WriteStartArray("commands")

        commands
        |> List.sortBy (fun command -> command.Id)
        |> List.iter (fun command ->
            writer.WriteStartObject()
            writer.WriteString("id", command.Id)
            writer.WriteString("title", command.Title)
            writer.WriteString("stage", command.Stage)
            writer.WriteString("purpose", command.Purpose)
            writeStringArray writer "relatedIds" command.RelatedIds
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("skills")

        skills
        |> List.sortBy (fun skill -> skill.Id)
        |> List.iter (fun skill ->
            writer.WriteStartObject()
            writer.WriteString("id", skill.Id)
            writer.WriteString("title", skill.Title)
            writer.WriteString("capability", skill.Capability)
            writeStringArray writer "relatedIds" skill.RelatedIds
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("renderedFiles")

        renderedFiles
        |> List.sortBy fst
        |> List.iter (fun (path, kind) ->
            writer.WriteStartObject()
            writer.WriteString("path", path)
            writer.WriteString("kind", kind)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")

        // The durable guidance view records the diagnostics attributable to it — the same
        // set the command report projects into this target's generated-view `diagnosticIds`.
        // Hardcoding an empty array silently dropped real diagnostics into a durable artifact
        // (ADR-0002 Gap B, finding 4). Canonicalize the order so the view stays byte-stable.
        diagnostics
        |> DiagnosticsModule.sort
        |> List.iter (writeAnalysisDiagnosticJson writer)

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let agentCommandsMarkdown (workId: string) (targetId: string) (commands: GuidanceCommandEntry list) =
        let builder = StringBuilder()
        builder.AppendLine($"# Agent commands for {targetId} (generated)") |> ignore
        builder.AppendLine("") |> ignore

        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the")
        |> ignore

        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.")
        |> ignore

        builder.AppendLine("") |> ignore

        commands
        |> List.sortBy (fun command -> command.Id)
        |> List.iter (fun command ->
            let related = String.concat ", " command.RelatedIds
            builder.AppendLine($"## {command.Id} — {command.Title}") |> ignore
            builder.AppendLine($"- Stage: {command.Stage}") |> ignore
            builder.AppendLine($"- Purpose: {command.Purpose}") |> ignore
            builder.AppendLine($"- Related: {related}") |> ignore
            builder.AppendLine("") |> ignore)

        builder.ToString()

    let agentSkillsMarkdown (workId: string) (targetId: string) (skills: GuidanceSkillEntry list) =
        let builder = StringBuilder()
        builder.AppendLine($"# Agent skills for {targetId} (generated)") |> ignore
        builder.AppendLine("") |> ignore

        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the")
        |> ignore

        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.")
        |> ignore

        builder.AppendLine("") |> ignore

        skills
        |> List.sortBy (fun skill -> skill.Id)
        |> List.iter (fun skill ->
            let related = String.concat ", " skill.RelatedIds
            builder.AppendLine($"## {skill.Id} — {skill.Title}") |> ignore
            builder.AppendLine($"- Capability: {skill.Capability}") |> ignore
            builder.AppendLine($"- Related: {related}") |> ignore
            builder.AppendLine("") |> ignore)

        builder.ToString()

    let agentGuidanceFindingSeverity (diagnostic: Diagnostic) =
        match diagnostic.Severity with
        | DiagnosticSeverity.DiagnosticError -> "blocking"
        | DiagnosticSeverity.DiagnosticWarning -> "warning"
        | DiagnosticSeverity.DiagnosticInfo -> "advisory"

    let agentGuidanceFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic ->
            sprintf "GF%03d" (index + 1), diagnostic, agentGuidanceFindingSeverity diagnostic)

    let computeAgentsPlan model =
        let summaries, diagnostics, generatedViews, effects =
            runHandler model None (fun workId ->
                let request = model.Request
                let projectDiags = projectDiagnostics model
                let duplicateDiags = duplicateWorkIdDiagnostics workId model
                let configOpt = agentsConfigOpt model

                let configDiags =
                    match configOpt with
                    | None -> []
                    | Some config ->
                        let noTargets =
                            if List.isEmpty config.Targets then
                                [ agentsNoTargets ".fsgg/agents.yml" ]
                            else
                                []

                        let invalidTargets =
                            config.Targets
                            |> List.filter (fun target ->
                                not (agentRootResolvesWithinProject workId target.GeneratedRoot))
                            |> List.map (fun target -> agentsInvalidGeneratedRoot ".fsgg/agents.yml" target.Id)

                        let invalidWorkModel =
                            if agentRootResolvesWithinProject workId config.WorkModelPath then
                                []
                            else
                                [ agentsInvalidGeneratedRoot ".fsgg/agents.yml" "workModel" ]

                        noTargets @ invalidTargets @ invalidWorkModel

                let workModelP = workModelPath workId
                let workModelSnap = snapshot workModelP model

                // Early-stage (FR-004/FR-010b): an *absent* work model is the expected
                // pre-work-model state, not a defect. Reclassify it from the blocking
                // agents.missingWorkModel to a non-blocking advisory that points to the
                // seeded static guidance. Malformed/stale/blocked work models below are
                // untouched and still block (Observability VIII, FR-008).
                let isEarlyStage = Option.isNone workModelSnap

                let workModelDiags, workModelOpt =
                    match workModelSnap with
                    | None -> [ agentsEarlyStageGuidance (earlyStagePresentStages workId model) ], None
                    | Some snap ->
                        match WorkModelModule.parseWorkModel snap with
                        | Error errs ->
                            (errs
                             |> List.map (fun diagnostic -> agentsMalformedWorkModel workModelP diagnostic.Message)),
                            None
                        | Ok wm when not (String.Equals(wm.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                            [ agentsWorkModelIdentityMismatch workModelP workId wm.WorkId ], None
                        | Ok wm ->
                            let embedded = wm.Diagnostics

                            let unknownRefs =
                                embedded
                                |> List.filter (fun diagnostic ->
                                    diagnostic.Id.StartsWith("unknownReference", StringComparison.OrdinalIgnoreCase))
                                |> List.collect (fun diagnostic ->
                                    match diagnostic.RelatedIds with
                                    | [] -> [ agentsUnknownSourceReference workModelP diagnostic.Id ]
                                    | ids -> ids |> List.map (agentsUnknownSourceReference workModelP))

                            let staleMarkers =
                                if embedded |> List.exists signalsStaleView then
                                    [ agentsStaleWorkModel workModelP ]
                                else
                                    []

                            let otherBlocking =
                                embedded
                                |> List.filter (fun diagnostic ->
                                    diagnostic.Severity = DiagnosticSeverity.DiagnosticError
                                    && not (
                                        diagnostic.Id.StartsWith(
                                            "unknownReference",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                    && not (signalsStaleView diagnostic))

                            let blockedDiag =
                                if List.isEmpty otherBlocking then
                                    []
                                else
                                    [ agentsBlockedWorkModel
                                          workModelP
                                          (otherBlocking
                                           |> List.map (fun diagnostic -> diagnostic.Id)
                                           |> List.distinct
                                           |> List.sort) ]

                            let gateDiags = unknownRefs @ staleMarkers @ blockedDiag

                            if List.isEmpty gateDiags then
                                [], Some wm
                            else
                                gateDiags, None

                let workModelText =
                    workModelSnap |> Option.map (fun snap -> snap.Text) |> Option.defaultValue ""

                let sourceDigest = SchemaVersionModule.sha256Text workModelText

                let equivalenceRequired =
                    configOpt
                    |> Option.map (fun config -> config.RequireEquivalentClaudeAndCodexBehavior)
                    |> Option.defaultValue true

                let targetResults =
                    match configOpt, workModelOpt with
                    | Some config, Some wm ->
                        let guidanceModel = WorkModelModule.deriveGuidanceModel wm
                        let behaviorDigest = WorkModelModule.behaviorModelDigest guidanceModel

                        config.Targets
                        |> List.sortBy (fun target -> target.Id)
                        |> List.map (fun target ->
                            let root = resolveGeneratedRoot workId target.GeneratedRoot
                            let guidancePath = root + "/guidance.json"
                            let commandsPath = root + "/commands.md"
                            let skillsPath = root + "/skills.md"
                            let renderedFiles = [ commandsPath, "commands"; skillsPath, "skills" ]

                            // The manifest is finalized at write time, not here: a target's own
                            // diagnostics (currency/divergence) are only fully known once every
                            // target is compared, so defer the render behind a builder that takes
                            // the resolved per-target diagnostics.
                            let buildManifest diagnostics =
                                agentGuidanceManifestJson
                                    workId
                                    target.Id
                                    request.GeneratorVersion
                                    workModelP
                                    sourceDigest
                                    behaviorDigest
                                    guidanceModel.Commands
                                    guidanceModel.Skills
                                    renderedFiles
                                    diagnostics

                            let commandsMd = agentCommandsMarkdown workId target.Id guidanceModel.Commands
                            let skillsMd = agentSkillsMarkdown workId target.Id guidanceModel.Skills

                            // Currency is a per-target fact: does this target's own recorded digest still
                            // match the digest recomputed from the current work model? Divergence is a
                            // CROSS-target fact and cannot be decided here — it is resolved once all
                            // targets are known (see `divergentTargetIds` below). Conflating the two made
                            // `agents` block on ordinary staleness and refuse the very regeneration its
                            // remediation demanded (FS.GG.SDD#197).
                            let currency, targetDiags, recordedBehaviorDigest, behaviorMatches =
                                match snapshot guidancePath model with
                                | None -> GeneratedViewCurrency.Missing, [], None, None
                                | Some existing ->
                                    match parseGeneratedAgentGuidance existing with
                                    | Error errs ->
                                        let message =
                                            errs
                                            |> List.tryHead
                                            |> Option.map (fun diagnostic -> diagnostic.Message)
                                            |> Option.defaultValue "Generated agent guidance is malformed."

                                        GeneratedViewCurrency.Malformed,
                                        [ agentsMalformedGeneratedGuidance guidancePath message ],
                                        None,
                                        None
                                    | Ok manifest ->
                                        let recordedDigest =
                                            manifest.Sources
                                            |> List.tryPick (fun source -> source.Digest)
                                            |> Option.map (fun digest -> digest.Value)

                                        let digestMatches = recordedDigest = Some sourceDigest.Value

                                        let behaviorMatches =
                                            String.Equals(
                                                manifest.BehaviorModelDigest.Value,
                                                behaviorDigest.Value,
                                                StringComparison.OrdinalIgnoreCase
                                            )

                                        let recorded = Some manifest.BehaviorModelDigest.Value

                                        if digestMatches && behaviorMatches then
                                            GeneratedViewCurrency.Current, [], recorded, Some true
                                        else
                                            GeneratedViewCurrency.Stale, [], recorded, Some behaviorMatches

                            {| TargetId = target.Id
                               Root = root
                               GuidancePath = guidancePath
                               BuildManifest = buildManifest
                               CommandsPath = commandsPath
                               CommandsMd = commandsMd
                               SkillsPath = skillsPath
                               SkillsMd = skillsMd
                               Currency = currency
                               Diagnostics = targetDiags
                               RecordedBehaviorDigest = recordedBehaviorDigest
                               BehaviorMatches = behaviorMatches |})
                    | _ -> []

                // Both targets are rendered from one `NormalizedGuidanceModel` and stamped with one
                // recomputed `behaviorDigest`, so they are equivalent by construction. Targets can only
                // disagree with EACH OTHER if a generated `guidance.json` was edited or corrupted out of
                // band. That — not staleness — is what `agents.behaviorDivergence` names.
                let recordedBehaviorDigests =
                    targetResults
                    |> List.choose (fun result -> result.RecordedBehaviorDigest)
                    |> List.map (fun digest -> digest.ToLowerInvariant())
                    |> List.distinct

                let staleTargets =
                    targetResults
                    |> List.filter (fun result -> result.Currency = GeneratedViewCurrency.Stale)

                let divergent = equivalenceRequired && List.length recordedBehaviorDigests > 1

                // A target is divergent only if its own recorded behavior digest disagrees with the shared
                // model. Staleness is not enough: a target whose *source* digest moved while its behavior
                // digest still matches is merely out of date, and naming it here would repeat the very
                // category error this guard exists to correct.
                let divergentTargets =
                    if divergent then
                        targetResults
                        |> List.filter (fun result -> result.BehaviorMatches = Some false)
                        |> List.sortBy (fun result -> result.TargetId)
                    else
                        []

                let divergentTargetIds =
                    divergentTargets |> List.map (fun result -> result.TargetId) |> List.distinct

                // Emitted once, naming every divergent target. Both this and staleness are warnings, so
                // the run still regenerates: divergence is an observation about the views it is about to
                // replace, never a refusal to replace them.
                let divergenceDiagnostic =
                    divergentTargets
                    |> List.tryHead
                    |> Option.map (fun result -> agentsBehaviorDivergence result.GuidancePath divergentTargetIds)

                // One condition, one diagnostic per target: a divergent target reports divergence and not
                // also staleness. Keyed by target so it can also become each generated view's
                // `diagnosticIds` — a non-current view must name its own cause, because `refresh` discards
                // the agents diagnostics and keeps only those ids.
                // One per-target diagnostic set feeds both the generated-view `diagnosticIds`
                // (below) and the durable guidance.json (finding 4) — derived once so the machine
                // contract and the command report can never disagree about a target's cause.
                let viewDiagnosticsByTarget =
                    targetResults
                    |> List.map (fun result ->
                        let currency =
                            if List.contains result.TargetId divergentTargetIds then
                                divergenceDiagnostic |> Option.toList
                            elif result.Currency = GeneratedViewCurrency.Stale then
                                [ agentsStaleGeneratedGuidance result.GuidancePath result.TargetId ]
                            else
                                []

                        result.TargetId,
                        (result.Diagnostics @ currency)
                        |> List.distinctBy (fun diagnostic -> diagnostic.Id))
                    |> Map.ofList

                let viewDiagnosticIdsByTarget =
                    viewDiagnosticsByTarget
                    |> Map.map (fun _ diagnostics -> diagnostics |> List.map (fun diagnostic -> diagnostic.Id))

                let currencyDiagnostics =
                    (divergenceDiagnostic |> Option.toList)
                    @ (staleTargets
                       |> List.filter (fun result -> not (List.contains result.TargetId divergentTargetIds))
                       |> List.map (fun result -> agentsStaleGeneratedGuidance result.GuidancePath result.TargetId))

                let targetDiagnostics =
                    (targetResults |> List.collect (fun result -> result.Diagnostics))
                    @ currencyDiagnostics

                let baseDiagnostics = projectDiags @ duplicateDiags @ configDiags @ workModelDiags

                baseDiagnostics @ targetDiagnostics,
                (fun hasBlocking diagnostics ->
                    let hasWarning =
                        diagnostics
                        |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticWarning)

                    let generatedTargetIds =
                        targetResults
                        |> List.map (fun result -> result.TargetId)
                        |> List.distinct
                        |> List.sort

                    let generatedRoots =
                        targetResults
                        |> List.map (fun result -> result.Root)
                        |> List.distinct
                        |> List.sort

                    let refusedTargetIds =
                        targetResults
                        |> List.filter (fun result -> result.Currency = GeneratedViewCurrency.Malformed)
                        |> List.map (fun result -> result.TargetId)
                        |> List.distinct
                        |> List.sort

                    let effects =
                        if hasBlocking then
                            []
                        else
                            targetResults
                            |> List.collect (fun result ->
                                match result.Currency with
                                | GeneratedViewCurrency.Current -> []
                                | _ ->
                                    let targetDiagnostics =
                                        viewDiagnosticsByTarget
                                        |> Map.tryFind result.TargetId
                                        |> Option.defaultValue []

                                    [ CreateDirectory result.Root
                                      WriteFile(
                                          result.GuidancePath,
                                          result.BuildManifest targetDiagnostics,
                                          GeneratedView
                                      )
                                      WriteFile(result.CommandsPath, result.CommandsMd, GeneratedView)
                                      WriteFile(result.SkillsPath, result.SkillsMd, GeneratedView) ])

                    let generatedViews =
                        targetResults
                        |> List.map (fun result ->
                            generatedViewState
                                result.GuidancePath
                                "agent-commands"
                                request.GeneratorVersion
                                [ { Path = workModelP
                                    Digest = Some sourceDigest
                                    SchemaVersion = Some 1
                                    SchemaStatus = Some "current" } ]
                                (if
                                     hasBlocking
                                     && (result.Currency = GeneratedViewCurrency.Missing
                                         || result.Currency = GeneratedViewCurrency.Stale)
                                 then
                                     GeneratedViewCurrency.Blocked
                                 else
                                     result.Currency)
                                (viewDiagnosticIdsByTarget
                                 |> Map.tryFind result.TargetId
                                 |> Option.defaultValue []))

                    let disposition =
                        if hasBlocking then
                            "blocked"
                        elif isEarlyStage then
                            "early-stage"
                        elif
                            targetResults
                            |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale)
                            && request.DryRun
                        then
                            "stale"
                        elif hasWarning then
                            "advisory"
                        else
                            "generated-current"

                    let readiness =
                        if hasBlocking then "needsAgentGuidanceCorrection"
                        elif isEarlyStage then "agentGuidanceEarlyStage"
                        else "agentGuidanceReady"

                    let findings = agentGuidanceFindings diagnostics

                    let findingCount severity =
                        findings
                        |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity)
                        |> List.length

                    let generatedViewStateLabel =
                        if hasBlocking then
                            "blocked"
                        elif isEarlyStage then
                            "early-stage"
                        elif
                            targetResults
                            |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale)
                        then
                            "stale"
                        elif
                            targetResults
                            |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Missing)
                        then
                            "missing"
                        elif List.isEmpty targetResults then
                            "missing"
                        else
                            "current"

                    let summary: AgentGuidanceSummary =
                        { WorkId = workId
                          Stage = "agents"
                          Status = disposition
                          GeneratedRoots = generatedRoots
                          GeneratedTargetIds = generatedTargetIds
                          RefusedTargetIds = refusedTargetIds
                          FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                          ReadyFindingCount =
                            if disposition = "generated-current" then
                                List.length generatedTargetIds
                            else
                                0
                          AdvisoryCount = findingCount "advisory"
                          WarningCount = findingCount "warning"
                          BlockingCount = findingCount "blocking"
                          Disposition = disposition
                          EquivalenceRequired = equivalenceRequired
                          DivergentTargetIds = divergentTargetIds
                          GeneratedViewState = generatedViewStateLabel
                          SourceSnapshotCount = (if Option.isSome workModelSnap then 1 else 0)
                          Readiness = readiness }

                    Some summary, generatedViews, effects, []))

        diagnostics, summaries, generatedViews, effects
