namespace FS.GG.SDD.Commands.Tests

open System
open FS.GG.SDD.Artifacts
open Xunit

/// SC-005 drift guard: every accepted/rejected coverage line and every
/// satisfied/unsatisfied evidence block published in
/// docs/reference/authoring-contracts.md is run through the LIVE public parsers
/// (`Specification.parseSpecificationFacts` and `Evidence.parseEvidence`). If the
/// documented contract and the tool ever disagree, this test fails — the docs
/// cannot silently drift from behavior.
module AuthoringDocsContractTests =

    let private referenceDoc =
        TestSupport.readRelative TestSupport.repoRoot "docs/reference/authoring-contracts.md"

    /// Extract the content lines of every fenced block whose info string contains
    /// the given label (e.g. "coverage:accepted").
    let private taggedBlocks (label: string) (markdown: string) : string list list =
        let lines = markdown.Replace("\r\n", "\n").Split('\n')

        let mutable blocks = []
        let mutable current: string list option = None

        for line in lines do
            let trimmed = line.TrimStart()

            if trimmed.StartsWith("```") then
                match current with
                | Some collected ->
                    // closing fence
                    blocks <- (List.rev collected) :: blocks
                    current <- None
                | None ->
                    // opening fence — capture only when the info string carries our label
                    let info = trimmed.Substring(3)

                    if info.Contains(label) then
                        current <- Some []
            else
                match current with
                | Some collected -> current <- Some(line :: collected)
                | None -> ()

        List.rev blocks

    let private nonBlankLines (block: string list) =
        block |> List.map (fun l -> l.Trim()) |> List.filter (fun l -> l <> "")

    // --- Coverage ------------------------------------------------------------

    /// Wrap a single coverage line in a minimal, valid specification snapshot and
    /// return its requirement references (populated by the real
    /// `requirementReferences`).
    let private coverageReferences (line: string) =
        let text =
            String.concat
                "\n"
                [ "---"
                  "schemaVersion: 1"
                  "workId: 001-authoring-contracts-guard"
                  "stage: specify"
                  "---"
                  ""
                  "## Functional Requirements"
                  ""
                  line ]

        match
            Specification.parseSpecificationFacts
                { Path = "work/001-authoring-contracts-guard/spec.md"
                  Text = text }
        with
        | Ok facts -> facts.RequirementReferences
        | Error diagnostics -> failwith $"Documented coverage line did not parse: {line}\n{diagnostics}"

    /// A line "establishes coverage" iff a strict-scan reference carries at least
    /// one acceptance scenario id — the same signal `requirementCoverage`/`hasCoverage`
    /// use to mark a requirement covered.
    let private establishesCoverage (line: string) =
        coverageReferences line
        |> List.exists (fun reference -> not (List.isEmpty reference.AcceptanceScenarioIds))

    [<Fact>]
    let ``Documented accepted coverage lines establish coverage in the live parser`` () =
        let lines =
            taggedBlocks "coverage:accepted" referenceDoc |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                establishesCoverage line,
                $"Doc lists this line as ACCEPTED coverage, but the parser does not cover it: {line}"
            )

    [<Fact>]
    let ``Documented rejected coverage lines establish no coverage in the live parser`` () =
        let lines =
            taggedBlocks "coverage:rejected" referenceDoc |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.False(
                establishesCoverage line,
                $"Doc lists this line as REJECTED coverage, but the parser covered it: {line}"
            )

    // --- Evidence ------------------------------------------------------------

    let private declarations (block: string list) =
        let text = String.concat "\n" block

        match
            Evidence.parseEvidence
                { Path = "work/001-authoring-contracts-guard/evidence.yml"
                  Text = text }
        with
        | Ok declarations -> declarations
        | Error diagnostics -> failwith $"Documented evidence block did not parse:\n{text}\n{diagnostics}"

    /// The non-synthetic-`pass` satisfaction rule, re-expressed here in one line
    /// because it is not exposed as a public predicate (it lives in the verify and
    /// evidence disposition ladders; T002 keeps this rule in sync with them).
    let private satisfies (declaration: Evidence.EvidenceDeclaration) =
        declaration.Result.Trim().ToLowerInvariant() = "pass"
        && not declaration.Synthetic

    [<Fact>]
    let ``Documented satisfied evidence blocks satisfy under the live parser`` () =
        let blocks = taggedBlocks "evidence:satisfied" referenceDoc
        Assert.NotEmpty blocks

        for block in blocks do
            let parsed = declarations block
            Assert.NotEmpty parsed

            Assert.All(
                parsed,
                fun declaration ->
                    Assert.True(
                        satisfies declaration,
                        $"Doc marks this declaration SATISFIED, but the non-synthetic-pass rule rejects it: {declaration.Id.Value}"
                    )
            )

    [<Fact>]
    let ``Documented unsatisfied evidence blocks do not satisfy under the live parser`` () =
        let blocks = taggedBlocks "evidence:unsatisfied" referenceDoc
        Assert.NotEmpty blocks

        for block in blocks do
            let parsed = declarations block
            Assert.NotEmpty parsed

            Assert.All(
                parsed,
                fun declaration ->
                    Assert.False(
                        satisfies declaration,
                        $"Doc marks this declaration UNSATISFIED, but the non-synthetic-pass rule accepts it: {declaration.Id.Value}"
                    )
            )

    // --- Clarify `## Remaining Ambiguity` empty-section rule (#105 Trap 1) -----

    /// Wrap one documented `## Remaining Ambiguity` bullet in a minimal clarifications
    /// snapshot and return the live `BlockingAmbiguityCount`.
    let private remainingAmbiguityBlockingCount (line: string) =
        let text =
            String.concat
                "\n"
                [ "---"
                  "schemaVersion: 1"
                  "workId: 001-authoring-contracts-guard"
                  "stage: clarify"
                  "sourceSpec: work/001-authoring-contracts-guard/spec.md"
                  "---"
                  ""
                  "## Remaining Ambiguity"
                  ""
                  line ]

        match
            Clarification.parseClarificationFacts
                { Path = "work/001-authoring-contracts-guard/clarifications.md"
                  Text = text }
        with
        | Ok facts -> facts.BlockingAmbiguityCount
        | Error diagnostics -> failwith $"Documented remaining-ambiguity line did not parse: {line}\n{diagnostics}"

    [<Fact>]
    let ``Documented remaining-ambiguity disclaimers leave zero blocking ambiguities`` () =
        let lines =
            taggedBlocks "remaining-ambiguity:disclaimer" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                remainingAmbiguityBlockingCount line = 0,
                $"Doc lists this as a DISCLAIMER, but the parser counts it as blocking: {line}"
            )

    [<Fact>]
    let ``Documented remaining-ambiguity blocking lines count as blocking`` () =
        let lines =
            taggedBlocks "remaining-ambiguity:blocking" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                remainingAmbiguityBlockingCount line > 0,
                $"Doc lists this as BLOCKING, but the parser leaves it non-blocking: {line}"
            )

    // --- Checklist `## Blocking Findings` empty-section rule (#105 Trap 2) -----

    /// Wrap one documented `## Blocking Findings` bullet in a minimal checklist snapshot
    /// and return the live parsed blocking findings.
    let private blockingFindings (line: string) =
        let text =
            String.concat
                "\n"
                [ "---"
                  "schemaVersion: 1"
                  "workId: 001-authoring-contracts-guard"
                  "stage: checklist"
                  "sourceSpec: work/001-authoring-contracts-guard/spec.md"
                  "sourceClarifications: work/001-authoring-contracts-guard/clarifications.md"
                  "---"
                  ""
                  "## Blocking Findings"
                  ""
                  line ]

        match
            Checklist.parseChecklistFacts
                { Path = "work/001-authoring-contracts-guard/checklist.md"
                  Text = text }
        with
        | Ok facts -> facts.BlockingFindings
        | Error diagnostics -> failwith $"Documented blocking-findings line did not parse: {line}\n{diagnostics}"

    [<Fact>]
    let ``Documented blocking-findings disclaimers record no finding`` () =
        let lines =
            taggedBlocks "blocking-findings:disclaimer" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                List.isEmpty (blockingFindings line),
                $"Doc lists this as a DISCLAIMER, but the parser records it as a finding: {line}"
            )

    [<Fact>]
    let ``Documented blocking-findings lines record a real finding`` () =
        let lines =
            taggedBlocks "blocking-findings:finding" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.False(
                List.isEmpty (blockingFindings line),
                $"Doc lists this as a FINDING, but the parser drops it as a placeholder: {line}"
            )
