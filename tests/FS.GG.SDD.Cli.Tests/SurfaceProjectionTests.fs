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
                    UnparseableFallback = false } ] }
          // Feature 094: the breaking verdict implies a major bump off the resolved axis.
          VersionBump =
            { AxisFile = "Directory.Build.props"
              AxisProperty = "Version"
              AxisState = "resolved"
              CurrentVersion = Some "0.8.0"
              RequiredBump = "major"
              SuggestedVersion = Some "1.0.0" } }

    let private report: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Surface
            Outcome = CommandOutcome.Blocked
            Specification = None
            Surface = Some summary }

    /// The same report with an unresolvable axis — the two optional scalars must project as explicit
    /// `null` (json) and `(none)` (text), never as an omitted key or an empty string.
    let private unresolvedReport: CommandReport =
        { report with
            Surface =
                Some
                    { summary with
                        VersionBump =
                            { summary.VersionBump with
                                AxisState = "undeterminable"
                                CurrentVersion = None
                                SuggestedVersion = None } } }

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

    // ---- Feature 094: the version-bump prompt across the three projections (V20, V21) -----------

    /// V20: the `versionBump` object carries a stable key set in json, five flat `key: value` lines
    /// in text, and rich — which auto-derives from those lines — degrades to byte-identical text.
    [<Fact>]
    let ``surface versionBump facts appear in every projection`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich nonInteractive report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("0.8.0", projection)
            Assert.Contains("1.0.0", projection)

        Assert.Contains("\"versionBump\": {", json)
        Assert.Contains("\"axisFile\": \"Directory.Build.props\"", json)
        Assert.Contains("\"axisProperty\": \"Version\"", json)
        Assert.Contains("\"axisState\": \"resolved\"", json)
        Assert.Contains("\"currentVersion\": \"0.8.0\"", json)
        Assert.Contains("\"requiredBump\": \"major\"", json)
        Assert.Contains("\"suggestedVersion\": \"1.0.0\"", json)

        Assert.Contains("surfaceVersionAxis: Directory.Build.props:Version", text)
        Assert.Contains("surfaceVersionAxisState: resolved", text)
        Assert.Contains("surfaceVersionCurrent: 0.8.0", text)
        Assert.Contains("surfaceVersionRequiredBump: major", text)
        Assert.Contains("surfaceVersionSuggested: 1.0.0", text)

        // Rich, redirected, is the text projection with zero ANSI — no bespoke rich block exists.
        let redirected = resolve Rich nonInteractive report
        Assert.False redirected.UsedRichRendering
        Assert.Equal(renderText report, redirected.Text)
        Assert.DoesNotContain("[38;", redirected.Text)

    /// FR-007: an unresolved axis projects as explicit `null` / `(none)` — the key is never omitted,
    /// and the report never asserts a version it did not read.
    [<Fact>]
    let ``an unresolved axis projects as explicit null and (none), never an omitted key`` () =
        let json = (resolve Json interactiveColor unresolvedReport).Text
        let text = (resolve Text nonInteractive unresolvedReport).Text

        Assert.Contains("\"currentVersion\": null", json)
        Assert.Contains("\"suggestedVersion\": null", json)
        Assert.Contains("\"axisState\": \"undeterminable\"", json)
        // The required bump still lands — it depends on the classification, not the axis (I1).
        Assert.Contains("\"requiredBump\": \"major\"", json)

        Assert.Contains("surfaceVersionAxisState: undeterminable", text)
        Assert.Contains("surfaceVersionCurrent: (none)", text)
        Assert.Contains("surfaceVersionSuggested: (none)", text)
        Assert.Contains("surfaceVersionRequiredBump: major", text)

    /// V21 (FR-014 / SC-006): two renders of the same fixture are byte-identical in `--json` and
    /// `--text`, in both the resolved and the unresolved axis state.
    [<Fact>]
    let ``surface versionBump json and text are byte-identical across renders`` () =
        for candidate in [ report; unresolvedReport ] do
            Assert.Equal(serializeReport candidate, serializeReport candidate)

            Assert.Equal((resolve Text nonInteractive candidate).Text, (resolve Text nonInteractive candidate).Text)
