namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// Feature 105, Phase 2 (ADR-0004): the `dependency-surface` report is fact-identical across
/// `--json`, `--text`, and `--rich`; `--rich` redirected equals `--text` with zero ANSI; the rich
/// path changes no JSON byte. Constructed reports.
module DependencySurfaceProjectionTests =
    let private interactiveColor =
        { IsInteractive = true
          ColorEnabled = true
          Width = Some 100
          IsInputInteractive = true }

    let private nonInteractive =
        { interactiveColor with
            IsInteractive = false }

    let private summary: DependencySurfaceSummary =
        { BaselineRoot = "docs/dependency-surface"
          Mode = "check"
          CheckedCount = 2
          Entries =
            [ { PackageId = "Some.Pkg"
                Version = "1.2.0"
                Status = "drifted"
                CommittedSha256 = Some "aaaa"
                ObservedSha256 = Some "bbbb"
                ObservedSymbolCount = 42 }
              { PackageId = "Other.Pkg"
                Version = "3.0.0"
                Status = "unavailable"
                CommittedSha256 = Some "cccc"
                ObservedSha256 = None
                ObservedSymbolCount = 0 } ]
          DriftedPackages = [ "Some.Pkg@1.2.0" ]
          UnavailablePackages = [ "Other.Pkg@3.0.0" ]
          UpdatedPackages = []
          IsCoherent = false }

    let private report: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = DependencySurface
            Outcome = CommandOutcome.Blocked
            Specification = None
            DependencySurface = Some summary }

    [<Fact>]
    let ``dependency-surface json equals serializeReport and the rich path changes no byte`` () =
        let before = serializeReport report
        resolve Rich interactiveColor report |> ignore
        Assert.Equal(before, serializeReport report)
        Assert.Equal(serializeReport report, (resolve Json interactiveColor report).Text)

    [<Fact>]
    let ``dependency-surface facts appear in every projection and rich redirected is zero-ANSI text`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich interactiveColor report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("Some.Pkg", projection)
            Assert.Contains("Other.Pkg", projection)

        Assert.Contains("\"mode\": \"check\"", json)
        Assert.Contains("\"isCoherent\": false", json)
        Assert.Contains("\"status\": \"drifted\"", json)
        Assert.Contains("\"status\": \"unavailable\"", json)
        Assert.Contains("dependencySurfaceMode: check", text)
        Assert.Contains("dependencySurfaceCoherent: False", text)
        Assert.Contains("dependencySurfaceEntry: Some.Pkg@1.2.0=drifted (42 symbols)", text)
        Assert.Contains("dependencySurfaceDrifted: Some.Pkg@1.2.0", text)
        Assert.Contains("dependencySurfaceUnavailable: Other.Pkg@3.0.0", text)

        let redirected = resolve Rich nonInteractive report
        Assert.False redirected.UsedRichRendering
        Assert.Equal(renderText report, redirected.Text)
        Assert.DoesNotContain("[38;", redirected.Text)

    [<Fact>]
    let ``dependency-surface json is deterministic across renders`` () =
        Assert.Equal(serializeReport report, serializeReport report)
