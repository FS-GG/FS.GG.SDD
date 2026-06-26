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

module Serialization =
    let normalizeSnapshotsToWorkModel snapshots workId =
        loadWorkItemFromSnapshots snapshots workId
        |> WorkModel.fromParsedWorkItem

    let writeStringList (writer: Utf8JsonWriter) (name: string) (values: string list) =
        writer.WriteStartArray name
        values |> List.iter (fun value -> writer.WriteStringValue(value: string))
        writer.WriteEndArray()

    let writeDigest (writer: Utf8JsonWriter) (name: string) (digest: SourceDigest) =
        writer.WriteStartObject name
        writer.WriteString("algorithm", digest.Algorithm)
        writer.WriteString("value", digest.Value)
        writer.WriteEndObject()

    let writeOutputDigest (writer: Utf8JsonWriter) (name: string) (digest: OutputDigest) =
        writer.WriteStartObject name
        writer.WriteString("algorithm", digest.Algorithm)
        writer.WriteString("value", digest.Value)
        writer.WriteEndObject()

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
        writeDigest writer "sourceDigest" source.SourceDigest
        writer.WriteEndObject()

    let writeSourceLocation (writer: Utf8JsonWriter) (name: string) location =
        match location with
        | Some location ->
            writer.WriteStartObject(name)

            match location.Line with
            | Some line -> writer.WriteNumber("line", line)
            | None -> writer.WriteNull "line"

            match location.Column with
            | Some column -> writer.WriteNumber("column", column)
            | None -> writer.WriteNull "column"

            writer.WriteEndObject()
        | None -> writer.WriteNull name

    let writeRequirement (writer: Utf8JsonWriter) (requirement: RequirementEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", requirement.Id)
        writer.WriteString("title", requirement.Title)
        writer.WriteString("text", requirement.Text)
        writeStringList writer "acceptanceCriteria" requirement.AcceptanceCriteria
        match requirement.Priority with
        | Some priority -> writer.WriteString("priority", priority)
        | None -> writer.WriteNull "priority"
        writer.WriteString("source", requirement.Source)
        writeSourceLocation writer "sourceLocation" requirement.SourceLocation
        writeStringList writer "linkedTaskIds" requirement.LinkedTaskIds
        writeStringList writer "linkedEvidenceIds" requirement.LinkedEvidenceIds
        writer.WriteEndObject()

    let writeDecision (writer: Utf8JsonWriter) (decision: DecisionEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", decision.Id)
        writer.WriteString("title", decision.Title)
        writer.WriteString("decision", decision.Decision)
        writer.WriteString("source", decision.Source)
        writeSourceLocation writer "sourceLocation" decision.SourceLocation
        writeStringList writer "linkedTaskIds" decision.LinkedTaskIds
        writer.WriteEndObject()

    let writeTask (writer: Utf8JsonWriter) (task: TaskEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", task.Id)
        writer.WriteString("title", task.Title)
        writer.WriteString("status", task.Status)
        writer.WriteString("owner", task.Owner)
        writeStringList writer "dependencies" task.Dependencies
        writeStringList writer "requirements" task.Requirements
        writeStringList writer "decisions" task.Decisions
        writeStringList writer "sourceIds" task.SourceIds
        writeStringList writer "requiredSkills" task.RequiredSkills
        writeStringList writer "requiredEvidence" task.RequiredEvidence
        writer.WriteString("source", task.Source)
        writeSourceLocation writer "sourceLocation" task.SourceLocation
        writer.WriteEndObject()

    let writeEvidence (writer: Utf8JsonWriter) (evidence: EvidenceEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", evidence.Id)
        writer.WriteString("kind", evidence.Kind)
        writer.WriteString("subjectType", evidence.SubjectType)
        writer.WriteString("subjectId", evidence.SubjectId)
        writeStringList writer "taskRefs" evidence.TaskRefs
        writeStringList writer "requirementRefs" evidence.RequirementRefs
        writeStringList writer "artifactRefs" evidence.ArtifactRefs
        writer.WriteString("result", evidence.Result)
        writer.WriteBoolean("synthetic", evidence.Synthetic)
        match evidence.Rationale with
        | Some rationale -> writer.WriteString("rationale", rationale)
        | None -> writer.WriteNull "rationale"
        writer.WriteString("source", evidence.Source)
        writeSourceLocation writer "sourceLocation" evidence.SourceLocation
        writer.WriteEndObject()

    let writeManifestSource (writer: Utf8JsonWriter) (source: SourceIdentity) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Artifact.Path)
        writeDigest writer "digest" source.Digest
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

        match view.OutputDigest with
        | Some digest -> writeOutputDigest writer "outputDigest" digest
        | None -> writer.WriteNull "outputDigest"

        writer.WriteString("currency", GenerationManifest.currencyStatusValue view.Currency)
        writer.WriteEndObject()

    let writeLocation (writer: Utf8JsonWriter) location =
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
        writer.WriteString("severity", Diagnostics.severityValue diagnostic.Severity)

        match diagnostic.Artifact with
        | Some artifact -> writer.WriteString("artifact", artifact.Path)
        | None -> writer.WriteNull "artifact"

        writeLocation writer diagnostic.Location
        writer.WriteString("message", diagnostic.Message)
        writer.WriteString("correction", diagnostic.Correction)
        writeStringList writer "relatedIds" diagnostic.RelatedIds
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
        model.Diagnostics |> List.iter (writeDiagnostic writer)
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
            RegexOptions.CultureInvariant)

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
                    match ArtifactRef.create source.Path (ArtifactKind.Other "generatedSource") ArtifactOwner.Sdd true with
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
            GeneratedViews = [ { manifest with Currency = currency; Diagnostics = diagnostics } ] }

    let generateWorkModel request =
        let parsed = loadWorkItemFromSnapshots request.Snapshots request.WorkId
        let outputPath = request.ExpectedOutputPath |> Option.defaultValue (GenerationManifest.expectedWorkModelOutputPath request.WorkId)
        let model = parsed |> WorkModel.fromParsedWorkItem |> applyGeneratedView outputPath request.GeneratorVersion None CurrencyCurrent []
        let jsonWithoutDigest = serializeWorkModel model
        let manifestDigest = SchemaVersion.outputSha256Text (canonicalizeOutputDigestForHash jsonWithoutDigest)
        let modelWithDigest = applyGeneratedView outputPath request.GeneratorVersion (Some manifestDigest) CurrencyCurrent [] model
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
            |> List.map (fun source -> source.Artifact.Path, (source.Digest.Value, source.SchemaVersion |> Option.map (fun version -> version.Major)))
            |> Map.ofList

        generatedSources
        |> List.exists (fun source ->
            match Map.tryFind source.Artifact.Path current with
            | Some(currentDigest, currentSchema) ->
                currentDigest <> source.Digest.Value
                || currentSchema <> (source.SchemaVersion |> Option.map (fun version -> version.Major))
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
            |> List.map (fun snapshot -> { snapshot with Path = snapshot.Path.Trim().Replace('\\', '/').TrimStart('/') })

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
