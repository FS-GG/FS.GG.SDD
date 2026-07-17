namespace FS.GG.SDD.Artifacts

module ArtifactRef =
    type ArtifactOwner =
        | Sdd
        | Governance
        | Rendering
        | GeneratedProduct
        /// 056: the SDD orchestrator mirror-copy owner — a `.claude`/`.codex` copy of a
        /// provider-produced `.agents/skills/*` skill that SDD fanned out. Serialized
        /// `"mirrored"`; appears only in `ScaffoldProvenanceRecord.MirroredPaths`.
        | Mirrored

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

    val create:
        path: string -> kind: ArtifactKind -> owner: ArtifactOwner -> requiredBySdd: bool -> Result<ArtifactRef, string>

    val ownerValue: owner: ArtifactOwner -> string
    val kindValue: kind: ArtifactKind -> string
    val path: artifact: ArtifactRef -> string
    val optionalGovernanceBoundary: path: string -> ArtifactRef
