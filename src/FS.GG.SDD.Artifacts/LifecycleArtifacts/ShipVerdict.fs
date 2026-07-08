namespace FS.GG.SDD.Artifacts

open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module ShipVerdict =
    type ShipVerdict =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: string
          Stage: string
          Status: string
          Generator: string
          SourcesDigest: SourceDigest
          VerificationReadinessStatus: string
          DispositionState: string
          DispositionBlockingFindingIds: string list
          Readiness: string }

    let sourcesDigest (sources: AnalysisSourceRecord list) : SourceDigest =
        // The canonical pre-image. `sources[]` is already path-sorted on parse; sorting again
        // here keeps the digest a function of the *set*, not of the caller's discipline.
        // Pairing each path with its digest is load-bearing (ADR-0026 §1): hashing the digest
        // values alone would not bind which input produced which digest.
        sources
        |> List.sortBy (fun source -> source.Path)
        |> List.map (fun source ->
            match source.Digest with
            | Some digest -> $"{source.Path}|{digest.Algorithm}:{digest.Value}"
            | None -> $"{source.Path}|")
        |> String.concat "\n"
        |> sha256Text

    let fromShipView (view: ShipView) : ShipVerdict =
        { SchemaVersion = view.SchemaVersion
          ViewVersion = view.ViewVersion
          WorkId = workIdValue view.WorkId
          Stage = stageValue view.Stage
          Status = view.Status
          Generator = view.Generator
          SourcesDigest = sourcesDigest view.Sources
          VerificationReadinessStatus = view.VerificationReadiness.Status
          DispositionState = view.Disposition
          DispositionBlockingFindingIds = view.DispositionBlockingFindingIds
          Readiness = view.Readiness }

    let toJson (verdict: ShipVerdict) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        // `ship.json`'s own top-level order, with `sources` replaced in place by
        // `sourcesDigest`, so the verdict reads as an order-preserving projection.
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", verdict.SchemaVersion.Major)
        writer.WriteString("viewVersion", verdict.ViewVersion)
        writer.WriteString("workId", verdict.WorkId)
        writer.WriteString("stage", verdict.Stage)
        writer.WriteString("status", verdict.Status)
        writer.WriteString("generator", verdict.Generator)

        writer.WriteStartObject("sourcesDigest")
        writer.WriteString("algorithm", verdict.SourcesDigest.Algorithm)
        writer.WriteString("value", verdict.SourcesDigest.Value)
        writer.WriteEndObject()

        writer.WriteStartObject("verificationReadiness")
        writer.WriteString("status", verdict.VerificationReadinessStatus)
        writer.WriteEndObject()

        writer.WriteStartObject("disposition")
        writer.WriteString("state", verdict.DispositionState)
        writer.WriteStartArray("blockingFindingIds")
        verdict.DispositionBlockingFindingIds |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteEndObject()

        writer.WriteString("readiness", verdict.Readiness)
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
