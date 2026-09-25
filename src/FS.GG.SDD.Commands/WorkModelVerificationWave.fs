namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// A pure two-step preview for a future verification wave. It binds proposed output bytes to
/// the selected v2 candidate before comparing supplied captures. It never returns write effects.
module internal WorkModelVerificationWave =
    type Refusal =
        | InvalidWorkId
        | WrongOutputPath
        | MalformedOutput
        | WrongOutputWorkId
        | WrongMetadataPath
        | SourceMismatch
        | Bundle of WorkModelSourceBundle.Refusal

    type Prepared = private {
        WorkId: string
        OutputPath: string
        OutputDigest: OutputDigest
        Selected: FileSnapshot list
        Candidate: WorkModelSourceBundle.Candidate
    }

    type VerifiedPreview = {
        WorkId: string
        OutputPath: string
        OutputDigest: OutputDigest
        SourcePaths: string list
    }

    exception private Refused of Refusal
    let private refuse reason = raise (Refused reason)

    let private property (name: string) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then None
        else
            let mutable value = Unchecked.defaultof<JsonElement>
            if element.TryGetProperty(name, &value) then Some value else None

    let private stringProperty name element =
        property name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.String then value.GetString() |> Option.ofObj
            else None)

    let private digest element =
        match stringProperty "algorithm" element, stringProperty "value" element with
        | Some algorithm, Some value -> SchemaVersion.createSourceDigest algorithm value |> Result.toOption
        | _ -> None

    let private sourceRows digestProperty (container: JsonElement) =
        let sources =
            match property "sources" container with
            | Some value when value.ValueKind = JsonValueKind.Array -> value
            | _ -> refuse MalformedOutput
        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        sources.EnumerateArray()
        |> Seq.map (fun source ->
            match stringProperty "path" source, property digestProperty source |> Option.bind digest with
            | Some path, Some sourceDigest when seen.Add path -> path, sourceDigest
            | _ -> refuse MalformedOutput)
        |> Seq.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
        |> Seq.toList

    /// Bind both the model's root source rows and its generated-view metadata to the same
    /// candidate. A stale or foreign JSON body cannot borrow a valid separate candidate.
    let prepare workId outputPath outputJson (selected: FileSnapshot list)
                (candidate: WorkModelSourceBundle.Candidate) : Result<Prepared, Refusal> =
        try
            if String.IsNullOrWhiteSpace workId
               || not (Regex.IsMatch(workId, "^[a-z0-9][a-z0-9-]*$")) then refuse InvalidWorkId
            let expectedPath = $"readiness/{workId}/work-model.json"
            if outputPath <> expectedPath then refuse WrongOutputPath
            if String.IsNullOrWhiteSpace outputJson || obj.ReferenceEquals(candidate, null)
               || obj.ReferenceEquals(candidate.Sources, null) then refuse MalformedOutput
            use document = JsonDocument.Parse outputJson
            let root = document.RootElement
            if stringProperty "workId" root <> Some workId then refuse WrongOutputWorkId
            let modelSources = sourceRows "sourceDigest" root
            let views =
                match property "generatedViews" root with
                | Some value when value.ValueKind = JsonValueKind.Array ->
                    value.EnumerateArray()
                    |> Seq.filter (fun view -> stringProperty "kind" view = Some "workModel")
                    |> Seq.toList
                | _ -> refuse MalformedOutput
            let view =
                match views with
                | [ only ] -> only
                | _ -> refuse MalformedOutput
            if stringProperty "path" view <> Some expectedPath then refuse WrongMetadataPath
            let metadataSources = sourceRows "digest" view
            let candidateSources =
                candidate.Sources
                |> List.map (fun source ->
                    if obj.ReferenceEquals(source, null) then refuse SourceMismatch
                    source.Path, source.Digest)
                |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
            if modelSources <> metadataSources || modelSources <> candidateSources then refuse SourceMismatch
            Ok {
                WorkId = workId
                OutputPath = expectedPath
                OutputDigest = SchemaVersion.outputSha256Text outputJson
                Selected = selected
                Candidate = candidate
            }
        with
        | Refused reason -> Error reason
        | :? JsonException -> Error MalformedOutput

    /// A supplied capture must still pass the v2 bundle verifier. The returned preview has no
    /// output bytes or CommandEffect; physical capture and staging remain separate decisions.
    let verifyCaptured (prepared: Prepared) (captured: GenerationSourceSnapshot.CapturedFile list)
        : Result<VerifiedPreview, Refusal> =
        match WorkModelSourceBundle.verify prepared.WorkId prepared.Selected captured prepared.Candidate with
        | Error reason -> Error(Bundle reason)
        | Ok files ->
            Ok {
                WorkId = prepared.WorkId
                OutputPath = prepared.OutputPath
                OutputDigest = prepared.OutputDigest
                SourcePaths = files |> List.map _.Path
            }
