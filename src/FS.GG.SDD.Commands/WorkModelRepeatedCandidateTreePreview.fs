namespace FS.GG.SDD.Commands

open System

/// Read-only observed-stability check over two pinned complete-tree captures.
/// A declared set is still supplied by the caller, and two passes are not atomic.
module internal WorkModelRepeatedCandidateTreePreview =
    module Physical = GenerationSourceSnapshot
    module Inventory = WorkModelCandidateInventoryPreview

    type Refusal =
        | FirstCapture of Physical.Refusal
        | FirstInventory of Inventory.Refusal
        | SecondCapture of Physical.Refusal
        | SecondInventory of Inventory.Refusal
        | RosterChanged
        | RawChanged of string

    /// Injected capture is a deterministic read-only test seam. Production uses the Linux
    /// descriptor-pinned closed-root reader for both full `work/` passes.
    let verifyWithCapture
        (capture: unit -> Result<Physical.CapturedFile list, Physical.Refusal>) workId
        : Result<Inventory.Preview, Refusal> =
        match capture () with
        | Error reason -> Error(FirstCapture reason)
        | Ok first ->
            match Inventory.verify workId first with
            | Error reason -> Error(FirstInventory reason)
            | Ok _ ->
                match capture () with
                | Error reason -> Error(SecondCapture reason)
                | Ok second ->
                    match Inventory.verify workId second with
                    | Error reason -> Error(SecondInventory reason)
                    | Ok preview ->
                        let sortPaths (files: Physical.CapturedFile list) =
                            files
                            |> List.map _.Path
                            |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                        if sortPaths first <> sortPaths second then Error RosterChanged
                        else
                            let firstByPath =
                                first |> List.map (fun file -> file.Path, file.Bytes) |> Map.ofList
                            match second |> List.tryPick (fun file ->
                                if firstByPath.[file.Path] <> file.Bytes then Some file.Path else None) with
                            | Some path -> Error(RawChanged path)
                            | None -> Ok preview

    let verifyPhysical workspaceRoot workId declared =
        verifyWithCapture
            (fun () -> Physical.capturePinnedWithHooks ignore ignore
                           workspaceRoot "work" declared Physical.ExactBytes)
            workId
