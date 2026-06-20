namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

module LifecycleRuleContracts =
    /// Advisory placeholder describing how a lifecycle rule *might* relate to Governance.
    /// SUPERSEDED by the concrete `GovernanceHandoff` generated view
    /// (`readiness/<id>/governance-handoff.json`), which is the single, versioned, Governance-facing
    /// source of declared facts (Constitution VII). These booleans are retained only as a coarse
    /// pointer to that contract; they assert no route/profile/freshness/enforcement themselves
    /// (SDD never decides those — `route`/profile/freshness/gate are Governance-owned). Prefer the
    /// handoff for any real Governance integration.
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
