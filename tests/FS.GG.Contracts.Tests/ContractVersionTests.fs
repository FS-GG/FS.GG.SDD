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
    [<Fact>]
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
    let ``contract version self-report matches 2_0_0`` () =
        Assert.Equal("2.0.0", ContractVersion.value)
        Assert.Equal(2, ContractVersion.major)
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
