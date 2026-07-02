namespace FS.GG.SDD.Commands.Internal

open Fsgg
open Fsgg.Schemas
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes

/// `fsgg-sdd doctor` handler (feature 053, US1). A strictly read-only projection: the
/// `plan` stage snapshots the provenance, provider registry, and every expected seeded
/// artifact (read effects only); a provenance-driven second read (058/ADR-0014 P1) then
/// snapshots the provider *product* skill copies across every root, and this driver
/// computes the shared pure `Drift` picture (now content-addressed) and builds the
/// `DoctorSummary`. It emits **no** mutating effect on any path (FR-002 / SC-001), so a
/// write-audit over a doctor run finds only `ReadFile`/`EnumerateDirectory`.
[<AutoOpen>]
module internal HandlersDoctor =

    // Shared with HandlersUpgrade (both resolve the same drift inputs from the snapshots).
    let resolveProvenance model =
        match snapshot ".fsgg/scaffold-provenance.json" model with
        | Some snap -> tryParse snap.Text
        | None -> None

    let resolveDriftDescriptor model (provenance: ScaffoldProvenanceRecord option) =
        match provenance with
        | Some record -> resolveDescriptors model |> List.tryFind (fun descriptor -> descriptor.Name = record.ProviderName)
        | None -> None

    let presentArtifacts model =
        Drift.expectedArtifactPaths
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

    // The read-gate that brings the product-skill copies into snapshots before the drift is
    // computed. `None` ⇒ ready to compute; `Some effects` ⇒ not ready (emit the reads, or `[]`
    // while awaiting their interpretation). A missing copy stays absent after its read, so the
    // gate resolves on read *interpretation*, not snapshot presence (else a deleted copy loops).
    let skillReadGate model =
        let reads = productSkillCopyPaths model |> List.map ReadFile
        let allInterpreted = reads |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)
        let anyPlanned = reads |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        if List.isEmpty reads || allInterpreted then None
        elif anyPlanned then Some []
        else Some reads

    // The read body of every skill copy (process expected paths + product copies) keyed by path,
    // the content-addressed input to `Drift.compute`. A copy absent from snapshots ⇒ absent here
    // ⇒ `verify` treats it as missing.
    let skillBodies model =
        (Drift.expectedArtifactPaths @ productSkillCopyPaths model)
        |> List.filter (fun path -> SkillMirror.skillIdOfPath path |> Option.isSome)
        |> List.choose (fun path -> snapshot path model |> Option.map (fun snap -> path, snap.Text))
        |> Map.ofList

    let computeDrift model =
        let provenance = resolveProvenance model
        let descriptor = resolveDriftDescriptor model provenance
        Drift.compute provenance descriptor model.Request.GeneratorVersion.Version (presentArtifacts model) (skillBodies model)

    let doctorSummaryOf (drift: Drift.DriftReport) : DoctorSummary =
        { HasProvenance = drift.HasProvenance
          ProviderName = drift.ProviderName
          InstalledCliVersion = drift.InstalledCliVersion
          RequiredMinimumCliVersion = drift.RequiredMinimumCliVersion
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
                if List.isEmpty effects then model, []
                else { model with PendingEffects = model.PendingEffects @ effects }, effects
            | None ->
                let drift = computeDrift model
                let summary = doctorSummaryOf drift

                // Non-blocking drift advisory (doctor always exits 0); only when there is a
                // scaffold to reconcile and it is not already coherent.
                let diagnostics =
                    if drift.HasProvenance && not drift.IsCoherent then [ doctorDriftDetected () ] else []

                { model with
                    Doctor = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics },
                []
