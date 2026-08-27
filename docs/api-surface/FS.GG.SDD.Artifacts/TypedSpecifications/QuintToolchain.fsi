namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// The kind of content-addressed object required by the hermetic Quint toolchain.
type QuintCacheObjectKind =
    | File
    | Tree
    | Closure
    | Source

/// One immutable object in the offline tool cache.
type QuintCacheRequirement =
    { Id: string
      Kind: QuintCacheObjectKind
      Sha256: string
      Bytes: int64 option }

/// One pinned component of the Q1-qualified toolchain.
type QuintToolComponent =
    { Id: string
      Version: string
      Source: string
      Objects: QuintCacheRequirement list }

/// Optional, content-addressed guidance. Guidance is never compiler authority.
type QuintGuidanceIdentity =
    { Source: string
      License: string
      LicenseSha256: string
      TrackedTreeSha256: string }

/// The complete, language-neutral identity of a compiler toolchain.
type QuintToolchainManifest =
    { Schema: string
      Profile: string
      Platform: string
      Components: QuintToolComponent list
      Guidance: QuintGuidanceIdentity option }

/// What the effect edge learned while reading one declared cache object.
type QuintCacheObjectState =
    | Absent
    | Unreadable of detail: string
    | Present of sha256: string * bytes: int64 option * complete: bool

/// A cache observation is keyed by the requirement id, never by a host path.
type QuintCacheObservation =
    { Id: string
      Kind: QuintCacheObjectKind
      State: QuintCacheObjectState }

/// Availability of a dedicated local server endpoint before tool execution.
type QuintEndpointState =
    | Available
    | Occupied of detail: string

/// A deterministic local process request. It cannot express acquisition or a network URI.
type QuintProcessRequest =
    { StepId: string
      ExecutableObjectId: string
      Arguments: string list
      Environment: (string * string) list
      WorkingDirectory: string }

/// A pure compilation plan over already-resolved cache objects.
type QuintCompilationPlan =
    { ManifestSha256: string
      RequiredObjects: QuintCacheRequirement list
      Requests: QuintProcessRequest list }

/// Result observed for one planned process.
type QuintProcessOutcome =
    | Succeeded
    | Failed of exitCode: int * detail: string

/// Effect-edge receipt for one planned process.
type QuintProcessObservation =
    { StepId: string
      Outcome: QuintProcessOutcome }

[<RequireQualifiedAccess>]
module QuintToolchain =
    /// Stable schema identity for the Q2 toolchain manifest.
    val schema: string

    /// Exact Q1-qualified profile identity.
    val profile: string

    /// Explicit profile identity for the same exact tool closure under the general adapter.
    val generalProfile: string

    /// Exact Q1-qualified Linux amd64 manifest, including optional guidance identity.
    val q1: QuintToolchainManifest

    /// Exact Linux amd64 tool closure explicitly bound to fsgg-quint-profile/2.
    val general: QuintToolchainManifest

    /// Validate an offered manifest against the exact Q1-qualified identity.
    val validateManifest: manifest: QuintToolchainManifest -> SpecificationDiagnostic list

    /// Emit deterministic UTF-8 canonical JSON for a manifest.
    val encodeCanonical: manifest: QuintToolchainManifest -> byte array

    /// Return lowercase SHA-256 over canonical manifest bytes.
    val fingerprint: manifest: QuintToolchainManifest -> string

    /// Validate caller-supplied offline cache observations against a manifest.
    val validateCache:
        manifest: QuintToolchainManifest -> observations: QuintCacheObservation list -> SpecificationDiagnostic list

    /// Construct a pure local execution plan after manifest and cache validation.
    val plan:
        manifest: QuintToolchainManifest ->
        observations: QuintCacheObservation list ->
        requests: QuintProcessRequest list ->
            Result<QuintCompilationPlan, SpecificationDiagnostic list>

    /// Validate endpoint availability and bind every process observation to one planned step.
    val validateExecution:
        plan: QuintCompilationPlan ->
        endpoint: QuintEndpointState ->
        observations: QuintProcessObservation list ->
            SpecificationDiagnostic list
