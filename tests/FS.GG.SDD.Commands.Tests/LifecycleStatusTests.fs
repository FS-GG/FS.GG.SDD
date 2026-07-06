namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts.Core
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.Internal
open Xunit

module Footer = FS.GG.SDD.Commands.LifecycleFooter

// Feature 084: pure derivation of the lifecycle-status footer fact from the interpreted directory
// enumerations. These drive `LifecycleSensing.deriveFromEffects` directly with SYNTHETIC directory
// snapshots (disclosed per Principle V) — the same shape the real interpreter produces, exercised
// end-to-end by the CLI in the offline smoke and the command integration tests.
module LifecycleStatusTests =

    // SYNTHETIC: a directory-enumeration result standing in for the interpreter's directorySnapshot
    // (root-relative, forward-slashed, newline-joined paths). The real path is exercised via the CLI.
    let private enumResult (dir: string) (paths: string list) : CommandEffectResult =
        { Effect = EnumerateDirectory dir
          Succeeded = true
          Snapshot =
            Some
                { Path = dir
                  Text = String.concat "\n" paths }
          Process = None
          Confirmed = None
          Diagnostic = None }

    let private stateOf (status: LifecycleStatus) (command: SddCommand) =
        status.Stages
        |> List.find (fun entry -> entry.Command = command)
        |> fun entry -> entry.State

    let private states (status: LifecycleStatus) =
        status.Stages |> List.map (fun entry -> entry.State)

    [<Fact>]
    let ``mid-lifecycle: current stage, prior stages done, successor next`` () =
        let effects =
            [ enumResult "work" [ "work/x/charter.md"; "work/x/spec.md"; "work/x/clarifications.md" ] ]

        let status =
            LifecycleSensing.deriveFromEffects Clarify (Some "x") CommandOutcome.Succeeded effects

        Assert.Equal(StageState.Done, stateOf status Charter)
        Assert.Equal(StageState.Done, stateOf status Specify)
        Assert.Equal(StageState.Current, stateOf status Clarify)
        Assert.Equal(StageState.Next, stateOf status Checklist)
        Assert.Equal(StageState.Pending, stateOf status Plan)
        Assert.Equal(Some 3, status.CurrentOrdinal)
        Assert.Equal(10, status.TotalStages)
        Assert.Equal(Some Checklist, status.NextCommand)
        Assert.True(status.IsLifecycleStage)

    [<Fact>]
    let ``SC-006 non-contiguous progress renders true on-disk state`` () =
        // A later stage's artifact present while an earlier one is absent.
        let effects =
            [ enumResult "work" [ "work/x/charter.md"; "work/x/spec.md" ]
              enumResult "readiness" [ "readiness/x/verify.json" ] ]

        let status =
            LifecycleSensing.deriveFromEffects Ship (Some "x") CommandOutcome.Succeeded effects

        Assert.Equal(StageState.Done, stateOf status Verify) // present
        Assert.Equal(StageState.Pending, stateOf status Evidence) // absent — not assumed done
        Assert.Equal(StageState.Pending, stateOf status Analyze)

    [<Fact>]
    let ``FR-011 cross-cutting command is not a lifecycle stage and marks no current`` () =
        let effects = [ enumResult "work" [ "work/x/charter.md"; "work/x/spec.md" ] ]

        let status =
            LifecycleSensing.deriveFromEffects Refresh (Some "x") CommandOutcome.Succeeded effects

        Assert.False(status.IsLifecycleStage)
        Assert.DoesNotContain(StageState.Current, states status)
        Assert.Equal(StageState.Done, stateOf status Charter)
        Assert.Equal(StageState.Next, stateOf status Clarify) // lowest pending after the done frontier
        Assert.Equal(Some 2, status.CurrentOrdinal) // count of done stages

    [<Fact>]
    let ``blocked outcome marks the current stage blocked, not current`` () =
        let effects = [ enumResult "work" [ "work/x/charter.md"; "work/x/spec.md" ] ]

        let status =
            LifecycleSensing.deriveFromEffects Clarify (Some "x") CommandOutcome.Blocked effects

        Assert.Equal(StageState.Blocked, stateOf status Clarify)
        Assert.DoesNotContain(StageState.Current, states status)

    [<Fact>]
    let ``FR-012 no work id renders a coherent all-pending footer`` () =
        let status =
            LifecycleSensing.deriveFromEffects Init None CommandOutcome.Succeeded []

        Assert.Equal(None, status.WorkId)
        Assert.False(status.IsLifecycleStage)
        // Nothing sensed done; the first stage is the frontier "next", the rest pending.
        Assert.Equal(StageState.Next, stateOf status Charter)

        Assert.True(
            status.Stages
            |> List.forall (fun e -> e.State = StageState.Next || e.State = StageState.Pending)
        )

        Assert.Equal(Some Charter, status.NextCommand)

    [<Fact>]
    let ``deterministic: same inputs yield the same status`` () =
        let effects = [ enumResult "work" [ "work/x/charter.md" ] ]

        let first =
            LifecycleSensing.deriveFromEffects Specify (Some "x") CommandOutcome.Succeeded effects

        let second =
            LifecycleSensing.deriveFromEffects Specify (Some "x") CommandOutcome.Succeeded effects

        Assert.Equal<StageState list>(states first, states second)
        Assert.Equal(first.CurrentOrdinal, second.CurrentOrdinal)

    [<Fact>]
    let ``T022 sensing is presence-only: a listed stage is done regardless of content`` () =
        // The enumeration lists the path; content is never read, so a malformed body cannot break it.
        let effects = [ enumResult "work" [ "work/x/charter.md" ] ]

        let status =
            LifecycleSensing.deriveFromEffects Specify (Some "x") CommandOutcome.Succeeded effects

        Assert.Equal(StageState.Done, stateOf status Charter)

    // ----- MVU edge: what the pure plan step emits (T008 / T028) -----

    [<Fact>]
    let ``T028 sensing effects touch only the work and readiness roots`` () =
        let effects = LifecycleSensing.lifecycleSensingEffects "084-demo"
        Assert.Equal<CommandEffect list>([ EnumerateDirectory "work"; EnumerateDirectory "readiness" ], effects)

    [<Fact>]
    let ``T008 plan emits the lifecycle sensing enumerations for a stage command`` () =
        let root = TestSupport.tempDirectory ()

        let request =
            { TestSupport.request Clarify root with
                WorkId = Some "084-demo" }

        let _, effects = Foundation.plan request
        Assert.Contains(EnumerateDirectory "work", effects)
        Assert.Contains(EnumerateDirectory "readiness", effects)

    // ----- Integration through the real command pipeline (T012 / T015 / T017 / T027) -----

    let private specifiedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "084-demo" "Demo" |> ignore
        root

    [<Fact>]
    let ``T012 a real specify run senses charter done and specify current`` () =
        let root = specifiedProject ()
        let report = TestSupport.runSpecify root "084-demo" "Demo"
        let status = report.LifecycleStatus
        Assert.Equal(Some "084-demo", status.WorkId)
        Assert.Equal(StageState.Done, stateOf status Charter)
        Assert.Equal(StageState.Current, stateOf status Specify)
        Assert.Equal(StageState.Next, stateOf status Clarify)
        Assert.Equal(Some 2, status.CurrentOrdinal)
        Assert.True(status.IsLifecycleStage)

    [<Fact>]
    let ``T013 the text footer stages and next lines are exact for a known state`` () =
        let root = specifiedProject ()
        let report = TestSupport.runSpecify root "084-demo" "Demo"
        let footer = Footer.plainLines report
        // Deterministic contract content (golden-equivalent): the sensed rail + successor.
        Assert.Contains(
            "stages: charter=done specify=current clarify=next checklist=pending plan=pending tasks=pending analyze=pending evidence=pending verify=pending ship=pending",
            footer
        )

        Assert.Contains("next: fsgg-sdd clarify", footer)

    [<Fact>]
    let ``T015 json lifecycleStatus and text footer carry identical stage facts`` () =
        let root = specifiedProject ()
        let report = TestSupport.runSpecify root "084-demo" "Demo"

        let stagesLine =
            (renderText report).Split('\n')
            |> Array.find (fun line -> line.StartsWith "stages: ")
            |> fun line -> line.TrimEnd()

        let fromJsonFacts =
            report.LifecycleStatus.Stages
            |> List.map (fun entry -> $"{commandName entry.Command}={Footer.stageStateToken entry.State}")
            |> String.concat " "

        Assert.Equal("stages: " + fromJsonFacts, stagesLine)

    [<Fact>]
    let ``T027 every produced report ends with the footer and carries lifecycleStatus`` () =
        let root = specifiedProject ()

        let reports =
            [ TestSupport.runSpecify root "084-demo" "Demo"
              TestSupport.runRequest
                  { TestSupport.request Refresh root with
                      WorkId = Some "084-demo" } ]

        for report in reports do
            Assert.Contains("\"lifecycleStatus\"", serializeReport report)
            let rendered = (renderText report).TrimEnd().Split('\n')
            let footerLines = Footer.plainLines report
            Assert.Equal(List.last footerLines, Array.last rendered)

    [<Fact>]
    let ``T017 blocked footer surfaces explanation and options traceable to existing facts`` () =
        // Clarify with no spec authored blocks; the footer's why/fix/options must reuse the report's
        // own diagnostics/next-action — nothing fabricated (FR-017 / SC-007).
        let root = specifiedProject ()
        let report = TestSupport.runClarify root "084-demo" "Demo"
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let lines = (renderText report).Split('\n')
        Assert.Contains("blocked: clarify", lines |> Array.map (fun l -> l.TrimEnd()))

        let whyLine = lines |> Array.tryFind (fun line -> line.StartsWith "why: ")
        Assert.True(whyLine.IsSome)
        let whyMessage = whyLine.Value.Substring(5).TrimEnd()
        // The explanation is a real blocking-diagnostic message, not invented.
        Assert.Contains(whyMessage, report.Diagnostics |> List.map (fun d -> d.Message))
        Assert.Contains("options: fsgg-sdd", (renderText report))
