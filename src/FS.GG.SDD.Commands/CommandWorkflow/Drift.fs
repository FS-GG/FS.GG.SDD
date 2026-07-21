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
          SkillDriftPaths: string list
          // ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced skill copies (driver + product classes)
          // this scaffold is EXPECTED to carry — per the recorded parameters + present-skill set —
          // but is missing on disk. These are backfilled no-clobber by the `artifactReSeed` step,
          // so they are folded into that step's `TargetPaths`; the field names them on their own for
          // observability. Sorted, deduped. Non-empty ⇒ actionable (re-seed WouldApply, not coherent).
          OwnerSkillBackfillPaths: string list
          Steps: ReconciliationStep list
          IsCoherent: bool }

    // The content-addressed union `verify` covers: SDD-seeded *process* skills (canonical body =
    // the embedded seeded body) and provider *product* skills recorded in provenance (canonical
    // digest = the recorded `sha256`). `skillBodies` is the caller-read body of each skill copy
    // keyed by its on-disk path; a copy absent from `skillBodies` is treated as missing.
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

    let private computeSkillDriftPaths
        (provenance: ScaffoldProvenanceRecord option)
        (skillBodies: Map<string, string>)
        : string list =
        let expected = expectedSkills provenance

        let actual =
            [ for skill in expected do
                  for root in agentSkillRoots ->
                      { SkillMirror.ActualCopy.Root = root
                        SkillMirror.ActualCopy.Id = skill.Id
                        SkillMirror.ActualCopy.Body = Map.tryFind (SkillMirror.skillPath root skill.Id) skillBodies } ]

        SkillMirror.verify agentSkillRoots expected actual
        |> List.collect (fun drift ->
            // When a reference digest pinpoints the offending root(s) (`HashMismatchRoots`), report
            // only those. When copies merely disagree with no reference to arbitrate (a divergent
            // process skill, or a product skill whose digest wasn't recorded), the canonical copy is
            // unknowable — report every present root so the operator reconciles them.
            let presentRoots =
                agentSkillRoots
                |> List.filter (fun root -> not (List.contains root drift.MissingRoots))

            let divergentRoots =
                if drift.Divergent && List.isEmpty drift.HashMismatchRoots then
                    presentRoots
                else
                    []

            let roots = drift.MissingRoots @ drift.HashMismatchRoots @ divergentRoots
            roots |> List.map (fun root -> SkillMirror.skillPath root drift.Id))
        |> List.distinct
        |> List.sort

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
              // No provenance ⇒ not a scaffold ⇒ nothing to backfill (#624).
              OwnerSkillBackfillPaths = []
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
            let skillDriftPaths = computeSkillDriftPaths provenance skillBodies

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
              SkillDriftPaths = skillDriftPaths
              OwnerSkillBackfillPaths = ownerBackfillMissing
              Steps = steps
              IsCoherent = not hasActionableWork && List.isEmpty skillDriftPaths }
