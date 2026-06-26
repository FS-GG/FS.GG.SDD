namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel

module Serialization =
    val normalizeSnapshotsToWorkModel: snapshots: FileSnapshot list -> workId: string -> WorkModel
    val generateWorkModel: request: WorkModelGenerationRequest -> WorkModelGenerationResult
    val serializeWorkModel: model: WorkModel -> string
    val checkGeneratedWorkModelCurrency:
        snapshots: FileSnapshot list -> workId: string -> generatorVersion: GeneratorVersion -> Diagnostics.Diagnostic list
    val diagnosticIds: model: WorkModel -> string list
