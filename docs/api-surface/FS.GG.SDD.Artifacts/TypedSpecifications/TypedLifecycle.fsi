namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// Stable lifecycle lanes understood by provider provenance.
type LifecycleLane =
    | NoLifecycle
    | StandardSdd
    | TypedSdd
    | LegacySpecKit

type TypedAuthorityManifest =
    { SchemaVersion: int
      Lifecycle: string
      Backend: string
      CompilerIdentity: string
      PackageIdentity: string
      ExtensionIdentity: string
      CanonicalPath: string
      CanonicalSha256: string
      NormalizedPath: string
      NormalizedSha256: string
      MarkdownPath: string
      MarkdownSha256: string
      AuthoringAgent: string
      AuthoringSession: string
      RollbackSourceSha256: string option }

type TypedLifecycleDiagnostic =
    { Id: string
      Message: string
      Correction: string }

[<RequireQualifiedAccess>]
module LifecycleLane =
    val resolve: value: string option -> Result<LifecycleLane, TypedLifecycleDiagnostic>
    val value: lane: LifecycleLane -> string
    val backend: lane: LifecycleLane -> string

[<RequireQualifiedAccess>]
module TypedAuthorityManifest =
    val path: workId: string -> string
    val sha256: bytes: byte array -> string
    val serialize: manifest: TypedAuthorityManifest -> string
    val deserialize: text: string -> Result<TypedAuthorityManifest, TypedLifecycleDiagnostic>

    /// Projects a typed requirements model into the shared Standard SDD stage grammar.
    val markdownProjection:
        workId: string ->
        model: SpecificationModel<RequirementsExtension> ->
            Result<string, SpecificationDiagnostic list>

    val validate:
        expectedPackageIdentity: string ->
        compilerAvailable: bool ->
        canonicalBytes: byte array option ->
        normalizedBytes: byte array option ->
        markdownBytes: byte array option ->
        manifest: TypedAuthorityManifest ->
            TypedLifecycleDiagnostic list

    /// Proves both projections are the deterministic outputs of the model embedded in canonical F#.
    val validateDerivation:
        canonicalBytes: byte array ->
        normalizedBytes: byte array ->
        markdownBytes: byte array ->
            TypedLifecycleDiagnostic list
