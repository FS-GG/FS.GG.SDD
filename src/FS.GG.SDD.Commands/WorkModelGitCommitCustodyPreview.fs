namespace FS.GG.SDD.Commands

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Security.Cryptography
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
        | PackDirectoryRedirect
        | PackFileRedirect
        | PackFileLimit
        | LooseObjectDirectoryRedirect
        | LooseObjectLeafRedirect
        | LooseObjectLeafLimit
        | UnsupportedPlatform
        | NotCommit
        | MissingPath of string
        | NonRegularPath of string
        | MalformedTreeEntry of string
        | BlobTooLarge of string
        | BlobIdMismatch of string
        | CommitTooLarge
        | CommitIdMismatch
        | TreeTooLarge of string
        | TreeIdMismatch of string
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
    let private maxCommitBytes = 1024 * 1024
    let private maxTreeBytes = 1024 * 1024

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
        expected

    let private requireDirectPackDirectory root objects =
        let expected = Path.Combine(objects, "pack")
        let reported =
            gitAbsolutePath root [ "rev-parse"; "--path-format=absolute"; "--git-path"; "objects/pack" ]
                            PackDirectoryRedirect
        if not (String.Equals(reported, expected, StringComparison.Ordinal)) then
            refuse PackDirectoryRedirect
        let buffer = Marshal.AllocHGlobal 256
        try
            // A missing pack directory is allowed for a loose-only store. If
            // present, inspect its final entry without following a symlink.
            if statx(-100, expected, 0x100, 1u, buffer) <> 0 then
                if Marshal.GetLastPInvokeError() <> 2 then refuse PackDirectoryRedirect
            else
                let mode = Marshal.ReadInt16(buffer, 28) |> uint16 |> int
                if mode &&& 0xf000 <> 0x4000 then refuse PackDirectoryRedirect
                // A direct pack directory can still borrow another store via
                // symlinked .idx/.pack leaves. Inspect every present child and
                // cap the roster; Git may read any of these files later.
                try
                    let mutable leafCount = 0
                    for leaf in Directory.EnumerateFileSystemEntries expected do
                        leafCount <- leafCount + 1
                        if leafCount > 4096 then refuse PackFileLimit
                        if statx(-100, leaf, 0x100, 1u, buffer) <> 0 then
                            refuse PackFileRedirect
                        let leafMode = Marshal.ReadInt16(buffer, 28) |> uint16 |> int
                        if leafMode &&& 0xf000 <> 0x8000 then
                            refuse PackFileRedirect
                with
                | :? IOException
                | :? UnauthorizedAccessException -> refuse PackFileRedirect
        finally Marshal.FreeHGlobal buffer

    let private requireDirectLooseFanouts objects =
        // Git loose-object paths use exactly two lowercase hex digits for the
        // first directory level. Scan the fixed 256-name set, including names
        // absent at this instant, instead of trusting a caller inventory.
        // Bound the total leaf roster retained/inspected by this preview.
        let buffer = Marshal.AllocHGlobal 256
        try
            let mutable leafCount = 0
            for index in 0 .. 255 do
                let prefix = index.ToString("x2", Globalization.CultureInfo.InvariantCulture)
                let path = Path.Combine(objects, prefix)
                if statx(-100, path, 0x100, 1u, buffer) <> 0 then
                    if Marshal.GetLastPInvokeError() <> 2 then
                        refuse LooseObjectDirectoryRedirect
                else
                    let mode = Marshal.ReadInt16(buffer, 28) |> uint16 |> int
                    if mode &&& 0xf000 <> 0x4000 then
                        refuse LooseObjectDirectoryRedirect
                    try
                        for leaf in Directory.EnumerateFileSystemEntries path do
                            leafCount <- leafCount + 1
                            if leafCount > 4096 then refuse LooseObjectLeafLimit
                            if statx(-100, leaf, 0x100, 1u, buffer) <> 0 then
                                refuse LooseObjectLeafRedirect
                            let leafMode = Marshal.ReadInt16(buffer, 28) |> uint16 |> int
                            if leafMode &&& 0xf000 <> 0x8000 then
                                refuse LooseObjectLeafRedirect
                    with
                    | :? IOException
                    | :? UnauthorizedAccessException -> refuse LooseObjectLeafRedirect
        finally Marshal.FreeHGlobal buffer

    let private requireObjectDigest kind (expectedId: string) (bytes: byte[]) refusal =
        // Git cat-file can return valid foreign content placed under an
        // existing loose-object filename. Hash the actual returned payload,
        // including Git's object header, without copying the payload again.
        let algorithm = if expectedId.Length = 40 then HashAlgorithmName.SHA1 else HashAlgorithmName.SHA256
        use hash = IncrementalHash.CreateHash algorithm
        hash.AppendData(Encoding.ASCII.GetBytes($"{kind} {bytes.Length}\u0000"))
        hash.AppendData bytes
        let actualId = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
        if not (String.Equals(actualId, expectedId, StringComparison.Ordinal)) then
            refuse refusal

    let private requireCommitDigest root commitId =
        // This is a deliberately bounded commit-body observation. Subsequent
        // tree-object reads remain separate, not handle-pinned to this read.
        let bytes = runGit root [ "cat-file"; "commit"; commitId ] maxCommitBytes CommitTooLarge
        requireObjectDigest "commit" commitId bytes CommitIdMismatch
        bytes

    let private commitRootTreeId (commitId: string) (commitBytes: byte[]) =
        let newline = Array.IndexOf(commitBytes, 10uy)
        if newline < 6 || ascii commitBytes.[0..4] <> "tree " then
            refuse (MalformedTreeEntry "<root>")
        let treeId = ascii commitBytes.[5..newline - 1]
        if treeId.Length <> commitId.Length || not (fullObjectId treeId) then
            refuse (MalformedTreeEntry "<root>")
        treeId

    let private readTree root path treeId =
        let bytes = runGit root [ "cat-file"; "tree"; treeId ] maxTreeBytes (TreeTooLarge path)
        requireObjectDigest "tree" treeId bytes (TreeIdMismatch path)
        bytes

    type private TreeEntry = { Mode: string; Id: string }

    let private selectedTreeEntry (treeBytes: byte[]) idLength path (name: string) =
        // Git tree records are "mode name\0<raw object ID>". Parse bounded,
        // verified bytes in memory so pathname-based ls-tree cannot reselect
        // a different child after the digest check.
        let target = Encoding.UTF8.GetBytes name
        let idBytes = idLength / 2
        let mutable offset = 0
        let mutable selected = None
        while offset < treeBytes.Length do
            let space = Array.IndexOf(treeBytes, 32uy, offset)
            if space < 0 || space - offset < 5 || space - offset > 6 then
                refuse (MalformedTreeEntry path)
            let nul = Array.IndexOf(treeBytes, 0uy, space + 1)
            if nul < 0 || nul = space + 1 || nul + 1 + idBytes > treeBytes.Length then
                refuse (MalformedTreeEntry path)
            let mode = Encoding.ASCII.GetString(treeBytes, offset, space - offset)
            if mode |> Seq.exists (fun c -> c < '0' || c > '7') then
                refuse (MalformedTreeEntry path)
            let nameLength = nul - space - 1
            let mutable matches = nameLength = target.Length
            let mutable index = 0
            while matches && index < target.Length do
                if treeBytes.[space + 1 + index] <> target.[index] then matches <- false
                index <- index + 1
            if matches then
                if selected.IsSome then refuse (MalformedTreeEntry path)
                let oid =
                    Convert.ToHexString(treeBytes, nul + 1, idBytes).ToLowerInvariant()
                selected <- Some { Mode = mode; Id = oid }
            offset <- nul + 1 + idBytes
        selected

    let private readBlob (root: string) (path: string) (blobId: string) =
        let sizeText = runGit root [ "cat-file"; "-s"; blobId ] 64 (BlobTooLarge path) |> ascii
        let mutable size = 0L
        if not (Int64.TryParse(sizeText.Trim(), &size)) || size < 0L then refuse GitFailure
        if size > int64 maxBlobBytes then refuse (BlobTooLarge path)
        let bytes = runGit root [ "cat-file"; "blob"; blobId ] maxBlobBytes (BlobTooLarge path)
        if int64 bytes.Length <> size then refuse GitFailure
        requireObjectDigest "blob" blobId bytes (BlobIdMismatch path)
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
            let objects = requireDirectObjectDirectory root
            requireDirectPackDirectory root objects
            requireDirectLooseFanouts objects
            afterRegistration ()
            let kind = runGit root [ "cat-file"; "-t"; commitId ] 32 GitFailure |> ascii
            if kind.Trim() <> "commit" then refuse NotCommit
            let commitBytes = requireCommitDigest root commitId
            let rootTreeId = commitRootTreeId commitId commitBytes
            let rootTree = readTree root "<root>" rootTreeId
            let child =
                selectedTreeEntry rootTree commitId.Length ".fsgg" ".fsgg"
                |> Option.defaultWith (fun () -> refuse (MissingPath ".fsgg/project.yml"))
            if child.Mode <> "40000" then refuse (NonRegularPath ".fsgg/project.yml")
            let configTree = readTree root ".fsgg" child.Id
            let files =
                [ ".fsgg/project.yml"; ".fsgg/sdd.yml" ]
                |> List.map (fun path ->
                    let name = path.Substring(".fsgg/".Length)
                    let entry =
                        selectedTreeEntry configTree commitId.Length path name
                        |> Option.defaultWith (fun () -> refuse (MissingPath path))
                    if entry.Mode <> "100644" && entry.Mode <> "100755" then
                        refuse (NonRegularPath path)
                    CommitFile(path, entry.Mode, entry.Id, readBlob root path entry.Id))
            beforeFinalCheck ()
            // Recheck after the separate Git object reads. A persistent .git
            // switch to an unregistered repository must not return an observed
            // match. A switch restored before this check remains ABA.
            try
                requireRepositoryRoot root
                requireRegisteredWorktree root
            with Refused _ -> refuse RepositoryChanged
            requireNoAlternates root
            let objects = requireDirectObjectDirectory root
            requireDirectPackDirectory root objects
            requireDirectLooseFanouts objects
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
