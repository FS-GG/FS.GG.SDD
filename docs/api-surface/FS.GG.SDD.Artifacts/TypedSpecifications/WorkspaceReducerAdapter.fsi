namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Consumer adapter for the production SDD workspace reducer and its canonical Quint model.
[<RequireQualifiedAccess>]
module WorkspaceReducerAdapter =
    /// Project a real reducer run against a model-produced coherent-acceptance ITF trace.
    val replayCoherent:
        accepted: WorkspaceModel ->
        itfBytes: byte array ->
        environment: QuintReplayEnvironment ->
            Result<QuintReplayResult, QuintReplayDiagnostic list>

    /// Attempt a stale proposal against the real reducer after another accepted revision.
    val checkStaleBase: accepted: WorkspaceModel -> Result<unit, QuintReplayDiagnostic list>
