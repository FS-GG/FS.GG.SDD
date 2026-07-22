namespace FS.GG.SDD.Validation

open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Diagnostics

module BugGuardCheck =

    type MarkerKind =
        | PinsBug
        | Guards

    type BugGuardMarker =
        { Kind: MarkerKind
          Issue: int
          Path: string
          Line: int }

    type IssueState =
        | Open
        | Closed
        | Unknown

    let markerKindValue (kind: MarkerKind) : string =
        match kind with
        | PinsBug -> "pins-bug"
        | Guards -> "guards"

    // The structured marker: the token `pins-bug` or `guards`, optional whitespace, `#`,
    // optional whitespace, then the issue number. Case-insensitive. The mandatory `#<digits>`
    // is what disambiguates a deliberate marker from the ordinary word "guards" in prose.
    let private markerPattern =
        Regex(@"(?i)\b(pins-bug|guards)\s*#\s*(\d+)\b", RegexOptions.Compiled)

    let private kindOfToken (token: string) =
        match token.ToLowerInvariant() with
        | "pins-bug" -> PinsBug
        | _ -> Guards

    let scanText (path: string) (text: string) : BugGuardMarker list =
        // Scan line-by-line so each marker carries a correct 1-based line number and a
        // single line may carry several markers (matches in left-to-right order).
        (text.Replace("\r\n", "\n").Replace('\r', '\n')).Split('\n')
        |> Array.mapi (fun idx line ->
            markerPattern.Matches(line)
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                { Kind = kindOfToken m.Groups.[1].Value
                  Issue = int m.Groups.[2].Value
                  Path = path
                  Line = idx + 1 })
            |> Seq.toList)
        |> Array.toList
        |> List.concat

    // Attach the source path to the diagnostic when it is a repo-relative path the
    // ArtifactRef grammar accepts; fall back to a path-less diagnostic (path stays in the
    // message) rather than letting a stray path shape make the pure rule throw.
    let private artifactFor (path: string) =
        match ArtifactRef.create path (ArtifactRef.Other "test-source") ArtifactRef.GeneratedProduct false with
        | Ok ref -> Some ref
        | Error _ -> None

    let private diagnosticFor (state: IssueState) (marker: BugGuardMarker) : Diagnostic option =
        let where = $"{marker.Path}:{marker.Line}"
        let token = markerKindValue marker.Kind

        let location =
            Some
                { Line = Some marker.Line
                  Column = None }

        let artifact = artifactFor marker.Path

        match state with
        | Closed -> None
        | Open ->
            let message, correction =
                match marker.Kind with
                | PinsBug ->
                    $"Test at {where} pins the reported-wrong behavior of issue #{marker.Issue}, which is still OPEN — a green test is locking in an unfixed bug (the TD1#14 hazard).",
                    $"Fix issue #{marker.Issue} and update this test to assert the corrected behavior, or remove the `pins-bug #{marker.Issue}` marker if it no longer applies."
                | Guards ->
                    $"Test at {where} declares it guards issue #{marker.Issue}, which is still OPEN — the fix it guards has not landed, so the assertion cannot be pinning corrected behavior.",
                    $"Close issue #{marker.Issue} once its fix and this guard are both in place, or correct the `guards #{marker.Issue}` marker."

            Some(create "bugGuard.openIssuePinned" DiagnosticWarning artifact location message correction [])
        | Unknown ->
            Some(
                create
                    "bugGuard.unresolvedIssue"
                    DiagnosticWarning
                    artifact
                    location
                    $"Test at {where} references issue #{marker.Issue} via a `{token}` marker, but that issue could not be resolved — a dangling structured link."
                    $"Point the marker at a real issue number, or remove the `{token} #{marker.Issue}` marker."
                    []
            )

    let check (resolve: int -> IssueState) (markers: BugGuardMarker list) : Diagnostic list =
        markers
        |> List.sortBy (fun m -> m.Path, m.Line, m.Issue, markerKindValue m.Kind)
        |> List.choose (fun marker -> diagnosticFor (resolve marker.Issue) marker)

    let checkSources (resolve: int -> IssueState) (sources: (string * string) list) : Diagnostic list =
        sources
        |> List.collect (fun (path, text) -> scanText path text)
        |> check resolve
