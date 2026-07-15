namespace FS.GG.SDD.Cli.Tests

open System.IO
open Spectre.Console
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Cli.Rendering
open Xunit

/// Rich Spectre.Console projection of the `validation-report` (feature 021).
/// Rendered to a color-off, fixed-width Spectre console (the established
/// `RichRenderingTests` pattern) so assertions see the bare status tokens that
/// differentiate failing vs non-failing cells once color is stripped.
module ValidationRichRenderingTests =

    // ----- T004: color-off console harness + fixture builder -----

    /// A color-off, fixed-width Spectre console backed by a StringWriter. A wide
    /// width keeps the short fixture lines from wrapping so `Contains` assertions
    /// see contiguous tokens.
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

    let render (report: ValidationReport) =
        let writer, console = makeConsole 200
        renderValidationRichTo console report
        writer.ToString()

    let private generator: GeneratorVersion = { Id = "fsgg-sdd"; Version = "1.0.0" }

    let private failDiagnostic (message: string) : Diagnostic =
        { Id = "VALIDATION-CELL-DIVERGENCE"
          Severity = DiagnosticError
          Artifact = None
          Location = None
          Message = message
          Correction = "re-run the cell"
          RelatedIds = []
          IsToolDefect = false
          DefectTag = None }

    let private cell coordinates status : MatrixCell =
        { Coordinates = coordinates
          Status = status }

    let private report (matrices: Matrix list) : ValidationReport =
        { SchemaVersion = 1
          GeneratorVersion = generator
          Matrices = matrices
          Summary = summarize matrices
          Sensed = emptySensed }

    /// A mix of all five statuses across two matrices (short coordinate values so
    /// nothing wraps at width 200).
    let private lifecycleMatrix: Matrix =
        { Name = "lifecycle-output"
          Dimensions = [ "command"; "projection" ]
          Cells =
            [ cell [ "command", "specify"; "projection", "json" ] Pass
              cell [ "command", "verify"; "projection", "text" ] (Fail(failDiagnostic "verify text diverged"))
              cell [ "command", "ship"; "projection", "rich" ] (SkippedWithReason "rich deferred") ] }

    let private determinismMatrix: Matrix =
        { Name = "determinism"
          Dimensions = [ "output"; "environment" ]
          Cells =
            [ cell [ "output", "workmodel"; "environment", "colorDisabled" ] Pass
              cell [ "output", "summary"; "environment", "termDumb" ] (CoverageGap "summary surface")
              cell [ "output", "audit"; "environment", "interactive" ] (NotValidated "not run") ] }

    let private mixedReport = report [ lifecycleMatrix; determinismMatrix ]

    // ----- T005: projection completeness (INV-5 / C-2 / SC-004) -----

    [<Fact>]
    let ``T005 rich projection surfaces verdict, all five counts, matrices, and non-passing cells`` () =
        let text = render mixedReport

        // Overall verdict (mixed report is not passing).
        Assert.Contains("Verdict", text)
        Assert.Contains("not passed", text)

        // All five summary counts.
        Assert.Contains("passed=2", text)
        Assert.Contains("failed=1", text)
        Assert.Contains("skipped=1", text)
        Assert.Contains("coverageGaps=1", text)
        Assert.Contains("notValidated=1", text)

        // Each matrix name and its dimensions.
        Assert.Contains("lifecycle-output", text)
        Assert.Contains("determinism", text)
        Assert.Contains("dimensions:", text)
        Assert.Contains("command", text)
        Assert.Contains("projection", text)
        Assert.Contains("output", text)
        Assert.Contains("environment", text)

        // Every non-passing cell's coordinates + status token.
        Assert.Contains("command=verify", text)
        Assert.Contains("projection=text", text)
        Assert.Contains("fail", text)
        Assert.Contains("command=ship", text)
        Assert.Contains("skipped", text)
        Assert.Contains("output=summary", text)
        Assert.Contains("coverageGap", text)
        Assert.Contains("output=audit", text)
        Assert.Contains("notValidated", text)

        // The Fail cell's diagnostic message.
        Assert.Contains("verify text diverged", text)

    [<Fact>]
    let ``T005 pass cells are summarized, not enumerated, and no foreign fact appears`` () =
        let text = render mixedReport

        // Pass coordinates are not listed individually (kept scannable).
        Assert.DoesNotContain("command=specify", text)
        Assert.DoesNotContain("output=workmodel", text)

        // A coordinate value absent from the report must not appear.
        Assert.DoesNotContain("command=delete", text)
        Assert.DoesNotContain("invented", text)

    [<Fact>]
    let ``T005 missing schemaVersion or generatorVersion is not a completeness failure`` () =
        // The rich projection intentionally omits envelope metadata; their absence
        // is not an omission (C-2). Rendering still succeeds and is non-empty.
        let text = render mixedReport
        Assert.False(System.String.IsNullOrWhiteSpace text)

    // ----- T006: verdict + status differentiation (INV-6 / C-3 / FR-007) -----

    [<Fact>]
    let ``T006 an all-pass report renders a passed verdict with no invented diagnostics`` () =
        let allPass =
            report
                [ { lifecycleMatrix with
                      Cells =
                          [ cell [ "command", "specify"; "projection", "json" ] Pass
                            cell [ "command", "verify"; "projection", "text" ] Pass ] } ]

        let text = render allPass
        Assert.Contains("passed", text)
        Assert.DoesNotContain("not passed", text)
        // No failing cells invented for an all-pass run: the counts are zero and the
        // matrix section reports every evaluated cell as passing. (The rollup column
        // headers `coverageGap`/`notValidated` are structural, not cell facts.)
        Assert.Contains("failed=0", text)
        Assert.Contains("coverageGaps=0", text)
        Assert.Contains("all evaluated cells pass", text)
        Assert.Contains("lifecycle-output", text)

    [<Fact>]
    let ``T006 coverageGap and notValidated render a not-passed verdict, distinct from skipped`` () =
        let gapsOnly =
            report
                [ { Name = "determinism"
                    Dimensions = [ "output"; "environment" ]
                    Cells =
                      [ cell [ "output", "workmodel"; "environment", "interactive" ] Pass
                        cell [ "output", "summary"; "environment", "termDumb" ] (CoverageGap "summary surface")
                        cell [ "output", "audit"; "environment", "colorDisabled" ] (NotValidated "not run")
                        cell [ "output", "ship"; "environment", "interactive" ] (SkippedWithReason "deferred") ] } ]

        let text = render gapsOnly
        // The only non-passing cells fail the run despite there being no `Fail`.
        Assert.Contains("not passed", text)
        // The failing tokens are present and distinct from the non-failing skipped token.
        Assert.Contains("coverageGap", text)
        Assert.Contains("notValidated", text)
        Assert.Contains("skipped", text)

    // ----- T007: single-failing-cell isolation (C-2) -----

    [<Fact>]
    let ``T007 a single failing cell is isolated; passing siblings are not listed`` () =
        let oneFail =
            report
                [ { Name = "lifecycle-output"
                    Dimensions = [ "command"; "projection" ]
                    Cells =
                      [ cell [ "command", "specify"; "projection", "json" ] Pass
                        cell [ "command", "verify"; "projection", "text" ] (Fail(failDiagnostic "only this diverged"))
                        cell [ "command", "ship"; "projection", "rich" ] Pass ] } ]

        let text = render oneFail
        Assert.Contains("not passed", text)
        Assert.Contains("command=verify", text)
        Assert.Contains("only this diverged", text)
        // Passing siblings are not enumerated as failing.
        Assert.DoesNotContain("command=specify", text)
        Assert.DoesNotContain("command=ship", text)

    // ----- T009: degradation + parity (INV-2 / C-4) -----

    let private interactive: TerminalCapabilities =
        { IsInteractive = true
          ColorEnabled = true
          Width = Some 200
          IsInputInteractive = true }

    let private hasEsc (value: string) =
        value |> Seq.exists (fun c -> int c = 27)

    [<Fact>]
    let ``T009 Rich degrades to exact plain text when non-interactive`` () =
        let caps =
            { interactive with
                IsInteractive = false }

        let result = resolveValidation Rich caps mixedReport
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText mixedReport, result.Text)
        Assert.False(hasEsc result.Text, "degraded output contains an ANSI escape")

    [<Fact>]
    let ``T009 Rich degrades to exact plain text when color disabled`` () =
        let caps =
            { interactive with
                ColorEnabled = false }

        let result = resolveValidation Rich caps mixedReport
        Assert.False(result.UsedRichRendering)
        Assert.Equal(renderText mixedReport, result.Text)
        Assert.False(hasEsc result.Text, "degraded output contains an ANSI escape")

    [<Fact>]
    let ``T009 Rich on an interactive color console renders richly`` () =
        let result = resolveValidation Rich interactive mixedReport
        Assert.True(result.UsedRichRendering)
        Assert.NotEqual<string>(renderText mixedReport, result.Text)

    [<Fact>]
    let ``T009 Json and Text resolve byte-for-byte to serialize and renderText`` () =
        let json = resolveValidation Json interactive mixedReport
        Assert.False(json.UsedRichRendering)
        Assert.Equal(serialize mixedReport, json.Text)

        let plain = resolveValidation Text interactive mixedReport
        Assert.False(plain.UsedRichRendering)
        Assert.Equal(renderText mixedReport, plain.Text)

    // ----- T010: automation invariance (INV-1 / INV-3 / C-5 / SC-002) -----

    [<Fact>]
    let ``T010 resolveValidation never mutates the report's JSON or text projection`` () =
        let jsonBefore = serialize mixedReport
        let textBefore = renderText mixedReport

        resolveValidation Rich interactive mixedReport |> ignore
        resolveValidation Json interactive mixedReport |> ignore
        resolveValidation Text interactive mixedReport |> ignore

        Assert.Equal(jsonBefore, serialize mixedReport)
        Assert.Equal(textBefore, renderText mixedReport)

    [<Fact>]
    let ``T010 the serialized JSON keeps the sensed block normalized to null`` () =
        let json = serialize mixedReport
        Assert.Contains("\"startedAtUtc\": null", json)
        Assert.Contains("\"durationMs\": null", json)
        Assert.Contains("\"host\": null", json)
