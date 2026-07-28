namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// Feature 093 / FS.GG.SDD#164 (FS.GG.Audio feedback §3.9). The first *direct* test of the effect
/// interpreter — until now it was only exercised transitively through the command handlers.
///
/// The defect: `WriteFile` was interpreted with `File.WriteAllText`, which opens `FileMode.Create` —
/// the destination is truncated to zero, then refilled. A reader in between sees a prefix, which is
/// how a `spec.md` was briefly observable holding only its boilerplate `FR-001` placeholder.
///
/// Note what is *not* asserted here: "no observer ever sees a prefix". Proving that needs a reader
/// racing the writer, which would be flaky and would test `rename(2)` rather than this code. What is
/// asserted is the property that actually protects the author — **a failed write leaves the prior
/// bytes intact and no residue** — plus the structural fact that no direct truncating write remains.
module CommandEffectsTests =
    let private interpret root effect =
        CommandEffects.interpret root false effect

    let private relative = "work/demo/spec.md"

    let private absolute root =
        Path.Combine(root, "work", "demo", "spec.md")

    /// Any file the atomic commit leaves behind in the destination's directory.
    let private residue root =
        let directory = Path.Combine(root, "work", "demo")

        if Directory.Exists directory then
            Directory.EnumerateFiles directory
            |> Seq.map Path.GetFileName
            |> Seq.filter (fun name -> name <> "spec.md")
            |> Seq.toList
        else
            []

    let private seed root (text: string) =
        Directory.CreateDirectory(Path.Combine(root, "work", "demo")) |> ignore
        File.WriteAllText(absolute root, text)

    [<Fact>]
    let ``creates a file that does not yet exist`` () =
        let root = TestSupport.tempDirectory ()

        let result =
            interpret root (WriteFile(relative, "created", HybridArtifact MergePolicies.specification))

        Assert.True result.Succeeded
        Assert.Equal("created", File.ReadAllText(absolute root))
        Assert.Empty(residue root)

    [<Fact>]
    let ``replaces an existing file's content wholesale`` () =
        let root = TestSupport.tempDirectory ()
        seed root "old content that is longer than the new"

        let result =
            interpret root (WriteFile(relative, "new", HybridArtifact MergePolicies.specification))

        Assert.True result.Succeeded
        Assert.Equal("new", File.ReadAllText(absolute root))
        Assert.Empty(residue root)

    /// An identical-content write does not touch the file at all.
    ///
    /// This is not cosmetic. `writeFileAtomic` renames a fresh inode over the destination, so a no-op
    /// re-commit would still unlink the old one — replacing a symlink with a regular file, detaching
    /// hardlinks, and churning inode-tracking watchers on every unchanged `refresh`. The truncating write
    /// it replaced had no such side effect. Asserted via the write timestamp, which stands in for "the
    /// destination was never opened".
    [<Fact>]
    let ``an identical-content write does not touch the destination`` () =
        let root = TestSupport.tempDirectory ()
        seed root "same"

        let before = File.GetLastWriteTimeUtc(absolute root)
        File.SetLastWriteTimeUtc(absolute root, before.AddDays -1.0)
        let stamped = File.GetLastWriteTimeUtc(absolute root)

        let result =
            interpret root (WriteFile(relative, "same", HybridArtifact MergePolicies.specification))

        Assert.True result.Succeeded
        Assert.Equal("same", File.ReadAllText(absolute root))
        Assert.Equal(stamped, File.GetLastWriteTimeUtc(absolute root))
        Assert.Empty(residue root)

    [<Fact>]
    let ``dryRun writes nothing at all - destination or temp`` () =
        let root = TestSupport.tempDirectory ()

        let result =
            CommandEffects.interpret
                root
                true
                (WriteFile(relative, "unwritten", HybridArtifact MergePolicies.specification))

        Assert.True result.Succeeded
        Assert.False(File.Exists(absolute root))
        Assert.Empty(residue root)

    /// A no-clobber kind refuses before any write. The refusal must not leave a temp sibling behind
    /// either — `canOverwrite` is evaluated first, so the atomic path is never entered.
    [<Fact>]
    let ``a refused overwrite touches nothing and leaves no residue`` () =
        let root = TestSupport.tempDirectory ()
        seed root "authored by a human"

        let result = interpret root (WriteFile(relative, "clobbered", StructuredSource))

        Assert.False result.Succeeded
        Assert.Equal("authored by a human", File.ReadAllText(absolute root))
        Assert.Empty(residue root)

        match result.Diagnostic with
        | Some diagnostic -> Assert.Equal("unsafeOverwrite", diagnostic.Id)
        | None -> failwith "expected an unsafeOverwrite diagnostic"

    /// The rename replaces the destination's inode, so the temp's mode (umask-derived, typically `0644`)
    /// would become the artifact's mode unless it is carried across. `File.WriteAllText` preserved the
    /// mode for free by writing through the existing inode; the atomic path must do it deliberately.
    ///
    /// Both directions matter: an executable script must keep its exec bit, and a deliberately
    /// mode-restricted artifact must not silently become world-readable.
    [<Theory>]
    [<InlineData(0o755)>]
    [<InlineData(0o600)>]
    let ``an overwrite preserves the destination's file mode`` (mode: int) =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()
            seed root "before"
            File.SetUnixFileMode(absolute root, enum<UnixFileMode> mode)

            let result =
                interpret root (WriteFile(relative, "after", HybridArtifact MergePolicies.specification))

            Assert.True result.Succeeded
            Assert.Equal("after", File.ReadAllText(absolute root))
            Assert.Equal(enum<UnixFileMode> mode, File.GetUnixFileMode(absolute root))

    /// The property that protects the author. Make the *directory* unwritable so the temp file cannot
    /// be created; the destination's prior bytes must survive intact and nothing may be left behind.
    ///
    /// Skipped on Windows, where directory permissions do not gate file creation this way.
    [<Fact>]
    let ``a failed write leaves the prior bytes intact and no residue`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()
            seed root "the bytes that must survive"
            let directory = Path.Combine(root, "work", "demo")

            File.SetUnixFileMode(directory, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)

            try
                let result =
                    interpret root (WriteFile(relative, "never lands", HybridArtifact MergePolicies.specification))

                Assert.False result.Succeeded

                match result.Diagnostic with
                | Some diagnostic -> Assert.Equal("toolDefect", diagnostic.Id)
                | None -> failwith "expected a toolDefect diagnostic"

                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

                Assert.Equal("the bytes that must survive", File.ReadAllText(absolute root))
                Assert.Empty(residue root)
            finally
                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    /// Structural regression guard (FR-005/FR-006): no `WriteFile` path may truncate the destination
    /// directly. "The bug is gone" is a property of the code, not of any single run — a future edit that
    /// reintroduces `File.WriteAllText(absolute, …)` would pass every behavioral test above and silently
    /// restore the torn-read window, because a single-threaded test cannot observe the gap.
    ///
    /// Deliberately spelling-tolerant: it matches the *shape* of a direct write to `absolute`, not the
    /// exact typography of the current implementation. Pinning source-text verbatim would turn a
    /// reformat or a local rename into a red test with a misleading message.
    ///
    /// The temp-sibling *behavior* — same directory, no residue — is proven by the tests above, not here.
    [<Fact>]
    let ``no WriteFile path truncates the destination directly`` () =
        let source =
            TestSupport.readRelative TestSupport.repoRoot "src/FS.GG.SDD.Commands/CommandEffects.fs"

        Assert.False(
            Regex.IsMatch(source, @"File\.WriteAllText\s*\(\s*absolute\b"),
            "CommandEffects.fs writes directly to the destination path; commit through a temp sibling "
            + "and an atomic rename instead (FS.GG.SDD#164)."
        )

    // ===================================================================================
    // FS.GG.SDD#745 (decision FS.GG.SDD#754) — the read edge's THIRD state.
    //
    // Before this, `interpret` gave the pure core two read states: bytes, or nothing, and
    // *nothing* meant ABSENT. A file that exists and cannot be read had nowhere to go, so it
    // either threw (surfacing as `toolDefect` at exit 2 — the tool accused of being broken over a
    // mode bit) or, once routed through the absent branch, became a SILENTLY VERIFIED file.
    //
    // These are the EDGE legs. The per-lane verdict legs live with their lanes
    // (`SurfaceCommandTests`, `MultiFileSkillDriftTests`, `DependencySurfaceCommandTests`),
    // because #745 AC1 is a property of each lane's verdict, not of any single diagnostic.
    // ===================================================================================

    /// The pair that must never collapse again: one effect, two different files, two DIFFERENT
    /// answers. A test asserting only the `Unreadable` arm would still pass if a future edit made
    /// EVERY read unreadable, so the absent leg is asserted in the same breath.
    [<Fact>]
    let ``read distinguishes absent from present-but-unreadable`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()

            match (interpret root (ReadFile relative)).Read with
            | Absent -> ()
            | other -> failwith $"expected Absent for a file that does not exist, got {other}"

            seed root "secret"
            File.SetUnixFileMode(absolute root, enum<UnixFileMode> 0)

            try
                let result = interpret root (ReadFile relative)

                match result.Read with
                | Unreadable(path, reason) ->
                    Assert.Equal(relative, path)
                    Assert.False(System.String.IsNullOrWhiteSpace reason)
                | other -> failwith $"expected Unreadable for a mode-000 file, got {other}"

                // No bytes, and no exception: the READ succeeded at being a read. The block belongs
                // to the verdict fold, not here — `doctor` is documented read-only and exit 0, and
                // #754 rejected refusing at the edge for exactly that reason.
                Assert.True(Option.isNone result.Snapshot)
                Assert.True result.Succeeded

                match result.Diagnostic with
                | Some diagnostic ->
                    Assert.Equal("unreadableFile", diagnostic.Id)
                    // Never `toolDefect`: nothing about the tool is broken (#745 AC5).
                    Assert.False diagnostic.IsToolDefect
                    // The finding NAMES the file — #735 AC2, reachable only now.
                    Assert.Contains(relative, diagnostic.RelatedIds)
                    Assert.Contains(relative, diagnostic.Message)
                | None -> failwith "expected an unreadableFile diagnostic"
            finally
                File.SetUnixFileMode(absolute root, enum<UnixFileMode> 0o644)

    /// The `EnumerateDirectory` sibling, and the sharper of the two: a listing that comes back
    /// empty because the directory could not be opened yields an EMPTY candidate set, which every
    /// fold downstream reads as "there is nothing here to check".
    [<Fact>]
    let ``enumerate distinguishes absent from present-but-unreadable`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()

            match (interpret root (EnumerateDirectory "work")).Read with
            | Absent -> ()
            | other -> failwith $"expected Absent for a directory that does not exist, got {other}"

            seed root "body"
            let directory = Path.Combine(root, "work", "demo")
            File.SetUnixFileMode(directory, enum<UnixFileMode> 0)

            try
                let result = interpret root (EnumerateDirectory "work/demo")

                match result.Read with
                | Unreadable(path, _) -> Assert.Equal("work/demo", path)
                | other -> failwith $"expected Unreadable for a mode-000 directory, got {other}"

                Assert.True(Option.isNone result.Snapshot)

                match result.Diagnostic with
                | Some diagnostic -> Assert.Equal("unreadableFile", diagnostic.Id)
                | None -> failwith "expected an unreadableFile diagnostic"
            finally
                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    /// FS.GG.SDD#743 — the state only a LISTING can be in, at the edge that produces it.
    ///
    /// The leg above pins the root itself being unopenable. This one pins the case #743 is about:
    /// the root opens, one directory BENEATH it does not. `SearchOption.AllDirectories` resolves to
    /// `EnumerationOptions.CompatibleRecursive`, whose `IgnoreInaccessible` is false, so that threw
    /// and #745 reported the whole root `Unreadable` — fail-closed, but it discards a listing that
    /// was 99% obtainable, and downstream that is indistinguishable from an empty root.
    ///
    /// Both halves are asserted together on purpose. Keeping the entries without naming the skip is
    /// the fail-OPEN (a truncated listing read as complete); naming the skip without keeping the
    /// entries is exactly the whole-root drift being fixed. Either alone would pass a test that
    /// asserted only the other.
    [<Fact>]
    let ``enumerate keeps what it could list and names the subdirectory it could not open`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()

            let write (path: string) =
                let full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

                Path.GetDirectoryName full
                |> Option.ofObj
                |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)

                File.WriteAllText(full, "body")

            write "tree/top.md"
            write "tree/keep/kept.md"
            write "tree/blocked/hidden.md"

            let blocked = Path.Combine(root, "tree", "blocked")
            File.SetUnixFileMode(blocked, enum<UnixFileMode> 0)

            try
                let result = interpret root (EnumerateDirectory "tree")

                match result.Read with
                | Truncated(snapshot, skipped) ->
                    // The listable entries survive — the sibling `keep/kept.md` and the parent's
                    // own `top.md`. `hidden.md` is genuinely unobserved and must not appear.
                    Assert.Equal("tree/keep/kept.md\ntree/top.md", snapshot.Text)

                    // …and the truncation is carried, naming the directory and the OS's reason.
                    Assert.Equal<string list>([ "tree/blocked" ], skipped |> List.map fst)
                    Assert.All(skipped, (fun (_, reason) -> Assert.False(System.String.IsNullOrWhiteSpace reason)))
                | other -> failwith $"expected Truncated for a listing with a mode-000 subdirectory, got {other}"

                // The listing reaches the folds that build candidate sets — #743 AC3 depends on
                // `Snapshot` being the partial listing rather than `None`.
                Assert.Equal(
                    Some "tree/keep/kept.md\ntree/top.md",
                    result.Snapshot |> Option.map (fun snapshot -> snapshot.Text)
                )

                // The READ succeeded at being a read (#754's rule, unchanged): the block belongs to
                // the verdict fold, so nothing here escalates and nothing here is a tool defect.
                Assert.True result.Succeeded

                match result.Diagnostic with
                | Some diagnostic ->
                    Assert.Equal("unlistableDirectory", diagnostic.Id)
                    Assert.False diagnostic.IsToolDefect
                    Assert.Equal<string list>([ "tree/blocked" ], diagnostic.RelatedIds)
                    Assert.Contains("tree/blocked", diagnostic.Message)
                | None -> failwith "expected an unlistableDirectory diagnostic"
            finally
                File.SetUnixFileMode(
                    blocked,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    /// Why `opens` performs a real `readdir` instead of testing mode bits or `Directory.Exists`.
    ///
    /// `IgnoreInaccessible = true` is silent: whatever the enumerator could not open it simply omits,
    /// and an omission reads exactly like an empty directory. The only sound way to name what was
    /// omitted is to ask the SAME question the enumerator asked, so the two can never disagree —
    /// and "readable" is not one question. Two directory modes break `read` and `traverse` apart,
    /// and each loses a different thing:
    ///
    ///   - `--x` — `readdir` fails, so its own files are omitted. The directory itself is still an
    ///     entry of its readable parent, so it is reached and named.
    ///   - `r--` — `readdir` SUCCEEDS, so its files are listed and its subdirectories are listed as
    ///     entries; what fails is descending, so the SUBDIRECTORY is what goes unobserved and what
    ///     must be named. Naming the parent here would be wrong: it was listed.
    ///
    /// A mode-bit test would have to reproduce that reasoning and would get it wrong in one of the
    /// two directions; `Directory.Exists` answers yes for both.
    [<Fact>]
    let ``a directory that is readable-but-not-traversable loses only its subdirectory, and says so`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()

            for directory in [ "tree/listable"; "tree/listable/deeper"; "tree/opaque" ] do
                Directory.CreateDirectory(Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar)))
                |> ignore

            File.WriteAllText(Path.Combine(root, "tree", "top.md"), "body")
            File.WriteAllText(Path.Combine(root, "tree", "listable", "seen.md"), "body")
            File.WriteAllText(Path.Combine(root, "tree", "opaque", "unseen.md"), "body")

            let listable = Path.Combine(root, "tree", "listable")
            let opaque = Path.Combine(root, "tree", "opaque")

            // r-- : its own entries list, its children cannot be entered.
            File.SetUnixFileMode(listable, UnixFileMode.UserRead)
            // --x : it can be entered, its own entries cannot be listed.
            File.SetUnixFileMode(opaque, UnixFileMode.UserExecute)

            try
                match (interpret root (EnumerateDirectory "tree")).Read with
                | Truncated(snapshot, skipped) ->
                    // `listable/seen.md` survives — `readdir` on an `r--` directory works, and
                    // dropping it would be the narrowed read set this whole item forbids.
                    // `opaque/unseen.md` genuinely could not be listed.
                    Assert.Equal("tree/listable/seen.md\ntree/top.md", snapshot.Text)

                    // Exactly what went unobserved, at the right level in each case.
                    Assert.Equal<string list>([ "tree/listable/deeper"; "tree/opaque" ], skipped |> List.map fst)
                | other -> failwith $"expected Truncated, got {other}"
            finally
                for directory in [ listable; opaque ] do
                    File.SetUnixFileMode(
                        directory,
                        UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                    )

    /// The choke point, asserted directly. `Foundation.unreadablePathsOf` is what every lane's
    /// verdict blocks on, so #743 AC2 reduces to this fold seeing the SKIPPED DIRECTORY — and not
    /// seeing the enumerated root, which was read perfectly well and whose permissions are fine.
    [<Fact>]
    let ``the coherence fold blocks on the skipped directory, not on the root that listed`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()
            let full = Path.Combine(root, "tree", "blocked")
            Directory.CreateDirectory full |> ignore
            File.WriteAllText(Path.Combine(root, "tree", "top.md"), "body")
            File.SetUnixFileMode(full, enum<UnixFileMode> 0)

            try
                let read = (interpret root (EnumerateDirectory "tree")).Read

                Assert.Equal<string list>(
                    [ "tree/blocked" ],
                    FS.GG.SDD.Commands.Internal.Foundation.unreadablePathsOf [ read ]
                )
            finally
                File.SetUnixFileMode(
                    full,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    /// #745 AC3 + AC5. The write edge pre-reads its destination because `canOverwrite` decides from
    /// that file's current bytes — so an unreadable destination makes the decision undecidable, and
    /// the fail-closed answer to an undecidable safety question is to refuse. It must refuse
    /// WITHOUT claiming a tool defect: this arm used to throw into the outer handler, so
    /// `upgrade --yes` and `charter` over a mode-000 target both exited 2.
    ///
    /// Paired deliberately with `a failed write leaves the prior bytes intact and no residue`
    /// above, which pins the OTHER half of AC5: a genuine write fault is still `toolDefect`.
    [<Fact>]
    let ``a write over an unreadable destination is refused, blocking but not a tool defect`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()
            seed root "the bytes that must survive"
            File.SetUnixFileMode(absolute root, enum<UnixFileMode> 0)

            try
                let result =
                    interpret root (WriteFile(relative, "never lands", HybridArtifact MergePolicies.specification))

                Assert.False result.Succeeded

                match result.Diagnostic with
                | Some diagnostic ->
                    Assert.Equal("unreadableWriteTarget", diagnostic.Id)
                    Assert.False diagnostic.IsToolDefect
                    Assert.Contains(relative, diagnostic.RelatedIds)
                | None -> failwith "expected an unreadableWriteTarget diagnostic"

                match result.Read with
                | Unreadable(path, _) -> Assert.Equal(relative, path)
                | other -> failwith $"expected Unreadable, got {other}"

                File.SetUnixFileMode(absolute root, enum<UnixFileMode> 0o644)
                Assert.Equal("the bytes that must survive", File.ReadAllText(absolute root))
                Assert.Empty(residue root)
            finally
                File.SetUnixFileMode(absolute root, enum<UnixFileMode> 0o644)
