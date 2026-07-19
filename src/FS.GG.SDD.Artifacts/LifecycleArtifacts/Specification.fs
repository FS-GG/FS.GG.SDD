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
module Specification =
    type SpecificationFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          PublicOrToolFacingImpact: bool option }

    type SpecificationRequirementReference =
        {
            RequirementId: RequirementId
            StoryIds: UserStoryId list
            AcceptanceScenarioIds: AcceptanceScenarioId list
            /// The classification facets declared on the requirement's coverage line (ADR-0048), so the
            /// task graph can derive the per-classified-FR gameplay obligation (WI-4). Empty ⇒ unclassified.
            Classification: string list
            SourceLocation: SourceLocation option
        }

    type SpecificationFacts =
        { FrontMatter: SpecificationFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          UserStoryIds: UserStoryId list
          RequirementIds: RequirementId list
          AcceptanceScenarioIds: AcceptanceScenarioId list
          ScopeBoundaryIds: ScopeBoundaryId list
          AmbiguityIds: AmbiguityId list
          RequirementReferences: SpecificationRequirementReference list
          Diagnostics: Diagnostic list }

    let specificationStandardSections () =
        [ "User Value"
          "Scope"
          "Non-Goals"
          "User Stories"
          "Acceptance Scenarios"
          "Functional Requirements"
          "Ambiguities"
          "Public Or Tool-Facing Impact"
          "Lifecycle Notes" ]

    let parseSpecificationFrontMatter (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        match frontMatter snapshot with
        | None ->
            Error [ Diagnostics.malformedSchemaVersion artifact "Specification is missing structured front matter." ]
        | Some(yaml, body) ->
            match yamlRoot artifact "Specification front matter is empty." 1 yaml with
            | Error diagnostics -> Error diagnostics
            | Ok root ->
                let version, versionDiagnostics = schemaVersion artifact root

                let workId =
                    tryScalarAt [ "workId" ] root
                    |> Option.bind (Identifiers.createWorkId >> Result.toOption)

                let stage =
                    tryScalarAt [ "stage" ] root
                    |> Option.bind (Identifiers.parseStage >> Result.toOption)

                match version, workId, stage, versionDiagnostics with
                | Some schema, Some workId, Some stage, [] ->
                    Ok(
                        { SchemaVersion = schema
                          WorkId = workId
                          Title =
                            tryScalarAt [ "title" ] root
                            |> Option.defaultValue (Identifiers.workIdValue workId)
                          Stage = stage
                          ChangeTier = tryScalarAt [ "changeTier" ] root |> Option.defaultValue "tier1"
                          Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                          PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] root },
                        body
                    )
                | _ ->
                    Error(
                        versionDiagnostics
                        @ [ Diagnostics.workModelInconsistent
                                artifact
                                "Specification front matter is incomplete."
                                "Add schemaVersion, workId, title, stage, changeTier, and status to spec front matter."
                                []
                            |> Diagnostics.withDefectTag Diagnostics.DefectTags.FrontMatterIncomplete ]
                    )

    let missingIdDiagnostics artifact (text: string) =
        // §3.3: a "none outstanding" sentinel under `## Ambiguities` (prose or bullet) is
        // exempt from the "every bullet needs a stable id" rule — it carries no id and is
        // non-blocking. Other sections do not allow the sentinel exemption.
        // `isCoverageLine` marks the FR/AC sections whose missing-id defect IS the load-bearing
        // coverage-line grammar defect (`lint` surfaces these as `CoverageLine`); US/AMB missing
        // ids are a different stable-id concern lint does not surface, so they carry no tag.
        let missing
            (heading: string)
            (pattern: string)
            (relatedId: string)
            (allowSentinel: bool)
            (isCoverageLine: bool)
            =
            sectionLines heading text
            |> List.choose (fun (lineNumber, line) ->
                if
                    Regex.IsMatch(line, @"^\s*-\s+\S", RegexOptions.IgnoreCase)
                    && not (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                    && not (allowSentinel && isNoOutstandingSentinel line)
                then
                    let diag =
                        Diagnostics.workModelInconsistent
                            artifact
                            $"Specification list item in '{heading}' is missing a required stable id."
                            $"Add a stable {relatedId} id to the list item before rerunning."
                            [ relatedId ]

                    Some(
                        if isCoverageLine then
                            diag |> Diagnostics.withDefectTag Diagnostics.DefectTags.CoverageStableId
                        else
                            diag
                    )
                else
                    None)

        [ missing "User Stories" @"\bUS-\d{3,}\b" "US-###" false false
          missing "Acceptance Scenarios" @"\bAC-\d{3,}\b" "AC-###" false true
          missing "Functional Requirements" @"\bFR-\d{3,}\b" "FR-###" false true
          missing "Ambiguities" @"\bAMB-\d{3,}\b" "AMB-###" true false ]
        |> List.concat

    let requirementReferences (text: string) : SpecificationRequirementReference list =
        text.Split('\n')
        |> Array.mapi (fun index line -> index + 1, line)
        |> Array.choose (fun (lineNumber, line) ->
            let m =
                Regex.Match(line, @"^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$", RegexOptions.IgnoreCase)

            if m.Success then
                match Identifiers.createRequirementId m.Groups.[1].Value with
                | Ok requirementId ->
                    let storyIds =
                        Regex.Matches(line, @"\bUS-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.choose (fun value -> Identifiers.createUserStoryId value.Value |> Result.toOption)
                        |> Seq.distinctBy (fun id -> id.Value)
                        |> Seq.toList

                    let acceptanceScenarioIds =
                        Regex.Matches(line, @"\bAC-\d{3,}\b", RegexOptions.IgnoreCase)
                        |> Seq.cast<Match>
                        |> Seq.choose (fun value ->
                            Identifiers.createAcceptanceScenarioId value.Value |> Result.toOption)
                        |> Seq.distinctBy (fun id -> id.Value)
                        |> Seq.toList

                    Some
                        { RequirementId = requirementId
                          StoryIds = storyIds
                          AcceptanceScenarioIds = acceptanceScenarioIds
                          Classification = RequirementModel.requirementClassification line
                          SourceLocation = sourceLocation lineNumber }
                | Error _ -> None
            else
                None)
        |> Array.toList

    let unknownSpecificationReferences
        artifact
        (stories: (UserStoryId * SourceLocation option) list)
        (acceptanceScenarios: (AcceptanceScenarioId * SourceLocation option) list)
        (references: SpecificationRequirementReference list)
        =
        let storyIds = stories |> List.map (fun (id, _) -> id.Value) |> Set.ofList

        let acceptanceIds =
            acceptanceScenarios |> List.map (fun (id, _) -> id.Value) |> Set.ofList

        references
        |> List.collect (fun reference ->
            [ reference.StoryIds
              |> List.choose (fun id ->
                  if Set.contains id.Value storyIds then
                      None
                  else
                      Some(
                          Diagnostics.unknownReference
                              artifact
                              id.Value
                              "Declare the user story id in the specification or remove the requirement link."
                      ))
              reference.AcceptanceScenarioIds
              |> List.choose (fun id ->
                  if Set.contains id.Value acceptanceIds then
                      None
                  else
                      Some(
                          Diagnostics.unknownReference
                              artifact
                              id.Value
                              "Declare the acceptance scenario id in the specification or remove the requirement link."
                      )) ]
            |> List.concat)

    let parseSpecificationFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Spec

        match parseSpecificationFrontMatter snapshot with
        | Error diagnostics -> Error diagnostics
        | Ok(frontMatter, body) ->
            let text =
                (if String.IsNullOrEmpty snapshot.Text then
                     ""
                 else
                     snapshot.Text)
                    .Replace("\r\n", "\n")

            let standardSections = specificationStandardSections ()

            let missingStandardSections =
                standardSections |> List.filter (fun heading -> not (hasHeading heading text))

            let stories =
                scopedIdLocationsInSections [ "User Stories" ] @"\bUS-\d{3,}\b" Identifiers.createUserStoryId text

            let requirements =
                scopedIdLocationsInSections
                    [ "Functional Requirements" ]
                    @"\bFR-\d{3,}\b"
                    Identifiers.createRequirementId
                    text

            let acceptanceScenarios =
                scopedIdLocationsInSections
                    [ "Acceptance Scenarios" ]
                    @"\bAC-\d{3,}\b"
                    Identifiers.createAcceptanceScenarioId
                    text

            let scopeBoundaries =
                scopedIdLocationsInSections
                    [ "Scope"; "Non-Goals" ]
                    @"\bSB-\d{3,}\b"
                    Identifiers.createScopeBoundaryId
                    text

            let ambiguities =
                scopedIdLocationsInSections [ "Ambiguities" ] @"\bAMB-\d{3,}\b" Identifiers.createAmbiguityId text

            let references = requirementReferences text

            let diagnostics =
                [ duplicateScopedDiagnostics artifact (fun (id: UserStoryId) -> id.Value) stories
                  duplicateScopedDiagnostics artifact (fun (id: RequirementId) -> id.Value) requirements
                  duplicateScopedDiagnostics artifact (fun (id: AcceptanceScenarioId) -> id.Value) acceptanceScenarios
                  duplicateScopedDiagnostics artifact (fun (id: ScopeBoundaryId) -> id.Value) scopeBoundaries
                  duplicateScopedDiagnostics artifact (fun (id: AmbiguityId) -> id.Value) ambiguities
                  missingIdDiagnostics artifact text
                  unknownSpecificationReferences artifact stories acceptanceScenarios references ]
                |> List.concat
                |> Diagnostics.sort

            Ok
                { FrontMatter = frontMatter
                  StandardSections = standardSections
                  MissingStandardSections = missingStandardSections
                  UserStoryIds = stories |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  RequirementIds = requirements |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  AcceptanceScenarioIds =
                    acceptanceScenarios
                    |> List.map fst
                    |> List.distinctBy _.Value
                    |> List.sortBy _.Value
                  ScopeBoundaryIds =
                    scopeBoundaries
                    |> List.map fst
                    |> List.distinctBy _.Value
                    |> List.sortBy _.Value
                  AmbiguityIds = ambiguities |> List.map fst |> List.distinctBy _.Value |> List.sortBy _.Value
                  RequirementReferences = references |> List.sortBy (fun reference -> reference.RequirementId.Value)
                  Diagnostics = diagnostics }
