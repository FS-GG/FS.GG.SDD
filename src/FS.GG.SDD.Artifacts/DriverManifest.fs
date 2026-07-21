namespace FS.GG.SDD.Artifacts

open System
open System.Text.Json

module DriverManifest =
    type DriverManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          SuppliedBy: string option
          MaterializesWhen: string }

    type DriverManifest =
        { SchemaVersion: int
          Skills: DriverManifestEntry list }

    let tryParse (text: string) : Result<DriverManifest, string> =
        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            match jsonInt "schemaVersion" root with
            | None -> Error "driver-skill-manifest.json: missing or non-integer 'schemaVersion'."
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
            Error(sprintf "driver-skill-manifest.json: %s" ex.Message)

module DriverPredicate =
    // A single `has <glob>` / `always` / `false` atom. `<glob>` is an exact id or a trailing-`*`
    // prefix (spelled out — no interior glob matcher, matching the touch-set grammar's spirit).
    let private evaluateAtom (presentIds: Set<string>) (atom: string) : bool option =
        let atom = atom.Trim()

        if atom = "always" then
            Some true
        elif atom = "false" then
            Some false
        elif atom.StartsWith("has ", StringComparison.Ordinal) then
            let pattern = atom.Substring(4).Trim()

            if String.IsNullOrWhiteSpace pattern then
                None
            elif pattern.EndsWith("*", StringComparison.Ordinal) then
                let prefix = pattern.Substring(0, pattern.Length - 1)

                Some(
                    presentIds
                    |> Set.exists (fun id -> id.StartsWith(prefix, StringComparison.Ordinal))
                )
            else
                Some(presentIds.Contains pattern)
        else
            None

    let evaluate (predicate: string) (presentIds: Set<string>) : bool option =
        let predicate = predicate.Trim()
        let hasAnd = predicate.Contains(" and ")
        let hasOr = predicate.Contains(" or ")

        let combine (separator: string) (fold: bool option list -> bool option) =
            let results =
                predicate.Split([| separator |], StringSplitOptions.None)
                |> Array.toList
                |> List.map (evaluateAtom presentIds)

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
            evaluateAtom presentIds predicate
