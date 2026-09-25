namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.Text.Json
open FS.GG.SDD.Artifacts

/// Pure exact-byte check over a proposed generated work model. It retains #1019's typed
/// source/candidate check and independently re-renders the body from selected snapshots.
module internal WorkModelExactOutputPreview =
    module Wave = WorkModelVerificationWave
    module SerializationModule = FS.GG.SDD.Artifacts.Serialization

    type Refusal =
        | Proposed of WorkModelVerificationWave.Refusal
        | InvalidSelection
        | AmbiguousJson
        | RegenerationFailed
        | OutputDrift

    type Prepared = private { Wave: WorkModelVerificationWave.Prepared }

    let private inspectPropertyNames (json: string) =
        if String.IsNullOrWhiteSpace json then Error(Proposed Wave.MalformedOutput)
        else
            try
                use document = JsonDocument.Parse json
                let rec unique (element: JsonElement) =
                    match element.ValueKind with
                    | JsonValueKind.Object ->
                        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        element.EnumerateObject()
                        |> Seq.forall (fun property -> seen.Add property.Name && unique property.Value)
                    | JsonValueKind.Array -> element.EnumerateArray() |> Seq.forall unique
                    | _ -> true
                if unique document.RootElement then Ok () else Error AmbiguousJson
            with :? JsonException -> Error(Proposed Wave.MalformedOutput)

    let prepare workId outputPath outputJson (selected: FileSnapshot list)
                (candidate: WorkModelSourceBundle.Candidate) generatorVersion
        : Result<Prepared, Refusal> =
        match inspectPropertyNames outputJson with
        | Error reason -> Error reason
        | Ok () ->
            match Wave.prepare workId outputPath outputJson selected candidate with
            | Error reason -> Error(Proposed reason)
            | Ok _ when obj.ReferenceEquals(selected, null) -> Error InvalidSelection
            | Ok wave ->
                try
                    let expected =
                        SerializationModule.generateWorkModel
                            { WorkId = workId
                              Snapshots = selected
                              GeneratorVersion = generatorVersion
                              ExpectedOutputPath = Some outputPath }
                    if expected.Json <> outputJson then Error OutputDrift
                    else Ok { Wave = wave }
                with _ -> Error RegenerationFailed

    let verifyCaptured (prepared: Prepared) (captured: GenerationSourceSnapshot.CapturedFile list)
        : Result<WorkModelVerificationWave.VerifiedPreview, Refusal> =
        Wave.verifyCaptured prepared.Wave captured
        |> Result.mapError Proposed
