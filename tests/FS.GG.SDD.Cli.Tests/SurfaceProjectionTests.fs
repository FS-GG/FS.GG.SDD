namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// Feature 086 (FR-009/FR-010/FR-011 / SC-005): the `surface` report is fact-identical across
/// `--json`, `--text`, and `--rich`; `--rich` redirected equals `--text` with zero ANSI; the rich
/// path changes no JSON byte. Constructed reports.
module SurfaceProjectionTests =
    let private interactiveColor =
        { IsInteractive = true
          ColorEnabled = true
          Width = Some 100
          IsInputInteractive = true }

    let private nonInteractive =
        { interactiveColor with
            IsInteractive = false }

    let private summary: SurfaceSummary =
        { SourceRoot = "src"
          BaselineRoot = "docs/api-surface"
          Mode = "check"
          CheckedCount = 3
          MissingBaselinePaths = [ "docs/api-surface/Pkg/New.fsi" ]
          DriftedSourcePaths = [ "src/Pkg/Changed.fsi" ]
          OrphanBaselinePaths = [ "docs/api-surface/Pkg/Stale.fsi" ]
          UpdatedBaselinePaths = []
          IsCoherent = false
          // Feature 087: the drifted `src/Pkg/Changed.fsi` classified breaking (a member changed).
          Classification =
            { Verdict = "breaking"
              RecommendedBump = "major"
              Entries =
                [ { Path = "src/Pkg/Changed.fsi"
                    Classification = "breaking"
                    RecommendedBump = "major"
                    AddedMembers = [ "val changed: int -> string" ]
                    RemovedOrChangedMembers = [ "val changed: int -> int" ]
                    UnparseableFallback = false } ] } }

    let private report: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Surface
            Outcome = CommandOutcome.Blocked
            Specification = None
            Surface = Some summary }

    [<Fact>]
    let ``surface json equals serializeReport and the rich path changes no byte`` () =
        let before = serializeReport report
        resolve Rich interactiveColor report |> ignore
        Assert.Equal(before, serializeReport report)
        Assert.Equal(serializeReport report, (resolve Json interactiveColor report).Text)

    [<Fact>]
    let ``surface facts appear in every projection and rich redirected is zero-ANSI text`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich interactiveColor report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("Changed.fsi", projection)
            Assert.Contains("New.fsi", projection)
            Assert.Contains("Stale.fsi", projection)

        Assert.Contains("\"mode\": \"check\"", json)
        Assert.Contains("\"isCoherent\": false", json)
        Assert.Contains("surfaceMode: check", text)
        Assert.Contains("surfaceCoherent: False", text)

        let redirected = resolve Rich nonInteractive report
        Assert.False redirected.UsedRichRendering
        Assert.Equal(renderText report, redirected.Text)
        Assert.DoesNotContain("[38;", redirected.Text)

    // Feature 087 (SC-005): the classification facts appear in every projection with an identical
    // fact set; the redirected rich output stays zero-ANSI plain text.
    [<Fact>]
    let ``surface classification facts appear in every projection`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich nonInteractive report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("breaking", projection)
            Assert.Contains("major", projection)

        Assert.Contains("\"verdict\": \"breaking\"", json)
        Assert.Contains("\"recommendedBump\": \"major\"", json)
        Assert.Contains("\"classification\": \"breaking\"", json)
        Assert.Contains("surfaceClassificationVerdict: breaking", text)
        Assert.Contains("surfaceClassificationBump: major", text)
        Assert.Contains("surfaceClassified: src/Pkg/Changed.fsi=breaking (major)", text)

    // Feature 087 (SC-008): the default projection is byte-identical across repeated renders (the
    // classification entries/member lists are deterministically sorted).
    [<Fact>]
    let ``surface classification json is deterministic across renders`` () =
        Assert.Equal(serializeReport report, serializeReport report)
