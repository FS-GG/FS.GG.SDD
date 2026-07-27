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
/// ## How this lane can fail — FS-GG/FS.GG.SDD#735, AC4
///
/// "Read-only" is not "cannot fail", and the header used to read as though it were. The lane's
/// actual outcomes, exhaustively:
///
/// * **Drift** — the file set disagrees with the recorded expectation. `IsCoherent = false` and a
///   `doctor.driftDetected` WARNING; `doctor` still exits 0 (the drift is advisory, and `upgrade`
///   is what repairs it).
/// * **An unreadable file** — the path exists but its bytes are unavailable (a permission bit, an
///   exclusive lock on Windows, a symlink loop, an over-long path). Since #726 this lane reads
///   every file under every expected skill directory across three roots, so the set of files that
///   can be in this state is the whole tree, not the ~51 seeded `SKILL.md` paths. Per the #735
///   decision it is a FINDING about the workspace, never a tool defect: the read edge reports an
///   `unreadableFile` warning NAMING the path and hands this driver no bytes, so the file lands in
///   `skillBodies` exactly as a missing one does and is reported as drift rather than as coherent.
///   Exit stays 0. It is never silently passed, and never mistaken for a broken tool.
/// * **A genuine tool defect** — an internal fault at the effect edge. `toolDefect`, exit 2. That
///   is what exit 2 continues to mean here, and nothing else in this lane produces it.
///
/// `HandlersUpgrade` shares `skillReadGate`/`skillBodies` verbatim, so all three outcomes are the
/// same in that lane (#735 AC5); `upgrade` additionally has its own step-failure defects.
module internal HandlersDoctor =

    // Shared with HandlersUpgrade (both resolve the same drift inputs from the snapshots).
    let resolveProvenance model =
        match snapshot ".fsgg/scaffold-provenance.json" model with
        | Some snap -> tryParse snap.Text
        | None -> None

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

    let presentArtifacts model =
        (Drift.expectedArtifactPaths @ ownerSkillTargetPaths model)
        |> List.filter (fun path -> snapshot path model |> Option.isSome)
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
        let expectedIds =
            Set.ofList (
                SeededSkills.skillNames
                @ (Drift.productSkillEntries (resolveProvenance model) |> List.map fst)
            )

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
            // entry, and its absence is the drift fact). `ownerSkillTargetPaths` rides along because
            // it is read for `presentArtifacts`, NOT for the content fold — the owner-sourced class
            // is not in `Drift.expectedSkills` and its bodies never reach `skillBodies` (#733).
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
        |> List.choose (fun path -> snapshot path model |> Option.map (fun snap -> path, snap.Text))
        |> Map.ofList

    // FS-GG/FS.GG.SDD#313: the workspace-declared `sdd.minToolVersion` floor, read from the
    // `.fsgg/project.yml` snapshot the remediation plan now takes. A malformed config yields no
    // floor and no diagnostic here — `ReportAssembly` already owns that reporting.
    let resolveWorkspaceFloor model =
        match snapshot ".fsgg/project.yml" model with
        | Some snap ->
            match FS.GG.SDD.Artifacts.Config.parseProjectConfig snap with
            | Ok config -> config.MinToolVersion
            | Error _ -> None
        | None -> None

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
                let summary = doctorSummaryOf drift

                // Non-blocking drift advisory (doctor always exits 0) whenever there is drift to
                // reconcile. #313: `IsCoherent` — not `HasProvenance` — is the gate, because an
                // unmet workspace floor is real drift in a workspace that was never scaffolded.
                // With no provenance and no floor, `IsCoherent` is true, so this stays silent.
                let diagnostics =
                    if not drift.IsCoherent then
                        [ doctorDriftDetected () ]
                    else
                        []

                { model with
                    Doctor = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics },
                []
