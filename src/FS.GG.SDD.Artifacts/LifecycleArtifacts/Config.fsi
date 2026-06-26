namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module Config =
    type ProjectLifecycleConfig =
        { SchemaVersion: SchemaVersion
          ProjectId: string
          DefaultWorkRoot: string
          SddConfigPath: string
          AgentsConfigPath: string
          GovernancePolicyPath: string option
          GovernanceCapabilitiesPath: string option
          GovernanceToolingPath: string option }

    type SddLifecyclePolicy =
        { SchemaVersion: SchemaVersion
          Stages: LifecycleStage list
          WorkRoot: string
          ReadinessRoot: string
          RequireSourceDigests: bool
          RequireGeneratorVersion: bool
          StaleBehavior: string }

    type AgentGuidanceTarget = { Id: string; GuidancePath: string; GeneratedRoot: string }

    type AgentGuidanceConfig =
        { SchemaVersion: SchemaVersion
          Targets: AgentGuidanceTarget list
          WorkModelPath: string
          GeneratedGuidanceIsAuthority: bool
          RequireEquivalentClaudeAndCodexBehavior: bool }

    /// One declared `--param` of a template provider (`.fsgg/providers.yml`).
    type ProviderParameterSpec =
        { Key: string
          Required: bool
          Default: string option }

    /// An author-/provider-supplied template-provider descriptor. Every field is
    /// opaque to SDD except `ContractVersion`, which SDD validates before invoking.
    type ProviderDescriptor =
        { Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterSpec list }

    val parseProjectConfig: snapshot: FileSnapshot -> Result<ProjectLifecycleConfig, Diagnostic list>
    val parseSddLifecyclePolicy: snapshot: FileSnapshot -> Result<SddLifecyclePolicy, Diagnostic list>
    val parseAgentGuidanceConfig: snapshot: FileSnapshot -> Result<AgentGuidanceConfig, Diagnostic list>
    val parseProviderRegistry: snapshot: FileSnapshot -> Result<ProviderDescriptor list, Diagnostic list>
