namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open Microsoft.Win32.SafeHandles
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

/// Provisional physical read seam for a caller-declared closed generation source root.
module internal GenerationSourceSnapshot =
    type DigestPolicy = ExactBytes | Utf8LfText

    type Refusal =
        | InvalidRoot
        | InvalidPath of string
        | DuplicatePath of string
        | MissingFile of string
        | UnexpectedFile of string
        | Symlink of string
        | NonRegular of string
        | Unreadable of string
        | EmptySet
        | UnsupportedPlatform

    type CapturedFile internal (path: string, raw: byte[], digest: SourceDigest) =
        let snapshot = Array.copy raw
        member _.Path = path
        member _.Bytes = Array.copy snapshot
        member _.Digest = digest

    type private EntryKind = Regular | Directory | Link | Special
    exception private CaptureRefused of Refusal

    [<DllImport("libc", SetLastError = true, EntryPoint = "statx")>]
    extern int statx(int directory, string path, int flags, uint32 mask, nativeint buffer)

    [<DllImport("libc", SetLastError = true, EntryPoint = "open")>]
    extern int private nativeOpen(string path, int flags)

    [<DllImport("libc", SetLastError = true, EntryPoint = "openat")>]
    extern int private nativeOpenAt(int directory, string path, int flags)

    [<DllImport("libc", SetLastError = true, EntryPoint = "close")>]
    extern int private nativeClose(int handle)

    // Linux open flags. O_NONBLOCK prevents a FIFO swapped into the final slot
    // from hanging the reader before the descriptor's type can be checked.
    let private directoryFlags = 0x10000 ||| 0x20000 ||| 0x80000
    let private fileFlags = 0x20000 ||| 0x80000 ||| 0x800

    let private refuse issue = raise (CaptureRefused issue)

    let private validRelative (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && not (Path.IsPathRooted value)
        && not (value.Contains '\\')
        && value.Split('/') |> Array.forall (fun segment -> segment <> "" && segment <> "." && segment <> "..")
        && value |> Seq.forall (fun c -> not (Char.IsControl c))

    let private classify (absolute: string) (relative: string) =
        if OperatingSystem.IsLinux() then
            let buffer = Marshal.AllocHGlobal 256
            try
                // AT_SYMLINK_NOFOLLOW and STATX_TYPE. The statx mode field has a stable offset
                // in Linux's versioned statx layout; unlike FileAttributes this distinguishes FIFO.
                if statx(-100, absolute, 0x100, 1u, buffer) <> 0 then refuse (Unreadable relative)
                match (int (uint16 (Marshal.ReadInt16(buffer, 28)))) &&& 0xf000 with
                | 0x8000 -> Regular
                | 0x4000 -> Directory
                | 0xa000 -> Link
                | _ -> Special
            finally Marshal.FreeHGlobal buffer
        elif OperatingSystem.IsWindows() then
            let attributes = File.GetAttributes absolute
            if attributes.HasFlag FileAttributes.ReparsePoint then Link
            elif attributes.HasFlag FileAttributes.Directory then Directory
            elif File.Exists absolute then Regular
            else Special
        else refuse UnsupportedPlatform

    let private digest policy raw relative =
        match policy with
        | ExactBytes -> SchemaVersion.sha256Bytes raw
        | Utf8LfText ->
            try
                let utf8 = UTF8Encoding(false, true)
                let text = utf8.GetString raw
                let body = if text.StartsWith("\uFEFF", StringComparison.Ordinal) then text.Substring 1 else text
                SchemaVersion.sha256Text body
            with :? DecoderFallbackException -> refuse (Unreadable relative)

    let private kindFromMode mode =
        match mode &&& 0xf000 with
        | 0x8000 -> Regular
        | 0x4000 -> Directory
        | 0xa000 -> Link
        | _ -> Special

    let private descriptorKind directory path flags relative =
        let buffer = Marshal.AllocHGlobal 256
        try
            if statx(directory, path, flags, 1u, buffer) <> 0 then refuse (Unreadable relative)
            Marshal.ReadInt16(buffer, 28) |> uint16 |> int |> kindFromMode
        finally Marshal.FreeHGlobal buffer

    let private withDirectory parent name relative (action: int -> 'a) =
        match descriptorKind parent name 0x100 relative with
        | Link -> refuse (Symlink relative)
        | Directory -> ()
        | _ -> refuse InvalidRoot
        let handle = nativeOpenAt(parent, name, directoryFlags)
        if handle < 0 then refuse (Unreadable relative)
        try
            // Classification and opening can race. The opened descriptor, rather
            // than the earlier path probe, decides whether this is a directory.
            if descriptorKind handle "" 0x1000 relative <> Directory then refuse InvalidRoot
            action handle
        finally nativeClose handle |> ignore

    /// Linux capture pins every directory from / through the closed root, then
    /// opens each regular file relative to its pinned parent. The hooks are only
    /// test seams, surrounding the file open to exercise both sides of the race.
    let capturePinnedWithHooks (beforeOpen: string -> unit) (afterOpen: string -> unit)
                              (workspaceRoot: string) (closedRoot: string) (declared: string list)
                              (policy: DigestPolicy) : Result<CapturedFile list, Refusal> =
        try
            if not (OperatingSystem.IsLinux()) then refuse UnsupportedPlatform
            if not (validRelative closedRoot) || String.IsNullOrWhiteSpace workspaceRoot
               || not (Directory.Exists workspaceRoot) then refuse InvalidRoot
            if obj.ReferenceEquals(declared, null) || List.isEmpty declared then refuse EmptySet
            let prefix = closedRoot + "/"
            let expected = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            for path in declared do
                if not (validRelative path)
                   || not (path.StartsWith(prefix, StringComparison.Ordinal))
                   || path.Length = prefix.Length then refuse (InvalidPath path)
                if not (expected.Add path) then refuse (DuplicatePath path)

            let workspace = Path.GetFullPath workspaceRoot
            let rootHandle = nativeOpen("/", directoryFlags)
            if rootHandle < 0 then refuse InvalidRoot
            try
                let rec descend parent segments relative action =
                    match segments with
                    | [] -> action parent
                    | segment :: tail ->
                        let child = if relative = "" then segment else relative + "/" + segment
                        let display = Path.GetRelativePath(workspace, "/" + child).Replace('\\', '/')
                        withDirectory parent segment display (fun handle -> descend handle tail child action)

                let segments (value: string) =
                    value.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.toList
                let workspaceSegments = segments workspace
                descend rootHandle workspaceSegments "" (fun workspaceHandle ->
                    let rec sourceRoot parent segments relative action =
                        match segments with
                        | [] -> action parent
                        | segment :: tail ->
                            let child = if relative = "" then segment else relative + "/" + segment
                            withDirectory parent segment child (fun handle -> sourceRoot handle tail child action)

                    sourceRoot workspaceHandle (segments closedRoot) "" (fun sourceHandle ->
                        let entries = HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        let files = ResizeArray<string * byte[]>()
                        let rec walk directory relative =
                            // procfs exposes the already-open directory. Entry names
                            // are subsequently resolved only through openat(directory).
                            for entry in Directory.EnumerateFileSystemEntries($"/proc/self/fd/%d{directory}") |> Seq.sort do
                                let name =
                                    Path.GetFileName entry
                                    |> Option.ofObj
                                    |> Option.defaultWith (fun () -> refuse (InvalidPath entry))
                                let path = relative + "/" + name
                                if not (validRelative path) then refuse (InvalidPath path)
                                if not (entries.Add path) then refuse (DuplicatePath path)
                                match descriptorKind directory name 0x100 path with
                                | Link -> refuse (Symlink path)
                                | Special -> refuse (NonRegular path)
                                | Directory ->
                                    if expected.Contains path then refuse (NonRegular path)
                                    withDirectory directory name path (fun child -> walk child path)
                                | Regular ->
                                    beforeOpen path
                                    let handle = nativeOpenAt(directory, name, fileFlags)
                                    if handle < 0 then refuse (Unreadable path)
                                    use safeHandle = new SafeFileHandle(nativeint handle, true)
                                    if descriptorKind handle "" 0x1000 path <> Regular then refuse (NonRegular path)
                                    afterOpen path
                                    use stream = new FileStream(safeHandle, FileAccess.Read)
                                    use output = new MemoryStream()
                                    stream.CopyTo output
                                    files.Add(path, output.ToArray())
                        walk sourceHandle closedRoot
                        let actual = HashSet<string>(files |> Seq.map fst, StringComparer.Ordinal)
                        match declared |> List.tryFind (fun path -> not (actual.Contains path)) with
                        | Some path -> refuse (MissingFile path)
                        | None -> ()
                        match files |> Seq.tryFind (fun (path, _) -> not (expected.Contains path)) with
                        | Some (path, _) -> refuse (UnexpectedFile path)
                        | None -> ()
                        files
                        |> Seq.sortBy fst
                        |> Seq.map (fun (path, raw) -> CapturedFile(path, raw, digest policy raw path))
                        |> Seq.toList))
                |> Ok
            finally nativeClose rootHandle |> ignore
        with
        | CaptureRefused issue -> Error issue
        | :? DllNotFoundException
        | :? EntryPointNotFoundException -> Error UnsupportedPlatform
        | :? IOException
        | :? UnauthorizedAccessException -> Error(Unreadable closedRoot)

    /// Test seam for a controlled interleaving between path classification and byte read.
    /// The Linux production path uses descriptor-pinned reads; this path-based
    /// reader remains a characterization control and the Windows provisional path.
    let captureWithReader (readBytes: string -> byte array)
                          (workspaceRoot: string) (closedRoot: string) (declared: string list)
                          (policy: DigestPolicy) : Result<CapturedFile list, Refusal> =
        try
            if not (validRelative closedRoot) || String.IsNullOrWhiteSpace workspaceRoot
               || not (Directory.Exists workspaceRoot) then refuse InvalidRoot
            if obj.ReferenceEquals(declared, null) || List.isEmpty declared then refuse EmptySet
            let prefix = closedRoot + "/"
            let expected = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            for path in declared do
                if not (validRelative path)
                   || not (path.StartsWith(prefix, StringComparison.Ordinal))
                   || path.Length = prefix.Length then refuse (InvalidPath path)
                if not (expected.Add path) then refuse (DuplicatePath path)

            let workspace = Path.GetFullPath workspaceRoot
            // A lexical workspace path can pass through a linked ancestor even
            // when its final directory is not itself a link. Probe every parent
            // before treating the supplied root as a physical source boundary.
            let mutable ancestor = Some workspace
            while ancestor.IsSome do
                let path = ancestor.Value
                let relative = Path.GetRelativePath(workspace, path).Replace('\\', '/')
                match classify path relative with
                | Directory -> ()
                | Link -> refuse (Symlink relative)
                | _ -> refuse InvalidRoot
                ancestor <- Directory.GetParent(path) |> Option.ofObj |> Option.map _.FullName
            let mutable absoluteRoot = workspace
            let mutable relativeRoot = ""
            for segment in closedRoot.Split('/') do
                absoluteRoot <- Path.Combine(absoluteRoot, segment)
                relativeRoot <- if relativeRoot = "" then segment else relativeRoot + "/" + segment
                match classify absoluteRoot relativeRoot with
                | Directory -> ()
                | Link -> refuse (Symlink relativeRoot)
                | _ -> refuse InvalidRoot

            let entries = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            let files = ResizeArray<string * string>()
            let rec walk absolute relative =
                for entry in Directory.EnumerateFileSystemEntries absolute |> Seq.sort do
                    let name =
                        Path.GetFileName entry
                        |> Option.ofObj
                        |> Option.defaultWith (fun () -> refuse (InvalidPath entry))
                    let path = relative + "/" + name
                    if not (validRelative path) then refuse (InvalidPath path)
                    if not (entries.Add path) then refuse (DuplicatePath path)
                    match classify entry path with
                    | Link -> refuse (Symlink path)
                    | Special -> refuse (NonRegular path)
                    | Directory ->
                        if expected.Contains path then refuse (NonRegular path)
                        walk entry path
                    | Regular -> files.Add(path, entry)
            walk absoluteRoot closedRoot
            let actual = HashSet<string>(files |> Seq.map fst, StringComparer.Ordinal)
            match declared |> List.tryFind (fun path -> not (actual.Contains path)) with
            | Some path -> refuse (MissingFile path)
            | None -> ()
            match files |> Seq.tryFind (fun (path, _) -> not (expected.Contains path)) with
            | Some (path, _) -> refuse (UnexpectedFile path)
            | None -> ()
            files
            |> Seq.sortBy fst
            |> Seq.map (fun (path, absolute) ->
                if classify absolute path <> Regular then refuse (NonRegular path)
                let raw = readBytes absolute
                if classify absolute path <> Regular then refuse (NonRegular path)
                CapturedFile(path, raw, digest policy raw path))
            |> Seq.toList
            |> Ok
        with
        | CaptureRefused issue -> Error issue
        | :? DllNotFoundException
        | :? EntryPointNotFoundException -> Error UnsupportedPlatform
        | :? IOException
        | :? UnauthorizedAccessException -> Error(Unreadable closedRoot)

    /// The caller owns the closed-root selection and digest policy. This is a read-only capture:
    /// it never stages or replaces a generated output.
    let capture (workspaceRoot: string) (closedRoot: string) (declared: string list)
                (policy: DigestPolicy) : Result<CapturedFile list, Refusal> =
        if OperatingSystem.IsLinux() then
            capturePinnedWithHooks ignore ignore workspaceRoot closedRoot declared policy
        else
            captureWithReader File.ReadAllBytes workspaceRoot closedRoot declared policy
