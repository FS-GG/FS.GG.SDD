namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.LifecycleArtifacts
open FS.GG.SDD.Artifacts.WorkModel

module Serialization =
    val normalizeSnapshotsToWorkModel: snapshots: FileSnapshot list -> workId: string -> WorkModel
    val serializeWorkModel: model: WorkModel -> string
    val diagnosticIds: model: WorkModel -> string list
