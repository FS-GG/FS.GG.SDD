namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.Internal

/// Pure join for a producer-selected work-model source set and separately captured bytes.
/// The caller still owns physical closure of every root and a coherent capture boundary.
module internal WorkModelSourceBundle =
    type Source = { Path: string; Digest: SourceDigest }
    type Candidate = { Version: int; WorkId: string; Sources: Source list }

    type Refusal =
        | InvalidWorkId
        | UnsupportedVersion of int
        | WrongWorkId
        | InvalidSelectionPath of string
        | MissingRequired of string
        | DuplicateSelection of string
        | DuplicatePhysical of string
        | DuplicateCandidate of string
        | InvalidCandidatePath of string
        | InvalidPerformancePath of string
        | MalformedEvidence
        | MissingPerformanceSelection of string
        | MissingPhysical of string
        | UnexpectedPhysical of string
        | MissingCandidate of string
        | UnexpectedCandidate of string
        | RawDrift of string
        | TextDrift of string
        | MalformedDigest of string
        | DigestDrift of string

    exception private Refused of Refusal
    let private refuse reason = raise (Refused reason)

    let private validRelative (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (path.Contains '\\')
        && path.Split('/') |> Array.forall (fun part -> part <> "" && part <> "." && part <> "..")
        && path |> Seq.forall (fun c -> not (Char.IsControl c))

    let private allowedPath workId performancePaths path =
        let config = [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml" ]
        let work =
            [ "spec.md"; "clarifications.md"; "checklist.md"; "plan.md"; "tasks.yml"; "evidence.yml" ]
            |> List.map (fun name -> $"work/{workId}/{name}")
        validRelative path
        && (List.contains path (config @ work)
            || Set.contains path performancePaths)

    let private distinct reason paths =
        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        for path in paths do
            if not (seen.Add path) then refuse (reason path)

    let private validDigest (digest: SourceDigest) =
        if obj.ReferenceEquals(digest, null) then false
        else
            match SchemaVersion.createSourceDigest digest.Algorithm digest.Value with
            | Ok canonical -> canonical = digest
            | Error _ -> false

    /// The selected snapshots must be the producer's `workModelSnapshots` result, not rows
    /// reconstructed from the candidate. Captures must already cover each physical root.
    let verify (workId: string) (selected: FileSnapshot list)
               (captured: GenerationSourceSnapshot.CapturedFile list) (candidate: Candidate)
        : Result<GenerationSourceSnapshot.CapturedFile list, Refusal> =
        try
            if String.IsNullOrWhiteSpace workId
               || not (Regex.IsMatch(workId, "^[a-z0-9][a-z0-9-]*$")) then refuse InvalidWorkId
            if obj.ReferenceEquals(candidate, null) then refuse (UnsupportedVersion 0)
            if candidate.Version <> 2 then refuse (UnsupportedVersion candidate.Version)
            if candidate.WorkId <> workId then refuse WrongWorkId
            if obj.ReferenceEquals(selected, null) || obj.ReferenceEquals(captured, null)
               || obj.ReferenceEquals(candidate.Sources, null) then refuse (MissingRequired ".fsgg/project.yml")
            if captured |> List.exists (fun file -> obj.ReferenceEquals(file, null) || not (validRelative file.Path)) then
                refuse (MissingPhysical "")
            distinct DuplicatePhysical (captured |> List.map _.Path)
            let physicalByPath = captured |> List.map (fun file -> file.Path, file) |> Map.ofList
            let evidencePath = $"work/{workId}/evidence.yml"
            let performancePaths =
                match physicalByPath.TryFind evidencePath with
                | None -> Set.empty
                | Some file ->
                    let bytes = file.Bytes
                    let text =
                        match Fsgg.SkillMirror.decodeBody bytes with
                        | Ok body -> body
                        | Error _ -> refuse MalformedEvidence
                    let snapshot = { Path = evidencePath; Text = text; RawBytes = Some bytes }
                    match Evidence.parseEvidenceArtifact snapshot with
                    | Error _ -> refuse MalformedEvidence
                    | Ok artifact when artifact.WorkId.Value <> workId || not (List.isEmpty artifact.Diagnostics) ->
                        refuse MalformedEvidence
                    | Ok artifact ->
                        artifact.Evidence
                        |> List.choose _.PerformanceBudget
                        |> List.map _.ArtifactPath
                        |> List.map (fun path ->
                            if not (validRelative path) then refuse (InvalidPerformancePath path)
                            path)
                        |> Set.ofList
            for source in selected do
                if obj.ReferenceEquals(source, null) || not (allowedPath workId performancePaths source.Path) then
                    refuse (InvalidSelectionPath(if obj.ReferenceEquals(source, null) then "" else source.Path))
            distinct DuplicateSelection (selected |> List.map _.Path)
            let selectedByPath = selected |> List.map (fun source -> source.Path, source) |> Map.ofList
            for required in [ ".fsgg/project.yml"; ".fsgg/sdd.yml"; ".fsgg/agents.yml"; $"work/{workId}/spec.md" ] do
                if not (selectedByPath.ContainsKey required) then refuse (MissingRequired required)
            if not (Set.isEmpty performancePaths) && not (selectedByPath.ContainsKey evidencePath) then
                refuse (MissingRequired evidencePath)
            for path in performancePaths do
                if not (selectedByPath.ContainsKey path) then refuse (MissingPerformanceSelection path)

            for source in selected do
                if not (physicalByPath.ContainsKey source.Path) then refuse (MissingPhysical source.Path)
            for file in captured do
                if not (selectedByPath.ContainsKey file.Path) then refuse (UnexpectedPhysical file.Path)

            if candidate.Sources |> List.exists (fun source -> obj.ReferenceEquals(source, null)) then
                refuse (MalformedDigest "")
            for source in candidate.Sources do
                if not (validRelative source.Path) then refuse (InvalidCandidatePath source.Path)
            distinct DuplicateCandidate (candidate.Sources |> List.map _.Path)
            let candidateByPath = candidate.Sources |> List.map (fun source -> source.Path, source) |> Map.ofList
            for source in selected do
                if not (candidateByPath.ContainsKey source.Path) then refuse (MissingCandidate source.Path)
            for source in candidate.Sources do
                if not (selectedByPath.ContainsKey source.Path) then refuse (UnexpectedCandidate source.Path)

            for source in selected do
                let path = source.Path
                let file = physicalByPath.[path]
                let bytes = file.Bytes
                if file.Digest <> SchemaVersion.sha256Bytes bytes
                   || (source.RawBytes |> Option.exists (fun raw -> raw <> bytes)) then
                    refuse (RawDrift path)
                let text =
                    match Fsgg.SkillMirror.decodeBody bytes with
                    | Ok body -> body
                    | Error _ -> refuse (TextDrift path)
                let projected =
                    if path = $"work/{workId}/evidence.yml" then
                        ViewGeneration.evidenceTextForWorkModel text
                    else text
                if source.Text <> projected then refuse (TextDrift path)
                let recorded = candidateByPath.[path].Digest
                if not (validDigest recorded) then refuse (MalformedDigest path)
                if recorded <> SchemaVersion.sha256Text projected then refuse (DigestDrift path)
            captured |> List.sortBy _.Path |> Ok
        with Refused reason -> Error reason
