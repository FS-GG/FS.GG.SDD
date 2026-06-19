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
