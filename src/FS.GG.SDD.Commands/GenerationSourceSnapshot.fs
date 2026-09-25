namespace FS.GG.SDD.Commands

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
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

    /// The caller owns the closed-root selection and digest policy. This is a read-only capture:
    /// it never stages or replaces a generated output.
    let capture (workspaceRoot: string) (closedRoot: string) (declared: string list)
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
            match classify workspace "." with
            | Directory -> ()
            | Link -> refuse (Symlink ".")
            | _ -> refuse InvalidRoot
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
                let raw = File.ReadAllBytes absolute
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
