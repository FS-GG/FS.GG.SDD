namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Resolution state of one authored ambiguity.
type AmbiguityState =
    | Open
    | Resolved
    | Deferred

type ScopeBoundary =
    { Id: SpecificationId
      Statement: string }

type RequirementStory =
    { Id: SpecificationId
      Priority: string
      Statement: string }

type Requirement =
    { Id: SpecificationId
      Statement: string
      AcceptanceIds: SpecificationId list
      EvidenceObligationIds: SpecificationId list }

type AcceptanceCriterion =
    { Id: SpecificationId
      StoryIds: SpecificationId list
      RequirementIds: SpecificationId list
      Statement: string }

type RequirementAmbiguity =
    { Id: SpecificationId
      Question: string
      State: AmbiguityState
      Decision: string option }

/// First SDD-owned domain extension over the reusable specification kernel.
type RequirementsExtension =
    { UserValue: string
      Scope: ScopeBoundary list
      NonGoals: ScopeBoundary list
      Stories: RequirementStory list
      Requirements: Requirement list
      Acceptance: AcceptanceCriterion list
      Ambiguities: RequirementAmbiguity list
      PublicImpact: string list
      LifecycleNotes: string list }

/// Functional authoring surface whose result is semantically identical to direct record construction.
type RequirementsDraft

[<RequireQualifiedAccess>]
module RequirementsDraft =
    val empty: RequirementsDraft
    val withUserValue: value: string -> draft: RequirementsDraft -> RequirementsDraft
    val addScope: boundary: ScopeBoundary -> draft: RequirementsDraft -> RequirementsDraft
    val addNonGoal: boundary: ScopeBoundary -> draft: RequirementsDraft -> RequirementsDraft
    val addStory: story: RequirementStory -> draft: RequirementsDraft -> RequirementsDraft
    val addRequirement: requirement: Requirement -> draft: RequirementsDraft -> RequirementsDraft
    val addAcceptance: acceptance: AcceptanceCriterion -> draft: RequirementsDraft -> RequirementsDraft
    val addAmbiguity: ambiguity: RequirementAmbiguity -> draft: RequirementsDraft -> RequirementsDraft
    val addPublicImpact: impact: string -> draft: RequirementsDraft -> RequirementsDraft
    val addLifecycleNote: note: string -> draft: RequirementsDraft -> RequirementsDraft
    val build: draft: RequirementsDraft -> RequirementsExtension

[<RequireQualifiedAccess>]
module RequirementsExtension =
    /// The explicit statically typed requirements compiler/codec/projection contract.
    val contract: ExtensionContract<RequirementsExtension>

    /// Validate requirements references and ambiguity/decision coherence.
    val validate: extension: RequirementsExtension -> SpecificationDiagnostic list

[<RequireQualifiedAccess>]
module RequirementsMigration =
    /// Analyze current Standard SDD Markdown without writing canonical source.
    val analyzeMarkdown: markdown: string -> MigrationOutcome<RequirementsExtension>
