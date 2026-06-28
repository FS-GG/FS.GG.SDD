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
        | DuplicateComponent
        | MalformedDocument

    type RegistryDiagnostic =
        { Entry: string
          Rule: RegistryRule
          Message: string }

    type ValidationResult =
        | Valid
        | Invalid of RegistryDiagnostic list

    // --- Real-schema document model (feature 042, additive). ---

    type RegistryRepo =
        { Id: string
          Name: string
          Role: string }

    type ContractEntry =
        { Id: string
          Version: string
          Owner: string
          Surface: string
          Consumers: string list
          PackageVersion: string option
          Range: string option }

    type DependencyEdge2 =
        { From: string
          To: string
          Via: string }

    type CoherenceEntry =
        { Id: string
          Coherent: bool }

    type RegistryDocument =
        { SchemaVersion: int
          Repos: RegistryRepo list
          Contracts: ContractEntry list
          Dependencies: DependencyEdge2 list
          Coherence: CoherenceEntry list }

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

    // --- Real-schema version grammar (feature 042; research R3). BCL regex only,
    // mirroring scripts/validate-registry.py exactly so the two cannot disagree. ---

    /// `version` / `package-version`: full SemVer with optional prerelease/build
    /// (`1.0.0`, `0.1.52-preview.1`) OR a bare integer (`1`, `2`).
    let private semVerRegex =
        System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")

    let private bareIntegerRegex = System.Text.RegularExpressions.Regex(@"^\d+$")

    /// `range`: permissive comparator/shorthand set (`1.x`, `>=1.0.0 <2.0.0`).
    let private rangeRegex = System.Text.RegularExpressions.Regex(@"^[\d.xX*\s<>=~^|.-]+$")

    let private isValidVersion (text: string) =
        not (isBlank text) && (semVerRegex.IsMatch text || bareIntegerRegex.IsMatch text)

    let private isValidRange (text: string) =
        not (isBlank text) && rangeRegex.IsMatch text

    // --- The real-schema pure validator (research R4; mirrors the Python authority). ---

    let validateDocument (document: RegistryDocument) : ValidationResult =
        // Reference set: declared repo ids. `owner` additionally allows `github`
        // (the org repo, owner of `shared-build-config`), matching the authority.
        let repoIds =
            document.Repos
            |> List.choose (fun r -> if isBlank r.Id then None else Some r.Id)
            |> Set.ofList

        let ownerIds = repoIds |> Set.add "github"

        // root: repos / contracts must be non-empty. (SchemaVersion is structurally
        // an int via the typed model; a non-integer is rejected at the load edge.)
        let rootDiagnostics =
            [ if document.Repos.IsEmpty then
                  { Entry = "<root>"
                    Rule = MissingField "repos"
                    Message = "Registry document has no 'repos'." }
              if document.Contracts.IsEmpty then
                  { Entry = "<root>"
                    Rule = MissingField "contracts"
                    Message = "Registry document has no 'contracts'." } ]

        // repos (file order): each repo needs a non-blank id, name, role.
        let repoDiagnostics =
            document.Repos
            |> List.collect (fun r ->
                let entry = if isBlank r.Id then "<unnamed repo>" else r.Id

                [ if isBlank r.Id then
                      { Entry = entry
                        Rule = MissingField "id"
                        Message = "Repo entry is missing a non-blank key/'id'." }
                  if isBlank r.Name then
                      { Entry = entry
                        Rule = MissingField "name"
                        Message = $"Repo '{entry}' is missing a non-blank 'name'." }
                  if isBlank r.Role then
                      { Entry = entry
                        Rule = MissingField "role"
                        Message = $"Repo '{entry}' is missing a non-blank 'role'." } ])

        // contracts (file order): structural + reference + version rules. Duplicate
        // detection walks in order, flagging the second+ occurrence of an id.
        let mutable seenIds = Set.empty

        let contractDiagnostics =
            document.Contracts
            |> List.collect (fun c ->
                let entry = if isBlank c.Id then "<unnamed contract>" else c.Id

                let duplicate =
                    not (isBlank c.Id) && Set.contains c.Id seenIds

                if not (isBlank c.Id) then
                    seenIds <- Set.add c.Id seenIds

                [ if isBlank c.Id then
                      { Entry = entry
                        Rule = MissingField "id"
                        Message = "Contract entry is missing a non-blank 'id'." }
                  if duplicate then
                      { Entry = entry
                        Rule = DuplicateComponent
                        Message = $"Contract '{entry}' has a duplicate 'id'." }

                  if isBlank c.Version then
                      { Entry = entry
                        Rule = MissingField "version"
                        Message = $"Contract '{entry}' is missing a non-blank 'version'." }
                  elif not (isValidVersion c.Version) then
                      { Entry = entry
                        Rule = MalformedVersion
                        Message = $"Contract '{entry}' has a malformed 'version': '{c.Version}'." }

                  if isBlank c.Owner then
                      { Entry = entry
                        Rule = MissingField "owner"
                        Message = $"Contract '{entry}' is missing a non-blank 'owner'." }
                  elif not (Set.contains c.Owner ownerIds) then
                      { Entry = entry
                        Rule = UnknownComponent
                        Message = $"Contract '{entry}' has an unknown 'owner': '{c.Owner}'." }

                  if isBlank c.Surface then
                      { Entry = entry
                        Rule = MissingField "surface"
                        Message = $"Contract '{entry}' is missing a non-blank 'surface'." }

                  if c.Consumers.IsEmpty then
                      { Entry = entry
                        Rule = MissingField "consumers"
                        Message = $"Contract '{entry}' is missing a non-empty 'consumers'." }
                  else
                      for consumer in c.Consumers do
                          if isBlank consumer then
                              { Entry = entry
                                Rule = MissingField "consumers"
                                Message = $"Contract '{entry}' has a blank 'consumers' entry." }
                          elif not (Set.contains consumer repoIds) then
                              { Entry = entry
                                Rule = UnknownComponent
                                Message = $"Contract '{entry}' lists an unknown consumer '{consumer}'." }

                  match c.PackageVersion with
                  | Some pv when not (isValidVersion pv) ->
                      { Entry = entry
                        Rule = MalformedVersion
                        Message = $"Contract '{entry}' has a malformed 'package-version': '{pv}'." }
                  | _ -> ()

                  match c.Range with
                  | Some range when not (isValidRange range) ->
                      { Entry = entry
                        Rule = MalformedVersion
                        Message = $"Contract '{entry}' has a malformed 'range': '{range}'." }
                  | _ -> () ])

        // dependencies (file order): from/to must be present repo ids. `via` is
        // free-text and is NOT contract-checked (research R4).
        let dependencyDiagnostics =
            document.Dependencies
            |> List.collect (fun e ->
                let label f t =
                    let f = if isBlank f then "<blank>" else f
                    let t = if isBlank t then "<blank>" else t
                    $"{f} -> {t}"

                let entry = label e.From e.To

                [ if isBlank e.From then
                      { Entry = entry
                        Rule = MissingField "from"
                        Message = $"Dependency edge '{entry}' is missing a non-blank 'from'." }
                  elif not (Set.contains e.From repoIds) then
                      { Entry = entry
                        Rule = UnknownComponent
                        Message = $"Dependency edge '{entry}' references an unknown 'from' repo '{e.From}'." }

                  if isBlank e.To then
                      { Entry = entry
                        Rule = MissingField "to"
                        Message = $"Dependency edge '{entry}' is missing a non-blank 'to'." }
                  elif not (Set.contains e.To repoIds) then
                      { Entry = entry
                        Rule = UnknownComponent
                        Message = $"Dependency edge '{entry}' references an unknown 'to' repo '{e.To}'." } ])

        // coherence (file order): each entry needs a non-blank id. `coherent` is a
        // bool via the typed model.
        let coherenceDiagnostics =
            document.Coherence
            |> List.collect (fun co ->
                let entry = if isBlank co.Id then "<unnamed coherence>" else co.Id

                [ if isBlank co.Id then
                      { Entry = entry
                        Rule = MissingField "id"
                        Message = "Coherence entry is missing a non-blank 'id'." } ])

        match
            rootDiagnostics
            @ repoDiagnostics
            @ contractDiagnostics
            @ dependencyDiagnostics
            @ coherenceDiagnostics
        with
        | [] -> Valid
        | diagnostics -> Invalid diagnostics
