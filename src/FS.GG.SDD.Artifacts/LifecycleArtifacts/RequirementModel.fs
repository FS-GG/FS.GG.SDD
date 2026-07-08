namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module RequirementModel =
    type Requirement =
        { Id: RequirementId
          Title: string
          Text: string
          AcceptanceCriteria: string list
          Priority: string option
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type Decision =
        { Id: DecisionId
          Title: string
          Decision: string
          RequirementRefs: RequirementId list
          StoryRefs: UserStoryId list
          AcceptanceRefs: AcceptanceScenarioId list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type MarkdownRequirementMention =
        { Id: string
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    let parseRequirements (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        let text =
            (if String.IsNullOrEmpty snapshot.Text then
                 ""
             else
                 snapshot.Text)
                .Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(line, @"^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createRequirementId m.Groups.[1].Value with
                | Ok id ->
                    let acceptanceCriteria =
                        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
                        |> Seq.distinct
                        |> Seq.toList

                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Text = m.Groups.[2].Value.Trim()
                          AcceptanceCriteria = acceptanceCriteria
                          Priority = None
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let parseMarkdownRequirementMentions (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        let text =
            (if String.IsNullOrEmpty snapshot.Text then
                 ""
             else
                 snapshot.Text)
                .Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.collect (fun (lineNumber, line) ->
            Regex.Matches(line, @"\b(?:FR|AC)-\d{3,}\b", RegexOptions.IgnoreCase)
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                { Id = m.Value.ToUpperInvariant()
                  Source = artifact
                  SourceLocation = sourceLocation lineNumber })
            |> Seq.toArray)
        |> Array.toList

    /// Every id of one family the line names, deduplicated and sorted by value. `create` rejects a token
    /// the regex matched but that is not a well-formed id, so a malformed ref is simply not a ref — the
    /// same rule the clarification parser uses.
    let private idsInLine
        (pattern: string)
        (create: string -> Result<'id, _>)
        (value: 'id -> string)
        (line: string)
        : 'id list =
        Regex.Matches(line, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> create m.Value |> Result.toOption)
        |> Seq.distinctBy value
        |> Seq.sortBy value
        |> Seq.toList

    let private requirementRefsInLine =
        idsInLine @"\bFR-\d{3,}\b" Identifiers.createRequirementId (fun id -> id.Value)

    let private storyRefsInLine =
        idsInLine @"\bUS-\d{3,}\b" Identifiers.createUserStoryId (fun id -> id.Value)

    let private acceptanceRefsInLine =
        idsInLine @"\bAC-\d{3,}\b" Identifiers.createAcceptanceScenarioId (fun id -> id.Value)

    let parseDecisions (snapshot: FileSnapshot) =
        let kind =
            let path = normalizePath snapshot.Path

            if path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then
                ArtifactKind.Clarifications
            else
                ArtifactKind.Spec

        let artifact = sourceArtifact snapshot.Path kind

        let text =
            (if String.IsNullOrEmpty snapshot.Text then
                 ""
             else
                 snapshot.Text)
                .Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(line, @"^\s*-\s*(DEC-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createDecisionId m.Groups.[1].Value with
                | Ok id ->
                    // A decision may settle several requirements at once, and name the stories and
                    // acceptance scenarios it touches (#164). Every ref on the line reaches the work
                    // model; before feature 093 none of them did.
                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Decision = m.Groups.[2].Value.Trim()
                          RequirementRefs = requirementRefsInLine line
                          StoryRefs = storyRefsInLine line
                          AcceptanceRefs = acceptanceRefsInLine line
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList
