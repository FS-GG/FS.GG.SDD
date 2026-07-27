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
    //
    // 5.0.0 -> 5.0.1 (FS.GG.SDD#612, ADR-0061 Option (b) step 1): a PATCH — the version-bump
    // checklist's last row, "behaviour change with no surface change". `validateSkillRegistry`'s
    // skill-`scope` check stopped rejecting a non-blank token that is not in a compiled-in enum
    // (`skillScopes` — a private binding, now deleted); a blank scope is still a `MissingField`, and
    // every other structural/malformed check is unchanged. Nothing in `Registry.fsi` moved — no
    // member added/removed/retyped, no DU case added (`UnknownComponent` stays, still raised by the
    // dependency-document validator) — so both detectors pass correctly: ApiCompat sees no break
    // (a loosening removes no member), and `surface --check` sees no `.fsi` drift. The behaviour
    // LOOSENED (strictly more inputs pass), which is why the number moves at all. This is step 1 of
    // the two-PR ADR-0061 flow; `.github#1261` (step 2) pins this published version and flips the
    // registry, retiring the ADR-0037 §3 "known, not enforced" rail that cost an ADR + republish +
    // bump+pin per new scope value.
    //
    // 5.0.1 -> 6.0.0 (FS.GG.SDD#687, PR #699, merge ca60cf5): a DECLARED break, and the version-bump
    // checklist's FIRST row again — "add a field to a public record" -> breaking -> major, declaration
    // required. `Schemas.fsi` grew +42 lines. Four of the new types are themselves additive
    // (`PerformanceEvidenceSampleSet`, `PerformanceEvidenceArtifact`, `PerformanceEvidenceMeasurement`,
    // `GovernanceHandoffPerformanceEvidence`); what forces the major is ONE line — the public record
    // `GovernanceHandoffSchema` gains `PerformanceEvidence: GovernanceHandoffPerformanceEvidence list`,
    // inserted MID-RECORD between `Evidence` and `GovernedReferences`. That regenerates the positional
    // primary constructor and the previous one CEASES TO EXIST (`CP0002`), the identical mechanism as
    // 2.0.0/3.0.0/4.0.0. The break is DECLARED (docs/release/contracts-6.0.0.md, added in this merge),
    // not suppressed — this merge adds no CompatibilitySuppressions.xml.
    //
    // AND IT CORRECTS THE ENTRY ABOVE, WHICH IS WHY THIS ONE IS WORTH READING: 5.0.0 called itself
    // "the LAST one this record row will ever force". That was true of `ContractEntry`, the single
    // record it de-positionalised into a class — and it was never true of the repo. `Schemas.fsi`'s
    // governance-handoff DTOs are all still ordinary F# records with positional constructors (all 8
    // `GovernanceHandoff*` types, measured on this file's `Schemas.fsi`), so the row that 5.0.0 spent
    // a major to retire fired twice more six days later — here and at 7.0.0, both on 2026-07-26. The
    // 5.0.0 fix was scoped to one record in `Registry.fsi`, not to the pattern.
    //
    // WHY THE MAJOR IS SPENT: Governance was being handed a producer-authored pass/fail summary and
    // had to trust it. The new DTO graph carries each `performance-evidence-v1` artifact's RAW
    // duration and catch-up samples, its workload/environment bindings, and SDD's own recomputed
    // measurements, so the verdict is independently recomputable at the boundary rather than asserted
    // across it. NOTE WHAT THE MAJOR DID *NOT* BUY, because the intuition is backwards: a single
    // verdict-shaped `bool` would have cost the IDENTICAL major — the price is set by adding ANY field
    // to a positional record, not by how much the field carries. Having decided to pay it, the cheap
    // design and the honest one cost the same, so richness here is free and the summary would have
    // been a worse artifact at the same price.
    //
    // WHAT THIS MERGE ALSO SHIPPED, RECORDED HERE BECAUSE IT IS THIS FILE'S OWN SUBJECT: it moved
    // `ContractVersion.value` "5.0.1" -> "6.0.0" and LEFT `major = 5`, `minor = 0`, `patch = 1`. For
    // the whole life of 6.0.0 the "single authoritative value" disagreed with itself inside one file.
    // The test below was edited into agreement with the defect — its string assertion was updated, its
    // three integer assertions were not, and its name still read "matches 5_0_1" while asserting
    // "6.0.0" — so the suite stayed green. #700 / PR #701 repaired the triple in passing while
    // spending 7.0.0; nobody filed it. This is the 1.4.1 shape (a number and a surface disagreeing)
    // for the third time in this file, and the first time INSIDE `ContractVersion.fs`.
    //
    // NOTHING WOULD CATCH IT TODAY EITHER, WHICH IS THE PART THAT MATTERS: the one downstream gate
    // that reads this constant at all — `.github`'s `check-source-coherence.py` — matches a single
    // `\blet\s+value\s*=\s*"([^"]*)"` and uses ONLY that. (`check-emitted-contract-version.py` is not
    // a second opinion: it reads `governanceHandoffContractVersion` out of `Schemas.fs` and never
    // opens `ContractVersion.fs`.) So `major`/`minor`/`patch` have exactly one reader in this repo —
    // the three hand-written literals below. Hand-written literals cannot detect drift from `value`;
    // they can only be updated to match whatever was typed, which is precisely what happened. Tracked
    // as FS.GG.SDD#728 (a structural assertion), deliberately NOT fixed here: #724 is the record, not
    // the numbers.
    //
    // 6.0.0 -> 7.0.0 (FS.GG.SDD#700, PR #701, merge 7ea65ac, under an hour after 6.0.0): a DECLARED
    // break, the checklist's FIRST row for the second time that day. `Schemas.fsi` grew +20 lines: the
    // new type `PerformanceIntentDeclaration` (additive on its own), and the record
    // `GovernanceHandoffPerformanceEvidence` — the one 6.0.0 had just introduced — gains
    // `Intent: PerformanceIntentDeclaration option` MID-RECORD, between `ArtifactPath` and `Artifact`:
    // ctor arity 4 -> 5, old ctor gone, `CP0002`. DECLARED in
    // docs/release/contracts-7.0.0.md, added in this merge.
    //
    // THE TRAP, STATED BECAUSE THE WRONG READING IS THE PLAUSIBLE ONE: the field is an `option`, and
    // `option` BUYS NOTHING HERE. An optional field is still a constructor parameter, so the 6.0.0
    // four-argument .ctor is deleted exactly as a required field would delete it. What `option` buys
    // is wire-level tolerance — a legacy handoff may carry `intent: null` — and wire tolerance is not
    // surface compatibility. "I made it optional, so it is additive" is the reasoning this row of the
    // table exists to refuse.
    //
    // AND IT WAS SUPPRESSED — CORRECTLY, WHICH IS THE OPPOSITE OF THE 1.4.1 CASE ABOVE. This merge
    // added `src/FS.GG.Contracts/CompatibilitySuppressions.xml` with ONE `CP0002` baseline suppression
    // naming that exact four-arg ctor, commented with the issue, the reason, and its own expiry. At
    // 1.4.1 (f18877f) a suppression file recording real debt was DELETED so a binary break could ship
    // as a patch; here the major is spent in the open and the suppression is a scoped, time-boxed
    // bridge for the window in which `scripts/apicompat-check.sh` still compares against the PUBLISHED
    // 6.0.0 baseline, where a correct tool must report the break the release notes already declare. It
    // was retired by #702 (d0f4514) once 7.0.0 was the published baseline — which is why no
    // CompatibilitySuppressions.xml exists in this repo today. A suppression is a lie when it stands
    // in for the bump; it is bookkeeping when it stands beside it and is removed on schedule.
    //
    // WHY THE MAJOR IS SPENT: performance intent has to be authored BEFORE implementation and carried
    // unchanged through evidence to Governance. Untyped, the target FPS, representative workload
    // identities and definition digests, scale, timing and structural limits, capability and
    // live-compositor posture were producer prose, so a run could be graded against a budget invented
    // after the measurement — the one failure the 6.0.0 evidence graph cannot detect on its own,
    // because it makes the samples auditable without fixing what they were promised against. Per the
    // release note added in this merge, consumers on 6.x must recompile.
    //
    // 7.0.0 -> 7.1.0 (FS.GG.SDD#720, owed by FS.GG.SDD#717 / PR #719): an ADDITIVE MINOR — the
    // version-bump checklist's fourth row, "add a new module, type, or `val`". #719 grew
    // `Fsgg.SkillMirror` so a skill can be MULTI-FILE: it adds the types `SkillFile`,
    // `MultiFileSkill`, `MirrorRefusalReason`, `MirrorRefusal` and `MirrorPlan`, and the vals
    // `skillFilePath` and `mirrorFiles`. ZERO members were removed, renamed or retyped, and no case
    // was added to an existing public DU — the new DUs (`MirrorRefusalReason`) are themselves new
    // types, so they do not carry the source-breaking `FS0025` tax the DU row warns about. Both
    // detectors agree and neither is being overruled: `surface --check` classifies the delta
    // `additive` / suggested `7.1.0` against the committed `.fsi` baseline, and
    // `scripts/apicompat-check.sh` reports `FS.GG.Contracts OK (compatible with 7.0.0)` — which for
    // an additive delta is a CORRECT pass, not a second opinion, since additions are binary-
    // compatible and ApiCompat is structurally blind to this whole class.
    //
    // WHY IT IS A SEPARATE ISSUE FROM THE CHANGE THAT EARNED IT: a Contracts bump is a COORDINATED
    // three-part change (docs/release/contracts-version-bump-checklist.md) — bump the source here,
    // publish `7.1.0` to the org feed, then advance `fsgg-contracts.version` and (only after the
    // feed serves it) `package-version` in `FS-GG/.github`'s `registry/dependencies.yml`. #717 was
    // an additive library change and correctly declined to take that release decision unilaterally.
    // The debt it left is EXACTLY the #432 shape this file already records twice: surface growth
    // shipped under a number that never moved, which makes the `.nupkg` at `7.0.0` and the source at
    // `7.0.0` different artifacts. `surface --check`'s version half is advisory and never reds a PR
    // (ADR-0025 §2), so nothing catches this automatically — it has to be done deliberately, and
    // this commit is the deliberate half that can be done in-repo.
    //
    // WHAT THIS COMMIT DOES NOT DO, STATED SO THE NEXT READER DOES NOT ASSUME IT: it moves the
    // SOURCE only. Between this merge and the feed publish + registry flip the coherence invariant
    // `source == feed(newest) == registry.version == registry.package-version` is BROKEN, and
    // `.github`'s `source-coherence` gate reds on that repo — correctly, and saying exactly what is
    // owed. Since FS-GG/.github#741 that red lands on `.github` ALONE (the repo that owns the
    // registry and is the only one that can flip it); it no longer holds this repo's merges or any
    // other repo's. Source-first is not a choice of ordering here, it is the ONLY correct order:
    // the publish workflow's manual-dispatch path packs `-p:Version=<input>` with NO
    // source-vs-published drift guard, so publishing before the source moves would ship a package
    // whose `ContractVersion.value` disagrees with its own package version — the 1.4.1 defect this
    // file's second test exists to prevent.
    // 7.1.0 -> 7.2.0 (FS.GG.SDD#727): an ADDITIVE MINOR — the version-bump checklist's fourth row,
    // "add a new module, type, or `val`". The ADR-0017 producer manifest is amended to
    // content-address a skill's COMPLETE FILE SET rather than its `SKILL.md` alone, which grows
    // `Schemas.fsi` by the types `SkillManifestFile`, `SkillManifestFileSet` and `SkillManifestV2`,
    // and `SkillMirror.fsi` by the type `ExpectedSkillFiles` and the val `verifyFileSet`. ZERO
    // members were removed, renamed or retyped; no case was added to an existing public DU; and —
    // the row that would have forced a MAJOR — NO public record gained a field. That last point was
    // a design constraint, not an outcome: the obvious spelling of this change is a `Files` field on
    // `Schemas.SkillManifestEntry` or on `SkillMirror.ExpectedSkill`, and either would have
    // regenerated a positional primary constructor and deleted the old one (`CP0002`), costing a
    // coordinated major that was NOT authorised. The amendment is expressed as NEW types and a NEW
    // entry point beside the shipped ones instead, which is why it fits in a minor at all.
    //
    // AND IT IS BEING MADE HERE RATHER THAN DEFERRED, WHICH IS THE LESSON THIS FILE RECORDS THREE
    // TIMES ALREADY. #717's growth could ride an unpublished 7.1.0 because 7.1.0 had not shipped;
    // 7.1.0 IS now live on the org feed (verified against
    // `/orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions` while preparing this change), so
    // growing the surface without moving the number would make the `.nupkg` at 7.1.0 and the source
    // at 7.1.0 different artifacts — the exact #426/#432 shape recorded above, for the third time.
    //
    // WHAT THIS COMMIT DOES NOT DO: it moves the SOURCE only. Between this merge and the feed
    // publish + registry flip the coherence invariant is broken and `.github`'s `source-coherence`
    // reds on that repo alone (FS-GG/.github#741). Note that registry is ALREADY behind at 7.0.0 —
    // the 7.1.0 flip was never made — so this bump widens an existing gap rather than opening one.
    [<Fact>]
    let ``contract version self-report matches 7_2_0`` () =
        Assert.Equal("7.2.0", ContractVersion.value)
        Assert.Equal(7, ContractVersion.major)
        Assert.Equal(2, ContractVersion.minor)
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

    // THE OTHER MISSING ASSERTION — same promise, the second place it was broken (FS.GG.SDD#728).
    //
    // The test above pins `value` to the fsproj `<Version>`, one file away. It says nothing about the
    // three integers sitting DIRECTLY BESIDE `value` in `ContractVersion.fs`, and those disagreed with
    // it for the entire life of 6.0.0: `ca60cf5` moved `value` "5.0.1" -> "6.0.0" and left
    // `major = 5`, `minor = 0`, `patch = 1`. The `.fsi` promises "no second place can disagree" and
    // there were four places in ONE FILE.
    //
    // WHY THE SUITE STAYED GREEN, which is the whole reason this test is shaped the way it is: the
    // per-version `[<Fact>]` at the top of this module asserts all four facts against HAND-WRITTEN
    // LITERALS. Literals cannot detect drift BETWEEN the facts — they can only be updated to match
    // whatever was typed, and at `ca60cf5` exactly that happened (the string assertion was updated,
    // the three integer assertions were not, and the test's name still read "matches 5_0_1" while
    // asserting "6.0.0"). That test still earns its place — it pins the EXPECTED version, a different
    // job — but it must not be the only guard, because it is the guard that was edited into agreement
    // with the defect.
    //
    // So this assertion carries NO literal at all. There is nothing in it to update: the expectation
    // is DERIVED from the triple at run time, and the only way to satisfy it is to make the constant
    // self-consistent. Move any ONE of the four and it reds.
    //
    // DIRECTION IS LOAD-BEARING, AND THE OBVIOUS SPELLING IS THE WEAKER ONE. The natural instinct is
    // to parse `value` into three ints and compare them to the triple. That has a blind spot this
    // spelling does not: `int "07"` is `7`, so a `value` of "07.2.0" would PASS a parse-and-compare
    // while disagreeing with its own rendering — and a `value` with too few segments would die on an
    // index rather than report a comparison. Rendering the triple and comparing whole strings has
    // neither hole, so the derivation runs triple -> string, never string -> triple.
    //
    // AND IT IS DELIBERATELY STRICT. `Assert.Equal` on the FULL string means a `value` that the triple
    // cannot describe — a pre-release or metadata suffix such as "7.2.0-rc1" — reds here. That is the
    // intended answer, not a false positive: the triple genuinely does not describe such a `value`,
    // and loosening the compare to segment-slicing or a prefix match would trade one wrong red for a
    // permanent blind spot of exactly the kind that let 6.0.0 ship. If this repo ever ships a
    // pre-release contract version, that is a decision to take in the open, here.
    //
    // WHAT IT COMPLETES: `value` is now the HUB. The triple is pinned to it by this test, the fsproj
    // `<Version>` by the test above, so every in-repo restatement of the package contract version is
    // transitively forced to agree with every other — which is what the `.fsi` has claimed all along.
    //
    // WHAT IT DELIBERATELY DOES NOT ASSERT, because keeping it to ONE proposition is the point: this
    // is a CONSISTENCY check, not a VALIDITY check. A self-consistent but nonsensical constant — say
    // `value = "-1.2.0"` with `major = -1` — satisfies it, and that is correct division of labour, not
    // an oversight: WHICH version this is belongs to the literal `[<Fact>]` above (which pins 7.2.0 and
    // would red), and WHETHER the string is a well-formed version belongs to `Fsgg.Version.tryParse`,
    // whose own grammar already rejects "-1.2.3". Folding either of those in here would put a literal
    // or a second proposition back inside the one guard that must have neither — which is precisely
    // how the assertion above became editable into agreement with the defect it should have caught.
    //
    // MEASURED WHILE FIXING THIS, because the issue left it open: `major`/`minor`/`patch` have NO
    // reader anywhere in the org outside this file. A grep for `ContractVersion.(major|minor|patch)`
    // across all eight FS-GG repositories plus `FS-GG/.github` returns only the assertions in this
    // module; `.github`'s `check-source-coherence.py` matches `\blet\s+value\s*=\s*"([^"]*)"` and uses
    // only that capture. So 6.0.0's inconsistency was LATENT — no consumer took a 5.x path — and it
    // stays latent only for as long as nobody branches on the triple, which is a property of today's
    // consumers rather than of the surface. The triple is public `val`s on a gated package; the guard
    // is what makes the promise true regardless.
    [<Fact>]
    let ``the major/minor/patch triple and ContractVersion.value cannot disagree`` () =
        let renderedFromTriple =
            sprintf "%d.%d.%d" ContractVersion.major ContractVersion.minor ContractVersion.patch

        Assert.Equal(renderedFromTriple, ContractVersion.value)
