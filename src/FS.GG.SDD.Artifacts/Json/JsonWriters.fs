namespace FS.GG.SDD.Artifacts.Json

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion

module JsonWriters =

    type StringListOrder =
        | SourceOrder
        | Sorted

    let writeStringList (writer: Utf8JsonWriter) (order: StringListOrder) (name: string) (values: string list) =
        writer.WriteStartArray name

        let ordered =
            match order with
            | Sorted -> values |> List.sort
            | SourceOrder -> values

        ordered |> List.iter (fun value -> writer.WriteStringValue(value: string))
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

    let writeLocation (writer: Utf8JsonWriter) (name: string) (location: SourceLocation option) =
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

    let writeDiagnostic (writer: Utf8JsonWriter) (relatedIdsOrder: StringListOrder) (diagnostic: Diagnostic) =
        writer.WriteStartObject()
        writer.WriteString("id", diagnostic.Id)
        writer.WriteString("severity", severityValue diagnostic.Severity)

        match diagnostic.Artifact with
        | Some artifact -> writer.WriteString("artifact", artifact.Path)
        | None -> writer.WriteNull "artifact"

        writeLocation writer "location" diagnostic.Location
        writer.WriteString("message", diagnostic.Message)
        writer.WriteString("correction", diagnostic.Correction)
        writeStringList writer relatedIdsOrder "relatedIds" diagnostic.RelatedIds
        writer.WriteEndObject()
