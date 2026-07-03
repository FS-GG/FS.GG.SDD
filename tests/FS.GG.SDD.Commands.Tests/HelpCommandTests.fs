namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// §3.5 (FR-008–011): help is scoped, lists every command and flag, builds a NoChange/exit-0
// report, and a genuinely unknown command still resolves to unknownCommand/exit 1.
module HelpCommandTests =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let private generator = SchemaVersionModule.currentGeneratorVersion()

    let private allCommands =
        [ Init; Charter; Specify; Clarify; Checklist; Plan; Tasks; Analyze; Evidence; Verify; Ship; Agents; Refresh; Scaffold ]

    [<Fact>]
    let ``top-level help is scoped TopLevel and lists every command plus CLI peers`` () =
        let summary = CommandHelp.topLevelHelp generator

        Assert.Equal(TopLevel, summary.Scope)
        let names = summary.Commands |> List.map (fun entry -> entry.Name)
        for command in allCommands do
            Assert.Contains(commandName command, names)
        Assert.Contains("version", names)
        Assert.Contains("validate", names)
        Assert.Contains("registry", names)
        Assert.NotEmpty(summary.GlobalFlags)
        Assert.Empty(summary.CommandFlags)

    [<Fact>]
    let ``command help is scoped to the command and carries its own flags`` () =
        for command in allCommands do
            let summary = CommandHelp.commandHelp command

            Assert.Equal(Command(commandName command), summary.Scope)
            Assert.Equal<HelpFlag list>(CommandHelp.commandFlags command, summary.CommandFlags)
            Assert.Empty(summary.Commands)
            Assert.NotEmpty(summary.GlobalFlags)

    [<Fact>]
    let ``help report is NoChange exit 0 with help populated and no next action`` () =
        let request = TestSupport.request Init "."

        let report = helpReport request (CommandHelp.topLevelHelp generator)

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        Assert.True(report.Help.IsSome)
        Assert.True(report.NextAction.IsNone)

    // FR-011: a genuinely unknown command resolves to unknownCommand/exit 1 — the
    // ± --help dispatch in Program.fs routes unknown commands here unchanged.
    [<Fact>]
    let ``unknown command report is Blocked and exits 1`` () =
        let request = TestSupport.request Init "."

        let model =
            { Request = request
              PendingEffects = []
              InterpretedEffects = []
              Diagnostics = [ unknownCommand "frobnicate" ]
              Specification = None
              Clarification = None
              Checklist = None
              Plan = None
              Tasks = None
              Analysis = None
              Evidence = None
              Verification = None
              Ship = None
              AgentGuidance = None
              Refresh = None
              Scaffold = None
              Doctor = None
              Upgrade = None
              GeneratedViews = []
              Report = None }

        let report = buildReport model

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(1, exitCodeForReport report)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownCommand")
        Assert.True(report.Help.IsNone)

    // Feature 063 (FR-005 / SC-003): the unknownCommand correction must name every command the CLI
    // accepts — the 16 lifecycle commands plus the CLI-level peers `validate` and `registry`.
    // This pin fails if a command is added without updating the correction.
    [<Fact>]
    let ``unknownCommand correction names every accepted command`` () =
        let correction = (unknownCommand "frobnicate").Correction
        let expected =
            [ "init"; "charter"; "specify"; "clarify"; "checklist"; "plan"; "tasks"; "analyze"
              "evidence"; "verify"; "ship"; "agents"; "refresh"; "scaffold"; "doctor"; "upgrade"
              "validate"; "registry" ]
        for command in expected do
            Assert.Contains(command, correction)

    // Feature 063 (FR-006 / SC-004): the reseed NextAction (triggered by scaffold.cliBehindMinimum)
    // must name all three seeded-skill roots, including the 056 neutral .agents/skills.
    [<Fact>]
    let ``reseed NextAction lists all three seeded-skill roots`` () =
        let model =
            { Request = TestSupport.request Doctor "."
              PendingEffects = []
              InterpretedEffects = []
              Diagnostics = [ FS.GG.SDD.Artifacts.Diagnostics.scaffoldCliBehindMinimum "0.2.1" "9.9.9" ]
              Specification = None
              Clarification = None
              Checklist = None
              Plan = None
              Tasks = None
              Analysis = None
              Evidence = None
              Verification = None
              Ship = None
              AgentGuidance = None
              Refresh = None
              Scaffold = None
              Doctor = None
              Upgrade = None
              GeneratedViews = []
              Report = None }

        let report = buildReport model
        let nextAction = Option.get report.NextAction
        Assert.Equal("reseedSeededSkills", nextAction.ActionId)
        for root in [ ".claude/skills"; ".codex/skills"; ".agents/skills" ] do
            Assert.Contains(root, nextAction.RequiredArtifacts)
