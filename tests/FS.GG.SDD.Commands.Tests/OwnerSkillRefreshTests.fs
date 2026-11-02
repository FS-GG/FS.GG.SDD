namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// FS.GG.SDD#1155: `fsgg-sdd upgrade` replaces an unedited owner-sourced skill (driver or product)
/// as one unit when it changed upstream, and never otherwise. One test per safety condition.
///
/// The "changed upstream" workspaces are real: a coherent scaffold fixture whose skill files and
/// recorded digests are aged to an older body, so the CLI's genuinely embedded pack is the newer
/// one. Nothing here substitutes the production decision.
module OwnerSkillRefreshTests =
    open RemediationSupport

    let private skillId = "work-roadmap"
    let private provenanceFile = ".fsgg/scaffold-provenance.json"
    let private retiredRelative = "references/retired-upstream.md"

    let private embeddedRecord () =
        // The record a coherent current-generator scaffold fixture carries.
        let root = coherentFixture ()

        let record =
            tryParse (File.ReadAllText(Path.Combine(root, provenanceFile))) |> Option.get

        root, record

    let private skillRows (record: ScaffoldProvenanceRecord) =
        record.DriverPaths
        |> List.filter (fun row ->
            OwnerSkillRefresh.skillCopyOf row.Path
            |> Option.exists (fun (_, id, _) -> id = skillId))

    // Identical in every root for a given file, as a real scaffold's mirrored copies are: the roots
    // must agree for cross-root identity to hold.
    let private oldBody (path: string) =
        $"OLD UPSTREAM BODY of {(OwnerSkillRefresh.skillCopyOf path
                                 |> Option.get
                                 |> fun (_, _, relative) -> relative)}\n"

    let private embeddedPackage () =
        OwnerPackages.embeddedFor ArtifactOwner.Driver |> Option.get

    /// Age the skill to an older upstream: every file on disk and every recorded digest carries the
    /// old body, plus one file the current pack no longer ships. Returns the workspace root and the
    /// paths involved.
    let private aged (generator: string) (versions: OwnerSkillVersion list) =
        let root, record = embeddedRecord ()
        let rows = skillRows record
        Assert.NotEmpty rows

        let retired =
            Fsgg.Schemas.agentSkillRoots
            |> List.map (fun skillRoot -> $"{skillRoot}/skills/{skillId}/{retiredRelative}")

        let agedRows =
            (rows |> List.map (fun row -> row.Path)) @ retired
            |> List.map (fun path ->
                TestSupport.writeRelative root path (oldBody path)

                {
                    Path = path
                    Owner = ArtifactOwner.Driver
                    Sha256 = Some(Fsgg.SkillMirror.sha256 (oldBody path))
                })

        let agedPaths = agedRows |> List.map (fun row -> row.Path) |> Set.ofList

        let updated =
            { record with
                Generator =
                    { record.Generator with
                        Version = generator
                    }
                DriverPaths =
                    (record.DriverPaths |> List.filter (fun row -> not (agedPaths.Contains row.Path)))
                    @ agedRows
                OwnerSkillVersions = versions
            }

        TestSupport.writeRelative root provenanceFile (serialize updated)
        root, rows |> List.map (fun row -> row.Path), retired

    let private recordOf root =
        tryParse (File.ReadAllText(Path.Combine(root, provenanceFile))) |> Option.get

    let private hint root =
        (upgradeYes root).Upgrade.Value.NextActionHint

    let private read root path =
        File.ReadAllText(Path.Combine(root, path))

    let private exists root path = File.Exists(Path.Combine(root, path))

    let private desiredOf (record: ScaffoldProvenanceRecord) =
        OwnerSkillRefresh.embeddedDesired record
        |> List.filter (fun file ->
            OwnerSkillRefresh.skillCopyOf file.Path
            |> Option.exists (fun (_, id, _) -> id = skillId))

    // ---------- replacement ----------

    [<Fact>]
    let ``an unedited skill that changed upstream is replaced as one unit in every root`` () =
        let root, paths, _ = aged "0.1.0" []
        let desired = desiredOf (recordOf root)
        Assert.NotEmpty desired

        let report = upgradeYes root

        Assert.False(
            report.Upgrade.Value.ResidualDrift,
            sprintf
                "the refresh must close as converged: %s | drift=%A | diags=%A"
                report.Upgrade.Value.NextActionHint
                report.Upgrade.Value.SkillDriftPaths
                (report.Diagnostics |> List.map (fun d -> d.Id, d.Message))
        )

        for file in desired do
            Assert.Equal(file.Text, read root file.Path)

        // both roots, not one: the `.claude` and `.agents` copies are one unit
        for path in paths do
            let expected = (desired |> List.find (fun file -> file.Path = path)).Text
            Assert.Equal(expected, read root path)

    [<Fact>]
    let ``replacing a skill records the new digests and the package version it came from`` () =
        let root, _, _ = aged "0.1.0" []
        upgradeYes root |> ignore
        let record = recordOf root
        let desired = desiredOf record

        for file in desired do
            let row = record.DriverPaths |> List.find (fun row -> row.Path = file.Path)
            Assert.Equal(Some file.Sha256, row.Sha256)
            Assert.Equal(Some(Fsgg.SkillMirror.sha256 (read root file.Path)), row.Sha256)

        let package, version = embeddedPackage ()

        let entry =
            record.OwnerSkillVersions |> List.find (fun entry -> entry.SkillId = skillId)

        Assert.Equal(package, entry.Package)
        Assert.Equal(version, entry.Version)

    [<Fact>]
    let ``a second upgrade after a refresh changes nothing`` () =
        let root, _, _ = aged "0.1.0" []
        upgradeYes root |> ignore
        let before = read root provenanceFile
        let second = (upgradeYes root).Upgrade.Value
        Assert.True second.AlreadyCoherent
        Assert.Equal(before, read root provenanceFile)

    [<Fact>]
    let ``a refreshed executable file keeps its executable bit`` () =
        let root, record = embeddedRecord ()

        let executable =
            OwnerSkillRefresh.embeddedDesired record
            |> List.tryFind (fun file -> file.Executable && file.Owner = ArtifactOwner.Driver)

        match executable with
        | None -> () // this pinned pack ships no executable driver file; nothing to assert
        | Some file ->
            let id =
                OwnerSkillRefresh.skillCopyOf file.Path
                |> Option.map (fun (_, id, _) -> id)
                |> Option.get

            let rows =
                record.DriverPaths
                |> List.filter (fun row ->
                    OwnerSkillRefresh.skillCopyOf row.Path
                    |> Option.exists (fun (_, i, _) -> i = id))

            for row in rows do
                TestSupport.writeRelative root row.Path (oldBody row.Path)

            let agedRows =
                rows
                |> List.map (fun row ->
                    { row with
                        Sha256 = Some(Fsgg.SkillMirror.sha256 (oldBody row.Path))
                    })

            let agedPaths = agedRows |> List.map (fun row -> row.Path) |> Set.ofList

            let updated =
                { record with
                    Generator =
                        { record.Generator with
                            Version = "0.1.0"
                        }
                    DriverPaths =
                        (record.DriverPaths |> List.filter (fun row -> not (agedPaths.Contains row.Path)))
                        @ agedRows
                }

            TestSupport.writeRelative root provenanceFile (serialize updated)
            upgradeYes root |> ignore

            if not (System.OperatingSystem.IsWindows()) then
                let mode = File.GetUnixFileMode(Path.Combine(root, file.Path))
                Assert.True(mode.HasFlag UnixFileMode.UserExecute)

    // ---------- locally edited skills keep today's behavior and warning ----------

    [<Fact>]
    let ``a skill edited in one root is preserved in every root and keeps the drift warning`` () =
        let root, paths, _ = aged "0.1.0" []
        let edited = paths |> List.find (fun path -> path.StartsWith ".agents/")
        TestSupport.writeRelative root edited (oldBody edited + "my local note\n")

        let summary = (upgradeYes root).Upgrade.Value

        // the edit survives, and so does the untouched sibling: one unit, held as one unit
        Assert.Equal(oldBody edited + "my local note\n", read root edited)

        for path in paths |> List.filter ((<>) edited) do
            Assert.Equal(oldBody path, read root path)

        // today's warning for a locally edited owner skill is the content-drift advisory
        Assert.Contains("edited locally", summary.NextActionHint)
        Assert.Contains("Skill content drift detected", summary.NextActionHint)

    [<Fact>]
    let ``an edit confined to the other root also holds the whole unit`` () =
        let root, paths, _ = aged "0.1.0" []
        let edited = paths |> List.find (fun path -> path.StartsWith ".claude/")
        TestSupport.writeRelative root edited "hand written\n"

        upgradeYes root |> ignore

        for path in paths |> List.filter ((<>) edited) do
            Assert.Equal(oldBody path, read root path)

    [<Fact>]
    let ``a recorded file without a digest counts as edited`` () =
        let root, paths, _ = aged "0.1.0" []
        let record = recordOf root
        let victim = List.head paths

        let blanked =
            { record with
                DriverPaths =
                    record.DriverPaths
                    |> List.map (fun row ->
                        if row.Path = victim then
                            { row with Sha256 = None }
                        else
                            row)
            }

        TestSupport.writeRelative root provenanceFile (serialize blanked)
        upgradeYes root |> ignore

        for path in paths do
            Assert.Equal(oldBody path, read root path)

    [<Fact>]
    let ``a held skill is not backfilled and does not fail the rest of the upgrade`` () =
        let root, paths, _ = aged "0.1.0" []

        let edited =
            paths
            |> List.find (fun path -> path.EndsWith "/SKILL.md" && path.StartsWith ".agents/")

        TestSupport.writeRelative root edited (oldBody edited + "mine\n")

        // a recorded file is missing from the held skill as well
        let missing =
            paths
            |> List.find (fun path -> path.Contains "/references/" && path.StartsWith ".claude/")

        File.Delete(Path.Combine(root, missing))

        let summary = (upgradeYes root).Upgrade.Value

        Assert.DoesNotContain(ReconciliationStepId.ArtifactReSeed, summary.FailedStepIds)
        Assert.False(exists root missing, "a missing file of a held skill must not be recreated from the newer pack")
        Assert.Equal(oldBody edited + "mine\n", read root edited)

    // ---------- never downgrade ----------

    [<Fact>]
    let ``an older embedded pack never replaces a newer materialized one`` () =
        let package, _ = embeddedPackage ()

        let root, paths, _ =
            aged
                "0.1.0"
                [
                    {
                        SkillId = skillId
                        Package = package
                        Version = "999.0.0"
                    }
                ]

        let summary = (upgradeYes root).Upgrade.Value

        for path in paths do
            Assert.Equal(oldBody path, read root path)

        Assert.Contains("older than the 999.0.0 already materialized", summary.NextActionHint)

        Assert.Equal(
            "999.0.0",
            (recordOf root).OwnerSkillVersions
            |> List.find (fun e -> e.SkillId = skillId)
            |> fun e -> e.Version
        )

    [<Fact>]
    let ``the same package version with different bytes is not replaced`` () =
        let package, version = embeddedPackage ()

        let root, paths, _ =
            aged
                "0.1.0"
                [
                    {
                        SkillId = skillId
                        Package = package
                        Version = version
                    }
                ]

        upgradeYes root |> ignore

        for path in paths do
            Assert.Equal(oldBody path, read root path)

    [<Fact>]
    let ``a strictly older recorded version is replaced`` () =
        let package, _ = embeddedPackage ()

        let root, paths, _ =
            aged
                "0.1.0"
                [
                    {
                        SkillId = skillId
                        Package = package
                        Version = "0.0.1"
                    }
                ]

        upgradeYes root |> ignore
        let desired = desiredOf (recordOf root)

        for file in desired do
            Assert.Equal(file.Text, read root file.Path)

        Assert.NotEmpty paths

    [<Fact>]
    let ``a record without versions is not replaced by a CLI that is not newer than the one that wrote it`` () =
        let root, paths, _ = aged "999.0.0" []
        let summary = (upgradeYes root).Upgrade.Value

        for path in paths do
            Assert.Equal(oldBody path, read root path)

        Assert.Contains("newer-ness unprovable", summary.NextActionHint)

    [<Fact>]
    let ``a version recorded for a different package is not trusted`` () =
        let root, paths, _ =
            aged
                "0.1.0"
                [
                    {
                        SkillId = skillId
                        Package = "Some.Other.Package"
                        Version = "0.0.1"
                    }
                ]

        upgradeYes root |> ignore

        for path in paths do
            Assert.Equal(oldBody path, read root path)

    // ---------- files that disappear from the manifest ----------

    [<Fact>]
    let ``a file the new pack no longer ships is removed with the unit and its row dropped`` () =
        let root, _, retired = aged "0.1.0" []

        for path in retired do
            Assert.True(exists root path)

        upgradeYes root |> ignore

        for path in retired do
            Assert.False(exists root path, $"{path} should have been removed")

        let record = recordOf root

        for path in retired do
            Assert.DoesNotContain(record.DriverPaths, fun row -> row.Path = path)

    [<Fact>]
    let ``an edited file the new pack dropped holds the whole skill`` () =
        let root, paths, retired = aged "0.1.0" []
        let edited = List.head retired
        TestSupport.writeRelative root edited "I rely on this file\n"

        upgradeYes root |> ignore

        Assert.Equal("I rely on this file\n", read root edited)

        for path in paths do
            Assert.Equal(oldBody path, read root path)

    [<Fact>]
    let ``a dropped file that is already gone only loses its row`` () =
        let root, _, retired = aged "0.1.0" []

        for path in retired do
            File.Delete(Path.Combine(root, path))

        upgradeYes root |> ignore
        let record = recordOf root

        for path in retired do
            Assert.DoesNotContain(record.DriverPaths, fun row -> row.Path = path)

    [<Fact>]
    let ``an unrecorded file already at a path the new pack wants holds the skill`` () =
        let root, record = embeddedRecord ()
        let desired = desiredOf record |> List.head
        let rows = skillRows record

        // age every recorded file, but leave the wanted path unrecorded with foreign content
        let others = rows |> List.filter (fun row -> row.Path <> desired.Path)

        for row in others do
            TestSupport.writeRelative root row.Path (oldBody row.Path)

        TestSupport.writeRelative root desired.Path "foreign\n"

        let agedRows =
            others
            |> List.map (fun row ->
                { row with
                    Sha256 = Some(Fsgg.SkillMirror.sha256 (oldBody row.Path))
                })

        let updated =
            { record with
                Generator =
                    { record.Generator with
                        Version = "0.1.0"
                    }
                DriverPaths =
                    (record.DriverPaths
                     |> List.filter (fun row ->
                         OwnerSkillRefresh.skillCopyOf row.Path
                         |> Option.exists (fun (_, id, _) -> id <> skillId)))
                    @ agedRows
            }

        TestSupport.writeRelative root provenanceFile (serialize updated)
        upgradeYes root |> ignore

        Assert.Equal("foreign\n", read root desired.Path)

    [<Fact>]
    let ``a skill the pack no longer ships at all is never deleted`` () =
        let root, record = embeddedRecord ()

        let stray =
            Fsgg.Schemas.agentSkillRoots
            |> List.map (fun skillRoot -> $"{skillRoot}/skills/retired-skill/SKILL.md")

        let rows =
            stray
            |> List.map (fun path ->
                TestSupport.writeRelative root path "retired\n"

                {
                    Path = path
                    Owner = ArtifactOwner.Driver
                    Sha256 = Some(Fsgg.SkillMirror.sha256 "retired\n")
                })

        TestSupport.writeRelative
            root
            provenanceFile
            (serialize
                { record with
                    DriverPaths = record.DriverPaths @ rows
                })

        upgradeYes root |> ignore

        for path in stray do
            Assert.True(exists root path)

    // ---------- the write edge re-checks what the planner read ----------

    [<Fact>]
    let ``the write edge refuses to replace a file whose digest moved after planning`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".agents/skills/x/SKILL.md" "edited since the plan\n"
        let recorded = Fsgg.SkillMirror.sha256 "what provenance attested\n"

        let result =
            CommandEffects.interpret
                root
                false
                (WriteFile(".agents/skills/x/SKILL.md", "new\n", OwnerSkillRefresh recorded))

        Assert.False result.Succeeded
        Assert.Equal("edited since the plan\n", read root ".agents/skills/x/SKILL.md")

    [<Fact>]
    let ``the write edge replaces a file that still matches the recorded digest`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".agents/skills/x/SKILL.md" "as attested\n"
        let recorded = Fsgg.SkillMirror.sha256 "as attested\n"

        let result =
            CommandEffects.interpret
                root
                false
                (WriteFile(".agents/skills/x/SKILL.md", "new\n", OwnerSkillRefresh recorded))

        Assert.True result.Succeeded
        Assert.Equal("new\n", read root ".agents/skills/x/SKILL.md")

    [<Fact>]
    let ``the write edge never overwrites an existing file when no digest was attested`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".agents/skills/x/SKILL.md" "mine\n"

        let result =
            CommandEffects.interpret root false (WriteFile(".agents/skills/x/SKILL.md", "new\n", OwnerSkillRefresh ""))

        Assert.False result.Succeeded
        Assert.Equal("mine\n", read root ".agents/skills/x/SKILL.md")

    [<Fact>]
    let ``removal refuses an edited file and prunes the directories it empties`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.writeRelative root ".agents/skills/x/references/a.md" "attested\n"
        TestSupport.writeRelative root ".agents/skills/x/references/b.md" "edited\n"
        let attested = Fsgg.SkillMirror.sha256 "attested\n"

        let refused =
            CommandEffects.interpret root false (RemoveFile(".agents/skills/x/references/b.md", attested))

        Assert.False refused.Succeeded
        Assert.True(exists root ".agents/skills/x/references/b.md")

        let removed =
            CommandEffects.interpret root false (RemoveFile(".agents/skills/x/references/a.md", attested))

        Assert.True removed.Succeeded
        Assert.False(exists root ".agents/skills/x/references/a.md")

        let last = Fsgg.SkillMirror.sha256 "edited\n"

        CommandEffects.interpret root false (RemoveFile(".agents/skills/x/references/b.md", last))
        |> ignore

        Assert.False(Directory.Exists(Path.Combine(root, ".agents/skills/x")))

        Assert.True(
            Directory.Exists(Path.Combine(root, ".agents/skills"))
            || not (Directory.Exists(Path.Combine(root, ".agents/skills/x")))
        )

    [<Fact>]
    let ``removal of an absent file succeeds and a dry run removes nothing`` () =
        let root = TestSupport.tempDirectory ()
        Assert.True((CommandEffects.interpret root false (RemoveFile(".agents/skills/x/a.md", "abc"))).Succeeded)
        TestSupport.writeRelative root ".agents/skills/x/a.md" "keep\n"

        let dry =
            CommandEffects.interpret root true (RemoveFile(".agents/skills/x/a.md", Fsgg.SkillMirror.sha256 "keep\n"))

        Assert.True dry.Succeeded
        Assert.True(exists root ".agents/skills/x/a.md")

    // ---------- pure planner ----------

    let private ownerFiles (texts: (string * string) list) =
        texts
        |> List.map (fun (path, text) ->
            ({
                Path = path
                Sha256 = Fsgg.SkillMirror.sha256 text
                Text = text
                Executable = false
                Owner = ArtifactOwner.Driver
            }
            : OwnerSkillRefresh.DesiredFile))

    let private rowsFor (texts: (string * string) list) =
        texts
        |> List.map (fun (path, text) ->
            {
                Path = path
                Owner = ArtifactOwner.Driver
                Sha256 = Some(Fsgg.SkillMirror.sha256 text)
            })

    let private baseRecord rows versions =
        { (embeddedRecord () |> snd) with
            Generator = { Id = "fsgg-sdd"; Version = "1.0.0" }
            ProducedPaths = []
            MirroredPaths = []
            SddOwnedPaths = []
            DriverPaths = rows
            GameSkillPaths = []
            RenderingSkillPaths = []
            OwnerSkillVersions = versions
        }

    let private embedded version =
        fun (_: ArtifactOwner) -> Some("Pkg", version)

    [<Fact>]
    let ``the planner holds a skill when any file cannot be read`` () =
        let old =
            [ ".agents/skills/s/SKILL.md", "old\n"; ".claude/skills/s/SKILL.md", "old\n" ]

        let desired =
            ownerFiles [ ".agents/skills/s/SKILL.md", "new\n"; ".claude/skills/s/SKILL.md", "new\n" ]

        let bodies = Map.ofList old

        let plan =
            OwnerSkillRefresh.plan
                (baseRecord (rowsFor old) [])
                "2.0.0"
                desired
                (embedded "2.0.0")
                bodies
                (Set.ofList [ ".claude/skills/s/SKILL.md" ])

        Assert.Empty plan.Refreshed
        Assert.Equal(OwnerSkillRefresh.Unreadable, (List.head plan.Held).Reason)

    [<Fact>]
    let ``the planner replaces a skill whose only change is a new file`` () =
        let old = [ ".agents/skills/s/SKILL.md", "same\n" ]

        let desired =
            ownerFiles
                [
                    ".agents/skills/s/SKILL.md", "same\n"
                    ".agents/skills/s/extra.md", "added\n"
                ]

        let plan =
            OwnerSkillRefresh.plan
                (baseRecord (rowsFor old) [])
                "2.0.0"
                desired
                (embedded "2.0.0")
                (Map.ofList old)
                Set.empty

        let refresh = List.head plan.Refreshed

        Assert.Equal<string list>(
            [ ".agents/skills/s/SKILL.md"; ".agents/skills/s/extra.md" ],
            refresh.Writes |> List.map (fun f -> f.Path)
        )

        Assert.Equal<string list>(
            [ ".agents/skills/s/extra.md" ],
            OwnerSkillRefresh.changedPaths (Map.ofList old) refresh
        )

    [<Fact>]
    let ``the planner does nothing for a skill that is already current`` () =
        let current = [ ".agents/skills/s/SKILL.md", "same\n" ]

        let plan =
            OwnerSkillRefresh.plan
                (baseRecord (rowsFor current) [])
                "2.0.0"
                (ownerFiles current)
                (embedded "2.0.0")
                (Map.ofList current)
                Set.empty

        Assert.Empty plan.Refreshed
        Assert.Empty plan.Held

    [<Fact>]
    let ``the planner ignores a skill another authority owns`` () =
        let old = [ ".agents/skills/s/SKILL.md", "old\n" ]
        let record = baseRecord (rowsFor old) []

        let provider =
            {
                Path = ".agents/skills/s/SKILL.md"
                Owner = ArtifactOwner.GeneratedProduct
                Sha256 = None
            }

        let plan =
            OwnerSkillRefresh.plan
                { record with
                    ProducedPaths = [ provider ]
                }
                "2.0.0"
                (ownerFiles [ ".agents/skills/s/SKILL.md", "new\n" ])
                (embedded "2.0.0")
                (Map.ofList old)
                Set.empty

        Assert.Empty plan.Refreshed
        Assert.Empty plan.Held

    [<Fact>]
    let ``the planner holds when this build embeds no package version`` () =
        let old = [ ".agents/skills/s/SKILL.md", "old\n" ]

        let plan =
            OwnerSkillRefresh.plan
                (baseRecord (rowsFor old) [])
                "2.0.0"
                (ownerFiles [ ".agents/skills/s/SKILL.md", "new\n" ])
                (fun _ -> None)
                (Map.ofList old)
                Set.empty

        Assert.Empty plan.Refreshed

        match (List.head plan.Held).Reason with
        | OwnerSkillRefresh.VersionUnprovable _ -> ()
        | other -> failwithf "expected an unprovable version, got %A" other

    // ---------- provenance round trip ----------

    [<Fact>]
    let ``owner skill versions survive a provenance round trip and add nothing when absent`` () =
        let _, record = embeddedRecord ()
        let without = serialize { record with OwnerSkillVersions = [] }
        Assert.DoesNotContain("ownerSkillVersions", without)

        let entry: OwnerSkillVersion =
            {
                SkillId = "s"
                Package = "Pkg"
                Version = "1.2.3"
            }

        let withVersions =
            serialize
                { record with
                    OwnerSkillVersions = [ entry ]
                }

        Assert.Equal<OwnerSkillVersion list>([ entry ], (tryParse withVersions |> Option.get).OwnerSkillVersions)
        Assert.Equal(withVersions, serialize (tryParse withVersions |> Option.get))

    // ---------- .gitattributes ----------

    [<Fact>]
    let ``the exact-byte rules are appended to an existing gitattributes without touching its lines`` () =
        let existing = "* text=auto\n*.png binary\n"

        let merged = Foundation.gitattributesWithExactBytes (Some existing) |> Option.get

        Assert.StartsWith(existing, merged)

        for pattern in Foundation.exactBytePatterns do
            Assert.Contains(pattern + " -text", merged)

    [<Fact>]
    let ``the exact-byte rules are idempotent and respect rules already present`` () =
        let once = Foundation.gitattributesWithExactBytes None |> Option.get
        Assert.Equal(None, Foundation.gitattributesWithExactBytes (Some once))

        let partial = ".agents/skills/** -text\n"
        let completed = Foundation.gitattributesWithExactBytes (Some partial) |> Option.get
        Assert.StartsWith(partial, completed)
        Assert.Equal(1, completed.Split(".agents/skills/** -text").Length - 1)

    [<Fact>]
    let ``the rules cover skill roots and the knowledge archive`` () =
        Assert.Contains(".claude/skills/**", Foundation.exactBytePatterns)
        Assert.Contains(".agents/skills/**", Foundation.exactBytePatterns)
        Assert.Contains("docs/atlas/**", Foundation.exactBytePatterns)

    [<Fact>]
    let ``a text file with no trailing newline gets one before the appended rules`` () =
        let merged =
            Foundation.gitattributesWithExactBytes (Some "* text=auto") |> Option.get

        Assert.StartsWith("* text=auto\n", merged)

    [<Fact>]
    let ``upgrade declares the exact-byte rules and keeps existing gitattributes lines`` () =
        let root, _, _ = aged "0.1.0" []
        TestSupport.writeRelative root ".gitattributes" "* text=auto eol=lf\n"
        upgradeYes root |> ignore
        let text = read root ".gitattributes"
        Assert.StartsWith("* text=auto eol=lf\n", text)

        for pattern in Foundation.exactBytePatterns do
            Assert.Contains(pattern + " -text", text)
