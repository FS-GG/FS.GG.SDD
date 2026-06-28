namespace Fsgg

module Schemas =

    type SchemaOwner =
        | Sdd
        | Governance

    type SchemaContractEntry =
        { Name: string
          SchemaVersion: int
          ContractVersion: string option
          Owner: SchemaOwner }

    type GeneratorRef =
        { Id: string
          Version: string }

    type ProviderParameterEntry =
        { Key: string
          Required: bool
          Default: string option }

    type SchemaSourceIdentity =
        { Path: string
          DigestAlgorithm: string
          DigestValue: string
          SchemaVersion: int option }

    type SchemaDiagnostic =
        { Id: string
          Severity: string
          Message: string
          Correction: string
          RelatedIds: string list }

    type ProviderRegistryEntry =
        { Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterEntry list }

    type ProvidersSchema =
        { SchemaVersion: int
          Providers: ProviderRegistryEntry list }

    type ProjectSchema =
        { SchemaVersion: int
          ProjectId: string
          DefaultWorkRoot: string
          SddConfigPath: string
          AgentsConfigPath: string
          GovernancePolicyPath: string option
          GovernanceCapabilitiesPath: string option
          GovernanceToolingPath: string option }

    type SddSchema =
        { SchemaVersion: int
          Stages: string list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    type AgentGuidanceTargetEntry =
        { Id: string
          GuidancePath: string
          GeneratedRoot: string }

    type AgentsSchema =
        { SchemaVersion: int
          Targets: AgentGuidanceTargetEntry list
          WorkModelPath: string
          GeneratedGuidanceIsAuthority: bool
          RequireEquivalentClaudeAndCodexBehavior: bool }

    type ScaffoldProducedPathEntry =
        { Path: string
          Owner: string }

    type ScaffoldProvenanceSchema =
        { SchemaVersion: int
          Generator: GeneratorRef
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPathEntry list }

    type GovernanceHandoffEvidenceNode =
        { Id: string
          State: string
          Rationale: string option }

    type GovernanceHandoffEvidenceEdge =
        { Dependent: string
          Dependency: string }

    type GovernanceHandoffEvidence =
        { Nodes: GovernanceHandoffEvidenceNode list
          Dependencies: GovernanceHandoffEvidenceEdge list }

    type GovernanceHandoffReference =
        { Path: string
          Owner: string
          Relationship: string
          Kind: string option
          Operation: string option }

    type GovernanceHandoffConfigPresence =
        { PolicyPresent: bool
          PolicyPointer: string option
          CapabilitiesPresent: bool
          CapabilitiesPointer: string option
          ToolingPresent: bool
          ToolingPointer: string option }

    type GovernanceHandoffReadiness =
        { ShipDisposition: string
          VerificationReadiness: string
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          BlockingDiagnosticIds: string list
          PerViewState: (string * string) list }

    type GovernanceHandoffSchema =
        { SchemaVersion: int
          ContractVersion: string
          GeneratorVersion: GeneratorRef
          WorkId: string
          Sources: SchemaSourceIdentity list
          Evidence: GovernanceHandoffEvidence
          GovernedReferences: GovernanceHandoffReference list
          GovernanceConfig: GovernanceHandoffConfigPresence
          Readiness: GovernanceHandoffReadiness
          Diagnostics: SchemaDiagnostic list }

    // SOURCE: Governance published reference (TBD-link) — minimal provisional shape.
    type GovernanceSchema = { SchemaVersion: int }
    // SOURCE: Governance published reference (TBD-link) — minimal provisional shape.
    type PolicySchema = { SchemaVersion: int }
    // SOURCE: Governance published reference (TBD-link) — minimal provisional shape.
    type CapabilitiesSchema = { SchemaVersion: int }
    // SOURCE: Governance published reference (TBD-link) — minimal provisional shape.
    type ToolingSchema = { SchemaVersion: int }

    // SDD-owned constants. Each equals today's emitted value, grounded in-repo:
    //   src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs (parsers default to schemaVersion 1)
    //   src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs (provenance schema v1)
    //   src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs (handoff SchemaVersion=1, ContractVersion="1.0.0")
    let providersVersion = 1
    let projectVersion = 1
    let sddVersion = 1
    let agentsVersion = 1
    let scaffoldProvenanceVersion = 1
    let governanceHandoffVersion = 1
    let governanceHandoffContractVersion = "1.0.0"
    // Governance-owned: declared to the Governance published reference, NOT SDD-emitted.
    let governanceVersion = 1
    let policyVersion = 1
    let capabilitiesVersion = 1
    let toolingVersion = 1

    let entries: SchemaContractEntry list =
        [ { Name = "providers"; SchemaVersion = providersVersion; ContractVersion = None; Owner = Sdd }
          { Name = "project"; SchemaVersion = projectVersion; ContractVersion = None; Owner = Sdd }
          { Name = "sdd"; SchemaVersion = sddVersion; ContractVersion = None; Owner = Sdd }
          { Name = "agents"; SchemaVersion = agentsVersion; ContractVersion = None; Owner = Sdd }
          { Name = "scaffold-provenance"; SchemaVersion = scaffoldProvenanceVersion; ContractVersion = None; Owner = Sdd }
          { Name = "governance-handoff"
            SchemaVersion = governanceHandoffVersion
            ContractVersion = Some governanceHandoffContractVersion
            Owner = Sdd }
          { Name = "governance"; SchemaVersion = governanceVersion; ContractVersion = None; Owner = Governance }
          { Name = "policy"; SchemaVersion = policyVersion; ContractVersion = None; Owner = Governance }
          { Name = "capabilities"; SchemaVersion = capabilitiesVersion; ContractVersion = None; Owner = Governance }
          { Name = "tooling"; SchemaVersion = toolingVersion; ContractVersion = None; Owner = Governance } ]
