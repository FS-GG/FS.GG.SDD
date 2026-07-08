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
    let private interpret root effect = CommandEffects.interpret root false effect

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

        let result = interpret root (WriteFile(relative, "created", AuthoredSource))

        Assert.True result.Succeeded
        Assert.Equal("created", File.ReadAllText(absolute root))
        Assert.Empty(residue root)

    [<Fact>]
    let ``replaces an existing file's content wholesale`` () =
        let root = TestSupport.tempDirectory ()
        seed root "old content that is longer than the new"

        let result = interpret root (WriteFile(relative, "new", AuthoredSource))

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

        let result = interpret root (WriteFile(relative, "same", AuthoredSource))

        Assert.True result.Succeeded
        Assert.Equal("same", File.ReadAllText(absolute root))
        Assert.Equal(stamped, File.GetLastWriteTimeUtc(absolute root))
        Assert.Empty(residue root)

    [<Fact>]
    let ``dryRun writes nothing at all - destination or temp`` () =
        let root = TestSupport.tempDirectory ()

        let result = CommandEffects.interpret root true (WriteFile(relative, "unwritten", AuthoredSource))

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

            let result = interpret root (WriteFile(relative, "after", AuthoredSource))

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
                let result = interpret root (WriteFile(relative, "never lands", AuthoredSource))

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
