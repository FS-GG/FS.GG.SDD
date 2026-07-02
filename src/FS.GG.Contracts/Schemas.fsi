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
    /// `Sha256` is the additive (contract 1.1.0, ADR-0014) per-path content digest;
    /// `None` when no digest was recorded — a `1.0.0` document has none.
    type ScaffoldProducedPathEntry =
        { Path: string
          Owner: string
          Sha256: string option }

    /// `.fsgg/scaffold-provenance.json` — mirror of `ScaffoldProvenanceRecord`.
    type ScaffoldProvenanceSchema =
        { SchemaVersion: int
          Generator: GeneratorRef
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPathEntry list }

    // --- Skill-vendoring contract (ADR-0014, SDD-owned). ---

    /// Whether a skill is an SDD lifecycle *process* skill or a provider *product*
    /// skill. Producers ship only `Product` skills to a scaffolded product; the
    /// `Process` skills are SDD-seeded.
    type SkillScope =
        | Process
        | Product

    /// One declared skill in a producer's skill manifest (ADR-0014 §Decision 1).
    /// `Sha256` is the digest of the canonical body; the body itself is carried
    /// inline in `Body` or as a resolvable in-package path in `ResolvablePath`
    /// (exactly one in practice — a P1 library policy, not a shape constraint).
    type SkillManifestEntry =
        { Id: string
          Scope: SkillScope
          Sha256: string
          Body: string option
          ResolvablePath: string option }

    /// A producer's declarative skill manifest — the contract the skill fan-out
    /// reads instead of directory scans or per-source `template.json` strings.
    type SkillManifest =
        { SchemaVersion: int
          Skills: SkillManifestEntry list }

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
    val skillManifestVersion: int
    val governanceVersion: int
    val policyVersion: int
    val capabilitiesVersion: int
    val toolingVersion: int

    /// The single declared agent-skill root set (`AGENT_SKILL_ROOTS`, ADR-0014
    /// §Decision 5). Every fan-out/verify derives its targets from this; skills
    /// live under `<root>/skills/`. Adding a runtime root is a one-line change.
    val agentSkillRoots: string list

    /// All 11 named schemas, for the "every schema represented?" check (SC-001).
    val entries: SchemaContractEntry list
