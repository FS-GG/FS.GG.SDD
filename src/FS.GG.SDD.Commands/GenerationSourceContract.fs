namespace FS.GG.SDD.Commands

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// A provisional binding between one producer-selected closed root and its physical snapshot.
/// The selection must come from the producer, not from the candidate manifest being checked.
module internal GenerationSourceContract =
    type Selection = {
        ClosedRoot: string
        Policy: GenerationSourceSnapshot.DigestPolicy
    }

    type Source = {
        Path: string
        Digest: SourceDigest
    }

    type Contract = {
        Version: int
        ClosedRoot: string
        Policy: GenerationSourceSnapshot.DigestPolicy
        Sources: Source list
    }

    type Refusal =
        | UnsupportedVersion of int
        | WrongRoot
        | WrongPolicy
        | MalformedDigest of string
        | Physical of GenerationSourceSnapshot.Refusal
        | DigestDrift of string

    let private validDigest (digest: SourceDigest) =
        if obj.ReferenceEquals(digest, null) then false
        else
            match SchemaVersion.createSourceDigest digest.Algorithm digest.Value with
            | Ok canonical -> canonical = digest
            | Error _ -> false

    /// Compare a candidate v1 contract with a producer-owned selection and fresh read.
    /// This is read-only and does not establish physical race freedom or own output rollback.
    let verify (workspaceRoot: string) (selected: Selection) (candidate: Contract)
        : Result<GenerationSourceSnapshot.CapturedFile list, Refusal> =
        if candidate.Version <> 1 then Error(UnsupportedVersion candidate.Version)
        elif not (String.Equals(selected.ClosedRoot, candidate.ClosedRoot, StringComparison.Ordinal)) then
            Error WrongRoot
        elif selected.Policy <> candidate.Policy then Error WrongPolicy
        elif obj.ReferenceEquals(candidate.Sources, null) then
            Error(Physical GenerationSourceSnapshot.EmptySet)
        else
            match candidate.Sources |> List.tryFind (fun source ->
                obj.ReferenceEquals(source, null) || not (validDigest source.Digest)) with
            | Some source -> Error(MalformedDigest(if obj.ReferenceEquals(source, null) then "" else source.Path))
            | None ->
                let declared = candidate.Sources |> List.map _.Path
                match GenerationSourceSnapshot.capture workspaceRoot selected.ClosedRoot declared selected.Policy with
                | Error reason -> Error(Physical reason)
                | Ok captured ->
                    let actual = captured |> List.map (fun source -> source.Path, source.Digest) |> Map.ofList
                    match candidate.Sources |> List.tryFind (fun source -> actual.[source.Path] <> source.Digest) with
                    | Some source -> Error(DigestDrift source.Path)
                    | None -> Ok captured
