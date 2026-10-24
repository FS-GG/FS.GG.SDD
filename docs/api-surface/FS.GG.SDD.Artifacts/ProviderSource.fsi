namespace FS.GG.SDD.Artifacts

/// Source declarations identify untrusted data. No parser or digest grants execution authority.
module ProviderSource =
    type Origin =
        | BuiltIn
        | Workspace
        | Explicit

    type Locator =
        | LocalDirectory of absolutePath: string
        | HttpsEnvelope of uri: string * revision: string * envelopeSha256: string

    type Reference =
        {
            Id: string
            Origin: Origin
            Locator: Locator
            ManifestSha256: string
        }

    type SourceSet = { Schema: string; Sources: Reference list }

    type Member = { Path: string; Bytes: int64; Sha256: string }
    type Archive = { TemplateSource: string; Path: string }

    type Manifest =
        {
            Schema: string
            Id: string
            Revision: string
            CatalogPath: string
            Members: Member list
            Archives: Archive list
        }

    /// Caller-selected acquisition limits can narrow, never widen, the hard ceilings.
    type Policy =
        {
            Schema: string
            AllowedOrigins: string list
            MaximumRedirects: int
            AcquisitionSeconds: int
            MaximumTransferBytes: int64
            MaximumExpandedBytes: int64
        }

    type Acquisition =
        | LocalCopy
        | HttpsFetch
        | CacheRead

    type NetworkHop = { Uri: string; Address: string; StatusCode: int }

    /// Observed source identity remains distinct from the existing catalog execution verdict.
    type Observation =
        {
            Schema: string
            SourceSetSha256: string
            AcquisitionPolicySha256: string
            Source: Reference
            Revision: string
            Acquisition: Acquisition
            ResolvedUri: string option
            Redirects: string list
            NetworkHops: NetworkHop list
            CatalogSha256: string
            TemplateSource: string
            ArchiveSha256: string
        }

    type Limits =
        {
            MaximumSources: int
            MaximumSetBytes: int
            MaximumManifestBytes: int
            MaximumPolicyBytes: int
            MaximumReceiptBytes: int
            MaximumMembers: int
            MaximumMemberBytes: int64
            MaximumEnvelopeBytes: int64
            MaximumExpandedBytes: int64
            MaximumCatalogBytes: int
            MaximumPathBytes: int
            MaximumPathDepth: int
            MaximumAcquisitionSeconds: int
            CleanupSeconds: int
            MaximumPrivateBytes: int64
        }

    val limits: Limits
    val sha256: bytes: byte array -> string
    val validateSet: value: SourceSet -> Fsgg.ProviderCatalog.Diagnostic list
    val validateManifest: value: Manifest -> Fsgg.ProviderCatalog.Diagnostic list
    val validatePolicy: value: Policy -> Fsgg.ProviderCatalog.Diagnostic list
    val validateObservation: value: Observation -> Fsgg.ProviderCatalog.Diagnostic list

    /// Closed JSON parsing verifies the expected raw digest before semantic interpretation.
    val parseSet: expectedSha256: string -> bytes: byte array -> Result<SourceSet, Fsgg.ProviderCatalog.Diagnostic list>
    val parseManifest: expectedSha256: string -> bytes: byte array -> Result<Manifest, Fsgg.ProviderCatalog.Diagnostic list>
    val parsePolicy: expectedSha256: string -> bytes: byte array -> Result<Policy, Fsgg.ProviderCatalog.Diagnostic list>
    /// Deterministic authoring helpers emit declarations only, never trust or observed evidence.
    val serializeSet: value: SourceSet -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
    val serializeManifest: value: Manifest -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
    val serializePolicy: value: Policy -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
    val parseObservation: text: string -> Result<Observation, Fsgg.ProviderCatalog.Diagnostic list>
    val serializeObservation: value: Observation -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>

    /// Exact complete population check; this pure check does not establish filesystem custody.
    val verifyMembers:
        manifest: Manifest -> members: (string * byte array) list -> Result<unit, Fsgg.ProviderCatalog.Diagnostic list>

    /// Reject collisions across the whole selected set before returning its deterministic order.
    val orderCatalogs:
        sources: (Reference * Fsgg.ProviderCatalog.Catalog) list ->
            Result<(Reference * Fsgg.ProviderCatalog.Catalog) list, Fsgg.ProviderCatalog.Diagnostic list>

    val cacheKey: source: Reference -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
    val validateUri: policy: Policy -> uri: string -> Result<System.Uri, Fsgg.ProviderCatalog.Diagnostic list>
    val isPublicAddress: address: System.Net.IPAddress -> bool
    val validateRedirect:
        policy: Policy -> original: System.Uri -> followed: int -> target: string ->
            Result<System.Uri, Fsgg.ProviderCatalog.Diagnostic list>
