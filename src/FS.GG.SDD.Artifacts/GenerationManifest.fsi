namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion

module GenerationManifest =
    type GeneratedViewKind =
        | WorkModel
        | Analysis
        | Verify
        | Ship
        | Summary
        | AgentCommands
        | Other of string

    type SourceIdentity = { Artifact: ArtifactRef; Digest: SourceDigest }

    type GenerationManifest =
        { View: ArtifactRef
          Kind: GeneratedViewKind
          SchemaVersion: SchemaVersion
          Generator: GeneratorVersion
          Sources: SourceIdentity list
          OutputDigest: OutputDigest option
          Diagnostics: Diagnostic list }

    val viewKindValue: kind: GeneratedViewKind -> string
    val createWorkModelManifest:
        viewPath: string -> generatorVersion: GeneratorVersion -> sources: SourceIdentity list -> outputDigest: OutputDigest option -> GenerationManifest
    val isStale: currentSources: SourceIdentity list -> manifest: GenerationManifest -> bool
