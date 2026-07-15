namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Validation.ValidationContracts
open Xunit

/// Feature 088 / FS.GG.SDD#172 — the deterministic, ANSI-free Markdown "report card"
/// projection (`renderMarkdown`). Fact-parity with the rich projection; byte-identical
/// across runs; safe to capture into a log or file.
module ValidationMarkdownTests =

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

    /// One all-pass matrix and one matrix carrying every non-pass status.
    let private passingMatrix: Matrix =
        { Name = "baseline-conformance"
          Dimensions = [ "contract"; "check" ]
          Cells =
            [ cell [ "contract", "a"; "check", "conformance" ] Pass
              cell [ "contract", "b"; "check", "conformance" ] Pass ] }

    let private mixedMatrix: Matrix =
        { Name = "determinism"
          Dimensions = [ "output"; "environment" ]
          Cells =
            [ cell [ "output", "workmodel"; "environment", "interactive" ] Pass
              cell [ "output", "verify"; "environment", "colorDisabled" ] (Fail(failDiagnostic "verify text diverged"))
              cell [ "output", "summary"; "environment", "termDumb" ] (CoverageGap "summary surface")
              cell [ "output", "audit"; "environment", "redirected" ] (NotValidated "not run")
              cell [ "output", "ship"; "environment", "interactive" ] (SkippedWithReason "deferred") ] }

    let private mixedReport = report [ passingMatrix; mixedMatrix ]

    let private hasEsc (value: string) =
        value |> Seq.exists (fun c -> int c = 27)

    // ----- T010: fact-parity -----

    [<Fact>]
    let ``renderMarkdown surfaces verdict, five counts, matrices rollup, and non-passing cells`` () =
        let md = renderMarkdown mixedReport

        Assert.Contains("# Validation Report", md)
        Assert.Contains("**Verdict:** not passed", md)

        // Summary table with the five counts (passed=3, failed=1, skipped=1, gap=1, notValidated=1).
        Assert.Contains("| passed | failed | skipped | coverageGaps | notValidated |", md)
        Assert.Contains("| 3 | 1 | 1 | 1 | 1 |", md)

        // Per-matrix rollup rows.
        Assert.Contains("| baseline-conformance | 2 | 0 | 0 | 0 | 0 |", md)
        Assert.Contains("| determinism | 1 | 1 | 1 | 1 | 1 |", md)

        // Every non-passing cell surfaced with coordinates + token.
        Assert.Contains("- (output=verify, environment=colorDisabled) **fail**: verify text diverged", md)
        Assert.Contains("**coverageGap**: summary surface", md)
        Assert.Contains("**notValidated**: not run", md)
        Assert.Contains("**skipped**: deferred", md)

    [<Fact>]
    let ``renderMarkdown summarizes passing cells and invents no foreign fact`` () =
        let md = renderMarkdown mixedReport

        // The all-pass matrix is summarized, not enumerated.
        Assert.Contains("### baseline-conformance (dimensions: contract, check)", md)
        Assert.Contains("All evaluated cells pass.", md)
        Assert.DoesNotContain("contract=a", md)
        // The one passing cell of the mixed matrix is not enumerated either.
        Assert.DoesNotContain("output=workmodel", md)
        // A coordinate absent from the report never appears.
        Assert.DoesNotContain("output=invented", md)

    // ----- T011: determinism, zero-ANSI, empty/all-pass, optional fields -----

    [<Fact>]
    let ``renderMarkdown is byte-identical across runs`` () =
        Assert.Equal(renderMarkdown mixedReport, renderMarkdown mixedReport)

    [<Fact>]
    let ``renderMarkdown emits zero ANSI`` () =
        Assert.False(hasEsc (renderMarkdown mixedReport), "markdown output contains an ANSI escape")

    [<Fact>]
    let ``an all-pass report renders a passed verdict and enumerates no cell`` () =
        let allPass = report [ passingMatrix ]
        let md = renderMarkdown allPass

        Assert.Contains("**Verdict:** passed", md)
        Assert.Contains("| 2 | 0 | 0 | 0 | 0 |", md)
        Assert.Contains("All evaluated cells pass.", md)
        // Well-formed, non-empty document even with nothing failing.
        Assert.False(System.String.IsNullOrWhiteSpace md)

    [<Fact>]
    let ``an empty report still renders a well-formed document`` () =
        let md = renderMarkdown (report [])
        Assert.Contains("# Validation Report", md)
        Assert.Contains("**Verdict:** passed", md)
        Assert.Contains("## Matrices", md)
        Assert.False(System.String.IsNullOrWhiteSpace md)

    [<Fact>]
    let ``renderMarkdown omits envelope metadata and does not surface sensed data`` () =
        let md = renderMarkdown mixedReport
        // Parity with the rich projection: schemaVersion / generatorVersion are not shown,
        // and no sensed/wall-clock data leaks in.
        Assert.DoesNotContain("schemaVersion", md)
        Assert.DoesNotContain("generatorVersion", md)
        Assert.DoesNotContain("startedAtUtc", md)
        Assert.DoesNotContain("1.0.0", md)

    // ----- T012: table-cell escaping -----

    [<Fact>]
    let ``a pipe in a matrix name is escaped so the rollup table is not broken`` () =
        let piped =
            report
                [ { Name = "weird|name"
                    Dimensions = [ "d" ]
                    Cells = [ cell [ "d", "v" ] Pass ] } ]

        let md = renderMarkdown piped
        Assert.Contains("| weird\\|name |", md)
        Assert.DoesNotContain("| weird|name |", md)
