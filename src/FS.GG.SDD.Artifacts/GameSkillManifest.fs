namespace FS.GG.SDD.Artifacts

open System
open System.Text.Json

module GameSkillManifest =
    type GameSkillManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          Mirrored: bool option
          SuppliedBy: string option
          MaterializesWhen: string }

    type GameSkillManifest =
        { SchemaVersion: int
          Skills: GameSkillManifestEntry list }

    let tryParse (text: string) : Result<GameSkillManifest, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | None -> Error "skill-manifest.json: missing or non-integer 'schemaVersion'."
            | Some version ->
                let skills =
                    jsonArray "skills" root
                    |> List.choose (fun element ->
                        match
                            jsonString "id" element,
                            jsonString "sha256" element,
                            jsonString "materializes-when" element
                        with
                        | Some id, Some sha256, Some materializesWhen when
                            not (String.IsNullOrWhiteSpace id)
                            && not (String.IsNullOrWhiteSpace sha256)
                            && not (String.IsNullOrWhiteSpace materializesWhen)
                            ->
                            Some
                                { Id = id.Trim()
                                  Scope = jsonString "scope" element |> Option.defaultValue "" |> (fun s -> s.Trim())
                                  Sha256 = sha256.Trim()
                                  // Three-state: a plain JSON `true`/`false` ⇒ `Some`; an absent
                                  // key ⇒ `None` (unclassified — never coerced to `false`, which
                                  // would silently promote an unclassified row into the delivered
                                  // set). `jsonBool` returns `None` for absent/non-boolean alike;
                                  // for delivery we only ever ACT on `Some false`, so a malformed
                                  // value is treated as not-delivered, never as delivered.
                                  Mirrored = jsonBool "mirrored" element
                                  SuppliedBy =
                                    jsonString "supplied-by" element
                                    |> Option.map (fun s -> s.Trim())
                                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                  MaterializesWhen = materializesWhen.Trim() }
                        | _ -> None)

                Ok
                    { SchemaVersion = version
                      Skills = skills }
        with ex ->
            Error(sprintf "skill-manifest.json: %s" ex.Message)

module ProductPredicate =
    // A single atom: `always` / `false` / `<key> == <v>` / `<key> != <v>` / `<key> in [a, b]`
    // (the ADR-0017 canonical grammar's atoms). A key absent from the parameter map reads as the
    // empty string, so `<key> == v` / `<key> in [v]` are `false` when the key is unset and
    // `<key> != v` is `true` — an unspecified parameter simply matches no declared value.
    let private evaluateAtom (parameters: Map<string, string>) (atom: string) : bool option =
        let atom = atom.Trim()

        let valueOf key =
            Map.tryFind key parameters |> Option.defaultValue ""

        if atom = "always" then
            Some true
        elif atom = "false" then
            Some false
        elif atom.Contains(" in ") then
            let index = atom.IndexOf(" in ", StringComparison.Ordinal)
            let key = atom.Substring(0, index).Trim()
            let rest = atom.Substring(index + 4).Trim()

            if
                key = ""
                || not (rest.StartsWith("[", StringComparison.Ordinal))
                || not (rest.EndsWith("]", StringComparison.Ordinal))
            then
                None
            else
                let members =
                    rest.Substring(1, rest.Length - 2).Split(',')
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (fun s -> s <> "")
                    |> Set.ofArray

                Some(members.Contains(valueOf key))
        elif atom.Contains(" == ") then
            match atom.Split([| " == " |], StringSplitOptions.None) |> Array.toList with
            | [ key; value ] when key.Trim() <> "" -> Some(valueOf (key.Trim()) = value.Trim())
            | _ -> None
        elif atom.Contains(" != ") then
            match atom.Split([| " != " |], StringSplitOptions.None) |> Array.toList with
            | [ key; value ] when key.Trim() <> "" -> Some(valueOf (key.Trim()) <> value.Trim())
            | _ -> None
        else
            None

    let evaluate (predicate: string) (parameters: Map<string, string>) : bool option =
        let predicate = predicate.Trim()
        let hasAnd = predicate.Contains(" and ")
        let hasOr = predicate.Contains(" or ")

        let combine (separator: string) (fold: bool option list -> bool option) =
            let results =
                predicate.Split([| separator |], StringSplitOptions.None)
                |> Array.toList
                |> List.map (evaluateAtom parameters)

            if results |> List.exists Option.isNone then
                None
            else
                fold results

        if String.IsNullOrWhiteSpace predicate then
            None
        elif hasAnd && hasOr then
            // Mixed connectives — precedence is ambiguous without parentheses; fail closed
            // rather than guess (FR-004).
            None
        elif hasAnd then
            combine " and " (fun rs -> Some(rs |> List.forall (fun r -> r = Some true)))
        elif hasOr then
            combine " or " (fun rs -> Some(rs |> List.exists (fun r -> r = Some true)))
        else
            evaluateAtom parameters predicate
