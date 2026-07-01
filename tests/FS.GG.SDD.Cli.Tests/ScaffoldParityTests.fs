namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// SC-006 / Scenario E: the scaffold report is fact-identical across `--json`,
/// `--text`, and `--rich`; `--rich` redirected equals `--text`; and the rich path
/// changes no JSON byte. Built from a constructed report (no template engine).
module ScaffoldParityTests =
    let private interactiveColor = { IsInteractive = true; ColorEnabled = true; Width = Some 100; IsInputInteractive = true }
    let private nonInteractive = { interactiveColor with IsInteractive = false }

    let private scaffoldSummary: ScaffoldSummary =
        { ProviderName = Some "fixture"
          ProviderContractVersion = Some "1.0.0"
          RequiredMinimumCliVersion = Some "0.3.0"
          Outcome = "providerSucceeded"
          SkeletonCreated = true
          ProviderInvoked = true
          ProducedPathCount = 2
          ProducedPaths = [ "App.fsproj"; "Program.fs" ]
          EffectiveParameters = [ "productName", "Acme"; "variant", "alpha" ]
          RepoInitOutcome = "initialized"
          ExecutableScriptCount = 0
          ExecutableScriptsSkipped = 0
          NextActionHint = "SDD skeleton ready; begin the lifecycle at charter."
          ProviderInvocation = None }

    let private report: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Scaffold
            Outcome = CommandOutcome.Succeeded
            Specification = None
            Scaffold = Some scaffoldSummary }

    [<Fact>]
    let ``scaffold json projection equals serializeReport and the rich path changes no byte`` () =
        let before = serializeReport report
        resolve Rich interactiveColor report |> ignore
        let after = serializeReport report
        Assert.Equal(before, after)
        Assert.Equal(serializeReport report, (resolve Json interactiveColor report).Text)

    [<Fact>]
    let ``scaffold rich redirected equals the text projection`` () =
        let result = resolve Rich nonInteractive report
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText report, result.Text)

    [<Fact>]
    let ``scaffold facts appear in every projection`` () =
        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich interactiveColor report).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("providerSucceeded", projection)

        Assert.Contains("\"producedPaths\"", json)
        Assert.Contains("scaffoldProducedPath: App.fsproj", text)
        Assert.Contains("App.fsproj", rich)

        // Feature 052 US1: the provider-declared required minimum appears in every projection.
        Assert.Contains("\"requiredMinimumCliVersion\": \"0.3.0\"", json)
        Assert.Contains("scaffoldRequiredMinimumCliVersion: 0.3.0", text)
        for projection in [ json; text; rich ] do
            Assert.Contains("0.3.0", projection)

    // 050 T018 (FR-003/FR-008): the effective forwarded parameters project consistently across
    // json (array of {key,value}, sorted), text (`scaffoldEffectiveParam: key=value`), and rich
    // (the same key/value facts via the details table); the rich path adds/drops no JSON byte and
    // degrades to zero-ANSI when non-interactive.
    [<Fact>]
    let ``scaffold effective parameters are identical across json text and rich`` () =
        // Rich is a pure projection: it changes no JSON byte.
        let before = serializeReport report
        resolve Rich interactiveColor report |> ignore
        Assert.Equal(before, serializeReport report)

        let json = (resolve Json interactiveColor report).Text
        let text = (resolve Text nonInteractive report).Text
        let rich = (resolve Rich interactiveColor report).Text

        Assert.Contains("\"key\": \"productName\"", json)
        Assert.Contains("\"value\": \"Acme\"", json)
        Assert.Contains("\"key\": \"variant\"", json)
        Assert.Contains("\"value\": \"alpha\"", json)
        Assert.Contains("scaffoldEffectiveParam: productName=Acme", text)
        Assert.Contains("scaffoldEffectiveParam: variant=alpha", text)

        // Every projection (rich reuses the plain key=value lines) carries the same facts.
        for projection in [ json; text; rich ] do
            Assert.Contains("productName", projection)
            Assert.Contains("Acme", projection)
            Assert.Contains("variant", projection)
            Assert.Contains("alpha", projection)

    // 050 T018: rich redirected (non-interactive) is byte-identical to --text and carries zero ANSI.
    [<Fact>]
    let ``scaffold effective parameters degrade to zero-ANSI text when non-interactive`` () =
        let result = resolve Rich nonInteractive report
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText report, result.Text)
        Assert.DoesNotContain("[", result.Text)

    // 032 (FR-011 / SC-006): the repo-init outcome and the make-executable counts are
    // fact-identical across json/text/rich, and the rich projection adds/drops no JSON byte.
    [<Fact>]
    let ``scaffold repo-init and exec facts are identical across json text and rich`` () =
        let postInstReport: CommandReport =
            { report with
                Scaffold =
                    Some
                        { scaffoldSummary with
                            ProducedPathCount = 2
                            ProducedPaths = [ "App.fsproj"; "run.sh" ]
                            RepoInitOutcome = "initialized"
                            ExecutableScriptCount = 1
                            ExecutableScriptsSkipped = 0 } }

        // Rich is a pure projection: it changes no JSON byte.
        let before = serializeReport postInstReport
        resolve Rich interactiveColor postInstReport |> ignore
        Assert.Equal(before, serializeReport postInstReport)

        let json = (resolve Json interactiveColor postInstReport).Text
        let text = (resolve Text nonInteractive postInstReport).Text
        let rich = (resolve Rich interactiveColor postInstReport).Text

        Assert.Contains("\"repoInitOutcome\": \"initialized\"", json)
        Assert.Contains("\"executableScriptCount\": 1", json)
        Assert.Contains("scaffoldRepoInit: initialized", text)
        Assert.Contains("scaffoldExecutableScripts: 1", text)

        for projection in [ json; text; rich ] do
            Assert.Contains("initialized", projection)
            Assert.Contains("run.sh", projection)

    // Feature 052 T024 (US2 / FR-007 / SC-002): the behind-minimum advisory is a pure
    // projection over one report — the same fact appears in json (diagnostics[] id +
    // message), text (diagnostics count), and rich (diagnostics table), the rich path
    // changes no JSON byte, and rich redirected degrades to zero-ANSI == --text.
    let private behindReport: CommandReport =
        { report with
            Diagnostics = [ scaffoldCliBehindMinimum "0.2.1" "0.3.0" ]
            NextAction =
                Some
                    { ActionId = "reseedSeededSkills"
                      Command = Some Init
                      WorkId = None
                      Reason =
                        "Installed fsgg-sdd is behind the provider-declared minimum. Upgrade the CLI, then re-run `fsgg-sdd init` to re-seed the fs-gg-sdd-* skills and .fsgg/early-stage-guidance.md (idempotent, no-clobber). Note: fsgg-sdd refresh does not re-seed."
                      RequiredArtifacts =
                        [ ".claude/skills"; ".codex/skills"; ".fsgg/early-stage-guidance.md" ]
                      BlockingDiagnosticIds = [] } }

    [<Fact>]
    let ``behind-minimum advisory is fact-identical across json text and rich`` () =
        // Rich is a pure projection: it changes no JSON byte.
        let before = serializeReport behindReport
        resolve Rich interactiveColor behindReport |> ignore
        Assert.Equal(before, serializeReport behindReport)

        let json = (resolve Json interactiveColor behindReport).Text
        let text = (resolve Text nonInteractive behindReport).Text
        let rich = (resolve Rich interactiveColor behindReport).Text

        // JSON carries the full advisory: id, installed, minimum, gap.
        Assert.Contains("scaffold.cliBehindMinimum", json)
        Assert.Contains("0.2.1", json)
        Assert.Contains("0.3.0", json)
        Assert.Contains("behind by 1 minor version", json)
        // Text surfaces it via the diagnostics count.
        Assert.Contains("diagnostics: 1", text)
        // Rich surfaces it via the diagnostics table (installed + minimum + gap).
        Assert.Contains("0.2.1", rich)
        Assert.Contains("0.3.0", rich)

        // Feature 052 T026 (US3): the reseed next-action pointer appears in every projection.
        Assert.Contains("reseedSeededSkills", json)
        Assert.Contains("nextAction: reseedSeededSkills", text)
        Assert.Contains("reseedSeededSkills", rich)

    [<Fact>]
    let ``behind-minimum advisory rich redirected equals the text projection with zero ANSI`` () =
        let result = resolve Rich nonInteractive behindReport
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText behindReport, result.Text)
        Assert.DoesNotContain("[", result.Text)

    // 031 T019 (FR-006 / US2.4): the app-only produced-path facts of a lifecycle=sdd
    // run (including the recording manifest) are identical across json/text/rich, and
    // the rich projection adds and drops no JSON byte.
    // 054 T014 (US2 / FR-004 / SC-003): on a provider defect the four provider-output facts
    // (command line, stdout, stderr, exit code) appear identically in json/text/rich; rich
    // redirected equals --text with zero ANSI; and the rich path changes no JSON byte.
    let private defectReport: CommandReport =
        { report with
            Outcome = CommandOutcome.Blocked
            Scaffold =
                Some
                    { scaffoldSummary with
                        Outcome = "providerFailed"
                        ProviderInvocation =
                            Some
                                { CommandLine = "dotnet new fsgg-fixture-app -o . --productName Acme"
                                  ProcessStarted = true
                                  ExitCode = Some 127
                                  StandardOutput = "produced partial output"
                                  StandardOutputTruncated = false
                                  StandardError = "option --productName was not recognized"
                                  StandardErrorTruncated = false } } }

    [<Fact>]
    let ``scaffold provider-defect output facts are identical across json text and rich`` () =
        // Rich is a pure projection: it changes no JSON byte.
        let before = serializeReport defectReport
        resolve Rich interactiveColor defectReport |> ignore
        Assert.Equal(before, serializeReport defectReport)

        let json = (resolve Json interactiveColor defectReport).Text
        let text = (resolve Text nonInteractive defectReport).Text
        let rich = (resolve Rich interactiveColor defectReport).Text

        // JSON carries the structured block…
        Assert.Contains("\"commandLine\": \"dotnet new fsgg-fixture-app -o . --productName Acme\"", json)
        Assert.Contains("\"exitCode\": 127", json)
        Assert.Contains("option --productName was not recognized", json)
        // …text carries the single-line key/value pairs…
        Assert.Contains("scaffoldProviderCommandLine: dotnet new fsgg-fixture-app -o . --productName Acme", text)
        Assert.Contains("scaffoldProviderExitCode: 127", text)
        Assert.Contains("scaffoldProviderStderr: option --productName was not recognized", text)

        // …and every projection (rich reuses the plain lines) carries the same four facts.
        for projection in [ json; text; rich ] do
            Assert.Contains("dotnet new fsgg-fixture-app -o . --productName Acme", projection)
            Assert.Contains("127", projection)
            Assert.Contains("produced partial output", projection)
            Assert.Contains("option --productName was not recognized", projection)

    [<Fact>]
    let ``scaffold provider-defect output rich redirected equals the text projection with zero ANSI`` () =
        let result = resolve Rich nonInteractive defectReport
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText defectReport, result.Text)
        Assert.DoesNotContain("[", result.Text)

    [<Fact>]
    let ``scaffold lifecycle produced-path facts are identical across json text and rich`` () =
        let lifecycleProducedPaths = [ "App.fsproj"; "Program.fs"; "scaffold-manifest.txt" ]
        let lifecycleReport: CommandReport =
            { report with
                Scaffold =
                    Some
                        { scaffoldSummary with
                            ProducedPathCount = lifecycleProducedPaths.Length
                            ProducedPaths = lifecycleProducedPaths } }

        // Rich is a pure projection: it changes no JSON byte.
        let before = serializeReport lifecycleReport
        resolve Rich interactiveColor lifecycleReport |> ignore
        Assert.Equal(before, serializeReport lifecycleReport)

        let json = (resolve Json interactiveColor lifecycleReport).Text
        let text = (resolve Text nonInteractive lifecycleReport).Text
        let rich = (resolve Rich interactiveColor lifecycleReport).Text

        for path in lifecycleProducedPaths do
            for projection in [ json; text; rich ] do
                Assert.Contains(path, projection)

    // 055 T008/T014 (US1/US3 / Scenario E / FR-004/008, SC-004): a co-tenant provider skill under
    // the shared root is a produced-product path like any other — it renders identically across
    // json ≡ text ≡ rich (listed under producedPaths, never as an SDD-owned/skeleton entry), and
    // the rich path adds/drops no JSON byte. This proves the change is additive: the co-tenant path
    // flows through the existing producedPaths projection with no new or reshaped field.
    [<Fact>]
    let ``scaffold co-tenant skill produced-path facts are identical across json text and rich`` () =
        let cotenantProducedPaths = [ ".claude/skills/fs-gg-elmish/SKILL.md"; "App.fsproj"; "Program.fs" ]
        let cotenantReport: CommandReport =
            { report with
                Scaffold =
                    Some
                        { scaffoldSummary with
                            ProducedPathCount = cotenantProducedPaths.Length
                            ProducedPaths = cotenantProducedPaths } }

        // Rich is a pure projection: it changes no JSON byte (additive-only shape).
        let before = serializeReport cotenantReport
        resolve Rich interactiveColor cotenantReport |> ignore
        Assert.Equal(before, serializeReport cotenantReport)

        let json = (resolve Json interactiveColor cotenantReport).Text
        let text = (resolve Text nonInteractive cotenantReport).Text
        let rich = (resolve Rich interactiveColor cotenantReport).Text

        // The co-tenant skill is a produced-product path in json/text and appears in every projection.
        Assert.Contains("\"producedPaths\"", json)
        Assert.Contains("scaffoldProducedPath: .claude/skills/fs-gg-elmish/SKILL.md", text)
        for path in cotenantProducedPaths do
            for projection in [ json; text; rich ] do
                Assert.Contains(path, projection)
