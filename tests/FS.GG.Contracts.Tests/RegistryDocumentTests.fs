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
        { Id = id
          Name = "FS.GG." + id
          Role = "role-" + id }

    let private contract id : Registry.ContractEntry =
        Registry.ContractEntry(
            Id = id,
            Version = "1.0.0",
            Owner = "sdd",
            Surface = "surface",
            Consumers = Registry.ConsumersDeclared [ "templates" ],
            WireContract = Registry.WireUnspecified,
            PackageVersion = None,
            Range = None
        )

    /// FS.GG.SDD#610: `ContractEntry` is a class, so the record copy-update `{ e with F = v }`
    /// these tests leaned on is gone. Each `contract "x"` above is a fresh instance, so this
    /// mutates it in place and hands it back — the readable stand-in for the copy-update at the
    /// call sites below (`contract "a" |> editContract (fun c -> c.Range <- Some "1.x")`).
    let private editContract (mutate: Registry.ContractEntry -> unit) (e: Registry.ContractEntry) =
        mutate e
        e

    /// A coherent representative document (sdd/templates/governance repos), used as
    /// the base for the broken-case mutations below.
    let private baseDoc: Registry.RegistryDocument =
        { SchemaVersion = 1
          Repos = [ repo "sdd"; repo "templates"; repo "governance" ]
          Contracts = [ contract "alpha" ]
          Dependencies =
            [ { From = "templates"
                To = "sdd"
                Via = "free text" } ]
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
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c ->
                          c.Owner <- "nope"
                          c.Version <- "abc"
                          c.Range <- Some "??") ] }

        Assert.Equal(Registry.validateDocument broken, Registry.validateDocument broken)

    // --- US2: reference rules (FR-003 / FR-006 / SC-002) ---

    [<Fact>]
    let ``US2: repo-to-repo dependency edge is accepted`` () =
        let doc =
            { baseDoc with
                Dependencies =
                    [ { From = "sdd"
                        To = "governance"
                        Via = "via" } ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US2: dependency edge to a non-repo id reports UnknownComponent`` () =
        let doc =
            { baseDoc with
                Dependencies =
                    [ { From = "sdd"
                        To = "nope"
                        Via = "via" } ] }

        Assert.Contains(Registry.UnknownComponent, rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: owner 'github' is accepted (repo ids plus github)`` () =
        let doc =
            { baseDoc with
                Contracts = [ contract "alpha" |> editContract (fun c -> c.Owner <- "github") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US2: consumer that is not a repo id reports UnknownComponent`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersDeclared [ "ghost" ]) ] }

        Assert.Contains(Registry.UnknownComponent, rules (Registry.validateDocument doc))

    // --- The three-state `consumers` declaration (FS.GG.SDD#508) ---
    //
    // The rule these cover had NO test in either direction before this feature: the
    // `MissingField "consumers"` branch was unexercised code, and the `isBlank` arm beside
    // it was unreachable through the YAML edge. They are the first coverage it has ever had.

    /// The case the major exists for. `[]` is a real answer — "nothing consumes this" — and
    /// a producer whose package no repo restores (ADR-0039 §5) has no other honest row.
    [<Fact>]
    let ``US2: an explicitly EMPTY consumers is Valid - nothing consumes this is an answer`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersDeclared []) ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    /// …and it stays Valid with a `package-version`, which is the actual shipping case
    /// (`new-sdd-workspace`). Deliberately NOT coupled: the request proposed allowing empty
    /// only for package-bearing contracts, but `package-version` (inventory) and `consumers`
    /// (graph) are orthogonal in every gate that exists, so no rule keys on the pair.
    [<Fact>]
    let ``US2: an empty consumers is Valid on a package-bearing contract too`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c ->
                          c.Consumers <- Registry.ConsumersDeclared []
                          c.PackageVersion <- Some "1.2.3") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    /// The other half, and the half that keeps the first one safe: ABSENT is still refused.
    /// If this ever goes green, a row that simply forgot `consumers:` validates as one that
    /// deliberately has none — and `fsgg-surface-impact` then routes zero consumer-impact
    /// issues for a breaking change while printing "(none declared)".
    [<Fact>]
    let ``US2: an ABSENT consumers still reports MissingField - absent is not empty`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersUnspecified) ] }

        Assert.Contains(Registry.MissingField "consumers", rules (Registry.validateDocument doc))

    /// The pair, asserted together on one document. This is the feature in one line: the two
    /// states must not collapse, and a test that only checked them apart would pass under a
    /// validator that had merged them.
    [<Fact>]
    let ``US2: absent and empty consumers are DIFFERENT verdicts on the same document`` () =
        let withEmpty =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersDeclared []) ] }

        let withAbsent =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersUnspecified) ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument withEmpty)
        Assert.NotEqual(Registry.validateDocument withEmpty, Registry.validateDocument withAbsent)

    /// A present-but-unparseable declaration is its OWN fault, reported as `MalformedField`
    /// rather than collapsed into either neighbour. Collapsing it into `Unspecified` would
    /// tell the author they forgot a line that is right there; collapsing it into
    /// `Declared []` would — now that empty is legal — pass a typo off as a deliberate
    /// "nothing consumes this".
    [<Fact>]
    let ``US2: a malformed consumers reports MalformedField, not Missing and not Valid`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersMalformed "'sdd'") ] }

        let reported = rules (Registry.validateDocument doc)

        Assert.Contains(Registry.MalformedField "consumers", reported)
        Assert.DoesNotContain(Registry.MissingField "consumers", reported)

    /// The `isBlank` arm, reachable for the first time. It has been in `validateDocument`
    /// since feature 042 and the YAML edge filtered blanks before it could ever fire.
    [<Fact>]
    let ``US2: a blank consumers entry reports MissingField, and does not read as empty`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "alpha"
                      |> editContract (fun c -> c.Consumers <- Registry.ConsumersDeclared [ "" ]) ] }

        Assert.Contains(Registry.MissingField "consumers", rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: dropped owner reports MissingField`` () =
        let doc =
            { baseDoc with
                Contracts = [ contract "alpha" |> editContract (fun c -> c.Owner <- "") ] }

        Assert.Contains(Registry.MissingField "owner", rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US2: duplicate contract id reports DuplicateComponent`` () =
        let doc =
            { baseDoc with
                Contracts = [ contract "dup"; contract "dup" ] }

        Assert.Contains(Registry.DuplicateComponent, rules (Registry.validateDocument doc))

    // --- US3: version grammar (FR-004 / FR-005 / FR-006 / SC-002) ---

    [<Fact>]
    let ``US3: bare-integer and prerelease versions are accepted`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "a" |> editContract (fun c -> c.Version <- "1")
                      contract "b" |> editContract (fun c -> c.Version <- "2")
                      contract "c" |> editContract (fun c -> c.Version <- "0.1.52-preview.1") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US3: shorthand range 1.x is accepted`` () =
        let doc =
            { baseDoc with
                Contracts = [ contract "a" |> editContract (fun c -> c.Range <- Some "1.x") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    // --- Feature 045: 4-segment versions accepted (FR-001, FR-002); the widening is
    // bounded to one extra numeric segment so genuine defects still fail (FR-004). ---

    [<Fact>]
    let ``US3-045: 4-segment version 1.2.1.1 is accepted on both version and package-version`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "a"
                      |> editContract (fun c ->
                          c.Version <- "1.2.1.1"
                          c.PackageVersion <- Some "1.2.1.1") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``US3-045: 4-segment version composes with a prerelease tag (1.2.1.1-preview.1)`` () =
        let doc =
            { baseDoc with
                Contracts =
                    [ contract "a" |> editContract (fun c -> c.Version <- "1.2.1.1-preview.1") ] }

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Theory>]
    [<InlineData("1.2.x.4")>]
    [<InlineData("abc")>]
    [<InlineData("1.2.3.4.5")>]
    let ``US3: genuinely malformed version still reports MalformedVersion`` (bad: string) =
        let doc =
            { baseDoc with
                Contracts = [ contract "a" |> editContract (fun c -> c.Version <- bad) ] }

        Assert.Contains(Registry.MalformedVersion, rules (Registry.validateDocument doc))

    [<Fact>]
    let ``US3: malformed range still reports MalformedVersion`` () =
        let doc =
            { baseDoc with
                Contracts = [ contract "a" |> editContract (fun c -> c.Range <- Some "??") ] }

        Assert.Contains(Registry.MalformedVersion, rules (Registry.validateDocument doc))

    // --- The three-state `wire-contract` dimension (FS.GG.SDD#589 / ADR-0052) ---
    //
    // Each arm below goes red if its diagnostic branch in `validateDocument` is disabled
    // (SC / FR: every rule has a test that fails when its arm is removed). The `contract`
    // helper declares `WireUnspecified`, so the base document already covers "absent is not
    // a fault"; the coherent-document test above is that arm's red-when-broken guard.

    let private withWire wire =
        { baseDoc with
            Contracts =
                [ contract "alpha" |> editContract (fun c -> c.WireContract <- wire) ] }

    /// Absent wire contract: NOT a fault. Most contracts have no wire dimension.
    [<Fact>]
    let ``wire: an absent wire-contract is Valid - most contracts are not networked`` () =
        Assert.Equal(Registry.Valid, Registry.validateDocument (withWire Registry.WireUnspecified))

    /// Provenance 1 — vendored external `.proto`, versioned independently of the source.
    [<Fact>]
    let ``wire: a well-formed vendored-proto is Valid`` () =
        let doc =
            withWire (Registry.WireDeclared(Registry.VendoredProto("Blizzard/s2client-proto", "5.0.12")))

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``wire: vendored-proto missing upstream reports MissingField naming the field`` () =
        let doc = withWire (Registry.WireDeclared(Registry.VendoredProto("", "5.0.12")))
        Assert.Contains(Registry.MissingField "wire-contract.upstream", rules (Registry.validateDocument doc))

    [<Fact>]
    let ``wire: vendored-proto missing upstream-version reports MissingField`` () =
        let doc =
            withWire (Registry.WireDeclared(Registry.VendoredProto("Blizzard/s2client-proto", "")))

        Assert.Contains(Registry.MissingField "wire-contract.upstream-version", rules (Registry.validateDocument doc))

    /// The independent version is a version, so a non-SemVer one is MalformedVersion — the
    /// same grammar `version` / `package-version` are held to.
    [<Fact>]
    let ``wire: vendored-proto with a non-SemVer upstream-version reports MalformedVersion`` () =
        let doc =
            withWire (Registry.WireDeclared(Registry.VendoredProto("Blizzard/s2client-proto", "not-a-version")))

        Assert.Contains(Registry.MalformedVersion, rules (Registry.validateDocument doc))

    /// Provenance 2 — owned `.proto`; the file is the compatibility surface.
    [<Fact>]
    let ``wire: a well-formed owned-proto is Valid`` () =
        let doc = withWire (Registry.WireDeclared(Registry.OwnedProto("protos/bar.proto")))
        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``wire: owned-proto missing proto reports MissingField naming the field`` () =
        let doc = withWire (Registry.WireDeclared(Registry.OwnedProto("")))
        Assert.Contains(Registry.MissingField "wire-contract.proto", rules (Registry.validateDocument doc))

    /// Provenance 3 — code-first protobuf-net; no `.proto`, the F# types are the contract.
    [<Fact>]
    let ``wire: a well-formed code-first-protobuf-net is Valid`` () =
        let doc =
            withWire (Registry.WireDeclared(Registry.CodeFirstProtobufNet("src/FS.GG.Net/Wire.fsi")))

        Assert.Equal(Registry.Valid, Registry.validateDocument doc)

    [<Fact>]
    let ``wire: code-first-protobuf-net missing surface reports MissingField naming the field`` () =
        let doc = withWire (Registry.WireDeclared(Registry.CodeFirstProtobufNet("")))
        Assert.Contains(Registry.MissingField "wire-contract.surface", rules (Registry.validateDocument doc))

    /// A present-but-unparseable declaration is its OWN fault — reported as `MalformedField`,
    /// never collapsed into `WireUnspecified` (a phantom "no wire contract") or a guessed
    /// provenance. Mirror of the `consumers` malformed case.
    [<Fact>]
    let ``wire: a malformed wire-contract reports MalformedField, not Missing and not Valid`` () =
        let doc = withWire (Registry.WireMalformed "unknown provenance 'grpc'")
        let reported = rules (Registry.validateDocument doc)

        Assert.Contains(Registry.MalformedField "wire-contract", reported)
        Assert.NotEqual(Registry.Valid, Registry.validateDocument doc)
