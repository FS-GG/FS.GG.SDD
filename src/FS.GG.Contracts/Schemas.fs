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

    type GeneratorRef = { Id: string; Version: string }

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
          Owner: string
          Sha256: string option }

    type ScaffoldProvenanceSchema =
        { SchemaVersion: int
          Generator: GeneratorRef
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPathEntry list }

    type SkillScope =
        | Process
        | Product

    type SkillManifestEntry =
        { Id: string
          Scope: SkillScope
          Sha256: string
          Body: string option
          ResolvablePath: string option }

    type SkillManifest =
        { SchemaVersion: int
          Skills: SkillManifestEntry list }

    type SkillManifestFile =
        { RelativePath: string; Sha256: string }

    type SkillManifestFileSet =
        { Skill: SkillManifestEntry
          Files: SkillManifestFile list }

    type SkillManifestV2 =
        { SchemaVersion: int
          Skills: SkillManifestFileSet list }

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

    type PerformanceEvidenceSampleSet =
        { WorkloadId: string
          WorkloadDefinitionDigest: string
          WorkloadClass: string
          TargetFps: int
          MaxP95Ms: decimal
          MaxP99Ms: decimal
          MaxCatchUpFrames: int
          MeasurementScope: string
          RequiredCapability: string
          HostProfile: string
          PackageVersions: string list
          MeasurementMode: string
          Capabilities: string list
          WarmupPolicy: string
          SamplePolicy: string
          CapturedAtUtc: string
          CurrencyToken: string
          ProbeReadbackContaminated: bool
          DurationSamplesMs: decimal list
          CatchUpFrames: int list }

    type PerformanceEvidenceArtifact =
        { ContractVersion: string
          ClaimedBudgetPassed: bool option
          SampleSets: PerformanceEvidenceSampleSet list }

    type PerformanceEvidenceMeasurement =
        { WorkloadId: string
          P95Ms: decimal
          P99Ms: decimal
          MaxCatchUpFrames: int }

    /// The single lifecycle declaration of performance intent. Early SDD stages author this
    /// contract; evidence and Governance carry the same value instead of maintaining mirrors.
    type PerformanceIntentDeclaration =
        { Id: string
          Disposition: string
          TargetFps: int
          WorkloadIds: string list
          WorkloadDefinitionDigests: string list
          MaximumExpectedScale: string
          MaxP95Ms: decimal
          MaxP99Ms: decimal
          MaxCatchUpFrames: int
          StructuralCostBudgets: string list
          RequiredCapability: string
          LiveCompositorRequired: bool
          DeferralIssue: string option
          EvidenceRefs: string list
          Rationale: string option }

    type GovernanceHandoffPerformanceEvidence =
        { EvidenceId: string
          ArtifactPath: string
          Intent: PerformanceIntentDeclaration option
          Artifact: PerformanceEvidenceArtifact
          Measurements: PerformanceEvidenceMeasurement list }

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
          PerformanceEvidence: GovernanceHandoffPerformanceEvidence list
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
    let providersVersion = 1
    let projectVersion = 1
    let sddVersion = 1
    let agentsVersion = 1
    let scaffoldProvenanceVersion = 1
    // governance-handoff: the SINGLE source of these two values. Every other site CONSUMES them —
    // the emitter (src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs) and the release-contract
    // declaration (src/FS.GG.SDD.Artifacts/ReleaseContract.fs) both read them rather than
    // re-typing them, so no site can self-declare a version this package never declared. They were
    // hand-mirrored until #427, and all three had drifted apart.
    //
    // 1.1.0 (ADR-0035 stage 3, FS.GG.SDD#422): `ship.unobservedEvidence` became reachable in
    // readiness.blockingDiagnosticIds[] — additive, so minor. Bumping this re-stamps the artifact.
    // FS-GG/.github `registry/dependencies.yml` declares the same 1.1.0; the gate that COMPARES the
    // two does not exist yet and is owed by that repo (its coherence row
    // `governance-handoff-emitted-version` tracks it). Until it lands, the two are kept in step by
    // hand — so change this value only alongside the registry.
    let governanceHandoffVersion = 1
    let governanceHandoffContractVersion = "2.0.0"
    // SDD-owned skill-vendoring contract (ADR-0014). The manifest is the producer's
    // declarative skill set; the root set is a single declared constant.
    //
    // 2 (ADR-0017 amendment, FS.GG.SDD#727): the manifest content-addresses a skill's COMPLETE
    // FILE SET (`SkillManifestV2`/`SkillManifestFileSet`), not its `SKILL.md` alone. Deliberately
    // a VERSION BUMP and not a silent widening: at v1 the absence of per-file digests truthfully
    // said "the auxiliaries carry no declared authority", while at v2 the declared set is complete,
    // so a file outside it is a file no digest authorises. A reader must be able to tell which
    // claim it holds, and `schemaVersion` is how.
    let skillManifestVersion = 2
    // Governance-owned: declared to the Governance published reference, NOT SDD-emitted.
    let governanceVersion = 1
    let policyVersion = 1
    let capabilitiesVersion = 2
    let toolingVersion = 1

    let entries: SchemaContractEntry list =
        [ { Name = "providers"
            SchemaVersion = providersVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "project"
            SchemaVersion = projectVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "sdd"
            SchemaVersion = sddVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "agents"
            SchemaVersion = agentsVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "scaffold-provenance"
            SchemaVersion = scaffoldProvenanceVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "governance-handoff"
            SchemaVersion = governanceHandoffVersion
            ContractVersion = Some governanceHandoffContractVersion
            Owner = Sdd }
          { Name = "skill-manifest"
            SchemaVersion = skillManifestVersion
            ContractVersion = None
            Owner = Sdd }
          { Name = "governance"
            SchemaVersion = governanceVersion
            ContractVersion = None
            Owner = Governance }
          { Name = "policy"
            SchemaVersion = policyVersion
            ContractVersion = None
            Owner = Governance }
          { Name = "capabilities"
            SchemaVersion = capabilitiesVersion
            ContractVersion = None
            Owner = Governance }
          { Name = "tooling"
            SchemaVersion = toolingVersion
            ContractVersion = None
            Owner = Governance } ]

    // The single declared agent-skill root set (ADR-0065, as amended by ADR-0067 §5).
    // Bare repo-root names; consumers append `skills/`. One place to add/rename a runtime root.
    let agentSkillRoots: string list = [ ".claude"; ".agents" ]
