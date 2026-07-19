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
        Assert.Equal("0.16.x", entry.SddVersionLine)
        Assert.False(String.IsNullOrWhiteSpace entry.SpecKitRange)

        // ...and the literal above is only half the guard. What makes a compatibility entry TRUE
        // is that it names THIS release's line: `0.10.x` on an 0.11.0 release is not stale, it is
        // a false compatibility claim, and a consumer resolving against it would take the wrong
        // range. The two facts are derived from one `Identity.Version` here so they cannot drift
        // apart the way the literal alone allowed (it survived the 0.10.0 -> 0.11.0 bump unchanged
        // and nothing failed until this line was added).
        let identityLine =
            let parts = release.Identity.Version.Split('.')
            $"{parts.[0]}.{parts.[1]}.x"

        Assert.Equal(identityLine, entry.SddVersionLine)

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

    // 0.11.0 is ADDITIVE, so it carries NO migration note (`migrationNoteRequired Additive =
    // false`). The obvious edit when 0.10.0's note came out was to swap `exactlyOne` for
    // `Assert.Empty` — and that would have SILENTLY DELETED the only guard in the repo that says
    // a note must be FOR this release and must EXIST ON DISK. Those checks were written against
    // `exactlyOne`, so they die with it, and nothing would notice until the next BREAKING release
    // shipped a note pointing at a file nobody wrote.
    //
    // So the well-formedness guard is stated as a PROPERTY over whatever `Migrations` holds. It is
    // vacuous today — that is the point: it costs nothing now and is already standing, unedited,
    // the moment a note comes back. A guard that has to be re-derived at exactly the moment it
    // first matters is a guard that is not there.
    /// The well-formedness obligation on a migration note, as a PREDICATE rather than a pile of
    /// asserts — so it can be run against a release that HAS one. `Assert.All` over this release's
    /// (empty) `Migrations` proves nothing, and a guard that is only ever evaluated vacuously is
    /// indistinguishable from a guard that is wrong.
    let private noteDefects (identityVersion: string) (note: MigrationNoteRef) =
        [
          // a note whose version drifts from identity advertises a migration that this artifact
          // does not describe.
          if note.Version <> identityVersion then
              $"version {note.Version} does not match the release identity {identityVersion}"

          if note.Path <> $"docs/release/migrations/{identityVersion}.md" then
              $"path {note.Path} is not the note path for {identityVersion}"

          // a note that enumerates nothing under-reports — the exact failure the obligation exists
          // to prevent.
          if List.isEmpty note.BreakingChanges then
              $"note {note.Path} enumerates no breaking changes"

          // the obligation is a FILE, not a claim.
          if not (File.Exists(Path.Combine(TestSupport.repoRoot, note.Path))) then
              $"note {note.Path} is referenced by release-readiness.json but absent from disk" ]

    [<Fact>]
    let ``T023 every migration note this release declares is for this release and exists on disk`` () =
        Assert.All(release.Migrations, fun note -> Assert.Empty(noteDefects release.Identity.Version note))

    // GUARD THE GUARD. The assertion above is vacuous while `Migrations` is empty, so on its own it
    // would happily still pass with a typo'd path format or a dropped disk check — and nobody would
    // find out until a BREAKING release shipped a note pointing at a file nobody wrote, which is the
    // precise moment the guard was supposed to matter. So the predicate is exercised here against a
    // release that DOES carry a note: 0.10.0, whose note is still on disk.
    [<Fact>]
    let ``T023 the note obligation ACCEPTS a well-formed note and NAMES each way one can be wrong`` () =
        let good: MigrationNoteRef =
            { Version = "0.10.0"
              Path = "docs/release/migrations/0.10.0.md"
              BreakingChanges = [ "the seven lifecycle artifacts now report kind hybridArtifact" ] }

        // the real, on-disk 0.10.0 note satisfies the obligation in full.
        Assert.Empty(noteDefects "0.10.0" good)

        // ...and each failure leg fires. A leg that never fails is how this defect class survives.
        Assert.NotEmpty(noteDefects "0.11.0" good) // version/path drift from identity
        Assert.NotEmpty(noteDefects "0.10.0" { good with BreakingChanges = [] }) // under-reports

        Assert.NotEmpty(
            noteDefects
                "9.9.9"
                { good with
                    Version = "9.9.9"
                    Path = "docs/release/migrations/9.9.9.md" } // names a file nobody wrote
        )

    // ...and the classification of THIS release, pinned separately, so the vacuous guard above is a
    // MEASURED verdict rather than a presence nobody accounted for. 0.15.0 is ADDITIVE: feature 105
    // (plan-time framework-API resolution) plus the evidence/lint/checklist fixes ADD public members
    // and remove none (the F# surface diff v0.14.0..HEAD is additive), no command or flag is removed,
    // and no exit-code contract changes — so `migrationNoteRequired Additive = false` and the release
    // carries no note. (0.14.0 was the BREAKING one — ADR-0035 stage 3b's observed-run default flip;
    // its note lives on disk at docs/release/migrations/0.14.0.md and is exercised by the on-disk
    // 0.10.0 predicate above, so dropping the note here does not silently delete the guard — the
    // lesson recorded in the US4 header.)
    [<Fact>]
    let ``T023 this additive release carries no migration note`` () =
        Assert.False(migrationNoteRequired Additive)

        // Additive-empty: an additive release is obliged to carry NO note, and this one carries none.
        Assert.Empty release.Migrations

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
