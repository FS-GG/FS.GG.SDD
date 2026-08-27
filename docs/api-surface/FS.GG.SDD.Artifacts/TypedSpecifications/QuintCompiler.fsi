namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Stable contract metadata supplied by the integration domain; catalogue and effects always come from Quint.
type QuintContractMetadata =
    { Specification: string
      Relationships: QuintRelationship list
      VerificationProfiles: QuintVerificationProfile list
      Bounds: QuintFiniteBound list
      Impacts: QuintImpact list
      Compatibility: QuintCompatibility list
      Digests: QuintSemanticDigest list }

/// Complete pure observation offered after a host executes already-resolved local tools.
type QuintObservedCompilation =
    { ModuleName: string
      Toolchain: QuintToolchainManifest
      Cache: QuintCacheObservation list
      ProcessRequests: QuintProcessRequest list
      Endpoint: QuintEndpointState
      ProcessObservations: QuintProcessObservation list
      Source: QuintMarkdownSource
      FenceManifest: QuintFenceManifest
      Extraction: QuintExtractionObservation
      SourceMap: QuintSourceMap
      TypedEffect: QuintTypedEffectObservation
      Metadata: QuintContractMetadata }

/// Complete pure observation for the explicit general profile.
type QuintGeneralObservedCompilation =
    { ModuleName: string
      Toolchain: QuintToolchainManifest
      Cache: QuintCacheObservation list
      ProcessRequests: QuintProcessRequest list
      Endpoint: QuintEndpointState
      ProcessObservations: QuintProcessObservation list
      Source: QuintMarkdownSource
      FenceManifest: QuintFenceManifest
      Extraction: QuintExtractionObservation
      SourceMap: QuintSourceMap
      TypedEffect: QuintGeneralTypedEffectObservation
      Metadata: QuintContractMetadata }

/// Content-addressed receipt for one accepted observed compilation.
type QuintCompilationReceipt =
    { Schema: string
      SourceSha256: string
      FenceManifestSha256: string
      GeneratedModulesSha256: string
      ToolchainSha256: string
      TypedEffectSha256: string
      ContractSha256: string
      CompilationFingerprint: string
      ProcessSteps: string list }

/// Stable outputs from the pure compiler composition.
type QuintCompilationOutput =
    { Plan: QuintCompilationPlan
      Contract: QuintCompiledContract
      CanonicalContract: string
      CompilationFingerprint: string
      Bindings: QuintGeneratedBindings
      Receipt: QuintCompilationReceipt
      CanonicalReceipt: string }

/// Stable profile-2 outputs from the pure compiler composition.
type QuintGeneralCompilationOutput =
    { Plan: QuintCompilationPlan
      Contract: QuintCompiledContractV2
      CanonicalContract: string
      CompilationFingerprint: string
      Bindings: QuintGeneratedBindings
      Receipt: QuintCompilationReceipt
      CanonicalReceipt: string }

[<RequireQualifiedAccess>]
module QuintCompiler =
    /// Stable schema identity for observed-compilation receipts.
    val receiptSchema: string

    /// Stable receipt schema for profile-2 observed compilations.
    val generalReceiptSchema: string

    /// Validate and compose tool, source, profile, contract, binding, and receipt boundaries without performing IO.
    val compileObserved: input: QuintObservedCompilation -> Result<QuintCompilationOutput, SpecificationDiagnostic list>

    /// Compose the general profile through the same source/toolchain effect observations.
    val compileGeneralObserved:
        input: QuintGeneralObservedCompilation -> Result<QuintGeneralCompilationOutput, SpecificationDiagnostic list>

    /// Emit deterministic receipt JSON with one trailing newline.
    val encodeReceipt: receipt: QuintCompilationReceipt -> string
