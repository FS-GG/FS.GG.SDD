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
        let mutable current : string list option = None

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
                    if info.Contains(label) then current <- Some []
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
            String.concat "\n"
                [ "---"
                  "schemaVersion: 1"
                  "workId: 001-authoring-contracts-guard"
                  "stage: specify"
                  "---"
                  ""
                  "## Functional Requirements"
                  ""
                  line ]

        match Specification.parseSpecificationFacts { Path = "work/001-authoring-contracts-guard/spec.md"; Text = text } with
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
        let lines = taggedBlocks "coverage:accepted" referenceDoc |> List.collect nonBlankLines
        Assert.NotEmpty lines

        for line in lines do
            Assert.True(
                establishesCoverage line,
                $"Doc lists this line as ACCEPTED coverage, but the parser does not cover it: {line}")

    [<Fact>]
    let ``Documented rejected coverage lines establish no coverage in the live parser`` () =
        let lines = taggedBlocks "coverage:rejected" referenceDoc |> List.collect nonBlankLines
        Assert.NotEmpty lines

        for line in lines do
            Assert.False(
                establishesCoverage line,
                $"Doc lists this line as REJECTED coverage, but the parser covered it: {line}")

    // --- Evidence ------------------------------------------------------------

    let private declarations (block: string list) =
        let text = String.concat "\n" block

        match Evidence.parseEvidence { Path = "work/001-authoring-contracts-guard/evidence.yml"; Text = text } with
        | Ok declarations -> declarations
        | Error diagnostics -> failwith $"Documented evidence block did not parse:\n{text}\n{diagnostics}"

    /// The non-synthetic-`pass` satisfaction rule, re-expressed here in one line
    /// because it is not exposed as a public predicate (it lives in the verify and
    /// evidence disposition ladders; T002 keeps this rule in sync with them).
    let private satisfies (declaration: Evidence.EvidenceDeclaration) =
        declaration.Result.Trim().ToLowerInvariant() = "pass" && not declaration.Synthetic

    [<Fact>]
    let ``Documented satisfied evidence blocks satisfy under the live parser`` () =
        let blocks = taggedBlocks "evidence:satisfied" referenceDoc
        Assert.NotEmpty blocks

        for block in blocks do
            let parsed = declarations block
            Assert.NotEmpty parsed
            Assert.All(parsed, fun declaration -> Assert.True(satisfies declaration, $"Doc marks this declaration SATISFIED, but the non-synthetic-pass rule rejects it: {declaration.Id.Value}"))

    [<Fact>]
    let ``Documented unsatisfied evidence blocks do not satisfy under the live parser`` () =
        let blocks = taggedBlocks "evidence:unsatisfied" referenceDoc
        Assert.NotEmpty blocks

        for block in blocks do
            let parsed = declarations block
            Assert.NotEmpty parsed
            Assert.All(parsed, fun declaration -> Assert.False(satisfies declaration, $"Doc marks this declaration UNSATISFIED, but the non-synthetic-pass rule accepts it: {declaration.Id.Value}"))
