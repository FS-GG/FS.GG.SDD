namespace FS.GG.SDD.Commands

open System
open FS.GG.SDD.Artifacts

/// Read-only exact-byte overlap check between a verified work-model bundle and
/// an independently discovered complete `work/` tree. Capture order is temporal,
/// so agreement is an observation rather than proof of a common source instant.
module internal WorkModelCandidateBundleJoinPreview =
    module Bundle = WorkModelSourceBundle
    module Physical = GenerationSourceSnapshot
    module Inventory = WorkModelCandidateInventoryPreview

    type Refusal =
        | BundleCapture of Bundle.Refusal
        | BundleVerification of Bundle.Refusal
        | TreeCapture of Physical.Refusal
        | TreeInventory of Inventory.Refusal
        | MissingInTree of string
        | RawMismatch of string

    type Preview = { BundlePaths: string list; CandidatePaths: string list }

    /// The callbacks are deterministic read-only test seams. The physical wrapper
    /// uses pinned no-follow captures; this function also re-verifies the bundle.
    let verifyWithCapture
        (captureBundle: unit -> Result<Physical.CapturedFile list, Bundle.Refusal>)
        (captureTree: unit -> Result<Physical.CapturedFile list, Physical.Refusal>)
        workId (selected: FileSnapshot list) (candidate: Bundle.Candidate)
        : Result<Preview, Refusal> =
        match captureBundle () with
        | Error reason -> Error(BundleCapture reason)
        | Ok bundle ->
            match Bundle.verify workId selected bundle candidate with
            | Error reason -> Error(BundleVerification reason)
            | Ok verified ->
                match captureTree () with
                | Error reason -> Error(TreeCapture reason)
                | Ok tree ->
                    match Inventory.verify workId tree with
                    | Error reason -> Error(TreeInventory reason)
                    | Ok inventory ->
                        let byPath = tree |> List.map (fun file -> file.Path, file) |> Map.ofList
                        let mismatch =
                            verified
                            |> List.tryPick (fun file ->
                                if not (file.Path.StartsWith("work/", StringComparison.Ordinal)) then None
                                else
                                    match byPath.TryFind file.Path with
                                    | None -> Some(MissingInTree file.Path)
                                    | Some discovered when file.Bytes <> discovered.Bytes ->
                                        Some(RawMismatch file.Path)
                                    | Some _ -> None)
                        match mismatch with
                        | Some reason -> Error reason
                        | None ->
                            Ok {
                                BundlePaths = verified |> List.map _.Path
                                CandidatePaths = inventory.CandidatePaths
                            }

    let verifyPhysical workspaceRoot workId selected candidate =
        verifyWithCapture
            (fun () -> Bundle.verifyFromPinnedCoreSources workspaceRoot workId selected candidate)
            (fun () -> Physical.capturePinnedDiscovered workspaceRoot "work" Physical.ExactBytes)
            workId selected candidate
