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
          Classification: string list
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

    // The closed set of recognized FR classification facets (ADR-0048), lowercased. Initially just
    // `gameplay`. Single source of truth for the coverage-line `{…}` annotation vocabulary.
    let recognizedRequirementClasses = [ "gameplay" ]

    let private recognizedRequirementClassSet =
        recognizedRequirementClasses
        |> List.map (fun cls -> cls.ToLowerInvariant())
        |> Set.ofList

    let requirementClassification (line: string) : string list =
        // Opt-in and additive: collect only brace tokens whose lowercased value is a recognized
        // class. An unrecognized `{…}` token (or braces used incidentally in prose) is ignored — it
        // never blocks, so a line with no recognized token stays unclassified. This is why every
        // pre-ADR-0048 spec remains valid.
        Regex.Matches(line, @"\{\s*([A-Za-z][A-Za-z0-9-]*)\s*\}")
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups.[1].Value.ToLowerInvariant())
        |> Seq.filter (fun token -> Set.contains token recognizedRequirementClassSet)
        |> Seq.distinct
        |> Seq.sort
        |> Seq.toList

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
                          Classification = requirementClassification line
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

    /// Every id of one family the line names, deduplicated (by `Internal.idsInLine`) and then sorted, so
    /// the author's phrasing order cannot move the bytes these refs reach — a decision's requirement refs
    /// are emitted into a task's `requirements:` list (#164). `create` rejects a token the regex matched
    /// but that is not a well-formed id, so a malformed ref is simply not a ref.
    let private sortedRefsInLine pattern create (value: 'id -> string) line : 'id list =
        idsInLine pattern create line |> List.sortBy value

    let private requirementRefsInLine =
        sortedRefsInLine @"\bFR-\d{3,}\b" Identifiers.createRequirementId (fun id -> id.Value)

    let private storyRefsInLine =
        sortedRefsInLine @"\bUS-\d{3,}\b" Identifiers.createUserStoryId (fun id -> id.Value)

    let private acceptanceRefsInLine =
        sortedRefsInLine @"\bAC-\d{3,}\b" Identifiers.createAcceptanceScenarioId (fun id -> id.Value)

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
            // Converge on the *authored* decision grammar the clarify stage and
            // `.fsgg/early-stage-guidance.md` teach and the shipped example uses:
            // `- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: text` — the id may
            // be bold, and bracketed decision tags stand between the id and the colon (a tag
            // like `[AMB:AMB-001]` may itself carry a colon, so the tag run is matched by
            // brackets, not by "up to the first colon"). The bare `- DEC-001: text` form still
            // parses (empty bold, empty tag run). Fixing this here rather than re-authoring the
            // example is a blocking->green change: a decision authored in the canonical grammar
            // never entered the work model before, so a task referencing it raised
            // unknownReference; it cannot newly break a `tasks.yml` that parses today
            // (ADR-0003, FS.GG.SDD#265).
            let m =
                Regex.Match(
                    line,
                    @"^\s*-\s*\*{0,2}(DEC-\d{3,})\*{0,2}((?:\s*\[[^\]]*\])*)\s*:\s*(.+)$",
                    RegexOptions.IgnoreCase
                )

            if m.Success then
                match Identifiers.createDecisionId m.Groups.[1].Value with
                | Ok id ->
                    // A decision may settle several requirements at once, and name the stories and
                    // acceptance scenarios it touches (#164). Every ref on the line reaches the work
                    // model; before feature 093 none of them did.
                    Some
                        { Id = id
                          Title = m.Groups.[3].Value.Trim()
                          Decision = m.Groups.[3].Value.Trim()
                          RequirementRefs = requirementRefsInLine line
                          StoryRefs = storyRefsInLine line
                          AcceptanceRefs = acceptanceRefsInLine line
                          Source = artifact
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList
