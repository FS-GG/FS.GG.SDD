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
module Checklist =
    type ChecklistFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          PublicOrToolFacingImpact: bool option }

    type ChecklistSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type ChecklistItem =
        { ItemId: ChecklistItemId
          Text: string
          Blocking: bool
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type ChecklistReviewResult =
        { ResultId: ChecklistResultId
          ItemId: ChecklistItemId option
          Status: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type ChecklistFacts =
        { FrontMatter: ChecklistFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          SourceSnapshots: ChecklistSourceSnapshot list
          Items: ChecklistItem list
          Results: ChecklistReviewResult list
          AcceptedDeferrals: ChecklistReviewResult list
          BlockingFindings: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleResultCount: int
          Diagnostics: Diagnostic list }

    let checklistStandardSections () =
        [ "Source Specification"
          "Source Clarifications"
          "Source Snapshot"
          "Checklist Items"
          "Review Results"
          "Accepted Deferrals"
          "Blocking Findings"
          "Advisory Notes"
          "Lifecycle Notes" ]

    let parseChecklistFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Checklist

        match frontMatter snapshot with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Checklist artifact is missing structured front matter." ]
        | Some(yaml, body) ->
            match parseYaml yaml with
            | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Checklist front matter is empty." ]
            | Some root ->
                let version, versionDiagnostics = schemaVersion artifact root
                let workId = tryScalarAt [ "workId" ] root |> Option.bind (Identifiers.createWorkId >> Result.toOption)
                let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption)
                let sourceSpec = tryScalarAt [ "sourceSpec" ] root
                let sourceClarifications = tryScalarAt [ "sourceClarifications" ] root

                match version, workId, stage, sourceSpec, sourceClarifications, versionDiagnostics with
                | Some schema, Some workId, Some stage, Some sourceSpec, Some sourceClarifications, [] ->
                    Ok
                        ({ SchemaVersion = schema
                           WorkId = workId
                           Title = tryScalarAt [ "title" ] root |> Option.defaultValue (Identifiers.workIdValue workId)
                           Stage = stage
                           ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                           Status = tryScalarAt [ "status" ] root |> Option.defaultValue "needsReview"
                           SourceSpec = sourceSpec
                           SourceClarifications = sourceClarifications
                           PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                         body)
                | _ ->
                    Error
                        (versionDiagnostics
                         @ [ Diagnostics.workModelInconsistent
                                 artifact
                                 "Checklist front matter is incomplete."
                                 "Add schemaVersion, workId, title, stage: checklist, changeTier, status, sourceSpec, and sourceClarifications to checklist.md."
                                 [] ])

    let checklistItemIdsInLine line =
        Regex.Matches(line, @"\bCHK-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createChecklistItemId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let checklistResultIdsInLine line =
        Regex.Matches(line, @"\bCR-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.choose (fun m -> Identifiers.createChecklistResultId m.Value |> Result.toOption)
        |> Seq.distinctBy (fun id -> id.Value)
        |> Seq.toList

    let sourceIdsInLine line =
        Regex.Matches(line, @"\b(?:FR|US|AC|SB|AMB|CQ|DEC|CHK)-\d{3,}\b", RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value.ToUpperInvariant())
        |> Seq.distinct
        |> Seq.toList

    let parseChecklistSourceSnapshots text : ChecklistSourceSnapshot list =
        sectionLines "Source Snapshot" text
        |> List.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(
                    line,
                    @"^\s*-\s*([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(\S+)(?:\s+sha256:([a-fA-F0-9]{64}))?(?:\s+schemaVersion:(\d+))?",
                    RegexOptions.IgnoreCase)

            if m.Success then
                let schema =
                    if m.Groups.[4].Success then
                        match Int32.TryParse m.Groups.[4].Value with
                        | true, value -> Some value
                        | _ -> None
                    else
                        None

                Some
                    { Label = m.Groups.[1].Value
                      Path = normalizePath m.Groups.[2].Value
                      Digest = if m.Groups.[3].Success then Some(m.Groups.[3].Value.ToLowerInvariant()) else None
                      SchemaVersion = schema
                      SourceLocation = sourceLocation lineNumber }
            else
                None)

    let parseChecklistItems text =
        sectionLines "Checklist Items" text
        |> List.choose (fun (lineNumber, line) ->
            match checklistItemIdsInLine line |> List.tryHead with
            | Some itemId ->
                let lowered = line.ToLowerInvariant()

                Some
                    { ItemId = itemId
                      Text = cleanAfterId itemId.Value line
                      Blocking = not (containsWord "advisory" lowered)
                      SourceIds = sourceIdsInLine line |> List.filter ((<>) itemId.Value)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let parseChecklistResultsInSection heading text =
        sectionLines heading text
        |> List.choose (fun (lineNumber, line) ->
            match checklistResultIdsInLine line |> List.tryHead with
            | Some resultId ->
                let itemId = checklistItemIdsInLine line |> List.tryHead
                let lowered = line.ToLowerInvariant()
                let status =
                    if containsWord "accepteddeferral" lowered || containsWord "accepted deferral" lowered then
                        "acceptedDeferral"
                    elif containsWord "stale" lowered then "stale"
                    elif containsWord "fail" lowered then "fail"
                    elif containsWord "advisory" lowered then "advisory"
                    elif containsWord "pass" lowered then "pass"
                    else "unknown"

                Some
                    { ResultId = resultId
                      ItemId = itemId
                      Status = status
                      Text = cleanAfterId resultId.Value line
                      SourceIds = sourceIdsInLine line |> List.filter (fun value -> itemId |> Option.exists (fun id -> id.Value = value) |> not)
                      SourceLocation = sourceLocation lineNumber }
            | None -> None)

    let checklistReferenceDiagnostics artifact (items: ChecklistItem list) (results: ChecklistReviewResult list) =
        let knownItems = items |> List.map (fun item -> item.ItemId.Value) |> Set.ofList

        results
        |> List.choose (fun result ->
            match result.ItemId with
            | Some itemId when Set.contains itemId.Value knownItems -> None
            | Some itemId -> Some(Diagnostics.unknownReference artifact itemId.Value "Declare the checklist item before recording a review result for it.")
            | None -> Some(Diagnostics.workModelInconsistent artifact $"Checklist result {result.ResultId.Value} is missing a CHK-### item reference." "Add [CHK:CHK-###] to the review result." [ result.ResultId.Value ]))

    let parseChecklistFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Checklist

        match parseChecklistFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, _) ->
            let text = (if String.IsNullOrEmpty snapshot.Text then "" else snapshot.Text).Replace("\r\n", "\n")
            let standardSections = checklistStandardSections ()
            let missingStandardSections = standardSections |> List.filter (fun heading -> not (hasHeading heading text))
            let snapshots = parseChecklistSourceSnapshots text
            let items = parseChecklistItems text
            let reviewResults = parseChecklistResultsInSection "Review Results" text
            let acceptedDeferrals = parseChecklistResultsInSection "Accepted Deferrals" text
            let results = reviewResults @ acceptedDeferrals
            let blockingFindings = parseNonEmptySectionLines "Blocking Findings" text
            let advisoryNotes = parseNonEmptySectionLines "Advisory Notes" text
            let lifecycleNotes = parseNonEmptySectionLines "Lifecycle Notes" text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: ChecklistItemId) -> id.Value) (items |> List.map (fun item -> item.ItemId, item.SourceLocation))
                  duplicateScopedDiagnostics artifact (fun (id: ChecklistResultId) -> id.Value) (results |> List.map (fun result -> result.ResultId, result.SourceLocation))
                  checklistReferenceDiagnostics artifact items results
                  missingStandardSections
                  |> List.map (fun heading ->
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Checklist artifact is missing the '{heading}' section."
                          $"Add a '## {heading}' section to checklist.md before relying on the parsed facts."
                          [ heading ]) ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  SourceSnapshots = snapshots |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                  Items = items |> List.sortBy (fun item -> item.ItemId.Value)
                  Results = results |> List.sortBy (fun result -> result.ResultId.Value)
                  AcceptedDeferrals = acceptedDeferrals |> List.sortBy (fun result -> result.ResultId.Value)
                  BlockingFindings = blockingFindings |> List.sort
                  AdvisoryNotes = advisoryNotes |> List.sort
                  LifecycleNotes = lifecycleNotes
                  StaleResultCount = results |> List.filter (fun result -> result.Status = "stale") |> List.length
                  Diagnostics = diagnostics }
