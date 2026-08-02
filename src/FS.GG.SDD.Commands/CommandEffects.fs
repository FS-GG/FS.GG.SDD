namespace FS.GG.SDD.Commands

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Reflection
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes

module CommandEffects =
    let fullPath (projectRoot: string) (relativePath: string) =
        Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))

    let parentDirectory (path: string) =
        match Path.GetDirectoryName path with
        | null
        | "" -> "."
        | parent -> parent

    /// Commit `text` to `absolute` so that no reader ever observes a partial write.
    ///
    /// `File.WriteAllText` opens with `FileMode.Create`: the destination is truncated to zero and then
    /// refilled. Anything reading in between — an agent harness, a file watcher, a second `fsgg-sdd`
    /// process — sees a prefix. That is how a `spec.md` was briefly observable holding only its
    /// boilerplate `FR-001` placeholder (FS.GG.SDD#164, FS.GG.Audio feedback §3.9).
    ///
    /// Instead: fill a sibling temp file, then rename it over the destination. A *sibling* shares the
    /// destination's volume, which is exactly what makes the rename atomic (`rename(2)` /
    /// `MoveFileEx(MOVEFILE_REPLACE_EXISTING)`). `File.Replace` would also be atomic but requires the
    /// destination to already exist, and `WriteFile` must create-or-replace uniformly.
    ///
    /// The temp never survives the call: `finally` removes it on any failure, so a crashed write leaves
    /// the destination's prior bytes intact and no residue. The leading `.` keeps it out of the
    /// `readiness/**` and `work/**` globs even inside the crash window, and the GUID never reaches any
    /// report, digest, or artifact — determinism contracts observe committed bytes only.
    ///
    /// The rename *replaces the destination's inode*, so without care the temp's mode (whatever the
    /// process umask yields, typically `0644`) would silently become the artifact's mode — where
    /// `File.WriteAllText` used to preserve it by writing through the existing inode. That regresses in
    /// both directions: a `chmod +x` script loses its exec bit, and a deliberately `0600` artifact
    /// becomes world-readable. So carry the destination's mode onto the temp before the rename.
    ///
    /// Two inode-identity consequences remain, both accepted: a symlink at `absolute` is *replaced*
    /// rather than written through, and a hardlink elsewhere stops tracking the file. No SDD artifact
    /// path is a symlink or a hardlink.
    let private writeFileAtomic (absolute: string) (text: string) =
        let directory = parentDirectory absolute

        let temp =
            Path.Combine(directory, $".{Path.GetFileName absolute}.{Guid.NewGuid():N}.tmp")

        try
            File.WriteAllText(temp, text)

            if not (OperatingSystem.IsWindows()) && File.Exists absolute then
                File.SetUnixFileMode(temp, File.GetUnixFileMode absolute)

            File.Move(temp, absolute, true)
        finally
            // `File.Delete` is a no-op on a missing path, and a successful `File.Move` has already
            // unlinked the temp. Swallow a cleanup failure so it cannot replace the in-flight write
            // exception — the caller's `toolDefect` must report why the *write* failed, not why the
            // cleanup did.
            try
                File.Delete temp
            with _ ->
                ()

    /// Why a file read did not yield a body — the file edge's states, before they are projected
    /// onto `ReadResult`. Local to this module, and deliberately NOT a fifth `ReadResult` case.
    ///
    /// Every verdict fold's correct decision about a body that does not DECODE is the one it
    /// already makes about a body it could not READ: do not compare it, do not call it drift, do
    /// not call it missing, do not count it checked, and do not report coherent over it.
    /// FS.GG.SDD#748 says so in as many words — *"It is the same representation gap; a non-UTF-8
    /// body is just a second cause that needs it."* A fifth case would oblige every fold over
    /// `ReadResult` to spell an arm byte-identical to its `Unreadable` one, and each of those is a
    /// fresh opportunity to spell it differently; the DU's job is to make a fold DECIDE, not to
    /// make it decide the same thing twice.
    ///
    /// What genuinely differs is the operator's repair, so the distinction is kept exactly where it
    /// changes behaviour and nowhere else: the diagnostic `interpret` emits.
    type private FileRead =
        /// The path does not exist.
        | Missing
        /// The bytes were read AND decoded.
        | Body of FileSnapshot
        /// The open or the read threw: permissions, an IO fault, a device error.
        | IoRefusal of reason: string
        /// The bytes were obtained and are not a decodable body, carrying the offset of the first
        /// invalid sequence (FS.GG.SDD#737's `SkillMirror.decodeBody`).
        | DecodeRefusal of byteOffset: int

    /// The `Unreadable` reason for a body that does not decode. ONE spelling, shared by the
    /// `ReadResult` a fold sees and the diagnostic an operator reads, so the two can never drift
    /// apart into two different accounts of one file.
    let private undecodableReason (byteOffset: int) =
        $"the bytes are not a decodable body — the first invalid sequence begins at byte offset {byteOffset}"

    /// The file read, in the states the edge can actually distinguish.
    ///
    /// FS.GG.SDD#748: the bytes are read RAW and decoded through `SkillMirror.decodeBody`, not
    /// `File.ReadAllText`. Those differ in exactly one case and it is the case that matters —
    /// `File.ReadAllText` SUBSTITUTES `U+FFFD` for a sequence it cannot decode and returns a string,
    /// so an undecodable file used to read as a perfectly ordinary body, get hashed, and compare
    /// equal to every other file whose invalid bytes substituted the same way. `decodeBody` is
    /// character-for-character `File.ReadAllText` for every body that DOES decode — BOM detection
    /// and preamble stripping included — so no digest anywhere changes and no manifest migrates;
    /// only the mangling case is refused.
    let private readFile (projectRoot: string) (path: string) : FileRead =
        let absolute = fullPath projectRoot path

        if not (File.Exists absolute) then
            Missing
        else
            try
                let bytes = File.ReadAllBytes absolute

                match Fsgg.SkillMirror.decodeBody bytes with
                | Ok text ->
                    Body(
                        { Path = path
                          Text = text
                          RawBytes = Some bytes }
                        : FileSnapshot
                    )
                | Error(Fsgg.SkillMirror.NotDecodable byteOffset) -> DecodeRefusal byteOffset
            with ex ->
                IoRefusal ex.Message

    /// The file read, as the states the core actually has (FS.GG.SDD#745, decision #754).
    ///
    /// The whole point is the third arm. `File.Exists` answers from `stat`, which succeeds on a
    /// mode-000 file, so *exists* and *readable* are genuinely different questions — and before
    /// #745 the second one had no answer to return. The open threw, the exception escaped to
    /// `interpret`'s outer handler, and the read surfaced as a `toolDefect` at exit 2 (an
    /// accusation that the TOOL is broken over a permissions accident) while every verdict fold
    /// downstream still saw only "no bytes", indistinguishable from a file that is not there.
    ///
    /// `Unreadable` carries the reason verbatim from the exception rather than a rephrasing: the
    /// operator needs to know whether this was a mode bit, a dangling symlink, or a device error,
    /// and the tool cannot classify that better than the OS already did. FS.GG.SDD#748's arm is the
    /// one exception, and it is not a rephrasing either: nothing threw, so there is no OS message
    /// to carry, and `undecodableReason` states the fact the decoder established instead.
    ///
    /// Catching broadly is deliberate and is the fail-CLOSED direction: every escape from the read
    /// on a path `File.Exists` just affirmed is, by construction, "it is there and I could not read
    /// it". A narrower filter would let some IO faults keep escaping to the outer handler — i.e.
    /// keep the exit-2 bug for a subset — which is the failure this replaces.
    let tryRead (projectRoot: string) (path: string) : ReadResult =
        match readFile projectRoot path with
        | Missing -> Absent
        | Body snapshot -> Bytes snapshot
        | IoRefusal reason -> Unreadable(path, reason)
        // #748: an undecodable body rides the state #745 built, because every fold's decision about
        // it is already the right one. It is `Unreadable` and never `Absent` — routing it through
        // the only other channel the read edge has would convert a refusal into a PASS, which is
        // the entire failure #748 was filed to prevent.
        | DecodeRefusal byteOffset -> Unreadable(path, undecodableReason byteOffset)

    /// `tryRead` projected to the bytes. Retained verbatim for the callers that only ever wanted
    /// the body; it cannot express the third state, so it may never decide a coherence verdict.
    let snapshotIfExists (projectRoot: string) (path: string) =
        match tryRead projectRoot path with
        | Bytes snapshot -> Some snapshot
        | Absent
        | Unreadable _
        | Truncated _ -> None

    /// Does this directory OPEN? One entry is enough to answer, and reading one entry is what a
    /// mode-000 directory refuses — so this is the cheapest honest probe and it costs one
    /// `opendir` per directory. `Ok ()` = it opens; `Error reason` = it does not, verbatim from
    /// the OS.
    ///
    /// It exists because `IgnoreInaccessible = true` is SILENT: the tolerant enumerations below
    /// simply omit what they could not open, and an omission is indistinguishable from an empty
    /// directory. That silence is the whole hazard #743 is about, so the skip is discovered by
    /// asking each directory directly rather than inferred from an absence.
    let private opens (absoluteDirectory: string) =
        try
            Directory.EnumerateFileSystemEntries absoluteDirectory |> Seq.tryHead |> ignore
            Ok()
        with ex ->
            Error ex.Message

    /// The directory listing, in the same states as the file edge plus the one only a LISTING can
    /// be in: complete-but-partial (FS.GG.SDD#743).
    ///
    /// `Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)` resolves to
    /// `EnumerationOptions.CompatibleRecursive`, whose `IgnoreInaccessible` is **false**, so one
    /// mode-000 subdirectory threw and the whole call failed. #745 caught that and reported the
    /// ROOT `Unreadable` — fail-closed, and better than the `toolDefect`/exit 2 it replaced, but
    /// it discards the listing entirely. Measured on `main`: one `chmod 000` on
    /// `.claude/skills/fs-gg-demo/references` made `doctor` report four OTHER, readable, present
    /// copies under `.claude/skills` as *not mirrored* — phantom whole-root drift, #743 AC3.
    ///
    /// So: enumerate TOLERANTLY, and record what was skipped.
    ///
    /// - `IgnoreInaccessible = true` keeps every listable entry, which is AC3.
    /// - A second, equally tolerant pass over the DIRECTORIES probes each one with `opens`. An
    ///   inaccessible directory is still an ENTRY of its readable parent, so it is returned by
    ///   that pass even though nothing under it is; probing is what turns the enumerator's silence
    ///   into a named path, which is AC2. A directory whose PARENT could not be opened is never
    ///   reached and needs no row of its own — its parent's row already says the subtree is
    ///   unobserved.
    /// - The ROOT is probed FIRST and separately, because `IgnoreInaccessible = true` applies to it
    ///   too: a mode-000 root returns an EMPTY listing instead of throwing (verified on net10.0).
    ///   Reporting that as `Bytes ""` would be precisely the fail-open this fixes, one level up
    ///   again — "there is nothing here to check" over a root nothing could look inside. It stays
    ///   `Unreadable`, which is what #745 established and what `CommandEffectsTests` pins.
    let tryEnumerate (projectRoot: string) (path: string) : ReadResult =
        let absolute = fullPath projectRoot path

        let relative (full: string) =
            Path.GetRelativePath(projectRoot, full).Replace('\\', '/')

        if not (Directory.Exists absolute) then
            Absent
        else
            match opens absolute with
            | Error reason -> Unreadable(path, reason)
            | Ok() ->
                // Recursive AND tolerant. Nothing below throws: a subtree that cannot be opened is
                // omitted here and named by `skipped` instead.
                //
                // `AttributesToSkip` and `MatchType` are set EXPLICITLY, and that is the whole
                // reason this is not a one-argument constructor call. `SearchOption.AllDirectories`
                // resolved to `EnumerationOptions.CompatibleRecursive`, whose `AttributesToSkip` is
                // `None` and whose `MatchType` is `Win32`; a bare `EnumerationOptions(…)` defaults
                // to `Hidden ||| System` and `Simple`. Constructing one to gain `IgnoreInaccessible`
                // therefore silently ALSO stops listing every dot-named file — `.DS_Store`,
                // `.gitignore`, a dot-named skill auxiliary — because .NET maps a leading dot to
                // `Hidden` on Unix. That is a narrowed read set bought for a fixed exit code, which
                // is the trade #743 explicitly forbids ("would trade a wrong exit code for a blind
                // spot — the strictly worse failure"), and it is invisible: fewer subjects examined
                // reads exactly like fewer subjects in drift. The repo's own suite caught it — a
                // dot-named file under a skill root went green-to-empty.
                //
                // FS-GG/FS.GG.SDD#747 MOVED THAT CANARY, and deliberately kept one. The case that
                // caught it was `an EXTRA junk file in one root is advisory drift named at the
                // roots that LACK it`, whose subject was `.DS_Store` — a file #747 now EXCLUDES
                // from the comparison, so that case would report empty either way and would have
                // stopped being able to fail. The dot-named subject is therefore carried by
                // `an EXTRA dot-named file in one root is advisory drift named at the roots that
                // LACK it` (`.editorconfig` — dot-named, and on no ignore list), which reds the
                // same way if this enumeration ever stops listing dot-named files.
                //
                // Tolerance is the ONLY intended change from the previous behaviour; everything
                // else is pinned to match it.
                let options =
                    EnumerationOptions(
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.None,
                        MatchType = MatchType.Win32
                    )

                let entries =
                    Directory.EnumerateFiles(absolute, "*", options)
                    |> Seq.map relative
                    |> Seq.sort
                    |> String.concat "\n"

                let skipped =
                    Directory.EnumerateDirectories(absolute, "*", options)
                    |> Seq.choose (fun directory ->
                        match opens directory with
                        | Ok() -> None
                        | Error reason -> Some(relative directory, reason))
                    |> Seq.sortBy fst
                    |> Seq.toList

                let snapshot =
                    { Path = path
                      Text = entries
                      RawBytes = None }
                    : FileSnapshot

                match skipped with
                | [] -> Bytes snapshot
                | _ -> Truncated(snapshot, skipped)

    /// `tryEnumerate` projected to the listing. A TRUNCATED listing is still a listing and is
    /// returned as one — that is #743 AC3, and it is safe here only because the truncation is
    /// carried on `Read`, where the verdict folds consult it. As with `snapshotIfExists`, this
    /// projection cannot express the refusal, so it may never decide a coherence verdict.
    let directorySnapshot (projectRoot: string) (path: string) =
        match tryEnumerate projectRoot path with
        | Bytes snapshot
        | Truncated(snapshot, _) -> Some snapshot
        | Absent
        | Unreadable _ -> None

    /// The tag decides. An absent file is always writable, and an identical rewrite is a no-op
    /// whatever the kind. Otherwise only the two tool-owned kinds may replace bytes: a
    /// `GeneratedView` the tool alone produces, and a `HybridArtifact` whose text is already the
    /// merge of re-derived tool regions with the author's preserved ones. `AuthoredSource` joins
    /// `StructuredSource` and `AgentGuidanceTarget` in refusing — the tool never writes authored
    /// prose, so an effect that claims it is a tool defect, caught here rather than on disk.
    let canOverwrite (kind: ArtifactWriteKind) (existing: FileSnapshot option) (text: string) =
        match existing, kind with
        | None, _ -> true
        | Some snapshot, _ when snapshot.Text = text -> true
        | Some _, HybridArtifact _ -> true
        | Some _, GeneratedView -> true
        | Some _, _ -> false

    /// `Snapshot` is DERIVED from `Read` here and nowhere else, so the two cannot drift apart —
    /// the failure mode a second, hand-passed snapshot argument would have re-opened is a result
    /// claiming `Read = Unreadable` while still carrying bytes for a fold to compare.
    ///
    /// `Truncated` yields its snapshot (#743). That is NOT the failure above: the bytes are a real,
    /// honestly-obtained partial listing that AC3 requires downstream, and the incompleteness has
    /// not been dropped — it is on `Read`, which is where every verdict fold is already required to
    /// look. The invariant that matters is preserved exactly: `Snapshot` is still a projection of
    /// `Read` and can still never carry bytes `Read` does not have.
    let private snapshotOf (read: ReadResult) =
        match read with
        | Bytes snapshot
        | Truncated(snapshot, _) -> Some snapshot
        | Absent
        | Unreadable _ -> None

    let success (effect: CommandEffect) (read: ReadResult) =
        { Effect = effect
          Succeeded = true
          Read = read
          Snapshot = snapshotOf read
          Process = None
          Confirmed = None
          Diagnostic = None }

    let failure (effect: CommandEffect) (read: ReadResult) diagnostic =
        { Effect = effect
          Succeeded = false
          Read = read
          Snapshot = snapshotOf read
          Process = None
          Confirmed = None
          Diagnostic = Some diagnostic }

    // The per-stream retention bound for captured provider stdout/stderr (feature 054,
    // E4). Content beyond this many characters is drained (deadlock-safe) but neither
    // retained nor buffered, so a runaway child cannot exhaust parent memory (#68); the
    // stream's truncation flag records that a tail was dropped (FR-005).
    let providerOutputCapChars = 65536

    // The wall-clock ceiling for a single child process (#68). `dotnet new install/update/
    // create`, `dotnet tool update`, and the git probe all launch at this edge; without a
    // bound a wedged child hangs the CLI forever. The default is generous enough for a cold
    // network restore; `FSGG_SDD_PROCESS_TIMEOUT_MS` overrides it (a test uses a tiny value
    // to exercise the kill path). A non-positive / unparseable value falls back to the default.
    let private defaultProcessTimeoutMs = 600_000

    // The synthesized exit code reported when a child is killed on timeout: a nonzero,
    // fail-closed value (the conventional `timeout(1)` code) so handlers classify a hung
    // process as a provider/step failure rather than mistaking it for success.
    let private processTimeoutExitCode = 124

    // The bounded wait for reaping a killed child — its exit and its two drain tasks (§3).
    // `Kill true` is swallowed on the vanishingly rare unkillable-process / permission fault,
    // and `WaitForExit(int)` returns at child exit, not at pipe EOF, so a grandchild that
    // inherited the write ends can leave both readers pending. Without a bound the follow-up
    // reap would be the exact hang the timeout exists to prevent. A real kill completes in well
    // under this budget, so the ordinary timeout path is unaffected; a stuck reap hits the bound
    // and we still report the fail-closed timeout result.
    let private postKillReapMs = 5_000

    let processTimeoutMs () =
        match Int32.TryParse(Environment.GetEnvironmentVariable "FSGG_SDD_PROCESS_TIMEOUT_MS") with
        | true, ms when ms > 0 -> ms
        | _ -> defaultProcessTimeoutMs

    // Drain a redirected stream to EOF (so the child never blocks on a full pipe, R1) while
    // retaining at most the cap in memory (R2/#68): bytes past the cap are counted for the
    // truncation flag but discarded, not buffered. Reads in bounded chunks; runs as a hot
    // task so both streams drain concurrently with `WaitForExit`.
    let private readCappedAsync (reader: TextReader) : System.Threading.Tasks.Task<string * bool> =
        task {
            let builder = System.Text.StringBuilder()
            let buffer = Array.zeroCreate<char> 8192
            let mutable truncated = false
            let mutable reading = true

            while reading do
                let! read = reader.ReadAsync(buffer, 0, buffer.Length)

                if read = 0 then
                    reading <- false
                else
                    let remaining = providerOutputCapChars - builder.Length

                    if remaining > 0 then
                        builder.Append(buffer, 0, min read remaining) |> ignore

                    if read > remaining then
                        truncated <- true

            return builder.ToString(), truncated
        }

    // Edge interpreter for `RunProcess`: launches a real child process, captures its
    // exit code and (bounded) stdout/stderr, and snapshots the working directory
    // afterwards so the handler can diff produced paths. Honors DryRun (plans without
    // spawning). Both streams are read concurrently before `WaitForExit` so a child that
    // fills one pipe while the parent bounds the other cannot deadlock (R1); the retained
    // content is capped per stream (R2) and decoded as UTF-8 with replacement so non-UTF-8
    // / binary bytes cannot throw or corrupt the JSON report (R9).
    let runProcess
        (projectRoot: string)
        (effect: CommandEffect)
        (command: string)
        (args: string list)
        (workingDir: string)
        =
        let absolute = fullPath projectRoot workingDir
        Directory.CreateDirectory absolute |> ignore

        // Non-throwing UTF-8 decode: invalid bytes become replacement characters (CAP-5).
        let decoding = System.Text.UTF8Encoding(false, false)

        let startInfo =
            ProcessStartInfo(
                FileName = command,
                WorkingDirectory = absolute,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = decoding,
                StandardErrorEncoding = decoding
            )

        args |> List.iter startInfo.ArgumentList.Add

        // The fully-resolved command line as executed (program + args) — FR-001.
        let commandLine = String.concat " " (command :: args)

        try
            use proc = Process.Start startInfo

            match proc with
            | null ->
                { Effect = effect
                  Succeeded = true
                  Read = Absent
                  Snapshot = None
                  Process =
                    Some
                        { Started = false
                          ExitCode = -1
                          Command = commandLine
                          StandardOutput = ""
                          StandardOutputTruncated = false
                          StandardError = ""
                          StandardErrorTruncated = false }
                  Confirmed = None
                  Diagnostic = None }
            | proc ->
                // Read both pipes concurrently so neither can block the child (R1); retain at
                // most the cap per stream (R2).
                let stdoutTask = readCappedAsync proc.StandardOutput
                let stderrTask = readCappedAsync proc.StandardError
                let timeoutMs = processTimeoutMs ()

                if proc.WaitForExit timeoutMs then
                    // Exited within the bound: reap the reader tasks and report normally.
                    let stdout, stdoutTruncated = stdoutTask.GetAwaiter().GetResult()
                    let stderr, stderrTruncated = stderrTask.GetAwaiter().GetResult()

                    // ONE enumeration, shared by the three-state `Read` and its derived
                    // `Snapshot`. Enumerating twice would walk the tree twice and, worse, could
                    // disagree if the directory's readability changed between the two walks.
                    let after = tryEnumerate projectRoot workingDir

                    { Effect = effect
                      Succeeded = true
                      Read = after
                      Snapshot = snapshotOf after
                      Process =
                        Some
                            { Started = true
                              ExitCode = proc.ExitCode
                              Command = commandLine
                              StandardOutput = stdout
                              StandardOutputTruncated = stdoutTruncated
                              StandardError = stderr
                              StandardErrorTruncated = stderrTruncated }
                      Confirmed = None
                      Diagnostic = None }
                else
                    // Timed out: kill the whole tree, reap, and report a fail-closed nonzero
                    // exit so the handler classifies it as a provider/step failure (#68) — an
                    // incomplete run is never mistaken for success. The termination note is
                    // appended to stderr so the report can explain the failure.
                    (try
                        proc.Kill true
                     with _ ->
                         ())

                    // Bound every step of the reap. If `Kill` threw and was swallowed (unkillable
                    // process / permission fault) or a grandchild still holds the pipes, an un-timed
                    // wait here would relocate the hang from before the timeout to after it. A drain
                    // task that has not completed within the budget yields ("", false), and we report
                    // the fail-closed timeout result regardless of the `Kill` outcome.
                    proc.WaitForExit postKillReapMs |> ignore

                    let reap (readerTask: System.Threading.Tasks.Task<string * bool>) =
                        if readerTask.Wait postKillReapMs then
                            readerTask.Result
                        else
                            ("", false)

                    let stdout, stdoutTruncated = reap stdoutTask
                    let capturedErr, stderrTruncated = reap stderrTask
                    let after = tryEnumerate projectRoot workingDir

                    let timeoutNote =
                        $"fsgg-sdd: process timed out after {timeoutMs} ms and was terminated: {commandLine}"

                    let stderr =
                        if String.IsNullOrEmpty capturedErr then
                            timeoutNote
                        else
                            capturedErr + "\n" + timeoutNote

                    { Effect = effect
                      Succeeded = true
                      Read = after
                      Snapshot = snapshotOf after
                      Process =
                        Some
                            { Started = true
                              ExitCode = processTimeoutExitCode
                              Command = commandLine
                              StandardOutput = stdout
                              StandardOutputTruncated = stdoutTruncated
                              StandardError = stderr
                              StandardErrorTruncated = stderrTruncated }
                      Confirmed = None
                      Diagnostic = None }
        with ex ->
            // The provider engine/command could not be launched: surfaced as
            // scaffold.providerUnavailable by the handler (Started = false). The launch
            // error is retained on StandardError so the report can explain the failure (R4).
            { Effect = effect
              Succeeded = true
              Read = Absent
              Snapshot = None
              Process =
                Some
                    { Started = false
                      ExitCode = -1
                      Command = commandLine
                      StandardOutput = ""
                      StandardOutputTruncated = false
                      StandardError = ex.Message
                      StandardErrorTruncated = false }
              Confirmed = None
              Diagnostic = None }

    // Edge interpreter for `ReadPackageSurface` (feature 105, Phase 2; ADR-0004 D2). Reads the
    // AUTHORITATIVE public surface of a restored framework package by loading its restored assembly
    // from the global packages cache and reflecting it. Deliberately reflection, not `.fsi` text
    // (spec 086's stance) — defeating a stale vendored text is the whole point (ADR-0004
    // §Consequences). Confined to this edge; `analyze` (Phase 3) reads only the committed capture.
    //
    // Fail-open by construction: an uncached/unreadable package yields `None`, which the handler
    // reports as `unavailable` (advisory), never a false drift — "could not look" is not a negative
    // verdict (ADR-0002 / #266). The restore that populates the cache is the workspace's own
    // (a consumer product references the package); this verb reads what restore left behind.

    // Rank a target-framework folder so the richest modern surface wins: modern `netX.Y` over
    // `netcoreappX.Y` over `netstandardX.Y` over legacy framework `net4x`. Deterministic.
    let private targetFrameworkScore (tfm: string) =
        let lower = tfm.ToLowerInvariant()

        let numericTail (prefix: string) =
            let rest = lower.Substring(prefix.Length)

            match Double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, value -> value
            | _ -> 0.0

        if lower.StartsWith "netstandard" then
            200.0 + numericTail "netstandard"
        elif lower.StartsWith "netcoreapp" then
            500.0 + numericTail "netcoreapp"
        elif lower.StartsWith "net" then
            let rest = lower.Substring 3
            // A modern TFM carries a dotted version (`net8.0`); a legacy framework one does not
            // (`net47`). The modern surface is preferred, so it scores far higher.
            if rest.Contains "." then
                1000.0 + numericTail "net"
            else
                100.0 + numericTail "net"
        else
            0.0

    let private globalPackagesRoot () =
        match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
        | null
        | "" -> Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".nuget", "packages")
        | configured -> configured

    // Pick the best restored assembly for a package: the `<Pkg>.dll` under the highest-scoring
    // `lib`/`ref` target-framework folder (preferring `lib`). `None` when nothing plausible exists.
    let private selectPackageAssembly (packageDir: string) (packageId: string) =
        let expectedName = packageId + ".dll"

        [ "lib"; "ref" ]
        |> List.collect (fun kind ->
            let kindDir = Path.Combine(packageDir, kind)

            if not (Directory.Exists kindDir) then
                []
            else
                Directory.EnumerateDirectories kindDir
                |> Seq.collect (fun tfmDir ->
                    let tfmName = Path.GetFileName tfmDir |> Option.ofObj |> Option.defaultValue ""

                    Directory.EnumerateFiles(tfmDir, "*.dll")
                    |> Seq.map (fun dll ->
                        let fileName = Path.GetFileName dll |> Option.ofObj |> Option.defaultValue ""

                        let nameMatches =
                            String.Equals(fileName, expectedName, StringComparison.OrdinalIgnoreCase)

                        // `lib` wins ties over `ref` (the runtime surface); name-match wins over any.
                        let kindBonus = if kind = "lib" then 0.5 else 0.0
                        (nameMatches, targetFrameworkScore tfmName + kindBonus, dll)))
                |> Seq.toList)
        |> List.sortByDescending (fun (nameMatches, score, _) -> (nameMatches, score))
        |> List.tryHead
        |> Option.map (fun (_, _, dll) -> dll)

    let readPackageSurface (effect: CommandEffect) (packageId: string) (version: string) =
        try
            let packageDir =
                Path.Combine(globalPackagesRoot (), packageId.ToLowerInvariant(), version)

            match
                (if Directory.Exists packageDir then
                     selectPackageAssembly packageDir packageId
                 else
                     None)
            with
            // Not restored / no assembly. `Absent` and NOT `Unreadable`, deliberately and against
            // the surface reading of the word: this is the ONE read edge whose "could not look" is
            // already a first-class reported state — `dependency-surface` folds it to
            // `unavailable` and `dependencySurfaceUnavailable` (advisory, exit 0, ADR-0002/#266),
            // because a package outside the restore graph is not a subject the workspace declared.
            // #745 changes the FILE edge, where "could not look" had no representation at all.
            | None -> success effect Absent
            | Some assemblyPath ->
                // Load into a fresh context is unnecessary for name-only reflection; `LoadFrom`
                // is enough and `symbolsFromAssembly` tolerates partial type loads. Any failure
                // (bad image, load conflict) is caught below and reported as unavailable.
                let assembly = Assembly.LoadFrom assemblyPath
                let symbols = DependencySurface.symbolsFromAssembly assembly

                success
                    effect
                    (Bytes(
                        { Path = $"{packageId}@{version}"
                          Text = String.concat "\n" symbols
                          RawBytes = None }
                        : FileSnapshot
                    ))
        with _ ->
            // Fail-open: an unreadable surface is advisory, never a tool defect and never a drift.
            // Same `Absent`-not-`Unreadable` reasoning as the arm above — the lane already reports
            // it as `unavailable`, so the third state would add a second name for one fact.
            success effect Absent

    // Edge interpreter for `Confirm` (feature 053, confirm-effect contract). Under `DryRun`
    // it never mutates and never reads stdin (`Some false`). Otherwise it writes the step
    // diff/prompt and reads one line from `Console.In` (`y`/`yes`, case-insensitive →
    // confirmed). A `Confirm` is only ever emitted on the interactive path (the pure core
    // refuses a non-interactive run without `--yes` up front, and `--yes` applies directly
    // without emitting `Confirm`), so this stdin read is only reached interactively; EOF/
    // redirected-empty stdin returns null → declined, never a hang. The prompt text is
    // presentation-only and excluded from the deterministic json; the decision
    // (`Confirmed`) is the contract-relevant fact. The prompt is written to **stderr** (not
    // stdout) so `fsgg-sdd upgrade > out.json` from a TTY cannot prepend prompt bytes to the
    // deterministic JSON report on stdout (#68).
    let confirm (dryRun: bool) (effect: CommandEffect) (prompt: string) =
        let decision =
            if dryRun then
                false
            else
                Console.Error.Write prompt
                Console.Error.Flush()

                match (Option.ofObj (Console.In.ReadLine()) |> Option.defaultValue "").Trim().ToLowerInvariant() with
                | "y"
                | "yes" -> true
                | _ -> false

        { Effect = effect
          Succeeded = true
          Read = Absent
          Snapshot = None
          Process = None
          Confirmed = Some decision
          Diagnostic = None }

    let interpret (projectRoot: string) (dryRun: bool) (effect: CommandEffect) =
        try
            match effect with
            // THE READ EDGE (FS.GG.SDD#745, decision #754; the fourth state, FS.GG.SDD#743). Every
            // state is carried forward; none of them is an exception any more.
            //
            // `Succeeded = true` on the `Unreadable` arm is deliberate and is the difference
            // between this and the pre-#745 behaviour. The READ did what a read can do: it looked,
            // and it reports what it found. Nothing about the tool failed, so nothing here may
            // escalate to exit 2 — the run continues, the WARNING names the file and the reason,
            // and it is the verdict FOLD downstream that must refuse to be coherent over a subject
            // it did not read. Putting the block here instead would make one unreadable file fatal
            // to `doctor`, which is documented read-only and exit 0 (#754 rejected that).
            // Matched on `readFile`, not `tryRead`, and that is the whole of FS.GG.SDD#748's
            // caller half. Both refusals are the same `ReadResult` — `Unreadable` — because every
            // fold downstream must treat them identically; they are two different DIAGNOSTICS
            // because the operator must not. This is the one place in the program that can tell
            // them apart, and it is the one place the difference does any good.
            | ReadFile path ->
                match readFile projectRoot path with
                | Body snapshot -> success effect (Bytes snapshot)
                | Missing -> success effect Absent
                | IoRefusal reason ->
                    { Effect = effect
                      Succeeded = true
                      Read = Unreadable(path, reason)
                      Snapshot = None
                      Process = None
                      Confirmed = None
                      Diagnostic = Some(Diagnostics.unreadableFile path reason) }
                | DecodeRefusal byteOffset ->
                    // #737 AC2, finally satisfiable: the library named the byte offset because that
                    // is all it can see, and NAMING THE FILE is the caller's half. `Succeeded` is
                    // true and this is not a tool defect for the same reason the arm above is
                    // neither — a mis-encoded file in the workspace is an authoring accident, and
                    // the block is the verdict fold's to apply (`unreadableSubject`), not the
                    // edge's.
                    { Effect = effect
                      Succeeded = true
                      Read = Unreadable(path, undecodableReason byteOffset)
                      Snapshot = None
                      Process = None
                      Confirmed = None
                      Diagnostic = Some(Diagnostics.undecodableFile path byteOffset) }
            | EnumerateDirectory path ->
                match tryEnumerate projectRoot path with
                | Bytes snapshot -> success effect (Bytes snapshot)
                | Absent -> success effect Absent
                | Unreadable(unreadablePath, reason) ->
                    // The `EnumerateDirectory` sibling, and the more dangerous of the two: a
                    // listing that comes back empty because the directory could not be opened
                    // yields an EMPTY candidate set, which every fold reads as "there is nothing
                    // here to check" — a pass over an unknown number of subjects.
                    { Effect = effect
                      Succeeded = true
                      Read = Unreadable(unreadablePath, reason)
                      Snapshot = None
                      Process = None
                      Confirmed = None
                      Diagnostic = Some(Diagnostics.unreadableFile unreadablePath reason) }
                | Truncated(snapshot, skipped) ->
                    // FS.GG.SDD#743. The listing SURVIVES — `success` derives `Snapshot` from
                    // `Read`, so every candidate set downstream gets the entries that were listable
                    // (AC3) — and the skipped directories ride on `Read`, where `unreadablePathsOf`
                    // finds them and the verdict folds block on them (AC2). The warning names each
                    // one and why (AC2); it is not blocking on its own and never a tool defect
                    // (AC1/AC4), for the same reason `unreadableFile` is not.
                    { success effect (Truncated(snapshot, skipped)) with
                        Diagnostic = Some(Diagnostics.unlistableDirectory path skipped) }
            | CreateDirectory path ->
                let absolute = fullPath projectRoot path

                let existing =
                    if Directory.Exists absolute then
                        Bytes(
                            { Path = path
                              Text = "<directory>"
                              RawBytes = None }
                            : FileSnapshot
                        )
                    else
                        Absent

                if not dryRun then
                    Directory.CreateDirectory absolute |> ignore

                success effect existing
            | WriteFile(path, text, kind) ->
                // THE WRITE EDGE'S PRE-READ (#745 AC3). `canOverwrite` decides whether the tool may
                // replace an existing file FROM THAT FILE'S CURRENT BYTES, so an unreadable
                // destination makes the decision undecidable — and the fail-closed answer to an
                // undecidable safety question is to refuse, not to write.
                //
                // Before #745 this call threw, the exception escaped to the outer handler, and the
                // run reported `toolDefect` at exit 2: `upgrade --yes` and `charter` over a
                // mode-000 target both accused the TOOL of being broken over a mode bit, while
                // emitting three `toolDefect`s beside warnings whose correction read "Nothing about
                // the tool is broken." Now it blocks at exit 1 with a diagnostic naming the file.
                //
                // FS.GG.SDD#748 refuses here for the same reason and needs its OWN arm, which is not
                // the same statement. The refusal is identical — `canOverwrite` compares the
                // destination's CURRENT TEXT to the text being written, and a body that does not
                // decode has no current text, only a substitution that would make a file that
                // DIFFERS compare equal — but the REMEDY is not, and this is the edge where getting
                // the remedy wrong costs the most. The operator is being told the tool will not
                // overwrite their file; `chmod +r` against a `0644` file is an instruction they can
                // carry out completely, after which the run refuses in exactly the same way.
                //
                // So this arm matches `readFile` rather than `tryRead`: `tryRead` has already
                // collapsed both causes into one `Unreadable`, which is right for every FOLD and
                // wrong for the only two callers that choose a diagnostic.
                match readFile projectRoot path with
                | IoRefusal reason ->
                    failure effect (Unreadable(path, reason)) (Diagnostics.unreadableWriteTarget path reason)
                | DecodeRefusal byteOffset ->
                    failure
                        effect
                        (Unreadable(path, undecodableReason byteOffset))
                        (Diagnostics.undecodableWriteTarget path byteOffset)
                | fileRead ->
                    let existing =
                        match fileRead with
                        | Body snapshot -> Some snapshot
                        | Missing
                        // Unreachable: both refusals are matched above.
                        | IoRefusal _
                        | DecodeRefusal _ -> None

                    let existingRead =
                        match existing with
                        | Some snapshot -> Bytes snapshot
                        | None -> Absent

                    // The bytes are already on disk. Skip the commit entirely rather than re-committing
                    // identical content: `writeFileAtomic` renames a fresh inode over the destination, so a
                    // no-op write would still unlink the old inode — replacing a symlink with a regular file,
                    // detaching hardlinks, and churning every inode-tracking watcher on an unchanged
                    // `refresh`. The truncating write it replaced had no such side effect, so this keeps a
                    // no-op run genuinely no-op. `ArtifactOperation.NoChange` is unchanged: it is derived from
                    // `existing` at report assembly and never depended on the write happening.
                    let unchanged =
                        match existing with
                        | Some snapshot -> snapshot.Text = text
                        | None -> false

                    if canOverwrite kind existing text then
                        if not dryRun && not unchanged then
                            let absolute = fullPath projectRoot path
                            Directory.CreateDirectory(parentDirectory absolute) |> ignore
                            writeFileAtomic absolute text

                        success effect existingRead
                    else
                        failure effect existingRead (unsafeOverwrite path)
            | RunProcess(command, args, workingDir) ->
                if dryRun then
                    success effect Absent
                else
                    runProcess projectRoot effect command args workingDir
            | ReadPackageSurface(packageId, version) ->
                // Read-only reflection over the restored package; safe under `--dry-run` too, since
                // it mutates nothing. `--check`/`--update` both need the real surface to compare.
                readPackageSurface effect packageId version
            | SetExecutable path ->
                if dryRun then
                    success effect Absent
                else
                    try
                        let absolute = fullPath projectRoot path

                        let executable =
                            File.GetUnixFileMode absolute
                            ||| UnixFileMode.UserExecute
                            ||| UnixFileMode.GroupExecute
                            ||| UnixFileMode.OtherExecute

                        File.SetUnixFileMode(absolute, executable)
                        success effect Absent
                    with _ ->
                        // Read-only FS, non-Unix host, or a missing file: reported as a
                        // skipped/partial make-executable (FR-005, US2-AC3), never a tool
                        // defect. Caught here so the outer handler never escalates it.
                        { Effect = effect
                          Succeeded = false
                          Read = Absent
                          Snapshot = None
                          Process = None
                          Confirmed = None
                          Diagnostic = None }
            | Confirm(_, prompt) -> confirm dryRun effect prompt
        with ex ->
            let path = CommandTypes.effectPath effect
            failure effect Absent (toolDefect path ex.Message)

    let interpretAll (projectRoot: string) (dryRun: bool) (effects: CommandEffect list) =
        effects |> List.map (interpret projectRoot dryRun)

    /// Drive an MVU command to its final report: initialize, then interpret produced effects
    /// and fold their interpreted results back through `update` until no effects remain, build
    /// the report, and resolve it. This is the single canonical run loop shared by the CLI
    /// entry point and the validation harness (feature 061 / issue #71) — previously duplicated
    /// verbatim, where any divergence silently changed validate-vs-CLI behavior.
    let driveToReport (request: CommandRequest) : CommandReport =
        let model, effects = CommandWorkflow.init request

        let rec interpretUntilIdle state pendingEffects =
            match pendingEffects with
            | [] -> state
            | current ->
                let results = interpretAll request.ProjectRoot request.DryRun current

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (currentState, accumulatedEffects) result ->
                            let updatedState, producedEffects =
                                CommandWorkflow.update (EffectInterpreted result) currentState

                            updatedState, accumulatedEffects @ producedEffects)
                        (state, [])

                interpretUntilIdle nextState nextEffects

        let finalModel =
            interpretUntilIdle model effects
            |> fun state -> CommandWorkflow.update BuildReport state |> fst

        finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)
