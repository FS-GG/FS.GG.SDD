namespace FS.GG.SDD.Commands

open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

module CommandSerialization =
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let writeStringList (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray(name)
        values |> List.sort |> List.iter (fun value -> writer.WriteStringValue(value: string))
        writer.WriteEndArray()

    let writeSourceDigest (writer: Utf8JsonWriter) (name: string) (digest: SourceDigest option) =
        match digest with
        | Some digest ->
            writer.WriteStartObject(name)
            writer.WriteString("algorithm", digest.Algorithm)
            writer.WriteString("value", digest.Value)
            writer.WriteEndObject()
        | None -> writer.WriteNull name

    let writeOutputDigest (writer: Utf8JsonWriter) (name: string) (digest: OutputDigest option) =
        match digest with
        | Some digest ->
            writer.WriteStartObject(name)
            writer.WriteString("algorithm", digest.Algorithm)
            writer.WriteString("value", digest.Value)
            writer.WriteEndObject()
        | None -> writer.WriteNull name

    let writeLocation (writer: Utf8JsonWriter) (location: SourceLocation option) =
        match location with
        | Some location ->
            writer.WriteStartObject("location")
            match location.Line with
            | Some line -> writer.WriteNumber("line", line)
            | None -> writer.WriteNull "line"
            match location.Column with
            | Some column -> writer.WriteNumber("column", column)
            | None -> writer.WriteNull "column"
            writer.WriteEndObject()
        | None -> writer.WriteNull "location"

    let writeDiagnostic (writer: Utf8JsonWriter) (diagnostic: Diagnostic) =
        writer.WriteStartObject()
        writer.WriteString("id", diagnostic.Id)
        writer.WriteString("severity", DiagnosticsModule.severityValue diagnostic.Severity)

        match diagnostic.Artifact with
        | Some artifact -> writer.WriteString("artifact", artifact.Path)
        | None -> writer.WriteNull "artifact"

        writeLocation writer diagnostic.Location
        writer.WriteString("message", diagnostic.Message)
        writer.WriteString("correction", diagnostic.Correction)
        writeStringList writer "relatedIds" diagnostic.RelatedIds
        writer.WriteEndObject()

    let writeChange (writer: Utf8JsonWriter) (change: ArtifactChange) =
        writer.WriteStartObject()
        writer.WriteString("path", change.Path)
        writer.WriteString("kind", change.Kind)
        writer.WriteString("ownership", change.Ownership)
        writer.WriteString("operation", artifactOperationValue change.Operation)
        writeSourceDigest writer "beforeDigest" change.BeforeDigest
        writeSourceDigest writer "afterDigest" change.AfterDigest
        writer.WriteString("safeWriteDecision", change.SafeWriteDecision)
        writeStringList writer "diagnosticIds" change.DiagnosticIds
        writer.WriteEndObject()

    let writeSpecification (writer: Utf8JsonWriter) (summary: SpecificationSummary option) =
        match summary with
        | Some summary ->
            writer.WriteStartObject("specification")
            writer.WriteString("workId", summary.WorkId)
            writer.WriteString("stage", summary.Stage)
            writer.WriteString("status", summary.Status)
            writeStringList writer "storyIds" summary.StoryIds
            writeStringList writer "requirementIds" summary.RequirementIds
            writeStringList writer "acceptanceScenarioIds" summary.AcceptanceScenarioIds
            writeStringList writer "ambiguityIds" summary.AmbiguityIds
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
            writeStringList writer "questionIds" summary.QuestionIds
            writeStringList writer "answeredQuestionIds" summary.AnsweredQuestionIds
            writeStringList writer "decisionIds" summary.DecisionIds
            writeStringList writer "acceptedDeferralIds" summary.AcceptedDeferralIds
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
            writeStringList writer "itemIds" summary.ItemIds
            writeStringList writer "resultIds" summary.ResultIds
            writer.WriteNumber("passedCount", summary.PassedCount)
            writer.WriteNumber("failedBlockingCount", summary.FailedBlockingCount)
            writer.WriteNumber("acceptedDeferralCount", summary.AcceptedDeferralCount)
            writer.WriteNumber("staleResultCount", summary.StaleResultCount)
            writer.WriteNumber("advisoryCount", summary.AdvisoryCount)
            writer.WriteEndObject()
        | None -> writer.WriteNull "checklist"

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
        view.Sources |> List.sortBy (fun source -> source.Path) |> List.iter (writeGeneratedSource writer)
        writer.WriteEndArray()
        writeOutputDigest writer "outputDigest" view.OutputDigest
        writer.WriteString("currency", generatedViewCurrencyValue view.Currency)
        writeStringList writer "diagnosticIds" view.DiagnosticIds
        writer.WriteEndObject()

    let writeGovernanceFact (writer: Utf8JsonWriter) (fact: GovernanceCompatibilityFact) =
        writer.WriteStartObject()
        writer.WriteString("path", fact.Path)
        writer.WriteString("relationship", fact.Relationship)
        writer.WriteBoolean("requiredBySdd", fact.RequiredBySdd)
        writer.WriteString("state", fact.State)
        writeStringList writer "diagnosticIds" fact.DiagnosticIds
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
            writeStringList writer "requiredArtifacts" action.RequiredArtifacts
            writeStringList writer "blockingDiagnosticIds" action.BlockingDiagnosticIds
            writer.WriteEndObject()
        | None -> writer.WriteNull "nextAction"

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
        writer.WriteString("overwritePolicy", overwritePolicyValue report.OverwritePolicy)
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
        writer.WriteStartArray("generatedViews")
        report.GeneratedViews |> List.sortBy (fun view -> view.Path) |> List.iter (writeGeneratedView writer)
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        report.Diagnostics |> DiagnosticsModule.sort |> List.iter (writeDiagnostic writer)
        writer.WriteEndArray()
        writer.WriteStartArray("governanceCompatibility")
        report.GovernanceCompatibility |> List.sortBy (fun fact -> fact.Path) |> List.iter (writeGovernanceFact writer)
        writer.WriteEndArray()
        writeNextAction writer report.NextAction
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
