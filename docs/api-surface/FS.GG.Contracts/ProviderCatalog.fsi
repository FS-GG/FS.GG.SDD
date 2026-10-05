namespace Fsgg

/// Additive schema-2 catalog data; no lifecycle or invocation authority is implied.
module ProviderCatalog =
    type ParameterKind =
        | String
        | Enum
        | ExactVersion

    type Validation =
        {
            NonEmpty: bool
            MinLength: int
            MaxLength: int
            AllowedValues: string list
        }

    type Parameter =
        {
            Key: string
            Kind: ParameterKind
            Required: bool
            Prompt: string
            Help: string
            Default: string option
            Values: string list
            Validation: Validation
        }

    type IdentityBindings =
        {
            RawName: string
            PackageIdentity: string
            CodeIdentifier: string
        }

    type ToolRequirement =
        {
            Id: string
            Version: string
            Platforms: string list
        }

    type EvidenceDeclaration =
        {
            Id: string
            Format: string
            Path: string
            Required: bool
        }

    /// Limits are declared requirements, not observed or enforced execution facts.
    type CommandLimits =
        {
            WorkingDirectory: string
            TimeoutSeconds: int
            CostClass: string
            EnvironmentIds: string list
        }

    type CapabilityBinding =
        | SemanticOnly
        | Command of command: Provider.DeclaredCommand * limits: CommandLimits

    type CapabilityDeclaration =
        {
            Id: string
            Required: bool
            Platforms: string list
            ToolIds: string list
            EvidenceIds: string list
            Binding: CapabilityBinding
        }

    type Descriptor =
        {
            Id: string
            DisplayName: string
            Help: string
            Language: string
            ProductShape: string
            DescriptorId: string
            DescriptorRevision: string
            DescriptorDigest: string
            ContractVersion: string
            TemplateSource: string
            TemplateId: string
            Platforms: string list
            Parameters: Parameter list
            Identities: IdentityBindings
            Tools: ToolRequirement list
            Capabilities: CapabilityDeclaration list
            Evidence: EvidenceDeclaration list
            Skills: string list
        }

    type Catalog =
        {
            SchemaVersion: int
            Id: string
            Revision: string
            Digest: string
            Providers: Descriptor list
        }

    type Diagnostic =
        {
            Code: string
            Path: string
            Message: string
        }

    /// Exact declared pins and metadata, with no successful scaffold/observed-tool claim.
    type PreparedConfiguration =
        {
            CatalogId: string
            CatalogRevision: string
            CatalogDigest: string
            Descriptor: Descriptor
            EffectiveParameters: (string * string) list
            RawProductName: string
            PackageIdentity: string
            CodeIdentifier: string
        }

    /// Closed, ecosystem-neutral lexical version literal; no ranges or floating selectors.
    val isExactVersion: value: string -> bool
    /// Validate shape, pins, defaults and references, returning stable ordered diagnostics.
    val validate: catalog: Catalog -> Diagnostic list

    /// Defaults precede overrides; any invalid declaration/input refuses the entire result.
    val resolve:
        catalog: Catalog ->
        providerId: string ->
        overrides: (string * string) list ->
            Result<PreparedConfiguration, Diagnostic list>
