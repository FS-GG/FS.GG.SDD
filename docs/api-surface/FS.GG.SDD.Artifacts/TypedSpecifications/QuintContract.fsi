namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Closed relationship vocabulary carried by compiled-contract v1.
type QuintRelationshipKind =
    | Requires
    | VerifiedBy
    | ImplementedBy
    | Reads
    | Writes

type QuintRelationship =
    { FromId: string
      Kind: QuintRelationshipKind
      ToId: string }

/// One named, finite verification profile. Bounds are references to declared bound ids.
type QuintVerificationProfile =
    { Id: string
      Kind: string
      SubjectIds: string list
      BoundIds: string list }

type QuintFiniteBound =
    { Id: string
      Minimum: int64
      Maximum: int64 }

/// Stable integration impact metadata; it cannot carry executable expressions.
type QuintImpact =
    { SubjectId: string
      Category: string
      Detail: string }

/// Stable compatibility metadata for one named integration surface.
type QuintCompatibility =
    { Surface: string
      Requirement: string
      Detail: string }

type QuintSemanticDigest = { Name: string; Sha256: string }

/// Language-neutral compiled-contract v1. Raw Quint IR and arbitrary expressions are unrepresentable.
type QuintCompiledContract =
    { Schema: string
      Profile: string
      Specification: string
      Catalogue: QuintCatalogueEntry list
      ActionEffects: QuintActionEffect list
      Relationships: QuintRelationship list
      VerificationProfiles: QuintVerificationProfile list
      Bounds: QuintFiniteBound list
      Impacts: QuintImpact list
      Compatibility: QuintCompatibility list
      Digests: QuintSemanticDigest list }

/// All compilation inputs whose meaning is bound by a semantic fingerprint.
type QuintFingerprintInputs =
    { SourceSha256: string
      FenceManifestSha256: string
      GeneratedModulesSha256: string
      ToolchainSha256: string
      Contract: QuintCompiledContract }

type QuintContractDiagnostic =
    { Code: string
      Path: string
      Message: string
      Correction: string }

type QuintContractChange =
    { Path: string
      BeforeSha256: string
      AfterSha256: string }

type QuintContractDiff =
    | Equivalent
    | Changed of QuintContractChange list

[<RequireQualifiedAccess>]
module QuintContract =
    /// The only compiled-contract schema emitted and accepted by this Q2 implementation.
    val schema: string

    /// Validate references, uniqueness, finite bounds, digests, and the exact schema/profile.
    val validate: contract: QuintCompiledContract -> QuintContractDiagnostic list

    /// Emit deterministic UTF-8 canonical JSON (no insignificant whitespace, one trailing newline).
    val serializeCanonical: contract: QuintCompiledContract -> Result<string, QuintContractDiagnostic list>

    /// Decode only compiled-contract v1; unknown, duplicate, and expression-bearing fields fail closed.
    val deserialize: text: string -> Result<QuintCompiledContract, QuintContractDiagnostic list>

    /// Return lowercase SHA-256 over the complete semantic compilation input frame.
    val fingerprint: inputs: QuintFingerprintInputs -> Result<string, QuintContractDiagnostic list>

    /// Compare stable integration meaning; incompatible schema/profile pairs are refused.
    val semanticDiff:
        before: QuintCompiledContract ->
        after: QuintCompiledContract ->
            Result<QuintContractDiff, QuintContractDiagnostic list>
