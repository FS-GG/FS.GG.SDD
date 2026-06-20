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
        | GovernanceHandoff
        | Other of string

    type GeneratedViewCurrencyStatus =
        | CurrencyCurrent
        | CurrencyMissing
        | CurrencyStale
        | CurrencyMalformed

    type SourceIdentity =
        { Artifact: ArtifactRef
          Digest: SourceDigest
          SchemaVersion: SchemaVersion option
          SchemaStatus: SchemaCompatibilityStatus
          RawSchemaVersion: string option }

    type GenerationManifest =
        { View: ArtifactRef
          Kind: GeneratedViewKind
          SchemaVersion: SchemaVersion
          Generator: GeneratorVersion
          Sources: SourceIdentity list
          OutputDigest: OutputDigest option
          Currency: GeneratedViewCurrencyStatus
          Diagnostics: Diagnostic list }

    type GeneratedWorkModelMetadata =
        { Path: string
          SchemaVersion: SchemaVersion option
          ModelVersion: string option
          Generator: GeneratorVersion option
          Sources: SourceIdentity list
          OutputDigest: OutputDigest option }

    val viewKindValue: kind: GeneratedViewKind -> string
    val currencyStatusValue: status: GeneratedViewCurrencyStatus -> string
    val expectedWorkModelOutputPath: workId: string -> string
    val expectedSummaryOutputPath: workId: string -> string
    val expectedGovernanceHandoffOutputPath: workId: string -> string
    val createWorkModelManifest:
        viewPath: string -> generatorVersion: GeneratorVersion -> sources: SourceIdentity list -> outputDigest: OutputDigest option -> GenerationManifest
    val createSummaryManifest:
        viewPath: string -> generatorVersion: GeneratorVersion -> sources: SourceIdentity list -> outputDigest: OutputDigest option -> GenerationManifest
    val isStale: currentSources: SourceIdentity list -> manifest: GenerationManifest -> bool
    val parseWorkModelMetadata: path: string -> json: string -> Result<GeneratedWorkModelMetadata, Diagnostic list>
