namespace FS.GG.SDD.Commands

open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.Json.JsonWriters
open FS.GG.SDD.Commands.CommandTypes

module CommandSerialization =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let writeChange (writer: Utf8JsonWriter) (change: ArtifactChange) =
        writer.WriteStartObject()
        writer.WriteString("path", change.Path)
        writer.WriteString("kind", change.Kind)
        writer.WriteString("ownership", change.Ownership)
        writer.WriteString("operation", artifactOperationValue change.Operation)
        writeSourceDigest writer "beforeDigest" change.BeforeDigest
        writeSourceDigest writer "afterDigest" change.AfterDigest
        writer.WriteString("safeWriteDecision", change.SafeWriteDecision)
        writeStringList writer Sorted "diagnosticIds" change.DiagnosticIds
        writer.WriteEndObject()

    let writeSpecification (writer: Utf8JsonWriter) (summary: SpecificationSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("specification")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writeStringList writer Sorted "storyIds" summary.StoryIds
            writeStringList writer Sorted "requirementIds" summary.RequirementIds
            writeStringList writer Sorted "acceptanceScenarioIds" summary.AcceptanceScenarioIds
            writeStringList writer Sorted "ambiguityIds" summary.AmbiguityIds
            writer.WriteNumber("unresolvedAmbiguityCount", summary.UnresolvedAmbiguityCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "specification"

    let writeClarification (writer: Utf8JsonWriter) (summary: ClarificationSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("clarification")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("sourceSpec", summary.SourceSpec)
            writeStringList writer Sorted "questionIds" summary.QuestionIds
            writeStringList writer Sorted "answeredQuestionIds" summary.AnsweredQuestionIds
            writeStringList writer Sorted "decisionIds" summary.DecisionIds
            writeStringList writer Sorted "acceptedDeferralIds" summary.AcceptedDeferralIds
            writer.WriteNumber("remainingAmbiguityCount", summary.RemainingAmbiguityCount)
            writer.WriteNumber("blockingAmbiguityCount", summary.BlockingAmbiguityCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "clarification"

    let writeChecklist (writer: Utf8JsonWriter) (summary: ChecklistSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("checklist")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("sourceSpec", summary.SourceSpec)
            writer.WriteString("sourceClarifications", summary.SourceClarifications)
            writeStringList writer Sorted "itemIds" summary.ItemIds
            writeStringList writer Sorted "resultIds" summary.ResultIds
            writer.WriteNumber("passedCount", summary.PassedCount)
            writer.WriteNumber("failedBlockingCount", summary.FailedBlockingCount)
            writer.WriteNumber("acceptedDeferralCount", summary.AcceptedDeferralCount)
            writer.WriteNumber("staleResultCount", summary.StaleResultCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "checklist"

    let writePlan (writer: Utf8JsonWriter) (summary: PlanSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("plan")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("sourceSpec", summary.SourceSpec)
            writer.WriteString("sourceClarifications", summary.SourceClarifications)
            writer.WriteString("sourceChecklist", summary.SourceChecklist)
            writeStringList writer Sorted "decisionIds" summary.DecisionIds
            writeStringList writer Sorted "contractReferenceIds" summary.ContractReferenceIds
            writeStringList writer Sorted "verificationObligationIds" summary.VerificationObligationIds
            writeStringList writer Sorted "migrationNoteIds" summary.MigrationNoteIds
            writeStringList writer Sorted "generatedViewImpactIds" summary.GeneratedViewImpactIds
            writer.WriteNumber("acceptedDeferralCount", summary.AcceptedDeferralCount)
            writer.WriteNumber("staleDecisionCount", summary.StaleDecisionCount)
            writer.WriteNumber("blockingFindingCount", summary.BlockingFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "plan"

    let writeTasks (writer: Utf8JsonWriter) (summary: TasksSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("tasks")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("sourceSpec", summary.SourceSpec)
            writer.WriteString("sourceClarifications", summary.SourceClarifications)
            writer.WriteString("sourceChecklist", summary.SourceChecklist)
            writer.WriteString("sourcePlan", summary.SourcePlan)
            writeStringList writer Sorted "taskIds" summary.TaskIds
            writer.WriteNumber("dependencyCount", summary.DependencyCount)
            writer.WriteNumber("requiredSkillCount", summary.RequiredSkillCount)
            writer.WriteNumber("requiredEvidenceCount", summary.RequiredEvidenceCount)
            writer.WriteNumber("pendingCount", summary.PendingCount)
            writer.WriteNumber("inProgressCount", summary.InProgressCount)
            writer.WriteNumber("doneCount", summary.DoneCount)
            writer.WriteNumber("skippedCount", summary.SkippedCount)
            writer.WriteNumber("staleCount", summary.StaleCount)
            writer.WriteNumber("acceptedDeferralCount", summary.AcceptedDeferralCount)
            writer.WriteNumber("blockingFindingCount", summary.BlockingFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "tasks"

    let writeAnalysis (writer: Utf8JsonWriter) (summary: AnalysisSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("analysis")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("analysisPath", summary.AnalysisPath)
            writer.WriteNumber("sourceCount", summary.SourceCount)
            writer.WriteNumber("sourceRelationshipCount", summary.SourceRelationshipCount)
            writer.WriteNumber("readyFindingCount", summary.ReadyFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("warningCount", summary.WarningCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteNumber("staleSourceCount", summary.StaleSourceCount)
            writer.WriteNumber("missingDispositionCount", summary.MissingDispositionCount)
            writer.WriteNumber("malformedSourceCount", summary.MalformedSourceCount)
            writer.WriteNumber("generatedViewFindingCount", summary.GeneratedViewFindingCount)
            writer.WriteNumber("acceptedDeferralCount", summary.AcceptedDeferralCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "analysis"

    let writeEvidence (writer: Utf8JsonWriter) (summary: EvidenceSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("evidence")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("evidencePath", summary.EvidencePath)
            writeStringList writer Sorted "declarationIds" summary.DeclarationIds
            writer.WriteNumber("declarationCount", summary.DeclarationCount)
            writer.WriteNumber("obligationCount", summary.ObligationCount)
            writer.WriteNumber("supportedCount", summary.SupportedCount)
            writer.WriteNumber("deferredCount", summary.DeferredCount)
            writer.WriteNumber("missingCount", summary.MissingCount)
            writer.WriteNumber("staleCount", summary.StaleCount)
            writer.WriteNumber("syntheticCount", summary.SyntheticCount)
            writer.WriteNumber("invalidCount", summary.InvalidCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteNumber("sourceSnapshotCount", summary.SourceSnapshotCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "evidence"

    let writeVerification (writer: Utf8JsonWriter) (summary: VerificationSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("verification")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("verifyPath", summary.VerifyPath)
            writeStringList writer Sorted "findingIds" summary.FindingIds
            writer.WriteNumber("readyFindingCount", summary.ReadyFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("warningCount", summary.WarningCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteNumber("obligationCount", summary.ObligationCount)
            writer.WriteNumber("evidenceSupportedCount", summary.EvidenceSupportedCount)
            writer.WriteNumber("evidenceDeferredCount", summary.EvidenceDeferredCount)
            writer.WriteNumber("evidenceMissingCount", summary.EvidenceMissingCount)
            writer.WriteNumber("evidenceStaleCount", summary.EvidenceStaleCount)
            writer.WriteNumber("evidenceSyntheticCount", summary.EvidenceSyntheticCount)
            writer.WriteNumber("evidenceInvalidCount", summary.EvidenceInvalidCount)
            writer.WriteNumber("testSatisfiedCount", summary.TestSatisfiedCount)
            writer.WriteNumber("testDeferredCount", summary.TestDeferredCount)
            writer.WriteNumber("testMissingCount", summary.TestMissingCount)
            writer.WriteNumber("testStaleCount", summary.TestStaleCount)
            writer.WriteNumber("testInvalidCount", summary.TestInvalidCount)
            writer.WriteNumber("skillVisibleCount", summary.SkillVisibleCount)
            writer.WriteNumber("skillMissingCount", summary.SkillMissingCount)
            writer.WriteNumber("sourceSnapshotCount", summary.SourceSnapshotCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "verification"

    let writeShip (writer: Utf8JsonWriter) (summary: ShipSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("ship")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("shipPath", summary.ShipPath)
            writeStringList writer Sorted "findingIds" summary.FindingIds
            writer.WriteNumber("readyFindingCount", summary.ReadyFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("warningCount", summary.WarningCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteString("disposition", summary.Disposition)
            writer.WriteStartObject("lifecycleStageReadiness")

            summary.LifecycleStageReadiness
            |> List.sortBy fst
            |> List.iter (fun (stage, status) -> writer.WriteString(stage, status))

            writer.WriteEndObject()
            writer.WriteString("verificationReadiness", summary.VerificationReadiness)
            writer.WriteNumber("evidenceSupportedCount", summary.EvidenceSupportedCount)
            writer.WriteNumber("evidenceDeferredCount", summary.EvidenceDeferredCount)
            writer.WriteNumber("evidenceMissingCount", summary.EvidenceMissingCount)
            writer.WriteNumber("evidenceStaleCount", summary.EvidenceStaleCount)
            writer.WriteNumber("evidenceSyntheticCount", summary.EvidenceSyntheticCount)
            writer.WriteNumber("evidenceInvalidCount", summary.EvidenceInvalidCount)
            writer.WriteString("generatedViewState", summary.GeneratedViewState)
            writer.WriteNumber("sourceSnapshotCount", summary.SourceSnapshotCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "ship"

    let writeAgentGuidance (writer: Utf8JsonWriter) (summary: AgentGuidanceSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("agentGuidance")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writeStringList writer Sorted "generatedRoots" summary.GeneratedRoots
            writeStringList writer Sorted "generatedTargetIds" summary.GeneratedTargetIds
            writeStringList writer Sorted "refusedTargetIds" summary.RefusedTargetIds
            writeStringList writer Sorted "findingIds" summary.FindingIds
            writer.WriteNumber("readyFindingCount", summary.ReadyFindingCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("warningCount", summary.WarningCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteString("disposition", summary.Disposition)
            writer.WriteBoolean("equivalenceRequired", summary.EquivalenceRequired)
            writeStringList writer Sorted "divergentTargetIds" summary.DivergentTargetIds
            writer.WriteString("generatedViewState", summary.GeneratedViewState)
            writer.WriteNumber("sourceSnapshotCount", summary.SourceSnapshotCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "agentGuidance"

    let writeRefresh (writer: Utf8JsonWriter) (summary: RefreshSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("refresh")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writer.WriteString("summaryPath", summary.SummaryPath)
            writeStringList writer Sorted "refreshedViewIds" summary.RefreshedViewIds
            writeStringList writer Sorted "alreadyCurrentViewIds" summary.AlreadyCurrentViewIds
            writeStringList writer Sorted "blockedViewIds" summary.BlockedViewIds
            writeStringList writer Sorted "notApplicableViewIds" summary.NotApplicableViewIds
            writeStringList writer Sorted "preservedAuthoredPaths" summary.PreservedAuthoredPaths
            writeStringList writer Sorted "findingIds" summary.FindingIds
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteNumber("warningCount", summary.WarningCount)
            writer.WriteNumber("blockingCount", summary.BlockingCount)
            writer.WriteString("disposition", summary.Disposition)
            writer.WriteStartArray("perViewState")

            summary.PerViewState
            |> List.iter (fun (view, state) ->
                writer.WriteStartArray()
                writer.WriteStringValue(view: string)
                writer.WriteStringValue(state: string)
                writer.WriteEndArray())

            writer.WriteEndArray()
            writer.WriteNumber("sourceSnapshotCount", summary.SourceSnapshotCount)
            writer.WriteString("readiness", summary.Readiness)
            writer.WriteEndObject()
        | None -> writer.WriteNull "refresh"

    let writeScaffold (writer: Utf8JsonWriter) (summary: ScaffoldSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("scaffold")

            match summary.ProviderName with
            | Some name -> writer.WriteString("providerName", name)
            | None -> writer.WriteNull "providerName"

            match summary.ProviderContractVersion with
            | Some version -> writer.WriteString("providerContractVersion", version)
            | None -> writer.WriteNull "providerContractVersion"

            // Feature 052 (US1): provider-declared minimum coherent CLI version, string-or-null.
            match summary.RequiredMinimumCliVersion with
            | Some minimum -> writer.WriteString("requiredMinimumCliVersion", minimum)
            | None -> writer.WriteNull "requiredMinimumCliVersion"

            writer.WriteString("outcome", summary.Outcome)
            writer.WriteBoolean("skeletonCreated", summary.SkeletonCreated)
            writer.WriteBoolean("providerInvoked", summary.ProviderInvoked)
            writer.WriteNumber("producedPathCount", summary.ProducedPathCount)
            writeStringList writer Sorted "producedPaths" summary.ProducedPaths
            // 056: the `.claude`/`.codex` fan-out mirror copies (owner `mirrored` in
            // provenance), sorted; `[]` when the provider produced no skills.
            writeStringList writer Sorted "mirroredPaths" summary.MirroredPaths
            writer.WriteStartArray("effectiveParameters")

            summary.EffectiveParameters
            |> List.sortBy fst
            |> List.iter (fun (key, value) ->
                writer.WriteStartObject()
                writer.WriteString("key", key)
                writer.WriteString("value", value)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteString("repoInitOutcome", summary.RepoInitOutcome)
            writer.WriteNumber("executableScriptCount", summary.ExecutableScriptCount)
            writer.WriteNumber("executableScriptsSkipped", summary.ExecutableScriptsSkipped)
            writer.WriteString("nextActionHint", summary.NextActionHint)

            // Feature 054: additive provider-defect diagnostic block (FR-001/002/003).
            // Present (non-null) only on the three provider-defect outcomes; `null` on
            // success, dry-run, and every pre-invocation user-input block (FR-006). Fixed
            // key order; `exitCode` is int-or-null so a never-launched provider is never
            // confused with a real `0` (FR-003).
            match summary.ProviderInvocation with
            | Some invocation ->
                writer.WriteStartObject("providerInvocation")
                writer.WriteString("commandLine", invocation.CommandLine)
                writer.WriteBoolean("processStarted", invocation.ProcessStarted)

                match invocation.ExitCode with
                | Some code -> writer.WriteNumber("exitCode", code)
                | None -> writer.WriteNull "exitCode"

                writer.WriteString("standardOutput", invocation.StandardOutput)
                writer.WriteBoolean("standardOutputTruncated", invocation.StandardOutputTruncated)
                writer.WriteString("standardError", invocation.StandardError)
                writer.WriteBoolean("standardErrorTruncated", invocation.StandardErrorTruncated)
                writer.WriteEndObject()
            | None -> writer.WriteNull "providerInvocation"

            writer.WriteEndObject()
        | None -> writer.WriteNull "scaffold"

    // Feature 053: a `ReconciliationStep` — hand-ordered fields, `targetPaths` sorted.
    // `diffPreview` is deterministic content (version delta / sorted new-path list), not
    // the interactive confirm prompt, so it is contract-safe.
    let writeReconciliationStep (writer: Utf8JsonWriter) (step: ReconciliationStep) =
        writer.WriteStartObject()
        writer.WriteString("stepId", reconciliationStepIdValue step.StepId)
        writer.WriteString("kind", reconciliationStepIdValue step.Kind)
        writer.WriteString("diffPreview", step.DiffPreview)
        writer.WriteString("outcome", reconciliationOutcomeValue step.Outcome)
        writeStringList writer Sorted "targetPaths" step.TargetPaths
        writer.WriteEndObject()

    let writeDoctor (writer: Utf8JsonWriter) (summary: DoctorSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("doctor")
            writer.WriteBoolean("hasProvenance", summary.HasProvenance)

            match summary.ProviderName with
            | Some name -> writer.WriteString("providerName", name)
            | None -> writer.WriteNull "providerName"

            writer.WriteString("installedCliVersion", summary.InstalledCliVersion)

            match summary.RequiredMinimumCliVersion with
            | Some minimum -> writer.WriteString("requiredMinimumCliVersion", minimum)
            | None -> writer.WriteNull "requiredMinimumCliVersion"

            writer.WriteString("cliAxis", summary.CliAxis)

            match summary.CliBehindBy with
            | Some delta -> writer.WriteString("cliBehindBy", delta)
            | None -> writer.WriteNull "cliBehindBy"

            writer.WriteNumber("expectedArtifactCount", summary.ExpectedArtifactCount)
            writeStringList writer Sorted "missingArtifactPaths" summary.MissingArtifactPaths
            writeStringList writer Sorted "skillDriftPaths" summary.SkillDriftPaths
            writer.WriteStartArray("previewSteps")
            summary.PreviewSteps |> List.iter (writeReconciliationStep writer)
            writer.WriteEndArray()
            writer.WriteBoolean("isCoherent", summary.IsCoherent)
            writer.WriteEndObject()
        | None -> writer.WriteNull "doctor"

    let writeUpgrade (writer: Utf8JsonWriter) (summary: UpgradeSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("upgrade")
            writer.WriteBoolean("hasProvenance", summary.HasProvenance)
            writer.WriteString("mode", summary.Mode)
            writer.WriteBoolean("alreadyCoherent", summary.AlreadyCoherent)
            writer.WriteStartArray("steps")
            summary.Steps |> List.iter (writeReconciliationStep writer)
            writer.WriteEndArray()

            writeStringList
                writer
                Sorted
                "appliedStepIds"
                (summary.AppliedStepIds |> List.map reconciliationStepIdValue)

            writeStringList
                writer
                Sorted
                "skippedStepIds"
                (summary.SkippedStepIds |> List.map reconciliationStepIdValue)

            writeStringList writer Sorted "failedStepIds" (summary.FailedStepIds |> List.map reconciliationStepIdValue)
            writeStringList writer Sorted "skillDriftPaths" summary.SkillDriftPaths
            writer.WriteBoolean("residualDrift", summary.ResidualDrift)
            writer.WriteString("nextActionHint", summary.NextActionHint)
            writer.WriteEndObject()
        | None -> writer.WriteNull "upgrade"

    let private writeLintDefect (writer: Utf8JsonWriter) (defect: LintDefect) =
        writer.WriteStartObject()
        writer.WriteString("class", lintDefectClassValue defect.Class)
        writer.WriteString("id", defect.Diagnostic.Id)
        writer.WriteString("severity", severityValue defect.Diagnostic.Severity)

        match defect.Diagnostic.Location with
        | Some loc ->
            writer.WriteStartObject("location")

            match loc.Line with
            | Some line -> writer.WriteNumber("line", line)
            | None -> writer.WriteNull "line"

            match loc.Column with
            | Some col -> writer.WriteNumber("column", col)
            | None -> writer.WriteNull "column"

            writer.WriteEndObject()
        | None -> writer.WriteNull "location"

        writer.WriteString("message", defect.Diagnostic.Message)
        writer.WriteString("correction", defect.Diagnostic.Correction)

        match defect.GrammarPointer with
        | Some pointer ->
            writer.WriteStartObject("grammarPointer")
            writer.WriteString("doc", pointer.Doc)
            writer.WriteString("anchor", pointer.Anchor)

            match pointer.ExampleTag with
            | Some tag -> writer.WriteString("exampleTag", tag)
            | None -> writer.WriteNull "exampleTag"

            writer.WriteEndObject()
        | None -> writer.WriteNull "grammarPointer"

        writer.WriteEndObject()

    let writeLint (writer: Utf8JsonWriter) (summary: LintSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("lint")
            writer.WriteString("artifactPath", summary.ArtifactPath)
            writer.WriteString("kind", lintArtifactKindValue summary.Kind)
            writer.WriteString("outcome", lintOutcomeValue summary.Outcome)
            writer.WriteStartArray("defects")
            summary.Defects |> List.iter (writeLintDefect writer)
            writer.WriteEndArray()
            writer.WriteEndObject()
        | None -> writer.WriteNull "lint"

    let writeGeneratedSource (writer: Utf8JsonWriter) (source: GeneratedViewSource) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Path)
        writeSourceDigest writer "digest" source.Digest

        match source.SchemaVersion with
        | Some version -> writer.WriteNumber("schemaVersion", version)
        | None -> writer.WriteNull "schemaVersion"

        match source.SchemaStatus with
        | Some status -> writer.WriteString("schemaStatus", status)
        | None -> writer.WriteNull "schemaStatus"

        writer.WriteEndObject()

    let writeGenerator (writer: Utf8JsonWriter) (generator: GeneratorVersion option) =
        match generator with
        | Some generator ->
            writer.WriteStartObject("generator")
            writer.WriteString("id", generator.Id)
            writer.WriteString("version", generator.Version)
            writer.WriteEndObject()
        | None -> writer.WriteNull "generator"

    let writeGeneratedView (writer: Utf8JsonWriter) (view: GeneratedViewState) =
        writer.WriteStartObject()
        writer.WriteString("path", view.Path)
        writer.WriteString("kind", view.Kind)

        match view.SchemaVersion with
        | Some version -> writer.WriteNumber("schemaVersion", version)
        | None -> writer.WriteNull "schemaVersion"

        writeGenerator writer view.Generator
        writer.WriteStartArray("sources")

        view.Sources
        |> List.sortBy (fun source -> source.Path)
        |> List.iter (writeGeneratedSource writer)

        writer.WriteEndArray()
        writeOutputDigest writer "outputDigest" view.OutputDigest
        writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
        writeStringList writer Sorted "diagnosticIds" view.DiagnosticIds
        writer.WriteEndObject()

    let writeGovernanceFact (writer: Utf8JsonWriter) (fact: GovernanceCompatibilityFact) =
        writer.WriteStartObject()
        writer.WriteString("path", fact.Path)
        writer.WriteString("relationship", fact.Relationship)
        writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
        writer.WriteString("state", fact.State)
        writeStringList writer Sorted "diagnosticIds" fact.DiagnosticIds
        writer.WriteEndObject()

    let writeNextAction (writer: Utf8JsonWriter) (nextAction: NextAction option) =
        match nextAction with
        | Some action ->
            writer.WriteStartObject("nextAction")
            writer.WriteString("actionId", action.ActionId)

            match action.Command with
            | Some command -> writer.WriteString("command", commandName command)
            | None -> writer.WriteNull "command"

            match action.WorkId with
            | Some workId -> writer.WriteString("workId", workId)
            | None -> writer.WriteNull "workId"

            writer.WriteString("reason", action.Reason)
            writeStringList writer Sorted "requiredArtifacts" action.RequiredArtifacts
            writeStringList writer Sorted "blockingDiagnosticIds" action.BlockingDiagnosticIds
            writer.WriteEndObject()
        | None -> writer.WriteNull "nextAction"

    let writeHelpFlag (writer: Utf8JsonWriter) (flag: HelpFlag) =
        writer.WriteStartObject()
        writer.WriteString("name", flag.Name)

        match flag.Argument with
        | Some argument -> writer.WriteString("argument", argument)
        | None -> writer.WriteNull "argument"

        writer.WriteString("description", flag.Description)
        writer.WriteEndObject()

    // §3.5: emitted like `writeScaffold` — the `help` object when `Some`, `null` when
    // `None`, always present. Pure projection of static content (deterministic, FR-012).
    let writeHelp (writer: Utf8JsonWriter) (help: HelpSummary option) =
        match help with
        | Some summary ->
            writer.WriteStartObject("help")

            match summary.Scope with
            | TopLevel ->
                writer.WriteString("scope", "topLevel")
                writer.WriteNull "command"
            | Command name ->
                writer.WriteString("scope", "command")
                writer.WriteString("command", name)

            writer.WriteString("usage", summary.Usage)

            writer.WriteStartArray("commands")

            summary.Commands
            |> List.iter (fun entry ->
                writer.WriteStartObject()
                writer.WriteString("name", entry.Name)
                writer.WriteString("description", entry.Description)
                writer.WriteEndObject())

            writer.WriteEndArray()

            writer.WriteStartArray("globalFlags")
            summary.GlobalFlags |> List.iter (writeHelpFlag writer)
            writer.WriteEndArray()

            writer.WriteStartArray("commandFlags")
            summary.CommandFlags |> List.iter (writeHelpFlag writer)
            writer.WriteEndArray()

            writer.WriteEndObject()
        | None -> writer.WriteNull "help"

    // Feature 084: the additive `lifecycleStatus` object — the authoritative footer fact. The
    // stage-state token comes from the single canonical map (CommandTypes.stageStateName).
    let stageStateValue = stageStateName

    let writeLifecycleStatus (writer: Utf8JsonWriter) (status: LifecycleStatus) =
        writer.WriteStartObject("lifecycleStatus")

        match status.WorkId with
        | Some workId -> writer.WriteString("workId", workId)
        | None -> writer.WriteNull "workId"

        writer.WriteBoolean("isLifecycleStage", status.IsLifecycleStage)

        match status.CurrentOrdinal with
        | Some ordinal -> writer.WriteNumber("currentOrdinal", ordinal)
        | None -> writer.WriteNull "currentOrdinal"

        writer.WriteNumber("totalStages", status.TotalStages)
        writer.WriteString("outcome", outcomeValue status.Outcome)

        match status.NextCommand with
        | Some command -> writer.WriteString("nextCommand", commandName command)
        | None -> writer.WriteNull "nextCommand"

        writer.WriteStartArray("stages")

        for entry in status.Stages do
            writer.WriteStartObject()
            writer.WriteString("command", commandName entry.Command)
            writer.WriteNumber("ordinal", entry.Ordinal)
            writer.WriteString("state", stageStateValue entry.State)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()

    let serializeReport (report: CommandReport) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", report.SchemaVersion)
        writer.WriteString("reportVersion", report.ReportVersion)
        writer.WriteStartObject("command")
        writer.WriteString("name", commandName report.Command)
        writer.WriteString("stage", commandStage report.Command)
        writer.WriteEndObject()
        writer.WriteStartObject("context")
        writer.WriteString("projectRoot", report.ProjectRoot)

        match report.WorkId with
        | Some workId -> writer.WriteString("workId", workId)
        | None -> writer.WriteNull "workId"

        writer.WriteEndObject()
        writer.WriteStartObject("invocation")
        writer.WriteString("outputFormat", outputFormatValue report.OutputFormat)
        writer.WriteBoolean("dryRun", report.DryRun)
        writer.WriteEndObject()
        writer.WriteString("outcome", outcomeValue report.Outcome)
        writer.WriteStartArray("changedArtifacts")

        report.ChangedArtifacts
        |> List.sortBy (fun change -> change.Path, artifactOperationValue change.Operation, change.Ownership)
        |> List.iter (writeChange writer)

        writer.WriteEndArray()
        writeSpecification writer report.Specification
        writeClarification writer report.Clarification
        writeChecklist writer report.Checklist
        writePlan writer report.Plan
        writeTasks writer report.Tasks
        writeAnalysis writer report.Analysis
        writeEvidence writer report.Evidence
        writeVerification writer report.Verification
        writeShip writer report.Ship
        writeAgentGuidance writer report.AgentGuidance
        writeRefresh writer report.Refresh
        writeScaffold writer report.Scaffold
        writeDoctor writer report.Doctor
        writeUpgrade writer report.Upgrade
        writeLint writer report.Lint
        writer.WriteStartArray("generatedViews")

        report.GeneratedViews
        |> List.sortBy (fun view -> view.Path)
        |> List.iter (writeGeneratedView writer)

        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")

        report.Diagnostics
        |> DiagnosticsModule.sort
        |> List.iter (writeDiagnostic writer Sorted)

        writer.WriteEndArray()
        writer.WriteStartArray("governanceCompatibility")

        report.GovernanceCompatibility
        |> List.sortBy (fun fact -> fact.Path)
        |> List.iter (writeGovernanceFact writer)

        writer.WriteEndArray()
        writeNextAction writer report.NextAction
        writeHelp writer report.Help
        // Feature 084: the lifecycle-status fact is the final field — the footer is last.
        writeLifecycleStatus writer report.LifecycleStatus
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
