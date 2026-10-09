namespace FS.GG.SDD.Commands

/// One whole-request join into the selected actual Config runtime; declarations confer no authority.
module ProviderCapabilityAdmission =
    /// Convert original descriptor declarations under independent caller policy, with no fallbacks.
    val request:
        policy: CatalogScaffoldPolicy.Policy ->
        selectedPlatform: string ->
        descriptor: Fsgg.ProviderCatalog.Descriptor ->
            Result<FS.GG.Governance.Config.CapabilityBindings.ResolutionRequest, Fsgg.ProviderCatalog.Diagnostic list>
    /// Recompute the complete request through actual Config.resolve; never accepts a supplied ResolvedSet.
    val resolve:
        policy: CatalogScaffoldPolicy.Policy ->
        selectedPlatform: string ->
        descriptor: Fsgg.ProviderCatalog.Descriptor ->
            Result<FS.GG.Governance.Config.CapabilityBindings.ResolvedSet, Fsgg.ProviderCatalog.Diagnostic list>
