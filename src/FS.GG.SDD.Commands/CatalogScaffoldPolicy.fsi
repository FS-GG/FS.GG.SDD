namespace FS.GG.SDD.Commands

/// Independently selected host policy; parsing/digests establish identity, never trust or execution.
module CatalogScaffoldPolicy =
    type Identity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type EnvironmentBinding =
        {
            Id: string
            Environment: FS.GG.Governance.Config.Model.EnvironmentClass
        }

    type EvidenceMapping =
        {
            Format: string
            Id: string
            Version: string
        }

    type ToolProbe =
        {
            Id: string
            Executable: string
            Arguments: string list
            VersionPrefix: string
            VersionSuffix: string
            MaximumOutputCharacters: int
        }

    type ArchiveAssociation =
        {
            TemplateSource: string
            PackageId: string
            PackageVersion: string
        }

    type Policy =
        {
            Identity: Identity
            EvidenceMapIdentity: Identity
            RequiredCapabilityIds: string list
            SemanticOnlyCapabilityIds: string list
            KnownPlatforms: string list
            EnvironmentBindings: EnvironmentBinding list
            SupportedEvidenceFormats: FS.GG.Governance.Config.CapabilityBindings.EvidenceFormat list
            EvidenceMappings: EvidenceMapping list
            MaximumPreflightSeconds: int
            MaximumScaffoldSeconds: int
            ToolProbes: ToolProbe list
            Archives: ArchiveAssociation list
        }

    val parse: expectedRawDigest: string -> bytes: byte array -> Result<Policy, Fsgg.ProviderCatalog.Diagnostic list>
