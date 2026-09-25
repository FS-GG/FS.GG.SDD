namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts

/// Read-only join of exact proposed output and two independently captured source sets.
/// This observes changes between captures; it does not establish a simultaneous instant.
module internal WorkModelJointPreview =
    module Bundle = WorkModelSourceBundle
    module Exact = WorkModelExactOutputPreview
    module Wave = WorkModelVerificationWave

    type Refusal =
        | Proposed of Exact.Refusal
        | FirstCapture of Bundle.Refusal
        | FirstVerification of Exact.Refusal
        | SecondCapture of Bundle.Refusal
        | SecondVerification of Exact.Refusal
        | RawChanged of string

    /// The callback exists only for deterministic read-only controls. Production callers
    /// use verifyPhysical, whose capture callback is the pinned #1017 reader.
    let verifyWithCapture
        (capture: unit -> Result<GenerationSourceSnapshot.CapturedFile list, Bundle.Refusal>)
        workId outputPath outputJson (selected: FileSnapshot list)
        (candidate: Bundle.Candidate) generatorVersion
        : Result<Wave.VerifiedPreview, Refusal> =
        match Exact.prepare workId outputPath outputJson selected candidate generatorVersion with
        | Error reason -> Error(Proposed reason)
        | Ok prepared ->
            match capture () with
            | Error reason -> Error(FirstCapture reason)
            | Ok first ->
                match Exact.verifyCaptured prepared first with
                | Error reason -> Error(FirstVerification reason)
                | Ok _ ->
                    match capture () with
                    | Error reason -> Error(SecondCapture reason)
                    | Ok second ->
                        match Exact.verifyCaptured prepared second with
                        | Error reason -> Error(SecondVerification reason)
                        | Ok verified ->
                            let firstByPath =
                                first |> List.map (fun file -> file.Path, file.Bytes) |> Map.ofList
                            let changed =
                                second
                                |> List.tryPick (fun file ->
                                    if firstByPath.[file.Path] <> file.Bytes then Some file.Path else None)
                            match changed with
                            | Some path -> Error(RawChanged path)
                            | None -> Ok verified

    let verifyPhysical workspaceRoot workId outputPath outputJson selected candidate generatorVersion =
        verifyWithCapture
            (fun () -> Bundle.verifyFromPinnedCoreSources workspaceRoot workId selected candidate)
            workId outputPath outputJson selected candidate generatorVersion
