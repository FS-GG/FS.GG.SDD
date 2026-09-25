namespace FS.GG.SDD.Commands

/// Optional read-only observation of core bytes and Git's executable bit.
module internal WorkModelGitModePreview =
    type Refusal =
        | CommitRefused of WorkModelGitCommitCustodyPreview.Refusal
        | PhysicalRefused of GenerationSourceSnapshot.Refusal
        | DirtySource of string
        | ModeUnavailable of string
        | ModeMismatch of string

    type Observation = CoreBytesAndGitModeMatchedObserved of WorkModelGitCommitCustodyPreview.Observation

    let verifyCoreBytesAndGitModeObserved root commitId : Result<Observation, Refusal> =
        match WorkModelGitCommitCustodyPreview.captureCoreConfig root commitId with
        | Error reason -> Error(CommitRefused reason)
        | Ok committed ->
            let rec compare (files: WorkModelGitCommitCustodyPreview.CommitFile list) =
                match files with
                | [] -> Ok(CoreBytesAndGitModeMatchedObserved committed)
                | committedFile :: remaining ->
                    match GenerationSourceSnapshot.captureSelectedFile root committedFile.Path with
                    | Error reason -> Error(PhysicalRefused reason)
                    | Ok physicalFile when physicalFile.Bytes <> committedFile.Bytes ->
                        Error(DirtySource committedFile.Path)
                    | Ok physicalFile ->
                        match physicalFile.UnixMode with
                        | None -> Error(ModeUnavailable committedFile.Path)
                        | Some mode ->
                            // Git regular-file modes store executable policy, not
                            // each POSIX read/write or special permission bit.
                            let actualExecutable = mode &&& 0o111 <> 0
                            let committedExecutable = committedFile.Mode = "100755"
                            if actualExecutable <> committedExecutable then
                                Error(ModeMismatch committedFile.Path)
                            else compare remaining
            compare committed.Files
