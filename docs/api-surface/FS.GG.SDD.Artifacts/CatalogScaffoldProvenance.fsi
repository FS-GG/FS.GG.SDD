namespace FS.GG.SDD.Artifacts

/// Schema-2 structure/correspondence; parsing does not establish authentic execution.
module CatalogScaffoldProvenance =
    type Identity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type ArchiveIdentity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type EvidenceFormat = { Id: string; Version: string }

    type EvidenceMapping =
        {
            Format: string
            Id: string
            Version: string
        }

    type AdmissionRequest =
        {
            ContractVersion: string
            Platform: string
            RequiredCapabilityIds: string list
            SemanticOnlyCapabilityIds: string list
            SupportedPlatforms: string list
            SupportedEnvironmentIds: string list
            SupportedFormats: EvidenceFormat list
            EvidenceMappings: EvidenceMapping list
            Descriptor: Fsgg.ProviderCatalog.Descriptor
        }

    type Budgets =
        {
            RequestedPreflightSeconds: int
            RequestedScaffoldSeconds: int
            AdmittedPreflightSeconds: int
            AdmittedScaffoldSeconds: int
        }

    type Declaration =
        {
            RawCatalogDigest: string
            CatalogId: string
            CatalogRevision: string
            CatalogDigest: string
            Descriptor: Fsgg.ProviderCatalog.Descriptor
            EffectiveParameters: (string * string) list
            RawProductName: string
            PackageIdentity: string
            CodeIdentifier: string
            Archive: ArchiveIdentity
            Admission: AdmissionRequest
            Policy: Identity
            EvidenceMap: Identity
            Budgets: Budgets
        }

    type ObservedTool =
        {
            Id: string
            Version: string
            Executable: string
        }

    type TransportIdentity = { Executable: string; Version: string }
    type RootPath = { Role: string; Path: string }

    type Invocation =
        {
            Executable: string
            Arguments: string list
            WorkingRoot: string
            WorkingDirectory: string
            EnvironmentRoots: (string * RootPath) list
            TimeoutSeconds: int
            ExitCode: int
        }

    type Observation =
        {
            Platform: string
            Tools: ObservedTool list
            ConsumedArchiveDigest: string
            Transport: TransportIdentity
            Invocations: Invocation list
            Result: string
            ProducedPaths: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath list
        }

    type CatalogScaffoldProvenanceRecord =
        {
            SchemaVersion: int
            Generator: FS.GG.SDD.Artifacts.SchemaVersion.GeneratorVersion
            Declaration: Declaration
            Observation: Observation
            Ownership: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProvenanceRecord
        }

    val parse: text: string -> Result<CatalogScaffoldProvenanceRecord, Fsgg.ProviderCatalog.Diagnostic list>
    /// Pure codec only; production mutation/writing remains outside C2.1.
    val serialize: record: CatalogScaffoldProvenanceRecord -> Result<string, Fsgg.ProviderCatalog.Diagnostic list>
