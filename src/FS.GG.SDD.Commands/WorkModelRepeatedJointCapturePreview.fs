namespace FS.GG.SDD.Commands

open System
open FS.GG.SDD.Artifacts

/// Two read-only bundle/tree observations with exact raw comparison across passes.
/// Matching observations still do not establish an atomic cross-root instant.
module internal WorkModelRepeatedJointCapturePreview =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Joint = WorkModelCandidateBundleJoinPreview

    type Refusal =
        | FirstBundleCapture of Bundle.Refusal
        | FirstTreeCapture of Physical.Refusal
        | FirstJoin of Joint.Refusal
        | SecondBundleCapture of Bundle.Refusal
        | SecondTreeCapture of Physical.Refusal
        | SecondJoin of Joint.Refusal
        | BundleRosterChanged
        | TreeRosterChanged
        | BundleRawChanged of string
        | TreeRawChanged of string

    let private sortedPaths (files: Physical.CapturedFile list) =
        files |> List.map _.Path
        |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

    let private rawChange first second =
        let firstByPath = first |> List.map (fun (file: Physical.CapturedFile) -> file.Path, file.Bytes) |> Map.ofList
        second |> List.tryPick (fun (file: Physical.CapturedFile) ->
            if firstByPath.[file.Path] <> file.Bytes then Some file.Path else None)

    /// Callbacks permit deterministic interleavings. Each captured pair is checked by
    /// the #1043 join before any cross-pass comparison is made.
    let verifyWithCapture
        (captureBundle: unit -> Result<Physical.CapturedFile list, Bundle.Refusal>)
        (captureTree: unit -> Result<Physical.CapturedFile list, Physical.Refusal>)
        workId (selected: FileSnapshot list) (candidate: Bundle.Candidate)
        : Result<Joint.Preview, Refusal> =
        match captureBundle () with
        | Error reason -> Error(FirstBundleCapture reason)
        | Ok firstBundle ->
            match captureTree () with
            | Error reason -> Error(FirstTreeCapture reason)
            | Ok firstTree ->
                match Joint.verifyWithCapture (fun () -> Ok firstBundle) (fun () -> Ok firstTree)
                                             workId selected candidate with
                | Error reason -> Error(FirstJoin reason)
                | Ok _ ->
                    match captureBundle () with
                    | Error reason -> Error(SecondBundleCapture reason)
                    | Ok secondBundle ->
                        match captureTree () with
                        | Error reason -> Error(SecondTreeCapture reason)
                        | Ok secondTree ->
                            match Joint.verifyWithCapture (fun () -> Ok secondBundle) (fun () -> Ok secondTree)
                                                         workId selected candidate with
                            | Error reason -> Error(SecondJoin reason)
                            | Ok preview ->
                                if sortedPaths firstBundle <> sortedPaths secondBundle then
                                    Error BundleRosterChanged
                                elif sortedPaths firstTree <> sortedPaths secondTree then
                                    Error TreeRosterChanged
                                else
                                    match rawChange firstBundle secondBundle with
                                    | Some path -> Error(BundleRawChanged path)
                                    | None ->
                                        match rawChange firstTree secondTree with
                                        | Some path -> Error(TreeRawChanged path)
                                        | None -> Ok preview

    let verifyPhysical workspaceRoot workId selected candidate =
        verifyWithCapture
            (fun () -> Bundle.verifyFromPinnedCoreSources workspaceRoot workId selected candidate)
            (fun () -> Physical.capturePinnedDiscovered workspaceRoot "work" Physical.ExactBytes)
            workId selected candidate
