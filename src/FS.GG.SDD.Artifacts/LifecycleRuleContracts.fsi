namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

module LifecycleRuleContracts =
    type GovernanceCompatibility =
        { RouteAware: bool
          ProfileAware: bool
          FreshnessAware: bool
          EnforceableBySdd: bool }

    type RuleInput = { Artifact: ArtifactRef; Required: bool }

    type LifecycleRuleContract =
        { SchemaVersion: SchemaVersion
          Id: string
          Owner: ArtifactOwner
          Stage: LifecycleStage
          Inputs: RuleInput list
          FindingShape: string
          DiagnosticIds: string list
          Evidence: string list
          TestObligations: string list
          GovernanceCompatibility: GovernanceCompatibility }

    val sddOnlyCompatibility: unit -> GovernanceCompatibility
    val initialContracts: unit -> LifecycleRuleContract list
    val contractIds: unit -> string list
