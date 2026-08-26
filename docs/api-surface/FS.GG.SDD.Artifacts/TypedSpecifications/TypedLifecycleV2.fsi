namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// One content-addressed artifact owned by a manifest-v2 Quint authority.
type QuintAuthorityArtifact =
    { Id: string
      Path: string
      Sha256: string }

/// Additive manifest-v2 authority for the explicit Quint lifecycle backend.
type QuintAuthorityManifest =
    { SchemaVersion: int
      Lifecycle: string
      Backend: string
      ProfileIdentity: string
      ToolchainIdentity: string
      PackageIdentity: string
      Artifacts: QuintAuthorityArtifact list
      AuthoringAgent: string
      AuthoringSession: string
      RollbackManifestPath: string option
      RollbackManifestSha256: string option }

/// Distinct effect-edge states for one declared authority artifact.
type QuintAuthorityArtifactState =
    | Missing
    | Unreadable of detail: string
    | Present of bytes: byte array

/// One path-bound artifact observation; duplicate paths fail closed.
type QuintAuthorityArtifactObservation =
    { Path: string
      State: QuintAuthorityArtifactState }

/// Explicitly decoded authority. File presence never selects a backend.
type TypedAuthority =
    | FsharpSpecificationV1 of TypedAuthorityManifest
    | QuintSpecificationV1 of QuintAuthorityManifest

/// Increasing deterministic verification rungs for Quint-backed authorities.
type QuintVerificationRung =
    | ProseOnly
    | StructuralTypecheck
    | TestAndSimulation
    | ModelCheck
    | FullCorpus

/// Stable selector input from changed authority surfaces and compiled-contract impacts.
type QuintVerificationSelection =
    { ChangedPaths: string list
      Impacts: QuintImpact list }

[<RequireQualifiedAccess>]
module TypedAuthority =
    /// Strictly decode manifest v1 or v2 by schemaVersion and declared backend.
    val deserialize: text: string -> Result<TypedAuthority, TypedLifecycleDiagnostic>

    /// Emit deterministic manifest-v2 JSON with one trailing newline.
    val serializeQuintV2: manifest: QuintAuthorityManifest -> string

    /// Validate manifest-v2 identity, required artifacts, exact bytes, and rollback pairing.
    val validateQuintV2:
        expectedPackageIdentity: string ->
        observations: QuintAuthorityArtifactObservation list ->
        manifest: QuintAuthorityManifest ->
            TypedLifecycleDiagnostic list

[<RequireQualifiedAccess>]
module QuintVerificationSelector =
    /// Select the strongest required rung; unknown categories and selector-sensitive paths fail safe to FullCorpus.
    val select: selection: QuintVerificationSelection -> QuintVerificationRung

    /// Stable kebab-case projection used by CLI and CI reports.
    val value: rung: QuintVerificationRung -> string
