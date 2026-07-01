namespace FS.GG.SDD.Commands.Internal

open Fsgg.Provider
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes

/// The pure drift computation shared by both `HandlersDoctor` and `HandlersUpgrade`
/// (feature 053, data-model E7, contracts/drift-model.md). It performs **no I/O**: it
/// consumes snapshots already read via effects and returns the drift picture plus the
/// previewed `ReconciliationStep` list, so the drift a `doctor` previews and the drift an
/// `upgrade` reconciles are always computed by the same code (no second source of truth).
module internal Drift =

    // The expected seeded-skeleton set (R3 / FR-004): for every `SeededSkills.skillNames`,
    // both the `.claude` and `.codex` SKILL.md, plus `.fsgg/early-stage-guidance.md`. This
    // is exactly the set `init` seeds, so `upgrade`'s re-seed re-materializes the missing
    // subset via `initEffects` no-clobber writes (R8). Sorted, deterministic.
    let expectedArtifactPaths =
        (SeededSkills.skillNames
         |> List.collect (fun name -> [ $".claude/skills/{name}/SKILL.md"; $".codex/skills/{name}/SKILL.md" ]))
        @ [ ".fsgg/early-stage-guidance.md" ]
        |> List.sort

    let expectedArtifactCount = List.length expectedArtifactPaths

    type DriftReport =
        { HasProvenance: bool
          ProviderName: string option
          InstalledCliVersion: string
          RequiredMinimumCliVersion: string option
          CliAxis: string
          CliBehindBy: string option
          ExpectedArtifactCount: int
          MissingArtifactPaths: string list
          Steps: ReconciliationStep list
          IsCoherent: bool }

    let private noTargetStep stepId preview : ReconciliationStep =
        { StepId = stepId
          Kind = stepId
          DiffPreview = preview
          Outcome = "noTarget"
          TargetPaths = [] }

    /// Compute the drift picture from already-snapshotted inputs. `descriptor` is the
    /// live provider descriptor resolved from `.fsgg/providers.yml` by the provenance's
    /// provider name; when it disagrees with the provenance-recorded minimum, the live
    /// descriptor value wins (spec Assumption).
    let compute
        (provenance: ScaffoldProvenanceRecord option)
        (descriptor: ProviderDescriptor option)
        (installedVersion: string)
        (presentArtifacts: Set<string>)
        : DriftReport =
        match provenance with
        | None ->
            // Not a scaffolded product (FR-015 / R12): nothing to reconcile, no steps.
            { HasProvenance = false
              ProviderName = None
              InstalledCliVersion = installedVersion
              RequiredMinimumCliVersion = None
              CliAxis = "coherentByAbsence"
              CliBehindBy = None
              ExpectedArtifactCount = expectedArtifactCount
              MissingArtifactPaths = []
              Steps = []
              IsCoherent = true }
        | Some record ->
            // Live descriptor minimum wins over the provenance-recorded one; only a
            // parseable value is treated as a real minimum (else coherent-by-absence).
            let effectiveMinimumRaw =
                (descriptor |> Option.bind (fun d -> d.MinimumCliVersion))
                |> Option.orElseWith (fun () -> record.RequiredMinimumCliVersion)

            let validMinimum =
                effectiveMinimumRaw
                |> Option.bind (fun raw -> Fsgg.Version.tryParse raw |> Option.map (fun _ -> raw))

            let cliAxis, cliBehindBy =
                match validMinimum with
                | None -> "coherentByAbsence", None
                | Some minimum ->
                    match Fsgg.Version.tryParse installedVersion with
                    | None -> "undeterminable", None
                    | Some _ ->
                        match Fsgg.Version.compare installedVersion minimum with
                        | Some -1 -> "behind", Some $"{installedVersion} → {minimum}"
                        | _ -> "atOrAbove", None

            let missing =
                expectedArtifactPaths
                |> List.filter (fun path -> not (Set.contains path presentArtifacts))
                |> List.sort

            let minimumText = Option.defaultValue "" validMinimum

            let cliStep =
                if cliAxis = "behind" then
                    { StepId = "cliSelfUpdate"
                      Kind = "cliSelfUpdate"
                      DiffPreview = $"installed {installedVersion} → target ≥{minimumText}"
                      Outcome = "wouldApply"
                      TargetPaths = [] }
                else
                    noTargetStep "cliSelfUpdate" "no CLI version target"

            // R6: re-pin is value-agnostic and currently a recognized-but-usually-inert
            // step — generic SDD holds no template-version drift signal, so it previews as
            // `noTarget` and embeds no provider/template literal either way.
            let rePinStep = noTargetStep "templateRePin" "no re-pin target"

            let reSeedStep =
                if List.isEmpty missing then
                    noTargetStep "artifactReSeed" "no missing artifacts"
                else
                    { StepId = "artifactReSeed"
                      Kind = "artifactReSeed"
                      DiffPreview = missing |> List.map (fun path -> $"+ {path} (new)") |> String.concat "\n"
                      Outcome = "wouldApply"
                      TargetPaths = missing }

            let steps = [ cliStep; rePinStep; reSeedStep ]

            let hasActionableWork = steps |> List.exists (fun step -> step.Outcome = "wouldApply")

            { HasProvenance = true
              ProviderName = Some record.ProviderName
              InstalledCliVersion = installedVersion
              RequiredMinimumCliVersion = validMinimum
              CliAxis = cliAxis
              CliBehindBy = cliBehindBy
              ExpectedArtifactCount = expectedArtifactCount
              MissingArtifactPaths = missing
              Steps = steps
              IsCoherent = not hasActionableWork }
