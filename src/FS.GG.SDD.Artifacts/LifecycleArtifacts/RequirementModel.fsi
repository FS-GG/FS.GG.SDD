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
        {
            Id: RequirementId
            Title: string
            Text: string
            AcceptanceCriteria: string list
            Priority: string option
            /// The classification facets declared on the requirement's coverage line (ADR-0048),
            /// sorted and deduplicated. An unannotated FR carries the empty list — it is
            /// *unclassified*. Opt-in and additive: the facet is populated only from a recognized
            /// brace token (`{gameplay}`) on the FR line, so every existing spec stays valid.
            Classification: string list
            Source: ArtifactRef
            SourceLocation: SourceLocation option
        }

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

    /// The closed set of recognized functional-requirement classification facets (ADR-0048),
    /// lowercased. Initially just `gameplay`. This is the single source of truth for which class
    /// tokens a coverage line's `{…}` annotation may name; grow it here as new per-FR gates need
    /// new facets.
    val recognizedRequirementClasses: string list

    /// The classification facets a single functional-requirement line declares: every
    /// brace-delimited token (`{gameplay}`) whose lowercased value is in
    /// `recognizedRequirementClasses`, sorted and deduplicated. An unrecognized brace token is
    /// ignored — it never blocks, so a line with no recognized token is unclassified (`[]`). This
    /// keeps the annotation opt-in and every existing specification valid.
    val requirementClassification: line: string -> string list

    val parseRequirements: snapshot: FileSnapshot -> Requirement list
    val parseDecisions: snapshot: FileSnapshot -> Decision list
    val internal parseMarkdownRequirementMentions: snapshot: FileSnapshot -> MarkdownRequirementMention list
