namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val checklistStandardSections: unit -> string list
    val parseChecklistFacts: snapshot: FileSnapshot -> Result<ChecklistFacts, Diagnostic list>
