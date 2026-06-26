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
module internal HandlersAgents =
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
            let substituted = raw.Replace("{workId}", workId)

            not (Path.IsPathRooted substituted)
            && not ((resolveGeneratedRoot workId raw).StartsWith("..", StringComparison.Ordinal))

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
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let agentCommandsMarkdown (workId: string) (targetId: string) (commands: GuidanceCommandEntry list) =
        let builder = StringBuilder()
        builder.AppendLine($"# Agent commands for {targetId} (generated)") |> ignore
        builder.AppendLine("") |> ignore
        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the") |> ignore
        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.") |> ignore
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
        builder.AppendLine($"Generated from `{workModelPath workId}`. This is a generated projection of the") |> ignore
        builder.AppendLine("normalized work model, not an authored source of truth. See `guidance.json`.") |> ignore
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
        |> List.mapi (fun index diagnostic -> sprintf "GF%03d" (index + 1), diagnostic, agentGuidanceFindingSeverity diagnostic)

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
                            if List.isEmpty config.Targets then [ agentsNoTargets ".fsgg/agents.yml" ] else []

                        let invalidTargets =
                            config.Targets
                            |> List.filter (fun target -> not (agentRootResolvesWithinProject workId target.GeneratedRoot))
                            |> List.map (fun target -> agentsInvalidGeneratedRoot ".fsgg/agents.yml" target.Id)

                        let invalidWorkModel =
                            if agentRootResolvesWithinProject workId config.WorkModelPath then
                                []
                            else
                                [ agentsInvalidGeneratedRoot ".fsgg/agents.yml" "workModel" ]

                        noTargets @ invalidTargets @ invalidWorkModel

                let workModelP = workModelPath workId
                let workModelSnap = snapshot workModelP model

                let workModelDiags, workModelOpt =
                    match workModelSnap with
                    | None -> [ agentsMissingWorkModel workModelP ], None
                    | Some snap ->
                        match WorkModelModule.parseWorkModel snap with
                        | Error errs -> (errs |> List.map (fun diagnostic -> agentsMalformedWorkModel workModelP diagnostic.Message)), None
                        | Ok wm when not (String.Equals(wm.WorkId, workId, StringComparison.OrdinalIgnoreCase)) ->
                            [ agentsWorkModelIdentityMismatch workModelP workId wm.WorkId ], None
                        | Ok wm ->
                            let embedded = wm.Diagnostics

                            let unknownRefs =
                                embedded
                                |> List.filter (fun diagnostic -> diagnostic.Id.StartsWith("unknownReference", StringComparison.OrdinalIgnoreCase))
                                |> List.collect (fun diagnostic ->
                                    match diagnostic.RelatedIds with
                                    | [] -> [ agentsUnknownSourceReference workModelP diagnostic.Id ]
                                    | ids -> ids |> List.map (agentsUnknownSourceReference workModelP))

                            let staleMarkers =
                                if embedded |> List.exists (fun diagnostic -> diagnostic.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) >= 0) then
                                    [ agentsStaleWorkModel workModelP ]
                                else
                                    []

                            let otherBlocking =
                                embedded
                                |> List.filter (fun diagnostic ->
                                    diagnostic.Severity = DiagnosticSeverity.DiagnosticError
                                    && not (diagnostic.Id.StartsWith("unknownReference", StringComparison.OrdinalIgnoreCase))
                                    && diagnostic.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) < 0)

                            let blockedDiag =
                                if List.isEmpty otherBlocking then
                                    []
                                else
                                    [ agentsBlockedWorkModel workModelP (otherBlocking |> List.map (fun diagnostic -> diagnostic.Id) |> List.distinct |> List.sort) ]

                            let gateDiags = unknownRefs @ staleMarkers @ blockedDiag

                            if List.isEmpty gateDiags then [], Some wm else gateDiags, None

                let workModelText = workModelSnap |> Option.map (fun snap -> snap.Text) |> Option.defaultValue ""
                let sourceDigest = SchemaVersionModule.sha256Text workModelText
                let equivalenceRequired = configOpt |> Option.map (fun config -> config.RequireEquivalentClaudeAndCodexBehavior) |> Option.defaultValue true

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
                            let manifestJson = agentGuidanceManifestJson workId target.Id request.GeneratorVersion workModelP sourceDigest behaviorDigest guidanceModel.Commands guidanceModel.Skills renderedFiles
                            let commandsMd = agentCommandsMarkdown workId target.Id guidanceModel.Commands
                            let skillsMd = agentSkillsMarkdown workId target.Id guidanceModel.Skills

                            let currency, targetDiags, divergent =
                                match snapshot guidancePath model with
                                | None -> GeneratedViewCurrency.Missing, [], false
                                | Some existing ->
                                    match parseGeneratedAgentGuidance existing with
                                    | Error errs ->
                                        let message = errs |> List.tryHead |> Option.map (fun diagnostic -> diagnostic.Message) |> Option.defaultValue "Generated agent guidance is malformed."
                                        GeneratedViewCurrency.Malformed, [ agentsMalformedGeneratedGuidance guidancePath message ], false
                                    | Ok manifest ->
                                        let recordedDigest = manifest.Sources |> List.tryPick (fun source -> source.Digest) |> Option.map (fun digest -> digest.Value)
                                        let digestMatches = recordedDigest = Some sourceDigest.Value
                                        let behaviorMatches = String.Equals(manifest.BehaviorModelDigest.Value, behaviorDigest.Value, StringComparison.OrdinalIgnoreCase)
                                        let divergent = equivalenceRequired && not behaviorMatches

                                        if digestMatches && behaviorMatches then
                                            GeneratedViewCurrency.Current, [], false
                                        else
                                            let staleDiag = [ agentsStaleGeneratedGuidance guidancePath target.Id ]
                                            let divergenceDiag = if divergent then [ agentsBehaviorDivergence guidancePath [ target.Id ] ] else []
                                            GeneratedViewCurrency.Stale, (staleDiag @ divergenceDiag), divergent

                            {| TargetId = target.Id
                               Root = root
                               GuidancePath = guidancePath
                               ManifestJson = manifestJson
                               CommandsPath = commandsPath
                               CommandsMd = commandsMd
                               SkillsPath = skillsPath
                               SkillsMd = skillsMd
                               Currency = currency
                               Diagnostics = targetDiags
                               Divergent = divergent |})
                    | _ -> []

                let targetDiagnostics = targetResults |> List.collect (fun result -> result.Diagnostics)
                let baseDiagnostics = projectDiags @ duplicateDiags @ configDiags @ workModelDiags

                baseDiagnostics @ targetDiagnostics,
                (fun hasBlocking diagnostics ->
                    let hasWarning = diagnostics |> List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticWarning)

                    let divergentTargetIds =
                        targetResults |> List.filter (fun result -> result.Divergent) |> List.map (fun result -> result.TargetId) |> List.distinct |> List.sort

                    let generatedTargetIds = targetResults |> List.map (fun result -> result.TargetId) |> List.distinct |> List.sort
                    let generatedRoots = targetResults |> List.map (fun result -> result.Root) |> List.distinct |> List.sort

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
                                    [ CreateDirectory result.Root
                                      WriteFile(result.GuidancePath, result.ManifestJson, GeneratedView)
                                      WriteFile(result.CommandsPath, result.CommandsMd, GeneratedView)
                                      WriteFile(result.SkillsPath, result.SkillsMd, GeneratedView) ])

                    let generatedViews =
                        targetResults
                        |> List.map (fun result ->
                            shipGeneratedViewState
                                result.GuidancePath
                                "agent-commands"
                                request.GeneratorVersion
                                [ { Path = workModelP; Digest = Some sourceDigest; SchemaVersion = Some 1; SchemaStatus = Some "current" } ]
                                None
                                (if hasBlocking && (result.Currency = GeneratedViewCurrency.Missing || result.Currency = GeneratedViewCurrency.Stale) then GeneratedViewCurrency.Blocked else result.Currency)
                                (result.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)))

                    let disposition =
                        if hasBlocking then "blocked"
                        elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale) && request.DryRun then "stale"
                        elif hasWarning then "advisory"
                        else "generated-current"

                    let readiness = if hasBlocking then "needsAgentGuidanceCorrection" else "agentGuidanceReady"
                    let findings = agentGuidanceFindings diagnostics
                    let findingCount severity = findings |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity) |> List.length

                    let generatedViewState =
                        if hasBlocking then "blocked"
                        elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Stale) then "stale"
                        elif targetResults |> List.exists (fun result -> result.Currency = GeneratedViewCurrency.Missing) then "missing"
                        elif List.isEmpty targetResults then "missing"
                        else "current"

                    let summary: AgentGuidanceSummary =
                        { WorkId = workId
                          Stage = "agents"
                          Status = disposition
                          GeneratedRoots = generatedRoots
                          GeneratedTargetIds = generatedTargetIds
                          RefusedTargetIds = refusedTargetIds
                          FindingIds = findings |> List.map (fun (id, _, _) -> id) |> List.sort
                          ReadyFindingCount = if disposition = "generated-current" then List.length generatedTargetIds else 0
                          AdvisoryCount = findingCount "advisory"
                          WarningCount = findingCount "warning"
                          BlockingCount = findingCount "blocking"
                          Disposition = disposition
                          EquivalenceRequired = equivalenceRequired
                          DivergentTargetIds = divergentTargetIds
                          GeneratedViewState = generatedViewState
                          SourceSnapshotCount = (if Option.isSome workModelSnap then 1 else 0)
                          Readiness = readiness }

                    Some summary, generatedViews, effects, []))

        diagnostics, summaries, generatedViews, effects

