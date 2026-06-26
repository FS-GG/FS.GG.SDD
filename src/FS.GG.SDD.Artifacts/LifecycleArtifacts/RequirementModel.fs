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
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type MarkdownRequirementMention =
        { Id: string
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    let parseRequirements (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec
        let text = (if String.IsNullOrEmpty snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m = Regex.Match(line, @"^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

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
        let text = (if String.IsNullOrEmpty snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

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

    let parseDecisions (snapshot: FileSnapshot) =
        let kind =
            let path = normalizePath snapshot.Path

            if path.EndsWith("/clarifications.md", StringComparison.OrdinalIgnoreCase) then
                ArtifactKind.Clarifications
            else
                ArtifactKind.Spec

        let artifact = sourceArtifact snapshot.Path kind
        let text = (if String.IsNullOrEmpty snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")

        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m = Regex.Match(line, @"^\s*-\s*(DEC-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createDecisionId m.Groups.[1].Value with
                | Ok id ->
                    Some
                        { Id = id
                          Title = m.Groups.[2].Value.Trim()
                          Decision = m.Groups.[2].Value.Trim()
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList
