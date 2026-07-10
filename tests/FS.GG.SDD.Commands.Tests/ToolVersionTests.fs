namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// FS-GG/FS.GG.SDD#305. A stale toolchain used to be invisible in the artifacts it emitted: three
/// separate consumers independently rediscovered one already-fixed defect, the third two days after
/// the fix was tagged. Two facts close that loop — every report names the version that produced it,
/// and a workspace may declare the floor it expects.
module ToolVersionTests =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let private workId = "305-tool-version"
    let private title = "Tool Version"

    let private installedVersion = SchemaVersionModule.currentGeneratorVersion().Version

    /// A project.yml carrying an optional `sdd.minToolVersion` floor. Mirrors the shape `init` seeds,
    /// so these tests exercise the same parse path a real workspace takes.
    let private projectConfig (floor: string option) =
        let floorLine =
            match floor with
            | Some value -> $"\n  minToolVersion: {value}"
            | None -> ""

        $"""schemaVersion: 1
project:
  id: {workId}
  defaultWorkRoot: work
sdd:
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml{floorLine}
"""

    /// Charter over a freshly initialized workspace whose project.yml declares `floor`. Charter is an
    /// arbitrary choice: the floor is checked once at report assembly, so any command that reads the
    /// config would do.
    let private reportWithFloor (floor: string option) =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root ".fsgg/project.yml" (projectConfig floor)
        TestSupport.runCharter root workId title

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)

    let private floorDiagnosticIds (report: CommandReport) =
        diagnosticIds report
        |> List.filter (fun id -> id.StartsWith "project.toolVersion" || id.StartsWith "project.minToolVersion")

    // ----- The report names the version that produced it. -----

    [<Fact>]
    let ``every report records the running fsgg-sdd version`` () =
        let report = reportWithFloor None
        Assert.Equal(installedVersion, report.ToolVersion)

    /// The text projection is what a consumer pastes into a feedback report, so the version has to
    /// survive the trip out of JSON.
    [<Fact>]
    let ``the text projection records the running fsgg-sdd version`` () =
        let text = reportWithFloor None |> FS.GG.SDD.Commands.CommandRendering.renderText
        Assert.Contains($"toolVersion: {installedVersion}", text)

    // ----- The floor. -----

    [<Fact>]
    let ``no declared floor warns nothing`` () =
        let report = reportWithFloor None
        Assert.Empty(floorDiagnosticIds report)

    [<Fact>]
    let ``a floor at the running version warns nothing`` () =
        let report = reportWithFloor (Some installedVersion)
        Assert.Empty(floorDiagnosticIds report)

    [<Fact>]
    let ``a floor below the running version warns nothing`` () =
        let report = reportWithFloor (Some "0.0.1")
        Assert.Empty(floorDiagnosticIds report)

    [<Fact>]
    let ``a floor above the running version warns and names both versions`` () =
        let report = reportWithFloor (Some "999.0.0")

        let diagnostic =
            report.Diagnostics
            |> List.find (fun d -> d.Id = "project.toolVersionBelowMinimum")

        Assert.Equal(DiagnosticSeverity.DiagnosticWarning, diagnostic.Severity)
        // Both versions ride in the message and relatedIds, so a reader never has to guess which side
        // of the comparison moved.
        Assert.Contains(installedVersion, diagnostic.Message)
        Assert.Contains("999.0.0", diagnostic.Message)
        Assert.Equal<string list>([ installedVersion; "999.0.0" ], diagnostic.RelatedIds)

    /// A warning, never an error: a stale tool still produces usable output. Blocking here would wedge
    /// a workspace out of every lifecycle command the moment its floor moved ahead of the installed CLI.
    [<Fact>]
    let ``a floor above the running version warns without blocking`` () =
        let report = reportWithFloor (Some "999.0.0")
        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)

    /// Deliberate, not incidental. The floor warning participates in the ordinary severity rule (any
    /// warning ⇒ `succeededWithWarnings`), which pre-empts `noChange` and therefore clears the #183
    /// clean-advance signal on an otherwise-current re-run. Exempting tool diagnostics from that rule
    /// would be a special case layered on the shared outcome mechanism; instead a workspace running
    /// below its own declared floor is told, on every command, that it is not safe to advance. This
    /// test exists so that consequence cannot be silently reverted or silently introduced.
    [<Fact>]
    let ``a floor warning clears the clean-advance signal on an otherwise-current re-run`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root ".fsgg/project.yml" (projectConfig None)

        // First run authors the charter; the second finds everything current.
        TestSupport.runCharter root workId title |> ignore
        let cleanRerun = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.NoChange, cleanRerun.Outcome)
        Assert.True cleanRerun.Coherent

        // Same workspace, same already-current artifacts — only the declared floor moved.
        TestSupport.writeRelative root ".fsgg/project.yml" (projectConfig (Some "999.0.0"))
        let staleToolRerun = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.SucceededWithWarnings, staleToolRerun.Outcome)
        Assert.False staleToolRerun.Coherent

    /// The floor is read from `sdd.minToolVersion`, not a top-level `minToolVersion`. Issue #305 names
    /// the key without a parent, so this pins the one the parser actually reads — a floor written at the
    /// wrong depth would enforce nothing and warn nothing, which is precisely the silent staleness the
    /// issue exists to close.
    [<Fact>]
    let ``the floor is read from sdd minToolVersion and not from a top-level key`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let topLevelFloor =
            $"""schemaVersion: 1
minToolVersion: 999.0.0
project:
  id: {workId}
  defaultWorkRoot: work
sdd:
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml
"""

        TestSupport.writeRelative root ".fsgg/project.yml" topLevelFloor
        let report = TestSupport.runCharter root workId title

        Assert.Empty(floorDiagnosticIds report)

    /// An unparseable floor enforces nothing. Saying so out loud is the whole point of #305 — a silently
    /// ignored floor is exactly the invisible-staleness failure this issue exists to close.
    [<Fact>]
    let ``an unparseable floor warns rather than silently ignoring it`` () =
        let report = reportWithFloor (Some "banana")

        let diagnostic =
            report.Diagnostics
            |> List.find (fun d -> d.Id = "project.minToolVersionUnparseable")

        Assert.Equal(DiagnosticSeverity.DiagnosticWarning, diagnostic.Severity)
        Assert.Contains("banana", diagnostic.Message)
        // The floor never parsed, so no comparison happened and the below-minimum warning must not fire.
        Assert.DoesNotContain("project.toolVersionBelowMinimum", diagnosticIds report)
