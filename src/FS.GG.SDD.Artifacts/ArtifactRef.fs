namespace FS.GG.SDD.Artifacts

open System

module ArtifactRef =
    type ArtifactOwner =
        | Sdd
        | Governance
        | Rendering
        | GeneratedProduct
        // 056: the SDD orchestrator mirror-copy owner. A `.claude`/`.codex` copy of a
        // provider-produced `.agents/skills/*` skill that SDD fanned out (never the
        // provider's canonical `.agents` product, which stays `generatedProduct`).
        | Mirrored
        // 108 / ADR-0054: a `.github`-authored driver skill (e.g. `workRoadmap`) delivered
        // as bytes in the pinned `FS.GG.Drivers` package and materialized by the SDD
        // scaffolder into a product's skill roots. Externally owned (like `Mirrored`/
        // `GeneratedProduct`), so `refresh` never regenerates it; recorded only in
        // `ScaffoldProvenanceRecord.DriverPaths`. Serialized `"driver"`.
        | Driver
        // ADR-0063 / FS.GG.SDD#623: an owner-authored **product** skill (e.g.
        // `fs-gg-playtest`) whose bytes are `mirrored: false` (no frozen provider mirror),
        // delivered in the pinned the owner-skills package and materialized by the SDD
        // scaffolder into a product's skill roots. Externally owned (like `Driver`/`Mirrored`),
        // so `refresh` never regenerates it; recorded only in
        // `ScaffoldProvenanceRecord.GameSkillPaths`. Serialized `"gameSkill"`.
        | GameSkill

    type ArtifactKind =
        | ProjectConfig
        | SddConfig
        | AgentsConfig
        | GovernancePolicy
        | GovernanceCapabilities
        | GovernanceTooling
        | Spec
        | Clarifications
        | Checklist
        | Plan
        | Contracts
        | Tasks
        | Evidence
        | GeneratedView
        | FixtureManifest
        | Other of string

    type ArtifactRef =
        { Path: string
          Kind: ArtifactKind
          Owner: ArtifactOwner
          RequiredBySdd: bool }

    let normalizePath (path: string) =
        (if String.IsNullOrEmpty path then
             ""
         else
             path.Trim().Replace('\\', '/'))
            .TrimStart('/')

    let create (path: string) kind owner requiredBySdd =
        let path = normalizePath path

        if String.IsNullOrWhiteSpace path then
            Error "Artifact path is required."
        elif path.Contains("..") then
            Error "Artifact paths must be repository-relative and stay inside the repository."
        else
            Ok
                { Path = path
                  Kind = kind
                  Owner = owner
                  RequiredBySdd = requiredBySdd }

    let ownerValue owner =
        match owner with
        | Sdd -> "sdd"
        | Governance -> "governance"
        | Rendering -> "rendering"
        | GeneratedProduct -> "generatedProduct"
        | Mirrored -> "mirrored"
        | Driver -> "driver"
        | GameSkill -> "gameSkill"

    let kindValue kind =
        match kind with
        | ProjectConfig -> "projectConfig"
        | SddConfig -> "sddConfig"
        | AgentsConfig -> "agentsConfig"
        | GovernancePolicy -> "governancePolicy"
        | GovernanceCapabilities -> "governanceCapabilities"
        | GovernanceTooling -> "governanceTooling"
        | Spec -> "spec"
        | Clarifications -> "clarifications"
        | Checklist -> "checklist"
        | Plan -> "plan"
        | Contracts -> "contracts"
        | Tasks -> "tasks"
        | Evidence -> "evidence"
        | GeneratedView -> "generatedView"
        | FixtureManifest -> "fixtureManifest"
        | Other value -> value

    let path artifact = artifact.Path

    let optionalGovernanceBoundary path =
        match create path (Other "governanceBoundary") Governance false with
        | Ok artifact -> artifact
        | Error message -> invalidArg (nameof path) message
