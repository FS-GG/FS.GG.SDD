namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val standardArtifactContracts: unit -> LifecycleArtifactContract list
    val internal frontMatter: snapshot: FileSnapshot -> (string * string) option
