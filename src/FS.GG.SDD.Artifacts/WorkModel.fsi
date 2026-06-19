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
          SourceDigest: SourceDigest }

    type WorkItemSummary =
        { Id: string
          Title: string
          Stage: string
          ChangeTier: string
          Status: string }

    type RequirementEntry = { Id: string; Title: string; Text: string; Source: string }
    type DecisionEntry = { Id: string; Title: string; Decision: string; Source: string }

    type TaskEntry =
        { Id: string
          Title: string
          Status: string
          Owner: string
          Dependencies: string list
          Requirements: string list
          Decisions: string list
          RequiredSkills: string list
          RequiredEvidence: string list
          Source: string }

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
          Source: string }

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

    val fromParsedWorkItem: parsed: ParsedWorkItem -> WorkModel
    val blockingDiagnostics: model: WorkModel -> Diagnostic list
    val governanceBoundaryEntries: model: WorkModel -> GovernanceBoundaryEntry list
