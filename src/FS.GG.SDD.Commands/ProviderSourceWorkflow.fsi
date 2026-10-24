namespace FS.GG.SDD.Commands

/// Pure acquisition progression. Data declarations and emitted effects grant no runtime authority.
module ProviderSourceWorkflow =
    type Request =
        {
            Sources: FS.GG.SDD.Artifacts.ProviderSource.SourceSet
            SourceSetSha256: string
            Policy: FS.GG.SDD.Artifacts.ProviderSource.Policy
            CacheRoot: string
            Offline: bool
        }

    type Phase =
        | Preparing
        | ReadingCache
        | Acquiring
        | Validating
        | PublishingCache
        | Retiring
        | Terminal

    type Outcome =
        | Pending
        | Prepared
        | Refused
        | CleanupUnknown

    type Model

    type Msg =
        | CacheObserved of complete: bool
        | AcquisitionCompleted
        | ValidationCompleted
        | CachePublished
        | FailureObserved of Fsgg.ProviderCatalog.Diagnostic list
        | RetirementObserved of settled: bool

    type Effect =
        | ReadExactCache
        | AcquireSelectedData
        | VerifyClosedSourceSet
        | PublishVerifiedCache
        | RetireSourceOperation

    /// Invalid declarations refuse without effects. Offline cache absence never requests acquisition.
    val init: request: Request -> Model * Effect list
    val update: message: Msg -> model: Model -> Model * Effect list
    val phase: model: Model -> Phase
    val outcome: model: Model -> Outcome
    val diagnostics: model: Model -> Fsgg.ProviderCatalog.Diagnostic list
