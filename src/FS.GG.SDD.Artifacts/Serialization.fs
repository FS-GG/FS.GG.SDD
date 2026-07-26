namespace FS.GG.SDD.Artifacts

open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Artifacts.Json.JsonWriters

module Serialization =
    let normalizeSnapshotsToWorkModel snapshots workId =
        loadWorkItemFromSnapshots snapshots workId |> WorkModel.fromParsedWorkItem

    let writeSource (writer: Utf8JsonWriter) (source: SourceEntry) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Path)
        writer.WriteString("kind", source.Kind)
        writer.WriteString("owner", source.Owner)
        writer.WriteNumber("schemaVersion", source.SchemaVersion)

        match source.RawSchemaVersion with
        | Some raw -> writer.WriteString("rawSchemaVersion", raw)
        | None -> writer.WriteNull "rawSchemaVersion"

        writer.WriteString("schemaStatus", source.SchemaStatus)
        writeSourceDigest writer "sourceDigest" (Some source.SourceDigest)
        writer.WriteEndObject()

    let writeRequirement (writer: Utf8JsonWriter) (requirement: RequirementEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", requirement.Id)
        writer.WriteString("title", requirement.Title)
        writer.WriteString("text", requirement.Text)
        writeStringList writer SourceOrder "acceptanceCriteria" requirement.AcceptanceCriteria

        match requirement.Priority with
        | Some priority -> writer.WriteString("priority", priority)
        | None -> writer.WriteNull "priority"

        writeStringList writer SourceOrder "classification" requirement.Classification
        writer.WriteString("source", requirement.Source)
        writeLocation writer "sourceLocation" requirement.SourceLocation
        writeStringList writer SourceOrder "linkedTaskIds" requirement.LinkedTaskIds
        writeStringList writer SourceOrder "linkedEvidenceIds" requirement.LinkedEvidenceIds
        writer.WriteEndObject()

    let writeDecision (writer: Utf8JsonWriter) (decision: DecisionEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", decision.Id)
        writer.WriteString("title", decision.Title)
        writer.WriteString("decision", decision.Decision)
        writeStringList writer SourceOrder "requirementRefs" decision.RequirementRefs
        writeStringList writer SourceOrder "storyRefs" decision.StoryRefs
        writeStringList writer SourceOrder "acceptanceRefs" decision.AcceptanceRefs
        writer.WriteString("source", decision.Source)
        writeLocation writer "sourceLocation" decision.SourceLocation
        writeStringList writer SourceOrder "linkedTaskIds" decision.LinkedTaskIds
        writer.WriteEndObject()

    let writeTask (writer: Utf8JsonWriter) (task: TaskEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", task.Id)
        writer.WriteString("title", task.Title)
        writer.WriteString("status", task.Status)
        writer.WriteString("owner", task.Owner)
        writeStringList writer SourceOrder "dependencies" task.Dependencies
        writeStringList writer SourceOrder "requirements" task.Requirements
        writeStringList writer SourceOrder "decisions" task.Decisions
        writeStringList writer SourceOrder "sourceIds" task.SourceIds
        writeStringList writer SourceOrder "requiredSkills" task.RequiredSkills
        writeStringList writer SourceOrder "requiredEvidence" task.RequiredEvidence
        writer.WriteString("source", task.Source)
        writeLocation writer "sourceLocation" task.SourceLocation
        writer.WriteEndObject()

    let writePerformanceArtifact (writer: Utf8JsonWriter) (name: string) (artifact: PerformanceEvidenceArtifact) =
        writer.WriteStartObject name
        writer.WriteString("contractVersion", artifact.ContractVersion)

        match artifact.ClaimedBudgetPassed with
        | Some value -> writer.WriteBoolean("claimedBudgetPassed", value)
        | None -> writer.WriteNull "claimedBudgetPassed"

        writer.WriteStartArray "sampleSets"

        for sample in artifact.SampleSets do
            writer.WriteStartObject()
            writer.WriteString("workloadId", sample.WorkloadId)
            writer.WriteString("workloadDefinitionDigest", sample.WorkloadDefinitionDigest)
            writer.WriteString("workloadClass", sample.WorkloadClass)
            writer.WriteNumber("targetFps", sample.TargetFps)
            writer.WriteNumber("maxP95Ms", sample.MaxP95Ms)
            writer.WriteNumber("maxP99Ms", sample.MaxP99Ms)
            writer.WriteNumber("maxCatchUpFrames", sample.MaxCatchUpFrames)
            writer.WriteString("measurementScope", sample.MeasurementScope)
            writer.WriteString("requiredCapability", sample.RequiredCapability)
            writer.WriteString("hostProfile", sample.HostProfile)
            writeStringList writer SourceOrder "packageVersions" sample.PackageVersions
            writer.WriteString("measurementMode", sample.MeasurementMode)
            writeStringList writer SourceOrder "capabilities" sample.Capabilities
            writer.WriteString("warmupPolicy", sample.WarmupPolicy)
            writer.WriteString("samplePolicy", sample.SamplePolicy)
            writer.WriteString("capturedAtUtc", sample.CapturedAtUtc)
            writer.WriteString("currencyToken", sample.CurrencyToken)
            writer.WriteBoolean("probeReadbackContaminated", sample.ProbeReadbackContaminated)
            writer.WriteStartArray "durationSamplesMs"
            sample.DurationSamplesMs |> List.iter writer.WriteNumberValue
            writer.WriteEndArray()
            writer.WriteStartArray "catchUpFrames"
            sample.CatchUpFrames |> List.iter writer.WriteNumberValue
            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()

    let writeEvidence (writer: Utf8JsonWriter) (evidence: EvidenceEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", evidence.Id)
        writer.WriteString("kind", evidence.Kind)
        writer.WriteString("subjectType", evidence.SubjectType)
        writer.WriteString("subjectId", evidence.SubjectId)
        writeStringList writer SourceOrder "taskRefs" evidence.TaskRefs
        writeStringList writer SourceOrder "requirementRefs" evidence.RequirementRefs
        writeStringList writer SourceOrder "artifactRefs" evidence.ArtifactRefs
        writer.WriteString("result", evidence.Result)
        writer.WriteBoolean("synthetic", evidence.Synthetic)

        match evidence.PerformanceBudget with
        | Some budget ->
            writer.WriteStartObject("performanceBudget")
            writer.WriteString("artifactPath", budget.ArtifactPath)
            writer.WriteNumber("targetFps", budget.TargetFps)
            writeStringList writer SourceOrder "workloadIds" budget.WorkloadIds
            writeStringList writer SourceOrder "stressWorkloadIds" budget.StressWorkloadIds
            writer.WriteNumber("maxP95Ms", budget.MaxP95Ms)
            writer.WriteNumber("maxP99Ms", budget.MaxP99Ms)
            writer.WriteNumber("maxCatchUpFrames", budget.MaxCatchUpFrames)
            writer.WriteString("measurementScope", budget.MeasurementScope)
            writer.WriteString("requiredCapability", budget.RequiredCapability)
            writer.WriteBoolean("liveCompositorRequired", budget.LiveCompositorRequired)

            match budget.DeferralIssue with
            | Some issue -> writer.WriteString("deferralIssue", issue)
            | None -> writer.WriteNull "deferralIssue"

            writer.WriteEndObject()
        | None -> writer.WriteNull "performanceBudget"

        match evidence.PerformanceEvidenceArtifact with
        | Some artifact -> writePerformanceArtifact writer "performanceEvidenceArtifact" artifact
        | None -> writer.WriteNull "performanceEvidenceArtifact"

        writer.WriteStartArray "performanceMeasurements"

        for measurement in evidence.PerformanceMeasurements do
            writer.WriteStartObject()
            writer.WriteString("workloadId", measurement.WorkloadId)
            writer.WriteNumber("p95Ms", measurement.P95Ms)
            writer.WriteNumber("p99Ms", measurement.P99Ms)
            writer.WriteNumber("maxCatchUpFrames", measurement.MaxCatchUpFrames)
            writer.WriteEndObject()

        writer.WriteEndArray()

        match evidence.Rationale with
        | Some rationale -> writer.WriteString("rationale", rationale)
        | None -> writer.WriteNull "rationale"

        writer.WriteString("source", evidence.Source)
        writeLocation writer "sourceLocation" evidence.SourceLocation
        writer.WriteEndObject()

    let writeManifestSource (writer: Utf8JsonWriter) (source: SourceIdentity) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Artifact.Path)
        writeSourceDigest writer "digest" (Some source.Digest)

        match source.SchemaVersion with
        | Some version -> writer.WriteNumber("schemaVersion", version.Major)
        | None -> writer.WriteNull "schemaVersion"

        writer.WriteEndObject()

    let writeGeneratedView (writer: Utf8JsonWriter) (view: GenerationManifest) =
        writer.WriteStartObject()
        writer.WriteString("path", view.View.Path)
        writer.WriteString("kind", GenerationManifest.viewKindValue view.Kind)
        writer.WriteNumber("schemaVersion", view.SchemaVersion.Major)
        writer.WriteStartObject("generator")
        writer.WriteString("id", view.Generator.Id)
        writer.WriteString("version", view.Generator.Version)
        writer.WriteEndObject()
        writer.WriteStartArray("sources")
        view.Sources |> List.iter (writeManifestSource writer)
        writer.WriteEndArray()

        writeOutputDigest writer "outputDigest" view.OutputDigest

        writer.WriteString("currency", GenerationManifest.currencyStatusValue view.Currency)
        writer.WriteEndObject()

    let writeGovernanceBoundary (writer: Utf8JsonWriter) (boundary: GovernanceBoundaryEntry) =
        writer.WriteStartObject()
        writer.WriteString("path", boundary.Path)
        writer.WriteString("owner", boundary.Owner)
        writer.WriteBoolean("requiredBySdd", boundary.RequiredBySdd)
        writer.WriteString("relationship", boundary.Relationship)
        writer.WriteEndObject()

    let serializeWorkModel model =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", model.SchemaVersion)
        writer.WriteString("modelVersion", model.ModelVersion)
        writer.WriteString("workId", model.WorkId)
        writer.WriteStartObject("project")
        writer.WriteString("id", model.Project.Id)
        writer.WriteString("defaultWorkRoot", model.Project.DefaultWorkRoot)
        writer.WriteEndObject()
        writer.WriteStartArray("sources")
        model.Sources |> List.iter (writeSource writer)
        writer.WriteEndArray()
        writer.WriteStartObject("workItem")
        writer.WriteString("id", model.WorkItem.Id)
        writer.WriteString("title", model.WorkItem.Title)
        writer.WriteString("stage", model.WorkItem.Stage)
        writer.WriteString("changeTier", model.WorkItem.ChangeTier)
        writer.WriteString("status", model.WorkItem.Status)
        writer.WriteEndObject()
        writer.WriteStartArray("requirements")
        model.Requirements |> List.iter (writeRequirement writer)
        writer.WriteEndArray()
        writer.WriteStartArray("decisions")
        model.Decisions |> List.iter (writeDecision writer)
        writer.WriteEndArray()
        writer.WriteStartArray("tasks")
        model.Tasks |> List.iter (writeTask writer)
        writer.WriteEndArray()
        writer.WriteStartArray("evidence")
        model.Evidence |> List.iter (writeEvidence writer)
        writer.WriteEndArray()
        writer.WriteStartArray("generatedViews")
        model.GeneratedViews |> List.iter (writeGeneratedView writer)
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        model.Diagnostics |> List.iter (writeDiagnostic writer SourceOrder)
        writer.WriteEndArray()
        writer.WriteStartArray("governanceBoundaries")
        model.GovernanceBoundaries |> List.iter (writeGovernanceBoundary writer)
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let canonicalizeOutputDigestForHash (json: string) =
        Regex.Replace(
            json,
            "\"outputDigest\"\\s*:\\s*\\{\\s*\"algorithm\"\\s*:\\s*\"sha256\"\\s*,\\s*\"value\"\\s*:\\s*\"[a-f0-9]{64}\"\\s*\\}",
            "\"outputDigest\": null",
            RegexOptions.CultureInvariant
        )

    let applyGeneratedView
        (outputPath: string)
        (generator: GeneratorVersion)
        (outputDigest: OutputDigest option)
        (currency: GeneratedViewCurrencyStatus)
        (diagnostics: Diagnostic list)
        (model: WorkModel)
        =
        let sources =
            model.Sources
            |> List.map (fun source ->
                let artifact =
                    match
                        ArtifactRef.create source.Path (ArtifactKind.Other "generatedSource") ArtifactOwner.Sdd true
                    with
                    | Ok value -> value
                    | Error message -> invalidArg (nameof source.Path) message

                let compatibility = SchemaVersion.classifyRaw source.RawSchemaVersion

                let identity: SourceIdentity =
                    { Artifact = artifact
                      Digest = source.SourceDigest
                      SchemaVersion = compatibility.Version
                      SchemaStatus = compatibility.Status
                      RawSchemaVersion = source.RawSchemaVersion }

                identity)

        let manifest =
            GenerationManifest.createWorkModelManifest outputPath generator sources outputDigest

        { model with
            GeneratedViews =
                [ { manifest with
                      Currency = currency
                      Diagnostics = diagnostics } ] }

    let generateWorkModel request =
        let parsed = loadWorkItemFromSnapshots request.Snapshots request.WorkId

        let outputPath =
            request.ExpectedOutputPath
            |> Option.defaultValue (GenerationManifest.expectedWorkModelOutputPath request.WorkId)

        let model =
            parsed
            |> WorkModel.fromParsedWorkItem
            |> applyGeneratedView outputPath request.GeneratorVersion None CurrencyCurrent []

        let jsonWithoutDigest = serializeWorkModel model

        let manifestDigest =
            SchemaVersion.outputSha256Text (canonicalizeOutputDigestForHash jsonWithoutDigest)

        let modelWithDigest =
            applyGeneratedView outputPath request.GeneratorVersion (Some manifestDigest) CurrencyCurrent [] model

        let json = serializeWorkModel modelWithDigest
        let outputDigest = SchemaVersion.outputSha256Text json

        { WorkId = request.WorkId
          OutputPath = outputPath
          Model = modelWithDigest
          Json = json
          OutputDigest = outputDigest
          Diagnostics = modelWithDigest.Diagnostics }

    let generatedViewArtifact outputPath =
        match ArtifactRef.create outputPath ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
        | Ok value -> value
        | Error message -> invalidArg (nameof outputPath) message

    let generatorStale (expected: GeneratorVersion) (actual: GeneratorVersion option) =
        match actual with
        | Some generator -> generator.Id <> expected.Id || generator.Version <> expected.Version
        | None -> true

    let sourceStale (currentSources: SourceIdentity list) (generatedSources: SourceIdentity list) =
        let current =
            currentSources
            |> List.map (fun source ->
                source.Artifact.Path,
                (source.Digest.Value, source.SchemaVersion |> Option.map (fun version -> version.Major)))
            |> Map.ofList

        generatedSources
        |> List.exists (fun source ->
            match Map.tryFind source.Artifact.Path current with
            | Some(currentDigest, currentSchema) ->
                currentDigest <> source.Digest.Value
                || currentSchema
                   <> (source.SchemaVersion |> Option.map (fun version -> version.Major))
            | None -> true)

    let outputDigestStale (snapshot: FileSnapshot) (metadata: GeneratedWorkModelMetadata) =
        match metadata.OutputDigest with
        | Some digest ->
            let normalized = canonicalizeOutputDigestForHash snapshot.Text
            let actual = SchemaVersion.outputSha256Text normalized
            actual.Value <> digest.Value
        | None -> false

    let checkGeneratedWorkModelCurrency snapshots workId generatorVersion =
        let parsed = loadWorkItemFromSnapshots snapshots workId
        let outputPath = GenerationManifest.expectedWorkModelOutputPath workId
        let artifact = generatedViewArtifact outputPath

        let normalized =
            snapshots
            |> List.map (fun snapshot ->
                { snapshot with
                    Path = snapshot.Path.Trim().Replace('\\', '/').TrimStart('/') })

        match normalized |> List.tryFind (fun snapshot -> snapshot.Path = outputPath) with
        | None -> [ Diagnostics.missingGeneratedWorkModel artifact outputPath ]
        | Some snapshot ->
            match GenerationManifest.parseWorkModelMetadata snapshot.Path snapshot.Text with
            | Error diagnostics -> diagnostics |> Diagnostics.sort
            | Ok metadata ->
                let stale =
                    generatorStale generatorVersion metadata.Generator
                    || sourceStale parsed.Sources metadata.Sources
                    || outputDigestStale snapshot metadata

                if stale then
                    [ Diagnostics.staleGeneratedView
                          artifact
                          "Generated work-model metadata no longer matches current sources, generator version, schema versions, or output digest."
                          "Regenerate readiness/<id>/work-model.json from current lifecycle sources." ]
                else
                    []

    let diagnosticIds (model: WorkModel) =
        model.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)
