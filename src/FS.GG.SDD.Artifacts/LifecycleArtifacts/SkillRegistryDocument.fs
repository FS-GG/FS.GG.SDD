namespace FS.GG.SDD.Artifacts

open System
open System.IO
open YamlDotNet.RepresentationModel

module SkillRegistryDocument =

    type RegistryKind =
        | SkillRegistry
        | DependencyRegistry

    let private err path message : Result<Fsgg.Registry.SkillRegistryDocument, RegistryDocument.RegistryLoadError> =
        Error { Path = path; Message = message }

    /// The three-state `mirrored` read. THIS is the field the feature exists for, so it
    /// is spelled out rather than routed through `Internal.boolAt`: that helper's final
    /// arm is `| _ -> defaultValue`, which maps an ABSENT key and an UNPARSEABLE value
    /// onto the same result. Both collapses are the bug — the first invents a verdict the
    /// owner never gave, the second silently swallows one they gave wrongly.
    ///
    /// Presence is decided on the KEY (`tryNodeAt`), not on whether a scalar could be
    /// read, because `tryScalarAt` returns `None` both for an absent key and for a
    /// present-but-non-scalar value (`mirrored: [a, b]`) — and reading the latter as
    /// "absent" would be a silent skip, which is exactly what FR-003 forbids.
    ///
    /// An explicit `mirrored:` with no value (YAML null) is MALFORMED, not absent. The key
    /// is there; the verdict is not. That matches `.github`'s Python authority, which
    /// decides presence with `"mirrored" in row` and then rejects any non-boolean value —
    /// so the two validators cannot disagree about what a null verdict means.
    let private parseMirrored (item: YamlNode) : Fsgg.Registry.MirrorDeclaration =
        match tryNodeAt [ "mirrored" ] item with
        | None -> Fsgg.Registry.MirrorUnspecified
        | Some node ->
            match node with
            // ONLY a PLAIN (unquoted) `true`/`false` is a boolean. A QUOTED `"true"` is
            // the string "true", and reading it as a verdict would put this validator at
            // odds with `.github`'s Python authority, where PyYAML yields `str` for the
            // quoted form and the check rejects any non-`bool` — so a row it calls
            // malformed would sail past us. Two validators that disagree about the
            // canonical file are worse than one, and `Registry.fsi` says so.
            //
            // YamlDotNet's RepresentationModel does not resolve tags, so STYLE is the only
            // thing carrying that distinction. `Internal.isPlainNullScalar` already leans on
            // it for the same reason.
            //
            // Case-insensitive on the plain form, because YAML 1.1 resolves `true`/`True`/
            // `TRUE` alike and PyYAML emits `True`.
            | :? YamlScalarNode as scalar ->
                let raw = Option.ofObj scalar.Value |> Option.defaultValue ""
                let isPlain = scalar.Style = YamlDotNet.Core.ScalarStyle.Plain

                if isPlain && raw.Equals("true", StringComparison.OrdinalIgnoreCase) then
                    Fsgg.Registry.MirrorDeclared true
                elif isPlain && raw.Equals("false", StringComparison.OrdinalIgnoreCase) then
                    Fsgg.Registry.MirrorDeclared false
                else
                    Fsgg.Registry.MirrorMalformed raw
            // A sequence/mapping where a boolean belongs. Described by kind rather than
            // by its bytes, so the diagnostic stays deterministic and one-line.
            | :? YamlSequenceNode -> Fsgg.Registry.MirrorMalformed "<sequence>"
            | :? YamlMappingNode -> Fsgg.Registry.MirrorMalformed "<mapping>"
            | _ -> Fsgg.Registry.MirrorMalformed "<non-scalar>"

    let private parseSkills (node: YamlNode) : Fsgg.Registry.SkillRegistryEntry list =
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
                          Scope = scalar "scope"
                          Owner = scalar "owner"
                          Source = scalar "source"
                          Sha256 = scalar "sha256"
                          Mirrored = parseMirrored item
                          MaterializesWhen = optScalar "materializes-when" }
                        : Fsgg.Registry.SkillRegistryEntry
                    ))
            |> Seq.toList

    let detectKind (path: string) : RegistryKind =
        try
            if String.IsNullOrWhiteSpace path || not (File.Exists path) then
                DependencyRegistry
            else
                match parseYamlDocument (File.ReadAllText path) with
                | YamlEmpty
                | YamlMalformed _ -> DependencyRegistry
                | YamlRoot root ->
                    match tryMapping root with
                    | None -> DependencyRegistry
                    | Some rootMapping ->
                        match tryChild "skills" rootMapping with
                        | Some _ -> SkillRegistry
                        | None -> DependencyRegistry
        with _ ->
            DependencyRegistry

    let load (path: string) : Result<Fsgg.Registry.SkillRegistryDocument, RegistryDocument.RegistryLoadError> =
        try
            if String.IsNullOrWhiteSpace path then
                err path "Skill registry path is empty."
            elif not (File.Exists path) then
                err path $"Skill registry file not found: '{path}'."
            else
                let text = File.ReadAllText path

                match parseYamlDocument text with
                | YamlEmpty -> err path "Skill registry file is empty."
                | YamlMalformed(message, line, column) ->
                    err path $"Skill registry file has a YAML syntax error at line {line}, column {column}: {message}"
                | YamlRoot root ->
                    match tryMapping root with
                    | None -> err path "Skill registry root is not a YAML mapping."
                    | Some rootMapping ->
                        match tryScalarAt [ "schemaVersion" ] root with
                        | None -> err path "Skill registry file is missing 'schemaVersion'."
                        | Some raw ->
                            match Int32.TryParse raw with
                            | false, _ -> err path $"Skill registry 'schemaVersion' is not an integer: '{raw}'."
                            | true, schemaVersion ->
                                Ok
                                    { SchemaVersion = schemaVersion
                                      Parameters = scalarList [ "parameters" ] root
                                      Skills =
                                        tryChild "skills" rootMapping
                                        |> Option.map parseSkills
                                        |> Option.defaultValue [] }
        with ex ->
            err path $"Skill registry file could not be parsed: {ex.Message}"
