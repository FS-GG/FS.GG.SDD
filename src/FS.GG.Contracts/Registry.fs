namespace Fsgg

module Registry =

    type RegistryComponent =
        { Id: string
          Version: string }

    type DependencyEdge =
        { Consumer: string
          Provider: string
          CompatibleRange: string }

    type RegistryModel =
        { Components: RegistryComponent list
          Edges: DependencyEdge list }

    type RegistryRule =
        | MissingField of fieldName: string
        | UnknownComponent
        | IncompatibleVersion
        | MalformedVersion

    type RegistryDiagnostic =
        { Entry: string
          Rule: RegistryRule
          Message: string }

    type ValidationResult =
        | Valid
        | Invalid of RegistryDiagnostic list

    // --- Internal BCL-only SemVer helper (research R5; no third-party package). ---

    /// A parsed SemVer triple (major.minor.patch); pre-release/build metadata are
    /// out of scope for the registry coherence check.
    type private SemVer = { Major: int; Minor: int; Patch: int }

    let private tryParseSemVer (text: string) : SemVer option =
        match text.Split('.') with
        | [| a; b; c |] ->
            match System.Int32.TryParse a, System.Int32.TryParse b, System.Int32.TryParse c with
            | (true, major), (true, minor), (true, patch) when major >= 0 && minor >= 0 && patch >= 0 ->
                Some { Major = major; Minor = minor; Patch = patch }
            | _ -> None
        | _ -> None

    let private compareSemVer (a: SemVer) (b: SemVer) =
        match compare a.Major b.Major with
        | 0 ->
            match compare a.Minor b.Minor with
            | 0 -> compare a.Patch b.Patch
            | c -> c
        | c -> c

    /// One range comparator: an operator paired with its bound version.
    type private Comparator =
        { Op: string
          Bound: SemVer }

    let private tryParseComparator (token: string) : Comparator option =
        let ops = [ ">="; "<="; ">"; "<"; "=" ]

        match ops |> List.tryFind token.StartsWith with
        | Some op ->
            token.Substring(op.Length)
            |> tryParseSemVer
            |> Option.map (fun v -> { Op = op; Bound = v })
        | None ->
            // Bare version means exact match.
            token |> tryParseSemVer |> Option.map (fun v -> { Op = "="; Bound = v })

    /// Parse a whitespace-separated range into comparators; `None` if any token is
    /// malformed or the range is empty.
    let private tryParseRange (range: string) : Comparator list option =
        let tokens =
            range.Split([| ' '; '\t' |], System.StringSplitOptions.RemoveEmptyEntries)

        if tokens.Length = 0 then
            None
        else
            let parsed = tokens |> Array.map tryParseComparator

            if parsed |> Array.forall Option.isSome then
                Some(parsed |> Array.toList |> List.map Option.get)
            else
                None

    let private satisfiesComparator (version: SemVer) (comparator: Comparator) =
        let c = compareSemVer version comparator.Bound

        match comparator.Op with
        | ">=" -> c >= 0
        | "<=" -> c <= 0
        | ">" -> c > 0
        | "<" -> c < 0
        | _ -> c = 0

    let private satisfiesRange (version: SemVer) (comparators: Comparator list) =
        comparators |> List.forall (satisfiesComparator version)

    let private isBlank (text: string) = System.String.IsNullOrWhiteSpace text

    // --- The pure validator. ---

    let validate (model: RegistryModel) : ValidationResult =
        let componentDiagnostics =
            model.Components
            |> List.collect (fun c ->
                let entry = if isBlank c.Id then "<unnamed component>" else c.Id

                [ if isBlank c.Id then
                      { Entry = entry
                        Rule = MissingField "Id"
                        Message = "Component is missing a non-blank 'Id'." }
                  if isBlank c.Version then
                      { Entry = entry
                        Rule = MissingField "Version"
                        Message = $"Component '{entry}' is missing a non-blank 'Version'." }
                  elif (tryParseSemVer c.Version).IsNone then
                      { Entry = entry
                        Rule = MalformedVersion
                        Message = $"Component '{entry}' has a non-SemVer 'Version': '{c.Version}'." } ])

        let componentVersion id =
            model.Components
            |> List.tryFind (fun c -> not (isBlank c.Id) && c.Id = id)
            |> Option.map (fun c -> c.Version)

        let componentExists id =
            model.Components |> List.exists (fun c -> not (isBlank c.Id) && c.Id = id)

        let edgeDiagnostics =
            model.Edges
            |> List.collect (fun e ->
                let label c p =
                    let c = if isBlank c then "<blank>" else c
                    let p = if isBlank p then "<blank>" else p
                    $"{c} -> {p}"

                let entry = label e.Consumer e.Provider

                [ if isBlank e.Consumer then
                      { Entry = entry
                        Rule = MissingField "Consumer"
                        Message = $"Edge '{entry}' is missing a non-blank 'Consumer'." }
                  if isBlank e.Provider then
                      { Entry = entry
                        Rule = MissingField "Provider"
                        Message = $"Edge '{entry}' is missing a non-blank 'Provider'." }
                  if isBlank e.CompatibleRange then
                      { Entry = entry
                        Rule = MissingField "CompatibleRange"
                        Message = $"Edge '{entry}' is missing a non-blank 'CompatibleRange'." }

                  if not (isBlank e.Consumer) && not (componentExists e.Consumer) then
                      { Entry = entry
                        Rule = UnknownComponent
                        Message = $"Edge '{entry}' references an unknown consumer component '{e.Consumer}'." }
                  if not (isBlank e.Provider) && not (componentExists e.Provider) then
                      { Entry = entry
                        Rule = UnknownComponent
                        Message = $"Edge '{entry}' references an unknown provider component '{e.Provider}'." }

                  if not (isBlank e.CompatibleRange) then
                      match tryParseRange e.CompatibleRange with
                      | None ->
                          { Entry = entry
                            Rule = MalformedVersion
                            Message = $"Edge '{entry}' has a non-SemVer 'CompatibleRange': '{e.CompatibleRange}'." }
                      | Some comparators ->
                          match componentVersion e.Provider with
                          | Some providerVersion ->
                              match tryParseSemVer providerVersion with
                              | Some v when not (satisfiesRange v comparators) ->
                                  { Entry = entry
                                    Rule = IncompatibleVersion
                                    Message =
                                      $"Edge '{entry}' range '{e.CompatibleRange}' excludes provider '{e.Provider}' declared version '{providerVersion}'." }
                              | _ -> ()
                          | None -> () ])

        match componentDiagnostics @ edgeDiagnostics with
        | [] -> Valid
        | diagnostics -> Invalid diagnostics
