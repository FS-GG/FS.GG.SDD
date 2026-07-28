namespace FS.GG.SDD.Commands.Internal

open Fsgg
open Fsgg.Schemas
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.HandlersScaffold

/// `fsgg-sdd doctor` handler (feature 053, US1). A strictly read-only projection: the
/// `plan` stage snapshots the provenance, provider registry, and every expected seeded
/// artifact (read effects only); a second pass (058/ADR-0014 P1, FS-GG/FS.GG.SDD#726) then
/// enumerates the declared skill roots and snapshots every skill copy FILE across them, and
/// this driver computes the shared pure `Drift` picture (now content-addressed over the whole
/// file set, not `SKILL.md` alone) and builds the `DoctorSummary`. It emits **no** mutating
/// effect on any path (FR-002 / SC-001), so a write-audit over a doctor run finds only
/// `ReadFile`/`EnumerateDirectory`.
///
/// ## How this lane fails — FS-GG/FS.GG.SDD#735 AC4
///
/// Everything above says what the lane *reads*, and "strictly read-only" is easy to misread as
/// *cannot fail*. It can, in three ways that are deliberately kept distinguishable — telling
/// them apart is the whole job, because each sends the operator somewhere different:
///
/// - **Drift** — a subject was read and disagreed with what it should be. `IsCoherent` is
///   false and `doctorDriftDetected` points at `upgrade`, at exit **0**: this lane reports,
///   `upgrade` repairs.
/// - **Unreadable** — a subject is present and could *not* be read (#745, decision #754).
///   `IsCoherent` is false, because a verdict may never report coherent over a subject it did
///   not read, and the read edge emits an `unreadableFile` warning naming the path and the
///   reason. Still exit **0**: #754 rejected refusing at the edge precisely so that one
///   permission bit cannot wedge a repo through a lane documented read-only and exit 0. Note
///   the drift advisory above is keyed on `drift.IsCoherent`, *not* `summary.IsCoherent`, so
///   an unreadable-only run does not say "run `upgrade`" — `upgrade` cannot repair a file it
///   cannot read either, and the warning says `chmod +r` instead. Such a file also counts as
///   PRESENT (see `presentArtifacts`); reporting it *missing* would send the operator to
///   re-seed a file that is right there.
/// - **Tool defect** — the tool itself failed. Exit **2**, and nothing else reaches this
///   class. That last clause is what #735 was filed about: before #745, a `chmod 000` on any
///   file under an expected skill directory landed *here*, so an environment fault the
///   operator could fix was reported as a broken tool — across a read set #726 had just
///   widened from ~51 known `SKILL.md` paths to every file under every declared root.
///
/// Known residual, owned by FS-GG/FS.GG.SDD#743, not by this module: an unreadable skill copy
/// is *additionally* classified as skill drift of the *not mirrored* class, because
/// `SkillMirror.verifyFiles` builds its per-file union from the rows it observed and an unread
/// body contributes none. By then the verdict is already non-coherent and the file is already
/// named, so the cost is a misleading remedy hint rather than a wrong pass.
module internal HandlersDoctor =

    // Shared with HandlersUpgrade (both resolve the same drift inputs from the snapshots).
    // Total over `ReadResult` since #745. `Unreadable` collapses to `None` — the same value an
    // absent provenance yields — because there is genuinely no record to parse either way; what
    // changed is that the collapse is now a written decision and the fact is separately reported
    // by `unreadableSubjects`, so `doctor` can no longer answer `isCoherent: true` off the back of
    // a provenance file it failed to open.
    let resolveProvenance model =
        match readOf ".fsgg/scaffold-provenance.json" model with
        | Bytes snap -> tryParse snap.Text
        | Absent
        | Unreadable _ -> None

    let resolveDriftDescriptor model (provenance: ScaffoldProvenanceRecord option) =
        match provenance with
        | Some record ->
            resolveDescriptors model
            |> List.tryFind (fun descriptor -> descriptor.Name = record.ProviderName)
        | None -> None

    // ADR-0063 / FS-GG/FS.GG.SDD#624: the owner-sourced skill copies (driver + product classes) this
    // scaffold is expected to carry, derived from the recorded provenance by the same embedded
    // materialize-and-verify plan `scaffold` runs. These are NOT in `Drift.expectedArtifactPaths`
    // (the seeded set), so — like the product-skill copies — they are read in the provenance-driven
    // second pass so their presence/absence is known before the backfill drift is computed.
    let ownerSkillTargetPaths model =
        match resolveProvenance model with
        | Some record -> Drift.ownerSourcedBackfill record |> List.map fst |> List.distinct |> List.sort
        | None -> []

    /// FS.GG.SDD#745 AC4: an unreadable expected artifact is PRESENT, not missing. It used to test
    /// `Option.isSome`, so a file that is right there but unreadable dropped out of the present set
    /// and into `MissingArtifactPaths` — `doctor --text` said the file was missing and pointed at
    /// `upgrade`, which would then plan a re-seed write straight into it. "I could not read it" is
    /// reported by `unreadableSubjects` (and the edge's per-file warning), which is a different
    /// fact from "it is not there" and now says so.
    let presentArtifacts model =
        (Drift.expectedArtifactPaths @ ownerSkillTargetPaths model)
        |> List.filter (fun path ->
            match readOf path model with
            | Bytes _
            | Unreadable _ -> true
            | Absent -> false)
        |> Set.ofList

    // 058/ADR-0014 P1: the provider *product* skill copies to content-verify — every product id
    // recorded in provenance (never the SDD-seeded `fs-gg-sdd-*` process namespace), across every
    // declared root. These are NOT in `Drift.expectedArtifactPaths` (which is the seeded set), so
    // `doctor`/`upgrade` read them in a provenance-driven second pass.
    let productSkillCopyPaths model =
        // Same confined id source as `Drift.expectedSkills` (provider `.agents/skills/` only), so
        // doctor never reads phantom copies for a product file that merely looks skill-shaped.
        let ids = Drift.productSkillEntries (resolveProvenance model) |> List.map fst

        [ for id in ids do
              for root in agentSkillRoots -> SkillMirror.skillPath root id ]
        |> List.distinct
        |> List.sort

    // FS-GG/FS.GG.SDD#726: a skill is a DIRECTORY, not a file. Probing `SkillMirror.skillPath`
    // alone can only ever observe `SKILL.md`, so the file SET of each copy is discovered by
    // enumerating each declared root's `skills/` tree.
    //
    // #726 AC6, settled explicitly rather than by reaching for direct IO: this needs NO new effect.
    // `EnumerateDirectory` is an EXISTING read effect — recursive, listing-only (it carries no file
    // content), interpreted at the edge like every other effect (Principle V), and already
    // classified read-only by `Foundation`. `refresh` already enumerates `.agents/skills` for the
    // same reason. So the doctor lane stays offline and write-free per FR-002 / SC-001, and a
    // write-audit over a doctor run still finds only `ReadFile`/`EnumerateDirectory`.
    let skillRootEnumerations =
        agentSkillRoots |> List.map (fun root -> EnumerateDirectory(root + "/skills"))

    // Every skill-copy FILE the enumerated roots actually carry, confined to the ids the drift fold
    // EXPECTS — the SDD-seeded process namespace ∪ the product ids recorded in provenance, the same
    // union `Drift.expectedSkills` builds. `verifyFiles` folds over `expected`, so files under an
    // unexpected skill directory could not affect the verdict; confining the set keeps `doctor` from
    // reading bodies nothing verifies rather than relying on them being ignored later.
    let skillCopyFilePaths model =
        // FS-GG/FS.GG.SDD#733: the SDD-seeded process namespace ∪ the product ids recorded in
        // provenance ∪ the OWNER-SOURCED (driver + GameSkill) ids recorded in provenance — exactly the
        // union `Drift.computeSkillDrift` verifies across its two entry points. The owner-sourced
        // arm was the gap: its ids were in neither expected set, so its bodies were never collected
        // and its per-file digests arbitrated nothing.
        let expectedIds =
            Drift.contentVerifiedSkillIds (resolveProvenance model) |> Set.ofList

        skillRootEnumerations
        |> List.collect (fun effect ->
            match effect with
            | EnumerateDirectory path ->
                (directoryListing path model).Split([| '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.toList
            | _ -> [])
        |> List.map normalizeRelativePath
        |> List.filter (fun path ->
            match Drift.skillCopyOfPath path with
            | Some(_, id, _) -> Set.contains id expectedIds
            | None -> false)
        |> List.distinct
        |> List.sort

    // The read-gate that brings the skill copies into snapshots before the drift is computed.
    // `None` ⇒ ready to compute; `Some effects` ⇒ not ready (emit the effects, or `[]` while
    // awaiting their interpretation). Both phases resolve on effect *interpretation*, not snapshot
    // presence — a missing copy stays absent after its read, so a presence gate would loop forever
    // on a deleted copy.
    //
    // One phase resolves to: `None` when nothing is outstanding, and otherwise the outstanding
    // effects not yet emitted. Deriving that remainder from the OUTSTANDING set rather than testing
    // the whole set is what matters since #726: `skillCopyFilePaths` legitimately names paths
    // `Foundation.remediationReadEffects` already planned (every seeded `SKILL.md` is in both sets),
    // so a whole-set "is any of these already planned?" test answers yes on the first pass and parks
    // the gate at "emit nothing" forever — no effects produced, so the run loop goes idle and the
    // drift is never computed at all.
    //
    // The `Some []` arm is unreachable as the lanes stand — `CommandWorkflow` gates both drivers on
    // `allPlannedReadsInterpreted`, so nothing planned is ever still in flight here — and is kept
    // only so this stays correct if that guard is ever relaxed. It must never be the ONLY thing a
    // phase can return, which is precisely the parked-gate failure above.
    let private phase (effects: CommandEffect list) model =
        let outstanding =
            effects
            |> List.filter (fun effect -> not (hasInterpreted (effectKey effect) model))

        let unemitted =
            outstanding
            |> List.filter (fun effect -> not (hasPlanned (effectKey effect) model))

        if List.isEmpty outstanding then None
        elif List.isEmpty unemitted then Some []
        else Some unemitted

    let skillReadGate model =
        // Phase 1 (#726): enumerate the skill roots. Until the listings are interpreted, the file
        // set of each copy is unknown, so there is no complete set of bodies to ask for yet.
        match phase skillRootEnumerations model with
        | Some effects -> Some effects
        | None ->
            // Phase 2: the bodies. The enumerated copy files, plus the product copies, whose paths
            // are derived from provenance rather than discovered (an ABSENT copy has no listing
            // entry, and its absence is the drift fact). `ownerSkillTargetPaths` rides along for
            // `presentArtifacts` / the backfill axis.
            //
            // #733: the owner-sourced copies now also reach the CONTENT fold — through
            // `skillCopyFilePaths`, whose expected-id union includes them, so they are DISCOVERED
            // like every other multi-file copy rather than probed one canonical path at a time. A
            // file the provenance DECLARES but no root carries needs no read: `verifyFileSet`
            // compares over `declared ∪ observed`, and its absence from `skillBodies` is exactly the
            // drift fact.
            (productSkillCopyPaths model
             @ ownerSkillTargetPaths model
             @ skillCopyFilePaths model)
            |> List.distinct
            |> List.map ReadFile
            |> fun reads -> phase reads model

    // The read body of every skill copy FILE (seeded expected paths + product copies + every
    // auxiliary the roots carry) keyed by path — the content-addressed input to `Drift.compute`. A
    // file absent from snapshots ⇒ absent here ⇒ `verifyFiles` treats it as missing.
    //
    // The confinement predicate is `Drift.skillCopyOfPath`, the SAME parser the drift fold uses to
    // split these keys back apart, so the collector and the verifier cannot disagree about which
    // paths are skill copies. It also drops the two non-skill members of `expectedArtifactPaths`
    // (`.fsgg/early-stage-guidance.md`, `.gitignore`), as the old `skillIdOfPath` filter did.
    let skillBodies model =
        (Drift.expectedArtifactPaths
         @ productSkillCopyPaths model
         @ skillCopyFilePaths model)
        |> List.filter (fun path -> Drift.skillCopyOfPath path |> Option.isSome)
        |> List.distinct
        |> List.choose (fun path ->
            // Total since #745. An unread body is still dropped from the map — `verifyFiles`
            // compares over `declared ∪ observed` and has nothing to compare a missing body
            // against — but the DROP is no longer the end of the story: `unreadableSubjects`
            // carries the path, so the run cannot close as coherent over a copy it never read.
            match readOf path model with
            | Bytes snap -> Some(path, snap.Text)
            | Absent
            | Unreadable _ -> None)
        |> Map.ofList

    // FS-GG/FS.GG.SDD#313: the workspace-declared `sdd.minToolVersion` floor, read from the
    // `.fsgg/project.yml` snapshot the remediation plan now takes. A malformed config yields no
    // floor and no diagnostic here — `ReportAssembly` already owns that reporting.
    let resolveWorkspaceFloor model =
        match readOf ".fsgg/project.yml" model with
        | Bytes snap ->
            match FS.GG.SDD.Artifacts.Config.parseProjectConfig snap with
            | Ok config -> config.MinToolVersion
            | Error _ -> None
        // #745: an unreadable config declares no floor to this fold, exactly as an absent one does
        // — but it is in `unreadableSubjects`, so `cliAxis` can no longer flip `behind` to
        // `coherentByAbsence` on a floor the run simply could not read.
        | Absent
        | Unreadable _ -> None

    /// FS.GG.SDD#745 / decision #754 — every subject the doctor fold is responsible for and could
    /// not read. Exactly the union `skillBodies` and `presentArtifacts` fold over, plus the two
    /// provenance-driven config reads and the skill-root enumerations, so the set this blocks on is
    /// the set the verdict actually consumed.
    ///
    /// The shape being closed: `skillBodies` is a `List.choose (snapshot …)`, so an unread body is
    /// simply DROPPED from the map, and `SkillMirror.verifyFiles` builds its per-file union from
    /// the OBSERVED rows — a file no root contributed a row for is never compared at all.
    /// `presentArtifacts` has the mirror-image shape: `Option.isSome`, so an unreadable expected
    /// artifact reads as *missing*, which is the wrong finding for a file that is right there.
    let unreadableSubjects model =
        let bodyPaths =
            (Drift.expectedArtifactPaths
             @ ownerSkillTargetPaths model
             @ productSkillCopyPaths model
             @ skillCopyFilePaths model
             @ [ ".fsgg/scaffold-provenance.json"; ".fsgg/project.yml" ])
            |> List.distinct

        (bodyPaths |> List.map (fun path -> readOf path model))
        @ (agentSkillRoots |> List.map (fun root -> enumerationOf (root + "/skills") model))
        |> unreadablePathsOf

    let computeDrift model =
        let provenance = resolveProvenance model
        let descriptor = resolveDriftDescriptor model provenance

        Drift.compute
            provenance
            descriptor
            (resolveWorkspaceFloor model)
            model.Request.GeneratorVersion.Version
            (presentArtifacts model)
            (skillBodies model)

    let doctorSummaryOf (drift: Drift.DriftReport) : DoctorSummary =
        { HasProvenance = drift.HasProvenance
          ProviderName = drift.ProviderName
          InstalledCliVersion = drift.InstalledCliVersion
          RequiredMinimumCliVersion = drift.RequiredMinimumCliVersion
          RequiredMinimumCliVersionSource = drift.RequiredMinimumCliVersionSource
          CliAxis = drift.CliAxis
          CliBehindBy = drift.CliBehindBy
          ExpectedArtifactCount = drift.ExpectedArtifactCount
          MissingArtifactPaths = drift.MissingArtifactPaths
          SkillDriftPaths = drift.SkillDriftPaths
          PreviewSteps = drift.Steps
          IsCoherent = drift.IsCoherent }

    let computeDoctorNext model =
        match model.Doctor with
        | Some _ -> model, []
        | None ->
            match skillReadGate model with
            | Some effects ->
                // Product-skill copies not yet read: emit the provenance-driven reads (read-only)
                // and let the tick loop interpret them before the content-addressed drift runs.
                if List.isEmpty effects then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
            | None ->
                let drift = computeDrift model
                let unreadable = unreadableSubjects model

                // FS.GG.SDD#745 / decision #754: a verdict may never report coherent over a subject
                // it did not read. `doctor` is the lane #754 used to REJECT refusing at the edge —
                // it is documented read-only and exit 0, and one permissions accident must not
                // wedge a repo — so the correction here is the VERDICT, not the exit code: the
                // summary stops claiming coherence, and the `unreadableFile` warnings the read edge
                // already emitted name the files and keep the run at exit 0.
                let summary =
                    { doctorSummaryOf drift with
                        IsCoherent = drift.IsCoherent && List.isEmpty unreadable }

                // Non-blocking drift advisory (doctor always exits 0) whenever there is drift to
                // reconcile. #313: `IsCoherent` — not `HasProvenance` — is the gate, because an
                // unmet workspace floor is real drift in a workspace that was never scaffolded.
                // With no provenance and no floor, `IsCoherent` is true, so this stays silent.
                //
                // Keyed on `drift.IsCoherent`, NOT on `summary.IsCoherent`: `doctorDriftDetected`
                // says "run `fsgg-sdd upgrade`", and `upgrade` cannot repair a file it cannot read.
                // Pointing the operator at it for an unreadable subject would be advice that
                // silently does nothing — the `unreadableFile` warning says `chmod +r` instead.
                let diagnostics =
                    if not drift.IsCoherent then
                        [ doctorDriftDetected () ]
                    else
                        []

                { model with
                    Doctor = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics },
                []
