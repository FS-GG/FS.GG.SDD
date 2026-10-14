namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef

/// Optional owner-authored marked guidance, supplied only through a verified workspace payload.
module OwnerGuidance =
    type Declaration =
        {
            Id: string
            SourcePath: string
            Targets: string list
            RequiredPaths: string list
        }

    type Write =
        {
            Path: string
            Text: string
            ExpectedBytes: byte array option
        }

    type Plan =
        {
            Writes: Write list
            Problems: string list
            DescriptorDigest: string option
            FragmentDigests: (string * string) list
            MissingTargets: string list
        }

    val descriptorPath: string
    val isContainedPath: path: string -> bool
    /// Parse bounded declarations, rejecting unknown members and ambiguous identities.
    val parse: text: string -> Result<Declaration list, string>
    /// Replace a single valid managed block or append one; preserve all surrounding text.
    val merge: id: string -> fragment: string -> text: string -> Result<string, string>

    /// The complete read set required by selected verified owner bytes and configured agent targets.
    val requiredReads:
        verifiedFiles: Map<string, byte array> -> agents: AgentGuidanceConfig -> Result<string list, string>

    /// Preflight every selected target and prerequisite before emitting any write.
    val plan:
        verifiedFiles: Map<string, byte array> ->
        agents: AgentGuidanceConfig ->
        observed: Map<string, Result<FileSnapshot option, string>> ->
            Plan
