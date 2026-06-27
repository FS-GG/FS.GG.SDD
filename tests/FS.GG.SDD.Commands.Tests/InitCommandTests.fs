namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module InitCommandTests =
    let runInit root =
        let request = TestSupport.request Init root
        let model, effects = init request

        interpretAll root false effects
        |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
        |> fun state -> update BuildReport state |> fst
        |> fun state -> state.Report |> Option.defaultWith (fun () -> buildReport state)

    [<Fact>]
    let ``init creates SDD skeleton with real filesystem evidence`` () =
        let root = TestSupport.tempDirectory()

        let report = runInit root

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(Directory.Exists(Path.Combine(root, ".fsgg")))
        Assert.True(Directory.Exists(Path.Combine(root, "work")))
        Assert.True(Directory.Exists(Path.Combine(root, "readiness")))
        Assert.Contains("schemaVersion: 1", TestSupport.readRelative root ".fsgg/project.yml")
        Assert.Contains("requireEquivalentClaudeAndCodexBehavior: true", TestSupport.readRelative root ".fsgg/agents.yml")
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = ".fsgg/project.yml")
        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml")

    [<Fact>]
    let ``init preserves unrelated user files with real filesystem evidence`` () =
        let root = TestSupport.tempDirectory()
        let notes = Path.Combine(root, "notes.txt")
        File.WriteAllText(notes, "keep me")

        let report = runInit root

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Equal("keep me", File.ReadAllText notes)

    [<Fact>]
    let ``init refuses unsafe authored overwrite with real filesystem evidence`` () =
        let root = TestSupport.tempDirectory()
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        File.WriteAllText(Path.Combine(root, ".fsgg", "project.yml"), "user: content")

        let report = runInit root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeOverwrite")
        Assert.Equal("user: content", File.ReadAllText(Path.Combine(root, ".fsgg", "project.yml")))

    // ---- 033: SDD skeleton emits .fsgg/constitution.md (contract init-emission.md) ----

    // The forbidden token set the emitted constitution must avoid (FR-003/SC-006):
    // repo-, provider-, template-, or rendering-specific names; plus docs URLs.
    let private forbiddenTokens =
        [ "FS.GG.SDD"; "FS.GG.Rendering"; "FS.GG.Governance"; "fsgg-fixture-app"
          "dotnet new"; "http://"; "https://" ]

    // T003 (US1-AC1 / FR-001/FR-002): init emits a non-empty, recognizable constitution.
    [<Fact>]
    let ``init emits a populated constitution with real filesystem evidence`` () =
        let root = TestSupport.tempDirectory()

        let report = runInit root

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root ".fsgg/constitution.md")
        let content = TestSupport.readRelative root ".fsgg/constitution.md"
        Assert.False(System.String.IsNullOrWhiteSpace content)
        Assert.Contains("# Product Constitution", content)
        Assert.Contains("## Core Principles", content)

    // T003 (US1-AC1 report / FR-010): the report attributes it to the SDD skeleton as a
    // created authored agent-guidance artifact (same surface as CLAUDE.md/AGENTS.md).
    [<Fact>]
    let ``init reports the constitution as a created authored skeleton artifact`` () =
        let root = TestSupport.tempDirectory()

        let report = runInit root

        let change =
            report.ChangedArtifacts
            |> List.tryFind (fun c -> c.Path = ".fsgg/constitution.md")
            |> Option.defaultWith (fun () -> failwith "Expected a changed-artifact entry for .fsgg/constitution.md.")
        Assert.Equal("agentGuidance", change.Kind)
        Assert.Equal("authored", change.Ownership)
        Assert.Equal(ArtifactOperation.Create, change.Operation)

    // T003 (US1-AC3 generic / FR-003/SC-006): no repo/provider/template/rendering token
    // and no unfilled [BRACKET] placeholder remains.
    [<Fact>]
    let ``init constitution content is generic and placeholder-free`` () =
        let root = TestSupport.tempDirectory()

        runInit root |> ignore
        let content = TestSupport.readRelative root ".fsgg/constitution.md"

        for token in forbiddenTokens do
            Assert.DoesNotContain(token, content)
        Assert.DoesNotContain("TODO", content)
        Assert.DoesNotContain("FIXME", content)
        // No unfilled bracket placeholder like [PROJECT_NAME].
        Assert.DoesNotMatch(System.Text.RegularExpressions.Regex @"\[[A-Z0-9_]+\]", content)

    // T003 (US1-AC2 determinism / FR-007/SC-003): two init runs into leaf-stable roots
    // produce byte-identical constitution content.
    [<Fact>]
    let ``init constitution is byte-identical across runs`` () =
        let leaf = "const-det"
        let mk () =
            let dir = Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + System.Guid.NewGuid().ToString("N"), leaf)
            Directory.CreateDirectory dir |> ignore
            dir

        let firstRoot = mk ()
        let secondRoot = mk ()
        runInit firstRoot |> ignore
        runInit secondRoot |> ignore

        let first = File.ReadAllBytes(Path.Combine(firstRoot, ".fsgg", "constitution.md"))
        let second = File.ReadAllBytes(Path.Combine(secondRoot, ".fsgg", "constitution.md"))
        Assert.Equal<byte[]>(first, second)
