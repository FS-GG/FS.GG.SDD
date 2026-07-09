namespace FS.GG.SDD.Cli.Tests

open System.IO
open Spectre.Console
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

module RichRenderingTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    /// A color-off, fixed-width Spectre console backed by a StringWriter.
    let makeConsole (width: int) =
        let writer = new StringWriter()
        let settings = AnsiConsoleSettings()
        settings.Ansi <- AnsiSupport.No
        settings.ColorSystem <- ColorSystemSupport.NoColors
        settings.Out <- new AnsiConsoleOutput(writer)
        let console = AnsiConsole.Create settings
        // Spectre's CI profile enrichment (GITHUB_ACTIONS) re-enables ANSI *after*
        // AnsiSupport.No, so [bold]/[dim] decorations still emit SGR escapes. Force the
        // capability off so this color-off console genuinely emits zero ANSI in CI too.
        console.Profile.Capabilities.Ansi <- false
        console.Profile.Width <- width
        writer, console

    let renderAt (width: int) (report: CommandReport) =
        let writer, console = makeConsole width
        renderRichTo console report
        writer.ToString()

    let render report = renderAt 120 report

    // ----- A hand-built, fully populated report exercising every section. -----

    let diag id severity message =
        { Id = id
          Severity = severity
          Artifact = None
          Location = None
          Message = message
          Correction = "correction-for-" + id
          RelatedIds = []
          IsToolDefect = false }

    let generatedView path currency =
        { Path = path
          Kind = "workModel"
          SchemaVersion = Some 1
          Generator = None
          Sources = []
          Currency = currency
          DiagnosticIds = [] }

    let changedArtifact path =
        { Path = path
          Kind = "structuredSource"
          Ownership = "sdd"
          Operation = ArtifactOperation.Create
          BeforeDigest = None
          AfterDigest = None
          SafeWriteDecision = "create"
          DiagnosticIds = [] }

    let specification: SpecificationSummary =
        { WorkId = "042-rich-sample"
          Stage = "specify"
          Status = "draft"
          StoryIds = [ "US1"; "US2" ]
          RequirementIds = [ "FR-001"; "FR-002"; "FR-003" ]
          AcceptanceScenarioIds = [ "AS-001" ]
          AmbiguityIds = [ "AMB-001" ] }

    /// Feature 084: a representative lifecycle-status for the Specify sample (ordinal 2, current).
    let stageEntry command ordinal state : StageEntry =
        { Command = command
          Ordinal = ordinal
          State = state }

    let sampleLifecycleStatus: LifecycleStatus =
        { WorkId = Some "042-rich-sample"
          Stages =
            [ stageEntry Charter 1 StageState.Done
              stageEntry Specify 2 StageState.Current
              stageEntry Clarify 3 StageState.Next
              stageEntry Checklist 4 StageState.Pending
              stageEntry Plan 5 StageState.Pending
              stageEntry Tasks 6 StageState.Pending
              stageEntry Analyze 7 StageState.Pending
              stageEntry Evidence 8 StageState.Pending
              stageEntry Verify 9 StageState.Pending
              stageEntry Ship 10 StageState.Pending ]
          CurrentOrdinal = Some 2
          TotalStages = 10
          Outcome = SucceededWithWarnings
          NextCommand = Some Clarify
          IsLifecycleStage = true }

    /// Specify command, populated stage + every report section.
    let sampleReport: CommandReport =
        { SchemaVersion = 1
          ReportVersion = "1.0"
          Command = Specify
          ProjectRoot = "."
          OutputFormat = Rich
          DryRun = true
          Outcome = SucceededWithWarnings
          Coherent = false
          WorkId = Some "042-rich-sample"
          ChangedArtifacts = [ changedArtifact "work/042-rich-sample/spec.md" ]
          Specification = Some specification
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
          Lint = None
          Surface = None
          GeneratedViews =
            [ generatedView "readiness/042-rich-sample/work-model.json" GeneratedViewCurrency.Current
              generatedView "readiness/042-rich-sample/analysis.json" GeneratedViewCurrency.Stale ]
          Diagnostics =
            [ diag "ERR-1" DiagnosticError "a blocking problem"
              diag "WARN-1" DiagnosticWarning "a warning worth noting"
              diag "INFO-1" DiagnosticInfo "an informational note" ]
          GovernanceCompatibility =
            [ { Path = ".fsgg/config.yml"
                Relationship = "governance-config"
                RequiredBySdd = false
                State = "unevaluated"
                DiagnosticIds = [] } ]
          NextAction =
            Some
                { ActionId = "NEXT-CLARIFY"
                  Command = Some Clarify
                  WorkId = Some "042-rich-sample"
                  Reason = "resolve the remaining ambiguity"
                  RequiredArtifacts = [ "work/042-rich-sample/clarifications.md" ]
                  BlockingDiagnosticIds = [] }
          Help = None
          LifecycleStatus = sampleLifecycleStatus }

    [<Fact>]
    let ``T010 rich projection represents every populated report section`` () =
        let text = render sampleReport

        // Identity (Command / WorkId / DryRun)
        Assert.Contains("specify", text)
        Assert.Contains("042-rich-sample", text)
        Assert.Contains("dry-run", text)
        // Outcome
        Assert.Contains(outcomeValue sampleReport.Outcome, text)
        // Changed artifacts
        Assert.Contains("spec.md", text)
        // Populated stage summary (mirrors renderText facts)
        Assert.Contains("specificationRequirements", text)
        // Generated views (with stale currency emphasized)
        Assert.Contains("work-model.json", text)
        Assert.Contains(generatedViewCurrencyValue GeneratedViewCurrency.Stale, text)
        // Diagnostics grouped by severity
        Assert.Contains("ERR-1", text)
        Assert.Contains("WARN-1", text)
        Assert.Contains("INFO-1", text)
        // Next action callout
        Assert.Contains("NEXT-CLARIFY", text)
        // Governance compatibility section
        Assert.Contains("unevaluated", text)

    [<Fact>]
    let ``T010 rich projection invents no foreign stage fact`` () =
        let text = render sampleReport
        // A stage that is None must not surface its renderText-only keys.
        Assert.DoesNotContain("shipReadiness", text)
        Assert.DoesNotContain("refreshDisposition", text)

    [<Fact>]
    let ``T011 diagnostics are grouped by severity with severity labels`` () =
        let text = render sampleReport
        Assert.Contains(severityValue DiagnosticError, text)
        Assert.Contains(severityValue DiagnosticWarning, text)
        Assert.Contains(severityValue DiagnosticInfo, text)

    [<Fact>]
    let ``T011 outcome and next command are both visible`` () =
        let text = render sampleReport
        Assert.Contains(outcomeValue sampleReport.Outcome, text)
        // next lifecycle command name
        Assert.Contains(commandName Clarify, text)

    [<Fact>]
    let ``T011 a report with no diagnostics renders without inventing facts`` () =
        let report = { sampleReport with Diagnostics = [] }
        let text = render report
        Assert.DoesNotContain("ERR-1", text)
        Assert.Contains(outcomeValue report.Outcome, text)

    [<Fact>]
    let ``T011 a report with many diagnostics renders them all`` () =
        let many = [ for i in 1..12 -> diag $"D-{i}" DiagnosticWarning $"message {i}" ]
        let report = { sampleReport with Diagnostics = many }
        let text = render report

        for i in 1..12 do
            Assert.Contains($"D-{i}", text)

    [<Fact>]
    let ``T012 renderRichTo mutates only the console`` () =
        let before = serializeReport sampleReport
        let _, console = makeConsole 120
        renderRichTo console sampleReport
        let after = serializeReport sampleReport
        Assert.Equal(before, after)

    // ----- Report-shape coverage via the real workflow (T012a) -----

    let private specifyReport () =
        let root = Commands.tempDirectory ()
        Commands.initializeProject root
        Commands.runCharter root "050-specify" "Specify Shape" |> ignore
        Commands.runSpecify root "050-specify" "Specify Shape"

    let private shipReport () =
        let root = Commands.tempDirectory ()
        Commands.initializeVerifiedProject root "051-ship" "Ship Shape"
        Commands.runShip root "051-ship" "Ship Shape"

    let private agentsReport () =
        let root = Commands.tempDirectory ()
        Commands.initializeVerifiedProject root "052-agents" "Agents Shape"
        Commands.runAgents root "052-agents"

    let private refreshReport () =
        let root = Commands.tempDirectory ()
        Commands.initializeVerifiedProject root "053-refresh" "Refresh Shape"
        Commands.runRefresh root "053-refresh"

    [<Theory>]
    [<InlineData("specify")>]
    [<InlineData("ship")>]
    [<InlineData("agents")>]
    [<InlineData("refresh")>]
    let ``T012a each report shape renders without throwing`` (shape: string) =
        let report =
            match shape with
            | "specify" -> specifyReport ()
            | "ship" -> shipReport ()
            | "agents" -> agentsReport ()
            | "refresh" -> refreshReport ()
            | other -> failwith $"unknown shape {other}"

        let text = render report
        Assert.False(System.String.IsNullOrWhiteSpace text)
        Assert.Contains(outcomeValue report.Outcome, text)

    [<Fact>]
    let ``T012a agents report implies no next lifecycle stage`` () =
        // agents/refresh are not lifecycle stages; NextAction implies no next command.
        let report = agentsReport ()
        Assert.Equal(None, nextLifecycleCommand report.Command)
        let text = render report
        Assert.False(System.String.IsNullOrWhiteSpace text)

    [<Fact>]
    let ``T012a a blocked report renders without throwing`` () =
        let blocked =
            { sampleReport with
                Outcome = CommandOutcome.Blocked
                Diagnostics = [ diag "ERR-BLOCK" DiagnosticError "blocking" ] }

        let text = render blocked
        Assert.Contains(outcomeValue CommandOutcome.Blocked, text)
        Assert.Contains("ERR-BLOCK", text)

    // ----- Width adaptation (T012b) -----

    [<Theory>]
    [<InlineData(40)>]
    [<InlineData(200)>]
    let ``T012b rich output adapts to fixed width without throwing`` (width: int) =
        let text = renderAt width sampleReport
        Assert.False(System.String.IsNullOrWhiteSpace text)
        Assert.Contains(outcomeValue sampleReport.Outcome, text)

    [<Fact>]
    let ``T012b rich output handles a profile with default width`` () =
        // No explicit width set on the profile (uses Spectre default).
        let writer = new StringWriter()
        let settings = AnsiConsoleSettings()
        settings.Ansi <- AnsiSupport.No
        settings.ColorSystem <- ColorSystemSupport.NoColors
        settings.Out <- new AnsiConsoleOutput(writer)
        let console = AnsiConsole.Create settings
        // Force ANSI off (CI enrichment would otherwise re-enable it) — see makeConsole.
        console.Profile.Capabilities.Ansi <- false
        renderRichTo console sampleReport
        let text = writer.ToString()
        Assert.False(System.String.IsNullOrWhiteSpace text)
        Assert.Contains(outcomeValue sampleReport.Outcome, text)
