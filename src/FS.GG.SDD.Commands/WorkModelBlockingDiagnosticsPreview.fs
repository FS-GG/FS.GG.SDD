namespace FS.GG.SDD.Commands

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// Read-only mirror of the producer's blocking-model-diagnostics gate, before
/// the assembly-bound exact-output and repeated physical-source preview.
module internal WorkModelBlockingDiagnosticsPreview =
    type Refusal =
        | GenerationFailed
        | BlockingDiagnostics of string list
        | Source of WorkModelCurrentGeneratorPreview.Refusal

    let verifyWithCapture
        (capture: unit -> Result<GenerationSourceSnapshot.CapturedFile list, WorkModelSourceBundle.Refusal>)
        workId outputPath outputJson (selected: FileSnapshot list)
        (candidate: WorkModelSourceBundle.Candidate) =
        let generated =
            try
                Serialization.generateWorkModel
                    { WorkId = workId
                      Snapshots = selected
                      GeneratorVersion = SchemaVersion.currentGeneratorVersion ()
                      ExpectedOutputPath = Some outputPath }
                |> Ok
            with _ -> Error GenerationFailed
        match generated with
        | Error reason -> Error reason
        | Ok generated ->
            let blockingIds =
                WorkModel.blockingDiagnostics generated.Model
                |> List.map _.Id
                |> List.distinct
                |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
            if not (List.isEmpty blockingIds) then Error(BlockingDiagnostics blockingIds)
            else
                WorkModelCurrentGeneratorPreview.verifyWithCapture
                    capture workId outputPath outputJson selected candidate
                |> Result.mapError Source

    let verifyPhysical workspaceRoot workId outputPath outputJson selected candidate =
        verifyWithCapture
            (fun () -> WorkModelSourceBundle.verifyFromPinnedCoreSources
                           workspaceRoot workId selected candidate)
            workId outputPath outputJson selected candidate
