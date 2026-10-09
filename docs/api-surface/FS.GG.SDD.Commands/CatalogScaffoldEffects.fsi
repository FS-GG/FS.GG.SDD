namespace FS.GG.SDD.Commands

/// Caller-held ownership for the explicit catalog route; preparation grants no native authority.
module CatalogScaffoldEffects =
    /// First supported mode: cooperative Linux namespace and independently selected trusted tools.
    /// This mode provides neither a sandbox nor escaping-descendant containment.
    type HostMode = LocalLinux
    type HostSelection =
        { Mode: HostMode
          /// Explicit fully qualified executable selection, independently supplied by the host.
          TransportExecutable: string }
    type OwnershipStatus = NotStarted | Active | Settled | Unresolved
    /// Owns native tasks, handles and buffers from acquisition; pure Model is not their owner.
    type Operation
    type OperationObservation =
        { Ownership: OwnershipStatus
          Model: CatalogScaffoldWorkflow.Model option
          Diagnostics: Fsgg.ProviderCatalog.Diagnostic list }
    /// Validates only in-memory request/profile shape and constructs the owner before any effect.
    /// Filesystem acquisition, host sensing and child startup happen only in run.
    val prepare:
        host: HostSelection ->
        request: CatalogScaffoldWorkflow.CatalogScaffoldRequest ->
        cancellationToken: System.Threading.CancellationToken ->
        Result<Operation, Fsgg.ProviderCatalog.Diagnostic list>
    /// Consumes this operation once. Original phase ends include cleanup; unknown stays owned.
    val run: operation: Operation -> Async<CatalogScaffoldWorkflow.Model>
    /// Projects currently retained facts without resuming work or granting cleanup time.
    val observe: operation: Operation -> OperationObservation
    /// Passive event wait retains the same owner after unknown; it grants no renewed deadline.
    val waitForSettled: operation: Operation -> Async<OperationObservation>
    /// Refuses while any native task, reader or object custody remains unsettled.
    val release: operation: Operation -> Result<unit, Fsgg.ProviderCatalog.Diagnostic list>
