namespace FS.GG.SDD.Commands

/// Optional observation of selected working-tree bytes against one Git commit.
/// The selected commit and the observation do not authorize generation.
module internal WorkModelGitDirtySourcePreview =
    type Refusal =
        | CommitRefused of WorkModelGitCommitCustodyPreview.Refusal
        | PhysicalRefused of GenerationSourceSnapshot.Refusal
        | DirtySource of string

    type Observation = WorktreeBytesMatchedCommitObserved of WorkModelGitCommitCustodyPreview.Observation

    let verifyCoreBytesObserved root commitId : Result<Observation, Refusal> =
        match WorkModelGitCommitCustodyPreview.captureCoreConfig root commitId with
        | Error reason -> Error(CommitRefused reason)
        | Ok committed ->
            let rec compare (files: WorkModelGitCommitCustodyPreview.CommitFile list) =
                match files with
                | [] -> Ok(WorktreeBytesMatchedCommitObserved committed)
                | committedFile :: remaining ->
                    match GenerationSourceSnapshot.captureSelectedFile root committedFile.Path with
                    | Error reason -> Error(PhysicalRefused reason)
                    | Ok physicalFile when physicalFile.Bytes <> committedFile.Bytes ->
                        Error(DirtySource committedFile.Path)
                    | Ok _ -> compare remaining
            compare committed.Files
