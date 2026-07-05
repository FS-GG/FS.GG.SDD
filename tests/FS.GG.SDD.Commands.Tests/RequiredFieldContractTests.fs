namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open Xunit

/// Feature 081 (#140, FR-009): the required-field statements in the authored skills and the
/// authoring-contracts §5 table are CHECKED AGAINST the typed RequiredKeys registry, and the
/// registry is behaviourally confirmed against the parsers. So a gate-required field can never
/// go undocumented (#142 deferral fields, #143 clarify sourceSpec), and a field an author reads
/// about is always one the gate enforces.
module RequiredFieldContractTests =

    let private skillText (root: string) skill =
        File.ReadAllText(Path.Combine(TestSupport.repoRoot, root, "skills", skill, "SKILL.md"))

    let private authoringContracts root =
        skillText root "fs-gg-sdd-authoring-contracts"

    /// The §5 table's **Gating fields** cell for a stage — the SECOND markdown column only, so a
    /// field documented merely in the third "Defaulted (not gating)" column is NOT accepted as
    /// gating (the load-bearing distinction this check exists to protect).
    let private gatingCell (text: string) (stageLabel: string) =
        let row =
            text.Replace("\r\n", "\n").Split('\n')
            |> Array.tryFind (fun line -> line.TrimStart().StartsWith("| " + stageLabel + " "))
            |> Option.defaultWith (fun () ->
                failwith $"authoring-contracts §5 table has no row for stage '{stageLabel}'.")

        // `| stage | gating fields | defaulted |` → cells: ["", " stage ", " gating ", " defaulted ", ""]
        let cells = row.Split('|')

        if cells.Length < 3 then
            failwith $"authoring-contracts §5 table row '{stageLabel}' is malformed: {row}"

        cells.[2]

    // #142: the evidence skill documents every gate-required deferral field, in both roots.
    [<Theory>]
    [<InlineData(".claude")>]
    [<InlineData(".codex")>]
    let ``The evidence skill documents every required deferral field`` (root: string) =
        let text = skillText root "fs-gg-sdd-evidence"

        for key in RequiredKeys.requiredDeferralKeys do
            Assert.True(
                text.Contains key,
                $"{root}/fs-gg-sdd-evidence/SKILL.md does not document the required deferral field '{key}' (RequiredKeys.requiredDeferralKeys)."
            )

    // #143: the clarify skill names every required clarify front-matter field (incl. sourceSpec).
    [<Theory>]
    [<InlineData(".claude")>]
    [<InlineData(".codex")>]
    let ``The clarify skill names every required front-matter field`` (root: string) =
        let text = skillText root "fs-gg-sdd-clarify"

        for key in RequiredKeys.requiredFrontMatterKeys Clarify do
            Assert.True(
                text.Contains key,
                $"{root}/fs-gg-sdd-clarify/SKILL.md does not name the required front-matter field '{key}' (RequiredKeys.requiredFrontMatterKeys Clarify)."
            )

    // FR-009 reconciliation: every registry key appears in the authoring-contracts §5 row for
    // its stage — the human enumeration can never omit a gate-required key.
    [<Theory>]
    [<InlineData(".claude")>]
    [<InlineData(".codex")>]
    let ``The authoring-contracts table lists every registry front-matter key`` (root: string) =
        let text = authoringContracts root

        let stages =
            [ "charter", Charter
              "specify", Specify
              "clarify", Clarify
              "checklist", Checklist
              "plan", Plan ]

        for label, stage in stages do
            let cell = gatingCell text label

            for key in RequiredKeys.requiredFrontMatterKeys stage do
                Assert.True(
                    cell.Contains key,
                    $"authoring-contracts §5 table row '{label}' does not list the gate-required field '{key}' in its Gating fields column."
                )

    // Every registry deferral key is documented in the skill as a backtick-wrapped field, in
    // registry order (catches a missing or reordered field; not an extra-field check).
    [<Theory>]
    [<InlineData(".claude")>]
    [<InlineData(".codex")>]
    let ``The evidence skill documents the deferral fields as backtick-wrapped, in registry order`` (root: string) =
        let text = skillText root "fs-gg-sdd-evidence"

        let documentedInOrder =
            RequiredKeys.requiredDeferralKeys
            |> List.filter (fun key -> text.Contains("`" + key + "`"))

        Assert.Equal<string list>(RequiredKeys.requiredDeferralKeys, documentedInOrder)

    // Behavioural: the registry deferral keys match what the evidence GATE enforces — omit any
    // one on a deferral and the gate blocks with evidence.missingDeferralRationale.
    [<Theory>]
    [<InlineData("rationale")>]
    [<InlineData("owner")>]
    [<InlineData("scope")>]
    [<InlineData("laterLifecycleVisibility")>]
    let ``Omitting any required deferral field blocks the evidence gate`` (omit: string) =
        let root = TestSupport.tempDirectory ()
        let workId = "001-example"
        let title = "Example Work Item"
        TestSupport.initializeAnalyzedProject root workId title

        let field key value =
            if key = omit then "" else sprintf "\n    %s: %s" key value

        let passes =
            [ for i in 1..5 ->
                  sprintf
                      "  - id: EV%03d\n    kind: verification\n    subject:\n      type: task\n      id: T%03d\n    result: pass"
                      i
                      i ]

        let deferral =
            "  - id: EV006\n    kind: deferral\n    subject:\n      type: task\n      id: T006\n    result: deferred\n    synthetic: false"
            + field "rationale" "Out of scope."
            + field "owner" "codex"
            + field "scope" "the deferred capability"
            + field "laterLifecycleVisibility" "Re-open later."

        let yaml =
            "schemaVersion: 1\nevidence:\n"
            + String.concat "\n" (passes @ [ deferral ])
            + "\n"

        TestSupport.writeRelative root $"work/{workId}/evidence.yml" yaml
        let report = TestSupport.runEvidence root workId title

        let errorIds =
            report.Diagnostics
            |> List.filter (fun d -> d.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError)
            |> List.map (fun d -> d.Id)

        Assert.Contains("evidence.missingDeferralRationale", errorIds)
