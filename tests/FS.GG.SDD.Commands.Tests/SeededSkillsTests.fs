namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Commands.Internal
open Xunit

/// 051: the seeded fs-gg-sdd-* process skill set — no-clobber (INV-4), determinism
/// (INV-3), Claude/Codex parity (INV-2), the membership/embedded drift guard (INV-7),
/// and the offline init≡scaffold single-seam (INV-5). Real-filesystem temp-dir fixtures.
module SeededSkillsTests =

    let private claudePath name = $".claude/skills/{name}/SKILL.md"
    let private codexPath name = $".codex/skills/{name}/SKILL.md"

    let private seedInto root =
        TestSupport.request Init root |> TestSupport.runRequest

    // On-disk authored fs-gg-sdd-* skill names under a given surface dir in the repo,
    // excluding the product-internal fs-gg-sdd-project skill.
    let private onDiskAuthoredSet surface =
        let dir = Path.Combine(TestSupport.repoRoot, surface, "skills")

        Directory.GetDirectories dir
        |> Array.choose (fun d ->
            match Path.GetFileName d with
            | null -> None
            | name -> Some name)
        |> Array.filter (fun name -> name.StartsWith "fs-gg-sdd-" && name <> "fs-gg-sdd-project")
        |> Array.filter (fun name -> File.Exists(Path.Combine(dir, name, "SKILL.md")))
        |> Array.sort
        |> Array.toList

    // ---------- T008 (US2 / INV-4, SC-003): no-clobber re-run ----------

    [<Fact>]
    let ``re-seeding preserves an author-edited skill and refills a deleted one`` () =
        let root = TestSupport.tempDirectory ()
        seedInto root |> ignore

        // Author edit to one Claude skill; delete one Codex skill entirely.
        let edited = TestSupport.readRelative root (claudePath "fs-gg-sdd-plan") + "\n\nLOCAL EDIT\n"
        TestSupport.writeRelative root (claudePath "fs-gg-sdd-plan") edited
        File.Delete(Path.Combine(root, (codexPath "fs-gg-sdd-tasks").Replace('/', Path.DirectorySeparatorChar)))

        // A second present file, untouched, to prove no incidental overwrite.
        let untouchedBefore = TestSupport.readRelative root (claudePath "fs-gg-sdd-charter")

        let report = seedInto root

        // No-clobber: the edited file is preserved verbatim and the re-run surfaces the
        // unsafeOverwrite refusal (the same authored-skeleton policy as the constitution),
        // never silently rewriting it. The refused-artifact entry records Refuse, not write.
        Assert.Equal(edited, TestSupport.readRelative root (claudePath "fs-gg-sdd-plan"))
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "unsafeOverwrite")
        let editedChange =
            report.ChangedArtifacts
            |> List.tryFind (fun c -> c.Path = ".claude/skills/fs-gg-sdd-plan/SKILL.md")
            |> Option.defaultWith (fun () -> failwith "Expected a changed-artifact entry for the edited skill.")
        Assert.Equal(ArtifactOperation.Refuse, editedChange.Operation)
        // The deleted file is refilled, and an untouched present file is not disturbed.
        Assert.True(TestSupport.existsRelative root (codexPath "fs-gg-sdd-tasks"), "Deleted skill should be refilled.")
        Assert.False(System.String.IsNullOrWhiteSpace(TestSupport.readRelative root (codexPath "fs-gg-sdd-tasks")))
        Assert.Equal(untouchedBefore, TestSupport.readRelative root (claudePath "fs-gg-sdd-charter"))

    // ---------- T011 (US3 / INV-3, FR-006/SC-004): determinism ----------

    [<Fact>]
    let ``two seeding runs produce byte-identical, date-free skill files`` () =
        let mk () =
            let dir = Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + System.Guid.NewGuid().ToString("N"), "seed-det")
            Directory.CreateDirectory dir |> ignore
            dir

        let first = mk ()
        let second = mk ()
        seedInto first |> ignore
        seedInto second |> ignore

        let dateOrTime = Regex @"20\d\d-\d\d-\d\d|\d\d:\d\d:\d\d"

        for name in SeededSkills.skillNames do
            for path in [ claudePath name; codexPath name ] do
                let a = File.ReadAllBytes(Path.Combine(first, path.Replace('/', Path.DirectorySeparatorChar)))
                let b = File.ReadAllBytes(Path.Combine(second, path.Replace('/', Path.DirectorySeparatorChar)))
                Assert.Equal<byte[]>(a, b)
                Assert.DoesNotMatch(dateOrTime, TestSupport.readRelative first path)

    // ---------- T012 (US3 / INV-2, FR-002/SC-004): Claude/Codex parity ----------

    [<Fact>]
    let ``each seeded skill is byte-identical across the Claude and Codex surfaces`` () =
        let root = TestSupport.tempDirectory ()
        seedInto root |> ignore

        for name in SeededSkills.skillNames do
            let claude = File.ReadAllBytes(Path.Combine(root, (claudePath name).Replace('/', Path.DirectorySeparatorChar)))
            let codex = File.ReadAllBytes(Path.Combine(root, (codexPath name).Replace('/', Path.DirectorySeparatorChar)))
            Assert.Equal<byte[]>(claude, codex)

    // ---------- T013 (US3 / INV-7, FR-010/SC-005): membership + embedded drift guard ----------

    [<Fact>]
    let ``declared set equals the on-disk authored set on both surfaces`` () =
        Assert.Equal<string list>(SeededSkills.skillNames, onDiskAuthoredSet ".claude")
        Assert.Equal<string list>(SeededSkills.skillNames, onDiskAuthoredSet ".codex")

    [<Fact>]
    let ``each embedded skill body matches the on-disk Claude and Codex sources`` () =
        for name in SeededSkills.skillNames do
            let embedded = SeededSkills.loadBody name
            let claudeSource = File.ReadAllText(Path.Combine(TestSupport.repoRoot, (claudePath name).Replace('/', Path.DirectorySeparatorChar)))
            let codexSource = File.ReadAllText(Path.Combine(TestSupport.repoRoot, (codexPath name).Replace('/', Path.DirectorySeparatorChar)))
            Assert.Equal(claudeSource, embedded)
            Assert.Equal(claudeSource, codexSource)

    // ---------- T019 (US1 / INV-5, FR-007): offline init≡scaffold single seam ----------

    [<Fact>]
    let ``the effects scaffold builds reuse the shared seam and include every seeded skill effect`` () =
        let root = TestSupport.tempDirectory ()
        let request = { TestSupport.request Scaffold root with Provider = Some "fixture" }

        let descriptor: Fsgg.Provider.ProviderDescriptor =
            { Name = "fixture"
              ContractVersion = "1.0.0"
              TemplateId = "fsgg-fixture"
              Source = "/dev/null"
              Parameters = []
              Build = None
              Test = None
              Run = None
              Verify = None
              NameParameter = "name" }

        let planned = scaffoldInvocationEffects request descriptor Map.empty |> Set.ofList

        // Every one of the 30 seeded-skill WriteFile effects flows through scaffold via the
        // reused initEffects seam. Fails if scaffold stops reusing initEffects.
        Assert.Equal(30, List.length SeededSkills.skillEffects)
        for effect in SeededSkills.skillEffects do
            Assert.Contains(effect, planned)
