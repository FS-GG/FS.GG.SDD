namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// Read-only repeated capture over the #1017 physical source profile. It detects observed
/// changes between passes, without claiming that either pass was a simultaneous snapshot.
module internal WorkModelTemporalCapturePreview =
    module Bundle = WorkModelSourceBundle

    type Refusal =
        | FirstPass of Bundle.Refusal
        | SecondPass of Bundle.Refusal
        | RawChanged of string

    type Preview = {
        WorkId: string
        Sources: (string * SourceDigest) list
    }

    /// `capture` is a deterministic test seam; the production wrapper below supplies only
    /// the pinned, no-follow #1017 reader. Neither path returns bytes or write effects.
    let verifyWithCapture (capture: unit -> Result<GenerationSourceSnapshot.CapturedFile list, Bundle.Refusal>)
                          workId (selected: FileSnapshot list) (candidate: Bundle.Candidate)
        : Result<Preview, Refusal> =
        match capture () with
        | Error reason -> Error(FirstPass reason)
        | Ok first ->
            match Bundle.verify workId selected first candidate with
            | Error reason -> Error(FirstPass reason)
            | Ok firstVerified ->
                match capture () with
                | Error reason -> Error(SecondPass reason)
                | Ok second ->
                    match Bundle.verify workId selected second candidate with
                    | Error reason -> Error(SecondPass reason)
                    | Ok secondVerified ->
                        let firstByPath = firstVerified |> List.map (fun file -> file.Path, file.Bytes) |> Map.ofList
                        let changed =
                            secondVerified
                            |> List.tryPick (fun file ->
                                if firstByPath.[file.Path] <> file.Bytes then Some file.Path else None)
                        match changed with
                        | Some path -> Error(RawChanged path)
                        | None ->
                            Ok {
                                WorkId = workId
                                Sources = secondVerified |> List.map (fun file -> file.Path, file.Digest)
                            }

    let verifyPhysicalStable workspaceRoot workId selected candidate =
        verifyWithCapture
            (fun () -> Bundle.verifyFromPinnedCoreSources workspaceRoot workId selected candidate)
            workId selected candidate
