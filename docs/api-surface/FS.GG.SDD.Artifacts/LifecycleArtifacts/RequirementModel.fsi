namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module RequirementModel =
    type Requirement =
        { Id: RequirementId
          Title: string
          Text: string
          AcceptanceCriteria: string list
          Priority: string option
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type Decision =
        {
            Id: DecisionId
            Title: string
            Decision: string
            /// Every `FR-###` the decision line names, sorted and deduplicated. A decision may settle
            /// several requirements at once (#164); before feature 093 these were parsed nowhere and
            /// the work model recorded none of them.
            RequirementRefs: RequirementId list
            /// Every `US-###` the decision line names, sorted and deduplicated.
            StoryRefs: UserStoryId list
            /// Every `AC-###` the decision line names, sorted and deduplicated.
            AcceptanceRefs: AcceptanceScenarioId list
            Source: ArtifactRef
            SourceLocation: SourceLocation option
        }

    type MarkdownRequirementMention =
        { Id: string
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    val parseRequirements: snapshot: FileSnapshot -> Requirement list
    val parseDecisions: snapshot: FileSnapshot -> Decision list
    val internal parseMarkdownRequirementMentions: snapshot: FileSnapshot -> MarkdownRequirementMention list
