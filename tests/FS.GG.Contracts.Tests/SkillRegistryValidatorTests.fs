namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

/// Feature 104: the pure `Registry.validateSkillRegistry` over the org skill catalog
/// (`registry/skills.yml`). The load edge is tested in FS.GG.SDD.Artifacts.Tests; these
/// tests are about the RULES, and above all about the one the feature exists for —
/// an absent `mirrored:` is NOT `false`.
module SkillRegistryValidatorTests =

    let private row id : Registry.SkillRegistryEntry =
        { Id = id
          Scope = "product"
          Owner = "fs-gg-game"
          Source = $"FS.GG.Game/template/product-skills/{id}/SKILL.md"
          Sha256 = String.replicate 64 "a"
          Mirrored = Registry.MirrorUnspecified
          MaterializesWhen = Some "profile in [game]" }

    let private doc (skills: Registry.SkillRegistryEntry list) : Registry.SkillRegistryDocument =
        { SchemaVersion = 1
          Parameters = [ "profile" ]
          Skills = skills }

    let private diagnosticsOf result =
        match result with
        | Registry.Valid -> []
        | Registry.Invalid diagnostics -> diagnostics

    let private rulesOf result =
        diagnosticsOf result |> List.map (fun d -> d.Rule)

    let private validateOf skills =
        Registry.validateSkillRegistry (doc skills)

    // --- AC-002 / FR-002: the whole point. Absent is not false. ---

    /// The three states are DISTINCT VALUES. If this ever compiles-and-passes with
    /// `Mirrored: bool`, the model has been flattened and the fail-open is back.
    [<Fact>]
    let ``an absent mirrored is Unspecified, and Unspecified is not Declared false`` () =
        Assert.NotEqual(Registry.MirrorUnspecified, Registry.MirrorDeclared false)
        Assert.NotEqual(Registry.MirrorDeclared true, Registry.MirrorDeclared false)
        Assert.NotEqual<Registry.MirrorDeclaration>(Registry.MirrorUnspecified, Registry.MirrorDeclared true)

    /// An unclassified body is NOT a fault. 33 of the catalog's 41 rows carry no verdict,
    /// and DEMANDING one would be the mirror-image of coercing absent to `false`: inventing
    /// an answer the owner never gave. Only an UNPARSEABLE answer is reported.
    [<Fact>]
    let ``an unclassified row is valid - absence is not a diagnostic`` () =
        let result =
            validateOf
                [ { row "fs-gg-ai" with
                      Mirrored = Registry.MirrorUnspecified } ]

        Assert.Equal(Registry.Valid, result)

    [<Fact>]
    let ``a declared true and a declared false are both valid, and both survive as declared`` () =
        let skills =
            [ { row "fs-gg-game-core" with
                  Mirrored = Registry.MirrorDeclared true }
              { row "fs-gg-ballistics" with
                  Mirrored = Registry.MirrorDeclared false } ]

        Assert.Equal(Registry.Valid, Registry.validateSkillRegistry (doc skills))

    // --- AC-003 / FR-003: a present-but-unparseable verdict is a DIAGNOSTIC, not a shrug. ---

    [<Fact>]
    let ``a malformed mirrored is a MalformedField diagnostic naming the row`` () =
        let result =
            Registry.validateSkillRegistry (
                doc
                    [ { row "fs-gg-ai" with
                          Mirrored = Registry.MirrorMalformed "yes" } ]
            )

        match diagnosticsOf result with
        | [ d ] ->
            Assert.Equal(Registry.MalformedField "mirrored", d.Rule)
            Assert.Equal("fs-gg-ai", d.Entry)
            Assert.Contains("'yes'", d.Message)
        | other -> failwith $"expected exactly one MalformedField diagnostic, got {List.length other}"

    /// The failure leg must FAIL. If the `MirrorMalformed` arm of the validator is deleted,
    /// this is the test that goes red — an unparseable verdict would otherwise pass as
    /// silently as an absent one, which is the defect in a different coat.
    [<Fact>]
    let ``a malformed mirrored is NOT silently read as unspecified`` () =
        let malformed =
            Registry.validateSkillRegistry (
                doc
                    [ { row "fs-gg-ai" with
                          Mirrored = Registry.MirrorMalformed "" } ]
            )

        let unspecified =
            Registry.validateSkillRegistry (
                doc
                    [ { row "fs-gg-ai" with
                          Mirrored = Registry.MirrorUnspecified } ]
            )

        Assert.Equal(Registry.Valid, unspecified)
        Assert.NotEqual(Registry.Valid, malformed)

    // --- AC-007 / FR-006: the per-row rules. ---

    [<Fact>]
    let ``a blank id is a MissingField`` () =
        let result = Registry.validateSkillRegistry (doc [ { row "x" with Id = "" } ])
        Assert.Contains(Registry.MissingField "id", rulesOf result)

    [<Fact>]
    let ``a duplicate id is a DuplicateComponent on the second occurrence`` () =
        let result = Registry.validateSkillRegistry (doc [ row "fs-gg-ai"; row "fs-gg-ai" ])

        match diagnosticsOf result with
        | [ d ] ->
            Assert.Equal(Registry.DuplicateComponent, d.Rule)
            Assert.Equal("fs-gg-ai", d.Entry)
        | other -> failwith $"expected exactly one duplicate diagnostic, got {List.length other}"

    [<Fact>]
    let ``an unknown scope is an UnknownComponent, and its message names driver`` () =
        // FR-004 / AC-003: an unrecognized scope still fails, and the message enumerates all
        // three accepted scopes — including the one this feature adds. Naming `driver` in the
        // message is the reader-facing half of teaching the vocabulary; drop it from the
        // enumeration and this test goes red.
        let result =
            Registry.validateSkillRegistry (doc [ { row "x" with Scope = "nonsense" } ])

        match diagnosticsOf result with
        | [ d ] ->
            Assert.Equal(Registry.UnknownComponent, d.Rule)
            Assert.Contains("'driver'", d.Message)
        | other -> failwith $"expected exactly one UnknownComponent diagnostic, got {List.length other}"

    // --- FR-001/FR-002/AC-001/AC-004: the driver class is KNOWN. Adding it is a monotone
    // loosening — `process`/`product` still pass, and `driver` now joins them. ---

    [<Theory>]
    [<InlineData "process">]
    [<InlineData "product">]
    [<InlineData "driver">]
    let ``the declared scopes are accepted`` (scope: string) =
        Assert.Equal(Registry.Valid, Registry.validateSkillRegistry (doc [ { row "x" with Scope = scope } ]))

    /// FR-003 / AC-002: the full driver SHAPE ADR-0054 describes — `scope: driver`, a
    /// NON-PRODUCER owner (`.github`), and a COMPOSED `materializes-when` (the AND of two
    /// producer predicates) — validates in step 1. Nothing here is *enforced* (a `driver`
    /// row is not REQUIRED to look like this yet, ADR-0037 §3); the point is that this shape,
    /// which is what `.github` will emit in step 2, is accepted rather than rejected.
    [<Fact>]
    let ``a driver row with a .github owner and a composed materializes-when is valid`` () =
        let driverRow =
            { row "fs-gg-sdd-driver" with
                Scope = "driver"
                Owner = ".github"
                MaterializesWhen = Some "has fs-gg-sdd-* and has fs-gg-feedback-*" }

        Assert.Equal(Registry.Valid, Registry.validateSkillRegistry (doc [ driverRow ]))

    [<Fact>]
    let ``a missing owner and a missing source are each a MissingField`` () =
        let result =
            Registry.validateSkillRegistry (doc [ { row "x" with Owner = ""; Source = "" } ])

        let rules = rulesOf result
        Assert.Contains(Registry.MissingField "owner", rules)
        Assert.Contains(Registry.MissingField "source", rules)

    [<Fact>]
    let ``a missing sha256 is a MissingField, and a malformed one is a MalformedField`` () =
        let missing = Registry.validateSkillRegistry (doc [ { row "x" with Sha256 = "" } ])
        Assert.Contains(Registry.MissingField "sha256", rulesOf missing)

        let malformed =
            Registry.validateSkillRegistry (doc [ { row "x" with Sha256 = "NOTHEX" } ])

        Assert.Contains(Registry.MalformedField "sha256", rulesOf malformed)

    /// `$` in .NET also matches before a TRAILING NEWLINE, so `^[0-9a-f]{64}$` would accept a
    /// 65-character digest ending in `\n`. The regex is anchored with `\z` for exactly this.
    [<Fact>]
    let ``a sha256 with a trailing newline is malformed - the anchor is \z, not $`` () =
        let result =
            Registry.validateSkillRegistry (
                doc
                    [ { row "x" with
                          Sha256 = String.replicate 64 "a" + "\n" } ]
            )

        Assert.Contains(Registry.MalformedField "sha256", rulesOf result)

    /// Uppercase hex is a hand-edit: the catalog is reconciled from producer manifests that
    /// emit lowercase, and a hand-edited digest is the one thing it must never carry.
    [<Fact>]
    let ``an uppercase sha256 is malformed`` () =
        let result =
            Registry.validateSkillRegistry (
                doc
                    [ { row "x" with
                          Sha256 = String.replicate 64 "A" } ]
            )

        Assert.Contains(Registry.MalformedField "sha256", rulesOf result)

    [<Fact>]
    let ``a blank materializes-when is malformed - omit the key for the always default`` () =
        let result =
            Registry.validateSkillRegistry (
                doc
                    [ { row "x" with
                          MaterializesWhen = Some "  " } ]
            )

        Assert.Contains(Registry.MalformedField "materializes-when", rulesOf result)

    /// Absent `materializes-when` means `always` (the catalog's own rule) — not a fault.
    [<Fact>]
    let ``an absent materializes-when is valid`` () =
        Assert.Equal(Registry.Valid, Registry.validateSkillRegistry (doc [ { row "x" with MaterializesWhen = None } ]))

    // --- root ---

    [<Fact>]
    let ``a catalog with no skills is a MissingField`` () =
        let result = Registry.validateSkillRegistry (doc [])
        Assert.Contains(Registry.MissingField "skills", rulesOf result)

    // --- determinism: diagnostics in document order ---

    [<Fact>]
    let ``diagnostics are reported in document order`` () =
        let result =
            Registry.validateSkillRegistry (
                doc
                    [ { row "first" with Scope = "nonsense" }
                      { row "second" with Sha256 = "NOTHEX" } ]
            )

        Assert.Equal<string list>([ "first"; "second" ], diagnosticsOf result |> List.map (fun d -> d.Entry))
