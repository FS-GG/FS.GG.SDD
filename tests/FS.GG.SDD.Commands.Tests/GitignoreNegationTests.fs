namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.TestShared
open Xunit

/// Feature 092 (ADR-0026) — FR-014/FR-015, the load-bearing guard.
///
/// `readiness/*/` → `readiness/*/*` is **not** cosmetic. Git never descends into an *excluded
/// directory*, so `!readiness/*/ship-verdict.json` beneath the directory pattern is **silently
/// inert**: the verdict is written to disk, never committed, and the feature looks done.
/// Excluding the directory's *contents* keeps the parent traversable so the negation can fire.
///
/// No string assertion can tell those two apart — `"readiness/*/"` is a substring of
/// `"readiness/*/*"`, so the pre-092 byte/substring guards in `ArtifactTaxonomyTests` pass under
/// an absent, inert, *or* correct negation. Only git can decide. These tests run real git.
///
/// Joins ProcessGlobalEnv: spawning a PATH-resolved `git` must not race a sibling mutating
/// process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module GitignoreNegationTests =

    /// Run real `git` in a directory and return (exitCode, trimmed stdout).
    let private git = TestShared.ChildProcess.git

    let private initRepo root =
        git root [ "init"; "-q"; "." ] |> ignore
        git root [ "config"; "user.email"; "test@fs.gg" ] |> ignore
        git root [ "config"; "user.name"; "test" ] |> ignore

    let private write root (relative: string) (text: string) =
        let full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))

        Directory.CreateDirectory(Path.GetDirectoryName full |> Option.ofObj |> Option.defaultValue root)
        |> ignore

        File.WriteAllText(full, text)

    /// Every file a real `readiness/<id>/` holds: the six top-level views plus one agent target.
    let private materializeReadinessTree root (prefix: string) (workId: string) =
        let at name = $"{prefix}{workId}/{name}"

        [ "work-model.json"
          "analysis.json"
          "verify.json"
          "ship.json"
          "ship-verdict.json"
          "governance-handoff.json"
          "summary.md"
          "agent-commands/claude/guidance.json"
          "agent-commands/claude/commands.md"
          "agent-commands/claude/skills.md" ]
        |> List.iter (fun name -> write root (at name) "{}\n")

    /// The paths `git add -A` would stage, restricted to those under `prefix`.
    let private stagedUnder root (prefix: string) =
        git root [ "add"; "-A" ] |> ignore
        let _, out = git root [ "diff"; "--cached"; "--name-only" ]

        out.Split('\n')
        |> Array.map (fun l -> l.Trim())
        |> Array.filter (fun l -> l.StartsWith prefix)
        |> Set.ofArray

    let private isIgnored root (path: string) =
        let code, _ = git root [ "check-ignore"; "-q"; path ]
        code = 0

    // ---------- FR-014: the seeded consumer fragment ----------

    [<Fact; Trait("tier", "slow")>]
    let ``the seeded gitignore stages exactly the ship verdict under readiness`` () =
        let root = TestSupport.tempDirectory ()
        initRepo root
        TestSupport.initializeProject root // seeds the no-clobber .gitignore
        materializeReadinessTree root "readiness/" "003-demo"

        Assert.Equal<Set<string>>(Set.ofList [ "readiness/003-demo/ship-verdict.json" ], stagedUnder root "readiness/")

    [<Fact; Trait("tier", "slow")>]
    let ``the pre-092 directory rule makes the negation inert - staging nothing`` () =
        // The regression that proves `readiness/*/*` is load-bearing. With ADR-0018's directory
        // rule, the *identical* negation stages nothing: git never descends into `readiness/003-demo/`.
        let root = TestSupport.tempDirectory ()
        initRepo root
        materializeReadinessTree root "readiness/" "003-demo"
        write root ".gitignore" "readiness/*/\n!readiness/*/ship-verdict.json\n"

        Assert.Empty(stagedUnder root "readiness/")

    [<Fact; Trait("tier", "slow")>]
    let ``nested agent-command views stay ignored under the contents rule`` () =
        let root = TestSupport.tempDirectory ()
        initRepo root
        TestSupport.initializeProject root
        materializeReadinessTree root "readiness/" "003-demo"

        for ignored in
            [ "readiness/003-demo/ship.json"
              "readiness/003-demo/verify.json"
              "readiness/003-demo/work-model.json"
              "readiness/003-demo/analysis.json"
              "readiness/003-demo/summary.md"
              "readiness/003-demo/governance-handoff.json"
              "readiness/003-demo/agent-commands/claude/guidance.json"
              "readiness/003-demo/agent-commands/claude/commands.md"
              "readiness/003-demo/agent-commands/claude/skills.md" ] do
            Assert.True(isIgnored root ignored, $"expected {ignored} to be ignored")

        Assert.False(
            isIgnored root "readiness/003-demo/ship-verdict.json",
            "the ship verdict must NOT be ignored — the negation has to fire"
        )

    [<Fact; Trait("tier", "slow")>]
    let ``a per-file durable proof pin still fires under the contents rule`` () =
        // ADR-0018's `!readiness/<id>/<proof>` escape hatch must survive the change: excluding
        // directory *contents* keeps the parent traversable, so per-file negations still work.
        let root = TestSupport.tempDirectory ()
        initRepo root
        TestSupport.initializeProject root
        materializeReadinessTree root "readiness/" "003-demo"
        write root "readiness/003-demo/pinned-proof.json" "{}\n"

        let seeded = TestSupport.readRelative root ".gitignore"
        write root ".gitignore" (seeded + "!readiness/003-demo/pinned-proof.json\n")

        Assert.Equal<Set<string>>(
            Set.ofList
                [ "readiness/003-demo/pinned-proof.json"
                  "readiness/003-demo/ship-verdict.json" ],
            stagedUnder root "readiness/"
        )

    // ---------- FR-015: this repository's own dogfood rule ----------

    [<Fact; Trait("tier", "slow")>]
    let ``this repo's gitignore stages exactly the ship verdict under specs readiness`` () =
        // SDD dogfoods through Spec Kit, so its readiness views land at
        // `specs/<feature>/readiness/<work-id>/`. Same trap, different prefix (ADR-0026).
        let root = TestSupport.tempDirectory ()
        initRepo root
        File.Copy(Path.Combine(TestSupport.repoRoot, ".gitignore"), Path.Combine(root, ".gitignore"))
        materializeReadinessTree root "specs/092-x/readiness/" "003-demo"
        write root "specs/092-x/spec.md" "# spec\n"

        Assert.Equal<Set<string>>(
            Set.ofList [ "specs/092-x/readiness/003-demo/ship-verdict.json" ],
            stagedUnder root "specs/092-x/readiness/"
        )

    [<Fact; Trait("tier", "slow")>]
    let ``this repo's root readiness pinned proofs stay committed`` () =
        // The root `readiness/<id>/` holds hand-pinned durable proofs. It is matched by NO rule
        // and must stay that way — `specs/*/readiness/*/*` must not leak up to it.
        let root = TestSupport.tempDirectory ()
        initRepo root
        File.Copy(Path.Combine(TestSupport.repoRoot, ".gitignore"), Path.Combine(root, ".gitignore"))
        write root "readiness/019-pinned/proof.json" "{}\n"

        Assert.False(isIgnored root "readiness/019-pinned/proof.json")
        Assert.Contains("readiness/019-pinned/proof.json", stagedUnder root "readiness/")

    [<Fact>]
    let ``this repo's own gitignore actually carries the amended dogfood rule`` () =
        // FR-010: the two adoptions are one decision. Landing the seed without this one would
        // ship an artifact SDD does not itself commit.
        let lines =
            (TestSupport.readRelative TestSupport.repoRoot ".gitignore").Replace("\r\n", "\n").Split('\n')
            |> Array.map (fun l -> l.Trim())

        Assert.Contains("specs/*/readiness/*/*", lines)
        Assert.Contains("!specs/*/readiness/*/ship-verdict.json", lines)
        Assert.DoesNotContain("specs/*/readiness/", lines)

    // ---------- The seeded constant and the emitted artifact agree ----------

    [<Fact>]
    let ``the negation names exactly the path the ship verdict is written to`` () =
        // A typo in either half is a silently ignored verdict. Bind them to one source of truth.
        let expected =
            FS.GG.SDD.Artifacts.GenerationManifest.expectedShipVerdictOutputPath "<id>"

        Assert.Equal("readiness/<id>/ship-verdict.json", expected)

        Assert.Contains("!readiness/*/ship-verdict.json", Foundation.gitignoreSeedText)
