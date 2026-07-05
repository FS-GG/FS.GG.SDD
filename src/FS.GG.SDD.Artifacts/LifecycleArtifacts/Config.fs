namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Fsgg.Provider
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
          GovernanceToolingPath: string option
          TestFramework: string option }

    type SddLifecyclePolicy =
        { SchemaVersion: SchemaVersion
          Stages: LifecycleStage list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    type AgentGuidanceTarget =
        { Id: string
          GuidancePath: string
          GeneratedRoot: string }

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
                |> List.choose (function
                    | Error diagnostics -> Some diagnostics
                    | Ok _ -> None)
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
                      GovernanceToolingPath = tryScalarAt [ "governance"; "tooling" ] root
                      TestFramework =
                        tryScalarAt [ "project"; "testFramework" ] root
                        |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value)) }
            | _ -> Error(versionDiagnostics @ fieldDiagnostics)

    let parseSddLifecyclePolicy (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.SddConfig

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "SDD config is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let stageResults =
                scalarList [ "lifecycle"; "stages" ] root |> List.map Identifiers.parseStage

            let stageDiagnostics =
                stageResults
                |> List.choose (function
                    | Ok _ -> None
                    | Error message ->
                        Some(
                            Diagnostics.workModelInconsistent
                                artifact
                                message
                                "Use one of the standard SDD lifecycle stage ids."
                                []
                        ))

            match version with
            | Some schema when List.isEmpty versionDiagnostics && List.isEmpty stageDiagnostics ->
                Ok
                    { SchemaVersion = schema
                      Stages =
                        stageResults
                        |> List.choose (function
                            | Ok stage -> Some stage
                            | Error _ -> None)
                      WorkRoot = tryScalarAt [ "artifacts"; "workRoot" ] root |> Option.defaultValue "work"
                      ReadinessRoot =
                        tryScalarAt [ "artifacts"; "readinessRoot" ] root
                        |> Option.defaultValue "readiness"
                      RequireSourceDigests = boolAt [ "generatedViews"; "requireSourceDigests" ] root true
                      RequireGeneratorVersion = boolAt [ "generatedViews"; "requireGeneratorVersion" ] root true
                      StaleBehavior =
                        tryScalarAt [ "generatedViews"; "staleBehavior" ] root
                        |> Option.defaultValue "diagnostic" }
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
                            match
                                tryScalarAt [ "id" ] mapping,
                                tryScalarAt [ "guidancePath" ] mapping,
                                tryScalarAt [ "generatedRoot" ] mapping
                            with
                            | Some id, Some guidancePath, Some generatedRoot ->
                                Some
                                    { Id = id
                                      GuidancePath = guidancePath
                                      GeneratedRoot = generatedRoot }
                            | _ -> None))
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some schema, [] ->
                Ok
                    { SchemaVersion = schema
                      Targets = targets
                      WorkModelPath =
                        tryScalarAt [ "sourceModel"; "workModel" ] root
                        |> Option.defaultValue "readiness/{workId}/work-model.json"
                      GeneratedGuidanceIsAuthority = boolAt [ "policy"; "generatedGuidanceIsAuthority" ] root false
                      RequireEquivalentClaudeAndCodexBehavior =
                        boolAt [ "policy"; "requireEquivalentClaudeAndCodexBehavior" ] root true }
            | _ -> Error versionDiagnostics

    // A declared `build`/`test`/`run`/`verify` command under a provider entry: read the
    // nested `executable` scalar (default blank) and `arguments` sequence (default `[]`).
    // A blank executable is MALFORMED (`Fsgg.Provider.isMalformed`) and maps to `None`,
    // never a launchable empty command; an absent key likewise maps to `None` (FR-005).
    let private declaredCommand key (mapping: YamlNode) =
        let candidate =
            { Executable = tryScalarAt [ key; "executable" ] mapping |> Option.defaultValue ""
              Arguments = scalarList [ key; "arguments" ] mapping }

        if isMalformed candidate then None else Some candidate

    let parseProviderRegistry (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path (ArtifactKind.Other "providerRegistry")

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Provider registry is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            // Each entry needs name/contractVersion/templateId/source; declared
            // parameters are optional. Incomplete entries are dropped (a `--provider`
            // naming a dropped entry then resolves to scaffold.providerUnknown).
            let descriptors =
                trySequenceAt [ "providers" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.choose (fun node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            match
                                tryScalarAt [ "name" ] mapping,
                                tryScalarAt [ "contractVersion" ] mapping,
                                tryScalarAt [ "templateId" ] mapping,
                                tryScalarAt [ "source" ] mapping
                            with
                            | Some name, Some contractVersion, Some templateId, Some source ->
                                let parameters =
                                    trySequenceAt [ "parameters" ] mapping
                                    |> Option.map (fun parameterSequence ->
                                        parameterSequence.Children
                                        |> Seq.choose (fun parameterNode ->
                                            parameterNode
                                            |> tryMapping
                                            |> Option.bind (fun parameterMapping ->
                                                tryScalarAt [ "key" ] parameterMapping
                                                |> Option.map (fun key ->
                                                    { Key = key
                                                      Required = boolAt [ "required" ] parameterMapping false
                                                      Default = tryScalarAt [ "default" ] parameterMapping })))
                                        |> Seq.toList)
                                    |> Option.defaultValue []

                                Some
                                    { Name = name
                                      ContractVersion = contractVersion
                                      TemplateId = templateId
                                      Source = source
                                      Parameters = parameters
                                      Build = declaredCommand "build" mapping
                                      Test = declaredCommand "test" mapping
                                      Run = declaredCommand "run" mapping
                                      Verify = declaredCommand "verify" mapping
                                      NameParameter =
                                        tryScalarAt [ "nameParameter" ] mapping
                                        |> Option.defaultValue defaultNameParameter
                                      // Optional derivation sink (feature 080). Absent or
                                      // blank/whitespace ⇒ None ⇒ scaffold derives nothing.
                                      // Does NOT affect entry-drop (the four required scalars).
                                      IdentifierParameter =
                                        tryScalarAt [ "identifierParameter" ] mapping
                                        |> Option.filter (fun raw -> raw.Trim() <> "")
                                      // Optional, value-agnostic (feature 052 E2). The coherent-set
                                      // orchestrator axis (ADR-0008, epic FS-GG/.github#85) is
                                      // declared by Templates as a nested `minimumFsggSdd:` mapping
                                      // whose `version` scalar carries the minimum coherent fsgg-sdd
                                      // version (sibling metadata — requires/adr/registry/tracking —
                                      // is ignored here). A YAML-null `version` (the real PENDING
                                      // PUBLISH state) is treated as absent ⇒ `None`; any other value
                                      // is read verbatim, with validity decided only at comparison
                                      // (`Fsgg.Version`). Does NOT affect entry-drop (the four
                                      // required scalars above).
                                      MinimumCliVersion =
                                        tryScalarAt [ "minimumFsggSdd"; "version" ] mapping
                                        |> Option.filter (fun raw ->
                                            match raw.Trim() with
                                            | ""
                                            | "null"
                                            | "Null"
                                            | "NULL"
                                            | "~" -> false
                                            | _ -> true) }
                            | _ -> None))
                    |> Seq.toList)
                |> Option.defaultValue []

            match version, versionDiagnostics with
            | Some _, [] -> Ok descriptors
            | _ -> Error versionDiagnostics
