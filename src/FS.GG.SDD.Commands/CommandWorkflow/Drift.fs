namespace FS.GG.SDD.Commands.Internal

open Fsgg
open Fsgg.Provider
open Fsgg.Schemas
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes

/// The pure drift computation shared by both `HandlersDoctor` and `HandlersUpgrade`
/// (feature 053, data-model E7, contracts/drift-model.md). It performs **no I/O**: it
/// consumes snapshots already read via effects and returns the drift picture plus the
/// previewed `ReconciliationStep` list, so the drift a `doctor` previews and the drift an
/// `upgrade` reconciles are always computed by the same code (no second source of truth).
module internal Drift =

    // The expected seeded-skeleton set (R3 / FR-004): for every `SeededSkills.skillNames`,
    // the `.claude`, `.codex`, AND 056 neutral `.agents` SKILL.md, plus
    // `.fsgg/early-stage-guidance.md`. This is exactly the set `init` seeds, so `upgrade`'s
    // re-seed re-materializes the missing subset (e.g. the third root a pre-056 CLI never
    // wrote) via `initEffects` no-clobber writes (R8/FR-010). Sorted, deterministic. A
    // divergent/missing root among the three violates `claude ≡ codex ≡ agents` (E7).
    let expectedArtifactPaths =
        (SeededSkills.skillNames
         |> List.collect (fun name ->
             [ $".claude/skills/{name}/SKILL.md"
               $".codex/skills/{name}/SKILL.md"
               $".agents/skills/{name}/SKILL.md" ]))
        @ [ ".fsgg/early-stage-guidance.md"
            // 073/ADR-0018: the seeded regenerable-output `.gitignore` is part of the coherent
            // skeleton set — `doctor` reports it missing, `upgrade` no-clobber re-seeds it.
            ".gitignore" ]
        |> List.sort

    let expectedArtifactCount = List.length expectedArtifactPaths

    type DriftReport =
        { HasProvenance: bool
          ProviderName: string option
          InstalledCliVersion: string
          RequiredMinimumCliVersion: string option
          // FS-GG/FS.GG.SDD#313: which of the two floors produced `RequiredMinimumCliVersion` —
          // `providerDescriptor` / `scaffoldProvenance` / `workspaceFloor`. `None` iff there is no
          // effective minimum. Without it a divergence between the provider floor and the
          // workspace's `sdd.minToolVersion` is invisible in the report.
          RequiredMinimumCliVersionSource: string option
          CliAxis: string
          CliBehindBy: string option
          ExpectedArtifactCount: int
          MissingArtifactPaths: string list
          // 058/ADR-0014 §Decision 3: the content-addressed skill-drift surface — the concrete
          // root/skill paths where a skill in the union (process OR product) is missing from a
          // root, byte-divergent across roots, or hash-mismatched against its canonical digest.
          // Sorted, deduped. Non-empty ⇒ not coherent (advisory; `doctor` still exits 0).
          // FS-GG/FS.GG.SDD#726: these name the offending FILE, not merely the skill — a divergent
          // `<root>/skills/<id>/references/deep-detail.md` is reported at that path, because a skill
          // is a directory and this surface used to see only its `SKILL.md`.
          SkillDriftPaths: string list
          // FS-GG/FS.GG.SDD#736: `SkillDriftPaths` split by the CONDITION that produced each entry.
          // The union is unchanged; these name WHICH failure each path is, because they have three
          // different repairs and only one of them is about bytes. Disjoint by construction (see
          // `computeSkillDrift`), and their union is exactly `SkillDriftPaths`.
          //
          // A file at least one OTHER root carries and this one does not — an inconsistently applied
          // edit, or a provider file dropped from one root. Since FS-GG/FS.GG.SDD#747 this no longer
          // includes OS/VCS junk, which is excluded from the compared set (`IgnoredSkillJunkPaths`).
          SkillNotMirroredPaths: string list
          // No declared root carries a copy of the skill at all; there is nothing to mirror from.
          SkillLostPaths: string list
          // Every named root HAS the file and the bodies disagree (or disagree with the recorded
          // digest). This is the condition the single pre-#736 advisory described.
          SkillDivergentPaths: string list
          // ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced skill copies (driver + product classes)
          // this scaffold is EXPECTED to carry — per the recorded parameters + present-skill set —
          // but is missing on disk. These are backfilled no-clobber by the `artifactReSeed` step,
          // so they are folded into that step's `TargetPaths`; the field names them on their own for
          // observability. Sorted, deduped. Non-empty ⇒ actionable (re-seed WouldApply, not coherent).
          OwnerSkillBackfillPaths: string list
          // FS-GG/FS.GG.SDD#747: the skill-copy files this run OBSERVED on disk and then EXCLUDED
          // from the comparison as OS/VCS junk (`junkFileNames` / `junkFileSuffixes`). Sorted,
          // deduped, and EMPTY unless the exclusion actually fired — it names files, never the rule.
          //
          // It exists so the exclusion can be STATED rather than assumed. Without it "this tree is
          // converged" would be quietly conditional on an invisible subtraction, which is the same
          // class of silent-narrowing defect #743 caught in its own fix; `HandlersUpgrade` appends
          // the exclusion to its advisory whenever this is non-empty.
          IgnoredSkillJunkPaths: string list
          Steps: ReconciliationStep list
          IsCoherent: bool }

    // The content-addressed union covers THREE skill classes, each with the strongest authority it
    // actually has, and no stronger (FS-GG/FS.GG.SDD#733):
    //
    //   - SDD-seeded *process* skills — no digest at all; presence + cross-root byte-identity;
    //   - provider *product* skills recorded in provenance — one recorded `sha256`, covering
    //     `SKILL.md` alone (the ADR-0017 v1 gap, FS-GG/FS.GG.SDD#727);
    //   - owner-sourced *driver + GameSkill* skills recorded in provenance — a `sha256` for EVERY
    //     file, so every file is digest-arbitrated (`SkillMirror.verifyFileSet`).
    //
    // `skillBodies` is the caller-read body of each skill copy FILE keyed by its on-disk path; a
    // file absent from `skillBodies` is treated as missing, and a (root, id) with no file at all is
    // treated as a root that carries no copy of the skill.

    /// Split an on-disk path into `(root, id, relativePath)` when — and only when — it names a file
    /// inside a declared agent skill directory, `<root>/skills/<id>/<relative>`, for one of the
    /// declared `agentSkillRoots`.
    ///
    /// This is the multi-file counterpart of `SkillMirror.skillIdOfPath`, but NOT its exact
    /// generalization, and the difference is deliberate. `skillIdOfPath` matches the `SKILL.md`
    /// shape from the END and is not root-anchored, so it answers `Some "x"` for
    /// `app/docs/skills/x/SKILL.md`; its callers confine it themselves (`productSkillEntries`
    /// prefixes on `.agents/skills/`). This one is root-anchored instead, so a product file that
    /// merely LOOKS skill-shaped is `None` without the caller having to remember to guard it.
    ///
    /// It also applies the same lexical confinement `SkillMirror` applies on the WRITE side
    /// (`isConfinedRelativePath` / `isSafeSkillId`): no `.`, `..`, or empty segment in the id or the
    /// relative path. The doctor lane feeds this only paths returned by a directory enumeration,
    /// which cannot produce a traversal — but this function is the shared answer to "what counts as
    /// a skill copy", and its output is fed straight back into `SkillMirror.skillFilePath` to
    /// CONSTRUCT the paths this surface reports. A parser one careless reuse away from a containment
    /// hole is not the one to leave weaker than the neighbours it sits between (#185 / #337).
    ///
    /// Deliberately ONE parser, shared by the drift fold below and by the caller that collects the
    /// bodies (`HandlersDoctor.skillBodies`): a second spelling of "what counts as a skill copy" is
    /// how the collector and the verifier come to disagree about which files exist, which is the
    /// defect class ADR-0014 §Decision 2 exists to end.
    let skillCopyOfPath (path: string) : (string * string * string) option =
        let normalized = path.Replace('\\', '/')

        let isConfinedSegment segment =
            segment <> "" && segment <> "." && segment <> ".."

        agentSkillRoots
        |> List.tryPick (fun root ->
            let prefix = root + "/skills/"

            if normalized.StartsWith(prefix, System.StringComparison.Ordinal) then
                let rest = normalized.Substring(prefix.Length)

                match rest.IndexOf '/' with
                | -1 -> None
                | cut ->
                    let id = rest.Substring(0, cut)
                    let relative = rest.Substring(cut + 1)

                    if isConfinedSegment id && relative.Split('/') |> Array.forall isConfinedSegment then
                        Some(root, id, relative)
                    else
                        None
            else
                None)

    /// The provider *product* skills recorded in provenance, as `(id, recordedSha256)` — confined
    /// to the provider-owned source root's skill copies (`.agents/skills/<id>/SKILL.md`), so a
    /// product file that merely LOOKS skill-shaped (e.g. `app/docs/skills/x/SKILL.md`) is never
    /// mistaken for an agent skill. Excludes the SDD-seeded `fs-gg-sdd-*` process namespace. The
    /// `.agents` canonical is the authoritative id source — every mirror copy derives from it.
    let productSkillEntries (provenance: ScaffoldProvenanceRecord option) : (string * string) list =
        match provenance with
        | None -> []
        | Some record ->
            record.ProducedPaths
            |> List.choose (fun p ->
                if p.Path.StartsWith(SkillMirror.providerSourceRoot + "/skills/", System.StringComparison.Ordinal) then
                    SkillMirror.skillIdOfPath p.Path
                    |> Option.map (fun id -> id, Option.defaultValue "" p.Sha256)
                else
                    None)
            |> List.filter (fun (id, _) -> not (id.StartsWith("fs-gg-sdd-", System.StringComparison.Ordinal)))
            |> List.distinct

    /// ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced (driver + product classes) skill copies a
    /// scaffold with this provenance is EXPECTED to carry, as `(path, verified-body)` pairs. This is
    /// the SAME embedded, content-addressed materialize-and-verify plan `scaffold` runs
    /// (`DriverSkills.plan` / `GameSkills.plan`), fed from the recorded provenance instead of a live
    /// scaffold: the driver `has …` grammar reads the present-skill set (the SDD-seeded process
    /// skills ∪ the product ids recorded in provenance), and the product `materializes-when`
    /// predicate reads the recorded `EffectiveParameters`. Each body has already been hashed against
    /// its manifest `sha256` inside `plan` (a verify failure writes nothing), so a backfill can never
    /// materialize an unverified body (ADR-0014 preserved; only the byte SOURCE changed — ADR-0063).
    /// Pure: reads only the CLI's compiled-in package bytes + `record`, so it stays offline
    /// (FR-002). Empty when no owner-skill package is embedded (a build without the pin), so the
    /// backfill degrades to the pre-#624 shape rather than failing.
    let ownerSourcedBackfill (record: ScaffoldProvenanceRecord) : (string * string) list =
        let presentIds =
            Set.ofList (SeededSkills.skillNames @ (productSkillEntries (Some record) |> List.map fst))

        let driver = DriverSkills.plan presentIds
        let product = GameSkills.plan (record.EffectiveParameters |> Map.ofList)

        (driver.Writes @ product.Writes)
        |> List.choose (fun effect ->
            match effect with
            | WriteFile(path, body, _) -> Some(path, body)
            | _ -> None)

    let private expectedSkills (provenance: ScaffoldProvenanceRecord option) : SkillMirror.ExpectedSkill list =
        // Process (SDD-seeded) skills verify by presence + cross-root byte-identity ONLY — an
        // empty reference digest skips hash-match. The seeded roots are no-clobber
        // `AgentGuidanceTarget`, so a consumer's author edit applied consistently to every root is
        // PRESERVED as coherent (ADR-0011 invariant); only an INCONSISTENT edit (roots disagree)
        // or a missing copy is drift. Hash-matching against the running binary's embedded body
        // would instead flag every prior scaffold after any skill-text change across CLI versions.
        let processSkills =
            SeededSkills.seededSkills ()
            |> List.map (fun skill ->
                { SkillMirror.ExpectedSkill.Id = skill.Name
                  SkillMirror.ExpectedSkill.Scope = SkillScope.Process
                  SkillMirror.ExpectedSkill.Sha256 = "" })

        // Product (provider) skills carry the STABLE seed-time digest recorded in provenance, so
        // hash-match detects tampering even when all roots were edited identically — without the
        // cross-version volatility of an embedded reference.
        let productSkills =
            productSkillEntries provenance
            |> List.map (fun (id, digest) ->
                { SkillMirror.ExpectedSkill.Id = id
                  SkillMirror.ExpectedSkill.Scope = SkillScope.Product
                  SkillMirror.ExpectedSkill.Sha256 = digest })

        processSkills @ productSkills

    /// ADR-0063 / FS-GG/FS.GG.SDD#733: the OWNER-SOURCED skill class (driver + GameSkill), as the
    /// COMPLETE declared file set `SkillMirror.verifyFileSet` arbitrates against.
    ///
    /// This is the third skill class, and the only one whose recorded authority covers every file.
    /// `DriverSkills.plan` / `GameSkills.plan` materialize `<root>/skills/<id>/<file>` for every
    /// root, per file, and record `(path, file.Sha256)` into `DriverPaths` / `GameSkillPaths` — so
    /// unlike the process class (no digest at all) and the product class (one digest covering
    /// `SKILL.md`, the #727 gap), this class can arbitrate EVERY file against a stable seed-time
    /// digest. Before #733 it asserted none of it: the class was in neither `expectedSkills` arm and
    /// reached `doctor` only through the presence-only `ownerSourcedBackfill` axis, so a driver
    /// auxiliary edited away from its recorded digest — in one root or in all three — read coherent.
    ///
    /// The digest is the RECORDED one, never the running binary's embedded body, for the same reason
    /// the product class uses the recorded value: hash-matching against an embedded reference would
    /// flag every prior scaffold after any driver-skill text change across CLI versions. It is safe
    /// to trust because nothing ever launders it — `HandlersUpgrade.preservedFilesVerified` refuses
    /// to record a fresh digest for a preserved file that does not already match.
    ///
    /// Empty when provenance records no owner-sourced path — a pre-#624 tree, or a dev-repo `init`
    /// (`devRepoRecord` sets both fields empty). That degradation is the honest one: with no recorded
    /// declaration there is no authority to judge a copy against, so the class reports exactly what
    /// it did before — nothing — rather than inventing an expectation.
    ///
    /// It is a function of the RECORD ALONE, deliberately: unlike `ownerSourcedBackfill` it does not
    /// consult the CLI's embedded owner-skill package, so a build without that pin still verifies
    /// what a scaffold declared. Such a build can therefore report owner-sourced drift it has no
    /// backfill for — correct, and the honest report: the workspace really does disagree with its own
    /// provenance, and a CLI that cannot supply the canonical bytes must say so rather than go quiet.
    let ownerSourcedSkillFiles (provenance: ScaffoldProvenanceRecord option) : SkillMirror.ExpectedSkillFiles list =
        match provenance with
        | None -> []
        | Some record ->
            // The other two classes own their ids; an id in both would be verified twice, against
            // two different authorities, and report the union of both verdicts. Driver rows in the
            // reserved `fs-gg-sdd-*` namespace are already refused at materialize time
            // (`DriverSkills.reservedNamespacePrefix`), so this is belt-and-braces for the product
            // arm — but the drift fold is not the place to discover an overlap.
            let alreadyExpected =
                expectedSkills provenance |> List.map (fun skill -> skill.Id) |> Set.ofList

            record.DriverPaths @ record.GameSkillPaths
            |> List.choose (fun produced ->
                // The SAME root-anchored parser the fold and the body collector share. A recorded
                // path that is not a confined skill-copy path names no skill copy, whatever it says.
                skillCopyOfPath produced.Path
                |> Option.map (fun (_, id, relative) -> id, (relative, Option.defaultValue "" produced.Sha256)))
            |> List.filter (fun (id, _) -> not (Set.contains id alreadyExpected))
            |> List.groupBy fst
            |> List.map (fun (id, rows) ->
                { SkillMirror.ExpectedSkillFiles.Id = id
                  // Owner-sourced skills are delivered TO a product, so they are `Product` on the
                  // one axis this type carries. `Scope` is reporting metadata here — the fold below
                  // classifies by condition, not by scope.
                  SkillMirror.ExpectedSkillFiles.Scope = SkillScope.Product
                  SkillMirror.ExpectedSkillFiles.Files =
                    rows
                    // One row per (root × file): each relative path arrives once per root, and the
                    // three carry the SAME recorded digest because `DriverSkills.plan` fans one
                    // manifest `file.Sha256` into every root. Roots that DISAGREE mean the record was
                    // partially written or hand-edited, and there is then no authority to arbitrate
                    // with — electing one by sort order would invent one. Declare the path with an
                    // EMPTY digest instead, which `verifyFileSet` reads as "no reference digest":
                    // presence and cross-root identity still hold, and nothing is asserted that the
                    // record cannot support.
                    |> List.map snd
                    |> List.distinct
                    |> List.groupBy fst
                    |> List.sortBy fst
                    |> List.map (fun (relative, declared) ->
                        { SkillManifestFile.RelativePath = relative
                          SkillManifestFile.Sha256 =
                            match declared |> List.map snd |> List.distinct with
                            | [ single ] -> single
                            | _ -> "" }) })
            |> List.sortBy (fun skill -> skill.Id)

    /// FS-GG/FS.GG.SDD#733: every skill id the content-verified surface expects — the process ∪
    /// product union `expectedSkills` builds, plus the owner-sourced ids recorded in provenance.
    /// `HandlersDoctor` reads this to decide which enumerated skill-copy FILES to snapshot: a body
    /// the collector never reads cannot be verified however the fold is written.
    let contentVerifiedSkillIds (provenance: ScaffoldProvenanceRecord option) : string list =
        (expectedSkills provenance |> List.map (fun skill -> skill.Id))
        @ (ownerSourcedSkillFiles provenance |> List.map (fun skill -> skill.Id))
        |> List.distinct
        |> List.sort

    // -------------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#747 — the OS/VCS junk exclusion.
    //
    // THE DECISION (#747 AC1, taken 2026-07-27). Of {no repair, mirror, delete, ignore rule}, the
    // ignore rule alone was adopted. `upgrade` gains NO repair, NO delete effect and NO per-path
    // prompt; `doctor` stays read-only. The disqualifying fact for a repair is in #747 itself — the
    // tool cannot tell a legitimate authored addition from an OS turd FROM THE FILE ALONE, so a
    // repair means a per-path confirm, and a delete means introducing a destructive effect into a
    // lane that has never had one. #736 already left the operator "no longer stuck, only
    // unassisted", so none of that machinery buys a correctness fix.
    //
    // WHAT THE RULE BUYS. #736 left `ResidualDrift = true` PERMANENTLY on any tree carrying a
    // `.DS_Store`, so "converged" never meant what it says on a developer machine that has ever
    // opened a Finder window. With the junk subtracted, two consecutive `upgrade` runs over a
    // junk-only tree reach zero residual drift — #747 AC4 satisfied for real rather than waived.
    //
    // WHY THIS IS A CLOSED LITERAL SET AND NOT A PATTERN LANGUAGE (#747 AC3, condition 1). #726 AC1
    // says the surface observes EVERY file under `<root>/skills/<id>/`, and a provider is free to
    // ship any filename. A glob/regex dialect — worse, a configurable one — would let that
    // guarantee erode silently and invisibly, one added pattern at a time. So this is two flat
    // lists of exact strings, compared with `=` and `EndsWith`: no wildcard is ever interpreted,
    // and the complete set of files this tool will ever refuse to look at is READABLE HERE.
    //
    // ORDINAL, CASE-SENSITIVE, deliberately. `thumbs.db` is not `Thumbs.db` and is not ignored:
    // widening the match widens the blind spot, and every name below is written by a program that
    // writes it in exactly one casing.

    /// FS-GG/FS.GG.SDD#747: files ignored by EXACT name. Closed set — adding to it needs a code
    /// change and the #747 AC3 complement test below moves with it.
    let junkFileNames =
        [ ".DS_Store" // macOS Finder
          "Thumbs.db" // Windows Explorer thumbnail cache
          "ehthumbs.db" // ditto, Vista+
          "desktop.ini" // Windows folder settings
          ".directory" ] // KDE Dolphin folder settings

    /// FS-GG/FS.GG.SDD#747: files ignored by EXACT suffix — merge leftovers and editor backups,
    /// which are named after the file they shadow and so cannot be listed by exact name.
    ///
    /// A name that IS the suffix (a file called `.orig`, or called `~`) is NOT ignored: a backup is
    /// a backup OF something, and a bare `~` is a file someone chose to name that. The length test
    /// below is what enforces that, and it is the difference between a closed rule and a prefix of
    /// one.
    let junkFileSuffixes =
        [ ".orig" // `git merge` / `patch` leftover
          ".rej" // `patch` reject
          ".bak"
          ".swp" // vim swap
          ".swo"
          "~" ] // emacs/vi backup

    /// Does this skill-relative path NAME junk? Judged on the last segment only — an auxiliary may
    /// be nested (`references/deep-detail.md`), and it is the FILE name that identifies the junk.
    let isJunkFileName (relativePath: string) =
        let name = relativePath.Split('/') |> Array.last

        List.contains name junkFileNames
        || junkFileSuffixes
           |> List.exists (fun suffix ->
               name.Length > suffix.Length
               && name.EndsWith(suffix, System.StringComparison.Ordinal))

    /// FS-GG/FS.GG.SDD#736: which condition put a path on the drift surface. Kept as a tag carried
    /// alongside each path rather than a second traversal, so the classification cannot drift from
    /// the reporting rule that produced it.
    type private SkillDriftCondition =
        | NotMirroredAt
        | LostEverywhere
        | DivergentAt

    /// FS-GG/FS.GG.SDD#736: the skill-drift surface, split by the CONDITION that produced each path.
    ///
    /// One flat list could not tell an operator which repair to make, and the single advisory that
    /// described it named the wrong one: a file present in only one root was reported at the two
    /// roots that LACK it under a hint reading "some copies diverge from their canonical body —
    /// re-scaffold or restore the canonical skill sources", which for an extra `.DS_Store` reads as
    /// "create `.DS_Store` here". `All` is the union this surface has always reported.
    ///
    /// Three classes, not two, because a copy that is absent EVERYWHERE has a third repair and
    /// neither of the other two sentences is true of it: there is no sibling root to mirror from,
    /// and nothing disagrees about bytes. Collapsing it into `NotMirrored` would have the advisory
    /// assert "another root carries this file" about a file no root carries — the same class of
    /// false report #736 exists to end.
    type SkillDriftClasses =
        {
            /// This root does not carry a file that AT LEAST ONE other root does — a per-file
            /// absence, or a root carrying no copy of the skill while another root has one (in which
            /// case it is reported once at its `SKILL.md`). Repair: mirror it, or delete the stray.
            NotMirrored: string list
            /// No declared root carries a copy of the skill at all, reported once per root at its
            /// `SKILL.md`. Repair: restore the canonical source (for a SEEDED skill this is the
            /// `artifactReSeed` step, and `upgrade` subtracts it once applied).
            Lost: string list
            /// This root DOES carry the file and its bytes disagree — with the other roots, or with
            /// the digest recorded for it. Repair: reconcile the bodies.
            Divergent: string list
            /// `NotMirrored ∪ Lost ∪ Divergent`, sorted and deduped — byte-identical to the pre-#736
            /// list, which is the whole point: this is a SPLIT of the existing surface, not a change
            /// to what it reports.
            All: string list
            /// FS-GG/FS.GG.SDD#747: the observed skill-copy files subtracted from the comparison as
            /// OS/VCS junk before any of the three classes were computed. Disjoint from `All` by
            /// construction — an ignored file reaches no class.
            IgnoredJunk: string list
        }

    let private computeSkillDrift
        (provenance: ScaffoldProvenanceRecord option)
        (skillBodies: Map<string, string>)
        : SkillDriftClasses =
        let expected = expectedSkills provenance
        let ownerExpected = ownerSourcedSkillFiles provenance

        // FS-GG/FS.GG.SDD#747, and this is the clause that makes the ignore rule SAFE (AC3,
        // condition 4): a file the provenance DECLARES is never ignored, whatever it is called.
        //
        // "It cannot hide a file a producer actually ships" is the binding condition on this whole
        // change, and a name list alone cannot honour it — a producer that really does ship a
        // `desktop.ini` inside a skill would have it silently subtracted, and the workspace would
        // read converged while missing a file it is supposed to carry. The declaration is the only
        // evidence available that a producer meant a file, so it OVERRIDES the list: such a file
        // stays in the compared set and any drift about it is reported exactly as for any other
        // declared file. A conflict surfaced, not a silent skip.
        //
        // Keyed on `(id, relativePath)` rather than the whole path, because a declaration is about
        // the file the skill carries, not about which root happens to carry it.
        //
        // THE LIMIT OF THIS CLAUSE, STATED RATHER THAN GLOSSED. `ownerExpected` is the only class
        // that declares its files — the owner-sourced (driver + GameSkill) rows, which carry a
        // digest per file. The SDD-seeded process skills declare nothing at all, and a provider
        // PRODUCT skill declares its `SKILL.md` and nothing else, because the ADR-0017 manifest
        // content-addresses `SKILL.md` alone (the #727 gap). So a product provider that shipped a
        // junk-NAMED auxiliary would still be subtracted here, and no declaration exists to say
        // otherwise. That is a consequence of #727, not of this rule: with no recorded declaration
        // there is no evidence a producer meant the file, and inventing one would be worse. When
        // #727 gives the product class a per-file declaration this clause covers it unchanged,
        // because it reads `ownerExpected`'s shape and not its provenance field.
        let declaredSkillFiles =
            [ for skill in ownerExpected do
                  for file in skill.Files -> skill.Id, file.RelativePath ]
            |> Set.ofList

        // `SKILL.md` needs no entry here: it is on neither junk list, and could not be — it is what
        // MAKES a directory a skill (`SkillMirror.skillPath`), so a rule that could hide it would
        // hide every copy of every skill.
        let isIgnoredJunk path =
            match skillCopyOfPath path with
            | Some(_, id, relative) -> isJunkFileName relative && not (Set.contains (id, relative) declaredSkillFiles)
            | None -> false

        // Only files actually OBSERVED are reported as ignored: `skillBodies` is what the read
        // gate delivered, so this names what was really subtracted from this run rather than
        // restating the rule. Empty ⇒ the exclusion never fired ⇒ nothing to state.
        let ignoredJunk =
            skillBodies
            |> Map.toList
            |> List.map fst
            |> List.filter isIgnoredJunk
            |> List.sort

        // The subtraction itself, applied ONCE, before the union across roots is built — which is
        // the point. Filtering the CLASSES afterwards would leave a junk file in `All`, and
        // filtering only the root that HAS the file would leave the two roots that lack it in
        // `NotMirrored`, which is precisely the report #747 exists to stop.
        let skillBodies = skillBodies |> Map.filter (fun path _ -> not (isIgnoredJunk path))

        // `(root, id)` -> the file set that copy carries. A (root, id) with no file at all has NO
        // entry, which `verifyFiles` reads as "this root carries no copy of the skill" — the same
        // skill-level fact `verify` reported when the single `SKILL.md` body was `None`.
        let filesByCopy =
            skillBodies
            |> Map.toList
            |> List.choose (fun (path, body) ->
                skillCopyOfPath path
                |> Option.map (fun (root, id, relative) ->
                    (root, id),
                    { SkillMirror.SkillFile.RelativePath = relative
                      SkillMirror.SkillFile.Body = body }))
            |> List.groupBy fst
            |> List.map (fun (copy, group) -> copy, group |> List.map snd)
            |> Map.ofList

        // A directory WITHOUT `SKILL.md` is not a copy of the skill. `SKILL.md` is what MAKES a
        // directory a skill — it is what `SkillMirror.skillPath` names, what `skillIdOfPath`
        // recognises, and what `mirrorFiles` refuses to materialize without (`MissingSkillFile`).
        //
        // This is load-bearing, not pedantry. `verifyFiles` derives skill-level `MissingRoots` from
        // `Files = None`, and its per-file union from what the PRESENT roots carry. So a root that
        // lost `SKILL.md` but kept one stray file would count as present, `SKILL.md` would never
        // enter the union, and its loss would go unreported — and with EVERY root in that state (a
        // product skill deleted but for a leftover auxiliary) the whole skill reads as coherent.
        // That is strictly weaker than the pre-#726 surface, which reported it. Treating such a root
        // as carrying no copy restores exactly the pre-#726 verdict, and states one repair ("this
        // root has no copy") instead of a per-file inventory of a directory that is not a skill.
        let copyFiles root id =
            match Map.tryFind (root, id) filesByCopy with
            | Some files when files |> List.exists (fun file -> file.RelativePath = "SKILL.md") -> Some files
            | _ -> None

        // One observation set for BOTH verify entry points, over the union of ids either of them
        // expects. A row for an id the fold in question does not expect is inert (each entry point
        // folds over its OWN `expected`), so sharing the set is what keeps the two from disagreeing
        // about what is on disk — the same reason `SkillMirror` shares its observation fold.
        let actual =
            [ for id in
                  (expected |> List.map (fun skill -> skill.Id))
                  @ (ownerExpected |> List.map (fun skill -> skill.Id))
                  |> List.distinct do
                  for root in agentSkillRoots ->
                      { SkillMirror.ActualSkillFiles.Root = root
                        SkillMirror.ActualSkillFiles.Id = id
                        SkillMirror.ActualSkillFiles.Files = copyFiles root id } ]

        // The three facts, normalized off the two verify shapes so ONE classification rule sees
        // both. `verifyFiles` reports `SkillFileDrift`, `verifyFileSet` reports `DeclaredFileDrift`;
        // their first three fields are the SAME three independent facts with the same meanings, so
        // the rule below is written once over `(relativePath, missingRoots, divergent,
        // hashMismatchRoots)` rather than twice over two records that would drift apart.
        let classifyDrift
            (id: string)
            (skillMissingRoots: string list)
            (files: (string * string list * bool * string list) list)
            =
            // A root carrying NO copy of the skill at all is reported once, at its `SKILL.md` —
            // the file that MAKES a directory a skill (`SkillMirror.skillPath` /
            // `skillIdOfPath`) — rather than as one absence per file in the union. The repair is
            // a single one ("this root has no copy"), and naming every auxiliary of a root that
            // has none of them buries it under its own detail; it is also exactly what this
            // surface reported before it could see the auxiliaries, so the pre-#726 output is
            // preserved for that case.
            //
            // #736: a root with no copy is an ABSENCE, never a byte disagreement — but WHICH
            // absence depends on whether any root still has one. `verifyFiles` reports the
            // whole-skill loss (every root in `MissingRoots`) through this same field, and there
            // the sibling root an operator would mirror FROM does not exist. Classifying that as
            // not-mirrored would make the advisory assert "another root carries this file" about
            // a file no root carries.
            let skillLostEverywhere =
                agentSkillRoots
                |> List.forall (fun root -> List.contains root skillMissingRoots)

            let missingSkillCondition =
                if skillLostEverywhere then
                    LostEverywhere
                else
                    NotMirroredAt

            let missingSkillPaths =
                skillMissingRoots
                |> List.map (fun root -> missingSkillCondition, SkillMirror.skillPath root id)

            let rootsWithSkill =
                agentSkillRoots
                |> List.filter (fun root -> not (List.contains root skillMissingRoots))

            // The pre-existing root-selection rule, unchanged in substance and now applied PER
            // FILE: when a reference digest pinpoints the offending root(s)
            // (`HashMismatchRoots`), report only those. When copies merely disagree with no
            // reference to arbitrate — a divergent process skill, a product skill whose digest
            // wasn't recorded, and EVERY auxiliary, since the ADR-0017 manifest
            // content-addresses `SKILL.md` alone — the canonical copy is unknowable, so report
            // every root that HAS the file and let the operator reconcile them.
            //
            // #736 classifies the three sources without changing which paths they produce.
            // `fileMissingRoots` ranges over roots that CARRY THE SKILL but not this file, so
            // it is not-mirrored — and never `LostEverywhere` under `verifyFiles`, which builds
            // the per-file union from the PRESENT roots, so a file in the union is carried by at
            // least one of them. `hashMismatchRoots` and the digest-less `divergentRoots` both
            // range over roots that HAVE the file, so they are divergent. The classes are
            // therefore disjoint per (root, file), and the union below is what the pre-#736
            // fold returned.
            //
            // #733 note: under `verifyFileSet` the per-file union is `declared ∪ observed`, so a
            // DECLARED owner-sourced file absent from every root that carries the skill lands in
            // `fileMissingRoots` for all of them. That is still not-mirrored rather than lost —
            // the skill itself is present, and the repair is per-file, not "restore the copy".
            let filePaths =
                files
                |> List.collect (fun (relativePath, fileMissingRoots, divergent, hashMismatchRoots) ->
                    let rootsWithFile =
                        rootsWithSkill
                        |> List.filter (fun root -> not (List.contains root fileMissingRoots))

                    let divergentRoots =
                        if divergent && List.isEmpty hashMismatchRoots then
                            rootsWithFile
                        else
                            []

                    (fileMissingRoots |> List.map (fun root -> NotMirroredAt, root))
                    @ (hashMismatchRoots @ divergentRoots |> List.map (fun root -> DivergentAt, root))
                    |> List.map (fun (condition, root) -> condition, SkillMirror.skillFilePath root id relativePath))

            missingSkillPaths @ filePaths

        // The process ∪ product union, verified against `ExpectedSkill.Sha256` — a digest that
        // content-addresses `SKILL.md` and nothing else (the #727 gap). Unchanged by #733.
        let processProductClassified =
            SkillMirror.verifyFiles agentSkillRoots expected actual
            |> List.collect (fun drift ->
                classifyDrift
                    drift.Id
                    drift.MissingRoots
                    (drift.Files
                     |> List.map (fun file ->
                         file.RelativePath, file.MissingRoots, file.Divergent, file.HashMismatchRoots)))

        // FS-GG/FS.GG.SDD#733: the owner-sourced class, verified against the COMPLETE declared file
        // set recorded in provenance. `verifyFileSet` is the same algorithm with fact 3 widened from
        // `SKILL.md` to every declared file, so a tampered driver AUXILIARY is arbitrated to the
        // offending root instead of falling back to "report every present root".
        //
        // `DeclaredFileDrift.UndeclaredRoots` — fact 4, "this root carries a file the declaration
        // does not cover" — is deliberately NOT classified here. It is a genuine fourth condition
        // with a fourth repair, and every existing class's advisory sentence would be FALSE of it
        // (`NotMirrored` asserts a sibling root carries the file, and it is reported at the roots
        // that DO carry it). Reporting it needs a fourth advisory bucket on `DoctorSummary` and its
        // rendering, which is a wider change than #733's touch-set — filed as
        // FS-GG/FS.GG.SDD#750. Dropping it reports strictly what #733 asked for and regresses
        // nothing: before #733 this class was not content-verified at all.
        //
        // FACT 3 NOW SPANS ONE DIGEST DOMAIN, which is why there is no filter here any more.
        //
        // #751 shipped a mitigation: a `HashMismatchRoots` entry was DROPPED when an un-normalized
        // raw comparison cleared it, because `DriverPaths[].Sha256` could hold either a raw or a
        // canonical digest and this fold could not tell which. Accepting either domain is strictly
        // weaker than knowing which to expect — a body could clear on the domain it was not
        // recorded in — and #751 said so, deferring the fix to FS-GG/FS.GG.SDD#752 AC4. This is it.
        //
        // Both producers of the field now record `SkillMirror.sha256` of the body they wrote
        // (`DriverSkills` and `GameSkills` alike), and `HandlersUpgrade.preservedFilesVerified`
        // compares it the same way. `verifyFileSet` arbitrates with `SkillMirror.sha256`, so the
        // comparison here is already exactly the recorded domain and the allowance has nothing left
        // to forgive: the CRLF case matches outright, and the raw domain — the one no read seam can
        // reproduce for a BOM-prefixed file — is no longer accepted at all.
        let ownerClassified =
            SkillMirror.verifyFileSet agentSkillRoots ownerExpected actual
            |> List.collect (fun drift ->
                classifyDrift
                    drift.Id
                    drift.MissingRoots
                    (drift.Files
                     |> List.map (fun file ->
                         file.RelativePath, file.MissingRoots, file.Divergent, file.HashMismatchRoots)))

        let classified = processProductClassified @ ownerClassified

        let pathsOf condition =
            classified
            |> List.filter (fst >> (=) condition)
            |> List.map snd
            |> List.distinct
            |> List.sort

        let all = classified |> List.map snd |> List.distinct |> List.sort

        { NotMirrored = pathsOf NotMirroredAt
          Lost = pathsOf LostEverywhere
          Divergent = pathsOf DivergentAt
          All = all
          IgnoredJunk = ignoredJunk }

    let private noTargetStep (stepId: ReconciliationStepId) preview : ReconciliationStep =
        { StepId = stepId
          Kind = stepId
          DiffPreview = preview
          Outcome = ReconciliationOutcome.NoTarget
          TargetPaths = [] }

    let private cliSelfUpdateStep installedVersion (minimumText: string) : ReconciliationStep =
        { StepId = ReconciliationStepId.CliSelfUpdate
          Kind = ReconciliationStepId.CliSelfUpdate
          DiffPreview = $"installed {installedVersion} → target ≥{minimumText}"
          Outcome = ReconciliationOutcome.WouldApply
          TargetPaths = [] }

    // FS-GG/FS.GG.SDD#313: the source labels reported alongside the effective minimum, so a
    // divergence between the two floors is legible rather than silent.
    [<Literal>]
    let providerDescriptorSource = "providerDescriptor"

    [<Literal>]
    let scaffoldProvenanceSource = "scaffoldProvenance"

    [<Literal>]
    let workspaceFloorSource = "workspaceFloor"

    // Only a parseable `major.minor.patch` counts as a real minimum. An unparseable value is
    // not this module's to report: the provider floor degrades to coherent-by-absence (as it
    // always has), and an unparseable `sdd.minToolVersion` is already warned by
    // `project.minToolVersionUnparseable` at report assembly — re-reporting would double-count.
    let private validVersion raw =
        Fsgg.Version.tryParse raw |> Option.map (fun _ -> raw)

    // The strictest of the candidate minima, each paired with the source that declared it.
    // Candidates arrive in tie-break order and a later one replaces the incumbent only when it
    // is *strictly* greater, so an equal floor leaves the earlier (provider-side) source named —
    // the pre-existing authority, and the tie makes the choice inert anyway.
    let private strictestMinimum (candidates: (string * string) list) =
        candidates
        |> List.fold
            (fun best (version, source) ->
                match best with
                | None -> Some(version, source)
                | Some(incumbent, _) ->
                    match Fsgg.Version.compare version incumbent with
                    | Some rank when rank > 0 -> Some(version, source)
                    | _ -> best)
            None

    let private cliAxisOf installedVersion effectiveMinimum =
        match effectiveMinimum with
        | None -> "coherentByAbsence", None
        | Some(minimum, _) ->
            match Fsgg.Version.tryParse installedVersion with
            | None -> "undeterminable", None
            | Some _ ->
                match Fsgg.Version.compare installedVersion minimum with
                | Some -1 -> "behind", Some $"{installedVersion} → {minimum}"
                | _ -> "atOrAbove", None

    /// Compute the drift picture from already-snapshotted inputs. `descriptor` is the
    /// live provider descriptor resolved from `.fsgg/providers.yml` by the provenance's
    /// provider name; when it disagrees with the provenance-recorded minimum, the live
    /// descriptor value wins (spec Assumption). `workspaceFloor` is the raw
    /// `sdd.minToolVersion` declared in `.fsgg/project.yml` (FS-GG/FS.GG.SDD#305); the
    /// **stricter** of the two floors governs the CLI axis (FS-GG/FS.GG.SDD#313), so the
    /// remediation verbs can no longer report `coherent` against a floor the author declared.
    let compute
        (provenance: ScaffoldProvenanceRecord option)
        (descriptor: ProviderDescriptor option)
        (workspaceFloor: string option)
        (installedVersion: string)
        (presentArtifacts: Set<string>)
        (skillBodies: Map<string, string>)
        : DriftReport =
        let workspaceCandidate =
            workspaceFloor
            |> Option.bind validVersion
            |> Option.map (fun version -> version, workspaceFloorSource)
            |> Option.toList

        match provenance with
        | None ->
            // Not a scaffolded product (FR-015 / R12): no provider, so no re-pin and no re-seed
            // to preview. The CLI axis is not scaffold-scoped, though — it is a fact about the
            // *installed tool* — so a workspace-declared floor still governs it and still
            // previews a self-update. Absent that floor this is byte-identical to the pre-#313
            // shape: coherent by absence, no steps, coherent.
            let effectiveMinimum = strictestMinimum workspaceCandidate
            let cliAxis, cliBehindBy = cliAxisOf installedVersion effectiveMinimum

            let steps =
                match cliAxis, effectiveMinimum with
                | "behind", Some(minimum, _) -> [ cliSelfUpdateStep installedVersion minimum ]
                | _ -> []

            { HasProvenance = false
              ProviderName = None
              InstalledCliVersion = installedVersion
              RequiredMinimumCliVersion = effectiveMinimum |> Option.map fst
              RequiredMinimumCliVersionSource = effectiveMinimum |> Option.map snd
              CliAxis = cliAxis
              CliBehindBy = cliBehindBy
              ExpectedArtifactCount = expectedArtifactCount
              MissingArtifactPaths = []
              SkillDriftPaths = []
              SkillNotMirroredPaths = []
              SkillLostPaths = []
              SkillDivergentPaths = []
              // No provenance ⇒ not a scaffold ⇒ nothing to backfill (#624).
              OwnerSkillBackfillPaths = []
              // No provenance ⇒ no skill drift computed at all ⇒ nothing was compared, so nothing
              // was excluded. Reporting an exclusion here would claim a subtraction this arm never
              // made (#747).
              IgnoredSkillJunkPaths = []
              Steps = steps
              IsCoherent = List.isEmpty steps }
        | Some record ->
            // Live descriptor minimum wins over the provenance-recorded one — by *presence*, not
            // by parseability: a descriptor that declares an unparseable minimum still shadows the
            // recorded value, and degrades to coherent-by-absence exactly as it did before #313.
            let providerCandidate =
                match descriptor |> Option.bind (fun d -> d.MinimumCliVersion) with
                | Some raw -> raw |> validVersion |> Option.map (fun v -> v, providerDescriptorSource)
                | None ->
                    record.RequiredMinimumCliVersion
                    |> Option.bind validVersion
                    |> Option.map (fun v -> v, scaffoldProvenanceSource)
                |> Option.toList

            // Provider-side first: it is the tie-break winner (see `strictestMinimum`).
            let effectiveMinimum = strictestMinimum (providerCandidate @ workspaceCandidate)
            let validMinimum = effectiveMinimum |> Option.map fst
            let cliAxis, cliBehindBy = cliAxisOf installedVersion effectiveMinimum

            let missing =
                expectedArtifactPaths
                |> List.filter (fun path -> not (Set.contains path presentArtifacts))
                |> List.sort

            // ADR-0063 / #624: the owner-sourced skill copies this scaffold should carry but is
            // missing — kept OUT of `MissingArtifactPaths`/`ExpectedArtifactCount` (which are the
            // SEEDED-skeleton axis) so those report facts and their goldens are undisturbed. A
            // missing owner skill is still a missing expected artifact, so it is reconciled by the
            // same no-clobber `artifactReSeed` step (its `TargetPaths` union, below).
            let ownerBackfillMissing =
                ownerSourcedBackfill record
                |> List.map fst
                |> List.filter (fun path -> not (Set.contains path presentArtifacts))
                |> List.distinct
                |> List.sort

            let reSeedTargets = missing @ ownerBackfillMissing |> List.distinct |> List.sort

            let minimumText = Option.defaultValue "" validMinimum

            let cliStep =
                if cliAxis = "behind" then
                    cliSelfUpdateStep installedVersion minimumText
                else
                    noTargetStep ReconciliationStepId.CliSelfUpdate "no CLI version target"

            // R6: re-pin is value-agnostic and currently a recognized-but-usually-inert
            // step — generic SDD holds no template-version drift signal, so it previews as
            // `noTarget` and embeds no provider/template literal either way.
            let rePinStep = noTargetStep ReconciliationStepId.TemplateRePin "no re-pin target"

            let reSeedStep =
                if List.isEmpty reSeedTargets then
                    noTargetStep ReconciliationStepId.ArtifactReSeed "no missing artifacts"
                else
                    { StepId = ReconciliationStepId.ArtifactReSeed
                      Kind = ReconciliationStepId.ArtifactReSeed
                      DiffPreview = reSeedTargets |> List.map (fun path -> $"+ {path} (new)") |> String.concat "\n"
                      Outcome = ReconciliationOutcome.WouldApply
                      TargetPaths = reSeedTargets }

            let steps = [ cliStep; rePinStep; reSeedStep ]

            // 058/ADR-0014 §Decision 3: the content-addressed union verify over process +
            // product skills. Non-empty ⇒ content drift (advisory), which also makes the
            // scaffold not coherent alongside the CLI-axis / missing-artifact drivers.
            let skillDrift = computeSkillDrift provenance skillBodies

            let hasActionableWork =
                steps
                |> List.exists (fun step -> step.Outcome = ReconciliationOutcome.WouldApply)

            { HasProvenance = true
              // 085: a dev-repo document carries no provider — report `None` rather than the
              // empty provider field, so doctor/upgrade read as "tracked, provider-less" (a
              // dev-repo) instead of naming an empty provider. Everything else — the seeded
              // artifact axis, the `noTarget` re-pin, the coherent-by-absence CLI axis — is the
              // shared path, so a dev-repo reconciles its seeded skeleton like any scaffold.
              ProviderName = (if isDevRepo record then None else Some record.ProviderName)
              InstalledCliVersion = installedVersion
              RequiredMinimumCliVersion = validMinimum
              RequiredMinimumCliVersionSource = effectiveMinimum |> Option.map snd
              CliAxis = cliAxis
              CliBehindBy = cliBehindBy
              ExpectedArtifactCount = expectedArtifactCount
              MissingArtifactPaths = missing
              SkillDriftPaths = skillDrift.All
              SkillNotMirroredPaths = skillDrift.NotMirrored
              SkillLostPaths = skillDrift.Lost
              SkillDivergentPaths = skillDrift.Divergent
              OwnerSkillBackfillPaths = ownerBackfillMissing
              IgnoredSkillJunkPaths = skillDrift.IgnoredJunk
              Steps = steps
              IsCoherent = not hasActionableWork && List.isEmpty skillDrift.All }
