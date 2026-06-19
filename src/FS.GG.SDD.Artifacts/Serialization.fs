namespace FS.GG.SDD.Artifacts

open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.LifecycleArtifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel

module Serialization =
    let normalizeSnapshotsToWorkModel snapshots workId =
        LifecycleArtifacts.loadWorkItemFromSnapshots snapshots workId
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
        writeDigest writer "sourceDigest" source.SourceDigest
        writer.WriteEndObject()

    let writeRequirement (writer: Utf8JsonWriter) (requirement: RequirementEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", requirement.Id)
        writer.WriteString("title", requirement.Title)
        writer.WriteString("text", requirement.Text)
        writer.WriteString("source", requirement.Source)
        writer.WriteEndObject()

    let writeDecision (writer: Utf8JsonWriter) (decision: DecisionEntry) =
        writer.WriteStartObject()
        writer.WriteString("id", decision.Id)
        writer.WriteString("title", decision.Title)
        writer.WriteString("decision", decision.Decision)
        writer.WriteString("source", decision.Source)
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
        writeStringList writer "requiredSkills" task.RequiredSkills
        writeStringList writer "requiredEvidence" task.RequiredEvidence
        writer.WriteString("source", task.Source)
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
        writer.WriteString("source", evidence.Source)
        writer.WriteEndObject()

    let writeManifestSource (writer: Utf8JsonWriter) (source: SourceIdentity) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Artifact.Path)
        writeDigest writer "digest" source.Digest
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

    let diagnosticIds model =
        model.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id)
