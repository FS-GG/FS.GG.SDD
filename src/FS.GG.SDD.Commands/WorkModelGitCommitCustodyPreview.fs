namespace FS.GG.SDD.Commands

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Text
open FS.GG.SDD.Artifacts

/// Optional read-only source profile: two core config blobs from one immutable
/// Git commit object. A caller-selected commit is not producer authority.
module internal WorkModelGitCommitCustodyPreview =
    type Refusal =
        | InvalidCommitId
        | InvalidRepository
        | NotRepositoryRoot
        | UnregisteredWorktree
        | RepositoryChanged
        | AlternateObjectStore
        | ObjectDirectoryRedirect
        | UnsupportedPlatform
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

    [<DllImport("libc", SetLastError = true, EntryPoint = "statx")>]
    extern int private statx(int directory, string path, int flags, uint32 mask, nativeint buffer)

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
        // A promisor clone may fetch missing blobs during cat-file, mutating
        // its object store (and potentially contacting a remote). Missing
        // local objects must refuse this read-only preview instead.
        start.Environment.["GIT_NO_LAZY_FETCH"] <- "1"
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

    let private requireRegisteredWorktree root =
        // A copied source can point .git at another repository (by symlink or
        // gitfile) and still report itself as Git's top level. Require the
        // repository to register this exact worktree path. -z makes paths with
        // newlines unambiguous; this remains a point-in-time observation.
        let output =
            runGit root [ "worktree"; "list"; "--porcelain"; "-z" ]
                   (1024 * 1024) UnregisteredWorktree
        let roster =
            try UTF8Encoding(false, true).GetString output
            with :? DecoderFallbackException -> refuse UnregisteredWorktree
        let records = roster.Split("\u0000\u0000", StringSplitOptions.RemoveEmptyEntries)
        let expected = "worktree " + Path.TrimEndingDirectorySeparator(Path.GetFullPath root)
        let selected =
            records
            |> Array.filter (fun record ->
                let first = record.Split('\u0000').[0]
                if not (first.StartsWith("worktree ", StringComparison.Ordinal)) then
                    refuse UnregisteredWorktree
                String.Equals(first, expected, StringComparison.Ordinal))
        if selected.Length <> 1 then refuse UnregisteredWorktree

    let private gitAbsolutePath root args refusal =
        let output =
            runGit root args 4096 refusal
        let path =
            try UTF8Encoding(false, true).GetString output
            with :? DecoderFallbackException -> refuse refusal
        if not (path.EndsWith("\n", StringComparison.Ordinal)) then
            refuse refusal
        let path = path.Substring(0, path.Length - 1)
        if String.IsNullOrWhiteSpace path || not (Path.IsPathFullyQualified path) then
            refuse refusal
        Path.TrimEndingDirectorySeparator path

    let private requireNoAlternates root =
        // Refuse an alternates file as one prerequisite to object-store
        // custody; this check does not prove the store is self-contained.
        // Git follows objects/info/alternates even after ambient alternate
        // variables are cleared. Ask Git for the active common-store path so
        // registered linked worktrees use the same check as the main worktree.
        let path =
            gitAbsolutePath root
                [ "rev-parse"; "--path-format=absolute"; "--git-path"; "objects/info/alternates" ]
                AlternateObjectStore
        try
            File.GetAttributes path |> ignore
            // Even an empty or malformed alternate file is outside this
            // provisional no-alternates source profile.
            refuse AlternateObjectStore
        with
        | :? FileNotFoundException
        | :? DirectoryNotFoundException -> ()
        | :? IOException
        | :? UnauthorizedAccessException -> refuse AlternateObjectStore

    let private requireDirectObjectDirectory root =
        if not (OperatingSystem.IsLinux()) then refuse UnsupportedPlatform
        let common =
            gitAbsolutePath root [ "rev-parse"; "--path-format=absolute"; "--git-common-dir" ]
                            ObjectDirectoryRedirect
        let reported =
            gitAbsolutePath root [ "rev-parse"; "--path-format=absolute"; "--git-path"; "objects" ]
                            ObjectDirectoryRedirect
        let expected = Path.Combine(common, "objects")
        if not (String.Equals(reported, expected, StringComparison.Ordinal)) then
            refuse ObjectDirectoryRedirect
        let buffer = Marshal.AllocHGlobal 256
        try
            // AT_SYMLINK_NOFOLLOW and STATX_TYPE inspect the final object-store
            // directory entry itself. The Git path checks above are separate
            // subprocess observations; this does not pin later object reads.
            if statx(-100, expected, 0x100, 1u, buffer) <> 0 then
                refuse ObjectDirectoryRedirect
            let mode = Marshal.ReadInt16(buffer, 28) |> uint16 |> int
            if mode &&& 0xf000 <> 0x4000 then refuse ObjectDirectoryRedirect
        finally Marshal.FreeHGlobal buffer

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
    // Hooks are a disposable race test seam; production supplies no actions.
    let captureCoreConfigWithHooks afterRegistration beforeFinalCheck root (commitId: string)
        : Result<Observation, Refusal> =
        try
            if String.IsNullOrWhiteSpace root || not (Directory.Exists root) then refuse InvalidRepository
            if not (fullObjectId commitId) then refuse InvalidCommitId
            requireRepositoryRoot root
            requireRegisteredWorktree root
            requireNoAlternates root
            requireDirectObjectDirectory root
            afterRegistration ()
            let kind = runGit root [ "cat-file"; "-t"; commitId ] 32 GitFailure |> ascii
            if kind.Trim() <> "commit" then refuse NotCommit
            let files =
                [ ".fsgg/project.yml"; ".fsgg/sdd.yml" ]
                |> List.map (fun path ->
                    let mode, blobId = commitEntry root commitId path
                    CommitFile(path, mode, blobId, readBlob root path blobId))
            beforeFinalCheck ()
            // Recheck after the separate Git object reads. A persistent .git
            // switch to an unregistered repository must not return an observed
            // match. A switch restored before this check remains ABA.
            try
                requireRepositoryRoot root
                requireRegisteredWorktree root
            with Refused _ -> refuse RepositoryChanged
            requireNoAlternates root
            requireDirectObjectDirectory root
            Ok { CommitId = commitId; Files = files }
        with
        | Refused reason -> Error reason
        | :? System.ComponentModel.Win32Exception
        | :? DllNotFoundException
        | :? EntryPointNotFoundException
        | :? IOException
        | :? InvalidOperationException -> Error GitFailure

    let captureCoreConfig root commitId =
        captureCoreConfigWithHooks ignore ignore root commitId
