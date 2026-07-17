namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val parseGeneratedAgentGuidance: snapshot: FileSnapshot -> Result<GeneratedAgentGuidance, Diagnostic list>
