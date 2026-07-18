namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.Internal
open Xunit

/// Feature 105, Phase 3 (ADR-0004 D3). The pure plan-time framework-reference check:
/// `ViewGeneration.frameworkReferenceDiagnostics` over a parsed plan and an INJECTED oracle. Covers
/// the five symmetric verdicts and the version resolver — no I/O, no committed capture needed (the
/// oracle stubs the surface). The analyze wiring is exercised by AnalyzeCommandTests.
module FrameworkReferenceCheckTests =

    let private planWith (contractImpactLine: string) (deferralLine: string) =
        $"""---
schemaVersion: 1
workId: 008-plan-command
title: Plan Command
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/008-plan-command/spec.md
sourceClarifications: work/008-plan-command/clarifications.md
sourceChecklist: work/008-plan-command/checklist.md
publicOrToolFacingImpact: true
---

# Plan Command Plan

## Source Snapshot
- spec: work/008-plan-command/spec.md sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa schemaVersion:1
- clarifications: work/008-plan-command/clarifications.md sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb schemaVersion:1
- checklist: work/008-plan-command/checklist.md sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc schemaVersion:1

## Plan Scope
- Work item 008-plan-command is planned.

## Plan Decisions
- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans.

## Contract Impact
- PC-001 [PD-001] command report: fsgg-sdd plan JSON is tool-facing.
{contractImpactLine}

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run command tests and CLI smoke evidence.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: Plan schemaVersion 1 is accepted.

## Generated View Impact
- GV-001 [PD-001] workModel: readiness/008-plan-command/work-model.json refreshes from plan sources.

## Accepted Deferrals
- CR-002 acceptedDeferral: Deferral remains visible to tasks and evidence.
{deferralLine}

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: tasks.
"""

    let private parse text : PlanFacts =
        match
            parsePlanFacts
                { Path = "work/008-plan-command/plan.md"
                  Text = text }
        with
        | Ok facts -> facts
        | Error diagnostics -> failwith $"plan did not parse: {diagnostics}"

    let private ids diagnostics =
        diagnostics |> List.map (fun (d: Diagnostics.Diagnostic) -> d.Id)

    // Oracle that resolves every reference to `version` and reports `symbols` as the surface (or
    // `None` for unavailable).
    let private oracle version symbols : FrameworkApiReference -> string * Set<string> option =
        fun _ -> version, symbols

    let private useLine = "- framework: Pkg.Sample@1.0.0#useSymbol — a use."

    let private deferralLine =
        "- CR-003 blocked-on-framework: Pkg.Sample@1.0.0#deferSymbol — believed absent."

    let private planPath = "work/008-plan-command/plan.md"

    // ---- USE reference verdicts ----------------------------------------------------------

    [<Fact>]
    let ``use reference present in the real surface passes`` () =
        let facts = parse (planWith useLine "")
        let resolve = oracle "1.0.0" (Some(set [ "useSymbol" ]))
        Assert.Empty(ViewGeneration.frameworkReferenceDiagnostics resolve planPath facts)

    [<Fact>]
    let ``use reference absent from the real surface blocks as dangling`` () =
        let facts = parse (planWith useLine "")
        let resolve = oracle "1.0.0" (Some(set [ "somethingElse" ]))

        let diagnostics =
            ViewGeneration.frameworkReferenceDiagnostics resolve planPath facts

        Assert.Contains("frameworkApiDangling", ids diagnostics)

        Assert.All(diagnostics, fun d -> Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticError, d.Severity))

    // ---- blocked-on deferral verdicts ----------------------------------------------------

    [<Fact>]
    let ``blocked-on deferral contradicted when the symbol is present blocks`` () =
        let facts = parse (planWith "" deferralLine)
        let resolve = oracle "1.0.0" (Some(set [ "deferSymbol" ]))

        let diagnostics =
            ViewGeneration.frameworkReferenceDiagnostics resolve planPath facts

        Assert.Contains("frameworkApiDeferralContradicted", ids diagnostics)

    [<Fact>]
    let ``blocked-on deferral is legitimate when the symbol is absent`` () =
        let facts = parse (planWith "" deferralLine)
        let resolve = oracle "1.0.0" (Some(set [ "unrelated" ]))
        Assert.Empty(ViewGeneration.frameworkReferenceDiagnostics resolve planPath facts)

    // ---- fail-open when no capture ------------------------------------------------------

    [<Fact>]
    let ``no captured surface is advisory, never a block`` () =
        let facts = parse (planWith useLine deferralLine)
        let resolve = oracle "1.0.0" None

        let diagnostics =
            ViewGeneration.frameworkReferenceDiagnostics resolve planPath facts

        Assert.All(
            diagnostics,
            fun d ->
                Assert.Equal("frameworkApiSurfaceUnavailable", d.Id)
                Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticInfo, d.Severity)
        )

        Assert.Equal(2, List.length diagnostics) // one per reference

    // ---- version resolution -------------------------------------------------------------

    let private pin =
        """<Project><ItemGroup>
  <PackageVersion Include="Pkg.Sample" Version="2.3.4" />
</ItemGroup></Project>"""

    [<Fact>]
    let ``explicit reference version wins over the pin`` () =
        let facts = parse (planWith useLine "")
        let reference = List.exactlyOne facts.FrameworkApiReferences
        Assert.Equal(Some "1.0.0", ViewGeneration.resolveFrameworkVersion reference [ pin ])

    [<Fact>]
    let ``an unversioned reference resolves to the CPM pin`` () =
        let facts = parse (planWith "- framework: Pkg.Sample#bareSymbol — no version." "")
        let reference = List.exactlyOne facts.FrameworkApiReferences
        Assert.Equal(None, reference.Version)
        Assert.Equal(Some "2.3.4", ViewGeneration.resolveFrameworkVersion reference [ pin ])

    [<Fact>]
    let ``an unversioned reference with no pin does not resolve`` () =
        let facts = parse (planWith "- framework: Unpinned.Pkg#bareSymbol — no version." "")
        let reference = List.exactlyOne facts.FrameworkApiReferences
        Assert.Equal(None, ViewGeneration.resolveFrameworkVersion reference [ pin ])

/// Feature 105, Phase 3 (SC-001/SC-002/SC-003). The analyze wiring end-to-end: a real work item
/// whose plan cites a `framework:` reference, resolved at the edge against a COMMITTED capture. The
/// oracle binding, the second-wave capture read, and the diagnostic all fire through the real
/// `analyze` run.
module FrameworkReferenceAnalyzeTests =
    open FS.GG.SDD.Artifacts
    open FS.GG.SDD.Commands.CommandTypes
    open TestSupport

    let private workId = "010-analyze-command"
    let private title = "Analyze Command"

    // Inject a Contract Impact `framework:` reference (and optionally a blocked-on deferral) into the
    // tasks-ready fixture's plan. Additive — it introduces no new PD/PC/VO id, so it does not disturb
    // the disposition graph analyze validates.
    let private analyzableFixtureWith (packageId: string) (version: string) (symbol: string) (deferralLine: string) =
        let root = tempDirectory ()
        initializeTasksReadyProject root workId title
        let planPath = $"work/{workId}/plan.md"

        let plan =
            (readRelative root planPath)
                .Replace(
                    "## Contract Impact\n",
                    $"## Contract Impact\n- framework: {packageId}@{version}#{symbol} — a use.\n"
                )
                .Replace("## Accepted Deferrals\n", $"## Accepted Deferrals\n{deferralLine}")

        writeRelative root planPath plan
        root

    let private commitCapture root packageId version symbols =
        let capture = DependencySurface.create packageId version "nuget-cache" symbols

        writeRelative
            root
            (DependencySurface.capturePath DependencySurface.defaultBaselineRoot packageId version)
            (DependencySurface.serialize capture)

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun (d: Diagnostics.Diagnostic) -> d.Id)

    [<Fact>]
    let ``analyze blocks on a dangling framework reference against the committed capture`` () =
        let root = analyzableFixtureWith "Pkg.Sample" "1.0.0" "missingApi" ""
        // The committed capture does NOT contain `missingApi`.
        commitCapture root "Pkg.Sample" "1.0.0" [ "Sample.other"; "Sample.present" ]

        let report = runAnalyze root workId title
        Assert.Contains("frameworkApiDangling", diagnosticIds report)
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

    [<Fact>]
    let ``analyze passes when the cited framework API is present in the capture`` () =
        let root = analyzableFixtureWith "Pkg.Sample" "1.0.0" "presentApi" ""
        // The capture contains the exact cited symbol.
        commitCapture root "Pkg.Sample" "1.0.0" [ "presentApi"; "Sample.other" ]

        let report = runAnalyze root workId title
        Assert.DoesNotContain("frameworkApiDangling", diagnosticIds report)
        Assert.DoesNotContain("frameworkApiSurfaceUnavailable", diagnosticIds report)

    [<Fact>]
    let ``analyze is advisory when no capture is committed`` () =
        let root = analyzableFixtureWith "Pkg.Sample" "1.0.0" "someApi" ""
        // No capture committed under docs/dependency-surface/.

        let report = runAnalyze root workId title
        Assert.Contains("frameworkApiSurfaceUnavailable", diagnosticIds report)
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
