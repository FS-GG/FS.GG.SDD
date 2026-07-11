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

    // The single <Version> source of truth lives in the repo-owned
    // Directory.Build.local.props (feature 037: org shared-build-config moved
    // repo-specific MSBuild properties out of the canonical Directory.Build.props,
    // which is now synced byte-identically from FS-GG/.github and carries no
    // <Version>). The canonical file imports this local override last.
    let directoryBuildPropsVersion () =
        let text =
            File.ReadAllText(Path.Combine(TestSupport.repoRoot, "Directory.Build.local.props"))

        let m =
            System.Text.RegularExpressions.Regex.Match(text, @"<Version>([^<]+)</Version>")

        m.Groups[1].Value.Trim()

    // ===== US1 — version identity + compatibility (T011) =====

    [<Fact>]
    let ``T011 release-readiness round-trips through ReleaseContract`` () =
        let json = serialize release

        match parse json with
        | Ok parsed -> Assert.Equal(json, serialize parsed)
        | Error message -> failwith $"parse failed: {message}"

    // ===== Feature 092 (ADR-0026) — the durableGenerated marker =====

    [<Fact>]
    let ``the ship verdict is the only durableGenerated catalog entry`` () =
        let durable =
            release.Catalog
            |> List.filter (fun entry -> entry.DurableGenerated)
            |> List.map (fun entry -> entry.Contract)

        Assert.Equal<string list>([ "ship-verdict.json" ], durable)

    [<Fact>]
    let ``the ship verdict entry is a generatedView with no cross-repo contractVersion`` () =
        let entry =
            release.Catalog |> List.find (fun entry -> entry.Contract = "ship-verdict.json")

        Assert.Equal(ArtifactRef.GeneratedView, entry.SourceArtifact.Kind)
        Assert.Equal("readiness/<id>/ship-verdict.json", entry.SourceArtifact.Path)
        Assert.Equal(AdditiveOptional, entry.Stability)
        Assert.Equal(None, entry.ContractVersion) // not a cross-repo contract (FR-018)
        Assert.Equal(GeneratedViewContract(GenerationManifest.ShipVerdict, Json), entry.Kind)

    [<Fact>]
    let ``durableGenerated round-trips through serialize and parse`` () =
        match parse (serialize release) with
        | Error message -> failwith $"parse failed: {message}"
        | Ok parsed ->
            for entry in parsed.Catalog do
                let original = release.Catalog |> List.find (fun e -> e.Contract = entry.Contract)

                Assert.Equal(original.DurableGenerated, entry.DurableGenerated)

    [<Fact>]
    let ``a catalog entry without durableGenerated parses as regenerable`` () =
        // Absence means what every pre-092 catalog meant. Guards the tolerant parse against a
        // pre-092 release-readiness.json, which carries no such field at all.
        let stripped =
            System.Text.RegularExpressions.Regex.Replace(
                serialize release,
                ",\\s*\"durableGenerated\": (true|false)",
                ""
            )

        Assert.DoesNotContain("durableGenerated", stripped)

        match parse stripped with
        | Error message -> failwith $"parse failed: {message}"
        | Ok parsed -> Assert.Empty(parsed.Catalog |> List.filter (fun entry -> entry.DurableGenerated))

    [<Fact>]
    let ``T011 identity version equals the single Directory.Build.local.props Version and the generator version`` () =
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
        Assert.Equal("0.10.x", entry.SddVersionLine)
        Assert.False(String.IsNullOrWhiteSpace entry.SpecKitRange)

        // a null Governance range is valid and must round-trip and not block readiness
        let withoutGovernance =
            { release with
                Compatibility =
                    [ { entry with
                          GovernanceContractVersionRange = None } ] }

        match parse (serialize withoutGovernance) with
        | Ok parsed -> Assert.Equal(None, (List.exactlyOne parsed.Compatibility).GovernanceContractVersionRange)
        | Error message -> failwith message

    [<Fact>]
    let ``T011 cliCommandName and package ids identify all three packages`` () =
        Assert.Equal("fsgg-sdd", release.Identity.CliCommandName)

        Assert.Equal<string list>(
            [ "FS.GG.SDD.Artifacts"; "FS.GG.SDD.Commands"; "FS.GG.SDD.Cli" ],
            release.Identity.PackageIds
        )

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
            if contract.StartsWith "command-report" then
                "command-report"
            else
                contract.Split('/') |> Array.last

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
    let ``T023 this breaking release carries the obliged migration note`` () =
        // An additive release still carries no note — the policy is unchanged.
        Assert.False(migrationNoteRequired Additive)

        // 0.10.0 relabels the seven lifecycle artifacts in the `--json` command-report
        // (`authoredSource`/`authored` → `hybridArtifact`/`hybrid`, feature 312) and folds
        // the derived task graph from 2n to n tasks (feature 319) — Breaking ⇒ a note is
        // owed (FS-GG/FS.GG.SDD#190). Pre-1.0 it rides a minor bump; the note is not optional.
        Assert.True(migrationNoteRequired Breaking)

        let note = List.exactlyOne release.Migrations

        // the note is *for this release*: a note whose version drifts from identity
        // would advertise a migration that this artifact does not describe.
        Assert.Equal(release.Identity.Version, note.Version)
        Assert.Equal($"docs/release/migrations/{release.Identity.Version}.md", note.Path)
        Assert.NotEmpty note.BreakingChanges

        // the referenced note must actually exist — the obligation is a file, not a claim.
        Assert.True(
            File.Exists(Path.Combine(TestSupport.repoRoot, note.Path)),
            $"migration note {note.Path} is referenced by release-readiness.json but absent from disk"
        )

        // and it must name a stable marker of a 0.10.0 breaking change, so a consumer can grep for it.
        Assert.Contains(note.BreakingChanges, fun change -> change.Contains "hybridArtifact")

    [<Fact>]
    let ``T023 a breaking release is obliged to carry a migration note`` () =
        Assert.True(migrationNoteRequired Breaking)

        // a represented breaking release must have a MigrationNoteRef enumerating the change
        let breakingNote =
            { Version = "1.0.0"
              Path = "docs/release/migrations/1.0.0.md"
              BreakingChanges = [ "froze the work-model.json schema" ] }

        let breakingRelease =
            { release with
                Migrations = [ breakingNote ] }

        Assert.NotEmpty breakingRelease.Migrations
        Assert.All(breakingRelease.Migrations, fun note -> Assert.NotEmpty note.BreakingChanges)
