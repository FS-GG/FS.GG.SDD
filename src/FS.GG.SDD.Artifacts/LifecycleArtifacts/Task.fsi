namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module Task =
    type TaskFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          PublicOrToolFacingImpact: bool option }

    type TaskSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type TaskGraphFinding =
        { FindingId: string
          Severity: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type TaskStatus =
        | Pending
        | InProgress
        | Done
        | Skipped of string
        | Stale

    type WorkTask =
        { Id: TaskId
          Title: string
          Status: TaskStatus
          Owner: string
          Dependencies: TaskId list
          Requirements: RequirementId list
          Decisions: DecisionId list
          SourceIds: string list
          RequiredSkills: string list
          RequiredEvidence: EvidenceId list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type TaskFacts =
        { FrontMatter: TaskFrontMatter
          SourceSnapshots: TaskSourceSnapshot list
          Tasks: WorkTask list
          AcceptedDeferrals: string list
          Findings: TaskGraphFinding list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleTaskCount: int
          Diagnostics: Diagnostic list }

    /// The authored task record as one shared field list (FS.GG.SDD#260) — drives both the reader
    /// (`parseTaskFacts`) and the renderer (`TaskGraphAuthoring`). `id` is framed by the renderer and
    /// read by the semantic layer, so it is not a field here.
    module TaskCodec =
        val taskSeed: WorkTask
        val taskFields: ArtifactCodec.FieldCodec<WorkTask> list

    val parseTaskFacts: snapshot: FileSnapshot -> Result<TaskFacts, Diagnostic list>
    val parseTasks: snapshot: FileSnapshot -> Result<WorkTask list, Diagnostic list>
