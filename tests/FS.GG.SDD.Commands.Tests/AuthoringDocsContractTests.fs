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

    // --- Classification (ADR-0048) -------------------------------------------

    /// Wrap a single documented FR line in a minimal specification snapshot and return the
    /// classification the live `RequirementModel.parseRequirements` captured for it — the same
    /// facet that reaches the work model's `requirements[].classification`.
    let private lineClassification (line: string) =
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

        RequirementModel.parseRequirements
            { Path = "work/001-authoring-contracts-guard/spec.md"
              Text = text }
        |> List.tryHead
        |> Option.map (fun requirement -> requirement.Classification)
        |> Option.defaultWith (fun () ->
            failwith $"Documented classification line did not parse as a requirement: {line}")

    [<Fact>]
    let ``Documented gameplay-classified lines carry the gameplay facet in the live parser`` () =
        let lines =
            taggedBlocks "classification:gameplay" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                List.contains "gameplay" (lineClassification line),
                $"Doc lists this line as gameplay-classified, but the parser did not classify it: {line}"
            )

    [<Fact>]
    let ``Documented unclassified lines carry no classification in the live parser`` () =
        let lines =
            taggedBlocks "classification:unclassified" referenceDoc
            |> List.collect nonBlankLines

        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                List.isEmpty (lineClassification line),
                $"Doc lists this line as UNCLASSIFIED, but the parser classified it: {line}"
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

    // --- Clarify decision-tag resolution + front matter (feature 075) ---------

    /// Each block under these labels is a WHOLE `clarifications.md` file; parse it
    /// through the live `Clarification.parseClarificationFacts` public entry point.
    let private parseClarificationBlock (block: string list) =
        Clarification.parseClarificationFacts
            { Path = "work/001-authoring-contracts-guard/clarifications.md"
              Text = String.concat "\n" block }

    let private clarificationBlocks (label: string) =
        let blocks = taggedBlocks label referenceDoc
        Assert.NotEmpty blocks
        blocks

    [<Fact>]
    let ``Documented resolved decision blocks attach the ambiguity and leave zero blocking`` () =
        for block in clarificationBlocks "clarify-decision:resolved" do
            match parseClarificationBlock block with
            | Ok facts ->
                Assert.Equal(0, facts.BlockingAmbiguityCount)

                Assert.True(
                    facts.Decisions
                    |> List.exists (fun decision -> not (List.isEmpty decision.SourceAmbiguityIds)),
                    "Doc marks this as a RESOLVED decision, but no `## Decisions` line carries an AMB id"
                )
            | Error diagnostics -> failwith $"Documented resolved-decision block did not parse:\n{diagnostics}"

    [<Fact>]
    let ``Documented deferred decision blocks attach the ambiguity via an accepted deferral`` () =
        for block in clarificationBlocks "clarify-decision:deferred" do
            match parseClarificationBlock block with
            | Ok facts ->
                Assert.Equal(0, facts.BlockingAmbiguityCount)

                Assert.True(
                    facts.AcceptedDeferrals
                    |> List.exists (fun decision -> not (List.isEmpty decision.SourceAmbiguityIds)),
                    "Doc marks this as an accepted DEFERRAL, but no `## Accepted Deferrals` line carries an AMB id"
                )
            | Error diagnostics -> failwith $"Documented deferred-decision block did not parse:\n{diagnostics}"

    [<Fact>]
    let ``Documented answer-only decision blocks leave the ambiguity blocking`` () =
        for block in clarificationBlocks "clarify-decision:answer-does-not-resolve" do
            match parseClarificationBlock block with
            | Ok facts ->
                Assert.True(
                    facts.BlockingAmbiguityCount > 0,
                    "Doc says an answer alone does NOT resolve, but the parser left zero blocking ambiguities"
                )
            | Error diagnostics -> failwith $"Documented answer-only block did not parse:\n{diagnostics}"

    [<Fact>]
    let ``Documented duplicate decision blocks are flagged as duplicate ids`` () =
        for block in clarificationBlocks "clarify-dup:rejected" do
            let diagnostics =
                match parseClarificationBlock block with
                | Ok facts -> facts.Diagnostics
                | Error diagnostics -> diagnostics

            Assert.True(
                diagnostics
                |> List.exists (fun diagnostic -> diagnostic.Id = "duplicateIdentifier"),
                "Doc marks this as a DUPLICATE id, but the parser did not flag `duplicateIdentifier`"
            )

    [<Fact>]
    let ``Documented minimal clarify front matter parses without an incomplete error`` () =
        for block in clarificationBlocks "front-matter:clarify-minimal" do
            match parseClarificationBlock block with
            | Ok _ -> ()
            | Error diagnostics ->
                failwith $"Doc marks this front matter as MINIMAL/accepted, but the parser rejected it:\n{diagnostics}"

    [<Fact>]
    let ``Documented clarify front matter missing a gating field is rejected as incomplete`` () =
        for block in clarificationBlocks "front-matter:clarify-missing-required" do
            match parseClarificationBlock block with
            | Ok _ -> failwith "Doc marks this front matter as INCOMPLETE, but the parser accepted it"
            | Error diagnostics ->
                Assert.True(
                    diagnostics
                    |> List.exists (fun diagnostic -> diagnostic.Id = "workModelInconsistent"),
                    $"Expected an incomplete-front-matter diagnostic, got:\n{diagnostics}"
                )
