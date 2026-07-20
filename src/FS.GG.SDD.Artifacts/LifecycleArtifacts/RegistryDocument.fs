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
                    | :? YamlScalarNode as scalar -> (Option.ofObj scalar.Value |> Option.defaultValue "").Trim()
                    // A nested sequence/mapping where a repo id belongs. Rendered as a blank
                    // so the validator's `isBlank` arm reports it, rather than dropped — a
                    // dropped entry would shorten the list and could empty it entirely,
                    // turning a malformed row into a deliberate "nothing consumes this".
                    | _ -> "")
                |> Seq.toList
                |> Fsgg.Registry.ConsumersDeclared
            // Described by kind rather than by its bytes where the value is structural, so the
            // diagnostic stays deterministic and one-line. A scalar carries its text, because
            // `consumers: sdd` is the likely typo and the author needs to see what was read.
            | :? YamlScalarNode as scalar ->
                // An absent value and a whitespace-only one are the same fault (`consumers:`
                // with nothing after it), so they report identically rather than through two
                // arms that happen to agree.
                let raw = Option.ofObj scalar.Value |> Option.defaultValue ""

                if String.IsNullOrWhiteSpace raw then
                    Fsgg.Registry.ConsumersMalformed "<null>"
                else
                    Fsgg.Registry.ConsumersMalformed $"'{raw}'"
            | :? YamlMappingNode -> Fsgg.Registry.ConsumersMalformed "<mapping>"
            | _ -> Fsgg.Registry.ConsumersMalformed "<non-sequence>"

    /// The three-state `wire-contract` read (FS.GG.SDD#589 / ADR-0052), spelled out for the
    /// same reasons `parseConsumers` is:
    ///
    /// Presence is decided on the KEY (`tryNodeAt`): an absent `wire-contract:` is
    /// `WireUnspecified` (this contract has no wire dimension — the common case), never a
    /// malformed one.
    ///
    /// A present value that is not a MAPPING cannot carry a provenance, so it is MALFORMED
    /// rather than absent — reading a scalar/sequence as "no wire contract" would silence a
    /// real typo. It is described by KIND (`<null>`, `<sequence>`, …) where structural, so
    /// the diagnostic stays deterministic and one-line; a scalar carries its text, because
    /// `wire-contract: sc2` is the likely mistake and the author needs to see what was read.
    ///
    /// A mapping with an UNKNOWN or blank `provenance:` is MALFORMED, not silently dropped and
    /// not guessed: the union is closed (Registry.fsi), so an unrecognised provenance has no
    /// honest declared value. Each known provenance carries only its own fields, read as-is;
    /// the pure validator (`validateDocument`) reports any that are blank, exactly as it does
    /// for a blank `consumers` entry — this edge classifies, it does not judge completeness.
    let private parseWireContract (item: YamlNode) : Fsgg.Registry.WireContractDeclaration =
        match tryNodeAt [ "wire-contract" ] item with
        | None -> Fsgg.Registry.WireUnspecified
        | Some node ->
            match tryMapping node with
            | None ->
                match node with
                | :? YamlScalarNode as scalar ->
                    let raw = Option.ofObj scalar.Value |> Option.defaultValue ""

                    if String.IsNullOrWhiteSpace raw then
                        Fsgg.Registry.WireMalformed "<null>"
                    else
                        Fsgg.Registry.WireMalformed $"'{raw}'"
                | :? YamlSequenceNode -> Fsgg.Registry.WireMalformed "<sequence>"
                | _ -> Fsgg.Registry.WireMalformed "<non-mapping>"
            | Some _ ->
                let field key =
                    tryScalarAt [ key ] node |> Option.defaultValue ""

                match (field "provenance").Trim() with
                | "vendored-proto" ->
                    Fsgg.Registry.WireDeclared(Fsgg.Registry.VendoredProto(field "upstream", field "upstream-version"))
                | "owned-proto" -> Fsgg.Registry.WireDeclared(Fsgg.Registry.OwnedProto(field "proto"))
                | "code-first-protobuf-net" ->
                    Fsgg.Registry.WireDeclared(Fsgg.Registry.CodeFirstProtobufNet(field "surface"))
                | "" -> Fsgg.Registry.WireMalformed "<no provenance>"
                | other -> Fsgg.Registry.WireMalformed $"unknown provenance '{other}'"

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

                    // FS.GG.SDD#610: ContractEntry is a class — object-initializer, not a
                    // record literal. A future field is an added named argument here, and an
                    // added property there, with no positional-ctor break in between.
                    Some(
                        Fsgg.Registry.ContractEntry(
                            Id = scalar "id",
                            Version = scalar "version",
                            Owner = scalar "owner",
                            Surface = scalar "surface",
                            Consumers = parseConsumers item,
                            WireContract = parseWireContract item,
                            PackageVersion = optScalar "package-version",
                            Range = optScalar "range"
                        )
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
