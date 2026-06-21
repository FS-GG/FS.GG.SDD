namespace FS.GG.SDD.Commands.Tests

open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ReleaseContract
open Xunit

/// Determinism (FR-008 / SC-005): the release contract and the produced artifacts
/// are byte-stable over identical inputs, with no clock, duration, host path,
/// ordering nondeterminism, or ANSI styling.
module ReleaseDeterminismTests =
    let workId = "018-release-readiness"
    let title = "Release Readiness"

    [<Fact>]
    let ``T020 serializing the release contract twice is byte-identical`` () =
        let release = currentRelease ()
        Assert.Equal(serialize release, serialize release)

    [<Fact>]
    let ``T020 a produced artifact is byte-identical across two productions over an identical tree`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        let firstWorkModel = TestSupport.readRelative root $"readiness/{workId}/work-model.json"
        let firstHandoff = TestSupport.readRelative root $"readiness/{workId}/governance-handoff.json"

        TestSupport.runShip root workId title |> ignore
        let secondWorkModel = TestSupport.readRelative root $"readiness/{workId}/work-model.json"
        let secondHandoff = TestSupport.readRelative root $"readiness/{workId}/governance-handoff.json"

        Assert.Equal(firstWorkModel, secondWorkModel)
        Assert.Equal(firstHandoff, secondHandoff)

    [<Fact>]
    let ``T020 the release contract excludes clocks, host paths, and ANSI styling`` () =
        let json = serialize (currentRelease ())

        // no ANSI escape (ESC = U+001B)
        Assert.False(json.Contains(string '\u001b'), "release contract must carry no ANSI escape")
        // no absolute host path leakage
        Assert.DoesNotContain("/home/", json)
        Assert.DoesNotContain("/tmp/", json)
        Assert.DoesNotContain(":\\", json)
        // no ISO-8601 timestamp / wall-clock
        Assert.False(Regex.IsMatch(json, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}"), "release contract must carry no timestamp")
        // no duration field
        Assert.DoesNotContain("duration", json.ToLowerInvariant())
