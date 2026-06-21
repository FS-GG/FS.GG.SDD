namespace FS.GG.SDD.Artifacts.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ReleaseContract
open Xunit

module ReleaseContractTests =
    let release = currentRelease ()

    let baselinePath =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Artifacts.Tests", "baselines", "release-readiness.json")

    let publishedPath =
        Path.Combine(TestSupport.repoRoot, "docs", "release", "release-readiness.json")

    let directoryBuildPropsVersion () =
        let text = File.ReadAllText(Path.Combine(TestSupport.repoRoot, "Directory.Build.props"))
        let m = System.Text.RegularExpressions.Regex.Match(text, @"<Version>([^<]+)</Version>")
        m.Groups[1].Value.Trim()

    // ===== US1 — version identity + compatibility (T011) =====

    [<Fact>]
    let ``T011 release-readiness round-trips through ReleaseContract`` () =
        let json = serialize release

        match parse json with
        | Ok parsed -> Assert.Equal(json, serialize parsed)
        | Error message -> failwith $"parse failed: {message}"

    [<Fact>]
    let ``T011 identity version equals the single Directory.Build.props Version and the generator version`` () =
        Assert.Equal(directoryBuildPropsVersion (), release.Identity.Version)
        Assert.Equal(release.Identity.Version, release.GeneratorVersion.Version)

    [<Fact>]
    let ``T011 channel is derived from the version (major 0 implies preRelease)`` () =
        Assert.Equal(PreRelease, release.Identity.Channel)
        Assert.Equal(PreRelease, channelOfVersion "0.2.0")
        Assert.Equal(StableRelease, channelOfVersion "1.0.0")
        Assert.Equal(StableRelease, channelOfVersion "2.3.4")

    [<Fact>]
    let ``T011 the compatibility entry carries a Spec Kit range and tolerates a null Governance range`` () =
        let entry = List.exactlyOne release.Compatibility
        Assert.Equal("0.2.x", entry.SddVersionLine)
        Assert.False(String.IsNullOrWhiteSpace entry.SpecKitRange)

        // a null Governance range is valid and must round-trip and not block readiness
        let withoutGovernance =
            { release with
                Compatibility = [ { entry with GovernanceContractVersionRange = None } ] }

        match parse (serialize withoutGovernance) with
        | Ok parsed -> Assert.Equal(None, (List.exactlyOne parsed.Compatibility).GovernanceContractVersionRange)
        | Error message -> failwith message

    [<Fact>]
    let ``T011 cliCommandName and package ids identify all three packages`` () =
        Assert.Equal("fsgg-sdd", release.Identity.CliCommandName)
        Assert.Equal<string list>(
            [ "FS.GG.SDD.Artifacts"; "FS.GG.SDD.Commands"; "FS.GG.SDD.Cli" ],
            release.Identity.PackageIds)

    // ===== US1 — versioning policy (T012) =====

    [<Fact>]
    let ``T012 every change class maps to exactly one bump`` () =
        Assert.Equal("major", bumpRule Breaking)
        Assert.Equal("minor", bumpRule Additive)
        Assert.Equal("patch", bumpRule Clarifying)

    [<Fact>]
    let ``T012 the migration-note obligation matches Breaking implies required, additive implies none`` () =
        Assert.True(migrationNoteRequired Breaking)
        Assert.False(migrationNoteRequired Additive)
        Assert.False(migrationNoteRequired Clarifying)

    [<Fact>]
    let ``T012 the published versioning-policy doc agrees with the policy of record`` () =
        let doc =
            Path.Combine(TestSupport.repoRoot, "docs", "release", "versioning-policy.md")
            |> File.ReadAllText
            |> fun text -> text.ToLowerInvariant()

        // the doc is a projection: it must name each change class and its bump
        for token in [ "breaking"; "additive"; "clarifying"; "major"; "minor"; "patch" ] do
            Assert.Contains(token, doc)

    // ===== US2 — schema reference doc agrees with the contract (T016) =====

    [<Fact>]
    let ``T016 the schema-reference doc projects every catalogued contract (no drift)`` () =
        let doc =
            Path.Combine(TestSupport.repoRoot, "docs", "release", "schema-reference.md")
            |> File.ReadAllText

        // the doc is a projection of catalog[]; every contract must appear by its
        // stable identifying token (structured wins on any disagreement)
        let token (contract: string) =
            if contract.StartsWith "command-report" then "command-report"
            else contract.Split('/') |> Array.last

        for entry in release.Catalog do
            Assert.Contains(token entry.Contract, doc)

    // ===== US3 — golden baseline + published-artifact agreement (T017) =====

    [<Fact>]
    let ``T017 serialized release matches the locked golden baseline`` () =
        let baseline = File.ReadAllText(baselinePath).Replace("\r\n", "\n")
        let actual = (serialize release).Replace("\r\n", "\n")

        if baseline <> actual then
            failwith
                "release-readiness contract drifted from tests/FS.GG.SDD.Artifacts.Tests/baselines/release-readiness.json. \
                 Regenerate the baseline intentionally (with a version bump + migration note if breaking)."

        Assert.Equal(baseline, actual)

    [<Fact>]
    let ``T017 the published docs artifact matches the contract (projection cannot drift)`` () =
        let published = File.ReadAllText(publishedPath).Replace("\r\n", "\n")
        Assert.Equal((serialize release).Replace("\r\n", "\n"), published)

    // ===== US4 — migration-note obligation for this release (T023) =====

    [<Fact>]
    let ``T023 this additive release carries no migration note`` () =
        // 0.2.0 adds public surface but breaks no existing contract: additive ⇒ no note.
        Assert.False(migrationNoteRequired Additive)
        Assert.Empty release.Migrations

    [<Fact>]
    let ``T023 a breaking release is obliged to carry a migration note`` () =
        Assert.True(migrationNoteRequired Breaking)

        // a represented breaking release must have a MigrationNoteRef enumerating the change
        let breakingNote =
            { Version = "1.0.0"
              Path = "docs/release/migrations/1.0.0.md"
              BreakingChanges = [ "froze the work-model.json schema" ] }

        let breakingRelease = { release with Migrations = [ breakingNote ] }
        Assert.NotEmpty breakingRelease.Migrations
        Assert.All(breakingRelease.Migrations, fun note -> Assert.NotEmpty note.BreakingChanges)
