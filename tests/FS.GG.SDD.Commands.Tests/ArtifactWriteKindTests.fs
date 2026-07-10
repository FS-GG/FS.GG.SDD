namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// FS.GG.SDD#308 (child of #304, Breakout1 feedback §2.2(a)). `ArtifactWriteKind` used to model the
/// regenerated projections `checklist.md` and `tasks.yml` as `AuthoredSource` — the same tag as the
/// genuinely-authored `spec.md` — and `canOverwrite` returned `true` for both that tag and
/// `GeneratedView`, so across the pair the tag guarded nothing. The only real no-clobber guard was the
/// `<!-- fsgg-sdd: unsafe-overwrite -->` sentinel, checked ad hoc in six workflow sites.
///
/// The model is now: the seven lifecycle artifacts are `HybridArtifact` — tool-owned regions their
/// stage re-derives, authored regions it preserves — and `AuthoredSource` is the strict case the tool
/// never writes. The interpreter refuses an `AuthoredSource` write, so a handler cannot obtain the
/// ability to clobber authored prose by mis-tagging one.
///
/// This does not change what the seven stages *do*; it makes the tag mean what it says, which is the
/// precondition for FS.GG.SDD#309 deriving the merge policy from it.
module ArtifactWriteKindTests =
    let private relative = "work/demo/spec.md"

    let private absolute root =
        Path.Combine(root, "work", "demo", "spec.md")

    let private seed root (text: string) =
        Directory.CreateDirectory(Path.Combine(root, "work", "demo")) |> ignore
        File.WriteAllText(absolute root, text)

    let private allKinds =
        [ AuthoredSource
          HybridArtifact
          StructuredSource
          GeneratedView
          AgentGuidanceTarget ]

    /// The two tool-owned kinds, and only those, may replace existing bytes.
    let private overwritable = [ HybridArtifact; GeneratedView ]

    let private write root kind text =
        CommandEffects.interpret root false (WriteFile(relative, text, kind))

    [<Fact>]
    let ``an absent destination is writable whatever the kind`` () =
        for kind in allKinds do
            let root = TestSupport.tempDirectory ()

            Assert.True((write root kind "created").Succeeded, writeKindValue kind)
            Assert.Equal("created", File.ReadAllText(absolute root))

    [<Fact>]
    let ``an identical rewrite is permitted whatever the kind`` () =
        for kind in allKinds do
            let root = TestSupport.tempDirectory ()
            seed root "same"

            Assert.True((write root kind "same").Succeeded, writeKindValue kind)

    /// The truth table this issue exists to establish. Before the fix, `AuthoredSource` sat on the
    /// permitted side alongside `GeneratedView`, so the tag distinguished nothing across that pair.
    [<Theory>]
    [<InlineData("authoredSource", false)>]
    [<InlineData("hybridArtifact", true)>]
    [<InlineData("structuredSource", false)>]
    [<InlineData("generatedView", true)>]
    [<InlineData("agentGuidance", false)>]
    let ``only the tool-owned kinds may overwrite differing content`` (kindValue: string) (expected: bool) =
        let kind =
            allKinds |> List.find (fun candidate -> writeKindValue candidate = kindValue)

        let root = TestSupport.tempDirectory ()
        seed root "authored by a human"

        let result = write root kind "regenerated"

        Assert.Equal(expected, result.Succeeded)
        Assert.Equal(List.contains kind overwritable, result.Succeeded)

        if expected then
            Assert.Equal("regenerated", File.ReadAllText(absolute root))
        else
            Assert.Equal("authored by a human", File.ReadAllText(absolute root))

            match result.Diagnostic with
            | Some diagnostic -> Assert.Equal("unsafeOverwrite", diagnostic.Id)
            | None -> failwith $"expected an unsafeOverwrite diagnostic for {kindValue}"

    /// The structural half of the invariant, in the spirit of ADR-0002: `AuthoredSource` is refused by
    /// the interpreter, so the only way a stage could clobber authored prose is by never tagging a write
    /// with it. That is a property of the source, not of any single run — a future handler that tags one
    /// would pass every behavioral test in this file and then block at runtime on a diagnostic whose
    /// remedy is "fix the tool". Catch it here instead.
    ///
    /// Deliberately shape-tolerant: it matches `…, AuthoredSource)` as the last argument of a `WriteFile`
    /// construction, not the exact typography of any current call site.
    [<Fact>]
    let ``no command plans a WriteFile tagged AuthoredSource`` () =
        let sources =
            Directory.EnumerateFiles(Path.Combine(TestSupport.repoRoot, "src"), "*.fs", SearchOption.AllDirectories)
            |> Seq.filter (fun path ->
                let normalized = path.Replace('\\', '/')
                not (normalized.Contains "/bin/" || normalized.Contains "/obj/"))

        let offenders =
            sources
            |> Seq.filter (fun path -> Regex.IsMatch(File.ReadAllText path, @"WriteFile\([^)]*,\s*AuthoredSource\s*\)"))
            |> Seq.map (fun path -> Path.GetRelativePath(TestSupport.repoRoot, path).Replace('\\', '/'))
            |> Seq.sort
            |> List.ofSeq

        Assert.True(
            List.isEmpty offenders,
            "AuthoredSource is the kind the tool never writes; the interpreter refuses it. "
            + $"Tag the write HybridArtifact if a merge step preserves the authored regions. Offenders: {offenders}"
        )

    /// The six Markdown/YAML lifecycle artifacts a `charter … tasks` walk produces, end to end. Each
    /// stage reports the change entry for its own artifact, so run the lifecycle in order and look for
    /// the path across the collected reports. (`evidence.yml` is the seventh; it needs an implemented
    /// work item to be authored against, and is covered by the source-scan invariant above.)
    [<Theory>]
    [<InlineData("work/demo/charter.md")>]
    [<InlineData("work/demo/spec.md")>]
    [<InlineData("work/demo/clarifications.md")>]
    [<InlineData("work/demo/checklist.md")>]
    [<InlineData("work/demo/plan.md")>]
    [<InlineData("work/demo/tasks.yml")>]
    let ``each authored lifecycle artifact is reported as a hybrid write`` (path: string) =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let reports =
            [ TestSupport.runCharter root "demo" "Demo"
              TestSupport.runSpecify root "demo" "Demo"
              TestSupport.runClarify root "demo" "Demo"
              TestSupport.runChecklist root "demo" "Demo"
              TestSupport.runPlan root "demo" "Demo"
              TestSupport.runTasks root "demo" "Demo" ]

        let change =
            reports
            |> List.collect (fun report -> report.ChangedArtifacts)
            |> List.tryFind (fun c -> c.Path = path)
            |> Option.defaultWith (fun () -> failwith $"Expected a changed-artifact entry for {path}.")

        Assert.Equal("hybridArtifact", change.Kind)
        Assert.Equal("hybrid", change.Ownership)
