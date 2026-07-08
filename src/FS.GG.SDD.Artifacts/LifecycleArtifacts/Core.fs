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
module Core =
    type FileSnapshot = { Path: string; Text: string }

    type AnalysisSourceRecord =
        { Path: string
          Kind: string
          Digest: SourceDigest option
          SchemaVersion: int option
          SchemaStatus: string option }

    type AnalysisGeneratedViewRecord =
        { Path: string
          Kind: string
          Currency: string
          DiagnosticIds: string list }

    type AnalysisOptionalBoundaryFact =
        { Path: string
          Relationship: string
          RequiredBySdd: bool
          State: string
          DiagnosticIds: string list }

    type LifecycleArtifactContract =
        { Artifact: ArtifactRef
          Purpose: string
          SourceOfTruth: string
          StructuredContract: string
          GeneratedViewRelationship: string
          StaleBehavior: string
          DiagnosticFamily: string list }

    let standardArtifactContracts () =
        let row path kind purpose generated diagnostics =
            { Artifact = sourceArtifact path kind
              Purpose = purpose
              SourceOfTruth = path
              StructuredContract = "schemaVersion: 1 structured lifecycle data"
              GeneratedViewRelationship = generated
              StaleBehavior =
                "staleGeneratedView diagnostic when source digest, schema version, generator version, or output digest differs"
              DiagnosticFamily = diagnostics }

        [ row
              ".fsgg/project.yml"
              ArtifactKind.ProjectConfig
              "Project identity and lifecycle roots."
              "Contributes project identity to readiness/<id>/work-model.json."
              [ "missingArtifact"; "malformedSchemaVersion" ]
          row
              ".fsgg/sdd.yml"
              ArtifactKind.SddConfig
              "SDD lifecycle policy and artifact layout."
              "Contributes lifecycle policy to work-model, analysis, verify, and ship views."
              [ "missingArtifact"; "malformedSchemaVersion"; "unsupportedSchemaVersion" ]
          row
              ".fsgg/agents.yml"
              ArtifactKind.AgentsConfig
              "Agent guidance targets for Claude and Codex."
              "Contributes to readiness/<id>/agent-commands/."
              [ "missingArtifact"; "staleGeneratedView" ]
          row
              "work/<id>/charter.md"
              (ArtifactKind.Other "charter")
              "Optional local charter and boundary statement."
              "Contributes authored context to readiness summaries when present."
              [ "missingArtifact"; "proseStructuredMismatch" ]
          row
              "work/<id>/spec.md"
              ArtifactKind.Spec
              "User value, requirements, scenarios, and structured work metadata."
              "Sources requirements and work metadata in work-model.json."
              [ "missingArtifact"; "requirementNotTyped"; "proseStructuredMismatch" ]
          row
              "work/<id>/clarifications.md"
              ArtifactKind.Clarifications
              "Clarification answers and material ambiguity decisions."
              "Sources decision entries in work-model.json."
              [ "missingArtifact"; "unknownReference" ]
          row
              "work/<id>/checklist.md"
              ArtifactKind.Checklist
              "Requirements-quality review checklist."
              "Feeds analysis and verify readiness views."
              [ "missingArtifact"; "workModelInconsistent" ]
          row
              "work/<id>/plan.md"
              ArtifactKind.Plan
              "Technical plan, contracts, risks, and verification strategy."
              "Sources decisions and plan obligations."
              [ "missingArtifact"; "unknownReference"; "proseStructuredMismatch" ]
          row
              "work/<id>/contracts/"
              ArtifactKind.Contracts
              "Public and tool-facing contracts attached to the plan."
              "Referenced by rule contracts and work-model sources."
              [ "missingArtifact"; "unknownReference" ]
          row
              "work/<id>/tasks.yml"
              ArtifactKind.Tasks
              "Typed implementation task graph."
              "Sources task entries in work-model.json and verify readiness."
              [ "missingArtifact"
                "duplicateIdentifier"
                "unknownReference"
                "workModelInconsistent" ]
          row
              "work/<id>/evidence.yml"
              ArtifactKind.Evidence
              "Implementation and verification evidence declarations."
              "Sources evidence entries in work-model.json and ship readiness."
              [ "missingArtifact"; "unknownReference"; "workModelInconsistent" ]
          row
              "readiness/<id>/work-model.json"
              ArtifactKind.GeneratedView
              "Deterministic normalized lifecycle contract."
              "Generated from SDD sources and used by tools and agents."
              [ "staleGeneratedView"; "malformedDigest" ]
          row
              "readiness/<id>/analysis.json"
              ArtifactKind.GeneratedView
              "Cross-artifact consistency diagnostics."
              "Generated from normalized work model diagnostics."
              [ "staleGeneratedView" ]
          row
              "readiness/<id>/verify.json"
              ArtifactKind.GeneratedView
              "SDD verification readiness facts."
              "Generated from work model and evidence declarations."
              [ "staleGeneratedView" ]
          row
              "readiness/<id>/ship.json"
              ArtifactKind.GeneratedView
              "Merge-boundary SDD readiness facts."
              "Generated from verify readiness and evidence declarations."
              [ "staleGeneratedView" ]
          row
              "readiness/<id>/ship-verdict.json"
              ArtifactKind.GeneratedView
              "Committed merge-boundary verdict (ADR-0026): the durable-generated projection of ship.json."
              "Projected from ship.json; sources[] replaced by one aggregate sourcesDigest."
              [ "staleGeneratedView" ]
          row
              "readiness/<id>/summary.md"
              ArtifactKind.GeneratedView
              "Human-readable readiness summary."
              "Rendered projection over structured readiness facts."
              [ "staleGeneratedView" ]
          row
              "readiness/<id>/agent-commands/"
              ArtifactKind.GeneratedView
              "Generated Claude/Codex command guidance."
              "Projection from lifecycle model, never authority."
              [ "staleGeneratedView" ] ]

    let frontMatter (snapshot: FileSnapshot) : (string * string) option =
        let normalized =
            (if String.IsNullOrEmpty snapshot.Text then
                 ""
             else
                 snapshot.Text)
                .Replace("\r\n", "\n")

        let lines = normalized.Split('\n')

        if lines.Length > 0 && lines.[0].Trim() = "---" then
            let endIndex =
                lines
                |> Array.mapi (fun index line -> index, line)
                |> Array.tryFind (fun (index, line) -> index > 0 && line.Trim() = "---")
                |> Option.map fst

            match endIndex with
            | Some index ->
                let yaml = lines.[1 .. index - 1] |> String.concat "\n"
                let body = lines.[index + 1 ..] |> String.concat "\n"
                Some(yaml, body)
            | None -> None
        else
            None
