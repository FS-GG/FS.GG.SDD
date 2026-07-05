namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// Feature 081 — the skill↔gate doctest. Binds the authored `fs-gg-sdd-*` stage skills
/// (what an author writes FROM) to the compiled `fsgg-sdd` gates (what accepts/rejects),
/// so following a skill verbatim can never produce a form the gate rejects (epic #140).
///
/// Two guarantees:
///   1. The copyable example corpus under docs/examples/lifecycle-artifacts/ passes the
///      REAL gates (charter→analyze), run through the in-process command loop.
///   2. Each stage skill's marked example is consistent with that gate-run corpus
///      (`contains`/`equals` bind the exact load-bearing bytes; `ref` guarantees the
///      corpus file it points at is gate-clean).
/// Plus the evidence-deferral gate rule (#142) is pinned directly.
module SkillGateDoctestTests =

    let private skillsDir = Path.Combine(TestSupport.repoRoot, ".claude", "skills")
    let private corpusDir = Path.Combine(TestSupport.repoRoot, "docs", "examples", "lifecycle-artifacts")

    /// The stage skills that document a HAND-AUTHORED gated artifact (FR-004 coverage set).
    /// analyze/verify/ship emit generated readiness views, not an authored artifact you
    /// hand-write, so they carry no author-facing example to gate — deliberately exempt.
    let private gatedStageSkills =
        [ "fs-gg-sdd-charter"
          "fs-gg-sdd-specify"
          "fs-gg-sdd-clarify"
          "fs-gg-sdd-checklist"
          "fs-gg-sdd-plan"
          "fs-gg-sdd-tasks"
          "fs-gg-sdd-evidence" ]

    type private Marker =
        { Skill: string
          Corpus: string option
          Mode: string
          Block: string }

    // <!-- fsgg-sdd:example <attrs> --> immediately followed by a fenced code block.
    let private markerRegex =
        Regex(
            @"<!--\s*fsgg-sdd:example\s+(?<attrs>.*?)-->\s*\r?\n```[^\n]*\r?\n(?<body>.*?)\r?\n```",
            RegexOptions.Singleline
        )

    let private parseAttrs (raw: string) =
        raw.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun (corpus, mode) (tok: string) ->
                if tok = "counter" then corpus, "counter"
                elif tok.StartsWith "corpus=" then Some(tok.Substring 7), mode
                elif tok.StartsWith "mode=" then corpus, tok.Substring 5
                else corpus, mode)
            (None, "contains")

    let private markersFor skill =
        let text = File.ReadAllText(Path.Combine(skillsDir, skill, "SKILL.md"))

        [ for m in markerRegex.Matches text do
              let corpus, mode = parseAttrs m.Groups.["attrs"].Value

              { Skill = skill
                Corpus = corpus
                Mode = mode
                Block = m.Groups.["body"].Value } ]

    let private normalize (s: string) =
        s.Replace("\r\n", "\n").Split('\n')
        |> Array.map (fun l -> l.TrimEnd())
        |> String.concat "\n"
        |> fun t -> t.Trim()

    let private allMarkers = gatedStageSkills |> List.collect markersFor

    let private errorIds (report: CommandReport) =
        report.Diagnostics
        |> List.filter (fun d -> d.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError)
        |> List.map (fun d -> d.Id)

    // FR-004: no silently unexercised stage skill.
    [<Fact>]
    let ``Every gated stage skill contributes at least one marked example`` () =
        for skill in gatedStageSkills do
            let positive = markersFor skill |> List.filter (fun m -> m.Mode <> "counter")

            Assert.True(
                not (List.isEmpty positive),
                $"{skill}/SKILL.md has no <!-- fsgg-sdd:example --> marker; every gated stage skill must ship a runnable example (FR-004)."
            )

    // FR-006/007/008: the marked example is bound to the gate-run corpus.
    [<Fact>]
    let ``Each marked example is consistent with its corpus file`` () =
        for m in allMarkers do
            if m.Mode <> "counter" then
                let corpus =
                    m.Corpus
                    |> Option.defaultWith (fun () -> failwith $"{m.Skill}: example marker is missing corpus=<file>.")

                let corpusPath = Path.Combine(corpusDir, corpus)
                Assert.True(File.Exists corpusPath, $"{m.Skill}: corpus file {corpus} does not exist.")
                let corpusText = normalize (File.ReadAllText corpusPath)
                let block = normalize m.Block

                match m.Mode with
                | "ref" -> () // pointer only; the gate-run test below proves the corpus file
                | "equals" ->
                    Assert.True(
                        (corpusText = block),
                        $"{m.Skill}: marked example is not byte-equal to {corpus} (mode=equals)."
                    )
                | "contains" ->
                    Assert.True(
                        corpusText.Contains block,
                        $"{m.Skill}: marked example is not a verbatim fragment of {corpus} (mode=contains). The skill teaches a form the gate-run corpus does not contain."
                    )
                | other -> failwith $"{m.Skill}: unknown example marker mode '{other}'."

    // FR-001/002/003: the corpus authored sources pass the REAL gates through analyze.
    [<Fact>]
    let ``Corpus authored sources pass the real gates through analyze`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        let workId = "001-example"
        let title = "Example Work Item"

        // Seed the pure AUTHORED corpus artifacts (charter/spec/clarifications/tasks/evidence).
        // checklist.md and plan.md are generated VIEWS the commands regenerate from these, so
        // they are intentionally not seeded — the gates produce them fresh with current digests.
        for f in [ "charter.md"; "spec.md"; "clarifications.md"; "tasks.yml"; "evidence.yml" ] do
            TestSupport.writeRelative root $"work/{workId}/{f}" (File.ReadAllText(Path.Combine(corpusDir, f)))

        let assertClean label (report: CommandReport) =
            let errs = errorIds report

            Assert.True(
                report.Outcome <> CommandOutcome.Blocked && List.isEmpty errs,
                $"Gate '{label}' blocked on the example corpus: outcome={report.Outcome}, errors={errs}. The corpus (and the skill examples bound to it) would not pass its own gate."
            )

        assertClean "charter" (TestSupport.runCharter root workId title)
        assertClean "checklist" (TestSupport.runChecklist root workId title)
        assertClean "plan" (TestSupport.runPlan root workId title)
        assertClean "tasks" (TestSupport.runTasks root workId title)
        assertClean "analyze" (TestSupport.runAnalyze root workId title)

    // #142: the evidence gate's deferral rule — a COMPLETE four-field deferral is accepted;
    // one MISSING a field blocks with evidence.missingDeferralRationale. The evidence skill
    // documents exactly these four fields; here we pin the gate behaviour they describe.
    let private ladderWithDeferral (includeVisibility: bool) =
        let passes =
            [ for i in 1..5 ->
                  sprintf
                      "  - id: EV%03d\n    kind: verification\n    subject:\n      type: task\n      id: T%03d\n    result: pass"
                      i
                      i ]

        let visibilityLine =
            if includeVisibility then
                "\n    laterLifecycleVisibility: Re-open as a follow-on work item."
            else
                ""

        let deferral =
            "  - id: EV006\n    kind: deferral\n    subject:\n      type: task\n      id: T006\n    result: deferred\n    synthetic: false\n    rationale: Out of scope for this work item.\n    owner: codex\n    scope: the deferred capability"
            + visibilityLine

        "schemaVersion: 1\nevidence:\n" + String.concat "\n" (passes @ [ deferral ]) + "\n"

    let private runEvidenceWith (evidenceYaml: string) =
        let root = TestSupport.tempDirectory ()
        let workId = "001-example"
        let title = "Example Work Item"
        TestSupport.initializeAnalyzedProject root workId title
        TestSupport.writeRelative root $"work/{workId}/evidence.yml" evidenceYaml
        TestSupport.runEvidence root workId title

    [<Fact>]
    let ``A four-field deferral is accepted by the evidence gate`` () =
        let report = runEvidenceWith (ladderWithDeferral true)

        Assert.DoesNotContain(
            "evidence.missingDeferralRationale",
            errorIds report
        )

    [<Fact>]
    let ``A deferral missing laterLifecycleVisibility is rejected by the evidence gate`` () =
        let report = runEvidenceWith (ladderWithDeferral false)

        Assert.Contains(
            "evidence.missingDeferralRationale",
            errorIds report
        )
