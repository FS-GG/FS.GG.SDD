namespace FS.GG.SDD.Commands

open System
open System.Diagnostics
open System.IO
open System.Text
open FS.GG.SDD.Artifacts

/// Optional read-only source profile: two core config blobs from one immutable
/// Git commit object. A caller-selected commit is not producer authority.
module internal WorkModelGitCommitCustodyPreview =
    type Refusal =
        | InvalidCommitId
        | InvalidRepository
        | NotRepositoryRoot
        | NotCommit
        | MissingPath of string
        | NonRegularPath of string
        | MalformedTreeEntry of string
        | BlobTooLarge of string
        | GitFailure

    type CommitFile internal (path: string, mode: string, blobId: string, raw: byte[]) =
        let bytes = Array.copy raw
        member _.Path = path
        member _.Mode = mode
        member _.BlobId = blobId
        member _.Bytes = Array.copy bytes
        member _.RawSha256 = SchemaVersion.sha256Bytes bytes

    type Observation = { CommitId: string; Files: CommitFile list }

    exception private Refused of Refusal
    let private refuse reason = raise (Refused reason)
    let private maxBlobBytes = 32 * 1024 * 1024

    let private fullObjectId (value: string) =
        not (String.IsNullOrEmpty value)
        && (value.Length = 40 || value.Length = 64)
        && value |> Seq.forall (fun c -> c >= '0' && c <= '9' || c >= 'a' && c <= 'f')

    let private runGit (root: string) (args: string list) (limit: int) (tooLarge: Refusal) : byte[] =
        let start = ProcessStartInfo("git")
        start.WorkingDirectory <- root
        start.UseShellExecute <- false
        start.CreateNoWindow <- true
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        // Repository selection must come from WorkingDirectory, not ambient
        // Git overrides inherited from a parent build or shell.
        [ "GIT_DIR"; "GIT_WORK_TREE"; "GIT_COMMON_DIR"; "GIT_OBJECT_DIRECTORY"
          "GIT_ALTERNATE_OBJECT_DIRECTORIES"; "GIT_NAMESPACE"; "GIT_INDEX_FILE" ]
        |> List.iter (fun key -> start.Environment.Remove key |> ignore)
        start.Environment.["GIT_OPTIONAL_LOCKS"] <- "0"
        // Git replace refs can otherwise make a full object ID resolve to the
        // replacement commit's tree. Inspect the named object itself.
        start.Environment.["GIT_NO_REPLACE_OBJECTS"] <- "1"
        args |> List.iter start.ArgumentList.Add
        use proc =
            Process.Start start
            |> Option.ofObj
            |> Option.defaultWith (fun () -> refuse GitFailure)
        let stderr = proc.StandardError.ReadToEndAsync()
        use output = new MemoryStream()
        let buffer = Array.zeroCreate<byte> 81920
        let mutable count = proc.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)
        while count > 0 do
            if output.Length + int64 count > int64 limit then
                try proc.Kill(true) with _ -> ()
                refuse tooLarge
            output.Write(buffer, 0, count)
            count <- proc.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)
        if not (proc.WaitForExit 30000) then
            try proc.Kill(true) with _ -> ()
            refuse GitFailure
        stderr.GetAwaiter().GetResult() |> ignore
        if proc.ExitCode <> 0 then refuse GitFailure
        output.ToArray()

    let private ascii (bytes: byte[]) = Encoding.ASCII.GetString bytes

    let private requireRepositoryRoot root =
        // Git otherwise walks to an enclosing repository. The caller's
        // physical source root must be that repository's worktree root.
        // A linked worktree (.git file) is valid; a nested copied source is not.
        let location =
            runGit root [ "rev-parse"; "--is-inside-work-tree"; "--show-prefix" ]
                   4096 NotRepositoryRoot
        if location <> Encoding.ASCII.GetBytes "true\n\n" then
            refuse NotRepositoryRoot

    let private commitEntry (root: string) (commitId: string) (path: string) =
        let output = runGit root [ "ls-tree"; "-z"; "--full-tree"; commitId; "--"; path ] 4096 (MalformedTreeEntry path)
        if output.Length = 0 then refuse (MissingPath path)
        let records = ascii output |> fun text -> text.Split('\u0000', StringSplitOptions.RemoveEmptyEntries)
        if records.Length <> 1 then refuse (MalformedTreeEntry path)
        let tab = records.[0].IndexOf '\t'
        if tab <= 0 || records.[0].Substring(tab + 1) <> path then
            refuse (MalformedTreeEntry path)
        let fields = records.[0].Substring(0, tab).Split(' ', StringSplitOptions.RemoveEmptyEntries)
        if fields.Length <> 3 || fields.[1] <> "blob" || not (fullObjectId fields.[2]) then
            refuse (MalformedTreeEntry path)
        if fields.[0] <> "100644" && fields.[0] <> "100755" then
            refuse (NonRegularPath path)
        fields.[0], fields.[2]

    let private readBlob (root: string) (path: string) (blobId: string) =
        let sizeText = runGit root [ "cat-file"; "-s"; blobId ] 64 (BlobTooLarge path) |> ascii
        let mutable size = 0L
        if not (Int64.TryParse(sizeText.Trim(), &size)) || size < 0L then refuse GitFailure
        if size > int64 maxBlobBytes then refuse (BlobTooLarge path)
        let bytes = runGit root [ "cat-file"; "blob"; blobId ] maxBlobBytes (BlobTooLarge path)
        if int64 bytes.Length <> size then refuse GitFailure
        bytes

    /// The commit ID is a full object ID, never a moving ref. Bytes come from
    /// Git blob objects, not the working tree; source-selection policy is separate.
    let captureCoreConfig root (commitId: string) : Result<Observation, Refusal> =
        try
            if String.IsNullOrWhiteSpace root || not (Directory.Exists root) then refuse InvalidRepository
            if not (fullObjectId commitId) then refuse InvalidCommitId
            requireRepositoryRoot root
            let kind = runGit root [ "cat-file"; "-t"; commitId ] 32 GitFailure |> ascii
            if kind.Trim() <> "commit" then refuse NotCommit
            let files =
                [ ".fsgg/project.yml"; ".fsgg/sdd.yml" ]
                |> List.map (fun path ->
                    let mode, blobId = commitEntry root commitId path
                    CommitFile(path, mode, blobId, readBlob root path blobId))
            Ok { CommitId = commitId; Files = files }
        with
        | Refused reason -> Error reason
        | :? System.ComponentModel.Win32Exception
        | :? IOException
        | :? InvalidOperationException -> Error GitFailure
