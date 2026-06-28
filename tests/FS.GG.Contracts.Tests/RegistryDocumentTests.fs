namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

/// Feature 042: pure `Registry.validateDocument` over constructed real-schema
/// documents. The real-file "validates clean" evidence lives in
/// FS.GG.SDD.Artifacts.Tests (which owns the YAML `load` edge); here we exercise the
/// rule pairs (US2 references, US3 version grammar) and determinism on the pure
/// function with explicit, ugly literals — no I/O.
module RegistryDocumentTests =

    let private repo id : Registry.RegistryRepo =
        { Id = id; Name = "FS.GG." + id; Role = "role-" + id }

    let private contract id : Registry.ContractEntry =
        { Id = id
          Version = "1.0.0"
          Owner = "sdd"
          Surface = "surface"
          Consumers = [ "templates" ]
          PackageVersion = None
          Range = None }

    /// A coherent representative document (sdd/templates/governance repos), used as
    /// the base for the broken-case mutations below.
    let private baseDoc: Registry.RegistryDocument =
        { SchemaVersion = 1
          Repos = [ repo "sdd"; repo "templates"; repo "governance" ]
          Contracts = [ contract "alpha" ]
          Dependencies = [ { From = "templates"; To = "sdd"; Via = "free text" } ]
          Coherence = [ { Id = "c1"; Coherent = true } ] }

    let private rules result =
        match result with
        | Registry.Valid -> []
        | Registry.Invalid diagnostics -> diagnostics |> List.map (fun d -> d.Rule)

    // --- Structural happy path + determinism (FR-007 / SC-004) ---

    [<Fact>]
    let ``coherent document is Valid`` () =
        Assert.Equal(Registry.Valid, Registry.validateDocument baseDoc)

    [<Fact>]
    let ``determinism: identical input yields identical diagnostics`` () =
        let broken =
            { baseDoc with
                Contracts = [ { contract "alpha" with Owner = "nope"; Version = "abc"; Range = Some "??" } ] }

        Assert.Equal(Registry.validateDocument broken, Registry.validateDocument broken)

    // --- US2: reference rules (FR-003 / FR-006 / SC-002) ---

    [<Fact>]
    let ``US2: repo-to-repo dependency edge is accepted`` () =
        let doc = { baseDoc with Dependencies = [ { From = "sdd"; To = "governance"; Via = "via" } ] }
        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US2: dependency edge to a non-repo id reports UnknownComponent`` () =
        let doc = { baseDoc with Dependencies = [ { From = "sdd"; To = "nope"; Via = "via" } ] }
        Assert.Contains(Registry.UnknownComponent, rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: owner 'github' is accepted (repo ids plus github)`` () =
        let doc = { baseDoc with Contracts = [ { contract "alpha" with Owner = "github" } ] }
        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US2: consumer that is not a repo id reports UnknownComponent`` () =
        let doc = { baseDoc with Contracts = [ { contract "alpha" with Consumers = [ "ghost" ] } ] }
        Assert.Contains(Registry.UnknownComponent, rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: dropped owner reports MissingField`` () =
        let doc = { baseDoc with Contracts = [ { contract "alpha" with Owner = "" } ] }
        Assert.Contains(Registry.MissingField "owner", rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: duplicate contract id reports DuplicateComponent`` () =
        let doc = { baseDoc with Contracts = [ contract "dup"; contract "dup" ] }
        Assert.Contains(Registry.DuplicateComponent, rules (Registry.validateDocument doc))

    // --- US3: version grammar (FR-004 / FR-005 / FR-006 / SC-002) ---

    [<Fact>]
    let ``US3: bare-integer and prerelease versions are accepted`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ { contract "a" with Version = "1" }
                      { contract "b" with Version = "2" }
                      { contract "c" with Version = "0.1.52-preview.1" } ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US3: shorthand range 1.x is accepted`` () =
        let doc = { baseDoc with Contracts = [ { contract "a" with Range = Some "1.x" } ] }
        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Theory>]
    [<InlineData("1.2.x.4")>]
    [<InlineData("abc")>]
    let ``US3: genuinely malformed version still reports MalformedVersion`` (bad: string) =
        let doc = { baseDoc with Contracts = [ { contract "a" with Version = bad } ] }
        Assert.Contains(Registry.MalformedVersion, rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US3: malformed range still reports MalformedVersion`` () =
        let doc = { baseDoc with Contracts = [ { contract "a" with Range = Some "??" } ] }
        Assert.Contains(Registry.MalformedVersion, rules (Registry.validateDocument doc))
