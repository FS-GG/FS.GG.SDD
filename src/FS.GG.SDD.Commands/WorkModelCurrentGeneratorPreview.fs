namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// Read-only work-model preview whose generator identity comes from the referenced
/// Artifacts assembly instead of the same caller that supplies the proposed JSON.
module internal WorkModelCurrentGeneratorPreview =
    type Refusal = Joint of WorkModelJointPreview.Refusal

    let verifyWithCapture
        (capture: unit -> Result<GenerationSourceSnapshot.CapturedFile list, WorkModelSourceBundle.Refusal>)
        workId outputPath outputJson (selected: FileSnapshot list)
        (candidate: WorkModelSourceBundle.Candidate) =
        WorkModelJointPreview.verifyWithCapture
            capture workId outputPath outputJson selected candidate
            (SchemaVersion.currentGeneratorVersion ())
        |> Result.mapError Joint

    let verifyPhysical workspaceRoot workId outputPath outputJson selected candidate =
        verifyWithCapture
            (fun () -> WorkModelSourceBundle.verifyFromPinnedCoreSources
                           workspaceRoot workId selected candidate)
            workId outputPath outputJson selected candidate
