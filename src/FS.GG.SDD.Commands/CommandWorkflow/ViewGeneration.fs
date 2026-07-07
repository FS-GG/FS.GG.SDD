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

module internal ViewGeneration =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module GenerationManifestModule = FS.GG.SDD.Artifacts.GenerationManifest
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion
    module SerializationModule = FS.GG.SDD.Artifacts.Serialization
    module WorkModelModule = FS.GG.SDD.Artifacts.WorkModel

    // The disposition-completeness check is shared with the `tasks` stage — see
    // `TaskGraphAuthoring.missingDispositionIds` (the single `required` universe). `tasks`
    // now fails fast on a gap, so `analyze` normally sees none; this stays as the
    // downstream backstop and to render `missing` disposition relationships (#162).
    let missingDispositionDiagnostics
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (taskFacts: TaskFacts)
        =
        match
            TaskGraphAuthoring.missingDispositionIds specFacts clarificationFacts checklistFacts planFacts taskFacts
        with
        | [] -> []
        | missing -> [ missingDisposition (tasksPath workId) missing ]

    type AnalysisRelationshipDraft =
        { SourcePath: string
          TargetPath: string
          SourceId: string option
          TargetId: string option
          Relationship: string
          State: string
          DiagnosticIds: string list }

    let relationship
        (sourcePath: string)
        (targetPath: string)
        (sourceId: string option)
        (targetId: string option)
        (relationship: string)
        (state: string)
        (diagnosticIds: string list)
        : AnalysisRelationshipDraft =
        { SourcePath = sourcePath
          TargetPath = targetPath
          SourceId = sourceId
          TargetId = targetId
          Relationship = relationship
          State = state
          DiagnosticIds = diagnosticIds }

    let analysisRelationships
        workId
        (specFacts: SpecificationFacts)
        (clarificationFacts: ClarificationFacts)
        (checklistFacts: ChecklistFacts)
        (planFacts: PlanFacts)
        (taskFacts: TaskFacts)
        =
        let taskDispositionIds = TaskGraphAuthoring.allTaskDispositionIds taskFacts

        let dispositionRelationships (sourcePath: string) (relationshipName: string) (ids: string list) =
            ids
            |> List.map (fun id ->
                let current = Set.contains (id.ToUpperInvariant()) taskDispositionIds

                relationship
                    sourcePath
                    (tasksPath workId)
                    (Some id)
                    None
                    relationshipName
                    (if current then "current" else "missing")
                    (if current then [] else [ "missingDisposition" ]))

        [ [ relationship (specPath workId) (clarificationPath workId) None None "sourceSpec" "current" []
            relationship (specPath workId) (checklistPath workId) None None "checklistSourceSpec" "current" []
            relationship
                (clarificationPath workId)
                (checklistPath workId)
                None
                None
                "checklistSourceClarifications"
                "current"
                []
            relationship (specPath workId) (planPath workId) None None "planSourceSpec" "current" []
            relationship (clarificationPath workId) (planPath workId) None None "planSourceClarifications" "current" []
            relationship (checklistPath workId) (planPath workId) None None "planSourceChecklist" "current" []
            relationship (specPath workId) (tasksPath workId) None None "taskSourceSpec" "current" []
            relationship (clarificationPath workId) (tasksPath workId) None None "taskSourceClarifications" "current" []
            relationship (checklistPath workId) (tasksPath workId) None None "taskSourceChecklist" "current" []
            relationship (planPath workId) (tasksPath workId) None None "taskSourcePlan" "current" [] ]
          dispositionRelationships
              (specPath workId)
              "requirementDisposition"
              (specFacts.RequirementIds |> List.map _.Value)
          dispositionRelationships
              (specPath workId)
              "acceptanceDisposition"
              (specFacts.AcceptanceScenarioIds |> List.map _.Value)
          dispositionRelationships
              (clarificationPath workId)
              "clarificationDisposition"
              (clarificationFacts.Decisions
               |> List.map (fun decision -> decision.DecisionId.Value))
          dispositionRelationships
              (checklistPath workId)
              "checklistDeferralDisposition"
              (checklistFacts.AcceptedDeferrals
               |> List.map (fun result -> result.ResultId.Value))
          dispositionRelationships
              (planPath workId)
              "planDecisionDisposition"
              (planFacts.Decisions |> List.map (fun decision -> decision.DecisionId.Value))
          dispositionRelationships
              (planPath workId)
              "contractDisposition"
              (planFacts.ContractReferences
               |> List.map (fun contract -> contract.ContractId.Value))
          dispositionRelationships
              (planPath workId)
              "verificationDisposition"
              (planFacts.VerificationObligations
               |> List.map (fun obligation -> obligation.ObligationId.Value))
          dispositionRelationships
              (planPath workId)
              "migrationDisposition"
              (planFacts.MigrationNotes
               |> List.map (fun migration -> migration.MigrationId.Value))
          dispositionRelationships
              (planPath workId)
              "generatedViewDisposition"
              (planFacts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value))
          taskFacts.Tasks
          |> List.collect (fun task ->
              task.Dependencies
              |> List.map (fun dependency ->
                  relationship
                      (tasksPath workId)
                      (tasksPath workId)
                      (Some task.Id.Value)
                      (Some dependency.Value)
                      "taskDependency"
                      "current"
                      [])) ]
        |> List.concat
        |> List.sortBy (fun relationship ->
            relationship.SourcePath, relationship.Relationship, relationship.SourceId, relationship.TargetId)

    let analysisSourceFromSnapshot (path: string) (text: string) : GeneratedViewSource =
        { Path = path
          Digest = Some(SchemaVersionModule.sha256Text text)
          SchemaVersion = Some 1
          SchemaStatus = Some "current" }

    let analysisSources
        workId
        workModelJson
        specText
        clarificationText
        checklistText
        planText
        tasksText
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
          workModelJson |> Option.map (analysisSourceFromSnapshot (workModelPath workId)) ]
        |> List.choose id
        |> List.sortBy (fun source -> source.Path)

    let analysisSourceKind (path: string) =
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
        elif path.EndsWith("/work-model.json", StringComparison.OrdinalIgnoreCase) then
            "workModel"
        else
            "source"

    let writeStringArray (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray(name)

        values
        |> List.sort
        |> List.iter (fun value -> writer.WriteStringValue(value: string))

        writer.WriteEndArray()

    let writeDigestObject (writer: Utf8JsonWriter) (name: string) (digest: SourceDigest option) =
        match digest with
        | Some digest ->
            writer.WriteStartObject(name)
            writer.WriteString("algorithm", digest.Algorithm)
            writer.WriteString("value", digest.Value)
            writer.WriteEndObject()
        | None -> writer.WriteNull name

    let writeAnalysisDiagnosticJson (writer: Utf8JsonWriter) (diagnostic: Diagnostic) =
        writer.WriteStartObject()
        writer.WriteString("id", diagnostic.Id)
        writer.WriteString("severity", DiagnosticsModule.severityValue diagnostic.Severity)

        match diagnostic.Artifact with
        | Some artifact -> writer.WriteString("artifact", artifact.Path)
        | None -> writer.WriteNull "artifact"

        writer.WriteString("message", diagnostic.Message)
        writer.WriteString("correction", diagnostic.Correction)
        writeStringArray writer "relatedIds" diagnostic.RelatedIds
        writer.WriteEndObject()

    let analysisFindingSeverity (diagnostic: Diagnostic) =
        match diagnostic.Id with
        | "missingDisposition" -> "missingDisposition"
        | "staleTask"
        | "stalePlanDecision"
        | "staleChecklistResult"
        | "staleGeneratedView" -> "staleSource"
        | "malformedGeneratedView"
        | "malformedAnalysisView"
        | "malformedTasksArtifact"
        | "malformedPlanFrontMatter"
        | "malformedChecklistFrontMatter"
        | "malformedClarificationFrontMatter"
        | "malformedSpecificationFacts"
        | "malformedSpecificationFrontMatter" -> "malformedSource"
        | "blockedGeneratedViewRefresh" -> "generatedView"
        | _ ->
            match diagnostic.Severity with
            | DiagnosticSeverity.DiagnosticError -> "blocking"
            | DiagnosticSeverity.DiagnosticWarning -> "warning"
            | DiagnosticSeverity.DiagnosticInfo -> "advisory"

    let analysisFindingCategory severity =
        match severity with
        | "missingDisposition" -> "missingDisposition"
        | "staleSource" -> "staleSource"
        | "malformedSource" -> "malformedSource"
        | "generatedView" -> "generatedView"
        | "blocking" -> "blocking"
        | "warning" -> "warning"
        | _ -> "advisory"

    let diagnosticPath (diagnostic: Diagnostic) =
        diagnostic.Artifact |> Option.map _.Path |> Option.defaultValue ""

    let analysisFindings (diagnostics: Diagnostic list) =
        diagnostics
        |> DiagnosticsModule.sort
        |> List.mapi (fun index diagnostic ->
            let severity = analysisFindingSeverity diagnostic
            let id = sprintf "AF%03d" (index + 1)
            id, diagnostic, severity)

    let countFindings severity (findings: (string * Diagnostic * string) list) =
        findings
        |> List.filter (fun (_, _, findingSeverity) -> findingSeverity = severity)
        |> List.length

    let analysisReadiness
        (acceptedDeferralCount: int)
        (relationships: AnalysisRelationshipDraft list)
        (diagnostics: Diagnostic list)
        =
        let findings = analysisFindings diagnostics
        let blockingCount = countFindings "blocking" findings
        let missingDispositionCount = countFindings "missingDisposition" findings
        let staleSourceCount = countFindings "staleSource" findings
        let malformedSourceCount = countFindings "malformedSource" findings
        let generatedViewFindingCount = countFindings "generatedView" findings
        let warningCount = countFindings "warning" findings
        let advisoryCount = countFindings "advisory" findings + acceptedDeferralCount

        let status =
            if blockingCount > 0 || missingDispositionCount > 0 || malformedSourceCount > 0 then
                "needsCorrection"
            elif staleSourceCount > 0 || generatedViewFindingCount > 0 then
                "needsGeneratedViewRefresh"
            else
                "implementationReady"

        { WorkId = ""
          Stage = "analyze"
          Status = status
          AnalysisPath = ""
          SourceCount = 0
          SourceRelationshipCount = List.length relationships
          ReadyFindingCount =
            if status = "implementationReady" then
                List.length relationships
            else
                0
          AdvisoryCount = advisoryCount
          WarningCount = warningCount
          BlockingCount = blockingCount
          StaleSourceCount = staleSourceCount
          MissingDispositionCount = missingDispositionCount
          MalformedSourceCount = malformedSourceCount
          GeneratedViewFindingCount = generatedViewFindingCount
          AcceptedDeferralCount = acceptedDeferralCount
          Readiness = status }

    let analysisBoundaryFacts () : GovernanceCompatibilityFact list =
        [ { Path = ".fsgg/policy.yml"
            Relationship = "optionalGovernancePolicy"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/capabilities.yml"
            Relationship = "optionalGovernanceCapabilities"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/tooling.yml"
            Relationship = "optionalGovernanceTooling"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] } ]

    // --- Shared readiness-view writers (feature 061 / issue #71) ---
    // analysis/verify/ship emit a structurally identical envelope: a common preamble
    // (schemaVersion..generator), a sources array (differing only by the source-kind
    // classifier), and shared generatedViews/boundaryFacts/diagnostics/nextAction
    // sub-writers. Routing all three through these helpers guarantees the views cannot
    // drift structurally; each helper emits the same token sequence the inline code did,
    // so the byte-exact JSON contract is preserved.

    let writeViewPreamble
        (writer: Utf8JsonWriter)
        (workId: string)
        (stage: string)
        (status: string)
        (generator: GeneratorVersion)
        =
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("viewVersion", "1.0")
        writer.WriteString("workId", workId)
        writer.WriteString("stage", stage)
        writer.WriteString("status", status)
        writer.WriteString("generator", $"{generator.Id}/{generator.Version}")

    let writeSourcesArray (writer: Utf8JsonWriter) (sourceKind: string -> string) (sources: GeneratedViewSource list) =
        writer.WriteStartArray("sources")

        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (fun source ->
            writer.WriteStartObject()
            writer.WriteString("path", source.Path)
            writer.WriteString("kind", sourceKind source.Path)
            writeDigestObject writer "digest" source.Digest

            match source.SchemaVersion with
            | Some version -> writer.WriteNumber("schemaVersion", version)
            | None -> writer.WriteNull "schemaVersion"

            match source.SchemaStatus with
            | Some status -> writer.WriteString("schemaStatus", status)
            | None -> writer.WriteNull "schemaStatus"

            writer.WriteEndObject())

        writer.WriteEndArray()

    let writeLifecycleReadiness
        (writer: Utf8JsonWriter)
        (lifecycleStatus: string)
        (lifecycleStages: (string * string) list)
        =
        writer.WriteStartObject("lifecycleReadiness")
        writer.WriteString("status", lifecycleStatus)
        writer.WriteStartArray("stages")

        lifecycleStages
        |> List.sortBy fst
        |> List.iter (fun (stage, status) ->
            writer.WriteStartObject()
            writer.WriteString("stage", stage)
            writer.WriteString("status", status)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()

    let writeGeneratedViewsArray (writer: Utf8JsonWriter) (generatedViews: GeneratedViewState list) =
        writer.WriteStartArray("generatedViews")

        generatedViews
        |> List.sortBy (fun view -> view.Path)
        |> List.iter (fun view ->
            writer.WriteStartObject()
            writer.WriteString("path", view.Path)
            writer.WriteString("kind", view.Kind)
            writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
            writeStringArray writer "diagnosticIds" view.DiagnosticIds
            writer.WriteEndObject())

        writer.WriteEndArray()

    let writeBoundaryFacts (writer: Utf8JsonWriter) (key: string) =
        writer.WriteStartArray(key)

        analysisBoundaryFacts ()
        |> List.iter (fun fact ->
            writer.WriteStartObject()
            writer.WriteString("path", fact.Path)
            writer.WriteString("relationship", fact.Relationship)
            writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
            writer.WriteString("state", fact.State)
            writeStringArray writer "diagnosticIds" fact.DiagnosticIds
            writer.WriteEndObject())

        writer.WriteEndArray()

    let writeViewDiagnostics (writer: Utf8JsonWriter) (diagnostics: Diagnostic list) =
        writer.WriteStartArray("diagnostics")

        diagnostics
        |> DiagnosticsModule.sort
        |> List.iter (writeAnalysisDiagnosticJson writer)

        writer.WriteEndArray()

    /// Findings shape shared by verify and ship (severity == category, no state field).
    let writeReadinessFindings (writer: Utf8JsonWriter) (findings: (string * Diagnostic * string) list) =
        writer.WriteStartArray("findings")

        findings
        |> List.iter (fun (id, diagnostic, severity) ->
            writer.WriteStartObject()
            writer.WriteString("id", id)
            writer.WriteString("severity", severity)
            writer.WriteString("category", severity)
            writer.WriteString("path", diagnosticPath diagnostic)
            writeStringArray writer "relatedIds" diagnostic.RelatedIds
            writer.WriteString("message", diagnostic.Message)
            writer.WriteString("correction", diagnostic.Correction)
            writer.WriteEndObject())

        writer.WriteEndArray()

    let writeNextAction
        (writer: Utf8JsonWriter)
        (isReady: bool)
        (readyActionId: string)
        (readyReason: string)
        (blockedReason: string)
        =
        writer.WriteStartObject("nextAction")

        if isReady then
            writer.WriteString("actionId", readyActionId)
            writer.WriteNull("command")
            writer.WriteString("reason", readyReason)
        else
            writer.WriteString("actionId", "correctBlockingDiagnostics")
            writer.WriteNull("command")
            writer.WriteString("reason", blockedReason)

        writer.WriteEndObject()

    let writeReadinessEnvelope
        (workId: string)
        (stage: string)
        (status: string)
        (generator: GeneratorVersion)
        (sourceKind: string -> string)
        (sources: GeneratedViewSource list)
        (writeBody: Utf8JsonWriter -> unit)
        : string =
        // The single frame all three readiness views share: the MemoryStream/Utf8JsonWriter
        // lifecycle, the opening object + preamble + sources, and the terminal close. Each
        // view supplies its ordered body, so analysis/verify/ship cannot drift structurally
        // and every emitted byte is preserved (feature 068 / FR-001..003).
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writeViewPreamble writer workId stage status generator
        writeSourcesArray writer sourceKind sources
        writeBody writer
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    /// The identical verify+ship tail: findings, governance boundary facts, view
    /// diagnostics, and the readiness scalar. Each caller supplies its own nextAction.
    let writeGovernanceReadinessTail
        (writer: Utf8JsonWriter)
        (findings: (string * Diagnostic * string) list)
        (diagnostics: Diagnostic list)
        (readiness: string)
        =
        writeReadinessFindings writer findings
        writeBoundaryFacts writer "governanceCompatibility"
        writeViewDiagnostics writer diagnostics
        writer.WriteString("readiness", readiness)

    let analysisJson
        (workId: string)
        (generator: GeneratorVersion)
        (sources: GeneratedViewSource list)
        (relationships: AnalysisRelationshipDraft list)
        (readiness: AnalysisSummary)
        (diagnostics: Diagnostic list)
        (generatedViews: GeneratedViewState list)
        =
        let findings = analysisFindings diagnostics

        writeReadinessEnvelope workId "analyze" readiness.Readiness generator analysisSourceKind sources (fun writer ->
            writer.WriteStartArray("sourceRelationships")

            relationships
            |> List.mapi (fun index relationship -> sprintf "AR%03d" (index + 1), relationship)
            |> List.iter (fun (id, relationship) ->
                writer.WriteStartObject()
                writer.WriteString("id", id)
                writer.WriteString("sourcePath", relationship.SourcePath)
                writer.WriteString("targetPath", relationship.TargetPath)

                match relationship.SourceId with
                | Some value -> writer.WriteString("sourceId", value)
                | None -> writer.WriteNull "sourceId"

                match relationship.TargetId with
                | Some value -> writer.WriteString("targetId", value)
                | None -> writer.WriteNull "targetId"

                writer.WriteString("relationship", relationship.Relationship)
                writer.WriteString("state", relationship.State)
                writeStringArray writer "diagnosticIds" relationship.DiagnosticIds
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteStartObject("readiness")
            writer.WriteString("status", readiness.Readiness)
            writer.WriteNumber("readyCount", readiness.ReadyFindingCount)
            writer.WriteNumber("advisoryCount", readiness.AdvisoryCount)
            writer.WriteNumber("warningCount", readiness.WarningCount)
            writer.WriteNumber("blockingCount", readiness.BlockingCount)
            writer.WriteNumber("staleSourceCount", readiness.StaleSourceCount)
            writer.WriteNumber("missingDispositionCount", readiness.MissingDispositionCount)
            writer.WriteNumber("malformedSourceCount", readiness.MalformedSourceCount)
            writer.WriteNumber("generatedViewFindingCount", readiness.GeneratedViewFindingCount)
            writer.WriteNumber("acceptedDeferralCount", readiness.AcceptedDeferralCount)
            writer.WriteEndObject()
            writer.WriteStartArray("findings")

            findings
            |> List.iter (fun (id, diagnostic, severity) ->
                writer.WriteStartObject()
                writer.WriteString("id", id)
                writer.WriteString("category", analysisFindingCategory severity)
                writer.WriteString("severity", severity)
                writer.WriteString("state", if severity = "ready" then "closed" else "open")
                writer.WriteString("path", diagnosticPath diagnostic)
                writeStringArray writer "relatedIds" diagnostic.RelatedIds
                writer.WriteString("message", diagnostic.Message)
                writer.WriteString("correction", diagnostic.Correction)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writeGeneratedViewsArray writer generatedViews
            writeBoundaryFacts writer "optionalBoundaryFacts"
            writeViewDiagnostics writer diagnostics

            writeNextAction
                writer
                (readiness.Readiness = "implementationReady")
                "analysis.next.implement"
                "Lifecycle sources are current and ready for implementation."
                "Analysis found lifecycle diagnostics that must be corrected before implementation.")

    let existingAnalysisDiagnostic workId model =
        let path = analysisPath workId

        match snapshot path model with
        | None -> None
        | Some existing ->
            match parseAnalysisView existing with
            | Error diagnostics ->
                diagnostics
                |> List.tryHead
                |> Option.map (fun diagnostic -> malformedAnalysisView path diagnostic.Message)
            | Ok view when not (String.Equals(view.WorkId.Value, workId, StringComparison.OrdinalIgnoreCase)) ->
                Some(analysisIdentityMismatch path workId view.WorkId.Value)
            | Ok _ -> None

    let analysisPlan
        (workId: string)
        (specText: string)
        (clarificationText: string)
        (checklistText: string)
        (planText: string)
        (tasksText: string)
        (workModelJson: string option)
        (relationships: AnalysisRelationshipDraft list)
        (diagnostics: Diagnostic list)
        (generatedViews: GeneratedViewState list)
        (model: CommandModel)
        =
        let path = analysisPath workId

        let sources =
            analysisSources workId workModelJson specText clarificationText checklistText planText tasksText model

        let acceptedDeferralCount =
            diagnostics
            |> List.collect (fun diagnostic -> diagnostic.RelatedIds)
            |> List.filter (fun id ->
                id.StartsWith("DEC-", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("CR-", StringComparison.OrdinalIgnoreCase))
            |> List.distinct
            |> List.length

        let readiness = analysisReadiness acceptedDeferralCount relationships diagnostics

        let summary =
            { readiness with
                WorkId = workId
                AnalysisPath = path
                SourceCount = List.length sources }

        let text =
            analysisJson workId model.Request.GeneratorVersion sources relationships summary diagnostics generatedViews

        let outputDigest = SchemaVersionModule.outputSha256Text text

        let view =
            generatedViewState
                path
                "analysis"
                model.Request.GeneratorVersion
                sources
                (Some outputDigest)
                GeneratedViewCurrency.Current
                []

        summary, text, view

    let sourceFromEntry (entry: SourceEntry) =
        { Path = entry.Path
          Digest = Some entry.SourceDigest
          SchemaVersion =
            if entry.SchemaVersion <= 0 then
                None
            else
                Some entry.SchemaVersion
          SchemaStatus = Some entry.SchemaStatus }

    let charterSource path text =
        { Path = path
          Digest = Some(SchemaVersionModule.sha256Text text)
          SchemaVersion = Some 1
          SchemaStatus = Some "current" }

    let existingGeneratedViewDiagnostic workId path model =
        match snapshot path model with
        | None -> None
        | Some generated ->
            match GenerationManifestModule.parseWorkModelMetadata path generated.Text with
            | Error _ -> Some(malformedGeneratedView path)
            | Ok _ ->
                // §3.4: the currency-check input set MUST mirror the exact authored-source
                // set used to generate the work model (`workModelSnapshots`), including
                // plan.md and charter.md. Omitting them made `sourceStale`'s "recorded
                // source absent from current set" branch fire on every clean run (FR-005/006);
                // genuine source-digest drift still flags via `sourceStale` (FR-007).
                let currentSnapshots =
                    [ snapshot ".fsgg/project.yml" model
                      snapshot ".fsgg/sdd.yml" model
                      snapshot ".fsgg/agents.yml" model
                      snapshot (specPath workId) model
                      snapshot (clarificationPath workId) model
                      snapshot (checklistPath workId) model
                      snapshot (planPath workId) model
                      snapshot (charterPath workId) model
                      snapshot (tasksPath workId) model
                      snapshot (evidencePath workId) model
                      Some generated ]
                    |> List.choose id

                match
                    SerializationModule.checkGeneratedWorkModelCurrency
                        currentSnapshots
                        workId
                        model.Request.GeneratorVersion
                with
                | [] -> None
                | _ ->
                    Some(
                        commandDiagnostic
                            "staleGeneratedView"
                            DiagnosticSeverity.DiagnosticWarning
                            (Some path)
                            $"Generated view '{path}' is stale."
                            "Regenerate readiness/<id>/work-model.json from current lifecycle sources."
                            [ path ]
                    )

    let workModelSnapshots
        workId
        charterText
        specText
        clarificationText
        checklistText
        planText
        tasksText
        evidenceText
        model
        =
        [ snapshot ".fsgg/project.yml" model
          snapshot ".fsgg/sdd.yml" model
          snapshot ".fsgg/agents.yml" model
          specText
          |> Option.map (fun text -> { Path = specPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (specPath workId) model)
          clarificationText
          |> Option.map (fun text ->
              { Path = clarificationPath workId
                Text = text })
          |> Option.orElseWith (fun () -> snapshot (clarificationPath workId) model)
          checklistText
          |> Option.map (fun text ->
              { Path = checklistPath workId
                Text = text })
          |> Option.orElseWith (fun () -> snapshot (checklistPath workId) model)
          planText
          |> Option.map (fun text -> { Path = planPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (planPath workId) model)
          tasksText
          |> Option.map (fun text -> { Path = tasksPath workId; Text = text })
          |> Option.orElseWith (fun () -> snapshot (tasksPath workId) model)
          evidenceText
          |> Option.map (fun text ->
              { Path = evidencePath workId
                Text = text })
          |> Option.orElseWith (fun () -> snapshot (evidencePath workId) model)
          charterText
          |> Option.map (fun text ->
              { Path = charterPath workId
                Text = text })
          |> Option.orElseWith (fun () -> snapshot (charterPath workId) model) ]
        |> List.choose id
        |> List.map (fun snapshot ->
            { snapshot with
                Path = normalizeRelativePath snapshot.Path })

    let generatedViewPlan
        (request: CommandRequest)
        (workId: string)
        (charterText: string option)
        (specText: string option)
        (clarificationText: string option)
        (checklistText: string option)
        (planText: string option)
        (tasksText: string option)
        (evidenceText: string option)
        (commandDiagnostics: Diagnostic list)
        (model: CommandModel)
        =
        let path = workModelPath workId
        let currentDiagnostic = existingGeneratedViewDiagnostic workId path model
        let blockingCommandIds = blockingDiagnosticIds commandDiagnostics

        if not (List.isEmpty blockingCommandIds) then
            let sources =
                [ charterText |> Option.map (fun text -> charterSource (charterPath workId) text)
                  specText |> Option.map (fun text -> charterSource (specPath workId) text)
                  clarificationText
                  |> Option.map (fun text -> charterSource (clarificationPath workId) text)
                  checklistText
                  |> Option.map (fun text -> charterSource (checklistPath workId) text)
                  planText |> Option.map (fun text -> charterSource (planPath workId) text)
                  tasksText |> Option.map (fun text -> charterSource (tasksPath workId) text) ]
                |> List.choose id

            let view =
                generatedViewState
                    path
                    "workModel"
                    request.GeneratorVersion
                    sources
                    None
                    GeneratedViewCurrency.Blocked
                    blockingCommandIds

            currentDiagnostic |> Option.toList, view, []
        else
            let snapshots =
                workModelSnapshots
                    workId
                    charterText
                    specText
                    clarificationText
                    checklistText
                    planText
                    tasksText
                    evidenceText
                    model

            let result =
                SerializationModule.generateWorkModel
                    { WorkId = workId
                      Snapshots = snapshots
                      GeneratorVersion = request.GeneratorVersion
                      ExpectedOutputPath = Some path }

            let blockingModelDiagnostics = WorkModelModule.blockingDiagnostics result.Model

            if List.isEmpty blockingModelDiagnostics then
                let sources = result.Model.Sources |> List.map sourceFromEntry

                let diagnosticIds =
                    currentDiagnostic
                    |> Option.map (fun diagnostic -> [ diagnostic.Id ])
                    |> Option.defaultValue []

                let view =
                    generatedViewState
                        path
                        "workModel"
                        request.GeneratorVersion
                        sources
                        (Some result.OutputDigest)
                        GeneratedViewCurrency.Current
                        diagnosticIds

                let effects =
                    [ CreateDirectory(readinessDirectory workId)
                      WriteFile(path, result.Json, GeneratedView) ]

                currentDiagnostic |> Option.toList, view, effects
            else
                let existing = snapshot path model

                let currency =
                    match existing, currentDiagnostic with
                    | None, _ -> GeneratedViewCurrency.Missing
                    | Some _, Some diagnostic when diagnostic.Id = "malformedGeneratedView" ->
                        GeneratedViewCurrency.Malformed
                    | Some _, Some diagnostic when diagnostic.Id = "staleGeneratedView" -> GeneratedViewCurrency.Stale
                    | Some _, _ -> GeneratedViewCurrency.Blocked

                let diagnostic =
                    match existing with
                    | None -> None
                    | Some _ -> Some(blockedGeneratedViewRefresh path (blockingModelDiagnostics |> List.map _.Id))

                let diagnostics = [ currentDiagnostic; diagnostic ] |> List.choose id
                let diagnosticIds = diagnostics |> List.map _.Id

                let sources =
                    [ charterText |> Option.map (fun text -> charterSource (charterPath workId) text)
                      specText |> Option.map (fun text -> charterSource (specPath workId) text)
                      clarificationText
                      |> Option.map (fun text -> charterSource (clarificationPath workId) text)
                      checklistText
                      |> Option.map (fun text -> charterSource (checklistPath workId) text)
                      planText |> Option.map (fun text -> charterSource (planPath workId) text)
                      tasksText |> Option.map (fun text -> charterSource (tasksPath workId) text) ]
                    |> List.choose id

                let view =
                    generatedViewState path "workModel" request.GeneratorVersion sources None currency diagnosticIds

                diagnostics, view, []

    let charterWriteEffects workId text =
        [ CreateDirectory($"work/{workId}")
          WriteFile(charterPath workId, text, AuthoredSource) ]
