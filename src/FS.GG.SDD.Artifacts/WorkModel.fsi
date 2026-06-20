namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.LifecycleArtifacts
open FS.GG.SDD.Artifacts.SchemaVersion

module WorkModel =
    type ProjectSummary = { Id: string; DefaultWorkRoot: string }

    type SourceEntry =
        { Path: string
          Kind: string
          Owner: string
          SchemaVersion: int
          RawSchemaVersion: string option
          SchemaStatus: string
          SourceDigest: SourceDigest }

    type WorkItemSummary =
        { Id: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    type RequirementEntry =
        { Id: string
          Title: string
          Text: string
          AcceptanceCriteria: string list
          Priority: string option
          Source: string
          SourceLocation: SourceLocation option
          LinkedTaskIds: string list
          LinkedEvidenceIds: string list }

    type DecisionEntry =
        { Id: string
          Title: string
          Decision: string
          Source: string
          SourceLocation: SourceLocation option
          LinkedTaskIds: string list }

    type TaskEntry =
        { Id: string
          Title: string
          Status: string
          Owner: string
          Dependencies: string list
          Requirements: string list
          Decisions: string list
          SourceIds: string list
          RequiredSkills: string list
          RequiredEvidence: string list
          Source: string
          SourceLocation: SourceLocation option }

    type EvidenceEntry =
        { Id: string
          Kind: string
          SubjectType: string
          SubjectId: string
          TaskRefs: string list
          RequirementRefs: string list
          ArtifactRefs: string list
          Result: string
          Synthetic: bool
          Rationale: string option
          Source: string
          SourceLocation: SourceLocation option }

    type GovernanceBoundaryEntry =
        { Path: string
          Owner: string
          RequiredBySdd: bool
          Relationship: string }

    type WorkModel =
        { SchemaVersion: int
          ModelVersion: string
          WorkId: string
          Project: ProjectSummary
          Sources: SourceEntry list
          WorkItem: WorkItemSummary
          Requirements: RequirementEntry list
          Decisions: DecisionEntry list
          Tasks: TaskEntry list
          Evidence: EvidenceEntry list
          GeneratedViews: GenerationManifest list
          Diagnostics: Diagnostic list
          GovernanceBoundaries: GovernanceBoundaryEntry list }

    type WorkModelGenerationRequest =
        { WorkId: string
          Snapshots: FileSnapshot list
          GeneratorVersion: GeneratorVersion
          ExpectedOutputPath: string option }

    type WorkModelGenerationResult =
        { WorkId: string
          OutputPath: string
          Model: WorkModel
          Json: string
          OutputDigest: OutputDigest
          Diagnostics: Diagnostic list }

    type NormalizedGuidanceModel =
        { WorkId: string
          Stage: string
          Commands: GuidanceCommandEntry list
          Skills: GuidanceSkillEntry list
          SourceIdentities: string list }

    val fromParsedWorkItem: parsed: ParsedWorkItem -> WorkModel
    val blockingDiagnostics: model: WorkModel -> Diagnostic list
    val governanceBoundaryEntries: model: WorkModel -> GovernanceBoundaryEntry list
    val parseWorkModel: snapshot: FileSnapshot -> Result<WorkModel, Diagnostic list>
    val deriveGuidanceModel: model: WorkModel -> NormalizedGuidanceModel
    val behaviorModelDigest: model: NormalizedGuidanceModel -> SourceDigest
