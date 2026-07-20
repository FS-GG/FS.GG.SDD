namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module ContractVersionTests =

    // FR-012 / quickstart Scenario F: self-describing contract version. Additive
    // minor bump 1.0.1 → 1.1.0 (feature 042: new RegistryDocument model + validateDocument);
    // patch bump 1.1.0 → 1.1.1 (feature 045: widen semVerRegex to accept the 4-segment
    // version form — source behavior changes, no public surface change); additive minor
    // bump 1.1.1 → 1.2.0 (feature 052: new `Fsgg.Version` module + additive
    // `ProviderDescriptor.MinimumCliVersion` public surface).
    // Feature 057 / ADR-0014: additive minor bump 1.2.0 -> 1.3.0 (new skill-manifest types +
    // `agentSkillRoots` + additive `ScaffoldProducedPathEntry.Sha256` public surface).
    // Feature 058 / ADR-0014 P1: additive minor bump 1.3.0 -> 1.4.0 (new public `Fsgg.SkillMirror`
    // materialize-and-verify module).
    // 1.4.0 -> 1.4.1 (f18877f, ADR-0032 adoption): patch bump. No public surface change — the
    // fsproj <Version> moved and this constant did not, and NOTHING IN THIS REPO NOTICED: the
    // assertion lives DOWNSTREAM, in .github's contract-coherence gate, so SDD's main went green
    // and .github's went red, blocking every PR there (.github#386-class; FS.GG.SDD#386).
    //
    // 1.4.1 -> 2.0.0 (FS.GG.SDD#393): the SemVer MAJOR the repo had already decided on twice and
    // never made. `ProviderDescriptor` gained `IdentifierParameter` mid-record in 4e6f8b7 (feature
    // 080) with NO version bump; an F# record generates a positional primary constructor, so its
    // arity went 11 -> 12 and the 11-arg .ctor CEASED TO EXIST. f18877f then shipped that binary
    // break as a PATCH (1.4.1) and deleted the CompatibilitySuppressions.xml that recorded the debt
    // — the file which said, verbatim, that the honest resolution is "a Contracts MAJOR bump
    // (1.x -> 2.0.0) + republish". 2.0.0 is that bump. There is no additive way to add a field to an
    // F# record: every field changes the generated ctor arity, so this is a major or it is a lie.
    // The break is DECLARED (docs/release/contracts-2.0.0.md), not suppressed.
    // 2.0.0 -> 2.0.1 (full-platform release, 2026-07-16): shipped AS a PATCH — the
    // `governanceHandoffContractVersion` constant moved 1.0.0 -> 1.1.0 (reconciling three drifted
    // hand-copies). The justification recorded here was "the package API surface (Schemas.fsi)
    // unchanged" — TRUE of Schemas.fsi, and the wrong file to have looked at. See below.
    //
    // 2.0.1 -> 2.1.0 (FS.GG.SDD#432): the additive MINOR that 2.0.1 should have been. #426
    // (80d0c28, 07-14) grew `Registry.fsi` by +78 lines — `SkillRegistryEntry`,
    // `SkillRegistryDocument`, `MirrorDeclaration`, `validateSkillRegistry`, and the
    // `MalformedField` case on the public DU `RegistryRule` — and moved no version. Measured:
    // the published 2.0.0 was cut at 04dd742 (07-12), which does NOT contain #426, so the feed's
    // 2.0.0 -> 2.0.1 delta IS that surface growth. Per this repo's own rule ("add a new module,
    // type, or `val`" -> additive -> minor) it owed 2.1.0 and shipped as a patch: the number
    // understated the API.
    //
    // WHY IT WAS MISSED, because the shape recurs: the 2.0.1 classification was made against the
    // wrong baseline. Diffed tag-to-tag (v0.11.0 -> v0.12.0) the `.fsi` surface really is
    // unchanged — but Contracts 2.0.0 was PUBLISHED from 04dd742 two days BEFORE v0.11.0 was cut,
    // so #426's growth landed in the gap between the publish point and the next tag, where a
    // tag-to-tag diff cannot see it. ApiCompat passed too, correctly — additions are binary-
    // compatible, and a DU case doubly so. Both detectors were looking somewhere true.
    //
    // The detector that sees it is the committed `.fsi` baseline (FS.GG.SDD#475, PR #484):
    // `surface --check` classifies this delta `additive` and names 2.1.0 on sight. It is keyed on
    // the baseline, not on a tag, which is exactly why it cannot be fooled the same way.
    //
    // 2.1.0 -> 3.0.0 (FS.GG.SDD#508): a DECLARED break, and the same record row that produced
    // 2.0.0 — `ContractEntry.Consumers` is RETYPED `string list` -> `ConsumerDeclaration`, so the
    // generated positional ctor's signature changes and the old one ceases to exist. The rule is
    // this repo's own ("remove/rename/RETYPE a public member; change a signature" -> breaking ->
    // major, docs/release/contracts-version-bump-checklist.md), and there is no additive spelling:
    // a parallel field would be a new field on a public record, which that same table's first row
    // calls a break for the identical reason. The break is DECLARED
    // (docs/release/contracts-3.0.0.md), not suppressed.
    //
    // WHY A MAJOR WAS SPENT ON IT: the two-state `string list` could not tell an ABSENT
    // `consumers:` from an explicitly EMPTY one — the YAML edge mapped both onto `[]` — so a
    // producer whose package nothing restores had no honest row, and `FS.GG.NewSddWorkspace` sat
    // unregistered while the org's package inventory read "off by two" (ADR-0039 §5). The
    // three-state model is the `MirrorDeclaration` precedent applied to the same question; what it
    // does NOT inherit is that feature's change class, because #426 ADDED types (additive -> minor)
    // where this one MUTATES a shipped record. Same shape, different bump — and it is worth being
    // explicit about that, since "we did this before as a minor" is exactly the reasoning that
    // shipped 2.0.1 understated.
    //
    // Blast radius, MEASURED rather than assumed (both declared consumers, 2026-07-17): neither
    // FS.GG.Governance nor FS.GG.Templates references `Fsgg.Registry` at all — Governance's own
    // `ContractEntry` is an unrelated domain type in its `Route.fsi`. So, as with 2.0.0, for a
    // consumer already on 2.1.0 this is a version-number change and no source edit. That does not
    // make it a minor: the surface broke, and the number says so.
    //
    // 3.0.0 -> 4.0.0 (FS.GG.SDD#589, ADR-0052): a DECLARED break, and the SAME record row that
    // produced 2.0.0 and 3.0.0 — `ContractEntry` gains a `WireContract: WireContractDeclaration`
    // field (the optional wire-contract dimension: three provenances — vendored `.proto`, owned
    // `.proto`, code-first protobuf-net). Adding a field to a public F# record generates a new
    // positional ctor and DELETES the old one, so it is breaking for the identical reason the
    // version-bump checklist's first row states, and there is NO additive spelling: a parallel
    // record (`RegistryDocument` gaining a `WireContracts` list) is a new field on a public record
    // too. The new union types (`WireContract`, `WireContractDeclaration`) are themselves additive;
    // the record field is what forces the major. The break is DECLARED
    // (docs/release/contracts-4.0.0.md), not suppressed.
    //
    // WHY THE MAJOR IS SPENT: a networked component's compatibility surface is often its wire
    // bytes, which the source `.fsi` `Surface` cannot express, and `.github`'s registry could not
    // record them at all (blocking FS.GG.Net's SC2/BAR contracts under ADR-0052). This is the SDD
    // half of the two ordered PRs (ADR-0037); `.github` bumps `schemaVersion` + the validator pin
    // after this publishes. Blast radius (both declared `Fsgg.Registry` consumers, unchanged from
    // 3.0.0): neither Governance nor Templates references `Fsgg.Registry`, so for a consumer on
    // 3.0.0 this is a version-number change and no source edit. That does not make it a minor: the
    // record surface broke, and the number says so.
    // 4.0.0 -> 5.0.0 (FS.GG.SDD#610): a DECLARED break, and the LAST one this record row will ever
    // force. 2.0.0/3.0.0/4.0.0 were each `ContractEntry` — a public F# RECORD — changing, and each
    // was major for the one reason the version-bump checklist's first row states: a record compiles
    // its fields into a positional primary constructor, so any field add/retype changes the ctor's
    // arity and deletes the old one (`CP0002`). This bump changes the SHAPE, not the fields:
    // `ContractEntry` becomes a non-positional CLASS (parameterless ctor + settable typed
    // properties). The one-time cost is a break (the record ctor and get-only properties are gone,
    // construction moves to object-initializer, and `{ e with … }` copy-update / structural
    // comparison are lost). The payoff is that from here a NEW field is an additive property — a
    // MINOR, no fleet adopt round, no registry flip — while the typed unions the prior three majors
    // bought are fully preserved. `[<CLIMutable>]` was NOT the fix: it keeps the positional ctor and
    // would have re-broken on the next field. The break is DECLARED
    // (docs/release/contracts-5.0.0.md), not suppressed.
    //
    // Blast radius (both declared `Fsgg.Registry` consumers, unchanged from 3.0.0/4.0.0): neither
    // Governance nor Templates references `Fsgg.Registry`, so for a consumer already on 4.0.0 this
    // is a version-number change and no source edit. That does not make it a minor — the record
    // surface was replaced — but it does make it the cheapest possible time to spend this last major.
    [<Fact>]
    let ``contract version self-report matches 5_0_0`` () =
        Assert.Equal("5.0.0", ContractVersion.value)
        Assert.Equal(5, ContractVersion.major)
        Assert.Equal(0, ContractVersion.minor)
        Assert.Equal(0, ContractVersion.patch)

    // THE ASSERTION THAT WAS MISSING, AND THE ONLY ONE THAT WOULD HAVE CAUGHT IT.
    //
    // `ContractVersion.fsi` promises: "Single authoritative value — no second place can disagree."
    // There IS a second place — the fsproj `<Version>` — and on 2026-07-12 it disagreed. `f18877f`
    // ("adopt ADR-0032 — sync the shared build config") moved <Version> 1.4.0 -> 1.4.1 and left this
    // constant at 1.4.0.
    //
    // NOTHING IN THIS REPO NOTICED. The two-facts-must-agree assertion lived DOWNSTREAM, in
    // `.github`'s contract-coherence gate, which checks out this repo to run it. So SDD's `main` went
    // green, `.github`'s went red, and every open PR in `.github` was blocked by a break this repo
    // merged and could not see (FS.GG.SDD#386).
    //
    // A repo that can red another repo's `main` without its own gate going red is the
    // coherence-gate-in-the-wrong-place shape (FS-GG/.github epic #266). The gate belongs where the
    // break happens. MSBuild stamps `<Version>` into AssemblyInformationalVersion, so the compiled
    // package version is readable here with no file paths and no build plumbing — and the two facts
    // are now forced to agree by the PR that changes either one.
    [<Fact>]
    let ``the fsproj <Version> and ContractVersion.value cannot disagree`` () =
        let asm = System.Reflection.Assembly.Load("FS.GG.Contracts")

        let attr =
            asm.GetCustomAttributes(typeof<System.Reflection.AssemblyInformationalVersionAttribute>, false)
            |> Array.map (fun a -> a :?> System.Reflection.AssemblyInformationalVersionAttribute)
            |> Array.tryHead

        match attr with
        | None ->
            // Never a silent pass: if the attribute is missing the coupling is UNVERIFIABLE, and an
            // unverifiable subject must not report green (FS-GG/.github epic #266).
            failwith "no AssemblyInformationalVersion on FS.GG.Contracts — the coupling cannot be verified"
        | Some a ->
            // A deterministic build appends `+<sha>` (SourceLink). The version is the part before it.
            let fsprojVersion = a.InformationalVersion.Split('+').[0]
            Assert.Equal(fsprojVersion, ContractVersion.value)
