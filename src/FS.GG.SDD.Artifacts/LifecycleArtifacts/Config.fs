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
module Config =
    type ProjectLifecycleConfig =
        { SchemaVersion: SchemaVersion
          ProjectId: string
          DefaultWorkRoot: string
          SddConfigPath: string
          AgentsConfigPath: string
          GovernancePolicyPath: string option
          GovernanceCapabilitiesPath: string option
          GovernanceToolingPath: string option }

    type SddLifecyclePolicy =
        { SchemaVersion: SchemaVersion
          Stages: LifecycleStage list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    type AgentGuidanceTarget = { Id: string; GuidancePath: string; GeneratedRoot: string }

    type AgentGuidanceConfig =
        { SchemaVersion: SchemaVersion
          Targets: AgentGuidanceTarget list
          WorkModelPath: string
          GeneratedGuidanceIsAuthority: bool
          RequireEquivalentClaudeAndCodexBehavior: bool }

    let parseProjectConfig (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.ProjectConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Project config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let fields =
                [ requiredScalar artifact "project.id" [ "project"; "id" ] root
                  requiredScalar artifact "project.defaultWorkRoot" [ "project"; "defaultWorkRoot" ] root
                  requiredScalar artifact "sdd.config" [ "sdd"; "config" ] root
                  requiredScalar artifact "sdd.agents" [ "sdd"; "agents" ] root ]

            let fieldDiagnostics =
                fields
                |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)
                |> combine

            match version, fields, versionDiagnostics @ fieldDiagnostics with
            | Some schema, [ Ok projectId; Ok workRoot; Ok sddPath; Ok agentsPath ], [] ->
                Ok
                    { SchemaVersion = schema
                      ProjectId = projectId
                      DefaultWorkRoot = workRoot
                      SddConfigPath = sddPath
                      AgentsConfigPath = agentsPath
                      GovernancePolicyPath = tryScalarAt [ "governance"; "policy" ] root
                      GovernanceCapabilitiesPath = tryScalarAt [ "governance"; "capabilities" ] root
                      GovernanceToolingPath = tryScalarAt [ "governance"; "tooling" ] root }
            | _ -> Error(versionDiagnostics @ fieldDiagnostics)

    let parseSddLifecyclePolicy (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.SddConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "SDD config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root
            let stageResults = scalarList [ "lifecycle"; "stages" ] root |> List.map Identifiers.parseStage
            let stageDiagnostics =
                stageResults
                |> List.choose (function
                    | Ok _ -> None
                    | Error message -> Some(Diagnostics.workModelInconsistent artifact message "Use one of the standard SDD lifecycle stage ids." []))

            match version with
            | Some schema when List.isEmpty versionDiagnostics && List.isEmpty stageDiagnostics ->
                Ok
                    { SchemaVersion = schema
                      Stages = stageResults |> List.choose (function Ok stage -> Some stage | Error _ -> None)
                      WorkRoot = tryScalarAt [ "artifacts"; "workRoot" ] root |> Option.defaultValue "work"
                      ReadinessRoot = tryScalarAt [ "artifacts"; "readinessRoot" ] root |> Option.defaultValue "readiness"
                      RequireSourceDigests = boolAt [ "generatedViews"; "requireSourceDigests" ] root true
                      RequireGeneratorVersion = boolAt [ "generatedViews"; "requireGeneratorVersion" ] root true
                      StaleBehavior = tryScalarAt [ "generatedViews"; "staleBehavior" ] root |> Option.defaultValue "diagnostic" }
            | _ -> Error(versionDiagnostics @ stageDiagnostics)

    let parseAgentGuidanceConfig (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.AgentsConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Agent config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let targets =
                trySequenceAt [ "agents" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.choose (fun node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            match tryScalarAt [ "id" ] mapping, tryScalarAt [ "guidancePath" ] mapping, tryScalarAt [ "generatedRoot" ] mapping with
                            | Some id, Some guidancePath, Some generatedRoot ->
                                Some { Id = id; GuidancePath = guidancePath; GeneratedRoot = generatedRoot }
                            | _ -> None))
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some schema, [] ->
                Ok
                    { SchemaVersion = schema
                      Targets = targets
                      WorkModelPath = tryScalarAt [ "sourceModel"; "workModel" ] root |> Option.defaultValue "readiness/{workId}/work-model.json"
                      GeneratedGuidanceIsAuthority = boolAt [ "policy"; "generatedGuidanceIsAuthority" ] root false
                      RequireEquivalentClaudeAndCodexBehavior = boolAt [ "policy"; "requireEquivalentClaudeAndCodexBehavior" ] root true }
            | _ -> Error versionDiagnostics
