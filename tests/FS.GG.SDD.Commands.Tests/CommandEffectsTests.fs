namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Runtime.InteropServices
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

    /// `canOverwrite` returns true for identical content, so the bytes *are* re-committed — the
    /// `ArtifactOperation.NoChange` classification lives in report assembly, not here. This pins the
    /// observable outcome (destination unchanged, nothing left over), not a write-skip we do not do.
    [<Fact>]
    let ``an identical-content write leaves the destination unchanged`` () =
        let root = TestSupport.tempDirectory ()
        seed root "same"

        let result = interpret root (WriteFile(relative, "same", AuthoredSource))

        Assert.True result.Succeeded
        Assert.Equal("same", File.ReadAllText(absolute root))
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

    /// Structural (FR-005/FR-006/FR-008): the interpreter must commit through a dot-prefixed sibling
    /// temp and an atomic rename — never a direct truncating write to the destination. Asserted against
    /// the source text, because "the bug is gone" is a property of the code, not of any single run: a
    /// future edit that reintroduces `File.WriteAllText(absolute, …)` would pass every behavioral test
    /// above and silently restore the torn-read window.
    [<Fact>]
    let ``the WriteFile interpreter commits through a temp sibling, not a direct truncate`` () =
        let source =
            TestSupport.readRelative TestSupport.repoRoot "src/FS.GG.SDD.Commands/CommandEffects.fs"

        Assert.Contains("writeFileAtomic absolute text", source)
        Assert.Contains("File.Move(temp, absolute, true)", source)
        // The temp is a dot-prefixed sibling: same directory ⇒ same volume ⇒ the rename is atomic,
        // and the leading `.` keeps it out of the `readiness/**` / `work/**` globs in the crash window.
        Assert.Contains("$\".{Path.GetFileName absolute}.{Guid.NewGuid():N}.tmp\"", source)
        Assert.DoesNotContain("File.WriteAllText(absolute, text)", source)
