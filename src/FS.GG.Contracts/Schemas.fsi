namespace Fsgg

/// One typed source of truth for every `.fsgg` schema shape and its version
/// constant (FR-004/005). SDD-owned version constants equal the value SDD emits
/// today; Governance-owned schemas are declared to their published reference.
/// BCL-only: FSharp.Core types exclusively; no serialization, no I/O.
module Schemas =

    /// Which repo owns (emits) a schema. SDD-owned versions must equal today's
    /// emitted values; Governance-owned shapes are declared, not emitted by SDD.
    type SchemaOwner =
        | Sdd
        | Governance

    /// The unit of "one fact in one place": a schema's contract name paired with
    /// its version constant(s) and owner.
    type SchemaContractEntry =
        { Name: string
          SchemaVersion: int
          ContractVersion: string option
          Owner: SchemaOwner }

    // --- Shared generic mirrors of SDD-specific nested shapes (BCL-only). ---

    /// Generic mirror of an SDD `GeneratorVersion` ({ Id; Version }).
    type GeneratorRef =
        { Id: string
          Version: string }

    /// Generic mirror of an SDD provider `--param` declaration.
    type ProviderParameterEntry =
        { Key: string
          Required: bool
          Default: string option }

    /// Generic mirror of a contributing-source identity in a generated view.
    type SchemaSourceIdentity =
        { Path: string
          DigestAlgorithm: string
          DigestValue: string
          SchemaVersion: int option }

    /// Generic mirror of an SDD diagnostic carried in a generated view.
    type SchemaDiagnostic =
        { Id: string
          Severity: string
          Message: string
          Correction: string
          RelatedIds: string list }

    // --- SDD-owned typed schema records (mirror today's in-repo field sets). ---

    /// One declared template provider in `.fsgg/providers.yml`.
    type ProviderRegistryEntry =
        { Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterEntry list }

    /// `.fsgg/providers.yml` — the author-/provider-owned template-provider registry.
    type ProvidersSchema =
        { SchemaVersion: int
          Providers: ProviderRegistryEntry list }

    /// `.fsgg/project.yml` — mirror of `ProjectLifecycleConfig`.
    type ProjectSchema =
        { SchemaVersion: int
          ProjectId: string
          DefaultWorkRoot: string
          SddConfigPath: string
          AgentsConfigPath: string
          GovernancePolicyPath: string option
          GovernanceCapabilitiesPath: string option
          GovernanceToolingPath: string option }

    /// `.fsgg/sdd.yml` — mirror of `SddLifecyclePolicy`. `Stages` is the generic
    /// lifecycle-stage token list (SDD's `LifecycleStage` DU rendered as strings).
    type SddSchema =
        { SchemaVersion: int
          Stages: string list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    /// One agent-guidance target in `.fsgg/agents.yml`.
    type AgentGuidanceTargetEntry =
        { Id: string
          GuidancePath: string
          GeneratedRoot: string }

    /// `.fsgg/agents.yml` — mirror of `AgentGuidanceConfig`.
    type AgentsSchema =
        { SchemaVersion: int
          Targets: AgentGuidanceTargetEntry list
          WorkModelPath: string
          GeneratedGuidanceIsAuthority: bool
          RequireEquivalentClaudeAndCodexBehavior: bool }

    /// One produced path recorded in `.fsgg/scaffold-provenance.json`. `Owner` is
    /// the generic owner token (SDD's `ArtifactOwner` rendered as a string).
    type ScaffoldProducedPathEntry =
        { Path: string
          Owner: string }

    /// `.fsgg/scaffold-provenance.json` — mirror of `ScaffoldProvenanceRecord`.
    type ScaffoldProvenanceSchema =
        { SchemaVersion: int
          Generator: GeneratorRef
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPathEntry list }

    /// A declared evidence node in the governance-handoff projection.
    type GovernanceHandoffEvidenceNode =
        { Id: string
          State: string
          Rationale: string option }

    /// A declared evidence dependency edge in the governance-handoff projection.
    type GovernanceHandoffEvidenceEdge =
        { Dependent: string
          Dependency: string }

    /// The evidence projection embedded in the governance-handoff envelope.
    type GovernanceHandoffEvidence =
        { Nodes: GovernanceHandoffEvidenceNode list
          Dependencies: GovernanceHandoffEvidenceEdge list }

    /// A governed reference declared to the Governance boundary.
    type GovernanceHandoffReference =
        { Path: string
          Owner: string
          Relationship: string
          Kind: string option
          Operation: string option }

    /// `.fsgg` Governance-config presence flags + optional pointers.
    type GovernanceHandoffConfigPresence =
        { PolicyPresent: bool
          PolicyPointer: string option
          CapabilitiesPresent: bool
          CapabilitiesPointer: string option
          ToolingPresent: bool
          ToolingPointer: string option }

    /// Merge-boundary readiness facts mirrored into the handoff (advisory only).
    type GovernanceHandoffReadiness =
        { ShipDisposition: string
          VerificationReadiness: string
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          BlockingDiagnosticIds: string list
          PerViewState: (string * string) list }

    /// `readiness/<id>/governance-handoff.json` — mirror of `GovernanceHandoff`.
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

    // --- Governance-owned schema records (declared to the published reference). ---
    // Minimal, explicitly-provisional placeholder shapes; the full field set is
    // deferred to the Governance counterpart item. Not invented field sets.

    /// `governance` schema — declared to the Governance published reference.
    type GovernanceSchema =
        { SchemaVersion: int }

    /// `policy` schema — declared to the Governance published reference.
    type PolicySchema =
        { SchemaVersion: int }

    /// `capabilities` schema — declared to the Governance published reference.
    type CapabilitiesSchema =
        { SchemaVersion: int }

    /// `tooling` schema — declared to the Governance published reference.
    type ToolingSchema =
        { SchemaVersion: int }

    // --- Named version constants (FR-005). One authoritative value each. ---
    val providersVersion: int
    val projectVersion: int
    val sddVersion: int
    val agentsVersion: int
    val scaffoldProvenanceVersion: int
    val governanceHandoffVersion: int
    val governanceHandoffContractVersion: string
    val governanceVersion: int
    val policyVersion: int
    val capabilitiesVersion: int
    val toolingVersion: int

    /// All 10 named schemas, for the "every schema represented?" check (SC-001).
    val entries: SchemaContractEntry list
