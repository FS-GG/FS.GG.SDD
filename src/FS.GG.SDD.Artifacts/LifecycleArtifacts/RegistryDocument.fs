namespace FS.GG.SDD.Artifacts

open System
open System.IO
open YamlDotNet.RepresentationModel

module RegistryDocument =

    type RegistryLoadError = { Path: string; Message: string }

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

    /// The three-state `consumers` read (FS.GG.SDD#508). THIS is the field the major exists
    /// for, so it is spelled out rather than routed through `Internal.scalarList`: that
    /// helper ends in `Option.defaultValue []`, which maps an ABSENT key and a present `[]`
    /// onto the same empty list — and once an empty declaration is LEGAL, that collapse
    /// stops being merely lossy and starts silently accepting rows nobody declared.
    ///
    /// `scalarList` is left alone on purpose: every list field in `LifecycleArtifacts` shares
    /// it, and making it three-state would ripple through parsers that have no such question.
    ///
    /// Presence is decided on the KEY (`tryNodeAt`), not on whether a sequence could be read,
    /// because a present-but-non-sequence value (`consumers: sdd`) must report as MALFORMED
    /// rather than as absent — reading a typo as "you forgot to declare" sends the author
    /// hunting for a missing line that is right there.
    ///
    /// An explicit `consumers:` with no value (YAML null) is MALFORMED, not an empty
    /// declaration. The key is there; the list is not. The honest empty is `[]`, and
    /// requiring it to be written is the whole point — a deliberate, machine-readable
    /// nothing beats an omission that renders identically. (Same call `parseMirrored` makes
    /// for a null verdict, for the same reason.)
    ///
    /// Blank entries are NOT filtered here, unlike `scalarListFromNode`. `validateDocument`
    /// has always carried an `isBlank` arm for them that the filter made unreachable; now
    /// that `[]` is a valid answer, filtering `consumers: [""]` down to `[]` would promote a
    /// blank entry into a deliberate "nothing consumes this". The blanks are passed through
    /// so the validator can report them, which is what that arm was written to do.
    let private parseConsumers (item: YamlNode) : Fsgg.Registry.ConsumerDeclaration =
        match tryNodeAt [ "consumers" ] item with
        | None -> Fsgg.Registry.ConsumersUnspecified
        | Some node ->
            match node with
            | :? YamlSequenceNode as sequence ->
                sequence.Children
                |> Seq.map (fun child ->
                    match child with
                    | :? YamlScalarNode as scalar -> Option.ofObj scalar.Value |> Option.defaultValue ""
                    // A nested sequence/mapping where a repo id belongs. Rendered as a blank
                    // so the validator's `isBlank` arm reports it, rather than dropped — a
                    // dropped entry would shorten the list and could empty it entirely.
                    | _ -> "")
                |> Seq.map (fun value -> value.Trim())
                |> Seq.toList
                |> Fsgg.Registry.ConsumersDeclared
            // Described by kind rather than by its bytes where the value is structural, so the
            // diagnostic stays deterministic and one-line. A scalar carries its text, because
            // `consumers: sdd` is the likely typo and the author needs to see what was read.
            | :? YamlScalarNode as scalar ->
                match Option.ofObj scalar.Value with
                | None -> Fsgg.Registry.ConsumersMalformed "<null>"
                | Some raw when String.IsNullOrWhiteSpace raw -> Fsgg.Registry.ConsumersMalformed "<null>"
                | Some raw -> Fsgg.Registry.ConsumersMalformed $"'{raw}'"
            | :? YamlMappingNode -> Fsgg.Registry.ConsumersMalformed "<mapping>"
            | _ -> Fsgg.Registry.ConsumersMalformed "<non-sequence>"

    let private parseContracts (node: YamlNode) : Fsgg.Registry.ContractEntry list =
        match trySequence node with
        | None -> []
        | Some sequence ->
            sequence.Children
            |> Seq.choose (fun item ->
                match tryMapping item with
                | None -> None
                | Some _ ->
                    let scalar key =
                        tryScalarAt [ key ] item |> Option.defaultValue ""

                    let optScalar key =
                        tryScalarAt [ key ] item |> Option.filter (String.IsNullOrWhiteSpace >> not)

                    Some(
                        { Id = scalar "id"
                          Version = scalar "version"
                          Owner = scalar "owner"
                          Surface = scalar "surface"
                          Consumers = parseConsumers item
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

                match parseYamlDocument text with
                | YamlEmpty -> err path "Registry file is empty."
                | YamlMalformed(message, line, column) ->
                    err path $"Registry file has a YAML syntax error at line {line}, column {column}: {message}"
                | YamlRoot root ->
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
