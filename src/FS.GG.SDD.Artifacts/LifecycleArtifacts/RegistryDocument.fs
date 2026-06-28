namespace FS.GG.SDD.Artifacts

open System
open System.IO
open YamlDotNet.RepresentationModel

module RegistryDocument =

    type RegistryLoadError =
        { Path: string
          Message: string }

    let private err path message : Result<Fsgg.Registry.RegistryDocument, RegistryLoadError> =
        Error { Path = path; Message = message }

    // The `repos:` mapping (key -> {name, role}); preserves declaration order.
    let private parseRepos (node: YamlNode) : Fsgg.Registry.RegistryRepo list =
        match tryMapping node with
        | None -> []
        | Some mapping ->
            mapping.Children
            |> Seq.choose (fun pair ->
                match pair.Key with
                | :? YamlScalarNode as keyNode ->
                    Some(
                        { Id = Option.ofObj keyNode.Value |> Option.defaultValue ""
                          Name = tryScalarAt [ "name" ] pair.Value |> Option.defaultValue ""
                          Role = tryScalarAt [ "role" ] pair.Value |> Option.defaultValue "" }
                        : Fsgg.Registry.RegistryRepo
                    )
                | _ -> None)
            |> Seq.toList

    let private parseContracts (node: YamlNode) : Fsgg.Registry.ContractEntry list =
        match trySequence node with
        | None -> []
        | Some sequence ->
            sequence.Children
            |> Seq.choose (fun item ->
                match tryMapping item with
                | None -> None
                | Some _ ->
                    let scalar key = tryScalarAt [ key ] item |> Option.defaultValue ""

                    let optScalar key =
                        tryScalarAt [ key ] item |> Option.filter (String.IsNullOrWhiteSpace >> not)

                    Some(
                        { Id = scalar "id"
                          Version = scalar "version"
                          Owner = scalar "owner"
                          Surface = scalar "surface"
                          Consumers = scalarList [ "consumers" ] item
                          PackageVersion = optScalar "package-version"
                          Range = optScalar "range" }
                        : Fsgg.Registry.ContractEntry
                    ))
            |> Seq.toList

    let private parseDependencies (node: YamlNode) : Fsgg.Registry.DependencyEdge2 list =
        match trySequence node with
        | None -> []
        | Some sequence ->
            sequence.Children
            |> Seq.choose (fun item ->
                match tryMapping item with
                | None -> None
                | Some _ ->
                    Some(
                        { From = tryScalarAt [ "from" ] item |> Option.defaultValue ""
                          To = tryScalarAt [ "to" ] item |> Option.defaultValue ""
                          Via = tryScalarAt [ "via" ] item |> Option.defaultValue "" }
                        : Fsgg.Registry.DependencyEdge2
                    ))
            |> Seq.toList

    let private parseCoherence (node: YamlNode) : Fsgg.Registry.CoherenceEntry list =
        match trySequence node with
        | None -> []
        | Some sequence ->
            sequence.Children
            |> Seq.choose (fun item ->
                match tryMapping item with
                | None -> None
                | Some _ ->
                    Some(
                        { Id = tryScalarAt [ "id" ] item |> Option.defaultValue ""
                          Coherent = boolAt [ "coherent" ] item false }
                        : Fsgg.Registry.CoherenceEntry
                    ))
            |> Seq.toList

    let load (path: string) : Result<Fsgg.Registry.RegistryDocument, RegistryLoadError> =
        try
            if String.IsNullOrWhiteSpace path then
                err path "Registry path is empty."
            elif not (File.Exists path) then
                err path $"Registry file not found: '{path}'."
            else
                let text = File.ReadAllText path

                match parseYaml text with
                | None -> err path "Registry file is empty or has no YAML document."
                | Some root ->
                    match tryMapping root with
                    | None -> err path "Registry root is not a YAML mapping."
                    | Some rootMapping ->
                        match tryScalarAt [ "schemaVersion" ] root with
                        | None -> err path "Registry file is missing 'schemaVersion'."
                        | Some raw ->
                            match Int32.TryParse raw with
                            | false, _ -> err path $"Registry 'schemaVersion' is not an integer: '{raw}'."
                            | true, schemaVersion ->
                                let childList key parse =
                                    tryChild key rootMapping |> Option.map parse |> Option.defaultValue []

                                Ok
                                    { SchemaVersion = schemaVersion
                                      Repos = childList "repos" parseRepos
                                      Contracts = childList "contracts" parseContracts
                                      Dependencies = childList "dependencies" parseDependencies
                                      Coherence = childList "coherence" parseCoherence }
        with ex ->
            err path $"Registry file could not be parsed: {ex.Message}"
