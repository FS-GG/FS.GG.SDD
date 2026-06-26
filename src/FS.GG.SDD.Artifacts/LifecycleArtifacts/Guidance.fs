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
module Guidance =
    type GuidanceCommandEntry =
        { Id: string
          Title: string
          Stage: string
          Purpose: string
          RelatedIds: string list }

    type GuidanceSkillEntry =
        { Id: string
          Title: string
          Capability: string
          RelatedIds: string list }

    type GeneratedGuidanceFileRef = { Path: string; Kind: string }

    type GeneratedAgentGuidance =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          TargetId: string
          Generator: string
          Generated: bool
          Sources: AnalysisSourceRecord list
          BehaviorModelDigest: SourceDigest
          Commands: GuidanceCommandEntry list
          Skills: GuidanceSkillEntry list
          RenderedFiles: GeneratedGuidanceFileRef list
          Diagnostics: Diagnostic list }

    let parseGuidanceCommandEntry (element: JsonElement) : GuidanceCommandEntry =
        { Id = jsonRequiredString "id" element
          Title = jsonRequiredString "title" element
          Stage = jsonRequiredString "stage" element
          Purpose = jsonRequiredString "purpose" element
          RelatedIds = jsonStringList "relatedIds" element }

    let parseGuidanceSkillEntry (element: JsonElement) : GuidanceSkillEntry =
        { Id = jsonRequiredString "id" element
          Title = jsonRequiredString "title" element
          Capability = jsonRequiredString "capability" element
          RelatedIds = jsonStringList "relatedIds" element }

    let parseGuidanceFileRef (element: JsonElement) : GeneratedGuidanceFileRef =
        { Path = normalizePath (jsonRequiredString "path" element)
          Kind = jsonRequiredString "kind" element }

    let parseGeneratedAgentGuidance (snapshot: FileSnapshot) =
        parseJsonView
            "Generated agent guidance"
            "Regenerate the generated agent-commands guidance.json with valid JSON."
            (fun artifact schema root ->
                let workIdText = jsonRequiredString "workId" root
                let targetId = jsonRequiredString "targetId" root

                match Identifiers.createWorkId workIdText, jsonDigest "behaviorModelDigest" root with
                | Ok workId, Some behaviorDigest when not (String.IsNullOrWhiteSpace targetId) ->
                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          TargetId = targetId
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Generated = jsonBool "generated" root |> Option.defaultValue true
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          BehaviorModelDigest = behaviorDigest
                          Commands = jsonArray "commands" root |> List.map parseGuidanceCommandEntry |> List.sortBy (fun command -> command.Id)
                          Skills = jsonArray "skills" root |> List.map parseGuidanceSkillEntry |> List.sortBy (fun skill -> skill.Id)
                          RenderedFiles = jsonArray "renderedFiles" root |> List.map parseGuidanceFileRef |> List.sortBy (fun file -> file.Path)
                          Diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Generated agent guidance identity fields are malformed."
                              "Regenerate guidance.json with a valid workId, targetId, and behaviorModelDigest."
                              [ workIdText; targetId ] ])
            snapshot.Path
            snapshot.Text
