namespace FS.GG.SDD.Commands.Internal

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes

/// `fsgg-sdd doctor` handler (feature 053, US1). A strictly read-only projection: the
/// `plan` stage snapshots the provenance, provider registry, and every expected seeded
/// artifact (read effects only); this driver then computes the shared pure `Drift` picture
/// and builds the `DoctorSummary`. It emits **no** mutating effect on any path (FR-002 /
/// SC-001), so a write-audit over a doctor run finds only `ReadFile`/`EnumerateDirectory`.
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

    let computeDrift model =
        let provenance = resolveProvenance model
        let descriptor = resolveDriftDescriptor model provenance
        Drift.compute provenance descriptor model.Request.GeneratorVersion.Version (presentArtifacts model)

    let doctorSummaryOf (drift: Drift.DriftReport) : DoctorSummary =
        { HasProvenance = drift.HasProvenance
          ProviderName = drift.ProviderName
          InstalledCliVersion = drift.InstalledCliVersion
          RequiredMinimumCliVersion = drift.RequiredMinimumCliVersion
          CliAxis = drift.CliAxis
          CliBehindBy = drift.CliBehindBy
          ExpectedArtifactCount = drift.ExpectedArtifactCount
          MissingArtifactPaths = drift.MissingArtifactPaths
          PreviewSteps = drift.Steps
          IsCoherent = drift.IsCoherent }

    let computeDoctorNext model =
        match model.Doctor with
        | Some _ -> model, []
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
