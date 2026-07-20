namespace Fsgg

module Registry =

    type RegistryComponent = { Id: string; Version: string }

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
        | MalformedField of fieldName: string

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

    /// THREE states, and the middle one is the point: `absent` is NOT `[]`. See Registry.fsi
    /// for why a bare `string list` (or a `string list option`) cannot express this honestly.
    type ConsumerDeclaration =
        | ConsumersUnspecified
        | ConsumersDeclared of consumers: string list
        | ConsumersMalformed of raw: string

    /// Three PROVENANCES of a wire contract (ADR-0052). See Registry.fsi for why the
    /// union is closed and an unknown provenance is `WireMalformed`, not a fourth case.
    type WireContract =
        | VendoredProto of upstream: string * upstreamVersion: string
        | OwnedProto of proto: string
        | CodeFirstProtobufNet of surface: string

    /// THREE states, and the reasons mirror `ConsumerDeclaration`: `absent` is NOT a
    /// declaration and a typo is NEITHER. See Registry.fsi for why a `WireContract option`
    /// cannot express this honestly.
    type WireContractDeclaration =
        | WireUnspecified
        | WireDeclared of WireContract
        | WireMalformed of raw: string

    /// FS.GG.SDD#610: a CLASS, not a record, so a NEW field is an additive property rather
    /// than a positional-ctor arity change. See Registry.fsi for the full rationale. It keeps
    /// record-like value semantics by overriding structural equality over its eight members.
    [<Sealed>]
    type ContractEntry() =
        member val Id: string = "" with get, set
        member val Version: string = "" with get, set
        member val Owner: string = "" with get, set
        member val Surface: string = "" with get, set
        member val Consumers: ConsumerDeclaration = ConsumersUnspecified with get, set
        member val WireContract: WireContractDeclaration = WireUnspecified with get, set
        member val PackageVersion: string option = None with get, set
        member val Range: string option = None with get, set

        override this.Equals(other: obj) =
            match other with
            | :? ContractEntry as o ->
                this.Id = o.Id
                && this.Version = o.Version
                && this.Owner = o.Owner
                && this.Surface = o.Surface
                && this.Consumers = o.Consumers
                && this.WireContract = o.WireContract
                && this.PackageVersion = o.PackageVersion
                && this.Range = o.Range
            | _ -> false

        override this.GetHashCode() =
            hash (
                this.Id,
                this.Version,
                this.Owner,
                this.Surface,
                this.Consumers,
                this.WireContract,
                this.PackageVersion,
                this.Range
            )

    type DependencyEdge2 =
        { From: string
          To: string
          Via: string }

    type CoherenceEntry = { Id: string; Coherent: bool }

    type RegistryDocument =
        { SchemaVersion: int
          Repos: RegistryRepo list
          Contracts: ContractEntry list
          Dependencies: DependencyEdge2 list
          Coherence: CoherenceEntry list }

    // --- Skill-registry document model (feature 104, additive; `registry/skills.yml`). ---

    /// THREE states, and the third is the point: `absent` is NOT `false`. See Registry.fsi
    /// for why a two-state `bool` (or a `bool option`) cannot express this honestly.
    type MirrorDeclaration =
        | MirrorUnspecified
        | MirrorDeclared of mirrored: bool
        | MirrorMalformed of raw: string

    type SkillRegistryEntry =
        { Id: string
          Scope: string
          Owner: string
          Source: string
          Sha256: string
          Mirrored: MirrorDeclaration
          MaterializesWhen: string option }

    type SkillRegistryDocument =
        { SchemaVersion: int
          Parameters: string list
          Skills: SkillRegistryEntry list }

    // --- Internal BCL-only SemVer helper (research R5; no third-party package). ---
    // The grammar now lives in the shared `Fsgg.Version` module (feature 052 D3);
    // these private helpers delegate so exactly one grammar exists in the repo.

    /// A parsed SemVer triple (major.minor.patch); pre-release/build metadata are
    /// out of scope for the registry coherence check.
    type private SemVer = Fsgg.Version.Version

    let private tryParseSemVer (text: string) : SemVer option = Fsgg.Version.tryParse text

    let private compareSemVer (a: SemVer) (b: SemVer) =
        match compare a.Major b.Major with
        | 0 ->
            match compare a.Minor b.Minor with
            | 0 -> compare a.Patch b.Patch
            | c -> c
        | c -> c

    /// One range comparator: an operator paired with its bound version.
    type private Comparator = { Op: string; Bound: SemVer }

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

    /// `version` / `package-version`: full SemVer with an optional 4th numeric
    /// segment and optional prerelease/build (`1.0.0`, `0.1.52-preview.1`,
    /// `1.2.1.1`, `1.2.1.1-preview.1`) OR a bare integer (`1`, `2`). The optional
    /// `(\.\d+)?` 4th segment (feature 045) mirrors scripts/validate-registry.py's
    /// `(?:\.\d+)?` byte-for-byte so the typed validator and the Python authority
    /// cannot disagree on the 4-segment `major.minor.patch.revision` form (ADR-0007).
    let private semVerRegex =
        System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")

    let private bareIntegerRegex = System.Text.RegularExpressions.Regex(@"^\d+$")

    /// `range`: permissive comparator/shorthand set (`1.x`, `>=1.0.0 <2.0.0`).
    let private rangeRegex =
        System.Text.RegularExpressions.Regex(@"^[\d.xX*\s<>=~^|.-]+$")

    let private isValidVersion (text: string) =
        not (isBlank text)
        && (semVerRegex.IsMatch text || bareIntegerRegex.IsMatch text)

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

                let duplicate = not (isBlank c.Id) && Set.contains c.Id seenIds

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

                  // FS.GG.SDD#508: an EMPTY declaration is valid and an ABSENT one is not.
                  // `ConsumersDeclared []` asserts "nothing consumes this" — the only honest
                  // row for a producer whose package no repo restores (ADR-0039 §5) — while
                  // `ConsumersUnspecified` is still the unanswered question it always was.
                  // The distinction is the entire feature; before it, the YAML edge collapsed
                  // both onto `[]` and this branch had to refuse the pair.
                  //
                  // Deliberately NOT gated on `PackageVersion.IsSome`, though the request
                  // proposed that: `package-version` (inventory — who is held to the feed,
                  // `check-feed-coherence.py`) and `consumers` (graph — who a surface mutation
                  // must flag, `fsgg-surface-impact`) are orthogonal in every gate that exists,
                  // and coupling them would invent a rule nothing enforces. "Nothing consumes
                  // this" is an honest claim for ANY contract; the three-state read is what
                  // makes it safe, not the package coupling.
                  match c.Consumers with
                  | ConsumersUnspecified ->
                      { Entry = entry
                        Rule = MissingField "consumers"
                        Message =
                          $"Contract '{entry}' is missing 'consumers'. Declare it — use an explicit '[]' to assert that nothing consumes this contract." }
                  | ConsumersMalformed raw ->
                      { Entry = entry
                        Rule = MalformedField "consumers"
                        Message = $"Contract '{entry}' has a 'consumers' that is not a list: {raw}." }
                  | ConsumersDeclared consumers ->
                      for consumer in consumers do
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
                  | _ -> ()

                  // FS.GG.SDD#589 / ADR-0052: the optional wire-contract dimension. Three
                  // provenances, each with its own required fields, and the same three-state
                  // read `consumers` uses: absent is NOT a fault (most contracts have no wire
                  // dimension), a present-but-unparseable declaration IS one, and a declared
                  // provenance is checked for the fields that provenance makes load-bearing.
                  // `WireUnspecified` yields nothing, exactly as an absent `range` does.
                  match c.WireContract with
                  | WireUnspecified -> ()
                  | WireMalformed raw ->
                      { Entry = entry
                        Rule = MalformedField "wire-contract"
                        Message =
                          $"Contract '{entry}' has a 'wire-contract' that is present but unparseable: {raw}. Declare a 'provenance' of 'vendored-proto', 'owned-proto', or 'code-first-protobuf-net'." }
                  | WireDeclared(VendoredProto(upstream, upstreamVersion)) ->
                      // The vendored upstream ref and its OWN version — both load-bearing:
                      // the ref says which upstream the bytes match, the version pins it,
                      // and it is validated as a version because it is one (independent of
                      // the component's `version`).
                      if isBlank upstream then
                          { Entry = entry
                            Rule = MissingField "wire-contract.upstream"
                            Message =
                              $"Contract '{entry}' declares a vendored-proto wire contract but is missing a non-blank 'upstream'." }

                      if isBlank upstreamVersion then
                          { Entry = entry
                            Rule = MissingField "wire-contract.upstream-version"
                            Message =
                              $"Contract '{entry}' declares a vendored-proto wire contract but is missing a non-blank 'upstream-version'." }
                      elif not (isValidVersion upstreamVersion) then
                          { Entry = entry
                            Rule = MalformedVersion
                            Message =
                              $"Contract '{entry}' has a malformed vendored-proto 'upstream-version': '{upstreamVersion}'." }
                  | WireDeclared(OwnedProto proto) ->
                      // The owned `.proto` file IS the compatibility surface (field-number /
                      // `reserved` discipline), so its path must be named.
                      if isBlank proto then
                          { Entry = entry
                            Rule = MissingField "wire-contract.proto"
                            Message =
                              $"Contract '{entry}' declares an owned-proto wire contract but is missing a non-blank 'proto'." }
                  | WireDeclared(CodeFirstProtobufNet surface) ->
                      // No `.proto`: the F# `[<ProtoContract>]` types are the contract, so the
                      // type surface that carries the field numbers must be named.
                      if isBlank surface then
                          { Entry = entry
                            Rule = MissingField "wire-contract.surface"
                            Message =
                              $"Contract '{entry}' declares a code-first-protobuf-net wire contract but is missing a non-blank 'surface'." } ])

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

    // --- The skill-registry pure validator (feature 104; `registry/skills.yml`). ---

    /// A canonical-body digest: 64 lowercase hex, byte-equivalent to `sha256sum SKILL.md`.
    /// Uppercase is rejected deliberately — the catalog is reconciled from producer
    /// manifests that emit lowercase, so an uppercase digest is a hand-edit, and a
    /// hand-edited digest is the one thing this catalog must never carry.
    ///
    /// Anchored with `\z`, not `$`: in .NET `$` ALSO matches immediately before a trailing
    /// newline, so `^[0-9a-f]{64}$` would accept a 65-character digest ending in `\n` — a
    /// digest that is not 64 hex characters, passing a check whose whole job is to say so.
    let private sha256Regex = System.Text.RegularExpressions.Regex(@"\A[0-9a-f]{64}\z")

    // `scope` is validated STRUCTURALLY, not against a compiled-in vocabulary (ADR-0061,
    // Option (b)). A non-blank `scope` token is a well-formed opaque string; adding a scope
    // value (`driver` — ADR-0054; `operator` — ADR-0057; whatever comes next) is no longer
    // "schema growth" — it needs no CLI republish, no `schemaVersion` bump, and no pin
    // advance. This retired the ADR-0037 §3 "known, not enforced" rail whose exhaustive enum
    // cost an ADR + a step-1 teach-and-publish + a step-2 `.github` bump+pin per new value.
    //
    // The fail-closed discipline that earns its keep stays: a BLANK scope is still an error
    // (the `isBlank` branch below), as is every other structural/malformed check. What stops
    // being an error is a token this validator was not recompiled to know. SEMANTIC
    // enforcement — that a given scope's row carries the shape its meaning requires — moves
    // to the consumer that materializes the token, where its meaning lives (ADR-0061
    // trade-off; ADR-0058 "gate the capability, not the declaration").

    let validateSkillRegistry (document: SkillRegistryDocument) : ValidationResult =
        // root: a catalog with no skills is not a catalog. (`schemaVersion` is
        // structurally an int via the typed model; a non-integer is rejected at the
        // load edge. `parameters` may legitimately be empty.)
        let rootDiagnostics =
            [ if document.Skills.IsEmpty then
                  { Entry = "<root>"
                    Rule = MissingField "skills"
                    Message = "Skill registry document has no 'skills'." } ]

        // skills (file order). Duplicate detection walks in order, flagging the
        // second+ occurrence of an id — same shape as `contracts` above.
        let mutable seenIds = Set.empty

        let skillDiagnostics =
            document.Skills
            |> List.collect (fun s ->
                let entry = if isBlank s.Id then "<unnamed skill>" else s.Id

                let duplicate = not (isBlank s.Id) && Set.contains s.Id seenIds

                if not (isBlank s.Id) then
                    seenIds <- Set.add s.Id seenIds

                [ if isBlank s.Id then
                      { Entry = entry
                        Rule = MissingField "id"
                        Message = "Skill entry is missing a non-blank 'id'." }
                  if duplicate then
                      { Entry = entry
                        Rule = DuplicateComponent
                        Message = $"Skill '{entry}' has a duplicate 'id'." }

                  if isBlank s.Scope then
                      { Entry = entry
                        Rule = MissingField "scope"
                        Message = $"Skill '{entry}' is missing a non-blank 'scope'." }

                  if isBlank s.Owner then
                      { Entry = entry
                        Rule = MissingField "owner"
                        Message = $"Skill '{entry}' is missing a non-blank 'owner'." }

                  if isBlank s.Source then
                      { Entry = entry
                        Rule = MissingField "source"
                        Message = $"Skill '{entry}' is missing a non-blank 'source'." }

                  if isBlank s.Sha256 then
                      { Entry = entry
                        Rule = MissingField "sha256"
                        Message = $"Skill '{entry}' is missing a non-blank 'sha256'." }
                  elif not (sha256Regex.IsMatch s.Sha256) then
                      { Entry = entry
                        Rule = MalformedField "sha256"
                        Message =
                          $"Skill '{entry}' has a malformed 'sha256' (expected 64 lowercase hex): '{s.Sha256}'." }

                  // `mirrored` — the three-state field this feature exists for.
                  //
                  // ONLY the malformed arm is a diagnostic. `MirrorUnspecified` is NOT a
                  // fault: 33 of the catalog's rows legitimately carry no verdict, and
                  // demanding one would be the mirror-image error of coercing absent to
                  // `false` — inventing an answer where the owner gave none. What is
                  // reported is an UNPARSEABLE answer, which is neither an answer nor an
                  // absence, and which a `bool`-with-default would have silently swallowed.
                  match s.Mirrored with
                  | MirrorMalformed raw ->
                      { Entry = entry
                        Rule = MalformedField "mirrored"
                        Message =
                          $"Skill '{entry}' has a 'mirrored' that is present but not a boolean: '{raw}'. An unparseable verdict is not the same as an absent one — omit the key to leave the body unclassified, or declare true/false." }
                  | MirrorUnspecified
                  | MirrorDeclared _ -> ()

                  match s.MaterializesWhen with
                  | Some predicate when isBlank predicate ->
                      { Entry = entry
                        Rule = MalformedField "materializes-when"
                        Message =
                          $"Skill '{entry}' has a blank 'materializes-when'. Omit the key for the 'always' default rather than declaring an empty predicate." }
                  | _ -> () ])

        match rootDiagnostics @ skillDiagnostics with
        | [] -> Valid
        | diagnostics -> Invalid diagnostics
