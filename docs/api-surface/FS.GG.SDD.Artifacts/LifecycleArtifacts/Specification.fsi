namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val specificationStandardSections: unit -> string list
    val parseSpecificationFacts: snapshot: FileSnapshot -> Result<SpecificationFacts, Diagnostic list>
