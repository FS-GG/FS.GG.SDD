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

    let viewKindValue kind =
        match kind with
        | WorkModel -> "workModel"
        | Analysis -> "analysis"
        | Verify -> "verify"
        | Ship -> "ship"
        | Summary -> "summary"
        | AgentCommands -> "agentCommands"
        | Other value -> value

    let createWorkModelManifest viewPath generatorVersion sources outputDigest =
        let view =
            match ArtifactRef.create viewPath ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
            | Ok value -> value
            | Error message -> invalidArg (nameof viewPath) message

        { View = view
          Kind = WorkModel
          SchemaVersion = SchemaVersion.create 1
          Generator = generatorVersion
          Sources = sources |> List.sortBy (fun source -> source.Artifact.Path)
          OutputDigest = outputDigest
          Diagnostics = [] }

    let isStale currentSources manifest =
        let expected =
            currentSources
            |> List.map (fun source -> source.Artifact.Path, source.Digest.Value)
            |> Map.ofList

        manifest.Sources
        |> List.exists (fun source ->
            match Map.tryFind source.Artifact.Path expected with
            | Some digest -> digest <> source.Digest.Value
            | None -> true)
