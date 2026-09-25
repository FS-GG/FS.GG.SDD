namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.Internal

/// Pure interpretation of a separately supplied, supposedly complete physical
/// `work/` inventory. Completeness and capture timing are not established here.
module internal WorkModelCandidateInventoryPreview =
    type Refusal =
        | InvalidWorkId
        | InvalidPath of string
        | DuplicatePath of string
        | RawDigestDrift of string
        | MissingSelectedSpec of string
        | MalformedCandidate of string
        | DuplicateWorkId of string list

    type Preview = { CandidatePaths: string list }

    exception private Refused of Refusal
    let private refuse reason = raise (Refused reason)

    let private validPath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (path.Contains '\\')
        && path.Split('/') |> Array.forall (fun part -> part <> "" && part <> "." && part <> "..")
        && path |> Seq.forall (fun c -> not (Char.IsControl c))

    let verify workId (captured: GenerationSourceSnapshot.CapturedFile list)
        : Result<Preview, Refusal> =
        try
            if String.IsNullOrWhiteSpace workId
               || not (Regex.IsMatch(workId, "^[a-z0-9][a-z0-9-]*$")) then refuse InvalidWorkId
            if obj.ReferenceEquals(captured, null) then
                refuse (MissingSelectedSpec $"work/{workId}/spec.md")
            let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            let candidates = ResizeArray<string * string>()
            for file in captured do
                if obj.ReferenceEquals(file, null) || not (validPath file.Path)
                   || not (file.Path.StartsWith("work/", StringComparison.Ordinal)) then
                    refuse (InvalidPath(if obj.ReferenceEquals(file, null) then "" else file.Path))
                if not (seen.Add file.Path) then refuse (DuplicatePath file.Path)
                let bytes = file.Bytes
                if file.Digest <> SchemaVersion.sha256Bytes bytes then refuse (RawDigestDrift file.Path)
                let parts = file.Path.Split('/')
                if parts.Length = 3
                   && (String.Equals(parts.[2], "spec.md", StringComparison.OrdinalIgnoreCase)
                       || String.Equals(parts.[2], "charter.md", StringComparison.OrdinalIgnoreCase)) then
                    if parts.[2] <> "spec.md" && parts.[2] <> "charter.md" then
                        refuse (InvalidPath file.Path)
                    let text =
                        match Fsgg.SkillMirror.decodeBody bytes with
                        | Ok body -> body
                        | Error _ -> refuse (MalformedCandidate file.Path)
                    let candidateWorkId =
                        if parts.[2] = "spec.md" then
                            let snapshot: FileSnapshot =
                                { Path = file.Path; Text = text; RawBytes = Some bytes }
                            match parseWorkItemMetadata snapshot with
                            | Ok metadata -> metadata.WorkId.Value
                            | Error _ -> refuse (MalformedCandidate file.Path)
                        else
                            match EarlyStageAuthoring.parseCharterFrontMatter file.Path text with
                            | Ok frontMatter -> frontMatter.WorkId
                            | Error _ -> refuse (MalformedCandidate file.Path)
                    candidates.Add(file.Path, candidateWorkId)
            let selectedSpec = $"work/{workId}/spec.md"
            if not (candidates |> Seq.exists (fun (path, _) -> path = selectedSpec)) then
                refuse (MissingSelectedSpec selectedSpec)
            let duplicates =
                candidates
                |> Seq.filter (fun (path, candidateId) ->
                    not (path.StartsWith($"work/{workId}/", StringComparison.OrdinalIgnoreCase))
                    && String.Equals(candidateId, workId, StringComparison.OrdinalIgnoreCase))
                |> Seq.map fst
                |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                |> Seq.toList
            if not (List.isEmpty duplicates) then refuse (DuplicateWorkId duplicates)
            Ok {
                CandidatePaths =
                    candidates
                    |> Seq.map fst
                    |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                    |> Seq.toList
            }
        with Refused reason -> Error reason
